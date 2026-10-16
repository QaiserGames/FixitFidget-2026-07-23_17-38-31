# Art Direction + Environment: the bench's look, and the café full from above (14 Oct 2026)

*Art Direction lead with the Environment & Scene hat (Opus 5.5). Read-only on `~/cafe/repo` (`playtest-2`, `fab65d3`). No Unity, no Mac. Every world coordinate below was read from `Assets/Playtests/AcesCafeLayoutPlaytest.unity` with a script that resolves prefab instances and parents. The feel sheet had not landed when I finished (checked twice), so nothing here is compared against it yet.*

## What he will see

At the bench, Grace's camera lies on a blue silicone repair mat. The mat has a printed grid, rulers along two edges, a row of moulded screw cups, a pale "new part" pad and a deeper well for covers. A small steel tray waits behind the device for old parts. To the left, the tools lie side by side on a low steel rest, with the yellow cloth folded at its end. Pick up the cloth and it opens into a soft wad under the cursor. Drag it and it bunches toward the stroke. The camera has a real leather neck strap: it runs from a ring at each end, curves down onto the mat and gathers in a loop behind the lens, and her husband's scratches are on that loop. On Day 2 the jammed shutter comes out dusty, with a bent blade and a sprung spring, and a bright clean copy of it waits on the pale pad. Screws are small and dark. The hole a screw leaves is a pinprick with a bright lip, not a black disc. Out in the café, from the usual high view, the counters and the back bar look used. Ace's bench has a cluttered desk corner. The workshop end has a row of tagged, bagged repairs waiting for pickup under the drawer wall, with a black-framed whiteboard above it. There's a parts box on the counter end, a colourful syrup rack by the dispenser, cups and lids behind the till, and mugs on a bar mat. Up close: a crate of donor parts under Ace's bench, post-its, tape and a ruler. A mortgage letter sits under the till, and a FINAL NOTICE is taped inside the back door.

---

## 1. What I did

- Read the common brief, my brief, both role files, the Bible (all), the art style guide, hero spec, reference sheet, café research, realism, furnishing and furniture passes, the 10 and 13 Oct notes and the First Hour report. Also read UI/UX's, NPC's and Cinematics' specs as they landed, plus the scripts and scene the brief lists.
- Measured both cameras from the scene. Projected every café area into the overhead home view. Checked a silicone mat layout numerically against each device's close-up and UI/UX's reserved screen zones.
- Researched real silicone repair mats (text sources). Image hosts were blocked by the proxy, so the camera and strap looks lean on the reference sheet's [seen] entries.
- Wrote briefs A1–A9 (bench), B1–B4 (café) and one ordered list for the Blender lead (§9). Nothing was built.

## 2. The decisions, why, and what lost

| Decision | Why | Rejected |
|---|---|---|
| **Mat `#4F70A0`, smoothness 0.28** | Today's `BM_Mat` (`.31/.42/.54` ≈ `#4F6B8A`, `Playtest3Session4Steps.cs:287`) is already blue-grey. Under the bench lamp's `#FFE4C2` it renders ≈ `#4F5F68`: grey-teal, b* −7. `#4F70A0` renders ≈ `#4F6379` (L* 41, b* −14), still slate blue. At L* 41 it sits about 20 L* or more from every device's darks (leatherette 16, phone black 11, phone glass 22, tool grips 16) and lights (chrome 74, gold 66, dial 93, sage back 79, cloth 77). | A saturated repair blue `#2F6FB5`: the toy look, and it glows. A dark slate `#3F4E63` (L* 33, ≈ 29 lit): the camera's black body sits within about 15 L* of it. Keeping today's colour: it goes grey under the lamp. |
| **Compartments moulded on the device's right; tools on a separate rest on its left; old parts in a separate steel tray behind the device** | Continues today's habit (tray right, caddy left: `Playtest3Session4Steps.cs:117,136`). Pieces go one way: screws and covers right ("coming back"), broken parts away from the player into steel ("gone"). The rest and the tray are separate, so their positions can follow each device. | A ring of compartments around the device (tested: the frame grows). The mirror image (tested: +5 % device size for the phone only; it breaks the habit). Tool slots moulded in the mat (a fixed position can't follow a 50 mm watch). A moulded old-parts bin (same colour as the mat, so it doesn't say "out"). |
| **Pale for "parts go here" (cups, new-part pad); dark for the cover well; steel for scrap** | A value grammar the player learns without words. Dark screws and new parts read on pale. Pale covers (the sage phone back, the gold watch back) read on dark. Dark broken parts read on steel. | One colour for everything (nothing reads in the cups). Words printed on the mat beyond the ruler's numbers and the cups' 1–6. |
| **Tools lie on the rest, handles toward the player** | At 58° a lying tool shows its whole silhouette: the driver's three diameters, the tweezers' V, the brush's fan. It hides nothing and is a long, easy target. | Standing tools (today's caddy): their silhouettes overlap at 58° and they stand over the work. A brush stood on its bristles is wrong in real life. |
| **Held cloth = a gathered wad, about 45 × 55 mm** | It "opens up" from the fold and reads as cloth held in a fist. It covers a spot, not the device. | A fully open 15 × 18 cm sheet: it would cover the whole camera. |
| **A neck strap: two lugs, split rings, keepers, a loop behind the lens half** | It is what a rangefinder strap is. Grace says "my husband carried it everywhere" (`GraceCameraEpisode.cs:45`), and that is a neck strap. It ends the "rectangle": two flat 83 × 17 mm bands and a 140 × 18 mm loop sticking straight out of the lens end (`hero_camera.py:422-447`). | A wrist strap (one lug): wrong for her story. Keeping the bands and bending them: still a handle. |
| **The hole is a decal: a dark bore half the head's width, a bright lip, a faint dip and witness ring** | Reads as "a threaded hole" on any surface, at any size, from one texture. Today it is a dark disc the head's full width with a near-black core (`Screw.cs:341-361`): on the phone, an 8 mm pit. | Geometry per hole (needs each surface's colour). A smaller dark disc (still a pit). |

**One debate for the Technical Director (TD).** "The device fills about 60 % of the frame" and "the compartments in view" don't fit together at real proportion, once the frame also has to show the tool handles, the new-part spot and the cover well, and stay clear of UI/UX's zones U1–U5. With my layout, the best framings I found put the phone at about 38 % of the frame height, the camera at 38 % (its strap loop in view) and the watch at 24 %. That is 3.1–4.0 px per mm at 1080p. The cause is real proportion itself: a 134 mm screwdriver next to a 50–120 mm device. My recommendation: the default close-up shows the working layout, and the existing zoom (`ItemInspector`) reaches about 2× for screw work (device 75–85 % of the height, 7–9 px/mm). If the TD frames tighter anyway, the tool rest and the compartments go to the frame's edge before anything else does, never off it.

---

## 3. Part A: the bench's look (briefs for the Blender lead)

### A0. The numbers everything is built against
- **Close-up today:** `CM_InspectCam`, FOV 40°, pitched 58°, 0.58 m from `InspectPoint` (−5.034, 1.006, 13.998) (`Playtest3Session4Steps.cs:42`; scene). On the mat that frame is 61 cm wide 20 cm toward the player, 75 cm wide at the point and 97 cm wide 32 cm beyond it. Today's 7 cm phone is 9 % of that width, which is why its screws were drawn 8 mm wide (`hero_phone.py:50`).
- **The rig is turned 180°** (the bench faces the room). All mat coordinates below are in the `InspectRig`'s local frame: mm, origin at the mat's centre, +x the player's right (world −X), +y away from the player (world −Z).
- **UI/UX's zones at the bench** (`uiux-checklist-v2.md` §5.8, "the stage contract"; 1080p px from the top left): U1 x 24–1408 y 20–64 (the tabs), U2 x 1596–1896 y 18–110 (takings), U3 x 1416–1896 y 132–300 (the card), U4 x 24–256 y 864–1060 (the sign, bar and time), U5 x 1496–1896 y 740–1054 (the controls). The layout below was checked to stay out of all five.
- **Scale:** everything is authored at real size. If the TD's method scales the bench rig per device, every piece here scales with it as one group (`BenchStage` reads the positions as data).

### A1. The silicone repair mat (`BM_RepairMat`)
**Reference (no brand).** The common electronics-repair silicone mat is 45 × 30 cm (17.7 × 11.8 in), about 5 mm thick and usually blue. It is heat-resistant to 500 °C, with a 0–36 cm scale ruler and an anti-slip base. Its moulded features: 124 tiny screw cells, 42 spare cells, three lidded parts boxes, magnetic parts areas and a magnetic tool area, an 11-screwdriver slot row (proto-pic.co.uk; littlebirdelectronics.com.au). Larger 55–60 cm mats exist (unverified). **What we take:** the grid, the rulers, moulded cups, a well, a marked magnetic pad, the colour and the thickness. **What we cut:** the 124 tiny cells and the printed code charts (noise at 4 px/mm, and the "lines where words should be" verdict), the lids, and the tool slots (the rest does that job).

**Size:** 600 × 360 × 5 mm, today's footprint (`Playtest3Session4Steps.cs:103`), on the chestnut bench top (1.005 m). Plan corners r 12 mm, one 1.5 mm chamfer.

**Layout (mat frame, mm):**

| Feature | x | y | Build |
|---|---|---|---|
| Ruler, near edge | −290 … +290 | −180 … −166 | Cream ticks: 5 mm minor (2.5 long), 10 mm major (4.5 long), cm numbers 3 mm tall every 10 mm, 0 at the left |
| Ruler, left edge | −300 … −286 | −166 … +170 | Same, 0 at the near end |
| Grid | the rest of the top | | 10 mm squares, 0.5 mm lines; every 50 mm a 0.9 mm line (texture) |
| Screw cups × 6 | centres +36, +60 | centres −24, 0, +24 | Round, inner Ø 18, 3.5 deep, rim r 1. Walls `BM_Recess`, floor `BM_Pad`. A cream "1"–"6" (3 mm) left of each cup, numbered near to far, left column first |
| New-part pad (the "magnetic pad") | +90 … +170 | −82 … +84 | A pale inset 0.3 mm proud, 0.5 mm chamfer. Four cream L corner marks (12 mm legs, 1.5 mm wide, 0.3 proud) 6 mm in from its corners |
| Cover well | +182 … +262 | −82 … +84 | A recess 3 mm deep, corners r 8, a 0.8 mm raised lip round it, floor `BM_Recess` |

**Empties in the FBX** (the positions Systems reads; names are a contract): `Cup_1` … `Cup_6` (cup centres, on the floor), `Pad_Small` (+116, −58: where a small new part waits, the pad's near-left), `Pad_Large` (+130, 0: the phone's new screen, long axis along y), `Well_Large` (+222, 0), `Well_Small` (+206, −58: the watch's back plate). Every size fits its largest piece: the phone's cover and screen are 71.6 × 147.6 mm (reference sheet §6), and each bay is 80 × 166.

**Per device** (data, Systems decides where it lives; footprints include the strap and bow):

| | Rest centre | Footprint (mm) | Tool rest x (mat) | Scrap tray centre |
|---|---|---|---|---|
| Phone (long axis along y) | (−30, −14) | 72 × 148 | −190 … −80 | (−30, +133) |
| Camera (lens to the left) | (−82, 0) | body 120 × 80; the strap adds 28 at each end and the loop behind the lens half (A6) | −294 … −184 | (−5, +115) |
| Watch | (−20, 0) | 50 × 65 | −170 … −60 | (−20, +107) |

Every device's footprint ends 18 mm left of the cups. The tool rest stands 14 mm left of the footprint. The scrap tray stands ≥ 28 mm beyond the device's far edge. The camera's tray needs 30 mm, because its lens (28 mm tall) hides about 17 mm of mat behind it from 58°. The camera's tray is offset right, clear of the strap's loop.

**Colour and materials** (metallic 0 throughout; "the cool accent" is his call of 14 Oct, flagged in §8):

| Material | sRGB | Smoothness | Where |
|---|---|---|---|
| `BM_Silicone` | `#4F70A0`, with `BM_Grid.png` | 0.28 | The mat's body. The grid is a 512² tile = 50 mm (10 px/mm): minor lines `#6684B0`, major lines `#7A95BC` |
| `BM_Recess` | `#3D5680` | 0.22 | Cup walls, the well's floor and walls |
| `BM_Pad` | `#B4C3D6` | 0.30 | The pad and the cup floors |
| `BM_Print` | `#EDE4D3` on `#4F70A0`, `BM_Rulers.png` 4096 × 256 (7 px/mm) | 0.28 | The two ruler strips; the cup numbers and pad corners can be geometry in `#F2EFE8` |

The grid tiles at world scale, per the house rule for long pieces. The grid stays crisp from the default framing (3–4 px/mm on screen) to a 2× zoom (8–9 px/mm).

**Why it reads from the close-up at 58°.** Lit, the mat sits at L* ≈ 41. That is ≥ 20 L* from every dark and light on every device. The one exception is Grace's tan strap (L* 36), which reads by opposite hue (b* +28 against −14) and by its dark edges and cream stitching (A6). The mint glow (`#7FD3A8`, L* ≈ 79) reads on it at ΔL* ≈ 38, which answers UI/UX's question.

**Done looks like:** in the Bench lab's close-up photo, a patch of mat beside the device samples L* 38–46 with b* ≤ −12. Dark screws read in the pale cups. The grid lines are 1–3 px wide, without shimmer.

### A2. The tool rest (`BT_ToolRest`, replaces the "Tool caddy" block)
- **Reference:** bench-top tool rests in bent sheet steel. The tools lie on a notched bar, as on a pen rest (real life first: a soft brush is never stood on its bristles).
- **Build.** A base plate 110 × 230 × 2 mm in warm steel (`DC_Steel` `#93908A`, satin 0.5), corners r 6.
  - A near bar 20 mm from the near edge, 16 mm tall, with four U notches 18 mm wide.
  - A far bar 120 mm from the near edge, 9 mm tall, with four V notches.
  - A cloth tray from 150 to 230 mm: 100 × 80 × 6 mm, lined with cork (`BT_Cork` `#A57B4F`, 0.15).
  - About 600 tris.
- **How each tool sits.** The tools lie at about 5°, handle end higher, tips pointing away from the player, 25 mm apart. Left to right: screwdriver, pry tool (blade flat side up), tweezers (flat, the V showing), brush (wide face up, the ferrule in the far notch, bristles over the cloth tray's lip). The cloth lies folded in its tray, its free corner lifted toward the player.
- **The order is the work's order.** Day 1's tools sit where the eye lands first: the brush lies nearest the device, and the cloth sits at the rest's far end, in the widest part of the frame. (UI/UX glows the cloth first, then the brush.) If Systems keeps LB/RB, the cycle runs in the rest's order.
- **The tool's origin stays its working tip** (`hero_lib.py:19-21`). Only the rest poses change, in `BenchClothSteps` (Systems).
- **Read:** warm steel (L* 60) on the mat (41); the black grips (16) and gold bristles on the steel.

### A3. The scrap tray (`BT_ScrapTray`, the old-parts bin)
- A separate shallow stainless instrument tray: 160 × 90 × 22 mm, corners r 10, a rolled rim r 2, satin `#B9B5AD` (0.5). About 300 tris.
- It stands on the mat's far band, behind the device (the table in A1).
- It holds the phone's cracked screen lying flat, the camera's old mechanism and the watch's broken mainspring.
- Why separate and steel: a different material and height say "out of the job". Dark broken parts (cracked glass, the dull mainspring at L* 24, the tarnished mechanism) read on L* 74.

### A4. The cloth in three states (all `BT_Cloth*`; 15 × 18 cm suede microfibre, `#D9BD5C`)
References: the reference sheet §5 (fold geometry; lens cloths 15 × 18 cm). Today's model is terry-thick at 2.1 mm a layer (`bench_tools.py:205`); a lens cloth is about 0.5–0.8 mm.
1. **Folded (rest), `BT_Cloth`, the same file name.** A quarter fold, 75 × 90 mm, at 0.8 mm a layer: about 5 mm with the puff, two rounded fold edges, two stepped open edges and no triangle (§5). Its free corner lifts 6 mm toward the player: the grab point. Smooth shading (the hero rule for soft things). It needs one change: thinner. Cut it to about 2,500 tris (4,358 today).
2. **Held, `BT_ClothHeld`.** The cloth folded once and gathered in an unseen fist. It has a working pad on the surface, about 40 × 45 mm, with soft edges and 5–6 radial folds. The gathered excess rises behind it into a twisted tail about 30 mm high, leaning back toward the player where the hand is. From the camera: a rounded yellow wad about 45 × 55 mm.
3. **Pressed and bunched (shape keys on `BT_ClothHeld`; one topology; about 1,200 tris; a fine weave normal map 256² is optional).**
   - Key `Press`: the pad flattens and spreads to 50 × 52 mm, and the tail drops 5 mm.
   - Key `Bunch`: the leading edge (+x) rolls up into 2–3 compressed folds 4 mm higher, the trailing edge stretches flat and drags 8–10 mm behind, and the tail leans 12° against the stroke.
   - Systems turns the cloth so +x points along the stroke, and sets `Bunch` from the stroke's speed and `Press` from the press.
   - Folded ↔ held is a swap, hidden by the pick-up motion. An optional half-open in-between (`BT_ClothHalf`, folded once, lifting off the tray) smooths it.
   - If shape keys can't be used, three meshes (held, pressed, bunched) swapped are the fallback: cheaper, poppier.
4. **Read:** yellow (L* 77) on the mat (41) and on cork (≈ 55).

### A5. The brush's bend
Add a shape key `Bend` to `BT_Brush`: the clumps bend 25° toward −x from the ferrule's mouth, and the tips splay 2 mm. Systems turns it into the stroke. No new triangles. This is Systems' "bristles bending" (`systems` brief §4).

### A6. Grace's camera: the strap, and the body's outline
**The strap**: real rangefinder hardware, reference sheet §8.
- **Lugs:** two eyelets, one on each END face at the top plate's band height, centred in the body's depth. Today there are three, two of them at the lens end feeding the "handle" (`hero_camera.py:247-250`). Each: a chrome boss 6 × 8 mm (real) with a Ø 3.5 hole.
- **Split rings:** Ø 12 mm, 1.2 mm wire, satin chrome, hanging from the eyelets. Each has a teardrop leather protector, 15 × 12 × 1.5 mm, `#4A2C18`, between the ring and the body.
- **Strap:** tan leather 14 mm wide and 2.5 mm thick, with burnished dark edges (`HD_CamStrapEdge` `#4A2C18`) and cream stitching 1.5 mm in from each edge (`HD_CamStitch`). At each ring the strap passes through, folds back 40 mm and is held by a leather keeper (18 × 8 × 1 mm) 25 mm from the ring.
- **The drape:** from each ring the strap rises a little over the ring, then falls in a soft S, with a bend radius of about 15 mm, to touch the mat about 25 mm out from the body's end. Both runs curve away from the player. The opening end's run passes behind the body's far edge. They meet in a loosely gathered loop, two turns of strap, about 110 × 55 mm. It lies behind the lens half, 30–85 mm beyond the body's far edge, clear of the lens's shadow and of the scrap tray (A1). It rolls ±20° along its length, as leather does.
- **Names and code:** `Upper strap` = the lens end's run, `Lower strap` = the opening end's run, `Worn loop` = the loop. Devices 2 maps meshes by those names. `Old scratch 0–2` move onto the loop's flattest top face (a `moves` entry). The parent keeps its name, `KEEP - scratched sentimental strap` (`BenchLab.cs:922` checks it). The strap has no collider and is never a hover target.
- **The flip:** a flip or peek lifts the loop with the body. It's cosmetic and rare: the camera has no work on its back.
- **Budget:** about 3,000 tris (the sweeps about 1,600, the stitches about 1,000, the hardware about 400).

**The body's outline needs work**, at whatever scale the TD's method lands:

| | Model today (real equivalent of 2×) | Canonet QL17 GIII / Olympus 35 SP | Verdict |
|---|---|---|---|
| Width | 120 mm | 120 / ≈ 129 | ✓ |
| Height | 80 mm | 75 / ≈ 76 | Keep (+6 %) |
| Body depth without the lens | **23 mm** (`hero_camera.py:21,68`) | ≈ 32 (sheet §8), 38 (hero spec §6) | **Deepen to 32**. From 58° the top plate's controls side faces the player, and at 23 mm it is a strip |
| Top plate's step | chrome band 1 mm (0.5 real) proud of the leatherette (`:191-194`) | 0.5 mm forward step [seen] | ✓ |
| Rounded ends | rounds the *front-view* corners r 6 real; the vertical edges get a 0.6 mm chamfer (`block_loop`, `:122-149`) | the **vertical edges** r ≈ 6 [seen]; crisp plate corners | **Swap them**: the ends' edges r 6–7 real (the rounded ends from 58°), the front-view corners r ≈ 2.5 |

Deepening the body needs a `moves` set for the lens, film path, mechanism, blades, grime and straps (hero spec §6). It belongs in the same export as the strap.

### A7. The clean shutter mechanism (the new part) and the old one's tells
- **`Clean shutter mechanism` (new, the fresh visual).** The same silhouette, origin and bounds as `mechanism()` (`hero_camera.py:368-388`), with both blades flat on top.
  - Chassis satin black `#1F1D1B` at 0.5.
  - Gears bright new brass `#D4AF5A` (L* ≈ 73).
  - The drum rim bright chrome `#CFCBC2`; the steel bright `#C9C5BC`.
  - The spring seated in its groove; the three chassis screws bright.
  - About 2,000 tris.
- **`Jammed shutter mechanism` (the broken visual) keeps its shape and gains its tells** (job 2: it shows what's wrong):
  - Brass tarnished `#8A6A32` (L* ≈ 47); steel dull `#77746E`.
  - The bent blade kinked up (it exists, `:413-416`).
  - The spring's end sprung out of its seat, 4 mm proud (real).
  - Optionally a grey dust overlay on the top faces.
- Side by side, the two read as "old one out, new one in": the same shape, about 25 L* apart in the metals.
- **Wiring is Systems'/the TD's.** Today `ReplaceablePart` swaps `Bent shutter blade` (broken) for `Working shutter blade` (fresh) on `Jammed shutter mechanism` (the prefab, `seatDistance 0.016`). Proposed: `brokenVisual` becomes the jammed assembly, `freshVisual` becomes `Clean shutter mechanism`, with the names above.
- **Read on the pad:** the black chassis on pale (L* 78); the bright brass reads on the black.

### A8. Screws and holes in proportion
- **Sizes are the TD's.** Today: the phone 8 × 1.6 mm heads, the watch 12 × 2.6 (`hero_phone.py:50`, `hero_watch.py:27`).
- **Look:** a pan head (head height 0.35 × head Ø), one chamfer, 12 sides. A Phillips recess: two slots 0.55 × head Ø long and 0.12 wide, `HD_ScrewSlot` `#2A2D31`. The shank Ø 0.55 × head Ø, with 3 ring grooves for thread, length per device.
- **Colour by device, for contrast:**
  - Phone: black-oxide `#2B2724` (0.45). Steel would vanish on the sage back: L* 74 against 79.
  - Watch: blued steel `#2A3F6B` (0.55). Real watch screws are blued, and it reads on the gold.
  - Camera: no removable screws.
  - Both read in the pale cups (L* 78).
- **The hole** (`BT_ScrewHole.png`, 128² RGBA; a quad 1.15 × head Ø, laid 0.05 mm proud, replacing the two cylinders in `ScrewSocket.Make`). Radii as fractions of the head's radius:
  - 0–0.48: the bore, `#1A1816`, with two faint thread rings `#3A3632` at 0.30 and 0.40.
  - 0.48–0.55: the lip, `#D8D2C6` at 0.8 alpha (the thread's entry chamfer catching the lamp).
  - 0.55–0.90: the dip, darkening from 0 to 0.35 alpha black toward the centre.
  - 0.90–1.00: the witness ring, white at 0.12 (the cleaner metal under the head).
  - 1.00–1.15: transparent.
  - The dark area is about 25 % of the head's area. Today it is the head's full width plus a 45 % near-black core.
- **Readability threshold (for the TD):** a head reads as a screw (cross visible) at ≥ 12 px and as a bright point at 6–8 px. At my default framing (3.1–4.0 px/mm) a 2.4 mm head is 7–10 px. At a 2× zoom it is 15–20 px.

### A9. Tool icons (UI/UX §5.3 is the contract)
Agreed as written there:
- **The set:** the 8 generic icons and the 5 per device. `part_camera` = A7's clean mechanism.
- **The render:** `Tools/Blender/render_bench_icons.py`. Orthographic, 256², transparent, flat-shaded in the models' own colours, one warm key light from the upper left, no ground shadow, an 8 px `#1F1B17` outline at 256 px. Long tools lie diagonally, tip to the lower left, filling 80 % of the diagonal.
- **Additions:**
  - The cloth icon uses the rest pose (A4.1) from 35° above, so the lifted corner shows.
  - The screw icon uses the phone's black-oxide screw at a three-quarter view, with the cross readable at 36 px. Test it at 36 px on `#F7F3EE`: if the cross clogs, open the recess to 0.15 × head Ø in the icon render only.
  - `newpart` shows a part sitting on a pale pad corner, so the icon matches the world.
- **Review:** one contact sheet at 36 and 56 px.

---

## 4. Part B: the café full from the overhead camera

### B1. What the overhead actually shows
- **The camera:** `CmShopCam` at (0, 23.79, −15.87), 43° down, yaw 0 (looking north), FOV 44°, 34 m from the focus (0, 0.6, 9). The scene sets it, and `CafeViewMode` copies it at start (`CafeViewMode.cs:241-246`, `:553`). The player can zoom 24–48 m (`:38-39`) and tilt 38–68° by day (`:189-190`).
- **The role files' "50° from the south, ~20 m" is stale** (§8).
- **The room on screen:** the whole café, 15 × 18 m, fills x 600–1320 and y 263–856 at 1080p, about 37 % × 55 % of the screen.
- **Scale:** **33 px/m at the back bar, 36 at the counter, 42 mid-room, 48 at the front windows** (×1.33 at 1440p).

| Area | Screen (1080p) | px/m |
|---|---|---|
| Repair mat / Ace at the bench | (780, 407) / (813, 387) | 36 |
| Repair-bar stools | (807, 440) | 36 |
| Till / intake shelf | (964, 406) / (916, 415) | 36 |
| Back bar, workshop end / middle | (773, 332) / (918, 336) | 33 |
| Parts wall top / menu board | (771, 308) / (960, 284) | 34 |
| Dispenser / grinder | (1063, 338) / (1102, 336) | 33 |
| Counter's west end | (701, 410) | 36 |
| Communal table / banquette | (836, 616) / (672, 605) | 42 / 41 |
| Notice board (faces west: edge-on) | (1294, 691) | 45 |

**The 10-pixel rule:** a thing reads as a shape at ≥ 10–12 px in its smaller visible size. That is ≥ 0.33 m at the back bar and ≥ 0.25 m at the front. Under about 8 px only colour reads. So small props count only in clusters: a shared base ≥ 35 × 25 cm, ≥ 20 L* from what's under it, or one saturated colour unlike its surroundings. Upright faces toward the camera (the back wall) read at cos 43° = 0.73; tops at sin 43° = 0.68.

**What the overhead can't see:** the floor for 1.35–1.45 m behind the front counter and the bench (by sight line from the camera: z 14.24 → 15.69 behind the counter, 14.45 → 15.79 behind the bench). That includes Ace's feet at the bench and everything under it. The back bar's top and wall are fully visible.

### B2. Today's lived-in props from above (`cafe_livedin.py`; positions from the scene)

| Prop | Size | Overhead | Verdict |
|---|---|---|---|
| `LI_Rug_Corner` (−6.0, 1.4) | 1.60 × 1.10 | 75 × 51 px, oat on oak | **Reads.** Keep |
| `LI_BinStation` (2.79, 0.36) | 0.96 × 0.43 × 0.85 | Its three coloured tops, 46 × 14 px, above the 0.78 m sill | **Reads** (colour). Keep |
| `LI_StreetBin` (5.10, −0.60) | Ø 0.68 × 1.0 | A dark disc, about 30 px | **Reads.** Keep |
| `LI_Coat_OnChair` × 2 | about 0.5 × 0.3 | A dark slate blob, about 20 × 12 px | **Reads weakly.** Keep |
| `LI_CondimentStation` (2.78, 13.5) | 0.60 × 0.30 | 22 × 11 px, busy but low contrast | Weak. Keep (first person) |
| `LI_Laptop_Open/Closed` | 0.32 × 0.22 | 13 × 9 px steel on chestnut | Weak. The person at it does the work |
| `LI_ToteBag` | 0.38 × 0.15 | 18 × 7 px, often under the table | Weak. Keep |
| `LI_TillMat` (−0.26, 14.57) | 0.91 × 0.61 | **Hidden** behind the counter | First person only. Keep |
| `LI_NapkinDispenser` × 8 | 0.165 × 0.10 | 5 × 4 px | Invisible. Keep (first person) |
| `LI_LeftCup` × 3 | Ø 0.09 | 3–4 px | Invisible. Keep |
| `LI_KidsDrawing` (on the notice board) | 0.28 × 0.22 | Edge-on (the board faces west) | Invisible. Keep (the waiting nook) |

**The fix is not to enlarge these.** They are right for first person, and enlarging a napkin dispenser makes a toy. Instead, dress the surfaces the overhead does see (the bench top, the back bar's top and wall, the counter's west end) with clusters that pass the 10-pixel rule (B3).

People are the biggest lever: a body is about 18 × 60 px. For NPC: a settled guest's table read is strongest as a big light flat thing, like an open newspaper (0.6 × 0.4, about 20 × 13 px) lying on the table. That would be a table prop with no hold. NPC ruled out a book in hand ("a prop plus a hold": `npc-stools-guests-looks.md`, its "Reading" bullet in §2 and its flags). Offered, not built here.

### B3. The prop list (each piece: purpose by the purpose rule's four jobs (1 worked, 2 tells how it works, 3 silhouette, 4 this world's texture), reference, size, place, overhead read)

**Repair side**

| # | Prop | Purpose | Real reference, size | Where (named object → world, m) | Overhead | Tris |
|---|---|---|---|---|---|---|
| R1 | **"Fixed, waiting for pickup" row**: 7 finished non-job items, each bagged or wrapped, each with a manila tag on a string (names handwritten, by Content): a muted cherry-red table radio (0.30 × 0.14 × 0.18), a desk fan in a kraft bag, headphones in a paper bag, a game controller in a zip bag, a kettle, a desk lamp wrapped in paper lying down, a small speaker | 2 (people come back for fixed things), 4 | Repair-shop pickup shelves: tagged and bagged | On `CK_BackBar`'s top in front of `CK_PartsWall`: x −7.20 … −4.10, z 17.10–17.40, y 1.00. All ≤ 0.25 m tall, so the lower drawer row shows | A 3.1 m band of varied shapes under the drawer grid, at about (700–795, 325–335). Pale tags are specks; the red radio is the one accent | ≈ 3,500 |
| R2 | **Ace's desk corner**: a small-parts cabinet (24 clear-front drawers, 0.28 × 0.14 × 0.22) at the east end; a spool rack (red, black and green wire and a silver solder spool, Ø 0.08); a multimeter in a yellow holster with coiled red and black leads (0.18 × 0.09 × 0.05); a repair-ticket pad and pen; a yellow post-it pad (`#F5D76E`) plus two post-its on the glass screen's Ace side at 1.25 m; masking tape (Ø 0.075, cream); a 30 cm steel rule along Ace's edge; Ace's own green enamel mug (the brand, Ace's thing) | 2, 4 (his list: post-its, tape, ruler, paper, a coffee cup) | Electronics benches | On `CK_RepairBench`'s top, east of the stage: x −4.40 … −3.15, z 13.78–14.38, y 1.005. Out of every close-up frame (today's widest frame ends near x −4.54; the new framings are tighter). Nothing taller than the glass's 1.395 m top. Clear of the screen posts at x −4.30 and −3.47 | A 1.25 × 0.6 m speckled patch next to the mat at about (800–850, 403–412). Yellow and green accents | ≈ 2,800 |
| R3 | **Under-bench donor parts** (his "bunch of parts in a bin under a desk near Ace"): a grey tote (0.40 × 0.30 × 0.22) heaped with two old phones face down, a remote, a router, a tangle of black and white cable and a keyboard corner; beside it two stacked open-front bins, blue `#2C67A3` and red `#A33A2E`, 0.105 × 0.28 × 0.127 each, with small parts | 2, 4 | The parts pile every repair shop keeps | In `CK_RepairBench`'s knee space at its east end, on the floor against the modesty panel: the tote at (−3.55, 0, 13.88), the bins at (−3.20, 0, 13.88), in the knee space (x −5.34 … −3.06, `cafe_bar.py:509`). Ace works at the west end (x −5.03) | **Hidden from the overhead** by sight line. Reads in the walking first-person view and the intro's bench shot | ≈ 1,800 |
| R4 | **Whiteboard "this week"**: 0.90 × 0.60, black aluminium frame `#2B2724`, static hand-written notes (by Content), 3 coloured magnets, a yellow and a pink post-it, a clipped invoice. No live game data (§8) | 2, 4 | Repair-shop job boards | On the back wall over `CK_PartsWall`: centre x −5.60, bottom 1.92, top 2.52, z 17.99 | Faces the camera: a black-framed rectangle about 30 × 20 px at (757–787, 283–302) on the white wall | ≈ 150 + 1024 × 683 texture |
| R5 | **The parts delivery**: a half-open shipping box (0.40 × 0.30 × 0.25, kraft, brown packing tape, a packing slip, bubble wrap showing) and two small parts boxes beside it | 2 (parts come in by post), 4 (his "tape") | A parts order | On `CK_FrontCounter`'s west end top, Ace's half: centre (−6.60, 1.095, 13.95). The monstera stands in front at (−6.8, 12.6) | Kraft (L* ≈ 58) on pale stone (≈ 88), about 14 × 10 px with height, at about (720, 405) | ≈ 600 |
| — | Small-parts drawers, magnifier lamp | — | Already built: `CK_PartsWall` (72 labelled drawers), `CK_BenchLamp` | — | The drawer grid reads (120 × 25 px) | Reuse |

**Café side**

| # | Prop | Purpose | Reference, size | Where | Overhead | Tris |
|---|---|---|---|---|---|---|
| C1 | **Syrup rack**: a black wire rack, six 0.075 Ø × 0.30 bottles with white and black pumps (+0.12), in caramel `#B4782E`, vanilla `#E3CF9A`, hazelnut `#7A4A28`, chocolate `#3E2A1E`, raspberry `#8E2A3A` and sugar-free `#D9D3C4`. Labels from a fictional maker, by Content | 2 (how lattes are made), his list | Coffee-bar syrup racks, 0.50 × 0.10 × 0.45 | On `CK_BackBar`'s top at the dispenser bay's west end, against the tile: x +1.65 … +2.15, z 17.85. **Switch off** the three Synty `16 - POLYGON cafe details/Drinks/Drinks - Bottle_01..03` (x 1.74–2.06) there, recorded | Warm colours on the cream tile, about 17 × 15 px at (1050, 320). Also in the drink-station camera's frame beside the dispenser | ≈ 1,400 |
| C2 | **Cup and lid station**: a black 3-tube to-go cup dispenser (ivory `LI_CupPaper` cups, 3 sizes), a black 3-bay lid organiser, a stack of kraft sleeves printed in Ace's green (the style guide: "the green lives on … cups"), a steel milk pitcher | 2, his list (to-go cups, lids, sleeves, milk jug) | Behind-the-till cup stations, 0.70 × 0.30 × 0.55 | On `CK_BackBar`'s top behind the till, against the tile: x +0.35 … +1.05, z 17.80 | Black frames around ivory: about 23 × 18 px at (985, 333). Ivory alone would vanish against the cream tile (L* 88 vs 91) | ≈ 1,500 |
| C3 | **Bar mat and mugs**: a black ribbed rubber bar mat 0.60 × 0.30 with 10 cream mugs upside down (the existing mug mesh) and 2 saucer stacks | 2, his "coffee cups" | Back-bar glass mats | On `CK_BackBar`'s top under `CK_MenuBoard`: x −0.80 … −0.20, z 17.75 | Light dots on dark, about 20 × 10 px at (945, 336) | ≈ 1,200 (reuse the mug) |
| — | Bags of beans, milk jug, napkins, lids, sleeves | — | Already built: 8 `CK_BeanBag` on the shelf (a kraft band about 30 px wide); `LI_CondimentStation` (milk jug, napkins, lids, sleeves) | — | — | Reuse |

**Story props for Ace** (job 4, private by design: none should read from above)

| # | Prop | Where he'd see them | Build |
|---|---|---|---|
| S1 | **The mortgage statement**: opened, flattened, the overdue line circled in red pen. A fictional lender (Content writes the words; brands fictional) | Tucked under `'Register v4'`'s corner on Ace's side, (+0.30, 1.10, 14.15). He sees it at every sale. It shows in `CmCounterCam`'s view over the till and in the walking first-person view | Reuse the house kit's letter mesh, with a new 1024² texture |
| S2 | **The FINAL NOTICE**: the letter taped by its top corners with masking tape, its envelope with a red rubber-stamped "FINAL NOTICE" pinned beside it | Inside the back door at 1.50 m, near (6.63, 1.50, 17.94). He passes it every night on his way out to the man at the bins. Make it a child of the door leaf if the door swings (unverified), and never rename the door: Night 0 finds it by name | Reuse the house kit's letter and post meshes, with a new texture |

### B4. Budgets and the build split
- **Triangles:**
  - Bench new: about 15,000 (mat 2,400, rest 600, tray 300, folded cloth 2,500, held cloth 1,200, strap 3,000, clean mechanism 2,000, screws 150 each).
  - Café new: about 13,000 (repair side 8,850, café side 4,100, story props 100).
  - Both are trivial next to the café's 64k (10 Oct). The house limits hold: standard props < 1,000, small < 300.
- **Materials:**
  - Reuse by name: `CC_Wood_Counter`, `DC_Steel`, `DC_SteelDark`, `CF_Black`, `CF_Kraft`, `DC_Chalk`, `LI_CupPaper`, `T2_Brass`, `DC_Ceramic`, `LI_RecycleBlue`.
  - New: `BM_Silicone`, `BM_Recess`, `BM_Pad`, `BM_Print`, `BT_Cork`, `HD_CamStrapEdge`, `HD_CamBrassOld`, `HD_CamSteelOld`, `HD_ScrewBlack`, `HD_ScrewBlued`, `CF_Manila` (`#E8D9A8`, handwriting atlas), `CF_Whiteboard` (texture).
  - Metallic 0 everywhere.
- **Reused, not built:** `CK_PartsWall`, `CK_BenchLamp`, `CK_BeanBag`, `LI_CondimentStation`, the mug, and the house kit's letter and post meshes.
- **Placed by Environment in Unity C:** one recorded, undoable step (Bible §6). Guards before and after. No floor piece outside the bench's footprint, so walkways are untouched.

---

## 5. Decisions his to overrule
1. The mat's colour, `#4F70A0` (today's `#4F6B8A` goes grey-teal under the lamp).
2. The mat's size, 60 × 36 cm (today's footprint; the common real one is 45 × 30).
3. The layout: tools left, device left of centre, cups → new-part pad → cover well to the right, the scrap tray behind the device. (The mirror image gives the phone +5 % size.)
4. Pale cups and pad, a dark well, a steel scrap tray.
5. The old-parts bin is a separate steel tray, not moulded.
6. Tools lie on a low steel rest, handles toward you. Order: driver, pry tool, tweezers, brush. The cloth in a cork tray at the rest's far end.
7. The phone's screws black-oxide, the watch's blued.
8. The hole as a decal (bore, lip, dip, witness ring).
9. The cloth: a thinner suede lens cloth, yellow kept. When held, a wad, not a whole sheet.
10. The strap: a tan leather neck strap with two lugs, rings, keepers and protectors, gathered behind the lens half, the scratches on the loop.
11. The camera body deepened to 32 mm (real) with its roundings swapped.
12. The clean mechanism bright. The jammed one tarnished, with a sprung spring.
13. Every café prop and place in B3: the cherry-red radio as the row's one accent, the whiteboard over the parts wall, the parts box on the counter's west end, the three Synty bottles replaced by the syrup rack.
14. The story props' places: under the till, and inside the back door.

## 6. Checks (what proves it)
- **Blender review renders** (Bible §4: both cameras):
  - The close-up at 58°, FOV 40°, at the TD's distance for each device, with each device on the mat, the rest, the tray and the cloth in all three states.
  - The overhead at **43°, FOV 44°, 34 m from (0, 0.6, 9)**, not "50°, 20 m", with the B3 clusters placed.
- **Bench lab photo check** (new, in the Bench 3 photo pass):
  - The mat beside the device samples L* 38–46, b* ≤ −12.
  - A screw in a cup is ≥ 6 px at the default framing.
  - Nothing of the mat's compartments or the rest falls inside U1–U5 at 16:9 and 16:10 (UI/UX's overlap check can own it).
- **An overhead 10-pixel check** (an editor script for Unity C): project each B3 cluster's bounds through `CmShopCam`. Pass ≥ 12 px in the smaller size at 1080p for R1, R2, R4, R5, C1, C2 and C3. R3 and S1–S2 are exempt by design.
- **Guards** (Environment's hard rules): the layout and occupied-circulation guards before and after (no change expected: nothing on the floor in a lane), Night walk 2's solid list rebuilt, Stations as reach (the desk corner must not block the bench's reach), and Room 1's occlusion-aware close-up check (no B3 piece in any device's frame).
- **Day 1 in playtest 6:** does he say "repair mat" without prompting? Does the cloth "open up"? Does the strap still read as a handle?

## 7. Open questions (his)
1. Is the watch at about 25 % of the frame by default, with the zoom for screws, acceptable? The alternative is to let its tool rest sit at the frame's edge. That is mostly the TD's, but it's a feel call.

## 8. Flags
- **The style guide:** "grey must lean warm" and "never cool". The mat is a cool chromatic accent by his decision of 14 Oct, like the brand green. Add `BM_Silicone #4F70A0` to the guide's palette with his say-so (the guide changes only with it).
- **Stale documents:** three role files say the overhead is "50° from the south, ~20 m" (`art-direction.md:23`, `environment-scene.md:22`, `blender-3d.md:24`). The scene has 43°, 34 m, FOV 44°. Review renders made at 50°/20 m mislead. The Producer should correct the three lines.
- **The TD's 60 % target** collides with "compartments in view" plus U1–U5 at real proportion (§2). The numbers are from a search over camera distance and aim point for each device.
- **The purpose rule, job 2 negative:** R1's items must never be phones, watches or cameras. They have no collider and no hover, so they can't read as jobs.
- **"Objects are objects, information is projected":** the whiteboard and tags are static texture. A live "today's jobs" board would put game state in the world and add a feature (scope freeze).
- **Real life first:** the brush must never be stored on its bristles (`BenchClothSteps` poses).
- **Contracts:** the file names `BT_Cloth` and `BT_Brush`; tools' origins at their working tips; the camera's child names (`Upper strap`, `Lower strap`, `Worn loop`, `KEEP - scratched sentimental strap`); Devices 2's `moves` for the deeper body. They change only with Systems and the TD.
- **Licensed assets:** the three Synty bottles are switched off, not deleted.
- **Not mine, noticed:** the grinder's beans are one solid (`cafe_props.py`). Cinematics cut the shot; it waits for a hero pass if the shot returns.
- **No new mechanics:** the cups, pad, well and tray are the auto-tray's places (decisions 1 and 6), and per-device positions are data. Everything in B is décor (decision 9).

## 9. The one ordered list for the Blender lead
1. **`Tools/Blender/bench_stage.py` (new):** `BM_RepairMat` with its empties, `BM_Grid.png` and `BM_Rulers.png` (A1). It doesn't wait on the TD.
2. **`bench_tools.py`:** `BT_ToolRest` with the cork cloth tray (A2) and `BT_ScrapTray` (A3).
3. **`bench_tools.py`:** `BT_Cloth` thinned with its corner up, `BT_ClothHeld` with keys `Press` and `Bunch`, optional `BT_ClothHalf` (A4), and the `Bend` key on `BT_Brush` (A5). Re-export with `export_bench_tools.py` and its manifest. Check: `0 problem(s)`.
4. **`hero_camera.py` v3.6, once the TD's scale method is known:**
   - The strap (two lugs, rings, protectors, keepers, the drape and loop, the scratches moved) (A6).
   - `Clean shutter mechanism` and the jammed one's tells (A7).
   - The body 32 mm deep with its roundings swapped, plus its `moves` (A6).
   - One export through `export_hero_devices.py` and `devices.json`.
5. **Screws** (`hero_phone.py`, `hero_watch.py`) at the TD's sizes, black-oxide and blued, plus `BT_ScrewHole.png` (A8).
6. **`render_bench_icons.py`:** UI/UX's 13 icons plus the contact sheet (A9).
7. **Review renders** for 1–6: the close-up per device, both rest states of the cloth, a cups-with-screws detail.
8. **`Tools/Blender/cafe_repairside.py` (new):** R1, R2, R3, R4, R5 (the tags, whiteboard and letters as textures with Content's words), plus export and review.
9. **`Tools/Blender/cafe_cafeside.py` (new):** C1, C2, C3.
10. **Story textures** S1 and S2 on the house kit's letter and post meshes.
11. **Overhead review renders** at 43°/34 m/FOV 44° with 8–10 placed, before and after, for Environment's Unity C step. Update `Tools/Blender/README.md`.

## Sources
- Silicone repair mats: [proto-pic.co.uk, 45 × 30 cm mat](https://proto-pic.co.uk/product/silicon-repair-mat-17-7-x-11-8-inch-45-x-30cm-anti-slip-base); [littlebirdelectronics.com.au, 300 × 450 mm magnetic mat](https://littlebirdelectronics.com.au/products/heat-resistant-magnetic-silicone-mat300x450mm).
- ReStory's bench (parts by zones, the notepad on the right, cleaning at the bench's side): [progameguides.com](https://progameguides.com/restory-chill-electronics-repairs/restory-chill-electronics-repairs-beginners-guide/), [gamerblurb.com](https://gamerblurb.com/articles/restory-chill-electronics-repairs-how-to-clean).
- Camera, strap, cloth and tool measurements: `claude/hero-reference-sheet.md` §1–5 and §8 (its [seen] photographs).
