"""
Review renders of the café's words (brief D), each at the distance it is read: the menu from the queue (5 m), the
street front from across the street (13 m), the mosaic from the overhead camera's angle (50 degrees down from the
south, 20 m) and close, the parts wall's labels and the notice board at 0.6 m, the enamel sign from a repair-bar stool,
and the six tiling materials tiled 3 x 3 under warm light. Cycles on the CPU, small and few samples (a shared machine).

    python3 review_cafe_signs.py [renders folder] [textures folder] [only,names]
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402  (first: bmesh and mathutils come with it)
import bmesh  # noqa: E402
import cafe_signs as cs  # noqa: E402
import fixit_blender as fb  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'renders')
TEX = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, 'tex')
ONLY = set(sys.argv[3].split(',')) if len(sys.argv) > 3 else None
SAMPLES = 16


def img(name, non_color=False):
    im = bpy.data.images.get(name) or bpy.data.images.load(os.path.join(TEX, name), check_existing=True)
    if non_color:
        im.colorspace_settings.name = 'Non-Color'
    return im


def textured(mat_name, tex_file, smooth, alpha=False, scale=1.0, normal=None, emission=None):
    m = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Roughness'].default_value = max(0.05, 1 - smooth)
    bsdf.inputs['Metallic'].default_value = 0.0
    if tex_file:
        tx = nt.nodes.new('ShaderNodeTexImage')
        tx.image = img(tex_file)
        tx.interpolation = 'Cubic'
        if scale != 1.0:
            uvn = nt.nodes.new('ShaderNodeTexCoord')
            mp = nt.nodes.new('ShaderNodeMapping')
            mp.inputs['Scale'].default_value = (scale, scale, 1)
            nt.links.new(uvn.outputs['UV'], mp.inputs['Vector'])
            nt.links.new(mp.outputs['Vector'], tx.inputs['Vector'])
        nt.links.new(tx.outputs['Color'], bsdf.inputs['Base Color'])
        if alpha:
            nt.links.new(tx.outputs['Alpha'], bsdf.inputs['Alpha'])
        if normal:
            nx = nt.nodes.new('ShaderNodeTexImage')
            nx.image = img(normal, non_color=True)
            if scale != 1.0:
                nt.links.new(mp.outputs['Vector'], nx.inputs['Vector'])
            nm = nt.nodes.new('ShaderNodeNormalMap')
            nt.links.new(nx.outputs['Color'], nm.inputs['Color'])
            nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*fb.rgb('#' + emission), 1)
        bsdf.inputs['Emission Strength'].default_value = 4.0
    return m


def dress(ob):
    """Swap each lettered material for a textured one (the FBX's flat stand-ins stay for the rest)."""
    for i, m in enumerate(ob.data.materials):
        hexstr, smooth, tex = cs.MATERIALS.get(m.name, (None, 0.5, None))
        if tex:
            ob.data.materials[i] = textured(m.name + '.r', tex, smooth, alpha=m.name in cs.TRANSPARENT)
        elif m.name in cs.EMISSION:
            ob.data.materials[i] = textured(m.name + '.r', None, smooth, emission=cs.EMISSION[m.name])


def flat(name, hexstr, rough=0.6):
    return fb.material(name, hexstr, rough=rough)


def slab(name, x0, x1, y0, y1, z0, z1, mat):
    bm = fb.zbox(x0, x1, y0, y1, z0, z1)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def place(ob, loc=(0, 0, 0), rot_z=0.0):
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(rot_z))
    return ob


def light(name, kind, loc, rot, energy, colour='#FFE2C0', size=1.0, spot=None):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = fb.rgb(colour)
    if kind == 'AREA':
        ld.size = size
    if kind == 'SPOT' and spot:
        ld.spot_size = math.radians(spot)
    ob = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = tuple(math.radians(a) for a in rot)
    return ob


def world(hexstr='#3A3632', strength=0.6):
    w = bpy.data.worlds.get('R') or bpy.data.worlds.new('R')
    bpy.context.scene.world = w
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (*fb.rgb(hexstr), 1)
    bg.inputs['Strength'].default_value = strength


def cam(loc, target, fov_v=None, lens=None):
    c = fb.camera('Cam', loc, target, lens=lens or 35.0)
    if fov_v:
        c.data.sensor_fit = 'VERTICAL'
        c.data.angle_y = math.radians(fov_v)
    return c


def shoot(name, c, res=(900, 600)):
    if ONLY and name not in ONLY:
        return
    fb.render(os.path.join(OUT, name + '.png'), c, res=res, samples=SAMPLES)
    print('rendered', name)


def reset():
    fb.clear_scene()
    for coll in (bpy.data.materials, bpy.data.meshes, bpy.data.lights, bpy.data.cameras):
        for b in list(coll):
            if b.users == 0:
                coll.remove(b)


def built():
    out = {}
    for r in cs.build_all():
        ob = r['object']
        dress(ob)
        out[ob.name] = ob
    return out


# ---------------------------------------------------------------- 1. the menu from the queue (5 m), the till's wall

def menu():
    reset()
    P = built()
    for n, ob in P.items():
        if n != 'CK_MenuBoard':
            bpy.data.objects.remove(ob, do_unlink=True)
    place(P['CK_MenuBoard'], (0, 0, 1.98))
    slab('wall', -5, 5, 0, 0.2, 0, 3.2, flat('Chalk', '#E6E2D8', 0.9))
    slab('tile', -5, 5, -0.02, 0.0, 1.0, 1.75, textured('Tile', 'CF_Tile_color.png', 0.7, scale=1.0, normal='CF_Tile_normal.png'))
    slab('shelf', -5, 5, -0.30, 0.0, 1.75, 1.81, flat('Chestnut', '#8F5B32', 0.35))
    slab('floor', -5, 5, -8, 0.2, -0.01, 0, flat('Floor', '#CDB797', 0.6))
    world('#4A4440', 0.5)
    light('Room', 'AREA', (0, -4.0, 3.0), (60, 0, 0), 140, '#FFE6C8', size=4.0)
    light('Picture', 'SPOT', (0, -0.21, 3.08), (35, 0, 0), 25, '#FFD7A8', spot=110)
    shoot('menu_from_queue_5m', cam((0.0, -5.1, 1.65), (0, 0, 2.45), fov_v=60))
    shoot('menu_close_2m', cam((0.6, -2.2, 1.70), (0.3, 0, 2.48), fov_v=50))


# ---------------------------------------------------------------- 2. the street front from across the street

def street():
    reset()
    P = built()
    keep = {'CK_Fascia', 'CK_WindowGilding_L', 'CK_WindowGilding_R', 'CK_BladeSign', 'CK_AFrameFace'}
    for n, ob in list(P.items()):
        if n not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
    green = flat('Frames', '#2E5E4A', 0.5)
    glass = flat('Glass', '#3E4E52', 0.15)
    slab('band', -7.4, 7.4, 0, 0.25, 2.90, 3.60, green)
    slab('bulkhead', -7.4, 7.4, 0, 0.25, 0, 0.78, green)
    for (x0, x1) in ((-7.4, -1.15), (1.15, 7.4)):     # two panes a side, about 3.1 m each
        slab('glass', x0 + 0.06, x1 - 0.06, 0.08, 0.10, 0.78, 2.90, glass)
        xm = -4.30 if x0 < 0 else 4.18
        for xx in (x0, x1 - 0.12, xm):
            slab('mullion', xx, xx + 0.12, 0, 0.18, 0.78, 2.90, green)
    slab('door', -1.15, 1.15, 0.3, 0.4, 0, 2.90, flat('Dark', '#2A2622', 0.6))
    slab('pavement', -12, 12, -7, 0.2, -0.02, 0, flat('Pave', '#A8A39A', 0.8))
    place(P['CK_Fascia'], (0, -0.002, 2.98))
    # seen from the street the left window is the west one (Unity -x); each gilding centred on the pane by the door
    place(P['CK_WindowGilding_L'], (-2.70, 0.078, 1.30))
    place(P['CK_WindowGilding_R'], (2.70, 0.078, 1.30))
    slab('west wall', -7.6, -7.4, 0, 18, 0, 3.6, green)
    place(P['CK_BladeSign'], (-7.6, -0.6, 2.50), rot_z=180)  # out from the west wall (the mock's left), near the corner
    face = P['CK_AFrameFace']
    face.location = (2.3, -1.6, 0.08)
    face.rotation_euler = (math.radians(-12), 0, 0)
    world('#AFC3CC', 0.9)
    light('Sun', 'SUN', (0, 0, 10), (55, 0, 20), 3.5, '#FFF1DE')
    shoot('street_front_13m', cam((0.0, -13.0, 1.65), (0.6, 0, 2.0), fov_v=40))
    shoot('street_signs_6m', cam((-4.2, -6.5, 1.65), (-5.2, 0, 2.4), fov_v=45))


# ---------------------------------------------------------------- 3. the mosaic from the overhead camera

def mosaic():
    reset()
    P = built()
    for n, ob in list(P.items()):
        if n != 'CK_EntryMosaic':
            bpy.data.objects.remove(ob, do_unlink=True)
    # Unity: mosaic at (0, 0, 0.8), no turn -> Blender (0, -0.8, 0); the overhead camera 20 m away from the south,
    # 50 degrees down -> Unity (0, 15.3, -12.06) -> Blender (0, 12.06, 15.3)
    place(P['CK_EntryMosaic'], (0, -0.8, 0))
    slab('floor', -7.4, 7.4, -18, 0, -0.01, 0, textured('Oak', 'CF_Floor_Oak_color.png', 0.45, scale=14.8 / 1.8, normal='CF_Floor_Oak_normal.png'))
    slab('threshold', -1.0, 1.0, 0, 0.3, -0.01, 0.01, flat('Sill', '#6B6862', 0.5))
    world('#3A3632', 0.4)
    light('Room', 'AREA', (0, -3, 3.1), (0, 0, 0), 160, '#FFE6C8', size=6.0)
    target = (0, -0.8, 0)
    shoot('mosaic_overhead_camera', cam((0, 12.06, 15.3), target, fov_v=44))
    shoot('mosaic_overhead_zoom', cam((0, 12.06, 15.3), target, fov_v=7))
    shoot('mosaic_from_the_door', cam((0, 1.2, 1.65), (0, -2.2, 0), fov_v=60))


# ---------------------------------------------------------------- 4. close: the parts wall's labels, the notice board

def parts_wall_standin():
    """A stand-in for the bar lead's CK_PartsWall: 18 x 4 drawers, brass frames, each card UV'd to its atlas cell."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    W, H = 3.60, 0.75
    cw, chh = W / 18, H / 4
    card_w, card_h = 0.073, 0.032
    lab = textured('Labels', 'CF_DrawerLabels.png', 0.15)
    front = flat('Espresso', '#4A3422', 0.6)
    brass = flat('Brass', '#B8933F', 0.4)
    me = bpy.data.meshes.new('parts')
    obs = []
    for col in range(18):
        for row in range(4):
            x0 = -W / 2 + col * cw
            z1 = H - row * chh
            cx, cz = x0 + cw / 2, z1 - chh * 0.42
            vs = [bm.verts.new(p) for p in ((cx - card_w / 2, -0.012, cz - card_h / 2), (cx + card_w / 2, -0.012, cz - card_h / 2),
                                            (cx + card_w / 2, -0.012, cz + card_h / 2), (cx - card_w / 2, -0.012, cz + card_h / 2))]
            f = bm.faces.new(vs)
            u0, u1 = col / 18, (col + 1) / 18
            v0, v1 = 1 - (row + 1) / 4, 1 - row / 4
            for loop, (u, v) in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                loop[uv].uv = (u, v)
            f.normal_flip()
            obs.append((x0, z1))
    for f in bm.faces:
        if f.normal.y > 0:
            f.normal_flip()
    bm.to_mesh(me)
    bm.free()
    me.materials.append(lab)
    ob = bpy.data.objects.new('labels', me)
    bpy.context.scene.collection.objects.link(ob)
    for (x0, z1) in obs:
        slab('drawer', x0 + 0.004, x0 + cw - 0.004, -0.010, 0.0, z1 - chh + 0.004, z1 - 0.004, front)
        cx, cz = x0 + cw / 2, z1 - chh * 0.42
        for (a, b, c, d) in ((cx - 0.0395, cx + 0.0395, cz + 0.016, cz + 0.019), (cx - 0.0395, cx + 0.0395, cz - 0.019, cz - 0.016),
                             (cx - 0.0395, cx - 0.0365, cz - 0.019, cz + 0.019), (cx + 0.0365, cx + 0.0395, cz - 0.019, cz + 0.019)):
            slab('frame', a, b, -0.015, -0.010, c, d, brass)
        slab('pull', cx - 0.02, cx + 0.02, -0.03, -0.010, cz - 0.045, cz - 0.035, brass)
    return ob


def close():
    reset()
    P = built()
    keep = {'CK_NoticeBoard', 'CK_TentCard', 'CK_StaffPlate', 'CK_PhotoLedge'}
    for n, ob in list(P.items()):
        if n not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
    world('#4A4440', 0.5)
    # the parts wall on the back bar's top (1.00 to 1.75), seen by Ace from 0.6 m
    labels = parts_wall_standin()
    for ob in list(bpy.context.scene.collection.objects):
        if ob.name.startswith(('labels', 'drawer', 'frame', 'pull')):
            ob.location.z += 1.0
    slab('wall', -5, 5, 0, 0.2, 0, 3.2, flat('Chalk', '#E6E2D8', 0.9))
    light('Room', 'AREA', (0, -2.0, 2.6), (40, 0, 0), 60, '#FFE6C8', size=2.0)
    shoot('labels_0.6m', cam((-1.10, -0.62, 1.55), (-1.10, 0, 1.45), fov_v=55))
    # the notice board at eye height, 0.6 m
    for ob in list(bpy.context.scene.collection.objects):
        if ob.name.startswith(('labels', 'drawer', 'frame', 'pull')):
            bpy.data.objects.remove(ob, do_unlink=True)
    place(P['CK_NoticeBoard'], (0, 0, 1.15))
    place(P['CK_PhotoLedge'], (2.0, 0, 1.30))
    place(P['CK_StaffPlate'], (-1.4, 0, 1.45))
    shoot('notice_board_0.6m', cam((0, -0.62, 1.55), (0, 0, 1.47), fov_v=60))
    shoot('photo_ledge_0.6m', cam((1.75, -0.62, 1.50), (1.70, 0, 1.38), fov_v=50))
    shoot('staff_plate_0.6m', cam((-1.4, -0.60, 1.55), (-1.4, 0, 1.49), fov_v=40))
    # the tent cards on the pastry case's top, from the queue side, 0.7 m
    for n in ('CK_NoticeBoard', 'CK_PhotoLedge', 'CK_StaffPlate'):
        bpy.data.objects.remove(P[n], do_unlink=True)
    slab('case top', -0.6, 0.6, -0.3, 0.3, 1.0, 1.02, flat('Oak', '#9A7044', 0.4))
    tc = P['CK_TentCard']
    place(tc, (-0.25, -0.1, 1.02))
    for i, x in ((2, 0.0), (3, 0.25)):
        c = tc.copy()
        c.data = tc.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.location = (x, -0.1, 1.02)
        for k, m in enumerate(c.data.materials):
            if m.name.startswith('CF_TentCard1'):
                c.data.materials[k] = textured('CF_TentCard%d.r' % i, 'CF_TentCard%d.png' % i, 0.2)
    shoot('tent_cards_0.7m', cam((0.0, -0.75, 1.30), (0, -0.1, 1.04), fov_v=40))


# ---------------------------------------------------------------- 5. the enamel sign from a repair-bar stool

def enamel():
    reset()
    P = built()
    for n, ob in list(P.items()):
        if n != 'CK_EnamelSign':
            bpy.data.objects.remove(ob, do_unlink=True)
    place(P['CK_EnamelSign'], (0, 0, 2.88))
    slab('beam', -3, 3, -0.10, 0.10, 2.88, 3.20, flat('Chestnut', '#8F5B32', 0.6))
    slab('ceiling', -6, 6, -6, 6, 3.2, 3.25, flat('Chalk', '#E6E2D8', 0.9))
    world('#4A4440', 0.6)
    light('Room', 'AREA', (0, -2.5, 2.2), (70, 0, 0), 80, '#FFE6C8', size=3.0)
    shoot('enamel_from_stool', cam((0.6, -1.85, 1.25), (0, 0, 2.55), fov_v=50))


# ---------------------------------------------------------------- 6. the six materials, 3 x 3 each, under warm light

def materials():
    reset()
    names = ['CF_Stone', 'CF_Tile', 'CF_Floor_Oak', 'CF_Floor_Concrete', 'CF_Boards', 'CF_Fabric']
    for i, n in enumerate(names):
        hexstr, smooth, tile = cs.TILING[n]
        m = textured(n + '.r', n + '_color.png', smooth, scale=3.0, normal=n + '_normal.png')
        x, y = (i % 3) * 1.15 - 1.15, (i // 3) * -1.15 + 0.6
        bm = fb.zbox(x - 0.5, x + 0.5, y - 0.5, y + 0.5, 0, 0.01)
        uvl = bm.loops.layers.uv.verify()
        for f in bm.faces:
            for loop in f.loops:
                loop[uvl].uv = ((loop.vert.co.x - (x - 0.5)), (loop.vert.co.y - (y - 0.5)))
        me = bpy.data.meshes.new(n)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(m)
        ob = bpy.data.objects.new(n, me)
        bpy.context.scene.collection.objects.link(ob)
    world('#3A3632', 0.5)
    light('Warm sun', 'SUN', (0, 0, 5), (62, 0, -35), 3.2, '#FFD9A8')    # low and warm: the normal maps show
    c = cam((0, 0.025, 6.0), (0, 0.025, 0))
    c.data.type = 'ORTHO'
    c.data.ortho_scale = 3.55
    shoot('materials_3x3_warm', c)
    shoot('materials_3x3_oblique', cam((0, -2.2, 1.6), (0, 0.15, 0), fov_v=48))


for fn in (menu, street, mosaic, close, enamel, materials):
    fn()
