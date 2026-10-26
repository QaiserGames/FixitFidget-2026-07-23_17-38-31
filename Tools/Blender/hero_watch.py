"""
v3.7 (24 Oct 2026, art audit a27): the sub-seconds' ring and twelve ticks stand on the dial's face (they had been built under
it, so the small seconds hand turned over bare enamel), and the 6 is left off, as railroad dials do, because the seconds
bit's ring now shows where it stood. Nothing else moved: the same pieces, names, places and sizes.

The hero pocket watch, v3.6 (14 Oct 2026): AUTHORED AT 3x, EXPORTED REAL (TD one-scale §6.1: build() scales every mesh and
child place by 1 / AUTHORED_SCALE through hero_lib.scale_build, so the FBX is a 50 mm watch); the four case screws real:
3.8 mm blued pan heads (11.4 at this file's 3x; feel-targets call 3), their heads on the back plate's dome. On v3 (9 Oct 2026): PocketWatch.prefab's pieces (a 15 cm cylinder for the case, a cylinder back
plate, a cylinder crown, cylinder screws, a cube for the mainspring) as real meshes, built from measured pocket watches
(claude/hero-reference-sheet.md: 48-50 mm open-face cases, a pendant-crown-bow a third of the diameter tall, an enamel
dial with a railroad track and sub-seconds at 6, a 3/4-plate movement) at the prefab's 3x size, in the house style
(hero_lib.py), to the prefab's sizes and places so Devices 2 swaps the meshes and nothing the bench knows moves.

The prefab's frame (Unity): the face is +Y, the crown sticks out along +Z. Blender here: Z through (+Z the face),
Y along (Unity +Z is Blender -Y: the crown side is -Y), X across (Unity +X is Blender -X: hero_lib.to_blender).

What the prefab fixes: the back plate's origin at y -16 (its rim meets the case at -14), the four screws at y -20 on a
45 mm radius (their heads rest on the plate's dome), the grime spots at y -13 (on the movement, under the plate), the
mainspring's seat at (30, -7, 20) (in the barrel, on the pillar plate at -5.7). So, in Unity's y:

  +10.0  bezel top          +6.5 dial face (3.5 mm under the bezel, as if under a crystal)
   -5.7  pillar plate       the floor of the movement; the barrel bite where the mainspring lies (its seat is -7)
  -11.5  the 3/4 plate      (nearly 6 mm thick) with the balance in its own bite, the crown wheel, jewels, screws
  -14.0  back rim           the case band's back edge; the back plate's rim sits level with it
  -20.5  back plate's dome  with the four screws on it

  Case (the prefab's "Cylinder")   the band (18 mm), the bezel, the dial with its track, numerals, sub-seconds and blued
                                   spade hands, the bow at 12 (part of the case: it is rigid; no hinge: the back unscrews),
                                   and the movement in the open back.
  BackPlate                        a 130 mm back: a flat rim, a shallow dome, one fine engraved ring.
  Crown                            built along +Z (its outward axis; the prefab turns the object so that is Unity +Z):
                                   the pendant tube with a collar and a fluted crown.
  Screw                            v3.6: hero_phone.screw_mesh at 3x: a pan-head Phillips screw, head Ø 11.4 (3.8 real)
                                   x 0.35, blued; the shank 2 x the head (7.6 real). Top facing out (-Z), z = 0 the
                                   head's middle. Used four times; at Unity y -0.020635 (3x) the head's middle sits on the
                                   dome at r 45 (was -0.020 under the 12 mm heads).
  Mainspring                       a blued-steel ribbon coiled five turns round a brass arbor, 2.5 mm tall; lies in XY,
                                   centred on its origin. The FRESH part.
  MainspringBroken                 the BROKEN part (v3.3): the same spring snapped three-quarters of a turn from its
                                   hook; the inner coil sits tired and dull, the outer piece with the hook has sprung
                                   up out of the barrel and twisted, and the two broken ends are bright fresh steel.
                                   What the player sees in the barrel before the tweezers, and in the tray after.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from hero_lib import Faceted, disc, dome, gear, moved, revolve, ring_wall, spoked_wheel, sweep, to_blender, tube  # noqa: E402
from mathutils import Vector  # noqa: E402

AUTHORED_SCALE = 3.0      # this file's numbers are at 3x; the FBX is real (build() scales by 1 / AUTHORED_SCALE)
SCREW_HEAD = .0038        # real (feel-targets call 3)

HD_MATERIALS = {
    'HD_WatchGold':     ('#C19A52', 0.55, 0.0),   # the case, bezel, crown, bow, back plate
    'HD_WatchGoldDark': ('#8E6E2E', 0.50, 0.0),   # the engraved ring, the seams
    'HD_WatchDial':     ('#F1ECE0', 0.60, 0.0),   # white enamel
    'HD_WatchInk':      ('#2B2520', 0.30, 0.0),   # numerals, the track, the sub-dial ring
    'HD_WatchBlue':     ('#2A3F6B', 0.55, 0.0),   # blued steel: the hands, the mainspring
    'HD_WatchBlueDull': ('#343A48', 0.30, 0.0),   # the tired, broken mainspring: the blue gone grey
    'HD_WatchNickel':   ('#B5B1A8', 0.45, 0.0),   # the plates of the movement
    'HD_WatchBrass':    ('#B08A3E', 0.50, 0.0),   # wheels, chatons, the arbor
    'HD_WatchRuby':     ('#8B1A2B', 0.70, 0.0),   # jewels
    'HD_WatchSteel':    ('#93908A', 0.45, 0.0),   # screws, the balance
    'HD_ScrewSlot':     ('#2A2D31', 0.30, 0.0),   # the movement's plate screws' slots
    'HD_ScrewBlued':    ('#2A3F6B', 0.55, 0.0),   # v3.6: the four case screws, blued steel (Art A8: it reads on the gold)
    'HD_ScrewRecess':   ('#121110', 0.20, 0.0),   # v3.6: their cross recess
}

PREFAB_OFFSETS = {        # Unity's frame
    'BackPlate':  (0.0, -0.016, 0.0),
    'Crown':      (0.0, 0.0, 0.085),
    'Screw':      (0.032, -0.020635, 0.032),       # v3.6: the 11.4 x 3.99 head's middle on the dome at r 45 (3x); was -0.020
    'MainspringBroken': (0.030, -0.007, 0.020),   # the broken part, in the barrel
    'Mainspring': (0.110, 0.0, 0.0),              # the fresh part: beside the watch in the FBX assembly (the prefab's Fresh sits in the seat, off)
}

CASE_R = .0750
BORE_R = .0650
BEZEL_TOP = .0100
DIAL_Z = .0065
FLOOR = -.00565           # the pillar plate: the floor the mainspring (its far face at -5.75) lies on
PLATE_BOTTOM = -.0115     # the 3/4 plate's visible face
RIM_BACK = -.0140
BARREL = (-.030, -.020, .0320)     # x, y (Blender: Unity x and z negated, hero_lib.to_blender), radius of the barrel bite round the mainspring's seat
BALANCE = (.034, .030, .0225)      # the balance's bite (reaches past the plate's edge, so it is a bite, not a hole)


def M(name):
    return hl.material(HD_MATERIALS, name)


def plate_outline(R, bites, n=160):
    """The outline of a disc of radius R with circular bites taken out of its edge, as a CCW polygon."""
    big = [(R * math.cos(2 * math.pi * i / n), R * math.sin(2 * math.pi * i / n)) for i in range(n)]

    def which(p):
        for b in bites:
            if math.hypot(p[0] - b[0], p[1] - b[1]) < b[2]:
                return b
        return None

    pts = []
    i = 0
    guard = 0
    while i < n and guard < 4 * n:
        guard += 1
        p = big[i]
        b = which(p)
        if b is None:
            pts.append(p)
            i += 1
            continue
        j = i
        while which(big[j % n]) is b:
            j += 1
        # the bite circle meets the big circle at two points: the entry (near big[i-1]) and the exit (near big[j])
        cx, cy, r = b
        d = math.hypot(cx, cy)
        a0 = math.atan2(cy, cx)
        cosg = (R * R + d * d - r * r) / (2 * R * d)
        g = math.acos(max(-1.0, min(1.0, cosg)))
        cands = [(R * math.cos(a0 - g), R * math.sin(a0 - g)), (R * math.cos(a0 + g), R * math.sin(a0 + g))]
        prev = big[(i - 1) % n]
        entry = min(cands, key=lambda q: math.hypot(q[0] - prev[0], q[1] - prev[1]))
        exit_ = cands[1] if entry is cands[0] else cands[0]
        ae = math.atan2(entry[1] - cy, entry[0] - cx)
        ax = math.atan2(exit_[1] - cy, exit_[0] - cx)
        # go round the bite the way whose middle lies inside the big disc
        best = None
        for direction in (1, -1):
            span = (ax - ae) * direction
            while span < 0:
                span += 2 * math.pi
            mid = ae + direction * span / 2
            m = (cx + r * math.cos(mid), cy + r * math.sin(mid))
            if math.hypot(*m) < R and (best is None or span < best[1]):
                best = (direction, span)
        direction, span = best
        steps = max(6, int(span / (2 * math.pi) * 48))
        pts.append(entry)
        for k in range(1, steps):
            a = ae + direction * span * k / steps
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        pts.append(exit_)
        i = j
    return pts


def prism(outline, z0, z1):
    """A straight prism from a polygon outline (CCW seen from +Z), its caps triangulated."""
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in outline]
    hi = [bm.verts.new((x, y, z1)) for x, y in outline]
    n = len(outline)
    top = bm.faces.new(hi)
    bot = bm.faces.new(list(reversed(lo)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bmesh.ops.triangulate(bm, faces=[top, bot])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def hand(length, width, spade_at, spade_w, spade_l, z0, z1, tail=.006):
    """A watch hand lying along +Y from the centre: a tapered stem, a diamond spade, a point; a short tail behind."""
    half = width / 2
    pts = [(-half, -tail), (half, -tail), (half * .8, spade_at - spade_l / 2), (spade_w / 2, spade_at), (half * .5, spade_at + spade_l / 2),
           (0.0, length), (-half * .5, spade_at + spade_l / 2), (-spade_w / 2, spade_at), (-half * .8, spade_at - spade_l / 2)]
    return prism(pts, z0, z1)


def case():
    p = Faceted('HD_Watch')
    gold, gold_dark, dial_m, ink, blue = M('HD_WatchGold'), M('HD_WatchGoldDark'), M('HD_WatchDial'), M('HD_WatchInk'), M('HD_WatchBlue')
    nickel, brass, ruby, steel = M('HD_WatchNickel'), M('HD_WatchBrass'), M('HD_WatchRuby'), M('HD_WatchSteel')
    S = 64
    # the band and the bezel: one solid of revolution round a bore, from the back rim up to the bezel's inner lip
    outer = [(RIM_BACK, .0735), (RIM_BACK + .0008, CASE_R), (.0036, CASE_R), (.0040, .0742), (.0044, CASE_R), (.0075, .0745),
             (.0092, .0725), (BEZEL_TOP, .0690), (BEZEL_TOP, .0665), (.0085, .0652)]
    p.add('band and bezel', hl.ring_wall_profile(outer, BORE_R, sides=S), gold)
    # the inside: the pillar plate (the floor of the movement) and the dial's seat, one nickel core from the floor up
    p.add('pillar plate', revolve([(FLOOR, BORE_R + .0005), (DIAL_Z - .0010, BORE_R + .0005)], sides=S), nickel)
    # the dial: white enamel, its face at DIAL_Z, inside the bezel's lip
    p.add('dial', revolve([(DIAL_Z - .0012, .0655), (DIAL_Z, .0655)], sides=S), dial_m)
    # the railroad minute track: two fine rings and sixty ticks, every fifth bold
    for r0, r1 in ((.0572, .0578), (.0532, .0538)):
        p.add('track ring', ring_wall(DIAL_Z, DIAL_Z + .0002, r0, r1, sides=S), ink)
    for k in range(60):
        bold = k % 5 == 0
        w = .0012 if bold else .0006
        tick = fb.zbox(-w / 2, w / 2, .0535 if not bold else .0528, .0575 if not bold else .0582, DIAL_Z, DIAL_Z + .0002)
        fb.turn(tick, 6.0 * k, 'Z')
        p.add('tick', tick, ink)
    # the numerals: bold Arabic figures, upright, raised a quarter millimetre (text_mesh uses Blender's built-in font)
    # THE DIAL'S ORIENTATION (v3.5, found in the review): the pendant is at Blender -Y, which is the far end from the
    # player, so twelve is at -Y, the player's up is -Y, and the player's right (front up) is -X: clockwise for the
    # player runs from -Y toward -X, i.e. DECREASING angle. v3 to v3.4 had twelve at +Y (six at the pendant) and the
    # glyphs upright for someone on the far side of the bench.
    for k in range(1, 13):
        if k == 6:
            continue     # v3.7: the seconds bit takes the six's place, as on a railroad dial (its ring would cross the 6)
        a = math.radians(-90 - 30 * k)
        txt = hl.text_mesh(str(k), .0135, .00025)
        if txt is None:
            continue
        # centre the glyphs on their box, turn them upright for the player, then place them on a 44 mm ring
        xs = [v.co.x for v in txt.verts]
        ys = [v.co.y for v in txt.verts]
        moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, 0)
        fb.turn(txt, 180.0, 'Z')
        moved(txt, .0440 * math.cos(a), .0440 * math.sin(a), DIAL_Z)
        p.add('numeral %d' % k, txt, ink)
    # the maker's name under twelve and the grade above the seconds (v3.5): dial lettering, upright for the player
    for (text, size, y) in ((DIAL_MAKER, .0046, -.0225), ('RAILROAD GRADE', .0027, .0135)):
        if not text:
            continue
        txt = hl.text_mesh(text, size, .00020)
        if txt is None:
            continue
        xs = [v.co.x for v in txt.verts]
        ys = [v.co.y for v in txt.verts]
        moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, 0)
        fb.turn(txt, 180.0, 'Z')
        p.add('dial lettering', moved(txt, 0, y, DIAL_Z), ink)
    # the sub-seconds at 6 (+Y, toward the player): its own ring and twelve ticks printed on the enamel like the minute
    # track, and a slim needle over them. (24 Oct 2026, art audit a27: "the small seconds hand turns over bare dial ... it
    # reads as a stray scratch". The ring and ticks were there, but built on a sunk disc 0.1-0.3 mm UNDER the dial's face,
    # inside the dial's own solid, so they never showed. Now they stand on the face (DIAL_Z to +0.2, the track's height),
    # the ring a little bolder and the four quarter ticks longer, as a real sub-seconds is printed; the buried disc is gone.)
    sx, sy = 0.0, .0330
    p.add('seconds ring', moved(ring_wall(DIAL_Z, DIAL_Z + .0002, .0111, .0119, sides=32), sx, sy, 0), ink)
    for k in range(12):
        quarter = k % 3 == 0
        tick = fb.zbox(-.0004 if quarter else -.0003, .0004 if quarter else .0003, .0088 if quarter else .0097, .0112,
                       DIAL_Z, DIAL_Z + .0002)
        fb.turn(tick, 30.0 * k, 'Z')
        p.add('seconds tick', moved(tick, sx, sy, 0), ink)
    needle = prism([(-.0004, -.0030), (.0004, -.0030), (.0002, .0095), (-.0002, .0095)], DIAL_Z + .00025, DIAL_Z + .00065)
    fb.turn(needle, -140.0, 'Z')
    p.add('seconds hand', moved(needle, sx, sy, 0), blue)
    p.add('seconds boss', disc(sx, sy, DIAL_Z - .0001, DIAL_Z + .0007, .0010, sides=8), steel)
    # the hands: blued steel spades at ten past ten, on a gold cannon
    hour = hand(.0330, .0028, .0200, .0080, .0100, DIAL_Z + .0004, DIAL_Z + .0010)
    fb.turn(hour, 240.0, 'Z')                # ten o'clock (twelve is at -Y)
    p.add('hour hand', hour, blue)
    minute = hand(.0505, .0020, .0150, .0050, .0080, DIAL_Z + .0010, DIAL_Z + .0015)
    fb.turn(minute, 120.0, 'Z')              # two (ten past)
    p.add('minute hand', minute, blue)
    p.add('cannon', revolve([(DIAL_Z, .0032), (DIAL_Z + .0016, .0032), (DIAL_Z + .0021, .0022)], sides=16, tip=(0, 0, DIAL_Z + .0024)), gold)
    # (no hinge at six: this watch's back comes off with four screws, and a hinge would say otherwise)
    # the bow at twelve: a hexagonal wire ring standing round the crown, through the pendant
    R, cy = .0210, -.0800 - .0210
    ring = [(R * math.cos(a), cy + R * math.sin(a), -.0010) for a in (2 * math.pi * i / 36 for i in range(36))]
    p.add('bow', sweep(ring, .0030, sides=6, closed=True), gold)
    # the pendant's ears, where the bow pivots: a short gold saddle on the band
    p.add('pendant saddle', moved(fb.zbox(-.0110, .0110, -.0800, -.0700, -.0060, .0040), 0, 0, 0), gold, bevel=.0015)
    # ---- the movement, seen when the back plate is off: a 3/4 plate with two bites (the barrel, the balance)
    barrel_bite = (BARREL[0], BARREL[1], BARREL[2])
    balance_bite = (BALANCE[0], BALANCE[1], BALANCE[2])
    outline = plate_outline(.0620, [barrel_bite, balance_bite])
    p.add('three-quarter plate', prism(outline, PLATE_BOTTOM, FLOOR - .0003), nickel)
    # the barrel wall (the mainspring lies inside it, on the floor)
    bx, by, br = barrel_bite
    p.add('barrel wall', moved(ring_wall(FLOOR - .0032, FLOOR - .0002, br - .0040, br - .0014, sides=40), bx, by, 0), brass)
    # the balance: a two-armed steel wheel in its bite, under a cock with a jewel at its end
    cx, cy_, cr = balance_bite
    p.add('balance', spoked_wheel(cx, cy_, FLOOR - .0022, FLOOR - .0006, cr - .0025, .0024, spokes=2, spoke_w=.0022, hub=.0030, sides=40), steel)
    p.add('hairspring', moved(ring_wall(FLOOR - .0030, FLOOR - .0026, .0060, .0066, sides=24), cx, cy_, 0), blue)
    cock = tube([hl.ring_rrect(0, 0, PLATE_BOTTOM - .0018, .0110, .0320, .0030, per_corner=2), hl.ring_rrect(0, 0, PLATE_BOTTOM, .0110, .0320, .0030, per_corner=2)])
    ang = math.degrees(math.atan2(cy_, cx))
    moved(cock, 0, -.0160, 0)                 # the cock reaches from the plate's body out over the balance's centre
    fb.turn(cock, ang - 90.0, 'Z')
    p.add('balance cock', moved(cock, cx, cy_, 0), nickel)
    p.add('balance chaton', disc(cx, cy_, PLATE_BOTTOM - .0024, PLATE_BOTTOM - .0018, .0036, sides=12), brass)
    p.add('balance jewel', disc(cx, cy_, PLATE_BOTTOM - .0027, PLATE_BOTTOM - .0024, .0019, sides=10), ruby)
    # the crown wheel beside the barrel, a few jewels in chatons and the plate's screws
    p.add('crown wheel', gear(.0040, -.0460, PLATE_BOTTOM - .0016, PLATE_BOTTOM, .0130, 30, depth=.12), brass)
    p.add('crown wheel screw', disc(.0040, -.0460, PLATE_BOTTOM - .0022, PLATE_BOTTOM - .0016, .0025, sides=10), steel)
    for (jx, jy) in ((-.0050, .0280), (.0300, -.0160), (.0450, .0020)):
        p.add('chaton', disc(jx, jy, PLATE_BOTTOM - .0005, PLATE_BOTTOM, .0034, sides=12), brass)
        p.add('jewel', disc(jx, jy, PLATE_BOTTOM - .0008, PLATE_BOTTOM - .0005, .0018, sides=10), ruby)
    for (sx_, sy_) in ((-.0240, .0300), (.0120, .0480), (.0540, -.0220), (-.0520, .0150)):
        p.add('plate screw', disc(sx_, sy_, PLATE_BOTTOM - .0005, PLATE_BOTTOM, .0022, sides=10), steel)
        slot = fb.zbox(sx_ - .0018, sx_ + .0018, sy_ - .0003, sy_ + .0003, PLATE_BOTTOM - .0007, PLATE_BOTTOM - .0004)
        p.add('plate screw slot', slot, M('HD_ScrewSlot'))
    # the plate's engraving (v3.4): a railroad movement is lettered. Upright for the player (whose up is -Y here), in
    # the clear ground at the upper right between the crown wheel and the rim; the maker's name goes on a line above
    # the two once he has chosen it (PLATE_MAKER).
    lines = [t for t in (PLATE_MAKER, '17 JEWELS', 'ADJUSTED') if t]
    y0 = -.0380 - .0048 * (len(lines) - 2)
    for k, line in enumerate(lines):
        p.add('plate engraving', engraving(line, .0032 if len(line) > 9 else .0034, (.0320, y0 + .0048 * k), 0.0), M('HD_WatchInk'))
    return p


PLATE_MAKER = 'BRIGHTWATER'   # the maker (his pick, 9 Oct: a town that doesn't exist, in the railroad watches' voice); one word to change
DIAL_MAKER = PLATE_MAKER


def engraving(text, size, at, degrees, depth=.00025):
    """Lettering on the movement side, read by the player from the back (-Z, their up being -Y): text_mesh's glyphs
    turned over about X so they read from that side, laid on the 3/4 plate's face, turned in the plane by `degrees`."""
    txt = hl.text_mesh(text, size, depth)
    if txt is None:
        return bmesh.new()
    xs = [v.co.x for v in txt.verts]
    ys = [v.co.y for v in txt.verts]
    moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, 0)
    fb.turn(txt, 180.0, 'X')                 # readable from -Z with up -Y; now from z 0 down to -depth
    fb.turn(txt, degrees, 'Z')
    return moved(txt, at[0], at[1], PLATE_BOTTOM)


def back_plate():
    """Centred on its origin (Unity y -16): a flat rim level with the case's back rim (-14), a shallow dome to -20.5."""
    p = Faceted('BackPlate')
    gold, gold_dark = M('HD_WatchGold'), M('HD_WatchGoldDark')
    prof = list(reversed(dome(-.0005, .0550, .0040, n=4, up=False))) + [(-.0005, .0550), (-.0005, .0648), (.0020, .0648)]
    p.add('plate', revolve(prof, sides=64, bottom_tip=(0, 0, -.0046)), gold)
    # one fine engraved ring on the dome
    p.add('engraved ring', ring_wall(-.0034, -.0030, .0400, .0408, sides=64), gold_dark)
    # inside (the flat face at +2.0, toward the movement): the dates watchmakers scratch into a case back at every
    # service (v3.4). This watch has been opened before; the player is the next hand on it. Scratchy, at odd angles.
    for (text, r, ang, tilt, size) in (('3-61', .046, 118.0, -14.0, .0040), ('11-74', .040, 196.0, 9.0, .0036),
                                       ('6-88', .050, 262.0, -5.0, .0042), ('2-02', .034, 331.0, 17.0, .0034)):
        txt = hl.text_mesh(text, size, .00006)
        if txt is None:
            continue
        xs = [v.co.x for v in txt.verts]
        ys = [v.co.y for v in txt.verts]
        moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, 0)
        fb.turn(txt, ang + tilt, 'Z')
        a = math.radians(ang)
        p.add('service date', moved(txt, r * math.cos(a), r * math.sin(a), .0020), gold_dark)
    return p


def crown():
    """Along +Z (its outward axis): the pendant tube into the case, a collar, the fluted crown."""
    p = Faceted('Crown')
    gold = M('HD_WatchGold')
    p.add('pendant', revolve([(-.0120, .0075), (-.0010, .0075), (-.0010, .0092), (.0020, .0092), (.0020, .0080)], sides=16), gold)
    head = revolve([(.0020, .0100), (.0030, .0120), (.0110, .0120), (.0125, .0105)], sides=48, lobes=(24, .045), tip=(0, 0, .0138))
    p.add('crown', head, gold)
    return p


def screw():
    import hero_phone
    return hero_phone.screw_mesh('Screw', M('HD_ScrewBlued'), head=SCREW_HEAD * AUTHORED_SCALE, shank_len=2 * SCREW_HEAD * AUTHORED_SCALE)


def mainspring():
    """A blued ribbon, 2.5 mm tall and 0.9 thick, five turns from 7 mm out to 26 mm round a brass arbor; centred on its
    origin, lying in XY."""
    p = Faceted('Mainspring')
    blue, brass = M('HD_WatchBlue'), M('HD_WatchBrass')
    turns, steps = 5.0, 160
    loops = []
    for i in range(steps + 1):
        t = i / steps
        a = 2 * math.pi * turns * t
        r = .0070 + (.0260 - .0070) * t
        c = Vector((r * math.cos(a), r * math.sin(a), 0))
        radial = Vector((math.cos(a), math.sin(a), 0))
        th, h = .00045, .00125
        loops.append([tuple(c - radial * th + Vector((0, 0, -h))), tuple(c + radial * th + Vector((0, 0, -h))),
                      tuple(c + radial * th + Vector((0, 0, h))), tuple(c - radial * th + Vector((0, 0, h)))])
    p.add('spring', tube(loops), blue)
    end = loops[-1]
    cx = sum(v[0] for v in end) / 4
    cy = sum(v[1] for v in end) / 4
    p.add('outer hook', fb.zbox(cx - .0006, cx + .0006, cy - .0045, cy + .0008, -.00125, .00125), blue)
    p.add('arbor', revolve([(-.00125, .0036), (.00125, .0036)], sides=12), brass)
    p.add('arbor square', fb.zbox(-.0012, .0012, -.0012, .0012, .00125, .0018), brass)
    return p


def mainspring_broken():
    """The broken spring: the coil to 4.2 turns (tired, dull), a ragged bright break, and the outer piece, hook and all,
    sprung out of plane (toward the back, -Z, where the player looks) and twisted. Centred like the fresh one."""
    p = Faceted('MainspringBroken')
    dull, bright, brass = M('HD_WatchBlueDull'), M('HD_WatchSteel'), M('HD_WatchBrass')

    def section(c, radial, up, th, h):
        return [tuple(c - radial * th - up * h), tuple(c + radial * th - up * h),
                tuple(c + radial * th + up * h), tuple(c - radial * th + up * h)]

    turns, steps = 5.0, 160
    t_break = 4.2 / turns                       # the ribbon snapped here
    t_gap = 4.42 / turns                        # the outer piece starts here, having sprung away from the break
    inner = []
    outer = []
    for i in range(steps + 1):
        t = i / steps
        a = 2 * math.pi * turns * t
        r = .0070 + (.0260 - .0070) * t
        c = Vector((r * math.cos(a), r * math.sin(a), 0))
        radial = Vector((math.cos(a), math.sin(a), 0))
        up = Vector((0, 0, 1))
        if t <= t_break:
            # the inner coil, tired: the last tenth of a turn before the break thins to a ragged point
            frac = max(0.0, (t - (t_break - .02)) / .02)
            inner.append(section(c, radial, up, .00045, .00125 * (1 - .6 * frac)))
        elif t >= t_gap:
            # the outer piece: lifted out toward the back (-Z) and twisted as it goes, the hook end 3 mm out of plane
            k = (t - t_gap) / (1 - t_gap)
            lift = -.0030 * k * k
            twist = math.radians(55) * k
            up_t = Vector((0, 0, math.cos(twist))) + radial * math.sin(twist)
            c2 = c + Vector((0, 0, lift)) + radial * (.0006 * k)
            outer.append(section(c2, radial, up_t, .00045, .00125))
    def break_face(loop, outward):
        # the snapped end: the ribbon's section as one bright face, its normal along the ribbon, outward
        bm = bmesh.new()
        f = bm.faces.new([bm.verts.new(v) for v in loop])
        bm.faces.ensure_lookup_table()
        if f.normal.dot(outward) < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
        return bm

    a_in = 2 * math.pi * turns * t_break
    a_out = 2 * math.pi * turns * t_gap
    p.add('spring, the inner coil', tube(inner, cap_top=False), dull)
    p.add('the break', break_face(inner[-1], Vector((-math.sin(a_in), math.cos(a_in), 0))), bright)
    p.add('spring, the sprung piece', tube(outer, cap_bottom=False), dull)
    p.add('the other break', break_face(outer[0], Vector((math.sin(a_out), -math.cos(a_out), 0))), bright)
    end = outer[-1]
    cx = sum(v[0] for v in end) / 4
    cy = sum(v[1] for v in end) / 4
    cz = sum(v[2] for v in end) / 4
    p.add('outer hook', fb.zbox(cx - .0006, cx + .0006, cy - .0045, cy + .0008, cz - .00125, cz + .00125), dull)
    p.add('arbor', revolve([(-.00125, .0036), (.00125, .0036)], sides=12), brass)
    p.add('arbor square', fb.zbox(-.0012, .0012, -.0012, .0012, .00125, .0018), brass)
    return p


PIECES = [
    (case, ['Cylinder']),
    (back_plate, ['BackPlate']),
    (crown, ['Crown']),
    (screw, ['Screw0', 'Screw1', 'Screw2', 'Screw3']),
    (mainspring_broken, ['Part_Extra_Mainspring']),
    (mainspring, ['Fresh']),
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
            if ob.name == 'Crown':
                ob.rotation_euler = (math.radians(90.0), 0.0, 0.0)      # its +Z (outward) to Blender -Y = Unity +Z, as the prefab turns it
            children.append(ob)
    hl.scale_build(made, children, 1.0 / AUTHORED_SCALE)
    return root, children, made


# The prefab's places v3.6 moves (real metres, Unity): Devices 2 bakes today's x 1/3, then sets these. Only the screws move
# (the shorter 3.8 mm heads); the grime move HeroDeviceSteps already makes (Grime_Extra_Watch_1) is x 1/3 by the bake.
MOVES = [
    ('Screw0', (0.032, -0.020, 0.032), (0.010667, -0.006878, 0.010667), "the 3.8 x 1.33 head's middle on the back plate's dome"),
    ('Screw1', (-0.032, -0.020, 0.032), (-0.010667, -0.006878, 0.010667), "the same"),
    ('Screw2', (0.032, -0.020, -0.032), (0.010667, -0.006878, -0.010667), "the same"),
    ('Screw3', (-0.032, -0.020, -0.032), (-0.010667, -0.006878, -0.010667), "the same"),
]
