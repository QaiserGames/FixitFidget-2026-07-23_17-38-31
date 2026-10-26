# Builder N report: Bench 8, the pad at any frame rate (26 Oct 2026)

`/root/wt24/N`, `work-N`, commit `88ad33e`; `csparse.py` clean on all four files.

## 1. The files
`Diagnostics/ClumsyHand.cs` and `Diagnostics/ClumsyBenchLab.cs` (the hand and its lab); `ItemInspector.cs` and `BenchRules.cs` (the pad's "held still" rub test).

## 2. The causes
**Parity (4 checks)**, in the lab's hand; the game's pad path is time-based.
- The same seed chose differently at 15 fps, pad only (phone "Press in": 1 press at 60, 3 at 15): `StickReach.Update` drew each push from the decisions stream, and the number of pushes depends on the frames.
- Steps with identical presses still differed by 75 % (watch debris Unscrew 13.6 / 23.9 s): the stick let go on a frame boundary, a frame before the game applies it; at 15 fps that is up to 150 px past the 60 px snap, then slow corrective pushes of 1-2 s. Ported (`reach_sim.py`): a 400 px reach 1.13 s at 60, 1.66 at 15.
- The game's one per-frame test: `Rub()` called the pad still under 0.5 px a frame (30 px/s at 60 fps, 7.5 at 15).

**Watch debris, 4 strokes (2 checks).** One brush spot, one cloth spot; the median of two is the larger. Ported (`scrub_sim.py`): the cloth cleans in 0.75-0.77 s at both rates, 2.5 sweeps; the count reads 4 because it runs from the plan, charging the thumb's quarter-cycle from the spot's middle to the first end, free for the mouse's hand.

**The hidden frame (1 check).** "Broken" is the phone's old screen in the first scrap slot. The phone, lifted 48 mm, turns over in "Cover on" and on paper (`flip_geom.py`) crosses the camera's line to it for ~5 ms: a 60 fps frame a third of the time, hardly ever at 15.

## 3. The fixes
- Each reach and scrub gets its own `System.Random`, seeded by one draw from the decisions stream.
- The let-go is placed in continuous time (the crossing extrapolated at the speed seen, plus the late delay); the push's last frame sends a stick sized to the part of the frame before the release, back through the dead zone and curve. Simulated: 400 px 1.17 / 1.22 s; four-reach steps over the bar: 0 of 300 (22 before).
- The pad's strokes count from the first end, a quarter-cycle in: cloth 3, brush 2. The bars stand.
- `Blocked()` skips the device's colliders only while `inspector.Flipping`, as it skips flights; a hidden piece is reported with the blocker's name, distance and step.
- `Rub()`: still = under `BenchRules.PadStillPxPerSecond` (30).

## 4. Not provable here
The hidden frame's cause rests on the slot order, the FBX's x sign and the camera; if it is something else, the check still fails and names it. The rest is from ports, not Unity.

## 5. On his Mac, in order
1. `Bench/Bench 8 - The clumsy hand`: `[Clumsy hand] All 220 checks passed.`; the four parity lines, watch debris / pad strokes ≤ 3 and phone / mouse / 60 "0 hidden" (else the line names the blocker).
2. `Bench/Bench 3`, `5`, `6` (Bench 7 is a scene step): all pass, as on 23 Oct.
3. `Playtest/Juice`, then `Playtest/Aim help`: all pass.

## 6. His to overrule
- The stroke count and the flip exclusion are measuring changes, not bar changes; a line each to revert.
- Pad feel: a thumb resting just off the dead zone over dirt at 15 fps now rubs in place after 0.15 s, as at 60.
- Seen, not changed: the pad's near-miss press never fires (`JustLetGo` and `PadCursor.Gliding` never coincide).
