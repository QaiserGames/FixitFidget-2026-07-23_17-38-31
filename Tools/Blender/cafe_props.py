"""
The cafe redesign's room and props, v1 (Brief C, 9-10 Oct 2026): five big plants and the trailing pothos, the
ceiling beams and skylight wells, the water station, the bean bag, the service bell, the grinder and the sidewalk
bench. Built with Python in Blender 5.2 on fixit_blender.py and hero_lib.py (Tools/Blender), the house way:

  * REAL LIFE FIRST. Every piece starts from a measured reference (REFERENCES below, with links); the house style
    then simplifies it, and every part that does none of the four jobs (the player works it; it tells how the thing
    works; it makes the silhouette; it is this world's texture) is cut.
  * THE HOUSE STYLE. Flat-shaded (faceted), one-segment chamfers, metallic 0, warm greys, chunky where it is joinery,
    junctions from boundaries with 2-4 mm overlaps, no two faces in one plane, nothing floating (fb.audit).
  * FOLIAGE. Every leaf is a thin CLOSED shell (a midrib crease on top, a keel below), so it shows from both sides in
    Unity without two-sided materials, and flat shading draws the crease as the midrib. Species are told apart by
    their real leaf outlines, sizes and habits (the silhouette test at 2 m: renders/plants-silhouettes-2m.png).

Conventions (the house kit's): 1 unit = 1 m, Z up, the front faces -Y (Unity +Z); origin at the bottom centre; a
piece that hangs (the beam, the skylight well) has its origin on the ceiling plane; the pothos's origin is the
bottom of its saucer, which stands on the shelf 0.12 m behind the shelf's front edge (its vines hang over that edge).
Unity (x, y, z) = Blender (-x, z, -y). No UVs are needed (textures are world-space in Unity) except the bean bag's
label face, which carries UVs 0..1 for the Words lead's CF_BagLabel texture.
"""
import math
import os
import random
import sys

TOOLS = '/home/claude/cafe/inputs/Tools/Blender'
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# ============================================================================= materials
# (sRGB hex, Unity smoothness, why). Metallic 0 everywhere (the house rule).

EXISTING = {   # the project's own materials, used by their exact names (scene-facts.md, the palette); Unity keeps its own
    'CC_Wood_Counter': ('#8F5B32', 0.71, 'chestnut: the beams, the skylight trim, the tray, the bench slats (Cafe grain in Unity)'),
    'DC_Steel':        ('#93908A', 0.50, 'warm steel: the grinder burr housing, chute and fork, the skylight frame'),
    'DC_SteelDark':    ('#4E4B47', 0.50, 'dark warm steel: the beam hangers and bolts, the bench frames'),
    'T2_Brass':        ('#B8933F', 0.60, 'brass: the service bell'),
    'DC_Leaf':         ('#4C7A3F', 0.30, 'leaves'),
    'DC_Terracotta':   ('#B4633C', 0.15, 'terracotta pots and saucers; the rubber plant\'s red sheaths'),
}
CF_MATERIALS = {   # new, CF_ prefix (the brief's, then the ones this kit adds, each with its reason)
    'CF_LeafLight':  ('#6E9A55', 0.30, 'new growth (the brief)'),
    'CF_PotCream':   ('#EDE4D3', 0.60, 'cream-glazed pots (the brief)'),
    'CF_Soil':       ('#3B2A1E', 0.05, 'potting soil (the brief)'),
    'CF_Glass':      ('#DCE6E4', 0.90, 'glass: transparent in Unity (the palette)'),
    'CF_Water':      ('#BFD3D3', 0.95, 'water in the carafe: transparent in Unity (the brief)'),
    'CF_Kraft':      ('#B08A5E', 0.15, 'kraft paper bag (the brief)'),
    'CF_BagLabel':   ('#EDE4D3', 0.20, 'the bag label: the Words lead draws CF_BagLabel.png onto UVs 0..1 (the brief)'),
    'CF_LeafOlive':  ('#7B8B67', 0.25, 'NEW: the olive\'s grayish-green leaf tops (NC State); the brief asks for silvery olive leaves'),
    'CF_LeafSilver': ('#B9BDAA', 0.20, 'NEW: the olive\'s silver, hairy undersides (NC State): the flicker that says olive'),
    'CF_LeafDark':   ('#2F4A2D', 0.55, 'NEW: the rubber plant\'s dark, shiny leaves (the brief: glossy oval leaves)'),
    'CF_Bark':       ('#76695A', 0.10, 'NEW: trunks (fig, olive): a wood material would get Cafe grain in Unity'),
    'CF_Black':      ('#2B2724', 0.35, 'NEW: black powder coat and cast iron (grinder, bell base, the bag\'s valve and tie)'),
    'CF_Beans':      ('#4A2C1C', 0.35, 'NEW: roasted beans in the hopper (a wood material would get grain)'),
    'CF_ChalkWhite': ('#E6E2D8', 0.06, 'NEW: the skylight well\'s lining, the walls\' chalk white (the wall material lives in a sub-folder the kit importer does not search)'),
}
TRANSPARENT = ('CF_Glass', 'CF_Water')
TEXTURED = {'CF_BagLabel': 'Assets/Art/Textures/Cafe/CF_BagLabel.png'}


def M(name):
    hexstr, smooth, _ = EXISTING.get(name) or CF_MATERIALS[name]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


# ============================================================================= the piece

class Piece(fb.Prop):
    """A prop whose parts may carry two materials (a leaf's top and underside), flat-shaded."""

    def add_tagged(self, part_name, src_bm, mat_top, mat_under):
        """Faces tagged 'top' (a leaf's upper side) get mat_top, the rest mat_under."""
        tag = src_bm.faces.layers.int.get('top')
        dst = self.bm
        vmap = {v: dst.verts.new(v.co) for v in src_bm.verts}
        faces = []
        for f in src_bm.faces:
            try:
                nf = dst.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            top = (f[tag] == 1) if tag is not None else True
            nf.material_index = self.slot(mat_top if top else mat_under)
            nf.smooth = False
            faces.append(nf)
        src_bm.free()
        self.parts.append((part_name, faces))
        return faces

    def add_by_normal(self, part_name, src_bm, mat_up, mat_down, threshold=-0.2):
        """Faces turned down (normal z under threshold) get mat_down, the rest mat_up: the olive's silver undersides
        show from below and its grey-green from above, wherever the leaf or the mass is."""
        bmesh.ops.recalc_face_normals(src_bm, faces=src_bm.faces[:])
        dst = self.bm
        vmap = {v: dst.verts.new(v.co) for v in src_bm.verts}
        faces = []
        for f in src_bm.faces:
            down = f.normal.z < threshold
            try:
                nf = dst.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = self.slot(mat_down if down else mat_up)
            nf.smooth = False
            faces.append(nf)
        src_bm.free()
        self.parts.append((part_name, faces))
        return faces


# ============================================================================= geometry helpers

def bez3(p0, p1, p2, t):
    return p0 * (1 - t) ** 2 + p1 * (2 * (1 - t) * t) + p2 * (t * t)


def bez3_d(p0, p1, p2, t):
    return (p1 - p0) * (2 * (1 - t)) + (p2 - p1) * (2 * t)


def unit(v):
    v = Vector(v)
    return v.normalized() if v.length > 1e-9 else Vector((0, 0, 1))


def spin(profile, sides=16, phase=0.0):
    """A lathe from (r, z) points bottom to top; an end on the axis (r = 0) closes as a cone tip."""
    prof = list(profile)
    bottom_tip = top_tip = None
    if prof[0][0] <= 1e-6:
        bottom_tip = (0.0, 0.0, prof[0][1])
        prof = prof[1:]
    if prof[-1][0] <= 1e-6:
        top_tip = (0.0, 0.0, prof[-1][1])
        prof = prof[:-1]
    return fb.loft([(0.0, 0.0, z, r, r) for r, z in prof], sides=sides, cap_bottom=bottom_tip is None,
                   cap_top=top_tip is None, tip=top_tip, bottom_tip=bottom_tip, phase=phase)


def lathe_shell(outer, inner, sides=16):
    """A thin round shell open at both ends (a hopper): outer and inner [(r, z)] bottom to top, the same count;
    the walls joined by a rim at the top and at the bottom."""
    bm = bmesh.new()
    ring = lambda r, z: [bm.verts.new((r * math.cos(2 * math.pi * k / sides), r * math.sin(2 * math.pi * k / sides), z))
                         for k in range(sides)]
    o = [ring(r, z) for r, z in outer]
    i_ = [ring(r, z) for r, z in inner]
    def band(a, b):
        for k in range(sides):
            j = (k + 1) % sides
            bm.faces.new([a[k], a[j], b[j], b[k]])
    for a, b in zip(o, o[1:]):
        band(a, b)
    for a, b in zip(i_, i_[1:]):
        band(b, a)
    band(o[-1], i_[-1])
    band(i_[0], o[0])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def prism(profile, x0, x1):
    """A solid with a constant (y, z) profile along X (house_kit.prism)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile]
    b = [bm.verts.new((x1, y, z)) for y, z in profile]
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], b[i], b[j], a[j]])
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def square_ring(profile, half):
    """A square frame: each (d, z) of a closed profile becomes a square loop of half-size half + d at height z."""
    loops = []
    for d, z in profile:
        h = half + d
        loops.append([(h, -h, z), (h, h, z), (-h, h, z), (-h, -h, z)])
    loops.append(loops[0])
    bm = hl.tube(loops, cap_bottom=False, cap_top=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def sweep_var(points, radii, sides=6, cap_start=True, cap_end=True, lobes=None, twist=0.0, end_tip=None):
    """A tube along a polyline with a radius per point (parallel-transported frames); lobes (n, k) and a twist in
    degrees per ring make a fluted, twisting trunk (the olive's)."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    tans = []
    for i in range(n):
        if i == 0:
            d = pts[1] - pts[0]
        elif i == n - 1:
            d = pts[-1] - pts[-2]
        else:
            d = pts[i + 1] - pts[i - 1]
        tans.append(d.normalized())
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    u = ref.cross(tans[0]).normalized()
    loops = []
    for i in range(n):
        if i > 0:
            u = (tans[i - 1].rotation_difference(tans[i]) @ u).normalized()
        w = tans[i].cross(u).normalized()
        loop = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + math.radians(twist * i)
            r = radii[i]
            if lobes:
                r *= 1.0 + lobes[1] * math.cos(lobes[0] * a)
            loop.append(tuple(pts[i] + (u * math.cos(a) + w * math.sin(a)) * r))
        loops.append(loop)
    return hl.tube(loops, cap_bottom=cap_start, cap_top=cap_end and end_tip is None, tip=end_tip)


def bezier_points(p0, p1, p2, n):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    return [tuple(bez3(p0, p1, p2, i / (n - 1))) for i in range(n)]


def polyline_at(points, s):
    """The point and tangent at arc length s along a polyline."""
    pts = [Vector(p) for p in points]
    acc = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length
        if acc + seg >= s or b is pts[-1]:
            t = 0.0 if seg < 1e-9 else min(1.0, max(0.0, (s - acc) / seg))
            return a + (b - a) * t, (b - a).normalized()
        acc += seg
    return pts[-1], (pts[-1] - pts[-2]).normalized()


def polyline_len(points):
    pts = [Vector(p) for p in points]
    return sum((b - a).length for a, b in zip(pts, pts[1:]))


def hexhead(centre, axis, across, height):
    """A hex bolt head (6 sides), its axis along 'X', 'Y' or 'Z', centred at centre."""
    rot = {'X': (0, 90, 0), 'Y': (90, 0, 0), 'Z': (0, 0, 0)}[axis]
    return fb.cylinder(centre, across / 2, height, sides=6, rot=rot)


# ============================================================================= leaves

def blade(base, direction, up, length, outline, fold=0.10, thick=0.003, arch=0.0, droop=0.0, wave=0.0,
          end='tip', roll=0.0):
    """
    A leaf blade (or a lobe) as a thin closed shell along a bent spine. outline: [(t, half width in m)], t from 0 (the
    base) to below 1; end='tip' closes at t = 1 to a point, 'blunt' ends square at the last station (a monstera lobe
    at the margin). The section is a flat diamond: the midrib crease on top (the edges sit fold x half width above
    it, a shallow V), a keel below; flat shading draws the crease as the midrib. arch lifts the spine's middle along
    the leaf's normal, droop lowers the tip, wave ruffles the margin (alternate stations up and down).
    Faces on the upper side carry the 'top' tag (for two-tone leaves).
    """
    p0 = Vector(base)
    d = unit(direction)
    upv = Vector(up)
    n0 = upv - d * upv.dot(d)
    if n0.length < 1e-6:
        n0 = Vector((0, 0, 1)) - d * d.z
    n0.normalize()
    if roll:
        n0 = (Matrix.Rotation(math.radians(roll), 3, d) @ n0).normalized()
    p2 = p0 + d * length + Vector((0, 0, -droop))
    p1 = p0 + d * (length * 0.5) + n0 * arch
    bm = bmesh.new()
    tag = bm.faces.layers.int.new('top')
    rows = []
    N = n0
    for i, (t, hw) in enumerate(outline):
        c = bez3(p0, p1, p2, t)
        T = bez3_d(p0, p1, p2, t).normalized()
        N = (n0 - T * n0.dot(T)).normalized()
        S = T.cross(N).normalized()
        w = 0.0
        if wave and 0 < i < len(outline) - 1:
            w = wave * (1 if i % 2 else -1)
        L = c - S * hw + N * (fold * hw + w)
        R = c + S * hw + N * (fold * hw - w)
        rows.append([bm.verts.new(L), bm.verts.new(c + N * (thick / 2)), bm.verts.new(R), bm.verts.new(c - N * (thick / 2))])

    def face(vs, top):
        f = bm.faces.new(vs)
        f[tag] = top

    for a, b in zip(rows, rows[1:]):
        La, Ta, Ra, Ba = a
        Lb, Tb, Rb, Bb = b
        face([Ta, La, Lb, Tb], 1)
        face([Ta, Tb, Rb, Ra], 1)
        face([Ba, Bb, Lb, La], 0)
        face([Ba, Ra, Rb, Bb], 0)
    L0, T0, R0, B0 = rows[0]
    face([L0, B0, R0, T0], 0)
    Lz, Tz, Rz, Bz = rows[-1]
    if end == 'tip':
        tp = bm.verts.new(bez3(p0, p1, p2, 1.0))
        face([Tz, Lz, tp], 1)
        face([Tz, tp, Rz], 1)
        face([Bz, tp, Lz], 0)
        face([Bz, Rz, tp], 0)
    else:
        face([Lz, Tz, Rz, Bz], 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def kite_leaf(base, direction, up, length, width, widest=0.45, fold=0.18, thick=0.0018, droop=0.0):
    """A small narrow leaf in six triangles (the olive's): a creased top over a shallow keel."""
    p0 = Vector(base)
    d = unit(direction)
    n = Vector(up) - d * Vector(up).dot(d)
    n = n.normalized() if n.length > 1e-6 else unit(Vector((0, 0, 1)) - d * d.z)
    s = d.cross(n).normalized()
    tip = p0 + d * length + Vector((0, 0, -droop))
    c = p0 + (tip - p0) * widest
    hw = width / 2
    bm = bmesh.new()
    tag = bm.faces.layers.int.new('top')
    B, Tp = bm.verts.new(p0), bm.verts.new(tip)
    Lf = bm.verts.new(c - s * hw + n * (fold * hw))
    Rt = bm.verts.new(c + s * hw + n * (fold * hw))
    K = bm.verts.new(c - n * thick)
    for vs, top in (([B, Lf, Tp], 1), ([B, Tp, Rt], 1), ([B, K, Lf], 0), ([Lf, K, Tp], 0), ([Tp, K, Rt], 0), ([Rt, K, B], 0)):
        f = bm.faces.new(vs)
        f[tag] = top
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def heart_leaf(base, direction, up, length, width, fold=0.14, thick=0.002, droop=0.0):
    """A heart-shaped leaf in ten triangles (the pothos's): the petiole meets it at the notch between two basal lobes."""
    B0 = Vector(base)
    d = unit(direction)
    n = Vector(up) - d * Vector(up).dot(d)
    n = n.normalized() if n.length > 1e-6 else unit(Vector((0, 0, 1)) - d * d.z)
    s = d.cross(n).normalized()
    hw = width / 2
    dz = Vector((0, 0, -droop))

    def pt(a, b, lift):
        return B0 + d * (a * length) + s * (b * hw) + n * lift + dz * (max(0.0, a) ** 2)

    bm = bmesh.new()
    tag = bm.faces.layers.int.new('top')
    B = bm.verts.new(B0)
    Lb, Rb = bm.verts.new(pt(-0.07, -0.62, fold * 0.62 * hw)), bm.verts.new(pt(-0.07, 0.62, fold * 0.62 * hw))
    Lm, Rm = bm.verts.new(pt(0.30, -1.0, fold * hw)), bm.verts.new(pt(0.30, 1.0, fold * hw))
    Tp = bm.verts.new(pt(1.0, 0.0, 0.0))
    K = bm.verts.new(B0 + d * (0.35 * length) - n * thick + dz * 0.12)
    for vs, top in (([B, Lb, Lm], 1), ([B, Lm, Tp], 1), ([B, Tp, Rm], 1), ([B, Rm, Rb], 1),
                    ([K, Lb, B], 0), ([K, Lm, Lb], 0), ([K, Tp, Lm], 0), ([K, Rm, Tp], 0), ([K, Rb, Rm], 0), ([K, B, Rb], 0)):
        f = bm.faces.new(vs)
        f[tag] = top
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def leaf_frame(direction, elevation, roll=0.0):
    """A leaf heading `direction` (horizontal) tilted up by elevation degrees; returns (dir, up)."""
    h = Vector((direction[0], direction[1], 0)).normalized()
    e = math.radians(elevation)
    d = h * math.cos(e) + Vector((0, 0, 1)) * math.sin(e)
    up = -h * math.sin(e) + Vector((0, 0, 1)) * math.cos(e)
    if roll:
        up = Matrix.Rotation(math.radians(roll), 3, d) @ up
    return d, up


# ============================================================================= pots

def pot_terracotta(p, D, H, saucer=True, sides=16, z0=0.0):
    """
    A standard terracotta pot (rolled rim collar about a sixth of its height, a tapered body, the base about three
    quarters of the rim) on a saucer, hollow above its soil. Returns (soil top z, inner radius at the soil).
    """
    R = D / 2
    rb = R * 0.74
    rim_h = H * 0.17
    wall = max(0.008, D * 0.028)
    lift = 0.0
    if saucer:
        Rs = rb + 0.022
        hs = 0.022 + D * 0.02
        p.add('saucer', spin([(0.0, z0), (Rs - 0.004, z0), (Rs + 0.007, z0 + hs), (Rs, z0 + hs), (Rs - 0.008, z0 + 0.008),
                              (0.0, z0 + 0.008)], sides=sides), M('DC_Terracotta'))
        lift = 0.007                     # the pot stands in the saucer, 1 mm into its floor (no shared plane)
    zb = z0 + lift
    soil = zb + H - 0.035 - D * 0.04
    r_in_top = R - wall
    r_in_soil = (R * 0.90 - wall) + (r_in_top - (R * 0.90 - wall)) * 0.3
    p.add('pot', spin([(0.0, zb), (rb - 0.005, zb), (rb, zb + 0.006), (R * 0.90, zb + H - rim_h), (R, zb + H - rim_h + 0.006),
                       (R, zb + H - 0.006), (R - 0.006, zb + H), (r_in_top, zb + H), (r_in_soil, soil - 0.03),
                       (0.0, soil - 0.03)], sides=sides), M('DC_Terracotta'))
    p.add('soil', spin([(0.0, soil - 0.02), (r_in_soil + 0.003, soil - 0.02), (r_in_soil + 0.003, soil),
                        (0.0, soil + 0.012)], sides=sides), M('CF_Soil'))
    return soil, r_in_soil


def pot_cream(p, D, H, sides=16, z0=0.0):
    """A cream-glazed planter: a straight, gently tapered cylinder, a chamfered lip and foot. Returns (soil z, inner r)."""
    R = D / 2
    rb = R * 0.86
    wall = max(0.008, D * 0.025)
    soil = z0 + H - 0.04
    r_in = R - wall
    r_in_soil = r_in - (R - rb) * 0.04 / H
    p.add('pot', spin([(0.0, z0), (rb - 0.006, z0), (rb, z0 + 0.006), (R, z0 + H - 0.008), (R - 0.004, z0 + H),
                       (r_in + 0.002, z0 + H), (r_in, z0 + H - 0.006), (r_in_soil, soil - 0.03), (0.0, soil - 0.03)],
                      sides=sides), M('CF_PotCream'))
    p.add('soil', spin([(0.0, soil - 0.02), (r_in_soil + 0.003, soil - 0.02), (r_in_soil + 0.003, soil),
                        (0.0, soil + 0.012)], sides=sides), M('CF_Soil'))
    return soil, r_in_soil


# ============================================================================= the five big plants

def fiddle_leaf_fig():
    """
    Ficus lyrata as an indoor column, 1.80 m in a 0.40 cream pot (NC State: blades 8-15 in, up to 18 x 12; obovate to
    lyrate, a cordate base, a truncate apex, entire wavy margins, dull green above, paler beneath, alternate). 24
    leaves 0.24-0.36 long spiral up one trunk and a side branch from 0.62 m, held 20-55 degrees up, the outer ones
    drooping at the tip; the three newest are smaller and paler (CF_LeafLight).
    """
    p = Piece('CK_FiddleLeafFig')
    rnd = random.Random(11)
    soil, _ = pot_cream(p, 0.40, 0.38)
    trunk = [(0.0, 0.0, soil - 0.03), (0.006, 0.004, soil + 0.25), (0.022, -0.008, 0.86), (0.012, -0.020, 1.26),
             (-0.010, -0.012, 1.70)]
    p.add('trunk', sweep_var(trunk, [0.026, 0.023, 0.019, 0.014, 0.008], sides=6), M('CF_Bark'))
    branch = [(0.020, -0.010, 1.00), (0.075, 0.010, 1.20), (0.150, 0.045, 1.42), (0.180, 0.060, 1.52)]
    p.add('branch', sweep_var(branch, [0.012, 0.010, 0.008, 0.006], sides=5), M('CF_Bark'))
    outline = [(0.00, 0.010), (0.10, 0.17), (0.32, 0.215), (0.56, 0.27), (0.80, 0.30), (0.94, 0.20)]
    stems = [(trunk, 0.62, 1.66, 17), (branch, 0.0, 1.0, 7)]
    k = 0
    for path, z_lo, z_hi, count in stems:
        n_path = polyline_len(path)
        for i in range(count):
            f = i / max(1, count - 1)
            if path is trunk:
                z = z_lo + (z_hi - z_lo) * f
                s = None
                # the point on the trunk at height z
                pts = [Vector(q) for q in path]
                for a, b in zip(pts, pts[1:]):
                    if a.z <= z <= b.z:
                        c = a + (b - a) * ((z - a.z) / (b.z - a.z))
                        r_here = 0.026 + (0.008 - 0.026) * (z - soil) / (1.70 - soil)
                        break
            else:
                s = n_path * (0.25 + 0.75 * f)
                c, _ = polyline_at(path, s)
                r_here = 0.010 - 0.004 * f
            az = math.radians(k * 137.5 + rnd.uniform(-12, 12) + (40 if path is branch else 0))
            out = Vector((math.cos(az), math.sin(az), 0))
            if path is branch:
                out = (out + Vector((0.8, 0.3, 0))).normalized()
            top = (path is trunk and i >= count - 2) or (path is branch and i == count - 1)
            L = (0.36 - 0.10 * f) if not top else rnd.uniform(0.19, 0.23)
            elev = 22 + 30 * f + rnd.uniform(-6, 6) + (18 if top else 0)
            d, up = leaf_frame(out, elev, roll=rnd.uniform(-14, 14))
            base = Vector(c) + out * (r_here * 0.4)
            bm = blade(base, d, up, L, [(t, hw * L) for t, hw in outline], fold=0.10, thick=0.004,
                       arch=0.05 * L, droop=(0.10 + 0.10 * (1 - f)) * L, wave=0.007, end='tip')
            mat = M('CF_LeafLight') if top else M('DC_Leaf')
            p.add_tagged('leaf', bm, mat, mat)
            k += 1
    return p


def monstera():
    """
    Monstera deliciosa, about 1.0 m, in a 0.35 terracotta pot on a saucer (Penn State: indoor leaves a foot and more,
    juveniles mostly entire, adults cut and perforated, long petioles, aerial roots at the leaf bases; NParks: a
    cordate base, an apiculate tip, a winged petiole). Five adult leaves 0.30-0.40 long, each a central band along the
    midrib with four lobes a side: the lobes narrow where they leave the band (the row of holes by the midrib) and
    nearly touch at the margin (the slits), drooping off the midrib like a tent; two smaller entire juvenile leaves;
    two aerial roots down into the soil.
    """
    p = Piece('CK_Monstera')
    rnd = random.Random(23)
    soil, r_soil = pot_terracotta(p, 0.35, 0.30)
    green, light = M('DC_Leaf'), M('CF_LeafLight')
    leaves = [  # azimuth, petiole reach (out, up), blade length, blade pitch down, adult
        (10, 0.14, 0.56, 0.42, 10, True), (85, 0.12, 0.68, 0.40, 14, True), (160, 0.15, 0.48, 0.40, 8, True),
        (235, 0.12, 0.62, 0.41, 16, True), (305, 0.14, 0.44, 0.37, 6, True),
        (55, 0.08, 0.30, 0.20, 4, False), (200, 0.07, 0.27, 0.18, 2, False)]
    for az_deg, reach, rise, L, pitch, adult in leaves:
        az = math.radians(az_deg + rnd.uniform(-6, 6))
        out = Vector((math.cos(az), math.sin(az), 0))
        start = Vector((0, 0, soil - 0.03)) + out * 0.018
        tip = Vector((0, 0, soil)) + out * reach + Vector((0, 0, rise))
        ctrl = Vector((0, 0, soil)) + out * (reach * 0.15) + Vector((0, 0, rise * 1.05))
        pts = bezier_points(start, ctrl, tip, 4)
        p.add('petiole', sweep_var(pts, [0.009, 0.008, 0.007, 0.006], sides=4), green)
        d, up = leaf_frame(out, -pitch)
        if not adult:
            outline = [(0.0, 0.05 * L), (0.12, 0.30 * L), (0.40, 0.33 * L), (0.75, 0.22 * L), (0.92, 0.08 * L)]
            p.add_tagged('juvenile leaf', blade(tip - d * 0.004, d, up, L, outline, fold=-0.10, thick=0.004,
                                                 arch=0.04 * L, droop=0.12 * L, end='tip'), light, light)
            continue
        # the spine: a Bezier from the petiole's end, arching and dropping toward the apex
        n0 = up
        P0 = tip - d * 0.004
        P2 = P0 + d * L + Vector((0, 0, -0.10 * L))
        P1 = P0 + d * (0.5 * L) + n0 * (0.05 * L)
        band_hw = 0.05 * L
        band = [(0.0, 0.012), (0.25, band_hw), (0.60, band_hw * 0.9), (0.88, band_hw * 0.55)]
        p.add_tagged('midrib band', blade(P0, d, up, L, band, fold=-0.15, thick=0.006, arch=0.05 * L,
                                           droop=0.10 * L, end='tip'), green, green)
        K = 5
        for side in (-1, 1):
            ends = []
            for kk in range(K):
                f = kk / (K - 1)
                a = math.radians(118 - 88 * f)            # basal lobes point back, apical ones forward
                u = 0.04 + 0.74 * f
                # where the lobe meets the outline: an ovate-cordate leaf ((u-.40)/.60)^2 + (v/.47)^2 = 1, in L
                ou, ov = u, side * band_hw / L
                du, dv = math.cos(a), side * math.sin(a)
                r = 0.0
                for _ in range(200):
                    r += 0.005
                    uu, vv = ou + du * r, ov + dv * r
                    if ((uu - 0.40) / 0.60) ** 2 + (vv / 0.47) ** 2 > 1.0:
                        break
                ends.append((u, a, r))
            for kk, (u, a, r) in enumerate(ends):
                c = bez3(P0, P1, P2, u)
                T = bez3_d(P0, P1, P2, u).normalized()
                N = (n0 - T * n0.dot(T)).normalized()
                S = T.cross(N).normalized()
                beta = math.radians(6 + 6 * math.sin(a))            # the halves droop off the midrib (a tent)
                dl = (T * math.cos(a) + S * (side * math.sin(a))) * math.cos(beta) - N * math.sin(beta)
                attach = c + S * (side * (band_hw - 0.004))
                # widths from the neighbours' ends: slits about 8 % of the spacing at the margin; each lobe narrows to
                # half where it leaves the band, which opens the row of holes beside the midrib
                def end_pt(e):
                    uu, aa, rr = e
                    return Vector((uu + math.cos(aa) * rr, math.sin(aa) * rr))
                me = end_pt((u, a, r))
                gaps = [(me - end_pt(ends[j])).length for j in (kk - 1, kk + 1) if 0 <= j < K]
                w_margin = 0.92 * min(gaps) * L if gaps else 0.14 * L
                w_margin = min(w_margin, 0.22 * L)
                length = r * L * 0.97
                outline = [(0.0, 0.48 * w_margin / 2), (0.40, 0.86 * w_margin / 2), (1.0, w_margin / 2)]
                bm = blade(attach, dl, N, length, outline, fold=0.16, thick=0.004, arch=0.02 * length,
                           droop=0.04 * length, end='blunt')
                p.add_tagged('lobe', bm, green, green)
    # two aerial roots: from the base of two petioles down into the soil
    for az_deg in (40, 230):
        az = math.radians(az_deg)
        out = Vector((math.cos(az), math.sin(az), 0))
        a0 = Vector((0, 0, soil + 0.06)) + out * 0.02
        a2 = Vector((0, 0, soil - 0.015)) + out * (r_soil * 0.75)
        a1 = Vector((0, 0, soil + 0.07)) + out * (r_soil * 0.55)
        p.add('aerial root', sweep_var(bezier_points(a0, a1, a2, 4), [0.0045, 0.004, 0.0035, 0.003], sides=4),
              M('CF_Bark'))
    return p


def olive_tree():
    """
    Olea europaea as a potted standard, 1.70 m, in a 0.40 terracotta pot on a saucer (NC State: leaves up to 3 in,
    elliptical to lanceolate, opposite, grayish-green above and silver beneath; the bark gnarled, furrowed and
    twisted). A fluted, twisting trunk to 1.0 m, four branches and their side shoots, and an airy crown of 16 small
    faceted leaf masses with gaps between them, each fringed with ten narrow leaves 7-8 cm long: the grey-green tops
    and silver undersides are two materials, so the crown flickers silver from below and reads grey-green from above.
    The pair by the door is one mesh placed twice.
    """
    p = Piece('CK_OliveTree')
    rnd = random.Random(37)
    soil, r_soil = pot_terracotta(p, 0.40, 0.34)
    bark, top, under = M('CF_Bark'), M('CF_LeafOlive'), M('CF_LeafSilver')
    trunk = [(0.0, 0.0, soil - 0.03), (0.012, 0.006, soil + 0.12), (0.030, -0.004, 0.62), (0.010, -0.022, 0.86),
             (-0.012, -0.010, 1.04)]
    p.add('trunk', sweep_var(trunk, [0.046, 0.040, 0.034, 0.031, 0.026], sides=9, lobes=(3, 0.16), twist=32), bark)
    fork = Vector(trunk[-1]) - Vector((0, 0, 0.03))
    branches = [  # end point (relative to the fork), radius at the fork
        (Vector((0.28, 0.08, 0.40)), 0.019), (Vector((-0.24, 0.17, 0.44)), 0.018), (Vector((0.05, -0.27, 0.38)), 0.017),
        (Vector((-0.08, -0.05, 0.56)), 0.016)]
    crown_c = fork + Vector((0, 0, 0.34))
    lumps = []                                       # (centre, radius): the crown's small leaf masses
    for bi, (endp, r0) in enumerate(branches):
        mid = fork + endp * 0.45 + Vector((0, 0, 0.06)) + Vector((rnd.uniform(-.03, .03), rnd.uniform(-.03, .03), 0))
        pts = bezier_points(fork, mid, fork + endp, 5)
        p.add('branch', sweep_var(pts, [r0, r0 * 0.8, r0 * 0.6, r0 * 0.45, r0 * 0.3], sides=5), bark)
        n_len = polyline_len(pts)
        for f, rr in ((0.55, 0.075), (0.80, 0.09), (1.0, 0.105)):
            c0, t0 = polyline_at(pts, n_len * f)
            lumps.append((c0, rr * rnd.uniform(0.9, 1.1)))
        # a side shoot from 45 % along, out and up, ending in its own small mass
        s0, t0 = polyline_at(pts, n_len * 0.45)
        side = Vector((-t0.y, t0.x, 0)).normalized() * (1 if bi % 2 else -1)
        e = s0 + (side * 0.7 + t0 * 0.3 + Vector((0, 0, 0.55))).normalized() * 0.22
        sp = bezier_points(s0 - t0 * 0.004, s0 + (e - s0) * 0.5 + Vector((0, 0, 0.02)), e, 3)
        p.add('shoot', sweep_var(sp, [r0 * 0.40, r0 * 0.30, r0 * 0.22], sides=4), bark)
        lumps.append((Vector(sp[-1]), 0.07 * rnd.uniform(0.9, 1.1)))
    # The crown, the olive way: many small faceted masses (the street trees' language) with gaps between them, so it
    # stays airy, each fringed with narrow leaves whose silver undersides show from below and grey-green tops from
    # above. The masses give the crown its body from the overhead; the fringe gives it the olive's texture at 2 m.
    for ci, (c0, r) in enumerate(lumps):
        lump = fb.ellipsoid(c0, (r, r, r * 0.72), u=6, v=4, rot=(0, 0, rnd.uniform(0, 60)))
        for v in lump.verts:
            v.co = c0 + (v.co - c0) * rnd.uniform(0.85, 1.12)
        p.add_by_normal('leaf mass', lump, top, under)
        away = c0 - crown_c
        away = away.normalized() if away.length > 1e-6 else Vector((0, 0, 1))
        for j in range(10):
            # a point on the mass, all round it but more on its outer side
            a = 2 * math.pi * (j + rnd.uniform(-0.3, 0.3)) / 10
            el = math.radians(rnd.uniform(-60, 65))
            dirn = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
            dirn = (dirn + away * 0.5).normalized()
            base = c0 + dirn * (r * 0.60)            # rooted inside the mass (its facets sit at about 0.7 r)
            # the leaf lies along the surface, pointing downhill and round, its tip lifted 18 degrees off it
            downhill = Vector((0, 0, -1)) - dirn * (-dirn.z)
            downhill = downhill.normalized() if downhill.length > 1e-6 else dirn.orthogonal().normalized()
            tang = (Matrix.Rotation(math.radians(rnd.uniform(-75, 75)), 3, dirn) @ downhill).normalized()
            ld = (tang * math.cos(math.radians(18)) + dirn * math.sin(math.radians(18))).normalized()
            L = rnd.uniform(0.070, 0.085) + 0.012
            p.add_by_normal('leaf', kite_leaf(base, ld, dirn, L, L * 0.18, fold=0.12), top, under)
    p.masses = len(lumps)
    return p


def rubber_plant():
    """
    Ficus elastica, 1.20 m, three stems in a 0.32 cream pot (Wikipedia: leaves 10-35 cm long and 5-15 cm broad, broad
    shiny ovals; each new leaf grows inside a sheath at the tip that drops when it unfurls). 17 oval leaves 0.20-0.27
    long, alternate up each stem, the lower ones angled down and the upper ones up; at every tip the red sheath (the
    common dark cultivars show it red) and the newest leaf still rolled beside it.
    """
    p = Piece('CK_RubberPlant')
    rnd = random.Random(53)
    soil, _ = pot_cream(p, 0.32, 0.30)
    dark, light, sheath = M('CF_LeafDark'), M('CF_LeafLight'), M('DC_Terracotta')
    outline = [(0.0, 0.015), (0.10, 0.19), (0.35, 0.28), (0.65, 0.27), (0.86, 0.16)]
    stems = [((0.02, 0.03), (0.07, 0.10), 1.17, 8), ((-0.04, -0.01), (-0.12, -0.03), 0.98, 7), ((0.03, -0.035), (0.10, -0.14), 0.84, 6)]
    k = 0
    for (bx, by), (tx, ty), h, count in stems:
        base = Vector((bx, by, soil - 0.03))
        topp = Vector((tx, ty, h))
        mid = base + (topp - base) * 0.5 + Vector((rnd.uniform(-.02, .02), rnd.uniform(-.02, .02), 0))
        pts = bezier_points(base, mid, topp, 5)
        p.add('stem', sweep_var(pts, [0.013, 0.012, 0.010, 0.008, 0.006], sides=6), M('CF_Bark'))
        n_len = polyline_len(pts)
        for i in range(count):
            f = i / max(1, count - 1)
            c, t = polyline_at(pts, n_len * (0.34 + 0.58 * f))
            az = math.radians(k * 141 + rnd.uniform(-15, 15))
            out = Vector((math.cos(az), math.sin(az), 0))
            L = 0.29 - 0.08 * f + rnd.uniform(-0.01, 0.01)
            elev = -16 + 42 * f + rnd.uniform(-6, 6)
            d, up = leaf_frame(out, elev, roll=rnd.uniform(-10, 10))
            # the short petiole: the blade's first station starts inside the stem, the next is 3 cm out
            bm = blade(c + out * 0.003, d, up, L, [(t_, hw * L) for t_, hw in outline], fold=0.06, thick=0.005,
                       arch=0.03 * L, droop=(0.08 + 0.05 * (1 - f)) * L, end='tip')
            p.add_tagged('leaf', bm, dark, dark)
            k += 1
        # the tip: the red sheath and the newest leaf, rolled
        tdir = (Vector(pts[-1]) - Vector(pts[-2])).normalized()
        p.add('sheath', sweep_var([tuple(topp - tdir * 0.01), tuple(topp + tdir * 0.035), tuple(topp + tdir * 0.075)],
                                  [0.0065, 0.0055, 0.002], sides=5, end_tip=tuple(topp + tdir * 0.09)), sheath)
        nl_dir = (tdir + Vector((0.25, 0.1, 0))).normalized()
        p.add_tagged('new leaf', blade(topp - tdir * 0.005, nl_dir, Vector((0.3, -1.0, 0.0)), 0.12,
                                       [(0.0, 0.004), (0.3, 0.013), (0.7, 0.012)], fold=0.9, thick=0.004, end='tip'),
                     light, light)
    return p


SHELF_EDGE = -0.12        # the pothos: the shelf's front edge, 0.12 in front of the pot's centre (Blender -Y)
SHELF_THICK = 0.06        # the shelf (CK_BackBarShelf): 6 cm thick, its top at the origin's z


def pothos():
    """
    Epipremnum aureum (golden pothos) for each end of the back bar's shelf: a 0.15 terracotta pot on its saucer, a
    crown of eight leaves and four vines trailing 0.35-0.65 m over the shelf's front edge (NC State: leaves 3-6 in,
    heart-shaped, a cordate base and an acuminate tip; slender green stems). Leaves every 6-7 cm along the vines,
    turned to the room, smaller and paler toward the tips (new growth). Origin: the saucer's bottom, on the shelf top,
    0.12 m behind the shelf's front edge; nothing goes into the shelf (y > -0.12 between z -0.06 and 0).
    """
    p = Piece('CK_Pothos', floor=False)
    rnd = random.Random(71)
    soil, r_soil = pot_terracotta(p, 0.15, 0.13, sides=12)
    green, light = M('DC_Leaf'), M('CF_LeafLight')
    rim_z = 0.007 + 0.13

    def clear_of_shelf(bm):
        """Push a leaf forward (or up) so none of it is inside the shelf (with 3 mm to spare)."""
        lo_y = min(v.co.y for v in bm.verts)
        hi_y = max(v.co.y for v in bm.verts)
        inside = [v for v in bm.verts if v.co.y > SHELF_EDGE - 0.003 and -SHELF_THICK - 0.003 < v.co.z < 0.003]
        if inside and min(v.co.z for v in bm.verts) < -0.003:
            dy = max(v.co.y for v in inside) - (SHELF_EDGE - 0.003)
            bmesh.ops.translate(bm, verts=bm.verts[:], vec=(0, -dy, 0))
        inside = [v for v in bm.verts if v.co.y > SHELF_EDGE - 0.003 and -SHELF_THICK - 0.003 < v.co.z < 0.003]
        if inside:
            dz = 0.003 - min(v.co.z for v in inside)
            bmesh.ops.translate(bm, verts=bm.verts[:], vec=(0, 0, dz))
        return bm, (lo_y, hi_y)

    # the crown: eight leaves on short stems from the soil
    for i in range(8):
        az = math.radians(i * 45 + rnd.uniform(-10, 10))
        out = Vector((math.cos(az), math.sin(az), 0))
        s0 = Vector((0, 0, soil - 0.01)) + out * 0.01
        s1 = Vector((0, 0, soil)) + out * rnd.uniform(0.035, 0.06) + Vector((0, 0, rnd.uniform(0.05, 0.09)))
        p.add('crown stem', sweep_var([tuple(s0), tuple(s0 + (s1 - s0) * 0.5 + Vector((0, 0, 0.015))), tuple(s1)],
                                      [0.0028, 0.0024, 0.002], sides=3), green)
        d, up = leaf_frame(out, rnd.uniform(5, 35))
        L = rnd.uniform(0.07, 0.09)
        p.add_tagged('leaf', heart_leaf(s1 - d * 0.003, d, up, L, L * 0.72, droop=0.012), green, green)
    # the vines: (x where it leaves the rim, run along the shelf top in x, drop below the shelf top)
    vines = [(-0.035, -0.06, 0.62), (0.030, 0.07, 0.52), (0.000, -0.01, 0.36), (0.050, 0.26, 0.42)]
    for vi, (x0, run, drop) in enumerate(vines):
        r_rim = 0.075 - 0.004
        start = Vector((x0, -math.sqrt(max(0.0, r_rim ** 2 - x0 ** 2)) + 0.012, soil - 0.005))
        over = Vector((x0 * 1.3, -r_rim - 0.016, rim_z + 0.012))
        on_shelf = Vector((x0 + run * 0.6, SHELF_EDGE + 0.025, 0.0045))
        corner = Vector((x0 + run * 0.95, SHELF_EDGE - 0.004, 0.0032))
        edge = Vector((x0 + run, SHELF_EDGE - 0.011, -0.008))
        pts = [start, over, on_shelf, corner, edge]
        # down the front of the shelf and on, drifting and curling a little
        sway = rnd.uniform(-0.05, 0.05)
        steps = max(4, int(drop / 0.07))
        for j in range(1, steps + 1):
            f = j / steps
            pts.append(Vector((x0 + run + sway * math.sin(f * 2.4) + 0.02 * f * (1 if vi % 2 else -1),
                               SHELF_EDGE - 0.016 - 0.035 * f * f, -drop * f)))
        pts = [tuple(q) for q in pts]
        p.add('vine', sweep_var(pts, [0.0028] * (len(pts) - 1) + [0.0018], sides=3), green)
        # leaves along the vine, past the rim, every 6.5 cm, alternating sides, the last ones small and pale
        n_len = polyline_len(pts)
        s = 0.11
        j = 0
        while s < n_len - 0.01:
            c, t = polyline_at(pts, s)
            frac = s / n_len
            L = 0.085 - 0.04 * frac + rnd.uniform(-0.006, 0.006)
            sgn = 1 if j % 2 else -1
            if c.z > 0.002:             # on the shelf top: the leaf lies out to the side, face up
                side = Vector((-t.y, t.x, 0)).normalized() * sgn
                d, up = leaf_frame(side + t * 0.3 + Vector((0, -0.4, 0)), rnd.uniform(4, 16))
            else:                       # hanging: the blade hangs down and out, its face to the room (-Y)
                side = Vector((1, 0, 0)) * sgn
                d = (Vector((0, 0, -1)) * 0.75 + side * 0.55 + Vector((0, -0.25, 0))).normalized()
                up = Vector((0, -1, 0.15))
            bm = heart_leaf(c - d * 0.002, d, up, L, L * 0.72, droop=0.004)
            bm, _ = clear_of_shelf(bm)
            mat = light if frac > 0.78 else green
            p.add_tagged('leaf', bm, mat, mat)
            s += rnd.uniform(0.060, 0.072)
            j += 1
    return p


# ============================================================================= the room

def beam():
    """
    A chunky chestnut beam 14.80 x 0.20 x 0.32 (the research, section 6.6), hung from its top (origin at the top
    centre, the beam below z = 0), one 18 mm chamfer on each long arris. At each wall it sits in a face-mount steel
    hanger (after Simpson Strong-Tie's HU glulam hangers: a seat, two side plates, two flanges fixed to the wall;
    here 5 mm dark steel, the sides 0.24 m up a 0.32 m beam), two screw heads on each flange and a through-bolt's
    heads on the sides: how a real exposed beam is held (Sightglass 7th even made its light from old joist hangers).
    """
    p = Piece('CK_Beam', floor=False)
    Lh, W, Dp, c = 7.40, 0.20, 0.32, 0.018
    wood, steel = M('CC_Wood_Counter'), M('DC_SteelDark')
    x1 = Lh - 0.006                          # the beam's end stands 1 mm off the hanger's flange
    prof = [(-W / 2 + c, -Dp), (W / 2 - c, -Dp), (W / 2, -Dp + c), (W / 2, -c), (W / 2 - c, 0.0), (-W / 2 + c, 0.0),
            (-W / 2, -c), (-W / 2, -Dp + c)]
    p.add('beam', prism(prof, -x1, x1), wood)
    hw = W / 2
    for s in (-1, 1):
        def X(a, b):
            return (min(s * a, s * b), max(s * a, s * b))
        xa, xb = X(x1 - 0.11, Lh - 0.003)
        p.add('hanger seat', fb.zbox(xa, xb, -hw - 0.002, hw + 0.002, -Dp - 0.005, -Dp + 0.002), steel)
        sa, sb = X(x1 - 0.113, Lh - 0.002)           # the sides 3 mm longer than the seat inward, 1 mm into the flange
        for sy in (-1, 1):
            y0, y1 = sorted((sy * (hw - 0.002), sy * (hw + 0.004)))
            p.add('hanger side', fb.zbox(sa, sb, y0, y1, -Dp - 0.003, -Dp + 0.24), steel)
            fx0, fx1 = X(Lh - 0.005, Lh)
            fy0, fy1 = sorted((sy * (hw + 0.001), sy * (hw + 0.064)))
            p.add('hanger flange', fb.zbox(fx0, fx1, fy0, fy1, -Dp - 0.004, -Dp + 0.236), steel)
            for z in (-Dp + 0.05, -Dp + 0.19):
                p.add('flange screw', hexhead((s * (Lh - 0.0075), sy * (hw + 0.036), z), 'X', 0.018, 0.006), steel)
            p.add('bolt head', hexhead((s * (x1 - 0.055), sy * (hw + 0.0065), -Dp + 0.12), 'Y', 0.022, 0.007), steel)
    return p


def skylight_well():
    """
    A 1.20 m light well through the ceiling deck (the research's three over the aisle), origin on the ceiling plane,
    the well rising above it: a 16 mm white lining 0.40 tall (CF_ChalkWhite), a chestnut trim where it meets the
    ceiling (70 mm, chamfered: the beams' wood), and at the top a curb-mounted skylight after VELUX's FCM 4646
    (inside curb 46 1/2 in = 1.18 m; a neutral grey aluminium frame): a 50 mm grey frame showing round the glass.
    The ceiling deck needs a 1.20-1.30 m hole here (the trim covers its edge).
    """
    p = Piece('CK_SkylightWell', floor=False)
    half = 0.60
    p.add('trim', square_ring([(0.0, -0.018), (0.064, -0.018), (0.074, -0.008), (0.074, 0.002), (0.0, 0.002)], half - 0.004),
          M('CC_Wood_Counter'))
    p.add('lining', square_ring([(0.0, -0.003), (0.016, -0.003), (0.016, 0.40), (0.0, 0.40)], half), M('CF_ChalkWhite'))
    p.add('frame', square_ring([(0.0, 0.392), (0.08, 0.392), (0.08, 0.452), (0.012, 0.452), (0.0, 0.444)], half - 0.05),
          M('DC_Steel'))
    p.add('glass', fb.zbox(-0.556, 0.556, -0.556, 0.556, 0.428, 0.436), M('CF_Glass'))
    return p


# ============================================================================= on the counters

def tumbler(p, cx, cy, z0, label='tumbler'):
    """A Duralex Picardie 25 cl (87 x 90 mm, stackable): twelve flat facets tapering to a thick base, hollow."""
    prof = [(0.0, 0.0), (0.031, 0.0), (0.0435, 0.090), (0.0410, 0.090), (0.0285, 0.010), (0.0, 0.010)]
    bm = spin([(r, z + z0) for r, z in prof], sides=12, phase=15)
    p.add(label, hl.moved(bm, cx, cy, 0), M('CF_Glass'))


def water_station():
    """
    The self-serve water at the counter's end (the research: Sightglass Divisadero, Ritual): a chestnut tray 0.40 x
    0.25 with a 30 mm rim, a 1 litre glass carafe after the Anchor Hocking 139UR (11 in tall, a 3 in mouth, a 4 1/8 in
    base, a concave neck) about two-thirds full, and six Duralex Picardie tumblers (87 x 90 mm, stackable): four
    upright, two stacked (they nest 26 mm apart, resting on the lower one's wall).
    """
    p = Piece('CK_WaterStation')
    loops = []
    for (sx, sy, rc, z) in ((0.394, 0.244, 0.017, 0.0), (0.40, 0.25, 0.02, 0.004), (0.40, 0.25, 0.02, 0.026),
                            (0.392, 0.242, 0.016, 0.030), (0.382, 0.232, 0.011, 0.030), (0.376, 0.226, 0.008, 0.026),
                            (0.376, 0.226, 0.008, 0.010)):
        loops.append(hl.ring_rrect(0, 0, z, sx, sy, rc, per_corner=2))
    tray = hl.tube(loops, cap_bottom=True, cap_top=True)
    p.add('tray', tray, M('CC_Wood_Counter'))
    floor = 0.010 - 0.001
    # the carafe: back left; the stack: front left; four upright: a 2 x 2 block on the right
    cx, cy = -0.120, 0.050
    carafe = [(0.0, 0.0), (0.0490, 0.0), (0.0525, 0.004), (0.0525, 0.150), (0.0440, 0.196), (0.0335, 0.226),
              (0.0360, 0.262), (0.0385, 0.279), (0.0355, 0.279), (0.0330, 0.262), (0.0305, 0.226), (0.0410, 0.196),
              (0.0495, 0.150), (0.0495, 0.012), (0.0, 0.012)]
    p.add('carafe', hl.moved(spin([(r, z + floor) for r, z in carafe], sides=12), cx, cy, 0), M('CF_Glass'))
    water = [(0.0, 0.0125), (0.0485, 0.0125), (0.0485, 0.150), (0.0430, 0.176), (0.0, 0.176)]
    p.add('water', hl.moved(spin([(r, z + floor) for r, z in water], sides=12), cx, cy, 0), M('CF_Water'))
    tumbler(p, -0.120, -0.058, floor, 'tumbler (stack, lower)')
    tumbler(p, -0.120, -0.058, floor + 0.0265, 'tumbler (stack, upper)')
    for x in (0.010, 0.101):
        for y in (-0.046, 0.046):
            tumbler(p, x + 0.040, y, floor)
    return p


LABEL = {'x0': -0.042, 'x1': 0.042, 'z0': 0.026, 'z1': 0.138}     # the label's front face: 84 x 112 mm (3 : 4)


def bean_bag():
    """
    A 12 oz kraft coffee bag, filled and folded (0.115 x 0.075 x 0.22): a side-gusset bag (after Bagcraft's 1 lb
    bag, 4.25 x 2.5 x 10.5 in flat) with a flat front for the label, a crease down each gusset, the shoulders drawn
    in where the beans stop, the empty top pressed flat and folded twice to the front as a cuff, the tin tie's two
    black ends bent down at the sides, and a round one-way valve on the front shoulder. The label: a cream card
    84 x 112 mm (3 : 4) 0.6 mm proud of the front, its front face UV-mapped 0..1 (u left to right, v bottom to top,
    as seen from the front) for CF_BagLabel.
    """
    p = Piece('CK_BeanBag')
    kraft, black = M('CF_Kraft'), M('CF_Black')
    W, D = 0.112, 0.072

    def loop(z, w, d, crease, rc=0.012):
        hw_, hd = w / 2, d / 2
        rc = min(rc, hd * 0.9, hw_ * 0.9)
        k = rc * (1 - math.cos(math.radians(45)))
        return [(-hw_ + rc, -hd, z), (hw_ - rc, -hd, z), (hw_ - k, -hd + k, z), (hw_ - crease, 0.0, z),
                (hw_ - k, hd - k, z), (hw_ - rc, hd, z), (-hw_ + rc, hd, z), (-hw_ + k, hd - k, z), (-hw_ + crease, 0.0, z),
                (-hw_ + k, -hd + k, z)]
    loops = [loop(0.0, W - 0.010, D - 0.010, 0.002), loop(0.006, W, D, 0.004), loop(0.142, W, D, 0.004),
             loop(0.178, W + 0.001, 0.040, 0.008, rc=0.008), loop(0.200, W + 0.002, 0.016, 0.004, rc=0.004),
             loop(0.212, W + 0.002, 0.010, 0.002, rc=0.003)]
    p.add('bag', hl.tube(loops), kraft)
    # the cuff: the top folded twice to the front, lying on the neck (its back meets the neck 1 mm in)
    cuff = fb.rbox(-W / 2 - 0.0012, W / 2 + 0.0012, -0.0045, 0.0045, -0.021, 0.021, bevel=0.002)
    tilt = math.degrees(math.atan2(0.015, 0.034))           # the neck's front leans back 24 degrees
    cuff = fb.turn(cuff, -tilt, 'X')
    y_neck = -0.020 + (0.193 - 0.178) / 0.034 * 0.015          # the neck's front at the cuff's middle
    p.add('cuff', hl.moved(cuff, 0, y_neck - (0.0045 - 0.001) / math.cos(math.radians(tilt)), 0.193), kraft)
    for s in (-1, 1):
        x0, x1 = sorted((s * (W / 2 + 0.0006), s * (W / 2 + 0.0042)))
        p.add('tin tie end', fb.zbox(x0, x1, -0.0130, -0.0045, 0.188, 0.2145), black)
    # the valve on the front shoulder, square to its slope
    z_v = 0.157
    y_face = -D / 2 + (z_v - 0.142) / (0.178 - 0.142) * (D / 2 - 0.020)
    ang = math.degrees(math.atan2((D / 2 - 0.020), (0.178 - 0.142)))
    for r, h in ((0.011, 0.0016), (0.0045, 0.0028)):
        disc = fb.cylinder((0, 0, 0), r, h * 2, sides=10 if r > 0.006 else 6)
        disc = fb.turn(disc, 90 - ang, 'X')
        p.add('valve', hl.moved(disc, 0, y_face, z_v), black)
    p.add('label', fb.zbox(LABEL['x0'], LABEL['x1'], -D / 2 - 0.0006, -D / 2 + 0.0012, LABEL['z0'], LABEL['z1']),
          M('CF_BagLabel'))
    return p


def label_uvs(ob):
    """The label's faces: UVs 0..1 across its front (u = x left to right as seen from -Y, v = z bottom to top)."""
    me = ob.data
    uv = me.uv_layers.active or me.uv_layers.new(name='UVMap')
    names = [m.name for m in me.materials]
    if 'CF_BagLabel' not in names:
        return 0
    idx = names.index('CF_BagLabel')
    n = 0
    for poly in me.polygons:
        if poly.material_index != idx:
            continue
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            u = (co.x - LABEL['x0']) / (LABEL['x1'] - LABEL['x0'])
            v = (co.z - LABEL['z0']) / (LABEL['z1'] - LABEL['z0'])
            uv.data[li].uv = (min(1.0, max(0.0, u)), min(1.0, max(0.0, v)))
        n += 1
    return n


def service_bell():
    """
    A brass service bell, 80 mm across, 74 mm tall (the classic counter bell; 1stdibs' solid-brass courtesy bell is
    the larger 4 x 4 in kind): a turned black base, the brass dome on a hidden post, and the plunger with its knob.
    """
    p = Piece('CK_ServiceBell')
    brass, black = M('T2_Brass'), M('CF_Black')
    p.add('base', spin([(0.0, 0.0), (0.0385, 0.0), (0.0400, 0.003), (0.0400, 0.010), (0.0350, 0.017), (0.0200, 0.0205),
                        (0.0, 0.0205)], sides=16), black)
    p.add('post', fb.cylinder((0, 0, 0.0255), 0.006, 0.012, sides=8), brass)
    p.add('dome', spin([(0.0, 0.029), (0.0300, 0.0275), (0.0380, 0.0245), (0.0385, 0.0270), (0.0350, 0.0400),
                        (0.0270, 0.0510), (0.0150, 0.0572), (0.0, 0.0590)], sides=16), brass)
    p.add('plunger', fb.cylinder((0, 0, 0.0625), 0.0025, 0.0085, sides=8), brass)
    p.add('knob', spin([(0.0, 0.0655), (0.0070, 0.0660), (0.0072, 0.0700), (0.0050, 0.0738), (0.0, 0.0742)], sides=10), brass)
    return p


def grinder():
    """
    A cafe espresso grinder after the Mazzer Super Jolly V Up (240 x 410 x 595 mm, 64 mm flat burrs, a 1.1 kg hopper):
    a black base with a steel tray where the portafilter stands, the black motor body with its display and two
    buttons, the steel burr housing with the knurled grind-adjustment collar and its lever, the chute, the portafilter
    fork, and the clear hopper with beans two-thirds up and a black lid. No maker's name (brands are fictional).
    """
    p = Piece('CK_Grinder')
    black, steel, dark = M('CF_Black'), M('DC_Steel'), M('DC_SteelDark')
    by = 0.05                                       # the motor body's centre (y); the base reaches forward under the fork
    p.add('base', fb.rbox(-0.12, 0.12, -0.205, 0.205, 0.0, 0.034, bevel=0.006), black)
    p.add('tray', fb.rbox(-0.075, 0.075, -0.195, -0.075, 0.032, 0.038, bevel=0.002), steel)
    body = hl.tube([hl.ring_chamfered_rect(0, by, 0.032, 0.200, 0.250, 0.012), hl.ring_chamfered_rect(0, by + 0.012, 0.330, 0.170, 0.205, 0.012)])
    p.add('body', body, black)
    # the front of the body (the -Y face) leans: find its y at a height
    def front_y(z):
        f = (z - 0.032) / (0.330 - 0.032)
        return (by - 0.125) + f * ((by + 0.012 - 0.1025) - (by - 0.125))
    lean = math.degrees(math.atan2(0.0345, 0.298))
    zp = 0.12
    yp = front_y(zp)
    disp = fb.turn(fb.zbox(-0.042, 0.042, -0.004, 0.010, -0.022, 0.022), -lean, 'X')
    p.add('display', hl.moved(disp, 0, yp, zp), dark)
    for x in (-0.024, 0.024):
        p.add('button', fb.cylinder((x, yp - 0.006, zp - 0.044), 0.008, 0.012, sides=8, rot=(90, 0, 0)), steel)
    # the burr housing, the collar (knurled: you turn it to set the grind) and its lever
    hy = by + 0.010
    p.add('burr housing', hl.moved(hl.revolve([(0.326, 0.080), (0.395, 0.080)], sides=20), 0, hy, 0), steel)
    collar = hl.ring_wall(0.352, 0.384, 0.070, 0.092, sides=40, knurl=(40, 0.05))
    p.add('collar', hl.moved(collar, 0, hy, 0), steel)
    p.add('collar lever', fb.strut((0.105, hy - 0.035, 0.368), (0.075, hy - 0.024, 0.368), 0.0045, 0.0045, sides=6), steel)
    p.add('lever knob', fb.ellipsoid((0.110, hy - 0.037, 0.368), (0.009, 0.009, 0.009), u=8, v=5), black)
    # the chute: from the housing's front, down and forward
    # the chute, built along -Y then tipped 30 degrees nose-down: its back inside the housing, its mouth over the
    # portafilter's basket (the fork holds the basket about 0.11 in front of the body)
    chute = fb.rbox(-0.017, 0.017, -0.095, 0.0, -0.013, 0.013, bevel=0.003)
    mouth = fb.zbox(-0.011, 0.011, -0.0965, -0.089, -0.008, 0.008)
    for part, mat, name in ((chute, steel, 'chute'), (mouth, black, 'chute mouth')):
        part = fb.turn(part, 30, 'X')
        p.add(name, hl.moved(part, 0, hy - 0.070, 0.352), mat)
    # the portafilter fork: two prongs from the body's front under the chute, a bridge, the activation button
    zf = 0.215
    yf = front_y(zf)
    for x in (-0.036, 0.036):
        p.add('fork prong', fb.rbox(x - 0.007, x + 0.007, yf - 0.115, yf + 0.006, zf - 0.008, zf + 0.008, bevel=0.002), steel)
    p.add('fork bridge', fb.rbox(-0.040, 0.040, yf - 0.020, yf + 0.004, zf - 0.010, zf + 0.010, bevel=0.002), steel)
    p.add('fork button', fb.zbox(-0.012, 0.012, yf - 0.026, yf - 0.012, zf - 0.006, zf + 0.012), black)
    # the hopper (clear), its beans and its lid
    hz0 = 0.390
    hop_out = [(0.050, hz0), (0.058, hz0 + 0.030), (0.092, hz0 + 0.150), (0.096, hz0 + 0.178)]
    hop_in = [(r - 0.003, z) for r, z in hop_out]
    p.add('hopper', hl.moved(lathe_shell(hop_out, hop_in, sides=16), 0, hy, 0), M('CF_Glass'))

    def hop_inner(z):
        for (r0, z0), (r1, z1) in zip(hop_in, hop_in[1:]):
            if z0 <= z <= z1:
                return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
        return hop_in[-1][0]
    rnd = random.Random(5)
    zt = hz0 + 0.110
    beans = spin([(0.0, hz0 + 0.004), (hop_inner(hz0 + 0.004) - 0.002, hz0 + 0.004), (hop_inner(hz0 + 0.030) - 0.002, hz0 + 0.030),
                  (hop_inner(zt) - 0.002, zt), (0.045, zt + 0.009), (0.0, zt + 0.014)], sides=16)
    for v in beans.verts:                            # a heap of beans, not a cap: the mound's ring broken up
        if abs(v.co.z - (zt + 0.009)) < 1e-4:
            v.co.z += rnd.uniform(-0.004, 0.004)
            v.co.x *= rnd.uniform(0.9, 1.08)
            v.co.y *= rnd.uniform(0.9, 1.08)
    p.add('beans', hl.moved(beans, 0, hy, 0), M('CF_Beans'))
    p.add('lid', hl.moved(spin([(0.0, hz0 + 0.172), (0.099, hz0 + 0.172), (0.100, hz0 + 0.188), (0.090, hz0 + 0.198),
                                (0.0, hz0 + 0.199)], sides=20), 0, hy, 0), black)
    p.add('lid knob', hl.moved(spin([(0.0, hz0 + 0.197), (0.014, hz0 + 0.197), (0.012, hz0 + 0.205), (0.0, 0.595)],
                                    sides=10), 0, hy, 0), black)
    return p


def sidewalk_bench():
    """
    A backless slatted bench for the pavement against the front bulkhead, 2.40 x 0.44 x 0.45 (after Belson's
    backless courtyard bench: hardwood slats on a steel frame, an 8 ft length): five chestnut slats 80 x 38 mm with
    10 mm gaps, on three closed frames of 60 x 12 mm dark steel flat bar (the ends 0.10 in, one in the middle: a
    2.4 m seat needs it), a carriage-bolt head through each slat at each frame.
    """
    p = Piece('CK_SidewalkBench')
    wood, steel = M('CC_Wood_Counter'), M('DC_SteelDark')
    Lh, Dh, Hs, t = 1.20, 0.22, 0.45, 0.038
    n, w, g = 5, 0.080, 0.010
    y0 = -(n * w + (n - 1) * g) / 2
    ys = [y0 + i * (w + g) + w / 2 for i in range(n)]
    for y in ys:
        p.add('slat', fb.rbox(-Lh, Lh, y - w / 2, y + w / 2, Hs - t - 0.002, Hs, bevel=0.005), wood)
    for x in (-Lh + 0.10, 0.0, Lh - 0.10):
        fw, ft = 0.06, 0.012
        bm = bmesh.new()
        oy, oz0, oz1 = 0.20, 0.0, Hs - t
        iy, iz0, iz1 = oy - fw, oz0 + fw, oz1 - fw
        outer = [(-oy, oz0), (oy, oz0), (oy, oz1), (-oy, oz1)]
        inner = [(-iy, iz0), (iy, iz0), (iy, iz1), (-iy, iz1)]
        ring = []
        for xx in (x - ft / 2, x + ft / 2):
            ring.append(([bm.verts.new((xx, yy, zz)) for yy, zz in outer], [bm.verts.new((xx, yy, zz)) for yy, zz in inner]))
        (oa, ia), (ob, ib) = ring
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new([oa[i], oa[j], ia[j], ia[i]])
            bm.faces.new([ob[j], ob[i], ib[i], ib[j]])
            bm.faces.new([oa[j], oa[i], ob[i], ob[j]])
            bm.faces.new([ia[i], ia[j], ib[j], ib[i]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        p.add('frame', bm, steel)
        for y in ys:
            p.add('bolt head', fb.ellipsoid((x, y, Hs - 0.001), (0.009, 0.009, 0.004), u=6, v=3), steel)
    return p


# ============================================================================= the list

PIECES = [
    # (builder, group, what it is, where it goes (Unity), origin, collider)
    (fiddle_leaf_fig, 'Plants', 'fiddle-leaf fig, 1.80 m, in a 0.40 cream pot',
     'front-left corner, about (-6.85, 0, 0.75); turn freely', 'bottom centre of the pot',
     'a capsule round the pot only (r 0.21, h 0.40); the foliage none'),
    (monstera, 'Plants', 'monstera, about 1.0 m, in a 0.35 terracotta pot on a saucer',
     "on the floor in front of the counter's plain west end, about (-6.8, 0, 12.6) (clear of WaitSpot 1 and the first stool)",
     'bottom centre of the saucer', 'a capsule round the pot (r 0.20, h 0.33)'),
    (olive_tree, 'Plants', 'olive tree, 1.70 m, in a 0.40 terracotta pot on a saucer (placed twice)',
     'either side of the door inside, about (-1.6, 0, 0.5) and (+1.6, 0, 0.5); turn the second about 140 degrees so the pair is not twins',
     'bottom centre of the saucer', 'a capsule round the pot (r 0.22, h 0.37)'),
    (rubber_plant, 'Plants', 'rubber plant, 1.20 m, three stems in a 0.32 cream pot',
     'between the bistro 2-tops, about (3.0, 0, 6.2)', 'bottom centre of the pot',
     'a capsule round the pot (r 0.17, h 0.31)'),
    (pothos, 'Plants', "trailing pothos for each end of the back bar's shelf (placed twice)",
     "on CK_BackBarShelf's top (y 1.81), the pot's centre 0.12 m behind the shelf's front edge: (-3.68, 1.81, 17.82) turned 180 degrees, and (4.78, 1.81, 17.82) turned 180 degrees AND mirrored (scale x -1), so each long vine runs along the shelf toward the middle and never past its end",
     "bottom centre of the saucer (on the shelf top); the vines hang to 0.62 below it", 'none'),
    (beam, 'Room', 'chestnut ceiling beam 14.80 x 0.20 x 0.32 with steel hangers at the walls (placed five times)',
     'hung at (0, 3.20, z) for z = 2, 5, 8, 11, 14; an overhead fixture (hidden from above)',
     'top centre (the ceiling plane)', 'none'),
    (skylight_well, 'Room', 'skylight well 1.20 x 1.20, lining 0.40, chestnut trim, grey frame and glass (placed three times)',
     'at (0, 3.20, z) over the aisle; z = 3.5, 6.5, 9.5 proposed (the research\'s 10.5 runs into the z 11 beam); cut a 1.20-1.30 m hole in the ceiling deck at each; an overhead fixture',
     'centre of the ceiling plane, the well rising above it', 'none'),
    (water_station, 'Counter', 'water station: chestnut tray, 1 L carafe of water, six tumblers',
     "on the front counter's stone top at about (3.9, 1.095, 13.50), turned 180 degrees (the customers' side)",
     'bottom centre of the tray', 'a box on the tray (0.40 x 0.03 x 0.25) if anything needs one'),
    (bean_bag, 'Retail', '12 oz kraft coffee bag with the house-blend label (placed eight times)',
     "on the back bar's shelf, left of the menu: eight in a row 0.135 apart from x -3.20 to -2.255 (clear of the west pothos's long vine), at (x, 1.81, 17.85), turned 180 degrees",
     'bottom centre', 'none'),
    (service_bell, 'Counter', 'brass service bell, 80 mm',
     "on the repair bar's ledge near its east end, about (-2.85, 1.095, 13.47)", 'bottom centre',
     'a box 0.08 x 0.075 x 0.08 if it is ever clicked'),
    (grinder, 'Back bar', 'espresso grinder, 0.24 x 0.41 x 0.595, black and steel',
     'on the back bar beside the dispenser, about (4.25, 1.00, 17.50), turned 180 degrees (it faces the room)',
     'bottom centre of the base', 'a box 0.24 x 0.60 x 0.41'),
    (sidewalk_bench, 'Street', 'backless slatted bench, 2.40 m, chestnut on dark steel',
     'on the pavement against the left (west) front bulkhead, turned 180 degrees (it faces the street); clear of the planters',
     'bottom centre', 'a box 2.40 x 0.45 x 0.44 (solid at night)'),
]


def build_all():
    """Every piece, finished and audited: [{object, group, what, place, origin, collider, islands, floating, tris, size, min, max, materials}]."""
    out = []
    for builder, group, what, place, origin, collider in PIECES:
        prop = builder()
        ob = prop.finish()
        labelled = label_uvs(ob)
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'group': group, 'what': what, 'place': place, 'origin': origin, 'collider': collider,
                    'islands': n, 'floating': floating, 'turned': prop.turned, 'label_faces': labelled, **st})
    return out


REFERENCES = {
    'CK_FiddleLeafFig': ['https://plants.ces.ncsu.edu/plants/ficus-lyrata/common-name/banjo-fig'],
    'CK_Monstera': ['https://extension.psu.edu/monstera-as-a-houseplant', 'https://www.nparks.gov.sg/florafaunaweb/flora/1/4/1453'],
    'CK_OliveTree': ['https://plants.ces.ncsu.edu/plants/olea-europaea'],
    'CK_RubberPlant': ['https://en.wikipedia.org/wiki/Ficus_elastica'],
    'CK_Pothos': ['https://plants.ces.ncsu.edu/plants/epipremnum-aureum/common-name/devils-vine',
                  'https://hort.extension.wisc.edu/articles/pothos-epipremmum-aureum/'],
    'CK_Beam': ['https://www.fastenersplus.com/products/simpson-hu5-125-12-face-mount-glulam-beam-hanger'],
    'CK_SkylightWell': ['https://cdn.intelligencebank.com/us/share/M7y7/8VZ3v/Maj44/original/FCM+product+data+sheet'],
    'CK_WaterStation': ['https://www.katom.com/075-139UR.html',
                        'https://www.ggmgastro.com/en-ie-eur/12-pcs-duralex-tumbler-picardie-25-cl-clear-dugpdw25t-12'],
    'CK_BeanBag': ['https://www.imperialbrady.com/catalog/product-detail/bagcraft-coffee-bag-4-25x2-5x10-5-in-paper-poly-blend-kraft-with-tin-ties-closur?id=5568'],
    'CK_ServiceBell': ['https://www.1stdibs.com/furniture/more-furniture-collectibles/collectibles-curiosities/musical-instruments/brass-courtesy-counter-top-bell-reception-desk-bell/id-f_30589062'],
    'CK_Grinder': ['https://www.kwcommercial.com.au/media/assets/55734/fed-jollyvp-spec_pdf-eb93724f1e58.pdf'],
    'CK_SidewalkBench': ['https://Belson.com/Wood-Backless-Courtyard-Benches'],
}
