# Builder N: the bench on a pad, the same at any frame rate (26 Oct 2026)

You are the gameplay engineer for Fix It Fiasco's repair bench: a scripted, first-person close-up where Ace unscrews, pries, brushes and swaps parts with a cursor driven by the mouse or a PlayStation pad. His girlfriend plays on the pad. Bench 8, "The clumsy hand", drives every repair with a deliberately imperfect hand, with the mouse and the pad, at 60 and at 15 fps. It has failed the same 7 of 220 checks since 23 Oct. Find out why, fix the cause, and make it pass without weakening it.

## Read first
- `/root/review24/builders-common.md`: the house rules. Two changes since it was written:
  - his Mac's current scene and prefabs are in `/root/ref26` (read-only), not ref24;
  - you commit in your copy (branch `work-N`); that wins over "commit nothing".
- **The lab:** `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs`.
- **Today's full report from his Mac:** `/mnt/user-data/uploads/FixitFidget/Logs/BenchClumsy/2026-10-26_080613/report.txt` (417 lines). The failures:
  ```
  FAIL  Phone screen / mouse / 60 fps: no piece lost or hidden for a frame (0 lost, 1 hidden over 1 frames)
  FAIL  Watch debris / pad / 60 fps: strokes per spot median 4 (≤ 3), most 4 (≤ 6), 100 % within 4 (≥ 90 %)
  FAIL  Watch debris / pad / 15 fps: strokes per spot median 4 (≤ 3), most 4 (≤ 6), 100 % within 4 (≥ 90 %)
  FAIL  Camera swap / pad: 60 and 15 fps within 25 % per step beyond whole frames: Press in 5.3/3.4 s
  FAIL  Phone screen / pad: … Press in 10.3/5.8 s, Cover on 3.1/6.1 s, Screws back 5.9/13.8 s
  FAIL  Watch seized / pad: … Brush 5.5/14.2 s, Lift out 5.2/3.4 s, Press in 2.5/6.8 s, Cover on 7.1/3.2 s, Screws back 16.4/25.1 s
  FAIL  Watch debris / pad: … Unscrew 13.6/23.9 s, Pry off 3.4/4.9 s, Screws back 14.6/20.1 s
  ```
- **The strongest clue:** every parity failure is on the **pad**; the mouse passes. The pad's times differ in both directions, some slower at 15 fps and some faster. So look for:
  - anything the pad path does per frame rather than per second: stick acceleration or smoothing, a snap or dwell counted in frames, a repeat rate, an aim-help settle, a lerp without `deltaTime`, a `Time.deltaTime` clamp, `FixedUpdate` against `Update`;
  - the lab's own simulated pad hand: is its wobble, aim or randomness drawn per frame, so 15 fps gets a different hand than 60?
  - In both, a fair comparison is the same hand in seconds. Making the lab's hand frame-rate independent is a fix, not a weakening; changing its bars is.
- The bench's code: grep `PadInput`, `BenchCursor` or the bench's cursor, `AimHelp`, `GrimeSpot` (the scrub), `Screw`, `ReplaceablePart`, `RepairChecklist`, `ToolPickup`, `LoosePart`. Read each file's header first: they explain the forgiving bench that was tuned with him in playtests 5–7 (the feel targets are in `Docs/Studio/`; grep "strokes per spot").

## The three problems
1. **Pad parity (60 against 15 fps).** Find each frame-rate dependency in the pad path and make it time-based, so a step takes the same seconds at 15 fps as at 60, within the lab's 25 %.
   - Explain each cause in plain words in your report.
   - If the lab's hand is the frame-dependent part, fix the hand and say so.
2. **The pad's watch debris: 4 strokes per spot against the target of 3.**
   - The mouse does it in 2–3, and the camera clean on the pad in 3.
   - Find why the watch's debris needs one more stroke on the pad (its spots' size, the pad cursor's speed across them, how a stroke is counted), and tune that job or the pad's scrub so the median is ≤ 3. Don't change the mouse's feel.
   - **Careful:** a serialized value in the scene or a prefab overrides a code default. Grep `/root/ref26` (the scene `Assets/Playtests/AcesCafeLayoutPlaytest.unity` and the prefabs) for any field you tune. If it is overridden there, change it with an editor step (house style, with Undo, ending "DONE"), not by editing the default.
3. **"1 hidden over 1 frames"** on the phone screen with the mouse at 60 fps. Find which piece hides for a frame and why (a parent change, a pool, a camera-layer swap, a `SetActive` order), and fix it.

## Rules
- **Locks:** the bench is scripted, with no physics in the core loop.
- Don't change the bench's design, steps or words. Its cards (`BenchCards`) belong to builder R this round: don't edit them.
- **Your files:**
  - the bench's logic and input (`PadInput` and the bench cursor included);
  - `ClumsyBenchLab.cs`;
  - any bench lab whose assert you must update (`BenchLab`, `BenchViewProbe`, `JuiceCheck` if it measures timing).
- **Not yours (others are building in them right now):**
  - `CafeLab.cs` and the Day labs: builder Y;
  - the drink machine and `StationInteractable`: builder K;
  - the HUD, the phone and every UI words file: builder R.

  If one must change, write the exact edit in your report.
- Mark edits `// 26 Oct 2026 (Bench parity)`, with the reason in plain words.
- Run `python3 /root/tools/csparse.py` on every C# file you change, and grep the definition of every member you call. Nothing compiles here.
- **Be economical:** he asked us to preserve usage. Read the report and the lab first, form hypotheses, then read only the code they point to. Keep a short progress log at `/root/review24/reports/N-progress.md`.
- Don't use any remote-device or Mac tools, and don't spawn agents.

## Report: `/root/review24/reports/N-report.md`, under 600 words
1. The files.
2. Each cause found, with the evidence (the report's numbers, the code).
3. Each fix.
4. What you couldn't prove without running Unity.
5. The Mac steps and labs to run, in order: Bench 8, then Bench 3, 5, 6 and 7 (the bench's other labs), Juice and the aim help lab. Say what a pass looks like.
6. Decisions he may overrule. Any feel change on the pad is his to judge: say what he should feel.
