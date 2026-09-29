"""
Build the layout v2 pieces of Grace's house (grace_house_v2.py: the quarter-turn stairs, the bedroom set, the
bedroom's doors, the 2.0 m counter) and write one FBX per piece (for Unity), the .blend and a manifest.
Step 4's pieces (export_grace_house.py) are left as they are.

Headless:  blender -b --python Tools/Blender/export_grace_house_v2.py [-- <fbx folder> <blend path>]
In Blender: open this file in the Scripting tab and Run Script (the scene is cleared first).
Defaults: Assets/Art/Models/GraceHouse/ and BlenderSource/GraceHouse_v2.blend in this repository.
Then in Unity: Fixit Fidget > Night > Break-ins - Grace's furniture: import and check (the models only).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
import grace_house_v2 as g2  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'GraceHouse')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'GraceHouse_v2.blend')

fb.clear_scene()
results = g2.build_all()
manifest = []
for r in results:
    ob = r['object']
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    manifest.append({k: r[k] for k in ('room', 'what', 'tris', 'size', 'materials', 'islands')} | {
        'name': ob.name, 'floating': r['floating']})
    print('%-26s %5d tris  %s' % (ob.name, r['tris'], r['size']))
# lay the pieces out in the .blend so they can be looked at (the FBX files went out from the origin)
x = 0.0
for r in results:
    ob = r['object']
    if ob.name == 'GH_Stairs_L':
        continue
    w = r['size'][0]
    ob.location = (x + w / 2, 3.0, 0)
    x += w + .4
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
with open(os.path.splitext(blend_path)[0] + '.json', 'w') as fh:
    json.dump({'pieces': manifest, 'materials': {k: v[0] for k, v in {**gh.CAFE_MATERIALS, **gh.GH_MATERIALS}.items()}}, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s' % (len(manifest), sum(m['tris'] for m in manifest), fbx_dir, blend_path))
