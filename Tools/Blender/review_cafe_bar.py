"""
Review renders of the café's bar, assembled where it stands (render only, nothing exported).

  python3 review_cafe_bar.py [-- <out folder>]        (BAR_SHOTS=a,b,... renders only those)

The scene: Blender X = Unity x, Y = Unity z, Z = Unity y (the pieces are built in that orientation, so each goes in at
its PLACEMENT with no turn). A pale oak floor, the back and west walls in chalk white, and STAND-INS (not models of
ours): the dispenser, cup stack and basin at their scene positions, the register v4 (the library FBX) at the till, a
pastry-case box at x -2.6..-1.6, the bench stage's mat/tray/caddy where the move is expected to put them, and three
library bar stools at the repair bar. The label cards show a DEBUG atlas (cell numbers), drawn here, to prove the
mapping: the Words lead's CF_DrawerLabels replaces it.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cafe_bar as cb  # noqa: E402
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402

out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'renders')
os.makedirs(out, exist_ok=True)
MODELS = '/home/claude/cafe/inputs/models'
FONT = '/home/claude/cafe/inputs/fonts/leaguegothic/LeagueGothic-wdth.ttf'
only = os.environ.get('BAR_SHOTS', '').split(',') if os.environ.get('BAR_SHOTS') else None


def debug_atlas(path, cell_w=228, cell_h=100):
    """18 x 4 cells at the card's aspect (73 x 32 mm = 2.28): 'c<col> r<row>' on a tint that changes with the column."""
    from PIL import Image, ImageDraw, ImageFont
    cols, rows = cb.PARTS_COLS, cb.PARTS_ROWS
    im = Image.new('RGB', (cols * cell_w, rows * cell_h), (236, 230, 214))
    dr = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype(FONT, int(cell_h * 0.62))
    except Exception:
        font = ImageFont.load_default()
    for r in range(rows):
        for c in range(cols):
            x0, y0 = c * cell_w, r * cell_h          # row 0 at the top of the image (v = 1 at the top)
            tint = (236 - c * 4, 228 - r * 14, 200 + c * 2)
            dr.rectangle([x0 + 3, y0 + 3, x0 + cell_w - 4, y0 + cell_h - 4], fill=tint, outline=(40, 34, 30), width=3)
            txt = 'c%d r%d' % (c, r)
            tw = dr.textlength(txt, font=font)
            dr.text((x0 + (cell_w - tw) / 2, y0 + cell_h * 0.14), txt, fill=(30, 26, 22), font=font)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    return path


def flat(name, hexstr, rough=0.6, alpha=1.0, emit=0.0):
    m = fb.material(name, hexstr, rough=rough, metal=0.0)
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if alpha < 1.0:
        bsdf.inputs['Alpha'].default_value = alpha
    if emit > 0:
        bsdf.inputs['Emission Color'].default_value = (*fb.rgb(hexstr), 1.0)
        bsdf.inputs['Emission Strength'].default_value = emit
    return m


def box_obj(name, x0, x1, y0, y1, z0, z1, mat):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), mat)
    return p.finish()


def import_fbx(path, loc, rot_z=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            o.location = loc
            o.rotation_euler = (o.rotation_euler[0], o.rotation_euler[1], math.radians(rot_z))
    return new


fb.clear_scene()
res = cb.build_all()
objs = {r['object'].name: r['object'] for r in res}
for name, ob in objs.items():
    px, py, pz = cb.PLACEMENT[name]
    ob.location = (px, pz, py)

# review-only material settings: the glass see-through, the strips glowing, the debug atlas on the cards
g = bpy.data.materials['CF_Glass']
bsdf = next(n for n in g.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Alpha'].default_value = 0.22
for nm, strength in (('CF_LightStrip', 6.0),):
    m = bpy.data.materials[nm]
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Emission Color'].default_value = (*fb.rgb(cb.MATERIALS[nm][0]), 1.0)
    b.inputs['Emission Strength'].default_value = strength
atlas = debug_atlas(os.path.join(HERE, 'debug', 'CF_DrawerLabels_debug.png'))
lab = bpy.data.materials['CF_DrawerLabels']
nt = lab.node_tree
tex = nt.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(atlas)
tex.interpolation = 'Closest'
b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])

# the room: floor, back wall (with the back door), west wall
floor = flat('Review floor (pale oak)', '#CDB797', rough=0.55)
wall = flat('Review wall (chalk white)', '#E6E2D8', rough=0.9)
door = flat('Review door', '#5A4632', rough=0.6)
stand = flat('Review stand-in', '#5E5C58', rough=0.5)
stand_light = flat('Review stand-in light', '#B9B4AA', rough=0.5)
mat_green = flat('Review mat green', '#2E7D5B', rough=0.7)
box_obj('Floor', -7.4, 7.4, 0.0, 18.0, -0.02, 0.0, floor)
box_obj('Back wall west', -7.6, 5.6, 18.0, 18.2, 0.0, 3.2, wall)
box_obj('Back wall east', 6.6, 7.6, 18.0, 18.2, 0.0, 3.2, wall)
box_obj('Back wall over the door', 5.6, 6.6, 18.0, 18.2, 2.1, 3.2, wall)
box_obj('Back door', 5.6, 6.6, 18.08, 18.12, 0.0, 2.1, door)
box_obj('West wall', -7.6, -7.4, 10.0, 18.2, 0.0, 3.2, wall)
# stand-ins: dispenser, cup stack, basin, pastry case, the bench stage
box_obj('STAND-IN dispenser', 3.06 - 0.21, 3.06 + 0.21, 17.43 - 0.22, 17.43 + 0.23, 1.0, 1.62, stand)
box_obj('STAND-IN dispenser front', 3.06 - 0.17, 3.06 + 0.17, 17.43 - 0.225, 17.43 - 0.215, 1.25, 1.55, stand_light)
box_obj('STAND-IN cup stack', 2.43 - 0.05, 2.43 + 0.05, 17.32 - 0.05, 17.32 + 0.05, 1.0, 1.30, stand_light)
box_obj('STAND-IN basin', 3.70 - 0.13, 3.70 + 0.13, 17.32 - 0.10, 17.32 + 0.10, 1.0, 1.10, stand)
glass_case = flat('Review pastry glass', '#DCE6E4', rough=0.1, alpha=0.3)
box_obj('STAND-IN pastry case base', -2.58, -1.62, 13.45, 13.98, 1.095, 1.16, flat('Review oak', '#9A7044'))
box_obj('STAND-IN pastry case glass', -2.56, -1.64, 13.47, 13.96, 1.16, 1.52, glass_case)
# the bench stage where the move plan puts it (Systems lead, 10 Oct): mat, tray, caddy and its four tools
MX, MZ = -5.034, 13.998
box_obj('STAND-IN stage mat', -5.334, -4.734, 13.818, 14.178, 1.000, 1.006, mat_green)
box_obj('STAND-IN stage tray', -5.344, -5.174, 13.913, 14.083, 1.006, 1.030, stand_light)
box_obj('STAND-IN stage caddy', -4.849, -4.729, 14.018 - 0.04, 14.018 + 0.04, 1.006, 1.041, stand)
_tools = fb.Prop('STAND-IN caddy tools', floor=False)
for k in range(4):
    x = -4.835 + k * 0.03
    _tools.add('tool', fb.strut((x, 14.018 - 0.20 * math.sin(math.radians(18)), 1.03 + 0.20 * math.cos(math.radians(18))), (x, 14.018, 1.03), 0.004, 0.004, sides=6), stand)
_tools.finish()
# the library pieces: the register v4 at the till (keys and drawer to Ace), three bar stools at the repair bar
try:
    import_fbx(os.path.join(MODELS, 'CashRegister.fbx'), (0.0, 13.92, 1.095), rot_z=180.0)
    stool_wood = flat('Review stool chestnut', '#8F5B32', rough=0.4)
    for x in (-5.2, -4.2, -3.2):
        for o in import_fbx(os.path.join(MODELS, 'Stool_Bar.fbx'), (x, 12.85, 0.0), rot_z=180.0):
            if o.type == 'MESH':
                for i in range(len(o.material_slots)):
                    o.material_slots[i].material = stool_wood
except Exception as exc:      # pragma: no cover
    print('library import failed:', exc)

# light: soft daylight from the windows (the studio suns, dimmed), the lamp's spot onto the mat, one warm light every
# 1.2 m under the shelf grazing the tile (both as the report asks Unity to place them)
fb.studio(strength=0.55, world='#B9B1A5')
sc = bpy.context.scene
sc.view_settings.exposure = -0.35


def add_light(name, kind, loc, energy, colour, rot=(0, 0, 0), size=None, spot=None):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = fb.rgb(colour)
    if size is not None:
        ld.shadow_soft_size = size
    if spot is not None:
        ld.spot_size = math.radians(spot)
        ld.spot_blend = 0.4
    ob = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = tuple(math.radians(a) for a in rot)
    return ob


lw = cb.to_world('CK_BenchLamp', cb.LAMP['light'])
add_light('Lamp spot', 'SPOT', (lw[0], lw[2], lw[1]), 14.0, '#FFE4C2', size=0.05, spot=50)
for x in cb.SHELF_BRACKETS_WORLD[:-1]:
    add_light('Shelf light', 'POINT', (x + 0.6, 17.78, 1.70), 4.0, '#FFD7A8', size=0.05)
for x in (-5.79, -2.79, 0.21, 3.21):                     # the four pendants over the counter (session 6), as lights
    add_light('Pendant', 'POINT', (x, 13.75, 2.6), 22.0, '#FFC98F', size=0.15)

sc.render.threads_mode = 'FIXED'
sc.render.threads = 2
sc.cycles.max_bounces = 4
sc.cycles.diffuse_bounces = 2
sc.cycles.glossy_bounces = 1
sc.cycles.transparent_max_bounces = 6


def shoot(name, loc, target, fov_v, res=(900, 600), samples=16, ortho=None):
    if name.startswith('e'):
        res = (960, 540)                                   # the close-up's own 16:9 frame
    cam = fb.camera('Cam ' + name, loc, target, lens=35)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.sensor_height = 24.0
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = 12.0 / math.tan(math.radians(fov_v) / 2)
    path = os.path.join(out, name + '.png')
    fb.render(path, cam, res=res, samples=samples)
    print('rendered', path)


def from_target(target, dist, elev, az=0.0):
    t = Vector(target)
    e, a = math.radians(elev), math.radians(az)
    return (t.x + dist * math.cos(e) * math.sin(a), t.y - dist * math.cos(e) * math.cos(a), t.z + dist * math.sin(e))


# the close-up camera after the move: 0.58 m from the inspect point (the mat's centre), 58 degrees down, looking south
ip = Vector((MX, MZ, 1.006))
ic = ip + Vector((0, 0.58 * math.cos(math.radians(58)), 0.58 * math.sin(math.radians(58))))     # (-5.034, 14.305, 1.498)
ic_far = ip + (ic - ip) * 1.2
ic_near = ip + (ic - ip) * 0.42
shots = [
    ('a-overhead', from_target((-1.25, 15.7, 0.9), 15.5, 50, 0), (-1.25, 15.7, 0.9), 44),
    ('a2-overhead-game-distance', from_target((-1.25, 13.5, 0.0), 21.0, 50, 0), (-1.25, 13.5, 0.0), 44),
    ('b-from-the-door', (0.0, 1.0, 1.65), (0.0, 18.0, 1.30), 50),
    ('c1-repair-bar-from-a-stool', (-4.2, 12.85, 1.25), (-4.45, 14.10, 1.00), 55),
    ('c1b-repair-bar-from-a-stool-adult-eye', (-4.2, 12.85, 1.52), (-4.5, 14.05, 1.00), 55),
    ('c2-repair-bar-from-ace', (-4.2, 14.80, 1.65), (-4.75, 13.85, 0.98), 60),
    ('d-parts-wall', (-5.60, 14.9, 1.375), (-5.60, 18.0, 1.375), 46),
    ('d2-parts-wall-close', (-6.62, 17.12, 1.56), (-6.62, 18.0, 1.53), 34),
    ('e-close-up-camera', tuple(ic), tuple(ip), 40),
    ('e2-close-up-zoom-1.2x', tuple(ic_far), tuple(ip), 40),
    ('e3-close-up-zoom-0.42x', tuple(ic_near), tuple(ip), 40),
    ('f-bench-from-ace-low', (-4.6, 15.9, 0.75), (-4.6, 14.2, 0.75), 55),
    ('g-counter-round-end', (5.9, 11.6, 1.55), (3.6, 13.8, 0.6), 50),
]
for name, loc, tgt, fov in shots:
    if only is None or name in only:
        shoot(name, loc, tgt, fov)
print('done')
