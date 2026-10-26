# Reviewer F: the café that grows (Builder F2), 26 Oct 2026

Clone `/root/rv24/F`, commit `ee545e3`. `csparse` is clean on every file I changed.

## Fixed
- **`Editor/FreshGameMenu.cs`** (Fresh game 1): the menu wrote `new SaveData()` to disk. That makes `HasSave` true, and a format 0 save loads as today's 28-seat café, so his "Start a new game" never opened the half-finished café (r1). The first morning now carries `CafeLab.HalfFinishedLayout(...)` (format 1: 10 seats, 5 sheets) when the café scene is open. With another scene open it falls back to format 0, as before.
- **`PatronSpawner.cs` StandInSeats**: a stand-in seat could be a later guest's own named seat. Seats aren't claimed until `SeatGuest`, so `SeatsFor` would give the same seats to that guest too (two guests in one seat after a sale). Stand-ins now skip the named and alternative seats of every other guest booked for today. Day 1 doesn't change: all three guests' named seats are under sheets (I checked the reference scene).
- **`RecapUI.cs` ReturnFromArrange**: if a lab's `ContinueAfterNight` ran while Arrange was open, Arrange's own self-close on the next frame put the recap phone back up over the new, running day. It now returns when the day isn't over.
- **`ArrangeMode.cs`**: it ignores buttons on the frame it opens. On a pad, the ✕ that pressed "Arrange the café" (the UI's submit) could also count as Arrange's place in the same frame, buying the first bistro unseen.

## Not fixed
- **Medium, `FurnitureCheck`/`FurnitureSaveCheck` (`CafeLayout.Apply`)**: going back to NewGame or OldSave never puts the café's own pieces back at their scene poses. Only a lab can hit this (normal play applies once). Today's labs avoid it: the save lab's moved south bistro is off in the new game. Fix: store each own piece's scene pose at Awake and use it for both sources.
- **Low, `SaveManager.LoadFromDisk`**: an unreadable save sets `HasSave` false, so his café shows half-finished. Writes are blocked, so nothing on disk changes, but the room he sees is wrong. Fix: keep a `LoadFailed` flag and apply OldSave in that case.
- **Low, `CafeLayout` vs `CutawayWall` / `NpcLifeProps`**: their caches may be built in Start before Apply switches pieces off. `AfterArranging` refreshes them only after an Arrange. Worth a look on a new game's Day 1.
- **Low, `FurnitureSteps` Survey**: the prefab-instance guard checks the pieces' parts, but not the lounge lamp or the banquette's two end caps, which are also re-parented. If one is inside a prefab instance, Furniture 1 throws partway through. Its rollback should catch that.
- Checked with no problems found:
  - The `SaveData` and `CaptureState` merge order with P2 (thrift, then memories, then cafe).
  - Furniture 1's re-parenting: `Undo.SetTransformParent` keeps every object, so serialized references survive. No other script uses the old paths.
  - The NavMesh: each piece root is Ignore From Build, with one carving box per solid; the surface needs Ignore NavMesh Obstacle.
  - Arrange is exclusive against the pause phone (blocked while `RecapUI.Showing`, which includes arranging) and the recap phone (its Update is off while put away).
  - Time scale: Arrange never changes it; the recap's 0 holds.
  - In `BuildShop` the order is YOUR CAFÉ, then stock, then thrift.
  - DayShapeCheck's `halfFinished` hunks sit cleanly beside P2's bars.

## Lab expectations at risk
- **RecapPhoneCheck's pad walk** ("the D-pad goes up to the restock"): Close up's "up" goes to the last *pressable* row. P2's thrift card comes after the stock card, so if tomorrow's find is pressable (affordable or "Free"), up lands on the thrift button and the walk fails. The restock is the last content button only when the thrift button is greyed or missing. P2 or the integrator should fix it: make the check's expected button "the last pressable", or grey the thrift card in that sample.
- **RecapPhoneCheck step 6**: YOUR CAFÉ now pushes the stock card down. `Clickable(restock)` may need a `RevealKey("restock")` first.
- **The twin**: I re-ran `f5/labs.py`, `labs2`, `labs3`, `save.py`, `turn.py` and `day3.py`. Every number in the labs matches the twin's output. That includes 38/35, the aisle verdict, 20/17, (-3, 11) and (-2.5, 10.25) turned, the bays, 0.89 m, $60, the cram (40 seats, 50/47) and the save lab's four positions (32/29, 22 seats). It also includes Day 1's ghost at (-4.75, 10.25) turned (sheet 2), Day 2's square at (-2.25, 9.0) turned (sheet 3), and 12 and 14 seats.
- Live physics (`BuildMap`) may count places differently from the twin's data; the Play labs only note that. The Day 1 half-finished lab needs at least $80 in the till at closing. The cram's 50 ms frame limit is his Mac's number.

## Needs his decision
- **Should the half-finished café become the Day labs' only Day 1?** Yes, for Day 1. A real new game (and now Fresh game 1) opens with 10 seats, so a 28-seat Day 1 measures a room no player ever sees on Day 1. Days 2 and 3 should follow the week's model (a bistro, then a square table). Do it in `StartDaysLab` after his Mac run shows the HalfFinished menus pass, then retire F2's three menus. The old 28-seat runs file stays as history; its medians don't carry over.
- The same question for every other "fresh Day 1" lab that writes `new SaveData()` (Day 1 guide, intro, bench and tray labs). They still run on 28 seats (F-report §3.20).
- `RoomAtLeast` 10 against 11 on the half-finished Day 1 (F-report §3.19).
