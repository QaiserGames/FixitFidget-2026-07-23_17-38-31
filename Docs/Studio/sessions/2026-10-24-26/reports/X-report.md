# X: Mac run fixes (26 Oct 2026). Commit 94207a6 on work-X in /root/wt24/X

All five changed files pass csparse ("clean"). Every edit is marked `// 26 Oct 2026 (Mac run fix)`.

## 1. Chat with a seated guest (lab bug, not a game bug)
**Causes**
- **Gossip 0 at t = 0.** PlayLab runs `Run()` inside its own `Start()`, and Start runs before any Update. "The day is open" is already true there, so the gossip check ran before `GuestGossip.Update` had called `Morning(2)`. Step G's data is fine.
- **Nobody seated.** `SeatOpeningGuests()` returns 0 in any lab session unless `ForceOpeningGuests` is set. The lab called `SeatOpeningGuestsNow()` without setting it, so that call did nothing. Step H's Day 2 guests are real; the game would seat them. That left only a walk-in, who needs 15–20 s from the door, and the lab's first wait was 15 s.

**Fix** (WaitingJobsCheck.cs)
- Sets `ForceOpeningGuests = true` before the first yield, then puts the old value back in a new `Restore()`.
- Waits up to 3 s for exactly 2 untold lines.
- One 25 s wait for a seated guest (settled guest or walk-in), plus a note saying who sat.

Every assertion is as strict as before.

## 2. Save and load (twin data)
**Cause:** the front counter's foot rail, not c12 or a43.
- `BuildMap` reads the counter's MeshCollider bounds. In CK_FrontCounter.fbx (measured in Blender) the mesh has a foot rail on the customers' side, 0.17–0.23 m up, reaching z 13.16. The twin's box stopped at the stone's edge, z 13.30.
- The turned bistro's north stand point (z 12.84) was 0.32 m from the rail. The rules want 0.40 m, so the verdict was NoRoomToSit.
- c12 moves no stand point or collider: FurnitureRules reads `StandPoint.position`, and Approach Side only affects NpcSeating.
- a43 is about 6 m from that spot. It does change the repair-bar stools and their stand points (10 cm in −z).
- Props 1's shelves are behind the counter.

**Patched twin:** `f5/live26.py` (rail plus a43), with `save26.py`, `rules26.py`, `part1_26.py`, `half26.py`, `start26.py` in the scratchpad f5 folder. It reproduces the Mac: NoRoomToSit at (3, 11.5), today's café 38/35, the half-finished café 20/17.

**Fixes**
- FurnitureSaveCheck.cs: the bistro moves to **(3.00, 11.25)** turned. The twin finds it first; the other buys are unchanged.
- FurnitureRulesCheck.cs: pasted data updated (counter z0 13.16, three stools 12.55–12.951, stands z 12.0865).
  - With the true a43 data, two checks would have been wrong. Item 4's bistro at (−3, 11) becomes BlocksAChair (the table edge is 0.73 m from the east stool's stand point). Item 10's ghost at (−4.75, 10.25) becomes NearSeat (0.67 m).
  - They move to the twin's new first spots: **(−3.0, 10.25)**, and **(−4.75, 10.0) turned, folding sheets 2 and 3**.
  - Items 1, 2, 3, 6 and 7 were rechecked in the twin: unchanged.
- FurnitureCheck.cs: only the twin's ghost position in one line of text.

**Expected numbers now:** $1,000 → $1,055 (the $80 bistro is now paid for), 22 seats, 32/32 reached and 29/29 served. Old save 28 seats, 38/35. Rules check: 28 seats 38/35; half-finished café 10 seats 20/17; cram 40 seats, 50/47, no seventh bistro.

## 3. Lab queue and Run In Background (LabQueue.cs)
**Cause:** ProjectSettings has `runInBackground: 0`, so Play Mode freezes whenever another app is in front.

**Fix**
- `Run()` saves `PlayerSettings.runInBackground` in SessionState and sets it true.
- `Finish()` puts the old value back. Every way a run ends goes through `Finish()`: the last entry, stopping by hand, and StopRun.
- Extra safety: the setting is also put back when the editor quits, and on any reload that finds the queue idle with the setting still held.
- One log line when it's held. The summary says so too.
- Nothing else touches his setting.

## Decisions his to overrule
- The chat lab now seats Day 2's settled guests, as the real game does at the open.
- Moving rules-check items 4 and 10 instead of keeping their old spots.

## Risks
- The editor may still slow its own update loop when Unity is not in front, so a queue run might still be slower then.
- The rail's side was worked out from the house axis convention and the intake tray's note ("stone's edge at z 13.30"). The Mac's NoRoomToSit fits it.

## Rerun on his Mac (after the reimport)
1. Checks > Furniture rules: All 12 passed.
2. Lab - Save and load: All 20 passed.
3. Lab - Chat with a seated guest (Day 2).
4. Lab - Place, move and sell: still 33.
5. A short queue run, checking the Run In Background lines at the start and the end, then the five labs that timed out: Stations overhead, Day 1 guide, Juice, Bench 5, Bench 6.
