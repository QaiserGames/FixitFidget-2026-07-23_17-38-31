"""
Review renders for the cafe props (Brief C). Cycles CPU, modest (900 x 600 or smaller, 12-16 samples; the machine
is shared), contact sheets stitched with PIL. Writes into renders/:

  plants-row.png              the five plants in a row with a 1.80 m figure, eye height
  plants-2m.png               each plant at 2 m from eye height 1.65, in colour (top) and as a silhouette (bottom)
  overhead.png                the room from the overhead camera (from the south, 50 degrees down, 44 degree field, ~20 m)
  ceiling-first-person.png    a beam and a skylight well from below, eye height 1.65
  props-close-1.png           water station, bean bag (with a UV test card on the label), bell, grinder at 0.5-0.8 m
  props-close-2.png           pothos on its shelf, the bench's end, a beam's hanger, a skylight from under it

Run:  python3 review_cafe_props.py [names of renders to make]
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, '/home/claude/cafe/inputs/Tools/Blender')
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from mathutils import Vector  # noqa: E402
import cafe_props as cp  # noqa: E402

OUT = os.path.join(HERE, 'renders')
TMP = os.path.join(OUT, 'tiles')
os.makedirs(TMP, exist_ok=True)
SAMPLES = 14


# ----------------------------------------------------------------------------- scene helpers

def glassify():
    """Cycles look only: glass and water transmit (Unity makes them transparent by their own settings)."""
    for name, ior in (('CF_Glass', 1.45), ('CF_Water', 1.33)):
        m = bpy.data.materials.get(name)
        if not m or not m.node_tree:
            continue
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        for key, val in (('Transmission Weight', 1.0), ('Transmission', 1.0), ('Roughness', 0.04), ('IOR', ior)):
            try:
                b.inputs[key].default_value = val
            except Exception:
                pass


def label_test_texture():
    """A UV test card on the label (review only): TOP at the top, L and R at the sides, a 4 x 4 grid."""
    from PIL import Image, ImageDraw, ImageFont
    w, h = 384, 512
    im = Image.new('RGB', (w, h), (237, 228, 211))
    d = ImageDraw.Draw(im)
    for i in range(1, 4):
        d.line([(w * i // 4, 0), (w * i // 4, h)], fill=(190, 170, 140), width=2)
        d.line([(0, h * i // 4), (w, h * i // 4)], fill=(190, 170, 140), width=2)
    try:
        f = ImageFont.truetype('/home/claude/cafe/inputs/fonts/leaguegothic/LeagueGothic-wdth.ttf', 90)
        fs = ImageFont.truetype('/home/claude/cafe/inputs/fonts/leaguegothic/LeagueGothic-wdth.ttf', 60)
    except Exception:
        f = fs = ImageFont.load_default()
    d.text((w // 2, 60), 'TOP', fill=(46, 125, 91), font=f, anchor='mm')
    d.text((40, h // 2), 'L', fill=(46, 46, 46), font=fs, anchor='mm')
    d.text((w - 40, h // 2), 'R', fill=(46, 46, 46), font=fs, anchor='mm')
    d.text((w // 2, h - 50), 'UV 0..1', fill=(120, 80, 40), font=fs, anchor='mm')
    d.rectangle([4, 4, w - 5, h - 5], outline=(46, 125, 91), width=6)
    path = os.path.join(TMP, 'label-uv-test.png')
    im.save(path)
    m = bpy.data.materials.get('CF_BagLabel')
    nt = m.node_tree
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(path)
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])


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


def box(name, x0, x1, y0, y1, z0, z1, hexstr, rough=0.6):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), fb.material('Review_' + name, hexstr, rough=rough))
    return p.finish()


def only(objs):
    keep = {o.name for o in objs}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = o.name not in keep


def place(ob, loc, rot_z=0.0):
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(rot_z))


def to_b(u):
    """Unity (x, y, z) to Blender."""
    return (-u[0], -u[2], u[1])


def cam_fov(name, loc, target, fov_v_deg, res):
    cam = fb.camera(name, loc, target)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.lens_unit = 'FOV'
    cam.data.angle = math.radians(fov_v_deg)
    return cam


def shoot(name, cam, res, samples=SAMPLES, transparent=False):
    path = os.path.join(TMP, name + '.png')
    fb.render(path, cam, res=res, samples=samples, transparent=transparent)
    return path


def sheet(paths, cols, out, labels=None, bg=(217, 214, 207)):
    from PIL import Image, ImageDraw, ImageFont
    ims = [Image.open(p).convert('RGB') for p in paths]
    w, h = ims[0].size
    rows = (len(ims) + cols - 1) // cols
    canvas = Image.new('RGB', (w * cols, h * rows), bg)
    try:
        font = ImageFont.truetype('/home/claude/cafe/inputs/fonts/leaguegothic/LeagueGothic-wdth.ttf', 22)
    except Exception:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(canvas)
    for i, im in enumerate(ims):
        x, y = (i % cols) * w, (i // cols) * h
        canvas.paste(im, (x, y))
        if labels:
            d.rectangle([x, y, x + 8 + 10 * len(labels[i]) * 0.62, y + 26], fill=(43, 39, 36))
            d.text((x + 6, y + 2), labels[i], fill=(242, 239, 232), font=font)
    canvas.save(out)
    return out


# ----------------------------------------------------------------------------- the renders

def build():
    fb.clear_scene()
    res = cp.build_all()
    glassify()
    label_test_texture()
    fb.studio(strength=1.0)
    obs = {r['object'].name: r['object'] for r in res}
    return obs


PLANTS = ['CK_FiddleLeafFig', 'CK_OliveTree', 'CK_RubberPlant', 'CK_Monstera', 'CK_Pothos']


def render_plants_row(obs):
    floor = box('Floor', -4.5, 4.5, -3, 3, -0.02, 0.0, '#CDB797', 0.55)
    fig = figure()
    xs = [-3.0, -1.7, -0.45, 0.75, 2.0, 3.1]
    order = ['CK_FiddleLeafFig', 'CK_OliveTree', 'CK_RubberPlant', 'CK_Monstera', 'CK_Pothos']
    stand = box('PothosStand', 1.55, 2.45, -0.12, 0.30, 0.0, 1.10, '#4A3422', 0.6)   # a shelf stand-in (1.10 high)
    for x, name in zip(xs, order):
        ob = obs[name]
        if name == 'CK_Pothos':
            place(ob, (2.0, 0.0, 1.10))
        else:
            place(ob, (x, 0.0, 0.0), 20 if name == 'CK_OliveTree' else 0)
    place(fig, (xs[-1], 0.0, 0.0))
    only([floor, fig, stand] + [obs[n] for n in order])
    cam = cam_fov('CamRow', (0.0, -7.2, 1.25), (0.05, 0.0, 0.92), 30, (900, 500))
    shoot('plants-row', cam, (900, 500))
    from shutil import copyfile
    copyfile(os.path.join(TMP, 'plants-row.png'), os.path.join(OUT, 'plants-row.png'))


def silhouette_mode(on, objs):
    """Swap every material on objs for flat black (and the world for white), or back."""
    blk = bpy.data.materials.get('Review_Black') or fb.material('Review_Black', '#000000', rough=1.0)
    if blk.node_tree:
        nt = blk.node_tree
        for n in list(nt.nodes):
            if n.type != 'OUTPUT_MATERIAL':
                nt.nodes.remove(n)
        em = nt.nodes.new('ShaderNodeEmission')
        em.inputs['Color'].default_value = (0, 0, 0, 1)
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    for ob in objs:
        if on:
            ob['saved_mats'] = [m.name for m in ob.data.materials]
            for i in range(len(ob.data.materials)):
                ob.data.materials[i] = blk
        else:
            for i, n in enumerate(ob['saved_mats']):
                ob.data.materials[i] = bpy.data.materials[n]


def render_plants_2m(obs):
    floor = box('Floor2', -2, 2, -2, 2, -0.02, 0.0, '#CDB797', 0.55)
    stand = box('Stand2', -0.45, 0.45, -0.12, 0.30, 0.0, 1.10, '#4A3422', 0.6)
    colour, sil = [], []
    w = bpy.context.scene.world
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    for name in PLANTS:
        ob = obs[name]
        base_z = 1.10 if name == 'CK_Pothos' else 0.0
        place(ob, (0, 0, base_z))
        objs = [floor, ob] + ([stand] if name == 'CK_Pothos' else [])
        only(objs)
        mid = base_z + (0.0 if name == 'CK_Pothos' else 0.0)
        top = fb.stats(ob)['max'][2] + base_z
        low = fb.stats(ob)['min'][2] + base_z
        target = (0, 0, (top + low) / 2)
        cam = cam_fov('Cam2m_' + name, (0, -2.0, 1.65), target, 50, (300, 400))
        colour.append(shoot('2m-colour-' + name, cam, (300, 400)))
        # the silhouette: black on white, the floor and stand hidden
        only([ob])
        silhouette_mode(True, [ob])
        old = (tuple(bg.inputs['Color'].default_value), bg.inputs['Strength'].default_value)
        bg.inputs['Color'].default_value = (1, 1, 1, 1)
        bg.inputs['Strength'].default_value = 1.0
        sil.append(shoot('2m-silhouette-' + name, cam, (300, 400), samples=4))
        bg.inputs['Color'].default_value = old[0]
        bg.inputs['Strength'].default_value = old[1]
        silhouette_mode(False, [ob])
    labels = ['fiddle-leaf fig', 'olive', 'rubber plant', 'monstera', 'pothos (on a shelf)'] * 2
    sheet(colour[:3] + sil[:3], 3, os.path.join(OUT, 'plants-2m-a.png'), labels=labels[:3] + labels[:3])
    sheet(colour[3:] + sil[3:], 2, os.path.join(OUT, 'plants-2m-b.png'), labels=labels[3:5] + labels[3:5])


def room_standins():
    obs = []
    obs.append(box('RoomFloor', -7.4, 7.4, -18.0, 0.0, -0.02, 0.0, '#CDB797', 0.55))
    obs.append(box('Pavement', -11, 11, 0.0, 4.0, -0.04, -0.02, '#A9A396', 0.7))
    obs.append(box('BackWall', -7.6, 7.6, -18.24, -18.0, 0.0, 3.2, '#E6E2D8', 0.9))
    obs.append(box('WestWall', 7.4, 7.64, -18.0, 0.0, 0.0, 0.78, '#E6E2D8', 0.9))
    obs.append(box('EastWall', -7.64, -7.4, -18.0, 0.0, 0.0, 0.78, '#E6E2D8', 0.9))
    obs.append(box('FrontWallW', 1.0, 7.64, 0.0, 0.24, 0.0, 0.78, '#E6E2D8', 0.9))
    obs.append(box('FrontWallE', -7.64, -1.0, 0.0, 0.24, 0.0, 0.78, '#E6E2D8', 0.9))
    # the front counter (Unity x -7.40..4.87, z 13.30..14.24, top 1.095) and the back bar, the shelf
    obs.append(box('Counter', -4.87, 7.40, -14.24, -13.30, 0.0, 1.095, '#4A3422', 0.6))
    obs.append(box('CounterTop', -4.87, 7.40, -14.24, -13.30, 1.015, 1.0955, '#E6E1D7', 0.4))
    obs.append(box('BackBar', -4.90, 7.40, -18.0, -16.90, 0.0, 1.0, '#4A3422', 0.6))
    obs.append(box('BackBarTop', -4.90, 7.40, -18.0, -16.90, 0.92, 1.0005, '#E6E1D7', 0.4))
    obs.append(box('Shelf', -4.90, 3.80, -18.0, -17.70, 1.75, 1.81, '#8F5B32', 0.4))
    obs.append(box('Dispenser', -3.30, -2.80, -17.75, -17.15, 1.0, 1.55, '#2B2724', 0.5))
    obs.append(box('Tables', 2.5, 3.4, -8.0, -4.4, 0.0, 0.75, '#8F5B32', 0.5))
    return obs


def layout(obs):
    """The props where the plan puts them (Unity coordinates from the report), for the overhead and first person."""
    placed = []
    def put(name, unity, rot=0.0, copy=False):
        ob = obs[name]
        if copy:
            ob = ob.copy()
            bpy.context.scene.collection.objects.link(ob)
        place(ob, to_b(unity), rot)
        placed.append(ob)
        return ob
    put('CK_FiddleLeafFig', (-6.85, 0, 0.75))
    put('CK_OliveTree', (-1.6, 0, 0.5))
    put('CK_OliveTree', (1.6, 0, 0.5), 140, copy=True)
    put('CK_Monstera', (-6.8, 0, 12.6))
    put('CK_RubberPlant', (3.0, 0, 6.2))
    put('CK_Pothos', (-3.68, 1.81, 17.82), 180)
    put('CK_Pothos', (4.78, 1.81, 17.82), 180, copy=True)
    put('CK_WaterStation', (3.9, 1.095, 13.50), 180)
    put('CK_ServiceBell', (-2.85, 1.095, 13.47))
    put('CK_Grinder', (4.25, 1.0, 17.50), 180)
    put('CK_SidewalkBench', (-4.3, 0, -0.50), 180)
    for i in range(8):
        put('CK_BeanBag', (-3.20 + 0.135 * i, 1.81, 17.85), 180, copy=i > 0)
    beams = [put('CK_Beam', (0, 3.2, z), 0, copy=i > 0) for i, z in enumerate((2, 5, 8, 11, 14))]
    wells = [put('CK_SkylightWell', (0, 3.2, z), 0, copy=i > 0) for i, z in enumerate((3.5, 6.5, 9.5))]
    return placed, beams, wells


def render_overhead(obs):
    room = room_standins()
    placed, beams, wells = layout(obs)
    fixtures = {o.name for o in beams + wells}
    only(room + [o for o in placed if o.name not in fixtures])
    # Unity: from the south, 50 degrees down, 44 degree vertical field, ~20 m from the room's middle
    tgt = to_b((0, 0, 7.6))
    d = 23.5
    cam_u = (0, d * math.sin(math.radians(50)), 7.6 - d * math.cos(math.radians(50)))
    cam = cam_fov('CamOverhead', to_b(cam_u), tgt, 44, (900, 600))
    shoot('overhead', cam, (900, 600), samples=16)
    from shutil import copyfile
    copyfile(os.path.join(TMP, 'overhead.png'), os.path.join(OUT, 'overhead.png'))
    only(room + placed)
    shoot('overhead-with-ceiling-fixtures', cam, (450, 300), samples=12)
    copyfile(os.path.join(TMP, 'overhead-with-ceiling-fixtures.png'), os.path.join(OUT, 'overhead-if-the-fixtures-showed.png'))
    return room, placed, beams, wells


def ceiling_deck_with_holes(wells_z):
    """A ceiling deck (Unity y 3.2) with 1.24 m holes at the wells (review only), in strips."""
    obs = []
    zs = sorted(wells_z)
    hole = 0.62
    # full-width strips between holes along z, and side strips beside each hole
    edges = [0.0] + [v for z in zs for v in (z - hole, z + hole)] + [18.0]
    for i in range(0, len(edges), 2):
        z0, z1 = edges[i], edges[i + 1]
        obs.append(box('Deck%d' % i, -7.4, 7.4, -z1, -z0, 3.2, 3.25, '#E6E2D8', 0.9))
    for k, z in enumerate(zs):
        obs.append(box('DeckSideA%d' % k, hole, 7.4, -(z + hole), -(z - hole), 3.2, 3.25, '#E6E2D8', 0.9))
        obs.append(box('DeckSideB%d' % k, -7.4, -hole, -(z + hole), -(z - hole), 3.2, 3.25, '#E6E2D8', 0.9))
    return obs


def render_ceiling(room, placed, beams, wells):
    deck = ceiling_deck_with_holes((3.5, 6.5, 9.5))
    walls = [box('WestWallFull', 7.4, 7.64, -18, 0, 0.0, 3.25, '#E6E2D8', 0.9),
             box('EastWallFull', -7.64, -7.4, -18, 0, 0.0, 3.25, '#E6E2D8', 0.9)]
    lamps = []
    for k, (x, z) in enumerate(((-3.0, 4.6), (3.0, 4.6), (-3.0, 9.0), (3.0, 9.0), (0.0, 13.0), (-6.0, 5.0), (6.0, 8.0))):
        ld = bpy.data.lights.new('Pendant%d' % k, 'POINT')
        ld.energy = 70
        ld.color = fb.rgb('#FFC98F')
        ld.shadow_soft_size = 0.15
        lo = bpy.data.objects.new('Pendant%d' % k, ld)
        bpy.context.scene.collection.objects.link(lo)
        lo.location = to_b((x, 2.55, z))
        lamps.append(lo)
    only(room + placed + deck + walls)
    # first person: eye 1.65 m by the door, looking north and up into the ceiling
    cam = cam_fov('CamCeiling', to_b((-1.4, 1.65, 0.9)), to_b((1.2, 3.0, 7.0)), 60, (900, 600))
    shoot('ceiling-first-person', cam, (900, 600), samples=16)
    from shutil import copyfile
    copyfile(os.path.join(TMP, 'ceiling-first-person.png'), os.path.join(OUT, 'ceiling-first-person.png'))
    # close: a beam's hanger at the west wall (Unity x -7.4), and a well from straight under it
    cam = cam_fov('CamHanger', to_b((-6.0, 1.80, 4.2)), to_b((-7.3, 2.98, 5.0)), 30, (450, 300))
    t1 = shoot('close-hanger', cam, (450, 300))
    cam = cam_fov('CamWell', to_b((0.35, 1.65, 6.0)), to_b((0.0, 3.4, 6.5)), 55, (450, 300))
    t2 = shoot('close-well', cam, (450, 300))
    for lo in lamps:
        lo.hide_render = True
    return deck + walls, t1, t2


def render_close(obs, extra_tiles):
    tiles = []
    floor = box('CloseTop', -0.6, 0.6, -0.6, 0.6, -0.03, 0.0, '#E6E1D7', 0.4)    # a stone top
    floor2 = box('ClosePave', -2, 2, -2, 2, -0.03, 0.0, '#A9A396', 0.7)
    shelf = box('CloseShelf', -0.45, 0.45, -0.12, 0.18, -0.06, 0.0, '#8F5B32', 0.4)
    wall = box('CloseWall', -0.6, 0.6, 0.18, 0.20, -0.9, 0.6, '#EDE4D3', 0.5)
    items = [('CK_WaterStation', (0, 0, 0), (0.30, -0.62, 0.42), (0, 0, 0.10), 'water station'),
             ('CK_BeanBag', (0, 0, 0), (0.10, -0.42, 0.20), (0, 0, 0.11), 'bean bag (UV test card)'),
             ('CK_ServiceBell', (0, 0, 0), (0.14, -0.36, 0.20), (0, 0, 0.035), 'service bell'),
             ('CK_Grinder', (0, 0, 0), (0.50, -0.78, 0.50), (0, -0.02, 0.29), 'grinder')]
    for name, loc, cam_loc, tgt, label in items:
        ob = obs[name]
        place(ob, loc)
        only([floor, ob])
        cam = cam_fov('CamClose_' + name, cam_loc, tgt, 40, (450, 300))
        tiles.append(shoot('close-' + name, cam, (450, 300)))
    sheet(tiles, 2, os.path.join(OUT, 'props-close-1.png'),
          labels=['water station', 'bean bag (UV test card)', 'service bell', 'grinder'])
    tiles2 = []
    ob = obs['CK_Pothos']
    place(ob, (0, 0, 0))
    only([shelf, wall, ob])
    cam = cam_fov('CamClosePothos', (0.25, -0.75, 0.05), (0, -0.10, -0.22), 50, (450, 300))
    tiles2.append(shoot('close-pothos', cam, (450, 300)))
    ob = obs['CK_SidewalkBench']
    place(ob, (0, 0, 0))
    only([floor2, ob])
    cam = cam_fov('CamCloseBench', (0.75, -0.55, 0.85), (1.0, 0.0, 0.32), 50, (450, 300))
    tiles2.append(shoot('close-bench', cam, (450, 300)))
    tiles2 += extra_tiles
    sheet(tiles2, 2, os.path.join(OUT, 'props-close-2.png'),
          labels=['pothos on the shelf (0.8 m)', "bench end (0.8 m)", "beam hanger at the wall", 'skylight well from under it'])


if __name__ == '__main__':
    want = set(sys.argv[1:]) or {'row', '2m', 'overhead', 'ceiling', 'close'}
    obs = build()
    if 'row' in want:
        render_plants_row(obs)
    if '2m' in want:
        render_plants_2m(obs)
    extra = []
    if 'overhead' in want or 'ceiling' in want or 'close' in want:
        room, placed, beams, wells = render_overhead(obs) if 'overhead' in want else (room_standins(), *layout(obs))
        if 'ceiling' in want or 'close' in want:
            _, t1, t2 = render_ceiling(room, placed, beams, wells)
            extra = [t1, t2]
        if 'close' in want:
            for o in placed:
                if o.name in obs:
                    pass
            render_close(obs, extra)
    print('renders in', OUT)
