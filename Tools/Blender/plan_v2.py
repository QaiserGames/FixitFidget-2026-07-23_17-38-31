"""
Grace's house, layout v2 (29 Sept 2026): the numbers for a 1.0 m wide Ace, in one place, so the plan drawing
and the Blender mock-up can't disagree.

Plan coordinates, metres:
  X from the inside of the south wall (0) to the inside of the north party wall (5.42), along West Street;
  Y from the inside of the back wall (0) to the inside of the street wall (4.02);
  z up from the ground floor (the floor is at world y 0.15; the first floor is 2.40 above it).

Measured from the house's own mesh (`1 - Saffron bay-window house - Street palette (doorway)` in
Street doors.asset): the body is a closed box 5.60 x 4.20 x 8.20 m (world x -21.75..-17.55, z -4.80..0.80), and
its outside faces are all there is. The linings inside are 0.08 m thick with a 0.01 m gap, so 0.09 comes off
each side: 5.42 x 4.02 inside. (The overnight plan used 5.3 x 4.7: the 4.7 came from the bounding box, which
includes the bays and the stoop. The ground floor is 0.68 m shallower than drawn.)

World = (-21.66 + Y, 0.15 + z, -4.71 + X).
"""

# ---------------------------------------------------------------- Ace
ACE_RADIUS = 0.50          # the CharacterController (and the capsule you see)
ACE_SKIN = 0.08            # Unity's skin width: the controller keeps this far from things
ACE_WIDTH = 2 * (ACE_RADIUS + ACE_SKIN)   # 1.16 m: what actually has to fit
ACE_TOP = 2.0 + ACE_SKIN   # 2.08 m

# ---------------------------------------------------------------- the shell
W, D = 5.42, 4.02
LINING = 0.09
FLOOR1 = 2.40              # the first floor, above the ground floor
SLAB = 0.15                # its thickness: the ground floor's ceiling is at 2.25
FLOOR2 = 5.00              # the second floor (not playable)
CEIL1 = FLOOR2 - SLAB      # 4.85: the first floor's ceiling

# street wall features (from the mesh)
DOOR_NOW = (0.62, 1.60)            # 0.98 m, hinge on the north side
DOOR = (0.46, 1.76)                # 1.30 m, the surround 0.16 m wider each side
DOOR_HINGE_X = DOOR[1]
DOOR_HEAD = 2.19
SURROUND_NOW = (0.50, 1.72)        # the cream door surround (plan X)
SURROUND = (0.34, 1.88)            # as wide as the stoop
STOOP = (0.335, 1.885)             # the stone steps outside
RAILINGS = (0.255, 1.965)          # the iron railings either side of them
WINDOW = (2.635, 4.885)            # ground floor glass
WINDOW_SILL, WINDOW_HEAD = 0.605, 2.055
WINDOW_TRIM = (2.575, 4.945)
BAYS = ((0.61, 2.11), (3.31, 4.81))   # first and second floors; south bay first
BAY_Y = (4.11, 4.91)

# ---------------------------------------------------------------- the stairs: a quarter turn in the back-south corner
RISE, GOING = 0.20, 0.24
CLEAR = 1.40               # between the wall and the banister, both flights
BAL = 0.06                 # the banister's thickness
LANDING = (0.0, CLEAR, 0.0, CLEAR)             # x0, x1, y0, y1 at z 1.2
LOWER_BOTTOM_Y = CLEAR + 5 * GOING             # 2.60: the bottom riser, facing the front door
UPPER_TOP_X = CLEAR + 5 * GOING                # 2.60: the top riser, onto the first floor
CUPBOARD_DOOR = (1.90, 2.55)                   # in the panel under the upper flight (Y 1.40), 1.14 m high

# ---------------------------------------------------------------- ground floor
ENTRY = (0.0, DOOR[1], LOWER_BOTTOM_Y, D)      # in front of the stairs
POCKET = (CLEAR + BAL, 2.70, CLEAR + BAL, LOWER_BOTTOM_Y)   # beside the lower flight, the cupboard door behind it
KITCHEN_LANE = (2.60, W, 0.66, 2.06)          # 1.40 m in front of the counter

# pieces: (name, X, Y, z, turn); turn 0 = the piece's front faces the street (+Y), 90 = faces north (+X),
# -90 = faces south, 180 = faces the back. Positions are the piece's origin (bottom centre, or the back's
# bottom centre for wall pieces).
GROUND = {
    'GH_KitchenCounter': (3.62, 0.31, 0, 0),           # shortened to 2.0 m (one cupboard unit comes out)
    'GH_WallCupboards': (3.64, 0.0, 1.45, 0),
    'GH_Fridge': (5.03, 0.345, 0, 0),
    'GH_Kettle': (3.20, 0.42, 0.926, 15),
    'GH_Teapot': (4.25, 0.25, 0.92, -25),
    'GH_CupBox': (2.95, 0.33, 0.92, 8),                # on the worktop by the stairs
    'GH_TVCabinet': (5.175, 2.83, 0, -90),
    'GH_TV': (5.175, 2.83, 0.50, -90),
    'GH_Armchair': (4.00, 2.83, 0, 90),                # faces the TV, her back to the pocket
    'GH_StandardLamp': (3.25, 3.55, 0, 0),
    'GH_Sideboard': (3.60, 3.765, 0, 180),             # under the window, photos on it
    'GH_Frame_S': (3.20, 3.80, 0.865, 170),
    'GH_Frame_M': (3.95, 3.78, 0.865, 186),
    'GH_Rug': (3.95, 2.90, 0, 0),
    'GH_CoatStand': (2.15, 3.78, 0, 0),
    'GH_Frame_L': (5.42, 2.83, 1.35, -90),             # over the TV
    'GH_Frame_XL': (0.0, 2.00, 1.75, 90),             # on the wall over the lower flight
}

# ---------------------------------------------------------------- first floor (z = FLOOR1)
VOID = ((0.0, CLEAR + BAL, 0.0, 2.90), (CLEAR + BAL, UPPER_TOP_X, 0.0, CLEAR + BAL))
F1_LANDING = (UPPER_TOP_X, 4.00, 0.0, CLEAR + BAL)
BATHROOM = (4.10, W, 0.0, CLEAR + BAL)
BEDROOM_DOOR = (UPPER_TOP_X, 4.00)              # 1.40 m, in the wall at Y 1.46..1.56
OPENING_HEAD = 2.20
F1_WALLS = [
    # (x0, x1, y0, y1): full height on the first floor, cut where the doors are
    (CLEAR + BAL, UPPER_TOP_X, CLEAR + BAL, CLEAR + BAL + 0.10),     # the bedroom's back wall, west of its door
    (4.00, W, CLEAR + BAL, CLEAR + BAL + 0.10),                      # ... east of it (the bathroom's front)
    (CLEAR + BAL, CLEAR + BAL + 0.10, CLEAR + BAL + 0.10, 3.00),     # along the stairwell
    (0.0, CLEAR + BAL + 0.10, 2.90, 3.00),                           # the stairwell's front
    (4.00, 4.10, 0.0, CLEAR + BAL),                                  # the bathroom's side (its door in it)
]
BATHROOM_DOOR_Y = (0.32, 1.14)
FIRST = {
    # new pieces (stand-ins in the mock-up until they're built): bed, bedside tables, lamp, wardrobe, dressing table
    'Bed': (4.42, 2.90, 0, -90),           # head against the north wall, the foot toward the stairs
    'Bedside table (back)': (5.195, 1.935, 0, -90),
    'Bedside table (front)': (5.195, 3.80, 0, -90),
    'Bedside lamp': (5.195, 1.935, 0.55, 0),
    'Wardrobe': (2.075, 1.86, 0, 0),
    'Dressing table': (1.36, 4.62, 0, 180),   # in the south bay, the mirror over it
    'Bedroom rug': (3.40, 2.90, 0, 0),
}

# ---------------------------------------------------------------- her routine (placeholder times)
ROUTINE = [
    ('11:00', 'front room: TV on, in her armchair'),
    ('12:15', 'kitchen: the kettle'),
    ('12:30', 'up to bed (the landing light)'),
    ('1:00', 'the bedroom goes dark: asleep'),
    ('2:40', 'down to the kitchen for water, then back up'),
    ('4:00', 'dawn'),
]


def world(X, Y, z=0.0):
    return (-21.66 + Y, 0.15 + z, -4.71 + X)
