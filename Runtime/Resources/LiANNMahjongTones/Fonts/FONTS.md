# Fonts (SIL Open Font License 1.1 — see OFL.txt)

| File | Family | Copyright | Use |
|---|---|---|---|
| NotoSansSC-Bold.ttf | Noto Sans SC Bold (spec D10), subset to the hanzi in words.json + ASCII + every tone-marked vowel | © 2014-2021 Adobe (Source Han Sans) / Google (Noto) | Hanzi, tone-marked pinyin, any text mixing them |
| Nunito-ExtraBold.ttf | Nunito ExtraBold ("LiANN V5" brand face), ASCII + partial Latin | © 2014 The Nunito Project Authors | UI text, English labels, toneless pinyin |

Nunito does not contain ǐ ǒ ǖ ǘ ǚ ǜ (and their capitals), so never render tone-marked pinyin in Nunito.
Rebuild the Noto subset whenever words.json changes: `Tools~/fonts/get_fonts.py`.
