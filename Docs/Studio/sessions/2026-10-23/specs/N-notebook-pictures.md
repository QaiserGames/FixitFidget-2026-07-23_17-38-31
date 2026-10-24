# Builder N — the night's notebook, with pictures (23 Oct 2026)

Copy: `/root/wt23/N`. Rules: `/root/review23/builders-common.md` (read it first). Photos: `/root/review23/photos/` (the `Night_*` ones; `Recap_*__06-notes-list.jpg` and `07-notes-open.jpg` show the phone's Notes app).

## His note (verbatim)
"So let's add like pictures that make it also easier for players to know what's happening, because whenever I open the notes in nighttime, I it's like just a cluster of words, it's word vomit, and I rather like it being easier to see."

His girlfriend (dyslexic) "felt overall lost most of the time" in playtest 7. Notes the player can't parse fail her first.

## What's there
- `Assets/Scripts/NightNotebook.cs`: the night's notebook page (D-pad up / N). A dark page in the skin's band with cream writing, a list of facts.
- The phone's **Notes** app (in `RecapPhone.cs`, the people and their facts, "Press a name for everything"). It shows the same notebook data by day and at closing. Don't edit `RecapPhone.cs` (builder J owns it); see the end.
- The facts come from the save's notebook (grep `NotebookFact`, `SaveManager` notebook, `NightThings.cs`, `LodgerStory.cs`, `GraceAtHome.cs`, `NightCycle.cs` notes). Each has a who, a line, a day, and sometimes a "sure" or "hunch".
- The night's notes (the toasts that pop while playing) are `NightCycle`'s queue. They're not this task, but the page must agree with them.

## What to build
1. **A picture for every subject.** Each person, place and thing the notebook can mention gets a small picture:
   - people: Grace, the man at the bins, the Officer, the neighbours;
   - places: Grace's house at 12 West Street (the yellow house), the back door, the alley, his corner;
   - things: Barnaby the gnome, Grace's cups, the crate.

   **Render them from the game's own models**, polaroid-style, by an editor step that photographs each subject in a small preview studio and saves PNGs under `Assets/Data/Resources/UI/NotePhotos/` (with a `.json` index mapping subject keys to files). That way:
   - every picture matches the game exactly;
   - a new subject gets its picture by adding one line to the step's list;
   - his girlfriend can later replace any PNG with a drawing of the same name.

   The project already photographs models in preview scenes: `BenchScale.Studio` (used by `ReviewModelSheets.cs`, `HeroDeviceSteps`, the bench steps) and the house close-up tools. Reuse that; don't invent a new renderer. People are characters (Synty Sidekick and POLYGON prefabs in the scene). If a character can't be posed in a studio cleanly, use their face card if the game has one (grep "portrait", "FaceCard"). Otherwise a simple silhouette icon in their colour, and say so.
2. **The page becomes pictures first, few words.** Each entry is:
   - a polaroid (about 96–120 px, slightly rotated, tape or pin);
   - a bold short title (the subject: "Barnaby", "12 West Street");
   - one line of at most 8 words (what matters now: "Grace's gnome. Bring it to him.").

   The fuller text sits behind a press, like the phone's "Press a name for everything".
   - Group by tonight's errand first: what you're doing now on top, highlighted with the amber "next" (#FFB238, the bench's colour). Then the people. Then what you've learned.
   - Places get a tiny street map card: a simple drawn street (West Street, Back Street, the café) with a pin. Draw it in code like the HUD's sun dial, or render it from above with the studio. Say which and why.
   - Readable: titles 26–28 px bold, lines 22–24 px, contrast ≥ 4.5:1, no more than 5–6 entries on the first screen; the rest scrolls.
3. **The data.** Map each fact to its subject key without breaking saves: derive the key from the fact's who or what. Add a field only if it serializes safely and old saves load (Bible §2: saves; read the save's migration code before touching it).

## Labs
- The notebook's lab is "Fixit Fidget › Checks › Night notebook (Play Mode, lab session)" plus the edit-mode "Notebook rules" (66 assertions). Grep their files; they're yours to update.
- Add checks:
  - every fact the night can add has a picture (or a declared fallback);
  - the first screen holds at most 6 entries;
  - no line over 8 words on the page (`WordBudget`);
  - the current errand is on top.
- The labs' photos (the word check runs on them) must show no S1 or S2 text findings on the page.
- The editor step must report every subject photographed and any it couldn't.

## Files you own
`Assets/Scripts/NightNotebook.cs`, new files for the photo step (Assets/Editor) and a small runtime loader (e.g. `NotePhotos.cs`), the notebook's labs and checks.

**The phone's Notes app:** describe exactly how it should show the same pictures (the call into your loader and the layout), so the integrator can add it after builder J. Don't edit `RecapPhone.cs`.
