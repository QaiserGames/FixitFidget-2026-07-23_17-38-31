#!/usr/bin/env python3
"""
Button icons for the HUD (19 Oct 2026, playtest 7).

WHY. His girlfriend is dyslexic and plays on a PlayStation pad: "Instead of writing out square, just put the icon."
"I like how you gotta see what button you press, but I can't read fast enough. So icons would look better." Every button
name the game shows is a TextMesh Pro sprite now (PadInput.Label / ControlHints give `<sprite name="ps_cross">`), drawn from
the sheet this script makes. The six drink icons at the end are for Grace's latte card ("Oh, it needs to be bigger, huh?
With images?").

WHAT IT WRITES (run from the repo root, or from anywhere: paths are worked out from this file):
  Assets/Data/Resources/UI/ButtonIcons/button_icons.png    a 2048 x 2048 transparent PNG, the icons packed in rows
  Assets/Data/Resources/UI/ButtonIcons/button_icons.json   where each icon is: name -> x, y, w, h (pixels, origin top left),
                                                           and the PNG's size. Fixit Fidget › UI › Button icons (ButtonIconSteps)
                                                           turns the pair into the TMP sprite asset.

THE LOOK. The game's cozy, chunky, flat style (UiSkin's palette: Ink #1f1b17, Paper #f7f3ee, Gold #f2a61f).
  Face buttons   128 px discs: an ink disc with a 6 px paper rim and a 6 px soft bottom shade. PlayStation's shapes in their
                 own colours (cross pale blue, circle soft red, square pink, triangle teal-green); Xbox's letters A green,
                 B red, X blue, Y yellow; Nintendo's letters in paper.
  Pills          176 x 112 rounded pills: L1 R1 L2 R2, LB RB LT RT, L R ZL ZR; Options, Share, Create, Touchpad, Menu, View,
                 Plus, Minus.
  D-pad          an ink plus, the pressed arm in paper: up, down, left, right, up/down, left/right.
  Sticks         L and R, each also pressed.
  Keycaps        128 px paper rounded squares, an ink letter, a darker bottom edge: A-Z, 0-9, Esc, Tab, Space (wide), Ctrl,
                 Shift, Enter, the four arrows.
  Mouse          a simple body; the left, right or middle button, or the wheel, in Gold; "drag" with motion lines.
  Drinks         a cup each in its own colour and mark, readable at 32 px.
Letters are set in Baloo 2 Bold (Assets/Fonts/Baloo2, the UI font) when it is there, else Inter Bold (Tools/Blender/fonts).

Every icon is drawn at 4x and shrunk (Lanczos) so the edges are smooth at any size. Requires Pillow.
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "Assets", "Data", "Resources", "UI", "ButtonIcons")
PNG_NAME, JSON_NAME = "button_icons.png", "button_icons.json"

SHEET = 2048
SS = 4                     # supersampling
GUTTER = 16                # pixels between icons on the sheet (the sprite rects are tight; this keeps bilinear bleed away)

# UiSkin's palette, and the symbols' own colours.
INK = (0x1f, 0x1b, 0x17, 255)
INK_SHADE = (0x3a, 0x33, 0x2d, 255)        # UiSkin.BandLine: the disc's soft lip
PAPER = (0xf7, 0xf3, 0xee, 255)
PAPER_EDGE = (0xd6, 0xcf, 0xc5, 255)       # a keycap's darker bottom edge
GOLD = (0xf2, 0xa6, 0x1f, 255)
PS_CROSS = (0x8a, 0xbb, 0xf0, 255)         # pale blue
PS_CIRCLE = (0xf0, 0x7a, 0x6a, 255)        # soft red
PS_SQUARE = (0xf2, 0x9c, 0xd0, 255)        # pink
PS_TRIANGLE = (0x5f, 0xd6, 0xb0, 255)      # teal-green
XB_A = (0x7b, 0xc9, 0x5a, 255)
XB_B = (0xe8, 0x5b, 0x4a, 255)
XB_X = (0x5a, 0xa0, 0xee, 255)
XB_Y = (0xf4, 0xc8, 0x2e, 255)

FONT_CANDIDATES = [
    os.path.join(REPO, "Assets", "Fonts", "Baloo2", "Baloo2-Bold.ttf"),
    os.path.join(REPO, "Tools", "Blender", "fonts", "Inter-Bold.otf"),
    "/usr/share/fonts/opentype/inter/Inter-Bold.otf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]


def find_font():
    for path in FONT_CANDIDATES:
        if os.path.isfile(path):
            return path
    sys.exit("No font found. Put Baloo2-Bold.ttf in Assets/Fonts/Baloo2 or Inter-Bold.otf in Tools/Blender/fonts.")


FONT_PATH = find_font()
_fonts = {}


def font(size):
    size = int(round(size))
    if size not in _fonts:
        _fonts[size] = ImageFont.truetype(FONT_PATH, size)
    return _fonts[size]


# ---------------------------------------------------------------- drawing helpers (all in supersampled pixels)

def canvas(w, h):
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def s(v):
    return v * SS


def text_centred(draw, img, label, size, colour, cx, cy, max_width=None):
    """A label centred on (cx, cy) in supersampled pixels, shrunk to fit max_width when given."""
    f = font(size * SS)
    if max_width is not None:
        while f.getlength(label) > max_width * SS and size > 8:
            size -= 1
            f = font(size * SS)
    # Centre on the glyphs' own box (not the font's line box), so a short word sits dead centre.
    left, top, right, bottom = draw.textbbox((0, 0), label, font=f, anchor="ls")
    width, height = right - left, bottom - top
    draw.text((cx - left - width / 2, cy - top - height / 2), label, font=f, fill=colour, anchor="ls")


def soft_lip(img, mask_fn, w, h, lip=6, colour=INK_SHADE):
    """A soft lighter band along the bottom inside a shape: the shape's mask minus the same mask lifted by `lip`, blurred."""
    full = Image.new("L", img.size, 0)
    mask_fn(ImageDraw.Draw(full), 0)
    lifted = Image.new("L", img.size, 0)
    mask_fn(ImageDraw.Draw(lifted), -s(lip))
    band = Image.new("L", img.size, 0)
    band.paste(full, (0, 0))
    from PIL import ImageChops
    band = ImageChops.subtract(full, lifted)
    band = band.filter(ImageFilter.GaussianBlur(s(2)))
    layer = Image.new("RGBA", img.size, colour)
    img.paste(layer, (0, 0), band)


# ---------------------------------------------------------------- the icons

def disc(symbol_fn):
    """A 128 px face button: paper rim, ink disc, soft bottom lip, then the symbol."""
    w = h = 128
    img, draw = canvas(w, h)
    cx = cy = s(64)
    draw.ellipse([s(0), s(0), s(128), s(128)], fill=PAPER)
    draw.ellipse([s(6), s(6), s(122), s(122)], fill=INK)
    soft_lip(img, lambda d, dy: d.ellipse([s(6), s(6) + dy, s(122), s(122) + dy], fill=255), w, h)
    symbol_fn(ImageDraw.Draw(img), img, cx, cy)
    return finish(img, w, h), w, h


PS_STROKE = 12   # the symbols' stroke, sheet pixels: 3 px at the 32 px a line shows them at


def ps_cross(draw, img, cx, cy):
    import math
    arm, thick = s(27), s(PS_STROKE)
    for a in (45, -45):
        dx, dy = math.cos(math.radians(a)) * arm, math.sin(math.radians(a)) * arm
        draw.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=PS_CROSS, width=thick)
        for px, py in ((cx - dx, cy - dy), (cx + dx, cy + dy)):
            draw.ellipse([px - thick / 2, py - thick / 2, px + thick / 2, py + thick / 2], fill=PS_CROSS)


def ps_circle(draw, img, cx, cy):
    r, thick = s(29), s(PS_STROKE)
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=PS_CIRCLE, width=thick)


def ps_square(draw, img, cx, cy):
    r, thick = s(26), s(PS_STROKE)
    draw.rounded_rectangle([cx - r, cy - r, cx + r, cy + r], radius=s(5), outline=PS_SQUARE, width=thick)


def ps_triangle(draw, img, cx, cy):
    # An outlined triangle with rounded corners: the stroke runs along a path at radius Rc (its outer edge then sits at
    # Rc + t, its inner at Rc - t: offsetting an equilateral triangle by d moves its circumradius by 2d). Its box is
    # centred on the disc (the circumcentre sits a quarter radius above the box's middle).
    import math
    rc, thick = s(27), s(PS_STROKE)
    cy = cy + rc / 4
    pts = [(cx + rc * math.cos(math.radians(a)), cy + rc * math.sin(math.radians(a))) for a in (-90, 30, 150)]
    draw.line([pts[0], pts[1], pts[2], pts[0], pts[1]], fill=PS_TRIANGLE, width=thick, joint="curve")


def letter(label, colour, size=66):
    def fn(draw, img, cx, cy):
        text_centred(draw, img, label, size, colour, cx, cy + s(1))
    return fn


def pill(label=None, symbol_fn=None, size=48):
    """A 176 x 112 rounded pill: paper rim, ink face, soft lip, a label or a symbol."""
    w, h = 176, 112
    img, draw = canvas(w, h)
    draw.rounded_rectangle([s(0), s(0), s(176), s(112)], radius=s(44), fill=PAPER)
    draw.rounded_rectangle([s(6), s(6), s(170), s(106)], radius=s(38), fill=INK)
    soft_lip(img, lambda d, dy: d.rounded_rectangle([s(6), s(6) + dy, s(170), s(106) + dy], radius=s(38), fill=255), w, h)
    draw = ImageDraw.Draw(img)
    if label is not None:
        text_centred(draw, img, label, size, PAPER, s(88), s(56) + s(1), max_width=140)
    if symbol_fn is not None:
        symbol_fn(draw, img, s(88), s(56))
    return finish(img, w, h), w, h


def menu_lines(draw, img, cx, cy):
    for dy in (-s(16), 0, s(16)):
        draw.rounded_rectangle([cx - s(24), cy + dy - s(4), cx + s(24), cy + dy + s(4)], radius=s(4), fill=PAPER)


def view_squares(draw, img, cx, cy):
    a = s(16)
    draw.rounded_rectangle([cx - a - s(8), cy - a - s(8), cx + a - s(8), cy + a - s(8)], radius=s(3), outline=PAPER, width=s(6))
    draw.rounded_rectangle([cx - a + s(8), cy - a + s(8), cx + a + s(8), cy + a + s(8)], radius=s(3), fill=INK, outline=PAPER, width=s(6))


def share_glyph(draw, img, cx, cy):
    # A box with an arrow leaving its top: the DualShock's SHARE mark.
    draw.rounded_rectangle([cx - s(20), cy - s(6), cx + s(20), cy + s(26)], radius=s(4), outline=PAPER, width=s(6))
    draw.rectangle([cx - s(10), cy - s(10), cx + s(10), cy + s(2)], fill=INK)
    draw.line([(cx, cy + s(10)), (cx, cy - s(26))], fill=PAPER, width=s(6))
    draw.polygon([(cx - s(12), cy - s(16)), (cx, cy - s(30)), (cx + s(12), cy - s(16))], fill=PAPER)


def touchpad_glyph(draw, img, cx, cy):
    draw.rounded_rectangle([cx - s(40), cy - s(24), cx + s(40), cy + s(24)], radius=s(10), outline=PAPER, width=s(6))
    draw.line([(cx, cy - s(18)), (cx, cy + s(18))], fill=PAPER, width=s(4))


def plus_glyph(draw, img, cx, cy):
    draw.rounded_rectangle([cx - s(26), cy - s(5), cx + s(26), cy + s(5)], radius=s(5), fill=PAPER)
    draw.rounded_rectangle([cx - s(5), cy - s(26), cx + s(5), cy + s(26)], radius=s(5), fill=PAPER)


def minus_glyph(draw, img, cx, cy):
    draw.rounded_rectangle([cx - s(26), cy - s(5), cx + s(26), cy + s(5)], radius=s(5), fill=PAPER)


def dpad(pressed):
    """An ink plus on a paper rim; the pressed arm(s) in paper ("up", "down", "left", "right", "updown", "leftright")."""
    w = h = 128
    img, draw = canvas(w, h)
    cx = cy = s(64)
    arm, half, r = s(58), s(19), s(10)
    # The rim, then the plus.
    for colour, grow in ((PAPER, s(6)), (INK, 0)):
        draw.rounded_rectangle([cx - half - grow, cy - arm - grow, cx + half + grow, cy + arm + grow], radius=r, fill=colour)
        draw.rounded_rectangle([cx - arm - grow, cy - half - grow, cx + arm + grow, cy + half + grow], radius=r, fill=colour)
    arms = {
        "up": [(cx - half + s(5), cy - arm + s(5), cx + half - s(5), cy - half - s(2))],
        "down": [(cx - half + s(5), cy + half + s(2), cx + half - s(5), cy + arm - s(5))],
        "left": [(cx - arm + s(5), cy - half + s(5), cx - half - s(2), cy + half - s(5))],
        "right": [(cx + half + s(2), cy - half + s(5), cx + arm - s(5), cy + half - s(5))],
    }
    arms["updown"] = arms["up"] + arms["down"]
    arms["leftright"] = arms["left"] + arms["right"]
    # The pressed arm is paper, whole: at the 32 px a line shows it, the arm is 5 px wide, and anything drawn inside it
    # would be noise. The paper arm against the ink plus is the direction.
    for box in arms[pressed]:
        draw.rounded_rectangle(list(box), radius=s(6), fill=PAPER)
    return finish(img, w, h), w, h


def stick(label, pressed):
    """A thumbstick seen from above: a paper ring, an ink cap with its letter; pressed: an inner gold ring."""
    w = h = 128
    img, draw = canvas(w, h)
    cx = cy = s(64)
    draw.ellipse([s(0), s(0), s(128), s(128)], fill=PAPER)
    draw.ellipse([s(6), s(6), s(122), s(122)], fill=INK)
    draw.ellipse([s(16), s(16), s(112), s(112)], fill=INK_SHADE)
    draw.ellipse([s(22), s(22), s(106), s(106)], fill=INK)
    if pressed:
        draw.ellipse([s(24), s(24), s(104), s(104)], outline=GOLD, width=s(7))
    soft_lip(img, lambda d, dy: d.ellipse([s(22), s(22) + dy, s(106), s(106) + dy], fill=255), w, h)
    draw = ImageDraw.Draw(img)
    text_centred(draw, img, label, 54 if not pressed else 46, GOLD if pressed else PAPER, cx, cy + s(1))
    return finish(img, w, h), w, h


def keycap(label, wide=False, size=66, arrow=None):
    """A paper keycap with an ink letter, its bottom edge darker (the key's side)."""
    w, h = (256 if wide else 128), 128
    img, draw = canvas(w, h)
    draw.rounded_rectangle([s(0), s(0), s(w), s(h)], radius=s(22), fill=PAPER_EDGE)
    draw.rounded_rectangle([s(0), s(0), s(w), s(h) - s(8)], radius=s(22), fill=PAPER)
    draw.rounded_rectangle([s(0), s(0), s(w), s(h)], radius=s(22), outline=INK, width=s(4))
    cx, cy = s(w / 2), s(60)
    if arrow is not None:
        import math
        a = {"up": -90, "down": 90, "left": 180, "right": 0}[arrow]
        shaft = s(26)
        ex, ey = cx + shaft * math.cos(math.radians(a)), cy + shaft * math.sin(math.radians(a))
        sx, sy = cx - shaft * math.cos(math.radians(a)), cy - shaft * math.sin(math.radians(a))
        draw.line([(sx, sy), (ex, ey)], fill=INK, width=s(11))
        pts = []
        for da, rr in ((0, s(22)), (135, s(20)), (225, s(20))):
            pts.append((ex + rr * math.cos(math.radians(a + da)) - s(6) * math.cos(math.radians(a)),
                        ey + rr * math.sin(math.radians(a + da)) - s(6) * math.sin(math.radians(a))))
        draw.polygon(pts, fill=INK)
    else:
        text_centred(draw, img, label, size, INK, cx, cy, max_width=(w - 24))
    return finish(img, w, h), w, h


def mouse(part):
    """A mouse from above: paper body with an ink outline; the named part in Gold. "drag" adds motion lines."""
    w, h = 112, 128
    img, draw = canvas(w, h)
    bx0, by0, bx1, by1 = s(14), s(6), s(98), s(122)
    body = [bx0, by0, bx1, by1]
    draw.rounded_rectangle(body, radius=s(40), fill=PAPER, outline=INK, width=s(5))
    midx, split = s(56), s(54)
    # The two buttons: the top half, split down the middle.
    draw.line([(bx0, split), (bx1, split)], fill=INK, width=s(5))
    draw.line([(midx, by0), (midx, split)], fill=INK, width=s(5))
    if part in ("left", "right", "drag"):
        mask = Image.new("L", img.size, 0)
        md = ImageDraw.Draw(mask)
        md.rounded_rectangle(body, radius=s(40), fill=255)
        md.rectangle([bx0, split, bx1, by1], fill=0)
        if part == "right":
            md.rectangle([bx0, by0, midx, split], fill=0)
        else:
            md.rectangle([midx, by0, bx1, split], fill=0)
        gold = Image.new("RGBA", img.size, GOLD)
        img.paste(gold, (0, 0), mask)
        draw = ImageDraw.Draw(img)
        draw.rounded_rectangle(body, radius=s(40), outline=INK, width=s(5))
        draw.line([(bx0, split), (bx1, split)], fill=INK, width=s(5))
        draw.line([(midx, by0), (midx, split)], fill=INK, width=s(5))
    # The wheel: a small rounded bar in the middle of the split.
    wheel = [midx - s(7), s(22), midx + s(7), s(46)]
    draw.rounded_rectangle(wheel, radius=s(7), fill=GOLD if part in ("middle", "wheel") else PAPER, outline=INK, width=s(4))
    if part == "wheel":
        # Arrows above and below the wheel.
        for dirn, y in ((-1, s(14)), (1, s(54))):
            draw.polygon([(midx - s(9), y), (midx + s(9), y), (midx, y + dirn * s(9))], fill=GOLD)
    if part == "drag":
        for sx in (s(4), s(108)):
            for dy in (-s(22), 0, s(22)):
                x0, x1 = (sx - s(10), sx) if sx < midx else (sx, sx + s(10))
                draw.line([(x0, s(70) + dy), (x1, s(70) + dy)], fill=INK, width=s(4))
    return finish(img, w, h), w, h


def drink(kind):
    """A cup per drink, each its own shape and colour, readable at 32 px."""
    w = h = 128
    img, draw = canvas(w, h)
    cx = s(64)
    outline = dict(outline=INK, width=s(5))
    if kind == "latte":
        # A tall glass: latte brown with a pale foam band and a steam curl.
        draw.rounded_rectangle([s(36), s(26), s(92), s(116)], radius=s(10), fill=(0xc9, 0x9d, 0x6a, 255), **outline)
        draw.rounded_rectangle([s(36), s(26), s(92), s(48)], radius=s(10), fill=PAPER, **outline)
        draw.rectangle([s(39), s(40), s(89), s(50)], fill=PAPER)
        draw.line([(s(39), s(50)), (s(89), s(50))], fill=INK, width=s(4))
        steam(draw, cx, s(14))
    elif kind == "coffee":
        # A mug with a handle, dark coffee, steam.
        draw.rounded_rectangle([s(28), s(34), s(90), s(114)], radius=s(12), fill=(0x6b, 0x3f, 0x1f, 255), **outline)
        draw.ellipse([s(82), s(50), s(112), s(90)], outline=INK, width=s(9))
        draw.rounded_rectangle([s(28), s(34), s(90), s(114)], radius=s(12), fill=(0x6b, 0x3f, 0x1f, 255), **outline)
        draw.rounded_rectangle([s(34), s(40), s(84), s(54)], radius=s(6), fill=(0x3b, 0x23, 0x14, 255))
        steam(draw, s(58), s(18))
    elif kind == "tea":
        # A teacup on a saucer, amber, a tea bag's tag over the rim.
        draw.ellipse([s(14), s(96), s(114), s(118)], fill=PAPER, **outline)
        draw.rounded_rectangle([s(28), s(44), s(96), s(102)], radius=s(16), fill=(0xe0, 0xa8, 0x3e, 255), **outline)
        draw.ellipse([s(90), s(54), s(116), s(84)], outline=INK, width=s(8))
        draw.rounded_rectangle([s(28), s(44), s(96), s(102)], radius=s(16), fill=(0xe0, 0xa8, 0x3e, 255), **outline)
        draw.line([(s(56), s(46)), (s(44), s(22))], fill=INK, width=s(4))
        draw.rounded_rectangle([s(30), s(10), s(54), s(28)], radius=s(4), fill=PAPER, **outline)
    elif kind == "espresso":
        # A small demitasse on a saucer, very dark, a tiny steam.
        draw.ellipse([s(24), s(94), s(104), s(114)], fill=PAPER, **outline)
        draw.rounded_rectangle([s(40), s(56), s(88), s(100)], radius=s(12), fill=(0x3a, 0x22, 0x12, 255), **outline)
        draw.ellipse([s(82), s(62), s(104), s(86)], outline=INK, width=s(7))
        draw.rounded_rectangle([s(40), s(56), s(88), s(100)], radius=s(12), fill=(0x3a, 0x22, 0x12, 255), **outline)
        draw.rounded_rectangle([s(46), s(60), s(82), s(70)], radius=s(5), fill=(0xa8, 0x6a, 0x3a, 255))
        steam(draw, cx, s(36), small=True)
    elif kind == "hotchocolate":
        # A mug of chocolate with a swirl of cream on top.
        draw.rounded_rectangle([s(28), s(40), s(90), s(114)], radius=s(12), fill=(0x8a, 0x52, 0x30, 255), **outline)
        draw.ellipse([s(82), s(56), s(112), s(94)], outline=INK, width=s(9))
        draw.rounded_rectangle([s(28), s(40), s(90), s(114)], radius=s(12), fill=(0x8a, 0x52, 0x30, 255), **outline)
        draw.ellipse([s(30), s(22), s(88), s(54)], fill=PAPER, **outline)
        draw.ellipse([s(42), s(12), s(76), s(38)], fill=PAPER, **outline)
        draw.ellipse([s(52), s(6), s(66), s(20)], fill=PAPER, **outline)
    elif kind == "americano":
        # A tall takeaway cup with a lid, a brown band.
        draw.polygon([(s(34), s(40)), (s(94), s(40)), (s(86), s(118)), (s(42), s(118))], fill=PAPER, outline=INK)
        draw.line([(s(34), s(40)), (s(42), s(118)), (s(86), s(118)), (s(94), s(40))], fill=INK, width=s(5))
        draw.polygon([(s(37), s(62)), (s(91), s(62)), (s(87), s(92)), (s(41), s(92))], fill=(0x6b, 0x44, 0x23, 255))
        draw.rounded_rectangle([s(28), s(26), s(100), s(42)], radius=s(6), fill=PAPER_EDGE, **outline)
        draw.rounded_rectangle([s(50), s(14), s(78), s(28)], radius=s(5), fill=PAPER_EDGE, **outline)
    return finish(img, w, h), w, h


def steam(draw, cx, top, small=False):
    import math
    amp, height = (s(4), s(14)) if small else (s(6), s(22))
    for dx in (-s(14), 0, s(14)) if not small else (-s(8), s(8)):
        pts = []
        for i in range(0, 11):
            t = i / 10
            pts.append((cx + dx + amp * math.sin(t * math.pi * 2), top + height - t * height))
        draw.line(pts, fill=INK, width=s(4), joint="curve")


# ---------------------------------------------------------------- the list

def all_icons():
    icons = []

    def add(name, made):
        img, w, h = made
        icons.append((name, img, w, h))

    add("ps_cross", disc(ps_cross))
    add("ps_circle", disc(ps_circle))
    add("ps_square", disc(ps_square))
    add("ps_triangle", disc(ps_triangle))
    for name, label in (("ps_l1", "L1"), ("ps_r1", "R1"), ("ps_l2", "L2"), ("ps_r2", "R2")):
        add(name, pill(label))
    add("ps_options", pill("OPTIONS", size=30))
    add("ps_share", pill("SHARE", size=30))
    add("ps_create", pill("CREATE", size=30))
    add("ps_touchpad", pill(symbol_fn=touchpad_glyph))

    add("xb_a", disc(letter("A", XB_A)))
    add("xb_b", disc(letter("B", XB_B)))
    add("xb_x", disc(letter("X", XB_X)))
    add("xb_y", disc(letter("Y", XB_Y)))
    for name, label in (("xb_lb", "LB"), ("xb_rb", "RB"), ("xb_lt", "LT"), ("xb_rt", "RT")):
        add(name, pill(label))
    add("xb_menu", pill(symbol_fn=menu_lines))
    add("xb_view", pill(symbol_fn=view_squares))

    add("sw_a", disc(letter("A", PAPER)))
    add("sw_b", disc(letter("B", PAPER)))
    add("sw_x", disc(letter("X", PAPER)))
    add("sw_y", disc(letter("Y", PAPER)))
    for name, label in (("sw_l", "L"), ("sw_r", "R"), ("sw_zl", "ZL"), ("sw_zr", "ZR")):
        add(name, pill(label))
    add("sw_plus", pill(symbol_fn=plus_glyph))
    add("sw_minus", pill(symbol_fn=minus_glyph))

    for arm in ("up", "down", "left", "right", "updown", "leftright"):
        add("dpad_" + arm, dpad(arm))
    add("ls", stick("L", False))
    add("rs", stick("R", False))
    add("ls_press", stick("L", True))
    add("rs_press", stick("R", True))

    for c in "abcdefghijklmnopqrstuvwxyz":
        add("key_" + c, keycap(c.upper()))
    for c in "0123456789":
        add("key_" + c, keycap(c))
    add("key_esc", keycap("Esc", size=44))
    add("key_tab", keycap("Tab", size=44))
    add("key_space", keycap("Space", wide=True, size=50))
    add("key_ctrl", keycap("Ctrl", size=44))
    add("key_shift", keycap("Shift", size=40))
    add("key_enter", keycap("Enter", size=38))
    for arrow in ("up", "down", "left", "right"):
        add("key_" + arrow, keycap("", arrow=arrow))

    for part in ("left", "right", "middle", "wheel", "drag"):
        add("mouse_" + part, mouse(part))

    for kind in ("latte", "coffee", "tea", "espresso", "hotchocolate", "americano"):
        add("drink_" + kind, drink(kind))
    return icons


# ---------------------------------------------------------------- packing and writing

def pack(icons):
    """Rows, left to right, top to bottom, a gutter between; the rects are tight to each icon."""
    sheet = Image.new("RGBA", (SHEET, SHEET), (0, 0, 0, 0))
    entries = []
    x, y, row_h = GUTTER, GUTTER, 0
    for name, img, w, h in icons:
        if x + w + GUTTER > SHEET:
            x = GUTTER
            y += row_h + GUTTER
            row_h = 0
        if y + h + GUTTER > SHEET:
            sys.exit("The icons don't fit on a %d px sheet." % SHEET)
        sheet.paste(img, (x, y), img)
        entries.append({"name": name, "x": x, "y": y, "w": w, "h": h})
        x += w + GUTTER
        row_h = max(row_h, h)
    return sheet, entries


def main():
    icons = all_icons()
    names = [n for n, *_ in icons]
    assert len(names) == len(set(names)), "duplicate icon names"
    sheet, entries = pack(icons)
    os.makedirs(OUT_DIR, exist_ok=True)
    png_path, json_path = os.path.join(OUT_DIR, PNG_NAME), os.path.join(OUT_DIR, JSON_NAME)
    sheet.save(png_path, optimize=True)
    data = {
        "png": PNG_NAME,
        "width": SHEET,
        "height": SHEET,
        "origin": "top-left",
        "font": os.path.relpath(FONT_PATH, REPO) if FONT_PATH.startswith(REPO) else FONT_PATH,
        "icons": entries,
    }
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print("Wrote %d icons to %s and %s (letters: %s)" % (len(entries), png_path, json_path, data["font"]))


if __name__ == "__main__":
    main()
