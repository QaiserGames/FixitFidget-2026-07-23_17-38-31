"""
Build the lived-in café's pieces (cafe_livedin.py), audit them, and write one FBX per piece, the manifest the Unity step
reads (cafe_livedin.json, cafe_props.json's format: each piece's size in Unity axes (across, up, deep), bottom, back,
triangles, materials and islands as built, where it goes, its origin, its solid, its references, its textured face set;
the kit's new materials with colour, smoothness and texture, the existing ones it reuses by name), and the .blend.

With bpy:   python3 export_cafe_livedin.py [-- <fbx folder> <blend path>]
Headless:   blender -b --python export_cafe_livedin.py [-- <fbx folder> <blend path>]
Run from Tools/Blender in the repo, the defaults are Assets/Art/Models/CafeKit/ and BlenderSource/CafeLivedIn_v1.blend;
anywhere else, fbx/ and CafeLivedIn_v1.blend beside this script. The textures come first:
python3 cafe_livedin_textures.py (it writes Assets/Art/Textures/Cafe/LI_*.png).

The audit (printed, and any failure counted in the last line's problems):
  * nothing floating: every island within 2 mm of another (fixit_blender.audit);
  * nothing below the floor (a floor piece's lowest point at z >= -0.5 mm; the coat's is its hem, above the floor);
  * no degenerate faces (no face without area);
  * no two faces in one plane facing the same way (cafe_seating.coplanar_overlaps, 'same');
  * each textured face set mapped (the faces carrying its picture counted);
  * the coat on both chairs: no triangle of it inside CK_Chair_Cafe or CK_Chair_Spindle where it hangs (COAT_ON),
    and the gap over each rail.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_TOOLS', ''), '/home/claude/cafe/s9/src/Tools/Blender'):
    if _p and os.path.exists(os.path.join(_p, 'fixit_blender.py')) and _p not in sys.path:
        sys.path.append(_p)
import bpy  # noqa: E402
import bmesh  # noqa: E402
import cafe_livedin as li  # noqa: E402
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
REPO = os.path.abspath(os.path.join(HERE, '..', '..')) if IN_REPO else None
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_dir = args[0] if args else (os.path.join(REPO, 'Assets', 'Art', 'Models', 'CafeKit') if IN_REPO else os.path.join(HERE, 'fbx'))
blend_path = args[1] if len(args) > 1 else (os.path.join(REPO, 'BlenderSource', 'CafeLivedIn_v1.blend') if IN_REPO
                                            else os.path.join(HERE, 'CafeLivedIn_v1.blend'))
LABEL_SIZES = {'LI_RugCorner': li.RUG, 'LI_CoirMat': li.COIR, 'LI_RubberMat': li.RUBBER,
               'LI_BinLabels': li.BIN_LABEL[:2], 'LI_KidsDrawing': li.DRAWING,
               'LI_Screen': (0.296, li.LAP['D'] - 0.029)}      # 24 Oct 2026, art audit a50: the laptop's screen picture
LABEL_UV = {
    'LI_RugCorner': "0..1 over the top, u along the rug's length (x), v across it, as seen from above with the front at "
                    "the bottom; the chamfers take the picture's outer 4 mm (the hems), the sides and the bottom one hem texel",
    'LI_CoirMat': "0..1 over the top as seen from above (u along x, v along y), the chamfer the picture's cut edge; the "
                  "sides one coir texel; the PVC backing is CF_Black",
    'LI_RubberMat': "0..1 over the top and the bevelled ramp as seen from above (the ramp is the picture's outer 25 mm); "
                    "the 1.5 mm lip one ramp texel",
    'LI_BinLabels': "three plates share the texture: the left plate (RECYCLING) maps to the left third, COMPOST the "
                    "middle, LANDFILL the right (columns round(k*1024/3)..round((k+1)*1024/3), inset 3 px); u to the "
                    "reader's right, v up, on each plate's front; its edges one border texel of its own third",
    'LI_KidsDrawing': "0..1 on the front of the quad (u to the reader's right, v up); a single quad facing -Y",
    'LI_Screen': "0..1 over the glass's face, u to the reader's right, v up the open lid (the title bar at the top); the "
                 "glass's edges one dark texel of the bar (24 Oct 2026, art audit a50)",
}


def coat_fit():
    """The coat on both chairs (cafe_seating's own builds): intersecting triangle pairs and the gap over each rail."""
    import cafe_seating as cs
    coat = bpy.data.objects['LI_Coat_OnChair']
    out = {}
    for name, fn in (('CK_Chair_Cafe', cs.chair_cafe), ('CK_Chair_Spindle', cs.chair_spindle)):
        chair = fn().finish()
        chair.name = name + ' (fit check)'
        off = li.COAT_ON[name]
        cb = bmesh.new()
        cb.from_mesh(coat.data)
        bmesh.ops.translate(cb, verts=cb.verts[:], vec=off)
        hb = bmesh.new()
        hb.from_mesh(chair.data)
        t1, t2 = BVHTree.FromBMesh(cb), BVHTree.FromBMesh(hb)
        hits = len(t1.overlap(t2))
        near = [t1.find_nearest(v.co)[3] for v in hb.verts if v.co.z > 0.78]
        gaps = []
        for x in (-0.12, -0.06, 0.0, 0.06, 0.12):
            o = Vector((x, li.RAIL['yc'] + off[1], 1.5))
            loc_h = t2.ray_cast(o, Vector((0, 0, -1)))[0]
            if loc_h is not None:
                lo = t1.ray_cast(loc_h + Vector((0, 0, 1e-4)), Vector((0, 0, 1)))[0]
                if lo is not None:
                    gaps.append(round((lo.z - loc_h.z) * 1000, 1))
        out[name] = {'hits': hits, 'nearest_mm': round(min(near) * 1000, 1) if near else None, 'gaps_mm': gaps}
        bpy.data.objects.remove(chair, do_unlink=True)
        cb.free()
        hb.free()
    return out


fb.clear_scene()
results = li.build_all()
pieces, problems = [], 0
print('%-20s %5s  %-22s %-7s %5s %4s %4s %5s  %s' % ('piece', 'tris', 'size (m, Blender)', 'bottom', 'isl', 'flt', 'cop', 'degen', 'materials'))
for r in results:
    ob = r['object']
    floor_piece = ob.name not in ('LI_Coat_OnChair',)
    below = r['min'][2] < -0.0005
    if r['floating']:
        problems += 1
        print('FLOATING', ob.name, r['floating'])
    if r['degenerate']:
        problems += 1
        print('DEGENERATE', ob.name, r['degenerate'], 'face(s) with no area')
    if below:
        problems += 1
        print('BELOW THE FLOOR', ob.name, r['min'][2])
    if r['coplanar_same']:
        problems += 1
        print('TWO FACES IN ONE PLANE', ob.name, r['coplanar_same'], r['coplanar_examples'])
    fb.export_fbx(ob, os.path.join(fbx_dir, ob.name + '.fbx'))
    sx, sy, sz = r['size']
    entry = {
        'name': ob.name, 'group': r['group'], 'what': r['what'], 'stretch': False, 'tris': r['tris'],
        'size': [sx, sz, sy],                       # Unity's axes: across, up, deep
        'bottom': r['min'][2],                      # 0 on the floor; the coat's hem hangs above it
        'back': r['max'][1],                        # the back's y in Blender (the front is -y)
        'materials': r['materials'], 'islands': r['islands'],
        'origin': r['origin'], 'place': r['place'], 'collider': r['collider'],
        'references': li.REFERENCES.get(ob.name, []),
    }
    for mname, faces in r['labels'].items():
        w, h = LABEL_SIZES[mname]
        entry['label'] = {'material': mname, 'faces': faces, 'size_m': [round(w, 3), round(h, 3)], 'uv': LABEL_UV[mname]}
    if ob.name == 'LI_Coat_OnChair':
        entry['fits'] = {k: {'local_position_unity': [round(-v[0], 4), round(v[2], 4), round(-v[1], 4)], 'local_rotation': [0, 0, 0]}
                         for k, v in li.COAT_ON.items()}
    pieces.append(entry)
    print('%-20s %5d  %-22s %-7s %5d %4s %4d %5d  %s' % (ob.name, r['tris'], '%.3f %.3f %.3f' % tuple(r['size']),
                                                       '%.4f' % r['min'][2], r['islands'], 'ok' if not r['floating'] else 'NO',
                                                       r['coplanar_same'], r['degenerate'], ', '.join(r['materials'])))

fit = coat_fit()
for name, f in fit.items():
    if f['hits']:
        problems += 1
    print('COAT ON %-17s %s intersecting triangle pairs; nearest rail point %s mm; gap over the rail %s mm' % (
        name, f['hits'], f['nearest_mm'], f['gaps_mm']))

materials = {}
for name, (hexstr, smooth, why, tex) in li.LI_MATERIALS.items():
    e = {'hex': hexstr.lstrip('#'), 'smoothness': smooth, 'metallic': 0.0, 'why': why}
    if tex:
        e['texture'] = li.TEX + tex
    if name in li.EMISSION:
        e['emission'] = li.EMISSION[name]
    materials[name] = e
existing = {name: {'hex': h.lstrip('#'), 'smoothness': s, 'metallic': 0.0, 'use': why}
            for name, (h, s, why) in li.EXISTING.items()}

# lay the pieces out in the .blend in 2.2 m rows (the review's layout), so they can be looked at; the FBX files went out
# from the origin
ROWS = [['LI_Rug_Corner', 'LI_Rug_Entry', 'LI_TillMat'], ['LI_BinStation', 'LI_StreetBin', 'LI_ToteBag', 'LI_Coat_OnChair'],
        ['LI_NapkinDispenser', 'LI_CondimentStation', 'LI_Laptop_Open', 'LI_Laptop_Closed', 'LI_LeftCup', 'LI_KidsDrawing']]
by = {r['object'].name: r for r in results}
for i, row in enumerate(ROWS):
    x = 0.0
    for name in row:
        r = by[name]
        w = r['size'][0]
        r['object'].location = (x - r['min'][0], i * 2.2, 0.0)
        x += w + 0.5
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0      # no CafeLivedIn_v1.blend1 beside it on a re-run
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
manifest = {'version': 1, 'kit': 'cafe_livedin',
            'note': "Unity (x, y, z) = Blender (-x, z, -y); the front of each piece faces Unity +Z as built. The lived-in "
                    "pass (findings 12 Oct, section 8), built by Tools/Blender/cafe_livedin.py; textures by "
                    "cafe_livedin_textures.py into Assets/Art/Textures/Cafe/LI_*.png. collider: the piece's solid in its own "
                    "Unity frame ({centre, size}, cafe_seating.json's form), null for none. A y of 0 in a place line means "
                    "the floor's top: find it by a ray down, as CafeFurnishing.Mat does (TopAt); where the bench and drink "
                    "mats lie it is 0.0165, and a rug set at y 0 there would sit under the floor. The kid's drawing's "
                    "words are PLACEHOLDER COPY (THANK YOU ACE!, LILY 6).",
            'pieces': pieces, 'materials': materials, 'existing_materials': existing}
os.makedirs(fbx_dir, exist_ok=True)
with open(os.path.join(fbx_dir, 'cafe_livedin.json'), 'w') as fh:
    json.dump(manifest, fh, indent=1)
print('%d pieces, %d triangles; FBX in %s; blend %s; %d problem(s)' % (
    len(pieces), sum(p['tris'] for p in pieces), fbx_dir, blend_path, problems))
