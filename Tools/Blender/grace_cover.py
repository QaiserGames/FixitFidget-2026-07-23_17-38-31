"""
Grace's cover (7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md sections 4 and 6.1): two pieces at least 1.1 m tall
for session 5, where a crouched Ace (a 1.0 m capsule) hides behind anything 1.1 m tall from about 1.5 m away:

  * GH_KitchenIsland: a bar-height island, 1.10 x 0.56 m and 1.12 m to its worktop (a real bar height; a 44-inch
    island), in her kitchen's cream with the cafe's chestnut top: two Shaker doors and two drawers on its front
    (-Y), a towel rail on its back, a dark kick. Its whole length is solid from the kick up, so it covers along all
    of it.
  * GH_BoxStack: three taped cardboard boxes, 1.14 m tall together, each turned a little on the one below, the way
    a stack she hasn't got round to stands in a corner.

Both follow the furniture library's conventions (fixit_blender.py, grace_house.py): origin at the bottom centre,
the front toward -Y, flat shading, one-segment bevels, parts overlapping 2-4 mm, the audit. Materials are Grace's
(GH_Kitchen_Cream, GH_Cardboard, GH_Tape) and the cafe's (CC_Wood_Counter, CC_Dark_Kick, DC_Steel), all of which
already exist in Unity.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixit_blender as fb  # noqa: E402
import grace_house as gh  # noqa: E402

M = gh.M

ISLAND_W, ISLAND_D, ISLAND_H = 1.10, 0.56, 1.12       # across, deep, to the top of the worktop
BOX_STACK_H = 1.14


def kitchen_island():
    p = fb.Prop('GH_KitchenIsland')
    cream, top, T = 'GH_Kitchen_Cream', M('CC_Wood_Counter'), .04
    hw, hd = ISLAND_W / 2, ISLAND_D / 2
    fy, by = -hd + .02, hd - .02                        # the carcass's front and back faces (the top overhangs 2 cm)
    p.add('kick', fb.zbox(-hw + .05, hw - .05, fy + .04, by - .04, 0, .105), M('CC_Dark_Kick'))
    p.add('carcass', fb.rbox(-hw + .02, hw - .02, fy, by, .10, ISLAND_H - T + .004, bevel=.006), M(cream))
    p.add('worktop', fb.rbox(-hw, hw, -hd, hd, ISLAND_H - T, ISLAND_H, bevel=.008), top)
    # the front: two doors below, two drawers above
    for x0, x1 in ((-hw + .035, -.015), (.015, hw - .035)):
        gh.shaker_door(p, x0, x1, .13, .70, fy, cream, 'knob' if x0 < 0 else 'knob-left')
        gh.shaker_door(p, x0, x1, .73, 1.00, fy, cream, 'bar', label='drawer')
    # the back: a towel rail, and a plain panel line so the back isn't a blank
    p.add('back panel', fb.zbox(-hw + .055, hw - .055, by - .002, by + .010, .16, ISLAND_H - T - .06), M(cream))
    p.add('rail', fb.cylinder((0, by + .045, .92), .009, .50, sides=6, rot=(0, 90, 0)), M('DC_Steel'))
    for x in (-.24, .24):
        p.add('rail post', fb.zbox(x - .009, x + .009, by + .004, by + .048, .908, .932), M('DC_Steel'))
    return p


def box_stack():
    """Three closed cardboard boxes, taped, each turned a little; the bottom one on the floor."""
    p = fb.Prop('GH_BoxStack')
    card, tape = M('GH_Cardboard'), M('GH_Tape')
    boxes = [  # (across, deep, tall, turn)
        (.56, .44, .40, 0.0),
        (.50, .40, .38, 9.0),
        (.46, .36, .36, -7.0),
    ]
    z = 0.0
    for i, (w, d, h, turn) in enumerate(boxes):
        z0 = z - (.003 if i > 0 else 0.0)                     # sits 3 mm into the box below
        parts = []
        parts.append(('box', fb.rbox(-w / 2, w / 2, -d / 2, d / 2, z0, z + h, bevel=.004), card))
        # the flaps' seam: two lips proud of the top, meeting along the middle, and the tape over them
        for x0, x1 in ((-w / 2 + .01, -.004), (.004, w / 2 - .01)):
            parts.append(('flap', fb.zbox(x0, x1, -d / 2 + .01, d / 2 - .01, z + h - .002, z + h + .006), card))
        parts.append(('tape', fb.zbox(-.03, .03, -d / 2 - .002, d / 2 + .002, z + h + .004, z + h + .009), tape))
        parts.append(('tape (front)', fb.zbox(-.03, .03, -d / 2 - .006, -d / 2 + .002, z + h - .07, z + h + .008), tape))
        parts.append(('tape (back)', fb.zbox(-.03, .03, d / 2 - .002, d / 2 + .006, z + h - .07, z + h + .008), tape))
        for name, bm, mat in parts:
            if turn:
                fb.turn(bm, turn, 'Z', (0, 0, 0))
            p.add(name, bm, mat)
        z += h
    assert abs(z - BOX_STACK_H) < 1e-6, z
    return p


PIECES = [
    (kitchen_island, 'Kitchen', "bar-height island, 1.12 m to the worktop: cover along its whole length"),
    (box_stack, 'Anywhere', "three taped boxes, 1.14 m stacked: cover where a corner needs it"),
]


def build_all():
    out = []
    for builder, room, what in PIECES:
        prop = builder()
        ob = prop.finish()
        n, floating = fb.audit(ob)
        st = fb.stats(ob)
        out.append({'object': ob, 'room': room, 'what': what, 'islands': n, 'floating': floating, **st})
    return out
