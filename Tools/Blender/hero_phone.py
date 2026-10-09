"""
The hero phone, v3.2 (9 Oct 2026, 02:00): PhoneRepair.prefab's pieces (a cube body, cube screens, a cube back cover,
cylinder screws) as real meshes, built from the iPhone 15's measurements (claude/hero-reference-sheet.md) in the house
style (hero_lib.py). Devices 2 puts each mesh into the prefab's existing object, gives it a collider that is its solid
(HeroDeviceSteps: head / faceY / boxes), and moves the three positions the thickness fixes (moves); everything else the
bench and the labs know (names, parents, scripts, the grime under the Screen) stays.

The prefab's frame (Unity): X across the phone (70 mm), Y through it (+Y the front), Z along it (140 mm; the screws
are at -Z, so -Z is the bottom end). Blender here: X across, Y along (Unity +Z is Blender -Y: the top end is -Y, the
bottom end with the port and the screws is +Y), Z through (+Z the front).

v3.2 is the real SECTION. v3 kept the stand-in's 13 mm (screen top at +6, cover face at -7, a glass plate standing
1.4 mm proud of a slab) and Mansoor's verdict was "the phone's front screen is so thick it doesn't look real". A real
iPhone 15 is 7.8 mm: a flat rail about 1 mm wide at the face, the front glass sitting INSIDE the rail and flush with
it behind a hairline reveal, a 2.5 mm black border under the glass, the back glass flush too, and only the camera
island proud of anything (about 2 mm). So, in Unity millimetres through the phone (+ is the front):

  +3.9   the rail's front edge, the front glass's top (flush), the black border's top
  +2.7   the chassis's front face (black; what shows, 1.2 mm down, when the screen is lifted out)
  -1.5   the chassis's back face, where the battery, the coil, the camera module and the board shield stand in relief
  -2.7   the back glass's inner face (its lip rises to -1.5 and meets the chassis)
  -3.9   the back glass's outer face; the screw heads sit on it; the island stands proud of it to -5.8

  Body       the rail ring (70 x 140 x 7.8, corner radius 10, wall 1, a 0.6 mm chamfer on its outer edges); the black
             border ring inside it (2.5 mm wide, from the chassis up to the rail's edge); the chassis (black) filling
             the ring from -1.5 to +2.7; the side button (right), the ring switch and two volumes (left), antenna
             lines; the bottom edge with the USB-C port, two pentalobe screws and the 3 + 6 holes; on the chassis's
             back face the inside a back-glass removal shows: the battery with its label and charging coil, the
             camera module under the island, the board shield.
  Screen     the display glass, 62.8 x 132.8 x 1.2, sitting in the border ring with a 0.1 mm reveal, its top flush at
             +3.9; the Screen object moves from +5 to +3.3. The FRESH part (it keeps M_FreshGlass; one material, so
             the border has to be the body's).
  ScreenCracked  the BROKEN part (v3.3, his note "a cracked screen should look like a cracked screen"): the same glass
             (it keeps M_BrokenGlass) with the crack on it as geometry, in HD_Crack: one impact near the top-right
             corner as the player sees it, eight radial cracks running to the edges with the small jogs real ones
             have, branches off five of them, three broken concentric arcs round the impact, chips at the point.
             Thin ribbons 0.4-0.8 mm wide standing 0.08 mm proud of the glass, so the light catches them as it does
             a real crack. The screen is the part the player pinches out, so the crack is in the mesh and travels
             with it to the tray.
  BackCover  the back glass, 67.6 x 137.6 x 1.2 (a 0.2 mm reveal inside the rail), outer face at -3.9, with the camera
             island (30 x 33, 1.0 mm plateau, two diagonal lens rings 0.9 mm more, the flash, the mic) and the black
             inner lip up to the chassis; the BackCover object moves from -5.5 to -3.3.
  Screw      a button-head Phillips screw, 8 mm head (a cartoon size kept so it can be hit from 50 cm; a real one is
             1.2 mm), 1.6 mm tall, its dome facing out (-Z), the shank going +Z; the screws move from -8 to -4.7 so
             the heads sit on the back glass. Used twice.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
from hero_lib import Faceted, chamfered_slab, disc, moved, revolve, ring_wall, rrect_wall, to_blender  # noqa: E402

# (sRGB hex, Unity smoothness, Unity metallic): metallic 0 throughout
HD_MATERIALS = {
    'HD_PhoneRail':   ('#8FA08C', 0.35, 0.0),   # anodised aluminium rail and buttons, the back's darker sage
    'HD_PhoneBack':   ('#B9C9B6', 0.30, 0.0),   # matte colour-infused back glass (sage); the antenna lines
    'HD_PhoneIsland': ('#A7B8A4', 0.55, 0.0),   # the camera island's glossier plateau
    'HD_PhoneBlack':  ('#1F1D1B', 0.45, 0.0),   # the front bezel, the port, the battery wrap, the camera module, the frame lip
    'HD_PhoneSteel':  ('#B9B5AD', 0.45, 0.0),   # board shields, Taptic engine, lens rings, the pentalobe screws
    'HD_PhoneCopper': ('#B07A4A', 0.40, 0.0),   # the charging coil
    'HD_Lens':        ('#141414', 0.80, 0.0),   # camera glass
    'HD_Flash':       ('#E8E2CF', 0.50, 0.0),   # the flash window
    'HD_Screw':       ('#93908A', 0.45, 0.0),   # the two case screws
    'HD_ScrewSlot':   ('#2A2D31', 0.30, 0.0),   # the cross recess
    'HD_Glass':       ('#20355A', 0.70, 0.0),   # the screen in the FBX only: the prefab keeps M_BrokenGlass / M_FreshGlass
    'HD_Crack':       ('#E9ECEF', 0.35, 0.0),   # the cracks: scattered light, white-grey on the dark glass
}

PREFAB_OFFSETS = {        # Unity's frame; v3.2: where Devices 2 MOVES the prefab's objects to (HeroDeviceSteps moves)
    'ScreenCracked': (0.0, 0.0033, 0.0),       # the broken part, in its seat
    'Screen':    (-0.090, 0.0, 0.0),           # the fresh part: beside the phone in the FBX assembly (the prefab's Fresh sits in the seat, off)
    'BackCover': (0.0, -0.0033, 0.0),
    'Screw':     (0.022, -0.0047, -0.058),
}

W, L, T = .070, .140, .0078   # the real 7.8 mm (v3.2); 70 x 140 in plan is the prefab's (the real 71.6 x 147.6)
CORNER = .010
RAIL_WALL, RAIL_CHAMFER = .0010, .0006
FRONT = T / 2                 # +3.9: the rail's front edge, the glass's top, the border's top
CHASSIS_TOP, CHASSIS_BACK = .0027, -.0015
BORDER = .0025                # the black border under the glass
ISLAND = (.0150, -.0485)      # the camera island's centre on the back (x, y here): 5 mm in from the top and the right (seen from the back)


def rail_ring(z0, z1, sx, sy, rc, wall, c, per_corner=4):
    """A rounded-rectangle ring (the rail) with ONE chamfer on its outer top and bottom edges, closed top and bottom."""
    bm = bmesh.new()

    def ring(z, s_x, s_y, r):
        return [bm.verts.new(q) for q in hl.ring_rrect(0, 0, z, s_x, s_y, r, per_corner=per_corner)]

    o0 = ring(z0, sx - 2 * c, sy - 2 * c, max(.0003, rc - c))
    o1 = ring(z0 + c, sx, sy, rc)
    o2 = ring(z1 - c, sx, sy, rc)
    o3 = ring(z1, sx - 2 * c, sy - 2 * c, max(.0003, rc - c))
    i0 = ring(z0, sx - 2 * wall, sy - 2 * wall, max(.0002, rc - wall))
    i1 = ring(z1, sx - 2 * wall, sy - 2 * wall, max(.0002, rc - wall))
    n = len(o0)

    def band(a, b):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([a[i], a[j], b[j], b[i]])

    band(o0, o1); band(o1, o2); band(o2, o3)     # the outer face with its two chamfers
    band(o3, i1)                                 # the front edge
    band(i1, i0)                                 # the inner wall
    band(i0, o0)                                 # the back edge
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def M(name):
    return hl.material(HD_MATERIALS, name)


def body():
    p = Faceted('HD_Phone')
    rail, back, black, steel, copper = M('HD_PhoneRail'), M('HD_PhoneBack'), M('HD_PhoneBlack'), M('HD_PhoneSteel'), M('HD_PhoneCopper')
    inner_w, inner_l = W - 2 * RAIL_WALL, L - 2 * RAIL_WALL          # 68 x 138: inside the rail
    # the rail: a 1 mm ring, 7.8 mm through, a 0.6 mm chamfer on its outer edges (the 15's contoured edge)
    p.add('rail', rail_ring(-FRONT, FRONT, W, L, CORNER, RAIL_WALL, RAIL_CHAMFER), rail)
    # the chassis fills the ring from the inside's floor (-1.5) to just under the glass (+2.7); black: its front face
    # is what shows when the screen is lifted out, its back face carries the inside (0.1 mm clear of the rail's wall)
    p.add('chassis', chamfered_slab(inner_w - .0002, inner_l - .0002, CHASSIS_TOP - CHASSIS_BACK, CORNER - RAIL_WALL, .0001,
                                    per_corner=4, centre_z=(CHASSIS_TOP + CHASSIS_BACK) / 2), black)
    # the black border under the glass: a 2.5 mm ring from the chassis up to the rail's edge; the glass sits inside it
    p.add('border', rrect_wall(CHASSIS_TOP, FRONT, inner_w - .0002, inner_l - .0002, CORNER - RAIL_WALL, BORDER, per_corner=4), black)
    # side buttons, 0.6 mm proud of the rail; the ring switch is shorter and sits higher
    def button(x0, x1, y0, y1):
        b = fb.zbox(x0, x1, y0, y1, -.00125, .00125)
        fb.bevel_all(b, .0003, segments=1)
        return b
    p.add('side button', button(.0348, .0356, -.0320, -.0080), rail)
    p.add('ring switch', button(-.0356, -.0348, -.0440, -.0350), rail)
    p.add('volume up', button(-.0356, -.0348, -.0290, -.0140), rail)
    p.add('volume down', button(-.0356, -.0348, -.0120, .0030), rail)
    # antenna lines: thin lighter stripes across the rail's flat band near the four corners
    for sx in (1, -1):
        for y in (-.060, .060):
            p.add('antenna line', fb.zbox(sx * .03490, sx * .03515, y - .0005, y + .0005, -.0032, .0032), back)
    # the bottom edge (+Y): the USB-C port (8.4 x 2.6), the two pentalobe screws, the 3 + 6 holes
    p.add('usb-c', fb.zbox(-.0042, .0042, .0699, .0702, -.0013, .0013), black)
    for sx in (1, -1):
        s_ = disc(0, 0, .0699, .0703, .0008, sides=8)
        fb.turn(s_, -90.0, 'X')
        p.add('pentalobe screw', moved(s_, sx * .0068, 0, 0), steel)
    for x in [-.0120, -.0100, -.0080] + [.0080, .0100, .0120, .0140, .0160, .0180]:
        h = disc(0, 0, 0.0, .0004, .0006, sides=8)
        fb.turn(h, -90.0, 'X')
        p.add('hole', moved(h, x, .0699, 0), black)
    # the INSIDE, in relief on the chassis's back face (-1.5), toward the back glass (whose inner face is at -2.7;
    # the cover's lip is 1.2 mm wide, so everything sits inside a 65 x 135 field and nothing reaches past -2.6)
    zb = CHASSIS_BACK
    p.add('battery', chamfered_slab(.0480, .0950, .0010, .0020, .0003, per_corner=2, centre_z=zb - .0005), black)
    p.add('battery label', fb.zbox(-.0180, .0100, -.0100, .0220, zb - .00105, zb - .00100), back)
    p.add('charging coil', ring_wall(zb - .00112, zb - .00100, .0070, .0220, sides=24), copper)
    p.add('coil ring', ring_wall(zb - .00108, zb - .00100, .0220, .0240, sides=24), steel)
    cam = chamfered_slab(.0300, .0300, .0010, .0030, .0003, per_corner=2, centre_z=zb - .0005)
    p.add('camera module', moved(cam, ISLAND[0], ISLAND[1], 0), black)
    for (cx, cy) in ((ISLAND[0] - .0070, ISLAND[1] - .0068), (ISLAND[0] + .0070, ISLAND[1] + .0068)):
        p.add('camera barrel', disc(cx, cy, zb - .0011, zb - .0010, .0050, sides=16), steel)
    shield = chamfered_slab(.0220, .0520, .0008, .0010, .0003, per_corner=1, centre_z=zb - .0004)
    p.add('board shield', moved(shield, -.0210, -.0380, 0), steel)
    # (no speaker, Taptic engine, port assembly or board screws: the battery, the coil, the camera module and the board
    # shield are what say "a phone's inside"; the rest was clutter at 50 cm)
    return p


def screen():
    """The display glass, centred on its origin (Unity y +3.3): 62.8 x 132.8 x 1.2, from +2.7 (on the chassis) to +3.9
    (flush with the rail), a 0.1 mm reveal to the border ring, a 0.3 mm edge chamfer (2.5D glass)."""
    p = Faceted('Screen')
    w = W - 2 * RAIL_WALL - 2 * BORDER - .0004      # 62.8 (the border's inside less 0.1 a side, and the ring's own 0.1)
    l = L - 2 * RAIL_WALL - 2 * BORDER - .0004      # 132.8
    p.add('glass', chamfered_slab(w, l, FRONT - CHASSIS_TOP, CORNER - RAIL_WALL - BORDER, .0003, per_corner=4, centre_z=0.0), M('HD_Glass'))
    return p


def _glass_plan():
    """The display glass's plan: (half width, half length, corner radius)."""
    w = W - 2 * RAIL_WALL - 2 * BORDER - .0004
    l = L - 2 * RAIL_WALL - 2 * BORDER - .0004
    return w / 2, l / 2, CORNER - RAIL_WALL - BORDER


def _inside_glass(x, y, inset=.0004):
    """Inside the rounded-rectangle glass plan, a little in from its edge (a crack stops at the edge, never past it)."""
    hw, hl, rc = _glass_plan()
    qx, qy = abs(x) - (hw - rc), abs(y) - (hl - rc)
    d = math.hypot(max(qx, 0.0), max(qy, 0.0)) - rc
    return d < -inset


def _to_edge(p, q, steps=14):
    """p inside, q outside: the point on the glass's edge between them."""
    a, b = p, q
    for _ in range(steps):
        m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if _inside_glass(*m):
            a = m
        else:
            b = m
    return a


def _ribbon(bm, pts, widths, z_bot, z_top):
    """A crack: a thin strip along a polyline, standing from z_bot to z_top, its width per vertex."""
    n = len(pts)
    if n < 2:
        return
    dirs = []
    for i in range(n):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, n - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dy) or 1.0
        dirs.append((dx / ln, dy / ln))
    L, R = [], []
    for (x, y), (dx, dy), w in zip(pts, dirs, widths):
        nx, ny = -dy * w / 2, dx * w / 2
        L.append((bm.verts.new((x + nx, y + ny, z_top)), bm.verts.new((x + nx, y + ny, z_bot))))
        R.append((bm.verts.new((x - nx, y - ny, z_top)), bm.verts.new((x - nx, y - ny, z_bot))))
    for i in range(n - 1):
        bm.faces.new([L[i][0], R[i][0], R[i + 1][0], L[i + 1][0]])        # the top
        bm.faces.new([L[i][1], L[i][0], L[i + 1][0], L[i + 1][1]])        # the left wall
        bm.faces.new([R[i][0], R[i][1], R[i + 1][1], R[i + 1][0]])        # the right wall
        bm.faces.new([R[i][1], L[i][1], L[i + 1][1], R[i + 1][1]])        # the bottom, inside the glass (closes the solid)
    bm.faces.new([L[0][1], R[0][1], R[0][0], L[0][0]])                    # the end caps
    bm.faces.new([R[-1][1], L[-1][1], L[-1][0], R[-1][0]])


def _walk(rng, start, heading, step, wobble, max_len):
    """A crack's path: steps with a small random turn each, until it leaves the glass (clipped to the edge) or runs out."""
    pts = [start]
    x, y = start
    travelled = 0.0
    while travelled < max_len:
        heading += rng.uniform(-wobble, wobble)
        ln = step * rng.uniform(.7, 1.3)
        nx_, ny_ = x + ln * math.cos(heading), y + ln * math.sin(heading)
        if not _inside_glass(nx_, ny_):
            pts.append(_to_edge((x, y), (nx_, ny_)))
            break
        pts.append((nx_, ny_))
        x, y = nx_, ny_
        travelled += ln
    return pts


def crack_web(seed=7, impact=(-.019, -.046)):
    """The crack as geometry on the glass's top face (z +0.6): radials, branches, arcs, chips. One mesh."""
    import random
    rng = random.Random(seed)
    bm = bmesh.new()
    half = (FRONT - CHASSIS_TOP) / 2             # the glass is centred on its origin: its top face is at +0.6
    z_bot, z_top = half - .00002, half + .00008   # the ribbons sink 0.02 into the glass and stand 0.08 proud of it
    # where cracks cross, their tops must not share a height (coplanar tops fight in any renderer): each radial stands
    # a hundredth of a millimetre above the last, the arcs sit under them all, the branches over, the crater over all
    step = .00001
    px, py = impact
    radials = []
    n = 8
    for i in range(n):
        heading = 2 * math.pi * i / n + rng.uniform(-.25, .25)
        max_len = rng.choice([.040, .060, .090, .140, .140, .140, .140, .140])
        pts = _walk(rng, (px, py), heading, .0055, .16, max_len)
        if len(pts) < 2:
            continue
        radials.append(pts)
        m = len(pts) - 1
        widths = [.0008 - (.0008 - .00035) * (k / m) for k in range(len(pts))]
        _ribbon(bm, pts, widths, z_bot, z_top + i * step)
    # branches: off five radials, part way along, at a shallow angle, shorter and thinner
    for pts in rng.sample(radials, min(5, len(radials))):
        k = rng.randint(max(1, len(pts) // 3), max(1, (2 * len(pts)) // 3))
        a, b = pts[k - 1], pts[k]
        heading = math.atan2(b[1] - a[1], b[0] - a[0]) + rng.choice([-1, 1]) * rng.uniform(.5, .9)
        br = _walk(rng, pts[k], heading, .0045, .2, rng.uniform(.010, .030))
        if len(br) >= 2:
            _ribbon(bm, br, [.00035] * len(br), z_bot, z_top + (n + 1) * step)
    # arcs: three broken rings round the impact, each missing a sector
    for r0 in (.0050, .0092, .0150):
        gap_at = rng.uniform(0, 2 * math.pi)
        gap = rng.uniform(.9, 1.6)
        pts = []
        a = gap_at + gap / 2
        while a < gap_at + 2 * math.pi - gap / 2:
            r = r0 * rng.uniform(.90, 1.10)
            q = (px + r * math.cos(a), py + r * math.sin(a))
            if _inside_glass(*q):
                pts.append(q)
            elif len(pts) >= 2:
                _ribbon(bm, pts, [.0004] * len(pts), z_bot, z_top - 2 * step)
                pts = []
            else:
                pts = []
            a += .32
        if len(pts) >= 2:
            _ribbon(bm, pts, [.0004] * len(pts), z_bot, z_top - 2 * step)
    # the point of impact: a crushed crater, a ragged white disc standing over the junction of the radials (which
    # hides their overlapping ends), with chips flaking off it
    ring = []
    for i in range(14):
        a = 2 * math.pi * i / 14
        r = rng.uniform(.0013, .0021)
        ring.append((px + r * math.cos(a), py + r * math.sin(a)))
    z_cr = z_top + (n + 3) * step
    lo = [bm.verts.new((x, y, z_bot)) for x, y in ring]
    hi = [bm.verts.new((x, y, z_cr)) for x, y in ring]
    bm.faces.new(hi)
    bm.faces.new(list(reversed(lo)))
    for k in range(14):
        m = (k + 1) % 14
        bm.faces.new([lo[k], lo[m], hi[m], hi[k]])
    for i in range(5):
        a = 2 * math.pi * i / 5 + rng.uniform(-.3, .3)
        r0, r1 = rng.uniform(.0018, .0024), rng.uniform(.0030, .0042)
        tri = [(px + r0 * math.cos(a - .25), py + r0 * math.sin(a - .25)), (px + r1 * math.cos(a), py + r1 * math.sin(a)),
               (px + r0 * math.cos(a + .25), py + r0 * math.sin(a + .25))]
        lo = [bm.verts.new((x, y, z_bot)) for x, y in tri]
        hi = [bm.verts.new((x, y, z_cr)) for x, y in tri]
        bm.faces.new(hi)
        bm.faces.new(list(reversed(lo)))
        for k in range(3):
            m = (k + 1) % 3
            bm.faces.new([lo[k], lo[m], hi[m], hi[k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])      # everything here is a closed solid: this resolves them all
    return bm


def screen_cracked():
    """The broken screen: the display glass with the crack web on it. Keeps M_BrokenGlass in Unity (slot 0); the crack
    is the FBX's HD_Crack (slot 1)."""
    p = Faceted('ScreenCracked')
    w = W - 2 * RAIL_WALL - 2 * BORDER - .0004
    l = L - 2 * RAIL_WALL - 2 * BORDER - .0004
    p.add('glass', chamfered_slab(w, l, FRONT - CHASSIS_TOP, CORNER - RAIL_WALL - BORDER, .0003, per_corner=4, centre_z=0.0), M('HD_Glass'))
    p.add('cracks', crack_web(), M('HD_Crack'))
    return p


def back_cover():
    """Centred on its origin at Unity y -3.3: the glass from -3.9 to -2.7 (local -0.6 .. +0.6), the lip up to -1.5
    (local +1.8), the island proud of the glass to -5.8 (local -2.5). Devices 2 stops its collider at the glass's
    outer face (faceY -0.6): the island is not the cover, or it would catch the cursor before the screws."""
    p = Faceted('BackCover')
    back, island_m, black, steel, lens, flash = M('HD_PhoneBack'), M('HD_PhoneIsland'), M('HD_PhoneBlack'), M('HD_PhoneSteel'), M('HD_Lens'), M('HD_Flash')
    gw, gl = W - 2 * RAIL_WALL - .0004, L - 2 * RAIL_WALL - .0004      # 67.6 x 137.6: a 0.2 mm reveal inside the rail
    p.add('glass', chamfered_slab(gw, gl, .0012, CORNER - RAIL_WALL - .0002, .0004, per_corner=4, centre_z=0.0), back)
    p.add('frame lip', rrect_wall(.0006, .0018, gw, gl, CORNER - RAIL_WALL - .0002, .0012, per_corner=4), black)
    bx, by = ISLAND
    plateau = chamfered_slab(.030, .033, .0010, .0070, .0003, per_corner=3, centre_z=-.0011)
    p.add('camera island', moved(plateau, bx, by, 0), island_m)
    for (cx, cy) in ((bx - .0070, by - .0068), (bx + .0070, by + .0068)):
        p.add('lens ring', moved(ring_wall(-.0025, -.0016, .0048, .0070, sides=24), cx, cy, 0), steel)
        p.add('lens', disc(cx, cy, -.0022, -.0016, .0049, sides=16), lens)
    p.add('flash', disc(bx + .0085, by - .0085, -.0020, -.0016, .0025, sides=12), flash)
    p.add('mic', disc(bx - .0090, by + .0090, -.0018, -.0016, .0008, sides=8), black)
    return p


def screw():
    p = Faceted('Screw')
    steel, slot_m = M('HD_Screw'), M('HD_ScrewSlot')
    # a button head, 8 mm across and 1.6 mm tall (v3.2: was 2.0 on the 13 mm phone), its dome facing -Z (out of the
    # device); z = 0 is its middle, so Devices 2's head box (8 x 1.6 x 8) round the origin is exactly the head
    head = revolve([(-.0008, .0030), (-.0005, .0038), (.0000, .0040), (.0008, .0040)], sides=16, bottom_tip=(0, 0, -.0009))
    p.add('head', head, steel)
    for rot in (0.0, 90.0):
        slot = fb.zbox(-.0024, .0024, -.00045, .00045, -.0011, -.0005)
        fb.turn(slot, rot, 'Z')
        p.add('slot', slot, slot_m)
    prof = [(.0008, .0012)]
    for i in range(3):
        z = .0014 + i * .0012
        prof += [(z, .0010), (z + .0006, .0013)]
    prof += [(.0052, .0010), (.0056, .0006)]
    p.add('shank', revolve(prof, sides=10, tip=(0, 0, .0060)), steel)
    return p


PIECES = [
    # (builder, the prefab object(s) the mesh goes into)
    (body, ['Body']),
    (screen_cracked, ['Broken']),
    (screen, ['Fresh']),
    (back_cover, ['BackCover']),
    (screw, ['Screw0', 'Screw1']),
]


def build():
    """The phone, assembled as the prefab assembles it: the body is the root, the others its children at the prefab's
    offsets. Returns (root, children, [(object, targets)])."""
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
            children.append(ob)
    return root, children, made
