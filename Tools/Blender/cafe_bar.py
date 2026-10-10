"""
The café's bar, v1 (10 Oct 2026): the redesign's first Blender session (research Part B section 6.3-6.5 and 7.1,
scene-facts.md for the coordinates, briefs/A-bar.md). Seven pieces, built from measured references in the house style
(fixit_blender.py and hero_lib.py; the references, the reasoning and the cut list are in report.md beside this file):

  CK_FrontCounter  ONE counter, 12.27 m: a pale stone top 8 cm thick at 1.095, an espresso customer face, a recessed
                   dark kick, the half-round right end (r 0.47, the room's one curve), the ADA 904.4 lowered section
                   (0.95 m long, top 0.91), the repair bar's 35 cm ledge with the opening behind it, two open shelves
                   under the top on Ace's side (the pastry case, the till), a 2" warm-steel foot rail for the queue
                   on bar-mount brackets every 1.2 m with returns into the face, broken at the lowered section.
  CK_RepairBench   the watch-and-electronics bench in that opening: a chestnut top 7 cm thick at 1.005 exactly, two
                   pedestals of five graduated drawers with brass bar pulls, a modesty panel, an apron, a bench power
                   strip under Ace's edge, and the low glass screen: three 10 mm panels in a steel shoe on the
                   customer edge, 30 cm above the ledge, on four warm-steel posts with glass clamps.
  CK_BackBar       the back bar's base run, 12.30 m, stone top at 1.00 everywhere, 21 espresso doors (twenty 0.60 m,
                   one 0.27 m at the end) with real 6 mm reveals and brass bar pulls, a recessed kick, an end panel.
  CK_BackBarShelf  one chestnut shelf 8.70 x 0.30 x 0.06 at 1.75, warm-steel L brackets every 1.2 m (symmetric about
                   the till), a steel light channel under its front edge with the warm strip (emissive in Unity).
  CK_Backsplash    the tile panel 8.70 x 0.754, 2 cm proud of the wall, a steel edge trim at its open (east) end.
  CK_PartsWall     72 labelled parts drawers, 4 rows x 18, in an espresso carcass 3.60 x 0.30 x 0.75; each drawer a
                   front with a brass label holder with pull (the card-catalogue hardware) and a label card whose face
                   is UV-mapped into the shared atlas CF_DrawerLabels (row 0 the top, col 0 the room's left).
  CK_BenchLamp     a spring-balanced magnifier lamp: an edge clamp, a post, two parallel-rod arms with their springs,
                   an elbow with its tension knob, a cream enamel head (0.18 m) holding a 127 mm 5-diopter lens with
                   the LED ring under it, and the handle. Its pose is computed from where its light must fall.

THE FRAME (every piece): 1 unit = 1 m, Z up, the front (the side the room or the customer sees) faces -Y. Every piece
is built facing the way it stands in the room, so in Unity each goes in turned 180 degrees about Y at PLACEMENT[name];
then Blender +X reads as world +x (east), Blender +Y as world +z (north), Blender +Z as up: world = position + (bx,
bz, by). Inside each FBX the house rule holds: Unity local (x, y, z) = Blender (-x, z, -y).

Run with the bpy module or headless Blender; see export_cafe_bar.py and review_cafe_bar.py.
"""
import math
import os
import sys

# the shared helpers (fixit_blender.py, hero_lib.py): beside this file in the repo's Tools/Blender, or the cloud copy
_HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.environ.get('FIXIT_TOOLS') or next(
    (p for p in (_HERE, '/home/claude/cafe/inputs/Tools/Blender') if os.path.exists(os.path.join(p, 'fixit_blender.py'))), _HERE)
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# ============================================================================= materials
# (sRGB hex, Unity smoothness, extra manifest fields). Metallic 0 everywhere (the house rule).
MATERIALS = {
    'CF_Stone':         ('#E6E1D7', 0.65, {'note': 'pale honed stone, tiling texture CF_Stone (1 tile = 1.0 m)'}),
    'CC_Wood_Espresso': ('#4A3422', 0.38, {'note': 'existing; boards (CF_Boards) on the counter face and the doors'}),
    'CC_Dark_Kick':     ('#2A1E16', 0.30, {'note': 'existing; kicks and reveals'}),
    'CC_Wood_Counter':  ('#8F5B32', 0.71, {'note': 'existing; chestnut: bench top, shelf'}),
    'DC_Steel':         ('#93908A', 0.50, {'note': 'existing; warm steel, metallic 0'}),
    'DC_SteelDark':     ('#4E4B47', 0.50, {'note': 'existing; dark warm steel, metallic 0'}),
    'T2_Brass':         ('#B8933F', 0.50, {'note': 'existing; brass, metallic 0'}),
    'DC_Ceramic':       ('#F2EFE8', 0.30, {'note': 'existing; the power strip\'s socket faces'}),
    'CF_Tile':          ('#EDE4D3', 0.70, {'note': 'glazed tile, tiling texture CF_Tile (5 x 20 cm, 1 tile = 1.0 m)'}),
    'CF_Glass':         ('#DCE6E4', 0.90, {'transparent': True, 'alpha': 0.22, 'note': 'transparent in Unity'}),
    'CF_Enamel':        ('#F2EFE8', 0.60, {'note': 'cream enamel, the pendants\' shade colour: the lamp head'}),
    'CF_LightStrip':    ('#FFD7A8', 0.50, {'emission': 'FFD7A8', 'note': 'NEW: emissive warm white (shelf strip, lamp LED ring, strip switch)'}),
    'CF_DrawerLabels':  ('#EDE6D6', 0.20, {'texture': 'Assets/Art/Textures/Cafe/CF_DrawerLabels.png',
                                           'note': 'the label atlas, 18 x 4 cells (Words lead); faces carry UVs'}),
}


def M(name):
    hexstr, smooth, extra = MATERIALS[name]
    m = fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)
    m['smoothness'] = smooth
    return m


# ============================================================================= where each piece stands (Unity world)
# position (x, y, z) in Unity world metres; every piece turned (0, 180, 0). Filled in below for the lamp.
PLACEMENT = {
    'CK_FrontCounter': (-1.265, 0.0, 13.77),
    'CK_RepairBench':  (-4.20, 0.0, 14.055),
    'CK_BackBar':      (-1.25, 0.0, 18.00),
    'CK_BackBarShelf': (0.55, 1.75, 18.00),
    'CK_Backsplash':   (0.55, 1.00, 18.00),
    'CK_PartsWall':    (-5.60, 1.00, 18.00),
}

# The lamp: its post on the bench top 4 cm in from Ace's edge, 0.50 m from the bench's west end (in the knee space,
# 1.8 cm east of the west pedestal, clear of the apron, north of the mat, the tray and the caddy); its head over the
# mat's centre as the bench-move plan places it (Systems lead, 10 Oct: mat x -5.334..-4.734, z 13.818..14.178, top
# 1.006; the close-up camera at (-5.034, 1.498, 14.305), 58 degrees down, 40 degrees vertical, 16:9, zoomed 0.42x-1.2x
# along its line). The head's height is set by the 1.2x zoom: its north rim must stay above the frame's top plane.
LAMP_POST_WORLD = (-5.30, 14.41)
LAMP_TARGET_WORLD = (-5.034, 13.998)
LAMP_HEAD_Y = 1.425           # world height of the head's centre: 0.42 above the bench top
BENCH_TOP_Y = 1.005
# The glass screen's four posts (world x). Not evenly spaced, on purpose: no post inside the close-up camera's frame
# (at the glass it spans the mat's x +/- 0.45 m: -5.33 to -4.43 for the expected mat) and none on the middle stool's
# line of sight to the work; the wide pane stands in front of the work, the two narrow ones toward the pastry case.
SCREEN_POSTS_WORLD = [-5.76, -4.30, -3.47, -2.64]


# ============================================================================= the prop with the bar's own finishing


class BarProp(fb.Prop):
    """fb.Prop plus: n-gons triangulated (concave caps read the same in every importer), UV rules run after the box
    projection (the label atlas, the boards round the curve), and a record of faces that must face a given way."""

    def __init__(self, name, floor=True):
        super().__init__(name, floor=floor)
        self.uv_rules = []
        self.must_face = []       # (list of BMFace, direction Vector)

    def finish(self, collection=None):
        bm = self.bm
        self.floored = 0
        if self.floor:
            for v in bm.verts:
                if -0.02 < v.co.z < 0.0:
                    v.co.z = 0.0
                    self.floored += 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.turned = fb.orient_shells(bm)
        # single faces (the label cards) keep the side they were built facing
        self.flipped_cards = 0
        for faces, want in self.must_face:
            for f in faces:
                if f.is_valid:
                    f.normal_update()
                    if f.normal.dot(want) < 0:
                        f.normal_flip()
                        self.flipped_cards += 1
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method='BEAUTY', ngon_method='EAR_CLIP')
        bm.normal_update()
        fb.box_uvs(bm)
        uv = bm.loops.layers.uv.verify()
        for rule in self.uv_rules:
            rule(bm, uv)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        for m in self.mats:
            me.materials.append(m)
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        ob['fixit_shells_turned'] = self.turned
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


# ============================================================================= geometry helpers (each returns a bmesh)


def arc(cx, cy, r, a0, a1, n):
    """n + 1 points from angle a0 to a1 (degrees) round (cx, cy)."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * k / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * k / n)))
            for k in range(n + 1)]


def prism_z(poly, z0, z1):
    """A closed solid from a plan polygon (x, y), anticlockwise seen from above, between z0 and z1."""
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in poly]
    hi = [bm.verts.new((x, y, z1)) for x, y in poly]
    n = len(poly)
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    return bm


def prism_y(poly_xz, y0, y1):
    """A closed solid from an elevation polygon (x, z), anticlockwise seen from the front (-Y), from y0 back to y1."""
    bm = bmesh.new()
    fr = [bm.verts.new((x, y0, z)) for x, z in poly_xz]
    bk = [bm.verts.new((x, y1, z)) for x, z in poly_xz]
    n = len(poly_xz)
    bm.faces.new(fr)
    bm.faces.new(list(reversed(bk)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([fr[i], bk[i], bk[j], fr[j]])
    return bm


def prism_x(poly_yz, x0, x1):
    """A closed solid with a constant profile (y, z) from x0 to x1 (a lip, a wedge)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in poly_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in poly_yz]
    n = len(poly_yz)
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], b[i], b[j], a[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def front_box(x0, x1, y0, y1, z0, z1):
    """A box with no back face (+Y): the front (-Y) and four sides, wound outward. For parts whose back is buried."""
    bm = bmesh.new()
    v = {}
    for ix, x in enumerate((x0, x1)):
        for iy, y in enumerate((y0, y1)):
            for iz, z in enumerate((z0, z1)):
                v[(ix, iy, iz)] = bm.verts.new((x, y, z))
    F = bm.faces.new
    F([v[0, 0, 0], v[1, 0, 0], v[1, 0, 1], v[0, 0, 1]])      # front
    F([v[0, 0, 0], v[0, 1, 0], v[1, 1, 0], v[1, 0, 0]])      # bottom
    F([v[0, 0, 1], v[1, 0, 1], v[1, 1, 1], v[0, 1, 1]])      # top
    F([v[0, 0, 0], v[0, 0, 1], v[0, 1, 1], v[0, 1, 0]])      # left
    F([v[1, 0, 0], v[1, 1, 0], v[1, 1, 1], v[1, 0, 1]])      # right
    return bm


def card_quad(x0, x1, y, z0, z1):
    """One face facing -Y (a label card's face)."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1))]
    bm.faces.new(vs)
    return bm


def oriented_box(centre, ax, ay, az, hx, hy, hz):
    """A box at centre with its half sizes along three (orthonormal) axes."""
    c, ax, ay, az = Vector(centre), Vector(ax).normalized(), Vector(ay).normalized(), Vector(az).normalized()
    bm = bmesh.new()
    v = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                v[(sx, sy, sz)] = bm.verts.new(c + ax * (sx * hx) + ay * (sy * hy) + az * (sz * hz))
    F = bm.faces.new
    F([v[-1, -1, -1], v[1, -1, -1], v[1, -1, 1], v[-1, -1, 1]])
    F([v[-1, 1, -1], v[-1, 1, 1], v[1, 1, 1], v[1, 1, -1]])
    F([v[-1, -1, -1], v[-1, -1, 1], v[-1, 1, 1], v[-1, 1, -1]])
    F([v[1, -1, -1], v[1, 1, -1], v[1, 1, 1], v[1, -1, 1]])
    F([v[-1, -1, -1], v[-1, 1, -1], v[1, 1, -1], v[1, -1, -1]])
    F([v[-1, -1, 1], v[1, -1, 1], v[1, 1, 1], v[-1, 1, 1]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def bar_between(p0, p1, side, w, t):
    """A flat bar from p0 to p1: w wide along `side` (made square to the bar), t thick in the third direction."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    length = d.length
    d.normalize()
    s = Vector(side)
    s = (s - d * s.dot(d)).normalized()
    n = d.cross(s).normalized()
    return oriented_box((p0 + p1) / 2, d, s, n, length / 2, w / 2, t / 2)


def rod(p0, p1, r, sides=6):
    """A round rod between two points."""
    return fb.strut(tuple(p1), tuple(p0), r, r, sides=sides)


def disc_y(cx, cz, y0, y1, r, sides=8):
    """A short cylinder whose axis runs along Y, from y0 to y1 (a flange on a face)."""
    return fb.cylinder((cx, (y0 + y1) / 2, cz), r, abs(y1 - y0), sides=sides, rot=(90, 0, 0))


def rail_tube(path_xy, z, r, sides=10):
    """A round tube along a horizontal path at height z, its corners mitred at the true width (a bent foot rail)."""
    pts = [Vector((x, y, z)) for x, y in path_xy]
    up = Vector((0, 0, 1))
    loops = []
    n = len(pts)
    for i in range(n):
        if i == 0:
            d, s = (pts[1] - pts[0]).normalized(), 1.0
        elif i == n - 1:
            d, s = (pts[-1] - pts[-2]).normalized(), 1.0
        else:
            d0, d1 = (pts[i] - pts[i - 1]).normalized(), (pts[i + 1] - pts[i]).normalized()
            d = (d0 + d1).normalized()
            s = 1.0 / max(0.3, d0.dot(d))
        k = d.cross(up).normalized()
        loops.append([tuple(pts[i] + k * (r * s * math.cos(a)) + up * (r * math.sin(a)))
                      for a in (2 * math.pi * j / sides + math.pi / sides for j in range(sides))])
    return hl.tube(loops)


def ribbed(p0, p1, r_out, r_in, ridges, sides=5):
    """A close-coiled spring as a ribbed tube: rings alternating between r_out and r_in along p0 -> p1."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    d = axis.normalized()
    k = d.cross(Vector((0, 0, 1)))
    if k.length < 1e-6:
        k = d.cross(Vector((0, 1, 0)))
    k.normalize()
    m = d.cross(k).normalized()
    rings = 2 * ridges + 1
    loops = []
    for i in range(rings):
        c = p0 + axis * (i / (rings - 1))
        rr = r_out if i % 2 else r_in
        if i in (0, rings - 1):
            rr = r_in
        loops.append([tuple(c + (k * math.cos(a) + m * math.sin(a)) * rr) for a in (2 * math.pi * j / sides for j in range(sides))])
    return hl.tube(loops)


def ring_z(cx, cy, z0, z1, r_in, r_out, sides=16):
    """A flat ring about a vertical axis (a lamp head's housing, its LED ring)."""
    bm = hl.ring_wall(z0, z1, r_in, r_out, sides=sides)
    return hl.moved(bm, cx, cy, 0.0)


def bevel(bm, w):
    fb.bevel_all(bm, w, segments=1, angle=30.0)
    bmesh.ops.dissolve_degenerate(bm, dist=2e-6, edges=bm.edges[:])
    return bm


def rbox(x0, x1, y0, y1, z0, z1, b=0.0):
    bm = fb.zbox(x0, x1, y0, y1, z0, z1)
    if b > 0:
        bevel(bm, min(b, (x1 - x0) / 2.2, (y1 - y0) / 2.2, (z1 - z0) / 2.2))
    return bm


def bar_pull(p, centre, axis, length, standoff, r=0.006, post=0.008, facing=-1):
    """A brass bar pull: the bar (6 sides) on two square standoffs, standing off a face whose outside is `facing` in Y
    (-1: the face looks -Y, the room's side; +1: it looks +Y, Ace's side). axis 'x' (horizontal) or 'z' (vertical)."""
    cx, cy, cz = centre                    # cy: the face's y
    yb = cy + facing * standoff            # the bar's centre line
    y0, y1 = sorted((cy - facing * 0.002, yb + facing * 0.002))
    half = length / 2
    if axis == 'x':
        p.add('pull bar', rod((cx - half, yb, cz), (cx + half, yb, cz), r, sides=6), M('T2_Brass'))
        ends = [(cx - half + 0.012, cz), (cx + half - 0.012, cz)]
    else:
        p.add('pull bar', rod((cx, yb, cz - half), (cx, yb, cz + half), r, sides=6), M('T2_Brass'))
        ends = [(cx, cz - half + 0.012), (cx, cz + half - 0.012)]
    for ex, ez in ends:
        p.add('pull post', fb.zbox(ex - post / 2, ex + post / 2, y0, y1, ez - post / 2, ez + post / 2), M('T2_Brass'))


# ============================================================================= 1. the front counter

FC_OX, FC_OZ = PLACEMENT['CK_FrontCounter'][0], PLACEMENT['CK_FrontCounter'][2]
FC = {}       # the counter's key numbers in its own frame, for the report and the checks


def front_counter():
    """
    ONE counter (research 6.3; the brief's coordinates). Its frame: x = world x + 1.265, y = world z - 13.77.
    The stone overhangs the body 4 cm all round (the customer side as specified; Ace's side and the round the same, so
    no face of the top lies in the plane of a face of the body), and 5 mm at the ledge's back; the body stops 2 mm
    inside the stone; the kick touches the body (no overlap, so no two faces share a plane).
    """
    p = BarProp('CK_FrontCounter')
    X = lambda w: w - FC_OX          # noqa: E731
    Y = lambda w: w - FC_OZ          # noqa: E731
    stone, wood, kick, chest, steel = M('CF_Stone'), M('CC_Wood_Espresso'), M('CC_Dark_Kick'), M('CC_Wood_Counter'), M('DC_Steel')
    x_w, x_r0, x_r1 = X(-7.40), X(-5.80), X(-2.60)          # west end; the repair bar's opening
    x_a0, x_a1 = X(-2.55), X(-1.65)                           # the open shelf under the pastry case (Ace's side)
    x_b0, x_b1 = X(-0.55), X(0.55)                            # the open shelf under the till
    x_l0, x_l1 = X(2.30), X(3.25)                             # the lowered section (ADA 904.4: 36" long, 36" high max), east of the intake's slots (x -1.6..1.7)
    x_c = X(4.40)                                             # the half-round's centre line (the straight run ends)
    R_TOP, R_BODY, R_KICK = 0.47, 0.43, 0.36
    y_tf, y_bf, y_kf = -0.47, -0.43, -0.36                    # top, body and kick: the customer side
    y_tb, y_bb, y_kb = 0.47, 0.43, 0.36                       # and Ace's side
    y_lt = Y(13.65)                                           # the ledge's back (the stone)
    y_lb = y_lt - 0.005                                       # the ledge body's back, 5 mm under the stone
    y_rb = 0.03                                               # the open shelves' back wall (40 cm deep)
    KICK, TOP0, TOP1, LOW0, LOW1 = 0.10, 1.015, 1.095, 0.83, 0.91
    BT = TOP0 + 0.002                                         # the body's top, 2 mm inside the stone
    REC0, REC1 = 0.55, 0.93                                   # the open shelves: the board's top at REC0 + 0.02
    N = 24                                                    # facets on the half-round (each about 6 cm on the top)

    # --- the stone: the long west top (with the repair bar's notch), the east top with the half-round, the low top
    OH = 0.004                                                # the stone's overhang where its end meets the body's
    west = [(x_w, y_tf), (x_l0 + OH, y_tf), (x_l0 + OH, y_tb), (x_r1 - OH, y_tb), (x_r1 - OH, y_lt), (x_r0 + OH, y_lt),
            (x_r0 + OH, y_tb), (x_w, y_tb)]
    p.add('stone top (west, the ledge, the till)', bevel(prism_z(west, TOP0, TOP1), 0.008), stone)
    east = [(x_l1 - OH, y_tf)] + arc(x_c, 0.0, R_TOP, -90, 90, N) + [(x_l1 - OH, y_tb)]
    p.add('stone top (east, the half-round)', bevel(prism_z(east, TOP0, TOP1), 0.008), stone)
    p.add('stone top (lowered section)', bevel(prism_z([(x_l0 - 0.002, y_tf), (x_l1 + 0.002, y_tf), (x_l1 + 0.002, y_tb), (x_l0 - 0.002, y_tb)], LOW0, LOW1), 0.008), stone)

    # --- the body: one customer face for the whole straight run (the dip at the lowered section), the blocks behind
    x_wb = x_w + 0.004                                        # the body stops 4 mm off the west wall (a scribe gap)
    face = [(x_wb, KICK), (x_c, KICK), (x_c, BT), (x_l1, BT), (x_l1, LOW0 + 0.002), (x_l0, LOW0 + 0.002), (x_l0, BT), (x_wb, BT)]
    p.add('customer face and ledge body', prism_y(face, y_bf, y_lb), wood)
    p.add('body (west end)', fb.zbox(x_wb, x_r0, y_lb, y_bb, KICK, BT), wood)
    slabs = [(x_r1, x_a0, 'full'), (x_a0, x_a1, 'shelf'), (x_a1, x_b0, 'full'), (x_b0, x_b1, 'shelf'), (x_b1, x_l0, 'full'),
             (x_l0, x_l1, 'low'), (x_l1, x_c, 'full')]
    for x0, x1, kind in slabs:
        if kind == 'full':
            p.add('body', fb.zbox(x0, x1, y_lb, y_bb, KICK, BT), wood)
        elif kind == 'low':
            p.add('body (lowered section)', fb.zbox(x0, x1, y_lb, y_bb, KICK, LOW0 + 0.002), wood)
        else:
            p.add('body behind the open shelf', fb.zbox(x0, x1, y_lb, y_rb, KICK, BT), wood)
            p.add('body under the open shelf', fb.zbox(x0, x1, y_rb, y_bb, KICK, REC0), wood)
            p.add('rail over the open shelf', fb.zbox(x0, x1, y_rb, y_bb, REC1, BT), wood)
            p.add('open shelf board (chestnut)', fb.zbox(x0 - 0.002, x1 + 0.002, y_rb - 0.002, y_bb - 0.002, REC0 - 0.002, REC0 + 0.02), chest)
    round_body = arc(x_c, 0.0, R_BODY, -90, 90, N)
    p.add('body (the half-round)', prism_z(round_body, KICK, BT), wood)

    # --- the kick: 10 cm high, 7 cm back on both faces and round the end; to the wall at the west; only under the ledge
    #     in the opening (1 cm clear of the opening's walls and of the ledge's back)
    kick_poly = ([(x_wb, y_kf), (x_c, y_kf)] + arc(x_c, 0.0, R_KICK, -90, 90, N)[1:-1] +
                 [(x_c, y_kb), (x_r1 + 0.01, y_kb), (x_r1 + 0.01, y_lb - 0.01), (x_r0 - 0.01, y_lb - 0.01), (x_r0 - 0.01, y_kb), (x_wb, y_kb)])
    p.add('kick', prism_z(kick_poly, 0.0, KICK), kick)

    # --- the foot rail: 2" (5 cm) warm steel, centre 0.20 off the floor, 0.15 off the face, along the queue from
    #     x -1.90 to the round (world), broken at the lowered section (a parallel approach needs the counter's face),
    #     each run returning into the face through a flange, bar-mount brackets so no span is over 1.2 m
    RAIL_Z, RAIL_R, RB = 0.20, 0.025, 0.06
    y_rail = y_bf - 0.15
    y_in = y_bf + 0.004
    runs = [(X(-1.90), X(2.15), [X(-0.90), X(0.10), X(1.10)]), (X(3.40), X(4.30), [])]
    for xa, xb, brackets in runs:
        path = [(xa, y_in)]
        path += [(xa + RB + RB * math.cos(math.radians(a)), y_rail + RB + RB * math.sin(math.radians(a))) for a in (180, 210, 240, 270)]
        path += [(xb - RB + RB * math.cos(math.radians(a)), y_rail + RB + RB * math.sin(math.radians(a))) for a in (270, 300, 330, 360)]
        path += [(xb, y_in)]
        p.add('foot rail', rail_tube(path, RAIL_Z, RAIL_R, sides=10), steel)
        for xe in (xa, xb):
            p.add('rail flange', disc_y(xe, RAIL_Z, y_bf - 0.012, y_bf + 0.002, 0.040, sides=10), steel)
        for xm in brackets:
            p.add('rail bracket flange', disc_y(xm, RAIL_Z, y_bf - 0.012, y_bf + 0.002, 0.036, sides=10), steel)
            p.add('rail bracket arm', rod((xm, y_bf - 0.010, RAIL_Z), (xm, y_rail, RAIL_Z), 0.011, sides=8), steel)
            p.add('rail bracket saddle', fb.cylinder((xm, y_rail, RAIL_Z), RAIL_R + 0.005, 0.040, sides=10, rot=(0, 90, 0)), steel)

    # the boards wrap round the half-round by arc length (u continues from the straight front, round the end, and on
    # along Ace's side), so CF_Boards' 9 cm boards stay 9 cm all the way round instead of jumping at 45 degrees
    def boards_uv(bm, uv):
        for f in bm.faces:
            if abs(f.normal.z) > 0.5:
                continue
            mat = p.mats[f.material_index].name
            r_layer = {'CC_Wood_Espresso': R_BODY, 'CC_Dark_Kick': R_KICK, 'CF_Stone': R_TOP}.get(mat)
            if r_layer is None:
                continue
            c = f.calc_center_median()
            for loop in f.loops:
                co = loop.vert.co
                if c.x > x_c + 1e-4:
                    th = math.atan2(co.y, co.x - x_c) + math.pi / 2
                    u = x_c + math.hypot(co.x - x_c, co.y) * th
                elif f.normal.y < -0.5:
                    u = co.x
                elif f.normal.y > 0.5:
                    u = 2 * x_c + math.pi * r_layer - co.x
                else:
                    u = co.y
                loop[uv].uv = (u, co.z)
    p.uv_rules.append(boards_uv)
    FC.update(dict(top=TOP1, low_top=LOW1, x=(x_w, x_c + R_TOP), opening=(x_r0, x_r1), ledge_back=y_lt,
                   lowered=(x_l0, x_l1), rail_z=RAIL_Z, rail_y=y_rail, shelves=((x_a0, x_a1), (x_b0, x_b1)), shelf_h=(REC0 + 0.02, REC1)))
    return p


# ============================================================================= 2. the repair bench

RB_OX, RB_OZ = PLACEMENT['CK_RepairBench'][0], PLACEMENT['CK_RepairBench'][2]
RB = {}


def repair_bench():
    """
    The bench in the repair bar's opening. Frame: x = world x + 4.20, y = world z - 14.055 (front -Y = the customers).
    Top: chestnut, 7 cm, at 1.005 exactly (the close-up and the stage are tuned to it), 3 mm clear of the counter's
    opening walls each side. The watchmaker's references (Bergeon: 95-105 cm high, 75 cm deep; H.S. Walsh HB265: 140 x
    65 x 4 cm top, a drawer stack each side of the knee space) at this bench's 3.2 m.
    """
    p = BarProp('CK_RepairBench')
    X = lambda w: w - RB_OX          # noqa: E731
    Y = lambda w: w - RB_OZ          # noqa: E731
    chest, wood, kick, brass, steel, dark = M('CC_Wood_Counter'), M('CC_Wood_Espresso'), M('CC_Dark_Kick'), M('T2_Brass'), M('DC_Steel'), M('DC_SteelDark')
    HX = 1.597
    yf, yb = Y(13.66), Y(14.45)                     # -0.395, +0.395
    T0, T1 = 0.935, BENCH_TOP_Y
    p.add('work top (chestnut)', rbox(-HX, HX, yf, yb, T0, T1, b=0.006), chest)

    # --- two pedestals of five graduated drawers (shallow on top for small tools), 0.45 wide, at the ends
    PW, PY0, PY1 = 0.45, -0.375, 0.345             # pedestal width; carcass from the modesty panel's line to the drawers
    DF0, DF1 = PY1 - 0.001, PY1 + 0.020            # drawer fronts, 2 cm
    heights = [0.13, 0.16, 0.19, 0.24, 0.28]       # top drawer first: shallow on top (small tools), deep at the bottom
    gap = 0.006
    avail = (T0 - 0.10) - 6 * gap
    hs = [h / sum(heights) * avail for h in heights]
    for side in (-1, 1):
        xo = side * (HX - 0.004)
        xi = side * (HX - 0.004 - PW)
        x0, x1 = min(xo, xi), max(xo, xi)
        p.add('pedestal carcass', fb.zbox(x0, x1, PY0, PY1, 0.10, T0 + 0.002), wood)
        p.add('pedestal kick', fb.zbox(x0 + 0.01, x1 - 0.01, PY0 + 0.01, PY1 - 0.07, 0.0, 0.10), kick)
        z = T0 - gap
        for h in hs:
            z0 = z - h
            p.add('drawer front', rbox(x0 + 0.004, x1 - 0.004, DF0, DF1, z0, z, b=0.003), wood)
            bar_pull(p, ((x0 + x1) / 2, DF1, (z0 + z) / 2), 'x', 0.10, 0.026, r=0.0055, facing=1)
            z = z0 - gap
    xi_w, xi_e = -(HX - 0.004 - PW), (HX - 0.004 - PW)

    # --- the modesty panel (customer side, stiffens the 2.3 m span and closes the knee space) and the apron (Ace's side,
    #     set back 8 cm so the lamp's clamp has a throat under the edge; it carries the power strip)
    p.add('modesty panel', fb.zbox(xi_w, xi_e, PY0, PY0 + 0.02, 0.25, T0 + 0.002), wood)
    AY0, AY1 = 0.295, 0.315
    p.add('apron', fb.zbox(xi_w, xi_e, AY0, AY1, 0.86, T0 + 0.002), wood)

    # --- the bench power strip under the edge, in front of the work zone: a dark steel body, six socket faces, a lit
    #     switch (purpose: this is an electronics bench; the iron, the charger and the lamp plug in here)
    sx0, sx1 = X(-5.10), X(-4.65)
    SZ0, SZ1 = 0.872, 0.918
    p.add('power strip', rbox(sx0, sx1, AY1 - 0.002, AY1 + 0.036, SZ0, SZ1, b=0.003), dark)
    for k in range(6):
        cx = sx0 + 0.045 + k * 0.058
        p.add('socket', fb.zbox(cx - 0.0145, cx + 0.0145, AY1 + 0.033, AY1 + 0.0375, SZ0 + 0.008, SZ1 - 0.008), M('DC_Ceramic'))
    p.add('switch (lit)', fb.zbox(sx1 - 0.032, sx1 - 0.014, AY1 + 0.033, AY1 + 0.039, SZ0 + 0.012, SZ1 - 0.012), M('CF_LightStrip'))

    # --- the low glass screen on the customer edge: a steel shoe on the top, three 10 mm panels standing in it (their
    #     visible part 1.095-1.395, 30 cm above the ledge), four posts behind them, a clamp at each post top and middle
    shoe0, shoe1 = yf + 0.002, yf + 0.027
    p.add('glass shoe', rbox(-HX + 0.012, HX - 0.012, shoe0, shoe1, T1 - 0.002, T1 + 0.020, b=0.002), steel)
    gy = (shoe0 + shoe1) / 2
    GZ0, GZ1 = T1 + 0.006, 1.395
    posts = [X(w) for w in SCREEN_POSTS_WORLD]
    joints = posts[1:-1]
    cuts = [-HX + 0.015] + [c for j in joints for c in (j - 0.0025, j + 0.0025)] + [HX - 0.015]
    edges = [(cuts[k], cuts[k + 1]) for k in range(0, len(cuts), 2)]
    for e0, e1 in edges:
        p.add('glass panel', fb.zbox(e0, e1, gy - 0.005, gy + 0.005, GZ0, GZ1), M('CF_Glass'))
    py = shoe1 + 0.0125
    for x in posts:
        p.add('screen post', fb.cylinder((x, py, (T1 - 0.002 + 1.42) / 2), 0.011, 1.42 - (T1 - 0.002), sides=8), steel)
        p.add('post cap', fb.cylinder((x, py, 1.425), 0.013, 0.010, sides=8), steel)
        for cz in (1.17, 1.36):
            p.add('glass clamp', rbox(x - 0.019, x + 0.019, gy - 0.009, py - 0.004, cz - 0.016, cz + 0.016, b=0.002), steel)
    RB.update(dict(top=T1, half_width=HX, y=(yf, yb), pedestals=((-HX + 0.004, xi_w), (xi_e, HX - 0.004)), knee=(xi_w, xi_e),
                   strip=(sx0, sx1), glass_y=gy, glass_z=(GZ0, GZ1), posts=posts, shoe=(shoe0, shoe1)))
    return p


# ============================================================================= 3. the back bar's base run

BB_OX = PLACEMENT['CK_BackBar'][0]


def back_bar():
    """
    One run, x -7.40 to +4.90 (world), back against the wall at y = 0, front at y -1.10. Stone top at 1.00 everywhere.
    Doors on a 0.60 m grid from the west wall: the grid lands on x -3.80 (the parts wall's end) and on +1.60 and +4.60
    (the dispenser bay), so the bay is exactly five doors; the last 0.30 m is one narrow door and the end panel.
    """
    p = BarProp('CK_BackBar')
    X = lambda w: w - BB_OX          # noqa: E731
    stone, wood, kick = M('CF_Stone'), M('CC_Wood_Espresso'), M('CC_Dark_Kick')
    xw, xe_top = X(-7.40), X(4.90)
    xe_panel = xe_top - 0.015                       # the top overhangs the end panel 1.5 cm
    TOP0, TOP1 = 0.92, 1.00
    YF, Y_DOOR, Y_CARC, Y_KICK = -1.10, -1.075, -1.055, -1.005
    p.add('stone top', bevel(prism_z([(xw, YF), (xe_top, YF), (xe_top, 0.0), (xw, 0.0)], TOP0, TOP1), 0.008), stone)
    p.add('end panel', rbox(xe_panel - 0.02, xe_panel, Y_DOOR, -0.002, 0.0, TOP0 + 0.002, b=0.004), wood)
    p.add('carcass (seen through the reveals)', fb.zbox(xw + 0.004, xe_panel - 0.02, Y_CARC, -0.002, 0.10, TOP0 + 0.002), kick)
    p.add('kick', fb.zbox(xw + 0.004, xe_panel - 0.02, Y_KICK, -0.002, 0.0, 0.10), kick)
    G = 0.006
    DZ0, DZ1 = 0.106, 0.896
    doors = []
    for k in range(20):
        doors.append((X(-7.40 + 0.6 * k), X(-7.40 + 0.6 * (k + 1))))
    doors.append((X(4.60), xe_panel - 0.02))
    for k, (a, b) in enumerate(doors):
        d0, d1 = a + G / 2, b - G / 2
        p.add('door', rbox(d0, d1, Y_DOOR, Y_CARC + 0.001, DZ0, DZ1, b=0.003), wood)
        # pulls at the meeting stiles: pairs open from the middle; the narrow end door from its west edge
        if k == 20 or k % 2 == 1:
            px = d0 + 0.045
        else:
            px = d1 - 0.045
        bar_pull(p, (px, Y_DOOR, DZ1 - 0.12), 'z', 0.16, 0.030, r=0.0065)
    return p


# ============================================================================= 4. the long shelf

SH_OX = PLACEMENT['CK_BackBarShelf'][0]
SHELF_BRACKETS_WORLD = [-3.6, -2.4, -1.2, 0.0, 1.2, 2.4, 3.6, 4.65]    # symmetric about the till (x 0), 1.2 m apart


def back_bar_shelf():
    """
    The one line that turns the wall from random to designed (research 6.4). Origin at the bottom centre of its back,
    which is the shelf board's underside at 1.75; the brackets hang below it on the tile's face (the backsplash is 2 cm
    proud of the wall from 1.00 to 1.75), so the piece's lowest point is the brackets' foot.
    """
    p = BarProp('CK_BackBarShelf', floor=False)
    X = lambda w: w - SH_OX          # noqa: E731
    chest, steel = M('CC_Wood_Counter'), M('DC_Steel')
    x0, x1 = X(-3.80), X(4.90)
    p.add('shelf (chestnut)', rbox(x0, x1, -0.30, 0.0, 0.0, 0.06, b=0.005), chest)
    # warm-steel L brackets after the 12" heavy-duty L brackets (Knape & Vogt 208: 12" x 8"), 40 x 6 mm flat bar:
    # an arm under the board, a leg down the tile, a brace between them
    YT = -0.020                                       # the tile's face
    for wx in SHELF_BRACKETS_WORLD:
        bx = X(wx)
        p.add('bracket arm', fb.zbox(bx - 0.02, bx + 0.02, -0.255, YT, -0.006, 0.002), steel)
        p.add('bracket leg', fb.zbox(bx - 0.02, bx + 0.02, YT - 0.006, YT, -0.20, -0.006), steel)
        p.add('bracket brace', bar_between((bx, YT - 0.004, -0.185), (bx, -0.185, -0.004), (1, 0, 0), 0.034, 0.006), steel)
    # the light channel under the front edge, and the strip in it (emissive in Unity; one warm light every 1.2 m grazes
    # the tile: the lights are Unity's)
    p.add('light channel', fb.zbox(x0 + 0.02, x1 - 0.02, -0.287, -0.265, -0.014, 0.002), steel)
    p.add('light strip', fb.zbox(x0 + 0.03, x1 - 0.03, -0.283, -0.269, -0.017, -0.012), M('CF_LightStrip'))
    return p


# ============================================================================= 5. the backsplash

BS_OX = PLACEMENT['CK_Backsplash'][0]


def backsplash():
    """The tile panel from the top (1.00) to the shelf (1.75), 2 mm into each; the tile is texture. Origin at the
    bottom centre of its back on the stone (1.00)."""
    p = BarProp('CK_Backsplash', floor=False)
    X = lambda w: w - BS_OX          # noqa: E731
    x0, x1 = X(-3.80), X(4.90)
    p.add('tile panel', fb.zbox(x0, x1, -0.020, 0.0, -0.002, 0.752), M('CF_Tile'))
    # its open (east) end gets a metal edge trim, as tile ends do (a Schluter-type profile)
    p.add('edge trim', fb.zbox(x1, x1 + 0.006, -0.022, 0.0, -0.001, 0.751), M('DC_Steel'))
    return p


# ============================================================================= 6. the parts wall

PW_OX = PLACEMENT['CK_PartsWall'][0]
PARTS_COLS, PARTS_ROWS = 18, 4
CARD_W, CARD_H = 0.073, 0.032          # the card a 3-1/8" x 1-1/2" label holder takes: 2-7/8" x 1-1/4" (aspect 2.28)
PLATE_W, PLATE_H = 0.080, 0.038
PW = {}


def parts_wall():
    """
    72 drawers, 4 rows x 18, on the back bar's top under the shelf line. Origin at the bottom centre of its back on the
    stone (1.00); 2 mm into the stone. Each drawer: a front 1.3 cm proud of a dark reveal panel (6 mm reveals), a brass
    label holder with pull (one stamped part: a frame, the card behind it, a lip under it to pull by) and the card,
    whose face carries UVs into CF_DrawerLabels: cell (col, row), u col/18..(col+1)/18, v 1-(row+1)/4..1-row/4; row 0 the
    top row; col 0 the room's left (west: Blender -x here; Unity local +x inside the FBX; world -x once turned 180).
    """
    p = BarProp('CK_PartsWall', floor=False)
    wood, kick, brass = M('CC_Wood_Espresso'), M('CC_Dark_Kick'), M('T2_Brass')
    W, D, H = 3.60, 0.30, 0.75
    B = 0.025                                   # the carcass's face frame: stiles and rails
    Y_CF = -0.285                               # the carcass's front
    p.add('carcass', rbox(-W / 2, W / 2, Y_CF, -0.001, -0.002, H, b=0.004), wood)
    p.add('reveal panel', fb.zbox(-W / 2 + B, W / 2 - B, Y_CF - 0.003, Y_CF + 0.002, B, H - B), kick)
    y_df0 = -D                                  # the drawer fronts' faces at -0.30
    y_df1 = Y_CF - 0.002                        # 1 mm into the reveal panel
    pc = (W - 2 * B) / PARTS_COLS
    pr = (H - 2 * B) / PARTS_ROWS
    g = 0.006
    cards = []
    cells = {}
    for row in range(PARTS_ROWS):
        zc = H - B - pr * (row + 0.5)
        for col in range(PARTS_COLS):
            xc = -W / 2 + B + pc * (col + 0.5)
            p.add('drawer front', front_box(xc - pc / 2 + g / 2, xc + pc / 2 - g / 2, y_df0, y_df1, zc - pr / 2 + g / 2, zc + pr / 2 - g / 2), wood)
            # the label holder sits in the drawer's upper middle, as on card-catalogue drawers
            pz = zc + 0.012
            p.add('label holder', front_box(xc - PLATE_W / 2, xc + PLATE_W / 2, y_df0 - 0.0015, y_df0 + 0.0005, pz - PLATE_H / 2, pz + PLATE_H / 2), brass)
            zb = pz - PLATE_H / 2
            lip = [(y_df0 + 0.0005, zb + 0.002), (y_df0 - 0.0185, zb - 0.006), (y_df0 + 0.0005, zb - 0.013)]
            p.add('pull lip', prism_x(lip, xc - PLATE_W / 2 + 0.006, xc + PLATE_W / 2 - 0.006), brass)
            faces = p.add('label card', card_quad(xc - CARD_W / 2, xc + CARD_W / 2, y_df0 - 0.002, pz - CARD_H / 2 + 0.001, pz + CARD_H / 2 + 0.001), M('CF_DrawerLabels'))
            cards.append((faces, col, row))
            cells[(col, row)] = (xc, pz)
    p.must_face.append(([f for fs, _, _ in cards for f in fs], Vector((0, -1, 0))))

    def atlas_uv(bm, uv):
        for faces, col, row in cards:
            u0, u1 = col / PARTS_COLS, (col + 1) / PARTS_COLS
            v0, v1 = 1.0 - (row + 1) / PARTS_ROWS, 1.0 - row / PARTS_ROWS
            for f in faces:
                xs = [l.vert.co.x for l in f.loops]
                zs = [l.vert.co.z for l in f.loops]
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv].uv = (u0 + (u1 - u0) * (co.x - min(xs)) / (max(xs) - min(xs)),
                                   v0 + (v1 - v0) * (co.z - min(zs)) / (max(zs) - min(zs)))
    p.uv_rules.append(atlas_uv)
    PW.update(dict(cells=cells, pitch=(pc, pr), front=(pc - g, pr - g), card=(CARD_W, CARD_H)))
    return p


# ============================================================================= 7. the bench lamp

LAMP = {}


def bench_lamp():
    """
    A spring-balanced magnifier lamp after the Luxo LFM (a 127 mm 5-diopter glass lens in a full ring of LEDs, a 30"
    internal-spring arm, an edge clamp) and the classic external-spring magnifiers: here the springs show, because
    they say how the arm holds itself up. Origin at the clamp's bottom (the screw knob's underside) on the post's axis;
    the frame turned like the bench (Blender +X world east, +Y world north). The pose is computed: the head's centre over
    LAMP_TARGET_WORLD at LAMP_HEAD_Y, its lens level so the light falls straight down.
    """
    p = BarProp('CK_BenchLamp', floor=False)
    steel, dark, enamel = M('DC_Steel'), M('DC_SteelDark'), M('CF_Enamel')
    # the bench top in this frame: its surface 0.155 above the origin, 7 cm thick, Ace's edge 4 cm behind the post
    HT = 0.155
    TU = HT - 0.07
    EDGE = 0.04
    origin_y = BENCH_TOP_Y - HT
    # --- the clamp (C-frame): a pad on the top, the spine outside the edge, the lower jaw, the screw, its knob
    p.add('clamp knob', fb.cylinder((0, 0, 0.010), 0.018, 0.020, sides=8), dark)
    p.add('clamp screw', fb.cylinder((0, 0, (0.018 + TU - 0.004) / 2), 0.005, TU - 0.004 - 0.018, sides=6), steel)
    p.add('clamp pad', fb.cylinder((0, 0, TU - 0.003), 0.015, 0.006, sides=8), dark)
    p.add('clamp lower jaw', fb.zbox(-0.025, 0.025, -0.022, EDGE + 0.022, 0.035, 0.050), steel)
    p.add('clamp spine', fb.zbox(-0.021, 0.021, EDGE + 0.004, EDGE + 0.019, 0.038, HT + 0.011), steel)
    p.add('clamp top jaw', fb.zbox(-0.025, 0.025, -0.030, EDGE + 0.022, HT - 0.001, HT + 0.014), steel)
    # --- the post in its socket
    POST_TOP = HT + 0.014 + 0.095
    p.add('post socket', fb.cylinder((0, 0, HT + 0.014 + 0.012), 0.016, 0.026, sides=8), steel)
    p.add('post', fb.cylinder((0, 0, (HT + 0.012 + POST_TOP) / 2), 0.011, POST_TOP - HT - 0.012, sides=8), steel)

    # --- the pose: the head's centre, then the arms by two-link inverse kinematics (elbow up)
    tx, tz = LAMP_TARGET_WORLD[0] - LAMP_POST_WORLD[0], LAMP_TARGET_WORLD[1] - LAMP_POST_WORLD[1]
    head_c = Vector((tx, tz, LAMP_HEAD_Y - origin_y))
    plan = Vector((tx, tz, 0.0))
    reach = plan.length
    dirp = plan.normalized()
    side = Vector((-dirp.y, dirp.x, 0.0))          # horizontal, square to the arm's plane
    L1, L2 = 0.48, 0.30                             # 0.78 m of arm: the LFM's 30" class
    R_HEAD = 0.09
    p0 = Vector((0, 0, POST_TOP + 0.016))           # the shoulder pivot
    h0 = head_c - dirp * (R_HEAD + 0.025) + Vector((0, 0, 0.035))   # the head's knuckle, off its rim, a little above
    rel = h0 - p0
    rh = Vector((rel.x, rel.y, 0)).length
    dz = rel.z
    D = math.hypot(rh, dz)
    gamma = math.acos(max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2 * L1 * D))))
    alpha = math.atan2(dz, rh) + gamma
    elbow = p0 + dirp * (L1 * math.cos(alpha)) + Vector((0, 0, L1 * math.sin(alpha)))
    # the shoulder: a yoke on the post top, its pivot pin across the arm's plane
    p.add('shoulder yoke', oriented_box(p0 - Vector((0, 0, 0.006)), dirp, side, Vector((0, 0, 1)), 0.016, 0.022, 0.014), steel)
    p.add('shoulder pin', rod(p0 - side * 0.03, p0 + side * 0.03, 0.007, sides=6), dark)

    def arm(a, b, name, spring_side, spring_from, spring_to, shift=0.0):
        """Two parallel rods 28 mm apart in the arm's plane (the parallelogram that keeps the head level), and the
        spring along the outside, anchored to the rods. `shift` moves the pair along the pivot (the upper arm's rods
        sit beside the lower arm's at the elbow, as on the real joint)."""
        d = (b - a).normalized()
        n = d.cross(side).normalized()             # in the arm's plane, square to the arm
        if n.z < 0:
            n = -n
        for off in (-0.014, 0.014):
            p.add(name + ' rod', rod(a + n * off + side * shift, b + n * off + side * shift, 0.0055, sides=6), steel)
        s0 = a + (b - a) * spring_from + side * (spring_side * 0.018)
        s1 = a + (b - a) * spring_to + side * (spring_side * 0.018)
        p.add(name + ' spring', ribbed(s0, s1, 0.0068, 0.0046, 6, sides=5), dark)
        for s in (s0, s1):
            p.add(name + ' spring anchor', rod(s, s - side * (spring_side * 0.020), 0.0035, sides=3), dark)
        return n

    arm(p0, elbow, 'lower arm', 1, 0.10, 0.55)
    arm(elbow, h0, 'upper arm', -1, 0.12, 0.60, shift=0.004)
    # the elbow: a knuckle across the plane, its tension knob on the outside
    p.add('elbow knuckle', rod(elbow - side * 0.022, elbow + side * 0.022, 0.014, sides=8), steel)
    p.add('elbow knob', rod(elbow + side * 0.020, elbow + side * 0.036, 0.017, sides=8), dark)
    # the head's knuckle and the yoke down to the head's rim
    p.add('head knuckle', rod(h0 - side * 0.018, h0 + side * 0.018, 0.011, sides=6), steel)
    rim = head_c - dirp * R_HEAD
    p.add('head yoke', bar_between(h0, rim + Vector((0, 0, 0.012)) - dirp * 0.004, side, 0.026, 0.008), steel)
    # --- the head: the cream enamel housing ring, the lens, the LED ring under it, the handle opposite the yoke
    HZ0, HZ1 = head_c.z - 0.019, head_c.z + 0.019
    p.add('head housing', ring_z(head_c.x, head_c.y, HZ0, HZ1, 0.0655, R_HEAD, sides=16), enamel)
    p.add('lens (5 dioptre, 127 mm)', fb.cylinder((head_c.x, head_c.y, head_c.z - 0.003), 0.0660, 0.006, sides=16), M('CF_Glass'))
    p.add('LED ring', ring_z(head_c.x, head_c.y, HZ0 - 0.006, HZ0 + 0.002, 0.068, 0.084, sides=12), M('CF_LightStrip'))
    hstart = head_c + dirp * (R_HEAD - 0.004)
    hend = head_c + dirp * (R_HEAD + 0.065) + Vector((0, 0, 0.012))
    p.add('head handle', rod(hstart, hend, 0.007, sides=8), dark)

    light_pos = head_c - Vector((0, 0, 0.025))
    LAMP.update(dict(origin_world=(LAMP_POST_WORLD[0], origin_y, LAMP_POST_WORLD[1]), head_centre=tuple(head_c),
                     light=tuple(light_pos), reach=reach, alpha=math.degrees(alpha), elbow=tuple(elbow), h0=tuple(h0),
                     upper_angle=math.degrees(math.atan2(h0.z - elbow.z, Vector((h0.x - elbow.x, h0.y - elbow.y, 0)).length))))
    PLACEMENT['CK_BenchLamp'] = (LAMP_POST_WORLD[0], origin_y, LAMP_POST_WORLD[1])
    return p


# ============================================================================= the list

PIECES = [
    (front_counter, 'Bar', 'the front counter: one piece, stone top 1.095, half-round end, lowered section, repair bar ledge, foot rail'),
    (repair_bench, 'Bar', 'the repair bench: chestnut top 1.005, two 5-drawer pedestals, modesty panel, power strip, low glass screen'),
    (back_bar, 'Bar', 'the back bar base run: stone top 1.00, 21 doors with 6 mm reveals, kick, end panel (wall piece)'),
    (back_bar_shelf, 'Bar', 'the long chestnut shelf, bottom at 1.75, steel brackets every 1.2 m, light channel and strip (wall piece)'),
    (backsplash, 'Bar', 'the tile backsplash 1.00-1.75, 2 cm proud, edge trim at the east end (wall piece)'),
    (parts_wall, 'Bar', 'the parts wall: 72 labelled drawers, 4 x 18, label cards UV-mapped to CF_DrawerLabels (wall piece)'),
    (bench_lamp, 'Bar', 'the bench magnifier lamp: clamp, post, two arms with springs, 5-dioptre lens head with LED ring'),
]


def build_all(collection=None):
    out = []
    for builder, group, what in PIECES:
        prop = builder()
        ob = prop.finish(collection)
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'group': group, 'what': what, 'islands': n, 'floating': floating,
                    'turned': prop.turned, 'flipped_cards': getattr(prop, 'flipped_cards', 0), **st})
    return out


# ============================================================================= checks beyond the house audit


def _clip(subject, clip):
    """Sutherland-Hodgman: the part of a convex 2D polygon inside another (both anticlockwise)."""
    out = list(subject)
    n = len(clip)
    for i in range(n):
        a, b = clip[i], clip[(i + 1) % n]
        inp, out = out, []
        if not inp:
            break

        def inside(p):
            return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= -1e-12

        def cut(p, q):
            x1, y1, x2, y2 = p[0], p[1], q[0], q[1]
            x3, y3, x4, y4 = a[0], a[1], b[0], b[1]
            den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
            if abs(den) < 1e-18:
                return q
            t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
            return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))
        for k in range(len(inp)):
            p, q = inp[k], inp[(k + 1) % len(inp)]
            if inside(q):
                if not inside(p):
                    out.append(cut(p, q))
                out.append(q)
            elif inside(p):
                out.append(cut(p, q))
    return out


def _area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))


def coplanar_overlaps(ob, tol_d=0.0006, tol_area=1e-6):
    """Pairs of faces facing the same way, in the same plane, whose areas really overlap (what z-fights in Unity),
    by exact clipping of the (convex) faces. Returns (face, face, overlap in mm2, centre)."""
    me = ob.data
    groups = {}
    for poly in me.polygons:
        n = poly.normal
        key = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(poly.center) / tol_d))
        groups.setdefault(key, []).append(poly.index)
    hits = []
    verts = me.vertices
    for key, ids in groups.items():
        if len(ids) < 2:
            continue
        n = Vector(key[:3]).normalized()
        a = n.orthogonal().normalized()
        b = n.cross(a).normalized()
        polys = []
        for i in ids:
            pts = [(verts[v].co.dot(a), verts[v].co.dot(b)) for v in me.polygons[i].vertices]
            if _area(pts) < 0:
                pts = pts[::-1]
            us, vs = [q[0] for q in pts], [q[1] for q in pts]
            polys.append((i, pts, (min(us), max(us), min(vs), max(vs))))
        for ii in range(len(polys)):
            i, pi, bi = polys[ii]
            for jj in range(ii + 1, len(polys)):
                j, pj, bj = polys[jj]
                if bi[1] <= bj[0] or bj[1] <= bi[0] or bi[3] <= bj[2] or bj[3] <= bi[2]:
                    continue
                inter = _clip(pi, pj)
                if len(inter) >= 3:
                    ar = abs(_area(inter))
                    if ar > tol_area:
                        hits.append((i, j, round(ar * 1e6, 1), tuple(round(c, 3) for c in me.polygons[i].center)))
    return hits


def top_heights(ob, mat_name):
    """The heights of the upward faces of one material (the working surfaces)."""
    me = ob.data
    zs = {}
    for poly in me.polygons:
        if poly.normal.z > 0.99 and me.materials[poly.material_index].name == mat_name:
            z = round(poly.center.z, 4)
            zs[z] = zs.get(z, 0.0) + poly.area
    return sorted(zs.items(), key=lambda kv: -kv[1])


def to_world(name, b):
    """A point of a piece (its Blender frame) in Unity world coordinates (the piece turned 180 about Y)."""
    px, py, pz = PLACEMENT[name]
    return (round(px + b[0], 4), round(py + b[2], 4), round(pz + b[1], 4))


def to_unity_local(b):
    """A point of a piece (its Blender frame) in the FBX's own Unity frame (before the 180 degree turn)."""
    return (round(-b[0], 4), round(b[2], 4), round(-b[1], 4))
