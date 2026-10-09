"""
The hero phone, v3 (9 Oct 2026): PhoneRepair.prefab's pieces (a cube body, cube screens, a cube back cover, cylinder
screws) as real meshes, built from the iPhone 15's measurements (claude/hero-reference-sheet.md) in the house style
(hero_lib.py), to the SAME SIZES AND PLACES as the prefab's pieces, so Devices 2 puts each mesh into the prefab's
existing object and every collider, screw home, seat, grime spot and script stays where the bench and the labs know it.

The prefab's frame (Unity): X across the phone (70 mm), Y through it (+Y the front), Z along it (140 mm; the screws
are at -Z, so -Z is the bottom end). Blender here: X across, Y along (Unity +Z is Blender -Y: the top end is -Y, the
bottom end with the port and the screws is +Y), Z through (+Z the front).

What the stand-in fixes in place: the screen's top at +6 mm (the grime spots sit on it), the back cover's outer face at
-7 mm (the screw heads at -8 rest on it), the screws at (+/-22, -8, -58). So the phone is 13 mm thick where a real one is
7.8: the proportions in plan (corner radius 10, the island, the buttons, the bottom edge) carry the realism.

  Body       the flat aluminium rail, 70 x 140 x 8, corner radius 10, a 1 mm chamfer; the black front bezel plate; the
             side button (right), the ring switch and two volume buttons (left), antenna lines; the
             bottom edge with the USB-C port, two pentalobe screws and the 3 + 6 holes; and on the BACK FACE, what a
             back-glass removal shows: the battery with its charging coil, the camera module, the board shield
             (seen when the cover is off).
  Screen     65 x 134 x 1.5 glass (a 2.5 mm bezel), from +4.5 to +6.0. Used twice (Broken / Fresh keep their materials).
  BackCover  the back glass, 68 x 138 x 1.2, its outer face at -7, with the camera island (30 x 33, two diagonal lens
             rings, the flash, the mic) and a black inner frame lip reaching back to the rail.
  Screw      a pan-head Phillips screw, 8 mm head (a cartoon size kept so it can be hit from 50 cm), dome facing out
             (-Z), the shank going +Z. Used twice.
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
}

PREFAB_OFFSETS = {        # Unity's frame
    'Screen':    (0.0, 0.005, 0.0),
    'BackCover': (0.0, -0.0055, 0.0),
    'Screw':     (0.022, -0.008, -0.058),
}

W, L, T = .070, .140, .008
CORNER = .010
ISLAND = (.0150, -.0485)      # the camera island's centre on the back (x, y here): 5 mm in from the top and the right (seen from the back)


def M(name):
    return hl.material(HD_MATERIALS, name)


def body():
    p = Faceted('HD_Phone')
    rail, back, black, steel, copper = M('HD_PhoneRail'), M('HD_PhoneBack'), M('HD_PhoneBlack'), M('HD_PhoneSteel'), M('HD_PhoneCopper')
    # the rail: a flat band with a 1 mm chamfer into each face
    p.add('rail', chamfered_slab(W, L, T, CORNER, .0010, per_corner=4), rail)
    # the front: the black bezel plate the screen sits on
    p.add('front bezel', chamfered_slab(.068, .138, .0006, .0090, .0002, per_corner=4, centre_z=.0043), black)
    # side buttons, 0.6 mm proud of the rail; the ring switch is shorter and sits higher
    def button(x0, x1, y0, y1):
        b = fb.zbox(x0, x1, y0, y1, -.00125, .00125)
        fb.bevel_all(b, .0003, segments=1)
        return b
    p.add('side button', button(.0348, .0356, -.0320, -.0080), rail)
    p.add('ring switch', button(-.0356, -.0348, -.0440, -.0350), rail)
    p.add('volume up', button(-.0356, -.0348, -.0290, -.0140), rail)
    p.add('volume down', button(-.0356, -.0348, -.0120, .0030), rail)
    # antenna lines: thin lighter stripes across the rail near the four corners
    for sx in (1, -1):
        for y in (-.060, .060):
            p.add('antenna line', fb.zbox(sx * .03490, sx * .03515, y - .0005, y + .0005, -.0038, .0038), back)
    # the bottom edge (+Y): the USB-C port, the two pentalobe screws, the 3 + 6 holes
    p.add('usb-c', fb.zbox(-.0045, .0045, .0699, .0702, -.00165, .00165), black)
    for sx in (1, -1):
        s = disc(0, 0, .0699, .0703, .0008, sides=8)
        fb.turn(s, -90.0, 'X')
        p.add('pentalobe screw', moved(s, sx * .0068, 0, 0), steel)
    for x in [-.0120, -.0100, -.0080] + [.0080, .0100, .0120, .0140, .0160, .0180]:
        h = disc(0, 0, 0.0, .0004, .0006, sides=8)
        fb.turn(h, -90.0, 'X')
        p.add('hole', moved(h, x, .0699, 0), black)
    # the BACK FACE (-Z): what the back glass covers. The cover's inner lip is 1.2 mm wide, so everything sits inside
    # a 65 x 135 field, and nothing reaches past z = -5.6 (the glass's inner face is at -5.8).
    p.add('inner frame', chamfered_slab(.0650, .1350, .0004, .0085, .0001, per_corner=4, centre_z=-.0042), black)
    p.add('battery', chamfered_slab(.0480, .0950, .0012, .0020, .0003, per_corner=2, centre_z=-.0050), black)
    p.add('battery label', fb.zbox(-.0180, .0100, -.0100, .0220, -.00565, -.00560), back)
    p.add('charging coil', ring_wall(-.00574, -.00560, .0070, .0220, sides=24), copper)
    p.add('coil ring', ring_wall(-.00570, -.00560, .0220, .0240, sides=24), steel)
    cam = chamfered_slab(.0300, .0300, .0012, .0030, .0003, per_corner=2, centre_z=-.0050)
    p.add('camera module', moved(cam, ISLAND[0], ISLAND[1], 0), black)
    for (cx, cy) in ((ISLAND[0] - .0070, ISLAND[1] - .0068), (ISLAND[0] + .0070, ISLAND[1] + .0068)):
        p.add('camera barrel', disc(cx, cy, -.0057, -.0056, .0050, sides=16), steel)
    shield = chamfered_slab(.0220, .0520, .0010, .0010, .0003, per_corner=1, centre_z=-.0049)
    p.add('board shield', moved(shield, -.0210, -.0380, 0), steel)
    # (no speaker, Taptic engine, port assembly or board screws: the battery, the coil, the camera module and the board
    # shield are what say "a phone's inside"; the rest was clutter at 50 cm)
    return p


def screen():
    p = Faceted('Screen')
    p.add('glass', chamfered_slab(.065, .134, .0015, .0080, .0004, per_corner=4, centre_z=.00025), M('HD_Glass'))
    return p


def back_cover():
    """Centred on its origin at Unity y -5.5: the glass from -7.0 to -5.8 (local -1.5 .. -0.3), the lip up to -4.2."""
    p = Faceted('BackCover')
    back, island_m, black, steel, lens, flash = M('HD_PhoneBack'), M('HD_PhoneIsland'), M('HD_PhoneBlack'), M('HD_PhoneSteel'), M('HD_Lens'), M('HD_Flash')
    p.add('glass', chamfered_slab(.068, .138, .0012, .0090, .0004, per_corner=4, centre_z=-.0009), back)
    p.add('frame lip', rrect_wall(-.0004, .0015, .0670, .1370, .0085, .0012, per_corner=4), black)
    bx, by = ISLAND
    plateau = chamfered_slab(.030, .033, .0015, .0070, .0004, per_corner=3, centre_z=-.00225)
    p.add('camera island', moved(plateau, bx, by, 0), island_m)
    for (cx, cy) in ((bx - .0070, by - .0068), (bx + .0070, by + .0068)):
        p.add('lens ring', moved(ring_wall(-.0040, -.0029, .0048, .0070, sides=24), cx, cy, 0), steel)
        p.add('lens', disc(cx, cy, -.0036, -.0029, .0049, sides=16), lens)
    p.add('flash', disc(bx + .0085, by - .0085, -.0034, -.0029, .0025, sides=12), flash)
    p.add('mic', disc(bx - .0090, by + .0090, -.0032, -.0029, .0008, sides=8), black)
    return p


def screw():
    p = Faceted('Screw')
    steel, slot_m = M('HD_Screw'), M('HD_ScrewSlot')
    # a pan head, 8 mm across and 2 mm tall, its dome facing -Z (out of the device); z = 0 is its middle
    head = revolve([(-.0010, .0030), (-.0006, .0038), (.0000, .0040), (.0010, .0040)], sides=16, bottom_tip=(0, 0, -.0011))
    p.add('head', head, steel)
    for rot in (0.0, 90.0):
        slot = fb.zbox(-.0024, .0024, -.00045, .00045, -.0013, -.0007)
        fb.turn(slot, rot, 'Z')
        p.add('slot', slot, slot_m)
    prof = [(.0010, .0012)]
    for i in range(3):
        z = .0016 + i * .0014
        prof += [(z, .0010), (z + .0007, .0013)]
    prof += [(.0062, .0010), (.0066, .0006)]
    p.add('shank', revolve(prof, sides=10, tip=(0, 0, .0070)), steel)
    return p


PIECES = [
    # (builder, the prefab object(s) the mesh goes into)
    (body, ['Body']),
    (screen, ['Broken', 'Fresh']),
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
