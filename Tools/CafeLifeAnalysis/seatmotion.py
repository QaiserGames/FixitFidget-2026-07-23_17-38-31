"""Seat and counter motion from a café life trace (pass 2b).

usage: seatmotion.py <folder-with-trace.csv> [label]

Per walk into a seat (NpcSeating Approaching) and out of one (Returning):
  * walk share   - share of moving time with the Walk clip playing (or blending in)
  * slide        - longest stretch moving > 0.25 m/s with no walk clip (the "hover")
  * turned       - total degrees turned; worst turn rate (deg/s, over 0.15 s)
  * sit swivel   - degrees turned during SittingDown (the finish of the turn)
Per customer leaving the counter (first 6 s of Settling, before any seat step):
  * spin         - fastest turn while standing (< 0.2 m/s), deg/s over 0.15 s
  * stops        - dips below 0.35 m/s after having walked > 0.8 m/s, before the seat
  * backwards    - seconds moving > 0.4 m/s more than 110 deg off the facing
"""
import sys, os, json
import numpy as np, pandas as pd


def walk_playing(anim):
    a = anim if isinstance(anim, str) else ''
    state = a.split('@')[0]
    return state.startswith('Walk') or '>Walk' in state


def yaw_rate(t, yaw, window=.15):
    """Worst sustained turn rate: yaw change over ~window seconds."""
    if len(t) < 2: return 0.0
    y = np.unwrap(np.radians(yaw.astype(float)))
    worst = 0.0
    j = 0
    for i in range(len(t)):
        while j < i and t[i] - t[j] > window: j += 1
        if i > j and t[i] - t[j] >= window * .6:
            worst = max(worst, abs(np.degrees(y[i] - y[j])) / (t[i] - t[j]))
    return worst


def turned(yaw):
    y = np.unwrap(np.radians(yaw.astype(float)))
    return float(np.degrees(np.abs(np.diff(y)).sum())) if len(y) > 1 else 0.0


def runs(mask, t):
    best, start = 0.0, None
    for i, m in enumerate(mask):
        if m and start is None: start = t[i]
        if not m and start is not None: best = max(best, t[i] - start); start = None
    if start is not None: best = max(best, t[-1] - start)
    return best


def main(folder, label=''):
    df = pd.read_csv(os.path.join(folder, 'trace.csv'), low_memory=False)
    p = df[df.kind.isin(['C', 'P'])].copy()
    p['info'] = p['info'].fillna('')
    walks_in, walks_out, swivels, departures = [], [], [], []
    for pid, g in p.groupby('id'):
        g = g.sort_values('t').reset_index(drop=True)
        t = g.t.values
        x, z = g.x.values.astype(float), g.z.values.astype(float)
        dt = np.diff(t, prepend=t[0])
        step = np.hypot(np.diff(x, prepend=x[0]), np.diff(z, prepend=z[0]))
        speed = np.where(dt > 1e-4, step / np.maximum(dt, 1e-4), 0.0)
        speed = pd.Series(speed).rolling(3, center=True, min_periods=1).mean().values
        sub = g['sub'].fillna('').values
        playing = np.array([walk_playing(a) for a in g.anim.values])
        # Episodes of each seating phase.
        i = 0
        while i < len(g):
            ph = sub[i]
            j = i
            while j < len(g) and sub[j] == ph: j += 1
            seg = slice(i, j)
            if ph in ('Approaching', 'Returning') and j - i >= 3:
                mv = speed[seg] > .25
                dur = t[j - 1] - t[i]
                moving_time = float(dt[seg][mv].sum())
                share = float(dt[seg][mv & playing[seg]].sum() / moving_time) if moving_time > 0 else float('nan')
                rec = dict(name=g.name.iloc[0], kind=g.kind.iloc[0], t0=round(t[i], 1), dur=round(dur, 2),
                           walk_share=round(share, 2), slide=round(runs(mv & ~playing[seg], t[seg]), 2),
                           turned=round(turned(g.yaw.values[seg]), 0), worst_rate=round(yaw_rate(t[seg], g.yaw.values[seg]), 0))
                (walks_in if ph == 'Approaching' else walks_out).append(rec)
            if ph == 'SittingDown' and j - i >= 3:
                swivels.append(round(turned(g.yaw.values[seg]), 0))
            i = j
        # Leaving the counter (customers): from the first Settling row.
        if g.kind.iloc[0] == 'C':
            idx = np.where(g.state.values == 'Settling')[0]
            if len(idx):
                a = idx[0]
                b = a
                while b < len(g) and t[b] - t[a] < 6.0 and sub[b] in ('Standing', '') and g.state.values[b] == 'Settling': b += 1
                seg = slice(a, b)
                st = speed[seg] < .2
                spin = yaw_rate(t[seg][st], g.yaw.values[seg][st]) if st.sum() > 3 else 0.0
                walked_fast, stops = False, 0
                prev_low = False
                for k in range(a, b):
                    if speed[k] > .8: walked_fast = True
                    low = walked_fast and speed[k] < .35
                    if low and not prev_low: stops += 1
                    prev_low = low
                # A stop at the very end is arriving (or the seat hand-over), not a stop.
                if prev_low and stops: stops -= 1
                yaw = np.radians(g.yaw.values[seg].astype(float))
                fx, fz = np.sin(yaw), np.cos(yaw)
                vx = np.gradient(x[seg], t[seg]) if b - a > 2 else np.zeros(b - a)
                vz = np.gradient(z[seg], t[seg]) if b - a > 2 else np.zeros(b - a)
                sp = np.hypot(vx, vz)
                cosang = (vx * fx + vz * fz) / np.maximum(sp, 1e-6)
                back = (sp > .4) & (cosang < np.cos(np.radians(110)))
                departures.append(dict(name=g.name.iloc[0], t0=round(t[a], 1), spin=round(spin, 0), stops=stops,
                                       backwards=round(float(dt[seg][back].sum()), 2),
                                       stepped_while_turning=round(float(np.mean(playing[seg][st])) if st.sum() else float('nan'), 2)))
    res = dict(label=label or os.path.basename(folder))
    for key, rows in (('in', walks_in), ('out', walks_out)):
        d = pd.DataFrame(rows)
        if len(d):
            res[key] = dict(n=len(d), walk_share_median=float(d.walk_share.median()), walk_share_min=float(d.walk_share.min()),
                            slide_worst=float(d.slide.max()), slide_median=float(d.slide.median()),
                            turned_median=float(d.turned.median()), turned_max=float(d.turned.max()),
                            worst_rate_median=float(d.worst_rate.median()), worst_rate_max=float(d.worst_rate.max()),
                            dur_median=float(d.dur.median()))
    if swivels: res['sit_swivel'] = dict(median=float(np.median(swivels)), max=float(np.max(swivels)))
    d = pd.DataFrame(departures)
    if len(d):
        res['counter'] = dict(n=len(d), spin_median=float(d.spin.median()), spin_max=float(d.spin.max()),
                              stops_total=int(d.stops.sum()), people_with_stops=int((d.stops > 0).sum()),
                              backwards_total_s=float(d.backwards.sum()),
                              stepped_while_turning=float(d.stepped_while_turning.mean(skipna=True)) if d.stepped_while_turning.notna().any() else None)
    print(json.dumps(res, indent=1))
    if '--detail' in sys.argv:
        print(pd.DataFrame(walks_in).to_string()); print(pd.DataFrame(walks_out).to_string()); print(d.to_string())
    return res


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    main(args[0], args[1] if len(args) > 1 else '')
