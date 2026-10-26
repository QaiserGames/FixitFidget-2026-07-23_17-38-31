"""
The patio A-frame (Assets/Art/Models/MenuBoard.fbx), rebuilt as a true A (24 Oct 2026, art audit a14: "Only the
chalkboard side stands up. The plain back leans behind it with its foot off the ground, and the two never meet at the
top"; the fix, which he said yes to: "rebuild it as a true A: both sides leaning from one hinge, both feet down, a chalk
face on each side").

MenuBoard.fbx is one of the café's first models, made before the house scripts, and it had no script; this one makes its
replacement, keeping everything the project knows it by:
  * the object Decor_MenuBoard.001 and its mesh Cube.002 (Unity names the model's parts after them, so the scene's
    references, its grain copy "Cube.002 - grain UV" in Assets/Art/CafeFurnishing/Furnishing meshes.asset and its
    material overrides stay attached);
  * its three materials in their order: CC_Wood_Counter (the frame), DC_Chalkboard (the boards), DC_Chalk. Street 1
    (CafeStreetSteps) finds the boards and the chalk by these names and painted the DC_Chalk slot with the chalkboard
    when the chalk words went on; the chalk slot now carries the A's iron: the hinges at the top and the two stays
    that hold the legs apart (dark once painted, which is right for iron);
  * the old way of exporting: Z up, the axis turn left on the object (-90 degrees about X, not baked into the mesh), at
    the size it was drawn, 1/4.2 of the real thing (CafeFurnishing places it at scale 4.2); the origin at the bottom
    centre, the street side toward -Y (Unity +Z before the scene's turn).

Real sizes (x 4.2): two leaves 0.67 m wide meeting at a hinge 0.93 m up, each leaning 14 degrees, so the feet stand
0.51 m apart, flat on the pavement; each leaf a 40 mm wood frame 22 mm thick round a chalkboard sunk 4 mm into it; a
CK_AFrameFace goes on each board (ArtFixBlenderSteps). Nothing floats; no two faces in one plane.

With bpy:   python3 cafe_aframe.py [-- <fbx path> <blend path>]
Defaults in the repo: Assets/Art/Models/MenuBoard.fbx and BlenderSource/CafeAFrame_v2.blend.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402

K = 4.2                                 # CafeFurnishing's scale for it: the model is drawn at 1/4.2
W = .672 / K                            # a leaf's width (the old board's 0.16)
HT = .2223                              # the top of the A (the old board's height: 0.934 m)
LEAN = math.radians(14.0)
T = .022 / K                            # a leaf's thickness
F = .040 / K                            # the frame's width
RECESS, PANEL, EMBED = .004 / K, .008 / K, .002 / K   # the board: 4 mm under the frame's face, 8 mm thick, 2 mm into it
D2 = HT * math.tan(LEAN) + T / math.cos(LEAN)          # half the feet's spread: the leaves' inner faces meet at the top

MATERIALS = [('CC_Wood_Counter', '#8F5B32', 0.40), ('DC_Chalkboard', '#2B2B28', 0.25), ('DC_Chalk', '#E8E4D8', 0.10)]


def mat(name):
    for n, hexstr, smooth in MATERIALS:
        if n == name:
            return fb.material(n, hexstr, rough=1.0 - smooth)
    raise KeyError(name)


def point(side, u, s, w):
    """A point of a leaf: u across it (x), s up along it from its foot, w into it from its outer face. side -1 is the
    street leaf (its face toward -Y), +1 the café leaf."""
    return (u, side * (D2 - s * math.sin(LEAN) - w * math.cos(LEAN)), s * math.cos(LEAN) - w * math.sin(LEAN))


def s_foot(w):
    return w * math.tan(LEAN)                                 # the foot cut level: z 0 across the leaf's thickness


def s_top(w):
    return (HT + w * math.sin(LEAN)) / math.cos(LEAN)         # the top cut level: z HT


def frame(side):
    """A leaf's wood frame: one closed shell, a rectangle with the board's opening through it."""
    bm = fb.bmesh.new()
    hw, iw = W / 2, W / 2 - F
    s0, s1 = F, s_top(0) - F                                  # the opening, square to the leaf
    rings = {}
    for w in (0.0, T):
        outer = [(-hw, s_foot(w)), (hw, s_foot(w)), (hw, s_top(w)), (-hw, s_top(w))]
        inner = [(-iw, s0), (iw, s0), (iw, s1), (-iw, s1)]
        rings[w] = ([bm.verts.new(point(side, u, s, w)) for u, s in outer], [bm.verts.new(point(side, u, s, w)) for u, s in inner])
    for w in (0.0, T):
        o, i = rings[w]
        for k in range(4):
            j = (k + 1) % 4
            bm.faces.new([o[k], o[j], i[j], i[k]])
    for which in (0, 1):                                       # the outer edges, then the opening's sides
        a, b = rings[0.0][which], rings[T][which]
        for k in range(4):
            j = (k + 1) % 4
            bm.faces.new([a[k], a[j], b[j], b[k]])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def board(side):
    """The chalkboard in a leaf's opening: a thin slab sunk RECESS under the frame's face, its edges into the frame."""
    iw = W / 2 - F + EMBED
    s0, s1 = F - EMBED, s_top(0) - F + EMBED
    bm = fb.bmesh.new()
    vs = [bm.verts.new(point(side, u, s, w)) for w in (RECESS, RECESS + PANEL) for u, s in ((-iw, s0), (iw, s0), (iw, s1), (-iw, s1))]
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[k] for k in f])
    fb.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def build():
    """The A-frame as one Prop (its slots in order of first use: the wood, the chalkboard, the chalk slot's iron)."""
    p = fb.Prop('Decor_MenuBoard.001')
    wood, slate, iron = mat('CC_Wood_Counter'), mat('DC_Chalkboard'), mat('DC_Chalk')
    for side in (-1, 1):
        p.add('leaf frame', frame(side), wood)
    for side in (-1, 1):
        p.add('chalkboard', board(side), slate)
    # the hinges: two barrels along the top, half sunk into the leaves' top edges
    for x in (-(W / 2 - .09 / K), W / 2 - .09 / K):
        p.add('hinge', fb.cylinder((x, 0.0, HT), .007 / K, .07 / K, sides=8, rot=(0, 90, 0)), iron)
    # the stays: a flat bar a side inside the A, 0.40 m up, from one leaf's inner face into the other's
    zs = .40 / K
    reach = (HT - zs) * math.tan(LEAN) + .006 / K
    for x in (-(W / 2 - .005 / K), W / 2 - .005 / K):
        p.add('stay', fb.zbox(x - .0017 / K, x + .0017 / K, -reach, reach, zs - .010 / K, zs + .010 / K), iron)
    return p


def export(ob, path):
    """As the old file went out: the axis turn on the object (bake_space_transform off), Z-up geometry, the same units."""
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
    return path


if __name__ == '__main__':
    IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
    REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    fbx_path = args[0] if args else (os.path.join(REPO, 'Assets', 'Art', 'Models', 'MenuBoard.fbx') if IN_REPO else os.path.join(HERE, 'MenuBoard.fbx'))
    blend_path = args[1] if len(args) > 1 else (os.path.join(REPO, 'BlenderSource', 'CafeAFrame_v2.blend') if IN_REPO
                                                else os.path.join(HERE, 'CafeAFrame_v2.blend'))
    fb.clear_scene()
    ob = build().finish()
    ob.data.name = 'Cube.002'
    n, floating = fb.audit(ob)
    st = fb.stats(ob)
    try:
        import cafe_seating
        same, opposite, examples = cafe_seating.coplanar_overlaps(ob)
    except Exception as e:                                     # the check is a help, not a gate
        same, opposite, examples = '?', '?', [str(e)]
    degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
    print('Decor_MenuBoard.001 (mesh Cube.002): %d tris, size %s (x %.1f: %s m), %d islands, floating %s, coplanar same %s '
          'opposite %s, degenerate %d, materials %s' % (st['tris'], st['size'], K, tuple(round(v * K, 3) for v in st['size']),
                                                       n, floating or 'none', same, opposite, degenerate, [m.name for m in ob.data.materials]))
    export(ob, fbx_path)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    print('FBX %s; blend %s' % (fbx_path, blend_path))
