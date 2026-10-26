# Builder I: icons on notes (26 Oct 2026)

Built placement A, keeping the eyes on the bins icon. Branch `work-I`, commit `1218b30`.

## Files
- `Tools/UI/draw_button_icons.py`: the 10 pictures from `note_icons.py` as drawn (no torch), on the paper-and-gold chip, added last as `note_*`.
- `Assets/Data/Resources/UI/ButtonIcons/button_icons.png/.json`: regenerated, now 109 icons.
- `Assets/Scripts/NoteIcons.cs` (new): the rule. `Chip()` drops the chip if it would add a line.
- `NightCycle.cs`: `NowIcon`, set alongside each Now-line state. The words don't change.
- `DayOneGuideUI.cs`: the chip goes before the step. New `NowChip`; `NowText` is still just the words. The guide pin gets the line's chip.
- `UiPins.cs`: `ShowGuide(..., icon)`, `GuideIcon`; the plate fits the chip.
- `Barks.cs`: his ask gets its chip, chosen by line id. New `Shown.icon`; `Shown.text` is still just the words.
- `LodgerDay.cs`: MessNote gets the table chip.
- `Assets/Editor/NoteIconChecks.cs` (new): an edit-mode check.

csparse: clean (11 files).

## Chips
- **His ask:** gnome (`lodger.night0.09`), cups (`lodger.night2.01`, plain and warm), cones (`lodger.night3.02`). His other lines get none.
- **Night Now line:**
  - door: "Take the rubbish out the back door" and "Call it a night";
  - bins: "Put the bag in the bins" and "Bring X to the man at the bins";
  - the thing itself (gnome, cups or cones): "X: where…" and "X is on your shelf".
- **Day lines:**
  - back shelf: every "…Fetch more from the back shelf." (the refill and both stock lines);
  - machine: carrying the crate to the drinks machine;
  - table: every "Clear…" table line, including when "Nina's on the way." comes first;
  - thrift: the thrift find.
- **MessNote:** the table chip.
- **Guide pin (day):** the same chip as the line.
- **Never:** lines with a button icon, story, captions, TableNote, lessons, the notebook, bench card, morning card, phone. HudCorners, RecapPhone and the save code are untouched.
- **Size:** the chip is 208 px on the sheet: about 42 px beside 26 px words, 48 px beside 30 px speech. No editor-step code changed.

## Mac steps, in order
1. **Fixit Fidget › UI › Button icons - Make…** (after the new PNG/JSON arrive). Pass: "Rebuilt … 109 icons".
2. **Fixit Fidget › Checks › Note icons (edit mode)**. Pass: `[Note icons] All N checks passed.` It checks:
   - all 10 chips are on the sheet and in TMP's sprite asset;
   - every marked line gets its chip and no unmarked line gets one;
   - only the 3 asks among BarkSteps' lines get one;
   - a chip never counts as a word, and there is one icon per line.
3. **Night 0 lab**: 5 Now-line states, each with its chip first. The word budgets are unchanged.
4. **Night 2 lab**: the ask gets the cups chip and his other lines get none; the cups and bins Now-line states; MessNote starts with the table chip.
5. **Waiting jobs lab**: the table line and its pin get the table chip.
6. **Day 1 guide lab**: the tray step (which shows a button icon) gets no chip.

Each lab passes with its usual "All N checks passed."

## Decisions his to overrule
- The night line keeps its chip while the " · [N] notes" nudge shows. Strictly, that is two icons in one line. I kept it because the A crops he approved show it, and without it Night 0's errand has no chip.
- The pin chip shows by day only, because nights have no guide pin.
- `note_house` is on the sheet but no line uses it.

## Risks
- The chip makes its line taller. The Now card and pin measure it; the speech plate's cap gets +0.6 em. See Night 2 photos 03/04.
- If a chip is dropped to avoid a wrap, the labs report a FAIL. That is on purpose.
- APIs I can't confirm compile here: `GetPreferredValues(string)`, and casting BarkSteps' private `Lines` (read by reflection) to `ValueTuple<string,string,string,string>[]`.
- Day lines are matched on their endings; the edit-mode check catches rewrites.
