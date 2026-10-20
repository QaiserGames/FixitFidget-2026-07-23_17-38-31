# Builder task: "Review 2 — every lab photo checks its own words" (runtime + editor), Fix It Fiasco

You write code into the cloud copy of the repo at `/root/code` (a snapshot of the developer's Mac repo, branch playtest-2, Unity 6000.5.2f1, URP 17, TextMesh Pro, Input System). You do NOT touch his Mac or run Unity; the integrator reviews your code and runs it on the Mac. Read `/root/code/CLAUDE.md` and `/root/code/Docs/Studio/BIBLE.md` §1, §2, §6 first, then `Assets/Scripts/Diagnostics/PlayLab.cs`, then this spec.

## Why it exists
His girlfriend (dyslexic, on a PlayStation pad) played Days 1–3 on 19 Oct and "felt overall lost most of the time". Among the reasons: text too small and too thin ("your text needs to be bolder"; the day and time "way too small"; the bench's "flip it over" "so tiny"), buttons written as words ("Instead of writing out square, just put the icon"), notes that vanish before a slow reader finishes. He asked for "something that reviews EVERYTHING AND EVERYTHING". The game's self-driving checks ("labs", `PlayLab` subclasses and a few others) already photograph every moment they play; this tool makes every one of those photos also measure the words on screen at that instant, so a too-small, too-faint or button-word text can never pass unseen again.

## Files you write (complete files)
1. `/root/code/Assets/Scripts/Diagnostics/ReviewScreen.cs` (runtime; the labs are runtime code). 
2. `/root/code/Assets/Editor/ReviewScreenGather.cs` (editor, `#if UNITY_EDITOR … #endif`).
3. Minimal edits (one call each, wrapped so it can never throw) in every place that captures a lab photo: `PlayLab.Photo` and the other files that call `ScreenCapture.CaptureScreenshotAsTexture` (grep `/root/code/Assets` for it: JuiceCheck, NightTwoCheck, NightOneCheck, NightZeroCheck, DayIntroCheck, Day1GuideCheck, BarkCheck, GraceAtHomeCheck, NightTour, ConversationCheck, AceTurnCheck, RecapPhoneCheck, …). Each gets `ReviewScreen.Scan(shot, folder, name)` right after the capture and before the texture is destroyed, using that file's own folder/name variables. Change nothing else in those files. List every file you touched.

## ReviewScreen (runtime)
`public static class ReviewScreen`
- `public static bool Enabled` — default true; off when `PlayerPrefs.GetInt("FixitFidget.ReviewScreen.Off", 0) == 1`.
- `public static Result Scan(Texture2D shot, string folder, string name)` — call in the same frame as the capture (end of frame), so the UI it reads is the UI in the photo. Never throws (catch everything, log one warning with the tag `[Review screen]`, return an empty result). Must cost little: one `GetPixels32` of the shot, then per-text rect sampling.
- What it reads: every active, enabled `TMP_Text` (both `TextMeshProUGUI` and world-space `TextMeshPro`) via `Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)`; skip if: its text is empty after stripping rich-text tags (`<…>`), its effective alpha (text colour alpha × vertex alpha × every parent `CanvasGroup.alpha` × the canvas enabled) < .05, it's off screen, or (world-space) behind the camera. For each kept text:
  - **screen rect**: from `textBounds` (rendered text, local) transformed to world then to screen (UGUI: `RectTransformUtility.WorldToScreenPoint(cam, p)` with `cam = null` for ScreenSpaceOverlay canvases, else the canvas's `worldCamera`; world-space TMP: `Camera.main`). Clamp to the screen.
  - **size at 1080p**: the on-screen height of one em: project (0,0,0) and (0, fontSize, 0) in the text's local space to the screen; px = distance; `px1080 = px × 1080 / Screen.height`. Use `fontSize` as TMP has it after auto-size (TMP writes the computed size there; if `enableAutoSizing`, read `fontSize` after `ForceMeshUpdate()` is NOT allowed (it would change the frame) — just read it).
  - **colour**: `text.color` × the font material's `_FaceColor` (when present), as sRGB.
  - **background**: sample the shot within the screen rect (expanded 3 px); drop pixels within a colour distance of the text colour (the glyphs); background = per-channel median of the rest (if < 20 % remain, mark background "unknown").
  - **contrast**: WCAG ratio from relative luminance (sRGB → linear) of text vs background.
  - **outline/shadow**: the material has `_OutlineWidth` > .05 or the `UNDERLAY_ON` keyword → "outlined".
  - **weight**: font asset name, `fontStyle` (Bold flag), material `_FaceDilate`; "thin" = not bold, dilate ≤ .05, and the font name not containing Bold/Black/Heavy/SemiBold/Semibold/Medium/ExtraBold.
  - **button words**: regex `\b(Square|Triangle|Cross|Circle|R1|R2|R3|L1|L2|L3|Options|Share|Touchpad|LB|RB|LT|RT|Menu|View)\b` on the stripped text (case-sensitive; note "Menu"/"View" may be ordinary words — mark those low confidence).
  - **words**: count of words in the stripped text.
  - **owner**: hierarchy path of the text (`Canvas/…/Label`) and the nearest `MonoBehaviour` type name up the parents that isn't TMP/UI (e.g. `HudCorners`) — so a reader knows which script draws it.
- **Severity per text**: S1 if `px1080 < 18`, or contrast < 3 and not outlined; S2 if `px1080 < 26` (Xbox Accessibility Guideline 101's 26 px minimum at 1080p), or contrast < 4.5 and not outlined, or a button word; S3 if thin and `px1080 < 32`, or more than 25 words in one block.
- **Writes** (folder = the lab's photo folder): `<name>.text.json` (every text with all measures and flags), appends to `<folder>/text-check.txt` one block per photo: `<name>: <n> texts, S1 a, S2 b, S3 c` then one line per flagged text: `  [S2] 21 px, contrast 3.4, thin, "Hold R2 to wipe" (HudCorners: Canvas/…/Label)`. And saves `<name>.text.jpg`: the shot with a 2 px rectangle around each flagged text (red S1, amber S2, yellow S3) — draw on a copy of the pixels, encode JPG 85, destroy temporaries.
- `public struct Result { public int texts, s1, s2, s3; }`.
- A Play-mode helper: `public static void SnapNow(string name)` — starts a coroutine on a hidden `DontDestroyOnLoad` runner that waits for end of frame, captures, saves `Logs/Review/screens-<yyyy-MM-dd>/<HHmmss>-<name>.jpg` and scans it (for the integrator's manual sweeps).

## ReviewScreenGather (editor)
- Menu `Fixit Fidget/Review/Screens 1 - Gather every lab photo's word checks (edit mode, read-only)`: finds every `*.text.json` under `Logs/` written in the last 3 days (file time), aggregates by the stripped text string (the same words in many photos are one finding), ranks S1 → S3 then by how many photos show it, and writes `Logs/Review/text-<yyyy-MM-dd_HHmmss>/report.txt` (a header with totals by severity and by owner script; then the findings: severity, px, contrast, thin/outlined, words, the text, owner, and up to 5 photo paths) plus `findings.csv`. Console one line with the folder.
- Menu `Fixit Fidget/Review/Screens 2 - Photograph the Game view now and check its words (Play mode)`: calls `ReviewScreen.SnapNow("manual")` (refuses outside Play mode).

## Hard rules for this codebase
- Unity 6.5: `GetInstanceID()` does not compile — never use it. Use `FindObjectsByType` (not FindObjectsOfType).
- Comments explain WHY, plainly, quoting her/his words where a line answers one (see the header comments in PlayLab.cs and HudCorners.cs for the voice). A header block explains the tool and the playtest it answers.
- The labs must behave exactly as before apart from the extra files: never change timing logic, never yield inside Scan, never throw.
- No allocations in Update loops; this only runs at photo time.
- Use invariant culture for numbers in files.

## When you're done
Return: what you built, every existing file you changed (with the exact one-line change in each), the menu names, known limits, and APIs you were unsure would compile.
