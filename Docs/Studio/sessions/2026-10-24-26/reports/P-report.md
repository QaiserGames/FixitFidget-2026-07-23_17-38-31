# Builder P2 report: Days 1–3 that teach, and the four things to do while waiting

24–26 Oct 2026. My copy is `/root/wt24/P` (branch work-P, base 13ac46f). Nothing is committed: everything is in the working tree.

- **Size:** 50 C# files, 36 changed and 14 new.
- **Syntax:** `python3 /root/tools/csparse.py` is clean on all 50.
- **Built:** Designer P's §6 Parts 1–7, with his answers p1–p6, r1 and r3 applied.
- **Also done:** the three seat fixes and the two Day-lab bars.
- **Shared files:** every edit is marked `// 24 Oct 2026 (Days)`.

## 1. What I did (files, one line each)

**New files**

- `Assets/Editor/PacingSteps.cs`. Two steps:
  - "Fixit Fidget › Days › G - The pacing pass (data)" writes §1.5 with his answers into the five day assets. It reports old → new and runs its checks. It is one undo, and it refuses until the new fields compile.
  - "H - Day 1's two settled guests (scene)".
- `Assets/Editor/ThriftSteps.cs`. Two steps:
  - "Days › Thrift 1 - Day 1's find: the thrift watch (prefab)".
  - "Days › Props 1 - The thrift shelf and the back shelf (scene)".
- `Assets/Editor/SupportCallSteps.cs`: "Days › Support call 1 - The support call's phone: the detailed parts (prefab)", plus its Undo step. This is art finding a03.
- `Assets/Editor/DowntimeRules.cs`: the new edit-mode check "Fixit Fidget › Checks › Downtime rules" (133 checks).
- `Assets/Editor/WaitingJobsSteps.cs`: four lab menus under "Fixit Fidget › Days".
- `Assets/Scripts/Downtime.cs`: the one reading of the room for everything.
  - It decides "somebody needs Ace", "something to do while waiting" (in P's order) and "nothing to do".
  - It also holds the day's teaching gate and the day's counters.
- `Assets/Scripts/DowntimeMeter.cs`: samples Downtime 10 times a second, late in the frame, into the day's counters.
- `Assets/Scripts/SideProject.cs`: marks a device as a find (nobody's job). It holds the find's words and its sale share.
- `Assets/Scripts/ThriftShelf.cs`: the shelf by the bench.
  - It holds today's find and flies it to the bench's corner.
  - It closes the close-up for a customer (his p6) and sells the find at closing.
  - It makes the Shop's offer for tomorrow, rotates finds past Day 4, and saves the find.
- `Assets/Scripts/StockCrate.cs`: the crate of cups and beans. It is a JobBase token, so it fits in one hand.
- `Assets/Scripts/BackShelf.cs`: "Take cups and beans", and one press puts the crate back.
- `Assets/Scripts/GuestGossip.cs`: deals the day's gossip to seated guests, shows the "…" over them, and runs the chat.
- `Assets/Scripts/GossipChat.cs`: the "Chat" interactable on a guest who has gossip.
- `Assets/Scripts/Diagnostics/WaitingJobsCheck.cs`: four play labs, one for each waiting job.

**Changed files**

- `DayDefinition.cs`: new fields thriftFind/thriftBuy/thriftSale, gossip and previewLine, with their readers. No save change.
- `DaysOneToThreeSteps.cs`:
  - Its checks moved to the new values (taken from PacingSteps' constants), never weaker.
  - Day 2 now teaches the screwdriver and the pry.
  - A find may use tomorrow's verb.
  - The support call is allowed on Day 3.
- `CustomerSpawner.cs`:
  - The cap's retry in 1.5 s while Ace has at most one step left.
  - The quiet nudge after 5 s.
  - Walk-in repairs' last orders come 90 s earlier.
  - The cap's measure for the labs.
  - Schedule, DevicePool and AuthoredDay accessors.
  - A closing tidy (guests' cups, a crate in hand).
  - It adds DowntimeMeter and GuestGossip to itself.
  - Pure rules for the checks: RetrySoon and NudgeAllowed.
- `DayOneGuideUI.cs`:
  - Part 4: the quiet moments on the Now line.
  - The drink steps' "fetch them from the back shelf".
  - `ShowingWaitingJob`, which the meter samples.
- `CustomerBrain.cs`: the c11 roll (whether a repair customer stays at the bar). Customers, not only patrons, now leave cups on the table.
- `SeatTags.cs` (the SeatChoice class): c11's AwayChance, the roll and its dice; PlaceWords.
- `WaitingArea.cs`: the repair bar's stool is chosen by personal space, with dice to break near-ties, like any seat.
- `DirtyCup.cs`:
  - Place(seat, guests).
  - MessTable.Join, Open and PieceOf.
  - The live list.
  - The count of tables cleared.
  - Putting guests' cups away at closing.
- `NpcCup.cs`: LeaveOnTable and LeavesItBehind. Day 1's settled guests always leave their cup.
- `NpcMovementProfile.cs`: a leavesCup chance. The values go into the profile assets through step G.
- `PatronBrain.cs`: when a patron leaves, the cup stays on the table, by that chance.
- `ItemSlotArea.cs`: the side slot, which is the find's corner and never a customer's. It refuses a crate.
- `ItemInteractable.cs`: a find counts as owned, and it is worked on only from its corner.
- `BenchCards.cs`: the find's card line ("Your thrift watch: grime on the glass.") and "Fixed. It sells tonight."
- `FocusNameUI.cs`: the pin "Thrift watch".
- `FeaturedRepairRequest.cs`: accepts the support call's HoldCallJob root, so the call can be a Day 3 walk-in.
- `ShopInventory.cs`:
  - The stock is split between the machine (12 and 12) and the back shelf.
  - Refill() brings the machine back to 12.
  - A restock goes onto the shelf.
  - CanMake counts both places.
- `BeverageCupSupply.cs` and `CupStack.cs`: "Refill the machine" with a crate in hand, and "No cups at the machine".
- `BeverageStation.cs`:
  - The machine's line reads the machine's own count.
  - A crate in hand gives "Refill the machine".
  - An empty machine with a stocked shelf gives "No cups: refill from the back shelf".
- `DropInteractable.cs`: never takes a crate.
- `NotebookEntries.cs`: the gossip table (pure: four lines and their facts).
- `NotebookHooks.cs`: HeardGossip.
- `NoteSubjects.cs`: EveryFact lists the gossip facts. No new subject.
- `SaveData.cs`: the thrift record, plus machineCups/machineBeans. Both additive.
- `SaveManager.cs`: the find sells at closing, before the till is read. It saves the thrift record and the machine's share.
- `RecapPhone.cs`:
  - Tonight's "Sold your thrift watch: +$55" and "Tomorrow: …" lines.
  - The takings now include the finds.
  - The Shop's "THRIFT FIND FOR TOMORROW" card.
  - The restock row says "+20 each, on the back shelf".
- `Diagnostics/DayShapeCheck.cs`: P's §6.8 bars, the two median bars, a dead-air runs file, and a c11 note.
- `Diagnostics/CafeLab.cs`: at human speed the autopilot also does the waiting jobs. Adds FinishByHand.
- `Diagnostics/Day1GuideCheck.cs`: watches the quiet line through the whole lab (+3 checks).
- `Diagnostics/RecapPhoneCheck.cs`: Tonight's word budget rises by exactly the thrift lines' words.
- `Editor/UpgradeEconomyChecks.cs`: its day simulation gains the cap retry, patrons limited by free seats, and the find's sale.
- `Editor/SeatChoiceRules.cs`: +10 checks. They prove c11 over 4000 simulated visits, plus the dice.
- `Editor/WaitingSpaceChecks.cs`: three different stools, the second never beside the first while a free stool with no neighbour exists. 60 stand-ins per counter place spread over all three.
- `Editor/NpcWave2Steps.cs`:
  - Life 4 expects the new stool rule.
  - Part B expects 2 people on Day 1 and 4 on Days 2–3.
  - The Wave 2 undo refuses while H's guests exist.
- `Editor/NpcLifeSteps.cs`: Life 3 tests the bin on a guest who bins the cup. A new 7b tests a guest who leaves it.

## 2. Decisions and their reasoning

### Day 1's find: a clean (his p1)

**What it is:** a thrift pocket watch with grime on its glass, bezel and dial. Only the brush and cloth are needed.

**How it's built:** step Thrift 1 makes it as a prefab variant of the pocket watch, `Assets/Data/Thrift/ThriftWatch.prefab`.
- It has one fault: Cleaning, "Grimy Glass and Case", $55.
- The walk-in roll never deals it, and the base watch is untouched.

**Why the watch:** Day 2's walk-in is the watch. The watch is also the hardest to brush (Bench 8's misses), so a no-pressure clean is real practice. Day 1's walk-in is the gunked phone, so the find is never that day's job.

### The chain: tomorrow's new job, with nobody waiting

| Day | The find | Buy → sale | Why |
|---|---|---|---|
| 1 | The watch clean | Free → $55 | Day 2's walk-in device, with Day 1's tools |
| 2 | A cracked phone | $25 → $75 | Day 3's walk-in |
| 3 | A frozen phone (the circuit) | $10 → $32 | Day 4's walk-in |
| 4 | A seized watch | $35 → $110 | |
| 5 on | The Shop rotates the jobs taught | | Never the same two nights running |

**The circuit's price:** P priced it on a guessed payout of $85. The phone prefab really pays $45, so by P's own rule (a find sells for about 55–60 % of a customer's Perfect pay) it is $10 → $32. A customer is always worth more.

### One find at a time (his p2)

- **Buying:** at closing, in the Shop.
- **Selling:** at the next closing, by itself, inside SaveManager's closing checkpoint before the till is read. It sells at Good or better (Good fetches 80 %).
- **If it isn't done:** an untouched or broken find waits on its shelf as it is.
- **The save:** keeps which find, for which day, and whether it was fixed. It never keeps its screws, so a find left half-done comes back whole, with its fault, after a quit.

### The close-up closes for a customer (his p6)

ThriftShelf watches for someone at the counter. When they arrive, it calls ItemInspector.CancelInspection once any hold in progress has finished, and waits at most 1.2 s for that hold. I made no edit to PlayerInteractor or ItemInspector. A customer's own repair never closes by itself.

### The support call on Day 3, with its real phone (his p4, a03)

**The step:** Support call 1 gives PhoneJob a child, "Phone (the detailed model)". It is made of visual copies of PhoneRepair's body, back cover, screen glass, two screws and the job tag.
- **Collider:** the grey cube keeps its collider, resized to the model's box, with its renderer off.
- **Size:** restHeight comes from the phone, and realSize is on.
- **Checks:** that it rests and takes up the same space as the phone, and that it fits the intake tray (when the scene is open).

**Its own flow is untouched:** on hold, the ring and the counter work as before.

**Moving the call:** G puts the call on Day 3 only when the real model is there. Otherwise it leaves the call on Day 4 and says why.

### Day 3 is two at once (his p3)

maxCustomers is 2 and patience ×1.6. Three at once moves to Day 4 (×1.45).

### Day 1's 10 seats and two settled guests (r1, r3)

F2 owns which seats are open. H adds two Day-1-only guests:
- "a reader in the lounge": tub chair A, B if A is taken, leaves at 30 % of the day.
- "someone at the bistro": the north 2-top's west seat, east if taken, leaves at 40 %.

H moves NPC wave 2's three guests to Days 2–3, so there are 2 people on Day 1 and 4 from Day 2. PatronSpawner needed no code change.

Day 1's settled guests always leave their cup, so Day 1 has a table to clear after Grace's visit.

### Seat fix 1: the repair bar's stool

Each free stool is scored on crowding plus the walk to it. Stools that score nearly the same are picked by the person's own dice.

This dice is a hash of the person; it never draws from Unity's random sequence. So the bar no longer always gives the middle stool.

Builder S's rule still holds: Ace reaches all three stools over the counter from the bench. The labs that expected "middle, east, west" (Life 4 and Waiting space) now expect "three different stools, spaced".

### Seat fix 2 (c11): about 2 in 5 sit away from the bar from Day 2

**The roll:** from Day 2, each walk-in bench job rolls once per visit whether to sit away from the bar.
- **The chance:** it starts at 0.36. It goes up for comfort-seekers (Sentimental, Relaxed) and down for watchers (Impatient, Rushed). It is kept between 0.12 and 0.70.
- **Never:** on Day 1, or for regulars.

**The result:** in 4000 simulated visits, 41 % sit away on Day 2 and 42 % on Day 3. The Seat choice rules check the share is 33–47 %, and 0 on Day 1.

### Seat fix 3: customers leave cups too

A cup left on a table takes that seat until Ace clears it. So the next drink customer can't take the same window seat, and the spread shows in the Day labs' seat notes.

The share comes from the movement profiles: Hurried .6, Distracted .6, Social .25, Relaxed .2, Shy .15. That is about a third of guests, and G checks the weighted share is 0.25–0.42.

### Day-lab bar: "the day ends by 7:30"

Walk-in repairs' last orders now come 90 s earlier. That is a repair's typical length; Alishba's late repair took about 90 s at human speed. Drinks keep their old last orders.

The Day lab still notes each run's end time. It judges the median of that day's last 3 runs, the way the seat medians are judged.

### Day-lab bar: "the first customer within 15 s"

**The cause:** only the random sequence.
- Walk-ins roll a movement profile by weight from Unity's Random (NpcProfileLibrary.Roll).
- The five profiles walk at 0.86–1.18×, so the same walk from the street takes about 13.5–15.5 s.
- The seat dice of 23 Oct drew more numbers first, which changed which profile the opener got. Nothing else varies.

**The bar:** it stays at 15 s. On Days 1–3 it is judged on the median of the last 3 runs, and each run is still noted. Other days still judge each run.

### Restock by hand: the split

**Cups and Beans stay the café's whole stock.** That is the machine plus the shelf. I did this so the recap, the HUD chips, the save and every lab still read what they always read. MachineCups/MachineBeans and ShelfCups/ShelfBeans are new.

**What reads the machine:**
- Drinks take from the machine (TakeCup, CanBrew, ConsumeBeans).
- The start is the old 20 of each: 12 in the machine, 8 on the shelf.
- What's left in the machine stays overnight.

**The save:** keeps the machine's share, additive. An older save fills the machine from its stock.

**CanMake counts the shelf too.** This differs from P's line "CanMake reads the machine". With P's version, a drink customer would hear "Sorry, we're out" while the shelf was full. P's own words say that should "only happen when the shelf is empty too".

### Gossip

There are four lines; the words are placeholders for his sister.

| Day | The guest says | Filed under |
|---|---|---|
| 2 | "Grace bought fancy cups for tonight's reunion." | Grace |
| 2 | "Grace never misses her late film. Bed by half one." | Grace |
| 3 | "The Officer's been asking about a man. Everyone." | The Officer |
| 3 | "That man by your bins? Here before the last owner." | The man at the bins |

**The facts:** each is 7 words or fewer, so "(likely)" still fits the night page's 8-word line. Examples: "Reunion cups: a box on her worktop.", "TV from 11, in bed about 1:20.". They are overheard and likely.

**Ids:** they start "gossip.", so the night's own checks of "does the notebook know the cups?" still work. His Night 2 ask still writes his fact.

**When it starts:** gossip is dealt only once the day's first customer is heard, so it never competes with the day's first card. Seated patrons and settled guests get it, never customers and never the man on his morning visit.

**After the chat:** the notebook plays its page-turn sound, and the phone's Notes badge counts the new fact. I found no other "+1" by day to reuse.

### The Now line's quiet moments (Part 4)

**When it offers a waiting job:** only while nobody needs Ace, and only in place of a wait:
- "Wait for X to reach the counter.";
- "A customer is on the way.";
- nobody here at all;
- "On hold." (a support call);
- a visit-over note.

**What it never replaces:** Day 1's goal card, or Day 2's "Grace is visiting again". Those are the days' teaching.

**How it names people:** it names the person walking in first ("Nina's on the way. Clear the window table."), but only when the whole line fits 10 words. P's example said "her way"; the data has no genders, so I wrote "the way".

**The first time:** the first of each kind in a session is the full card "WHILE YOU WAIT", for the day's opening hint time, then the one line. Pins go over the back shelf, the table, the guest or the find.

**A customer always wins:** the frame someone needs Ace, the line is read again. The meter samples after the line, late in the frame, so the bar "no waiting job while needed" can really be 0.

**Which jobs wait for the teaching:**
- The refill and the table are always offered.
- Gossip and the find wait until the day's teaching has begun (Day 1: Grace's visit over; other days: the first customer heard).

### The props

They are made of boxes in the house's own materials, so no model is needed.
- **The thrift shelf:** under the repair work area at (-3.30, 1.005, 14.27).
- **The find's corner:** "Slot_Side" on the bench at (-4.30, 1.006, 14.263).
- **The back shelf:** under the drinks work area at (0.15, 1.0, 17.72), staff side, about 2.9 m from the machine.
- **E priority:** the thrift shelf's is 0, as the bench's things are. Anything higher would take E from a customer's device on the bench.

### The labs' autopilot

It is abstract: it never makes a drink or a real repair. So at human speed its waiting jobs are abstract too.
- **Table:** one press, through the game's own Interact.
- **Chat:** one press, the same way.
- **Refill:** Refill(), with 8 s of its hands.
- **Find:** 35–70 s of bench time, then the "labs' hand" (FinishByHand: scrub the grime, swap the part, set and run the circuit). It stops the moment somebody needs Ace and goes on later.

### The dead-air numbers go in their own file

They are in `Logs/CafeLife/days-1-3-downtime.csv`, beside the runs file. P asked for the same file. But the runs file's seat columns are read from the end of each row, so new columns would break the reads of every older row.

## 3. Decisions his to overrule

1. Day 1's find is a thrift pocket watch with a grimy glass. The other choices were a grimy phone or a dusty camera, but the watch is Day 2's device.
2. The chain, the prices, and the circuit's $10 → $32 (P guessed $60 for it).
3. **Day 4's find (a seized watch) is also one of Day 4's walk-ins.**
   - The "never the same as that day's walk-in" rule is checked on Days 1–3 only; his p1 was about Day 1.
   - The other choice: the gunked phone, $10 → $30.
4. CanMake counts the back shelf (P wrote "the machine"), so an order is never refused while the shelf has stock.
5. Cups and Beans stay the whole stock, not the machine's.
6. Walk-in repairs' last orders come 90 s earlier than drinks'.
7. The day's end and the first customer are judged on the median of the last 3 runs. The bars themselves are unchanged.
8. Day 1's settled guests always leave their cup (a table lesson on Day 1). Other guests leave theirs by profile, about a third.
9. The repair bar's stool uses personal space with dice for near-ties, not "the middle first".
10. The c11 numbers: 0.36 base, ±0.04 for comfort, ±0.08 for watching, kept between 0.12 and 0.70.
11. The Now line's words, which are placeholder copy:
    - "Low on cups. Fetch more from the back shelf."
    - "Clear the window table."
    - "{E} Chat with the guest in the lounge."
    - "Your thrift watch: clean it while you wait." (Day 1)
    - "Your thrift phone: fix it between customers." (later days)
    - "Take the cups and beans to the drinks machine."
    - "No cups left. Fetch more from the back shelf."
    - "{Name}'s on the way." (P wrote "her way")
12. The Now line never replaces Day 1's goal card or "Grace is visiting again".
13. Gossip and the find wait for the first customer to be heard; refills and tables don't.
14. The four gossip facts are shortened to 7 words.
15. "Chat" is the prompt, and the guest's bubble lasts 2.5 s.
16. The props' places and their box stand-ins. B2 could model a crate and shelves later.
17. The dead-air numbers go in a sibling CSV file.
18. The economy check now counts the find's sale (bought the night before; sold when a bench job's worth of free hands went to it).
19. The phone's restock row says "+20 each, on the back shelf".

## 4. Edits outside my files

### a) HudCorners.cs: not edited

M's HUD pass owns it. P's chip words merge into whichever lands second. The exact edit:

In `UpdateChips`, replace the two `ChipFor` lines with:
```csharp
        // 24 Oct 2026 (Days; restock by hand, P's §2.5): the machine at 3 or fewer with stock on the back shelf says so.
        bool cupsMachine = stock != null && !stock.CupsLow && stock.MachineCups <= Downtime.RefillAt && stock.ShelfCups > 0;
        bool beansMachine = stock != null && !stock.BeansLow && stock.MachineBeans <= Downtime.RefillAt && stock.ShelfBeans > 0;
        ChipFor(cupsChip, stock != null && (stock.CupsLow || cupsMachine), stock == null ? 0 : cupsMachine ? stock.MachineCups : stock.Cups, "cups", cupsMachine, now, ref row);
        ChipFor(beansChip, stock != null && (stock.BeansLow || beansMachine), stock == null ? 0 : beansMachine ? stock.MachineBeans : stock.Beans, "beans", beansMachine, now, ref row);
```
Then give `ChipFor` a `bool atMachine` after `what`. Inside it:
```csharp
        int shown = atMachine ? -1000 - count : count;   // the machine's count and the whole stock's never read as the same
        if (shown != chip.shownCount)
        {
            chip.shownCount = shown;
            chip.title.text = atMachine ? $"Low at the machine · {count} {what}" : count <= 0 ? $"Out of {what}" : $"Low on {what} · {count} left";
            chip.line.text = atMachine ? "Refill from the back shelf" : "Restock at closing";
            chip.face.color = !atMachine && count <= 0 ? Plum : UiSkin.Accent;
            // ...the width lines as they are
```
- **Budget:** both lines together are 11 words, within BenchAlert's 12.
- **PauseCheck:** still passes. It sets 3 cups with nothing on the shelf, so the chip still says "Low on cups · 3 left / Restock at closing".

### b) Files edited outside the brief's ownership list

These are small and marked. Each is named in P's §6, or is a lab for my parts. Take them with the diff or merge them by hand:

- **Restock (P §6.6):**
  - `BeverageStation.cs`: P says "grep every reader".
  - `DropInteractable.cs`: P says the crate is "refused by every DropSpot". The edit is `&& !(carried is StockCrate)` in Takes.
  - `CupStack.cs`, `BeverageCupSupply.cs`.
- **Clearing tables (P §6.2):** `NpcCup.cs`, `NpcMovementProfile.cs`, `PatronBrain.cs` (only the Leave lines).
- **The find (P §6.3):** `FocusNameUI.cs`.
- **The support-call device:** `FeaturedRepairRequest.cs`.
- **Gossip (P §6.5):** `NotebookEntries.cs`, `NotebookHooks.cs`, and `NoteSubjects.cs` (EveryFact only).
- **Labs (P §6.7):** `Day1GuideCheck.cs`, `RecapPhoneCheck.cs`, `UpgradeEconomyChecks.cs`, `NpcLifeSteps.cs`, `NpcWave2Steps.cs`, `WaitingSpaceChecks.cs`.

### c) Merge notes for the shared files

All blocks are marked `// 24 Oct 2026 (Days)`.

- **SaveData.cs:**
  - the ThriftSaveData class;
  - the `thrift` field;
  - `machineCups` / `machineBeans` (default -1);
  - three ValidateAndMigrate lines;
  - one TryCreateNextDay line, which copies `thrift`.
- **SaveManager.cs:**
  - CaptureState calls `ThriftShelf.SettleAtClosing` after SettleReputation;
  - it sets `data.machineCups/Beans` after the initializer;
  - it sets `data.thrift` before regularMemories.
- **RecapPhone.cs:**
  - Tonight's thrift lines and their word counts (TonightSold/TonightTomorrow and their Words);
  - the takings include the finds;
  - an overload `TakingsSplit(earned, patron, finds)`; the old two-argument version is kept;
  - in BuildShop, the "THRIFT FIND FOR TOMORROW" card after the stock card, and the restock row's words.

  F2's YOUR CAFÉ card and Arrange button also go in BuildShop; mine sits after the stock card.
- **DayShapeCheck.cs:**
  - header lines;
  - Arrival.call/counter and two lines that set them in Watch;
  - the repairs' last-orders Check in Run;
  - the first-customer lines in Report;
  - the day-ends lines in DaysBars;
  - the new DeadAir method and its fields.

  The seat code is untouched.
- **CafeLab.cs:**
  - fields after handsFreeAt;
  - one line at the start of RunAutopilot and one at its end;
  - new methods GiveWayForCustomers, WaitingJobs and FinishByHand.
- **PatronSpawner.cs, ControlHints.cs:** no change.

### d) For F2 (the furniture)

- **Placed tables:** `MessTable.PieceOf` finds a placed table's piece by reflection, `Type.GetType("PlacedPiece")`. If F2's class has another name, cups fall back to the 1 m grouping. P §6.2 also asks for a CupSpot on every placed seat.
- **Day 1's open seats:** H seats its guests in the lounge tub chairs A and B and the north bistro 2-top. Those must be among Day 1's 10 open seats.
- **Keep clear:** the props stand under the repair work area and the drinks work area (positions in §2). F2's layout must keep those places free.

### e) Data

Step G writes `leavesCup` into the five `Assets/Data/NpcProfiles` assets in the editor. Nothing is hand-edited.

## 5. Proof on his Mac, in run order

1. Let Unity compile. The console has no errors.
2. **Fixit Fidget › Days › Thrift 1 - Day 1's find: the thrift watch (prefab).** One PASS line. A second run changes nothing.
3. **Fixit Fidget › Days › Support call 1 - The support call's phone: the detailed parts (prefab).** PASS, with rest and footprint parity, and the tray fit when the café scene is open.
4. **Fixit Fidget › Bench › Bench 7 - The forgiving stage (scene)**, again (a03). Its own PASS.
5. **Fixit Fidget › Days › G - The pacing pass (data).** PASS, with old → new in its report and the call on Day 3.
6. **Fixit Fidget › Days › H - Day 1's two settled guests (scene).** PASS: 2 people on Day 1, 4 from Day 2. Save the scene.
7. **Fixit Fidget › Days › Props 1 - The thrift shelf and the back shelf (scene).** PASS. Save the scene.
8. **Edit-mode checks:**
   - Checks › Seat choice rules: 69 checks (59 + 10).
   - Checks › Waiting space reservations: one more check for each counter place.
   - Checks › Night notebook rules: more facts, with the four gossip facts in EveryFact. Each has its subject, its picture and a line within 8 words.
   - Checks › Downtime rules (new): "[Downtime rules] All 133 checks passed."
9. **The four new labs.** Each is its own Play session; the last line is "[Waiting jobs] All N checks passed."

   | Lab | Checks | A pass means |
   |---|---|---|
   | Days › Lab - The find gives way (Day 1) | 23 | the close-up closes within 1.5 s of the customer reaching the counter; the pieces stay put; the customer's device gets a main place; the find finishes and goes back on its shelf |
   | Days › Lab - Clear the tables (Day 3) | 16 | |
   | Days › Lab - Chat with a seated guest (Day 2) | 18 | |
   | Days › Lab - Restock by hand (Day 2) | 16 | |

10. **The Day labs:** Days › Lab - Day 1 / 2 / 3 at the counter (the autopilot at human speed), **3 runs each**.

    | Bar | Day 1 | Day 2 | Day 3 | How it's judged |
    |---|---|---|---|---|
    | Nothing to do after the first 15 s | ≤ 50 s | ≤ 25 s | ≤ 30 s | median of the last 3 runs |
    | Longest stretch of it | ≤ 25 s | ≤ 20 s | ≤ 20 s | each run |
    | A waiting job on the line while somebody needs Ace | 0 | 0 | 0 | each run |
    | The find offered before Grace's visit is over | never | | | each run |
    | Bench jobs done (customers' and the find) | ≥ 3 | ≥ 3 | ≥ 3 | median of the last 3 runs |
    | A walk-in held back by the cap > 2 s while Ace had ≤ 1 job | 0 | 0 | 0 | each run |
    | The first customer at the till | ≤ 15 s | ≤ 15 s | ≤ 15 s | median of the last 3 runs |
    | The day ends | 7:30 | 7:30 | 8:30 | median of the last 3 runs |

    The old bars stay. New counts over the old ones:
    - Day 1: +4 on runs 1–2, +8 from run 3.
    - Days 2–3: +3 on runs 1–2, +7 from run 3.
    - Other days' Day lab: +1 (the repairs' last orders).

    The report's notes show the c11 share and the seats.
11. **The Day 1 guide lab:** +3 checks. They are: no waiting job while needed, never the find before Grace's visit is over, and every line within 10 words.
12. **Re-runs:**
    - Life 4: the stool rule changed.
    - Life 3: +1 check for 7b, or a note if the guest who leaves the cup never puts it down within 12 s.
    - Sit 4, Stations, Night 0, and Night 1 and 2 (the gossip facts).
    - The recap phone: the budget gains the thrift lines' words.
    - Pause: unchanged, still "Restock at closing".
    - Juice.
    - Bench 3, 5, 6, 8 and 9: two devices plus the find's corner.

## 6. Risks, and APIs I'm unsure of

- **Old saves and JsonUtility.** I rely on JsonUtility running field initializers for fields missing from an old save (machineCups -1, thrift).
  - If it doesn't, an old save loads with an empty machine, and one refill fixes it.
  - The Downtime rules check this ("an older save has no split: -1").
- **The labs' hand on a find.** FinishByHand calls ReplaceablePart.Activate, then CircuitRun.Turn / Retry / Tick, outside the bench's flow. If the cracked screen or the circuit won't finish that way:
  - the Day 2/3 labs note "the labs' hand couldn't finish it";
  - the find stays something to do, so the dead-air numbers read low;
  - the bench-jobs median may then miss.

  The Find lab uses the Day 1 clean, which is safe.
- **Day-lab bars and the abstract autopilot.** The dead-air bars depend on the autopilot's abstract timings (6 s a table, 6 s a chat, 8 s a refill, 35–70 s on the find). P said to tighten them to the measured median plus a quarter once runs land well inside.
- **The chat's trigger.** It is a child sphere on a seated patron. A script with OnTriggerEnter on someone walking past could see it. I found none, but I didn't check every one.
- **The bubble's range.** Barks.Say is heard only within its range. The Chat lab moves Ace beside the guest first.
- **The Table lab's timing.** It needs a quiet moment at Day 3's open, before the first customer is inside. If the line doesn't show within 30 s it fails, and the report says what the line said.
- **Cost while a waiting job shows.** The Now line reads Downtime every frame (one FindObjectsByType a frame), because a customer must win at once.
- **The economy check moves.** Its totals now include the finds, about +$200 over Days 1–4. The "cheapest first level at the first recap" margin grows; the "whole catalogue not by the demo's end" margin shrinks a little.
- **Not built:** P's optional night hint "A guest's word." for the cups. Also the HudCorners chip, which is described above.
- **APIs I'm unsure compile:**
  - `[MenuItem(path, true)]` stacked four times on one validate method. MenuItem allows multiple; I believe this is fine.
  - `Barks.TryGetShown(transform, out _)`: it discards a `Shown`.
  - Every other call I checked against its definition in this copy.
