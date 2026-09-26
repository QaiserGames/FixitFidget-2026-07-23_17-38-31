"""Before/after comparison of the four café recordings (same cameras, same stress timeline)."""
import sys, os, json, math, numpy as np, pandas as pd
sys.path.insert(0, '.')
import analyse as A
from classify_pivots import classify

HOTSPOTS = {'exit point (0,-1.2)': (0.0, -1.2), 'T3/S1 stand (-1.7,4.8)': (-1.7, 4.8), 'T2/S3 stand (1.7,7.7)': (1.7, 7.7),
            'T1/S2 stand (-1.7,7.7)': (-1.7, 7.7), 'T4/S2 stand (4.3,2.3)': (4.3, 2.3)}

def pushed_back(k, min_dur=0.3):
    """Body moving against its desired direction (RVO pushing it backwards)."""
    ev = []
    for pid, g in k.groupby('id'):
        g = g.sort_values('t')
        v = np.hypot(g.vx, g.vz); dv = np.hypot(g.dvx, g.dvz)
        dot = (g.vx * g.dvx + g.vz * g.dvz) / np.maximum(v * dv, 1e-6)
        m = ((g['sub'] == 'Standing') & (v > 0.4) & (dv > 0.4) & (dot < -0.3)).values
        for t0, t1 in A.runs(m, g.t.values, min_dur):
            seg = g[(g.t >= t0) & (g.t <= t1)]
            ev.append(dict(name=g.name.iloc[0], kind=g.kind.iloc[0], t0=round(t0, 1), dur=round(t1 - t0, 1),
                           moved=round(seg.step.sum(), 2), state=seg.state.mode().iloc[0], x=round(seg.x.iloc[0], 2), z=round(seg.z.iloc[0], 2)))
    return pd.DataFrame(ev)

def yaw_jitter(k):
    """Direction reversals of the turning rate while walking: how often a body wobbles left-right."""
    reversals, walked = 0, 0.0
    for pid, g in k.groupby('id'):
        g = g.sort_values('t')
        # Turn rate between CONSECUTIVE samples first; only then keep the walking ones,
        # so a gap (standing still for a minute) never becomes a fake spike.
        rate = (A.ang(g.yaw, g.yaw.shift()) / g.dt).values
        walking = ((g['sub'] == 'Standing') & (g.speed > 0.5) & (g.dt < 0.2)).values
        walked += float(g.dt.values[walking].sum()) if walking.any() else 0.0
        for i in range(2, len(g)):
            if not (walking[i] and walking[i - 1]): continue
            a, b = rate[i - 1], rate[i]
            if np.isnan(a) or np.isnan(b): continue
            if np.sign(a) != np.sign(b) and abs(a) > 60 and abs(b) > 60: reversals += 1
    return reversals, walked, (reversals / walked * 60 if walked else float('nan'))

def door_departures(k, exit_xz=(-0.3, -1.2), r=1.2):
    """Leavers inside the door funnel at the same time (crowd at the door), and time each leaver spent inside it."""
    d = k[(k.state == 'Leaving') & (k['sub'] == 'Standing')].copy()
    d['near'] = np.hypot(d.x - exit_xz[0], d.z - exit_xz[1]) < r
    n = d[d.near].groupby('t').id.nunique()
    per = d[d.near].groupby('id').t.agg(lambda s: s.max() - s.min())
    return dict(max_simultaneous=int(n.max()) if len(n) else 0, seconds_with_3plus=round(float((n >= 3).sum()) * 0.05, 1),
                funnel_time_median=round(float(per.median()), 1) if len(per) else 0, funnel_time_max=round(float(per.max()), 1) if len(per) else 0)

def attention(df):
    """Head-look episodes (rows tagged look= in the trace) and how the queue stands."""
    c = df[df.kind == 'C'].sort_values(['id', 't'])
    look = c['info'].fillna('').str.contains('look=')
    episodes, seconds = 0, 0.0
    for pid, g in c.groupby('id'):
        l = look.loc[g.index].values
        episodes += int((l[1:] & ~l[:-1]).sum() + (1 if len(l) and l[0] else 0))
        seconds += float(l.sum()) * 0.05
    q = c[c.state == 'WaitingInQueue']
    if len(q):
        slotx = np.array([-1.6, 0.0, 1.6])
        off = np.abs(q.x.values[:, None] - slotx[None, :]).min(axis=1)
        yaw = np.abs(((q.yaw.values + 180) % 360) - 180)
        queue = dict(queue_x_offset_p90=round(float(np.quantile(off, .9)), 2), queue_yaw_p90=round(float(np.quantile(yaw, .9)), 1),
                     queue_z_offset_p90=round(float(np.quantile(np.abs(q.z.values - 12.5), .9)), 2))
    else: queue = dict(queue_x_offset_p90=0.0, queue_yaw_p90=0.0, queue_z_offset_p90=0.0)
    return dict(look_episodes=episodes, look_seconds=round(seconds, 1), **queue)

def one(path, label):
    df = A.load(path); k = A.people(df)
    st, pv, fl, mw, ct, sh, jn = A.stuck(k), A.pivots(k), A.flicker(k), A.moonwalk(k), A.contacts(df), A.shoves(df), A.journeys(k)
    pb = pushed_back(k)
    spins = classify(path, label)
    nspin = int((spins.kind == 'SPIN').sum()) if len(spins) else 0
    rev, walked, per_min = yaw_jitter(k)
    hot = {}
    for name, (hx, hz) in HOTSPOTS.items():
        if len(st):
            m = np.hypot(st.x - hx, st.z - hz) < 0.8
            hot[name] = (int(m.sum()), round(float(st.dur[m].sum()), 1))
        else: hot[name] = (0, 0.0)
    fr = df[df.kind == 'F'].x
    res = dict(rec=label, seconds=round(df.t.max(), 1), visits=int(k.id.nunique()),
               stuck_n=len(st), stuck_s=round(float(st.dur.sum()), 1) if len(st) else 0.0,
               stuck_mean=round(float(st.dur.mean()), 1) if len(st) else 0.0, stuck_worst=round(float(st.dur.max()), 1) if len(st) else 0.0,
               stuck_stage3plus=int((st.maxstage >= 3).sum()) if len(st) else 0,
               pivots=len(pv), spins=nspin, flicker=len(fl), moonwalk_s=round(float(mw.dur.sum()), 1) if len(mw) else 0.0,
               pushed_back_n=len(pb), pushed_back_m=round(float(pb.moved.sum()), 2) if len(pb) else 0.0,
               contacts=len(ct), ace_shoved_m=round(float(sh.moved.sum()), 2) if len(sh) else 0.0,
               yaw_reversals_per_min=round(per_min, 1), walk_speed_median=round(float(k[(k['sub']=='Standing')&(k.speed>0.3)&(k.dt<0.2)].speed.median()), 2),
               leave_median=round(float(jn[jn.state=='Leaving'].dur.median()), 1) if len(jn) else 0, leave_max=round(float(jn[jn.state=='Leaving'].dur.max()), 1) if len(jn) else 0,
               settle_median=round(float(jn[jn.state=='Settling'].dur.median()), 1) if len(jn) else 0, settle_max=round(float(jn[jn.state=='Settling'].dur.max()), 1) if len(jn) else 0,
               detour_median=round(float((jn.path / jn.straight.clip(lower=0.5)).median()), 2) if len(jn) else 0,
               frame_ms_mean=round(float(fr.mean()), 1), frame_ms_p99=round(float(fr.quantile(.99)), 1),
               door=door_departures(k), hotspots=hot, **attention(df))
    return res, st, pb

if __name__ == '__main__':
    pd.set_option('display.width', 250)
    # usage: compare.py [prefix ...]   (default: rec after) - each prefix names <prefix>1..4/trace.csv
    prefixes = sys.argv[1:] or ['rec', 'after']
    recs = [(f'{p}{i}', f'{p}{i}/trace.csv') for p in prefixes for i in (1, 2, 3, 4) if os.path.exists(f'{p}{i}/trace.csv')]
    allres, allst, allpb = [], [], []
    for lab, p in recs:
        r, st, pb = one(p, lab); allres.append(r)
        if len(st): st = st.assign(rec=lab); allst.append(st)
        if len(pb): pb = pb.assign(rec=lab); allpb.append(pb)
    json.dump(allres, open('results/compare.json', 'w'), indent=1)
    cols = ['rec','seconds','visits','stuck_n','stuck_s','stuck_mean','stuck_worst','stuck_stage3plus','pivots','spins','flicker','moonwalk_s','pushed_back_n','pushed_back_m','ace_shoved_m','yaw_reversals_per_min','walk_speed_median','leave_median','leave_max','settle_median','settle_max','detour_median','frame_ms_mean','frame_ms_p99']
    t = pd.DataFrame(allres)[cols]
    print(t.to_string(index=False))
    print('\nDOOR', pd.DataFrame([dict(rec=r['rec'], **r['door']) for r in allres]).to_string(index=False))
    print('\nATTENTION / QUEUE', pd.DataFrame([dict(rec=r['rec'], **{k: r[k] for k in ('look_episodes','look_seconds','queue_x_offset_p90','queue_z_offset_p90','queue_yaw_p90')}) for r in allres]).to_string(index=False))
    print('\nHOTSPOTS (episodes, seconds)')
    print(pd.DataFrame([dict(rec=r['rec'], **{k: v for k, v in r['hotspots'].items()}) for r in allres]).to_string(index=False))
    if allst:
        st = pd.concat(allst); st.to_csv('results/stuck_all.csv', index=False)
        print('\nSTUCK (all but the baseline)'); print(st[~st.rec.str.startswith('rec')].to_string(index=False))
    if allpb:
        pb = pd.concat(allpb); pb.to_csv('results/pushed_back.csv', index=False)
        print('\nPUSHED BACK'); print(pb.groupby('rec').agg(n=('dur','size'), s=('dur','sum'), m=('moved','sum')).to_string())
        print(pb[~pb.rec.str.startswith('rec')].to_string(index=False))
