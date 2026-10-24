# The HUD: three mockups to pick from

*Designer M, 23 Oct 2026. Pictures only: nothing in the game has changed.*

These are three new HUDs for *Fix It Fiasco*, drawn over the game's own lab photos of 20 Oct. **Open `compare-all-three.png` first.** On a phone it scrolls: each design whole, then its top band at true size.

His note, the part this answers: "I am a fan of the way that the close and opening sign kind of like does the shaking... I would like it to look a lot nicer, to look more unique and different with the opening sign. I just don't like the way that like it just looks like a cluster... I'd rather the timing be in the middle, on the top middle... like the sun is actually changing... I do like the way that the money icon is showing. However, I would like it to look more juicy... The franchise 5 stars... I would like it to look a lot more cleaner."

## What all three do

- **The sign keeps its shake.** It has the same three faces and colours as today: OPEN on paper in brand green, LAST ORDERS on honey, CLOSED on plum. Never red, and no screen tint. It keeps the same flip, sway and closing bounce; only its drawing is new. The stills hang it at a 2–3° tilt so the swing reads. In the game it sways as today: ±1° while open, ±2.8° at last orders, still once closed.
- **The time is top centre, in a living sky.** The time is 32 px bold and the day 22 px. The sky's colour carries the hour:
  - 9 AM: dawn peach-pink;
  - 11 AM: clear blue;
  - 2 PM: a warm gold haze;
  - 6:30 PM: golden;
  - 8 PM: dusk violet;
  - 11 PM: navy with stars.

  The sun comes up from behind the roofs at 8:40 AM, so it is rising at opening. It climbs, then grows and reddens as it lowers. It sets behind the roofs at 7:50 PM, so at closing only its glow is left. Then come the moon, the stars and the city's lit windows. Each design has its own small San Francisco. All six hours are in `X-sky-strip.png`.
- **The money sits under the sign, and it is the juiciest thing:** the game's bill icon, hard against a 40 px figure. A payment takes three frames (`X-payment.png`):
  1. the bill (in C, a coin) flies up from the counter;
  2. it lands: the figure rolls up and punches, a green glow blooms, "+$12" pops;
  3. "+$12" drifts up and fades. The whole moment lasts about 1 s, today's timing.
- **The stars are five pips, with the franchise goal at the end of the row:** a little café front in A, a pennant in B, a key in C. The "Franchise at 5 stars" line goes. How the goal stays readable without it is the next section.
- **Fewer boxes.** The column is about 100 px shorter than today's: it ends about 270 px down, not 382. The stars have no plate.
- **At the bench**, the time piece folds into the strip at the top right, as today's column does (`X-bench.png`).
- **In first person**, `first-person-check.png` shows the three time pieces over the two brightest first-person views, at true size.
- **Every word was measured** against the real pixels under it: 249 words across the 57 screens drawn (`src/measurements.json`).
  - None is under 22 px. The time is 32 px, and 30 px at the bench.
  - Every word reaches at least 4.5:1. The lowest is OPEN at 4.53:1, which is today's own brand green on the sign's paper. The lowest measured on drawn pixels is 4.84:1 (B's rolling digit, mid-payment).
  - The sign's other words: "Come in" 7.3:1, LAST ORDERS 9.3:1, CLOSED 7.4:1.

## How the franchise goal stays readable (playtest 7's risk)

Playtest 7 found that the goal wasn't apparent ("You have no idea, right? No."). That is why the words went under the stars. Without that line, the goal is told four ways:

1. **Day 1's first Now-line card already names it:** "Five stars and the café can franchise. Grace is on her way."
2. **On Day 1 the words show once beside the row**, for 6 s, pointing at the goal icon: "Five stars to franchise" (`X-new-star.png`, left).
3. **When a star lands**, the star pops with a burst. That is the next morning, as the sign turns to OPEN, because the stars are settled at the recap. The goal icon glows (A), waves (B) or swings (C), and "New star! / 4 more stars to franchise" shows for 4 s (middle picture).
4. **Then the row is quiet** (right picture). The phone's Franchise app keeps the full words.

Playtest 8 checks it: after Day 1, ask her what the stars are for.

## The three

### A · Shop window
- **Idea:** the café's own painted shopfront across the top of the screen.
  - The time piece is an arched window in the café's deep green with a gold-leaf line. The Painted Ladies and downtown are in the glass, and the time sits on its sill.
  - The sign is a hand-painted board on a scrolled brass bracket.
  - The money is a green tag hung from the sign, with a gilt "$".
  - Five gold pips end at a tiny café front.
- **Why it fits:** it is the only design where the sign, the time and the money are one object: the café's identity, built around the sign he loves. It has the biggest sky of the three (316 × 100 px), so the hour reads in the corner of the eye during a rush. The café front says "another café" without words.
- **Build in UGUI:** all of it is drawn in code, as today's HUD is. That is about 17 new sprites: the arch frame and its mask, the keystone, the sill, the notched board, the bracket and S-hooks, the tag and its rings, the café front, the Ladies in two layers and their windows. It is the most to draw: about 3 builder-days in all.
- **Art:** none is needed. His girlfriend's hand would lift this one most. She could draw the Painted Ladies strip as two silhouette layers and a windows layer, light grey on transparent so the code can tint them by the hour. She could also draw the bracket.
- **Risk:** it is the biggest piece at top centre (372 × 161 px), so it must stay this clean or it becomes the clutter he dislikes. The gold must stay a line, not a theme. The Ladies read as "San Francisco" mostly to people who know them; the Transamerica spire helps.

### B · Horizon ribbon
- **Idea:** flat and quiet.
  - The time piece is a pill of sky, 480 px wide, that the sun travels along. A long, low San Francisco sits on its horizon (Sutro Tower, downtown, the Bay Bridge), and the time hangs on a tab under its middle.
  - The sign is today's enamel card, cleaned up: rounder, with a fine inset line and four screws.
  - The money is a candy-green pill that hugs its number, with the bill bursting out of its end.
  - The stars float free and end at a pennant.
- **Why it fits:** it is the closest to the prompt line he loves ("that does look so clean"). It has the fewest plates, and it is the slimmest at top centre (92 px tall). Money is the brand's green everywhere.
- **Build in UGUI:** it is the cheapest, at about 12 sprites: the pill and its mask, the tab, the long skyline in two layers and its windows, the candy pill (its gradient baked into a 9-sliced sprite), the gloss and the pennant. The sign and the pill are nearly what the game has today. About 2 builder-days in all.
- **Art:** none is needed; its city is a low silhouette.
- **Risk:** it is the least unique, and his note asks for unique: the sign is today's card. In a thin ribbon the sun mostly slides sideways (27 px of rise), which is the nearest of the three to "a circle moving". So the colour has to carry the hour. It is also the widest piece, so the tabs reach it first: 3 fit beside it, with 4 px to spare.

### C · Porthole on the Bay
- **Idea:** brass and round.
  - The time piece is a brass porthole on the Bay. The sun rises over the Marin hills, crosses, and sets behind the Golden Gate, its road gold on the water. The time sits on a plate under it.
  - The sign is a walnut plaque on one brass hook, and it swings like a pendulum.
  - The money is a brass medallion holding the bill; a coin flies in when Ace is paid.
  - The stars are set in a brass rail, with a key hanging at its end: the key to the next café.
- **Why it fits:** it gives the clearest sunrise and sunset of the three: the sun's road on the water turns gold, then the bridge's lights come on. A round window reads as a clock face. It is the most distinctive shape.
- **Build in UGUI:** about 18 sprites:
  - the brass ring with its bolts, drawn twice;
  - the Bay: hills, bridge, water, ripples and the sun's road;
  - the plaque and its hook;
  - the coin, the rail and the key.

  About 2.5 builder-days in all.
- **Art:** none is needed. Her hand would help the bridge and the hills, with the same tinting rule as A.
- **Risk:** brass and the waterfront are a new family for a café on a street grid. Its sky is the smallest: a 102 px circle, half of it water. It is as tall as A (156 px). In a shop sim a key can read as "locked".

## Building it in Unity UGUI

All three are built the way today's HUD is:
- shapes drawn in code once, with HudCorners' painter at 2 × 2 samples a pixel;
- the skin's rounded sprites and drop shadows;
- TextMesh Pro for the words;
- no shaders and no imported art.

The builder-day numbers above are estimates for the studio's way of working: a builder writes it, a reviewer reads it, then one lab run on his Mac. About 1.5 of those days are the core that all three share:

1. **One sky table:** the 13 colour keys in `src/sky.py`, the sun's path, the moon, the stars and the lit windows. `DayClock.CurrentHour` drives it by day, and the night's hour at night.
2. **The gradient.** UGUI has no gradient fill, so the sky is a 1 × 64 px texture on a RawImage. Its pixels are rewritten when the in-game minute changes.
3. **The shape.** A UI `Mask` uses the window's inner shape as a code-drawn sprite (arch, pill or circle). A Mask's edge is hard, so the frame overlaps it by 1.5 px. RectMask2D can only clip rectangles.
4. **Inside the sky**, child Images are tinted from the table each minute:
   - the sun (`UiSkin.Circle` and today's Glow sprite);
   - the moon (today's Moon sprite);
   - a star field and the clouds;
   - the skyline's layers.

   They are drawn in the order sky, sun, skyline, so the sun rises from behind the roofs.
5. **At the bench**, the piece folds into the strip in zone U2, 24–110 px down. It can't stay top centre, because the tab chips take the top band there: zone U1, 20–64 px down, running to 512 px short of the right edge. The strip keeps the time and the figure at 30 px, as today.
6. **At night** the same piece stays top centre with the night's hour. It replaces ShopUI's night clock at the top right (`clockText`). The sign, the money and the stars hide at night, as today.
7. **The tabs** stop 16 px short of the time piece. That needs a centre reserve in `TicketRailUI`, beside `RightReserve`. Three tabs fit in a row beside each design, and a fourth starts a second row; today six fit in one row. That is fine for Days 1–3 and a squeeze in a Day 4 rush.
8. **The sign keeps its code:**
   - its two faces;
   - the flip (0.72 s);
   - the sway;
   - the closing bounce;
   - the "Swing" name that the pause lab reads.

   Only the drawing changes.
9. **The payment:**
   - The bill flies from the payer's head on screen to the icon in 0.45 s (`Barks.HeadPointOf`, then `WorldToScreenPoint`). That replaces today's 0.44 s drop.
   - The last digit rolls during today's 0.55 s count-up: two TMP texts under a RectMask2D.
   - The punch, the glow and "+$12" are today's, with the chip moved to the pill's left.
   - A patron's coffee still counts up quietly.
10. **The stars** use today's Star sprite, gold once earned. The empty pip is a new outline star (a cream rim around a dark middle), so it reads over the bright street; today's empty pips are a 30 % ghost. Then come the goal icon and the two toasts.
11. **The labs.** PauseCheck reads 27 HudCorners members, and most keep their meaning. These change, and their checks with them:
    - `FranchiseLine`: the line goes.
    - `ClockLine`, `ClockGap`, `ClockLineSize` and `GapFor`: they now belong to the time piece.
    - `SunProgress`, `SunSet`, `MoonShowing` and `DialWarmth`: same names, now the sky's sun.
    - `EdgesShown`: B and C drop the stack's edges; A keeps them.
    - `TopRightBottom`: the column ends about 270 px down, not 382.

    JuiceCheck reads the takings. GraceAtHomeCheck reads `ClockLine` at night, so it would now see the night's time. The stock chips at the bottom right are untouched.

## My recommendation: A · Shop window

Four reasons, each from his own words:

1. **"More unique and different with the opening sign."** A is the only design where the sign, the time and the money are one object: the café's own painted front. The window, the board on its bracket and the tag hung from the board belong together. That also answers "it just looks like a cluster": the screen shows three parts of one shopfront, not a column of boxes.
2. **"The sun is actually changing."** A has the biggest sky: 316 × 100 px, against B's 474 × 44 and C's 102 px circle, half of which is water. During a rush he can read the hour from the window's colour in the corner of his eye, without reading the time.
3. **"More juicy."** A has a gilt "$" on a green painted tag, like a signwriter's gold leaf, with the bill hard against it.
4. **"A lot more cleaner."** Five pips and a little café front. It is the only goal icon that says "another café" without words: a flag says "finish", a key says "unlock".

**Why not B:** it is the cleanest and the cheapest, and the closest to the prompt line he loves. But it is the least unique, because the sign is today's card. And in a thin ribbon the sun mostly slides sideways, which is the nearest of the three to the "circle moving" he wants gone.

**Why not C:** it has the clearest sunrise and sunset: the sun's road on the water, then the bridge lighting up. But brass and the waterfront are new to a café on a street grid, its sky is the smallest, and the key can read as "locked".

**If A feels too big at top centre**, the mix I'd take is B's ribbon (92 px tall) with A's board sign and green tag.

## Choices I made where the brief left one (his to overrule)

1. **The time piece also shows at night** (the 11 PM frames), in place of the night's clock at the top right. His words: "I'd rather the timing be in the middle, on the top middle."
2. **The sun rises at 8:40 AM and sets at 7:50 PM.** So it is rising at opening, and at closing only its glow is left. Dusk is the sky's colour, not just the sun's place. His words: "sunrise, sunset, dawn, dusk... the sun is actually changing."
3. **"today" under the figure is gone.** The figure is still today's takings, emptied each morning. The brief's words: "A pill with the bills icon... and the number."
4. **"+$12" pops at the pill's left**, not under the figure, because the stars sit under the figure now.
5. **The day name stays at 22 px**, as today. The brief asks for "the day name small", and 22 px is small next to a 32 px time; I didn't go smaller, because of her playtest 7 note: "what day, what time? Oh, this is way too small."
6. **The sign's word keeps today's sizes, and the line under it grows from 20 to 22 px.** Her words: "your text needs to be bolder."
7. **A Day 1 goal toast beside the stars, for 6 s.** This adds to the brief's two moments (a star earned, and the phone). It ties the words to the icon once, on the day the icon is new. Drop it if the Day 1 card is enough.
8. **A and B fly a bill and C flies a coin.** The bill is his icon: "I do like the way that the money icon is showing". C flies a coin to match its brass medallion.
9. **The stack's edges under the bill** (one for every $24) are kept in A and dropped in B and C, whose icon is a single bill.

## How the pictures were made, and how to redraw them

- **The scripts are in `src/`:**
  - `hud.py`: the toolkit (the skin's colours, drawing at 2×, the icons, measuring the words);
  - `sky.py`: the sky table, the sun, moon, stars and clouds, and the three skylines;
  - `variants.py`: the three HUDs and their bench strips;
  - `patch.py`: takes today's HUD out of the photos;
  - `make_all.py`: draws every picture.
- **To redraw everything:** run `cd /root/review23/mockups/src && python3 make_all.py`. It takes about 2.5 min.
  - For only some pictures, add letters and names, e.g. `python3 make_all.py B day sky`.
  - The letters are A, B, C. The names are day, afternoon, last, closing, pay, sky, bench, star, fp.
  - It needs Python 3 with Pillow, numpy, scipy, scikit-learn and OpenCV. The fonts are the repo's Baloo 2 Bold and SemiBold.
- **How they are drawn:** everything is drawn at 2× and reduced, with real text in the game's font. Every word is measured, as described above.
- **How today's HUD was taken out of the photos:**
  - The café camera never moves, and CafeLife 06 (8 PM, with the recap up) shows the top-right corner bare.
  - In the 8 PM photos that bare corner goes straight in, its brightness matched.
  - By day it guides the fill: each hidden pixel takes the day colour of the visible pixel whose night neighbourhood matches best. The two flat roofs take their own day colour from the strip at the screen's edge.
  - The labs' banner at the bottom right (labs only, never in the game) is lifted too. A few small smudges stay in the crosswalk where its words were.
  - At the bench, the desk behind the old strip is cloned from its left.
  - The Now line, the tabs, the prompt line and the controls strip are left in the photos as they are.
- **The other files in `src/`:**
  - `_cache/` holds the patched photos. Delete it to recompute; that takes about 5 minutes more.
  - `_checks/` holds the check crops.
  - `measurements.json` holds every word's size and contrast.
  - `skin.py` is the first run's colour table and is not used.
  - `arrange_*.py` (and `arrange-*.png` in this folder) are designer F's furniture mockups, not part of this work.
- **`_make/` and `_work/` are left as they are.** They hold the first run's drafts, scripts and test crops, which `src/` replaces; nothing imports them, and they are safe to delete.

## The pictures

| Picture (for each of A, B, C) | What it shows |
|---|---|
| `X-cafe-day.png` | 10:22 AM, Day 1, the café overhead, $15 (photo: CafeLife 02-calm) |
| `X-cafe-afternoon.png` | 1:24 PM, busier, $65 (CafeLife 03-build) |
| `X-last-orders.png` | 7:19 PM, golden hour, the honey sign (Pause 20) |
| `X-closing.png` | 8:00 PM, dusk, the plum CLOSED sign (Pause 31) |
| `X-sky-strip.png` | the time and its sky at 9 AM, 11 AM, 2 PM, 6:30 PM, 8 PM and 11 PM (2×; the 11 PM frame is over Grace's street at night) |
| `X-payment.png` | the payment in 3 frames (2×) |
| `X-new-star.png` | the franchise goal: Day 1's goal toast, a star landing on Day 2, then the quiet row (1.5×) |
| `X-bench.png` | the strip at the bench (Bench 07, the cracked screen) |
| `compare-all-three.png` | the three day views on one sheet, each with its top band at true size |
| `first-person-check.png` | the three time pieces in first person, at the counter and at the drinks machine (true size) |

## Questions he must answer to pick

1. **Which one, or which mix?** He can pick per part:
   - the time: window, ribbon or porthole;
   - the sign: painted board, today's card cleaned up, or wooden plaque;
   - the money: green tag, candy pill or brass medallion;
   - the goal icon: café front, pennant or key.

   If he is torn: does "more unique" outrank "clean"? That means A or C against B.
2. **At night:** should the time be top centre too, as drawn, or should the night keep its clock at the top right?
3. **The franchise goal without the line:** the Day 1 card, a 6 s goal toast, the star toast and the phone. Is that enough, or should a small "5 stars to franchise" stay under the row through Day 1?
4. **"today" under the figure:** drop it, as drawn, or keep it?
5. **Her art:** would his girlfriend like to draw the skyline strip (A's Painted Ladies, or C's Bay)? The code draws a stand-in either way.
6. **The tabs:** is it OK that a fourth tab starts a second row, now that the top centre belongs to the time?
