"""
Hero props, the house way (v3, 9 Oct 2026). Shared by bench_tools.py, hero_phone.py, hero_watch.py and hero_camera.py.

Two verdicts shaped this file. The first hero set (7 Oct) was "just so blocky": eight-sided prisms, primitive forms. The
second (8 Oct) went smooth, glossy, metallic and swollen, and read as "kid toys ... we lose that distinctive art style".
Reality (claude/hero-reference-sheet.md: ~70 photographs with rulers in frame) and the art style guide agree on the fix:

  * REAL PROPORTIONS. Every hand tool is thinner than a pencil somewhere (a 3 mm blade under a 16 mm cap, 1.2 mm
    tweezer arms, a 0.8 mm pry blade). The fat parts stay fat and the thin parts are thin: the contrast is the realism.
  * THE GAME'S SHADING. Hard goods are flat-shaded (faceted) like everything else in the cafe, with enough sides that a
    curve reads as a curve from 50 cm (16 on a handle, 24 on a crown, 48 on a watch case); boxy parts get ONE chamfer
    (the guide's bevel: segments 1). Soft things (bristle, cloth) are smooth: a faceted cloth is paper.
  * THE GAME'S MATERIALS. Metallic 0 everywhere, warm muted colours (grey leans warm), satin at most. Two or three
    materials per object: black + steel + one accent. The only colourful things are the driver's pads, the cloth and
    the phone's back.
  * ANATOMY OVER ORNAMENT. Seams where parts are separate (a cap that spins, a bezel, a back that comes off), the right
    count of small details (ticks, flutes, knurls, screws), the signature silhouette first.

What never changes (the bench code and the labs depend on it): 1 unit = 1 m; a tool's origin is its working tip, its
handle up +Z; a flat tool's flat side faces -Y (+Z in Unity, toward the close-up camera); Unity (x, y, z) = Blender
(-x, z, -y) (see to_blender); file names and the tweezers' three meshes (see bench_tools.py).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

SMOOTH_ANGLE = 42.0     # degrees: on a SMOOTH part, an edge whose faces meet at more than this stays hard


def material(table, name):
    hexstr, smooth, metal = table[name]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=metal)


class Faceted(fb.Prop):
    """A prop built the house way: flat shading unless a part asks for smooth; one chamfer on boxy parts."""

    def __init__(self, name):
        super().__init__(name, floor=False)
        self.smooth_faces = set()

    def add(self, part_name, src_bm, mat, bevel=0.0, segments=1, smooth=False):
        if bevel > 0:
            fb.bevel_all(src_bm, bevel, segments=segments, angle=30.0)
            bmesh.ops.dissolve_degenerate(src_bm, dist=2e-6, edges=src_bm.edges[:])
        faces = super().add(part_name, src_bm, mat, smooth=smooth)
        if smooth:
            self.smooth_faces.update(faces)
        return faces

    def finish(self, collection=None):
        limit = math.radians(SMOOTH_ANGLE)
        for e in self.bm.edges:
            if not e.is_manifold:
                e.smooth = True
                continue
            fa, fb_ = e.link_faces
            if fa in self.smooth_faces and fb_ in self.smooth_faces:
                e.smooth = e.calc_face_angle(0.0) <= limit
            else:
                e.smooth = False
        return super().finish(collection)


# ----------------------------------------------------------------------------- maths

def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def bez(t, p0, p1, p2):
    return (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2


def interp(profile, z):
    """Radius of a (z, r) profile at z (linear between points)."""
    for (z0, r0), (z1, r1) in zip(profile, profile[1:]):
        if z0 <= z <= z1 and z1 > z0:
            return lerp(r0, r1, (z - z0) / (z1 - z0))
    return profile[0][1] if z < profile[0][0] else profile[-1][1]


# ----------------------------------------------------------------------------- loops, tubes and solids

def ring_round(cx, cy, z, rx, ry, sides=16, lobes=None, phase=0.0, knurl=None):
    """A round (or oval) loop. lobes (n, k) swells it by k cos(n a); knurl (n, k) makes it a saw of n teeth."""
    pts = []
    for i in range(sides):
        a = 2 * math.pi * i / sides + phase
        k = 1.0
        if lobes:
            k *= 1.0 + lobes[1] * math.cos(lobes[0] * a)
        if knurl:
            k *= 1.0 - knurl[1] * (i % 2)
        pts.append((cx + rx * k * math.cos(a), cy + ry * k * math.sin(a), z))
    return pts


def ring_rrect(cx, cy, z, sx, sy, rc, per_corner=2, wave=None, per_side=0):
    """A rounded-rectangle loop, sx across X and sy across Y, corners of radius rc (clamped under half the short side)."""
    rc = min(rc, 0.45 * min(sx, sy))
    hx, hy = sx / 2 - rc, sy / 2 - rc
    corners = ((hx, -hy, -90.0), (hx, hy, 0.0), (-hx, hy, 90.0), (-hx, -hy, 180.0))
    pts = []
    for ci, (ox, oy, a0) in enumerate(corners):
        for k in range(per_corner + 1):
            a = math.radians(a0 + 90.0 * k / per_corner)
            pts.append((cx + ox + rc * math.cos(a), cy + oy + rc * math.sin(a), z))
        nx_, ny_, na0 = corners[(ci + 1) % 4]
        a_end, a_next = math.radians(a0 + 90.0), math.radians(na0)
        p_end = (cx + ox + rc * math.cos(a_end), cy + oy + rc * math.sin(a_end))
        p_next = (cx + nx_ + rc * math.cos(a_next), cy + ny_ + rc * math.sin(a_next))
        for m in range(1, per_side + 1):
            f = m / (per_side + 1)
            pts.append((p_end[0] + (p_next[0] - p_end[0]) * f, p_end[1] + (p_next[1] - p_end[1]) * f, z))
    if wave:
        n, amp = wave
        out = []
        for (x, y, zz) in pts:
            a = math.atan2(y - cy, x - cx)
            s = 1.0 + amp * math.cos(n * a)
            out.append((cx + (x - cx) * s, cy + (y - cy) * s, zz))
        pts = out
    return pts


def ring_chamfered_rect(cx, cy, z, sx, sy, c):
    """A rectangle with its four corners cut at 45 degrees by c (the house's one-chamfer corner), 8 points."""
    hx, hy = sx / 2, sy / 2
    return [(cx + hx - c, cy - hy, z), (cx + hx, cy - hy + c, z), (cx + hx, cy + hy - c, z), (cx + hx - c, cy + hy, z),
            (cx - hx + c, cy + hy, z), (cx - hx, cy + hy - c, z), (cx - hx, cy - hy + c, z), (cx - hx + c, cy - hy, z)]


def tube(loops, cap_bottom=True, cap_top=True, tip=None, bottom_tip=None):
    """Faces between consecutive loops of the same length (bottom to top), capped or closed to a point."""
    bm = bmesh.new()
    vl = [[bm.verts.new(p) for p in loop] for loop in loops]
    n = len(loops[0])
    for lo, hi in zip(vl, vl[1:]):
        for i in range(n):
            j = (i + 1) % n
            try:
                bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
            except ValueError:
                pass
    if bottom_tip is not None:
        t = bm.verts.new(bottom_tip)
        for i in range(n):
            bm.faces.new([vl[0][(i + 1) % n], vl[0][i], t])
    elif cap_bottom:
        bm.faces.new(list(reversed(vl[0])))
    if tip is not None:
        t = bm.verts.new(tip)
        for i in range(n):
            bm.faces.new([vl[-1][i], vl[-1][(i + 1) % n], t])
    elif cap_top:
        bm.faces.new(vl[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def revolve(profile, sides=16, lobes=None, squash=1.0, tip=None, bottom_tip=None, cap_bottom=True, cap_top=True, phase=0.0, knurl=None):
    """A round solid from a profile [(z, r)] bottom to top; squash flattens it along Y."""
    loops = [ring_round(0.0, 0.0, z, r, r * squash, sides=sides, lobes=lobes, phase=phase, knurl=knurl) for z, r in profile]
    return tube(loops, cap_bottom=cap_bottom, cap_top=cap_top, tip=tip, bottom_tip=bottom_tip)


def dome(z0, r0, h, n=4, up=True):
    """Profile points for a domed end: from (z0, r0) toward the pole h away (the pole itself left for a tip)."""
    pts = []
    for i in range(1, n):
        a = (math.pi / 2) * i / n
        pts.append((z0 + (h if up else -h) * math.sin(a), r0 * math.cos(a)))
    return pts


def ring_wall(z0, z1, r_in, r_out, sides=48, lobes=None, knurl=None):
    """A ring (a tube with thickness) from z0 to z1, closed top and bottom."""
    bm = bmesh.new()
    outer = [[bm.verts.new(p) for p in ring_round(0, 0, z, r_out, r_out, sides, lobes=lobes, knurl=knurl)] for z in (z0, z1)]
    inner = [[bm.verts.new(p) for p in ring_round(0, 0, z, r_in, r_in, sides)] for z in (z0, z1)]
    n = sides
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([outer[0][i], outer[0][j], outer[1][j], outer[1][i]])
        bm.faces.new([inner[1][i], inner[1][j], inner[0][j], inner[0][i]])
        bm.faces.new([outer[1][i], outer[1][j], inner[1][j], inner[1][i]])
        bm.faces.new([inner[0][i], inner[0][j], outer[0][j], outer[0][i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def rrect_wall(z0, z1, sx, sy, rc, wall, per_corner=3):
    """A rounded-rectangle frame (a lip, a bezel) of the given wall thickness from z0 to z1, closed top and bottom."""
    bm = bmesh.new()
    outer = [[bm.verts.new(p) for p in ring_rrect(0, 0, z, sx, sy, rc, per_corner=per_corner)] for z in (z0, z1)]
    inner = [[bm.verts.new(p) for p in ring_rrect(0, 0, z, sx - 2 * wall, sy - 2 * wall, max(.0002, rc - wall), per_corner=per_corner)] for z in (z0, z1)]
    n = len(outer[0])
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([outer[0][i], outer[0][j], outer[1][j], outer[1][i]])
        bm.faces.new([inner[1][i], inner[1][j], inner[0][j], inner[0][i]])
        bm.faces.new([outer[1][i], outer[1][j], inner[1][j], inner[1][i]])
        bm.faces.new([inner[0][i], inner[0][j], outer[0][j], outer[0][i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def disc(cx, cy, z0, z1, r, sides=12):
    """A short cylinder (a hole seen as a dark dot, a rivet, a dial)."""
    return moved(revolve([(z0, r), (z1, r)], sides=sides), cx, cy, 0)


def ring_wall_profile(prof_out, r_in, sides=48):
    """A ring whose OUTSIDE follows a profile [(z, r)] (bottom to top) and whose inside is a straight bore of r_in."""
    bm = bmesh.new()
    outer = [[bm.verts.new(p) for p in ring_round(0, 0, z, r, r, sides)] for z, r in prof_out]
    inner = [[bm.verts.new(p) for p in ring_round(0, 0, z, r_in, r_in, sides)] for z in (prof_out[0][0], prof_out[-1][0])]
    n = sides
    for lo, hi in zip(outer, outer[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([inner[1][i], inner[1][j], inner[0][j], inner[0][i]])
        bm.faces.new([outer[-1][i], outer[-1][j], inner[1][j], inner[1][i]])
        bm.faces.new([inner[0][i], inner[0][j], outer[0][j], outer[0][i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def gear(cx, cy, z0, z1, r, teeth, depth=.14, sides_per_tooth=4):
    """A toothed disc (a spur wheel): the radius pulses between r and r (1 - depth)."""
    sides = teeth * sides_per_tooth
    loops = []
    for z in (z0, z1):
        pts = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            phase = (i % sides_per_tooth) / sides_per_tooth
            rr = r * (1.0 if phase < .5 else 1.0 - depth)
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), z))
        loops.append(pts)
    return tube(loops)


def spoked_wheel(cx, cy, z0, z1, r_out, rim, spokes=4, spoke_w=None, hub=None, sides=48):
    """A rim ring plus straight spokes and a hub (a balance wheel, a hand-wheel). Returns one bmesh."""
    bm = ring_wall(z0, z1, r_out - rim, r_out, sides=sides)
    bmesh.ops.translate(bm, verts=bm.verts[:], vec=(cx, cy, 0))
    sw = spoke_w or rim
    hr = hub or rim * 1.6
    for k in range(spokes):
        a = 360.0 * k / spokes
        sp = fb.zbox(-sw / 2, sw / 2, 0.0, r_out - rim * .5, z0, z1)
        fb.turn(sp, a, 'Z')
        bmesh.ops.translate(sp, verts=sp.verts[:], vec=(cx, cy, 0))
        fb.merge(bm, sp)
    h = revolve([(z0, hr), (z1, hr)], sides=12)
    bmesh.ops.translate(h, verts=h.verts[:], vec=(cx, cy, 0))
    fb.merge(bm, h)
    return bm


def pillow_slab(sx, sy, thickness, plan_r, edge_r, per_corner=4, centre_z=0.0, n=2):
    """A slab with rounded plan corners and a rounded edge, lofted from rounded-rectangle loops (faceted by n)."""
    half = thickness / 2
    r = min(edge_r, half * .999)
    loops = []
    for i in range(n + 1):
        a = (math.pi / 2) * i / n
        inset = r * (1 - math.sin(a))
        z = centre_z - half + r * (1 - math.cos(a))
        loops.append(ring_rrect(0, 0, z, sx - 2 * inset, sy - 2 * inset, max(.0003, plan_r - inset), per_corner=per_corner))
    start = 0 if (half - r) > (-half + r) + 1e-6 else 1
    for i in range(start, n + 1):
        a = (math.pi / 2) * i / n
        inset = r * (1 - math.cos(a))
        z = centre_z + half - r * (1 - math.sin(a))
        loops.append(ring_rrect(0, 0, z, sx - 2 * inset, sy - 2 * inset, max(.0003, plan_r - inset), per_corner=per_corner))
    return tube(loops)


def chamfered_slab(sx, sy, thickness, plan_r, chamfer, per_corner=3, centre_z=0.0):
    """The house slab: rounded plan corners, ONE chamfer top and bottom (segments 1)."""
    half = thickness / 2
    c = min(chamfer, half * .9)
    loops = [ring_rrect(0, 0, centre_z - half, sx - 2 * c, sy - 2 * c, max(.0003, plan_r - c), per_corner=per_corner),
             ring_rrect(0, 0, centre_z - half + c, sx, sy, plan_r, per_corner=per_corner),
             ring_rrect(0, 0, centre_z + half - c, sx, sy, plan_r, per_corner=per_corner),
             ring_rrect(0, 0, centre_z + half, sx - 2 * c, sy - 2 * c, max(.0003, plan_r - c), per_corner=per_corner)]
    return tube(loops)


def shell_from_surface(top, bottom, faces):
    """A closed shell from a surface: faces are lists of keys into top (each key also in bottom), wound as seen from
    above; the underside is the same faces on bottom, and a wall runs round the boundary."""
    bm = bmesh.new()
    tv = {k: bm.verts.new(v) for k, v in top.items()}
    bv = {k: bm.verts.new(v) for k, v in bottom.items()}
    edges = {}
    for f in faces:
        bm.faces.new([tv[k] for k in f])
        bm.faces.new([bv[k] for k in reversed(f)])
        for a, b in zip(f, f[1:] + f[:1]):
            edges[(a, b)] = edges.get((a, b), 0) + 1
    for (a, b), n in edges.items():
        if n == 1 and (b, a) not in edges:
            bm.faces.new([tv[a], tv[b], bv[b], bv[a]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def sweep(points, radius, sides=6, closed=False, cap=True):
    """A round tube along a polyline (a wire bow, a spring, a crank)."""
    pts = [Vector(p) for p in points]
    loops = []
    n = len(pts)
    for i, c in enumerate(pts):
        if closed:
            d = (pts[(i + 1) % n] - pts[(i - 1) % n]).normalized()
        else:
            d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        u = Vector((0, 0, 1)).cross(d)
        if u.length < 1e-6:
            u = Vector((0, 1, 0)).cross(d)
        u.normalize()
        w = d.cross(u).normalized()
        loops.append([tuple(c + (u * math.cos(a) + w * math.sin(a)) * radius) for a in (2 * math.pi * k / sides for k in range(sides))])
    if closed:
        loops.append(loops[0])
        return tube(loops, cap_bottom=False, cap_top=False)
    return tube(loops, cap_bottom=cap, cap_top=cap)


def moved(bm, dx, dy, dz):
    bmesh.ops.translate(bm, verts=bm.verts[:], vec=(dx, dy, dz))
    return bm


def turned(bm, degrees, axis='Z', pivot=(0, 0, 0)):
    return fb.turn(bm, degrees, axis, pivot)


def text_mesh(text, size, depth, font_path=None, align='CENTER'):
    """
    Raised lettering (a dial's numerals, a brand) as a bmesh lying in XY, extruded +Z by depth, its baseline at y = 0 and
    centred on x = 0. Uses Blender's built-in font (headless-safe). Returns None if conversion is not possible.
    """
    try:
        cu = bpy.data.curves.new('tmp_text', type='FONT')
        cu.body = text
        cu.size = size
        cu.extrude = depth / 2
        cu.align_x = align
        cu.align_y = 'BOTTOM_BASELINE'
        cu.resolution_u = 3
        if font_path and os.path.exists(font_path):
            try:
                cu.font = bpy.data.fonts.load(font_path)
            except Exception:
                pass
        ob = bpy.data.objects.new('tmp_text', cu)
        bpy.context.scene.collection.objects.link(ob)
        bpy.context.view_layer.update()
        me = ob.to_mesh()
        bm = bmesh.new()
        bm.from_mesh(me)
        ob.to_mesh_clear()
        bmesh.ops.translate(bm, verts=bm.verts[:], vec=(0, 0, depth / 2))
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.curves.remove(cu)
        return bm
    except Exception as exc:          # pragma: no cover
        print('text_mesh failed:', exc)
        return None


def to_blender(unity_xyz):
    """
    A Unity position in this Blender frame. Unity (x, y, z) = Blender (-x, z, -y): the FBX transfer from Blender's
    right-handed frame to Unity's left-handed one keeps what you SEE (a lens left of centre stays left of centre), which
    means Blender +X lands on Unity -X. (Found by Devices 1 on 9 Oct 2026: every child of the first hero camera sat on the
    far side of its body. Every kit before it was symmetric in X.)
    """
    x, y, z = unity_xyz
    return (-x, -z, y)


def to_unity(blender_xyz):
    x, y, z = blender_xyz
    return (-x, z, -y)
