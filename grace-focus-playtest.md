# Grace: storyteller and remembered focus

GDD v4.1 §4.4, §19.1–19.3 and Appendix B place a complete Grace encounter before
expanding the device roster. This is one M1 behaviour slice, not a finished
camera episode. No portraits or Blender work are needed for this test.

## What changed

- Only Grace opts into storyteller behaviour. Pending bench repairs can trigger
  her three short authored lines, in order, without repeating.
- First delay: 12 seconds of eligible waiting time. Later spacing: 22 seconds,
  excluding her other speech. These values and the maximum line count are on
  `Regular_Grace` in the Inspector; no code edit is needed to tune them.
- Stories wait for the normal interaction/conversation to finish and defer to
  ringing support calls and other customers' active speech. A due line is not
  discarded when deferred. It never races through several lines to catch up.
- After a story, approach Grace from the shop floor, aim at her and use the
  normal Interact action (E): **Let me focus**. She acknowledges it and stops.
  Drink service, shortages and handbacks keep their existing higher priority.
- This is not reassurance: no patience boost, tip charge, relationship penalty
  or repair-grade change. Normal time/patience still runs while you move around.
- On departure, the boundary is recorded once with the visit, under her stable
  `grace` memory record. It is written to disk by the existing day-end checkpoint.
  The next visit adds a brief acknowledgement to the honest previous-outcome
  intake and starts quiet. Failed repairs still receive their failed-repair line.
- Existing v3 saves without the new `focusBoundarySet` field default to false.
  No reset, format-version bump or changed checkpoint timing is required.

## Start here: checks without changing progress

Stop Play Mode. Under **Fixit Fidget → Checks**, run:

1. **Storyteller timing rules** — expect 58 assertions.
2. **Customer memory and identity** — includes old-save compatibility, Unity
   JSON roundtrip, factual outcome-plus-boundary callbacks and portrait fallback.
3. **Storyteller interaction guards** — prompt/interaction, pause, recap,
   lifecycle, repeated direct requests, unchanged grade/patience and remembered quiet.

The checks use synthetic records and temporary objects; they do not reset/load
your save, edit assets, pay customers or schedule visits. Runtime rendering,
NavMesh and the complete scene still need the live test below.

## Live two-visit test

Use your existing development test save/copy; do not delete current progress
just for this check. The existing schedule still features Grace late in Day 1
and early in Day 2. This patch does not force or reschedule her arrival.

1. Accept Grace's bench repair and allow her to settle. Keep her in camera view
   while the repair remains pending. Let other dialogue finish and wait for her
   first story. Her drink request should still work normally.
2. Step back from a station if needed, approach her and aim at her. Confirm
   **Let me focus** appears. Press E once. Expect her tea-break reply, then quiet
   for the remainder of the visit. Keep working and finish the order normally.
3. Finish the day and use the normal recap/next-day path. On her next visit she
   should remember both the actual repair outcome and the quiet boundary. She
   should not resume ambient stories while you work. Reloading that checkpoint
   should retain the same facts without awarding an extra visit or payout.
4. On a separate test run without setting the boundary, confirm her lines are
   spaced out and stop after three. Walk-ins should never acquire this behaviour.
5. Regression: serve a held drink/return a repair while facing her; those actions
   should win over focus. Pause or enter recap: no new story or focus action.
   Exercise a ringing support call alongside Grace; it should defer new stories.

## Deliberate limits / next gate

This uses the existing world speech bubble. A line waits if Grace is outside the
camera's viewport; there is no voice acting or new off-screen story HUD yet.
Being inside the viewport does not guarantee readability or lack of occlusion.
Check bubble size, readability and the longer return intake in the actual game.
If the bench camera hides her, look back into the room to see the deferred line.
Fast repair completion can legitimately finish before any story is due.

That is a prototype limitation, not the completed GDD broadcast/presentation
system. If the timing or visibility hides her personality, adjust/resolve that
before adding more episodes. Do not call M1 complete on a green test alone.

The camera model, preserved scratched strap, returned photo/prop, full episode
schedule, final portraits and fresh-player character/callback recall remain open.
The fuse/circuit gameplay, support-call rules, Human toggle, day pacing and
economy are unchanged. A lamp or two more verbs is not the next gate.

## Verification available in the coding workspace

- Standalone .NET: 58 storyteller timing assertions and 485 memory assertions pass.
  Run `dotnet run --project Tests/StorytellerRules/StorytellerRules.csproj` where a
  .NET 8 SDK is installed. Its JSON smoke test uses System.Text.Json; the Editor
  check above separately exercises Unity's actual JsonUtility.
- Unity is unavailable in the coding workspace. Editor checks, full-project
  compilation and the live playtest are supplied for verification, not claimed run.
