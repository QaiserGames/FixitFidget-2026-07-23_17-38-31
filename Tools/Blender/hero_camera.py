"""
Grace's camera, v3 (9 Oct 2026): GraceReunionCamera.prefab's pieces (a cube body, a cylinder lens housing and glass, a
cube film path, a cube shutter mechanism with two cube blades, cube straps) as real meshes, built from a 1970s
rangefinder (claude/hero-reference-sheet.md: the Canonet QL17 GIII and the Olympus 35 SP, measured) at the prefab's 2x
size, in the house style (hero_lib.py), to the prefab's sizes and places so Devices 2 swaps the meshes and nothing the
bench or the Grace checks know moves. The grime spots and the strap's old scratches stay what they are.

The prefab's frame (Unity): the camera lies on its back, its front face up (+Y); the lens at -X; at +X an opening with
the film transport (toward -Z) and the shutter mechanism (toward +Z); the close-up camera looks from -Z. Blender here
(hero_lib.to_blender: Unity (x, y, z) = Blender (-x, z, -y)): Z through (+Z the front), the lens at +X, the opening at
-X, the film path at +Y, the mechanism at -Y, and the player looks from +Y.

The fiction, made plausible: the camera's TOP PLATE runs along the +Y edge, the edge nearest the player (rewind crank
at the lens end, hot shoe over the lens, shutter release and advance lever at the other end, strap eyelets; the
three finder windows in its front face), the bottom plate along -Y, leatherette between; at -X the front
panel is OFF for service, a well under the top plate's chrome lintel, and in it the film transport (the gate with its
steel rails, the sprocket drum, the take-up spool) nearer the player and the leaf-shutter assembly (the shutter drum
with its gear train, cocking lever and spring, the two blades on top) behind it. From the player's seat the lens is
left of centre and the opening is on the right, as a Canonet's lens and its advance side are.

  Camera body        240 x 160 x 46, centred on its origin (23 mm up in the prefab), vertical edges rounded 12 mm.
  Lens housing       along its Y (Unity up): a 76 mm barrel with a chrome base ring, a black focus ring with its tab,
                     two knurled chrome rings, the black name ring and the meter window; top at Unity y 0.091.
  Lens glass         the front element, a dark dome 57 mm across in a chrome retaining ring; apex under the grime (0.0965).
  Open film path     a black chamber floor with the film gate between two polished rails, the sprocket drum (under the
                     film-guide grime) and the take-up spool; its top under the two grime boxes (Unity 0.0545).
  Jammed shutter mechanism   the chassis in the well, the shutter drum with a chrome rim, three brass gears, a lever, a spring.
  Bent / Working shutter blade   thin steel leaf-shutter blades (a petal shape in the 55 x 39 box); the bent one kinks up.
  Upper strap / Lower strap / Worn loop   tan leather, stitched; a slide buckle on the strap; the loop frayed.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from hero_lib import Faceted, chamfered_slab, disc, dome, gear, moved, revolve, ring_rrect, ring_wall, sweep, to_blender, tube  # noqa: E402
from hero_watch import prism  # noqa: E402

HD_MATERIALS = {
    'HD_CamLeather':  ('#2A2623', 0.25, 0.0),   # the body's leatherette
    'HD_CamChrome':   ('#B9B5AD', 0.50, 0.0),   # satin chrome: plates, rings, rails, the drum's rim
    'HD_CamBlack':    ('#1F1D1B', 0.40, 0.0),   # black paint and plastic: the lens rings, the gate, the chassis, the well
    'HD_CamGlass':    ('#1A2430', 0.70, 0.0),   # the lens, the finder windows
    'HD_CamMilk':     ('#D9D7D0', 0.50, 0.0),   # the frosted frame-line illuminator
    'HD_CamSteel':    ('#8E8B85', 0.45, 0.0),   # the shutter blades, levers, screws
    'HD_CamBrass':    ('#B08A3E', 0.50, 0.0),   # the gear train
    'HD_CamStrap':    ('#7A4A28', 0.30, 0.0),   # tan leather
    'HD_CamStitch':   ('#D9C7A6', 0.30, 0.0),   # the strap's stitching
    'HD_CamWhite':    ('#F2EFE8', 0.30, 0.0),   # index marks on the rings
}

PREFAB_OFFSETS = {        # Unity's frame; the body is the root at (0, 0.023, 0) in the prefab, the origin here
    'LensHousing':  (-0.065, 0.055 - 0.023, -0.024),
    'LensGlass':    (-0.065, 0.093 - 0.023, -0.024),
    'FilmPath':     (0.056, 0.049 - 0.023, -0.043),
    'Mechanism':    (0.053, 0.060 - 0.023, 0.041),
    'BentBlade':    (0.053, 0.060 + 0.0123 - 0.023, 0.041),
    'WorkingBlade': (0.053, 0.060 - 0.010 - 0.023, 0.041),      # in the FBX only: inside the housing, out of the bent one's way
    'Strap':        (-0.162, 0.026 - 0.023, -0.063),
    'WornLoop':     (-0.200, 0.026 - 0.023, 0.0),
}

HX, HY, HZ = .120, .080, .023      # the body's half sizes
WELL_FLOOR = .036 - .023           # the opening's floor, Unity y 0.036, in the body's frame (+0.013)
WELL = (-.104, -.010, -.0655, .0635)   # x0, x1, y0, y1 of the opening (Blender: Unity x 10..104 is Blender -104..-10)
BAND = (.050, .080)                # the top plate's front band (y): the NEAR edge for the player; over the opening only its lintel (y 0.0635 .. 0.080)
LENS = (.065, .024)                # the lens axis (x, y here): Unity (-0.065, z -0.024)


def M(name):
    return hl.material(HD_MATERIALS, name)


def block_loop(x0, x1, y0, y1, z, r_left=0.0, r_right=0.0, per_corner=2):
    """A rectangle loop (CCW) whose -X corners are rounded by r_left and +X corners by r_right."""
    pts = []

    def corner(cx, cy, a0, r):
        if r <= 0:
            return [(cx, cy, z)]
        out = []
        for k in range(per_corner + 1):
            a = math.radians(a0 + 90.0 * k / per_corner)
            out.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
        return out

    pts += corner(x1 - r_right, y0 + r_right, -90.0, r_right)
    pts += corner(x1 - r_right, y1 - r_right, 0.0, r_right)
    pts += corner(x0 + r_left, y1 - r_left, 90.0, r_left)
    pts += corner(x0 + r_left, y0 + r_left, 180.0, r_left)
    return pts


def block(x0, x1, y0, y1, z0, z1, r_left=0.0, r_right=0.0, chamfer=.0012):
    """A body block: rounded on the ends asked for, one chamfer into the front and back faces."""
    c = min(chamfer, (z1 - z0) / 2.2)
    loops = [block_loop(x0 + c, x1 - c, y0 + c, y1 - c, z0, max(0, r_left - c), max(0, r_right - c)),
             block_loop(x0, x1, y0, y1, z0 + c, r_left, r_right),
             block_loop(x0, x1, y0, y1, z1 - c, r_left, r_right),
             block_loop(x0 + c, x1 - c, y0 + c, y1 - c, z1, max(0, r_left - c), max(0, r_right - c))]
    return tube(loops)


def body():
    p = Faceted('HD_Camera')
    leather, chrome, black, glass, milk, steel, white = (M('HD_CamLeather'), M('HD_CamChrome'), M('HD_CamBlack'), M('HD_CamGlass'),
                                                        M('HD_CamMilk'), M('HD_CamSteel'), M('HD_CamWhite'))
    wx0, wx1, wy0, wy1 = WELL
    R = .012
    # ---- the leatherette body, in blocks round the opening (the opening is at -X; the lens end is +X)
    p.add('body end', block(-HX, wx0, -HY, HY, -HZ, HZ, r_left=R), leather)
    p.add('body lens side', block(wx1, HX, -HY, HY, -HZ, HZ, r_right=R), leather)
    p.add('body under the well', block(wx0, wx1, -HY, HY, -HZ, WELL_FLOOR, chamfer=.0006), leather)
    p.add('body far strip', block(wx0, wx1, -HY, wy0, -HZ, HZ, chamfer=.0006), leather)
    p.add('body lintel', block(wx0, wx1, wy1, HY, -HZ, HZ, chamfer=.0006), leather)
    # the well's lining: black floor and walls (the inside of a camera)
    p.add('well floor', fb.zbox(wx0, wx1, wy0, wy1, WELL_FLOOR, WELL_FLOOR + .0008), black)
    for (x0, x1, y0, y1) in ((wx0, wx0 + .0008, wy0, wy1), (wx1 - .0008, wx1, wy0, wy1), (wx0, wx1, wy0, wy0 + .0008), (wx0, wx1, wy1 - .0008, wy1)):
        p.add('well wall', fb.zbox(x0, x1, y0, y1, WELL_FLOOR, HZ - .0005), black)
    # ---- the chrome plates: the top plate's front band (30 mm; a 16 mm lintel over the opening), its top face, the bottom plate
    b0, b1 = BAND
    pz0, pz1 = -HZ - .0010, HZ + .0010
    p.add('top band, opening end', block(-HX - .0010, wx0, b0, HY + .0010, pz0, pz1, r_left=R + .001), chrome)
    p.add('top band, lens side', block(wx1, HX + .0010, b0, HY + .0010, pz0, pz1, r_right=R + .001), chrome)
    p.add('top band lintel', block(wx0, wx1, wy1, HY + .0010, pz0, pz1, chamfer=.0008), chrome)
    p.add('bottom plate', block(-HX - .0010, HX + .0010, -HY - .0010, -HY + .0135, pz0, pz1, r_left=R + .001, r_right=R + .001), chrome)
    # ---- the front face: the three finder windows in the band's face (the rangefinder's over the lens, the frosted
    # illuminator and the big viewfinder toward the middle), the badge, the self-timer, the lens boss
    zf = HZ + .0010
    for (cx, w, h, mat) in ((LENS[0], .0110, .0110, glass), (.0400, .0170, .0110, milk), (.0120, .0260, .0130, glass)):
        cy = (b0 + b1) / 2
        p.add('window frame', fb.zbox(cx - w / 2 - .0012, cx + w / 2 + .0012, cy - h / 2 - .0012, cy + h / 2 + .0012, zf, zf + .0006), black)
        p.add('window', fb.zbox(cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2, zf, zf + .0010), mat)
    # the lens boss (no badge and no self-timer lever: ornament that does nothing for the player)
    lx, ly = LENS
    p.add('lens boss', moved(ring_wall(HZ, HZ + .0025, .0400, .0450, sides=48), lx, ly, 0), chrome)
    # ---- the top face (y = +HY, toward the player): rewind crank at the lens end, hot shoe over the lens, frame counter,
    # shutter release and advance lever at the opening end, eyelets
    yt = HY + .0010

    def on_top(bm):
        """A part built standing on z = 0 (up +Z; its +Y toward the camera's front face) turned to stand on the top face (up +Y)."""
        fb.turn(bm, -90.0, 'X')
        return moved(bm, 0, yt, 0)

    rx = .0960
    knob = revolve([(0.0, .0140), (.0050, .0140), (.0060, .0120)], sides=24, knurl=(1, .03))
    p.add('rewind knob', on_top(moved(knob, rx, 0, 0)), chrome)
    p.add('rewind crank', on_top(fb.zbox(rx - .0110, rx + .0110, -.0015, .0015, .0060, .0075)), black)
    p.add('crank knob', on_top(disc(rx - .0090, 0.0, .0075, .0105, .0020, sides=8)), black)
    # the hot shoe over the lens: a black insulator plate under two chrome rails, the centre contact between
    shoe = fb.zbox(-.0200, .0200, -.0150, .0150, 0.0, .0020)
    p.add('hot shoe', on_top(moved(shoe, lx, 0, 0)), black)
    for sz in (1, -1):
        rail = fb.zbox(-.0200, .0200, sz * .0120 - .0025, sz * .0120 + .0025, .0020, .0040)
        p.add('shoe rail', on_top(moved(rail, lx, 0, 0)), chrome)
    p.add('shoe contact', on_top(disc(lx, 0.0, .0020, .0026, .0020, sides=8)), black)
    # the shutter release on its boss (a touch toward the front face)
    p.add('release boss', on_top(disc(-.0840, .0040, 0.0, .0030, .0110, sides=16)), chrome)
    p.add('shutter release', on_top(disc(-.0840, .0040, .0030, .0070, .0085, sides=16)), chrome)
    p.add('cable thread', on_top(disc(-.0840, .0040, .0070, .0074, .0022, sides=8)), black)
    # the advance lever, resting along the back edge, with its black tip
    arm = fb.zbox(-.0340, 0.0, -.0030, .0030, .0032, .0052)
    fb.turn(arm, -20.0, 'Z')
    p.add('advance lever', on_top(moved(arm, -.0840, -.0120, 0)), chrome)
    tip = fb.zbox(-.0400, -.0280, -.0045, .0045, .0028, .0062)
    fb.bevel_all(tip, .0010, segments=1)
    fb.turn(tip, -20.0, 'Z')
    p.add('lever tip', on_top(moved(tip, -.0840, -.0120, 0)), black)
    # the strap eyelets: at the lens end where the straps attach (both edges), one at the other end
    for (x, y) in ((HX + .0010, .0630), (HX + .0010, -.0630), (-HX - .0010, .0630)):
        eye = ring_wall(-.0030, .0030, .0025, .0045, sides=12)
        fb.turn(eye, 90.0, 'Y')
        p.add('strap eyelet', moved(eye, x, y, .0030), chrome)
    return p


def lens_housing():
    """Along the object's Y in Unity (Blender Z here): the barrel from inside the body (-36) to its front (+36)."""
    p = Faceted('LensHousing')
    chrome, black, white, glass = M('HD_CamChrome'), M('HD_CamBlack'), M('HD_CamWhite'), M('HD_CamGlass')
    S = 48
    p.add('base', revolve([(-.0360, .0360), (-.0090, .0360), (-.0090, .0380), (-.0040, .0380)], sides=S), black)
    p.add('base ring', revolve([(-.0085, .0385), (-.0040, .0385), (-.0040, .0370)], sides=S), chrome)
    p.add('focus ring', revolve([(-.0040, .0370), (-.0030, .0380), (.0050, .0380), (.0060, .0360)], sides=S), black)
    tab = fb.zbox(-.0080, .0080, .0340, .0450, -.0030, .0050)
    fb.bevel_all(tab, .0010, segments=1)
    fb.turn(tab, 130.0, 'Z')
    p.add('focus tab', tab, chrome)
    p.add('speed ring', revolve([(.0060, .0360), (.0070, .0375), (.0150, .0375), (.0160, .0350)], sides=96, knurl=(1, .025)), chrome)
    p.add('spacer', revolve([(.0150, .0345), (.0175, .0345)], sides=S), black)
    p.add('aperture ring', revolve([(.0170, .0350), (.0180, .0375), (.0260, .0375), (.0270, .0345)], sides=96, knurl=(1, .025)), chrome)
    p.add('name ring', revolve([(.0260, .0340), (.0340, .0340), (.0350, .0320), (.0360, .0300), (.0360, .0270), (.0330, .0265)], sides=S), black)
    # index marks: white ticks on the two chrome rings and an index line on the name ring
    for z0, z1, n in ((.0085, .0135, 7), (.0195, .0245, 6)):
        for k in range(n):
            tick = fb.zbox(-.0004, .0004, .0372, .0382, z0, z1)
            fb.turn(tick, -60.0 + 120.0 * k / (n - 1), 'Z')
            p.add('index', tick, white)
    p.add('index line', fb.zbox(-.0005, .0005, .0335, .0345, .0280, .0335), white)
    # the meter cell window at twelve o'clock inside the name ring
    p.add('meter window', fb.zbox(-.0050, .0050, .0230, .0300, .0340, .0356), glass)
    return p


def lens_glass():
    """Centred on its origin (Unity y 0.093): a chrome retaining ring and the dark front element, apex at +3.2 mm."""
    p = Faceted('LensGlass')
    glass, chrome = M('HD_CamGlass'), M('HD_CamChrome')
    p.add('retaining ring', revolve([(-.0030, .0255), (-.0030, .0285), (.0008, .0285), (.0014, .0262), (-.0010, .0255)], sides=48, cap_top=False), chrome)
    prof = [(-.0028, .0150), (-.0028, .0256), (.0004, .0256)] + dome(.0004, .0250, .0028, n=4)
    p.add('front element', revolve(prof, sides=48, tip=(0, 0, .0032)), glass)
    return p


def film_path():
    """Centred on its origin (Unity y 0.049): the chamber floor from the well's floor up; everything under +5.5 mm. The
    sprocket drum sits under the film-guide grime (Unity x 69..89: here local x -33..-13), the take-up spool at the other end."""
    p = Faceted('FilmPath')
    chrome, black, steel = M('HD_CamChrome'), M('HD_CamBlack'), M('HD_CamSteel')
    floor = WELL_FLOOR - (.049 - .023)         # the well's floor in this piece's frame: -0.013
    p.add('chamber floor', chamfered_slab(.080, .040, .0010 - floor, .0020, .0004, per_corner=1, centre_z=(floor + .0010) / 2), black)
    # the gate: a black recess between two polished rails
    p.add('gate', fb.zbox(-.0180, .0180, -.0120, .0120, .0010, .0014), black)
    for sy in (1, -1):
        p.add('film rail', fb.zbox(-.0290, .0290, sy * .0135 - .0015, sy * .0135 + .0015, .0010, .0025), chrome)
    p.add('gate edge', fb.zbox(-.0190, -.0180, -.0120, .0120, .0010, .0020), black)
    p.add('gate edge', fb.zbox(.0180, .0190, -.0120, .0120, .0010, .0020), black)

    def across(bm, x):
        fb.turn(bm, 90.0, 'X')                 # the axis along Y, across the film
        return moved(bm, x, 0, -.0030)

    p.add('sprocket drum', across(revolve([(-.0170, .0080), (.0170, .0080)], sides=16), -.0310), chrome)
    for yz in (-.0120, .0120):
        teeth = gear(0.0, 0.0, yz - .0012, yz + .0012, .0096, 12, depth=.18)
        p.add('sprocket teeth', across(teeth, -.0310), chrome)
    p.add('sprocket arbor', across(revolve([(-.0190, .0014), (.0190, .0014)], sides=8), -.0310), steel)
    p.add('take-up spool', across(revolve([(-.0170, .0075), (.0170, .0075)], sides=16), .0320), black)
    p.add('spool slot', fb.zbox(.0310, .0330, -.0150, .0150, .0040, .0050), chrome)
    for yz in (-.0160, .0160):
        p.add('spool flange', across(revolve([(yz - .0012, .0085), (yz + .0012, .0085)], sides=16), .0320), chrome)
    return p


def mechanism():
    """Centred on its origin (Unity y 0.060): the chassis from the well's floor, the shutter drum to +11; blades above."""
    p = Faceted('Mechanism')
    steel, black, brass, chrome = M('HD_CamSteel'), M('HD_CamBlack'), M('HD_CamBrass'), M('HD_CamChrome')
    floor = WELL_FLOOR - (.060 - .023)         # -0.024
    p.add('chassis', chamfered_slab(.073, .049, -.0060 - floor, .0030, .0006, per_corner=1, centre_z=(floor - .0060) / 2), black)
    p.add('shutter drum', revolve([(-.0060, .0210), (.0090, .0210)], sides=32), black)
    p.add('drum rim', ring_wall(.0090, .0105, .0180, .0210, sides=32), chrome)
    p.add('aperture', disc(0.0, 0.0, .0090, .0094, .0100, sides=24), M('HD_CamGlass'))
    p.add('drum step', ring_wall(.0090, .0098, .0100, .0130, sides=24), steel)
    for (gx, gy, gr, teeth, mat) in ((.0300, .0140, .0090, 14, brass), (.0300, -.0060, .0065, 10, brass), (-.0300, .0100, .0105, 18, brass), (-.0300, -.0120, .0050, 8, steel)):
        p.add('gear', gear(gx, gy, -.0060, -.0030, gr, teeth, depth=.16), mat)
        p.add('arbor', disc(gx, gy, -.0060, -.0010, .0012, sides=8), steel)
    lever = fb.zbox(0.0, .0300, -.0100, -.0060, -.0030, -.0016)
    fb.turn(lever, -15.0, 'Z', pivot=(.0300, -.0060, 0))
    p.add('cocking lever', lever, steel)
    helix = [(.0340 - .0200 * i / 60, -.0190 + .0022 * math.cos(2 * math.pi * 7 * i / 60), -.0040 + .0022 * math.sin(2 * math.pi * 7 * i / 60)) for i in range(61)]
    p.add('spring', sweep(helix, .0005, sides=4), steel)
    for (sx, sy) in ((.0330, .0220), (-.0330, -.0210), (-.0330, .0220)):
        p.add('chassis screw', disc(sx, sy, -.0060, -.0052, .0018, sides=8), steel)
    return p


def blade_outline():
    """A leaf-shutter blade: a petal from a pivot at one corner to a curved tip, inside the 55 x 39 box (CCW)."""
    pts = []
    pivot = (.0230, -.0150)
    tip = (-.0268, .0060)
    for k in range(13):
        t = k / 12
        pts.append((hl.bez(t, pivot[0], -.0050, tip[0]), hl.bez(t, pivot[1] + .0010, -.0180, tip[1])))
    for k in range(1, 13):
        t = k / 12
        pts.append((hl.bez(t, tip[0], -.0040, pivot[0] + .0040), hl.bez(t, tip[1] + .0010, .0210, pivot[1] + .0060)))
    # wound clockwise as listed (pivot, along the lower edge to the tip, back along the upper edge): reverse for CCW
    pts.reverse()
    return pts


def blade(bent):
    p = Faceted('BentBlade' if bent else 'WorkingBlade')
    steel = M('HD_CamSteel')
    outline = blade_outline()
    bm = prism(outline, -.0005, .0005)
    if bent:
        for v in bm.verts:
            if v.co.x < -.0040:
                s = (-.0040 - v.co.x) / .0230
                v.co.z += .0085 * s * s
    p.add('blade', bm, steel)
    p.add('pivot', disc(.0225, -.0145, -.0007, .0009, .0022, sides=10), M('HD_CamChrome'))
    return p


def strap(buckle=True):
    p = Faceted('Strap')
    leather, stitch, chrome = M('HD_CamStrap'), M('HD_CamStitch'), M('HD_CamChrome')
    p.add('band', chamfered_slab(.083, .017, .0040, .0030, .0008, per_corner=2, centre_z=.0025), leather)
    for y in (.0060, -.0060):
        for i in range(10):
            x = -.0370 + i * .0082
            p.add('stitch', fb.zbox(x - .0015, x + .0015, y - .0004, y + .0004, .0045, .0048), stitch)
    if buckle:
        p.add('slide buckle', fb.zbox(.0180, .0260, -.0095, .0095, .0005, .0056), chrome, bevel=.0006)
        p.add('buckle bar', fb.zbox(.0215, .0225, -.0095, .0095, .0056, .0062), chrome)
    return p


def worn_loop():
    p = Faceted('WornLoop')
    leather, stitch = M('HD_CamStrap'), M('HD_CamStitch')
    p.add('band', chamfered_slab(.018, .140, .0040, .0030, .0008, per_corner=2, centre_z=.0025), leather)
    for x in (.0060, -.0060):
        for i in range(16):
            y = -.0600 + i * .0080
            p.add('stitch', fb.zbox(x - .0004, x + .0004, y - .0015, y + .0015, .0045, .0048), stitch)
    for sy in (1, -1):
        for x in (-.0060, 0.0, .0060):
            p.add('fray', fb.zbox(x - .0018, x + .0018, sy * .0700 - .0010, sy * .0760, .0005, .0040), leather, bevel=.0006)
    return p


PIECES = [
    (body, ['Camera body']),
    (lens_housing, ['Lens housing']),
    (lens_glass, ['Lens glass']),
    (film_path, ['Open film path']),
    (mechanism, ['Jammed shutter mechanism']),
    (lambda: blade(True), ['Bent shutter blade']),
    (lambda: blade(False), ['Working shutter blade']),
    (strap, ['Upper strap', 'Lower strap']),
    (worn_loop, ['Worn loop']),
]


def build():
    made = []
    root = None
    children = []
    for builder, targets in PIECES:
        ob = builder().finish()
        made.append((ob, targets))
        if root is None:
            root = ob
        else:
            ob.parent = root
            ob.location = to_blender(PREFAB_OFFSETS[ob.name])
            children.append(ob)
    return root, children, made
