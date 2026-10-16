"""
Build the bench stage's silicone repair mat (bench_stage.py), audit it, and write its FBX (the mat with its ten empties
as children), the .blend, its two pictures (bench_stage_textures.py) and the manifest the Unity step reads
(bench_stage.json: the mat's size and triangles as built, its empties and where they are in Unity's axes, its materials
with colour, smoothness, metallic, texture and tiling, how it is placed, and Art's per-device layout as data).

With bpy:  python3 Tools/Blender/export_bench_stage.py [-- <fbx folder> <blend path> <texture folder>]
Headless:  blender -b --python Tools/Blender/export_bench_stage.py [-- <fbx folder> <blend path> <texture folder>]
Defaults (run from the repository's Tools/Blender): Assets/Art/Models/BenchStage/, BlenderSource/BenchStage_v1.blend,
Assets/Art/Textures/Bench/. The pictures need Pillow; under Blender's own Python, draw them first with
python3 Tools/Blender/bench_stage_textures.py.

The audit (any failure is counted in the last line's problems):
  * nothing floating (fixit_blender.audit: every island within 2 mm of another);
  * no degenerate faces; every edge of the mat's body shared by exactly two faces (a closed solid: no cracks where the
    top meets the cups, the well and the chamfer);
  * no two faces in one plane facing the same way (cafe_seating.coplanar_overlaps);
  * every empty where the layout puts it.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import bench_stage as bs  # noqa: E402
import bench_stage_textures as bst  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'BenchStage')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'BenchStage_v1.blend')
tex_dir = args[2] if len(args) > 2 else os.path.join(REPO, 'Assets', 'Art', 'Textures', 'Bench')

try:
    bst.main(tex_dir)
except ImportError:
    print('Pillow is not here: the pictures were not redrawn (run bench_stage_textures.py with python3)')

fb.clear_scene()
mat, empties = bs.build_mat()
problems = 0
n_islands, floating = fb.audit(mat)
if floating:
    problems += 1
    print('FLOATING', floating)
degenerate = sum(1 for poly in mat.data.polygons if poly.area < 1e-10)
if degenerate:
    problems += 1
    print('DEGENERATE', degenerate, 'face(s) with no area')
bm = bmesh.new()
bm.from_mesh(mat.data)
open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
bm.free()
if open_edges:
    problems += 1
    print('OPEN EDGES', open_edges, '(the solid has a crack)')
try:
    import cafe_seating as cs
    same, opposite, examples = cs.coplanar_overlaps(mat)
except Exception as exc:                       # pragma: no cover
    same, opposite, examples = 0, 0, [str(exc)]
if same:
    problems += 1
    print('TWO FACES IN ONE PLANE', same, examples[:3])
bpy.context.view_layer.update()
for e in empties:
    want = bs.to_b(*bs.EMPTIES[e.name])
    if sum((a - b) ** 2 for a, b in zip(e.matrix_world.translation, want)) ** .5 > 1e-6:
        problems += 1
        print('EMPTY MOVED', e.name)

st = fb.stats(mat)
path = os.path.join(fbx_dir, mat.name + '.fbx')
fb.export_fbx_tree(mat, path, object_types={'MESH', 'EMPTY'})
print('%-14s %5d tris  %s  islands %d  open edges %d  coplanar %d/%d  shells turned %s' % (
    mat.name, st['tris'], st['size'], n_islands, open_edges, same, opposite, mat.get('fixit_shells_turned')))


def unity(b):
    """Blender (x, y, z) -> Unity (-x, z, -y), rounded."""
    return [round(-b[0], 5), round(b[2], 5), round(-b[1], 5)]


children = []
for e in empties:
    x, y, z = bs.EMPTIES[e.name]
    children.append({'name': e.name, 'at': unity(bs.to_b(x, y, z)), 'mat_mm': [x, y, z], 'turned': False, 'empty': True})
per_device = {}
for dev, d in bs.PER_DEVICE.items():
    rx, ry = d['rest_centre']
    tx, ty = d['scrap_tray_centre']
    per_device[dev] = {
        'rest_centre_mm': [rx, ry], 'footprint_mm': list(d['footprint']), 'tool_rest_x_mm': list(d['tool_rest_x']),
        'tool_rest_y_mm': d['tool_rest_y'],
        'scrap_tray_centre_mm': [tx, ty], 'note': d['note'],
        'unity_local': {'rest_centre': [rx / 1000, round(bs.T / 1000, 4), ry / 1000],
                        'tool_rest_centre': [round(sum(d['tool_rest_x']) / 2000, 4), round(bs.T / 1000, 4), d['tool_rest_y'] / 1000],
                        'scrap_tray_centre': [tx / 1000, round(bs.T / 1000, 4), ty / 1000]},
    }
materials = {}
for name, (hexstr, smooth, tex, tiling, why) in bs.BM_MATERIALS.items():
    e = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0.0, 'why': why}
    if tex:
        e['texture'] = bs.TEX + tex
        e['tiling'] = tiling
        e['wrap'] = 'Repeat' if tiling != 1.0 else 'Clamp'
    materials[name] = e
sx, sy, sz = st['size']
manifest = {
    'version': 1, 'kit': 'bench_stage',
    'note': ("Built by Tools/Blender/bench_stage.py (Art's brief A1, specs/art-bench-and-cafe.md). THE FRAME: built in the "
             "InspectRig's own frame, not the house's front-faces--Y frame. Imported with no turn, its local +x is the "
             "player's right and +z is away from the player (Art's mat x, y), so it goes under the rig's stage with local "
             "rotation 0. Origin: the bottom centre (the mat's centre on the bench top). Unity (x, y, z) = Blender (-x, z, -y). "
             "The top is at y 0.005; the old slab's was 0.006: the InspectPoint goes 1 mm lower. Textured materials have "
             "a white base (the picture carries the colour); BM_Silicone's UVs are metres from the rulers' zero, so its "
             "tiling is 20 (a 50 mm tile). The empties are the auto-tray's places: Cup_1..6 on each cup's steel disc "
             "(1.8 mm up), Pad_* on the pad (5.3 mm), Well_* on the well's floor (2.0 mm); their names are a contract."),
    'pieces': [{
        'name': mat.name, 'what': 'silicone repair mat 600 x 360 x 5 mm: grid, two rulers, six screw cups with steel discs, '
                                  'a new-part pad, a cover well',
        'tris': st['tris'], 'meshes': 1, 'children': children,
        'size': [sx, sz, sy], 'bottom': st['min'][2], 'materials': st['materials'], 'islands': n_islands,
        'collider': {'centre': [0.0, round(bs.T / 2000, 4), 0.0], 'size': [bs.W / 1000, bs.T / 1000, bs.D / 1000],
                     'note': 'the slab; the cups and the well are recesses in it, so nothing may rely on physics to rest '
                             'in them (the auto-tray places pieces by script)'},
        'place': "a child of the InspectRig's 'Stage (v2)' at local (0, the bench top, -0.02) (today's mat: "
                 "Playtest3Session4Steps.cs:103, matZ -0.02), local rotation 0, scale 1; it replaces the 'Mat' slab "
                 "(switch the slab's renderer off, keep its collider while anything still falls)",
    }],
    'per_device': per_device,
    'materials': materials,
}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'bench_stage.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
print('1 piece, %d triangles; FBX in %s; blend %s; %d problem(s)' % (st['tris'], fbx_dir, blend_path, problems))
