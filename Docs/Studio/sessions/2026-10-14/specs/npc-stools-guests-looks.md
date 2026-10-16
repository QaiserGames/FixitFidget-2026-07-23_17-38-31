# NPC spec: the repair-bar stools, settled guests, outfits, less robotic (14 Oct 2026)

*NPC & Characters lead (Opus 5.5, max effort), wave 1 of the "forgiving bench" session. I read the common brief, the NPC brief, my role file, the Bible (§2, §5, §7), the six Project docs, and the code and scene at `fab65d3` (`playtest-2`). The repo was read-only. Line numbers refer to that commit. The feel sheet (`~/cafe/feel/`) had not landed when I finished; nothing here depends on it.*

## What he will see

On Day 1, Grace takes the middle stool across the counter from the bench. She sits on it, half-turned, with her feet up at the foot ring, and watches her camera while he cleans it. A walk-in with a watch takes the stool beside her. When the intro ends, four people are already in the café: one in the reading corner, one at a laptop, and a pair talking at a banquette table. They leave over the morning while other guests come and go at an uneven pace, and some of those stay longer. No two people in the room wear the same body, and a man's name no longer walks in wearing a woman's body. Walk-ins who know what they want go straight to the counter. Only some stop to look round, and Grace and the Officer never do.

## What I did

- **The stools.** I found two separate causes, with evidence:
  - nobody can sit on any of the three stools;
  - the seat choice almost never reaches the middle or west stool.
- **The fix.** I specified the fix in two parts:
  - repair customers take the repair bar first;
  - stool sitters sit now, on the existing chair clips raised 0.30 m. The stool and its counter turn out to be a chair and a table raised together.
- **Settled guests.** I specified settled guests that use what exists.
- **Outfits.** I counted the outfits and traced the uniform to a badly spread look picker. The colour of the shirt itself is unverified from here.
- **"Robotic."** I picked the three cheapest changes that read less robotic.
- **Not done.** There are no clip downloads, no models and no code, as the brief says. Anything that needs his eyes or the Mac is marked **unverified**.

---

## 1. The repair-bar stools

### 1.1 Why nobody sits (evidence)

**A. Nobody can sit on any of the three. This cause is total.**

- **Snap To Seat is off on all three.** The scene has `snapToSeat: 0` on every repair-bar `TableSeat`: `AcesCafeLayoutPlaytest.unity:187995` (west), `:304023` (middle) and `:202669` (east). Each also has `clearanceRadius: 0.3` and an empty `faceTowards`.
- **The sit refuses them.** `NpcSeating.CanSit` refuses any seat with Snap To Seat off (`NpcSeating.cs:239`).
- **So they stand behind the stool.** `CustomerBrain.SettleHere` then leaves the customer on the stand point (`CustomerBrain.cs:1711-1723`). That point is 0.67 m behind the stool on the room side, facing the bar (StandPoint local z −0.67, yaw 0). A "stool customer" is therefore a person standing behind an empty stool, which is what his girlfriend saw.
- **Why it was switched off.** The seat plan's own note explains it (`CafeSeatsSteps.cs:43-48`, 10 Oct):
  - the sit clips are fitted to the 0.45 m chairs;
  - `NpcSeating` lifts a body at most 0.12 m (`maxHeightCorrection`, `NpcSeating.cs:148`, applied at `:400`);
  - on the 0.745 m stool the hips would land about 14 cm low, with the thighs through the seat.

  The same note calls "a tall-stool sit clip" the real fix.

**B. Two of the three are almost never chosen.**

A customer whose job is accepted takes the nearest free seat by walking distance from the counter place they are standing at (`CustomerBrain.cs:1595`, then `WaitingArea.PickNearest`, `WaitingArea.cs:113-148`). Three rules shape that choice:
- only the 8 seats nearest in a straight line are weighed (`:130`);
- seats within 0.6 m of the nearest are picked between at random (`:27`; scene `:242789`);
- a customer alone at the counter stands at place 0 (x +1.6), because `ClaimSlot` fills the first free index (`CounterQueue.cs:57-68`; the places are at x +1.6 / 0 / −1.6, z 12.5).

The ranking below uses straight-line distances. The measured walks quoted in the 12 Oct comment (`WaitingArea.cs:153-160`) match them within 0.1 m.

| Claimed from | Nearest | East stool | Middle stool | West stool | Result |
|---|---|---|---|---|---|
| Counter place 0, x +1.6 (anyone alone at the counter) | Bistro N-W chair, 4.70 m | #2, 4.81 m | #4, 5.81 m | #9, 6.81 m (never weighed) | Bistro or east, 50/50 |
| Place 1, x 0 (two at the counter) | East stool | #1, 3.21 m | #2, 4.21 m | #5, 5.21 m | East |
| Place 2, x −1.6 (three at the counter) | East stool | #1, 1.63 m | #2, 2.62 m | #3, 3.61 m | East |
| The door (patrons) | Reading corner S, 5.36 m | #24 | #25 | #26 of 26 | Never |

So:
- for a lone customer the east stool is a coin flip, and it is the first choice only when two or three customers queue at once;
- the middle stool wins only when the east is taken and its occupant has reached it;
- the west stool almost never wins.

Patrons never get that far. They stop coming when only 3 seats are free (`PatronSpawner.cs:133-134`), and the three stools are the three seats farthest from the door.

**C. What isn't the cause.**

- **`WaitingArea.HasArrived` (12 Oct).** It only stops someone who has reached a stool from adding the 2.5 m "still on the way" penalty to the stools beside them (`WaitingArea.cs:161-176`, `:186-192`). The penalty now lasts only the roughly 4 s walk to the stool. The Bible §7 line "a stool is claimed only once the customer has arrived" misdescribes this (see Flags).
- **The Stations lab's east-stool result.** It is built into the lab. The lab claims every other place in the room so that a stool is the only choice (`StationsCheck.cs:193-200`, `:722-750`), and from every counter place the east stool is the nearest of the three. The lab tests the hand-back over the counter, not seat choice.
- **Clearance.** The stools are 1.0 m apart with 0.3 m footprints, which need 0.7 m between them (`WaitingArea.cs:220-249`). All three can be taken at once.

### 1.2 Fix, part 1: repair customers sit at the repair bar

**The rule.**
- A customer whose job goes to the bench takes the free repair-bar stool nearest the bench first. "Goes to the bench" means a repair that is not the Human fault (`IsCounterRepair`, `CustomerBrain.cs:402`).
- The order is the middle stool (3.11 m from the bench's stand point), then the east and west stools (3.27 m each). A tie goes to the stool nearer the customer, which is east from the counter.
- With all three taken, the customer takes the nearest seat, as today.
- Everyone else keeps today's nearest-seat rule: drink customers, Human-fault customers and patrons.

**Why.**
- It is what the repair bar was built for. Since playtest 4, the owner waits across the counter and Ace reassures and hands back from the bench without a step. The walk round the counter's east end is about 20 m (`StationsCheck.cs:27-31`, `WaitingSpaceChecks.cs:15-18`).
- It is how a watch-repair counter or a Genius Bar works.
- It puts the person whose thing is on the mat right where he spent all of playtest 5 looking.

**Rejected.**
1. *Reorder the counter places so the first customer talks at x −1.6.* This is data only, and from there all three stools beat every floor seat. But it moves the conversation off the till and its camera, it touches the most-tested path (Day 1 guide 74/74, Conversation view 54/54), and it sends drink customers to the bar too.
2. *A distance bonus on the stools for everyone.* Drink customers would fill the bar and push repair owners onto the floor.
3. *Stools for repair customers only.* The bar would sit empty on a day with no bench jobs.

**Files (about 30 lines).**
- `TableSeat.cs`: new field `repairBar` (bool).
- `WaitingArea.cs`: new method `ClaimRepairBar(occupant, benchPoint)`. It takes free bar seats with claim clearance, picks the one with the smallest flat distance from its seat pose to the bench (ties within 0.1 m go to the one nearer the occupant), and claims it.
- `CustomerBrain.cs` in `TryTakeWaitingSpot` (`:1592-1596`): when the preferred kind is Seat and the job goes to the bench, try the bar first. The bench point comes from `PlayerInteractor.BenchStation.StandPoint` (`PlayerInteractor.cs:112`).

### 1.3 Fix, part 2: how a stool sitter looks until a perch clip exists

**Pick: they sit now, on the existing chair clips, with the body raised 0.30 m.**

**The finding.** At this bar, a stool and its counter are a café chair and table raised 0.30 m together.

| Seat | Seat height | Top height | Top − seat |
|---|---|---|---|
| Café chairs, all 18 at tables (scene) | 0.443–0.445 m | 0.750 m | 0.305–0.307 m |
| Repair-bar stools | 0.745 m | 1.095 m (the ledge) | 0.350 m |
| Window-counter stools | 0.745 m | 1.050 m | 0.305 m |

- **The foot ring.** The stool's steel foot ring sits 0.44 m under the seat ("the feet rest on it", `Tools/Blender/cafe_seating.py:508-556`; the ring is `zr = 0.310` at `:539`). That is the chairs' own seat height.
- **What the raise gives.** The existing Sit Down, Sitting and Stand Up clips, with the root raised, put:
  - the hips on the stool. The lift is 0.745 + 0.085×1.1 − 0.494×1.1 = 0.295 m, using `NpcSitData.asset` and the NPC scale of 1.1 (`Customer.prefab:3684`);
  - the hands at a café table's height relative to the ledge, so finger-tapping and the cup read right;
  - the soles at the ring's height: about 0.30 m against the ring's top at 0.318 m.
- **What doesn't fit.** The baked pose puts the ankles about 0.50 m ahead of the hip (`seatedHip.z` −0.22, `seatedFeet.z` +0.23, ×1.1), and the repair bar has no knee space. Its stone overhangs only 4 cm, with the customer face at z 13.34 against a hip at 12.87 (`cafe_bar.py:374`, `y_bf` −0.43, in the frame y = z − 13.77 of `:359`).
- **The answer to that.** Each sitter turns 45° off square using the existing Face Towards field. At 45° the front of the knee clears the counter face by about 10 cm and the toes by about 3 cm.

**What it needs: about 25 lines in `NpcSeating`, plus two fields in `TableSeat`.**

1. **`TableSeat.maxLift` (m).** How far the body may rise to meet this seat. 0 means `NpcSeating`'s 0.12. The stools get 0.32.
2. **The rise happens only inside the clips.** It rises during Sit Down and comes down during Stand Up.
   - Today the walk's last step climbs to the lifted point (`path[2] = feet`, `NpcSeating.cs:279`; `waypoints.Add(feet)`, `:300`). With a 0.30 m lift that would be the hover he complained about on 26 Sept.
   - New behaviour: the walk ends at floor height. During Sit Down (`:466`) the height eases from 0 at 25 % of the clip to full at 85 % (smoothstep). Stand Up (`:482`) does the reverse between 15 % and 75 %.
   - The head goes from about 1.75 m standing to about 1.67 m perched. That is an 8 cm settle, and it never rises.
3. **`TableSeat.approachSide` (m).** How far beside the seat the way in passes. 0 means today's 0.62 (`NpcSeating.cs:105`). The stools get 0.50, which is the middle of the 1.0 m pitch. At 0.62 the walk brushes the next stool, whose seat edge is 0.81 m out.

**Data, all three stools.**
- Snap To Seat on.
- `maxLift` 0.32, `approachSide` 0.50, `repairBar` on.
- Face Towards points 1 m out at 45°:
  - west at −45° (north-west), (−5.907, 1.0, 13.564);
  - middle at +45° (north-east), (−3.493, 1.0, 13.564);
  - east at +45° (north-east), (−2.493, 1.0, 13.564).
- No two neighbours are turned toward each other, so their feet never meet.
- `CafeSeatsSteps.cs:249`: the repair bar's `snap` becomes true, and the note at `:43-48` changes to match, so a rerun of Seats 1 doesn't undo this.

**Why sit now rather than the alternatives.**
- **Stand-lean (rejected).** No clip exists for it. Batch 1's two leans are against a wall: "Leaning Square With Shoulders Against A Wall" and "Leaning Against A Wall With Various Postures" (`claude/mixamo-clips-batch1.md` §3). At a 1.095 m counter the shoulders would lean on air. Standing is also exactly what she complained about.
- **Wait for a perch clip (rejected).** Batch 2 has no perch clip. Its seated eight are Typing, Writing Seated, Sitting Drinking, Sitting Clap, Sitting Disapproval, Sitting Fidgeting Feet, Sitting Rubbing Arm and Cheering While Sitting (`claude/mixamo-clips-batch2.md` §5). "A perch for the bar stools" appears only in next-step lines (Bible §7; `Docs/Studio/sessions/2026-10-13.md:46,64`), so waiting means waiting forever unless the list changes. Any perch clip would also need the raised body, so this code is reused either way.
- **An existing high-hipped clip (rejected for the sit itself).** Batch 1's "Sitting Thumbs Up" and "Beckoning" put the hips at 75–79 cm, "as if perched on a tall seat" (batch 1 §2). They are one-shot gestures, not a sit-down, idle and stand-up set. They could play as beats on a stool later.

**Where it reads.**
- **From the overhead camera.** `CmShopCam` sits south of the room at (0, 23.79, −15.87), yaw 0 and 43° down, about 37 m from the stools. It sees sitters' backs and heads, and their feet are hidden behind their own bodies.
- **From behind the bench.** Ace's eye is about 1.65 m up at z about 14.6, and the counter edge is at 1.095 m, z 13.30. Everything on the customer's side below about 0.9 m is hidden, so he sees a seated person from the chest up across the ledge.
- **Unverified:** whether the feet read as hanging in front of the ring rather than on it. Only the optional walking view (V) shows them, from the side.

**Fallback.** Tick Snap To Seat off on the three stools (one field each). That returns today's standing, with the bar rule kept.

### 1.4 What can break, and how we'd know

| Risk | How we'd see it | Answer |
|---|---|---|
| The sit reads as a float | Frames of the sit 0.2 s apart in the new lab; his recording | Shorten the ease window. If it still floats, use the fallback. |
| Knees or toes go into the counter face | Sit clearance photos (`NpcSitClearance` now includes the stools) | Turn the stools to 50°, or ask Environment to move them 0.10 m off the counter. That move also shifts the stand points and needs a navmesh rebake. |
| The walk in clips a seated neighbour | Sit 4's "two bodies overlap" | `approachSide` 0.45 |
| The hand-back over the counter misses a seated customer | Stations, both views | The query has no layer mask (`PlayerInteractor.cs:233`). A seated body's capsule moves to Default (`NpcSeating.cs:318`) and is still found (expected pass). |
| Days with no bench job leave the bar empty | By design | The patrons and settled guests carry the room. |

---

## 2. Settled guests on Day 1

### 2.1 Mechanism: guests already seated when the café opens

**This needs a small amount of code.** No "start seated" path exists today. `NpcSeating` exposes only `TrySit`, which walks round the chair and plays the 1.3 s sit (`NpcSeating.cs:254-322`). The precedent for an instant seat is `GraceAtHome.SeatNow` (`GraceAtHome.cs:1155-1167`). From their first frame these guests are ordinary seated patrons, so there is no new behaviour, only a new way into the existing seated state.

1. **`NpcSeating.SitAtOnce(TableSeat)`.** It sets the same state the approach sets on entry (`:309-322`) plus the seated end state, and returns false (so the caller falls back to a walk in) when `CanSit` fails:
   - Placement;
   - `path[0]` is the stand point and `path[2]` is the seated feet point;
   - the agent is warped to the stand point and then switched off;
   - the body's layer goes to Default;
   - `visual.Seated = true`;
   - the animator's `Seated` is true and `Play("Sitting", 0, Random.value)`. The random phase means guests placed in the same frame never breathe in step;
   - the phase becomes Seated.
2. **`PatronBrain.InitSeated(exit, seat)`.**
   - It does what `Init` does (`PatronBrain.cs:172-186`) except the arrival notice (`:182`), because nobody came through the door.
   - It claims the seat, calls `SitAtOnce` and then `Sat()` (`:390`).
   - `seatedAt` is back-dated by `cupAfterSeated` (`:77`), so the coffee is already on the table.
   - A laptop guest reserves and uses the laptop at that seat, as `ClaimLaptopSeat` does.
   - On any failure it falls back to `Init(exit)`.
3. **`PatronSpawner.SeatOpeningGuests()`.** It runs once per day, guarded by `DayClock.Day`, from two places:
   - the spawner's own day-change branch (`PatronSpawner.cs:112-120`). This is the path with the intro Off, and it is already gated on `IsOpen` (`:110`);
   - `DayIntro.HoldTheWorld` (`DayIntro.cs:524`), one line before the spawner is disabled, so the guests are placed under the intro's first shot.

   Placing them when the spawner wakes after the intro would make them pop into view as control returns.

   Rules for the guests:
   - they don't go through `ComeIn` (`:292`), so they pay nothing and Day 1's money is unchanged;
   - they are skipped in labs unless a lab asks (`CafeLab.Active`), the same rule as the intro.

About 70 lines in 4 files.

**Rejected: walking in at the open with existing calls only.** The intro holds the spawner (`DayIntro.cs:518-524`) and the frame order between the two isn't fixed. So the guests would mostly walk in after the intro, into a visibly empty café. They would also come through the 0.96 m navmesh doorway in the same seconds as Grace, who arrives at 0 s (`Day_01_GraceCamera.asset:46`).

### 2.2 Data: the scene's PatronSpawner, a new list `openingGuests`

Each entry holds: the seats; an alternative seat set (picked at random each morning); whether it's a laptop guest; the fraction of the day to leave at (through the existing `StayUntil`, `PatronBrain.cs:135`); and the first and last day.

| Guest | Seats (alternative) | Leaves at | 180 s day | 420 s day |
|---|---|---|---|---|
| A reader, alone | Reading corner N tub chair (S tub chair) | 0.30 (≈ 12:18 PM) | 54 s | 126 s |
| A pair, chatting | Banquette 2-top south: chair + banquette seat, 1.48 m apart (banquette 2-top middle) | 0.40 (≈ 1:24 PM), together | 72 s | 168 s |
| A laptop worker | Communal table east 1, where the laptop lies (no alternative) | 0.55 (≈ 3:03 PM) | 99 s | 231 s |

- **How many.** 4 people on each of Days 1, 2 and 3. They are fresh faces each morning, dealt new looks (see §3).
- **Where.** All the seats are in the room's south half. None is among the 8 seats a customer weighs from any counter place, nor among the stools. The regular patron flow still gets the 2–3 seats nearest the door.
- **The valve.** It sees 26 − 4 = 22 free seats at the open, so patrons keep coming as today.
- **Leaving.** They leave staggered, so seats free up before lunch.
- **The lifetime cap.** `maxLifetime` (240 s, `PatronBrain.cs:63`) still ends any stay. Keep leave fractions at 0.55 or below for days up to 436 s, or have `StayUntil` lift the cap (one line).
- **"Reading".** There is no book prop. The reader is a person in the lamp-lit reading corner doing the seated idles. A book in hand would be a prop plus a hold, which is out of scope (flagged).
- **What exists and is reused.** The pair chats sooner and more often (`NpcAttentionDirector.Companions`). The laptop opens, the bag goes down and the eyes go to the screen (`NpcSocial.cs:496`). The cups go to the bins on the way out. And the room looks up at the opening bell.

---

## 3. Outfits: why it's a uniform, and the fix

### 3.1 The count

- **What exists.**
  - 17 POLYGON looks on the Patron prefab and 18 on the Customer prefab (`Patron.prefab:6444-6461`, `Customer.prefab:4071-4089`). The 17 are 8 women and 9 men (`PolygonNpcSetup.cs:37-45`).
  - Two looks are kept for regulars and skipped for everyone else (`PolygonNpcVisual.cs:242`): `Character_BusinessWoman` is Grace's stand-in and `Character_Male_Police` is the Officer's (`Regular_Grace.asset:21`, `Regular_Officer.asset:21`). The 18th Customer entry is inferred to be the police look; the look prefabs are git-ignored, so that is **unverified**.
  - 20 % of anonymous people keep a CC0 body (`keepOriginalShare: 0.2`). Those get one of 5 outfits in rotation: Casual_2, Casual_Hoodie, Farmer, Suit and Worker (`NpcVisualVariants.cs:48-51`).
  - Usable for walk-ins and patrons: **16 city looks + 5 CC0 = 21.**
- **Patrons.** The Patron prefab has no `CustomerIdentity`, so every patron takes the anonymous path: `(k+1) × 2654435761`, then `/1000 % 17` (`PolygonNpcVisual.cs:220-223`).
  - Five steps of that sequence add 387,276,917 (mod 2³²). That shifts the index by −1 or 0, so patrons k, k+5, k+10 … get the same look or the one next to it.
  - The first 10 patrons get **6 different looks**. The first 20 get 8, and 12 of those 20 wear just three looks: `SM_Gen_Chr_Street_Female_01`, `Street_Male_02` and `Street_Male_04`.
  - His Day 1 had 14 patrons. They wore **7 different looks**, those three on 8 of the 14, and 9 of the 12 city bodies were POLYGON Generic street characters.
- **Named walk-ins.** The FNV hash of the name, `/1000 % 18`.
  - The 15 walk-in names give **9 different looks**. `Character_Female_Coat` gets 4 of them: Ali and Alishba land on Grace's look, and Nina and Saamin land on the Officer's and then on Grace's.
  - **5 of the 15 wear the other sex's body**: Valerie is in `BusinessMan_Shirt`, and Deshawn, Mansoor, Ali and Omar are in women's bodies. The ticket and the conversation show the name.
- **The red shirt (unverified).** I can't see the bodies' colours from here. The likely explanation: the POLYGON Generic street bodies share one material and top colour, and the picker favours three of them. Two existing steps on the Mac settle it:
  - **Fixit Fidget › City pack › NPC looks 2 - Line-up photo**;
  - **Fixit Fidget › City pack › Catalog**, which lists each look's materials (`CityPackCatalog.cs:109`).

### 3.2 The fix, within existing assets

1. **Deal looks instead of hashing them** (`PolygonNpcVisual.Start`, `:203-225`; about 20 lines).
   - Unchanged: regulars, fixed looks and the 20 % CC0 share. The share becomes a plain roll.
   - Everyone else gets the look worn by the fewest people in the café right now, counting only those with a `CustomerBrain` or `PatronBrain`. Ties are broken at random.
   - A look counts as worn from the moment it is applied, so a pair can't draw the same look.
   - Result: in a room of 10, at most one of any city look, until more than 16 people are present.
2. **Match the body to the name** (about 10 lines plus data).
   - A look is a woman's when its name contains "Female" or "Woman". Every one of the 17 follows that naming.
   - Named walk-ins are dealt only from their own sex's looks.
   - The data is a parallel list beside `CustomerSpawner.firstNames` (scene `:58659`), with Any, Woman or Man per name, carried to `CustomerIdentity`.
   - Proposed default, for his sister to correct:
     - women: Valerie, Priya, Ingrid, Alishba, Nina, Beatriz;
     - men: Deshawn, Tomas, Mansoor, Ali, Walter, Omar, Malcom;
     - any: Yuki, Saamin.
   - Patrons stay "any".
3. **Palette swaps, only once the catalog shows they apply.**
   - The pack's 8 alternate atlases are on the Mac (`Assets/Synty/PolygonCity/Materials/Alts/`, PolygonCity_01_A … 04_C; used for the cars, `PolygonCityDressing.cs:845-858`).
   - If a body's material is one of them, each person gets an alternate, dealt the same way as the looks.
   - Gate: one line-up photo per alternate. Any alternate that turns skin or hair an unnatural colour is struck off.
   - The Generic pack's alternates are unverified.
4. **Zero-code stopgap**, if the build slips: interleave the Patron prefab's look list (City, Generic, woman, man, alternating). The repeats remain, but repeats no longer cluster on one pack.

---

## 4. "It seems robotic": the three cheapest changes

Ranked by how they read from the overhead camera on Days 1–3. All three change presentation only.

1. **The room keeps some faces (timing).**
   - Today Day 1 runs as a conveyor: 14 patrons arrive one every 12 s ± 35 %, walk to the nearest chair, sit 40–90 s and leave.
   - Change: laptop patrons stay 2× (80–180 s, because they're working), a pair 1.3× (52–117 s), and everyone else as today. Arrivals jitter ±60 % (gaps of 4.8–19.2 s instead of 7.8–16.2 s), so people come in clumps and lulls.
   - Files: `PatronBrain.Sat` (`:390-394`) gets a stay factor by kind (2 fields, 3 lines). The scene's PatronSpawner `spawnJitter` goes from 0.35 to 0.6 (data).
   - Cost: about 15 minutes.
   - It reads from above as tables that keep their people while others turn over.
2. **Only some walk-ins stop to look round (entrance).**
   - Today every walk-in except the day's first walks 2–5 m in, stops for 1–3 s and turns round (`CustomerBrain.cs:824-836`, `:1010-1024`; only the opener skips it, `CustomerSpawner.cs:539-541`). Regulars do it too.
   - Change: regulars never do it, because they know the place. Walk-ins do it with a chance of clamp01(0.9 − urgency):
     - Relaxed 0.7;
     - Distracted 0.55;
     - Shy and Social 0.4;
     - Hurried 0.
     
     Weighted by the profiles that is about 42 %.
   - File: `CustomerBrain.cs:829`, the condition (4 lines).
   - Cost: about 10 minutes.
   - On Day 1 it affects the Officer and 1–2 walk-ins; on busier days, every arrival.
3. **They watch their own repair (look-at). This is the Grace one.**
   - While their device is on the bench (`StationInteractable.BenchHolds`, `:46`) or in Ace's hands (`ItemInspector.FocusedItem`, `:173`), the owner's eyes go to it. This reuses the laptop pattern:
     - weight 45 seated and 30 standing, out of a pool of about 100–150 (the laptop is 60, `NpcSocial.cs:496`);
     - 3–7 s each time (`:501`);
     - its own beat kind `LookRepair`, so Life 3's laptop counts don't change.
   - Files: `CustomerBrain` (4 lines in the wait) and `NpcSocial` (6 lines).
   - Cost: about 20 minutes.
   - It reads strongly from the bench, where he spent playtest 5: across the ledge, Grace watches her camera in his hands. From above it shows only as heads turning. There is no "watching your repair" bonus, which would be a new mechanic.

**Considered, not picked.**
- Beat timing jitter and unsynced idles already exist: random gaps of 5–14 s per profile (`NpcSocial.cs:451-460`) and per-looker reaction delays (`NpcAttentionDirector.cs:426`). Only the instantly seated guests could fall into step, and §2 randomises their phase.
- A per-person `Animator.speed` would slide feet, because the walk rate is fitted per person (`NpcLocomotion.cs:757`).
- A yaw offset at waiting spots (`CustomerBrain.cs:1699`) helps only the rare person who stands to wait. The counter already has one (`:765-770`).
- Choosing seats from a wider tie window would bring back the "walking past empty chairs" read that pass 2b removed (`WaitingArea.cs:105-112`).

---

## 5. Build list for Unity C, in order

Each step is whole files, then its check, before the next.

| # | Change | Files | Data |
|---|---|---|---|
| 1 | Repair customers take the bar first | `TableSeat.cs`, `WaitingArea.cs`, `CustomerBrain.cs` | `repairBar` on the 3 stools |
| 2 | They sit, raised | `TableSeat.cs`, `NpcSeating.cs`, `CafeSeatsSteps.cs` (`:43-48`, `:249`) | Snap To Seat on, `maxLift` 0.32, `approachSide` 0.50, 3 Face Towards points |
| 3 | Settled guests | `NpcSeating.cs`, `PatronBrain.cs`, `PatronSpawner.cs`, `DayIntro.cs` (1 line; Cinematics owns this file this wave) | `openingGuests` (3 entries) |
| 4 | The three robotic changes | `PatronBrain.cs`, `CustomerBrain.cs`, `NpcSocial.cs` | `spawnJitter` 0.6 |
| 5 | Dealt looks, name-sex match | `PolygonNpcVisual.cs`, `CustomerSpawner.cs`, `CustomerIdentity.cs` | Name sexes (16) |
| 6 | Palette swaps, if the catalog shows atlas materials | `PolygonNpcVisual.cs` | Allowed alternates per prefab |

The total is about 250 lines in 11 files. Steps 1–4 fit one session; 5–6 fit another, or the tail of the same one.

---

## 6. Checks

**Must still pass, with today's counts.**
- The café's guards: Neighborhood guards (26 seats), Café life setup 54/54, Lived in 2 (36/36 paths, 33/33 served).
- The occupied-circulation guard. It models every stand point as a standing body (`AcesCafeLayoutSetup.cs:204-232`), and no stand point moves.
- Life 2 15/15 and Life 3 22/22.
- Attention 11/11 (in a session where the lab serves the customers).
- Café life 32/32.
- Day shape C 15/15, Day 1 guide 74/74, Day intro 104/104, the door rushes 12/12 and 12/12, Stations 95/95 and 94/94 (both views).
- Mixamo 5 no worse than 47/52.

**Must change.**
- **`WaitingSpaceChecks.cs`.** Find the stools by `RepairBar`, not `!SnapToSeat` (`:162`). Keep "the second stool" (`:199-256`). Add:
  - the three stools are flagged;
  - `ClaimRepairBar` from each counter place gives middle, then east, then west;
  - a drink claim from place 0 still gets the nearest seat.
- **`Playtest4SystemsSteps.cs`** (`:219`, `:402`). Find the stools by `RepairBar`.
- **Sit 4** (`NpcSitLiveCheck.cs:33-37`, `:241`). The repair-bar stools now count as seats people sit on, with hips within its 5 cm. The window counter's three still stand and keep the note.

**New: "Life 4 - Stools, settled guests, looks" (a lab session).**

A. Stools.
- Three bench-job customers, one after another, take the middle, east and west stools.
- Each sits with the hips within 5 cm of the seat pose.
- The root never leaves the floor before Sit Down starts.
- The head's lowest point during the sit is no lower than its seated height minus 2 cm.
- Reassure and hand-back over the counter work with a seated customer, with Ace's displacement at 0.00 m.
- Each stands up and walks out with no stall over 3 s.
- A fourth bench job, with all three stools taken, gets the nearest floor seat.
- Photos: from overhead, from the bench close-up, and the sit as a strip of frames 0.2 s apart.

B. Settled guests (`ForceOpeningGuests`).
- 4 seated on the day's first frame, in their listed seats.
- Patron income is still 0 at the open.
- The laptop is on, and the pair are companions who chat within 30 s.
- Each pair of guests' `Sitting` phases differ by more than 0.1.
- Each leaves within ±0.03 of its fraction, and the pair within 1.4 s of each other.
- An overhead photo 2 s after the open, which Art's "café full from above" pass can also use.

C. Looks.
- 20 patrons plus 6 walk-ins, reporting each one's look (and material, if palettes are on).
- Pass: no look on more than 2 of the 20, at most 1 repeat among any 10 present at once, and every named walk-in's body matches their name's sex.

---

## 7. Playtest 6: what to watch

Timestamp each on the recording.

- **The bar.** When Grace sits on Day 1, does it read as sitting, or as a float or a slide? Does his girlfriend say anything about the stools now? Does a second person sit at the bar?
- **Opening.** When the intro ends, does the café look lived-in? Ask her the question she answered last time: "does it feel empty?"
- **Clones.** Count any two identical bodies in one frame.
- **Arrivals.** Does any arrival's stop-and-look read as a robot scanning?
- **Watching.** Does he notice Grace watching her camera? Unprompted only; nobody helps during play.

---

## Decisions his to overrule

1. A customer with a bench repair takes the repair bar first, with the middle stool, across from the mat, taken first. Drinks keep the nearest seat.
2. Stool sitters sit now on the chair clips raised 0.30 m, turned 45° (west turned west, the other two turned east), instead of standing or waiting for a clip.
3. Settled guests are placed already seated, under the intro's first shot. There are 4 on each of Days 1–3: a reader, a laptop worker and a pair. They pay nothing and leave at 30 %, 40 % and 55 % of the day.
4. Looks are dealt least-worn first, and named walk-ins get their name's sex. The default name list is in §3.2.
5. Palette swaps happen only after the line-up photos.
6. 4 in 10 walk-ins look round as they come in; regulars never do.
7. Laptop patrons stay 2×, pairs 1.3×, and arrival jitter is ±60 %.

## Open questions

1. Settled guests on Days 1–3 only (my default), or every morning?
2. His sister: are the names' sexes in §3.2 right?

## Flags

- **The perch clip isn't on the batch-2 list.** The Bible §7 and the common brief §4 say it is. Either add a bar-stool sit to the list (search "stool" and "bar" once) or accept the raised chair sit as the answer.
- **This spec reaches a different conclusion from the 10 Oct seat plan.** That plan said "a tall-stool sit clip is the real fix" (`CafeSeatsSteps.cs:48`). This spec replaces that conclusion, so QA should read §1.3.
- **The Bible §7 misdescribes the 12 Oct fix.** Its line "a stool is claimed only once the customer has arrived" should read "a customer who has reached a stool no longer penalises the stools beside them".
- **The Stations lab can't show seat choice.** It holds the room on purpose.
- **Balance (Progression and the Days 1–3 lead).** Hand-backs and reassures to the bar need no walk round the counter (about 20 m). That is the repair bar's intent since playtest 4, and the Stations lab already assumes it. Longer patron stays shift patron income by roughly ±$5–10 a day on a 180 s day (an estimate). Settled guests add $0.
- **"Layout is cosmetic" (brushed, not broken).** The bar's advantage is physical distance, not a hidden number.
- **Instant seating is a new way into an existing state, not config.** It is about 30 lines in `NpcSeating`. The precedent is `GraceAtHome.SeatNow`.
- **`DayIntro.cs`.** One line goes into Cinematics' file during intro v2; coordinate.
- **Names belong to Content.** The name-sex list is his sister's to correct.
- **The 240 s `maxLifetime` cuts any `StayUntil` stay** on days longer than about 436 s at 0.55.
- **The window counter's three stools** would sit with the same three fields and no turn, because the brackets under its top leave knee space. Then its laptop can return (the 13 Oct note). Not this wave.
- **A reader's book is a prop plus a hold.** Not in this wave.
