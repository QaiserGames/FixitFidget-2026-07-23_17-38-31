# 3D Art & Blender, wave 2: Grace's camera v3.6, the devices at real size, real screws and holes, the mug tree, the device icons (14 Oct 2026)

*Blender lead (Opus 5.5, max). Blender 5.2.2 as a Python module in the cloud, in `/root/cafe/work/blender/` (`work/` below). The repo, the Mac and Unity were not touched. Every number below comes from a script run or a render I looked at.*

## What he will see

Grace's camera is a real camera's size now, and its "handle" is gone. A tan leather neck strap hangs from a chrome lug at each end through a split ring. It is folded back under a dark keeper, twists from edge-on at the ring to flat on the mat, and curves away into a loose two-turn loop behind the lens. Her husband's scratches are on the loop's top turn. The body is deeper, with soft rounded ends like a rangefinder's. On Day 2 the jammed shutter has dull tarnished gears, a bent blade and a spring sprung out of its seat. The bright new one waiting on the pale pad is the same mechanism, clean. The phone's and the watch's screws are small: dark pan heads on the phone, blued on the watch. The hole a screw leaves is a pinprick: a dark bore with a bright lip, in a faint dip, clearly smaller than the screw. On the checklist card, the steps for the new screen, the mainspring, the shutter and the two covers show their own pictures. From the overhead camera, the bar mat now has a small mug tree with four green mugs.

---

## 1. What I did

All six items in my brief are built, exported and audited, and every audit prints **0 problem(s)**:

| # | Item | Result |
|---|---|---|
| 1 | Grace's camera v3.6 | Strap (lugs, rings, protectors, keepers, fold-backs, a two-turn loop), body 32 mm deep with the roundings swapped, `Clean shutter mechanism`, the jammed one's tells. Lens size unchanged. |
| 2 | Real-size exports | `HD_Phone/Watch/Camera.fbx`, real size. `devices.json` v3 (`"units": "real"`, `authoredScale`, moves, screws). The `REAL` audit: 33 rows ok, **3 devices; 0 problem(s)**. |
| 3 | Screws and holes | 3.8 mm heads (0.35 × Ø tall, shank 0.55 × Ø): black oxide on the phone, blued on the watch. Driver tip cross 2.28 mm. `BT_ScrewHole.png` (256², RGBA). |
| 4 | Mug tree | 0.26 m, four green mugs, on `LI_BarMat`. Overhead check 25 × 13 px: **PASS** (was 25 × 9). |
| 5 | Device icons | `part_phone`, `part_watch`, `part_camera`, `cover_phone`, `cover_watch`, plus a 13-icon contact sheet. |
| 6 | Review renders | Each device at the TD's layout view and zoom limit, 16:9 and 16:10, U1–U6 drawn, with the numbers. A 4-view turntable of the camera. |

**I looked at every render and fixed one real problem.** At the TD's tighter framings (0.48 / 0.42 m), the tool rest's near end fell inside U4 (bottom left) for the phone and the watch. That was my wave-1 rest depth of −60 mm, set for the old 0.58 m view.
- A numeric search showed the rest clears U4 at 16:10 only when centred (y 0).
- Centred, it collides with the phone's and the watch's scrap trays on the mat. The TD's projection model never checked rest against tray.
- The fix: all rests centred, and two trays moved right (§2). I re-rendered both devices, and every view now reads ALL CLEAR.

**Checked and kept:**
- **Metal contrast:** the jammed and clean brass measure ΔL* ≈ 19 under my lamp (Art asked for about 25).
- **Screw seating:** a ray test puts every screw head on its surface: phone gap 0.000 mm, watch gap 0.001 mm.

## 2. The decisions and their reasoning

| Decision | Why | Rejected |
|---|---|---|
| **The camera stays authored at 2× and its new parts are designed in real mm**, then doubled. `build()` ends with `hl.scale_build(…, 1/AUTHORED_SCALE)`. | One frame for the whole file. The TD's §6.2 allows either. Re-authoring at 1× means rewriting 980 lines of checked geometry. | Re-authoring the camera at 1×. |
| **The body's back stays where it was. It deepens upward**, 23 → 32 mm real. The front face and everything on it or in the well rise 9 mm. The well keeps its 5 mm depth. | The camera lies on its back on the mat, so the back is the resting plane. A deeper well would hide the film-path grime from 58°: Day 1's clean. | Deepening downward: the rest height and the strap's plane change. A deeper well. |
| **Roundings swapped as Art A6 asks:** the ends' vertical edges r 6.5 real; the top and bottom faces' outlines r 2.5 (the long edges too). | The rangefinder's rounded ends from above. One rounding on the plate outline is simpler and reads as a soft plate edge. | Two different radii on the outline's chain of edges. |
| **The worn patch wraps the rounded end edge**, 0.3 mm proud. | The new r 6.5 left only 1.5 mm of flat front face beside the well. The grip end is also where leatherette really wears. | Dropping it. Moving it to the lens side. |
| **Two lugs replace v3's three eyelets.** They sit on the end plates at the band's height, centred in depth (Art A6), with the hole axis through the body's depth. | That is how a Canonet's lugs sit. The split ring hangs from the lug, and the strap through it is edge-on at the ring, then twists flat. A camera lying on its back really does this. | A ring lying flat (it cannot pass through a vertical hole). |
| **The loop is 88 × 58 mm, not Art's 110 × 55.** It lies at device x −96…−8, y +66…+124. | At 110 mm it would hit the tool rest (mat x −184) or the scrap tray (mat x −85). At 88 mm it clears both by 4–7 mm. | 110 mm: a collision on the mat. |
| **One strap, cut into three meshes:** `UpperStrap` (lens-end run), `WornLoop`, `LowerStrap` (opening-end run). | Art's names. It is built as one centreline, so the cuts meet exactly. | Two copies of one band mesh (v3). |
| **Every tool rest is centred (`toolRestCentreY` 0/0/0), the phone's scrap tray moves (−30, 133) → (6, 133) and the watch's (−20, 107) → (26, 107).** Mat mm. | The only arrangement that keeps the rest out of U4 at both aspects without the rest and tray overlapping. Rest and tray now sit 6 mm apart. Both trays stay in frame and clear of U1–U5 (searched, then rendered). | Rest y −30 with the tray kept: U4 at 16:10 by about 100 canvas px. Moving the tray back (y): it falls off the mat. |
| **The phone's screw shank is 4.5 mm; the watch's is 2 × the head (7.6 mm).** | At 7.6 mm a phone screw from the back glass pokes 1 mm out of the chassis's front face, which shows with the screen out. `devices.json` carries `shank_len` for Systems' `threadLength`. | One length for both. |
| **The cross recess is darker than the head** (`HD_ScrewRecess` #121110). | Art's slot colour #2A2D31 is within 3 L* of the black-oxide head #2B2724, so the cross would never show. | Art's slot colour. |
| **The mug tree's four mugs are Ace's green** (`LI_MugGreen` #2E7D5B, smoothness 0.6). There are 8 cream mugs on the mat (was 10). | From above, cream mugs on the tree would sit on the cream tile and vanish. The style guide says "the green lives on cups". The tree needed the mat's east end. | Cream (no read). A bigger rack (Art's 1,200-tri budget is already passed). |
| **The tree's mugs lean 25°, all to the same side of their arms.** | Four mugs a quarter turn apart then never meet. The audit shows 0 coplanar faces and nothing floating. | Hanging straight under the hook (66°): they collide. |
| **Real-size slivers are cleaned in `scale_build`**: edges under 4 µm are collapsed when a piece would fail the degenerate audit. | Three glyph slivers in the watch's engravings had area at 3× and none at ⅓. The finest real feature is 20 µm. | Relaxing the audit. |

## 3. Decisions his to overrule

1. The strap's form: two lugs, rings, teardrop protectors, keepers, 40 mm fold-backs, the twist at the rings, a two-turn loop, and the opening end's run passing behind the body.
2. The loop at 88 × 58 mm (Art: 110 × 55), to clear the rest and the tray.
3. The camera body 32 mm deep, rising 9 mm at the front. The worn patch now on the rounded grip end.
4. **The bench layout data:** every rest centred; the phone's tray at (6, 133), the watch's at (26, 107). These are Systems' `BenchLayout` fields, proposed here.
5. The mug tree's green mugs, and 8 mugs on the mat instead of 10.
6. The phone's screw shank 4.5 mm.
7. The dark cross recess.
8. The icons: `part_phone` drawn in the prefab's `M_FreshGlass` blue (#8BA4D5, what the pad shows), not the FBX's navy.

## 4. The deliverable

### 4.1 Files (all under `work/` = `/root/cafe/work/blender/`)

| File | New or changed | Notes |
|---|---|---|
| `Assets/Art/Models/Devices/HD_Phone.fbx`, `HD_Watch.fbx`, `HD_Camera.fbx`, `devices.json` | changed (real size) | v3 manifest |
| `BlenderSource/HeroPhone_v3.blend`, `HeroWatch_v3.blend`, `HeroCamera_v3.blend` | changed | |
| `Assets/Art/Models/BenchTools/BT_Screwdriver.fbx` (+ the other seven re-exported unchanged), `bench_tools.json` | changed | tip cross 2.28 mm; 8 pieces, 7,618 tris, 0 problem(s) |
| `Assets/Data/Resources/Bench/BT_ScrewHole.png` | new | 256², RGBA, sRGB, straight alpha |
| `Assets/Art/Models/CafeKit/LI_BarMat.fbx`, `cafe_dressing.json` (+ the other ten re-exported unchanged) | changed | 11 pieces, 13,536 tris, 0 problem(s) |
| `Assets/Art/Models/BenchStage/bench_stage.json` (+ `BM_RepairMat.fbx` re-exported unchanged) | changed | per-device table: rest y and trays |
| `Assets/Data/Resources/UI/BenchIcons/part_phone.png`, `part_watch.png`, `part_camera.png`, `cover_phone.png`, `cover_watch.png` | new | |

**Scripts** in `work/Tools/Blender/`:
- **Changed:** `hero_lib.py` (`scale_build`, `PART_FACES`), `hero_camera.py` (v3.6), `hero_phone.py` (v3.7), `hero_watch.py` (v3.6), `export_hero_devices.py` (v3), `bench_tools.py` (`TIP_CROSS`), `bench_stage.py` (`PER_DEVICE`), `cafe_cafeside.py` (mug tree), `render_bench_icons.py` (+5), `review_cafe_wave1.py` (`CAFE_REVIEW=` parts), `review_bench_stage.py` (no longer rescales the now-real devices; superseded by the new review), `README.md`.
- **New:** `bench_screw_hole.py`, `review_devices_wave2.py`.

### 4.2 Triangles and budgets (as exported)

| Piece | Tris | Budget (Art B4 / TD) |
|---|---|---|
| **Camera, all** | **16,892** (11 meshes) | +about 5k on v3 (TD §8) |
| Camera body (with lugs) | 2,268 | — |
| Strap: `UpperStrap` 760, `LowerStrap` 1,008, `WornLoop` 988 | **2,756** | ~3,000 |
| `Mechanism` (jammed) / `CleanMechanism` | 2,356 / **2,488** | ~2,000 (+24 %: the blades and spring rails) |
| Lens housing / glass / film path / blades | 4,532 / 1,004 / 1,224 / 132 + 132 | unchanged |
| **Phone, all** | **4,936** | — |
| Phone screw | 284 | ~150 (the head 12 sides, an 8-sided threaded shank) |
| **Watch, all** | **18,178** | — |
| Watch screw | 284 | ~150 |
| `LI_BarMat` | 3,382 (was 2,356) | 1,200 (flagged in wave 1: real mugs) |

**Sizes at real scale** (the `REAL` table, ±7 %):
- **Phone:** 71.2 × 7.8 × 140.3 mm.
- **Watch:** Ø 50.0 mm, 66.7 mm long with the bow.
- **Camera:** 134 mm across the lugs, depth 32.0 mm, 81 mm across the plates.
- **Screws:** heads Ø 3.8, 1.33 tall.
- **Camera lens:** Ø 38.7, recorded as built (the TD's 0.73× flag).

### 4.3 Every name or anchor that moved (Unity, the prefab root's frame, metres at real size)

"Baked" is today's value × 1/authoredScale: what the TD's bake produces. "New" is what v3.6 / v3.7 needs, set after the bake. The same table is in `devices.json` → each piece's `moves`.

**GraceReunionCamera** (k = ½)

| Object | Today (2×) | Baked | **New** | Why |
|---|---|---|---|---|
| `Camera body` | (0, .023, 0) | (0, .0115, 0) | **(0, .016, 0)** | the body 32 deep, back unmoved |
| `Lens housing` | (−.065, .055, −.024) | (−.0325, .0275, −.012) | **(−.0325, .0365, −.012)** | +9 mm |
| `Lens glass` (**lens centre**) | (−.065, .093, −.024) | (−.0325, .0465, −.012) | **(−.0325, .0555, −.012)** | +9 mm |
| `Lens grime` | (−.065, .100, −.024) | (−.0325, .050, −.012) | **(−.0325, .059, −.012)** | +9 mm; `domeRadius` .0431 (bake), unchanged |
| `Open film path` (**film path**) | (.056, .049, −.043) | (.028, .0245, −.0215) | **(.028, .0335, −.0215)** | +9 mm |
| `Film-path grime` | (.045, .058, −.043) | (.0225, .029, −.0215) | **(.0225, .038, −.0215)** | +9 mm |
| `Film-guide grime` (**film guide**) | (.079, .058, −.043) | (.0395, .029, −.0215) | **(.0395, .038, −.0215)** | +9 mm |
| `Jammed shutter mechanism` (**shutter's seat**) | (.053, .060, .041) | (.0265, .030, .0205) | **(.0265, .039, .0205)** | +9 mm |
| `Bent` / `Working shutter blade` (its children) | (0, .01232, 0) | (0, .00616, 0) | unchanged | move with it |
| `Upper strap` | (−.162, .026, −.063) | (−.081, .013, −.0315) | **(−.06375, .016, −.0325)** | the lens-end lug's hole |
| `Lower strap` | (−.162, .026, .063) | (−.081, .013, .0315) | **(.06375, .016, −.0325)** | the opening-end lug's hole (it was at the lens end) |
| `Worn loop` | (−.200, .026, 0) | (−.100, .013, 0) | **(−.050, −.0005, .095)** | the loop's middle, on the mat |
| `Old scratch 0` | (−.200, .032, −.027) | (−.100, .016, −.0135) | **(−.056, .0046, .11439)** | on the loop's top turn, which runs flat along x there |
| `Old scratch 1` | (−.200, .032, −.009) | (−.100, .016, −.0045) | **(−.048, .0046, .1185)** | the same |
| `Old scratch 2` | (−.200, .032, .009) | (−.100, .016, .0045) | **(−.040, .0046, .11598)** | the same |
| **`Clean shutter mechanism`** (new) | — | — | **(.0265, .039, .0205)**, inactive | under the root, in the jammed one's seat |

The scratches keep rotation 0 and their baked scale (7.5 × 0.5 × 1 mm). They lie along the strand.

- **Strap lugs** (part of the body mesh, not objects), hole centres:
  - old eyelets, baked: (−.0605, .013, ∓.0315) and (.0605, .013, −.0315);
  - **new:** (−.06375, .016, −.0325) and (.06375, .016, −.0325).
- **The mechanism's three chassis screws** are decoration inside its mesh. They move with the mechanism.
- **The camera's lowest point** is y −0.62 mm (the strap). Its `restHeight` for "0–1.2 mm over the felt" is 0.0006–0.0018, not the TD's .002, which floats it 1.4 mm.

**PhoneRepair** (k = 1)

| Object | Today | **New** | Why |
|---|---|---|---|
| `Screw0` | (.022, −.0047, −.058) | **(.022, −.004565, −.058)** | the 3.8 × 1.33 head's underside on the back glass (−3.9 mm) |
| `Screw1` | (−.022, −.0047, −.058) | **(−.022, −.004565, −.058)** | the same |

**PocketWatch** (k = ⅓)

| Object | Today (3×) | Baked | **New** |
|---|---|---|---|
| `Screw0` | (.032, −.020, .032) | (.010667, −.006667, .010667) | **(.010667, −.006878, .010667)** |
| `Screw1` | (−.032, −.020, .032) | (−.010667, −.006667, .010667) | **(−.010667, −.006878, .010667)** |
| `Screw2` | (.032, −.020, −.032) | (.010667, −.006667, −.010667) | **(.010667, −.006878, −.010667)** |
| `Screw3` | (−.032, −.020, −.032) | (−.010667, −.006667, −.010667) | **(−.010667, −.006878, −.010667)** |

Each watch head's middle sits on the back plate's dome; the ray gap is 0.001 mm. The watch's lowest point is now the screw heads at −7.55 mm, so the TD's `restHeight` .0083 leaves them 0.75 mm over the felt. That is fine.

**Names in the FBX (children):**
- **Camera:**
  - `Strap` is gone, replaced by `UpperStrap` and `LowerStrap` (two meshes now);
  - new: `CleanMechanism`;
  - kept: `LensHousing`, `LensGlass`, `FilmPath`, `Mechanism`, `BentBlade`, `WorkingBlade`, `WornLoop`.
- **Phone and watch:** names unchanged.
- **Every prefab object name is kept**, including `KEEP - scratched sentimental strap`, which `BenchLab.cs:922` finds.

## 5. What the Unity import must do, file by file

1. **Devices 1** (`BlenderKitSteps.ImportDevices`, imports builder). Copy `work/Assets/Art/Models/Devices/*` over `Assets/Art/Models/Devices/`.
   - The importer settings stay (`globalScale 1`, `useFileScale`): the FBX is real.
   - `GatherMaterials` makes the new HD_ materials from the manifest: `HD_CamStrapEdge`, `HD_CamBrassNew/Old`, `HD_CamSteelBright/Old`, `HD_CamChromeBright`, `HD_CamBlackSatin`, `HD_ScrewBlack`, `HD_ScrewBlued`, `HD_ScrewRecess`.
   - **Expect:** phone 6 meshes, 4,936 tris; watch 6, 18,178; **camera 11**, 16,892; every child at its `at` ±1 mm; all PASS.
   - The manifest's new fields (`units`, `authoredScale`, `moves`, `screws`, `real`, `newObjects`, `scratches`) are ignored by `JsonUtility`. That is harmless.
2. **Devices 2 at real size** (`HeroDeviceSteps.cs`, the **scale** builder):
   - **The camera's pieces must change, or the step FAILs** "no mesh 'Strap'":
     - `UpperStrap` → `Upper strap`;
     - `LowerStrap` → `Lower strap`;
     - `WornLoop` → `Worn loop`;
     - **`CleanMechanism` → `Clean shutter mechanism`**, with `createUnder = "GraceReunionCamera"`, then a move to (.0265, .039, .0205), and set **inactive**.
   - **Head boxes:** phone and watch `head = (.0038, .00133, .0038)`. The TD's §6.4 already lists the watch's case boxes ÷3.
   - **Order:** the bake first (TD §6.4), then the moves in §4.3, all real-size values (`devices.json` → `moves`).
   - **Colliders:**
     - Art A6 says the strap has no collider and is never a hover target. Remove the BoxColliders on `Upper strap`, `Lower strap`, `Worn loop` and `Old scratch 0–2` (listed in `strapNoCollider`). Fitted to the new bounds, `Lower strap`'s box would wrap the camera's opening end.
     - `Clean shutter mechanism` needs a BoxCollider fitted to its mesh before Systems presents it on `Pad_Small` (the fresh-part press).
   - **Wiring** (Systems'/the TD's, Art A7): `ReplaceablePart.brokenVisual` → the `Jammed shutter mechanism` assembly; `freshVisual` → `Clean shutter mechanism`. `Working shutter blade` stays in the prefab, unused.
3. **Bench tools 1** (`BlenderKitSteps.ImportTools`). Copy `work/Assets/Art/Models/BenchTools/*`.
   - Only `BT_Screwdriver`'s tip changed; 990 tris, as before.
   - Blend shapes still need `importBlendShapes = true` for `BT_ClothHeld` and `BT_Brush` (my wave-1 flag).
4. **The hole decal** (imports). Copy `BT_ScrewHole.png` to `Assets/Data/Resources/Bench/`.
   - TextureImporter: Default, sRGB, alpha is transparency, mipmaps on, bilinear, **Clamp**, max 256, no compression.
   - `Resources.Load<Texture2D>("Bench/BT_ScrewHole")` (contract §8).
   - Systems lays it on a quad 1.15 × the head, 0.05 mm proud, facing out. The bore is 0.48 × the head (1.82 mm).
5. **Icons** (imports). Copy the five PNGs to `Assets/Data/Resources/UI/BenchIcons/`: Sprite (2D and UI), mipmaps, bilinear, max 256, no compression.
   - Keys: `part_phone` (the new screen), `part_watch`, `part_camera` (the clean mechanism), `cover_phone`, `cover_watch`.
6. **Café dressing 1** (`CafeDressingSteps`, imports). Copy `LI_BarMat.fbx` and `cafe_dressing.json`.
   - Make the new `LI_MugGreen` from the manifest.
   - The place is unchanged: (−0.50, 1.00, 17.75), rotation (0, 180, 0). It is 0.27 m tall, well under the menu board.
   - Art's overhead 10-pixel check: `LI_BarMat` should now report about 25 × 13 px.
7. **Bench layout data** (the **bench** builder's `DeviceDefinition.benchLayout`, or Bench 9):
   - `toolRestCentreY` = 0 for the phone, camera and watch;
   - `scrapTray` = (6, 133) for the phone and (26, 107) for the watch;
   - the camera's tray is unchanged at (−5, 115).
   - `bench_stage.json` → `per_device` carries the same numbers.

## 6. The renders (all looked at; `work/renders/`)

**Device views** (`devices/`):
- `phone-layout-16x9[-zones].png`, `-16x10-zones.png`: the phone back up on the mat at (+10, −14), 0.48 m. One screw in, its twin out in cup 4, the hole decal where it was. **ALL CLEAR**, fill 35.3 %, head 13.1 px, bore 6.1 px.
- `phone-zoom-16x9/16x10-zones.png`: FOV 19°. The screw shows its cross at 28 px; the hole is a 13 px bore with a lit lip, smaller than the head. Fill 75.4 %, ALL CLEAR.
- `camera-layout-16x9[-zones].png`, `-16x10-zones.png`: (−58, −8), 0.49 m. The strap from both lugs, the loop behind the lens, `Clean shutter mechanism` on `Pad_Small` beside the jammed one in the camera. **ALL CLEAR**, fill 35.9 %.
- `camera-zoom-16x9/16x10-zones.png`: FOV 27°. Rings, keepers and stitching readable; the bent blade and tarnished gears. Fill 55.9 %, ALL CLEAR.
- `watch-layout-16x9[-zones].png`, `-16x10-zones.png`: (+10, −20), 0.42 m. Back up, two blued screws in, two in cups 1–2 with their holes, the mainspring on `Pad_Small`. **ALL CLEAR**, fill 17.8 %, heads 13.4 px, bore 6.3 px.
- `watch-zoom-16x9/16x10-zones.png`: FOV 12.5°. Heads 45 px, bore 22 px, fill 61.7 %.
- `camera-turntable-1..4-*.png`, `-sheet.png`: the camera alone, four sides. Each run from its lug, the twist at the ring, the opening end's run behind the body, the stacked loop, the rounded ends.
- `review-numbers.txt`: every view's fill, screw and bore px, and zone and frame checks.

**Café** (`cafe/`):
- `overhead-2-after.png`, `overhead-3-bar-x2-after.png`, `overhead-4-barmat-x4-after.png`: the real overhead camera (43°, 34 m, FOV 44°). The mug tree reads as a green accent at the bar mat's east end.
- `overhead-10px-check.txt`: `LI_BarMat` 25 × 13, **PASS**.

**Icons** (`icons/`):
- `bench-icons-contact-sheet[-x2].png`: all 13 at 36 and 56 px on #F7F3EE, two bands.

**Light:** the bench lamp and a flat ambient. It is URP-like and good for layout and read, not a lab photo.

## 7. Against the feel targets (my renders, the TD's model framings; Bench 9 measures on the real meshes)

| Target | Phone | Camera | Watch |
|---|---|---|---|
| **T8** head ≥ 12 px at 1080p, default | **13.1** ✓ | — (no screws) | **13.4** ✓ |
| T9 default fill (36 / 38 / 18 %) | 35.3 (−0.7) | **35.9 (−2.1)** | 17.8 (−0.2) |
| T9 zoom 1 (75 / 58 / 60 %) | 75.4 ✓ | **55.9 (−2.1)** | 61.7 ✓ |
| Items in frame, clear of U1–U5, both aspects | ✓ | ✓ | ✓ |

## 8. Flags

- **The camera misses T9 by about 2 points at the TD's model framings** (35.9 / 55.9 against 38 / 58). Every zone is clear, and the frame has spare mat below the camera (its near edge is at y 622 of 1080 at 16:9). Bench 9's search on the real mesh should find a closer aim. The phone and the watch miss by under a point. I measured them back up (screws showing), not face up.
- **Layout change outside my file:**
  - the scrap-tray moves (phone, watch) are Systems' data, proposed here;
  - the rest depth is my field (`toolRestCentreY`, now 0/0/0);
  - Bench 9 re-checks both.
  - The cause is a gap in the TD's model: it never placed the rest and the tray on the mat together.
- **The film is half-size at real scale.** The film strip in `Open film path` is 16 mm wide, against 35 mm film; the gate is 18 × 12 mm. It was authored to fill the 2× well. A real 35 mm path needs a 45 mm-wide well, which moves the grime spots. Not changed; Art's call for a later pass.
- **The lens is Ø 38 (0.73× real).** The flag stays as the brief says, and the `REAL` table records it "as built".
- **The camera's `restHeight`:** about .001, not the TD's .002 (§4.3).
- **Strap colliders:** remove them, or the new `Lower strap` box wraps the body's opening end (§5.2).
- **`Clean shutter mechanism` has no collider** until Devices 2 or Systems adds one.
- **The jammed and clean metals measure ΔL* ≈ 19 under my lamp** (Art about 25: the designed hexes are 26 apart). If the Unity photos read them too alike, darken `HD_CamBrassOld` toward #7A5C2A.
- **Budgets over Art's estimate:**
  - `CleanMechanism` 2,488 (~2,000);
  - screws 284 each (~150);
  - `LI_BarMat` 3,382 (1,200).
  - All trivial next to the café's 64k.
- **Blender import:** `HD_Camera.fbx` carries 11 meshes. Devices 1's mesh count comes from the manifest, so it holds.
- **Not mine, noticed:** the TD's §4 still says 3.0 mm everywhere. The feel targets' call 3 (3.8 mm) is what I built, with the head box at (.0038, .00133, .0038). Devices 2's code must use 3.8, not the spec's 3.0.
- **The scratchpad is shared between agents** (another agent overwrote one of my helper scripts mid-session). I moved my scratch files to `scratchpad/blender-w2/`. Others should namespace theirs too.

## 9. Next (if asked)

1. Bench 9's real framings: re-render with its numbers (`review_devices_wave2.py`, `VIEWS`).
2. A 35 mm film path, if Art wants it (moves the film grime).
3. Unity photos of the strap and the two shutter states under URP, to settle the metal contrast.
