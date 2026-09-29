"""
Render-only mock-up of Grace's house, layout v2 (both floors), for the step 5 discussion. Nothing here goes into
the game. The numbers come from plan_v2.py; the built pieces from grace_house.py; the new ones (the quarter-turn
stairs, the bedroom furniture) are plain stand-ins here, to be built properly once the layout is agreed.

In Blender: x = plan X (north), y = -plan Y (the street is at -y), z up from the ground floor.
    blender -b --python Tools/Blender/mockup_v2.py -- <out dir>
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
import plan_v2 as P  # noqa: E402

CUT = 1.05          # walls toward the camera, cut like the café's
T = 0.08            # lining thickness (the inner face is the plan's edge)


def mat(name, hexcol, rough=0.7, alpha=None):
    m = fb.material(name, hexcol, rough=rough)
    if alpha is not None:
        bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Alpha'].default_value = alpha
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    return m


def B(x0, x1, Y0, Y1, z0, z1):
    """A box in plan terms (X, Y), turned into Blender's (x, -Y)."""
    return fb.zbox(x0, x1, -Y1, -Y0, z0, z1)


def prism_y(Y0, Y1, poly_xz):
    """A slab across plan Y (from Y0 to Y1) whose face is the polygon [(X, z)]."""
    bm = fb.bmesh.new()
    a = [bm.verts.new((x, -Y0, z)) for x, z in poly_xz]
    b = [bm.verts.new((x, -Y1, z)) for x, z in poly_xz]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(poly_xz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# ============================================================================ walls, floors, openings


def wall_run(p, m, axis, fixed0, fixed1, a0, a1, z0, z1, holes=()):
    """A wall along X (axis 'X', fixed = Y range) or along Y (axis 'Y', fixed = X range), from a0 to a1, with
    rectangular holes [(h0, h1, hz0, hz1)] cut out as separate panels (no booleans)."""
    cuts = sorted(holes)
    spans = []
    pos = a0
    for h0, h1, hz0, hz1 in cuts:
        if h0 > pos:
            spans.append((pos, h0, z0, z1))
        if hz0 > z0:
            spans.append((h0, h1, z0, min(hz0, z1)))
        if hz1 < z1:
            spans.append((h0, h1, max(hz1, z0), z1))
        pos = h1
    if pos < a1:
        spans.append((pos, a1, z0, z1))
    for s0, s1, sz0, sz1 in spans:
        if s1 - s0 < 1e-4 or sz1 - sz0 < 1e-4:
            continue
        if axis == 'X':
            p.add('wall', B(s0, s1, fixed0, fixed1, sz0, sz1), m)
        else:
            p.add('wall', B(fixed0, fixed1, s0, s1, sz0, sz1), m)


def ground_shell(cut_sides):
    """The ground floor's floor and linings. cut_sides: the sides toward the camera, cut at CUT."""
    p = fb.Prop('Ground shell', floor=False)
    wood = mat('Mock floor wood', '#8A6746', 0.6)
    tile_a, tile_b = mat('Mock tile light', '#E8E2D2', 0.5), mat('Mock tile dark', '#9DA3A0', 0.5)
    wall = mat('Mock wall', '#EDE3D1', 0.85)
    edge = mat('Mock wall cut', '#3A332D', 0.9)
    # floors: wood, and the kitchen's checker from X 2.6 to the north wall, Y 0 to 2.06
    p.add('floor', B(-T, P.W + T, -T, P.D + T, -0.06, 0), wood)
    n, s = 0, 0.33
    x = 2.62
    while x < P.W - 1e-6:
        y = 0.0
        while y < 2.06 - 1e-6:
            x1, y1 = min(x + s, P.W), min(y + s, 2.06)
            p.add('tile', B(x, x1, y, y1, 0, 0.002), tile_a if (n + int(round(y / s))) % 2 == 0 else tile_b)
            y += s
        n += 1
        x += s
    top = lambda side: CUT if side in cut_sides else P.FLOOR1 - P.SLAB   # 2.25, the ceiling
    # back, south, north (party) and the street wall with the door and the window
    wall_run(p, wall, 'X', -T, 0, -T, P.W + T, 0, top('back'))
    wall_run(p, wall, 'Y', -T, 0, 0, P.D, 0, top('south'))
    wall_run(p, wall, 'Y', P.W, P.W + T, 0, P.D, 0, top('north'))
    street_top = top('street')
    holes = [(P.DOOR[0], P.DOOR[1], 0, P.DOOR_HEAD), (P.WINDOW[0], P.WINDOW[1], P.WINDOW_SILL, P.WINDOW_HEAD)]
    wall_run(p, wall, 'X', P.D, P.D + T, -T, P.W + T, 0, street_top, holes=holes)
    # a dark cap on the cut walls, as in the café
    for side in cut_sides:
        if side == 'back':
            p.add('cap', B(-T - .005, P.W + T + .005, -T - .005, .005, CUT, CUT + .012), edge)
        if side == 'south':
            p.add('cap', B(-T - .005, .005, -T - .005, P.D + T + .005, CUT, CUT + .012), edge)
        if side == 'north':
            p.add('cap', B(P.W - .005, P.W + T + .005, -T - .005, P.D + T + .005, CUT, CUT + .012), edge)
        if side == 'street':
            for a, b in ((-T, P.DOOR[0]), (P.DOOR[1], P.W + T)):
                p.add('cap', B(a - .005, b + .005, P.D - .005, P.D + T + .005, CUT, CUT + .012), edge)
    # the window's glass and sill
    p.add('glass', B(P.WINDOW[0], P.WINDOW[1], P.D + .03, P.D + .045, P.WINDOW_SILL, min(P.WINDOW_HEAD, street_top)),
          mat('Mock glass', '#9CC3DA', 0.1, alpha=0.35))
    p.add('sill', B(P.WINDOW[0] - .03, P.WINDOW[1] + .03, P.D - .05, P.D + T + .02, P.WINDOW_SILL - .03, P.WINDOW_SILL),
          mat('Mock sill', '#F2EFE8', 0.4))
    return p.finish()


def first_shell(cut_sides, inner_cut=False):
    """The first floor: its slab with the stairwell cut out, its linings, the bays, and the inner walls."""
    p = fb.Prop('First shell', floor=False)
    z = P.FLOOR1
    wood = mat('Mock floor boards', '#9A7652', 0.6)
    wall = mat('Mock wall bedroom', '#E4E7DA', 0.85)
    inner = mat('Mock wall', '#EDE3D1', 0.85)
    edge = mat('Mock wall cut', '#3A332D', 0.9)
    under = mat('Mock ceiling', '#F4EFE6', 0.9)
    (v0x0, v0x1, v0y0, v0y1), (v1x0, v1x1, v1y0, v1y1) = P.VOID
    # the slab, in pieces round the L-shaped stairwell
    for x0, x1, y0, y1 in ((v0x1, P.W + T, v1y1, P.D + T), (v1x1, P.W + T, -T, v1y1), (-T, v0x1, v0y1, P.D + T)):
        p.add('slab', B(x0, x1, y0, y1, z - P.SLAB, z), wood)
        p.add('ceiling', B(x0, x1, y0, y1, z - P.SLAB - .004, z - P.SLAB), under)
    hi = P.CEIL1 - P.FLOOR1
    top = lambda side: z + (CUT if side in cut_sides else hi)
    wall_run(p, wall, 'X', -T, 0, -T, P.W + T, z, top('back'))
    wall_run(p, wall, 'Y', -T, 0, 0, P.D, z, top('south'))
    wall_run(p, wall, 'Y', P.W, P.W + T, 0, P.D, z, top('north'))
    # the street wall, open where the bays are
    holes = [(bx0, bx1, z, z + 2.05) for bx0, bx1 in P.BAYS]
    wall_run(p, wall, 'X', P.D, P.D + T, -T, P.W + T, z, top('street'), holes=holes)
    # the bays: floor, sides, front with its glass
    glass = mat('Mock glass', '#9CC3DA', 0.1, alpha=0.35)
    by0, by1 = P.BAY_Y
    btop = z + (CUT if 'street' in cut_sides else 2.05)
    for bx0, bx1 in P.BAYS:
        p.add('bay floor', B(bx0, bx1, P.D, by1 - .02, z - P.SLAB, z), wood)
        p.add('bay side', B(bx0 - T, bx0, P.D + T, by1, z, btop), wall)
        p.add('bay side', B(bx1, bx1 + T, P.D + T, by1, z, btop), wall)
        p.add('bay front', B(bx0 - T, bx1 + T, by1, by1 + T, z, z + .1), wall)
        p.add('bay glass', B(bx0 + .05, bx1 - .05, by1 + .02, by1 + .035, z + .1, btop), glass)
        if btop > z + 2.0:
            p.add('bay head', B(bx0 - T, bx1 + T, P.D, by1 + T, btop, btop + .1), wall)
    # inner walls, full height (or cut, for looking down into the rooms), cut where the doors are
    ih = CUT if inner_cut else hi
    for x0, x1, y0, y1 in P.F1_WALLS:
        horizontal = (x1 - x0) > (y1 - y0)
        if (x0, x1) == (4.00, 4.10):                                   # the bathroom's side, with its door
            wall_run(p, inner, 'Y', x0, x1, y0, y1, z, z + ih, holes=[(P.BATHROOM_DOOR_Y[0], P.BATHROOM_DOOR_Y[1], z, z + 2.03)])
        else:
            p.add('wall', B(x0, x1, y0, y1, z, z + ih), inner)
            if inner_cut:
                p.add('cap', B(x0 - .003, x1 + .003, y0 - .003, y1 + .003, z + ih, z + ih + .012), edge)
    # the bedroom's doorway: a wall over it, up to the ceiling
    d0, d1 = P.BEDROOM_DOOR
    if not inner_cut:
        p.add('over the door', B(d0, d1, P.CLEAR + P.BAL, P.CLEAR + P.BAL + .10, z + P.OPENING_HEAD, z + hi), inner)
    for side in cut_sides:
        if side == 'back':
            p.add('cap', B(-T - .005, P.W + T + .005, -T - .005, .005, z + CUT, z + CUT + .012), edge)
        if side == 'south':
            p.add('cap', B(-T - .005, .005, -T - .005, P.D + T + .005, z + CUT, z + CUT + .012), edge)
    return p.finish()


# ============================================================================ the quarter-turn stairs (stand-in)


def l_stairs():
    p = fb.Prop('Stairs L (stand-in)', floor=False)
    c, R, G = P.CLEAR, P.RISE, P.GOING
    tread, riser = gh.M('CC_Wood_Counter'), gh.M('GH_Paint_White')
    runner = gh.M('GH_Rug_Field')
    dark = gh.M('CC_Wood_Espresso')
    panel = gh.M('GH_Paint_White')
    # the lower flight: risers at Y = 2.60 - 0.24 i, rising toward the back
    for i in range(6):
        y = P.LOWER_BOTTOM_Y - i * G
        p.add('riser', B(0, c, y - .02, y, i * R, (i + 1) * R - .03), riser)
        if i < 5:
            p.add('tread', B(0, c, y - G - .002, y + .02, (i + 1) * R - .03, (i + 1) * R), tread)
            p.add('runner', B(.35, c - .35, y - G - .002, y + .024, (i + 1) * R, (i + 1) * R + .004), runner)
    # the landing (1.2 m) and the cupboard under it
    p.add('landing', B(0, c, 0, c + .02, 1.2 - .03, 1.2), tread)
    # the upper flight: risers at X = 1.40 + 0.24 j, rising toward the north
    for j in range(6):
        x = c + j * G
        p.add('riser', B(x, x + .02, 0, c, 1.2 + j * R, 1.2 + (j + 1) * R - .03), riser)
        if j < 5:
            p.add('tread', B(x - .02, x + G + .002, 0, c, 1.2 + (j + 1) * R - .03, 1.2 + (j + 1) * R), tread)
            p.add('runner', B(x - .024, x + G + .002, .35, c - .35, 1.2 + (j + 1) * R, 1.2 + (j + 1) * R + .004), runner)
    # the lower flight's side (under the stringer) and the cupboard front under the upper flight
    nl = lambda y: (P.LOWER_BOTTOM_Y - y) / G * R          # tread line over the lower flight
    nu = lambda x: 1.2 + (x - c) / G * R                     # tread line over the upper flight
    poly = [(P.LOWER_BOTTOM_Y, 0), (P.LOWER_BOTTOM_Y, .2), (c, 1.2), (c, 0)]
    side = fb.bmesh.new()
    a = [side.verts.new((c, -y, zz)) for y, zz in poly]
    b = [side.verts.new((c + P.BAL, -y, zz)) for y, zz in poly]
    side.faces.new(a)
    side.faces.new(list(reversed(b)))
    for k in range(4):
        m = (k + 1) % 4
        side.faces.new([a[k], a[m], b[m], b[k]])
    fb.bmesh.ops.recalc_face_normals(side, faces=side.faces[:])
    p.add('side panel', side, panel)
    # the cupboard front: a panel under the upper flight, following it up, with the door's hole
    d0, d1 = P.CUPBOARD_DOOR
    dh = 1.14
    top = lambda x: 1.17 + (x - c) * R / G
    for poly in ([(c, 0), (c, top(c)), (d0, top(d0)), (d0, 0)],
                 [(d0, dh), (d0, top(d0)), (d1, top(d1)), (d1, dh)],
                 [(d1, 0), (d1, top(d1)), (P.UPPER_TOP_X, top(P.UPPER_TOP_X)), (P.UPPER_TOP_X, 0)]):
        p.add('cupboard front', prism_y(c, c + P.BAL, poly), panel)
    p.add('stringer', fb.strut((c + .03, -(P.LOWER_BOTTOM_Y + .02), .23), (c + .03, -c, 1.23), .04, .04, sides=4), dark)
    p.add('stringer', fb.strut((c, -(c + .03), 1.23), (P.UPPER_TOP_X + .02, -(c + .03), 2.43), .04, .04, sides=4), dark)
    # newels, handrails, balusters
    for X, Y, z0, z1 in ((c + .03, P.LOWER_BOTTOM_Y + .03, 0, 1.12), (c + .03, c + .03, 0, 2.2), (P.UPPER_TOP_X + .03, c + .03, 1.6, 3.35)):
        p.add('newel', B(X - .045, X + .045, Y - .045, Y + .045, z0, z1), dark)
    p.add('handrail', fb.strut((c + .03, -(P.LOWER_BOTTOM_Y + .03), 1.1), (c + .03, -(c + .03), 2.1), .03, .03, sides=6), dark)
    p.add('handrail', fb.strut((c + .03, -(c + .03), 2.1), (P.UPPER_TOP_X + .03, -(c + .03), 3.3), .03, .03, sides=6), dark)
    for i in range(5):
        y = P.LOWER_BOTTOM_Y - (i + .5) * G
        zt = nl(y)
        p.add('baluster', fb.strut((c + .03, -y, zt + .02), (c + .03, -y, zt + .88), .015, .012, sides=6), panel)
        x = c + (i + .5) * G
        zt = nu(x)
        p.add('baluster', fb.strut((x, -(c + .03), zt + .02), (x, -(c + .03), zt + .88), .015, .012, sides=6), panel)
    return p.finish()


# ============================================================================ the new bedroom pieces (stand-ins)


def bed():
    p = fb.Prop('Bed (stand-in)')
    wood, quilt, sheet = gh.M('CC_Wood_Espresso'), mat('Mock quilt', '#8FA2C4', 0.8), gh.M('GH_Lace')
    # along its local y: the head at +y, the foot at -y (its front)
    for sx in (-1, 1):
        for sy, h in ((-1, .45), (1, 1.05)):
            p.add('post', fb.rbox(sx * .70 - .04, sx * .70 + .04, sy * 1.0 - .04, sy * 1.0 + .04, 0, h, bevel=.01), wood)
    p.add('headboard', fb.rbox(-.68, .68, .95, 1.0, .35, .98, bevel=.012), wood)
    p.add('footboard', fb.rbox(-.68, .68, -1.0, -.95, .25, .43, bevel=.01), wood)
    p.add('frame', fb.rbox(-.68, .68, -.97, .97, .25, .36, bevel=.01), wood)
    p.add('mattress', fb.rbox(-.67, .67, -.95, .95, .36, .55, bevel=.04, segments=2), sheet)
    p.add('quilt', fb.rbox(-.70, .70, -.97, .45, .50, .60, bevel=.04, segments=2), quilt)
    for sx in (-.33, .33):
        p.add('pillow', fb.ellipsoid((sx, .72, .60), (.28, .16, .07), u=10, v=5), sheet)
    return p.finish()


def small_box(name, w, d, h, m, top=None):
    p = fb.Prop(name)
    p.add('body', fb.rbox(-w / 2, w / 2, -d / 2, d / 2, .05, h, bevel=.01), m)
    p.add('plinth', fb.zbox(-w / 2 + .03, w / 2 - .03, -d / 2 + .03, d / 2 - .03, 0, .06), gh.M('CC_Dark_Kick'))
    if top:
        p.add('top', fb.rbox(-w / 2 - .01, w / 2 + .01, -d / 2 - .01, d / 2 + .01, h, h + .03, bevel=.005), top)
    p.add('front line', fb.zbox(-w / 2 + .03, w / 2 - .03, -d / 2 - .006, -d / 2, h * .55, h * .55 + .012), gh.M('CC_Dark_Kick'))
    return p.finish()


def wardrobe():
    p = fb.Prop('Wardrobe (stand-in)')
    m = gh.M('CC_Wood_Counter')
    p.add('body', fb.rbox(-.475, .475, -.30, .30, .08, 1.92, bevel=.012), m)
    p.add('plinth', fb.zbox(-.45, .45, -.27, .27, 0, .09), gh.M('CC_Dark_Kick'))
    p.add('cornice', fb.rbox(-.49, .49, -.32, .31, 1.92, 1.98, bevel=.008), gh.M('CC_Wood_Espresso'))
    p.add('split', fb.zbox(-.006, .006, -.306, -.30, .15, 1.85), gh.M('CC_Wood_Espresso'))
    for x in (-.05, .05):
        p.add('knob', fb.ellipsoid((x, -.32, 1.0), (.016, .016, .016), u=6, v=4), gh.M('T2_Brass'))
    return p.finish()


def dressing_table():
    p = fb.Prop('Dressing table (stand-in)')
    m = gh.M('CC_Wood_Counter')
    p.add('top', fb.rbox(-.55, .55, -.22, .22, .72, .76, bevel=.006), m)
    for sx in (-1, 1):
        p.add('drawers', fb.rbox(sx * .55 - sx * .16 - .16, sx * .55 - sx * .16 + .16, -.2, .2, .06, .72, bevel=.008), m)
    p.add('mirror frame', fb.rbox(-.34, .34, .15, .19, .76, 1.45, bevel=.01), gh.M('CC_Wood_Espresso'))
    p.add('mirror', fb.zbox(-.30, .30, .145, .15, .80, 1.41), mat('Mock mirror', '#C9D6DE', 0.05))
    for x, h in ((-.40, .20), (.42, .16), (.30, .24)):
        p.add('photo', fb.rbox(x - .06, x + .06, -.05, -.03, .76, .76 + h, bevel=.004), gh.M('T2_Brass'))
    return p.finish()


# ============================================================================ people (stand-ins)


def person(name, colour, seated=False, lying=False):
    p = fb.Prop(name, floor=False)
    body, skin, hair = mat(name + ' body', colour, 0.8), mat('Mock skin', '#E8BE9A', 0.6), mat(name + ' hair', '#D7D3CC', 0.7)
    if lying:
        p.add('head', fb.ellipsoid((0, .68, .66), (.10, .11, .10), u=10, v=6), skin)
        p.add('hair', fb.ellipsoid((0, .73, .69), (.11, .09, .09), u=10, v=6), hair)
        p.add('hump', fb.ellipsoid((0, -.05, .60), (.30, .70, .13), u=12, v=6), mat('Mock quilt', '#8FA2C4', 0.8))
        return p.finish()
    if seated:
        p.add('legs', fb.rbox(-.16, .16, -.45, -.05, .42, .56, bevel=.05, segments=2), body)
        p.add('shins', fb.rbox(-.16, .16, -.50, -.38, .04, .46, bevel=.04, segments=2), body)
        p.add('torso', fb.ellipsoid((0, .02, .86), (.20, .14, .30), u=10, v=6), body)
        p.add('head', fb.ellipsoid((0, .0, 1.27), (.10, .11, .12), u=10, v=6), skin)
        p.add('hair', fb.ellipsoid((0, .03, 1.31), (.11, .11, .10), u=10, v=6), hair)
        return p.finish()
    p.add('legs', fb.rbox(-.15, .15, -.10, .10, 0, .85, bevel=.05, segments=2), body)
    p.add('torso', fb.ellipsoid((0, 0, 1.2), (.22, .14, .36), u=10, v=6), body)
    p.add('head', fb.ellipsoid((0, 0, 1.65), (.10, .11, .12), u=10, v=6), skin)
    return p.finish()


def ace_ring(X, Y, z=0.0):
    """Ace: a person-sized stand-in body inside the 1.0 m capsule (faint), and the 1.16 m ring that has to fit."""
    fig = person('Ace stand-in', '#2F7D6D')
    fig.location = (X, -Y, z)
    p = fb.Prop('Ace capsule', floor=False)
    p.add('capsule', fb.cylinder((0, 0, 1.0), P.ACE_RADIUS, 2.0, sides=24), mat('Mock Ace capsule', '#2F9E86', 0.4, alpha=0.18))
    ring = fb.bmesh.new()
    r0, r1 = P.ACE_RADIUS + P.ACE_SKIN - .02, P.ACE_RADIUS + P.ACE_SKIN
    n = 40
    outer = [ring.verts.new((r1 * math.cos(2 * math.pi * k / n), r1 * math.sin(2 * math.pi * k / n), .012)) for k in range(n)]
    inner = [ring.verts.new((r0 * math.cos(2 * math.pi * k / n), r0 * math.sin(2 * math.pi * k / n), .012)) for k in range(n)]
    for k in range(n):
        m = (k + 1) % n
        ring.faces.new([outer[k], outer[m], inner[m], inner[k]])
    p.add('ring', ring, mat('Mock Ace ring', '#1F6F5E', 0.5))
    ob = p.finish()
    ob.location = (X, -Y, z)
    return fig, ob


# ============================================================================ the scene


def place(ob, X, Y, z, turn):
    ob.location = (X, -Y, z)
    ob.rotation_euler = (0, 0, math.radians(turn))


def build(cut_sides, show_first=True, show_ground=True, people=True, people_ground=True, inner_cut=False):
    fb.clear_scene()
    results = {r['object'].name: r['object'] for r in gh.build_all()}
    for ob in results.values():
        ob.hide_render = True
    if show_ground:
        ground_shell(cut_sides)
        l_stairs()
        on_wall = {'GH_Frame_XL': 'south', 'GH_Frame_L': 'north'}
        for name, (X, Y, z, turn) in P.GROUND.items():
            ob = results[name]
            if on_wall.get(name) in cut_sides:
                continue
            ob.hide_render = False
            if name == 'GH_KitchenCounter':
                ob.scale = (2.0 / 2.6, 1, 1)                 # stands in for the 2.0 m counter
            place(ob, X, Y, z, turn)
        door = results['GH_UnderStairsDoor']
        door.hide_render = False
        d0, d1 = P.CUPBOARD_DOOR
        place(door, d0 + .003, P.CLEAR + P.BAL + .003, 0, -90)
        door.scale = (1, (d1 - d0) / .644, 1)
        if people and people_ground:
            g = person('Grace stand-in', '#7C5A8C', seated=True)
            place(g, 4.02, 2.83, 0, 90)
    if show_first:
        first_shell(cut_sides, inner_cut)
        for name, (X, Y, z, turn) in P.FIRST.items():
            if name == 'Bed':
                ob = bed()
            elif name.startswith('Bedside table'):
                ob = small_box(name, .43 if 'back' in name else .40, .45, .55, gh.M('CC_Wood_Counter'), gh.M('CC_Wood_Espresso'))
            elif name == 'Bedside lamp':
                ob = results['GH_StandardLamp'].copy()
                bpy.context.scene.collection.objects.link(ob)
                ob.hide_render = False
                ob.scale = (.45, .45, .30)
            elif name == 'Wardrobe':
                ob = wardrobe()
            elif name == 'Dressing table':
                ob = dressing_table()
            elif name == 'Bedroom rug':
                ob = results['GH_Rug'].copy()
                bpy.context.scene.collection.objects.link(ob)
                ob.hide_render = False
            place(ob, X, Y, P.FLOOR1 + z, turn)
        # the bathroom's door, closed, in its frame
        fr = results['GH_InteriorDoor_Frame']
        fr.hide_render = False
        place(fr, 4.05, sum(P.BATHROOM_DOOR_Y) / 2, P.FLOOR1, 90)
        lf = results['GH_InteriorDoor_Leaf']
        lf.hide_render = False
        place(lf, 4.05, P.BATHROOM_DOOR_Y[1] - .01, P.FLOOR1, 90)
        # the photos on the bedroom walls (her secret)
        frames = ((1.61, 2.55, 1.2, 90), (4.3, 1.61, 1.35, 0), (4.7, 1.61, 1.2, 0))
        if inner_cut:
            frames = ((1.61, 2.55, .45, 90), (4.3, 1.61, .45, 0), (4.72, 1.61, .40, 0))
        for i, (X, Y, zz, turn) in enumerate(frames):
            f = results['GH_Frame_L' if i % 2 == 0 else 'GH_Frame_XL'].copy()
            bpy.context.scene.collection.objects.link(f)
            f.hide_render = False
            place(f, X, Y, P.FLOOR1 + zz, turn)
        if people:
            g = person('Grace asleep', '#7C5A8C', lying=True)
            place(g, 4.42, 2.90, P.FLOOR1, -90)


def pavement():
    g = fb.Prop('Pavement', floor=False)
    g.add('pavement', fb.plane(22, 18, centre=(2.7, -2.5, -0.065)), mat('Mock pavement', '#8E8B84', 0.9))
    g.add('stoop', B(P.STOOP[0], P.STOOP[1], P.D + T, P.D + T + .9, -0.06, 0), mat('Mock stone', '#B9B2A6', 0.8))
    return g.finish()


def night_lights():
    w = bpy.data.worlds.get('Review') or bpy.data.worlds.new('Review')
    bpy.context.scene.world = w
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    bg.inputs['Color'].default_value = (*fb.rgb('#1B2233'), 1)
    bg.inputs['Strength'].default_value = 0.30
    for name in ('Key', 'Fill'):
        ob = bpy.data.objects.get(name)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)
    moon = bpy.data.lights.new('Moon', 'SUN')
    moon.energy = 0.22
    moon.color = fb.rgb('#9DB4FF')
    mo = bpy.data.objects.new('Moon', moon)
    bpy.context.scene.collection.objects.link(mo)
    mo.rotation_euler = (math.radians(55), 0, math.radians(-30))
    lamp = bpy.data.lights.new('Standard lamp', 'POINT')
    lamp.energy = 70
    lamp.color = fb.rgb('#FFC98A')
    lamp.shadow_soft_size = 0.12
    lo = bpy.data.objects.new('Standard lamp', lamp)
    bpy.context.scene.collection.objects.link(lo)
    lo.location = (3.25, -3.55, 1.36)
    tv = bpy.data.lights.new('TV glow', 'AREA')
    tv.energy = 45
    tv.color = fb.rgb('#9FC2FF')
    tv.size = 0.45
    to = bpy.data.objects.new('TV glow', tv)
    bpy.context.scene.collection.objects.link(to)
    to.location = (4.85, -2.83, 0.80)
    to.rotation_euler = (0, math.radians(-90), 0)
    for mat_name, colour, strength in (('GH_Lampshade', '#FFD49A', 4.0), ('GH_TV_Screen', '#9FC2FF', 3.0)):
        m = bpy.data.materials.get(mat_name)
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        bsdf.inputs['Emission Color'].default_value = (*fb.rgb(colour), 1)
        bsdf.inputs['Emission Strength'].default_value = strength


def shoot(out, name, target, dist, el, az, lens=30, res=(1600, 1000), samples=40):
    cam = fb.camera('Cam ' + name, fb.look_from(target, dist, el, az), target, lens=lens)
    fb.render(os.path.join(out, name + '.png'), cam, res=res, samples=samples)


if __name__ == '__main__':
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(HERE, 'out')
    which = sys.argv[sys.argv.index('--') + 2] if '--' in sys.argv and len(sys.argv) > sys.argv.index('--') + 2 else 'all'
    FRONT = -38        # inside her house the camera looks in from the street (front-south), the front cut away
    if which in ('all', 'ground'):
        build({'street', 'south'}, show_first=False)
        pavement()
        for spot in ((1.11, 3.35), (2.95, 1.30)):
            ace_ring(*spot)
        fb.studio(strength=1.0)
        shoot(out, 'grace-v2-ground-floor', (2.75, -1.9, 0.5), 10.4, 50, FRONT)
    if which in ('all', 'first'):
        build({'street', 'south', 'back', 'north'}, show_first=True, people_ground=False, inner_cut=True)
        pavement()
        for spot in ((3.25, 0.72), (1.36, 3.62)):
            ace_ring(spot[0], spot[1], P.FLOOR1)
        fb.studio(strength=1.0)
        shoot(out, 'grace-v2-first-floor', (2.75, -2.2, 2.9), 10.4, 52, FRONT)
    if which in ('all', 'night'):
        build({'street', 'south'}, show_first=False)
        pavement()
        ace_ring(2.08, 2.05)
        night_lights()
        shoot(out, 'grace-v2-ground-floor-at-night', (2.75, -1.9, 0.5), 10.4, 50, FRONT, samples=64)
    if which in ('all', 'from-the-sw'):
        build({'south', 'back'}, show_first=False)
        pavement()
        ace_ring(2.08, 2.05)
        fb.studio(strength=1.0)
        shoot(out, 'grace-v2-from-the-usual-side', (2.7, -2.0, 0.6), 10.5, 47, -128, res=(1200, 750), samples=24)
    print('done')
