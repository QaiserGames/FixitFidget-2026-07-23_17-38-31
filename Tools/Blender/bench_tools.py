"""
The bench's tools (7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md section 4): the five real tools that replace the
rail's tinted cylinders, built with Python in Blender 5.2 (fixit_blender.py beside it): a screwdriver, tweezers, a
brush, a pry tool and a cloth (his call: the cloth wipes smudges off glass in session 4).

Conventions, for tools (they are held, not stood on a floor):

  * 1 unit = 1 m, Z up. The ORIGIN IS THE WORKING TIP: the blade's edge, the tweezers' points, the bristles' ends,
    the cloth's face on the glass. The handle goes up +Z (+Y in Unity). So in Unity a tool is put to work by
    placing its origin on the part and pointing its -Y along the part's normal; turning a screwdriver is a turn
    about its own Y. Nothing goes below z = 0.
  * The flat side of a flat tool (the pry blade, the cloth's fold) faces -Y: the rail shows the tools to a camera
    on the -Y side (+Z in Unity, the front), tilted 20 degrees as the cylinders are today.
  * Chunky: handles 20-26 mm across (real ones are 10-15), shafts 6 mm; flat shading; 8-sided handles, 6-sided
    shafts; one-segment bevels. About 300 triangles a tool, well under the 1,800 the plan allowed for all five.
  * One joined mesh per tool, except the tweezers: its two leaves are their own meshes under one root, each with
    its origin at the heel (the bend), so session 4 can close them by turning each leaf about its own X. The root
    is still the points.
  * Materials: the cafe's (DC_Steel, DC_SteelDark, T2_Brass, CC_Wood_Counter) and BT_ ones for the handles, the
    bristles and the cloth (BT_MATERIALS: sRGB hex and a Unity smoothness; the Unity step makes them). Each
    handle has its own colour so the tools read apart at a glance, as the tints did.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# (sRGB hex, Unity smoothness)
BT_MATERIALS = {
    'BT_Handle_Green': ('#2E7D5B', 0.45),   # the screwdriver: the brand's green (it is Ace's own tool)
    'BT_Handle_Red':   ('#B13A2F', 0.45),   # the pry tool
    'BT_Handle_Blue':  ('#34507A', 0.45),   # the brush's painted band
    'BT_Bristle':      ('#C9A46A', 0.05),   # natural bristle
    'BT_Grip_Black':   ('#2A2622', 0.20),   # the tweezers' grip pads, the screwdriver's cap
    'BT_Cloth':        ('#6F9BC4', 0.02),   # a blue microfibre: the glass cloth everyone knows
}


def M(name):
    """A material from the cafe's table or this one."""
    if name in BT_MATERIALS:
        hexstr, smooth = BT_MATERIALS[name]
        return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth))
    return gh.M(name)


# ----------------------------------------------------------------------------- two helpers the furniture never needed


def frustum(z0, size0, z1, size1, centre=(0.0, 0.0), rot_z=0.0):
    """A box that changes size along Z: (wx, wy) at z0 to (wx, wy) at z1, centred on (cx, cy). Blades, leaves, tufts."""
    cx, cy = centre
    bm = bmesh.new()
    lo = [bm.verts.new((cx + sx * size0[0] / 2, cy + sy * size0[1] / 2, z0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    hi = [bm.verts.new((cx + sx * size1[0] / 2, cy + sy * size1[1] / 2, z1)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if rot_z:
        fb.turn(bm, rot_z, 'Z', (cx, cy, 0))
    return bm


def barrel(rings, sides=8, tip=None, bottom_tip=None):
    """A handle: a loft through (z, rx, ry) rings, flattened where ry < rx."""
    return fb.loft([(0.0, 0.0, z, rx, ry) for z, rx, ry in rings], sides=sides, tip=tip, bottom_tip=bottom_tip)


# ----------------------------------------------------------------------------- the five tools


def screwdriver():
    """A precision screwdriver, 153 mm: a flat blade, a steel shaft, a brass ferrule, a green faceted handle, a black cap."""
    p = fb.Prop('BT_Screwdriver', floor=False)
    steel, brass = M('DC_Steel'), M('T2_Brass')
    # the blade: 4.5 mm wide, its edge at the origin; its flat faces -Y/+Y
    p.add('blade', frustum(0.0, (.0045, .0010), .007, (.0045, .0034)), steel)
    p.add('shaft', fb.cylinder((0, 0, (.005 + .072) / 2), .0032, .072 - .005, sides=6), steel)
    p.add('ferrule', fb.lathe([(.0060, .070), (.0085, .0725), (.0085, .0815), (.0078, .0845)], sides=8), brass)
    p.add('handle', barrel([(.0825, .0080, .0080), (.087, .0108, .0108), (.100, .0122, .0122), (.118, .0128, .0128),
                            (.136, .0116, .0116), (.146, .0092, .0092), (.1495, .0060, .0060)], sides=8), M('BT_Handle_Green'))
    # three flats on the handle, proud of it, so it reads as a grip and doesn't roll
    for k in range(3):
        flat = fb.rbox(-.0045, .0045, -.0015, .0015, .094, .140, bevel=.001)
        fb.move(flat, (0, -.0125, 0))                        # on the handle's front, half sunk into it
        fb.turn(flat, 90 + k * 120, 'Z', (0, 0, 0))
        p.add('grip flat', flat, M('BT_Grip_Black'))
    p.add('cap', fb.lathe([(.0048, .1485), (.0052, .1515), (.0040, .1530)], sides=8), M('BT_Grip_Black'))
    return p


def pry_tool():
    """A pry tool (an opening spudger), 135 mm: a flat steel blade and shank, a dark collar, a red flattened handle."""
    p = fb.Prop('BT_PryTool', floor=False)
    steel = M('DC_Steel')
    p.add('blade', frustum(0.0, (.016, .0010), .014, (.016, .0030)), steel)           # the edge at the origin
    p.add('shank', fb.zbox(-.0055, .0055, -.0015, .0015, .012, .050), steel)
    p.add('collar', fb.rbox(-.0092, .0092, -.0062, .0062, .048, .058, bevel=.002), M('DC_SteelDark'))
    p.add('handle', barrel([(.056, .0088, .0060), (.070, .0106, .0070), (.100, .0116, .0076), (.122, .0100, .0068),
                            (.1315, .0068, .0048)], sides=8, tip=(0, 0, .135)), M('BT_Handle_Red'))
    return p


TWEEZERS_HEEL = .112      # z of the bend, where the leaves meet; each leaf's origin


def tweezers():
    """
    Pointed tweezers, 120 mm, as three meshes under one root: the heel (the bend, with the leaves' tops in it), and
    two leaves whose origins are at the heel so each can be turned about its own X to close the points. The root's
    origin is the points, 2.4 mm apart at rest. The leaves' broad faces look along X (the gap is seen from -Y).
    Returns (root object, [child objects]).
    """
    import bpy
    steel = M('DC_Steel')
    heel = fb.Prop('BT_Tweezers', floor=False)
    heel.add('heel', fb.rbox(-.0066, .0066, -.0058, .0058, TWEEZERS_HEEL - .002, TWEEZERS_HEEL + .008, bevel=.0015), steel)
    root = heel.finish()
    leaves = []
    for side, name in ((-1, 'Leaf_L'), (1, 'Leaf_R')):
        leaf = fb.Prop(name, floor=False)
        # built in the leaf's own frame: origin at the heel, the leaf going down to z = -TWEEZERS_HEEL
        x_tip, x_heel = side * .0024, side * .0050            # the leaf's centre line, from the point to the heel
        tip_size, heel_size = (.0018, .0028), (.0024, .0105)   # (thickness along X, width along Y): chunkier than real
        bm = bmesh.new()
        lo = [bm.verts.new((x_tip + sx * tip_size[0] / 2, sy * tip_size[1] / 2, -TWEEZERS_HEEL)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        hi = [bm.verts.new((x_heel + sx * heel_size[0] / 2, sy * heel_size[1] / 2, .0005)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bm.faces.new(list(reversed(lo)))
        bm.faces.new(hi)
        for i in range(4):
            bm.faces.new([lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        leaf.add('leaf', bm, steel)
        # a grip pad on the outside, where the thumb goes
        z0, z1 = -.050, -.018
        x_out = x_tip + (x_heel - x_tip) * ((z0 + z1) / 2 + TWEEZERS_HEEL) / TWEEZERS_HEEL
        pad = fb.rbox(-.0014, .0014, -.0042, .0042, z0, z1, bevel=.0006)
        fb.move(pad, (x_out + side * .0018, 0, 0))
        leaf.add('grip pad', pad, M('BT_Grip_Black'))
        ob = leaf.finish()
        ob.parent = root
        ob.location = (0, 0, TWEEZERS_HEEL)
        leaves.append(ob)
    return root, leaves


def brush():
    """A cleaning brush, 140 mm: three tufts of bristle, a crimped steel ferrule, a chestnut handle with a blue band."""
    p = fb.Prop('BT_Brush', floor=False)
    for cx in (-.0068, 0.0, .0068):
        p.add('tuft', frustum(0.0, (.0052, .0062), .024, (.0066, .0088), centre=(cx, 0)), M('BT_Bristle'))
    p.add('ferrule', fb.rbox(-.0118, .0118, -.0058, .0058, .022, .038, bevel=.0015), M('DC_Steel'))
    p.add('handle', barrel([(.0365, .0096, .0046), (.046, .0084, .0044), (.090, .0106, .0058), (.125, .0094, .0054),
                            (.1355, .0060, .0040)], sides=8, tip=(0, 0, .140)), M('CC_Wood_Counter'))
    p.add('band', barrel([(.100, .0110, .0062), (.106, .0112, .0063), (.114, .0110, .0062), (.119, .0106, .0060)], sides=8), M('BT_Handle_Blue'))
    return p


def cloth():
    """A folded glass cloth, 80 x 68 mm and 17 mm high: its face on the glass is the origin; one corner lifted."""
    p = fb.Prop('BT_Cloth', floor=False)
    blue = M('BT_Cloth')
    p.add('cloth', fb.rbox(-.040, .040, -.034, .034, 0.0, .009, bevel=.0035), blue)
    p.add('fold', fb.rbox(-.036, .031, -.027, .031, .008, .017, bevel=.0035), blue)
    corner = fb.rbox(-.018, .002, -.002, .018, 0.0, .003, bevel=.001)
    fb.turn(corner, -28, 'Y', (.002, 0, 0))                      # lifted off the glass at its free end
    fb.move(corner, (.038, -.032, .0085))
    p.add('corner', corner, blue)
    return p


# ----------------------------------------------------------------------------- the list


TOOLS = [
    # (builder, Unity's ToolType, what it is, and what session 4 does with it)
    (screwdriver, 'Screwdriver', "screwdriver: the blade's edge is the origin; turn about Y at a screw"),
    (tweezers, 'Tweezers', "tweezers: the points are the origin; Leaf_L and Leaf_R turn about their own X to pinch"),
    (brush, 'Brush', "brush: the bristles' ends are the origin; strokes along the surface"),
    (pry_tool, 'Pry', "pry tool: the blade's edge is the origin; the lever goes in flat along -Y"),
    (cloth, 'Cloth', "cloth: its face on the glass is the origin; wiped in circles"),
]


def build_all():
    """Every tool, finished and audited: [{object, children, tool, what, islands, floating, tris, size, min, max, materials}]."""
    out = []
    for builder, tool, what in TOOLS:
        built = builder()
        if isinstance(built, tuple):
            root, children = built
        else:
            root, children = built.finish(), []
        # the audit and the figures over the whole tool, children included (a leaf's mesh is in its own frame)
        tris, mins, maxs, mats, islands, floating = 0, [], [], [], 0, []
        objs = [root] + children
        for ob in objs:
            n, fl = fb.audit(ob)
            st = fb.stats(ob)
            off = Vector(ob.location) if ob.parent is not None else Vector((0, 0, 0))
            tris += st['tris']
            mins.append(Vector(st['min']) + off)
            maxs.append(Vector(st['max']) + off)
            mats += [m for m in st['materials'] if m not in mats]
            islands += n
            floating += fl
        lo = Vector((min(v.x for v in mins), min(v.y for v in mins), min(v.z for v in mins)))
        hi = Vector((max(v.x for v in maxs), max(v.y for v in maxs), max(v.z for v in maxs)))
        if len(objs) > 1:
            # the pieces of a jointed tool must touch each other too
            boxes = [(Vector(fb.stats(o)['min']) + (Vector(o.location) if o.parent else Vector()),
                      Vector(fb.stats(o)['max']) + (Vector(o.location) if o.parent else Vector())) for o in objs]
            for i, a in enumerate(boxes):
                best = min(fb.gap(a, b) for j, b in enumerate(boxes) if j != i)
                if best > .002:
                    floating.append((objs[i].name, 'part', round(best * 1000, 1)))
        out.append({'object': root, 'children': children, 'tool': tool, 'what': what, 'islands': islands,
                    'floating': floating, 'tris': tris,
                    'size': tuple(round(hi[k] - lo[k], 4) for k in range(3)),
                    'min': tuple(round(lo[k], 4) for k in range(3)), 'max': tuple(round(hi[k], 4) for k in range(3)),
                    'materials': mats})
    return out
