# Builder Y: Day 1's first table, the thrift find in the Day labs, and Day 2's timing (26 Oct 2026)

You are the systems designer-engineer for Fix It Fiasco's Days 1–3 and the café that grows. Today's run on his Mac left four open items in your area. He is away and will test once everything is in place, so make the calls, explain them, and leave them overrulable.

## Read first
- `/root/review24/builders-common.md`: the house rules. Two changes since it was written:
  - his Mac's current scene and prefabs are in `/root/ref26` (read-only), not ref24;
  - you commit in your copy (branch `work-Y`); that wins over "commit nothing".
- The reports from his Mac (staged, read-only):
  - **Day 2 (full café), three runs:** `/mnt/user-data/uploads/FixitFidget/Logs/CafeLife/days-1-3-day2-2026-10-26_085845/report.txt` (pass), `…_090634/report.txt` (fail: first walk-in), `…_091425/report.txt` (fail: medians).
  - **Day 3:** `…/days-1-3-day3-2026-10-26_093630/report.txt` (fail: bench jobs median).
  - **Half-finished Day 1:** `…/CafeLife/HalfFinished/days-1-3-day1-2026-10-26_094421/report.txt` (bought the first table with $145) and `…_111556/report.txt` (fail: $70, "You need $10 more").
- Builder P2's and F2's reports, `/root/review24/reports/P-report.md` and `F-report.md`: the Days design and the furniture design. Read only the sections you need.
- The code: `Assets/Scripts/Diagnostics/CafeLab.cs` (the autopilot at human speed, `WaitingJobs`, `FinishByHand`), `DayShapeCheck.cs` (the bars), `Downtime.cs` (the Now line's order), `ThriftShelf.cs`, `FurnitureRules.cs`, `CafeLayout.cs` (`AddMoney(-kind.price)` at line ~281), `ArrangeMode.cs` (~587), `ArrangeUI.cs` (~270), and the walk-in spawner (grep "first walk-in" and `PatronSpawner`/`CustomerSpawner`).

## 1. Day 1's first table is free (his call to overrule)
**What happened:** the half-finished café's Day 1 closed with $145 in one run and $70 in another (the second skipped the $55 thrift watch). With $70, the first $80 table couldn't be bought, so the "Build your café" teach failed: the YOUR CAFÉ badge asked for something he couldn't afford.

**The call:** the first table bought on Day 1 is free. "Table" means a piece with seats on sale on Day 1: the bistro or the square table. The plant stays $35.
- Show the price it would have cost, so he still learns what tables cost: "$80" struck through, then "Free today".
- Prices are back to normal from Day 2, or after that one table, whichever comes first.

**Why:**
- A teach must never fail on luck.
- The best-selling cozy games give the first build away: Stardew's mayor leaves the first seeds, and Animal Crossing's first tent is free.
- The money sense he wants ("builds a sense of money") starts with Day 2's prices, not on a Day 1 the player can't control.

**Build it in one place:** a price function in `FurnitureRules` (e.g. `PriceToday(kind)` and `PriceWords(kind)`) used by:
- the placing check (`FurnitureRules` ~571);
- the charge (`CafeLayout` ~281);
- the strip (`ArrangeUI`'s afford test and price pill);
- the prompt line (`ArrangeMode` ~587).

Remember that the first table was free in the save only if it matters for selling: selling gives half back, so a free table must sell for $0, never $40. Grep `SaveData` and `CafeLayoutData` before adding a field; if you add one, add it the house's way (additive, with ValidateAndMigrate).

**The labs:** update the half-finished Day 1 lab's expectations (`DayShapeCheck`'s furniture part, `FurnitureCheck`, `FurnitureRulesCheck` and `FurnitureSaveCheck` as they apply) to the new rule. Keep every check's strength: it must still prove the money moved right ($0 for the free table, the full price for the next) and the seats and sheets changed.

## 2. The thrift find in the Day labs
**What happened:**
- Days 2 and 3 failed "bench jobs done, the median of the last 3 runs: 2 (at least 3)".
- The reports show why: "The autopilot's waiting jobs … 8 table(s) cleared, 2 chat(s), 0 refill(s), **0 time(s) at the find**" on every Day 2 and Day 3 run.
- On Day 1, one run worked the find once and the other never did.

So it isn't that the labs' hand can't finish the cracked screen or the circuit. The autopilot never gets to the find. Find out which it is:
- **Is there a find on the shelf** when a Day 2 or Day 3 lab starts? A player buys tomorrow's find at closing. A lab that starts on Day 2 from a lab save may never have bought one.
- **Does `Downtime.Next`** (the Now line's order, which the autopilot follows) ever reach the find while tables keep coming? Each table takes 6 s, and there were 60–105 s of "nobody needs Ace" per day.
- **Anything else** that keeps `Downtime.Job.Find` from being chosen: the bench busy with a customer's job, `NobodyNeedsAce`'s rule, the find's state.

**Fix the real cause:**
- If the labs skip what a player does (buying the find at the prior closing), make the lab do it.
- If the game never offers the find in a normal day, that is a design problem for the player too. Fix the Now line's order or rule the way P2's design intended ("the four things to do while waiting"; read P's report §6.7 and the header of `Downtime.cs`), and say what changes for the player.
- Then make sure `FinishByHand` really finishes Day 2's cracked phone and Day 3's frozen phone (the circuit). If the device must be opened first (screws, cover), drive it through the same calls the bench labs use. Don't edit the bench's files: builder N is working in them. Use their public members or the labs' reflection, as `CafeLab` already does.
- Don't lower the bar of 3.

## 3. Day 2's end: the median 7:42 against "by 7:30"
- The three Day 2 runs ended at 7:42, 7:44 and 7:08, so service ran about 43 s past closing in two of the three.
- P2's rule was "walk-in repairs stop 90 seconds before drinks do, so the day ends by 7:30".
- Read who was inside at closing in each run and why they took so long: a late bench job, the walk from the street, the autopilot's hands busy with tables.
- Fix the game's rule if it doesn't hold. If it's the lab's hand (for example, clearing tables while a customer waits), fix that. Explain which.

## 4. The first walk-in reached the till at 1:29 (no sooner than 1:30)
- One Day 2 run's first walk-in reached the till at 89.9 s.
- The spawner looks ahead by its estimate of the walk: "From the street to the counter: 30–43 s".
- Make the first walk-in's timing hold the bar for real, e.g. plan the spawn on the shortest walk measured, not the median. Don't loosen the bar.

## 5. ArrangeUI's words (the gather, S1 and S2)
These are yours this round:
- "2 seats", "3 seats" and "8 seats": 20 px, contrast 2.7, thin.
- "Day 2" and "Day 3": 20 px, 2.5:1.
- "+2": 28 px, 2.1:1.
- 13 S2 more.

The floor: at least 24 px (20 only for a secondary label), bold or semibold, and 4.5:1 or on a plate or shadowed. See `/mnt/user-data/uploads/FixitFidget/Logs/Review/text-2026-10-26_112735/findings.csv` (filter the owners column for ArrangeUI). Keep the strip's layout intact.

## Files

**Yours:**
- `FurnitureRules`, `CafeLayout`, `ArrangeMode`, `ArrangeUI`.
- `CafeLab.cs`, `DayShapeCheck.cs`, `Downtime.cs`, `ThriftShelf.cs`, the walk-in spawner.
- The furniture and Days labs and checks.
- New files if you need them (e.g. an editor step for data).
- `SaveData` only for an additive field.

**Not yours (others are building in them right now):**
- `RecapPhone` and every HUD or words file: builder R.
- The bench's logic and `PadInput`: builder N.
- The drink machine and `StationInteractable`: builder K.

If one must change, write the exact edit in your report.

## Rules
- **Data in `.asset` files** (the Days) changes only through an editor step (house style, with Undo, ending "DONE").
- **A serialized value in the scene or a prefab overrides a code default.** Grep `/root/ref26` before tuning a field.
- Mark edits `// 26 Oct 2026 (Days and first table)`.
- Run `python3 /root/tools/csparse.py` on every C# file, and grep the definition of every member you call. Nothing compiles here.
- **Be economical:** he asked us to preserve usage. Grep first, read only the ranges you need. Keep a short progress log at `/root/review24/reports/Y-progress.md`.
- Don't use any remote-device or Mac tools, and don't spawn agents.

## Report: `/root/review24/reports/Y-report.md`, under 700 words
1. The files.
2. For each of items 1–5: the cause you found, with evidence, and the fix.
3. Decisions he may overrule: the free table's exact rule and words, and any change to the Now line's order.
4. Edits needed in others' files.
5. The Mac steps and labs in order, with what a pass looks like. At least: the furniture rules, place/move/sell and save labs; the half-finished Day 1; Day 2 three times and Day 3 three times (the medians); the four waiting-jobs labs.
6. Risks.
