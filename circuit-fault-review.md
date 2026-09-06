# Circuit fault review and playtest — 2026-09-06

Branch: `codex/circuit-fault-verb`, based on merged main `7f2dbe7`.
The uploaded originals are preserved. This branch develops their circuit verb;
it does not merge main or modify the save format.

Resumed above the owner's `81a478c` commit. Kept the `fastForwardMultiplier`
serialized field and `RouteClear` hint; added visible-travel and input safeguards.
The owner's TimeManager asset remains untouched. The next Human phone fault is
documented separately in `human-fault-review.md`.

## What needed fixing

| Finding in the submitted scripts | Result in this branch |
| --- | --- |
| `step == 0` rejects a straight wire rotated 180°, despite identical connections. Scrambling can also choose that visibly correct orientation. | Grade the actual port mask; only visibly disconnected orientations count as scrambled. |
| Tile click also enters ItemInspector's held-button device rotation. | A drag must start on empty space. Tile/button presses never start device rotation; UI clicks cannot reach parts underneath. |
| Automatic retries repeatedly reduce quality while the player repairs a blockage. Status text exists but is never displayed. | Explicit Retry button discloses the next maximum credit. No retry or penalty until pressed; status/instructions remain visible. |
| Completion takes an extra interval after the last tile. | Completing the last tile finishes the run immediately. |
| Grade relies on Start/Awake order and repeatedly searches visible descendants. | Capture the selected fault after ApplyFault; initialize rules independently of visual generation. Hiding task objects cannot manufacture completion. |
| ApplyFault disables an object if a later, unselected fault shares it. Invalid indices can disable everything. | Compute the union required by the selected fault; clamp selection consistently. |
| A small one-sided world panel can face away, inherit odd device scale, and move away while clicking. | A camera-facing diagnostic projection of consistent screen size. Tile picking uses the displayed plane rather than delayed physics poses. |
| Each wire segment creates materials through `.material`; runtime Shader.Find can fail in a stripped build. | One explicit URP material/shader asset, shared by the projection; property-block colours and no shadow casting. |
| Scene searches/builds and Unity's global RNG can disturb other systems. | Build presentation only when inspecting; cache references; generate with a local seeded RNG. |
| The scripts are not wired to a real fault. | Append software fault 2 to PhoneRepair, retaining faults 0 and 1. Disable it for the existing faults. |

## Gameplay decisions

- Keep the submitted core: rotate visible wires ahead of the charge; verified
  tiles lock; leaving the bench pauses the signal but not customers' patience.
- White fixed ports, tile numbers, an IN/OUT label, a moving charge and a named
  blocked tile explain the route. This is a readable alignment task, not a hidden
  routing riddle. Check readability at the actual game resolution.
- Keep the submitted one-credit-per-retry penalty and existing grade thresholds.
  A 6-tile board completed after one retry earns 5/6; two retries earn 4/6.
  The denominator is the selected job's tasks, including other fault tasks if mixed.
- A finished circuit now has a **one-credit minimum**. Exhausting retry credit
  can still leave unfinished work Rejected, but a finished software-only phone
  earns at least Passable. Completion ceilings use the same floor. Existing job
  grade thresholds and payout multipliers remain unchanged; finishing after many
  retries can still pay less than handing back a higher-credit unfinished job.
- The retry is now a deliberate action; the six-second automatic retry wait is
  removed. Passed tiles remain locked. The charge restarts at the input, crossing
  verified tiles at **0.3 seconds each**, then resumes four seconds per unverified
  tile. Leaving preserves the remaining interval at either speed; replay never
  awards duplicate credit. Hitches still advance at most one tile per frame.
- Default 4x4, four scrambles, four seconds per new tile are retained. Default
  routes now contain **6–9 tiles: 24–36 seconds of uninterrupted verification**.
  Player hesitation, interruption and retries add time; this is not a guarantee
  of total repair duration. Generation tries at most 32 candidates, then builds
  a valid exact-length staircase inside the requested band. Settings clamp to
  feasible lengths for other grid sizes. Grid, minimum/maximum route length,
  scramble count, both speeds, seed and display size are serialized on
  `PhoneRepair / SoftwareCircuit`. Normal jobs force at least one genuinely
  disconnected tile; the pure rules model permits zero for controlled tests.
- The lower HUD presents one contextual instruction, verified progress and
  current repair result. The retry button and its point cost/next completion
  limit appear only after a blockage. The whole panel still blocks click-through.
- **Hold Space to fast-forward the pulse**, default 4x, adjustable from 1–8 on
  `PhoneRepair / SoftwareCircuit / Fast Forward Multiplier`. New tiles take one
  second while held at default settings. Release returns to normal speed without
  resetting interval progress. The existing moving pulse turns gold, and the HUD
  changes from the visible hold-Space prompt to "Fast-forwarding". Boost keeps at
  least 0.25 seconds of travel per tile; verified replay therefore remains visible
  rather than multiplying its already-fast 0.3 seconds down to 0.075 seconds.
- Boost still verifies every connection and stops on errors; it has no extra
  grade cost and never automatically retries. It affects only circuit time, not
  the day or customer patience. Leaving inspection, pause/recap, loss of application
  focus, failure and completion cancel boost. Release a held key before boosting
  again after these transitions. The Retry button uses mouse input only so a UI
  Submit binding cannot spend a retry when Space is intended to accelerate.
- New fault payout is a **provisional $45**, matching the existing phone-cleaning
  entry, rather than altering any existing payout. Validate time versus reward.
- Phone first. The mechanic is reusable on compatible devices; a mechanical
  pocket watch is not automatically given software. One circuit projection per
  selected fault is the supported presentation for this prototype.
- Your Phase 1 note calls for software at the counter; these uploaded scripts
  call for a bench projection. This prototype follows the uploaded scripts.
  Moving repair interaction to the counter remains a separate design decision.

## Quick Unity check

1. Fetch and switch to `codex/circuit-fault-verb`. Let Unity import and compile.
2. Outside Play Mode, run **Fixit Fidget > Checks > Circuit rules**, then
   **Circuit integration**. Both should log PASS. These checks use temporary
   objects/prefab contents and do not save assets or reset progress.
3. Enter Play Mode during an open day. Choose **Fixit Fidget > Playtest > Spawn
   circuit phone at empty bench**. Free a bench slot first if needed. This adds
   one unowned, zero-payout practice phone; it disappears on leaving Play Mode.
   Ordinary day simulation and its existing save checkpoints still operate.
4. Go to that repair bench with F and click the phone as usual. Reconnect its
   signal. The live phone fault also appears in the normal random fault pool
   when the phone is eligible; the Day 1 device pool still contains the watch.

## Acceptance checks

- Correct straight wires work in both equivalent orientations. Every tile can
  be aligned by at most three clicks. White ports and numbers remain readable.
- Click/hold/drag from a tile: the device does not rotate. Empty-space drag still
  rotates it. Clicking the Retry HUD cannot scrub, select a tool or turn a tile.
- Leave partway through an interval, deliver a beverage, return: same layout,
  verified tiles, retry count and interval progress. No credit lost for leaving.
- Let the charge hit a bad tile. Wait: no automatic penalty. Fix it and choose
  Retry: exactly one credit is lost, including under a double click.
- Fail after several verified tiles: those tiles replay quickly; the blocked
  tile gets its full interval again. Leave during replay and return to check the
  saved interval. After excessive retries, completing the circuit must show
  Passable, with no zero-credit completion result.
- Hold Space halfway through travel: pulse moves visibly faster and turns gold;
  release: it continues normally from the same position. Rotate another wire
  while boosting. Leave a wrong tile ahead: the pulse must stop and wait for an
  explicit mouse Retry. Hold Space through exit/re-entry, pause, recap or an
  application switch: boost must require release before it can resume. Check the
  prompt at 1280x720 and confirm customers/day do not accelerate with the pulse.
- Final tile completes without an extra wait. HUD shows both completion and
  grade. Early handback still uses existing partial grading; detached parts
  still block handback. Test a real customer's resulting payment/recap too.
- Right-click out, exit station, disable/destroy the job, or enter recap: no
  stranded projection/controls, no circuit input over recap. Resume tomorrow.
- Original phone screen/cleaning faults and watch repairs still behave normally.
  Run the existing recap-input checks as a regression gate.
- Check a standalone player build for shader inclusion, readable UI at 1280x720
  and your normal resolution, frame time, and camera blending. There is no claim
  of measured low-end performance or controller support yet.

## Validation performed here

- Compiled the production `CircuitRun.cs` and its shared checks with Roslyn and
  ran them on .NET 8: **58,804 assertions passed**, including 1,200 seeded layouts
  plus every feasible exact route length across the supported grid sizes.
  Includes reciprocal paths, no repeated cells, deterministic seeds, visible
  scrambles, solve-to-Perfect, guaranteed fallback geometry, route bounds,
  both retry speeds, pause/resume at their boundary, completion-credit floor,
  visible boost travel, mid-tile speed switching, boost failure, equivalent
  grades at either speed, and invalid boost settings.
- C# syntax parsing across 92 available source files: no syntax errors.
- Phone YAML: 73 unique object records including the new Human task; local file references, parent linkage,
  script/material GUIDs and original fault indices validated.
- Unity Editor, Play Mode and the player shader compiler are unavailable here.
  The Unity integration checks are supplied, **not reported as executed**.
  They now also cover finished-run grading through RepairJob after excessive
  retries, runtime minimum scramble setting and boost cancellation/input guards.
  HUD appearance and live keyboard input need the
  owner's Unity playtest; it has not been rendered here.

The dependency-free runner can also be run with .NET 8:
`dotnet run --project Tests/CircuitRules/CircuitRules.csproj`.

## Next roadmap gate

The owner likes the interaction but solves the guided boards very quickly;
waiting for verification was the main frustration. The approved first step is
manual speed-up, followed by another playtest before changing board difficulty.
Do not use forced idle time as the target repair duration. Record time spent
making decisions separately from pulse travel and interruptions.

Test this refinement on an actual customer and report clarity, clicks, total time,
grade and whether an interruption feels fair. Then tune it into Phase 1's
content foundation. Human is now implemented for review; wiring the existing
HoldCallJob follows its playtest. Don't mark the software family finished until the player-build
and live-customer checks pass; don't start devices 3–8 merely to duplicate an
unproven interaction.
