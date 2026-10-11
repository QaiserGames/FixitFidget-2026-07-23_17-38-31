"""
Review renders for the intake tray (cafe_intake.py), in the cafe's own numbers: the stone top at 1.095, the five intake
slots at z 13.65 (x 1.56, 0.78, 0.00, -0.78, -1.56), register v4 behind slot 2, the pastry case on the east run, and
the hero devices on the base slots exactly as PlacementJitter puts them (slot + up * restHeight, the slot's turn; no
jitter): the hero phone on slot 0, the pocket watch on slot 1, Grace's camera on slot 2.

Renders (Cycles CPU, modest): before/after from the game's overhead camera (50 deg from the south, 34 m, a 44 deg
vertical field, 1920 x 1080 so a tray's size at 1080p can be read off it), the same view closer, first person at the
till (the counter camera's pose: 1.883 m up behind the till, looking south, a 60 deg field), Ace's eye at the counter's
stand point, and two close views (the customers' side; Ace's side, the open end). Stand-ins (the stone, the counter's
front, the register, the pastry case, the back bar, the floor) are boxes in the palette's colours: for judging the
tray, not shipped.

With bpy:  python3 review_cafe_intake.py [-- <render folder> <device fbx folder>]
Defaults:  Logs/Blender/cafe_intake and Assets/Art/Models/Devices in the repo. REVIEW_SAMPLES (16) sets Cycles' samples.
The device FBX folder holds HD_Phone.fbx, HD_Watch.fbx and HD_Camera.fbx (Assets/Art/Models/Devices in the repo).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_LIB', '')):
    if _p and _p not in sys.path:
        sys.path.insert(0, _p)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import cafe_intake as ci  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = args[0] if len(args) > 0 else os.path.join(REPO, 'Logs', 'Blender', 'cafe_intake')      # Logs/ is local, not committed
DEVICES = args[1] if len(args) > 1 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'Devices')
SAMPLES = int(os.environ.get('REVIEW_SAMPLES', '16'))

STONE = 1.095
SLOTS = [1.56, 0.78, 0.00, -0.78, -1.56]      # Unity x, slot order 0..4 (0..2 open from Day 1)
SLOT_Y, SLOT_Z = 1.100, 13.65


def B(x, y, z):
    """Unity (x, y, z) -> Blender."""
    return (-x, -z, y)


def ubox(name, x0, x1, y0, y1, z0, z1, mat, col):
    """A stand-in box from Unity boundaries."""
    bm = fb.zbox(-x1, -x0, -z1, -z0, y0, y1)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def import_device(path, keep_hidden, extra_screws):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = next(o for o in new if o.parent is None and o.type == 'MESH')
    for o in new:
        if o.name.split('.')[0] in keep_hidden:
            o.hide_render = True
            o.hide_viewport = True
    screw = next((o for o in new if o.name.split('.')[0] == 'Screw'), None)
    if screw is not None:
        bpy.context.view_layer.update()
        here = screw.matrix_world.copy()               # the FBX's one screw, the device still at the origin
        su = (-here.translation.x, here.translation.z, -here.translation.y)      # its place in Unity, device-local
        for (ux, uy, uz) in extra_screws:            # the prefab's other screws (Unity, device-local)
            s = screw.copy()
            bpy.context.scene.collection.objects.link(s)
            s.parent = None
            s.matrix_world = Matrix.Translation(Vector(B(ux - su[0], uy - su[1], uz - su[2]))) @ here
            s.parent = root
            s.matrix_parent_inverse = root.matrix_world.inverted()
    root['import_matrix'] = [list(r) for r in root.matrix_world]      # as imported, at the origin
    return root


def place_device(root, slot_x, rest, lift=0.0):
    """As PlacementJitter does with no jitter: slot + up * restHeight, the slot's turn (180), composed with whatever
    turn the FBX import gave the root (so its parts keep their places)."""
    m0 = Matrix([list(r) for r in root['import_matrix']])
    root.matrix_world = Matrix.Translation(Vector(B(slot_x, SLOT_Y + rest + lift, SLOT_Z))) @ Matrix.Rotation(math.pi, 4, 'Z') @ m0


def cam_unity(name, frm, at, vfov_deg, res):
    c = fb.camera(name, B(*frm), B(*at), lens=35.0)
    c.data.sensor_fit = 'VERTICAL'
    c.data.angle = math.radians(vfov_deg)
    c.data.clip_start = 0.02
    c.data['res'] = res
    return c


def shoot(cam, name, res=(960, 600), samples=None):
    path = os.path.join(OUT, name + '.png')
    fb.render(path, cam, res=res, samples=samples or SAMPLES)
    print('rendered', path)
    return path


def main():
    fb.clear_scene()
    scene = bpy.context.scene
    fb.studio(scene, strength=0.75, world='#CFCAC0')
    # The counter's four pendants (session 6), warm, 2.63 m up over z 13.75.
    for i, x in enumerate((-5.79, -2.79, 0.21, 3.21)):
        ld = bpy.data.lights.new('Pendant %d' % i, 'POINT')
        ld.energy = 60.0
        ld.color = fb.rgb('#FFC98F')
        ld.shadow_soft_size = 0.12
        ob = bpy.data.objects.new('Pendant %d' % i, ld)
        scene.collection.objects.link(ob)
        ob.location = B(x, 2.63, 13.75)

    ctx = fb.collection('Stand-ins')
    stone = fb.material('stand-in stone', '#DDD6C9', rough=0.5)
    boards = fb.material('stand-in counter front', '#4A3422', rough=0.62)
    floor = fb.material('stand-in floor', '#7D7266', rough=0.55)
    steel = fb.material('stand-in register', '#8C8A84', rough=0.5)
    screen = fb.material('stand-in register display', '#A9B98C', rough=0.4)
    case = fb.material('stand-in pastry case', '#C9C3B8', rough=0.3)
    bar = fb.material('stand-in back bar', '#3E2C1E', rough=0.6)
    ubox('floor', -7.4, 7.4, -0.02, 0.0, 0.0, 18.0, floor, ctx)
    ubox('stone top', -7.4, 4.87, STONE - 0.08, STONE, 13.30, 14.24, stone, ctx)
    ubox('counter front', -7.4, 4.87, 0.0, STONE - 0.08, 13.34, 14.20, boards, ctx)
    # Register v4 at the till (Bar 1: x -0.118..0.318, z 13.796..14.104, its tall side to the queue).
    ubox('register base', -0.118, 0.318, STONE, STONE + 0.231, 13.796, 14.104, steel, ctx)
    ubox('register tower', -0.05, 0.25, STONE + 0.231, STONE + 0.411, 13.80, 13.90, steel, ctx)
    ubox('register display', -0.03, 0.23, STONE + 0.30, STONE + 0.38, 13.795, 13.80, screen, ctx)
    ubox('pastry case', 3.285, 4.335, STONE, STONE + 0.469, 13.375, 13.925, case, ctx)
    ubox('back bar', -7.4, 4.9, 0.0, 1.0, 16.9, 18.0, bar, ctx)
    ubox('back bar top', -7.4, 4.9, 1.0, 1.08, 16.9, 18.0, stone, ctx)

    # The devices, as the game puts them on the base slots.
    phone = import_device(os.path.join(DEVICES, 'HD_Phone.fbx'), {'Screen', 'ScreenPlain'}, [(-0.022, -0.0047, -0.058)])
    watch = import_device(os.path.join(DEVICES, 'HD_Watch.fbx'), {'Mainspring', 'MainspringBroken'},
                          [(-0.032, -0.02, 0.032), (0.032, -0.02, -0.032), (-0.032, -0.02, -0.032)])
    camera_ = import_device(os.path.join(DEVICES, 'HD_Camera.fbx'), {'WorkingBlade'}, [])
    place_device(phone, SLOTS[0], 0.010)
    place_device(watch, SLOTS[1], 0.010)
    place_device(camera_, SLOTS[2], 0.004, lift=0.023)      # the prefab lifts every part of the camera 23 mm

    # Cameras (Unity numbers).
    pitch, dist = math.radians(50.0), 34.0
    target = (0.0, 1.0, 15.0)       # Ace behind the till: the overhead camera follows him
    over = (target[0], target[1] + dist * math.sin(pitch), target[2] - dist * math.cos(pitch))
    cam_over = cam_unity('overhead (game)', over, target, 44.0, (1920, 1080))
    near = (0.0, 1.1 + 6.0 * math.sin(pitch), 13.65 - 6.0 * math.cos(pitch))
    cam_near = cam_unity('overhead (closer)', near, (0.0, 1.1, 13.65), 44.0, (960, 600))
    cam_till = cam_unity('counter camera (first person at the till)', (0.0, 1.883, 15.311), (0.0, 1.883, 10.0), 60.0, (960, 540))
    cam_eye = cam_unity("Ace's eye at the counter's stand point", (-0.26, 1.65, 14.54), (0.55, 1.10, 13.62), 60.0, (960, 540))
    cam_cust = cam_unity("close, from the customers' side", (0.95, 1.42, 13.02), (0.78, 1.11, 13.66), 40.0, (960, 600))
    cam_ace = cam_unity("close, from Ace's side (the open end)", (1.10, 1.40, 14.30), (1.42, 1.11, 13.70), 40.0, (960, 600))

    # BEFORE: the devices on the bare stone.
    shoot(cam_over, '1-before-overhead-game-camera-1080p', res=(1920, 1080), samples=8)
    shoot(cam_till, '1-before-first-person-at-the-till', res=(960, 540))

    # AFTER: a tray under each slot (Day 1: the three open slots; then with both Shelf Space levels).
    trays = []
    for i, x in enumerate(SLOTS):
        ob = ci.build_tray()
        ob.name = 'CK_IntakeTray (slot %d)' % i
        ob.location = B(x, STONE, SLOT_Z)
        ob.rotation_euler = (0.0, 0.0, math.radians(180.0))
        trays.append(ob)
    for ob in trays[3:]:
        ob.hide_render = True              # not bought yet: the game shows no tray there
    shoot(cam_over, '2-after-overhead-game-camera-1080p', res=(1920, 1080), samples=8)
    shoot(cam_near, '2-after-overhead-closer')
    shoot(cam_till, '2-after-first-person-at-the-till', res=(960, 540))
    shoot(cam_eye, "2-after-aces-eye-at-the-stand-point", res=(960, 540))
    shoot(cam_cust, "2-after-close-customers-side")
    shoot(cam_ace, "2-after-close-aces-side-open-end")
    for ob in trays[3:]:
        ob.hide_render = False
    shoot(cam_near, '3-after-overhead-closer-shelf-space-bought')


if __name__ == '__main__':
    main()
