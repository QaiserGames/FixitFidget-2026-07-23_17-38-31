"""
Build the drinks machine's six buttons (drink_buttons.py, 26 Oct 2026) and write one FBX per drink (the cap with its
picture as a child, for Unity), the .blend, and a manifest the Unity step reads (DB_DrinkButtons.json: each button's size in
Unity axes, its triangles, materials and islands as built, where it sits in the machine's frame, the press travel; the DB_
materials with their colours).

With bpy:  python3 Tools/Blender/export_drink_buttons.py [-- <fbx folder> <blend path>]
Defaults:  Assets/Art/Models/DrinkButtons/ and BlenderSource/DrinkButtons_v1.blend, from the repo root.
Unity:     Fixit Fidget > Drinks > Buttons 1 - The machine's new buttons (DrinkButtonsSteps.cs).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import drink_buttons as db  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'DrinkButtons')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'DrinkButtons_v1.blend')

fb.clear_scene()
results = db.build_all()
problems = 0
buttons = {}
for r in results:
    ob = r['object']
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
    if degenerate:
        problems += 1
        print('DEGENERATE', ob.name, degenerate, 'face(s) with no area')
    entry = buttons.setdefault(r['name'], {'name': r['name'], 'icon': db.ICON_OF[r['name']], 'index': db.DRINKS.index(r['name']),
                                            'place': list(db.machine_place(db.DRINKS.index(r['name']))), 'pieces': {}})
    sx, sy, sz = r['size']
    entry['pieces'][ob.name] = {
        'tris': r['tris'], 'size': [sx, sz, sy],      # Unity's axes: across, up, deep
        'front': r['min'][1],                          # the most negative y: how far it stands in front of the paddle's origin
        'back': r['max'][1],
        'materials': r['materials'], 'islands': r['islands'], 'turned': r['turned'],
    }
    print('%-22s %5d tris  %s  front %.4f' % (ob.name, r['tris'], r['size'], r['min'][1]))

for name in db.DRINKS:
    cap = bpy.data.objects['DB_Cap_' + name]
    fb.export_fbx_tree(cap, os.path.join(fbx_dir, 'DB_Cap_%s.fbx' % name))

# lay them out in the .blend in a row, as on the machine, so they can be looked at (the FBX went out from the origin)
for i, name in enumerate(db.DRINKS):
    bpy.data.objects['DB_Cap_' + name].location = db.machine_place(i)
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)

manifest = {
    'version': 1, 'kit': 'drink_buttons',
    'note': 'Unity (x, y, z) = Blender (-x, z, -y). Each cap is built in its paddle\'s frame: parent DB_Cap_<Name> under '
            '"<Name> ceramic paddle" of BeverageDispenserV2 with no offset and no turn; its front faces the player as the paddle\'s does. '
            'place is the paddle\'s origin in the machine\'s Blender frame (x, y, z), for reference only.',
    'press_travel': db.PRESS,
    'cap': {'across': db.CAP_W, 'up': db.CAP_H, 'front': db.CAP_FRONT, 'back': db.CAP_BACK, 'label': db.LABEL * 2,
            'pixel': db.PX, 'picture_px': 128},
    'buttons': [buttons[name] for name in db.DRINKS],
    'materials': {name: {'hex': hexstr.lstrip('#'), 'smoothness': 0.45, 'metallic': 0.0, 'why': why}
                  for name, (hexstr, why) in db.DB_MATERIALS.items()},
    'existing_materials': {name: {'hex': hexstr.lstrip('#'), 'use': 'the machine\'s own; the cap of that drink'}
                           for name, hexstr in db.BF_MATERIALS.items()},
}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'DB_DrinkButtons.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d buttons, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(buttons), sum(r['tris'] for r in results), fbx_dir, blend_path, problems))
