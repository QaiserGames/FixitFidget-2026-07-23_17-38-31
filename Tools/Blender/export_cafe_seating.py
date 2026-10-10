"""
Build the cafe's seats (cafe_seating.py) and write one FBX per piece (for Unity), the .blend, and a manifest the Unity
step reads (cafe_seating.json, the house kit's format: name, group, what, tris, size in Unity axes (across, up, deep),
bottom, back, materials, islands; the kit's materials with colour and smoothness). It adds, per piece, what the old
library piece was (replaces), whether it hangs on a wall, where a body sits (seats: the seat surface's centre in the
piece's own Unity frame, the way it faces, chair or bench) and a collider that is the piece's solid.

  python3 export_cafe_seating.py [-- <fbx folder> <blend path>]
  (or: blender -b --python export_cafe_seating.py -- ...)
Defaults: ./fbx/ and ./CafeSeating_v1.blend beside this script. In the repository they would be
Assets/Art/Models/CafeKit/ and BlenderSource/CafeSeating_v1.blend.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cafe_seating as cs  # noqa: E402  (puts the studio's Tools/Blender on the path)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(HERE, 'fbx')
blend_path = args[1] if len(args) > 1 else os.path.join(HERE, 'CafeSeating_v1.blend')


def u(v):
    """A Blender point in the piece's own Unity frame (Unity (x, y, z) = Blender (-x, z, -y)), rounded."""
    x, y, z = v
    return [round(-x + 0.0, 4), round(z + 0.0, 4), round(-y + 0.0, 4)]


# Where a body sits on each seat: the seat surface's centre (its dished low point there, not the rim), in Blender
# coordinates, then given to Unity in the piece's frame. The rim heights are the old library's seat heights.
SEATS = {
    'CK_Chair_Cafe':    [{'surface': (0.0, -0.0356, 0.4444), 'rim': 0.4511, 'style': 'Chair'}],
    'CK_Chair_Spindle': [{'surface': (0.0, -0.0303, 0.4430), 'rim': 0.4524, 'style': 'Chair'}],
    'CK_Stool_Bar':     [{'surface': (0.0, -0.0065, 0.7449), 'rim': 0.7517, 'style': 'Chair'}],
    'CK_Stool_Low':     [{'surface': (-0.0115, 0.0, 0.4452), 'rim': 0.4522, 'style': 'Chair'}],
    # the banquette: anywhere along the bay; put x where the 2-top's centre is. 0.28 m off the wall (see report)
    'CK_BanquetteBay':  [{'surface': (0.0, -0.280, 0.4440), 'rim': 0.4500, 'style': 'Bench', 'along': [-1.0, 1.0]}],
}

fb.clear_scene()
results = cs.build_all()
pieces = []
problems = 0
for r in results:
    ob = r['object']
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    if r['degenerate']:
        problems += 1
        print('DEGENERATE', ob.name, r['degenerate'])
    if r['coplanar_same'] or r['coplanar_opposite']:
        problems += 1
        print('COPLANAR', ob.name, r['coplanar_same'], r['coplanar_opposite'], r['coplanar_examples'])
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    (x0, y0, z0), (x1, y1, z1) = r['min'], r['max']
    entry = {
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'tris': r['tris'],
        # Unity's axes: across, up, deep
        'size': [sx, sz, sy],
        'bottom': r['min'][2],                      # 0 on the floor; the window counter hangs, its lowest point 0.34
        'back': r['max'][1],                        # the back's y (0 for a wall piece); the front is -y in Blender
        'materials': r['materials'], 'islands': r['islands'],
        'replaces': r['old'], 'wall': r['wall'],
        # a box collider that is the piece's solid (Unity frame: centre, size)
        'collider': {'centre': [round(-(x0 + x1) / 2 + 0.0, 4), round((z0 + z1) / 2, 4), round(-(y0 + y1) / 2 + 0.0, 4)],
                     'size': [sx, sz, sy]},
    }
    if ob.name in SEATS:
        entry['seats'] = []
        for s in SEATS[ob.name]:
            seat = {'pose': u(s['surface']), 'rim': s['rim'], 'faces': [0, 0, 1], 'style': s['style']}
            if 'along' in s:
                seat['along_x'] = s['along']
            entry['seats'].append(seat)
    pieces.append(entry)
    print('%-22s %5d tris  %s  islands %d' % (ob.name, r['tris'], r['size'], r['islands']))
materials = {}
for name, (hexstr, smooth, existing) in cs.CK_MATERIALS.items():
    materials[name] = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0, 'existing': existing}
# lay the pieces out in the .blend so they can be looked at (the FBX files went out from the origin)
x = 0.0
for r in results:
    ob = r['object']
    w = r['size'][0]
    ob.location = (x + w / 2, 2.0, 0.0)
    x += w + 0.5
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'kit': 'cafe_seating', 'built_by': 'cafe_seating.py', 'pieces': pieces, 'materials': materials,
            'notes': 'Unity (x, y, z) = Blender (-x, z, -y). Seat poses and colliders are in each piece\'s own Unity frame. '
                     'Every material is an existing project material (Assets/Art/Materials), mapped by name.'}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_seating.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
