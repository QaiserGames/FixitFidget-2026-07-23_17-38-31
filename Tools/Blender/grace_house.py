"""
Grace's house, the ground floor: the furniture list of claude/break-ins-spec.md section 4, built with
Python in Blender 5.2 (fixit_blender.py beside it).

Every piece follows the café's rules (claude/art-style-guide.md, claude/furniture-library.md): real
sizes in metres, chunky and friendly, flat-shaded, bevels of one segment, junctions from boundaries,
parts overlapping 2-4 mm, and no floating parts (audit). Seats at 0.45 m. Origins at the bottom centre;
a piece hung on a wall has its origin at the bottom centre of its back (the back at y = 0). Fronts
face -Y (+Z in Unity).

Materials: the café's own where they fit (the woods, steel, brass, ceramic), and GH_ materials for
the house (GH_MATERIALS below: sRGB hex and a Unity smoothness). The Unity step creates the GH_ ones
and maps every FBX material to the Unity material of the same name.

Nothing here is canon: Grace's taste (teal velvet, a rose sofa, lace, a wood-cased TV) is a
placeholder for Mansoor to change.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402

# The café's materials (Assets/Art/Materials), with the colours the Blender renders use.
CAFE_MATERIALS = {
    'CC_Wood_Counter':  ('#8F5B32', 0.40),
    'CC_Wood_Espresso': ('#4A3422', 0.55),
    'CC_Wood_Inner':    ('#8A6746', 0.55),
    'CC_Dark_Kick':     ('#2A1E16', 0.80),
    'DC_Steel':         ('#93908A', 0.35),
    'DC_SteelDark':     ('#4E4B47', 0.45),
    'DC_Ceramic':       ('#F2EFE8', 0.30),
    'T2_Brass':         ('#B8913F', 0.30),
}

# Grace's house: (sRGB hex, Unity smoothness). The Unity step makes these as URP Lit materials.
GH_MATERIALS = {
    'GH_Velvet_Teal':    ('#2F5D5A', 0.25),
    'GH_Fabric_Rose':    ('#B7857C', 0.12),
    'GH_Pillow_Sage':    ('#8FA58B', 0.12),
    'GH_Pillow_Mustard': ('#C99A3E', 0.12),
    'GH_Lace':           ('#EFE7D6', 0.10),
    'GH_Rug_Field':      ('#7E3434', 0.05),
    'GH_Rug_Border':     ('#D8C39C', 0.05),
    'GH_Plastic_Dark':   ('#2B2926', 0.35),
    'GH_TV_Screen':      ('#1D2A2F', 0.80),
    'GH_Lampshade':      ('#E7D5B0', 0.10),
    'GH_Photo':          ('#BFA88A', 0.25),
    'GH_Kitchen_Cream':  ('#E6DCC3', 0.35),
    'GH_Fridge_Mint':    ('#BCD6C4', 0.55),
    'GH_Enamel_Red':     ('#B13A2F', 0.55),
    'GH_Delft_Blue':     ('#2F5B8F', 0.60),
    'GH_Wool_Camel':     ('#A57B52', 0.08),
    'GH_Scarf_Red':      ('#8E3130', 0.08),
    'GH_Paint_White':    ('#ECE6D8', 0.30),
    'GH_Cardboard':      ('#B78C5A', 0.05),
    'GH_Tape':           ('#CDB57E', 0.30),
    'GH_Glass_Dark':     ('#2A3438', 0.85),
}


def M(name):
    """The Blender material for a name in either table (made on first use)."""
    if name in CAFE_MATERIALS:
        hexstr, smooth = CAFE_MATERIALS[name]
    else:
        hexstr, smooth = GH_MATERIALS[name]
    metal = 0.6 if name in ('T2_Brass', 'DC_Steel') else 0.0
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=metal)


def legs(p, pts, h_top, r_top, r_bot, splay=0.02, mat='CC_Wood_Espresso', sides=6):
    """Tapered legs from (x, y) under a piece at h_top down to the floor, splayed outward."""
    for x, y in pts:
        out = Vector((x, y, 0.0))
        out = out.normalized() * splay if out.length > 0 else out
        p.add('leg', fb.strut((x, y, h_top), (x + out.x, y + out.y, 0.0), r_top, r_bot, sides=sides), M(mat))


# ============================================================================= the front room


def armchair():
    """Her armchair: a wingback in teal velvet, a lace cover over the top of the back. Seat 0.45."""
    p = fb.Prop('GH_Armchair')
    v = M('GH_Velvet_Teal')
    legs(p, [(-.31, -.31), (.31, -.31), (-.29, .29), (.29, .29)], 0.14, 0.024, 0.016)
    p.add('base', fb.rbox(-.39, .39, -.38, .37, .12, .36, bevel=.02), v)
    p.add('seat cushion', fb.rbox(-.28, .28, -.41, .22, .33, .45, bevel=.03), v)
    for s in (-1, 1):
        x0, x1 = sorted((s * .27, s * .40))
        p.add('arm', fb.rbox(x0, x1, -.41, .30, .12, .60, bevel=.03), v)
        p.add('arm roll', fb.cylinder((s * .335, -.06, .60), .068, .74, sides=8, rot=(90, 0, 0)), v)
    back = fb.rbox(-.31, .31, .20, .36, .30, 1.02, bevel=.04)
    p.add('back', fb.turn(back, -10, 'X', (0, .30, .33)), v)
    for s in (-1, 1):
        x0, x1 = sorted((s * .29, s * .393))
        wing = fb.rbox(x0, x1, .05, .34, .56, .98, bevel=.035)
        p.add('wing', fb.turn(wing, -10, 'X', (0, .30, .33)), v)
    lace = fb.rbox(-.17, .17, .165, .19, .74, .96, bevel=.004)
    p.add('lace cover', fb.turn(lace, -10, 'X', (0, .30, .33)), M('GH_Lace'))
    return p


def sofa():
    """The sofa under the front window: dusty rose, two seat and two back cushions, a pillow at each end."""
    p = fb.Prop('GH_Sofa')
    f = M('GH_Fabric_Rose')
    legs(p, [(-.80, -.36), (.80, -.36), (-.80, .36), (.80, .36)], 0.12, 0.025, 0.017)
    p.add('base', fb.rbox(-.86, .86, -.42, .41, .10, .34, bevel=.02), f)
    for x0, x1 in ((-.66, -.005), (.005, .66)):
        p.add('seat cushion', fb.rbox(x0, x1, -.45, .20, .31, .45, bevel=.035), f)
    pivot = (0, .40, .32)
    p.add('back', fb.turn(fb.rbox(-.855, .855, .24, .42, .30, .86, bevel=.04), -9, 'X', pivot), f)
    for x0, x1 in ((-.66, -.005), (.005, .66)):
        p.add('back cushion', fb.turn(fb.rbox(x0, x1, .10, .27, .43, .80, bevel=.045), -9, 'X', pivot), f)
    for s in (-1, 1):
        x0, x1 = sorted((s * .65, s * .875))
        p.add('arm', fb.rbox(x0, x1, -.45, .425, .10, .60, bevel=.035), f)
        p.add('arm roll', fb.cylinder((s * .76, -.015, .60), .062, .86, sides=8, rot=(90, 0, 0)), f)
    for s, mat in ((-1, 'GH_Pillow_Sage'), (1, 'GH_Pillow_Mustard')):
        pillow = fb.rbox(-.19, .19, -.07, .07, 0, .38, bevel=.05)
        pillow = fb.turn(pillow, s * -14, 'Y', (0, 0, 0))
        pillow = fb.turn(pillow, -18, 'X', (0, 0, 0))
        p.add('pillow', fb.move(pillow, (s * .47, .02, .43)), M(mat))
    return p


def tv():
    """A wood-cased television with a curved-glass screen, two knobs, a grille and rabbit ears."""
    p = fb.Prop('GH_TV')
    wood, dark = M('CC_Wood_Counter'), M('GH_Plastic_Dark')
    p.add('case', fb.rbox(-.31, .31, -.23, .14, 0, .50, bevel=.025), wood)
    p.add('tube housing', fb.taper_box(.52, .40, .30, .24, .13, .26, .25), wood)
    p.add('bezel', fb.rbox(-.29, .15, -.238, -.20, .04, .46, bevel=.012), dark)
    p.add('screen', screen_panel(-.26, .15, .075, .425, -.2385, -.248), M('GH_TV_Screen'))
    p.add('control panel', fb.rbox(.165, .29, -.238, -.20, .04, .46, bevel=.01), dark)
    for z in (.39, .31):
        p.add('knob', fb.cylinder((.228, -.25, z), .026, .03, sides=8, rot=(90, 0, 0)), M('DC_SteelDark'))
    for i in range(4):
        z = .09 + i * .03
        p.add('grille slot', fb.zbox(.18, .275, -.242, -.236, z, z + .012), M('CC_Dark_Kick'))
    p.add('aerial base', fb.ellipsoid((.06, .0, .50), (.055, .045, .03), u=8, v=4), dark)
    for s in (-1, 1):
        p.add('aerial', fb.strut((.06 + s * .16, .06, .80), (.06 + s * .01, .0, .52), .004, .006, sides=5), M('DC_Steel'))
        p.add('aerial tip', fb.ellipsoid((.06 + s * .16, .06, .80), (.009, .009, .009), u=5, v=3), M('DC_Steel'))
    return p


def screen_panel(x0, x1, z0, z1, y_front, y_back):
    """A screen that bulges a little in the middle (the curved glass of an old set)."""
    bm = fb.bmesh.new()
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    outer = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    vo = [bm.verts.new((x, y_back, z)) for x, z in outer]
    vf = [bm.verts.new((x, y_front, z)) for x, z in outer]
    mid = bm.verts.new((cx, y_front - .012, cz))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([vo[i], vo[j], vf[j], vf[i]])
        bm.faces.new([vf[i], vf[j], mid])
    bm.faces.new(list(reversed(vo)))
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def tv_cabinet():
    """The low cabinet the television stands on: two doors, brass knobs, short legs."""
    p = fb.Prop('GH_TVCabinet')
    legs(p, [(-.45, -.18), (.45, -.18), (-.45, .18), (.45, .18)], 0.09, 0.022, 0.016, splay=.012)
    p.add('carcass', fb.rbox(-.50, .50, -.22, .22, .08, .465, bevel=.008), M('CC_Wood_Espresso'))
    p.add('top', fb.rbox(-.525, .525, -.24, .235, .46, .50, bevel=.012), M('CC_Wood_Counter'))
    for x0, x1 in ((-.48, -.008), (.008, .48)):
        p.add('door', fb.rbox(x0, x1, -.232, -.215, .11, .435, bevel=.006), M('CC_Wood_Inner'))
    for x in (-.04, .04):
        p.add('knob', fb.ellipsoid((x, -.242, .30), (.014, .012, .014), u=6, v=4), M('T2_Brass'))
    return p


def coffee_table():
    """An oval coffee table on four splayed legs, a shelf beneath, a lace doily on top."""
    p = fb.Prop('GH_CoffeeTable')
    top_z0, top_z1 = .385, .42
    p.add('top', fb.loft([(0, 0, top_z0, .465, .265), (0, 0, top_z0 + .008, .475, .275),
                          (0, 0, top_z1 - .006, .475, .275), (0, 0, top_z1, .465, .265)], sides=16),
          M('CC_Wood_Counter'))
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            top = (sx * .30, sy * .14, top_z0 + .004)
            bot = (sx * .35, sy * .18, 0.0)
            pts.append((top, bot))
            p.add('leg', fb.strut(top, bot, .022, .014, sides=6), M('CC_Wood_Espresso'))
    # the shelf meets the legs where they are at its height (Rule 3), not where they start
    c = fb.at_z(*pts[3], .13)
    p.add('shelf', fb.rbox(-c.x - .004, c.x + .004, -c.y - .004, c.y + .004, .12, .14, bevel=.006), M('CC_Wood_Inner'))
    p.add('doily', fb.cylinder((0, 0, top_z1 + .0015), .13, .003, sides=12), M('GH_Lace'))
    return p


def sideboard():
    """The sideboard (photos stand on it): three drawers over three doors, brass handles, short legs."""
    p = fb.Prop('GH_Sideboard')
    legs(p, [(-.62, -.18), (.62, -.18), (-.62, .18), (.62, .18)], 0.11, 0.024, 0.017, splay=.012)
    p.add('carcass', fb.rbox(-.675, .675, -.23, .23, .10, .835, bevel=.01), M('CC_Wood_Espresso'))
    p.add('top', fb.rbox(-.695, .695, -.25, .245, .83, .865, bevel=.012), M('CC_Wood_Counter'))
    w = (1.31 - 2 * .015) / 3
    for i in range(3):
        x0 = -.655 + i * (w + .015)
        p.add('drawer', fb.rbox(x0, x0 + w, -.24, -.222, .675, .81, bevel=.006), M('CC_Wood_Inner'))
        p.add('drawer handle', fb.rbox(x0 + w / 2 - .045, x0 + w / 2 + .045, -.256, -.236, .735, .75, bevel=.003), M('T2_Brass'))
        p.add('door', fb.rbox(x0, x0 + w, -.24, -.222, .14, .655, bevel=.006), M('CC_Wood_Inner'))
        p.add('door knob', fb.ellipsoid((x0 + w / 2, -.25, .56), (.014, .013, .014), u=6, v=4), M('T2_Brass'))
    return p


def frame_standing(name, photo_w, photo_h, border, mat):
    """A photo frame that stands on a sideboard, leaning back on its strut."""
    p = fb.Prop(name)
    w, h, d = photo_w + 2 * border, photo_h + 2 * border, .014
    lean = 12
    parts = [
        ('frame', fb.rbox(-w / 2, w / 2, -d / 2, d / 2, 0, h, bevel=.003), M(mat)),
        ('photo', fb.zbox(-photo_w / 2, photo_w / 2, -d / 2 - .0015, -d / 2 + .002, border, border + photo_h), M('GH_Photo')),
    ]
    for label, bm, mat_ in parts:
        p.add(label, fb.turn(bm, -lean, 'X', (0, d / 2, 0)), mat_)
    # the strut behind, from the frame's back down to the table
    back_top_y = d / 2 + math.sin(math.radians(lean)) * h * .55
    back_top_z = math.cos(math.radians(lean)) * h * .55
    p.add('strut', fb.strut((0, back_top_y - .002, back_top_z), (0, back_top_y + h * .30, 0.0), .006, .006, sides=4), M(mat))
    return p


def frame_wall(name, photo_w, photo_h, border, mat):
    """A photo frame for a wall: the origin at the bottom centre of its back (the back at y = 0)."""
    p = fb.Prop(name)
    w, h, d = photo_w + 2 * border, photo_h + 2 * border, .022
    p.add('frame', fb.rbox(-w / 2, w / 2, -d, 0, 0, h, bevel=.004), M(mat))
    p.add('mount', fb.zbox(-photo_w / 2 - .012, photo_w / 2 + .012, -d - .001, -d + .004, border - .012, border + photo_h + .012), M('GH_Lace'))
    p.add('photo', fb.zbox(-photo_w / 2, photo_w / 2, -d - .003, -d + .004, border, border + photo_h), M('GH_Photo'))
    return p


def standard_lamp():
    """A standard lamp: a brass stem on a round foot, a fabric shade (it glows when her light is on)."""
    p = fb.Prop('GH_StandardLamp')
    brass = M('T2_Brass')
    p.add('foot', fb.lathe([(.15, 0), (.15, .018), (.12, .032), (.04, .05), (.02, .07)], sides=12), brass)
    p.add('stem', fb.cylinder((0, 0, .70), .012, 1.30, sides=6), brass)
    p.add('collar', fb.cylinder((0, 0, 1.22), .022, .03, sides=8), brass)
    # the shade: a fabric cone, open at the bottom, with an inside you see from below
    shade = [(.215, 1.25), (.205, 1.26), (.135, 1.56), (.13, 1.57)]
    outer = fb.lathe(shade, sides=14, cap_bottom=False, cap_top=True)
    inner = fb.lathe([(r - .006, z + .004) for r, z in shade[1:3]], sides=14, cap_bottom=False, cap_top=False)
    for f in inner.faces:
        f.normal_flip()
    fb.merge(outer, inner)
    p.add('shade', outer, M('GH_Lampshade'))
    p.add('bulb', fb.ellipsoid((0, 0, 1.31), (.035, .035, .045), u=8, v=5), M('DC_Ceramic'))
    p.add('bulb holder', fb.cylinder((0, 0, 1.265), .02, .05, sides=8), brass)
    return p


def rug():
    """The front-room rug: a burgundy field in a cream border, a medallion, a fringe at each end."""
    p = fb.Prop('GH_Rug')
    p.add('border', fb.zbox(-1.0, 1.0, -.70, .70, 0, .010), M('GH_Rug_Border'))
    p.add('field', fb.zbox(-.88, .88, -.58, .58, 0, .012), M('GH_Rug_Field'))
    # a thin cream line inside the field, then a diamond in the middle
    for x0, x1, y0, y1 in ((-.80, .80, -.50, -.48), (-.80, .80, .48, .50), (-.80, -.78, -.50, .50), (.78, .80, -.50, .50)):
        p.add('line', fb.zbox(x0, x1, y0, y1, .0115, .0132), M('GH_Rug_Border'))
    p.add('medallion', fb.loft([(0, 0, .0115, .30, .20), (0, 0, .0136, .30, .20)], sides=4, phase=0), M('GH_Rug_Border'))
    p.add('medallion heart', fb.loft([(0, 0, .0130, .15, .10), (0, 0, .0145, .15, .10)], sides=4, phase=0), M('GH_Pillow_Mustard'))
    for s in (-1, 1):
        x0, x1 = sorted((s * .999, s * 1.05))
        p.add('fringe', fb.zbox(x0, x1, -.66, .66, 0, .004), M('GH_Lace'))
    return p


# ============================================================================= the kitchen


def shaker_door(p, x0, x1, z0, z1, y_face, mat, handle=None, handle_mat='DC_Steel', label='door'):
    """A cupboard door on a face at y_face: a slab, a frame proud of it, a handle (knob, bar or none)."""
    p.add(label, fb.zbox(x0, x1, y_face - .018, y_face + .002, z0, z1), M(mat))
    fw = min(.055, (x1 - x0) / 5, (z1 - z0) / 5)
    p.add(label + ' panel', fb.zbox(x0 + fw, x1 - fw, y_face - .026, y_face - .016, z0 + fw, z1 - fw), M(mat))
    if handle == 'knob':
        hx = x1 - .06 if (x1 - x0) > .2 else (x0 + x1) / 2
        p.add('knob', fb.ellipsoid((hx, y_face - .04, (z0 + z1) / 2), (.015, .014, .015), u=6, v=4), M(handle_mat))
    elif handle == 'knob-left':
        p.add('knob', fb.ellipsoid((x0 + .06, y_face - .04, (z0 + z1) / 2), (.015, .014, .015), u=6, v=4), M(handle_mat))
    elif handle == 'bar':
        cx = (x0 + x1) / 2
        p.add('handle', fb.zbox(cx - .06, cx + .06, y_face - .045, y_face - .02, z1 - .065, z1 - .045), M(handle_mat))


def kitchen_counter():
    """
    The counter run along the kitchen's back wall, 2.6 m: a cupboard, the cooker (oven under a four-ring
    hob), a stack of drawers, and the sink unit with a steel sink and a tap. Worktop at 0.92 m.
    """
    p = fb.Prop('GH_KitchenCounter')
    cream, top = 'GH_Kitchen_Cream', M('CC_Wood_Counter')
    W, D, H, T = 2.60, .62, .92, .04
    fy = -.28                       # the carcass front (doors sit on it)
    p.add('kick', fb.zbox(-1.29, 1.29, -.24, .30, 0, .105), M('CC_Dark_Kick'))
    p.add('carcass', fb.rbox(-1.30, 1.30, fy, .31, .10, H - T + .004, bevel=.006), M(cream))
    # the worktop, built round the sink's hole (real panels, no booleans)
    hx0, hx1, hy0, hy1 = .62, 1.18, -.22, .19
    p.add('worktop', fb.rbox(-1.305, hx0, -.31, .31, H - T, H, bevel=.004), top)
    for x0, x1, y0, y1 in ((hx1, 1.305, -.31, .31), (hx0, hx1, -.31, hy0), (hx0, hx1, hy1, .31)):
        p.add('worktop', fb.zbox(x0, x1, y0, y1, H - T, H), top)
    p.add('upstand', fb.rbox(-1.30, 1.30, .285, .31, H - .002, H + .10, bevel=.004), top)
    # unit 1: a cupboard with a drawer
    shaker_door(p, -1.285, -.715, .13, .64, fy, cream, 'knob')
    shaker_door(p, -1.285, -.715, .665, .855, fy, cream, 'bar', label='drawer')
    # unit 2: the cooker, its hob on the worktop above
    p.add('oven door', fb.rbox(-.685, -.115, fy - .03, fy + .002, .13, .66, bevel=.008), M('GH_Plastic_Dark'))
    p.add('oven window', fb.zbox(-.62, -.18, fy - .034, fy - .026, .27, .58), M('GH_Glass_Dark'))
    p.add('oven handle', fb.cylinder((-.40, fy - .06, .625), .011, .46, sides=6, rot=(0, 90, 0)), M('DC_Steel'))
    for x in (-.62, -.18):
        p.add('oven handle post', fb.zbox(x - .008, x + .008, fy - .062, fy - .026, .615, .635), M('DC_Steel'))
    p.add('controls', fb.zbox(-.685, -.115, fy - .025, fy + .002, .68, .86), M('DC_Steel'))
    for i in range(4):
        x = -.63 + i * .15
        p.add('control knob', fb.cylinder((x, fy - .04, .77), .022, .03, sides=6, rot=(90, 0, 0)), M('GH_Plastic_Dark'))
    p.add('hob', fb.rbox(-.69, -.11, -.25, .22, H - .004, H + .006, bevel=.003), M('GH_Plastic_Dark'))
    for x, y, r in ((-.54, -.11, .075), (-.26, -.11, .06), (-.54, .10, .06), (-.26, .10, .075)):
        p.add('ring', fb.cylinder((x, y, H + .009), r, .008, sides=8), M('DC_SteelDark'))
    # unit 3: three drawers
    for i, (z0, z1) in enumerate(((.13, .40), (.425, .64), (.665, .855))):
        shaker_door(p, -.085, .485, z0, z1, fy, cream, 'bar', label='drawer')
    # unit 4: the sink unit, two doors
    shaker_door(p, .515, .895, .13, .855, fy, cream, 'knob')
    shaker_door(p, .905, 1.285, .13, .855, fy, cream, 'knob-left')
    # the sink: a steel bowl hung in the hole, a rim round it, a tap behind
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


def wall_cupboards():
    """Cupboards on the wall over the counter: four doors with brass knobs. Origin: bottom centre of the back."""
    p = fb.Prop('GH_WallCupboards')
    cream = 'GH_Kitchen_Cream'
    p.add('carcass', fb.rbox(-1.0, 1.0, -.32, 0, 0, .70, bevel=.006), M(cream))
    p.add('cornice', fb.rbox(-1.02, 1.02, -.345, 0, .68, .72, bevel=.006), M('CC_Wood_Counter'))
    p.add('pelmet', fb.rbox(-1.006, 1.006, -.335, -.30, 0, .04, bevel=.004), M('CC_Wood_Counter'))
    w = (1.96 - 3 * .01) / 4
    for i in range(4):
        x0 = -.98 + i * (w + .01)
        shaker_door(p, x0, x0 + w, .06, .66, -.32, cream, 'knob' if i % 2 == 0 else 'knob-left', handle_mat='T2_Brass')
    return p


def fridge():
    """A rounded old fridge in mint enamel: a small door over a tall one, chrome handles, a kick plate."""
    p = fb.Prop('GH_Fridge')
    body = fb.rbox(-.31, .31, -.29, .33, .06, 1.62, bevel=.05, segments=2)
    p.add('body', body, M('GH_Fridge_Mint'))
    p.add('kick', fb.zbox(-.29, .29, -.27, .30, 0, .07), M('CC_Dark_Kick'))
    for z0, z1, hz in ((1.14, 1.58, 1.20), (.10, 1.12, .95)):
        p.add('door', fb.rbox(-.30, .30, -.315, -.28, z0, z1, bevel=.025), M('GH_Fridge_Mint'))
        p.add('handle', fb.rbox(.215, .245, -.36, -.335, hz - .09, hz + .09, bevel=.008), M('DC_Steel'))
        for dz in (-.07, .07):
            p.add('handle post', fb.zbox(.222, .238, -.345, -.305, hz + dz - .012, hz + dz + .012), M('DC_Steel'))
    p.add('badge', fb.rbox(-.07, .07, -.322, -.312, 1.05, 1.08, bevel=.004), M('DC_Steel'))
    return p


def kettle():
    """A whistling kettle for the hob, in red enamel, with a black handle arched over its lid."""
    p = fb.Prop('GH_Kettle')
    red, dark = M('GH_Enamel_Red'), M('GH_Plastic_Dark')
    p.add('body', fb.lathe([(.075, 0), (.092, .012), (.105, .05), (.098, .09), (.075, .13), (.05, .15), (.046, .155)], sides=12), red)
    p.add('lid', fb.lathe([(.05, .15), (.052, .158), (.03, .172), (.012, .176)], sides=12), red)
    p.add('lid knob', fb.ellipsoid((0, 0, .183), (.015, .015, .012), u=6, v=4), dark)
    p.add('spout', fb.strut((-.165, 0, .16), (-.07, 0, .065), .012, .02, sides=6), red)
    p.add('whistle', fb.cylinder((-.172, 0, .168), .014, .025, sides=6, rot=(0, -55, 0)), M('DC_Steel'))
    for s in (-1, 1):
        p.add('handle post', fb.strut((s * .045, 0, .225), (s * .06, 0, .135), .007, .008, sides=5), dark)
    p.add('handle', fb.rbox(-.06, .06, -.012, .012, .215, .238, bevel=.008), dark)
    return p


def teapot():
    """A round white teapot with a blue band, a spout and a looped handle."""
    p = fb.Prop('GH_Teapot')
    white, blue = M('DC_Ceramic'), M('GH_Delft_Blue')
    p.add('body', fb.lathe([(.05, 0), (.07, .012), (.084, .045), (.08, .08), (.062, .11), (.045, .12)], sides=12), white)
    p.add('band', fb.lathe([(.0855, .036), (.0865, .048), (.0845, .06)], sides=12), blue)
    p.add('lid', fb.lathe([(.047, .118), (.05, .124), (.03, .138), (.01, .141)], sides=12), white)
    p.add('lid knob', fb.ellipsoid((0, 0, .148), (.013, .013, .011), u=6, v=4), blue)
    p.add('spout', fb.strut((-.135, 0, .115), (-.07, 0, .045), .010, .02, sides=6), white)
    p.add('spout tip', fb.cylinder((-.138, 0, .118), .011, .012, sides=6, rot=(0, -40, 0)), white)
    pts = [(.075, 0, .10), (.12, 0, .098), (.128, 0, .065), (.11, 0, .035), (.078, 0, .03)]
    for a, b in zip(pts, pts[1:]):
        p.add('handle', fb.strut(b, a, .009, .009, sides=5), white)
    return p


# ============================================================================= the hall


def coat_stand():
    """A coat stand by the front door: three feet, brass hooks, her coat, a scarf and a hat."""
    p = fb.Prop('GH_CoatStand')
    wood, brass = M('CC_Wood_Espresso'), M('T2_Brass')
    p.add('pole', fb.cylinder((0, 0, .90), .022, 1.72, sides=8, radius_top=.018), wood)
    p.add('finial', fb.ellipsoid((0, 0, 1.79), (.03, .03, .035), u=8, v=5), wood)
    for i in range(3):
        a = math.radians(90 + i * 120)
        p.add('foot', fb.strut((math.cos(a) * .015, math.sin(a) * .015, .30), (math.cos(a) * .26, math.sin(a) * .26, 0), .018, .013, sides=6), wood)
    hooks = []
    for i in range(4):
        a = math.radians(45 + i * 90)
        tip = (math.cos(a) * .15, math.sin(a) * .15, 1.66)
        hooks.append(tip)
        p.add('hook', fb.strut(tip, (math.cos(a) * .012, math.sin(a) * .012, 1.60), .007, .009, sides=5), brass)
        p.add('hook ball', fb.ellipsoid(tip, (.012, .012, .012), u=5, v=3), brass)
    # her coat, hung on the front-left hook: a long wool coat, shoulders and a collar
    hx, hy, hz = hooks[1]   # 135 degrees: front-left
    coat = fb.loft([
        (0, 0, .78, .19, .11), (0, 0, 1.05, .17, .10), (0, 0, 1.36, .18, .10),
        (0, 0, 1.50, .20, .10), (0, 0, 1.58, .13, .07), (0, 0, 1.62, .05, .04),
    ], sides=8, phase=22.5)
    # its width across the hook's direction (the hook points 135 degrees round; the coat's broad side faces out)
    coat = fb.turn(coat, 45, 'Z', (0, 0, 0))
    p.add('coat', fb.move(coat, (hx * 1.3, hy * 1.3, -.01)), M('GH_Wool_Camel'))
    collar = fb.turn(fb.loft([(0, 0, 1.53, .11, .075), (0, 0, 1.60, .08, .06)], sides=8), 45, 'Z', (0, 0, 0))
    p.add('collar', fb.move(collar, (hx * 1.3, hy * 1.3, -.01)), M('GH_Wool_Camel'))
    # a scarf over the back-right hook, hanging both sides
    sx, sy, sz = hooks[3]
    for side in (-1, 1):
        p.add('scarf', fb.strut((sx + side * .03, sy, sz + .015), (sx + side * .05, sy + .01, sz - .42), .022, .02, sides=4), M('GH_Scarf_Red'))
    # a hat on the finial
    p.add('hat', fb.lathe([(.13, 1.78), (.13, 1.79), (.075, 1.80), (.07, 1.87), (.05, 1.89)], sides=12), M('GH_Pillow_Sage'))
    p.add('hat band', fb.lathe([(.073, 1.80), (.0735, 1.825)], sides=12), M('GH_Scarf_Red'))
    return p


def prism_x(x0, x1, poly_yz):
    """A slab across X (from x0 to x1) whose side is the polygon poly_yz [(y, z)] (a stringer, a panel)."""
    bm = fb.bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in poly_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in poly_yz]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(poly_yz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# The flight: 12 risers of 0.20 m (0.15 m to 2.55 m, her two floors) and 11 treads of 0.24 m.
RISE, GOING, STEPS, WIDTH, NOSING = .20, .24, 12, .95, .02
SLOPE = RISE / GOING
DOOR_Y0, DOOR_Y1, DOOR_H = 1.55, 2.20, 1.15     # the cupboard door in the side under the stairs


def nosing_line(y):
    """Height of the line through the treads' front edges, at y."""
    return RISE + (y + NOSING) * SLOPE


def stairs():
    """
    Her stairs: a straight flight rising toward +Y (from the hall toward the back of the house), the wall
    on its -X side, a banister on its open +X side, a red runner, and the cupboard under the stairs with
    its doorway (the door is GH_UnderStairsDoor). Origin: the front of the bottom riser, centred.
    """
    p = fb.Prop('GH_Stairs')
    hw = WIDTH / 2
    run = (STEPS - 1) * GOING
    for i in range(STEPS):
        y = i * GOING
        p.add('riser', fb.zbox(-hw, hw, y, y + .02, i * RISE, (i + 1) * RISE - .028), M('GH_Paint_White'))
        p.add('runner', fb.zbox(-.30, .30, y - .004, y, i * RISE, (i + 1) * RISE - .03), M('GH_Rug_Field'))
    for i in range(STEPS - 1):
        top = (i + 1) * RISE
        y0, y1 = i * GOING - NOSING, (i + 1) * GOING + .002
        p.add('tread', fb.zbox(-hw, hw, y0, y1, top - .03, top), M('CC_Wood_Counter'))
        p.add('runner', fb.zbox(-.30, .30, y0 - .003, y1 - .004, top, top + .004), M('GH_Rug_Field'))
    p.add('landing edge', fb.rbox(-hw, hw, run - NOSING, run + .10, STEPS * RISE - .03, STEPS * RISE, bevel=.006), M('CC_Wood_Counter'))
    # the stringers: a band above and below the line of the nosings
    y_end = run + .02
    top_at = lambda y: nosing_line(y) + .05
    bottom_at = lambda y: nosing_line(y) - .28
    y_floor = -NOSING + (0 - (RISE - .28)) / SLOPE           # where the bottom edge meets the floor
    band = [(-NOSING, 0), (-NOSING, top_at(-NOSING)), (y_end, top_at(y_end)), (y_end, bottom_at(y_end)), (y_floor, 0)]
    p.add('stringer (open side)', prism_x(hw - .005, hw + .035, band), M('CC_Wood_Espresso'))
    p.add('stringer (wall side)', prism_x(-hw - .03, -hw + .005, band), M('CC_Wood_Espresso'))
    # the side under the stairs, built round the cupboard's doorway (real panels, no booleans)
    px0, px1 = hw - .01, hw + .01
    p.add('side panel', prism_x(px0, px1, [(y_floor, 0), (DOOR_Y0, bottom_at(DOOR_Y0) + .01), (DOOR_Y0, 0)]), M('GH_Paint_White'))
    p.add('side panel', prism_x(px0, px1, [(DOOR_Y0, DOOR_H), (DOOR_Y0, bottom_at(DOOR_Y0) + .01),
                                           (DOOR_Y1, bottom_at(DOOR_Y1) + .01), (DOOR_Y1, DOOR_H)]), M('GH_Paint_White'))
    p.add('side panel', prism_x(px0, px1, [(DOOR_Y1, 0), (DOOR_Y1, bottom_at(DOOR_Y1) + .01), (y_end, bottom_at(y_end) + .01), (y_end, 0)]), M('GH_Paint_White'))
    for y0, y1, z0, z1 in ((DOOR_Y0 - .05, DOOR_Y0, 0, DOOR_H + .05), (DOOR_Y1, DOOR_Y1 + .05, 0, DOOR_H + .05), (DOOR_Y0 - .05, DOOR_Y1 + .05, DOOR_H, DOOR_H + .05)):
        p.add('doorway trim', fb.zbox(hw + .005, hw + .022, y0, y1, z0, z1), M('GH_Paint_White'))
    # inside the cupboard: the underside of the stairs and a back wall
    p.add('soffit', prism_x(-hw, hw, [(y_floor, 0), (y_end, bottom_at(y_end)), (y_end, bottom_at(y_end) + .03), (y_floor - .036, 0)]), M('GH_Paint_White'))
    p.add('cupboard back', fb.zbox(-hw, hw, y_end - .02, y_end, 0, bottom_at(y_end) + .03), M('GH_Paint_White'))
    # the banister: newels, a rail at 0.9 m over the nosings, one baluster per tread
    nb = (hw + .015, .03)
    nt = (hw + .015, run - .02)
    p.add('newel', fb.rbox(nb[0] - .045, nb[0] + .045, nb[1] - .045, nb[1] + .045, 0, 1.12, bevel=.008), M('CC_Wood_Espresso'))
    p.add('newel cap', fb.ellipsoid((nb[0], nb[1], 1.16), (.05, .05, .045), u=8, v=5), M('CC_Wood_Espresso'))
    t_base = top_at(nt[1]) - .10
    t_top = nosing_line(nt[1]) + 1.02
    p.add('newel', fb.rbox(nt[0] - .045, nt[0] + .045, nt[1] - .045, nt[1] + .045, t_base, t_top, bevel=.008), M('CC_Wood_Espresso'))
    p.add('newel cap', fb.ellipsoid((nt[0], nt[1], t_top + .04), (.05, .05, .045), u=8, v=5), M('CC_Wood_Espresso'))
    rail_a = (nb[0], nb[1] + .04, nosing_line(nb[1]) + .90)
    rail_b = (nt[0], nt[1] - .04, nosing_line(nt[1]) + .90)
    p.add('handrail', fb.strut(rail_b, rail_a, .03, .03, sides=6), M('CC_Wood_Espresso'))
    for i in range(STEPS - 1):
        y = i * GOING + GOING / 2 + .1
        if y > nt[1] - .1:
            continue
        p.add('baluster', fb.strut((hw + .015, y, nosing_line(y) + .88), (hw + .015, y, top_at(y) - .01), .012, .015, sides=6), M('GH_Paint_White'))
    return p


def under_stairs_door():
    """The cupboard door under the stairs: origin on its hinge line at the floor (it swings about Z)."""
    p = fb.Prop('GH_UnderStairsDoor')
    w, h, t = DOOR_Y1 - DOOR_Y0 - .006, DOOR_H - .006, .025
    # hinged at its front edge (toward -Y); the leaf runs +Y from the hinge, its outer face toward +X
    p.add('leaf', fb.rbox(-t, 0, 0, w, 0, h, bevel=.004), M('GH_Paint_White'))
    for z0, z1 in ((.08, .52), (.62, h - .08)):
        p.add('panel', fb.rbox(-.003, .004, .07, w - .07, z0, z1, bevel=.003), M('GH_Paint_White'))
    p.add('knob', fb.ellipsoid((.013, w - .07, .62), (.014, .014, .014), u=6, v=4), M('T2_Brass'))
    return p


def interior_door_frame():
    """A doorway for an inside wall (the landing door, the kitchen): lining and architraves both sides.
    Origin: bottom centre of the opening, on the wall's centre line."""
    p = fb.Prop('GH_InteriorDoor_Frame')
    ow, oh, depth = .82, 2.03, .14
    white = M('GH_Paint_White')
    for x0, x1 in ((-ow / 2 - .025, -ow / 2), (ow / 2, ow / 2 + .025)):
        p.add('lining', fb.zbox(x0, x1, -depth / 2, depth / 2, 0, oh), white)
    p.add('lining head', fb.zbox(-ow / 2 - .025, ow / 2 + .025, -depth / 2, depth / 2, oh, oh + .025), white)
    for y0, y1, proud in ((-depth / 2 - .015, -depth / 2 + .002, -.004), (depth / 2 - .002, depth / 2 + .015, .004)):
        for x0, x1 in ((-ow / 2 - .07, -ow / 2 + .005), (ow / 2 - .005, ow / 2 + .07)):
            p.add('architrave', fb.rbox(x0, x1, y0, y1, 0, oh + .06, bevel=.004), white)
        # the head sits over the sides' tops, a little proud and a little wider (no faces in the same plane)
        hy0, hy1 = sorted((y0 + proud, y1 + proud))
        p.add('architrave head', fb.rbox(-ow / 2 - .078, ow / 2 + .078, hy0, hy1, oh - .005, oh + .078, bevel=.004), white)
    p.add('door stop', fb.zbox(-ow / 2, ow / 2, .015, .03, oh - .012, oh), white)
    return p


def interior_door_leaf():
    """A four-panel door: origin on its hinge line at the floor, the leaf running +X from it."""
    p = fb.Prop('GH_InteriorDoor_Leaf')
    w, h, t = .80, 2.01, .04
    white = M('GH_Paint_White')
    p.add('leaf', fb.rbox(0, w, -t / 2, t / 2, 0, h, bevel=.004), white)
    for side in (-1, 1):
        y0, y1 = sorted((side * t / 2, side * (t / 2 + .006)))
        for x0, x1 in ((.08, w / 2 - .03), (w / 2 + .03, w - .08)):
            for z0, z1 in ((.12, .82), (1.0, h - .12)):
                p.add('panel', fb.zbox(x0, x1, y0, y1, z0, z1), white)
        p.add('knob', fb.ellipsoid((w - .07, side * (t / 2 + .045), .98), (.026, .018, .026), u=8, v=5), M('T2_Brass'))
        p.add('knob neck', fb.cylinder((w - .07, side * (t / 2 + .02), .98), .012, .05, sides=6, rot=(90, 0, 0)), M('T2_Brass'))
        p.add('rose', fb.cylinder((w - .07, side * (t / 2 + .002), .98), .03, .006, sides=10, rot=(90, 0, 0)), M('T2_Brass'))
    for z in (.25, h - .25):
        p.add('hinge', fb.zbox(-.004, .012, -.02, .02, z - .05, z + .05), M('DC_SteelDark'))
    return p


# ============================================================================= the stash


SLEEVE_R, SLEEVE_L = .046, .38


def sleeve_geom(cx, cy, cz, length=SLEEVE_L, r=SLEEVE_R):
    """A sleeve of twelve paper cups lying along X: rims every few centimetres, the open mouth at +X."""
    rings = []
    n = 7
    for i in range(n):
        x = -length / 2 + i * length / (n - 1)
        rr = r if i % 2 == 0 else r * .94
        rings.append((x, rr))
    bm = fb.bmesh.new()
    loops = []
    sides = 8
    for x, rr in rings:
        loops.append([bm.verts.new((cx + x, cy + rr * math.cos(2 * math.pi * k / sides), cz + rr * math.sin(2 * math.pi * k / sides))) for k in range(sides)])
    for lo, hi in zip(loops, loops[1:]):
        for k in range(sides):
            j = (k + 1) % sides
            bm.faces.new([lo[k], lo[j], hi[j], hi[k]])
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def cup_sleeve():
    """One sleeve of paper cups (what Ace takes from her box): origin under its middle."""
    p = fb.Prop('GH_CupSleeve')
    p.add('cups', sleeve_geom(0, 0, SLEEVE_R), M('DC_Ceramic'))
    p.add('mouth', fb.cylinder((SLEEVE_L / 2 + .001, 0, SLEEVE_R), SLEEVE_R * .8, .003, sides=8, rot=(0, 90, 0)), M('GH_Glass_Dark'))
    p.add('base band', fb.cylinder((-SLEEVE_L / 2 + .02, 0, SLEEVE_R), SLEEVE_R + .002, .025, sides=8, rot=(0, 90, 0)), M('GH_Tape'))
    return p


def cup_box():
    """The reunion cups: an open cardboard box with three sleeves of twelve in it, its flaps folded out."""
    p = fb.Prop('GH_CupBox')
    card = M('GH_Cardboard')
    x0, x1, y0, y1, h, wall = -.21, .21, -.16, .16, .17, .006
    p.add('box', fb.open_box(x0, x1, y0, y1, 0, h, wall), card)
    for side in (-1, 1):
        flap = fb.zbox(x0, x1, 0, .005, 0, .15)
        yy = y0 if side < 0 else y1
        flap = fb.move(fb.turn(flap, -side * 118, 'X', (0, 0, 0)), (0, yy, h))      # folded out and down
        p.add('flap (long side)', flap, card)
        flap = fb.zbox(0, .005, y0 + .005, y1 - .005, 0, .12)
        xx = x0 if side < 0 else x1
        flap = fb.move(fb.turn(flap, side * 112, 'Y', (0, 0, 0)), (xx, 0, h))
        p.add('flap (short side)', flap, card)
    p.add('tape', fb.zbox(-.03, .03, y0 - .004, y0 + .002, h - .06, h + .002), M('GH_Tape'))
    for i, y in enumerate((-.098, 0, .098)):
        cz = wall + SLEEVE_R + (.003 if i == 1 else 0)
        p.add('sleeve', sleeve_geom(.01, y, cz), M('DC_Ceramic'))
        p.add('mouth', fb.cylinder((.01 + SLEEVE_L / 2 + .001, y, cz), SLEEVE_R * .8, .003, sides=8, rot=(0, 90, 0)), M('GH_Glass_Dark'))
    return p


# ============================================================================= the list


PIECES = [
    # (builder, room, what it is)
    (armchair, 'Front room', "her armchair (seat 0.45)"),
    (sofa, 'Front room', "the sofa under the window"),
    (tv, 'Front room', "the television"),
    (tv_cabinet, 'Front room', "the low cabinet it stands on"),
    (coffee_table, 'Front room', "coffee table"),
    (sideboard, 'Front room', "sideboard (photos on it)"),
    (lambda: frame_standing('GH_Frame_S', .10, .14, .02, 'T2_Brass'), 'Front room', "photo frame, small, standing"),
    (lambda: frame_standing('GH_Frame_M', .15, .20, .025, 'CC_Wood_Espresso'), 'Front room', "photo frame, medium, standing"),
    (lambda: frame_wall('GH_Frame_L', .28, .36, .04, 'CC_Wood_Counter'), 'Front room', "photo frame, large, for a wall"),
    (lambda: frame_wall('GH_Frame_XL', .42, .56, .05, 'CC_Wood_Espresso'), 'Front room', "photo frame, very large, for a wall"),
    (standard_lamp, 'Front room', "standard lamp"),
    (rug, 'Front room', "rug"),
    (kitchen_counter, 'Kitchen', "counter with sink, hob and oven (worktop 0.92)"),
    (wall_cupboards, 'Kitchen', "wall cupboards (hang the bottom at 1.45)"),
    (fridge, 'Kitchen', "fridge"),
    (kettle, 'Kitchen', "kettle (on the hob)"),
    (teapot, 'Kitchen', "teapot"),
    (coat_stand, 'Hall', "coat stand with her coat"),
    (stairs, 'Hall', "the stairs, with the cupboard under them"),
    (under_stairs_door, 'Hall', "the cupboard door under the stairs"),
    (interior_door_frame, 'Hall', "doorway (the landing door)"),
    (interior_door_leaf, 'Hall', "door"),
    (cup_box, 'Kitchen', "the reunion cups: a box of three sleeves (the stash)"),
    (cup_sleeve, 'Kitchen', "one sleeve of twelve cups"),
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
