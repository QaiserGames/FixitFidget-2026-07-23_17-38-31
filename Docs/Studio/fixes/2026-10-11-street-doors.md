*The Systems & Mechanics lead's report to the Producer, 11 October 2026, as returned: the street doors' overnight failure at the dusty rose door, its diagnosis and the fix (commit `37ae37b`). Paths under `/home/claude/cafe/s7/doors/` are the cloud workspace's; the Python port it mentions stayed there.*

**Dusty rose door rush: diagnosis and fix (Systems)**

The fix is two files under `/home/claude/cafe/s7/doors/fix/`. It removes the standstill. It will not make the check pass every time: about 1 run in 6 can still fail on "Nobody bunched up" in the first 30 s, for an older reason that this fix doesn't touch (see 6.2).

**1. Diagnosis**
- **What happens.** People walking back from the door's waiting spots get stuck a step from the start of the single-file stretch at (−16.45, 2.40). Nothing in the code ever gets them moving again.
- **How it forms.**
  - StreetDoor lets people going the same way follow each other 1 s apart (StreetDoor.cs lines 59 and 323). So 2 or 3 people waiting on the spots or in the line get their turn at almost the same moment.
  - Each walks straight to the same start point (`NpcJourney.Rejoin`, HEAD lines 656–676). They only count as arrived within 0.2 m of it (line 660).
  - Stepping round each other, one gets pushed past the start. It then tries to walk back to the start through the others.
- **Why it never clears.** `Rejoin` resets the progress clock every frame (line 671) and steers without `Advance` (line 672). So the squeeze after 5 s (line 726) and the help-on after 14 s never happen.
- **00:25 trace.**
  - Going home 1, 2 and 3 got their turns between 174.8 and 177 s.
  - Going home 1 was on the stretch line 0.29 m past the start at 177.1 s, then stopped at (−16.82, 2.07). Going home 2 stopped at (−16.63, 2.86) and going home 3 at (−16.18, 2.33). All three were within 0.5 m of the start; none got within 0.2 m.
  - The HELD lines from 181.8 to 242.5 s read "still 63.8s … stuck 0 … door their turn".
  - They stood 85, 55 and 55 s until the 2-minute backstop sent them home (the report shows 7 backstops).
- **00:30 trace.**
  - Walk-in 2 stopped on the stretch line 0.35 m past the start, at (−16.80, 2.46), at 77 s (events line 8, "stuck 0").
  - Walk-ins 2 and 5 stood 43.8 s and 43.3 s. All 15 bunching moments in that run were this knot.
- **Street walkers make it worse; they don't start it.** Their loop passes 0.25 m from the start point and through both waiting spots. They get boxed in and stand: 33–72 s on one spot at 00:25, and 44–47 s at 00:30, one of them on the stoop itself for 43 s.
- **Yes, it's flaky. The numbers:**
  - Real runs: 0 of 2 passed at HEAD, against 2 of 2 before (27 Sept and 10 Oct 15:39). Two failures out of two only tells us HEAD fails at least 16% of the time (95% lower bound).
  - I ported the door's walking rules to Python and restarted it 30 times, with small random jitter, from the real trace states.
    - From the 00:25 state at 174.8 s, HEAD stood someone still for more than 25 s in 2 of 30 runs, and bunched in 7 of 30.
    - From inside the 00:30 knot, HEAD failed to clear it within 25 s in 17 of 30 runs (median 29 s; 6 of 30 needed a teleport).

**2. Caused or exposed by tonight**
Exposed, not caused.
- None of `NpcJourney.cs`, `StreetDoor.cs`, `StreetLife.cs`, `HomeDoor.cs` or `StreetDoorRushCheck.cs` is in the 52c70a5→HEAD code diff.
- The only scene changes near the door are Grace's three lights and a wheel stop. None of them is something these walkers react to, and these walkers don't use the NavMesh.
- At 00:25 the knot formed at 177–179 s. No café visitor came within 8 m of the door until 193.1 s, so the rush's own ten walkers and three street walkers did it alone.
- Tonight did add people at this door mid-rush. The spawner's 36 s walk lead and the opener (af735cb) shift when café customers walk home along the dusty rose route.
  - At 00:30, two customers and a patron went in through that door during the rush.
  - One of them, most likely the opener (inside the café by 16.6 s, left at 47.1 s), went in at 77.9 s just as the knot set.
  - Another walked into the knot at about 94 s and stood 34 s.
  - So tonight meant more people in the knot; the bug itself was already there.

**3. The fix, and why this one**
All of it is in `NpcJourney.Rejoin`:
- **Already on the stretch counts as arrived.** Anyone on the stretch's first segment, past its start and within 0.25 m of its line, is back on the path and walks on in. Both stuck walkers above (00:30 walk-in 2 at 77 s; 00:25 going home 1 at 177.1 s) would simply have gone in.
- **The walk back can now count as stuck,** the same way it does on the path. Getting no nearer (by 5 cm) starts the clock: after 5 s they squeeze past, and after 14 s they are put on the start if nobody stands on it (that counts as a teleport). The clock starts when the turn is given.
- **Trace label.** Someone walking back from a spot now shows "door their turn, back from their spot".

No new mechanic. Turn rules, spots, StreetLife, the scene, Grace's Day 1 latte and the Night 0 deal are untouched.

Port results, 30 runs each, HEAD → fix:

| Starting point | Measure | HEAD | Fix |
|---|---|---|---|
| 00:25 state at 174.8 s | worst standstill | 82.6 s | 6.3 s |
| | standstill over 25 s | 2/30 | 0/30 |
| | everyone home | not in every run | every run, within 22.8 s |
| | bunching | 7/30 | 4/30 (all in the first 0.7 s, already in the starting state) |
| | street walker on one spot | 57.5 s | 14.3 s |
| Inside the 00:30 knot | cleared within 25 s | 13/30 | 30/30 |
| | median / worst time to clear | 29.2 s / never | 5.0 s / 10.0 s |
| | teleports | 6/30 | 0 |
| | bunching | 23/30 | 1/30 |
| | street walker on one spot | 110 s | 4 s |
| Queue: door busy the other way for 25 s, 7 people coming home | worst standstill | 88.5 s | 18.4 s |
| | not all home in 120 s | 3/30 | 0 |
| | bunching | 4/30 | 0 |
| | teleports | 1/30 | 0 |

From a cold start (whole rush, 40 runs), the one knot (158 s, 9 people not home) is gone. The other 7 failing runs are identical with or without the fix (see 6.2).

The port cannot predict how often the knot forms. From a cold start it knotted in 1 of 40 runs, while the real game knotted 2 of 2 tonight. It is a comparison tool, not a probability estimate.

Alternatives (one line each):
- **Walk back from the spots in turn order:** built and tested, dropped. People waiting their turn stood in the path of the person they were waiting for (queue case: 3/30 runs never got everyone home, a 97 s standstill, bunching 17/30 against 3/30 at HEAD).
- **Street walkers pick a clear side and turn back after 6 s boxed in:** built and tested, dropped. No gain once the knot can't form, and street walkers came within 0.42 m of a visitor in 10/30 runs against 7/30.
- **Wider "on the line" (0.45 m):** better inside the 00:30 knot (worst 5.7 s) but worse from 00:25 (worst 20.9 s, and one run took 59 s to get everyone home). Kept 0.25 m.
- **Keep café customers out of a door mid-rush:** 00:25 knotted without any, and it changes routing.
- **Teleport sooner:** still a standstill, and the check fails on any teleport.
- **Admit one at a time from the spots (StreetDoor):** slows every door, including the shop door's 12/12.
- **Move the dusty rose spots off the walkers' loop:** right in the long run, but it's scene work (see 6.1).

**The check had a hole.** It only judged the rush's own ten people.
- Café visitors within 8 m now count for "stood still over 25 s" and for teleports.
- Street walkers within 8 m now count for "over 25 s on one spot" (within 1.2 m, shuffling included).
- Walkers are told apart the way StreetLife does it: legs, or an Animator with the IsWalking switch. That excludes the two passing birds.
- At 00:30 this would have flagged the café customer's 34.4 s and the street walkers' 34.5–43.5 s. At 15:39 nobody went over 8 s, so a good run stays good.
- Still 12 checks.

**4. Files written**
- `/home/claude/cafe/s7/doors/fix/Assets/Scripts/NpcJourney.cs` (HEAD plus changes at lines 62, 121–124, 616–621, 663–726)
- `/home/claude/cafe/s7/doors/fix/Assets/Editor/StreetDoorRushCheck.cs` (HEAD plus changes at lines 41–51, 63–65, 87–96, 194–197, 209, 223, 241, 295–327, 357–368, 375–376, 379–386, 415–439)

Both are whole files and pass `syntaxcheck`. They also compile together (Roslyn, C# 9, UNITY_EDITOR) with HEAD's `StreetDoor.cs` and `StreetLife.cs` plus stand-ins for Unity's types. Everything the Editor-side check uses on `NpcJourney` and `StreetLife.Actor` is public. `StreetLife.cs` is unchanged; no scene or Inspector change.

The Python port behind the numbers is in `/home/claude/cafe/s7/doors/port/`. To rerun: `python3 -I k25.py 30 old,rejoin`, `knotens2.py 30 old,rejoin`, `queue.py 30 25 old,rejoin 7`, `fullrush.py 40 old,rejoin 0`.

**5. Mac test steps and expected results**
1. Copy `fix/Assets/Scripts/NpcJourney.cs` over `Assets/Scripts/NpcJourney.cs`, and `fix/Assets/Editor/StreetDoorRushCheck.cs` over `Assets/Editor/StreetDoorRushCheck.cs`.
2. Outside Play Mode, run `Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)`. Expect no Console errors.
3. Run the dusty rose rush in a fresh session:
   - Start `Fixit Fidget › Café life › Lab › Play a lab session - Day 5, I serve`.
   - Once the café is open, run `Fixit Fidget › Checks › Street doors - rush hour at the dusty rose door (Play Mode, lab session)`.
   - Expect **12/12** with 0 teleports and 0 backstops, and the longest standstill under about 10 s.
   - New report lines: "None of the café's own visitors…" (or a list of them), and "Street walkers who came by: N; the longest any of them stayed on one spot…".
   - In the arrivals trace, people walking back from a spot read "door their turn, back from their spot", and their "stuck" value counts up (before, it was always 0).
4. Exit Play Mode and repeat step 3 in two more fresh sessions (three runs in all). Expect 12/12 in at least two of them.
5. In a fresh session, run `Checks › Street doors - rush hour at the shop door (Play Mode, lab session)`: expect 12/12.
6. Run `Checks › Street doors (Play Mode, lab session)`: expect 25/25.
7. Run `Checks › Grace's home (Play Mode, lab session)`: expect 20/20 (her door uses the same code).
8. Stress test from the café side:
   - Start `Café life › Lab › Play a lab session - Day 5, autopilot serves`, then `Café life › Record 8 minutes - dusty rose door camera (west street)`.
   - Run `Lab › Fill every free seat with patrons`, and about 60 s later `Lab › All patrons leave now`.
   - Expect nobody standing at the dusty rose step for more than a few seconds, and people from the two spots going in one after another.
9. Optional: `Fixit Fidget › Night › Night 0 - Play check (lab, drives itself)` should give the same result as before.

How to read a fail:
- **Not this bug:** a fail only on "Nobody bunched up", with the first moment in the first ~30 s at the corner below the stoop (x −15.3 to −15.6, z 1.4 to 2.9) while walk-ins are coming out. That's the older flake (6.2).
- **The fix didn't hold:** anyone standing more than 25 s within a metre of the stoop, any teleport, or any backstop. Send me the report and its arrivals-trace folder.

**6. Other things wrong on the street**
1. The dusty rose waiting spots and the stretch start sit on the street walkers' loop: spot A is 0.24 m from the southbound lane (x −16.2), spot B 0.12 m from the northbound lane (x −15.6), and the start 0.25 m. Doors 4 (`StreetDoorSteps.SpotProblem`, line 1084) only checks road lanes.
2. Corner crowding while the walk-ins come out:
   - The overflow queue stands on the east half of a 0.75 m pavement (x ≈ −15.40, z 1.5 / 0.55 / −0.40), right where the walk-ins come round the corner.
   - The 00:25 run had 4 bunching moments there, 11.6–29.4 s in, before the knot.
   - In the port, 6 of 40 cold-start runs fail on this, with or without the fix.
   - Queueing 1 m further back cut it to 1 of 40, but caused a 79 s standstill elsewhere. It needs its own look.
3. Street walkers sidestep a fixed ±0.55 m without checking that side is clear, and never give up. At 00:30 one stood 43 s on the dusty rose stoop, inside the stretch, at (−16.75, 2.55); another shuffled side to side for 44 s at (−16.15 to −16.42, 2.92).
4. Someone walking along a path who inches forward never counts as stuck, because each 5 cm of progress resets the clock. At 00:25, walk-in 1 spent about 24 s at the corner (−15.06 to −15.43, 2.03 to 2.14) without squeezing past or being helped on.
5. The arrivals trace's HELD lines print the path's next point as "heading for" for people walking back from a spot (they show −15.7, 2.4 while heading for −16.45, 2.4). That's in `CafeArrivalsRecorder`, which I didn't change.

For the notes: after this fix, about 15% of dusty rose rush runs can still fail, on corner crowding (6.2), not the knot.