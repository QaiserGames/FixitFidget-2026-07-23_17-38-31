# Builder U report: the art audit's Unity fixes (26 Oct 2026)

Copy: `/root/wt24/U`. Nothing committed. Every changed file passes `python3 /root/tools/csparse.py` ("clean"). I had no Unity, so nothing here has been compiled or run.

## 1. What I did (files)

- `Assets/Editor/ArtFixUnitySteps.cs` (new). Nine scene/asset steps under `Fixit Fidget/Art fixes/`, an Undo for each, and a read-only Play-mode check for a01/a04.
- `Assets/Scripts/PolygonNpcVisual.cs`. a01: the CC0 outfits are switched off once a body has been dealt, and the hidden body is kept hidden. a04: each body part's culling box is widened once.
- `Assets/Scripts/DrinkLiquidVisual.cs`. a08: a matt drink surface, the finding's colours, and a crema ring on black coffee.
- `Assets/Scripts/DayIntro.cs`. a08: one literal (the intro cup uses the same smoothness). a69: the `house.grace` shot moves 1 m north.
- `Assets/Scripts/ConversationController.cs`. a36: the talk camera sits about 20 cm further back (new default `ShoulderOffsetDefault`).
- `Assets/Scripts/GrimeSpot.cs`. a26: the smeared haze is a grey-brown instead of near-white.
- `Assets/Data/Resources/Bench outline.shader`. a25: the hidden (dashed) pass only counts something really in front of a part.

Every runtime edit is marked `// 26 Oct 2026 (Art fixes)`. I did not touch any file the Days or furniture builders own.

## 2. Each finding: what I found, what changes, where, and how to see it

### The three to investigate first

**a01 (a second person inside each customer). Real, and it is in the game, not only the labs.**
- Cause: every customer and patron carries two body systems. `NpcVisualVariants` is the CC0 walk-in outfits. `PolygonNpcVisual` is the bought city look.
- `PolygonNpcVisual.ApplyAppearance` hides every skinned renderer. But `NpcVisualVariants.Start` switches one outfit's renderers back on.
- The code assumed the variants' `Start` runs first ("which runs first"). Unity doesn't promise the order of two scripts' `Start` on one object.
- When the variants ran second, the CC0 body showed inside the city look, on the same skeleton and moving with it.
- That matches every photo. The blonde woman in the red coat is the dealt city look, scaled to the rig's hips. Inside her you see Valerie's grey T-shirt, Beatriz's darker CC0 face, and Priya, half T-shirt and half jacket.
- The lodger and Grace-at-home code already switch the variants off for exactly this reason. Customers and patrons never did.
- `SkinToWorld` and the recorders only bake meshes for measuring, so they are not the cause.
- Why he never saw it: it shows mostly up close (first person at the till and the repair bar). From the overhead, the larger city body hides the inner one.
- Fix (`PolygonNpcVisual`): `QuietOutfits()` switches `NpcVisualVariants` off after a successful deal (a city look on, or the CC0 share's outfit picked). Whichever `Start` comes first, it can't switch an outfit back on.
- `LateUpdate` also re-hides any hidden renderer while a look is on. That is a cheap loop over about 24 references.
- See it: run `Art fixes > Check - one body each, heads kept (Play mode)` during a day ("a01 ... one body drawn"). The Stations lab's first-person counter photos should show one person.

**a04 (seated customers lose their heads). Real.**
- Each CC0 body part (head, hat, torso, legs) is its own skinned mesh. Each is culled by a box fitted to that part in the standing pose and carried by its root bone.
- Seated, the head drops about 0.5 m below its box. A camera about 2 m away that frames only head and shoulders can have the box off screen while the head is on screen. That is the side view of the hard-hat worker (Worker_Head).
- Fix (`PolygonNpcVisual.WidenBounds`): once at `Start` for the actor's own parts, and once per applied look, each part's local box becomes the whole body's box plus 0.35 m.
- This costs nothing per frame. I did not tick Update When Offscreen, because that would skin every hidden body every frame.
- See it: the same check ("a04 ... culling box holds the head", seated people only). In the NpcSit lab's `3-Patron_12-seated-side` photo the head should be there.

**a05 (the black lid behind the man at the bins). Real; it is lighting, not paint.**
- I parsed the cut lid meshes in `/root/ref24/Assets/Art/NightZero/Dumpster - lids cut free.asset`. Every corner of both lids has one UV (0.076, 0.011), so the top and the underside are the same atlas colour.
- When the lid is open, the underside faces the street and the camera, away from the wall lamp. Only the night's ambient light reaches it, so it reads as RGB about 6,6,12.
- Fix (step 8): the two lids get their own copy of the skip's material, with a faint emission of its own atlas colour (0.16). At night the underside reads as the lid's dark green; by day the glow is lost in the light.
- See it: step 8's photos (the reveal camera, and from the alley with the lid opened for the photo). The Night 0 lab, photos `06-the-deal-begins` and `07-the-deal-reply-1`.

### The steps (run on his Mac in this order)

**Step 1, People** (`art-fix-1-people`)
- **a15:** finds the street walkers (StreetLife actors with a fixed city look), working out the look each actually wears after the stand-in skip.
  - A twin gets the least-used free look of the same body (never a regular's stand-in, read from the `CustomerProfile` assets).
  - Walkers on the same route that start less than a fifth of it apart are moved about half a route apart.
  - See it: photo `a15-west-street-pavement`, and the DayIntro lab's `1-first-morning-1-street-row-a`.
- **a46:** writes Palette Base and Palette Alternates onto `Customer.prefab` and `Patron.prefab`, from the pack's `Assets/Synty/PolygonCity/Materials/Alts`. Matching is by family (the name without its last letter).
  - The house's Wave 2 6b uses an exact match. The prefabs in ref24 carry no palette at all, so I think it never matched.
  - If no alternates exist, it changes nothing and says so. The code already deals colourings fewest-first.
  - See it: CafeLife overhead `04-rush`.

**Step 2, The bench close-up** (`art-fix-2-bench`)
- **a24:** every renderer of `CK_BenchLamp*` stops casting shadows. The lamp's own light casts none; the room's lights were casting its shape onto the bench.
- **a30:** on Ace's desk only (`LI_DeskCorner*`), the red spool becomes brick, the green spool and the mug (DC_Sticker_Green) sage, and the meter (LI_Yellow) mustard.
- **a32:** this is a power strip under the bench top (the R4 reading), kept and toned on this bench only:
  - DC_Ceramic sockets become a dark socket grey;
  - the CF_LightStrip (emission 1.6, the one pure-white square) becomes a soft warm glow (0.55, 0.45, 0.33).
- **a33:** `LI_DonorParts*` is switched off.
- See it: photos `a24-a30-a32-the-bench-in-first-person` and `a32-a33-under-the-bench`; the Bench lab's `00`, `01` and `15`.

**Step 3, The counter and the drinks machine** (`art-fix-3-counter-drinks`)
- **a09:** `LI_CupStation`'s CF_Black is split into two sub-meshes by connected parts: the widest, lowest part is the board, the rest are the tubes. The board becomes T2_Walnut and the tubes DC_Steel. The green sticker band already on the mesh stays.
- **a35:**
  - The pendant mesh shared by the counter's and the tables' lamps loses its bulb part (small, near the axis, egg-shaped, lowest of the candidates).
  - A smooth sphere child of the same size and material replaces it on every lamp that uses that mesh.
  - Each sphere is added to `CafeViewMode.overheadFixtures` when its pendant is listed there.
- **a36:** sets `shoulderOffset` (0.6, 0.06, -0.62) on the scene's ConversationController(s). That is 0.87 m from Ace's eyes instead of 0.68 m.
- **a38:** the two `Cup caddy rail*` parts are switched off.
- **a39:** a small badge in the middle of the "Ivory curved header", facing out of the machine (the root's -Z): a chrome rim, the machine's own green enamel (BF_Enamel), and "ACE'S" in TextMesh Pro, bold, in the machine's cream.
- See it: photos `a09-a38-a39-the-drinks-machine-head-on`, `a09-the-cup-station-from-ace` and `a35-the-counter-lamp`; Stations first person `drink-2a` and `drink-1-at-the-counter`; Dialogue `03-her-request-and-the-replies` for a36.

**Step 4, The café room** (`art-fix-4-cafe-room`; guards on, routes re-baked, the room's 14 photos)
- **a13:** the renderer "Cloud plaster" whose parent is "V3 - courtyard-facing cafe windows" gets the back wall's Oat plaster. The street's copies are untouched.
- **a52:** the "Cream trim" of that same group (the ledge) gets Sage joinery.
- **a43:** each repair-bar stool model and its SeatPose, StandPoint and FaceTowards move 10 cm away from the bar. Each CupSpot stays on the bar.
  - The hand-over probe still passes. It measures from the bench's stand point to the seat. The east stool goes from 3.75 m to about 3.85 m, inside the 4.1 m the bench reaches across (Across Counter Reach 3.5 + 0.6). Stool 1, the nearest, is about 2.8 m.
  - The step refuses if any seat would be more than 4.0 m away.
  - The seat height doesn't change, so Life 4 and Sit 4 still sit people.
  - The circulation guards roll the step back if any route gets worse.
- **a51:** `LI_Rug_Corner` gets a deeper tan copy of its material and becomes 35% bigger in plan.
- **a54:** "Repair bench mat" and "Drink station mat" become a dark brown rubber (0.21, 0.16, 0.13).
- See it: photos `a13-a52-east-windows-from-the-bench`, `a43-the-repair-bar-stools` and the game camera; NpcWave2 Life 4 `A3-sit-strip-0x`.

**Step 5, The patio and the shopfront** (`art-fix-5-patio`)
- **a07:** the yellow ball is switched off. The bud vase is cut down to a small pot. A copy of the pocket gardens' Kenney `flower_yellowA` stands in it.
- **a58:** this patio table's T2_Marble slot becomes a warm grey copy (0.71, 0.68, 0.63).
- **a60:** the `T2_Brass` material asset becomes a little paler and softer (0.80, 0.69, 0.45; smoothness 0.72). It applies to every Tier 2 piece.
- **a59:**
  - Three Kenney `plant_bushDetailed` copies go in each "Patio - Planter_01", sized to cover the boulders.
  - The boulders are part of the Synty planter mesh, so they are covered, not removed.
- **a61:**
  - The two Kenney "Patio rounded timber chair"s are switched off.
  - The café's own `CK_Chair_Spindle`, from its FBX, stands in each place, turned to face the table.
  - I named them "Patio spindle chair (...)" so nothing that looks for "CK_Chair" counts them as café seats.
- See it: photos `a07-a58-a61-the-patio-flower-shot` (the intro's own camera), `a59-the-door-planters` and the game camera.

**Step 6, The streets** (`art-fix-6-streets`)
- **a06:** the "Leaf green" and "Planter clay" renderers under "10 - cohesive neighborhood V4/Street furniture" are switched off. In that group those two materials are used only by the two trees.
  - A copy of the courtyard's Kenney `tree_oak` stands at each tree's foot, (-17.05, *, -13) and (-17.05, *, 33).
  - The foot height is read from the trunk mesh.
- **a16:**
  - Eight homes, built like the hill's own "Hill home" (box, roof, windows, and the hill's materials), line the first 30 m of both uphill streets on both sides (z 51 and 65).
  - They sit in the gaps between the hill's two existing rows, with windows on the street side.
- **a65:** the five "Low sage shrub" balls are switched off, with a Kenney bush in each place.
- **a19 (surveyed, not changed):** the step lists the POLYGON City buildings with the most wall facing the game camera (rays on a 48 x 27 grid), and takes a game-camera photo.
- See it: photos `a06-a16-street-row-first-frame`, `a06-west-street-north`, `a16-the-hill-streets-from-above`, `a65-the-car-park-island` and `a19-game-camera`; DayIntro `1-first-morning-1-street-row-a`.

**Step 7, Grace's house** (`art-fix-7-grace-house`)
- **a17:** her "Street palette" renderer gets "Saffron - Grace's clear yellow (art fix a17)": #E9B43C, with the plaster's normal map kept and its colour map removed.
  - The name starts with "Saffron" so the Night guidance checks still count her house as saffron.
- **a18:** the glowing ball shrinks to about 9 cm and sits in a small iron lantern (cap, finial, base and four posts, in Ironwork).
  - The lantern is on the house itself, so it shows by day too.
- **a21:** the `GH_Lampshade` material is drawn on both sides (`_Cull` 0, double-sided GI). That also covers her bedside lamp and the house kit's pendant.
- **a70:** the house-paint slot of her "Door leaf" becomes the door's own colour, 12% lighter.
- **a71:** the front's clapboard lips (3.5 cm every 25 cm, in her new yellow) run down the house's open side, the side away from house 2.
- **a75 (look only, as he asked; nothing changes):**
  - The black block is the stairs model's own side (GH_Stairs_L, in CC_Wood_Espresso, the darkest wood), where an under-stairs cupboard would be.
  - GH_UnderStairsDoor is in the kit but not placed. It is unlit under the dark upstairs at night.
  - "Upper flight's side (the cupboard front)" is only a collider.
  - The step's report lists the stair renderer's materials to confirm this.
- See it: photos `a17-a69-a70-her-house-the-day-2-shot`, `a18-a70-her-door-from-her-step` and `a71-her-side-wall-from-the-cross-street`; Grace-at-home night `17a`; GraceHouseWalk crouched photos for a21.

**Step 8, The bins at night (a05).** Described above.

**Step 9, Softer window shadows** (`art-fix-9-shadows`)
- **a45:** High (`PC_RPAsset`, the default level) already has soft shadows on High. Its 50 m of shadow over four cascades leaves the café in the last cascade, at about 26 mm a texel.
- Now: a 4096 shadow map and 35 m of shadow, about 9 mm a texel.
- Medium and Low are untouched. `QualityPresetSteps`' table owns them, and re-running Performance 2 would overwrite them.
- See it: photo `a45-the-floor-by-the-windows`; CafeLife `01`–`04`.

### Runtime-only

- **a08:** the drink colours were dark already. The cause of the white and the dishwater was smoothness 0.68: a mirror-flat disc seen at a glancing angle showed the ceiling and the windows, so two coffees under different lights differed.
  - Smoothness is now 0.3.
  - Coffee, espresso and americano are a dark roast (0.23, 0.13, 0.075) with a crema ring (0.54, 0.35, 0.20).
  - Latte is tan, cocoa brown and tea amber.
  - The ring is a second disc, 0.8 of the radius, 0.6 mm above.
  - See it: Stations first person `drink-3-the-drink-made`.
- **a25:** the hidden pass is tested 1.5 mm plus 1% of the distance nearer the eye (about 5.5 mm at the bench). Only a real occluder now makes dashes. Parts under a cover get no glow anyway (`CoveredBy`).
  - See it: Bench lab `06-cover-in-the-well`.
- **a26:** the smeared grease goes toward (158, 148, 132) instead of (242, 245, 247). Alpha and grading are unchanged.
  - See it: Bench lab `12b-whats-left`.
- **a69:** the signal post (V4 through traffic, x -15.45, z -2.8) was 0.5 m off the line from the camera to her door. With the camera and the aim both moved 1 m north it is 1.5 m off.
  - See it: DayIntro `2-free-day-2-3-house-grace-a`.

## 3. Decisions his to overrule

- **a01:** I fixed the game, not a lab, because it is game code and the cause can happen in play. I quietened the outfits rather than removing `NpcVisualVariants`.
- **a04:** wider culling boxes instead of Update When Offscreen, for performance.
- **a05:** a faint self-glow on the lids instead of repainting (the paint is already green) or opening the lid less (the underside would still face the camera).
- **a08:** the crema ring only on the three black coffees. The exact colours are my picks from the finding.
- **a09:** T2_Walnut for the board and DC_Steel for the tubes.
- **a16:** I lined the streets with code-built hill homes, not by re-aiming the intro.
- **a19:** surveyed only. I can't tell which two blocks show their blank sides without seeing the frames, and turning a whole Synty block is a layout change. The step lists candidates; turning two of them is his call.
- **a32:** kept as a power strip and toned, not switched off.
- **a43:** a full 10 cm (my reach sums say it is safe). Cups stay on the bar.
- **a45:** High only.
- **a46:** palette alternates matched by family, not exact name. There is no skin-colour line-up in my step; the house's Wave 2 6a line-up only runs on an exact match, so look at the first busy-day photos.
- **a51:** a 35% bigger rug and a tan of (0.80, 0.60, 0.42).
- **a54:** a dark brown rubber instead of the till mat's texture (a stretched texture on a scaled box is a guess).
- **a58:** warm grey; veined marble for Tier 2 waits until the shop sells it.
- **a59:** covering the boulders with bushes rather than removing them.
- **a61:** CK_Chair_Spindle as the café's own chair.
- **a69:** moved 1 m north (+z).
- **a71:** lips only, no corner trim on the back corner.

## 4. Outside my files (exact edits)

None are required. Two notes:
- If the Night 0 step (`NightZeroSteps`) is re-run, it rebuilds the lids with the skip's material. Run Art fixes 8 again after it.
- If `Fixit Fidget > NPC > Wave 2 6b` is ever run, it would overwrite a46's palette on the two prefabs (or clear it, if its exact match finds nothing).

## 5. On his Mac, in order

1. Let it compile. The Console should be clean.
2. Open the café scene. Run `Fixit Fidget > Art fixes > 1` through `9`, in order.
   - Each ends with "DONE. Art fixes N - ...". If a step can't find its pieces it says "REFUSED (nothing was changed): ...", and the reason names what's missing.
   - Reports go to `Logs/ArtFixes/<label>-<time>/report.txt`, with photos `1-before-*` and `2-after-*` (a PASS line for each check), plus `<label>-last.txt`.
   - Step 4 also prints the layout and circulation checks, and takes the room's 14 photos before and after. A worse route rolls it back.
3. Save the scene.
4. Run `Fixit Fidget > Checks > Probe - the bench's hand-over reach`. It must still say "PASS  from every one of them stool 1 is handed over to." Then run NPC Life 4 and Sit 4; people must sit on the repair-bar stools.
5. Start a Day 2 or 3 in Play and run `Art fixes > Check - one body each, heads kept (Play mode)`. Pass is "[Art fixes check] All N checks passed."
6. Run the labs whose photos show the findings: Stations (first person), Bench, Dialogue, NpcSit, Night 0, Grace at home (night), DayIntro, CafeLife Day 1.
7. **Undo:** `Fixit Fidget > Art fixes > N - Undo`, newest first. The café redesign's "latest step first" rule covers these steps too.
   - Each Undo puts back the scene record and then the extra record in `Logs/ArtFixes/<label>-undo.json`: shadow switches, field values, material and settings assets, prefab palettes.
   - Materials and meshes the steps made stay in `Assets/Art/Materials/ArtFixes` and `Assets/Art/Models/ArtFixes`, unused.

## 6. Risks and APIs I'm unsure of

**APIs**
- `GlobalObjectId.GetGlobalObjectIdSlow`, `TryParse` and `GlobalObjectIdentifierToObjectSlow`, used for the extra record. Objects made in an unsaved scene have no stable id; I only record existing objects and assets.
- `EditorJsonUtility.ToJson` and `FromJsonOverwrite` on a Material and on the URP asset (asset Undo).
- `SkinnedMeshRenderer.localBounds`, assumed to be in root-bone space (I am fairly sure).
- `TMP_Settings.defaultFontAsset` and `TextWrappingModes`, copied from `BeverageStationSetup`.
- `GetComponentInParent<T>(bool)`.
- `Mesh.SetTriangles(List<int>, int)` after raising `subMeshCount`.

**Heuristics with refusals**
- a09's board: the widest, lowest part. If the step refuses, the board and tubes are one piece and it's a Blender job.
- a35's bulb: near the axis, egg-shaped, lowest. The step refuses if nothing matches; the report names the part it picked.
- The a43 move direction: the seat's direction to its CupSpot.

**Placements without a picture of the result**
- These were placed from code and the scene file, not from a view: a59's bush sizes, a61's chair facing (+Z faces the table, as the seat plan's chairs do), the a16 homes, a18's lantern size, the a39 badge sitting flush on a curved header, and a71's side choice.
- Every one has photos for review.

**Other risks**
- a21 edits a shared GraceHouse material. If the Blender builder's re-import rebuilds materials, re-run step 7.
- a60 changes `T2_Brass` everywhere, as the finding asked for "all Tier 2 shop pieces later".
- a46 depends on the Alts folder's naming on his Mac.
