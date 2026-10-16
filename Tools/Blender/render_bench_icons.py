"""
The checklist card's tool icons (14 Oct 2026; UI/UX's brief §5.3 in specs/uiux-checklist-v2.md, Art Direction's A9 in
specs/art-bench-and-cafe.md): the eight generic icons, rendered from the same scripts that build the bench.

  screwdriver, pry, tweezers, brush   bench_tools.py's tools, lying diagonally, the working tip to the lower left, filling
                                      80 % of the diagonal, the flat side toward the viewer
  cloth                               the folded lens cloth (its rest pose, BT_Cloth) from 35 degrees above, its lifted
                                      corner toward the viewer
  screw                               a pan-head Phillips screw at real proportions (Art's A8: head 0.35 of its diameter,
                                      one chamfer, 12 sides, the recess 0.55 x 0.15 of the head in the icon only), three
                                      quarters from above so the cross reads at 36 px
  newpart                             a clean steel plate with one bright edge, on a corner of the mat's pale new-part pad
                                      with its cream corner mark (so the icon matches the world)
  cover                               a plain rounded back plate with four screw holes

  wave 2 (14 Oct 2026), the per-device set (UI/UX §5.3; contract §5: `part_*` / `cover_*` before the generic ones):
  part_phone                          the new screen as the player sees it on the pad: the glass in the prefab's
                                      M_FreshGlass blue (#8BA4D5) with its orange peel tab, from 42 degrees above
  part_watch                          the fresh mainspring: the blued coil round its brass arbor, from 55 degrees above
  part_camera                         Art's A7 clean shutter mechanism (hero_camera's 'clean' state), three quarters
  cover_phone                         the phone's back cover, outer face up: sage glass, the camera island, the leaf
  cover_watch                         the watch's back plate, its dome up: gold, the engraved ring

Each: Cycles, orthographic, 1024 x 1024 with a transparent background, flat-shaded in the models' own colours under one
warm key light from the upper left and a soft fill (no ground, no shadow on the ground), then a #1F1B17 outline 32 px
wide (8 px at 256) round the silhouette, reduced to 256 x 256 (sRGB PNG, premultiplied while reducing so no fringe).
Output: Assets/Data/Resources/UI/BenchIcons/<key>.png. Import (UI/UX): Sprite (2D and UI), mipmaps on, bilinear, max 256,
no compression. And one contact sheet, every icon at 36 and 56 px on the card's cream #F7F3EE, for his eye.

With bpy:  python3 Tools/Blender/render_bench_icons.py [-- <icon folder> <contact sheet path>]
Headless:  blender -b --python Tools/Blender/render_bench_icons.py [...] (the outline and the sheet need numpy, scipy and
           Pillow: run it with the bpy module, or give Blender's Python those packages)
Defaults: Assets/Data/Resources/UI/BenchIcons/ and Tools/Blender/out/bench-icons-contact-sheet.png.
ICON_SAMPLES (default 48) sets Cycles' samples.
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
import bench_tools as bt  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if args else os.path.join(REPO, 'Assets', 'Data', 'Resources', 'UI', 'BenchIcons')
SHEET = args[1] if len(args) > 1 else os.path.join(HERE, 'out', 'bench-icons-contact-sheet.png')
SAMPLES = int(os.environ.get('ICON_SAMPLES', '48'))
RES = 1024
FINAL = 256
OUTLINE_PX = 32                  # at 1024: 8 px at 256, so >= 1 px at 36 px
OUTLINE = (0x1F, 0x1B, 0x17)
CREAM = (0xF7, 0xF3, 0xEE)
KEYS = ['screwdriver', 'pry', 'tweezers', 'brush', 'cloth', 'screw', 'newpart', 'cover',
        'part_phone', 'part_watch', 'part_camera', 'cover_phone', 'cover_watch']


def M(name, hexstr, smooth):
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth))


def lights():
    sc = bpy.context.scene
    w = bpy.data.worlds.get('Icon') or bpy.data.worlds.new('Icon')
    sc.world = w
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    bg.inputs['Color'].default_value = (*fb.rgb('#F2EEE6'), 1.0)
    bg.inputs['Strength'].default_value = 0.55
    ld = bpy.data.lights.new('Key', 'SUN')
    ld.energy = 3.0
    ld.color = fb.rgb('#FFE9CC')
    ld.angle = math.radians(4.0)
    ob = bpy.data.objects.new('Key', ld)
    sc.collection.objects.link(ob)
    return ob


def aim_key(key_ob, cam_ob):
    """The key light from the upper left of the picture, a little in front."""
    r = cam_ob.matrix_world.to_3x3()
    right, up, back = r.col[0], r.col[1], r.col[2]
    d = (-right * 0.55 + up * 0.65 + back * 0.55).normalized()        # the direction the light comes from
    key_ob.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()


def ortho_camera(view_dir, up_hint=(0, 0, 1)):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new('Icon')
    cd.type = 'ORTHO'
    ob = bpy.data.objects.new('Icon', cd)
    sc.collection.objects.link(ob)
    d = Vector(view_dir).normalized()
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return ob


def frame(cam, objs, fill=0.78, diagonal=False):
    """Centre the orthographic camera on the objects' projected bounds; fill that share of the frame (or the diagonal)."""
    bpy.context.view_layer.update()
    r = cam.matrix_world.to_3x3()
    right, up, back = r.col[0], r.col[1], r.col[2]
    pts = []
    for o in objs:
        if o.type != 'MESH' or o.hide_render:
            continue
        mw = o.matrix_world
        pts += [mw @ v.co for v in o.data.vertices]
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    zs = [p.dot(back) for p in pts]
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    if diagonal:
        length = math.hypot(w, h)
        scale = length / (fill * math.sqrt(2.0))
    else:
        scale = max(w, h) / fill
    cam.data.ortho_scale = scale
    cam.location = right * cx + up * cy + back * (max(zs) + 1.0)
    cam.data.clip_start, cam.data.clip_end = 0.001, 10.0


def render(path, cam):
    sc = bpy.context.scene
    sc.camera = cam
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def outline_and_reduce(src, dst):
    """The silhouette's outline (an anti-aliased band OUTLINE_PX wide round the alpha), the render over it, reduced."""
    import numpy as np
    from PIL import Image
    from scipy import ndimage
    im = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32) / 255.0
    a = im[..., 3]
    inside = a > 0.5
    dist = ndimage.distance_transform_edt(~inside)
    ring = np.clip(OUTLINE_PX + 0.5 - dist, 0.0, 1.0)
    out = np.zeros_like(im)
    out[..., 0], out[..., 1], out[..., 2] = (c / 255.0 for c in OUTLINE)
    out[..., 3] = ring
    # the render over the outline (straight alpha "over")
    fa = a[..., None]
    rgb = im[..., :3] * fa + out[..., :3] * out[..., 3:4] * (1 - fa)
    alpha = fa + out[..., 3:4] * (1 - fa)
    rgb = np.where(alpha > 1e-6, rgb / np.maximum(alpha, 1e-6), 0.0)
    big = Image.fromarray((np.concatenate([rgb, alpha], axis=2) * 255 + 0.5).clip(0, 255).astype(np.uint8), 'RGBA')
    small = big.convert('RGBa').resize((FINAL, FINAL), Image.LANCZOS).convert('RGBA')
    small.save(dst)
    return dst


# ----------------------------------------------------------------------------- the icon models

def tool_icon(builder):
    built = builder()
    if isinstance(built, tuple):
        root, kids = built
    else:
        root, kids = built.finish(), []
    # the tip to the lower left: the handle (+Z) turned to the upper right in the picture plane (XZ, seen from -Y),
    # and a small roll about its own axis so it reads as a solid, not a cut-out
    root.rotation_euler = Euler((0.0, math.radians(45.0), 0.0), 'XYZ')
    root.rotation_euler.rotate(Euler((0.0, 0.0, math.radians(-12.0))))
    for leaf in kids:                    # the tweezers: their real 4 mm opening is one line at 36 px; open the V for the icon only
        leaf.rotation_euler = (0.0, math.radians(3.2 if leaf.name.startswith('Leaf_R') else -3.2), 0.0)
    cam = ortho_camera((0.0, 1.0, -0.18))
    return [root] + kids, cam, True


def cloth_icon():
    ob = bt.cloth().finish()
    ob.rotation_euler = (0.0, 0.0, math.radians(-25.0))
    el = math.radians(35.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))      # 35 degrees above, from the front (-Y), where the corner lifts
    return [ob], cam, False


def screw_icon():
    p = hl.Faceted('Icon screw')
    steel, recess = M('Icon steel', '#B9B5AD', 0.5), M('Icon recess', '#1A1816', 0.2)
    d = 1.0
    r, h = d / 2, 0.35 * d
    head = hl.revolve([(0.0, r), (h * 0.62, r), (h, r * 0.80), (h, 0.0001)], sides=12)
    p.add('head', head, steel)
    shank_r = 0.55 * d / 2
    prof = [(-1.25 * d, shank_r * 0.55), (-1.15 * d, shank_r)]
    for k in range(3):                                       # three ring grooves for the thread
        z = -1.0 * d + k * 0.28 * d
        prof += [(z - 0.06, shank_r), (z, shank_r * 0.82), (z + 0.06, shank_r)]
    prof += [(0.02, shank_r)]
    p.add('shank', hl.revolve(prof, sides=12), steel)
    for rot in (0.0, 90.0):                                  # the Phillips cross, opened to 0.15 x the head for the icon
        bar = fb.zbox(-0.55 * d / 2, 0.55 * d / 2, -0.075 * d, 0.075 * d, h - 0.10, h + 0.004)
        fb.turn(bar, rot + 20.0, 'Z')
        p.add('recess', bar, recess)
    ob = p.finish()
    el = math.radians(52.0)
    cam = ortho_camera((0.35, math.cos(el), -math.sin(el) * 1.05))
    return [ob], cam, False


def newpart_icon():
    pad_m, ink, plate, bright, hole = (M('Icon pad', '#B4C3D6', 0.3), M('Icon ink', '#F2EFE8', 0.3), M('Icon plate', '#C9C5BC', 0.5),
                                       M('Icon bright', '#F4F1EA', 0.7), M('Icon hole', '#3A3632', 0.2))
    p = hl.Faceted('Icon new part')
    p.add('pad corner', hl.chamfered_slab(1.6, 1.6, 0.10, 0.12, 0.03, centre_z=-0.05), pad_m)
    for (x0, x1, y0, y1) in ((-0.62, -0.10, -0.62, -0.54), (-0.62, -0.54, -0.62, -0.10)):
        p.add('corner mark', fb.zbox(x0, x1, y0, y1, -0.02, 0.012), ink)
    body = hl.chamfered_slab(0.90, 0.58, 0.07, 0.06, 0.018, centre_z=0.035 + 0.003)
    fb.turn(body, 14.0, 'Z')
    hl.moved(body, 0.12, 0.10, 0.0)
    p.add('plate', body, plate)
    edge = fb.zbox(-0.43, 0.43, -0.300, -0.272, 0.058, 0.0745)       # the one bright edge: the near chamfer catching the light
    fb.turn(edge, 14.0, 'Z')
    hl.moved(edge, 0.12, 0.10, 0.0)
    p.add('bright edge', edge, bright)
    for (hx, hy) in ((-0.30, 0.0), (0.30, 0.0)):
        hd = hl.disc(hx, hy, 0.06, 0.0745, 0.055, sides=12)
        fb.turn(hd, 14.0, 'Z')
        hl.moved(hd, 0.12, 0.10, 0.0)
        p.add('hole', hd, hole)
    ob = p.finish()
    el = math.radians(40.0)
    cam = ortho_camera((0.25, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def cover_icon():
    plate, hole = M('Icon cover', '#9FB09C', 0.35), M('Icon hole', '#3A3632', 0.2)
    p = hl.Faceted('Icon cover')
    p.add('plate', hl.chamfered_slab(0.60, 1.10, 0.07, 0.12, 0.022, centre_z=0.035), plate)
    for hx in (-0.22, 0.22):
        for hy in (-0.46, 0.46):
            p.add('screw hole', hl.disc(hx, hy, 0.06, 0.0712, 0.032, sides=12), hole)
    ob = p.finish()
    ob.rotation_euler = (0.0, 0.0, math.radians(-30.0))
    el = math.radians(48.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def _part(ob, tilt_x=0.0, spin=0.0, flip=False):
    """A device part lying in its own XY (built at any scale): turned face up if flip, spun about Z."""
    ob.rotation_euler = Euler((math.radians(180.0 if flip else 0.0) + math.radians(tilt_x), 0.0, math.radians(spin)), 'XYZ')
    return ob


def part_phone_icon():
    import hero_phone
    ob = hero_phone.screen().finish()
    fresh = M('Icon fresh glass', '#8BA4D5', 0.5)               # the prefab's M_FreshGlass (what the pad shows), not the FBX's navy
    for i, m in enumerate(ob.data.materials):
        if m.name.startswith('HD_Glass'):
            ob.data.materials[i] = fresh
    _part(ob, spin=-28.0)
    el = math.radians(42.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def part_watch_icon():
    import hero_watch
    ob = _part(hero_watch.mainspring().finish(), spin=15.0)
    el = math.radians(55.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def part_camera_icon():
    import hero_camera
    ob = _part(hero_camera.clean_mechanism().finish(), spin=-20.0)
    el = math.radians(48.0)
    cam = ortho_camera((0.25, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def cover_phone_icon():
    import hero_phone
    ob = _part(hero_phone.back_cover().finish(), flip=True, spin=-28.0)     # its outer face (-Z) up
    el = math.radians(46.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))
    return [ob], cam, False


def cover_watch_icon():
    import hero_watch
    ob = _part(hero_watch.back_plate().finish(), flip=True, spin=10.0)      # its dome (-Z) up
    el = math.radians(50.0)
    cam = ortho_camera((0.0, math.cos(el), -math.sin(el)))
    return [ob], cam, False


BUILDERS = {
    'screwdriver': lambda: tool_icon(bt.screwdriver),
    'pry': lambda: tool_icon(bt.pry_tool),
    'tweezers': lambda: tool_icon(bt.tweezers),
    'brush': lambda: tool_icon(bt.brush),
    'cloth': cloth_icon,
    'screw': screw_icon,
    'newpart': newpart_icon,
    'cover': cover_icon,
    'part_phone': part_phone_icon,
    'part_watch': part_watch_icon,
    'part_camera': part_camera_icon,
    'cover_phone': cover_phone_icon,
    'cover_watch': cover_watch_icon,
}


def contact_sheet(paths, dst):
    """Every icon at 36 and 56 px on the card's cream, eight to a band (the generic set, then the per-device set)."""
    from PIL import Image, ImageDraw
    cell = 104
    per_band = 8
    bands = [paths[i:i + per_band] for i in range(0, len(paths), per_band)]
    band_h = 2 * cell + 36
    w = cell * per_band + 24
    sheet = Image.new('RGB', (w, band_h * len(bands) + 30), CREAM)
    d = ImageDraw.Draw(sheet)
    for b, band in enumerate(bands):
        y0 = b * band_h
        for i, (key, path) in enumerate(band):
            icon = Image.open(path).convert('RGBA')
            for row, size in enumerate((36, 56)):
                small = icon.convert('RGBa').resize((size, size), Image.LANCZOS).convert('RGBA')
                x = 12 + i * cell + (cell - size) // 2
                y = y0 + 12 + row * cell + (cell - size) // 2
                sheet.paste(small, (x, y), small)
            d.text((12 + i * cell + 6, y0 + 2 * cell + 16), key, fill=(0x4A, 0x44, 0x3C))
    d.text((w - 150, band_h * len(bands) + 10), 'top 36 px, bottom 56 px', fill=(0x8A, 0x84, 0x7C))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    sheet.save(dst)
    # the same, twice the size, for looking at (each pixel doubled)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(dst.replace('.png', '-x2.png'))
    return dst


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    tmp = os.path.join(HERE, 'out', 'icon-renders')
    os.makedirs(tmp, exist_ok=True)
    done = []
    only = [k for k in os.environ.get('ICONS', '').split(',') if k]
    for key in KEYS:
        if only and key not in only:
            continue
        fb.clear_scene()
        key_light = lights()
        objs, cam, diag = BUILDERS[key]()
        frame(cam, objs, fill=0.80 if diag else 0.80, diagonal=diag)
        aim_key(key_light, cam)
        raw = render(os.path.join(tmp, key + '-1024.png'), cam)
        done.append((key, outline_and_reduce(raw, os.path.join(OUT, key + '.png'))))
        print('icon', key, '->', done[-1][1])
    every = [(k, os.path.join(OUT, k + '.png')) for k in KEYS if os.path.exists(os.path.join(OUT, k + '.png'))]
    if every:
        print('contact sheet', contact_sheet(every, SHEET))
