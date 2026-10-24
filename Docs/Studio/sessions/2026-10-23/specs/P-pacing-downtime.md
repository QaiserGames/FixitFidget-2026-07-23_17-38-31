# Designer P — Days 1–2 that teach, and things to do while waiting (23 Oct 2026)

You are the Progression & Economy lead, with the Narrative & Game Design director's hat on. Read `/root/review23/builders-common.md`, `/root/review23/his-notes-2026-10-22.md` and `/root/review23/plan.md` first. You write a spec for his approval; you don't change code. Your copy for reading code: `/root/wt23/P` (read-only for you).

## What he said (verbatim)
- "there's a lot of downtime in day one to day two and day three. I don't want the beginning or the tutorial to be a whole boring 1 to 2 days. Like, I played it right now and I just wanted to stop after day two, because it was like I do a bunch of coffee and like I do 2 repairs, and I know it's the beginning of the game, but the game leveling has to show, I think, like a lot better in the beginning."
- His answer to the studio's question: **"day one and two should really teach mostly but what i mean about dead air is really the things you could be doing when waiting for a customer to approach the counter"**.
- He chose all four things to do while waiting:
  - **your own side project:** a thrift-shop device on the shelf you fix between customers and sell at closing;
  - **clear the tables:** guests leave cups; a table isn't free until cleared;
  - **chat with seated guests:** gossip goes into the notebook and feeds the night's errands;
  - **restock by hand:** carry cups and beans from the back shelf to the machine.
- He chose "grow the café": Ace opens a half-finished café on Day 1 and buys and places the furniture; seats count (each seat lets one more customer stay), décor is looks. Designer F writes the furniture spec at the same time (`/root/review23/specs/F-cafe-grows.md`); stay consistent with it. Where the two touch (when the first furniture purchase comes, prices, the half-finished Day 1), propose, and say F must agree. The integrator reconciles.

## The facts to build on
- **His Day 1 last night** (`DayLogs` on his Mac):
  - 7 customers: Grace's camera, the Officer's coffee, an Americano, a tea, Nina's phone, a hot chocolate, a coffee;
  - arrivals at 3, 89, 156, 219, 249, 310 and 341 s of a 420 s day; rarely two needing him at once;
  - all Perfect, all "Loved it", $354.
- **Days 1 and 2 repeat.** Day 2 (his girlfriend's, 15 Oct) had 8 customers, and its only walk-in repair was the same gunked-up phone as Day 1. Day 3 brings the watch (debris, seized movement) and the cracked screen. His own Day 4 (20 Oct): 12 customers, 3 walked out of the queue (a spike after three flat days).
- **The days are data:** `Assets/Data/Days/` (`DayDefinition` assets: `Day_01_GraceCamera`, `Day_02_GraceReturn`, `Day_03_TheRealGame`, …).
  - Phases with spawn intervals, e.g. Day 1: Calm from 0.00 every 75 s / Build from 0.25 every 30 s / Rush from 0.55 every 24 s / Recover from 0.80 every 30 s.
  - The walk-in repair list per day.
  - Day 1 holds at most 2 customers.

  See `CustomerSpawner.cs`, `CafeArrivals.cs`, `DayClock.cs` (420 s days), `DayShapeCheck.cs` (the Day 1–3 labs and their bars: first walk-in no sooner than 2:00, Grace's camera back by 2:00, storm-outs, day ends by 7:30), and the spec `Docs/Studio/sessions/2026-10-14/specs/` if it's there (grep "days-1-3").
- **Teaching:** `DayOneGuideUI.cs` (the Now line, Day 1's steps), `DayIntro.cs` (the morning intro), the bench's cards (`BenchCards.cs`).
- **Money:** six upgrades today, all tool speeds and capacity ($100–$700): Shelf Space 200/500, Wide Brush 125/300, Magnetic Driver 125/325/700, Faster Machine 125/325/700, Bigger Bench 250/650, Bulk Restock 100/250. Restock $30 for +20 cups and beans (`ShopInventory`). Day earnings in Days 1–3: about $230–$380.
- **Clearing tables already exists:** `DirtyCup.cs`, `MessTable`, `LodgerDay.cs`. The bins man's cups after a skipped morning make seats unavailable until Ace clears the table (one press a table, a badge over it, a sound). Today only his mess uses it.
- **The night needs the day:** the night's errands come from the man at the bins (`NightCycle.cs`, `LodgerStory.cs`, `NightThings.cs`, `NightNotebook.cs`). The notebook holds facts about people; "Chat with seated guests" feeds it.
- **The market** (plan.md): the median player gives a Steam demo 14 minutes, so Day 1 is the game for most demo players. Shop-builder sims sell on start small, buy, place, grow.

## What to write
`/root/review23/specs-out/pacing-downtime-spec.md`, with a one-screen summary at the top that he can read on his phone. Then:

1. **The shape of Days 1–3**, a beat sheet per day.
   - What's taught when, and the Now line's steps: Days 1 and 2 teach (his answer); Day 3 is the first real day.
   - Customers and their order: repairs, which devices and faults, drinks.
   - The phases and how busy each is.
   - What's new each day; what the recap says about tomorrow.

   More repairs than today (the bench is the part he loves), never the same walk-in repair two days running, overlap growing day by day, no cliff into Day 4: show the curve. Keep the locks: Grace's Day 1 latte suppression; Grace's camera clean on Day 1 and swap on Day 2.
2. **No dead air.** For each of the four waiting activities:
   - what the player does, step by step, with the controls (`ControlHints`);
   - where it lives in the café;
   - how long it takes;
   - what it pays or prevents;
   - how it is taught (which day, the Now line's words, at most 10 of them);
   - how it can never block a customer (a customer at the counter always wins, and an activity drops cleanly when someone arrives);
   - which existing code it extends: e.g. the side project as a `RepairJob` with no customer, using the bench as-is, sold at closing through the phone; clearing tables as `DirtyCup` for every guest's cup; chat as a short line plus a notebook fact; restock as a carry from the back shelf to the cup stack and hopper.

   Say which one of the four should come first if he can only have one in the next playtest, and why.
3. **Pacing between customers:** a rule that keeps the day full without punishing a slow player. E.g. when nothing is pending and the player is idle for N seconds, the next arrival comes sooner; or the downtime activities fill it. Numbers, and how the Day labs measure it ("seconds of nothing to do": define it).
4. **"The leveling has to show":** what changes visibly by the end of each day:
   - the first furniture (with F);
   - the first upgrade within reach after Day 1;
   - the first star by Day 2 or 3;
   - a new device type each day.

   Optional, flagged because it touches the "no new mechanics" lock: the mortgage he mentioned is in the story only. A first payment due at the end of week one, shown on the phone, could give Days 1–3 a goal. Pros, cons, your recommendation.
5. **The economy numbers for the first week:** earnings per day, prices of the first purchases (furniture with F, the side project's buy and sell prices, restock), and the day the player can first afford each.
6. **What changes in the code** (files, data assets, new classes), in an order to build it in, so that one builder can take each part. What the labs check, and the new bars for the Day 1–3 labs.
7. **Risks**, and the questions he must answer.

Return the whole spec as your final message. Plain words; he reads the summary on a phone, so keep it under 250 words.
