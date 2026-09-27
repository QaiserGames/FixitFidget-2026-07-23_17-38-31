"""Pass 2c summary for café life recordings: the numbers Mansoor's complaints map to.

usage: pass2c.py <folder> [<folder> ...]

Per recording:
  sits          walks into a seat (hand-walked)
  slide_in      longest slide without the walk clip on the way in (s)
  kink_p90      direction change at the hand-over into the seat, 90th percentile (deg)
  side_p90      body facing vs the way it moves in the first 0.4 s of the way in, 90th pct (deg)
  in_rate_med   fastest turn on the way in, median over sits (deg/s)
  exits         walks out of a seat
  loop_max      largest one-way turn from standing up to 1.5 s after the hand-back (deg; >240 = loop)
  out_total_med every degree turned over the same stretch, median
  cnt_n         customers leaving the counter
  cnt_excess    turning beyond the shortest turn to the way they walk off, median / max (deg)
  cnt_wrong     first big turn went the long way round (count)
  cnt_spin      fastest turn standing, median (deg/s)
  cnt_accel     hardest speed-up, median (m/s^2)
  flick/min     walk clip off-and-on-again within 0.6 s while moving, per minute of walking
"""
import sys, os
import numpy as np, pandas as pd

sys.path.insert(0, os.path.dirname(__file__))
from seatmotion import walk_playing, yaw_rate, runs
from turns import smooth_speed, flickers, accel, shortest, heading_at


def summarise(folder):
    df = pd.read_csv(os.path.join(folder, 'trace.csv'), low_memory=False)
    p = df[df.kind.isin(['C', 'P'])].copy()
    p['sub'] = p['sub'].fillna('')
    r = dict(rec=os.path.basename(folder.rstrip('/')))
    sits, slides, kinks, sides, in_rates = 0, [], [], [], []
    loops, out_tot = [], []
    cnt = []
    flick, walk_time = 0, 0.0
    for pid, g in p.groupby('id'):
        g = g.sort_values('t').reset_index(drop=True)
        t = g.t.values
        x, z = g.x.values.astype(float), g.z.values.astype(float)
        yaw = g.yaw.values.astype(float)
        y = np.degrees(np.unwrap(np.radians(yaw)))
        sp = smooth_speed(t, x, z)
        sub = g['sub'].values
        state = g.state.values
        anim = g.anim.values
        playing = np.array([walk_playing(a) for a in anim])
        dt = np.diff(t, prepend=t[0])
        walk_time += float(dt[sp > .3].sum())
        flick += flickers(t, sp, anim)
        i = 0
        while i < len(g):
            ph = sub[i]
            j = i
            while j < len(g) and sub[j] == ph: j += 1
            if ph == 'Approaching' and j - i >= 3:
                sits += 1
                seg = slice(i, j)
                mv = sp[seg] > .25
                slides.append(runs(mv & ~playing[seg], t[seg]))
                in_rates.append(yaw_rate(t[seg], yaw[seg]))
                if i >= 3 and i + 3 < len(g):
                    v0 = np.array([x[i] - x[i - 2], z[i] - z[i - 2]])
                    v1 = np.array([x[i + 2] - x[i], z[i + 2] - z[i]])
                    n0, n1 = np.linalg.norm(v0), np.linalg.norm(v1)
                    if n0 > 1e-3 and n1 > 1e-3:
                        kinks.append(np.degrees(np.arccos(np.clip(v0 @ v1 / n0 / n1, -1, 1))))
                    offs = []
                    for k in range(i + 1, min(len(g), i + 9)):
                        vx, vz = x[k] - x[k - 1], z[k] - z[k - 1]
                        if np.hypot(vx, vz) < 1e-3: continue
                        mvd = np.degrees(np.arctan2(vx, vz))
                        offs.append(abs((yaw[k] - mvd + 180) % 360 - 180))
                    if offs: sides.append(max(offs))
            if ph == 'StandingUp':
                s0 = i
                k = i
                while k < len(g) and sub[k] in ('StandingUp', 'Returning'): k += 1
                e = k
                while e < len(g) and t[e] - t[k - 1] < 1.5: e += 1
                if e - s0 > 5:
                    cum = y[s0:e] - y[s0]
                    loops.append(float(np.abs(cum).max()))
                    out_tot.append(float(np.abs(np.diff(y[s0:e])).sum()))
                j = max(j, e)
            i = j
        if g.kind.iloc[0] == 'C':
            idx = np.where(state == 'Settling')[0]
            if len(idx):
                a = idx[0]
                b = a
                while b < len(g) and t[b] - t[a] < 7.0 and state[b] == 'Settling' and sub[b] in ('Standing', ''): b += 1
                if b - a > 10:
                    end = b - 1
                    h = heading_at(x, z, end - 3, 3)
                    if h is not None and sp[end - 3] > .4:
                        net = abs(shortest(y[a], h))
                        total = float(np.abs(np.diff(y[a:b])).sum())
                        cum = y[a:b] - y[a]
                        lim = min(60.0, net * .6)
                        big = np.abs(cum) > lim
                        first_dir = np.sign(cum[np.argmax(big)]) if big.any() else 0
                        wrong = bool(net > 30 and first_dir != 0 and first_dir != np.sign(shortest(y[a], h)))
                        st = sp[a:b] < .2
                        spin = yaw_rate(t[a:b][st], yaw[a:b][st]) if st.sum() > 3 else 0.0
                        cnt.append((total - net, wrong, spin, accel(t[a:b], sp[a:b])))
    r['sits'] = sits
    r['slide_in'] = round(max(slides), 2) if slides else None
    r['kink_p90'] = round(float(np.percentile(kinks, 90))) if kinks else None
    r['side_p90'] = round(float(np.percentile(sides, 90))) if sides else None
    r['in_rate_med'] = round(float(np.median(in_rates))) if in_rates else None
    r['exits'] = len(loops)
    r['loop_max'] = round(max(loops)) if loops else None
    r['loops>240'] = int(sum(l > 240 for l in loops))
    r['out_total_med'] = round(float(np.median(out_tot))) if out_tot else None
    r['cnt_n'] = len(cnt)
    if cnt:
        ex = [c[0] for c in cnt]
        r['cnt_excess'] = f"{np.median(ex):.0f}/{max(ex):.0f}"
        r['cnt_wrong'] = int(sum(c[1] for c in cnt))
        r['cnt_spin'] = round(float(np.median([c[2] for c in cnt])))
        r['cnt_accel'] = round(float(np.median([c[3] for c in cnt])), 1)
    r['flick/min'] = round(flick / max(walk_time / 60.0, 1e-3), 2)
    return r


if __name__ == '__main__':
    rows = [summarise(f) for f in sys.argv[1:]]
    print(pd.DataFrame(rows).to_string(index=False))
