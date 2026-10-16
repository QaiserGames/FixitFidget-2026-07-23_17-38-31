"""
The silicone repair mat's two pictures (14 Oct 2026; Art Direction's brief A1 in specs/art-bench-and-cafe.md), drawn with
Pillow, no Blender needed. bench_stage.py maps them (it imports the layout numbers below, never Pillow);
export_bench_stage.py runs this first.

  BM_Grid_color.png   512 x 512, one 50 mm tile of the mat's printed grid (10.24 px/mm): the silicone's slate blue
                      #4F70A0, minor lines #6684B0 0.5 mm wide every 10 mm, a major line #7A95BC 0.9 mm wide on the
                      tile's edge (so every 50 mm, counted from the rulers' zero). It tiles: the material repeats it 20
                      times per metre of UV (the mat's top is mapped in metres from the rulers' zero, the house rule for
                      long pieces). Named *_color so Kit 1's texture settings (CafeKitSteps.ConfigureTexture) give it
                      Repeat and mipmaps.
  BM_Rulers.png       4096 x 256 at 7 px/mm, the two printed ruler strips, cream #EDE4D3 on the slate blue, clamped:
                      row A the near ruler (0 at x -285 mm of the mat, 57 cm to x +285), row B the left ruler (0 at
                      y -166 mm, 33 cm). Ticks every 5 mm (2.5 mm long) and 10 mm (4.5 mm long) rising from the mat's top
                      edge, the centimetre numbers 3 mm tall (Inter SemiBold, OFL), a 0.5 mm rule along each strip's
                      inner edge.

Run:  python3 bench_stage_textures.py [<texture folder>]
Default: Assets/Art/Textures/Bench/ under the repository this folder sits in (Tools/Blender), else ./textures/.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
DEFAULT_OUT = (os.path.join(HERE, '..', '..', 'Assets', 'Art', 'Textures', 'Bench') if IN_REPO
               else os.path.join(HERE, 'textures'))
FONT = os.path.join(HERE, 'fonts', 'Inter-SemiBold.otf')

SILICONE = '#4F70A0'
MINOR = '#6684B0'
MAJOR = '#7A95BC'
PRINT = '#EDE4D3'

GRID_PX = 512            # one tile
GRID_MM = 50.0           # what the tile covers

# ---- the ruler layout, shared with bench_stage.py (mm in the mat frame: +x the player's right, +y away) ----
RULER_W, RULER_H = 4096, 256
PPM = 7.0                # px per mm
MARGIN = 30.0            # px left of each ruler's 0
NEAR_ZERO_X = -285.0     # the near ruler's 0 (it reads 0-57 cm, -285..+285)
NEAR_EDGE_Y = -178.5     # the near strip's outer edge: the mat's top edge (inside the 1.5 mm chamfer)
NEAR_INNER_Y = -166.0
NEAR_LEN_MM = 570
LEFT_ZERO_Y = -166.0     # the left ruler's 0 (it reads 0-33 cm, -166..+164)
LEFT_EDGE_X = -298.5
LEFT_INNER_X = -286.0
LEFT_LEN_MM = 330
ROW_A_INNER = 15.0       # pixel row of the near strip's inner edge; its outer edge is 12.5 mm lower (row 102.5)
ROW_B_EDGE = 143.0       # pixel row of the left strip's outer edge; its inner edge is 12.5 mm lower (row 230.5)


def near_uv(x, y):
    u = (MARGIN + (x - NEAR_ZERO_X) * PPM) / RULER_W
    row = ROW_A_INNER + (NEAR_INNER_Y - y) * PPM
    return (u, 1.0 - row / RULER_H)


def left_uv(x, y):
    u = (MARGIN + (y - LEFT_ZERO_Y) * PPM) / RULER_W
    row = ROW_B_EDGE + (x - LEFT_EDGE_X) * PPM
    return (u, 1.0 - row / RULER_H)


def hexrgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def grid(path):
    """One tile, drawn at 4x and reduced (the lines land between pixels: 10.24 px/mm)."""
    from PIL import Image, ImageDraw
    s = 4
    n = GRID_PX * s
    ppm = n / GRID_MM
    im = Image.new('RGB', (n, n), hexrgb(SILICONE))
    d = ImageDraw.Draw(im)

    def line(at_mm, width_mm, colour, vertical):
        a = at_mm * ppm
        hw = width_mm * ppm / 2
        for off in (0.0, n, -n):                       # wrap a line that straddles the tile's edge
            lo, hi = a - hw + off, a + hw + off
            if hi < 0 or lo > n:
                continue
            if vertical:
                d.rectangle([lo, 0, hi - 1, n], fill=hexrgb(colour))
            else:
                d.rectangle([0, lo, n, hi - 1], fill=hexrgb(colour))

    for k in range(1, 5):                              # minor lines at 10, 20, 30, 40 mm
        line(k * 10.0, 0.5, MINOR, True)
        line(k * 10.0, 0.5, MINOR, False)
    line(0.0, 0.9, MAJOR, True)                        # the major line on the tile's edge (0 = 50 mm)
    line(0.0, 0.9, MAJOR, False)
    im = im.resize((GRID_PX, GRID_PX), Image.LANCZOS)
    im.save(path)
    return path


def rulers(path):
    """The two ruler strips in one atlas (bench_stage.py maps them with near_uv and left_uv)."""
    from PIL import Image, ImageDraw, ImageFont
    s = 2
    im = Image.new('RGB', (RULER_W * s, RULER_H * s), hexrgb(SILICONE))
    d = ImageDraw.Draw(im)
    font = ImageFont.truetype(FONT, int(round(3.0 / 0.727 * PPM * s)))   # Inter's digits are 0.727 em tall
    cream = hexrgb(PRINT)

    def px(mm):
        return (MARGIN + mm * PPM) * s

    # row A, the near ruler: the mat's top edge is the strip's bottom (row 102.5), its inner edge the top (row 15). The
    # ticks rise from the edge; the numbers stand on the long ticks, upright for the player.
    edge = (ROW_A_INNER + (NEAR_INNER_Y - NEAR_EDGE_Y) * PPM) * s
    inner = ROW_A_INNER * s
    for mm in range(0, NEAR_LEN_MM + 1, 5):
        x = px(mm)
        long_ = mm % 10 == 0
        length = (4.5 if long_ else 2.5) * PPM * s
        w = (0.45 if long_ else 0.35) * PPM * s
        d.rectangle([x - w / 2, edge - length, x + w / 2, edge + 2 * s], fill=cream)
        if long_:
            label = str(mm // 10)
            tw = d.textlength(label, font=font)
            d.text((x - tw / 2, edge - length - 1.0 * PPM * s), label, font=font, fill=cream, anchor='ls')
    d.rectangle([px(-2), inner, px(NEAR_LEN_MM + 2), inner + 0.5 * PPM * s], fill=cream)   # the strip's inner rule

    # row B, the left ruler, along the mat's depth: u runs with y (away from the player), the mat's top edge is the row's
    # top (row 143), the inner edge 12.5 mm lower. On the mat the atlas lies turned a quarter (its right is "away", its up
    # is "toward the mat's left edge"), so the ticks hang from the row's top and the numbers sit under them, upright in the
    # atlas: on the mat their tops face the edge and they read from the near end, head tilted left.
    edge = ROW_B_EDGE * s
    inner = (ROW_B_EDGE + (LEFT_INNER_X - LEFT_EDGE_X) * PPM) * s
    for mm in range(0, LEFT_LEN_MM + 1, 5):
        x = px(mm)
        long_ = mm % 10 == 0
        length = (4.5 if long_ else 2.5) * PPM * s
        w = (0.45 if long_ else 0.35) * PPM * s
        d.rectangle([x - w / 2, edge - 2 * s, x + w / 2, edge + length], fill=cream)
        if long_:
            label = str(mm // 10)
            tw = d.textlength(label, font=font)
            d.text((x - tw / 2, edge + length + 1.0 * PPM * s), label, font=font, fill=cream, anchor='lt')
    d.rectangle([px(-2), inner - 0.5 * PPM * s, px(LEFT_LEN_MM + 4), inner], fill=cream)
    im = im.resize((RULER_W, RULER_H), Image.LANCZOS)
    im.save(path)
    return path


def main(out):
    os.makedirs(out, exist_ok=True)
    a = grid(os.path.join(out, 'BM_Grid_color.png'))
    b = rulers(os.path.join(out, 'BM_Rulers.png'))
    print('textures:', a, b)


if __name__ == '__main__':
    main(os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT))
