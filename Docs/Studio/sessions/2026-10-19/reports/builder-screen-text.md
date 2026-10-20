# Review 2 — "every lab photo checks its own words": built into /root/code

## What was built

**1. `/root/code/Assets/Scripts/Diagnostics/ReviewScreen.cs` (runtime, new, 641 lines)** — `public static class ReviewScreen`
- `public static bool Enabled` (property): true unless `PlayerPrefs.GetInt("FixitFidget.ReviewScreen.Off", 0) == 1`; read once per session, settable; reset by a `SubsystemRegistration` `ResetStatics` (domain-reload-off safe).
- `public static Result Scan(Texture2D shot, string folder, string name)` — `public struct Result { public int texts, s1, s2, s3; }`. Whole body in try/catch: one `Debug.LogWarning` tagged `[Review screen]` and an empty result on any failure; never yields; one `GetPixels32()` of the shot, then per-text rect sampling; the shot itself is only read (boxes are drawn on the pixel copy). Each text is measured inside its own try/catch so one odd text can't lose the photo.
  - Reads every `TMP_Text` via `FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)`; skips: not active/enabled, empty after tag stripping (`<…>` removed, `<br>` → space, whitespace squashed), canvas/root canvas off, effective alpha < .05 (text colour α × material `_FaceColor` α × every parent CanvasGroup (honouring `ignoreParentGroups`) × `canvasRenderer.GetAlpha()` for UGUI), off screen, behind the camera (dot with camera forward ≤ near plane), rendered to a camera `targetTexture`, world-space TMP with its Renderer disabled.
  - Camera: `null` for ScreenSpaceOverlay; the canvas's `worldCamera` for ScreenSpaceCamera (null → treated as overlay, as Unity does); `worldCamera` or `Camera.main` for world-space canvases; `Camera.main` for world-space `TextMeshPro`.
  - Screen rect: 4 corners of `textBounds` (fallback `GetWorldCorners` when nothing is laid out) → `rectTransform.TransformPoint` → `RectTransformUtility.WorldToScreenPoint(cam, p)`, clamped to the screen. Rects are mapped onto the shot with `shot.width/Screen.width` factors (as AceTurnCheck does).
  - Size: (0,0,0) and (0, em, 0) projected from the text's local space, `px1080 = px × 1080 / Screen.height`; `fontSize` read as-is (no ForceMeshUpdate). em = `fontSize` for `TextMeshProUGUI`, `fontSize × 0.1` for world-space `TextMeshPro` (TMP's own non-orthographic factor; the counter phone's screen is `fontSize .19`).
  - Colour: `text.color × _FaceColor`; if the whole text is wrapped in one `<color=…>` tag that colour is used instead (so "Fixed!" in brand green is measured as green). Background: shot pixels in the rect +3 px, pixels within RGB distance .3 of the ink dropped, per-channel median of the rest via 256-bin histograms (no sorting); if < 20 % remain → `backgroundKnown=false`, `contrast=-1`. Huge blocks are sampled every n-th pixel (budget 120k). Contrast: WCAG 2 (sRGB→linear) of the ink *blended by its effective alpha* over the background vs the background.
  - Outlined: material `_OutlineWidth` > .05 or keyword `UNDERLAY_ON` (`ShaderUtilities.Keyword_Underlay`). Thin: not `FontStyles.Bold`, not a whole-text `<b>…</b>`, `_FaceDilate` ≤ .05, font asset name not matching Bold|Black|Heavy|SemiBold|Semibold|Medium|ExtraBold. Material read via `fontSharedMaterial` (never `fontMaterial`, which would instantiate).
  - Button words: the spec's regex exactly, case-sensitive; distinct matches joined "R2, Square"; `buttonLowConfidence` when only Menu/View matched.
  - Words: `WordBudget.Words(stripped)` (the codebase's own definition).
  - Owner: hierarchy path (capped at 120 chars: root/.../last three) and the nearest `MonoBehaviour` up the parents whose namespace isn't TMPro/UnityEngine/Unity.*; plus `root` (root object's name) because e.g. `HudCorners` builds its canvas at the scene root so no script is above its texts — the line then reads `(no script above it, under "HUD corners (while playing)": path)`.
  - Severity exactly as specified (S1 <18 px or contrast <3 unoutlined; S2 <26 px, contrast <4.5 unoutlined, or a button word; S3 thin & <32 px, or >25 words); `reasons` lists every rule that applied; severity = the worst. `Grade(TextMeasure)` is public so the gather re-grades aggregates with the same rule.
  - Writes: `<name>.text.json` (JsonUtility of `PhotoCheck { name, folder, time, screen/shot sizes, texts, s1..s3, skipped, items[] }`), appends `<name>: <n> texts, S1 a, S2 b, S3 c` + `  [S2] 21 px, contrast 3.4, thin, button word R2, "Hold R2 to wipe" (HudCorners: Canvas/…/Label)` lines to `<folder>/text-check.txt`, and saves `<name>.text.jpg` (2 px boxes: red S1 / amber S2 / yellow S3, rect +3 px; RGB24 copy, JPG 85, destroyed). Numbers in text files via invariant culture (`FormattableString.ToString(InvariantCulture)`); JsonUtility is culture-independent.
- `public static void SnapNow(string name)`: Play mode only; hidden `HideAndDontSave` + `DontDestroyOnLoad` runner (private nested `Runner : MonoBehaviour`, the same pattern as RecapPhoneCheck's nested `MoveProbe`), waits for end of frame, captures, saves `Logs/Review/screens-<yyyy-MM-dd>/<HHmmss>-<name>.jpg` (JPG 90), scans it, logs one line with counts and the folder.

**2. `/root/code/Assets/Editor/ReviewScreenGather.cs` (editor, new, `#if UNITY_EDITOR`, 260 lines)**
- Menu `Fixit Fidget/Review/Screens 1 - Gather every lab photo's word checks (edit mode, read-only)` (validator: not in/entering Play mode; refuses with an error otherwise): finds every `*.text.json` under `Logs/` (recursive) with a last-write time in the last 3 days, parses with `JsonUtility.FromJson<ReviewScreen.PhotoCheck>`, aggregates by the stripped text string (smallest px, lowest known contrast, thin/outlined OR'd, owners set, photos seen / photos flagged, up to 5 `<name>.text.jpg` paths relative to the project), re-grades each finding with `ReviewScreen.Grade` on its worst measures, ranks S1→S3 then by flagged-photo count then text, writes `Logs/Review/text-<yyyy-MM-dd_HHmmss>/report.txt` (header: photo/folder counts and date span, the rules, totals by severity, totals by owner script; then each finding: severity, px, contrast, thin/not thin, outlined/no outline, words, flagged-in-N-of-M, the text, drawn-by, why, photo paths) and `findings.csv` (all texts, severity 0 = fine, CSV-quoted). Console: one line with counts and the folder.
- Menu `Fixit Fidget/Review/Screens 2 - Photograph the Game view now and check its words (Play mode)` (validator: Play mode; logs an error outside it): calls `ReviewScreen.SnapNow("manual")`.
- Beyond the spec: each finding also shows the *typical* (median over all sightings) px and contrast when they differ from the worst — one photo of the sign mid-flip or a card half-faded gives a text a 0-px / low-contrast worst number, and a reader must see that it is the exception (`[S1] 1 px (typically 40 px), …; flagged in 1 of 212 photos`). The ranking rule stays the spec's.

**3. One-line edits at every `ScreenCapture.CaptureScreenshotAsTexture` site** (14 sites, 13 files; nothing else in them changed). The exact inserted line, right after the capture and before the texture is destroyed/encoded, is
`        ReviewScreen.Scan(shot, folder, name);   // Review 2: the words in this photo, measured (reads the shot only; never throws)`
in:
- `Assets/Scripts/Diagnostics/PlayLab.cs` (Photo, line 118)
- `Assets/Scripts/Diagnostics/Day1GuideCheck.cs` ×2 (CloseOnHead line 587, before `Texture2D big = null;`; Photo1080 line 633, before `Texture2D save = shot;` — scans the raw full-screen shot, not the 1920×1080 resample)
- `Assets/Scripts/Diagnostics/DayIntroCheck.cs` (Photo1080, line 752, before `Texture2D save = shot;`)
- `Assets/Scripts/Diagnostics/RecapPhoneCheck.cs` (line 884)
- `Assets/Scripts/Diagnostics/NightZeroCheck.cs` (line 627)
- `Assets/Scripts/Diagnostics/AceTurnCheck.cs` (line 712, before `photos++`/the try)
- `Assets/Scripts/Diagnostics/NightOneCheck.cs` (line 586)
- `Assets/Scripts/Diagnostics/ConversationCheck.cs` (line 350)
- `Assets/Scripts/Diagnostics/NightTwoCheck.cs` (line 876)
- `Assets/Scripts/Diagnostics/JuiceCheck.cs` (line 494)
- `Assets/Scripts/Diagnostics/GraceAtHomeCheck.cs` (line 1396)
- `Assets/Scripts/Diagnostics/BarkCheck.cs` (line 428)
- `Assets/Scripts/Diagnostics/NightTour.cs` (line 232) — its photo name is computed into `file`, so the line there is `ReviewScreen.Scan(shot, folder, Path.GetFileNameWithoutExtension(file));   // Review 2: …`, placed after the `string file = …` line and before `File.WriteAllBytes`.
The "wrapped so it can never throw" is inside `Scan` (everything caught; its static initialisers are plain Regex/arrays that cannot throw). The labs' JPGs, counts, timing and `photos++` are untouched; `Scan` runs synchronously in the same end-of-frame moment as the capture. No lab counts warnings as errors (checked PlayLab.Heard and JuiceCheck.Heard).

No `.meta` files were created (the snapshot has none; Unity makes them on the Mac's reimport: `Fixit Fidget › Checks › Pick up scripts changed from outside`).

## Verification done here (no Unity, no compiler on this box)
- tree-sitter C# parse of both new files and all 13 edited files: clean (the only flag was pre-existing `var with = …` in BarkCheck.cs, a contextual-keyword quirk Roslyn accepts).
- The background-median + WCAG maths replicated in Python on a real pack photo (BenchWords S1-first-repair-pad.jpg): brand green on paper → 4.5, light text on the dark band → 12.9, "Lens" over the camera → 10.1 — plausible and matching standard calculators.
- Stripping / button regexes exercised on sample UI strings (`<b>Grace:</b> …` not counted bold; `<color=…>Fixed!</color>` colour picked up; "Drinks Menu" → Menu, low confidence; `<br>` → a space; `<sprite=…>` dropped).

## Known limits
- Occlusion isn't detected: a text on an active canvas under a higher-sorted screen (or a world text behind a wall) is still measured; `glyphShare` in the JSON hints at it (≈0 = probably covered) but is *not* used to suppress flags, because thin light-on-dark small text also yields ≈0 (exactly the texts she complained about).
- Partial rich-text colour/alpha/size tags (`<color>` round one key, `<size>`, `<alpha>`) are ignored; only a tag round the whole text is honoured. Contrast then uses the base colour; the median background still comes out right (the tagged words are a minority of pixels).
- Aggregation is by exact stripped text: the same words in two different labels are one finding (owners listed as a set), and "View"/"Menu" as ordinary words are S2 at low confidence as the spec says. The regex is the spec's exactly: DualSense "Create", Nintendo ZL/ZR/Plus/Minus and Xbox LS/RS aren't matched.
- Background is a single median: a text across a half-dark/half-light background is judged against one side.
- `px1080` is one em; a 16:10 screen scores the HUD slightly smaller than a 16:9 one (the CanvasScaler's 0.5 match), which is what the player sees.
- Cost: per photo roughly one more JPG encode plus a GetPixels32 (8 MB at 1080p, ~16 MB at the Mac's 2560×1600) — about the same again as the lab's own encode, in the photo frame only. `<name>.text.jpg` is written for every photo even when nothing is flagged (the spec says to save it; one `if (photo.s1 + photo.s2 + photo.s3 > 0)` in `Scan` around `SaveMarked` would halve the disk use if he prefers).
- The 3-day window uses file write time; a pack copied in from another machine keeps its own times.

## Decisions his/the integrator's to overrule
- Whole-text `<color>` tag taken as the text's colour; contrast computed on the alpha-blended ink (a half-faded card measures as half-faded).
- `root` object name recorded and shown when no script sits above a text (HudCorners, UiSkin-built canvases at the root).
- Gather shows typical (median) px/contrast beside the worst; ranking unchanged.
- `Enabled` is cached once per session (set the property to flip it mid-session).

## APIs I could not compile here and am least sure of
- `TMPro.ShaderUtilities.ID_FaceColor`, `ID_FaceDilate`, `ID_OutlineWidth` (property-ID ints; `ID_UnderlayColor`/`Keyword_Underlay` are already used by UiSkin.cs, so the class is there — these three names are the standard ones).
- `TMP_Text.richText` (bool property) and `Graphic.canvasRenderer.GetAlpha()` — standard, but unverified in this Unity.
- `System.FormattableString.ToString(IFormatProvider)` via my `Inv($"…")` helper (present in .NET Standard 2.1 and .NET Framework profiles; every call site passes an interpolated string). If the API level somehow lacks it, replace `Inv` bodies with `string.Format(CultureInfo.InvariantCulture, …)`.
- A private nested `sealed class Runner : MonoBehaviour` inside a static class, added with `AddComponent<Runner>()` — nested MonoBehaviours are allowed in static classes and the repo already adds a private nested MonoBehaviour (`RecapPhoneCheck.MoveProbe`).
- `Texture2D.SetPixels32` onto an `RGB24` texture (documented as supported) and `Object.FindObjectsByType<TMP_Text>(FindObjectsInactive, FindObjectsSortMode)` (Unity 2023+/6 overload; BenchWordsCheck uses the one-argument form).
Everything else (`textBounds`, `fontSharedMaterial`, `canvas`/`rootCanvas`, `CanvasGroup.enabled/ignoreParentGroups`, `rectTransform`, `WordBudget.Words`, JsonUtility on `[Serializable]` nested classes) is already exercised by files that compile on the Mac.