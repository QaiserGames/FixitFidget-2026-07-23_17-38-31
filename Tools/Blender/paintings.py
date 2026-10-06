"""
The four paintings for the house kit's picture frames (HK_Painting_*): simple flat-colour compositions in the
game's palette, drawn here with Pillow so nothing is copied from anywhere. Unity maps each to the material of
the same name (the kit's import step puts the PNG on it).

  python Tools/Blender/paintings.py [<folder>]     (default: Assets/Art/Textures/HouseKit)
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, 'Assets', 'Art', 'Textures', 'HouseKit')
os.makedirs(out, exist_ok=True)
random.seed(5)


def canvas(w, h, colour):
    return Image.new('RGB', (w, h), colour)


def grain(im, amount=10):
    """A little canvas grain, so a flat colour reads as paint."""
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            d = random.randint(-amount, amount)
            px[x, y] = (max(0, min(255, r + d)), max(0, min(255, g + d)), max(0, min(255, b + d)))
    return im.filter(ImageFilter.GaussianBlur(0.6))


def hills(w=512, h=368):
    im = canvas(w, h, (214, 196, 160))
    d = ImageDraw.Draw(im)
    # sky bands
    for i, c in enumerate([(226, 206, 168), (219, 190, 150), (206, 170, 134)]):
        d.rectangle([0, i * h * .12, w, h], fill=c)
    d.ellipse([w * .66, h * .12, w * .78, h * .12 + w * .12], fill=(241, 214, 150))
    # four hills, far to near
    hills_c = [(142, 160, 150), (112, 140, 118), (90, 122, 92), (70, 102, 72)]
    for k, c in enumerate(hills_c):
        pts = []
        base = h * (.42 + .16 * k)
        for x in range(0, w + 1, 16):
            y = base - (28 + 10 * k) * math.sin(x / (120.0 + 30 * k) + k * 1.3) - 12 * math.sin(x / 41.0 + k)
            pts.append((x, y))
        pts += [(w, h), (0, h)]
        d.polygon(pts, fill=c)
    # a small house on the second hill and a track
    hx, hy = w * .30, h * .60
    d.rectangle([hx, hy, hx + 26, hy + 18], fill=(226, 220, 204))
    d.polygon([(hx - 3, hy), (hx + 13, hy - 12), (hx + 29, hy)], fill=(140, 72, 58))
    d.rectangle([hx + 10, hy + 8, hx + 16, hy + 18], fill=(70, 50, 40))
    d.line([(hx + 13, hy + 18), (w * .22, h * .78), (w * .05, h)], fill=(190, 170, 130), width=5)
    return grain(im)


def harbour(w=512, h=348):
    im = canvas(w, h, (205, 190, 165))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w, h * .55], fill=(216, 200, 176))
    d.rectangle([0, h * .55, w, h], fill=(92, 128, 140))
    for k in range(6):
        y = h * .58 + k * h * .07
        d.line([(0, y), (w, y)], fill=(118, 150, 160), width=2)
    # a quay on the right, a lighthouse
    d.rectangle([w * .70, h * .50, w, h * .60], fill=(150, 140, 120))
    d.rectangle([w * .84, h * .22, w * .89, h * .52], fill=(236, 230, 220))
    d.rectangle([w * .84, h * .30, w * .89, h * .34], fill=(180, 60, 50))
    d.rectangle([w * .835, h * .19, w * .895, h * .23], fill=(60, 50, 44))
    # three boats
    for (bx, by, s, sail) in ((w * .18, h * .70, 1.0, (238, 232, 220)), (w * .42, h * .64, .8, (210, 90, 70)), (w * .56, h * .74, 1.15, (238, 232, 220))):
        hull = [(bx - 40 * s, by), (bx + 40 * s, by), (bx + 30 * s, by + 14 * s), (bx - 30 * s, by + 14 * s)]
        d.polygon(hull, fill=(70, 55, 45))
        d.line([(bx, by), (bx, by - 60 * s)], fill=(60, 50, 44), width=3)
        d.polygon([(bx + 2, by - 58 * s), (bx + 36 * s, by - 6), (bx + 2, by - 6)], fill=sail)
    # gulls
    for (gx, gy) in ((w * .3, h * .2), (w * .36, h * .16), (w * .6, h * .3)):
        d.arc([gx - 10, gy - 6, gx, gy + 6], 200, 340, fill=(80, 70, 60), width=2)
        d.arc([gx, gy - 6, gx + 10, gy + 6], 200, 340, fill=(80, 70, 60), width=2)
    return grain(im)


def still_life(w=340, h=420):
    im = canvas(w, h, (178, 142, 106))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w, h * .62], fill=(166, 128, 96))
    d.rectangle([0, h * .62, w, h], fill=(120, 86, 60))
    d.rectangle([0, h * .60, w, h * .64], fill=(150, 112, 80))
    # a jug
    d.rounded_rectangle([w * .14, h * .30, w * .40, h * .62], radius=22, fill=(74, 96, 130))
    d.ellipse([w * .12, h * .27, w * .42, h * .35], fill=(96, 120, 156))
    d.arc([w * .36, h * .36, w * .50, h * .54], 270, 90, fill=(74, 96, 130), width=9)
    # a bowl with three fruits
    d.ellipse([w * .44, h * .50, w * .90, h * .66], fill=(214, 200, 176))
    for (fx, fy, r, c) in ((w * .55, h * .49, 26, (200, 70, 50)), (w * .70, h * .46, 28, (226, 170, 60)), (w * .80, h * .52, 22, (124, 150, 70))):
        d.ellipse([fx - r, fy - r, fx + r, fy + r], fill=c)
    d.ellipse([w * .44, h * .58, w * .90, h * .70], fill=(196, 180, 156))
    # a lemon on the table
    d.ellipse([w * .62, h * .70, w * .74, h * .78], fill=(232, 200, 80))
    return grain(im)


def portrait(w=300, h=380):
    im = canvas(w, h, (96, 110, 126))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w, h], fill=(104, 120, 136))
    d.ellipse([-w * .3, -h * .1, w * 1.3, h * 1.1], fill=(118, 134, 150))
    # shoulders, neck, head, hair, collar: a plain figure, no one in particular
    d.polygon([(w * .10, h), (w * .90, h), (w * .80, h * .70), (w * .62, h * .60), (w * .38, h * .60), (w * .20, h * .70)], fill=(70, 60, 80))
    d.rectangle([w * .43, h * .48, w * .57, h * .64], fill=(214, 178, 150))
    d.ellipse([w * .32, h * .18, w * .68, h * .56], fill=(222, 188, 160))
    d.chord([w * .30, h * .14, w * .70, h * .44], 180, 360, fill=(72, 48, 36))
    d.polygon([(w * .30, h * .30), (w * .34, h * .52), (w * .40, h * .30)], fill=(72, 48, 36))
    d.polygon([(w * .70, h * .30), (w * .66, h * .52), (w * .60, h * .30)], fill=(72, 48, 36))
    d.polygon([(w * .38, h * .62), (w * .50, h * .74), (w * .62, h * .62), (w * .50, h * .68)], fill=(236, 230, 220))
    d.ellipse([w * .49, h * .40, w * .51, h * .42], fill=(120, 80, 70))
    d.arc([w * .44, h * .44, w * .56, h * .50], 10, 170, fill=(150, 90, 80), width=2)
    return grain(im)


for name, fn in (('HK_Painting_Hills', hills), ('HK_Painting_Harbour', harbour), ('HK_Painting_Still', still_life), ('HK_Painting_Portrait', portrait)):
    im = fn()
    path = os.path.join(out, name + '.png')
    im.save(path)
    print(path, im.size)
