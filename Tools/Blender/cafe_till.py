"""
The till's labels (24 Oct 2026, art audit a34: "The keys are blank white and the screen is an empty green panel, on an
otherwise good till"; the fix, which he said yes to: "key labels and a price on the screen").

The till (Assets/Art/Models/CashRegister.fbx, "Register v4" in the café) is one of the café's first models, made before
the house scripts, and it has no script of its own; its shape is good, so this does not rebuild it. It opens the till as it
is, changes only what the finding is about, and writes it back in its old form:
  * the twenty key caps' tops (5 across, 4 rows) take a new material, CF_TillKeys (cafe_art_fix_textures.py draws its
    atlas, CF_TillKeys.png), each top mapped to its own label's cell, upright for Ace (the keys face him; the tower faces
    the customers); the caps' sides stay DC_Ceramic;
  * the two displays' faces, Ace's on the tower's front and the customers' on its back, take CF_TillScreen (a price in
    seven-segment digits, CF_TillScreen.png), each mapped upright for the one who reads it; the panels' edges stay
    DC_Note_Green;
  * nothing else changes: not a vertex moves, the seven old materials keep their order and the two new ones come after
    them, and the object (Equip_Register.001), its mesh (RG_Plinth.002) and the old export (Z up, the axis turn left on
    the object, not baked) are as they were, so Unity reimports it in place.
Run on its own output it does the same again (the faces are found by where they are, not by their material).

With bpy:   python3 cafe_till.py [-- <the till's FBX> <out FBX> <blend path>]
Defaults in the repo: Assets/Art/Models/CashRegister.fbx in and out, BlenderSource/CafeTill_v5.blend.
The key labels' layout (TILL_KEYS, till_cell) lives here, for the picture and the mapping to agree.
"""
import math
import os
import sys

# The keypad as Ace sees it: row 0 the farthest from him, column 0 on his left. TOTAL in Ace's green (the picture).
TILL_KEYS = [['7', '8', '9', 'CLR', 'VOID'],
             ['4', '5', '6', 'TAX', 'SUB'],
             ['1', '2', '3', 'CARD', 'CASH'],
             ['0', '00', '.', 'NO SALE', 'TOTAL']]
TILL_ATLAS = (1024, 512)           # CF_TillKeys.png: 5 x 4 cells of 204.8 x 128 px (a cap is 46 x 30 mm: about 1.5 to 1)
CELL_INSET = 6                     # px kept clear inside each cell's edge


def till_cell(row, col):
    """Key (row, col)'s label in CF_TillKeys.png, as UVs (u0, v0, u1, v1), inset CELL_INSET px; row 0 at the top."""
    w, h = TILL_ATLAS
    rows, cols = len(TILL_KEYS), len(TILL_KEYS[0])
    cw, ch = w / cols, h / rows
    return ((col * cw + CELL_INSET) / w, (h - (row + 1) * ch + CELL_INSET) / h,
            ((col + 1) * cw - CELL_INSET) / w, (h - row * ch - CELL_INSET) / h)


def _components(bm, faces):
    """The faces split into connected pieces (faces sharing a vertex)."""
    faces = set(faces)
    seen, out = set(), []
    for f in faces:
        if f in seen:
            continue
        comp, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            comp.append(g)
            for v in g.verts:
                for h in v.link_faces:
                    if h in faces and h not in seen:
                        seen.add(h)
                        stack.append(h)
        out.append(comp)
    return out


def _map(faces, uv, u_axis, v_axis, rect):
    """Faces projected on (u_axis, v_axis) and fitted into rect (u0, v0, u1, v1)."""
    pts = [l.vert.co for f in faces for l in f.loops]
    us, vs = [p.dot(u_axis) for p in pts], [p.dot(v_axis) for p in pts]
    u_lo, u_hi, v_lo, v_hi = min(us), max(us), min(vs), max(vs)
    for f in faces:
        for l in f.loops:
            a = (l.vert.co.dot(u_axis) - u_lo) / max(1e-9, u_hi - u_lo)
            b = (l.vert.co.dot(v_axis) - v_lo) / max(1e-9, v_hi - v_lo)
            l[uv].uv = (rect[0] + a * (rect[2] - rect[0]), rect[1] + b * (rect[3] - rect[1]))


def label_till(ob):
    """The keys' tops and the displays' faces onto the two new materials, mapped; returns what it found."""
    import bmesh
    import bpy
    from mathutils import Vector
    me = ob.data
    names = [m.name.split('.')[0] for m in me.materials]
    for new, hexstr in (('CF_TillKeys', '#F2EFE8'), ('CF_TillScreen', '#A9CF8E')):
        if new not in names:
            m = bpy.data.materials.get(new) or bpy.data.materials.new(new)
            m.diffuse_color = tuple(int(hexstr[i:i + 2], 16) / 255.0 for i in (1, 3, 5)) + (1.0,)
            me.materials.append(m)
            names.append(new)
    ceramic, keys = names.index('DC_Ceramic'), names.index('CF_TillKeys')
    green, screen = names.index('DC_Note_Green'), names.index('CF_TillScreen')
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new('UVMap')
    caps = _components(bm, [f for f in bm.faces if f.material_index in (ceramic, keys)])
    if len(caps) != 20:
        raise RuntimeError('expected the twenty key caps, found %d pieces' % len(caps))
    centres = [sum((f.calc_center_median() for f in c), Vector()) / len(c) for c in caps]
    ys = sorted({round(c.y, 3) for c in centres})            # the rows: the farthest from Ace (lowest y) first
    xs = sorted({round(c.x, 3) for c in centres}, reverse=True)   # the columns: Ace's left (+x) first
    if len(ys) != 4 or len(xs) != 5:
        raise RuntimeError('the caps are not 5 x 4 (rows %d, columns %d)' % (len(ys), len(xs)))
    u_axis = Vector((-1.0, 0.0, 0.0))                          # to Ace's right: he stands at +y, facing -y
    for comp, c in zip(caps, centres):
        row, col = ys.index(round(c.y, 3)), xs.index(round(c.x, 3))
        tops = [f for f in comp if f.normal.z > 0.5]
        for f in comp:
            f.material_index = keys if f in tops else ceramic
        n = sum((f.normal for f in tops), Vector()).normalized()
        _map(tops, uv, u_axis, n.cross(u_axis).normalized(), till_cell(row, col))   # up = away from Ace, along the slope
    panels = _components(bm, [f for f in bm.faces if f.material_index in (green, screen)])
    if len(panels) != 2:
        raise RuntimeError('expected the two displays, found %d pieces' % len(panels))
    found = []
    for comp in panels:
        c = sum((f.calc_center_median() for f in comp), Vector()) / len(comp)
        ace = c.y > -0.1                                       # Ace's display is on the tower's front (y -0.047), facing +y
        facing = Vector((0.0, 1.0, 0.0)) if ace else Vector((0.0, -1.0, 0.0))
        face = [f for f in comp if f.normal.dot(facing) > 0.7]
        for f in comp:
            f.material_index = screen if f in face else green
        n = sum((f.normal for f in face), Vector()).normalized()
        right = Vector((-1.0, 0.0, 0.0)) if ace else Vector((1.0, 0.0, 0.0))
        _map(face, uv, right, n.cross(right).normalized(), (0.0, 0.0, 1.0, 1.0))
        found.append(('Ace' if ace else 'the customers', len(face)))
    bm.to_mesh(me)
    bm.free()
    return {'caps': len(caps), 'cap tops': sum(1 for p in me.polygons if p.material_index == keys), 'displays': found,
            'materials': [m.name for m in me.materials]}


def export_old_way(ob, path):
    """As the old file went out: the axis turn on the object (bake_space_transform off), Z-up geometry, the same units."""
    import bpy
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for o in bpy.data.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode='AUTO',
        use_triangles=False, embed_textures=False)


if __name__ == '__main__':
    import bpy
    HERE = os.path.dirname(os.path.abspath(__file__))
    REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    src = args[0] if args else os.path.join(REPO, 'Assets', 'Art', 'Models', 'CashRegister.fbx')
    out = args[1] if len(args) > 1 else src
    blend_path = args[2] if len(args) > 2 else os.path.join(REPO, 'BlenderSource', 'CafeTill_v5.blend')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    till = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(till) != 1 or till[0].name != 'Equip_Register.001':
        raise RuntimeError('%s is not the till (one mesh, Equip_Register.001): %s' % (src, [o.name for o in till]))
    ob = till[0]
    before = (len(ob.data.vertices), len(ob.data.polygons))
    info = label_till(ob)
    ob.data.name = 'RG_Plinth.002'
    print('the till: %d vertices, %d faces (unchanged: %s); %s' % (len(ob.data.vertices), len(ob.data.polygons),
                                                                   before == (len(ob.data.vertices), len(ob.data.polygons)), info))
    export_old_way(ob, out)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    print('FBX %s; blend %s' % (out, blend_path))
