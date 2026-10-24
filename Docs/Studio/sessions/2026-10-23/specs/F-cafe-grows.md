# Designer F — the café grows with you: buy and place furniture (23 Oct 2026)

You are the Systems & Mechanics lead, with the Technical Director's and Environment's hats on. Read `/root/review23/builders-common.md`, `/root/review23/his-notes-2026-10-22.md` and `/root/review23/plan.md` first. You write a design and technical spec for his approval; you don't change code. Your copy for reading code: `/root/wt23/F` (read-only for you). Photos: `/root/review23/photos/CafeLife_*` (the café from above), `Stations_*` (first person).

## What he said (verbatim)
- "I also wanted to add more on the aspect of like adding inventory, so we can allow the player to kind of customize their own the cafe to their own in a sense, not like entirely, but like the furniture, like tables, chairs, where the NPCs can sit down."
- "But I want to be able to kind of like have the player to customize and add furniture wherever they want, pay for the furniture, kind of have a sense of like their own freedom, because this whole game is seeming like a simulation, and I feel like simulation definitely sell with a sense of sneaking at night, which is kind of fun."
- On the cart idea he chose **"Grow the café"**: Ace opens a half-finished café on Day 1 (counter, bench, drinks machine, repair bar; the seating area under dust sheets) and buys and places the furniture himself. The cart lives only in the intro, as story.
- On what furniture does he chose **"Seats count, décor is looks"**: each seat placed lets one more customer stay; lamps, plants and rugs are cosmetic. This changes the 9 Oct lock "layout is cosmetic, not systemic". His 9 Oct note had asked for customization as "just customization" with the hint that fewer tables leave more room to walk around.
- He wants playtest 8 (his girlfriend, recorded) **after** this is in.

## What exists to build on
- **The furniture catalogue** (Project doc `claude/furniture-catalog.md`; Blender, Aug 2026): 24 pieces in two tiers, "Workshop" and "Marble & Brass". Every Tier 1 piece has a Tier 2 counterpart at identical dimensions and seat heights, "so upgrading swaps the mesh without disturbing a layout the player already built"; built as "the furniture half of the between-days shop catalog". Seats at 0.45 m, dining tables 0.75, bar stool 0.75 with bar table 1.05, tub chair with lounge table.
- **The café's own pieces** in Assets/Art/Models: the `CK_` café kit, e.g. `CK_BanquetteBay`, `CK_CommunalTable`, `CK_WindowCounter`, `CK_Stool_Bar`, `CK_Chair_Cafe`, `CK_Table_BistroRound`, `CK_Table_Square2`, plants `CK_Monstera`, `CK_RubberPlant`, `CK_FiddleLeafFig`, `CK_Pothos`, `CK_OliveTree`. Also `Chair_TubChair` and the lounge set (20 Oct).
- **The café's seat plan** (Project doc `claude/studio/cafe-sf-research-and-redesign.md` §6.11): 24 seats in zones: the repair bar (3), the west banquette bays (6), the communal table (8), the bistro 2-tops (4), the east window counter (3); plus the lounge (2) since 20 Oct.
- **Seats in code:**
  - `TableSeat` (a `WaitingSpot`; `SeatPose`, `CupSpot`, `RepairBar`, dirty with a `DirtyCup`);
  - `WaitingArea` (the registry: seats register themselves at runtime, which is good for placed furniture; `Claim`, `ClaimRepairBar`, `FreeSeats`);
  - `NpcSeating` (the sit);
  - `PatronSpawner` (guests who just sit, limited by free seats);
  - `CustomerBrain` (seated vs loitering patience: 0.6× vs 1.15×, "the entire pressure model").

  Builder S is rewriting seat choice right now (seat tags: window, lounge, bar, communal; personality; personal space). Your placed seats must carry those tags. Read `/root/review23/specs/S-seats.md`.
- **The dressing:** the café's lived-in props are placed by editor steps relative to named seats and chairs: `CafeLivedInSteps.cs` (cups, laptops, coats on named seats), `CafeDressing3Steps.cs` (the lounge), `CafeFurnishing.cs`, `CafeSeatsSteps.cs`, `CafeTidySteps.cs`. Movable furniture breaks every "named seat" assumption. Decide what happens to each kind of prop: it becomes part of a furniture prefab, follows its seat, or goes.
- **The walking surface:** NavMesh (`Cafe routes.asset`, baked). Placed furniture must re-route people at runtime: `NavMeshObstacle` carving vs. updating a `NavMeshSurface`. Check what the project uses (grep `NavMeshSurface`, `NavMeshObstacle`, `NavMeshModifier`). The counter's queue (`CounterQueue`), the waiting spots, the door and the staff side must stay reachable.
- **The camera:** the overhead `CmShopCam` (isometric, the cut-away walls, zoom LT/RT) is where arranging should happen; Bible §2 says isometric is for moving and delivering. The phone (`RecapPhone`) has a Shop app at closing; the pause phone by day has Today, Notes, Settings.
- **The save:** `SaveManager` (atomic JSON with versioning and migration; "saves at Open Tomorrow"). The layout must save and load, and old saves must load with today's café as their layout.
- **The labs that know the café's layout by seat names:** grep for seat names like `"Seat (bistro 2-top` in `Assets/Scripts/Diagnostics` and `Assets/Editor`. They must keep working, or be rewritten to work from tags.

## What to write
`/root/review23/specs-out/furniture-spec.md`, with a one-screen summary at the top for his phone. Then:

1. **The player's experience, step by step.**
   - Day 1's half-finished café: what's there, what's under dust sheets, how the story says it (the cart in the intro: "years of selling coffee from a cart to afford this place").
   - The first purchase (when, what, how it's taught).
   - Arrange mode: how you enter it (and when you can't: during service? only before opening and after closing? or any time? Argue it); the camera; picking a piece; the ghost; snapping (grid size, rotation steps); green or red for fits or not, and why it doesn't fit; place, move, rotate, sell back (refund %); mouse and pad controls with `ControlHints` icons.
   - What customers do with new seats.
2. **The catalogue for v1:** which pieces, in which order they unlock, prices, seats each adds, footprint. Décor that's v1 and décor that waits. How Tier 2 works (an upgrade that swaps meshes in place, as the catalogue was designed).
3. **"Seats count":** capacity → how many customers can stay → money. Show the numbers so a bigger café pays, but a cramped one blocks paths (his "fewer tables leave more room to walk around").
4. **Technical design:**
   - the runtime furniture object (a prefab per piece: mesh, collider, seats with tags, cup spots, an obstacle);
   - placement validation (overlap, path to the counter, the door, the bench, the staff side, the queue);
   - NavMesh updating;
   - seat registration;
   - the save format and migration;
   - converting today's café furniture to placed pieces (an editor step that turns the scene's furniture into the default layout);
   - the dust-sheet state;
   - how the dressing props follow.

   Name every file to add or change, in an order to build it in, so that separate builders can take parts (e.g. 1 the placeable runtime and save; 2 Arrange mode's input and camera; 3 the shop's catalogue; 4 the default-layout conversion; 5 labs).
5. **The labs and checks that prove it:**
   - a placement lab (place, move, sell; paths stay open; a placed seat gets used);
   - save and load of a layout;
   - an old save loads with the default layout;
   - the Day 1–3 labs on the half-finished café.
6. **The risks** (scope: this is the biggest feature since the scope freeze; performance of carving; labs tied to seat names; save migration), and what to cut if it runs long.
7. **Questions for him**, each with your recommendation:
   - Arrange any time, or only while closed?
   - Grid or free placement?
   - Can you sell back, and at what refund?
   - Do Tier 2 upgrades come in v1?
   - Do customers react to décor (the lock says no)?

Optionally, mock up Arrange mode as one or two images over `/root/review23/photos/CafeLife_days-1-3-day1-2026-10-20_031410__02-calm.jpg` with Python (PIL; the font is `/root/wt23/F/Assets/Fonts/Baloo2/Baloo2-Bold.ttf`): a ghost table in green, a red one that doesn't fit, the catalogue strip, the controls hint. Save them under `/root/review23/mockups/arrange-*.png`.

Return the whole spec as your final message.
