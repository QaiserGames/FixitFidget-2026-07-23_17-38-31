"""
Build the hero phone (hero_phone.py) and write Assets/Art/Models/Devices/HD_Phone.fbx (one model: the body as the root,
the screen, the back cover and a screw as its children, assembled as the prefab assembles them), the .blend, and the
manifest the Unity steps read (devices.json: the piece's size and triangles as built, its children and where they are,
the HD_ materials with colour, smoothness and metallic).

With bpy:  python Tools/Blender/export_hero_phone.py [-- <fbx folder> <blend path>]
Headless:  blender -b --python Tools/Blender/export_hero_phone.py [-- <fbx folder> <blend path>]
Then in Unity: Fixit Fidget > Bench > Devices 1 - Import and check the hero phone, then Devices 2 - Fit it into PhoneRepair.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_phone as hp  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'Devices')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'HeroPhone_v1.blend')

fb.clear_scene()
root, children, made = hp.build()
problems = 0
tris = 0
mats = []
lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for ob, targets in made:
    n, floating = fb.audit(ob)
    if floating:
        problems += 1
        print('FLOATING', ob.name, floating)
    degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
    if degenerate:
        problems += 1
        print('DEGENERATE', ob.name, degenerate)
    st = fb.stats(ob)
    tris += st['tris']
    mats += [m for m in st['materials'] if m not in mats]
    off = Vector(ob.location) if ob.parent is not None else Vector((0, 0, 0))
    for v in ob.data.vertices:
        w = v.co + off
        lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
        hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    print('%-10s %5d tris  %s  -> %s' % (ob.name, st['tris'], st['size'], ', '.join(targets)))
path = os.path.join(fbx_dir, root.name + '.fbx')
fb.export_fbx_tree(root, path)
# Unity's axes: x across, y up (Blender z), z deep (Blender -y)
piece = {
    'name': root.name, 'tool': '', 'room': '', 'what': 'the hero phone: body, screen, back cover and a screw, assembled as the prefab has them',
    'tris': tris, 'meshes': 1 + len(children),
    'children': [{'name': c.name, 'at': [round(c.location.x, 4), round(c.location.z, 4), round(-c.location.y, 4)]} for c in children],
    'size': [round(hi.x - lo.x, 4), round(hi.z - lo.z, 4), round(hi.y - lo.y, 4)],
    'bottom': round(lo.z, 4),
    'materials': mats, 'islands': 0,
    'targets': {ob.name: targets for ob, targets in made},
}
materials = {name: {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal} for name, (hexstr, smooth, metal) in hp.HD_MATERIALS.items()}
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
with open(os.path.join(fbx_dir, 'devices.json'), 'w') as fh:
    json.dump({'version': 2, 'pieces': [piece], 'materials': materials}, fh, indent=1)
print('%s: %d triangles, %d meshes; FBX %s; blend %s; %d problem(s)' % (root.name, tris, 1 + len(children), path, blend_path, problems))
