"""
Review renders for the lived-in café's pieces (cafe_livedin.py), with their textures on. Cycles on the CPU, modest (the
machine is shared): tiles 420 x 315 at 16 samples, the big views 1000 x 650 at 24, denoised; contact sheets stitched
with PIL. Writes into the folder given (default out/livedin beside this script):

  pieces-52.png                 every piece alone from 52 degrees up, a three-quarter view (the coat on its chair, from
                                behind as well; the drawing on a cork stand-in, also straight on)
  kit-rows-overhead.png         the kit laid out in 2.2 m rows with a 1.80 m figure, from the overhead's 50 degrees
  bin-station-standing-eye.png  the three-stream station from a standing eye (1.65 m, 1.5 m off)
  coat-on-both-chairs.png       the coat on CK_Chair_Cafe and CK_Chair_Spindle (cafe_seating's own builds), behind and front
  drawing-on-the-board.png      CK_NoticeBoard with its four flyers (cafe_signs, draw_cafe_textures) and the drawing pinned
  cup-beside-the-games-cup.png  the left cup beside the cup the game serves in (DrinkLiquidVisual's shell, a latte in it)
  patron-table.png              a bistro 2-top lived in (laptop, napkins, a left cup, the coat, the tote): from the overhead
                                at the game's 50 degrees and 44 degree lens, close and at the room's ~20 m, and from a
                                standing eye
  condiment-station-close.png   the self-serve tray on a counter stand-in (stone top at 1.095), from the customer's side
  floor-pieces-overhead.png     the rug, the coir mat and the till mat from the overhead's 50 degrees, a figure for scale

Run:  python3 review_cafe_livedin.py [--out <folder>] [names: tiles rows bin coat board cup table station floor]
"""
import argparse
import math
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_TOOLS', ''), '/home/claude/cafe/s9/src/Tools/Blender'):
    if _p and os.path.exists(os.path.join(_p, 'fixit_blender.py')) and _p not in sys.path:
        sys.path.append(_p)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import cafe_livedin as li  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import cafe_props as cp  # noqa: E402
from mathutils import Vector  # noqa: E402

IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
REPO = os.path.abspath(os.path.join(HERE, '..', '..')) if IN_REPO else HERE
TEXDIR = os.path.join(REPO, 'Assets', 'Art', 'Textures', 'Cafe') if IN_REPO else os.path.join(HERE, 'tex')
SAMPLES = 16
FONT = None
for _f in (os.path.join(HERE, 'fonts', 'LeagueGothic-wdth.ttf'), '/home/claude/cafe/s9/src/Tools/Blender/fonts/LeagueGothic-wdth.ttf'):
    if os.path.exists(_f):
        FONT = _f
        break


# ============================================================================================ scene helpers

def texture_materials(extra=None):
    """The kit's textured materials get their pictures (Cycles, review only); the screen glows as Unity's emission."""
    names = dict(li.TEXTURED)
    names.update(extra or {})
    for name, rel in names.items():
        m = bpy.data.materials.get(name)
        path = rel if os.path.isabs(rel) else os.path.join(TEXDIR, os.path.basename(rel))
        if m is None or m.node_tree is None or not os.path.exists(path):
            continue
        nt = m.node_tree
        b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if b is None or b.inputs['Base Color'].is_linked:
            continue
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.interpolation = 'Cubic'
        nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])
    for name, hexstr in li.EMISSION.items():
        m = bpy.data.materials.get(name)
        if m is None or m.node_tree is None:
            continue
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        try:
            b.inputs['Emission Color'].default_value = (*fb.rgb(hexstr), 1.0)
            b.inputs['Emission Strength'].default_value = 2.2
        except (KeyError, TypeError):
            pass


def slab(name, x0, x1, y0, y1, z0, z1, hexstr, rough=0.6):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), fb.material('Review_' + name, hexstr, rough=rough))
    return p.finish()


def figure(height=1.80):
    """A plain 1.80 m figure for scale (review only)."""
    p = fb.Prop('Figure_1m80')
    mat = fb.material('Review_Figure', '#8C8478', rough=0.7)
    s = height / 1.80
    for x in (-0.09, 0.09):
        p.add('leg', fb.rbox((x - 0.065) * s, (x + 0.065) * s, -0.07 * s, 0.07 * s, 0.0, 0.86 * s, bevel=0.02), mat)
    p.add('torso', fb.rbox(-0.19 * s, 0.19 * s, -0.11 * s, 0.11 * s, 0.84 * s, 1.47 * s, bevel=0.05), mat)
    for x in (-0.24, 0.24):
        p.add('arm', fb.rbox((x - 0.05) * s, (x + 0.05) * s, -0.05 * s, 0.05 * s, 0.86 * s, 1.44 * s, bevel=0.02), mat)
    p.add('neck', fb.cylinder((0, 0, 1.50 * s), 0.05 * s, 0.08 * s, sides=8), mat)
    p.add('head', fb.ellipsoid((0, 0, 1.66 * s), (0.10 * s, 0.11 * s, 0.13 * s), u=10, v=8), mat)
    return p.finish()


def game_cup(amount=0.85):
    """The cup the game serves in: DrinkLiquidVisual.BuildShell (40 sides, smooth, open, warm ivory) with a latte in it."""
    p = hl.Faceted('Review_GameCup')
    prof = [(0.030, 0.0), (0.042, 0.109), (0.0425, 0.112), (0.0385, 0.112), (0.0375, 0.108), (0.028, 0.008), (0.0, 0.008)]
    p.add('shell', cp.spin(prof, sides=40), fb.material('Review_GameCupPaper', '#E8D9B3', rough=0.92), smooth=True)
    zs = 0.012 + (0.098 - 0.012) * amount
    rs = 0.028 + (0.036 - 0.028) * amount
    p.add('latte', fb.cylinder((0, 0, zs - 0.001), rs, 0.002, sides=40), fb.material('Review_Latte', '#A6662E', rough=0.32),
          smooth=True)
    return p.finish()


def only(objs):
    keep = {o.name for o in objs}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = o.name not in keep


def place(ob, loc, rot_z=0.0):
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(rot_z))


def dup(ob, name=None):
    c = ob.copy()
    if name:
        c.name = name
    bpy.context.scene.collection.objects.link(c)
    return c


def cam_fov(name, loc, target, fov_v_deg):
    cam = fb.camera(name, loc, target)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.lens_unit = 'FOV'
    cam.data.angle = math.radians(fov_v_deg)
    return cam


def shoot(folder, name, cam, res, samples=SAMPLES):
    path = os.path.join(folder, 'tiles', name + '.png')
    fb.render(path, cam, res=res, samples=samples)
    return path


def sheet(paths, cols, out, labels=None, bg=(217, 214, 207)):
    from PIL import Image, ImageDraw, ImageFont
    ims = [Image.open(p).convert('RGB') for p in paths]
    w, h = ims[0].size
    rows = (len(ims) + cols - 1) // cols
    canvas = Image.new('RGB', (w * cols, h * rows), bg)
    try:
        font = ImageFont.truetype(FONT, 22)
    except Exception:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(canvas)
    for i, im in enumerate(ims):
        x, y = (i % cols) * w, (i // cols) * h
        canvas.paste(im.resize((w, h)), (x, y))
        if labels:
            tw = d.textlength(labels[i], font=font)
            d.rectangle([x, y, x + tw + 14, y + 28], fill=(43, 39, 36))
            d.text((x + 7, y + 2), labels[i], fill=(242, 239, 232), font=font)
    canvas.save(out)
    return out


def bounds(ob):
    st = fb.stats(ob)
    lo, hi = Vector(st['min']), Vector(st['max'])
    return lo, hi


# ============================================================================================ the renders

def build():
    fb.clear_scene()
    res = li.build_all()
    texture_materials()
    fb.studio(strength=1.0)
    return {r['object'].name: r['object'] for r in res}


def floor_slab(name='Floor', size=6.0, hexstr='#CDBFA8'):
    return slab(name, -size, size, -size, size, -0.02, 0.0, hexstr, 0.6)


def render_tiles(obs, out):
    """Each piece alone from 52 degrees up (azimuth 30, a three-quarter view from the front right)."""
    import cafe_seating as cs
    floor = floor_slab('TileFloor', 4.0)
    chair = cs.chair_cafe().finish()
    cork = slab('CorkStandIn', -0.20, 0.20, 0.0005, 0.012, -0.03, 0.27, '#A9825A', 0.9)
    tiles, labels = [], []
    order = ['LI_Rug_Corner', 'LI_Rug_Entry', 'LI_TillMat', 'LI_NapkinDispenser', 'LI_CondimentStation', 'LI_BinStation',
             'LI_StreetBin', 'LI_Laptop_Open', 'LI_Laptop_Closed', 'LI_ToteBag', 'LI_Coat_OnChair', 'LI_KidsDrawing',
             'LI_LeftCup']
    for name in order:
        ob = obs[name]
        place(ob, (0, 0, 0))
        extra = [floor]
        if name == 'LI_Coat_OnChair':
            extra.append(chair)
            place(chair, (0, 0, 0))
        if name == 'LI_KidsDrawing':
            extra = [cork]
        only([ob] + extra)
        lo, hi = bounds(ob)
        if name == 'LI_Coat_OnChair':
            lo, hi = Vector((-0.33, -0.27, 0.0)), Vector((0.33, 0.33, 0.92))
        c = (lo + hi) / 2
        rad = max(0.06, (hi - lo).length / 2)
        dist = rad / math.tan(math.radians(14)) * 1.05
        cam = cam_fov('CamTile_' + name, fb.look_from(c, dist, 52, 30), c, 28)
        tiles.append(shoot(out, '52-' + name, cam, (420, 315)))
        labels.append(name)
        if name == 'LI_Coat_OnChair':
            cam = cam_fov('CamTileBack_' + name, fb.look_from(c, dist, 30, 155), c, 28)
            tiles.append(shoot(out, '52-' + name + '-behind', cam, (420, 315)))
            labels.append(name + ' (from behind, 30 deg)')
        if name == 'LI_KidsDrawing':
            cam = cam_fov('CamTileFront_' + name, fb.look_from(c, dist * 0.95, 4, 0), c, 28)
            tiles.append(shoot(out, '52-' + name + '-front', cam, (420, 315)))
            labels.append(name + ' (straight on)')
    sheet(tiles, 3, os.path.join(out, 'pieces-52.png'), labels=labels)
    for ob in (chair, cork):
        ob.hide_render = True


def render_rows(obs, out):
    """The kit in its 2.2 m rows (the .blend's layout) with a 1.80 m figure, from the overhead's 50 degrees."""
    import cafe_seating as cs
    floor = floor_slab('RowsFloor', 8.0)
    rows = [['LI_NapkinDispenser', 'LI_CondimentStation', 'LI_Laptop_Open', 'LI_Laptop_Closed', 'LI_LeftCup', 'LI_KidsDrawing'],
            ['LI_BinStation', 'LI_StreetBin', 'LI_ToteBag', 'LI_Coat_OnChair'],
            ['LI_Rug_Corner', 'LI_Rug_Entry', 'LI_TillMat']]
    placed = [floor]
    chair = cs.chair_spindle().finish()
    fig = figure()
    for i, row in enumerate(rows):
        x = 0.0
        for name in row:
            ob = obs[name]
            lo, hi = bounds(ob)
            w = hi.x - lo.x
            loc = Vector((x - lo.x, i * 2.2, 0.0))
            if name == 'LI_Coat_OnChair':
                off = li.COAT_ON['CK_Chair_Spindle']
                place(chair, loc)
                placed.append(chair)
                loc = loc + Vector(off)
            if name == 'LI_KidsDrawing':
                cork = slab('RowsCork', -0.20, 0.20, 0.0005, 0.012, -0.03, 0.27, '#A9825A', 0.9)
                loc = loc + Vector((0, 0, 0.75))
                place(cork, loc)
                stand = slab('RowsStand', -0.02, 0.02, 0.012, 0.05, -0.75, 0.0, '#4E4B47', 0.6)
                place(stand, loc)
                placed += [cork, stand]
            place(ob, loc)
            placed.append(ob)
            x += w + 0.5
    place(fig, (-0.9, 2.2, 0.0))
    placed.append(fig)
    only(placed)
    target = Vector((2.3, 2.0, 0.25))
    cam = cam_fov('CamRows', fb.look_from(target, 8.2, 50, 0), target, 44)
    p = shoot(out, 'kit-rows-overhead', cam, (1000, 650), samples=24)
    shutil.copyfile(p, os.path.join(out, 'kit-rows-overhead.png'))
    for o in (chair, fig):
        o.hide_render = True


def render_bin(obs, out):
    """The bin station from a standing eye: 1.65 m up, 1.5 m in front, a little to the right."""
    floor = floor_slab('BinFloor', 4.0, '#857E73')
    wall = slab('BinWall', -2.0, 2.0, 0.215, 0.235, 0.0, 2.6, '#E6E2D8', 0.9)
    ob = obs['LI_BinStation']
    place(ob, (0, 0, 0))
    cup = dup(obs['LI_LeftCup'], 'CupOnTop')
    place(cup, (-0.055, 0.115, 0.851), 20)              # on the compost lid, by its mouth: where a cup goes
    only([floor, wall, ob, cup])
    cam = cam_fov('CamBinEye', (0.38, -1.50, 1.65), (0.0, 0.0, 0.60), 46)
    p = shoot(out, 'bin-station-standing-eye', cam, (900, 675), samples=24)
    shutil.copyfile(p, os.path.join(out, 'bin-station-standing-eye.png'))
    cup.hide_render = True


def render_coat(obs, out):
    """The coat on both café chairs, where COAT_ON hangs it: from behind (the walking side) and from the front."""
    import cafe_seating as cs
    floor = floor_slab('CoatFloor', 4.0)
    cafe, spindle = cs.chair_cafe().finish(), cs.chair_spindle().finish()
    place(cafe, (-0.45, 0, 0))
    place(spindle, (0.45, 0, 0))
    c1 = obs['LI_Coat_OnChair']
    c2 = dup(c1, 'CoatOnSpindle')
    place(c1, Vector((-0.45, 0, 0)) + Vector(li.COAT_ON['CK_Chair_Cafe']))
    place(c2, Vector((0.45, 0, 0)) + Vector(li.COAT_ON['CK_Chair_Spindle']))
    only([floor, cafe, spindle, c1, c2])
    tgt = Vector((0.0, 0.10, 0.55))
    tiles = [shoot(out, 'coat-behind', cam_fov('CamCoatBack', fb.look_from(tgt, 2.6, 22, 160), tgt, 34), (640, 480)),
             shoot(out, 'coat-front', cam_fov('CamCoatFront', fb.look_from(tgt, 2.6, 26, 20), tgt, 34), (640, 480)),
             shoot(out, 'coat-rail-close', cam_fov('CamCoatRail', fb.look_from(Vector((-0.45, 0.25, 0.86)), 0.75, 18, 115),
                                                   Vector((-0.45, 0.25, 0.86)), 34), (640, 480))]
    sheet(tiles, 3, os.path.join(out, 'coat-on-both-chairs.png'),
          labels=['from behind: CK_Chair_Cafe (left), CK_Chair_Spindle', 'from the front (the seat side)',
                  'over the cafe chair\'s rail, close'])
    for o in (cafe, spindle, c2):
        o.hide_render = True


def render_board(obs, out):
    """CK_NoticeBoard as cafe_signs builds it, its flyers drawn by draw_cafe_textures, and the drawing pinned on it."""
    import cafe_signs as csg
    import draw_cafe_textures as dct
    flyers = os.path.join(out, 'tiles', 'flyers')
    os.makedirs(flyers, exist_ok=True)
    dct.draw_flyers(flyers)
    board = csg.notice_board().finish()
    texture_materials({'CF_Flyer%d' % i: os.path.join(flyers, 'CF_Flyer%d.png' % i) for i in range(1, 5)})
    wall = slab('BoardWall', -1.2, 1.2, 0.0, 0.02, -1.15, 1.6, '#E6E2D8', 0.9)
    place(board, (0, 0, 0))
    d = obs['LI_KidsDrawing']
    d.location = (0.270, -0.0150, 0.042)
    d.rotation_euler = (0, math.radians(-3.0), 0)
    only([wall, board, d])
    tgt = Vector((0.05, 0.0, 0.33))
    cam = cam_fov('CamBoard', (0.18, -1.15, 0.50), tgt, 34)
    t1 = shoot(out, 'board', cam, (900, 640), samples=24)
    cam = cam_fov('CamBoardClose', (0.30, -0.45, 0.20), Vector((0.27, 0.0, 0.15)), 34)
    t2 = shoot(out, 'board-close', cam, (900, 640), samples=24)
    sheet([t1, t2], 2, os.path.join(out, 'drawing-on-the-board.png'),
          labels=['CK_NoticeBoard with the drawing pinned (lower right)', 'close: crayon, the pin, the watch flyer under it'])
    for o in (wall, board):
        o.hide_render = True
    d.location = (0, 0, 0)
    d.rotation_euler = (0, 0, 0)


def render_cup(obs, out):
    """The left cup beside the cup the game serves in, on a table top: the same cup, a lid on."""
    top = slab('CupTable', -0.5, 0.5, -0.5, 0.5, -0.03, 0.0, '#8F5B32', 0.4)
    gc = game_cup()
    place(gc, (-0.07, 0.0, 0.0))
    cup = obs['LI_LeftCup']
    place(cup, (0.07, 0.0, 0.0), 30)
    only([top, gc, cup])
    tgt = Vector((0.0, 0.0, 0.065))
    t1 = shoot(out, 'cup-side', cam_fov('CamCup', fb.look_from(tgt, 0.55, 18, 8), tgt, 30), (640, 480), samples=24)
    t2 = shoot(out, 'cup-above', cam_fov('CamCupHi', fb.look_from(tgt, 0.75, 55, 20), tgt, 30), (640, 480), samples=24)
    sheet([t1, t2], 2, os.path.join(out, 'cup-beside-the-games-cup.png'),
          labels=["left: the game's served cup (DrinkLiquidVisual) / right: LI_LeftCup", 'from above (52-55 degrees)'])
    gc.hide_render = True


def render_table(obs, out):
    """A bistro 2-top lived in, from the game's overhead (50 degrees, a 44 degree lens) close and at the room's 20 m,
    and from a standing eye."""
    import cafe_seating as cs
    floor = floor_slab('TableFloor', 12.0, '#7D7266')
    table = cs.table_bistro_round().finish()
    ch1, ch2 = cs.chair_spindle().finish(), cs.chair_spindle().finish()
    place(table, (0, 0, 0))
    # the chairs face the table (their front, -y, toward it): the west chair turned +90, the east -90
    place(ch1, (-0.62, 0, 0), 90)
    place(ch2, (0.62, 0, 0), -90)
    coat = obs['LI_Coat_OnChair']
    # the coat as a child of the west chair at its spindle offset
    off = Vector(li.COAT_ON['CK_Chair_Spindle'])
    coat.parent = ch1
    coat.location = off
    coat.rotation_euler = (0, 0, 0)
    lap = obs['LI_Laptop_Open']
    place(lap, (-0.15, 0.0, 0.75), -90)
    nap = obs['LI_NapkinDispenser']
    place(nap, (0.10, 0.20, 0.75), 15)
    cup = obs['LI_LeftCup']
    place(cup, (0.22, -0.10, 0.75), 0)
    tote = obs['LI_ToteBag']
    place(tote, (-0.62, -0.36, 0.0), -70)
    rug = obs['LI_Rug_Corner']
    place(rug, (-2.4, 0.4, 0.0), 0)
    fig = figure()
    place(fig, (1.4, -0.6, 0.0), 0)
    objs = [floor, table, ch1, ch2, coat, lap, nap, cup, tote, rug, fig]
    only(objs)
    tgt = Vector((-0.5, 0.0, 0.5))
    t1 = shoot(out, 'table-overhead-close', cam_fov('CamTblClose', fb.look_from(tgt, 5.0, 50, 0), tgt, 44), (640, 480), samples=24)
    far = cam_fov('CamTblFar', fb.look_from(tgt, 20.0, 50, 0), tgt, 44)
    p = shoot(out, 'table-overhead-20m', far, (1600, 1200), samples=16)
    from PIL import Image
    im = Image.open(p)
    W, H = im.size
    im.crop((W // 2 - 320, H // 2 - 240, W // 2 + 320, H // 2 + 240)).save(os.path.join(out, 'tiles', 'table-overhead-20m-crop.png'))
    t2 = os.path.join(out, 'tiles', 'table-overhead-20m-crop.png')
    t3 = shoot(out, 'table-eye', cam_fov('CamTblEye', (0.9, -1.7, 1.65), Vector((-0.25, 0.05, 0.62)), 46), (640, 480), samples=24)
    sheet([t1, t2, t3], 3, os.path.join(out, 'patron-table.png'),
          labels=['overhead 50 deg, 5 m', "overhead 50 deg, 20 m (the game's distance, 1:1 pixels of a 1600 x 1200 frame)",
                  'standing eye, 1.65 m'])
    coat.parent = None
    for o in (table, ch1, ch2, fig):
        o.hide_render = True


def render_station(obs, out):
    """The condiment station on a counter stand-in (stone top at 1.095), from the customer's side."""
    floor = floor_slab('StationFloor', 4.0, '#7D7266')
    counter = slab('Counter', -1.0, 1.0, -0.47, 0.47, 0.0, 1.015, '#4A3422', 0.6)
    stone = slab('CounterStone', -1.02, 1.02, -0.49, 0.49, 1.015, 1.095, '#DDD6C9', 0.5)
    st = obs['LI_CondimentStation']
    place(st, (0.0, -0.20, 1.095))
    cup = dup(obs['LI_LeftCup'], 'StationCup')
    place(cup, (0.21, -0.20, 1.105))
    only([floor, counter, stone, st, cup])
    t1 = shoot(out, 'station-customer', cam_fov('CamStation', (0.35, -1.25, 1.62), Vector((0.0, -0.18, 1.16)), 34),
               (800, 600), samples=24)
    t2 = shoot(out, 'station-above', cam_fov('CamStationHi', fb.look_from(Vector((0, -0.2, 1.17)), 1.2, 52, 25),
                                             Vector((0, -0.2, 1.17)), 34), (800, 600), samples=24)
    sheet([t1, t2], 2, os.path.join(out, 'condiment-station-close.png'),
          labels=["the customer's side, standing (a cup set down on the clear patch)", '52 degrees'])
    cup.hide_render = True


def render_floor(obs, out):
    """The floor pieces from the overhead's 50 degrees, a 1.80 m figure for scale, on the café's smoked oak colour."""
    floor = floor_slab('FloorPieces', 8.0, '#7D7266')
    rug, coir, till = obs['LI_Rug_Corner'], obs['LI_Rug_Entry'], obs['LI_TillMat']
    place(rug, (-1.6, 0.4, 0), 0)
    place(coir, (0.5, 0.0, 0), 0)
    place(till, (1.9, 0.2, 0), 0)
    fig = figure()
    place(fig, (2.9, 0.6, 0))
    only([floor, rug, coir, till, fig])
    tgt = Vector((0.3, 0.3, 0.2))
    p = shoot(out, 'floor-pieces-overhead', cam_fov('CamFloor', fb.look_from(tgt, 7.5, 50, 0), tgt, 44), (1000, 650), samples=24)
    shutil.copyfile(p, os.path.join(out, 'floor-pieces-overhead.png'))
    fig.hide_render = True


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'livedin'))
    ap.add_argument('names', nargs='*')
    a = ap.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None)
    os.makedirs(os.path.join(a.out, 'tiles'), exist_ok=True)
    want = set(a.names) or {'tiles', 'rows', 'bin', 'coat', 'board', 'cup', 'table', 'station', 'floor'}
    for key, fn in (('tiles', render_tiles), ('rows', render_rows), ('bin', render_bin), ('coat', render_coat),
                    ('board', render_board), ('cup', render_cup), ('table', render_table), ('station', render_station),
                    ('floor', render_floor)):
        if key in want:
            obs = build()
            fn(obs, a.out)
            print(key, 'done')
    print('renders in', a.out)
