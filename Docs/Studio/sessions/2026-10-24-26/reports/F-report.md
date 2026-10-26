# F2 report: the café that grows (buy and place furniture), 24-26 Oct 2026

Builder F2, copy `/root/wt24/F` (branch `work-F`), nothing committed: every change is in the working tree. Designer F's spec
(`Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md`) built in its merge order, Part 1 -> 4 -> 2 -> 3 -> 5, with his answers
of 24 Oct applied (f1-f5, r1-r3, c12, a80, a56, the intro line). `csparse` is clean on all 28 new and changed files. Progress log:
`/root/review24/reports/F-progress.md`. The rules' Python twin (scratchpad `f5/*.py` over the reference scene) gave every expected
number below.

## 1. What I did (files, one line each)

New, runtime (`Assets/Scripts`):
- `FurnitureDefinition.cs`: a kind of piece (asset name = save identity; price, seats, day on sale, wall piece, zone, prefab or plant variants; `SellBack` = half).
- `FurnitureCatalogue.cs`: the seven kinds in the strip's order (`Find` by asset name), the ghost's material.
- `PlacedPiece.cs`: one piece (id `scene:<root>` or `p<n>`, kind, tier, variant, pose, on/off, seats, solids, carve obstacles, caps; its shape in its own frame with tight boxes for round pots; `ToWorld`, `BoxIn`).
- `FurnitureRules.cs`: the rules R1-R7 with their plate words (each 7 words or fewer), both guards' replicas (every place reached from the door; every place served with all taken), `BuildMap` (physics, once) / `MapFromData`, `CheckLocal`, `FoldsSheet`; lying dust sheets' boxes are solids for the guards unless the piece folds that sheet.
- `CafeLayout.cs`: the layout at run time (`Apply`: new game half-finished / old save today's café / saved layout; `Snapshot`; `Buy`, `Move`, `Sell`, `PickUp`, `PutBack`, `AfterArranging`; the sheets; saved at once after every change); adds `ArrangeMode` beside it.
- `DustSheet.cs`: a canvas sheet and its box stack; lies or folds away ("cloth.lift").
- `BanquetteRun.cs`: bays on a wall join into a run; each cap shows only where no bay joins it.
- `ArrangeMode.cs`: Arrange the café (closed only): the eased overhead at 26 m, cursor (mouse, left stick, D-pad/WASD one cell), ghost, place / turn / hold-to-sell / back, the strip, the starting spot (on a sheet first, nearest the counter, one deterministic order).
- `ArrangeUI.cs`: the strip's cards, the Now card, the till, the controls strip, the plate with the hold ring, dust puffs (UiSkin, UiTween, unscaled time).
- `ArrangeGhost.cs`: the green/red ghost copy, footprint, stand rings, in-the-way box, hover and home outlines, the 25 cm grid.
- `Diagnostics/FurnitureCheck.cs`: lab `[Furniture]`, place, move and sell (§5.2), and `AtClosing` for the Days labs (§5.5).
- `Diagnostics/FurnitureSaveCheck.cs`: lab `[Furniture save]`, save and load, an old save, a new game, an unknown kind, the morning's save (§5.3, §5.4).

New, editor (`Assets/Editor`):
- `FurnitureSteps.cs`: Furniture 0 (read-only check), 1 (the café's own 17 pieces become placeable; a56 backs; c12 stools; the kinds and catalogue), 2 (the prefabs, a80 grain), 3 (the dust sheets), 1 Undo, 3 Undo.
- `FurnitureRulesCheck.cs`: Fixit Fidget > Checks > Furniture rules, Edit Mode, 12 checks on the café as data.
- `FurnitureLabMenus.cs`: Furniture > Lab - Place, move and sell / Lab - Save and load; Days > Lab - Day 1/2/3 at the counter (half-finished café).

Changed, mine (marked `// 24 Oct 2026 (Furniture)`):
- `GameBootstrap.cs`: one line, `CafeLayout.Instance.Apply(data, SaveManager.Instance.HasSave)` before the clock is restored.
- `CafeViewMode.cs`: `OverlayOwnsInput` is false while `ArrangeMode.Active`; V (first person) ignored while arranging.
- `RecapUI.cs`: `Showing` includes arranging; `HideForArrange()` (the phone put away) and `ReturnFromArrange()` (the phone back on the Shop).
- `DayIntro.cs`: Day 1's line is "Years at a coffee cart bought this place. Now make it a café." (61 characters; the old line in a comment).

Changed, shared (marked; merge notes in §4): `SaveData.cs`, `SaveManager.cs`, `PatronSpawner.cs`, `RecapPhone.cs`, `ControlHints.cs`,
`Diagnostics/CafeLab.cs`, `Diagnostics/DayShapeCheck.cs`; comments and one report line only: `Editor/NpcSitLiveCheck.cs` (Sit 4),
`Editor/NpcWave2Steps.cs` (Life 4).

Where each piece lives after Furniture 1-3 (names kept):
- `31 - furniture/The café's own pieces/<root>`: "Bistro table, south", "Bistro table, north", "Banquette bay, south / middle / north", "Communal table, west of the aisle", "Window counter, east wall", "Lounge corner, the reading corner", "Lounge corner, by the counter", and eight plants ("Plant, fig in the front-left corner", "Plant, monstera at the counter's west end", "Plant, olive by the door (east)", "Plant, olive by the door (west)", "Plant, rubber plant between the bistros", "Plant, monstera by the lounge", "Plant, rubber plant front right", "Plant, rubber plant front left"). Each set's group moved under its root whole: "Bistro 2-top (south)", "Reading corner", "Cafe dressing 3 - lounge" keep their names, so seats keep their parents' names (NpcBeatChecks) and the settled guests' references.
- `31 - furniture/Not furniture (fixed)`: the lounge's floor lamp (with its own Not Walkable modifier).
- `31 - furniture/Bought pieces`: where bought pieces go at run time (empty in the scene).
- `31 - furniture/Dust sheets`: the five sheets, `sheet-1` .. `sheet-5`, switched off in the scene.
- `Assets/Data/Furniture/`: the seven kinds (`Bistro table`, `Square table`, `Plant`, `Banquette bay`, `Window counter`, `Lounge corner`, `Communal table`: SAVE IDENTITY, never rename), `Furniture catalogue.asset`, `Furniture ghost.mat`, the sheets' canvas, weave and cloth meshes. `Assets/Prefabs/Furniture/`: the ten prefabs.
- The art findings left to the Unity art builder: a51 the lounge's rug is "LI_Rug_Corner (lounge)" under "Lounge corner, by the counter" (and in `Lounge corner.prefab`); a48 the napkin boxes are "LI_NapkinDispenser (...)" under their tables' pieces (and in the bistro, square, window counter, bay and communal prefabs); a43 the repair bar's stools are not furniture and stay in Seats 1's "Repair bar stools"; a58, a59, a61 (the patio table, the door planters' boulders, the patio chairs) are outside and untouched.

## 2. Decisions and reasoning

The runtime and the save (Part 1)
- **Format.** `SaveData.cafe` (`CafeLayoutData`: format, the pieces that are on, the sheets still lying, the next id), additive with no version bump, the house way. Format 0 (absent) = a save from before 24 Oct (his own `playtest-aces-cafe.json` included) = today's café, 28 seats, nothing lost. A new game (no save file: `SaveManager.HasSave` false) opens half-finished. Format 1 = a saved layout. An unknown kind is skipped with one warning; a piece listed twice loads once.
- **The NavMesh** (I checked the scene): one NavMeshSurface on "Floor" (collect All, physics colliders, 0.08 voxels) that already ignores NavMeshObstacles in the bake. So Furniture 1 gives each piece's root a NavMeshModifier Ignore From Build, re-bakes the routes without the pieces, and each solid gets a carving obstacle child: pieces carve their own holes at run time (§4.3: carving, not a runtime re-bake). The lounge group's own Not Walkable modifier is switched off (the nearest modifier wins, so it would have overridden the root's).
- **The rules run on a floor map built once per closing** (physics read once), and the starting-spot search asks the quick rules first and the guards only for the few that pass (at most 60).
- **Settled guests** (PatronSpawner): a seat the layout switched off is no fault (no warning); the guest takes a free seat of the same kind on the furniture that's on, by S's tags (the reader a soft seat, the pair a two-seat piece that isn't a soft corner, the laptop worker a communal seat), never a repair-bar stool, and only under the valve. On the half-finished Day 1 that seats the reader (the lounge's tub chairs) and the pair (the north bistro); the laptop worker has no communal seat and stays away: two guests, as his r3 says. P2 owns the numbers.

The conversion (Part 4)
- **17 pieces, not 16**: the reading corner and the lounge are both pieces; the eight floor plants are pieces.
- **The lounge's floor lamp is fixed, not furniture**: it stands in Ace's way out from behind the counter (rule R1's apron), so as part of the lounge corner the lounge could never be placed back where it stands today. The reading corner's lamp goes with its corner.
- **a56**: the bay's back closed with a thin dark box (`CC_Dark_Kick`, 3 mm off the wall, between the base and the top roll), the brief's Unity option.
- **c12**: the window counter's three stools sit, as NPC wave 2 made the repair bar's: Snap To Seat on, Max Lift 0.32, Approach Side 0.60 (half their 1.2 m spacing: the way in passes between two stools, as the repair bar's 0.50 at 1.0 m), no turn (the counter's brackets leave knee space). Each old value is noted for the undo.
- **a80**: the prefabs are copies of the café's own dressed pieces, so they wear the same grain; any flat `CC_Wood_*` or `DC_Fabric` left is swapped for the café palette's grain (as `CafeFurnishing.Remap` does) and listed. The square table (which the café doesn't have) is the south banquette 2-top's table with two café chairs facing.
- **Dust sheets made in Unity**, not Blender: a procedural cloth mesh per sheet (soft folds, draped edges), a woven canvas texture (#CEC3AE), and a stack of Grace's taped boxes (`GH_BoxStack`, its own box collider and carving box) on each. They look like decorators' drop cloths with boxes waiting on them.
- **r1, the half-finished café**: open, the repair bar's 3, the north bistro 2, the window counter 3, the lounge by the counter 2 = 10 seats; under five sheets, the three bays, the communal table, the south bistro and the reading corner. The plants: six of eight stay (both olives, the fig, both monsteras, the front-right rubber plant).

Arrange mode and the Shop (Parts 2, 3)
- Arrange opens only at a closing (f1), from the Shop's YOUR CAFÉ card. The world holds still (time scale 0); Arrange runs in real time. The camera eases to the overhead's home angle at 26 m; first person is put away and given back. V is ignored while arranging.
- Controls (icons from `ControlHints`, never "[E]"): ✕/click place or pick up, △/R turn a quarter (a new plant: the next plant), hold □/F 0.6 s to sell (let go early and nothing happens), L1 R1 / Q E along the strip, ○/Esc/right-click back (with nothing in hand: leave).
- The first ghost waits on a dust sheet if any lies (so the first table folds one away: §1.2), nearest the counter. The search's order is one fixed order (the score in thousandths, then x, z and the turn), the one the twin used: Day 1's first ghost is a bistro at (-4.75, 10.25) turned, on the west banquette's north sheet. The mouse takes the cursor only once it moves, so the first ghost isn't thrown to wherever the mouse happens to point.
- A sheet folds when a piece's footprint is on it, or one of its people's stand points is within 0.45 m of it; until then its boxes block the guards.
- The YOUR CAFÉ card is the Shop's first card (the furniture is the Shop's headline); Close up's "up" still lands on the stock's last button. Day 1's "!" badge says a first table waits.
- The card follows the phone's neighbouring cards' type (18 and 20 in the phone's units; bold name, a full-width green button); the lab photos measure their words.

The labs (Part 5)
- **Positions the spec's labs named don't fit his r1 café or the 25 cm grid**; each lab uses the twin's nearest equivalent and says so in its header: the front-window bistro at (2.75, 1.75) (the spec's (3.55, 0.80)); the moved bistro at (-2.75, 2.0) (the spec's (-3.70, 1.05) is in the reading corner); the aisle test at (1.0, 9.5) (0.9 is off the grid); the cram is six bistros, not five (the lamp fixed frees a corner); the half-finished bistros at (-3.0, 11.0) and turned at (-2.5, 10.25) (the spec's 3-seat café's spots now hold the lounge and the window counter).
- **The save lab sells the lounge by the counter and the communal table first**: in today's unchanged café no table turned a quarter fits anywhere (the twin), and §5.3 turns one.
- **"Reload the scene" is `CafeLayout.Apply` from the file on disk**, the same call GameBootstrap makes at a load; no lab reloads the scene mid-Play in this project, and doing it would test every other system's statics, not the layout.
- **A new game in the save lab is `Apply` with no save**; a lab session with no save file at all would be a real first run (the intro, the tutorial). The spec's "no guest comes on Day 1" is gone with r3 (P2's numbers).
- **The place lab's crammed-café frame test runs at that closing, before the night** (the café is empty, so loading the crammed layout can't pull a seat from under anyone); then the layout before it is loaded back, the night skipped, and tomorrow's paths and placed-seat use checked.
- **The Days labs on the half-finished café are their own menus** (`FurnitureLabMenus`) and their runs go to their own file (`Logs/CafeLife/HalfFinished/days-1-3-runs.csv`), so their medians never mix with today's 28-seat café's. Day 2 and 3 saves carry the week's model (§3.3: a bistro bought at Day 1's closing, a square table at Day 2's), each where the ghost first waits that evening: (-4.75, 10.25) turned (sheet 2 folds: 12 seats), then (-2.25, 9.0) turned (sheet 3 folds: 14 seats); money as `Playtest4SystemsSteps.StartDaysLab` gives (P2's $369 / $788) less what the furniture cost ($289 / $618).
- **The Edit Mode rules check runs on data pasted from the twin** (the reference scene's 49 fixed solids, waiting spots, queue, stations, stools, the 17 pieces' shapes and the sheets), so it proves the C# rules equal the twin's without a scene; the scene itself is proved by Furniture 1's own checks and the Play labs.

## 3. Decisions his to overrule

1. The reading corner is under a sheet on Day 1 (sheet 4). P's r1 lists the bays, the communal table and the second bistro (16 seats); I add the reading corner's 2, so 18 seats wait, keeping his 10 open.
2. The open bistro on Day 1 is the north one (beside the lounge and the window counter); the south one is under sheet 5.
3. Six plants stay on Day 1: both olives, the fig, both monsteras, the front-right rubber plant (the other two rubber plants come back as bought plants).
4. The lounge's floor lamp is part of the room, not of the lounge corner; the bought Lounge corner has no lamp (a light that moves needs the café's lighting to register it: later).
5. The strip's order: by the day on sale, then price: Bistro $80, Square $90, Plant $35 (Day 1); Banquette bay $140, Window counter $150 (Day 2); Lounge corner $160, Communal $360 (Day 3).
6. Bought seats are named "Seat (bought <kind>, <seat>)"; the bays' zone word is "banquette".
7. The square table is the south banquette 2-top's table with two café chairs facing, their stand points straight behind the chairs.
8. The first ghost waits on a sheet first, nearest the counter.
9. A sheet folds when a piece's footprint or one of its stand points (0.45 m) is on it; a lying sheet's boxes block the guards until then.
10. V is blocked while arranging; the camera eases to the home angle at 26 m and back.
11. `CafeLayout` adds `ArrangeMode` at Awake (no scene step for it).
12. Hold □/F 0.6 s to sell; ○ with nothing in hand leaves.
13. c12's numbers: Max Lift 0.32, Approach Side 0.60, no turn.
14. a56: a thin dark box, not a modelled panel.
15. The dust sheets are Unity procedural cloths with Grace's box stacks, not Blender models.
16. The YOUR CAFÉ card first in the Shop, a full-width button "Arrange the café".
17. The mouse doesn't move the cursor until it moves.
18. The labs' adapted positions, the save lab's two sales first, `Apply` instead of a scene reload, the cram at closing (all in §2).
19. The Days labs on the half-finished café as separate menus with their own runs file; Day 2 and 3's layouts and money as in §2. **10 seats in 4 zones is exactly S's line** (`RoomAtLeast` 10, `RoomZonesAtLeast` 4), so on the half-finished Day 1 the seat medians are judged, not reported, if the open seats make 4 zones. If he'd rather they be reported until the café grows, `RoomAtLeast` 11 (S's and P2's call).
20. Every other lab that starts from a fresh Day 1 save (`new SaveData()`, format 0) still runs on today's 28-seat café; only a real new game and the half-finished menus open half-finished (see §4, Playtest4SystemsSteps).

## 4. Outside my files: what must change, and merge notes

Shared files (every edit additive and marked `// 24 Oct 2026 (Furniture)`; merge both sides by hunk):
- **`SaveData.cs`**: (1) two new classes, `CafeLayoutData` and `PlacedPieceData`, between `RecapSaveData` and "// The complete contents of a save file"; (2) the field `public CafeLayoutData cafe = new CafeLayoutData();` after `public NightSaveData night = ...;`, before `ValidateAndMigrate`; (3) in `ValidateAndMigrate`, null guards for `cafe` after the night questions' guard (format >= 0, pieces non-null with ids, kind/variant non-null, tier >= 1, sheets non-null, nextId >= 1). P2's thrift find is another additive field: keep both. No version bump.
- **`SaveManager.cs`**: one statement in `CaptureState` after `data.regularMemories = SnapshotRegularMemory();`: `data.cafe = CafeLayout.Instance != null ? CafeLayout.Instance.Snapshot() : Loaded != null && Loaded.cafe != null ? Loaded.cafe : new CafeLayoutData();` (a scene without a layout carries the loaded one forward, so a save made there never drops the café's).
- **`PatronSpawner.cs`**: in the opening guests' loop, after `TableSeat[] seats = SeatsFor(guest);`: `bool switchedOff = SeatsSwitchedOff(guest); if (seats == null && switchedOff) seats = StandInSeats(guest, today, people);` and the warning only `if (!switchedOff)`; then a new block after that method: `SeatsSwitchedOff`, `AllOn`, `StandInSeats`, `WhatItIs`. If P2 reshapes the guests' loop, keep the two behaviours: a switched-off seat set is no fault (no warning), and the stand-in seat by S's tags under the valve.
- **`RecapPhone.cs`**: (1) `public bool ShopFirstTable { get; private set; }` after `Current`; (2) in `UpdateBadges`, `ShopFirstTable = FirstTableWaiting(); Badge(App.Shop, ShopAlert || ShopFirstTable, "!");` replacing the Shop's badge line (ShopAlert stays stock-only for the labs); (3) in `BuildShop`, `y = BuildYourCafe(y);` right after the app header; (4) after `BuildShop`'s helpers: `chair` sprite field, `BuildYourCafe`, `OpenArrange`, `FirstTableWaiting`; (5) `ContentButton`'s doc lists "arrange". P2's thrift find card and Tonight line: put the thrift card after YOUR CAFÉ (the spec's §1.2 makes the café the Shop's first card) and keep the stock's last button the last content button (RecapPhoneCheck's pad walk goes up from Close up to the restock).
- **`ControlHints.cs`**: four verbs appended at the end of `Verb` (`Place, Turn, SellHold, Browse`: the existing verbs keep their numbers), four static properties, four cases each in the icon and word switches, four `Words` properties. If P2 appends verbs too, keep both sets after `StopNeedle`.
- **`Diagnostics/CafeLab.cs`**: one static method at the end of `CafeLab`: `HalfFinishedLayout(CafeLayout)` (the new game's open pieces at the scene's poses, every sheet lying, format 1).
- **`Diagnostics/DayShapeCheck.cs`**: (1) a header paragraph "THE HALF-FINISHED CAFÉ" before the class; (2) `public bool halfFinished;` after `dayNumber`; (3) `(halfFinished ? ", on the half-finished café" : "") +` in `Title`; (4) in `StartFromTheEditor`: read and delete `FurnitureCheck.HalfFinishedKey`, set `check.halfFinished`, and the folder gets a `"HalfFinished"` segment when it's set (`Path.Combine(..., "CafeLife", half ? "HalfFinished" : "", ...)`), which gives those runs their own runs file; (5) after `Report(today, day, spawner);` at the end of `Run`: `if (halfFinished) yield return FurnitureCheck.AtClosing(dayNumber, Check, Note, Photo);`. P2's day bars and seat medians are elsewhere in the file: merge by hunk.

Outside the ownership list (made because the brief's c12 bullet asks for these headers; comment and report text only; the integrator may drop them if the NPC labs' owner prefers):
- **`Editor/NpcSitLiveCheck.cs` (Sit 4)**: the header's "The window counter's three still have Snap To Seat off..." now says they sit since Furniture 1 (c12), a customer who takes one is expected to sit, feet on its foot ring; the NOTE line reads "...waited standing beside a stool with Snap To Seat off (none expected: the repair bar's three sit since NPC wave 2, the window counter's three since Furniture 1)...". No check changed: `standsAtSeat` already follows each stool's Snap To Seat.
- **`Editor/NpcWave2Steps.cs` (Life 4)**: a header paragraph: the window counter's stools sit too; Life 4's stools are still the repair bar's; in the half-finished café the settled guests' named seats are under sheets and PatronSpawner seats them by their tags.

Not made, for their owners:
- **`Editor/SeatChoiceRules.cs` (S's, P2's area)**: section 6 models Day 1 with "the window counter's stools waited at standing, as today" (header line 38; its seat list); after Furniture 1 they sit, so its data should mark them `standing: false` and the comment say "they sit since Furniture 1 (c12)". Section 7 reads the scene, so it sees them sitting by itself (the Officer's favourite wakes: "once they sit, he takes one"). The unit checks at lines 200-251 set standing explicitly and stay true. Section 6/7 still model Day 1 on the 28-seat café; on his r1 Day 1 has 10 seats: P2's call whether section 6 should model the half-finished room.
- **`Editor/Playtest4SystemsSteps.cs` (`StartDaysLab`, P2's)**: its Day 1 save is `new SaveData()` (format 0), so the existing Days labs run Day 1 on today's 28-seat café, not the café a new game opens. If he wants the half-finished café to be the only Day 1, set `start.cafe` from `CafeLab.HalfFinishedLayout(FindAnyObjectByType<CafeLayout>())` there (plus the week's pieces for Days 2 and 3, as `FurnitureLabMenus.StartDay` does) and retire my three Days menus. My `DaysSave` copies its Day 2/3 save (Grace's memory, $369 / $788): if P2 changes those, mirror them in `FurnitureLabMenus.DaysSave`.
- **P2's two new props** (the thrift shelf by the bench, the back shelf behind the drinks machine): `FurnitureRules.BuildMap` treats every collider that isn't under a PlacedPiece as fixed, so they're fixed as long as they have colliders. Both sit behind the counter, outside the floor the rules check, so no expected number moves. The Edit Mode rules check's data doesn't include them (it's the 24 Oct reference scene).
- **New scripts' `.meta` files**: Unity makes them on import (none written here).

## 5. On his Mac, in order (what a pass looks like)

Open `Assets/Playtests/AcesCafeLayoutPlaytest.unity` (Seat tags 1 and Art tidy 1 are already in it; Furniture 1 refuses without them).
1. **Fixit Fidget > Cafe redesign > Furniture 0 - Check first (read-only)**: its report lists every set it will move, by path, and the rules' numbers today and half-finished; it ends "CHECKED: ready. Nothing was changed." 
2. **Furniture 1 - The café's own pieces become placeable (scene, with Undo)**: "DONE. Furniture 1 - ..." with every check PASS: 17 pieces; every part and the 25 seats on them where they were (1 mm) with their tags; 28 seats in all; the window counter's stools sit (0.32 / 0.60); no static flags; one carving box per solid; each piece fits against the rest; today's café passes both replicas (38 of 38 reached, 35 of 35 served) and the real guards are not worse; the half-finished café: 10 seats. The report lists every object it moved. Photos in `Logs/CafeRedesign/furniture-1-<time>/`.
3. **Furniture 2 - Build the furniture prefabs (assets)**: "DONE. Furniture 2 - Build the furniture prefabs": ten prefabs in `Assets/Prefabs/Furniture/` (bistro, square, bay, window counter, lounge corner, communal, four plants), each with its source's solids and stand points (1 mm), no `CC_Wood_*` or `DC_Fabric` left, its seat count its kind's; the ghost material.
4. **Furniture 3 - Lay the dust sheets (scene, with Undo)**: "DONE. Furniture 3 - Lay the dust sheets": five sheets, clear of the open pieces, the waiting nook, the aisle, the queue and the door; the half-finished café passes both replicas with the boxes solid (20 of 20, 17 of 17); its photos show the half-finished café.
5. **Save the scene.**
6. **Fixit Fidget > Checks > Furniture rules**: `[Furniture rules] All 12 checks passed.` (17 of 17 own pieces fit; today 28 seats, 38/38 and 35/35; the square at (1.0, 9.5) "Keep the way from the door clear"; half-finished 10 seats, 20/20 and 17/17, bistros at (-3.0, 11.0) and turned at (-2.5, 10.25) fit; bays at z 2.25 and 4.35 fit, 4.34 "Too close to the banquette bay"; the cram 40 seats, 50/50, 47/47, and no seventh bistro anywhere; "Too close to where people wait" at 0.89 m; "You need $60 more" for the communal table with $300; every verdict 7 words or fewer; Day 1's first ghost fits, folding sheet 2.)
7. **Fixit Fidget > Furniture > Lab - Place, move and sell** (about three minutes; hands off): `[Furniture] All 33 checks passed. <folder>`. The till 1,000 -> 920 -> 960 -> 1,000 -> 920; seats 28 -> 30 -> 28 -> 26 -> 28; the cram's worst of 30 frames under 50 ms (his Mac's number is the one that counts: please send it); every path complete from the SpawnPoint on Day 3; somebody sits at the bought bistro after the patrons are sent in. `Logs/Furniture/place-move-sell-<time>/`.
8. **Fixit Fidget > Furniture > Lab - Save and load** (about half a minute; covers §5.3 and §5.4): `[Furniture save] All 20 checks passed. <folder>`. Old save: 28 seats, 17 own pieces, 0 sheets, 38/38 and 35/35; saved as format 1 with 17; the layout: $1,055, 22 seats, 32/32 and 29/29, every piece back within 1 mm and 0.1°; new game: 10 seats, 9 pieces, 5 sheets, 20/20 and 17/17; the unknown kind: 1 warning; the morning's save holds the layout.
9. **Fixit Fidget > Days > Lab - Day 1 / 2 / 3 at the counter (half-finished café)** (one Play session each, a full day at human speed): DayShapeCheck's own verdict, with its bars unchanged (P2's), plus AtClosing's lines: Day 1 (8 lines): SavedLayout with 10 seats; the Shop's "!"; the YOUR CAFÉ card ("10 seats", "Place your first table"); Arrange from its button; the first ghost a bistro on a sheet, green (the twin: (-4.75, 10.25) turned); placed: -$80, 12 seats, one sheet folded; back on the phone. Day 2 (3 lines): 12 seats; the bay and the window counter on sale, not the lounge or the communal. Day 3 (3 lines): 14 seats; everything on sale. Reports in `Logs/CafeLife/HalfFinished/`, with their own runs file.
10. Then the house labs, on today's café (their saves are format 0, so 28 seats, unchanged):
   - **Fixit Fidget > NPC > Sit 4 - Live sit check (Play Mode)**: customers who take a window-counter stool now sit on it (feet on its foot ring); the NOTE about standing beside a stool should not appear.
   - **Fixit Fidget > NPC > Life 4 - Stools, settled guests, looks**: as before (the repair bar's stools; the guests at their named seats).
   - **Fixit Fidget > Checks > Seat choice rules**: section 7 (the scene) now sees the window counter's stools as seats people sit on; its Officer sits at one. If section 6 fails on the standing stools, that's the data edit in §4 (S's/P2's).
   - **Fixit Fidget > Checks > Waiting space reservations**, and a **Café life** lab session: as before; people path around the pieces' carved holes.

If a step's check fails, the step puts everything back (the shared step's rule); its report says which check and why. Undo order: Furniture 3 - Undo, then Furniture 1 - Undo (each ends "UNDONE: ... Save the scene."). Furniture 2's prefabs and the kinds' assets stay after an undo; a rerun rebuilds them in place.

## 6. Risks, and APIs I'm unsure of

- **Nothing here was compiled or run** (no Unity in this copy). `csparse` checks syntax only; every member the new code calls was grepped in this copy, but a type mismatch or a missing `using` only shows on his Mac. The reviewer should read `FurnitureCheck.cs`, `FurnitureSaveCheck.cs` and `FurnitureLabMenus.cs` closely: they were written last.
- **Carving at time scale 0.** Arrange runs while the world is still; I believe NavMesh carving updates every frame regardless of time scale, but if it waits for the world to move, the holes appear the next morning (no one walks at closing anyway). The cram's frame test runs at closing for the same reason: if carving is deferred there, its 50 ms number under-measures the carve, and the morning's first frames are the real cost.
- **Cinemachine at time scale 0.** The Arrange camera is driven by `CafeViewMode.OrbitTo` each frame on unscaled time; if the brain only blends on scaled time, the ease may jump instead of glide (functionally fine).
- **The nearest-modifier rule.** Each piece's root carries Ignore From Build; the lounge's inner Not Walkable modifier is switched off for that reason. If the NavMesh package applies modifiers differently (all of them, not the nearest), the bake could leave a hole where the lounge stood: Furniture 1's "guards not worse" check would catch it.
- **`PrefabUtility.SaveAsPrefabAsset` flattens nested prefab instances** (the café kit's pieces are kit prefab instances): the shop's prefabs won't update if the kit's prefab changes later.
- **The physics map vs the twin's data.** The labs' place counts (38, 35, 20, 17, 50, 47, 32, 29, 22, 24) come from the twin on the reference scene's data; `BuildMap` reads the live colliders. The checks require all reached / all served and report the counts; a different count with every place reached is a note, not a failure, except in the Edit Mode rules check, which is data only.
- **RecapUI.ContinueAfterNight** is how both Play labs skip the night (the call NightCycle makes once Ace is home); the night itself isn't exercised.
- **The virtual pad** (PlayLab) for the hold-to-sell test; once it's plugged in, `PadInput.UsingPad` is true for the rest of that session (the hints show pad icons; harmless).
- **Day 1's first table needs $80 in the till at closing**: a very poor Day 1 in the Days lab would fail that line with "You need $... more" on the plate.
- **The half-finished Day 1 seat medians** are judged at exactly S's line (10 seats, 4 zones) if the open seats' zones number four (§3, item 19).
- **The Days labs' Day 2 and 3 saves copy `StartDaysLab`'s** (Grace's memory and P2's money): they can drift if P2 changes that method (§4).
- **The window counter's stools at Max Lift 0.32 and Approach Side 0.60** are my reading of NPC wave 2's numbers for a 1.2 m spacing; Sit 4 measures the feet and the facing on them for the first time.
- **The dust sheets' look** is procedural: soft folds and a weave texture, no cloth simulation; the first photos (Furniture 3's) will show whether it reads as canvas.
