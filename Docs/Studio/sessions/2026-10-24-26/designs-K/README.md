# K: the drinks machine's buttons, built (26 Oct 2026)

## On your phone (under 120 words)
You said the buttons are "just words on a square in different colors", and the drawings were "ai art slop". So this time
nothing is drawn: the six buttons are real pieces, built in Blender and rendered on your own machine from the game's own
close-up pose. Each is a chunky cap in the drink's colour (the same colour as its chip on the drip tray) with a cream label
and the drink's picture standing out of it in relief: the same cup the ticket shows. No words. The prompt still says
"Pour Latte". Look at `K-before-after-1to1.png` first: today's photo over the new one, pixel for pixel.

## The pictures
| File | What it shows |
|---|---|
| `K-before-after-1to1.png` | Today's row (your Mac's lab photo, 26 Oct) over the new row, the same camera, 1:1 pixels. Start here. |
| `K-buttons-2x.png` | The new row at twice the game's pixels, to see the relief. |
| `renders/1-first-person-at-the-machine.png` | The whole machine as the close-up opens (1920 x 1080, the game's lens), Espresso pressed. |
| `renders/2-close-the-middle-four.png` | Tea, Hot Chocolate, Espresso (pressed), Americano, twice as close. |
| `renders/3-three-quarter.png` | From the right and low: the relief, the colour on the sides, the pressed one sunk. |

The renders are Cycles, lit like the café: warm light from the front-left (the windows), the warm strip under the shelf
above, a soft cream fill. The machine is `BeverageDispenserV2.fbx` as it is, in the installer's own colours. The caps are
the exported FBX files themselves, placed where the Unity step places them.

## What it is
- **The cap:** 92 x 98 mm, 30 mm proud of the dark backing (the old tile was 94 x 90 x 26), its front edges rounded, in
  the drink's own `BF_<Drink>` material. The dark backing shows as a thin rim round it, and the chrome spout still comes
  out under it. It presses 9 mm (the tile pressed 6: a third of the new depth, so the press reads).
- **The label:** cream, 72 mm, with a thin ink rim, on the cap's face.
- **The picture:** the ticket's own icon (`Tools/UI/draw_button_icons.py`, `drink()`), rebuilt in 3D from that script's
  own pixel coordinates at 0.54 mm a pixel: every solid part (the mug, the saucer, the glass, the cream) stands 4 mm out on
  an ink plate 2.7 mm bigger, which is the icon's outline made solid; the thin ink lines (handles, steam, the tea bag's
  string) are ink ribbons; the liquid bands sit 1 mm on their cups. Flat shaded, one colour a part, the icons' own
  colours. So she matches the cup on the ticket to the cup on the machine, and the machine's light falls on it.
- **No words.** At the close-up's distance (0.94 m, a 58 degree lens) a 26 px em (the floor, as the review screen
  measures it) is 26 mm on the machine. "Hot Chocolate" in Baloo 2 Bold is about 7 em: 180 mm, on a 92 mm button with
  158 mm between buttons. "Americano" is 130 mm, "Espresso" 110. Only Coffee, Tea and Latte would fit, so one size for
  all six cannot. (The drawn designs put 24 px words on plates spanning the whole pitch, under the floor and nearly
  touching.) The word lives where it already is, the prompt line: "(A) Pour Latte", 34 px, when the crosshair is on the
  button, and "Pouring Latte" with the gold ring while it pours.
- **The machine's working parts** are untouched: the slots, the pour, the hit boxes, the aim help's targets (c2), the
  prompt. The cap is a child of the old paddle's transform, so `BeveragePaddleMotion` presses it as before.

## The references I held it to
- **Overcooked** (R4 §3): chunky, clear silhouettes, bold colour, nothing fussy; its ingredient crates say what they
  hold with one big picture and no word. The caps are that crate: one colour, one picture.
- **PlateUp!**: appliances are blocks with rounded edges and a flat-colour pictogram; the press and the pour are the
  "juice". The cap sinks a visible third of its depth.
- **Good Pizza, Great Pizza**: toppings read by shape and colour at thumbnail size; outlines keep light things readable on
  light grounds. Hence the ink plates under every part, and cream labels so a brown mug never sits on a brown cap.
- **The game's own tickets and HUD** (19 Oct, her: "icons would look better"): the same six cups, so nothing new to learn.

## What I looked at and changed
- First render: the pictures were small on the cap (a 66 mm label, 0.46 mm a pixel) and the caps were deep boxes (38 mm).
  Now 72 mm labels, 0.54 mm a pixel, 30 mm deep, a bigger rounding.
- Second render: the caps were too light. The installer's colour values are what the materials show (sRGB), so the
  machine's real colours are darker (Coffee #5c331a, not the lighter value I had converted). Fixed in the Blender palette
  and rendered again: the caps now match today's tiles and the tray's chips.

## Questions for you
1. The colours are the machine's own six (the tray's chips). Espresso's slate is the dullest; it is still told apart by
   its picture (the little cup on a saucer). Want a livelier Espresso colour? It is one material, `BF_Espresso`.
2. The word is gone from the button on purpose. If you want one back, it has to be on a plate under the machine's header,
   not on the button, and it would be at most 20 px: tell me and I'll build that plate.
