"""
Build the café's bar (cafe_bar.py), audit every piece, and write one FBX per piece (for Unity), the .blend, and the
manifest the Unity step reads (cafe_bar.json, in the house kit's format: name, group, what, tris, size in Unity axes
[across, up, deep], bottom, back, materials, islands; plus where each piece stands in the café and how it is turned,
and the kit's materials with colour and smoothness; the lamp's light).

With bpy:  python3 export_cafe_bar.py [-- <fbx folder> <blend path>]
Headless:  blender -b --python export_cafe_bar.py [-- <fbx folder> <blend path>]
Defaults: fbx/ and CafeBar_v1.blend beside this file (in the repo: Assets/Art/Models/CafeBar/ and
BlenderSource/CafeBar_v1.blend).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cafe_bar as cb  # noqa: E402  (puts Tools/Blender on the path)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(HERE, 'fbx')
blend_path = args[1] if len(args) > 1 else os.path.join(HERE, 'CafeBar_v1.blend')

fb.clear_scene()
results = cb.build_all()
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
    overlaps = cb.coplanar_overlaps(ob)
    if overlaps:
        problems += 1
        print('COPLANAR', ob.name, overlaps[:5])
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    pos = cb.PLACEMENT[ob.name]
    entry = {
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': False, 'tris': r['tris'],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # the lowest point (0 on the floor; negative: below the origin)
        'back': r['max'][1],                        # the back's y (0 for a wall piece); the front is -y in Blender
        'materials': r['materials'], 'islands': r['islands'],
        'placement': {'position': [round(v, 4) for v in pos], 'rotation': [0, 180, 0],
                      'note': 'Unity world; turned 180 about Y so the front faces the room (-z)'},
    }
    if ob.name == 'CK_BenchLamp':
        hc, lt = cb.LAMP['head_centre'], cb.LAMP['light']
        # a child of CK_BenchLamp (which stands turned 180 about Y): local position in the FBX's Unity frame, and a
        # local Euler of (90, 0, 0) that points the spot's +Z straight down (the parent's turn about Y keeps it down)
        entry['light'] = {
            'type': 'Spot', 'world_position': list(cb.to_world('CK_BenchLamp', lt)),
            'local_position': list(cb.to_unity_local(lt)), 'local_euler': [90, 0, 0], 'direction_world': [0, -1, 0],
            'colour': '#FFE4C2', 'intensity': 1.6, 'range': 1.5, 'spot_angle': 50, 'shadows': 'None',
            'head_centre_world': list(cb.to_world('CK_BenchLamp', hc)), 'head_centre_local': list(cb.to_unity_local(hc)),
        }
    pieces.append(entry)
    print('%-16s %5d tris  size %s  islands %d  bottom %.3f  placed at %s' % (ob.name, r['tris'], entry['size'], r['islands'], entry['bottom'], entry['placement']['position']))

materials = {}
for name, (hexstr, smooth, extra) in cb.MATERIALS.items():
    entry = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0}
    for k in ('texture', 'emission', 'transparent'):
        if k in extra:
            entry[k] = extra[k]
    if 'note' in extra:
        entry['note'] = extra['note']
    materials[name] = entry

# the .blend: the pieces assembled where they stand in the café (Blender X = Unity x, Y = Unity z, Z = Unity y).
# The FBX files went out from the origin, so this layout never moves a piece.
for r in results:
    ob = r['object']
    px, py, pz = cb.PLACEMENT[ob.name]
    ob.location = (px, pz, py)
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {
    'version': 1, 'kit': 'CafeBar',
    'frame': 'Each FBX: origin as listed per piece, front -Y in Blender = +Z in Unity local. Place with the position '
             'given, rotation (0, 180, 0): the front then faces the room (-z) and Blender X/Y read as world x/z.',
    'pieces': pieces, 'materials': materials,
}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_bar.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
