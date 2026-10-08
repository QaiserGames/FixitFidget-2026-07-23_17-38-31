"""
Review renders of the bench's tools and Grace's cover pieces (Cycles, CPU): the tools as the inspection camera will
see them on the rail (points down, leaning back 20 degrees, 0.55 m away), each tool close up, and the island and the
box stack with a crouched Ace (his 1.0 m capsule) behind them, seen from Grace's eye (1.7 m) at 1.5 m and 2.5 m.

With bpy:  python Tools/Blender/review_bench_tools.py [-- <output folder>]
Headless:  blender -b --python Tools/Blender/review_bench_tools.py [-- <output folder>]
Default output: Tools/Blender/out/ (not kept in git).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import bench_tools as bt  # noqa: E402
import grace_cover as gc  # noqa: E402
from mathutils import Vector  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = args[0] if args else os.path.join(HERE, 'out')
os.makedirs(out, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '40'))


def slab(name, x0, x1, y0, y1, z0, z1, hexstr):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), fb.material('Review ' + name, hexstr, rough=.7))
    return p.finish()


def capsule(name, at, radius=.35, height=1.0, hexstr='#D98A5A'):
    """Ace's capsule, standing on the floor at `at` (x, y)."""
    p = fb.Prop(name, floor=False)
    straight = height - 2 * radius
    p.add('body', fb.cylinder((0, 0, radius + straight / 2), radius, straight, sides=16), fb.material('Review Ace', hexstr, rough=.6))
    p.add('top', fb.ellipsoid((0, 0, radius + straight), (radius, radius, radius), u=16, v=8), fb.material('Review Ace', hexstr, rough=.6))
    p.add('bottom', fb.ellipsoid((0, 0, radius), (radius, radius, radius), u=16, v=8), fb.material('Review Ace', hexstr, rough=.6))
    ob = p.finish()
    ob.location = (at[0], at[1], 0)
    return ob


# ----------------------------------------------------------------------------- the tools
fb.clear_scene()
fb.studio()
tools = bt.build_all()
spacing = .055
x = -spacing * (len(tools) - 1) / 2
for r in tools:
    ob = r['object']
    ob.location = (x, 0, 0)
    ob.rotation_euler = (math.radians(-20), 0, 0)      # leaning back, as the rail's cylinders do (top toward +Y)
    x += spacing
slab('Table', -.25, .25, -.15, .15, -.02, 0, '#8F5B32')
cam = fb.camera('Rail', (0, -.55, .25), (0, 0, .06), lens=45)
fb.render(os.path.join(out, 'tools-1-on-the-rail.png'), cam, res=(1200, 800), samples=SAMPLES)
cam = fb.camera('Close', (.16, -.30, .14), (0, 0, .07), lens=50)
fb.render(os.path.join(out, 'tools-2-close.png'), cam, res=(1200, 800), samples=SAMPLES)
# each tool alone, upright, from the front and from the side
for r in tools:
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.startswith('BT_'):
            o.hide_render = True
            for c in o.children_recursive:
                c.hide_render = True
    ob = r['object']
    ob.hide_render = False
    for c in ob.children_recursive:
        c.hide_render = False
    keep = ob.location.copy(), ob.rotation_euler.copy()
    ob.location, ob.rotation_euler = (0, 0, 0), (0, 0, 0)
    h = r['size'][2]
    cam = fb.camera('Front ' + ob.name, (.10, -.28, h / 2 + .06), (0, 0, h / 2), lens=60)
    fb.render(os.path.join(out, 'tool-%s.png' % ob.name[3:].lower()), cam, res=(700, 900), samples=SAMPLES)
    ob.location, ob.rotation_euler = keep

# ----------------------------------------------------------------------------- the cover
fb.clear_scene()
fb.studio()
cover = gc.build_all()
island = next(r['object'] for r in cover if r['object'].name == 'GH_KitchenIsland')
boxes = next(r['object'] for r in cover if r['object'].name == 'GH_BoxStack')
island.location = (0, 0, 0)
boxes.location = (1.4, .1, 0)
slab('Floor', -4, 4, -4, 4, -.02, 0, '#B9A98C')
cam = fb.camera('Pieces', fb.look_from((.7, 0, .6), 3.6, 22, 35), (.7, 0, .55), lens=40)
fb.render(os.path.join(out, 'cover-1-the-pieces.png'), cam, res=(1200, 800), samples=SAMPLES)
# the cover test: a crouched Ace 0.45 m behind each piece's face, Grace's eye at 1.70 m, from 1.5 m and 2.5 m
capsule('Ace crouched (island)', (0, gc.ISLAND_D / 2 + .45))
capsule('Ace crouched (boxes)', (1.4, .1 + .236 + .45))
for dist in (1.5, 2.5):
    cam = fb.camera('Grace %.1f' % dist, (0, -gc.ISLAND_D / 2 - dist, 1.70), (0, 0, 1.0), lens=35)
    fb.render(os.path.join(out, 'cover-2-grace-at-%.1fm.png' % dist), cam, res=(1200, 800), samples=SAMPLES)
cam = fb.camera('Side', (4.2, 0, 1.4), (.7, .2, .7), lens=40)
fb.render(os.path.join(out, 'cover-3-from-the-side.png'), cam, res=(1200, 800), samples=SAMPLES)
print('renders in', out)
