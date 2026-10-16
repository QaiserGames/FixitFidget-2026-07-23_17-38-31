"""
The bench stage's silicone repair mat, v1 (14 Oct 2026). Art Direction's brief A1 (specs/art-bench-and-cafe.md) and his
words of 14 Oct: "i would also prefer the mat to also look like a repair mat". Built on fixit_blender.py and hero_lib.py.

  BM_RepairMat   the kind of mat repair techs use, without a brand: 600 x 360 x 5 mm of slate-blue silicone, plan corners
                 r 12, one 1.5 mm chamfer round the top. On top, a printed 10 mm grid (BM_Grid_color.png, tiled in
                 metres), a printed ruler along the near edge and one along the left edge (BM_Rulers.png), six moulded
                 screw cups (inner 18 mm, 3.5 deep, a rounded rim, a pale floor with a small steel disc in it: the
                 magnetic insert a screw lands on with a tink, Audio's ask), cream numbers 1-6 beside them, a pale
                 "new part" pad 0.3 mm proud with four cream corner marks, and a cover well 3 mm deep inside a 0.8 mm
                 raised lip. Its empties are the auto-tray's places (Art's names; a contract): Cup_1..Cup_6 (on each
                 cup's steel disc), Pad_Small, Pad_Large (on the pad), Well_Small, Well_Large (on the well's floor).

THE FRAME (a contract, read this before placing it). The mat is built in the bench rig's own frame, not in the house's
"front faces -Y" frame: Art's mat coordinates (mm, origin at the mat's centre, +x the player's right, +y away from the
player) are Unity's local (+x, +z) once the FBX is imported, with no turn. So it goes under the InspectRig's stage with
local rotation 0, its origin (the bottom centre) on the bench top under the inspect point. In Blender that puts the
player at +Y: Art (x, y) mm = Blender (-x, -y) / 1000 (the FBX's Unity (x, y, z) = Blender (-x, z, -y)).

Cut, by the purpose rule: the real mat's 124 tiny screw cells and 42 spare cells, its printed code charts and screw
charts, the lidded boxes and the screwdriver slot row (noise at 3-4 px/mm; the tool rest does the slots' job), any brand.
Kept: the grid and rulers (it reads as a repair mat), the cups, pad and well (the auto-tray's places), the colour.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import bench_stage_textures as bst  # noqa: E402  (the ruler layout only; Pillow is imported inside its drawing functions)
from mathutils import Vector, geometry  # noqa: E402

TEX = 'Assets/Art/Textures/Bench/'
FONT = os.path.join(HERE, 'fonts', 'Inter-SemiBold.otf')

# (sRGB hex, Unity smoothness, texture or None, tiling, why). Metallic 0 everywhere.
BM_MATERIALS = {
    'BM_Silicone': ('#4F70A0', 0.28, 'BM_Grid_color.png', 20.0, "the mat's slate-blue silicone with its printed 10 mm grid; "
                    "the top is mapped in metres from the rulers' zero, the tile is 50 mm, so tiling 20"),
    'BM_Recess':   ('#3D5680', 0.22, None, 1.0, "the cups' walls, the cover well's walls and floor: a deeper blue (shadowed silicone)"),
    'BM_Pad':      ('#B4C3D6', 0.30, None, 1.0, "the pale new-part pad and the cups' floors: dark screws and parts read on it"),
    'BM_Print':    ('#EDE4D3', 0.28, 'BM_Rulers.png', 1.0, "the two printed ruler strips (cream ticks and numbers on the blue; "
                    "the texture carries both colours, so the material's base is white)"),
    'BM_Ink':      ('#F2EFE8', 0.28, None, 1.0, "the cups' numbers and the pad's corner marks, raised print"),
    'BM_Steel':    ('#B9B5AD', 0.50, None, 1.0, "the steel disc in each cup's floor (the magnetic insert; BT_Steel's values)"),
}

# ----------------------------------------------------------------------------- the layout (Art's table, mm, mat frame)
W, D, T = 600.0, 360.0, 5.0          # the mat
PLAN_R, CHAMFER = 12.0, 1.5
CUPS = [(36.0, -24.0), (36.0, 0.0), (36.0, 24.0), (60.0, -24.0), (60.0, 0.0), (60.0, 24.0)]   # Cup_1..6: near to far, left column first
CUP_R, CUP_RIM, CUP_DEPTH, CUP_SIDES = 9.0, 1.0, 3.5, 16
DISC_R, DISC_PROUD, DISC_SIDES = 5.0, 0.3, 12
PAD = (90.0, 170.0, -82.0, 84.0)     # x0, x1, y0, y1
PAD_R, PAD_PROUD, PAD_CHAMFER = 6.0, 0.3, 0.5
MARK_LEG, MARK_W, MARK_IN, MARK_PROUD = 12.0, 1.5, 6.0, 0.3
WELL = (182.0, 262.0, -82.0, 84.0)
WELL_R, WELL_DEPTH, LIP_W, LIP_H = 8.0, 3.0, 2.0, 0.8
# The ruler strips stop on the outline's own corners (no T-junction where they meet the chamfer): the near strip along the
# straight front edge (x -288..+288; Art's -290..+290 less the corners, so its 0 sits at x -285), the left strip from
# y -166 (a point added to the outline there) to the straight edge's far end (+168).
NEAR_RULER = (-288.0, 288.0, -180.0, bst.NEAR_INNER_Y)
LEFT_RULER = (-300.0, bst.LEFT_INNER_X, bst.LEFT_ZERO_Y, 168.0)
EMPTIES = {   # name: (x, y, z) mm in the mat frame; z is the surface a piece lands on
    **{'Cup_%d' % (i + 1): (cx, cy, T - CUP_DEPTH + DISC_PROUD) for i, (cx, cy) in enumerate(CUPS)},
    'Pad_Small': (116.0, -58.0, T + PAD_PROUD),
    'Pad_Large': (130.0, 0.0, T + PAD_PROUD),
    'Well_Large': (222.0, 0.0, T - WELL_DEPTH),
    'Well_Small': (206.0, -58.0, T - WELL_DEPTH),
}
# Art's per-device table (data for Systems; mm in the mat frame). footprint: x, y extents of the device as it rests.
# tool_rest_y (mine, 14 Oct): Art's table gives the rest's x only. Centred in depth (y -115..+115) the 230 mm rest runs into
# the phone's and the watch's scrap trays (their near edges at y 88 and 62), so for those two it stands at y -60 (-175..+55,
# its near edge 5 mm in from the mat's): 7 mm or more clear of the tray. The camera's tray is clear of its rest, and at
# y -60 the rest's handles fell into UI/UX's zone U4 (the review render, 14 Oct), so the camera's rest stays centred (y 0).
TOOL_REST_Y = -60.0
PER_DEVICE = {
    # wave 2 (14 Oct, the TD's framings, td-one-scale.md §5.2): at 0.48 / 0.42 m the rest's near end at y -60 fell into UI/UX's
    # U4 (bottom left) at 16:9 and 16:10, so every rest is centred (y 0), and the phone's and the watch's scrap trays move
    # right, clear of their rests (6 mm): phone (-30, 133) -> (6, 133), watch (-20, 107) -> (26, 107). Systems' data, proposed.
    'phone':  {'rest_centre': (-30.0, -14.0), 'footprint': (72.0, 148.0), 'tool_rest_x': (-190.0, -80.0), 'tool_rest_y': 0.0, 'scrap_tray_centre': (6.0, 133.0),
               'note': 'long axis along y'},
    'camera': {'rest_centre': (-82.0, 0.0), 'footprint': (120.0, 80.0), 'tool_rest_x': (-294.0, -184.0), 'tool_rest_y': 0.0, 'scrap_tray_centre': (-5.0, 115.0),
               'note': 'lens to the left; v3.6 strap: lugs at both ends, the runs 28-38 mm out, the loop 88 x 58 behind the lens half (device x -96..-8, y +66..+124)'},
    'watch':  {'rest_centre': (-20.0, 0.0), 'footprint': (50.0, 65.0), 'tool_rest_x': (-170.0, -60.0), 'tool_rest_y': 0.0, 'scrap_tray_centre': (26.0, 107.0),
               'note': 'the bow toward +y'},
}
CLEAN_UV = (0.005, 0.005)            # a point of plain silicone in the grid tile (between its lines): sides, chamfers, rims
GRID_UV_ZERO = (bst.NEAR_ZERO_X, bst.LEFT_ZERO_Y)   # the rulers' zeros: the grid's major lines fall every 50 mm from here


def to_b(x, y, z):
    """Art's mat frame (mm) to Blender (m): the player at Blender +Y."""
    return (-x / 1000.0, -y / 1000.0, z / 1000.0)


def M(name):
    hexstr, smooth = BM_MATERIALS[name][:2]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


# ----------------------------------------------------------------------------- 2D helpers (mm, mat frame)

def rrect(x0, x1, y0, y1, r, per_corner=4, left_at=None):
    """A rounded rectangle, counter-clockwise; left_at adds a point on the left edge at that y (the mat's outline)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    pts = [(p[0], p[1]) for p in hl.ring_rrect(cx, cy, 0.0, x1 - x0, y1 - y0, r, per_corner=per_corner)]
    if left_at is not None:
        k = 3 * (per_corner + 1)            # the bottom-left corner's first point; the left edge ends just before it
        pts.insert(k, (x0, left_at))
    return pts


def circle(cx, cy, r, n):
    return [(cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def clip(poly, clipper):
    """Sutherland-Hodgman: poly clipped by a convex, counter-clockwise clipper."""
    def inside(p, a, b):
        return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= -1e-9

    def cross(p, q, a, b):
        x1, y1, x2, y2 = p[0], p[1], q[0], q[1]
        x3, y3, x4, y4 = a[0], a[1], b[0], b[1]
        den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
        t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
        return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))
    out = poly
    for a, b in zip(clipper, clipper[1:] + clipper[:1]):
        src, out = out, []
        if not src:
            break
        for p, q in zip(src, src[1:] + src[:1]):
            if inside(q, a, b):
                if not inside(p, a, b):
                    out.append(cross(p, q, a, b))
                out.append(q)
            elif inside(p, a, b):
                out.append(cross(p, q, a, b))
    return out


def box2(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def in_rect(p, r, pad=1e-6):
    return r[0] - pad <= p[0] <= r[1] + pad and r[2] - pad <= p[1] <= r[3] + pad


def ruler_uv(x, y, which):
    return bst.near_uv(x, y) if which == 'near' else bst.left_uv(x, y)


# ----------------------------------------------------------------------------- the mesh

class MatBuilder:
    """Faces in mm (mat frame) with a material and a UV rule each; welded, oriented and converted at the end."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.mats = list(BM_MATERIALS)
        self.parts = []

    def face(self, pts3, mat, uvs=None):
        vs = [self.bm.verts.new(p) for p in pts3]
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return None
        f.material_index = self.mats.index(mat)
        for k, loop in enumerate(f.loops):
            loop[self.uv].uv = uvs[k] if uvs else CLEAN_UV
        return f

    def tube(self, loops, mat, cap_top=None, cap_bottom=None, uv=None):
        """Quads between consecutive loops (bottom to top in the list's order, same counts, both counter-clockwise)."""
        n = len(loops[0])
        for lo, hi in zip(loops, loops[1:]):
            for i in range(n):
                j = (i + 1) % n
                self.face([lo[i], lo[j], hi[j], hi[i]], mat if isinstance(mat, str) else mat(lo, hi))
        if cap_top:
            self.face(loops[-1], cap_top, [uv(p) for p in loops[-1]] if uv else None)
        if cap_bottom:
            self.face(list(reversed(loops[0])), cap_bottom)

    def solid(self, name, bm_src, mat, z_shift=0.0):
        """A closed part (text, a disc, a corner mark) added as it is, in mm."""
        for f in bm_src.faces:
            pts = [(v.co.x, v.co.y, v.co.z + z_shift) for v in f.verts]
            self.face(pts, mat)
        bm_src.free()


def build_mat():
    mb = MatBuilder()
    top_outline = rrect(-W / 2 + CHAMFER, W / 2 - CHAMFER, -D / 2 + CHAMFER, D / 2 - CHAMFER, PLAN_R - CHAMFER, left_at=bst.LEFT_ZERO_Y)
    body = rrect(-W / 2, W / 2, -D / 2, D / 2, PLAN_R, left_at=bst.LEFT_ZERO_Y)

    # the body: bottom, sides, the top chamfer
    loops = [[(x, y, 0.0) for x, y in body], [(x, y, T - CHAMFER) for x, y in body], [(x, y, T) for x, y in top_outline]]
    mb.tube(loops, 'BM_Silicone', cap_bottom='BM_Silicone')

    # the top: one constrained triangulation of the outline, with the cups and the well's lip as holes and the two ruler
    # strips as regions of their own (mathutils.geometry.delaunay_2d_cdt; output 1 tells which input face each triangle is in)
    cup_holes = [circle(cx, cy, CUP_R + CUP_RIM, CUP_SIDES) for cx, cy in CUPS]
    wx0, wx1, wy0, wy1 = WELL
    well_hole = rrect(wx0 - LIP_W, wx1 + LIP_W, wy0 - LIP_W, wy1 + LIP_W, WELL_R + LIP_W)
    near = clip(box2(*NEAR_RULER), top_outline)
    left = clip(box2(*LEFT_RULER), top_outline)
    polys = [top_outline] + cup_holes + [well_hole, near, left]
    verts, faces = [], []
    for poly in polys:
        faces.append(list(range(len(verts), len(verts) + len(poly))))
        verts += [Vector(p) for p in poly]
    vc, _ed, fc, _ov, _oe, of = geometry.delaunay_2d_cdt(verts, [], faces, 1, 1e-6)
    hole_ids = set(range(1, 1 + len(cup_holes) + 1))
    near_id, left_id = len(polys) - 2, len(polys) - 1
    for tri, owners in zip(fc, of):
        if hole_ids & set(owners):
            continue
        pts = [(vc[i].x, vc[i].y, T) for i in tri]
        if near_id in owners or left_id in owners:
            which = 'near' if near_id in owners else 'left'
            mb.face(pts, 'BM_Print', [ruler_uv(p[0], p[1], which) for p in pts])
        else:
            mb.face(pts, 'BM_Silicone', [((p[0] - GRID_UV_ZERO[0]) / 1000.0, (p[1] - GRID_UV_ZERO[1]) / 1000.0) for p in pts])

    # the cups: a rounded rim (one chamfer), walls, a pale floor and the steel disc
    floor_z = T - CUP_DEPTH
    for cx, cy in CUPS:
        rim = circle(cx, cy, CUP_R + CUP_RIM, CUP_SIDES)
        wall = circle(cx, cy, CUP_R, CUP_SIDES)
        top = [(x, y, T) for x, y in rim]
        lip = [(x, y, T - CUP_RIM) for x, y in wall]
        bottom = [(x, y, floor_z) for x, y in wall]
        # the rim (silicone) going down into the cup: the loops run top to bottom, so wind them for an inward-facing hole
        mb.tube([list(reversed(top)), list(reversed(lip))], 'BM_Silicone')
        mb.tube([list(reversed(lip)), list(reversed(bottom))], 'BM_Recess')
        mb.face(bottom, 'BM_Pad')                                                     # the floor, looking up
        disc = hl.revolve([(floor_z - 0.1, DISC_R), (floor_z + DISC_PROUD - 0.08, DISC_R), (floor_z + DISC_PROUD, DISC_R - 0.12)], sides=DISC_SIDES)
        hl.moved(disc, cx, cy, 0.0)
        mb.solid('disc', disc, 'BM_Steel')

    # the cover well: the raised lip, the walls, the floor
    def well_loop(off, z):
        return [(x, y, z) for x, y in rrect(wx0 - off, wx1 + off, wy0 - off, wy1 + off, WELL_R + off)]
    lip_loops = [well_loop(LIP_W, T), well_loop(LIP_W - 0.6, T + LIP_H), well_loop(0.6, T + LIP_H), well_loop(0.0, T + 0.2)]
    mb.tube([list(reversed(l)) for l in lip_loops], 'BM_Silicone')
    mb.tube([list(reversed(well_loop(0.0, T + 0.2))), list(reversed(well_loop(0.0, T - WELL_DEPTH)))], 'BM_Recess')
    mb.face(well_loop(0.0, T - WELL_DEPTH), 'BM_Recess')

    # the pad: a pale slab 0.3 mm proud with a 0.5 mm chamfer, its foot sunk 0.6 mm into the mat (no two faces in a plane)
    px0, px1, py0, py1 = PAD
    pad_loops = [[(x, y, T - 0.6) for x, y in rrect(px0, px1, py0, py1, PAD_R)],
                 [(x, y, T) for x, y in rrect(px0, px1, py0, py1, PAD_R)],
                 [(x, y, T + PAD_PROUD) for x, y in rrect(px0 + PAD_CHAMFER, px1 - PAD_CHAMFER, py0 + PAD_CHAMFER, py1 - PAD_CHAMFER, PAD_R - PAD_CHAMFER)]]
    mb.tube(pad_loops, 'BM_Pad', cap_top='BM_Pad', cap_bottom='BM_Pad')
    # the four L corner marks, 6 mm in from the pad's corners, legs 12 mm, 1.5 mm wide, 0.3 mm proud of the pad
    for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        cx = px0 + MARK_IN if sx > 0 else px1 - MARK_IN
        cy = py0 + MARK_IN if sy > 0 else py1 - MARK_IN
        pts = [(0, 0), (MARK_LEG, 0), (MARK_LEG, MARK_W), (MARK_W, MARK_W), (MARK_W, MARK_LEG), (0, MARK_LEG)]
        pts = [(cx + sx * a, cy + sy * b) for a, b in pts]
        if sx * sy < 0:
            pts = list(reversed(pts))
        z0, z1 = T + PAD_PROUD - 0.1, T + PAD_PROUD + MARK_PROUD
        bm = bmesh.new()
        lo = [bm.verts.new((x, y, z0)) for x, y in pts]
        hi = [bm.verts.new((x, y, z1)) for x, y in pts]
        bm.faces.new(hi)
        bm.faces.new(list(reversed(lo)))
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
        mb.solid('corner mark', bm, 'BM_Ink')

    # the cups' numbers: cream, 3 mm tall, left of each cup, 0.2 mm proud (their feet sunk 0.1 mm)
    for k, (cx, cy) in enumerate(CUPS):
        bm = hl.text_mesh(str(k + 1), 4.1, 0.3, font_path=FONT, align='CENTER')
        if bm is None:
            continue
        ys = [v.co.y for v in bm.verts]
        hl.moved(bm, cx - CUP_R - CUP_RIM - 2.0, cy - (max(ys) + min(ys)) / 2, T - 0.1)
        mb.solid('number', bm, 'BM_Ink')

    # weld, orient, convert to Blender metres (Art (x, y) -> Blender (-x, -y): a half turn about Z, the winding kept)
    bm = mb.bm
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    for v in bm.verts:
        v.co = Vector(to_b(v.co.x, v.co.y, v.co.z))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    turned = fb.orient_shells(bm)
    bm.normal_update()
    for f in bm.faces:              # flat shading (the house's), the soft things are elsewhere
        f.smooth = False
    me = bpy.data.meshes.new('BM_RepairMat')
    bm.to_mesh(me)
    bm.free()
    for name in mb.mats:
        me.materials.append(M(name))
    ob = bpy.data.objects.new('BM_RepairMat', me)
    ob['fixit_shells_turned'] = turned
    bpy.context.scene.collection.objects.link(ob)
    # the empties: the auto-tray's places, children of the mat at the origin (their names are a contract)
    empties = []
    for name, (x, y, z) in EMPTIES.items():
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'PLAIN_AXES'
        e.empty_display_size = 0.01
        bpy.context.scene.collection.objects.link(e)
        e.parent = ob
        e.location = to_b(x, y, z)
        empties.append(e)
    return ob, empties


def review_textures(root):
    """For review renders only: wire the two pictures into the Blender materials (the FBX goes out without them)."""
    for name, (hexstr, smooth, tex, tiling, _why) in BM_MATERIALS.items():
        if not tex:
            continue
        m = bpy.data.materials.get(name)
        path = os.path.join(root, TEX, tex)
        if m is None or not os.path.exists(path):
            continue
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        img = nt.nodes.new('ShaderNodeTexImage')
        img.image = bpy.data.images.load(path, check_existing=True)
        img.interpolation = 'Cubic'
        if tiling != 1.0:
            uvn = nt.nodes.new('ShaderNodeTexCoord')
            mp = nt.nodes.new('ShaderNodeMapping')
            mp.inputs['Scale'].default_value = (tiling, tiling, 1.0)
            nt.links.new(uvn.outputs['UV'], mp.inputs['Vector'])
            nt.links.new(mp.outputs['Vector'], img.inputs['Vector'])
        else:
            img.extension = 'EXTEND'
        nt.links.new(img.outputs['Color'], bsdf.inputs['Base Color'])
