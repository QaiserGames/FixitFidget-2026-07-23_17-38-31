# Repair presentation review — ticket and world cue revision

Branch: `codex/repair-presentation-review`. Keep main unmerged until the Unity
playtest passes. The owner's `System.Random` editor compile correction remains
included. No fuse rules, timing, grading, payments, saves or schedule changes.

## Current design

The prior separate screen-space call cards are superseded by this revision.
There is no call `OnGUI`, world-space countdown text or numbered world marker.
`SupportCallHUD` is retained as an empty compatibility component so a scene
that added it does not acquire a missing-script reference. It creates no UI.

| State | Physical phone | Customer ticket |
| --- | --- | --- |
| Needs dialing / missed call | Dim grey handset | Call support / Missed call, Dial phone / Redial |
| Connecting | Amber handset | Connecting |
| On hold | Slow amber brightness/size pulse | On hold and remaining seconds |
| Ringing | Bright green, faster pulse, shake and ring arcs | Matching animated handset, Answer now and seconds |
| Resolved | World cue removed | Resolved / Return phone until delivery |

The handset is a vector Graphic, not a font character or downloaded image.
Its dark backing separates it from bright and dark surfaces. The world-space
canvas faces the camera and grows with distance toward a 52-pixel box, capped
at 1.8 world units. It uses normal world depth; it does not reveal a phone
through walls. Timing and the physical phone's ownership remain authoritative.

The countdown is a line within the existing JobTicket, not a second obligation
manager. Customer name, colour, patience and any outstanding drink remain on
that same card. Delivering the phone removes its call line; any remaining drink
stays visible. Pause stops animation/timers; recap hides the rail/world cue.

## Rail layout

The existing scene used a single HorizontalLayoutGroup, so adding many tickets
did not wrap automatically. TicketRailUI now lays out fixed-size 220×172 cards
in stable order, wrapping centrally with 12-unit gaps. Defaults reserve 460
canvas units on either side and cap the rail at 1000 units. These three layout
settings are serialized on TicketRailUI. No authored scene/prefab is rewritten.

At the current CanvasScaler settings, a 24-point countdown is 16 pixels at
1280×720. Names and call state use the same font size. Normal job descriptions
and secondary drink lines use 20 points and may ellipsize if too long.
At 16:9, four tickets fit per row and eight occupy two rows. Larger workloads
add rows rather than hiding or shrinking urgent calls; this is not a promise
that unlimited obligations fit without encroaching on the central game view.
Validate your maximum realistic concurrent workload before shipping.

## Required Unity test — sound off

1. Fetch/pull the review branch with a clean working tree. Let Unity compile.
   Enter Play Mode during an open day. Do not reset your save.
2. Use `Fixit Fidget > Playtest > Spawn support-call customer`. Accept and dial.
   Walk away: the device should have only the amber handset, while the ticket
   carries its countdown. No separate call box should appear in a corner.
3. Turn game sound off. Face the bench while keeping the shelf in the camera's
   view, roughly 15 metres away. When ringing starts, judge whether the green
   motion is noticed without searching the ticket. Repeat in isometric and
   counter/bench views. This perceptual test has NOT been run here.
4. Deliberately aim the camera away from the phone or put it behind a solid
   object. The ticket must still signal the obligation; a world cue cannot be
   expected to be visible outside the camera or through a wall.
5. Repeat with two calls and normal repair/drink customers. Check identity,
   separate timers, wrap, ordering, missed-call redial, answering and delivery.
   If a call customer also orders a drink, it must stay listed after handback.
6. Pause, resume, reach recap and continue to the next day. Check for orphaned
   icons, ringing sounds, stale call lines or tickets covering corner controls.
7. Retest the mute-switch phone and one fuse repair, including Spacebar.
   Their gameplay is unchanged.

The practice visits pay zero but count in that play session's recap/log.
No world-position distance check substitutes for the sound-off visual test.

## Mute phone / future Blender asset

The previous rounded prototype, sliding orange switch, fixed-size controls
caption and authored-model hook are retained. It is still prototype art.

Create a Unity wrapper prefab with CounterPhoneModel, facing local -Z with top
+Y. Assign a PhysicalToggle with collider, the sliding transform and its sound-on
local position; initially place it in the silent position. Assign an optional
TMP screen, then set HumanFault.PresentationPrefab to the wrapper. No job,
customer or payment scripts belong in the visual prefab. An empty field uses
the existing generated prototype.

## Verification limits

Source syntax, protected-gameplay comparisons and numeric layout/world-size
checks are run in this workspace. Unity is not installed here, so full engine
compilation, UI mesh rendering, typography, occlusion and actual player detection
must be tested on the owner's machine. The referenced `claude/hud-spec.md` was
not present in the published branch; this revision follows the user's supplied
ticket-rail/four-corner requirements and the actual scene's HUD anchors.
