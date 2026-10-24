# Review of builder N: the night's notebook, with pictures (23 Oct 2026)

Reviewer: a separate agent who did not see it built. Base `76df1b4` (= Mac `8033112`). The work is the uncommitted diff in `/root/wt23/N`:
- 3 changed files: `Assets/Scripts/NightNotebook.cs` (rewritten), `Assets/Editor/NotebookRuleChecks.cs` (adds `CheckPictures`), `Assets/Editor/NotebookPlayCheck.cs` (starts the page check);
- 4 new files: `Assets/Scripts/NoteSubjects.cs`, `Assets/Scripts/NotePhotos.cs`, `Assets/Editor/NotePhotoSteps.cs`, `Assets/Scripts/Diagnostics/NotebookPageCheck.cs`.

Phase 1 (findings marked P1) was written before I read the builder's report. Phase 2 (P2) checks the report's claims and its edits outside N's files.

Syntax (`csparse.py`): all 7 files are clean. So are scratch copies with the outside edits applied: J's `RecapPhone.cs` with edits A1–A4, `NightZeroCheck.cs` with edit C, and `NightNotebook.cs` with fix 1.

No blockers: by my reading nothing fails to compile, nothing touches saves, and the photo step leaves his scene alone. Two Majors should be fixed before it goes to his Mac; both are a few lines.

## Findings, worst first

**1. [Major] (P1) `Assets/Scripts/NightNotebook.cs:315` (with `:383`): a pressed-open entry's notes are cut to the room left below where it sits, not to the screen, so an entry in the middle of the page shows a short version and never moves up.**
- What's wrong: `Row(e, i == open, top, ViewHeight - top, ...)` passes the room under the row. Line 383 stops adding notes once `panelTop + bottom + 50 > room` and writes "+N more on the phone". The shortened row then fits, so `openFits` is true and `Layout()` never moves the entry to the top, where it would show whole. This contradicts the header ("each one whole") and the report's decision 10 ("+N more on the phone *when an open entry overflows*").
- Evidence: I modelled `Build`/`Row` with the real Night 0 texts (the page check's first notebook). Pressing down to "The man at the bins" (the third entry) shows only 1 or 2 of his 3 pages:
  - in 7 of 12 font metrics between 1.2 and 1.5 em line height;
  - at 1.6 em (the line height the report gives for Baloo 2) in 2 of 3 glyph widths.
  
  The first entry, the only one the lab checks, is always whole. This is the moment of Night 0's hand-over ("His notebook is yours now. N Read it.").
- Smallest fix (line 315):
  ```csharp
  Shown s = Row(e, i == open, top, ViewHeight - HeadingHeight, out float height);
  ```
  The notes are laid out for the room the entry would have at the top. If that doesn't fit where it sits, the existing `!Build(first) && open > first` branch moves it there. The model gives 3 of 3 in every case.
- Keep it caught: in `Assets/Scripts/Diagnostics/NotebookPageCheck.cs`, after line 144 (the D-pad-down check), add:
  ```csharp
  page.More();   // the third entry: the man at the bins, his three pages
  yield return Frames(2);
  NightNotebook.Shown him = page.Visible.FirstOrDefault(v => v.open);
  Check(him != null && him.entry.key == NoteSubjects.Lodger && him.notes.Count == him.entry.facts.Count,
      $"...the man at the bins' pages, whole wherever he sits ({him?.notes.Count} of {him?.entry.facts.Count})");
  ```
  The loop after it still closes them all. The page check then reports 77 checks (70 with the word check off).

**2. [Major] (P1, P2) `Assets/Scripts/NotePhotos.cs:397-409` (`Draw`) with `Assets/Scripts/NightNotebook.cs:265-266` (`State`): the drawings and maps are drawn on the main thread when first needed, at about a tenth of a second each, and the maps are drawn again for every 15° the overhead camera turns while the page is open.**
- Evidence: I benchmarked the builder's exact `DrawSubject`/`DrawMap` (Mono 6.8, stand-in `Color`/`Vector2`/`Mathf`) on this 2.1 GHz Xeon:

  | Drawing | ms each |
  |---|---|
  | person | 117 |
  | house | 133 |
  | door | 107 |
  | bins | 212 |
  | corner | 138 |
  | gnome | 117 |
  | cups | 98 |
  | crate | 45 |
  | cone | 81 |
  | café | 86 |
  | a map | 118–123 |

  Expect roughly half on his Mac's editor. What that means in play:
  - **First open, before the photos exist:** the first N press of a session draws up to five subjects plus one or two maps, a visible freeze.
  - **After the photos:** it still draws the maps.
  - **Orbiting:** `State()` includes the yaw in 15° steps, and the right stick orbits the overhead camera at night. With the page open, turning the camera rebuilds the page and draws new maps once a second, a stutter until every step is cached.
  - **Memory:** the cache keeps (place × 15° step × tonight's or not) textures forever, up to 240 readable 128 px RGBA textures.
  - **The phone:** with edits A1–A4, its Notes pays the same first-draw cost per picture.
- Smallest fix:
  - (a) In `Draw`, take one sample at the pixel's middle instead of 2×2. `Inside(d) = Clamp01(.5f - d)` already smooths every edge over one pixel, so it looks the same at about a quarter of the cost. Replace lines 397–409 with:
    ```csharp
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            pixels[y * size + x] = paint(x + .5f, y + .5f);
    ```
  - (b) Delete the yaw from `State()` (lines 265–266 become `stateText.Append(stage).Append('|').Append(errand).Append('|').Append(PadInput.UsingPad ? 'p' : 'k');`). A map then takes the camera's turn when the page opens or its contents change. That bounds the cache, and orbiting never rebuilds the page.
  - Better, if he wants the map to follow the camera live: draw each map once, north up, and turn its `Image`.

**3. [Minor] (P1) `Assets/Scripts/NightNotebook.cs:174-180` (`More`): the press stops on entries with nothing behind them.**
- These are tonight's back door, the bins, the café, and the man at the bins with no pages.
- On Night 0's bag, for example, the first press visibly does nothing but play the page sound.
- Fix, replacing line 177:
  ```csharp
  int next = open;
  do next = next + 1 >= entries.Count ? -1 : next + 1;
  while (next >= 0 && entries[next].facts.Count == 0);
  open = next;
  ```
- None of the page check's sequences open such an entry, so the lab still holds.

**4. [Minor] (P1, P2) `Assets/Scripts/NotebookRecap.cs:17-18, 21-22, 82, 106-107`: the comments still say the night's page uses `Page`/`People`.**
- The report's edit B names lines 17–18, 22 and 82 but gives no wording ("given in full in the builder's message", and this report is that message). It also misses 106–107.
- Exact replacements (comments only; Tests/NotebookRules and HomeRules are unaffected):
  - 17–18:
    ```
    // The recap phone's Notes app (playtest 2, step 2) lays the same notebook out person by person from People.
    // The night's page is laid out by subject since 23 Oct 2026 (NoteSubjects.Entries, NightNotebook); Page is the
    // notebook as one page of text, kept for the checks.
    ```
  - 21–22:
    ```
    // notebook is laid out: they are the notebook's first pages, in his hand, so Page and the phone show them in italics
    // under his heading (the night's page puts them behind "The man at the bins", with his square bullet, since 23 Oct
    // 2026). What Ace learned sits under them.
    ```
  - 82:
    ```
    /// The whole notebook as one page of text (the night's page until 23 Oct 2026; now the checks read it):
    ```
  - 106–107:
    ```
    /// The notebook person by person, in the order Ace first learned about them (the recap phone's Notes; the night's
    /// page until 23 Oct 2026). Each person's facts: where they live first, since
    ```

**5. [Minor] (P1, P2) `Assets/Scripts/Diagnostics/NightZeroCheck.cs:301-303`: the old check tests a page the night no longer shows, and the builder's replacement (edit C) doesn't check everything its message claims.**
- The old check asserts "the notebook's page opens with his pages, in italics" on `NotebookRecap.Page`. It still passes but tests text the night no longer shows.
- Edit C is right, compiles and works:
  - `saves` and `ledger` are fields, `pages` is a local just above it, and `System.Linq` is imported;
  - nothing later in `Run` uses the removed `page` local;
  - after the deal the errand is the gnome, so Barnaby is on top and the man has 3 inherited pages;
  - it wisely tests `NoteSubjects.Entries` rather than opening the live page. Opening it would set `EverOpened` and fail the "notes" nudge check eight lines later.
  - No coverage is lost: `NotebookRuleChecks.RunAll` still checks the italics of `Page`.
- But its message says the man is "first of the people", and it only checks he is among them. Add one line to its condition:
  ```csharp
        && onPage.FindIndex(e => e.group == NoteGroup.People) == onPage.IndexOf(him)
  ```

**6. [Minor] (P2) `Assets/Scripts/RecapPhone.cs` (builder J's, `/root/wt23/J`): edits A1–A4 are correct and apply after J, but the line numbers move, A4 is three lines, and the pictures carry finding 2's cost.**
- J left `PersonRow`/`PersonCard`/`NotebookLine` textually unchanged; they only moved. At 8033112 / in J's file:
  - A1: `Avatar(card, CardPadX, 12f, 36f, …)`, 1628 / 2285;
  - A2: `Avatar(card, CardPadX, 14f, 40f, …)`, 1655 / 2312;
  - A3: `Picture(card, circle, Accent, CardPadX + 1f, …)`, 1677 / 2334;
  - A4: the icon, 1088 / 1745–1747.
- Each anchor matches exactly once in J's file. I applied all four to a scratch copy: it parses clean and has no name clashes.
- The thumbnails don't break the phone:
  - they are non-raycast children of the card, so J's button, squish and cascade carry them;
  - J's Notes checks match names and sentences and `Fits()` (text inside the phone's width), so they still hold with the narrower fact column.
- Say in the report that A4 replaces all three lines:
  ```csharp
          RectTransform square = Box("Icon", card, CardPadX, 11f, 32f, 32f);
          Paint(square, Hex(0xece7f5), 9f);
          Picture(square, note, Hex(0x6b5aa0), 7f, 7f, 18f, 18f);
  ```
  becomes
  ```csharp
          NotePhotos.Thumb(card, NoteSubjects.KeyOf(fact), CardPadX, 11f, 32f);
  ```
- Each new picture is drawn on first use (finding 2) until the photos are committed.

**7. [Minor] (P1) `Assets/Scripts/NightNotebook.cs:361-362`: a place's map card is drawn over its photo, not "tucked under it" as the header says.**
- It's made after the polaroid, so it renders on top. It spans x 74–144 and y 57–127 of the row; the photo spans x 21–107 and y 13–99.
- It covers the lower-right fifth of the house's photo. On a one-line row it also runs a few px past the 124 px row.
- Check photo `01-tonight-the-gnome` on the Mac. If it hides Grace's door, move it right (x `14f + PolaroidWidth - 26f`), or keep it as the look.

**8. [Minor] (P1) `Assets/Scripts/NightNotebook.cs:178`: every press plays `notebook.page`, whose meaning elsewhere is "A new line in the notebook" (`SoundBank.cs:217`, played when a fact is learned).** Turning a page now sounds like learning something. Sounds are his to redo, so flag it to him. Smallest fix: `Sfx.Play2D("notebook.page", .4f)`, or no sound.

**9. [Minor] (P2) `reports/N-builder-report.md` §5.1: the "pass" line for the photo step is the zero-failure line, but the same paragraph expects `COULDN'T cones`.** If no cones are in the scene, the console line will be the yellow warning `[Notebook pictures] 12 photographed, 0 kept (not this step's), 1 couldn't (see above; …)`. Tell the integrator that this is a pass.

**10. [Minor] (P2) `reports/N-builder-report.md` §3 and §6 have small gaps.**
- §6 says "No other night code reads D-pad down or Tab". `Barks` does read D-pad down while choosing; it's guarded, as §2 says.
- §6 doesn't list finding 2's cost as a risk.
- §3 (his to overrule) omits:
  - the page grew from 540×720 to 560×860 at 1080p, 80% of the screen's height; it still overlaps the right end of a long night note (notes at order 90, x ≤ about 1378, y 770–830), as the old page did;
  - `NoteSubjects.cs` is a second new runtime file beyond the spec's "small runtime loader" (§2 explains why);
  - the reused page sound (finding 8).

**11. [Minor] (P1) `Assets/Editor/NotePhotoSteps.cs:513-518`: in a fallback path, a copy lands in his open scene first.** If the Unity 6 `InstantiateParameters` call ever throws, `Object.Instantiate(original)` puts the copy in his open scene, marking it dirty, before it moves to the studio. It's identical to the deployed `ReviewModelSheets.Copy`, so it's acceptable; noting it only.

**12. [Note] (P1) Two design points, already covered or harmless.**
- D-pad down walks down, but D-pad up closes the page rather than stepping back; re-opening starts at the top. The report already puts this to him (§3.4, question 2).
- `corner` and `crate` are photographed, but no fact or stage maps to them yet. That's harmless.

## Checked and sound (P1)

- **Compiles.**
  - Every identifier, member and signature used exists, checked by grep. This covers the UiSkin, input, night-state, ledger, story-data, place and photo-helper members, and the internal editor helpers `BenchScale.Studio`, `AceBodySteps.*` and `PolygonNpcSetup.SkinToWorld`, which share the editor assembly. There are no asmdefs.
  - No duplicate locals (CS0128/CS0136). A tree-sitter scope scan of all seven files plus the patched scratch copies finds none, and it catches planted conflicts.
  - No `UnityEditor` in `Assets/Scripts`.
  - No `GetInstanceID`/`FindObjectsSortMode` in N's files.
  - `InstantiateParameters` and the preview-scene trick are the deployed `ReviewModelSheets` code.
  - `CheckPictures` is inside `#if UNITY_EDITOR`.
  - `NoteSubjects.cs` is byte-identical to the copy in the builder's mcs harness, which I re-ran: ALL PASS.
- **Saves.**
  - Nothing new serializes into the save. `SaveData` stays at version 5, `index.json` is a Resources asset, and `NightNotebook` holds no serialized fields.
  - `KeyOf` maps every fact the real writers produce: every `NotebookHooks` path (Grace's and the Officer's own repairs included, via `KeyOfPerson`), `NightZero`'s hand-over, `Lodger`'s `PageFor` and `SaveManager`'s backfill.
  - Old or unknown ids fall back by `who`.
- **Runtime.**
  - Missing PNGs fall back to drawings, so a polaroid is never blank.
  - Maps appear where a spot exists (from the index or the scene at night).
  - No per-frame garbage. `State()` makes a few small allocations once a second while the page is open; the old page re-laid a whole TMP text every second.
  - The page draws over the HUD corners (order 40 against 2).
- **Input.**
  - At night only `Barks` reads D-pad down, and it's guarded.
  - Tab has no other night use: `ConversationController`'s Tab and `BenchCards`' are day-only, and the page exists only during a night walk.
  - N / D-pad up open and close exactly as before.
  - Every public member others use is kept: `EverOpened`, `Open`, `Opened`, `FactsShown`, `Begin`, `End`, `Show`, `Describe`.
- **The photo step.**
  - It never overwrites a PNG it didn't write. The index keeps each written file's fingerprint, and a missing index means "kept".
  - It uses one studio per subject and copies scene objects into the studio's preview scene. Posing parents the look under the studio actor, so nothing lands in his scene.
  - It sets importer settings only on PNGs it wrote.
  - It reports every subject.
- **Labs.**
  - No check is weakened: two messages are reworded with the same conditions.

## The report's claims I verified (P2)

- 14 subjects, 39 facts in `EveryFact`, 23 short forms, and `CheckPictures` = 257 assertions, so `[Notebook rules] PASS: 323 assertions` (66 + 257). My independent count agrees.
- The page check is 76 checks (69 with the word check off); my count agrees. It becomes 77/70 with finding 1's check.
- The fact-to-subject table in §5 matches the real writers.
- The tonight lines, titles and order match `NightCycle.NowLine`.
- Sizes are 26 px or more, with contrast of at least 6.9:1 (my calculation).
- 4 entries fit on the first screen (at most 5) at the stated 1.6 em.
- The scratch harness passes as claimed.

**Verdict: ready after these fixes.** Fix the two Majors (finding 1's one-line room fix plus its lab check, and finding 2's single-sample `Draw` plus dropping the yaw from `State()`) and give the integrator the exact `NotebookRecap` wording from finding 4; the other Minors can ride along or wait for his call.
