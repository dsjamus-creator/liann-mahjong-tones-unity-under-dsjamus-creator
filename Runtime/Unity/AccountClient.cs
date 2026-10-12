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

        [Serializable] class Endpoints { public string pinyinApiBase, alongsideApiBase; }
        [Serializable] class AuthConfig { public bool enabled; public string supabaseUrl, publishableKey; }
        [Serializable] class AuthConfigResponse { public AuthConfig authentication; }
        [Serializable] class UserDto { public string id, email; }
        [Serializable] class SessionDto { public string access_token, refresh_token; public long expires_in, expires_at; public UserDto user; }
        [Serializable] class UploadResult { public int inserted, duplicates, added, existing, unresolved; public string learningStatus; }
        [Serializable] class Saved
        {
            public string email, accessToken, refreshToken, userId, pendingEmail;
            public long expiresAt;
            public List<string> uploaded = new List<string>();   // event ids already accepted for userId
        }

        public State Status { get; private set; } = State.Loading;
        public string Email => _saved.email;
        public string Message { get; private set; } = "";

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
            if (ResendInSeconds > 0) { Message = $"Check your inbox. You can ask for another code in {ResendInSeconds} s."; return false; }
            email = (email ?? "").Trim();
            if (!email.Contains("@") || email.Length < 5) { Message = "Enter your email address."; return false; }
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

        public void ChangeEmail() { _saved.pendingEmail = null; Persist(); Status = State.SignedOut; Message = ""; }

        public async Task<bool> VerifyCode(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length < 6 || !code.All(char.IsDigit)) { Message = "Enter the code from the email (6 to 8 digits)."; return false; }
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
            bool cleared = ProtectedAccountStore.Delete();
            Status = !cleared ? State.Unavailable : _auth != null && _auth.enabled ? State.SignedOut : State.Disabled;
            Message = cleared ? "Signed out. Your SetLists stay on this device."
                : "The protected session could not be cleared. Clear this app's data before sharing this device.";
        }

        bool Store(SessionDto s, string email)
        {
            if (s == null || string.IsNullOrEmpty(s.access_token)) return false;
            string newUser = s.user?.id;
            if (!string.IsNullOrEmpty(newUser) && newUser != _saved.userId) _saved.uploaded = new List<string>();   // markers belong to one account
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
        public List<GameEvent> Pending()
        {
            var done = new HashSet<string>(_saved.uploaded);
            var all = new List<GameEvent>();
            foreach (var list in SetListStore.Load().lists)
                all.AddRange(SetListEvents.ForList(list.id, list.name, list.words.Select(w => (w.entryId, w.senseId, w.hanzi, w.savedAt))));
            return all.Where(e => !done.Contains(e.EventId)).ToList();
        }

        /// <summary>Uploads pending saves. Returns a short message for the player.</summary>
        public async Task<string> Upload()
        {
            if (Status != State.SignedIn) return "Sign in to save your SetLists to AlongSide.";
            var pending = Pending();
            if (pending.Count == 0) return "Your SetLists are up to date in AlongSide.";
            if (!await Refresh(false)) return Status == State.SignedOut ? "Please sign in again to upload." : "AlongSide can't be reached. Your SetLists are kept and will upload later.";
            int added = 0, unresolved = 0, sent = 0; bool profileNeeded = false;
            var bodies = SetListEvents.Bodies(pending);
            for (int i = 0; i < bodies.Count; i++)
            {
                var (ok, code, text) = await Send("POST", Url(_ends.alongsideApiBase, "/learning/game-events"), bodies[i], _saved.accessToken);
                if (!ok && code == 401 && await Refresh(true)) (ok, code, text) = await Send("POST", Url(_ends.alongsideApiBase, "/learning/game-events"), bodies[i], _saved.accessToken);
                if (!ok)
                {
                    Persist();
                    // The route is missing or not accepting this source yet (backend PR #102 not deployed): keep the words.
                    if (code == 400 || code == 403 || code == 404 || code == 405 || code == 501) return "AlongSide isn't taking Mahjong Tones saves yet. They're kept on this device and will upload later.";
                    return "AlongSide can't be reached. Your SetLists are kept and will upload later.";
                }
                var r = JsonUtility.FromJson<UploadResult>(text) ?? new UploadResult();
                added += r.added; unresolved += r.unresolved; profileNeeded |= r.learningStatus == "learner_profile_required";
                foreach (var e in pending.Skip(i * SetListEvents.MaxBatch).Take(SetListEvents.MaxBatch)) _saved.uploaded.Add(e.EventId);
                sent += Math.Min(SetListEvents.MaxBatch, pending.Count - i * SetListEvents.MaxBatch);
            }
            Persist();
            if (profileNeeded) return $"{sent} words sent. They'll appear in AlongSide once your learner profile is set up.";
            return $"{sent} words sent to AlongSide" + (added > 0 ? $", {added} new" : "") + (unresolved > 0 ? $"; {unresolved} need checking" : "") + ".";
        }

        static string Url(string b, string path) => b.TrimEnd('/') + path;

        async Task<(bool ok, long code, string text)> Send(string method, string url, string body, string bearer, bool supabase = false)
        {
            using var req = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = 20 };
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

