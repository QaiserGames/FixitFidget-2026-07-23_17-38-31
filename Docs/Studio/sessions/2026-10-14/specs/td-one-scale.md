# Technical Director: one scale at the bench

*Technical Director (Opus 5.5, max), 14 Oct 2026, playtest 5's "forgiving bench" session, wave 1b. Read at `playtest-2` (`fab65d3`, clone at `/root/cafe/repo`). Spec only: no code, no Unity, no Mac. Line numbers are from the clone. Framing numbers come from a projection model of the close-up (scratch, not repo code) checked against UI/UX's measured tray rect (x 1292, y 740: matched).*

## What he will see

Grace's camera on the mat is a real camera's size, and everything around it agrees with it: the screwdriver is longer than the camera is wide, the cups hold screws that fit them, the cloth is a cloth beside it. The camera fills about the same share of the screen as on 13 Oct (40 %), because the close-up now comes in closer for it. The phone fills more than before (38 % of the screen's height, was 30 %). The pocket watch, a real 5 cm watch next to 13 cm tools, fills less (19 %). The wheel or D-pad up zooms any device to 60–80 % of the screen; there a screw shows its cross, and the hole beside it is a small threaded opening with a bright lip, not a pit. Screws are 3 mm across, dark and crisp: under half the phone's old 8 mm cartoon heads. In the café, devices are their real size in Ace's hands in first person, and from above a carried device is still drawn large enough to see.

---

## 1. What I did

- Read the common brief, the addendum, my brief and role file, the Bible (§2, §4–§7), the hero spec, the reference sheet, bench-spec v2, the import guide, the five specs in, then Systems' spec (landed 07:44) and the feel sheet (landed 07:36).
- Read the Blender scripts and manifests, the three prefabs (dumped with a script), the scene's bench rig, close-up camera, lamp and quality assets, and every script that reads a size or distance at the bench or holds a device in the café.
- Built a projection model of the close-up: pitch, FOV, distance, aim, 16:9 and 16:10, UI/UX's zones U1–U6, Art's mat layout, and the three real-size devices at every quarter turn and both faces. Searched it for the best framing per device and per layout variant.
- Not done: no code, no Unity, no Blender. The framing numbers are the model's. Bench 9 (§6) recomputes them on the real meshes.

## 2. The decision and its reasoning

### 2.1 Recommendation: (a) real size everywhere, as real-size exports plus a one-time prefab bake

- **Blender** exports the camera and the watch at real size. One constant per script scales every mesh and child offset at export (camera ×½, watch ×⅓). The phone is already real (k = 1).
- **Devices 2** fits them and **bakes** the same k into everything else in each prefab:
  - child positions;
  - stand-in scales (grime spots, smudges, scratches);
  - its own hard-coded collider boxes;
  - the metric fields that are the device's (`restHeight`, `domeRadius`).
- **The close-up frames each device from data** (`BenchLayout`), and the zoom becomes a lens zoom (§5).
- **From above, a carried device is drawn at least 0.45 m long** (`PlayerCarry`), so the overhead read holds.
- The tools are already real and stay. So are the mat, the cups and the rest Art specified.

### 2.2 The four options compared

| | (a) Real size everywhere (recommended) | (b) Scale the device root only at the bench | (c) Scale the bench rig per device | (d) Permanent root scale k on the prefab (meshes stay 2×/3×) |
|---|---|---|---|---|
| **What changes** | 3 Blender scripts (one constant each); Devices 2's constants and a bake; `BenchLayout` camera fields; `ItemInspector`'s camera and zoom; `PlayerCarry`'s overhead size | A scale switch at `BeginInspection`/`Release` (and on the bench top), masking for the blend | Rig scale ×1/2/3; every tool and stage constant ×k; camera distance ×k | Two numbers in two prefabs |
| **What breaks** | Nothing structural. Old builder steps would re-write 2×/3× numbers if re-run (§6.4 guards them). `restHeight` and the lens dome must scale (bake does it) | **Visible seams.** The device shrinks in view during the 0.75 s blend in and grows back on the way out (`ItemInspector.cs:300`; Cinematics keeps the blend). It also lies on the real-size mat at 2×/3× in the walking first-person view until the close-up opens: the disproportion he complained about, one step away. `InspectableItem.Update` lerps the root back to its Awake scale every frame (`InspectableItem.cs:26`). `Screw.headSize` is cached in Awake at café scale (`Screw.cs:74`), so the driver's tip would float 0.9 mm (about 10 px zoomed) over the watch's screws. Loose pieces left in the cups stay at bench scale while their device leaves at café scale | **Visibly wrong.** The watch's rig would be a 1.8 × 1.08 m mat on a bench about 0.6 m deep (unverified depth), and the camera 1.74 m from the aim, above Ace's head. The real-size room beyond the mat (stools, customers, the glass screen) would read at ⅓ scale. The tools would be 40 cm in his hand. Every world constant in `ToolPickup`/`GrimeSpot`/`ItemInspector` would need ×k | `ScrewSocket.Make` mixes world and local units (`Screw.cs:341-366`), so the hole is drawn k× the head (⅓ on the watch). Meshes, manifests and scripts stay in 2×/3× units forever, a trap for every later step. Devices 2's rule "scale 1 = the mesh is real size" stops being true |
| **Cost** | Blender ~1 session (shared with Art's camera v3.6, needed anyway); Unity ~1–1.5 | Unity 1.5–2, plus two scales per device to maintain for ever | Unity ~1, but the result is broken | Unity ~0.5 + the local/world fixes |
| **Proof** | Scale audit before/after; Devices 1/2 reports; Bench 9; Bench 5; the clumsy hand before/after | Every lab at both scales, plus a seam check in the blend | n/a | As (a), plus a check that every lossy/local mix is fixed |

**Why the others lost:**
- **(b)** saves the café's oversized look, which is worth little. From the overhead (33–48 px/m, art spec B1) a 2× camera on the intake tray is about 12 × 6 px and a 3× watch 7 × 7 px: both under the 10-px rule on their smaller side already. The job tag is screen-sized (`JobMarker.cs:170-187`) and carries the read. Carried items are drawn ×2.2 anyway (`PlayerCarry.cs:68`). In first person a 15 cm pocket watch in Ace's hand is a toy tell, the thing he has rejected three times (Bible §4). And (b) pays for that with seams and a permanent second scale.
- **(c)** fails on screen.
- **(d)** is cheaper by about half a session and leaves the trap.

### 2.3 Physics, masks, grips, light, near clip (the risks the brief names)

- **Physics:** none left in the core loop after Systems (their §4.0.3). Rule: no rigidbody on any bench piece under 2 cm. At that size PhysX's 1 cm default contact offset (`Physics.defaultContactOffset`) is bigger than the piece, and tiny bodies jitter.
- **`lossyScale` reads:** with the root at scale 1 after the bake, lossy equals local for every hero piece. The grime spots keep consistent non-unit scales. Clean.
- **Grime masks:** 64 texels across a spot (`GrimeSpot.cs:46`). Default view, at real size:
  - the phone's 36 mm spot at 3.14 px/mm = 113 px (1.8 px a texel);
  - the watch's 16.7 mm spot at 3.55 px/mm = 59 px;
  - the lens's 19 mm spot at 3.18 px/mm = 60 px.

  Fine. At the zoom limit the phone's and the watch's spots reach 3–4 px a texel: soft, acceptable for dirt. Keep 64. 128 is an option if the zoomed photos look mushy (+48 KB a spot, about 4× the erase loop).
- **Tool grips:** tools are unchanged. One thing gets worse at closer framings: the in-hand lean is 18° toward the camera (`ToolPickup.cs:54`). It points the driver's handle within about 14° of the view ray, so the handle hides the screw it is turning. Systems is rewriting `Follow` for the direct grab; their rule should lean the handle toward the screen's upper right (Ace's right hand), at least 30° off the view ray (§6.7).
- **Light and shadow:** the bench lamp casts no shadows (scene `:254144`, `m_Shadows m_Type 0`). The PC preset's first shadow cascade covers 6.15 m on a 1024 px tile (`PC_RPAsset.asset:57-64`): about 6 mm a texel at the bench. Sub-centimetre parts already have no meaningful shadow, and real size changes nothing there.
- **Near clip:** the close-up's near plane is 0.05 m (scene `:223419`). A dolly zoom to the watch would put the driver's handle top at about 0.03 m from the eye: clipped, and filling the screen. The lens zoom (§5) keeps the camera 0.42–0.49 m out, and the nearest tool point at 0.28 m or more.

## 3. The scale audit

Real sizes are from the reference sheet (§ given) unless named. "Where it lives" is every place a change must reach.

| Item | In game today | Real | Factor | Where it lives | After |
|---|---|---|---|---|---|
| Phone body | 70 × 140 × 7.8 | iPhone 15: 71.6 × 147.6 × 7.80 (§6) | 0.98 × 0.95 × 1.0 | `hero_phone.py:94`; Body's 5 boxes `HeroDeviceSteps.cs:90-97`; prefab | unchanged (k = 1) |
| Phone screws ×2 | head Ø 8 × 1.6 | 1.5–2.5 (§6: pentalobe Ø 1.5) | 3.2–5.3× | `hero_phone.py:448-465`; head box `HeroDeviceSteps.cs:109`; prefab colliders | **Ø 3.0 × 1.05**, black oxide |
| Phone hole | grey disc Ø 8, near-black core Ø 3.6 (0.9 × r as the core's diameter), radius floor 2 mm | bore ≈ the thread, 1.4–1.6 | ~2.5–5× | `Screw.cs:341-361` (floor `:341`) | decal: bore Ø 1.44, lip to 1.65 (Art A8) |
| Phone screen, cover | 62.8 × 132.8 × 1.2; 67.6 × 137.6 × 1.2 | ≈ 64 × 140 | ≈ 0.96 | `hero_phone.py:204-226, 403-445` | unchanged |
| Phone grime ×3 | Ø 36 | a patch | — | prefab scale `.036`; `BenchFeelSteps.cs:64-74` | unchanged |
| Camera body | 240 × 160 × 46 | Canonet 120 × 75 × 32 (+28 lens) (§8) | 2.0 × 2.1 × 1.44 | `hero_camera.py:68`, docstring `:4` "2x" | 120 × 80 × 32 (Art A6 deepens) |
| Camera lens barrel | Ø 76, 45 proud | Ø ≈ 50–52 at the body, 28 proud (§8) | 1.46 × (Ø) | `hero_camera.py:254-281`; capsule r .0387 | Ø 38 at ½: **still 0.73× real: Art's call** |
| Camera strap | bands 83 × 17, loop 140 × 18 | 12–15 wide (§8) | 1.1–1.4× width | `hero_camera.py:422-447` | Art A6 rebuild, 14 wide |
| Camera mechanism (part) | chassis 73 × 49 × 30; box 79.5 × 34.5 × 49; `seatDistance` .016 | leaf shutter Ø 30–35 | ≈ 2× | `hero_camera.py:368-388`; prefab | 36.5 × 24.5 × 15 (+ Art A7's clean copy) |
| Camera grime ×3 | lens 38 × 39, film path 28 × 33, guide 20 × 32; lens dome r .0862 | front element Ø 24 (`hero_camera.py:288-289`) | 2× | prefab scales; `BenchFeelSteps.cs:173` | 19 × 19.5, 14 × 16.5, 10 × 16; dome .0431 |
| Watch case | Ø 150, 30.5 thick with back | Ø 48–52; 12–16 thick (§7a) | 3.0 × 2.2 | `hero_watch.py:68-74`, docstring `:5` "3x"; boxes `HeroDeviceSteps.cs:131-133` | Ø 50 × 10.2 |
| Watch screws ×4 | head Ø 12 × 2.6 | case screws Ø 1–3 (§7a) | 4–12× | `hero_watch.py:333-348`; `HeroDeviceSteps.cs:137` | **Ø 3.0 × 1.05**, blued |
| Watch hole | grey Ø 12, core Ø 5.4 | — | — | `Screw.cs:341-361` | decal: bore Ø 1.44 |
| Watch back plate, crown | Ø 130; Ø 24 | ≈ Ø 43; Ø 7–9 | 3× | `hero_watch.py:299-330` | Ø 43; Ø 8 |
| Watch mainspring (part) | coil Ø 52 × 2.5 | ≈ Ø 15–17 | 3× | `hero_watch.py:351-374` | Ø 17.3 |
| Watch grime ×3, smudge | Ø 50; Ø 45 | a patch | 3× | prefab; `BenchFeelSteps.cs:130-133` | Ø 16.7; Ø 15 |
| Screwdriver | 134, blade Ø 3.0, tip cross 2.6 | PicoFinish PH00: 134, Ø 2.5, cross ≈ 1.5 (§1) | 1.0; 1.2; 1.7 | `bench_tools.py:55-83` | 134, Ø 3.0 (PH0 class), **cross 1.8** |
| Tweezers, brush, pry | 120; 140 (head ≈ 24); 125 (blade 8 × 0.8) | 110–140; 140–230; 125 (8 × 0.5) (§2–4) | 1.0 | `bench_tools.py:86-189` | unchanged |
| Cloth | 75 × 90 folded, 2.1 mm a layer | 15 × 18 cm lens cloth, 0.8 a layer (§5) | 1.0; 2.6× thick | `bench_tools.py:192-288` | Art A4 (thinner, held states) |
| Mat | 600 × 360 × 6 slab | 450 × 300 × 5 common; 550–600 exist | 1.0–1.3 | `Playtest3Session4Steps.cs:103` | Art's `BM_RepairMat` 600 × 360 × 5 |
| Tray / caddy | 170 × 170 × 24 tray; 120 × 35 × 80 block | — | — | `:117-136`; `BenchStage.cs:49-54` | retired: cups Ø 18, pads, wells, scrap tray 160 × 90 × 22, rest 110 × 230 (Art A1–A3) |
| Close-up camera | FOV 40, pitch 58°, 0.58 m, near 0.05 | — | — | `Playtest3Session4Steps.cs:42`; scene `:223417-223419` | per device (§5) |
| Zoom | dolly ×1.2 … ×0.42 | — | — | `ItemInspector.cs:108, 933-950`; scene `:155829` | lens zoom per device (§5) |
| Bench code, world metres | brush 4.8 / cloth 9.5 mm; `HeldLift` .04; `seatDistance` .016; `snapDistance` .03; pop .6/.16 m/s; `threadLength` .006; catch .25 | — | tuned for 2×/3× | `GrimeSpot.cs:45`; `ReplaceablePart.cs:34,39`; `RemovablePart.cs:51`; `Screw.cs:23,30-32`; `BenchStage.cs:33` | retired or made relative by Systems (their §4) |
| Rest height | phone .010, watch .025, camera .004 | — | — | prefabs; `Playtest4ProducerSteps.cs:34`; `GraceShowcaseSetup.cs:132` | .010 / .0083 / .002 |
| Carried from above | ×2.2 | — | — | `PlayerCarry.cs:68, 202` | ≥ 0.45 m long (§6.8) |

**The common brief's numbers, confirmed and corrected:**
- **Phone 1×:** confirmed (95–98 % of an iPhone 15).
- **Camera 2×:** confirmed across the face. Its body depth is only 1.44× (the "third too flat" of hero spec §6), and its lens is undersized at 0.73× real.
- **Watch 3×:** confirmed in diameter, 2.2× in thickness.
- **Tools real:** confirmed. The driver's blade is PH0-sized (Ø 3.0, not PH00's 2.5), and its tip cross (2.6 mm) is wider than any screw at real proportion will take.
- **"An 8 mm black pit":** strictly, it is an 8 mm dark-grey disc with a 3.6 mm near-black core. A 2 mm radius floor also means no hole is ever drawn under 4 mm (`Screw.cs:341`).

## 4. Screws and holes: sizes and the screen math

**Sizes:**
- **Both devices: head Ø 3.0 mm, height 1.05 (0.35 × Ø), shank Ø 1.65, length 6.0.** That is Systems' 2 × head (their §4.1.5).
- **The recess cross is 1.8 mm, the driver's tip** (0.6 × head).
- **The hole is Art's decal:** a quad 1.15 × head = 3.45 mm; bore Ø 1.44 (0.48 × head); lip to Ø 1.65; dip to 2.7; witness ring to 3.0.

**Why 3.0:** it is a real M1.6 cheese/pan head (DIN 84 / ISO 7045: 3.0–3.2 mm), exactly what a PH0 precision driver like ours fits. It is 4.2 % of the phone's width and 6 % of the watch's diameter. ReStory's are about 7 % of its phone (feel sheet §4).

**Screen math** (px/mm at the screw face; 1440p = ×1.333; default = the layout view, §5):

| | default 1080p / 1440p | at the 60 % stop | zoom limit |
|---|---|---|---|
| **Phone** px/mm | 3.14 / 4.19 | 4.98 / 6.64 | 6.75 / 9.00 |
| Head Ø 3.0 | **9.4 / 12.6 px** | 15.0 / 19.9 | 20.2 / 27.0 |
| Hole bore Ø 1.44 | 4.5 / 6.0 px | 7.2 / 9.6 | 9.7 / 13.0 |
| **Watch** px/mm | 3.55 / 4.73 | 11.2 / 14.9 | 11.7 / 15.6 |
| Head Ø 3.0 | **10.6 / 14.2 px** | 33.6 / 44.8 | 35.2 / 46.9 |
| Hole bore Ø 1.44 | 5.1 / 6.8 px | 16.1 / 21.5 | 16.9 / 22.5 |
| *Today (dolly 0.58 m)* | phone head 8 mm = 20.7 px, core 9.4 px; watch head 12 mm = 32 px, core 14 px | | |

**What reads:**
- **At default** a head is a dark 9–11 px disc with a lit rim, inside UI/UX's glow ring (radius ≥ 14 px), and its hole a 5 px point with a bright lip. That is how a real screw looks at arm's length, and the fix for "ENORMOUS holes": the dark area drops from the head's full width to 0.48 of it.
- **The cross shows from about 12 px:** at 1440p by default, and anywhere past the first zoom notch.
- **Hit areas are Systems':** screw radius max(28 px, projected head + 10 px), independent of all this.

**Rejected:**
- **2.4 mm** (Art A8): truer, but only 7.5–8.5 px by default.
- **3.8 mm:** meets the feel sheet's T8 (≥ 12 px at 1080p default). It is M2, 5.3 % of the phone's width, bigger than our driver fits: "don't make the screws enormous".

The size is one constant per script.

## 5. Close-up framing per device

### 5.1 The settlement (Art 38/38/24 vs U1–U6 vs "about 60 %")

1. **Art is right.** At real proportion a framing that shows the job's tools and compartments, clear of U1–U5, caps the device at about 38–40 % of the frame's height (phone, camera) and 19–25 % (watch).
   - At 60 % the frame sees only about 237 mm of mat depth around the phone (1.6× its projected length) and 126 mm around the watch. A 120–140 mm tool can't lie beside the device.
   - The phone's 166 mm bays don't fit the 440 px band between U3 and U5.
   - Model results, every pose and both aspects: phone **37.8 %**, camera **39.9 %**, watch **19.0 %**. That is Art's search under stricter rules: both faces, 16:10, 20 px margins, and the watch's small well included.
2. **Nothing else moves the cap:**
   - Pitch 50–74° and FOV 30–40° change it by ≤ 1 point.
   - Relaxing U5 to its permanent footprint (hint and chips) gains 1–2.
   - A shallower, wider tool rest makes the camera worse (43.6 → 40.9 %).
   - Small bays beside the cups gain the watch 2 points.
   - **The binding items** are the tool rest's 230 mm depth (the watch with only the rest in view: 25 %) and the width of the job's targets.
3. **UI/UX's zones stand as the contract**, unchanged.
4. **The brief's "about 60 %" becomes the zoom.** Every device's zoom runs from the layout view (default) to a close view of 60–80 %.

**Fill is measured as height:** the device's screen box at the pose it opens in (yaw 0, face up), at 16:9, ÷ 1080. Height is the frame's short side, and the zones make it the binding one; width swings with shape (the camera is wide).

### 5.2 The framings (pitch 58° and default FOV 40° kept: one camera language with Cinematics)

| | Layout view (default, zoom 0) | Zoom limit (zoom 1) | 60 % stop |
|---|---|---|---|
| **Phone** (in view: rest, cups, pad, well, scrap) | aim (+10, −14) mm, **0.48 m**, FOV 40 → **37.8 %** (today 29.6 %) | aim ≈ device +(21, −9), 0.48 m, **FOV 19°** → 80 % | FOV 25.8° |
| **Camera** (rest, `Pad_Small`, scrap) | aim (−58, −8), **0.49 m** → **39.9 %** (today 39.1 %) | aim ≈ device +(21, 6), 0.50 m, **FOV 27°** → 62 % (the strap at quarter turns binds) | FOV 27.2° |
| **Watch** (rest, cups, `Pad_Small`, `Well_Small`, scrap) | aim (+10, −20), **0.42 m** → **19.0 %** (today 40.1 %) | aim ≈ device +(9, −3), 0.43 m, **FOV 12.5°** → 64 % | FOV 13.1° |

- **Aims** are in Art's mat frame (mm; +x the player's right, +y away), converted through Systems' `BenchStage.matFrame`.
- **Distances** are along the 58° view from the aim.
- **Every target in view:** in the layout view each device's slots and the tool rest are in frame and clear of U1–U5 at 1920 × 1080 and 2560 × 1600. The device is clear of U1–U6 at every quarter turn and face at both zooms.
- **A flight may land off-frame only when zoomed** (zoom > 0). Systems accepted this (their §8: "its slot is in the default frame").

### 5.3 The zoom (replaces the dolly)

- **z ∈ [0, 1]; 0 on open.** The old −1 "zoom out past the default" goes: there is nothing more to see.
- **Magnification** m(z) = M^z, where M = tan 20° ÷ tan(fovClose ÷ 2): phone 2.2, camera 1.5, watch 3.3. **FOV** = 2·atan(tan 20° ÷ m).
- **The aim** moves from the layout aim to the close aim by z. **The camera** stays at the layout distance.
- **Input:**
  - one wheel notch is ±0.25;
  - D-pad ↕ moves 0.8 a second;
  - smoothing at today's 12 /s.
- **Why a lens zoom:**
  - The near clip stays safe (§2.3).
  - The tool in hand stays about 1.3× the device's scale instead of 2.6×.
  - The lamp, the shadows and the perspective don't change as you zoom.

## 6. The mechanism (what changes, file by file)

1. **`Tools/Blender/hero_lib.py`:** `scale_build(made, children, k)`. It scales every mesh's data and every child's location by k before export.
2. **`hero_camera.py` (v3.6), `hero_watch.py` (v3.6), `hero_phone.py` (v3.7):**
   - `AUTHORED_SCALE` = 2.0 / 3.0 / 1.0, applied in `build()` as `scale_build(…, 1/AUTHORED_SCALE)`.
   - Docstrings say "authored at 2×, exported real".
   - New parts (Art A6/A7) may be authored at the script's scale or the camera re-authored at 1.0: the Blender lead's choice.
   - **The contract:** the FBX is real size, 1 unit = 1 m.
   - Screws at §4's sizes, black oxide (phone) and blued (watch).
   - `bench_tools.py`: the driver's tip cross 2.6 → 1.8 mm (`:62-63`).
   - `BT_ScrewHole.png` (Art A8).
3. **`export_hero_devices.py`:**
   - `devices.json` gains `"units": "real"` and per-device `"authoredScale"`.
   - A `REAL` table (the §3 real sizes, ±7 %) counts any piece outside it as a problem.
4. **`Assets/Editor/HeroDeviceSteps.cs` → "Devices 2 - Fit the hero devices at real size":**
   - **Real constants:**
     - the phone's screw head box (.003, .00105, .003);
     - the watch's three case boxes and its head box ÷3 (`:131-137`);
     - its grime-spot move ÷3 (`:146`).
   - **The bake, per device** (phone 1, camera ½, watch ⅓):
     - every descendant's `localPosition` × k;
     - `localScale` × k **only** on non-hero objects shaped by their own scale (a non-unit scale: grime, `Dookie`, smudges, scratches);
     - grouping nodes (`KEEP - scratched sentimental strap`, `Screen`) and hero pieces keep scale 1, or their children would be scaled twice;
     - the `JobMarker` subtree skipped (it sizes itself on screen);
     - `JobBase.restHeight` × k; `GrimeSpot.domeRadius` × k; `seatDistance`/`snapDistance` × k if Systems keeps them.
   - **Guard:** sets `DeviceDefinition.realSize = true`; a second run reports "already real" and changes nothing.
   - **Old builder steps** that write 2×/3× numbers refuse to run on a real-size prefab (2–3 lines each):
     - `BenchFeelSteps.cs:130-133, 173`;
     - `GraceShowcaseSetup.cs`;
     - `FaultExpansionSetup.cs`;
     - `Playtest3Session3Steps.cs`;
     - `Playtest4ProducerSteps.cs:34`.
5. **`DeviceDefinition.BenchLayout`** (Systems' struct) gains `cameraAim` (mat mm), `cameraDistance` (m), `closeAim` (mat mm) and `fovClose` (°). Not save fields: prefab data.
6. **`Assets/Editor/BenchFramingSteps.cs` (new) → "Bench 9 - Frame each device (prefabs)":**
   - **The search:** maximise the device's height at zoom 0 (FOV 40, pitch 58) over aim and distance.
   - **Subject to:**
     - every slot the device's faults use (Systems' slot rule) and the tool rest in frame and clear of U1–U5;
     - the device clear of U1–U6 at every quarter turn and face;
     - both aspects;
     - 20 px margins.
   - **Then** bisect `fovClose` (the device alone, every pose).
   - **It reads:** the real meshes, `BenchStage`'s slots, and a new `Assets/Scripts/BenchStageContract.cs` (U1–U6 per aspect, UI/UX's numbers; shared with Bench 5 and 6).
   - **Output:** the fields, plus photos at z 0 and 1 at both aspects with the zones drawn, in `Logs/Bench/framing-<time>/`. One undo group per prefab; idempotent.
   - (Systems' new steps are Bench 7 and 8.)
7. **`Assets/Scripts/ItemInspector.cs`** (Systems' file; the TD's rule):
   - `BeginInspection`: place `CM_InspectCam` from `BenchLayout` (aim → world via `matFrame`, pitch 58, distance) with FOV 40 **before** `Priority = 30` (`:300`), so the blend lands right.
   - `HandleZoom` (`:933-950`) becomes §5.3.
   - `Release` (`:382-390`) restores the scene's pose and FOV 40.
   - The serialized `zoomRange` (scene `:155829`) is replaced by a renamed field: a changed meaning on a field the scene holds keeps the old value (Bible §7).
   - **Tool lean** (with Systems' direct-grab `Follow`): the handle tilts toward the screen's upper right and stays ≥ 30° off the view ray, so a tip never hides its own target.
8. **`Assets/Scripts/PlayerCarry.cs`:**
   - From above, the scale is max(`overheadScale`, 0.45 m ÷ the device's largest bound at pickup, `held.visualBounds` `:269`), devices only (not `DrinkJob`), capped at 7×.
   - Carried at 36–42 px/m: every device 0.45 m, 16–19 px (camera ×2.6, watch ×6.4, phone ×3.0). Today: camera with strap 26–31 px, watch 17–19, phone 11–13. The camera reads smaller than today, the phone larger, the watch about the same; all clear the 10-px rule.
   - First person stays real (`:201-202`).
9. **The bench's spare slots** (Systems' Bench 7 moves them off the mat). Put them **in front of the mat**, toward Ace: mat y −230 … −300, x −150 and +120.
   - Every layout frame's bottom ray meets the mat plane at y −162 … −177, so anything nearer to Ace is out of every frame.
   - Not right of the mat: the frames reach x +386 there, and the bench ends about 6 mm past the mat's right edge (world x −5.334 vs −5.34, unverified).
   - Not behind it: the frames reach y +215, and there is about 38 mm of bench beyond the mat (unverified).
   - Environment confirms in the scene.

**Data that changes:**
- the three FBX and `devices.json`, `BT_Screwdriver.fbx`, `BT_ScrewHole.png`;
- `PhoneRepair.prefab` (screws only), `PocketWatch.prefab`, `GraceReunionCamera.prefab` (baked);
- each `BenchLayout`;
- the scene's `ItemInspector` zoom field.

**No save field changes:** `SaveData` holds no device geometry (`SaveData.cs:128-283`), and the bench's state isn't saved.

## 7. The stage plan

| Stage | Who, where | What moves | Proof (pass looks like) |
|---|---|---|---|
| **0. Exports** (now, parallel, cloud) | Blender lead, with Art's camera v3.6 (strap, body, clean mechanism) | §6.1–6.3, plus review renders: each device on the mat at its layout and zoom-limit views, both aspects, U1–U6 drawn | Export audit `0 problem(s)`; manifest sizes inside the `REAL` table; renders read by Art |
| **1. Before** (start of Unity A, ~0.25 session) | Producer | `Assets/Editor/BenchScaleAudit.cs` → **Fixit Fidget › Checks › Bench scale audit (read-only)**: §3's table measured in Unity, against real sizes and ratios (device ÷ driver length: phone 1.10, camera 1.31 with strap, watch 0.49), plus `BenchStageContract.cs` | Runs and records; camera, watch and screw rows FAIL by design (the "before") |
| **2. Systems A1** | Systems | Their steps 1–6 and 14 (rules, flights, the stage with stand-ins at Art's real coordinates) | Their Bench 3 acts one and three. The phone is k = 1, so these are scale-proof |
| **3. One scale** (~1–1.5 sessions) | Producer, with the Blender output copied in | Devices 1 → Devices 2 (real size + bake + guards) → `BenchLayout` fields → Bench 9 → `ItemInspector` camera and zoom → `PlayerCarry` → spare slots | Audit **after: all PASS**: pieces within ±7 % of real, screws 3.0 ± 0.1, decal 1.15 × head, every child at before × k ± 0.1 mm. **Bench 9:** zoom 0 ≥ 36 / 38 / 18 % (phone / camera / watch); zoom 1 ≥ 75 / 58 / 60 %; slots and rest in frame, zones clear, both aspects. **Bench 5** (Systems' rows + these). **Intake tray check:** each device 0–1.2 mm over the felt. Stations, Day 1 guide, Grace tweezers and content, Juice, Aim help green. Photos: no part of Ace in frame; no tool tip hiding its target |
| **4. Systems A2–A3** | Systems | Cleaning calibrated on the real spots; the clumsy hand | Their bars, at 60 and 15 fps; per-step times within +20 % of an A1 run unless Fitts's widths changed (reported) |
| **5. Unity B, C** | UI/UX; Cinematics | Bench 6's overlaps; the parked bench shots re-framed | Bench 6; Art passes the shots |

**Order: Systems' A1 first, the scale second, A2–A3 after.**
- **A1 first:** it deletes the physics (pops, magnet, `seatDistance`) that real size would mis-tune. Its labs run on the phone, which doesn't change.
- **The scale lands before A2 and A3**, so Systems calibrates cleaning on the real spots and the clumsy hand measures the final framing.
- **Scale first** would re-tune physics that A1 deletes one session later.
- **Scale last** would let the 2× camera and 3× watch overlap Art's real-coordinate cups and rest through A2–A3, and every number would be measured twice.

**Sessions:** Blender about 1 (shared with Art's camera rebuild); Unity 0.25 + 1–1.5. That is Systems' "+1 session".

## 8. What a stranger's PC needs

- **The scale itself:** free. The same meshes, smaller; about 5k triangles added by Art's strap and mechanism.
- **Hole decals:** at most 4 alpha quads.
- **The lens zoom:** free.
- **Grime masks:** unchanged at 64.
- **Anti-aliasing: the one real cost.** The game renders with **no anti-aliasing**: the main camera's `m_Antialiasing: 0` (scene `:49569`) and URP `m_MSAA: 1` (`PC_RPAsset.asset:28`). At real size the close-up's smallest high-contrast features will crawl as the device turns: 9–11 px heads, 5 px hole lips, 0.5 mm grid lines (1.5 px). If stage 3's turning photos show it, add SMAA on Medium/High and FXAA on Low in `QualityPreset`. Cost about +0.6–1.0 ms at 1080p on a GTX 1660-class card (unverified; `PerformanceCheck` measures before and after).
- **The cloth's blend shapes** (Art/Systems): turn **Import BlendShapes** on for that FBX only. `BT_Cloth.fbx.meta` has `importBlendShapes: 0` today.
- **Steam Deck** (1280 × 800, 16:10): default heads are 7–8 px. The zoom matters more there; it belongs on the Deck checklist.

## 9. What can break, and how we'd know

| Risk | How we'd know | Answer |
|---|---|---|
| A prefab child missed by the bake (floats, sinks, a spot off its surface) | Audit "after": child at before × k; Bench photos | Fix the bake's list |
| An old builder step re-run writes 2×/3× numbers | The step refuses (`realSize`); the audit drifts | Guards (§6.4) |
| The lens smudge floats at its edges (dome not halved) | Bench 4's lens photo; the audit's `domeRadius` row | The bake scales it |
| A compartment under U3/U5 at 16:10 | Bench 9 / Bench 5 name the rect | Re-run Bench 9; never move the card |
| The strap's new drape changes the camera's bounds, and its rest centre with them | Bench 9's rects; Systems' slot coverage | `BenchLayout.rest` is the footprint's middle including the strap (Art and Systems agree it) |
| A Systems rule hides a metre constant | The clumsy hand before vs after | Make it relative (their contract) |
| Small parts crawl while turning | Turning photos | SMAA (§8) |
| A tool tip hides its screw | Photos at zoom 0 and 1 | The lean rule (§6.7) |
| The new FOV fights the blend | Bench 5's first-frame photo | Set the lens before raising priority (§6.7) |
| The watch's 19 % reads as "too small" | His playtest 6 notes; the clumsy hand's watch time | His open question 1 |

## 10. Decisions his to overrule

1. **Real size everywhere:** in Ace's hands in first person and on the intake tray too, not only at the bench.
2. **The default framing is the layout view** (38 / 40 / 19 %); 60–80 % comes from the zoom.
3. **Fill is measured by height**, at the pose it opens in, 16:9.
4. **The zoom is a lens zoom** from a fixed distance, not the camera moving in (bench-spec v2 §2.5's dolly).
5. **Screw heads 3.0 mm on both devices.** Holes are Art's decal.
6. **The driver's tip cross is 1.8 mm**; the blade stays Ø 3.0.
7. **The watch opens at 19 %**, not at the feel sheet's 1.5×. A 1.5× open puts its small well off-frame, against T13.
8. **From above, a carried device is at least 0.45 m:** the watch is drawn about 6.4×, the phone 3×.
9. **Spare bench slots go in front of the mat**, toward Ace.
10. **No AA change unless the photos crawl.**

## 11. Checks

New:
- **Fixit Fidget › Checks › Bench scale audit** (edit, read-only).
- **Fixit Fidget › Bench › Bench 9 - Frame each device** (edit, with its report).

Changed:
- **Bench 5:** Systems' rows plus §7's framing rows.
- **Intake tray check:** the rest heights.
- **Devices 1/2:** real-size constants.

Re-run green: Bench 3, Stations, Day 1 guide, Grace tweezers and content, Juice, Aim help, the clumsy hand.

**What to watch in playtest 6** (timestamped):
- Does anyone zoom, unprompted, on the watch?
- Does he call the screws "tiny" or the holes "big"?
- Does the camera look like a camera in his hands?

## 12. Open questions (his)

1. **The watch by default at 19 % with everything in view (my pick), or closer, with its tools and small well starting off-screen?**
2. **Screws 3.0 mm (my pick), or 3.8 mm?** 3.8 meets the feel sheet's 12 px at 1080p and is 5 % of the phone's width.

## 13. Flags

- **Locks:**
  - No new mechanic: the zoom exists; this changes how it frames.
  - The camera architecture is unchanged: `CM_InspectCam` stays the close-up; its pose and lens become per-device data.
  - "Objects are objects": untouched.
  - No save field and no upgrade name touched.
- **Superseded:**
  - Hero spec §2 "the watch at 3×, the camera at 2×, the phone at 1×" and its §5 open call (answered by his decision 3).
  - Bench-spec v2 §2.5's zoom ("only its distance moves").
  - Art's role-file framing lines are already flagged by Art.
- **Found, not mine:**
  - `Screw.cs:341`'s 2 mm radius floor must go with the decal, or every hole is ≥ 4 mm (Systems).
  - The camera's lens is 0.73× real (Art, v3.6).
  - `BenchSlots` inherit the Workbench's (2, 1, 1) scale (hygiene; harmless today).
  - The game has no anti-aliasing at all (§8).
- **Unverified:** the bench top's extent beyond the mat (§6.9); the AA cost; the model's framings until Bench 9 runs on the real meshes.

## 14. Against the other specs and the feel sheet

**The other specs:**
- **Art:**
  - Agree: the framing finding (reproduced: 37.8 / 39.9 / 19.0 %); real-size authoring; the hole decal; the layout as near-optimal for "everything in view" (the variants above lose).
  - Disagree: screws 2.4 → 3.0 mm (§4).
  - Add: the lens Ø, and the strap counted in the camera's bounds.
- **UI/UX:** agree with U1–U6 as the contract, kept strict. Their ring (≥ 14 px) is what a default-view screw relies on. Zoom left off the first-repair card is right: Day 1 needs none.
- **Systems:**
  - Agree: relative and pixel rules, `BenchLayout` (extended in §6.5), slots off the mat (placed in front, §6.9), the decal, `threadLength` 2 × head = 6 mm.
  - Their T13 "hidden" check runs at zoom 0 in these units.
  - Order: their A1, then this, then A2–A3.
- **Audio:** agree. Stroke speed in 1080p px is scale-proof; nothing here changes their hooks.
- **Cinematics:** agree the bench shots stay parked until this lands. The lens zoom inside the close-up is a camera-language change for their review; FOV 40 at rest and the 0.75 s blend are kept.
- **NPC:** no overlap.

**The feel sheet:**

| Target | This spec | Verdict |
|---|---|---|
| **T9** device ≥ 35 % at default | Phone 37.8 %, camera 39.9 % (ReStory ≈ 41 %) | Met |
| T9, the watch | 19 % | Not met; its 1.5× default fails T13 (open question 1) |
| **T8** head ≥ 12 px at default | 9.4–10.6 px at 1080p; 12.6–14.2 at 1440p; ≥ 15 at the first zoom stop | Met at 1440p and zoomed; at 1080p it needs 3.8 mm heads (open question 2). ReStory's own heads are about 3.4 mm on a 48 mm phone: it exaggerates too |
| **T13** nothing out of sight | Every slot in the default frame by Bench 9's construction | Met at zoom 0 |
| **T6** footprint ≥ 40 % of a spot | Systems' relative widths | Holds at any scale |
