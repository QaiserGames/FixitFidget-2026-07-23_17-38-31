"""
Build the hero devices (hero_phone.py, hero_watch.py, hero_camera.py) and write one FBX each to Assets/Art/Models/Devices/
(HD_Phone.fbx, HD_Watch.fbx, HD_Camera.fbx: the main piece as the root, the other pieces as its children where the prefab has
them), the .blend, and the manifest the Unity steps read (devices.json).

v3 (14 Oct 2026, the TD's one scale, specs/td-one-scale.md §6.1-6.3): every FBX is REAL SIZE, 1 unit = 1 m. Each script
keeps its AUTHORED_SCALE (camera 2, watch 3, phone 1) and its build() scales the meshes and the children's places by
1 / AUTHORED_SCALE (hero_lib.scale_build). devices.json gains "units": "real", each piece's "authoredScale", the screws'
sizes, the prefab places each script moves (today's value, the bake's x k, the new value) and the camera's new objects;
and the audit gains the REAL table: a piece whose size lies outside its real size +-7 % is a problem (a 2x or 3x export
is caught at once).

With bpy:  python Tools/Blender/export_hero_devices.py [-- <fbx folder> <blend folder>]
Headless:  blender -b --python Tools/Blender/export_hero_devices.py [-- <fbx folder> <blend folder>]
Then in Unity: Fixit Fidget > Bench > Devices 1 - Import and check the hero devices, then Devices 2 (real size).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import hero_lib as hl  # noqa: E402
import hero_camera  # noqa: E402
import hero_phone  # noqa: E402
import hero_watch  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'Devices')
blend_dir = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource')

KITS = [(hero_phone, 'HeroPhone_v3.blend', 'the hero phone: body, screen, back cover and a screw, assembled as the prefab has them'),
        (hero_watch, 'HeroWatch_v3.blend', 'the hero pocket watch: case, back plate, crown, a screw and the mainspring, assembled as the prefab has them'),
        (hero_camera, 'HeroCamera_v3.blend', "Grace's camera, hero: body, lens, glass, film path, the shutter (jammed and clean) and blades, the strap's three pieces, assembled as the prefab has them")]

TOLERANCE = 0.07
# The REAL table (TD §3's "after" column and the references it names; mm along Unity's axes, measured on the exported,
# real-size mesh). (root, piece): {axis: (mm, source, parts or None)}; axis 'xz' is the larger of x and z. A part list
# measures only those named parts of the piece (hero_lib.PART_FACES), where the bounds would count knobs and lugs.
REAL = {
    ('HD_Phone', 'HD_Phone'): {'x': (71.6, 'iPhone 15 width (sheet §6)'), 'y': (7.8, 'iPhone 15 thickness'), 'z': (147.6, 'iPhone 15 height')},
    ('HD_Phone', 'ScreenCracked'): {'x': (64.0, 'the display glass ≈ 64 x 140 (TD §3)'), 'z': (140.0, 'the display glass')},
    ('HD_Phone', 'Screen'): {'x': (64.0, 'the display glass'), 'z': (140.0, 'the display glass')},
    ('HD_Phone', 'ScreenPlain'): {'x': (64.0, 'the display glass'), 'z': (140.0, 'the display glass')},
    ('HD_Phone', 'BackCover'): {'x': (69.6, 'the back glass: iPhone 15 less its 1 mm rail'), 'z': (145.6, 'the back glass')},
    ('HD_Phone', 'Screw'): {'x': (3.8, 'head Ø 3.8: an M2 pan head (ISO 7045 dk 3.7-4.0); feel-targets call 3'), 'z': (3.8, 'head Ø')},
    ('HD_Watch', 'HD_Watch'): {'x': (50.0, 'open-face case Ø 48-52 (sheet §7a)'), 'z': (66.7, 'the case with its pendant and bow, a third of the Ø more')},
    ('HD_Watch', 'BackPlate'): {'x': (43.0, 'back Ø ≈ 43 (TD §3)'), 'z': (43.0, 'back Ø')},
    ('HD_Watch', 'Crown'): {'x': (8.0, 'crown Ø 7-9 (TD §3)')},
    ('HD_Watch', 'Screw'): {'x': (3.8, 'head Ø 3.8 (feel-targets call 3)'), 'z': (3.8, 'head Ø')},
    ('HD_Watch', 'Mainspring'): {'x': (17.3, 'mainspring coil Ø ≈ 15-17 (TD §3: 17.3)'), 'z': (17.3, 'coil Ø')},
    ('HD_Watch', 'MainspringBroken'): {'xz': (17.3, 'the same spring, snapped and sprung')},
    ('HD_Camera', 'HD_Camera'): {'x': (129.0, 'Olympus 35 SP overall width (Canonet QL17 GIII 120 without lugs)'),
                                 'y': (32.0, 'body depth without the lens (sheet §8; Art A6)', ('body end', 'body lens side')),
                                 'z': (76.0, 'height of the plates (Olympus 35 SP ≈ 76; Art A6 keeps 80, +6 %)',
                                       ('top band, opening end', 'top band, lens side', 'bottom plate'))},
    ('HD_Camera', 'LensHousing'): {'x': (38.0, "AS BUILT: Ø 38, the TD's 0.73x-real flag (a real 40 mm f/1.7 barrel is Ø 50-52), kept at Art's call")},
    ('HD_Camera', 'LensGlass'): {'x': (24.0, "a 40 mm f/1.7's front element ≈ Ø 24 (hero_camera.lens_glass)")},
    ('HD_Camera', 'Mechanism'): {'x': (36.5, 'the shutter chassis 36.5 x 24.5 (TD §3)', ('chassis',)), 'z': (24.5, 'the chassis', ('chassis',))},
    ('HD_Camera', 'CleanMechanism'): {'x': (36.5, 'the same chassis', ('chassis',)), 'z': (24.5, 'the same chassis', ('chassis',))},
    ('HD_Camera', 'WornLoop'): {'z': (55.0, "Art A6: the loop lies 30-85 mm beyond the body's far edge")},
}


def unity_bounds(ob, faces=None):
    """The piece's own mesh bounds in Unity axes (mm): x = -Blender x, y = Blender z, z = -Blender y (sizes only)."""
    me = ob.data
    if faces is None:
        vs = [v.co for v in me.vertices]
    else:
        ids = set()
        for f in faces:
            ids.update(me.polygons[f].vertices)
        vs = [me.vertices[i].co for i in ids]
    if not vs:
        return None
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return {'x': (hi.x - lo.x) * 1000, 'y': (hi.z - lo.z) * 1000, 'z': (hi.y - lo.y) * 1000}


def real_check(root_name, ob):
    """[(axis, built mm, real mm, ratio, ok, source)] for this piece's REAL entry."""
    out = []
    for axis, spec in REAL.get((root_name, ob.name), {}).items():
        ref, source = spec[0], spec[1]
        parts = spec[2] if len(spec) > 2 else None
        faces = None
        if parts:
            pf = hl.PART_FACES.get(ob.name, {})
            faces = [i for p in parts for i in pf.get(p, [])]
            if not faces:
                out.append((axis, None, ref, None, False, source + ' (parts not found: %s)' % (parts,)))
                continue
        b = unity_bounds(ob, faces)
        built = max(b['x'], b['z']) if axis == 'xz' else b[axis]
        ratio = built / ref
        out.append((axis, round(built, 2), ref, round(ratio, 3), abs(ratio - 1.0) <= TOLERANCE, source))
    return out


def screw_rest(root, made, screw_name, cover_name, places):
    """For each screw place (real m, Unity, the prefab's frame relative to the root), the gap between the head's underside and
    the surface under it (the cover), by a ray up the screw's axis through the cover's mesh. + is a gap, - sunk."""
    import bmesh as _bm
    from mathutils.bvhtree import BVHTree
    cover = next(ob for ob, _t in made if ob.name == cover_name)
    screw = next(ob for ob, _t in made if ob.name == screw_name)
    bpy.context.view_layer.update()
    bm = _bm.new()
    bm.from_mesh(cover.data)
    bm.transform(cover.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    zs = [v.co.z for v in screw.data.vertices]
    head_h = hero_phone.SCREW_HEAD * 0.35
    out = []
    root_y = root.matrix_world.translation.z
    for (x, y, z) in places:
        under = y + head_h / 2                                    # the underside's Unity y (the shank goes +y)
        origin = Vector((-x, -z, root_y + under - 0.010))          # Blender: from 10 mm out, looking up (+Blender z = +Unity y)
        hit = tree.ray_cast(origin, Vector((0, 0, 1)), 0.05)
        if hit[0] is None:
            out.append(None)
            continue
        out.append(round((hit[0].z - (root_y + under)) * 1000, 3))
    bm.free()
    return out


if __name__ == '__main__':
    pieces = []
    materials = {}
    problems = 0
    real_lines = []
    for kit, blend_name, what in KITS:
        fb.clear_scene()                     # each kit in its own scene and .blend (the pieces share names: Screw, Fresh)
        hl.PART_FACES.clear()
        root, children, made = kit.build()   # real size (scale_build inside)
        bpy.context.view_layer.update()
        tris = 0
        mats = []
        lo = Vector((1e9, 1e9, 1e9))
        hi = Vector((-1e9, -1e9, -1e9))
        piece_real = {}
        for ob, targets in made:
            n, floating = fb.audit(ob)
            if floating:
                problems += 1
                print('FLOATING', ob.name, floating)
            degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
            if degenerate:
                problems += 1
                print('DEGENERATE', ob.name, degenerate)
            for axis, built, ref, ratio, ok, source in real_check(root.name, ob):
                line = '%-10s %-16s %-2s built %7s mm  real %6.1f mm  x%s  %s  (%s)' % (root.name, ob.name, axis, built, ref, ratio, 'ok' if ok else 'PROBLEM', source)
                real_lines.append(line)
                piece_real.setdefault(ob.name, []).append({'axis': axis, 'built_mm': built, 'real_mm': ref, 'ratio': ratio, 'ok': ok, 'source': source})
                if not ok:
                    problems += 1
            st = fb.stats(ob)
            tris += st['tris']
            mats += [m for m in st['materials'] if m not in mats]
            for v in ob.data.vertices:
                w = (ob.matrix_local @ v.co) if ob.parent is not None else v.co
                lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
                hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
            print('%-16s %5d tris  %s  -> %s' % (ob.name, st['tris'], st['size'], ', '.join(targets)))
        extra = {}
        if kit is hero_phone:
            extra['screws'] = {'head_d': hero_phone.SCREW_HEAD, 'head_h': round(hero_phone.SCREW_HEAD * .35, 5), 'shank_d': round(hero_phone.SCREW_HEAD * .55, 5),
                               'shank_len': hero_phone.SCREW_SHANK_LEN, 'material': 'HD_ScrewBlack',
                               'rest_gap_mm': screw_rest(root, made, 'Screw', 'BackCover', [m[2] for m in hero_phone.MOVES])}
        if kit is hero_watch:
            extra['screws'] = {'head_d': hero_watch.SCREW_HEAD, 'head_h': round(hero_watch.SCREW_HEAD * .35, 5), 'shank_d': round(hero_watch.SCREW_HEAD * .55, 5),
                               'shank_len': round(2 * hero_watch.SCREW_HEAD, 5), 'material': 'HD_ScrewBlued',
                               'rest_gap_mm': screw_rest(root, made, 'Screw', 'BackPlate', [m[2] for m in hero_watch.MOVES])}
        if kit is hero_camera:
            extra['newObjects'] = [{'name': 'Clean shutter mechanism', 'source': 'CleanMechanism', 'createUnder': 'GraceReunionCamera',
                                    'at': [0.0265, 0.039, 0.0205], 'active': False,
                                    'why': "Art A7's fresh visual: the jammed mechanism's twin, in the jammed one's seat, off until Systems presents it on Pad_Small"}]
            extra['scratches'] = [{'name': 'Old scratch %d' % i, 'today': [-0.2, 0.032, round(-0.027 + i * 0.018, 3)], 'at': list(p)}
                                  for i, p in enumerate(hero_camera.scratch_places())]
            extra['strapNoCollider'] = ['Upper strap', 'Lower strap', 'Worn loop', 'Old scratch 0', 'Old scratch 1', 'Old scratch 2']
        moves = [{'name': n_, 'today': list(t_), 'baked': [round(c * (1.0 / kit.AUTHORED_SCALE), 6) for c in t_], 'to': list(to_), 'why': why}
                 for (n_, t_, to_, why) in getattr(kit, 'MOVES', [])]
        path = os.path.join(fbx_dir, root.name + '.fbx')
        fb.export_fbx_tree(root, path)
        # the lowest point of the assembly (prefab root frame for the camera: its body sits 16 mm up)
        body_y = getattr(kit, 'BODY_Y', 0.0) / getattr(kit, 'AUTHORED_SCALE', 1.0)
        pieces.append({
            'name': root.name, 'tool': '', 'room': '', 'what': what,
            'authoredScale': kit.AUTHORED_SCALE,
            'tris': tris, 'meshes': 1 + len(children),
            'children': [{'name': c.name, 'at': [round(-c.location.x, 5), round(c.location.z, 5), round(-c.location.y, 5)],
                          'turned': any(abs(a) > 1e-6 for a in c.rotation_euler)} for c in children],
            'size': [round(hi.x - lo.x, 5), round(hi.z - lo.z, 5), round(hi.y - lo.y, 5)],
            'bottom': round(lo.z, 5),
            'lowestInPrefab': round(lo.z + body_y, 5),
            'materials': mats, 'islands': 0,
            'targets': {ob.name: targets for ob, targets in made},
            'real': piece_real,
            'moves': moves,
            **extra,
        })
        for name, (hexstr, smooth, metal) in kit.HD_MATERIALS.items():
            materials[name] = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': metal}
        os.makedirs(blend_dir, exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, blend_name), compress=True)
        print('%s: %d triangles, %d meshes -> %s; %s' % (root.name, tris, 1 + len(children), path, blend_name))
    os.makedirs(fbx_dir, exist_ok=True)
    manifest = {
        'version': 3, 'units': 'real',
        'note': ("v3 (14 Oct 2026, TD one-scale §6.3): every FBX is REAL SIZE, 1 unit = 1 m (each script's authoredScale is "
                 "undone at export by hero_lib.scale_build). 'moves' are prefab places at real size (Unity, the prefab root's "
                 "frame): Devices 2 bakes today's places x 1/authoredScale, then sets each 'to'. 'real' is the REAL audit "
                 "(+-7 %). Screw heads are 3.8 mm (feel-targets call 3); their holes are BT_ScrewHole.png on a quad 1.15 x "
                 "the head (Resources 'Bench/BT_ScrewHole')."),
        'screwHole': {'texture': 'Bench/BT_ScrewHole', 'quad': 1.15, 'bore': 0.48, 'lip': 0.55, 'proud_m': 0.00005},
        'pieces': pieces, 'materials': materials}
    with open(os.path.join(fbx_dir, 'devices.json'), 'w') as fh:
        json.dump(manifest, fh, indent=1, ensure_ascii=False)
    print('REAL table (+-%d %%):' % round(TOLERANCE * 100))
    print('\n'.join(real_lines))
    print('%d devices; %d problem(s)' % (len(pieces), problems))
