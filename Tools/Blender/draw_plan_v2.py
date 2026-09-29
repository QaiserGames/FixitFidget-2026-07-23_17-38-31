"""Draw grace-house-plan-v2.png: both floors of layout v2, with Ace's real footprint along the way."""
import math
import sys

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle, Wedge, Polygon, Arc, FancyArrowPatch

import plan_v2 as P

INK = '#1f1d1a'
WALL = '#2b2825'
FLOOR = '#f6efe4'
KITCHEN = '#eef2ee'
FURN = '#cfc9c1'
FURN_EDGE = '#8d857c'
HER = '#c4a283'
PHOTO = '#e0843a'
GLASS = '#7aa7c7'
ACE = '#2f7d6d'
STAIR = '#e3dcd0'
MUTED = '#6d665e'

OUT = sys.argv[1] if len(sys.argv) > 1 else 'grace-house-plan-v2.png'


def uv(X, Y):
    """Plan (X north, Y toward the street) to drawing (u right, v up): the street at the bottom."""
    return X, -Y


def rect(ax, x0, x1, y0, y1, fc, ec=None, lw=0.8, ls='-', z=2, alpha=1.0, hatch=None):
    u0, v0 = uv(x0, y1)
    ax.add_patch(Rectangle((u0, v0), x1 - x0, y1 - y0, facecolor=fc, edgecolor=ec or fc, lw=lw, ls=ls, zorder=z,
                           alpha=alpha, hatch=hatch))


def label(ax, X, Y, text, size=8.5, color=INK, weight='normal', ha='center', va='center', z=9, rot=0, style='normal'):
    u, v = uv(X, Y)
    ax.text(u, v, text, fontsize=size, color=color, weight=weight, ha=ha, va=va, zorder=z, rotation=rot,
            style=style)


def piece_box(name, X, Y, w, d, turn):
    """Footprint of a piece (w across its front, d deep) at its origin, turned (0 faces the street)."""
    if turn % 180 == 0:
        return X - w / 2, X + w / 2, Y - d / 2, Y + d / 2
    return X - d / 2, X + d / 2, Y - w / 2, Y + w / 2


def ace(ax, X, Y, n=None, alpha=0.9):
    u, v = uv(X, Y)
    ax.add_patch(Circle((u, v), P.ACE_RADIUS + P.ACE_SKIN, facecolor='none', edgecolor=ACE, lw=1.0, ls=(0, (3, 2)),
                        zorder=7, alpha=alpha))
    ax.add_patch(Circle((u, v), P.ACE_RADIUS, facecolor=ACE, edgecolor=ACE, lw=0.8, zorder=7, alpha=0.18))
    if n is not None:
        ax.text(u, v, str(n), fontsize=8, color=ACE, weight='bold', ha='center', va='center', zorder=8)


def route(ax, pts):
    us = [uv(x, y) for x, y in pts]
    ax.plot([p[0] for p in us], [p[1] for p in us], color=ACE, lw=1.4, ls=(0, (1, 2)), zorder=6)


def shell(ax, first=False):
    t_out = P.LINING
    # floor
    rect(ax, 0, P.W, 0, P.D, FLOOR, z=0)
    # walls: outer face to inner face
    rect(ax, -t_out, P.W + t_out, -t_out, 0, WALL, z=3)                  # back
    rect(ax, -t_out, 0, -t_out, P.D + t_out, WALL, z=3)                  # south
    rect(ax, P.W, P.W + t_out, -t_out, P.D + t_out, WALL, z=3)           # north (party wall)
    if not first:
        # street wall with the door and the window
        rect(ax, -t_out, P.DOOR[0], P.D, P.D + t_out, WALL, z=3)
        rect(ax, P.DOOR[1], P.W + t_out, P.D, P.D + t_out, WALL, z=3)
        rect(ax, P.WINDOW[0], P.WINDOW[1], P.D + 0.02, P.D + t_out - 0.02, GLASS, z=4)
        # the new door leaf, swung open into the house (hinge on the north side)
        hx, hy = uv(P.DOOR_HINGE_X, P.D)
        w = P.DOOR[1] - P.DOOR[0]
        ax.add_patch(Arc((hx, hy), 2 * w, 2 * w, theta1=90, theta2=180, color=MUTED, lw=0.8, ls=(0, (2, 2)), zorder=4))
        ax.plot([hx - 0.02, hx - 0.02], [hy, hy + w], color='#5a4636', lw=2.2, zorder=5)
        # the stoop outside
        rect(ax, P.STOOP[0], P.STOOP[1], P.D + t_out, P.D + t_out + 0.8, '#d9d2c6', ec='#a79f93', z=1)
        label(ax, (P.STOOP[0] + P.STOOP[1]) / 2, P.D + t_out + 0.33, 'front door', size=8, weight='bold')
        label(ax, (P.STOOP[0] + P.STOOP[1]) / 2, P.D + t_out + 0.58, '0.98 m today, 1.30 m', size=7.5, color=MUTED)
    else:
        rect(ax, -t_out, P.W + t_out, P.D, P.D + t_out, WALL, z=3)
        for bx0, bx1 in P.BAYS:
            rect(ax, bx0, bx1, P.D, P.BAY_Y[1], FLOOR, z=3.1)
            rect(ax, bx0 - 0.08, bx0, P.D, P.BAY_Y[1] + 0.08, WALL, z=3)
            rect(ax, bx1, bx1 + 0.08, P.D, P.BAY_Y[1] + 0.08, WALL, z=3)
            rect(ax, bx0, bx1, P.BAY_Y[1], P.BAY_Y[1] + 0.08, WALL, z=3)
            rect(ax, bx0 + 0.12, bx1 - 0.12, P.BAY_Y[1] + 0.015, P.BAY_Y[1] + 0.065, GLASS, z=4)


def stairs(ax, faint=False, first=False):
    a = 0.45 if faint else 1.0
    c = P.CLEAR
    # landing, lower flight, upper flight
    rect(ax, 0, c, 0, c, STAIR, ec=FURN_EDGE, z=2, alpha=a)
    rect(ax, 0, c, c, P.LOWER_BOTTOM_Y, STAIR, ec=FURN_EDGE, z=2, alpha=a)
    rect(ax, c, P.UPPER_TOP_X, 0, c, STAIR, ec=FURN_EDGE, z=2, alpha=a)
    for i in range(1, 6):
        y = c + i * P.GOING
        ax.plot([0, c], [-y, -y], color=FURN_EDGE, lw=0.6, zorder=3, alpha=a)
        x = c + i * P.GOING
        ax.plot([x, x], [0, -c], color=FURN_EDGE, lw=0.6, zorder=3, alpha=a)
    # banisters
    rect(ax, c, c + P.BAL, c, P.LOWER_BOTTOM_Y + 0.03, '#5a4636', z=3, alpha=a)
    rect(ax, c, P.UPPER_TOP_X + 0.03, c, c + P.BAL, '#5a4636', z=3, alpha=a)
    for X, Y in ((c + .03, P.LOWER_BOTTOM_Y + .03), (c + .03, c + .03), (P.UPPER_TOP_X + .03, c + .03)):
        u, v = uv(X, Y)
        ax.add_patch(Rectangle((u - .06, v - .06), .12, .12, facecolor='#4a3422', zorder=3.5, alpha=a))
    if not faint:
        # the way up
        arr = [(0.7, 2.45), (0.7, 0.7), (2.45, 0.7)]
        us = [uv(*p) for p in arr]
        ax.annotate('', xy=us[1], xytext=us[0], arrowprops=dict(arrowstyle='-', color=MUTED, lw=1.0), zorder=4)
        ax.annotate('', xy=us[2], xytext=us[1], arrowprops=dict(arrowstyle='->', color=MUTED, lw=1.0), zorder=4)
        label(ax, 0.7, 2.05, 'up', size=8, color=MUTED)
        label(ax, 0.7, 0.35, 'landing\n1.2 m up', size=6.5, color=MUTED)
        # the cupboard under the upper flight and the landing: its door faces the pocket
        rect(ax, 0.02, P.UPPER_TOP_X - 0.02, 0.02, c - 0.02, 'none', ec='#9b6a3c', lw=1.0, ls=(0, (3, 2)), z=3.6)
        d0, d1 = P.CUPBOARD_DOOR
        rect(ax, d0, d1, c - 0.03, c + 0.03, '#9b6a3c', z=4)
        label(ax, (d0 + d1) / 2, c - 0.25, 'cupboard\n(hide)', size=7, color='#9b6a3c', weight='bold')


def ground(ax):
    shell(ax)
    rect(ax, 2.60, P.W, 0, 2.06, KITCHEN, z=0.5)                # the kitchen's floor
    stairs(ax)
    G = P.GROUND
    sizes = {
        'GH_KitchenCounter': (2.0, .62), 'GH_Fridge': (.62, .69), 'GH_TVCabinet': (1.05, .49),
        'GH_Armchair': (.806, .902), 'GH_Sideboard': (1.39, .508), 'GH_Rug': (2.1, 1.4),
    }
    X, Y, _, t = G['GH_Rug']
    x0, x1, y0, y1 = piece_box('rug', X, Y, 2.1, 1.4, t)
    rect(ax, x0, x1, y0, y1, '#e8d2c9', ec='#b98f83', z=1, lw=0.6)
    for name, (w, d) in sizes.items():
        if name == 'GH_Rug':
            continue
        X, Y, _, t = G[name]
        x0, x1, y0, y1 = piece_box(name, X, Y, w, d, t)
        rect(ax, x0, x1, y0, y1, HER if name == 'GH_Armchair' else FURN, ec=FURN_EDGE, z=5)
    # wall cupboards (above), the photos on the sideboard, the lamp, the coat stand
    rect(ax, 2.62, 4.66, 0.02, 0.36, 'none', ec=FURN_EDGE, ls=(0, (2, 2)), z=5.5)
    rect(ax, 2.95, 4.25, 3.52, 3.60, PHOTO, z=5.6)
    rect(ax, P.W - 0.05, P.W, 2.2, 3.45, PHOTO, z=5.6)
    rect(ax, 0.0, 0.05, 1.65, 2.45, PHOTO, z=5.6)
    for X, Y, r, c in ((3.25, 3.55, .2, '#f1dfb8'), (2.15, 3.78, .2, FURN)):
        u, v = uv(X, Y)
        ax.add_patch(Circle((u, v), r, facecolor=c, edgecolor=FURN_EDGE, lw=0.8, zorder=5))
    label(ax, 3.62, 0.31, 'counter: sink, hob, kettle', size=7.5)
    label(ax, 5.03, 0.345, 'fridge', size=7)
    label(ax, 5.175, 2.83, 'TV', size=7.5, rot=90)
    label(ax, 4.0, 2.83, 'her\narmchair', size=7.5)
    label(ax, 3.6, 3.83, 'sideboard + photos', size=7)
    label(ax, 3.25, 3.1, 'lamp', size=6.5, color=MUTED)
    label(ax, 2.15, 3.3, 'coats', size=6.5, color=MUTED)
    # the stash
    u, v = uv(2.95, 0.33)
    ax.add_patch(Polygon([(u + .16 * math.cos(math.radians(90 + 72 * k)), v + .16 * math.sin(math.radians(90 + 72 * k)))
                          for k in range(5)], closed=True, facecolor='#f2a531', edgecolor=INK, lw=0.8, zorder=8))
    label(ax, 2.95, 1.0, 'the reunion\ncups', size=7.5, weight='bold')
    # her TV view: 60 degrees toward the screen
    u, v = uv(4.12, 2.83)
    ax.add_patch(Wedge((u, v), 1.5, -30, 30, facecolor='#f6d79c', edgecolor='none', alpha=0.55, zorder=4))
    # room names
    label(ax, 3.9, 1.55, 'KITCHEN', size=10, color=MUTED, weight='bold')
    label(ax, 4.55, 3.55, 'FRONT ROOM', size=10, color=MUTED, weight='bold')
    label(ax, 0.9, 3.5, 'ENTRY', size=10, color=MUTED, weight='bold')
    # Ace: in the door, at the foot of the stairs, in the pocket, at the cups, behind her chair
    route(ax, [(1.11, 4.5), (1.11, 3.35), (2.08, 2.05), (2.95, 1.30)])
    for n, (X, Y) in enumerate([(1.11, 3.35), (2.08, 2.05), (2.95, 1.30)], 1):
        ace(ax, X, Y, n)
    ace(ax, 0.70, 3.25, 'S', alpha=0.55)


def first(ax):
    shell(ax, first=True)
    # the stairwell: the flights below, faint; the void hatched
    stairs(ax, faint=True, first=True)
    for x0, x1, y0, y1 in P.VOID:
        rect(ax, x0, x1, y0, y1, 'none', ec='#b3aa9e', lw=0.0, z=2.5, hatch='////')
    # the upper flight's last treads arriving
    for x0, x1, y0, y1 in P.F1_WALLS:
        rect(ax, x0, x1, y0, y1, WALL, z=3)
    # the bathroom door (a normal door, closed)
    rect(ax, 4.0, 4.1, P.BATHROOM_DOOR_Y[0], P.BATHROOM_DOOR_Y[1], '#b7a58f', z=3.5)
    rect(ax, 4.1, P.W, 0, 1.46, '#ece7df', z=0.5)
    label(ax, 4.76, 0.73, 'bathroom\n(closed)', size=7.5, color=MUTED)
    label(ax, 3.3, 0.55, 'LANDING', size=9, color=MUTED, weight='bold')
    label(ax, 0.73, 1.9, 'stairwell', size=7.5, color=MUTED, style='italic')
    # bedroom furniture
    F = P.FIRST
    X, Y, _, t = F['Bedroom rug']
    x0, x1, y0, y1 = piece_box('r', X, Y, 2.1, 1.4, t)
    rect(ax, x0, x1, y0, y1, '#dfe3d6', ec='#9aa58f', z=1, lw=0.6)
    boxes = {'Bed': (1.4, 2.0), 'Bedside table (back)': (.43, .45), 'Bedside table (front)': (.40, .45),
             'Wardrobe': (.95, .60), 'Dressing table': (1.1, .45)}
    for name, (w, d) in boxes.items():
        X, Y, _, t = F[name]
        x0, x1, y0, y1 = piece_box(name, X, Y, w, d, t)
        rect(ax, x0, x1, y0, y1, '#d6cfe0' if name == 'Bed' else FURN, ec=FURN_EDGE, z=5)
    # pillows and the quilt line
    rect(ax, 5.0, 5.36, 2.28, 2.86, '#f4f1ea', ec=FURN_EDGE, z=5.5)
    rect(ax, 5.0, 5.36, 2.94, 3.52, '#f4f1ea', ec=FURN_EDGE, z=5.5)
    ax.plot([4.75, 4.75], [-2.2, -3.6], color=FURN_EDGE, lw=0.8, zorder=5.5)
    u, v = uv(5.195, 1.935)
    ax.add_patch(Circle((u, v), .11, facecolor='#f1dfb8', edgecolor=FURN_EDGE, lw=0.8, zorder=6))
    rect(ax, 0.86, 1.86, 4.80, 4.84, PHOTO, z=5.6)          # the photos over the dressing table
    rect(ax, 1.56, 1.61, 2.25, 2.85, PHOTO, z=5.6)
    rect(ax, 4.10, 4.85, 1.56, 1.61, PHOTO, z=5.6)
    label(ax, 4.42, 2.9, 'her bed', size=8)
    label(ax, 2.075, 1.86, 'wardrobe\n(hide)', size=7, weight='bold', color='#9b6a3c')
    label(ax, 1.36, 4.55, 'dressing table', size=6.8)
    label(ax, 3.1, 3.85, 'BEDROOM', size=10, color=MUTED, weight='bold')
    label(ax, 1.36, 5.25, 'the photos\n(her secret?)', size=7.5, color=PHOTO, weight='bold')
    # the bedroom's opening
    label(ax, 3.3, 1.78, 'opening 1.40 m', size=6.5, color=MUTED, z=10)
    # Ace: arriving, in the doorway, at the photos
    route(ax, [(2.1, 0.72), (3.25, 0.72), (3.30, 2.25), (2.2, 3.2), (1.36, 3.62)])
    for n, (X, Y) in enumerate([(3.25, 0.72), (3.30, 2.25), (1.36, 3.62)], 4):
        ace(ax, X, Y, n)


def frame_axes(ax, title):
    ax.set_aspect('equal')
    ax.set_xlim(-0.55, P.W + 0.55)
    ax.set_ylim(-(P.BAY_Y[1] + 0.95), 0.55)
    ax.axis('off')
    ax.set_title(title, fontsize=12.5, loc='left', color=INK, weight='bold', pad=6)
    # scale bar and dimensions
    ax.plot([0, 1], [0.32, 0.32], color=INK, lw=1.2)
    ax.plot([0, 0], [0.26, 0.38], color=INK, lw=1.2)
    ax.plot([1, 1], [0.26, 0.38], color=INK, lw=1.2)
    ax.text(0.5, 0.42, '1 m', fontsize=7.5, ha='center', color=INK)
    ax.text(P.W, 0.35, 'north ->', fontsize=7.5, ha='right', color=MUTED)
    ax.text(P.W + 0.2, -P.D / 2, '4.02 m inside', fontsize=7.5, rotation=90, va='center', color=MUTED)
    ax.text(P.W / 2, 0.35, '5.42 m inside', fontsize=7.5, ha='center', color=MUTED)


def main():
    fig = plt.figure(figsize=(19.5, 10.6), dpi=110, facecolor='#faf7f2')
    fig.text(0.035, 0.955, "Grace's house, layout v2: built round a 1.0 m wide Ace", fontsize=20, weight='bold', color=INK)
    fig.text(0.035, 0.925, "12 West Street. The street is at the bottom. Measured from the house's own mesh: 5.42 x 4.02 m inside "
             "each floor (the overnight plan's 4.7 m depth was the bounding box, bays and stoop included).",
             fontsize=10.5, color=MUTED)
    fig.text(0.035, 0.905, "Green rings: Ace. The dashed ring is what has to fit (1.16 m: the 1.0 m capsule plus Unity's 8 cm skin). "
             "Grey: furniture already built. Lilac and the stand-ins upstairs: new pieces. Orange: photos.",
             fontsize=10.5, color=MUTED)
    ax1 = fig.add_axes([0.02, 0.06, 0.36, 0.80], facecolor='#faf7f2')
    ax2 = fig.add_axes([0.37, 0.06, 0.36, 0.80], facecolor='#faf7f2')
    ground(ax1)
    frame_axes(ax1, 'Ground floor: open plan')
    first(ax2)
    frame_axes(ax2, 'First floor: her bedroom across the front')
    notes = [
        ("Why it changed", [
            "Ace needs 1.16 m to pass and 2.08 m of height.",
            "A hall beside a straight stair needs 1.4 + 1.4 m,",
            "half the house's width. So: no hall, an open",
            "ground floor, and a stair that turns a corner.",
        ]),
        ("The way through", [
            "1  in at the front door (widened to 1.30 m)",
            "2  the pocket by the stairs: the cupboard to hide in",
            "3  the cups on the worktop, behind her back",
            "S  the stairs: 1.40 m flights, a landing at 1.2 m",
            "4  the landing   5  her door   6  the photos",
        ]),
        ("Her night (placeholder times)", [f"{t:>6}  {w}" for t, w in P.ROUTINE]),
        ("What doesn't fit any more", [
            "the sofa, the coffee table, the kitchen table",
            "and its two chairs (a 1.4 m lane each side)",
            "The counter loses one cupboard: 2.0 m.",
        ]),
    ]
    y = 0.84
    for head, lines in notes:
        fig.text(0.745, y, head, fontsize=12, weight='bold', color=INK)
        y -= 0.03
        for line in lines:
            fig.text(0.745, y, line, fontsize=9.6, color=INK if head != "Why it changed" else MUTED,
                     family='DejaVu Sans Mono' if head.startswith('Her') else None)
            y -= 0.024
        y -= 0.02
    fig.savefig(OUT, dpi=110, facecolor=fig.get_facecolor())
    print('wrote', OUT)


if __name__ == '__main__':
    main()
