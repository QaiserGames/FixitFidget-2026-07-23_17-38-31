"""
The house dressing kit, v1 (5 Oct 2026; claude/house-interiors-plan.md section 9, route A): the pieces that make
a box-built house read as a house up close, built with Python in Blender 5.2 (fixit_blender.py beside it).

Four groups, every piece reusable in any house (Grace's first):

  A. The finish layer: skirting, dado rail, cornice, architrave, a lining board for reveals, a threshold, a
     window sill. These have one constant profile along X and are 1.00 m long: Unity stretches them along X
     to each wall or opening (scale x = the length in metres), so one mesh fits every room.
  B. The window at night: a curtain panel (placed twice, mirrored), a curtain rod, a roller blind.
  C. The someone-lives-here layer: doormat, radiator, four pictures (their paintings are generated textures,
     mapped in Unity), cushion, round rug, hall runner, a plant, a row of books, an open book, a wall clock,
     a mug, a stack of plates, a pile of post and a single letter, a pair of shoes, a hook rail with a coat
     and a scarf on it, a toaster, a kitchen bin, a newspaper, slippers, a pendant light and a strip light.
  D. The door from the street: a panelled leaf with a glazed upper panel (its paint and glass slots are
     mapped per house in Unity, so every house keeps its own colour), a porch lantern.

Conventions (the furniture library's): 1 unit = 1 m, Z up, the front faces -Y (+Z in Unity); origin at the
bottom centre, or for a piece hung on a wall at the bottom centre of its back with the back at y = 0; a
piece that hangs from above (the pendant, the curtains, the coat) has its origin at the top and goes down
from z = 0 (floor=False). Flat shading, one-segment bevels, junctions from boundaries, parts overlapping
2-4 mm, no two faces in one plane, and the audit (nothing floating more than 2 mm from the rest).

Materials: the cafe's own where they fit (CC_/DC_/T2_), Grace's (GH_) where they exist, and HK_ ones for the
kit (HK_MATERIALS: sRGB hex and a Unity smoothness; the Unity step makes them). HK_Painting_* carry a
texture (Tools/Blender/paintings.py makes the PNGs); HK_Door_Paint and HK_Door_Glass are slots the Unity
door step maps per house.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
from mathutils import Vector  # noqa: E402

# (sRGB hex, Unity smoothness)
HK_MATERIALS = {
    'HK_Trim_Paint':      ('#F2EDE3', 0.35),   # skirting, dado, cornice, architraves, sills: a soft white
    'HK_Oak':             ('#9A7044', 0.30),   # thresholds, the book ends, the clock
    'HK_Cord_Black':      ('#2A2622', 0.30),
    'HK_Coir':            ('#A88A5C', 0.02),
    'HK_Coir_Dark':       ('#5E4A33', 0.02),
    'HK_Radiator':        ('#E8E4DC', 0.30),
    'HK_Curtain_Oat':     ('#CDBBA0', 0.08),
    'HK_Curtain_Rose':    ('#B98A8A', 0.08),
    'HK_Blind':           ('#E9E2D2', 0.10),
    'HK_Cushion_Mustard': ('#C99A3E', 0.10),
    'HK_Cushion_Sage':    ('#8FA58B', 0.10),
    'HK_Rug_Teal':        ('#3E6B6A', 0.03),
    'HK_Rug_Cream':       ('#D9CBA9', 0.03),
    'HK_Rug_Rust':        ('#9A4A32', 0.03),
    'HK_Terracotta':      ('#B5603A', 0.20),
    'HK_Soil':            ('#3B2A1E', 0.02),
    'HK_Leaf':            ('#4F7A3F', 0.15),
    'HK_Leaf_Light':      ('#78A05A', 0.15),
    'HK_Book_Red':        ('#8E3A33', 0.20),
    'HK_Book_Blue':       ('#34507A', 0.20),
    'HK_Book_Green':      ('#4A6B4A', 0.20),
    'HK_Book_Cream':      ('#E2D6BC', 0.20),
    'HK_Paper':           ('#F2EEE6', 0.05),
    'HK_Ink':             ('#2E2B28', 0.10),
    'HK_Stamp':           ('#B04A3C', 0.10),
    'HK_Leather_Brown':   ('#5A3A24', 0.30),
    'HK_Sole':            ('#2B2622', 0.20),
    'HK_Slipper':         ('#9C8A9C', 0.05),
    'HK_Enamel_Cream':    ('#EFE6D0', 0.60),
    'HK_Bin_Grey':        ('#6B6E70', 0.45),
    'HK_Clock_Face':      ('#F4EFE2', 0.20),
    'HK_Painting_Hills':  ('#8FA6B8', 0.10),   # textured in Unity (the hex is the Blender stand-in)
    'HK_Painting_Harbour':('#7F9BB0', 0.10),
    'HK_Painting_Still':  ('#B48E6A', 0.10),
    'HK_Painting_Portrait':('#6E7F8E', 0.10),
    'HK_Door_Paint':      ('#2F6E63', 0.30),   # mapped per house in Unity
    'HK_Door_Glass':      ('#C9D6DE', 0.90),   # mapped per house in Unity (the house's window glass)
    'HK_Lantern_Glass':   ('#FFE2B0', 0.60),   # emissive in Unity when its light is on
    'HK_Iron_Black':      ('#1F1E1C', 0.35),
}


def M(name):
    """A material by name from the kit's, Grace's or the cafe's tables."""
    if name in HK_MATERIALS:
        hexstr, smooth = HK_MATERIALS[name]
        metal = 0.0
        return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=metal)
    return gh.M(name)


def spin(profile, sides=12, phase=0.0, cap_bottom=True, cap_top=True):
    """A lathe whose profile may start or end on the axis: those ends become a cone's tip, never a degenerate ring."""
    prof = list(profile)
    bottom_tip = top_tip = None
    if prof[0][0] <= 1e-6:
        bottom_tip = (0.0, 0.0, prof[0][1])
        prof = prof[1:]
    if prof[-1][0] <= 1e-6:
        top_tip = (0.0, 0.0, prof[-1][1])
        prof = prof[:-1]
    return fb.loft([(0.0, 0.0, z, r, r) for r, z in prof], sides=sides, cap_bottom=cap_bottom and bottom_tip is None,
                   cap_top=cap_top and top_tip is None, tip=top_tip, bottom_tip=bottom_tip, phase=phase)


def prism(profile, x0, x1, close=True):
    """
    A solid with a constant profile along X: profile is a list of (y, z) points, anticlockwise when seen
    from +X (so the outside faces look outward). Faces: the two ends and one quad per profile edge.
    """
    import bmesh
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile]
    b = [bm.verts.new((x1, y, z)) for y, z in profile]
    n = len(profile)
    for i in range(n if close else n - 1):
        j = (i + 1) % n
        bm.faces.new([a[i], b[i], b[j], a[j]])
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def ring(cx, cy, cz, r_outer, r_inner, height, sides=16, mat_box=None):
    """A flat ring (a disc with a hole): outer and inner walls, a top and a bottom."""
    import bmesh
    bm = bmesh.new()
    lo_o, hi_o, lo_i, hi_i = [], [], [], []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        co, si = math.cos(a), math.sin(a)
        lo_o.append(bm.verts.new((cx + r_outer * co, cy + r_outer * si, cz)))
        hi_o.append(bm.verts.new((cx + r_outer * co, cy + r_outer * si, cz + height)))
        lo_i.append(bm.verts.new((cx + r_inner * co, cy + r_inner * si, cz)))
        hi_i.append(bm.verts.new((cx + r_inner * co, cy + r_inner * si, cz + height)))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new([lo_o[i], lo_o[j], hi_o[j], hi_o[i]])
        bm.faces.new([hi_i[i], hi_i[j], lo_i[j], lo_i[i]])
        bm.faces.new([hi_o[i], hi_o[j], hi_i[j], hi_i[i]])
        bm.faces.new([lo_i[i], lo_i[j], lo_o[j], lo_o[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def torus(cx, cy, cz, radius, tube, sides=12, rings_n=8, rot=(0, 0, 0)):
    """A torus in the XY plane at (cx, cy, cz), turned by rot."""
    import bmesh
    bm = bmesh.new()
    loops = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        loop = []
        for j in range(rings_n):
            b = 2 * math.pi * j / rings_n
            r = radius + tube * math.cos(b)
            loop.append(bm.verts.new((r * math.cos(a), r * math.sin(a), tube * math.sin(b))))
        loops.append(loop)
    for i in range(sides):
        la, lb = loops[i], loops[(i + 1) % sides]
        for j in range(rings_n):
            k = (j + 1) % rings_n
            bm.faces.new([la[j], lb[j], lb[k], la[k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm = fb._xf(bm, fb.euler_matrix(rot))
    return fb.move(bm, (cx, cy, cz))


# ============================================================================= A. the finish layer (1.00 m along X)


SKIRTING_PROFILE = [(0, 0), (-.018, 0), (-.018, .10), (-.008, .12), (0, .12)]
DADO_PROFILE = [(0, 0), (-.016, 0), (-.016, .022), (-.030, .030), (-.030, .044), (-.016, .050), (-.012, .060), (0, .060)]


def cornice_profile():
    """The cove cornice's profile (y, z): the wall foot, a small lip, the cove (a quarter circle out and up), the ceiling edge."""
    prof = [(0, 0), (-.014, 0), (-.014, .012)]
    for a in (85, 65, 45, 25, 8):
        t = math.radians(a)
        prof.append((-.08 + .066 * math.cos(t), .08 - .068 * math.sin(t)))
    prof += [(-.08, .08), (0, .08)]
    return prof


def skirting():
    """Skirting board, 0.12 tall, 0.018 deep, a chamfer at the top. Back at y = 0, the room toward -Y."""
    p = fb.Prop('HK_Skirting', floor=False)
    p.add('board', prism(SKIRTING_PROFILE, -.5, .5), M('HK_Trim_Paint'))
    return p


def dado_rail():
    """A dado rail, 0.06 tall: a flat band and a bead over it. Goes on the sill-cut seam at 0.78 (its bottom at 0.75)."""
    p = fb.Prop('HK_DadoRail', floor=False)
    p.add('rail', prism(DADO_PROFILE, -.5, .5), M('HK_Trim_Paint'))
    return p


def cornice():
    """A cove cornice, 0.08 x 0.08, against the wall and the ceiling: origin at its bottom back (the wall), the ceiling at z 0.08."""
    p = fb.Prop('HK_Cornice', floor=False)
    p.add('cove', prism(cornice_profile(), -.5, .5), M('HK_Trim_Paint'))
    return p


def inside_corner(name, profile, length=.10):
    """
    A moulding turning an inside corner, mitred: two stubs of `length`, one along wall A (the plane y = 0, the
    room toward -Y, the stub toward +X) and one along wall B (the plane x = 0, the room toward +X, the stub
    toward -Y), meeting on the mitre plane x = -y. Origin at the corner, on the floor line. In Unity the straight
    runs stop `length` short of such a corner and this piece fills it (turn 0: the corner where the back wall
    meets the south wall; 90: south wall and street; 180: street and north; -90: north and back).
    """
    import bmesh
    p = fb.Prop(name, floor=False)
    bm = bmesh.new()
    n = len(profile)

    def stub(reflect):
        near, far = [], []
        for y, z in profile:
            a, b = (-y, y, z), (length, y, z)
            if reflect:
                a, b = (-a[1], -a[0], a[2]), (-b[1], -b[0], b[2])
            near.append(bm.verts.new(a))
            far.append(bm.verts.new(b))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([near[i], far[i], far[j], near[j]])
        bm.faces.new(far)

    stub(False)
    stub(True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.add('corner', bm, M('HK_Trim_Paint'))
    return p


def architrave():
    """A length of architrave, 0.07 wide (z) and 0.025 deep, the outer edge bevelled; Unity turns it for the jambs and heads."""
    p = fb.Prop('HK_Architrave', floor=False)
    prof = [(0, 0), (-.018, 0), (-.025, .010), (-.025, .058), (-.014, .070), (0, .070)]
    p.add('moulding', prism(prof, -.5, .5), M('HK_Trim_Paint'))
    return p


def lining_board():
    """A reveal's lining: a board 1.00 long (x), 0.20 deep (y, from the wall face at y = 0 into the wall) and 12 mm thick (z)."""
    p = fb.Prop('HK_LiningBoard', floor=False)
    p.add('board', fb.zbox(-.5, .5, 0, .20, 0, .012), M('HK_Trim_Paint'))
    return p


def threshold():
    """A threshold on the floor, 1.00 long, 0.12 deep, 0.02 tall, its top edges rounded off."""
    p = fb.Prop('HK_Threshold')
    prof = [(-.06, 0), (.06, 0), (.06, .012), (.05, .020), (-.05, .020), (-.06, .012)]
    p.add('threshold', prism(prof, -.5, .5), M('HK_Oak'))
    return p


def window_sill():
    """A window sill, 1.00 long: 0.08 deep from the wall face (y = 0) into the room (-Y), 0.035 tall, a bullnose front."""
    p = fb.Prop('HK_WindowSill', floor=False)
    prof = [(0, 0), (-.07, 0), (-.08, .010), (-.08, .025), (-.07, .035), (0, .035)]
    p.add('sill', prism(prof, -.5, .5), M('HK_Trim_Paint'))
    return p


# ============================================================================= B. the window at night


def curtain_panel():
    """
    One curtain panel, gathered into folds: 0.35 wide, 1.50 long, hanging from its origin (the rod's line at
    z = 0). Its back (the window) is +Y; the folds are a zig-zag of 8 pleats, 0.03 deep.
    """
    import bmesh
    p = fb.Prop('HK_CurtainPanel', floor=False)
    pleats, w, depth, length = 8, .35, .03, 1.50
    xs = [-w / 2 + w * i / (2 * pleats) for i in range(2 * pleats + 1)]
    front = [(x, -depth if i % 2 else 0.0) for i, x in enumerate(xs)]
    # a sheet with thickness: the front zig-zag and a back zig-zag 8 mm behind it
    bm = bmesh.new()
    top_f = [bm.verts.new((x, y, 0.0)) for x, y in front]
    bot_f = [bm.verts.new((x, y, -length)) for x, y in front]
    top_b = [bm.verts.new((x, y + .008, -.02)) for x, y in front]
    bot_b = [bm.verts.new((x, y + .008, -length + .004)) for x, y in front]
    n = len(front)
    for i in range(n - 1):
        bm.faces.new([top_f[i], bot_f[i], bot_f[i + 1], top_f[i + 1]])          # front
        bm.faces.new([top_b[i + 1], bot_b[i + 1], bot_b[i], top_b[i]])          # back
        bm.faces.new([top_f[i + 1], top_b[i + 1], top_b[i], top_f[i]])          # top edge
        bm.faces.new([bot_f[i], bot_b[i], bot_b[i + 1], bot_f[i + 1]])          # hem
    bm.faces.new([top_f[0], top_b[0], bot_b[0], bot_f[0]])
    bm.faces.new([bot_f[-1], bot_b[-1], top_b[-1], top_f[-1]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.add('panel', bm, M('HK_Curtain_Oat'))
    # the header: a band folded over the rod, 0.04 tall
    p.add('header', fb.zbox(-w / 2 - .004, w / 2 + .004, -depth - .004, .016, -.04, .004), M('HK_Curtain_Oat'))
    return p


def curtain_rod():
    """The rod alone, 1.00 long, 0.09 off the wall (y = 0 is the wall), stretched in Unity; its brackets and finials are
    HK_CurtainRod_End, placed unstretched at each end (the first kit stretched them with the rod: bullet finials)."""
    p = fb.Prop('HK_CurtainRod', floor=False)
    p.add('rod', fb.cylinder((0, -.09, 0), .012, 1.0, sides=8, rot=(0, 90, 0)), M('T2_Brass'))
    return p


def curtain_rod_end():
    """
    One end of a curtain rod: the finial just past the rod's end (x = 0 is the end; the finial reaches x 0.052) and
    the bracket 0.10 in from it (a plate on the wall, an arm out to the rod), with a stub of rod joining them. Placed
    at each end of a stretched HK_CurtainRod, the second mirrored (scale x -1).
    """
    p = fb.Prop('HK_CurtainRod_End', floor=False)
    brass = M('T2_Brass')
    p.add('stub', fb.cylinder((-.065, -.09, 0), .0125, .155, sides=8, rot=(0, 90, 0)), brass)
    p.add('finial', fb.ellipsoid((.03, -.09, 0), (.022, .022, .022), u=8, v=5), brass)
    p.add('bracket arm', fb.zbox(-.108, -.092, -.09, 0, -.008, .008), brass)
    p.add('bracket plate', fb.zbox(-.12, -.08, -.006, 0, -.03, .03), brass)
    return p


def roller_blind():
    """A roller blind, 1.00 wide, half down: the roll at the top (z 0), the sheet 0.80 down, a bottom bar, on two brackets."""
    p = fb.Prop('HK_Blind', floor=False)
    blind = M('HK_Blind')
    p.add('roll', fb.cylinder((0, -.035, -.03), .03, 1.0, sides=10, rot=(0, 90, 0)), blind)
    p.add('sheet', fb.zbox(-.49, .49, -.066, -.062, -.84, -.03), blind)
    p.add('bar', fb.cylinder((0, -.064, -.845), .009, .98, sides=8, rot=(0, 90, 0)), M('HK_Oak'))
    for s in (-1, 1):
        p.add('bracket', fb.zbox(s * .50 - .008, s * .50 + .008, -.07, 0, -.06, 0), M('DC_SteelDark'))
    return p


# ============================================================================= C. someone lives here


def doormat():
    p = fb.Prop('HK_Doormat')
    p.add('border', fb.zbox(-.35, .35, -.225, .225, 0, .014), M('HK_Coir_Dark'))
    p.add('field', fb.zbox(-.32, .32, -.195, .195, 0, .016), M('HK_Coir'))
    return p


def radiator():
    """A panel radiator 1.00 x 0.60, 0.10 deep, on two feet, its back against the wall (y = 0)."""
    p = fb.Prop('HK_Radiator')
    w = M('HK_Radiator')
    p.add('back panel', fb.rbox(-.50, .50, -.025, -.004, .12, .72, bevel=.004), w)
    p.add('front panel', fb.rbox(-.50, .50, -.10, -.079, .12, .70, bevel=.004), w)
    for i in range(14):
        x = -.46 + i * (.92 / 13)
        p.add('fin', fb.zbox(x - .012, x + .012, -.081, -.022, .15, .67), w)
    p.add('top grille', fb.zbox(-.49, .49, -.10, -.004, .70, .725), w)
    for s in (-1, 1):
        p.add('foot', fb.zbox(s * .40 - .02, s * .40 + .02, -.07, -.02, 0, .13), M('DC_SteelDark'))
        p.add('valve', fb.cylinder((s * .53, -.05, .17), .014, .04, sides=8), M('T2_Brass'))
        p.add('pipe', fb.cylinder((s * .53, -.05, .08), .008, .17, sides=6), M('DC_Steel'))
    return p


def picture(name, w, h, border, frame_mat, painting_mat):
    """A framed painting for a wall: origin at the bottom centre of its back (the back at y = 0)."""
    p = fb.Prop(name, floor=False)
    W, H, d = w + 2 * border, h + 2 * border, .028
    p.add('frame', fb.rbox(-W / 2, W / 2, -d, 0, 0, H, bevel=.005), M(frame_mat))
    p.add('painting', fb.zbox(-w / 2, w / 2, -d - .003, -d + .006, border, border + h), M(painting_mat))
    return p


def cushion():
    """A square cushion, 0.45, plump."""
    p = fb.Prop('HK_Cushion')
    p.add('cushion', fb.rbox(-.225, .225, -.225, .225, 0, .14, bevel=.06, segments=2), M('HK_Cushion_Mustard'))
    return p


def rug_round():
    p = fb.Prop('HK_Rug_Round')
    p.add('border', fb.cylinder((0, 0, .005), .70, .010, sides=24), M('HK_Rug_Cream'))
    p.add('field', fb.cylinder((0, 0, .0065), .58, .013, sides=24), M('HK_Rug_Teal'))
    p.add('ring', ring(0, 0, .0135, .46, .42, .0015, sides=24), M('HK_Rug_Cream'))
    p.add('centre', fb.cylinder((0, 0, .0140), .14, .0020, sides=12), M('HK_Rug_Rust'))
    return p


def rug_runner():
    p = fb.Prop('HK_Rug_Runner')
    p.add('border', fb.zbox(-1.0, 1.0, -.35, .35, 0, .010), M('HK_Rug_Cream'))
    p.add('field', fb.zbox(-.92, .92, -.27, .27, 0, .012), M('HK_Rug_Rust'))
    for k in range(5):
        x = -.72 + k * .36
        p.add('diamond', fb.loft([(x, 0, .0115, .11, .16), (x, 0, .0135, .11, .16)], sides=4, phase=0), M('HK_Rug_Cream'))
    for s in (-1, 1):
        x0, x1 = sorted((s * .999, s * 1.05))
        p.add('fringe', fb.zbox(x0, x1, -.31, .31, 0, .004), M('GH_Lace'))
    return p


def leaf(length, width, tilt, azimuth, base, mat_name='HK_Leaf'):
    """A leaf blade: a flat loft up Z, tilted outward by tilt degrees, turned to azimuth, from a base point."""
    rings = [(0, 0, 0.0, .012, .004), (0, 0, length * .45, width / 2, .006), (0, 0, length * .85, width * .32, .005)]
    bm = fb.loft(rings, sides=6, cap_bottom=True, cap_top=False, tip=(0, 0, length))
    bm = fb.turn(bm, -tilt, 'X')
    bm = fb.turn(bm, azimuth, 'Z')
    return fb.move(bm, base)


def plant():
    """A pot plant, 0.62 tall: a terracotta pot, soil, nine leaves."""
    p = fb.Prop('HK_Plant')
    p.add('pot', spin([(.10, 0), (.10, .005), (.09, .02), (.11, .20), (.125, .23), (.125, .25), (.112, .25)], sides=12), M('HK_Terracotta'))
    p.add('soil', fb.cylinder((0, 0, .235), .108, .02, sides=12), M('HK_Soil'))
    for k in range(7):
        az = k * 360 / 7 + 10
        p.add('leaf', leaf(.34 + .03 * (k % 3), .11, 48 + 6 * (k % 2), az, (0, 0, .235)), M('HK_Leaf' if k % 3 else 'HK_Leaf_Light'))
    for k in range(2):
        p.add('leaf', leaf(.38, .09, 14, 60 + 180 * k, (0, 0, .235)), M('HK_Leaf'))
    return p


def books_row():
    """Nine books in a row, 0.40 long, on a shelf or a sideboard; the last leans on an oak book end."""
    p = fb.Prop('HK_Books_Row')
    mats = ['HK_Book_Red', 'HK_Book_Blue', 'HK_Book_Green', 'HK_Book_Cream', 'HK_Book_Blue', 'HK_Book_Red', 'HK_Book_Cream', 'HK_Book_Green', 'HK_Book_Blue']
    heights = [.22, .19, .24, .20, .21, .18, .23, .20, .19]
    thick = [.035, .028, .045, .03, .038, .025, .04, .03, .032]
    x = -.20
    for i in range(9):
        t = thick[i]
        bm = fb.rbox(x, x + t, -.075, .075, 0, heights[i], bevel=.003)
        if i == 8:
            bm = fb.turn(bm, -14, 'Y', (x + t, 0, 0))
        p.add('book', bm, M(mats[i]))
        p.add('pages', fb.zbox(x + .003, x + t - .003, -.072, -.0745, .006, heights[i] - .006), M('HK_Paper'))
        x += t + .001
    p.add('book end', fb.zbox(x + .055, x + .063, -.08, .08, 0, .14), M('HK_Oak'))
    p.add('book end foot', fb.zbox(x - .02, x + .063, -.08, .08, 0, .006), M('HK_Oak'))
    return p


def book_open():
    p = fb.Prop('HK_Book_Open')
    for s in (-1, 1):
        x0, x1 = sorted((0, s * .125))
        cover = fb.rbox(x0, x1, -.09, .09, 0, .006, bevel=.002)
        p.add('cover', fb.turn(cover, s * 6, 'Y', (0, 0, 0)), M('HK_Book_Green'))
        pages = fb.zbox(min(x0, x1) + .004, max(x0, x1) - .004, -.085, .085, .005, .024)
        p.add('pages', fb.turn(pages, s * 6, 'Y', (0, 0, 0)), M('HK_Paper'))
    p.add('spine', fb.zbox(-.012, .012, -.09, .09, 0, .008), M('HK_Book_Green'))
    return p


def wall_clock_fixed():
    """The clock built round its centre at z 0.15 (the first draft above kept the face on the floor)."""
    import bmesh
    p = fb.Prop('HK_Clock', floor=False)
    c = (0, 0, .15)
    # the rim and face are lathes about Y: build them about Z then turn them to face -Y
    rim = ring(0, 0, 0, .15, .125, .04, sides=20)
    rim = fb.turn(rim, 90, 'X')             # now its axis is along Y, spanning y 0..-0.04
    p.add('rim', fb.move(rim, c), M('HK_Oak'))
    face = fb.cylinder((0, 0, 0), .13, .006, sides=20)
    face = fb.turn(face, 90, 'X')
    p.add('face', fb.move(face, (0, -.030, .15)), M('HK_Clock_Face'))
    for h in range(12):
        a = math.radians(h * 30)
        r = .11
        mark = fb.zbox(-.004, .004, -.038, -.032, r - .012, r + .012)
        mark = fb.turn(mark, h * 30, 'Y', (0, 0, 0))
        p.add('mark', fb.move(mark, c), M('HK_Ink'))
    hour = fb.turn(fb.zbox(-.006, .006, -.040, -.034, -.012, .07), -60, 'Y')
    p.add('hour hand', fb.move(hour, c), M('HK_Ink'))
    minute = fb.turn(fb.zbox(-.004, .004, -.042, -.036, -.012, .105), 150, 'Y')
    p.add('minute hand', fb.move(minute, c), M('HK_Ink'))
    p.add('pin', fb.cylinder((0, -.040, .15), .008, .012, sides=8, rot=(90, 0, 0)), M('T2_Brass'))
    p.add('back', fb.cylinder((0, -.002, .15), .128, .004, sides=20, rot=(90, 0, 0)), M('HK_Oak'))
    return p


def mug():
    p = fb.Prop('HK_Mug')
    cer = M('DC_Ceramic')
    p.add('body', spin([(.038, 0), (.040, .004), (.040, .090), (.042, .095), (.034, .095), (.034, .090), (.032, .012)], sides=12, cap_top=False), cer)
    p.add('band', ring(0, 0, .060, .0415, .0395, .018, sides=12), M('GH_Delft_Blue'))
    # the handle: three short boxes making a C on the +X side
    p.add('handle', fb.zbox(.038, .062, -.008, .008, .070, .080), cer)
    p.add('handle', fb.zbox(.054, .064, -.008, .008, .028, .080), cer)
    p.add('handle', fb.zbox(.038, .062, -.008, .008, .022, .032), cer)
    return p


def plates():
    """A stack of three plates."""
    p = fb.Prop('HK_Plates')
    cer = M('DC_Ceramic')
    for i in range(3):
        z = i * .014
        p.add('plate', spin([(.07, z), (.118, z + .013), (.120, z + .016), (.095, z + .008), (.0, z + .008)], sides=12, cap_bottom=True, cap_top=False), cer)
    p.add('rim line', ring(0, 0, .044, .116, .112, .0015, sides=12), M('GH_Delft_Blue'))
    return p


def envelope(p, cx, cy, z, turn, label='envelope'):
    env = fb.zbox(-.11, .11, -.055, .055, 0, .004)
    env = fb.turn(env, turn, 'Z')
    p.add(label, fb.move(env, (cx, cy, z)), M('HK_Paper'))
    stamp = fb.turn(fb.zbox(.07, .095, .02, .045, .004, .0055), turn, 'Z')
    p.add('stamp', fb.move(stamp, (cx, cy, z)), M('HK_Stamp'))
    for k in range(3):
        line = fb.turn(fb.zbox(-.07, -.01 + .015 * k, -.012 + .012 * k - .002, -.012 + .012 * k + .002, .004, .005), turn, 'Z')
        p.add('address', fb.move(line, (cx, cy, z)), M('HK_Ink'))


def post_pile():
    p = fb.Prop('HK_Post_Pile')
    for i, (dx, dy, t) in enumerate(((0, 0, 0), (.012, .006, 12), (-.01, .012, -8), (.02, -.006, 22))):
        envelope(p, dx, dy, i * .0045, t)
    return p


def letter():
    p = fb.Prop('HK_Letter')
    envelope(p, 0, 0, 0, 0)
    return p


def shoe(p, cy, turn, mat='HK_Leather_Brown'):
    """One shoe, 0.28 long along x (the toe at +x), set cy across (y) from the pair's middle."""
    sole = fb.rbox(-.14, .14, -.05, .05, 0, .022, bevel=.008)
    upper = fb.loft([(.02, 0, .020, .10, .045), (.00, 0, .055, .085, .040), (-.04, 0, .085, .060, .030)], sides=10, cap_bottom=False, cap_top=True)
    heel = fb.rbox(-.14, -.08, -.045, .045, .02, .055, bevel=.01)
    for part, mat_ in ((sole, 'HK_Sole'), (upper, mat), (heel, mat)):
        p.add('shoe', fb.move(fb.turn(part, turn, 'Z'), (0, cy, 0)), M(mat_))


def shoes():
    """A pair of shoes side by side (the first kit set them end to end, one through the other), toes toward +x."""
    p = fb.Prop('HK_Shoes')
    shoe(p, -.062, 4)
    shoe(p, .062, -7)
    return p


def slippers():
    p = fb.Prop('HK_Slippers')
    for s in (-1, 1):
        sole = fb.rbox(-.13, .13, -.05, .05, 0, .018, bevel=.008)
        band = fb.loft([(.03, 0, .016, .095, .05), (.00, 0, .045, .08, .045)], sides=10, cap_bottom=False, cap_top=True)
        for part, mat_ in ((sole, 'HK_Sole'), (band, 'HK_Slipper')):
            p.add('slipper', fb.move(fb.turn(part, s * 5, 'Z'), (0, s * .058, 0)), M(mat_))
    return p


def hook_rail():
    """A rail with three hooks, 0.50 long: origin at the bottom centre of its back (y = 0)."""
    p = fb.Prop('HK_HookRail', floor=False)
    p.add('rail', fb.rbox(-.25, .25, -.022, 0, 0, .08, bevel=.004), M('HK_Oak'))
    for x in (-.17, 0, .17):
        p.add('hook plate', fb.zbox(x - .015, x + .015, -.028, -.02, .015, .065), M('DC_SteelDark'))
        p.add('hook', fb.strut((x, -.06, .062), (x, -.026, .04), .006, .006, sides=6), M('DC_SteelDark'))
        p.add('hook tip', fb.ellipsoid((x, -.062, .064), (.009, .009, .009), u=6, v=4), M('DC_SteelDark'))
    return p


def coat():
    """A coat hanging from a hook: origin at the hook (the collar), hanging down -Z; its back toward +Y (the wall)."""
    p = fb.Prop('HK_Coat', floor=False)
    camel = M('GH_Wool_Camel')
    rings = [(0, -.06, -.02, .055, .035), (0, -.07, -.09, .19, .075), (0, -.075, -.40, .20, .085),
             (0, -.08, -.75, .215, .095), (0, -.08, -.92, .22, .10)]
    body = fb.loft(rings, sides=10, cap_bottom=True, cap_top=True)
    p.add('coat', body, camel)
    p.add('collar', fb.loft([(0, -.06, -.01, .08, .05), (0, -.06, -.07, .12, .06)], sides=10), camel)
    for s in (-1, 1):
        p.add('sleeve', fb.loft([(s * .21, -.07, -.14, .055, .05), (s * .23, -.075, -.60, .05, .045)], sides=8), camel)
    p.add('loop', fb.strut((0, -.06, .0), (0, -.06, -.03), .004, .004, sides=4), M('HK_Cord_Black'))
    p.add('scarf', fb.loft([(.05, -.12, -.06, .035, .02), (.07, -.13, -.45, .04, .018), (.08, -.13, -.62, .05, .015)], sides=6), M('GH_Scarf_Red'))
    return p


def toaster():
    p = fb.Prop('HK_Toaster')
    p.add('body', fb.rbox(-.14, .14, -.085, .085, 0, .19, bevel=.02), M('HK_Enamel_Cream'))
    for y in (-.03, .03):
        p.add('slot', fb.zbox(-.10, .10, y - .007, y + .007, .185, .194), M('HK_Ink'))
    p.add('lever', fb.zbox(.145, .165, -.012, .012, .09, .11), M('DC_Steel'))
    p.add('lever slot', fb.zbox(.139, .146, -.004, .004, .05, .15), M('HK_Ink'))
    p.add('dial', fb.cylinder((.143, .045, .06), .012, .014, sides=8, rot=(0, 90, 0)), M('DC_Steel'))
    for s in (-1, 1):
        p.add('foot', fb.zbox(s * .11 - .015, s * .11 + .015, -.06, .06, -.0, .012), M('HK_Ink'))
    return p


def kitchen_bin():
    p = fb.Prop('HK_Bin')
    g = M('HK_Bin_Grey')
    p.add('body', spin([(.14, 0), (.15, .01), (.165, .50), (.17, .52), (.155, .52), (.145, .03), (0, .03)], sides=14, cap_top=False), g)
    p.add('lid', spin([(.16, .52), (.175, .53), (.17, .555), (.0, .56)], sides=14), g)
    p.add('lid knob', fb.ellipsoid((0, 0, .565), (.02, .02, .012), u=8, v=4), M('DC_SteelDark'))
    p.add('pedal', fb.zbox(-.05, .05, -.22, -.14, 0, .025), M('DC_SteelDark'))
    p.add('pedal arm', fb.zbox(-.01, .01, -.16, -.12, 0, .02), M('DC_SteelDark'))
    return p


def newspaper():
    p = fb.Prop('HK_Newspaper')
    p.add('paper', fb.rbox(-.15, .15, -.10, .10, 0, .012, bevel=.002), M('HK_Paper'))
    p.add('fold', fb.zbox(-.15, .15, -.004, .004, .012, .016), M('HK_Paper'))
    p.add('masthead', fb.zbox(-.12, .02, .05, .08, .012, .0135), M('HK_Ink'))
    for k in range(3):
        p.add('column', fb.zbox(-.12 + k * .09, -.05 + k * .09, -.08, .035, .012, .0128), M('HK_Ink'))
    return p


def pendant():
    """
    A pendant light on a short drop (0.36: the rooms are 2.25 and 2.45 high and Ace is 2.04): a ceiling rose at
    z 0, a cord, a brass holder, a linen drum shade with an inside; it hangs from its origin (floor=False).
    """
    p = fb.Prop('HK_Pendant', floor=False)
    brass = M('T2_Brass')
    p.add('rose', spin([(.075, -.02), (.075, -.004), (.06, 0), (0, 0)], sides=14), M('HK_Trim_Paint'))
    p.add('cord', fb.cylinder((0, 0, -.085), .004, .14, sides=6), M('HK_Cord_Black'))
    p.add('holder', fb.cylinder((0, 0, -.165), .018, .05, sides=8), brass)
    shade = [(.13, -.36), (.14, -.35), (.14, -.19), (.13, -.18)]
    outer = spin(shade, sides=16, cap_bottom=False, cap_top=True)
    inner = spin([(r - .006, z) for r, z in ((.14, -.348), (.14, -.192))], sides=16, cap_bottom=False, cap_top=False)
    for f in inner.faces:
        f.normal_flip()
    fb.merge(outer, inner)
    p.add('shade', outer, M('GH_Lampshade'))
    p.add('bulb', fb.ellipsoid((0, 0, -.25), (.03, .03, .04), u=8, v=5), M('DC_Ceramic'))
    return p


def strip_light():
    """A strip light for under the kitchen cupboards, 0.60 long: it hangs from its origin (the cupboard's underside)."""
    p = fb.Prop('HK_StripLight', floor=False)
    p.add('housing', fb.rbox(-.30, .30, -.025, .025, -.03, 0, bevel=.004), M('HK_Enamel_Cream'))
    p.add('tube', fb.cylinder((0, 0, -.02), .012, .54, sides=8, rot=(0, 90, 0)), M('DC_Ceramic'))
    return p


# ============================================================================= D. the door from the street


def door_panelled():
    """
    A panelled front door, 0.98 x 2.19 x 0.045: the hinge line at x = 0 on the floor, the back at y = 0, the
    street toward -Y (the panels and furniture stand proud of the front). Two raised panels below, a glazed
    panel above with a cross of glazing bars, a letterbox, a knocker and a knob.
    """
    p = fb.Prop('HK_Door_Panelled')
    paint = M('HK_Door_Paint')
    w, h, t = .98, 2.19, .045
    p.add('leaf', fb.rbox(0, w, -t, 0, 0, h, bevel=.004), paint)
    # the lower panels: a recess (a frame proud of the leaf) with a raised field inside it
    for x0, x1 in ((.09, .455), (.525, .89)):
        z0, z1 = .14, .96
        for (a, b, c, d) in ((x0, x1, z0, z0 + .05), (x0, x1, z1 - .05, z1), (x0, x0 + .05, z0 + .05, z1 - .05), (x1 - .05, x1, z0 + .05, z1 - .05)):
            p.add('panel moulding', fb.zbox(a, b, -t - .012, -t + .002, c, d), paint)
        p.add('panel field', fb.rbox(x0 + .05, x1 - .05, -t - .009, -t + .002, z0 + .05, z1 - .05, bevel=.006), paint)
    # the middle rail (between the lower panels and the glass) and the glazed panel
    gx0, gx1, gz0, gz1 = .12, .86, 1.20, 1.96
    for (a, b, c, d) in ((gx0 - .03, gx1 + .03, gz0 - .03, gz0), (gx0 - .03, gx1 + .03, gz1, gz1 + .03), (gx0 - .03, gx0, gz0, gz1), (gx1, gx1 + .03, gz0, gz1)):
        p.add('glazing bead', fb.zbox(a, b, -t - .010, -t + .002, c, d), paint)
    p.add('glass', fb.zbox(gx0, gx1, -t / 2 - .003, -t / 2 + .003, gz0, gz1), M('HK_Door_Glass'))
    p.add('glazing bar', fb.zbox((gx0 + gx1) / 2 - .012, (gx0 + gx1) / 2 + .012, -t - .006, .006, gz0, gz1), paint)
    p.add('glazing bar', fb.zbox(gx0, gx1, -t - .006, .006, (gz0 + gz1) / 2 - .012, (gz0 + gz1) / 2 + .012), paint)
    # the furniture
    brass = M('T2_Brass')
    p.add('letterbox plate', fb.rbox(.33, .65, -t - .012, -t + .002, 1.02, 1.11, bevel=.004), brass)
    p.add('letterbox flap', fb.zbox(.36, .62, -t - .014, -t - .010, 1.045, 1.095), M('HK_Iron_Black'))
    p.add('knocker ring', torus(.49, -t - .030, 1.50, .045, .009, sides=12, rings_n=6, rot=(90, 0, 0)), brass)
    p.add('knocker boss', fb.cylinder((.49, -t - .012, 1.55), .02, .024, sides=8, rot=(90, 0, 0)), brass)
    p.add('knob rose', fb.cylinder((.86, -t - .010, 1.04), .035, .02, sides=10, rot=(90, 0, 0)), brass)
    p.add('knob stem', fb.cylinder((.86, -t - .035, 1.04), .012, .04, sides=8, rot=(90, 0, 0)), brass)
    p.add('knob', fb.ellipsoid((.86, -t - .065, 1.04), (.034, .028, .034), u=8, v=6), brass)
    p.add('escutcheon', fb.zbox(.835, .885, -t - .004, -t + .002, .88, .96), brass)
    return p


def porch_lantern_fixed():
    """A coach lantern on a bracket, 0.25 tall: origin at the bottom centre of its wall plate (the wall at y = 0); the glass glows in Unity."""
    p = fb.Prop('HK_PorchLantern', floor=False)
    iron = M('HK_Iron_Black')
    p.add('wall plate', fb.rbox(-.05, .05, -.012, 0, 0, .12, bevel=.004), iron)
    p.add('arm', fb.zbox(-.012, .012, -.14, -.008, .044, .068), iron)
    p.add('arm brace', fb.strut((0, -.13, .044), (0, -.012, .006), .007, .007, sides=4), iron)
    cy = -.13
    base = spin([(.0, .0), (.07, .0), (.08, .01), (.08, .02), (.06, .03), (.055, .036)], sides=6, phase=30)
    p.add('lantern base', fb.move(base, (0, cy, .068)), iron)
    top = spin([(.055, .0), (.085, .006), (.085, .018), (.06, .041), (.025, .076), (.0, .086)], sides=6, phase=30)
    p.add('lantern top', fb.move(top, (0, cy, .154)), iron)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        p.add('post', fb.cylinder((math.cos(a) * .062, cy + math.sin(a) * .062, .113), .006, .10, sides=4), iron)
    glass = spin([(.058, .0), (.058, .056)], sides=6, phase=30, cap_bottom=True, cap_top=True)
    p.add('glass', fb.move(glass, (0, cy, .102)), M('HK_Lantern_Glass'))
    p.add('bulb', fb.ellipsoid((0, cy, .128), (.018, .018, .024), u=8, v=5), M('DC_Ceramic'))
    return p


# ============================================================================= the list


PIECES = [
    # (builder, group, what it is, stretched along x in Unity)
    (skirting, 'Finish', 'skirting board, 1.00 m (stretch x)', True),
    (dado_rail, 'Finish', 'dado rail, 1.00 m, for the sill-cut seam at 0.78 (stretch x)', True),
    (cornice, 'Finish', 'cove cornice, 1.00 m (stretch x)', True),
    (lambda: inside_corner('HK_Skirting_Corner', SKIRTING_PROFILE), 'Finish', 'skirting, an inside corner, mitred (0.10 each way)', False),
    (lambda: inside_corner('HK_DadoRail_Corner', DADO_PROFILE), 'Finish', 'dado rail, an inside corner, mitred (0.10 each way)', False),
    (lambda: inside_corner('HK_Cornice_Corner', cornice_profile()), 'Finish', 'cornice, an inside corner, mitred (0.10 each way)', False),
    (architrave, 'Finish', 'architrave moulding, 1.00 m (stretch x; turn it for the jambs)', True),
    (lining_board, 'Finish', 'a reveal lining board, 1.00 x 0.20 (stretch x and y)', True),
    (threshold, 'Finish', 'threshold, 1.00 m (stretch x)', True),
    (window_sill, 'Finish', 'window sill, 1.00 m (stretch x)', True),
    (curtain_panel, 'Window', 'one curtain panel, gathered, 1.50 long (hangs from the rod)', False),
    (curtain_rod, 'Window', 'curtain rod, 1.00 m, the rod alone (stretch x)', True),
    (curtain_rod_end, 'Window', 'a curtain rod end: finial and bracket (one each end, the second mirrored)', False),
    (roller_blind, 'Window', 'roller blind, 1.00 m, half down (stretch x)', True),
    (doormat, 'Lived in', 'doormat', False),
    (radiator, 'Lived in', 'panel radiator, 1.00 x 0.60', False),
    (lambda: picture('HK_Picture_Hills', .50, .36, .04, 'CC_Wood_Espresso', 'HK_Painting_Hills'), 'Lived in', 'picture: hills', False),
    (lambda: picture('HK_Picture_Harbour', .56, .38, .035, 'HK_Oak', 'HK_Painting_Harbour'), 'Lived in', 'picture: the harbour', False),
    (lambda: picture('HK_Picture_Still', .34, .42, .03, 'T2_Brass', 'HK_Painting_Still'), 'Lived in', 'picture: a still life', False),
    (lambda: picture('HK_Picture_Portrait', .30, .38, .035, 'CC_Wood_Counter', 'HK_Painting_Portrait'), 'Lived in', 'picture: a portrait', False),
    (cushion, 'Lived in', 'cushion', False),
    (rug_round, 'Lived in', 'round rug, 1.40', False),
    (rug_runner, 'Lived in', 'hall runner, 2.00 x 0.70', False),
    (plant, 'Lived in', 'pot plant', False),
    (books_row, 'Lived in', 'a row of books, 0.40', False),
    (book_open, 'Lived in', 'an open book', False),
    (wall_clock_fixed, 'Lived in', 'wall clock, 0.30', False),
    (mug, 'Lived in', 'mug', False),
    (plates, 'Lived in', 'a stack of plates', False),
    (post_pile, 'Lived in', 'a pile of post', False),
    (letter, 'Lived in', 'one letter', False),
    (shoes, 'Lived in', 'a pair of shoes', False),
    (slippers, 'Lived in', 'slippers', False),
    (hook_rail, 'Lived in', 'hook rail, three hooks', False),
    (coat, 'Lived in', 'a coat and scarf on a hook (hangs from the hook)', False),
    (toaster, 'Lived in', 'toaster', False),
    (kitchen_bin, 'Lived in', 'pedal bin', False),
    (newspaper, 'Lived in', 'a folded newspaper', False),
    (pendant, 'Lived in', 'pendant light (hangs from the ceiling)', False),
    (strip_light, 'Lived in', 'strip light, 0.60 (hangs under a cupboard)', False),
    (door_panelled, 'Door', 'panelled front door, 0.98 x 2.19 (hinge at x 0)', False),
    (porch_lantern_fixed, 'Door', 'porch lantern on a bracket', False),
]


def build_all():
    out = []
    for builder, group, what, stretch in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'group': group, 'what': what, 'stretch': stretch, 'islands': n, 'floating': floating, **st})
    return out
