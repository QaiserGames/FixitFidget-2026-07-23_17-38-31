"""
Build the café's signs (cafe_signs.py) and write one FBX per piece (for Unity), the .blend, and the manifest the Unity
step reads (cafe_signs.json, in the house kit's format: each piece's size and triangles as built; the kit's materials
with their colour and smoothness, a lettered material's texture by path, the transparent ones flagged, the picture
light's glow; and the six tiling materials with their colour and normal maps and their real tile size).

With bpy:   python3 export_cafe_signs.py [-- <fbx folder> <blend path>]
Headless:   blender -b --python export_cafe_signs.py [-- <fbx folder> <blend path>]
Defaults: fbx/ and CafeSigns_v1.blend beside this script (in the repo: Assets/Art/Models/Cafe/ and
BlenderSource/CafeSigns_v1.blend). Every piece is audited first: nothing floating more than 2 mm, no face without area.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import cafe_signs as cs  # noqa: E402
import fixit_blender as fb  # noqa: E402

IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
REPO = os.path.abspath(os.path.join(HERE, '..', '..')) if IN_REPO else None
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if args else (os.path.join(REPO, 'Assets', 'Art', 'Models', 'Cafe') if IN_REPO else os.path.join(HERE, 'fbx'))
blend_path = args[1] if len(args) > 1 else (os.path.join(REPO, 'BlenderSource', 'CafeSigns_v1.blend') if IN_REPO
                                            else os.path.join(HERE, 'CafeSigns_v1.blend'))

ORIGINS = {
    'CK_MenuBoard': 'bottom centre of the back (back at y = 0); the picture light reaches 0.245 forward and 0.140 above the frame',
    'CK_Fascia': 'bottom centre of the back (back at y = 0)',
    'CK_BladeSign': 'on the wall face (x = 0 in Blender), at the height of the disc\'s bottom, centred on the disc\'s faces; the sign stands out along Blender +X (Unity -X); disc centre 0.58 out, 0.40 up',
    'CK_WindowGilding_L': 'bottom centre of the back (back at y = 0: the face on the glass); the street face is the front',
    'CK_WindowGilding_R': 'bottom centre of the back (back at y = 0: the face on the glass); the street face is the front',
    'CK_EntryMosaic': 'bottom centre; reads from the door looking north with no turn in Unity',
    'CK_EnamelSign': 'the top: the beam\'s underside at z = 0 between the two screw eyes; it hangs down 0.43',
    'CK_StaffPlate': 'bottom centre of the back (back at y = 0)',
    'CK_NoticeBoard': 'bottom centre of the back (back at y = 0)',
    'CK_PhotoLedge': 'bottom centre of the back (back at y = 0); the photo frame stands at x -0.36 (Unity +0.36)',
    'CK_TentCard': 'bottom centre (the fold is 0.048 up)',
    'CK_AFrameFace': 'bottom centre of the back (back at y = 0)',
}

fb.clear_scene()
results = cs.build_all()
pieces, problems = [], 0
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
    lettered = [m for m in r['materials'] if cs.MATERIALS.get(m, (0, 0, None))[2]]
    pieces.append({
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': False, 'tris': r['tris'],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],
        'back': r['max'][1],
        'materials': r['materials'], 'islands': r['islands'],
        'lettered': lettered, 'lettered_faces': r['lettered_faces'],
        'origin': ORIGINS.get(ob.name, 'bottom centre'),
    })
    print('%-20s %5d tris  %s  islands %d  lettered faces %d  %s' % (ob.name, r['tris'], r['size'], r['islands'],
                                                                     r['lettered_faces'], 'FLOATING' if r['floating'] else 'ok'))
materials = {}
for name, (hexstr, smooth, tex) in cs.MATERIALS.items():
    entry = {'hex': hexstr.lstrip('#'), 'smoothness': smooth}
    if tex:
        entry['texture'] = cs.TEX + tex
    if name in cs.TRANSPARENT:
        entry['transparent'] = 1
    if name in cs.EMISSION:
        entry['emission'] = cs.EMISSION[name]
    materials[name] = entry
for name, (hexstr, smooth, tile) in cs.TILING.items():
    materials[name] = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'texture': cs.TEX + name + '_color.png',
                       'normal': cs.TEX + name + '_normal.png', 'tile_m': tile, 'tiling': round(1.0 / tile, 4)}
for name in ('CF_BagLabel', 'CF_DrawerLabels'):          # drawn here, worn by the props and the bar kits' meshes
    materials[name] = {'hex': 'F2EFE8' if name == 'CF_BagLabel' else 'EEE6D2', 'smoothness': 0.15,
                       'texture': cs.TEX + name + '.png'}
# lay the pieces out in the .blend so they can be looked at (the FBX files went out from the origin)
x = 0.0
for r in results:
    ob = r['object']
    w = r['size'][0]
    ob.location = (x - r['min'][0], 3.0, 0 if r['min'][2] >= 0 else -r['min'][2])
    x += w + 0.4
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'kit': 'cafe_signs', 'pieces': pieces, 'materials': materials}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_signs.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d with problems' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
