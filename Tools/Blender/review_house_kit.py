"""Build the house kit, audit every piece, and render review sheets (render only, nothing exported).

  blender -b --python Tools/Blender/review_house_kit.py -- <out folder>
  or with the bpy module: python Tools/Blender/review_house_kit.py -- <out folder>
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import house_kit as hk  # noqa: E402

out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'out')
os.makedirs(out, exist_ok=True)
fb.clear_scene()
res = hk.build_all()
rows = []
for r in res:
    ob = r['object']
    rows.append({'name': ob.name, 'group': r['group'], 'tris': r['tris'], 'size': r['size'], 'islands': r['islands'],
                 'floating': r['floating'], 'min': r['min'], 'max': r['max']})
print(json.dumps(rows, indent=1, default=str))
with open(os.path.join(out, 'house-kit-audit.json'), 'w') as fh:
    json.dump(rows, fh, indent=1, default=str)
objs = {r['object'].name: r['object'] for r in res}

# A review wall: the wall-hung and hanging pieces on a vertical board, the floor pieces on the floor in front.
wall = fb.Prop('Review wall', floor=False)
wall.add('wall', fb.zbox(-7.0, 7.0, 0.0, 0.1, -0.001, 2.8), fb.material('Review plaster', '#D9CFC0', rough=0.9))
wall.add('floor', fb.plane(16, 10, centre=(0, -4.0, -0.002)), fb.material('Review floor', '#B8B2A8', rough=0.9))
wall.finish()

# hung on the wall (y = 0, the back of each piece against it); z = the piece's origin height
hung = {
    'HK_Skirting': (-6.3, 0.0), 'HK_DadoRail': (-6.3, 0.75), 'HK_Cornice': (-6.3, 2.72), 'HK_Architrave': (-6.3, 1.40),
    'HK_WindowSill': (-6.3, 0.95), 'HK_Picture_Hills': (-4.9, 1.30), 'HK_Picture_Harbour': (-4.1, 1.30),
    'HK_Picture_Still': (-3.3, 1.30), 'HK_Picture_Portrait': (-2.7, 1.30), 'HK_Clock': (-2.0, 1.45),
    'HK_HookRail': (-1.1, 1.65), 'HK_Radiator': (-4.6, 0.0), 'HK_PorchLantern': (-0.3, 1.5),
    'HK_CurtainRod': (2.0, 2.3), 'HK_CurtainRod_End': (2.5, 2.3), 'HK_Blind': (4.2, 2.3), 'HK_LiningBoard': (5.6, 0.0),
}
for name, (x, z) in hung.items():
    objs[name].location = (x, 0.0, z)
objs['HK_LiningBoard'].rotation_euler = (math.radians(90), 0, 0)   # shown standing on the wall
# the curtain panels hang from the rod: two, the second mirrored
objs['HK_CurtainPanel'].location = (2.0 - .35, -0.09, 2.3)
cp2 = bpy.data.objects.new('HK_CurtainPanel 2', objs['HK_CurtainPanel'].data)
bpy.context.scene.collection.objects.link(cp2)
cp2.location = (2.0 + .35, -0.09, 2.3)
cp2.scale = (-1, 1, 1)
end2 = bpy.data.objects.new('HK_CurtainRod_End 2', objs['HK_CurtainRod_End'].data)
bpy.context.scene.collection.objects.link(end2)
end2.location = (1.5, 0.0, 2.3)
end2.scale = (-1, 1, 1)
objs['HK_Coat'].location = (-1.1, -0.04, 1.71)
objs['HK_Pendant'].location = (3.2, -1.2, 2.75)
objs['HK_StripLight'].location = (5.0, -1.2, 1.5)
objs['HK_Door_Panelled'].location = (6.0 - .49, -0.01, 0.0)
# on the floor in front
floor = {
    'HK_Threshold': (-6.3, -0.9, 0), 'HK_Doormat': (-6.3, -1.6, 0), 'HK_Rug_Round': (-4.3, -2.0, 0),
    'HK_Rug_Runner': (-1.6, -2.2, 0), 'HK_Plant': (-4.3, -0.8, 0), 'HK_Shoes': (-3.3, -0.9, 0),
    'HK_Slippers': (-2.8, -0.9, 0), 'HK_Bin': (-5.4, -1.0, 0), 'HK_Cushion': (0.6, -1.0, 0),
}
for name, (x, y, z) in floor.items():
    objs[name].location = (x, y, z)
# on a table (a review table at 0.75)
table = fb.Prop('Review table', floor=True)
table.add('top', fb.zbox(0.8, 4.4, -2.1, -1.3, 0.73, 0.75), fb.material('Review table', '#8F5B32', rough=0.6))
table.finish()
on_table = {
    'HK_Books_Row': (1.3, -1.7, .75), 'HK_Book_Open': (1.9, -1.7, .75), 'HK_Mug': (2.3, -1.7, .75), 'HK_Plates': (2.7, -1.7, .75),
    'HK_Post_Pile': (3.15, -1.7, .75), 'HK_Letter': (3.55, -1.7, .75), 'HK_Toaster': (4.0, -1.7, .75), 'HK_Newspaper': (3.6, -1.4, .75),
}
for name, (x, y, z) in on_table.items():
    objs[name].location = (x, y, z)

fb.studio(strength=1.0)
only = os.environ.get('KIT_SHOTS', '').split(',') if os.environ.get('KIT_SHOTS') else None
shots = [
    ('kit-all', (0.0, -1.2, 1.2), 15.5, 22, 0),
    ('kit-finish', (-5.6, -0.6, 1.2), 4.8, 18, 25),
    ('kit-pictures', (-3.4, -0.6, 1.3), 4.6, 12, 10),
    ('kit-table', (2.7, -1.6, 0.9), 3.6, 35, 5),
    ('kit-window', (3.0, -0.6, 1.6), 5.5, 10, 15),
    ('kit-door', (5.7, -0.2, 1.1), 3.6, 6, 25),
    ('kit-lantern', (-0.3, -0.1, 1.6), 1.2, 10, 30),
    ('kit-floor', (-3.8, -1.4, 0.3), 4.6, 40, -20),
]
shots = [s for s in shots if only is None or s[0] in only]
for name, tgt, dist, el, az in shots:
    cam = fb.camera('Cam ' + name, fb.look_from(tgt, dist, el, az), tgt, lens=35)
    fb.render(os.path.join(out, name + '.png'), cam, res=(1600, 1000), samples=24)
print('done')
