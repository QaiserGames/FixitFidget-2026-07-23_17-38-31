"""
Build the cafe props (cafe_props.py) and write one FBX per piece (for Unity), the .blend, and a manifest the Unity
step reads (fbx/cafe_props.json, the house kit's format: each piece's size in Unity axes (across, up, deep), bottom,
back, triangles, materials and islands as built, plus where it goes, its origin and its collider; the kit's
materials with their colours and smoothness, the CF_ ones to be made, the existing ones named for reference).

With bpy:  python3 export_cafe_props.py [-- <fbx folder> <blend path>]
Defaults:  fbx/ and CafeProps_v1.blend beside this script.
In the repo the script would sit in Tools/Blender/, the FBX in Assets/Art/Models/Cafe/, the .blend in BlenderSource/.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, '/home/claude/cafe/inputs/Tools/Blender')
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import cafe_props as cp  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(HERE, 'fbx')
blend_path = args[1] if len(args) > 1 else os.path.join(HERE, 'CafeProps_v1.blend')

fb.clear_scene()
results = cp.build_all()
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
    if r['floating'] == [] and ob.name != 'CK_Pothos' and r['min'][2] < -0.0005 and r['group'] not in ('Room',):
        problems += 1
        print('BELOW THE FLOOR', ob.name, r['min'][2])
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    entry = {
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': False, 'tris': r['tris'],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # 0 on the floor; negative: it hangs below its origin
        'back': r['max'][1],                        # the back's y in Blender (the front is -y)
        'materials': r['materials'], 'islands': r['islands'],
        'origin': r['origin'], 'place': r['place'], 'collider': r['collider'],
        'references': cp.REFERENCES.get(ob.name, []),
    }
    if r['label_faces']:
        entry['label'] = {'material': 'CF_BagLabel', 'faces': r['label_faces'],
                          'size_m': [round(cp.LABEL['x1'] - cp.LABEL['x0'], 3), round(cp.LABEL['z1'] - cp.LABEL['z0'], 3)],
                          'uv': "0..1 on the label's front face: u left to right, v bottom to top, as seen from the front"}
    pieces.append(entry)
    print('%-18s %5d tris  %s' % (ob.name, r['tris'], r['size']))

materials = {}
for name, (hexstr, smooth, why) in cp.CF_MATERIALS.items():
    e = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0.0, 'why': why}
    if name in cp.TRANSPARENT:
        e['transparent'] = True
    if name in cp.TEXTURED:
        e['texture'] = cp.TEXTURED[name]
    materials[name] = e
existing = {name: {'hex': h.lstrip('#'), 'smoothness': s, 'metallic': 0.0, 'use': why}
            for name, (h, s, why) in cp.EXISTING.items()}

# lay the pieces out in the .blend so they can be looked at (the FBX files went out from the origin)
x = 0.0
for r in results:
    ob = r['object']
    w = r['size'][0]
    if r['group'] == 'Room':
        ob.location = (0.0, 4.0 + (2.0 if ob.name == 'CK_SkylightWell' else 0.0), 3.2)
        continue
    ob.location = (x + w / 2, 0.0, -r['min'][2])
    x += w + 0.4
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'kit': 'cafe_props', 'note': 'Unity (x, y, z) = Blender (-x, z, -y); the front of each piece faces Unity +Z as built',
            'pieces': pieces, 'materials': materials, 'existing_materials': existing}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_props.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
