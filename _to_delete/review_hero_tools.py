"""
Review renders of the bench's hero tools (bench_tools.py v2; Cycles, CPU): the five standing points-down and leaning
back as the caddy holds them, seen from where the close-up camera is; a closer three-quarter view; and each tool on
its own, upright, from the front and from a three-quarter angle.

With bpy:  python Tools/Blender/review_hero_tools.py [-- <output folder>]
Headless:  blender -b --python Tools/Blender/review_hero_tools.py [-- <output folder>]
Default output: Tools/Blender/out/ (not kept in git). REVIEW_SAMPLES sets the Cycles samples (default 32).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import bench_tools as bt  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = args[0] if args else os.path.join(HERE, 'out')
os.makedirs(out, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '32'))


def slab(name, x0, x1, y0, y1, z0, z1, hexstr, rough=.7):
    p = fb.Prop(name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), fb.material('Review ' + name, hexstr, rough=rough))
    return p.finish()


def show_only(objs):
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.startswith('BT_') or o.name in ('Leaf_L', 'Leaf_R'):
            o.hide_render = True
    for o in objs:
        o.hide_render = False
        for c in o.children_recursive:
            c.hide_render = False


fb.clear_scene()
fb.studio(strength=1.1)
tools = bt.build_all()
roots = [r['object'] for r in tools]
spacing = .052
x = -spacing * (len(tools) - 1) / 2
for r in tools:
    ob = r['object']
    ob.location = (x, 0, .012)
    ob.rotation_euler = (math.radians(-18), 0, 0)      # leaning back as the caddy holds them (handle toward +Y)
    x += spacing
slab('Table', -.30, .30, -.20, .20, -.02, 0, '#7F5634')
slab('Mat', -.22, .22, -.12, .12, 0, .004, '#3C5377', rough=.9)
cam = fb.camera('Close-up camera', (0, -.29, .33), (0, 0, .06), lens=42)
fb.render(os.path.join(out, 'hero-1-the-five-from-the-close-up.png'), cam, res=(1400, 800), samples=SAMPLES)
cam = fb.camera('Three-quarter', (.17, -.24, .15), (0, .0, .065), lens=50)
fb.render(os.path.join(out, 'hero-2-the-five-closer.png'), cam, res=(1400, 800), samples=SAMPLES)
# each tool alone, upright
for r in tools:
    ob = r['object']
    show_only([ob])
    keep = ob.location.copy(), ob.rotation_euler.copy()
    ob.location, ob.rotation_euler = (0, 0, .004), (0, 0, 0)          # standing on the mat (its top is at 4 mm)
    h = r['size'][2]
    if h < .03:          # the cloth: lying on the mat, from above and a little in front
        cam = fb.camera('Over ' + ob.name, (.06, -.16, .17), (0, 0, .010), lens=55)
        fb.render(os.path.join(out, 'hero-tool-%s.png' % ob.name[3:].lower()), cam, res=(1000, 800), samples=SAMPLES)
    else:
        cam = fb.camera('Front ' + ob.name, (.10, -.36, h / 2 + .07), (0, 0, h / 2 + .004), lens=50)
        fb.render(os.path.join(out, 'hero-tool-%s.png' % ob.name[3:].lower()), cam, res=(700, 1000), samples=SAMPLES)
    ob.location, ob.rotation_euler = keep
print('renders in', out)
