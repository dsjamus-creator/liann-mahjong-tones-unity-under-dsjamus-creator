# LiANN Pinyin Mahjong Tones — Unity edition

Game 3 of the LiANN Pinyin family, as a Unity package (`com.liann.mahjongtones`).
Design source of truth: `PINYIN-MAHJONG-DESIGN-SPEC (2026-10-11).md` in
`dsjamus-creator/learningwithliann-registry` (§0.2 rules, §4.1 generation, §6.1 scoring, decisions D6–D17).

## What is in it

| Path | What |
|---|---|
| `Runtime/Core/` | Unity-free game rules: layouts and the free-tile rule, seeded RNG (mulberry32 + FNV-1a), solvable board generator, tone checking, scoring, undo / hint / shuffle. Identical to the web edition. |
| `Runtime/Unity/` | `MahjongTonesApp` — builds the whole UI in UI Toolkit from code: start screen, board, tone picker, results, stats. Porcelain tiles baked in code to match Pinyin Tiles (D8). |
| `Runtime/Resources/LiANNMahjongTones/` | `words.json` (556 HSK 1–3 words with recorded audio), `Audio/*.mp3`, `Fonts/` (Noto Sans SC Bold subset, Nunito ExtraBold). |
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
3. **File → New Scene → Empty**, add an empty GameObject, **Add Component → LiANN → Mahjong Tones App**, save the scene, press Play.
4. To update after a push: Package Manager → the package → **Update** (or remove and re-add the git URL).

Optional: drag a PanelSettings asset (with a theme) onto the component; otherwise one is created at run time
(Unity may log "No Theme Style Sheet" — harmless, the game styles everything itself).

## Tests

- In Unity: **Window → General → Test Runner → EditMode → Run All**.
- Outside Unity: `cd Tools~/CoreTests && dotnet test` (needs .NET 8). Regenerate vectors with `LIANN_WRITE_VECTORS=1 dotnet test`.

## Rebuilding data

```
python3 -I Tools~/data/build_words.py <pinyin-tiles-playtest checkout> .   # words.json, MP3s, english-labels-review.csv
python3 -I Tools~/fonts/get_fonts.py .                                      # font subsets (after words change)
python3 -I Tools~/make_meta.py .                                            # .meta files for new assets
```

English tile labels come from the dictionary's first sense plus `Tools~/data/label-overrides.json`.
Proofread `Tools~/data/english-labels-review.csv` before release (spec §2).

## Not in this version

Pictures on meaning tiles (D13, off in the first build), sign-in and SetList upload to AlongSide
(spec §13 — local SetLists are saved in the Pinyin Tiles word shape so upload needs no new format),
Medium and larger layouts, sound/music settings.

See `THIRD-PARTY-NOTICES.md` for data, audio and font licences.
