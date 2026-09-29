"""Build the layout v2 pieces, audit them, and render a review sheet (render only)."""
import math, os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa
import fixit_blender as fb  # noqa
import grace_house_v2 as g2  # noqa

out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'out')
fb.clear_scene()
res = g2.build_all()
rows = []
for r in res:
    ob = r['object']
    rows.append({k: r[k] for k in ('room', 'what', 'islands', 'floating')} | {'name': ob.name, 'tris': r.get('tris'), 'size': r.get('size')})
print(json.dumps(rows, indent=1, default=str))
# lay out: the stairs on their own, the rest in a row
layout = {
    'GH_Stairs_L': (-4.2, 0.0, 0), 'GH_KitchenCounter_Short': (0.3, 1.2, 0), 'GH_Bed': (0.6, -2.2, 0),
    'GH_Quilt_Made': (0.6, -2.2, 0), 'GH_Quilt_Asleep': (3.0, -2.2, 0), 'GH_BedsideTable': (4.6, -2.2, 0),
    'GH_BedsideLamp': (4.6, -2.2, .55), 'GH_Wardrobe': (2.6, 1.2, 0), 'GH_DressingTable': (4.4, 1.2, 0),
    'GH_BedroomDoor_Frame': (6.4, 0.2, 0), 'GH_BedroomDoor_Leaf': (5.72, -0.6, 0),
}
objs = {r['object'].name: r['object'] for r in res}
for name, (x, y, z) in layout.items():
    objs[name].location = (x, y, z)
# a second bed for the asleep quilt, a second leaf
bed2 = bpy.data.objects.new('GH_Bed 2', objs['GH_Bed'].data)
bpy.context.scene.collection.objects.link(bed2)
bed2.location = (3.0, -2.2, 0)
leaf2 = bpy.data.objects.new('GH_BedroomDoor_Leaf 2', objs['GH_BedroomDoor_Leaf'].data)
bpy.context.scene.collection.objects.link(leaf2)
leaf2.location = (7.08, -0.6, 0)
leaf2.rotation_euler = (0, 0, math.radians(180))
g = fb.Prop('floor', floor=False)
g.add('floor', fb.plane(24, 14, centre=(1.5, -0.5, -0.001)), fb.material('Review floor', '#B8B2A8', rough=0.9))
g.finish()
fb.studio(strength=1.0)
for name, tgt, dist, el, az in (("v2-pieces", (1.2, -0.5, 0.7), 12.5, 40, 20), ("v2-asleep", (3.0, -2.2, 0.5), 3.6, 45, 15),
                               ('v2-stairs', (-3.4, -1.2, 1.1), 6.2, 32, 28),
                               ('v2-stairs-above', (-3.4, -1.2, 0.6), 7.0, 62, -35)):
    cam = fb.camera('Cam ' + name, fb.look_from(tgt, dist, el, az), tgt, lens=35)
    fb.render(os.path.join(out, name + '.png'), cam, res=(1600, 1000), samples=32)
print('done')
