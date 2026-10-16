# Days 1–3 as an intro level

*Narrative & Game Design Director, wearing Progression & Economy's hat too (Opus 5.5, max). 14 Oct 2026, wave 1b. Read at `playtest-2` (Mac head `fab65d3`, clone `431f081`); the repo was read-only, no code, no Unity, no Mac. **The timeline uses the Systems lead's time budget** (`specs/systems-forgiving-bench.md` §4.8, landed 07:47), not the brief's provisional times. I also read the feel sheet (its 08:05 version) and the TD's spec before finishing.*

## What he will see

A day now lasts seven minutes, not three. On Day 1 Grace walks in first with her late husband's camera and asks only for a clean of the lens and the film path: about a minute with the cloth and the brush while she watches from the stool, and nobody else reaches the till until it is back in her hands. Then the Officer comes in for the game's first coffee, and the café fills slowly with drinks and at most one grubby phone. On Day 2 she is back first thing (cross about her gnome, if Ace took it): the shutter jammed on the first frame and the reunion is tonight, so two holds swap it while she waits for her latte. On Day 3, if the shutter worked, she brings a print of the reunion photo for the wall, the Officer asks about a man, and the screwdriver arrives with three jobs: a watch to open, a cracked phone screen, then the seized watch. No walk-in on Days 1–3 brings a job the game hasn't taught yet.

## What this is

This spec makes the first three days an intro level. For each day it gives:
- what the day teaches;
- who comes when, and what they bring;
- how long the day is and what it pays;
- the first-timer's timeline, minute by minute.

It also gives the exact data, and the few code rules Grace's two visits need.

**Launch scope:** the demo's Days 1–3, the same systems. **Vision:** Days 4+ can be authored the same way, after the scope freeze lifts.

---

## 1. What I did

- **Read:**
  - the three briefs and both role files; the Bible §1–§5;
  - the five specs, plus Systems', the TD's and the feel sheet;
  - the Project docs, code and data the brief lists (cited below).
- **Found which days a fresh game plays.** The scene's `CustomerSpawner.schedule` (`AcesCafeLayoutPlaytest.unity:58481`) plays these days. `Day_01_LearnTheShop` and `Day_02_Juggle` are not scheduled.

  | Day | Asset |
  |---|---|
  | 1 | `GraceShowcase/Day_01_GraceCamera` |
  | 2 | `GraceShowcase/Day_02_GraceReturn` |
  | 3 | `Data/Days/Day_03_TheRealGame` |
  | 4, 5 | `Day_04`, `Day_05` |
- **Ran a day simulation:**
  - **What it is:** a port of the studio's own `UpgradeEconomyChecks.DaySim` (`:602`), 1,500 runs per day. It is scratch work, not repo code.
  - **Rules from the scene:** the spawner's jitter is 0 (`:58499`), the opener reaches the till at about 12 s, last orders come 45 s before closing (`:58497`), the day's cap applies with named visits above it, and there are 3 counter places.
  - **My additions:** the hands can step away from a repair to hear someone or pour a drink. Patrons are capped by how many are in the café at once, as `PatronSpawner` does.
  - **Inputs:** Systems' bench times, for a first-timer and for the clumsy hand's pass bars.
- **Not done:** Unity and code. The final words are placeholders for his sister.

## 2. The decisions and their reasoning

| # | Call | Why | Rejected |
|---|---|---|---|
| 1 | **The day lasts 420 s, every day** (one scene field) | The first repair takes about 17 % of the day: the feel sheet's T11 ("≤ 50 %"), close to ReStory's ≈ 18 %. Day 3's work (≈ 380 s of a first-timer's hands) fits. The code's own note says "7 for ship" (`DayClock.cs:28`). Lab D already runs at 420 s (`DayShapeCheck.cs:53`), and the reputation spec says to set its thresholds from 7-minute days, never 180 s ones (§4). It is data only. | **Per-day lengths.** `DayDefinition.cs:70-79` explains why there is no field: `DayClock` starts the day in `Awake` before the schedule is known, so it would need a new field plus DayClock owning the lookup. Nothing here needs it: gaps and caps shape each day. **Keep 180 s.** Day 3 can't fit, and his run lost the whole day to one repair. **480 s or more.** Day 1 would sit idle, the demo would pass 40 minutes, and the settled guests' stays would hit their 240 s cap (§7). |
| 2 | **Grace's two visits use Systems' split:** fault 0 is the clean, fault 1 the shutter. One episode runs across both visits, and the photo comes on Day 3 | Her Day 1 story needs the episode id on Day 1: the strap's meaning, the reveal, Barnaby's mention and the notebook facts (`CustomerIdentity.cs:280-287`, `NotebookHooks.cs:26-52`). The photo has to wait for the shutter. | **A second episode id.** `FeaturedRepairRequest.cs:35-40` refuses an id the device doesn't carry. **The episode on Day 2 only.** Day 1 would lose the strap's story and the gnome. **A dormant shutter flag.** Code in four places. |
| 3 | **The reunion is Day 2's evening** | Every existing line already agrees. Her intake says "The reunion's tomorrow." On Day 1 Barnaby is being polished "for the reunion" (`NightThings.cs:89`). Night 2's cups are "my reunion cups" (`:125`). The photo then comes on Day 3. | Moving it to the weekend: the payoff would fall outside the demo. |
| 4 | **Day 1 protects the first repair.** Grace is the only arrival before ≈ 1:52; the Officer comes next (the guided first drink); walk-ins start at about 2:23 | The feel targets protect the first repair. The Officer's coffee stays the guided drink lesson, with no other customer at the till. Walk-ins stay light (common §6.8): see §4.1. | **`maxCustomers` 1:** a strictly sequential day, with about 40 s empty after each person. **Today's 26 s gap:** a walk-in reaches the till at about 1:02, in the middle of the repair. |
| 5 | **One new job type a day.** Walk-ins bring only what has been taught: Day 1 cleaning (cloth, brush); Day 2 the swap (tweezers); Day 3 opening up (screwdriver, pry), ending on the hardest bench job. The circuit, the support call and the Human fault wait for Day 4. | Teach, practise, combine. The swap must be Day 2 (decision 8 in the common brief), so the screwdriver comes on Day 3. The screwdriver is also the verb that "felt the best", so it can carry the busiest day. | Random faults (`DeviceDefinition.cs:34-38`). In playtest 5 they gave Day 2 Walter's seized watch. |
| 6 | **Her latte:** none on Day 1, as the lock says. On Day 2 it is the juggle. No regular leaves "Never again" on Days 1–3, by tuning alone: Day 2's patience goes from ×1.2 to ×1.4 | Day 2 is where she was lost in playtest 5 (the 13 Oct morning). | A rule that forbids storm-outs: a new mechanic. |
| 7 | **On Day 3 Grace is featured at 0** for her latte and the photo | The payoff lands whether or not the player did Night 2's favour. | Leaving it to the cups' morning: anyone who skipped the favour never sees the photo in the demo. |
| 8 | **The upgrade ladder stays as it is for playtest 6** | The first purchase should feel easy in the intro, and a price change would add a variable to a playtest about the bench. | A rescale now. The pass's own note (`UpgradeEconomyPass.cs:91-93`) says to measure first. |

## 3. Decisions his to overrule

1. **The day:** 420 s for every day, Days 4–5 included (see Flags).
2. **Day 1:**
   - Grace's latte is suppressed for all repair customers (`suppressRepairDrinkWishes` 1);
   - the Officer arrives at 0.25 of the day (≈ 1:52);
   - the first walk-in reaches the till at ≈ 2:23.
3. **Day 1's camera** shows its shutter in place, looking jammed but with no glow and no hover: a quiet hint of Day 2.
4. **Day 1's camera wakes** by winding only, with no shutter fire. On Day 2 it winds and fires.
5. **Grace's story days:** the reunion is on Day 2's evening; her nephew takes the pictures; she stands in the middle.
6. **The photo outcome** reads the shutter visit's grade, downgraded to Passable when Day 1's clean wasn't Perfect or Good. A smudged lens shows on the photo.
7. **Payouts:** the clean $40 (Perfect $50, as her whole job pays today); the shutter $60 (Perfect $75).
8. **The walk-in repairs, in order:**
   - Days 1 and 2: the phone's "Gunked up" once;
   - Day 3: the watch's debris, then the phone's screen, then the seized watch;
   - after that, walk-ins come for drinks.
9. **Day 3's patience** goes from ×1.0 to ×1.6. Day 2's goes from ×1.2 to ×1.4.
10. **Day 3:** at most 2 walk-ins at once (was 5); Grace featured at 0.
11. **The intro:** Day 2 is the only story morning in Days 1–3. No police car until the night has that consequence.
12. **The upgrade prices** are unchanged until playtest 6 is measured.

---

## 4. The three days

### 4.0 Where the numbers come from

**The bench, hands-on, from the close-up's first frame** (Systems §4.8 and §5; the last two rows are mine, derived from theirs and unverified):

| Job | Steady | p50 | p90 | Clumsy-hand bar |
|---|---|---|---|---|
| Grace's clean (Day 1) | 15.6 s | 25 s | 39 s | 45 s |
| Grace's shutter (Day 2) | 9.4 s | 15 s | 24 s | 30 s |
| Phone, cracked screen | 27 s | 43 s | 67 s | 75 s |
| Watch, seized movement | 37.8 s | 60 s | 95 s | 110 s |
| Phone, gunked up (mine: same as Grace's clean: three spots, no cover) | — | 25 s | 39 s | 45 s |
| Watch, debris (mine: the seized movement, with a wipe in place of the swap) | — | 59 s | 92 s | 105 s |

**Around the bench** (mine, unverified):

| What | Seconds |
|---|---|
| Talk with a walk-in | 5–8 |
| Grace's talk | 25 on Day 1, 30 on Day 2 (with the straight face) |
| The Officer's talk | 10 on Day 1, 18 on Day 3 (with his question) |
| Carry to the bench and hand back | 12–18 |
| A drink, with the walk | 15–25 (the brief); +12 for the game's first drink |
| A walk-in's walk from the street | 30–45 |

**Patience** is exactly the code's: service 45 s, queue 40 s, seated ×0.6, drinking ×0.5 for 20 s then ×0.8, in conversation ×0.1 (`Customer.prefab:3907-3949`, `CustomerBrain.cs:703-731`). The repair-bar stools drain at ×0.6 (scene).

**Simplifications:**
- every repair is Perfect;
- tips use the code's formula;
- reassure is not modelled, which is pessimistic.

**The two players:**
- "First-timer": Systems' p50 as the median and p90 at the 90th percentile.
- "Clumsy": exactly the lab's pass bars.

**The clock at 420 s:** 1 game hour = 38.2 s.

| Real | Game | Event |
|---|---|---|
| 6:15 | 6:49 PM | Last orders for walk-ins (`CustomerSpawner.cs:87`, `:254`) |
| 6:22 | 7:00 PM | The sign turns LAST ORDERS (`HudCorners.cs:475-477`) |
| 7:00 | 8:00 PM | Closing; the day waits for everyone inside |

### 4.1 Day 1, Monday: "Fix what they bring. Pour what they order. Make the rent."

**Teaches:** talk at the till; take a job; carry it to the bench; cloth and brush; hand back over the counter; the drinks station. **New job type:** cleaning.

| | Today | Proposed |
|---|---|---|
| Asset | `GraceShowcase/Day_01_GraceCamera` | same |
| Length | 180 s | **420 s** |
| Phases (start: gap) | Calm 0: 26 · Build .30: 20 · Rush .55: 16 · Recover .80: 24 | **Calm 0: 75 · Build .25: 30 · Rush .55: 24 · Recover .80: 30** |
| Patience | ×2.0 | ×2.0 |
| At once (walk-ins) | 2 | 2 |
| Drink-only walk-ins | .55 | **.70** (fallback 1.0) |
| Repair customers' drink wishes | on (her ticket shows "+ Latte") | **off** |
| Grace (featured, 0) | the four-task camera, fault 0 | **the clean (Systems' new fault 0)** |
| The Officer (story visit) | .35 | **.25** |
| Walk-in repairs | the watch, random fault: debris or seized, 50/50 | **the phone's "Gunked up", once** (fallback: none) |
| Guided opening / featured hints | off / on | unchanged |
| Archetypes | Cheerful, Chatty | unchanged |
| Settled guests | — | 4 (NPC spec) |

**The first-timer, minute by minute:**

| Real | Game | What happens |
|---|---|---|
| 0:00 | 9:00 AM | The intro (fixed Day 1 order, clock held), then open. Four settled guests are already seated; patrons from ≈ 0:05. |
| 0:12 | 9:19 | Grace reaches the till (she is the opener). The first card names her. |
| 0:12–0:37 | | Her intake (the camera, the strap, "the reunion's tomorrow", "just a clean"); Barnaby if Ace asks; E takes the job; her thanks (the reveal). |
| 0:37–0:52 | | The camera goes to the bench. She takes the middle stool and watches. |
| 0:52–1:17 | | The clean: the cloth on the lens, the brush on the film path and the guide. Then "Fixed!". |
| **1:22** | 11:09 | Handed back over the counter. The 75th percentile is 1:29; the clumsy hand 1:49. She leaves happy. |
| 1:52 | 11:56 | The Officer reaches the till: "Coffee, black. First batch; the second batch is always burnt." The guided first drink, served at ≈ 2:39 (clumsy hand ≈ 2:49). |
| ≈ 2:23 | 12:45 PM | The first walk-in: a drink (70 %), or the phone to clean (at most once). The clumsy hand's first walk-in comes at ≈ 2:53. |
| 2:23–6:15 | | Walk-ins, at most two at once, about six in all: ≈ 5 drinks and ≈ 0.9 phone cleans. |
| 6:15 → 7:00 | 6:49 → 8:00 PM | Last orders, then closing. The median day ends at 7:00. |

**Totals (median):**
- 7.9 people served, 0 lost;
- the hands busy 284 s of 420 (68 %);
- the clumsy hand also loses nobody.

**Light, as common §6.8 asks:** at most 2 walk-ins in the café at once, 70 % of them for a drink, at most one bench job (the easiest), patience ×2.0, and nobody lost by either hand. The count (≈ 6 in 7 minutes) is set by that cap, not by the gaps: longer gaps (Build 40, Rush 32, Recover 40) change it by 0.2.

**The money:**
- **$369** median, $362 at the bottom quarter: $195 from his work, $175 from café guests;
- the clumsy hand earns $349.

**The beat:** Ace fixes things for people. Grace trusts Ace with her husband's camera, and the café is warm. The Officer's "second batch" joke comes back that night in the man's mouth.

**What makes him press on:**
- the first star: about 8 reviews, nearly all "Loved it" (+2 each, `ReputationRules.cs:10`), against a first threshold of 5;
- an upgrade he can afford;
- "Close up for the night", and the bins.

### 4.2 Day 2, Tuesday: "Grace comes in early. The coffee won't wait for the bench."

**Teaches:** the swap; keeping a waiting customer happy with a drink (the juggle). **New job type:** the swap.

| | Today | Proposed |
|---|---|---|
| Asset | `GraceShowcase/Day_02_GraceReturn` | same |
| Phases | Calm 0: 22 · Build .25: 15 · Rush .50: 11 · Recover .75: 20 | **Calm 0: 60 · Build .25: 24 · Rush .50: 18 · Recover .80: 26** |
| Patience | ×1.2 | **×1.4** |
| At once | 2 | 2 |
| Drink-only walk-ins | .40 | **.70** (fallback 1.0) |
| Drink wishes | on | on (her latte is the lesson) |
| Grace (featured, 0) | a latte, and the photo's news | **the shutter (fault 1) and her latte** |
| Walk-in repairs | every device and fault (the seized watch included) | **the phone's "Gunked up", once** (fallback: none) |

**The first-timer:**

| Real | Game | What happens |
|---|---|---|
| 0:00 | | The intro: Grace's morning. Her front, with her step empty if Ace took Barnaby (§8). |
| 0:12 | 9:19 | Grace at the till. If Barnaby was taken, first her complaint and the straight face (≈ 10 s). Then "the shutter jammed on the first frame; the reunion's tonight". E takes the job. |
| 0:42–0:57 | | To the bench. At ≈ 0:50 she orders her latte ("A latte, please. No sugar. Life provides enough of that.", `Regular_Grace.asset:104`). |
| 0:57–1:12 | | The swap: the tweezers on the jammed mechanism (it flies to the scrap tray); one hold on the clean one (it flies over and presses in). Then "Fixed!". |
| **1:17** | 11:01 AM | Handed back. The 75th percentile is 1:21; the clumsy hand 1:39. Her latte comes before or after, as he chooses: served at ≈ 1:37 (clumsy hand ≈ 2:07), and she leaves. |
| 1:37 | 11:32 | The first walk-in. |
| 1:37–6:15 | | At most two at once, about seven in all: ≈ 7 drinks (a phone customer may want one too) and ≈ 0.9 phone cleans. |
| 7:00 | 8:00 PM | Closing. The median day ends at 7:01 (clumsy hand 7:14). |

**Totals (median):**
- 8.2 people, 0 lost; Grace is never lost;
- busy 303 s (72 %);
- the clumsy hand loses 0.01 on average: a walk-in, never Grace.

**The money:** **$420** median ($412 at the bottom quarter); $246 of it is his work. The clumsy hand earns $388.

**The beat: trust on the reunion day.** She trusts Ace with her husband's camera on the day it matters. If Ace took Barnaby, it is also the mask: she tells Ace someone stole her gnome, then hands over the camera.

**What makes him press on:**
- the camera he fixed is at the reunion tonight;
- his second star (≈ 32 reputation against 30: about even odds);
- the man's ask at the bins: her reunion cups.

### 4.3 Day 3, Wednesday: "Full house today. Some of them are in a hurry."

**Teaches:** opening a device (screwdriver, pry, cover back on, screws back in), then combining every verb so far. Also the Officer's "Say nothing". **New job type:** opening up.

| | Today | Proposed |
|---|---|---|
| Asset | `Data/Days/Day_03_TheRealGame` | same |
| Phases | Calm 0: 20 · Build .22: 13 · Rush .45: 8 · Recover .72: 18 | **Calm 0: 35 · Build .20: 22 · Rush .45: 18 · Recover .72: 26** |
| Patience | ×1.0 | **×1.6** (×1.4 once the clumsy-hand lab shows the bench within its bars) |
| At once | 5 | **2** |
| Drink-only walk-ins | .30 | **.45** |
| Drink wishes | on | on (`DayOneOnboardingChecks.cs:252-258` requires it) |
| Grace | — | **featured at 0, no repair:** her latte and the photo |
| The Officer | .25, with his question | unchanged |
| Walk-in repairs | random across the watch, the phone and the support call: every fault | **in order: watch debris → phone screen → watch seized movement**, then drinks (fallback: the watch only, at random) |
| Archetypes | Cheerful, Chatty, Sentimental, Impatient | unchanged |

**The first-timer:**

| Real | Game | What happens |
|---|---|---|
| 0:00 | | The intro: B-roll, no Grace shot (§8). |
| 0:12 | 9:19 | Grace at the till. If Night 2's favour was done: her cups and the straight face. Then her latte order. Once Ace takes it, she gives the photo's news ("The reunion photos came out! I'm right in the middle."), then "I've brought a print for your shop." |
| ≈ 1:02 | | Her latte served; she leaves. On screen: "Grace left the reunion photo for the shop." The print appears on the café wall, captioned "FINALLY IN THE FRAME" (`GracePhotoDisplay.cs`; with a smudge and "ARTISTIC, APPARENTLY" for the Imperfect photo). |
| ≈ 1:13 | | The first walk-in: the watch's debris (55 %), or a drink. |
| 1:47 | 11:48 AM | The Officer: his coffee, then "Have you seen a man of this description?" (Say nothing). Served at ≈ 2:59. |
| ≈ 1:18 → 3:47 | | The debris, at the till ≈ 1:18 and handed back ≈ 3:47 (medians): the first screws of the game (p50 59 s hands-on), between drinks and the Officer. |
| ≈ 3:30–6:15 | | The phone's screen (p50 43 s) and the seized watch (p50 60 s) as they come; ≈ 6 drinks in the day. |
| 7:00 | 8:00 PM | Closing. The median day ends at 7:38 (75th percentile 8:14). |

**Totals (median):**
- 7.3 people; 0.46 lost on average (most days none, some days one walk-out);
- 2.2 walk-in repairs done;
- busy 378 s (83 %).
- **The clumsy hand:** 1.75 lost and 0.8 repairs. Day 3 is not the clumsy-hand gate; that is Days 1–2.

**The money:** **$580** median ($499 at the bottom quarter). The clumsy hand earns $351.

**The beat:**
- the payoff: the photo Ace made possible goes up on the wall, possibly the morning after Ace stole her cups;
- the Officer asks about the man.

**What makes him press on:**
- the print on the wall;
- money for the structural upgrades.
- Not the night: Night 3's favour (the cones, `LodgerStory.cs:165-167`) isn't in the game, so tonight plays "Nothing tonight. The street is yours." (`NightZero.cs:464`; Flags).

### 4.4 Against the build he played

The same model, the old bench, 180 s days, the first-timer:

| | Today | Proposed |
|---|---|---|
| Grace lost on Day 1 | 65 % of runs (clumsy: 100 %) | 0 % |
| Grace's first repair handed back | 3:54 when she isn't lost | 1:22 |
| Money by recap 3 (median) | $94 / $349 / $517 | $369 / $788 / $1,365 |

### 4.5 The money, and what it buys

| Recap | That day, median (bottom quarter) | Running total | Affordable at first level |
|---|---|---|---|
| 1 | $369 ($362) | $369 ($362) | Any one ($100–$250), or two of the $100–$125 ones |
| 2 | $420 ($412) | $788 ($775) | The three tools + Bulk Restock + Shelf Space ($675) |
| 3 | $580 ($499) | $1,365 ($1,271) | All six first levels ($925) + one level 2 |

**The whole catalogue ($4,675) is never affordable within the demo.** The first restock ($30) falls on Day 3: about 20 drinks against 20 cups.

**At 420 s the ladder is about 1.5× cheap against its $250 reference day** (`UpgradeEconomyPass.cs:91-93`, `:107`). Held for playtest 6 (decision 8).

### 4.6 The beats

| # | When | Who | What happens | What changes | Cost |
|---|---|---|---|---|---|
| 1 | Day 1, 0:12 | Grace | The camera, "just a clean", the reunion tomorrow, the strap, the reveal; Barnaby if Ace asks | Notebook: strap, husband, reunion, "behind the camera"; relationship +2 | Data + rule R2 |
| 2 | Day 1, 1:52 | The Officer | The first coffee; the second batch | The man repeats it that night | Data (his time) |
| 3 | Night 0/1 | The man | The deal; Barnaby from her step | Nerve; his pages | Built |
| 4 | Day 2, 0:12 | Grace | Barnaby's complaint and the straight face (if taken); the shutter; "tonight"; her latte | The episode's attempt (R3) | Data + R2/R3 |
| 5 | Night 2 | The man | Her reunion cups | Soft feet; the cups' morning | Built |
| 6 | Day 3, 0:12 | Grace | The cups (if taken); the photo; the print | The print on the wall; notebook: the photo | Data (featured) |
| 7 | Day 3, 1:47 | The Officer | "A man of this description" | Notebook: his description; suspicion if Ace flinches | Built |

Featuring Grace on Day 3 can't bring her twice: the morning's visitor skips today's featured regular (`CustomerSpawner.cs:631-641`), and her complaint runs for any regular with an unfaced deed (`MorningFace.cs:68-79`).

---

## 5. Grace's two visits

### 5.1 What the code supports today

- **One fault holds all four tasks:** three grime spots plus the shutter (`GraceReunionCamera.prefab:1043-1051`).
- **`Quality` is 0 until the shutter is replaced** (`GraceCameraRepairJob.cs:10`). Today a clean-only camera can only be Rejected.
- **One episode id gates four things:**
  - her intake, thanks and completion lines (`CustomerIdentity.cs:365-369`, `:429`);
  - Barnaby's mention (`:280-287`);
  - the notebook (`NotebookHooks.cs:26-52`);
  - the memory: the attempt is recorded at departure (`SaveManager.cs:248-251`) and pays off on her next accepted order on a later day (`GraceCameraEpisode.cs:20-22`, `CustomerMemoryService.cs:59-91`).

So "clean only" on Day 1 needs Systems' data split plus four small rules.

### 5.2 The data

**Systems' step 11, agreed as written:**
- fault 0 is the clean, fault 1 the shutter;
- Day 1 keeps `faultIndex: 0`;
- Day 2 features the camera with `faultIndex: 1`.

**My additions:**

| Item | Value |
|---|---|
| Fault 0 label → spoken form | "Dirty Lens and Film Path" → "the lens and film path are dirty" |
| Fault 1 label → spoken form | "Jammed Shutter" → "the shutter's jammed" |
| Fault 0 type, payout | Cleaning (1); $40 |
| Fault 1 type, payout | Mechanical (0); $60 |
| Where the spoken forms go | `FaultSpeech.cs:43` (the camera's row; the table is `:29-44`) |

**The shutter in place on Day 1.**
- **The problem:** fault 0 switches the real part off (`DeviceDefinition.cs:41-63`). That would leave an empty shutter well beside the film path, on the camera's top face at (0.053, 0.060, 0.041).
- **The fix:** add a child, `Shutter mechanism (resting)`:
  - a copy of the jammed assembly's meshes and materials at the same pose;
  - no `ReplaceablePart` and no collider;
  - listed in fault 0 only.
- **Why it must exist:** the camera on Day 1 has a shutter, it just isn't today's job. Art's A7 tells (tarnished brass, a kinked blade) make it a quiet setup for Day 2.
- **Who makes it:** the data step, which re-copies it when A7 lands.

### 5.3 Four rules (Systems builds them; no mechanic, no save field)

- **R1. The shutter gate.** `Quality` gates on the shutter only when it is one of today's tasks. This is Systems' step 11.
- **R2. The visit comes from the job's fault type.** Cleaning is the clean visit; anything else is the shutter visit. `GraceCameraEpisode` gets intake, thanks and completion lines per visit (§5.4); `CustomerIdentity` passes the visit.
  - On Day 2 the intake has two variants, picked from her memory (`lastVisitServed`, `lastGrade`; existing fields, `CustomerMemoryService.cs:36-47`): was her last visit served and graded Perfect or Good?
  - The notebook's camera facts (`NotebookHooks.cs:24-28` her request, `:44-51` her thanks) are learned on the clean visit only. On the shutter visit, a player who never heard Day 1's thanks would get "Family reunion tomorrow" (`NotebookEntries.cs:30-31`) a day late.
  - Barnaby's mention needs nothing: it already stops once he is on Ace's shelf (`CustomerIdentity.cs:280-287`).
- **R3. The photo's attempt is recorded only for the shutter visit** (`SaveManager.RecordRegularVisit`).
  - **The grade it stores:** the shutter's grade, downgraded to Passable when her clean visit before it wasn't Perfect or Good. That memory is read before this visit is written.
  - **What the existing outcomes then mean:** a jammed shutter is no photo ("Missed"), a sloppy lens is the smudged photo ("Imperfect"), and both days' work count.
  - This answers Systems' flag: which job's grade drives the photo.
- **R4. Day 1's wake** winds with no fire (Systems' open question 2: one line in `DeviceWake`). On Day 2 it winds and fires.

**Tests:**
- `ContinuationRuleChecks` (`:33-51`; also compiled into `Tests/ContinuationRules`) gains three cases:
  - a clean visit records no attempt;
  - the downgrade;
  - the shutter visit's return comes on Day 3.
- `GraceRepairInteractionChecks:72` holds for fault 1.

### 5.4 Her lines (placeholders for his sister; each ≤ 90 characters)

**Day 1, the clean:**

| Moment | Line |
|---|---|
| Intake, line 1 | "My husband's old camera. The reunion's tomorrow, and the lens is filthy." |
| Intake, line 2 | "Just a clean, please: lens and film path. Leave the strap; he carried it everywhere." |
| Thanks | Unchanged: "Thank you. For once, I might let somebody put me in the picture." |
| Done, Perfect | Unchanged: "Clean as a whistle, and his old strap's still here!" |
| Done, Good | "Much brighter. Thank you, Ace." |
| Done, Passable | "A little cloudy still, but it'll do. Thank you for minding the strap." |
| Done, Rejected | "Still grubby. I'll try a soft cloth at home." |

**Day 2, the shutter:**

| Moment | Line |
|---|---|
| Intake, after a Perfect or Good clean | "You cleaned it beautifully. Then the shutter jammed on the first frame." / "The reunion's tonight, Ace." |
| Intake, otherwise (Passable, Rejected, or she left unserved) | "I cleaned the lens myself in the end. Now the shutter's jammed." / "The reunion's tonight, Ace." |
| Thanks | "Bless you. My nephew takes the pictures tonight. I'm standing in the middle." |
| Done, Perfect / Good | "It clicks! Shutter works, strap's safe. Thank you, Ace." |
| Done, Rejected | "Still jammed. I'll take it tonight and hope." |

A one-task job can't grade Passable.

**Day 3:** the existing return lines, gift and hand-off note, unchanged (`GraceCameraEpisode.cs:54-101`).

**UI/UX's card lines:**
- `Grace: "The lens and film path are dirty."` (8 words, 42 characters);
- `Grace: "The shutter's jammed."`, not "still jammed": she didn't know on Day 1.

### 5.5 Her latte, and "Never again" on Day 2

- **Should a regular ever leave "Never again" on Day 2? No: not on Days 1–3.** She is the arc's return, and a storm-out loses the photo. From Day 4 she can: consequences matter, and one bad day never loses her for good (nothing makes a regular stop coming: reputation spec §5).
- **Why she was lost in playtest 5 (the 13 Oct morning):**
  - On Day 2 at ×1.2 her bar is 45 × 1.15 (hers) × 1.05 (her relationship after a happy Day 1, `CustomerIdentity.cs:325-330`) × 1.2 = 65 s, which is **109 s seated**.
  - That run also drew Walter's seized watch, on the old bench.
- **Proposed, at ×1.4:**
  - Her bar is 76 s, which is **127 s seated**.
  - The clumsy hand's whole visit for her takes ≈ 85 s: the swap (30 s), carry (17 s), her latte (29 s) and one walk-in's talk.
  - That leaves ≥ 40 s spare.
- **Simulated:** she is lost in 0 of 1,500 runs at ×1.2 and at ×1.4, for both hands. The extra 0.2 is insurance: Systems' times are estimates until the lab measures them. It also takes the clumsy hand's walk-in losses on Day 2 from 0.07 to 0.01 a day.
- **Her Day 1 latte:** suppressed, so the first customer is one job. The suppression reaches her: `RollDrinkWish` returns nothing for every repair customer on that day (`CustomerSpawner.cs:750-753`). The Officer teaches the drink. From Day 2 her latte comes in her own words ("A latte, please. No sugar. Life provides enough of that.", her warm lines, `Regular_Grace.asset:104`).

### 5.6 Nights 0–2 still make sense

- **Night 0** (after the clean): the deal's "Grace likes you." stands. If Day 1 went wrong, the "walked out" version plays (`NightZero.cs:319-325`, unchanged).
- **Night 1:** Barnaby sits on her step, polished for tomorrow's reunion. Her window watches the pavement at 11:49–11:59 PM and 12:44–12:54 AM. Unchanged, and no camera line is needed.
- **Day 2:** if Ace took Barnaby, the gnome's complaint, then the shutter: a bad morning on her reunion day, half of it Ace's doing. If not, just the shutter.
- **Night 2** (after the reunion): the reunion cups sit on her worktop, back from the evening. Her night is unchanged: her armchair and the TV from 11 PM (`GraceAtHome.cs:15`). Day 2 is a Tuesday (`Weekdays.cs:6`), so it is not her Thursday out.
- **Day 3:** the cups' complaint, if they were taken, next to the photo Ace made possible.

---

## 6. The walk-ins

**What each day brings, and what waits:**

| Day | New job type (its verbs) | Walk-in repairs, in order | Waits |
|---|---|---|---|
| 1 | Cleaning (cloth, brush) | Phone "Gunked Up, Won't Charge" (2 brush spots, 1 smudge, no cover), once | Everything else |
| 2 | The swap (tweezers; Grace's shutter) | Phone "Gunked Up", once (practice) | Screws, the circuit, the support call |
| 3 | Opening up (screwdriver, pry, cover back on, screws back in) | Watch "Debris Inside" (4 screws + known cleaning) → Phone "Cracked Screen" (2 screws + the known swap) → Watch "Seized Movement" (everything: the hardest bench job) | The circuit, the support call and the Human fault: Day 4 |

**The field this needs (flagged: new authoring data, not a mechanic):**
- **What:** `DayDefinition.walkInRepairs`, an array of the existing `FeaturedRepairRequest` (a device and a fault index, with an empty episode id).
- **The rule:** when the array isn't empty, a walk-in who brings a device gets the next entry; once they are used up, walk-ins come for drinks.
- **Cost:**
  - about 15 lines in `CustomerSpawner.RollJob` (`:796-862`), plus a counter reset each morning;
  - about 6 lines in `DayDefinition`;
  - an edit-mode check (each entry passes `TryCreateJob`);
  - about 8 lines in the economy check's `WalkIn`.
- **No save change.** The checks this spec adds or changes are in §10.

**The data-only fallback, if he says no:**
- Days 1 and 2: drink-only walk-ins (1.0). Grace's job is each day's only bench job.
- Day 3: `devices: [PocketWatch]`, at random: debris or the seized watch.
- **Simulated:** Day 1 $295, Day 2 $346, Day 3 $593 (0.63 lost); $1,235 by recap 3. The phone's swap never appears in the intro.

---

## 7. The day's length and the clock

**The arithmetic** (first-timer, median hands busy):

| | Day 1 | Day 2 | Day 3 |
|---|---|---|---|
| Hands busy | 284 s | 303 s | 378 s |
| The day as played | 420 s | 421 s | 458 s |
| Share | 68 % | 72 % | 83 % |
| First repair, from the till to the hand-back | 70 s (17 % of the day) | 65 s | — |

**At 180 s**, Day 1's work does not fit: on the 13 Oct 10:43 run, Grace's four-task camera alone took 158 s in the shop, 88 % of the day (common §2). And Day 3's 378 s of hands is twice the whole day.

**Why not 360 s:** Day 3 would run over by more than 1:30, with about one loss more.

**Why not 480 s or more:**
- Day 1 drops to about 60 % busy;
- the demo passes 40 minutes;
- the settled guests' 0.55 leave runs into their 240 s cap past 436 s (NPC spec).

**The demo at 420 s** runs about 35–40 minutes:
- three days of about 7.5 minutes;
- Night 0/1 and Night 2 at about 4–6 minutes each;
- Night 3 is short;
- the intros and recaps.

**The ship default: 420 s for every day.** Revisit after playtest 6, measuring P for the reputation thresholds and the ladder's reference day.

**What assumes 180 s, and what each needs:**

| Lab or check | What it assumes | What it needs |
|---|---|---|
| Day shape Lab C (`Playtest4SystemsSteps.cs:71`, "the scene's 180 s day") | The scene's length | It runs at 420 s now, the same as Lab D. Rename it "(the scene's day)" and re-run C, D and E in fresh sessions. **Give `DayShapeCheck` a day number** (§10). |
| Day 1 guide, `TheOfficer` (`Day1GuideCheck.cs:366-377`) | Moves the clock to 0.36 | Still works with 0.25. Better: read his `arrivesAt`. |
| Night 2 (`NightTwoCheck.cs:441`, `:466-468`, `:709`) | Grace first on Day 3 within 60 s; the Officer by 0.3 × length + 90 s; his Day 1 and Day 3 visits | Holds: Grace is featured at 0, the Officer at 0.25. |
| Pause (`PauseCheck.cs:158`) | An hour worked out from the length | Nothing. |
| Upgrade economy (`UpgradeEconomyChecks.cs:647`) | At most 14 patrons per day, which was true at 180 s | **Count by those alive.** Otherwise it reports $70 where the day makes ≈ $175. |
| `UpgradeEconomyPass` (`:91-93`, `:107`) | A $250 day at 180 s | Re-measure after playtest 6. |
| The café recorder, "Record 3 minutes" (`CafeLifeRecorder.cs:85`) | One day = 3 minutes | The 8-minute recording now covers one day. |
| Day 1 onboarding (`DayOneOnboardingChecks.cs:124-152` the played Day 1; `:155-164` both Grace days keep their featured hints; `:252-258` Day 3 keeps drink wishes) | Not the length; the data | Holds: Day 1 still guides its featured visit, Days 1 and 2 keep their hints, Day 3 keeps drink wishes. |
| Bench 3 act three, Grace's camera content (`ContinuationIntegrationChecks.cs:66-90`) | The four-task camera | Systems' §6 already rewrites both for the split. |
| Night 1 and Night 2 labs' made-up saves (`NightOneSteps.cs:390-400`, `NightTwoSteps.cs:484-518`) | The photo already back by Day 2 (Clear) | They still run (no second return on Day 3). Leave them; they no longer match a real game, where Day 3 plays the cups, the order, then the photo's news. |
| Settled guests (NPC) | — | They leave at 2:06, 2:48 and 3:51, under the 240 s cap. |

---

## 8. The intro's story rule (with Cinematics)

1. **A story morning** is one where a regular arrives at ≤ 0.25 of the day with news or a story job.
   - In Days 1–3, only Day 2 is a story morning: Grace's return.
   - Day 1 is never one: its order is fixed.
   - On Day 3 Grace is due, but Cinematics' one-in-six rule blocks her shot, so the morning is B-roll.
2. **Day 2's Grace shot holds on her step.** If Ace took Barnaby, the step is empty: the game hides a taken thing (`NightTrophy.cs:32`), so the morning after Night 1 tells itself without words.
   - Cinematics' frame lists "Barnaby on her step". He is there only if the favour was skipped.
3. **One line a day, unchanged** (`DayIntro.cs:850-852`):
   - "Fix what they bring. Pour what they order. Make the rent."
   - "Grace comes in early. The coffee won't wait for the bench."
   - "Full house today. Some of them are in a hurry."

   The lines forecast the day's pressure and never the night.
4. **The intro never:**
   - shows Grace's house on a morning she isn't coming in early, more than once in six, or on Day 1;
   - looks into anyone's window;
   - shows the man by day;
   - shows a consequence the game doesn't have. **The police car: no**, until a caught night costs a morning;
   - puts a number, a key or a mechanic in the line;
   - names what happened in the night ("Barnaby's gone").
5. **Rain:** not before Day 4 exists (the scope freeze).

---

## 9. The data changes (for the Producer's Unity B)

Make them with one step, `Fixit Fidget › Days › Days 1–3 - the intro level (data)`. It records old → new, has an Undo, and prints a report. Don't re-run the Grace showcase setup for this: it never overwrites an existing day asset (`GraceShowcaseSetup.cs:107-109`).

**A. The scene, `Assets/Playtests/AcesCafeLayoutPlaytest.unity`**
1. `DayClock.dayLengthSeconds`: 180 → **420** (`:296131`). Optionally the same at `DayClock.cs:28`, with its comment.

**B. `Assets/GraceShowcase/GraceReunionCamera.prefab`** (Systems' step 11, plus the rows marked *mine*)
2. `faults[0]`:
   - type 0 → **1** (Cleaning);
   - description → **"Dirty Lens and Film Path"**;
   - `enableObjects` → Lens grime, Film-path grime, Film-guide grime, + `Shutter mechanism (resting)` (*mine*);
   - payout → **40**.
3. `faults[1]` (new):
   - type **0**;
   - **"Jammed Shutter"**;
   - `enableObjects` → Jammed shutter mechanism;
   - payout **60**.
4. *Mine:* the new child `Shutter mechanism (resting)` (§5.2). It is inactive in the prefab; the fault turns it on.

**C. `Assets/GraceShowcase/Day_01_GraceCamera.asset`**
5. `phases`:
   - Calm 0: 26 → **75**;
   - Build .30 → **.25**: 20 → **30**;
   - Rush .55: 16 → **24**;
   - Recover .80: 24 → **30**.
6. `drinkOnlyChance` .55 → **.70** (`:33`); in the fallback, **1.0**.
7. `suppressRepairDrinkWishes` 0 → **1** (`:37`).
8. `storyVisits[0].arrivesAt` .35 → **.25** (`:54`).
9. `featuredRepair`: unchanged (fault 0 is now the clean).
10. `walkInRepairs` (new): [PhoneRepair · fault 1].
11. `intent`: "Intro: Grace's clean (cloth, brush), the Officer's first coffee; walk-ins after her."

**D. `Assets/GraceShowcase/Day_02_GraceReturn.asset`**
12. `useFeaturedRepair` 0 → **1** (`:47`); `featuredRepair` → the camera, **fault 1**, `grace_reunion_camera_v1` (Systems).
13. `patienceMultiplier` 1.2 → **1.4** (`:31`).
14. `phases`:
    - Calm 0: 22 → **60**;
    - Build .25: 15 → **24**;
    - Rush .50: 11 → **18**;
    - Recover .75 → **.80**: 20 → **26**.
15. `drinkOnlyChance` .40 → **.70** (`:33`); in the fallback, **1.0**.
16. `walkInRepairs`: [PhoneRepair · fault 1].
17. `intent`: "Grace's shutter (the swap) and her latte: the juggle."

**E. `Assets/Data/Days/Day_03_TheRealGame.asset`**
18. Grace:
    - `featuredRegular` → `Regular_Grace` (guid `dfd14ad3…`);
    - `featuredRegularArrivesAt` .15 → **0**;
    - `showFeaturedVisitHints` stays 0;
    - `useFeaturedRepair` stays 0.
19. `patienceMultiplier` 1.0 → **1.6** (`:31`).
20. `maxCustomers` 5 → **2** (`:32`).
21. `phases`:
    - Calm 0: 20 → **35**;
    - Build .22 → **.20**: 13 → **22**;
    - Rush .45: 8 → **18**;
    - Recover .72: 18 → **26**.
22. `drinkOnlyChance` .30 → **.45** (`:33`).
23. `devices` → **[PocketWatch]**, read only by the fallback.
24. `walkInRepairs`: [PocketWatch · 0, PhoneRepair · 0, PocketWatch · 1].
25. `intent`: "The screwdriver; the Officer's question; Grace's photo."

**F. Code**
26. `FaultSpeech.cs`: the two rows in §5.2 replace the camera's old row.
27. Systems: rules R1–R4 (§5.3), and the `walkInRepairs` field (§6) if he says yes.

**Untouched:**
- `Regular_Grace` (patience 1.15) and `Regular_Officer`;
- the day lines;
- the upgrade assets.

**Unity steps (Unity B, after Systems' R1–R4 and the field are in):**
1. Open `Assets/Playtests/AcesCafeLayoutPlaytest.unity`. Run `Fixit Fidget › Days › Days 1–3 - the intro level (data)`. You should see a report listing items 1–25, each old → new, and nothing failed.
2. Save the scene (Ctrl+S). Select DayClock: Day Length Seconds reads 420.
3. Run `Fixit Fidget › Checks › Days 1–3`, then `Checks › Day 1 onboarding`. You should see PASS on both.
4. Run `Fixit Fidget › Bench › Bench 8 - The clumsy hand (lab, drives itself)`. You should see Grace's clean at ≤ 45 s and her swap at ≤ 30 s.
5. Start a fresh game and leave it alone for 2 minutes. You should see Grace at the till at about 0:12, then nobody else until the Officer at about 1:52.

## 10. Checks

**New edit-mode check: `Fixit Fidget › Checks › Days 1–3`.** It passes when:
- the schedule plays GraceCamera, GraceReturn and TheRealGame as Days 1–3, and the clock is 420 s;
- the camera's faults are right:
  - fault 0 turns on the 3 spots and the resting shutter, and not the part;
  - fault 1 turns on the part only;
  - both pass `TryCreateJob` with the episode id;
- every walk-in entry is valid with an empty episode id;
- no entry uses a verb not taught by its day: Day 1 {cloth, brush}, Day 2 + {tweezers}, Day 3 + {screwdriver, pry}, using `RepairChecklist`'s tools;
- every new line is ≤ 90 characters (Dialogue rules).

**The gate: the clumsy-hand lab (Systems' Bench 8).** Grace's clean must finish within 45 s and her swap within 30 s. If either fails, the build is not ready.

**The intro's shape, by day.** `DayShapeCheck` with a day number, the autopilot at human speed, 20 runs a day:

| Bar | Day 1 | Day 2 | Day 3 |
|---|---|---|---|
| First walk-in at the till | ≥ 2:00 (after the Officer) | ≥ 1:30 | — |
| Grace handed back | ≤ 2:00 | ≤ 2:00 | her latte ≤ 1:30 |
| Storm-outs | 0 | 0 | ≤ 1 (median) |
| The day ends | ≤ 7:30 | ≤ 7:30 | ≤ 8:30 |
| Walk-in repairs | only "Gunked up" | only "Gunked up" | only the listed three, in order |

**Re-run:**
- Day 1 guide; Night 0, 1 and 2; Grace at home and her window;
- Stations; the Day intro;
- Upgrade economy (with the patron fix); Reputation rules; Continuation rules and its console test.

## 11. Risks

| Risk | How we'd know | Answer |
|---|---|---|
| Systems' times are estimates (Fitts plus assumptions) | The clumsy-hand report; the intro's-shape lab | Day 3 first: patience up to 2.0, or drop the seized watch from its list |
| The field is refused | The walk-in log | The fallback (§6): Day 3 can open on the seized watch |
| The 30 s between Grace's hand-back and the Officer reads as "what now?" | Playtest 6 timestamps (T16) | The Day 1 guide already gives his coffee its cards (`DayOneGuideUI.cs:21`); its card could name him as he comes in the door (UI/UX's call). Or Calm 75 → 60, at the cost of overlapping the clumsy hand's 1:49 |
| 420 s reaches Days 4–5 too | Anyone who plays Day 4 | Day 4 drops from ×1.6 to ×0.95, with ≈ 2.3× the arrivals on 7 s and 6 s rush gaps. **Retune before the freeze lifts** |
| The money runs ≈ 1.5× the ladder's reference | The recaps; the journal | Re-measure after playtest 6, then rescale (asset names kept) |
| Intro v2 slips | Day 3's intro | Today's code would show her house on Day 3 (`DayIntro.cs:955-973`), as it already does after the cups |
| The resting shutter drifts from Art's new model | Bench 5's photo of Day 1's camera | The data step re-copies it |
| Day 2's first conversation is long (the straight face, then the shutter) | His recording | Fine: patience drains at ×0.1 while talking |
| ★★ comes at recap 2 (≈ even odds) | The recap | Measure P after playtest 6 (reputation spec §4) |

## 12. Playtest 6: what to watch (timestamp each)

**Day 1:**
- times from the open to:
  - Grace at the till;
  - her job taken;
  - the first touch on the cloth;
  - "Fixed!";
  - the hand-back (target ≤ 1:45, T10's 90 s from her arrival);
- does anyone notice the jammed shutter, or ask about it?
- what do they do in the half-minute before the Officer?
- the Officer's coffee, from his arrival to served;
- when the day ends.

**Day 2:**
- do they connect "the reunion's tonight"?
- the swap, from "Lift out" to "Press in";
- her latte: before, during or after the swap?
- does any bar go red?

**Day 3:**
- the first screw of the game, from the first touch to the cover off;
- each storm-out: who, when, and what they were doing;
- the Officer's question: held or flinched?
- do they notice the print on the wall?
- **do they want Day 4?** This is the scope freeze's own test.

**After each day:** the journal's "did you want to play the next day?"

## 13. Against the other specs

| Spec | Agree | Disagree / add |
|---|---|---|
| **Systems** | The split, its indices, R1, Day 2's data. Its "a 420 s day holds 5–6 bench jobs" (mine hold about 2 a day, on purpose: the café half) | **Add:** fault 0 must show the shutter in place (§5.2). **Answers to its questions:** the photo's grade is R3; Day 2's camera arrives clean (yes, and her line says why); Day 1's wake has no fire (R4) |
| **UI/UX** | Day 1's card line (`uiux-checklist-v2.md:115`); the first-repair rule (Day 1, until a hand-back, `:24`); ≤ 8 words and ≤ 46 characters | Day 2's line: "The shutter's jammed." rather than "still jammed": she didn't know on Day 1. **Add (their call):** the Day 1 guide's Officer card could appear as he comes in, to cover the half-minute after Grace (T16) |
| **NPC** | Grace on the middle stool, watching "her camera while he cleans it" (`npc-stools-guests-looks.md:7`): exactly Day 1; 4 settled guests on Days 1–3 (its open question: Days 1–3 only, yes) | Its "a walk-in with a watch takes the stool beside her" won't happen: no watch comes in before Day 3. A walk-in with the phone may |
| **Cinematics** | Day 1's fixed order; Grace at most one morning in six, never Day 1; "with today's data she shows on Day 2 and not on Day 3" (`:278`) | **Answers:** story mornings (§8); no police car; rain later. **Add:** on Day 2 the step is empty if Barnaby was taken |
| **Art** | The neck strap (A6) carries "he carried it everywhere"; A7's jammed assembly with its tells for Day 2 | **Add:** Day 1's resting shutter is a copy of A7's jammed assembly, with no glow. Its tells sit in view a day early, as a quiet setup |
| **Audio** | The camera wakes in its own voice: a wind and a "ka-chick" (`audio-bench-sounds.md:15`) | **Split it:** Day 1's wake is `camera.wind` only (`DeviceWake.cs:115`); the "ka-chick" (`camera.shutter`, `:117`) is Day 2's payoff (R4) |
| **TD** | No conflict | — |

## 14. Against the feel sheet

| Target | Verdict |
|---|---|
| **T10** (the first repair: ≤ 18 actions, first-timer ≤ 90 s, clumsy hand ≤ 120 s) | **Met.** 11 steps in Systems' budget (§4.8). Hands-on 25 s (p50) and 45 s (the clumsy bar); even counted from her arrival at the till to the hand-back, 70 s at the median and 97 s for the clumsy hand |
| **T11** (the first repair ≤ 50 % of Day 1) | **Met:** 17 %, against ReStory's ≈ 18 % |
| **§7.6** ("make one repair a small slice of the day") | This is the 420 s argument |
| **T16** (no stall over 10 s) | The risk is the half-minute between Grace and the Officer: nothing to do, which isn't a stall at the bench, but a stranger may read it as one. Watch it |
| **T17** (show-dirt fires once by itself) | Fits Day 1's clean |

## 15. Open questions (his)

1. **Grace's latte on Day 1:**
   - none, my pick, which is what the lock says;
   - or keep the 13 Oct reading, where her latte stays on the Day 1 the game plays.
2. **The walk-in repair list** (a new field, about 40 lines):
   - yes, my pick;
   - or the data-only fallback.

## 16. Flags

1. **The latte lock contradicts itself across documents.**
   - The Bible says "Grace's Day 1 latte suppression is intentional", and the common brief says it "stays".
   - But the Day 1 the game plays has `suppressRepairDrinkWishes: 0` (`Day_01_GraceCamera.asset:37`). Her ticket shows "+ Latte" (First Hour, 11 Oct).
   - On 13 Oct ★3 was read as "the old Learn-the-Shop day", the unscheduled asset.
   - My data makes the played day match the lock's words. His to overrule (question 1).
2. **New field:** `DayDefinition.walkInRepairs` (§6). With the fallback.
3. **Code for Grace's visits:** R2–R4 (R1 is Systems'). No mechanic, no save field. About 40 lines, plus tests.
4. **420 s changes Days 4–5.** Their 180 s-era gaps and ×0.95 / ×0.9 patience make a cliff after Day 3. Their walk-ins may also pass the 16 names a day (the roster resets each morning; past 16 it says "Walk-in N", `CustomerVisitRoster.cs:47`). Days 1–3 bring 6–8 walk-ins a day: fine.
5. **Economy:**
   - the ladder is cheap at 420 s;
   - the economy check undercounts patrons (`:647`).
6. **Intro v2 must land before playtest 6**, or Day 3 shows her house.
7. **Night 3 is still "Nothing tonight"** (the cones; the scope freeze). The demo's last night is empty: his call once someone wants Day 4.
8. **Stale lines in the docs:**
   - the playtest-4 call "the Officer's Day 1 time (written 63 s, plays 91 s)" (`claude/playtest-4-findings-and-proposals.md` §10, call 6) is now 0.25 (≈ 1:52 at 420 s);
   - the common brief §4's "Day 1 has at most two or three customers" is now ≈ 8 by design. The café half needs them, and the first repair is protected.
9. **The brief asked "the screwdriver first, the swap second?"** No: decision 8 puts the swap on Day 2, so the screwdriver comes on Day 3.
10. **`DayOneOnboardingChecks` tests the played Day 1 (`:124-152`) and still tests the unscheduled `Day_01_LearnTheShop` and `Day_02_Juggle` (`:234-258`).** Harmless; noted so nobody reads a pass on those two as a pass on the played days.
11. **Paths:**
    - this spec is at `/root/cafe/specs/days-1-3.md`;
    - the simulation is scratch, in my session's scratchpad (`daysim2.py`, `sys_times.py`, `final.py`), not the repo.
