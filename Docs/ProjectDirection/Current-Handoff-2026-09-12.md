# Current handoff — September 12, 2026

## September 28 (later morning) — night walk part 3: getting about (the camera follows Ace, see-through buildings, the café closed); on `night-walk`

**Read this entry first.** One code commit, `9680c9e`, on `night-walk`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His answers (28 Sept):

- Scope: "Part 3 + night HUD (Recommended)". That covers the follow camera, see-through buildings, the night HUD and closed stations. The streets stay empty until part 4.
- Framing: "Close: 20 m, zoom 12–34 (Recommended)", tilt 55–80°.
- For the feel he pointed at Schedule I's town at night. Asked which parts, he said "all of the above": darkness between the lamps, asleep but lived-in, alleys and shortcuts, and night pressure. These steer the tuning after his test. Nothing in this step is built for them yet.
- He asked for part 3 to be playable, so he can test it and say what he liked and didn't.

**What this step builds** (all at run time; nothing in the scene changes):

- **The overhead camera follows Ace at night** (`CafeViewMode.FollowAce`, called by `NightWalk.Begin` and `End`):
  - it starts 20 m from Ace at a 62° tilt and keeps the heading the day's view had;
  - the controls are the same (middle-drag orbits and tilts, scroll zooms; the pad's right stick and triggers), within 12–34 m and 55–80°. R3 returns to the start;
  - by day nothing changes: 38–68°, and the café's own Minimum/Maximum Distance;
  - it trails 0.2 s behind Ace (`SmoothDamp`), which hides the capsule's instant starts and stops;
  - the café's cut-away walls: with Ace outside, a wall only lowers when it hides Ace (sightlines to knees, middle and head). Inside, the day's rule applies;
  - `FollowAce(false)` restores the day's view exactly;
  - the settings are new serialized fields on `CafeViewMode`, under "Following Ace (the night walk)". They are still at the code's defaults, because the scene hasn't been saved with them;
  - a new `LookTo(yaw, pitch)` turns the first-person view, for scripted views.
- **Buildings in the way turn see-through** (`NightSeeThrough`, new, added to the night group at run time):
  - the shader is `Fixit Fidget/Night see-through`, file `Assets/Playtests/AcesCafeLayout/Night walk - see-through.shader`;
  - a building or big tree between the camera and Ace fades to a quarter of its dots in 0.2 s. It comes back 0.3 s after it's clear. The whole building fades, never single pieces;
  - "in the way" means any of five sightlines (knees, middle, head, and either side) crosses one of the building's pieces, tested against their bounding boxes;
  - a building is the nearest `Building …` or `… house` above a piece. Anything else groups its pieces up to 8 m wide (a tree, a shelter);
  - these never fade: tops under 2.3 m, anything thinner than 1 m, the café room, the ground, the hill, the skyline band, people and cars;
  - 78 buildings and trees can fade, 485 pieces in all;
  - the fade is a screen-door dither (a 4×4 Bayer pattern) in a copy of URP Lit. Every surface of a building drops the same dots, so what's behind shows through the gaps;
  - each material gets a copy with that shader the first time its building fades (27 after the tour). The copy keeps the albedo, colour, cut-out and glow; Synty's `_Albedo_Map` and `_Emission_Map` are carried across. Glass keeps its own material;
  - shadows aren't dithered, so the street's shadows stay put;
  - the first version was a blended fade. It looked murky (stacked transparent layers) and was dropped. Without the shader, a blended copy still stands in;
  - **for a build later:** the shader is found by name (`Shader.Find`). A player build only finds it if it's included: in Always Included Shaders, or used by a material. In the editor this doesn't matter.
- **The café is closed at night** (`ShopUI`, `PlayerInteractor`):
  - the HUD shows `Night 11:00 PM` in place of the day's clock;
  - money and stock are hidden, and there are no prompts;
  - the crosshair shows only in first person;
  - nothing in the café offers anything or can be used;
  - this takes over part 5's "the HUD's day clock and counter prompt at night".
- **Tool:** `Fixit Fidget > Night > Night walk 3 - Walk the tour (Play Mode, night walk)` (`NightTour`, in Diagnostics), for a night lab session:
  - Ace walks out of the café and round the 9 blocks by itself. The waypoints were found by A* over the night sweep's grid;
  - the camera is turned so buildings stand in the way. It zooms out and in, then Ace walks a stretch in first person;
  - photos every 2.5 s, `trail.csv` and `report.txt` go to `Logs/Night/night-tour-*`;
  - hands off the mouse while it runs (about a minute and a half).

**Checks** (lab sessions; the playtest save was never written):

- **The tour** (`Logs/Night/night-tour-2026-09-28_061038`):
  - 342 m in 70 s, 0 stuck spots;
  - frames: average 4.2 ms, 95% under 4.4 ms, worst 25.9 ms;
  - 11 different buildings and trees turned see-through, at most 2 at once;
  - no café wall went down while Ace was outside;
  - 0 warnings and 0 errors, also on leaving Play;
  - an earlier build destroyed the glass materials it had passed through unchanged, which gave 2 errors on leaving Play. Fixed;
  - earlier tours: `_055549` (the blended fade) and `_060500` (the dither at 0.3).
- **The day:**
  - the 15 standard day photos (`day-photos-with-night-group-2026-09-28_061538`) are byte-identical to the committed state's (`..._050450`);
  - one retake: the first set's top-down map (`_061304`) came out without the ground, most likely because a shader was still compiling after the reload. The retake was byte-identical;
  - a day lab session (Day 5, autopilot serves): the overhead view, HUD and service as usual, 0 warnings, 0 errors.
- **The scene:** not saved and not changed. After lab sessions Unity marks it as changed, and this step found why:
  - Cinemachine's "Save During Play" is on (see `CmShopCam`). On leaving Play it registers an undo step ("SaveDuringPlay") for every Cinemachine camera, even when nothing changed;
  - when a value really did change, it asks with a dialog. None did;
  - that explains part 2's changed-but-byte-identical scene;
  - a save now would also write the new `CafeViewMode` fields, at their defaults.

**Seen on the tour, for Mansoor's test:**

- Ace's grey capsule is hard to see in a building's shadow at night.
- The café's entrance frame counts as a building, and fades as Ace walks through the door.
- No sound yet; nobody about (part 4); the night's hour holds at 11 PM.
- The LAB banner overlaps the view hint (as before).

**Left open:**

- **Mansoor's test of part 3:** what he liked and didn't. The Schedule I aspects steer what follows.
- **Part 4:** night owls and local cars. With CafeArrivals resting, `CafeArrivals.Players` isn't refreshed at night, and `NpcJourney` uses it to steer round Ace.
- **Part 5:** lab entry, Call it a night, dawn, checks and recording.
- As before:
  - the east street's lantern lights a tree green-yellow;
  - one tower has every window lit;
  - the two small patio air walls;
  - hardening the Café life check waits for Mansoor's say-so.
- **Next:** Mansoor's test and feedback on part 3.

## September 28 (morning) — night walk part 2: the edges closed (road works by night, the corners for good); on `night-walk`

**Read this entry first.** One code commit, `f4528eb`, on `night-walk`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His answer on the corners (28 Sept, the question in the entry below): "Fence + building site (Recommended)". So the corners close for good, day and night.

**What this step builds** (`Fixit Fidget > Night > Night walk 2 - Build the edges (road works at night, the corners for good)`, Edit Mode; editor script `NightEdges`, new):

- **Road works at the 8 street ends, night only.** They sit under `20 - Night walk/Road works (night only)`, on `NightWalk.nightOnly`, inactive by day. Each street end has a different job:
  1. West Street, south end (z −43): a burst water main: a van, pipes, a manhole with steam, cones;
  2. East Street, south end (z −43): resurfacing: road patches, a pallet of sacks, drums;
  3. West Street, north end (z 35.6): a timber delivery: beams, barrels, cones;
  4. East Street, north end (z 34.9): a blocked drain: a manhole, barrels, a crate, cones;
  5. Front Street, west end (x −44.6): a new cable box: a power box, boxes, planks, cones;
  6. Back Street, west end (x −45): a trench: sacks, planks, a ladder lying down, drums;
  7. Front Street, east end (x 43.3): a lane closure: a taper of drums, crates, a pallet with a crate;
  8. Back Street, east end (x 46.3): a skip and rubble: a skip, a pallet, bags, cones.
  - Every site has a line of barriers (`Barrier_01`, stretched to fit), warning signs at the quarter points, three amber lamps on the barriers and an amber point light (range 5.5, intensity 1.6). The lamps are spheres that keep their sphere colliders, in the material `Night walk - works lamp`.
  - 78 barriers and 84 other pieces. Their packs' convex colliders are off, and each mesh gets an exact, non-convex `MeshCollider` of its own.
  - The set-up (`Night walk 1`) now builds them too, so a fresh set-up gives the same night.
- **The corners, for good** (day and night), under `21 - Edges of the 9 blocks`:
  - **A1, the city car park's open west side** (x −41.5, z −41..−24): fence panels and piers, with a locked iron gate (material `Night walk - iron gate`);
  - **A2, behind the car park** (z −42.8): a fence between the two buildings;
  - **B1 and B2, a fenced building site** behind the south-east hotel. Each fence has a warning sign, and there are 16 pieces inside: a skip, a pallet of sacks, beams, barrels, crates, planks and cones;
  - **NW and NE, two more gaps behind the north corners** (z 31.6 and 31). They led up onto the hill. The first plan had counted the hill as inside, and told Mansoor there were only two corners to close. They are fenced like A2, under his fence answer, and flagged to him in the report.
  - In all: 9 fence panels, 4 piers, the gate and 18 other pieces.
  - **Nothing is solid by day**, as everywhere outside the café: their prefab colliders are off. At night the list of what is solid makes them solid. The list was rebuilt: 732 meshes, 352,740 triangles.
  - **The day's routes are checked before building.** Every StreetLife walker's and car's route, and CafeArrivals' foot routes, lot-to-door routes, stalls and turn-in and turn-out, are checked against the permanent fences. Result: 64 routes, none closer than 0.75 m. The build refuses if one is.
- **The patio's two small air walls** (the A-frame's box and an outdoor chair's box) stay.
  - `FitPatioBoxes` found all 3 patio boxes already fitted to their objects' bounds; the air is the empty space inside those bounds.
  - Exact shapes would change the café's day collision. What's left is 2 spots and 1 spot of air, at the patio.
- **The sweep:**
  - it now says whether all the ground Ace can reach is enclosed, meaning none of it reaches the edge of the swept area;
  - its "ways out" are now the reachable ground beyond the 9 blocks' box, each to be checked against the closing lines just beyond.
- **`Night walk 2 - Take the edges out again`** removes both groups (Edit > Undo puts them back) and rebuilds the list without them.

**Checks** (lab sessions; the playtest save was never written):

- **The sweep, the night simulated** (`Logs/Night/edges-night-2026-09-28_045028`):
  - Ace can reach 4487 m², **all of it enclosed** within x −44.9..45.6, z −42.9..34.9.
  - Outside the café room: 0 walk-throughs, 0 high spots, and the 2 small patio air walls.
  - The only holes are roofs above 2.4 m and one part-1 lamp bulb over the pavement at (41.1, −10.4), all out of Ace's reach.
- **A night lab session:** 732 meshes made solid (0.01 s), 3 day-only colliders off, 0 warnings, 0 errors.
- **The day:**
  - The 15 standard day photos, before (`day-photos-with-night-group-2026-09-28_042523`) and after (`..._050450`): 6 identical. The other 9 differ only where the new fences are in view (the parking lot view by 2.1%, the rest under 0.1%). No road works by day.
  - A day lab session (Day 5, autopilot serves): served as usual, 0 warnings, 0 errors.
- **Scene diff against `d6aa026`:**
  - 962 objects added, all under the two new groups;
  - the layout root and `20 - Night walk` each gain a child, and `NightWalk.nightOnly` gains the road works;
  - nothing removed, nothing else changed.
- **Photos:** 24 views, by day and at night, from `Logs/Night/views.json`: `views-day-2026-09-28_050538` and `views-night-2026-09-28_050338`.
- **One slip, no harm:** while opening the Night menu, a click ran `Add the notebook to the recap (open scene)`. It found the block, re-wired the same reference and saved; the scene file came out byte-identical.
  - The safe menu path: click `Fixit Fidget`, *hover* along `Night` into its submenu, then click the item. `Add the notebook...` is the submenu's first item, level with `Night`.
- After the photos and lab sessions, Unity marked the scene as changed. Saved, the file was byte-identical to the commit.
- That save also rewrote `Night walk - works lamp.mat`: Unity brought its unused legacy `_Color` in step with `_BaseColor` (2.6, 0.8, 0.05); it had kept the first, brighter colour. URP's Unlit shader draws with `_BaseColor`, so nothing looks different. It is committed on its own, right after this entry.

**Left open:**

- Mansoor's look at the photos: the jobs at the 8 street ends, and the NW/NE fences, which weren't in the question he answered.
- The two small patio air walls, as above.
- **Part 3:** the overhead camera follows Ace at night.
- **Part 4:** night owls and local cars. With CafeArrivals resting, `CafeArrivals.Players` isn't refreshed at night, and `NpcJourney` uses it to steer round Ace.
- **Part 5:** lab entry, Call it a night, dawn, checks and recording, and the HUD's day clock and counter prompt at night.
- As before:
  - the east street's lantern lights a tree green-yellow;
  - one tower has every window lit;
  - hardening the Café life check waits for Mansoor's say-so.
- **Next:** Mansoor's OK on part 2's photos, then part 3.

## September 28 (early morning) — night walk part 2, first step: solid by night, and the edges surveyed; on `night-walk`

**Read this entry first.** One code commit, `d6aa026`, on `night-walk`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His answer to the part 2 question (28 Sept): "lets choose the route you best think, i dont however want to ruin any immersion from this world we created".

**The route taken** (sent to him before anything was built): wherever Ace can't go, there's a visible reason.

- No invisible walls on open ground, and nothing visible that Ace walks through.
- The 8 street ends get **night road works**, a different job at each, built from POLYGON props, right across the street. Night only: road works at night are ordinary, and the day's traffic is untouched.
- At night only local cars drive (part 4): a patrol car and a taxi, round inside the 9 blocks.
- Collision is fixed where Ace would bump into air or walk through things.
- Order: survey, build, sweep check, day unchanged, photos, commit, docs, report.

**What the survey found** (`Night walk 2 - Survey the edges and collisions (read-only)`):

- **The neighbourhood outside the café has no collision at all.** POLYGON's convex colliders were switched off by prefab overrides when the city was built (1,333 colliders on 764 objects). Only the café room is solid. Nobody noticed, because by day only NPCs walk outside, on their own routes. At night Ace would have fallen through the ground.
- **The patio has an invisible day fence:** three boxes on `Window collision boundaries` (the front one at z −4.05, and the two sides). They keep Ace in the café by day.
- **The 9 blocks are not fully closed by buildings.** Besides the 8 streets, Ace could walk out at two corners in the south:
  - **A, south-west, about 20 m:** past the city car park's open west side and round its corner;
  - **B, south-east, about 21 m:** beside the south-east hotel, out to the empty ground by the round glass tower.
  
  The whole closing line is about 120 m, most of it across the 8 streets and their pavements, building face to building face. The two north streets climb the hill into the backdrop; their road works close them at the edge of the 9 blocks.
- **In Play Mode the city's meshes are static-batched:** `MeshFilter.sharedMesh` returns "Combined Mesh (root: scene)". So the night's collision can't be read from the scene at run time. It comes from a list written in Edit Mode.

**What this step builds:**

- **Solid by night.** While the night runs, 688 fixed meshes near the streets (332,021 triangles) get exact `MeshCollider`s under a scene-root object. They are made in 0.01 s and removed by `End()`, or with the scene.
  - The list is `Assets/Playtests/AcesCafeLayout/Night walk - collision.asset` (`NightCollision`): mesh references with positions, rotations and scales, never mesh copies.
  - Left off: 12 meshes already solid, 721 inside the café room, 440 above 2.4 m, and 13 without a single real triangle (the bay-window houses' emptied doorway pieces among them).
  - **A real triangle is judged by its shape, never its size:** three corners at least a micrometre apart and not in a line. The first version tested the size and dropped 3 real, finely made meshes (the café's brass door and window pulls). They showed up as one walk-through spot at the café entrance.
- **The patio fence** (`NightWalk.dayOnlyColliders`) is off while the night runs, and back on after.
- **Fixed from part 1:** `QuietTheStreet` empties StreetLife's routes. `CafeArrivals` then found no lanes, logged "The entry or exit lane isn't on the street; arrivals come on foot only" and emptied its car pool for the rest of the session. It now rests with the spawners at night: disabled, so its crossings and keep-clear boxes come back in `OnEnable`, and it finds its lanes again after `End()`.
- The night scripts use `FindAnyObjectByType` (Unity 6.5 marks `FindFirstObjectByType` obsolete), so they add no compiler warnings.
- **Code:**
  - `NightWalk`: solid by night, the fence, CafeArrivals;
  - `NightCollision` (new): the list's type;
  - editor: `NightCollisionList` (new, writes the list; the set-up calls it), `NightEdgesSweep` (new: the survey and the sweep), `NightWalkSteps` (the set-up now writes the list and the fence; the rebuild and views menus), `NightWalkSurvey` (the Find calls).
- **Tools** (`Fixit Fidget > Night`):
  - `Night walk 2 - Survey the edges and collisions (read-only)`, in Edit Mode, by day;
  - `Night walk 2 - Sweep the night (Edit Mode, the night simulated)`. An Ace-sized capsule (radius 0.5 m, 2 m tall, step 0.3 m, slopes to 45°) is tested every 25 cm over the district and 12 m round it. It compares the visible world (every renderer as exact collision, in a preview scene) with the night's physics (the scene without moving things or the fence, plus the list), flooded from the café's front pavement. It reports ways out, air walls, walk-throughs, holes and high spots, with `map.png` and CSVs, to `Logs/Night/edges-night-*`;
  - `Night walk 2 - Rebuild the list of what is solid by night (Edit Mode)`: rewrites only the list asset;
  - `Night walk - Photograph the views in Logs - Night - views.json`: any list of views, by day or at night.
- **Scene:** the night group was rebuilt by re-running the set-up (the same 87 objects under new ids), and `NightWalk` gained the list and the three fence boxes. Nothing else changed (checked against `f2788e2`). The set-up also rewrites the night look profile: the same settings, its three effects under new internal ids.

**Checks** (lab sessions; the playtest save was never written):

- **The sweep, with the night simulated:** outside the café room, 0 walk-throughs, and 2 one-spot air walls (the patio A-frame's box and an outdoor chair's box, a little bigger than they look). The only "holes" are roofs above 2.4 m, which Ace can't reach. The last sweep matches the first spot for spot, and logs no errors. The sweep lists the café room separately; its day collision is unchanged.
- **A night lab session:** "688 meshes made solid (332,021 triangles, 0.01 s); 3 day-only colliders off", with 0 warnings and 0 errors.
- **The console's warnings had been hidden** (its warning filter was off). Shown again, they turned up the CafeArrivals warning and the obsolete calls above. The console shows warnings again now.
- **The scene saved after these sessions is byte-identical** to the one checked against `f2788e2`.
- The code commit was amended twice before this entry: a stale git plan had left two files out (`a2c0fa7`), and then the message's account of the gaps was made precise. `d6aa026` is the one to keep.

**How it got there:** a first try in Play Mode made the colliders from `MeshFilter.sharedMesh` and got static batching's combined meshes: 10 million triangles, in the wrong places (the sweep put the street at 19.34 m). Hence the Edit Mode list and the Edit Mode sweep. The solid objects are plain scene-root objects, so they go with the Play scene; hidden, don't-save objects could have leaked out of Play Mode.

**Left open:**

- **The question for Mansoor: how to close the two corners.** Recommended: permanent, day and night. A boundary fence on the car park's open side, with a gate locked at night (A), and a fenced building site on the empty ground by the hotel (B). The other options: new buildings in both gaps; night-only barriers (the day unchanged, but a fence that isn't there by day); or photos of the options first. Nothing of the closing is built yet.
- The two one-spot air walls (the patio A-frame and the outdoor chair): fit their boxes in the build step.
- Part 4: with CafeArrivals resting, `CafeArrivals.Players` isn't refreshed at night, and `NpcJourney` uses it to steer round Ace. The night owls need their own way to see Ace.
- As before: the HUD's day clock and the counter prompt at night (part 5); the east street's lantern lighting a tree green-yellow; one tower with every window lit; hardening the Café life check waits for Mansoor's say-so.
- **Next:** Mansoor's answer on the corners. Then build the 8 road works and the corners, fix the two air walls, and sweep again (no way out, no air walls, no reachable holes or high spots). Photograph the day before and after: it must be unchanged, apart from anything permanent he chooses. Then commit, docs and report.

## September 28 (small hours) — night walk part 1: the night's lighting; on `night-walk`

**Read this entry first.** One code commit, `f2788e2`, on a new branch `night-walk`, made from `npc-mixamo` at `3d6d05f`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His request (27 Sept): "lets build out an actual small city that can be explored inside at night for the night life for ace".

His answers (28 Sept):

- **The 9 blocks (Recommended)**: the café's block and the eight round it. They measure about **80 × 75 m** (the street grid runs from x −38 to 39 and z −34 to 37). An earlier note said "about 115 × 120 m"; that was wrong and has been corrected in the project docs.
- **Sleepy + night owls (Recommended)**, **outside first (Recommended)**, and **"both first person and isometric"**.
- For this step: **a Lab menu for now (Recommended)**; buildings in the overhead view: **"both 1 and 2"** (a steeper camera outside, and buildings fade when they hide Ace); Ace's look outside: **the placeholder for now (Recommended)**.

The plan is in `claude/night-city-proposal.md` in the project: night step 4, "Night walk", in five parts with photos after each. **This is part 1: the lighting only.** There is nothing to do at night yet, and Ace can't walk out into it yet (that needs parts 2, 3 and 5).

- **What the night does** (`NightWalk`, new). Only `Begin()` switches anything on. `End()`, or leaving Play Mode, puts it all back.
  - `CafeDaylight` is held at 23:00, with a moon. The café's own lights inside the room drop to 15%: it is closed for the night.
  - The **39 POLYGON street lamps** light up. Each head gets a spot light and a small bulb that you can see from the street. The **4 older lamp heads** along the west street each get a point light; their glass already glows by evening. All of these are inactive in the scene by day.
  - **Windows:** 346 of 432 POLYGON building parts get lit windows from the pack's five window masks (`Emissive_01–05`). The mask is chosen per building and is the same every night. About one part in six stays dark.
  - **12 signs** glow in their own colours.
  - **The late spot** is the south-west corner shop (`SM_Bld_Shop_Corner_01`, pivot −18, −14.8). Its window glows warm, and two lights spill onto the pavement outside it.
  - **A night look** on its own Volume: more bloom, a touch brighter and cooler. It is at weight 0 by day.
  - **The street goes quiet:** no customers or patrons, the day clock stops, and the day's 46 walkers and cars go home. Part 4 brings the night owls.
  - The glowing materials are run-time copies and are never saved. The scene keeps its originals.
- **Code:**
  - `NightWalk` (new, `Assets/Scripts`);
  - `CafeDaylight`: `SetNight(hour, moon, interior lamps)`. It is neutral by default (no hour held, no moon, lamps ×1), so the day plays exactly as before. The café's own lights are the ones inside the room (`CafeInside`, x −7.4..7.4, z 0.1..18);
  - editor: `NightWalkSteps` and `NightWalkSurvey`.
- **Scene:** the `20 - Night walk` group under `ACE'S CAFE - layout study 02`: inactive lamps, lights and bulbs, the late spot's lights, and the night look's Volume at weight 0. Also the four `CafeDaylight` moon fields.
- **Assets:** `Assets/Playtests/AcesCafeLayout/Night walk - night look.asset` (the Volume profile) and `Night walk - lamp bulb.mat` (URP Unlit, no textures). Neither is from a purchased pack.
- **Tools** (`Fixit Fidget > Night`):
  - `Night walk 0 - Survey the night lighting (read-only)`: lamps, lights and materials in the 9 blocks, written to `Logs/Night/night-survey-*`;
  - `Night walk 1 - Set up the night lighting (lamps, windows, signs, late spot)`: builds the group. It is undoable and safe to run again (it rebuilds). Its report goes to `Logs/Night/night-setup-*`;
  - `Night walk - Take the night lighting out again`;
  - `Play the night walk (lab)`: a Day 5 lab session that starts at night;
  - `Night walk - Photograph the night (Play Mode, night walk)`: the 15 standard views, with post-processing, plus `night-state.txt`;
  - `Night walk - Photograph the day (Edit Mode, before-and-after check)`: the same views by day, for comparing with and without the night group.
- **Checks** (lab sessions):
  - **The day is unchanged.** The Edit Mode day photos with and without the night group match in **15 of 15 views, pixel for pixel**. A Day 5 lab session at 10 AM looked as usual: daylight, traffic and walkers, and nothing of the night.
  - **The scene diff against `3d6d05f` is additions only**: 10,865 lines added and none removed (`git diff --patience`). That is 87 new objects with 45 lights and 39 bulbs, the `NightWalk` and Volume components, and 444 references to existing building and sign renderers (Unity writes those so the list can point into the prefab instances). The only existing things touched are the layout root's list of children and the new `CafeDaylight` fields. The plain `git diff --stat` shows 777 "deletions"; those are lines it counts as moved, not removed.
  - **Night state** in Play Mode: 45 of 45 night-only objects on, 43 lamp lights, 346 of 432 building parts lit, 12 of 12 signs, 8 material copies, 46 street actors sent home.
  - **Photos:** `Logs/Night/night-photos-2026-09-28_020033` (night) and `day-photos-with-night-group-2026-09-28_020615` / `day-photos-without-night-group-2026-09-28_020712` (the day check). A contact sheet and day-and-night pairs went to Mansoor.
- **How it got there** (for whoever tunes it next):
  - POLYGON's material (`Synty/Generic_Basic`) only glows with `_Enable_Emission` set to 1. The window masks are drawn for the `PolygonCity_0x` atlas, so only atlas materials get them.
  - The first bulbs were dark: a URP Lit material asset had lost its `_EMISSION` keyword. The bulbs now use URP Unlit with an HDR colour (6, 4.6, 3), which blooms.
  - The lamps went from 9 to 22 to 40 (spot 116°, inner 60°, range 14 m) before their pools showed at street level. Windows (1.2, 0.84, 0.46) and the late spot (1.5, 1.05, 0.6) were softened once the bloom went up.
  - The night look's profile is rebuilt each set-up with only its own overrides (bloom 0.85 / 0.9 / 0.72, post-exposure 0.35, saturation −6, white balance −10), so it doesn't override anything else.
  - Leaving Play Mode used to log "GameObjects can not be made active when they are being destroyed" ×46. `NightWalk.OnDestroy` now only destroys the material copies.
- **Left open:**
  - The HUD still shows the day's clock and money during the night walk. The café's prompt ("[F] Serve at counter") still shows too. Part 5 deals with both.
  - The six "V3 warm lantern" lights near the café light the tree canopies beside them. On the east street one canopy glows green-yellow. It does the same in the day's evening, where it is less noticeable. It was not touched, to keep the day unchanged.
  - One office tower's mask lights every window. It reads as people working late.
  - Hardening the Café life check is still waiting for Mansoor's say-so (see the previous entry).
- **Next:** part 2, edges and collisions. One question for Mansoor first: the day's cars drive out through the 8 street exits, and part 4's odd car will need to as well. So should the roadworks at the exits close the whole street at night, or only the pavements (with a wall only Ace bumps into across the road)? Nothing of part 2 is built.

## September 27 (just before midnight) — front doors take turns; on `npc-mixamo`

**Read this entry first.** One code commit on `npc-mixamo`: `0e7877c`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His request: "they are congesting on the right side of the house", then "i see alot of congestion whenever some npcs try to go through doors, they just get stuck and then that causes everyother npc to also get stuck, but after that lets build out an actual small city that can be explored inside at night for the night life for ace".

His answers:

- **"Doors take turns (Recommended)"**: one direction at a time through each front door.
- **Walk-ins from the other bay-window houses: "Not now"**. Walk-ins still come only from the dusty rose house and the courtyard shop.

Full notes are in `claude/night-homes-spec.md` in the project, under "Door jams: the doors take turns".

- **What jammed** (from the traces, before the fix):
  - people used the one-person doorway both ways at once;
  - two walkers could each give way to the other for good;
  - new arrivals were spawned onto the same point in the hall;
  - everyone who stepped aside picked the same spot.
  - In lab bursts, passing a door took 22 s at the median at the shop (64 s in a second burst, worst 108 s) and 28 s at the dusty rose door (worst 67 s). In normal flow it takes 4–5 s.
- **How it works now.**
  - Each door keeps turns for its **single-file stretch**: the hall, the doorway and the path out to where two people can pass. That is 6.6 m at the shop and 2.4 m at the dusty rose house.
  - One direction at a time. People going the same way follow 1 s apart. When both sides are waiting, the sides swap after 3 people.
  - **Coming out**, people wait inside the house, unseen, until it is their turn. While waiting they have no body and are not on the street.
  - **Going in**, people wait on a marked **"Wait here"** spot beside the path (and after those, further back along the path). They step back onto the path when their turn comes.
  - Someone who has gone in stands in the hall until the door shuts, as before. If other people need the door, they go straight on in instead.
  - Walkers ask for their turn every frame, and a ticket that nobody asks for in 0.75 s is dropped. A walker who vanishes can't block a door.
- **Code:**
  - `StreetDoor`: the turns (`Ask`, `MayGo`, `Leave`, `WaitSpotFor`) and the new fields `waitSpots`, `turnBatch` (3) and `followGap` (1 s);
  - `NpcJourney`: `DoorPassage`, `TakeTurn` and the unseen waiting. Giving way now ignores people waiting inside and the door's own stretch, and people who give way spread out instead of sharing one spot;
  - `CafeArrivals`: hands each walk its door (arrivals going out; departures and turned-back visits going in) and works out where the single-file stretch ends (`SingleFileEnd`). The first door hold on arrival is gone;
  - editor:
    - `StreetDoorSteps` (Doors 4);
    - `StreetDoorCheck` (Doors 3 checks the spots; the play check accepts a walker waiting inside);
    - `StreetDoorRushCheck` (new);
    - `CafeLifeRecorder` (two door cameras);
    - `CafeArrivalsRecorder` (the trace shows each walker's door state);
    - `CafeParkingLot` (its lane and obstacle helpers are now shared).
- **Scene:**
  - 9 waiting spots, as `Wait here N` children of their doors: saffron 3, dusty rose 2, shop 4;
  - the new door fields;
  - Step B's `copyFingers`/`handsToFace` defaults on 20 bodies (value 1, as in the code).
  - After later Play sessions the title bar showed an unsaved `*` again. The committed scene is the one saved right after Doors 4. Don't save the scene without diffing it first.
- **Tools:**
  - `Fixit Fidget > Night > Doors 4 - Mark where people wait for their turn at the door`. It is already run and safe to run again. It writes photos and a report to `Logs/Night/door-waiting-spots-*`;
  - `Fixit Fidget > Checks > Street doors - rush hour at the shop door (Play Mode, lab session)` and `… at the dusty rose door`. Each sends five walk-ins out of the door and five people home into it, all at once. It pauses the café's spawners and waits for a clear door, so every run is the same test;
  - `Fixit Fidget > Café life > Record 8 minutes - shop door camera (east street)` and `… dusty rose door camera (west street)`.
- **Checks** (lab sessions):
  - **Rush, shop door:**
    - before: 7/11;
    - after: 12/12 (`232412`) and 12/12 (`233332`).
    - The longest anyone stood at the door was 12.3 s, then 8.1 s, both on a waiting spot for their turn.
  - **Rush, dusty rose door:**
    - before: 8/11;
    - after: 12/12 (`233048`).
    - The longest anyone stood in view was 9.0 s, at the crossing. At the door it was 3.6 s or less.
  - **The same yardstick for before and after.** The rush check itself was tightened between the first run and the last, so the before and after runs were measured again the same way, from the arrivals traces:

    | | shop before | shop after (2 runs) | rose before | rose after |
    |---|---|---|---|---|
    | doorway used both ways at once | 64 s | 0 / 0 | 39 s | 0 |
    | people walking into each other (< 0.4 m) | 12 times | 0 / 0 | 11 times | 0 |
    | three or more standing shoulder to shoulder (0.7 m) | 12 s | 0 / 0 | 9.5 s | 0 |
    | …within 1 m | 12 s | 0.1 s / 0 | 14 s | 5.2 s |
    | longest standing still by the door | 19 s (and 2 teleports) | 12 s / 8 s | 7 s | 4 s |

    The before runs still had the café's own traffic running. One extra patron got caught in the shop jam.
  - Street doors **25/25**, Grace's home **20/20**, Doors 3 all clear.
  - **Café life check** (it doesn't use the door code): 29/32, 29/32, 30/31. It failed on a different timing race each time:
    - a chat the director had already started ended during the check's 2.2 s wait;
    - a run started in a lab that was already busy. The table seat next to the tub chair was taken, which blocks the tub chair (their stand points are 0.8 m apart), and one sitter didn't turn to the other in time;
    - in a fresh lab, two of the five sofa patrons had already got up before the check looked at them. The likely reason is their own 40–90 s stay.
    - Before the doors work it was 32/32 twice (plus 31/32 once, fixed in Step B).
    - The door turns can move the moment a patron arrives by a second or so per person at a shared door. That is exactly the kind of thing this check is touchy about. A proposal to harden the check (hold the sofa patrons' stay while it runs; wait out a chat that's already going; start only in a fresh lab) is **waiting for Mansoor's say-so**.
- **Recordings** (`Logs/`, git-ignored):
  - door cameras in `Logs/CafeLife/`: `2026-09-27_223719` (shop, before) and `_223953` (dusty rose, before); `_232424` and `_233332` (shop, after) and `_233048` (dusty rose, after);
  - arrivals traces under the same times in `Logs/ArrivalsTrace/` (the shop's after runs are `_232424` and `_233333`);
  - the side-by-side clips `Logs/CafeLife/clips/door-turns-shop-door-before-after.mp4` and `door-turns-dusty-rose-door-before-after.mp4` went to Mansoor.
- **Left open:**
  - the dusty rose door is tight. Its two waiting spots are 0.8 m apart, so two people waiting and one passer-by make a "three within a metre" moment of about 5 s;
  - when ten people reach one small door at once, there is still an orderly queue in front of it for about 8 s;
  - hardening the Café life check (above);
  - no `took over 120s to walk away` warning showed in any of these runs;
  - walk-ins from the other bay-window houses (his "Not now");
  - step C of the Mixamo clips: lean spots, and a gadget in hand for Holding Idle.
- **Next:** Ace's night city, which Mansoor asked for after the doors. It is to be talked through with him before anything is built.

## September 27 (late night) — Mixamo step B: the clips wired into the café's people; on `npc-mixamo`

**Read this entry first.** One code commit on the new branch `npc-mixamo`, made from `night-notebook`: `aa37d48`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

His answers after the step A photos:

- **"32 clips + fingers + ear fix"**: wire the good clips into waiting idles, frustration, happy reactions, greetings and seated chats; the city bodies copy their fingers; the phone goes to the ear. Photos and a recording come to him before he plays it.
- **"Sofas only, drop Seated Idle"**: Sitting Laughing and Sitting Thumbs Up only on the sofas and the tub chair; Seated Idle is not used.

Full notes, with the table of which clip plays when, are in `claude/mixamo-clips-batch1.md` in the project, under "Step B, as built".

- **Only how people look changes.** Queue order, patience, orders, money and the day are untouched.
- **How it plays.**
  - The customer animator has a second layer, **Beats**, with one state per clip (35). Its weight is 0 unless a beat is showing, and the café's own animation keeps running underneath.
  - **No Mixamo motion is in the public repo.** The animator holds empty placeholder clips. The baked clips are listed in `NpcBeatLibrary.asset` inside the git-ignored `Assets/Art/Mixamo/`, and `NpcBeats` swaps them in at run time. Without that folder, people use the café's own clips exactly as before.
  - A beat only shows while the body holds still. A walk, a step round, a turn on the spot or standing up fades it out in 0.15 s.
  - When a beat can't play, the brain falls back to the old "Interact" gesture.
- **When.**
  - Reactions where the one "Interact" gesture used to play: a greeting when Ace comes over, a nod for an order, thanks, excitement, a shrug, disappointment, relief, the angry walk-out.
  - Idle variety, chosen by standing or seated, the movement profile and the patience bar.
  - Frustration a step at a time: a sulk under 30% patience, a head shake under 12%, and the angry gesture only as they walk out.
  - Calling for a drink: they look at Ace and, on foot, turn to him before they wave.
  - Seated chats use the two talking clips, and the sofas laugh.
- **Seats:** exactly the five lounge seats have no table in front (`NpcBeats.TableInFront`). Sitting Laughing and Thumbs Up play only there, Tapping Fingers only at tables.
- **Seated feet:** `Mixamo 1` now rests the seated clips' feet where the café's own sit clip rests its ankles. They had been 2–10 cm into the floor on the city bodies (already visible in the step A photo report).
- **Fingers:** `PolygonNpcVisual` copies 22 finger bones per city body, joint for joint (measured). All 17 looks.
- **The phone:** POLYGON City's smartphone in the right hand for Texting and the call. On a call the arm brings the phone's speaker to the ear, measured per look. Only city bodies take calls: the rig's own body can't reach its ear convincingly.
- **Code:**
  - new: `NpcBeats`, `NpcBeatLibrary` (`Assets/Scripts`), `NpcBeatChecks` (`Assets/Editor`);
  - changed: `CustomerBrain` (reactions, the drink call), `NpcSocial` (idle and talking beats), `NpcAttentionDirector` (laughs), `NpcPosture` (quiet while a beat shows), `PolygonNpcVisual` (fingers, phone at the ear), `NpcLocomotion` (a read-only `FacingLeft`), `NpcMixamoClips` (Mixamo 3 and 4; seated feet in Mixamo 1), `CafeLifeRecorder` (queue camera, keep the day open), `CafeLifeProbe` (the trace names the clip: `;mx=`);
  - `CustomerAnimator(.controller` (the Beats layer), `Customer.prefab` and `Patron.prefab` (`NpcBeats`);
  - two checks taught the beats: `NpcLifeChecks` (a seated talker may use the talking clip) and `NpcAttentionCheck` (the thanks may be a clip).
- **Tools:**
  - `Fixit Fidget > NPC > Mixamo 3 - Wire the clip beats (animator layer, library, prefabs)` (already run; safe to re-run);
  - `… > Mixamo 4 - Photograph the wiring (fingers, phone call, sofa)`;
  - `… > Mixamo 5 - Beat check (Play Mode, lab session)`;
  - `Fixit Fidget > Café life > Record 8 minutes - queue camera (from behind the counter)` and `… > Lab > Keep the day open 5 more minutes`.
- **Checks** (lab sessions):
  - `Mixamo 5` (beat check): **51/51** in all five runs. Every clip plays on the right body and fades out cleanly; the body never moves; seated hips move 0.1–3.8 cm at a table and 5.8–10 cm on a sofa (Thumbs Up, Laughing); seated feet now 0.0–0.7 cm above the floor at a table (the café's own sit clip: 0.7) and 0.6–2.7 cm on a sofa (own: 2.7); on a call the phone is 0.0 cm from its spot at the ear; rising or walking off mid-beat clears it at once; in 75 s of the café by itself no beat showed while a body moved;
  - `Café life check`: **32/32**, once taught the talking clip;
  - `Attention check`: **10/10**, once taught that the thanks may be a clip (two runs stopped short when none of its three customers wanted a drink, which is random);
  - `Sit 4`: **14/14** after the feet fix (the run before it caught the feet; it also had two walkers pass 0.35 m apart once, random crowding).
- **Photos and recordings:**
  - recordings in `Logs/CafeLife/` (git-ignored): `2026-09-27_203833` (counter camera: showed the angry gesture twice in a row and the wave at the window, both fixed), `_205216` (queue camera, the beat check), `_205821` (queue camera, a hello, nods, then nobody serves), `_210652` and `_213713` (lounge, before and after the feet fix), `_214620` (lounge, the beat check's sofa clips);
  - captioned clips in `Logs/CafeLife/clips/mx-0*.mp4` and the wiring photos went to Mansoor.
- **Left open:**
  - step C: lean spots for the two leaning clips (wired, never picked yet), and the gadget in hand for Holding Idle (empty hands meanwhile);
  - the lab's Day 5 patience is short (40–60 s), so there the three frustration steps come within about 10 s;
  - seen while checking, not from the beats: `[CafeArrivals] ... took over 120s to walk away` warnings in a long "I serve" lab session (the backstop for walks that never end). To look at with the street doors.

## September 27 (night) — the street's front doors really open; 36 Mixamo clips downloaded; on `night-notebook`

**Read this entry first.** One code commit on `night-notebook`: `1131eb7` (front doors). This entry and the project docs come in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

Mansoor's request: "no the house is fine but lets now add doors for these houses. so we can see them leave and enter more believably, also i saw some animations of sitting in maxiamo like sitting and laughing and sitting and talking, can we find some more sitting and idle standing or leaning and frustrations and happy animations, all kinds when they come in".

His answers:

- a real doorway, with a door that swings in;
- all six houses and the courtyard shop;
- the first batch of 36 clips;
- doors first, then clips.

**Grace's house:** she stays in the saffron house ("the house is fine"). The open call in the entry below is closed.

**Front doors (`1131eb7`)**

Full notes are in `claude/night-homes-spec.md` in the project, under "Front doors that open".

- **What you see.** All six bay-window houses and the courtyard shop have a real doorway.
  - The house's own door swings inwards 88° on a hinge, opening in 0.35 s and closing in 0.6 s.
  - Behind it is a short dark hall (1.6 m), with no interior.
  - **Coming out:** people wait in the hall until their door is open, step out, and the door closes behind them.
  - **Going in:** the door opens as they reach it. They stand in the hall while it closes, and only then leave the game.
- **Code:**
  - `StreetDoor` (new) swings the door, and anyone can hold it open;
  - `CafeArrivals` holds a route's door for the walker and adds the hall to the walk;
  - `NpcJourney` gained gates: a walker waits until the door is open, and goes anyway after 4 s.
- **Tools** (`Fixit Fidget > Night`):
  - `Doors 1 - Survey the front doors (read-only)`;
  - `Doors 2 - Give the houses real front doors` (already run). It cuts the doorways into copies of the merged meshes;
  - `Doors 3 - Check the front doors (read-only)`;
  - `Doors - Put the old doors back`, which undoes Doors 2.
  - Assets: `Assets/Playtests/AcesCafeLayout/Street doors.asset` and `Street doors - dark hall.mat`.
- **Checks:**
  - `Doors 3`: all clear;
  - `Checks > Street doors (Play Mode, lab session)`: **25/25**. Grace, a dusty rose walk-in and a courtyard walk-in each come out and go back in. The door is open before they pass, closed behind them, and shut before they vanish;
  - `Checks > Grace's home`: 20/20. She now starts in her hall;
  - `Checks > Night notebook`: 16/16;
  - `Night > Check homes`: all clear.
  - The scene diff was read line by line: only 43 mesh references changed (to the "(doorway)" copies), plus the new door objects.
- **Left alone:** the bay houses' carved bracket dips to 2.28 m inside the door head. It was already there, and it is above head height.
- **Left over:** one unused mesh from a first run in `Street doors.asset`. It is harmless.

**Mixamo clips, batch 1 (downloaded and in the project; not baked or wired)**

Full notes are in `claude/mixamo-clips-batch1.md` in the project. They cover the list with lengths, the checks, the suggested moments and the proposed next step.

- **36 clips** on the X Bot rig: without skin, FBX for Unity, 30 fps, no keyframe reduction. Each was downloaded with Mansoor's account through Mixamo's own Download button.
  - **24 standing** in `Assets/Art/Mixamo/Standing/`: idles, phone, leaning, frustration, happy, greeting, nod.
  - **12 seated** in `Assets/Art/Mixamo/Sitting/`: idles, chats, laughing, impatience, thumbs up, beckoning.
  - **Git-ignored** with the rest of `Assets/Art/Mixamo/`, 31.3 MB. The originals are still in his Downloads.
- **Renamed:** the two duplicate names became `Sitting Idle - Breathing` / `Sitting Idle - Hands On Thighs` and `Sitting - Looking Side To Side` / `Sitting - Impatiently Waiting`, and Mixamo's "Dice Idle" became `Sitting - Tapping Fingers`.
- **Checked** (every file read back):
  - all use the same 65-bone `mixamorig` skeleton, with no mesh;
  - the hips end where they start, so every clip loops;
  - two seated clips put the pelvis well off the origin (up to 31 cm), and two sit higher (hips at 75–79 cm), so the bake has to line each seated clip up with the seat.
- **His answers:** bake and photos first, nothing wired; the phone is POLYGON City's `SM_Prop_SmartPhone_01`; once wired, all four kinds of moment matter (waiting idles, frustration, happy reactions, greetings and chats).

**Mixamo Step A: baked onto the café rig and photographed (nothing wired)**

- **New editor tool `Assets/Editor/NpcMixamoClips.cs`** (tracked):
  - `Fixit Fidget > NPC > Mixamo 1 - Bake the Mixamo clips onto the cafe rig` writes 36 `Npc Mx …` clips to `Assets/Art/Mixamo/Baked/` (git-ignored);
  - `… > Mixamo 2 - Photograph the baked Mixamo clips (contact sheet)` writes `Logs/NpcMixamo/photos-<time>/`.
- **Same retargeting as the Quaternius sit and gait clips:** limbs copy Mixamo's directions, the pelvis is scaled by hip height, the feet use leg IK, and the clips stay in place.
  - **Collarbones** take Mixamo's turn from its own rest, dropping or pulling back at most 4° (the Pass 2b lesson).
  - **Seated clips** are lined up on `NpcSitData.seatedHip`, so `NpcSeating` places them like today's sit clips.
- **Watch out: Unity leaves Mixamo's bones in each clip's first frame, not the T-pose.** The first bake was wrong because of it. The X Bot's T-pose is now a table read from the FBX files, checked against Unity's own import before every bake (agrees within 0.00°), with every file's bone lengths checked too.
- **Photos** are in `Logs/NpcMixamo/photos-2026-09-27_184151`; the contact sheets went to Mansoor.
  - 32 of the 36 look right.
  - **Talking On A Cell Phone** needs the hand pulled to the ear: the city bodies' longer arms put the phone in front of the mouth.
  - **Sitting Laughing, Sitting Thumbs Up and Seated Idle** don't suit a table: they bend into it, or the hands don't reach it.
- **Found: `PolygonNpcVisual` copies no fingers**, so a thumbs-up or a point shows as an open hand on the city bodies.
- **Next, to agree with Mansoor first (Step B):**
  - wire in the clips that look right;
  - fingers for the city bodies;
  - the ear IK for the call;
  - the table-shy seated clips on sofas only, or dropped.

  Full notes are in `claude/mixamo-clips-batch1.md` (project).

## September 27 (evening) — café walls slide down, the painting wall is glazed, night step 3: Grace's home; on `night-notebook`

**Read this entry first.** Two commits on `night-notebook`: `7188a0a` (walls) and `4030b8f` (Grace's home). Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

Mansoor's request: "the wall with the painting doesnt have a glass side, and also theres a wall that disappears when we rotate the camera a little the back wall and the wall that has the tan concrete wall dissapear … also theres street address just put a placeholder for now and have it where i can always edit the street names. also pick wherever house would suit grace".

**Walls (`7188a0a`)**

- **They slide down instead of vanishing.** Mansoor chose "slide down to sill height". A wall that hides the room from the overhead camera now slides down to 0.78 m in 0.25 s, and comes back up once it is clearly out of the way (a margin plus a short delay, so it never flickers).
  - Its full-height shadow stays.
  - Anything fixed high on it hides with it, and nothing floats.
  - Code: `CutawayWall` and `CafeViewMode`, "Cut-away walls" section.
  - The back bar's cup shelf got its own mesh so it can hide with the back wall: `Fixit Fidget > Neighborhood refresh > Second pass > Give the back-bar cup shelf its own mesh`, undo `… > Put the cup shelf back into the merged meshes`.
  - Check: `Fixit Fidget > Checks > Wall cut-away (Play Mode, lab session)`, 31/31.
- **The painting wall (east, courtyard side) is glazed.** `… > Second pass > Glaze the painting wall (courtyard windows)` adds one pane of the café's own window glass across its open bays, with no collider. The paintings stay on their piers. Undo: `… > Take the glass out of the painting wall`.

**Night step 3: Grace's home (`4030b8f`)**

Full notes are in `claude/night-homes-spec.md` in the project, under "As built".

- **Where she lives.** Mansoor asked me to pick. It is house 1, the saffron house, the closest to the café door. Walk-ins now come from the dusty rose house next door (a new route, measured; the car park check says "all paths clear").
- **Her address is a placeholder: 12 West Street.**
  - Street names live in one asset: `Assets/Data/Resources/District streets.asset` (West, East, Front and Back Street). Rename freely; keep the ids.
  - Notebook lines store the street's id, so a rename reaches older lines too.
  - The house number is on the Home Door component of "Grace's front door (home.grace)", under the saffron house.
- **Coming and going.** Grace always walks out of her own front door to the café and back in afterwards, never by car.
- **What Ace notices.** When Ace sees her within 3 m of her door, the notebook gets "Came out of / Went into the saffron house at 12 West Street." as a hunch; it becomes likely when seen there on a later day.
  - Seen means in the camera's frame with nothing solid in between. Clear glass and cut-away walls see through.
  - The recap marks unsure facts "(hunch)" or "(likely)" and lists facts that became surer today as "(likely now)".
  - The save gains `surerDay`, additive and still version 5.
- **Tools:**
  - `Fixit Fidget > Night > Give Grace her home (the saffron house, 12 West Street)` (already run; safe to re-run);
  - `Fixit Fidget > Night > Check homes (read-only)`.
- **Checks:**
  - `Checks > Home rules`: 40 assertions, also `Tests/HomeRules`;
  - `Checks > Night notebook rules`: 42;
  - `Night > Check homes`: all clear;
  - `Checks > Grace's home (Play Mode, lab session)`: **20/20**. She walks out, to the café door and back in, twice: watched, then with the camera away. About 1½ minutes.
  - `Checks > Night notebook (Play Mode, lab session)`: 16/16.
- **Open call for Mansoor: her door is just out of the usual overhead view.**
  - In the view a session starts with, her doorstep is just below the bottom edge of the screen.
  - It is seen at 23 of 36 camera turns at the usual zoom, at 30 of 36 zoomed out, and from the café sofas in first person.
  - The dusty rose house's door is seen from the default view.
  - Keep her where she is (spotting her takes a little curiosity), or move her next door. Moving her is a small change, not made unless he asks.

**Mixamo animations (Mansoor approved: "download whichever we need for the game")**

- Four clips from Mixamo on the X Bot rig, without skin: `Left Turn 90`, `Right Turn 90`, `Left Strafe Walking` and `Right Strafe Walking` (the strafes In Place). They are for NPCs turning on the spot and side-stepping.
- They are in `Assets/Art/Mixamo/`, which is **git-ignored** (downloaded with Mansoor's Adobe account; the repo is public). The originals are still in his Downloads.
- They are not wired into any animator yet; that belongs to the NPC passes.

## September 27 (later) — night track step 2: Ace's notebook (data only), on `night-notebook`

Mansoor asked to start Ace's night-time life "only if you think we are ready". Together we picked the smallest first piece: the notebook, data only. Every later night step reads from it, and it changes nothing about how a day plays. The spec was signed off as written (`claude/night-notebook-spec.md` in the project). It is built and committed on `night-notebook` (`0fde3d8`), which was made from `main` after `npc-polish` was fast-forwarded into it (`main` = `b36878e`). Nothing is pushed. The Day 2 Grace/reputation playtest is still Mansoor's open milestone: the playtest save was never opened (lab sessions only, and the play check confirms the file was not written).

- **What the player sees:** at closing, a **Notebook** block under the reviews in the recap, but only on a day something was learned. It shows up to five of today's facts as "Name: fact", then "+N more", then the running total ("6 facts about 3 people so far"). Resuming a saved recap shows the same block.
- **Where facts come from (only what is already in the game):** Grace's camera intake gives four facts (the strap, her husband, the reunion tomorrow, "usually stays behind the camera"). Her return gives one photo fact per outcome (clear, smudged, missed). Any other regular's repair gives "Brought in a {device}: {fault}.", and walk-ins give nothing. A save that already has Grace's Day 1 visit is backfilled with her four facts, dated Day 1. So on the Day 2 playtest the recap should show 1 new (her photo) and 5 facts in total.
- **Code:** `Notebook` (plain C#: same id = same fact, sureness only rises, snapshot/restore), `NotebookEntries` (the authored table), `NotebookHooks` (called from `CustomerBrain.HearIntake` and from `CustomerIdentity.AcceptReturnMemento`), `NotebookRecap`, `SaveData.notebook` + `NotebookFactData`, `SaveManager.Notebook`, `RecapUI.notebookText`. Nothing reads the notebook yet.
- **Save:** additive and **still version 5**, which differs from the spec's "v6" on purpose. Older builds refuse a save with a higher version number; with no bump, a save made on this branch still loads on `main`, which simply drops the notebook the next time it saves.
- **Tools:** `Fixit Fidget > Night > Add the notebook to the recap (open scene)` (already run on the café scene; running it again only re-wires, and a block still in the first 230-tall layout is updated). `Fixit Fidget > Night > Preview a notebook day in the recap (Play mode, recap open)` shows a long made-up day and saves nothing.
- **Checks:** `Fixit Fidget > Checks > Night notebook rules` (42 assertions, no scene) and `Fixit Fidget > Checks > Night notebook (Play Mode, lab session)` (16/16). The play check covers the real hooks, the recap text and the layout: the block sits 20 units under the reviews and 20 above Open Tomorrow, and a worst-case day of 11 lines fits at 24pt. `Tests/NotebookRules` passes (dotnet, 38 + 2). Recap save checkpoint, Reputation rules and `Tests/ReputationRules` still pass with the new `SaveData`.
- **Fixed while checking:** the first block (230 tall, fixed 22pt) cut off the last lines of a long day. It is now 640×290 at 24pt and shrinks to 16pt only if a day will not fit. The heading is styled like "Today's reviews".
- **Not in this step (per the spec):** the seen/read/overheard/found sources, questions from conflicting facts, a key or in-world object to open the notebook, and any night gameplay.
- **Next on the night track** (`claude/ace-after-dark.md` §8) is step 3, homes for regulars. It needs Mansoor first: which front door is Grace's, and the `home` text is his to write. Small open wording point: the generic line reads "Name: brought in a phone: cracked screen." (two colons); a bracketed version is a one-line change if he prefers.
- **Two older requests (the 23 Sept second-pass list) turned out never to have been done; checked today:**
  - **Floating door pulls: fixed** (`cf25fc8`). In GPT Astra's `EntranceRefresh.fbx` each open door leaf was built at two angles. The stiles, hinges and pulls hang together and meet the frame, but the glass, rails, bottom panel, beads and kick plate were turned 24° the other way, so from above each door was an X and the pulls sat up to 0.2 m off the glass. `Fixit Fidget > Neighborhood refresh > Second pass > Line up the entrance door leaves` turns that infill (12 pieces per door) onto the stiles' line in copies of the meshes (`Assets/Art/CC0Neighborhood/Authored/EntranceRefresh aligned/`). The FBX is untouched, and `Put the original entrance doors back` reverts it. Before/after photos: `Logs/NeighborhoodRefresh/entrance-2026-09-27_110747` and `_111533`.
  - **"The café's left side has no windows": needs Mansoor.** *(Answered and done the same evening: it was the painting wall; see the entry above.)* The 23 Sept wall photos (`Logs/NeighborhoodRefresh/walls-2026-09-23_130547`) show the west wall (the sofa side, on the left from the default camera and from the street) is glazed end to end. The east wall has two solid bays between its windows, the ones carrying the two paintings; from behind the counter, facing the room, that wall is on Ace's left. The back wall is solid behind the counter. Which wall he means decides the fix, and glazing the east bays would move the paintings.
- **Night step 3 (homes for regulars): spec written, waiting for Mansoor** *(built the same evening; see the entry above)* (`claude/night-homes-spec.md` in the project; nothing built). Grace gets one front door she always comes out of and goes back into; when Ace can actually see her at that door, the notebook gets a *seen* fact (hunch, then likely on a later day; sure is left for the night). His decisions: which of the six bay-window houses across the west street is hers (recommended: 2 dusty rose or 3 sea green, directly opposite the café's west windows), her address line (unused for now), whether Ace must be watching (recommended), and whether sightings stop at likely (recommended). Read-only survey: `Fixit Fidget > Night > Survey the bay-window houses (read-only)`, photos in `Logs/Night/homes-2026-09-27_142041`.

## September 27 — café NPC Pass 2 (life layer), 2b and 2c (walk up and sit), on `npc-polish`; STOP-and-report point

**Read this entry first.** Pass 2 (the life layer) was built on the evening of 26 Sept; Mansoor then played it and reported that people "idle and then hover" to their chairs, spin at the counter after ordering ("walk backwards and then quickly circle rotate"), that it "doesn't feel smooth", and that some of the new walks put the shoulders in odd places. Pass 2b (night of 26–27 Sept) and Pass 2c (27 Sept) fixed those. Committed on `npc-polish`: `9424572` (Pass 2 + 2b) and the Pass 2c commit after it (not merged, not pushed). No new major system has been started. The Day 2 Grace/reputation playtest is still an open manual milestone; the playtest save is untouched (every session was a lab session).

- **Full write-up with numbers:** project doc `claude/npc-pass2-life-results-2026-09-27.md`.
- **Pass 2:** NPC layer + give-way so Ace cannot be shoved (0 m in the doorway stress); movement profiles (`Assets/Data/NpcProfiles`, Grace = Relaxed); three walks and two idles baked from the CC0 UAL library already in the project (`Fixit Fidget > NPC > Gait 1/2/3`); five lounge seats (sofas, tub chair) through the normal seat reservations, lounge furniture Not Walkable; `NpcSocial` + `NpcAttentionDirector` (idle beats, glances, acknowledgements, short seated chats, arrival looks); queue attention.
- **Pass 2b, the causes:** (1) `NpcLocomotion.Update` reset its walk timer every frame while the agent was off, so the hand-walked steps into and out of a chair played Idle — the "hover" (walk clip 10 % → 100 %, slide 1.2 s → 0). (2) The steps were two straight legs faced at 360°/s (now one smooth curve, least turning, the rest done while sitting down). (3) Seats were random anywhere (now the nearest by walking distance; groups spread). (4) The counter customer spun on still legs during the hand-over gesture and stopped at the step-back point. (5) The library clips turned the collarbones 54° off the rig (now the rig's own). (6) The customer being served is attentive; the rest of the queue glances.
- **Pass 2c, the causes (measured with `Tools/CafeLifeAnalysis/turns.py`, `pass2c.py`):** (1) the walk out of a chair always went round to the stand point behind it — loops of up to 440° and a stop-and-spin after (now weighed towards where they go next; loops 15–22 per recording → 0). (2) People coming along the table walked past their seat to the chair's back corner and slid backwards into it while turning round (now `NpcSeating.ReadyToTakeOver` hands over at the chair's side, or 1.3 m out along the aisle; direction change at the hand-over p90 110–134° → 37°). (3) At the counter the step back went straight back — an about-turn either way round, then a second turn; and the turn-away waited on the wrong animator state name (now: after the gesture, step back only when someone is in the way and towards the seat's side; turning beyond the need 43–159° → 0–5°). (4) Every set-off flickered the walk clip (Walk, Idle, Walk ~3 a minute), paused the turn and hit full speed in 0.25 s (now held, carried, and built up at 2.2 m/s²; flickers → 0–0.16/min).
- **Checks (final code):** Café life 32/32 (new: a group at the tables followed out onto their next leg — no loops, turning, walk-clip flicker), live sit 14/14 (early hand-overs from 1.3–1.5 m), attention check (its gesture test looked for the wrong state name too; fixed), Café life setup 64/64; no exceptions.
- **Recordings / clips (git-ignored):** Pass 2c: `Logs/CafeLife/2026-09-27_024102` (tables and door, every seat filled), `_023624` (counter, three customers at once). Pass 2b: `_005651`, `_010324`, `_010757`, `_011123`, `_011948`, `_012617`. Clips `Logs/CafeLife/clips/p2b-0*.mp4` and `p2c-0*.mp4`.
- **Watch for:** turning on the spot (at the counter, before setting off) plays the forward walk slowly in place — a Mixamo turn-in-place pair and a side step would replace it (needs Mansoor's own Adobe sign-in; the files stay out of the public repo); the lab's stress bursts (21 or 6 people spawned at the door in one frame) still produce a few 2–4 s door stalls.

## September 26 (night) — café NPC Pass 1b: the first feel layer, on `npc-polish`; STOP-and-report point

**Read this entry first.** Mansoor asked to continue straight from the movement pass into a small layer of visible life, then stop and report. Done and committed on `npc-polish` (not merged, not pushed). The next NPC pass (conversations between customers, groups, movement personalities, extra walk sets, sofa/banquette, richer idles) is **not** started. The Day 2 Grace/reputation playtest is still an open manual milestone; the playtest save is untouched (every session was a lab session).

- **What Pass 1b added** (results and numbers in the project doc `claude/npc-pass1-movement-results-2026-09-26.md`, top section):
  - `Assets/Scripts/NpcLookAt.cs` — head/neck look-at (world-space turn of the rig's Neck and Head after the Animator, before `PolygonNpcVisual` copies the bones; yaw ±72°, fades out past ~105°). Brains decide the target; the component only turns the head.
  - `CustomerBrain`: queued customers look at Ace near the counter; the conversation partner squares up once when the panel opens; waiting customers follow Ace when he carries *their* order (device or matching cup), standing ones turn to meet him inside 2.6 m, seated ones move only the head, the gaze holds through the hand-over; queue variation (personal offset ±0.18 m / depth / yaw ±14° per customer, small weight shifts every 7–14 s) — queue order and slot logic unchanged.
  - `NpcLocomotion`: turn-then-walk when a leg starts > 60° off the heading (≤ 0.5 s at 360°/s); `DriveWalk(speed)` so a hand-moved body keeps a matched stride.
  - `NpcSeating`: eased steps round the chair, turn to the table over the last 40 % at 360°/s, walk clip from real speed, turn away over the first 40 % of the return.
  - `Assets/Editor/NpcAttentionCheck.cs` — `Fixit Fidget > NPC > Attention check (Play Mode)`; run it in a Day 5 "I serve" lab. 8/8 today.
  - `CafeLifeProbe` writes `look=<yaw>` while a head is turned; `Tools/CafeLifeAnalysis/compare.py` takes any number of recording sets (`compare.py rec after final`) and reports head-look and queue-variation columns.
- **Measured (same four scenarios):** stuck 37 → 4 → 4 episodes; person-seconds 500 → 11 → 12; worst stall 44.7 → 4.3 → 4.0 s; spins 11 → 2 → 0; flicker 18 → 0 → 0; head-look episodes 0 → 0 → 27 (154 s); queue yaw p90 0° → 0° → 9–16°. Ace shoved in the doorway 4.0 → 0.2 → 1.2 m (varies with how many squeeze past; physics, not navigation — see the doc).
- **Checks:** all 25 edit-mode checks pass; live sit check 14/14; controller 26/26; attention 8/8; no console errors across the four recordings. One check (`Customer delivery from either hand`) failed once mid-work because the cached `PlayerCarry` ignored a `player` injected by the check; fixed (`DeliveryCarry` derives the carry from whoever `player` is).
- **Recordings kept:** Pass 1b: `Logs/CafeLife/2026-09-26_164855` (R1), `_165301` (R2 stress), `_165902` (R3 counter), `_170112` (R4 tables and door); `_161548` is a counter-camera look-at test with Ace put in front of the counter (he was behind the queue, so nobody looked — that is the design working). Clips `Logs/CafeLife/clips/final-0*.mp4`; maps `analysis/hotspots_before_pass1_final.png`; tables `analysis/compare_before_pass1_final.txt`. All git-ignored.
- **Watch for:** a standing customer turning to meet Ace on a delivery has been exercised by the check's logic path but the check's random target was seated both times — one manual hand-over to someone at a loiter spot is worth a look. Regulars wear the original rig body; walk-ins wear city bodies; both follow the head look.

## September 26 (evening) — café NPC movement, Pass 1 done on branch `npc-polish`; STOP here and report

**Read this entry first.** The movement foundation pass (Pass 1 of the NPC plan) is built, measured and committed on the branch `npc-polish` (main is untouched since `fc0b670`). The next NPC phase (seating variety, conversations, social groups, personality profiles, animation variation, look-at) has **not** been started: Mansoor asked for the before/after report first. The Day 2 Grace/reputation playtest is still an open manual milestone; the playtest save was not touched (every session here was a lab session on `playtest-cafe-lab.json`).

- **What Pass 1 did** (details and numbers: `claude/npc-pass1-movement-results-2026-09-26.md` in the project):
  - New agent type "Cafe NPC" (radius 0.35) on the café surface and both NPC prefabs, routes rebaked; doorway 1.00 → 1.32 m, every seat stand point off the mesh edge. `Fixit Fidget > Café life > Pass 1 - 1/2/3` re-applies it; `Pass 1 - Report` lists the state.
  - `Assets/Scripts/NpcLocomotion.cs` — the one movement component for customers and patrons: heading follows the route (no more turning with every avoidance wiggle, no destination yaw mid-walk), walk animation with hysteresis and a `WalkRate` that matches the stride to the speed (1.6 m/s ± 12 %), progress measured along the route, recovery ladder re-path → step aside → push through → give up (brain decides) → gentle re-path/pass-through, and a "carried backwards → stop and let them pass" rule. Nobody is teleported. `CustomerBrain`/`PatronBrain` lost their own copies of all this and only choose destinations now.
  - Seated NPCs: the NavMeshAgent is switched off while seated (the chair is inside the Not Walkable footprint; the seat stays claimed; `NpcSeating.RequestStand(callback)` re-enables and warps the agent onto the stand point before the walk). Documented in the `NpcSeating.cs` header.
  - Arrivals and departures use different sides of the door (`CafeArrivals.DoorPoint` / `DepartureTarget`, 0.7 m departure radius).
  - Ace has a non-carving `NavMeshObstacle` (capsule r 0.3, h 2); the mesh is never carved around him.
- **Before → after, same four recordings, same stress timeline:** stuck episodes 37 → 4; person-seconds stuck 500 → 11; worst stall 44.7 → 4.3 s; spins 11 → 2; Idle/Walk flicker 18 → 0; walking on the spot 65 → 0 s; Ace shoved in the doorway 4.0 → 0.2 m; the door standoff (three patrons 76 s) is gone — seven leavers passed Ace in the doorway in under 10 s; the five hotspots are empty. Maps: `Logs/CafeLife/analysis/hotspots_before_after.png`; clips: `Logs/CafeLife/clips/after-0*.mp4`; numbers: `Logs/CafeLife/analysis/compare_before_after.txt`.
- **Regression checks:** all 25 edit-mode checks pass, live sit check 14/14, controller check 26/26, three lab days to closing with the recap, no console errors. The `Waiting space reservations` check had been failing since the spot registry moved to `OnEnable` (23–25 Sept); it now registers its synthetic spots the way runtime does.
- **What remains** (from the report): waiting at the door still reads as fidgeting (a 4 s wait with a turn — the worst stall left); a crowd squeezing past Ace still nudges him (0.2 m normally; a physics matter, NPC capsules vs. the CharacterController); an extreme crossing flow through the single door (16 in + 15 out in ten seconds, `Logs/CafeLife/2026-09-26_121840`) jams for ~10 s with overlapping bodies — the shape of the next door problem, not part of the comparison scenarios.
- **Recordings kept:** `Logs/CafeLife/2026-09-26_115533` (R1 Game view), `_115923` (R2 stress, whole café), `_120935` (R3 counter), `_121151` (R4 tables and door); earlier after-runs `_113012/_113436/_114041/_114245` and `_120505`; the crossing-flow run `_121840` (trace only). All git-ignored.
- **Git:** `npc-polish` has the pass in clearly named commits (code + scene/prefabs/NavMesh; analysis tools; check + lab-log fixes; this handoff). Not merged, not pushed. `DayLogs/`, `Claude outputs/` and `Assets/_Recovery` are left uncommitted on purpose.
- **Menu gotcha for the next session:** `GitPlanRunner` consumes `Logs/Git/plan.json` and renames it; write each plan under a fresh staged file name — a re-sent file with an old name can carry the old content.

## September 26 (later) — café NPC pass: observation only, nothing changed yet

**Read this entry first.** Mansoor is about to play Day 2 (Grace returning) to inspect the reputation work before it is committed. Until that is approved, no gameplay code changes. The café NPC pass has started, but only as observation.

- **Nothing in the NPC systems was changed.** `CustomerBrain`, `PatronBrain`, `NpcSeating`, `PersonalSpace`, `CafeArrivals`, `NpcJourney`, the prefabs, the animator and the NavMesh are as they were.
- **New, observation-only tooling (uncommitted, none of it runs unless you use it):**
  - `Assets/Scripts/Diagnostics/CafeLab.cs` — the "Café lab": a throwaway test save (`playtest-cafe-lab.json`, day logs under `DayLogs/CafeLab`), an autopilot that serves customers, and stress buttons (send N in at once, all leave, block the aisle/door, move Ace). Started only from `Fixit Fidget > Café life > Lab`; a normal Play never enters it.
  - `Assets/Scripts/Diagnostics/CafeLifeProbe.cs` + `Assets/Editor/CafeLifeRecorder.cs` — record a 20 Hz trace of every NPC (`trace.csv`) and an MP4 (Game view or one of four fixed café cameras) to `Logs/CafeLife/<stamp>/`.
  - `Assets/Editor/CafeMapExport.cs` — writes the NavMesh, seats, slots and furniture footprints to `Logs/CafeLife/map.json` for analysis.
  - Two one-line hooks make the lab save path work: `SaveManager.PathToFile` and `DayLog.LogDirectory` check `CafeLab.Active` first (false outside a lab session).
  - `Tools/CafeLifeAnalysis/` — Python scripts that turn a trace into the stuck/spin/flicker/contact tables and the hotspot map (needs python3 with pandas, numpy, matplotlib, pillow, and ffmpeg).
- **Baseline footage kept:** `Logs/CafeLife/2026-09-26_020900` (normal), `_022112` (stress, whole café), `_024632` (counter camera), `_024840` (tables and door). Clips of the five key incidents in `Logs/CafeLife/clips/`, the hotspot map and contact sheets in `Logs/CafeLife/analysis/`. All four sessions used the lab save; **the playtest save is still the Day 2 start of 25 Sept 23:52.**
- **Findings and the proposal** are in the project: `claude/npc-behavior-map.md` (how the current architecture works, file by file) and `claude/cafe-npc-observations-2026-09-26.md` (the observation report with timestamps, the root causes of the spinning, and the smallest changes proposed). Short version: the spinning is RVO deflecting walkers around *parked* agents (seated NPCs leave their invisible agent on the aisle corner, priority 0), the body turning to face whatever the avoidance did, watchdogs that cannot see an orbit, and everyone converging on the one exit point through a 0.96 m NavMesh doorway (baked at radius 0.5 for 0.35 m agents).
- **Customer-memory check (`CustomerMemoryChecks.cs:199`, fails at line 216):** the check expects the *response* face to win after "Accepted" even at low patience; `CustomerIdentity.ExpressionAt` (used by the ticket rail and the conversation panel) shows Impatient at ≤ 25 % patience for Intake, Accepted and OrderedDrink. Facts and options are in the chat report; Mansoor decides which behaviour fits before the NPC work starts.
- **Git:** still nothing committed on `main`. The reputation work is one commit-to-be; the diagnostics are a second. `Logs/` is git-ignored.

## September 26 — the café's reputation (day side) built; pink-scene recovery; working on main

**Read this entry first.** Everything below is saved in the project and **not committed**: git is still at the "minor" commit (`9426285`) on `main`.

- **Recovery (early 26 Sept).**
  - Switching GitHub Desktop to the old `main` (5 Sept) made the café pink. That branch's `.gitignore` predates the Synty purchase, so GitHub Desktop moved the Synty, CityNeighbors and lighting-tutorial folders into a stash (and `_Recovery`, `Claude outputs` and `DayLogs` from the baseline branch into another). Stashing removes the files from disk.
  - Everything was copied back into the project folder with `Fixit Fidget > Recovery > Restore files swept into GitHub Desktop stashes (26 Sept)` (`Assets/Editor/StashRecovery.cs`). Nothing was staged, committed or deleted.
  - The stashes are kept as a backup: `a33d42a` (made on main), `8c92557` (baseline) and `d226896` (pensive-pasteur). Don't press "Restore" on them in GitHub Desktop.
  - **From now on, work on `main` only.** `main`, `codex/grace-showcase-baseline` and `origin/main` are the same commit. Don't check out the old `codex/*` branches: their `.gitignore` lacks the Synty rules, and the same thing would happen again.
- **Reputation, day side (built 26 Sept; spec: `claude/reputation-spec.md` §12).** Mansoor asked to keep building rather than wait for the next playtest.
  - **In the recap:** a new block in the middle column shows five stars (gold = earned, outline = not yet), the café's name for its stars ("Just opened" to "Best in the city"), "Next star X / Y" with a bar, today's reviews (+/−) with the five counts, and up to three quotes (best, worst, a regular's). "New star!" appears on the day one is earned.
  - **Rules:** Loved it +2, Liked it +1, Fine 0, Let down −1, Never again −2. Declines, out of stock and a full shelf leave no review, and patrons never review. Stars come at 5, 30, 120, 300 and 580 and are never taken away. Reputation never goes below 0. The day is counted once, at closing; resuming a recap never counts it again.
  - **Save version 5.** Old saves load with reputation 0.
  - **Code:** new `ReputationRules`, `ReputationLedger` and `ReviewLines`, hooked into `CustomerBrain.Depart`, `DayClock.EndDay`, `SaveManager`, `SaveData`, `RecapUI` and `DayLog` (a `review` column and a REVIEWS block). `PlaytestCheckpointTools` now shows each checkpoint's reputation.
  - **Review lines are draft placeholders** in `Assets/Data/Reputation/ReviewLines.asset` (tokens `{name}`, `{thing}`, `{drink}`), waiting for the content pass.
  - **Tools:** `Fixit Fidget > Reputation > Add stars and reviews to the recap (open scene)` (already run on the café scene; running it again only re-wires), `Fixit Fidget > Reputation > Preview a busy day in the recap (Play mode, recap open)` (shows the longest lines; saves nothing), `Fixit Fidget > Checks > Reputation rules`, and the console test `Tests/ReputationRules`.
  - **Placeholder art:** `Assets/Art/UI/ReputationStar.png` and `ReputationStarEmpty.png`, drawn by the setup tool.
  - **Not built yet:** scandals and the franchise offer (night side), star objects in the café, faces for the verdicts, and lines of their own for regulars.
- **Verified 26 Sept.**
  - 0 compile errors. Reputation rules PASS (19,685 assertions), Recap save PASS, Recap input PASS. The console tests pass.
  - An unattended Day 2 was played to closing on the playtest save: 4 queue walkouts gave 4 "Let down" reviews and −4 for the day, with reputation staying at 0. The v5 checkpoint held the reviews and the quote, and the resumed recap showed the same.
  - **Afterwards the playtest save was stepped back.** The current checkpoint is byte-identical to the Day 2 start of 25 Sept 23:52. The "previous" checkpoint is now that same Day 2 start (it was the Day 1 recap; a copy is in `Logs/SaveInspection/2026-09-26_010909/previous.json`). The test day's checkpoint is kept beside the save as `playtest-aces-cafe.replaced-…json`. `DayLogs/AcesCafeLayout/Day02_*` were put back byte for byte.
- **Fixed on the way:** resuming a closed day showed "New Text" in the middle of the recap. The focus-name label's fade used game time, which the recap pauses. `FocusNameUI` now fades in real time.
- **Found, not fixed:**
  - `Fixit Fidget > Checks > Customer memory and identity` fails at `CustomerMemoryChecks.cs:218` ("The response takes precedence over low-patience intake."). `CustomerIdentity.ExpressionAt` shows Impatient for the Intake, Accepted and OrderedDrink beats at 25 % patience or less. This is older than today's work, and neither file was touched. Decide which of the two is right.
  - The upgrade list grows downward past its own frame. Six upgrades already reach the bottom of a 1080p screen, so a seventh will run off it. It needs a scroll view or two columns before more upgrades are added.
- **Git:** all of the above is uncommitted on `main`. New files are the reputation scripts (with .meta files), `Assets/Art/UI/`, `Assets/Data/Reputation/`, `Tests/ReputationRules/` and `Assets/Editor/StashRecovery.cs` (safe to delete once everything is confirmed back). None of it is purchased content.

## September 24–25 — furnishing pass, the café car park with real arrivals, walkers fixed

**Read this entry first.** All of it is saved in `AcesCafeLayoutPlaytest.unity` and committed to git on 25 Sept (a local commit on the current branch, not pushed). Project docs: `claude/cafe-furnishing-pass.md`, `claude/cafe-car-park-and-arrivals.md`, `claude/ace-after-dark.md`.

- **Furnishing pass (24 Sept).** The café now reads as a real, lived-in café that is still a repair shop:
  - window lounge, reading nook, window bar, entrance nook, counter cubbies, work mats, parts cabinet, signs and menu;
  - nothing gameplay-critical moved, and occupied circulation was re-proven.
  - Tool: `Fixit Fidget > Cafe furnishing`. Details in the project doc.
- **Car park and arrivals (24 Sept).** Visitors no longer appear at the door. They drive into the café's own car park across the street (three stalls, one-way aisle, dropped kerbs, a zebra to the door), or walk out of the Saffron house or the first courtyard shop and use the junction crossings. Inside the café nothing changed, because the brain starts at the door. Tool: `Fixit Fidget > Cafe parking lot`.
- **Walker fix (25 Sept), after Mansoor saw NPCs "line up because of the cars" and collide.** Found with a new recorder (`Play - record arrivals for 2 minutes`, writing to `Logs/ArrivalsTrace/`):
  - **Line-ups.** A car waiting at its red light touched the signalled crossing, so walkers stood through their own green. Now only a car on the walker's own line, or one that can't stop, holds them. A waiting pedestrian also shortens the conflicting green (like a button; minimum 7 s).
  - **Collisions.** The 12 s "unstuck" jump landed on people, and nobody could step aside on a crossing. `NpcJourney` now picks each frame's velocity by trying 40 options against everyone (people, café NPCs, the player, cars as boxes). Walkers keep their own lines, gather at kerbs in groups, wait at doorways for people coming out, and land beside anyone on the door spot.
  - **Walking room.** `3 - Measure walking room` bakes, per walk segment, how far walkers may stray without touching anything. Combined meshes are now split into their real pieces, and `2 - Check` now reports all paths clear (the old Saffron-house PROBLEM was a box round a whole combined mesh).
  - **Results** (2-minute rush recordings): overlaps 16 → 0, walks helped on 2 → 0, time held still 57 s → 0, longest kerb wait 31 s → 15 s.
  - **Files:** `NpcJourney`, `StreetCrossing`, `StreetLife` (new field `walkRequestMinGreen` = 7), `CafeArrivals` (room per segment, `Today` visit records), `CafeCar` (`BodyCentre`), `PersonalSpace`, `CafeParkingLot`, new `CafeArrivalsRecorder`.
  - **Scene diff** (checked against the 22:22 save): only the new room arrays, `walkRequestMinGreen`, and 12 stale `approachPoint: {fileID: 0}` lines that `TableSeat` no longer has.
- **Night mode (design only, no code).** The pitch, the pushbacks and Mansoor's decisions are in `claude/ace-after-dark.md`. Decided 25 Sept:
  - there is a night every night (the calm phase after a chaotic day);
  - theft gives trophies only, and the next morning Ace has to keep a straight face (a fast meter: stop it on the green with Space or pad X);
  - getting caught ends the night in jail, with bail the next morning and a reputation hit (5 stars are needed to franchise);
  - regulars and enterable places grow with each area.
  The only related code is `CafeArrivals.Today` (who came from which door or car), a playtest readout for now.
- **Playtest save.** The recordings played Days 13–15 on the playtest save.
- **Settled 25 Sept:**
  - the six comment-only `*-1.cs` stubs are deleted (through Unity, with their .meta files);
  - Mansoor's 22:22 edits were intended: lamp posts stood in the road, and cars drove through them.
- **Reputation spec (25 Sept, written only):** `claude/reputation-spec.md` in the project. Decided:
  - stars are earned on a points ladder from customer reviews (+2 to −2 per review) and stay earned; declines, out of stock and a full shelf leave no review;
  - reviews show as faces/verdicts, so "stars" only ever means the café;
  - getting caught starts a scandal: one star off for 5 days (7, then 10 in the same district), franchise on hold, −20 reputation; a catch is saved at once;
  - stars never change how busy a day is;
  - five stars plus a franchise fee ends the San Francisco chapter, then free play continues;
  - build the day side (reviews + stars) right after M1 closes, and the scandal with "getting caught". Thresholds get rescaled from 7-minute playtests.
- **Still open:** the rest of section 9 of `claude/ace-after-dark.md` (how kind night Ace can be; whether Night 1 is in the public demo).

## September 23 (evening) — POLYGON City pass: a real city, POLYGON cars and people, café props

**Read this entry first.** The owner bought POLYGON City (Synty) and asked to use it:
- give the NPCs randomised looks from the pack;
- make the neighbourhood feel like a city;
- replace the cars, and the buildings where it helps;
- add props inside the café so it feels alive.

Claude also finished the work Codex (GPT Astra) had started: it had extracted the pack but applied nothing. Fable's second-pass items (left café windows, the door-pull gap, cups and food, different trees) were not found in the project, either as files or as scene changes.

**Now in the game.**
- **NPC looks.**
  - Walk-ins and patrons wear 17 POLYGON civilians (8 women, 9 men). A fifth of anonymous walk-ins keep a CC0 look, and named regulars (Grace) keep their own bodies.
  - The 6 street neighbours and 14 new street walkers have fixed looks. The two police officers only walk the beat; they never queue.
  - `PolygonNpcVisual` is a bone follower. The Quaternius rig cannot be Humanoid, so the POLYGON body copies each bone's turn from its T-pose, sized so the hips match. The actor's Animator, navigation, patience bar and bubbles are untouched.
- **City** (`15 - POLYGON City streets`), 118 buildings:
  - Brick mixed-use rows on the empty halves of the eight blocks: shops below, flats above, rooftop plant, signs, water towers, a billboard and a donut shop.
  - POLYGON terraces on the hill. The grey placeholder homes are hidden, not deleted.
  - Office towers (the glass ones stacked on their lobby and capped with their roof) and a hazy skyline band out to about 140 m.
  - A parking lot, a bus stop and a hot-dog cart.
  - Lamps, scaled street trees, benches, bins, hydrants, mailboxes and manholes, plus yard greenery.
  - The old distant grass is paved as city ground; the hill stays green.
- **Traffic.**
  - All 16 Kenney cars now drive POLYGON bodies, keeping the same routes, lanes, signals and spacing. Each through lane also has one extra POLYGON car, 24 in all.
  - Colours are varied using the pack's alternate atlases. Taxi, police and ambulance keep their livery.
  - `CityCarBody` puts the Kenney body back if the purchased art is missing.
- **Café** (`16 - POLYGON cafe details`, 30 props):
  - tall plants in the corners and along the courtyard windows;
  - a supply shelf, a crate and boxes in the staff corner past the repair bench;
  - a bread board and mugs on the front counter, and plates and a small plant at its right end;
  - syrup bottles and mugs either side of the drinks dispenser;
  - a delivery box on the rear cabinet, a wall clock, and planters either side of the doors.
- **Kept on purpose.** The hand-built ring round the café stays: the bay-window houses and the shop houses. It was built around the café windows, the orbit camera and the sun angles, and it gives Ace's its San Francisco character. Swapping it for stock modules would make the café look like a Synty demo scene.

**Guards.**
- **Buildings.** Each is capped by CafeDaylight's own sun path, so no new shadow falls in the café from 9:30 to 17:00. It is also capped by the orbit camera's lowest height at that distance (0.7 r − 1 m).
- **Clearance map.** A 20 cm map of everything 0.12–2.1 m above the street, computed from mesh triangles and taken before and after the pass. Rendering would miss walls and poles, which a top-down camera only sees edge-on. No new obstacle is within 0.35 m of any walking route or 1.1 m of any car lane, and nothing new is on the café's block.
- **Added props.** None has a collider, light or script, and the NavMesh ignores them.
- **Café prop distances.** None stands within:
  - 0.8 m of a waiting spot;
  - 0.7 m of a stand point;
  - 0.35 m of a cup spot;
  - 0.55 m of an item slot, drop spot, bench rig or the beverage station;
  - 1.2 m of the counter queue.
- **Café prop placement.** Floor props stay out of open NavMesh walking space. Heights come from the real geometry: counter tops, the 17 mm staff floor, and a plate for a loaf.
- **Final City 3: PASS.**
  - 20 walking routes and 24 car routes are clear.
  - All 118 buildings pass the shadow and camera rules.
  - 16 seats, 20 waiting spots, 16 signal heads and 48 lenses.
  - Café layout 26/26; occupied circulation 23/23.
  - Gameplay preservation: 179 records unchanged.
- **Play check** (about 60 s, Day 3). POLYGON customers and patrons entered and paid, and POLYGON cars and walkers moved. There were 0 errors. The only warning was a Synty shelf mesh with an extra material, fixed and re-dressed afterwards. Play was stopped mid-day, before the end-of-day save.

**How it runs.** `Fixit Fidget > City pack`:
- NPC looks: 1 builds the look prefabs, 2 takes a line-up photo, Close-up takes a walk-cycle photo, 3 applies the looks to Customer and Patron, and Undo removes them.
- City: 1 records the baseline and before photos, 2 builds, 3 checks and photographs; there are also Undo and Photograph.
- Cafe: 0 surveys (plan, anchors, NavMesh), 1 dresses; there are also Undo and Photograph.
- Catalog measures and photographs the pack.

Evidence is in `Logs/CityPack/`, which git ignores.

**Licensing.** The repository is public:
- The Synty files and the look prefabs built from them are ignored: `/Assets/Synty/`, `/Assets/Art/CityNeighbors/` and the lighting sample folders.
- The scene and prefabs refer to them by GUID only.
- A fresh clone shows the original bodies and cars through the fallbacks. It also shows missing city prefabs until the pack is imported and NPC looks 1 is run again.

**Lighting pack.** Not applied: it is a Viking village tutorial. As City-Asset-Setup.md says, only its two skyboxes and lightmap parameter assets are kept, as references. CafeDaylight still runs the day.

**Files.**
- New:
  - `Assets/Scripts/PolygonNpcVisual.cs` and `CityCarBody.cs`;
  - `Assets/Editor/CityPackCatalog.cs`, `PolygonNpcSetup.cs`, `CityPackChecks.cs`, `PolygonCityDressing.cs` and `CafeLivelyProps.cs`;
  - `Assets/Art/CityNeighbors/Prefabs/*` (19 looks, ignored).
- Changed: `Assets/AssetsPrefabs/Customer.prefab` and `Patron.prefab` (PolygonNpcVisual added; the check found no other component changed), and `Assets/Playtests/AcesCafeLayoutPlaytest.unity`.
- `MovementCornerProbe` stays removed; the gameplay guard rejects any probe component.

**Saved: yes. Committed: no. Pushed: no.**

**Next.**
1. The owner plays a full day and judges the looks, the traffic density and the café props.
2. Owner decision: keep the hand-built ring round the café (recommended), or swap it for POLYGON modules.
3. Commit in checkpoints. Code, prefabs and scene are fine to push; the Synty folders are ignored.
4. Candidates:
   - leave used cups and plates on tables after patrons go, a gameplay way to make the café feel alive;
   - greenery on the paved plazas;
   - the hill's east tower, skipped because its 16:30 shadow reached the café.

## September 23 (afternoon) — GPT Astra's neighborhood refresh finished, applied and verified

**Read this entry first.** On September 22–23 the owner asked GPT Astra (the Codex desktop app) for free CC0 assets that fit the game: trees, shrubs, bushes and flowers, "actual good chairs", better door handles, a nicer stoplight, and more NPC looks than the beach guy, without breaking patience bars. It imported the art and wrote the tools, then stopped before applying anything (last edit 11:44). Its final edit also broke the Editor assembly (`EditorUtility.InstanceIDToObject` no longer compiles in Unity 6.5), so none of its tools could run. The owner asked Claude to finish the work; this entry records that.

**Now in the game.** Walk-in customers and patrons use five CC0 Quaternius bodies: Casual, Hoodie, Farmer, Suit and Worker. Named regulars (Grace) keep the Beach body, so for now the Beach body means "a regular". Walk-ins pick a look from a stable hash of their name; patrons cycle. The six street neighbors have fixed looks. In the café scene:
- The 16 table chairs and 2 patio chairs are Kenney rounded timber chairs.
- The three courtyard and rear-lane trees are faceted Kenney trees, and the four benches are Kenney benches.
- The four existing planter beds and the three tree planters have flowers and bushes.
- The entrance doors have rounded brass pulls.
- All 16 traffic signals have hooded ochre/charcoal housings on slim posts. Their lenses are the same renderers StreetLife already drives.

Old visuals are hidden, not deleted. Every old collider, seat anchor and route is untouched. The new visuals have no colliders and are excluded from NavMesh builds.

**How it runs.** `Fixit Fidget > Neighborhood refresh` (`Assets/Editor/NeighborhoodRefreshSteps.cs`) runs in four steps:
1. Record gameplay baseline and before photos.
2. Placeholder neighbors.
3. Apply scenery.
4. Verify.

The same menu can also retake photos, put the original chairs back, or discard unsaved scene changes. Evidence is in `Logs/NeighborhoodRefresh/2026-09-23_120730/`: before-, now- and after- photos, `npc-lineup.png` and `gameplay-baseline.json`.

**Defects found in GPT Astra's tools and fixed before applying.**
1. The compile error: the baseline now records serialized values with stable GlobalObjectIds.
2. The rig check compared live bone poses. Beach.fbx is imported with its animations, so its hips sit 7.8 mm below rest, and the check failed. Read straight from the FBX files, all 80 bones and every bind matrix of Beach and Casual_2 are identical. The check now compares mesh bind poses.
3. The skinning-bounds check baked meshes without their 100× renderer scale, so every box was inflated 100× and millimetre differences failed. It now uses `BakeMesh(useScale: true)`.
4. The gameplay guard required stand points on loiter spots. `WaitingSpot.StandPoint` deliberately falls back to the spot itself, so only seats require one now.
5. The courtyard cut removed the planter soil and box tops, leaving hollow planters. It now removes only the old canopy spheres and trunks. Per-mesh counts are logged: foliage 3×2,304, trunks 3×80, bench slats 4×108, bench iron 4×96.
6. Smaller fixes:
   - a preflight check, so nothing changes if a target is missing;
   - one-step undo;
   - mesh assets rewritten in place on re-runs, and named to match their files;
   - script edits to prefab instances recorded as overrides; the saved scene holds all 88 CC0 instances with their material and position overrides.

**Verified.** Step 2 PASS: every look has complete meshes, materials and bones; Customer (12 components) and Patron (10) have gameplay, UI and animation components byte-identical; baked skinning passed in idle and walk poses. The line-up photo shows all six bodies correctly skinned. Step 4 PASS:
- 16 seats at 0.6 drain, 20 active waiting spots, original save and log paths, no probes or missing scripts, walking settings intact, 16 signal heads and 48 lenses;
- café layout 26/26 paths; occupied circulation 23/23;
- gameplay preservation: all 180 settings and anchor records unchanged from the pre-refresh baseline, including Customer and Patron brain, identity and interactable.

A 90-second Play check showed new-look customers and patrons entering, queuing and paying (+$5 patron income), with 0 errors and 0 warnings. It was stopped before closing: the save is still Day 2 start, $139, and the day logs are untouched. `TableSeat.snapToSeat` is still off (no sit animation), so seated NPCs stand beside the chair as before; chair height does not affect posing yet.

**Owner decisions.**
1. **Chairs.** The Kenney chairs are chunkier and a single flat colour, where the authored ladder-back chairs follow the art-style guide's dark-frame / light-seat grain. GDD 14.1's "confident colour blocking" allows them, and the owner asked for new chairs, so they are in. If they are not wanted, use `Neighborhood refresh > Put the original chairs back` (one undo step), then save.
2. **Regulars.** All regulars still share the Beach body. GDD 14.2 wants each regular to have a distinct silhouette, which belongs with the owner's character work.

**Files.**
- New: `Assets/Scripts/NpcVisualVariants.cs`; `Assets/Editor/NpcVisualVariantSetup.cs`, `NeighborhoodVisualRefresh.cs`, `NeighborhoodRefreshChecks.cs`, `NeighborhoodRefreshSteps.cs`; `Assets/Art/CC0Neighborhood/**` (Kenney models, licences and manifest, `Adapted/SignalHousing`, `Authored/EntranceRefresh.fbx`, generated `Materials/` and `Meshes/`); `Assets/Art/PlaceholderNeighbors/**` (five FBX, CC0 text, manifest, README, generated `Materials/`).
- Changed: `Assets/AssetsPrefabs/Customer.prefab`, `Patron.prefab`, `Assets/Playtests/AcesCafeLayoutPlaytest.unity`.

**Saved: yes. Committed: no. Pushed: no.** Unity was left stopped in `AcesCafeLayoutPlaytest` with the scene clean.

**Next, in order.**
1. The owner plays a day and judges the look: chairs, benches, NPC bodies.
2. Commit in focused checkpoints: movement fixes; Grace camera and checks; neighborhood refresh and NPC looks.
3. Continue the morning's list below: feel test, a fresh Day 1→2 Grace Perfect run with the Restock click, and the portrait policy.

## September 23 — walking stutter and floating player fixed; Grace's camera can reach Perfect

**Read this entry first.** The owner asked to fix the first-person stutter and a new "player capsule floating above the café", then continue the roadmap with GDD v4.1 as the source of truth, and authorised working directly in Unity without a discussion round.

**Floating player: cause and fix.** A throwaway QA script from the previous night (`Assets/MovementCornerProbe.cs`) had been saved onto the Player in `AcesCafeLayoutPlaytest`. On every Play it disabled PlayerInput, teleported the player to (1000, 1, 1000) onto a temporary floor, forced first person, walked a scripted square, then threw every frame (its Temp output folder no longer existed). The same session had also left the scene's save name as `playtest-movement-qa-20260922.json` and its day logs in `Temp/MovementQA/DayLogs`. The component is removed from the scene and `playtest-aces-cafe.json` / `DayLogs/AcesCafeLayout` are restored. The script file is attached to nothing; the owner should delete `Assets/MovementCornerProbe.cs` (the assistant does not delete files).

**First-person stutter: two measured causes.** (1) Instant movement: at a corner the player releases one key a few milliseconds before pressing the next, and that zero input was a dead stop followed by a full-speed jump; every reversal was a hard jolt. (2) CharacterController Min Move Distance 0.001: at the editor's ~240 fps, SimpleMove's per-frame gravity step (~0.15 mm) was below 1 mm and ignored, so the capsule lost grounding every few frames and swallowed the first ~20 ms of movement when walking resumed. Fix: `PlayerMovement` eases first-person input in camera space (acceleration 50 m/s², braking 50 m/s², serialized on the Player). The camera stays rigidly attached and mouse turning stays instant; isometric keeps its immediate controls. It also enforces `minMoveDistance = 0` in Awake (the café scene value is 0 too). The Player's redundant CapsuleCollider was A/B tested and ruled out; it is left in place.

**Stutter evidence.** New `Fixit Fidget > Checks > Walking feel (Play Mode) > Corner test`: a scripted square with 17/33/50 ms key gaps, a 50 ms overlap and reversals, on an empty temporary floor; the player is restored afterwards and results go to `Logs/WalkingFeel/`. Before: 8 frozen frames, 124 ms below 1 m/s at corners, slowest corner 0 m/s, largest one-frame velocity change 2,487 m/s². After: PASS, 0 frozen frames, 0 ms stalled, slowest corner 2.24 m/s, largest change 61 m/s², camera error 0 mm. A real-keyboard recording in the café (`Record my walking for 10 seconds`) showed 0 ungrounded frames and 16–26 ms key gaps bridged smoothly; its only jolts were real collisions in the narrow staff aisle behind the counter. Owner feel acceptance is still needed.

**Grace's camera capped at Good: cause and fix.** The Lens glass is a Cylinder primitive squashed to 3 mm, so its CapsuleCollider becomes a 57 mm sphere that encloses the Lens grime. The bench brush scrubs only the first collider on the view ray, so the lens spot could not be cleaned: 3 of 4 tasks, Good at best. The old checker deleted grime directly, which hid this. Fix: the Lens glass collider is removed from `GraceReunionCamera.prefab` and from `GraceShowcaseSetup.BuildCamera`. `Checks > Grace camera tweezers interaction` now brushes every spot through view rays (straight down plus 30° tilts, re-checked while each spot shrinks). It FAILED before the prefab fix ("'Lens grime' can be brushed from above at 100% dirty, but the view ray first hits 'Lens glass'") and PASSES after. New `Checks > Every device's grime can be brushed`: PASS, 9 spots across PhoneRepair, PocketWatch and GraceReunionCamera. `Grace camera content` still passes. A live Day 1 brush-to-Perfect has not been replayed by the owner.

**Wide Brush upgrade.** `UpgradeManager.ScrubSpeedMultiplier` had no consumer (flagged in the September 17 audit), so buying Wide Brush changed nothing. `ItemInspector` now scales each scrub stroke by it; without the upgrade the multiplier is 1, so default behaviour is unchanged.

**Save and log incident (restored).** The first corner test was left in Play long enough for Day 2 to close unattended. That checkpointed the café save as Day 2 closed (Grace stormed out, relationship 0) and overwrote `DayLogs/AcesCafeLayout/Day02_*`. The checkpoint was restored from its automatic `.bak` with the new `Fixit Fidget > Playtest > Café playtest save > Step back to previous checkpoint`; the replaced file is kept as `playtest-aces-cafe.replaced-2026-09-23_*.json` in the save folder. The Day 2 logs were restored from the last commit. Verified afterwards: Day 2 start, $139, Grace visits 1, relationship 2, camera Good, photo not claimed. Rule for future automated runs: stop Play immediately after a check.

**GDD v4 M0 queue re-audited against source and scene.** Loiter 1.15 ×6 and seats 0.6 ×16 are saved in the café scene; Interact has no Hold interaction; the per-day name bag exists (`CustomerVisitRoster`); drink orders and storm-outs use broadcast bubbles; people/orders metrics and `service_s` naming are in the logs. The remaining M0 items need the owner at the keyboard: the recap Restock click-through and a clean Days 1–5 run.

**Open decision (unchanged).** `CustomerMemoryChecks` still fails one assertion: it expects a Happy portrait after Accepted at ≤25% patience, while `ExpressionAt` deliberately shows Impatient. GDD 8.4 (10–34% → angry/worried) supports the code; B.4 says expression follows patience, choice and outcome. Recommendation: keep Impatient and update the check, but this is the owner's call.

**Files.** New: `Assets/Scripts/WalkingFeelProbe.cs` (editor-only, never saved in a scene), `Assets/Editor/WalkingFeelChecks.cs`, `Assets/Editor/DeviceCleaningReachabilityChecks.cs`, `Assets/Editor/PlaytestCheckpointTools.cs`. Changed: `PlayerMovement.cs`, `ItemInspector.cs`, `GraceRepairInteractionChecks.cs`, `GraceShowcaseSetup.cs`, `GraceShowcase/GraceReunionCamera.prefab`, `Playtests/AcesCafeLayoutPlaytest.unity`. `DayLogs/AcesCafeLayout/Day02_*` restored to committed content. Unity compile: 0 errors. **Saved: yes. Committed: no. Pushed: no. Merged: no.** Unity was left stopped in `AcesCafeLayoutPlaytest`. The editor marks the scene modified after Play (pre-existing serialization churn); the disk version is the intended one.

**Next, in order.** (1) Owner feel test: press V, walk squares and reversals; if it feels floaty or too snappy, tune the two values on Player > Player Movement. (2) Delete `Assets/MovementCornerProbe.cs`. (3) Fresh Day 1 → Day 2 Grace run: brush all three grime spots and replace the shutter for Perfect, then see the Day 2 photo; also click Restock at a recap. (4) Decide the Accepted-at-low-patience portrait policy. (5) Commit in focused checkpoints (movement; Grace camera and checks; tools). (6) Then the roadmap's creative step: the Blender character blockout with the owner modelling, in its own Blender session.

## September 22 — current position and identity audit completed

**Backup completed:** owner explicitly authorized the full checkpoint. **7f9fc8e** preserves the saved scene/prefab/material/log changes and copies of this handoff, current assessment and character guide under **Docs/ProjectDirection**. It was successfully pushed to **codex/grace-showcase-baseline**, also uploading the previously local 3233348 and e334eb2 commits. GitHub's branch hash was verified against local HEAD; main remains **7f2dbe7**. Only Assets/_Recovery and its metadata remain untracked. This is a preservation checkpoint, not gameplay acceptance. This completion note is recorded in a subsequent documentation-only commit.

Before checkpointing, the working branch was 25 commits ahead of main, with no main-only commits; the remote working branch was e39e33f. A verified local safety copy exists at validation/git-safety-2026-09-22 (two-commit bundle, tracked working-copy patch, untracked recovery scene/logs). That copy is on this computer; the pushed branch is the off-device backup of the checkpointed files. Recovery assets remain excluded from GitHub, as approved.

**Working habit:** end each work session with explicit saved / committed / pushed / merged status. Make focused checkpoints part of the agreed task; review pending owner edits before including them. Do not conflate GitHub backup with main-branch acceptance. Retain unresolved issues in checkpoint descriptions.

**Read this entry before older next-step instructions.** Current work was assessment and continuity recovery only. The owner reports feeling lost and losing ownership of the game's identity. The text of **Fix It Fiasco Feedback** was recovered; that conversation's attached portrait/Meshy images were not included in the retrieved content. Do not claim to have inspected them.

The project's identity remains a warm, stylized, character-driven repair café: meaningful possessions, drinks, overlapping customer needs, and remembered consequences. GDD priority is people first, place second, service third. The owner is the final creative authority; provide creative direction within the task without inventing character canon or selecting a new major production milestone automatically.

**Where we are:** substantial playable service prototype; M1 customer showcase remains open. At gameplay inspection, local HEAD was e334eb2 on codex/grace-showcase-baseline; the checkpoint above preserves later saved edits. The camera/photo episode and showcase fixes are implemented, but the complete normal-input repair/return/reload journey is not newly verified. Visual Style Lock 01 has a written character-production guide only. No finished character style lock, expressive showcase, or public-ready slice is certified. The older D:/FixitFidget/roadmap-progress.md includes superseded claims that no camera asset exists; use dated current evidence instead.

**Creative continuity:** retain the September 20 portrait-led direction below. The September 19 invented Grace appearance and its board are superseded, not approved canon. The owner wants to build the first character himself in Blender with coaching. Next proposed creative action is an actual reference comparison and beginner character blockout session after the references/guide are settled; no asset creation is authorized by this assessment. Representative bench, cup, furniture and foliage follow that character language in bounded later tasks. Do not restart the room or expand the neighborhood.

**Open owner playtest reports, deliberately deferred:**
- Grace's physical camera repair still yields only Good. September 21 Day01 log corroborates Good. The current four-task design is three grime spots plus shutter; a Good result is consistent with one remaining grime spot, but this is not proof of user error or a diagnosed root cause. The existing checker destroys grime directly, so its Perfect result does not prove normal brushing works. Keep open until a real tool-input reproduction explains every task and result. Do not change grading to conceal the issue.
- First-person stutter during up → left → down → right → up and repeated back-and-forth movement. User wants smoother gameplay later. No reproduction/profile was performed. Later distinguish direction-change feel, collisions/camera follow, and frame-time hitches before choosing smoothing. Prior station sensitivity tests do not close this separate walking complaint.

**Inspection evidence:** reviewed GDD extract, current/older notes, feedback conversation text, current source and assets, actual first-person/runtime screenshots, September 21 logs, and live editor scene/profile data. On September 22 Unity was stopped/unpaused in AcesCafeLayoutPlaytest, scene clean in memory; one CustomerProfile exists (Grace), with all five portrait slots empty. New editor camera capture: validation/identity-audit-2026-09-22-editor.png. This is a stopped-scene render, not a gameplay/overlay capture. No new playthrough, listening test, build, performance measurement or test suite run.

**Changes this assessment:** documentation in this handoff and Feedback-and-Roadmap only, plus the audit screenshot. No game code, assets, scenes, save files, branch history or commits changed. Existing modified scenes/prefab/logs and deleted duplicate materials are owner work and remain untouched. Detailed findings and the compact production map are at the top of Feedback-and-Roadmap-2026-09-12.md.

## September 20 — Visual Style Lock 01 replaces previous Grace concept

Controlling user direction: DO NOT implement the earlier Grace Counter Corner concept or redesign Grace. Girlfriend's original illustration is authoritative; a Meshy experiment is useful only as a visual 2D-to-3D translation reference, never as production mesh. Desired language: oversized stylized head/features, long body proportions, chunky shoes, readable hands, clear clothing/hair masses and clean soft PBR. New references have not yet been supplied/inspected. Do not invent an exact head ratio or likeness from the superseded board.

Owner will personally model the first character to learn Blender. Assistant coaches proportions/topology/modifiers/materials/rigging/export and diagnoses problems; do not automatically model or replace it. Approximate finished hero target20k–35k triangles, lower where sufficient, not a rigid quota or measured performance guarantee. Eventual style set: portrait-derived character, technical repair bench, cup family, one furniture object, one foliage asset; none is authorized for creation yet.

Completed only Visual-Style-Lock-01-Character-Guide.md, a concise production guide with construction, topology, selective subdivision, example25k allocation, materials/UVs, rigging and later Unity export. No Unity/Blender execution, game assets or models. STOP for guide approval before modeling or Unity implementation. Earlier September19 proposal remains historical and explicitly superseded.

## September 19 latest direction — proceed to creative proposal without owner playtest

Owner explicitly declined playtesting and asked to continue the next work. Do not ask him to run the two-day acceptance route as a gate to creative preproduction. Remaining normal-input/save/photo uncertainty stays documented; Phase 1 has not magically become fully verified. No further broad bug audit or technical expansion.

**Completed deliverable:** Grace-Counter-Corner-Style-Lock.md and one concept board at validation/grace-counter-style-proposal-01.png. The brief covers the actual existing station arrangement, hierarchy, all requested material/environment treatments, neutral Grace character/portrait sheet specification, current five portrait slots, interaction/audio/UI direction, purposeful clutter and bounded production order. Appearance (mature Black Grace, silver low bun, plum cardigan, ivory neckline, charcoal trousers, one ochre pin) is explicitly a new proposal, not established canon. Actual callback remains the family-reunion photo with Grace in it; Ace + Grace is not silently substituted. Neutral must be approved before all five portraits.

The board is generated concept art based on actual current screenshots plus recovered approved Layout Study 02. It is not a Unity result. Final game forms should be simpler than the illustration's fabric/face detail; actual drink names/geometry/interaction anchors take precedence. Full prompt and references are in the brief. No Unity assets, scene, gameplay, Git state or save files changed this turn. Unity was read-only/stopped and retains the owner's pre-existing dirty scene. The prior local hint checkpoint remains e334eb2.

**Next boundary:** present the concrete proposal for Mansoor's creative review, per his explicit requirement before substantial visual implementation. After approval, first build Grace's neutral portrait/silhouette and simple 3D study with the portrait artist; then camera/strap and one local material/light treatment. Do not broaden to cafe/neighborhood redesign or hands. Gameplay acceptance remains our bounded responsibility, not homework required from the owner.

## September 19 continuation — showcase hints restored; acceptance still open

Latest user asked to restore missing Day 1/Day 2 hints and continue the agreed showcase. Confirmed cause: DayOneGuideUI required the old guided-opening sequence (now disabled for Grace) and hard-gated Day 1. Fixed in local **e334eb2**, six existing files: three runtime scripts, one existing check, two Grace day assets. Optional featured hints now follow the actual featured customer separately from spawning. E pickup wording, two-hand drink delivery guidance, and outstanding latte after camera return are covered. Normal gameplay, save, stock and grading rules unchanged. No new runtime/art files or push.

Unity compilation and focused onboarding/hint checks PASS. Live Day 1 panel was visible. Day 2 was explicitly selected as an isolated runtime fixture; its active return hint was captured at validation/grace-day2-hint.png and visually inspected without clipping. Do not call this a completed save/return journey. Owner's September 19 Day01 log supports Good camera + latte served; older September 17 Day02 logs cannot verify that run's callback.

Final Unity state: stopped/unpaused, AcesCafeLayoutPlaytest active and DIRTY as it was on entry. Owner's unsaved edits preserved; safety scene copy validation/grace-hint-session-scene-backup.unity. Original save name playtest-aces-cafe.json, log folder DayLogs/AcesCafeLayout and startingDay=1 restored. Do not save/reopen the scene blindly. Owner's unrelated scene/prefab/material/log changes remain uncommitted and untouched. No broad cleanup or art pass. Remaining gate: normal two-day Grace acceptance, especially Perfect brushing, E collection, unused-cup return and recap/reload/photo durability. Then present the Counter Corner Style Lock for approval.

## September 19 — Grace nucleus Phase 1, fixes checkpointed; acceptance still open

Owner explicitly limits work to Grace + intake + camera + one drink + return/photo. No broad art, neighborhood, new systems/content or cleanup. Stop technical expansion after the journey is trustworthy, then present the style proposal before visual implementation.

See the top of Feedback-and-Roadmap-2026-09-12.md for classifications, exact checks and limitations. Focused local commit 3233348 on codex/grace-showcase-baseline follows safety checkpoint 948892a and owner's e39e33f. Fourteen existing files; no new runtime/art assets. E collection from inspection, clear replacement/remaining-Grace-work prompts, closing-cup settlement, and existing two-day schedule adjustments are installed. Shared Grace profile now uses DrinkOnly for ordinary visits; Day 1 explicit reunion-camera override remains. Preserve partial grades and all existing memory/save rules.

Unity compile, Grace blade checker, beverage checker and recap-save checker pass. CustomerMemoryChecks retains one failing low-patience Accepted-expression expectation; it is a presentation-policy mismatch, not demonstrated save corruption. Real live arrival and simulated F station entry were observed. Normal-input full repair/collection/latte/handoff/recap/reload/return/photo was NOT completed because simulated aiming was unreliable; no sign-off on the complete foundation yet. Do not re-run broad audits or start art. Finish only the short acceptance route documented in the report.

Final editor state: stopped, active AcesCafeLayoutPlaytest scene clean, normal save name playtest-aces-cafe.json and log folder DayLogs/AcesCafeLayout restored. Existing saves unchanged. Unrelated owner changes (three material-pair deletions, PhoneRepair and scene edits, Day 4 logs) remain uncommitted and untouched. Do not overwrite or include them in fixes. Test helper code was sent through eval; the active scene contains no test driver. The proposed style lock has been drafted as notes but is not approved or implemented.

## September 16 — V4 textures, through traffic, station feel and reading shelf

This supersedes the earlier V4 circular-road design below. The owner rejected the racetrack silhouette and repeated circling cars, requested surface textures and more intentional cafe detail, and reported erratic station-camera movement. Their chosen bookshelf theme is local art, repair books and neighborhood reads. All changes remain in **Assets/Playtests/AcesCafeLayoutPlaytest.unity**; original room/stations, 3.20 m ceiling, 0.50 m player radius, deferred hands and removed cat are retained.

**Neighborhood:** four connected junctions replace the oval road, with double-yellow two-way lanes, crossings, curbs, drains and traffic signals. Front/rear and left building rows were repositioned to leave actual street openings. Distant streets climb a gradual hill instead of ending at sheer terraced blocks. Cars now use eight open through lanes, with 16 reusable vehicles across eight Kenney model styles. They exit at distant endpoints (approximately 180 m), wait for varied delays, then re-enter beyond the 160 m haze. Roads extend to 220 m. There is no visible circuit around the cafe. Lane spacing prevents overtaking; NS/EW signals alternate 14 seconds green, 2 amber and 3 all-red. This remains bounded ambient traffic, not a general city traffic simulation. Pedestrians stay on sidewalks and birds retain their existing routes.

**Surfaces:** nine new 1K maps add concrete paving, oak grain, roofing, woven normals and planted ground texture. The existing plaster/asphalt maps remain in use with revised scale/contrast. Entry apron, surrounding paving and storefront thresholds share the same concrete. Original flat cafe cubes received metre-based UVs; the counter FBX was found to have no UVs, so scene-specific mesh copies were mapped. Imported source meshes/materials remain reusable. Tangents were corrected after UV changes. Ceiling uses a softer, lighter finish. Source URLs, CC0 records and hashes are in validation/texture-source/sources.json, with human-readable credits in the existing StreetModels/AssetCredits.txt.

**Reading corner:** one authored Blender bookcase beside the window banquette, centred at (-7.16, 0, 10), yaw90. It is 1.60 m wide, 1.65 m tall and 0.38 m deep, with bevelled oak boards, sage inset cabinetry, 24 deliberately grouped books, an art volume and repair stack. The frame now contains an original miniature geometric San Francisco postcard. The FBX has seven material meshes and 14,076 triangles; source/preview are retained at implementation/art/CafeBookcase. One fitted BoxCollider is included in the cafe navigation bake. The existing banquette has subtle fabric normals. Books and postcard are decorative; no reading/borrowing system was added.

**Station feel:** bench/counter Cinemachine mouse readers incorrectly multiplied per-frame mouse distance by frame time, had different gains (7 vs .3), and the bench added .2 s acceleration/deceleration. StationInteractable now configures existing readers for .09 degrees per mouse pixel, cancels that extra frame factor and removes acceleration. Stick input remains a rate at 90 degrees/s. Input is owned only by the active station and blocked during inspection, dialogue, pause, blending, lost focus and released cursor; the first resumed frame is skipped. BeverageLook uses matching guards and cached references. Four existing source files changed in this completion: CafeStreetUpgrade, StreetLife, StationInteractable and BeverageLook. No new runtime scripts.

**Verification completed:** runtime/editor compilation passes. Rebuilt cafe navigation passes 26/26 destination paths and 23/23 static occupied customer approaches. Through-traffic stress runs 8,000 steps (813.45 traffic seconds) with unequal speeds and hitches: zero car overlaps, spacing violations, red departures, stop overshoots, nearby-building intersections or road-envelope violations; all cars repeatedly exit/re-enter; all six signal states and 48 bulbs pass. Nearest reuse was 170.4 m from cafe centre. All 1,080 distant-building triangles also clear the north-road corridors. See validation/v4-through-traffic-result.json.

Camera driver checks at30/60/144 Hz return the same 40.5 degrees per450 mouse pixels and90 degrees per second for stick input. Live bench and counter entry checks both report enabled readers after blending and0.900000 degrees for10 simulated mouse pixels through their actual reader/controller. One attempted counter check addressed the wrong duplicate-named Counter object and invoked a disabled reader; it was corrected by resolving the StationInteractable component. No gameplay-camera exception was found. The orange rectangle visible near the ceiling in some first-person captures was identified as an existing customer's PatienceBar, not an environment artifact. Input tests do not establish hardware feel or an FPS benchmark.

Final visual review completed in daytime isometric, first-person reading corner and7:30 p.m. evening views. Console error count was zero. Unity was stopped after screenshots, restoring normal authored clock, player/camera and traffic settings; scene changes had been saved before Play. Actual review images are D:/FixitFidget/Assets/Screenshots/aces-cafe-v4-finished-day.png, aces-cafe-v4-reading-corner-final.png and aces-cafe-v4-finished-evening.png. No commit or push was made. Taste acceptance and a normal moving-customer playtest remain with the owner. Next roadmap work remains interaction reliability (cup return discoverability, collection directly from finished inspection, Grace's Perfect blocker), measured performance, then M1's two-visit customer showcase. Final character/hand/environment art remains a deliberate Blender phase; this environment pass does not complete M1.

## September 15 — V4 neighborhood consistency and traffic fixes

The owner's V3 playtest found cars overlapping/overtaking and passing through buildings, inconsistent street heights and paving, a disappearing left corner wall, and floating ceiling fixtures in isometric view. V4 addresses this set in the same café playtest scene. The design decision is a level immediate café block with the hill retained in the distant neighborhood, one rounded one-way street loop around the café, matching concrete sidewalks and small planted pockets. The former right courtyard now has a street with sidewalks and visible passing traffic. This is an authored ambient loop, not an open-world traffic simulation.

Four cars share the exact road centreline markers and travel at 4 m/s. StreetLife now supports explicit traffic groups with vehicle length and minimum bumper gaps, simultaneous spacing updates and bounded movement during long frames. Existing birds and walkers keep their independent motion. The road surface is at y=-0.18 m, main sidewalks at -0.02 m, shop threshold infill at -0.03 m and the existing entrance apron at 0 m. The small differences avoid coplanar flicker; the previous high rear road/platform is gone. Shop bases were brought to the local grade. Road width is 5.6 m, with 4 m centreline corner radii, consistent curbs, crossings and one-way markings. The entrance apron now uses world-scaled texture coordinates matching the sidewalk; its physical collider remains unchanged. Planted pockets have defined edging, soil, low grass and sage rather than loose greenery. Left sidewalk pedestrian routes and lamp posts were moved clear of the house stoops.

CafeViewMode now tests finite sightlines into the room before hiding a wall. The left rear plaster is visible in the saved default overhead view. Fifteen explicitly assigned fixture renderers (the four table pendants and three older ceiling lights) hide in isometric, including pause, while all 14 warm lights stay active. Fixtures return in first-person and station views. The ceiling remains at 3.20 m, player radius remains 0.50 m, hands remain deferred, and the removed cat remains absent. The 9 a.m.–8 p.m. day cycle and existing camera controls are retained.

Verification: combined runtime/editor compilation passed; 26/26 navigation destinations and 23/23 static occupied delivery approaches passed. An actual StreetLife run of 4,800 steps with unequal car speeds (2/11/4/7 m/s) and simulated long frames reported zero spacing violations and zero overlaps with building bounds. Closest car centres were 7.02 m in that stress run. A separate 1,200-step full-route geometry check found all car model corners inside the road, with maximum centreline distance 1.276 m versus the available 2.8 m half-width. Normal speeds and phases were restored. Fixture checks passed in isometric, paused isometric, first person and a station; illumination remained enabled and the left corner wall stayed visible. These are focused automated/live-method checks, not a full native-keyboard Day 1–5 test or a performance benchmark.

Only three existing source files changed for V4: CafeStreetUpgrade, StreetLife and CafeViewMode. There are no new runtime scripts. The saved scene and existing street geometry/palette assets hold the authored changes. Pre-V4 scene, geometry and palette backups are in workspace validation. Day/evening previews: D:/FixitFidget/Assets/Screenshots/aces-cafe-v4-day.png and aces-cafe-v4-evening.png. No commit or push was made.

Final readiness: Unity is stopped in the saved and reopened AcesCafeLayoutPlaytest scene, with no unsaved changes and zero Console errors. DayClock and StreetLife are enabled; all four authored car speeds are restored to 4 m/s. Runtime test overrides are cleared. Day and evening screenshots were visually inspected after the final paving correction. Use Fixit Fidget > Ace's Cafe > Open layout playtest, then Play.

Next playtest: watch at least one full car circuit, orbit past the left corner, compare the room in first person and overhead, and check evening brightness. Give taste feedback on the single-level block, sidewalk texture and planted pockets before adding more decoration. After this bounded environment pass, return to the interaction reliability queue (cup return, pickup from completed inspection, Grace's Perfect blocker), measure performance, and complete M1's two-visit customer showcase. M1 remains incomplete.

## September 15 — café V3, two walking views and visible day cycle

Owner-approved scope: keep isometric and add switchable first-person walking; show neighborhood life around the café; retain colorful San Francisco bay-window architecture; close at **8 p.m.** The removed cat stays removed. Hands, Ace's model and authored animation remain a separate Blender session. Preserve the owner's **3.20 m ceiling**, **0.50 m player radius**, room layout and station positions.

V3 is applied in **D:/FixitFidget/Assets/Playtests/AcesCafeLayoutPlaytest.unity**. Existing geometry and palette assets are extended: front shops/cross-street, right courtyard/shopfronts, rear hill neighborhood and distant buildings. Right-side café windows reveal courtyard activity. Added mural, benches, small shop displays, perimeter café details, four table pendants and evening lighting. The front junction now grades into the sloping street with an opening in the curb; overlapping paving was trimmed. Background traffic has two cars, six walking neighbors and two birds; birds follow a smooth loop around the café, clear of the main roof masses. These are decorative routes, not a walkable open world or a new NPC simulation. No replacement cat was added.

**Controls:** V switches walking views; middle-mouse drag orbits overhead; wheel zooms; first-person mouse look uses .09 sensitivity; Esc frees the cursor and click resumes looking. Existing F station controls are retained. View switching is blocked while a station, inspection, dialogue or recap owns input. First-person entry initially faces into the café. Held cups use camera-relative positions and remain steady when paused; there are no new procedural hands. Overhead framing uses rotation (43,25,0), distance34 and focus (0,.6,9). Camera comfort remains owner acceptance work.

The existing 180-second playtest day maps from **9 a.m. to 8 p.m.** A procedural sun moves, shadows change, the sky darkens, windows glow and 14 shadowless warm lights brighten toward evening. The shop stops new arrivals at8 p.m.; accepted work retains the previous completion rules. HUD shows café time and closing time. CafeDaylight shares DayClock progression and uses private runtime sky/glow materials, restoring authored state on disable. The final evening pass increased seating light after the first capture was too dark. Unused "Sample text" speech content was cleared from the existing Patron prefab, preserving future speech behavior.

Verification completed: runtime/editor C# compilation; 26/26 navigation destinations; 23/23 static occupied delivery approaches; all three station entry/exit ownership checks; V in both directions and Esc release through Unity's input system; camera-relative movement across game frames; two-cup visibility and zero position shift on pause; 8 p.m. last-orders transition; 9 a.m. StartDay reset; material restoration. Visible sun was inspected from the entrance apron at5:48 p.m. Native desktop V taps were inconclusive; do not claim a full physical-keyboard or Day1–5 playthrough. A single direct SimpleMove call outside a normal game frame was inconclusive; the subsequent across-frame input test moved0.982m with direction alignment1.0. No performance benchmark or guarantee against moving-crowd congestion is claimed. The original campaign and interaction save hashes, and the ServiceInteractionPlaytest scene hash, match their prior fingerprints.

Eight focused source files comprise this pass: new CafeViewMode/CafeDaylight; existing PlayerMovement, PlayerInteractor, PlayerCarry, DayClock, ShopUI and CafeStreetUpgrade. Scene/material/mesh edits were made through Unity. The single Patron prefab text change is also saved. Reuse this handoff; do not create more parallel status documents. Existing V3 scene backups and a pre-ground-repair geometry backup are under workspace validation. No commit or push was made.

**Next owner playtest:** compare walking views while serving; judge right-side/window visibility and overhead framing; work through sunset and closing to judge room brightness. Record any camera disorientation and actual customer blockage. Then return to the reliability queue: extra-cup return discoverability, collecting a completed repair directly from inspection, and Grace's Good/Perfect blocker. Measure performance before adding more ambient density. M1's two-visit customer showcase remains incomplete; V3 does not replace that roadmap gate.

## September 14 — café circulation and San Francisco street V2 finished

Latest owner request implemented in the existing **AcesCafeLayoutPlaytest** scene: more colorful bay-window buildings, detailed street texture/architecture, coherent counter wings, a small outside table, street activity and better occupied-room access. Unity is left stopped in the saved scene. Use **Fixit Fidget > Ace's Cafe > Open layout playtest**, then Play. The owner's ceiling height remains exactly **3.20 m**; player capsule radius remains **0.50 m**; experimental hands remain deferred.

The interior is now 14.8 × 18 m, with a 4 m entrance apron. All 16 seats remain. Four round-table arrangements are rotated 45 degrees to keep occupied chair approaches out of the main aisles; standing distances relative to chairs were retained. Waiting positions were moved away from the staff exit and cross aisle. Final active loiters are (-6.2,10.9), (-5.5,0.9), (6.2,0.75), (6.2,4.0), given as X/Z. These positions are deliberate results of occupied-room checks, not decoration placement. The wider room alone initially failed that check, which is why the seating/waiting adjustments were necessary.

Outside: six pastel San Francisco-inspired houses have projecting bay windows, sash frames, clapboard relief, cornices/brackets, doors, stoops/railings, chimneys and two shop awnings. Plaster and asphalt use shared 1K textures. Per-building geometry is combined by material rather than retaining hundreds of individual authoring objects. Shared geometry/materials and the selected imported models live together under Assets/Playtests/AcesCafeLayout. AssetCredits.txt records CC0 sources (Kenney, Quaternius and Poly Haven). Three matching sections reuse the existing Counter.fbx; the thin battens and primitive wings were removed. The bistro table and two outside chairs are physical scenery, not additional claimable service seats.

StreetLife drives two cars, three walking neighbors, one walking cat and two flying birds along decorative routes, independently of café AI. Walkers avoid the stoops/trees; bird routes stay over the road, clear of roofs. Imported animal animation components and a 100× sizing error were caught and fixed during live visual checks. Scales verified in Play Mode: neighbors 0.92, cat 0.125, birds 0.095. The cat and birds were visually inspected in motion; all six animated actors reported their walk/flight clips. These are an initial ambient-life pass, not new interactive pet/pedestrian systems.

PatronBrain now retries paths without visible unwedging warps, counts actual route progress so sideways rocking cannot indefinitely hold a seat, releases blocked claims after bounded retries, and uses animation hysteresis to reduce walk/idle flicker. Other customer AI has not been broadly rewritten.

Verification: runtime/editor compilation passed; **26/26 navigation destinations** and **23/23 occupied customer delivery approaches** passed in the saved scene. Occupied check uses the actual player capsule, real furniture collisions and conservative stationary customer envelopes on a 0.20 m grid. Live play showed 11 patrons present with 8 settled at one sample, ongoing arrivals/departures, and accepted repair customer Beatriz reaching a waiting position. An actual CharacterController traversal passed staff exit, right aisle, cross aisle and entrance among live customers. These were direct game-method/physics checks, not a full keyboard Day 1–5 run. No Console errors were reported during the bounded run or saved-actor reload. Temporary clock/spawn changes were runtime-only and removed by stopping Play Mode; normal clock is enabled. No FPS benchmark or full crowd-jitter guarantee is claimed.

Campaign save, earlier interaction save and ServiceInteractionPlaytest scene SHA-256 values remained unchanged. Current café progress still uses its separate playtest save. V1 backup remains in workspace validation/pre-cafe-v2.unity. Four purpose-specific source files changed for this pass: existing AcesCafeLayoutSetup/PatronBrain, new CafeStreetUpgrade/StreetLife. Final previews: D:/FixitFidget/Assets/Screenshots/aces-cafe-street-v2.png and aces-cafe-v2-live.png. Intermediate close-ups are kept in workspace validation, not the Unity Project view.

Next: owner playtests this environment for delivery access, travel distance, crowd flow and taste. Then close the existing interaction reliability queue (extra-cup return discoverability, pickup directly from completed inspection, Grace's Good/Perfect blocker), measure performance, and validate M1's two-visit customer showcase. M1 remains incomplete. Hands, Ace's final model and detailed repair-device modelling remain separate Blender sessions. The environment pass does not replace those roadmap gates.

## Approved café layout — playable blockout built

September 13 owner adjustment: the owner lowered the ceiling to y=3.20 m to close a visible sky gap. Live verification found this saved in the new café scene, stopped and clean, with the normal day clock enabled. Preserve this owner-authored placement; do not re-run the generation recipe over the scene. The recipe's initial ceiling height (3.25 m) is superseded by the saved scene. No ceiling transform was changed by the assistant after this feedback. Final check: 26/26 destination paths pass, zero missing scripts and zero Console errors. Campaign save, earlier interaction save and ServiceInteractionPlaytest scene hashes match their pre-build values. Clean camera preview: `D:/FixitFidget/Assets/Screenshots/aces-cafe-layout-preview.png` (editor camera render; overlay HUD intentionally absent). Unity is stopped in the saved new scene, ready for Play.

The owner approved Layout study 02 and explicitly asked to start playtesting it. After a usage-limit interruption, the new scene was created: `D:/FixitFidget/Assets/Playtests/AcesCafeLayoutPlaytest.unity`. Open through **Fixit Fidget > Ace's Cafe > Open layout playtest**. This is an ordinary editable scene, not runtime-generated scenery. The existing ServiceInteractionPlaytest and campaign scenes are preserved.

The initial measured layout is 12.8 × 15 m inside, with a 2.5 m entrance apron; these dimensions are a playtest proposal, not owner-approved final dimensions. The concept's modest enlargement is relative to the image study; the old 20 × 20 m test hall is not the design baseline. Rear intake faces the front entrance; repair and drinks are behind it. Four round-table groups keep 16 working seats, with the existing four active loiter spots. A window banquette is visual only until seated animation is authored. The central runner leaves an obvious approach, and a 1.6 m right-hand staff opening connects to the customer floor. Warm plaster, sage framing, existing wood flooring, simple varied art/cat-print studies, two outdoor planters and a sloping street context establish the approved direction. Street buildings and decoration remain blockout art. A single-sided ceiling is visible from inside while leaving the overhead view open.

Bounded project changes: one editor recipe (`AcesCafeLayoutSetup.cs`), one small SaveManager configuration addition, one new scene, and a two-asset folder holding the shared color palette and dedicated navigation. No new runtime environment system, character rig, hands, cat/baby simulation or gameplay redesign. Shop camera uses a steady whole-room 45-degree view for layout assessment; first-person station cameras and their existing look settings are preserved. Counter camera is parented under an upright rotated group so its look controls respect the new facing direction.

Verification: runtime and editor C# compilation passed (110 runtime / 39 editor sources); 26/26 destination paths passed after saving and reopening; no missing scripts. Initial route failures were corrected by finer navigation baking, excluding interaction/player colliders during bake and placing chair approaches on nearby valid floor. Excluded colliders are restored in finally. Customers were observed arriving at intake, and patrons reaching tables. A CharacterController movement check passed the staff opening while carrying two cups. Counter, bench and drink views were visually inspected; drink labels were readable and both first-person cups visible. Station-view checks used the game's entry methods; native F/E attempts did not provide a conclusive full keyboard-flow check, so do not claim an uncoached keyboard playthrough. These checks do not certify a full Day 1–5 run, repair grading, audio acceptance or performance.

Save isolation: new scene uses `playtest-aces-cafe.json` and `DayLogs/AcesCafeLayout`; existing interaction progress remains `interaction-playtest.json`, campaign remains `save.json`. Filename selection was checked including rejection of campaign/path traversal names. Temporary verification clock disabling and spawned/carried items are runtime-only and are removed by stopping Play Mode. Final readiness and screenshot are recorded below after the last check.

Next: owner plays the new room and judges travel distance, crowd readability, seating clearance, station approach and camera framing. Make one bounded layout adjustment from that feedback, then return to the reliability queue (extra-cup return, inspection collection, Grace's Good/Perfect blocker) and finish M1's two-visit showcase. Performance measurement is still open; no FPS claim has been made. Hands and authored environment/device art remain separate Blender sessions. M1 is not complete.

## Current owner direction — after the latest Day 5 playtest

Latest concept feedback: the owner strongly likes Layout study 01, especially the seating and spaciousness. Requested changes are far fewer indoor plants, varied fun artwork, and a slightly larger café. Layout study 02 has been shown inline with reduced greenery, neighborhood/cat/abstract art and more breathing room; exact artwork and dimensions remain proposals. Preserve the accepted warmth, street connection, seating arrangement and compact work area. Revised preview: Codex generated image exec-971b89e7-19a6-4f98-81a2-ba10c2ae9ce6.png. No Unity geometry was changed for this revision.

The owner reports another playtest through Day 5. New notes: accidental extra cups cannot be conveniently put back and placement seems to trigger pouring; a finished device can no longer be collected directly from inspection; Grace remains Good rather than Perfect, possibly involving lens selection. This is reported playtest evidence, not a reconciled new log audit.

The owner has now explicitly chosen to rethink the café layout together. Intended atmosphere: strongly cozy, warm, modern, in hilly San Francisco; customers supply the chaos; cats, families/babies and varied neighbors should inform the sense of a living world. These are direction and world-building notes, not authorization to implement new cat/baby simulation systems. Earlier Grace environment mockups are liked but their exact images have not yet been recovered. Hands and a full device-art batch remain separate work. Prior blanket art deferral is superseded for this bounded environment concept/layout pass.

Keep the owner closely involved: explain evidence, purpose and tradeoffs; show a small preview; let the owner judge taste and placement before extending it. Reuse existing notes instead of adding reports. One read-only agent checked the three gameplay notes; no gameplay source or saved scene changes were made in this pass. Unity's Scene view was framed from above for layout review; the actual game cameras/objects were untouched. Current floor measures 20 by 20 metres, with workbench/intake/drinks spread across one side.

New audit findings: current live/staged code and both saved scenes separate cup placement from paddle pouring; automatic pouring is still un-reproduced. Empty-cup return exists at the discard basin only, so it is poorly discoverable. PlayerInteractor blocks E while ItemInspector owns an item, confirming the inspection collection regression; a future fix must safely release inspection before carrying. On the specialized Grace prefab, an unrepaired shutter forces zero quality, so Good conditionally suggests an incomplete cleaning task rather than a failed shutter. The generic "Not yet" can also mean already repaired. Record actual remaining task/hover state before fixing; do not assume the tweezers are the sole cause.

Next: review one warm San Francisco café layout concept, agree the work loop, entrance, seating and atmosphere, then build only the approved layout blockout. Keep the cup/inspection/Grace notes in the existing reliability queue and performance measurement open. M1 remains incomplete; this environment pass does not certify its two-visit customer gate.

One preview-only concept board has now been generated and shown inline: "ACE'S CAFE — Layout study 01 — neighborhood warmth". It proposes street-facing window seating, a central approach to intake, and a compact rear work area connecting repair/intake/drinks. The owner has been asked what to keep/change; this arrangement, its character appearances, decor, and implied dimensions are not yet approved. Exact older Grace mockups remain unfound. The preview remains in Codex generated images (exec-9198780d-653d-4644-b3d0-6fb1d39ae0cb.png); it is not imported into the Unity project. Only this existing handoff was edited; no additional code/helper/report files were created.

## Latest update — screenshot notes, September 12

The latest user notes prioritize mechanics and authorize easy fixes to the dispenser camera, mirrored labels and obscured second carried item. Art, hands, models and environment work stay deferred.

Applied and compiled four source changes: BeverageLook now turns around world up with zero camera roll, rejects stale input after entry/focus/pause and ignores look while the camera blends; BeverageStationSetup creates correctly facing printed labels; PlayerCarry uses temporary body-relative isometric offsets (±.62, −.10, −.22) to clear the current capsule; CarryInteractionChecks uses a near-origin fixture with unchanged strict camera-follow tolerance and checks capsule clearance. First-person cup positions and the .09 mouse sensitivity value are unchanged. Control feel still needs user acceptance; bench and counter currently use different sensitivity implementations.

Six label rotations were corrected and saved through Unity in BOTH SampleScene and ServiceInteractionPlaytest. Live visual inspection confirmed readable forward-facing drink names, two visible first-person cups, and sideways camera rotation with zero roll. A camera-range regression passed, as did the full carry/bench check that previously failed. The earlier carry failure was resolved by moving the test fixture from 5km to 5m, without weakening its tolerance.

RunTransactions passed in isolated Play Mode: exact matching cup/device consumption, expected drink and Perfect repair payments, and no duplicate payment. Actual two-hand supply pickups also passed. Native F input successfully exited the beverage station. Final isometric screenshot inspection was interrupted by the user's physical Escape key stopping Computer Use. Do not claim final visual acceptance of the isometric offsets. No more Computer Use was called after that signal. Unity was stopped via its CLI to restore temporary runtime clock/spawner/camera overrides.

Grace's real shutter targeting regression remains unresolved. The Grace agent was interrupted when Computer Use stopped; no new Grace fix from that investigation was deployed. Earlier sections below describe the prior handoff and are superseded by this update where they conflict. The eight previously passing check groups were not all rerun in this latest narrow pass. M1 remains incomplete. Next: normal-control Grace repair, then the two-visit customer playtest.

Review artifacts: validation/pre-label-scene-snapshot.unity preserved transient pre-edit scene state; differences from disk were only calculated UI layout and Cinemachine serialization and were reviewed before saving. validation/camera-notes-compile.txt records compilation. validation/check-beverage-look.cs verifies level yaw/pitch. Live screenshots are Assets/Screenshots/station-level-2026-09-12.png and iso-two-cups-2026-09-12.png in D:/FixitFidget. The latter was captured but not visually inspected after the stop signal.

The user has explicitly deferred hands to a separate Blender modelling, rigging and animation session. Experimental procedural hand meshes are disabled by default. Do not continue shaping those meshes or present them as accepted Ace art. Focus next on the roadmap and M1 Grace's customer showcase.

## Saved implementation

Both `Assets/Scenes/SampleScene.unity` and `Assets/Playtests/ServiceInteractionPlaytest.unity` have the revised compact dispenser installed and saved. Final live inspection confirmed six printed paddle labels, six assigned pour clips, a BeverageLook component and experimental hands disabled. Unity is stopped in the isolated playtest scene, which uses its own save path. The campaign save fingerprint is unchanged.

The saved source includes independent left/right cup selection; place a cup before pressing a named paddle; progressive liquid/stream; restored circular freshness; a separate lid/gasket; a synthesized splash loop and completion chime; removal of the hands inventory HUD; body-relative carrying; matching-item delivery; and a proposed Grace child-collider targeting fix. These are implementation facts, not acceptance of their feel or appearance.

## Validation and limits

The latest source compilation passes against Unity 6000.5.2f1. The latest complete editor check run passed 8 of 10 groups. Stock integration, beverage state/visible fill, continuation rules, Grace content, matching-hand availability, recap input, storyteller and ticket layout passed.

The carry check has since passed with its fixture near the origin and tolerance unchanged. The ray targeting the actual Grace shutter blade remains unresolved. Do not call Grace's repair fixed until normal interaction reaches Perfect. Do not describe this build as fully verified or M1 complete.

CustomerDeliveryHandsChecks.RunTransactions has since passed in isolated Play Mode. Labels and level camera rotation received live visual inspection. Final audio/control feel and isometric carrying acceptance remain open. The owner has subsequently reported a Day 5 playtest with the issues at the top of this handoff.

## Earlier gameplay milestone sequence (environment concept now comes first)

1. Resolve the Grace shutter click blocker and demonstrate normal brush/tweezers repair to Perfect, preserving the original strap and existing fuse rules.
2. Close remaining interaction regressions and verify one coherent normal playtest: two-item bench drops, cup placement/paddle pouring/collection, matching deliveries, closing recap and save.
3. Finish and validate M1: Grace's two visits, readable identity, correct outcome-dependent keepsake, persistence and fresh-player recognition. Settle test-vs-campaign schedule, retry/reward and portrait gaps. Verify the actual photograph composition: older content describes a reunion family photo; the user described a photo of Ace and Grace together.
4. Hold a separate Blender/art session for Ace and the hands; validate anatomy, scale, gripping, rig and animation in isolation. Reuse the agreed character direction for M2's player/customer contact and animation gate.
5. Proceed through M2 animation/contact, M3 boundary encounter, then M4's cohesive Days 1–5 vertical slice. Avoid expanding drinks or systems before the small customer-first experience works.

The clearest positive evidence is the user's enjoyment of watching a cup fill and the emotional response to Grace returning with a photograph. The weak point is integration and feel, not a lack of additional features.
