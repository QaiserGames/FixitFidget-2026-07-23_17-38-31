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

`fixit_blender.py` holds the shared helpers (materials, boxes, lofts, struts, the audit, FBX export),
`barnaby.py` and `grace_house.py` the pieces.

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
