# Tools/Blender: props built with Python in Blender

These scripts build the game's props in Blender 5.2 from code, so a piece can be changed by changing a
number and building it again. They follow the café's art rules (`claude/art-style-guide.md`,
`claude/furniture-library.md` in the project docs): real sizes in metres, chunky and friendly,
flat-shaded, one-segment bevels, junctions worked out from boundaries, and an audit that fails any part
floating more than 2 mm from the rest.

| Script | What it builds | Output |
|---|---|---|
| `export_barnaby.py` | Barnaby, Grace's garden gnome (Night 1) | `Assets/Art/Models/Night/Barnaby.fbx`, `BlenderSource/Night1_Barnaby.blend` |
| `export_grace_house.py` | Grace's ground floor: 24 pieces (break-ins, step 4) | `Assets/Art/Models/GraceHouse/GH_*.fbx`, `BlenderSource/GraceHouse_GroundFloor.blend` (+ `.json`, what was built) |
| `review_grace.py` | The same pieces laid out, with review renders (Cycles) | `Tools/Blender/out/` (not kept in git) |
| `export_one.py` | One of Grace's pieces again, by name, leaving the others alone | e.g. `blender -b --python Tools/Blender/export_one.py -- GH_Rug Assets/Art/Models/GraceHouse` |
| `mockup_grace.py` | The first render-only mock-up of her ground floor (layout v1, superseded: it was drawn 0.68 m too deep, for a slim Ace) | `Tools/Blender/out/` |
| `plan_v2.py` | Layout v2 of Grace's house, both floors, for a 1.0 m wide Ace: every number in one place (the shell as measured, the stairs, the rooms, where each piece goes) | (read by the scripts below) |
| `draw_plan_v2.py` | The plan picture of layout v2, with Ace's real footprint along the way (needs matplotlib) | e.g. `grace-house-plan-v2.png` |
| `mockup_v2.py` | A render-only mock-up of layout v2: both floors, looked into from the street, by day and by night; the new pieces are plain stand-ins | `Tools/Blender/out/` |
| `draw_facade_v2.py` | Her front from the street, the door today and widened to 1.30 m, drawn from the house's own meshes | e.g. `grace-house-front-door-v2.png` |
| `export_bench_tools.py` | The bench's five tools (`bench_tools.py`, 7 Oct 2026): screwdriver, tweezers, brush, pry tool, cloth; origin at the working tip, the tweezers' leaves as children at the heel | `Assets/Art/Models/BenchTools/BT_*.fbx` (+ `bench_tools.json`), `BlenderSource/BenchTools_v1.blend` |
| `export_grace_cover.py` | Grace's cover for session 5 (`grace_cover.py`): a bar-height kitchen island (1.12 m) and a stack of three boxes (1.14 m) | `Assets/Art/Models/GraceHouse/GH_KitchenIsland.fbx`, `GH_BoxStack.fbx` (+ `grace_cover.json`), `BlenderSource/GraceCover_v1.blend` |
| `review_bench_tools.py` | Review renders of both: the tools as the inspection camera sees them, each tool alone, the cover pieces with a crouched Ace behind them from Grace's eye | `Tools/Blender/out/` (not kept in git) |
| `unity_mesh.py` | Reads meshes (positions, triangles) out of a Unity text asset, for measuring things like the house shell | (a helper) |
| `bench_stage.py`, `bench_stage_textures.py`, `export_bench_stage.py` | The silicone repair mat (14 Oct 2026, Art's A1): grid, rulers, six screw cups with steel discs, the new-part pad, the cover well, and its empties `Cup_1..6`, `Pad_Small/Large`, `Well_Small/Large`; built in the bench rig's own frame (no turn under the stage) | `Assets/Art/Models/BenchStage/BM_RepairMat.fbx` (+ `bench_stage.json`), `Assets/Art/Textures/Bench/BM_Grid_color.png`, `BM_Rulers.png`, `BlenderSource/BenchStage_v1.blend` |
| `export_bench_tools.py` (v4) | Adds to the tools: `BT_ClothHeld` (keys `Press`, `Bunch`), the `Bend` key on `BT_Brush`, the thinner `BT_Cloth`, `BT_ToolRest` with its `Seat_*` empties, `BT_ScrapTray` | `Assets/Art/Models/BenchTools/` (+ `bench_tools.json` v3), `BlenderSource/BenchTools_v4.blend` |
| `review_bench_stage.py` | The close-up review: each device on the mat from the close-up camera (58 degrees, 0.58 m, 40 degree lens) with UI/UX's zones drawn over, the mat alone, the cloth's states and the brush's bend, a 2x zoom on the cups; the mat's rendered colour | `Tools/Blender/out/bench-stage/` |
| `render_bench_icons.py` | The checklist card's icons (256 px, transparent, an 8 px outline): the eight generic ones and (14 Oct, wave 2) the five per device, `part_phone`, `part_watch`, `part_camera`, `cover_phone`, `cover_watch` (`ICONS=` for a subset); a contact sheet at 36 and 56 px on the card's cream | `Assets/Data/Resources/UI/BenchIcons/*.png`, `Tools/Blender/out/bench-icons-contact-sheet.png` |
| `hero_phone.py`, `hero_watch.py`, `hero_camera.py`, `hero_lib.py`, `export_hero_devices.py` | The hero devices (wave 2, 14 Oct 2026): AUTHORED at 1x / 3x / 2x, EXPORTED REAL through `hero_lib.scale_build` (the TD's one scale); screws 3.8 mm pan heads (phone black oxide, watch blued); Grace's camera v3.6 (a real neck strap: lugs, split rings, protectors, keepers, a gathered loop; the body 32 mm deep with rounded ends; the clean and jammed shutter mechanisms). `devices.json` v3: `units: real`, `authoredScale`, the prefab `moves`, the screws, the REAL audit (+-7 %) | `Assets/Art/Models/Devices/HD_*.fbx` (+ `devices.json`), `BlenderSource/Hero*_v3.blend` |
| `bench_screw_hole.py` | `BT_ScrewHole.png` (Art A8): the hole a screw leaves, a decal on a quad 1.15 x the head: a dark bore 0.48 x the head with two thread rings, a lit lip, a soft dip, a witness ring; 256 px RGBA (Pillow, no Blender) | `Assets/Data/Resources/Bench/BT_ScrewHole.png` |
| `review_devices_wave2.py` | The devices at real size on the mat from the TD's framings (layout view and zoom limit, 16:9 and 16:10, UI/UX's U1-U6 drawn), screws out in their cups with the hole decal, numbers (fill, screw px, zone checks), and a turntable of Grace's camera | `Tools/Blender/out/devices-wave2/` |
| `cafe_repairside.py`, `cafe_cafeside.py`, `cafe_wave1_textures.py`, `export_cafe_wave1.py` | The café full from above (Art's B3): the pickup row, Ace's desk corner and its glass notes, the donor parts, the whiteboard, the parts delivery, the syrup rack, the cup station, the bar mat, the mortgage letter and the final notice | `Assets/Art/Models/CafeKit/LI_*.fbx` (+ `cafe_dressing.json`), `Assets/Art/Textures/Cafe/LI_Tags.png`, `LI_Whiteboard.png`, `LI_Mortgage.png`, `LI_FinalNotice.png`, `BlenderSource/CafeDressing_v1.blend` |
| `review_cafe_wave1.py` | The overhead review from the real CmShopCam (43 degrees, 34 m, 44 degree lens) before and after, a 10-pixel check, a turntable | `Tools/Blender/out/cafe-wave1/` |

`fixit_blender.py` holds the shared helpers (materials, boxes, lofts, struts, the audit, FBX export),
`barnaby.py` and `grace_house.py` the pieces.

The layout v2 scripts run from this folder, for example:

```
blender -b --python Tools/Blender/mockup_v2.py -- Tools/Blender/out ground
python Tools/Blender/draw_plan_v2.py Tools/Blender/out/grace-house-plan-v2.png
python Tools/Blender/draw_facade_v2.py "Assets/Playtests/AcesCafeLayout/Street doors.asset" Tools/Blender/out/grace-house-front-door-v2.png
```

`mockup_v2.py` takes a view after the output folder: `ground`, `first`, `night`, `from-the-sw` or `all`.

## Running them

In Blender: open a script in the **Scripting** tab and press **Run Script**. It clears the scene first,
so run it in a new file. Or headless, from the repository's folder:

```
blender -b --python Tools/Blender/export_grace_house.py
```

They also run with Blender as a Python module (`pip install bpy==5.2.2`, Python 3.13), which is how they
were first run (29 Sept 2026, overnight). The `.blend` files are Blender 5.2 files.

## The conventions

- 1 Blender unit = 1 m. Z is up. A piece's front faces -Y, which is +Z in Unity.
- Origin at the bottom centre. A tool's origin is its working tip, the handle up +Z (`bench_tools.py`). A piece hung on a wall (cupboards, wall frames) has its origin at the
  bottom centre of its back, with the back at y = 0.
- Nothing of a piece goes below the floor: when a piece is finished, points a hair under z = 0 (the
  square end of a splayed leg) are put on it. Things that aren't furniture (the mock-up's floor slabs)
  say `floor=False`.
- No two faces share a plane: make the part that should show a few millimetres proud.
- Every closed shell faces outward: `Prop.finish` checks each closed shell's volume and turns one built inside out the
  right way round (`orient_shells`). Barnaby's flopped hat had come out inside out, so in Unity you saw into his head
  (6 Oct 2026); the report says how many were turned.
- Doors have their origin on the hinge line at the floor, so Unity can swing them about Y.
- The bench stage's mat, rest and tray (14 Oct 2026) are the exception to "front faces -Y": they are built in the
  InspectRig's own frame (the player at Blender +Y) so they go under the rig's stage with no turn.
- Colours are sRGB hex, converted to linear for Blender. The materials are named after the Unity
  materials they become: the café's own (`CC_Wood_Counter`, `DC_Steel`, `T2_Brass`, ...) or new
  ones (`GH_*` for Grace's house; `Night 1 - *` for Barnaby, the placeholder's own).
- FBX: Forward -Z, Up +Y, Apply Unit, FBX Scale All, the axis turn baked in (the node comes in upright
  with no turn and scale 1), face smoothing (flat shading survives), exported from a copy at the origin
  so a layout in the `.blend` never moves a piece.

## In Unity afterwards

- The bench's tools: `Fixit Fidget > Bench > Bench tools 1 - Import and check the tools (the models only)`. Since 14 Oct
  its manifest also carries the rest, the tray and the held cloth; BT_ClothHeld and BT_Brush need `importBlendShapes = true`
  (the step sets it false today), and the rest's `Seat_*` are empties (children with no mesh).
- The repair mat (14 Oct): a step to come (Bench stage 1): the FBX, its BM_ materials with their two pictures and tiling.
- Grace's cover: `Fixit Fidget > Night > Grace's cover 1 - Import and check the pieces (the models only)`.
- Barnaby: `Fixit Fidget > Night > Night 1 - Barnaby: use the Blender model (the prefab only)`.
- Grace's furniture: `Fixit Fidget > Night > Break-ins - Grace's furniture: import and check (the models only)`.

Both set the import settings, map the materials by name, check what came in, and photograph it. Neither
changes the café scene.
