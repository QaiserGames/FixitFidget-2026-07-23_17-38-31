"""
Build Grace's two cover pieces (grace_cover.py) and write one FBX each (for Unity), the .blend, and a manifest the
Unity step reads (grace_cover.json: each piece's size and triangles as built).

Headless:  blender -b --python Tools/Blender/export_grace_cover.py [-- <fbx folder> <blend path>]
With bpy:  python Tools/Blender/export_grace_cover.py [-- <fbx folder> <blend path>]
Defaults: Assets/Art/Models/GraceHouse/ (beside her other pieces) and BlenderSource/GraceCover_v1.blend.
Then in Unity: Fixit Fidget > Night > Grace's cover 1 - Import and check the pieces (the models only).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_cover as gc  # noqa: E402
import grace_house as gh  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'GraceHouse')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'GraceCover_v1.blend')

fb.clear_scene()
results = gc.build_all()
pieces = []
problems = 0
for r in results:
    ob = r['object']
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
    if degenerate:
        problems += 1
        print('DEGENERATE', ob.name, degenerate, 'face(s) with no area')
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    pieces.append({
        'name': ob.name, 'room': r['room'], 'what': r['what'], 'tris': r['tris'],
        'size': [sx, sz, sy], 'bottom': r['min'][2], 'back': r['max'][1],
        'materials': r['materials'], 'islands': r['islands'],
    })
    print('%-20s %5d tris  %s' % (ob.name, r['tris'], r['size']))
x = 0.0
for r in results:
    ob = r['object']
    ob.location = (x + r['size'][0] / 2, 0.0, 0.0)
    x += r['size'][0] + .5
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'pieces': pieces,
            'grace_materials': {k: {'hex': v[0].lstrip('#'), 'smoothness': v[1]} for k, v in gh.GH_MATERIALS.items()}}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'grace_cover.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
