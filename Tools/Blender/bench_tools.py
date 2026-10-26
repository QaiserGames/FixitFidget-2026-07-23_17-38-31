"""
The bench's tools, v3 (9 Oct 2026): the five tools of the close-up, built from measured references in the house style
(hero_lib.py says why; claude/hero-reference-sheet.md has the measurements and the photographs they came from).

  BT_Screwdriver   a precision driver, 134 mm, after the Wiha PicoFinish: a 3 mm matt steel blade with a Phillips tip, a
                   black handle with the three-diameter silhouette (a 9 mm spinning waist, a 16 mm grip with four green
                   soft pads, a free-spinning cap behind a groove, a white cross on its top).
  BT_Tweezers      fine-point ESD tweezers, 120 mm (Dumont No. 5 proportions): two 1.2 mm arms 10 mm wide at a rounded
                   heel, diverging and tapering slowly to needle points 4 mm apart, bright ground tips on a black
                   coated body, a hairline serration band on the outer faces. Three meshes (the heel the root at the
                   points, Leaf_L / Leaf_R with their origin at the heel) as ToolPickup needs.
  BT_Brush         a soft flat dusting brush, 140 mm: one block of golden bristle with five grooves a face, cut to a chisel
                   (24 Oct 2026, art audit a23: the seven clumps it had read as a rake), a crimped tinned ferrule with two
                   grooves, a raw beech handle that flattens toward the butt.
  BT_PryTool       the repair shop's pry tool (a "jimmy"): a 0.8 x 8 mm steel blade in a black over-moulded handle
                   15 x 8.5 mm with green grip inlays and a steel bolster, 125 mm (no hang hole: nothing to hang it on).
  BT_Cloth         a microfibre lens cloth, 15 x 18 cm, folded in quarters: 75 x 90 mm, four layers, two rounded fold
                   edges, two open edges with the four layer-lips stepped, a puffed top, the free corner lifting.

What never changes: 1 unit = 1 m, Z up; the ORIGIN IS THE WORKING TIP (the blade's edge, the points, the bristles' ends,
the cloth's face on the glass); the handle goes up +Z; a flat tool's flat side faces -Y; nothing below z = 0; the file
names; the tweezers' joints (TWEEZERS_HEEL, the leaves' points ~4 mm apart, closed by ToolPickup's 1.1 degree turn).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from hero_lib import Faceted, bez, lerp, moved, revolve, ring_rrect, ring_round, smoothstep, tube  # noqa: E402
from mathutils import Vector  # noqa: E402

# (sRGB hex, Unity smoothness, Unity metallic): warm, muted, metallic 0 throughout (the art style guide)
BT_MATERIALS = {
    'BT_Steel':        ('#B9B5AD', 0.50, 0.0),   # satin steel: the blade, the pry blade, the ferrule
    'BT_SteelBright':  ('#DAD7D0', 0.62, 0.0),   # ground steel: the tweezers' tips
    'BT_SteelDark':    ('#6B6862', 0.40, 0.0),   # the driver's tip, the pry tool's bolster
    'BT_Grip_Black':   ('#2B2724', 0.26, 0.0),   # black ABS and rubber: handles, the tweezers' coating
    'BT_Handle_Green': ('#2E7D5B', 0.30, 0.0),   # the brand's green: the driver's pads, the pry tool's inlays
    'BT_Wood':         ('#C9A878', 0.30, 0.0),   # raw beech
    'BT_Bristle':      ('#CFA253', 0.10, 0.0),   # golden synthetic bristle
    'BT_Cloth':        ('#D9BD5C', 0.05, 0.0),   # a yellow microfibre lens cloth
    'BT_White':        ('#F2EFE8', 0.30, 0.0),   # the cap's printed cross
    'BT_Cork':         ('#A57B4F', 0.15, 0.0),   # the tool rest's cloth tray lining (14 Oct 2026)
}
# the café's own, used by name (Unity keeps its values; BlenderKitSteps maps them from Assets/Art/Materials)
EXISTING = {
    'DC_Steel':        ('#93908A', 0.50, 0.0),   # warm satin steel: the tool rest (bent sheet steel)
}


def M(name):
    return hl.material(BT_MATERIALS if name in BT_MATERIALS else EXISTING, name)


SCREW_HEAD = .0038            # the devices' screw heads (hero_phone.SCREW_HEAD; feel-targets call 3)
TIP_CROSS = 0.6 * SCREW_HEAD  # the driver's tip cross: 2.28 mm (was 2.6)


# ----------------------------------------------------------------------------- the five tools


def screwdriver():
    p = Faceted('BT_Screwdriver')
    steel, dark, black, green, white = M('BT_Steel'), M('BT_SteelDark'), M('BT_Grip_Black'), M('BT_Handle_Green'), M('BT_White')
    # the blade: 3 mm round, matt steel, 41 mm up into the neck; the tip ground to a point
    p.add('blade', revolve([(.0028, .0015), (.0410, .0015)], sides=10, bottom_tip=(0, 0, .0004)), steel)
    # the Phillips cross: four fins, 0.5 mm thick, as two crossed plates that taper to the point. 14 Oct 2026 (wave 2): the
    # cross is TIP_CROSS (2.3 mm, 0.6 x the 3.8 mm screw head, feel-targets call 3; was 2.6), so the tip fits the screws
    for rot in (0.0, 90.0):
        fin = tube([ring_rrect(0, 0, .0003, TIP_CROSS * .385, .0005, .0001, per_corner=1), ring_rrect(0, 0, .0030, TIP_CROSS, .0005, .0001, per_corner=1),
                    ring_rrect(0, 0, .0036, TIP_CROSS, .0005, .0001, per_corner=1)])
        fb.turn(fin, rot, 'Z')
        p.add('tip fin', fin, dark)
    # the handle: neck, spinning waist, grip swell, the groove, then the cap (measured: Wiha PicoFinish, 134 mm)
    handle = [(.0400, .0020), (.0430, .0030), (.0470, .0042), (.0500, .0046), (.0560, .0045), (.0660, .0045), (.0740, .0049),
              (.0800, .0056), (.0880, .0070), (.1000, .0078), (.1140, .0080), (.1200, .0078), (.1215, .0072), (.1220, .0066)]
    p.add('handle', revolve(handle, sides=16), black)
    cap = [(.1220, .0066), (.1226, .0066), (.1230, .0080), (.1236, .0084), (.1325, .0084), (.1335, .0078), (.1340, .0066)]
    p.add('cap', revolve(cap, sides=16), black)
    # four soft green pads on the grip zone, a touch proud of the black core
    for k in range(4):
        pad = fb.zbox(.0068, .0084, -.0032, .0032, .0900, .1180)
        fb.bevel_all(pad, .0007, segments=1)
        fb.turn(pad, 45.0 + 90.0 * k, 'Z')
        p.add('pad', pad, green)
    # the printed cross on the cap's top
    for rot in (0.0, 90.0):
        bar = fb.zbox(-.0026, .0026, -.0005, .0005, .1338, .1343)
        fb.turn(bar, rot, 'Z')
        p.add('cap cross', bar, white)
    return p


def pry_tool():
    p = Faceted('BT_PryTool')
    steel, dark, black, green = M('BT_Steel'), M('BT_SteelDark'), M('BT_Grip_Black'), M('BT_Handle_Green')
    # the blade: 8 mm wide (X), 0.8 thick (Y), its edge thinned and its corners eased; up into the handle
    blade = tube([ring_rrect(0, 0, 0.0000, .0074, .0003, .0010, per_corner=2), ring_rrect(0, 0, .0022, .0080, .0008, .0012, per_corner=2),
                  ring_rrect(0, 0, .0500, .0080, .0008, .0012, per_corner=2)])
    p.add('blade', blade, steel)
    bolster = fb.zbox(-.0055, .0055, -.0019, .0019, .0450, .0500)
    p.add('bolster', bolster, dark, bevel=.0008)
    # the over-moulded handle: a rounded 15 x 8.5 section, 77 mm, domed at the butt
    handle = tube([ring_rrect(0, 0, .0480, .0126, .0066, .0024, per_corner=2), ring_rrect(0, 0, .0520, .0150, .0085, .0030, per_corner=2),
                   ring_rrect(0, 0, .1120, .0150, .0085, .0030, per_corner=2), ring_rrect(0, 0, .1200, .0136, .0074, .0028, per_corner=2),
                   ring_rrect(0, 0, .1245, .0100, .0054, .0022, per_corner=2)])
    p.add('handle', handle, black)
    for sy in (1, -1):
        inlay = fb.zbox(-.0040, .0040, min(sy * .0036, sy * .0048), max(sy * .0036, sy * .0048), .0620, .1100)
        p.add('grip inlay', inlay, green, bevel=.0006)
    return p


TWEEZERS_HEEL = .112      # z of the heel (where the arms join); each leaf's origin


def tweezers():
    """Returns (root object, [Leaf_L, Leaf_R])."""
    black, bright = M('BT_Grip_Black'), M('BT_SteelBright')
    root = Faceted('BT_Tweezers')
    # the welded heel: both arms as one 2.4 mm block, 10 mm wide, its end rounded (the semicircle seen on every pair)
    secs = []
    for z, w in ((TWEEZERS_HEEL - .0070, .0100), (TWEEZERS_HEEL + .0030, .0100), (TWEEZERS_HEEL + .0050, .0096), (TWEEZERS_HEEL + .0064, .0082),
                 (TWEEZERS_HEEL + .0074, .0060), (TWEEZERS_HEEL + .0079, .0034), (TWEEZERS_HEEL + .0080, .0012)):
        secs.append([(-.0012, -w / 2, z), (.0012, -w / 2, z), (.0012, w / 2, z), (-.0012, w / 2, z)])
    root.add('heel', tube(secs), black)
    root_ob = root.finish()
    leaves = []
    for side, name in ((-1, 'Leaf_L'), (1, 'Leaf_R')):
        leaf = Faceted(name)
        # the arm, in the leaf's frame: origin at the heel, the point at z = -TWEEZERS_HEEL
        n = 18
        secs = []
        for i in range(n + 1):
            t = i / n
            z = .0040 - (TWEEZERS_HEEL + .0040) * t
            xc = side * bez(t, .0006, .0050, .0021)                      # the centre line: out to 3.2 mm, in to the point
            if t < .30:
                w = .0100
            elif t < .93:
                w = lerp(.0100, .0014, (t - .30) / .63)
            else:
                w = lerp(.0014, .0003, (t - .93) / .07)
            th = lerp(.0012, .0009, t) if t < .5 else lerp(.0009, .00025, (t - .5) / .5)
            secs.append([(xc - th / 2, -w / 2, z), (xc + th / 2, -w / 2, z), (xc + th / 2, w / 2, z), (xc - th / 2, w / 2, z)])
        body = list(reversed(secs))                                        # bottom (the point) to top (the heel)
        split = 3                                                          # the last ~17 mm: the ground, bright tip
        leaf.add('tip', tube(body[:split + 1], cap_top=False), bright)
        leaf.add('arm', tube(body[split:], cap_bottom=False), black)
        # the serration band on the outer face: seven hairline ridges, 40-60 % of the way to the points
        for k in range(7):
            z = -.0450 - k * .0036
            t = -z / TWEEZERS_HEEL
            xo = side * (bez(t, .0006, .0050, .0021) + lerp(.0012, .0009, t) / 2)
            w = lerp(.0100, .0014, (t - .30) / .63) if t > .30 else .0100
            xa, xb = xo - side * .0001, xo + side * .0003
            ridge = fb.zbox(min(xa, xb), max(xa, xb), -w * .42, w * .42, z - .0004, z + .0004)
            leaf.add('serration', ridge, black)
        ob = leaf.finish()
        ob.parent = root_ob
        ob.location = (0, 0, TWEEZERS_HEEL)
        leaves.append(ob)
    return root_ob, leaves


BRISTLE_TUFTS = 6           # the bristle block's tufts across its width: five grooves on each broad face


def _bristle_ring(z, sx, sy, groove, tip_z=None, rc=.0012, tufts=BRISTLE_TUFTS):
    """
    One section of the bristle block (24 Oct 2026, art audit a23): a rounded rectangle sx across X and sy through Y whose two
    broad faces dip `groove` deep between the tufts, so the block reads as bristle gathered in bundles. Every ring has the
    same points in the same order (tube() lofts them). tip_z(x), when given, sets each point's height (the chisel's uneven
    end); otherwise the ring is flat at z.
    """
    rc = min(rc, .45 * min(sx, sy))
    hx, hy = sx / 2 - rc, sy / 2
    w = 2 * hx / tufts
    xs = []
    for k in range(tufts):
        x0 = -hx + k * w
        xs += [x0, x0 + .25 * w, x0 + .5 * w, x0 + .75 * w]
    xs.append(hx)

    def dip(i):                       # a groove at each inner tuft boundary, its shoulders a third as deep, the tuft's middle full
        if i == 0 or i == len(xs) - 1:
            return 0.0
        return (1.0, .30, 0.0, .30)[i % 4] * groove

    def zz(x):
        return z if tip_z is None else tip_z(x)

    pts = [(x, -(hy - dip(i)), zz(x)) for i, x in enumerate(xs)]                     # the front face (-Y), toward +X
    for k in range(1, 3):                                                            # the right edge, rounded
        a = math.radians(-90 + 60 * k)
        pts.append((hx + rc * math.cos(a), hy * math.sin(a), zz(hx + rc * math.cos(a))))
    pts += [(x, hy - dip(i), zz(x)) for i, x in reversed(list(enumerate(xs)))]       # the back face (+Y), toward -X
    for k in range(1, 3):                                                            # the left edge, rounded
        a = math.radians(90 + 60 * k)
        pts.append((-hx + rc * math.cos(a), hy * math.sin(a), zz(-hx + rc * math.cos(a))))
    return pts


def brush():
    p = Faceted('BT_Brush')
    bristle, steel, wood = M('BT_Bristle'), M('BT_Steel'), M('BT_Wood')
    # the bristles (24 Oct 2026, art audit a23): ONE tapered block, 25.5 mm exposed, with five grooves down each broad face
    # (six tufts), flaring a little in width toward the end and thinning to a chisel 3 mm through, cut with a little
    # unevenness (the end is the origin, so the unevenness is a few tenths above z = 0, never below). It was seven separate
    # clumps splaying like a fork, and in the close-up they read as a rake (R3; R1 counted the brush among the good tools;
    # his answer to a23 was "Yes, do that"). The block still runs up inside the ferrule to 33 mm, and its rings are close
    # enough below the mouth (BRUSH_MOUTH) for the Bend key to curve it smoothly.
    def tip(x):
        # each tuft cut a few tenths longer or shorter than its neighbours, and nicked where two tufts meet, so the chisel's
        # edge reads as bristle rather than one cut solid; the longest tuft's end is z = 0
        half = .0232 / 2 - .0012                      # the end ring's straight half-width (sx / 2 - rc)
        u = min(max((x + half) / (2 * half / BRISTLE_TUFTS), 0.0), BRISTLE_TUFTS - 1e-6)
        k, f = int(u), u - int(u)
        nick = max(0.0, 1.0 - min(f, 1.0 - f) * 5.0)
        return (.00055, .00010, .00035, 0.0, .00045, .00015)[k] + .00035 * nick
    rows = [  # (z, width, thickness, groove depth); None: the uneven end
        (None, .0232, .0030, .00050),
        (.0010, .0238, .0040, .00070),
        (.0045, .0237, .0049, .00085),
        (.0085, .0233, .0055, .00090),
        (.0130, .0228, .0058, .00085),
        (.0175, .0224, .0059, .00070),
        (.0215, .0221, .0057, .00055),
        (.0255, .0218, .0053, .00040),
        (.0290, .0212, .0049, .0),
        (.0330, .0204, .0044, .0),
    ]
    loops = [_bristle_ring(0.0 if z is None else z, sx, sy, g, tip_z=tip if z is None else None) for z, sx, sy, g in rows]
    p.add('bristle block', tube(loops), bristle, smooth=True)
    # the ferrule: tinned steel, crimped flat at the mouth (24 x 6) and tapering over 22 mm to round (9) at the handle, a
    # flat crimp at the mouth and two crimp grooves near the handle
    ferrule = [ring_rrect(0, 0, .0255, .0236, .0058, .0026, per_corner=3), ring_rrect(0, 0, .0262, .0244, .0066, .0030, per_corner=3),
               ring_rrect(0, 0, .0275, .0244, .0066, .0030, per_corner=3), ring_rrect(0, 0, .0282, .0236, .0062, .0028, per_corner=3),
               ring_rrect(0, 0, .0320, .0228, .0070, .0032, per_corner=3), ring_rrect(0, 0, .0380, .0196, .0082, .0038, per_corner=3),
               ring_rrect(0, 0, .0430, .0152, .0090, .0042, per_corner=3), ring_rrect(0, 0, .0470, .0112, .0094, .0046, per_corner=3),
               ring_rrect(0, 0, .0490, .0098, .0094, .0046, per_corner=3), ring_rrect(0, 0, .0496, .0090, .0086, .0042, per_corner=3),
               ring_rrect(0, 0, .0508, .0090, .0086, .0042, per_corner=3), ring_rrect(0, 0, .0514, .0096, .0092, .0046, per_corner=3),
               ring_rrect(0, 0, .0530, .0096, .0092, .0046, per_corner=3), ring_rrect(0, 0, .0536, .0088, .0084, .0042, per_corner=3),
               ring_rrect(0, 0, .0548, .0088, .0084, .0042, per_corner=3), ring_rrect(0, 0, .0554, .0094, .0090, .0045, per_corner=3),
               ring_rrect(0, 0, .0580, .0092, .0088, .0044, per_corner=3)]
    p.add('ferrule', tube(ferrule), steel)
    # the handle: round where it enters the ferrule, flattening and widening toward a chamfered butt
    handle = [ring_round(0, 0, .0560, .0042, .0042, 16), ring_round(0, 0, .0700, .0048, .0044, 16), ring_round(0, 0, .0950, .0058, .0040, 16),
              ring_round(0, 0, .1200, .0064, .0040, 16), ring_round(0, 0, .1380, .0062, .0038, 16), ring_round(0, 0, .1400, .0048, .0028, 16)]
    p.add('handle', tube(handle), wood)
    return p


def cloth():
    """
    A 15 x 18 cm suede microfibre lens cloth folded in quarters, lying on the glass (its underside is the origin): 75 x 90
    mm, four layers of 0.8 mm, about 5 mm with the puff (14 Oct 2026, Art's A4.1: today's 2.1 mm layers were terry-thick;
    a lens cloth is 0.5-0.8 mm; the free corner lifts 6 mm, the grab point; ~2,500 triangles, from 4,358). The -X edge is the first fold and the +Y edge the second: there the top layer WRAPS down
    over the inner layers (which end inside the roll), so the stack's edge is a round shoulder. The +X and -Y edges are
    OPEN: the four layers end there one after another, pulled back by different amounts because nobody folds square,
    each ending in a soft lip (a terrace). The top puffs, sags toward the open edges, carries three old creases, and the
    free corner (+X, -Y) lifts. The closed corner (-X, +Y) is tight. (No triangle: a quarter fold has none.)
    """
    p = Faceted('BT_Cloth')
    yellow = M('BT_Cloth')
    sx, sy = .075, .090
    hx, hy = sx / 2, sy / 2
    t = .0008                                                      # suede lens cloth: 0.8 mm a layer
    # per layer, bottom (4) to top (1): pull-back from the open edges (+X, -Y) and inset from the fold edges (-X, +Y)
    layers = {4: ((0.0, 0.0), (0.0, 0.0)), 3: ((.0034, .0012), (.0016, .0020)), 2: ((.0016, .0046), (.0026, .0032)), 1: ((.0050, .0032), (0.0, 0.0))}
    # the three inner layers: flat slabs with a soft top lip (one chamfer), their terraces showing at the open edges
    for k in (4, 3, 2):
        (px, py), (ix, iy) = layers[k]
        z0 = (4 - k) * t
        x0, x1, y0, y1 = -hx + ix, hx - px, -hy + py, hy - iy
        slab_ = fb.zbox(x0, x1, y0, y1, z0 - (.0002 if k != 4 else 0.0), z0 + t)
        fb.bevel_all(slab_, .00025, segments=1)
        p.add('layer %d' % k, slab_, yellow, smooth=True)
    # the top layer: a draped surface over the stack, wrapping down at the fold edges, lipped at the open edges
    (px, py), _ = layers[1]
    x0, x1, y0, y1 = -hx, hx - px, -hy + py, hy
    cell = .0025
    nx, ny = int(round((x1 - x0) / cell)), int(round((y1 - y0) / cell))

    def shoulder(d, w, floor):
        s = max(0.0, min(1.0, d / w))
        return floor + (1.0 - floor) * math.sqrt(max(0.0, 1.0 - (1.0 - s) ** 2))

    def base(x, y):
        # the underside: on the inner layers inside, down onto the bottom layer at the fold edges
        wrap = min(smoothstep((x - x0) / .0030), smoothstep((y1 - y) / .0036))
        return t + 2 * t * wrap

    def top(x, y):
        h = base(x, y) + t
        u, v = (x - x0) / (x1 - x0), (y - y0) / (y1 - y0)
        dome = max(0.0, 4.0 * u * (1 - u) * 4.0 * v * (1 - v))
        h += .0016 * dome ** .6 + .0004 * (1 - u) * v
        d1 = abs((x - .004) - (y + .010) * .55)
        d2 = abs((x + .012) + (y - .006) * .8)
        d3 = abs((y - .028) - (x + .02) * .2)
        h -= .0005 * math.exp(-(d1 / .006) ** 2) + .0004 * math.exp(-(d2 / .007) ** 2) + .0003 * math.exp(-(d3 / .005) ** 2)
        # the lips: the layer's own rounded edge, all four sides (the fold sides round down onto the wrap)
        edge = min(x - x0, x1 - x, y - y0, y1 - y)
        lip = shoulder(edge, .0011, .40)
        return base(x, y) + (h - base(x, y)) * lip + curl(x, y)

    def curl(x, y):
        # the free corner lifts as a sheet: most at the tip, nothing 16 mm in
        dc = math.hypot(max(0.0, x1 - x), max(0.0, y - y0))
        return .0060 * smoothstep(1.0 - dc / .022) ** 1.2

    def under(x, y):
        # the layer's underside: on the stack, except where the corner lifts (then the sheet's own thickness)
        return max(base(x, y) - .0002, top(x, y) - t)

    bm = bmesh.new()
    verts = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x = x0 + (x1 - x0) * i / nx
            y = y0 + (y1 - y0) * j / ny
            verts[(i, j)] = bm.verts.new((x, y, top(x, y)))
    for i in range(nx):
        for j in range(ny):
            bm.faces.new([verts[(i, j)], verts[(i + 1, j)], verts[(i + 1, j + 1)], verts[(i, j + 1)]])
    # the skirt: from the boundary down to the layer's underside (the wall of the lip); under the lifted corner the
    # underside itself is built, so the sheet has a visible back there
    ring = [(i, 0) for i in range(nx)] + [(nx, j) for j in range(ny)] + [(i, ny) for i in range(nx, 0, -1)] + [(0, j) for j in range(ny, 0, -1)]
    low = {}

    def low_vert(key):
        if key not in low:
            v = verts[key]
            low[key] = bm.verts.new((v.co.x, v.co.y, under(v.co.x, v.co.y)))
        return low[key]

    for a, b in zip(ring, ring[1:] + ring[:1]):
        bm.faces.new([verts[b], verts[a], low_vert(a), low_vert(b)])
    for i in range(nx):
        for j in range(ny):
            keys = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(curl(verts[k].co.x, verts[k].co.y) > .0004 for k in keys):
                bm.faces.new([low_vert(k) for k in reversed(keys)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # the surface's normals must point up (recalc on an open shell can pick either side)
    up = sum(f.normal.z for f in bm.faces if abs(f.normal.z) > .5)
    if up < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    p.add('top layer', bm, yellow, smooth=True)
    return p


# ----------------------------------------------------------------------------- the bench stage's steel (14 Oct 2026)
#
# Art Direction's briefs A2-A5 (specs/art-bench-and-cafe.md). The rest and the tray are built in the bench rig's own frame
# (like bench_stage.py's mat): imported with no turn, their Unity +x is the player's right and +z away from the player, so
# in Blender the player sits at +Y. Origin: the bottom centre. Unity (x, y, z) = Blender (-x, z, -y).

REST_W, REST_D, REST_T = .110, .230, .002            # the base plate: across, deep (toward/away), thick
NEAR_BAR = {'y': .020, 'h': .016, 't': .003}         # from the near edge, tall above the plate, thick
FAR_BAR = {'y': .120, 'h': .009, 't': .003}
CLOTH_TRAY = {'y0': .150, 'y1': .230, 'w': .100, 'h': .006, 'wall': .0012, 'cork': .001}
REST_TILT = 5.0                                      # degrees: the handles higher (Art: "about 5")
# the four tools, left to right as the player sees them, 25 mm apart (Art x = player's right); tip = how far the working
# tip lies from the near edge; the far bar's V notch half-width and depth for each (the U notches are 18 mm wide)
REST_TOOLS = [
    # (builder name, ToolType, Art x, tip from the near edge, V half-width, V depth)
    ('screwdriver', 'Screwdriver', -.0375, .145, .0035, .0045),
    ('pry_tool', 'Pry', -.0125, .140, .0060, .0045),
    ('tweezers', 'Tweezers', .0125, .133, .0045, .0045),
    ('brush', 'Brush', .0375, .147, .0140, .0045),
]


def _rest_y(from_near):
    """Blender y of a point this far from the rest's near edge (the near edge, toward the player, at +Y)."""
    return REST_D / 2 - from_near


def _bar_profile(x_half, z0, z_top, notches):
    """A bar's outline in (x, z), counter-clockwise from its bottom left: notches [(x, kind, a, b)] cut from its top;
    kind 'U' (a = half-width, b = bottom z: a half-round bottom under straight sides) or 'V' (a = half-width, b = depth)."""
    pts = [(-x_half, z0)]
    top = []
    for x, kind, a, b in sorted(notches):
        if kind == 'U':
            zc = b + a                                   # the half-round's centre
            arc = [(x + a * math.cos(t), zc + a * math.sin(t)) for t in (math.pi + math.pi * k / 10 for k in range(11))]
            arc = [(px, pz) for px, pz in arc if pz < z_top - 1e-5]
            if zc < z_top:
                top += [(x - a, z_top), (x - a, zc)] + arc[1:-1] + [(x + a, zc), (x + a, z_top)]
            else:                                        # a shallow notch: only the arc's foot
                ends = math.sqrt(max(0.0, a * a - (z_top - zc) ** 2))
                top += [(x - ends, z_top)] + arc + [(x + ends, z_top)]
        else:
            top += [(x - a, z_top), (x, z_top - b), (x + a, z_top)]
    # the top edge runs right to left in a counter-clockwise outline
    pts += [(x_half, z0), (x_half, z_top)] + list(reversed(top)) + [(-x_half, z_top)]
    return pts


def _extrude_xz(profile, y0, y1):
    """An outline in (x, z) extruded along Y from y0 to y1; the two ends are filled by ear clipping (concave)."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in profile]
    b = [bm.verts.new((x, y1, z)) for x, z in profile]
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    # the two ends: a constrained Delaunay fill (ear clipping made zero-area triangles from the collinear points along
    # the bar's top between its notches)
    from mathutils import Vector as V, geometry
    vc, _e, tris, orig, _oe, _of = geometry.delaunay_2d_cdt([V(p) for p in profile], [], [list(range(n))], 1, 1e-9)
    if len(vc) != n:
        raise ValueError('the bar outline crosses itself')
    for t in tris:
        ids = [orig[i][0] for i in t]
        bm.faces.new([a[i] for i in reversed(ids)])
        bm.faces.new([b[i] for i in ids])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def _rest_mesh(u_bottoms):
    """The rest itself, with each tool's U notch bottom (z, m) as given."""
    p = Faceted('BT_ToolRest')
    steel, cork = M('DC_Steel'), M('BT_Cork')
    plate = hl.chamfered_slab(REST_W, REST_D, REST_T, .006, .0004, per_corner=3, centre_z=REST_T / 2)
    p.add('base plate', plate, steel)
    xh = REST_W / 2 - .003
    near = [(-x, 'U', .009, zb) for (_, _, x, _, _, _), zb in zip(REST_TOOLS, u_bottoms)]     # Blender x = -Art x
    prof = _bar_profile(xh, REST_T - .0002, REST_T + NEAR_BAR['h'], near)
    y = _rest_y(NEAR_BAR['y'])
    p.add('near bar', _extrude_xz(prof, y - NEAR_BAR['t'] / 2, y + NEAR_BAR['t'] / 2), steel)
    far = [(-x, 'V', vw, vd) for (_, _, x, _, vw, vd) in REST_TOOLS]
    prof = _bar_profile(xh, REST_T - .0002, REST_T + FAR_BAR['h'], far)
    y = _rest_y(FAR_BAR['y'])
    p.add('far bar', _extrude_xz(prof, y - FAR_BAR['t'] / 2, y + FAR_BAR['t'] / 2), steel)
    # the cloth tray at the far end: steel walls round a cork lining
    ct = CLOTH_TRAY
    cy = (_rest_y(ct['y0']) + _rest_y(ct['y1'])) / 2
    wall = hl.rrect_wall(REST_T - .0002, REST_T + ct['h'], ct['w'], ct['y1'] - ct['y0'], .004, ct['wall'], per_corner=2)
    hl.moved(wall, 0.0, cy, 0.0)
    p.add('cloth tray wall', wall, steel)
    liner = fb.zbox(-ct['w'] / 2 + ct['wall'] - .0002, ct['w'] / 2 - ct['wall'] + .0002,
                    cy - (ct['y1'] - ct['y0']) / 2 + ct['wall'] - .0002, cy + (ct['y1'] - ct['y0']) / 2 - ct['wall'] + .0002,
                    REST_T - .0003, REST_T + ct['cork'])
    p.add('cork lining', liner, cork)
    return p.finish()


def _world_mesh(objs):
    """World-space vertices and polygons of some mesh objects, for a BVH."""
    from mathutils.bvhtree import BVHTree
    verts, polys = [], []
    for o in objs:
        mw = o.matrix_world
        base = len(verts)
        verts += [mw @ v.co for v in o.data.vertices]
        polys += [[base + i for i in poly.vertices] for poly in o.data.polygons]
    return verts, polys, BVHTree.FromPolygons(verts, polys)


def _clearances(tool_objs, rest_ob):
    """How far the tool (held high) can drop before it meets the rest: (at the near bar, at the far bar, anywhere)."""
    from mathutils import Vector as V
    bpy.context.view_layer.update()
    tv, _tp, tbvh = _world_mesh(tool_objs)
    rv, _rp, rbvh = _world_mesh([rest_ob])
    down, up = V((0, 0, -1)), V((0, 0, 1))
    y_near, y_far = _rest_y(NEAR_BAR['y']), _rest_y(FAR_BAR['y'])
    best = {'near': 9.0, 'far': 9.0, 'other': 9.0}

    def which(y):
        if abs(y - y_near) < .006:
            return 'near'
        if abs(y - y_far) < .006:
            return 'far'
        return 'other'
    for v in tv:                                     # from the tool down onto the rest
        hit = rbvh.ray_cast(v, down, 1.0)
        if hit[0] is not None:
            k = which(hit[0].y)
            best[k] = min(best[k], hit[3])
    for v in rv:                                     # from the rest up into the tool (a notch's edge between tool points)
        hit = tbvh.ray_cast(v, up, 1.0)
        if hit[0] is not None:
            k = which(v.y)
            best[k] = min(best[k], hit[3])
    return best['near'], best['far'], best['other']


def _place_tool(objs, root, x_b, tip_from_near, z):
    from mathutils import Euler
    root.location = (x_b, _rest_y(tip_from_near), z)
    root.rotation_euler = Euler((math.radians(-(90.0 - REST_TILT)), 0.0, 0.0))


def tool_rest():
    """
    BT_ToolRest (Art's A2): a bench-top tool rest in bent sheet steel (the café's DC_Steel), 110 x 230 mm. A near bar 16 mm
    tall with a U notch per tool takes the handles; a far bar 9 mm tall with a V notch per tool takes the blades; the
    tools lie on them 5 degrees tipped, handles higher and toward the player, tips away, 25 mm apart, left to right
    screwdriver, pry tool, tweezers, brush (the work's order; the brush nearest the device), each with its flat side up.
    At the far end a cork-lined tray 100 x 80 mm holds the folded cloth. Each U notch's depth is fitted to its tool (the
    tool rests on both bars, measured by rays: nothing floats, nothing sinks), and each tool's resting pose is an empty,
    a child of the rest: Seat_Screwdriver, Seat_Pry, Seat_Tweezers, Seat_Brush, Seat_Cloth (where the tool's origin, its
    working tip, goes; its turn). Cut: hang holes, a brand plate, magnets (the auto-tray does the holding), a fifth slot.
    Returns (rest object, [the seat empties]).
    """
    from mathutils import Euler
    builders = {'screwdriver': screwdriver, 'pry_tool': pry_tool, 'tweezers': tweezers, 'brush': brush}
    bottoms = [.0080] * len(REST_TOOLS)
    poses = []
    for attempt in range(3):                         # fit, adjust the notches, check
        rest = _rest_mesh(bottoms)
        poses, adjust = [], []
        for (name, ttype, x_art, tip, _vw, _vd), zb in zip(REST_TOOLS, bottoms):
            built = builders[name]()
            if isinstance(built, tuple):
                root, kids = built
            else:
                root, kids = built.finish(), []
            objs = [root] + kids
            high = .060
            _place_tool(objs, root, -x_art, tip, high)
            near, far, other = _clearances(objs, rest)
            drop = min(near, far, other)
            poses.append((ttype, root.location.copy(), root.rotation_euler.copy(), near - drop, far - drop, other - drop, high - drop))
            adjust.append(near - far)                # > 0: the handle hovers over its notch by this much
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
        if all(abs(a) < .0002 for a in adjust) or attempt == 2:
            break
        bottoms = [zb + a for zb, a in zip(bottoms, adjust)]
        bpy.data.objects.remove(rest, do_unlink=True)
    seats = []
    for ttype, loc, rot, gap_near, gap_far, gap_other, z in poses:
        e = bpy.data.objects.new('Seat_' + ttype, None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_type = 'ARROWS'
        e.empty_display_size = .01
        e.parent = rest
        e.location = (loc.x, loc.y, z)
        e.rotation_euler = rot
        e['gaps_mm'] = [round(gap_near * 1000, 2), round(gap_far * 1000, 2), round(gap_other * 1000, 2)]
        seats.append(e)
    # the cloth: folded, on the cork, turned a quarter so its 90 mm side lies across the tray and its lifted corner
    # faces the player (the cloth's own free corner is its +X -Y corner; a quarter turn sends it to Blender +Y)
    ct = CLOTH_TRAY
    e = bpy.data.objects.new('Seat_Cloth', None)
    bpy.context.scene.collection.objects.link(e)
    e.empty_display_type = 'ARROWS'
    e.empty_display_size = .01
    e.parent = rest
    e.location = (0.0, (_rest_y(ct['y0']) + _rest_y(ct['y1'])) / 2, REST_T + ct['cork'])
    e.rotation_euler = Euler((0.0, 0.0, math.radians(90.0)))
    seats.append(e)
    rest['notch_bottoms_mm'] = [round(b * 1000, 2) for b in bottoms]
    return rest, seats


def scrap_tray():
    """
    BT_ScrapTray (Art's A3): the old-parts bin, a shallow stainless instrument tray 160 x 90 x 22 mm, corners r 10,
    slightly flared walls 0.8 mm thick, a rolled rim, satin BT_Steel. It stands on the mat's far band behind the device;
    a different material and height say "out of the job", and dark broken parts read on its L* 74. Cut: handles, a lid,
    graduations, a brand. One closed shell, ~250 triangles.
    """
    p = Faceted('BT_ScrapTray')
    steel = M('BT_Steel')
    loops = []
    for z, sx, sy, r in ((0.0, .150, .080, .006), (.0008, .151, .081, .0065), (.0195, .156, .086, .009), (.0212, .1592, .0892, .0106),
                         (.0220, .1576, .0876, .0098), (.0210, .1552, .0852, .0086), (.0195, .1544, .0844, .0082), (.0008, .1484, .0784, .0052)):
        loops.append(ring_rrect(0, 0, z, sx, sy, r, per_corner=3))
    p.add('tray', tube(loops), steel)
    tray = p.finish()
    # Systems' scrapSlots (their spec 4.0.1): three places on the floor (0.8 mm up) in the tray's right two-thirds, as the
    # player sees it (Art x -10, +25, +60 mm = Blender -x), centred in depth. A long part (the phone's 133 mm screen) lies
    # along the tray's length and takes the middle one.
    slots = []
    for k, x_art in enumerate((-0.010, 0.025, 0.060)):
        e = bpy.data.objects.new('Slot_%d' % (k + 1), None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_type = 'PLAIN_AXES'
        e.empty_display_size = .01
        e.parent = tray
        e.location = (-x_art, 0.0, .0008)
        slots.append(e)
    return tray, slots


# ----------------------------------------------------------------------------- the cloth in the hand, the brush's bend

# BT_ClothHeld v2 (26 Oct 2026; art audit a02, S1: "The cloth is a smooth yellow cone that looks like a rubber duck. The
# folded cloth in its tray, and the Wipe icon, look right". His words, 26 Oct: "it was an issue i brought up to you before,
# i would want it to look less like a glob, more like someone is grabbing a cloth and cleaning an item in a sense"; and
# playtest 7, his girlfriend: "it looks like a glob"). The cone's tall smooth tail read as a duck's neck, and so does any
# gathered neck of cloth while the fist round it is out of sight (three tries on 26 Oct: a ruffled wad with hanging
# corners read as popcorn with fangs, a pleated knot as a paper lantern, a gathered tail as a bow tie). What reads as a
# cloth is the tray's own folded cloth, so this is that cloth, grabbed: two layers on the glass with soft rounded lips,
# turned a little off the stroke as it came to hand, its edges never quite straight, and pinched up near its middle where
# the unseen fingers hold it, into a soft peak that draws the cloth in round it, with folds running down from the pinch
# toward the corners and the corner nearest Ace lifting off the glass. Its wiping face is the flat cloth all round the
# pinch. Smooth (the hero rule for soft things), its thin edges left sharp so the cloth's thinness shows. One sheet with
# thickness: a grid across (u) and along (v, from the far edge toward Ace), its top and its underside, and a rim round it.
HELD_NU, HELD_NV = 19, 21                            # the grid: across, along
HELD_HALF = .026                                     # half the cloth's width: 52 mm across
HELD_FAR, HELD_LEN = -.028, .056                     # its far edge (up the screen, at the cursor while wiping); its length
HELD_T = .0026                                       # two layers of the folded cloth
PINCH = (0.0, .007)                                  # where the fingers have hold of it: near the middle, toward Ace
PINCH_H, PINCH_R = .0220, .0125                      # the peak rises 22 mm over the cloth; how far round it it lifts
# the folds running down from the pinch: (direction round it, radians from Blender +X; spread; depth)
PINCH_FOLDS = [(.35, .30, 1.0), (1.35, .38, -.7), (2.15, .26, .9), (2.95, .42, .6), (3.90, .30, -.8), (4.60, .36, 1.0), (5.55, .28, .7)]
PRESS_SPREAD = 1.08                                  # pressed, the cloth spreads a little and the pinch sinks
HELD_TURN = 26.0                                     # the cloth turned this far off the stroke (degrees), as it came to hand


def _held_point(t, s, press=0.0, bunch=0.0):
    """
    The cloth's top surface and its thickness at (t across, -1..1; s along, 0 at the far edge, 1 at the near end), for a
    press and a bunch (0..1). Returns (x, y, z_top, thickness).
    """
    a = abs(t)
    spread = 1.0 + (PRESS_SPREAD - 1.0) * press
    # laid out: its corners rounded (nobody folds a cloth to a sharp corner), its edges never quite straight, and the
    # whole cloth turned HELD_TURN off the stroke, held as it came to hand
    corner = smoothstep((a - .55) / .45) * max(1.0 - smoothstep(s / .12), smoothstep((s - .88) / .12))
    wave = 1.0 + .035 * math.sin(7.0 * t + 3.0 * s + 1.0) * smoothstep((max(a, abs(2 * s - 1)) - .8) / .2)
    fx = t * HELD_HALF * (1.0 - .16 * corner) * wave
    fy = (HELD_FAR + s * HELD_LEN) * wave
    c, sn = math.cos(math.radians(HELD_TURN)), math.sin(math.radians(HELD_TURN))
    x, y = (fx * c - fy * sn) * spread, (fx * sn + fy * c) * spread
    # the pinch: the cloth lifted to a soft peak where it is held, drawn in round it, the folds running down from it to
    # the corners; on a stroke the peak leans back against it (the stroke runs along Unity +x = Blender -x)
    px, py = PINCH[0] + .0060 * bunch, PINCH[1]
    dx, dy = x - px, y - py
    d = math.hypot(dx, dy)
    lift = math.exp(-(d / PINCH_R) ** 1.4)
    x -= dx * .24 * lift ** .7
    y -= dy * .24 * lift ** .7
    z = HELD_T + PINCH_H * (1.0 - .15 * press) * lift
    theta = math.atan2(dy, dx)
    ridge = 0.0
    for cdir, w, depth in PINCH_FOLDS:
        dt = (theta - cdir + math.pi) % (2 * math.pi) - math.pi
        ridge += depth * math.exp(-(dt / w) ** 2)
    z += .0060 * ridge * 4.0 * lift * (1.0 - lift)
    y += .0040 * lift ** 2                                           # the peak leans toward the hand
    # the corner nearest Ace (the turned layout's +Y) lifts off the glass toward the grip; the others lie flat
    z += .0080 * (1.0 - .35 * press) * corner ** 1.5 * smoothstep(((fx * sn + fy * c) - .010) / .015)
    # the lips: the cloth's own rounded edge, all round
    edge = min((1.0 - a) * HELD_HALF, s * HELD_LEN, (1.0 - s) * HELD_LEN)
    lip = .45 + .55 * math.sqrt(max(0.0, 1.0 - (1.0 - min(1.0, edge / .0016)) ** 2))
    z = HELD_T * lip + (z - HELD_T)
    # four old creases across it, away from the pinch
    for k, (ct, cs, cw, depth) in enumerate(((.9, 3.0, .22, .00045), (.6, -2.2, .18, .00035), (-.4, 1.4, .16, .00030), (1.2, .8, .20, .00030))):
        dd = abs(t * ct + (s - (.30, .50, .70, .20)[k]) * cs - (0.0, .35, -.2, .5)[k])
        z -= depth * math.exp(-(dd / cw) ** 2) * (1.0 - lift)
    # the stroke: the leading side (Blender -x) of the cloth rolls up off the glass, the trailing side drags behind
    if bunch:
        lead = smoothstep((-x / HELD_HALF - .55) / .45)
        z += bunch * .0035 * lead * (1.0 - lift)
        x += bunch * .0080 * smoothstep((x / HELD_HALF - .2) / .8) * (1.0 - lift)
    return x, y, max(z, .0006 + .6 * HELD_T * lift), HELD_T


def _held_positions(press=0.0, bunch=0.0):
    """
    Every vertex of the held cloth in one fixed order for any press and bunch (0..1), so the Basis and both keys share one
    topology: the top grid (u across, then v along), then the underside grid. bunch 1 = the stroke at full speed, along
    Unity +X (Blender -X).
    """
    from mathutils import Vector as V
    top, under = [], []
    for jv in range(HELD_NV):
        s = jv / (HELD_NV - 1)
        for iu in range(HELD_NU):
            t = -1.0 + 2.0 * iu / (HELD_NU - 1)
            x, y, z, thick = _held_point(t, s, press, bunch)
            top.append(V((x, y, z)))
            under.append(V((x, y, max(0.0, z - thick))))
    return top + under


def cloth_held():
    """
    BT_ClothHeld (Art's A4.2-3; v2 26 Oct 2026, art audit a02: see HELD_NU): the lens cloth, folded and grabbed. It lies
    on the glass (its underside is the origin's plane, z = 0, the cloth centred on the origin): 52 x 56 mm laid out, two
    layers (2.6 mm) with rounded lips, turned 26 degrees off the stroke, pinched up 7 mm toward the player (Blender +Y =
    Unity -Z, where the hand is) into a peak 25 mm over the glass, seven folds running down from it, the near corner lifted
    8 mm. One material, BT_Cloth, the folded cloth's own (the colour of the tray's cloth and the Wipe icon). From the
    close-up camera: a soft yellow cloth pinched up in the middle, held by a hand just out of sight.
    Shape keys (one topology): Press (the cloth spreads 8 %, the peak sinks 15 %, the corner lies lower) and Bunch (for a
    stroke along the model's Unity +X: the leading side rolls up 3.5 mm off the glass, the trailing side drags 8 mm, the
    peak leans 6 mm back against the stroke). Systems turns the cloth so Unity +X points along the stroke, sets Bunch from
    the stroke's speed and Press from the press (ToolPickup: Press 1 on the device, 0.55 in the air, Bunch up to 0.45),
    and leads with its far edge (here its far corner) from its vertices and keys each frame, so the new shape needs no
    code. Returns the object (the keys on it).
    """
    from mathutils import Vector as V
    base = _held_positions()
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in base]
    nu, nv = HELD_NU, HELD_NV
    grid = nu * nv

    def top(iu, jv):
        return vs[jv * nu + iu]

    def under(iu, jv):
        return vs[grid + jv * nu + iu]
    for jv in range(nv - 1):
        for iu in range(nu - 1):
            bm.faces.new([top(iu, jv), top(iu + 1, jv), top(iu + 1, jv + 1), top(iu, jv + 1)])
            bm.faces.new([under(iu, jv + 1), under(iu + 1, jv + 1), under(iu + 1, jv), under(iu, jv)])
    rim = []                                                      # the cloth's thin edges, left sharp
    for jv in range(nv - 1):
        for iu in (0, nu - 1):
            rim.append(bm.faces.new([top(iu, jv), under(iu, jv), under(iu, jv + 1), top(iu, jv + 1)]))
    for jv in (0, nv - 1):
        for iu in range(nu - 1):
            rim.append(bm.faces.new([top(iu, jv), top(iu + 1, jv), under(iu + 1, jv), under(iu, jv)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    fb.orient_shells(bm)
    for f in bm.faces:
        f.smooth = True
    for f in rim:
        f.smooth = False
    fb.box_uvs(bm)
    me = bpy.data.meshes.new('BT_ClothHeld')
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M('BT_Cloth'))
    ob = bpy.data.objects.new('BT_ClothHeld', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.shape_key_add(name='Basis', from_mix=False)
    for key_name, pts in (('Press', _held_positions(press=1.0)), ('Bunch', _held_positions(bunch=1.0))):
        key = ob.shape_key_add(name=key_name, from_mix=False)
        for i, p in enumerate(pts):
            key.data[i].co = V(p)
    return ob


BRUSH_MOUTH = .0255                                  # the ferrule's mouth: the bristles bend below it
BRUSH_BEND_DEG = 25.0


def brush_bend(ob):
    """
    The Bend shape key on BT_Brush (Art's A5): the bristles bend from the ferrule's mouth like a loaded cantilever,
    their end 25 degrees off the handle's line and 7.4 mm aside, toward the model's Unity -X (Blender +X: against a
    stroke along Unity +X), the tips staying on the working plane (z 0: the origin contract holds bent), and the end
    splays 2 mm (each edge 1 mm outward, the block 2 mm thicker). No new triangles. Systems turns the brush
    so Unity +X points along the stroke and sets Bend from the stroke's speed and the press. (24 Oct 2026: the same key on
    the one grooved bristle block that replaced the seven clumps, art audit a23.)
    """
    from mathutils import Vector as V
    me = ob.data
    bristle = me.materials.find('BT_Bristle')
    verts = set()
    for poly in me.polygons:
        if poly.material_index == bristle:
            verts.update(poly.vertices)
    L = BRUSH_MOUTH
    delta = (2.0 / 3.0) * L * math.radians(BRUSH_BEND_DEG)   # a cantilever's end deflection for that end slope
    if ob.data.shape_keys is None:
        ob.shape_key_add(name='Basis', from_mix=False)
    key = ob.shape_key_add(name='Bend', from_mix=False)
    moved = 0
    for i in verts:
        co = me.vertices[i].co
        if co.z >= L:
            continue
        s_ = (L - co.z) / L
        dx = delta * (3 * s_ * s_ - s_ ** 3) / 2.0
        splay_x = (co.x / .0105) * .0010 * s_ * s_
        y = co.y * (1.0 + .30 * s_ * s_)
        key.data[i].co = V((co.x + dx + splay_x, y, co.z))
        moved += 1
    ob['bend_vertices'] = moved
    return ob


# ----------------------------------------------------------------------------- the list


TOOLS = [
    # (builder, Unity's ToolType, what it is, and what the bench does with it)
    (screwdriver, 'Screwdriver', "screwdriver: the blade's edge is the origin; turn about Y at a screw"),
    (tweezers, 'Tweezers', "tweezers: the points are the origin; Leaf_L and Leaf_R turn about their own axis to pinch"),
    (brush, 'Brush', "brush: the bristles' ends are the origin; strokes along the surface"),
    (pry_tool, 'Pry', "pry tool: the blade's edge is the origin; the lever goes in flat along -Y"),
    (cloth, 'Cloth', "cloth, folded (on the rest): its face on the glass is the origin; wiped in circles"),
    (cloth_held, 'Cloth', "cloth in the hand: gathered in the grip, loose corners hanging, the pad's face on the glass the "
                          "origin; keys Press and Bunch (stroke along +X)"),
    (tool_rest, '', "tool rest: the five tools' seats (Seat_*) as children, its origin the bottom centre; no turn in the rig"),
    (scrap_tray, '', "old-parts tray: its origin the bottom centre; it stands on the mat's far band behind the device"),
]


def build_all():
    """Every piece, finished and audited: [{object, children, empties, tool, what, islands, floating, tris, size, min, max,
    materials, keys}]. children are the meshes under the root (the tweezers' leaves), empties the named places under it
    (the rest's seats); keys the shape keys (the held cloth's Press and Bunch, the brush's Bend)."""
    out = []
    for builder, tool, what in TOOLS:
        built = builder()
        if isinstance(built, tuple):
            root, kids = built
        elif isinstance(built, fb.Prop):
            root, kids = built.finish(), []
        else:
            root, kids = built, []
        if root.name == 'BT_Brush':
            brush_bend(root)
        children = [k for k in kids if k.type == 'MESH']
        empties = [k for k in kids if k.type == 'EMPTY']
        bpy.context.view_layer.update()
        tris, mins, maxs, mats, islands, floating = 0, [], [], [], 0, []
        objs = [root] + children
        for ob in objs:
            n, fl = fb.audit(ob)
            st = fb.stats(ob)
            off = Vector(ob.location) if ob.parent is not None else Vector((0, 0, 0))
            tris += st['tris']
            mins.append(Vector(st['min']) + off)
            maxs.append(Vector(st['max']) + off)
            mats += [m for m in st['materials'] if m not in mats]
            islands += n
            floating += fl
        lo = Vector((min(v.x for v in mins), min(v.y for v in mins), min(v.z for v in mins)))
        hi = Vector((max(v.x for v in maxs), max(v.y for v in maxs), max(v.z for v in maxs)))
        if len(objs) > 1:
            boxes = [(Vector(fb.stats(o)['min']) + (Vector(o.location) if o.parent else Vector()),
                      Vector(fb.stats(o)['max']) + (Vector(o.location) if o.parent else Vector())) for o in objs]
            for i, a in enumerate(boxes):
                best = min(fb.gap(a, b) for j, b in enumerate(boxes) if j != i)
                if best > .002:
                    floating.append((objs[i].name, 'part', round(best * 1000, 1)))
        keys = [k.name for k in root.data.shape_keys.key_blocks[1:]] if root.data.shape_keys else []
        out.append({'object': root, 'children': children, 'empties': empties, 'tool': tool, 'what': what, 'islands': islands,
                    'floating': floating, 'tris': tris,
                    'size': tuple(round(hi[k] - lo[k], 4) for k in range(3)),
                    'min': tuple(round(lo[k], 4) for k in range(3)), 'max': tuple(round(hi[k], 4) for k in range(3)),
                    'materials': mats, 'keys': keys})
    return out
