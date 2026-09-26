"""Walkers passing through seated bodies: a standing, moving body within `r` of a seated body's position."""
import sys, math, numpy as np, pandas as pd
sys.path.insert(0, '.')
import analyse as A

def seated_contacts(path, r=0.45, min_dur=0.2):
    df = A.load(path)
    p = df[df.kind.isin(['C', 'P'])].copy()
    seated = p[p['sub'].isin(['Seated', 'SittingDown', 'StandingUp'])]
    walkers = p[(p['sub'] == 'Standing')].copy()
    walkers = walkers.sort_values(['id', 't'])
    walkers['speed'] = np.hypot(walkers.groupby('id').x.diff(), walkers.groupby('id').z.diff()) / walkers.groupby('id').t.diff()
    walkers = walkers[walkers.speed > 0.3]
    open_, done = {}, []
    st = {t: g for t, g in seated.groupby('t')}
    for t, g in walkers.groupby('t'):
        s = st.get(t)
        seen = set()
        if s is not None and len(s):
            for _, w in g.iterrows():
                d = np.hypot(s.x.values - w.x, s.z.values - w.z)
                j = int(np.argmin(d))
                if d[j] < r:
                    key = (w.id, s.id.values[j]); seen.add(key)
                    e = open_.get(key)
                    if e is None: open_[key] = e = dict(walker=w['name'], seated=s.name.values[j], t0=t, t1=t, dmin=d[j], x=round(w.x, 2), z=round(w.z, 2))
                    e['t1'] = t; e['dmin'] = min(e['dmin'], d[j])
        for key in list(open_):
            if key not in seen: done.append(open_.pop(key))
    done.extend(open_.values())
    out = pd.DataFrame(done)
    if len(out):
        out['dur'] = (out.t1 - out.t0).round(2); out = out[out.dur >= min_dur]; out['dmin'] = out.dmin.round(2)
    return out

if __name__ == '__main__':
    pd.set_option('display.width', 200)
    for lab in sys.argv[1:]:
        out = seated_contacts(f'{lab}/trace.csv')
        print(f'{lab}: {len(out)} walker-through-seated contacts, {out.dur.sum() if len(out) else 0:.1f} s')
        if len(out): print(out.to_string(index=False))
