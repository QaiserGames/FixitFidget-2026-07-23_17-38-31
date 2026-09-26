"""Café life trace analysis, second pass: the metrics that matter for feel.

   stuck      wants to go somewhere (has a path, not stopped, on its feet, goal > 0.35 m away)
              but its body covers < 0.3 m in any 2 s window
   pivot      turns > 120 deg in 2 s while covering < 0.35 m (spinning / rotating in place)
   flicker    IsWalking toggles >= 4 times in 2 s (Idle<->Walk popping)
   moonwalk   walk clip while the body is still (> 0.5 s); idle clip while sliding (> 0.3 s)
   contact    two standing bodies closer than 0.5 m for > 0.4 s
   shove      Ace moved with no input
   settle     time from accepting a seat/spot to reaching it, against the straight-line distance
   leave      time from leaving to being out of the door
usage: analyse.py trace.csv [label]
"""
import sys, math, json
import numpy as np, pandas as pd

STILL_STATES = {'WaitingInQueue', 'Waiting', 'Sitting', 'Speaking'}
SEAT_SUBS = {'Approaching', 'SittingDown', 'Seated', 'StandingUp', 'Returning'}

def ang(a, b):
    return (a - b + 180.0) % 360.0 - 180.0

def load(path):
    df = pd.read_csv(path)
    df['anim_state'] = df['anim'].fillna('').str.split('@').str[0]
    return df

def people(df):
    p = df[df.kind.isin(['C', 'P']) & (df.en == 1)].copy()
    out = []
    for pid, g in p.groupby('id'):
        g = g.sort_values('t').copy()
        g['dt'] = g.t.diff()
        g['step'] = np.hypot(g.x.diff(), g.z.diff())
        g['speed'] = g.step / g.dt
        g['dyaw'] = ang(g.yaw, g.yaw.shift()).abs()
        g['goal'] = np.hypot(g.destx - g.x, g.destz - g.z)
        g['intent'] = (g.path == 1) & (g.stop == 0) & (g['sub'] == 'Standing') & (g.goal > 0.35)
        out.append(g)
    return pd.concat(out)

def windows(g, seconds=2.0):
    """For each row i: index j of the last row within `seconds` after it."""
    t = g.t.values
    j = np.searchsorted(t, t + seconds, side='right') - 1
    return j

def stuck(k):
    ev = []
    for pid, g in k.groupby('id'):
        g = g.reset_index(drop=True)
        if len(g) < 20: continue
        j = windows(g, 2.0)
        x, z, t = g.x.values, g.z.values, g.t.values
        net = np.hypot(x[j] - x, z[j] - z)
        intent = g.intent.values
        # intent for the whole window: fraction of rows with intent
        ci = np.concatenate([[0], np.cumsum(intent)])
        frac = (ci[j + 1] - ci[np.arange(len(g))]) / np.maximum(1, j - np.arange(len(g)) + 1)
        bad = (net < 0.3) & (frac > 0.8) & (t[j] - t > 1.8)
        # merge into episodes (a window that starts inside the previous episode extends it)
        spans = []
        i = 0
        while i < len(g):
            if not bad[i]: i += 1; continue
            s = i
            while i < len(g) and bad[i]: i += 1
            e = min(j[i - 1], len(g) - 1)
            if spans and t[s] <= t[spans[-1][1]]: spans[-1] = (spans[-1][0], max(e, spans[-1][1]))
            else: spans.append((s, e))
        for s, e in spans:
            row = g.iloc[s]
            ev.append(dict(name=row['name'], kind=row.kind, id=pid, t0=round(t[s], 1), t1=round(t[e], 1),
                           dur=round(t[e] - t[s], 1), state=g.state.iloc[s:e + 1].mode().iloc[0],
                           x=round(row.x, 2), z=round(row.z, 2), destx=round(row.destx, 2), destz=round(row.destz, 2),
                           maxstage=int(g.stuck.iloc[s:e + 1].max()),
                           turned=int(g.dyaw.iloc[s + 1:e + 1].sum())))
    return pd.DataFrame(ev)

def pivots(k):
    ev = []
    for pid, g in k.groupby('id'):
        g = g.reset_index(drop=True)
        if len(g) < 20: continue
        j = windows(g, 2.0)
        x, z, t = g.x.values, g.z.values, g.t.values
        seat = g['sub'].isin(SEAT_SUBS).values
        cy = np.concatenate([[0], np.cumsum(np.nan_to_num(g.dyaw.values))])
        cs = np.concatenate([[0], np.cumsum(seat)])
        last = -1
        for i in range(len(g)):
            if t[i] <= last: continue
            jj = j[i]
            if t[jj] - t[i] < 1.8: continue
            if cs[jj + 1] - cs[i] > 0: continue
            turned = cy[jj + 1] - cy[i + 1]
            net = math.hypot(x[jj] - x[i], z[jj] - z[i])
            if turned > 120 and net < 0.35:
                ev.append(dict(name=g.name.iloc[i], kind=g.kind.iloc[i], id=pid, t0=round(t[i], 1), t1=round(t[jj], 1),
                               turned=int(turned), net=round(net, 2), state=g.state.iloc[i],
                               x=round(x[i], 2), z=round(z[i], 2), speed=round(float(np.nanmean(g.speed.values[i:jj + 1])), 2)))
                last = t[jj]
    return pd.DataFrame(ev)

def flicker(k):
    ev = []
    for pid, g in k.groupby('id'):
        g = g.reset_index(drop=True)
        w = g.walk.fillna(0).values.astype(int)
        tog = np.concatenate([[0], np.abs(np.diff(w))])
        t = g.t.values
        j = windows(g, 2.0)
        ct = np.concatenate([[0], np.cumsum(tog)])
        last = -1
        for i in range(len(g)):
            if t[i] <= last: continue
            n = ct[j[i] + 1] - ct[i + 1]
            if n >= 4:
                ev.append(dict(name=g.name.iloc[i], kind=g.kind.iloc[i], t0=round(t[i], 1), t1=round(t[j[i]], 1),
                               toggles=int(n), state=g.state.iloc[i], sub=g['sub'].iloc[i],
                               speed=round(float(np.nanmean(g.speed.values[i:j[i] + 1])), 2)))
                last = t[j[i]]
    return pd.DataFrame(ev)

def runs(mask, t, min_len):
    res, start, prev = [], None, None
    for ok, tt in zip(mask, t):
        if ok and start is None: start = tt
        if not ok and start is not None:
            if prev - start >= min_len: res.append((start, prev))
            start = None
        prev = tt
    if start is not None and prev - start >= min_len: res.append((start, prev))
    return res

def moonwalk(k):
    ev = []
    for pid, g in k.groupby('id'):
        g = g.sort_values('t')
        standing = g['sub'] == 'Standing'
        walk_anim = g.anim_state.str.startswith('Walk')
        idle_anim = g.anim_state.isin(['Idle', 'Interact'])
        sp = g.speed.rolling(3, min_periods=1).mean()
        for label, mask, mn in [('walks on the spot', standing & walk_anim & (sp < 0.1), 0.5),
                                ('slides while idle', standing & idle_anim & (sp > 0.35), 0.3)]:
            for t0, t1 in runs(mask.values, g.t.values, mn):
                row = g[(g.t >= t0) & (g.t <= t1)]
                ev.append(dict(name=g.name.iloc[0], kind=g.kind.iloc[0], what=label, t0=round(t0, 1), t1=round(t1, 1),
                               dur=round(t1 - t0, 1), state=row.state.mode().iloc[0], x=round(row.x.iloc[0], 2), z=round(row.z.iloc[0], 2)))
    return pd.DataFrame(ev)

def contacts(df, threshold=0.5, min_dur=0.4):
    feet = df[df.kind.isin(['C', 'P', 'W', 'A'])].copy()
    on_feet = feet.kind.isin(['W', 'A']) | ((feet.en == 1) & (feet['sub'] == 'Standing'))
    feet = feet[on_feet]
    open_ = {}
    done = []
    for t, g in feet.groupby('t'):
        xs, zs, ids = g.x.values, g.z.values, g.id.values
        names, kinds, states = g.name.values, g.kind.values, g.state.values
        seen = set()
        n = len(g)
        for a in range(n):
            for b in range(a + 1, n):
                d = math.hypot(xs[a] - xs[b], zs[a] - zs[b])
                if d < threshold:
                    key = (min(ids[a], ids[b]), max(ids[a], ids[b]))
                    seen.add(key)
                    e = open_.get(key)
                    if e is None:
                        open_[key] = e = dict(a=names[a], ka=kinds[a], sa=states[a], b=names[b], kb=kinds[b], sb=states[b],
                                              t0=t, t1=t, dmin=d, x=round(xs[a], 2), z=round(zs[a], 2))
                    e['t1'] = t; e['dmin'] = min(e['dmin'], d)
        for key in list(open_):
            if key not in seen:
                done.append(open_.pop(key))
    done.extend(open_.values())
    out = pd.DataFrame(done)
    if len(out):
        out['dur'] = (out.t1 - out.t0).round(2)
        out = out[out.dur >= min_dur]
        out['dmin'] = out.dmin.round(2)
    return out

def shoves(df):
    a = df[df.kind == 'A'].sort_values('t').copy()
    if len(a) < 2: return pd.DataFrame()
    a['step'] = np.hypot(a.x.diff(), a.z.diff())
    a['cmd'] = np.hypot(a.vx, a.vz)
    m = (a.step > 0.02) & (a.cmd < 0.01) & (a.step < 1.0)
    res = []
    for t0, t1 in runs(m.values, a.t.values, 0.0):
        seg = a[(a.t >= t0) & (a.t <= t1)]
        res.append(dict(t0=round(t0, 1), t1=round(t1, 1), moved=round(seg.step.sum(), 2),
                        x0=round(seg.x.iloc[0], 2), z0=round(seg.z.iloc[0], 2), x1=round(seg.x.iloc[-1], 2), z1=round(seg.z.iloc[-1], 2)))
    out = pd.DataFrame(res)
    return out[out.moved > 0.1] if len(out) else out

def journeys(k):
    """Settling (customers after acceptance, patrons walking to a seat) and Leaving: time and route efficiency."""
    res = []
    for pid, g in k.groupby('id'):
        g = g.sort_values('t')
        for state in ('Settling', 'Leaving'):
            m = (g.state == state).values
            for t0, t1 in runs(m, g.t.values, 0.5):
                seg = g[(g.t >= t0) & (g.t <= t1) & (g['sub'] == 'Standing')]
                if len(seg) < 5: continue
                moving = seg[seg.stop == 0]
                if len(moving) < 3: continue
                path = moving.step.sum()
                x0, z0 = moving.x.iloc[0], moving.z.iloc[0]
                dest = (moving.destx.iloc[-1], moving.destz.iloc[-1])
                straight = math.hypot(dest[0] - x0, dest[1] - z0)
                dur = moving.t.iloc[-1] - moving.t.iloc[0]
                res.append(dict(name=g.name.iloc[0], kind=g.kind.iloc[0], state=state, t0=round(moving.t.iloc[0], 1),
                                dur=round(dur, 1), path=round(path, 1), straight=round(straight, 1),
                                pace=round(path / dur, 2) if dur > 0 else np.nan))
    return pd.DataFrame(res)

def summary(df, k, label):
    walking = k[(k['sub'] == 'Standing') & (k.speed > 0.3) & (k.dt < 0.2)]
    yr = (k[(k['sub'] == 'Standing') & (k.dt < 0.2)].dyaw / k.dt).abs()
    fr = df[df.kind == 'F'].x
    s = dict(label=label, seconds=round(df.t.max(), 1), people=int(k.id.nunique()),
             walk_speed_median=round(walking.speed.median(), 2), walk_speed_p90=round(walking.speed.quantile(.9), 2),
             yaw_rate_p99=int(yr.quantile(.99)), frame_ms_mean=round(fr.mean(), 2), frame_ms_p99=round(fr.quantile(.99), 1))
    return s

def main(path, label=''):
    df = load(path)
    k = people(df)
    pd.set_option('display.width', 260); pd.set_option('display.max_columns', 40); pd.set_option('display.max_rows', 400)
    res = dict(summary=summary(df, k, label))
    st, pv, fl, mw, ct, sh, jn = stuck(k), pivots(k), flicker(k), moonwalk(k), contacts(df), shoves(df), journeys(k)
    print('==', json.dumps(res['summary']))
    for name, table in [('STUCK (intent, < 0.3 m in 2 s)', st), ('PIVOT / SPIN (> 120 deg in 2 s, < 0.35 m)', pv),
                        ('WALK FLICKER (>= 4 toggles in 2 s)', fl), ('MOONWALK', mw), ('CONTACT (< 0.5 m, > 0.4 s)', ct),
                        ('ACE SHOVED', sh)]:
        print('\n== %s: %d' % (name, len(table)))
        if len(table): print(table.to_string())
    if len(jn):
        print('\n== JOURNEYS')
        for state, g in jn.groupby('state'):
            print('  %s: n=%d, duration median %.1f s p90 %.1f s max %.1f s; path/straight median %.2f; pace median %.2f m/s' % (
                state, len(g), g.dur.median(), g.dur.quantile(.9), g.dur.max(),
                (g.path / g.straight.clip(lower=0.5)).median(), g.pace.median()))
        print(jn.sort_values('dur', ascending=False).head(12).to_string())
    totals = dict(stuck_episodes=len(st), stuck_seconds=round(st.dur.sum(), 1) if len(st) else 0,
                  pivots=len(pv), flicker_bursts=len(fl), moonwalk=len(mw),
                  moonwalk_seconds=round(mw.dur.sum(), 1) if len(mw) else 0, contacts=len(ct),
                  contact_seconds=round(ct.dur.sum(), 1) if len(ct) else 0,
                  ace_shoved_m=round(sh.moved.sum(), 2) if len(sh) else 0)
    print('\n== TOTALS', json.dumps(totals))
    res['totals'] = totals
    return res

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else '')
