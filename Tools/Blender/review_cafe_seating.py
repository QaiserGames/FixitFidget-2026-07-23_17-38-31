"""
Review renders of the cafe's seats (render only; nothing exported). Builds the kit, brings in the old library pieces
for the before/after sheets, lays the four seat zones out where scene-facts.md's seat plan puts them, and shoots them
from the two game cameras' points of view: the overhead (from the south, 50 degrees down, a 44 degree field) and first
person (an eye at 1.65 m), plus the banquette's channels from 0.6 m.

  python3 review_cafe_seating.py -- <out folder> [shot,shot,...]

Modest on purpose (a shared 2-CPU machine): Cycles CPU, 900 x 600, 14 samples, denoised.
"""
import math
import os
import shutil
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cafe_seating as cs  # noqa: E402  (puts the studio's Tools/Blender on the path)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from mathutils import Vector  # noqa: E402

LIB = '/home/claude/cafe/inputs/models/'
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if args else os.path.join(HERE, 'renders')
ONLY = set(args[1].split(',')) if len(args) > 1 else None
RES, SAMPLES = (900, 600), 14
os.makedirs(OUT, exist_ok=True)


def u2b(x, y, z):
    """A Unity position in Blender (Unity (x, y, z) = Blender (-x, z, -y))."""
    return (-x, -z, y)


def place(ob_or_data, name, unity_pos, unity_yaw=0.0):
    """An instance of a built piece at a Unity position and yaw (Unity yaw +a = Blender Z rotation -a)."""
    data = ob_or_data.data if hasattr(ob_or_data, 'data') else ob_or_data
    ob = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = u2b(*unity_pos)
    ob.rotation_euler = (0, 0, math.radians(-unity_yaw))
    return ob


def import_old(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=LIB + name + '.fbx')
    new = [o for o in set(bpy.data.objects) - before if o.type == 'MESH']
    ob = new[0]
    # the FBX brings its own copies of the cafe's materials ("CC_Wood_Counter.001"): use the kit's, by name
    for i, m in enumerate(ob.data.materials):
        base = m.name.split('.')[0]
        if base in cs.CK_MATERIALS:
            ob.data.materials[i] = cs.M(base)
    return ob


def label(text, at, size=0.07, rot_z=0.0):
    bm = hl.text_mesh(text, size, 0.002)
    if bm is None:
        return
    me = bpy.data.meshes.new('label ' + text)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(fb.material('Review ink', '#3A3430', rough=0.8))
    ob = bpy.data.objects.new('label ' + text, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = at
    ob.rotation_euler = (0, 0, math.radians(rot_z))


def box(name, x0, x1, y0, y1, z0, z1, mat):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), mat)
    return p.finish()


def ubox(name, ux0, ux1, uy0, uy1, uz0, uz1, mat):
    """A box from Unity bounds."""
    bx0, bx1 = sorted((-ux0, -ux1))
    by0, by1 = sorted((-uz0, -uz1))
    return box(name, bx0, bx1, by0, by1, uy0, uy1, mat)


def cam_vertical(name, loc, target, fov_v):
    cam = fb.camera(name, loc, target)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(fov_v)
    return cam


def overhead(name, unity_target, dist, fov=44.0, pitch=50.0):
    """The overhead camera's view: from the south (Unity -z), pitched down 50 degrees, at dist from the target."""
    tx, ty, tz = unity_target
    loc = (tx, ty + dist * math.sin(math.radians(pitch)), tz - dist * math.cos(math.radians(pitch)))
    return cam_vertical(name, u2b(*loc), u2b(*unity_target), fov)


def eye(name, unity_eye, unity_target, fov=50.0):
    return cam_vertical(name, u2b(*unity_eye), u2b(*unity_target), fov)


def want(shot):
    return ONLY is None or shot in ONLY


def shoot(shot, cam, res=RES, samples=SAMPLES):
    if want(shot):
        fb.render(os.path.join(OUT, shot + '.png'), cam, res=res, samples=samples)
        print('rendered', shot)


# ----------------------------------------------------------------------------- build the kit

fb.clear_scene()
built = {r['object'].name: r['object'] for r in cs.build_all()}
store = bpy.data.collections.new('Built (hidden)')
bpy.context.scene.collection.children.link(store)
for ob in built.values():
    bpy.context.scene.collection.objects.unlink(ob)
    store.objects.link(ob)
store.hide_render = True
fb.studio(strength=1.0)
bpy.context.scene.view_settings.exposure = 0.0

# ----------------------------------------------------------------------------- 1. the library, old beside new

SHEETS = [
    ('old-new-chairs', [('Chair_Cafe', 'CK_Chair_Cafe'), ('Chair_Spindle', 'CK_Chair_Spindle')]),
    ('old-new-stools', [('Stool_Bar', 'CK_Stool_Bar'), ('Stool_Low', 'CK_Stool_Low')]),
    ('old-new-tables', [('Table_BistroRound', 'CK_Table_BistroRound'), ('Table_Square2', 'CK_Table_Square2')]),
]
sheet_x = 40.0                                     # the sheets stand away from the room, each on its own floor
for si, (shot, pairs) in enumerate(SHEETS):
    base = Vector((sheet_x + si * 12.0, 0.0, 0.0))
    box('sheet floor ' + shot, base.x - 3, base.x + 3, -3, 3, -0.02, 0.0, fb.material('Review floor', '#CDB797', rough=0.55))
    for pi, (old, new) in enumerate(pairs):
        x0 = base.x - 1.05 + pi * 2.1
        o = import_old(old)
        o.location = (x0 - 0.42, 0, 0)
        place(built[new], new + ' (review)', (-(x0 + 0.42), 0, 0))
        label('old', (x0 - 0.42, -0.55, 0.0005))
        label('new', (x0 + 0.42, -0.55, 0.0005))
    if want(shot) or want(shot + '-back') or want(shot + '-close'):
        tgt = (base.x, 0, 0.55 if 'tables' not in shot else 0.42)
        shoot(shot, fb.camera('cam ' + shot, fb.look_from(tgt, 4.6, 16, 18), tgt, lens=38))
        shoot(shot + '-back', fb.camera('cam back ' + shot, fb.look_from(tgt, 4.6, 24, 155), tgt, lens=38))
        # the new ones close, from a seated person's side and a little above (where detail is judged)
        tgt2 = (base.x + 0.0, 0, 0.5 if 'tables' not in shot else 0.45)
        shoot(shot + '-close', fb.camera('cam close ' + shot, fb.look_from(tgt2, 2.6, 34, 28), tgt2, lens=45))

# ----------------------------------------------------------------------------- 2. the room and the zones (Unity coordinates)

floor_mat = fb.material('Review floor oak', '#CDB797', rough=0.55)
wall_mat = fb.material('Review chalk white', '#E6E2D8', rough=0.9)
post_mat = fb.material('Review window frame', '#3E5A4C', rough=0.6)
ubox('floor', -7.40, 7.40, -0.02, 0.0, 0.0, 18.0, floor_mat)
ubox('west knee wall', -7.64, -7.40, 0.0, 0.78, 0.0, 12.7, wall_mat)
ubox('east knee wall', 7.40, 7.64, 0.0, 0.78, 0.0, 16.45, wall_mat)
ubox('east skirting', 7.32, 7.40, 0.0, 0.26, 0.0, 16.45, wall_mat)
for z in (0.0, 4.2, 8.45, 12.7):
    ubox('west window post', -7.61, -7.43, 0.78, 3.2, z - 0.09, z + 0.09, post_mat)
for z in (0.0, 4.2, 8.45, 12.7, 16.45):
    ubox('east window post', 7.43, 7.61, 0.78, 3.2, z - 0.09, z + 0.09, post_mat)
ubox('west sill line', -7.62, -7.40, 0.76, 0.78, 0.0, 12.7, wall_mat)

# the banquette: three bays on the west wall, an end panel each end, a 2-top and a cafe chair per bay
BAYS = (4.69, 6.79, 8.89)
for i, z in enumerate(BAYS):
    place(built['CK_BanquetteBay'], 'bay %d' % i, (-7.40, 0, z), 90)
    place(built['CK_Table_Square2'], 'bay table %d' % i, (-6.30, 0, z), 0)
    place(built['CK_Chair_Cafe'], 'bay chair %d' % i, (-5.60, 0, z), -90)
for z in (3.62, 9.96):
    place(built['CK_BanquetteEnd'], 'banquette end', (-7.40, 0, z), 90)
# the communal table and eight low stools (0.80 m from its centre line: see the report)
place(built['CK_CommunalTable'], 'communal', (-2.95, 0, 6.20), 0)
for z in (4.85, 5.75, 6.65, 7.55):
    place(built['CK_Stool_Low'], 'low stool W', (-3.75, 0, z), 0)
    place(built['CK_Stool_Low'], 'low stool E', (-2.15, 0, z), 180)
# the window counter and three bar stools
place(built['CK_WindowCounter'], 'window counter', (7.40, 0, 7.20), -90)
for z in (6.00, 7.20, 8.40):
    place(built['CK_Stool_Bar'], 'window stool', (6.50, 0, z), 90)
# the bistro 2-tops, two spindle chairs each
for z in (4.6, 7.8):
    place(built['CK_Table_BistroRound'], 'bistro', (3.0, 0, z), 0)
    place(built['CK_Chair_Spindle'], 'bistro chair W', (2.38, 0, z), 90)
    place(built['CK_Chair_Spindle'], 'bistro chair E', (3.62, 0, z), -90)

# the whole room as the overhead camera frames it (about 20 m away)
shoot('room-overhead', overhead('cam room', (0.0, 0.0, 8.0), 20.0))
# each zone, from the overhead's angle, closer
shoot('zone-banquette-overhead', overhead('cam zb', (-6.4, 0.3, 6.8), 7.5))
shoot('zone-communal-overhead', overhead('cam zc', (-2.95, 0.3, 6.2), 6.5))
shoot('zone-window-overhead', overhead('cam zw', (6.8, 0.4, 7.2), 6.0))
shoot('zone-bistro-overhead', overhead('cam zbi', (3.0, 0.3, 6.2), 6.0))
# first person, the eye at 1.65 m
shoot('zone-banquette-eye', eye('cam eb', (-3.6, 1.65, 3.2), (-6.6, 0.55, 6.6)))
shoot('zone-communal-eye', eye('cam ec', (-0.6, 1.65, 3.4), (-2.95, 0.5, 6.4)))
shoot('zone-window-eye', eye('cam ew', (4.2, 1.65, 4.6), (6.9, 0.8, 7.6)))
shoot('zone-bistro-eye', eye('cam ebi', (0.6, 1.65, 2.6), (3.0, 0.55, 6.0)))
# the banquette's back from 0.6 m: the channels, the seams, the welts
shoot('banquette-channels-close', eye('cam ch', (-6.74, 0.80, 6.45), (-7.30, 0.58, 6.76), fov=40.0))

# ----------------------------------------------------------------------------- 3. details from 0.5-0.9 m (contact sheets)

DETAIL_X = 100.0
box('detail floor', DETAIL_X - 2, DETAIL_X + 24, -3, 3, -0.02, 0.0, fb.material('Review floor', '#CDB797', rough=0.55))
box('detail knee wall', DETAIL_X + 16, DETAIL_X + 22, 0.0, 0.24, 0.0, 0.78, wall_mat)
spots = {'CK_Chair_Spindle': 0.0, 'CK_Chair_Cafe': 2.0, 'CK_Stool_Bar': 4.0, 'CK_Stool_Low': 6.0, 'CK_Table_Square2': 8.0,
         'CK_Table_BistroRound': 10.5, 'CK_CommunalTable': 13.0, 'CK_WindowCounter': 19.0}
for name, dx in spots.items():
    ob = bpy.data.objects.new(name + ' (detail)', built[name].data)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = (DETAIL_X + dx, 0, 0)


def tile(name, piece, target, dist, el, az, lens=40):
    x0 = DETAIL_X + spots[piece]
    t = (x0 + target[0], target[1], target[2])
    return name, fb.camera('cam ' + name, fb.look_from(t, dist, el, az), t, lens=lens)


DETAIL_SHEETS = {
    'details-seats': [
        tile('windsor seat: saddle, wedged tenons', 'CK_Chair_Spindle', (-0.04, -0.10, 0.44), 0.62, 40, 28),
        tile('cafe chair: apron, pegs, staggered stretchers', 'CK_Chair_Cafe', (0.12, -0.17, 0.33), 0.62, 14, 38),
        tile('bar stool: foot ring 0.44 under the seat', 'CK_Stool_Bar', (0.0, -0.04, 0.52), 0.85, 18, 32),
        tile('low stool: wedged tenons, dished seat', 'CK_Stool_Low', (-0.0115, 0.0, 0.43), 0.58, 52, 22),
    ],
    'details-tables': [
        tile('square 2-top: keyed through-tenon', 'CK_Table_Square2', (0.30, 0.0, 0.16), 0.55, 16, 62),
        tile('square 2-top: breadboard end, pins, planks', 'CK_Table_Square2', (0.27, -0.22, 0.75), 0.62, 46, 24),
        tile('bistro: spider, column, dome base', 'CK_Table_BistroRound', (0.0, 0.0, 0.36), 1.05, 6, 25),
        tile('communal: end trestle, keyed beam', 'CK_CommunalTable', (0.0, 0.95, 0.32), 0.95, 14, 152),
    ],
}
TILE = (450, 300)
tiles_dir = tempfile.mkdtemp(prefix='cafe-seating-tiles-')       # intermediates, never in the deliverable folder
for sheet, tiles in DETAIL_SHEETS.items():
    if not want(sheet):
        continue
    os.makedirs(tiles_dir, exist_ok=True)
    paths = []
    for i, (name, cam) in enumerate(tiles):
        path = os.path.join(tiles_dir, '%s-%d.png' % (sheet, i))
        fb.render(path, cam, res=TILE, samples=SAMPLES)
        paths.append((name, path))
    from PIL import Image, ImageDraw
    img = Image.new('RGB', (TILE[0] * 2, TILE[1] * 2), (40, 36, 32))
    for i, (name, path) in enumerate(paths):
        t = Image.open(path).convert('RGB')
        img.paste(t, ((i % 2) * TILE[0], (i // 2) * TILE[1]))
        d = ImageDraw.Draw(img)
        x, y = (i % 2) * TILE[0] + 8, (i // 2) * TILE[1] + 6
        d.rectangle([x - 4, y - 2, x + 7 * len(name) + 4, y + 13], fill=(40, 36, 32))
        d.text((x, y), name, fill=(242, 239, 232))
    img.save(os.path.join(OUT, sheet + '.png'))
    print('rendered', sheet)
shutil.rmtree(tiles_dir, ignore_errors=True)
shoot('detail-window-bracket', fb.camera('cam bracket', fb.look_from((DETAIL_X + 19.6, -0.20, 0.72), 1.0, 4, 68),
                                         (DETAIL_X + 19.6, -0.20, 0.72), lens=35))
print('done')
