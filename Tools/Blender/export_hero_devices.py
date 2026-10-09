"""
Build the hero devices (hero_phone.py, hero_watch.py, hero_camera.py) and write one FBX each to Assets/Art/Models/Devices/
(HD_Phone.fbx, HD_Watch.fbx, HD_Camera.fbx: the main piece as the root, the other pieces as its children where the prefab has them), the .blend, and
the manifest the Unity steps read (devices.json: each piece's size and triangles as built, its children and where
they are, which prefab objects each mesh goes into, and the HD_ materials with colour, smoothness and metallic).

With bpy:  python Tools/Blender/export_hero_devices.py [-- <fbx folder> <blend path>]
Headless:  blender -b --python Tools/Blender/export_hero_devices.py [-- <fbx folder> <blend path>]
Then in Unity: Fixit Fidget > Bench > Devices 1 - Import and check the hero devices, then Devices 2 - Fit them into their prefabs.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_camera  # noqa: E402
import hero_phone  # noqa: E402
import hero_watch  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'Devices')
blend_dir = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource')

KITS = [(hero_phone, 'HeroPhone_v3.blend', 'the hero phone: body, screen, back cover and a screw, assembled as the prefab has them'),
        (hero_watch, 'HeroWatch_v3.blend', 'the hero pocket watch: case, back plate, crown, a screw and the mainspring, assembled as the prefab has them'),
        (hero_camera, 'HeroCamera_v3.blend', "Grace's camera, hero: body, lens, glass, film path, shutter mechanism and blades, straps, assembled as the prefab has them")]

pieces = []
materials = {}
problems = 0
for kit, blend_name, what in KITS:
    fb.clear_scene()                     # each kit in its own scene and .blend (the pieces share names: Screw, Fresh)
    root, children, made = kit.build()
    bpy.context.view_layer.update()      # the children's matrices, for the bounds below
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
        for v in ob.data.vertices:
            w = (ob.matrix_local @ v.co) if ob.parent is not None else v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
        print('%-12s %5d tris  %s  -> %s' % (ob.name, st['tris'], st['size'], ', '.join(targets)))
    path = os.path.join(fbx_dir, root.name + '.fbx')
    fb.export_fbx_tree(root, path)
    # Unity's axes: x = Blender -x, y up = Blender z, z = Blender -y (hero_lib.to_unity)
    pieces.append({
        'name': root.name, 'tool': '', 'room': '', 'what': what,
        'tris': tris, 'meshes': 1 + len(children),
        'children': [{'name': c.name, 'at': [round(-c.location.x, 4), round(c.location.z, 4), round(-c.location.y, 4)],
                      'turned': any(abs(a) > 1e-6 for a in c.rotation_euler)} for c in children],
        'size': [round(hi.x - lo.x, 4), round(hi.z - lo.z, 4), round(hi.y - lo.y, 4)],
        'bottom': round(lo.z, 4),
        'materials': mats, 'islands': 0,
        'targets': {ob.name: targets for ob, targets in made},
    })
    for name, (hexstr, smooth, metal) in kit.HD_MATERIALS.items():
        materials[name] = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal}
    os.makedirs(blend_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, blend_name), compress=True)
    print('%s: %d triangles, %d meshes -> %s; %s' % (root.name, tris, 1 + len(children), path, blend_name))
with open(os.path.join(fbx_dir, 'devices.json'), 'w') as fh:
    json.dump({'version': 2, 'pieces': pieces, 'materials': materials}, fh, indent=1)
print('%d devices; %d problem(s)' % (len(pieces), problems))
