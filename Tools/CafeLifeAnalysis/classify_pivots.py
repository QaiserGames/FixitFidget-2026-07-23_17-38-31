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
    import os
    prefixes = sys.argv[1:] or ['rec', 'after']
    out = []
    for lab, p in [(f'{pre}{i}', f'{pre}{i}/trace.csv') for pre in prefixes for i in (1, 2, 3, 4)]:
        if os.path.exists(p): out.append(classify(p, lab))
    out = pd.concat(out)
    print(out.to_string())
    print(out.groupby(['rec','kind']).size().unstack(fill_value=0))
