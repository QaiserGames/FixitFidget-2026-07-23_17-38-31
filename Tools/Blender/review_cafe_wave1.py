"""
Review renders of the café's wave-1 dressing (14 Oct 2026) from the real overhead camera: CmShopCam at Unity (0, 23.79,
-15.87), 43 degrees down, yaw 0, a 44 degree vertical lens, 34 m from (0, 0.6, 9) (Art's B1, read from the scene; not the
role files' stale "50 degrees, 20 m"), on a neutral stand-in of the room: a floor and three walls, the bar pieces built by
cafe_bar.py (front counter, repair bench, back bar, shelf, backsplash, parts wall, bench lamp), the menu board
(cafe_signs.py), the bean bags and the grinder (cafe_props.py), each where cafe_bar.json / cafe_props.json place it (the
menu board's place is a guess: x -0.5, bottom 1.95). Before and after the eleven new pieces, a 2x crop of the bar, a
10-pixel check of every new piece through the camera (Art's rule: >= 12 px in the smaller size at 1080p), and a turntable
of the pieces (four turns of the line-up).

With bpy:  python3 Tools/Blender/review_cafe_wave1.py [-- <output folder>]
Default output: Tools/Blender/out/cafe-wave1/. REVIEW_SAMPLES (default 32). CAFE_REVIEW=check,after,before,turntable picks
the parts (default all; "check" alone projects without rendering).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import cafe_bar as cb  # noqa: E402
import cafe_props as cp  # noqa: E402
import cafe_signs as csg  # noqa: E402
import export_cafe_wave1 as ex  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if args else os.path.join(HERE, 'out', 'cafe-wave1')
os.makedirs(OUT, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '32'))
PARTS = [k for k in os.environ.get('CAFE_REVIEW', 'check,after,before,turntable').split(',') if k]
TEX_ROOT = os.path.join(REPO, 'Assets', 'Art', 'Textures', 'Cafe')

BAR = {'CK_FrontCounter': ((-1.265, 0.0, 13.77), 180), 'CK_RepairBench': ((-4.2, 0.0, 14.055), 180), 'CK_BackBar': ((-1.25, 0.0, 18.0), 180),
       'CK_BackBarShelf': ((0.55, 1.75, 18.0), 180), 'CK_Backsplash': ((0.55, 1.0, 18.0), 180), 'CK_PartsWall': ((-5.6, 1.0, 18.0), 180),
       'CK_BenchLamp': ((-5.3, 0.85, 14.41), 180)}


def place(ob, unity_pos, rot_y):
    """A piece placed in Unity's world (position, rotation about Y) as it lands in this Blender world: (-x, -z, y), -rot."""
    x, y, z = unity_pos
    ob.location = (-x, -z, y)
    ob.rotation_euler = (0.0, 0.0, math.radians(-rot_y))


def room():
    p = fb.Prop('Room (stand-in)', floor=False)
    floor_m = fb.material('Stand-in floor (oak)', '#B08E66', rough=0.7)
    wall_m = fb.material('Stand-in wall', '#EDE8DE', rough=0.9)
    p.add('floor', fb.zbox(-7.6, 7.3, -18.0, 0.0, -0.02, 0.0), floor_m)          # Unity x -7.3..7.6, z 0..18
    p.add('back wall', fb.zbox(-7.6, 7.3, -18.2, -18.0, 0.0, 3.2), wall_m)
    p.add('west wall', fb.zbox(7.3, 7.5, -18.2, 0.0, 0.0, 3.2), wall_m)
    p.add('east wall', fb.zbox(-7.8, -7.6, -18.2, 0.0, 0.0, 3.2), wall_m)
    return p.finish()


def lights():
    sc = bpy.context.scene
    w = bpy.data.worlds.get('Cafe') or bpy.data.worlds.new('Cafe')
    sc.world = w
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (*fb.rgb('#999079'), 1.0)
    bg.inputs['Strength'].default_value = 0.9
    ld = bpy.data.lights.new('Sun', 'SUN')
    ld.energy = 2.2
    ld.color = fb.rgb('#FFF1DE')
    ld.angle = math.radians(20)
    ob = bpy.data.objects.new('Sun', ld)
    sc.collection.objects.link(ob)
    ob.rotation_euler = (math.radians(35), 0.0, math.radians(200))


def overhead():
    cd = bpy.data.cameras.new('CmShopCam')
    cd.sensor_fit = 'VERTICAL'
    cd.angle = math.radians(44.0)
    cd.clip_end = 200.0
    ob = bpy.data.objects.new('CmShopCam', cd)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = (0.0, 15.87, 23.79)
    ob.rotation_euler = (Vector((0.0, -9.0, 0.6)) - ob.location).to_track_quat('-Z', 'Y').to_euler()
    return ob


def wire_textures():
    for name, (_h, _s, tex, _w) in ex.rs.NEW.items():
        m = bpy.data.materials.get(name)
        path = os.path.join(TEX_ROOT, tex) if tex else None
        if m is None or not path or not os.path.exists(path):
            continue
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        img = nt.nodes.new('ShaderNodeTexImage')
        img.image = bpy.data.images.load(path, check_existing=True)
        img.extension = 'EXTEND'
        nt.links.new(img.outputs['Color'], bsdf.inputs['Base Color'])


def px_box(cam, ob, res=(1920, 1080)):
    from bpy_extras.object_utils import world_to_camera_view
    sc = bpy.context.scene
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    vs = [world_to_camera_view(sc, cam, p) for p in pts]
    xs = [v.x * res[0] for v in vs]
    ys = [(1 - v.y) * res[1] for v in vs]
    return min(xs), min(ys), max(xs), max(ys)


fb.clear_scene()
lights()
room()
for r in cb.build_all():
    if r['object'].name in BAR:
        pos, rot = BAR[r['object'].name]
        place(r['object'], pos, rot)
    else:
        r['object'].hide_render = True
menu = csg.menu_board().finish()
place(menu, (-0.5, 1.95, 18.0), 180)
for k in range(8):
    bag = cp.bean_bag().finish()
    place(bag, (-3.20 + 0.135 * k, 1.81, 17.85), 180)
grinder = cp.grinder().finish()
place(grinder, (4.25, 1.00, 17.50), 180)
cam = overhead()
if 'before' in PARTS:
    fb.render(os.path.join(OUT, 'overhead-1-before.png'), cam, res=(1920, 1080), samples=SAMPLES)

results = ex.build_all()
wire_textures()
lines = ["Art's 10-pixel check (the smaller of the piece's projected width and height, 1080p; pass >= 12 px; R3 and S1-S2 exempt)"]
for r in results:
    ob = r['object']
    pos = r['place']['position']
    rot = r['place']['rotation'][1]
    place(ob, pos, rot)
bpy.context.view_layer.update()
for r in results:
    ob = r['object']
    x0, y0, x1, y1 = px_box(cam, ob)
    small = min(x1 - x0, y1 - y0)
    exempt = ob.name in ('LI_DonorParts', 'LI_MortgageLetter', 'LI_FinalNotice', 'LI_GlassNotes')
    lines.append('%-18s px box (%4.0f, %4.0f)-(%4.0f, %4.0f): %3.0f x %3.0f px, smaller %3.0f  %s' % (
        ob.name, x0, y0, x1, y1, x1 - x0, y1 - y0, small, 'exempt' if exempt else ('PASS' if small >= 12 else 'FAIL')))
if 'after' in PARTS:
    fb.render(os.path.join(OUT, 'overhead-2-after.png'), cam, res=(1920, 1080), samples=SAMPLES)
with open(os.path.join(OUT, 'overhead-10px-check.txt'), 'w') as fh:
    fh.write('\n'.join(lines) + '\n')
print('\n'.join(lines))
try:
    from PIL import Image
    for tag in ('before', 'after'):
        src = os.path.join(OUT, 'overhead-%d-%s.png' % (1 if tag == 'before' else 2, tag))
        if os.path.exists(src):
            im = Image.open(src)
            im.crop((560, 240, 1360, 480)).resize((1600, 480), Image.LANCZOS).save(os.path.join(OUT, 'overhead-3-bar-x2-%s.png' % tag))
except ImportError:
    pass
if 'turntable' not in PARTS:
    print('renders in', OUT)
    sys.exit(0)

# the turntable: the line-up of the new pieces, four turns, from a 3/4 view
fb.clear_scene()
lights()
p = fb.Prop('Turntable floor', floor=False)
p.add('floor', fb.zbox(-9.0, 9.0, -2.0, 2.0, -0.02, 0.0), fb.material('Stand-in grey', '#B9B3A8', rough=0.8))
p.finish()
results = ex.build_all()
wire_textures()
x = -7.4
for r in results:
    ob = r['object']
    w = r['size'][0]
    ob.location = (x + w / 2 - (r['min'][0] + r['max'][0]) / 2, 0.0, -r['min'][2] if ob.name not in ('LI_Whiteboard', 'LI_FinalNotice', 'LI_GlassNotes') else 0.3)
    x += max(w, 0.35) + 0.25
bpy.context.view_layer.update()
for k, turn in enumerate((0, 90, 180, 270)):
    for r in results:
        r['object'].rotation_euler = (0.0, 0.0, math.radians(turn))
    cd = bpy.data.cameras.new('Turn %d' % turn)
    cd.type = 'ORTHO'
    cd.ortho_scale = (x + 7.4) * 1.04
    cam = bpy.data.objects.new('Turn %d' % turn, cd)
    bpy.context.scene.collection.objects.link(cam)
    mid = (x - 0.25 - 7.4) / 2
    cam.location = (mid, -9.0, 5.6)
    cam.rotation_euler = (Vector((mid, 0.0, 0.25)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    fb.render(os.path.join(OUT, 'turntable-%d-%03d.png' % (k + 1, turn)), cam, res=(2400, 700), samples=max(16, SAMPLES // 2))
print('renders in', OUT)
