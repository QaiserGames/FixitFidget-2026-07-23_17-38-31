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
| `unity_mesh.py` | Reads meshes (positions, triangles) out of a Unity text asset, for measuring things like the house shell | (a helper) |

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
- Origin at the bottom centre. A piece hung on a wall (cupboards, wall frames) has its origin at the
  bottom centre of its back, with the back at y = 0.
- Nothing of a piece goes below the floor: when a piece is finished, points a hair under z = 0 (the
  square end of a splayed leg) are put on it. Things that aren't furniture (the mock-up's floor slabs)
  say `floor=False`.
- No two faces share a plane: make the part that should show a few millimetres proud.
- Doors have their origin on the hinge line at the floor, so Unity can swing them about Y.
- Colours are sRGB hex, converted to linear for Blender. The materials are named after the Unity
  materials they become: the café's own (`CC_Wood_Counter`, `DC_Steel`, `T2_Brass`, ...) or new
  ones (`GH_*` for Grace's house; `Night 1 - *` for Barnaby, the placeholder's own).
- FBX: Forward -Z, Up +Y, Apply Unit, FBX Scale All, the axis turn baked in (the node comes in upright
  with no turn and scale 1), face smoothing (flat shading survives), exported from a copy at the origin
  so a layout in the `.blend` never moves a piece.

## In Unity afterwards

- Barnaby: `Fixit Fidget > Night > Night 1 - Barnaby: use the Blender model (the prefab only)`.
- Grace's furniture: `Fixit Fidget > Night > Break-ins - Grace's furniture: import and check (the models only)`.

Both set the import settings, map the materials by name, check what came in, and photograph it. Neither
changes the café scene.
