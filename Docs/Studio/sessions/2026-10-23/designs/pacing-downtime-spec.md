# Days 1–3 that teach, and things to do while waiting

*Designer P (Progression & Economy, with the Narrative & Game Design director's hat), 23 Oct 2026. A spec for his approval: nothing is built. Code read at `8033112` in `/root/wt23/P` (read-only). Every number comes from one simulation, re-run at the end (§0.3). Where this touches the furniture spec, it says **F must agree**.*

## On your phone

**Dead air is mostly a walk.** On today's Day 1 nobody needs you for 151 of 420 seconds; for 127 of them a customer is walking in. Things to do fill the walk: your own answer.

**The plan**
- **Two waiting jobs a day.** Day 1: your side project (a thrift pocket watch: the screwdriver a day early, nobody waiting) and clearing tables. Day 2: chatting with seated guests (gossip about tonight's errand) and refilling the machine.
- **A new repair every day**, never yesterday's walk-in. Bench jobs a day: 2/2/3 → 3/3/4 (your find counts).
- **The spawner stops losing a whole gap** when the café is full and you're nearly free.
- **Days 4–5 eased into a ramp.**

**Simulated first-timer, today → plan**
- Nothing to do: Day 1 151 → 48 s, Day 2 133 → 17 s, Day 3 106 → 32 s.
- Nobody lost on Days 1–2. Day 4's 7 walk-outs → under 1.
- Takings about $333, $429, $679. A star at the first recap, a second at the next (82 %).
- Your café: half-finished Monday, whole by Friday, a piece a night.

**Only one for playtest 8?** The side project: alone it takes Day 1 from 151 to 73 s; any other alone, 114–127 s. It's the bench, and it pays at closing.

**Yours to decide:** Day 1's watch, buying finds, Day 3 at two at once, the mortgage as story for now, and (with F) the 10-seat Day 1.

---

## 0. What I read, what I found, how the numbers were made

### 0.1 Read

- The brief (`/root/review23/specs/P-pacing-downtime.md`), `builders-common.md`, his notes and answers (`his-notes-2026-10-22.md`: his answers win), `plan.md`, F's brief (`specs/F-cafe-grows.md`), S's brief (`specs/S-seats.md`), the art audit's R2 (`findings/art-R2.md:6`, the support call's phone), M's HUD mockup notes (`mockups/README.md`).
- `CLAUDE.md`; the Bible §1, §2, §4, §4.1, §6; the last session (`Docs/Studio/sessions/2026-10-20.md`); the 14 Oct days spec and its build report (`Docs/Studio/sessions/2026-10-14/specs/days-1-3.md`, `reports/days.md`); the 19 Oct "phone-days" finding.
- Code: `CustomerSpawner`, `CafeArrivals`, `DayClock`, `DayDefinition`, `DayShapeCheck`, `DayOneGuideUI`, `DirtyCup`/`MessTable`, `LodgerDay`, `TableSeat`, `WaitingArea`, `PatronSpawner`, `PatronBrain`, `NpcCup`, `ShopInventory`, `CupStack`, `BeverageCupSupply`, `PlayerCarry`, `RepairJob`, `JobBase`, `ItemInteractable`, `StationInteractable`, `ItemSlotArea`, `BenchStage`, `BenchCards`, `BenchLab`, `CustomerBrain` (patience, reassure, departure), `ReputationRules`, `RecapPhone` (Tonight, Shop), `HudCorners` (stock chips), `NotebookHooks`, `NotebookEntries`, `SaveData`, `NightThings`, `LodgerStory`, `GraceAtHome`, `HoldCallJob`, `ControlHints`, `WordBudget`, `DayIntro` (the day lines); editor: `DaysOneToThreeSteps`, `UpgradeEconomyPass`, `UpgradeEconomyChecks`, `DrinkMenuSetup`, `FaultExpansionSetup`, `CircuitIntegrationChecks`, `HumanIntegrationChecks`.
- Data: `Assets/Data/Days/Day_03_TheRealGame`, `Day_04_Pressure`, `Day_05_TwoRushes`; the NPC movement profiles.
- Market: two web searches (§Sources), besides the plan's 14-minute demo and the shop-builder sims.

### 0.2 Eight facts the brief didn't have

1. **Which Day 1 and Day 2 the game plays.** The scene schedules `Assets/GraceShowcase/Day_01_GraceCamera` and `Day_02_GraceReturn` (days spec §1). My copy has no `GraceShowcase` folder, so their live values are what `DaysOneToThreeSteps.cs:83-88` and `:497-570` write: Day 1 Calm 75 s / Build 30 / Rush 24 / Recover 30, two walk-ins at once (`:530`), patience ×2.0. `Data/Days/Day_01_LearnTheShop` and `Day_02_Juggle` (Calm 26 s, three at once) are not scheduled. The brief's numbers are right.
2. **A full café loses a whole gap.** `CustomerSpawner.cs:377` sets the next gap *before* the cap check at `:427-431`, and only the guided opening retries early. So once the café is at today's limit, the next walk-in waits a whole gap (75 s in Day 1's calm) after a place frees. A full counter, by contrast, retries in 1.5 s (`:446-451`).
3. **The dead air is the walk.** 127 of Day 1's 151 idle seconds have a customer already on the way (§3.3). The walk from the street is 25–61 s (`CustomerSpawner.cs:83-87`), median about 38.
4. **The Now line tells you to wait.** "Wait for {name} to reach the counter." (`DayOneGuideUI.cs:515`), "A customer is on the way." (`:503`), and after Grace "Grace's visit is over. Closing time brings the recap." (`:687-690`). With nobody in, it shows nothing (`:339-362`). The 19 Oct review found "Wait for Grace to reach the counter." was the only instruction on screen (`phone-days.md:5`); playtest 7: "Yeah, but you should have like an object[ive]." (`DayOneGuideUI.cs:13-15`).
5. **A job with no customer can't be touched today.** `ItemInteractable.OnBench` needs an owner (`ItemInteractable.cs:20-21`), and an owner-less device can't be picked up (`:47`). The bench still handles one: `BenchLab.cs:126-145` puts a customer-less phone on the bench and repairs it, and `CircuitIntegrationChecks.cs:11-45` spawns a "Practice circuit phone (no customer)".
6. **The bench holds one device, but its stage can hold two jobs.** "Bigger Bench opens two devices at once" (`RecapPhone.cs:1527`). The close-up's stage already keeps two jobs' pieces apart: another job's pieces never lie under the device in view (`BenchStage.cs:233-238`), and the stock of the job opened longest ago is put away until its close-up reopens (`:287-288`).
7. **The first star already comes on Day 1.** Stars arrive at 5, 30, 120, 300 and 580 reputation; "Loved it" is +2 (`ReputationRules.cs:9-12`, `:94`). Three happy reviews make ★1.
8. **Night 3 has no errand.** The cones favour isn't in the game, so Night 3 plays "Nothing tonight" (days spec §4.3; `LodgerStory.cs:185-191`). Day 3's gossip can feed the story, not an errand.

### 0.3 How the numbers were made

**A day simulation** (Python; scratch work, not repo code). It ports:
- **The spawner:** gaps read at "now + the walk", the cap and its whole-gap quirk, the counter's three places, last orders, named visits, the walk-in list.
- **Patrons:** one every 12 s; none when free seats minus those walking in fall to 3; $5 at the door (`PatronSpawner.cs:142-188`, `:453-468`).
- **Patience:** queue 40 s, service 45 s; seated ×0.6, standing ×1.15, talking ×0.1, drinking ×0.5 then ×0.8; the day's multiplier (days spec §4.0).
- **Reassure** (+30 % of max, ×0.6 a use, 3 at most, 12 s apart, `CustomerBrain.cs:30-34`, `:2194-2205`).
- **Tips, pay, reviews and stars:** `UpgradeEconomyChecks.cs:783-795`, `JobBase.cs:61-67`, `ReputationRules.cs:107-147`.

**The player** has one pair of hands. They hear the counter first, then reassure anyone with the sweat drop, then do the most urgent job. When nobody needs them, they take the waiting jobs in the Now line's order (§2.1).

**Times:**
- **Bench:** Systems' first-timer p50/p90 (days spec §4.0: Grace's clean 25 s, the shutter 15 s, the phone's gunk 25 s, its screen 43 s, the watch's grit 59 s, its seized movement 60 s).
- **Learning:** Ace gets faster day by day (×1.0, .95, .9, .8, .75, .72, .7 of those times), toward Systems' "steady" times (about ×0.62).
- **Slow player:** every one of Ace's times ×1.5.

**Calibration:** his Day 1 had 7 customers and $354; the model's today gives 7 and $346.

**Limits:**
- The model never declines a job. That makes it pessimistic at high load: today's Day 4 loses 7.2 in the model, while he lost 3 on his own Day 4.
- Every repair is Perfect.
- The circuit's time, and the payouts of the circuit ($85) and the support call ($35), are my guesses: those prefabs aren't in my copy.
- The lab's autopilot will behave differently.

Read the model as a comparison, not a promise. The labs and playtest 8 measure the real thing.

**Runs:** 150–500 days per line, 200 weeks per week line, fixed seeds.
- **The final re-run** (`verify_final.py`) recomputed every number that depends on the proposal. It caught two places where the model differed from this spec: Day 3 had three gossip guests, and Day 4's drink-only share was .30. Both now match the spec (two lines; .28, unchanged), and every Day 3–7 number here is the re-run's. Days 1–2 came out identical.
- **Scripts and outputs**, in `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/sim/`: the model (`daysim.py`, `today.py`, `final_cfg.py`, `final.py`, `week_ladder.py`); today's days and the Day 1–2 tables (`final_out_copy.txt`); the re-run (`verify_final_log.txt`); the arrival rules and the slow player (`retry_check_log.txt`, `retry_check2_log.txt`).

---

## 1. The shape of Days 1–3

### 1.1 The rules I designed to

1. **Days 1 and 2 teach; Day 3 is the first real day** (his answer).
2. **More repairs than today.** Never the same walk-in repair two days running (Days 1–3).
3. **Overlap grows day by day, with no cliff into Day 4.**
4. **The locks stay:** Grace's Day 1 latte is suppressed; her camera is cleaned on Day 1 and swapped on Day 2.
5. **One new bench job a day.** Each day's thrift find is tomorrow's new job, with nobody waiting (my rule; §2.2).
6. **Two waiting jobs are taught a day:** one line each, in a quiet moment, never two at once.
7. **The support call (`PhoneJob`) only once it has its real phone model** (art audit R2: today it is "a default-grey Unity cube", `findings/art-R2.md:6`).

How to read the tables:
- **Clock** is real minutes from the opening (7:00 is closing), the median of 200 simulated weeks of the steady player (§5).
- **Now line** is the top-left objective line: at most 10 words (`WordBudget.cs:34`), the button as its icon. `{E}` is `ControlHints.Interact`; the bench's own card guides at the bench (`DayOneGuideUI.cs:654-656`).
- Lines marked **NEW** are mine, placeholder copy for his sister. All others are today's.

### 1.2 Day 1, Monday: "Fix what they bring. Pour what they order. Make the rent." (`DayIntro.cs:988`, unchanged)

**Teaches:**
- the counter;
- Grace's clean (cloth, brush);
- the hand-back;
- the first drink (the Officer);
- a walk-in's repair (the gunked phone);
- your side project (the screwdriver, optional, nobody waiting);
- clearing a table.

**New job:** cleaning. **The café:** half-finished, 10 seats (§4.2, **F must agree**).

| Clock | What happens | Now line |
|---|---|---|
| 0:00 | Open. The counter, the bench, the drinks machine, the repair bar (3 stools), the lounge's 2 tub chairs, the window counter (3 stools), one bistro 2-top; the rest under dust sheets. Two settled guests. On the thrift shelf by the bench: a pocket watch that won't tick. | The first card, unchanged (`DayOneGuideUI.cs:680-685`): "Five stars and the café can franchise. Grace is on her way."; its line: "Grace is on her way." |
| 0:12 | Grace at the till: the camera, "just a clean". | "{E} Talk to Grace." (unchanged) |
| 0:12–1:19 | Her job: carry, cloth, brush, hand back. She watches from the repair bar. No latte (the lock). | the existing carry and bench steps |
| 1:19 | She leaves happy. The Officer is already walking over. | **NEW:** "Your thrift watch: fix it while you wait." (8 words) |
| 1:25 | Ace starts the watch: four screws, the grit, the crystal. The bench's card guides each step. | (the bench's card) |
| 1:54 | The Officer at the till. The watch's close-up closes by itself; the watch waits on its own corner of the bench. | "{E} Talk to the Officer." (unchanged) |
| 2:25–2:37 | The first walk-in (a drink), then the Officer's coffee: the first drink lesson. | the existing drink steps |
| 3:08 | A guest got up and left a cup: the table has the used-cup badge. | **NEW** (the first time, as a full card titled "WHILE YOU WAIT"): "Clear the table by the window." (6 words) |
| 3:23 | The walk-in phone, gunked (97 % of days). | the existing steps |
| 4:58 | The watch is fixed (88 % of days): "Fixed. It sells tonight." It goes back on the thrift shelf with a SOLD TONIGHT tag. | — |
| 6:21 | Last orders for walk-ins. | — |
| 7:00 | Closing. | "Closing time: serve who's inside." (unchanged) |
| Recap | Tonight: the takings. **NEW:** "Sold your thrift watch: +$55" and "Tomorrow: Grace again, and a customer's pocket watch." ★1 (100 % of days). Shop: the first furniture (F), a tool, tomorrow's find (a phone with a cracked screen, $25). | — |

**Day 1, in numbers:**
- 8 at the till;
- 3 bench jobs (Grace, the phone, your watch);
- nobody lost;
- $333 (lowest quarter $320);
- nothing to do 48 s: 12 s of it is the opening walk under the goal card, 12 s comes after last orders.

**What makes him press on:**
- the first sale;
- the first star;
- a table to place;
- the bins.

### 1.3 Day 2, Tuesday: "Grace comes in early. The coffee won't wait for the bench." (`DayIntro.cs:989`, unchanged)

**Teaches:**
- the swap (Grace's shutter: tweezers, one hold);
- the juggle (her latte);
- the screwdriver on a customer's watch;
- chatting with a seated guest;
- refilling the machine from the back shelf.

**New job:** the swap, and the watch at the counter.

| Clock | What happens | Now line |
|---|---|---|
| 0:00 | Open with 12 seats if a set was bought. On the shelf: the cracked phone. Four settled guests. The machine has what's left from yesterday (about 5 cups). | "Grace is visiting again. Talk at the counter." (unchanged) |
| 0:12–1:10 | Grace: the shutter, "the reunion's tonight". Handed back at 1:10. | the existing steps |
| 1:30 | Her latte served. | the existing drink steps |
| 1:39 | The first walk-in (the lab's bar is no sooner than 1:30). A seated guest shows "…": they have gossip. | **NEW:** "{E} Chat with the guest in the lounge." (7 words) |
| 1:39 | The chat: "Grace bought fancy cups for tonight's reunion." Into the notebook. | — |
| 1:54 | The watch with grit inside: a customer's now, and the screwdriver again if Ace did yesterday's find. | the existing steps |
| 3:01 | A table to clear. | "Clear the table by the window." |
| 3:39 | Ace starts the cracked phone (two screws, the cover, the swap): tomorrow's job. | "Your thrift phone: fix it between customers." (7 words) |
| 4:09 | The machine is down to 3 cups. | **NEW:** "Low on cups. Fetch more from the back shelf." (9 words) |
| 6:15 | The phone fixed and sold tonight (60 % of days; otherwise it waits for tomorrow). | — |
| Recap | ★★ (82 % of days). **NEW:** "Tomorrow: a full house, and a cracked screen." The Shop: "Low on cups" (restock $30), tomorrow's find (a phone frozen on its start-up screen, $20). | — |
| Night 2 | The man asks for her reunion cups: the notebook already has them, a guest's word. The second gossip ("Grace never misses her late film. Bed by half one.") gives "Go in once she's asleep" a time. This is true in the game: TV from 11 PM, bed at 1:20, lamp off 1:40 (`GraceAtHome.cs:15-18`). | — |

**Day 2, in numbers:**
- 7 at the till;
- 3 bench jobs;
- 0.01 lost;
- $429;
- nothing to do 17 s, 12 s of it at the opening.

### 1.4 Day 3, Wednesday: "Full house today. Some of them are in a hurry." (`DayIntro.cs:990`, unchanged)

**The first real day.** No new verb at the counter except the support call, and only if its real model is in (§1.1 rule 7). Four walk-in repairs; Impatient customers; the Officer's question. The find rehearses tomorrow's circuit, with nobody waiting.

| Clock | What happens | Now line |
|---|---|---|
| 0:12 | Grace: her latte and the photo's news (unchanged, days spec §4.3). Served 0:45. | the existing lines |
| 0:53 | Ace starts the thrift find: a phone frozen on its start-up screen (the circuit, tomorrow's new job). Fixed by 2:39 on 76 % of days. | "Your thrift phone: fix it between customers." |
| 1:01 | Gossip: "The Officer's been asking about a man. Everyone." | "{E} Chat with the guest by the window." (7 words; the long table is still under its dust sheet) |
| 1:14–1:21 | The first walk-in: the cracked screen (yesterday's find, now with a customer waiting). | — |
| 1:52 | The Officer: "Have you seen a man of this description?" (Say nothing). | — |
| through the day | The seized watch (everything: the hardest bench job), the gunked phone again (Day 1's, not Day 2's), and the support call if its model is in: "On hold. Work on another job until the phone rings." (`DayOneGuideUI.cs:624`: the hold is the waiting lesson). | — |
| 3:11 | Tables to clear. | — |
| 5:13 | A refill (84 % of days). | — |
| Recap | **NEW:** "Tomorrow: a frozen phone, and three at once." Tomorrow's find: a seized watch ($35). | — |

**Day 3, in numbers:**
- 7 at the till;
- 4 bench jobs;
- 0.09 lost;
- $679;
- nothing to do 32 s, 12 s of it at the opening.

Without the support call, a separate batch of 150 days loses the same (0.08) and earns $27 less ($626 against $653).

### 1.5 The data, old → new (one editor step writes it; §6, part 1)

| Asset | Field | Today | Proposed | Why |
|---|---|---|---|---|
| **Day 1** `GraceShowcase/Day_01_GraceCamera` | phases, cap, patience | Calm 75 · Build .25: 30 · Rush .55: 24 · Recover .80: 30; 2 at once; ×2.0 | unchanged | Protects the first repair (days spec §2 call 4); the cap fix (§3) does the rest |
| | `drinkOnlyChance` | .70 | **.55** | The walk-in phone comes sooner (3:23) |
| | walk-in repairs | [phone · Gunked Up] | unchanged | Cleaning only on Day 1 |
| | **new** `thriftFind` | — | **PocketWatch · 0 (Debris Inside)**, free | Tomorrow's job with nobody waiting |
| **Day 2** `GraceShowcase/Day_02_GraceReturn` | phases | Calm 60 · Build .25: 24 · Rush .50: 18 · Recover .80: 26 | Calm 60 · Build **.22: 26** · Rush **.45: 20** · Recover .80: **28** | A longer walk-in job (the watch) needs a little more room |
| | `patienceMultiplier` | ×1.4 | **×1.8** | A teaching day shouldn't lose a slow player: they lose 0.09, against 0.27 at ×1.6 and 0.64 at today's ×1.4 (500 days each). The first-timer loses 0.01 |
| | `drinkOnlyChance` | .70 | **.45** | More repairs, fewer plain coffees (plan §8) |
| | walk-in repairs | [phone · Gunked Up] | **[PocketWatch · 0 (Debris Inside)]** | Never Day 1's walk-in again; the new device at the counter |
| | **new** `gossip` | — | **2 lines** (§2.4) | The chat lesson; feeds Night 2 |
| **Day 3** `Data/Days/Day_03_TheRealGame` | phases | Calm 35 · Build .20: 22 · Rush .45: 18 · Recover .72: 26 | Calm 35 · Build .20: **20** · Rush .45: **16** · Recover **.75: 24** | The first real day is busier |
| | `maxCustomers`, patience | 2; ×1.6 | unchanged | Three at once cost the slow player 2.3 losses against today's 0.9; two at once, 0.9 (§3.4) |
| | `drinkOnlyChance` | .45 | **.35** | More repairs |
| | walk-in repairs | [watch · Debris, phone · Screen, watch · Seized] | **[phone · Cracked Screen, watch · Seized Movement, phone · Gunked Up, + the support call if its model is real]** | Not Day 2's watch; the screen was Day 2's find |
| | **new** `gossip` | — | **2 lines** | The story (Night 3 has no errand) |
| **Day 4** `Day_04_Pressure` | phases | Calm 42 · Build .20: 28 · Rush .42: 16.3 · Recover .75: 37.3 | Calm **36** · Build .20: **20** · Rush .42: **15** · Recover .75: **26** | A real step up from Day 3 |
| | `maxCustomers`; patience | 6; ×1.0 | **3; ×1.45** | Today's Day 4 loses 7.2 in the model (he lost 3 of 12); this loses 0.8 |
| | walk-in repairs | [] (any fault at random) | **[phone · Frozen Boot (the circuit), watch · Seized, watch · Debris, phone · the Human fault]** | Opens on the day's new jobs |
| | **new** `gossip` | — | **2 lines (to write)** | Keeps the chat alive after Day 3. Night 4 is a night off (`LodgerStory.cs:109`), so they feed the story |
| **Day 5** `Day_05_TwoRushes` | phases | Opening 37.3 · Morning rush .15: 16.3 · Lull .38: 60.7 · Lunch rush .58: 14 · Last orders .82: 35 | 37.3 · **.15: 18** · **.38: 50** · **.58: 16** · **.82: 32** | Keeps its two rushes, survivable |
| | `maxCustomers`; patience | 6; ×1.0 | **3; ×1.3** | 1.0 lost (today 9.0). It repeats for every later day, so **Days 6–7 need their own assets** after playtest 8 (§7) |
| | **new** `gossip` | — | **2 lines (to write)** | As Day 4 |

The finds the Shop sells for each next day: Day 2 a cracked screen, Day 3 the circuit, Day 4 a seized watch (§5.2).

### 1.6 The curve, Days 1–7

The simulated first-timer, today against the plan (the steady player, who buys a set of seats and an upgrade a night). "Needing Ace ≥ 2" is the seconds a day with two or more customer jobs waiting on him: the overlap.

| Day | Today: at the till / lost / takings / nothing to do / overlap | Plan: at the till / bench jobs / lost / takings / nothing to do (p90) / overlap |
|---|---|---|
| 1 | 7 / 0.00 / $346 / 151 s / 27 s | 8 / 3 / 0.00 / $333 / 48 s (77) / 25 s |
| 2 | 8 / 0.00 / $395 / 133 s / 72 s | 7 / 3 / 0.01 / $429 / 17 s (52) / 107 s |
| 3 | 7 / 0.07 / $688 / 106 s / 160 s | 7 / 4 / 0.09 / $679 / 32 s (71) / 168 s |
| 4 | 12 / **7.22** / $479 / 51 s / 336 s | 8 / 4 / 0.81 / $660 / 12 s (30) / 221 s |
| 5 | 13 / **8.95** / $432 / 41 s / 387 s | 8 / 6 / 0.97 / $817 / 12 s (52) / 255 s |
| 6 | — | 8 / 6 / 0.81 / $847 / 12 s (55) / 239 s |
| 7 | — | 9 / 6 / 0.64 / $883 / 12 s (57) / 248 s |

- **Today:** flat for three days (a third of Day 1 and Day 2 with nobody needing you: 35 % and 31 %), then the Day 4 cliff.
- **The plan:** the overlap rises every day to Day 5 (25 → 107 → 168 → 221 → 255 s) and the losses rise gently (0 → 0 → 0.1 → 0.8 → 1.0). Repairs go 3 → 3 → 4 → 4 → 6.
- **Takings:** they rise to Day 3, hold through Day 4 (−$19), and rise again from Day 5.
- **From Day 4**, the 12 s left is the opening walk.
- **The slow player** (every one of Ace's times ×1.5, 500 days a day): lost 0 / 0.1 / 0.9 / 2.8 / 3.4 on Days 1–5, against today's 0 / 0.0 / 0.9 / 9.2 / 11.0 (300 days a day). The cliff is gone, but Day 4 is still a step for them (§7.1).

---

## 2. No dead air: the four things to do while waiting

### 2.1 The rules all four share

- **A customer always wins.** The Now line offers a waiting job only when nobody needs Ace (§3.2). The moment someone does, the customer's step replaces it within the line's 0.15 s refresh (`DayOneGuideUI.cs:261-265`).
- **One offer at a time, in a fixed order:**
  1. a refill, if the machine is low (it saves a stall later);
  2. a dirty table (seats are money);
  3. a guest with gossip;
  4. your find (it fills whatever is left).
- **The wait in words.** When someone is walking in and the line has room, it names them first: "Nina's on her way. Clear the window table." (8 words). This replaces "Wait for {name} to reach the counter." (`DayOneGuideUI.cs:515`).
- **The first time** each one appears in a session it shows as the full card (title "WHILE YOU WAIT", the step in 26 px bold), then the one line, the way the guide's cards already work (`DayOneHintTimer`).
- **All four are optional.** Nothing is lost by ignoring one except its own pay, and only clearing has a cost: a dirty seat.
- **The words for places** come from S's seat tags: window, lounge, bar, communal, quiet corner, near the door → "by the window", "in the lounge", "at the bar", "at the long table", "in the quiet corner", "by the door".

### 2.2 Your side project: a thrift find you fix and sell

**What the player does:**
1. At the bench, the thrift shelf holds today's find (Day 1: a pocket watch that won't tick).
2. Work on it:
   - from above, `ControlHints.Interact` (E / Cross): "Work on your thrift watch";
   - in first person, look at it and press `ControlHints.Use` (Click / R2).
3. It flies onto the bench's own corner and the close-up opens. The bench's card, glow and controls are the usual ones: `BenchUse`, `BenchHandStick`, `BenchTurnStick`, `Flip`, `BenchZoom`, `BenchHelp`.
4. Step back any time with `ControlHints.Cancel` (right-click / Circle). The find stays on its corner with its pieces in its own cups.
5. Fixed: "Fixed. It sells tonight." It goes back on the shelf with a SOLD TONIGHT tag.
6. At closing it sells by itself: the recap's Tonight says "Sold your thrift watch: +$55". The Shop offers tomorrow's find.

**Where:** a small shelf at the bench's end, within reach of the bench's stand point (no walk). Environment places it.

**How long:** the job's own bench time:
- Day 1's watch, p50 59 s;
- the cracked phone, 43 s;
- the circuit, about 40 s (my guess);
- the seized watch, 60 s.

In the steady week the find took 66 s of Day 1 (fixed in 88 % of days), 41 s of Day 2 (60 %) and 34 s of Day 3 (76 %).

**What it pays:**
- Day 1's find is free and sells for $55.
- Later finds cost $10–35 and sell for $30–110, about 3× (§5.2).
- A find always sells for about 55–60 % of what a customer pays for the same job at Perfect (a customer's grit-inside watch pays $94 plus a tip). So a customer is always worth more, which is also why a customer always wins.
- Good sells for 80 % of the price, Passable for 48 % (the code's own ratios, `JobBase.cs:61-67`, divided by Perfect's).

**What it's for, beyond the money:**
- It is **tomorrow's new job with nobody waiting**: Day 1's watch is Day 2's walk-in; Day 2's cracked phone is Day 3's walk-in; Day 3's frozen phone is Day 4's.
- New verbs get a rehearsal with no patience bar.
- For the demo player who only plays Day 1 (the 14-minute median), the screwdriver, "the verb that felt the best" (days spec §2 call 5), shows up on Day 1.

**Taught:** Day 1, at the first quiet moment after Grace's visit (1:19): "Your thrift watch: fix it while you wait." (8 words). The pin "Thrift watch" sits over the shelf. Later: "Your thrift {thing}: fix it between customers." (7 words). It is never offered before the day's first customer has been heard, so it never competes with the first card.

**How it never blocks a customer:**
1. **Its own corner of the bench.** A slot outside the customers' count, so a customer's device always has the bench's main place. Bigger Bench still adds a second *customer* slot.
2. **When someone reaches the counter, the close-up closes itself** once any hold in progress has finished (at most about 1 s). The Now line points at the customer. Only the find does this; a customer's repair never closes by itself.
3. **Its pieces stay apart.** The stage keeps another job's pieces out from under the device in view (`BenchStage.cs:233-238`).
4. **It never shows** in the Now line while anyone needs Ace, and it has no patience and no review.
5. **Unfinished at closing,** it waits for tomorrow as it is (same session). After a quit and Continue it comes back whole with its fault: the save keeps which find and whether it is fixed, not its screws.

**Code it extends:**
- `RepairJob`, the bench as it is, `BenchCards`, `BenchStage`.
- The precedents: `BenchLab.cs:126-145` (a customer-less phone on the bench) and `CircuitIntegrationChecks.cs:11-45` (the practice phone).
- The gaps to close: `ItemInteractable.cs:20-21, :47` (the owner checks); `ItemSlotArea.cs:58` (usable slots: the find gets its own); `BenchCards.cs:668-686` (the card line names the owner: "Your thrift watch: grit inside, won't tick."); the recap and the save (§6, part 3).

### 2.3 Clear the tables: guests leave cups

**What happens:**
- About one seated guest in three leaves the cup on the table instead of binning it at the door. Mostly the Hurried and the Distracted, by their movement profile: they bin it today, `NpcCup.cs:12-15`.
- The cup is the café's own paper cup with dregs, the same one the man at the bins leaves (`DirtyCup.cs:15-17`).
- The table gets the used-cup badge, and its seats are out of play until cleared. That is today's rule: "a seat with a cup on it is out of play" (`DirtyCup.cs:8-9`; `TableSeat.cs:106-112`).

**What the player does:**
1. Walk to the table.
2. Press `ControlHints.Interact` (E / Cross) at any cup on it: "Clear the table" (`LodgerStory.cs:106`). This works in either view.
3. One press clears the whole table, with a sparkle and the "mess.clear" sound (`DirtyCup.cs:60-68`, `MessTable.Clear`).

**Where:** wherever the seats are.

**How long:** about 5 s: two short walks and one press. The steady Day 1 cleared 5 tables in 24 s.

**What it pays:**
- A dirty seat is a seat nobody can take.
- Patrons pay $5 at the door only when there's a free seat beyond the 3 kept for customers (`PatronSpawner.cs:45-50`, `:177-180`).
- A customer with nowhere to sit drains patience at 1.15 instead of 0.6, "the entire pressure model" (`WaitingArea.cs:91-103`).

Patrons a day on Day 2, with clearing and without it:

| Guest seats (+3 stools) | 7 | 9 | 11 | 13 | 15 | 19 | 23 |
|---|---|---|---|---|---|---|---|
| With clearing | 15 ($75) | 20 ($100) | 25 ($125) | 28 ($140) | 31 ($155) | 33 ($165) | 33 ($165) |
| Without | 9 ($45) | 13 ($65) | 17 ($85) | 22 ($110) | 25 ($125) | 30 ($150) | 33 ($165) |

So clearing is worth about $30–40 a day while seats are scarce, which is exactly the half-finished café. It falls to nothing in a full room. That is the link between his "seats count" and this activity: **F must agree** that placed seats carry cup spots and the same dirty rule.

**Taught:** Day 1, the first quiet moment with a dirty table (3:08): "Clear the table by the window." (6 words), with a pin over the table.

**Never blocks:** one press, nothing in hand. A dirty seat is a squeeze on the room, never a gate on a customer: they still order, and only stand.

**Code it extends:**
- `DirtyCup.Place` (a guest's seat, `DirtyCup.cs:35-55`) and `MessTable` (one press a table, the badge);
- `NpcCup` (the guest who leaves it, instead of `DropInto`);
- `NpcMovementProfile` (a new "leaves the cup" chance);
- `TableSeat.IsDirty` (unchanged).

### 2.4 Chat with seated guests: gossip into the notebook

**What happens:**
- Each day's data names its gossip: Day 1 none, then two lines a day. Days 2 and 3 are below. From Day 4 his sister writes them, because a waiting job taught on Day 2 has to keep coming back.
- One seated guest per line (a patron or a settled guest, never a customer waiting on Ace) shows a small "…" over the head.

**What the player does:**
1. Walk up to the guest.
2. Press `ControlHints.Interact` (E / Cross): "Chat".
3. The guest says one line in a bubble (at most 10 words; `Barks.Say`, `Barks.cs:168-172`).
4. The notebook learns a fact (at most 12 words, `WordBudget.Page`) with the notebook's "+1" nudge.
5. There is no conversation camera and no panel. Ace is free the moment he presses.

**Where:** wherever the guest sits.

**How long:** about 5–7 s. The steady Day 2: 2 chats, 13 s.

**What it pays:**
- No money.
- It feeds the night: on an errand night, each line names tonight's thing, place or time before the man asks; on the other nights, it feeds the story. The fact is filed under the person it's about, because walk-ins never get entries (`NotebookHooks.cs:10`), with the notebook's existing `source` "overheard" and `sure` "likely" (`SaveData.cs:43-58`): no save change.

The lines, all PLACEHOLDER COPY for his sister:

| Day | The guest says | The notebook gets | What it feeds |
|---|---|---|---|
| 2 | "Grace bought fancy cups for tonight's reunion." (7) | "Grace: reunion cups, a sleeve, on her worktop. A guest's word." (11) | Night 2's ask, the reunion cups. Today "Nobody mentions the cups by day" (`NightThings.cs:17-18`) |
| 2 | "Grace never misses her late film. Bed by half one." (10) | "Grace: TV from 11, bed about 1:20. A guest's word." (10) | Night 2's "Go in once she's asleep" (`LodgerStory.cs:181-182`), true to her night (`GraceAtHome.cs:15-18`) |
| 3 | "The Officer's been asking about a man. Everyone." (8) | "The Officer: asking the whole street about a man." (9) | The Officer's question the same day |
| 3 | "That man by your bins? Here before the last owner." (9) | "The man at the bins: here before the last owner." (10) | The man's story; Night 3 has no errand to feed |

**Taught:** Day 2, the first quiet moment with a gossip guest (1:39): "{E} Chat with the guest in the lounge." (7 words).

**Never blocks:** one press, a bubble, no camera. Walking off ends nothing.

**Code it extends:**
- `Barks.Say`;
- `NotebookHooks` (a new `HeardGossip`);
- `NotebookEntries` (a gossip table, pure, compiled by `Tests/NotebookRules`);
- `Juice.Mark` (the "…");
- `NightThings`' day-mention idea: by day someone mentions a thing; at night Ace can take it (`NightThings.cs:6-12`).

### 2.5 Restock by hand: carry cups and beans to the machine

**What changes:**
- The machine holds 12 cups and 12 shots of beans. What's left stays overnight (no free morning refill).
- The rest of the stock sits on the back shelf: what you buy at closing, $30 for +20 each (`ShopInventory.cs:7-13`, unchanged).
- The start is today's 20 of each: 12 at the machine and 8 on the shelf.

**What the player does:**
1. When the machine is at 3 or fewer, the stock chip says "Low at the machine · 3 cups" (`HudCorners.cs:37-38` today says "Low on cups · 7 left / Restock at closing").
2. Walk to the back shelf. `ControlHints.Interact` (E / Cross): "Take cups and beans". One crate goes in one hand.
3. Walk to the machine. `ControlHints.Interact` at the cup stack: "Refill the machine". It fills back to 12 of each from the shelf.

**Where:** the back shelf on the staff side behind the drinks machine, 2–3 m from it. Environment places it.

**How long:** about 8 s. Once or twice a day from Day 2 (the steady week: 0 on Day 1, 1 on Days 2 and 3).

**What it prevents:**
- A stall in the rush. With the machine empty and the shelf full, a drink waits for an 8 s trip while the customer waits too.
- Done in a quiet moment, it costs nothing. It moves time from the rush to the calm.
- It is kinder than today's out-of-stock, where an empty count means "No stock. Talk to {name} and apologise." (`DayOneGuideUI.cs:599`): now that only happens when the shelf is empty too.
- The Supermarket Simulator review describes the same between-customers job: "While you ring people up, you may occasionally need to get up from your spot to restock some of the shelves again."

**Taught:** Day 2, the first quiet moment with the machine at 3 or fewer (4:09): "Low on cups. Fetch more from the back shelf." (9 words).

**Never blocks:**
- The crate is one hand, and talking at the counter needs none.
- A customer's device can still be picked up with the other hand.
- One press puts the crate back, at the shelf or into the machine.
- Two full hands get the existing "Hands full" words.

**Code it extends:**
- `ShopInventory` (split: at the machine and on the shelf);
- `CupStack` and `BeverageCupSupply` ("Out of cups", `BeverageCupSupply.cs:23-24`);
- `PlayerCarry`, which carries only a `JobBase` (`PlayerCarry.cs:295`), so the crate is a small `StockCrate : JobBase` (flagged);
- `HudCorners` (the chips);
- `DayOneGuideUI` (the drink words, `:596-606`);
- `RecapPhone` (the restock row, `:1505-1511`).

### 2.6 If he can only have one for playtest 8: the side project

Day 1, 200 simulated first-timers, seconds with nothing to do (median, p90 in brackets):

| On Day 1 | Nothing to do |
|---|---|
| None, today's spawner | 151 (173) |
| None, the new spawner rules (§3) | 136 (163) |
| **The side project alone** | **73 (109)** |
| Clearing alone | 114 (140) |
| Chat alone (3 guests) | 118 (142) |
| Restock alone (a machine of 6, so it's needed) | 127 (152) |
| The plan's Day 1 (the find and clearing) | 52 (87) |
| All four | 32 (69) |

The 52 s here is a separate batch of single days; the week runs give 48 s.

Why it comes first:
1. **It fills the most.** It removes 63 s of Day 1's dead air alone; the next best, clearing, removes 22 s. The walk it fills is 25–61 s, and so is a bench job.
2. **It's the part he loves.** "I do a bunch of coffee and like I do 2 repairs." The bench is the hook (plan §9), and its help is what he praised.
3. **It shows the leveling.** The first sale lands on Day 1's recap.
4. **It reuses the most:** the bench, the cards and the stage as they are. The new code is the owner checks, the corner slot and the close-up stepping back.

Clearing would come second. It's the cheapest (the mechanic exists) and it makes F's seats pay.

### 2.7 What the market says about these four

- **The demo is Day 1.** The median player gives a Steam demo 14 minutes (plan, How To Market A Game). Intro, Day 1 and its recap take about that, so whatever Day 1 does with its waits is what most demo players judge. That is why the find, the bench, comes on Day 1, and the lighter jobs come on Day 2.
- **The hit shop sims fill the time between customers with exactly these jobs.**
  - **Supermarket Simulator** (38,056 players at its peak, our price, per the plan): "While you ring people up, you may occasionally need to get up from your spot to restock some of the shelves again", and "From ringing up your customers to cleaning and making sure that the shelves are stocked, you are the boss!" (Thumb Culture). That is the refill and clearing.
  - **TCG Card Shop Simulator** (one developer, over 4 million players, per the plan):
    - packs you "crack them open yourself to see whether anything inside is worth more than the pack you just sacrificed": the find's buy-and-flip;
    - "Keeping things well-stocked feels like another sport of its own";
    - you "challenge customers waiting at play tables while the rest of the shop continues operating in the background": chatting with seated guests (GosuGamers).
- **The warning in the same review:** the loop "could feel a bit repetitive for some players". So every job here is optional, short and paid, and the Now line offers one at a time (§7.1).

---

## 3. Pacing between customers

### 3.1 The rule, in three parts

1. **When the café is full and Ace is nearly free, the next walk-in tries again in 1.5 s, not a whole gap later.**
   - Today the gap is set before the cap check (`CustomerSpawner.cs:377`, `:427-431`, fact 2).
   - The new rule: when today's limit holds a walk-in back and Ace has at most one customer job left, look again in `blockedRetryInterval` (1.5 s, `:40-43`). With two or more jobs left, it waits as today.
2. **The quiet nudge.** When nobody needs Ace, nobody is walking in, and Ace has done nothing for 5 s, the next arrival sets off now. It stays within today's limit and before last orders, and never comes before today's featured visit is over: Day 1 protects the first repair (days spec §2, call 4).
3. **The four waiting jobs fill the walk.** The arrival rules can't shorten it.

**Why it never punishes a slow player.** Both arrival parts act only when Ace has at most one job left (the nudge: none). A player who is behind (two or more jobs waiting) never gets an extra customer from them; one who has caught up does, like anyone. The slow player (×1.5 on every time), 500 days each, rules off → on:

| Day | Lost (±0.04 or less; Day 5 ±0.08) | At the till |
|---|---|---|
| 1 | 0.00 → 0.00 | 6 → 7 |
| 2 | 0.07 → 0.09 | 5 → 5 |
| 3 | 0.78 → 0.86 | 6 → 6 |
| 4 | 2.69 → 2.76 | 7 → 7 |
| 5 | 3.39 → 3.36 | 7 → 7 |

The changes are within the noise. The first-timer gets one more customer on Day 1 (7 → 8).

**Rejected**, with the tuning runs' numbers:
- **Retry in 1.5 s always.** Day 1's nothing to do falls from 50 to 40 s, but it costs on the full days (500 days each): Day 5's losses rise for the first-timer (1.09 → 1.25) and the slow player (3.36 → 3.74, ±0.08), and on Day 2 the find gets finished on 45 % of days instead of 61 % ($40 less).
- **Retry only when nothing is pending.** Day 1 comes out at 58 s, against 52 s for "at most one job" (200 days each).
- **Shorter walks:** start walk-ins a few metres from the door, as the opener does (`CustomerSpawner.cs:71-74`). People would pop up on the pavement in view.
- **A faster clock after last orders when the café is empty.** It saved 3–6 s a day (Days 1–3: 52 → 46, 19 → 16, 20 → 17), not worth a rule.
- **"The next one comes sooner whenever Ace is idle"** (the plan's first idea, plan §8). Most of the idle time already has someone walking in, so it can't help.

### 3.2 "Seconds of nothing to do": the definition the labs use

**Somebody needs Ace** when any of these is true (the same reading the Now line uses, `DayOneGuideUI.cs:367-396`):
- a customer at the counter can be heard or decided (`CanHearIntake` / `CanDecide`);
- an accepted customer's device has a step left: on the tray, on the bench, or finished and waiting to be handed back;
- a drink is ordered and not served (a repair customer's wish counts once they've ordered it);
- a support call is ringing (on hold doesn't count: there is nothing to do until it rings);
- someone with the sweat drop can be reassured.

**Something to do while waiting** when any of these is true:
- a dirty table;
- an unfinished find (offered once the day's first customer is heard; on Day 1, once Grace's visit is over);
- the machine at 3 or fewer with stock on the shelf;
- a seated guest with gossip.

**Seconds of nothing to do** are the seconds, while the café is open or anyone is still inside, when nobody needs Ace, there is nothing to do, and Ace isn't in the middle of a waiting job.
- Sampled at 10 Hz.
- Reported in three parts: the first 15 s (the opener's walk under the goal card), after last orders, and the rest.
- Also reported: the seconds nobody needs Ace, and of those the seconds someone was walking in; the seconds on each waiting job; the longest stretch of nothing to do.

### 3.3 The numbers under the rule (simulated first-timer)

| Day | Today: nobody needs Ace (s) / of it someone walking in / longest stretch | Plan: nothing to do (s) / of it: the first 15 s, after last orders, the rest / longest stretch |
|---|---|---|
| 1 | 151 / 127 / 46 | 48 / 12, 12, 19 / 17 |
| 2 | 133 / 116 / 41 | 17 / 12, 4, 0 / 12 |
| 3 | 106 / 90 / 39 | 32 / 12, 1, 10 / 12 |
| 4–7 | 51, 41 (Days 4–5) | 12: the opening walk only |

Every figure is a median, so the parts don't add up to the whole. Today has no waiting jobs, so its "nothing to do" is its idle time. The plan's p90 is 77 s on Day 1, 52 on Day 2 and 71 on Day 3. For the bars this sets, see §6.8.

### 3.4 Why Day 3 stays at two at once

Day 3 at three walk-ins at once would add overlap, but it hits the slow player hardest. The plan's Day 3 with the support call, 150 days each (without the call, the losses move by 0.04 or less and takings fall $18–43):

| Day 3 | First-timer lost | Slow player lost | Takings (first-timer) | Needing Ace ≥ 2 (first-timer) |
|---|---|---|---|---|
| Today (2 at once, ×1.6), as it plays | 0.10 | 0.90 | $674 | 162 s |
| 3 at once, ×1.6 | 0.33 | 2.27 | $626 | 238 s |
| 3 at once, ×1.8 | 0.15 | 1.63 | $650 | 242 s |
| **2 at once, ×1.6 (the plan)** | **0.10** | **0.87** | **$653** | **173 s** |

Two at once keeps the slow player where today has them (0.87 against 0.90) and still grows the overlap (Day 2's 107 s → 173 s). Three at once moves to Day 4. (Today's café has all 26 seats; the plan's Day 3 has 14 in this batch, so it earns less from patrons.)

---

## 4. "The leveling has to show"

### 4.1 What changes visibly by each recap (the steady player)

| By recap | Seats (with the 3 stools) | Pieces / first-level upgrades | Stars | Also new |
|---|---|---|---|---|
| 1 | 10 → 12 | 1 / 1 | ★ (100 %) | The first sale (+$55, 88 %); the first table placed; the dust sheets on the rest |
| 2 | 14 | 2 / 2 | ★★ (82 %) | The watch at the counter; Grace's reunion tonight; the first gossip and refill; the second sale (60 %) |
| 3 | 16 | 3 / 3 | ★★ (100 %) | Four bench jobs; the print on the wall; the Officer's question |
| 4 | 18 | 4 / 4 | ★★ | The circuit, the Human fault, three at once |
| 5 | **26: the café whole again** | 5 / 5 | ★★ | Two rushes |
| 6–7 | 26 | 5 / 6 (all six by recap 6) | ★★ (★★★ about Day 9) | Week two: Tier 2 and level-2 upgrades |

(Seats and pieces: the furniture ladder in §5.3, 200 weeks.)

**The four items the brief names:**
- **The first furniture: at Day 1's recap.** A 2-seat set for $95, placed before Day 2 opens. **F must agree** (§8).
- **The first upgrade within reach after Day 1: yes.** At recap 1, every first level but Bigger Bench is in the till in 100 % of weeks, Bigger Bench in 96 %, and a set plus a tool together ($220) in 199 weeks of 200 (§5.4).
- **The first star by Day 2 or 3: it already comes on Day 1.** ★1 at recap 1 in 100 % of weeks (5 reputation, three "Loved it"); ★★ (30) at recap 2 in 82 %, by recap 3 in 100 %; ★★★ (120) about Day 9. No change from me. The reputation spec's thresholds wait for 7-minute days to be measured (`ReputationRules.cs:90-93`).
- **A new device type each day.** Honestly, the game has three bench devices (the phone, the watch, Grace's camera) plus the support call's phone.
  - Day 1 brings the camera and the phone, and the watch on your own shelf.
  - Day 2 brings the watch to the counter.
  - Day 3 brings the support call's phone only once it has its real model; otherwise it brings new jobs on known devices (the cracked screen, the seized movement).
  - Past that, a new device needs a new model. The cheapest door is the thrift shelf (a radio, a Walkman): a Blender session plus Bench 7 layouts. Flagged for later, not in this plan.
- **Also, not mine but it would help:** the upgrades still change nothing you can see (plan §8). Give each a tell on the bench (the magnetic driver on the tool rest) and have the recap's tomorrow line name it. That's for UI and Art.

### 4.2 The half-finished café on Day 1 (**F must agree**)

- **Open: 10 seats.** The repair bar's 3 stools, the lounge's 2 tub chairs, the window counter's 3 stools, one bistro 2-top.
- **Under dust sheets: 16 seats.** The west banquette bays (6), the communal table (8), the second bistro 2-top (2).
- **Why 10:** the patrons. At 7 guest seats a day brings 15 patrons ($75); the full room brings 33 ($165) (§2.3). So each early seat bought pays visibly, and clearing matters most exactly when the room is small.
  - With fewer, patrons barely come: the valve keeps 3 seats for customers (`PatronSpawner.cs:45-50`), so 6 open seats would leave room for 3 guests at most.
  - With more, the first pieces stop paying: the room fills up near 19 guest seats (§2.3).
- **Settled guests** (the people already seated at the open; NPC wave 2, `PatronSpawner.cs:82-105`): two on Day 1, in the lounge and at the bistro table. Four from Day 2, once seats exist.
- **Day 1 still earns $333**, today's level: the find's sale and more repairs make up for $70 fewer patrons.

### 4.3 Optional, flagged: the mortgage as a first payment

He mentioned it ("The mortgage is coming, he needs to pay it off"). A first payment due at the end of week one, shown on the phone, would give Days 1–3 a goal. It touches the "no new mechanics" lock: a balance and a due date are a new meter.

**For:**
- A goal you can say in one sentence from the first minute. Thief Simulator's "you owe him", Potionomics and Moonlighter run on a debt or a goal (the Project's foundation doc, `claude/foundation-the-fixers-notebook.md` §0.2).
- It ties the café's money to the story Ace already has: broke, with a mortgage.
- A sink. Week one ends with $2,431 unspent even with the furniture (§5.3), so a payment would make money matter longer.

**Against:**
- **The GDD's lock: "There is no game over, ever."** "Not punishing. Bad days earn less." (`foundation-the-fixers-notebook.md` §0.2, point 3.) A missed payment can't end anything, so it is either toothless or a penalty.
- **It works against what he asked for.** Money for the bank is money not spent on the furniture's "sense of like their own freedom".
- **It falls outside the demo.** Week one ends on Day 7; the demo is Days 1–3, so a demo player sees a due date they never reach.
- **More numbers and words on the phone** for a dyslexic first-timer.

**My recommendation: not now.**
- The café that grows is already the Days 1–3 goal: dust sheets on Monday, every seat back by Friday.
- Keep the mortgage as story: the intro's cart line (F) and a letter from the bank on the phone, with no number due.
- Re-decide at the economy pass after playtest 8, with his measured days. If he wants it then, make it a milestone that rewards (the bank's letter turns friendly, a receipt framed on the wall), never a penalty, falling after the demo (end of week two).

---

## 5. The economy, first week

All from the week simulation: 200 weeks each, the first-timer getting faster day by day, cash starting at $0 (`ShopEconomy.cs:7`). Each night the shop is visited in this order:
1. restock when the back shelf is nearly empty;
2. tomorrow's find;
3. one furniture piece and one first-level upgrade, if each leaves $40 in the till.

### 5.1 Earnings per day

| Day | Buying nothing (restock and finds only): takings | Steady player (the furniture ladder, §5.3): takings / patrons / seats after the night / cash after the night | Work (repairs, drinks, tips) | Find sold that day | Lost |
|---|---|---|---|---|---|
| 1 | $333 | $331 / $90 / 12 / $112 | $189 | 88 % | 0.00 |
| 2 | $413 | $439 / $100 / 14 / $188 | $275 | 60 % | 0.01 |
| 3 | $652 | $675 / $115 / 16 / $530 | $508 | 76 % | 0.09 |
| 4 | $601 | $665 / $135 / 18 / $846 | $521 | 22 % | 0.81 |
| 5 | $746 | $830 / $155 / 26 / $973 | $624 | 48 % | 0.97 |
| 6 | $765 | $859 / $170 / 26 / $1,581 | $643 | 40 % | 0.81 |
| 7 | $814 | $886 / $170 / 26 / $2,431 | $694 | 46 % | 0.64 |
| **Week** | **$4,226** | **$4,608** | | | |

Work, find shares and losses are from the same weeks with $95 sets throughout (the steady run with sets only); the two runs differ only in what the furniture costs.

- **Seats pay.** The seats bought add $382 to the week ($4,608 against $4,226), all of it from patrons ($90 a day → $170; $930 for the week against $540).
- **Today's Days 1–5 earn $2,340 in the same model; the plan's earn $2,940.** The difference is Days 4–5 without their storm-outs.

### 5.2 Prices

**Restock: unchanged.** $30 for +20 cups and +20 beans (`ShopInventory.cs:11-12`); it now goes to the back shelf. The first one is needed at Day 2's recap (the shelf empties into the machine on Day 2). Today the first falls on Day 3 (days spec §4.5).

**The finds: new.** One at a time; the Shop sells the next once the last is sold. The sale price is about 55–60 % of what a customer pays for the same job at Perfect; the buy price is about a third of the sale.

| For | Find | Buy | Sells for (Perfect; Good ×0.8, Passable ×0.48) | A customer pays at Perfect |
|---|---|---|---|---|
| Day 1 | Pocket watch: "Debris Inside" | free, on the shelf at the open | $55 | $94 |
| Day 2 | Phone: "Cracked Screen" | $25 | $75 | $138 |
| Day 3 | Phone: "Frozen Boot Sequence" (the circuit) | $20 | $60 | $106 (payout guessed) |
| Day 4 | Pocket watch: "Seized Movement" | $35 | $110 | $200 |
| Later | from the jobs taught, never the same two nights running: Gunked Up $10 → $30; Debris $20 → $55; Screen $25 → $75; Circuit $20 → $60; Seized $35 → $110 | | | |

**Furniture: the economic frame (F must agree; F owns the catalogue).**
- The first 2-seat set (a 2-top table and two chairs): **$95**.
- A 2-seat banquette bay: **$180**.
- The communal table with two benches (8 seats): **$480**.
- The 16 covered seats then cost **$1,115** in Tier 1. Comfier pieces cost more per seat ($48 → $90 → $60 a seat): the communal is a saving goal, the bays a treat.
- **Tier 2** at about 3× (the catalogue's own advice: "Tier 2 costs ~3–4× Tier 1", `claude/furniture-catalog.md`).
- **Décor** (looks only): F's call. I'd keep it at $15–60 a piece, an any-night treat.

**Why $95 first:**
- At recap 1 the till holds about $333 minus the next find ($25).
- So one set plus one tool ($220) fits on all but one first night in 200 (§5.4), and the first night already asks "seats or a tool, or both?".
- A first set at about $190 would make it one or the other on most first nights (both fit on 27 %). That would also work, but it's further from his "pay for the furniture, kind of have a sense of like their own freedom".

**Upgrades: unchanged.** $100–$250 first levels, $4,675 for the lot (`UpgradeEconomyPass.cs:137-213`). The ladder was priced against a $250 day (`:105-107`), and these days earn $330–$890, so it's cheap. Even with furniture as a new place to spend, only about half the first week's money is spent (the steady player ends with $2,431 of $4,608). The rescale waits for playtest 8's DayLogs, as the 14 Oct spec planned.

### 5.3 The steady player's week with the ladder

| Recap | Bought (most nights) | Seats | Pieces | First-level upgrades | Cash after |
|---|---|---|---|---|---|
| 1 | Tomorrow's find, the first set ($95), one upgrade | 12 | 1 | 1 | $112 |
| 2 | Restock, a find, a banquette bay ($180), an upgrade | 14 | 2 | 2 | $188 |
| 3 | A find, a bay, an upgrade | 16 | 3 | 3 | $530 |
| 4 | A bay, an upgrade | 18 | 4 | 4 | $846 |
| 5 | The communal table ($480), an upgrade | **26** | 5 | 5 | $973 |
| 6 | The last first level | 26 | 5 | **6** | $1,581 |
| 7 | (Tier 2 and level 2 from here) | 26 | 5 | 6 | $2,431 |

The model buys the cheapest upgrade it can afford. The café is whole again by recap 5 in all 200 weeks: half-finished Monday, full on Friday.

### 5.4 The first recap each price is in the till (buying nothing else but restock and finds)

| Item | Price | First recap (median) | By recap 1 | By recap 3 | By recap 5 |
|---|---|---|---|---|---|
| A 2-seat set | $95 | 1 | 100 % | 100 % | 100 % |
| Bulk Restock | $100 | 1 | 100 % | 100 % | 100 % |
| A tool (Driver, Machine or Brush) | $125 | 1 | 100 % | 100 % | 100 % |
| A 4-seat set (two sets) | $190 | 1 | 100 % | 100 % | 100 % |
| Shelf Space | $200 | 1 | 100 % | 100 % | 100 % |
| **A set and a tool together** | **$220** | **1** | **100 %** | 100 % | 100 % |
| Bigger Bench | $250 | 1 | 96 % | 100 % | 100 % |
| All six first levels | $925 | 3 | 0 % | 99 % | 100 % |
| The 16 covered seats on the ladder | $1,115 | 3 | 0 % | 95 % (median cash $1,270) | 100 % |
| The covered seats and all six first levels | $2,040 | 5 | 0 % | 0 % | 98 % |
| The whole upgrade catalogue | $4,675 | after 7 | 0 % | 0 % | 0 % |

---

## 6. What changes in the code, in build order

Seven parts. Each can go to one builder.
- **Build order:** 1 first; then 2 and 3 (they touch different files); then 4, which owns `DayOneGuideUI`; then 5 and 6. Part 7 goes alongside each.
- **Shared files:** `RecapPhone.cs` is touched by parts 3 and 6 and by the phone-juice builder, so serialize those. `DayShapeCheck.cs` is shared with S (seat variety), so merge it. `HudCorners.cs` is rebuilt by M's HUD pass, which leaves the stock chips alone (`mockups/README.md:130`), so part 6's chip words merge into whichever lands second.
- **If he wants only the side project for playtest 8:** parts 1, 3, 4 (the find's line only) and 7.

### 6.1 Part 1: the days' data and the spawner (the biggest win for the least code)

- **New `Assets/Editor/PacingSteps.cs`:** `Fixit Fidget › Days › G - The pacing pass (23 Oct)`.
  - It writes §1.5's values into the five day assets, following `DaysOneToThreeSteps.cs:24-38`: survey, change, checks, save; old → new in a report; one undo group; it refuses until the new fields have compiled.
- **`Assets/Editor/DaysOneToThreeSteps.cs`:** its checks assert the 14 Oct values (`:521-530`, `:558-568`, `:597-607`, `:631-636`; the lint at `:992-1003`, `:1109-1110`). Move them to the new values in the same change, never weaker. The "only taught verbs" rule (days spec §10) becomes:
  - Day 2 adds the screwdriver and the pry;
  - a find may use tomorrow's verb, because it is nobody's job.
- **`Assets/Scripts/DayDefinition.cs`:** new authoring fields, with no save change:
  - `thriftFind`, a `FeaturedRepairRequest`, with `thriftBuy` and `thriftSale` in dollars: the day's find. Day 1's is on the shelf at the open, free; each later one is sold at the recap before (§5.2). Past the authored days, the Shop rotates through the jobs taught, never the same two nights running;
  - `gossip`, a `string[]` of today's gossip ids;
  - `previewLine`, the recap's "Tomorrow:" line about this day, shown at the previous recap.
- **`Assets/Scripts/CustomerSpawner.cs`:**
  - (a) at `:427-431`, when the cap holds a walk-in back and `Downtime.CustomerSteps <= 1`, set `timer = blockedRetryInterval`;
  - (b) the quiet nudge: a new `quietNudgeSeconds = 5`, cut the timer when nobody needs Ace, `NpcJourney.OnTheWay(Customer) == 0`, and Ace has been idle that long. Never before today's featured visit is over, never after last orders, always within the cap.
  - Keep `RollJob`'s signature: reflection callers (14 Oct report).
- **New `Assets/Scripts/Downtime.cs`:** a static class, with a small `DowntimeMeter` component sampling at 10 Hz. It holds:
  - `CustomerSteps()`, the §3.2 rules, as the Now line reads them;
  - `NobodyNeedsAce`;
  - `Available`: refill, table, gossip, find;
  - `InActivity`;
  - the day's counters for the labs (§3.2), reset each morning.

### 6.2 Part 2: clear the tables

- **`NpcMovementProfile.cs`:** a new `leavesCup` chance. In the five profile assets: Hurried .6, Distracted .6, Social .25, Relaxed .2, Shy .15, about a third of guests (tune).
- **`NpcCup.cs`:** a seated guest who leaves the cup calls `DirtyCup.Place(seat, guests: true)` and their own cup vanishes, instead of `TakeInHand` and `DropInto` (`:146-167`).
- **`DirtyCup.cs`:**
  - `Place(TableSeat, bool guests = false)` (`:35-55`);
  - new `MessTable.Join(DirtyCup)`: a single cup joins the table it's on (within `TableReach`, `:133`) or starts one, and shows the badge.
  - The man's mess and its note are unchanged (`LodgerDay.cs:184-203`).
- **With F (F must agree):** a placed table's seats know their table, so `MessTable` groups by the piece instead of the 1 m guess (`DirtyCup.cs:126-128`), and every placed seat has a `CupSpot`.

### 6.3 Part 3: the side project

- **New `SideProject.cs`:** a component that marks a customer-less job. It holds `findId` and `salePrice`, plus `IsSideProject(JobBase)`.
- **New `ThriftShelf.cs`:** an `Interactable` at the bench's end that holds today's find. "Work on your thrift {thing}" flies it to the bench's corner and opens the close-up (`ItemInspector.BeginInspection`).
- **`ItemSlotArea.cs`:** a `sideSlot` outside `slots`, never counted in `UsableSlots` (`:58`); `ClaimSlot` gives it to a `SideProject`. A customer's device always has the main slot.
- **`ItemInteractable.cs:20-21, :47`:** a `SideProject` counts as owned (pick up, work on).
- **`PlayerInteractor.cs` (`WorkOn`, `:821-825`) and `ItemInspector`:** while the close-up holds a find and a customer reaches the counter, end the close-up once the current hold finishes, as Cancel would.
- **`BenchCards.cs:668-686`** and the finished words (`:528`): "Your thrift watch: grit inside, won't tick." / "Fixed. It sells tonight."
- **`FocusNameUI.cs:196`:** the pin "Thrift watch".
- **`RecapPhone.cs`:**
  - Tonight: "Sold your thrift watch: +$55", and the day's takings gain "from your finds" (beside "$X from your work · $Y from café guests", `:36-40`);
  - Shop: the find row ("Thrift find for tomorrow: a cracked phone. $25. Sells for about $75.");
  - the "Tomorrow:" line.
- **`SaveData.cs` / `SaveManager.cs`:** an additive `thrift` record: the device prefab's name, the fault index, fixed or not. An old save has none, so it gets the day's `thriftFind`, or none.
- **Scene:** a step places the thrift shelf and the `sideSlot` at the bench (Environment chooses where).

### 6.4 Part 4: the Now line in quiet moments

- **`DayOneGuideUI.cs`:**
  - `FallbackStep` (`:339-362`) and `NextAction`'s waiting line (`:500-518`): when `Downtime.NobodyNeedsAce`, show the waiting-job line in §2.1's order, naming whoever is on the way first when the line has room.
  - The first of each kind in a session shows as the full card, "WHILE YOU WAIT".
  - Pins over the table, the guest and the shelves.
  - `VisitOver` (`:687-690`) gives way to them. Never while anything is pending.
- **`WordBudget.cs`:** unchanged; every line fits 10 words (counted in §1–§2).

### 6.5 Part 5: chat with seated guests

- **New `GuestGossip.cs`:**
  - deals today's gossip ids to seated patrons and settled guests, never to customers;
  - the "…" mark (`Juice.Mark` with a new icon);
  - a "Chat" `Interactable` on the guest;
  - `Barks.Say`;
  - `NotebookHooks.HeardGossip(id)`.
- **`NotebookEntries.cs`:** a pure gossip table: id, who, the line (≤ 10 words), the fact (≤ 12), "overheard", "likely". PLACEHOLDER COPY; `Tests/NotebookRules` compiles it.
- **`NotebookHooks.cs`:** `HeardGossip`.
- **Optional:** `NightThings` / `LodgerStory`: when the cups' gossip is known, the cups' night hint can say "A guest's word."

### 6.6 Part 6: restock by hand

- **`ShopInventory.cs`:**
  - the machine's stock (12 and 12) apart from the back shelf;
  - `TakeCup`, `CanBrew`, `ConsumeBeans` and `CanMake` (`:40-80`) read the machine;
  - a new `Refill()`;
  - `BuyRestock` (`:82-93`) adds to the shelf;
  - `CupsLow` / `BeansLow` (`:23-26`) split into the machine's low and the shelf's.
- **New `BackShelf.cs`:** an `Interactable` on the staff side.
- **New `StockCrate.cs`:** `: JobBase`, no owner, no pay, refused by every `DropSpot`. `PlayerCarry` carries only a `JobBase` (`PlayerCarry.cs:295`).
- **`CupStack.cs`, `BeverageCupSupply.cs:23-24`:** "Refill the machine"; "No cups at the machine".
- **`HudCorners.cs:37-38, :473`:** the chips.
- **`DayOneGuideUI.cs:596-606`:** the drink words when the machine is empty but the shelf isn't.
- **`RecapPhone.cs:1505-1511`:** "+20 each, on the back shelf".
- **`SaveData.cs` / `SaveManager.cs`:** both stocks, additive. An old save puts all its stock on the shelf and fills the machine from it at load.
- **First, grep every reader of `ShopInventory.Cups` / `Beans`**, the labs included.

### 6.7 Part 7: labs and checks

- **`DayShapeCheck.cs`** (`Fixit Fidget › Days › Lab - Day N at the counter`): reads `Downtime`'s counters and adds the bars below. The 14 Oct bars stay (`:416-421`). Each run's new columns go into `days-1-3-runs.csv`, so the medians cover them.
- **`CafeLabDirector`** (the autopilot): at human speed, it does the waiting jobs when nobody needs Ace, in the Now line's order, so the lab measures what is left.
- **`Day1GuideCheck.cs`:** the find's line comes after Grace; the table line; no waiting-job line while anyone needs Ace; every line ≤ 10 words, with icons, not button words.
- **`UpgradeEconomyChecks.cs`:** its day simulation (`:618-841`) gets the cap retry, patrons limited by free seats (it counts patrons alive, not the valve), and the find's sale. It reads the new day data itself.
- **`RecapPhoneCheck`:** Tonight's 40-word budget (`WordBudget.cs:30`) rises by exactly the "Tomorrow:" and "Sold" lines' words, on days with them. Precedent: the guests line, `RecapPhone.cs:36-40`.
- **New edit-mode `Fixit Fidget › Checks › Downtime rules`** (pure):
  - the order;
  - the 10-word lines;
  - the cap retry only at one job or fewer;
  - the nudge never before the featured visit is over, nor after last orders;
  - the gossip budgets;
  - a find never takes a customer's slot.
- **New play lab `Fixit Fidget › Days › Lab - The find gives way`:** a find half done on the bench, then a customer reaches the counter. It passes when:
  - the close-up closes within 1.5 s;
  - the customer's device takes the main slot;
  - the find's pieces stay in their cups;
  - the find finishes after.

  Photos via the lab's Photo.
- **Re-run:** Bench 3, 5, 6, 8 and 9 (two devices on the bench, framed); the Day 1 guide; Stations; Night 1 and Night 2 (the gossip facts); the recap phone; Pause; Juice.

### 6.8 The new bars for the Day 1–3 labs (the autopilot at human speed, doing the waiting jobs)

| Bar | Day 1 | Day 2 | Day 3 | Model's median |
|---|---|---|---|---|
| Seconds of nothing to do after the first 15 s, median of the runs file | ≤ 50 | ≤ 25 | ≤ 30 | 35 / 5 / 19 |
| Longest stretch of nothing to do after the first 15 s | ≤ 25 s | ≤ 20 s | ≤ 20 s | 17 / 12 / 12 |
| Samples with a waiting job on the Now line while somebody needs Ace | 0 | 0 | 0 | — |
| The find offered before Grace's visit is over | never | — | — | — |
| Bench jobs done (customers' and the find), median | ≥ 3 | ≥ 3 | ≥ 3 | 3 / 3 / 4 |
| A walk-in held back by the cap > 2 s while Ace had ≤ 1 job | 0 | 0 | 0 | — |
| Storm-outs (unchanged) | 0 | 0 | ≤ 1, median | 0 / 0.01 / 0.09 |
| First walk-in at the till (unchanged) | ≥ 2:00 | ≥ 1:30 | — | 2:25 / 1:39 |
| Grace done (unchanged) | ≤ 2:00 | ≤ 2:00 | latte ≤ 1:30 | 1:19 / 1:10 / 0:45 |
| The day ends (unchanged) | ≤ 7:30 | ≤ 7:30 | ≤ 8:30 | — |
| Walk-in repairs, in order (the data) | phone: Gunked Up | watch: Debris | screen, seized, gunked (, the call) | — |

The bars leave room because the lab's autopilot isn't my model. If the first runs land well inside them, tighten them to the measured median plus a quarter.

---

## 7. Risks, and the questions he must answer

### 7.1 Risks

| Risk | How we'd know | Answer |
|---|---|---|
| The waiting jobs read as chores. The Supermarket Simulator review: "I can see how the game could feel a bit repetitive for some players". | Playtest 8's notes; the seconds on each job in the Day labs | All four are optional and short. One line at a time, never during a customer. Drop the weakest (the refill) first |
| Teaching overload on Day 2: the swap, the screwdriver for a customer, chat, the refill | Her recording: what she does after each new line | Move the refill to Day 3 (one data change: the machine starts Day 2 full) |
| Two devices on the bench: framing, hover, the pad's cursor. The stage keeps two jobs apart, but Bench 9's framing was never run with two | Bench 5, 6 and 9 photos with the find on its corner | Fallback: the find works only while no customer's device is on the bench, and flies back to its shelf when one arrives. This is clean only for cleaning finds, so Day 1's would become the gunked phone |
| The find's close-up closing itself reads as the game taking control | His and her playtest | A card instead: "Nina is at the counter. {Cancel} Step back." (the bench's impatience card, `DayOneGuideUI.cs:713-714`) |
| The model is a model: no declines; Systems' times are estimates; the circuit's time and two payouts are guesses | The Day labs (20 runs a day) and playtest 8's DayLogs | The bars have room. Retune with the step (it records old → new). The economy check reads the real payouts |
| More customers on Day 1 (7 → 8) push the day's end. It already ran to 7:34 once in three runs on 20 Oct (`2026-10-20.md:78`) | The lab's "the day ends ≤ 7:30" | Last orders' lead 45 → 50 s (`CustomerSpawner.cs:87`) |
| Restock by hand splits the stock: the save, every reader of `ShopInventory.Cups`, the chips | Grep; the save and load checks | Part 6 goes last, with an additive save |
| Guests' cups need F's placed seats to carry cup spots and know their table | F's placement lab | F's prefab rule (§8) |
| Gossip is new canon. `NotebookEntries.cs:7-8`: "Only facts from lines that are already in the game" | His sister's pass | Placeholders, each line ≤ 10 words, each fact ≤ 12, checked by the rules |
| The support call's phone is still a grey cube (art R2) | The art fix list | Day 3's list leaves it out until the real model lands |
| Day 5 repeats for every later day at three at once, so the game never reaches its six at once | Anyone who plays Day 6 | Author Days 6–7 (4 and 5 at once) after playtest 8 |
| A slow player still meets a step on Day 4: lost 0.9 on Day 3, 2.8 on Day 4, 3.4 on Day 5 (every time ×1.5; today 0.9, 9.2, 11.0) | Playtest 8's DayLogs; a slow run of the Day 4 lab | Day 4's patience (×1.45) is the dial. The model never declines a job, which a real player can |
| Money piles up: $2,431 unspent after week one | The recap's Shop; the economy check | The rescale after playtest 8, keeping the asset names (save identity) |

### 7.2 His questions (each with my pick)

1. **Day 1's find:** the pocket watch, which brings the screwdriver a day early, optional and with nobody waiting (**my pick**)? Or a cleaning job, so Day 1 adds no new verb?
2. **The finds:** one at a time, bought at closing for tomorrow and sold by itself at closing (**my pick**)? Or a free find every morning?
3. **Day 3:** two walk-ins at once (**my pick**: the slow player loses 0.9, as today) or three (more overlap; the slow player loses 2.3)?
4. **The support call:** on Day 3 only once its real phone model is in (**my pick**), otherwise Day 4?
5. **The mortgage:** story only for now (**my pick**), or a first payment at the end of week one?
6. **When a customer reaches the counter during your find:** the close-up closes by itself (**my pick**), or only a card says so?
7. **With F:** the 10-seat half-finished Day 1 and a $95 first set (§4.2, §5.2)?

---

## 8. Where this touches F's furniture spec (F must agree)

1. **The half-finished café on Day 1:**
   - Open: 10 seats (the repair bar's 3 stools, the lounge's 2 tub chairs, the window counter's 3 stools, one bistro 2-top).
   - Under dust sheets: the west banquette bays, the communal table and the second bistro 2-top (16 seats).
   - Two settled guests on Day 1, four from Day 2 (§4.2).
2. **The first furniture purchase:** at Day 1's recap, in the phone's Shop, placed before Day 2 opens. Arrange mode's timing is F's call; Day 2 must open with it in.
3. **Prices against the first week:**
   - the first 2-seat set $95;
   - about $180 for a 2-seat banquette bay;
   - about $480 for the communal table with benches;
   - the 16 covered seats about $1,115 in Tier 1, against a week that earns $4,226–4,608 (§5.1);
   - Tier 2 at about 3×.

   A steady player who buys one piece and one upgrade a night has the café whole by recap 5 (§5.3).
4. **Clearing tables:**
   - A dirty seat is unavailable until cleared (today's rule, `TableSeat.cs:106-112`).
   - Every placed seat carries a `CupSpot`, and a placed table knows its seats, so one press clears it.
   - About a third of guests leave a cup.
5. **Seats count, in money:** patrons are limited by free seats (the valve keeps 3 for customers, `PatronSpawner.cs:45-50`). At 7, 9, 11, 13, 15 and 19 guest seats, a day brings 15, 20, 25, 28, 31 and 33 patrons (§2.3), so the first seats pay and the room saturates near 19. If F's "each seat lets one more customer stay" means something stronger, my patron numbers move with it.
6. **The thrift shelf and the back shelf** are new small props. Placement is F's or Environment's, near the bench and behind the drinks machine.
7. **The intro's cart line** ("years of selling coffee from a cart to afford this place") is F's. The mortgage stays in the same intro as story (§4.3).

---

## 9. Decisions his to overrule

1. Two waiting jobs taught a day: Day 1 the find and clearing; Day 2 chat and the refill.
2. Each find is tomorrow's new job. Day 1's is the pocket watch (the screwdriver a day early).
3. Finds: buy for about a third of the sale; sell for 55–60 % of a customer's Perfect pay. Day 1's is free ($55).
4. The walk-in lists:
   - Day 2: the watch's grit (was the gunked phone);
   - Day 3: the screen, the seized watch, the gunked phone, and the call only with its real model;
   - Day 4: the circuit, the seized watch, the watch's grit, the Human fault.
5. Patience and at-once limits:
   - Day 2 ×1.8 (was ×1.4), still 2 at once;
   - Day 3 stays at 2 at once, ×1.6;
   - Day 4: 3 at once, ×1.45 (was 6, ×1.0);
   - Day 5: 3 at once, ×1.3 (was 6, ×1.0).
6. Drink-only shares: Day 1 .55, Day 2 .45, Day 3 .35; Days 4–5 unchanged (.28, .25: `Day_04_Pressure.asset:33`, `Day_05_TwoRushes.asset:36`). Phase gaps as in §1.5.
7. The cap retries in 1.5 s when Ace has at most one job left. The quiet nudge after 5 s. No faster clock after last orders.
8. The machine holds 12 cups and 12 beans; no morning refill; "low" at 3. The shelf holds the rest.
9. About a third of guests leave their cup: the Hurried and the Distracted most.
10. The gossip lines and facts (placeholders), what each feeds, and two a day from Day 4 (to write).
11. The Now line's new lines, their order, the "WHILE YOU WAIT" first card, and naming whoever is on the way.
12. The recap's "Sold your thrift watch: +$55" and "Tomorrow: …" lines.
13. The find's close-up closes itself when a customer reaches the counter.
14. The mortgage: not now.
15. The labs' new bars (§6.8).
16. With F: 10 seats on Day 1, the $95 / $180 / $480 frame, two settled guests on Day 1.

---

## 10. Flags

1. **New mechanics.** All four waiting jobs are inside his 23 Oct lift. Three details are new state, and each is part of a job he chose:
   - the find you buy at closing (a purchase and a save field);
   - stock split between the machine and the shelf;
   - a carried crate (`StockCrate : JobBase`).

   The cap retry and the quiet nudge are tuning, not mechanics.
2. **Locks.** "Layout is cosmetic, not systemic" (Bible §2) was changed by him on 23 Oct ("seats count"); my economy builds on his change. Grace's Day 1 latte suppression, the clean on Day 1 and the swap on Day 2 all stay.
3. **Occupancy.** "occupancy-and-pacing.md fixes the ceiling at 6 for the whole game" (`DayDefinition.cs:102-104`). With Day 5 at 3 at once and repeating, the game holds at 3 until Days 6–7 exist.
4. **Old checks on unplayed days.** `DayOneOnboardingChecks` still tests the unscheduled `Day_01_LearnTheShop` and `Day_02_Juggle` (days spec 14 Oct, flag 10). A pass there is not a pass on the played days.
5. **Shared files.** `DayShapeCheck.cs` is shared with S (seat variety), and `HudCorners.cs` with M's HUD pass. The integrator merges the parts.
6. **The model's guesses.** The circuit's payout ($85), the support call's ($35) and the circuit's bench time (p50 40 s) are guesses; `UpgradeEconomyChecks` reads the real ones from the prefabs.
7. **Night 3 is still "Nothing tonight"** (the cones). Day 3's gossip can feed only the story.
8. **Days 4–5's gossip is unwritten.** The model gives them two lines a day; without them those days lose about 10 s of waiting jobs (the chats took 9 and 11 s).
9. **The brief's paths.** It names the day assets `Day_01_GraceCamera` / `Day_02_GraceReturn` under `Assets/Data/Days/`; they are in `Assets/GraceShowcase/` (the 14 Oct steps, `DaysOneToThreeSteps.cs:47-48`).

---

## Sources

- How To Market A Game, the median demo play time: 14 minutes (cited in `plan.md`). https://howtomarketagame.com/2022/10/26/what-is-a-good-median-play-time-for-a-demo-benchmark/
- WN Hub, Supermarket Simulator's launch (cited in `plan.md`). https://wnhub.io/news/stores-and-publishing/item-43033
- Outlook Respawn, TCG Card Shop Simulator (cited in `plan.md`). https://respawn.outlookindia.com/gaming/gaming-originals/the-solo-dev-who-built-a-card-shop-game-and-watched-it-explode
- Game Developer, PlateUp! at 1.5 million (cited in `plan.md`). https://www.gamedeveloper.com/press-release/plateup-reaches-1-5-million-steam-copies-sold
- Thumb Culture, *Supermarket Simulator – PC Review*: restocking between customers, cleaning, "doesn't feel too repetitive or grindy", "could feel a bit repetitive for some players". https://www.thumbculture.co.uk/supermarket-simulator-pc-review
- GosuGamers, *TCG Card Shop Simulator is easy to get into and dangerously hard to put down*: opening packs "to see whether anything inside is worth more than the pack you just sacrificed", restocking "another sport of its own", playing customers "while the rest of the shop continues operating in the background". https://www.gosugamers.net/entertainment/news/79177-review-tcg-card-shop-simulator-is-easy-to-get-into-and-dangerously-hard-to-put-down
