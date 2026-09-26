"""Top-down café map (NavMesh, furniture, spots) with optional trajectories and markers.
Note on axes: plotted with x to the right and z up (north = counter)."""
import json, math
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon, Circle, Rectangle

def load_map(path):
    return json.load(open(path))

def base(m, ax, xlim=(-8.5, 8.5), zlim=(-2.8, 14.2), labels=True):
    v = np.array(m['navmesh']['vertices']); idx = np.array(m['navmesh']['indices']).reshape(-1, 3)
    for t in idx:
        ax.add_patch(Polygon(v[t][:, [0, 2]], closed=True, facecolor='#cfe8cf', edgecolor='#9fc79f', lw=0.3))
    for s in m['solids']:
        if 'corners' in s:
            c = np.array(s['corners'])[:, [0, 2]]
            ax.add_patch(Polygon(c, closed=True, facecolor='#8a6d52', alpha=0.35, edgecolor='#5a4632', lw=0.6))
        else:
            mn, mx = s['min'], s['max']
            ax.add_patch(Rectangle((mn[0], mn[2]), mx[0] - mn[0], mx[2] - mn[2], facecolor='#7a7a7a', alpha=0.25, edgecolor='#555', lw=0.5))
    for s in m['spots']:
        st = s['stand']
        if s['type'] == 'TableSeat':
            se = s['seat']
            ax.add_patch(Circle((se[0], se[2]), 0.22, facecolor='#e0a040', edgecolor='k', lw=0.4, alpha=0.9))
            ax.plot([st[0], se[0]], [st[2], se[2]], color='#b07020', lw=0.6, ls=':')
            ax.add_patch(Circle((st[0], st[2]), 0.15, facecolor='none', edgecolor='#b07020', lw=0.8))
        else:
            ax.add_patch(Circle((st[0], st[2]), 0.3, facecolor='#80a0ff' if s['active'] else 'none',
                                edgecolor='#3050c0', lw=0.8, alpha=0.6, ls='-' if s['active'] else '--'))
            if labels: ax.text(st[0], st[2] + 0.35, s['name'].split('/')[-1], fontsize=5, ha='center', color='#3050c0')
    for sl in m['slots']:
        p = sl['pos']; ax.add_patch(Rectangle((p[0] - 0.3, p[2] - 0.3), 0.6, 0.6, facecolor='#ff8080', alpha=0.5, edgecolor='r', lw=0.6))
    ex = m['points'].get('CustomerSpawner.exitPoint')
    if ex:
        ax.plot(ex[0], ex[2], marker='*', ms=10, color='crimson')
        if labels: ax.text(ex[0] + 0.25, ex[2] - 0.45, 'exit = door = spawn (0,-1.2)', fontsize=6, color='crimson')
    ax.set_xlim(*xlim); ax.set_ylim(*zlim); ax.set_aspect('equal')
    ax.set_xlabel('x (m)'); ax.set_ylabel('z (m) - counter at top')
    ax.grid(True, lw=0.2, alpha=0.5)
    return ax
