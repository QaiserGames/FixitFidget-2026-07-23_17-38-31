"""Build Grace's furniture, lay it out room by room, render review pictures (Cycles)."""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402

LAYOUT = {
    # name: (x, y, z, rotation about Z in degrees)
    'GH_Rug': (0.0, 0.2, 0, 0),
    'GH_CoffeeTable': (0.0, 0.2, 0.014, 0),
    'GH_Armchair': (-1.6, 0.2, 0, 60),
    'GH_Sofa': (0.0, 1.55, 0, 180),
    'GH_TVCabinet': (1.75, 0.2, 0, -90),
    'GH_TV': (1.75, 0.2, 0.50, -90),
    'GH_Sideboard': (-0.2, -1.45, 0, 0),
    'GH_Frame_S': (-0.55, -1.52, 0.865, 8),
    'GH_Frame_M': (-0.2, -1.48, 0.865, -6),
    'GH_Frame_L': (0.8, -1.2, 0, 0),
    'GH_Frame_XL': (1.35, -1.2, 0, 0),
    'GH_StandardLamp': (-1.7, 1.35, 0, 0),
    'GH_KitchenCounter': (5.2, -1.2, 0, 0),
    'GH_WallCupboards': (5.2, -0.89 + .32 - .32, 1.45, 0),
    'GH_Kettle': (4.75, -1.3, 0.93, 20),
    'GH_Teapot': (5.35, -1.1, 0.92, -30),
    'GH_Fridge': (7.0, -1.15, 0, 0),
    'GH_CupBox': (5.4, 0.1, 0, 15),
    'GH_CupSleeve': (4.8, 0.2, 0, 70),
    'GH_Stairs': (10.0, -1.4, 0, 0),
    'GH_UnderStairsDoor': (10.0 + gh.WIDTH / 2 + .01, -1.4 + gh.DOOR_Y0 + .003, 0, 0),
    'GH_CoatStand': (11.2, -1.3, 0, 0),
    'GH_InteriorDoor_Frame': (12.4, 0.2, 0, 0),
    'GH_InteriorDoor_Leaf': (12.4 - .40, 0.2, 0, -25),
}


def build_and_lay_out():
    fb.clear_scene()
    results = gh.build_all()
    rooms = {}
    for r in results:
        ob = r['object']
        coll = fb.collection('GraceHouse_' + r['room'].replace(' ', ''))
        for c in list(ob.users_collection):
            c.objects.unlink(ob)
        coll.objects.link(ob)
        x, y, z, rz = LAYOUT.get(ob.name, (0, 0, 0, 0))
        ob.location = (x, y, z)
        ob.rotation_euler = (0, 0, __import__('math').radians(rz))
        rooms.setdefault(r['room'], []).append(r)
    return results


def render_views(out_dir):
    ground = gh.fb.Prop('Review floor')
    ground.add('floor', fb.plane(18, 7, centre=(6, 0, -0.001)), fb.material('Review floor', '#B9B2A5', rough=0.9))
    g = ground.finish()
    fb.studio(strength=1.1)
    shots = [
        ('overview', (6.0, 0.0, 0.5), 17.5, 52, 0, 38, (1800, 900)),
        ('front-room', (0.0, 0.0, 0.5), 6.2, 42, 20, 40, (1400, 1000)),
        ('kitchen', (5.8, -0.6, 0.8), 5.6, 30, 10, 40, (1400, 1000)),
        ('hall', (10.9, 0.0, 1.2), 7.4, 25, 38, 40, (1400, 1000)),
        ('front-room-from-above', (0.0, 0.0, 0.3), 9.0, 62, 25, 40, (1400, 1000)),
    ]
    paths = []
    for name, tgt, dist, el, az, lens, res in shots:
        cam = fb.camera('Cam ' + name, fb.look_from(tgt, dist, el, az), tgt, lens=lens)
        path = os.path.join(out_dir, 'grace-house-%s.png' % name)
        t0 = time.time()
        fb.render(path, cam, res=res, samples=28)
        print('rendered %s in %.1f s' % (name, time.time() - t0))
        paths.append(path)
    bpy.data.objects.remove(g, do_unlink=True)
    return paths


if __name__ == '__main__':
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'out')
    res = build_and_lay_out()
    for r in res:
        print('%-24s %5d tris  %s  floating %s' % (r['object'].name, r['tris'], r['size'], r['floating'] or '-'))
    render_views(out)
