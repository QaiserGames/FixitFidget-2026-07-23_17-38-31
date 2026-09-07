# Counter phone and support-call presentation review

Review branch: `codex/repair-presentation-review`, based on the published
`codex/physical-human-hold-call` commit `7596790`.

The owner's screenshot identified the compile fix in `HoldCallRuleChecks.cs`:
`new System.Random(7291)` disambiguates System.Random from UnityEngine.Random.
That exact correction is included here. Commit the matching local correction
before switching branches so GitHub Desktop can preserve it. This is a separate
review branch; the previous branch and main were not advanced.

## What to expect

- Support calls have a screen-space card below the money/clock/stock area.
  Each card identifies the customer and job number, shows large remaining
  seconds and a time bar, and says when the player is free to work elsewhere.
  Ringing changes the card to amber and `Answer now`; a matching number marks
  the visible phone, with a direction cue if the player needs to find it.
  The old small world label and extra multi-line counter prompt are removed.
- Concurrent calls retain separate cards, clocks and job numbers. Pause and
  recap hide the cards. Existing spatial hold music/ringing remains.
- The Human phone has a rounded casing, recessed sliding switch, clearer
  silent/ringing feedback and a short entrance/confirmation movement. Its
  framing adapts to the camera rather than depending on one field of view.
  A single fixed-size caption identifies the customer and the controls.
- This is still replaceable prototype art. It is not a Blender-made asset or
  a claim of final shipping quality. `HumanFault.PresentationPrefab` now accepts
  an authored replacement without changing the repair/handback logic.

The fuse path, hold/ring durations, grading, payments, saves, customer schedule,
and counter repair completion rules are unchanged in this pass.

## Short playtest

1. Let Unity compile. During an open day, use the existing
   `Fixit Fidget > Playtest > Spawn support-call customer` menu. Accept and dial
   the phone. Walk away in isometric view: read the timer without approaching.
2. Repeat with a second phone. Check both names/timers, answer the right phone,
   miss a ring deliberately, redial and deliver. Test 1280×720 and your normal
   resolution. Cards must not cover the top tickets or lower interaction prompt.
3. Spawn a Human phone customer. Accept, hover and click the orange switch.
   Confirm the slide, ring, completion line, single payout and departure. Try
   right-click to put down and F to leave before completion; resume with E.
4. Pause, reach recap, and load the next day. There must be no stranded caption,
   call card, ringing audio or unlocked counter cursor. Play one fuse repair,
   including Spacebar, to confirm the presentation did not intercept its input.

Practice guests pay zero but still appear in that play session's recap/log.
No reset or scene migration is required.

## Adjustable presentation

To persist call HUD adjustments, add one `SupportCallHUD` to an empty scene
object and set UI Scale, Top Inset or Right Inset. If none is authored, one is
created automatically when a support phone appears. Runtime Inspector changes
are temporary unless copied to an authored component outside Play Mode.

Default layout calculations fit one through six cards at 720p, 1080p, 1440p,
1920×1200 and 3440×1440. At 720p the default action text is 16 pixels and the
timer is 25.6 pixels. This verifies dimensions, not Unity's rendered result;
very long names/prompts may ellipsize. Unusually large active-call counts reduce
the card scale to fit, and should be assessed if the intake capacity increases.

## Future Blender replacement

1. Import the phone with its front facing local -Z, top +Y, and a separate
   sliding mute switch. Create a Unity wrapper prefab with `CounterPhoneModel`.
   Imported scale and the visible mesh centre are preserved when framing it.
2. Add `PhysicalToggle` and a collider to the switch. Assign the wrapper's
   Mute Switch, Switch Slider and Sound On Position references; the position is
   relative to the slider's parent. Place it initially in the silent position.
   The optional Screen field accepts a TMP label for silent/ringing text.
3. Assign the wrapper to Presentation Prefab on the phone's `HumanFault`.
   Leave that field empty to use the procedural prototype. The visual prefab
   needs no job, customer or payment scripts. Keep its local orientation upright.

## Verification performed here

- All 102 available C# source files parsed without syntax errors.
- Layout calculations checked the five resolutions above with 1–6 calls.
- Geometry calculations verified five closed rounded-part meshes, 800 outward
  nondegenerate triangles, and consistent edge connectivity.
- Compared 13 protected gameplay/input/save source files byte-for-byte with
  the published baseline, including all circuit files and the support timer.
- Checked new metadata GUIDs for collisions and checked the diff for whitespace
  errors. The published scene's HUD anchors were read to place the call tray.

Unity is unavailable in this workspace. Unity compilation, actual text wrapping,
model lighting/appearance, collider targeting and the player-flow checks above
have not been run here. The reported Random ambiguity is corrected; the prior
standalone checks did not enable UNITY_EDITOR and therefore missed that import
collision. Full Unity compilation is still required before Play Mode.
