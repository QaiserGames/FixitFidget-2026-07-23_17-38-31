"""
Grace's camera, v3.6 (14 Oct 2026, playtest 5's "forgiving bench" session; v3 of 9 Oct below it). GraceReunionCamera.prefab's
pieces as real meshes, built from a 1970s rangefinder (claude/hero-reference-sheet.md: the Canonet QL17 GIII and the
Olympus 35 SP, measured) in the house style (hero_lib.py).

AUTHORED AT 2x, EXPORTED REAL (TD one-scale §6.1): every number in this file is at the prefab's old 2x size, and build()
scales the meshes and the children's places by 1 / AUTHORED_SCALE (hero_lib.scale_build), so the FBX is real size: a
120 x 80 x 32 mm body. The strap (v3.6) is designed in real millimetres (STRAP_* below) and doubled into this frame.

v3.6 (Art A6-A7, specs/art-bench-and-cafe.md; his note "why the handle for the camera is just a rectangle, it doesn't flow
like how a camera would"):
  * THE STRAP is a real neck strap: two chrome lugs on the END faces at the top plate's height, a split ring and a
    teardrop leather protector on each, the tan leather strap (14 x 2.5 mm, burnished dark edges, cream stitching 1.5 mm
    in from each edge) through each ring, folded back 40 mm under a keeper 25 mm from the ring, twisting from edge-on at
    the ring to flat on the mat, then curving away from the player into a loosely gathered loop of two turns behind the
    lens half (the opening end's run passes behind the body). Upper strap = the lens end's run, Lower strap = the
    opening end's run, Worn loop = the loop; the old scratches lie on the loop's upper turn, where it runs flat along x.
  * THE BODY is 32 mm deep (was 23: "a third too flat"), its back where it was (it lies on the mat), so the front face and
    everything on it or in the well (lens, glass, film path, mechanism, the three grime spots) rise 9 mm (real).
  * ITS ROUNDINGS ARE SWAPPED: the ends' vertical edges (where the end faces meet the front and back) are rounded r 6.5
    (real), the rounded ends a rangefinder shows from above; the top and bottom faces' outlines are rounded r 2.5.
  * THE SHUTTER, in two states of one mechanism: `Clean shutter mechanism` (the fresh part: bright new brass, bright
    steel and chrome, the spring seated, two flat blades on top) and `Jammed shutter mechanism` (the same shape with its
    tells: tarnished brass, dull steel, the spring's end sprung 4 mm out of its seat, one flat dull blade under the bent
    one, which stays its own piece).

The prefab's frame (Unity): the camera lies on its back, its front face up (+Y); the lens at -X; at +X an opening with
the film transport (toward -Z) and the shutter mechanism (toward +Z); the close-up camera looks from -Z. Blender here
(hero_lib.to_blender: Unity (x, y, z) = Blender (-x, z, -y)): Z through (+Z the front), the lens at +X, the opening at
-X, the film path at +Y, the mechanism at -Y, and the player looks from +Y.

The fiction, made plausible: the camera's TOP PLATE runs along the +Y edge, the edge nearest the player (rewind crank
at the lens end, hot shoe over the lens, shutter release and advance lever at the other end, the strap lugs at its two
ends; the three finder windows in its front face), the bottom plate along -Y, leatherette between; at -X the front
panel is OFF for service, a well under the top plate's chrome lintel, and in it the film transport (the gate with its
steel rails, the sprocket drum, the take-up spool) nearer the player and the leaf-shutter assembly (the shutter drum
with its gear train, cocking lever and spring, the blades on top) behind it.

  Camera body        240 x 160 x 64 (2x), centred on its origin (Unity y 0.032 in the prefab at 2x: 0.016 real).
  Lens housing       unchanged (Ø 76 at 2x: the TD's 0.73x-real flag stands); its origin at Unity y 0.073 (2x).
  Lens glass         unchanged; Unity y 0.111 (2x).
  Open film path     unchanged; Unity y 0.067 (2x).
  Jammed shutter mechanism / Clean shutter mechanism   the same chassis, drum, gears, lever, spring; Unity y 0.078 (2x).
  Bent / Working shutter blade   thin steel leaf-shutter blades; the bent one kinks up.
  Upper strap / Lower strap / Worn loop   the strap's three pieces (v3.6), with the rings, protectors and keepers.
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
from mathutils import Matrix, Vector  # noqa: E402

AUTHORED_SCALE = 2.0      # this file's numbers are at 2x; the FBX is real (build() scales by 1 / AUTHORED_SCALE)

HD_MATERIALS = {
    'HD_CamLeather':  ('#2A2623', 0.25, 0.0),   # the body's leatherette
    'HD_CamLeatherWorn': ('#4F4842', 0.38, 0.0), # the leatherette where forty years of fingers have worn the grain smooth
    'HD_CamFilm':     ('#3A302A', 0.35, 0.0),   # the film in the camera
    'HD_CamChrome':   ('#B9B5AD', 0.50, 0.0),   # satin chrome: plates, rings, rails, the lugs and split rings
    'HD_CamBlack':    ('#1F1D1B', 0.40, 0.0),   # black paint and plastic: the lens rings, the gate, the chassis, the well
    'HD_CamGlass':    ('#1A2430', 0.70, 0.0),   # the lens, the finder windows
    'HD_CamMilk':     ('#D9D7D0', 0.50, 0.0),   # the frosted frame-line illuminator
    'HD_CamSteel':    ('#8E8B85', 0.45, 0.0),   # the WORKING blade (today's fresh visual), kept
    'HD_CamBrass':    ('#B08A3E', 0.50, 0.0),   # (v3 brass; no longer used by the mechanisms, kept for the prefab's slots)
    'HD_CamStrap':    ('#7A4A28', 0.30, 0.0),   # tan leather
    'HD_CamStrapEdge': ('#4A2C18', 0.35, 0.0),  # the strap's burnished edges, the keepers' and protectors' leather (v3.6, Art A6)
    'HD_CamStitch':   ('#D9C7A6', 0.30, 0.0),   # the strap's stitching
    'HD_CamWhite':    ('#F2EFE8', 0.30, 0.0),   # index marks on the rings, the lettering round the name ring
    # the shutter's two states (v3.6, Art A7): about 25 L* apart in the metals, the same shape
    'HD_CamBrassNew':    ('#D4AF5A', 0.55, 0.0),  # the clean mechanism's gears: bright new brass (L* ~73)
    'HD_CamBrassOld':    ('#8A6A32', 0.35, 0.0),  # the jammed one's gears: tarnished (L* ~47)
    'HD_CamSteelBright': ('#C9C5BC', 0.55, 0.0),  # the clean one's steel: arbors, lever, spring, screws, blades
    'HD_CamSteelOld':    ('#77746E', 0.30, 0.0),  # the jammed one's steel, dull; the bent blade
    'HD_CamChromeBright': ('#CFCBC2', 0.60, 0.0), # the clean one's drum rim
    'HD_CamBlackSatin':  ('#1F1D1B', 0.50, 0.0),  # the clean one's chassis: satin black (Art: 0.5)
}

BODY_Y = .032             # the body's centre in the prefab (Unity y, 2x): its back stays at -0.001, the 64 mm body rises
PREFAB_OFFSETS = {        # Unity's frame, 2x, relative to the body (the FBX root); v3.6: the prefab's places minus (0, BODY_Y, 0)
    'LensHousing':  (-0.065, 0.073 - BODY_Y, -0.024),
    'LensGlass':    (-0.065, 0.111 - BODY_Y, -0.024),
    'FilmPath':     (0.056, 0.067 - BODY_Y, -0.043),
    'Mechanism':    (0.053, 0.078 - BODY_Y, 0.041),
    'BentBlade':    (0.053, 0.078 + 0.01232 - BODY_Y, 0.041),
    'WorkingBlade': (0.053, 0.078 - 0.010 - BODY_Y, 0.041),     # in the FBX only: inside the housing, out of the bent one's way
    'CleanMechanism': (0.053 + 0.300, 0.078 - BODY_Y, 0.041),   # in the FBX only: beside the camera (the prefab puts it in its seat, off)
    'UpperStrap':   (-0.1275, 0.032 - BODY_Y, -0.065),          # its origin: the lens-end lug's hole (real (-0.06375, 0.016, -0.0325))
    'LowerStrap':   (0.1275, 0.032 - BODY_Y, -0.065),           # the opening-end lug's hole
    'WornLoop':     (-0.100, -0.001 - BODY_Y, 0.190),           # the loop's middle, on the mat (real (-0.050, -0.0005, 0.095))
}

HX, HY, HZ = .120, .080, .032      # the body's half sizes (2x): 240 x 160 x 64; v3.6: HZ .023 -> .032 (32 mm real)
R_END = .013                       # v3.6: the ends' vertical edges, r 6.5 real (Art A6: "r 6-7")
R_TOP = .005                       # v3.6: the top and bottom faces' outlines, r 2.5 real ("the front-view corners r ~2.5")
MAKER = 'HALCYON'                  # the camera's maker (his pick, 9 Oct). One word to change.
LENS_NAME = MAKER + ' 40mm 1:1.7'  # round the lens's name ring

def lettering(text, size, depth):
    """text_mesh's glyphs centred on their box, in XY, extruded +Z."""
    txt = hl.text_mesh(text, size, depth)
    if txt is None:
        return bmesh.new()
    xs = [v.co.x for v in txt.verts]
    ys = [v.co.y for v in txt.verts]
    return moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, 0)


def ring_lettering(text, r, z, size, depth, phi_centre_deg=90.0, advance=None, tops_inward=True):
    """Letters one by one round a ring's front face (facing +Z). The player's up is -Y, so lettering on the NEAR side of
    the ring (+Y, phi 90) reads the right way up with its tops toward the centre, as the name on the bottom of a lens
    ring does; it runs from the player's left (+X) round to the right (-X), i.e. with increasing phi."""
    bm = bmesh.new()
    advance = advance or size * .78
    n = len(text)
    dphi = advance / r
    phi0 = math.radians(phi_centre_deg) - dphi * (n - 1) / 2
    for i, ch in enumerate(text):
        if ch == ' ':
            continue
        g = lettering(ch, size, depth)
        phi = phi0 + i * dphi
        fb.turn(g, math.degrees(phi) - 90.0 + (180.0 if tops_inward else 0.0), 'Z')   # its up (+Y) to the radial direction, or inward
        g = moved(g, r * math.cos(phi), r * math.sin(phi), z)
        for v in g.verts:
            bm.verts.new(v.co)
        bm.verts.ensure_lookup_table()
        base = len(bm.verts) - len(g.verts)
        g.verts.ensure_lookup_table()
        idx = {v.index: k for k, v in enumerate(g.verts)}
        for f in g.faces:
            try:
                bm.faces.new([bm.verts[base + idx[v.index]] for v in f.verts])
            except ValueError:
                pass
        g.free()
    return bm
WELL_FLOOR = HZ - .010             # the opening's floor in the body's frame: 10 mm (5 real) under the front face, as in v3
WELL = (-.104, -.010, -.0655, .0635)   # x0, x1, y0, y1 of the opening (Blender: Unity x 10..104 is Blender -104..-10)
BAND = (.050, .080)                # the top plate's front band (y): the NEAR edge for the player; over the opening only its lintel (y 0.0635 .. 0.080)
LENS = (.065, .024)                # the lens axis (x, y here): Unity (-0.065, z -0.024)
LUG_OUT = .0065                    # v3.6: the lug's hole centre, this far out from the end plate (2x; 3.25 real)
LUG_Y = (BAND[0] + BAND[1]) / 2    # at the top plate's band height (Art A6), centred in the body's depth
INNER = .006                       # the leatherette stops this far inside the plates' ends (their rounded edges must cover it)


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


def xz_loop(x0, x1, zh, y, r_left, r_right, per_corner=4):
    """A loop round an X-Z section (x0..x1 across, -zh..zh through the body) at height y; its -X corners rounded by r_left
    and its +X corners by r_right (0: square). v3.6: these are the ends' VERTICAL edges, the rangefinder's rounded ends."""
    pts = block_loop(x0, x1, -zh, zh, 0.0, r_left, r_right, per_corner=per_corner)
    return [(px, y, pz) for (px, pz, _z) in pts]


def plated(x0, x1, zh, y0, y1, r_left=0.0, r_right=0.0, round_left=False, round_right=False, top_r=0.0, bottom_r=0.0, n=2, per_corner=4):
    """A block along Y (the camera's height) from y0 to y1 whose X-Z section has its -X corners rounded r_left and its +X
    corners r_right; top_r / bottom_r round its top / bottom face's outline by a quarter circle in n steps: the Z sides
    always, an X end only where round_left / round_right says (a straight cut where it meets the next block)."""
    levels = []
    if bottom_r > 0:
        for i in range(n + 1):
            a = (math.pi / 2) * i / n
            levels.append((y0 + bottom_r * (1 - math.cos(a)), bottom_r * (1 - math.sin(a))))
    else:
        levels.append((y0, 0.0))
    if top_r > 0:
        for i in range(n, -1, -1):
            a = (math.pi / 2) * i / n
            levels.append((y1 - top_r * (1 - math.cos(a)), top_r * (1 - math.sin(a))))
    else:
        levels.append((y1, 0.0))
    loops = []
    for (y, d) in levels:
        xl = x0 + (d if round_left else 0.0)
        xr = x1 - (d if round_right else 0.0)
        rl = max(r_left - d, .0004) if r_left > 0 else 0.0
        rr = max(r_right - d, .0004) if r_right > 0 else 0.0
        loops.append(xz_loop(xl, xr, zh - d, y, rl, rr, per_corner))
    return tube(loops)


def worn_patch():
    """The worn leatherette (v3.4) where the right hand grips the opening end: a ragged patch 14 x 28 mm (2x), v3.6 laid
    over the rounded end's vertical edge (from the front face round toward the end face), 0.6 mm proud of the arc so its
    flat facets clear the body's (4 facets a quarter)."""
    import random
    rng = random.Random(3)
    xc, zc, R = -HX + R_END, HZ - R_END, R_END + .0006
    th0, yc = math.radians(42.0), -.0200

    def on_arc(th, y, lift=0.0):
        return (xc - (R + lift) * math.sin(th), y, zc + (R + lift) * math.cos(th))

    ring = []
    for i in range(12):
        a = 2 * math.pi * i / 12
        ring.append((th0 + math.radians(30.0) * rng.uniform(.85, 1.15) * math.cos(a), yc + .0140 * rng.uniform(.85, 1.15) * math.sin(a)))
    bm = bmesh.new()
    top_c = bm.verts.new(on_arc(th0, yc, .0001))
    bot_c = bm.verts.new(on_arc(th0, yc, 0.0))
    top = [bm.verts.new(on_arc(th, y, .0001)) for th, y in ring]
    bot = [bm.verts.new(on_arc(th, y, 0.0)) for th, y in ring]
    for k in range(12):
        m = (k + 1) % 12
        bm.faces.new([top_c, top[k], top[m]])
        bm.faces.new([bot_c, bot[m], bot[k]])
        bm.faces.new([bot[k], bot[m], top[m], top[k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def lug(sign):
    """A chrome strap lug (v3.6, Art A6) on an end plate: sign +1 the lens end (+X), -1 the opening end. A base pad 16 x 12 x
    1.5 (2x) on the plate, and an eyelet (outside Ø 13, hole Ø 7: 3.5 real) whose hole runs through the body's depth, so
    the split ring hangs from it."""
    xf = sign * (HX + .0010)
    bm = fb.zbox(min(xf - sign * .0006, xf + sign * .0015), max(xf - sign * .0006, xf + sign * .0015), LUG_Y - .0080, LUG_Y + .0080, -.0060, .0060)
    fb.bevel_all(bm, .0005, segments=1)
    eye = ring_wall(-.0030, .0030, .0035, .0065, sides=16)
    fb.merge(bm, moved(eye, xf + sign * LUG_OUT, LUG_Y, 0.0))
    return bm


def body():
    p = Faceted('HD_Camera')
    leather, chrome, black, glass, milk, steel, white = (M('HD_CamLeather'), M('HD_CamChrome'), M('HD_CamBlack'), M('HD_CamGlass'),
                                                        M('HD_CamMilk'), M('HD_CamSteel'), M('HD_CamWhite'))
    wx0, wx1, wy0, wy1 = WELL
    ly0, ly1 = -HY + INNER, HY - INNER
    # ---- the leatherette body, in blocks round the opening (the opening is at -X; the lens end is +X); v3.6: 64 mm deep (2x),
    # the ends' vertical edges rounded R_END; it stops INNER short of the plates' ends, which cover it there
    p.add('body end', plated(-HX, wx0, HZ, ly0, ly1, r_left=R_END), leather)
    p.add('body lens side', plated(wx1, HX, HZ, ly0, ly1, r_right=R_END), leather)
    p.add('body under the well', block(wx0, wx1, ly0, ly1, -HZ, WELL_FLOOR, chamfer=.0006), leather)
    p.add('body far strip', block(wx0, wx1, ly0, wy0, -HZ, HZ, chamfer=.0006), leather)
    p.add('body lintel', block(wx0, wx1, wy1, ly1, -HZ, HZ, chamfer=.0006), leather)
    p.add('worn patch', worn_patch(), M('HD_CamLeatherWorn'))
    # the well's lining: black floor and walls (the inside of a camera)
    p.add('well floor', fb.zbox(wx0, wx1, wy0, wy1, WELL_FLOOR, WELL_FLOOR + .0008), black)
    for (x0, x1, y0, y1) in ((wx0, wx0 + .0008, wy0, wy1), (wx1 - .0008, wx1, wy0, wy1), (wx0, wx1, wy0, wy0 + .0008), (wx0, wx1, wy1 - .0008, wy1)):
        p.add('well wall', fb.zbox(x0, x1, y0, y1, WELL_FLOOR, HZ - .0005), black)
    # ---- the chrome plates, 1 mm proud all round (2x): the top plate's front band (30 mm; a 16 mm lintel over the opening)
    # and the bottom plate; v3.6: their ends follow the body's rounded ends, and their top / bottom faces' outlines are
    # rounded R_TOP (the "front-view corners" Art asks for, r 2.5 real)
    b0, b1 = BAND
    pz = HZ + .0010
    p.add('top band, opening end', plated(-HX - .0010, wx0, pz, b0, HY + .0010, r_left=R_END + .0010, round_left=True, top_r=R_TOP + .0010), chrome)
    p.add('top band, lens side', plated(wx1, HX + .0010, pz, b0, HY + .0010, r_right=R_END + .0010, round_right=True, top_r=R_TOP + .0010), chrome)
    p.add('top band lintel', plated(wx0, wx1, pz, wy1, HY + .0010, top_r=R_TOP + .0010), chrome)
    p.add('bottom plate', plated(-HX - .0010, HX + .0010, pz, -HY - .0010, -HY + .0135, r_left=R_END + .0010, r_right=R_END + .0010,
                                 round_left=True, round_right=True, bottom_r=R_TOP + .0010), chrome)
    # the maker's name on the band's face (v3.5: where the player reads it from the close-up), 4 mm, upright for the player
    name = lettering(MAKER, .0040, .00020)
    fb.turn(name, 180.0, 'Z')                 # up to -Y
    p.add('maker name', moved(name, .0050, .0560, pz), black)
    # ---- the front face: the three finder windows in the band's face, the lens boss
    zf = pz
    for (cx, w, h, mat) in ((LENS[0], .0110, .0110, glass), (.0400, .0170, .0110, milk), (.0120, .0260, .0130, glass)):
        cy = (b0 + b1) / 2
        p.add('window frame', fb.zbox(cx - w / 2 - .0012, cx + w / 2 + .0012, cy - h / 2 - .0012, cy + h / 2 + .0012, zf, zf + .0006), black)
        p.add('window', fb.zbox(cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2, zf, zf + .0010), mat)
    lx, ly = LENS
    p.add('lens boss', moved(ring_wall(HZ, HZ + .0025, .0400, .0450, sides=48), lx, ly, 0), chrome)
    # ---- the top face (y = +HY, toward the player): rewind crank at the lens end, hot shoe over the lens, shutter release and
    # advance lever at the opening end
    yt = HY + .0010

    def on_top(bm):
        """A part built standing on z = 0 (up +Z) turned to stand on the top face (up +Y)."""
        fb.turn(bm, -90.0, 'X')
        return moved(bm, 0, yt, 0)

    rx = .0960
    knob = revolve([(0.0, .0140), (.0050, .0140), (.0060, .0120)], sides=24, knurl=(1, .03))
    p.add('rewind knob', on_top(moved(knob, rx, 0, 0)), chrome)
    p.add('rewind crank', on_top(fb.zbox(rx - .0110, rx + .0110, -.0015, .0015, .0060, .0075)), black)
    p.add('crank knob', on_top(disc(rx - .0090, 0.0, .0075, .0105, .0020, sides=8)), black)
    shoe = fb.zbox(-.0200, .0200, -.0150, .0150, 0.0, .0020)
    p.add('hot shoe', on_top(moved(shoe, lx, 0, 0)), black)
    for sz in (1, -1):
        rail = fb.zbox(-.0200, .0200, sz * .0120 - .0025, sz * .0120 + .0025, .0020, .0040)
        p.add('shoe rail', on_top(moved(rail, lx, 0, 0)), chrome)
    p.add('shoe contact', on_top(disc(lx, 0.0, .0020, .0026, .0020, sides=8)), black)
    p.add('release boss', on_top(disc(-.0840, .0040, 0.0, .0030, .0110, sides=16)), chrome)
    p.add('shutter release', on_top(disc(-.0840, .0040, .0030, .0070, .0085, sides=16)), chrome)
    p.add('cable thread', on_top(disc(-.0840, .0040, .0070, .0074, .0022, sides=8)), black)
    arm = fb.zbox(-.0340, 0.0, -.0030, .0030, .0032, .0052)
    fb.turn(arm, -20.0, 'Z')
    p.add('advance lever', on_top(moved(arm, -.0840, -.0120, 0)), chrome)
    tip = fb.zbox(-.0400, -.0280, -.0045, .0045, .0028, .0062)
    fb.bevel_all(tip, .0010, segments=1)
    fb.turn(tip, -20.0, 'Z')
    p.add('lever tip', on_top(moved(tip, -.0840, -.0120, 0)), black)
    # the strap lugs (v3.6): one on each END face at the band's height, centred in the depth (v3 had three eyelets, two
    # of them feeding the "handle" at the lens end)
    for sign in (1, -1):
        p.add('strap lug', lug(sign), chrome)
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
    p.add('name ring', revolve([(.0260, .0340), (.0340, .0340), (.0350, .0320), (.0360, .0300), (.0360, .0252), (.0330, .0248)], sides=S), black)
    # index marks: white ticks on the two chrome rings and an index line on the name ring
    for z0, z1, n in ((.0085, .0135, 7), (.0195, .0245, 6)):
        for k in range(n):
            tick = fb.zbox(-.0004, .0004, .0372, .0382, z0, z1)
            fb.turn(tick, -60.0 + 120.0 * k / (n - 1), 'Z')
            p.add('index', tick, white)
    p.add('index line', fb.zbox(-.0005, .0005, .0335, .0345, .0280, .0335), white)
    # the lens's name round the name ring's front face (v3.5), on the far side from the player, tops outward
    p.add('lens name', ring_lettering(LENS_NAME, .0277, .0360, .0023, .00015), white)
    # the meter cell window at twelve o'clock inside the name ring
    p.add('meter window', fb.zbox(-.0050, .0050, .0230, .0300, .0340, .0356), glass)
    return p


def lens_glass():
    """Centred on its origin (Unity y 0.093): a chrome retaining ring and the dark front element, apex at +3.2 mm."""
    p = Faceted('LensGlass')
    glass, chrome = M('HD_CamGlass'), M('HD_CamChrome')
    # 50 mm over the retaining ring (v3.5; was 57, which covered the name ring's face): a 40 mm f/1.7's front element is
    # about 24 mm across, 48 at this scale
    p.add('retaining ring', revolve([(-.0030, .0222), (-.0030, .0250), (.0008, .0250), (.0014, .0230), (-.0010, .0222)], sides=48, cap_top=False), chrome)
    prof = [(-.0028, .0150), (-.0028, .0223), (.0004, .0223)] + dome(.0004, .0218, .0028, n=4)
    p.add('front element', revolve(prof, sides=48, tip=(0, 0, .0032)), glass)
    return p


def film_path():
    """Centred on its origin (Unity y 0.049): the chamber floor from the well's floor up; everything under +5.5 mm. The
    sprocket drum sits under the film-guide grime (Unity x 69..89: here local x -33..-13), the take-up spool at the other end."""
    p = Faceted('FilmPath')
    chrome, black, steel = M('HD_CamChrome'), M('HD_CamBlack'), M('HD_CamSteel')
    floor = WELL_FLOOR - (.067 - BODY_Y)       # the well's floor in this piece's frame: -0.013
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
    # the film (v3.4): a strip 32 wide riding on the rails over the gate, climbing over the sprocket drum at one end
    # (its teeth through the perforations) and up to the take-up spool at the other. Perforated along both edges at the
    # real 4.75 mm pitch: the holes are real holes, the rails and the gate show through them. It is a film camera, and
    # the reunion photos are on this strip.
    p.add('film', film_strip(), M('HD_CamFilm'))
    return p


def film_strip():
    path = [(-.0330, .0046), (-.0300, .0050), (-.0240, .0027), (.0240, .0027), (.0300, .0044), (.0330, .0046)]  # (x, z) along the path
    pitch, hole, half_w = .00475, .0028, .0160
    rows = [(-half_w, -.0137), (-.0137, -.0113), (-.0113, .0113), (.0113, .0137), (.0137, half_w)]   # edge, perf, frame, perf, edge
    bm = bmesh.new()

    def z_at(x):
        for (x0, z0), (x1, z1) in zip(path, path[1:]):
            if x0 <= x <= x1:
                t = (x - x0) / (x1 - x0)
                return z0 + (z1 - z0) * t
        return path[0][1] if x < path[0][0] else path[-1][1]

    x0, x1 = path[0][0], path[-1][0]
    # cells along the strip: a hole's length, then the bar between holes, repeating
    xs = [x0]
    x = x0
    while x < x1:
        x = min(x + hole, x1); xs.append(x)
        x = min(x + (pitch - hole), x1); xs.append(x)
    for i in range(len(xs) - 1):
        xa, xb = xs[i], xs[i + 1]
        if xb - xa < 1e-6:
            continue
        is_hole = (i % 2 == 0)
        for (ya, yb), band in zip(rows, ('edge', 'perf', 'frame', 'perf', 'edge')):
            if band == 'perf' and is_hole:
                continue
            za, zb = z_at(xa), z_at(xb)
            top = [bm.verts.new(v) for v in ((xa, ya, za), (xb, ya, zb), (xb, yb, zb), (xa, yb, za))]
            bot = [bm.verts.new(v) for v in ((xa, ya, za - .00012), (xb, ya, zb - .00012), (xb, yb, zb - .00012), (xa, yb, za - .00012))]
            bm.faces.new(top)
            bm.faces.new(list(reversed(bot)))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-7)
    return bm


def mechanism(state='jammed'):
    """Centred on its origin (Unity y 0.078 at 2x): the chassis from the well's floor, the shutter drum to +10.5; blades above.
    v3.6, Art A7: ONE mechanism in two states. 'clean' (the fresh part, `Clean shutter mechanism`): satin black chassis,
    bright new brass, bright steel and chrome, the spring seated, and two flat blades on top. 'jammed' (the broken part,
    `Jammed shutter mechanism`): the same shape with its tells: tarnished brass, dull steel, the spring's free end sprung
    8 mm (4 real) out of its seat, one flat dull blade on the drum (the bent one lies over it: its own piece, kinked up)."""
    clean = state == 'clean'
    p = Faceted('CleanMechanism' if clean else 'Mechanism')
    black = M('HD_CamBlackSatin' if clean else 'HD_CamBlack')
    brass = M('HD_CamBrassNew' if clean else 'HD_CamBrassOld')
    steel = M('HD_CamSteelBright' if clean else 'HD_CamSteelOld')
    rim = M('HD_CamChromeBright' if clean else 'HD_CamChrome')
    floor = WELL_FLOOR - (.078 - BODY_Y)       # the well's floor in this piece's frame: -0.024
    p.add('chassis', chamfered_slab(.073, .049, -.0060 - floor, .0030, .0006, per_corner=1, centre_z=(floor - .0060) / 2), black)
    p.add('shutter drum', revolve([(-.0060, .0210), (.0090, .0210)], sides=32), black)
    p.add('drum rim', ring_wall(.0090, .0105, .0180, .0210, sides=32), rim)
    p.add('aperture', disc(0.0, 0.0, .0090, .0094, .0100, sides=24), M('HD_CamGlass'))
    p.add('drum step', ring_wall(.0090, .0098, .0100, .0130, sides=24), steel)
    for (gx, gy, gr, teeth, mat) in ((.0300, .0140, .0090, 14, brass), (.0300, -.0060, .0065, 10, brass), (-.0300, .0100, .0105, 18, brass), (-.0300, -.0120, .0050, 8, steel)):
        p.add('gear', gear(gx, gy, -.0060, -.0030, gr, teeth, depth=.16), mat)
        p.add('arbor', disc(gx, gy, -.0060, -.0010, .0012, sides=8), steel)
    lever = fb.zbox(0.0, .0300, -.0100, -.0060, -.0030, -.0016)
    fb.turn(lever, -15.0, 'Z', pivot=(.0300, -.0060, 0))
    p.add('cocking lever', lever, steel)
    # the spring: a coil lying in its groove along the chassis; jammed, its free end (the last 35 %) has sprung up out of it
    helix = []
    for i in range(61):
        k = i / 60
        x = .0340 - .0200 * k
        y = -.0190 + .0022 * math.cos(2 * math.pi * 7 * k)
        z = -.0040 + .0022 * math.sin(2 * math.pi * 7 * k)
        if not clean and k > .65:
            u = (k - .65) / .35
            z += .0080 * u * u                  # 8 mm proud at the tip (4 real)
            y -= .0025 * u * u                  # and a little out of line
        helix.append((x, y, z))
    p.add('spring', sweep(helix, .0005, sides=4), steel)
    for sy in (-.0222, -.0158):              # the spring's seat: two low rails either side of the coil (the jammed one has left them)
        p.add('spring seat', fb.zbox(.0120, .0360, sy - .0003, sy + .0003, -.0062, -.0046), black)
    for (sx, sy) in ((.0330, .0220), (-.0330, -.0210), (-.0330, .0220)):
        p.add('chassis screw', disc(sx, sy, -.0060, -.0052, .0018, sides=8), steel)
        slot = fb.zbox(sx - .0013, sx + .0013, sy - .0003, sy + .0003, -.0053, -.00505)
        p.add('chassis screw slot', slot, black)
    # the blades on the drum: the bent blade's seat is +12.3 mm (its object); a second blade lies flat just under it
    outline = blade_outline()
    under = prism(outline, -.0005, .0005)
    fb.turn(under, 72.0, 'Z')
    p.add('blade, under', moved(under, 0.0, 0.0, .01232 - .0011), steel)
    if clean:
        top = prism(outline, -.0005, .0005)
        p.add('blade, top', moved(top, 0.0, 0.0, .01232), steel)
        p.add('blade pivot', disc(.0225, -.0145, .01232 - .0007, .01232 + .0009, .0022, sides=10), rim)
    return p


def clean_mechanism():
    return mechanism('clean')


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
    steel = M('HD_CamSteelOld' if bent else 'HD_CamSteel')        # v3.6: the bent blade is the jammed assembly's: dull
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


# ----------------------------------------------------------------------------- the strap (v3.6, Art A6)
#
# Designed in REAL millimetres in the prefab's frame (Unity axes, the root at the camera's back; the mat at y -0.5, the
# body's lowest point), then doubled into this file's 2x frame. The lens end is -x, the player at -z.

STRAP_HALF_W, STRAP_HALF_T = 7.0, 1.25      # a 14 x 2.5 mm strap
MAT_Y = -0.5                                 # the mat under the camera (the chrome plates' back face)
FLAT_Y = MAT_Y + STRAP_HALF_T                # a flat strap's centre on the mat
LUG_HOLE = (1000 * (HX + .0010 + LUG_OUT) / AUTHORED_SCALE, 1000 * BODY_Y / AUTHORED_SCALE, -1000 * LUG_Y / AUTHORED_SCALE)  # (+x end) 63.75, 16, -32.5
RING_R, RING_WIRE = 5.4, 0.6                 # the split ring: Ø 12 outside, 1.2 mm wire (Art A6)
RING_TILT = 25.0                             # it hangs outward and this far down, in the end's vertical plane
FOLD, KEEPER_AT = 40.0, 25.0                 # the strap's end folds back 40 mm; the keeper 25 mm from the ring
TWIST_TO = 32.0                              # from edge-on at the ring to flat on the mat over this length
SECTION = [(-6.3, 1.25), (6.3, 1.25), (7.0, 0.0), (6.3, -1.25), (-6.3, -1.25), (-7.0, 0.0)]   # (across, through), mm
STRAP_FACES = (0, 3)                         # section edges 0-1 (the face) and 3-4 (the back) are leather; the rest burnished edge

# the run from each ring to the mat (x mirrored per end; the first point is computed from the ring)
RUN_START = [(80.5, 9.6, -29.5), (86.5, 6.0, -22.0), (89.5, 2.8, -12.0), (90.5, FLAT_Y, -2.0), (91.0, FLAT_Y, 18.0)]
UPPER_RUN = [(-x, y, z) for (x, y, z) in RUN_START] + [(-90.5, FLAT_Y, 40.0), (-89.0, FLAT_Y, 56.0), (-88.0, FLAT_Y, 66.0)]
LOWER_RUN = RUN_START + [(89.5, FLAT_Y, 36.0), (83.0, FLAT_Y, 47.0), (70.0, FLAT_Y, 53.5), (50.0, FLAT_Y, 55.5), (25.0, FLAT_Y, 55.5),
                         (5.0, FLAT_Y, 56.5), (-8.0, FLAT_Y, 60.0), (-14.0, FLAT_Y, 65.0), (-15.5, FLAT_Y, 70.0)]
LOOP_A = ((-53.0, 95.0), 36.0, 22.0)         # the loop's first turn: centre (x, z), half axes (centreline)
LOOP_B = ((-48.0, 99.0), 32.0, 18.0)         # its second turn, gathered inward; its far strand lies right on the first's
LOOP_EXIT = [(-16.0, 84.0), (-15.5, 76.0)]   # down from the second turn to the lower run
SCRATCHES = [(-56.0, -2.0), (-48.0, 1.5), (-40.0, -0.5)]   # on the upper turn's far strand: x, and z off its centreline (mm)


def _catmull(points, step=1.5):
    """A Catmull-Rom curve through the points, sampled about every `step` mm."""
    P = [Vector(p) for p in points]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(P[-2].copy())
    return out


def _ellipse(c, a, b, th0, th1, step=1.5, c2=None, a2=None, b2=None, blend=None):
    """Points on an ellipse (x = cx + a cos th, z = cz + b sin th) from th0 to th1 (degrees), blending its centre and half
    axes toward a second ellipse's over the first `blend` degrees."""
    pts = []
    span = th1 - th0
    n = max(4, int(abs(math.radians(span)) * max(a, b) / step))
    for k in range(n + 1):
        th = th0 + span * k / n
        f = 1.0
        if blend:
            f = hl.smoothstep(abs(th - th0) / blend)
        cx = c[0] + ((c2[0] - c[0]) * f if c2 else 0.0)
        cz = c[1] + ((c2[1] - c[1]) * f if c2 else 0.0)
        aa = a + ((a2 - a) * f if a2 else 0.0)
        bb = b + ((b2 - b) * f if b2 else 0.0)
        r = math.radians(th)
        pts.append(Vector((cx + aa * math.cos(r), FLAT_Y, cz + bb * math.sin(r))))
    return pts


def _flat_frame(t):
    up = Vector((0.0, 1.0, 0.0))
    n = up - t * up.dot(t)
    if n.length < 1e-6:
        n = Vector((0.0, 0.0, 1.0))
    n.normalize()
    w = n.cross(t).normalized()
    return w, n


def strap_design():
    """The strap's centreline from the lens-end ring to the opening-end ring (real mm, Unity), with its across direction W at
    every point, plus where it is cut into the three prefab pieces. Returns a dict."""
    # the rings first: each hangs from its lug's hole, outward and down; the strap wraps round its far side
    rings = {}
    for side in (-1, 1):
        hole = Vector((side * LUG_HOLE[0], LUG_HOLE[1], LUG_HOLE[2]))
        u = Vector((side * math.cos(math.radians(RING_TILT)), -math.sin(math.radians(RING_TILT)), 0.0))
        wrap = hole + u * (2 * RING_R)
        wire = Vector((side * math.sin(math.radians(RING_TILT)), math.cos(math.radians(RING_TILT)), 0.0))   # the wire's direction there
        rings[side] = (hole, wrap, wire, u)
    # the centreline: the upper run, the loop's two turns, the exit, the lower run (reversed: loop to ring)
    lens_wrap, opening_wrap = rings[-1][1], rings[1][1]
    upper = _catmull([tuple(lens_wrap)] + UPPER_RUN)
    loop = (_ellipse(LOOP_A[0], LOOP_A[1], LOOP_A[2], 196.0, -164.0)[1:] +
            _ellipse(LOOP_A[0], LOOP_A[1], LOOP_A[2], 196.0, -10.0, c2=LOOP_B[0], a2=LOOP_B[1], b2=LOOP_B[2], blend=60.0)[1:])
    exit_ = _catmull([tuple(loop[-1]), (LOOP_EXIT[0][0], FLAT_Y, LOOP_EXIT[0][1]), (LOOP_EXIT[1][0], FLAT_Y, LOOP_EXIT[1][1]),
                      LOWER_RUN[-1]])[1:]
    lower = list(reversed(_catmull([tuple(opening_wrap)] + LOWER_RUN)))[1:]
    # the loop, smoothed into the runs: one dense list, then evened out to ~1.5 mm
    dense = upper + loop + exit_ + lower                       # no repeated points: each piece starts past the last one's end
    # arc length
    s = [0.0]
    for a, b in zip(dense, dense[1:]):
        s.append(s[-1] + (b - a).length)
    L = s[-1]
    cut_upper = s[len(upper) - 1]                              # where the loop begins (the upper run's last point)
    cut_lower = s[len(upper) + len(loop) + len(exit_) - 1]     # where the lower run begins (its point at the loop)
    # heights: a later strand rides over an earlier one it overlaps (two turns of a gathered loop)
    ys = [p.y for p in dense]
    for i, p in enumerate(dense):
        if p.y > FLAT_Y + 1e-6:
            continue                                         # in the air near a ring
        best = ys[i]
        for j in range(i):
            if s[i] - s[j] < 30.0:
                break
            d = math.hypot(p.x - dense[j].x, p.z - dense[j].z)
            if d < 2 * STRAP_HALF_W:
                lift = 2 * STRAP_HALF_T * min(1.0, (2 * STRAP_HALF_W - d) / STRAP_HALF_W)
                best = max(best, ys[j] + lift)
        ys[i] = best
    # ramps: a triangular smoothing over +-9 mm that never lowers a point below where it rests
    sm = []
    for i in range(len(dense)):
        acc, wsum = 0.0, 0.0
        for j in range(len(dense)):
            dd = abs(s[j] - s[i])
            if dd < 9.0:
                w_ = 1.0 - dd / 9.0
                acc += ys[j] * w_
                wsum += w_
        sm.append(max(ys[i], acc / wsum) if dense[i].y <= FLAT_Y + 1e-6 else ys[i])
    for i, p in enumerate(dense):
        p.y = sm[i]
    # tangents and the across direction W: edge-on at each ring (along the wire), flat on the mat, a gentle roll on the loop
    T = []
    for i in range(len(dense)):
        a, b = dense[max(i - 1, 0)], dense[min(i + 1, len(dense) - 1)]
        T.append((b - a).normalized())
    scratch_zone = [(Vector((x, 0.0, LOOP_B[0][1] + LOOP_B[2] + dz))) for (x, dz) in SCRATCHES]
    W, N = [], []
    for i, p in enumerate(dense):
        w_flat, n_flat = _flat_frame(T[i])
        rho = 0.0
        for side, s0 in ((-1, s[i]), (1, L - s[i])):
            if s0 < TWIST_TO:
                wire = rings[side][2]
                r0 = math.atan2(wire.dot(n_flat), wire.dot(w_flat))
                if r0 > math.pi / 2:
                    r0 -= math.pi
                elif r0 < -math.pi / 2:
                    r0 += math.pi
                rho = r0 * (1.0 - hl.smoothstep(s0 / TWIST_TO))
        if cut_upper < s[i] < cut_lower:
            # the loop's roll, as leather lies: +-10 degrees, flat where the scratches are and at the cuts
            calm = min([1.0] + [hl.smoothstep((math.hypot(p.x - q.x, p.z - q.z) - 12.0) / 14.0) for q in scratch_zone])
            calm *= hl.smoothstep((s[i] - cut_upper) / 20.0) * hl.smoothstep((cut_lower - s[i]) / 20.0)
            rho = math.radians(10.0) * math.sin(2 * math.pi * (s[i] - cut_upper) / 150.0 + 0.6) * calm
            p.y += STRAP_HALF_W * abs(math.sin(rho)) + STRAP_HALF_T * (math.cos(rho) - 1.0)
        w = w_flat * math.cos(rho) + n_flat * math.sin(rho)
        n = -w_flat * math.sin(rho) + n_flat * math.cos(rho)
        W.append(w)
        N.append(n)
    return {'pts': dense, 's': s, 'T': T, 'W': W, 'N': N, 'L': L, 'cut_upper': cut_upper, 'cut_lower': cut_lower, 'rings': rings}


def _resample(design, s0, s1, step=7.0, max_turn=14.0):
    """Indices into the design's dense points between arc lengths s0 and s1: about every `step` mm, closer where it bends."""
    s, T = design['s'], design['T']
    idx = [i for i in range(len(s)) if s0 - 1e-6 <= s[i] <= s1 + 1e-6]
    keep = [idx[0]]
    for i in idx[1:-1]:
        last = keep[-1]
        bend = math.degrees(T[last].angle(T[i], 0.0))
        if s[i] - s[last] >= step or bend >= max_turn:
            keep.append(i)
    if keep[-1] != idx[-1]:
        keep.append(idx[-1])
    return keep


def _sections(frames):
    """frames: [(P, T, W)] -> section rings of Vectors (real mm)."""
    rings = []
    for P, T, W in frames:
        N = T.cross(W).normalized()
        W = N.cross(T).normalized()
        rings.append([P + W * a + N * b for (a, b) in SECTION])
    return rings


def _sweep(frames, origin):
    """The strap's solid along frames: (leather bmesh, edge bmesh), in this file's 2x Blender frame about origin (real mm,
    Unity). The face and back are leather; the two burnished edges and the cut ends are the edge colour.

    Every face is turned to point OUT of the strap: away from the section's middle (or, for the cut ends, along the strap
    out of its end). Until 19 Oct 2026 the two bmeshes were each run through recalc_face_normals, but the leather on its
    own is two open sheets (the face and the back, with the edges in the other bmesh), and on an open sheet Blender's
    "outside" is a guess: on the runs both sheets came out facing the mat and on the loop both faced up. Blender's review
    renders draw both sides of a face, so the strap looked right there; Unity draws front faces only, so in the game the
    runs were see-through from above and the loop from below, with only the stitches and the burnished edges floating.
    His girlfriend's playtest (19 Oct): "That's invisible... Strap." Found again by Review 1 - model sheets (the face
    test: 63-73 % of the runs seen from above were the backs of their faces)."""
    rings = _sections(frames)
    leather, edge = bmesh.new(), bmesh.new()
    n = len(SECTION)

    def middle(ring):
        return sum((Vector(p) for p in ring), Vector((0.0, 0.0, 0.0))) / len(ring)

    def outward(face, out_mm):
        """Flip face unless it points along out_mm (a Unity real-mm direction, taken into this file's Blender frame)."""
        face.normal_update()
        want = Vector(to_blender((out_mm.x, out_mm.y, out_mm.z)))
        if face.normal.dot(want) < 0:
            face.normal_flip()

    for a, b in zip(rings, rings[1:]):
        mid = (middle(a) + middle(b)) * 0.5
        for k in range(n):
            m = (k + 1) % n
            quad = [a[k], a[m], b[m], b[k]]
            dst = leather if k in STRAP_FACES else edge
            face = dst.faces.new([dst.verts.new(_b(q, origin)) for q in quad])
            outward(face, middle(quad) - mid)
    for ring, beyond in ((rings[0], rings[1]), (rings[-1], rings[-2])):
        cap = edge.faces.new([edge.verts.new(_b(q, origin)) for q in ring])
        outward(cap, middle(ring) - middle(beyond))   # the cut end faces out of the strap's end
    for bm in (leather, edge):
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-7)
        bm.normal_update()   # no recalc_face_normals: every face was turned out above, and recalc guesses on open sheets
    return leather, edge


def _b(p_mm, origin_mm):
    """A real-mm Unity point (prefab frame) as this file's 2x Blender coordinates about an object's origin (real mm)."""
    d = (Vector(p_mm) - Vector(origin_mm)) * (AUTHORED_SCALE / 1000.0)
    return Vector(to_blender((d.x, d.y, d.z)))


def _stitches(design, s0, s1, side=1.0, offset=0.0, origin=(0, 0, 0), skip=()):
    """Cream stitch dashes on the strap's face (+N), 1.5 mm in from each edge: 2.5 mm long every 4 mm. offset lifts them
    along N (the fold-back's outer face); skip is a list of (s0, s1) to leave bare."""
    bm = bmesh.new()
    pts, S, T, W, N = design['pts'], design['s'], design['T'], design['W'], design['N']
    k = 0
    s = s0 + 1.0
    while s < s1 - 1.0:
        if not any(a <= s <= b for a, b in skip):
            while k < len(S) - 2 and S[k + 1] < s:
                k += 1
            f = (s - S[k]) / max(S[k + 1] - S[k], 1e-9)
            P = pts[k].lerp(pts[k + 1], f)
            t = T[k].lerp(T[k + 1], f).normalized()
            w = W[k].lerp(W[k + 1], f).normalized()
            n = (N[k].lerp(N[k + 1], f).normalized()) * side
            for across in (-(STRAP_HALF_W - 1.5), STRAP_HALF_W - 1.5):
                c = P + w * across + n * (STRAP_HALF_T + offset + 0.05)
                q = [c - t * 1.25 - w * 0.3, c + t * 1.25 - w * 0.3, c + t * 1.25 + w * 0.3, c - t * 1.25 + w * 0.3]
                face = bm.faces.new([bm.verts.new(_b(v, origin)) for v in q])
                face.normal_update()
                want = Vector(to_blender((n.x, n.y, n.z)))
                if face.normal.dot(want) < 0:
                    face.normal_flip()
        s += 4.0
    return bm


def _ring_and_end(design, side, origin):
    """At one end: the split ring through the lug, the teardrop protector, the strap's end folded back round the ring's
    wire (40 mm, the U-turn and the fold) and the keeper over both layers. Returns [(part name, bmesh, material name)]."""
    hole, wrap_guess, wire, u = design['rings'][side]
    pts, S, T, W, N, L = design['pts'], design['s'], design['T'], design['W'], design['N'], design['L']
    out = []
    # the main strap's first point at this end, and its frame there (the frames run lens -> opening; at the opening end
    # the strap leaves the ring along -T)
    i0 = 0 if side < 0 else len(pts) - 1
    sgn = 1.0 if side < 0 else -1.0
    P0, T0, W0, N0 = pts[i0], T[i0] * sgn, W[i0], N[i0]          # N: the strap's face (up on the mat) at both ends
    gap = STRAP_HALF_T + RING_WIRE                      # the wire's centre is this far off the strap's centre, on +N0
    wire_c = P0 + N0 * gap
    # the ring: through the lug's hole and round the wire at the strap's U-turn
    centre = (hole + wire_c) * 0.5
    radius = (wire_c - hole).length / 2
    e1 = (wire_c - hole).normalized()
    e2 = (W0 - e1 * W0.dot(e1)).normalized()            # the ring's plane: the hole-to-wire line and the wire's own direction
    ring = bmesh.new()
    segs, sides = 14, 5
    vv = []
    for a in range(segs):
        ang = 2 * math.pi * a / segs
        c = centre + (e1 * math.cos(ang) + e2 * math.sin(ang)) * radius
        radial = (c - centre).normalized()
        nrm = e1.cross(e2).normalized()
        vv.append([ring.verts.new(_b(c + (radial * math.cos(2 * math.pi * k / sides) + nrm * math.sin(2 * math.pi * k / sides)) * RING_WIRE, origin))
                   for k in range(sides)])
    for a in range(segs):
        b = (a + 1) % segs
        for k in range(sides):
            m = (k + 1) % sides
            ring.faces.new([vv[a][k], vv[a][m], vv[b][m], vv[b][k]])
    bmesh.ops.recalc_face_normals(ring, faces=ring.faces[:])
    out.append(('split ring', ring, 'HD_CamChrome'))
    # the protector: a teardrop of dark leather against the end plate, under the lug, the ring through its narrow end
    tear = []
    for k in range(16):
        a = 2 * math.pi * k / 16
        r = 6.0 if math.sin(a) < 0 else 6.0 * (1.0 - 0.55 * math.sin(a) ** 2)
        tear.append((r * math.cos(a), -4.0 + 9.0 * math.sin(a) if math.sin(a) > 0 else -4.0 + 6.0 * math.sin(a)))
    xf = side * (LUG_HOLE[0] - 1000 * LUG_OUT / AUTHORED_SCALE + 0.9)
    prot = bmesh.new()
    lo = [prot.verts.new(_b((xf - side * 0.75, hole.y - 4.0 + ty, hole.z + tx), origin)) for tx, ty in tear]
    hi = [prot.verts.new(_b((xf + side * 0.75, hole.y - 4.0 + ty, hole.z + tx), origin)) for tx, ty in tear]
    prot.faces.new(lo)
    prot.faces.new(list(reversed(hi)))
    for k in range(16):
        m = (k + 1) % 16
        prot.faces.new([lo[k], lo[m], hi[m], hi[k]])
    bmesh.ops.recalc_face_normals(prot, faces=prot.faces[:])
    out.append(('ring protector', prot, 'HD_CamStrapEdge'))
    # the fold: from the free end (40 mm along) back to the wire, on the main strap's +N face, then round the wire
    frames = []
    s_here = S[i0]
    for k in range(int(FOLD / 4.0), -1, -1):
        target = (s_here + sgn * k * 4.0)
        j = min(range(len(S)), key=lambda q: abs(S[q] - target))
        lift = 2 * STRAP_HALF_T + (2 * RING_WIRE) * max(0.0, 1.0 - k * 4.0 / 6.0)
        frames.append((pts[j] + N[j] * lift, -T[j] * sgn, W[j]))
    for k in range(1, 8):                               # round the wire: from the fold's start to the main strap's start
        phi = math.pi * k / 8
        p = wire_c + (N0 * math.cos(phi) - T0 * math.sin(phi)) * gap
        t = (-N0 * math.sin(phi) - T0 * math.cos(phi)).normalized()
        frames.append((p, t, W0))
    frames.append((P0, T0, W0))
    leather, edge = _sweep(frames, origin)
    out.append(('strap end, folded', leather, 'HD_CamStrap'))
    out.append(('strap end, edges', edge, 'HD_CamStrapEdge'))
    # the fold's own stitches (its outer face)
    sk = bmesh.new()
    for k in range(1, int((FOLD - 4.0) / 4.0)):
        target = s_here + sgn * (k * 4.0 + 1.0)
        j = min(range(len(S)), key=lambda q: abs(S[q] - target))
        P, t, w, n = pts[j], T[j], W[j], N[j]
        for across in (-(STRAP_HALF_W - 1.5), STRAP_HALF_W - 1.5):
            c = P + w * across + n * (2 * STRAP_HALF_T + STRAP_HALF_T + 0.05)
            q = [c - t * 1.25 - w * 0.3, c + t * 1.25 - w * 0.3, c + t * 1.25 + w * 0.3, c - t * 1.25 + w * 0.3]
            face = sk.faces.new([sk.verts.new(_b(v, origin)) for v in q])
            face.normal_update()
            want = Vector(to_blender((n.x, n.y, n.z)))
            if face.normal.dot(want) < 0:
                face.normal_flip()
    out.append(('fold stitches', sk, 'HD_CamStitch'))
    # the keeper: a leather band round both layers, 8 mm long, 25 mm from the ring
    target = s_here + sgn * KEEPER_AT
    j = min(range(len(S)), key=lambda q: abs(S[q] - target))
    t, w, n = T[j], W[j], N[j]
    c = pts[j] + n * STRAP_HALF_T
    kb = hl.rrect_wall(-4.0, 4.0, 2 * STRAP_HALF_W + 3.0, 4 * STRAP_HALF_T + 2.6, 1.2, 1.1, per_corner=1)
    for v in kb.verts:
        lx, ly, lz = v.co
        v.co = _b(c + w * lx + n * ly + t * lz, origin)
    bmesh.ops.recalc_face_normals(kb, faces=kb.faces[:])
    out.append(('keeper', kb, 'HD_CamStrapEdge'))
    return out


_DESIGN = {}


def design():
    if 'd' not in _DESIGN:
        _DESIGN['d'] = strap_design()
    return _DESIGN['d']


def _origin_mm(name):
    """An object's prefab place in real mm (Unity), from PREFAB_OFFSETS (2x, relative to the body)."""
    x, y, z = PREFAB_OFFSETS[name]
    return (1000 * x / AUTHORED_SCALE, 1000 * (y + BODY_Y) / AUTHORED_SCALE, 1000 * z / AUTHORED_SCALE)


def strap_piece(name):
    """Upper strap (the lens end: its ring, protector, fold and keeper, the run to the loop), Worn loop, Lower strap."""
    d = design()
    origin = _origin_mm(name)
    p = Faceted(name)
    leather_m, edge_m, stitch_m = M('HD_CamStrap'), M('HD_CamStrapEdge'), M('HD_CamStitch')
    s0, s1 = {'UpperStrap': (0.0, d['cut_upper']), 'WornLoop': (d['cut_upper'], d['cut_lower']), 'LowerStrap': (d['cut_lower'], d['L'])}[name]
    idx = _resample(d, s0, s1)
    frames = [(d['pts'][i], d['T'][i], d['W'][i]) for i in idx]
    leather, edge = _sweep(frames, origin)
    # keep_normals: _sweep and _stitches turn every face out themselves; the leather and the dashes are open sheets whose
    # outside a recalc only guesses (19 Oct 2026: the runs came out facing the mat, see-through from above in the game).
    p.add('strap', leather, leather_m, smooth=True, keep_normals=True)
    p.add('strap edges', edge, edge_m, smooth=True, keep_normals=True)
    skip = [(0.0, FOLD + 3.0), (d['L'] - FOLD - 3.0, d['L'])]
    p.add('stitches', _stitches(d, s0, s1, origin=origin, skip=skip), stitch_m, keep_normals=True)
    if name != 'WornLoop':
        for part, bm, mat in _ring_and_end(d, -1 if name == 'UpperStrap' else 1, origin):
            p.add(part, bm, M(mat), smooth=mat in ('HD_CamStrap', 'HD_CamStrapEdge') and 'keeper' not in part and 'protector' not in part,
                  keep_normals=part in ('strap end, folded', 'strap end, edges', 'fold stitches'))
    return p


def scratch_places():
    """Where the prefab's three Old scratch cubes go (real metres, Unity, the prefab's root frame): on the loop's upper
    turn's far strand, which lies flat along x there, 0.1 mm into its face (they stand 0.35 mm proud)."""
    d = design()
    out = []
    for (x, dz) in SCRATCHES:
        z_line = LOOP_B[0][1] + LOOP_B[2]
        best = min(range(len(d['pts'])), key=lambda i: (d['pts'][i].x - x) ** 2 + (d['pts'][i].z - z_line) ** 2
                   + (0 if d['cut_upper'] < d['s'][i] < d['cut_lower'] else 1e9) - 50.0 * d['pts'][i].y)
        p, n = d['pts'][best], d['N'][best]
        top = p + n * STRAP_HALF_T
        out.append((round(x / 1000.0, 5), round((top.y + 0.1) / 1000.0, 5), round((p.z + dz) / 1000.0, 5)))
    return out


PIECES = [
    (body, ['Camera body']),
    (lens_housing, ['Lens housing']),
    (lens_glass, ['Lens glass']),
    (film_path, ['Open film path']),
    (mechanism, ['Jammed shutter mechanism']),
    (lambda: blade(True), ['Bent shutter blade']),
    (lambda: blade(False), ['Working shutter blade']),
    (clean_mechanism, ['Clean shutter mechanism']),
    (lambda: strap_piece('UpperStrap'), ['Upper strap']),
    (lambda: strap_piece('LowerStrap'), ['Lower strap']),
    (lambda: strap_piece('WornLoop'), ['Worn loop']),
]

# The prefab's places at REAL size (Unity, the root's frame) that v3.6 moves, with today's 2x value for the record:
# Devices 2 bakes today's x 0.5, then sets these (TD §6.4; the bake leaves rotations and the blades' local places alone).
MOVES = [
    ('Camera body', (0.0, 0.023, 0.0), (0.0, 0.016, 0.0), 'the body 32 mm deep (was 23), its back where it was'),
    ('Lens housing', (-0.065, 0.055, -0.024), (-0.0325, 0.0365, -0.012), 'up 9 mm with the front face (the lens itself unchanged)'),
    ('Lens glass', (-0.065, 0.093, -0.024), (-0.0325, 0.0555, -0.012), 'the lens centre: up 9 mm with the front face'),
    ('Lens grime', (-0.065, 0.100, -0.024), (-0.0325, 0.059, -0.012), 'over the lens glass, up 9 mm'),
    ('Open film path', (0.056, 0.049, -0.043), (0.028, 0.0335, -0.0215), 'the film path, up 9 mm with the well'),
    ('Film-path grime', (0.045, 0.058, -0.043), (0.0225, 0.038, -0.0215), 'over the film gate, up 9 mm'),
    ('Film-guide grime', (0.079, 0.058, -0.043), (0.0395, 0.038, -0.0215), 'over the sprocket drum (the film guide), up 9 mm'),
    ('Jammed shutter mechanism', (0.053, 0.060, 0.041), (0.0265, 0.039, 0.0205), "the shutter's seat, up 9 mm with the well"),
    ('Upper strap', (-0.162, 0.026, -0.063), (-0.06375, 0.016, -0.0325), "new strap: the lens-end lug's hole (its ring, fold, keeper and run)"),
    ('Lower strap', (-0.162, 0.026, 0.063), (0.06375, 0.016, -0.0325), "new strap: the opening-end lug's hole (it was at the lens end)"),
    ('Worn loop', (-0.200, 0.026, 0.0), (-0.050, -0.0005, 0.095), "new strap: the loop's middle, on the mat behind the lens half"),
]


def build():
    """The camera, assembled as the prefab assembles it: the body is the root, the others its children at the prefab's
    places (relative to the body), then scaled to real size (TD §6.1). Returns (root, children, [(object, targets)])."""
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
    hl.scale_build(made, children, 1.0 / AUTHORED_SCALE)
    return root, children, made
