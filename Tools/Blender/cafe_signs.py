"""
The café's signs, v1 (10 Oct 2026; brief D): the meshes that carry the words drawn by draw_cafe_textures.py.

  CK_MenuBoard       the painted menu (2.60 x 1.00 over all) in a chestnut frame, a brass picture light on two arms
  CK_Fascia          the painted board over the door, 2.60 x 0.55, with its drip cap
  CK_BladeSign       the round sign (Ø 0.80) on a warm-steel bracket that fixes to the wall; both faces lettered
  CK_EnamelSign      REPAIRS, an enamel plate 0.90 x 0.25 on two short chains from a beam (origin at the top)
  CK_StaffPlate      STAFF ONLY, a brass plate 0.25 x 0.08
  CK_WindowGilding_L / _R   the gold leaf on the two front windows: 1.30 x 0.8 slabs 2 mm thick (one 1.44 m pane each), transparent texture
  CK_EntryMosaic     ACE'S in the floor inside the door: a 2.40 x 1.20 plate, 2 mm
  CK_NoticeBoard     cork in a chestnut frame (0.90 x 0.60 of cork) with the four flyers pinned on it
  CK_PhotoLedge      a 1.20 m picture ledge with the first photo (opening day) in a frame leaning on it
  CK_TentCard        one pastry tent card (89 x 51 mm a face); three are placed, CF_TentCard1..3
  CK_AFrameFace      a chalk face (0.60 x 0.90) to lay on the existing patio A-frame's board (its own mesh has no UVs)

The house way (fixit_blender.py): 1 unit = 1 m, Z up, the front (the side the reader sees) faces -Y; origin at the
bottom centre, a wall piece's at the bottom centre of its back with the back at y = 0; flat shading, one-segment
bevels, junctions from boundaries with 2-4 mm overlaps, no two faces in one plane, and the audit (nothing more than
2 mm from the rest). Unity (x, y, z) = Blender (-x, z, -y).

UVs: only the faces that carry words have meaningful ones. Each lettered surface is its own thin part; its face
toward the reader is mapped 0..1 across the texture by the face's own axes (a planar map: u along the reader's
right, v up), a double-sided sign's back face mirrored so it reads from behind, and its edges sent to one border
texel of the same texture. Every other face keeps fixit_blender's box UVs, which flat colours ignore. (house_kit's
picture() leaves its painting on those metre-based box UVs; these don't, so a sign's picture fills its face exactly.)
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, '..', '..', 'Tools', 'Blender'), '/home/claude/cafe/inputs/Tools/Blender'):
    if os.path.exists(os.path.join(p, 'fixit_blender.py')):
        sys.path.insert(0, p)
        break
import bpy  # noqa: E402,F401  (first: bmesh and mathutils come with it)
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402

TEX = 'Assets/Art/Textures/Cafe/'

# (sRGB hex, Unity smoothness, texture file or None). Existing project materials keep their names and values.
MATERIALS = {
    # existing (scene-facts.md's palette)
    'CC_Wood_Counter':  ('#8F5B32', 0.71, None),
    'CC_Wood_Espresso': ('#4A3422', 0.38, None),
    'DC_Steel':         ('#93908A', 0.50, None),
    'DC_SteelDark':     ('#4E4B47', 0.50, None),
    'T2_Brass':         ('#B8933F', 0.60, None),
    'DC_Sticker_Green': ('#2E7D5B', 0.45, None),
    'DC_Ceramic':       ('#F2EFE8', 0.60, None),
    # new, lettered (textures by draw_cafe_textures.py)
    'CF_Menu':          ('#2E7D5B', 0.25, 'CF_Menu.png'),
    'CF_Fascia':        ('#2E7D5B', 0.30, 'CF_Fascia.png'),
    'CF_GildingL':      ('#C19A52', 0.80, 'CF_GildingL.png'),       # transparent
    'CF_GildingR':      ('#C19A52', 0.80, 'CF_GildingR.png'),       # transparent
    'CF_Blade':         ('#2E7D5B', 0.30, 'CF_Blade.png'),
    'CF_EnamelRepairs': ('#2E7D5B', 0.78, 'CF_EnamelRepairs.png'),  # fired glass: glossy
    'CF_StaffPlate':    ('#B8933F', 0.62, 'CF_StaffPlate.png'),
    'CF_AFrame':        ('#2B2B28', 0.08, 'CF_AFrame.png'),
    'CF_Mosaic':        ('#2E7D5B', 0.50, 'CF_Mosaic.png'),
    'CF_TentCard1':     ('#F4F0E6', 0.20, 'CF_TentCard1.png'),
    'CF_TentCard2':     ('#F4F0E6', 0.20, 'CF_TentCard2.png'),
    'CF_TentCard3':     ('#F4F0E6', 0.20, 'CF_TentCard3.png'),
    'CF_Flyer1':        ('#F6F3EA', 0.05, 'CF_Flyer1.png'),
    'CF_Flyer2':        ('#F8F7F2', 0.05, 'CF_Flyer2.png'),
    'CF_Flyer3':        ('#F7F7F4', 0.05, 'CF_Flyer3.png'),
    'CF_Flyer4':        ('#E8E6E0', 0.05, 'CF_Flyer4.png'),
    'CF_Photo1':        ('#C9BFA8', 0.40, 'CF_Photo1.png'),
    # new, flat
    'CF_Cork':          ('#A9825A', 0.05, None),
    'CF_Mat':           ('#EFE9DC', 0.10, None),                     # the photo's mount
    'CF_PictureLightGlow': ('#FFE6C4', 0.50, None),                  # the picture light's diffuser (emissive in Unity)
    'CF_PinRed':        ('#B5402F', 0.60, None),
    'CF_PinYellow':     ('#D9A93A', 0.60, None),
    'CF_PinBlue':       ('#3F6B8F', 0.60, None),
}
EMISSION = {'CF_PictureLightGlow': 'FFD7A8'}
TRANSPARENT = {'CF_GildingL', 'CF_GildingR'}
# the six tiling materials (other kits' meshes use them; recorded here with their maps and real tile sizes)
TILING = {
    'CF_Stone':          ('#DDD6C9', 0.50, 2.0),      # 10 Oct: darker, veined, honed (0.50), a 2 m tile (see draw_cafe_textures.mat_stone)
    'CF_Tile':           ('#EDE4D3', 0.70, 1.0),
    'CF_Floor_Oak':      ('#CDB797', 0.45, 1.8),
    'CF_Floor_Concrete': ('#A8A196', 0.55, 3.0),
    'CF_Boards':         ('#4A3422', 0.38, 0.9),
    'CF_Fabric':         ('#A8957C', 0.12, 0.25),
}


def M(name):
    hexstr, smooth, _ = MATERIALS[name]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


class SignProp(fb.Prop):
    """A Prop whose lettered parts get planar 0..1 UVs on their reading face (after fixit_blender's box UVs)."""

    def __init__(self, name, floor=True):
        super().__init__(name, floor=floor)
        self.uv_specs = []

    def lettered(self, part_name, src_bm, mat, origin, u_axis, v_axis, w, h, back=False, edge=(0.01, 0.5)):
        """
        A lettered part: the face whose outward normal is u_axis x v_axis (it looks at the reader, u to their right and
        v up) gets u = (p - origin).u_axis / w, v = (p - origin).v_axis / h. back=True maps the opposite face too,
        mirrored, so it reads from behind (a double-sided sign); back='same' gives it the same picture at the
        same places (one layer seen through: window gilding reads reversed from inside, as the real thing does).
        Every other face of the part (edges, insides) gets the one texel `edge`.
        """
        faces = self.add(part_name, src_bm, mat)
        U, V = Vector(u_axis).normalized(), Vector(v_axis).normalized()
        self.uv_specs.append((faces, Vector(origin), U, V, w, h, back, edge))
        return faces

    def finish(self, collection=None):
        bm = self.bm
        self.floored = 0
        if self.floor:
            for v in bm.verts:
                if -0.02 < v.co.z < 0.0:
                    v.co.z = 0.0
                    self.floored += 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.turned = fb.orient_shells(bm)
        bm.normal_update()
        fb.box_uvs(bm)
        uv = bm.loops.layers.uv.verify()
        self.lettered_faces = 0
        for faces, o, U, V, w, h, back, edge in self.uv_specs:
            reader = U.cross(V)                  # right x up points at the reader: the reading face's outward normal
            for f in faces:
                if not f.is_valid:
                    continue
                d = f.normal.dot(reader)
                for loop in f.loops:
                    p = loop.vert.co - o
                    if d > 0.9:
                        loop[uv].uv = (p.dot(U) / w, p.dot(V) / h)
                    elif back == 'same' and d < -0.9:
                        loop[uv].uv = (p.dot(U) / w, p.dot(V) / h)
                    elif back and d < -0.9:
                        loop[uv].uv = (1.0 - p.dot(U) / w, p.dot(V) / h)
                    else:
                        loop[uv].uv = edge
                if d > 0.9 or (back and d < -0.9):
                    self.lettered_faces += 1
        me = fb.bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        for m in self.mats:
            me.materials.append(m)
        ob = fb.bpy.data.objects.new(self.name, me)
        ob['fixit_shells_turned'] = self.turned
        (collection or fb.bpy.context.scene.collection).objects.link(ob)
        return ob


# ----------------------------------------------------------------------------------------------------------- helpers


def ring_y(cx, cz, r_in, r_out, y0, y1, sides=32):
    """A closed flat ring round the Y axis (a band round a disc), square in section."""
    bm = bmesh.new()
    loops = []
    for (r, y) in ((r_out, y0), (r_out, y1), (r_in, y1), (r_in, y0)):
        loops.append([bm.verts.new((cx + r * math.cos(2 * math.pi * i / sides), y, cz + r * math.sin(2 * math.pi * i / sides)))
                      for i in range(sides)])
    for k in range(4):
        a, b = loops[k], loops[(k + 1) % 4]
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new([a[i], a[j], b[j], b[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def link(cx, cy, cz, length, width, wire, plane='XZ', sides_path=6, sides_wire=4):
    """One chain link: a stadium-shaped loop of round wire (flat-shaded), long axis vertical, in plane XZ or YZ."""
    bm = bmesh.new()
    path = []
    straight = max(0.0, length - width) / 2
    rr = (width - wire) / 2
    for half, zc in ((0, straight), (1, -straight)):
        for i in range(sides_path // 2 + 1):
            a = math.pi * i / (sides_path // 2) + (0 if half == 0 else math.pi)
            path.append((rr * math.cos(a), zc + rr * math.sin(a)))
    n = len(path)
    loops = []
    for i, (px, pz) in enumerate(path):
        nx, nz = path[(i + 1) % n][0] - path[i - 1][0], path[(i + 1) % n][1] - path[i - 1][1]
        ln = math.hypot(nx, nz) or 1.0
        tx, tz = nx / ln, nz / ln
        ox, oz = tz, -tx                              # in-plane normal
        loop = []
        for k in range(sides_wire):
            a = 2 * math.pi * k / sides_wire + math.pi / 4
            r1, r2 = wire / 2 * math.cos(a), wire / 2 * math.sin(a)
            x, y, z = px + ox * r1, r2, pz + oz * r1
            if plane == 'YZ':
                x, y = y, x
            loop.append(bm.verts.new((cx + x, cy + y, cz + z)))
        loops.append(loop)
    for i in range(n):
        a, b = loops[i], loops[(i + 1) % n]
        for k in range(sides_wire):
            j = (k + 1) % sides_wire
            bm.faces.new([a[k], a[j], b[j], b[k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# ----------------------------------------------------------------------------------------------------------- the pieces

MENU_W, MENU_H = 2.60, 1.00            # over all
MENU_PANEL = (2.50, 0.92)              # the painted panel inside the frame (what CF_Menu.png covers)


def menu_board():
    """The menu over the till: a painted panel in a chestnut frame, a brass picture light on two arms over it."""
    p = SignProp('CK_MenuBoard', floor=True)
    W, H = MENU_W, MENU_H
    pw, ph = MENU_PANEL
    sx, sz = (W - pw) / 2, (H - ph) / 2              # 5 cm stiles, 4 cm rails
    wood = M('CC_Wood_Counter')
    p.add('top rail', fb.rbox(-W / 2, W / 2, -0.042, 0, H - sz, H, bevel=0.006), wood)
    p.add('bottom rail', fb.rbox(-W / 2, W / 2, -0.042, 0, 0, sz, bevel=0.006), wood)
    for s in (-1, 1):                                 # the stiles stand 3 mm proud of the rails
        x0, x1 = (-W / 2, -W / 2 + sx) if s < 0 else (W / 2 - sx, W / 2)
        p.add('stile', fb.rbox(x0, x1, -0.045, 0, sz - 0.003, H - sz + 0.003, bevel=0.006), wood)
    # the panel, its face 1.5 cm behind the frame's front; its edges run 3 mm under the frame
    x0, x1, z0, z1 = -pw / 2 - 0.003, pw / 2 + 0.003, sz - 0.003, H - sz + 0.003
    p.lettered('painted panel', fb.zbox(x0, x1, -0.030, -0.004, z0, z1), M('CF_Menu'),
               origin=(x0, -0.030, z0), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=x1 - x0, h=z1 - z0, edge=(0.004, 0.5))
    # the picture light: two foot plates on the top rail, swan-neck arms, a 1.20 m hood with its diffuser
    brass = M('T2_Brass')
    for ax in (-0.40, 0.40):
        p.add('foot plate', fb.rbox(ax - 0.03, ax + 0.03, -0.032, -0.006, H - 0.002, H + 0.022, bevel=0.003), brass)
        p.add('arm', fb.strut((ax, -0.019, H + 0.090), (ax, -0.019, H + 0.020), 0.0075, 0.0075, sides=8), brass)
        p.add('arm', fb.strut((ax, -0.188, H + 0.118), (ax, -0.019, H + 0.084), 0.0075, 0.0075, sides=8), brass)
    p.add('hood', fb.rbox(-0.60, 0.60, -0.245, -0.175, H + 0.098, H + 0.140, bevel=0.012), brass)
    p.add('diffuser', fb.zbox(-0.585, 0.585, -0.228, -0.192, H + 0.092, H + 0.100), M('CF_PictureLightGlow'))
    return p


def fascia():
    """The painted board over the door, 2.60 x 0.55: the lettered face plate on a green board, a drip cap on top."""
    p = SignProp('CK_Fascia', floor=True)
    W, H = 2.60, 0.55
    green = M('DC_Sticker_Green')
    p.add('board', fb.rbox(-W / 2, W / 2, -0.032, 0, 0, H, bevel=0.004), green)
    p.lettered('lettered face', fb.zbox(-W / 2 + 0.004, W / 2 - 0.004, -0.035, -0.030, 0.004, H - 0.004), M('CF_Fascia'),
               origin=(-W / 2 + 0.004, -0.035, 0.004), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=W - 0.008, h=H - 0.008,
               edge=(0.004, 0.5))
    p.add('drip cap', fb.rbox(-W / 2 - 0.02, W / 2 + 0.02, -0.062, -0.002, H - 0.003, H + 0.028, bevel=0.008), green)
    return p


BLADE_D = 0.80
BLADE_CX = 0.58          # the disc's centre, out from the wall face (+X, Unity -X)


def blade_sign():
    """
    The round blade sign on its bracket. The wall face is the plane x = 0 and the sign stands out along +X; the disc's
    faces look along -Y and +Y, each lettered to read the right way. Origin: on the wall face, at the disc's bottom.
    """
    p = SignProp('CK_BladeSign', floor=False)
    R, cx, cz = BLADE_D / 2, BLADE_CX, BLADE_D / 2
    t = 0.032
    disc = fb.cylinder((cx, 0, cz), R - 0.004, t, sides=40, rot=(90, 0, 0))
    p.lettered('lettered disc', disc, M('CF_Blade'), origin=(cx - R, -t / 2, cz - R), u_axis=(1, 0, 0), v_axis=(0, 0, 1),
               w=BLADE_D, h=BLADE_D, back=True, edge=(0.5, 0.004))
    p.add('steel band', ring_y(cx, cz, R - 0.006, R + 0.004, -t / 2 - 0.0025, t / 2 + 0.0025, sides=40), M('DC_SteelDark'))
    steel = M('DC_SteelDark')
    p.add('wall plate', fb.rbox(0, 0.012, -0.040, 0.040, 0.55, 0.90, bevel=0.003), steel)
    p.add('arm', fb.rbox(0.010, 1.00, -0.015, 0.015, 0.855, 0.885, bevel=0.004), steel)
    p.add('brace', fb.strut((0.46, 0, 0.862), (0.010, 0, 0.585), 0.010, 0.010, sides=6), steel)
    for hx in (cx - 0.12, cx + 0.12):                 # two rods from the arm into the band
        top = 0.858
        bot = cz + math.sqrt(R * R - (hx - cx) ** 2) - 0.004
        p.add('hanger', fb.strut((hx, 0, top), (hx, 0, bot), 0.004, 0.004, sides=6), steel)
        p.add('eye', ring_y(hx, top + 0.002, 0.004, 0.009, -0.003, 0.003, sides=8), steel)
    return p


ENAMEL = (0.90, 0.25)
ENAMEL_DROP = 0.18       # from the beam to the plate's top


def enamel_sign():
    """REPAIRS: an enamel plate on two short chains from a beam. Origin at the top (the beam's underside); hangs down."""
    p = SignProp('CK_EnamelSign', floor=False)
    W, H = ENAMEL
    top = -ENAMEL_DROP
    t = 0.006
    p.lettered('enamel plate', fb.rbox(-W / 2, W / 2, -t / 2, t / 2, top - H, top, bevel=0.0018), M('CF_EnamelRepairs'),
               origin=(-W / 2, -t / 2, top - H), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=W, h=H, back=True, edge=(0.5, 0.004))
    steel = M('DC_Steel')
    for hx in (-W / 2 + 0.045, W / 2 - 0.045):        # through the plate's two holes (3 cm under its top)
        hole_z = top - 0.030
        z = -0.006
        p.add('screw eye', ring_y(hx, z - 0.008, 0.004, 0.0085, -0.0025, 0.0025, sides=8), steel)
        p.add('eye shank', fb.cylinder((hx, 0, -0.003), 0.0025, 0.006, sides=6), steel)
        pitch, k, z = 0.032, 0, z - 0.020
        while z - 0.020 > hole_z:
            p.add('link', link(hx, 0, z, 0.040, 0.018, 0.0035, plane='YZ' if k % 2 else 'XZ'), steel)
            z -= pitch
            k += 1
        p.add('ring through the hole', link(hx, 0, hole_z + 0.004, 0.024, 0.016, 0.0035, plane='YZ'), steel)
    return p


def staff_plate():
    """STAFF ONLY: a brass plate 0.25 x 0.08, 4 mm, its edge bevelled. A wall (door) piece."""
    p = SignProp('CK_StaffPlate', floor=True)
    W, H, t = 0.25, 0.08, 0.004
    p.lettered('plate', fb.rbox(-W / 2, W / 2, -t, 0, 0, H, bevel=0.0012), M('CF_StaffPlate'),
               origin=(-W / 2, -t, 0), u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=W, h=H, edge=(0.002, 0.5))
    return p


def gilding(side):
    """Window gold leaf, 1.30 x 0.8 (a 1.44 m pane by the door), a 2 mm slab: the street face (-Y) reads; the room face carries the same picture at
    the same places, so it reads reversed from inside as real gilding does, and through the clear parts of one
    face the other never shows a mirrored copy; the edges take a transparent texel."""
    name, mat = ('CK_WindowGilding_L', 'CF_GildingL') if side == 'L' else ('CK_WindowGilding_R', 'CF_GildingR')
    p = SignProp(name, floor=True)
    W, H, t = 1.30, 0.80, 0.002
    p.lettered('leaf', fb.zbox(-W / 2, W / 2, -t, 0, 0, H), M(mat), origin=(-W / 2, -t, 0), u_axis=(1, 0, 0),
               v_axis=(0, 0, 1), w=W, h=H, back='same', edge=(0.0005, 0.9995))
    return p


def entry_mosaic():
    """ACE'S in the floor inside the door: a 2.40 x 1.20 plate, 2 mm. Read from the door looking in (Unity north):
    the word runs along Blender -X (Unity +X) and up along Blender -Y (Unity +Z)."""
    p = SignProp('CK_EntryMosaic', floor=True)
    W, D, t = 2.40, 1.20, 0.002
    # the top face looks up (+Z); a reader at Unity south (Blender +Y) sees right = Blender -X and up = Blender -Y
    p.lettered('mosaic', fb.zbox(-W / 2, W / 2, -D / 2, D / 2, 0, t), M('CF_Mosaic'), origin=(W / 2, D / 2, t),
               u_axis=(-1, 0, 0), v_axis=(0, -1, 0), w=W, h=D, edge=(0.002, 0.5))
    return p


FLYERS = [  # (material, w, h, centre x, centre z on the board, tilt degrees, pins)
    ('CF_Flyer1', 0.216, 0.279, -0.300, 0.350, -2.0, [('CF_PinRed', 0.0, 0.125)]),
    ('CF_Flyer2', 0.216, 0.279, -0.075, 0.315, 1.5, [('CF_PinBlue', 0.0, 0.125)]),
    ('CF_Flyer3', 0.216, 0.250, 0.162, 0.370, -1.0, [('CF_PinYellow', -0.090, 0.112), ('CF_PinRed', 0.090, 0.112)]),
    ('CF_Flyer4', 0.140, 0.216, 0.356, 0.290, 2.5, [('CF_PinBlue', 0.0, 0.094)]),
]


def notice_board():
    """Cork (0.90 x 0.60) in a chestnut frame by the door, the four flyers pinned on it. A wall piece."""
    p = SignProp('CK_NoticeBoard', floor=True)
    cw, ch, f = 0.90, 0.60, 0.03
    W, H = cw + 2 * f, ch + 2 * f
    wood = M('CC_Wood_Counter')
    p.add('top rail', fb.rbox(-W / 2, W / 2, -0.022, 0, H - f, H, bevel=0.004), wood)
    p.add('bottom rail', fb.rbox(-W / 2, W / 2, -0.022, 0, 0, f, bevel=0.004), wood)
    for s in (-1, 1):
        x0, x1 = (-W / 2, -W / 2 + f) if s < 0 else (W / 2 - f, W / 2)
        p.add('stile', fb.rbox(x0, x1, -0.025, 0, f - 0.003, H - f + 0.003, bevel=0.004), wood)
    p.add('cork', fb.zbox(-cw / 2 - 0.003, cw / 2 + 0.003, -0.012, -0.002, f - 0.003, H - f + 0.003), M('CF_Cork'))
    for i, (mat, w, h, cx, cz, tilt, pins) in enumerate(FLYERS):
        y = -0.0132 - 0.0004 * i                       # each sheet a hair proud of the last: no two in one plane
        a = math.radians(tilt)
        U = Vector((math.cos(a), 0, math.sin(a)))
        V = Vector((-math.sin(a), 0, math.cos(a)))
        c = Vector((cx, y, f + cz))
        # a sheet 0.4 mm thick (a closed shell, so it can't come out facing the cork), turned by its tilt
        bm = fb.zbox(-w / 2, w / 2, -0.0002, 0.0002, -h / 2, h / 2)
        fb.turn(bm, -tilt, 'Y')
        fb.move(bm, c)
        o = c - U * w / 2 - V * h / 2 + Vector((0, -0.0002, 0))
        p.lettered('flyer', bm, M(mat), origin=o, u_axis=U, v_axis=V, w=w, h=h, edge=(0.5, 0.995))
        for (pm, px, pz) in pins:                     # a push pin through the top of the sheet
            q = c + U * px + V * pz
            p.add('pin', fb.cylinder((q.x, y - 0.004, q.z), 0.0055, 0.008, sides=8, rot=(90, 0, 0)), M(pm))
    return p


def photo_ledge():
    """A 1.20 m picture ledge (after a 115 x 12 cm MOSSLANDA) with the first photo in a 10 x 8 in frame leaning on it."""
    p = SignProp('CK_PhotoLedge', floor=True)
    L = 1.20
    wood = M('CC_Wood_Counter')
    p.add('back', fb.rbox(-L / 2, L / 2, -0.018, 0, 0, 0.080, bevel=0.003), wood)
    p.add('shelf', fb.rbox(-L / 2 + 0.002, L / 2 - 0.002, -0.120, -0.016, 0, 0.018, bevel=0.003), wood)
    p.add('lip', fb.rbox(-L / 2 + 0.004, L / 2 - 0.004, -0.120, -0.102, 0.016, 0.042, bevel=0.003), wood)
    # the frame, built upright with its back on y = 0 and its foot on z = 0, then leaned 8 degrees on the wall
    fw, fh, fd, m = 0.254, 0.203, 0.020, 0.020
    lean = 8.0
    frame_x = -0.36                                    # the first of four places along the ledge
    parts = []
    dark = M('CC_Wood_Espresso')
    parts.append(('frame top', fb.rbox(frame_x - fw / 2, frame_x + fw / 2, -fd, 0, fh - m, fh, bevel=0.002), dark))
    parts.append(('frame foot', fb.rbox(frame_x - fw / 2, frame_x + fw / 2, -fd, 0, 0, m, bevel=0.002), dark))
    parts.append(('frame side', fb.rbox(frame_x - fw / 2, frame_x - fw / 2 + m, -fd - 0.002, 0, m - 0.002, fh - m + 0.002, bevel=0.002), dark))
    parts.append(('frame side', fb.rbox(frame_x + fw / 2 - m, frame_x + fw / 2, -fd - 0.002, 0, m - 0.002, fh - m + 0.002, bevel=0.002), dark))
    parts.append(('mount', fb.zbox(frame_x - fw / 2 + m - 0.002, frame_x + fw / 2 - m + 0.002, -0.012, -0.006, m - 0.002, fh - m + 0.002), M('CF_Mat')))
    pw_, ph_ = 0.178, 0.127
    pz0 = (fh - ph_) / 2
    photo = fb.zbox(frame_x - pw_ / 2, frame_x + pw_ / 2, -0.0130, -0.0118, pz0, pz0 + ph_)
    a = math.radians(lean)
    shift = Vector((0, -0.004 - fh * math.sin(a), 0.017))
    for name, bm, mat in parts:
        fb.turn(bm, -lean, 'X', pivot=(0, 0, 0))
        fb.move(bm, shift)
        p.add(name, bm, mat)
    fb.turn(photo, -lean, 'X', pivot=(0, 0, 0))
    fb.move(photo, shift)
    U = Vector((1, 0, 0))
    V = Vector((0, math.sin(a), math.cos(a)))           # the frame's up, leaned back
    o = Vector((frame_x - pw_ / 2, -0.0130, pz0))
    o = Vector((o.x, o.y * math.cos(a) + o.z * math.sin(a), -o.y * math.sin(a) + o.z * math.cos(a))) + shift
    p.lettered('photo', photo, M('CF_Photo1'), origin=o, u_axis=U, v_axis=V, w=pw_, h=ph_, edge=(0.003, 0.5))
    return p


def tent_card():
    """One pastry tent card: two 89 x 51 mm faces folded at the top, leaning 20 degrees each way. Material
    CF_TentCard1 (Morning bun); the other two copies take CF_TentCard2 and CF_TentCard3 in Unity."""
    p = SignProp('CK_TentCard', floor=True)
    w, h, t, lean = 0.089, 0.051, 0.0008, 20.0
    a = math.radians(lean)
    drop = h - h * math.cos(a)
    front = fb.zbox(-w / 2, w / 2, -t, 0, 0, h)
    fb.turn(front, -lean, 'X', pivot=(0, 0, h))
    fb.move(front, (0, 0, -drop))
    back = fb.zbox(-w / 2, w / 2, 0, t, 0, h)
    fb.turn(back, lean, 'X', pivot=(0, 0, h))
    fb.move(back, (0, 0, -drop))
    U = Vector((1, 0, 0))
    Vf = Vector((0, math.sin(a), math.cos(a)))          # up the front face, toward the fold
    of = Vector((-w / 2, -h * math.sin(a) - t * math.cos(a), 0.0))
    p.lettered('front face', front, M('CF_TentCard1'), origin=of, u_axis=U, v_axis=Vf, w=w, h=h, edge=(0.01, 0.5))
    Ub = Vector((-1, 0, 0))
    Vb = Vector((0, -math.sin(a), math.cos(a)))
    ob = Vector((w / 2, h * math.sin(a) + t * math.cos(a), 0.0))
    p.lettered('back face', back, M('CF_TentCard1'), origin=ob, u_axis=Ub, v_axis=Vb, w=w, h=h, edge=(0.01, 0.5))
    return p


def aframe_face():
    """The chalk face for the existing patio A-frame (0.60 x 0.90): a 3 mm board laid on the A-frame's own face."""
    p = SignProp('CK_AFrameFace', floor=True)
    W, H, t = 0.60, 0.90, 0.003
    p.lettered('chalk face', fb.zbox(-W / 2, W / 2, -t, 0, 0, H), M('CF_AFrame'), origin=(-W / 2, -t, 0),
               u_axis=(1, 0, 0), v_axis=(0, 0, 1), w=W, h=H, edge=(0.005, 0.5))
    return p


PIECES = [
    (menu_board, 'Menu', 'menu board 2.60 x 1.00 (panel 2.50 x 0.92), chestnut frame, brass picture light 1.20 m'),
    (fascia, 'Street', 'painted fascia 2.60 x 0.55 with a drip cap'),
    (blade_sign, 'Street', 'round blade sign Ø 0.80 on a steel bracket (wall face x = 0; origin at the disc\'s bottom)'),
    (lambda: gilding('L'), 'Street', 'gold leaf, left window, 1.30 x 0.8 (transparent)'),
    (lambda: gilding('R'), 'Street', 'gold leaf, right window, 1.30 x 0.8 (transparent)'),
    (entry_mosaic, 'Floor', 'ACE\'S entry mosaic 2.40 x 1.20, 2 mm'),
    (enamel_sign, 'Repair bar', 'enamel REPAIRS 0.90 x 0.25 on two chains, 0.18 drop (origin at the top)'),
    (staff_plate, 'Back door', 'brass STAFF ONLY plate 0.25 x 0.08'),
    (notice_board, 'Wall', 'notice board, cork 0.90 x 0.60 in a chestnut frame, four flyers pinned'),
    (photo_ledge, 'Wall', 'picture ledge 1.20 m with the opening-day photo (10 x 8 in frame)'),
    (tent_card, 'Pastry case', 'pastry tent card 89 x 51 mm a face (x3: CF_TentCard1..3)'),
    (aframe_face, 'Street', 'chalk face 0.60 x 0.90 for the existing patio A-frame'),
]


def build_all():
    out = []
    for builder, group, what in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'group': group, 'what': what, 'stretch': False, 'islands': n, 'floating': floating,
                    'lettered_faces': prop.lettered_faces, 'turned': prop.turned, **st})
    return out
