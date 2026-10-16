# 3D Art & Blender, wave 1: the repair mat, the rest and tray, the cloth and brush, the icons, the café kits (14 Oct 2026)

*Blender lead (Opus 5.5, max). Blender 5.2.2 as a Python module in the cloud. Worked in `/root/cafe/work/blender/` (a copy of `Tools/Blender`); the repo, the Mac and Unity untouched. Every number below comes from a script run or a render I looked at. Paths under `work/` mean `/root/cafe/work/blender/`.*

## What he will see

At the bench the device lies on a slate-blue silicone repair mat: a printed 1 cm grid, a centimetre ruler along the near edge and up the left side, six round screw cups numbered 1–6 with a small steel disc in each, a pale "new part" pad with four corner marks, and a dark well for covers. The tools lie side by side on a low steel rest at the left, handles toward him and tips away. The yellow lens cloth sits folded in a cork tray at the rest's far end, one corner lifted to grab. A small steel tray waits behind the device for old parts. Grab the cloth and it becomes a soft wad with a gathered tail. Press and it spreads; rub and it bunches toward the stroke. The brush's bristles bend as it sweeps. The checklist card shows small outlined pictures of the tools. From the usual overhead view the café's back bar has more on it: a row of tagged, bagged repairs under the drawer wall, a "THIS WEEK" whiteboard over it, a colourful syrup rack, black cup tubes, upside-down mugs on a bar mat, Ace's corner of the bench on a grey pad, and a half-open parts box on the counter's west end. Up close: a mortgage statement under the till with the past-due line circled, and a FINAL NOTICE taped inside the back door.

---

## 1. What I did

- **Built, exported and audited all six items of my brief.** Every audit printed **0 problem(s)**:
  1. the mat;
  2. the tool rest and the scrap tray;
  3. the cloth's states and the brush's bend;
  4. the eight icons and their contact sheet;
  5. the close-up review renders;
  6. Art's café kits R1–R5, C1–C3 and S1–S2, with overhead before/after renders, a 10-pixel check and a turntable.
- **Looked at every render and fixed what was wrong once:**
  - the camera layout's tools fell into UI/UX's zone U4;
  - the held cloth read as a six-petal flower;
  - the cups detail rendered without its screws;
  - the tweezers icon was one line at 36 px;
  - nine of the café pieces failed the audit at first (overlapping faces, a floating bubble wrap, zero-area handle faces, an inside-out whiteboard face).
- **Not built (my brief's "not now"):** the camera's strap, body and clean mechanism, the screws and the hole decal, and the device-specific icons. The TD's one-scale spec landed while I worked, so the numbers that session needs are now known (§9).
- **Fonts.** The repo's fonts are Git LFS pointers, and the proxy refused the LFS fetch ("not in this session's authorized repository set"). I used the same OFL/Apache fonts from npm (`@expo-google-fonts`: League Gothic Regular, Yellowtail, Caveat) and Inter from Debian (OFL). Each sits in `work/Tools/Blender/fonts/` with its licence. `LeagueGothic-wdth.ttf` is still a pointer there.

## 2. The decisions and their reasoning

| Decision | Why | Rejected |
|---|---|---|
| **The mat, rest and tray are built in the bench rig's own frame**, not the house's "front faces −Y" | Art's coordinates are the rig's local frame (+x the player's right, +z away). Built this way, they import with no turn, and the empties' local positions *are* Art's table (`Cup_1` = (0.036, 0.0018, −0.024) m). Systems' `matFrame` reads them as-is. | House frame plus a 180° turn in Unity: every position needs negating, and the turn is easy to forget. |
| **The ruler strips stop on the outline's own vertices.** The near ruler's 0 sits at x −285 (Art: −290); the grid's major lines count from (−285, −166). | At −290 the strips crossed the 12 mm corner arcs. That puts T-junctions in the top, which is a crack in a solid the audit checks as closed (0 open edges). It moves the ruler 5 mm and changes nothing anyone uses. | New vertices on the chamfer loop (three loops to keep in step). A ruler in the grid texture (the grid tiles, so it can't hold numbers). |
| **The grid texture is `BM_Grid_color.png`** (Art: `BM_Grid.png`) | Kit 1's `ConfigureTexture` gives Repeat and mipmaps only to `*_color` and `*_normal` files (`CafeLivedInSteps.cs:642-661`). The name makes it tile through the existing code. | Art's name plus a special case in a new step. |
| **A steel disc in each cup's floor** (Ø 10 mm, 0.3 mm proud, `BM_Steel` #B9B5AD) | Audio's tink needs metal under the screw (`audio-bench-sounds.md:76`). Steel at L* ~74 keeps the cup pale, so dark screws still read. | A pale floor only: Audio's "tup". |
| **The tool rest's depth is per device: phone and watch y −60, camera y 0** (Art gave x only) | Centred, the 230 mm rest runs into the phone's and the watch's scrap trays. Pulled to −60 for the camera, its handles fell into U4 in the review render. The camera's tray is clear of its rest, so that rest stays centred. All three layouts were rendered with the zones drawn: the zones are clear. | One y for all: either a tray collision or a U4 overlap. |
| **Each U notch is fitted to its tool by ray casts** (the tool rests on both bars, 0.0 mm gaps, nothing else touched). Notch bottoms: 8.3 / 10.5 / 13.9 / 17.6 mm. | The past error "modelled right, placed wrong" (the cloth in the caddy). The seats' poses come out of the fit, not my eye. | Equal notches: the brush hovered, and the screwdriver sank. |
| **The rest carries the poses as empties `Seat_Screwdriver/Pry/Tweezers/Brush/Cloth`; the tray carries `Slot_1..3`** | Systems' stage fields (`systems-forgiving-bench.md` §4.0.1: `toolSlots` from `BT_ToolRest`, three `scrapSlots` as children of the tray). | Poses as numbers in a C# file: they drift from the model. |
| **The held cloth is one mesh with keys `Press` and `Bunch`; the stroke runs along the model's Unity +X** | Art's A4.3 and Systems' "eased in closed form, ω 14 /s" both drive keys. One topology means no pop. | Three swapped meshes (Art's fallback): cheaper, poppier. |
| **The held wad's folds are irregular (two harmonics); the tail is 42 mm, leaning 24° toward the player** | The first build, six regular folds and a 32 mm tail, read as a yellow flower from 58° (`bench-5`, first pass). | Art's 30 mm tail: from 58° it is a knob. |
| **`Bend` keeps the bristle tips on z 0** (they bend as a loaded cantilever, ends 25° and 7.4 mm aside) | The origin is the working tip, and it stays on the glass when bent. No lift for Systems to add back. | A rigid 25° turn: the tips rise 2.4 mm off the glass. |
| **The generic screw icon is satin steel, not black-oxide** | A black-oxide head (#2B2724) disappears against the #1F1B17 outline at 36 px; steel shows the cross. The in-world screws stay black-oxide and blued (Art's A8). | Art's A9 note (the phone's black-oxide screw). |
| **The story pieces are their own sheets** (`LI_MortgageLetter`, `LI_FinalNotice`), not the house kit's letter mesh | `HK_Letter` is an envelope with geometry address lines and UVs in metres (`house_kit.py:510-531`). A printed page needs 0..1 UVs on its face. Each new sheet is 28 or 92 triangles. | Retexturing `HK_Letter`: an offset and a ×4.5 tiling per material, and still an envelope. |
| **Ace's desk corner sits on a grey ESD pad** (1.20 × 0.54 m, #4A4F55) | Real on an electronics bench. It is also Art's own 10-pixel rule: small props count only as a cluster on a base ≥ 35 × 25 cm and ≥ 20 L* from what's under it. And it gives the loose items something to rest on, so the audit's "nothing floating" holds. | Loose items on the chestnut: a speckle from above, and five "floating" islands. |
| **R2's two post-its on the glass are a separate piece (`LI_GlassNotes`)**, one overlapping the other | They stick to `CK_RepairBench`'s glass, which isn't in the desk-corner piece. Inside it they would be floating islands. | Dropping them (they are on his list). |

## 3. Decisions his to overrule

1. The mat's frame: the rig's, with no turn (above).
2. The near ruler reads 0–57 cm from x −285. The grid counts from there.
3. Steel discs in the cups.
4. The tool rest's depth: phone and watch −60 mm, camera 0. This also adds a field Systems' `BenchLayout` doesn't have yet (§5, step 3).
5. The tools' rest poses: 5° tilt, the fitted notch depths, the tip positions (145 / 140 / 133 / 147 mm from the rest's near edge).
6. The held cloth's look: the irregular folds, the 42 mm tail leaning 24° toward the player.
7. `Bend` keeps the tips on the glass.
8. The steel generic screw icon. The tweezers icon opened to a 6.4° V (icon only).
9. The grey ESD pad under Ace's corner.
10. All the café's words are placeholders for Content, including the lender: "Pennywhistle Mortgage Co." (fictional).
11. The story pieces as new sheets, not the house kit's letter.
12. Leaving the bar mat (C3) at 9 px from above (§7).

## 4. The deliverable

### 4.1 Scripts (all in `work/Tools/Blender/`, each runnable with the bpy module or `blender -b --python`)

| Script | New or changed | What |
|---|---|---|
| `bench_stage.py` | new | The mat (`BM_RepairMat`): one closed shell plus 17 closed details (discs, pad, marks, numbers), 10 empties, Art's per-device table as data |
| `bench_stage_textures.py` | new | `BM_Grid_color.png` (512², one 50 mm tile) and `BM_Rulers.png` (4096 × 256, 7 px/mm); Pillow, no Blender |
| `export_bench_stage.py` | new | Audit (floating, degenerate, open edges, coplanar, empties), FBX with empties, `.blend`, `bench_stage.json` |
| `bench_tools.py` | changed | Thinner `BT_Cloth`; new `BT_ClothHeld` (+`Press`, `Bunch`), `BT_ToolRest` (+ the seat fit), `BT_ScrapTray` (+ slots); the `Bend` key on `BT_Brush`; `BT_Cork`; `DC_Steel` by name |
| `export_bench_tools.py` | changed | v4: empties and keys in the FBX; manifest v3 (seats in Unity's frame, quaternion and Euler, keys, notes) |
| `review_bench_stage.py` | new | The close-up review (`REVIEW_ONLY=phone,camera,watch,mat,cloth,cups`) |
| `render_bench_icons.py` | new | The eight icons and the contact sheet (`ICONS=` for a subset) |
| `cafe_repairside.py` | new | R1, R2, R2's glass notes, R3, R4, R5, S1, S2 |
| `cafe_cafeside.py` | new | C1, C2, C3 |
| `cafe_wave1_textures.py` | new | `LI_Tags.png`, `LI_Whiteboard.png`, `LI_Mortgage.png`, `LI_FinalNotice.png` (Caveat and Inter) |
| `export_cafe_wave1.py` | new | Audit (floating, degenerate, coplanar, below floor, texture mapped), FBX, `.blend`, `cafe_dressing.json` |
| `review_cafe_wave1.py` | new | Overhead before/after from the real `CmShopCam`, the 10-pixel check, a turntable |
| `fixit_blender.py` | changed | `export_fbx_tree(..., object_types=)`: the empties can go out with their parent (default unchanged) |
| `README.md` | changed | The new scripts listed; the rig-frame exception noted |

### 4.2 Exports (audit: 0 problem(s) each)

`work/Assets/Art/Models/BenchStage/` (new kit):

| Piece | Tris | Size (Unity: across, up, deep) | Empties | Materials |
|---|---|---|---|---|
| `BM_RepairMat.fbx` | **2,142** (Art 2,400) | 0.600 × 0.006 × 0.360 (0.0058 with the pad's marks) | `Cup_1..6`, `Pad_Small`, `Pad_Large`, `Well_Small`, `Well_Large` | `BM_Silicone` (textured, tiling 20), `BM_Recess`, `BM_Pad`, `BM_Print` (textured), `BM_Ink`, `BM_Steel` |

Manifest: `bench_stage.json`. Pictures: `work/Assets/Art/Textures/Bench/BM_Grid_color.png`, `BM_Rulers.png`.

`work/Assets/Art/Models/BenchTools/` (replaces the kit; `bench_tools.json` v3):

| Piece | Tris | Notes |
|---|---|---|
| `BT_Screwdriver`, `BT_Tweezers`, `BT_PryTool` | 990 / 516 / 284 | unchanged |
| `BT_Brush` | 1,540 | + key `Bend` (252 bristle vertices move) |
| `BT_Cloth` | **2,436** (was 4,358) | 0.8 mm layers, 9 mm with the corner lifted 6 mm |
| `BT_ClothHeld` | **1,152** | keys `Press`, `Bunch`; 44 × 49 × 42 mm |
| `BT_ToolRest` | **448** | `DC_Steel` + `BT_Cork`; `Seat_*` × 5 |
| `BT_ScrapTray` | **252** | `BT_Steel`; `Slot_1..3` |

Total 7,618 triangles. Art's estimates: rest 600, tray 300, folded cloth 2,500, held cloth 1,200.

`work/Assets/Art/Models/CafeKit/` (`cafe_dressing.json`):

| Piece | Tris | Size (m) |
|---|---|---|
| `LI_PickupRow` (R1) | 1,840 | 2.83 × 0.25 × 0.33 |
| `LI_DeskCorner` (R2) | 2,988 | 1.22 × 0.22 × 0.54 |
| `LI_GlassNotes` (R2) | 24 | 0.14 × 0.09 |
| `LI_DonorParts` (R3) | 866 | 0.61 × 0.26 × 0.30 |
| `LI_Whiteboard` (R4) | 524 | 0.90 × 0.60 |
| `LI_PartsDelivery` (R5) | 460 | 0.64 × 0.35 × 0.63 (flaps open) |
| `LI_MortgageLetter` (S1) | 28 | 0.216 × 0.279 |
| `LI_FinalNotice` (S2) | 92 | 0.39 × 0.34 |
| `LI_SyrupRack` (C1) | 1,872 | 0.50 × 0.41 × 0.10 |
| `LI_CupStation` (C2) | 1,460 | 0.69 × 0.52 × 0.25 |
| `LI_BarMat` (C3) | 2,356 | 0.75 × 0.10 × 0.30 |

Total 12,510 triangles (Art about 13,000). Pictures: `work/Assets/Art/Textures/Cafe/LI_Tags.png`, `LI_Whiteboard.png`, `LI_Mortgage.png`, `LI_FinalNotice.png`. Three pieces run past the house's "standard prop < 1,000" because each is a cluster of 6–24 objects (the lived-in condiment station did the same). `LI_DeskCorner` matches Art's 2,800. The bar mat (Art 1,200) carries ten real mugs instead of reusing one mesh.

- **Sources:** `work/BlenderSource/BenchStage_v1.blend`, `BenchTools_v4.blend`, `CafeDressing_v1.blend`.
- **Icons:** `work/Assets/Data/Resources/UI/BenchIcons/` holds `screwdriver`, `pry`, `tweezers`, `brush`, `cloth`, `screw`, `newpart` and `cover` (`.png`, 256², RGBA).
- **Contact sheet:** `work/renders/icons/bench-icons-contact-sheet.png` (and `-x2.png`).

### 4.3 The renders (all looked at; `work/renders/`)

- `bench/bench-1-phone.png`: the phone, real size, on the mat at (−30, −14), its fresh screen on `Pad_Large`; the rest at x −135, the tray behind. It reads as a repair mat: grid, rulers, numbered cups, pad, well.
- `bench/bench-2-camera.png`: today's camera mesh at ×½ at (−82, 0), the strap hidden (being replaced, §9); the rest at the frame's left edge, the cloth tray top-left.
- `bench/bench-3-watch.png`: the watch at ×⅓; the fresh mainspring on `Pad_Small`. The watch is small (Art's and the TD's 19–24 %).
- `bench/bench-1..3-*-zones.png`: the same with U1–U6 drawn. **All clear** of every zone except the mat's own far-right corner under U3. UI/UX's contract lists cups, wells, pads, bins and tools, not the mat. U5 has 10 px to spare over the cover well.
- `bench/bench-4-mat-alone.png`: the bare mat under the lamp.
- `bench/bench-5-cloth-and-brush.png`: the folded cloth, then the wad held, pressed (wider and flatter) and bunched (its trailing side drawn out), then the brush straight and bent.
- `bench/bench-6-cups-detail.png`: a 2× zoom: the cups with stand-in 2.4 mm black-oxide screws on the steel discs, and the pad and well. **Even at 2× the 2.4 mm heads are dark dots with no cross (about 10 px)**, which supports the TD's 3.0 mm.
- `bench/bench-colour.txt`: the mat's rendered colour beside each device.
- `cafe/overhead-1-before.png`, `overhead-2-after.png`: the real overhead (43°, 34 m, FOV 44°) on a stand-in room (floor, three walls, the bar pieces from `cafe_bar.py`, the menu board, the bean bags, the grinder). The room lands about where Art measured it (x 600–1320, y 263–856).
- `cafe/overhead-3-bar-x2-before.png`, `-after.png`: 2× crops of the back bar and bench. After: the row under the drawers, the whiteboard, the cup tubes, the bottles, the mugs, Ace's grey corner, the parcel.
- `cafe/overhead-10px-check.txt`: the projected size of every piece (§7).
- `cafe/turntable-1..4-*.png`: the eleven pieces in a line, turned 0/90/180/270°. The wall pieces are lifted 0.3 m off the floor for viewing.
- `icons/bench-icons-contact-sheet-x2.png`: all eight at 36 and 56 px on #F7F3EE.

**Light:** the bench lamp's spot (#FFE4C2, 50°, 0.39 m above the mat) and the scene's flat ambient. It approximates URP, for judging colour and layout. It is not a lab photo.

## 5. What the Unity import steps must do

**Step 1. Bench tools 1** (`BlenderKitSteps.ImportTools`, existing). Copy `work/Assets/Art/Models/BenchTools/*` over `Assets/Art/Models/BenchTools/`.
- **It must import blend shapes for `BT_ClothHeld` and `BT_Brush`.** `Configure` sets `importBlendShapes = false` for every FBX (`BlenderKitSteps.cs:285`), and the TD flags the same (`td-one-scale.md:272`). Set it true for those two, with `importBlendShapeNormals = Import`, or the keys are dropped.
- The manifest now lists 8 pieces. The rest and the tray are 1 mesh each: their seats and slots are empties, children without a `MeshFilter`, so the mesh count holds.
- The `children` check finds the empties by name at `at` (±1 mm). They are flagged `turned`, so it skips rotation. Better: compare against the manifest's new `quaternion_xyzw` (seats (−0.6756, 0, 0, 0.7373), i.e. Euler (275, 0, 0); `Seat_Cloth` (0, −0.7071, 0, 0.7071), i.e. (0, 270, 0)).
- `BT_Cork` is made from the manifest (BT_ prefix). `DC_Steel` is mapped by name from `Assets/Art/Materials` (already found there).
- `PhotographTools` places the five tools by name, so the new pieces don't disturb it.
- **Expect:** "8 pieces, 7618 triangles", every check PASS.

**Step 2. Bench stage 1 - Import and check the mat** (new; models only). Copy `BenchStage/` and `Assets/Art/Textures/Bench/`.
- Import settings as `Configure`.
- Materials into `Assets/Art/Materials/Bench/`, where `BM_Mat.mat` lives (`Playtest3Session4Steps.cs:34`), defined Kit 1's way (`CafeLivedInSteps.Define`, `:665-720`):
  - textured materials get a white base, `_BaseMap` and `tiling`;
  - `BM_Silicone`: tiling 20, `BM_Grid_color.png` on Repeat;
  - `BM_Print`: `BM_Rulers.png` on Clamp;
  - the others are flat colours, metallic 0.
- **Checks:**
  - 1 mesh, 2,142 triangles (±0.5 %);
  - size (0.600, 0.006, 0.360) ±12 mm, bottom 0;
  - all 10 empties at their `at` ±1 mm (`Cup_1` (0.036, 0.0018, −0.024) … `Well_Small` (0.206, 0.002, −0.058));
  - a photo from 58°.
- I proved the empties survive the FBX only by a Blender round trip (exact). This step proves it in Unity.

**Step 3. The stage** (Systems' step 14, which replaces "Bench 2"'s slab, tray and caddy). Under `InspectRig/Stage (v2)`, all with local rotation 0:
- **The mat:**
  - `BM_RepairMat` at (0, bench top, −0.02), replacing `Mat` (`:103`). Switch the slab's renderer off; keep its collider only while anything still falls.
  - The InspectPoint goes to the mat's top, 0.005 above the bench (the slab's was 0.006).
  - `matFrame` = the mat's root. `cups`, `padSmall/Large` and `wellSmall/Large` = its empties.
- **The rest:**
  - `BT_ToolRest` at (`toolRestCentreX`/1000, 0.005, −0.02 + `toolRestCentreY`/1000). Phone (−0.135, −0.060), camera (−0.239, 0), watch (−0.115, −0.060).
  - **`toolRestCentreY` is a field Systems' `BenchLayout` must gain** (their table has x only, `systems-forgiving-bench.md:80`).
  - It replaces `Tool caddy`.
  - Each tool's slot is the matching `Seat_*` (tool origin at the seat, rotation the seat's).
- **The tray:** `BT_ScrapTray` at (`scrapTray.x`/1000, 0.005, −0.02 + `scrapTray.y`/1000), replacing `Parts tray`. `scrapSlots` = `Slot_1..3`.
- **The held cloth:** `BT_ClothHeld` goes in as a second model under `Tool_Cloth`, hidden at rest and shown while in the hand. `BT_Cloth` (folded) hides then.
- **Systems sets the keys:**
  - `Bunch` from the stroke's speed and `Press` from the press, with the cloth yawed so its +X points along the stroke;
  - `Bend` the same way on the brush.

**Step 4. Café dressing 1 - Import and place** (new; a `CafeLivedInSteps` sibling; recorded, with an undo; Environment's guards before and after).
- **Import:** copy `CafeKit/LI_*.fbx`, `cafe_dressing.json` and the four `Assets/Art/Textures/Cafe/LI_*.png`. Make the new LI_ materials from the manifest (four textured: white base, Clamp). Map the existing ones by name (`CF_Kraft`, `CF_Black`, `DC_Steel`, `DC_SteelDark`, `DC_Chalk`, `DC_Ceramic`, `DC_Sticker_Green`, `LI_CupPaper`, `LI_RecycleBlue`, `CF_PinRed`, `T2_Brass`).
- **Place** each piece at its manifest `placement`, in Unity world space, rotation (0, 180, 0) unless noted:

| Piece | Position | Notes |
|---|---|---|
| R1 `LI_PickupRow` | (−5.65, 1.00, 17.25) | |
| R2 `LI_DeskCorner` | (−3.775, 1.005, 14.08) | |
| `LI_GlassNotes` | (−4.05, 1.215, 13.681) | rotation (0, 0, 0): it faces Ace |
| R3 `LI_DonorParts` | (−3.375, floor, 13.88) | floor = ray down |
| R4 `LI_Whiteboard` | (−5.60, 1.92, 17.99) | |
| R5 `LI_PartsDelivery` | (−6.60, 1.095, 13.95) | |
| S1 `LI_MortgageLetter` | (0.30, 1.0955, 14.15) | rotation (0, 192, 0) |
| S2 `LI_FinalNotice` | (6.63, 1.36, 17.94) | a child of the door leaf if it swings (unverified); never rename the door |
| C1 `LI_SyrupRack` | (1.90, 1.00, 17.85) | switch off the three Synty `Drinks - Bottle_01..03` at x 1.74–2.06, recorded |
| C2 `LI_CupStation` | (0.70, 1.00, 17.80) | |
| C3 `LI_BarMat` | (−0.50, 1.00, 17.75) | |

- No colliders, no hover: décor.

**Step 5. The icons.** Copy `BenchIcons/` to `Assets/Data/Resources/UI/BenchIcons/`. TextureImporter: Sprite (2D and UI), mipmaps on, bilinear, max 256, no compression (UI/UX §5.3). The keys are the file names.

## 6. Checks

- **Bench tools 1:** all PASS, with the keys present on the two FBX (`sharedMesh.blendShapeCount`: `BT_ClothHeld` 2, `BT_Brush` 1).
- **Bench stage 1:** all PASS as listed in §5.
- **In the Bench lab's photos (Art's §6):**
  - the mat beside the device samples L* 38–46, b* ≤ −12. My approximate lamp gave L* 58–63, b* −12 to −15, so the hue holds and the value is the lamp's: measure it in Unity;
  - every compartment, the rest and the tray clear of U1–U5 at 16:9 and 16:10, at the TD's framings (his layout views are tighter than my 0.58 m renders);
  - each tool seated on both bars, no part of it under the mat.
- **Café dressing 1:** Art's overhead 10-pixel editor check (≥ 12 px for R1, R2, R4, R5, C1, C2, C3), my numbers below as the expectation; Night walk 2's solid list; Stations as reach (R2 must not block the bench's reach); Room 1's close-up check (no B3 piece in any device's frame: R2's west end is at x −4.40, east of today's widest frame).
- **Playtest 6:** does he say "repair mat" unprompted? Does the cloth "open up"? Does the checklist read from its pictures?

## 7. Results worth his eye

- **The overhead 10-pixel check** (smaller projected size at 1080p; pass ≥ 12; my stand-in room):

| Piece | Size on screen | Result |
|---|---|---|
| Pickup row | 97 × 14 | pass |
| Desk corner | 46 × 18 | pass |
| Whiteboard | 33 × 18 | pass |
| Parcel | 27 × 24 | pass |
| Syrup rack | 17 × 13 | pass |
| Cup station | 23 × 19 | pass |
| **Bar mat** | **25 × 9** | **fail by 3 px** (Art estimated about 10) |
| Donor parts, letter, notice, glass notes | | exempt |

  The bar mat is low and shallow. Options: a two-tier mug rack or a mug tree 0.25 m tall (about 13 px), or accept it as light dots on dark. Art's call; I left it.
- **The mat's colour:** see Checks.
- **The held cloth's tail** is seen nearly end-on from 58°, because it leans toward the camera, so it reads as a knob on the wad. If it reads odd in Unity: lean it less, or let it lean along the stroke instead (Systems already yaws the model).
- **Icons at 36 px:** the screwdriver and the pry tool are both dark with green grips; they differ by the blade (thin steel against flat steel). At 56 px every icon is clear.

## 8. The cut list

- **Mat:** the real mat's 124 small cells and 42 spares, its code and screw charts, the lidded boxes, the driver slots, any brand.
- **Rest:** hang holes, a brand plate, magnets, a fifth slot.
- **Tray:** handles, a lid, graduations.
- **Pickup row:** phones, watches and cameras (they'd read as jobs); visible brands; more than one loop of string per tag.
- **Desk corner:** cables off the bench; the multimeter's dial lettering.
- **Donor parts:** the router's lights.
- **Syrup rack:** label words (they wait for Content's maker); the pumps' dosing collars.
- **Cup station:** the dispenser's wall bracket; cups beyond the bottom one in each tube.
- **Not built (optional, Art A4):** `BT_ClothHalf`, the in-between pose. The pick-up motion hides the swap.

## 9. Next Blender session (ordered; now unblocked by the TD's spec)

1. **The camera's strap, body (32 mm deep, its roundings swapped) and the clean and jammed mechanisms (Art A6–A7).** Export at real size: `hero_lib.scale_build(made, children, k)`, with k = ½ for the camera and ⅓ for the watch (TD §6.1).
2. **Screws at 3.0 mm** (the TD's number; the feel sheet's 3–3.5 mm): phone black-oxide, watch blued. Plus `BT_ScrewHole.png` (the decal, 1.15 × head).
3. **The device icons** `part_*` and `cover_*` from those meshes.
4. **Review renders at the TD's layout views** (phone aim (+10, −14) mm at 0.48 m; camera (−58, −8) at 0.49 m; watch (+10, −20) at 0.42 m; FOV 40°) and their zoom limits, both aspects, U1–U6 drawn. `review_bench_stage.py`'s `close_up_camera(dist, aim)` already takes them.

## 10. Open questions (his)

1. The bar mat at 9 px from above: add a mug rack, or accept it as dots?

## 11. Flags

- **The import step drops blend shapes** (`BlenderKitSteps.cs:285`). Without the change in §5, `Press`, `Bunch` and `Bend` vanish.
- **Contracts, new and kept:**
  - kept: the file names `BT_Cloth` and `BT_Brush`; every tool's origin at its working tip (`BT_ClothHeld`'s is the pad's face on the glass);
  - new: the empty names `Cup_1..6`, `Pad_*`, `Well_*` (Art's), and `Seat_*`, `Slot_1..3` (mine, after Systems' fields);
  - new: the rig-frame build of the mat, rest and tray.
- **Systems' `BenchLayout` needs `toolRestCentreY`** (§5, step 3).
- **Art's ruler start moves 5 mm** (0 at x −285). Art's grid name gains `_color`.
- **The bar mat fails Art's 10-pixel rule** (§7).
- **Placeholder copy:** every word on the tags, the whiteboard, the statement and the notice. The lender "Pennywhistle Mortgage Co." is invented; Content should confirm no real lender uses it (brands-fictional lock).
- **Fonts:** fetched from npm and Debian, not LFS. The same families, the static League Gothic instead of the variable one. Licences sit beside them. The Producer should prefer the repo's LFS files when copying back.
- **The FINAL NOTICE reads from above as a 14 × 10 px white speck** on the back wall's east end. Art wanted the story props private. It reads as "a paper", not as its words. Fine unless he notices it.
- **The stand-in room's menu board place is a guess** (x −0.5, bottom 1.95). It only frames the overhead; nothing is exported from it.
- **The review lamp is approximate** (§4.3). Colour judgements belong to Unity photos.
- **Not mine, noticed:** the scene's three role files still say the overhead is "50°, ~20 m" (Art's flag). I used 43° / 34 m / FOV 44° throughout.

## 12. Against the feel sheet (`feel/feel-sheet-2026-10-14.md`, landed 08:03)

- **Agree:**
  - a blue gridded mat that fills the close-up (ReStory's fills about 85 % of the frame's width; ours about 90 % at today's 0.58 m);
  - "nothing lost": every place a piece can go is a moulded cup, pad, well or tray slot, an empty the flights aim at;
  - the cloth and brush used where the dirt is, with no work-spot step.
- **Differ, on purpose:**
  - **ReStory's one brass dish** at the mat's corner, against our six numbered cups. One screw per cup keeps the reassembly order visible, the stalled moment the sheet names ("what goes back next"). The tink survives through the steel discs. The sheet's "physics only inside the dish" can live as a 1 mm settle in a cup.
  - **ReStory's four pale lanes**, against our one pad and one well. The auto-tray needs fewer, named places.
- **Screw heads:** the sheet says 3–3.5 mm (14 px); the TD says 3.0 mm. My cups detail used Art's 2.4 mm stand-ins (`bench-6`). The 18 mm cups hold either.
- **The cleaning footprint:** ReStory's ring is about 45 % of the part's width. Our pressed cloth (50 × 52 mm) is about 42 % of the real-size camera body's 120 mm. The brush (24 mm) is 20 %. If Systems sizes the clean radius from the visible pad, the cloth matches ReStory's coarse feel.
- **Against the other specs:**
  - UI/UX's zones hold in all three layouts.
  - The TD's real size changes nothing here: the mat, rest, tray and tools are real.
  - Systems' field names are matched.
  - Audio's steel disc is in.
