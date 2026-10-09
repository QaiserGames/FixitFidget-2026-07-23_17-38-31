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
  BT_Brush         a soft flat dusting brush, 140 mm: seven clumps of golden bristle cut to a chisel, a crimped tinned
                   ferrule with two grooves, a raw beech handle that flattens toward the butt.
  BT_PryTool       the repair shop's pry tool (a "jimmy"): a 0.8 x 8 mm steel blade in a black over-moulded handle
                   15 x 8.5 mm with green grip inlays and a steel bolster, 125 mm.
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
}


def M(name):
    return hl.material(BT_MATERIALS, name)


# ----------------------------------------------------------------------------- the five tools


def screwdriver():
    p = Faceted('BT_Screwdriver')
    steel, dark, black, green, white = M('BT_Steel'), M('BT_SteelDark'), M('BT_Grip_Black'), M('BT_Handle_Green'), M('BT_White')
    # the blade: 3 mm round, matt steel, 41 mm up into the neck; the tip ground to a point
    p.add('blade', revolve([(.0028, .0015), (.0410, .0015)], sides=10, bottom_tip=(0, 0, .0004)), steel)
    # the Phillips cross: four fins, 0.5 mm thick, as two crossed plates that taper to the point
    for rot in (0.0, 90.0):
        fin = tube([ring_rrect(0, 0, .0003, .0010, .0005, .0001, per_corner=1), ring_rrect(0, 0, .0030, .0026, .0005, .0001, per_corner=1),
                    ring_rrect(0, 0, .0036, .0026, .0005, .0001, per_corner=1)])
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
    # a hang hole through the butt: two dark dimples
    for sy in (1, -1):
        dimple = revolve([(0.0, .0017), (.0006, .0017)], sides=10)
        fb.turn(dimple, -90.0 * sy, 'X')
        p.add('hang hole', moved(dimple, 0, sy * .0036, .1170), dark)
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


def brush():
    p = Faceted('BT_Brush')
    bristle, steel, wood = M('BT_Bristle'), M('BT_Steel'), M('BT_Wood')
    # seven clumps of bristle, 28 mm exposed, cut to a chisel with a little unevenness (the ends are the origin, so the
    # unevenness is a few tenths above z = 0, never below), splaying a touch toward the tips
    ends = (.0012, .0002, .0008, 0.0, .0006, .0001, .0010)
    for k in range(7):
        x = (k - 3) * .0030
        e = ends[k]
        loops = [ring_rrect(x * 1.16, 0, e, .0029, .0064, .0010, per_corner=2),
                 ring_rrect(x * 1.11, 0, e + .0040, .0031, .0068, .0012, per_corner=2),
                 ring_rrect(x * 1.04, 0, .0160, .0031, .0056, .0012, per_corner=2),
                 ring_rrect(x, 0, .0260, .0029, .0046, .0010, per_corner=2),
                 ring_rrect(x, 0, .0330, .0028, .0044, .0010, per_corner=2)]
        p.add('bristle clump', tube(loops), bristle, smooth=True)
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
    A 15 x 18 cm terry microfibre cloth folded in quarters, lying on the glass (its underside is the origin): 75 x 90 mm,
    four layers of about 2 mm. The -X edge is the first fold and the +Y edge the second: there the top layer WRAPS down
    over the inner layers (which end inside the roll), so the stack's edge is a round shoulder. The +X and -Y edges are
    OPEN: the four layers end there one after another, pulled back by different amounts because nobody folds square,
    each ending in a soft lip (a terrace). The top puffs, sags toward the open edges, carries three old creases, and the
    free corner (+X, -Y) lifts. The closed corner (-X, +Y) is tight. (No triangle: a quarter fold has none.)
    """
    p = Faceted('BT_Cloth')
    yellow = M('BT_Cloth')
    sx, sy = .075, .090
    hx, hy = sx / 2, sy / 2
    t = .0021                                                      # terry microfibre: about 2 mm a layer
    # per layer, bottom (4) to top (1): pull-back from the open edges (+X, -Y) and inset from the fold edges (-X, +Y)
    layers = {4: ((0.0, 0.0), (0.0, 0.0)), 3: ((.0034, .0012), (.0016, .0020)), 2: ((.0016, .0046), (.0026, .0032)), 1: ((.0050, .0032), (0.0, 0.0))}
    # the three inner layers: flat slabs with a soft top lip (one chamfer), their terraces showing at the open edges
    for k in (4, 3, 2):
        (px, py), (ix, iy) = layers[k]
        z0 = (4 - k) * t
        x0, x1, y0, y1 = -hx + ix, hx - px, -hy + py, hy - iy
        slab_ = fb.zbox(x0, x1, y0, y1, z0 - (.0003 if k != 4 else 0.0), z0 + t)
        fb.bevel_all(slab_, .0007, segments=1)
        p.add('layer %d' % k, slab_, yellow, smooth=True)
    # the top layer: a draped surface over the stack, wrapping down at the fold edges, lipped at the open edges
    (px, py), _ = layers[1]
    x0, x1, y0, y1 = -hx, hx - px, -hy + py, hy
    cell = .0018
    nx, ny = int(round((x1 - x0) / cell)), int(round((y1 - y0) / cell))

    def shoulder(d, w, floor):
        s = max(0.0, min(1.0, d / w))
        return floor + (1.0 - floor) * math.sqrt(max(0.0, 1.0 - (1.0 - s) ** 2))

    def base(x, y):
        # the underside: on the inner layers inside, down onto the bottom layer at the fold edges
        wrap = min(smoothstep((x - x0) / .0046), smoothstep((y1 - y) / .0056))
        return t + 2 * t * wrap

    def top(x, y):
        h = base(x, y) + t
        u, v = (x - x0) / (x1 - x0), (y - y0) / (y1 - y0)
        dome = max(0.0, 4.0 * u * (1 - u) * 4.0 * v * (1 - v))
        h += .0024 * dome ** .6 + .0006 * (1 - u) * v
        d1 = abs((x - .004) - (y + .010) * .55)
        d2 = abs((x + .012) + (y - .006) * .8)
        d3 = abs((y - .028) - (x + .02) * .2)
        h -= .0008 * math.exp(-(d1 / .006) ** 2) + .0006 * math.exp(-(d2 / .007) ** 2) + .0005 * math.exp(-(d3 / .005) ** 2)
        # the lips: the layer's own rounded edge, all four sides (the fold sides round down onto the wrap)
        edge = min(x - x0, x1 - x, y - y0, y1 - y)
        lip = shoulder(edge, .0017, .40)
        return base(x, y) + (h - base(x, y)) * lip + curl(x, y)

    def curl(x, y):
        # the free corner lifts as a sheet: most at the tip, nothing 16 mm in
        dc = math.hypot(max(0.0, x1 - x), max(0.0, y - y0))
        return .0046 * smoothstep(1.0 - dc / .020) ** 1.2

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


# ----------------------------------------------------------------------------- the list


TOOLS = [
    # (builder, Unity's ToolType, what it is, and what the bench does with it)
    (screwdriver, 'Screwdriver', "screwdriver: the blade's edge is the origin; turn about Y at a screw"),
    (tweezers, 'Tweezers', "tweezers: the points are the origin; Leaf_L and Leaf_R turn about their own axis to pinch"),
    (brush, 'Brush', "brush: the bristles' ends are the origin; strokes along the surface"),
    (pry_tool, 'Pry', "pry tool: the blade's edge is the origin; the lever goes in flat along -Y"),
    (cloth, 'Cloth', "cloth: its face on the glass is the origin; wiped in circles"),
]


def build_all():
    """Every tool, finished and audited: [{object, children, tool, what, islands, floating, tris, size, min, max, materials}]."""
    out = []
    for builder, tool, what in TOOLS:
        built = builder()
        if isinstance(built, tuple):
            root, children = built
        else:
            root, children = built.finish(), []
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
        out.append({'object': root, 'children': children, 'tool': tool, 'what': what, 'islands': islands,
                    'floating': floating, 'tris': tris,
                    'size': tuple(round(hi[k] - lo[k], 4) for k in range(3)),
                    'min': tuple(round(lo[k], 4) for k in range(3)), 'max': tuple(round(hi[k], 4) for k in range(3)),
                    'materials': mats})
    return out
