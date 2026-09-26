"""World -> screen for the recorder's fixed observer cameras (Unity: left-handed, y up, vertical fov)."""
import numpy as np
VIEWS = {
    'whole':   ((8.5, 11, -5.5), (0, 0, 7.2), 50),
    'counter': ((4.2, 5.2, 4.6), (0, .8, 12), 52),
    'tables':  ((6.5, 6.5, -3.5), (-.5, .4, 4.5), 55),
    'lounge':  ((-.8, 4.4, .8), (-6.4, .5, 5), 58),
}
def project(view, p, w=960, h=540):
    pos, tgt, fov = VIEWS[view]
    pos = np.array(pos, float); tgt = np.array(tgt, float); p = np.array(p, float)
    f = tgt - pos; f /= np.linalg.norm(f)
    up = np.array([0, 1, 0.0])
    r = np.cross(up, f); r /= np.linalg.norm(r)      # Unity: right = up x forward (left-handed)
    u = np.cross(f, r)
    d = p - pos
    x, y, z = d @ r, d @ u, d @ f
    if z <= 0: return None
    t = np.tan(np.radians(fov) / 2)
    sx = (x / (z * t * w / h)) * 0.5 + 0.5
    sy = (y / (z * t)) * 0.5 + 0.5
    return sx * w, (1 - sy) * h
if __name__ == '__main__':
    import sys
    print(project(sys.argv[1], [float(v) for v in sys.argv[2].split(',')]))
