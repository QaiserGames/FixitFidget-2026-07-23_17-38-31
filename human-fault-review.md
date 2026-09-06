# Human fault: silent calls — review and playtest

Implemented on `codex/circuit-fault-verb`, above the owner's `81a478c` Spacebar
commit. Main remains unchanged. This is the next Phase 1 reusable fault family;
Unity playtesting is still required before marking it complete.

## Why this interaction

The phone works, but its call settings do not match the customer's needs. The
repair is listening, choosing an appropriate configuration, and explaining it.
That adds a different kind of decision without another model or a portrait
dependency. The player is helping someone, not dismantling an unbroken phone.

Three authored checks establish cause, preserve quiet messages while allowing
family calls, and test/explain the setting. Wrong choices produce corrective
feedback and cost reading/attention time. They do not invent physical damage
or permanently punish a player who eventually solves the misunderstanding.
There is no forced countdown; E reveals the line immediately for faster readers.
The GDD's 15–30 second target needs measurement with a first-time reader.

## Integration and limits

- `PhoneRepair` appends Human fault **3**. Mechanical 0, Cleaning 1 and Software
  2 keep their indices. Its provisional payout is **$25**. Existing payouts,
  grade thresholds and save format remain unchanged.
- It enters the existing random phone-fault pool when phones are eligible.
  The Day 1 watch-only pool and authored character jobs remain as configured.
- Accept normally. The conversation immediately offers numbered choices.
  F/Escape suspends it; E near that customer resumes through **Talk through the
  problem**, including while they settle into the waiting area.
- Each phone snapshots its own scenario and progress. Stepping away, carrying
  or hiding it does not erase progress or advance it. This is live-day progress,
  not a new mid-day save feature; the existing checkpoint rules still apply.
- Zero checks = Rejected; one = Passable; two = Good; all three = Perfect.
  Wrong choices cost time, not extra grade penalties. A completed conversation
  cannot grant credits repeatedly. Missing/invalid Human content fails closed.
- Completion clears the fault, **not the whole transaction**. Retrieve and
  return the customer's phone through the normal handback, payment, visitor and
  day-log flow. `fault_family` records Human through the existing record.
- While carrying a partially resolved, reassembled phone, Q in its conversation
  offers **Return as-is** with the grade shown. Detached parts still block
  handback. No new reward or economy shortcut was added.
- Conversation input owns the closing frame, so F/Escape cannot also trigger a
  station exit. The player stays still; the customer's movement/watchdog pauses
  while existing conversation patience drain continues. Closing restores the
  customer's previous movement state or performs its deferred handoff.
- The existing conversation panel expands only for choices and restores its
  original layout afterward. Missing portraits use the existing fallback.
  Day 1 guidance directs this fault back to conversation instead of tools.

The scenario lives in `Assets/Data/HumanFaults/SilentCalls.asset`. Another
compatible device can reuse the HumanFault component with another scenario;
this does not automatically add Human faults to all devices. This first scenario
has a linear sequence with corrective feedback, not a branching story system.

## Quick Unity test

1. Fetch/pull `codex/circuit-fault-verb`; let Unity import and compile.
2. Outside Play Mode run **Fixit Fidget > Checks > Human conversation rules**
   and **Human conversation integration**. Also run **Circuit rules**,
   **Circuit integration**, and the existing recap-input check.
3. During an open day choose **Fixit Fidget > Playtest > Spawn Human phone
   customer** with a counter slot free. Talk to **Practice guest** and accept.
   This uses the normal visit flow with zero payout, so it affects that playtest's
   recap/log. It does not reset the save; normal checkpoints still operate.
4. Try a wrong answer, then a correct one. Press F, let the customer settle,
   and resume. Progress and feedback should be retained. Use 1/2/3 to finish,
   collect the phone from its shelf and hand it back.

Also verify: two Human customers retain separate progress; partial handback
shows the promised grade; a removed cover must be refitted; running out of
patience closes the dialogue; the recap blocks all conversation keys; normal
repair/drink intake still works after closing. Check choice readability at
1280×720 and your usual resolution, including without portrait art.

## Validation

Production Human rules compiled and ran on .NET 8: **52 assertions pass**.
Production circuit rules: **58,804 assertions pass**, including the owner's
route-clear hint and the boost safeguards. C# syntax parsing covers 92 available
source files. Phone YAML has 73 unique object records with valid local
references; appended fault indices, scenario answers and new GUID links check
out. Unity Editor/Play Mode/player builds are unavailable here: the supplied
integration checks and visual tests are **not claimed as executed**.

## Following roadmap work

Validate this one family before introducing another. Next is integrating the
existing HoldCallJob as the Bureaucratic family, then the reusable device
scaffolding tool ahead of devices 3–8. Circuit difficulty tiers remain planned:
measure decision time separately from boosted pulse travel before tuning them.
