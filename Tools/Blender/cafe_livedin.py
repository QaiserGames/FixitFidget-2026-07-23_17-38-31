"""
The lived-in café, v1 (12-13 Oct 2026; playtest-4 findings §8, his words: "I do wish to also add more decorations to the
cafe itself, like carpets, rugs, some napkins, trash cans, people on their laptops"; his answer: "i trust your
judgement"). Thirteen small pieces that say people use this room, built with Python in Blender 5.2 on fixit_blender.py,
hero_lib.py and cafe_props.py's helpers, the house way:

  * REAL LIFE FIRST. Every piece starts from a measured reference (REFERENCES below, with links); the house style
    simplifies it, and every part that does none of the four jobs (the player works it; it tells how the thing works;
    it makes the silhouette; it is this world's texture) is cut. The cut list is in each builder's docstring.
  * THE HOUSE STYLE. Flat-shaded, one-segment chamfers, metallic 0, warm greys, junctions from boundaries with 1-4 mm
    overlaps, no two faces in one plane, nothing below the floor, nothing floating (fb.audit). Soft and paper things
    the game already shows smooth (the café's own paper cup) are smooth inside their seams (hero_lib's 42 degree rule).
  * FEWER AND CALMER. The redesign (10 Oct) took four loud floor pieces out on purpose; the two rugs that come back are
    a calm flatweave and a plain coir mat, where real cafés have them. Colour belongs to the city's three bins (a quote
    of San Francisco), a child's drawing and nothing else; green stays Ace's (the compost green is a grass green, not
    the brand's teal).

  LI_Rug_Corner        a flatweave for the reading corner, 1.60 x 1.10 x 0.010, the pattern in LI_RugCorner.png
  LI_Rug_Entry         a plain coir mat on a PVC back, 1.20 x 0.70 x 0.017 (LI_CoirMat.png)
  LI_TillMat           the till's black anti-fatigue mat, 36 x 24 x 1/2 in, bevelled all round (LI_RubberMat.png)
  LI_NapkinDispenser   a chrome two-faced tabletop dispenser, rounded, the napkin stack in its windows (24 Oct: a48)
  LI_CondimentStation  the pickup end's self-serve tray (24 Oct, a37: every stack made of its things, and a labelled
                       cinnamon shaker): napkins, lids, sleeves, a sugar caddy, a cup of stirrers, a
                       small milk jug, on chestnut
  LI_BinStation        San Francisco's three streams in one slim body: blue RECYCLING, green COMPOST, black LANDFILL,
                       three openings, three labels (LI_BinLabels.png)
  LI_StreetBin         the city's green round litter can: grillwork bars round a dark liner, a dome lid, a hooded mouth
  LI_Laptop_Open       a plain 14 in laptop, no brand, the screen at 110 degrees and on (24 Oct, a50: LI_Screen.png)
  LI_Laptop_Closed     the same laptop shut
  LI_ToteBag           a canvas tote, slumped, its handles flopped (24 Oct, a49: soft, a flat base, the mouth folding)
  LI_Coat_OnChair      a coat hung by its shoulders over a café chair's top rail (fits both CK_Chair_Cafe and
                       CK_Chair_Spindle: origin = the chair's origin)
  LI_KidsDrawing       a child's crayon thank-you drawing of the café on US letter, pinned (LI_KidsDrawing.png)
  LI_LeftCup           the café's own paper cup (DrinkLiquidVisual's), left behind with a black sip lid on

Conventions (the kit's): 1 unit = 1 m, Z up, the front faces -Y (Unity +Z); origin at the bottom centre (a wall piece:
the bottom centre of its back, the back at y = 0); Unity (x, y, z) = Blender (-x, z, -y). UVs: fixit_blender's box UVs
in metres everywhere (the café's grain and flat colours ignore or tile them), except the textured face sets, mapped
0..1 by their own axes (u to the reader's right, v up), their edges sent to one border texel (cafe_signs' way).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_TOOLS', ''), '/home/claude/cafe/s9/src/Tools/Blender'):
    if _p and os.path.exists(os.path.join(_p, 'fixit_blender.py')) and _p not in sys.path:
        sys.path.append(_p)
import bpy  # noqa: E402,F401  (first: bmesh and mathutils come with it)
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import cafe_props as cp  # noqa: E402  (its spin and sweep_var; imported, never edited)
from mathutils import Matrix, Vector  # noqa: E402

TEX = 'Assets/Art/Textures/Cafe/'

# ============================================================================================ materials
# (sRGB hex, Unity smoothness, why / where it lives). Metallic 0 everywhere (the house rule).

EXISTING = {   # the project's own, used by their exact names; Unity keeps its own values
    'CC_Wood_Counter': ('#8F5B32', 0.71, 'chestnut, the room\'s one wood: the condiment tray (Kit 1 maps it to "Cafe grain - CC_Wood_Counter")'),
    'DC_Steel':        ('#93908A', 0.50, 'warm steel: the napkin dispenser, the milk jug, the laptop, the bin station\'s body (Assets/Art/Materials)'),
    'DC_SteelDark':    ('#4E4B47', 0.50, 'dark warm steel: the laptop\'s trackpad (Assets/Art/Materials)'),
    'DC_Chalk':        ('#E8E4D8', 0.05, 'matte off-white: paper napkins, white sugar packets, the tote\'s natural canvas (Assets/Art/Materials)'),
    'DC_Ink':          ('#4A4E5A', 0.05, 'a matte slate blue: the coat\'s cloth (Assets/Art/Materials)'),
    'DC_Fabric':       ('#A8957C', 0.12, 'oat: the coat\'s corduroy collar, the jumper in the tote (Assets/Art/Materials)'),
    'CF_Black':        ('#2B2724', 0.35, 'black plastic and powder coat: lids, the sugar caddy, the keyboard and hinge, the bins\' liners, the coir mat\'s PVC back (Assets/Art/Materials/Cafe, made by Kit 1 from cafe_props.json)'),
    'CF_Kraft':        ('#B08A5E', 0.15, 'kraft: cup sleeves, raw-sugar packets (Assets/Art/Materials/Cafe, from cafe_props.json)'),
    'CF_PinYellow':    ('#D9A93A', 0.60, 'the drawing\'s push pin, the notice board\'s own pins (Assets/Art/Materials/Cafe, from cafe_signs.json)'),
}
LI_MATERIALS = {   # new, LI_ prefix (the lived-in kit's), each with its reason
    'LI_RugCorner':   ('#C9B89B', 0.08, 'NEW, textured: the flatweave\'s top (oat, rust, charcoal; no existing rug material is in the root folder the kit importer searches, and the house kit\'s HK_Rug_* are flat colours)', 'LI_RugCorner.png'),
    'LI_CoirMat':     ('#A07545', 0.05, 'NEW, textured: coir bristles (HK_Coir is a flat colour in a sub-folder the importer does not search)', 'LI_CoirMat.png'),
    'LI_RubberMat':   ('#2B2927', 0.22, 'NEW, textured: black rubber and its drainage holes, smoothness as the bench and drink mats\' "Mat - rubber" (0.22), which lives in Assets/Art/CafeFurnishing and cannot be mapped by name', 'LI_RubberMat.png'),
    'LI_BinLabels':   ('#2C67A3', 0.35, 'NEW, textured: the three stream labels, vinyl stickers (satin)', 'LI_BinLabels.png'),
    'LI_KidsDrawing': ('#F7F5EF', 0.05, 'NEW, textured: printer paper and wax crayon', 'LI_KidsDrawing.png'),
    'LI_RecycleBlue': ('#2C67A3', 0.35, 'NEW: the recycling stream\'s blue (San Francisco\'s blue cart), moulded plastic', None),
    'LI_CompostGreen': ('#4F8F3A', 0.35, 'NEW: the compost stream\'s green, a grass green kept well apart from Ace\'s teal #2E7D5B', None),
    'LI_StreetGreen': ('#2E4636', 0.35, 'NEW: the city litter can\'s dark green powder coat (darker and greyer than Ace\'s green)', None),
    'LI_CupPaper':    ('#E8D9B3', 0.08, 'NEW: the café\'s paper cup, the exact colour the game draws it in (DrinkLiquidVisual.PaperColour 0.91, 0.85, 0.70, "Warm ivory paper", smoothness 0.08); also the birch stirrers', None),
    'LI_Screen':      ('#FFFFFF', 0.55, 'NEW, textured: the laptop\'s screen, on: a dark title bar over a light page (24 Oct 2026, '
                                        'art audit a50: the flat dark-glass panel at smoothness 0.85 mirrored the room as a pale '
                                        'blue slab and read as a dead screen; the picture is cafe_art_fix_textures.py\'s)', 'LI_Screen.png'),
    'LI_Chrome':      ('#CBC8C1', 0.80, 'NEW (24 Oct 2026, art audit a48): the napkin holder\'s polished body, a bright glossy '
                                        'steel (metallic 0, the house rule), so it reads as the diner chrome it is and not a grey box', None),
}
EMISSION = {'LI_Screen': '1C2126'}       # a faint glow over the picture (Lived in 1 adds it flat, x 1.6)
TEXTURED = {name: TEX + v[3] for name, v in LI_MATERIALS.items() if v[3]}


def M(name):
    hexstr, smooth = (EXISTING.get(name) or LI_MATERIALS[name])[:2]
    m = fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)
    return m


# ============================================================================================ the piece

class LivedPiece(fb.Prop):
    """
    fb.Prop, the house way, plus three things the lived-in pieces need:
      * smooth parts (hero_lib.Faceted's rule: an edge stays smooth only inside smooth parts and under 42 degrees);
      * open parts (a bin's liner, a drawing's single quad) that say which way they face (face_test), because
        recalculating the normals of an open sheet can turn it the wrong way;
      * textured face sets mapped 0..1 by their own axes (cafe_signs.SignProp's lettered faces): the faces looking
        along u x v (more than min_dot) get the picture, every other face of the part one border texel.
    """

    def __init__(self, name, floor=True):
        super().__init__(name, floor=floor)
        self.smooth_faces = set()
        self.tests = []
        self.uv_specs = []
        self.labels = {}

    def add(self, part_name, src_bm, mat, bevel=0.0, smooth=False, face_test=None):
        if bevel > 0:
            fb.bevel_all(src_bm, bevel, segments=1, angle=30.0)
            bmesh.ops.dissolve_degenerate(src_bm, dist=2e-6, edges=src_bm.edges[:])
        faces = super().add(part_name, src_bm, mat, smooth=smooth)
        if smooth:
            self.smooth_faces.update(faces)
        if face_test is not None:
            self.tests.append((faces, face_test))
        return faces

    def add_split(self, part_name, src_bm, mat_a, mat_b, pick_b, smooth=False):
        """One part in two materials: faces for which pick_b(face) is true get mat_b (a jug's milk, a cup's lid)."""
        bmesh.ops.recalc_face_normals(src_bm, faces=src_bm.faces[:])
        flags = [pick_b(f) for f in src_bm.faces]
        ia, ib = self.slot(mat_a), self.slot(mat_b)
        vmap = {v: self.bm.verts.new(v.co) for v in src_bm.verts}
        faces = []
        for f, b in zip(src_bm.faces, flags):
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = ib if b else ia
            nf.smooth = smooth
            faces.append(nf)
        src_bm.free()
        self.parts.append((part_name, faces))
        if smooth:
            self.smooth_faces.update(faces)
        return faces

    def textured(self, part_name, src_bm, mat, origin, u_axis, v_axis, w, h, rect=(0.0, 0.0, 1.0, 1.0),
                 edge=(0.5, 0.5), min_dot=0.25, face_test=None):
        faces = self.add(part_name, src_bm, mat, face_test=face_test)
        self.uv_specs.append((faces, Vector(origin), Vector(u_axis).normalized(), Vector(v_axis).normalized(), w, h,
                              rect, edge, min_dot, mat.name))
        return faces

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
        bm.normal_update()
        self.flipped_open = 0
        for faces, test in self.tests:
            live = [f for f in faces if f.is_valid]
            if live and not test(live):
                bmesh.ops.reverse_faces(bm, faces=live)
                self.flipped_open += 1
        bm.normal_update()
        # the 42 degree rule (hero_lib.Faceted's), once the normals are real: an edge is smooth only inside smooth
        # parts and where its faces meet at 42 degrees or less (a cup's wall smooth, its rim and base hard)
        limit = math.radians(hl.SMOOTH_ANGLE)
        for e in bm.edges:
            if not e.is_manifold:
                e.smooth = True
                continue
            fa, fb_ = e.link_faces
            e.smooth = fa in self.smooth_faces and fb_ in self.smooth_faces and e.calc_face_angle(0.0) <= limit
        fb.box_uvs(bm)
        uv = bm.loops.layers.uv.verify()
        for faces, o, U, V, w, h, rect, edge, min_dot, mname in self.uv_specs:
            n_dir = U.cross(V)
            u0, v0, u1, v1 = rect
            for f in faces:
                if not f.is_valid:
                    continue
                d = f.normal.dot(n_dir)
                for loop in f.loops:
                    if d > min_dot:
                        p = loop.vert.co - o
                        uu = min(1.0, max(0.0, p.dot(U) / w))
                        vv = min(1.0, max(0.0, p.dot(V) / h))
                    else:
                        uu, vv = edge
                    loop[uv].uv = (u0 + (u1 - u0) * uu, v0 + (v1 - v0) * vv)
                if d > 0.9:
                    self.labels[mname] = self.labels.get(mname, 0) + 1
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        ob['fixit_shells_turned'] = self.turned
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


def faces_up(faces):
    """An open part (a liner: walls and a floor) is the right way round when its floor looks up."""
    caps = [f for f in faces if abs(f.normal.z) > 0.9]
    return sum(f.normal.z * f.calc_area() for f in caps) > 0


def faces_toward(d):
    d = Vector(d)
    return lambda faces: sum(f.normal.dot(d) * f.calc_area() for f in faces) > 0


# ============================================================================================ geometry helpers

def rect_loop(cx, cy, z, sx, sy):
    hx, hy = sx / 2, sy / 2
    return [(cx + hx, cy - hy, z), (cx + hx, cy + hy, z), (cx - hx, cy + hy, z), (cx - hx, cy - hy, z)]


def slab(sx, sy, t, c, z0=0.0):
    """A floor slab sx x sy, t thick from z0, a one-segment chamfer c on its top edges (a rug, a mat)."""
    return hl.tube([rect_loop(0, 0, z0, sx, sy), rect_loop(0, 0, z0 + t - c, sx, sy),
                    rect_loop(0, 0, z0 + t, sx - 2 * c, sy - 2 * c)])


def circle(cx, cy, z, r, n, phase=0.0):
    return [(cx + r * math.cos(2 * math.pi * k / n + phase), cy + r * math.sin(2 * math.pi * k / n + phase), z)
            for k in range(n)]


def bridge(bm, outer, inner, centre, axes=(0, 1)):
    """
    Triangles across the ring between two closed loops of BMVerts (any counts, both counter-clockwise in `axes`),
    zipped by their angle round `centre`: the face of a panel with a hole in it, a frame round a window.
    """
    a0, a1 = axes

    def ang(v):
        a = math.atan2(v.co[a1] - centre[1], v.co[a0] - centre[0])
        return a if a >= 0 else a + 2 * math.pi

    def ordered(loop):
        k = min(range(len(loop)), key=lambda i: ang(loop[i]))
        lo = loop[k:] + loop[:k]
        angs = [ang(v) for v in lo]
        for i in range(1, len(angs)):
            while angs[i] < angs[i - 1]:
                angs[i] += 2 * math.pi
        return lo, angs
    o, ao = ordered(outer)
    n, an = ordered(inner)
    o, ao = o + [o[0]], ao + [ao[0] + 2 * math.pi]
    n, an = n + [n[0]], an + [an[0] + 2 * math.pi]
    i = j = 0
    faces = []
    while i < len(o) - 1 or j < len(n) - 1:
        if j >= len(n) - 1 or (i < len(o) - 1 and ao[i + 1] <= an[j + 1]):
            faces.append(bm.faces.new([o[i], o[i + 1], n[j]]))
            i += 1
        else:
            faces.append(bm.faces.new([o[i], n[j + 1], n[j]]))
            j += 1
    return faces


def panel_with_hole(x0, x1, y0, y1, zb, zt, hole, centre, c=0.008):
    """A box panel from (x0, y0, zb) to (x1, y1, zt), its top edges chamfered c, with a hole right through it whose outline
    at height z is hole(z) (counter-clockwise from above). A closed shell (a lid with an aperture)."""
    bm = bmesh.new()
    ob = [bm.verts.new(p) for p in rect_loop((x0 + x1) / 2, (y0 + y1) / 2, zb, x1 - x0, y1 - y0)]
    oc = [bm.verts.new(p) for p in rect_loop((x0 + x1) / 2, (y0 + y1) / 2, zt - c, x1 - x0, y1 - y0)]
    ot = [bm.verts.new(p) for p in rect_loop((x0 + x1) / 2, (y0 + y1) / 2, zt, x1 - x0 - 2 * c, y1 - y0 - 2 * c)]
    ht = [bm.verts.new(p) for p in hole(zt)]
    hb = [bm.verts.new(p) for p in hole(zb)]
    for lo, hi in ((ob, oc), (oc, ot)):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    n = len(ht)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([ht[i], ht[j], hb[j], hb[i]])
    bridge(bm, ot, ht, centre)
    bridge(bm, ob, hb, centre)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def liner(hole, z_top, z_bot):
    """The dark inside of an opening: an open tube down from z_top to a floor at z_bot (walls looking in, floor up)."""
    bm = bmesh.new()
    top = [bm.verts.new(p) for p in hole(z_top)]
    bot = [bm.verts.new(p) for p in hole(z_bot)]
    n = len(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([top[i], bot[i], bot[j], top[j]])
    bm.faces.new(bot)
    return bm


LENS8 = [(-1.0, 0.0), (-0.86, 0.75), (0.0, 1.0), (0.86, 0.75), (1.0, 0.0), (0.86, -0.75), (0.0, -1.0), (-0.86, -0.75)]
# denser on its inner side (sy < 0, the side that lies on something): a bowed rail under it is followed, not cut
LENS10 = [(-1.0, 0.0), (-0.8, 0.72), (0.0, 1.0), (0.8, 0.72), (1.0, 0.0), (0.8, -0.72), (0.4, -0.95), (0.0, -1.0),
          (-0.4, -0.95), (-0.8, -0.72)]


def ribbon(points, normals, widths, thicks, section=LENS8):
    """
    A soft flat band along a path (a draped coat): at each point a section `widths[i]` across the binormal and
    `thicks[i]` along the given normal, a lens with rounded edges (section: its points, x across, y along the normal),
    the ends closed.
    """
    pts = [Vector(p) for p in points]
    k = len(pts)
    n = len(section)
    bm = bmesh.new()
    rings = []
    for i in range(k):
        t = (pts[min(i + 1, k - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = Vector(normals[i])
        nrm = (nrm - t * nrm.dot(t)).normalized()
        b = t.cross(nrm).normalized()
        w, th = widths[i] / 2, thicks[i] / 2
        rings.append([bm.verts.new(pts[i] + b * (sx * w) + nrm * (sy * th)) for sx, sy in section])
    for ra, rb in zip(rings, rings[1:]):
        for s in range(n):
            q = (s + 1) % n
            bm.faces.new([ra[s], ra[q], rb[q], rb[s]])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def flat_strap(points, normals, width, thick):
    """A woven strap (a tote's handle): a thin rectangle swept along a path lying on a surface whose normal is given."""
    pts = [Vector(p) for p in points]
    k = len(pts)
    bm = bmesh.new()
    rings = []
    for i in range(k):
        t = (pts[min(i + 1, k - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = Vector(normals[i])
        nrm = (nrm - t * nrm.dot(t)).normalized()
        b = t.cross(nrm).normalized()
        rings.append([bm.verts.new(pts[i] + b * (sx * width / 2) + nrm * (sy * thick / 2))
                      for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for ra, rb in zip(rings, rings[1:]):
        for s in range(4):
            q = (s + 1) % 4
            bm.faces.new([ra[s], ra[q], rb[q], rb[s]])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def box(x0, x1, y0, y1, z0, z1, rot_z=0.0, about=(0.0, 0.0)):
    bm = fb.zbox(x0, x1, y0, y1, z0, z1)
    if rot_z:
        fb.turn(bm, rot_z, 'Z', pivot=(about[0], about[1], 0.0))
    return bm


# ============================================================================================ 1-3. floor pieces

RUG = (1.60, 1.10)
COIR = (1.20, 0.70)
RUBBER = (0.914, 0.610)


def rug_corner():
    """
    LI_Rug_Corner: a calm flatweave for the reading corner, 1.60 x 1.10 (the 110 x 160 cm flatwoven rugs; IKEA TIPHEDE,
    flatwoven cotton, 2 mm). One slab, 10 mm (the brief's: thick enough to read as a rug and not a decal), a 4 mm
    chamfer round its top; the weave, the stripes and the hems are in LI_RugCorner.png over the top and the chamfers
    (the sides take one hem texel). Cut: the fringe (a separate alpha-clipped part on the old kilim, and one of the
    loud things the redesign removed); the pile (a flatweave has none).
    """
    p = LivedPiece('LI_Rug_Corner')
    W, D = RUG
    p.textured('rug', slab(W, D, 0.010, 0.004), M('LI_RugCorner'), origin=(-W / 2, -D / 2, 0), u_axis=(1, 0, 0),
               v_axis=(0, 1, 0), w=W, h=D, edge=(0.004, 0.5))
    return p


def rug_entry():
    """
    LI_Rug_Entry: plain coir on a black PVC back, 1.20 x 0.70 x 0.017 (COBA's coir entrance matting: natural coconut
    fibre, 17 mm, PVC-backed, cut to size from the roll). The coir slab 15.5 mm with a 5 mm chamfer (the cut edge); the
    backing 2 mm, 1 mm proud all round, so the mat shows how it is made from a low angle. Cut: a border and any words
    (his "plain coir-coloured").
    """
    p = LivedPiece('LI_Rug_Entry')
    W, D = COIR
    p.textured('coir', slab(W, D, 0.0155, 0.005, z0=0.0015), M('LI_CoirMat'), origin=(-W / 2, -D / 2, 0),
               u_axis=(1, 0, 0), v_axis=(0, 1, 0), w=W, h=D, edge=(0.003, 0.5))
    p.add('PVC backing (2: how it is made)', fb.zbox(-W / 2 - 0.001, W / 2 + 0.001, -D / 2 - 0.001, D / 2 + 0.001, 0.0, 0.002),
          M('CF_Black'))
    return p


def till_mat():
    """
    LI_TillMat: a black anti-fatigue mat, 36 x 24 x 1/2 in (0.914 x 0.610 x 0.0127; the restaurant mats sold at that size,
    and Winco's RBM-35K: 1/2 in rubber, bevelled so the edges lie flat), the same kind as the bench's and the drink
    station's mats. A 1.5 mm vertical lip, then a 25 mm ramp up to the top (the real trip-safe bevel, in geometry), the
    drainage holes in LI_RubberMat.png. Cut: the holes as geometry (hundreds of faces for a floor mat).
    """
    p = LivedPiece('LI_TillMat')
    W, D = RUBBER
    ramp = 0.025
    bm = hl.tube([rect_loop(0, 0, 0.0, W, D), rect_loop(0, 0, 0.0015, W, D), rect_loop(0, 0, 0.0127, W - 2 * ramp, D - 2 * ramp)])
    p.textured('rubber', bm, M('LI_RubberMat'), origin=(-W / 2, -D / 2, 0), u_axis=(1, 0, 0), v_axis=(0, 1, 0),
               w=W, h=D, edge=(0.006, 0.5))
    return p


# ============================================================================================ 4. the napkin dispenser

def napkin_dispenser():
    """
    LI_NapkinDispenser: a tabletop dispenser that loads and dispenses from both faces (Palmer Fixture's ND0055: two-sided;
    folded napkins 6 1/2 x 5 in, as Dixie's 51602 takes), since 24 Oct 2026 at 0.80 of its 0.165 x 0.100 x 0.150, chrome. The
    body is one steel sleeve, its faces framing a window on each side where the napkin stack shows 4 mm in (you pull the
    front one); two black rubber feet. Cut: the spring and the follower plate inside (never seen), a maker's plate.
    """
    # 24 Oct 2026, art audit a48: "a plain light-grey cube with a white slot, bigger than the cups. From the counter they
    # read as white cups." His answer: "Yes, do that": rounded edges, a chrome body, about 20 % smaller. So the same
    # two-faced dispenser at 0.80 (0.132 x 0.080 x 0.120, the size of the smaller diner holders), its corners rounded on a
    # 22 mm radius and its front and back edges rolled, in LI_Chrome; the napkins and the feet as they were, scaled.
    p = LivedPiece('LI_NapkinDispenser')
    chrome, paper, black = M('LI_Chrome'), M('DC_Chalk'), M('CF_Black')
    hx, z0, z1, rr = 0.066, 0.0035, 0.120, 0.022
    wx, wz0, wz1 = 0.0545, 0.0195, 0.1040              # the window, scaled with the body: a 11.5 mm frame
    yf = 0.040

    def outer(y, inset):
        """The body's outline in XZ at depth y, counter-clockwise seen from the front (x right, z up): a rounded
        rectangle, three segments a corner."""
        a, b0, b1, r = hx - inset, z0 + inset, z1 - inset, rr - inset
        pts = []
        for (cx, cz, a0) in ((a - r, b0 + r, -90.0), (a - r, b1 - r, 0.0), (-a + r, b1 - r, 90.0), (-a + r, b0 + r, 180.0)):
            for k in range(4):
                t = math.radians(a0 + 30.0 * k)
                pts.append((cx + r * math.cos(t), y, cz + r * math.sin(t)))
        return pts
    bm = bmesh.new()
    rings = ((-yf, 0.0030), (-yf + 0.0012, 0.0009), (-yf + 0.0030, 0.0), (yf - 0.0030, 0.0), (yf - 0.0012, 0.0009), (yf, 0.0030))
    L = [[bm.verts.new(q) for q in outer(y, ins)] for y, ins in rings]
    n = len(L[0])
    for lo, hi in zip(L, L[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    win = [[bm.verts.new((x, y, z)) for x, z in ((wx, wz0), (wx, wz1), (-wx, wz1), (-wx, wz0))] for y in (-yf, yf)]
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([win[0][i], win[0][j], win[1][j], win[1][i]])
    centre = (0.0, (wz0 + wz1) / 2)
    bridge(bm, L[0], win[0], centre, axes=(0, 2))
    bridge(bm, L[-1], win[1], centre, axes=(0, 2))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.add('chrome sleeve, a window each face (1, 3)', bm, chrome)
    p.add('napkin stack (1: you pull the front one)', fb.zbox(-wx + 0.002, wx - 0.002, -yf + 0.004, yf - 0.004, wz0 + 0.002, wz1 - 0.002), paper)
    # the front napkin's lower half, half pulled: it folds out of the window from its middle (what you reach for)
    tab = fb.zbox(-0.034, 0.034, -0.0009, 0.0009, 0.0, 0.038)
    fb.turn(tab, 148.0, 'X')                               # hinged at its top, swung out and down toward the reader
    p.add('napkin, half pulled (1, 2: how it dispenses)', hl.moved(tab, 0.0, -yf + 0.0035, (wz0 + wz1) / 2 + 0.004), paper)
    for y in (-0.026, 0.026):
        p.add('rubber foot (2)', fb.zbox(-0.054, 0.054, y - 0.005, y + 0.005, 0.0, 0.004), black)
    return p


# ============================================================================================ 5. the condiment station

def game_cup_profile():
    """The café's own paper cup, DrinkLiquidVisual.BuildShell (metres, base at 0): (radius, height) up the outside."""
    return [(0.030, 0.0), (0.042, 0.109), (0.0425, 0.112)]


def condiment_station():
    """
    LI_CondimentStation: the self-serve tray at the counter's pickup end, 0.60 x 0.30 (the 24 x 11.8 in coffee-condiment
    organisers), every part a real one, laid out the way a station is used (the stocked things at the back and left, a
    clear patch on the right to set your cup down):
      the tray, chestnut, 30 mm rims (the water station's build);
      a stack of beverage napkins (5 x 5 in folded), white;
      a sugar caddy (American Metalcraft's 4 x 3 5/8 in, black) with white and raw sugar packets standing in it;
      a café cup of birch stirrers (140 x 5 x 1 mm, three);
      a nested stack of black 90 mm sip lids (they fit the café's cup);
      a stack of flat kraft sleeves (Sabert's Coffee Clutch: 4.88 in across, 2.38 in tall);
      a small stainless milk jug (12 oz), the milk showing, its lip pulled into a spout, side on to the customer.
    Round parts have 10 sides (the café's cup and lids at a metre); 442 triangles, over the 300 line for small pieces:
    seven real parts at the fewest facets that still read round, one instance in the room.
    Cut: a thermal carafe (a lid hides the milk; the jug says milk), a second lid size, labels.
    """
    p = LivedPiece('LI_CondimentStation')
    wood, paper, black, kraft = M('CC_Wood_Counter'), M('DC_Chalk'), M('CF_Black'), M('CF_Kraft')
    steel, cup_paper = M('DC_Steel'), M('LI_CupPaper')
    W, D, rim, wall, fl = 0.600, 0.300, 0.030, 0.012, 0.010
    tray = hl.tube([rect_loop(0, 0, 0.0, W - 0.006, D - 0.006), rect_loop(0, 0, 0.003, W, D), rect_loop(0, 0, rim - 0.003, W, D),
                    rect_loop(0, 0, rim, W - 0.006, D - 0.006), rect_loop(0, 0, rim, W - 2 * wall, D - 2 * wall),
                    rect_loop(0, 0, fl, W - 2 * wall, D - 2 * wall)])
    p.add('tray, chestnut (3)', tray, wood)
    z0 = fl                                              # everything stands on the tray's floor
    # 24 Oct 2026, art audit a37 ("a white block, a black box, a brown brick and a black pot. The blocks read as blocks"):
    # every stack is now made of the things it is a stack of. The napkins: six, fanned a few degrees each, back middle
    for k, (ang, ox, oy) in enumerate(((0.0, 0.0, 0.0), (4.0, 0.0015, -0.001), (-3.0, -0.001, 0.0015), (6.0, 0.002, 0.001),
                                       (-5.0, -0.0015, -0.001), (2.5, 0.001, 0.002))):
        nap = fb.zbox(-0.0625, 0.0625, -0.0625, 0.0625, 0.0, 0.0083)
        fb.turn(nap, ang, 'Z')
        p.add('napkin (1)', hl.moved(nap, -0.0615 + ox, 0.0675 + oy, z0 - 0.001 + 0.0085 * k), paper)
    # the sugar caddy and its packets, front middle: two rows of nine packets standing on edge, white and raw, each its own
    # height and lean (they are taken from the front), so it reads as packets in a caddy and not two bricks in a box
    cx, cy = -0.060, -0.075
    caddy = hl.tube([rect_loop(cx, cy, z0 - 0.001, 0.100, 0.068), rect_loop(cx, cy, z0 + 0.048, 0.100, 0.068),
                     rect_loop(cx, cy, z0 + 0.048, 0.088, 0.056), rect_loop(cx, cy, z0 + 0.006, 0.088, 0.056)])
    p.add('sugar caddy (1)', caddy, black)
    tops = (0.0, 0.004, -0.003, 0.002, -0.001, 0.005, -0.004, 0.001, 0.003)
    leans = (-6.0, -3.0, 2.0, -1.0, 4.0, -2.0, 3.0, 5.0, -4.0)
    for side, mat, height in ((-1, paper, 0.060), (1, kraft, 0.056)):
        for k in range(9):
            y = cy - 0.0235 + k * 0.0059
            pk = fb.zbox(-0.0185, 0.0185, -0.0019, 0.0019, 0.0, height + tops[(k + (side > 0) * 4) % 9])
            fb.turn(pk, leans[(k + (side > 0) * 3) % 9], 'X')
            fb.turn(pk, (k % 3 - 1) * 1.5 * side, 'Z')
            p.add('white sugar packet (1)' if side < 0 else 'raw sugar packet (1)',
                  hl.moved(pk, cx + side * (0.0215 + 0.0004 * (k % 2)), y, z0 + 0.0055), mat)
    # the cup of stirrers, front left: the café's own cup (its inside to a floor 12 mm up)
    sx, sy = -0.215, -0.078
    prof = [(r, z + z0 - 0.0005) for r, z in [(0.030, 0.0), (0.0425, 0.112)]] + \
           [(0.0385, z0 + 0.112), (0.028, z0 + 0.012), (0.0, z0 + 0.012)]
    p.add('cup (the café\'s, 3)', hl.moved(cp.spin(prof, sides=10), sx, sy, 0), cup_paper, smooth=True)
    for k, (ax, ay, lean) in enumerate(((0.008, 0.004, 7.0), (-0.006, 0.008, -9.0), (0.002, -0.009, 5.0))):
        st = fb.zbox(-0.00275, 0.00275, -0.00075, 0.00075, 0.0, 0.140)
        fb.turn(st, lean, 'Y')
        fb.turn(st, 35 + 70 * k, 'Z')
        p.add('birch stirrer (1)', hl.moved(st, sx + ax, sy + ay, z0 + 0.0125), cup_paper)
    # the lids: a short nested stack of 90 mm sip lids, back right: each lid's rolled rim a ridge round the stack (nine
    # showing, 5 mm apart), the top one's recess and dome, so it reads as lids and not a pot (a37)
    lx, ly = 0.075, 0.060
    prof = [(0.0, z0 - 0.0005), (0.0438, z0 - 0.0005)]
    for k in range(9):
        zb_ = z0 + 0.0040 + 0.0050 * k
        prof += [(0.0438, zb_ - 0.0020), (0.0462, zb_ - 0.0006), (0.0462, zb_ + 0.0004), (0.0438, zb_ + 0.0014)]
    prof += [(0.0440, z0 + 0.0465), (0.0400, z0 + 0.0490), (0.0310, z0 + 0.0590), (0.0, z0 + 0.0590)]
    p.add('lid stack (1)', hl.moved(cp.spin(prof, sides=12), lx, ly, 0), black)
    # the sleeves: flat kraft sleeves (each a cone flattened: wider at its top edge), front right: four, fanned a little
    vx, vy = 0.080, -0.072
    for k, (ang, ox, oy) in enumerate(((0.0, 0.0, 0.0), (-4.0, 0.002, 0.002), (5.0, -0.002, 0.001), (-2.0, 0.001, -0.002))):
        sleeve = bmesh.new()
        base = [(-0.056, -0.031), (0.056, -0.031), (0.062, 0.031), (-0.062, 0.031)]
        lo = [sleeve.verts.new((x, y, 0.0)) for x, y in base]
        hi = [sleeve.verts.new((x, y, 0.0078)) for x, y in base]
        for i in range(4):
            j = (i + 1) % 4
            sleeve.faces.new([lo[i], lo[j], hi[j], hi[i]])
        sleeve.faces.new(list(reversed(lo)))
        sleeve.faces.new(hi)
        bmesh.ops.recalc_face_normals(sleeve, faces=sleeve.faces[:])
        fb.turn(sleeve, ang, 'Z')
        p.add('kraft sleeve (1)', hl.moved(sleeve, vx + ox, vy + oy, z0 - 0.0005 + 0.0080 * k), kraft)
    # the milk jug, back left: a stainless creamer, the milk inside, its lip pulled into a spout to the right and its
    # handle to the left, so the customer sees a jug side on (handle, belly, spout)
    jx, jy = -0.205, 0.052
    jug = cp.spin([(0.0, z0 - 0.0005), (0.031, z0 - 0.0005), (0.037, z0 + 0.045), (0.033, z0 + 0.088), (0.0305, z0 + 0.088),
                   (0.0325, z0 + 0.074), (0.0, z0 + 0.074)], sides=10, phase=0.0)
    for v in jug.verts:                                   # the spout: the rim's right-hand point out and up
        if abs(v.co.z - (z0 + 0.088)) < 1e-6 and v.co.x > 0.029 and abs(v.co.y) < 1e-6:
            v.co.x += 0.011
            v.co.z += 0.005
    hl.moved(jug, jx, jy, 0)
    p.add_split('milk jug, steel; the milk (1, 2)', jug, steel, paper,
                lambda f: f.calc_center_median().z > z0 + 0.0735 and f.normal.z > 0.9)
    handle = [(jx - 0.029, jy, z0 + 0.078), (jx - 0.056, jy, z0 + 0.074), (jx - 0.060, jy, z0 + 0.040),
              (jx - 0.034, jy, z0 + 0.022)]
    p.add('jug handle (1)', cp.sweep_var(handle, [0.0042, 0.0042, 0.0042, 0.0042], sides=4), steel)
    # a37's third ask, "a labelled shaker": a cinnamon shaker at the back of the clear patch (the front right stays free
    # for a cup), a glass jar the colour of what's in it, a steel cap with seven holes, and a white label round its front
    # that says CINNAMON to the customers. Added last, so the tray's material slots keep their order.
    hx_, hy_, R = 0.200, 0.068, 0.024
    jar = cp.spin([(0.0, z0 - 0.0005), (R - 0.002, z0 - 0.0005), (R, z0 + 0.003), (R, z0 + 0.074), (R - 0.004, z0 + 0.080),
                   (0.0, z0 + 0.080)], sides=12)
    p.add('cinnamon shaker, the jar (1)', hl.moved(jar, hx_, hy_, 0), kraft)
    cap = cp.spin([(0.0, z0 + 0.0785), (R - 0.0035, z0 + 0.0785), (R - 0.0035, z0 + 0.0960), (R - 0.0080, z0 + 0.1010),
                   (0.0, z0 + 0.1010)], sides=12)
    p.add('cinnamon shaker, its cap (1)', hl.moved(cap, hx_, hy_, 0), steel)
    for (a, r) in [(0.0, 0.0)] + [(60.0 * k, 0.0075) for k in range(6)]:
        p.add('cap hole (2: how it shakes)', hl.disc(hx_ + r * math.cos(math.radians(a)), hy_ + r * math.sin(math.radians(a)),
                                                     z0 + 0.0995, z0 + 0.1016, 0.0016, sides=8), black)
    # the label: a band of paper round the jar's front 150 degrees (toward the customers, -Y), 0.6 mm proud
    band = bmesh.new()
    a0, a1, n = math.radians(-165.0), math.radians(-15.0), 10
    rings = []
    for (rr, zz) in ((R + 0.0006, z0 + 0.024), (R + 0.0006, z0 + 0.058), (R - 0.0004, z0 + 0.058), (R - 0.0004, z0 + 0.024)):
        rings.append([band.verts.new((hx_ + rr * math.cos(a0 + (a1 - a0) * i / n), hy_ + rr * math.sin(a0 + (a1 - a0) * i / n), zz))
                      for i in range(n + 1)])
    for a_, b_ in zip(rings, rings[1:] + rings[:1]):
        for i in range(n):
            band.faces.new([a_[i], a_[i + 1], b_[i + 1], b_[i]])
    for i0 in (0, n):
        band.faces.new([rings[k][i0] for k in (range(4) if i0 == 0 else reversed(range(4)))])
    bmesh.ops.recalc_face_normals(band, faces=band.faces[:])
    p.add('shaker label (1)', band, paper)
    word = hl.text_mesh('CINNAMON', 0.0078, 0.0005)
    if word is not None:
        bmesh.ops.triangulate(word, faces=word.faces[:])
        fb.turn(word, 90.0, 'X')                          # face -Y, up +Z, reading +X for the customers
        zs = [v.co.z for v in word.verts]
        rl = R + 0.0006                                   # wrapped round the label's face: x becomes an angle
        for v in word.verts:
            ang = math.radians(-90.0) + v.co.x / rl
            rr = rl - v.co.y                              # its depth (y from 0 to -0.5 mm) stands it off the label
            v.co.x, v.co.y = hx_ + rr * math.cos(ang), hy_ + rr * math.sin(ang)
            v.co.z += z0 + 0.041 - (min(zs) + max(zs)) / 2
        p.add('label word (1)', word, black)
    return p


# ============================================================================================ 6. the three-stream station

BIN = {'W': 0.956, 'D': 0.430, 'H': 0.851, 'top': 0.800}     # Glasdon's Nexus Evolution Trio: 956 x 430 x 851 mm
BIN_LABEL = (0.240, 0.300, 0.430)                           # a label's w, h, and its bottom's height
LABEL_ATLAS = (1024, 512)


def label_rect(k):
    """Label k's place in LI_BinLabels.png, in UVs, inset 3 px (cafe_livedin_textures.label_rect, same formula)."""
    x0, x1 = round(k * LABEL_ATLAS[0] / 3), round((k + 1) * LABEL_ATLAS[0] / 3)
    return ((x0 + 3) / LABEL_ATLAS[0], 3 / LABEL_ATLAS[1], (x1 - 3) / LABEL_ATLAS[0], (LABEL_ATLAS[1] - 3) / LABEL_ATLAS[1])


def bin_station():
    """
    LI_BinStation: San Francisco's three streams (the Mandatory Recycling and Composting Ordinance, 2009: blue for
    recycling, green for compost, black for landfill) in one slim indoor body, after Glasdon's Nexus Evolution Trio
    (956 x 430 x 851 mm: one moulded body, an aperture panel over each compartment, big label areas on the front),
    split into three equal streams. A warm-grey body on a recessed black kick, three coloured aperture panels with 6 mm
    gaps between them (each lifts for the bag), each with its opening (a round hole for bottles and cans; a wide mouth
    for compost, where the café's paper cups go, and for landfill) and the dark bag inside; a label sticker on the front
    of each (LI_BinLabels.png). Cut: the bag clips, a pedal, a maker's mark.
    """
    p = LivedPiece('LI_BinStation')
    W, D, top = BIN['W'], BIN['D'], BIN['top']
    body, black = M('DC_Steel'), M('CF_Black')
    p.add('kick, recessed (3)', fb.zbox(-W / 2 + 0.010, W / 2 - 0.010, -D / 2 + 0.010, D / 2 - 0.010, 0.0, 0.032), black)
    cv, ct = 0.022, 0.008
    pw, gap, edge = (W - 2 * 0.010 - 2 * 0.006) / 3, 0.006, 0.010
    zb, zt = top - 0.002, BIN['H']
    # the body in two: a closed lower box (its top, dark, is the floor of the bags' space), and the upper band, open
    # over the compartments under the panels, dark inside, so an opening shows the bag and not a grey lid underneath
    z_mid = top - 0.096
    # the lower box 1 mm inside the band all round: where they overlap no two walls share a plane, and the band's
    # 1 mm lip reads as the moulding's parting line
    lower = hl.tube([hl.ring_chamfered_rect(0, 0, 0.030, W - 0.002, D - 0.002, cv - 0.0006),
                     hl.ring_chamfered_rect(0, 0, z_mid + 0.004, W - 0.002, D - 0.002, cv - 0.0006)])
    p.add_split('body, lower (3)', lower, body, black, lambda f: f.normal.z > 0.9 and f.calc_center_median().z > 0.5)
    band = bmesh.new()
    o0 = [band.verts.new(q) for q in hl.ring_chamfered_rect(0, 0, z_mid, W, D, cv)]
    o1 = [band.verts.new(q) for q in hl.ring_chamfered_rect(0, 0, top - ct, W, D, cv)]
    o2 = [band.verts.new(q) for q in hl.ring_chamfered_rect(0, 0, top, W - 2 * ct, D - 2 * ct, cv - ct * 0.6)]
    hx, hy = W / 2 - edge - 0.002, D / 2 - edge - 0.002          # the opening: 2 mm inside the panels' outer edges
    i2 = [band.verts.new(q) for q in rect_loop(0, 0, top, 2 * hx, 2 * hy)]
    i0 = [band.verts.new(q) for q in rect_loop(0, 0, z_mid, 2 * hx, 2 * hy)]
    for lo, hi in ((o0, o1), (o1, o2)):
        for i in range(8):
            j = (i + 1) % 8
            band.faces.new([lo[i], lo[j], hi[j], hi[i]])
    for i in range(4):
        j = (i + 1) % 4
        band.faces.new([i2[i], i2[j], i0[j], i0[i]])
    bridge(band, o2, i2, (0.0, 0.0))
    bridge(band, o0, i0, (0.0, 0.0))
    bmesh.ops.recalc_face_normals(band, faces=band.faces[:])
    p.add_split('body, upper band (3), dark inside (2)', band, body, black,
                lambda f: f.normal.z < 0.5 and f.normal.dot(f.calc_center_median() * -1.0) > 0.0 and
                abs(f.calc_center_median().x) < hx + 1e-4 and abs(f.calc_center_median().y) < hy + 1e-4)
    streams = (('RECYCLING', M('LI_RecycleBlue'), 'round'), ('COMPOST', M('LI_CompostGreen'), 'mouth'),
               ('LANDFILL', black, 'mouth'))
    for k, (name, mat, kind) in enumerate(streams):
        x0 = -W / 2 + edge + k * (pw + gap)
        x1 = x0 + pw
        cx, cy = (x0 + x1) / 2, -0.015
        if kind == 'round':
            def hole(z, cx=cx, cy=cy, r=0.075):
                return circle(cx, cy, z, r, 16)

            def inside(z, cx=cx, cy=cy, r=0.0735):
                return circle(cx, cy, z, r, 16)
        else:
            def hole(z, cx=cx, cy=cy):
                return hl.ring_rrect(cx, cy, z, 0.200, 0.150, 0.025, per_corner=2)

            def inside(z, cx=cx, cy=cy):
                return hl.ring_rrect(cx, cy, z, 0.197, 0.147, 0.0235, per_corner=2)
        p.add('%s aperture panel (1, 2, 3)' % name.lower(), panel_with_hole(x0, x1, -D / 2 + edge, D / 2 - edge, zb, zt, hole, (cx, cy)), mat)
        p.add('%s bag, dark inside (2)' % name.lower(), liner(inside, zb + 0.002, z_mid + 0.012), black, face_test=faces_up)
        lw, lh, lz = BIN_LABEL
        p.textured('%s label (2: what goes in)' % name.lower(),
                   fb.zbox(cx - lw / 2, cx + lw / 2, -D / 2 - 0.002, -D / 2 + 0.0005, lz, lz + lh), M('LI_BinLabels'),
                   origin=(cx - lw / 2, -D / 2, lz), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=lw, h=lh, rect=label_rect(k),
                   edge=(0.01, 0.5), min_dot=0.9)
    return p


# ============================================================================================ 7. the street bin

def street_bin():
    """
    LI_StreetBin: San Francisco's green litter can (the green "Renaissance" cans on its streets since 1993: "4 feet tall,
    green, made of metal", KQED), after Forms+Surfaces' Urban Renaissance receptacle (round, 26.6 in across, 38.6 in
    tall for the 36 gallon with a flat top, grillwork, a dome-lid option): 0.676 across, 1.0 tall (the dome adds what the
    flat top does not have). A cast foot and two bands holding 18
    flat bars round a dark liner (the grillwork you see the bag through), a dome lid with a hooded mouth facing the
    pavement, and the service door's lock on the lower band. Cut: the recycling hopper some of the city's cans carry on
    top (the café's bins inside are the sorting beat), the city's crest (no logos).
    """
    p = LivedPiece('LI_StreetBin')
    green, black = M('LI_StreetGreen'), M('CF_Black')
    p.add('cast foot (3)', fb.cylinder((0, 0, 0.0125), 0.290, 0.025, sides=16), green)
    p.add('lower band (3)', fb.cylinder((0, 0, 0.065), 0.322, 0.090, sides=16), green)
    p.add('liner, dark (2: the bag behind the bars)', fb.cylinder((0, 0, 0.435), 0.296, 0.660, sides=16), black)
    for k in range(18):
        a = 360.0 * k / 18 + 10.0
        bar = fb.zbox(-0.020, 0.020, 0.306, 0.318, 0.105, 0.765)
        fb.turn(bar, a - 90.0, 'Z')
        p.add('grillwork bar (3)', bar, green)
    p.add('upper band (3)', fb.cylinder((0, 0, 0.7975), 0.322, 0.075, sides=16), green)
    p.add('dome lid (3)', cp.spin([(0.0, 0.828), (0.330, 0.828), (0.330, 0.850), (0.305, 0.900), (0.240, 0.950),
                                   (0.135, 0.985), (0.0, 0.998)], sides=16), green)
    # the hooded mouth: a frame standing out of the dome's front, its throat dark and 5 cm deep
    bm = bmesh.new()
    ox, oz0, oz1 = 0.150, 0.840, 0.935
    ix, iz0, iz1 = 0.120, 0.857, 0.918
    yb, yf = -0.190, -0.352            # the back inside the dome everywhere (at z 0.935 the dome is 0.26 round)
    O = [[bm.verts.new((x, y, z)) for x, z in ((ox, oz0), (ox, oz1), (-ox, oz1), (-ox, oz0))] for y in (yf, yb)]
    I_ = [[bm.verts.new((x, y, z)) for x, z in ((ix, iz0), (ix, iz1), (-ix, iz1), (-ix, iz0))] for y in (yf, yb)]
    for ring in (O, I_):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new([ring[0][i], ring[0][j], ring[1][j], ring[1][i]])
    for s in (0, 1):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new([O[s][i], O[s][j], I_[s][j], I_[s][i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.add('hooded mouth (1: where litter goes)', bm, green)
    p.add('throat, dark (1)', fb.zbox(-ix - 0.001, ix + 0.001, -0.300, -0.200, iz0 - 0.001, iz1 + 0.001), black)
    p.add('service door lock (2)', fb.zbox(-0.018, 0.018, -0.326, -0.316, 0.048, 0.086), black)
    # drawn at a 0.660 lid; the measured can is 26.6 in (0.676) across its lid: the whole can widened to it, in plan only
    s = STREET_BIN_ACROSS / 0.660
    for v in p.bm.verts:
        v.co.x *= s
        v.co.y *= s
    return p


STREET_BIN_ACROSS = 0.676                                  # Forms+Surfaces Urban Renaissance, 36 gal: 26.6 in overall


# ============================================================================================ 8-9. the laptop

LAP = {'W': 0.314, 'D': 0.226, 'base': 0.0115, 'lid': 0.0055, 'open_deg': 110.0}   # Dell 14: 314 x 226 x 17-18 mm


def _laptop(name, opened):
    """
    LI_Laptop_Open / LI_Laptop_Closed: a plain 14 in laptop (Dell's 14: 314 x 226 mm, 16-18 mm thick), no brand and no
    mark. A warm-steel base (rounded corners, one chamfer) with the keyboard as a dark plate and a trackpad; the lid,
    0.5 mm inside the base all round, on a dark hinge barrel at the back; open, the lid turns about the hinge to 110
    degrees from the deck and the screen (a dark glass, on) sits in its bezel, a 22 mm chin at the hinge. Cut: keys as
    geometry (hundreds of faces at 30 cm), ports, a logo.
    """
    p = LivedPiece(name)
    W, D, tb, tl = LAP['W'], LAP['D'], LAP['base'], LAP['lid']
    steel, black = M('DC_Steel'), M('CF_Black')
    rc = 0.009

    def shell(z0, t, inset=0.0):
        sx, sy = W - 2 * inset, D - 2 * inset
        return hl.tube([hl.ring_rrect(0, 0, z0, sx - 0.004, sy - 0.004, rc - 0.002, per_corner=1),
                        hl.ring_rrect(0, 0, z0 + 0.002, sx, sy, rc, per_corner=1),
                        hl.ring_rrect(0, 0, z0 + t - 0.0015, sx, sy, rc, per_corner=1),
                        hl.ring_rrect(0, 0, z0 + t, sx - 0.003, sy - 0.003, rc - 0.0015, per_corner=1)])
    p.add('base (3)', shell(0.0, tb), steel)
    hinge_y, hinge_z = D / 2 - 0.0005, tb + 0.0003
    p.add('hinge barrel (2: how it opens)', fb.cylinder((0, D / 2 - 0.0035, tb - 0.0005), 0.0042, 0.240, sides=8, rot=(0, 90, 0)), black)
    lid = shell(tb + 0.0003, tl, inset=0.0005)
    parts = [('lid (3)', lid, steel)]
    if opened:
        p.add('keyboard (1)', fb.zbox(-0.136, 0.136, -0.012, 0.097, tb - 0.0003, tb + 0.0002), black)
        p.add('trackpad (1)', fb.zbox(-0.055, 0.055, -0.100, -0.032, tb - 0.0003, tb + 0.0001), M('DC_SteelDark'))
        for nm, bm, mat in parts:
            fb.turn(bm, -LAP['open_deg'], 'X', pivot=(0.0, hinge_y, hinge_z))
            p.add(nm, bm, mat)
        # the screen on the lid's inner face (down when shut): the hinge end is its chin. 24 Oct 2026, art audit a50 ("a
        # flat light-blue panel. It reads as a dead screen"): its face now carries LI_Screen.png, a dark title bar over a
        # light page, mapped 0..1 across the glass (u to the reader's right, v up the open lid); its edges take the bar's
        # dark texel
        scr = fb.zbox(-0.1480, 0.1480, -D / 2 + 0.0065, D / 2 - 0.0225, tb + 0.00005, tb + 0.0004)
        fb.turn(scr, -LAP['open_deg'], 'X', pivot=(0.0, hinge_y, hinge_z))
        turn = Matrix.Rotation(math.radians(-LAP['open_deg']), 3, 'X')
        pivot = Vector((0.0, hinge_y, hinge_z))
        origin = pivot + turn @ (Vector((-0.1480, D / 2 - 0.0225, tb + 0.00005)) - pivot)    # the glass's bottom left, open
        up = turn @ Vector((0.0, -1.0, 0.0))
        p.textured('screen, on (1)', scr, M('LI_Screen'), origin=tuple(origin), u_axis=(1.0, 0.0, 0.0), v_axis=tuple(up),
                   w=0.2960, h=D - 0.0290, edge=(0.01, 0.99), min_dot=0.5)
    else:
        for nm, bm, mat in parts:
            p.add(nm, bm, mat)
    return p


def laptop_open():
    return _laptop('LI_Laptop_Open', True)


def laptop_closed():
    return _laptop('LI_Laptop_Closed', False)


# ============================================================================================ 10. the tote

def tote_bag():
    """
    LI_ToteBag: a natural canvas tote set down at a chair's foot (12 oz canvas, 14 W x 15 H x 4 in gusset, cotton web
    handles), slumped: its contents bulge it to 0.37 x 0.14 at the middle, it leans back and sags to 0.29 (the right end
    of its top 6 cm lower), the mouth half open with a jumper showing, one handle flopped forward over its face, the other
    back. 0.39 x 0.18 x 0.29.
    Cut: a print (a plain bag; a print would be a brand), the inside below the jumper.
    """
    p = LivedPiece('LI_ToteBag')
    canvas, web = M('DC_Chalk'), M('DC_Fabric')
    # 24 Oct 2026, art audit a49 ("a stiff blocky bag; side-on it's a pale egg-shaped rock with two sticks ... the last
    # soft thing built as a solid"; R2 judged it soft enough; his answer: "Yes, do that"): canvas, shaded soft (it was
    # faceted); a flat base as wide as its gusset, so side-on it is a wedge narrowing to the mouth, not an egg; finer
    # sections; the empty top slumping further to one side; and the front of the mouth folding forward and down between
    # the handles, the fold canvas makes when nothing holds it up. The handles are as they were.
    # (z, width, depth, y offset, bulge of the faces' middles, sag of the mouth's edge at the middle, the fold's forward
    # reach at the front's middle): the contents push the faces out, the upper body leans back, the top slumps
    # 26 Oct 2026, a second pass (my own look at the render: it still stood up square): the mouth's long edges sag
    # between the handles (sag 4 cm, was 3), its front folds forward and down further (4.5 cm, was 2.4), the upper body
    # leans a little more, and the right half of the top has given way, 6 cm lower than the left (SLUMP): a bag set
    # down half full, not a box. The mouth stays open, the jumper 5 cm down inside it.
    R = [(0.000, 0.330, 0.110, 0.000, 0.000, 0.0, 0.0), (0.010, 0.350, 0.118, 0.000, 0.006, 0.0, 0.0),
         (0.085, 0.364, 0.126, 0.004, 0.024, 0.0, 0.0), (0.170, 0.368, 0.112, 0.012, 0.026, 0.0, 0.0),
         (0.236, 0.358, 0.078, 0.024, 0.016, 0.010, 0.010), (0.286, 0.348, 0.054, 0.034, 0.006, 0.040, 0.045),
         (0.281, 0.336, 0.040, 0.034, 0.004, 0.040, 0.043), (0.234, 0.322, 0.052, 0.030, 0.000, 0.012, 0.006)]
    RC = 0.020
    lean = 0.060                                           # x drift a metre up: the empty top slumps toward +x
    SLUMP = 0.060                                          # the top's right end this much lower than its left

    def slump(x, z):
        return SLUMP * max(0.0, x / 0.17) ** 1.5 * hl.smoothstep((z - 0.15) / 0.11)

    def ring(z, w, d, yo, bulge, sag, fold):
        pts = [list(q) for q in hl.ring_rrect(lean * z, yo, z, w, d, RC, per_corner=1, per_side=3)]
        # 20 points: per corner 2, per side 3 (middles at 3 right, 8 back, 13 left, 18 front; their neighbours carry 0.6)
        for mid, axis, sgn in ((8, 1, 1.0), (18, 1, -1.0), (3, 0, 0.3), (13, 0, -0.3)):
            for k, wgt in ((-1, 0.6), (0, 1.0), (1, 0.6)):
                pts[mid + k][axis] += sgn * bulge * wgt
                if mid in (8, 18):
                    pts[mid + k][2] -= sag * wgt
        for k, wgt in ((-3, 0.25), (-2, 0.25), (-1, 0.6), (0, 1.0), (1, 0.6), (2, 0.25)):   # the front's fold
            q = pts[(18 + k) % 20]
            q[1] -= fold * wgt
            q[2] -= 0.55 * fold * wgt
        for q in pts:                                      # the right half of the top given way
            q[2] -= slump(q[0] - lean * z, z)
        return [tuple(q) for q in pts]
    body = hl.tube([ring(*r) for r in R], cap_bottom=True, cap_top=True)
    p.add_split('canvas body, the jumper inside (3, 4)', body, canvas, web,
                lambda f: f.normal.z > 0.9 and f.calc_center_median().z > 0.22, smooth=True)
    flat = R[0][1] / 2 - RC

    def face_y(x, z, side):
        """The body's front (side -1) or back (+1) face at (x, z): the rings' faces, their middles bulged."""
        for ra, rb in zip(R[:6], R[1:6]):
            if ra[0] <= z <= rb[0]:
                t = (z - ra[0]) / (rb[0] - ra[0])
                yo, d, bulge = (ra[3] + (rb[3] - ra[3]) * t, ra[2] + (rb[2] - ra[2]) * t, ra[4] + (rb[4] - ra[4]) * t)
                return yo + side * (d / 2 + RC * 0 + bulge * max(0.0, 1.0 - abs(x) / flat))
        return R[5][3] + side * R[5][2] / 2

    def mouth_z(x):
        return R[5][0] - R[5][5] * max(0.0, 1.0 - abs(x) / flat) - slump(x, R[5][0])
    # the handles, cotton webbing: each a U from its two ends at the mouth, one flopped over the front, one the back
    for side, drop in ((-1, 0.150), (1, 0.190)):
        pts, nrm = [], []
        for k in range(7):
            s = k / 6.0
            x = -0.080 * math.cos(math.pi * s)
            z = mouth_z(x) - 0.004 - drop * math.sin(math.pi * s) ** 1.2
            y = face_y(x, z, side) + side * 0.0042
            pts.append((x + lean * z, y, z))
            nrm.append((0.0, side, 0.0))
        p.add('web handle (1, 3)', flat_strap(pts, nrm, 0.025, 0.003), web)
    return p


# ============================================================================================ 11. the coat on a chair

# The two café chairs' top rails, measured from cafe_seating.py's own builds (Blender, the chair's frame):
#   CK_Chair_Cafe     top slat z 0.816-0.888, post tips z 0.8954 at x +-0.18 (y 0.232); at x 0 the slat's front face
#                     y 0.2424 and back 0.2604 at the top; it bows back about 21 mm at the middle; the back rakes 0.225
#   CK_Chair_Spindle  crest z 0.798-0.863, x +-0.1875; at x 0 front 0.2196, back 0.2416 at the top; bows 28 mm
# The coat is built on the cafe chair; on the spindle chair it goes 32.4 mm lower and 20.8 mm forward (Blender), where
# the two rails' tops and middles meet.
COAT_ON = {'CK_Chair_Cafe': (0.0, 0.0, 0.0), 'CK_Chair_Spindle': (0.0, -0.0208, -0.0324)}   # Blender offsets
RAIL = {'top': 0.8954, 'yc': 0.2514, 'half': 0.0105, 'bow': 0.0215, 'bow_back': 0.0155, 'rake': 0.225, 'x_end': 0.19}


def coat_on_chair():
    """
    LI_Coat_OnChair: a slate-blue chore coat (Le Laboureur's size M: chest 22 1/2 in flat, body 30 in, sleeve 35 3/4 in
    from the centre back) hung by its shoulders over a café chair's top rail, the way you hang one before you sit: the
    corduroy collar folded over onto the seat side, the shoulders over the rail and its posts, the body hanging down the
    back of the chair (the back panel outward, the fronts folded in behind it), the sleeves hanging beside it. It
    follows the rail's bow and its rake, so it fits both chairs: origin = the chair's origin; on CK_Chair_Spindle it
    sits 32 mm lower and 21 mm forward (COAT_ON). Cut: buttons, pockets and seams (on the hidden front panels), a hood.
    """
    p = LivedPiece('LI_Coat_OnChair')
    cloth, collar = M('DC_Ink'), M('DC_Fabric')
    top, yc, half, rake = RAIL['top'], RAIL['yc'], RAIL['half'], RAIL['rake']
    gap, gap_back = 0.003, 0.005

    def bow(x):
        """How far the rail's middle line is from y_c at x (the rail bows back at the middle)."""
        xx = min(abs(x), RAIL['x_end'])
        return -RAIL['bow'] * (xx / 0.18) ** 2

    def front_face(z):
        return yc - half + (z - 0.888) * rake

    # the collar and the fold over the rail, wrapped round the rail's section like a towel over a bar: up the front,
    # round the front-top corner, over the top, round the back-top corner, down the back. (y, z, width, thickness,
    # normal), the thickness centred on the path, every ring clear of the rail on its inside.
    t_c = 0.010

    def corner(y_edge, side, t, g):
        off = (g + t / 2) * 0.7071
        return y_edge + side * off, top + off
    f2y, f2z = corner(yc - half, -1, 0.012, gap)
    f4y, f4z = corner(yc + half, 1, 0.014, gap_back)
    fold = [
        (front_face(0.842) - gap - t_c / 2, 0.842, 0.200, t_c, (0, -1, 0)),
        (front_face(0.872) - gap - t_c / 2, 0.872, 0.240, t_c, (0, -1, 0)),
        (f2y, f2z, 0.330, 0.012, (0, -1, 1)),
        (yc, top + gap + 0.007, 0.440, 0.014, (0, 0, 1)),
        (f4y, f4z, 0.460, 0.014, (0, 1, 1)),
        (yc + half + gap_back + 0.011, top - 0.025, 0.480, 0.022, (0, 1, 0)),
    ]
    hang = [   # the body down the back of the chair: (y offset from the rail's back face, z, width, thickness)
        (0.024, 0.800, 0.510, 0.034), (0.034, 0.580, 0.548, 0.040), (0.040, 0.360, 0.562, 0.036),
        (0.042, 0.205, 0.570, 0.026)]
    pts = [(0.0, y, z) for y, z, w, t, n in fold] + [(0.0, yc + half + dy, z) for dy, z, w, t in hang]
    widths = [w for y, z, w, t, n in fold] + [w for dy, z, w, t in hang]
    thicks = [t for y, z, w, t, n in fold] + [t for dy, z, w, t in hang]
    normals = [n for y, z, w, t, n in fold] + [(0, 1, 0)] * len(hang)
    bm = ribbon(pts, normals, widths, thicks, section=LENS10)
    for v in bm.verts:
        # follow the rail's bow: in front, the slat's full bow; behind, a gentler one (the cafe chair's posts stand
        # 4-5 mm further back at the rail's ends than the slat's bow), blended across the top; half of it at the hem
        f = max(0.0, min(1.0, (v.co.z - 0.20) / (0.84 - 0.20)))
        w = max(0.0, min(1.0, (v.co.y - (yc - 0.010)) / 0.020))
        b = bow(v.co.x) * (1.0 - w) + bow(v.co.x) * (RAIL['bow_back'] / RAIL['bow']) * w
        v.co.y += b * (0.5 + 0.5 * f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.add_split('coat: the collar over the rail, the body down the back (3)', bm, cloth, collar,
                lambda f: f.calc_center_median().y < yc - half + 0.004 and f.calc_center_median().z < top - 0.004)
    # the sleeves, from the shoulders down beside the body, a little behind it
    for s in (-1, 1):
        sl = [(s * 0.232, yc + 0.020, 0.872), (s * 0.268, yc + 0.060, 0.760), (s * 0.282, yc + 0.074, 0.560),
              (s * 0.276, yc + 0.070, 0.355)]
        p.add('sleeve (3)', cp.sweep_var(sl, [0.052, 0.050, 0.046, 0.043], sides=6), cloth)
    return p


# ============================================================================================ 12. the kid's drawing

DRAWING = (0.279, 0.216)


def kids_drawing():
    """
    LI_KidsDrawing: a child's crayon drawing of the café on US letter turned sideways (0.279 x 0.216; a US kid's paper,
    landscape because a shop is wide), pinned to the notice board through its top middle: one quad (two triangles) 0.2 mm
    in front of its back plane (y = 0, which lies on the board), and the board's own push pin (11 mm across, 8 mm deep,
    CK_NoticeBoard's), its needle 0.8 mm into what is behind. A wall piece: origin at the bottom centre of its back.
    """
    p = LivedPiece('LI_KidsDrawing', floor=True)
    W, H = DRAWING
    bm = bmesh.new()
    vs = [bm.verts.new(q) for q in ((-W / 2, -0.0002, 0.0), (W / 2, -0.0002, 0.0), (W / 2, -0.0002, H), (-W / 2, -0.0002, H))]
    bm.faces.new(vs)
    p.textured('drawing (4)', bm, M('LI_KidsDrawing'), origin=(-W / 2, -0.0002, 0.0), u_axis=(1, 0, 0), v_axis=(0, 0, 1),
               w=W, h=H, edge=(0.5, 0.5), min_dot=0.9, face_test=faces_toward((0, -1, 0)))
    p.add('push pin (1)', fb.cylinder((0.0, -0.0037, H - 0.012), 0.0055, 0.0090, sides=8, rot=(90, 0, 0)), M('CF_PinYellow'))
    return p


# ============================================================================================ 13. the left cup

def left_cup():
    """
    LI_LeftCup: the café's own paper cup, the one the game serves in (DrinkLiquidVisual.BuildShell: 60 mm at the foot,
    84 mm at the rim, 112 mm tall, warm ivory), left on a table with a black 90 mm dome sip lid on (lids are on the
    condiment station: a guest took one). Smooth inside its seams, as the game draws the cup. 0.092 x 0.125. Cut: the
    rolled lip under the lid, the dregs (the lid hides them), a sleeve (the served cups have none).
    """
    p = LivedPiece('LI_LeftCup')
    prof = [(0.0, 0.0), (0.030, 0.0), (0.0415, 0.104), (0.0455, 0.104), (0.0458, 0.113), (0.0405, 0.1165),
            (0.0335, 0.1225), (0.0260, 0.1250), (0.0, 0.1250)]
    bm = cp.spin(prof, sides=16)
    p.add_split('cup and lid (3)', bm, M('LI_CupPaper'), M('CF_Black'), lambda f: f.calc_center_median().z > 0.1035,
                smooth=True)
    return p


# ============================================================================================ the list

PIECES = [
    # (builder, group, what, place (Unity, in words), origin, collider (the piece's solid, Unity local) or None)
    (rug_corner, 'Floor', 'flatweave rug 1.60 x 1.10, 10 mm, oat with rust and charcoal stripes near the ends (texture)',
     "the reading corner, where people sit softly: under a low table between two soft seats, never under chairs that slide. "
     "The café has no corner like that since the redesign; a free one: the front-west corner beside the fiddle-leaf fig, "
     "about (-5.5, 0, 2.0), long side along x (clear of the fig at (-6.85, 0, 0.75) and the banquette's south end at z 3.62). "
     "On the floor, no turn needed", 'bottom centre', None),
    (rug_entry, 'Floor', 'plain coir entry mat 1.20 x 0.70 x 0.017 on a black PVC back (texture)',
     "inside the door. CK_EntryMosaic covers z 0.20-1.40 from the threshold, so the mat lies on the door's inner side past "
     "it, its near edge on the mosaic's inner edge: about (0, 0, 1.78), long side along x. If the mosaic should stay the "
     "only entry floor, leave this one out (Environment's call)", 'bottom centre', None),
    (till_mat, 'Floor', 'black anti-fatigue mat 0.914 x 0.610 x 0.0127 (36 x 24 x 1/2 in), bevelled, drainage holes (texture)',
     "behind the till where Ace stands: CounterStandPoint is at (-0.26, 0, 14.54); about (-0.20, 0, 14.62), long side along "
     "x, on the floor where the bench and drink mats lie (their y is 0.0165)", 'bottom centre', None),
    (napkin_dispenser, 'Tables', 'chrome napkin dispenser, two-faced, rounded, 0.132 x 0.080 x 0.120, the stack in its windows',
     "one per table, on the top (the 2-tops and the communal table are 0.75 high): at the table's middle or its wall end, "
     "clear of every seat's cup spot (TableSeat.CupSpot); either face may face a seat", 'bottom centre (on the table top)',
     (-0.066, 0.066, -0.040, 0.040, 0.0, 0.120)),       # the chrome sleeve (24 Oct: 0.80): the half-pulled napkin is paper, not solid
    (condiment_station, 'Counter', 'self-serve condiment tray 0.60 x 0.30: napkins, lids, sleeves, sugar caddy, stirrers, milk jug',
     "on the front counter's stone top at the pickup end, between the intake shelf (its last tray ends at x 1.74) and the "
     "pastry case (x 3.2), about (2.85, 1.095, 13.50), turned 180 degrees so its front faces the customers (the water "
     "station's way)", 'bottom centre of the tray', 'tray'),
    (bin_station, 'Bins', "San Francisco's three-stream station 0.956 x 0.430 x 0.851: RECYCLING, COMPOST, LANDFILL (labels textured)",
     "inside, near the door where leaving guests pass, its back to a wall: e.g. against the front wall's inner face west of "
     "the door beside the olive tree, about (-2.75, 0, 0.24) with no turn (it faces the room), or on the west wall's free "
     "stretch (z 1.3-3.5) at about (-7.17, 0, 2.4) turned 90 degrees. It stands 7 cm above the 0.78 sill: fine in front of "
     "a window", 'bottom centre (the back at y +0.215)', 'box'),
    (street_bin, 'Street', "the city's green litter can, round, 0.676 x 1.0: grillwork, a dome lid, a hooded mouth",
     "outside on the pavement near the café's door, by the kerb, the mouth toward the pavement (no turn: it faces +z, "
     "turn it 180 degrees to face the café); clear of the patio, the bench and the A-frame at (3.48, 0, -2.32)",
     'bottom centre', 'box'),
    (laptop_open, 'People', 'plain 14 in laptop, open at 110 degrees, the screen on, no brand',
     "on a table in front of a seated patron, its front (the keyboard's edge) toward the seat, 6-10 cm in from the table's "
     "edge; it takes the place of the cup spot's neighbourhood, not the spot itself", 'bottom centre of the base',
     'box'),
    (laptop_closed, 'People', 'the same laptop, shut', "on a table (a patron arriving or packing up), or on a seat",
     'bottom centre of the base', 'box'),
    (tote_bag, 'People', 'natural canvas tote 0.39 x 0.18 x 0.29, slumped, its handles flopped',
     "at a seated patron's chair foot, on the floor, leaning: e.g. 0.30 m to the side of the chair's centre, turned to taste",
     'bottom centre', None),
    (coat_on_chair, 'People', 'slate-blue chore coat hung over a chair back by its shoulders, corduroy collar',
     "a child of the chair: on a CK_Chair_Cafe at local (0, 0, 0), rotation 0; on a CK_Chair_Spindle at local "
     "(0, -0.0324, 0.0208), rotation 0. It hangs behind the chair (the walking side), 0.31 m deep at the hem",
     "the chair's origin (the floor under the chair's centre): built on CK_Chair_Cafe's top rail", None),
    (kids_drawing, 'Wall', "a child's crayon thank-you drawing of the café, US letter landscape, pinned (texture)",
     "on CK_NoticeBoard (the east pier, (7.4, 1.15, 3.5)): a child of the board at local (-0.270, 0.042, 0.0150), turned "
     "3 degrees about the board's normal. It covers the foot of the watch flyer (4.6 cm) and nothing else; its pin is "
     "on bare cork", 'bottom centre of the back (the back at y = 0)', None),
    (left_cup, 'Tables', "the café's paper cup (the game's) left behind with a black sip lid, 0.092 x 0.125",
     "on a table at a seat's cup spot (TableSeat.CupSpot), for a moment after its guest leaves", 'bottom centre', None),
]


REFERENCES = {
    'LI_Rug_Corner': ['https://www.bunnings.com.au/aura-flatwoven-designer-wool-rug-6237-ivory-black-110x160cm_p1107701',
                      'https://www.ikea.com/gb/en/p/-40456757/'],
    'LI_Rug_Entry': ['https://www.coba.com/za/product/coir'],
    'LI_TillMat': ['https://www.restaurantware.ae/products/serve-secure-beveled-edge-floor-mat-black-2x3-foot-1-count-box',
                   'https://wincous.com/products/rbm-35k'],
    'LI_NapkinDispenser': ['https://www.sustainablesupply.com/products/palmer-fixture-table-top-minifold-napkin-dispenser-bs-nd0055-13-c1966647',
                           'https://www.gofoodservice.com/p/dixie-51602'],
    'LI_CondimentStation': ['https://demo.dealercommander.com/ORGANIZER-CF-CNDMNT-14-BK/EMS-MIND-READER-LLC/EMSCOMORG02BLK/details.php',
                            'https://argo.webstaurantstore.com/american-metalcraft-pws4-4-natural-poplar-wood-sugar-caddy/124PWS4.html',
                            'https://sabert.com/sites/default/files/catalog-files/Kraft-CupSleeves.pdf',
                            'https://shop.boelter.com/kitchen-supplies-s/steamtable-accessories/plastic-lids-acces/cups-lids/lid-plas-dome-sip-f-12-20oz-hot-cup-blk.html',
                            'https://imperialdade.com/catalog/product-detail/coffee-coffee-stirrer-5-5x0-2x0-04-in-wood-natural-wrapped-1000-count-pack-10-packs-case-10000-count-case-packnwood-210SPATBE?id=114572',
                            'https://www.katom.com/482-83706.html'],
    'LI_BinStation': ['https://uk.glasdon.com/recycling-bins/triple-recycling-units/nexus-evolution-recycling-stations/nexus-evolution-trio-recycling-bin',
                      'https://sfenvironment.org/mandatory-recycling-and-composting-ordinance',
                      'https://missionlocal.org/2009/09/newsoms-trash-math-fewer-cans-less-trash/'],
    'LI_StreetBin': ['https://forms-surfaces.com/urban-renaissance-litter-recycling-receptacle',
                     'https://www.kqed.org/news/11906815/the-sordid-saga-of-san-franciscos-trash-cans',
                     'https://sfgov.org/arts/sites/default/files/SFTRASHCAN_PRES_092120.pdf'],
    'LI_Laptop_Open': ['https://www.dell.com/en-ie/shop/laptop-computers-2-in-1-pcs/dell-14-laptop/spd/dell-dc14255-laptop'],
    'LI_Laptop_Closed': ['https://www.dell.com/en-ie/shop/laptop-computers-2-in-1-pcs/dell-14-laptop/spd/dell-dc14255-laptop'],
    'LI_ToteBag': ['https://www.walmart.com/ip/6003288447'],
    'LI_Coat_OnChair': ['https://www.bostongeneralstore.com/pages/le-laboureur-cotton-chore-coat-size-chart',
                        'Tools/Blender/cafe_seating.py (chair_cafe, chair_spindle: the two top rails, measured)'],
    'LI_KidsDrawing': ['US letter, 8.5 x 11 in (ANSI A)',
                       'Tools/Blender/cafe_signs.py (notice_board: the board, its flyers and its push pins)'],
    'LI_LeftCup': ['Assets/Scripts/DrinkLiquidVisual.cs (BuildShell: the cup the game serves in; PaperColour)',
                   'https://shop.boelter.com/kitchen-supplies-s/steamtable-accessories/plastic-lids-acces/cups-lids/lid-plas-dome-sip-f-12-20oz-hot-cup-blk.html'],
}


# ============================================================================================ build and audit

def collider(ob, kind):
    """The piece's solid, in the piece's own Unity frame ({centre, size}, cafe_seating.json's form), or None."""
    if kind is None:
        return None
    if isinstance(kind, tuple):                         # a body named in Blender's frame (x0, x1, y0, y1, z0, z1)
        x0, x1, y0, y1, z0, z1 = kind
    else:
        st = fb.stats(ob)
        (x0, y0, z0), (x1, y1, z1) = st['min'], st['max']
    if kind == 'tray':                                  # the condiment tray only (the things on it have no solid)
        z1 = 0.030
    c = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    s = (x1 - x0, y1 - y0, z1 - z0)
    return {'centre': [round(-c[0], 4), round(c[2], 4), round(-c[1], 4)], 'size': [round(s[0], 4), round(s[2], 4), round(s[1], 4)]}


def build_all():
    """Every piece, finished and audited: [{object, group, what, place, origin, collider, islands, floating, coplanar,
    degenerate, below, tris, size, min, max, materials, labels}]."""
    import cafe_seating as cs                           # its coplanar check (Rule 5), imported, never edited
    out = []
    for builder, group, what, place, origin, coll in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        same, opp, ex = cs.coplanar_overlaps(ob)
        degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-9)
        out.append({'object': ob, 'group': group, 'what': what, 'place': place, 'origin': origin,
                    'collider': collider(ob, coll), 'islands': n, 'floating': floating, 'coplanar_same': same,
                    'coplanar_opposite': opp, 'coplanar_examples': ex, 'degenerate': degenerate,
                    'turned': prop.turned, 'flipped_open': prop.flipped_open, 'labels': dict(prop.labels), **st})
    return out
