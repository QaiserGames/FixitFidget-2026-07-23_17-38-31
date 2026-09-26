import sys, json
sys.path.insert(0, 'tools'); sys.path.insert(0, '.')
import numpy as np, pandas as pd
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
import mapplot
import analyse as analyse2

def collect(recs):
    rows = []
    for label, path in recs:
        df = analyse2.load(path); k = analyse2.people(df)
        st = analyse2.stuck(k)
        st['rec'] = label
        rows.append(st)
    return pd.concat(rows, ignore_index=True)

def plot(m, st, out, title):
    fig, ax = plt.subplots(figsize=(9, 9))
    mapplot.base(m, ax)
    for _, r in st.iterrows():
        ax.add_patch(plt.Circle((r.x, r.z), 0.12 + min(r.dur, 45) / 45 * 0.5, color='red', alpha=0.35))
        ax.annotate('', xy=(r.destx, r.destz), xytext=(r.x, r.z), arrowprops=dict(arrowstyle='->', color='red', alpha=0.25, lw=0.8))
    ax.set_title(title, fontsize=10)
    fig.savefig(out, dpi=110, bbox_inches='tight')

if __name__ == '__main__':
    m = mapplot.load_map('map.json')
    recs = [('baseline', 'rec1/trace.csv'), ('stress', 'rec2/trace.csv'), ('counter', 'rec3/trace.csv'), ('tables', 'rec4/trace.csv')]
    st = collect(recs)
    st.to_csv('before_stuck.csv', index=False)
    plot(m, st, sys.argv[1] if len(sys.argv) > 1 else 'hotspots_before.png',
         'Before: %d stuck episodes, %.0f person-seconds (circle size = duration, arrow = where they were trying to go)' % (len(st), st.dur.sum()))
    print(st.groupby('rec').dur.agg(['count', 'sum']))
