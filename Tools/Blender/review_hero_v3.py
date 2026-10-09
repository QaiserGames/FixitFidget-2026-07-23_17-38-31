"""
Review renders of the v3 hero set (Cycles, CPU): the five tools standing points-down and leaning back as the caddy holds
them, seen from where the close-up camera is; a closer three-quarter view; each tool alone; then each device from the
close-up's angle, front and back (and opened where the game opens it).

With bpy:  python Tools/Blender/review_hero_v3.py [-- <output folder> [tools|phone|watch|camera ...]]
Default output: Tools/Blender/out/ (not kept in git). REVIEW_SAMPLES sets the Cycles samples (default 24).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = args[0] if args else os.path.join(HERE, 'out')
which = args[1:] or ['tools', 'phone', 'watch', 'camera']
os.makedirs(out, exist_ok=True)
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '24'))


def slab(name, x0, x1, y0, y1, z0, z1, hexstr, rough=.8):
    p = fb.Prop('Review ' + name, floor=False)
    p.add(name, fb.zbox(x0, x1, y0, y1, z0, z1), fb.material('Review ' + name, hexstr, rough=rough))
    return p.finish()


def bench():
    slab('Table', -.40, .40, -.30, .30, -.02, 0, '#7F5634')
    slab('Mat', -.26, .26, -.16, .16, 0, .004, '#3C5377', rough=.95)


def shot(name, location, target, lens=50, res=(1200, 800)):
    cam = fb.camera('Cam ' + name, location, target, lens=lens)
    fb.render(os.path.join(out, name + '.png'), cam, res=res, samples=SAMPLES)
    print('rendered', name)


def hide_all_but(objs):
    keep = set()
    for o in objs:
        keep.add(o)
        keep.update(o.children_recursive)
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.name.startswith('Review '):
            o.hide_render = o not in keep
            for c in o.children_recursive:
                c.hide_render = o not in keep


if 'tools' in which:
    import bench_tools as bt
    fb.clear_scene()
    fb.studio(strength=1.0)
    bench()
    tools = bt.build_all()
    spacing = .050
    x = -spacing * (len(tools) - 1) / 2
    for r in tools:
        ob = r['object']
        ob.location = (x, 0, .012)
        ob.rotation_euler = (math.radians(-18), 0, 0)      # leaning back as the caddy holds them
        x += spacing
    # the cloth lies flat in front
    cloth = tools[-1]['object']
    cloth.location, cloth.rotation_euler = (.05, -.09, .004), (0, 0, math.radians(8))
    shot('v3-tools-1-from-the-close-up', (0, -.30, .34), (0, -.01, .05), lens=42, res=(1400, 800))
    shot('v3-tools-2-closer', (.16, -.25, .16), (0, -.01, .06), lens=50, res=(1400, 800))
    for r in tools:
        ob = r['object']
        hide_all_but([ob])
        keep = ob.location.copy(), ob.rotation_euler.copy()
        h = r['size'][2]
        if h < .03:
            ob.location, ob.rotation_euler = (0, 0, .004), (0, 0, 0)
            shot('v3-tool-' + ob.name[3:].lower(), (.07, -.15, .15), (0, 0, .006), lens=55, res=(1000, 800))
            shot('v3-tool-' + ob.name[3:].lower() + '-low', (.12, -.14, .05), (0, 0, .004), lens=55, res=(1000, 700))
        else:
            ob.location, ob.rotation_euler = (0, 0, .004), (0, 0, 0)
            shot('v3-tool-' + ob.name[3:].lower(), (.09, -.30, h / 2 + .06), (0, 0, h / 2 + .004), lens=55, res=(700, 1000))
        ob.location, ob.rotation_euler = keep
    hide_all_but([r['object'] for r in tools])

if 'phone' in which:
    import hero_phone
    fb.clear_scene()
    fb.studio(strength=1.0)
    bench()
    root, children, made = hero_phone.build()
    root.location = (0, 0, .004 + .0075)
    root.rotation_euler = (0, 0, 0)                      # face up (Blender +Z is the front)
    # the player looks from Blender +Y (Unity -Z): every device shot is from that side
    shot('v3-phone-1-front', (-.10, .26, .30), (0, 0, .01), lens=50, res=(1000, 1000))
    root.rotation_euler = (math.radians(180), 0, 0)      # back up
    root.location = (0, 0, .004 + .0100)
    shot('v3-phone-2-back', (-.10, .26, .30), (0, 0, .01), lens=50, res=(1000, 1000))
    shot('v3-phone-3-back-low', (-.16, .18, .09), (0, 0, .01), lens=55, res=(1200, 700))
    # the back cover lifted off: the inside
    cover = next(c for c in children if c.name == 'BackCover')
    cover.location = (cover.location.x - .095, cover.location.y, cover.location.z + .004)
    shot('v3-phone-4-opened', (-.12, .24, .30), (-.04, 0, .01), lens=50, res=(1200, 900))

if 'watch' in which:
    import hero_watch
    fb.clear_scene()
    fb.studio(strength=1.0)
    bench()
    root, children, made = hero_watch.build()
    root.location = (0, 0, .004 + .022)
    shot('v3-watch-1-front', (-.12, .30, .34), (0, 0, .02), lens=50, res=(1000, 1000))
    shot('v3-watch-2-front-low', (-.20, .22, .10), (0, 0, .02), lens=55, res=(1200, 700))
    root.rotation_euler = (math.radians(180), 0, 0)
    root.location = (0, 0, .004 + .012)
    shot('v3-watch-3-back', (-.12, .30, .34), (0, 0, .02), lens=50, res=(1000, 1000))
    plate = next(c for c in children if c.name == 'BackPlate')
    plate.hide_render = True
    for c in children:
        if c.name.startswith('Screw'):
            c.hide_render = True
    shot('v3-watch-4-back-open', (-.12, .30, .34), (0, 0, .02), lens=50, res=(1000, 1000))

if 'camera' in which:
    import hero_camera
    fb.clear_scene()
    fb.studio(strength=1.0)
    bench()
    root, children, made = hero_camera.build()
    root.location = (0, 0, .004 + .023)
    shot('v3-camera-1-from-the-close-up', (-.10, .40, .48), (0, 0, .03), lens=45, res=(1400, 900))
    shot('v3-camera-2-the-opening', (-.20, .22, .26), (-.05, .01, .04), lens=55, res=(1200, 900))
    shot('v3-camera-3-the-lens-side', (.14, .30, .22), (.06, 0, .05), lens=55, res=(1200, 900))
    shot('v3-camera-4-low', (-.26, .30, .14), (0, 0, .04), lens=50, res=(1400, 800))
print('renders in', out)
