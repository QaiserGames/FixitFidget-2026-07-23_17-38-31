*The Systems & Mechanics lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

LEAD 1 (SYSTEMS & MECHANICS): PLAYTEST 4 FIXES, REPORT (common.md §8)

All files are complete, under /home/claude/cafe/s7/systems/, and mirror the repo paths. Every C# file passes the syntax check (§8 at the end). Nothing outside my folder was written.

====================================================================
1. WHAT I DID, FINDING BY FINDING
====================================================================

S1. Impatient and Rushed customers can't be saved.
Veteran: "17 of 18 standing waiters stormed out … Only the two standing types stand, and they choose to."
- Fix: step 1 sets the scene's CustomerSpawner.archetypes[Impatient, Rushed].preferredWaitKind from Loiter to Seat. Their patience multipliers stay (Impatient x0.7, Rushed x0.6), so they keep the short fuse in the queue.
- I checked every other source of the wait kind:
  - The NPC "profiles" in Assets/Data/NpcProfiles are NpcMovementProfile assets. They hold no wait kind or patience.
  - The CustomerProfile assets (Regular_Grace, Regular_Officer) already sit.
  - Step 1 would also seat any CustomerProfile set to Loiter. Today there are none.
- Patience after "yes" in seconds, standing → seated. Formula: 45 s × person × day; a seat drains x0.6, standing x1.15.
  - Impatient, Day 3: 27.4 → 52.5 (queue 28.0)
  - Impatient, Day 4: 26.0 → 49.9 (queue 26.6)
  - Impatient, Day 5: 24.7 → 47.3 (queue 25.2)
  - Rushed, Day 4: 22.3 → 42.8 (queue 22.8)
  - Rushed, Day 5: 21.1 → 40.5 (queue 21.6)
  - Rushed is not on Day 3's allowed list.
- Can a player win the race?
  - Yes, if they start that job first. The robot's cracked screen needs about 16 s of hands plus about 6 s of overhead.
  - Reassure adds +12.2 s, then +7.3 s, then +4.4 s for a seated Rushed customer on Day 5, so up to about 64 s. Since S2, Reassure works over the counter to the repair-bar stools.
  - A coffee adds about 10 s.
  - Mansoor's measured repairs (yes → walked out: 41–84 s, median 63 s) are still too slow without reassures.
  - If the human-speed lab still loses most Rushed customers, the one-line change would be Rushed x0.6 → x0.75: Day 5 seated 50.6 s, queue 27 s. I did not change the multipliers.
- Knock-on effects:
  - Bar stools: a customer stands beside one (Snap To Seat off), but its drain is already x0.6, a seat's rate. No fix needed. Step 1 and the Waiting space check assert it.
  - Seats: 24 seats. Patrons max out at 14 and stop arriving when 3 seats are free. Traces peaked at 9–12 seats in use (customers seated: at most 2).
  - With S1, roughly 2–4 customers sit at once. Nobody stands for want of a seat on a normal day, and patrons are not crowded out (they only stop under the stress "fill" test).
  - Sit 4 has two new lines (see §5).

S2. The repair bar's hand-back.
Veteran: "the bench to a repair-bar stool is about 20 m (4 s)"; brief: E reaches across the counter.
- PlayerInteractor:
  - Behind the counter at the till, or behind the repair bar's bench (within 2 m of its stand point, not past it toward the room), E reaches a customer in front of the counter. The reach is acrossCounterReach = 3.5 m, measured flat.
  - The stools stand 3.11 m (middle) and 3.27 m (east, west) from the bench's stand point.
- Priority, from above:
  1. Holding their finished device (IsComplete) or their drink: E hands it over, even at the bench.
  2. Holding an unfinished device, or someone else's: E sets it down on the bench, as before.
  3. Holding nothing: as before (the queue head, the dispenser's close-up). Reassure or "we're out of your drink" goes over the counter only when nothing nearer is in reach.
- First person: the crosshair decides. Behind the bench, the ray reaches 4 m (Counter Reach), and customers in front of the counter can be picked up to that distance. Every other item still needs 2.2 m.
- The overlap buffer for this query is 256 colliders, so a busy bench can't push a customer out of a full buffer.
- Ace's walk for a stool hand-back: about 20 m → 0 m. Lab A/B measures it (see §5).

S3. The day's shape.
Veteran: "Nobody reaches the counter for the first 41 s"; Day 5 phases reached the counter as 0/2/3/1/3.
- People still walk in from the street.
- The spawner now reads phases, featured visits and story visits at "now + the walk" (CounterFraction):
  - The walk starts at walkLeadSeconds = 36 s.
  - It is then learned per session from each street walker's spawn → counter time (moving average, weight 0.3, ignoring anything over 90 s).
  - Because it is in seconds, it works the same at 180 s and 420 s.
- The day's first arrival:
  - Set off 0.5 s into the day, 3 m short of the café door on their own route (CafeArrivals.TryArriveNearTheDoor; never on a crossing or a doorstep).
  - Walks straight to the counter (CustomerBrain.StraightToCounter skips the look round the room).
  - On Day 1 this is Grace: she is the featured regular at 0.
- Last orders still stops new arrivals, unchanged.
- Numbers:
  - Spawn → till for walk-ins is unchanged: 25–52 s, median 38 s.
  - First customer at the till: Days 3/5 were 39–41 s and Day 1 (Grace) about 32 s. Now about 12 s (simulated median 12.9 s).
  - Simulated Day 5 counts at the counter, 180 s: before 0 / 2.0 / 2.6 / 1.9 / 2.1, after 1.0 / 3.0 / 1.2 / 2.8 / 1.7.
- New lab: DayShapeCheck (§5).

S4. Day 1.
- Step 1 sets Day_01_GraceCamera patience x1.4 → x2.0.
  - Grace (x1.15, first visit): queue 64.4 → 92.0 s; seated after "yes" 120.8 → 172.5 s.
  - The Officer (x1.1): queue 61.6 → 88.0 s; seated 115.5 → 165.0 s.
  - Walk-ins: Cheerful queue 67 → 96 s, seated 134 → 192 s; Chatty queue 73 → 104 s, seated 146 → 208 s.
- Spawner: a story visit, featured regular, morning visitor or "word" visitor now comes when it is due.
  - The day's cap limits walk-ins only. A named visit may go past it, up to 6 people in the café (occupancy-and-pacing.md), and still needs a free counter place.
  - Day 1's cap of 2 no longer keeps the Officer out. He is due at the counter at 35% of the day (about 63 s).
  - I found no day this breaks. Days 3–5 have caps of 5–6, and the Day 3 story visit (at 25%) only gains.

S5. "[E] Talk to Walter" while the Officer was first in line.
- Cause: the photo was taken after closing (the recorder had stopped at 180 s). The Officer had reached the queue at 151.9 s and waited unheard for his whole Day 1 queue patience (40 × 1.1 × 1.4 = 61.6 s). He was storming out: the red burst and his parting line are in the photo.
- Someone saying their parting line is IsLeaving, so QueueHead correctly skipped him and offered Walter, the only one still waiting. The player-facing fix is S4 (88 s of queue patience, and he now arrives on time).
- I also fixed a latent ordering bug. QueueHead ordered people by ArrivedAt (the time at the café's door), and the look round the room can reorder them; a Day 3 trace shows it. It now orders by CustomerBrain.ReachedCounterAt (when they reached the counter), falling back to the door time.

S6. The patron stuck 35 s by the front door.
- In trace 150206, patron 1777319958 was walking out through the door while the lab's "Block the door for 15 seconds" was on (about 89.7–104.7 s). They were stuck from 93.1 s to 104.7 s and left at 107.6 s.
- The other stuck flags (124.9–127.7 s) are patrons pressing through the doorway during "Fill every free seat" (24 patrons sent in at 122.5 s). They are 0.1–1.2 s each.
- The veteran's "35 s" joins the two.
- There are no stuck flags outside the stress tests. This only happens under the stress tests, so per the brief: no change.

S7. The patrons' "+$5" pop.
- PatronSpawner.ComeIn no longer calls Juice.Money (the pop over the patron). The $5, AddMoney and DayClock.RecordPatronIncome stay.
- The HUD's cash stack still pops "+$5" for any rise in DayClock.Earned, and that file belongs to the UI lead (§6a).
- JuiceCheck does not expect the patron pop: it fires Juice.Money itself. Its HUD part pays through RecordPatronIncome(25) (§6b).

S8. The lab autopilot.
- CafeLab now works the Human fault the way a player does, through the game's own calls:
  - The phone is taken; after 1.5–3 s, HumanFault.Flip.
  - After the 1.2 s confirmation, CompleteCounterRepair.
- It now works the support line the same way:
  - Dial (HoldCallJob.Activate) after 2.5–6 s, redialling a missed call.
  - Answer within its ring.
  - Pick the phone up off the shelf (PlayerCarry.TryPickUp), then CompleteJob.
  - Payments go through the game's own paths.
- Human speed (CafeLab.HumanSpeedKey):
  - Repairs 45–90 s, drinks 20–40 s, a 3–6 s reaction.
  - One pair of hands: a job starts when the last one ends, and talking at the counter takes 4 s of the hands.
  - Menu: Fixit Fidget > Café life > Lab > Play a lab session - Day 5, autopilot at human speed.
  - The key is read once, so the existing lab menus keep their old behaviour.

S9. Anything else in my area.
- StationsCheck:
  - Total presses and the conversation's share are now reported, not asserted.
  - The old check never asserted 9; it asserted only station presses. It still asserts those, per visit.
  - It now reads like a player: it waits for the speech and the replies to come up by themselves (up to 12 s) and presses "hear them out" only if they don't.
  - Finding for the Content lead: lines already advance at reading pace (ConversationUI, 1.4–4.5 s a line) and the replies appear by themselves. The veteran's "hear them out, hear them out" was the check pressing every 0.45 s. A patient player needs 2 presses (talk, take).
- Seats are never short, so the seat squeeze never fires (veteran). Not changed: flagged in §7.
- Grace walking out on Day 1 is answered by S3 + S4 (at the till about 12 s, queue patience 92 s).
- Not mine:
  - Upgrade prices (economy).
  - FocusNameUI's floating name (UI).
  - The intake shelf object (Tray).
  - Review stars (UI).

====================================================================
2. DECISIONS, AND DECISIONS FOR MANSOOR TO OVERRULE
====================================================================
1. Reach over the counter is 3.5 m, not "about 3.2". The outer stools are 3.27 m from the bench's stand point, so 3.2 would miss them. It is a serialized field (PlayerInteractor.acrossCounterReach). Nothing else seated in the café is within 3.5 m of the till or the bench: the nearest are 5+ m away.
2. E's priority at the repair bar (§1, S2). An early, deliberate hand-back of an unfinished device to a stool still needs a walk round, or the crosshair in first person, because E at the bench sets it down. That is per the brief.
3. The opener appears on the café's own pavement 0.5 s into the day, in the overhead frame. At the 34 m opening camera height they are a few pixels, and the day has just begun, so it should read as "already there". If it looks like a pop on the Mac, raise CustomerSpawner.openerMetresOut (8 m costs about 4 s at the till). 0 restores the old opening.
4. The walk lead (36 s, learned) is per session, not saved.
5. Named visits may exceed the day's cap up to 6 (S4). The brief's fallback, Day 1 cap 3, was not needed.
6. DayShapeCheck asserts slightly softer rules than the brief's strict reading:
   - Asserted: each rush brings at least as many to the counter as the lull, and the busiest phase is a rush.
   - Reported as a note: the strict reading (the rushes are the two busiest phases and the lull the quietest after the opening).
   - Why: a 180 s day brings about ten people, one to three per phase. In simulation, the strict reading holds in about 77% of 180 s runs and 87% of 420 s runs (it was 17% before). The asserted rules hold in about 96% and 99%.
7. S1 multipliers unchanged (the Rushed alternative is in S1). S6: no change.

====================================================================
3. FILES (all under /home/claude/cafe/s7/systems/)
====================================================================
- Assets/Scripts/CustomerBrain.cs — ReachedCounterAt (seconds into the day); StraightToCounter (the opener skips the look round the room).
- Assets/Scripts/CustomerInteractable.cs — Brain, HandOverReady, HelpAcrossTheCounter.
- Assets/Scripts/PlayerInteractor.cs — across-the-counter reach (StaffSide, BehindTheBench, AcrossTheCounter, AcrossTheCounterTarget; acrossCounterReach 3.5, benchRange 2), the FindBest priority, the first-person ray behind the bench, QueueHead by counter time, gizmos. FindFloorTarget(Collider[]) and the other reflected names are kept.
- Assets/Scripts/CustomerSpawner.cs — scheduling by lead time (CounterFraction, LeadSeconds, LeadSamples, Today), the opener, named visits past the cap, the WhoIsDue refactor, "phase at the counter" log. Reflected names are kept.
- Assets/Scripts/CafeArrivals.cs — TryArriveNearTheDoor / BeginArrivalNearTheDoor (additions only).
- Assets/Scripts/PatronSpawner.cs — no Juice.Money pop for patrons.
- Assets/Scripts/Diagnostics/CafeLab.cs — the autopilot does the Human fault and the support line; human speed.
- Assets/Scripts/Diagnostics/DayShapeCheck.cs (new) — the day's shape lab.
- Assets/Scripts/Diagnostics/StationsCheck.cs — patient reading; presses reported; the repair leg's customer waits at a stool and gets the device over the counter; a new third leg (a drink over the bar); stool geometry notes; the press report also on an early stop.
- Assets/Editor/Playtest4SystemsSteps.cs (new) — step 0 and step 1, lab menus A–E, the human-speed session menu, cleanup of stale lab requests.
- Assets/Editor/NpcSitLiveCheck.cs — two playtest-4 lines: every waiting customer would rather sit; nobody stood while a seat was free.
- Assets/Editor/WaitingSpaceChecks.cs — after the reservation test, the open café scene's waiting rules: every archetype and regular sits, every seat drains x0.6 or less, the repair-bar stools are within reach over the counter.

Mine but unchanged after checking:
- StreetDoorRushCheck.cs: it pauses both spawners and uses only CafeArrivals' editor routes, which are unchanged.
- CustomerDeliveryHandsChecks, CarryInteractionChecks, HoldCallIntegrationChecks, HumanIntegrationChecks, FeaturedRepairChecks: the reflected names they use are kept.
- NpcJourney, PatronBrain, CounterQueue, WaitingSpot, TableSeat, CafeLifeProbe, the day assets and the profiles (the profiles are only touched by step 1).

====================================================================
4. UNITY STEPS IN ORDER (Edit Mode, café scene open and saved)
====================================================================
1. Compile. There are no new asmdefs.
2. Fixit Fidget > Playtest 4 > Systems > 0 - Check first (read-only). Logs/Playtest4/systems-0-check-first-last.txt lists:
   - Impatient and Rushed "waits standing <- step 1 seats them".
   - Every archetype's seconds per day.
   - Day 1 = Assets/GraceShowcase/Day_01_GraceCamera.asset at x1.4, cap 2, featured Regular_Grace at 0, story visit Regular_Officer at 0.35.
   - Grace's and the Officer's numbers at x1.4 and at x2.0.
   - 28 places in use: 24 seats and 4 standing spots.
   - The 6 bar stools at x0.6; the repair bar's three at 3.11 / 3.27 m from the bench's stand point.
   - The new fields: walkLeadSeconds 36, leadLearning 0.3, openerMetresOut 3, acrossCounterReach 3.5, benchRange 2.
3. Fixit Fidget > Playtest 4 > Systems > 1 - Seat the impatient and rushed, Day 1's patience (scene + assets). Expect "DONE: 3 change(s)":
   - archetypes[1] (Impatient) 0→1 and archetypes[3] (Rushed) 0→1.
   - Day_01_GraceCamera patienceMultiplier 1.4→2.
   - Every check passes, and the asset is saved.
   - The step refuses if the scene's Day 1 is not Day_01_GraceCamera.
   - Then File > Save the scene. A second run says "NOTHING TO CHANGE: already done".
4. Fixit Fidget > Checks > Waiting space reservations. The Console shows "PASS: shared occupancy…" then "The café scene: N passed, 0 failed". Before step 1 it fails on Impatient and Rushed, by design.

====================================================================
5. LABS IN ORDER
====================================================================
A. Fixit Fidget > Playtest 4 > Systems > Lab A - Stations and the repair bar, from above (drives itself).
   - Same as the existing Playtest > Stations as reach - play check, overhead. About 2–2.5 min.
   - Output: Logs/Stations/stations-overhead-<time>/report.txt.
   - Pass: every line passes, including:
     - "behind the bench, A is for <name> at the stool across the counter, 3.1–3.3 m away … "Hand it back"";
     - "no step taken for it: Ace walked 0.00 m", and the same for the bar drink ("Serve the Coffee");
     - "at the bench A sets it down (<name> waits across the counter … in E's reach: True …)";
     - at most one station press per visit.
   - Overhead photos for S2: repair-over-the-counter.jpg and bar-drink-over-the-counter.jpg.
   - Presses reported: expected about 8 for a drink (2 the conversation's), 7 for the repair, 8 for the bar drink.
   - What a player sees: the prompt over the stool customer while standing at the bench, and a single press.
B. Lab B - Stations and the repair bar, first person (drives itself). Same check, about 2–2.5 min, 0 station presses.
   - If the over-the-counter line fails, its notes say what the crosshair's ray hit (the clamp lamp, for example).
C. Lab C - The day's shape at the counter, Day 5 (the scene's 180 s day). About 3.5 min.
   - Output: Logs/CafeLife/day-shape-<time>/report.txt.
   - Pass: the first customer at the till within 15 s (about 12 s expected; was 39–41 s); each rush ≥ the lull; the busiest phase is a rush.
   - The report also has the per-phase counts, every arrival time and the strict reading as a note.
   - Photos: 01-the-first-customer-at-the-till plus the middle of each phase.
D. Lab D - … (the ship's 420 s day). About 7.5 min. Same pass rules.
E. Lab E - … (autopilot at human speed). About 3.5 min. Same pass rules.
F. Fixit Fidget > Café life > Lab > Play a lab session - Day 5, autopilot at human speed.
   - Console: "Autopilot on, at human speed (repairs 45-90 s, drinks 20-40 s, reacting in 3-6 s, one job at a time)". The LAB banner says "autopilot ON, at human speed".
   - Record it (Café life > Record 3 minutes) for S1's real verdict: do seated Impatient and Rushed customers survive?
G. Existing labs to re-run, and what a pass looks like:
   - Café life > Lab > Play a lab session - Day 5 / Day 3, autopilot serves (plus the stress menus). The Console notes "flipped X's mute switch", "dialled/answered X's support line", "gave X their phone back". DayLogs/CafeLab show the Human and support-call customers served, not stormed out. Impatient and Rushed customers wait in seats. The first customer reaches the till at about 12 s. There are no "+$5" pops over patrons.
   - Fixit Fidget > NPC > Sit 4 - Live sit check (Play Mode), inside "Café life > Lab > Play a lab session - Day 5, I serve". The old lines pass, plus "Every waiting customer would rather sit … (n of n)" and "Nobody waited standing while a seat was free (0 stood)".
   - Fixit Fidget > Checks > Street doors - rush hour at the shop door / at the dusty rose door (Play Mode, lab session). Passes as before.
   - Fixit Fidget > Checks > Dispenser interaction and visible filling; Support call integration; Human counter integration; Customer delivery from either hand; Two-hand bench placement and carry state; Featured repair requests. All pass as before (fixtures and reflection names unchanged).
   - Fixit Fidget > Playtest > Aim help - play check; Controller check. As before: the ray only lengthens behind the bench.
   - NightZeroCheck (Content's). Grace now arrives on Day 2 as the opener, about 12 s to the till; the check waits up to 60 s and 150 s, so it passes, faster.
   - JuiceCheck (Bench's) and PauseCheck (UI's) pass with my change. See §6b.

====================================================================
6. NEEDS ANOTHER LEAD'S FILE
====================================================================
a) UI lead, Assets/Scripts/HudCorners.cs (S7: patrons pay quietly; without this the HUD still pops "+$5" for every patron).
   - Add a field next to line 63:
       int patronIncomeSeen;   // Playtest 4 (S7): today's patron money already counted, quietly (DayClock.PatronIncome)
   - In UpdateCash(DayClock clock), after `int money = …;` add:
       int patrons = Mathf.Max(0, clock.PatronIncome);
   - Replace the "Paid" branch body's first line `Paid(money - targetMoney, now);` with:
       // A patron's coffee counts up in the figure, no pop, jolt or drop: those are the player's own pay.
       int quiet = Mathf.Clamp(patrons - patronIncomeSeen, 0, money - targetMoney);
       if (money - targetMoney - quiet > 0) Paid(money - targetMoney - quiet, now);
   - After the if/else-if block (before `if (countSince > -5f)`) add:
       patronIncomeSeen = patrons;
   - The new-morning branch needs nothing: PatronIncome resets to 0 and the line above re-syncs it.
b) Optional. Bench lead's Assets/Scripts/Diagnostics/JuiceCheck.cs (line 215) and UI lead's Assets/Scripts/Diagnostics/PauseCheck.cs (line 91).
   - Both still pass after (a), because the figure counts up quietly. But their "40-money-counting-up" and "02-paid" photos would no longer show the pop chip.
   - To keep photographing a real payment, replace `DayClock.Instance.RecordPatronIncome(25);` with `DayClock.Instance.RecordServed(25, 0, false);`, and `clock.RecordPatronIncome(30);` with `clock.RecordServed(30, 0, false);`.
   - Side effect: +1 drink served in the lab day's recap.
c) Content lead, through their step. With S1, Impatient waits seated, so their orderedDrink line contradicts the seat.
   - Scene CustomerSpawner, SerializedProperty "archetypes.Array.data[1].lines.orderedDrink.Array.data[0]".
   - Now: "Coffee. While I'm standing here."
   - Suggested: "Coffee. While I wait." (PLACEHOLDER COPY).
   - Rushed's lines are fine seated.

====================================================================
7. OPEN QUESTIONS AND FLAGS
====================================================================
- The rushes are partly limited by the counter itself. Its 3 places are reserved for people still walking over (about 36 s), so while all three are taken, new arrivals pause. That allows at most about one arrival per 14 s, against rushes authored at 6–7 s.
  - Options for Mansoor (not done, because they change the queue rules): reserve a place only from the door, or start walk-ins nearer.
- The seat squeeze ("the café competes for your space") still won't fire on a normal day: 24 seats, at most 14 patrons, 3 kept free. I didn't tighten it: that would put customers back on their feet, which is S1's problem. His call after a human day.
- "No new mechanics": the across-the-counter reach is the existing E verbs at a longer reach behind the counter, and the opener is the existing walk started nearer the door. No new verb, resource or loop.
- Locks untouched: Grace's Day 1 latte suppression (drink wishes are not changed), and the Night 0 deal.
- The day-shape assertions are statistical (§2.6). One failing run is not proof; re-run or use Lab D (420 s) before calling it.
- Brief note: the bar stools in the scene are TableSeats with their stand points at z 12.19. The brief's CK_Stool_Bar z 12.85 is the stool mesh.

====================================================================
8. SYNTAX CHECK (/home/claude/cafe/s7/tools/syntaxcheck, every delivered file)
====================================================================
OK    ./Assets/Editor/NpcSitLiveCheck.cs
OK    ./Assets/Editor/Playtest4SystemsSteps.cs
OK    ./Assets/Editor/WaitingSpaceChecks.cs
OK    ./Assets/Scripts/CafeArrivals.cs
OK    ./Assets/Scripts/CustomerBrain.cs
OK    ./Assets/Scripts/CustomerInteractable.cs
OK    ./Assets/Scripts/CustomerSpawner.cs
OK    ./Assets/Scripts/Diagnostics/CafeLab.cs
OK    ./Assets/Scripts/Diagnostics/DayShapeCheck.cs
OK    ./Assets/Scripts/Diagnostics/StationsCheck.cs
OK    ./Assets/Scripts/PatronSpawner.cs
OK    ./Assets/Scripts/PlayerInteractor.cs
