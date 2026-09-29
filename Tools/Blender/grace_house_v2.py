"""
Grace's house, layout v2 (claude/break-ins-spec.md section 4): the pieces a 1.0 m wide Ace needs, built with
Python in Blender 5.2 beside grace_house.py (step 4's pieces, left as they are).

  - the quarter-turn stairs, 1.40 m between the wall and the banister, with the cupboard under them;
  - the bedroom: a double bed (and two quilts: made, and slept in), bedside tables, a bedside lamp, the
    wardrobe, the dressing table with its mirror;
  - the bedroom's pair of doors (0.70 m leaves) and their 1.40 m frame;
  - the kitchen counter shortened to 2.0 m (its middle drawer unit left out).

The same rules as step 4: real sizes in metres, flat-shaded, one-segment bevels, junctions from boundaries,
no floating parts, no two faces in one plane. Fronts face -Y (+Z in Unity). Origins at the bottom centre,
except where a piece says otherwise:
  - the stairs: the inside corner of the back and south walls, on the floor, in plan coordinates
    (x = plan X, north; y = -plan Y, so the street is at -y; see plan_v2.py). Placed at the plan's origin
    with no turn, they sit where the plan says;
  - the door leaf: on its hinge line at the floor, the leaf running +X from it;
  - the door frame: the bottom centre of the opening, on the wall's centre line.

Nothing here is canon: Grace's taste is a placeholder for Mansoor to change.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
import plan_v2 as P  # noqa: E402

# New materials for v2 (sRGB hex, Unity smoothness), made in Unity by the furniture step like the others.
V2_MATERIALS = {
    'GH_Quilt': ('#8A9DC0', 0.10),     # a faded cornflower quilt
    'GH_Mirror': ('#C9D6DE', 0.95),
}
gh.GH_MATERIALS.update(V2_MATERIALS)
M = gh.M


def B(x0, x1, Y0, Y1, z0, z1):
    """A box given in plan terms (X, Y) for a piece built in plan coordinates (y = -Y)."""
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


def prism_x(x0, x1, poly_Yz):
    """A slab across plan X (from x0 to x1) whose face is the polygon [(Y, z)]."""
    bm = fb.bmesh.new()
    a = [bm.verts.new((x0, -Y, z)) for Y, z in poly_Yz]
    b = [bm.verts.new((x1, -Y, z)) for Y, z in poly_Yz]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(poly_Yz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# ============================================================================= the stairs


def stairs_l():
    """
    The quarter-turn stairs in the back-south corner: 6 risers from the entry along the south wall to a
    landing 1.2 m up, then 6 along the back wall to the first floor (2.4 m). 1.40 m clear between the wall
    and the banister. Treads of 0.24 m with a 2 cm nosing, a red runner up the middle, white risers.
    Under the upper flight and the landing: the cupboard, its front toward the pocket with the doorway for
    GH_UnderStairsDoor (0.65 x 1.14 m). The lower flight's side is closed down to the floor.
    Built in plan coordinates; origin at the inside corner of the back and south walls, on the floor.
    """
    p = fb.Prop('GH_Stairs_L')
    c, R, G, N = P.CLEAR, P.RISE, P.GOING, 0.02
    bal = P.BAL
    tread, riser, runner = M('CC_Wood_Counter'), M('GH_Paint_White'), M('GH_Rug_Field')
    dark, white = M('CC_Wood_Espresso'), M('GH_Paint_White')
    rw0, rw1 = (c - .70) / 2, (c + .70) / 2           # the runner: 0.70 m, in the middle of the flight
    Yb = P.LOWER_BOTTOM_Y                              # 2.60
    Xt = P.UPPER_TOP_X                                 # 2.60

    # ---- the lower flight: risers at Y = 2.60 - 0.24 i, climbing toward the back (-Y)
    for i in range(6):
        Y = Yb - i * G
        z0, z1 = i * R, (i + 1) * R - .03
        p.add('riser', B(0, c, Y - .02, Y, z0, z1), riser)
        p.add('runner (riser)', B(rw0, rw1, Y, Y + .004, z0, z1), runner)
        if i < 5:
            top = (i + 1) * R
            p.add('tread', B(0, c, Y - G - .002, Y + N, top - .03, top), tread)
            p.add('runner', B(rw0, rw1, Y - G - .002, Y + N + .004, top, top + .004), runner)
    # ---- the landing, 1.2 m up; the runner turns across it
    p.add('landing', B(0, c, 0, c + N, 1.2 - .03, 1.2), tread)
    p.add('runner (landing)', B(rw0, rw1, rw0, c + N + .004, 1.2, 1.204), runner)
    p.add('runner (landing)', B(rw1, c + .004, rw0, rw1, 1.2, 1.204), runner)
    # ---- the upper flight: risers at X = 1.40 + 0.24 j, climbing toward the north (+X)
    for j in range(6):
        X = c + j * G
        z0, z1 = 1.2 + j * R, 1.2 + (j + 1) * R - .03
        p.add('riser', B(X, X + .02, 0, c, z0, z1), riser)
        p.add('runner (riser)', B(X - .004, X, rw0, rw1, z0, z1), runner)
        if j < 5:
            top = 1.2 + (j + 1) * R
            p.add('tread', B(X - N, X + G + .002, 0, c, top - .03, top), tread)
            p.add('runner', B(X - N - .004, X + G + .002, rw0, rw1, top, top + .004), runner)
    # the last step's nose: the first floor's edge takes over at X 2.60 (built in Unity)

    # ---- the lines of the nosings: height at a point along each flight
    lower_line = lambda Y: R + (Yb + N - Y) * R / G                 # 0.2 at the first nosing, 1.2 at the landing's
    upper_line = lambda X: 1.2 + R + (X - (c - N)) * R / G          # 1.4 at the first nosing, 2.4 at the top

    # ---- the wall stringers (skirting boards up each flight, against the walls)
    # a band 0.08 above and 0.30 below the line of the nosings, meeting the floor at the bottom
    sk = .03
    y_foot = Yb + N - (.30 - R) * G / R          # where the band's lower edge meets the floor
    p.add('wall stringer', prism_x(-.001, sk, [(Yb + N, 0), (Yb + N, lower_line(Yb + N) + .08),
                                              (c - .03, lower_line(c - .03) + .08), (c - .03, lower_line(c - .03) - .30), (y_foot, 0)]), dark)
    xa, xb = c - .05, Xt - .02
    p.add('wall stringer', prism_y(-.001, sk, [(xa, upper_line(xa) - .30), (xa, upper_line(xa) + .08),
                                              (xb, upper_line(xb) + .08), (xb, upper_line(xb) - .30)]), dark)

    # ---- the open side of the lower flight: a closed panel down to the floor, a stringer, the banister
    side = [(Yb + N, 0), (Yb + N, .17), (c, lower_line(c) - .03), (c, 0)]
    p.add('side panel', prism_x(c + .005, c + bal - .005, side), white)
    band = [(Yb + N + .005, .11), (Yb + N + .005, .23), (c - .005, lower_line(c - .005) + .07), (c - .005, lower_line(c - .005) - .05)]
    p.add('open stringer', prism_x(c, c + bal, band), dark)
    # ---- the open side of the upper flight: the cupboard front, a stringer, the banister
    d0, d1 = P.CUPBOARD_DOOR
    dh = 1.14
    top_at = lambda X: upper_line(X) - .03
    for poly in ([(c, 0), (c, top_at(c)), (d0, top_at(d0)), (d0, 0)],
                 [(d0, dh), (d0, top_at(d0)), (d1, top_at(d1)), (d1, dh)],
                 [(d1, 0), (d1, top_at(d1)), (Xt, top_at(Xt)), (Xt, 0)]):
        p.add('cupboard front', prism_y(c + .005, c + bal - .005, poly), white)
    band = [(c + .005, top_at(c + .005) - .06), (c + .005, top_at(c + .005) + .07), (Xt, top_at(Xt) + .07), (Xt, top_at(Xt) - .06)]
    p.add('open stringer', prism_y(c, c + bal, band), dark)
    # the cupboard doorway's trim, a little proud of the panel on the pocket side
    for x0, x1 in ((d0 - .045, d0), (d1, d1 + .045)):
        p.add('doorway trim', B(x0, x1, c + bal - .004, c + bal + .012, 0, dh + .045), white)
    # the head over them, a little wider, taller and further out (no faces in the same plane)
    p.add('doorway trim head', B(d0 - .05, d1 + .05, c + bal - .004, c + bal + .016, dh - .004, dh + .05), white)
    # the cupboard's end, under the top of the upper flight: it is the kitchen's wall there
    p.add('cupboard end', B(Xt - .03, Xt, 0, c + bal, 0, 2.12), white)

    # ---- the banisters: newels, handrails 0.90 m above the nosings, a baluster on each tread
    nb = (c + bal / 2, Yb + .03)          # bottom newel (plan X, Y)
    nc = (c + bal / 2, c + bal / 2)       # the corner newel, on the floor, holding the landing's corner
    nt = (Xt + .01, c + bal / 2)          # the top newel, at the first floor's edge
    hw = .045
    p.add('newel', B(nb[0] - hw, nb[0] + hw, nb[1] - hw, nb[1] + hw, 0, 1.10), dark)
    p.add('newel cap', fb.ellipsoid((nb[0], -nb[1], 1.14), (.052, .052, .045), u=8, v=5), dark)
    p.add('newel', B(nc[0] - hw, nc[0] + hw, nc[1] - hw, nc[1] + hw, 0, 2.46), dark)       # tall enough for the upper rail
    p.add('newel cap', fb.ellipsoid((nc[0], -nc[1], 2.50), (.052, .052, .045), u=8, v=5), dark)
    t_base = top_at(nt[0]) - .25
    p.add('newel', B(nt[0] - hw, nt[0] + hw, nt[1] - hw, nt[1] + hw, t_base, 3.32), dark)
    p.add('newel cap', fb.ellipsoid((nt[0], -nt[1], 3.36), (.052, .052, .045), u=8, v=5), dark)
    rail_lower_a = (nb[0], -(nb[1] - .05), lower_line(nb[1] - .05) + .90)
    rail_lower_b = (nc[0], -(nc[1] + .05), lower_line(nc[1] + .05) + .90)
    p.add('handrail', fb.strut(rail_lower_b, rail_lower_a, .03, .03, sides=6), dark)
    rail_upper_a = (nc[0] + .05, -nc[1], upper_line(nc[0] + .05) + .90)
    rail_upper_b = (nt[0] - .05, -nt[1], upper_line(nt[0] - .05) + .90)
    p.add('handrail', fb.strut(rail_upper_b, rail_upper_a, .03, .03, sides=6), dark)
    for i in range(5):
        Y = Yb - i * G - G / 2
        base = lower_line(Y) + .05
        p.add('baluster', fb.strut((nb[0], -Y, lower_line(Y) + .88), (nb[0], -Y, base), .012, .015, sides=6), white)
        X = c + i * G + G / 2
        base = upper_line(X) + .05
        p.add('baluster', fb.strut((X, -nc[1], upper_line(X) + .88), (X, -nc[1], base), .012, .015, sides=6), white)
    return p


# ============================================================================= doors


def door_frame(name, ow, oh, depth):
    """A doorway for an inside wall: lining and architraves both sides (GH_InteriorDoor_Frame, any size).
    Origin: bottom centre of the opening, on the wall's centre line."""
    p = fb.Prop(name)
    white = M('GH_Paint_White')
    for x0, x1 in ((-ow / 2 - .025, -ow / 2), (ow / 2, ow / 2 + .025)):
        p.add('lining', fb.zbox(x0, x1, -depth / 2, depth / 2, 0, oh), white)
    p.add('lining head', fb.zbox(-ow / 2 - .025, ow / 2 + .025, -depth / 2, depth / 2, oh, oh + .025), white)
    for y0, y1, proud in ((-depth / 2 - .015, -depth / 2 + .002, -.004), (depth / 2 - .002, depth / 2 + .015, .004)):
        for x0, x1 in ((-ow / 2 - .07, -ow / 2 + .005), (ow / 2 - .005, ow / 2 + .07)):
            p.add('architrave', fb.rbox(x0, x1, y0, y1, 0, oh + .06, bevel=.004), white)
        hy0, hy1 = sorted((y0 + proud, y1 + proud))
        p.add('architrave head', fb.rbox(-ow / 2 - .078, ow / 2 + .078, hy0, hy1, oh - .005, oh + .078, bevel=.004), white)
    p.add('door stop', fb.zbox(-ow / 2, ow / 2, .015, .03, oh - .012, oh), white)
    return p


def door_leaf(name, w, h, t=.04):
    """A four-panel door leaf, knobs on both faces near its free edge. Origin on its hinge line at the floor,
    the leaf running +X from it."""
    p = fb.Prop(name)
    white = M('GH_Paint_White')
    p.add('leaf', fb.rbox(0, w, -t / 2, t / 2, 0, h, bevel=.004), white)
    rail = .08 if w < .75 else .09
    for side in (-1, 1):
        y0, y1 = sorted((side * t / 2, side * (t / 2 + .006)))
        for x0, x1 in ((rail, w / 2 - .03), (w / 2 + .03, w - rail)):
            for z0, z1 in ((.12, .82), (1.0, h - .12)):
                p.add('panel', fb.zbox(x0, x1, y0, y1, z0, z1), white)
        p.add('knob', fb.ellipsoid((w - .06, side * (t / 2 + .045), .98), (.026, .018, .026), u=8, v=5), M('T2_Brass'))
        p.add('knob neck', fb.cylinder((w - .06, side * (t / 2 + .02), .98), .012, .05, sides=6, rot=(90, 0, 0)), M('T2_Brass'))
        p.add('rose', fb.cylinder((w - .06, side * (t / 2 + .002), .98), .03, .006, sides=10, rot=(90, 0, 0)), M('T2_Brass'))
    for z in (.25, h / 2 + .1, h - .25):
        p.add('hinge', fb.zbox(-.004, .012, -.02, .02, z - .05, z + .05), M('DC_SteelDark'))
    return p


BEDROOM_OPENING = (1.40, 2.20)
BEDROOM_LEAF = (.695, 2.19)          # two of them meet in the middle with a 1 cm gap


# ============================================================================= the bedroom


# A 4ft6 double (1.35 x 1.90), not a 1.40 x 2.00: in the room as built, a 1.16 m Ace only gets from the
# bedroom's doorway past the foot of the bed if the foot is at least 3.40 m along (see break-ins-spec section 4).
BED_W, BED_L, MATTRESS_TOP = 1.35, 1.90, .55


def bed():
    """Her bed: a 1.35 x 1.90 double in dark wood, headboard at +Y (the back of the piece), footboard at the
    front. The mattress top is at 0.55 m, two pillows at the head. The quilt is a piece of its own
    (GH_Quilt_Made, or GH_Quilt_Asleep while she sleeps)."""
    p = fb.Prop('GH_Bed')
    dark, sheet = M('CC_Wood_Espresso'), M('GH_Lace')
    hw, hl = BED_W / 2, BED_L / 2
    for sx in (-1, 1):
        for sy, h in ((-1, .48), (1, 1.06)):
            x0, x1 = sorted((sx * (hw - .02), sx * (hw + .05)))
            y0, y1 = sorted((sy * (hl - .02), sy * (hl + .05)))
            p.add('post', fb.rbox(x0, x1, y0, y1, 0, h, bevel=.012), dark)
            p.add('post finial', fb.ellipsoid(((x0 + x1) / 2, (y0 + y1) / 2, h + .018), (.042, .042, .03), u=8, v=4), dark)
    # headboard: a panel between the head posts, a rail across the top; footboard the same, lower
    p.add('headboard', fb.rbox(-hw + .015, hw - .015, hl - .005, hl + .025, .30, .92, bevel=.01), dark)
    p.add('headboard rail', fb.rbox(-hw + .01, hw - .01, hl - .015, hl + .035, .92, .98, bevel=.008), dark)
    p.add('footboard', fb.rbox(-hw + .015, hw - .015, -hl - .025, -hl + .005, .22, .42, bevel=.01), dark)
    p.add('footboard rail', fb.rbox(-hw + .01, hw - .01, -hl - .035, -hl + .015, .42, .46, bevel=.008), dark)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (hw - .025), sx * (hw + .005)))
        p.add('side rail', fb.zbox(x0, x1, -hl + .01, hl - .01, .22, .34), dark)
    p.add('base', fb.zbox(-hw + .02, hw - .02, -hl + .02, hl - .02, .30, .34), dark)
    p.add('mattress', fb.rbox(-hw + .01, hw - .01, -hl + .03, hl - .03, .335, MATTRESS_TOP, bevel=.035, segments=2), sheet)
    for sx in (-.34, .34):
        p.add('pillow', fb.ellipsoid((sx, hl - .26, MATTRESS_TOP + .06), (.30, .17, .075), u=10, v=5), sheet)
    return p


def quilt_made():
    """The quilt, made: flat over the bed from the foot to under the pillows, hanging over the sides, its top
    folded back to show the sheet. Origin as the bed's (bottom centre of the bed)."""
    p = fb.Prop('GH_Quilt_Made')
    q, sheet = M('GH_Quilt'), M('GH_Lace')
    hw, hl = BED_W / 2, BED_L / 2
    top = MATTRESS_TOP + .03
    p.add('quilt', fb.rbox(-hw + .005, hw - .005, -hl + .02, hl - .52, MATTRESS_TOP - .004, top, bevel=.012), q)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (hw - .012), sx * (hw + .03)))
        p.add('overhang', fb.rbox(x0, x1, -hl + .02, hl - .52, .30, top, bevel=.01), q)
    p.add('overhang (foot)', fb.rbox(-hw + .005, hw - .005, -hl, -hl + .03, .36, top, bevel=.01), q)
    p.add('turn-back', fb.rbox(-hw + .005, hw - .005, hl - .60, hl - .50, top - .006, top + .018, bevel=.008), sheet)
    # a few quilting lines across it
    for i in range(1, 5):
        y = -hl + .02 + i * (BED_L - .54) / 5
        p.add('quilting', fb.zbox(-hw + .04, hw - .04, y - .006, y + .006, top - .001, top + .003), M('GH_Lace'))
    return p


def quilt_asleep():
    """The quilt with her asleep under it: pulled up to her shoulders on the left half of the bed (seen from the
    foot), the shape of someone lying on their side, the other half flat. Her head goes on the left pillow
    (the stand-in body's head, or nothing until she has a sleeping pose). Origin as the bed's."""
    p = fb.Prop('GH_Quilt_Asleep')
    q, sheet = M('GH_Quilt'), M('GH_Lace')
    hw, hl = BED_W / 2, BED_L / 2
    top = MATTRESS_TOP + .03
    p.add('quilt', fb.rbox(-hw + .005, hw - .005, -hl + .02, hl - .40, MATTRESS_TOP - .004, top, bevel=.012), q)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (hw - .012), sx * (hw + .03)))
        p.add('overhang', fb.rbox(x0, x1, -hl + .02, hl - .40, .30, top, bevel=.01), q)
    p.add('overhang (foot)', fb.rbox(-hw + .005, hw - .005, -hl, -hl + .03, .36, top, bevel=.01), q)
    # the sleeper's shape under the quilt: one long mound from the feet to the shoulders, lofted along the
    # body (feet, shins, knees, hips, waist, chest, shoulders) and laid down along the bed
    sx = -.34
    body = [(0.00, .10, .05), (0.27, .13, .07), (0.50, .16, .09), (0.86, .23, .13), (1.04, .20, .11),
            (1.26, .23, .12), (1.40, .21, .10), (1.46, .12, .05)]
    length = 1.40                                                    # feet to shoulders, in the 1.90 m bed
    rings = [(0, 0, z * length / 1.46, rx, rz) for z, rx, rz in body]
    mound = fb.loft(rings, sides=12)
    mound = fb.turn(mound, -90, 'X', (0, 0, 0))                      # the body's length now runs along +y
    mound = fb.move(mound, (sx, hl - .45 - length, top - .015))      # shoulders under the turn-back, feet in the bed
    p.add('shape', mound, q)
    p.add('turn-back', fb.rbox(-hw + .005, hw - .005, hl - .47, hl - .38, top - .006, top + .02, bevel=.008), sheet)
    return p


def bedside_table():
    """A bedside table: a drawer over an open shelf, four short splayed legs. 0.43 x 0.38 x 0.55."""
    p = fb.Prop('GH_BedsideTable')
    wood, dark, inner = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Wood_Inner')
    gh.legs(p, [(-.17, -.14), (.17, -.14), (-.17, .14), (.17, .14)], .16, .02, .014, splay=.012)
    p.add('carcass', fb.rbox(-.20, .20, -.17, .17, .15, .53, bevel=.008), dark)
    p.add('top', fb.rbox(-.215, .215, -.19, .185, .52, .55, bevel=.01), wood)
    p.add('shelf', fb.zbox(-.18, .18, -.172, .15, .19, .21), inner)
    p.add('drawer', fb.rbox(-.18, .18, -.18, -.165, .38, .50, bevel=.006), inner)
    p.add('drawer knob', fb.ellipsoid((0, -.19, .44), (.014, .013, .014), u=6, v=4), M('T2_Brass'))
    return p


def bedside_lamp():
    """A bedside lamp: a ceramic gourd base, a brass neck, a pleated-look fabric shade. 0.42 m tall."""
    p = fb.Prop('GH_BedsideLamp')
    p.add('base', fb.lathe([(.07, 0), (.075, .012), (.095, .07), (.09, .13), (.05, .19), (.03, .21)], sides=12), M('DC_Ceramic'))
    p.add('neck', fb.cylinder((0, 0, .245), .012, .08, sides=6), M('T2_Brass'))
    shade = [(.13, .235), (.125, .245), (.09, .41), (.085, .42)]
    outer = fb.lathe(shade, sides=12, cap_bottom=False, cap_top=True)
    inner = fb.lathe([(r - .005, z + .004) for r, z in shade[1:3]], sides=12, cap_bottom=False, cap_top=False)
    for f in inner.faces:
        f.normal_flip()
    fb.merge(outer, inner)
    p.add('shade', outer, M('GH_Lampshade'))
    p.add('bulb', fb.ellipsoid((0, 0, .30), (.025, .025, .032), u=8, v=5), M('DC_Ceramic'))
    return p


def wardrobe():
    """Her wardrobe: two panelled doors, a cornice, a plinth, brass knobs. 0.95 x 0.58 x 1.98. A place to hide."""
    p = fb.Prop('GH_Wardrobe')
    wood, dark, inner = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Wood_Inner')
    p.add('plinth', fb.rbox(-.46, .46, -.27, .27, 0, .10, bevel=.006), dark)
    p.add('carcass', fb.rbox(-.475, .475, -.29, .29, .09, 1.90, bevel=.01), wood)
    p.add('cornice', fb.rbox(-.495, .495, -.31, .30, 1.89, 1.98, bevel=.012), dark)
    fy = -.29
    for x0, x1 in ((-.455, -.006), (.006, .455)):
        p.add('door', fb.zbox(x0, x1, fy - .016, fy + .002, .13, 1.86), wood)
        for z0, z1 in ((.20, .95), (1.03, 1.79)):
            p.add('door panel', fb.zbox(x0 + .06, x1 - .06, fy - .024, fy - .014, z0, z1), inner)
    for x in (-.05, .05):
        p.add('knob', fb.ellipsoid((x, fy - .03, 1.0), (.016, .015, .016), u=6, v=4), M('T2_Brass'))
    return p


def dressing_table():
    """A dressing table for the bay: two pedestals of drawers, a kneehole, a three-part mirror on the top.
    1.10 x 0.45, top at 0.76, mirror to 1.46. Her photos stand on it (placed in Unity)."""
    p = fb.Prop('GH_DressingTable')
    wood, dark, inner = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Wood_Inner')
    mirror, brass = M('GH_Mirror'), M('T2_Brass')
    for s in (-1, 1):
        x0, x1 = sorted((s * .20, s * .53))
        p.add('pedestal', fb.rbox(x0, x1, -.20, .20, .04, .73, bevel=.008), dark)
        p.add('pedestal foot', fb.zbox(x0 + .02, x1 - .02, -.18, .18, 0, .05), M('CC_Dark_Kick'))
        for z0, z1 in ((.08, .30), (.33, .51), (.54, .70)):
            p.add('drawer', fb.rbox(x0 + .02, x1 - .02, -.21, -.195, z0, z1, bevel=.005), inner)
            p.add('drawer knob', fb.ellipsoid(((x0 + x1) / 2, -.22, (z0 + z1) / 2), (.012, .011, .012), u=6, v=4), brass)
    p.add('back panel', fb.zbox(-.21, .21, .17, .19, .30, .73), dark)
    p.add('top', fb.rbox(-.55, .55, -.225, .225, .72, .76, bevel=.01), wood)
    # the mirror: a middle glass and two side wings angled in, in dark frames
    p.add('mirror frame', fb.rbox(-.25, .25, .10, .14, .76, 1.46, bevel=.01), dark)
    p.add('mirror', fb.zbox(-.21, .21, .095, .101, .80, 1.42), mirror)
    for s in (-1, 1):
        wing = fb.rbox(-.16, .16, -.02, .02, 0, .56, bevel=.008)
        glass = fb.zbox(-.13, .13, -.026, -.019, .04, .52)
        pivot = (s * .25, .12, .76)
        for bm, m in ((wing, dark), (glass, mirror)):
            bm = fb.move(bm, (s * .41, .12, .78))
            bm = fb.turn(bm, s * 35, 'Z', pivot)
            p.add('mirror wing', bm, m)
    return p


# ============================================================================= the kitchen


def kitchen_counter_short():
    """The counter along the back wall, 2.0 m: a cupboard (the cups sit on the worktop over it), the cooker
    with its hob, and the sink unit. Step 4's GH_KitchenCounter without its middle drawer unit. Worktop 0.92."""
    p = fb.Prop('GH_KitchenCounter_Short')
    cream, top = 'GH_Kitchen_Cream', M('CC_Wood_Counter')
    W, H, T = 2.00, .92, .04
    hw = W / 2
    fy = -.28
    p.add('kick', fb.zbox(-hw + .01, hw - .01, -.24, .30, 0, .105), M('CC_Dark_Kick'))
    p.add('carcass', fb.rbox(-hw, hw, fy, .31, .10, H - T + .004, bevel=.006), M(cream))
    # the units, west to east: cupboard 0.57, cooker 0.57, sink 0.78 (plus the carcass's ends)
    x = -hw + .015
    cup0, cup1 = x, x + .57
    cook0, cook1 = cup1 + .03, cup1 + .03 + .57
    sink0, sink1 = cook1 + .03, hw - .015
    hx0, hx1, hy0, hy1 = sink0 + .09, sink0 + .65, -.22, .19        # the sink's hole in the worktop
    p.add('worktop', fb.rbox(-hw - .005, hx0, -.31, .31, H - T, H, bevel=.004), top)
    for x0, x1, y0, y1 in ((hx1, hw + .005, -.31, .31), (hx0, hx1, -.31, hy0), (hx0, hx1, hy1, .31)):
        p.add('worktop', fb.zbox(x0, x1, y0, y1, H - T, H), top)
    p.add('upstand', fb.rbox(-hw, hw, .285, .31, H - .002, H + .10, bevel=.004), top)
    gh.shaker_door(p, cup0, cup1, .13, .64, fy, cream, 'knob')
    gh.shaker_door(p, cup0, cup1, .665, .855, fy, cream, 'bar', label='drawer')
    p.add('oven door', fb.rbox(cook0, cook1, fy - .03, fy + .002, .13, .66, bevel=.008), M('GH_Plastic_Dark'))
    p.add('oven window', fb.zbox(cook0 + .065, cook1 - .065, fy - .034, fy - .026, .27, .58), M('GH_Glass_Dark'))
    cx = (cook0 + cook1) / 2
    p.add('oven handle', fb.cylinder((cx, fy - .06, .625), .011, .46, sides=6, rot=(0, 90, 0)), M('DC_Steel'))
    for hx in (cook0 + .065, cook1 - .065):
        p.add('oven handle post', fb.zbox(hx - .008, hx + .008, fy - .062, fy - .026, .615, .635), M('DC_Steel'))
    p.add('controls', fb.zbox(cook0, cook1, fy - .025, fy + .002, .68, .86), M('DC_Steel'))
    for i in range(4):
        p.add('control knob', fb.cylinder((cook0 + .055 + i * .15, fy - .04, .77), .022, .03, sides=6, rot=(90, 0, 0)), M('GH_Plastic_Dark'))
    p.add('hob', fb.rbox(cook0 - .005, cook1 + .005, -.25, .22, H - .004, H + .006, bevel=.003), M('GH_Plastic_Dark'))
    for dx, y, r in ((.13, -.11, .075), (.41, -.11, .06), (.13, .10, .06), (.41, .10, .075)):
        p.add('ring', fb.cylinder((cook0 + dx + .02, y, H + .009), r, .008, sides=8), M('DC_SteelDark'))
    half = (sink1 - sink0) / 2
    gh.shaker_door(p, sink0, sink0 + half - .005, .13, .855, fy, cream, 'knob')
    gh.shaker_door(p, sink0 + half + .005, sink1, .13, .855, fy, cream, 'knob-left')
    p.add('sink bowl', fb.open_box(hx0 + .01, hx1 - .01, hy0 + .01, hy1 - .01, H - .20, H, 0, inward=True), M('DC_Steel'))
    for x0, x1, y0, y1 in ((hx0 - .015, hx1 + .015, hy0 - .015, hy0 + .012), (hx0 - .015, hx1 + .015, hy1 - .012, hy1 + .015),
                           (hx0 - .015, hx0 + .012, hy0, hy1), (hx1 - .012, hx1 + .015, hy0, hy1)):
        p.add('sink rim', fb.zbox(x0, x1, y0, y1, H - .002, H + .004), M('DC_Steel'))
    p.add('plug', fb.cylinder(((hx0 + hx1) / 2, (hy0 + hy1) / 2, H - .199), .025, .004, sides=8), M('DC_SteelDark'))
    tx = (hx0 + hx1) / 2
    p.add('tap', fb.cylinder((tx, .24, H + .12), .016, .24, sides=8), M('DC_Steel'))
    p.add('spout', fb.strut((tx, .06, H + .20), (tx, .245, H + .235), .012, .014, sides=6), M('DC_Steel'))
    p.add('spout end', fb.cylinder((tx, .06, H + .19), .012, .04, sides=6), M('DC_Steel'))
    for s in (-1, 1):
        p.add('tap handle', fb.cylinder((tx + s * .10, .24, H + .03), .018, .06, sides=6), M('DC_Steel'))
        p.add('tap cross', fb.rbox(tx + s * .10 - .03, tx + s * .10 + .03, .234, .246, H + .06, H + .072, bevel=.003), M('DC_Steel'))
    return p


# ============================================================================= the list


PIECES = [
    (stairs_l, 'Entry', "the quarter-turn stairs, 1.40 m flights, the cupboard under them"),
    (lambda: door_frame('GH_BedroomDoor_Frame', BEDROOM_OPENING[0], BEDROOM_OPENING[1], .10), 'Landing', "the bedroom's doorway, 1.40 x 2.20"),
    (lambda: door_leaf('GH_BedroomDoor_Leaf', *BEDROOM_LEAF), 'Landing', "one of the bedroom's pair of doors (0.70 m)"),
    (bed, 'Bedroom', "her bed, 1.35 x 1.90, mattress at 0.55"),
    (quilt_made, 'Bedroom', "the quilt, made"),
    (quilt_asleep, 'Bedroom', "the quilt with her asleep under it"),
    (bedside_table, 'Bedroom', "bedside table"),
    (bedside_lamp, 'Bedroom', "bedside lamp"),
    (wardrobe, 'Bedroom', "the wardrobe (a place to hide)"),
    (dressing_table, 'Bedroom', "the dressing table with its mirror (for the south bay)"),
    (kitchen_counter_short, 'Kitchen', "the counter, 2.0 m: cupboard, cooker, sink (worktop 0.92)"),
]


def build_all():
    out = []
    for builder, room, what in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'room': room, 'what': what, 'islands': n, 'floating': floating, **st})
    return out
