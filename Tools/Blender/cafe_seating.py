"""
The cafe's seats, v1 (10 Oct 2026): the San Francisco seat plan's new pieces and the furniture library's chairs, stools
and tables rebuilt to the hero standard, built with Python in Blender 5.2 (bpy module or headless).

Mansoor's standard: "i want detail for every item in the cafe itself"; "every part has to mean something"; "inspect these
items in real life and see how they look and incorporate that with these assets". So every piece starts from a measured
real reference (cited below and in report.md), then the house style simplifies it (flat-shaded hard goods, soft
upholstery smooth inside its seams, one-segment bevels, metallic 0, chunky, detail in the texture), then every part that
does none of the four purpose jobs is cut. Each part's label says which job it does:
  (1) a person works it  (2) it tells how the thing works  (3) it makes the silhouette  (4) this world's texture.

  CK_BanquetteBay     one 2.10 m bay of the built-in banquette along the west windows (three end to end). Seat front at
                      0.45, 0.55 deep, the back's top at 0.765, under the 0.78 sill. Espresso base on a recessed dark kick,
                      a tight oat seat with welted front edges, a channel-tufted back (22 channels of 9.5 cm as real
                      geometry, seams sunk, a welted top roll). Wall piece. Refs: arcedior banquette guide (seat 430-460,
                      depth 460-560, back 5-10 deg), 4 in channels (ispydiy), "tight seat & channel tufted back" (1stdibs),
                      Sightglass Divisadero's channel-tufted banquette backs (homeadore).
  CK_BanquetteEnd     the finished end of the run: an espresso end panel with the kick notched through. Used twice.
  CK_CommunalTable    3.60 x 0.90 x 0.75, the Workshop family: four chunky chestnut planks with breadboard ends, three
                      trestles (a 3.6 m top sags on two: woodweb), one keyed through-tenon beam. Built long along Blender Y
                      (Unity Z), so it drops into the plan with no turn.
  CK_WindowCounter    the east window bar: a 4.00 x 0.45 chestnut top at 1.05 on four warm-steel knee-wall brackets fixed
                      to the wall under the sill (Federal Brace's kneewall/countertop brackets: 1/4 in plate, 2 in flanges).
                      Wall piece.
  CK_Chair_Cafe       the Workshop ladder-back cafe chair (ref IKEA STEFAN: 41.9 x 48.9 x 89.9, seat 45.1).
  CK_Chair_Spindle    the Bistro stick-back Windsor (refs HAY J77 by Folke Palsson: 43 x 43 x 79, seat 44.5; Windsor
                      anatomy: legs wedged through the seat, H-stretcher, 7 sticks, a steam-bent crest).
  CK_Stool_Bar        the bar stool with a backrest (ref IKEA INGOLF bar stool: 40 x 46 x 102, seat 74) and a steel foot
                      ring 44 cm under the seat (by-crea: a footrest 40-45 cm below the seat).
  CK_Stool_Low        the low three-legged stool, legs wedged through the seat.
  CK_Table_BistroRound  the pedestal 2-top on a cast-iron dome base (ref Peter Meier 3017-29: 17 in dome, 3 in column,
                      9 in spider, 28 1/4 in high).
  CK_Table_Square2    the Workshop square 2-top: planks and breadboard ends, inside-tapered legs, pegged aprons, a keyed
                      H-stretcher.

What never changes (the Unity seats, sit points and colliders depend on it): every rebuilt library piece keeps its old
overall size, seat height, footprint (where the feet stand) and origin (OLD below, measured from the library's FBX).
1 unit = 1 m, Z up; origin at the bottom centre (a wall piece: the bottom centre of its back, the back at y = 0); the
front faces -Y (+Z in Unity); Unity (x, y, z) = Blender (-x, z, -y). No UVs: Unity puts the wood grain, the fabric and the
steel on with world-space UVs.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = '/home/claude/cafe/inputs/Tools/Blender'
for _p in (TOOLS, os.path.join(HERE, '..', 'inputs', 'Tools', 'Blender')):
    if os.path.isdir(_p) and _p not in sys.path:
        sys.path.insert(0, _p)
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

# (sRGB hex, Unity smoothness, already a project material): scene-facts.md's palette. Metallic 0 throughout.
CK_MATERIALS = {
    'CC_Wood_Counter':  ('#8F5B32', 0.71, True),    # chestnut: tops, seats, slats
    'CC_Wood_Espresso': ('#4A3422', 0.38, True),    # frames, legs, bases
    'CC_Dark_Kick':     ('#2A1E16', 0.30, True),    # toe kick, pegs, keys, glides
    'DC_Fabric':        ('#A8957C', 0.12, True),    # oat upholstery
    'DC_Steel':         ('#93908A', 0.50, True),    # warm steel: the window counter's brackets
    'DC_SteelDark':     ('#4E4B47', 0.50, True),    # cast iron / blackened steel: the bistro base, the foot ring, screws
}


def M(name):
    hexstr, smooth, _ = CK_MATERIALS[name]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


# The library pieces as they were (measured from Assets/Art/Models/*.fbx with measure_library: bounds in Blender axes,
# the seat's top, the seat's centre in plan). The rebuilds must keep these.
OLD = {
    'Chair_Cafe':        {'min': (-0.2233, -0.2610, 0.0), 'max': (0.2233, 0.2610, 0.8954), 'seat': 0.4511, 'seat_xy': (0.0, -0.0356), 'tris': 788},
    'Chair_Spindle':     {'min': (-0.2120, -0.2423, 0.0), 'max': (0.2120, 0.2423, 0.8630), 'seat': 0.4524, 'seat_xy': (0.0, -0.0303), 'tris': 332},
    'Stool_Bar':         {'min': (-0.1926, -0.2010, 0.0), 'max': (0.1926, 0.2010, 1.0392), 'seat': 0.7517, 'seat_xy': (0.0, -0.0065), 'tris': 272},
    'Stool_Low':         {'min': (-0.1704, -0.1630, 0.0), 'max': (0.1704, 0.1630, 0.4522), 'seat': 0.4522, 'seat_xy': (-0.0115, 0.0), 'tris': 172},
    'Table_BistroRound': {'min': (-0.3600, -0.3600, 0.0), 'max': (0.3600, 0.3600, 0.7500), 'seat': None, 'top': 0.75, 'tris': 372},
    'Table_Square2':     {'min': (-0.3600, -0.3600, 0.0), 'max': (0.3600, 0.3600, 0.7500), 'seat': None, 'top': 0.75, 'tris': 616},
}


class Piece(hl.Faceted):
    """
    The house way (hero_lib.Faceted: flat unless a part asks for smooth, one-segment bevels), standing on the floor.
    One addition: an open surface (the channel back) says which way it must face (face_dir), because recalculating
    normals on an open sheet can turn it to face the wall (bench_tools' cloth met the same thing).
    """

    def __init__(self, name):
        super().__init__(name)
        self.floor = True
        self.facing = []        # (faces, direction) for open parts

    def add(self, part_name, src_bm, mat, bevel=0.0, segments=1, smooth=False, face_dir=None):
        faces = super().add(part_name, src_bm, mat, bevel=bevel, segments=segments, smooth=smooth)
        if face_dir is not None:
            self.facing.append((faces, Vector(face_dir)))
        return faces

    def finish(self, collection=None):
        # Faceted's edge rule (smooth only inside smooth parts, under 42 degrees), then Prop.finish's steps, then the
        # open parts turned back to face where they must.
        limit = math.radians(hl.SMOOTH_ANGLE)
        for e in self.bm.edges:
            if not e.is_manifold:
                e.smooth = True
                continue
            fa, fb_ = e.link_faces
            if fa in self.smooth_faces and fb_ in self.smooth_faces:
                e.smooth = e.calc_face_angle(0.0) <= limit
            else:
                e.smooth = False
        bm = self.bm
        self.floored = 0
        if self.floor:
            for v in bm.verts:
                if -0.02 < v.co.z < 0.0:
                    v.co.z = 0.0
                    self.floored += 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.turned = fb.orient_shells(bm)
        bm.normal_update()
        for faces, d in self.facing:
            live = [f for f in faces if f.is_valid]
            if live and sum(f.normal.dot(d) * f.calc_area() for f in live) < 0:
                bmesh.ops.reverse_faces(bm, faces=live)
        fb.box_uvs(bm)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        ob['fixit_shells_turned'] = self.turned
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


# ============================================================================= helpers (each returns a new bmesh)


def lerp3(a, b, t):
    return tuple(a[k] + (b[k] - a[k]) * t for k in range(3))


def along(p_bot, p_top, z):
    """Where a member running from p_bot to p_top is at height z (fixit_blender.at_z, as a tuple)."""
    return tuple(fb.at_z(p_top, p_bot, z))


def crect(cx, cy, z, sx, sy, c):
    """A horizontal section: sx by sy centred on (cx, cy), the corners cut by c (the house's one chamfer)."""
    return hl.ring_chamfered_rect(cx, cy, z, sx, sy, c)


def square_member(sections, c=0.003):
    """A square-section member lofted through horizontal sections [(cx, cy, z, sx, sy)], bottom to top: a leg cut flat at
    the floor and under the seat, the way a real splayed leg is cut (it never dips under the floor)."""
    return hl.tube([crect(cx, cy, z, sx, sy, c) for cx, cy, z, sx, sy in sections])


def round_member(sections, sides=8, phase=0.0):
    """A round (turned) member through horizontal sections [(cx, cy, z, r)], bottom to top."""
    return hl.tube([hl.ring_round(cx, cy, z, r, r, sides=sides, phase=phase) for cx, cy, z, r in sections])


def turned(p0, p1, prof, sides=8):
    """A turned member along any axis from p0 to p1: prof [(t, r)] from t = 0 at p0 to 1 at p1 (a stretcher's swell)."""
    a, b = Vector(p0), Vector(p1)
    d = (b - a)
    L = d.length
    d.normalize()
    u = Vector((0, 0, 1)).cross(d)
    if u.length < 1e-6:
        u = Vector((1, 0, 0)).cross(d)
    u.normalize()
    w = d.cross(u).normalized()
    loops = []
    for t, r in prof:
        c = a + d * (L * t)
        loops.append([tuple(c + (u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * r)
                      for k in range(sides)])
    return hl.tube(loops)


def peg(centre, axis, r=0.0042, length=0.006, sides=6):
    """A dowel's head showing on a face (the drawbored joint): a short cylinder along axis 'x', 'y' or 'z', centred on
    the face so it stands 3 mm proud and goes 3 mm in."""
    rot = {'x': (0, 90, 0), 'y': (90, 0, 0), 'z': (0, 0, 0)}[axis]
    return fb.cylinder(centre, r, length, sides=sides, rot=rot)


def glide(x, y, r, h=0.005, sides=8):
    """A nail-on glide under a foot (what a real cafe chair stands on)."""
    return fb.cylinder((x, y, h / 2), r, h, sides=sides)


def prism(profile, axis, a0, a1, tri_caps=False):
    """A solid of constant profile: profile [(u, v)] is (y, z) for axis 'x' or (x, z) for axis 'y'; extruded a0..a1."""
    bm = bmesh.new()

    def P(a, u, v):
        return (a, u, v) if axis == 'x' else (u, a, v)
    A = [bm.verts.new(P(a0, u, v)) for u, v in profile]
    B = [bm.verts.new(P(a1, u, v)) for u, v in profile]
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([A[i], B[i], B[j], A[j]])
    if tri_caps:
        # A concave profile: the caps as triangles from a 2D ear clip of the profile itself. bmesh's triangulate put
        # triangles outside a concave outline (both n-gon methods, here, headless): the window brackets' L came out
        # as solid black triangles in the review renders (10 Oct).
        for i, j, k in ear_clip(profile):
            bm.faces.new([A[k], A[j], A[i]])
            bm.faces.new([B[i], B[j], B[k]])
    else:
        bm.faces.new(list(reversed(A)))
        bm.faces.new(B)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def ear_clip(poly):
    """Triangles (index triples) of a simple 2D polygon, convex or not (ear clipping)."""
    pts = list(poly)
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    idx = list(range(len(pts))) if area > 0 else list(reversed(range(len(pts))))     # work anticlockwise

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        return cross(a, b, p) >= -1e-12 and cross(b, c, p) >= -1e-12 and cross(c, a, p) >= -1e-12
    tris = []
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = pts[i0], pts[i1], pts[i2]
            if cross(a, b, c) <= 1e-12:
                continue                                    # a reflex corner: not an ear
            if any(inside(pts[m], a, b, c) for m in idx if m not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
    tris.append(tuple(idx))
    return tris


def bent_slat(hw, y_at, sag, thick, z0, z1, n=8, x_at=None):
    """
    A slat bent in plan (a ladder-back's slat, a crest rail, a backrest): its centre line runs through the posts' centre
    line y_at(z) at x = +-hw and bows back by sag at x = 0, thickness thick across, from z0 to z1; it leans with the
    posts because y_at is taken at each height. Built from quads only (no concave n-gons).
    """
    R = (hw * hw + sag * sag) / (2.0 * sag) if sag > 1e-6 else None
    bm = bmesh.new()

    def row(z):
        F, B = [], []
        for i in range(n + 1):
            x = -hw + 2.0 * hw * i / n
            dy = (math.sqrt(R * R - x * x) - (R - sag)) if R else 0.0
            yc = y_at(z) + dy
            xs = x * (x_at(z) / hw) if x_at else x
            F.append(bm.verts.new((xs, yc - thick / 2, z)))
            B.append(bm.verts.new((xs, yc + thick / 2, z)))
        return F, B
    F0, B0 = row(z0)
    F1, B1 = row(z1)
    for i in range(n):
        bm.faces.new([F0[i], F0[i + 1], F1[i + 1], F1[i]])       # front
        bm.faces.new([B0[i + 1], B0[i], B1[i], B1[i + 1]])       # back
        bm.faces.new([F1[i], F1[i + 1], B1[i + 1], B1[i]])       # top
        bm.faces.new([F0[i + 1], F0[i], B0[i], B0[i + 1]])       # bottom
    bm.faces.new([F0[0], F1[0], B1[0], B0[0]])                   # ends
    bm.faces.new([F0[n], B0[n], B1[n], F1[n]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def seat_board(loop, z_bot, rim, dish, centre, cb=0.004, ct=0.006, insets=(0.05, 0.11), front_drop=0.0, half_d=0.2):
    """
    A seat as a real one is shaped: loop(inset, z) gives the outline shrunk by inset (the same point count for any
    inset). Bottom (chamfered by cb), straight sides, the top edge eased by ct and rolled over at the front by front_drop
    (the "waterfall" edge that keeps it off the backs of the knees), and the top dished by dish(x, y) metres where the
    sitting bones go. The rim stays at `rim`, so the seat height is the rim's. Added smooth: a dished seat flat-shaded
    shows its triangles in the highlight (the art guide's worktop exception: tops smooth, everything else flat); the
    42 degree rule keeps its sides and chamfers hard.
    """
    cx, cy = centre

    def wf(y):
        t = (cy - y) / half_d
        return front_drop * hl.smoothstep((t - 0.2) / 0.8)
    loops = [loop(cb, z_bot), loop(0.0, z_bot + cb)]
    loops.append([(x, y, rim - ct - wf(y)) for x, y, _ in loop(0.0, 0.0)])
    loops.append([(x, y, rim - 0.35 * wf(y) - dish(x, y)) for x, y, _ in loop(ct, 0.0)])
    for d in insets:
        loops.append([(x, y, rim - dish(x, y)) for x, y, _ in loop(d, 0.0)])
    return hl.tube(loops, cap_bottom=True, tip=(cx, cy, rim - dish(cx, cy)))


def surface_z(bm_src, x, y, r, z_from=2.0):
    """The highest point of a built surface over a small disc at (x, y) (rays straight down): where an end-grain disc
    must sit so it shows a hair proud everywhere and is never half buried."""
    tree = BVHTree.FromBMesh(bm_src)
    best = None
    for k in range(9):
        if k == 0:
            px, py = x, y
        else:
            a = 2 * math.pi * (k - 1) / 8
            px, py = x + r * math.cos(a), y + r * math.sin(a)
        hit = tree.ray_cast(Vector((px, py, z_from)), Vector((0, 0, -1)))
        if hit[0] is not None:
            best = hit[0].z if best is None else max(best, hit[0].z)
    return best


def copy_bm(bm):
    out = bmesh.new()
    fb.merge(out, bm.copy())
    return out


def wedged_tenon(p, seat_bm, x, y, r, centre, stretch=1.3):
    """
    A leg's wedged through-tenon on the seat's top: the leg's end grain (espresso, the leg's own colour) a hair proud of
    the seat, an oval because the splayed leg comes through at an angle (long along the splay, away from the seat's
    centre), and a hairline wedge across it. Job 2: it says how the leg holds. The first version (a round dark disc
    with a 2.2 mm bar) read as a slotted screw head in the detail renders (10 Oct): a part telling the wrong story.
    """
    a = math.atan2(y - centre[1], x - centre[0])
    z = surface_z(seat_bm, x, y, r * stretch)
    top = z + 0.0012
    disc = fb.cylinder((x, y, top - 0.003), r, 0.006, sides=10, rot=(0, 0, math.degrees(a)), scale_xy=(stretch, 1.0))
    p.add('tenon end grain (2: the leg comes through the seat)', disc, M('CC_Wood_Espresso'))
    wedge = fb.zbox(-0.0006, 0.0006, -r * 0.9, r * 0.9, top - 0.002, top + 0.0005)
    p.add('tenon wedge (2)', fb.move(fb.turn(wedge, math.degrees(a), 'Z'), (x, y, 0)), M('CC_Dark_Kick'))


# ============================================================================= the library pieces, rebuilt


def chair_cafe():
    """
    CK_Chair_Cafe: the Workshop ladder-back. Reference: IKEA STEFAN (dimensions.com: 41.9 W x 48.9 D x 89.9 H, seat 45.1),
    the plain pine ladder-back that IS the cafe chair, and the old library chair (same envelope). What the old one lacked
    and real ones have: a seat board shaped where you sit (dished 8 mm, the front edge rolled over), carried on aprons
    (the frame you see under any real seat), back legs that are ONE piece from the floor to the top, kinked at the seat
    and raked back 12 degrees, slats bent to the back's curve, stretchers staggered so their joints miss each other,
    pegged joints (the Workshop family), glides. Keeps the old box exactly: 0.4466 x 0.5219 x 0.8954, seat 0.451.
    """
    p = Piece('CK_Chair_Cafe')
    chestnut, wood, dark = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Dark_Kick')
    SC = OLD['Chair_Cafe']['seat_xy']
    rim = OLD['Chair_Cafe']['seat']

    def loop(d, z):
        return hl.ring_rrect(SC[0], SC[1], z, 0.400 - 2 * d, 0.400 - 2 * d, max(0.004, 0.030 - d), per_corner=2, per_side=1)

    def dish(x, y):
        return 0.008 * math.exp(-((x / 0.13) ** 2 + ((y - 0.015) / 0.12) ** 2))
    p.add('seat board, dished, front rolled (1, 3)', seat_board(loop, 0.417, rim, dish, SC, front_drop=0.012, half_d=0.2),
          chestnut, smooth=True)

    # legs: front legs straight, tapered 26 -> 36 mm, splayed to the old feet; back legs one bent piece floor -> top
    FF = (0.2103, -0.2480, 0.004)                 # front foot centre (+x side): its outer faces on the old box
    FT = (0.1700, -0.2030, 0.420)                 # front leg top, 3 mm into the seat
    BF = (0.2103, 0.1768, 0.004)                  # back foot
    BS = (0.1710, 0.1370, 0.430)                  # back leg at the seat (the kink)
    BT = (0.1800, 0.2320, 0.8954)                 # back post top (the old top height)
    for s in (-1, 1):
        p.add('front leg (3)', square_member([(s * FF[0], FF[1], FF[2], 0.026, 0.026),
                                              (s * FT[0], FT[1], FT[2], 0.036, 0.036)]), wood)
        p.add('back leg, one piece to the top, raked (3)', square_member([
            (s * BF[0], BF[1], BF[2], 0.026, 0.026), (s * BS[0], BS[1], BS[2], 0.034, 0.030),
            (s * BT[0], BT[1], BT[2] - 0.006, 0.030, 0.026), (s * BT[0], BT[1], BT[2], 0.022, 0.018)]), wood)
        for x, y in ((FF[0], FF[1]), (BF[0], BF[1])):
            p.add('glide (2: what it stands on)', glide(s * x, y, 0.0115), dark)

    # aprons under the seat, set 6 mm in from the legs' faces, their tops 2 mm into the seat board (2, 3)
    fl = along(FF, FT, 0.388)                      # the front leg at apron height
    bl = along(BF, BS, 0.388)
    fh = (0.026 + 0.010 * (0.384 / 0.416)) / 2    # front leg half-size there
    bh = (0.026 + 0.008 * (0.384 / 0.426)) / 2
    za0, za1 = 0.357, 0.419
    p.add('front apron (2, 3)', fb.zbox(-fl[0], fl[0], fl[1] - fh + 0.006, fl[1] - fh + 0.028, za0, za1), wood, bevel=0.003)
    p.add('back apron (2)', fb.zbox(-bl[0], bl[0], bl[1] + 0.0148 - 0.028, bl[1] + 0.0148 - 0.006, za0, za1), wood, bevel=0.003)
    for s in (-1, 1):
        x_out = s * (fl[0] + fh - 0.006)
        # the side aprons 4 mm shallower and 1.5 mm higher than the front and back ones: no two faces in one plane
        p.add('side apron (2, 3)', fb.zbox(min(x_out, x_out - s * 0.022), max(x_out, x_out - s * 0.022), fl[1], bl[1],
                                           za0 + 0.004, za1 + 0.0015), wood, bevel=0.003)
    # stretchers, staggered: the sides low, front and back higher, so the mortises never meet in a leg (2)
    for s in (-1, 1):
        a, b = along(FF, FT, 0.163), along(BF, BS, 0.163)
        p.add('side stretcher (2)', fb.zbox(s * a[0] - 0.010, s * a[0] + 0.010, a[1], b[1], 0.150, 0.176), wood, bevel=0.003)
    a, b = along(FF, FT, 0.218), along(BF, BS, 0.218)
    p.add('front stretcher (2)', fb.zbox(-a[0], a[0], a[1] - 0.010, a[1] + 0.010, 0.205, 0.231), wood, bevel=0.003)
    p.add('back stretcher (2)', fb.zbox(-b[0], b[0], b[1] - 0.010, b[1] + 0.010, 0.205, 0.231), wood, bevel=0.003)

    # three slats bent to the back, tenoned into the posts; the top one deepest and widest (3)
    def post_y(z):
        return BS[1] + (z - BS[2]) * (BT[1] - BS[1]) / (BT[2] - BS[2])

    def post_x(z):
        return BS[0] + (z - BS[2]) * (BT[0] - BS[0]) / (BT[2] - BS[2])
    top_z1 = 0.888
    top_sag = OLD['Chair_Cafe']['max'][1] - 0.009 - post_y(top_z1)        # its back reaches the old box's back
    for z0, z1, t, sag in ((0.598, 0.652, 0.016, 0.014), (0.708, 0.762, 0.016, 0.017), (0.816, top_z1, 0.018, top_sag)):
        p.add('back slat, bent (3)', bent_slat(post_x((z0 + z1) / 2), post_y, sag, t, z0, z1, n=8, x_at=post_x), chestnut,
              bevel=0.003)
    # the pegs through the apron tenons, one on each face where a tenon goes in (4: the Workshop family; 2)
    for s in (-1, 1):
        p.add('peg (4)', peg((s * fl[0], fl[1] - fh, 0.388), 'y'), dark)
        p.add('peg (4)', peg((s * (fl[0] + fh), fl[1], 0.388), 'x'), dark)
        p.add('peg (4)', peg((s * bl[0], bl[1] + 0.0148, 0.388), 'y'), dark)
        p.add('peg (4)', peg((s * (bl[0] + bh), bl[1], 0.388), 'x'), dark)
    return p


def chair_spindle():
    """
    CK_Chair_Spindle: the Bistro family's stick-back Windsor. References: HAY J77 (Folke Palsson for FDB, 1940s; 43 x 43 x
    79, seat 44.5, slightly conical legs) and the Windsor anatomy (dutchcrafters: legs set at an angle and wedged through
    the seat, an H-stretcher, 7 or 9 spindles, a steam-bent top). The old chair had a flat disc for a seat, four sticks
    and a straight rail. Now: a saddle seat (a well where you sit, a pommel at the front between two thigh grooves, the
    front rolled), the leg tenons wedged through the seat's top, a turned H-stretcher, two posts and five spindles fanning
    up to a crest bent 34 mm. Keeps 0.424 x 0.4847 x 0.863, seat 0.452, the sticks on the old back's lines.
    """
    p = Piece('CK_Chair_Spindle')
    chestnut, wood = M('CC_Wood_Counter'), M('CC_Wood_Espresso')
    SC = OLD['Chair_Spindle']['seat_xy']
    rim = OLD['Chair_Spindle']['seat']
    R = 0.212

    def loop(d, z):
        return hl.ring_round(SC[0], SC[1], z, R - d, R - d, sides=20)

    def dish(x, y):
        well = 0.012 * math.exp(-((x / 0.10) ** 2 + ((y - 0.015) / 0.09) ** 2))
        thighs = 0.006 * sum(math.exp(-(((x - s * 0.085) / 0.05) ** 2 + ((y + 0.13) / 0.08) ** 2)) for s in (-1, 1))
        return well + thighs
    seat = seat_board(loop, 0.4104, rim, dish, SC, cb=0.005, ct=0.007, insets=(0.05, 0.10, 0.15), front_drop=0.012,
                      half_d=R)
    seat_copy = copy_bm(seat)
    p.add('saddle seat (1, 3)', seat, chestnut, smooth=True)

    # legs: turned, slightly conical (J77), from the old feet to the old tops under the seat
    for s in (-1, 1):
        for foot, top in (((0.1900, -0.2223), (0.1422, -0.1728)), ((0.1900, 0.1617), (0.1422, 0.1121))):
            fx, fy = s * foot[0], foot[1]
            tx, ty = s * top[0], top[1]
            mx, my = fx + (tx - fx) * 0.62, fy + (ty - fy) * 0.62
            p.add('leg, turned (3)', round_member([(fx, fy, 0.004, 0.0125), (mx, my, 0.26, 0.0150), (tx, ty, 0.413, 0.0158)]),
                  wood)
            p.add('glide (2)', glide(fx, fy, 0.0115), M('CC_Dark_Kick'))
            # the tenon carries on through the seat: its end on the top, wedged
            k = (0.452 - 0.413) / (0.413 - 0.004)
            wedged_tenon(p, seat_copy, tx + (tx - fx) * k, ty + (ty - fy) * k, 0.0100, SC, stretch=1.2)
    # the H-stretcher: two side stretchers with a swell, a cross stretcher between their middles (2)
    zs = 0.158
    mids = []
    for s in (-1, 1):
        a = along((s * 0.19, -0.2223, 0.004), (s * 0.1422, -0.1728, 0.413), zs)
        b = along((s * 0.19, 0.1617, 0.004), (s * 0.1422, 0.1121, 0.413), zs)
        p.add('side stretcher, turned (2)', turned(a, b, [(0, 0.0095), (0.25, 0.0115), (0.5, 0.0128), (0.75, 0.0115), (1, 0.0095)]),
              wood)
        mids.append(lerp3(a, b, 0.5))
    p.add('cross stretcher, turned (2)', turned(mids[0], mids[1], [(0, 0.0090), (0.3, 0.0108), (0.5, 0.0118), (0.7, 0.0108), (1, 0.0090)]),
          wood)

    # the back: posts and spindles socketed into the seat, on the old back's lines (the seated bodies' clearance, Sit 5)
    def post_y(z):
        return 0.1234 + (z - 0.4126) * (0.1960 - 0.1234) / (0.8214 - 0.4126)

    def post_x(z):
        return 0.1509 + (z - 0.4126) * (0.1582 - 0.1509) / (0.8214 - 0.4126)
    z_lo, z_hi = 0.430, 0.812
    for s in (-1, 1):
        p.add('back post (3)', round_member([(s * post_x(z_lo), post_y(z_lo), z_lo, 0.0150),
                                             (s * post_x(z_hi), post_y(z_hi), z_hi, 0.0118)]), wood)
    # the crest: bent so its middle reaches the old rail's back; it runs 3 cm past the posts (the J77's ears)
    cz0, cz1, ct = 0.798, OLD['Chair_Spindle']['max'][2], 0.022
    hw = 0.1875
    sag = OLD['Chair_Spindle']['max'][1] - ct / 2 - post_y(cz1)
    Rc = (hw * hw + sag * sag) / (2 * sag)

    def bow(x):
        return math.sqrt(Rc * Rc - x * x) - (Rc - sag)
    p.add('crest rail, bent (3)', bent_slat(hw, post_y, sag, ct, cz0, cz1, n=10), chestnut, bevel=0.004)
    # five spindles, fanning a little, their tops on the crest's bow and their feet on a gentler arc in the seat (3)
    for xb in (-0.098, -0.049, 0.0, 0.049, 0.098):
        xt = xb * 1.06
        p.add('spindle (3)', round_member([(xb, post_y(0.440) + 0.5 * bow(xb), 0.440, 0.0078),
                                           (xt, post_y(0.806) + bow(xt), 0.806, 0.0060)], sides=6), wood)
    return p


def stool_bar():
    """
    CK_Stool_Bar: the bar stool with a backrest (the repair bar and the window counter). Reference: IKEA INGOLF bar stool
    with backrest (dimensions.com: 40 W x 46 D x 102 H, seat 74, seat 40 x 40) - the old stool's twin. The old one's
    footrest rails sat 19-22 cm off the floor, 53-56 cm under the seat: feet dangle (by-crea: the footrest goes 40-45 cm
    under the seat, 31-36 cm off the floor for a 76 cm stool). Now a steel foot ring at 0.31, fixed to the inside of the
    legs; a dished seat with a rolled front; turned legs; back legs one piece up to a backrest bent round the back.
    Keeps 0.3851 x 0.4019 x 1.0392, seat 0.752.
    """
    p = Piece('CK_Stool_Bar')
    chestnut, wood, iron = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('DC_SteelDark')
    SC = OLD['Stool_Bar']['seat_xy']
    rim = OLD['Stool_Bar']['seat']

    def loop(d, z):
        return hl.ring_rrect(SC[0], SC[1], z, 0.344 - 2 * d, 0.344 - 2 * d, max(0.004, 0.05 - d), per_corner=3, per_side=1)

    def dish(x, y):
        return 0.007 * math.exp(-((x / 0.11) ** 2 + ((y - 0.01) / 0.11) ** 2))
    p.add('seat, dished, front rolled (1, 3)', seat_board(loop, 0.7097, rim, dish, SC, front_drop=0.010, half_d=0.172,
                                                           insets=(0.05, 0.10)), chestnut, smooth=True)
    FF, FT = (0.180, -0.1885, 0.004), (0.115, -0.1215, 0.712)
    BF, BS, BT = (0.180, 0.1755, 0.004), (0.1185, 0.1125, 0.730), (0.1304, 0.1580, 1.030)
    for s in (-1, 1):
        p.add('front leg, turned (3)', round_member([(s * FF[0], FF[1], FF[2], 0.0125), (s * FT[0], FT[1], FT[2], 0.0185)]), wood)
        p.add('back leg, one piece to the backrest (3)', round_member([(s * BF[0], BF[1], BF[2], 0.0125),
                                                                       (s * BS[0], BS[1], BS[2], 0.0172),
                                                                       (s * BT[0], BT[1], BT[2], 0.0135)]), wood)
        for x, y in ((FF[0], FF[1]), (BF[0], BF[1])):
            p.add('glide (2)', glide(s * x, y, 0.0115), M('CC_Dark_Kick'))
    # the foot ring: 16 mm steel, 0.44 m under the seat, hugging the legs' inner corners (1: the feet rest on it)
    zr = 0.310
    a, b = along(FF, FT, zr), along(BF, BS, zr)
    cy = (a[1] + b[1]) / 2
    hx, hy = a[0] - 0.006, (b[1] - a[1]) / 2 - 0.006
    path = hl.ring_rrect(0.0, cy, zr, 2 * hx, 2 * hy, 0.025, per_corner=3)
    p.add('foot ring (1)', hl.sweep(path, 0.008, sides=6, closed=True), iron)

    # the backrest: bent round the back, its middle at the old backrest's back, the posts running into it
    def post_y(z):
        return BS[1] + (z - BS[2]) * (BT[1] - BS[1]) / (BT[2] - BS[2])

    def post_x(z):
        return BS[0] + (z - BS[2]) * (BT[0] - BS[0]) / (BT[2] - BS[2])
    z0, z1, t = 0.9392, OLD['Stool_Bar']['max'][2], 0.018
    sag = OLD['Stool_Bar']['max'][1] - t / 2 - post_y(z1)
    p.add('backrest, bent (1, 3)', bent_slat(0.1475, post_y, sag, t, z0, z1, n=10), chestnut, bevel=0.004)
    return p


def stool_low():
    """
    CK_Stool_Low: the low three-legged stool round the communal table (eight of them). Reference: the joined three-legged
    stool (splayed legs wedged through a thick round seat, a ring of stretchers; Windsor stool joinery), in the old
    stool's exact envelope (0.3407 x 0.326 x 0.4522, seat 0.452, its tripod centred 11.5 mm off the origin, as built).
    New: a seat eased and dished a little, turned legs, the three wedged tenons on the seat's top (the stool's signature,
    and three dots that read from above), stretchers with a swell, glides.
    """
    p = Piece('CK_Stool_Low')
    chestnut, wood = M('CC_Wood_Counter'), M('CC_Wood_Espresso')
    SC = OLD['Stool_Low']['seat_xy']
    rim = OLD['Stool_Low']['seat']

    def loop(d, z):
        return hl.ring_round(SC[0], SC[1], z, 0.1589 - d, 0.1630 - d, sides=20)

    def dish(x, y):
        return 0.007 * math.exp(-(((x - SC[0]) / 0.10) ** 2 + ((y - SC[1]) / 0.10) ** 2))
    seat = seat_board(loop, 0.4082, rim, dish, SC, cb=0.005, ct=0.008, insets=(0.05, 0.10), front_drop=0.0)
    seat_copy = copy_bm(seat)
    p.add('seat (1, 3)', seat, chestnut, smooth=True)
    legs = [((0.1531, 0.0), (0.1028, 0.0)), ((-0.0955, 0.1455), (-0.0659, 0.0943)), ((-0.0955, -0.1455), (-0.0659, -0.0943))]
    feet3d = []
    for (fx, fy), (tx, ty) in legs:
        p.add('leg, turned (3)', round_member([(fx, fy, 0.004, 0.0173), (tx, ty, 0.411, 0.0190)]), wood)
        p.add('glide (2)', glide(fx, fy, 0.0135), M('CC_Dark_Kick'))
        k = (0.452 - 0.411) / (0.411 - 0.004)
        wedged_tenon(p, seat_copy, tx + (tx - fx) * k, ty + (ty - fy) * k, 0.0125, SC)
        feet3d.append(((fx, fy, 0.004), (tx, ty, 0.411)))
    zs = 0.168
    pts = [along(f, t, zs) for f, t in feet3d]
    for i in range(3):
        a, b = pts[i], pts[(i + 1) % 3]
        p.add('stretcher, turned (2)', turned(a, b, [(0, 0.0100), (0.3, 0.0118), (0.5, 0.0126), (0.7, 0.0118), (1, 0.0100)]), wood)
    return p


def table_bistro_round():
    """
    CK_Table_BistroRound: the pedestal 2-top. Reference: Peter Meier 3017-29 cast-iron base (cabinetparts.com): a 17 in
    (432 mm) round dome base, a 3 in (76 mm) column, a 9 in (229 mm) spider under the top, 28 1/4 in high, tops to 30 in.
    The old one was three plain cylinders. Now: the chestnut top (0.72, 5 cm, its underside bevelled so the edge reads
    light), the spider that holds it, a 76 mm column with its collars, the dome base, four levellers. Base in the cafe's
    dark warm steel (cast iron) instead of espresso wood: a decision his to overrule. Keeps 0.72 x 0.72 x 0.75.
    """
    p = Piece('CK_Table_BistroRound')
    chestnut, iron, dark = M('CC_Wood_Counter'), M('DC_SteelDark'), M('CC_Dark_Kick')
    p.add('top, underside bevelled (1, 3)', hl.revolve([(0.700, 0.336), (0.712, 0.360), (0.746, 0.360), (0.750, 0.356)], sides=32),
          chestnut)
    p.add('spider hub (2)', hl.revolve([(0.680, 0.040), (0.7008, 0.044)], sides=12), iron)
    for k in range(4):
        arm = fb.zbox(0.030, 0.112, -0.013, 0.013, 0.689, 0.7003)
        p.add('spider arm (2: what holds the top)', fb.turn(arm, 45 + 90 * k, 'Z'), iron)
        a = math.radians(45 + 90 * k)
        p.add('spider screw boss (2)', fb.cylinder((0.104 * math.cos(a), 0.104 * math.sin(a), 0.6925), 0.012, 0.016, sides=8), iron)
    p.add('column collar, top (3)', hl.revolve([(0.652, 0.040), (0.664, 0.047), (0.681, 0.047)], sides=12), iron)
    p.add('column, 3 in (3)', hl.revolve([(0.100, 0.038), (0.656, 0.038)], sides=12), iron)
    p.add('column collar, foot (3)', hl.revolve([(0.078, 0.058), (0.092, 0.054), (0.110, 0.044), (0.120, 0.039)], sides=12), iron)
    p.add('dome base, 17 in (3)', hl.revolve([(0.005, 0.2143), (0.018, 0.2143), (0.026, 0.200), (0.040, 0.166), (0.057, 0.122),
                                               (0.071, 0.088), (0.081, 0.062)], sides=24), iron)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        p.add('leveller (2)', fb.cylinder((0.185 * math.cos(a), 0.185 * math.sin(a), 0.003), 0.015, 0.006, sides=8), dark)
    return p


def table_square2():
    """
    CK_Table_Square2: the Workshop square 2-top (the banquette's table). The family's construction, drawn from real
    farmhouse joinery: a plank top held flat by breadboard ends pinned with dowels, legs tapered on their two inside
    faces only (the outside stays straight and vertical: the art guide's rule 1), aprons tenoned into the legs and
    pegged, an H-stretcher whose middle rail goes through the side rails and is locked by a key (the communal table's
    keyed tenon, the family's signature). The old one: plain posts, a proud apron, no joinery. Keeps 0.72 x 0.72 x 0.75.
    """
    p = Piece('CK_Table_Square2')
    chestnut, wood, dark = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Dark_Kick')
    for y0, y1 in ((-0.360, -0.1215), (-0.1185, 0.1185), (0.1215, 0.360)):
        p.add('top plank (1, 3)', fb.rbox(-0.297, 0.297, y0, y1, 0.700, 0.750, bevel=0.005), chestnut)
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.300, s * 0.360))
        p.add('breadboard end (2: holds the planks flat)', fb.rbox(x0, x1, -0.360, 0.360, 0.700, 0.7488, bevel=0.005), chestnut)
        for y in (-0.24, 0.0, 0.24):
            p.add('breadboard pin (4)', fb.cylinder((s * 0.330, y, 0.7445), 0.0055, 0.011, sides=6), dark)
    out = 0.3275
    for sx in (-1, 1):
        for sy in (-1, 1):
            secs = []
            for z, w in ((0.005, 0.038), (0.632, 0.055), (0.703, 0.055)):
                secs.append((sx * (out - w / 2), sy * (out - w / 2), z, w, w))
            p.add('leg, inside-tapered (3)', square_member(secs, c=0.003), wood)
            p.add('glide (2)', fb.zbox(sx * (out - 0.019) - 0.017, sx * (out - 0.019) + 0.017,
                                       sy * (out - 0.019) - 0.017, sy * (out - 0.019) + 0.017, 0.0, 0.0055), dark)
            p.add('apron peg (4)', peg((sx * out, sy * 0.2955, 0.667), 'x', r=0.0048), dark)
            p.add('apron peg (4)', peg((sx * 0.2955, sy * out, 0.667), 'y', r=0.0048), dark)
    ao = out - 0.006
    for s in (-1, 1):
        x0, x1 = sorted((s * ao, s * (ao - 0.024)))
        p.add('apron (2, 3)', fb.rbox(x0, x1, -0.300, 0.300, 0.632, 0.7025, bevel=0.003), wood)
        p.add('apron (2, 3)', fb.rbox(-0.300, 0.300, x0, x1, 0.6335, 0.7020, bevel=0.003), wood)
    # the H-stretcher: side rails between the legs, the middle rail through them, keyed outside (2, 4)
    xc = out - (0.038 + 0.017 * (0.15 / 0.627)) / 2
    for s in (-1, 1):
        x0, x1 = sorted((s * (xc - 0.014), s * (xc + 0.014)))
        p.add('side stretcher (2)', fb.rbox(x0, x1, -0.310, 0.310, 0.130, 0.180, bevel=0.003), wood)
        k0, k1 = sorted((s * 0.326, s * 0.336))
        p.add('key through the tenon (4)', fb.rbox(k0, k1, -0.020, 0.020, 0.122, 0.188, bevel=0.002), dark)
    p.add('middle stretcher, through-tenoned (2)', fb.rbox(-0.344, 0.344, -0.014, 0.014, 0.135, 0.175, bevel=0.003), wood)
    return p


# ============================================================================= the new pieces


BAY = 2.10                 # one banquette bay
N_CHANNELS = 22            # 22 x 95.5 mm channels in a bay (refs: 4 in channels; the brief's 8-10 cm)
SEAT_FRONT = 0.45          # the seat's front edge height
BACK_TOP = 0.765           # the back's top: under the 0.78 sill


def back_base_y(z):
    """The channel back's seam plane: 112 mm off the wall at the seat, 62 mm at the top (a 9.3 degree rake)."""
    return -0.112 + (z - 0.432) * (0.050 / 0.306)


def banquette_bay():
    """
    CK_BanquetteBay: one 2.10 m bay of the built-in banquette (three end to end down the west windows, z 3.64-9.94).
    Real banquettes (arcedior's guide): seat 430-460 mm, seat depth 460-560, a back raked 5-10 degrees; a "tight seat &
    channel tufted back" (1stdibs), the channels about 4 in (ispydiy: plywood strips with 2 in foam, the seams sunk);
    Sightglass Divisadero's banquette backs are channel-tufted (homeadore). Here: a dark kick recessed 7 cm (job 3: the
    built-in's shadow line, and room for heels), the espresso base (3), the oat seat 11 cm thick, its front edge at 0.45
    pitched 13 mm down to the back, welts along its front edges (3), the back's 22 channels crowned 24 mm with the seams
    sunk between them (3, 4: the close-up detail), the back raked 9.3 degrees, a welted top roll at 0.765 (3). The bay's
    ends are open seams: the next bay's channels carry straight on; CK_BanquetteEnd finishes the first and last.
    Soft things are smooth inside their seams (hero_lib: "a faceted cloth is paper"); wood stays flat.
    """
    p = Piece('CK_BanquetteBay')
    fab, wood, kick = M('DC_Fabric'), M('CC_Wood_Espresso'), M('CC_Dark_Kick')
    hx = BAY / 2
    p.add('toe kick, recessed (3)', fb.zbox(-hx + 0.0015, hx - 0.0015, -0.430, -0.040, 0.0, 0.102), kick)
    p.add('base (3)', fb.rbox(-hx, hx, -0.500, 0.0, 0.098, 0.342, bevel=0.006), wood)
    # the seat cushion: a tight seat, its front bulging a little, the top pitched back (smooth: it is soft)
    seat_prof = [(-0.090, 0.340), (-0.090, 0.437), (-0.300, 0.4445), (-0.528, SEAT_FRONT), (-0.545, 0.444), (-0.551, 0.425),
                 (-0.552, 0.372), (-0.548, 0.350), (-0.535, 0.340)]
    p.add('seat cushion (1, 3)', prism(list(reversed(seat_prof)), 'x', -hx + 0.002, hx - 0.002), fab, smooth=True)
    for y, z in ((-0.5455, 0.4445), (-0.5485, 0.3505)):
        p.add('welt (3)', fb.cylinder((0.0, y, z), 0.0052, BAY - 0.008, sides=6, rot=(0, 90, 0)), fab, smooth=True)
    # the channel back: one surface across the bay, each channel crowned, the seams sunk (smooth inside a channel; the
    # seams' fold is sharper than 42 degrees, so they stay creased)
    rows = [(0.432, 0.00), (0.452, 0.55), (0.490, 0.95), (0.670, 1.00), (0.712, 0.80), (0.738, 0.00)]
    crown = 0.024
    per = 4
    ncol = N_CHANNELS * per
    w = BAY / N_CHANNELS
    shape = [0.0, 0.80, 1.0, 0.80]           # steep shoulders into the seam, a round crown
    bm = bmesh.new()
    grid = []
    for z, k in rows:
        line = []
        for i in range(ncol + 1):
            x = -hx + w * i / per
            c = shape[i % per] * crown * k
            line.append(bm.verts.new((x, back_base_y(z) - c, z)))
        grid.append(line)
    for r in range(len(rows) - 1):
        lo, hi = grid[r], grid[r + 1]
        for i in range(ncol):
            bm.faces.new([lo[i], lo[i + 1], hi[i + 1], hi[i]])
    # the surface must face the room (-Y): an open sheet, so it says so (Piece.finish keeps it that way)
    p.add('channel-tufted back (3, 4)', bm, fab, smooth=True, face_dir=(0, -1, 0))
    # the top roll and its welt: the channels are sewn into it
    yb = back_base_y(0.738)
    roll = [(yb - 0.002, 0.734), (yb - 0.004, 0.745), (yb + 0.002, 0.757), (yb + 0.016, 0.7635), (-0.040, BACK_TOP),
            (0.0, BACK_TOP), (0.0, 0.734)]
    p.add('back top roll (3)', prism(list(reversed(roll)), 'x', -hx, hx), fab, smooth=True)
    p.add('top welt (3)', fb.cylinder((0.0, yb - 0.004, 0.7415), 0.0055, BAY - 0.003, sides=6, rot=(0, 90, 0)), fab, smooth=True)
    return p


def banquette_end():
    """
    CK_BanquetteEnd: the run's finished end (one at z 3.60, one at 9.94; the same mesh both ends, it is symmetric across
    its 4 cm). An espresso panel standing against the wall: full height at the back (the top roll's 0.765), sweeping down
    to a low arm over the seat's front, the toe-kick's notch carried through so the kick line runs round (3).
    """
    p = Piece('CK_BanquetteEnd')
    prof = [(-0.430, 0.0), (0.0, 0.0), (0.0, BACK_TOP), (-0.060, BACK_TOP), (-0.085, 0.752), (-0.115, 0.712),
            (-0.180, 0.655), (-0.300, 0.630), (-0.470, 0.622), (-0.540, 0.605), (-0.562, 0.580), (-0.565, 0.555),
            (-0.565, 0.100), (-0.430, 0.100)]
    bm = prism(list(reversed(prof)), 'x', -0.020, 0.020, tri_caps=True)
    p.add('end panel (3)', bm, M('CC_Wood_Espresso'))
    return p


def communal_table():
    """
    CK_CommunalTable: 3.60 x 0.90 x 0.75, the Workshop family (the library's Table_Communal at twice the length). Real
    long tables: a 12 ft top "will sag in short order" on two supports; "support in the middle ... a third trestle"
    (woodweb, Trestle Table Dimensions); overhangs of about 30 in. So three trestles at 0 and +-0.90 m, between the eight
    stools (at +-0.45 and +-1.35 along the table, both sides), never where knees and feet go: 0.90 m overhang at each
    end. Four chestnut planks 6.5 cm thick with breadboard ends pinned through, trestles of a pegged post between a
    chamfered cleat and a foot relieved to stand on two pads, and one beam through all three posts, its ends keyed
    outside the end posts (the family's signature). Long along Blender Y = Unity Z: it goes into the plan with no turn.
    """
    p = Piece('CK_CommunalTable')
    chestnut, wood, dark = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Dark_Kick')
    hx, hy = 0.45, 1.80
    planks = 4
    gap = 0.003
    pw = (2 * hx - (planks - 1) * gap) / planks
    for i in range(planks):
        x0 = -hx + i * (pw + gap)
        p.add('top plank (1, 3)', fb.rbox(x0, x0 + pw, -1.727, 1.727, 0.685, 0.750, bevel=0.006), chestnut)
    for s in (-1, 1):
        y0, y1 = sorted((s * 1.730, s * hy))
        p.add('breadboard end (2)', fb.rbox(-hx, hx, y0, y1, 0.685, 0.7488, bevel=0.006), chestnut)
        for x in (-0.3375, -0.1125, 0.1125, 0.3375):
            p.add('breadboard pin (4)', fb.cylinder((x, s * 1.765, 0.7445), 0.0055, 0.011, sides=6), dark)
    cleat = [(-0.36, 0.688), (-0.36, 0.665), (-0.31, 0.628), (0.31, 0.628), (0.36, 0.665), (0.36, 0.688)]
    foot = [(-0.37, 0.025), (0.37, 0.025), (0.37, 0.060), (0.33, 0.088), (-0.33, 0.088), (-0.37, 0.060)]
    for yt in (-0.90, 0.0, 0.90):
        p.add('trestle cleat (2)', prism(cleat, 'y', yt - 0.040, yt + 0.040), wood, bevel=0.004)
        p.add('trestle post (3)', fb.rbox(-0.060, 0.060, yt - 0.040, yt + 0.040, 0.080, 0.632, bevel=0.005), wood)
        p.add('trestle foot (3)', prism(foot, 'y', yt - 0.045, yt + 0.045), wood, bevel=0.004)
        for s in (-1, 1):
            x0, x1 = sorted((s * 0.25, s * 0.37))
            p.add('foot pad (2: it stands on its ends)', fb.rbox(x0, x1, yt - 0.045, yt + 0.045, 0.0, 0.027, bevel=0.003), wood)
            p.add('peg, foot (4)', peg((0.0, yt + s * 0.045, 0.058), 'y', r=0.0055), dark)
            p.add('peg, cleat (4)', peg((0.0, yt + s * 0.040, 0.652), 'y', r=0.0055), dark)
    p.add('beam through the posts (2)', fb.rbox(-0.025, 0.025, -1.000, 1.000, 0.245, 0.325, bevel=0.004), wood)
    for s in (-1, 1):
        y0, y1 = sorted((s * 0.958, s * 0.976))
        p.add('key through the beam (2, 4)', fb.rbox(-0.021, 0.021, y0, y1, 0.215, 0.355, bevel=0.002), dark)
    return p


def window_counter():
    """
    CK_WindowCounter: the east window bar, 4.00 x 0.45, the top at 1.05 (a 0.75 bar stool + 0.30). The wall below the
    glass is a 24 cm knee wall to 0.78 with the glass 12 cm back, and an 8 cm skirting to 0.26: the counter's back sits at
    the wall's face and four knee-wall brackets carry it, their plates screwed to the knee wall between the skirting and
    the sill (Federal Brace's countertop and kneewall brackets: 1/4 in steel plate, 2 in flanges, a gusset). Brackets at
    +-0.60 and +-1.80 m: between the three stools (proposed at 0 and +-1.20 along the counter), so no knee meets one.
    Chestnut top 7 cm, warm steel (DC_Steel) brackets, dark screw heads. Wall piece.
    """
    p = Piece('CK_WindowCounter')
    p.floor = False
    top, steel, dark = M('CC_Wood_Counter'), M('DC_Steel'), M('CC_Dark_Kick')
    p.add('top (1, 3)', fb.rbox(-2.0, 2.0, -0.450, -0.003, 0.980, 1.050, bevel=0.006), top)
    web = [(-0.005, 0.346), (-0.045, 0.346), (-0.045, 0.935), (-0.370, 0.935), (-0.396, 0.960), (-0.396, 0.979), (-0.005, 0.979)]
    for xb in (-1.80, -0.60, 0.60, 1.80):
        p.add('bracket wall flange (2)', fb.zbox(xb - 0.025, xb + 0.025, -0.006, 0.0, 0.340, 0.981), steel)
        # the web: one L-shaped 6 mm plate, upright on the wall and an arm under the top (one piece, so no two faces fight)
        p.add('bracket web (2, 3)', prism(web, 'x', xb - 0.003, xb + 0.003, tri_caps=True), steel)
        p.add('bracket top flange (2)', fb.zbox(xb - 0.025, xb + 0.025, -0.400, -0.0063, 0.9745, 0.9805), steel)
        L = math.hypot(0.27, 0.345) + 0.024
        # the brace: 5 mm, so its faces sit just inside the web's where they cross
        p.add('bracket brace (2, 3)', fb.box((0.005, 0.035, L), (xb, -0.165, 0.7725), rot=(38.05, 0, 0)), steel)
        for z in (0.430, 0.640):
            for sx in (-1, 1):
                p.add('screw (2)', fb.cylinder((xb + sx * 0.015, -0.0072, z), 0.0055, 0.0035, sides=6, rot=(90, 0, 0)), dark)
    return p


# ============================================================================= the list and the audit


PIECES = [
    # (builder, group, what it is, the library piece it replaces or None, wall piece)
    (banquette_bay, 'Banquette', 'one 2.10 m bay of the built-in banquette: seat front 0.45, channel back to 0.765 (wall piece)', None, True),
    (banquette_end, 'Banquette', 'the banquette run\'s finished end panel, 0.04 thick (wall piece; the same mesh at both ends)', None, True),
    (communal_table, 'Tables', 'communal table 3.60 x 0.90 x 0.75, three trestles, long along Unity Z', None, False),
    (window_counter, 'Counters', 'window counter 4.00 x 0.45, top at 1.05, on four knee-wall brackets (wall piece)', None, True),
    (chair_cafe, 'Seats', 'cafe chair, ladder back, seat 0.451 (replaces Chair_Cafe)', 'Chair_Cafe', False),
    (chair_spindle, 'Seats', 'spindle chair, Windsor stick-back, seat 0.452 (replaces Chair_Spindle)', 'Chair_Spindle', False),
    (stool_bar, 'Seats', 'bar stool with backrest and foot ring, seat 0.752 (replaces Stool_Bar)', 'Stool_Bar', False),
    (stool_low, 'Seats', 'low three-legged stool, seat 0.452 (replaces Stool_Low)', 'Stool_Low', False),
    (table_bistro_round, 'Tables', 'bistro pedestal 2-top, 0.72 round, 0.75 (replaces Table_BistroRound)', 'Table_BistroRound', False),
    (table_square2, 'Tables', 'square 2-top, 0.72, 0.75 (replaces Table_Square2)', 'Table_Square2', False),
]


def coplanar_overlaps(ob, tol=2e-4, min_area=2e-6):
    """
    Rule 5 (no two faces in one plane): pairs of faces from different islands lying in one plane and overlapping by
    more than min_area. 'same' = facing the same way (they would fight: z-fighting); 'opposite' = pressed face to face
    (hidden, but a junction with no overlap). Returns (same, opposite).
    """
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    # island id per face
    island = {}
    cur = 0
    for f in bm.faces:
        if f.index in island:
            continue
        stack = [f]
        island[f.index] = cur
        while stack:
            g = stack.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in island:
                        island[h.index] = cur
                        stack.append(h)
        cur += 1
    groups = {}
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        a = max(range(3), key=lambda k: abs(n[k]))
        sgn = 1 if n[a] > 0 else -1
        cn = n * sgn
        d = cn.dot(f.verts[0].co)
        key = (round(cn.x, 2), round(cn.y, 2), round(cn.z, 2), round(d / tol))
        groups.setdefault(key, []).append((f, sgn))

    def proj(f, a):
        ax = [k for k in range(3) if k != a]
        return [(v.co[ax[0]], v.co[ax[1]]) for v in f.verts]

    def clip(subject, clipper):
        def inside(p, a, b):
            return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= -1e-12

        def inter(p1, p2, a, b):
            x1, y1 = p1
            x2, y2 = p2
            x3, y3 = a
            x4, y4 = b
            den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
            if abs(den) < 1e-15:
                return p2
            t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
            return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))
        out = subject
        for i in range(len(clipper)):
            a, b = clipper[i], clipper[(i + 1) % len(clipper)]
            inp, out = out, []
            if not inp:
                break
            s = inp[-1]
            for e in inp:
                if inside(e, a, b):
                    if not inside(s, a, b):
                        out.append(inter(s, e, a, b))
                    out.append(e)
                elif inside(s, a, b):
                    out.append(inter(s, e, a, b))
                s = e
        return out

    def area(poly):
        return 0.5 * abs(sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                             for i in range(len(poly))))

    def ccw(poly):
        s = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
        return poly if s > 0 else list(reversed(poly))
    same = opposite = 0
    examples = []
    for key, fs in groups.items():
        if len(fs) < 2:
            continue
        nrm = Vector(key[:3])
        a = max(range(3), key=lambda k: abs(nrm[k]))
        for i in range(len(fs)):
            fi, si = fs[i]
            pi = ccw(proj(fi, a))
            for j in range(i + 1, len(fs)):
                fj, sj = fs[j]
                if island[fi.index] == island[fj.index]:
                    continue
                pj = ccw(proj(fj, a))
                # convex faces only (quads and caps are); n-gons that are not convex are triangulated upstream
                ov = clip(pi, pj)
                if len(ov) >= 3 and area(ov) > min_area:
                    if si == sj:
                        same += 1
                    else:
                        opposite += 1
                    if len(examples) < 8:
                        c = fi.calc_center_median()
                        examples.append(('same' if si == sj else 'opposite', tuple(round(v, 3) for v in c)))
    bm.free()
    return same, opposite, examples


def build_all():
    """Every piece, finished and audited: [{object, group, what, old, wall, islands, floating, coplanar, tris, size, min, max,
    materials}]. Nothing is placed: each piece is at the origin."""
    out = []
    for builder, group, what, old, wall in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        same, opp, ex = coplanar_overlaps(ob)
        degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-9)
        out.append({'object': ob, 'group': group, 'what': what, 'old': old, 'wall': wall, 'islands': n,
                    'floating': floating, 'coplanar_same': same, 'coplanar_opposite': opp, 'coplanar_examples': ex,
                    'degenerate': degenerate, 'shells_turned': ob.get('fixit_shells_turned', 0), **st})
    return out
