"""
Build the intake tray (cafe_intake.py) and write its FBX (for Unity), the .blend, and the manifest the Unity step reads
(cafe_intake.json, the house kit's format: each piece's size in Unity axes (across, up, deep), bottom, back, triangles,
materials and islands as built, plus its origin, where it goes, its collider and the tray's own numbers: the felt's top,
the rims' top, the felt bed, the footprint, the open side and the device envelope it was sized to; the kit's materials
with their colours and smoothness: the CF_ one to be made, the existing one named for reference).

With bpy:  python3 export_cafe_intake.py [-- <fbx folder> <blend path>]
Defaults:  ../../Assets/Art/Models/CafeKit and ../../BlenderSource/CafeIntake_v1.blend from this script (the repo's
           layout: Tools/Blender/ beside Assets/ and BlenderSource/).
Prints "0 problem(s)" when the piece has no floating part, no face without area and nothing under its bottom.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_LIB', '')):
    if _p and _p not in sys.path:
        sys.path.insert(0, _p)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import cafe_intake as ci  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'CafeKit')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'CafeIntake_v1.blend')

fb.clear_scene()
results = ci.build_all()
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
    if r['min'][2] < -0.0005:
        problems += 1
        print('BELOW ITS BOTTOM', ob.name, r['min'][2])
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    pieces.append({
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': False, 'tris': r['tris'],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # 0: it stands on its origin
        'back': r['max'][1],                        # the back's y in Blender (the front is -y): the open side
        'materials': r['materials'], 'islands': r['islands'],
        'origin': r['origin'], 'place': r['place'], 'collider': r['collider'],
        'felt_top': r['felt_top'], 'rim_top': r['rim_top'],
        'felt_bed': r['felt_bed'], 'footprint': r['footprint'], 'open_side': r['open_side'],
        'device_envelope': ci.ENVELOPE,
        'references': ci.REFERENCES,
    })
    print('%-16s %5d tris  size (Blender x, y, z) %s  min %s  max %s  islands %d' % (
        ob.name, r['tris'], r['size'], r['min'], r['max'], r['islands']))

materials = {}
for name, (hexstr, smooth, why) in ci.CF_MATERIALS.items():
    materials[name] = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0.0, 'why': why}
existing = {name: {'hex': h.lstrip('#'), 'smoothness': s, 'metallic': 0.0, 'use': why}
            for name, (h, s, why) in ci.EXISTING.items()}

os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {
    'version': 1, 'kit': 'cafe_intake',
    'note': 'Unity (x, y, z) = Blender (-x, z, -y); the walled front faces Unity +Z (the queue) as built, the open side '
            'Unity -Z (Ace). Placed by Fixit Fidget > Playtest 4 > Tray > 1 (Assets/Editor/Playtest4TraySteps.cs).',
    'remaps': {'CC_Wood_Counter': 'Cafe grain - CC_Wood_Counter (Assets/Playtests/AcesCafeLayout/Cafe palette.asset)',
               'CF_Felt': 'Assets/Art/Materials/Cafe/CF_Felt.mat (made by the step from this manifest)'},
    'pieces': pieces, 'materials': materials, 'existing_materials': existing,
}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_intake.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d piece(s), %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
