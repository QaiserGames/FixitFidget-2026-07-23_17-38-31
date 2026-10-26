# Reviewer P: Days 1–3 that teach (Builder P2)

Copy: `/root/rv24/P`, commit `085af93`. csparse is clean on all three changed files.

## Fixed
- **`DayClock.cs` EndDay:** now calls `ThriftShelf.SettleAtClosing(Day)` before `closingTill` is read. Before this, the till rose by the find's sale but the recap's "Closing till" row (RecapPhone detail sheet, RecapUI, the saved RecapSaveData) didn't include it. SaveManager's own call stays as a safety net and does nothing the second time (`settledDay`). There's still no double count: `DayClock.Earned` never includes the sale, and RecapPhone adds `finds` once.
- **(a) `HudCorners.cs`:** applied P-report §4a's chip edit exactly as written: the `UpdateChips` lines, `ChipFor(..., bool atMachine, ...)`, the `shown` key, and the title, line and colour.
- **(b) `Editor/SeatChoiceRules.cs` §6:** the three window-counter fixtures are now `standing: false`, with the comment "they sit since Furniture 1 (c12)". Nothing else in §6 or §7 was touched.

## Checked and sound
- **Closing the close-up for a customer.** `GiveWay` calls `CancelInspection`, which is the same Release path that StepBack and the day's end use. It puts the find back at its rest position on the corner, returns a swapped device, sets the camera priority and FOV back, and clears the tool. PlayerInteractor reads the inspector's state, so input can't stick. Settle releases the corner slot before it destroys the find.
- **The sale and the takings.** The sale runs before `money` is read in CaptureState. `soldDay` and `soldFor` are saved, so a resumed recap shows the same takings.
- **The thrift save record.** It is additive, and a missing record in an older save means `known` is false. ValidateAndMigrate cleans it, and TryCreateNextDay deep-copies it.
- **The stock split, reader by reader:**
  - Whole stock (`Cups`/`Beans`): the HUD, the save, the Shop and `CupsLow`.
  - The machine (`MachineCups`/`MachineBeans`): drinks, BeverageStation, CupStack, BeverageCupSupply and the guide's drink steps.
  - Load order: GameBootstrap's Start (-100) runs `SetStock` (the whole stock fills the machine), then ShopInventory's Start applies the saved machine share.
  - An older save (`-1`) skips that second step, so the machine fills from the stock as intended.
  - PauseCheck's 3 cups with an empty shelf still shows "Low on cups · 3 left / Restock at closing".
- **Downtime and the Now line.**
  - `ShowingWaitingJob` is cleared every LateUpdate and set only in Present. The meter runs at order 1000, after the line.
  - Day 1's goal card and "is visiting again" are never marked replaceable.
  - Every quiet line is 10 words or fewer, including the "X's on the way." lead.
- **The gossip facts.** "TV from 11, in bed about 1:20" matches GraceNight's usual night, and the cups-on-worktop fact matches NightThings. The `gossip.` ids can't collide with the night's own ids. Each fact is 7 words or fewer.
- **The stool choice against Builder S's rule.** The reach over the bench is measured from the bench's stand point (`AcrossTheBenchReach` 4.1 m). The east stool is about 3.85 m after a43, so all three stools are reachable.
- **The c11 roll.** It uses the person's own hash and leaves Unity's random sequence alone. It is 0 on Day 1 and for regulars.
- **H's seats against F2's layout.** H's seat names exist in the reference scene, and both are among F2's open seats: the lounge by the counter and the north bistro.
- **DayShapeCheck.** The dead-air CSV's 19 columns match its header. The median columns 2, 3, 10 and 17 are right. Both the runs file and the dead-air file go in the folder's parent, so F2's half-finished labs write to `Logs/CafeLife/HalfFinished/` and their medians never mix with today's café's.

## Not fixed
- **Low, `ItemInspector.TryCollectInspectedItem` with a find.** The find's `ItemInteractable.Interact` re-opens the close-up instead of picking it up. So "collect" in a find's close-up closes it and opens it again. The fix: in TryCollectInspectedItem, return false when `pickup.IsFind`.
- **Low, `ThriftShelf.GiveWay`.** Ace can open the find while a customer already stands at the counter, and it closes again within a frame. That is his p6 working as asked, but it can feel like a glitch. The fix: in `IsAvailable` (and in ItemInteractable for a find), return false while `Downtime.AtTheCounter != null`.
- **Low, gossip copy (NotebookEntries).** "Grace bought fancy cups for tonight's reunion." Her Day 1 line says the reunion is "tomorrow", so it falls on Day 2. But that night she is home on her usual TV night, and Ace takes the cups from her worktop. "Tonight's" reads as a contradiction. Suggest "for the reunion"; it's placeholder copy for his sister anyway.
- **Low, `RecapPhone.BuildShop`.** When a resumed recap's find didn't sell, `shelf.Find` is null (it is only made in the morning), so the "waits on the shelf for tomorrow" hint doesn't show. Cosmetic.
- **Low, per frame, `ThriftShelf.Bench`.** If Props 1 hasn't run (no side slot anywhere), every `IsAvailable` or `OnTheShelf` call runs a FindObjectsByType. This only matters before the step is run.

## Lab expectations at risk
- **Seat choice rules §6:**
  - With the window stools now real seats, "no one seat best for more than six of ten" and "at least three zones" may shift; they will most likely improve.
  - The Officer's "his favourite wakes once they sit" is now true in §6's data, so §6's Officer line ("not standing at the window counter") still holds, but his seat may be a window stool.
  - Re-read its numbers on his Mac.
- **Day labs, dead-air bars and the bench-jobs median:** these depend on the abstract autopilot's timings and on FinishByHand finishing a cracked screen or circuit (P2's own §6 risk). Days 2 and 3 are the likeliest to miss.
- **Pause:** unchanged in principle. A new failure would come from the chip change, so watch for one.
- **Waiting space reservations:** line 17 of its header says the stools are within `acrossCounterReach` (3.5 m) of the bench's stand point. If it really tests 3.5 m rather than the bench's 4.1 m, the east stool (3.85 m) fails. That check predates this work, but it now matters, because bench jobs use every stool.

## Needs his decision
- **p1:** his answer said "a cleaning job… not the thrift pocket watch P recommended". P2 built a thrift *pocket watch* with a cleaning fault (brush and cloth, no new tool). It meets "no new tool", but not perhaps "not the watch". The other choices are a grimy phone or a dusty camera.
- **CanMake reads the whole stock** (P wrote "the machine"). So a customer is never refused while the back shelf has stock.
- **A find sells at Good** (80 %) even if it isn't fully finished.
