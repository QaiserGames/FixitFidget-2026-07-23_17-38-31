"""Draw grace-house-front-door-v2.png: her house front from the street, today and with the door widened to 1.30 m,
from the house's own meshes (Street doors.asset)."""
import collections
import sys

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

import plan_v2 as P
import unity_mesh as um

ASSET = sys.argv[1]
OUT = sys.argv[2]
HOUSE = "1 - Saffron bay-window house - "
PARTS = {"Street palette (doorway)": '#C9953F', "Cream trim (doorway)": '#EFE3C4', "Window glass (doorway)": '#2E4A5C',
         "Sidewalk stone (doorway)": '#A89F92'}


def islands(pts, tris):
    parent = list(range(len(pts)))

    def f(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    seen = {}
    for i, p in enumerate(pts):
        k = (round(p[0], 4), round(p[1], 4), round(p[2], 4))
        if k in seen:
            parent[f(i)] = f(seen[k])
        else:
            seen[k] = i
    for t in tris:
        a = f(t[0])
        for j in t[1:]:
            parent[f(j)] = a
    groups = collections.defaultdict(set)
    for t in tris:
        for i in t:
            groups[f(t[0])].add(i)
    out = []
    for vs in groups.values():
        xs = [pts[i][0] for i in vs]; ys = [pts[i][1] for i in vs]; zs = [pts[i][2] for i in vs]
        out.append((min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
    return out


def X(xl):
    return 2.71 - xl          # the mesh's local x to plan X (south 0 .. north 5.42)


def draw(ax, meshes, widen):
    d = 0.16 if widen else 0.0
    # the wall, up to the first floor's bays
    ax.add_patch(Rectangle((X(2.8), 0), 5.6, 4.8, facecolor=PARTS["Street palette (doorway)"], edgecolor='none', zorder=1))
    for part, colour in PARTS.items():
        pts, tris = meshes[HOUSE + part]
        for x0, x1, y0, y1, z0, z1 in islands(pts, tris):
            if z1 < -0.05 or y0 > 4.8:
                continue
            if part == "Street palette (doorway)":
                if x1 - x0 > 5 and y1 - y0 > 5:
                    continue                       # the wall itself
                if y1 - y0 < 0.05:                 # a clapboard's shadow line
                    a, b = X(x1), X(x0)
                    # the boards end at the door's jambs: follow them out when widening
                    if widen and abs(b - P.DOOR_NOW[0]) < .03:
                        b = P.DOOR[0]
                    if widen and abs(a - P.DOOR_NOW[1]) < .03:
                        a = P.DOOR[1]
                    ax.plot([a, b], [y0, y0], color='#9C7130', lw=0.7, zorder=2)
                    continue
                ax.add_patch(Rectangle((X(x1), y0), x1 - x0, y1 - y0, facecolor='#B8862F', edgecolor='#8F6624', lw=0.6, zorder=2))
                continue
            a, b = X(x1), X(x0)
            if widen and part == "Cream trim (doorway)" and abs(a - P.SURROUND_NOW[0]) < .03 and y1 < 2.7:
                a, b = P.SURROUND
            if widen and part == "Sidewalk stone (doorway)" and y1 < .2 and abs(a - P.DOOR_NOW[0]) < .03:
                a, b = P.DOOR
            ax.add_patch(Rectangle((a, y0), b - a, y1 - y0, facecolor=colour, edgecolor='#7d7468', lw=0.5, zorder=3 if part != "Window glass (doorway)" else 4))
    # the doorway: dark, and the door leaf (drawn closed) in it
    d0, d1 = (P.DOOR if widen else P.DOOR_NOW)
    ax.add_patch(Rectangle((d0, 0.15), d1 - d0, 2.19, facecolor='#284338', edgecolor='#1b2c25', lw=1.0, zorder=5))
    ax.add_patch(Rectangle((d0 + .12, 1.42), d1 - d0 - .24, .72, facecolor='#2E4A5C', edgecolor='#1b2c25', lw=0.8, zorder=6))
    ax.add_patch(Rectangle((d0 + .12, .32), d1 - d0 - .24, .9, facecolor='none', edgecolor='#1b2c25', lw=0.8, zorder=6))
    ax.plot([d1 - .12], [1.05], marker='o', color='#C79E4D', markersize=4, zorder=7)
    # the railings
    for x in P.RAILINGS:
        ax.plot([x, x], [0, 0.94], color='#2B2B2B', lw=2.0, zorder=7)
    ax.plot([P.RAILINGS[0], P.RAILINGS[0] - .02], [.94, .94], color='#2B2B2B', lw=2.0, zorder=7)
    # Ace, to scale: the capsule you'd bump with
    cx = (d0 + d1) / 2
    ax.add_patch(Rectangle((cx - P.ACE_RADIUS - P.ACE_SKIN, 0.15), 2 * (P.ACE_RADIUS + P.ACE_SKIN), P.ACE_TOP,
                           facecolor='none', edgecolor='#2F7D6D', lw=1.3, ls=(0, (3, 2)), zorder=8))
    ax.text(cx, 2.42 + (0.08 if widen else 0), f"{d1 - d0:.2f} m", ha='center', fontsize=10, weight='bold', zorder=9)
    ax.plot([-0.4, 5.9], [0, 0], color='#6d665e', lw=1.0)
    ax.set_xlim(-0.4, 5.9)
    ax.set_ylim(-0.3, 4.9)
    ax.set_aspect('equal')
    ax.axis('off')


meshes = um.load(ASSET, {HOUSE + p for p in PARTS})
fig = plt.figure(figsize=(16, 7.6), dpi=110, facecolor='#faf7f2')
fig.text(0.03, 0.93, "Her front from the street: the door, today and widened", fontsize=18, weight='bold', color='#1f1d1a')
fig.text(0.03, 0.885, "Drawn from the house's own meshes. The green dashed box is what Ace needs to pass (1.16 x 2.08 m: "
         "the 1.0 m capsule and Unity's 8 cm skin).", fontsize=10.5, color='#6d665e')
fig.text(0.03, 0.857, "Widened: the doorway 1.30 m and its cream surround 0.16 m out each side, which makes it exactly as wide "
         "as the stone steps. The door keeps its own art: its panels stretch, the knob keeps its shape.", fontsize=10.5, color='#6d665e')
ax1 = fig.add_axes([0.02, 0.03, 0.47, 0.76])
ax2 = fig.add_axes([0.51, 0.03, 0.47, 0.76])
draw(ax1, meshes, False)
draw(ax2, meshes, True)
ax1.set_title('Today: 0.98 m (Ace doesn\'t fit)', fontsize=12.5, loc='left', weight='bold')
ax2.set_title('Widened: 1.30 m (7 cm spare each side)', fontsize=12.5, loc='left', weight='bold')
fig.savefig(OUT, dpi=110, facecolor=fig.get_facecolor())
print('wrote', OUT)
