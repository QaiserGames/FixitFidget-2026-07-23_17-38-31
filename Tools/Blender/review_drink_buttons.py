"""
Review renders of the drinks machine's new buttons (drink_buttons.py, 26 Oct 2026) on the real machine
(Assets/Art/Models/BeverageDispenserV2.fbx, read as it is), lit like the café (warm from the front-left windows, the warm
strip under the shelf above, a soft cream fill), from the game's own close-up pose (BeverageStationSetup: 1.05 m back,
0.657 m up, pitched 23.5 degrees down, 58 degrees tall; CinematicsWave2Steps keeps it straight on), at 1920 x 1080 so a
button is as many pixels here as in the game.

  python3 Tools/Blender/review_drink_buttons.py [-- <out folder> <machine fbx> <views, e.g. 13>]

Writes:  1-first-person-at-the-machine.png   the whole machine as the close-up opens, Espresso pressed and pouring
         2-close-the-middle-four.png         Tea, Hot Chocolate, Espresso (pressed), Americano, 2x closer
         3-three-quarter.png                 from the right, low, to see the relief and the press
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import drink_buttons as db  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = args[0] if len(args) > 0 else os.path.join(HERE, 'out', 'drink-buttons')
machine = args[1] if len(args) > 1 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'BeverageDispenserV2.fbx')
views = args[2] if len(args) > 2 else '123'      # which of the three to render (two CPUs here: a view is 1-2 minutes)
PRESSED = 'Espresso'

# BeverageStationSetup.AssignModelMaterials' palette (its Color values, shown as sRGB) with its metal and smoothness
MACHINE = {
    'BF_Enamel': ('#13302d', 0.22, 0.56), 'BF_Cream': ('#decc9e', 0.05, 0.56), 'BF_Steel': ('#617070', 0.72, 0.65),
    'BF_Chrome': ('#abbab8', 0.87, 0.8), 'BF_Shadow': ('#050909', 0.05, 0.56), 'BF_Paper': ('#f2e8cc', 0.05, 0.3),
}
MACHINE.update({name: (hexstr, 0.05, 0.56) for name, hexstr in db.BF_MATERIALS.items()})

fb.clear_scene()
bpy.ops.import_scene.fbx(filepath=machine)
for ob in list(bpy.data.objects):
    if ob.type != 'MESH':
        continue
    if ob.name.endswith('ceramic paddle') or ob.name.endswith('finger grip'):
        ob.hide_render = True          # retired by the Unity step
        continue
    for slot in ob.material_slots:
        if slot.material is None:
            continue
        name = slot.material.name.split('.')[0]
        hexstr, metal, smooth = MACHINE.get(name, MACHINE['BF_Enamel'])
        slot.material = fb.material(name, hexstr, rough=1.0 - smooth, metal=metal)

results = db.build_all()
for i, name in enumerate(db.DRINKS):
    cap = bpy.data.objects['DB_Cap_' + name]
    x, y, z = db.machine_place(i)
    cap.location = (x, y + (db.PRESS if name == PRESSED else 0.0), z)

# the counter and the tiled wall, for the light to bounce off (the café's white counter, its cream tiles)
counter = bpy.data.objects.new('Counter', bpy.data.meshes.new('Counter'))
bm = fb.zbox(-1.2, 1.2, -1.4, 0.5, -0.03, 0.0)
bm.to_mesh(counter.data)
bm.free()
counter.data.materials.append(fb.material('Review_Counter', '#efede8', rough=0.35))
bpy.context.scene.collection.objects.link(counter)
wall = bpy.data.objects.new('Wall', bpy.data.meshes.new('Wall'))
bm = fb.zbox(-1.6, 1.6, 0.46, 0.5, 0.0, 1.2)
bm.to_mesh(wall.data)
bm.free()
wall.data.materials.append(fb.material('Review_Wall', '#e9e2d4', rough=0.45))
bpy.context.scene.collection.objects.link(wall)

# the café's light: warm windows front-left, the warm strip under the shelf, a soft cream fill
scene = bpy.context.scene
world = bpy.data.worlds.get('Review') or bpy.data.worlds.new('Review')
scene.world = world
bg = next((n for n in world.node_tree.nodes if n.type == 'BACKGROUND'), None)
if bg is not None:
    bg.inputs['Color'].default_value = (*fb.rgb('#cfc6b6'), 1.0)
    bg.inputs['Strength'].default_value = 0.55
sun = bpy.data.lights.new('Windows', 'SUN')
sun.energy, sun.color, sun.angle = 2.6, fb.rgb('#ffe6c4'), math.radians(10)
sun_ob = bpy.data.objects.new('Windows', sun)
scene.collection.objects.link(sun_ob)
sun_ob.rotation_euler = (math.radians(48), 0.0, math.radians(-150))     # from the front-left, high
strip = bpy.data.lights.new('Shelf strip', 'AREA')
strip.energy, strip.color, strip.shape, strip.size, strip.size_y = 60.0, fb.rgb('#ffd9a8'), 'RECTANGLE', 1.4, 0.12
strip_ob = bpy.data.objects.new('Shelf strip', strip)
scene.collection.objects.link(strip_ob)
strip_ob.location = (0.0, -0.3, 1.15)
strip_ob.rotation_euler = (math.radians(12), 0.0, 0.0)


def game_camera(name, location, target, fov_deg=58.0):
    cam = fb.camera(name, location, target, lens=50.0)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.sensor_height = 24.0
    cam.data.lens = 12.0 / math.tan(math.radians(fov_deg / 2.0))
    return cam


pitch = math.radians(23.5)
eye = (0.0, -1.05, 0.657)
aim = (0.0, -1.05 + math.cos(pitch), 0.657 - math.sin(pitch))
cam1 = game_camera('FirstPerson', eye, aim)
if '1' in views:
    fb.render(os.path.join(out, '1-first-person-at-the-machine.png'), cam1, res=(1920, 1080), samples=32)

# twice as close on the middle four, the same eye line (what a player leaning in, or a 2x crop, sees)
mid = db.machine_place(2)
mid = ((mid[0] + db.machine_place(3)[0]) / 2.0, mid[1], mid[2])
cam2 = game_camera('Close', (mid[0], mid[1] - 0.52, mid[2] + 0.19), (mid[0], mid[1], mid[2]), fov_deg=40.0)
if '2' in views:
    fb.render(os.path.join(out, '2-close-the-middle-four.png'), cam2, res=(1280, 720), samples=32)

# three-quarter, from the right and low: the relief, the pressed one against its neighbours
right = db.machine_place(4)
cam3 = game_camera('ThreeQuarter', (right[0] + 0.55, right[1] - 0.55, right[2] + 0.05), (mid[0] + 0.08, mid[1], mid[2] - 0.01), fov_deg=38.0)
if '3' in views:
    fb.render(os.path.join(out, '3-three-quarter.png'), cam3, res=(1280, 720), samples=32)
print('renders in', out)
