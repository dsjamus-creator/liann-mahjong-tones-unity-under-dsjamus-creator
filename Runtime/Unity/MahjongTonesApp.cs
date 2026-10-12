using System;
using System.Collections.Generic;
using System.Linq;
using LiANN.MahjongTones.Core;
using UnityEngine;
using UnityEngine.UIElements;
using PlayMode = LiANN.MahjongTones.Core.PlayMode;

namespace LiANN.MahjongTones
{
    /// <summary>
    /// LiANN Pinyin Mahjong Tones (Game 3), Unity edition. Add this one component to an empty GameObject in an
    /// empty scene: it builds the whole UI Toolkit interface in code (start, board, tone picker, results, stats).
    /// Rules live in LiANN.MahjongTones.Core and match the web edition (spec §4.1, §6.1).
    /// </summary>
    [AddComponentMenu("LiANN/Mahjong Tones App")]
    public sealed class MahjongTonesApp : MonoBehaviour
    {
        [Tooltip("Optional: a PanelSettings asset with a theme. If empty, one is created at run time.")]
        [SerializeField] PanelSettings panelSettings;
        [SerializeField] bool darkTheme;
        [SerializeField] bool reducedMotion;

        enum Source { Hsk1, Hsk2, Hsk3, TonePairs, Daily }

        // settings
        PlayMode _mode = PlayMode.Practice;
        /// <summary>At least 600 density-independent pixels on the short side (tablet or desktop).</summary>
        static bool WideScreen
        {
            get
            {
                if (Application.isEditor || !Application.isMobilePlatform) return true;
                float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
                return Mathf.Min(Screen.width, Screen.height) / (dpi / 160f) >= 600f;
            }
        }
        string _layoutName = "Starter";
        Source _source = Source.Hsk1;
        bool _showPinyin = true, _eye;
        // Pictures on meaning tiles (spec D13): off by default, because a picture can give the answer away.
        bool _showPictures;   // loaded in Awake (PlayerPrefs is not allowed in field initializers)
        string _emailDraft = "";   // kept while the account screen re-renders (cooldown countdown)

        // runtime
        readonly Tweens _tweens = new Tweens();
        Game _game;
        Font _hanziFont, _uiFont;
        AudioSource _audio;
        VisualElement _root, _screen, _board, _picker;
        AccountClient _account;
        Label _score, _tilesLeft, _pairs, _time, _seed, _feedback, _hintBtnLabel, _undoBtnLabel, _shuffleBtnLabel;
        readonly Dictionary<int, TileView> _tiles = new Dictionary<int, TileView>();
        float _elapsed, _hintUntil;
        (int a, int b)? _hint;
        bool _roundOver, _busy;
        List<int> _pickedTones;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Theme.Dark = darkTheme;
            _tweens.ReducedMotion = reducedMotion;
            _showPictures = PlayerPrefs.GetInt("lmt.pictures", 0) == 1;
            _hanziFont = Resources.Load<Font>("LiANNMahjongTones/Fonts/NotoSansSC-Bold");
            _uiFont = Resources.Load<Font>("LiANNMahjongTones/Fonts/Nunito-ExtraBold");
            if (_hanziFont == null || _uiFont == null) Debug.LogError("LiANN Mahjong Tones: fonts missing from Resources/LiANNMahjongTones/Fonts.");
            if (_uiFont == null) _uiFont = _hanziFont;
            _account = new AccountClient();
            _ = _account.Init();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            // The documented setup is an empty scene with no camera, so there may be no listener and audio would be silent.
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
#else
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
#endif

            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(900, 1400);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                // Unity's default runtime theme gives ScrollView its clipping and scrollbar; inline styles still win.
                panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("LiANNMahjongTones/Theme");
            }
            var host = new GameObject("LiANN Mahjong Tones UI");
            host.transform.SetParent(transform, false);
            host.SetActive(false);
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            host.SetActive(true);

            _root = doc.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.unityFontDefinition = FontDefinition.FromFont(_uiFont);
            _root.focusable = true;
            _root.RegisterCallback<KeyDownEvent>(OnKey);
            ShowStart();
        }

        void Update()
        {
            _tweens.Update();
            if (_game == null || _roundOver || _screen == null) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_mode == PlayMode.Challenge)
            {
                float left = Scoring.CountdownSeconds(_layoutName) - _elapsed;
                if (_time != null) _time.text = Clock(Mathf.Max(0, left));
                if (left <= 0) { EndRound(false); return; }
            }
            else if (_time != null) _time.text = Clock(_elapsed);

            if (_hint.HasValue)
            {
                if (Time.unscaledTime > _hintUntil) { ClearHint(); }
                else
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                    if (_tiles.TryGetValue(_hint.Value.a, out var ta)) ta.SetHint(true, pulse);
                    if (_tiles.TryGetValue(_hint.Value.b, out var tb)) tb.SetHint(true, pulse);
                }
            }
        }

        static string Clock(float s) => $"{(int)s / 60}:{(int)s % 60:00}";

        // ------------------------------------------------------------------ UI helpers

        VisualElement NewScreen()
        {
            ClosePicker();
            _root.Clear();
            _tiles.Clear();
            _tweens.Clear();
            _root.style.backgroundColor = Theme.Ground;
            _screen = new VisualElement();
            _screen.style.flexGrow = 1;
            _screen.style.paddingLeft = 24; _screen.style.paddingRight = 24; _screen.style.paddingTop = 24; _screen.style.paddingBottom = 24;
            _root.Add(_screen);
            _root.Focus();
            return _screen;
        }

        Label Text(string text, float size, Color color, bool heavy = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = heavy ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 0; l.style.marginBottom = 0;
            return l;
        }

        static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r; }
        static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
        static void Pad(VisualElement e, float v, float h) { e.style.paddingTop = v; e.style.paddingBottom = v; e.style.paddingLeft = h; e.style.paddingRight = h; }

        /// <summary>A pill button; plain VisualElement + ClickEvent so it does not depend on a theme style sheet.</summary>
        VisualElement Button(string text, Action onClick, bool primary = false, Label labelOut = null)
        {
            var b = new VisualElement();
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            Pad(b, 10, 18);
            Radius(b, 24);
            // Pill: radius = half the laid-out height (oversized radii turn pills into ellipses).
            b.RegisterCallback<GeometryChangedEvent>(e => Radius(b, Mathf.Max(8f, e.newRect.height / 2f)));
            Border(b, 2, primary ? Theme.Coral : Theme.BorderStrong);
            b.style.backgroundColor = primary ? Theme.Coral : Theme.Card;
            b.style.marginRight = 8; b.style.marginBottom = 8;
            var l = labelOut ?? new Label();
            l.text = text;
            l.style.fontSize = 18;
            l.style.color = primary ? Theme.OnCoral : Theme.Ink;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.Add(l);
            b.RegisterCallback<ClickEvent>(_ => onClick());
            return b;
        }

        VisualElement Chips<T>(string title, (string label, T value)[] options, Func<T> get, Action<T> set)
        {
            var box = new VisualElement();
            box.style.marginBottom = 18;
            box.Add(Text(title.ToUpperInvariant(), 13, Theme.Muted, true));
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6 } };
            void Render()
            {
                row.Clear();
                foreach (var (label, value) in options)
                {
                    bool on = EqualityComparer<T>.Default.Equals(get(), value);
                    var chip = Button(label, () => { set(value); Render(); }, on);
                    row.Add(chip);
                }
            }
            Render();
            box.Add(row);
            return box;
        }

        VisualElement Card()
        {
            var c = new VisualElement();
            c.style.backgroundColor = Theme.Card;
            Border(c, 1, Theme.Hairline);
            Radius(c, 18);
            Pad(c, 20, 20);
            c.style.marginBottom = 16;
            return c;
        }

        void Play(Word w)
        {
            var clip = WordRepository.Clip(w);
            if (clip != null) { _audio.Stop(); _audio.PlayOneShot(clip); }
        }

        // ------------------------------------------------------------------ start screen

        void ShowStart()
        {
            _game = null;
            var s = NewScreen();
            s.Add(Text("SOLO PLAY", 14, Theme.Muted, true));
            var title = Text("LiANN Pinyin Mahjong Tones", 40, Theme.Ink, true);
            title.style.marginBottom = 6;
            s.Add(title);
            s.Add(Text("Match each word tile to its meaning, then set the tones.", 18, Theme.Muted));

            var card = Card();
            card.style.marginTop = 20;
            card.Add(Chips("Mode", new[] { ("Practice", PlayMode.Practice), ("Challenge", PlayMode.Challenge), ("Zen", PlayMode.Zen) }, () => _mode, v => _mode = v));
            var boards = new List<(string, string)> { ("Starter · 16", "Starter"), ("Small · 24", "Small"), ("Medium · 36", "Medium") };
            if (WideScreen) boards.Add(("Large · 72", "Large"));     // Large is for desktop and tablet (spec §7)
            else if (_layoutName == "Large") _layoutName = "Medium";
            card.Add(Chips("Board", boards.ToArray(), () => _layoutName, v => _layoutName = v));
            card.Add(Chips("Words", new[] { ("HSK 1", Source.Hsk1), ("HSK 2", Source.Hsk2), ("HSK 3", Source.Hsk3), ("Tone pairs", Source.TonePairs), ("Daily challenge", Source.Daily) }, () => _source, v => _source = v));
            card.Add(Chips("Word tiles show", new[] { ("Hanzi + pinyin", true), ("Hanzi only", false) }, () => _showPinyin, v => _showPinyin = v));
            card.Add(Chips("Meaning tiles show", new[] { ("English", false), ("English + picture", true) }, () => _showPictures, v => { _showPictures = v; PlayerPrefs.SetInt("lmt.pictures", v ? 1 : 0); PlayerPrefs.Save(); }));
            card.Add(Chips("Theme", new[] { ("Light", false), ("Dark", true) }, () => Theme.Dark, v => { Theme.Dark = v; ShowStart(); }));
            s.Add(card);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(Button("Play", StartRound, true));
            row.Add(Button("How to play", ShowHelp));
            row.Add(Button("Stats", ShowStats));
            row.Add(Button(_account != null && _account.Status == AccountClient.State.SignedIn ? "Account ✓" : "Account", () => ShowAccount()));
            s.Add(row);
        }

        // ------------------------------------------------------------------ account (optional, spec §13)

        void ShowAccount(string note = null)
        {
            var s = NewScreen();
            s.Add(Text("Account", 32, Theme.Ink, true));
            s.Add(Text("Optional. Sign in with the same email as Pinyin Tiles to save your SetLists to AlongSide. You never need an account to play.", 17, Theme.Muted));
            var card = Card();
            card.style.marginTop = 16;
            var status = Text(note ?? _account.Message, 16, Theme.Muted, true);
            status.style.marginTop = 8;
            async void Run(System.Threading.Tasks.Task t) { try { await t; } catch (Exception e) { Debug.LogWarning(e.Message); } if (_screen == s) ShowAccount(); }

            TextField Field(string label, string hint, string value = "")
            {
                var f = new TextField { value = value };
                f.style.fontSize = 18;
                f.style.minWidth = 260; f.style.maxWidth = 420;
                f.style.marginBottom = 10;
#if UNITY_2023_1_OR_NEWER
                f.textEdition.placeholder = hint;
#endif
                // A visible box, so testers can see where to type (retest finding 5).
                var input = f.Q(className: TextField.inputUssClassName);
                if (input != null)
                {
                    Border(input, 2, Theme.BorderStrong);
                    Radius(input, 10);
                    Pad(input, 8, 12);
                    input.style.backgroundColor = Theme.Card;
                    f.RegisterCallback<FocusInEvent>(_ => Border(input, 2, Theme.Coral));
                    f.RegisterCallback<FocusOutEvent>(_ => Border(input, 2, Theme.BorderStrong));
                }
                card.Add(Text(label, 13, Theme.Muted, true));
                return f;
            }
            VisualElement Row() => new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

            switch (_account.Status)
            {
                case AccountClient.State.Loading:
                    card.Add(Text("Checking the sign-in service…", 18, Theme.Ink, true));
                    Run(_account.Init());
                    break;
                case AccountClient.State.Disabled:
                case AccountClient.State.Unavailable:
                    card.Add(Text(_account.Status == AccountClient.State.Disabled ? "Sign-in isn't available right now." : "The sign-in service can't be reached.", 18, Theme.Ink, true));
                    card.Add(Text("Your SetLists are saved on this device.", 16, Theme.Muted));
                    card.Add(Button("Try again", () => Run(_account.Init())));
                    break;
                case AccountClient.State.SignedOut:
                {
                    var email = Field("EMAIL", "you@example.com", _emailDraft);
                    email.RegisterValueChangedCallback(e => _emailDraft = e.newValue);
                    card.Add(email);
                    var row = Row();
                    int wait = _account.ResendInSeconds;
                    string label = _account.SendingCode ? "Sending…" : wait > 0 ? $"Send me a code ({wait} s)" : "Send me a code";
                    var send = Button(label, () => { if (!_account.SendingCode && _account.ResendInSeconds == 0) { status.text = "Sending…"; Run(_account.SendCode(email.value)); } }, true);
                    if (_account.SendingCode || wait > 0)
                    {
                        send.SetEnabled(false); send.style.opacity = 0.55f;
                        if (wait > 0) s.schedule.Execute(() => { if (_screen == s && _account.Status == AccountClient.State.SignedOut) ShowAccount(); }).StartingIn(1000);
                    }
                    row.Add(send);
                    card.Add(row);
                    break;
                }
                case AccountClient.State.CodeSent:
                {
                    var code = Field("CODE FROM THE EMAIL", "6-digit code");
                    card.Add(code);
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                    row.Add(Button("Sign in", () => Run(_account.VerifyCode(code.value)), true));
                    row.Add(Button("Use a different email", () => { _account.ChangeEmail(); ShowAccount(); }));
                    card.Add(row);
                    break;
                }
                case AccountClient.State.SignedIn:
                {
                    card.Add(Text("Signed in as " + _account.Email, 18, Theme.Ink, true));
                    int waiting = _account.Pending().Count;
                    card.Add(Text(waiting == 0 ? "Your SetLists are up to date in AlongSide." : $"{waiting} saved words waiting to upload.", 16, Theme.Muted));
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 10 } };
                    if (waiting > 0) row.Add(Button("Upload now", async () =>
                    {
                        status.text = "Uploading…";
                        string msg;
                        try { msg = await _account.Upload(); } catch (Exception e) { msg = "Upload failed: " + e.Message; }
                        if (_screen == s) ShowAccount(msg);
                    }, true));
                    row.Add(Button("Sign out", () => Run(_account.SignOut())));
                    card.Add(row);
                    break;
                }
            }
            card.Add(status);
            s.Add(card);
            var back = Button("Back", ShowStart);
            back.style.alignSelf = Align.FlexStart;   // sized to its text, like the other screens
            s.Add(back);
        }

        void ShowHelp()
        {
            var s = NewScreen();
            s.Add(Text("How to play", 32, Theme.Ink, true));
            var card = Card();
            string[] steps =
            {
                "1  A tile is free when nothing is on top of it and its left or right side is open.",
                "2  Tap a free word tile (hanzi, corner mark) and the free meaning tile (English, underline) that match.",
                "3  Set the tone of each syllable: 1 · 2 · 3 · 4 · neutral. Dictionary tones, no tone changes (你好 = nǐ hǎo).",
                "4  Right tones clear the pair and say the word. Clear the board to win.",
            };
            foreach (var t in steps) { var l = Text(t, 18, Theme.Ink); l.style.marginBottom = 10; l.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont); card.Add(l); }
            s.Add(card);
            var back = Button("Back", ShowStart, true);
            back.style.alignSelf = Align.FlexStart;
            s.Add(back);
        }

        void ShowStats()
        {
            var s = NewScreen();
            s.Add(Text("Stats", 32, Theme.Ink, true));
            foreach (var layout in Layout.Names)
            {
                var card = Card();
                card.Add(Text(layout, 20, Theme.Ink, true));
                int games = PlayerPrefs.GetInt($"lmt.{layout}.games"), wins = PlayerPrefs.GetInt($"lmt.{layout}.wins");
                int best = PlayerPrefs.GetInt($"lmt.{layout}.bestScore"); float bestTime = PlayerPrefs.GetFloat($"lmt.{layout}.bestTime");
                card.Add(Text($"Games {games} · Wins {wins} · Best score {best} · Best time {(bestTime > 0 ? Clock(bestTime) : "—")}", 17, Theme.Muted));
                s.Add(card);
            }
            s.Add(Text($"Daily streak: {PlayerPrefs.GetInt("lmt.daily.streak")}", 17, Theme.Muted));
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            row.Add(Button("Back", ShowStart, true));
            row.Add(Button("Reset stats", () => { foreach (var k in new[] { "games", "wins", "bestScore", "bestTime" }) foreach (var l in Layout.Names) PlayerPrefs.DeleteKey($"lmt.{l}.{k}"); ShowStats(); }));
            s.Add(row);
        }

        // ------------------------------------------------------------------ round

        void StartRound()
        {
            string seed;
            IEnumerable<Word> pool;
            IList<IList<string>> groups = null;
            switch (_source)
            {
                case Source.Daily:
                    var day = DateTime.UtcNow.Date;
                    seed = "daily:" + day.ToString("yyyy-MM-dd");
                    pool = WordRepository.Band(day.DayOfYear % 3 + 1);
                    break;
                case Source.TonePairs:
                    seed = "free:" + DateTime.UtcNow.Ticks;
                    pool = WordRepository.TonePairPool();
                    groups = WordRepository.TonePairs;
                    break;
                default:
                    seed = "free:" + DateTime.UtcNow.Ticks;
                    pool = WordRepository.Band(_source == Source.Hsk1 ? 1 : _source == Source.Hsk2 ? 2 : 3);
                    break;
            }
            _game = new Game(Layout.ByName(_layoutName), pool, _mode, seed, groups);
            _elapsed = 0; _roundOver = false; _busy = false; _hint = null; _eye = false;
            PlayerPrefs.SetInt($"lmt.{_layoutName}.games", PlayerPrefs.GetInt($"lmt.{_layoutName}.games") + 1);
            BuildGameScreen();
        }

        void BuildGameScreen()
        {
            var s = NewScreen();

            // Header mirrors the Pinyin Tiles game header: kicker + title, then the kick-label status row (spec D11).
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.FlexEnd, marginBottom = 12 } };
            var titles = new VisualElement { style = { marginRight = 24 } };
            titles.Add(Text(_mode == PlayMode.Challenge ? "CHALLENGE" : _mode == PlayMode.Zen ? "ZEN" : "PRACTICE", 13, Theme.Muted, true));
            titles.Add(Text("LiANN Pinyin Mahjong Tones", 26, Theme.Ink, true));
            header.Add(titles);
            var status = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.FlexEnd } };
            Label Stat(string k) { status.Add(Text(k, 13, Theme.Muted, true)); var v = Text("", 20, Theme.Ink, true); v.style.marginLeft = 4; v.style.marginRight = 14; status.Add(v); return v; }
            _score = Stat("Score"); _tilesLeft = Stat("Tiles"); _pairs = Stat("Pairs"); _time = Stat("Time"); _seed = Stat("Seed");
            _seed.style.fontSize = 13;
            header.Add(status);
            s.Add(header);

            // Board frame: sage well, like the Pinyin Tiles board frame.
            var frame = new VisualElement();
            frame.style.flexGrow = 1;
            frame.style.backgroundColor = Theme.BoardFrame;
            Radius(frame, 24);
            Pad(frame, 18, 18);
            _board = new VisualElement { style = { flexGrow = 1 } };
            _board.RegisterCallback<GeometryChangedEvent>(_ => LayoutBoard());
            frame.Add(_board);
            s.Add(frame);

            _feedback = Text("", 17, Theme.Ink, true);
            _feedback.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);   // shows hanzi and tone-marked pinyin
            _feedback.style.minHeight = 26; _feedback.style.marginTop = 10;
            s.Add(_feedback);

            var tools = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6 } };
            _hintBtnLabel = new Label(); _undoBtnLabel = new Label(); _shuffleBtnLabel = new Label();
            tools.Add(Button("Hint", DoHint, false, _hintBtnLabel));
            tools.Add(Button("Undo", DoUndo, false, _undoBtnLabel));
            tools.Add(Button("Shuffle", DoShuffle, false, _shuffleBtnLabel));
            tools.Add(Button("Free tiles", () => { _eye = !_eye; RefreshTiles(); }));
            tools.Add(Button("Quit", () => EndRound(false)));
            s.Add(tools);

            BuildTiles();
            RefreshStatus();
        }

        void BuildTiles()
        {
            _board.Clear();
            _tiles.Clear();
            var order = _game.Layout.Slots.OrderBy(sl => sl.Z).ThenBy(sl => sl.Y).ThenBy(sl => sl.X);
            foreach (var slot in order)
            {
                var tile = _game.Tiles[slot.Id];
                if (!tile.OnBoard) continue;
                var view = new TileView(slot.Id);
                var word = _game.Words[tile.WordIndex];
                view.Show(word, tile.Kind, _showPinyin, _hanziFont, _uiFont, _showPictures ? WordRepository.Picture(word) : null);
                int id = slot.Id;
                view.RegisterCallback<ClickEvent>(_ => OnTile(id));
                _tiles[id] = view;
                _board.Add(view);
            }
            LayoutBoard();
            RefreshTiles();
        }

        void LayoutBoard()
        {
            if (_board == null || _game == null) return;
            float w = _board.resolvedStyle.width, h = _board.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0 || float.IsNaN(h) || h <= 0) return;
            int cols = _game.Layout.Slots.Max(sl => sl.X) / 2 + 1;
            int rows = _game.Layout.Slots.Max(sl => sl.Y) / 2 + 1;
            const float gap = 10f, ratio = TileArt.H / (float)TileArt.W;
            float size = Mathf.Min((w - gap * (cols + 1)) / cols, ((h - gap * (rows + 1)) / rows) / ratio, 150f);
            float boardW = cols * size + (cols - 1) * gap, boardH = rows * size * ratio + (rows - 1) * gap;
            float ox = (w - boardW) / 2, oy = (h - boardH) / 2;
            foreach (var kv in _tiles)
            {
                var sl = _game.Layout.Slots[kv.Key];
                kv.Value.Resize(size);
                kv.Value.style.left = ox + sl.X / 2f * (size + gap) - sl.Z * size * 0.06f;
                kv.Value.style.top = oy + sl.Y / 2f * (size * ratio + gap) - sl.Z * size * 0.09f;
            }
        }

        void RefreshTiles()
        {
            foreach (var kv in _tiles)
            {
                bool selected = _game.Selected == kv.Key;
                kv.Value.SetSelected(selected);
                kv.Value.style.translate = new Translate(0, selected ? -6 : 0, 0);
                kv.Value.SetDimmed(_eye && !_game.IsFree(kv.Key));
            }
            if (_hint.HasValue) { if (_tiles.TryGetValue(_hint.Value.a, out var a)) a.SetHint(true); if (_tiles.TryGetValue(_hint.Value.b, out var b)) b.SetHint(true); }
        }

        void RefreshStatus()
        {
            _score.text = _mode == PlayMode.Zen ? "—" : _game.Score.ToString();
            _tilesLeft.text = _game.TilesLeft.ToString();
            _pairs.text = _game.OpenPairs().Count.ToString();
            _seed.text = _game.Seed.StartsWith("daily:") ? _game.Seed.Substring(6) : "random";
            bool limited = _mode == PlayMode.Challenge;
            _hintBtnLabel.text = limited ? $"Hint ({Scoring.ChallengeHints - _game.HintsUsed})" : "Hint";
            _undoBtnLabel.text = limited ? $"Undo ({Scoring.ChallengeUndos - _game.UndosUsed})" : "Undo";
            _shuffleBtnLabel.text = limited ? $"Shuffle ({Scoring.ChallengeShuffles - _game.ShufflesUsed})" : "Shuffle";
        }

        void Say(string text) { if (_feedback != null) _feedback.text = text; }

        // ------------------------------------------------------------------ moves

        void OnTile(int slot)
        {
            if (_busy || _roundOver || _picker != null) return;
            int? previous = _game.Selected;
            switch (_game.Select(slot))
            {
                case SelectOutcome.Blocked:
                    Shake(slot);
                    Say("That tile is blocked. A free tile has nothing on top and an open left or right side.");
                    break;
                case SelectOutcome.Selected:
                case SelectOutcome.Deselected:
                    Say("");
                    break;
                case SelectOutcome.WrongPair:
                    Shake(slot); if (previous.HasValue) Shake(previous.Value);
                    Say(_mode == PlayMode.Challenge ? $"Not a pair (−{Scoring.WrongPairPenalty})." : "Not a pair. Look for the meaning that goes with the word.");
                    break;
                case SelectOutcome.AwaitingTones:
                    _pendingSlots = (previous ?? slot, slot);
                    OpenTonePicker();
                    break;
            }
            RefreshTiles();
            RefreshStatus();
        }

        (int, int) _pendingSlots;

        void OpenTonePicker()
        {
            var word = _game.Words[_game.PendingWord.Value];
            _pickedTones = new List<int>();
            _picker = new VisualElement();
            _picker.style.position = Position.Absolute;
            _picker.style.left = 0; _picker.style.right = 0; _picker.style.top = 0; _picker.style.bottom = 0;
            _picker.style.backgroundColor = new Color(0.1f, 0.1f, 0.08f, 0.45f);
            _picker.style.alignItems = Align.Center;
            _picker.style.justifyContent = Justify.Center;

            var card = Card();
            card.style.maxWidth = 620;
            card.style.alignItems = Align.Center;
            var hz = Text(word.Hanzi, 64, Theme.Ink, true);
            hz.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
            card.Add(hz);
            card.Add(Text(word.English, 20, Theme.Muted, true));
            var rows = new VisualElement { style = { marginTop = 14 } };
            card.Add(rows);
            void Render()
            {
                rows.Clear();
                for (int i = 0; i < word.Syllables.Length; i++)
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
                    var syl = Text(i < _pickedTones.Count ? Pinyin.Mark(word.Syllables[i], _pickedTones[i]) : word.Syllables[i].Replace('v', 'ü'), 24, Theme.Ink, true);
                    syl.style.minWidth = 90;
                    syl.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);   // Noto covers every tone-marked vowel
                    row.Add(syl);
                    bool active = i == _pickedTones.Count;
                    for (int tone = 1; tone <= 5; tone++)
                    {
                        int t = tone;
                        var btn = Button(tone == 5 ? "·" : Pinyin.Mark(word.Syllables[i], tone), () => { if (active) PickTone(t); }, i < _pickedTones.Count && _pickedTones[i] == tone);
                        btn.style.opacity = active || i < _pickedTones.Count ? 1f : 0.4f;
                        ((Label)btn[0]).style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
                        row.Add(btn);
                    }
                    rows.Add(row);
                }
            }
            _renderPicker = Render;
            Render();
            var keysHint = Text("Keys 1–4 for tones, 5 for neutral, Esc to close.", 14, Theme.Muted);
            keysHint.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
            card.Add(keysHint);
            card.Add(Button("Close", () => { _game.CancelTones(); ClosePicker(); RefreshTiles(); }));
            _picker.Add(card);
            _root.Add(_picker);
            _picker.style.opacity = 0;
            card.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1));
            var picker = _picker;   // the field is nulled if the picker closes before the fade ends
            _tweens.Run(0.18f, t => { picker.style.opacity = t; card.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.9f, 1f, t)); }, null, Tweens.EaseOutBack);
            _root.Focus();
        }

        Action _renderPicker;

        void PickTone(int tone)
        {
            if (_picker == null || !_game.PendingWord.HasValue) return;
            var word = _game.Words[_game.PendingWord.Value];
            _pickedTones.Add(tone);
            _renderPicker?.Invoke();
            if (_pickedTones.Count < word.Tones.Length) return;

            var (a, b) = _pendingSlots;
            var result = _game.SubmitTones(_pickedTones);
            ClosePicker();
            if (result == ToneOutcome.Cleared) AnimateClear(word, a, b);
            else
            {
                Shake(a); Shake(b);
                Play(word);
                Say($"{word.Hanzi} = {word.Marked} · {word.English}" + (_mode == PlayMode.Challenge ? $"  (−{Scoring.WrongTonePenalty})" : ""));
                RefreshTiles(); RefreshStatus();
            }
        }

        void ClosePicker()
        {
            if (_picker == null) return;
            _picker.RemoveFromHierarchy();
            _picker = null;
        }

        void OnKey(KeyDownEvent e)
        {
            if (_picker == null) return;
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha5) PickTone(e.keyCode - KeyCode.Alpha0);
            else if (e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad5) PickTone(e.keyCode - KeyCode.Keypad0);
            else if (e.keyCode == KeyCode.Escape) { _game.CancelTones(); ClosePicker(); RefreshTiles(); }
        }

        // ------------------------------------------------------------------ animation

        void Shake(int slot)
        {
            if (!_tiles.TryGetValue(slot, out var v)) return;
            _tweens.Run(0.35f, t => v.style.translate = new Translate(Mathf.Sin(t * Mathf.PI * 6) * 7 * (1 - t), 0, 0), () => v.style.translate = new Translate(0, 0, 0), x => x);
        }

        void AnimateClear(Word word, int a, int b)
        {
            _busy = true;
            ClearHint();
            Play(word);
            Say($"{word.Hanzi} · {word.Marked} · {word.English}");
            if (!_tiles.TryGetValue(a, out var va) || !_tiles.TryGetValue(b, out var vb)) { AfterClear(); return; }
            float ax = va.resolvedStyle.left, ay = va.resolvedStyle.top, bx = vb.resolvedStyle.left, by = vb.resolvedStyle.top;
            float mx = (ax + bx) / 2, my = (ay + by) / 2;
            va.BringToFront(); vb.BringToFront();
            // Fly together, swell, then fade: the pair "clicks" into one word.
            _tweens.Run(0.32f, t =>
            {
                va.style.translate = new Translate((mx - ax) * t, (my - ay) * t - 14 * Mathf.Sin(t * Mathf.PI), 0);
                vb.style.translate = new Translate((mx - bx) * t, (my - by) * t - 14 * Mathf.Sin(t * Mathf.PI), 0);
            }, () =>
            {
                Burst(mx + va.Size / 2, my + va.Size / 2, 6 + Math.Min(_game.Streak, 5) * 3);
                _tweens.Run(0.26f, t =>
                {
                    var sc = new Scale(Vector3.one * (1 + 0.18f * t));
                    va.style.scale = sc; vb.style.scale = sc;
                    va.style.opacity = 1 - t; vb.style.opacity = 1 - t;
                }, AfterClear);
            }, Tweens.EaseInOut);
        }

        /// <summary>Small coral/gold sparks from the match point; count grows with the streak.</summary>
        void Burst(float x, float y, int count)
        {
            var rng = new System.Random(count * 7919 + (int)(x * 13));
            for (int i = 0; i < count; i++)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                float s = 6 + (float)rng.NextDouble() * 6;
                p.style.position = Position.Absolute; p.style.width = s; p.style.height = s;
                p.style.left = x; p.style.top = y;
                Radius(p, s);
                p.style.backgroundColor = i % 2 == 0 ? Theme.TileCoral : Theme.PorcelainEdge;
                _board.Add(p);
                float ang = (float)(rng.NextDouble() * Math.PI * 2), dist = 40 + (float)rng.NextDouble() * 60;
                _tweens.Run(0.55f, t =>
                {
                    p.style.translate = new Translate(Mathf.Cos(ang) * dist * t, Mathf.Sin(ang) * dist * t + 30 * t * t, 0);
                    p.style.opacity = 1 - t;
                }, p.RemoveFromHierarchy);
            }
        }

        void AfterClear()
        {
            _busy = false;
            if (_game.Won) { BuildTiles(); RefreshStatus(); _tweens.Delay(0.4f, () => EndRound(true)); return; }
            if (_game.LastAutoReshuffle)
            {
                Say("No open pairs left, so the board was reshuffled" + (_mode == PlayMode.Challenge ? $" (−{Scoring.AutoReshufflePenalty})." : "."));
                SwirlRebuild();
            }
            else { BuildTiles(); }
            RefreshStatus();
        }

        void SwirlRebuild()
        {
            _busy = true;
            var views = _tiles.Values.ToList();
            _tweens.Run(0.25f, t => { foreach (var v in views) { v.style.scale = new Scale(Vector3.one * (1 - 0.6f * t)); v.style.rotate = new Rotate(new Angle(180 * t)); v.style.opacity = 1 - t; } }, () =>
            {
                BuildTiles();
                var fresh = _tiles.Values.ToList();
                foreach (var v in fresh) { v.style.scale = new Scale(Vector3.one * 0.4f); v.style.opacity = 0; }
                _tweens.Run(0.3f, t => { foreach (var v in fresh) { v.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.4f, 1f, t)); v.style.opacity = t; } }, () => _busy = false, Tweens.EaseOutBack);
            });
        }

        // ------------------------------------------------------------------ helpers

        void DoHint()
        {
            if (_busy || _roundOver || _picker != null) return;
            var h = _game.Hint();
            if (!h.HasValue) { Say(_game.CanHint ? "No open pair right now." : "No hints left."); return; }
            var word = _game.Words[h.Value.word];
            _hint = (h.Value.wordSlot, h.Value.meaningSlot);
            _hintUntil = Time.unscaledTime + 3f;
            Play(word);
            Say($"Hint: {word.Hanzi} · {word.Marked} · {word.English}" + (_mode == PlayMode.Challenge ? $"  (−{Scoring.HintPenalty})" : ""));
            RefreshStatus();
        }

        void ClearHint()
        {
            if (!_hint.HasValue) return;
            if (_tiles.TryGetValue(_hint.Value.a, out var a)) a.SetHint(false);
            if (_tiles.TryGetValue(_hint.Value.b, out var b)) b.SetHint(false);
            _hint = null;
            RefreshTiles();
        }

        void DoUndo()
        {
            if (_busy || _roundOver || _picker != null) return;
            if (!_game.Undo()) { Say(_game.CanUndo ? "Nothing to undo." : "No undos left."); return; }
            ClearHint();
            BuildTiles(); RefreshStatus(); Say("Undone.");
        }

        void DoShuffle()
        {
            if (_busy || _roundOver || _picker != null) return;
            if (!_game.Shuffle()) { Say("No shuffles left."); return; }
            ClearHint();
            Say(_mode == PlayMode.Challenge ? $"Shuffled (−{Scoring.ShufflePenalty})." : "Shuffled.");
            SwirlRebuild(); RefreshStatus();
        }

        // ------------------------------------------------------------------ results

        void EndRound(bool won)
        {
            if (_roundOver) return;
            _roundOver = true;
            ClosePicker();
            string layout = _layoutName;
            if (won)
            {
                PlayerPrefs.SetInt($"lmt.{layout}.wins", PlayerPrefs.GetInt($"lmt.{layout}.wins") + 1);
                if (_game.Score > PlayerPrefs.GetInt($"lmt.{layout}.bestScore")) PlayerPrefs.SetInt($"lmt.{layout}.bestScore", _game.Score);
                float best = PlayerPrefs.GetFloat($"lmt.{layout}.bestTime");
                if (best <= 0 || _elapsed < best) PlayerPrefs.SetFloat($"lmt.{layout}.bestTime", _elapsed);
                if (_source == Source.Daily) UpdateDailyStreak();
            }
            PlayerPrefs.Save();

            var s = NewScreen();
            s.Add(Text(won ? "Board cleared" : (_mode == PlayMode.Challenge && _elapsed >= Scoring.CountdownSeconds(_layoutName) ? "Time's up" : "Round ended"), 34, Theme.Ink, true));
            s.Add(Text((_mode == PlayMode.Zen ? "" : $"Score {_game.Score} · ") + $"Time {Clock(_elapsed)}", 20, Theme.Muted, true));

            var card = Card();
            card.style.marginTop = 16;
            card.Add(Text("Words this round", 18, Theme.Ink, true));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 520;
            scroll.contentContainer.style.flexShrink = 0;          // rows keep their height and the list scrolls
            scroll.contentViewport.style.overflow = Overflow.Hidden;
            foreach (var r in _game.Results)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8, flexShrink = 0 } };
                var hz = Text(r.Word.Hanzi, 26, Theme.Ink, true);
                hz.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
                hz.style.minWidth = 90;
                hz.style.marginRight = 10;
                row.Add(hz);
                var py = Text(r.Word.Marked, 18, Theme.Ink, true);
                py.style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
                py.style.minWidth = 150;
                py.style.marginRight = 14;
                row.Add(py);
                var en = Text(r.Word.English, 17, Theme.Muted);
                en.style.flexGrow = 1;
                row.Add(en);
                string flag = !r.Matched ? "not matched" : r.WrongToneAttempts > 0 ? "missed tone" : r.Hinted ? "hinted" : "perfect";
                var flagLabel = Text(flag, 14, flag == "perfect" ? Theme.Good : Theme.Coral, true);
                flagLabel.style.marginLeft = 10; flagLabel.style.marginRight = 12;
                row.Add(flagLabel);
                var word = r.Word;
                var playBtn = Button("▶", () => Play(word));
                ((Label)playBtn[0]).style.unityFontDefinition = FontDefinition.FromFont(_hanziFont);
                row.Add(playBtn);
                scroll.Add(row);
            }
            card.Add(scroll);
            s.Add(card);

            var status = Text("", 16, Theme.Muted, true);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            buttons.Add(Button("Save to SetList", () =>
            {
                int added = SetListStore.Save("LiANN Mahjong Tones", _game.Results);
                status.text = added > 0 ? $"{added} words saved to your SetList." : "These words are already in your SetList.";
                if (_account != null && _account.Status == AccountClient.State.SignedIn) UploadAfterSave(status, s);
                else status.text += " Sign in under Account to save them to AlongSide too.";
            }, true));
            buttons.Add(Button("Play again", StartRound));
            buttons.Add(Button("Menu", ShowStart));
            s.Add(buttons);
            s.Add(status);
        }

        async void UploadAfterSave(Label status, VisualElement screen)
        {
            string before = status.text;
            try
            {
                string msg = await _account.Upload();
                if (_screen == screen) status.text = before + " " + msg;
            }
            catch (Exception e) { Debug.LogWarning("Upload failed: " + e.Message); }
        }

        static void UpdateDailyStreak()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd"), yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            string last = PlayerPrefs.GetString("lmt.daily.last");
            if (last == today) return;
            int streak = last == yesterday ? PlayerPrefs.GetInt("lmt.daily.streak") + 1 : 1;
            PlayerPrefs.SetInt("lmt.daily.streak", streak);
            PlayerPrefs.SetString("lmt.daily.last", today);
        }
    }
}
