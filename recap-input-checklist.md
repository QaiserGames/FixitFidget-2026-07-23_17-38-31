# Recap input regression check

## Cause and change

`DayClock.RestoreRecap` already sets `TimeRemaining = 0`, `IsOpen = false`,
`DayOver = true`, and `Time.timeScale = 0`. Adding that freeze again would not
close the input paths that continue running while paused.

`HoverTooltipUI.Update` followed raw mouse position while fading with scaled
`Time.deltaTime`. A visible tooltip could therefore freeze at a nonzero alpha
and continue following the cursor across the recap. Repair manipulation also
read clicks and mouse deltas directly, and conversation UI used a scaled fade.
These are source-confirmed leak paths; the exact visual report still needs a
live Play Mode reproduction and retest.

The recap now immediately clears hover/conversation overlays, cancels inspection,
clears cached movement, and temporarily disables Cinemachine camera-input readers.
Closed-day guards also reject repair, station, and conversation input. The shared
UI input actions remain available for recap shopping and Continue. Only readers
that the recap disabled are restored, and only after Continue successfully saves
the next day. A failed save keeps the recap and input restriction in place.

## Quick test

1. Stop Play Mode. Run **Fixit Fidget > Checks > Recap input isolation**.
   This uses inactive temporary objects and a blocked-save fixture; it never
   attempts to write your save. Expect one PASS message, no errors.
2. Enter Play Mode on your existing completed-day save. Move the cursor across
   the recap, click its empty areas, and press movement/station/repair keys.
   The world should not respond; no gameplay tooltip should follow the cursor.
   Recap buttons and the shop should still work.
3. Click Continue. Confirm exactly the next day opens, then walk and enter a
   station. Camera look, repair controls, and normal hover tips should work again.
4. At the next natural day end, leave a station tooltip visible as the recap
   opens. It should disappear immediately, with a visible unlocked cursor.
   Quit/reload the recap once more to cover the restored path as well.

Keep the Console visible and report which of the natural-close/restored-close
paths fails if anything still moves. You do not need to delete progress for this
test. Save-file failure, restock, and upgrade coverage remains documented in
`recap-save-checklist.md`.

## Validation boundary

Code and serialized scene wiring were reviewed. The existing scene uses
Cinemachine camera-input readers, and `RestoreRecap` already contains the freeze.
The new Editor checks cover paused overlay cleanup, inspection release, movement
reset, camera restoration, repeated suspension, and blocked Continue.

Unity/C# execution and Play Mode are unavailable in the coding workspace, so
the Unity checks and the live mouse test are pending on the full project.
