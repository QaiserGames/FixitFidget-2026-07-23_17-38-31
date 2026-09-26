"""Classify pivot events: 'arrival turn' (the agent reaches its goal / parks inside the window)
   vs 'spin' (turning while still wanting to go somewhere)."""
import sys, numpy as np, pandas as pd
sys.path.insert(0, '.')
import analyse as A

def classify(path, label):
    df = A.load(path); k = A.people(df); pv = A.pivots(k)
    rows = []
    for _, e in pv.iterrows():
        g = k[(k.id == e['id']) & (k.t >= e.t0 - 0.5) & (k.t <= e.t1 + 0.5)]
        goal_min = g.goal.min()
        parked = ((g.stop == 1) | (g.path == 0)).any()
        arrival = (goal_min < 0.6) or (parked and e.state in ('Settling', 'WalkingToCounter', 'Entering'))
        rows.append(dict(rec=label, name=e['name'], t0=e.t0, turned=e.turned, net=e.net, state=e.state, x=e.x, z=e.z,
                         goal_min=round(goal_min, 2), parked=bool(parked), kind='arrival turn' if arrival else 'SPIN'))
    return pd.DataFrame(rows)

if __name__ == '__main__':
    pd.set_option('display.width', 250)
    out = []
    for lab, p in [('rec1','rec1/trace.csv'),('rec2','rec2/trace.csv'),('rec3','rec3/trace.csv'),('rec4','rec4/trace.csv'),
                   ('after1','after1/trace.csv'),('after2','after2/trace.csv'),('after3','after3/trace.csv'),('after4','after4/trace.csv')]:
        out.append(classify(p, lab))
    out = pd.concat(out)
    print(out.to_string())
    print(out.groupby(['rec','kind']).size().unstack(fill_value=0))
