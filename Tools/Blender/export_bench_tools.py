"""
Build the bench's five tools (bench_tools.py, v3: the measured set in the house style) and write one FBX per tool (for Unity), the .blend, and a
manifest the Unity step reads (bench_tools.json: each tool's size and triangles as built, how many meshes it is, and
the BT_ materials with their colours, smoothness and metallic).

Headless:  blender -b --python Tools/Blender/export_bench_tools.py [-- <fbx folder> <blend path>]
With bpy:  python Tools/Blender/export_bench_tools.py [-- <fbx folder> <blend path>]
Defaults: Assets/Art/Models/BenchTools/ and BlenderSource/BenchTools_v3.blend in this repository.
Then in Unity: Fixit Fidget > Bench > Bench tools 1 - Import and check the tools (the models only).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import bench_tools as bt  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'BenchTools')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'BenchTools_v3.blend')

fb.clear_scene()
results = bt.build_all()
pieces = []
problems = 0
for r in results:
    ob = r['object']
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    for o in [ob] + r['children']:
        degenerate = sum(1 for poly in o.data.polygons if poly.area < 1e-10)
        if degenerate:
            problems += 1
            print('DEGENERATE', o.name, degenerate, 'face(s) with no area')
    path = os.path.join(fbx_dir, ob.name + '.fbx')
    if r['children']:
        fb.export_fbx_tree(ob, path)
    else:
        fb.export_fbx(ob, path)
    sx, sy, sz = r['size']
    pieces.append({
        'name': ob.name, 'tool': r['tool'], 'what': r['what'], 'tris': r['tris'],
        'meshes': 1 + len(r['children']),
        'children': [{'name': c.name, 'at': [round(-c.location.x, 4), round(c.location.z, 4), round(-c.location.y, 4)]} for c in r['children']],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # 0: the tip is the origin
        'materials': r['materials'], 'islands': r['islands'],
    })
    print('%-18s %4d tris  %s  %d mesh(es)' % (ob.name, r['tris'], r['size'], 1 + len(r['children'])))
materials = {name: {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal} for name, (hexstr, smooth, metal) in bt.BT_MATERIALS.items()}
# lay the tools out in the .blend so they can be looked at, points down on a line (the FBX files went out from the origin)
x = 0.0
for r in results:
    r['object'].location = (x, 0.0, 0.0)
    x += r['size'][0] + .06
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 2, 'pieces': pieces, 'materials': materials}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'bench_tools.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d tools, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
