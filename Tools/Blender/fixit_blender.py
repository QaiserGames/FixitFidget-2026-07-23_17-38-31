"""
Fixit Fidget - shared helpers for building props in Blender with Python.

Runs inside Blender 5.2 (the Scripting tab, or headless: blender -b --python <script>), or with the
bpy module (pip install bpy==5.2.2, Python 3.13). The props follow claude/art-style-guide.md and
claude/furniture-library.md:

  * 1 Blender unit = 1 m; Z up in Blender; a prop's front faces -Y (it faces +Z in Unity).
  * Origin at the bottom centre, transforms applied, one joined mesh per prop.
  * Flat shading (faceted), except where a part asks for smooth.
  * Colours are sRGB hex, converted to linear for Blender (rgb()).
  * Junctions come from boundaries (span, at_z), parts overlap 2-4 mm, and every prop is audited
    for floating parts before export (audit()).
  * Export: FBX, Forward -Z, Up +Y, transform baked, modifiers applied, face smoothing.

Everything here builds geometry with bmesh (no operators), so it runs the same headless.
"""
import math
import os

import bpy  # first: with the bpy module, bmesh and mathutils come with it
import bmesh
from mathutils import Matrix, Vector

# ----------------------------------------------------------------------------- colours and materials


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb(hexstr):
    h = hexstr.lstrip('#')
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4))


def material(name, hexstr, rough=0.5, metal=0.0):
    """A flat-colour Principled material (the Unity side remaps it by name)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True          # deprecated in 5.x (always on); harmless where it still exists
    except Exception:
        pass
    col = (*rgb(hexstr), 1.0)
    bsdf = None
    if m.node_tree is not None:
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is not None:
        bsdf.inputs['Base Color'].default_value = col
        bsdf.inputs['Roughness'].default_value = rough
        bsdf.inputs['Metallic'].default_value = metal
    m.diffuse_color = col
    m.roughness = rough
    m.metallic = metal
    m['hex'] = hexstr
    return m


# ----------------------------------------------------------------------------- a prop under construction


class Prop:
    """One prop: a bmesh that parts are added to, each with its material."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []          # material objects, in slot order
        self.parts = []         # (part name, set of face indices) for the audit and the report
        self.smooth = set()     # material names shaded smooth

    def slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def add(self, part_name, src_bm, mat, smooth=False):
        """Append src_bm (freed afterwards) as a part with one material."""
        idx = self.slot(mat)
        dst = self.bm
        vmap = {}
        for v in src_bm.verts:
            vmap[v] = dst.verts.new(v.co)
        dst.verts.index_update()
        new_faces = []
        for f in src_bm.faces:
            try:
                nf = dst.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue    # a duplicate face; skip
            nf.material_index = idx
            nf.smooth = smooth
            new_faces.append(nf)
        src_bm.free()
        self.parts.append((part_name, new_faces))
        return new_faces

    def finish(self, collection=None):
        """Write the mesh, make the object (origin at the world origin = bottom centre)."""
        bm = self.bm
        # Nothing below the floor: the end of a splayed leg is square to the leg, so its lower edge dips a
        # few millimetres under z = 0. Those points go onto the floor (the foot sits flat).
        self.floored = 0
        for v in bm.verts:
            if -0.02 < v.co.z < 0.0:
                v.co.z = 0.0
                self.floored += 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        box_uvs(bm)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


# ----------------------------------------------------------------------------- primitives (each returns a new bmesh)


def _xf(bm, mat):
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
    return bm


def box(size, centre=(0, 0, 0), rot=(0, 0, 0)):
    """A box of size (x, y, z) at centre, rotated by rot (degrees, XYZ)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    s = Matrix.Diagonal((*size, 1.0))
    r = euler_matrix(rot)
    return _xf(bm, Matrix.Translation(Vector(centre)) @ r @ s)


def span(z0, z1):
    """(centre, height) of the band from z0 to z1."""
    return (z0 + z1) / 2.0, (z1 - z0)


def zbox(x0, x1, y0, y1, z0, z1):
    """A box from its boundaries (never from eyeballed centres)."""
    return box((x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def euler_matrix(rot):
    rx, ry, rz = (math.radians(a) for a in rot)
    return (Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X'))


def ellipsoid(centre, radii, u=8, v=6, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1.0)
    return _xf(bm, Matrix.Translation(Vector(centre)) @ euler_matrix(rot) @ Matrix.Diagonal((*radii, 1.0)))


def cylinder(centre, radius, depth, sides=8, rot=(0, 0, 0), radius_top=None, scale_xy=(1.0, 1.0)):
    """A (tapered) cylinder along Z, centred at centre."""
    bm = bmesh.new()
    rt = radius if radius_top is None else radius_top
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=sides,
                          radius1=radius, radius2=rt, depth=depth)
    s = Matrix.Diagonal((scale_xy[0], scale_xy[1], 1.0, 1.0))
    return _xf(bm, Matrix.Translation(Vector(centre)) @ euler_matrix(rot) @ s)


def strut(p_top, p_bot, r_top, r_bot, sides=6):
    """A tapered member between two points (the furniture library's strut)."""
    pt, pb = Vector(p_top), Vector(p_bot)
    vec = pt - pb
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=sides,
                          radius1=r_bot, radius2=r_top, depth=vec.length)
    rot = vec.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return _xf(bm, Matrix.Translation((pb + pt) / 2) @ rot)


def at_z(p_top, p_bot, z):
    """Where a splayed member is at height z (never guess it)."""
    pt, pb = Vector(p_top), Vector(p_bot)
    return pb + (pt - pb) * ((z - pb.z) / (pt.z - pb.z))


def loft(rings, sides=8, cap_bottom=True, cap_top=True, tip=None, bottom_tip=None, phase=0.0):
    """
    A tube through elliptical rings (bottom to top).
    rings: [(cx, cy, cz, rx, ry)] with optional 6th and 7th values:
      twist  - degrees about the ring's own axis;
      tilt   - degrees about X, positive lifts the front (-Y) edge and lowers the back.
    tip / bottom_tip: a point that closes the top / bottom as a cone instead of a flat cap.
    """
    bm = bmesh.new()
    loops = []
    for ring in rings:
        cx, cy, cz, rx, ry = ring[:5]
        tw = math.radians(ring[5]) if len(ring) > 5 else 0.0
        tl = math.radians(ring[6]) if len(ring) > 6 else 0.0
        loop = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.radians(phase) + tw
            x, y = rx * math.cos(a), ry * math.sin(a)
            loop.append(bm.verts.new((cx + x, cy + y * math.cos(tl), cz - y * math.sin(tl))))
        loops.append(loop)
    for lo, hi in zip(loops, loops[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    if bottom_tip is not None:
        t = bm.verts.new(bottom_tip)
        lo = loops[0]
        for i in range(sides):
            bm.faces.new([lo[(i + 1) % sides], lo[i], t])
    elif cap_bottom:
        bm.faces.new(list(reversed(loops[0])))
    if tip is not None:
        t = bm.verts.new(tip)
        hi = loops[-1]
        for i in range(sides):
            bm.faces.new([hi[i], hi[(i + 1) % sides], t])
    elif cap_top:
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def lathe(profile, sides=10, phase=0.0, cap_bottom=True, cap_top=True):
    """A round solid from a profile [(radius, z)], bottom to top."""
    return loft([(0.0, 0.0, z, r, r) for r, z in profile], sides=sides, cap_bottom=cap_bottom,
                cap_top=cap_top, phase=phase)


def plane(size_x, size_y, centre=(0, 0, 0), rot=(0, 0, 0)):
    bm = bmesh.new()
    hx, hy = size_x / 2, size_y / 2
    vs = [bm.verts.new(p) for p in ((-hx, -hy, 0), (hx, -hy, 0), (hx, hy, 0), (-hx, hy, 0))]
    bm.faces.new(vs)
    return _xf(bm, Matrix.Translation(Vector(centre)) @ euler_matrix(rot))


def rbox(x0, x1, y0, y1, z0, z1, bevel=0.0, segments=1):
    """A box from its boundaries, its edges bevelled (chunky and friendly) when bevel > 0."""
    bm = zbox(x0, x1, y0, y1, z0, z1)
    if bevel > 0:
        bevel_all(bm, min(bevel, (x1 - x0) / 2.1, (y1 - y0) / 2.1, (z1 - z0) / 2.1), segments=segments)
    return bm


def turn(bm, degrees, axis='X', pivot=(0, 0, 0)):
    """Rotate a part about a pivot (the part's own junction, never its centre by eye)."""
    p = Vector(pivot)
    return _xf(bm, Matrix.Translation(p) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-p))


def move(bm, offset):
    return _xf(bm, Matrix.Translation(Vector(offset)))


def taper_box(x_front, z_front, x_back, z_back, y0, y1, zc):
    """A box that narrows from its front face (at y0) to its back (at y1), centred on x=0, z=zc."""
    bm = bmesh.new()
    f = [(-x_front / 2, y0, zc - z_front / 2), (x_front / 2, y0, zc - z_front / 2),
         (x_front / 2, y0, zc + z_front / 2), (-x_front / 2, y0, zc + z_front / 2)]
    b = [(-x_back / 2, y1, zc - z_back / 2), (x_back / 2, y1, zc - z_back / 2),
         (x_back / 2, y1, zc + z_back / 2), (-x_back / 2, y1, zc + z_back / 2)]
    vf = [bm.verts.new(p) for p in f]
    vb = [bm.verts.new(p) for p in b]
    bm.faces.new(vf)
    bm.faces.new(list(reversed(vb)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([vf[j], vf[i], vb[i], vb[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def open_box(x0, x1, y0, y1, z0, z1, wall, inward=False):
    """
    A box with no top: a bottom and four walls of thickness wall (a cardboard box, a drawer).
    inward=True instead gives only the inside faces, facing in (a sink bowl seen from above).
    """
    if inward:
        bm = bmesh.new()
        c = [bm.verts.new(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0))]
        t = [bm.verts.new(p) for p in ((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
        bm.faces.new(c)                                  # bottom, facing up
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new([c[j], c[i], t[i], t[j]])       # walls, facing in
        return bm
    bm = zbox(x0, x1, y0, y1, z0, z0 + wall)
    for part in (zbox(x0, x1, y0, y0 + wall, z0, z1), zbox(x0, x1, y1 - wall, y1, z0, z1),
                 zbox(x0, x0 + wall, y0, y1, z0, z1), zbox(x1 - wall, x1, y0, y1, z0, z1)):
        merge(bm, part)
    return bm


def merge(dst, src):
    """Append src's geometry to dst (src is freed)."""
    vmap = {v: dst.verts.new(v.co) for v in src.verts}
    for f in src.faces:
        try:
            dst.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            pass
    src.free()
    return dst


def bevel_all(bm, width, segments=1, angle=30.0):
    """A live-style bevel on the part's sharp edges (Limit: angle), applied here."""
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0.0) > math.radians(angle)]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=width, segments=segments, profile=0.5,
                        affect='EDGES', clamp_overlap=True)
    return bm


# ----------------------------------------------------------------------------- UVs (flat colours need none; cheap cube projection)


def box_uvs(bm, scale=1.0):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda k: abs(n[k]))
        for loop in f.loops:
            co = loop.vert.co
            if ax == 0:
                loop[uv].uv = (co.y * scale, co.z * scale)
            elif ax == 1:
                loop[uv].uv = (co.x * scale, co.z * scale)
            else:
                loop[uv].uv = (co.x * scale, co.y * scale)


# ----------------------------------------------------------------------------- the audit (Rule 4)


def islands(ob):
    """Connected pieces of the mesh, each as (min, max) bounds and its material names."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    seen = set()
    result = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        members = []
        while stack:
            cur = stack.pop()
            members.append(cur)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        lo = Vector((min(m.co.x for m in members), min(m.co.y for m in members), min(m.co.z for m in members)))
        hi = Vector((max(m.co.x for m in members), max(m.co.y for m in members), max(m.co.z for m in members)))
        mats = set()
        for m in members:
            for f in m.link_faces:
                mats.add(ob.data.materials[f.material_index].name if ob.data.materials else '?')
        result.append((lo, hi, sorted(mats), len(members)))
    bm.free()
    return result


def gap(a, b):
    (alo, ahi), (blo, bhi) = a, b
    return max(max(alo[k] - bhi[k], blo[k] - ahi[k]) for k in range(3))


def audit(ob, tolerance=0.002):
    """Every island's bounds must touch another island's (within 2 mm): nothing floats."""
    isl = islands(ob)
    floating = []
    if len(isl) > 1:
        for i, a in enumerate(isl):
            best = min(gap((a[0], a[1]), (b[0], b[1])) for j, b in enumerate(isl) if j != i)
            if best > tolerance:
                floating.append((i, a[2], round(best * 1000, 1)))
    return len(isl), floating


def stats(ob):
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    return {
        'tris': tris,
        'size': (round(max(xs) - min(xs), 3), round(max(ys) - min(ys), 3), round(max(zs) - min(zs), 3)),
        'min': (round(min(xs), 3), round(min(ys), 3), round(min(zs), 3)),
        'max': (round(max(xs), 3), round(max(ys), 3), round(max(zs), 3)),
        'materials': [m.name for m in me.materials],
    }


# ----------------------------------------------------------------------------- scene, renders and export


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.images):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def studio(scene=None, strength=1.0, world='#D9D6CF'):
    """A soft grey world, a warm key sun and a cool fill, for review renders only."""
    scene = scene or bpy.context.scene
    w = bpy.data.worlds.get('Review') or bpy.data.worlds.new('Review')
    scene.world = w
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    if bg is not None:
        bg.inputs['Color'].default_value = (*rgb(world), 1.0)
        bg.inputs['Strength'].default_value = 0.9 * strength
    for name, energy, rot, colour in (('Key', 3.2, (50, 0, 35), '#FFF1DE'), ('Fill', 0.9, (65, 0, -140), '#DDE6FF')):
        ld = bpy.data.lights.get(name) or bpy.data.lights.new(name, 'SUN')
        ld.energy = energy * strength
        ld.color = rgb(colour)
        ld.angle = math.radians(8)
        ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, ld)
        if ob.name not in scene.collection.objects:
            scene.collection.objects.link(ob)
        ob.rotation_euler = tuple(math.radians(a) for a in rot)


def camera(name, location, target, lens=50.0, ortho=None, scene=None):
    scene = scene or bpy.context.scene
    cd = bpy.data.cameras.get(name) or bpy.data.cameras.new(name)
    cd.lens = lens
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    else:
        cd.type = 'PERSP'
    cd.clip_start = 0.01
    cd.clip_end = 200
    ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, cd)
    if ob.name not in scene.collection.objects:
        scene.collection.objects.link(ob)
    ob.location = location
    d = Vector(target) - Vector(location)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return ob


def look_from(target, distance, elevation_deg, azimuth_deg):
    """A camera position around target: azimuth 0 = in front (-Y), positive turns to the prop's left (+X)."""
    el, az = math.radians(elevation_deg), math.radians(azimuth_deg)
    t = Vector(target)
    return (t.x + distance * math.cos(el) * math.sin(az),
            t.y - distance * math.cos(el) * math.cos(az),
            t.z + distance * math.sin(el))


def render(path, cam, res=(900, 900), samples=48, scene=None, transparent=False):
    scene = scene or bpy.context.scene
    scene.camera = cam
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    try:
        scene.cycles.use_denoising = True
    except Exception:
        pass
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    return path


def export_fbx(ob, path):
    """
    One prop to FBX the way the café's props were exported, with the axis turn baked in. It goes out
    from a temporary copy at the origin (sharing the mesh), so a layout in the .blend never moves it.
    """
    os.makedirs(os.path.dirname(path), exist_ok=True)
    name = ob.name
    ob.name = name + '.layout'
    tmp = bpy.data.objects.new(name, ob.data)
    bpy.context.scene.collection.objects.link(tmp)
    try:
        bpy.context.view_layer.update()
        for o in bpy.data.objects:
            try:
                o.select_set(o == tmp)
            except RuntimeError:
                pass            # not in this view layer
        bpy.context.view_layer.objects.active = tmp
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
            add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode='AUTO',
            use_triangles=False, embed_textures=False)
    finally:
        bpy.data.objects.remove(tmp, do_unlink=True)
        ob.name = name
    return path
