"""
The café full from above, part 1: the repair side and Ace's two private story pieces (14 Oct 2026). Art Direction's brief
B3 (specs/art-bench-and-cafe.md, R1-R5, S1-S2) and his list of 14 Oct: "paper, bins, post it notes, tape, ruler, coffee
cups, to go cups, coffee syrup ... a bunch of parts in a bin under a desk near ace". Built on fixit_blender.py, hero_lib.py
and cafe_livedin.py's LivedPiece (smooth parts, 0..1 textured face sets), the house way: real sizes, flat-shaded, one
chamfer, metallic 0, nothing floating, no two faces in one plane.

  LI_PickupRow      R1  seven finished repairs waiting on the back bar under the drawer wall, each bagged, wrapped or
                        tagged: a cherry-red table radio (the row's one accent), a desk fan in a kraft bag, headphones in a
                        paper bag, a game controller in a zip bag, a kettle, a desk lamp wrapped in paper lying down, a small
                        speaker; a manila tag on a string at each (names on LI_Tags.png, PLACEHOLDER COPY)
  LI_DeskCorner     R2  Ace's end of the repair bench: a 24-drawer small-parts cabinet, a spool rack (red, black, green
                        wire, silver solder), a yellow-holstered multimeter with its red and black leads, a repair-ticket
                        pad and pen, a yellow post-it pad, two post-its on the glass screen's Ace side, masking tape, a
                        30 cm steel rule along Ace's edge, Ace's green enamel mug
  LI_DonorParts     R3  under the bench, in the knee space: a grey tote heaped with donor parts (two old phones face
                        down, a remote, a router, a tangle of cable, a keyboard's corner) and two stacked open-front bins
  LI_Whiteboard     R4  "this week": a 0.90 x 0.60 whiteboard in a black frame over the drawer wall, marker notes
                        (LI_Whiteboard.png, PLACEHOLDER COPY), three magnets, a yellow and a pink post-it, a clipped invoice
  LI_PartsDelivery  R5  a half-open shipping box (kraft, brown tape, a packing slip, bubble wrap showing) and two small
                        parts boxes, on the front counter's west end
  LI_MortgageLetter S1  the mortgage statement, opened and flattened, the overdue line circled in red (LI_Mortgage.png),
                        under the till's corner on Ace's side (fictional lender, PLACEHOLDER COPY)
  LI_FinalNotice    S2  the FINAL NOTICE taped up by two corners, its envelope with the red stamp pinned beside it
                        (LI_FinalNotice.png), inside the back door

THE FRAME (cafe_bar.json's): every piece is placed with rotation (0, 180, 0); then Blender X/Y read as world x/z offsets
from the piece's origin, and a front built toward -Y faces the room (-z). Origin: the bottom centre (a wall piece: the
bottom centre of its back, the back at y = 0). Unity (x, y, z) = Blender (-x, z, -y) inside the piece.

Cut, by the purpose rule: no phones, watches or cameras in the pickup row (they would read as jobs: Art's flag); no
brands; no cables running off the bench (nothing to plug in); the router's lights; the tags' strings beyond one loop.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import cafe_livedin as li  # noqa: E402
from mathutils import Vector  # noqa: E402

TEX = 'Assets/Art/Textures/Cafe/'

EXISTING = {   # the project's own, by exact name (Unity keeps its values)
    'CF_Kraft': ('#B08A5E', 0.15), 'CF_Black': ('#2B2724', 0.35), 'DC_Steel': ('#93908A', 0.50),
    'DC_SteelDark': ('#4E4B47', 0.50), 'DC_Chalk': ('#E8E4D8', 0.05), 'DC_Ceramic': ('#F2EFE8', 0.60),
    'DC_Sticker_Green': ('#2E7D5B', 0.45), 'LI_CupPaper': ('#E8D9B3', 0.08), 'LI_RecycleBlue': ('#2C67A3', 0.35),
    'CF_PinRed': ('#B5402F', 0.60), 'CF_PinYellow': ('#D9A93A', 0.60), 'T2_Brass': ('#B8933F', 0.60),
}
NEW = {   # (sRGB hex, smoothness, texture or None, why)
    'CF_Manila':       ('#E8D9A8', 0.10, 'LI_Tags.png', "the repair tags' manila card; names handwritten (atlas)"),
    'CF_Whiteboard':   ('#F4F3EF', 0.55, 'LI_Whiteboard.png', "the whiteboard's face with this week's marker notes"),
    'LI_Mortgage':     ('#F4F1EA', 0.05, 'LI_Mortgage.png', "the mortgage statement's page, the overdue line circled"),
    'LI_FinalNotice':  ('#F4F1EA', 0.05, 'LI_FinalNotice.png', "the final notice's page and its envelope (one atlas)"),
    'LI_RadioRed':     ('#8E2F2A', 0.35, None, "the table radio's muted cherry red: the pickup row's one accent"),
    'LI_PaperWhite':   ('#ECE8DE', 0.06, None, "white paper bags and wrapping"),
    'LI_ZipBag':       ('#D5DEE0', 0.70, None, "a clear zip bag seen as a cool pale sheen"),
    'LI_Plastic':      ('#5E5A55', 0.35, None, "grey plastic: the controller, the router, the tote, the remote"),
    'LI_Yellow':       ('#E2B33C', 0.35, None, "the multimeter's yellow holster"),
    'LI_PostItYellow': ('#F5D76E', 0.10, None, "a yellow post-it pad and notes"),
    'LI_PostItPink':   ('#F2A7B8', 0.10, None, "a pink post-it"),
    'LI_Tape':         ('#E6D3A3', 0.20, None, "masking tape"),
    'LI_TapeBrown':    ('#9C6B3A', 0.40, None, "brown packing tape"),
    'LI_WireRed':      ('#A8332B', 0.40, None, "red hook-up wire, the red probe lead"),
    'LI_WireGreen':    ('#3F7A3A', 0.40, None, "green hook-up wire (a grass green, not Ace's)"),
    'LI_BinRed':       ('#A33A2E', 0.35, None, "the red open-front parts bin"),
    'LI_BubbleWrap':   ('#DCE4E6', 0.60, None, "bubble wrap"),
    'LI_EsdGrey':      ('#4A4F55', 0.20, None, "the grey anti-static pad under Ace's desk corner (the cluster's base from above)"),
}


def M(name):
    if name in EXISTING:
        hexstr, smooth = EXISTING[name]
    else:
        hexstr, smooth = NEW[name][:2]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


Piece = li.LivedPiece
box = li.box


def rb(x0, x1, y0, y1, z0, z1, c=0.002):
    """A box from its boundaries with one chamfer (the house bevel), c clamped to the box."""
    return fb.rbox(x0, x1, y0, y1, z0, z1, bevel=min(c, (x1 - x0) / 3, (y1 - y0) / 3, (z1 - z0) / 3))


def moved(bm, x, y, z=0.0, rot=0.0):
    if rot:
        fb.turn(bm, rot, 'Z')
    return hl.moved(bm, x, y, z)


def cyl(x, y, z0, z1, r, sides=12, r_top=None):
    return hl.moved(hl.revolve([(z0, r), (z1, r if r_top is None else r_top)], sides=sides), x, y, 0.0)


def open_box(x0, x1, y0, y1, z0, z1, t):
    """A box with no top (a tote, a bin, a carton) whose parts never share a plane: the floor inset 0.5 mm from the walls'
    outer faces, the front and back walls full width, the side walls between them, the walls' feet inside the floor."""
    bm = fb.zbox(x0 + 0.0005, x1 - 0.0005, y0 + 0.0005, y1 - 0.0005, z0, z0 + t)
    for part in (fb.zbox(x0, x1, y0, y0 + t, z0 + 0.0003, z1), fb.zbox(x0, x1, y1 - t, y1, z0 + 0.0003, z1),
                 fb.zbox(x0, x0 + t, y0 + t, y1 - t, z0 + 0.0003, z1 - 0.0004), fb.zbox(x1 - t, x1, y0 + t, y1 - t, z0 + 0.0003, z1 - 0.0004)):
        fb.merge(bm, part)
    return bm


def handle_arc(cx, cy, z0, z1, reach, axis='y', sign=-1, r=0.0055, n=7):
    """A C-shaped handle (a mug's, a jug's) as an arc of n points from z0 to z1, reaching out along -axis or +axis."""
    pts = []
    zc, rz = (z0 + z1) / 2, (z1 - z0) / 2
    for k in range(n):
        a = -math.pi / 2 + math.pi * k / (n - 1)
        out = reach * math.cos(a)
        z = zc + rz * math.sin(a)
        pts.append((cx + (sign * out if axis == 'x' else 0.0), cy + (sign * out if axis == 'y' else 0.0), z))
    import cafe_props as cp                      # its sweep carries the frame along the curve (hero_lib.sweep's flips at a C's middle)
    return cp.sweep_var(pts, [r] * len(pts), sides=6 if n > 5 else 5)


def _bag_ring(w, d, z, rc=0.008, per_long=6, per_short=3, per_corner=2):
    """The points of a paper bag's section: a rounded rectangle w x d, the same count and order at every height."""
    hx, hy = w / 2 - rc, d / 2 - rc
    pts = []
    for (ox, oy, a0) in ((hx, -hy, -90.0), (hx, hy, 0.0), (-hx, hy, 90.0), (-hx, -hy, 180.0)):
        for k in range(per_corner + 1):
            a = math.radians(a0 + 90.0 * k / per_corner)
            pts.append([ox + rc * math.cos(a), oy + rc * math.sin(a), z])
        # the straight side after this corner, without its ends (the next corner gives them)
        nxt = {-90.0: (hx, hy), 0.0: (-hx, hy), 90.0: (-hx, -hy), 180.0: (hx, -hy)}[a0]
        a_end = math.radians(a0 + 90.0)
        p0 = (ox + rc * math.cos(a_end), oy + rc * math.sin(a_end))
        p1 = (nxt[0] + rc * math.cos(a_end), nxt[1] + rc * math.sin(a_end))
        n = per_short if a0 in (-90.0, 90.0) else per_long
        for m in range(1, n + 1):
            f = m / (n + 1)
            pts.append([p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f, z])
    return pts


def paper_bag(w, d, h, seed, t=0.0015, creases=(-0.22, 0.18)):
    """
    A paper bag standing open, its walls paper-thin, its top crumpled (24 Oct 2026, art audit a41: the old bag was "a box
    and a folded flap", a crisp carton; R2: "crumple and roll the bag tops, taper the sides, add two or three creases").
    The walls bulge a little and narrow toward a ragged, crumpled mouth; the two side gussets fold in toward the top, as a
    filled paper bag's do; two creases run down the front; the rim wanders up and down by about a centimetre. One closed
    solid: the outside up to the rim, over it, and the inside down to the floor, so looking into the bag shows its
    inside, never through it. Whatever it holds stands in it (the fan's guard, the headband), poking out of the mouth.
    """
    import random
    rng = random.Random(seed)
    base = _bag_ring(w, d, 0.0)
    n = len(base)
    rim_wobble = [rng.uniform(-1.0, 1.0) for _ in range(n)]
    rim_wobble = [(rim_wobble[i - 1] + 2 * rim_wobble[i] + rim_wobble[(i + 1) % n]) / 4 for i in range(n)]   # soften it
    crumple = [rng.uniform(-1.0, 1.0) for _ in range(n)]
    # (height fraction, width scale, depth scale, crumple amount in metres)
    levels = [(0.0, 1.00, 1.00, 0.0), (0.03, 1.01, 1.02, 0.0), (0.30, 1.03, 1.06, 0.001), (0.60, 1.02, 1.05, 0.002),
              (0.82, 0.97, 0.94, 0.005), (0.94, 0.93, 0.86, 0.008), (1.00, 0.92, 0.82, 0.010)]
    outer, inner = [], []
    for (f, sw, sd, cr) in levels:
        ring = _bag_ring(w * sw, d * sd, 0.0)
        out_ring, in_ring = [], []
        side_x = w * sw / 2 - 0.008                       # where the short sides' straight part starts (_bag_ring's rc)
        fold = max(0.0, min(1.0, (f - 0.45) / 0.55)) ** 1.5
        for i, (x, y, _) in enumerate(ring):
            if abs(x) >= side_x and fold > 0.0:           # the side gusset folding in: deepest at the side's middle
                x -= math.copysign(0.13 * d * fold * max(0.0, 1.0 - abs(y) / (0.5 * d * sd)), x)
            r = math.hypot(x, y) or 1.0
            ux, uy = x / r, y / r
            k = cr * crumple[i]
            # the creases: dents across the front face (-Y), a third of the way in, from a quarter of the way up
            for cx_ in creases:
                if y < 0 and f > 0.2:
                    k -= 0.004 * max(0.0, 1.0 - abs(x - cx_ * w) / (0.05 * w)) * min(1.0, (f - 0.2) * 4)
            z = f * h + (rim_wobble[i] * 0.011 if f >= 1.0 else 0.0)
            xo, yo = x + ux * k, y + uy * k
            out_ring.append((xo, yo, z))
            in_ring.append((xo - ux * t, yo - uy * t, z if f >= 1.0 else max(t, z)))
        outer.append(out_ring)
        inner.append(in_ring)
    loops = outer + [inner[-1]] + list(reversed(inner[1:-1])) + [[(x, y, t) for (x, y, _) in inner[0]]]
    return hl.tube(loops, cap_bottom=True, cap_top=True)


def wrapped_lamp(x0, x1, axis_z, seed=11, band=(0.33, 0.39)):
    """
    A desk lamp wrapped in paper, lying down along X (24 Oct 2026, art audit a41: it read as "a giant crayon or a
    baguette"; the fix: "twist the lamp's paper shut with its shade and base showing"). The paper wraps the arm from x0 to
    x1, crumpled, and is twisted shut round the lamp's steel at both ends, like a sweet's wrapper; the round base and the
    shade stand out beyond it (added by the caller). Returns (paper, tape): two closed solids along X, the tape a band of
    masking tape lying on the crumpled paper between band[0] and band[1] of the bundle's length (it follows the paper's
    own radius there, 1.5 mm out).
    """
    import random
    rng = random.Random(seed)
    sides = 12
    L = x1 - x0
    crumple = [rng.uniform(-1.0, 1.0) for _ in range(sides)]
    # (position along the bundle 0..1, radius, crumple fraction, twist turns of the lobes)
    prof = [(0.000, 0.010, 0.00, 0.00), (0.035, 0.013, 0.10, 0.08), (0.070, 0.019, 0.15, 0.16), (0.110, 0.032, 0.12, 0.22),
            (0.160, 0.043, 0.10, 0.25), (0.300, 0.046, 0.12, 0.25), (0.500, 0.044, 0.14, 0.25), (0.700, 0.046, 0.12, 0.25),
            (0.840, 0.043, 0.10, 0.25), (0.890, 0.032, 0.12, 0.30), (0.930, 0.019, 0.15, 0.38), (0.965, 0.013, 0.10, 0.46),
            (1.000, 0.010, 0.00, 0.50)]

    def radii(u, r, cf, tw):
        out = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            lobe = 0.18 * math.cos(6 * (a + tw * 2 * math.pi)) if 0.0 < u < 0.14 or 0.86 < u < 1.0 else 0.0   # the twisted gathers
            out.append(r * (1.0 + cf * crumple[(i + int(u * 7)) % sides] + lobe))
        return out

    table = [(u, radii(u, r, cf, tw)) for (u, r, cf, tw) in prof]

    def ring_at(u, scale=1.0):
        """The paper's surface at u, as the loft draws it (each point between its two rings)."""
        for (ua, ra), (ub, rb_) in zip(table, table[1:]):
            if ua <= u <= ub:
                f = (u - ua) / (ub - ua)
                return [(x0 + u * L, (ra[i] + (rb_[i] - ra[i]) * f) * scale * math.cos(2 * math.pi * i / sides),
                         axis_z + (ra[i] + (rb_[i] - ra[i]) * f) * scale * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        raise ValueError(u)

    paper = hl.tube([ring_at(u) for (u, _r) in table], cap_bottom=True, cap_top=True)
    tape = hl.tube([[(x, y_ * 1.035, z_) for (x, y_, z_) in ring_at(band[0])],
                    [(x, y_ * 1.035, z_) for (x, y_, z_) in ring_at(band[1])]], cap_bottom=True, cap_top=True)
    for v in tape.verts:                                  # 1.5 mm out from the paper all round (the z about the bundle's axis)
        v.co.z = axis_z + (v.co.z - axis_z) * 1.035
    return paper, tape


def tag_on(p, x, y, z, k, rot=0.0):
    """A manila tag lying flat (64 x 32 mm, the k-th name in LI_Tags.png) and its string's loop up to the item."""
    tw, th = 0.064, 0.032
    t = box(-tw / 2, tw / 2, -th / 2, th / 2, 0.0, 0.0006)
    fb.turn(t, rot, 'Z')
    hl.moved(t, x, y, z)
    u0, u1 = k / 8.0, (k + 1) / 8.0
    ang = math.radians(rot)
    p.textured('tag %d' % k, t, M('CF_Manila'), origin=(x - tw / 2 * math.cos(ang) + th / 2 * math.sin(ang),
                                                        y - tw / 2 * math.sin(ang) - th / 2 * math.cos(ang), z),
               u_axis=(math.cos(ang), math.sin(ang), 0), v_axis=(-math.sin(ang), math.cos(ang), 0), w=tw, h=th,
               rect=(u0, 0.0, u1, 1.0), edge=(0.04, 0.5), min_dot=0.5)
    string = hl.sweep([(x + 0.03 * math.cos(ang), y + 0.03 * math.sin(ang), z + 0.0006),
                       (x + 0.05 * math.cos(ang), y + 0.05 * math.sin(ang) + 0.01, z + 0.012),
                       (x + 0.06 * math.cos(ang), y + 0.06 * math.sin(ang) + 0.03, z + 0.03)], 0.0008, sides=4)
    p.add('tag string', string, M('DC_Chalk'))


# ============================================================================================ R1 the pickup row

ROW_LEN = 3.10


def pickup_row():
    p = Piece('LI_PickupRow', floor=True)
    kraft, white, red, black, steel, dark, plastic, zipbag = (M('CF_Kraft'), M('LI_PaperWhite'), M('LI_RadioRed'), M('CF_Black'),
                                                              M('DC_Steel'), M('DC_SteelDark'), M('LI_Plastic'), M('LI_ZipBag'))
    xs = [-1.30, -0.86, -0.44, -0.06, 0.34, 0.80, 1.30]          # item centres along the back bar
    tag_y = -0.115                                                # the tags lie at the front, toward the room (-Y)
    # 1. the table radio: a 0.30 x 0.14 x 0.18 cabinet, a woven grille, a dial window, two knobs, a carry handle
    x = xs[0]
    p.add('radio cabinet', rb(x - 0.15, x + 0.15, -0.07, 0.07, 0.0, 0.18, c=0.008), red)
    p.add('radio grille', rb(x - 0.13, x - 0.01, -0.073, -0.06, 0.03, 0.15, c=0.002), dark)
    p.add('radio dial', rb(x + 0.01, x + 0.13, -0.073, -0.06, 0.11, 0.15, c=0.002), M('LI_PaperWhite'))
    for kx in (x + 0.04, x + 0.10):
        p.add('radio knob', hl.moved(fb.turn(hl.revolve([(0.0, 0.011), (0.012, 0.011), (0.015, 0.009)], sides=10), 90, 'X'), kx, -0.068, 0.06), black)
    p.add('radio handle', hl.sweep([(x - 0.10, 0.0, 0.175), (x - 0.08, 0.0, 0.215), (x + 0.08, 0.0, 0.215), (x + 0.10, 0.0, 0.175)], 0.006, sides=6), black)
    tag_on(p, x + 0.05, tag_y, 0.0, 0, rot=-8)
    # 2. a desk fan in a kraft bag, its guard showing over the crumpled mouth (24 Oct 2026, a41: the bags were crisp boxes
    # with one flat flap; now paper_bag's open, slumped bag)
    x = xs[1]
    p.add('fan bag', hl.moved(paper_bag(0.28, 0.17, 0.20, seed=21), x, 0.0, 0.0), kraft)   # 0.17: its bulge keeps the row's 0.09 back
    p.add('fan guard', hl.moved(fb.turn(hl.revolve([(0.0, 0.11), (0.012, 0.115), (0.03, 0.10), (0.04, 0.05), (0.042, 0.0001)], sides=16), 90, 'X'), x, 0.01, 0.21), steel)
    tag_on(p, x - 0.05, tag_y, 0.0, 1, rot=6)
    # 3. headphones in a white paper bag, the headband over the crumpled mouth
    x = xs[2]
    p.add('headphone bag', hl.moved(paper_bag(0.22, 0.12, 0.19, seed=33, creases=(-0.20, 0.24)), x, 0.0, 0.0), white)
    arc = [(x + 0.07 * math.cos(a), 0.0, 0.17 + 0.075 * math.sin(a)) for a in (math.radians(d) for d in range(0, 181, 20))]
    p.add('headband', hl.sweep(arc, 0.008, sides=6), black)
    tag_on(p, x + 0.03, tag_y, 0.0, 2, rot=-4)
    # 4. a game controller in a zip bag: the flat bag, its blue zip, the controller lying in it. 24 Oct 2026, a42: "so
    # small it looks like a tiny dark gun on a card". Now at a real pad's size (16 x 10.5 cm, 4.75 cm over its sticks, as
    # the common ones are), with the two sticks, a cross pad and four face buttons (cream: no maker's colours) that say
    # "controller" from the counter; the bag grew to hold it (26 x 17 cm).
    x = xs[3]
    p.add('zip bag', rb(x - 0.13, x + 0.13, -0.085, 0.088, 0.0, 0.006, c=0.002), zipbag)
    p.add('zip', rb(x - 0.13, x + 0.13, 0.073, 0.086, 0.004, 0.009, c=0.001), M('LI_RecycleBlue'))
    ctrl = rb(-0.072, 0.072, -0.022, 0.040, 0.005, 0.036, c=0.014)
    for side in (1, -1):
        grip = rb(0.030, 0.080, -0.078, 0.000, 0.0058, 0.0330, c=0.016)
        fb.turn(grip, 14.0, 'Z', pivot=(0.055, -0.010, 0.0))      # the grip's far end swings out, as a pad's horns do
        if side < 0:                                              # the left grip: the right one mirrored across x = 0
            bmesh.ops.scale(grip, vec=(-1.0, 1.0, 1.0), verts=grip.verts[:])
            bmesh.ops.reverse_faces(grip, faces=grip.faces[:])
        fb.merge(ctrl, grip)
    p.add('controller', moved(ctrl, x, 0.008, 0.0, rot=10), plastic)
    rot10 = math.radians(10.0)

    def on_pad(px, py):                                   # a point on the controller's face, turned with it
        return (x + px * math.cos(rot10) - py * math.sin(rot10), 0.008 + px * math.sin(rot10) + py * math.cos(rot10))
    for (px, py) in ((-0.040, 0.016), (0.020, -0.006)):
        sx_, sy_ = on_pad(px, py)
        p.add('thumbstick', hl.moved(hl.revolve([(0.034, 0.008), (0.042, 0.008), (0.044, 0.0115), (0.047, 0.0115), (0.0475, 0.009)], sides=12), sx_, sy_, 0.0), black)
    dx_, dy_ = on_pad(-0.018, -0.008)
    for rot in (0.0, 90.0):
        lift = 0.0002 if rot else 0.0                     # the two arms' faces never in one plane
        arm = rb(-0.0095, 0.0095, -0.0032, 0.0032, 0.033 + lift, 0.039 + lift, c=0.0008)
        fb.turn(arm, rot + 10.0, 'Z')
        p.add('cross pad', hl.moved(arm, dx_, dy_, 0.0), dark)
    for (px, py) in ((0.046, 0.004), (0.056, 0.014), (0.036, 0.014), (0.046, 0.024)):
        bx_, by_ = on_pad(px, py)
        p.add('face button', cyl(bx_, by_, 0.033, 0.0395, 0.0046, sides=10), M('DC_Chalk'))
    tag_on(p, x + 0.09, tag_y - 0.02, 0.0, 3, rot=12)
    # 5. a kettle: a tapered steel body, a lid and knob, a handle, a spout
    x = xs[4]
    p.add('kettle body', hl.moved(hl.revolve([(0.0, 0.075), (0.005, 0.080), (0.16, 0.068), (0.175, 0.05), (0.18, 0.03)], sides=16), x, 0.0, 0.0), steel)
    p.add('kettle knob', cyl(x, 0.0, 0.178, 0.198, 0.012, sides=10), black)
    p.add('kettle handle', hl.sweep([(x - 0.065, 0.0, 0.15), (x - 0.06, 0.0, 0.235), (x + 0.05, 0.0, 0.235), (x + 0.06, 0.0, 0.17)], 0.010, sides=6), black)
    p.add('kettle spout', hl.sweep([(x + 0.07, 0.0, 0.05), (x + 0.10, 0.0, 0.10), (x + 0.115, 0.0, 0.14)], 0.012, sides=8), steel)
    tag_on(p, x - 0.03, tag_y, 0.0, 4, rot=-10)
    # 6. a desk lamp wrapped in paper, lying down (24 Oct 2026, a41: it was a smooth cream log with a green cone at one
    # end, "a giant crayon or a baguette"). Now the paper is crumpled round the arm and twisted shut at both ends, and
    # what it can't wrap shows: the lamp's round black base standing on its edge at one end, its green shade at the other,
    # each on a short steel stem out of a twist; a band of masking tape holds the paper. The lamp's axis is at the base's
    # centre, 52 mm up: the base and the shade's rim stand on the bar and the paper clears it by a few millimetres.
    x = xs[5]
    az = 0.052
    paper, tape = wrapped_lamp(x - 0.205, x + 0.175, az)
    p.add('wrapped lamp', paper, white)
    base = hl.revolve([(0.0, 0.046), (0.004, 0.052), (0.022, 0.052), (0.026, 0.046)], sides=20)
    p.add('lamp base', hl.moved(fb.turn(base, 90, 'Y'), x - 0.262, 0.0, az), black)
    for (s0, s1) in ((-0.238, -0.190), (0.160, 0.196)):
        stem = hl.revolve([(0.0, 0.0075), (s1 - s0, 0.0075)], sides=10)
        p.add('lamp stem', hl.moved(fb.turn(stem, 90, 'Y'), x + s0, 0.0, az), steel)
    shade = hl.revolve([(0.0, 0.015), (0.010, 0.022), (0.030, 0.037), (0.050, 0.046), (0.068, 0.0505), (0.074, 0.052),
                        (0.074, 0.0492), (0.068, 0.0478), (0.050, 0.0432), (0.030, 0.0342), (0.012, 0.0195), (0.003, 0.0125)],
                       sides=18)                          # a bell, as a desk lamp's shade is
    p.add('lamp shade', hl.moved(fb.turn(shade, 90, 'Y'), x + 0.190, 0.0, az), M('DC_Sticker_Green'))
    p.add('tape band', tape, M('LI_Tape'))
    tag_on(p, x - 0.02, tag_y, 0.0, 5, rot=4)
    # 7. a small speaker: a cloth-fronted box on its end
    x = xs[6]
    p.add('speaker', rb(x - 0.08, x + 0.08, -0.05, 0.05, 0.0, 0.20, c=0.010), black)
    p.add('speaker cloth', rb(x - 0.07, x + 0.07, -0.054, -0.045, 0.012, 0.19, c=0.003), plastic)
    tag_on(p, x - 0.06, tag_y, 0.0, 6, rot=-6)
    return p


# ============================================================================================ R2 Ace's desk corner

DESK_W = 1.25


def desk_corner():
    """Origin: the bench top's surface under the middle of x -4.40..-3.15, z 14.08 (y 0 = the top, 1.005)."""
    p = Piece('LI_DeskCorner', floor=True)
    black, steel, dark, clear = M('CF_Black'), M('DC_Steel'), M('DC_SteelDark'), M('LI_ZipBag')
    # the grey anti-static desk pad the corner sits on: real on an electronics bench, and the cluster's shared base from
    # above (Art's 10-pixel rule: small props read only as a cluster on a base >= 35 x 25 cm, >= 20 L* from the wood)
    p.add('ESD pad', rb(-0.60, 0.60, -0.27, 0.27, 0.0, 0.002, c=0.0008), M('LI_EsdGrey'))
    Z = 0.002
    # the 24-drawer small-parts cabinet at the east end (world x -3.15 is local +0.625), its drawers facing Ace (+Y)
    cx, cy = 0.48, 0.05
    p.add('cabinet', rb(cx - 0.14, cx + 0.14, cy - 0.11, cy + 0.11, Z, Z + 0.22, c=0.004), M('LI_Plastic'))
    for i in range(4):
        for j in range(6):
            dx0 = cx - 0.13 + i * 0.065
            dz0 = Z + 0.012 + j * 0.034
            p.add('drawer', rb(dx0, dx0 + 0.058, cy + 0.106, cy + 0.116, dz0, dz0 + 0.028, c=0.0015), clear)
    # the spool rack: a black wire frame, four spools on a rod
    sx, sy = 0.17, 0.12
    p.add('spool rack', rb(sx - 0.10, sx + 0.10, sy - 0.04, sy + 0.04, Z, Z + 0.008, c=0.002), black)
    for side in (-1, 1):
        p.add('rack upright', rb(sx + side * 0.095 - 0.004, sx + side * 0.095 + 0.004, sy - 0.004, sy + 0.004, Z + 0.002, Z + 0.10, c=0.001), black)
    p.add('spool rod', hl.moved(fb.turn(hl.revolve([(-0.10, 0.004), (0.10, 0.004)], sides=8), 90, 'Y'), sx, sy, Z + 0.085), steel)
    for k, mat in enumerate(('LI_WireRed', 'CF_Black', 'LI_WireGreen', 'DC_Steel')):
        spool = hl.revolve([(-0.018, 0.042), (-0.014, 0.042), (-0.014, 0.034), (0.014, 0.034), (0.014, 0.042), (0.018, 0.042)], sides=14)
        fb.turn(spool, 90, 'Y')
        p.add('spool', hl.moved(spool, sx - 0.066 + k * 0.044, sy, Z + 0.085), M(mat))
    # the multimeter in its yellow holster, leads coiled beside it
    mx, my = -0.05, 0.10
    p.add('multimeter', rb(mx - 0.045, mx + 0.045, my - 0.09, my + 0.09, Z, Z + 0.05, c=0.008), M('LI_Yellow'))
    p.add('meter face', rb(mx - 0.035, mx + 0.035, my - 0.02, my + 0.075, Z + 0.045, Z + 0.053, c=0.002), dark)
    p.add('meter dial', cyl(mx, my - 0.045, Z + 0.045, Z + 0.058, 0.018, sides=12), black)
    for k, mat in enumerate(('LI_WireRed', 'CF_Black')):
        coil = [(mx - 0.13 + 0.045 * math.cos(a) + k * 0.012, my - 0.02 + 0.03 * math.sin(a), Z + 0.0024 + 0.0035 * (a / (2 * math.pi)))
                for a in (2 * math.pi * t / 16 * 2 for t in range(17))]
        p.add('lead', hl.sweep(coil, 0.0022, sides=5), M(mat))
    # the repair-ticket pad and a pen; the yellow post-it pad; masking tape; the steel rule along Ace's edge; the mug
    p.add('ticket pad', rb(-0.27, -0.12, 0.02, 0.12, Z, Z + 0.008, c=0.001), M('LI_PaperWhite'))
    p.add('ticket stub', rb(-0.265, -0.125, 0.10, 0.118, Z + 0.0078, Z + 0.0095, c=0.0005), M('LI_PostItPink'))
    pen = hl.sweep([(-0.25, 0.0, Z + 0.0046), (-0.12, 0.03, Z + 0.0046)], 0.0045, sides=8)
    p.add('pen', pen, black)
    p.add('post-it pad', rb(-0.36, -0.285, -0.06, 0.015, Z, Z + 0.012, c=0.0008), M('LI_PostItYellow'))
    tape = hl.ring_wall(Z, Z + 0.019, 0.019, 0.0375, sides=16)
    p.add('masking tape', hl.moved(tape, -0.45, 0.05, 0.0), M('LI_Tape'))
    p.add('steel rule', rb(-0.55, -0.25, 0.235, 0.26, Z, Z + 0.0012, c=0.0003), steel)
    mug = hl.revolve([(0.0, 0.040), (0.004, 0.042), (0.090, 0.042), (0.095, 0.044), (0.095, 0.036), (0.012, 0.034)], sides=14, cap_top=False)
    p.add('mug', hl.moved(mug, 0.02, -0.17, Z), M('DC_Sticker_Green'))
    p.add('mug handle', handle_arc(0.058, -0.17, Z + 0.025, Z + 0.075, 0.020, axis='x', sign=1), M('DC_Sticker_Green'))
    # ---- 24 Oct 2026 (the art audit; his answer "Yes, do that"). Added after everything else, so the material slots keep
    # their order: these use only materials the corner already had.
    # a29, the steel rule ("a bare light-grey bar with no marks. It reads as a stray strip"): black marks 0.5 mm proud
    # (less fights the steel's face in the depth buffer from the overhead camera), a tick every 5 mm along its far edge (centimetres longer, every 5 cm longest) and 5 10 15 20 25 under them,
    # read by Ace: his up is -Y and his right -X, so 0 is at his left, the +X end
    rz = Z + 0.0012
    for k in range(1, 60):
        x = -0.25 - k * 0.005
        length = 0.0080 if k % 10 == 0 else (0.0050 if k % 2 == 0 else 0.0030)
        p.add('rule tick', fb.zbox(x - 0.00035, x + 0.00035, 0.2358, 0.2358 + length, rz - 0.0001, rz + 0.0005), black)
    for k in range(1, 6):
        num = hl.text_mesh(str(5 * k), 0.0085, 0.0006)
        if num is None:
            continue
        bmesh.ops.triangulate(num, faces=num.faces[:])   # convex faces, so the coplanar audit judges the glyphs truly
        fb.turn(num, 180.0, 'Z')
        p.add('rule number', hl.moved(num, -0.25 - 0.05 * k, 0.2560, rz - 0.0001), black)
    # a44, the cabinet's back, which is what the repair bar's stools see ("a plain grey box sits at a seated guest's eye
    # level"): a moulded rim round its edge, two ribs across the panel, the two keyhole slots it hangs by, and Ace's label
    by = cy - 0.11                                       # the back face, toward the room (-Y)
    x0, x1, z0, z1 = cx - 0.1395, cx + 0.1395, Z + 0.0005, Z + 0.2195     # half a millimetre inside the cabinet's edges
    rim, proud = 0.012, 0.004
    for (a0, a1, b0, b1) in ((x0, x1, z1 - rim, z1), (x0, x1, z0, z0 + rim), (x0, x0 + rim, z0 + rim, z1 - rim),
                             (x1 - rim, x1, z0 + rim, z1 - rim)):
        p.add('cabinet rim', rb(a0, a1, by - proud, by, b0, b1, c=0.0015), M('LI_Plastic'))
    for zr in (Z + 0.077, Z + 0.152):
        p.add('cabinet rib', rb(x0 + rim, x1 - rim, by - 0.002, by + 0.001, zr - 0.004, zr + 0.004, c=0.001), M('LI_Plastic'))
    for kx in (x0 + 0.040, x1 - 0.040):
        p.add('keyhole slot', rb(kx - 0.003, kx + 0.003, by - 0.0006, by + 0.0005, Z + 0.180, Z + 0.196, c=0.0008), black)
    p.add('cabinet label', rb(cx - 0.065, cx + 0.065, by - 0.0015, by + 0.0005, Z + 0.163, Z + 0.203, c=0.0006), M('LI_PaperWhite'))
    word = hl.text_mesh('PARTS', 0.024, 0.0007)
    if word is not None:
        bmesh.ops.triangulate(word, faces=word.faces[:])
        fb.turn(word, 90.0, 'X')                         # face the room (-Y), its up +Z, reading +X from the stools
        zs_ = [v.co.z for v in word.verts]
        p.add('label word', hl.moved(word, cx, by - 0.0015, Z + 0.183 - (min(zs_) + max(zs_)) / 2), black)
    return p


def glass_notes():
    """R2's two post-its on the glass screen's Ace side, one overlapping the other's corner (a piece of their own: the glass
    is CK_RepairBench's, so they could touch nothing in the desk corner). A wall piece facing Ace: origin at the bottom
    centre of the yellow note's back, the back at y = 0 on the glass, the notes toward +Y (placed with rotation 0)."""
    p = Piece('LI_GlassNotes', floor=False)
    p.add('post-it', fb.zbox(-0.0375, 0.0375, 0.0, 0.0008, 0.0, 0.075), M('LI_PostItYellow'))
    pink = fb.zbox(-0.0375, 0.0375, 0.0005, 0.0013, -0.0375, 0.0375)
    fb.turn(pink, -6, 'Y')
    p.add('post-it', hl.moved(pink, 0.062, 0.0, 0.045), M('LI_PostItPink'))
    return p


# ============================================================================================ R3 the donor parts under the bench

def donor_parts():
    """Origin: the floor between the tote (local x -0.175) and the bins (+0.175), world (-3.375, 0, 13.88)."""
    p = Piece('LI_DonorParts', floor=True)
    grey, black, plastic = M('LI_Plastic'), M('CF_Black'), M('DC_SteelDark')
    tx = -0.175
    p.add('tote', open_box(tx - 0.20, tx + 0.20, -0.15, 0.15, 0.0, 0.22, 0.006), grey)
    # the heap: phones face down, a remote, a router, a keyboard corner, a tangle of cable
    heap = [((tx - 0.10, -0.06, 0.19, 18), rb(-0.035, 0.035, -0.072, 0.072, 0.0, 0.009, c=0.003), black),
            ((tx - 0.02, 0.05, 0.198, -25), rb(-0.035, 0.035, -0.072, 0.072, 0.0, 0.009, c=0.003), M('LI_RadioRed')),
            ((tx + 0.10, -0.02, 0.19, 70), rb(-0.025, 0.025, -0.09, 0.09, 0.0, 0.022, c=0.006), black),
            ((tx + 0.03, -0.05, 0.205, 8), rb(-0.11, 0.11, -0.07, 0.07, 0.0, 0.03, c=0.005), M('DC_Chalk')),
            ((tx - 0.07, 0.07, 0.222, 32), rb(-0.13, 0.04, -0.06, 0.06, 0.0, 0.02, c=0.004), plastic)]
    for (hx, hy, hz, rot), bm, mat in heap:
        p.add('donor part', moved(bm, hx, hy, hz, rot=rot), mat)
    for k in range(3):
        loop = [(tx + 0.06 * math.cos(a) + 0.03 * k, 0.03 * math.sin(a) - 0.04, 0.24 + 0.012 * math.sin(3 * a + k))
                for a in (2 * math.pi * t / 12 for t in range(13))]
        p.add('cable', hl.sweep(loop, 0.003, sides=5), black if k != 1 else M('DC_Chalk'))
    # two stacked open-front bins, blue under red, small parts showing
    bx = 0.175
    for k, mat in enumerate(('LI_RecycleBlue', 'LI_BinRed')):
        z0 = k * 0.127
        bin_ = open_box(bx - 0.0525, bx + 0.0525, -0.14, 0.14, z0 + (0.0004 if k else 0.0), z0 + 0.127, 0.004)
        p.add('bin', bin_, M(mat))
        p.add('bin parts', rb(bx - 0.045, bx + 0.045, -0.13, 0.10, z0 + 0.004, z0 + 0.05, c=0.004), M('DC_Steel') if k else M('T2_Brass'))
    return p


# ============================================================================================ R4 the whiteboard

WB = (0.90, 0.60)


def whiteboard():
    """A wall piece: origin at the bottom centre of its back, the back at y = 0, the face toward -Y."""
    p = Piece('LI_Whiteboard', floor=False)
    w, h = WB
    frame = M('CF_Black')
    p.add('backing', rb(-w / 2 + 0.002, w / 2 - 0.002, -0.012, 0.0, 0.002, h - 0.002, c=0.002), M('DC_Steel'))
    for x0, x1, z0, z1 in ((-w / 2, w / 2, 0.0, 0.018), (-w / 2, w / 2, h - 0.018, h), (-w / 2, -w / 2 + 0.018, 0.018, h - 0.018),
                           (w / 2 - 0.018, w / 2, 0.018, h - 0.018)):
        p.add('frame', rb(x0, x1, -0.020, -0.004, z0, z1, c=0.002), frame)
    face = fb.zbox(-w / 2 + 0.016, w / 2 - 0.016, -0.0145, -0.0125, 0.016, h - 0.016)
    p.textured('board', face, M('CF_Whiteboard'), origin=(-w / 2 + 0.016, -0.0145, 0.016), u_axis=(1, 0, 0), v_axis=(0, 0, 1),
               w=w - 0.032, h=h - 0.032, edge=(0.01, 0.01))
    p.add('marker tray', rb(-0.20, 0.20, -0.045, -0.0195, 0.0015, 0.012, c=0.002), frame)
    for k, (mx, mz, mat) in enumerate(((-0.33, 0.50, 'LI_RecycleBlue'), (0.06, 0.52, 'LI_BinRed'), (0.30, 0.20, 'LI_WireGreen'))):
        p.add('magnet', hl.moved(fb.turn(hl.revolve([(0.0, 0.011), (0.008, 0.011), (0.011, 0.008)], sides=10), 90, 'X'), mx, -0.0145, mz), M(mat))
    for k, (nx, nz, mat, rot) in enumerate(((0.33, 0.43, 'LI_PostItYellow', 5), (0.25, 0.10, 'LI_PostItPink', -4))):
        note = fb.zbox(-0.0375, 0.0375, -0.0157, -0.0147, -0.0375, 0.0375)
        fb.turn(note, rot, 'Y')
        p.add('post-it', hl.moved(note, nx, 0.0, nz), M(mat))
    inv = fb.zbox(-0.055, 0.055, -0.0158, -0.0150, -0.075, 0.075)
    fb.turn(inv, -3, 'Y')
    p.add('invoice', hl.moved(inv, -0.36, 0.0, 0.17), M('LI_PaperWhite'))
    p.add('bulldog clip', rb(-0.40, -0.32, -0.024, -0.014, 0.235, 0.255, c=0.002), M('DC_SteelDark'))
    return p


# ============================================================================================ R5 the parts delivery

def parts_delivery():
    p = Piece('LI_PartsDelivery', floor=True)
    kraft, tape = M('CF_Kraft'), M('LI_TapeBrown')
    # the shipping box, its flaps open: walls, a floor, four flaps leaning out, bubble wrap in it
    w, d, h = 0.40, 0.30, 0.25
    p.add('box', open_box(-w / 2, w / 2, -d / 2, d / 2, 0.0, h, 0.004), kraft)
    p.add('contents', rb(-w / 2 + 0.006, w / 2 - 0.006, -d / 2 + 0.006, d / 2 - 0.006, 0.0035, 0.185, c=0.004), M('LI_PaperWhite'))
    for k, (axis, sign) in enumerate((('X', 1), ('X', -1), ('Y', 1), ('Y', -1))):
        if axis == 'X':
            flap = fb.zbox(-0.002, 0.002, -d / 2 + 0.003, d / 2 - 0.003, h - 0.002, h + 0.14)
            fb.turn(flap, sign * 55, 'Y', pivot=(0.0, 0.0, h))
            hl.moved(flap, sign * (w / 2 - 0.002), 0.0, 0.0)
        else:
            flap = fb.zbox(-w / 2 + 0.003, w / 2 - 0.003, -0.002, 0.002, h - 0.002, h + 0.19)
            fb.turn(flap, -sign * 60, 'X', pivot=(0.0, 0.0, h))
            hl.moved(flap, 0.0, sign * (d / 2 - 0.002), 0.0)
        p.add('flap', flap, kraft)
    for k in range(3):
        bub = hl.sweep([(-0.15, -0.09 + 0.08 * k, 0.205 + 0.01 * (k % 2)), (0.0, -0.08 + 0.08 * k, 0.225), (0.15, -0.09 + 0.08 * k, 0.21)], 0.028, sides=8)
        p.add('bubble wrap', bub, M('LI_BubbleWrap'))
    p.add('tape', rb(-0.03, 0.03, -d / 2 - 0.0008, -d / 2 + 0.001, 0.12, h - 0.002, c=0.0004), tape)
    p.add('packing slip', rb(0.03, 0.15, -d / 2 - 0.0012, -d / 2, 0.06, 0.19, c=0.0004), M('LI_PaperWhite'))
    for k, (bx, by, rot) in enumerate(((0.262, -0.06, 4), (0.262, 0.08, -3))):
        p.add('parts box', moved(rb(-0.06, 0.06, -0.04, 0.04, 0.0, 0.05, c=0.003), bx, by, 0.0, rot=rot), M('LI_PaperWhite') if k else kraft)
    return p


# ============================================================================================ S1, S2 the story pieces

LETTER = (0.216, 0.279)


def mortgage_letter():
    """Flat on the counter, opened and flattened (a tri-fold's two creases), the page facing up; its text reads for Ace,
    who stands on the +Y side: u to Blender -X, v to -Y (the page's top away from him)."""
    p = Piece('LI_MortgageLetter', floor=True)
    w, h = LETTER
    pts = []
    zs = [0.0, 0.0022, 0.0, 0.0018]                     # the creases at a third and two thirds stand up a little
    for k in range(4):
        pts.append(h / 2 - k * h / 3)
    bm = bmesh.new()
    rows = []
    for k, y in enumerate(pts):
        rows.append([bm.verts.new((x, y, zs[k] + 0.0004)) for x in (w / 2, -w / 2)])
    for a, b in zip(rows, rows[1:]):
        bm.faces.new([a[0], a[1], b[1], b[0]])
    under = [[bm.verts.new((v.co.x, v.co.y, v.co.z - 0.0003)) for v in r] for r in rows]
    for a, b in zip(under, under[1:]):
        bm.faces.new([b[0], b[1], a[1], a[0]])
    for side in (0, 1):                                   # the two long edges
        for a, b, ua, ub in zip(rows, rows[1:], under, under[1:]):
            q = [a[side], b[side], ub[side], ua[side]]
            bm.faces.new(q if side == 0 else list(reversed(q)))
    bm.faces.new([rows[0][0], under[0][0], under[0][1], rows[0][1]])
    bm.faces.new([rows[-1][1], under[-1][1], under[-1][0], rows[-1][0]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p.textured('page', bm, M('LI_Mortgage'), origin=(w / 2, h / 2, 0.0), u_axis=(-1, 0, 0), v_axis=(0, -1, 0), w=w, h=h, edge=(0.02, 0.02), min_dot=0.5)
    return p


def final_notice():
    """A wall piece (the door's inner face): origin at the bottom centre of its back, the face toward -Y. The page taped by
    its top corners (two strips of masking tape), its envelope pinned beside it; both on LI_FinalNotice.png (the page in
    the atlas's left 0.62, the envelope in its right part)."""
    p = Piece('LI_FinalNotice', floor=False)
    w, h = LETTER
    page = fb.zbox(-0.16, -0.16 + w, -0.0006, -0.0002, 0.0, h)
    p.textured('page', page, M('LI_FinalNotice'), origin=(-0.16, -0.0006, 0.0), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=w, h=h,
               rect=(0.0, 0.0, 0.62, 1.0), edge=(0.02, 0.02), min_dot=0.5)
    for tx in (-0.16, -0.16 + w):
        strip = fb.zbox(-0.03, 0.03, -0.0011, -0.0005, -0.009, 0.009)
        fb.turn(strip, 35 if tx < 0 else -35, 'Y')
        p.add('tape', hl.moved(strip, tx, 0.0, h - 0.006), M('LI_Tape'))
    ew, eh = 0.220, 0.110
    env = fb.zbox(0.085, 0.085 + eh, -0.0008, -0.0002, 0.12, 0.12 + ew)    # standing on its end beside the page
    p.textured('envelope', env, M('LI_FinalNotice'), origin=(0.085, -0.0008, 0.12), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=eh, h=ew,
               rect=(0.62, 0.0, 1.0, 1.0), edge=(0.04, 0.02), min_dot=0.5)
    p.add('push pin', hl.moved(fb.turn(hl.revolve([(0.0, 0.0045), (0.004, 0.0045), (0.009, 0.0025)], sides=8), 90, 'X'), 0.14, -0.0005, 0.12 + ew - 0.015), M('CF_PinRed'))
    return p


PIECES = [
    # (builder, group, what, place (world, Unity), origin)
    (pickup_row, 'Repair side', 'R1: seven finished repairs waiting for pickup, bagged and tagged; the cherry-red radio the accent',
     {'position': [-5.65, 1.00, 17.25], 'rotation': [0, 180, 0], 'on': "CK_BackBar's top in front of CK_PartsWall: x -7.20..-4.10, z 17.10-17.40"},
     'bottom centre'),
    (desk_corner, 'Repair side', "R2: Ace's desk corner: parts cabinet, spool rack, multimeter, ticket pad, post-its, tape, steel rule, his green mug",
     {'position': [-3.775, 1.005, 14.08], 'rotation': [0, 180, 0], 'on': "CK_RepairBench's top east of the stage: x -4.40..-3.15, z 13.78-14.38; "
      "two post-its on the glass screen's Ace side (world z 13.681, 1.25 m up), between the posts at x -4.30 and -3.47"},
     'bottom centre (the bench top)'),
    (glass_notes, 'Repair side', "R2's two post-its on the glass screen's Ace side (yellow, the pink one over its corner)",
     {'position': [-4.05, 1.215, 13.6810], 'rotation': [0, 0, 0], 'on': "CK_RepairBench's glass screen, Ace's side (its +z face at "
      "world z 13.6795; the notes' backs 1.5 mm behind it), between the posts at x -4.30 and -3.47, their middle 1.25 m up; facing "
      "Ace (+z), so rotation 0, not 180"}, 'bottom centre of the back'),
    (donor_parts, 'Repair side', 'R3: under-bench donor parts: a grey tote heaped with old parts and cable, two stacked open-front bins',
     {'position': [-3.375, 0.0, 13.88], 'rotation': [0, 180, 0], 'on': "the floor in CK_RepairBench's knee space at its east end (y 0 = the "
      "floor's top: find it by a ray down)"}, 'bottom centre'),
    (whiteboard, 'Repair side', 'R4: "this week" whiteboard 0.90 x 0.60, black frame, marker notes, magnets, two post-its, a clipped invoice',
     {'position': [-5.60, 1.92, 17.99], 'rotation': [0, 180, 0], 'on': 'the back wall over CK_PartsWall, its face to the room'},
     'bottom centre of its back'),
    (parts_delivery, 'Repair side', 'R5: a half-open shipping box with tape, slip and bubble wrap, two small parts boxes',
     {'position': [-6.60, 1.095, 13.95], 'rotation': [0, 180, 0], 'on': "CK_FrontCounter's west end top, Ace's half"}, 'bottom centre'),
    (mortgage_letter, 'Story', "S1: the mortgage statement, opened and flattened, the overdue line circled in red (texture)",
     {'position': [0.30, 1.0955, 14.15], 'rotation': [0, 192, 0], 'on': "under 'Register v4''s corner on Ace's side, flat on the counter "
      "(its far third under the till)"}, 'bottom centre'),
    (final_notice, 'Story', "S2: the FINAL NOTICE taped up by two corners, its stamped envelope pinned beside it (texture)",
     {'position': [6.63, 1.36, 17.94], 'rotation': [0, 180, 0], 'on': "inside the back door at 1.50 m (the page's middle); a child of the "
      "door leaf if it swings (unverified); never rename the door (Night 0 finds it by name)"}, 'bottom centre of its back'),
]
