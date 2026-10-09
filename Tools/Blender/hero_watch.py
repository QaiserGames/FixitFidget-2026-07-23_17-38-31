"""
The hero pocket watch, v3 (9 Oct 2026): PocketWatch.prefab's pieces (a 15 cm cylinder for the case, a cylinder back
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
  Screw                            a 12 mm slotted-cross case screw, dome facing out (-Z), shank going +Z. Used four times.
  Mainspring                       a blued-steel ribbon coiled five turns round a brass arbor, 2.5 mm tall; lies in XY,
                                   centred on its origin.
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

HD_MATERIALS = {
    'HD_WatchGold':     ('#C19A52', 0.55, 0.0),   # the case, bezel, crown, bow, back plate
    'HD_WatchGoldDark': ('#8E6E2E', 0.50, 0.0),   # the engraved ring, the seams
    'HD_WatchDial':     ('#F1ECE0', 0.60, 0.0),   # white enamel
    'HD_WatchInk':      ('#2B2520', 0.30, 0.0),   # numerals, the track, the sub-dial ring
    'HD_WatchBlue':     ('#2A3F6B', 0.55, 0.0),   # blued steel: the hands, the mainspring
    'HD_WatchNickel':   ('#B5B1A8', 0.45, 0.0),   # the plates of the movement
    'HD_WatchBrass':    ('#B08A3E', 0.50, 0.0),   # wheels, chatons, the arbor
    'HD_WatchRuby':     ('#8B1A2B', 0.70, 0.0),   # jewels
    'HD_WatchSteel':    ('#93908A', 0.45, 0.0),   # screws, the balance
    'HD_ScrewSlot':     ('#2A2D31', 0.30, 0.0),
}

PREFAB_OFFSETS = {        # Unity's frame
    'BackPlate':  (0.0, -0.016, 0.0),
    'Crown':      (0.0, 0.0, 0.085),
    'Screw':      (0.032, -0.020, 0.032),
    'Mainspring': (0.030, -0.007, 0.020),
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
    for k in range(1, 13):
        a = math.radians(90 - 30 * k)
        txt = hl.text_mesh(str(k), .0135, .00025)
        if txt is None:
            continue
        # centre the glyphs on their box, then place them on a 44 mm ring
        xs = [v.co.x for v in txt.verts]
        ys = [v.co.y for v in txt.verts]
        moved(txt, -(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, DIAL_Z)
        moved(txt, .0440 * math.cos(a), .0440 * math.sin(a), 0)
        p.add('numeral %d' % k, txt, ink)
    # the sub-seconds at 6: a sunk disc with its own ring and twelve ticks, and a slim needle
    sx, sy = 0.0, -.0330
    p.add('seconds dial', moved(revolve([(DIAL_Z - .0016, .0125), (DIAL_Z - .0003, .0125)], sides=32), sx, sy, 0), dial_m)
    p.add('seconds ring', moved(ring_wall(DIAL_Z - .0003, DIAL_Z - .0001, .0112, .0118, sides=32), sx, sy, 0), ink)
    for k in range(12):
        tick = fb.zbox(-.0003, .0003, .0095, .0112, DIAL_Z - .0003, DIAL_Z - .0001)
        fb.turn(tick, 30.0 * k, 'Z')
        p.add('seconds tick', moved(tick, sx, sy, 0), ink)
    needle = prism([(-.0004, -.0030), (.0004, -.0030), (.0002, .0095), (-.0002, .0095)], DIAL_Z - .0001, DIAL_Z + .0003)
    fb.turn(needle, -140.0, 'Z')
    p.add('seconds hand', moved(needle, sx, sy, 0), blue)
    p.add('seconds boss', disc(sx, sy, DIAL_Z - .0001, DIAL_Z + .0004, .0010, sides=8), steel)
    # the hands: blued steel spades at ten past ten, on a gold cannon
    hour = hand(.0330, .0028, .0200, .0080, .0100, DIAL_Z + .0004, DIAL_Z + .0010)
    fb.turn(hour, 60.0, 'Z')                 # ten o'clock
    p.add('hour hand', hour, blue)
    minute = hand(.0505, .0020, .0150, .0050, .0080, DIAL_Z + .0010, DIAL_Z + .0015)
    fb.turn(minute, -60.0, 'Z')              # two (ten past)
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
    return p


def back_plate():
    """Centred on its origin (Unity y -16): a flat rim level with the case's back rim (-14), a shallow dome to -20.5."""
    p = Faceted('BackPlate')
    gold, gold_dark = M('HD_WatchGold'), M('HD_WatchGoldDark')
    prof = list(reversed(dome(-.0005, .0550, .0040, n=4, up=False))) + [(-.0005, .0550), (-.0005, .0648), (.0020, .0648)]
    p.add('plate', revolve(prof, sides=64, bottom_tip=(0, 0, -.0046)), gold)
    # one fine engraved ring on the dome
    p.add('engraved ring', ring_wall(-.0034, -.0030, .0400, .0408, sides=64), gold_dark)
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
    p = Faceted('Screw')
    steel, slot_m = M('HD_WatchSteel'), M('HD_ScrewSlot')
    head = revolve([(-.0013, .0046), (-.0008, .0056), (.0000, .0060), (.0013, .0060)], sides=16, bottom_tip=(0, 0, -.0015))
    p.add('head', head, steel)
    for rot in (0.0, 90.0):
        slot = fb.zbox(-.0036, .0036, -.0005, .0005, -.0017, -.0010)
        fb.turn(slot, rot, 'Z')
        p.add('slot', slot, slot_m)
    prof = [(.0013, .0020)]
    for i in range(3):
        z = .0020 + i * .0016
        prof += [(z, .0017), (z + .0008, .0022)]
    prof += [(.0070, .0017), (.0076, .0010)]
    p.add('shank', revolve(prof, sides=10, tip=(0, 0, .0080)), steel)
    return p


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


PIECES = [
    (case, ['Cylinder']),
    (back_plate, ['BackPlate']),
    (crown, ['Crown']),
    (screw, ['Screw0', 'Screw1', 'Screw2', 'Screw3']),
    (mainspring, ['Part_Extra_Mainspring', 'Fresh']),
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
    return root, children, made
