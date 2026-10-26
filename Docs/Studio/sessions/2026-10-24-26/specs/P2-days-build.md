# Builder P2: build Days 1–3 that teach, and the four things to do while waiting (24 Oct 2026)

You are the Systems & Mechanics builder, with the Progression & Economy lead's spec in hand. Read `/root/review24/builders-common.md` first, then `/root/review24/his-answers-2026-10-24.md`. Your copy: `/root/wt24/P`. Your progress log: `/root/review24/reports/P-progress.md`. Your report: `/root/review24/reports/P-report.md`.

## What you build
Designer P's spec, approved by him for building: `Docs/Studio/sessions/2026-10-23/designs/pacing-downtime-spec.md` in your copy, 933 lines. Read all of it. **§6 is the build order** (Parts 1–7), and **§6.8 gives the Day labs' new bars**. Build Parts 1–7 in that order, all of them, with his answers applied:

- **p1: Day 1's side project is a cleaning job, so Day 1 brings no new tool.** P recommended the thrift pocket watch, but he chose the clean.
  - Day 1's find uses only Day 1's tools (the brush and the cloth: look at Days 1–3's data and the bench's tool unlocks before choosing). Make it something with a story, e.g. a grimy phone or a dusty camera off a thrift shelf, never the same device and fault as that day's walk-in repair.
  - Keep the rest of P's chain ("tomorrow's new job with nobody waiting") from Day 2 on, within what each day's tools allow. Explain the chain you chose in your report.
  - Day 1's find is free and sells at closing, as the spec has it; Day 1's teaching line becomes "Your thrift {thing}: clean it while you wait." (or similar, ≤ 10 words).
- **p2:** finds one at a time: bought at closing (the phone's Shop) for tomorrow, fixed the next day, sold by itself at closing.
- **p3:** Day 3 is two walk-ins at once.
- **p4: the support call moves to Day 3, with its real phone model.** This includes the art finding **a03** ("The support-call phone is a grey block"; the fix: "give the support-call phone the detailed phone's parts, then rerun Bench 7's layout and check it fits the intake tray").
  - Find how the support-call device is built (grep "support call", "SupportCall", `Human`/`Bureaucratic` faults, and its prefab in `/root/ref24/Assets/AssetsPrefabs`).
  - Write an editor step that gives it the detailed phone's parts, using the hero phone model behind `PhoneRepair.prefab`, without breaking the support call's own flow (the call on hold, the counter repair).
  - Then move it to Day 3 in the days' data.
- **p5:** the mortgage stays story only.
- **p6:** when a customer reaches the counter while Ace is on his side project, the close-up closes by itself (§2.2, "How it never blocks a customer").
- **r1:** Day 1's half-finished café has **10 open seats**: the repair bar's 3, the lounge's 2 tub chairs, the window counter's 3 stools, and one bistro 2-top.
  - The furniture builder (F2, working at the same time in its own copy) owns the dust sheets, the layout and which pieces exist.
  - You own the day's flow on that café: P's §4.2 and §8, as agreed.
- **r3:** two settled guests on Day 1 (four from Day 2, as P's §8.1).

## Also yours: the seat fixes and two Day-lab bars from the 23 Oct run (Claude's calls; his to overrule)
His 22 Oct note was: "when customers come in to do a coffee order, they literally just only sit to the left of the couch. And then when a customer comes in to do a repair, they only sit on the right next to the bar stool. And I want that to be more spread out and diverse in a sense, because I want there to be a feel for the player to like kind of look for the customer that did the order".

Builder S's seat choice (`Assets/Scripts/SeatTags.cs`, the `SeatChoice` class; `WaitingArea.cs`) fixed part of it. The 23 Oct run showed the rest:

1. **The repair bar's middle stool, every time.** `WaitingArea.ClaimRepairBar` takes the free stool nearest the bench, which is always the middle one when the bar is empty. Choose among the bar's free stools with the same personal space and near-equals randomness as other seats (`SeatChoice.Crowding`, `NearlyBest`). Keep Builder S's rule that Ace reaches all three stools over the counter from the bench; NPC wave 2's Life 4 expects "middle, east, west" when three come in turn, so update that lab's expectation if you change the order.
2. **c11, kept by him: "From Day 2, about 2 in 5 repair customers sit away from the bar; never on Day 1."** In six Day 2–3 runs only 1 of 11 walk-in bench jobs did. The reason: only a Sentimental person, or a Relaxed one with a long repair, leaves the bar (`SeatChoice.StaysAtTheBar`), and those days' walk-ins are Cheerful, Chatty and Impatient.
   - Make c11 true, so a player sees it on Days 2 and 3. For example: a per-visit roll that sends about 2 in 5 walk-in bench jobs from Day 2 to a seat away from the bar, biased by character (the Impatient and the Rushed want to watch, the Sentimental and the Relaxed want comfort). Never on Day 1; regulars stay.
   - Keep `SeatChoiceRules` (Fixit Fidget › Checks › Seat choice rules, 59 checks today) true and extend it: over many simulated visits the share away is about 40 % from Day 2 and 0 on Day 1.
3. **Drink customers can still all take the nearest window seat in a quiet run** (Day 3 run 1: four at the north banquette 2-top). P's clearing tables fixes this on its own: a guest who leaves a cup makes that seat unavailable until Ace clears the table (`TableSeat.IsAvailable`, `DirtyCup`, `MessTable`), so the next customer can't take the same seat. When you build Part 2, make sure customers (not only patrons) can leave cups. Check that the spread shows in the Day labs' seat notes.
4. **"The day ended by 7:30" failed 3 times in 9 Day-lab runs.** Each time it was a customer who set off just before last orders: Nina's drink, then Alishba's repair (about 90 s at human speed) at 6:27. Make last orders for walk-in repairs come earlier than for drinks, by the repair's typical length. Judge "the day ends by 7:30" on the median of the day's last 3 runs, as the seat medians are judged (`DayShapeCheck`), still noting each run's own time.
5. **"The first customer reached the till within 15 s"** was 15.5 s once (13.7–13.8 s before 23 Oct, 14.3–15.5 s since the seat dice changed the random sequence). Find what makes her walk vary. If it's only the random sequence, judge it on the median of 3 runs too, and say so. Never just raise the bar.

## Who owns what (F2 builds the furniture at the same time)
- **Yours:**
  - the days' data and the steps that write it;
  - `CustomerSpawner`, `CafeArrivals`;
  - `DayOneGuideUI` (the Now line in quiet moments);
  - `BenchCards` and the bench's thrift-find corner;
  - `ItemInteractable` and `ItemSlotArea` as P's §2.2 needs;
  - `DirtyCup`, `MessTable`, `TableSeat`;
  - `WaitingArea`, `SeatChoice` / `SeatTags`;
  - `CustomerBrain` where your parts need it;
  - the chat and the notebook facts (`NightNotebook`'s data side, `NoteSubjects` if a new subject is needed);
  - restock (`ShopInventory`, the back shelf);
  - the support-call device;
  - every lab and check for these (`DayShapeCheck`'s day bars, `SeatChoiceRules`, new labs).
- **F2's:** the furniture runtime, Arrange mode, the dust sheets, the layout save, `DayIntro`'s Day 1 line (the cart line: his choice "the cart only in the intro"), the Shop's YOUR CAFÉ card.
- **Shared:** both of you may need to edit these. Keep your edits small, additive, and marked with a `// 24 Oct 2026 (Days)` comment; the integrator merges by hand.
  - `SaveData.cs`, `SaveManager.cs` (your thrift find's state, F's layout);
  - `RecapPhone.cs` (your thrift find in the Shop and the Tonight line; F's YOUR CAFÉ card and Arrange button, both in `BuildShop`);
  - `PatronSpawner.cs` (your two settled guests on Day 1; F's fallback by S's tags);
  - `DayShapeCheck.cs` (your day bars; F's half-finished café menus);
  - `CafeLab.cs`, `ControlHints.cs` (additive verbs only).
- **Props:** the new small props (the thrift shelf by the bench, the back shelf behind the drinks machine) are made by your editor steps from house parts or simple shapes, near the bench's stand point and behind the machine. Grep `/root/ref24`'s scene for the exact positions and names. The Blender builder (B2) is busy with the art fixes; if a prop really needs a model, say so in the report and use a stand-in.

## The code you'll lean on
P's spec names it with line numbers; check every one, because the 23 Oct merge moved some. Grep before you trust a line number: `RepairJob`, `BenchStage`, `BenchCards`, `BenchLab.cs:126-145` (a customer-less phone on the bench), `CircuitIntegrationChecks.cs:11-45` (the practice phone), `JobBase.cs` (grades and pay), `DayDefinition`, `CustomerSpawner`, `DayClock`, `RecapUI`, `RecapPhone` (J's juice is merged: use `UiTween` for anything that moves), `LodgerDay.cs` (the cups after a skipped morning), `NightNotebook`/`NoteSubjects` (N's pictures are merged: a new subject needs a picture or a drawing; say which).

## The proof on his Mac
List the steps and labs in the order he must run them, with what a pass looks like:
- the data step;
- the props step;
- the support-call phone step;
- edit-mode checks (Seat choice rules, Waiting space reservations, Night notebook rules, your new rules check);
- the labs: the Day 1/2/3 labs (3 runs each, with P's §6.8 bars), your new labs for each waiting job, the Day 1 guide lab, Stations, Life 4, Sit 4, Night 0.

Every lab you change keeps its count honest. Say the new expected counts.

## Done means
- Parts 1–7 are built, his answers are applied, and the seat fixes and the two bars are done.
- `csparse` is clean on every file.
- The report lists every decision his to overrule.
- The report has an exact merge note for each shared file.
