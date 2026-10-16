"""
Review renders of the bench stage (14 Oct 2026) from the close-up camera as the scene has it: 58 degrees down, 0.58 m from
the InspectPoint (the mat's centre), a 40 degree vertical lens, 1920 x 1080, in the rig's own frame (the rig is turned
180 degrees in the café; here the player simply sits at Blender +Y). On the mat: today's phone, camera and watch meshes
(hero_phone.py, hero_camera.py, hero_watch.py), scaled to real size (the camera x 1/2, the watch x 1/3, as Art's
per-device table assumes; the TD's one-scale method is still to come), each where Art's table puts it, with the tool rest,
the tools on their seats, the folded cloth and the old-parts tray. Light: the bench lamp's spot (#FFE4C2, 50 degrees,
0.39 m over the mat) and the scene's flat ambient (0.60, 0.56, 0.48): an approximation of URP, for judging colour and
layout, not a lab photo.

With bpy:  python3 Tools/Blender/review_bench_stage.py [-- <output folder>]
Headless:  blender -b --python Tools/Blender/review_bench_stage.py [-- <output folder>]
Default output: Tools/Blender/out/bench-stage/ (not kept in git). REVIEW_SAMPLES (default 40) sets Cycles' samples.

  bench-1-phone.png, bench-2-camera.png, bench-3-watch.png   the close-up per device
  bench-1..3-*-zones.png                                     the same with UI/UX's screen zones U1-U6 drawn over it
  bench-4-mat-alone.png                                      the mat with nothing on it
  bench-5-cloth-and-brush.png                                the cloth folded, held, pressed, bunched; the brush straight and bent
  bench-6-cups-detail.png                                    a 2x zoom on the cups and the pad, stand-in 2.4 mm screws in the cups
  bench-colour.txt                                           the mat's rendered colour (sRGB, L*a*b*) beside each device
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import bench_stage as bs  # noqa: E402
import bench_tools as bt  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if args else os.path.join(HERE, 'out', 'bench-stage')
os.makedirs(OUT, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '40'))
MAT_TOP = bs.T / 1000.0
CAM_PITCH, CAM_DIST, CAM_FOV = 58.0, 0.58, 40.0
ZONES = [('U1', 24, 20, 1408, 64), ('U2', 1596, 18, 1896, 110), ('U3', 1416, 132, 1896, 300), ('U4', 24, 864, 256, 1060),
         ('U5', 1496, 740, 1896, 1054), ('U6', 670, 966, 1250, 1054)]


def b_xy(x_mm, y_mm):
    """Art's mat frame (mm) to Blender (m)."""
    return (-x_mm / 1000.0, -y_mm / 1000.0)


def lights_and_world():
    sc = bpy.context.scene
    w = bpy.data.worlds.get('Bench') or bpy.data.worlds.new('Bench')
    sc.world = w
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    if bg is not None:
        bg.inputs['Color'].default_value = (*fb.rgb('#999079'), 1.0)        # the scene's flat ambient 0.60, 0.56, 0.48
        bg.inputs['Strength'].default_value = 1.0
    ld = bpy.data.lights.new('Bench lamp', 'SPOT')
    ld.energy = 8.0
    ld.color = fb.rgb('#FFE4C2')
    ld.spot_size = math.radians(50.0)
    ld.spot_blend = 0.35
    ld.shadow_soft_size = 0.03
    ob = bpy.data.objects.new('Bench lamp', ld)
    sc.collection.objects.link(ob)
    ob.location = (0.0, 0.0, MAT_TOP + 0.394)
    ob.rotation_euler = (0.0, 0.0, 0.0)                                       # a spot looks down -Z
    sd = bpy.data.lights.new('Room', 'SUN')                                   # the café's ceiling light, weak and soft
    sd.energy = 0.6
    sd.color = fb.rgb('#FFF1DE')
    sd.angle = math.radians(30.0)
    so = bpy.data.objects.new('Room', sd)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(25.0), 0.0, math.radians(160.0))


def close_up_camera(name='Close-up', dist=CAM_DIST, aim=(0.0, 0.0)):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new(name)
    cd.sensor_fit = 'VERTICAL'
    cd.angle = math.radians(CAM_FOV)
    cd.clip_start = 0.01
    ob = bpy.data.objects.new(name, cd)
    sc.collection.objects.link(ob)
    p = math.radians(CAM_PITCH)
    target = Vector((aim[0], aim[1], MAT_TOP))
    ob.location = target + Vector((0.0, dist * math.cos(p), dist * math.sin(p)))    # the player's side is +Y
    ob.rotation_euler = (target - ob.location).to_track_quat('-Z', 'Y').to_euler()
    return ob


def bench_top():
    p = fb.Prop('Bench top (review)', floor=False)
    p.add('top', fb.zbox(-0.55, 0.55, -0.40, 0.40, -0.04, 0.0), fb.material('CC_Wood_Counter', '#8F5B32', rough=0.29))
    return p.finish()


def stage(device):
    """The mat, the rest with the tools on their seats and the cloth, the scrap tray, for one device's layout."""
    mat, empties = bs.build_mat()
    bs.review_textures(REPO)
    d = bs.PER_DEVICE[device]
    results = bt.build_all()
    by = {r['object'].name: r for r in results}
    rest = by['BT_ToolRest']['object']
    rx = sum(d['tool_rest_x']) / 2
    rest.location = (*b_xy(rx, d['tool_rest_y']), MAT_TOP)
    tray = by['BT_ScrapTray']['object']
    tray.location = (*b_xy(*d['scrap_tray_centre']), MAT_TOP)
    bpy.context.view_layer.update()
    seat = {e.name[5:]: e for e in by['BT_ToolRest']['empties']}
    for name, key in (('BT_Screwdriver', 'Screwdriver'), ('BT_PryTool', 'Pry'), ('BT_Tweezers', 'Tweezers'),
                      ('BT_Brush', 'Brush'), ('BT_Cloth', 'Cloth')):
        ob = by[name]['object']
        ob.matrix_world = seat[key].matrix_world.copy()
    by['BT_ClothHeld']['object'].hide_render = True
    return mat, empties, by


def device_on_mat(device, empties):
    """Today's hero mesh, scaled to real size, resting at Art's rest centre; the fresh part on the pad (phone, watch)."""
    import hero_camera
    import hero_phone
    import hero_watch
    # wave 2: build() exports real size now (hero_lib.scale_build), so no scale here; review_devices_wave2.py supersedes this
    kit, scale, hide = {'phone': (hero_phone, 1.0, ('Screen', 'ScreenPlain')),
                        'camera': (hero_camera, 1.0, ('WorkingBlade', 'CleanMechanism')),
                        'watch': (hero_watch, 1.0, ('Mainspring',))}[device]
    root, children, _made = kit.build()
    root.scale = (scale, scale, scale)
    for c in children:
        if c.name.split('.')[0] in hide:
            c.hide_render = True
    bpy.context.view_layer.update()
    shown = [root] + [c for c in children if not c.hide_render]
    low = min((o.matrix_world @ v.co).z for o in shown for v in o.data.vertices)
    cx, cy = b_xy(*bs.PER_DEVICE[device]['rest_centre'])
    root.location = (cx, cy, root.location.z + (MAT_TOP - low) + 0.0002)
    bpy.context.view_layer.update()
    pad = {e.name: e for e in empties}
    fresh = {'phone': ('Screen', 'Pad_Large'), 'watch': ('Mainspring', 'Pad_Small')}.get(device)
    if fresh:
        part = next(c for c in children if c.name.split('.')[0] == fresh[0])
        mw = part.matrix_world.copy()
        part.parent = None
        part.matrix_world = mw
        part.hide_render = False
        bpy.context.view_layer.update()
        lo = min((part.matrix_world @ v.co).z for v in part.data.vertices)
        bb = [part.matrix_world @ v.co for v in part.data.vertices]
        mid = Vector(((max(p.x for p in bb) + min(p.x for p in bb)) / 2, (max(p.y for p in bb) + min(p.y for p in bb)) / 2, lo))
        target = pad[fresh[1]].matrix_world.translation
        part.location += Vector((target.x - mid.x, target.y - mid.y, target.z - lo + 0.0002))
    return root


def stand_in_screw(head=0.0024):
    """A pan-head Phillips screw at Art's A8 proportions (a stand-in for the review: the screws wait for the TD)."""
    p = hl.Faceted('Screw (review)')
    black, slot = fb.material('HD_ScrewBlack', '#2B2724', rough=0.55), fb.material('HD_ScrewSlot', '#14120F', rough=0.8)
    r, h = head / 2, head * 0.35
    p.add('head', hl.revolve([(0.0, r), (h * 0.7, r), (h, r * 0.75), (h, 0.0001)], sides=12), black)
    for rot in (0.0, 90.0):
        bar = fb.zbox(-head * 0.275, head * 0.275, -head * 0.06, head * 0.06, h - 0.0002, h + 0.00005)
        fb.turn(bar, rot, 'Z')
        p.add('recess', bar, slot)
    return p.finish()


def render(path, cam, res=(1920, 1080), samples=None):
    return fb.render(path, cam, res=res, samples=samples or SAMPLES)


def overlay_zones(src, dst):
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return None
    im = Image.open(src).convert('RGBA')
    over = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    for name, x0, y0, x1, y1 in ZONES:
        d.rectangle([x0, y0, x1, y1], fill=(255, 64, 160, 60), outline=(255, 64, 160, 230), width=3)
        d.text((x0 + 8, y0 + 6), name, fill=(255, 255, 255, 255))
    Image.alpha_composite(im, over).convert('RGB').save(dst)
    return dst


def lab(rgb):
    def lin(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(c) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def sample_mat(path, cam, world_xy):
    """The rendered mat's colour at a point of the mat (Art mm), a 9 x 9 px average."""
    try:
        from PIL import Image
        from bpy_extras.object_utils import world_to_camera_view
    except ImportError:
        return None
    sc = bpy.context.scene
    bx, by_ = b_xy(*world_xy)
    co = world_to_camera_view(sc, cam, Vector((bx, by_, MAT_TOP)))
    im = Image.open(path).convert('RGB')
    w, h = im.size
    px, py = int(co.x * w), int((1 - co.y) * h)
    pts = [im.getpixel((min(w - 1, max(0, px + i)), min(h - 1, max(0, py + j)))) for i in range(-4, 5) for j in range(-4, 5)]
    avg = tuple(sum(p[k] for p in pts) / len(pts) for k in range(3))
    return (px, py), tuple(round(c) for c in avg), tuple(round(v, 1) for v in lab(avg))


ONLY = [k for k in os.environ.get('REVIEW_ONLY', '').split(',') if k]     # e.g. camera,cloth (empty: all)


def wanted(key):
    return not ONLY or key in ONLY


colour_lines = []
for i, device in enumerate(('phone', 'camera', 'watch')):
    if not wanted(device):
        continue
    fb.clear_scene()
    lights_and_world()
    bench_top()
    mat, empties, by = stage(device)
    device_on_mat(device, empties)
    cam = close_up_camera()
    path = os.path.join(OUT, 'bench-%d-%s.png' % (i + 1, device))
    render(path, cam)
    overlay_zones(path, path.replace('.png', '-zones.png'))
    # a patch of bare mat beside the device: between the device and the cups, near the mat's centre line
    spot = {'phone': (15.0, -60.0), 'camera': (5.0, -60.0), 'watch': (15.0, -45.0)}[device]
    s = sample_mat(path, cam, spot)
    if s:
        colour_lines.append('%-7s mat at %s mm -> px %s: sRGB %s, L*a*b* %s' % (device, spot, s[0], s[1], s[2]))

# the mat alone
def mat_alone():
    fb.clear_scene()
    lights_and_world()
    bench_top()
    bs.build_mat()
    bs.review_textures(REPO)
    cam = close_up_camera()
    path = os.path.join(OUT, 'bench-4-mat-alone.png')
    render(path, cam)
    s = sample_mat(path, cam, (0.0, 0.0))
    if s:
        colour_lines.append('bare    mat at (0, 0) mm -> px %s: sRGB %s, L*a*b* %s' % (s[0], s[1], s[2]))


def cloth_and_brush():
    """The cloth's states and the brush's bend, on the bare mat, from a closer camera."""
    fb.clear_scene()
    lights_and_world()
    bench_top()
    bs.build_mat()
    bs.review_textures(REPO)
    results = bt.build_all()
    by = {r['object'].name: r for r in results}
    for r in results:
        r['object'].hide_render = True
        for c in r['children']:
            c.hide_render = True
    folded = by['BT_Cloth']['object']
    folded.hide_render = False
    folded.location = (*b_xy(-160.0, -85.0), MAT_TOP)
    folded.rotation_euler = (0.0, 0.0, math.radians(90.0))
    held = by['BT_ClothHeld']['object']
    for k, (name, press, bunch, x_mm) in enumerate((('held', 0, 0, -75.0), ('pressed', 1, 0, 0.0), ('bunched', 0, 1, 75.0))):
        ob = held if k == 0 else bpy.data.objects.new('BT_ClothHeld %s' % name, held.data.copy())
        if k:
            bpy.context.scene.collection.objects.link(ob)
        ob.hide_render = False
        keys = ob.data.shape_keys.key_blocks
        keys['Press'].value, keys['Bunch'].value = press, bunch
        ob.location = (*b_xy(x_mm, -85.0), MAT_TOP)
        ob.rotation_euler = (0.0, 0.0, 0.0)
    brush = by['BT_Brush']['object']
    for k, (bend, x_mm) in enumerate(((0.0, 135.0), (1.0, 175.0))):
        ob = brush if k == 0 else bpy.data.objects.new('BT_Brush bent', brush.data.copy())
        if k:
            bpy.context.scene.collection.objects.link(ob)
        ob.hide_render = False
        ob.data.shape_keys.key_blocks['Bend'].value = bend
        ob.location = (*b_xy(x_mm, 10.0), MAT_TOP)
        ob.rotation_euler = (0.0, 0.0, 0.0)
    cam = close_up_camera('Cloth', dist=0.40, aim=b_xy(10.0, -60.0))
    render(os.path.join(OUT, 'bench-5-cloth-and-brush.png'), cam, res=(1920, 1080))


def cups_detail():
    """A 2x zoom on the cups and the pad: stand-in 2.4 mm black-oxide screws in three cups."""
    fb.clear_scene()
    lights_and_world()
    bench_top()
    _mat, empties = bs.build_mat()
    bs.review_textures(REPO)
    bpy.context.view_layer.update()                  # the empties' world matrices, after parenting
    for e in empties:
        if e.name in ('Cup_1', 'Cup_2', 'Cup_5'):
            for k in range(1 if e.name != 'Cup_2' else 2):
                sc_ = stand_in_screw()
                sc_.location = e.matrix_world.translation + Vector((0.0015 * k, -0.002 * k, 0.0))
                sc_.rotation_euler = (0.0, 0.0, math.radians(20 + 35 * k))
    cam = close_up_camera('Cups', dist=0.29, aim=b_xy(100.0, 0.0))
    render(os.path.join(OUT, 'bench-6-cups-detail.png'), cam, res=(1920, 1080))


if wanted('mat'):
    mat_alone()
if wanted('cloth'):
    cloth_and_brush()
if wanted('cups'):
    cups_detail()
with open(os.path.join(OUT, 'bench-colour.txt'), 'a') as fh:
    fh.write('\n'.join(colour_lines) + '\n')
print('\n'.join(colour_lines))
print('renders in', OUT)
