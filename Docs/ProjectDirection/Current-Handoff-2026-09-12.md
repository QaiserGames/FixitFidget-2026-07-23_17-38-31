# Current handoff — September 12, 2026

## October 9 — The hero set, v3: the tools and the three devices from measured references, in the house style; on `playtest-2`; built and fitted on the Mac, labs pending

**Read this entry first.** Two sessions after `b01d001`. The night before (8 Oct, evening) the bench's five tools, the phone, the pocket watch and Grace's camera were built as hero models for the first time, smooth-shaded, bevelled, glossy and metallic (v2, never committed), and Mansoor's verdict was: "the tools look absolutely bad, now they look like kid toys, and we lose that distinctive art style … the cloth you can literally see a triangle on the square it just looks so half cooked. Same goes for all the hero items. Be honest, search through 10 images … one sub agent reads and looks at exactly how these items we've modeled are actually supposed to look like." So this commit is the whole set again, **v3**, built from a reference sheet of about seventy measured photographs (`claude/hero-reference-sheet.md`, gathered by a research agent through the Mac's browser while the models were rebuilt), in the game's own shading language. The standard is `claude/hero-models-spec.md` (rewritten); the session is `claude/session-2026-10-09-notes.md`.

**v3.1 (same day, `aee0b3f`):** a purpose pass. Every part that does nothing for the player was cut: the pry tool's hang hole; the phone's speaker, Taptic engine, port assembly and board screws; the watch's hinge knuckles; the camera's badge, self-timer lever, frame counter and tripod boss. Triangles now: tools 7,688, phone 3,272, watch 12,358, camera 10,624. Only `BT_PryTool.fbx` changed among the tools. **Not yet run in Unity:** Bench tools 1, Devices 1 and Devices 2 need re-running on the Mac. The push is still pending (the Mac's git has no credentials; GitHub Desktop needs the screen unlocked).

**What was wrong with v2, honestly:** proportions by instinct (a 26 mm bulbous driver handle where a precision driver has a 9 mm spinning waist and a 3 mm blade; a tuft like an eraser); the wrong shading language (smooth, 32-sided, three-segment bevels, glossy, metallic, saturated, next to a flat-shaded warm café: the art style guide's rules exist for this); detail from roundness instead of anatomy; and the cloth's triangle, a construction artefact.

**What changed:**

1. **`Tools/Blender/hero_lib.py`** (new): the rulebook as code. `Faceted` (flat shading by default, smooth only for soft parts with a 42° angle rule, one chamfer on boxy parts), loops, tubes, revolves with lobes and knurls, `chamfered_slab`, `ring_wall`, `rrect_wall`, `gear`, `sweep`, `text_mesh` (Blender's built-in font, headless) and **`to_blender` / `to_unity`: Unity (x, y, z) = Blender (−x, z, −y)**. The X sign was found by Devices 1 (every child of the first camera sat on the far side of its body): the FBX transfer from a right-handed to a left-handed frame keeps what you see, so Blender +X lands on Unity −X. Every earlier kit was symmetric in X.
2. **The five tools** (`bench_tools.py`, 7,760 triangles; `BT_*.fbx`, `bench_tools.json`, `BlenderSource/BenchTools_v3.blend`): a 134 mm Wiha-proportioned driver (3 mm blade with Phillips fins, 9 mm waist, 16 mm grip with four brand-green pads, a cap behind a seam, a white cross on top); Dumont-proportioned ESD tweezers (rounded welded heel, 1.2 → 0.25 mm arms, bright ground tips, a hairline serration band; points 4 mm apart so `ToolPickup`'s 1.1° pinch still closes them); a flat dusting brush (seven clumps of golden bristle cut to a chisel, a crimped tinned ferrule with two grooves, a raw beech handle); a jimmy-style pry tool (0.8 × 8 mm steel blade, a bolster, a 15 × 8.5 mm over-moulded handle with green inlays, a hang hole); a 15 × 18 cm terry cloth folded in quarters as **four layers** (two rounded fold edges, stepped lips on the open edges, a puffed top with three creases, the free corner lifting as a sheet). Materials `BT_*` remade by Bench tools 1 from the manifest: metallic 0, warm, satin at most.
3. **The phone** (`hero_phone.py`; `HD_Phone.fbx`): iPhone-15 proportions on the prefab's frame (the stand-in fixes the screen top at +6 and the cover face at −7, so it stays 13 mm thick; the plan carries the realism): 10 mm corners, a flat rail with antenna lines, the side button, ring switch and volumes, the SIM slot, USB-C with its two pentalobe screws and 3 + 6 holes; a 2.5 mm black bezel round a 65 × 134 screen (was 60 × 120); the back glass with the corner island (two diagonal rings, flash, mic) and a frame lip; and under it the inside a repair shows: battery and coil, camera module, shields, speaker, Taptic engine, board screws. Case screws 8 mm pan heads (were 10 mm cylinders).
4. **The pocket watch** (`hero_watch.py`; `HD_Watch.fbx`): an open-face railroad watch at the prefab's 3×: band and bezel as one gold solid, an enamel dial 3.5 mm under the bezel with a railroad track (60 ticks), raised Arabic numerals, a sunk sub-seconds at 6, blued spade hands, hinge knuckles at 6, the pendant saddle and a wire bow standing round a 24 mm fluted crown. The open back is a **3/4-plate movement** with the mainspring (a blued ribbon, five turns, in its brass barrel on the pillar plate: the prefab's seat at −7 sets the floor at −5.7) and the balance under its cock, the crown wheel, jewels in chatons, slotted plate screws; the back plate flat-rimmed and shallow-domed with the four 12 mm screws (were 18 mm) resting on the dome.
5. **Grace's camera** (`hero_camera.py`; `HD_Camera.fbx`): a Canonet/Olympus-35 rangefinder at the prefab's 2×, its top plate along the edge nearest the player (rewind crank, hot shoe over the lens, frame counter, shutter release, advance lever, eyelets, the three finder windows at their real size ratios), a bottom plate with a tripod boss, leatherette between; the lens barrel (base ring, focus ring and tab, two knurled chrome rings with ticks, name ring, meter window) and a domed front element in a retaining ring under the lens grime; the service opening at the player's right under the top plate's lintel, lined black, with the film transport (gate between polished rails, a toothed sprocket drum under the film-guide grime, a take-up spool) and the leaf-shutter assembly (chassis, drum with a chrome rim, four gears, cocking lever, coil spring, petal-shaped blades, the bent one kinked); stitched straps with a slide buckle, the loop frayed. The grime boxes and the old scratches are where they were.
6. **The Unity steps.** `BlenderKitSteps.cs`: a child the prefab turns (the crown, flagged `turned` in the manifest) no longer fails "upright"; a triangle count within half a percent is whole (Unity drops a few slivers: the camera loses 20 of 10,900). `HeroDeviceSteps.cs` (**Devices 2**): the new head sizes; a per-device `moves` list, used once: `Grime_Extra_Watch_1` sat 84 mm from the centre, 9 mm outside a 75 mm case, and now sits on the movement plate. `export_hero_devices.py` / `export_bench_tools.py` write children's positions in Unity's axes with the X sign right; `review_hero_v3.py` renders every kit from the player's side.
7. **The three prefabs** (`PhoneRepair`, `PocketWatch`, `GraceReunionCamera`) carry the hero meshes, scale 1, colliders from the meshes (screws head-only), the kit materials; and the bench scene has Bench 2 rebuilt with the new tools.

**Checks (9 Oct, on the Mac, 00:07–00:27 his time):** Bench tools 1: 5 pieces, all mapped (`Logs/Bench/bench-tools-2026-10-09_000740`); Bench 2 saved; **Devices 1: all PASS** (`hero-devices-2026-10-09_002622`, after two runs that found the X sign and the crown's turn); **Devices 2: 27 pieces PASS, one move** (`hero-devices-fit-2026-10-09_002659`). **Bench 3 was started at 00:27 and Unity wrote nothing afterwards**: the editor stalls whenever the Mac's display sleeps or locks (the same stall froze the project load at 21:15 the night before). **The play-mode labs are therefore pending**: Bench 3, Every device's grime, Device parts, Circuit integration, Human counter integration, Grace camera tweezers interaction and content, Two-hand bench placement, Stations (both), Aim help. Run them with the display kept awake (`caffeinate -d -i` in Terminal) before building on this commit; if one fails on a hero device, `git checkout -- <prefab>` restores the stand-in and the FBX stays imported.

**Decisions his to overrule:** the devices keep the stand-in sizes (15 cm watch, 24 cm camera, a 13 mm phone) proportioned as the real things; the brand's green as the accent on Ace's tools; a yellow cloth (blue vanished on the mat); a sage iPhone, a gold railroad watch, a chrome-and-leatherette camera; the brush is the soft wooden-handled kind, the pry tool the jimmy; no watch crystal; the camera's opening is a service fiction made of real parts.

**Also in this commit:** `BlenderSource/*_v3.blend` (the v2 tools blend and the v1 device blends were never committed and are set aside in `_to_delete/` on the Mac with the other leftovers).

## October 8 — The tabletop, and his playtest notes; on `playtest-2`; built and checked on the Mac

**Read this entry first.** One commit after `47af75c` (4a). Mansoor played 4a on the Mac and sent eight notes (8 Oct, early): the tool and item motion should be "more fluid"; using the tweezers "somehow just broke the camera"; the tools are blocky, he'd take "a more detailed cartoon style"; the tray and tools "are floating on the table"; "this random prop on the table whenever I place an item"; in the isometric view it is "literally impossible to notice if I'm grabbing an item like a phone or a watch"; the tip icon and the emoji come at the same time and look clustered; and the Mac ran it badly, "don't fixate on that". He asked for "more correctness". This commit answers seven of the eight (the tools' look is the Blender session, next) and, on the way, fixes four things the lab caught. **The bench spec is still `claude/bench-spec-v2.md`**; what changed here is the close-up's framing, in its spirit.

**What changed:**

1. **The close-up is a tabletop now** (`ItemInspector`, rewritten round the device *lying on the mat*): the device is set down flat where the player put it (`TableTopPoint`: its own slot, kept 9 cm inside the mat), face up or face down and square to the view; the close-up camera (`CM_InspectCam`, placed by Bench 2) looks down at it at **58° from 58 cm**. The stick or a drag **spins it on the mat** (yaw, with inertia; it settles square to the nearest quarter turn within `faceSnapWithin` 22°), up or down on the stick **tilts an edge up to peek under** (`peekTilt` 35°, springs back flat), R / Y **flips it over** (180° about its long axis, lifted clear as it turns), and the zoom closes on the device's own spot on the table. Its pose is always `faceSign × yaw × tilt` laid on the table (`Lay`, `PoseRotation`, `Place` puts the lowest corner on the mat), never a free rotation, so it can't end on an edge. Stepping back puts it down as it lay. The old "face-on, hanging in the air" close-up and its 20° camera are gone; `Bench 2 - Undo` restores the old camera.
2. **The stage is grounded** (`Playtest3Session4Steps`, Bench 2): the mat is laid on the **real bench top, found by a ray** (y 1.000; the first stage was laid off the rig and floated 22 cm above the wood: "floating on the table"), a touch toward the camera (`matZ` −2 cm, clear of the props at the back of the bench); the tray's floor is the light steel so a dark part reads in it; the physics material is `.4/.5` friction, `.08` bounce. **The bench's three slots are moved onto the mat** (`MatSlots`): a device set down used to land on the old `Slot_0`, half a metre left of the mat, on top of a wooden tool-organiser prop; that was "the random prop on the table whenever I place an item".
3. **Screws pop** (`Screw.Free`): freed, a screw hops about 2 cm out of its hole, toward the device's nearest side, with a little spin, and lands on the mat beside the device (`popUp` 0.6 m/s, `popOut` 0.16 m/s). With the device lying flat its screws face up, and without the hop a freed screw fell straight back against its own hole and looked still in (the lab's rest check caught it twice: at rest 7 mm from the hole). The hole's hover collider is thin and flush now, not a 3 mm step for the screw to lean on.
4. **Held parts ride at the right depth** (`BenchHand.PointAtDepth`; `ReplaceablePart.Follow`, `LoosePart`, `RemovablePart.GrabMove`, the floating tool in `ItemInspector.PlaceTool`): a part picked up rode "depth along the cursor's ray", but **a screen ray's origin is on the near clip plane, 5 cm out from the camera**, so it rode 5 cm too deep — the broken screen was carried in *under the tray's floor* and lay there on the mat, drawn but hidden (the lab, 8 Oct: "−13 mm above the floor's top" before it was even let go). Depth is measured from the camera now. Held in the tweezers a part rides **4 cm toward the camera** (`ReplaceablePart.HeldLift`), clear of its seat and over the tray's walls; a carried cover 3 cm.
5. **Nothing can bury the fresh part:** out of the device, the broken part (and the scrap after the swap) is a **loose piece any tool picks up and moves** (`ReplaceablePart`: `Loose`, `Grabbable`, prompt "Hold to move it", "Old cracked screen"), and the **fresh part lies across the tray at the device's side** (`LoosePart.Make`: along `TrayAcrossAxis`, −4.5 cm along, ±6°), leaving the rest of the tray to the broken one; the **tray is 16 × 16 cm inside** (was 16 × 14). The fresh part **arrives** rather than is there: dropped in from a few centimetres up with its tink and the word "New part" over it for a moment (`PresentFresh`, `Juice.Words`). The Day 1 guide says "drop it in the tray".
6. **Fluid tools** (`ToolPickup.Follow`): the tool in the hand follows an eased point (16/s idle, 40/s working), swings with its own speed (up to 12°), springs to the part (ω 16 idle, 26 working) and turns in over a few frames; it leans 18° to the camera (was 28°). `ReturnHome` resets the easing.
7. **The camera can't be "broken" by a trackpad** (`ItemInspector.HandleZoom`): a notch of a wheel is one step (0.03–0.18 of the zoom, from the notch's size), **no more than a step a frame** (a trackpad gives a little every frame and threw the camera to its limit: "used the tweezers and somehow just broke the camera"), D-pad up/down 1.2/s.
8. **What's in hand reads from above** (`PlayerCarry`, `ShopUI`): in the overhead view a carried item is drawn **2.2×** (`overheadScale`), held high beside the shoulders (±0.62 m out, 0.62 up, 0.22 back: the same sideways points as before, so no facing hides it behind the capsule; the carry check's silhouette rule), with a slow bob (`overheadBob` 3 cm); and the HUD's prompt line gets an **"In hand  Phone · Cracked Screen"** prefix (`PlayerCarry.Summary`; drinks by name, two things joined with "+"). At the bench the prompt line drops 78 px so it's off the device (`BenchPromptDrop`). First person keeps real sizes.
9. **The tip comes a beat after the face** (`Juice.Money(who, amount, tip, delay = .45f)`; the pop is born late and hidden until then): the reaction badge first, the "+$6 +$3 tip" chip 0.45 s later, up and to the right of it.
10. **Hints**: `ControlHints.Flip` (R / Y); the Day 1 guide tells the player to flip when the screw, the part or a loose cover is on the face that's down; bench lines reworded for v2.
11. **The lab** (`BenchLab`, now **89 checks**): the device lies flat and square on the mat; the stage sits on the bench top; a nudge settles square, a swing stays, the stick tilts an edge up and it lies back; the flip; the freed screw **hops clear of its hole** and the report says how far it lies from it; the lifted part rides toward the camera (38 mm nearer than its seat) and **above the tray's walls** over the tray (29 mm; the walls stand 21); the broken part lies in the tray **in view** (drawn, on the floor, not under it) **beside the fresh part** (8 cm apart); out of the device it is a loose piece; the scrap stays in the tray, in view, after the swap and at the end. `TrayPlace`/`InView` report where a thing lies in the tray and whether the camera draws it. Photos as before, plus `00-carried-from-above`, `00-placed-from-above`, `00-the-bench-in-first-person`.

**Checks (8 Oct, on the Mac):** Bench 3: **89/89** (`Logs/Bench/bench-check-2026-10-08_012438`, 32 s). Juice 31/31 (`Logs/Juice/juice-check-2026-10-08_012822`), Aim help 26/26, Stations first person 52/52, overhead 53/53 (`Logs/AimHelp/aim-help-2026-10-08_013038`, `Logs/Stations/stations-*-2026-10-08_0131*`). Edit mode, all PASS: Two-hand bench placement (the carry points are back at ±0.62/−0.22 for its silhouette rule, raised to 0.62), Day 1 onboarding, Grace camera tweezers interaction (the finished shutter's old mechanism is scrap that moves), Grace camera content, Circuit integration, Every device's grime can be brushed, Device parts, Featured repair, Customer delivery from either hand, Support call integration, Human counter integration.

**Found on the way, fixed:** the near-plane depth (4, above: the whole "parts under the floor" mystery; it also affected a carried cover and the tool floating by the cursor); the screw that lay against its own hole (3); a device set down landing on the old slot over the organiser (2); the interpolating fresh part (teleport through the rigidbody too, from 4a) was fine, but the broken part dropped on top of it was not: side by side now (5). The lab's own rest check had a 2 cm "away from the hole" condition that was wrong for a flat device; it is 1.2 cm and the pop makes it moot.

**Photos worth his look** (`Logs/Bench/bench-check-2026-10-08_012438/`): `00-the-bench-in-first-person` (the device on the mat between the caddy and the tray, nothing on the organiser), `04-screws-out` (the two screws lying on the mat below the device, the driver standing on the hole), `07-broken-part-in-the-tray` (the old screen dark, the new one blue, side by side), `10-fixed`.

**Not done:** the tools' look (his note 3) and the devices as models are **the next session, in Blender** ("a more detailed cartoon style": smooth-shaded round parts, real proportions, a far higher budget than 4a's stand-ins; the phone, the watch and Grace's camera with real screws and seats; a rig contract so the bench code needs no change). The Mac's frame rate (note 8): not looked at, by his instruction. 4b after that (grime and glass as paint masks, the "Fixed!" moment). The lamp on the bench lights the empty wood left of the mat, not the mat (a scene nudge for 4b). The four float-noise assets and `_to_delete/Outline.cs` on the Mac are as the previous entry says; his playtest's `DayLogs/AcesCafeLayout/Day01–03` are committed with this (two Perfect repairs on Days 2 and 3).

## October 7 (late) — The bench, v2, part 4a: the physical bench; on `playtest-2`; built and checked on the Mac

**Read this entry first.** One commit after `ad1f04d` (the Blender session). The spec is `claude/bench-spec-v2.md` (agreed with Mansoor, 7 Oct: "the screws actually coming out from where u clicked them … the exact same way restory has it, and not just the screws aspect, but everything"; his four answers in its §6: the driver fetches screws, nobody carries them; a let-go hold keeps its place; placeholder sounds made in code for now; an open cover costs no grade). This commit is **4a**: the holds, grabs and physics; the stage; the lab. **4b** (the grime and the glass as paint, the "Fixed!" moment, the cue set) and the devices session (hero models of the phone, the watch and Grace's camera; the tools redone) are next.

**What changed:**

1. **A press is one of three things, decided by the part** (`BenchInteractable`: `Holdable`/`HoldBegin`/`HoldTick`/`HoldEnd`/`HoldProgress`, `Grabbable`/`GrabBegin`/`GrabMove`/`GrabEnd`, `WorkPoint`/`WorkNormal`; `BenchHand` carries the cursor's ray, its hit, the tool and the camera). `ItemInspector` runs the hold and grab each frame (`HoldTarget`, `GrabTarget`), places the tool model on the part (`PlaceTool`), spins the device with inertia and **settles it onto a face** (`NearestFace`: the eight orientations that show one of the slab's two work faces square-on; never an edge; `faceSnapWithin` 22°), **presents it face-on when the close-up opens** (a phone lying flat showed the camera its edge), **flips it** on R / Y (`Flip`, 180° about the close-up camera's up), and **zooms** (scroll, D-pad up/down; `zoomRange` 1.2–0.42 of the camera's distance). The old instant `Activate()` stays for circuit tiles and the mute switch, and as "the whole thing by itself" for the labs' hand and the Day 1 guide (with an instant form in edit mode, for the editor's checks).
2. **Screws** (`Screw`, `ScrewTarget`, new `ScrewSocket`): the driver held on a screw backs it out **along its own axis** (3 turns over 0.45 s on the phone, a tick a turn; `outSign` works out which way is out from the device's middle, because the stand-in prefabs' screws have their local up pointing into the body); let go early it stays part way out and the prompt says so; free, it clicks, becomes a rigidbody and **falls** (the stage watches it); the hole it leaves is a dark ring (`ScrewSocket`, sized from the head). The driver held on the hole (or on the loose screw) **fetches** the screw in a 0.3 s arc and screws it home; the hole's mark goes. `Rescrew()`/`Unscrew()` kept for old callers.
3. **Covers** (`RemovablePart`: `CoverState` Seated → Popped → Held → Loose): the pry tool held at an edge (0.45 s) creaks and **pops** the cover 7° about its far edge; a press on the popped cover picks it up (`Grabbable`), it follows the cursor at the depth it was picked up at and **drops** where it is let go, with physics; carried back near its seat (within 3 cm as seen, turning home as it is carried) it **snaps home** (`part.on`). Any tool or bare hands pick a loose cover up.
4. **Parts** (`ReplaceablePart`: `PartState` InPlace → HeldBroken → Removed → Replaced; new `LoosePart`): the tweezers held on the broken part **pinch** (0.25 s) and it lifts out, follows the cursor with a wobble, and drops where let go. **The fresh part waits in the tray from the moment the close-up opens** (`PresentFresh`, laid the long way), is pinched up the same way and **seats when let go over the empty seat**: the held part is slid along its sight line to the seat's depth before the 16 mm test (`SeatWouldTake(pos, camera)`), so what the player sees over the seat, seats. Only then is the part replaced. A fresh visual authored *under* its broken part (Grace's camera) seats into the device, not into the scrap. `JobBase` gained **loose parts that don't count against handing back** (`RegisterLoose`/`LooseParts`: the fresh part before it is in, the broken one after; `DetachedParts` stays the gate; the pad's cursor steps through both).
5. **The stage** (`BenchStage`, built by the step below): the mat (a solid, slippery slab), the parts tray (16 × 14 cm inside, 2.4 cm walls; **its magnet pulls a loose piece down and damps its slide so it settles where it lands with a tink**, never to one heap), the catch (a piece 25 cm under the mat is set back over the tray), the tool caddy with five stands. `TrayDropPoint`, `FreeSpotOnMat`, `InTray`, `Watch`/`Forget`, `SettledInTray`.
6. **Tools in the hand** (`ToolPickup` with the `BT_*` models from the Blender session; `ToolType.Cloth` new): picked, a tool leaves its stand (its colliders off so the cursor reaches the part under the tip) and rides with the cursor over the device, tip on the surface, handle up the normal and leaning 28° to the camera; it does its motion at the part (`Animate`: the driver turns, the tweezers' leaves close, the pry levers, the brush strokes, the cloth circles); put down it returns to its stand. **The hover is a white outline round the whole part** (`BenchOutline`, an inverted hull with smoothed normals; shader `Assets/Data/Resources/Bench outline.shader`), not a tinted material. (`BenchOutline`, not `Outline`: UnityEngine.UI has one and the recap phone uses it.)
7. **Placeholder sounds made in code** (`PlaceholderSounds`): every bench cue (`screw.turn/free/drop/bounce/fetch/seat`, `cover.creak/pop/lift/clack`, `part.pinch/lift/drop/on/replace`, `tool.pick/down`, `device.snap/flip`, `cloth.squeak/gleam`, `grime.scrub/clean`) is a few milliseconds of enveloped noise and sines; `SoundPlayer.Play` plays the placeholder whenever a cue has no file, so the Sonniss pass replaces them cue by cue with no code change. `SoundBank.Defaults()` lists the new cues.
8. **Steps and the lab** (`Assets/Editor/Playtest3Session4Steps.cs`): `Fixit Fidget › Bench › Bench 2 - Build the bench stage (the scene)` (under the Workbench's `InspectRig`: mat, tray, caddy, the five tool models as `ToolPickup`s, `BenchStage` wired; the old tinted cylinders switched off; the scene saved; `Bench 2 - Undo` puts it back) and **`Bench 3 - The bench - play check (lab, drives itself)`** (`BenchLab`: a lab Day 1, a phone with a cracked screen put on the bench by the check's hand, then a virtual pad does the whole repair as a player would, 78 checks, photos in `Logs/Bench/bench-check-<time>/`). The pad's cursor runs in a lab session whether or not the editor is in front (`PadCursor`, `CafeLab.Active`).
9. **The Day 1 guide's bench lines** say hold, pop and drag, pinch, the fresh part from the tray, the holes.

**Checks (7 Oct, late, on the Mac):** Bench 3: **78/78** (`Logs/Bench/bench-check-2026-10-07_224713`: face-on; zoom in and out; a nudge settles back, a swing stays; the flip; the driver rides with the cursor; a short hold backs a screw 4 mm out along its axis, 0.00 mm sideways, and keeps its place; held on it comes free, falls and lies on the mat; the hole is marked; the pry pops the cover 7°; the popped cover is picked up, follows 14 cm, drops and lies on the mat; the tweezers pinch the broken part out, it's dropped in the tray and the magnet settles it; the fresh part is in the tray from the start, pinched up and seated 3 mm off as seen: Perfect but not finished; the cover dragged home snaps on; each hole held fetches its screw and turns it home: finished, Perfect, in one piece; the clock ran; the catch; every cue has a placeholder). Aim help 26/26, Stations first person 52/52 and overhead 53/53 (the lab's hand does the whole repair through `Activate`), Grace camera tweezers interaction (rewritten for v2: the hold begins only with the tweezers, unpaused, not in the recap; the pinch takes the mechanism out; the seated blade finishes it Perfect), Grace camera content, Every device's grime can be brushed (9 spots), Device parts, Two-hand bench placement, Day 1 onboarding, Circuit integration, Featured repair, Continuation rules: all PASS.

**Found on the way, fixed:** the stick's turn and the coast doubled up while the stick was held (the device turned twice as far); a rigidbody with interpolation snaps the transform back to its own pose, so a part teleported by its transform alone was left inside the device and shoved out (teleport through `Rigidbody.position` too); a 12 cm part dropped at any angle into a 13 × 10 cm tray lay across its wall; the hole ring was sized from the head's world AABB, which the device's yaw on the bench inflated; a release and a press queued in one frame read as one press (the lab's `LetGo()` waits two frames).

**A correction to the evening entry below:** `InspectRig`'s (0.5, 1, 1) does not squash what is under it: the `Workbench` above it is (2, 1, 1) and the two cancel; everything under the rig is at true size (the tool stands are children of the unscaled stage object only because a *rotated* child of a *stretched* slab shears).

**Not done, for 4b and the devices session:** the brush and the cloth still scrub `GrimeSpot` primitives (the paint masks are 4b); "Fixed!" is the old sparkle and word; the close-up camera is the old one (20° down from 55 cm, the device in the lower middle of the frame and the HUD's prompt line across the mat: a steeper, closer framing and a prompt moved up belong with the device models); the broken part can be dropped anywhere (fine) and the player can't yet rotate a carried cover; no outline colour change when a carried part is over its seat; the tools lean a lot toward the camera (28°) and read big; the mat is a flat blue slab, the tray and caddy boxes. Mansoor's verdict on the tool models stands ("blocky"): they are stand-ins until the devices session. On the Mac, four assets re-serialised by Unity with float noise (`GH_Room_Tiles.mat`, `Night 1 - Barnaby.asset`, `Night walk - street sign.mat`, the TMP fallback font) are left uncommitted; `_to_delete/Outline.cs` beside `Assets` is the renamed file's old copy, safe to bin.

## October 7 (evening) — The Blender session: the bench's five tools and Grace's cover; on `playtest-2`; built on the Mac

**Read this entry first.** One commit on `playtest-2` after `3f4439d` (the evening sync that pushed the branch to GitHub for the first time): this session's models, scripts and step, and this entry. **The branch is now pushed**, and the project runs on two machines: the Windows PC (`D:\FixitFidget`, the shipping machine) and the Apple Silicon MacBook Pro (`~/Documents/FixitFidget`, Unity 6000.5.2f1 matched); they meet on GitHub. The ignored licensed folders (`Assets/Synty`, `Art/Mixamo`, `Art/CityNeighbors`, `DownloadCache`, `ThirdParty/Quaternius_UAL/Humanoid`) were copied to the Mac by hand (`claude/mac-licensed-assets-handoff.md`). The plan is `claude/playtest-3-sessions-2-6-plan.md` (§4 this session; §5 session 4, §6 session 5 take these pieces).

**What changed:**

1. **The five tools, as models** (`Tools/Blender/bench_tools.py`, built headless in Blender 5.2; source `BlenderSource/BenchTools_v1.blend`; `Assets/Art/Models/BenchTools/BT_*.fbx` with `bench_tools.json`): a screwdriver (153 mm: flat blade, steel shaft, brass ferrule, a green faceted handle with three black grip flats, a black cap), tweezers (120 mm), a brush (140 mm: three tufts of bristle, a crimped steel ferrule, a chestnut handle with a blue band), a pry tool (135 mm: a flat steel blade and shank, a dark collar, a red flattened handle) and **the cloth** (his call: 80 × 68 × 17 mm, folded, one corner lifted, blue microfibre). 1,028 triangles for all five against the plan's 1,800. Materials: the café's (`DC_Steel`, `DC_SteelDark`, `T2_Brass`, `CC_Wood_Counter`) and six new `BT_*` (handle green = the brand's green, handle red, handle blue, bristle, grip black, cloth), made by the Unity step in `Assets/Art/Materials/BenchTools`.
   - **A tool's origin is its working tip** (the blade's edge, the points, the bristles' ends, the cloth's face on the glass) and its handle goes up +Y. So session 4 puts a tool to work by placing its origin on the part and pointing its −Y along the part's normal; a screwdriver turns about its own Y. Nothing goes below the origin.
   - **The tweezers are jointed:** one model, three meshes: the heel (the root, origin at the points), and `Leaf_L` and `Leaf_R`, each its own mesh with its origin at the heel (local (0, 0.112, 0)), so session 4 closes them by turning each leaf about its own X by a degree or two. At rest the points are 3 mm apart.
   - The flat side of a flat tool (the pry blade, the cloth's fold) faces +Z in Unity, the side the rail shows to the inspection camera.
2. **Grace's cover** (`Tools/Blender/grace_cover.py`; source `BlenderSource/GraceCover_v1.blend`; `Assets/Art/Models/GraceHouse/GH_KitchenIsland.fbx`, `GH_BoxStack.fbx` with `grace_cover.json`): a **bar-height kitchen island**, 1.10 × 0.56 m and **1.12 m** to its chestnut worktop (a real height: a 44-inch island), her kitchen's cream with two Shaker doors and two drawers on the front, a towel rail on the back, a dark kick, 348 triangles; and a **stack of three taped cardboard boxes**, **1.14 m** tall, each turned a little on the one below, 312 triangles. Both at least 1.1 m, the cover rule of §6.1: from about 1.5 m, a crouched Ace (his 1.0 m capsule) behind either is hidden from Grace's eye at 1.70 m. Materials all existing (`GH_Kitchen_Cream`, `GH_Cardboard`, `GH_Tape`, `CC_Wood_Counter`, `CC_Dark_Kick`, `DC_Steel`).
3. **Two Unity steps** (`Assets/Editor/BlenderKitSteps.cs`): `Fixit Fidget › Bench › Bench tools 1 - Import and check the tools (the models only)` and `Fixit Fidget › Night › Grace's cover 1 - Import and check the pieces (the models only)`. Each reads its manifest, sets the FBX imports (a still prop at its real size), makes or maps the materials by name, checks every piece (its meshes, triangles and size as built, upright, its origin where it was built, the tweezers' leaves at the heel) and photographs it in a preview scene: the tools points-down on a bench top as the rail will hold them; the island and the boxes with a crouched Ace 0.45 m behind each, from Grace's eye at 1.5 m and 2.5 m. **The café scene is never opened or changed.** Reports: `Logs/Bench/bench-tools-<time>/`, `Logs/Night/grace-cover-<time>/`.
4. **Shared helper:** `fixit_blender.export_fbx_tree` (a root and its children to one FBX, from copies at the origin). `Tools/Blender/review_bench_tools.py` renders both kits for review (Cycles). `Tools/Blender/README.md` lists them.
5. **Cleanup:** the placeholder `Assets/Scripts/Diagnostics/AceTurnCheck-1.cs` (a comment saying it is safe to delete, committed by the evening sync) is deleted.

**For session 4 (the bench's feel), found while measuring the rail:** `InspectRig` has a local scale of **(0.5, 1, 1)**, so anything parented under it comes out half as wide: the four cylinders are 0.01 × 0.12 m in the world, not 0.02. The new rail must sit under an unscaled parent (or the rig's X scale moves onto the `InspectPoint` alone) before the tools go on it, or they import squashed. The tools are built at true size. Today's `ToolPickup` tints `rend.material.color` (one renderer, one colour): with multi-material models that must become the outline the plan already asks for.

**Checks (7 Oct, evening):** Bench tools 1: 11/11 (`Logs/Bench/bench-tools-2026-10-07_203227`; 6 BT_ materials made; the tweezers' leaves at (0, 0.112, 0)). Grace's cover 1: 5/5 (`Logs/Night/grace-cover-2026-10-07_203302`). Blender: 0 floating parts, 0 degenerate faces in either kit. Unity opened the project on the Mac with no C# errors.

**Mansoor's verdict on seeing them (7 Oct, 20:30), which changes session 4:** too blocky; "it doesn't give *wow, this is a great indie game with a sense of uniqueness*." He wants the bench to feel the way ReStory's does: the screw comes out of the hole you clicked, along its own axis, and *falls*; and not only the screws, everything about removing things. So these five are **stand-ins** until the bench spec v2 is agreed: the close-up needs its own rules (smooth-shaded round parts, real proportions, material response, a far higher budget than the plan's 1,800 for five), and the real gap is the devices, which are still primitive boxes. The scripts stay (every piece is parametric); nothing of this is in the café scene.

**Choices made inside the plan (his to overrule):**

- The origin-at-the-tip convention and the jointed tweezers (above).
- Handle colours: green (the brand's) for the screwdriver, red for the pry tool, a blue band on the chestnut brush, steel tweezers with black grip pads, a blue cloth. They replace the tints as the way the tools read apart.
- Sizes: 120–153 mm long, 20–26 mm handles (chunky, as the style guide asks; real ones are 10–15 mm). The island at 1.12 m rather than a worktop's 0.92 (it has to be cover; bar height is a real thing). The box stack as three boxes turned on each other rather than a tidy column.
- The brush's bristles as three tufts, not one block.

**Seen, not changed:**

- The 1.6 MB `Claude outputs/` folder (182 images and notes) came into the repository with the evening sync; it isn't gitignored. Harmless; his to keep or drop.
- `Assets/_Recovery/` (Unity's crash recovery scenes) came in the same way.

**Next:** the bench spec v2 (ReStory's physicality at this game's pace: hold-to-unscrew from the real hole, screws that drop into a tray, covers and parts lifted with the mouse, an orbit camera, sound; the three demo devices as real models with real screws), agreed before anything is built; then session 4 rebuilt to it, then 5 (cover and her house), 6 (the café), then the playtest together.

## October 7 (afternoon) — Playtest 3, sessions 2 and 3: stations as reach, aim help, a slimmer Ace; one UI skin, the HUD's corners, the phone by day (`04a091b`, `638b318`); on `playtest-2`

**Read this entry first.** Three commits on `playtest-2` after `b1d419e` (session 1's handoff): session 2 (`04a091b`), session 3 (`638b318`) and this entry. Nothing is pushed. The plan is `claude/playtest-3-sessions-2-6-plan.md` in the project docs (§2 session 2, §3 session 3), written and agreed before either was built; Mansoor's answers are its §10 (build straight through, no stops; interior cells after the playtest; E on the station; keep the cloth).

### Session 2 (`04a091b`): interaction

1. **Stations as reach.** Nothing is docked by day. In first person the counter, the drinks and the bench work on Ace's own crosshair within reach: E talks to whoever is waiting, the hands work the drinks (left click / LB the left hand, right click / RB the right), a click or RT works on a device on the bench. From above, **E at the dispenser opens its close-up** (`StationCloseUp`; the left stick or WASD, B, Esc or right-click leave it) and E on a device on the bench works on it. F is retired by day (it stays the torch at night). One word for "Ace is busy", `InCloseUp`, replaces "docked" wherever it meant busy. The Day 1 guide's steps are "walk up and …".
2. **Aim help on a pad** (`AimAssist`): friction near things to use, magnetism while turning, a settle when the stick lets go, D-pad left/right steps between targets (and between a device's parts at the bench); the bench cursor snaps. The mouse is never helped. On by default; its switch is in the phone's Settings (session 3).
3. **A slimmer Ace:** the capsule 0.35 m round with a 0.035 m skin (0.77 m across; it was 1.16), crouched 1.0 m tall with the feet kept, the crouched eye 0.92 m, and "Too low to stand" under anything low. Every copy of the old size is updated; Grace's stairwell gets a collider for the thinner Ace.
4. **The officer's order** is one line said in the room, keeping the joke about the second batch.
5. **New labs** (`Fixit Fidget › Playtest`): stations as reach in each view (the presses counted: 0 station presses in first person, 1 per station from above), aim help, the capsule and the crouch. `PlayLab` is their shared base.

**Checks (session 2):** stations overhead 53, first person 52, aim help 26, capsule 18, controller 28, juice 28, recap phone 107, Night 0 95, Night 1 cracking 76 and straight face 76, Night 2 third skip 72 (cups 103 and one skip 61 once the officer's line was fixed), Grace at home night 80 and Thursday 22, the Break-ins 3 walk.

### Session 3 (`638b318`): the UI

1. **One skin** (`UiSkin`): the recap phone's paper and ink with the brand's green `#2E7D5B` (money, the sign's OPEN, the keys in a prompt), gold for warnings and stars, red for closing and quitting; one font swap point (`Resources/UI/UI font`, else the HUD's font: **still LiberationSans**, see the choices); one shadow (a shared TMP underlay); one corner radius; rounded sprites made in code. Every surface is on it: the prompt and the view's hint (keys in light green), the tabs, the Day 1 guide, barks, the conversation panel, tooltips, the night's notes (sized to their words) and the notebook page, the straight-face meter, the circuit's panel, the repair overlay, the drinks and counter captions, the "?" mark, and the phone. `UiClock` is the screen's own clock: it stands still while the phone pauses the game.
2. **The HUD's corners** (`HudCorners`, the HUD spec of 24 Aug, `claude/hud-spec.md`):
   - **Top right, today's takings as a cash stack:** a bill on top, an edge under it for every $24 (ten at most), the figure beside it. It empties each morning (the till's total is on the phone). A payment: a green "+$6" chip pops up under the stack and rises into it, the stack jolts, the bill drops and settles, a glow, the figure counts up and punches.
   - **Bottom left, the hanging sign:** OPEN, swaying; **LAST ORDERS** for the café's last hour (amber, a quicker sway, a soft amber pulse round the screen's edge; only a warning, arrivals still stop at closing); at closing it flips to **CLOSED** with a red glow that blooms and fades. Under it the day's bar (green, amber, red) and the day and the time.
   - **Top left, the tabs** (the ticket rail moved there). **Bottom right, stock chips**, only when cups or beans run low.
   - Hidden at night (the night's clock is top right) and behind the recap; the bottom corners step aside for a conversation or the counter phone. The old money, clock and stock lines are put away by day.
3. **Juice:** money pops in the brand's green with the tip in it ("+$6 +$3 tip"); the badges are bigger, with a rim coloured by mood, a bounce-in and a small burst on the heart and the star.
4. **The phone by day** (`PausePhone`, the recap's phone in a pause mode): **Esc, or Start on a pad, when nothing else is open**, brings it up and holds the game still (`Time.timeScale` 0, the screen's clock, the cameras' input; the pointer is free). Esc still steps back out of things first, and it won't come up over a close-up, a conversation, the recap, a night's fade or a scene's hold. Esc, Start, B or "Back to work" put it away, exactly as it was.
   - **Today:** what's earned so far, what's in the till, the café's stars, and the day's reviews as they come in (`ReputationLedger` now writes each review's card when the review happens).
   - **Notes:** the notebook, as at closing.
   - **Settings** (`GameSettings`, kept in PlayerPrefs): everything, music and effects volumes; look sensitivity and pad look speed; invert Y; aim help and movement help; quality Low/Medium/High.
   - **Quit:** says plainly that the day so far isn't kept (the game saves at Open Tomorrow), and asks again before quitting.
   - At night the phone opens on Notes, with Settings and Quit.
   - In first person Esc no longer frees the cursor: it pauses. The view's hint names the pause key.
5. **Sound:** three named cues (`hud.lastorders`, `phone.open`, `phone.close`), silent until the Sonniss pass. `Sound › Create or update the sound bank` added 39 cue slots that were already in the code but not in `Sound bank.asset` (additive). There is no mixer asset (`Assets/Audio/Game.mixer`), so the volumes scale the sound player's buses in code.
6. **Found by the labs and fixed:**
   - **The pocket watch's new mainspring pointed into the phone's prefab.** `Part_Extra_Mainspring` (made by the fault expansion from the phone's screen) kept the phone's "Fresh" as its new part: a mended watch showed no new part, and each mended watch switched on the phone prefab's own Fresh, in the asset, written to disk at the next save of assets (the stray `m_IsActive` in `PhoneRepair.prefab`, seen twice). `Fixit Fidget › Playtest › The pocket watch's new mainspring - give it its own (prefab)` made the watch its own (run once, committed); `Checks › Device parts keep to their own prefab (read-only)` guards it.
   - **The neighborhood guard** expected the 16 seats of 23 Sept at a 0.6 drain: the café has 21 since the furnishing pass, and the lounge's five drain at 0.55.
   - **The labs:** the recap phone check switches PlayerInput to its own pad (the UI's D-pad listens only to the devices PlayerInput has paired, and a pad made in code right after keys were pressed wasn't paired yet, so its first step was lost), holds each press three frames, traces a missed step, and stops instead of pressing A on Close up. The stations lab counts a device as taken back when it leaves Ace's hands (a customer still owed a drink stays in their seat).
7. **New lab:** `Fixit Fidget › Playtest › The phone by day - pause and HUD check (lab, drives itself)`: the corners, a payment, a low-stock chip, the phone at three moments (nothing moves, nothing expires, the view holds), the apps and Settings, Quit asking again, not over a close-up, last orders, closing. Photos in `Logs/Pause/`.

**Try it:** play a day and press Esc (or Start) at any time; walk into the café's last hour; let a customer pay. `Fixit Fidget › Playtest › The phone by day …` drives all of it.

**Checks (7 Oct, afternoon):** labs: pause and HUD 72/72 (`Logs/Pause/pause-2026-10-07_151741`), juice 31/31, recap phone 107/107, controller 28/28, aim help 23/23, stations first person 52/52 and overhead 53/53, Night 0 95/95, Night 1 straight face 76/76, Night 2 third skip 72/72, Grace at home night 80/80 and Thursday 22/22. Edit-mode checks all pass (Circuit rules 58,804; Support call 891,949; Reputation 19,709; Grace at home 1031; Night 1 492; Bark 505 + 709; Notebook 66; Home 40; Device parts 2 prefabs, 65 references) **except the neighborhood guard's occupied circulation**: 27 of 28 customer positions can be served with everything occupied; the front sofa's seat A can't, as since the lounge seats came in on 26 Sept (the realism pass recorded 24/28). Console tests: GraceRules 1031, NightRules 486 + 6, NotebookRules 62 + 2, HomeRules 38 + 62 + 2, BarkRules 505, ReputationRules 19,701 + 5. Compile: 0 errors, the two old CS0414 warnings.

**Choices made inside the plan (his to overrule):**

- **The font** is still LiberationSans, behind one swap point. The plan's mockup-and-pick step became my call under "build straight through"; the face I'd pick is **Baloo 2** (a rounded, free Google Font). It needs a download into `Assets/Fonts` and a TMP font asset; that's his to allow.
- The colours (the brand's green for money and OPEN, gold for last orders, red for closing).
- The volumes in code, not mixer parameters (there's no mixer yet).
- Quit doesn't save the day; last orders only warns; the paid chip rises into the stack from under it (at the top of the screen it went straight off it); the corners hide at night.

**Seen, not changed:**

- The neighborhood guard's front sofa (above): a café-layout question for session 6.
- `Content › 12 · Stability check` counts the lounge's 0.55 drain as wrong, and its fix (`11 · Fix waiting-spot drain rates`) would set them back to 0.6.
- In the overhead labs a customer's name floats mid-floor (the lab's pointer sits mid-screen); the conversation portrait shows its fallback ("G / Concerned") where no portrait is made.
- The stray `Assets/Scripts/Diagnostics/AceTurnCheck-1.cs` (and its .meta) is still not committed: Mansoor deletes it.

**Next:** the Blender session (the five tools, about 1,800 triangles; Grace's kitchen island and box stack, at least 1.1 m tall), then session 4 (the bench's feel and the cloth), 5 (cover and her house), 6 (the café), then the playtest together.

## October 7 (small hours) — Playtest 3, session 1: the words, the man's pull, the bins from any door (`2591594`); on `playtest-2`

**Read this entry first.** Three commits on `playtest-2` after `8ce5872` (chunk C's handoff entry): Mansoor's scene save on its own (`940c81f`), session 1 (`2591594`), and this entry. Nothing is pushed. The plan is `claude/playtest-3-notes-and-plan.md` in the project docs: §0 the scene, §1 Mansoor's notes after Days 1–3 with what each is in the code, §2 his four calls (all as recommended), §3 the order of the six sessions, §4 the writing rules, §5 this session exactly, with the before-and-after of every line, and §5.6 the second pass (what a review of the build found and what changed). The story doc has a new §16 (`claude/the-man-at-the-bins-story.md`).

**Mansoor's calls (6 Oct, late), all as recommended:** the man's pull is complicity and petty costs (never hard pressure); houses get the capsule and cover now and interior cells with the Town Pack; stations become reach, not rooms; words and the man first.

**Mansoor's scene save (`940c81f`, his change, committed alone):** 50 rooftop props that stood inside the city blocks are gone (17 `SM_Prop_Roof_Aircon_02`, 9 `SM_Prop_Water_Tower_01`, 6 `SM_Prop_SatDish_01`, 6 `SM_Prop_Roof_Aircon_03`, 5 `SM_Prop_Vents_Straight_01`, 4 `SM_Prop_Skylight_01`, 2 `SM_Prop_Roof_Aircon_01`, 1 `SM_Bld_Roof_Access_01`). With the save came Grace's way home serialized on `GraceAtHome` (`wayHome`) and one empty slot in `NightWalk`'s lit buildings (index 92 of 432): harmless, the night skips an empty entry and every other building keeps its bedtime.

**What changed (`2591594`):**

1. **A word budget, in code** (`WordBudget`, pure): a bark 7 words, a scene line 10, a scene 5 of its speaker's lines (Night 0's deal 8), a note or a notebook page 12, the phone's closing screen 40 before the review lines. The Bark rules fail on any line in the Night lines asset over it; the Night rules on any hint, note, lesson, page or question text; the Notebook rules on any line the notebook writes (her intake, her return, a regular's repair, a home sighting); the recap phone check counts the closing screen. One number each, in one place.
2. **Every placeholder rewritten to it** (`BarkSteps`: `Barks 1` reworded 16 lines and reshaped two scenes, each only because it was still exactly as made). The deal is eight lines; the gnome's return ends on **the hold** ("We've both got something on each other now. Mine's bigger."); the cups' return is three lines. Every note is one a beat and at most 12 words; the first night's keys come as a second note once the first has been read (`NightCycle.NoteThen`). Three of Grace's notebook lines were over the page budget and are cut (ids kept; **wording he approved on 27 Sept, his to put back**): "A camera with a scratched strap. Wants the strap left alone." · "Family reunion tomorrow. She might let someone put her in the picture." · "In the middle of the reunion photo. Left the shop a print."
3. **His pages say only what is in the game:** the parking page waits for the cones, the cups' page for her photos; the gnome pays "Grace is out every Thursday. Back at 1:30." The cups pay **Soft feet** (Ace's steps carry half as far: walking 2 m, sneaking 0.5 m; `AceFootsteps`) instead of Doors; Doors stays defined for saves that learned it.
4. **Stalling costs the day, by skips in a row, and stops the morning after the favour is done** (`NightLedger.CameHome`, `LodgerDay`, `DirtyCup`, `OfficerStory`, `CustomerSpawner`, `ReputationLedger`): the favour is never dropped now.
   - **Every skip:** he takes a table from opening to about 1:30 PM and says one line when Ace passes; on a morning without his mess, once he sits, a note: "The man at the bins took a table, waiting on his favour."
   - **From the second:** the café opens with his mess: one of the café's own paper cups, dregs in, on every table seat near the door (up to eight), each seat out of play. **E at any cup clears its whole table** (`MessTable`: spots within 1 m are a table), and a used-cup badge floats over each table until it's cleared (`Juice.Mark`, a new `Mess` icon). Note: "The man at the bins left his mess. Clear the tables."
   - **The third, once a favour:** a one-star review under "Anonymous" (−2 reputation, never a star back), the officer in that day with a harder question ("Someone says you keep odd company, Ace."), and his note on the counter ("Reviews are easy. Bring the cups.").
   - The save grows `messDay` and `wordDay` (additive; older saves load as before).
5. **The phone opens on one screen** (`RecapPhone`, a fifth app, **Tonight**, no tab bar): the weekday, the takings counting up, the café's stars, the reviews' count and what they did; the three review lines worth reading (`ReputationLedger.PickCards`); one notebook line under whose fact it is (`NotebookRecap.Tonight`; no NEW tag); the low stock only when true; Close up for the night; and More ("N new notes"), which opens the apps. Notes is a list, one line a person (`NotebookRecap.OneLine`). 35 words on the lab's Day 3.
6. **The bins from any door** (`NightZero`): with the bag in hand the step follows where Ace is, and holds still while a door blinks (`NightCycle.Blinking`).
7. **He follows the franchise:** written into the story doc (§16) as a rule for the franchise session; nothing to build yet.
8. **Small things from the review:** his cups no longer copy the drink prefab (that woke its scripts for a frame and leaked a material per part; they're built from the serving cup's shell, `DrinkLiquidVisual`'s builders, now shared); a seat knows which cup dirtied it (`TableSeat.IsDirtyWith`); a lasting badge keeps out of the way while Ace is talking, at a station, in the counter's repair view or inspecting, paused, or after closing.

**Try it:** `Night › Night 2 - Play check, stalling: the third skip (lab, drives itself)` shows a morning with his mess (the badges, one press a table), his note, his table and the officer's word; `...one skip, his table` shows the table note. `Night › Night 2 - Play from Day 2's recap (lab)` shows the new phone. In the real game: skip his favour one night and open the café the next morning, then skip again.

**Checks (7 Oct, all green):** console tests GraceRules 1031, NightRules 486 + 6, NotebookRules 62 + 2, HomeRules 38 + 62 + 2, BarkRules 505, ReputationRules 19697 + 5. Unity rule checks: Notebook 66, Night 1 492, Home 40, Bark 505 + 709 Night lines, Reputation 19705, Grace at home 1031. Labs: Night 2 third skip 72/72, one skip 61/61, cups 103/103; Night 0 95/95; Night 1 straight face 76/76; the recap phone 107/107; the juice check 28/28; Barks 2 43/43; Grace at home night 80/80 and Thursday 22/22. Compile: 0 errors, the two old CS0414 warnings.

**Choices made inside the plan (his to overrule):**

- Soft feet instead of Doors for the cups; Doors returns with the keys.
- The deal at eight lines rather than three: it is the reveal.
- The hold line's words; "Anonymous" as the review's name and −2 as its cost; 1:30 PM as the end of his table; a cup per seat near the door, eight at most, cleared a table at a time; the table note only on a morning without the mess (one note a beat); the officer's extra visit arrives like the morning's visitor and rides on his scheduled visit when he has one that day; the page comes without a note.
- A word is a run of characters with a letter or a digit in it: "·" and "›" between words don't count.
- The three Grace lines cut to the page budget (above).

**Seen, not changed:**

- Two orphan lines stay in the asset (`lodger.night0.04` "Grace likes you.", `lodger.night0.08` "And don't get caught.", and the cups' `r06`, `r07`): no scene says them; harmless, and his sister may want them.
- The stray `Assets/Scripts/Diagnostics/AceTurnCheck-1.cs` (and its .meta) is still not committed: Mansoor deletes it.

**Next:** session 2 of the plan (interaction: stations as reach, aim assist, the capsule and the crouch), then 3 (the UI skin), 4 (the bench), 5 (cover and the houses), 6 (the café).

## October 6 (late night) — Grace at home, break-ins chunk C (`0c55831`); on `playtest-2`

**Read this entry first.** Two commits on `playtest-2` after `c003d67` (the upright table reactions, wired with `NPC › Mixamo 3` once Mansoor had seen the photos): chunk C (`0c55831`) and this entry. Nothing is pushed. The plan is `claude/chunk-c-grace-at-home-plan.md` in the project docs (its "As built" section at the end), with `claude/break-ins-spec.md` §6.

**Mansoor's calls (6 Oct, night), all as recommended:** her night as proposed; getting caught before chunk E is a placeholder ("Caught.", the night ends, what was taken goes back, nothing else lost); weekdays from Day 1 = Monday, named in the HUD and the recap, and on Thursday nights she's out till 1:30 AM; the upright reactions wired.

**What changed:**

1. **Grace lives in her house at night** (`GraceAtHome`, new, on her rooms' root). `Night › Break-ins 6 - Put Grace at home (scene)` adds it (run once, scene saved); `Break-ins 6 - Take Grace back out` undoes it. Her own look (`Character_BusinessWoman`), placed at nightfall, walks set paths through her house (`GraceHouseMap`: 27 spots and the ways between them) and sits in her armchair with the café's sit. Her night (`GraceNight`, 48 s an hour):
   - 11 PM in her armchair, the TV on;
   - 11:35 the kettle (the kitchen light goes on as she steps into the kitchen and off as she leaves it);
   - 11:45 back to her chair with her tea;
   - midnight up the stairs (the landing light), into her bedroom, the doors shut behind her, her bedside lamp, into bed (the slept-in quilt `GH_Quilt_Asleep` takes the made one's place);
   - asleep about 12:20 (her lamp off);
   - 2:40 a glass of water (lamp, doors, landing and kitchen lights, the sink, back up, doors shut), asleep again about 3:05.
   Move the clock (the labs do) and she is wherever her night has got to.
2. **The street reads her lamps.** Her front window's curtains glow while the front room's lamp is on, and the TV flickers a cool light (`The TV's light (night)`, added by the step). A bedroom bay lights with her bedside lamp: `NightHomes.Drive` hands her windows to her instead of the city's guesses.
3. **Her eyes, her ears and her mark** (the spec's numbers):
   - she sees in 110°, 7 m in a lit room and 3 m in a dark one; watching TV, only the 60° toward the screen and at half the rate; sneaking halves the rate, the torch doubles it; walls and furniture block her view;
   - she hears steps (4 m walking, 1 m sneaking), the cups' rustle (3 m), the creaky tread on the upper flight and her doors opened at a walk (5 m), all straight-line;
   - a "?" badge beside her head (`NoticeMark`, drawn in code) fills as she sees or hears Ace. At a third she says "Hm?" and looks; from a half she comes to look where she noticed Ace, looks round for 3 s and goes back; full, she has caught Ace;
   - asleep she sees nothing; a loud sound within 5 m wakes her for 20 s (lamp on, "Hello?").
4. **Hiding** (`HidingPlace`): the cupboard under the stairs and her wardrobe, E in and E out. Ace's body and capsule are set aside and a note says so; she never opens them.
5. **Her bedroom doors** (`BedroomDoorsZone`) shut behind her at bedtime and after her water. E opens them: sneaking, slowly (2 s) and quietly; at a walk, at once, and the creak wakes her.
6. **Caught, the placeholder** (`NightCycle.Caught`): "Ace?! What on earth—", the screen fades to "Caught." ("Whatever Ace took tonight goes back. For now that's all: the cells, bail and the papers come later."), and the night ends there. What Ace took that night goes back: off the shelf, back where it was, crossed out of the notebook (`NightLedger.PutBack`, `Notebook.Forget`). Things from earlier nights stay.
7. **Weekdays** (`Weekdays`): Day 1 is a Monday. The HUD reads "Thursday · Day 4" by day and "Thursday night" by night; the dusk and morning captions and the recap say it too (the phone's kicker reads "THURSDAY · DAY 4"; its "· CLOSED" is dropped to make room).
8. **Her Thursday:** out all evening (the house dark at 11 PM). From 1:00 AM she's on her way: she appears at the nearest point of her way home that the camera can't see (up West Street, on the street side of the pavement, clear of the stoops' railings and the signal post at her corner), timed to be in at her front door at 1:30. Then tea, bed at 1:50 and no water at 2:40.
9. **Sounds named** in `SoundBank` for the Sonniss pass (the TV, the kettle, the tap, her lamp, doors, bed, "Hm?", woken, caught, the creaky tread, the doors' creak, hiding); silent until it has files.
10. **Found and fixed while checking:**
    - `Barks` is made on first use, and one made in the middle of a night took its first frame for nightfall and cleared the line that made it: her first "Hm?" never showed. It now starts knowing whether it's night.
    - Her first way home ran through a railing and the signal post at her corner (she walks by setting her position, so nothing stopped her). The Thursday check now walks her way with a capsule her size and lists anything solid on it.
    - The checks: the Night 2 check now waits on her stoop until she's asleep and sneaks in; Ace's walk through her house (`Break-ins 3`) runs on a night she's out.

**Try it:** `Night › Break-ins 7 - Grace at home: her night and her eyes and ears (lab, a check)` drives itself through all of it and leaves you in the lab; `Break-ins 7 - Grace at home: her Thursday (lab, a check)`. To play it: `Night › Night 2 - Play from Day 2's recap (lab)`, "Close up for the night", take the bins out, then go to her house (12 West Street): watch her windows and go in after 12:20.

**Checks:** Grace at home 80 of 80 (`Logs/Night/grace-at-home-night-2026-10-06_173057`) and her Thursday 22 of 22 (`grace-at-home-thursday-2026-10-06_172809`; in at her door at 1:28); Grace at home rules 1031; Night 2 100 of 100 (`night-two-check-cups-2026-10-06_173435`; asleep 52 s after Ace reached her stoop); stalling: his note 46 of 46, his visit 55 of 55; Night 0 89 of 89; Night 1 76 of 76 twice; Barks 2, 43 of 43; juice 24 of 24; the recap phone 84 of 84; Ace's walk through her house all clear; Night 1 rules 422; the console tests (GraceRules 1031, NightRules 416, NotebookRules 50, BarkRules 501, HomeRules 38, ReputationRules 19,691) pass; compile 0 errors, the two old CS0414 warnings. The HUD fits "Wednesday · Day 3", the longest day.

**Choices made inside the plan (his to overrule):**

- Sounds alone take her mark to two thirds at most: only seeing Ace catches him.
- She comes to look from a half (two sounds, or a sound and a glimpse).
- "Close up" in her sight is 1 m: the fastest notice, 1 s.
- The creaky tread carries 5 m at a walk and 2.5 m sneaking (her pillow is 4 m away).
- While she's up for water the made quilt shows (a thrown-back quilt would need a model).
- She's asleep about 12:22–12:25, not on the stroke of 12:20: her lamp goes off 4 s after she's in bed.
- The notebook learning her Thursdays waits for chunk D.
- On a Thursday, someone standing in the middle of the pavement at her steps stops her: she waits for him to step aside (she doesn't notice Ace on the street in this chunk).

**Seen, not changed:**

- By the spec's numbers, Ace standing still a metre to her side while she watches TV isn't seen (outside the TV's 60°). Walking there, his steps give him away. The first numbers to tune, after Mansoor plays it: the TV's slice, the rustle's 3 m, coming to look from a half.
- Her component in the saved scene still carries the old `homecomingWalk` field (renamed `wayHome`); it goes the next time the scene is saved. Harmless.
- Ace's walk check (`Break-ins 3`) stops Play by itself when it's done, unlike the other checks. Pressing Play after it starts an ordinary session on the real playtest save. It happened once this session: about 40 s on that save's Day 3 recap with nothing pressed; loading a recap doesn't write the save, so it's untouched.
- The Synty Package Helper asked about Shader Graph again after a reload: skipped, nothing installed.
- The stray `Assets/Scripts/Diagnostics/AceTurnCheck-1.cs` (and its .meta) is still not committed: Mansoor deletes it.

**Next:** Mansoor plays her night (and a Thursday: Day 4's night) and says how it feels. Then chunk D: the stash and the secret (the notebook learns her Thursdays there).

## October 6 (night) — Playtest fixes and the first juice pass (`989f043`); on `playtest-2`

**Read this entry first.** One commit on `playtest-2` after `0dfe903`, and this entry. Nothing is pushed. The notes are in the project docs: `claude/session-2026-10-06-notes.md` (the night section) and `claude/the-man-at-the-bins-story.md` §15.

**Mansoor's playtest (6 Oct) and his calls:** "the door inside the cafe that goes out towards the bin is off ... a side pillar that's going from the wall to hitting the door"; in first person "you can see inside the head" of the gnome; after Days 1 and 2 the man stands out of the dumpster all night, where he liked his head coming out when the bag went in; Grace stands in her doorway when Ace goes for the cups; everyone reacts the same way ("a little leaning in the chair"); more juice. His calls: move the door and trim the ledge; the man ducks down after his say and pops up again; juice in code now, the sounds later with the Sonniss files; these fixes and the juice first, then chunk C.

**What changed:**

1. **The back door's corner** (`Night › Bins 2 - Give the back door room (Edit Mode)`, run once and saved). The "pillar" was the courtyard window's cream sill, 0.26 m deep, running the whole east wall into the door under its handle. It now stops where the glass does, at the plaster pier (a cut copy of its mesh, `Assets/Art/NightZero/Courtyard sill - stops at the glass.asset`, used by the scene and the night's collision list; the street's geometry asset is untouched). The door moved 0.27 m along the wall, leaving 0.30 m of wall to the corner (the coffee sacks stop it going further: they reach x 6.14, the frame starts at 6.16). Round it: the chalkboard 0.31 m left, the cat picture onto the pier facing the room (0.58 m from the back wall, so it doesn't fade with it), the loose coffee sack under the window ledge, the clock centred over the door; Ace's places each side checked for room again. `Bins 1 - Take them out again` puts all of it back. Photos: `Logs/Night/bins-door-room-2026-10-06_142131`.
2. **The man pops up and ducks back** (`Lodger`, `NightZero`). From nightfall he's hidden in the dumpster, its lid shut, every night. When the bag goes in he pops up: a quick pop, a peek over the rim, or a slow rise, by night (Night 2 peeks). After his say he ducks back and the lid drops; he pops up again when Ace comes back within 4.5 m with what he asked for, and ducks 1.6 s after the return. Walk off more than 7 m for 2.5 s and he ducks too. His lines in passing come from inside the bin while he's down (the lid rattles).
3. **Nobody left on a doorstep at night** (`CafeArrivals.HomeForTheNight`, called from `NightWalk`'s nightfall). The day's last visitors on their way home were frozen where they stood when night fell; Grace stood in her doorway all of Night 2. Now nightfall sends them all home (and the cars back to the pool).
4. **Barnaby's hat** was built inside out (one closed shell facing in). `Tools/Blender/fixit_blender.py` turns any closed shell that faces in (`orient_shells`), and `Barnaby.fbx` and its `.blend` are rebuilt. In first person he's held low on the right, looking back at Ace (`NightCarry`).
5. **Reactions** (`NpcBeats`). Served, or handed a good repair, a person picks from a few reactions, less likely to repeat their own last ones or the room's last two. Upright versions of the sitting thumbs up and laugh are baked (`NPC › Mixamo 1`; back held as the breathing idle holds it) for tables, where the sofa versions lean into the table. **Not wired yet:** Mansoor sees the photos first, then `NPC › Mixamo 3` wires them. Until then a table picks between the two talking clips.
6. **Juice** (`Juice`, new; all drawn in code, nothing added to the scene): badges over heads (a heart for a perfect repair, a star for a good one, a cup for a drink, a tick when reassured, dots for so-so, a grey cloud for unfixed, a sweat drop when frustrated, a red burst when furious or walking out); "+$6" and "+$3 tip" over whoever pays; sparks when a part goes in or grime comes off; a big sparkle and "Fixed!" when a repair is finished on the bench; what Ace hands over flies to them in an arc; the money in the corner counts up with a bounce and a flash (`ShopUI`); new tickets drop onto the rail (`TicketRailUI`); the recap's takings count up (`RecapPhone`). Their sounds are named in `SoundBank` and stay silent until it has files.

**Try it:** `Fixit Fidget › Playtest › Play the whole game from Day 1 (test save)`; `Night › Night 2 - Play from Day 2's recap (lab)` for the man's peek. `Playtest › Juice - Photograph the feedback (lab, Play Mode)` shows and photographs every kind of juice.

**Checks:** Night 0 89 of 89 (`Logs/Night/night-zero-check-2026-10-06_144332`, with photo 09b: Barnaby in first person); Night 1 76 of 76 twice (`night-one-check-straight-face-2026-10-06_144719`, `night-one-check-cracking-2026-10-06_145003`); Night 2 97 of 97 (`night-two-check-cups-2026-10-06_145241`); stalling: his note 46 of 46, his visit 55 of 55; Barks 2, 43 of 43 (`barks-check-2026-10-06_150458`); the juice check 24 of 24 (`Logs/Juice/juice-check-2026-10-06_144027`); the console tests (NightRules 407, NotebookRules 50, BarkRules 501, HomeRules 38, ReputationRules 19,691) pass; compile 0 errors, the two old CS0414 warnings.

**Seen, not changed:**

- After the stalling visit check had passed, the lab logged one "[Deshawn] couldn't reach the counter and gave up". Nobody serves in that lab, so the queue fills; not seen elsewhere.
- The Synty Package Helper asks to install Shader Graph for the Sidekick Character Creator after some script reloads. Skipped each time (nothing installed).
- In first person Barnaby's face sits at the bottom edge of the view. Raise or shrink him if Mansoor prefers.

**Next:** Mansoor looks at the upright reactions' photos (then `Mixamo 3`), then chunk C: Grace at home (her routine, sight and hearing, her mark, hiding, her bedroom doors).

## October 6 (evening) — The favours, stalling and the officer (`234eedc`); on `playtest-2`

**Read this entry first.** Two commits on `playtest-2` after `e9b3f8f`: session 3 of the foundation pass (`234eedc`) and this entry. Nothing is pushed. The build note is in the project docs: `claude/session-3-favours-stalling-officer.md` (with `claude/the-man-at-the-bins-story.md` and `claude/night-0-and-the-favours-spec.md` for the design).

**Mansoor's calls (6 Oct):** Night 2's cups are takeable now, so Night 2 plays end to end; the officer comes this session (Day 1's coffee and joke, Day 3's question, the man's cop line on Night 0). Every line is still a placeholder.

**What changed:**

1. **Every night after the deal opens at the bins** (`NightZero.Ritual`): the back door and the bag, "Take the bins out", "Bin it" (no reveal: he is already standing in the far half). Then a held scene: his verdict on the day just ended (a straight face held or cracked; from Day 3, the officer's question kept or flinched at), and tonight's scene: the ask for the next favour with Ace's two replies (warm, he says why first), the ask again (colder, after a skip), a night off (Nights 4 and 7), or "Nothing tonight" when the next favour isn't in the game yet (Night 3's cones). Then a note says where the thing is.
2. **The favours as data** (`LodgerStory.Favours`: the gnome, the cups, the cones), each with its ask, return, lesson, page, Ace's line, note and corner. `NightLedger` keeps the favour, the nights it was asked, skips, the visit and note days, dropped favours and the officer's questions (`SaveData`: added fields, no version bump; an older save picks up where it was).
3. **Night 2, Grace's cups** (`NightThings.CupsOfGrace`): a sleeve on her reunion cups' box on her kitchen worktop. Taking needs line of sight (`NightTrophy.needsSight`), so never through her kitchen wall. In hand back to the bins, "Give him the reunion cups": he sets four paper cups out on his crate one by one, teaches **Doors** and gives a page (her photos). Day 3: Grace tells Ace about her cups (a harder meter; Nerve helps).
4. **Stalling, never a fail state** (`LodgerDay`): a night that ends without the favour he asked for is a skip. The next morning he comes into the café a quarter of the way through the day, on foot in his suit, sits a minute, orders and pays nothing, and says one line when Ace passes (warm, plain or cold). The third skip in a row: his note on the counter instead (on screen, and in the notebook as one of his pages), and the next favour comes up.
5. **The officer** (`OfficerStory`; `Night › The officer 1` makes `Assets/Data/Regulars/Regular_Officer.asset` once and never overwrites it): a regular in `Character_Male_Police`, added to the café customer's looks and kept for him. Coffee, black. Story visits on the open scene's own days (`DayDefinition.storyVisits`): Day 1 at 0.35 (the playtest scene's Day 1 is Grace's camera day, `Assets/GraceShowcase/Day_01_GraceCamera.asset`) with the joke about the second batch; Day 3 at 0.25, when after his order he asks "Have you seen a man of this description?" and the meter runs as "Say nothing" ("Said nothing." / "You flinched."). A flinch makes him one step more suspicious; his description goes in the notebook either way.
6. **The deal ends with the cop line** ("That cop who drinks your coffee? He's looking for me." / "Keep him happy.").
7. **The set:** `Night › Night 2 1` puts "25 - Night 2 (Grace's cups)" in the scene (the sleeve on her box, his crate by the trash can with four cups switched off, a place for the cups on Ace's shelf). The scene is saved with it.
8. **Found and fixed while checking:**
   - `The officer 1` had put his Day 1 visit on `Day_01_LearnTheShop`, which no scene plays (the playtest scene's Days 1 and 2 are Grace's showcase copies). It now uses the open scene's own schedule, and `Day_01_LearnTheShop` is back as committed.
   - The Night 0 and Night 1 play checks took any trophy for the gnome; with the cups in the scene the Night 0 check found the cups and walked into her kitchen. They find the gnome by its id.
   - The night's note sent Ace to where the thing had been even when it was already on Ace's shelf (a night that ended with it in hand, or taken before he asked). It now says "He wants what's already on Ace's shelf, behind the counter: …". On a night off it says nothing of errands.

**Try it:** `Fixit Fidget › Night › Night 2 - Play from Day 2's recap (lab)`, then "Close up for the night". `Night 2 - Play check (lab, drives itself)` runs Night 2 and Day 3; the two "stalling" checks run a skip and the third skip. Or `Playtest › Play the whole game from Day 1 (test save)`.

**Checks:** Night 2 play check 92 of 92 (`Logs/Night/night-two-check-cups-2026-10-06_113830`); stalling, his visit 52 of 52 (`night-two-check-visit-2026-10-06_111158`); his note 43 of 43 (`night-two-check-note-2026-10-06_111645`); Night 0 84 of 84 (`night-zero-check-2026-10-06_112842`); Night 1 76 of 76 twice; Barks 2, 43 of 43; Night 1 rules 413, Bark rules 501 + 585, Notebook rules 54; the console tests (NightRules, NotebookRules, BarkRules, HomeRules) pass.

**Seen, not changed:**

- **Doors** is learned and noted but does nothing yet; its edge (spare keys in the notebook) comes with the keys in chunk D. Recommendation for Mansoor: make Night 2's lesson "The straight face" instead (it would widen the officer's meter the very next morning) and move Doors to the keys. A two-line data change.
- Night 3 is "Nothing tonight" until the cones exist (sessions 4–6).
- `City pack › NPC looks 3` would take the police look off `Customer.prefab` again: run `The officer 1` after it.
- In the "his note" check's made-up save (asked twice, the notebook never told), the note reads "A sleeve of cups are on…". In play the first ask always names them, so it reads "The reunion cups are on…".
- The stray `Assets/Scripts/Diagnostics/AceTurnCheck-1.cs` (and its .meta) is not committed: Mansoor deletes it.

**Next:** Mansoor plays Days 1–3 (Night 0, Night 2, Day 3's officer). Then sessions 4–6 of the plan: chunks C and D (Grace at home), and Night 3's cones.

## October 6 (later) — Night 0 and the man at the bins (`2638fe6`), and Ace's body out of the counter close-up (`8e34269`); on `playtest-2`

**Read this entry first.** Three commits on `playtest-2` after `dda7316`: session 2 of the foundation pass (`2638fe6`), a fix its check photos turned up (`8e34269`), and this entry. Nothing is pushed. The design is in the project docs: `claude/the-man-at-the-bins-story.md` (the night as a story, Mansoor's four calls, and §13 as built), `claude/night-0-and-the-favours-spec.md`, `claude/foundation-pass-build-plan.md`.

**Mansoor's calls (6 Oct):** the story is a mystery with a heart; Ace answers and comes back to the bins, and the man keeps a corner of what Ace brings; a favour pays a lesson or a secret, never money; the night camera fix goes into this session. The café's green counters are left alone (he'll buy a town and café pack).

**What changed:**

1. **Night 0** (`NightZero`). Until Ace has met the man, the first night opens just inside the café's new back door with the bin bag in hand; the night's clock waits and only the bins' own things are offered. "Take the bins out" is a blink through the door onto Back Street. "Bin it": the near lid lifts, the bag goes in, the lamp flickers, the view pushes in, the far lid creaks up and he stands up out of the dumpster (5.3 s). The deal is a held scene: his pages go into the notebook on its hand-over line, and his errand is Grace's gnome. Calling it a night waits for the deal.
2. **The man at the bins** (`Lodger`, `LodgerStory`): one body (the café patron's, in a suit nobody else wears; the walk-ins' outfit picker comes off it), barks pinned to his head, a line now and then as Ace passes, standing in the dumpster all night once met. The return: "Give him Barnaby" plays the return scene; the gnome leaves Ace's hand for his corner (the trash can's lid, at night), he turns it to face the street, teaches **Nerve** and gives a page ("Grace. Thursdays. Find out."), then the hook: "Tomorrow, her kitchen."
3. **Ace answers** (`Barks`, `NightLines`): a held scene can have choices. Ace's two replies come up as chips beside the line they answer (1 / 2; X / Y on a pad), and each nudges the man's hidden warmth (±5). A reply never changes what happens. The press that ends a held scene can't also call it a night.
4. **What a favour pays:** Nerve widens the straight face's green 1.45× (`MorningFace`; the meter says "Nerve"). His pages are the notebook's new "inherited" source, laid out first, in italics.
5. **Carrying at night** (`NightCarry`): the bag, then the gnome, in Ace's right hand. A night ended with the gnome in hand puts it on the shelf, and "Take Barnaby off the shelf" gets it back for him.
6. **The record** (`NightLedger`, `SaveData`; added fields, no version bump): met, warmth, lessons, given. A given thing is still Ace's deed, so Grace's morning is the same.
7. **The night camera** (`NightSeeThrough`): it also fades what stands between the camera and the ground 6 m and 12 m in front of Ace, so the terrace south of the car park no longer covers the lower half of the screen.
8. **The set:** `Night › Bins 1` puts "24 - The bins (Night 0)" on Back Street (the back door, POLYGON City's Skip_02 with its lids cut free into `Assets/Art/NightZero`, a lamp, a trash can, a reveal camera, places to stand checked for room). It moves the pot plant and the chalkboard and turns the rear lane's walker back at x 1.6. The scene is saved with it; `Bins 1 - Take them out again` undoes it.
9. **Ace's body out of the counter close-up (`8e34269`).** Since the body came (29 Sept), the conversation camera behind the counter had been looking through the back of Ace's shirt: half the screen in every counter conversation, Grace's morning included. `AceBody` now draws the body only while the overhead view is on screen, and after a close-up only once the camera is 3 m clear.

**Try it:** `Fixit Fidget › Night › Night 1 - Play from Day 1's recap (lab)`, then "Close up for the night": Night 0 opens at the back door. `Night 0 - Play check (lab, drives itself)` runs all of it.

**Checks:** Night 0 play check 84 of 84 (`Logs/Night/night-zero-check-2026-10-06_070846`); Night 1 play checks 76 of 76 twice, with Night 0 skipped (after the fix: `night-one-check-straight-face-2026-10-06_070333`); Barks 2, 43 of 43; Grace's walk all clear (`grace-walk-2026-10-06_070612`); Bark rules 501 + 359, Night 1 rules 314, Notebook rules 54; the console tests (NightRules, NotebookRules, BarkRules, HomeRules) pass.

**Seen, not changed:**

- At night the editor logs "Reduced additional punctual light shadows resolution…" when two shadowed lamps are in view (from before this session; information only).
- The night walk measures about 180 B of garbage a frame in the editor, the same near the man and far from him, and with or without barks (Barks 2 measured the same at 00:51). Worth a look in a build in the performance pass.
- In the checks, nightfall's note is still on screen for the first seconds of the reveal, because the check walks straight to the dumpster.

**Next:** Mansoor plays Night 0; then session 3: the favours as data (each return's line, lesson and secret), stalling, and the officer.

## October 6 — The lighting pass (`4104e06`), the house kit, barks, and Ace running straight; on `playtest-2`

**Read this entry first.** Four commits on `playtest-2` after `4104e06`: the house kit, barks, Ace running straight, and this entry. `4104e06` (5 Oct, evening) went in without an entry and is summed up here too. Nothing is pushed. The plans are in the project docs: `claude/house-interiors-plan.md` (§9–§12), `claude/furniture-library.md` ("The house kit v1"), `claude/foundation-pass-build-plan.md` (the foundation pass: its order, and barks in §5), `claude/session-2026-10-05-notes.md` (the walking report).

**Mansoor's calls:** he dresses the houses himself from the kit ("I can do that myself"); the code next, foundation first; barks pinned to the speaker on screen; Night 0 inside Night 1; the story picks of the plan's §9; for the walking, "quick turn + legs step", and the clips straightened if they were turned (they were).

**What changed:**

1. **The lighting pass (`4104e06`).** `Night › Break-ins 5`: while Ace is inside a house at night, the ambient eases down to 0.35 (`CafeDaylight.indoorAmbient`), so the lamps read as pools; her lamps cast soft shadows; a warm light under the kitchen cupboards; the curtains glow toward the street only and wear an oat fabric into the room; the clapboard lips' backs are cut out of her window. `Fixit Fidget › Recovery` removes the Sidekick tool's stray "Combined Character" whenever it comes back (`SidekickPreviewCleanup`). Before and after: `Logs/Night/house-close-ups-2026-10-05_031614` and `_104811`.
2. **The house kit v1** (`Tools/Blender/house_kit.py`, built headless in Blender 5.2; source `BlenderSource/HouseKit_v1.blend`): 42 pieces, 7,484 triangles, in `Assets/Art/Models/HouseKit` with their materials and the four paintings. `Night › House kit 1` imports and checks them (85 of 85); the café scene is never touched. Mansoor places them by hand.
3. **Barks** (`Barks`, `BarkRules`, `NightLines`; `Night › Barks 1` and `Barks 2`, `Checks › Bark rules`): short lines pinned over whoever says them, the same size at any zoom, clamped to the edge with an arrow when the speaker is off screen, heard within 12 m, throttled (a speaker's 20 s, 4 s between anyone's), pools that never repeat early, and scenes that can hold Ace (`PlayerMovement.Hold`; `PlayerInteractor` stands aside so E belongs to the scene). The cast and placeholder lines live in `Assets/Data/Resources/Night lines.asset` (Barks 1 adds what's missing and never changes a line that's there). Checked: the rules 501 + 235 assertions; Barks 2, 43 of 43, no garbage a frame (`Logs/Night/barks-check-2026-10-06_005125`). Nothing says a bark yet in play: Night 0 is the first user.
4. **Ace runs straight.** Two causes, both measured. (a) The Humanoid copy's "Body Orientation" root takes the body's facing at each loop's first frame, so on Ace the jog ran 27.9° off his forward (the sprint 19.8°, the walk 6.6°). `AceSidekickSteps` now straightens every clip (`Straighten`: measure on Ace, set the Root Transform Rotation Offset, re-import, measure again; all 0.0°), and Ace's body 4 and 5 were run again and the scene saved (the jog's natural speed now reads 5.69 m/s, so it plays at 0.88×). (b) On a flick, the body swung round at a flat 720°/s with the run still playing, so for 0.158 s he ran side-on; `AceBody` gains Quick Turn (20) and Pivot Step (the legs step from 25° to 55° off). `Night › Ace's body 6` (`AceTurnCheck`) measures it with a virtual controller, before and now: 32 of 32 (`Logs/Night/ace-turn-check-2026-10-06_010238`). Grace's walk check is all clear (`grace-walk-2026-10-06_010452`).

**Seen, not changed:** at the night camera's home and far zoom, with Ace on the front street, the roof of the building south of the car park fills the lower half of the screen (since the home view turned to 0°; `barks-check-2026-10-06_005125/1-overhead-home.jpg`). The see-through only fades what stands between the camera and Ace. Raised with Mansoor; nothing changed.

**Next:** session 2 of the foundation pass (Night 0 at the bins, Night 1 as the man's errand), once Mansoor has seen the elevated story; then the plan's table, one session at a time.

## October 5 (later) — Playtest 3 settings (the home view at 0°, sneaking at 1.2 m/s), the slop audit's photographer, scripts changed from outside; on `playtest-2`

**Read this entry first.** One code commit on `playtest-2` after `a77eb44`, with this entry in it; nothing is pushed. **The scene is not in this commit** (see "Seen, not changed"): it is saved on disk with the two Playtest 3 settings, and it will be committed once the stray Sidekick character is out of it. The design work of the day is in the project docs: `claude/house-interiors-plan.md` (the audit's findings and the route A kit, §8–§11), `claude/night-0-and-the-favours-spec.md` (§11, the man follows the franchise), `claude/franchise-ready-cafe.md` (§4.1 called).

**Mansoor's calls today:** the overhead home view turned square to the counter, 0° (his pick of the three renders; it also cures "the stick never walks straight", which was the 25° view's screen axes lying diagonal to every wall); the sneak speed 1.2 m/s ("whatever feels and looks right"); the Town Pack later, route A now (the finish layer, lighting and a reusable prop kit on the houses as they are); franchising is "Ace moves on" and the man follows to every city.

**What changed:**

1. **`Playtest3Steps`** (`Fixit Fidget › Playtest`): *Camera home square to the counter, 0 degrees (scene)* turns `CmShopCam` round the café's focus at the same tilt and distance (and *… back to 25 degrees* undoes it); *Sneak speed 1.2 m per second (scene)* sets `PlayerMovement.sneakSpeed`. Both applied and the scene saved. At 0°: controller check 29/29 (the stick 4° off straight up walks straight up the screen), cut-away check 33/33 (`Logs/CafeWalls/cutaway-2026-10-05_032526`), Grace's walk check all clear with the crouch walk at 1.57× (`Logs/Night/grace-walk-2026-10-05_032739`).
2. **`HouseCloseUps`** (`Fixit Fidget › Night › Slop audit - photograph the houses up close at night (lab, read-only)`): a night lab on the test save in which Ace is moved in first person to every street door (3 m), every front (8 m), eight spots in Grace's house and four doors with the torch on, and each is photographed the same way every run: `Logs/Night/house-close-ups-<time>/` with a report. The "before" set is `house-close-ups-2026-10-05_031614`. Known: the first photo of a run is taken before the first-person camera has arrived (a longer first settle next time); house 4's door shot is that photo.
3. **`ReimportChanged`** (`Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)`): Unity's refresh prunes by folder time, and a script rewritten in place (how Claude's files arrive) in a folder where nothing was added or removed is **not** picked up by Ctrl+R or Assets › Refresh (`Editor.log`: "RefreshV2 … 0.005 seconds"; seen twice today, a stale assembly ran a lab). The menu reimports every script newer than its own assembly (runtime scripts against `Assembly-CSharp.dll`, editor scripts against `Assembly-CSharp-Editor.dll`). Also seen: a reimport touches the script's file time, so one run can list a script twice; harmless. The untracked `.touch` files in `Assets/Editor` and `Assets/Scripts/Diagnostics` were the first workaround (a new file changes the folder time); they can be deleted.

**Seen, not changed:** the scene file on disk (`AcesCafeLayoutPlaytest.unity`, saved 02:58 with the two settings) carries a **`Combined Character`** at its root: 92 objects and an embedded mesh, 9 MB, the Synty Sidekick Character Tool's preview character, which the tool's docked window put into the scene during a script reload (it is not in `a77eb44`, nor in the 02:24 copy of the scene). Not committed: the scene waits for Mansoor's word to delete it (one object in the hierarchy) and for the Sidekick window to be closed while labs run. The scene in the editor was also marked dirty after Play today, most likely by that window; it was not saved.

**Next:** Mansoor's calls on the house kit, the lighting pass and the stray character (`claude/house-interiors-plan.md` §11); then the lighting pass and the kit; then the foundation pass (`claude/night-0-and-the-favours-spec.md` §9) once §10 is called.

## October 5 — The 30 Sept evening's code lands: sneaking (break-ins chunk B), the NPC bodies and the walls cheaper, the deprecation warnings gone, builds keep their own save; on `playtest-2`

**Read this entry first.** One code commit on `playtest-2` after `0afef60`, with this entry in it. Nothing is pushed. The design work of 1–5 Oct (the foundation story, the house interiors, the franchise plan) is in the project docs, not here: `claude/foundation-the-fixers-notebook.md`, `claude/night-0-and-the-favours-spec.md`, `claude/house-interiors-plan.md`, `claude/franchise-ready-cafe.md`.

**What happened between the two commits.** The 4K test build of 30 Sept was never closed by Alt+F4 (the key never reached it); it ran Day 3 to 8 PM unattended and saved over the playtest save. Then Mansoor ended the build himself and played Days 1–2 again that afternoon, so the playtest save is his own again (Day 2 closed, 1 star, $29 after $350 of upgrades at the recap). `Fixit Fidget › Recovery` (`TestBuildCleanup`) exists for the next time: close a build in `Builds/`, list the saves, put a save back only when the files are exactly the case it was written for. The code written that evening was parked in `Claude outputs/pending-2026-09-30/` and compiled today, first time, with no errors.

**What changed:**

1. **Sneaking, break-ins chunk B** (`PlayerMovement`, `AceBody`, `AceFootsteps`, `CafeViewMode`, `ControlHints`, `ShopUI`; `NightNoise`, new; `AceSidekickSteps` gains `Night › Ace's body 5 - Sneaking: fit the crouch clips to Ace (scene)`; the scene saved with AceBody's crouch fields).
   - At night, **held Ctrl or C** sneaks (on a pad the **left stick's click** switches it on and off; holding a stick down while steering is a cramp). Sneak Speed 1.6 m/s (the spec's number), the crouch eased over 0.25 s, the first-person eye 0.55 m lower; the capsule never changes (call 1). By day it is off (C switches hands in the café); `Sneak By Day` on PlayerMovement turns it on for a lab.
   - The Sidekick body blends into the library's crouch clips (`Crouch_Idle_Loop`, `Crouch_Fwd_Loop`, already in the Humanoid copy) on two more mixer inputs; the stand-in has none and sneaks upright. Ace's body 5 measured the crouch walk at 0.76 m/s on Ace and Ace crouched at 1.24 m (standing 2.04).
   - **Tuning flag:** at 1.6 m/s the crouch walk plays at 2.0× its own pace (clamped), which reads as hurried; 1.2 m/s would play at 1.6×. The walk check marks this as its one FAIL on purpose until Mansoor picks the speed (PlayerMovement › Sneak Speed in the Inspector, live).
   - `NightNoise` (new): every noise Ace makes at night, with the spec's radii (walking 4 m, sneaking 1 m, lock pick 4, rustle 3, creaky stair 5), as one event anyone listening can take (Grace, chunk C). Footsteps raise it; sneaking steps are shorter and a third as loud. The HUD's night line reads `Ctrl  Sneak    F  Torch    N  Notebook`.
   - `GraceHouseWalkCheck` grew a sneaking section (the longest clear way from where the walk ends: sneak along it, stop crouched, first person crouched and standing, walk back): all PASS but the pace flag.
2. **Performance leftovers** (`PolygonNpcVisual`, `CutawayWall`, `SeeThroughMaterials`, `CafeViewMode`, `NightWalk`).
   - A body's skeleton is read once per spawn: Unity's `renderer.bones`, `mesh.bindposes` and every `Transform.name` hand back fresh copies, and binding read them per bone (~0.4 MB of garbage per arrival). Bind poses are cached per mesh for the Play session.
   - A body nobody can see isn't posed; above `Max Follow Rate` (120 fps) the café's people take turns (every other or third frame); Ace's own stand-in is posed every frame (`EveryFrame`). `LateBehaviourUpdate` fell from ~0.50 ms to ~0.19 ms a frame in the performance check (same scene, same phases); averages 4.2–4.4 ms from 4.4–5.1.
   - The cut-away walls search the scene once (kept until the night starts or ends, or the furnishing tools reset it; `ForgetCandidates`/`PrepareCandidates`), one wall re-looks per frame, `RescanAfter` 30 s, and `CafeViewMode` readies every wall and its see-through copies in the frames after loading (`Prepare`), which was the ~10 ms hitch on the first-person/overhead switch.
3. **Builds keep their own save** (`SaveManager.BuildFileName`): a built player writes `playtest-aces-cafe-build.json` / `save-build.json`, never the editor's file.
4. **The 33 CS0618 warnings** (sorted `FindObjectsByType`, `FindFirstObjectByType`, TMP's `enableWordWrapping`) in 12 scripts and 8 editor tools; two CS0414 unused-field warnings remain (`CustomerBrain.wordsPerSecond`, `NpcSocial.lastArrivalLookAt`), left for their owners.
5. **Tools:** `CompileLog` (every compile's errors and warnings to `Logs/Compile/last.txt`; `Checks › Copy the editor's log into Logs`), `Room › Camera angles - photograph the home view at 25°, 0° and 45° (read-only)` (renders plus `angles.json` with where each key walks; the marked-up comparison was sent to Mansoor), `Recovery` (above).

**Checks:** compiles clean; Grace's walk check all clear but the pace flag (`Logs/Night/grace-walk-2026-10-05_023252`); performance check all clear (`Logs/Performance/perf-2026-10-05_023429`); Ace's body 5 report and photo (`Logs/Night/ace-sneak-2026-10-05_023135`).

**Seen, not changed:** Mansoor's own scene edits of 30 Sept are in this commit's scene file: two roof props off the north-west corner building (`SM_Prop_Skylight_01`, `SM_Prop_Roof_Aircon_03`), the repair cabinets and tabletop moved 0.92 m back to the wall with the bench, the bench mat, tray and inspect rig nudged, the tool stand's parts moved 0.85 m along, one street lamp's glass moved. Nothing a step or check references.

**Next:** the foundation pass (Night 0, the favours) once Mansoor's calls are in; the house-interiors plan; chunk C (Grace at home); his camera-angle call.

## September 30 — Playtest 2, the second notes: Grace's door for real, the controller's diagonals, performance measured and presets, walls that fade, the real café pass; on `playtest-2`

**Read this entry first.** One code commit on `playtest-2` after `611830e`, with this entry in it. Nothing is pushed. The entries this doc owed for 29 Sept (the afternoon and evening) are at the end of this one, under "Owed from 29 Sept".

Mansoor's notes from his second playtest (Days 1–3, the whole game from Day 1), and his calls on the plan: start from Claude's list; "you control Unity"; the target is "really good performance like 4K 60 if anything"; VSync as Claude thinks best; the movement assist is Claude's first choice, on by default; Grace's door opens in the real game (he couldn't get in on Night 2); the walls should fade, "whichever makes the game feel more like an actual published game"; "make the cafe actually resemble what a cafe would look like, gather images first on the web of how cafés are designed, build a similar style layout", and it must be copyable for franchising; the phone is a separate session.

**What changed:**

1. **Grace's door opens for Ace in the real game** (`NightWalk.breakIns` on in the scene; `BreakInsRealGameSteps`, new: `Fixit Fidget › Night › Break-ins 4 - Grace's door opens for Ace in the real game (scene)`, and `… Lock it again`).
   - On Night 2 he found her door shut: the break-ins were off outside the labs, and the door opened on its own only when they were on, with nothing on screen to say so.
   - Now, on her stoop, the prompt reads **[E] Let yourself in** (`GraceDoorZone`, new: a zone `GraceHouse` makes while the night runs, ranked below Barnaby; `Interactable.SetPriority`). E opens the door for 8 s; the door still minds Ace's capsule in the doorway and shuts behind him on the pavement. With the break-ins off, E answers "It's locked" (`NightCycle.Note`) with a locked sound. Two cues in the bank: `night.door.open`, `night.door.locked`.
   - The walk check presses E on the stoop and waits for the door (all clear); the Night 1 play check 76/76, with Barnaby's prompt first.
2. **The controller's diagonals** (`PlayerMovement` movement assist, on by default; `ControllerChecks` 29/29).
   - The overhead camera's yaw is 25°, not 45°, so no stick or key direction ran along a wall: every straight push went diagonally. The assist bends the stick toward the nearest of the room's axes or the screen's axes when it is within 10–15° of one, smoothly, overhead only, never in first person, never on scripted input. A setting (`PlayerPrefs`); the keyboard is untouched.
   - **Open design call for Mansoor:** the keyboard's WASD still walks at 25° to the walls by the camera's nature. Turning the authored camera to 45° (or 0°) would fix that for keys too; it changes the look of every screen, so it's his.
3. **Performance, measured and answered** (`PerformanceCheck` + `PerformanceCheckSteps`, new: `Fixit Fidget › Checks › Performance (lab, drives itself)`; `FrameRateOverlay`, new: **F3** in any Play session or build; `QualityPreset` + `QualityPresetSteps`, new: `Fixit Fidget › Performance › Performance 2 - Make the quality presets (Low, Medium, High)`).
   - **His 240→180 drop when looking around is the editor.** The check drives six phases (standing, orbiting, zooming, first-person looking, walking, zoomed-out orbiting) and writes `Logs/Performance/perf-<time>/report.txt` + `frames.csv` with the CPU main-thread, render-thread and GPU split (Frame Timing Stats is now on in the Player settings). The game's own work is 2.6–3.2 ms a frame on the main thread, 1.1–1.4 ms on the render thread, 0.7–1.0 ms on the GPU at 1080p; the long frames the editor shows have the game's share under 4 ms. **In the built player at 3840×2160 on his PC: 240 fps flat (VSync at his 240 Hz), 4.2 ms, GPU 1.9–2.5 ms, 1% low 236–240 while mouse-looking.** Seen in the build and kept for later: a ~10 ms hitch on the first-person/overhead switch (the cut-away's first scan), the loading second.
   - **Per-frame garbage is now 0.0 KB in every phase** (it was the source of the stutter spikes): `PlayerInteractor` uses `RaycastNonAlloc`/`OverlapSphereNonAlloc` and a static comparer; `ShopUI`, `CafeViewMode.ControlsHint` and `PadInput.Kind` cache their strings and kinds; `PlayerCarry.Current` replaces `FindAnyObjectByType` in nine callers; `CafeCar` and `PersonalSpace` index instead of `foreach` over `IReadOnlyList`; `StreetLife` sorts with its own insertion sort (`List.Sort(IComparer)` allocates a delegate); `TicketRailUI` scans every sixth frame; `CutawayWall` shares one scene scan a minute for all walls and keeps only renderers within 4 m of a wall; `CameraWallFader` returns at once with nothing to fade.
   - **Quality presets:** `Low_RPAsset`/`Low_Renderer` and `Medium_*` copied from the PC asset (High) and the quality levels rewritten as Low, Medium, High (Standalone defaults to High). Low: 25 m of shadow, one cascade, 1024 map, no additional-light shadows, hard shadows, no SSAO, no HDR. Medium: 40 m, two cascades, 2048/1024, soft shadows medium, SSAO at half size. On first launch the build guesses from the GPU (integrated or under 2 GB → Low, under 6 GB or an MX/GTX 10/16 → Medium, else High); F4 cycles them and the choice is kept. The editor is left on whatever Project Settings selects. VSync is on by default in every level.
   - The verdict counts a long frame whose measured game work was under budget as an editor hiccup (listed, not failed). `.gitignore` has `/Builds/`; a Windows build lives in `Builds/Win64-2026-09-30` (not committed).
4. **The walls fade, they don't slide** (`CutawayWall` rewritten; `SeeThroughMaterials`, new; `GraceHouse` walls; the shader `Fixit Fidget/Night see-through` feathers by height; `WallFadeSteps`, new: `Fixit Fidget › Room › Wall fade 1 - The walls fade, not slide: shader and timings (scene)`; `CafeCutawayCheck` rewritten, 33/33).
   - A wall in the way fades to a ghost above the window sill in 0.35 s with an ease, solid below the sill, feathered over the next 0.45 m, one dot in five above; back only after 0.5 s clearly out of the way. What hangs on it fades with it at the same dots; the wall never moves and casts its whole shadow. Two knobs on `CafeViewMode`: **Cutaway Ghost** (0.2) and **Cutaway Feather** (0.45 m).
   - The see-through copies are now shared (`SeeThroughMaterials`: one copy per material per Play session; a renderer wears one for as long as any wall needs it; if something else dressed it meanwhile, it's left alone). `NightSeeThrough` uses the same copies. `CafeViewMode` holds the shader as a reference so a build keeps it (`Shader.Find` alone would have lost it in a build).
   - The shader carries the vertex's world height in one extra interpolator (`TEXCOORD15`) per pass; a first version rebuilt it from depth and didn't work, so don't go back to that.
5. **The real café pass** (`CafeRealismPass`, new: `Fixit Fidget › Cafe furnishing › 2 - Real cafe pass`, `Undo: the real cafe pass`, `Audit the decor`; the scene saved; the research and the plan in the project doc `claude/cafe-realism-pass.md`).
   - The bench group (`03 - repair work area`: bench, slots, rig, stand point, bench camera) moved 0.9 m back to the wall under the pegboard; the mat with it; the parts cabinet, radio, cable and box 1.8 m left beside it; a clamp lamp with a real spot light on the bench; a small oak tool stand behind the four tool pick-ups so they no longer hang in the air; the three wall-lamp stems now reach the wall.
   - The back wall's finish: paint linings 12 mm in front of the plaster, split at the sill so the fade treats them as hung on the wall — chalk white over the service side above the tiles and full height past the drink counter, deep green over the workshop side and the tan side wall (`Wall paint - chalk white`, `Wall paint - workshop green`, `Task lamp bulb` in `Assets/Art/CafeFurnishing/Materials`). The abstract print over the coffee shelf is off.
   - The furnishing guard runs each time: routes re-baked, layout 31/31 before and after, occupied circulation 24/28 before and after (the same four blocked positions as before). Photos in `Logs/CafeFurnishing/real-cafe-after-2026-09-30_082020` and `…-workshop-2026-09-30_082021`; the decor audit's six flags are false alarms of its bounds method (sofa cushions, Barnaby's step, the sconce shades).
6. **Small things:** `HoldCallIntegrationChecks.cs.meta` has a valid 32-character GUID (Mansoor: "address the stuff that wasn't done"), so `Checks › Support call integration` exists again; the sound bank has the two door cues (57 in all).

**Checks:** compiles with no errors (the CS0618 warnings are the old ones); controller 29/29; Grace's walk check all clear (before and after the wall fade); Night 1 play check 76/76; performance check: every phase's game work under budget, garbage 0.0 KB a frame; wall cut-away 33/33 after the café pass; layout 31/31.

**Seen, not changed:**

- `PolygonNpcVisual.Start` allocates ~410 KB per spawn (a spike each arrival) and its `LateUpdate` costs 0.92 ms a frame with a full room: pooling and a follow-LOD are the next two performance items if any are needed. `TicketRailUI` still scans customers (every sixth frame now).
- The bench is still a green block with a wood top; the "repair bench + tools" Blender item finishes the corner. The east wall's piers stay oat plaster. Density is a seating decision, not decor.
- Franchising needs the café as one prefab, and the scripts find their objects by name; a session of its own before a second café is placed.
- The Sidekick Package Helper still asks to install Shader Graph after some reloads; skip it.
- `Assets/_Recovery/` (Unity's own), `Claude outputs/`, `DayLogs/` from the labs, `FixitFidget.slnx`, the TMP fallback atlas and `UnityConnectSettings` were left out of the commit. `PC_RPAsset.asset` and `UniversalRenderPipelineGlobalSettings.asset` show the build's own prefiltering bookkeeping; left out too.

**Next:**

1. Mansoor's third playtest: Grace's door (E on the stoop), the stick in the overhead view, the walls, the café, F3 for the frame rate and F4 for the presets. His answer on the camera yaw (25° stays, or 45°/0°).
2. The NPC plan (Schedule I-style routines and memory): chunk C first (Grace at home with a day in the life), then the regulars' days, walk-in reasons and barks. A project doc before code.
3. Chunk B (sneaking), D, E, F of the break-ins; Ace's face close-ups; houses 2–4; the phone session; Modern Civilians on a sale.

**Owed from 29 Sept** (the commits this doc hadn't covered; details in the project doc `claude/break-ins-chunk-a-handoff.md` and the spec):

- **Layout v2** (`d6c0dca`) and **the stand-in photos** (`0d82491`).
- **Break-ins chunk A** (`0f4dabc` the Blender pieces, `f4551ac` the house): Grace's house built inside and walkable — `Night › Break-ins 1 - Build Grace's house inside`, `Break-ins 2 - Check`, `Break-ins 3 - Play the night at Grace's door (lab)` and `… Walk Grace's house by itself (lab, a check)`; `GraceHouse`, `GraceHouseSteps`, `GraceHouseCheck`, `GraceHouseWalkCheck`; collision is night-only; the see-through skips her house.
- **Ace's stand-in body** (`3701866`): `AceBody` on the Player, the café rig's idle/walk/run on a PlayableGraph, 2.04 m like the café's people; **by day too, and Sidekick installed** (`3dc5027`, `9b99032`: Starter Pack and Character Creator under `Assets/Synty/` git-ignored, `Assets/DownloadCache/` ignored, the FBX Exporter). Ace is designed in Synty's demo scene, never in the café scene.
- **The Sidekick Ace wired in** (`7808e32`: Humanoid Animator, the same gait, foot IK, `AceFace` blinks and glances, `AceSidekickSteps`) and **the Play freeze fixed** (`b29b993`: `SidekickHiddenWindowFix` closes Synty's hidden downloader window before Play).
- **The whole game from Day 1** (`611830e`): `Playtest › Play the whole game from Day 1 (test save)` on the lab's own save, with Grace's door open every night (lab mode 3).

## September 29 (overnight) — Playtest 2: the fixes step 2 turned up, the break-ins spec (step 3), and step 4 in Blender (Barnaby and Grace's furniture); on `playtest-2`

**Read this entry first.** Four code commits on `playtest-2` after step 2's handoff (`10d5627`): `0563d7d`, `d8a1063`, `6ca5ca3` and `7f56852`. This entry comes in the commit after them. Nothing is pushed.

Mansoor went to bed and asked for the steps to go on without waiting for his playtest. Everything below was done overnight and checked in lab sessions; the Day 2 playtest save was never used.

**What changed:**

1. **Editor Play sessions no longer leave helpers behind** (`0563d7d`, `PlaySessionLeftovers`, new).
   - The sound player, the soundscape, the NPC attention director and the straight-face meter were marked DontSave. In the editor such an object outlives its Play session, and 51 were waiting in the editor, each still switched on.
   - Now they end with their Play session. In the editor, each session starts by putting away any left from before. A build never had the problem.
2. **PhoneRepair.prefab is back as it was.** Its "Fresh" screen had been switched on in the prefab asset itself during step 2. The flag is flipped back in the file, so git no longer lists it. What switches it on during play wasn't found; if it shows as changed again, discard it.
3. **A correction to step 2's entry:** all nine console tests pass, ContinuationRules included. The "ContinuationRules fails" line came from a stale copy of the tests in the cloud workspace. The entry below is corrected.
4. **Step 3, the break-ins spec** (`claude/break-ins-spec.md` in the project; nothing built from it yet). It covers:
   - Grace's ground floor: a hall with the stairs and a cupboard under them, a front room and a kitchen;
   - the way in, which is learned by day;
   - the rules for being seen and heard;
   - getting caught: bail, a late opening, a scandal;
   - Ace's body, supplies, and the morning after.

   Eight calls in its §11 are Mansoor's. The floor plan is `Claude outputs/grace-house-ground-floor.png`.
   - **The survey tool** (`d8a1063`): Fixit Fidget › Night › Break-ins - Survey Grace's house (read-only). It gives her house's size, floors, windows, door and neighbours, with photos.
5. **Step 4 in Blender: Barnaby** (`6ca5ca3`).
   - **How it was built.** Blender on this PC wasn't running its MCP server, and the command-line mode has no BLENDER_PATH. So the models were built in Python with Blender 5.2.2 run as a module in the cloud workspace. That is the same version as this PC's Blender, and the .blend files open in it.
   - **Where it lives.** The scripts are in `Tools/Blender` (see its README) and the source is `BlenderSource/Night1_Barnaby.blend`.
   - **The model.** Barnaby is the placeholder's look made properly: red hat, blue coat, white beard, boots, a brass buckle, a mossy stone. He is 912 triangles and 0.23 × 0.23 × 0.52 m, inside the space the Night 1 set-up checked on her step. His materials are the placeholder's own, plus moss.
   - **In Unity.** `Assets/Art/Models/Night/Barnaby.fbx` comes in upright with no turn and scale 1.
     - Fixit Fidget › Night › **Night 1 - Barnaby: use the Blender model / back to simple shapes (the prefab only)** switches between the two looks. Both live in the prefab.
     - The scene file isn't touched. The gnome on her step and the copy on Ace's shelf are the same prefab, so both follow.
     - The Night 1 set-up builds the prefab the same way if it's run again.
6. **Step 4 in Blender: Grace's furniture** (`7f56852`). These are the 24 pieces on the spec's list, 9,584 triangles in all:
   - **Front room:** her armchair, the sofa, a wood-cased TV and its cabinet, the coffee table, the sideboard, four photo frames, a standard lamp, the rug.
   - **Kitchen:** the counter run with an oven, a hob and a sink; wall cupboards; a mint fridge; a kettle; a teapot; the box of reunion cups (three sleeves of twelve) and one sleeve on its own.
   - **Hall:** a coat stand with her coat, the stairs with the cupboard under them and its door, a doorway and a door.
   - **Files.** The FBX files are in `Assets/Art/Models/GraceHouse`. The source is `BlenderSource/GraceHouse_GroundFloor.blend`, laid out by room.
   - **In Unity.** Fixit Fidget › Night › **Break-ins - Grace's furniture: import and check (the models only)**:
     - sets the import settings;
     - makes her 21 materials (`Assets/Art/Materials/GraceHouse`);
     - maps every material by name to the café's or hers;
     - checks each piece and photographs the lot in a preview scene.
   - **Not in the scene yet:** that's step 5.

**Checks** (lab sessions):

- Compiles with no errors. There are no warnings from the new files; the CS0618 warnings are the old ones from other editor files.
- Barnaby's step: all pass. He is one mesh, upright, facing the street, and fits the checked space. Both scene copies are the prefab, and the shelf copy is still hidden (`Logs/Night/barnaby-model-2026-09-29_082222`).
- Night 1 play checks with the model: keeping a straight face 76/76, cracking 76/76, the morning after Day 2 73/73.
- Grace's furniture step: 49 checks, all pass (`Logs/Night/grace-furniture-2026-09-29_085237`).
- Before these: the recap phone check 84/84; the Night notebook 15/15; the rules in Unity all pass; all nine console tests pass.

**Seen, not changed:**

- **Unity pauses Play when it isn't the window in front.** Run In Background is off in the Player settings. A notification or another window taking focus stalls a play check until Unity is clicked again; one check tonight took 195 s instead of 90 for that reason.
  - Turning it on (Project Settings › Player › Resolution and Presentation) would keep the editor running in the background. It changes builds too, so it's Mansoor's call.
- **Menu items can be run from Unity Search** (Ctrl+K): type the item's name and double-click it.
- **The night camera at Grace's step.** Her house fades to a dither and Barnaby is small from that height; the glint makes him findable. Worth watching in play.

**Next** (the plan's order, `claude/playtest-2-plan.md` §6):

1. Mansoor plays: the phone at closing, and Night 1 with the new Barnaby.
2. Mansoor's eight calls in the break-ins spec (§11).
3. Step 5, Grace's house end to end, once they're decided.

**Known:** as before, the TextMesh Pro fallback font, the playtest's day logs and the café scene after Play (reloaded without saving) are left out of every commit.

## September 29 (morning) — Playtest 2, step 2: the end-of-day recap becomes Ace's phone; on `playtest-2`

**Read this entry first.** One code commit, `e919395`, on `playtest-2`, after step 1's handoff (`f0d6a6c`). This entry comes in the commit after it. Nothing is pushed.

This is step 2 of the second playtest's plan (`claude/playtest-2-plan.md` in the project: the idea in §4.3, the spec in §9, as built in §10). The playtest's note: the recap read like "word vomit", but the stars and the reviews were loved, so make it a phone. It is built from the mock-up Mansoor approved.

**What changed:**

1. **The recap is Ace's phone** (`RecapPhone`, new). It is built in code while playing, so the scene doesn't change.
   - It comes up at closing over the dimmed café, and the HUD hides behind it. Four apps sit on a tab bar, and it opens on Reviews each evening.
   - **Reviews:**
     - today's takings on a dark card, with **Details** for the day's 12 numbers (a failed save shows there, in red);
     - today's summary: how many reviews, the reputation change, and five bars;
     - every review of the day as a card, newest first: avatar, name ("a walk-in" for walk-ins), a "regular" tag, 1–5 stars, "today" and the line. No average anywhere.
   - **Franchise:**
     - the café's stars, "New star!" on the day, the level's name, and the bar to the next star;
     - HQ's requests: the next star, no scandal, the offer at five stars;
     - what changed today, with one line about the day's worst kind of review.
   - **Shop:**
     - the till;
     - cups and beans, in red under 10;
     - the restock: +20 each plus the upgrade's bonus, for $30;
     - the six upgrades with level and price, greyed out of reach and MAX when maxed.
     - Buying works as before: only at closing, saved at once.
   - **Notes:** the notebook person by person. Where they live comes first, with "hunch" or "likely", and today's facts are marked NEW.
   - **Close up for the night** sits above the tab bar in every app. It is the recap's own button: it still leads into the night, and reads Open Tomorrow once the night is walked.
   - **Badges:** Franchise gets a dot on the day a star is earned, Shop a "!" while cups or beans are low, and Notes the number of facts learned today.
   - **Controls:**
     - mouse: click, and the wheel scrolls;
     - keyboard: Q/E or ←/→ switch apps, 1–4 jump to one, W/S or ↑/↓ scroll;
     - pad: LB/RB switch apps, the right stick scrolls, the D-pad moves between buttons with a gold ring on the one selected, and A presses.
     - The hint for the device in use sits under the phone.
2. **RecapUI:** **Use Phone** (on) hands the recap's panel and button to the phone. Off brings back the three-column recap, which stays in the scene untouched. Also new: `RecapUI.Showing` (ShopUI uses it to hide the HUD) and `RecapUI.Phone`.
3. **A line for every review** (`ReputationLedger`, `SaveManager`, `SaveData`).
   - Settling the day now writes a card for every review, not only the three quotes. The lines come from the same pools, picked as the best quote is, so the day's best review reads the same on both.
   - The cards are saved with the recap: five additive arrays, no version bump. A recap saved before this step shows its quotes as cards (`ReputationRules.SplitQuote`).
   - Also `ReputationRules.StarsOf` (Loved it 5 … Never again 1, never averaged) and `ReputationRecap.Lesson` (the day's one line).
4. **Notes by person** (`NotebookRecap.People` and `Sureness`; `Sentence` is public). The night's notebook page is built from `People` now and reads exactly as before (compared on 20,000 random notebooks).
5. **Shop plumbing:** `ShopInventory.RestockAdds` and `LowStock`; `UpgradeShopUI.TryBuy` and `TryRestock`, shared by both recaps.
6. **Sound:** `phone.tap` in the sound bank, for switching apps, Details and buying. It stays silent until a file is chosen, like every cue so far.
7. **A leak fixed** (`NightCycle`).
   - The night cycle and its doorway were marked DontSave, and in the editor such an object outlives its Play session. A night stopped with its note on screen left that note in the Game view in Edit Mode, and drew it over the next session's recap. The phone's check caught it.
   - They're ordinary scene objects now, and any left over from before is put away when a session starts.
8. **The lab** (Fixit Fidget › Recap phone):
   - **Play from a sample Day 3 recap (lab):** a lab session (a test save) that opens on a made-up Day 3's recap:
     - five reviews and the café's second star;
     - $641 in the till, 12 cups and 9 beans (low);
     - Faster Machine at level 1;
     - Grace in the notebook, with two facts new today.
   - **Play check (lab, drives itself):** `RecapPhoneCheck`, 84 checks in about 6 s, and a photo of every app (`Logs/Recap/recap-phone-check-<time>/`).
   - Fixit Fidget › Reputation › Preview a busy day fills the phone too.

**Checks** (lab sessions; the playtest save was never used):

- **Compile:** no errors, and no new warnings.
- **The recap phone play check:** 84/84, 10 photos (`Logs/Recap/recap-phone-check-2026-09-29_061420`).
- **By hand in the lab, with the real mouse:** clicking the tabs and Details, and scrolling with the wheel.
- **Night 1 play checks** (they press the recap's button): cracking 76/76, keeping a straight face 76/76, the morning after Day 2 73/73.
- **Night notebook** (Play Mode, lab): 15/15.
- **Rules in Unity:** all PASS.
  - Reputation 19,699, Night notebook 51, Night 1 283, Home 40.
  - Recap input isolation and Recap save checkpoint.
- **Console tests:** all nine pass. (This line first said ContinuationRules failed; that came from a stale copy of the tests, and was corrected overnight.)

**Seen, not changed:**

- **Screenshots of the desktop look washed out.** Captures of the screen on this PC come out much brighter than what the game draws.
  - The Chrome icon on the taskbar is brightened by the same amount, so it's the capture, not the game (likely Windows HDR).
  - The in-game photos are the true colours, and they match the mock-up. Judge colours from the photos, or on the screen itself.
- **`Assets/AssetsPrefabs/PhoneRepair.prefab` shows as changed**: its "Fresh" screen is switched on.
  - Something in a Play session switches on the prefab asset's own screen rather than a copy's, and the sound bank update's save-all wrote it to disk.
  - It's harmless in play: every copy switches it off again when it wakes.
  - Nobody meant that change, so it was left out of the commit. (Restored overnight: see the entry above.)
- **Other runtime objects are still marked DontSave**: the straight face's screen, the sound player and soundscape, the attention director, the café life probe's camera, and others.
  - They may outlive an editor Play session the same way.
  - It's editor only; a build is unaffected. Not changed here, to keep the step small. (Fixed overnight in `0563d7d` for the sound player, the soundscape, the attention director and the meter.)
- **The first two clicks after the lab opened didn't register.** They worked once a key had been pressed in the Game view, and every click after that did. Most likely the editor's focus; worth noticing in play.

**Placeholder copy**, Mansoor's to rewrite:

- the apps' names;
- the headers: "Ace's Café" / "Repair café · Coffee · Reviews", "Franchise HQ" / "Your café's standing", "Supplies" / "In the till", "Notes" / "Everything Ace knows";
- HQ's three requests and their small print;
- the day's six lines about the worst kind of review;
- "One for every drink", "For the coffee", the low-stock warning, and "Nothing written down yet."

**Next** (the plan's order, `claude/playtest-2-plan.md` §6):

1. Mansoor plays a closing: the phone, in play.
2. Step 3: the break-ins spec, written and decided together.
3. Meanwhile, Mansoor's: the new regulars' names and who they are, with his sister.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before.
- The playtest's day logs (`DayLogs/AcesCafeLayout`) show as modified or new. They're never committed.
- The café scene showed as changed after Play (Cinemachine's Save During Play). It was reloaded without saving; the file on disk is unchanged.

## September 29 (early morning) — Playtest 2, step 1: the night camera matches the day's, and stars and a moon in the night sky; on `playtest-2`

**Read this entry first.** One code commit, `7660844`, on the new branch `playtest-2`, made from `dialogue-pass` at `2e8160f` (the dialogue-pass handoff below). This entry comes in the commit after it. Nothing is pushed.

Mansoor's second playtest brought nine notes. The plan, `claude/playtest-2-plan.md` in the project, turns them into one ordered list, and Mansoor decided its four questions on 29 Sept:

- at night Ace takes supplies (cups, beans, repair parts) and keepsakes, replacing 25 Sept's "trophies only";
- real rooms behind the doors;
- the furniture built in Blender;
- the order: quick fixes, then the recap as a phone, then the break-ins.

This entry is step 1, the quick fixes: the camera felt different at night, and the night sky wanted stars.

**What changed:**

1. **The night camera is the day's** (`CafeViewMode`).
   - At night the overhead camera still follows Ace, a fifth of a second behind, but with the day's own tilt and zoom: 38–68° and 24–48 m, carrying on from the view the day left. From the usual start that is 43° and 34 m, instead of 62° and 20 m.
   - Buildings in the way still turn see-through, and the café's walls still only lower when they hide Ace.
   - **Follow With Day Framing** (on) on `CafeViewMode`. Off brings back the 28 Sept framing (55–80°, 12–34 m, starting at 62° and 20 m).
   - First person was already the same camera by day and night; only the lighting differs there.
   - New for the checks: `OverheadLimits` and `OverheadHome`.
2. **Stars and the moon** (`NightSky`, new; the shader "Fixit Fidget/Night sky", in `Assets/Playtests/AcesCafeLayout/Night walk - sky.shader`, new).
   - About 1,600 stars, 40 of them brighter, some a little blue or warm, a few twinkling. They thin out towards the horizon.
   - The moon is where the moonlight comes from (`CafeDaylight`'s Moon Altitude and Moon Azimuth: 42° up, in the north-east). It has a glow, and no stars in front of it.
   - It is one mesh of small quads, drawn round the camera just inside its far plane: added light, no fog, never culled. Buildings and lamp posts hide it.
   - It is rolled from a fixed seed with its own random numbers: the same sky every night, and no other random stream moves.
   - `CafeDaylight` makes it the first time the moon is up (night walks only) and sets its strength. By day it doesn't exist. **Night Sky** (on) on `CafeDaylight` switches it off.
   - Only first person sees the sky. The overhead camera has a 44° field of view, so even at its flattest tilt (38°) the top of the screen looks 16° below the horizon.
   - **A player build must include the shader** (it is looked up by name), as with the see-through shader.
3. **The night tour** (`NightTour`, Fixit Fidget › Night › Night walk 3 - Walk the tour).
   - Each leg's tilt and zoom are given in terms of the camera's own limits: its lowest tilt, its start, zoomed right out or right in. So the tour tests whichever framing the night uses.
   - At the end it looks up at the moon, and at the stars away from it, in first person (photos 23 and 24).
   - Its report, and `NightWalk.Describe`, include the camera's limits and the night sky.

**Checks** (lab sessions; the playtest save was never used):

- **Compile:** no errors, and no warnings from these files.
- **The night tour** (`Logs/Night/night-tour-2026-09-29_022617`):
  - 342 m in 70 s, never stuck; frames 4.2 ms on average, the worst 24.8 ms;
  - 26 different buildings and trees turned see-through, at most 4 at once. With the old framing it was 10, at most 2 (`night-tour-2026-09-28_154707`);
  - the night sky: 1,635 stars (39 brighter) and the moon, 42° up towards 35°, drawn.
- **Night 1 play checks:** cracking 76/76, keeping a straight face 76/76, the morning after Day 2 73/73.
- **Night 1 rules:** 283 assertions, PASS.
- **Wall cut-away** (Play Mode, a Day 5 lab session): 31/31. The day's overhead camera is as before.
- No warnings or errors in any of these sessions.

**Seen, not changed:**

- **Ace is small on screen at night now**, as by day. The grey capsule is hard to see in a building's shadow or under a see-through building. The stand-in body (the plan's step 5) is the fix; the torch (F) helps meanwhile.
- **More buildings turn see-through** at the day's flatter tilt (26 against 10 on the tour). The dots read clearly in the photos; worth watching in play.
- **The moon is a bright, glowing disc.** The night look's bloom washes out its darker patches. A dimmer Moon Colour on the shader would show them but glow less. Mansoor's call.

**Next** (the plan's order, `claude/playtest-2-plan.md` §6):

1. Mansoor plays a night: the camera and the sky.
2. Step 2: the recap as Ace's phone (Reviews, Franchise, Shop, Notes), from the mock-up.
3. Meanwhile, Mansoor's: the new regulars' names and who they are, with his sister.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before.
- The playtest's day logs (`DayLogs/AcesCafeLayout`) show as modified or new. They're never committed.
- The café scene shows as changed in the editor after Play (Cinemachine's Save During Play). It was never saved; the file on disk is unchanged.

## September 28 (late night) — The dialogue pass: talking like Skyrim, not a novel (subtitles, Ace's replies, one question for Grace, shorter lines); on `dialogue-pass`

**Read this entry first.** One code commit, `66eacd8`, on the new branch `dialogue-pass`, made from `night-1` at `1e4d78e` (the bug-fixes handoff below). This entry comes in the commit after it. Nothing is pushed.

Mansoor didn't like how much the dialogue read like a novel ("im not a fan of how the dialogue is so much that it feels like a novel, i want the dialogue or just the feel to feel like dialogue from skyrim"). The pass was discussed first in `claude/dialogue-skyrim-proposal.md`, with measurements and a mock-up. He chose all seven steps, the whole line at once with a short listening beat, a question plus a waiting fallback for Barnaby, and to keep the placeholder portrait box. Every check ran in lab sessions, and the playtest save was never written.

**What changed:**

1. **Subtitles, not a typewriter** (`ConversationUI`).
   - What they say is a whole line under their face: bottom-aligned from 44 up, x 390–1250 at 1080p, with a soft shadow and a dark band along the bottom of the screen.
   - A speech of several lines (a `\n` between them, never a blank line) plays one line after another at reading pace (`readingSpeed` 18 characters a second; each line stays 1.4–4.5 s). The earlier ones stay on screen, dimmed and smaller, three lines at most.
   - After the last line comes a short listening beat (`listenBeat` 0.35–1 s), then Ace's replies.
   - E while they talk shows everything at once and ends the beat. It never answers (`SkipReveal`).
   - The placeholder portrait box and the name above it are as they were.
2. **Ace's replies** (`ConversationController`; placeholder copy in `AceReplies`).
   - A list on the right (x 1304–1864). Taking it comes first and is highlighted ("I'll take a look." or "Coming right up."), then any questions, then turning them away ("Not today." or "Sorry, not today.").
   - Out of stock or a full shelf offers one reply ("Sorry, we're out." or "No room on the shelf."). A finished job: "Here you go."
   - Keys: W/S, the arrows, the wheel, the D-pad or the left stick move the highlight. E, Enter, a left click or A chooses. 1–4 choose directly. Q or Y turns them away. Esc, F, Tab, B or X steps away. The footer reads "[W/S] Choose   [Esc] Step away".
   - E, E still takes a job: the first E shows the whole line, the second chooses the highlighted reply.
   - An asked question greys out, marked "noted" if Ace learned something from it.
   - A closing line ends by itself once it's read, or at once on E.
3. **Questions** (`CustomerIdentity.Topics`, `TopicChoice`, `CustomerProfile.topics`).
   - A regular can have up to two things for Ace to ask about on a visit. One can come from a night thing: Grace's camera visit has "Big plans tonight?" (`NightThings.topic`). The rest come from the profile's own `topics` (for first meetings, return visits, or any visit); none are written yet.
   - Asked, Grace answers with Barnaby in two lines, and the notebook has him.
   - Not asked, she mentions him while she waits, in place of one of her stories (`waitingMention`, `CustomerStoryteller`, `StorytellerRun.Rest`).
4. **Grace's Day 1** (`GraceCameraEpisode`, `NotebookEntries`, `NotebookHooks.HeardThanks`).
   - Her request is two short lines: "My camera picked a fine time to sulk. The reunion's tomorrow." and "Jammed shutter, dirty lens. Leave the strap alone; my husband carried it everywhere."
   - Her thanks when Ace takes it is the reveal: "Thank you. For once, I might let somebody put me in the picture."
   - The notebook files the strap and her husband from the request, and the reunion and the photos from the thanks: the same four facts as before.
   - Her hand-back lines are shorter.
5. **Grace's Day 2** (`CustomerIdentity.AcceptReturnMemento` and `TakeClosingNote`, `ConversationController.End`).
   - She orders her latte first, in her own words.
   - Once her order is taken, she tells Ace the photo's news and gives the print, in her own voice: "The reunion photos came out! I'm right in the middle." / "Usually I'm safely behind the camera." / "I've brought a print for your shop." (Smudged: "This print is for you." Missed: no print.)
   - What happened is a line on screen once the conversation closes ("Grace left the reunion photo for the shop.", `GraceCameraEpisode.HandoffLine`), never a narrator in her speech.
6. **Shorter lines** (`Regular_Grace`: 11 lines; the episode's lines; Barnaby's). Across Grace's two visits, the text you have to get through went from 1,074 characters to 644, and the longest line from 268 to 84. The rewrites are placeholders for Mansoor to rewrite. Nothing new about the characters was invented.
7. **Walk-ins** (Fixit Fidget > Dialogue > Walk-in lines, run once; the café scene is saved).
   - Five requests that began a sentence with `{fault}` or `{device}` read naturally now ("Morning! My {device} — {fault}. Can you look?").
   - Each personality orders a drink in its own words (`DialogueSet.drinkOrder`; placeholders).
   - A device is said the way a sentence says it: "my pocket watch", not "my Pocket Watch" (`CustomerIdentity.SpokenName`). The ticket keeps "Pocket Watch".
8. **Out of the subtitle's way** (found in the check photos):
   - The straight-face meter covered Grace's complaint: it was placed for the old layout. It takes the replies' place now, bottom right, on the subtitle's baseline (`StraightFaceUI`).
   - Grace's closing note sat on the "[F]  Serve at counter" prompt. The note box sits higher now, above the prompt, by day and at night (`NightCycle`).
   - The lab's banner sat under the replies. It steps aside while a conversation is open (`CafeLab`).
9. **The Day 1 guide** reads "Aim at Grace. E talks; when your replies appear, E takes the job." (`DayOneGuideUI`).

**New checks:**

- **Fixit Fidget > Checks > Dialogue rules** (edit mode, café scene open, read-only):
  - every line on screen is at most 150 characters (Skyrim's own cap), and over 100 is a warning;
  - more to say is another line, never a paragraph;
  - a bubble in the room is one line (aim for 60–90);
  - every `{token}` is one the game fills in, and none starts a sentence;
  - Ace's replies are under 40 characters (aim for 30);
  - then Grace's Day 1 as data (13 checks).
- **Fixit Fidget > Dialogue > Conversation - play check (lab, drives itself):** a fresh Day 1 in the lab's own save.
  - Grace's request line by line; E before the replies, which never answers; the replies; S moving the highlight; the question, and Barnaby in the notebook; the reveal and her four facts; E closing it.
  - Then a walk-in: take it or turn away only, the device in lower case; Q; they leave.
  - Eight photos and a report go to `Logs/Dialogue`.
- **The Night 1 play checks** now also check that the meter is clear of her line and the note is clear of the prompt.

**Checks** (lab sessions):

- **Compile:** no errors; 47 warnings, none from this pass's code.
- **Conversation play check:** 31/31.
- **Night 1 play checks** (on the final code): keeping a straight face 76/76, cracking 76/76, the morning after Day 2 73/73.
- **Night notebook** (Play Mode, lab): 15/15.
- **Dialogue rules:** 138 lines and 8 replies, no problems, 1 warning. Grace's Day 1 as data: 13/13.
- **PASS:** Night 1 rules 283, notebook rules 43, customer memory, human rules 52, human integration, continuation rules 66, featured repair, ticket layout, Day 1 onboarding, customer hands, storyteller interaction, storyteller 58.
- **Photos:**
  - `Logs/Dialogue/conversation-check-2026-09-28_214520`: the replies, and a walk-in;
  - `Logs/Night/night-one-check-cracking-2026-09-28_220632`: the meter beside her line;
  - `Logs/Night/night-one-check-straight-face-2026-09-28_215617`, photo 12: the note above the prompt.

**Seen, not changed:**

- **Grace's second story line is 97 characters**, the rules check's one warning (a bubble aims for 60–90). A shorter version, if wanted: "We arranged everyone by height once. Auntie Rose climbed a chair. She's eighty-three." (85)
- **A story bubble that began just before a conversation stays up over it.** The storyteller never starts one while Ace is talking to someone, but one already up finishes. The check caught Grace's story over a walk-in's face. Hiding other people's bubbles while a conversation is open would fix it, but a story line hidden that way would count as told. That's Mansoor's call.
- **The dark band behind the subtitle is faint** in the café's warm light; the text's shadow does most of the work. It can be darker (`ConversationUI.Band`, 0.62 at the bottom).
- **Fixit Fidget > Content > 4 · Rebuild archetypes as personalities** (August's one-time tool) still holds the old walk-in lines and replaces the whole list. Running it would undo the walk-in fixes and drink orders, and any tuning since.
- A conversation started from the overview camera shows the first line while the camera turns to the speaker (as before).

**Next:**

- Mansoor plays Days 1–2 (the lab entry or his own save) and says whether it feels like Skyrim now.
- The placeholder copy is his to rewrite: Ace's replies, the walk-ins' drink orders, Grace's trimmed lines.
- The three Night 1 questions in `claude/night-1-slice.md` §9 are still open.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before.
- The playtest's day logs (`DayLogs/AcesCafeLayout`) show as modified or new from earlier sessions. They're never committed.

## September 28 (night) — Night 1 bug fixes before the playtest: drink orders, the door on the way out, first person at the door, the morning's notebook, a safe nightfall; on `night-1`

**Read this entry first.** One code commit, `69c38b1`, on `night-1`, after the fixes entry below (`062ea9d`, handoff `7408576`). This entry comes in the commit after it. Nothing is pushed.

Mansoor asked for every bug to be fixed before he plays ("fix any and all bugs before i play"). What was found came from two code reviews of Night 1's changes, the check photos, a live day-into-night run and a play-through of the lab entry by hand. Every check ran in lab sessions, and the playtest save was never written.

**What was wrong, and what changed:**

1. **A drink was "brought in" like a repair** (`CustomerIdentity`, `ConversationController`).
   - A drink-only visit's first line came from the repair lines, with the drink as the device and "broken" as the fault. Grace on Day 3 said "Today's patient is my Latte: broken." A walk-in who came for a drink said "Hi! My Latte — broken. Any chance?", on every day, from before Night 1. Grace's Day 2 return ended "Today, I'd love a Latte."
   - Now a returning regular skips the repair callbacks on a drink visit and orders in their own drink line (`orderedDrink`). Grace on Day 3: "A latte, please. No sugar. Life provides enough of that."
   - A walk-in orders with a placeholder line, one pool for every personality: "Could I get {a drink}, please?" or "Just {a drink} today, please."
   - Grace's Day 2 return ends "Today, I'd love a latte."
   - New tokens: `{drink}` (the drink's name, lower case) and `{a drink}` ("a latte", "an espresso").
   - The key hint reads "[E]  Take the order" for a drink ("Take the job" for a repair).
   - One line is still picked, so the customers' random stream is as before.
2. **The café's door offered "Call it a night" on the way out** (`NightCycle`, `NightDoorway`). It's offered only once Ace has been out since the night began (`NightCycle.BeenOut`). The moment's wait after the night begins stays.
3. **In first person, standing in the doorway offered nothing** (`NightInteractable.IsZone`, `PlayerInteractor`). The crosshair's ray starts inside the doorway's trigger and never hits it. The doorway is now a place: offered wherever Ace stands in or near it, in either view.
4. **Night things still worked during the fades** (`NightInteractable`). E in the dark at nightfall, or during dawn's fade (Barnaby after 4 AM), still counted. They're offered only while the night is on.
5. **The notebook filed a mention Ace never heard** (`NightThings`, `NotebookHooks`, `MorningFace`). If Ace took the gnome without having heard of him (Mansoor's own save), Grace's complaint filed her Day 1 mention ("…the saffron house on the corner. Twenty years. Polishes him."). It now files what she has just said: "Had a garden gnome, Barnaby, on her front step. Twenty years." (a placeholder: `notebookComplaint`, `NightThings.Complained`, `NotebookHooks.HeardComplaint`).
6. **A note from the night stayed on over the morning.** "Barnaby is coming home with Ace" (4.5 s), or the first night's 9-second note, showed over the "Home" and "Day 2" captions. It's put away as the morning begins.
7. **An error at nightfall or in the morning would have left the screen dark for good** (`NightCycle`, `RecapUI`). The coroutine stopped where it threw. Each step is now guarded (`Safely`: logged, and the rest carries on).
   - A night that can't begin goes straight on to tomorrow.
   - If tomorrow can't open, the recap comes back (`RecapUI.ShowAgain`) and its button goes straight to tomorrow.
   - The screen always comes back.
8. **The meter stopped a frame late** (`MorningFace`). The needle moved on before Space was read, so it stopped a frame past where the player saw it: about 1.5% of the bar at 60 fps, 3% at 30. Space is read first now.
9. **Low patience turned Grace's face "Impatient" during the morning scene** (`CustomerIdentity.Feel`). The scene's faces (concerned, then pleased or surprised) now hold until her next line.
10. **The morning's visitor could be beaten to the counter** (`CustomerSpawner`). Grace walks over from her own front door, and the next arrival's countdown ran meanwhile: someone from the car park could reach the counter first. While someone with news of last night is walking over, the next arrival waits, a minute at most.
11. **A lab night walk made the day run ahead** (`DayClock`). The night walk stops the day's clock, but the day's elapsed time kept counting. After an editor lab's mid-day night walk, the spawner thought the day was further on and its rush came early. Time the clock stood still no longer counts. (A real night comes after the day is over, where this never mattered.)
12. **The lab's banner covered the controls hint and the conversation's portrait** (`CafeLab`). It's now in the bottom right corner, sized with the HUD.

**Checks** (lab sessions):

- **Compile:** no errors, 67 warnings (as before).
- **Night 1 play checks** (on the final code): keeping a straight face 70/70, cracking 70/70, the morning after Day 2 71/71. New in them:
  - on the way out, the door offers nothing;
  - in first person inside the door, with the camera at Ace's eyes, "[E]  Call it a night" is on screen (photo `05b`), then again from above;
  - Grace is first at the counter (nobody waiting ahead of her);
  - the notebook has her own words (the Day 2 lab, like an older save);
  - her order: "…Today, I'd love a latte." on Day 2, "A latte, please. No sugar. Life provides enough of that." on Day 3, and "Take the order".
- **Night 1 rules:** 267 (`Tests/NightRules`: 265 rules and 2 save-format).
- **Customer memory**, with new checks: a returning regular's drink visit orders in their own words, a walk-in orders "an espresso", and a repair keeps the repair line.
- **16 more gameplay checks PASS:** human counter integration, customer delivery from either hand, Day 1 onboarding, Grace camera content, Grace camera interaction, continuation rules, recap input isolation, recap save checkpoint, storyteller interaction, waiting space, ticket layout, human rules, home rules, notebook rules, storyteller rules, featured repair.
- **A live day into the night** (the path no check drives): a lab Day 5 on autopilot to its real end and recap, then "Close up for the night".
  - The lab put Ace in the doorway: nothing offered. Out and back in by hand: "Call it a night", and in first person too.
  - Then left alone: the night ended at dawn by itself, and Day 6 opened and ran.
  - No warnings or errors.
- **By hand, the Day 1 recap lab entry:** the recap's button; the night; out through the door (nothing offered); to Grace's step by her line's directions ("[E]  Take Barnaby"); the note; the notebook page (N); back in; "Call it a night"; Day 2's guide. No warnings or errors. Grace then ran out of patience at the counter while the mouse look was being fumbled (the clock runs about an hour every 16 seconds); the play checks cover her scene with real key presses.
- The playtest's own day logs weren't touched: lab sessions wrote only to `DayLogs/CafeLab`.

**Seen, not changed:**

- Grace's lines on a drink visit are her authored ones. Two read as if a repair were involved: her warm "accepted" line ("Take care of it—and yourself.") and her first-meeting drink line ("While you work, may I have my usual latte?"). They're Mansoor's to rewrite (`Regular_Grace`).
- The walk-ins' drink orders are placeholders in code (`CustomerIdentity.DrinkOrderLines`), one pool for every personality. A field on the dialogue sets would let each personality order in its own words.
- The conversation's text has no shadow, and over the café's bright windows in the close-up it's harder to read. The HUD prompt's shadow could be given to it.
- If Grace leaves before Ace talks to her, the morning scene waits for her next visit, and her complaint still says "last night".

**Next:** Mansoor plays Night 1 (the lab entry, then his own save) and answers the three questions in `claude/night-1-slice.md` §9.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before.
- The playtest's day logs (`DayLogs/AcesCafeLayout`) show as modified or new from earlier sessions. They're never committed.

## September 28 (late evening) — Night 1 fixes after the first look: the meter under Grace's line, no patience bars in conversations, a shadow behind the prompt, Grace's stand-in look; on `night-1`

**Read this entry first.** One code commit, `062ea9d`, on `night-1`, after the Night 1 entry below (`2038a44`, handoff `43665f8`). This entry comes in the commit after it. Nothing is pushed.

Mansoor still hasn't played Night 1. The Night 1 report raised four things from the check photos; he agreed ("on the areas you pushed back on, i agree") and asked for them to be fixed before he plays ("so go implement the changes we pushed back on"). He picked Grace's stand-in look himself: "1 Business Woman". He also agreed that nothing else is built until he has played it; his answers to three questions pick the next task (`claude/night-1-slice.md` §9). Every check ran in lab sessions, and the playtest save was never written.

**What changed:**

1. **The meter no longer covers Grace's face** (`StraightFaceUI`, `MorningFace`).
   - It sat just above the conversation's text, over the lower half of the speaker's face in the close-up. It now takes the conversation's key-hint row under her line: 81 to 159 up on the 1080p reference.
   - It gives its key itself: "[Space]  Keep a straight face", then "Straight face!" or "You cracked.", then "[E]  Go on" in the usual ink. `StraightFaceUI.Draw(meter, title)` and `Result(meter, then)`; `MorningFace` keeps the options row empty while the meter shows.
2. **No floating patience bars while a conversation is open** (`ConversationController.AnyOpen`, `CustomerBrain.ShowFloatingBar`).
   - The close-up showed the speaker's bar, and a bystander's, as big bars across the top of the screen.
   - `AnyOpen` is set in `Begin` and cleared in `End` (and reset when playing starts). Every counter conversation is affected, day or morning. Patience drains as before, the portrait shows the speaker's mood, and the bars come back when the conversation closes.
3. **A soft shadow behind the HUD prompt** (`ShopUI`, **Prompt Shadow**, on).
   - A street lamp behind "[E]  Take Barnaby" washed the name out. The prompt now has a TextMesh Pro underlay on its own runtime copy of its material: black at 80%, offset (0, -0.3), dilate 0.5, softness 0.6. Nothing is saved, and day prompts get it too.
   - Player builds keep the shader's `UNDERLAY_ON` variant because TMP's "LiberationSans SDF - Drop Shadow" preset, in a Resources folder, uses it.
4. **Grace's stand-in look** (`CustomerProfile`, `PolygonNpcVisual`, `Regular_Grace.asset`).
   - Every regular kept the actor's original body, the Quaternius "Beach" body: a young man in shorts. Grace's real look is Mansoor's girlfriend's drawing and his own Blender model (`Visual-Style-Lock-01-Character-Guide.md`), so this is a stand-in only, picked by him.
   - `CustomerProfile` has a **Stand-in Look** name (`standInLook`, `StandInLook`, and `IsStandInLook` over a registry filled in `OnEnable` / `OnValidate`). `Regular_Grace.asset`: `standInLook: Character_BusinessWoman`. Empty it to go back to the original body.
   - `PolygonNpcVisual.Start`: a regular wears only their own stand-in look. Walk-ins, patrons and street neighbours skip any regular's stand-in look (`SkipStandIns`: the next look along), so she stays recognisable. `ApplyAppearance` still refuses any other look for a regular.
   - Without the purchased art (a fresh clone of the public repository) nothing changes and she keeps the original body.

**Checks** (lab sessions):

- **Compile:** no errors, 67 warnings (as before). No warnings or errors in the play sessions.
- **Night 1 play checks:** keeping a straight face 60/60 (three runs), cracking 60/60, the morning after Day 2 61/61. The photos show her face clear, no bars in the close-up and her stand-in look. "[E]  Go on" was caught in a live screenshot of the meter's title.
- **15 gameplay checks PASS** (`Fixit Fidget > Checks`): human counter integration, customer delivery from either hand, customer memory and identity, Day 1 onboarding, Grace camera content, Grace camera interaction, continuation rules, Night 1 rules (264), recap input isolation, recap save checkpoint, storyteller interaction, waiting space, ticket layout, human rules, home rules.

**Seen, not changed:**

- **Grace's first line on Day 3 and after reads oddly:** "Today's patient is my Latte: broken." `CustomerIdentity` gives any returning regular's intake a repair callback (`returnMemoryLines`); on her drink-only visits the {device} is the drink and the {fault} is the default "broken". It was there before these fixes (the Day 2 variant check at 15:55 logged the same callback line). Mansoor will meet it in his own save when she comes in on Day 3.
- The lab's banner still overlaps the view hint (as before).

**Next:** Mansoor plays Night 1 (the lab entry, then his own save) and answers the three questions in `claude/night-1-slice.md` §9.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before.
- The scene shows as modified (`*`) after lab sessions. It wasn't saved, and git shows the scene file unchanged. `ShopUI`'s new Prompt Shadow field isn't in the scene file until the scene is next saved; its default (on) applies meanwhile.

## September 28 (evening) — Night 1: one night that changes the next morning (Grace's gnome, the straight face); on `night-1`

**Read this entry first.** One code commit, `2038a44`, on a new branch `night-1`, made from `sound` (`deec8bb`). This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone; he asked for this slice to be built before he plays. Every check here ran in lab sessions, and the playtest save was never written.

What he asked for (28 Sept): he pasted advice for one complete night that changes the following morning at the café. A customer mentions a prized garden ornament by day, Ace takes it at night as a trophy, the customer comes back the next morning to complain, and Ace has to keep a straight face; small enough to finish and play again and again. His words: "no need to draft, you can implement this too before i playtest". The choices made for him, said before building:

- Grace and her garden gnome, **Barnaby**. Every line about the gnome is a placeholder for Mansoor to rewrite, in `Assets/Scripts/NightThings.cs`. Keep the id `grace.gnome`: saves hold it.
- The night follows every day in the real game (`claude/ace-after-dark.md` §3.1), with a switch to turn it off.
- A lab entry that starts at Day 1's recap.

The as-built notes, how to play it and the placeholders are in the project doc `claude/night-1-slice.md` (new). `claude/ace-after-dark.md` (the loop table in §4 and build step 5 in §8, with notes in §3, §5, §7 and §9) and `claude/night-city-proposal.md` (new §10: part 5's ending, built with Night 1) are updated.

**The loop.**

1. **Day 1.** When Ace takes Grace's camera job she mentions Barnaby, "twenty years on the front step of the saffron house on the corner". The notebook notes it (a possession, told).
2. **The recap's button** reads **"Close up for the night"**. The screen goes dark ("Night 1"), the café is emptied (`DayClock.ClearTheShop`) and the night walk starts where Ace stands. The first night shows a note: the notebook (N), the torch (F), and E inside the café's door to call it a night.
3. **Barnaby** stands on the top step of Grace's stoop (12 West Street), beside her door, inside the railing. By day he's scenery.
   - At night, near him: "[E] Take Barnaby", or "Take the garden gnome" if she never mentioned him. A soft light catches him while he's the thing E would take.
   - E takes him: into the night's ledger, off her step for good (by day too), onto a new shelf on the café's back wall behind the counter, and into the notebook as Ace's secret (found).
   - If Ace didn't know whose he was, the notebook waits and learns it the next morning, when Grace says it.
4. **Calling it a night:** "[E] Call it a night" just inside the café's door. Otherwise dawn ends it at 4 AM on the night's clock, about 4 real minutes in. Either way: a fade and a caption, the night put away, Ace back behind the counter, and the recap's own Open Tomorrow saves the next morning with what the night did.
5. **The next morning Grace comes in first:** on Day 2 as the day's featured regular; on any other day as the morning's visitor (`CustomerSpawner`: each morning, the owner of a deed not yet faced comes in first, unless they're already the day's featured regular).
6. **At the counter** her first line is the complaint. 0.6 s after it has been read, the straight-face meter runs above the conversation's text; Space (X / Square on a pad) stops the needle.
   - Held: her thanks.
   - Cracked, or not stopped within 6 s: "Ace. Are you smiling?", one step more suspicion for her, and a note in the notebook that she's watching her step. Stars are never touched (reputation spec §5).
   - Her portrait shows her reaction.
7. **Then her usual visit goes on:** the reunion photo on Day 2, a regular's request on other days.

Nothing is saved during a night. Quitting mid-night comes back to the recap, and the night again.

**New** (`Assets/Scripts`):

- `NightLedger`: trophies; deeds (what, whose, which night, faced, cracked); suspicion per person; nights walked. Saved as `SaveData.night`, additively, with no version bump. Older saves load with an empty ledger.
- `StraightFaceMeter` (pure rules) and `StraightFaceUI` (on screen). Each thing sets its difficulty: for the gnome, a 1.1 s sweep, green 22% of the bar, 3% either side counted as near, 6 s patience. Where the green sits is rolled with the morning scene's own `System.Random`, never `UnityEngine.Random`, so it can't change who walks in.
- `NightThings`: the things Ace can take (one for now) and all their words.
- `NightInteractable` (offered only while the night runs), `NightTrophy` (the gnome, with its glint), `TrophyShelf` (shows what's been taken) and `NightDoorway` (call it a night).
- `NightCycle`: dusk, the doorway, dawn and the morning, with their fades, captions and notes. It also takes over a night begun from `Play the night walk (lab)`: the doorway and dawn work there too, and ending that night lets the lab's day go on.
- `MorningFace`: the morning scene, run inside `ConversationController`.

**Changed:**

- `RecapUI`: the night first, then tomorrow ("Close up for the night", `ContinueAfterNight`).
- `DayClock`: `RecapOwnsInput`, `ClearTheShop`.
- `PlayerMovement` and `CafeViewMode`: Ace walks at night after the recap.
- `PlayerInteractor` and `ShopUI`: the night's own prompts.
- `NightWalk`: `followsTheDay` (on). Off gives the old recap back.
- `CustomerSpawner` (the morning's visitor), `CustomerIdentity` and `CustomerBrain` (the mention), `ConversationController` (runs `MorningFace`).
- `NotebookHooks`, `SaveManager`, `SaveData`.
- `SoundBank`: 5 cues, silent until filled (`night.take`, `night.home`, `night.dawn`, `face.held`, `face.cracked`); 54 in all.

**The scene:** a new group "22 - Night 1 slice" with the gnome (`Night 1 - Barnaby the garden gnome.prefab`, simple shapes, with its cone mesh and colours in `Night 1 - Barnaby.asset`) and the trophy shelf (an oak board, brackets, and a hidden copy of the gnome in its slot). Both are placeholders for real models. Saving the scene also wrote `NightWalk`'s settings at their defaults (`followsTheDay`, and 4a's `nightOwls` and `owlsUntil`). The scene's diff is additions only.

**Tools** (`Fixit Fidget > Night`; editor script `NightOneSteps`, new):

- `Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene (Edit Mode)`.
  - It tries spots by Grace's door against a copy of everything solid there, in a preview physics scene: the whole footprint on one level, nothing in the way, off the line people walk out on, and at least 0.65 m from where people wait.
  - It refuses rather than place a gnome that clips. It picked the top step, 0.62 m to the side of the door.
  - Photos: `Logs/Night/night-one-setup-*`. Running it again rebuilds the group; Edit > Undo takes it out.
- `Night 1 - Take the gnome and the shelf out again`.
- `Night 1 - Check the scene (read-only)`.
- `Night 1 - Play from Day 1's recap (lab)`: a lab session (`playtest-cafe-lab.json`) on the recap of a made-up Day 1 on which Grace brought her camera in and mentioned Barnaby.
- Three play checks that drive themselves (lab sessions, about 70 s each; `Diagnostics/NightOneCheck`):
  - `Night 1 - Play check, keeping a straight face (lab, drives itself)`;
  - `Night 1 - Play check, cracking (lab, drives itself)`;
  - `Night 1 - Play check, the morning after Day 2 (lab, drives itself)`: like an older save, with no mention, so "Take the garden gnome", and Grace as the morning's visitor on Day 3.
  
  Ace walks out and back with scripted input, E goes through the game's own input, and Space is queued through the Input System as a player's press would be. Report and photos: `Logs/Night/night-one-check-*`.
- `Fixit Fidget > Checks > Night 1 rules` (editor script `NightRuleChecks`): the ledger, the meter, the gnome's words and the save format. Also run outside Unity by `Tests/NightRules`. Its `.csproj` is force-added, since `.gitignore` ignores `*.csproj`.

**Checks** (lab sessions):

- **Compile:** no errors, and no new warnings (67, as before).
- **Night 1 rules:** 264 assertions. `Tests/NightRules`: 262 rule and 2 save-format assertions.
- **The scene check:** ready for Night 1.
- **Play checks:** keeping a straight face 60/60; cracking 59/59 (suspicion 1, the notebook's note); the morning after Day 2 61/61. Each run checks the walk out and back without getting stuck, the prompts, the ledger, the shelf, her step empty by day, the morning's save on disk, her complaint, the meter, her reaction and her usual visit after.
- **Unchanged:** the night tour (540 m, never stuck), recap input isolation, recap save checkpoint, customer memory and identity, Day 1 onboarding, featured repair requests, Grace camera content, continuation rules, home rules, notebook rules.

**Found and fixed while checking:**

- The first placement put the gnome half on the bottom step, against the stoop's iron railing. The set-up now checks the whole footprint and tries the top step first.
- The meter first covered the first line of her complaint. It now sits just above the conversation's text.
- A 0.5 m gnome in the dark is hard to see from above: hence the glint while he's the thing E would take, and the mention saying where the step is.
- Taken without knowing whose he was, the gnome went into the notebook by name that night. The notebook now waits for the morning (`MorningFace` learns both facts when Grace speaks), and the note says "the garden gnome".
- `ConversationController`: a local `Sprite face` hid the new field; the field is now `morningFace`.
- `NightRuleChecks`: `Random` was ambiguous (UnityEngine and System); it now says `new System.Random(`.
- `NightOneCheck` used the obsolete `FindObjectsByType` overload with a sort mode (6 new warnings). It now uses `FindObjectsByType<T>(FindObjectsInactive.Exclude)`, and the count is back to 67.

**For Mansoor's playtest** (`claude/night-1-slice.md` §4):

- **Quickest:** `Fixit Fidget > Night > Night 1 - Play from Day 1's recap (lab)`. Press Close up for the night; out of the café's door, down to the front street and west to the corner; Barnaby is on Grace's top step. E, then back in through the café's door and E. On Day 2, go behind the counter and talk to Grace when she arrives. Space when the needle is in the green (or don't, to see the crack).
- **In his own save:** play Day 2 to its end, and the recap leads into Night 2. The prompt says "Take the garden gnome" (his Day 1 was before the mention existed). On Day 3 Grace comes in first.
- **The switch:** `Night Follows The Day` on `NightWalk` ("20 - Night walk"). Off, the recap opens the next day at once, as before.

**Next:**

1. Mansoor plays it (the lab entry, then his own save) and rewrites the placeholder lines in `NightThings.cs`.
2. Then his call: the rest of step 5 (a window to peek into, a prank), 4b (people and cars out and about), getting caught (step 6), or the sounds once the download is in. Making the mixer (the sound entry's Next 1) is still his step.

**Known:**

- From above at night the gnome is small, and Grace's house turns see-through as Ace stands at the step. The prompt and the glint carry it. A real model, or a bigger one, is an art call.
- The morning scene overlaps the speaker's chin slightly from the conversation camera.
- The café's door is the only way to call it a night; dawn is the backstop. The "tired Ace" cost of a late return isn't built.
- Suspicion is recorded but does nothing yet.
- Not in this slice: a window to peek into, a prank, being noticed, the meter's other scenes ("they get the story wrong"), an assist setting.
- The TextMesh Pro fallback font asset shows as modified after Play sessions. It was left out of the commit, as before. The scene shows as modified (`*`) after lab sessions without real changes.

## September 28 (afternoon) — sound, the plumbing: a sound bank, the ears at Ace, steps and a soundscape (silent until the files are chosen); on `sound`

**Read this entry first.** One code commit, `737c85a`, on a new branch `sound`, made from `night-walk` (`e965ee9`). This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone, and he hasn't played night walk parts 3 and 4a yet. Every check here ran in lab sessions, and the playtest save was never written.

The plan is the project doc `claude/sound-plan.md`. Mansoor's answers (28 Sept):

- Next thing to prepare: "Draft the sound plan (Recommended)".
- Order: "cafe first then night but remember the sounds have to sound and feel cozy and nice and unique".
- Download: "2018 + 2020, ~80 GB (Recommended)". He downloads the Sonniss GDC bundles himself, in his browser, to `D:\SoundLibrary\`, outside the project.
- Ears: "At Ace, turned with camera (Recommended)".
- When: "Now, before 4b (Recommended)". 4b gets its sounds as it's built.
- Plumbing: "Yes, build it (Recommended)".

**What this step builds.** Game code names a cue, and one asset holds everything else about it. Every cue stays silent until it has files, but each request is counted, so a check can show the hooks fire before any sound exists. A fresh clone of the public repository has no sound files at all and simply stays quiet.

- **The sound bank** (`SoundBank`, the asset `Assets/Data/Resources/Sound bank.asset`):
  - 49 cues for the café and the night, each with a note on when it plays;
  - per cue: its files, volume and pitch with a small wobble, placed or flat, how far it carries, the shortest gap between repeats, how many can play at once, and its bus;
  - the buses are World, Ace, UI, Ambience, Outside and Music. They're found by name in `Assets/Audio/Game.mixer`, which doesn't exist yet: Mansoor makes it by hand (see Next). Until then everything plays straight to the speakers;
  - `earHeight` 1.6 m and `stride` 0.72 m.
- **The player** (`SoundPlayer`, `Sfx`):
  - 24 pooled voices, made once;
  - its own random numbers, never `UnityEngine.Random`, so a sound can't change who walks in;
  - never the same file twice running; a soft roll-off from "near" to silence at "far";
  - loops fade in and out, follow what they belong to, and can be muffled;
  - while the game is paused (the recap) the world holds, the beds duck to 40% and the UI plays on;
  - game code calls `Sfx.Play("cup.set", position)`, `Sfx.Play2D(...)`, `Sfx.PlayLater(...)` or `Sfx.Loop(...)`. Components with their own AudioSource (the dispenser, the phones) use `Sfx.Choose`, which keeps their own sound until the bank has a file.
- **The ears** (`ListenerRig`):
  - overhead, the listener sits at Ace's head, turned with the camera's heading. Loudness goes by the distance from Ace, not from a camera 20–48 m up;
  - in first person, at a station, in a close-up or a conversation, it sits at the camera, as before;
  - there's always exactly one listener: the Main Camera's rests while the rig hears.
- **Ace's steps** (`AceFootsteps`): a step every stride walked (Ace is a capsule with no walk cycle). Inside the café it's the café floor. Outside, a short ray down reads the ground: road if its name or its parent's has a road word as a whole word (road, asphalt, crossing, lane, zebra, driveway, stall, aisle…), otherwise pavement.
- **The soundscape** (`CafeSoundscape`):
  - by day: the room, the murmur (it grows with the people in the café's room) and the street, muffled while Ace is inside;
  - at night: the city, busy at 11 PM and cross-fading to its late bed by 2 AM, and the closed café inside;
  - far-off one-offs at night every 20–60 s, fewer after 2 AM;
  - the door bell when anyone crosses the café's doorway, softer for Ace at night.
- **`SoundRig`** adds the ears and the steps to Ace and starts the soundscape while playing. All of it is made at run time; nothing is saved in the scene.
- **Hooks**, a line or two each: `BeveragePourAudio` (the pour, the machine under it, cup ready), `HoldCallPresentation` and `CounterRepairView` (the phones), `PlayerCarry`, `CupStack`, `ToolPickup`, `Screw`, `RemovablePart`, `ReplaceablePart` (Grace's camera shutter after its new part), `GrimeSpot` (its "Sparkling clean!" log is kept), `CircuitTile`, `CircuitPuzzle`, `CustomerBrain` (hand-overs, pay and tips), `TicketRailUI`, `NpcSeating`, `DayClock` and `RecapUI`.
- **`.gitignore`**: `/Assets/Audio/Licensed/` and its `.meta`. The Sonniss licence lets the game ship the sounds but forbids sharing the files.

**Tools** (`Fixit Fidget > Sound`; editor script `SoundSetup`, new):

- `Create or update the sound bank`: makes the bank with every cue, or adds only the missing ones. A cue already in the bank is never changed. It links `Assets/Audio/Game.mixer` once it exists.
- `Check the sound setup (read-only)`: the bank (cues with and without files, import advice for each file), the mixer's groups, the licensed folder, the ignore rule and the listeners. In Play it adds where the ears are, how far the dispensers and the phone are from them, Ace's steps and what was underfoot, the soundscape, and every cue asked for and heard. Report: `Logs/Sound/sound-check-*`.

**Checks** (lab sessions):

- **Compile:** no errors, and no new warnings.
- **A day** (`Play a lab session - Day 5, autopilot serves`, the whole day; `Logs/Sound/sound-check-2026-09-28_135223`):
  - the ears at Ace; the nearest dispenser 3.9 m from them, against 40.8 m from the camera where the old listener was;
  - asked for: the room, murmur and street beds; the bell 33 times; chairs 18 sits and 17 stands; 7 tickets; day open, last orders and closed once each; the recap.
- **The night** (`Night walk 3 - Walk the tour`; `Logs/Sound/sound-check-2026-09-28_140915`):
  - 473 steps over the tour's 342 m: café 46, pavement 21 (the entry apron, the sidewalk), road 406 (asphalt, crossing paint, the zebra);
  - the bell on each of Ace's 3 crossings of the doorway;
  - the night beds and 3 far-off sounds asked for; no opening bell;
  - frames unchanged: 4.2 ms on average, 95% under 4.4 ms (as in part 4a).
- **15 gameplay checks PASS** (`Fixit Fidget > Checks`): circuit integration, circuit rules, compact portrait tickets, customer delivery from either hand, Day 1 onboarding, dispenser interaction and visible filling, dispenser stock and two-hand transfers, every device's grime can be brushed, Grace camera tweezers interaction, human counter integration, recap input isolation, recap save checkpoint, support call rules, two-hand bench placement and carry state, waiting space reservations.
- **Not reached yet:** the autopilot skips the hands-on steps, so the pickups, tools, parts, hand-overs, money and phones haven't been counted in play. The check lists them once someone plays.

**Found and fixed while checking:**

- Unity's `FindObjectsByType` and `FindAnyObjectByType` never return objects marked `HideFlags.DontSave`. The first check said "Soundscape: not running" and counted no listener hearing, while both were running. Worse, `SoundRig.Fit` would have made a second soundscape on every scene load. Both are now found through `CafeSoundscape.Instance` and `ListenerRig.Ears`.
- The opening bell was asked for inside `DayClock.StartDay`, which also runs while the scene loads. It would have rung at the start of every night lab and before a restored recap. It now waits for the day's first frame (`DayClock.Update`) and rings only if the day is really open.
- The zebra's stripes counted as pavement. The road words are now whole words (so "Plane" isn't a lane) and cover the zebra, the driveways, the car park's stall lines and arrows.

**For the café's listening test:**

- The bell was asked for 33 times in one day, about every 9 seconds. Ringing on every coming and going may grate by the 50th time. The options: ring on the way in only, with a quieter door on the way out, or keep both at a lower level.
- Something to listen for now: today's pour (`SoftPour.wav`, 85% placed, silent past 4.5 m) now goes by Ace's distance in the overhead view. It's faint where the lab puts Ace (3.9 m away) and full with Ace at the dispenser. Before, from above, only its small flat share was heard, wherever Ace stood.

**Next:**

1. Mansoor makes the mixer (steps in `claude/sound-plan.md` §4.2), then `Create or update the sound bank` links it. The mixer and the bank go in one small commit.
2. When the download is done, he connects `D:\SoundLibrary` (reading is enough) and Claude shortlists 2–3 files per café cue for him to listen to (§7).
3. Then the café pass, the night pass, and 4b with its sounds.

**Known:**

- The TextMesh Pro fallback font asset shows as modified after Play sessions (it gathers glyphs as they're used). It was left out of the commit, as before.
- The scene shows as modified (`*`) after lab sessions. It wasn't saved, and git shows the scene file unchanged.

## September 28 (midday) — night walk part 4a: the lived-in street (the clock moves, windows go to bed, neighbours come home, a torch and the notebook); on `night-walk`

**Read this entry first.** One code commit, `10e65c4`, on `night-walk`. This entry comes in the commit after it. Nothing is pushed.

The Day 2 Grace/reputation playtest is still Mansoor's open milestone. Every check here ran in lab sessions, and the playtest save was never written.

Mansoor asked to start the next part, to assess what's done and improve it, to make it feel more alive, and to stick with the game's premise. He hasn't played parts 3 or 4 yet. His answers (28 Sept):

- Direction: "Asleep but lived-in (Recommended)".
- Hooks: "House numbers + street signs", "Notebook at night", "A pocket torch for Ace", "People glance at Ace". Glances need people about, so they're in 4b.
- Sound: he asked whether Higgsfield would do. It only makes speech, so the plan is the free Sonniss GDC bundles, kept out of git and planned with him first. When: "Right after part 4 (Recommended)".
- Night pressure: "Choose your risk (Recommended)". That belongs to part 5.
- Then: "Yes, start 4a (Recommended)".

**What this step builds:**

- **The night's clock moves** (`NightWalk`):
  - 11 PM to 4 AM over 4 real minutes (`nightMinutes`; a game hour is 48 s), then it holds at 4:00 AM (`nightEndsAt` 28) until part 5 decides how a night ends;
  - the HUD shows the moving time (`ShopUI`, from `NightWalk.ClockHour`);
  - `CafeDaylight` is given the hour every quarter hour.
- **The city's windows go to bed** (the 432 lit POLYGON building parts from part 1):
  - 30% lit at 11 PM (`cityLitAtStart`), each with its own bedtime, dark one by one by 3 AM (`cityLastLightsOut`), except 4% night owls (`cityNightOwls`);
  - 4 of them flicker blue like a TV (`cityTvWindows`);
  - a bedtime waits while its building is see-through (`NightSeeThrough.IsWorn`, new), so the fade can't undo it.
- **The houses round the café are lit room by room** (`NightHomes`, new; the rooms list is `NightRooms`, new):
  - each house keeps its window panes in one merged `Window glass` mesh and its curtains in one `Curtain glow`. The Edit Mode tool below splits them into rooms: 100 rooms in 17 houses (the 6 bay-window houses and 11 shop houses), numbered floor by floor. A bay's three panes are one room;
  - 45% of the rooms are lit at 11 PM (`homeHours.litAtStart`); downstairs goes out by 12:45 AM, and often a room upstairs comes on as it does (going up to bed); every room is out by 2 AM;
  - one night owl upstairs until 3:45 AM; 2 TV rooms (a blue flicker); 3 rooms where someone gets up later for a few minutes; the shop fronts stay dark;
  - a lit room is a copy of the house's glass material with a warm gradient glow, on a quad 4 mm in front of each pane. Its own curtains show lit or dark with it, and the merged evening curtains are hidden while the night runs. All made at run time, never saved;
  - **Grace's house:** one warm room on the first floor until midnight (`graceRoom`, `graceBedtime`). Nothing marks it.
- **Neighbours come home** (`NightNeighbours`, new). Three anonymous neighbours, set in `NightWalk.neighbours`:
  - the dusty rose house's at 11:36 PM, the lavender house's at 12:33 AM, the courtyard shop's at 1:40 AM;
  - each appears where the camera can't see, walks the pavement, goes up the step and in through their own front door (the doors open and close for them, as by day), and a downstairs room lights, later one upstairs;
  - their houses stay dark until they're home;
  - if their corner stays in view for 90 s they "come in the back way": no walk, but the light still comes on;
  - the bodies are the patron prefab (`PatronSpawner.PatronPrefab`, new) with its brain and interactions taken off. `NightNeighbours` refreshes `CafeArrivals.Players` itself (part 3's open point).
- **Broken things** (`NightFlicker`, new):
  - the old lamp at the back (`Old lamp (-17.1, 30.0)`) buzzes and blinks, and its glass goes dark with it (a dark cap kept in the rooms list);
  - a street lamp (`Lamp (-35.3, 26.9)`) stutters off and back on;
  - the donut shop's sign (`SM_Prop_LargeSign_Donut_01`, the south-west corner shops) cuts out now and then.
- **House numbers and street signs** (`HouseNumber`, `StreetNameSign`, new). Ordinary scene objects, seen by day too:
  - a small navy plaque with a cream number beside each bay-window house's front door, 12 to 22 up West Street. A regular's door shows its own number, so Grace's reads 12 (from `HomeDoor.houseNumber`); the others are placeholders;
  - at each of the 4 corners of the café's block, two green street-name blades on top of the signal post, e.g. `West Street / Front Street`. They read `District streets`, so renaming a street there renames the signs;
  - the lettering is lit (`Night walk - lit lettering.mat`, TextMesh Pro's URP Lit shader), so it takes the lamps' light at night.
- **Ace's pocket torch** (`NightTorch`, new): F, or the pad's West button (the day's station button, free at night):
  - a small warm spot light (12 m, 50°, no shadows);
  - from above it points the way Ace last walked, held at hand height and angled down, so the pool runs ahead on the pavement. In first person it points where Ace looks;
  - it's off when the night begins.
- **The notebook at night** (`NightNotebook`, new; `NotebookRecap.Page`, new): N, or the pad's D-pad up:
  - a page on the right of the screen, person by person in the order Ace met them, where they live first, then the rest. Guesses say "(likely)" or "(hunch)";
  - a dark page with cream writing. A paper-coloured first version washed out when the game view was small;
  - Ace can keep walking with it open, and the same key closes it;
  - it only reads the notebook. Nothing is written at night yet.
- **The café's entrance never fades** (`NightSeeThrough.NeverFade` gains `12 - authored cafe interior`). Part 3's "the entrance frame fades as Ace walks through the door" is fixed.
- **The HUD's hint** adds `F Torch    N Notebook` at night (`ControlHints.Torch` and `ControlHints.NotebookPage`, new).
- **The lab's notebook:** `Play the night walk (lab)` now writes the lab save with Grace's day-3 intake facts and her house ("likely"), so N has something to show. The playtest save is never used.

**Tools** (`Fixit Fidget > Night`; editor scripts `NightLivedIn`, new, and `NightWalkSteps`):

- `Night walk 4a - Build the lived-in windows (rooms list, Edit Mode)`:
  - writes `Assets/Playtests/AcesCafeLayout/Night walk - rooms.asset`: 133 meshes (panes, curtains, the lamp's dark cap);
  - made from the café's own `Street geometry` and `Street doors` assets, which are in git. No Synty file;
  - Edit Mode only: in Play, static batching has merged the meshes;
  - report: `Logs/Night/night-rooms-*`.
- `Night walk 4a - Put up house numbers and street signs (day and night, Edit Mode)` and `… Take the house numbers and street signs down again`: both undoable. Putting them up again replaces the old ones.
- `Night walk 4a - Check the lived-in street (read-only)`: the rooms list, Grace's room, the timetables, the neighbours' walks, the broken things, the plaques and signs. In Play it adds tonight's state. Report: `Logs/Night/lived-in-check-*`.
- In Play, in a night lab session:
  - `Photograph the night over the hours`: 3 views at 6 hours, then the hour is put back;
  - `Move the clock on an hour`;
  - `Send the neighbours home now`;
  - `Watch a neighbour come home`: a photo every 1.5 s until the light is on.

**Checks** (lab sessions; the playtest save was never written):

- **The night** (`Logs/Night/lived-in-check-2026-09-28_122411`, `night-hours-2026-09-28_121251`):
  - the clock ran from 11:00 PM to 4:00 AM and held there;
  - the city: 124 building parts lit at 11 PM, 12 (the night owls) at the end, 4 TVs;
  - the houses: 33 rooms lit at 11 PM, 0 at 4 AM. Grace's room went out at midnight. The 2 TV rooms, the night owl and the 3 wake-ups are in the timetable, and the photos over the hours show the street going to bed;
  - the broken things kept breaking (the sign cut out 20 times in one night);
  - the torch and the notebook opened and closed; the page showed Grace's 5 facts, her house "(likely)";
  - 0 warnings, 0 errors.
- **A neighbour coming home** (`Logs/Night/neighbour-watch-2026-09-28_122024`): the dusty rose neighbour walked 17 m of pavement; the door opened, they went in, the door closed, and the downstairs room lit. 24 game minutes (19 s) from setting off. Each of the three walked in through the door in at least one run. When the hours photos held the dusty rose corner in view, that neighbour came in the back way, as designed.
- **The tour** (`Logs/Night/night-tour-2026-09-28_120932`): 342 m in 70 s, 0 stuck spots; frames average 4.2 ms, 95% under 4.4 ms.
- **The day:**
  - the day photos differ from before only where the plaques and signs are: `views-day-2026-09-28_120349` (plaque 12, the plaques up West Street, two corner signs, the follow camera's corners) and the 15 standard views, `day-photos-with-night-group-2026-09-28_120501`;
  - a day lab session (Day 5, autopilot serves): 0 warnings, 0 errors.
- **The scene** was saved once, in Edit Mode, after the two tools: 6 plaques, 4 corner signs, the `NightWalk`'s new settings and its rooms list. Part 3's `CafeViewMode` fields are now written too, at their defaults. After later lab sessions Unity marks it changed again (Cinemachine's "Save During Play", as in part 3). It wasn't saved again.
- **Unity's asset database:** after the last day lab, Unity logged 48 errors from its own asset database, 16 each of `MDB_READERS_FULL`, "Asset database transaction committed twice!" and an LMDB assertion. The last was at 12:28. They come from the editor, not the project's code. Restarting Unity clears them.

**Seen, for Mansoor's test:**

- From the overhead camera the plaques are mostly hidden by the bays and the signs are small. Both read well close up and in first person. Painted kerb numbers would read from above; not built, his call.
- The lab's `CAFÉ LAB` label overlaps the HUD hint.
- 10 curtain pieces sit at no window; they're hidden at night.
- Nobody else is out yet, and there are no cars and no sound (4b and the sound step).

**Left open:**

- **Mansoor's test** of parts 3 and 4a: what he liked and didn't.
- **4b:** people out and about (a patrol officer, a shopkeeper, someone at the bus stop who leaves by taxi), a taxi and a patrol car with their headlights on, glances at Ace (`NpcLookAt`), moths at the lamps, steam and leaves.
- **The sound step,** after part 4: the Sonniss list, planned with him first, the files kept out of git.
- **Part 5:** getting to the night, calling it a night, dawn, "Choose your risk", the checks and the recording.
- As before:
  - the east street's lantern lights a tree green-yellow;
  - one tower has every window lit;
  - the two small patio air walls;
  - hardening the Café life check waits for his say-so.
- **Next:** 4a goes to Mansoor with photos and a short guide for his test.

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
