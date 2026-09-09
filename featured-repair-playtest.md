# Grace showcase: authored repair requests

The September 9 logs show Grace received a watch Cleaning fault on Day 1 and a
phone Human fault on Day 2. Both were Perfect. Her featured slot currently picks
a random job, so these visits do not test the reunion-camera episode.

This change adds a specific prefab/fault choice to each DayDefinition's featured
appearance. It is off by default. It changes neither existing campaign assets
nor the arrival schedule. Random regular visits retain their preferences.
Invalid selections warn and fall back to the normal job instead of blocking the
queue. This is a content-authoring step toward M1, not a completed camera episode.

## One short test; no five-day replay needed

1. Stop Play Mode. Run **Fixit Fidget > Checks > Featured repair requests**.
2. On a disposable copy of a day asset, enable **Use Featured Repair**, assign a
   working bench-repair prefab, and choose a valid zero-based fault index from
   its DeviceDefinition. Keep a featured regular assigned.
3. Use that day copy in a test scene. Observe the featured customer's ticket:
   the device and fault must match the authored selection. Other arrivals retain
   their normal mix. Do not rename a watch as a camera to simulate finished art.

The normal save system is still active in a copied scene. Stop Play before the
day closes to avoid a day-end checkpoint; a scene copy does not isolate saves.
No production day asset needs editing to run the menu checks.

The scaffold tool is separate: **Fixit Fidget > Content > Device scaffold**, choose
the copied prefab and its existing fault, then **Spawn practice customer** during
an open day with queue space. “Scaffold” is a prefab-copy tool, not a fault family
or an automatic customer arrival.

## Reading the next logs

CSV adds `story_lines`, `focus_requested`, and `remembered_focus`. A spoken line
means submitted to the existing bubble, not proof the player read it. These
columns distinguish a quiet return from a storyteller that never spoke.

`repair_s` is now named `service_s`: it has always measured acceptance through
departure, including waiting and drink-only visits. Update external CSV readers
that used the old header. `served` remains received-service, which can overlap
with a storm-out. The summary now separates helped, satisfied and helped-but-
unhappy visitors. Repair grades are emitted only when a repair was returned;
serving a repair customer's drink alone must not fabricate a repair grade.

Next content gate: author the camera's cleaning/shutter tasks and preserved strap,
then its grade-dependent return photo. Final artwork and the Day 2/3 schedule
decision remain open; existing Day 1/2 timing stays unchanged.
