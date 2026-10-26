# Builder I2: icons on the notebook, the furniture and the tickets' items (26 Oct 2026)

Branch `work-I2`, commit `18e8252`. 23 icons added to `Tools/UI/draw_button_icons.py` after the note chips (the older 109 keep their rects). csparse clean on all 9 C# files.

## Where icons show now, and why

**The notebook page** (`NightNotebook.cs`; the rule in `NoteIcons.cs`):
- **Tonight's first entry** wears the Now line's chip before its line, from the HUD's own table (`NoteIcons.ForStage`), so the HUD line, the pin and the page match by picture. The second tonight entry (where it is) has its polaroid and map: no chip.
- **Each fact behind the press starts with its kind's chip** in place of the bullet: pin (where someone is), case (what they own), clock (when), bubble (what was said), two people (relationship), padlock (Ace's secret). His pages wear the same mark on pinkish paper with his dark-red rim. She can scan an entry for "where" and "when" without reading.
- Nothing else on the page; without the sheet the bullets come back.

**The furniture** (`ArrangeUI.cs`, `RecapPhone.BuildYourCafe`):
- Every Arrange card wears its kind's picture (60 px) over its name; the card grows from 120 to 180 px so "Banquette bay" still wraps at 24 px.
- The YOUR CAFÉ card shows the seven in a row (44 px), tonight's at full and later days' dimmed as the strip dims them: what's behind "Arrange the café", with no words added.

**The tickets:** nothing built; the pictures wait on the sheet.

## Names (stable)
- Furniture, by asset name (`NoteIcons.Furniture`): `furn_bistro`, `furn_square`, `furn_plant`, `furn_banquette`, `furn_counter`, `furn_lounge`, `furn_communal`.
- Fact kinds (`NoteIcons.ForFact`): `kind_where`, `kind_owns`, `kind_when`, `kind_said`, `kind_who`, `kind_secret`, each also with `_his`.
- **Ticket items for the HUD builder** (`NoteIcons.ForDevice`, `JobTicket.DrinkIconName`): drinks `drink_latte`, `drink_coffee`, `drink_tea`, `drink_espresso`, `drink_hotchocolate`, `drink_americano` (existed); devices `item_phone` (Phone), `item_watch` (Pocket Watch, thrift watch), `item_camera` (reunion film camera); `item_clock`. Plain pictures like the drinks, readable at 40 px. A ringing call keeps `SupportCallIcon`.

## Mac steps, in order
1. **Fixit Fidget › UI › Button icons - Make…** once the PNG/JSON land. Pass: "Rebuilt … 132 icons".
2. **Checks › Note icons (edit mode)**: the 23 on the sheet and in TMP's asset; every fact the game can write has a chip; `ForStage` follows the Now line's table; every kind, device and drink has a picture.
3. **Checks › Night notebook (Play Mode, lab session)**: tonight's chip (gnome; bins in hand; cups; door for the bag), none on the second entry; every note starts with its kind's chip, the words unchanged; photo `03b`.
4. **Night 0 lab**: with Barnaby in hand the real page opens, its chip equals `NightCycle.NowIcon`, photo `09b`. **Night 2 lab (Cups)**: the same for the cups, photo `04c`.
5. **Furniture lab**: the YOUR CAFÉ row lists all seven (lounge and communal "Day 3", dimmed); every strip card wears its picture on the 180 px card (photos `01`, `02`).

A pass is each lab's usual "All N checks passed." Without the sheet, the new checks note it and pass on words.

## Decisions his to overrule
- Tonight's chip shows even when the polaroid is the same thing (the gnome): it is the job's badge.
- Kind chips replace the bullets; his pages are told apart by paper and rim, not a second mark.
- Strip cards 180 px tall; the YOUR CAFÉ row shows later days dimmed, not hidden.

## Risks
- `SpriteOf` reads `TMP_SpriteCharacter.glyph` as `UnityEngine.TextCore.Glyph` and `spriteSheet` as `Texture2D`: checked by reading, not compiling.
- Sprites cut from the sheet have no mipmaps (like the TMP icons): thin lines may shimmer a little at 44 px.
