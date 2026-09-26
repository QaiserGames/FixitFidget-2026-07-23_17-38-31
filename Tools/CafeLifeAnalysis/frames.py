"""Extract video frames at trace times and tile them into a labelled contact sheet.
usage: frames.py <recording dir> <out.jpg> <t1,t2,...> [crop=x,y,w,h] [cols=3] [scale=1.0]"""
import sys, subprocess, os, tempfile
import pandas as pd
from PIL import Image, ImageDraw, ImageFont

def frame_for(frames, t):
    i = (frames.t - t).abs().idxmin()
    return int(frames.frame[i]), float(frames.t[i])

def grab(video, index, fps=12.0):
    tmp = tempfile.mktemp(suffix='.png')
    ts = index / fps + 0.001
    subprocess.run(['ffmpeg', '-v', 'error', '-ss', '%.3f' % ts, '-i', video, '-frames:v', '1', '-y', tmp], check=True)
    im = Image.open(tmp).convert('RGB'); os.remove(tmp); return im

def sheet(rec, out, times, crop=None, cols=3, scale=1.0, labels=None):
    frames = pd.read_csv(os.path.join(rec, 'frames.csv'))
    video = os.path.join(rec, 'video.mp4')
    tiles = []
    for k, t in enumerate(times):
        idx, tt = frame_for(frames, t)
        im = grab(video, idx)
        if crop: im = im.crop((crop[0], crop[1], crop[0] + crop[2], crop[1] + crop[3]))
        if scale != 1.0: im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        label = 't=%.1fs' % tt + ((' ' + labels[k]) if labels and k < len(labels) else '')
        d.rectangle((0, 0, 8 + 7 * len(label), 16), fill=(0, 0, 0))
        d.text((4, 2), label, fill=(255, 255, 0))
        tiles.append(im)
    w, h = tiles[0].size
    rows = (len(tiles) + cols - 1) // cols
    canvas = Image.new('RGB', (w * cols, h * rows), (30, 30, 30))
    for i, im in enumerate(tiles):
        canvas.paste(im, ((i % cols) * w, (i // cols) * h))
    canvas.save(out, quality=88)
    return out

if __name__ == '__main__':
    rec, out, ts = sys.argv[1], sys.argv[2], [float(x) for x in sys.argv[3].split(',')]
    opts = dict(a.split('=') for a in sys.argv[4:])
    crop = tuple(int(v) for v in opts['crop'].split(',')) if 'crop' in opts else None
    sheet(rec, out, ts, crop, int(opts.get('cols', 3)), float(opts.get('scale', 1.0)))
    print(out)
