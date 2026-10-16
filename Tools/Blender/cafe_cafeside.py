"""
The café full from above, part 2: the café side of the back bar (14 Oct 2026). Art Direction's brief B3 (C1-C3,
specs/art-bench-and-cafe.md) and his list: "coffee cups, to go cups, coffee syrup". Built the house way on
cafe_repairside.py's helpers (the same frame: placed with rotation (0, 180, 0), Blender X/Y read as world x/z, fronts
toward -Y face the room).

  LI_SyrupRack    C1  a black wire rack, six syrup bottles (0.075 x 0.30) with white and black pumps (+0.12): caramel,
                      vanilla, hazelnut, chocolate, raspberry, sugar-free. Cream labels, no words yet (a fictional maker's
                      labels wait for Content). Replaces the three Synty bottles at x 1.74-2.06 (switched off, recorded).
  LI_CupStation   C2  a black three-tube to-go cup dispenser (the café's ivory cups, three sizes), a black three-bay lid
                      organiser (black sip lids, white flat lids, black domes), a stack of kraft sleeves banded in Ace's
                      green, a steel milk pitcher
  LI_BarMat       C3  a black ribbed rubber bar mat 0.60 x 0.30 with eight cream mugs upside down, two stacks of saucers and
                      (wave 2, 14 Oct: the feel targets' call 9, the bar mat read 9 px from above) a small black mug tree,
                      0.25 m tall, four arms at two heights, four mugs in Ace's green hanging from it

Cut: brand marks on the bottles and the dispenser (Content's and fictional, later), the pumps' dosing collars, the
dispenser's wall bracket (it stands on the back bar), a cup in every tube's mouth beyond the bottom one.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import cafe_repairside as rs  # noqa: E402

M, Piece, rb, cyl = rs.M, rs.Piece, rs.rb, rs.cyl

SYRUPS = [('LI_SyrupCaramel', '#B4782E'), ('LI_SyrupVanilla', '#E3CF9A'), ('LI_SyrupHazelnut', '#7A4A28'),
          ('LI_SyrupChocolate', '#3E2A1E'), ('LI_SyrupRaspberry', '#8E2A3A'), ('LI_SyrupSugarFree', '#D9D3C4')]
for _name, _hex in SYRUPS:
    rs.NEW[_name] = (_hex, 0.45, None, "a syrup bottle (glass and syrup seen as one colour): " + _name[8:].lower())
rs.NEW['LI_Rubber'] = ('#232120', 0.22, None, "the bar mat's black ribbed rubber (the till mat's smoothness)")
rs.NEW['LI_MugGreen'] = ('#2E7D5B', 0.60, None, "glazed mugs in Ace's green on the mug tree (the style guide: the green lives on cups)")


def syrup_rack():
    p = Piece('LI_SyrupRack', floor=True)
    black = M('CF_Black')
    w, d = 0.50, 0.10
    # the wire rack: a base frame (one ring), four posts sunk into it, a front rail at 9 cm and a back rail at 20 cm between them
    p.add('rack base', hl.rrect_wall(0.0, 0.006, w, d, 0.004, 0.006, per_corner=2), black)
    for px_, py_, top in ((-w / 2 + 0.0035, -d / 2 + 0.0035, 0.091), (w / 2 - 0.0035, -d / 2 + 0.0035, 0.091),
                          (-w / 2 + 0.0035, d / 2 - 0.0035, 0.201), (w / 2 - 0.0035, d / 2 - 0.0035, 0.201)):
        p.add('rack post', rb(px_ - 0.003, px_ + 0.003, py_ - 0.003, py_ + 0.003, 0.003, top, c=0.0008), black)
    p.add('front rail', rb(-w / 2 + 0.0055, w / 2 - 0.0055, -d / 2 + 0.0005, -d / 2 + 0.0055, 0.084, 0.090, c=0.0008), black)
    p.add('back rail', rb(-w / 2 + 0.0055, w / 2 - 0.0055, d / 2 - 0.0055, d / 2 - 0.0005, 0.194, 0.200, c=0.0008), black)
    for k, (name, _hex) in enumerate(SYRUPS):
        x = -w / 2 + 0.045 + k * 0.082
        bottle = hl.revolve([(0.004, 0.0355), (0.006, 0.0375), (0.215, 0.0375), (0.245, 0.030), (0.262, 0.013), (0.300, 0.013)], sides=10)
        p.add('bottle', hl.moved(bottle, x, 0.0, 0.0), M(name))
        p.add('label', cyl(x, 0.0, 0.07, 0.17, 0.0381, sides=10), M('LI_PaperWhite'))
        pump = M('DC_Chalk') if k % 2 == 0 else black
        p.add('pump collar', cyl(x, 0.0, 0.296, 0.318, 0.019, sides=8), pump)
        p.add('pump stem', cyl(x, 0.0, 0.318, 0.380, 0.005, sides=8), M('DC_Steel'))
        head = rb(-0.012, 0.050, -0.010, 0.010, 0.380, 0.405, c=0.004)
        fb.turn(head, -90, 'Z')                       # the nozzles point at the room (-Y)
        p.add('pump head', hl.moved(head, x, 0.0, 0.0), pump)
    return p


def cup_station():
    p = Piece('LI_CupStation', floor=True)
    black, cup, kraft, steel = M('CF_Black'), M('LI_CupPaper'), M('CF_Kraft'), M('DC_Steel')
    # the three-tube dispenser, standing: a base, a back panel, three open tubes with the cups' stacks in them
    p.add('dispenser base', rb(-0.35, -0.01, -0.10, 0.12, 0.0, 0.012, c=0.003), black)
    p.add('dispenser back', rb(-0.348, -0.012, 0.10, 0.118, 0.006, 0.52, c=0.003), black)
    for k, r in enumerate((0.042, 0.047, 0.052)):
        x = -0.29 + k * 0.115
        p.add('tube', hl.moved(hl.ring_wall(0.06, 0.50, r, r + 0.003, sides=16), x, 0.045, 0.0), black)
        p.add('cup stack', hl.moved(hl.revolve([(0.012, r * 0.72), (0.10, r - 0.001), (0.47, r - 0.001)], sides=16), x, 0.045, 0.0), cup)
        p.add('tube strut', rb(x - 0.006, x + 0.006, 0.045 + r, 0.105, 0.30, 0.32, c=0.001), black)
    # the lid organiser: a black three-bay box with its lids stacked in it
    for k, (lid, sx) in enumerate((('CF_Black', 0.04), ('DC_Chalk', 0.13), ('CF_Black', 0.22))):
        p.add('lid bay', rb(sx - 0.042, sx + 0.042, -0.08, 0.06, 0.0, 0.10, c=0.003), black)
        p.add('lids', cyl(sx, -0.01, 0.06, 0.112, 0.040, sides=14), M(lid))
    # the sleeves (kraft, banded in Ace's green) and the steel milk pitcher
    p.add('sleeves', rb(0.27, 0.34, -0.10, 0.02, 0.0, 0.05, c=0.004), kraft)
    p.add('sleeve band', rb(0.268, 0.342, -0.102, 0.022, 0.018, 0.032, c=0.001), M('DC_Sticker_Green'))
    jug = hl.revolve([(0.0, 0.036), (0.004, 0.040), (0.08, 0.042), (0.13, 0.034), (0.14, 0.036)], sides=14, cap_top=False)
    p.add('milk pitcher', hl.moved(jug, 0.30, 0.08, 0.0), steel)
    p.add('pitcher handle', rs.handle_arc(0.30, 0.118, 0.03, 0.115, 0.022, axis='y', sign=1, r=0.005), steel)
    return p


def hanging_mug(p, arm_end, phi_deg, mat):
    """A mug hung by its handle on a mug tree's arm: the arm runs through the handle; the mug leans 25 degrees off the
    vertical (its weight would take it further, the handle's grip on the arm holds it), its body to the arm's clockwise
    side, so four mugs a quarter turn apart never meet. arm_end: the arm's end (piece frame, m); phi: the arm's azimuth."""
    from mathutils import Matrix, Vector
    body = hl.revolve([(0.0, 0.031), (0.004, 0.033), (0.070, 0.034), (0.070, 0.0305), (0.007, 0.029)], sides=12)
    handle = rs.handle_arc(0.033, 0.0, 0.018, 0.058, 0.016, axis='x', sign=1, r=0.0045, n=6)
    hook = Vector((0.0405, 0.0, 0.0495))                     # inside the handle's loop, near its top: where the arm sits
    rot = Matrix.Rotation(math.radians(phi_deg - 90.0), 4, 'Z') @ Matrix.Rotation(math.radians(-25.0), 4, 'Y')
    at = Vector(arm_end) + Vector((0.0, 0.0, 0.0035)) - (rot @ hook)
    for part, bm in (('mug', body), ('mug handle', handle)):
        bm.transform(Matrix.Translation(at) @ rot)
        p.add(part, bm, mat)


def mug_tree(p, x, y, z0):
    """A small mug tree (0.25 m over the mat): a black base and post with a ball top, four arms 80 mm long rising 30 degrees,
    two at 150 mm and two at 205 mm a quarter turn apart, a ball on each end, a green mug on each."""
    from mathutils import Matrix, Vector
    black, green = M('CF_Black'), M('LI_MugGreen')
    p.add('tree base', hl.moved(hl.revolve([(z0, 0.050), (z0 + 0.008, 0.055), (z0 + 0.016, 0.052), (z0 + 0.018, 0.030)], sides=16), x, y, 0.0), black)
    top = z0 + 0.243
    p.add('tree post', hl.moved(hl.revolve([(z0 + 0.016, 0.008), (top, 0.0068)], sides=10), x, y, 0.0), black)
    p.add('tree finial', hl.moved(hl.revolve([(top - 0.002, 0.0068)] + hl.dome(top, 0.0072, 0.010, n=4), sides=10, tip=(0, 0, top + 0.010)), x, y, 0.0), black)
    L = 0.080
    for phi, h in ((45.0, 0.205), (225.0, 0.205), (135.0, 0.150), (315.0, 0.150)):
        arm = hl.revolve([(0.0, 0.0042), (L, 0.0038)], sides=8)
        ball = hl.moved(hl.revolve([(L - 0.004, 0.0040)] + hl.dome(L, 0.0058, 0.006, n=3), sides=8, tip=(0, 0, L + 0.006)), 0, 0, 0)
        fb.merge(arm, ball)
        m = Matrix.Translation((x, y, z0 + h)) @ Matrix.Rotation(math.radians(phi), 4, 'Z') @ Matrix.Rotation(math.radians(60.0), 4, 'Y')
        arm.transform(m)
        p.add('tree arm', arm, black)
        end = m @ Vector((0.0, 0.0, L - 0.010))
        hanging_mug(p, end, phi, green)


def bar_mat():
    p = Piece('LI_BarMat', floor=True)
    rubber, cer = M('LI_Rubber'), M('DC_Ceramic')
    w, d = 0.60, 0.30
    p.add('bar mat', rb(-w / 2, w / 2, -d / 2, d / 2, 0.0, 0.008, c=0.002), rubber)
    for k in range(9):                                # the ribs, along x
        y = -d / 2 + 0.03 + k * 0.03
        p.add('rib', rb(-w / 2 + 0.012, w / 2 - 0.012, y - 0.004, y + 0.004, 0.007, 0.012, c=0.001), rubber)
    # eight mugs upside down, two rows of four, handles toward the room (wave 2: was ten; the mug tree takes the east end)
    for row in range(2):
        for k in range(4):
            x = -0.235 + k * 0.095
            y = -0.06 + row * 0.115
            mug = hl.revolve([(0.0, 0.042), (0.005, 0.042), (0.083, 0.040), (0.090, 0.034)], sides=10)
            p.add('mug', hl.moved(mug, x, y, 0.012), cer)
            p.add('mug handle', rs.handle_arc(x, y - 0.039, 0.025, 0.075, 0.018, axis='y', sign=-1, r=0.0055, n=5), cer)
    # the mug tree on the mat's east end, standing on the ribs (its base sunk 0.5 mm into them)
    mug_tree(p, 0.205, 0.0, 0.0115)
    # two stacks of saucers on the bar beside the mat
    for k, sx in enumerate((0.38, 0.38)):
        sy = -0.06 + k * 0.13
        for j in range(3):
            z = j * 0.016
            saucer = hl.revolve([(z + 0.0004 * (j > 0), 0.045), (z + 0.004, 0.062), (z + 0.014, 0.072), (z + 0.016, 0.070), (z + 0.008, 0.050)], sides=12)
            p.add('saucer', hl.moved(saucer, sx, sy, 0.0), cer)
    return p


PIECES = [
    (syrup_rack, 'Café side', 'C1: syrup rack, six bottles with white and black pumps (caramel, vanilla, hazelnut, chocolate, raspberry, sugar-free)',
     {'position': [1.90, 1.00, 17.85], 'rotation': [0, 180, 0], 'on': "CK_BackBar's top at the dispenser bay's west end against the "
      "tile, x 1.65-2.15; switch off the three Synty 'Drinks - Bottle_01..03' there (x 1.74-2.06), recorded"}, 'bottom centre'),
    (cup_station, 'Café side', 'C2: three-tube to-go cup dispenser, three-bay lid organiser, green-banded kraft sleeves, steel milk pitcher',
     {'position': [0.70, 1.00, 17.80], 'rotation': [0, 180, 0], 'on': "CK_BackBar's top behind the till, against the tile, x 0.35-1.05"},
     'bottom centre'),
    (bar_mat, 'Café side', 'C3: black ribbed bar mat 0.60 x 0.30, eight cream mugs upside down, a 0.25 m mug tree with four green mugs, two stacks of saucers',
     {'position': [-0.50, 1.00, 17.75], 'rotation': [0, 180, 0], 'on': "CK_BackBar's top under CK_MenuBoard, x -0.80..-0.20 "
      "(the saucers stand just east of the mat, to x -0.04)"}, 'bottom centre'),
]
