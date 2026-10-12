# Retest evidence: 84accf6

Retest of `com.liann.mahjongtones` 0.2.0 at `84accf6b39495888bbeeea2671a4316815fae571` on DSJ-PC, Unity 6000.6.5f1.
Screenshots are cropped, redacted (email address and Windows user name) and have no metadata. The results table is in the PR #1 comment.

| File | Shows |
|---|---|
| 03-step3-start-screen.png | Start screen (Editor, 1080×1920) |
| 04-step3-starter-board.png | Starter board, 16 tiles |
| 05-step3-starter-hint.png | Hint ring on a free matching pair |
| 06-step3-small-board.png | Small board, 24 tiles, 2 layers |
| 07-step3-medium-board.png | Medium board, 36 tiles, 3 layers |
| 08-step3-large-board.png | Large board, 72 tiles, 4 layers (1920×1080) |
| 09-step3-large-results.png | Large round results |
| 10-step3-save-to-setlist.png | Save to SetList while signed out |
| 11-step3-small-challenge-timer.png | Challenge Small timer starts at 5:00 |
| 12-step3-medium-challenge-timer.png | Challenge Medium timer starts at 8:00 |
| 13-step3-large-challenge-timer.png | Challenge Large timer starts at 15:00 |
| 14-step3-stats-before-reset.png | Stats for all four boards |
| 15-step3-stats-after-reset.png | Stats after Reset |
| 16-step4-editor-signed-out.png | Editor Account screen, signed out |
| 17-step4-editor-signed-in-session-only.png | "Signed in for this session only. Secure storage is unavailable." |
| 18-step4-editor-upload-result.png | Upload result: "AlongSide can't be reached…", 36 words kept |
| 19-step4-editor-after-sign-out.png | "Signed out. Your SetLists stay on this device." |
| 20-step4-editor-after-play-restart.png | Signed out after Play restart |
| 21-step5-windows-build-output.png | `Builds/Windows/LiANN Mahjong Tones.exe` |
| 22-step5-datapath-before-launch.png | Disposable profile with dummy `liann-tones.account.v1.json` |
| 23-step5-exe-start.png | Windows player start screen |
| 24-step5-datapath-after-launch.png | Legacy v1 file deleted on launch |
| 25-step5-exe-code-entry.png | Player waiting for the email code |
| 26-step5-datapath-after-code-request.png | `liann-tones.account.v2.dpapi` (binary) and SetLists file, no v1 file |

## Follow-up at 3e83edb (Windows player rebuilt from 3e83edbcbbdc485898db1f3db40d12ba17daea7e)

| File | Shows |
|---|---|
| 27-step5-3e83edb-code-screen.png | Code screen: "6-digit code" hint, focus border, Back sized to its text |
| 28-step5-3e83edb-signed-in.png | Signed in on the Windows player (email hidden) |
| 29-step5-3e83edb-signed-in-after-reopen.png | Still signed in after closing and reopening the player |
| 30-step5-3e83edb-send-countdown.png | Send button disabled with countdown (Editor, cooldown set directly, no email sent) |
| 31-step5-3e83edb-datapath-after-signout.png | Rendered text of the folder listing after Sign out: no .dpapi or .tmp, SetLists kept |
