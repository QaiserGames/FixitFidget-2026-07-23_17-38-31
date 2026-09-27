"""Turning and setting-off quality from a café life trace (pass 2c).

usage: turns.py <folder-with-trace.csv> [label] [--detail]

Leaving the counter (customers, from the first Settling row until the seat's
hand-walk starts or 7 s):
  * net       - shortest turn between facing the counter and the heading they
                end up walking (degrees)
  * total     - every degree actually turned
  * excess    - total - net: the "circle": turning further than needed, or back again
  * wrong_way - the first big turn went the long way round (> 30 deg net)
  * walk_rate - fastest turn while walking (> .5 m/s), deg/s over .15 s
  * flicker   - the walk clip switched off and on again within .6 s while moving
  * accel     - hardest speed-up, m/s^2 over .25 s
Out of a chair (Returning phase and 2.5 s after the hand-back):
  * net / total / excess as above, from facing the table to the heading 2.5 s after
Whole trace: walk-clip flickers per minute of walking.
"""
import sys, os, json
import numpy as np, pandas as pd

sys.path.insert(0, os.path.dirname(__file__))
from seatmotion import walk_playing, yaw_rate


def unwrap_deg(yaw):
    return np.degrees(np.unwrap(np.radians(yaw.astype(float))))


def heading_at(x, z, i, span=6):
    j = min(len(x) - 1, i + span)
    k = max(0, i - span)
    dx, dz = x[j] - x[k], z[j] - z[k]
    if abs(dx) + abs(dz) < 1e-4: return None
    return np.degrees(np.arctan2(dx, dz))


def shortest(a, b):
    return (b - a + 180.0) % 360.0 - 180.0


def flickers(t, speed, anim):
    """Walk clip switched off and back on within .6 s while the body kept moving."""
    playing = np.array([walk_playing(a) and not str(a).startswith('Walk>Idle') for a in anim])
    n, i = 0, 1
    while i < len(t):
        if playing[i - 1] and not playing[i] and speed[i] > .2:
            j = i
            while j < len(t) and not playing[j] and t[j] - t[i] < .6: j += 1
            if j < len(t) and playing[j] and t[j] - t[i] < .6: n += 1
            i = j
        i += 1
    return n


def smooth_speed(t, x, z):
    dt = np.diff(t, prepend=t[0])
    step = np.hypot(np.diff(x, prepend=x[0]), np.diff(z, prepend=z[0]))
    sp = np.where(dt > 1e-4, step / np.maximum(dt, 1e-4), 0.0)
    return pd.Series(sp).rolling(3, center=True, min_periods=1).mean().values


def accel(t, sp, window=.25):
    best, j = 0.0, 0
    for i in range(len(t)):
        while j < i and t[i] - t[j] > window: j += 1
        if i > j and t[i] - t[j] >= window * .6:
            best = max(best, (sp[i] - sp[j]) / (t[i] - t[j]))
    return best


def main(folder, label=''):
    df = pd.read_csv(os.path.join(folder, 'trace.csv'), low_memory=False)
    p = df[df.kind.isin(['C', 'P'])].copy()
    p['sub'] = p['sub'].fillna('')
    dep, outs = [], []
    walk_time, flick = 0.0, 0
    for pid, g in p.groupby('id'):
        g = g.sort_values('t').reset_index(drop=True)
        t = g.t.values
        x, z = g.x.values.astype(float), g.z.values.astype(float)
        sp = smooth_speed(t, x, z)
        y = unwrap_deg(g.yaw.values)
        anim = g.anim.values
        sub = g['sub'].values
        state = g.state.values
        dt = np.diff(t, prepend=t[0])
        walk_time += float(dt[sp > .3].sum())
        flick += flickers(t, sp, anim)
        # ---- leaving the counter
        if g.kind.iloc[0] == 'C':
            idx = np.where(state == 'Settling')[0]
            if len(idx):
                a = idx[0]
                b = a
                while b < len(g) and t[b] - t[a] < 7.0 and state[b] == 'Settling' and sub[b] in ('Standing', ''): b += 1
                if b - a > 10:
                    seg = slice(a, b)
                    end = b - 1
                    h = heading_at(x, z, end - 3, 3)
                    if h is not None and sp[end - 3] > .4:
                        net = abs(shortest(y[a], h))
                        # yaw to heading along the unwrapped track
                        total = float(np.abs(np.diff(y[seg])).sum())
                        # first 90 deg of turning: which way?
                        cum = y[seg] - y[a]
                        k = np.argmax(np.abs(cum) > min(60.0, net * .6)) if np.any(np.abs(cum) > min(60.0, net * .6)) else None
                        first_dir = np.sign(cum[k]) if k is not None else 0
                        net_dir = np.sign(shortest(y[a], h))
                        wrong = bool(net > 30 and first_dir != 0 and first_dir != net_dir)
                        moving = sp[seg] > .5
                        wr = yaw_rate(t[seg][moving], g.yaw.values[seg][moving]) if moving.sum() > 3 else 0.0
                        dep.append(dict(name=g.name.iloc[0], t0=round(t[a], 1), net=round(net), total=round(total),
                                        excess=round(total - net), wrong_way=wrong, walk_rate=round(wr),
                                        flicker=flickers(t[seg], sp[seg], anim[seg]), accel=round(accel(t[seg], sp[seg]), 1)))
        # ---- out of a chair
        i = 0
        while i < len(g):
            if sub[i] == 'Returning':
                j = i
                while j < len(g) and sub[j] == 'Returning': j += 1
                # from the stand-up start (seat facing) to 2.5 s after the hand-back
                s0 = i
                while s0 > 0 and sub[s0 - 1] == 'StandingUp': s0 -= 1
                e = j
                while e < len(g) and t[e] - t[j - 1] < 2.5: e += 1
                e = min(e, len(g) - 1)
                h = heading_at(x, z, e - 3, 3)
                if h is not None and sp[e - 3] > .4 and e - s0 > 10:
                    net = abs(shortest(y[s0], h))
                    total = float(np.abs(np.diff(y[s0:e])).sum())
                    outs.append(dict(name=g.name.iloc[0], t0=round(t[s0], 1), net=round(net), total=round(total),
                                     excess=round(total - net)))
                i = j
            i += 1
    res = dict(label=label or os.path.basename(folder))
    d = pd.DataFrame(dep)
    if len(d):
        res['counter'] = dict(n=len(d), net_median=float(d.net.median()), total_median=float(d.total.median()),
                              excess_median=float(d.excess.median()), excess_max=float(d.excess.max()),
                              wrong_way=int(d.wrong_way.sum()), walk_rate_max=float(d.walk_rate.max()),
                              walk_rate_median=float(d.walk_rate.median()), flickers=int(d.flicker.sum()),
                              accel_median=float(d.accel.median()), accel_max=float(d.accel.max()))
    o = pd.DataFrame(outs)
    if len(o):
        res['out'] = dict(n=len(o), net_median=float(o.net.median()), total_median=float(o.total.median()),
                          excess_median=float(o.excess.median()), excess_max=float(o.excess.max()),
                          over_90_excess=int((o.excess > 90).sum()))
    res['flickers_per_walking_minute'] = round(flick / max(walk_time / 60.0, 1e-3), 2)
    res['flickers'] = flick
    print(json.dumps(res, indent=1))
    if '--detail' in sys.argv:
        print(d.to_string())
        print(o.to_string())
    return res


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    main(args[0], args[1] if len(args) > 1 else '')
