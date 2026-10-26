"""
Build the bench's tools and the stage's steel (bench_tools.py: the five tools; v4 of 14 Oct 2026 adds the held cloth with
its Press and Bunch keys, the brush's Bend key, the thinner folded cloth, the tool rest with its seats and the old-parts
tray) and write one FBX per piece (for Unity), the .blend, and the manifest the Unity step reads (bench_tools.json: each
piece's size and triangles as built, how many meshes it is, its children and empties and where they are, its shape keys,
and the BT_ materials with their colours, smoothness and metallic).

Headless:  blender -b --python Tools/Blender/export_bench_tools.py [-- <fbx folder> <blend path>]
With bpy:  python Tools/Blender/export_bench_tools.py [-- <fbx folder> <blend path>]
Defaults: Assets/Art/Models/BenchTools/ and BlenderSource/BenchTools_v4.blend in this repository.
Then in Unity: Fixit Fidget > Bench > Bench tools 1 - Import and check the tools (the models only). NOTE (14 Oct): that
step sets importBlendShapes = false for every FBX it configures (BlenderKitSteps.Configure); BT_ClothHeld and BT_Brush
need it true, or the keys are dropped on import.

The audit (any failure is counted in the last line's problems): nothing floating (every island within 2 mm of another),
no degenerate faces, and every tool on the rest touching both its bars (the rest's fit, within 0.3 mm).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import bench_tools as bt  # noqa: E402
from mathutils import Matrix  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'BenchTools')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'BenchTools_v4.blend')

C = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))     # Blender -> Unity: (x, y, z) -> (-x, z, -y)


def unity_pos(v):
    return [round(-v[0], 5), round(v[2], 5), round(-v[1], 5)]


def unity_rot(euler):
    """A Blender rotation as Unity sees it after the FBX: R_u = C R_b C^T; quaternion (x, y, z, w) and Unity's Euler (ZXY)."""
    r = C @ euler.to_matrix() @ C.transposed()
    q = r.to_quaternion()
    e = r.to_euler('ZXY')
    return {'quaternion_xyzw': [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)],
            'euler': [round(math.degrees(a) % 360.0, 2) for a in (e.x, e.y, e.z)]}


EXTRA = {   # what each new piece is for, where it goes, and how it rests (the manifest's words; the Unity steps read them)
    'BT_Cloth': {
        'rests': "folded on the rest's cork at Seat_Cloth, its free corner lifted 6 mm toward the player (the grab point)"},
    'BT_ClothHeld': {
        'place': "a second model under Tool_Cloth (the ToolPickup), shown while the cloth is in the hand: the folded BT_Cloth is "
                 "hidden and BT_ClothHeld shown at the cursor's contact point (the swap is hidden by the pick-up motion)",
        # 26 Oct 2026 (art audit a02): the folded cloth grabbed, no longer the cone with a tail
        'rests': "its underside on the glass (origin, y 0): the folded cloth, pinched up toward Unity -Z (the player) into a "
                 "peak 25 mm high, the near corner lifted",
        'stroke_axis_unity': [1, 0, 0],
        'blend_shapes': {'Press': "0..1 from the press: the cloth spreads 8 %, its peak sinks 15 %, the corner lies lower",
                         'Bunch': "0..1 from the stroke's speed (Audio's reference px/s): the leading side (+X) rolls up 3.5 mm "
                                  "off the glass, the trailing side (-X) drags 8 mm, the peak leans 6 mm toward -X"}},
    'BT_Brush': {
        'stroke_axis_unity': [1, 0, 0],
        'blend_shapes': {'Bend': "0..1 from the stroke's speed and the press: the bristle block bends from the ferrule's mouth, "
                                 "its end 25 degrees off and 7.4 mm toward -X (it trails a stroke along +X); the tips stay "
                                 "on y 0 (the origin contract holds bent) and splay 2 mm"}},
    'BT_ToolRest': {
        'place': "under the InspectRig's stage, local rotation 0 (built in the rig's frame: +X the player's right, +Z away), "
                 "its bottom on the mat's top; centre x per device (bench_stage.json per_device tool_rest_x), z 0 on the "
                 "mat's centre line. Replaces the 'Tool caddy' block. Each tool's slot takes its Seat_* pose (local to the rest)",
        'rests': "flat on the mat; the tools lie on its two bars (each U notch fitted to its tool: both bars touched, 0.0 mm)"},
    'BT_ScrapTray': {
        'place': "under the InspectRig's stage, local rotation 0, on the mat's top at the device's scrap_tray_centre "
                 "(bench_stage.json per_device); the old-parts bin the auto-tray flies broken parts into",
        'rests': "flat on the mat; its floor's top is 0.8 mm up (where a broken part lands); Slot_1..3 are Systems' scrapSlots"},
}

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
    for e in r['empties']:
        gaps = list(e.get('gaps_mm', []))
        if gaps and (abs(gaps[0]) > .3 or abs(gaps[1]) > .3):
            problems += 1
            print('A TOOL DOES NOT REST ON BOTH BARS', e.name, gaps)
    path = os.path.join(fbx_dir, ob.name + '.fbx')
    if r['children'] or r['empties']:
        fb.export_fbx_tree(ob, path, object_types={'MESH', 'EMPTY'} if r['empties'] else {'MESH'})
    else:
        fb.export_fbx(ob, path)
    sx, sy, sz = r['size']
    entry = {
        'name': ob.name, 'tool': r['tool'], 'what': r['what'], 'tris': r['tris'],
        'meshes': 1 + len(r['children']),
        'children': [{'name': c.name, 'at': [round(-c.location.x, 4), round(c.location.z, 4), round(-c.location.y, 4)]} for c in r['children']]
                    + [dict({'name': e.name, 'at': unity_pos(e.location), 'turned': True, 'empty': True}, **unity_rot(e.rotation_euler))
                       for e in r['empties']],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # 0: the tip (a tool) or the bottom centre (the rest, the tray)
        'materials': r['materials'], 'islands': r['islands'],
    }
    if r['keys']:
        entry['blend_shapes_order'] = r['keys']
    entry.update(EXTRA.get(ob.name, {}))
    if ob.name == 'BT_ToolRest':
        entry['notch_bottoms_mm'] = list(ob.get('notch_bottoms_mm', []))
        entry['seat_gaps_mm'] = {e.name: list(e.get('gaps_mm', [])) for e in r['empties'] if e.get('gaps_mm') is not None}
    pieces.append(entry)
    print('%-15s %5d tris  %s  %d mesh(es)  %s%s' % (ob.name, r['tris'], r['size'], 1 + len(r['children']),
                                                    ('keys ' + ', '.join(r['keys'])) if r['keys'] else '',
                                                    ('  seats ' + ', '.join(e.name for e in r['empties'])) if r['empties'] else ''))
materials = {name: {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal} for name, (hexstr, smooth, metal) in bt.BT_MATERIALS.items()}
existing = {name: {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal, 'use': 'by name, from Assets/Art/Materials'}
            for name, (hexstr, smooth, metal) in bt.EXISTING.items()}
# lay the pieces out in the .blend so they can be looked at, on a line (the FBX files went out from the origin)
x = 0.0
for r in results:
    r['object'].location = (x, 0.0, 0.0)
    x += r['size'][0] + .06
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 3, 'pieces': pieces, 'materials': materials, 'existing_materials': existing,
            'note': "Unity (x, y, z) = Blender (-x, z, -y). A tool's origin is its working tip. The rest and the tray are built "
                    "in the bench rig's frame (no turn under the stage). Empties: children with no mesh, named; their 'at' is "
                    "the local position and 'quaternion_xyzw' / 'euler' the local rotation as Unity will import them. Shape "
                    "keys: import with importBlendShapes = true (Bench tools 1 sets it false today)."}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'bench_tools.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
