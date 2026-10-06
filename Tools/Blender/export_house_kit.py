"""
Build the house dressing kit (house_kit.py) and write one FBX per piece (for Unity), the .blend, and a manifest
the Unity step reads (house_kit.json: the pieces with their sizes and triangle counts as built, and the kit's
materials with their colours and smoothness; the paintings' textures by path).

Headless:  blender -b --python Tools/Blender/export_house_kit.py [-- <fbx folder> <blend path>]
With bpy:  python Tools/Blender/export_house_kit.py [-- <fbx folder> <blend path>]
Defaults: Assets/Art/Models/HouseKit/ and BlenderSource/HouseKit_v1.blend in this repository.
Then in Unity: Fixit Fidget > Night > House kit 1 - Import and check the kit (the models only).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
import house_kit as hk  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'HouseKit')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'HouseKit_v1.blend')

fb.clear_scene()
results = hk.build_all()
pieces = []
problems = 0
for r in results:
    ob = r['object']
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-9)
    if degenerate:
        problems += 1
        print('DEGENERATE', ob.name, degenerate, 'face(s) with no area')
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    pieces.append({
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': r['stretch'], 'tris': r['tris'],
        # Unity's axes: across, up, deep
        'size': [sx, sz, sy],
        'bottom': r['min'][2],                      # where the lowest point is (0 on the floor; negative: it hangs)
        'back': r['max'][1],                        # the back's y (0 for a wall piece); the front is -y in Blender
        'materials': r['materials'], 'islands': r['islands'],
    })
    print('%-24s %5d tris  %s' % (ob.name, r['tris'], r['size']))
materials = {}
for name, (hexstr, smooth) in hk.HK_MATERIALS.items():
    entry = {'hex': hexstr.lstrip('#'), 'smoothness': smooth}
    if name.startswith('HK_Painting_'):
        entry['texture'] = 'Assets/Art/Textures/HouseKit/' + name + '.png'
    if name == 'HK_Lantern_Glass':
        entry['emission'] = 'FFD9A0'
    materials[name] = entry
# lay the pieces out in the .blend so they can be looked at (the FBX files went out from the origin)
x = 0.0
for r in results:
    ob = r['object']
    w = r['size'][0]
    ob.location = (x + w / 2, 3.0, 0 if r['min'][2] >= 0 else -r['min'][2])
    x += w + .4
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'pieces': pieces, 'materials': materials,
            'grace_materials': {k: {'hex': v[0].lstrip('#'), 'smoothness': v[1]} for k, v in gh.GH_MATERIALS.items()}}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'house_kit.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d with floating parts' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
