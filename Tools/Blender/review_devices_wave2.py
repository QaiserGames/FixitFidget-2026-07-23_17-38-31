"""
Review renders of the three hero devices at REAL size on the repair mat (14 Oct 2026, wave 2), from the Technical Director's
close-up framings (specs/td-one-scale.md §5.2-5.3): pitch 58 degrees, the layout view at FOV 40 and the zoom limit (the same
distance, the close aim, FOV 19 / 27 / 12.5), at 16:9 and 16:10 (the 16:10 frame is the 16:9 render's middle 1728 px: same
height, same vertical FOV), with UI/UX's zones U1-U6 drawn (§5.8; the TD's per-aspect anchors). On the mat: each device in
the pose its first steps work on (the phone and the watch back up, screws showing, one or two screws out in their cups
with the hole decal BT_ScrewHole.png where they were; the camera lens up), its fresh part on its pad, the tool rest with
the tools at its per-device depth, the scrap tray. Plus a close turntable of Grace's camera and its strap.

With bpy:  python3 Tools/Blender/review_devices_wave2.py [-- <output folder>]
REVIEW_ONLY=phone,camera,watch,turntable (default all); REVIEW_SAMPLES (default 20); REVIEW_PERCENT (default 100).
Writes <device>-layout-16x9.png and -zones.png, -layout-16x10-zones.png, -zoom-16x9-zones.png, -zoom-16x10-zones.png,
camera-turntable-*.png, camera-turntable-sheet.png and review-numbers.txt (fill, screw px, zone and frame checks).
Light: the bench lamp (#FFE4C2 spot, 0.39 m over the mat) and the scene's flat ambient: URP-ish, for layout and read.
"""
import json
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
from mathutils import Euler, Matrix, Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if args else os.path.join(HERE, 'out', 'devices-wave2')
os.makedirs(OUT, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '20'))
PERCENT = int(os.environ.get('REVIEW_PERCENT', '100'))
ONLY = [k for k in os.environ.get('REVIEW_ONLY', '').split(',') if k]
MAT_TOP = bs.T / 1000.0
PITCH = 58.0
HOLE_PNG = os.path.join(REPO, 'Assets', 'Data', 'Resources', 'Bench', 'BT_ScrewHole.png')

# the TD's framings (§5.2; final_framing.json): layout aim (mat mm), distance (m); close aim = rest + offset; FOV close
VIEWS = {
    'phone':  {'aim': (10.0, -14.0), 'd': 0.48, 'close': (21.0, -9.0), 'fov_close': 19.0},
    'camera': {'aim': (-58.0, -8.0), 'd': 0.49, 'close': (21.0, 6.0), 'fov_close': 27.0},
    'watch':  {'aim': (10.0, -20.0), 'd': 0.42, 'close': (9.0, -3.0), 'fov_close': 12.5},
}
# what each device's job needs in the default frame (the TD's NEEDS, Bench 9): cups, the pads, the wells, scrap, rest
NEEDS = {'phone': ('Cup_4', 'Cup_5', 'Cup_6', 'Pad_Large', 'Well_Large'), 'camera': ('Pad_Small',),
         'watch': ('Cup_1', 'Cup_2', 'Cup_3', 'Cup_4', 'Cup_5', 'Cup_6', 'Pad_Small', 'Well_Small')}
FOOT = {'Cup': (18.0, 18.0), 'Pad_Large': (80.0, 166.0), 'Pad_Small': (40.0, 40.0), 'Well_Large': (80.0, 166.0), 'Well_Small': (40.0, 40.0)}


def zones(aspect):
    """UI/UX's zones in canvas units (the TD's framing.zones), and the canvas size."""
    if aspect == '16:9':
        W, H = 1920.0, 1080.0
    else:
        W, H = 1821.4, 1138.4
    R = lambda x: W - (1920.0 - x)      # noqa: E731
    B = lambda y: H - (1080.0 - y)      # noqa: E731
    cx = W / 2
    return W, H, {'U1': (24, 20, W - 512, 64), 'U2': (R(1596), 18, R(1896), 110), 'U3': (R(1416), 132, R(1896), 300),
                  'U4': (24, B(864), 256, B(1060)), 'U5': (R(1496), B(740), R(1896), B(1054)), 'U6': (cx - 290, B(966), cx + 290, B(1054))}


def b_xy(x_mm, y_mm):
    return (-x_mm / 1000.0, -y_mm / 1000.0)


def lights_and_world():
    sc = bpy.context.scene
    w = bpy.data.worlds.get('Bench') or bpy.data.worlds.new('Bench')
    sc.world = w
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    bg.inputs['Color'].default_value = (*fb.rgb('#999079'), 1.0)
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
    sd = bpy.data.lights.new('Room', 'SUN')
    sd.energy = 0.6
    sd.color = fb.rgb('#FFF1DE')
    sd.angle = math.radians(30.0)
    so = bpy.data.objects.new('Room', sd)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(25.0), 0.0, math.radians(160.0))


def camera(aim_mm, d, fov):
    cd = bpy.data.cameras.new('Close-up')
    cd.sensor_fit = 'VERTICAL'
    cd.angle = math.radians(fov)
    cd.clip_start = 0.01
    ob = bpy.data.objects.new('Close-up', cd)
    bpy.context.scene.collection.objects.link(ob)
    p = math.radians(PITCH)
    target = Vector((*b_xy(*aim_mm), MAT_TOP))
    ob.location = target + Vector((0.0, d * math.cos(p), d * math.sin(p)))     # the player at Blender +Y
    ob.rotation_euler = (target - ob.location).to_track_quat('-Z', 'Y').to_euler()
    return ob


def bench_top():
    p = fb.Prop('Bench top (review)', floor=False)
    p.add('top', fb.zbox(-0.60, 0.60, -0.45, 0.45, -0.04, 0.0), fb.material('CC_Wood_Counter', '#8F5B32', rough=0.29))
    return p.finish()


def stage(device, tools=True):
    mat, empties = bs.build_mat()
    bs.review_textures(REPO)
    d = bs.PER_DEVICE[device]
    by = {}
    if tools:
        results = bt.build_all()
        by = {r['object'].name: r for r in results}
        rest = by['BT_ToolRest']['object']
        rest.location = (*b_xy(sum(d['tool_rest_x']) / 2, d['tool_rest_y']), MAT_TOP)
        tray = by['BT_ScrapTray']['object']
        tray.location = (*b_xy(*d['scrap_tray_centre']), MAT_TOP)
        bpy.context.view_layer.update()
        seat = {e.name[5:]: e for e in by['BT_ToolRest']['empties']}
        for name, key in (('BT_Screwdriver', 'Screwdriver'), ('BT_PryTool', 'Pry'), ('BT_Tweezers', 'Tweezers'),
                          ('BT_Brush', 'Brush'), ('BT_Cloth', 'Cloth')):
            by[name]['object'].matrix_world = seat[key].matrix_world.copy()
        by['BT_ClothHeld']['object'].hide_render = True
    bpy.context.view_layer.update()
    return mat, {e.name: e for e in empties}, by


def hole_material():
    m = bpy.data.materials.get('BT_ScrewHole (review)')
    if m:
        return m
    m = bpy.data.materials.new('BT_ScrewHole (review)')
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    img = nt.nodes.new('ShaderNodeTexImage')
    img.image = bpy.data.images.load(HOLE_PNG, check_existing=True)
    img.extension = 'CLIP'
    nt.links.new(img.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(img.outputs['Alpha'], bsdf.inputs['Alpha'])
    bsdf.inputs['Roughness'].default_value = 0.6
    return m


def hole_decal(parent, unity_xyz, head):
    """The hole a screw leaves (Art A8, BT_ScrewHole.png on a quad 1.15 x the head), lying on the surface the head sat on,
    0.05 mm proud, facing out (-Unity y), as a child of the device's root (its local frame is the FBX's)."""
    s = 1.15 * head / 2
    me = bpy.data.meshes.new('Hole decal')
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(3, 2, 1, 0)])     # facing -Z (out of the device)
    uv = me.uv_layers.new()
    for li, loop in enumerate(me.loops):
        co = me.vertices[loop.vertex_index].co
        uv.data[li].uv = ((co.x / s + 1) / 2, (co.y / s + 1) / 2)
    me.materials.append(hole_material())
    ob = bpy.data.objects.new('Hole decal', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    x, y, z = unity_xyz
    ob.location = hl.to_blender((x, y + 0.35 * head / 2 - 0.00005, z))
    return ob


def place_on_mat(root, shown, rest_mm, flip=False):
    """The device's root at the rest centre, turned face down if flip (about its long axis), its lowest point on the mat."""
    root.rotation_euler = (0.0, math.pi if flip else 0.0, 0.0)
    root.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    low = min((o.matrix_world @ v.co).z for o in shown for v in o.data.vertices)
    root.location = (*b_xy(*rest_mm), MAT_TOP - low + 0.0001)
    bpy.context.view_layer.update()


def to_pad(part, target_empty, lift=0.0002):
    """Lift a part off the assembly onto a mat empty: its footprint's middle over the empty, its underside on the pad."""
    mw = part.matrix_world.copy()
    part.parent = None
    part.matrix_world = mw
    part.hide_render = False
    bpy.context.view_layer.update()
    pts = [part.matrix_world @ v.co for v in part.data.vertices]
    lo = min(p.z for p in pts)
    mid = Vector(((max(p.x for p in pts) + min(p.x for p in pts)) / 2, (max(p.y for p in pts) + min(p.y for p in pts)) / 2, lo))
    t = target_empty.matrix_world.translation
    part.location += Vector((t.x - mid.x, t.y - mid.y, t.z - lo + lift))
    bpy.context.view_layer.update()


def screw_in_cup(screw_ob, cup, yaw):
    """A loose screw lying on its side on a cup's steel disc (Systems §4.0.1: axis +-30 degrees off radial)."""
    me = screw_ob.data.copy()
    ob = bpy.data.objects.new('Screw (in a cup)', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.rotation_euler = Euler((0.0, math.radians(90.0), math.radians(yaw)), 'XYZ')
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ v.co for v in me.vertices]
    lo = min(p.z for p in pts)
    mid = Vector(((max(p.x for p in pts) + min(p.x for p in pts)) / 2, (max(p.y for p in pts) + min(p.y for p in pts)) / 2, 0))
    t = cup.matrix_world.translation
    ob.location = Vector((t.x - mid.x, t.y - mid.y, t.z - lo + 0.0001))
    bpy.context.view_layer.update()
    return ob


def device(name, empties):
    import hero_camera
    import hero_phone
    import hero_watch
    kit = {'phone': hero_phone, 'camera': hero_camera, 'watch': hero_watch}[name]
    hl.PART_FACES.clear()
    root, children, _made = kit.build()
    by = {c.name: c for c in children}
    rest = bs.PER_DEVICE[name]['rest_centre']
    info = {'root': root, 'screws': [], 'holes': []}
    if name == 'phone':
        by['ScreenPlain'].hide_render = True
        fresh = by['Screen']
        screw = by['Screw']
        shown = [root] + [c for c in children if not c.hide_render and c.name != 'Screen']
        place_on_mat(root, shown, rest, flip=True)
        to_pad(fresh, empties['Pad_Large'])
        # Screw0 stays in; Screw1 is out (its hole shows) and lies in cup 4 (the phone's screws take cups 4-6)
        screw.location = hl.to_blender((0.022, -0.004565, -0.058))
        info['screws'].append(screw)
        info['holes'].append(hole_decal(root, (-0.022, -0.004565, -0.058), 0.0038))
        info['cup_screws'] = [screw_in_cup(screw, empties['Cup_4'], 25.0)]
    elif name == 'watch':
        fresh = by['Mainspring']
        screw = by['Screw']
        shown = [root] + [c for c in children if not c.hide_render and c.name != 'Mainspring']
        place_on_mat(root, shown, rest, flip=True)
        to_pad(fresh, empties['Pad_Small'])
        screw.location = hl.to_blender((0.010667, -0.006878, 0.010667))       # Screw0 in
        info['screws'].append(screw)
        twin = bpy.data.objects.new('Screw3', screw.data)                     # Screw3 in
        bpy.context.scene.collection.objects.link(twin)
        twin.parent = root
        twin.location = hl.to_blender((-0.010667, -0.006878, -0.010667))
        info['screws'].append(twin)
        for (x, z) in ((-0.010667, 0.010667), (0.010667, -0.010667)):          # Screw1 and Screw2 out: their holes, cups 1 and 2
            info['holes'].append(hole_decal(root, (x, -0.006878, z), 0.0038))
        info['cup_screws'] = [screw_in_cup(screw, empties['Cup_1'], 20.0), screw_in_cup(screw, empties['Cup_2'], -35.0)]
    else:
        by['WorkingBlade'].hide_render = True
        clean = by['CleanMechanism']
        shown = [root] + [c for c in children if not c.hide_render and c.name != 'CleanMechanism']
        place_on_mat(root, shown, rest, flip=False)
        to_pad(clean, empties['Pad_Small'])
    bpy.context.view_layer.update()
    fresh_names = ('Screen', 'Mainspring', 'CleanMechanism')
    info['shown'] = [o for o in [root] + children if not o.hide_render and o.type == 'MESH' and o.name not in fresh_names]
    info['fresh'] = [o for o in children if o.name in fresh_names and not o.hide_render]
    info['cups_out'] = info.get('cup_screws', [])
    return info


# ----------------------------------------------------------------------------- numbers

def px(cam, world, aspect):
    from bpy_extras.object_utils import world_to_camera_view
    v = world_to_camera_view(bpy.context.scene, cam, world)
    x, y = v.x * 1920.0, (1.0 - v.y) * 1080.0
    if aspect == '16:10':
        x -= 96.0
        return x / 0.948700, y / 0.948700            # back to canvas units (1821.4 x 1138.4)
    return x, y


def rect_of(cam, pts, aspect):
    xy = [px(cam, p, aspect) for p in pts]
    return (min(q[0] for q in xy), min(q[1] for q in xy), max(q[0] for q in xy), max(q[1] for q in xy))


def obj_pts(objs):
    out = []
    for o in objs:
        if o.type == 'MESH' and not o.hide_render:
            mw = o.matrix_world
            out += [mw @ v.co for v in o.data.vertices]
    return out


def overl(a, b):
    return not (a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1])


def check_view(name, cam, info, items, aspect, zoomed):
    """The device's fill, the screw head's and the bore's size in px, and every needed item inside the frame and clear of
    U1-U5 (the device of U1-U6)."""
    W, H, Z = zones(aspect)
    lines = []
    dev = rect_of(cam, obj_pts(info['shown']), aspect)
    lines.append('  device box (%.0f, %.0f)-(%.0f, %.0f): fill %.1f %% of the height' % (*dev, 100.0 * (dev[3] - dev[1]) / H))
    bad = []
    frame = (20, 20, W - 20, H - 20)
    for key, r in [('device', dev)] + items:
        inside = r[0] >= frame[0] and r[1] >= frame[1] and r[2] <= frame[2] and r[3] <= frame[3]
        hits = [z for z, zr in Z.items() if overl(r, zr) and (z != 'U6' or key == 'device')]
        if not inside and not zoomed:
            bad.append('%s outside the frame (%.0f, %.0f)-(%.0f, %.0f)' % (key, *r))
        if hits and (key == 'device' or not zoomed):
            bad.append('%s overlaps %s' % (key, ','.join(hits)))
    for s in info['screws']:
        pts = [s.matrix_world @ v.co for v in s.data.vertices]
        top = max(p.z for p in pts)
        head = [p for p in pts if p.z > top - 0.0014]
        r = rect_of(cam, head, aspect)
        lines.append('  screw head %s: %.1f x %.1f px' % (s.name, r[2] - r[0], r[3] - r[1]))
    for h in info['holes']:
        c = h.matrix_world.translation
        r = rect_of(cam, [c + Vector((-0.00091, 0, 0)), c + Vector((0.00091, 0, 0))], aspect)
        lines.append('  hole bore (0.48 x head = 1.82 mm) across: %.1f px' % (r[2] - r[0]))
    lines.append('  ' + ('ALL CLEAR' if not bad else 'PROBLEMS: ' + '; '.join(bad)))
    return lines


def item_rects(name, cam, empties, by, aspect):
    out = []
    for key in NEEDS[name]:
        e = empties[key]
        fx, fy = FOOT['Cup' if key.startswith('Cup') else key]
        c = e.matrix_world.translation
        pts = [c + Vector((sx * fx / 2000.0, sy * fy / 2000.0, dz)) for sx in (-1, 1) for sy in (-1, 1) for dz in (0.0, 0.002)]
        out.append((key, rect_of(cam, pts, aspect)))
    rest = [by[k]['object'] for k in ('BT_ToolRest', 'BT_Screwdriver', 'BT_PryTool', 'BT_Tweezers', 'BT_Brush', 'BT_Cloth')]
    out.append(('tool rest + tools', rect_of(cam, obj_pts(rest), aspect)))
    out.append(('scrap tray', rect_of(cam, obj_pts([by['BT_ScrapTray']['object']]), aspect)))
    return out


def fresh_rects(cam, info, aspect):
    out = [('fresh part ' + o.name, rect_of(cam, obj_pts([o]), aspect)) for o in info['fresh']]
    out += [('screw in its cup', rect_of(cam, obj_pts([o]), aspect)) for o in info['cups_out']]
    return out


def draw_zones(src, dst, aspect):
    from PIL import Image, ImageDraw
    im = Image.open(src).convert('RGBA')
    if aspect == '16:10':
        w = im.width
        cut = round(96 * w / 1920.0)
        im = im.crop((cut, 0, w - cut, im.height))
    k = im.height / (1080.0 if aspect == '16:9' else 1138.4)
    over = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    _W, _H, Z = zones(aspect)
    for zname, (x0, y0, x1, y1) in Z.items():
        d.rectangle([x0 * k, y0 * k, x1 * k, y1 * k], fill=(255, 64, 160, 55), outline=(255, 64, 160, 230), width=3)
        d.text((x0 * k + 8, y0 * k + 6), zname, fill=(255, 255, 255, 255))
    d.text((12, im.height - 22), aspect, fill=(255, 255, 255, 255))
    Image.alpha_composite(im, over).convert('RGB').save(dst)
    return dst


def render(path, cam, res=(1920, 1080)):
    sc = bpy.context.scene
    sc.render.resolution_percentage = PERCENT
    fb.render(path, cam, res=res, samples=SAMPLES)
    return path


def wanted(k):
    return not ONLY or k in ONLY


numbers = []
for name in ('phone', 'camera', 'watch'):
    if not wanted(name):
        continue
    fb.clear_scene()
    lights_and_world()
    bench_top()
    _mat, empties, by = stage(name)
    info = device(name, empties)
    v = VIEWS[name]
    rest = bs.PER_DEVICE[name]['rest_centre']
    close_aim = (rest[0] + v['close'][0], rest[1] + v['close'][1])
    for view, aim, fov in (('layout', v['aim'], 40.0), ('zoom', close_aim, v['fov_close'])):
        cam = camera(aim, v['d'], fov)
        base = os.path.join(OUT, '%s-%s-16x9.png' % (name, view))
        render(base, cam)
        for aspect in ('16:9', '16:10'):
            tag = aspect.replace(':', 'x')
            draw_zones(base, os.path.join(OUT, '%s-%s-%s-zones.png' % (name, view, tag)), aspect)
            numbers.append('%s %s %s (aim %s mm, %.2f m, FOV %.1f):' % (name, view, aspect, tuple(round(a, 1) for a in aim), v['d'], fov))
            numbers += check_view(name, cam, info, item_rects(name, cam, empties, by, aspect) + fresh_rects(cam, info, aspect), aspect, view == 'zoom')
        if view == 'zoom':
            os.remove(base) if os.path.exists(base) else None
    print('\n'.join(numbers[-12:]))


def turntable():
    """Grace's camera alone on the mat, four turns round it, close: the strap's run from each lug, the loop, the body's ends."""
    fb.clear_scene()
    lights_and_world()
    bench_top()
    _mat, empties, _by = stage('camera', tools=False)
    import hero_camera
    root, children, _made = hero_camera.build()
    for c in children:
        if c.name in ('WorkingBlade', 'CleanMechanism'):
            c.hide_render = True
    shown = [root] + [c for c in children if not c.hide_render]
    place_on_mat(root, shown, (0.0, 30.0))
    centre = Vector((*b_xy(0.0, 35.0), MAT_TOP + 0.012))
    paths = []
    for k, az in enumerate((20.0, 110.0, 200.0, 290.0)):
        cd = bpy.data.cameras.new('Turn')
        cd.sensor_fit = 'VERTICAL'
        cd.angle = math.radians(32.0)
        cam = bpy.data.objects.new('Turn', cd)
        bpy.context.scene.collection.objects.link(cam)
        el, dist = math.radians(38.0), 0.46
        a = math.radians(az)
        cam.location = centre + Vector((math.sin(a) * math.cos(el), math.cos(a) * math.cos(el), math.sin(el))) * dist
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        path = os.path.join(OUT, 'camera-turntable-%d-%03d.png' % (k + 1, int(az)))
        render(path, cam, res=(1200, 800))
        paths.append(path)
    try:
        from PIL import Image
        ims = [Image.open(p) for p in paths]
        w, h = ims[0].size
        sheet = Image.new('RGB', (2 * w, 2 * h))
        for i, im in enumerate(ims):
            sheet.paste(im, ((i % 2) * w, (i // 2) * h))
        sheet.save(os.path.join(OUT, 'camera-turntable-sheet.png'))
    except ImportError:
        pass


if wanted('turntable'):
    turntable()
with open(os.path.join(OUT, 'review-numbers.txt'), 'a') as fh:
    fh.write('\n'.join(numbers) + '\n')
print('renders in', OUT)
