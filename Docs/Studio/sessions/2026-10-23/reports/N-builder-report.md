# Builder N: the night's notebook, with pictures (23 Oct 2026)

(As handed back by builder N. Copy: /root/wt23/N, HEAD 76df1b4 = Mac 8033112, uncommitted.)

## 1. What I did (files)
- M `Assets/Scripts/NightNotebook.cs`: the night's page is rebuilt with pictures first. Each entry is a polaroid, a bold name and one line of at most 8 words. Groups: "Tonight" (amber) / "People" / "What you've learned". Tab or D-pad down opens one entry's notes. The page scrolls. The public members others use are all kept.
- ?? `Assets/Scripts/NoteSubjects.cs` (new, pure, no Unity types): the 14 subjects, which subject a fact is about (`KeyOf`, from id and who, nothing saved), the short forms, the page's entries in order, and `EveryFact` (39 facts) for the checks.
- ?? `Assets/Scripts/NotePhotos.cs` (new runtime loader): loads PNG + index.json from Resources/UI/NotePhotos. Falls back to a drawing made in code, so a picture is never blank. Also the street map card drawn in code, and the UI pieces `Polaroid`, `MapCard` and `Thumb`, shared with the phone.
- ?? `Assets/Editor/NotePhotoSteps.cs` (new photo step): photographs the 13 subjects from the game's own models in `BenchScale.Studio`. Writes PNGs + index.json, a report and a contact sheet.
- ?? `Assets/Scripts/Diagnostics/NotebookPageCheck.cs` (new PlayLab, `[Notebook page]`): drives the real page, takes 7 photos and reads each photo's word check.
- M `Assets/Editor/NotebookPlayCheck.cs`: starts NotebookPageCheck after its own checks (lab session only). Header updated.
- M `Assets/Editor/NotebookRuleChecks.cs`: adds `CheckPictures()` (editor only, 257 assertions) to Run. Two old messages reworded: NotebookRecap.Page is "the page of text" now.
- `python3 /root/tools/csparse.py`: all 7 files "clean".
- A scratch harness compiled copies of the pure files with Mono mcs: 272/272 PASS (the mapping of all 39 facts, line budgets, order, old-save cases, short forms and the CheckPictures count). At /tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/harness/, not a deliverable.

## 2. Decisions and reasoning
- **One entry per subject, not per person.** Barnaby gets his own photo; facts about Barnaby sit under Barnaby, not under Grace. The phone stays person by person (J's file).
- **Tonight's state** is read exactly as `NightCycle.NowLine` reads it, in the same order, with the same names: the bag to the back door, the bag to the bins, his scene (nothing is "tonight's" then), in hand, on the shelf, to take, and "Call it a night". Titles use the Now line's rule: "Barnaby" once known, otherwise "The garden gnome". The place's title is the address, "12 West Street". Lines: "Take the rubbish out the back door.", "Put the bag in the bins.", "Bring it to the man at the bins.", "On your shelf, behind the counter.", "Call it a night at a café door.", "Grace's gnome. Bring it to him."
- **A fact's line** uses its own words if they fit 8 words (counted by WordBudget; tags and icons don't count). Otherwise a short form keyed by the fact's exact text. Otherwise the subject's caption. "(hunch)"/"(likely)" stays, in grey, inside the 8. The whole fact is always behind the press. A fact he rewrites drops its short form, and the rules print a warning.
- **The man at the bins** is the first person once his pages are in; his pages are behind his entry, and also behind what each page is about. Before the deal he is not on the page. His pages get a square bullet instead of italics (italics are harder for a dyslexic reader).
- **Sizes:** every word on the page is 26 px or more (page title 32, names 28 bold, lines, headings, footer and notes 26), because ReviewScreen flags anything under 26 as S2 and the spec requires no S1/S2 on the page. The canvas is scaled by height only (`matchWidthOrHeight = 1`). Contrast about 7:1 or better. With Baloo 2 (line height 1.6 em): 4 entries fit on the first screen, 5 at most; the check caps it at 6.
- **The press:** Tab, or D-pad down on a pad, opens the next entry's notes; the last press closes them all. D-pad up/N already open and close the page, and the page is non-modal. Both buttons are ignored while `Barks.Choosing`. The footer shows `{ControlHints.NotebookPage} Close  {MoreKey} More`, icons only.
- **Map card:** drawn in code like the HUD's sun dial (signed distances, 2×2 samples), not rendered from above (from overhead the district is roofs and trees; at 70 px a render is a blur). 4 streets, the place's street lit (amber when tonight's), the café in its green, one pin, no words. It turns with the overhead camera (`CafeViewMode.OverheadAngle.x`, 15° steps). A file `map_<key>.png` replaces it. Places only.
- **People:** the game has no face cards (every profile's portrait is `{fileID: 0}`). Each person is posed on the café people's rig in their stand-in look, the way AceSidekickSteps photographs a café person: Grace = Character_BusinessWoman, the Officer = Character_Male_Police, the man = NightZeroSet.look. If a person can't be posed, the page draws a head and shoulders in their colour, and the report says so.
- **Protecting drawings:** the step stores each PNG's fingerprint in index.json and never overwrites a PNG it didn't make.
- **NoteSubjects.cs is a new file** to keep the rules pure, so the edit-mode rules check them and the phone can call `KeyOf`.

## 3. Decisions his to overrule
1. Lines at 26 px, not the spec's 22–24 (4–5 entries per screen; lines often wrap to 2 rows).
2. Canvas matched to height (it was 0.5).
3. Entries by subject; headings "Tonight" / "People" / "What you've learned".
4. The press = Tab / D-pad down, walking entry by entry.
5. All the words written: the 14 captions, the 23 short forms, the errand lines.
6. Three subjects beyond his list: "someone", "cafe", "cones".
7. Neighbours = a photo of the first night neighbour's house.
8. Tonight's polaroid has an amber pin and bar; others have tape. Tilt −3..+3°, never flat, stable. Polaroid 100 px, map 70 px.
9. Nothing is "tonight's" while his scene plays.
10. "+N more on the phone" when an open entry overflows.
11. His pages: square bullet instead of italics, on the page only.
12. Map drawn, not rendered; turns with the camera.
13. The page check runs from the existing "Night notebook (Play Mode, lab session)" menu.

## 4. Outside my files (exact edits)
**A. RecapPhone.cs, Notes app (J's file; apply after J).** Line numbers are at 8033112.
1. `PersonRow` (~1626), replace `Avatar(card, CardPadX, 12f, 36f, person.name);` with:
```csharp
string pictureKey = NoteSubjects.KeyOfPerson(person.who);   // Grace, the Officer, "His pages" -> the man at the bins
if (pictureKey != NoteSubjects.Someone) NotePhotos.Thumb(card, pictureKey, CardPadX, 12f, 36f);
else Avatar(card, CardPadX, 12f, 36f, person.name);         // other regulars keep their lettered disc
```
2. `PersonCard` (~1655): the same, with `14f, 40f` in place of `12f, 36f`.
3. `PersonCard`'s facts: set `const float factPicture = 28f;` and `float textX = CardPadX + factPicture + 10f` (it was `CardPadX + 16f`). Replace the `Picture(card, circle, …)` line with:
```csharp
string about = NoteSubjects.KeyOf(fact);
if (about != pictureKey && about != NoteSubjects.Someone) NotePhotos.Thumb(card, about, CardPadX, cy, factPicture);
else Picture(card, circle, Accent, CardPadX + (factPicture - 7f) / 2f, cy + 7.5f, 7f, 7f);
```
   Replace `cy = Bottom(line.rectTransform) + 10f;` with `cy = Mathf.Max(Bottom(line.rectTransform), cy + factPicture) + 10f;`.
4. Optional, Tonight's `NotebookLine` (~1088): replace the purple note square with `NotePhotos.Thumb(card, NoteSubjects.KeyOf(fact), CardPadX, 11f, 32f);`.
5. Map card on the phone: not recommended.

**B. NotebookRecap.cs, comments only.** Lines 17–18, line 22, line 82 (new wording given in full in the builder's message: the night's page is laid out by subject since 23 Oct 2026; Page is the notebook as one page of text, kept for the checks).

**C. Diagnostics/NightZeroCheck.cs, lines 301–303** (they still pass but test NotebookRecap.Page, which the night page no longer shows). Replace with:
```csharp
        // The night's page (pictures first since 23 Oct 2026): Barnaby on top, the man at the bins first of the people, his pages behind him.
        var onPage = NoteSubjects.Entries(saves.Notebook, ledger, NoteStage.ToTake, LodgerStory.Errand(ledger));
        NoteEntry him = onPage.FirstOrDefault(e => e.key == NoteSubjects.Lodger);
        Check(onPage.Count > 1 && onPage[0].key == NoteSubjects.Barnaby && onPage[0].now && him != null && him.group == NoteGroup.People
              && him.facts.Count(f => f.source == Notebook.Sources.Inherited) == pages.Count,
            $"the notebook's page: Barnaby on top, then the man at the bins with his {pages.Count} pages behind him");
```
(`System.Linq` is already imported; `ledger`, `pages` and `saves` are in scope.)

**D. On the Mac:** run the photo step, then commit `Assets/Data/Resources/UI/NotePhotos/*.png`, `index.json` and all new `.meta` files.

## 5. Labs and menus, and what a pass looks like
1. **Photo step** (Edit Mode, Ace's Cafe layout scene open): `Fixit Fidget/Night/Notebook pictures 1 - Photograph the notebook's subjects (Edit Mode)`. Console: `[Notebook pictures] 13 photographed, 0 kept (not this step's), 0 couldn't. Report: …/Logs/Night/notebook-pictures-<time>/report.txt  Contact sheet: …/contact-sheet.png`. Likely and acceptable: `COULDN'T cones …`. Look at the contact sheet (amber edge = drawn in code; green edge = drawn by hand).
2. **Rules** (Edit Mode): `Fixit Fidget/Checks/Night notebook rules` → `[Notebook rules] PASS: 323 assertions.` (66 + 257).
3. **Play check** (Play, lab session via Café life › Lab): `Fixit Fidget/Checks/Night notebook (Play Mode, lab session)` → `[Notebook check] PASS (n/n)`, then `[Notebook page] All 76 checks passed. …/Logs/Night/notebook-page-<time>` (69 if the word check is off). Photos 01-tonight-the-gnome … 07-scrolled-to-the-last; zero S1/S2 among texts owned by `NightNotebook`.
4. **Re-run** NightZeroCheck (after edit C), BarkCheck (its 10-notebook-open photo), PauseCheck (Esc closes the page first), and RecapPhoneCheck after A.

**Old saves.** SaveData stays version 5, no field added. The subject is worked out every time by `NoteSubjects.KeyOf`: (1) the id equals a night thing's id, or starts with it plus "."; (2) one of his 4 page ids; (3) `lodger.page.grace.*` → grace; (4) who == "lodger.pages" → lodger; (5) id == who + ".home" → that person's house; (6) otherwise by who.

**Every fact the game can write → subject** (39 in `EveryFact`): grace.camera.strap, grace.husband.strap, grace.reunion.date, grace.behind.camera, grace.reunion.photo (×3) → grace; <regular>.brought.<device> → someone; grace.home → gracehouse; grace.gnome (×2), grace.gnome.taken (×2), grace.gnome.suspects → barnaby; grace.cups (×2), grace.cups.taken (×2), grace.cups.suspects → cups; lodger.page.grace → gracehouse; lodger.page.gnome → barnaby; lodger.page.windows → neighbours; lodger.page.parking → cones; lodger.page.grace.thursdays, lodger.page.grace.photos → grace; lodger.note.grace.gnome / .grace.cups / .street.cones → lodger; officer.description, officer.word.favour, officer.word.grace.gnome, officer.word.grace.cups, officer.word.street.cones, each plus ".flinched" → officer. GraceAtHome and NightCycle write none.

## 6. Risks and APIs
- Photo framing is unverified (people assumed to face +Z; the house framed by its bounds; Barnaby from his forward). Check the contact sheet.
- `PolygonNpcVisual.Configure/ApplyAppearance/Follow` on the rig inside a preview scene in Edit Mode (same calls as AceSidekickSteps, which runs in the open scene). If it fails, the subject is skipped, drawn, and reported.
- `PreviewRenderUtility.ambientColor` and `lights[1]`; `Object.Instantiate(…, new InstantiateParameters{scene, worldSpace})` (same as ReviewModelSheets.Copy).
- TextureImporter settings (CompressedHQ, max 256, mipmaps) and `Sprite.Create` from Resources textures.
- `TMP_Text.GetPreferredValues` row heights depend on the real font; the ≤6 cap is checked live.
- No other night code reads D-pad down or Tab.

## Questions for him
1. Lines at 26 px rather than 22–24 (4–5 entries per screen)? Recommendation: keep 26 for her.
2. The press is D-pad down / Tab, entry by entry. Fine, or a cursor? Recommendation: keep it for playtest 8.
