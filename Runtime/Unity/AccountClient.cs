using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiANN.MahjongTones.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace LiANN.MahjongTones
{
    /// <summary>
    /// Optional sign-in and SetList upload (spec §13, D16). Signs in with the same Supabase email code as Pinyin Tiles
    /// (the project URL and publishable key come from the Pinyin API's public /v1/auth/config), then sends saved words
    /// to AlongSide's POST /learning/game-events with the bearer token. Playing never needs an account; SetLists stay
    /// on the device and upload later if AlongSide is unreachable or not yet accepting this game.
    /// </summary>
    public sealed class AccountClient
    {
        public enum State { Loading, Disabled, Unavailable, SignedOut, CodeSent, SignedIn }

        [Serializable] class Endpoints { public string pinyinApiBase, alongsideApiBase, supabaseHost; }
        [Serializable] class AuthConfig { public bool enabled; public string supabaseUrl, publishableKey; }
        [Serializable] class AuthConfigResponse { public AuthConfig authentication; }
        [Serializable] class UserDto { public string id, email; }
        [Serializable] class SessionDto { public string access_token, refresh_token; public long expires_in, expires_at; public UserDto user; }
        [Serializable] class UploadResult { public int inserted, duplicates, added, existing, unresolved; public string learningStatus; }
        [Serializable] class PassResult { public string url; }
        [Serializable] class HouseholdInfo { public string id, plan, role; }
        [Serializable] class ChildDto { public string id, name; }
        [Serializable] class HouseholdDto { public HouseholdInfo household; public ChildDto[] children; }
        [Serializable] class Saved
        {
            public string email, accessToken, refreshToken, userId, pendingEmail;
            public long expiresAt;
            public bool profileNeeded;   // AlongSide said the learner profile is missing; cleared when an upload links words
            public string learner = "", learnerName = "";  // who is playing: "" for this account's adult, or a child's profile id
            public List<string> uploaded = new List<string>();   // event ids already accepted for userId
        }

        public State Status { get; private set; } = State.Loading;
        /// <summary>A sign-in is in progress or complete on this device. Only then does the app contact the services at
        /// launch; otherwise nothing is fetched until the player opens the Account screen.</summary>
        public bool HasSession => !string.IsNullOrEmpty(_saved.refreshToken) || !string.IsNullOrEmpty(_saved.pendingEmail);
        public string Email => _saved.email;

        // ---- Who's playing (One LiANN account C3) ----
        // Children come from AlongSide's household (Family plan). A 404 means households are switched off: no choice shown.
        List<(string id, string name)> _children = new List<(string id, string name)>();
        public IReadOnlyList<(string id, string name)> Children => _children;
        /// <summary>True once the household has been asked for in this session (successfully or not).</summary>
        public bool HouseholdLoaded { get; private set; }
        /// <summary>The child playing now, or null when it is this account's adult.</summary>
        public string LearnerName => string.IsNullOrEmpty(_saved.learner) ? null : _saved.learnerName;
        public string LearnerId => _saved.learner ?? "";

        public async Task RefreshHousehold()
        {
            HouseholdLoaded = true;   // asked once per session, whatever the answer, so screens never loop on it
            if (Status != State.SignedIn || !await Refresh(false)) return;
            var (ok, code, text) = await Send("GET", Url(_ends.alongsideApiBase, "/learning/household"), null, _saved.accessToken);
            if (!ok)
            {
                if (code == 404 || code == 401 || code == 403) _children.Clear();   // switched off, or not allowed: no child profiles
                else return;                                                         // unreachable: keep what we had
            }
            else
            {
                HouseholdDto dto = null;
                try { dto = JsonUtility.FromJson<HouseholdDto>(text); } catch { dto = null; }
                _children = (dto?.children ?? new ChildDto[0]).Where(c => !string.IsNullOrEmpty(c?.id) && !string.IsNullOrEmpty(c.name))
                    .Select(c => (c.id, c.name)).ToList();
            }
            SetLearner(_saved.learner);
        }

        /// <summary>Chooses who is playing; anything that is not a current child of this household means the adult.</summary>
        public void SetLearner(string profileId)
        {
            var child = _children.FirstOrDefault(c => c.id == profileId);
            _saved.learner = child.id ?? "";
            _saved.learnerName = child.name ?? "";
            SetListStore.ActiveLearner = _saved.learner;
            Persist();
        }
        /// <summary>AlongSide needs a learner profile before uploaded words reach a SetList (One LiANN account, B3).</summary>
        public bool ProfileNeeded => _saved.profileNeeded;
        /// <summary>The line under the account form. A refused "send" shows a live countdown that clears itself at 0.</summary>
        public string Message
        {
            get => !_cooldownNote ? _message : ResendInSeconds > 0 ? $"Check your inbox. You can ask for another code in {ResendInSeconds} s." : "";
            private set { _message = value; _cooldownNote = false; }
        }
        string _message = "";
        bool _cooldownNote;

        readonly Endpoints _ends;
        AuthConfig _auth;
        Saved _saved;
        bool _legacyCacheRemoved;

        public AccountClient()
        {
            var cfg = Resources.Load<TextAsset>("LiANNMahjongTones/accounts");
            _ends = cfg ? JsonUtility.FromJson<Endpoints>(cfg.text) : new Endpoints();
            // Never deserialize the old plaintext token file. Remove it before sign-in.
            _legacyCacheRemoved = ProtectedAccountStore.RemoveLegacyPlaintext();
            try { _saved = _legacyCacheRemoved ? JsonUtility.FromJson<Saved>(ProtectedAccountStore.Load()) : null; }
            catch { _saved = null; }
            _saved ??= new Saved();
            _saved.uploaded ??= new List<string>();
            _saved.learner ??= ""; _saved.learnerName ??= "";
            SetListStore.ActiveLearner = _saved.learner;
        }

        bool Persist()
        {
            // Unsupported platforms and the Editor intentionally keep tokens in memory only.
            return ProtectedAccountStore.Save(JsonUtility.ToJson(_saved));
        }

        /// <summary>Reads the public auth config. Safe to call again, for example after a connection error.</summary>
        public async Task Init()
        {
            if (!_legacyCacheRemoved && !(_legacyCacheRemoved = ProtectedAccountStore.RemoveLegacyPlaintext()))
            {
                Status = State.Unavailable;
                Message = "The old account cache could not be removed. Clear this app's data before signing in.";
                return;
            }
            if (string.IsNullOrEmpty(_ends.pinyinApiBase) || string.IsNullOrEmpty(_ends.alongsideApiBase)) { Status = State.Disabled; return; }
            Status = State.Loading;
            var (ok, code, text) = await Send("GET", Url(_ends.pinyinApiBase, "/v1/auth/config"), null, null);
            if (!ok) { Status = State.Unavailable; Message = "The sign-in service can't be reached. Your SetLists stay on this device."; return; }
            _auth = JsonUtility.FromJson<AuthConfigResponse>(text)?.authentication;
            if (_auth == null || !_auth.enabled || string.IsNullOrEmpty(_auth.supabaseUrl) || string.IsNullOrEmpty(_auth.publishableKey))
            { Status = State.Disabled; Message = "Sign-in is switched off at the moment."; return; }
            // The email, code and refresh token go to this address, so a remote config may not redirect them elsewhere.
            if (!TrustedAuthUrl.IsAllowed(_auth.supabaseUrl, _ends.supabaseHost))
            { _auth = null; Status = State.Unavailable; Message = "Sign-in is unavailable: the sign-in service sent an unexpected address."; return; }
            Status = !string.IsNullOrEmpty(_saved.refreshToken) ? State.SignedIn : !string.IsNullOrEmpty(_saved.pendingEmail) ? State.CodeSent : State.SignedOut;
            Message = "";
        }

        /// <summary>True while a code request is in flight; the account screen disables its button.</summary>
        public bool SendingCode { get; private set; }
        DateTime _nextCodeAllowed = DateTime.MinValue;
        /// <summary>Seconds before another code may be requested (each request sends an email and counts towards
        /// Supabase's email limit, so repeated clicks are refused for a minute).</summary>
        public int ResendInSeconds => Math.Max(0, (int)Math.Ceiling((_nextCodeAllowed - DateTime.UtcNow).TotalSeconds));
        const int CodeCooldownSeconds = 60;

        public async Task<bool> SendCode(string email)
        {
            if (_auth == null || !_legacyCacheRemoved || SendingCode) return false;
            if (ResendInSeconds > 0) { _cooldownNote = true; return false; }
            email = (email ?? "").Trim();
            if (email.Length == 0) { Message = "Enter your email address."; return false; }
            if (!EmailAddress.LooksValid(email)) { Message = $"Check the email address: \"{email}\" isn't a valid address."; return false; }
            string body = "{\"email\":" + SetListEvents.Q(email) + ",\"create_user\":true,\"data\":{}}";
            SendingCode = true;
            try
            {
                var (ok, code, _) = await Send("POST", Url(_auth.supabaseUrl, "/auth/v1/otp"), body, null, true);
                if (!ok)
                {
                    Message = code == 429 ? "Too many codes were requested. Wait a few minutes, then try again."
                        : "The email couldn't be sent. Check the address and try again in a moment.";
                    return false;
                }
                _nextCodeAllowed = DateTime.UtcNow.AddSeconds(CodeCooldownSeconds);
                _saved.pendingEmail = email; Persist();
                Status = State.CodeSent; Message = "We emailed you a code. Enter it below.";
                return true;
            }
            finally { SendingCode = false; }
        }

        /// <summary>Sends a new code to the address waiting for one (code screen), under the same cooldown.</summary>
        public Task<bool> ResendCode() => SendCode(_saved.pendingEmail);

        public void ChangeEmail() { _saved.pendingEmail = null; Persist(); Status = State.SignedOut; Message = ""; }

        public async Task<bool> VerifyCode(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length < 6 || !code.All(char.IsDigit)) { Message = "Enter the 6-digit code from the email."; return false; }
            string body = "{\"email\":" + SetListEvents.Q(_saved.pendingEmail) + ",\"token\":" + SetListEvents.Q(code) + ",\"type\":\"email\"}";
            var (ok, _, text) = await Send("POST", Url(_auth.supabaseUrl, "/auth/v1/verify"), body, null, true);
            if (!ok) { Message = "That code is wrong, expired or already used. Request a new one."; return false; }
            if (!Store(JsonUtility.FromJson<SessionDto>(text), _saved.pendingEmail))
            { Message = "Sign-in could not be completed. Request a new code."; return false; }
            _saved.pendingEmail = null;
            bool stored = Persist();
            Status = State.SignedIn;
            Message = stored ? "" : "Signed in for this session only. Secure storage is unavailable.";
            return true;
        }

        public async Task SignOut()
        {
            if (!string.IsNullOrEmpty(_saved.accessToken) && _auth != null)
                await Send("POST", Url(_auth.supabaseUrl, "/auth/v1/logout"), "{}", _saved.accessToken, true);
            _saved = new Saved();
            SetListStore.ActiveLearner = ""; _children.Clear(); HouseholdLoaded = false;
            bool cleared = ProtectedAccountStore.Delete();
            Status = !cleared ? State.Unavailable : _auth != null && _auth.enabled ? State.SignedOut : State.Disabled;
            Message = cleared ? "Signed out. Your SetLists stay on this device."
                : "The protected session could not be cleared. Clear this app's data before sharing this device.";
        }

        bool Store(SessionDto s, string email)
        {
            if (s == null || string.IsNullOrEmpty(s.access_token)) return false;
            string newUser = s.user?.id;
            if (!string.IsNullOrEmpty(newUser) && newUser != _saved.userId)
            {
                _saved.uploaded = new List<string>();   // markers belong to one account
                _saved.learner = ""; _saved.learnerName = ""; SetListStore.ActiveLearner = ""; _children.Clear(); HouseholdLoaded = false;   // so does the household
            }
            _saved.accessToken = s.access_token;
            _saved.refreshToken = string.IsNullOrEmpty(s.refresh_token) ? _saved.refreshToken : s.refresh_token;
            _saved.expiresAt = s.expires_at > 0 ? s.expires_at : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, s.expires_in);
            _saved.userId = newUser ?? _saved.userId;
            _saved.email = s.user?.email ?? email ?? _saved.email;
            return true;
        }

        async Task<bool> Refresh(bool force)
        {
            if (string.IsNullOrEmpty(_saved.refreshToken)) return false;
            if (!force && _saved.expiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60) return true;
            string body = "{\"refresh_token\":" + SetListEvents.Q(_saved.refreshToken) + "}";
            var (ok, code, text) = await Send("POST", Url(_auth.supabaseUrl, "/auth/v1/token?grant_type=refresh_token"), body, null, true);
            if (!ok)
            {
                if (code == 400 || code == 401) { _saved.accessToken = _saved.refreshToken = null; Persist(); Status = State.SignedOut; Message = "Please sign in again."; }
                return false;
            }
            if (!Store(JsonUtility.FromJson<SessionDto>(text), _saved.email)) return false;
            Persist();
            return true;
        }

        /// <summary>Saved words not yet accepted by AlongSide for this account.</summary>
        public List<GameEvent> Pending() => PendingByLearner().SelectMany(g => g.events).ToList();

        /// <summary>Pending saves per learner: "" for this account's adult, else a child's profile id. A child's lists upload
        /// only while that child is in this account's household, so another adult's child never reaches this account.</summary>
        List<(string learner, List<GameEvent> events)> PendingByLearner()
        {
            var done = new HashSet<string>(_saved.uploaded);
            var groups = new List<(string learner, List<GameEvent> events)>();
            foreach (var list in SetListStore.Load().lists)
            {
                string learner = list.learner ?? "";
                if (learner != "" && !_children.Any(c => c.id == learner)) continue;
                var events = SetListEvents.ForList(list.id, list.name, list.words.Select(w => (w.entryId, w.senseId, w.hanzi, w.savedAt)))
                    .Where(e => !done.Contains(e.EventId)).ToList();
                if (events.Count == 0) continue;
                int at = groups.FindIndex(g => g.learner == learner);
                if (at < 0) groups.Add((learner, events)); else groups[at].events.AddRange(events);
            }
            return groups;
        }

        /// <summary>Uploads pending saves. Returns a short message for the player.</summary>
        public async Task<string> Upload()
        {
            if (Status != State.SignedIn) return "Sign in to save your SetLists to AlongSide.";
            if (!HouseholdLoaded) await RefreshHousehold();   // a child's lists upload only once their household is known
            var groups = PendingByLearner();
            if (groups.Count == 0) return "Your SetLists are up to date in AlongSide.";
            if (!await Refresh(false)) return Status == State.SignedOut ? "Please sign in again to upload." : "AlongSide can't be reached. Your SetLists are kept and will upload later.";
            int added = 0, unresolved = 0, sent = 0; bool profileNeeded = false, planNeeded = false; string childGone = null;
            foreach (var (learner, pending) in groups)
            {
                // A household child's saves carry their profile id and land in their own SetLists (One LiANN account C3).
                var bodies = SetListEvents.Bodies(pending, learner == "" ? null : learner);
                for (int i = 0; i < bodies.Count; i++)
                {
                    var (ok, code, text) = await Send("POST", Url(_ends.alongsideApiBase, "/learning/game-events"), bodies[i], _saved.accessToken, timeout: UploadTimeoutSeconds);
                    if (!ok && code == 401 && await Refresh(true)) (ok, code, text) = await Send("POST", Url(_ends.alongsideApiBase, "/learning/game-events"), bodies[i], _saved.accessToken, timeout: UploadTimeoutSeconds);
                    if (!ok && code == 403 && learner != "" && text.Contains("profile_not_in_household"))
                    {
                        childGone = _children.FirstOrDefault(c => c.id == learner).name ?? "A child";   // removed from the household
                        break;
                    }
                    if (!ok)
                    {
                        Persist();
                        // The route is missing or not accepting this source yet (backend PR #102 not deployed): keep the words.
                        if (code == 409 && text.Contains("account_link_required"))
                            return "This sign-in isn't linked to AlongSide yet. Open \"Link your LiANN account\" in AlongSide, then upload again. Your SetLists are kept.";
                        if (code == 400 || code == 403 || code == 404 || code == 405 || code == 501) return "AlongSide isn't taking Mahjong Tones saves yet. They're kept on this device and will upload later.";
                        Debug.LogWarning($"LiANN Mahjong Tones: upload failed (HTTP {code}).");
                        return code == 0 ? "AlongSide can't be reached. Your SetLists are kept and will upload later."
                            : $"AlongSide couldn't save your SetLists (error {code}). They're kept and will upload later.";
                    }
                    var r = JsonUtility.FromJson<UploadResult>(text) ?? new UploadResult();
                    added += r.added; unresolved += r.unresolved; planNeeded |= r.learningStatus == "plan_required";
                    // The learner-profile prompt is about this account's adult; a child's record is made with their profile.
                    if (learner == "") profileNeeded |= r.learningStatus == "learner_profile_required";
                    if (learner == "" && (r.learningStatus == "linked" || r.learningStatus == "some_words_unresolved")) _saved.profileNeeded = false;
                    // Without a learner profile (or a plan) AlongSide keeps the saves but files them nowhere; they stay pending here and are
                    // sent again after the profile is finished, when AlongSide files them (resending is safe: event ids dedupe).
                    if (r.learningStatus != "learner_profile_required" && r.learningStatus != "plan_required")
                        foreach (var e in pending.Skip(i * SetListEvents.MaxBatch).Take(SetListEvents.MaxBatch)) _saved.uploaded.Add(e.EventId);
                    sent += Math.Min(SetListEvents.MaxBatch, pending.Count - i * SetListEvents.MaxBatch);
                }
            }
            if (profileNeeded) _saved.profileNeeded = true;
            Persist();
            // One LiANN account C2: saving into AlongSide SetLists needs a plan or a tutor account (when AlongSide enforces it).
            if (planNeeded) return "Saving to AlongSide SetLists needs a LiANN plan or a tutor account. Your words stay in your SetLists on this device.";
            if (profileNeeded) return $"{sent} words sent. Finish your learner profile in AlongSide and they'll appear in your SetLists.";
            string gone = childGone == null ? "" : $" {childGone}'s profile is no longer in your household, so their words stay on this device.";
            return $"{sent} words sent to AlongSide" + (added > 0 ? $", {added} new" : "") + (unresolved > 0 ? $"; {unresolved} need checking" : "") + "." + gone;
        }

        /// <summary>
        /// Opens AlongSide's learner profile form in the browser, already signed in with this account: asks AlongSide for a
        /// one-time pass (60 seconds, single use) and opens the address it returns. Only AlongSide's own addresses are opened.
        /// Returns a short message for the player.
        /// </summary>
        public async Task<string> OpenProfile()
        {
            if (Status != State.SignedIn) return "Sign in first, then finish your learner profile.";
            string form = ProfileLink.FormUrl(_ends.alongsideApiBase);
            if (!await Refresh(false)) return Status == State.SignedOut ? "Please sign in again." : "AlongSide can't be reached. Try again in a moment.";
            string body = "{\"next\":" + SetListEvents.Q(ProfileLink.FormPath) + "}";
            string passUrl = Url(_ends.alongsideApiBase, ProfileLink.PassPath);
            var (ok, code, text) = await Send("POST", passUrl, body, _saved.accessToken, timeout: UploadTimeoutSeconds);
            if (!ok && code == 401 && await Refresh(true)) (ok, code, text) = await Send("POST", passUrl, body, _saved.accessToken, timeout: UploadTimeoutSeconds);
            if (!ok && code == 404)
            {
                // An AlongSide without the pass yet: open the form, where the player signs in with the same email.
                Application.OpenURL(form);
                return "AlongSide is open in your browser. Sign in there with " + _saved.email + ", then fill in your profile.";
            }
            if (!ok)
            {
                Debug.LogWarning($"LiANN Mahjong Tones: profile link failed (HTTP {code}).");
                return code == 409 ? "This email is already used by a different LiANN sign-in in AlongSide. Sign in there the way you did before."
                    : code == 0 ? "AlongSide can't be reached. Try again in a moment." : $"AlongSide couldn't open your profile (error {code}). Try again in a moment.";
            }
            string url = null;
            try { url = JsonUtility.FromJson<PassResult>(text)?.url; } catch { url = null; }
            if (!ProfileLink.IsOnAlongSide(url, _ends.alongsideApiBase)) return "AlongSide sent an unexpected address, so nothing was opened.";
            Application.OpenURL(url);
            return "AlongSide is open in your browser. Save your profile there, then come back and upload again.";
        }

        static string Url(string b, string path) => b.TrimEnd('/') + path;

        // AlongSide uploads wait longer: a sleeping staging server takes about 20 s to wake (DSJ-PC retest, 13 Oct).
        const int UploadTimeoutSeconds = 60;

        async Task<(bool ok, long code, string text)> Send(string method, string url, string body, string bearer, bool supabase = false, int timeout = 20)
        {
            using var req = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = timeout };
            if (body != null) { req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); req.SetRequestHeader("Content-Type", "application/json"); }
            if (supabase && _auth != null) req.SetRequestHeader("apikey", _auth.publishableKey);
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            var tcs = new TaskCompletionSource<bool>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
            await tcs.Task;
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.text ?? "");
        }
    }
}

