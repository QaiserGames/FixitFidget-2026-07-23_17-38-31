# Counter Human repairs and unattended support calls

Review branch: `codex/physical-human-hold-call`, based on `0c8e232`.
Do not merge until the Unity playtest below passes. No save reset is needed.

## What changed

- Human: accept the silent-call phone at the counter. A close-up shows an orange
  mute switch. Left-click flips it; the screen changes, a short ring confirms it,
  and the normal payment/grade/log/completion path returns it automatically.
  Right-click closes the view, F leaves the counter. E on that customer resumes.
  A full intake shelf does not block Human jobs. The customer keeps their counter
  slot until served or departing; this family does not add a secondary drink.
- Support: accept the service-disconnected phone. E on the phone on the intake
  shelf dials directly, without department quizzes. After connecting, it waits
  20–30 seconds while you do other jobs, then rings for 15 seconds. Each ringing
  phone has its own customer-named alert and a direction cue. E answers it.
  E again picks it up; deliver to the customer normally.
- Missing the answer window permits a redial. The cost is time and ordinary
  customer patience, not a hidden grade deduction. An unanswered call has no
  repair credit; a resolved call receives the usual completed-job quality.
- Calls are eligible in authored Days 3–5, including subsequent days reusing the
  Day 5 schedule. Days 1–2, wave timings, patience and regular scheduling are
  unchanged. Hold Min, Hold Max and Ring Window are editable on PhoneJob.prefab.
- Existing unfinished calls continue through closing while their customers stay.
  Pause freezes their clock; recap, leaving customers and destruction suppress
  their input/audio. Multiple calls never share a timer or numbered input.
- CircuitPuzzle, CircuitRun, CircuitTile, ItemInspector, circuit assets and the
  existing phone Software fault configuration were not changed. The only
  RepairJob edit is an explanatory Human-task comment. No circuit timing,
  difficulty, Spacebar boost, route generation, scoring or controls changed.
- HumanFaultScenario, SilentCalls.asset and HumanConversationRun are retained
  for possible story conversations. They no longer drive a routine repair.

## Quick Unity test

1. Save your current work. In GitHub Desktop, Fetch origin and switch to this
   branch. Do not discard any unrelated local changes or merge yet.
2. Open the project and let Unity compile. Stop here if the Console has errors.
3. Outside Play Mode run:
   - Fixit Fidget > Checks > Human counter integration
   - Fixit Fidget > Checks > Support call rules
   - Fixit Fidget > Checks > Support call integration
4. Enter Play Mode during an active day with room in the counter queue:
   Fixit Fidget > Playtest > Spawn Human phone customer.
   Accept normally. Confirm no numbered choices, no shelf item, a clickable
   switch, audible/visual confirmation, one payment and departure.
5. Repeat, first stepping away before flipping, then after flipping but before
   the confirmation ends. Resume with E. Neither route may lose the device,
   lock input, repeat the payment, reset completion or leave a ghost ticket.
   Also test with a full shelf and with another item already in your hands.
6. Use Playtest > Spawn support-call customer. Accept, E on the shelf phone,
   walk away and complete another task, then answer and deliver it. Repeat but
   deliberately miss the ring window: redial and finish without a quality cap.
   The practice visits pay zero but count in that play session's recap/log;
   the menus do not delete or reset a save.
7. Test two concurrent support phones; alerts must identify each customer.
   Turn away, inspect a fuse and test with audio muted: the visual signal must
   still make the ringing call findable. Judge text size/overlap at your resolution.
8. Check closing, recap cursor isolation and the next day. Calls should not
   end merely because closing starts. A departing owner must not be answerable.
9. Play your existing fuse fault, including hold-Space, retry and leaving the
   bench. It must behave exactly as it did before this branch.

## Verification and limits

Executed here: 891,949 support-timer assertions; 58,804 circuit regression
assertions; 52 preserved story-conversation rule assertions; C# syntax parsing;
prefab/day YAML parsing; source/prefab comparison protecting the fuse path.

Unity is not installed in this workspace. The Unity integration menu checks,
engine compilation, rendering/audio and full player flow are supplied for your
machine; they have NOT been run here. Text/switch visibility, timing feel and
low-end performance still require that test.

This is a functional presentation pass, not final phone art. Human currently
presents the phone with a switch, not every future device's physical fault.
Support uses the existing phone prototype and stores it on the intake shelf
until answered: it saves attention, not shelf capacity. Its starting timing,
spawn share and payout are provisional and should be measured in a real rush.
