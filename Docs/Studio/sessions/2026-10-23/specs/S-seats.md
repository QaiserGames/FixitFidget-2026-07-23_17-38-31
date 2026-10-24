# Builder S — seats chosen the way people choose them (23 Oct 2026)

Copy: `/root/wt23/S`. Rules: `/root/review23/builders-common.md` (read it first). Photos: `/root/review23/photos/CafeLife_*` (the café from the overhead camera, Day 1).

## His note (verbatim)
"I also noticed one other note was that when customers come in to do a coffee order, they literally just only sit to the left of the couch. And then when a customer comes in to do a repair, they only sit on the right next to the bar stool. And I want that to be more spread out and diverse in a sense, because I want there to be a feel for the player to like kind of look for the customer that did the order in a sense as well."

## What's there, and why (respect the history)
`Assets/Scripts/WaitingArea.cs`:
- **`Claim`/`PickNearest`:** people take the **nearest** free spot by walking distance from where they stand (the counter after ordering). Spots within 0.6 m of the nearest tie at random, with a penalty for heading toward a table someone else is walking to.
  - Why nearest (27 Sept 2026): it replaced a random free spot "which had people walking past empty chairs to one across the café — it read as automated".
- **`ClaimRepairBar`:** a customer whose repair goes to the bench takes the repair bar stool nearest the bench, middle first.
  - Why (14 Oct, NPC wave 2): his girlfriend in playtest 5, "people wouldn't sit down on the bar stools that are infront of the repair station". Since playtest 4, Ace reassures and hands back over the counter from the bench without a step.
- **Seating is a preference, not a requirement:** with no seat, a customer loiters and drains patience at 1.15× instead of 0.6×. "That difference is the entire pressure model."
- Seats are `TableSeat` (`WaitingSpot` subclasses). They register themselves, can be **dirty** (a `DirtyCup` on the table makes the seat unavailable until Ace clears the table, `MessTable`), and carry `SeatPose`, `CupSpot`, `RepairBar`.
- Who sits: `CustomerBrain` (repairs and drinks), `PatronBrain` (café guests who just sit), the lodger's visits.

The café's seats (Bible and `Docs`; check the scene steps `CafeSeatsSteps.cs`, `CafeDressing3Steps.cs` for names and zones):
- the repair bar's 3 stools;
- the west banquette bays;
- the long communal table;
- bistro 2-tops;
- the east window counter;
- the new lounge's two tub chairs (20 Oct).

## The design to build (make it read as people, not dice)
A seat's score for a person is a sum of reasons; they take the best, with a little randomness among near-equals so two people don't mirror each other.

1. **Character.** Each seat zone gets tags: window, lounge (comfy), bar, communal, quiet corner, near the door. Each customer kind or character gets leanings:
   - a Chatty person likes the communal table;
   - a Rushed one takes the nearest, near the door;
   - a Cheerful one likes the window;
   - someone with a long repair likes comfort.

   Use the characters the game already has (grep `CustomerProfile`, `NpcProfiles`, `character` in the day logs: "Chatty", "Cheerful"…). Keep the table of leanings in one place, data you can tune.
2. **Personal space.** People prefer seats away from strangers: a penalty for an occupied seat next to the candidate, a smaller one for the same table. When the café is quiet they spread out; when it's busy they fill in. This alone spreads a day across the room.
3. **Distance still counts, but less.** Nobody crosses the whole café past a perfect seat without a reason. The reasons above must outweigh the walk, the walk breaks ties.
4. **Regulars have a favourite seat.** Grace by the window, the Officer at the bar, if those zones exist; choose and say. They take it if free, otherwise they score like anyone else. That's character for free.
5. **Repair customers.** Most still take the repair bar (her playtest 5 note, and Ace's reassure from the bench depends on it). Some, by character, prefer comfort and sit elsewhere, e.g. a long repair plus a "likes comfort" leaning, about one in three. Then the player carries the device to them.

   Verify in `CustomerBrain` (and `PlayerInteractor`, the hand-back and reassure code) that:
   - the hand-back works wherever the customer sits;
   - reassuring works at their seat (or by walking up to them);
   - the ticket, the name pin and the job-colour dot still lead the player to them.

   If the hand-back is tied to the bar, say exactly what must change. This is the "look for the customer" he wants: it must be findable, not frustrating.
6. **The future.** Next he gets furniture he buys and places himself; every seat he places must just work with this (no seat names in the scoring, only tags and positions). Tags come from the seat (a field on `TableSeat`, or a component) with a sensible default for untagged seats. The scene's existing seats get their tags from a step you write, with undo.

## The proof: labs
- **Seat variety in the Day labs** (`DayShapeCheck.cs`, "Fixit Fidget/Days/Lab - Day 1/2/3 at the counter"): record every seat claimed, and add checks (choose the thresholds and say why):
  - Day 1 uses at least N different seats across at least 3 zones;
  - no single seat takes more than X % of the day's sittings;
  - drink customers don't all sit in one zone.
- **Edit-mode rules** for the scorer (a pure function you can test without Play): personal space spreads two people; a Rushed person takes the near seat; Grace takes her favourite when free; a repair customer takes the bar when the scorer says so; a dirty seat is never chosen.
- Existing labs that seat people must keep passing:
  - "Life 4" stools and settled guests;
  - "Sit 4";
  - "Hands on it";
  - the bench's hand-over probe;
  - the conversation lab.

  Grep for `ClaimRepairBar` and `Claim(` users.

## Files you own
`Assets/Scripts/WaitingArea.cs`, `Assets/Scripts/TableSeat.cs`, `Assets/Scripts/WaitingSpot.cs` (if needed), the seat-claiming parts of `CustomerBrain.cs` and `PatronBrain.cs` (touch the minimum; say exactly what), a new data or class file for leanings and tags, a new editor step to tag the scene's seats, `DayShapeCheck.cs` (only the seat-variety part), and a new edit-mode rules file.

Don't edit `UiPins.cs`, `BeverageStation.cs`, `RecapPhone.cs`, `NightNotebook.cs` or `AimAssist.cs`.
