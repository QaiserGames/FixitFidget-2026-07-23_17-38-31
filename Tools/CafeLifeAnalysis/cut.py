"""Cut a clip from a recording by trace time. usage: cut.py <folder> <t0> <t1> <out.mp4> [cx cy zoom] [speed]"""
import sys, os, subprocess
import pandas as pd
folder, t0, t1, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
fr = pd.read_csv(os.path.join(folder, 'frames.csv'))
n0 = int(fr.iloc[(fr.t - t0).abs().argmin()].frame); n1 = int(fr.iloc[(fr.t - t1).abs().argmin()].frame)
v0, dur = n0 / 12.0, (n1 - n0) / 12.0
vf = []
if len(sys.argv) >= 8 and sys.argv[5] != '-':
    cx, cy, zoom = float(sys.argv[5]), float(sys.argv[6]), float(sys.argv[7])
    w, h = int(960 / zoom) // 2 * 2, int(540 / zoom) // 2 * 2
    x = int(min(max(cx - w / 2, 0), 960 - w)); y = int(min(max(cy - h / 2, 0), 540 - h))
    vf.append(f'crop={w}:{h}:{x}:{y}'); vf.append('scale=960:540:flags=lanczos')
speed = float(sys.argv[8]) if len(sys.argv) >= 9 else 1.0
if speed != 1.0: vf.append(f'setpts=PTS/{speed}')
cmd = ['ffmpeg', '-loglevel', 'error', '-y', '-ss', f'{v0:.3f}', '-i', os.path.join(folder, 'video.mp4'), '-t', f'{dur:.3f}']
if vf: cmd += ['-vf', ','.join(vf)]
cmd += ['-r', '24' if speed != 1.0 else '12', '-c:v', 'libx264', '-crf', '22', '-preset', 'medium', '-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart', out]
subprocess.run(cmd, check=True)
print(out, f'{os.path.getsize(out)/1e6:.1f} MB', f'{dur/speed:.1f} s')
