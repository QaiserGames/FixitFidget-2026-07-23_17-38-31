"""
The drinks machine's six buttons, 26 Oct 2026 (Drink buttons).

WHY. His 26 Oct note on the machine's paddles: "its just words on a square in different colors, it doesn't give aces
aesthetics". Three drawn redesigns were "ai art slop" (flat paint-overs with thin clip-art icons). So the buttons are built
here as real pieces, chunky and toy-like, the way Overcooked's and PlateUp!'s machines read: a bold colour per button, a big
picture of the drink, no fussy detail. His girlfriend is dyslexic and plays on a PlayStation pad, so each drink is told apart
by its picture first, then its colour. There are no words on the buttons (see the report: a 26 px word doesn't fit a 90 mm
button at the close-up's distance); the prompt line says "Pour Latte".

WHAT. One piece per drink, DB_Cap_<Name>, built in the paddle's own frame (the origin is the old ceramic paddle's centre,
the front faces -Y, Z up), so Unity parents it under "<Name> ceramic paddle" with no offset and BeveragePaddleMotion presses
it as it pressed the old tile:
  * the cap: a 92 x 98 mm block, 30 mm proud of the dark backing (the old tile was 26), its front edges rounded 9 mm, in the
    drink's own BF_ colour (the same material as the tray's colour chip under its nozzle);
  * a cream label, 72 mm, with a 1.5 mm ink rim, on the cap's face;
  * the drink's picture in relief on the label: the SAME picture the tickets and the HUD use (Tools/UI/draw_button_icons.py,
    drink(kind)), its shapes rebuilt from that script's own pixel coordinates at 0.54 mm a pixel (128 px -> 69 mm), each solid
    part 4 mm proud on an ink plate that gives it the icon's outline, the thin ink lines (handles, steam, the tea bag's string)
    as ink ribbons, the liquid bands as 1 mm details. The child object DB_Picture_<Name> holds the picture, so Unity can
    measure it on its own.
Materials: BF_Coffee, BF_Tea, BF_Cocoa, BF_Espresso, BF_Americano, BF_Latte (the machine's own, the Unity step maps them by
name) and nine DB_ ones in the icons' own colours (DB_Ink, DB_Paper, DB_PaperEdge, DB_Brown, DB_Dark, DB_Amber, DB_Cocoa,
DB_Crema, DB_Latte; DB_MATERIALS below says which is which).

The cap's front edges have a two-segment bevel (the house rule is one segment: this is the one part a player stares at from
0.9 m, and one chamfer reads as a cut, not a cushion). Everything else is one segment or flat.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402

# ----------------------------------------------------------------------------- sizes (metres, the cap's own frame)

DRINKS = ['Coffee', 'Tea', 'Cocoa', 'Espresso', 'Americano', 'Latte']   # the FBX's paddle names (Cocoa is Hot Chocolate)
ICON_OF = {'Coffee': 'coffee', 'Tea': 'tea', 'Cocoa': 'hotchocolate', 'Espresso': 'espresso',
           'Americano': 'americano', 'Latte': 'latte'}
PITCH = 0.158                     # between paddles, as BeverageStationSetup lays them
PADDLE_Z = 0.469                  # the old paddle's centre over the machine's base (its origin)
PADDLE_Y = -0.149                 # and its y: the backing's front face is 13 mm behind it (y = -0.136)

BACKING_FRONT = 0.013             # the dark backing's front face, in the cap's frame
CAP_W, CAP_H = 0.092, 0.098       # the cap, across and up (the backing is 95 x 106, so a dark 1.5-4 mm rim shows round it; the hit box 93 x 92)
CAP_ZC = -0.005                   # the cap's middle, a little under the old tile's (the backing runs -59..+47 mm)
CAP_BACK = BACKING_FRONT + 0.007  # 7 mm inside the backing (hidden)
CAP_FRONT = -0.017                # 30 mm proud of the backing (the old tile was 26; a 9 mm press is a third of it)
CAP_BEVEL = 0.009
RIM = 0.0375                      # the label's ink rim, half size
LABEL = 0.036                     # the cream label, half size (72 mm)
LABEL_R = 0.009                   # its corner radius
RIM_H, LABEL_H = 0.0010, 0.0015   # proud of the cap, of the rim
PX = 0.00054                      # a picture pixel (the icons are drawn on a 128 grid): 128 px = 69 mm, inside the 72 mm label
PLATE_H, PART_H, DETAIL_H = 0.0015, 0.0040, 0.0010
STEP, SINK = 0.0003, 0.0003       # a later part stands this much prouder; every piece sinks this much into what it stands on
OUTLINE_PX = 5.0                  # the icons' ink outline is 5 px wide; the plate shows that much round each part
PRESS = 0.009                     # the press travel the Unity step gives BeveragePaddleMotion (it was 6 mm)

# ----------------------------------------------------------------------------- materials

# The machine's own six (BeverageStationSetup's palette: its Color values are what the material shows, as sRGB), mapped by
# name in Unity and never remade there. They are also the tray's six colour chips, one under each nozzle.
BF_MATERIALS = {
    'BF_Coffee': '#5c331a', 'BF_Tea': '#699445', 'BF_Cocoa': '#994a2b', 'BF_Espresso': '#384545',
    'BF_Americano': '#4d7d8f', 'BF_Latte': '#cf994a',
}
# The icons' own colours (draw_button_icons.py), for the Unity step to make as URP Lit (smoothness 0.45, not metallic).
DB_MATERIALS = {
    'DB_Ink': ('#1f1b17', 'the icons\' INK: outlines, handles, steam, the label\'s rim'),
    'DB_Paper': ('#f7f3ee', 'PAPER: the label, saucers, foam, the cream, the paper cup, the tea tag'),
    'DB_PaperEdge': ('#d6cfc5', 'PAPER_EDGE: the americano\'s lid'),
    'DB_Brown': ('#6b3f1f', 'the coffee mug, the americano\'s band'),
    'DB_Dark': ('#3b2314', 'the coffee\'s surface, the espresso cup'),
    'DB_Amber': ('#e0a83e', 'the teacup'),
    'DB_Cocoa': ('#8a5230', 'the hot chocolate mug'),
    'DB_Crema': ('#a86a3a', 'the espresso\'s crema'),
    'DB_Latte': ('#c99d6a', 'the latte glass'),
}


def materials():
    mats = {}
    for name, hexstr in BF_MATERIALS.items():
        mats[name] = fb.material(name, hexstr, rough=0.55)
    for name, (hexstr, _why) in DB_MATERIALS.items():
        mats[name] = fb.material(name, hexstr, rough=0.6)
    return mats


# ----------------------------------------------------------------------------- 2D shapes, in the icons' 128 px grid (y down)


def rrect(x0, y0, x1, y1, r, n=4):
    """A rounded rectangle's outline, as PIL draws it: r is the corner radius, n segments a corner."""
    r = min(r, (x1 - x0) / 2.0, (y1 - y0) / 2.0)
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90.0), (x1 - r, y1 - r, 0.0), (x0 + r, y1 - r, 90.0), (x0 + r, y0 + r, 180.0)):
        for k in range(n + 1):
            a = math.radians(a0 + 90.0 * k / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return dedupe(pts)


def rrect_top(x0, y0, x1, y1, r, n=4):
    """A rectangle rounded on top only (the latte's foam: PIL's rounded band with a plain rectangle over its bottom)."""
    r = min(r, (x1 - x0) / 2.0, (y1 - y0) / 2.0)
    pts = [(x1, y1), (x0, y1)]
    for cx, cy, a0 in ((x0 + r, y0 + r, 180.0), (x1 - r, y0 + r, 270.0)):
        for k in range(n + 1):
            a = math.radians(a0 + 90.0 * k / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return dedupe(pts)


def ellipse(x0, y0, x1, y1, n=28):
    cx, cy, rx, ry = (x0 + x1) / 2.0, (y0 + y1) / 2.0, (x1 - x0) / 2.0, (y1 - y0) / 2.0
    return [(cx + rx * math.cos(2 * math.pi * k / n), cy + ry * math.sin(2 * math.pi * k / n)) for k in range(n)]


def ring(x0, y0, x1, y1, stroke, n=28):
    """PIL's ellipse outline of that width: the stroke lies inside the box. Its centre line and width."""
    h = stroke / 2.0
    return ellipse(x0 + h, y0 + h, x1 - h, y1 - h, n), stroke


def wave(cx, top, amp, height, n=11):
    """One steam wisp as draw_button_icons.steam draws it: a sine from the bottom up."""
    return [(cx + amp * math.sin(k / (n - 1.0) * math.pi * 2), top + height - height * k / (n - 1.0)) for k in range(n)]


def dedupe(pts, eps=1e-6):
    out = []
    for p in pts:
        if not out or abs(p[0] - out[-1][0]) > eps or abs(p[1] - out[-1][1]) > eps:
            out.append(p)
    if len(out) > 1 and abs(out[0][0] - out[-1][0]) < eps and abs(out[0][1] - out[-1][1]) < eps:
        out.pop()
    return out


def grow(pts, d):
    """The outline d further out (a convex outline: the offset lines' meeting points)."""
    n = len(pts)
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n)) / 2.0
    sign = 1.0 if area > 0 else -1.0
    normals = []
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        tx, ty = bx - ax, by - ay
        L = math.hypot(tx, ty) or 1.0
        normals.append((sign * ty / L, -sign * tx / L))
    out = []
    for i in range(n):
        n1, n2 = normals[i - 1], normals[i]
        dot = max(-0.9, n1[0] * n2[0] + n1[1] * n2[1])
        mx, my = (n1[0] + n2[0]) * d / (1.0 + dot), (n1[1] + n2[1]) * d / (1.0 + dot)
        out.append((pts[i][0] + mx, pts[i][1] + my))
    return out


def to_cap(pts):
    """Icon pixels (y down, the picture's centre at 64,64) -> the cap's (x, z), centred on the label."""
    return [((px - 64.0) * PX, CAP_ZC - (py - 64.0) * PX) for px, py in pts]


# ----------------------------------------------------------------------------- 3D from 2D


def prism(points_xz, y_front, y_back):
    """A flat shape (x, z) as a solid from y_front to y_back (the front is the more negative y)."""
    bm = bmesh.new()
    front = [bm.verts.new((x, y_front, z)) for x, z in points_xz]
    back = [bm.verts.new((x, y_back, z)) for x, z in points_xz]
    faces = [bm.faces.new(front), bm.faces.new(list(reversed(back)))]
    n = len(points_xz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([front[i], front[j], back[j], back[i]])
    big = [f for f in faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def ribbon(points_xz, width, y_front, y_back, closed=False):
    """A strip of that width along a polyline (x, z), as a solid from y_front to y_back."""
    n = len(points_xz)
    half = width / 2.0
    left, right = [], []
    for i, (x, z) in enumerate(points_xz):
        a = points_xz[i - 1] if closed else points_xz[max(i - 1, 0)]
        b = points_xz[(i + 1) % n] if closed else points_xz[min(i + 1, n - 1)]
        tx, tz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, tz) or 1.0
        nx, nz = -tz / L, tx / L
        left.append((x + nx * half, z + nz * half))
        right.append((x - nx * half, z - nz * half))
    bm = bmesh.new()
    lf = [bm.verts.new((x, y_front, z)) for x, z in left]
    rf = [bm.verts.new((x, y_front, z)) for x, z in right]
    lb = [bm.verts.new((x, y_back, z)) for x, z in left]
    rb = [bm.verts.new((x, y_back, z)) for x, z in right]
    for i in (range(n) if closed else range(n - 1)):
        j = (i + 1) % n
        bm.faces.new([lf[i], rf[i], rf[j], lf[j]])
        bm.faces.new([lb[j], rb[j], rb[i], lb[i]])
        bm.faces.new([lf[j], lb[j], lb[i], lf[i]])
        bm.faces.new([rf[i], rb[i], rb[j], rf[j]])
    if not closed:
        bm.faces.new([lf[0], lb[0], rb[0], rf[0]])
        bm.faces.new([rf[-1], rb[-1], lb[-1], lf[-1]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# ----------------------------------------------------------------------------- the pictures (draw_button_icons.drink, in 3D)

# Each picture is a list of pieces, in the icon's own drawing order:
#   ('part', shape_px, material, layer)        a solid 4 mm proud, on an ink plate 5 px bigger (the icon's outline)
#   ('ink', polyline_px, width_px, closed)     an ink ribbon at the parts' depth (handles, steam, a string, a line)
#   ('detail', shape_px, material, layer)      1 mm proud of the part under it (a liquid's surface, a band)
# layer: a later part stands STEP prouder than an earlier one it overlaps (the cup on its saucer), never coplanar.


def steam(cx, top, small=False):
    amp, height = (4, 14) if small else (6, 22)
    return [('ink', wave(cx + dx, top, amp, height), 4, False) for dx in ((-8, 8) if small else (-14, 0, 14))]


def picture(kind):
    if kind == 'latte':
        return [('part', rrect(36, 26, 92, 116, 10), 'DB_Latte', 0),
                ('detail', rrect_top(36, 26, 92, 50, 10), 'DB_Paper', 0),      # the foam band: round on top, its bottom squared as the icon squares it
                ('ink', [(39, 50), (89, 50)], 4, False)] + steam(64, 14)
    if kind == 'coffee':
        rc, w = ring(82, 50, 112, 90, 9)
        return [('ink', rc, w, True),
                ('part', rrect(28, 34, 90, 114, 12), 'DB_Brown', 0),
                ('detail', rrect(34, 40, 84, 54, 6), 'DB_Dark', 0)] + steam(58, 18)
    if kind == 'tea':
        rc, w = ring(90, 54, 116, 84, 8)
        return [('part', ellipse(14, 96, 114, 118), 'DB_Paper', 0),
                ('ink', rc, w, True),
                ('part', rrect(28, 44, 96, 102, 16), 'DB_Amber', 1),
                ('ink', [(56, 46), (44, 22)], 4, False),
                ('part', rrect(30, 10, 54, 28, 4), 'DB_Paper', 2)]
    if kind == 'espresso':
        rc, w = ring(82, 62, 104, 86, 7)
        return [('part', ellipse(24, 94, 104, 114), 'DB_Paper', 0),
                ('ink', rc, w, True),
                ('part', rrect(40, 56, 88, 100, 12), 'DB_Dark', 1),
                ('detail', rrect(46, 60, 82, 70, 5), 'DB_Crema', 1)] + steam(64, 36, small=True)
    if kind == 'hotchocolate':
        rc, w = ring(82, 56, 112, 94, 9)
        return [('ink', rc, w, True),
                ('part', rrect(28, 40, 90, 114, 12), 'DB_Cocoa', 0),
                ('part', ellipse(30, 22, 88, 54), 'DB_Paper', 1),
                ('part', ellipse(42, 12, 76, 38), 'DB_Paper', 2),
                ('part', ellipse(52, 6, 66, 20), 'DB_Paper', 3)]
    if kind == 'americano':
        return [('part', [(34, 40), (94, 40), (86, 118), (42, 118)], 'DB_Paper', 0),
                ('detail', [(37, 62), (91, 62), (87, 92), (41, 92)], 'DB_Brown', 0),
                ('part', rrect(28, 26, 100, 42, 6), 'DB_PaperEdge', 1),
                ('part', rrect(50, 14, 78, 28, 5), 'DB_PaperEdge', 2)]
    raise ValueError(kind)


# ----------------------------------------------------------------------------- building


def build_cap(name, mats, collection=None):
    """The cap with its label: one object, DB_Cap_<Name>."""
    p = fb.Prop('DB_Cap_' + name, floor=False)
    x0, x1 = -CAP_W / 2, CAP_W / 2
    z0, z1 = CAP_ZC - CAP_H / 2, CAP_ZC + CAP_H / 2
    p.add('cap', fb.rbox(x0, x1, CAP_FRONT, CAP_BACK, z0, z1, bevel=CAP_BEVEL, segments=2), mats['BF_' + name])
    rim = to_cap(rrect(64 - RIM / PX, 64 - RIM / PX, 64 + RIM / PX, 64 + RIM / PX, (LABEL_R + RIM - LABEL) / PX, 5))
    p.add('rim', prism(rim, CAP_FRONT - RIM_H, CAP_FRONT + SINK), mats['DB_Ink'])
    label = to_cap(rrect(64 - LABEL / PX, 64 - LABEL / PX, 64 + LABEL / PX, 64 + LABEL / PX, LABEL_R / PX, 5))
    p.add('label', prism(label, CAP_FRONT - RIM_H - LABEL_H, CAP_FRONT - RIM_H + SINK), mats['DB_Paper'])
    ob = p.finish(collection)
    return ob, p


def build_picture(name, mats, collection=None):
    """The drink's picture in relief: one object, DB_Picture_<Name>, standing on the label."""
    kind = ICON_OF[name]
    p = fb.Prop('DB_Picture_' + name, floor=False)
    label_front = CAP_FRONT - RIM_H - LABEL_H
    for piece in picture(kind):
        what = piece[0]
        if what == 'part':
            _, shape, mat, layer = piece
            plate_front = label_front - PLATE_H - layer * STEP
            p.add('plate', prism(to_cap(grow(shape, OUTLINE_PX)), plate_front, label_front + SINK), mats['DB_Ink'])
            p.add('part', prism(to_cap(shape), plate_front - PART_H, plate_front + SINK), mats[mat])
        elif what == 'detail':
            _, shape, mat, layer = piece
            part_front = label_front - PLATE_H - layer * STEP - PART_H
            p.add('detail', prism(to_cap(shape), part_front - DETAIL_H, part_front + SINK), mats[mat])
        elif what == 'ink':
            _, line, width, closed = piece
            # a handle sits a hair behind the cup it belongs to (drawn first in the icon): never coplanar with it
            front = label_front - PLATE_H - PART_H + STEP
            back = label_front + SINK
            if kind == 'latte' and not closed and len(line) == 2:
                front = label_front - PLATE_H - PART_H - DETAIL_H - 0.0005   # the foam's line lies on the foam
                back = front + 0.0012
            p.add('ink', ribbon(to_cap(line), width * PX, front, back, closed=closed), mats['DB_Ink'])
    ob = p.finish(collection)
    return ob, p


def build_all(collection=None):
    mats = materials()
    results = []
    for name in DRINKS:
        cap, cap_prop = build_cap(name, mats, collection)
        pic, pic_prop = build_picture(name, mats, collection)
        pic.parent = cap
        for ob, prop in ((cap, cap_prop), (pic, pic_prop)):
            n_islands, floating = fb.audit(ob)
            st = fb.stats(ob)
            results.append({'name': name, 'object': ob, 'prop': prop, 'islands': n_islands, 'floating': floating,
                            'tris': st['tris'], 'size': st['size'], 'min': st['min'], 'max': st['max'],
                            'materials': st['materials'], 'turned': ob.get('fixit_shells_turned', 0)})
    return results


def machine_place(index):
    """Where button `index` (0 Coffee .. 5 Latte) sits in the machine's frame (BeverageDispenserV2.fbx, Blender axes)."""
    return ((index - 2.5) * PITCH, PADDLE_Y, PADDLE_Z)
