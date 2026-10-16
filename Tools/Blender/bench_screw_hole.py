"""
BT_ScrewHole.png (14 Oct 2026; Art Direction's A8 in specs/art-bench-and-cafe.md, sized by the feel targets' call 3): the
hole a screw leaves, as a decal. Laid on a quad 1.15 x the screw head's diameter, 0.05 mm proud of the surface the head sat
on (Screw.cs's ScrewSocket.Make, Systems §4.1.4), it reads as a small threaded opening in a slight dip, not a dark disc.

Radii as fractions of the head's radius r (the texture's edge midpoints are at 1.15 r):
  0    - 0.48   the bore: #1A1816, opaque, with two faint thread rings #3A3632 at 0.30 and 0.40 (bore Ø = 0.48 x the head:
                1.82 mm under a 3.8 mm head)
  0.48 - 0.55   the lip: #D8D2C6 at 0.8 alpha (the thread's entry chamfer catching the lamp)
  0.55 - 0.90   the dip: black, alpha 0.35 at the lip easing to 0 at 0.90
  0.90 - 1.00   the witness ring: white at 0.12 (the cleaner metal the head covered)
  1.00 - 1.15   transparent (its colour the witness ring's white, so mipmaps and bilinear filtering bring no dark fringe)
256 x 256, RGBA, sRGB, straight alpha, drawn at 8x and reduced (anti-aliased rings).

python3 Tools/Blender/bench_screw_hole.py [<output path>]   (Pillow and numpy; no Blender)
Default: Assets/Data/Resources/Bench/BT_ScrewHole.png -> Resources.Load<Texture2D>("Bench/BT_ScrewHole").
Import: Texture (Default), sRGB, alpha is transparency, mipmaps on, bilinear, Clamp, max 256, no compression.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
SIZE, SS = 256, 8
EDGE = 1.15                 # the texture's half-width, in head radii

BORE, LIP, DIP, WITNESS = 0.48, 0.55, 0.90, 1.00
C_BORE = (0x1A, 0x18, 0x16)
C_THREAD = (0x3A, 0x36, 0x32)
C_LIP = (0xD8, 0xD2, 0xC6)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def draw():
    n = SIZE * SS
    c = (np.arange(n) + 0.5) / n * 2.0 - 1.0
    xx, yy = np.meshgrid(c, c)
    rho = np.hypot(xx, yy) * EDGE                    # in head radii
    px = EDGE * 2.0 / n                              # one fine pixel in head radii (the edges' softness)
    rgb = np.zeros((n, n, 3))
    a = np.zeros((n, n))
    # layers from the outside in, each "over" the last (straight alpha)

    def over(col, alpha):
        nonlocal rgb, a
        col = np.broadcast_to(np.array(col, dtype=float) / 255.0, rgb.shape) if not isinstance(col, np.ndarray) else col
        out_a = alpha + a * (1 - alpha)
        safe = np.maximum(out_a, 1e-6)
        rgb = (col * alpha[..., None] + rgb * (a * (1 - alpha))[..., None]) / safe[..., None]
        a = out_a

    inside = lambda r: 1.0 - smooth(r - px, r + px, rho)          # noqa: E731  soft step: 1 inside radius r
    # the witness ring: white at 0.12 from 0.90 to 1.00
    over((255, 255, 255), 0.12 * inside(WITNESS) * (1.0 - inside(DIP)))
    # the dip: black, 0 at 0.90 easing to 0.35 at the lip
    dip = 0.35 * (1.0 - smooth(LIP, DIP, rho)) * inside(DIP) * (1.0 - inside(LIP))
    over((0, 0, 0), dip)
    # the lip: a bright ring at 0.8
    over(C_LIP, 0.8 * inside(LIP) * (1.0 - inside(BORE)))
    # the bore, opaque, with two faint thread rings
    over(C_BORE, inside(BORE))
    for r0 in (0.30, 0.40):
        ring = np.clip(1.0 - np.abs(rho - r0) / 0.018, 0.0, 1.0) * inside(BORE)
        over(C_THREAD, ring)
    # the transparent margin keeps the witness ring's white (no dark fringe when filtered)
    rgb = np.where((a < 1e-4)[..., None], 1.0, rgb)
    big = np.concatenate([rgb, a[..., None]], axis=2)
    # reduce with premultiplied alpha (straight colour back out)
    pre = big.copy()
    pre[..., :3] *= pre[..., 3:4]
    small = pre.reshape(SIZE, SS, SIZE, SS, 4).mean(axis=(1, 3))
    alpha = small[..., 3:4]
    col = np.where(alpha > 1e-6, small[..., :3] / np.maximum(alpha, 1e-6), 1.0)
    out = np.concatenate([col, alpha], axis=2)
    return Image.fromarray((out * 255 + 0.5).clip(0, 255).astype(np.uint8), 'RGBA')


def main(path=None):
    path = path or os.path.join(REPO, 'Assets', 'Data', 'Resources', 'Bench', 'BT_ScrewHole.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im = draw()
    im.save(path)
    # a look: the decal over the phone's sage back and the watch's gold, at 1:1 and x4, beside a 3.8 mm head to the same scale
    return path


if __name__ == '__main__':
    print(main(sys.argv[1] if len(sys.argv) > 1 else None))
