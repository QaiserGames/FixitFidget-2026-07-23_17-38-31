"""
A mock-up of Grace's ground floor for the step 5 discussion (render only; nothing here goes into the
game). The shell follows the proposed floor plan (claude/break-ins-spec.md section 4,
grace-house-ground-floor.png): inside 5.3 m along West Street by 4.7 m deep, the hall down the south
side with the stairs, the kitchen at the back, the front room at the front with the window.
Plan coordinates: X from the south wall (0) to the north wall (5.3); Y from the back wall (0) to the
street (4.7). In Blender, x = X and y = -Y, so the street is at -Y (a piece's front faces -Y).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402

W, D = 5.3, 4.7          # inside
T = 0.20                 # outer walls
CUT = 1.05               # cut-away height of the walls toward the camera
FULL = 2.40


def shell():
    p = fb.Prop('Shell', floor=False)     # its floor slabs go below z = 0 on purpose
    wall = fb.material('Mock wall', '#E9DDC8', rough=0.8)
    wall_in = fb.material('Mock wall hall', '#D8C9A8', rough=0.8)
    wood = fb.material('Mock floor wood', '#8A6746', rough=0.6)
    tile_a = fb.material('Mock tile light', '#E8E2D2', rough=0.5)
    tile_b = fb.material('Mock tile dark', '#9DA3A0', rough=0.5)
    # floors: wood in the hall and the front room, a checkered floor in the kitchen
    p.add('floor hall', fb.zbox(0, 1.9, -D, 0, -0.05, 0), wood)
    p.add('floor front room', fb.zbox(1.9, W, -D, -2.0, -0.05, 0), wood)
    n = 0
    s = 0.33
    x = 1.95
    while x < W - 1e-6:
        y = 0.0
        while y < 1.95 - 1e-6:
            x1, y1 = min(x + s, W), min(y + s, 1.95)
            p.add('tile', fb.zbox(x, x1, -y1, -y, -0.05, 0.001), tile_a if (n + int(y / s)) % 2 == 0 else tile_b)
            y += s
        n += 1
        x += s
    # the back wall and the south wall at full height; the street and north walls cut away
    p.add('back wall', fb.zbox(-T, W + T, 0, T, 0, FULL), wall)
    p.add('south wall', fb.zbox(-T, 0, -D - T, 0, 0, FULL), wall)          # meets the back wall's end, no shared faces
    p.add('north wall', fb.zbox(W, W + T, -D - T, 0, 0, CUT), wall)
    # the street wall: the front door (0.55 to 1.53) and the window (2.53 to 4.78, sill 0.9)
    p.add('street wall', fb.zbox(0, 0.55, -D - T, -D, 0, CUT), wall)
    p.add('street wall', fb.zbox(1.53, 2.53, -D - T, -D, 0, CUT), wall)
    p.add('street wall (under the window)', fb.zbox(2.53, 4.78, -D - T, -D, 0, 0.9), wall)
    p.add('street wall', fb.zbox(4.78, W, -D - T, -D, 0, CUT), wall)
    p.add('window sill', fb.zbox(2.5, 4.81, -D - T - 0.03, -D + 0.04, 0.88, 0.92), fb.material('Mock sill', '#F2EFE8', rough=0.4))
    # the hall's wall (X 1.85 to 1.95) with the kitchen doorway (Y 0.66-1.65) and the front room's (2.66-3.66)
    for y0, y1 in ((0, 0.66), (1.65, 2.66), (3.66, D)):
        p.add('hall wall', fb.zbox(1.85, 1.95, -y1, -y0, 0, CUT), wall_in)
    # kitchen | front room (Y 1.95 to 2.05)
    p.add('kitchen wall', fb.zbox(1.95, W, -2.05, -1.95, 0, CUT), wall_in)
    return p.finish()


def bistro_table():
    """Stand-in for the café's Table_BistroRound (0.72 across, 0.75 high)."""
    p = fb.Prop('Stand-in bistro table')
    wood, dark = gh.M('CC_Wood_Counter'), gh.M('CC_Wood_Espresso')
    p.add('top', fb.cylinder((0, 0, 0.73), 0.36, 0.04, sides=16), wood)
    p.add('column', fb.cylinder((0, 0, 0.37), 0.035, 0.72, sides=8), dark)
    p.add('foot', fb.cylinder((0, 0, 0.02), 0.22, 0.04, sides=12, radius_top=0.18), dark)
    return p.finish()


def spindle_chair():
    """Stand-in for the café's Chair_Spindle (seat 0.45)."""
    p = fb.Prop('Stand-in spindle chair')
    wood, dark = gh.M('CC_Wood_Counter'), gh.M('CC_Wood_Espresso')
    p.add('seat', fb.cylinder((0, 0, 0.43), 0.2, 0.04, sides=12), wood)
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        p.add('leg', fb.strut((sx * .13, sy * .13, .42), (sx * .16, sy * .16, 0), .016, .012), dark)
    p.add('back rail', fb.rbox(-.18, .18, .15, .19, .74, .8, bevel=.01), wood)
    for x in (-.12, -.04, .04, .12):
        p.add('spindle', fb.strut((x, .17, .75), (x, .16, .45), .01, .01), dark)
    return p.finish()


# where each piece goes: (plan X, plan Y, z, turn in degrees; 0 = facing the street)
PLACES = {
    'GH_KitchenCounter': (3.235, 0.31, 0, 0),
    'GH_WallCupboards': (3.1, 0.0, 1.45, 0),
    'GH_Fridge': (4.89, 0.33, 0, 0),
    'GH_Kettle': (2.695, 0.42, 0.926, 15),
    'GH_Teapot': (3.55, 0.25, 0.92, -25),
    'GH_CupBox': (2.35, 0.95, 0, 18),
    'GH_Sideboard': (2.75, 2.295, 0, 0),
    'GH_Frame_S': (2.35, 2.33, 0.865, 10),
    'GH_Frame_M': (2.75, 2.30, 0.865, -4),
    'GH_Armchair': (2.55, 2.95, 0, 90),
    'GH_Rug': (3.6, 3.15, 0, 0),
    'GH_CoffeeTable': (3.65, 3.15, 0.014, 0),
    'GH_TVCabinet': (5.06, 3.2, 0, -90),
    'GH_TV': (5.06, 3.2, 0.50, -90),
    'GH_Sofa': (3.1, 4.1, 0, 180),
    'GH_StandardLamp': (4.75, 2.3, 0, 0),
    'GH_Stairs': (0.475, 2.72, 0, 0),
    'GH_UnderStairsDoor': (0.475 + gh.WIDTH / 2 + 0.01, 2.72 - gh.DOOR_Y0 - 0.003, 0, 0),
    'GH_CoatStand': (0.3, 4.2, 0, 0),
    'GH_InteriorDoor_Frame': (1.9, 1.155, 0, 90),
    'GH_InteriorDoor_Frame.001': (1.9, 3.16, 0, 90),
}


def build():
    fb.clear_scene()
    shell()
    results = gh.build_all()
    obs = {r['object'].name: r['object'] for r in results}
    # a second doorway frame for the front room
    obs['GH_InteriorDoor_Frame.001'] = bpy.data.objects.new('GH_InteriorDoor_Frame.001', obs['GH_InteriorDoor_Frame'].data)
    bpy.context.scene.collection.objects.link(obs['GH_InteriorDoor_Frame.001'])
    for name, ob in obs.items():
        if name not in PLACES:
            ob.hide_render = True
            ob.hide_viewport = True
            continue
        X, Y, z, turn = PLACES[name]
        ob.location = (X, -Y, z)
        ob.rotation_euler = (0, 0, math.radians(turn))
    # the kitchen table and two chairs: the café's pieces, as the spec says (stand-ins here)
    t = bistro_table()
    t.location = (4.75, -1.45, 0)
    for dx, turn in ((-0.5, -90), (0.5, 90)):
        c = spindle_chair()
        c.location = (4.75 + dx, -1.45, 0)
        c.rotation_euler = (0, 0, math.radians(turn))
    # two doors, open, in the doorways
    for Y, open_turn in ((1.155, 70), (3.16, -70)):
        leaf = bpy.data.objects.new('Door', obs['GH_InteriorDoor_Leaf'].data)
        bpy.context.scene.collection.objects.link(leaf)
        leaf.location = (1.9, -(Y - 0.40), 0)
        leaf.rotation_euler = (0, 0, math.radians(90 + open_turn))
    obs['GH_InteriorDoor_Leaf'].hide_render = True
    return obs


def ground():
    g = fb.Prop('Street', floor=False)
    g.add('pavement', fb.plane(16, 12, centre=(2.6, -3.0, -0.051)), fb.material('Mock pavement', '#8E8B84', rough=0.9))
    return g.finish()


def night_lights():
    """Her evening: the standard lamp on, the TV on, the rest dark; moonlight outside."""
    w = bpy.data.worlds.get('Review') or bpy.data.worlds.new('Review')
    bpy.context.scene.world = w
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    bg.inputs['Color'].default_value = (*fb.rgb('#1B2233'), 1)
    bg.inputs['Strength'].default_value = 0.35
    for name in ('Key', 'Fill'):
        ob = bpy.data.objects.get(name)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)
    moon = bpy.data.lights.new('Moon', 'SUN')
    moon.energy = 0.25
    moon.color = fb.rgb('#9DB4FF')
    mo = bpy.data.objects.new('Moon', moon)
    bpy.context.scene.collection.objects.link(mo)
    mo.rotation_euler = (math.radians(55), 0, math.radians(-30))
    lamp = bpy.data.lights.new('Standard lamp', 'POINT')
    lamp.energy = 90
    lamp.color = fb.rgb('#FFC98A')
    lamp.shadow_soft_size = 0.12
    lo = bpy.data.objects.new('Standard lamp', lamp)
    bpy.context.scene.collection.objects.link(lo)
    lo.location = (4.75, -2.3, 1.36)
    tv = bpy.data.lights.new('TV glow', 'AREA')
    tv.energy = 40
    tv.color = fb.rgb('#9FC2FF')
    tv.size = 0.4
    to = bpy.data.objects.new('TV glow', tv)
    bpy.context.scene.collection.objects.link(to)
    to.location = (4.72, -3.2, 0.78)
    to.rotation_euler = (0, math.radians(-90), 0)
    # the lampshade and the screen glow
    for mat_name, colour, strength in (('GH_Lampshade', '#FFD49A', 4.0), ('GH_TV_Screen', '#9FC2FF', 3.0)):
        m = bpy.data.materials.get(mat_name)
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        bsdf.inputs['Emission Color'].default_value = (*fb.rgb(colour), 1)
        bsdf.inputs['Emission Strength'].default_value = strength


if __name__ == '__main__':
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'out')
    build()
    ground()
    fb.studio(strength=1.0)
    target = (2.65, -2.35, 0.5)
    views = [
        ('grace-house-mockup-day', target, 9.8, 52, 18, 30),
        ('grace-house-mockup-front-room', (3.4, -3.1, 0.5), 5.4, 44, 28, 32),
        ('grace-house-mockup-hall-and-kitchen', (1.8, -1.2, 0.8), 6.2, 48, -30, 32),
    ]
    for name, tgt, dist, el, az, lens in views:
        cam = fb.camera('Cam ' + name, fb.look_from(tgt, dist, el, az), tgt, lens=lens)
        fb.render(os.path.join(out, name + '.png'), cam, res=(1600, 1000), samples=32)
    night_lights()
    cam = fb.camera('Cam night', fb.look_from(target, 9.8, 52, 18), target, lens=30)
    fb.render(os.path.join(out, 'grace-house-mockup-night.png'), cam, res=(1600, 1000), samples=64)
    print('done')
