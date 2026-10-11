"""
The cafe's intake tray, v1 (playtest 4 fixes, 10 Oct 2026): CK_IntakeTray, one per intake slot on the front counter.

THE FINDING (playtester-first-hour, 10 Oct, Counter, L): "The intake shelf has no object of its own: devices lie on the
bare stone in front of the till (slots at x -1.56...1.56, z 13.65), but the card says 'pick up ... from the intake
shelf'." Mansoor's standard: "i want detail for every item in the cafe itself"; every part must mean something.

REAL LIFE FIRST (measured references; the house style then simplifies, then the purpose rule cuts):
  * Otto Frei's watch-service counter tray ("Watch Bracelet Sizing Tray", sold for "bench work and retail service
    counters"): 14-3/4 x 8-1/4 x 1 in (375 x 210 x 25 mm), a black velvet pad 7-1/2 x 14 in inside, "keeps them from
    sliding". https://www.ottofrei.com/products/jewelry-tray-black-velvet-pad-full-size-14-3-4x8-1-4-1in-watch-bracelet-sizing
    -> a low wooden tray with a soft lining is what a repair counter puts a customer's watch on.
  * Carver's solid-wood front-loading desk tray (the classic in-tray): 13.62 x 10.62 x 2.12 in (346 x 270 x 54 mm),
    "Cutout in the front-loading tray offers easy access". https://www.officecrave.com/carver-cw07212.html
    -> the in-tray's open front: the side the person who takes things out reaches in from.
  So: a chestnut tray (the room's one wood) lined with felt in the brand green (the bench mat's and the REPAIRS sign's
  green: green is where repairs live), walled on the customers' side and the two ends, OPEN on Ace's side.

THE GAME'S NUMBERS (measured from the scene and the prefabs, 10 Oct; analysis/envelope.py):
  * The slots (IntakeShelf's ItemSlotArea): x 1.56, 0.78, 0.00, -0.78, -1.56 at y 1.100, z 13.65, turned 180 (their +z
    looks at the queue). Three are open from Day 1, two come with Shelf Space. The stone (CK_FrontCounter) is at 1.095.
  * A device lands at slot + up * restHeight, turned up to +-12 deg and moved up to +-2 cm (PlacementJitter,
    CustomerBrain.shelfYawJitter 12 / shelfOffsetJitter 0.02). Worst cases, slot-local (+z toward the queue):
      Grace's camera body x +-0.160, z -0.135..+0.125 (its strap hangs at 30 mm, over the rim);
      the pocket watch (case and bow) x +-0.095, z -0.095..+0.149; the phones x +-0.069, z +-0.096.
  * Register v4 stands 0.146 m behind slot 2 (its front at z 13.796): the staff side has no more room than that, the
    customers' side has 0.35 m (the stone's edge at z 13.30).
  * Device bottoms over the slot point: hero phone +4.2 mm (rest 0.010, lowest point -5.8 mm), plain phone +4.0, Grace's
    camera +3.0 (rest 0.004, lowest -1.0). The pocket watch -11.7 (rest 0.010 but its back's screw heads are 21.7 mm
    under its anchor: it sinks into the bare stone today too; flagged, not this file's to fix).

THE TRAY (slot-local, Unity: +x = Blender -x, +z (toward the queue) = Blender -y, up = Blender +z):
  * 0.360 across x 0.305 deep x 0.020 high. Felt bed 0.333 x 0.287 inside (it holds the worst case of every device).
  * Origin: under the slot point, on the tray's bottom (where it stands on the stone). The footprint runs 0.167 m toward
    the queue (the walled front) and 0.138 m toward Ace (the open side): 8 mm short of the register behind slot 2.
  * The felt's top is 8 mm over the stone (1.103): the phones and the camera rest on it (0 to 1.2 mm), no slot moves.
  * Rim 15 mm wide, 20 mm high (a jeweller's tray is 25 mm; low enough that the camera's strap (30 mm) passes over it and
    the devices stay in view from Ace's eye). The two end rims ramp down to 11 mm over their last 28 mm at the open side.

THE PURPOSE RULE (every part, and what was cut):
  * the felt: where a device waits (job 2: how the place works; the read from above at 34 m is this green patch);
  * the walled front: nothing slides off toward the customers (job 2) and it is the tray's silhouette (job 3);
  * the open side and its ramps: where Ace's hand goes in (job 2: the player takes from this side);
  * the chestnut and its grain: this room's one wood (job 4).
  * CUT: a back rim (it would stand against the register at slot 2, and Ace takes from that side); a brass "IN" plate
    or slot numbers (2 px from the overhead camera; a number in the world is the HUD's job, and the job's colour tag
    already ties a device to its ticket); feet and corner brasses (nobody sees them; they mean nothing); a ticket clip
    (the colour tag does that job).

Conventions (the house kit's): 1 unit = 1 m, Z up, the front (the customers' side) faces -Y (Unity +Z); flat-shaded,
one-segment chamfers, metallic 0; parts overlap 0.5-11 mm, no two visible faces in one plane, nothing floating
(fb.audit). Box UVs in metres (fb.Prop.finish) so Unity's "Cafe grain" texture lands at its real scale.
Unity (x, y, z) = Blender (-x, z, -y). No collider: Unity's step adds none (the device on it keeps E and the crosshair).

Run with bpy (Blender 5.2.2 as a module) or inside Blender; export_cafe_intake.py writes the FBX, manifest and .blend.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.environ.get('FIXIT_BLENDER_LIB', '')):
    if _p and _p not in sys.path:
        sys.path.insert(0, _p)
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
import fixit_blender as fb  # noqa: E402

# ============================================================================= materials
# (sRGB hex, Unity smoothness, why). Metallic 0 everywhere (the house rule).
EXISTING = {
    'CC_Wood_Counter': ('#8F5B32', 0.71, 'chestnut, the room\'s one wood: the board and the rims (Kit 1 maps it to '
                                         '"Cafe grain - CC_Wood_Counter", the grain at 1 m a tile)'),
}
CF_MATERIALS = {
    'CF_Felt': ('#2E7D5B', 0.08, 'NEW: the felt bed in the brand green (the bench mat\'s and the REPAIRS sign\'s green: '
                                'green is where repairs live). Felt is matte: the existing DC_Sticker_Green (0.45) would '
                                'take a highlight from the counter\'s pendants and read as plastic.'),
}


def M(name):
    hexstr, smooth, _ = EXISTING.get(name) or CF_MATERIALS[name]
    return fb.material(name, hexstr, rough=max(0.05, 1.0 - smooth), metal=0.0)


# ============================================================================= the numbers (metres; Blender frame)
ACROSS = 0.360          # x: +-0.180
FRONT = 0.167           # the walled front's outer face is 0.167 m from the slot point toward the queue (Blender -y)
BACK = 0.138            # the open side's edge is 0.138 m from the slot point toward Ace (Blender +y)
HEIGHT = 0.020          # the rims' top over the stone
RIM = 0.015             # rim width
FELT_TOP = 0.008        # the felt's top over the stone: where a device rests
FELT = 0.0025           # felt thickness
BOARD_TOP = 0.0065      # the board under the felt (the felt overlaps it 0.5 mm)
BOARD_LIFT = 0.0005     # the board's underside, 0.5 mm up: its bottom is never in one plane with the rims'
FELT_SHORT = 0.005      # the felt stops 5 mm short of the open edge: the board's lip shows
REVEAL = 0.0015         # the felt tucks 1.5 mm under the rims; the end rims overlap the front rim by 1.5 mm
STEP = 0.001            # the end rims stand 1 mm inside the front rim's ends: the butt joint shows as a 1 mm step
RAMP = 0.028            # the end rims ramp down over their last 28 mm at the open side...
RAMP_LOW = 0.011        # ...to 11 mm (3 mm over the felt)
CHAMFER = 0.0025        # the one chamfer on the rims
LIP_CHAMFER = 0.0015    # and on the board's open edge

HX = ACROSS / 2
INNER_X = HX - RIM                  # 0.165: the end rims' inner faces
INNER_FRONT = -(FRONT - RIM)        # -0.152: the front rim's inner face (Blender y)


def prism_x(profile_yz, x0, x1):
    """A solid from a closed (y, z) profile (counter-clockwise seen from +x), swept along x from x0 to x1."""
    bm = bmesh.new()
    left = [bm.verts.new((x0, y, z)) for (y, z) in profile_yz]
    right = [bm.verts.new((x1, y, z)) for (y, z) in profile_yz]
    n = len(profile_yz)
    bm.faces.new(list(reversed(left)))
    bm.faces.new(right)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([left[i], left[j], right[j], right[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def end_rim(sign):
    """One end rim (sign +1 = Blender +x, Unity -x), full height along the front, ramped down at the open side."""
    y0 = INNER_FRONT - REVEAL                  # it butts into the front rim (1.5 mm overlap): no notch at the corner
    profile = [(y0, 0.0), (BACK, 0.0), (BACK, RAMP_LOW), (BACK - RAMP, HEIGHT), (y0, HEIGHT)]
    x0, x1 = (INNER_X, HX - STEP) if sign > 0 else (-(HX - STEP), -INNER_X)
    bm = prism_x(profile, x0, x1)
    fb.bevel_all(bm, CHAMFER, segments=1, angle=30.0)
    bmesh.ops.dissolve_degenerate(bm, dist=2e-6, edges=bm.edges[:])
    return bm


def build_tray():
    wood, felt = M('CC_Wood_Counter'), M('CF_Felt')
    p = fb.Prop('CK_IntakeTray', floor=True)

    # The board: under the felt, let 11 mm into the rims, its open edge the lip Ace's fingers meet.
    board = fb.rbox(-(HX - 0.004), HX - 0.004, -(FRONT - 0.004), BACK, BOARD_LIFT, BOARD_TOP, bevel=LIP_CHAMFER)
    p.add('board', board, wood)

    # The felt: tucked 1.5 mm under the rims, 5 mm short of the open edge.
    p.add('felt', fb.zbox(-(INNER_X + REVEAL), INNER_X + REVEAL, INNER_FRONT - REVEAL, BACK - FELT_SHORT,
                          FELT_TOP - FELT, FELT_TOP), felt)

    # The walled front (the customers' side), the full width: the end rims butt into it from behind.
    p.add('front rim', fb.rbox(-HX, HX, -FRONT, INNER_FRONT, 0.0, HEIGHT, bevel=CHAMFER), wood)

    # The two end rims, ramped at the open side.
    p.add('end rim (Unity -x)', end_rim(+1), wood)
    p.add('end rim (Unity +x)', end_rim(-1), wood)
    return p.finish()


# The device envelope the tray was sized to (slot-local, Unity axes; analysis/envelope.py), for the manifest.
ENVELOPE = {
    'jitter': {'yaw_deg': 12.0, 'offset_m': 0.02, 'source': 'CustomerBrain shelfYawJitter / shelfOffsetJitter'},
    "Grace's camera body": {'x': [-0.1603, 0.1603], 'z': [-0.1353, 0.1250], 'bottom_over_slot': 0.0030},
    'pocket watch (case and bow)': {'x': [-0.095, 0.095], 'z': [-0.095, 0.149], 'bottom_over_slot': -0.0117},
    'hero phone': {'x': [-0.0694, 0.0694], 'z': [-0.0962, 0.0959], 'bottom_over_slot': 0.0042},
    'plain phone (PhoneJob)': {'x': [-0.0688, 0.0688], 'z': [-0.0957, 0.0957], 'bottom_over_slot': 0.0040},
}

REFERENCES = [
    {'what': "Otto Frei watch-service counter tray, velvet pad: 14-3/4 x 8-1/4 x 1 in (375 x 210 x 25 mm)",
     'url': 'https://www.ottofrei.com/products/jewelry-tray-black-velvet-pad-full-size-14-3-4x8-1-4-1in-watch-bracelet-sizing'},
    {'what': "Carver solid-wood front-loading desk tray (in-tray, cut-out front): 13.62 x 10.62 x 2.12 in (346 x 270 x 54 mm)",
     'url': 'https://www.officecrave.com/carver-cw07212.html'},
]


def build_all():
    """Every piece of this kit (one), with its report: the house kit's build_all() shape."""
    ob = build_tray()
    n, floating = fb.audit(ob)
    st = fb.stats(ob)
    return [{
        'object': ob, 'group': 'Counter', 'tris': st['tris'], 'size': st['size'], 'min': st['min'], 'max': st['max'],
        'materials': st['materials'], 'islands': n, 'floating': floating,
        'what': 'the intake tray: felt-lined chestnut, walled on the customers\' side, open on Ace\'s; one under each '
                'intake slot, where an accepted device waits for the bench',
        'origin': 'under the slot point, on the tray\'s bottom (the stone); the footprint runs 0.167 m toward the '
                  'queue (Unity +z, the walled front) and 0.138 m toward Ace (Unity -z, the open side)',
        'place': 'a child of each intake slot (02 - intake and queue/IntakeShelf/Shelf_0..4), local rotation 0, local '
                 'position (0, the stone\'s height under the slot, 0): the stone is 5 mm under the slot point today',
        'collider': None,
        'felt_top': FELT_TOP, 'rim_top': HEIGHT,
        'felt_bed': {'x': [round(-INNER_X, 4), round(INNER_X, 4)], 'z': [round(-(BACK - FELT_SHORT), 4), round(FRONT - RIM, 4)]},
        'footprint': {'x': [round(-HX, 4), round(HX, 4)], 'z': [round(-BACK, 4), round(FRONT, 4)]},
        'open_side': 'Unity -z (toward Ace, the till side); the walled front faces Unity +z (the queue)',
    }]


if __name__ == '__main__':
    fb.clear_scene()
    for r in build_all():
        print(r['object'].name, r['tris'], 'tris', r['size'], 'islands', r['islands'], 'floating', r['floating'])
