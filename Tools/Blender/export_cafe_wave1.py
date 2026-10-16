"""
Build the café's wave-1 dressing (cafe_repairside.py: R1-R5, S1-S2; cafe_cafeside.py: C1-C3), audit it, and write one
FBX per piece, the .blend, the pictures (cafe_wave1_textures.py, needs Pillow) and the manifest the Unity step reads
(cafe_dressing.json, cafe_livedin.json's form: each piece's size in Unity axes (across, up, deep), bottom, back,
triangles, materials and islands as built, where it goes (world position and rotation), its origin, its textured face
sets; the new materials with colour, smoothness and texture, the existing ones it reuses by name).

With bpy:   python3 export_cafe_wave1.py [-- <fbx folder> <blend path> <texture folder>]
Headless:   blender -b --python export_cafe_wave1.py [-- ...]   (draw the pictures first with python3 cafe_wave1_textures.py)
Defaults (run from the repository's Tools/Blender): Assets/Art/Models/CafeKit/, BlenderSource/CafeDressing_v1.blend,
Assets/Art/Textures/Cafe/.

The audit (failures counted in the last line's problems): nothing floating (every island within 2 mm of another), no
degenerate faces, no two faces in one plane facing the same way, nothing of a floor piece below its floor, every textured
face set mapped.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import cafe_repairside as rs  # noqa: E402
import cafe_cafeside as cs  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'CafeKit')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'CafeDressing_v1.blend')
tex_dir = args[2] if len(args) > 2 else os.path.join(REPO, 'Assets', 'Art', 'Textures', 'Cafe')
ALL = rs.PIECES + cs.PIECES


def build_all():
    import cafe_seating as seat
    out = []
    for builder, group, what, place, origin in ALL:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        same, opp, ex = seat.coplanar_overlaps(ob)
        degenerate = sum(1 for poly in ob.data.polygons if poly.area < 1e-10)
        out.append({'object': ob, 'group': group, 'what': what, 'place': place, 'origin': origin, 'islands': n,
                    'floating': floating, 'coplanar_same': same, 'coplanar_examples': ex, 'degenerate': degenerate,
                    'labels': dict(prop.labels), 'floor': prop.floor, **st})
    return out


if __name__ == '__main__':
    try:
        import cafe_wave1_textures
        cafe_wave1_textures.main(tex_dir)
    except ImportError:
        print('Pillow is not here: the pictures were not redrawn (run cafe_wave1_textures.py with python3)')
    fb.clear_scene()
    results = build_all()
    pieces, problems = [], 0
    for r in results:
        ob = r['object']
        bad = []
        if r['floating']:
            bad.append('FLOATING %s' % (r['floating'][:3],))
        if r['degenerate']:
            bad.append('DEGENERATE %d' % r['degenerate'])
        if r['coplanar_same']:
            bad.append('TWO FACES IN ONE PLANE %d %s' % (r['coplanar_same'], r['coplanar_examples'][:2]))
        if r['floor'] and r['min'][2] < -0.0005:
            bad.append('BELOW THE FLOOR %.4f' % r['min'][2])
        tex = [m for m in r['materials'] if m in rs.NEW and rs.NEW[m][2]]
        for m in tex:
            if not r['labels'].get(m):
                bad.append('TEXTURE NOT MAPPED %s' % m)
        if bad:
            problems += 1
            print(ob.name, '; '.join(bad))
        fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
        sx, sy, sz = r['size']
        pieces.append({'name': ob.name, 'group': r['group'], 'what': r['what'], 'tris': r['tris'],
                       'size': [sx, sz, sy], 'bottom': r['min'][2], 'back': r['max'][1],
                       'materials': r['materials'], 'islands': r['islands'], 'origin': r['origin'],
                       'placement': r['place'], 'collider': None,
                       'textured_faces': {m: r['labels'].get(m, 0) for m in tex}})
        print('%-18s %5d tris  %-22s isl %3d  %s' % (ob.name, r['tris'], '%.3f %.3f %.3f' % tuple(r['size']), r['islands'], ', '.join(r['materials'])))
    materials = {}
    for name, (hexstr, smooth, tex, why) in rs.NEW.items():
        e = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0.0, 'why': why}
        if tex:
            e['texture'] = rs.TEX + tex
        materials[name] = e
    existing = {name: {'hex': h.lstrip('#'), 'smoothness': s, 'metallic': 0.0} for name, (h, s) in rs.EXISTING.items()}
    x = 0.0
    for r in results:
        r['object'].location = (x - r['min'][0], 0.0, 0.0)
        x += r['size'][0] + 0.4
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    manifest = {'version': 1, 'kit': 'cafe_dressing',
                'note': "Built by Tools/Blender/cafe_repairside.py and cafe_cafeside.py (Art's B3, specs/art-bench-and-cafe.md). "
                        "Unity (x, y, z) = Blender (-x, z, -y) inside a piece. Every placement is in Unity world space with "
                        "rotation (0, 180, 0) (S1: 192) as cafe_bar.json's pieces: the fronts face the room (-z). A y of 0 "
                        "means the floor's top: find it by a ray down. Every word on LI_Tags, LI_Whiteboard, LI_Mortgage and "
                        "LI_FinalNotice is PLACEHOLDER COPY for Content; the lender is fictional. None of these pieces has a "
                        "collider or a hover: decor (his decision 9); R1 holds no phones, watches or cameras (they'd read as jobs).",
                'pieces': pieces, 'materials': materials, 'existing_materials': existing}
    os.makedirs(fbx_dir, exist_ok=True)
    with open(os.path.join(fbx_dir, 'cafe_dressing.json'), 'w') as fh:
        json.dump(manifest, fh, indent=1)
    print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
        len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
