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
  Note chips     26 Oct 2026 (Icons on notes): ten 208 px paper chips with a gold rim (note_back_shelf, note_table,
                 note_thrift, note_gnome, note_cups_sleeve, note_cones, note_door, note_bins, note_house, note_machine):
                 the picture before a note's line and on the world pin over the place to go (NoteIcons.cs).
  Furniture      26 Oct 2026 (Icons 2): the seven kinds the Shop sells, plain 128 px pictures (furn_bistro, furn_square,
                 furn_plant, furn_banquette, furn_counter, furn_lounge, furn_communal): the Arrange strip's cards and the
                 phone's YOUR CAFÉ card (NoteIcons.Furniture).
  Items          26 Oct 2026 (Icons 2): the tickets' repair devices and a clock, plain 128 px beside the drinks (item_phone,
                 item_watch, item_camera, item_clock; NoteIcons.ForDevice).
  Kind chips     26 Oct 2026 (Icons 2): a fact's kind on the night's notebook page, 144 px chips (kind_where, kind_owns,
                 kind_when, kind_said, kind_who, kind_secret), and each again on his paper with his rim (kind_*_his).
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


# ---------------------------------------------------------------- the note chips (26 Oct 2026 (Icons on notes))
# His note (24 Oct): "the notes that are given to you from that guy, or any notes, should have icons so that its easier for
# the player to understand what to do ... only on times when we must." Designer I's ten pictures (review24/designs/I/
# note_icons.py), copied here as drawn, each on its chip: a round paper disc with a gold rim (never an ink disc or a keycap,
# so it can't be read as a button to press). He picked A on 26 Oct: the chip before the line, and the same chip on the
# world pin. NoteIcons.cs says which line wears which. The chip is drawn NOTE_CHIP px on the sheet (1.625 x a disc), so
# with ButtonIconSteps' metrics it draws ~42 px beside the Now line's 26 px words and ~48 px beside his 30 px speech,
# centred on the cap band like every icon. (The designer's torch is left out: every torch line already has the F key.)
NOTE_CHIP = 208
NOTE_INNER = round(NOTE_CHIP * 92 / 128)   # the picture inside the rim, as the design drew it (92 of 128)
BRAND = (0x2e, 0x7d, 0x5b, 255)
BRAND_DEEP = (0x1f, 0x5a, 0x41, 255)
GOLD_SOFT = (0xfd, 0xeb, 0xd0, 255)
WOOD = (0xb0, 0x7a, 0x45, 255)
WOOD_DARK = (0x7a, 0x50, 0x2a, 255)
COFFEE = (0x6b, 0x3f, 0x1f, 255)
SKIN = (0xf2, 0xc7, 0xa3, 255)
HAT_RED = (0xc7, 0x45, 0x2e, 255)
GNOME_BLUE = (0x3b, 0x5f, 0xa0, 255)
YELLOW_HOUSE = (0xf2, 0xc9, 0x4c, 255)
CONE = (0xee, 0x7a, 0x2a, 255)
O = dict(outline=INK, width=s(5))


def note_finish(img):
    """The pictures are drawn on the design's 128 grid at 4x (512 px) and shrunk straight to the chip's inner size, so
    they stay as sharp as the design's 128 px ones."""
    return img.resize((NOTE_INNER, NOTE_INNER), Image.LANCZOS)


def poly(d, pts, fill):
    d.polygon([(s(x), s(y)) for x, y in pts], fill=fill)
    d.line([(s(x), s(y)) for x, y in pts + [pts[0], pts[1]]], fill=INK, width=s(5), joint="curve")


def box(d, x0, y0, x1, y1, fill, r=6):
    d.rounded_rectangle([s(x0), s(y0), s(x1), s(y1)], radius=s(r), fill=fill, **O)


def paper_cup(d, x0, x1, top, bottom, band=COFFEE):
    inset = (x1 - x0) * .14
    poly(d, [(x0, top), (x1, top), (x1 - inset, bottom), (x0 + inset, bottom)], PAPER)
    mid = top + (bottom - top) * .45
    d.polygon([(s(x0 + inset * .45 + 2), s(mid)), (s(x1 - inset * .45 - 2), s(mid)),
               (s(x1 - inset * .7 - 2), s(mid + 9)), (s(x0 + inset * .7 + 2), s(mid + 9))], fill=band)
    d.rounded_rectangle([s(x0 - 3), s(top - 6), s(x1 + 3), s(top + 2)], radius=s(3), fill=PAPER_EDGE, **O)


def icon_back_shelf():
    img, d = canvas(128, 128)
    box(d, 14, 10, 24, 120, WOOD_DARK, 3)
    box(d, 104, 10, 114, 120, WOOD_DARK, 3)
    box(d, 10, 56, 118, 66, WOOD, 3)
    box(d, 10, 108, 118, 118, WOOD, 3)
    for x in (30, 54, 78):
        paper_cup(d, x, x + 20, 28, 56)
    # A bag of beans on the lower shelf.
    d.rounded_rectangle([s(34), s(70), s(94), s(108)], radius=s(12), fill=COFFEE, **O)
    d.ellipse([s(54), s(78), s(74), s(100)], fill=(0xc9, 0x9d, 0x6a, 255), **O)
    d.line([(s(64), s(80)), (s(64), s(98))], fill=INK, width=s(4))
    return note_finish(img)


def icon_table():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(57), s(78), s(71), s(116)], radius=s(3), fill=WOOD_DARK, **O)
    d.ellipse([s(36), s(108), s(92), s(122)], fill=WOOD_DARK, **O)
    d.ellipse([s(6), s(56), s(122), s(90)], fill=WOOD, **O)
    # Left behind: a used cup, two coffee rings, crumbs.
    d.ellipse([s(20), s(66), s(46), s(78)], outline=COFFEE, width=s(4))
    d.ellipse([s(40), s(72), s(60), s(82)], outline=COFFEE, width=s(4))
    for cx, cy in ((92, 80), (100, 74), (84, 84)):
        d.ellipse([s(cx - 3), s(cy - 2), s(cx + 3), s(cy + 2)], fill=WOOD_DARK)
    paper_cup(d, 64, 90, 26, 70)
    return note_finish(img)


def icon_thrift():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(46), s(20), s(62), s(32)], radius=s(3), fill=GOLD, **O)
    d.ellipse([s(14), s(28), s(94), s(108)], fill=GOLD, **O)
    d.ellipse([s(24), s(38), s(84), s(98)], fill=PAPER, **O)
    d.line([(s(54), s(68)), (s(54), s(46))], fill=INK, width=s(6))
    d.line([(s(54), s(68)), (s(70), s(76))], fill=INK, width=s(6))
    # The thrift tag on its string.
    d.line([(s(86), s(44)), (s(98), s(30))], fill=INK, width=s(4))
    d.polygon([(s(96), s(10)), (s(122), s(18)), (s(116), s(44)), (s(90), s(36))], fill=GOLD_SOFT)
    d.line([(s(96), s(10)), (s(122), s(18)), (s(116), s(44)), (s(90), s(36)), (s(96), s(10))], fill=INK, width=s(4), joint="curve")
    d.ellipse([s(99), s(18), s(107), s(26)], fill=INK)
    return note_finish(img)


def icon_gnome():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(36), s(76), s(92), s(120)], radius=s(14), fill=GNOME_BLUE, **O)
    poly(d, [(40, 66), (88, 66), (64, 108)], PAPER)                       # beard
    d.ellipse([s(50), s(50), s(78), s(76)], fill=SKIN, **O)              # face
    d.ellipse([s(59), s(62), s(69), s(72)], fill=(0xe8, 0x9a, 0x86, 255), outline=INK, width=s(3))   # nose
    poly(d, [(36, 58), (92, 58), (66, 6)], HAT_RED)                       # hat
    return note_finish(img)


def icon_reunion_cups():
    img, d = canvas(128, 128)
    band = (0xe0, 0x8a, 0x3a, 255)
    for i, top in enumerate((14, 32, 50)):
        paper_cup(d, 30 - i, 98 + i, top, top + 64, band)
    return note_finish(img)


def icon_cone():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(14), s(104), s(114), s(120)], radius=s(4), fill=(0x9a, 0x4a, 0x1a, 255), **O)
    poly(d, [(48, 106), (80, 106), (64, 10)], CONE)
    d.polygon([(s(55), s(64)), (s(73), s(64)), (s(76.5), s(78)), (s(51.5), s(78))], fill=PAPER)
    d.polygon([(s(60), s(34)), (s(68), s(34)), (s(70.5), s(46)), (s(57.5), s(46))], fill=PAPER)
    return note_finish(img)


def icon_door():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(24), s(8), s(104), s(122)], radius=s(6), fill=WOOD_DARK, **O)
    d.rounded_rectangle([s(34), s(18), s(94), s(122)], radius=s(4), fill=WOOD, **O)
    d.rounded_rectangle([s(44), s(28), s(84), s(62)], radius=s(3), outline=WOOD_DARK, width=s(4))
    d.rounded_rectangle([s(44), s(72), s(84), s(110)], radius=s(3), outline=WOOD_DARK, width=s(4))
    d.ellipse([s(78), s(62), s(90), s(74)], fill=GOLD, **O)
    return note_finish(img)


def icon_bins():
    img, d = canvas(128, 128)
    for cx in (34, 94):
        d.ellipse([s(cx - 9), s(104), s(cx + 9), s(122)], fill=INK)
    poly(d, [(12, 50), (116, 50), (108, 110), (20, 110)], BRAND)
    d.line([(s(24), s(70)), (s(104), s(70))], fill=BRAND_DEEP, width=s(4))
    # The lid lifted, two eyes in the gap: the man at the bins (he kept the eyes, 26 Oct).
    d.polygon([(s(16), s(50)), (s(112), s(50)), (s(112), s(40)), (s(16), s(26))], fill=INK)
    d.ellipse([s(48), s(38), s(60), s(48)], fill=PAPER)
    d.ellipse([s(68), s(40), s(80), s(50)], fill=PAPER)
    d.ellipse([s(52), s(41), s(58), s(47)], fill=INK)
    d.ellipse([s(72), s(43), s(78), s(49)], fill=INK)
    poly(d, [(8, 30), (112, 16), (118, 26), (14, 40)], BRAND_DEEP)
    return note_finish(img)


def icon_house():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(22), s(54), s(106), s(120)], radius=s(4), fill=YELLOW_HOUSE, **O)
    poly(d, [(10, 60), (118, 60), (64, 12)], (0xb8, 0x55, 0x3a, 255))
    d.rounded_rectangle([s(54), s(80), s(76), s(120)], radius=s(3), fill=WOOD, **O)
    d.rounded_rectangle([s(30), s(70), s(48), s(88)], radius=s(3), fill=(0x9c, 0xc3, 0xd8, 255), **O)
    d.rounded_rectangle([s(82), s(70), s(100), s(88)], radius=s(3), fill=(0x9c, 0xc3, 0xd8, 255), **O)
    d.ellipse([s(80), s(94), s(92), s(106)], fill=GOLD, **O)          # her porch light
    return note_finish(img)


def icon_machine():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(14), s(10), s(114), s(44)], radius=s(8), fill=(0xd9, 0xd4, 0xcc, 255), **O)
    d.rounded_rectangle([s(14), s(40), s(34), s(120)], radius=s(5), fill=(0xd9, 0xd4, 0xcc, 255), **O)
    d.rounded_rectangle([s(14), s(108), s(114), s(122)], radius=s(4), fill=(0x6b, 0x64, 0x5c, 255), **O)
    d.rounded_rectangle([s(56), s(42), s(84), s(56)], radius=s(3), fill=(0x6b, 0x64, 0x5c, 255), **O)
    d.ellipse([s(84), s(18), s(100), s(34)], fill=BRAND, **O)
    paper_cup(d, 56, 84, 74, 108)
    d.line([(s(70), s(58)), (s(70), s(70))], fill=COFFEE, width=s(5))
    return note_finish(img)


NOTE_ICONS = (
    ("note_back_shelf", icon_back_shelf), ("note_table", icon_table), ("note_thrift", icon_thrift), ("note_gnome", icon_gnome),
    ("note_cups_sleeve", icon_reunion_cups), ("note_cones", icon_cone), ("note_door", icon_door), ("note_bins", icon_bins),
    ("note_house", icon_house), ("note_machine", icon_machine),
)


def note_chip(picture_fn):
    """The design's chip (note_icons.chip): a paper disc, a gold rim, the picture inside; at NOTE_CHIP px."""
    k = NOTE_CHIP / 128
    img, d = canvas(NOTE_CHIP, NOTE_CHIP)
    d.ellipse([0, 0, s(NOTE_CHIP), s(NOTE_CHIP)], fill=PAPER)
    d.ellipse([s(4 * k), s(4 * k), s(NOTE_CHIP - 4 * k), s(NOTE_CHIP - 4 * k)], outline=GOLD, width=round(s(8 * k)))
    out = finish(img, NOTE_CHIP, NOTE_CHIP)
    inner = picture_fn()
    at = (NOTE_CHIP - NOTE_INNER) // 2
    out.alpha_composite(inner, (at, at))
    return out, NOTE_CHIP, NOTE_CHIP


# ---------------------------------------------------------------- 26 Oct 2026 (Icons 2): the furniture, the tickets' items, the facts' kinds
# His note (26 Oct): "the icons on both the notebook and notes, i wanted icons for the furniture you can also purchase too";
# and for the tickets, "nice big bold icons for each item, like a coffee, tea, etc, clock". Three more sets, in the drinks'
# and the note chips' style (ink outline, flat fills, chunky):
#   furn_*   the seven kinds the Shop sells (FurnitureDefinition's asset names, the save identity: NoteIcons.Furniture maps
#            them), plain 128 px pictures like the drinks, for the Arrange strip's cards (~60 px) and the phone's YOUR CAFÉ
#            card (~40 px). Each is its piece from the front with its seats showing, so a glance tells them apart: the bistro
#            round on a pedestal with two chairs, the square table on four legs, the plant, the bay's green booth back, the
#            window counter's pane and stools, the lounge's sofa and low table, the communal table's long row of chairs.
#   item_*   the tickets' repair devices (the phone, the pocket watch, Grace's film camera) and a clock, plain 128 px like
#            the six drink_* cups they sit beside, for the HUD round that comes next (NoteIcons.ForDevice).
#   kind_*   a fact's kind on the night's notebook page (Notebook.Kinds: where someone is, what they own, when they do
#            something, what was said, who they are to someone, a secret of Ace's): a smaller chip (KIND_CHIP px: ~31 px
#            beside the page's 26 px notes, in the line) before each fact, in place of the old bullet. Six more, kind_*_his,
#            are the same pictures on his paper with his rim (the man at the bins' pages, which had his square bullet).
# Every picture is drawn on the 128 grid at 4x like the rest; the kind marks use a heavier outline, since they show smallest.
KIND_CHIP = 144
KIND_INNER = round(KIND_CHIP * 92 / 128)
HIS_RIM = (0x9e, 0x52, 0x4d, 255)          # NightNotebook.HisInk: the man's suit
HIS_PAPER = (0xf3, 0xe4, 0xe0, 255)        # a page in his hand: warmer paper
UPHOLSTERY = (0xc9, 0x70, 0x2d, 255)       # UiSkin.Accent: the lounge's sofa
TERRACOTTA = (0xc2, 0x6a, 0x3c, 255)
GLASS = (0x9c, 0xc3, 0xd8, 255)            # the window pane (icon_house's windows)
SCREEN = (0xb9, 0xc7, 0xe0, 255)           # the phone's screen (the bench icon's pale blue)
CAMERA = (0x4a, 0x44, 0x3e, 255)
UI_RED = (0xb8, 0x48, 0x3c, 255)           # UiSkin.Red: the map pin
OB = dict(outline=INK, width=s(7))         # the bold outline for the small kind marks


def chair(d, x, top, w=22, h=70, fill=WOOD, back=WOOD_DARK):
    """A café chair from the front: its back (an open frame), its seat and two legs; x is its left, top its back's top."""
    d.rounded_rectangle([s(x), s(top), s(x + w), s(top + h * .45)], radius=s(4), fill=back, **O)
    d.rounded_rectangle([s(x + 4), s(top + 5), s(x + w - 4), s(top + h * .45 - 10)], radius=s(2), fill=PAPER)
    d.rounded_rectangle([s(x - 2), s(top + h * .45 - 2), s(x + w + 2), s(top + h * .45 + 9)], radius=s(3), fill=fill, **O)
    for lx in (x + 1, x + w - 6):
        d.rounded_rectangle([s(lx), s(top + h * .45 + 7), s(lx + 5), s(top + h)], radius=s(2), fill=back, **O)


def stool(d, cx, top):
    """A bar stool: a round seat on one leg with a foot."""
    d.rounded_rectangle([s(cx - 3), s(top + 8), s(cx + 3), s(top + 36)], radius=s(2), fill=WOOD_DARK, **O)
    d.rounded_rectangle([s(cx - 12), s(top + 32), s(cx + 12), s(top + 38)], radius=s(3), fill=WOOD_DARK, **O)
    d.ellipse([s(cx - 15), s(top), s(cx + 15), s(top + 14)], fill=WOOD, **O)


def icon_furn_bistro():
    img, d = canvas(128, 128)
    chair(d, 4, 40)
    chair(d, 102, 40)
    # A round top on a pedestal, as the café's bistros are (note_table's table, standing).
    d.rounded_rectangle([s(60), s(66), s(68), s(104)], radius=s(3), fill=WOOD_DARK, **O)
    d.ellipse([s(40), s(98), s(88), s(112)], fill=WOOD_DARK, **O)
    d.ellipse([s(30), s(50), s(98), s(72)], fill=WOOD, **O)
    return finish(img, 128, 128), 128, 128


def icon_furn_square():
    img, d = canvas(128, 128)
    chair(d, 4, 40)
    chair(d, 102, 40)
    # A square top seen a little from above (a lighter face, a dark front edge) on four legs.
    for lx in (36, 86):
        d.rounded_rectangle([s(lx), s(64), s(lx + 6), s(110)], radius=s(2), fill=WOOD_DARK, **O)
    poly(d, [(38, 46), (90, 46), (98, 60), (30, 60)], WOOD)
    d.rounded_rectangle([s(30), s(58), s(98), s(68)], radius=s(3), fill=WOOD_DARK, **O)
    return finish(img, 128, 128), 128, 128


def icon_furn_plant():
    img, d = canvas(128, 128)
    poly(d, [(36, 78), (92, 78), (84, 120), (44, 120)], TERRACOTTA)
    d.rounded_rectangle([s(30), s(72), s(98), s(84)], radius=s(4), fill=TERRACOTTA, **O)
    # Three big leaves on their stems, each with its vein.
    for (lx, ly) in ((36, 30), (92, 26), (64, 12)):
        d.line([(s(64), s(76)), (s(lx), s(ly + 14))], fill=BRAND_DEEP, width=s(5))
    for (cx, cy, rx, ry, vein) in ((36, 30, 24, 18, ((54, 42), (20, 20))), (92, 26, 24, 18, ((74, 38), (108, 16))), (64, 14, 20, 16, ((64, 28), (64, 2)))):
        d.ellipse([s(cx - rx), s(cy - ry), s(cx + rx), s(cy + ry)], fill=BRAND, **O)
        d.line([(s(vein[0][0]), s(vein[0][1])), (s(vein[1][0]), s(vein[1][1]))], fill=BRAND_DEEP, width=s(4))
    return finish(img, 128, 128), 128, 128


def icon_furn_banquette():
    img, d = canvas(128, 128)
    # A booth: the high padded back in the café's green (its seams), the bench, the wooden end caps, a small table in front.
    d.rounded_rectangle([s(10), s(18), s(118), s(76)], radius=s(8), fill=BRAND, **O)
    for sx in (46, 82):
        d.line([(s(sx), s(24)), (s(sx), s(70))], fill=BRAND_DEEP, width=s(4))
    d.rounded_rectangle([s(6), s(70), s(122), s(86)], radius=s(5), fill=BRAND_DEEP, **O)
    for cx in (6, 114):
        d.rounded_rectangle([s(cx), s(60), s(cx + 8), s(112)], radius=s(3), fill=WOOD_DARK, **O)
    d.rounded_rectangle([s(60), s(92), s(68), s(118)], radius=s(3), fill=WOOD_DARK, **O)
    d.ellipse([s(34), s(82), s(94), s(100)], fill=WOOD, **O)
    return finish(img, 128, 128), 128, 128


def icon_furn_counter():
    img, d = canvas(128, 128)
    # The window: a pane with its cross, the counter's top along it, three stools in front.
    d.rounded_rectangle([s(14), s(6), s(114), s(66)], radius=s(4), fill=GLASS, **O)
    d.line([(s(64), s(10)), (s(64), s(62))], fill=PAPER, width=s(5))
    d.line([(s(18), s(36)), (s(110), s(36))], fill=PAPER, width=s(5))
    d.rounded_rectangle([s(4), s(62), s(124), s(76)], radius=s(4), fill=WOOD, **O)
    for cx in (24, 64, 104):
        stool(d, cx, 82)
    return finish(img, 128, 128), 128, 128


def icon_furn_lounge():
    img, d = canvas(128, 128)
    # A two-seat sofa with rolled arms and a low round table in front of it.
    d.rounded_rectangle([s(14), s(30), s(106), s(70)], radius=s(10), fill=UPHOLSTERY, **O)
    d.rounded_rectangle([s(8), s(62), s(112), s(92)], radius=s(8), fill=UPHOLSTERY, **O)
    d.line([(s(60), s(36)), (s(60), s(64))], fill=INK, width=s(4))
    for ax in (2, 104):
        d.rounded_rectangle([s(ax), s(54), s(ax + 22), s(94)], radius=s(10), fill=UPHOLSTERY, **O)
    for lx in (14, 100):
        d.rounded_rectangle([s(lx), s(92), s(lx + 7), s(104)], radius=s(2), fill=WOOD_DARK, **O)
    d.rounded_rectangle([s(96), s(100), s(104), s(122)], radius=s(3), fill=WOOD_DARK, **O)
    d.ellipse([s(76), s(92), s(124), s(108)], fill=WOOD, **O)
    return finish(img, 128, 128), 128, 128


def icon_furn_communal():
    img, d = canvas(128, 128)
    # A long table with a row of chairs behind it (four backs showing) and its legs: many seats at a glance.
    for cx in (8, 36, 64, 92):
        d.rounded_rectangle([s(cx), s(28), s(cx + 26), s(58)], radius=s(4), fill=WOOD_DARK, **O)
        d.rounded_rectangle([s(cx + 5), s(33), s(cx + 21), s(50)], radius=s(2), fill=PAPER)
    for lx in (12, 110):
        d.rounded_rectangle([s(lx), s(68), s(lx + 7), s(116)], radius=s(2), fill=WOOD_DARK, **O)
    poly(d, [(14, 54), (114, 54), (124, 66), (4, 66)], WOOD)
    d.rounded_rectangle([s(4), s(64), s(124), s(74)], radius=s(3), fill=WOOD_DARK, **O)
    # A chair in front, turned to the table (its back to us), so the near side has seats too.
    d.rounded_rectangle([s(50), s(78), s(78), s(96)], radius=s(4), fill=WOOD, **O)
    for lx in (52, 71):
        d.rounded_rectangle([s(lx), s(94), s(lx + 5), s(118)], radius=s(2), fill=WOOD_DARK, **O)
    return finish(img, 128, 128), 128, 128


FURNITURE_ICONS = (
    ("furn_bistro", icon_furn_bistro), ("furn_square", icon_furn_square), ("furn_plant", icon_furn_plant),
    ("furn_banquette", icon_furn_banquette), ("furn_counter", icon_furn_counter), ("furn_lounge", icon_furn_lounge),
    ("furn_communal", icon_furn_communal),
)


def icon_item_phone():
    img, d = canvas(128, 128)
    d.rounded_rectangle([s(34), s(6), s(94), s(122)], radius=s(12), fill=INK, **O)
    d.rounded_rectangle([s(40), s(16), s(88), s(110)], radius=s(5), fill=SCREEN)
    d.polygon([(s(40), s(16)), (s(62), s(16)), (s(40), s(50))], fill=(0xd6, 0xdf, 0xef, 255))   # a glint
    d.ellipse([s(60), s(8), s(68), s(14)], fill=INK_SHADE)                                        # the camera
    d.rounded_rectangle([s(52), s(112), s(76), s(116)], radius=s(2), fill=INK_SHADE)             # the home bar
    return finish(img, 128, 128), 128, 128


def icon_item_watch():
    img, d = canvas(128, 128)
    # A pocket watch: the bow and crown, the gold case, a paper dial with its quarters and two hands.
    d.ellipse([s(52), s(4), s(76), s(28)], outline=INK, width=s(7))
    d.rounded_rectangle([s(56), s(22), s(72), s(36)], radius=s(3), fill=GOLD, **O)
    d.ellipse([s(14), s(30), s(114), s(126)], fill=GOLD, **O)
    d.ellipse([s(26), s(42), s(102), s(114)], fill=PAPER, **O)
    for (x0, y0, x1, y1) in ((62, 46, 66, 54), (62, 102, 66, 110), (30, 76, 38, 80), (90, 76, 98, 80)):
        d.rectangle([s(x0), s(y0), s(x1), s(y1)], fill=INK)
    d.line([(s(64), s(78)), (s(64), s(56))], fill=INK, width=s(6))
    d.line([(s(64), s(78)), (s(84), s(90))], fill=INK, width=s(6))
    d.ellipse([s(60), s(74), s(68), s(82)], fill=INK)
    return finish(img, 128, 128), 128, 128


def icon_item_camera():
    img, d = canvas(128, 128)
    # Grace's film camera: a boxy body with its leather band, the prism hump, the shutter button, a big lens.
    d.rounded_rectangle([s(44), s(22), s(84), s(44)], radius=s(5), fill=CAMERA, **O)
    d.rounded_rectangle([s(94), s(26), s(108), s(40)], radius=s(4), fill=GOLD, **O)
    d.rounded_rectangle([s(8), s(38), s(120), s(106)], radius=s(8), fill=CAMERA, **O)
    d.rectangle([s(12), s(54), s(116), s(92)], fill=COFFEE)
    d.ellipse([s(36), s(44), s(92), s(100)], fill=INK, **O)
    d.ellipse([s(46), s(54), s(82), s(90)], fill=(0x2d, 0x4d, 0x6e, 255), **O)
    d.ellipse([s(52), s(60), s(64), s(72)], fill=(0x8f, 0xb4, 0xd6, 255))
    return finish(img, 128, 128), 128, 128


def clock_face(d, cx, cy, r, rim=INK, bold=False):
    """A clock: a paper face in an ink rim, its quarters marked, the hands at ten past ten."""
    w = s(9) if bold else s(7)
    d.ellipse([s(cx - r), s(cy - r), s(cx + r), s(cy + r)], fill=PAPER, outline=rim, width=w)
    tick = r * .16
    for (dx, dy) in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        x0, y0 = cx + dx * (r - tick * 2.2), cy + dy * (r - tick * 2.2)
        x1, y1 = cx + dx * (r - tick * .9), cy + dy * (r - tick * .9)
        d.line([(s(x0), s(y0)), (s(x1), s(y1))], fill=INK, width=s(7 if bold else 5))
    d.line([(s(cx), s(cy)), (s(cx - r * .42), s(cy - r * .28))], fill=INK, width=s(8 if bold else 6))
    d.line([(s(cx), s(cy)), (s(cx + r * .3), s(cy - r * .58))], fill=INK, width=s(8 if bold else 6))
    d.ellipse([s(cx - 5), s(cy - 5), s(cx + 5), s(cy + 5)], fill=GOLD, outline=INK, width=s(3))


def icon_item_clock():
    img, d = canvas(128, 128)
    clock_face(d, 64, 64, 56)
    return finish(img, 128, 128), 128, 128


ITEM_ICONS = (
    ("item_phone", icon_item_phone), ("item_watch", icon_item_watch), ("item_camera", icon_item_camera), ("item_clock", icon_item_clock),
)


# The kind marks: bold, few shapes, drawn to read at ~22 px inside their chip.

def kind_where():
    img, d = canvas(128, 128)
    # A map pin in the game's red.
    d.polygon([(s(26), s(58)), (s(102), s(58)), (s(64), s(118))], fill=UI_RED)
    d.line([(s(28), s(60)), (s(64), s(118)), (s(100), s(60))], fill=INK, width=s(7), joint="curve")
    d.ellipse([s(20), s(8), s(108), s(96)], fill=UI_RED, **OB)
    d.ellipse([s(48), s(36), s(80), s(68)], fill=PAPER, **OB)
    return note_finish_kind(img)


def kind_owns():
    img, d = canvas(128, 128)
    # A suitcase: their belongings.
    d.rounded_rectangle([s(44), s(14), s(84), s(44)], radius=s(8), outline=INK, width=s(8))
    d.rounded_rectangle([s(10), s(36), s(118), s(112)], radius=s(10), fill=WOOD, **OB)
    d.rectangle([s(10), s(66), s(118), s(78)], fill=WOOD_DARK)
    d.rounded_rectangle([s(54), s(62), s(74), s(82)], radius=s(3), fill=GOLD, **O)
    return note_finish_kind(img)


def kind_when():
    img, d = canvas(128, 128)
    clock_face(d, 64, 64, 54, bold=True)
    return note_finish_kind(img)


def kind_said():
    img, d = canvas(128, 128)
    # A speech bubble, its tail to the left: what was said.
    d.polygon([(s(26), s(78)), (s(18), s(112)), (s(58), s(86))], fill=INK)
    d.rounded_rectangle([s(8), s(14), s(120), s(92)], radius=s(18), fill=PAPER, **OB)
    d.polygon([(s(32), s(84)), (s(26), s(104)), (s(52), s(86))], fill=PAPER)
    for cx in (40, 64, 88):
        d.ellipse([s(cx - 7), s(46), s(cx + 7), s(60)], fill=INK)
    return note_finish_kind(img)


def kind_who():
    img, d = canvas(128, 128)
    # Two people, one a little behind the other: who they are to someone.
    d.ellipse([s(62), s(22), s(104), s(64)], fill=SKIN, **OB)
    d.rounded_rectangle([s(50), s(62), s(120), s(118)], radius=s(22), fill=GNOME_BLUE, **OB)
    d.rectangle([s(50), s(96), s(120), s(118)], fill=GNOME_BLUE)
    d.ellipse([s(18), s(30), s(66), s(78)], fill=SKIN, **OB)
    d.rounded_rectangle([s(4), s(76), s(82), s(124)], radius=s(24), fill=HAT_RED, **OB)
    d.rectangle([s(4), s(104), s(82), s(124)], fill=HAT_RED)
    d.line([(s(4), s(118)), (s(82), s(118))], fill=INK, width=s(7))
    d.line([(s(50), s(112)), (s(120), s(112))], fill=INK, width=s(7))
    return note_finish_kind(img)


def kind_secret():
    img, d = canvas(128, 128)
    # A padlock: Ace's own secrets (what he took, and for whom).
    d.rounded_rectangle([s(34), s(10), s(94), s(70)], radius=s(26), outline=INK, width=s(10))
    d.rounded_rectangle([s(16), s(52), s(112), s(118)], radius=s(10), fill=GOLD, **OB)
    d.ellipse([s(54), s(70), s(74), s(90)], fill=INK)
    d.rounded_rectangle([s(60), s(84), s(68), s(104)], radius=s(3), fill=INK)
    return note_finish_kind(img)


def note_finish_kind(img):
    """A kind mark's picture at its chip's inner size (the kind chips are smaller than the note chips)."""
    return img.resize((KIND_INNER, KIND_INNER), Image.LANCZOS)


KIND_ICONS = (
    ("kind_where", kind_where), ("kind_owns", kind_owns), ("kind_when", kind_when), ("kind_said", kind_said),
    ("kind_who", kind_who), ("kind_secret", kind_secret),
)


def kind_chip(picture_fn, his):
    """A kind mark on its chip: paper with the gold rim; his pages' on his warmer paper with his rim. KIND_CHIP px."""
    k = KIND_CHIP / 128
    img, d = canvas(KIND_CHIP, KIND_CHIP)
    d.ellipse([0, 0, s(KIND_CHIP), s(KIND_CHIP)], fill=HIS_PAPER if his else PAPER)
    d.ellipse([s(4 * k), s(4 * k), s(KIND_CHIP - 4 * k), s(KIND_CHIP - 4 * k)], outline=HIS_RIM if his else GOLD, width=round(s(9 * k)))
    out = finish(img, KIND_CHIP, KIND_CHIP)
    inner = picture_fn()
    at = (KIND_CHIP - KIND_INNER) // 2
    out.alpha_composite(inner, (at, at))
    return out, KIND_CHIP, KIND_CHIP


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
    # 26 Oct 2026 (Icons on notes): the ten note chips, last, so every older icon keeps its place on the sheet.
    for name, picture in NOTE_ICONS:
        add(name, note_chip(picture))
    # 26 Oct 2026 (Icons 2): the furniture, the tickets' items and the facts' kinds, after them for the same reason.
    for name, picture in FURNITURE_ICONS:
        add(name, picture())
    for name, picture in ITEM_ICONS:
        add(name, picture())
    for name, picture in KIND_ICONS:
        add(name, kind_chip(picture, his=False))
    for name, picture in KIND_ICONS:
        add(name + "_his", kind_chip(picture, his=True))
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
