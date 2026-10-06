"""
Barnaby, Grace's garden gnome (the Night 1 slice), built with Python in Blender 5.2.

Run in Blender (Scripting tab: open this file, Run Script; fixit_blender.py must sit beside it) or
headless: blender -b --python barnaby.py -- <out folder>

His look is the placeholder's, made properly: a glazed gnome that has stood on a front step for
twenty years (red hat, blue coat, white beard, brown boots, a brass buckle, a mossy stone). Nothing
new about him is canon: the look is still a placeholder (claude/night-1-slice.md section 5).

The envelope is the one the Night 1 set-up step checked on Grace's top step: no wider than 0.24 m
(the stone is 0.23 across) and no taller than 0.56 m. His front faces -Y (+Z in Unity, the street).
Under 1,000 triangles, flat-shaded, one mesh with one material per colour. The material names are
the Unity materials' names ("Night 1 - hat" ...), so the importer maps them one to one.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402

# The placeholder's colours (NightOneSteps.Palette, sRGB), plus moss for the stone.
PALETTE = {
    'hat':   ('#B83026', 0.42),
    'coat':  ('#365C99', 0.42),
    'skin':  ('#F0C29E', 0.46),
    'beard': ('#F0EDE3', 0.50),
    'boots': ('#4D301C', 0.55),
    'stone': ('#737A69', 0.85),
    'nose':  ('#EB9987', 0.44),
    'eyes':  ('#14141A', 0.30),
    'brass': ('#C79E4D', 0.35),
    'moss':  ('#56703A', 0.90),
}


def mats():
    out = {}
    for key, (hexstr, rough) in PALETTE.items():
        out[key] = fb.material('Night 1 - ' + key, hexstr, rough=rough, metal=0.6 if key == 'brass' else 0.0)
    return out


def rock(sides=9, seed=7):
    """The stone he stands on: an irregular slab, 0.23 m across, domed a little on top."""
    import bmesh
    import random
    rnd = random.Random(seed)
    bm = bmesh.new()
    jit = [1.0 + rnd.uniform(-0.05, 0.05) for _ in range(sides)]
    rings = [(0.000, 0.108), (0.022, 0.112), (0.040, 0.102)]
    loops = []
    for z, r in rings:
        loop = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + 0.2
            rr = r * jit[i]
            loop.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z + rnd.uniform(-0.002, 0.002) * (z > 0))))
        loops.append(loop)
    for lo, hi in zip(loops, loops[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bm.faces.new(list(reversed(loops[0])))
    top = bm.verts.new((0.0, 0.0, 0.047))
    for i in range(sides):
        bm.faces.new([loops[-1][i], loops[-1][(i + 1) % sides], top])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def build():
    m = mats()
    p = fb.Prop('Barnaby')

    # --- the stone, with moss on part of its rim
    faces = p.add('stone', rock(), m['stone'])
    moss = p.slot(m['moss'])
    for f in faces:
        c = f.calc_center_median()
        ang = math.degrees(math.atan2(c.y, c.x)) % 360
        # the top's front-left and back-right wedges, and the upper sides below them
        mossy = (100 <= ang <= 175) or (285 <= ang <= 340)
        if mossy and c.z > 0.018:
            f.material_index = moss

    # --- boots, toes out front
    for side in (-1, 1):
        p.add('boot', fb.ellipsoid((side * 0.038, -0.046, 0.057), (0.031, 0.050, 0.025), u=6, v=4,
                                   rot=(0, 0, side * -14)), m['boots'])

    # --- the coat: a round body, flared at the hem, widest at the belly
    p.add('coat', fb.lathe([(0.080, 0.050), (0.091, 0.062), (0.095, 0.140), (0.088, 0.182),
                            (0.070, 0.222), (0.046, 0.256)], sides=10, phase=18), m['coat'])
    # the belt and its buckle
    p.add('belt', fb.lathe([(0.0975, 0.146), (0.0995, 0.156), (0.0955, 0.167)], sides=10, phase=18), m['boots'])
    p.add('buckle', fb.zbox(-0.018, 0.018, -0.104, -0.094, 0.144, 0.170), m['brass'])

    # --- arms: shoulder, elbow out to the side, hands on the belt either side of the buckle
    for side in (-1, 1):
        shoulder = Vector((side * 0.074, 0.000, 0.226))
        elbow = Vector((side * 0.093, -0.034, 0.176))
        wrist = Vector((side * 0.052, -0.089, 0.160))
        p.add('upper arm', fb.strut(shoulder, elbow, 0.027, 0.025), m['coat'])
        # the forearm starts a little inside the elbow so the bend has no notch
        p.add('forearm', fb.strut(elbow + (elbow - shoulder).normalized() * 0.012, wrist, 0.026, 0.021), m['coat'])
        p.add('hand', fb.ellipsoid((side * 0.040, -0.098, 0.158), (0.022, 0.019, 0.021), u=6, v=4), m['skin'])

    # --- the head, mostly hidden: beard in front, hat on top
    p.add('head', fb.ellipsoid((0.0, -0.004, 0.300), (0.060, 0.056, 0.060), u=8, v=6), m['skin'])

    # --- the beard: from under the nose to a point on his belly, above the belt
    p.add('beard', fb.loft([
        (0.0, -0.072, 0.212, 0.040, 0.028),
        (0.0, -0.054, 0.252, 0.062, 0.042),
        (0.0, -0.042, 0.288, 0.066, 0.045),
        (0.0, -0.036, 0.314, 0.058, 0.040),
    ], sides=10, bottom_tip=(0.0, -0.090, 0.180), cap_top=True, phase=90), m['beard'])
    for side in (-1, 1):
        p.add('moustache', fb.ellipsoid((side * 0.021, -0.074, 0.304), (0.025, 0.014, 0.011), u=6, v=4,
                                        rot=(0, side * 22, side * -14)), m['beard'])

    # --- nose, eyes, brows
    p.add('nose', fb.ellipsoid((0.0, -0.074, 0.318), (0.022, 0.020, 0.020), u=6, v=5), m['nose'])
    for side in (-1, 1):
        p.add('eye', fb.ellipsoid((side * 0.022, -0.049, 0.329), (0.0075, 0.0045, 0.0085), u=4, v=3), m['eyes'])
        p.add('brow', fb.box((0.021, 0.008, 0.007), (side * 0.023, -0.042, 0.343), rot=(0, side * -12, 0)), m['beard'])

    # --- the hat: sits back on his head, down to the brows in front; its tip flops back
    p.add('hat', fb.loft([
        (0.0, 0.004, 0.326, 0.066, 0.064, 0, 24),
        (0.0, 0.012, 0.372, 0.062, 0.060, 0, 18),
        (0.0, 0.024, 0.418, 0.050, 0.049, 0, 12),
        (0.0, 0.040, 0.458, 0.037, 0.036, 0, 4),
        (0.0, 0.060, 0.490, 0.025, 0.024, 0, -8),
        (0.004, 0.082, 0.512, 0.014, 0.014, 0, -25),
    ], sides=10, tip=(0.008, 0.106, 0.521), cap_bottom=True, phase=90), m['hat'])

    return p.finish()


def main(out_dir):
    fb.clear_scene()
    ob = build()
    n_islands, floating = fb.audit(ob)
    st = fb.stats(ob)
    report = [
        'Barnaby - built %s' % __import__('datetime').datetime.now().strftime('%Y-%m-%d %H:%M'),
        'triangles: %d' % st['tris'],
        'size (x, y, z): %s m; from %s to %s' % (st['size'], st['min'], st['max']),
        'materials: %s' % ', '.join(st['materials']),
        'parts (islands): %d; floating (more than 2 mm from any other): %s' % (n_islands, floating or 'none'),
        'shells built inside out and turned the right way out: %d' % ob.get('fixit_shells_turned', 0),
    ]
    print('\n'.join(report))
    return ob, report


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    main(argv[0] if argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out'))
