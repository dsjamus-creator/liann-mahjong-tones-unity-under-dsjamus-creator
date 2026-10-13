# LiANN Pinyin Mahjong Tones — Unity edition

Game 3 of the LiANN Pinyin family, as a Unity package (`com.liann.mahjongtones`).
Design source of truth: `PINYIN-MAHJONG-DESIGN-SPEC (2026-10-11).md` in
`dsjamus-creator/learningwithliann-registry` (§0.2 rules, §4.1 generation, §6.1 scoring, §13 SetLists, decisions D6–D17).

## What is in it

| Path | What |
|---|---|
| `Runtime/Core/` | Unity-free game rules: four layouts (Starter 16, Small 24, Medium 36, Large 72) and the free-tile rule, seeded RNG (mulberry32 + FNV-1a), solvable board generator, tone checking, scoring, undo / hint / shuffle, and the AlongSide SetList event format. Identical to the web edition. |
| `Runtime/Unity/` | `MahjongTonesApp` — builds the whole UI in UI Toolkit from code: start screen, board, tone picker, results, stats, account. Porcelain tiles baked in code to match Pinyin Tiles (D8). `AccountClient` — optional sign-in and SetList upload to AlongSide; `ProtectedAccountStore` and its Android/iOS plugins hold the session outside plaintext files. |
| `Runtime/Resources/LiANNMahjongTones/` | `words.json` (556 HSK 1–3 words with recorded audio, dictionary entry and sense ids), `accounts.json` (public service addresses), `Audio/*.mp3`, `Fonts/` (Noto Sans SC Bold subset, Nunito ExtraBold). |
| `Editor/` | **LiANN → Mahjong Tones** menu: create the play scene; build Android (App Bundle), iOS (Xcode project) and Windows. |
| `Tests/Editor/` | NUnit tests (run in Unity's Test Runner). |
| `Tests/Vectors/rules-test-vectors.json` | Seeded boards + scripted play-throughs. The web edition must reproduce them exactly. |
| `Tools~/` | Not imported by Unity: word-list builder, font subsetter, meta-file writer, and a .NET test project that runs the Core tests outside Unity. |

## Install on DSJ-PC (Unity 6000.6.5f1)

1. Open `C:\Users\dsjam\UnityProjects\Unity Project Pinyin Mahjong`.
2. **Window → Package Manager → + → Install package from git URL…** and paste
   `https://github.com/dsjamus-creator/liann-mahjong-tones-unity-under-dsjamus-creator.git`
   (the repository is private: Git on DSJ-PC must be signed in to GitHub, e.g. Git Credential Manager).
   Until the first pull request is merged, install from its branch instead by adding
   `#claude/unity-package-v0.1` to the end of the URL.
   To see the package's tests in the Test Runner, also add `"testables": ["com.liann.mahjongtones"]` to the
   project's `Packages/manifest.json` (git packages' tests are hidden otherwise).
3. **LiANN → Mahjong Tones → Create play scene**, then press Play. (Or by hand: an empty scene with a GameObject that has **LiANN → Mahjong Tones App**.)
4. To update after a push: Package Manager → the package → **Update** (or remove and re-add the git URL).

Optional: drag a PanelSettings asset (with a theme) onto the component; otherwise one is created at run time
(Unity may log "No Theme Style Sheet" — harmless, the game styles everything itself).

## Store builds

Menu **LiANN → Mahjong Tones → Build Android / Build iOS / Build Windows** (output in `Builds/`), or from the command line:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod LiANN.MahjongTones.EditorTools.MahjongTonesBuild.BuildAndroid
```

- App id `com.learningwithliann.mahjongtones` and version 0.2.0 are set in `Editor/MahjongTonesBuild.cs`; confirm the id
  before the first store upload. Portrait and landscape are both allowed.
- **Android with Minify (R8) on:** the Android build menu adds a keep rule for `SecureSessionStore` to
  `Assets/Plugins/Android/proguard-user.txt`; also tick **Player Settings → Publishing Settings → Custom Proguard File**,
  or sign-in falls back to "this session only" in release builds.
- **Large (72 tiles)** is offered only on a landscape screen (and on phones and tablets only with a short side of at
  least 600 dp); in portrait its labels would be about 9 px.
- Signing stays on the build machine: set the Android upload keystore (Google Play App Signing) and the iOS team in
  Player Settings. Never commit keystores or passwords.
- iOS builds an Xcode project; archive and upload it from a Mac.
- The build menus set **Company Name** "Learning with LiANN" and **Product Name** "LiANN Pinyin Mahjong Tones" in Player
  Settings, so `Application.persistentDataPath` (SetLists, stats, the account store) moves to
  `…/Learning with LiANN/LiANN Pinyin Mahjong Tones` in the Editor too. **Create play scene** adds
  `Assets/LiANN/MahjongTones.unity` to Build Settings without removing the project's own scenes.

## Account and SetLists (optional)

Playing never needs an account. **Account** on the start screen signs in with the same email code as Pinyin Tiles
(Supabase; URL and publishable key read from the Pinyin API's `/v1/auth/config`). When signed in, **Save to SetList**
also uploads the words to AlongSide (`POST /learning/game-events`, sourceApp `liann-mahjong-tones`) with stable ids, so
retries never duplicate. If AlongSide is unreachable, or doesn't accept this game yet, saves stay on the device and
upload later from the Account screen. Service addresses are in `Runtime/Resources/LiANNMahjongTones/accounts.json`;
empty values switch sign-in off. Sign-in uses `pinyin-tiles-api-staging`; **uploads go to the live AlongSide service**
(`lwl-backend-mhgo`, owner decision of 13 October 2026), because staging cannot send AlongSide sign-in emails and live
accepts a same-email LiANN sign-in without the link page. A tester's saves therefore land in their real AlongSide account.
The Supabase address returned by the remote config is used only if it is https on a `*.supabase.co` project host, or on
`supabaseHost` when that is set in `accounts.json`. The game makes no network call at launch unless this device has a
sign-in in progress or complete; otherwise the services are first contacted when the player opens **Account**.

"Send me a code" shows *Sending…* while the request runs and then waits 60 s before another code can be requested,
because every request sends an email and counts towards Supabase's email limit. The encrypted account store is first
written when a code is requested (it holds the pending email), so for tests "the store exists" does not by itself mean
"signed in"; check the Account screen.

The backend's draft PR #102 adds `liann-mahjong-tones` to the route and database constraints. That migration and route must be deployed before uploads are accepted.

Session access and refresh tokens are stored in Android Keystore-backed AES-GCM preferences, the iOS Keychain, or Windows user-scoped DPAPI, according to the build target. The Unity Editor and unsupported platforms keep them in memory only; sign-in then lasts until the app closes. On first launch after this update, the old plaintext `liann-tones.account.v1.json` is deleted without loading its tokens. The player signs in again. If that file cannot be removed, account sign-in is disabled until app data is cleared. SetLists use their separate local store and are not deleted.

Before distribution, verify a sign-in, app restart, token refresh, upload, and sign-out on Android, iOS and Windows. Check that no `liann-tones.account.v1.json` remains and that the platform store is cleared by sign-out. Existing test builds that wrote the old JSON file should have their sessions revoked or app data cleared before the device is shared.

## Tests

- In Unity: **Window → General → Test Runner → EditMode → Run All**.
- Outside Unity: `cd Tools~/CoreTests && dotnet test` (needs .NET 8). Regenerate vectors with `LIANN_WRITE_VECTORS=1 dotnet test`.

## Rebuilding data

```
python3 -I Tools~/data/build_words.py <pinyin-tiles-playtest checkout> . <dictionary>
                                                                            # words.json, MP3s, english-labels-review.csv
python3 -I Tools~/fonts/get_fonts.py .                                      # font subsets (after words change)
python3 -I Tools~/data/add_sense_ids.py . <dictionary>                      # senseId + gloss repair, in place
LIANN_WRITE_VECTORS=1 dotnet test Tools~/CoreTests                          # shared vectors (web edition must match)
python3 -I Tools~/make_meta.py .                                            # .meta files for new assets
```

`<dictionary>` is AlongSide's signed learner release, `<lwl-backend>/public/classroom/dictionary/unified-dictionary.browser.json`:
the same release the server resolves uploads against, so every senseId is one AlongSide accepts. The older
`data/unified-dictionary.ndjson` still works but lacks the corrected HSK headwords (医生, 公园 …).

English tile labels come from the dictionary's first sense plus `Tools~/data/label-overrides.json`.
Homophone mix-ups (the pronunciation pack's headword is a homophone of the HSK word, e.g. 一生 for 医生) are dropped via
`Tools~/data/homophone-denylist.json` and an automatic check against the dictionary. Since the reading set was corrected
(pinyin-tiles-playtest PR #6) the denylist is empty and the 8 words it held ship with their HSK headword; a test guards them.
Proofread `Tools~/data/english-labels-review.csv` before release (spec §2).

## Not in this version

Pictures on meaning tiles (D13: moved to the hub's Kids section), Classic 144 layout, sound/music settings,
hub profiles and plan checks (D19–D22; added when the hub's account service exists).

See `THIRD-PARTY-NOTICES.md` for data, audio and font licences.

