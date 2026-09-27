"""Contact sheet of video frames around a world point, for judging motion by eye.

usage: sheet.py <folder> <camera: whole|counter|tables|lounge> <x> <z> <t0> <t1> <step> <out.jpg> [crop_w crop_h]
Frames are picked from frames.csv (trace time -> video frame), cropped around the
projected world point (y 0.8) and laid out in rows with their times.
"""
import sys, os, math, subprocess, tempfile
import numpy as np, pandas as pd
from PIL import Image, ImageDraw

VIEWS = {  # CafeLifeRecorder observer cameras: position, target, vertical fov
    'whole': ((8.5, 11, -5.5), (0, 0, 7.2), 50),
    'counter': ((4.2, 5.2, 4.6), (0, .8, 12), 52),
    'tables': ((6.5, 6.5, -3.5), (-.5, .4, 4.5), 55),
    'lounge': ((-.8, 4.4, .8), (-6.4, .5, 5), 58),
}

def project(view, p, W=960, H=540):
    c, t, fov = VIEWS[view]
    c, t, p = np.array(c, float), np.array(t, float), np.array(p, float)
    f = t - c; f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)   # Unity: right = up x forward (left-handed)
    u = np.cross(f, r)
    d = p - c
    x, y, z = d @ r, d @ u, d @ f
    th = math.tan(math.radians(fov) / 2)
    sx = (x / (z * th * W / H)) * .5 + .5
    sy = (y / (z * th)) * .5 + .5
    return sx * W, (1 - sy) * H

def main():
    folder, view, x, z, t0, t1, step, out = sys.argv[1:9]
    cw, ch = (int(sys.argv[9]), int(sys.argv[10])) if len(sys.argv) > 10 else (240, 200)
    x, z, t0, t1, step = map(float, (x, z, t0, t1, step))
    fr = pd.read_csv(os.path.join(folder, 'frames.csv'))
    px, py = project(view, (x, .8, z))
    times = np.arange(t0, t1 + 1e-6, step)
    tiles = []
    with tempfile.TemporaryDirectory() as tmp:
        for i, t in enumerate(times):
            n = int(fr.iloc[(fr.t - t).abs().argmin()].frame)
            path = os.path.join(tmp, f'{i}.png')
            subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-i', os.path.join(folder, 'video.mp4'),
                            '-vf', f'select=eq(n\\,{n})', '-vframes', '1', path], check=True)
            im = Image.open(path).convert('RGB')
            box = (int(px - cw / 2), int(py - ch / 2), int(px + cw / 2), int(py + ch / 2))
            tile = im.crop(box).resize((cw * 2, ch * 2), Image.LANCZOS)
            ImageDraw.Draw(tile).text((6, 4), f't={t:.2f}', fill=(255, 255, 0))
            tiles.append(tile)
    cols = min(6, len(tiles)); rows = math.ceil(len(tiles) / cols)
    sheet = Image.new('RGB', (cols * cw * 2, rows * ch * 2), 'black')
    for i, tile in enumerate(tiles): sheet.paste(tile, ((i % cols) * cw * 2, (i // cols) * ch * 2))
    sheet.save(out, quality=88)
    print(out, sheet.size, 'centre px', round(px), round(py))

if __name__ == '__main__':
    main()
