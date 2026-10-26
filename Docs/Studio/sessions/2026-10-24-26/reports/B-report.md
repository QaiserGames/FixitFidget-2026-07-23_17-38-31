# Builder B2 (3D Art & Blender): the art audit's model and texture fixes — report

24–26 Oct 2026. Copy: `/root/wt24/B` (nothing committed). Renders: `/root/review24/designs/B-renders/` (`<id>-before.png`, `<id>-after.png`, and `contact-sheet.png`). Log: `/root/review24/reports/B-progress.md`.

**Status.** Done: a02 (added on 26 Oct), a12, a14+a62, a20, a22, a23, a27, a29, a31, a34, a37, a41+a42, a44, a48, a49, a50, a53, a72, a73, a74. a55 is done as a Unity step. **a47 is not fixed.** Synty's face texture isn't in the cloud, so nobody here could look at it. A read-only Unity step collects what the paint job needs.

Every model was rebuilt by its script, at the same path and name. Before each change I checked that the unchanged scripts rebuild `/root/ref24` exactly. After each rebuild, I compared every FBX in the family with the baseline: only the intended pieces differ. Names, origins, shape keys and the order of existing material slots are kept everywhere. New slots only come after the old ones.

## 1. What I did (files)

Scripts (`Tools/Blender/`):
- `bench_tools.py`: a23, the brush is one grooved bristle block (key `Bend` kept). a02, `BT_ClothHeld` v2 (keys `Press`, `Bunch` kept).
- `export_bench_tools.py`: the manifest's text for `Bend` and for `BT_ClothHeld`.
- `hero_phone.py`: a12, the phone's insides (v3.8).
- `hero_watch.py`: a27, the sub-seconds ring and ticks on the dial (v3.7).
- `cafe_repairside.py`: a29, the ruler's marks. a44, the cabinet's back. a41+a42, the pickup row's bags, lamp and controller.
- `cafe_livedin.py`: a37, the condiment tray. a48, the napkin dispenser. a49, the tote bag (two passes). a50, the laptop screen.
- `export_cafe_livedin.py`: `LI_Screen`'s label size and UV note.
- `grace_house.py`: a72 (`PhotoProp`, the photo faces mapped 0..1). a20 (the coat stand). a73 (`sleeve_geom`). a74 (`panelled_slab`, `panelled_door`).
- `grace_house_v2.py`: a22 (`quilt_asleep`, the new `GH_Hair` material, `loft_y`). a74 (`door_leaf` uses the shared door).
- `cafe_bar.py`: a31, round springs.
- `cafe_props.py`: a53, the olive tree's leaf cards removed.
- `draw_cafe_textures.py`: a62, `draw_aframe` now draws near-white chalk and an amber rule. Not run here: its fonts are missing.
- `README.md`: three rows for the new scripts.
- NEW `cafe_aframe.py`: a14, rebuilds `MenuBoard.fbx` as a true A.
- NEW `cafe_till.py`: a34, labels the till (`CashRegister.fbx`).
- NEW `cafe_art_fix_textures.py`: the new pictures, drawn with Pillow and Baloo 2 (a50, a72, a34, a62).

Unity:
- NEW `Assets/Editor/ArtFixBlenderSteps.cs`: five menu items under `Fixit Fidget/Art fixes/`. csparse: **clean**.

Built files, at their Mac paths in the copy. The integrator copies these to the same paths on his Mac:
- `Assets/Art/Models/BenchTools/BT_Brush.fbx`, `BT_ClothHeld.fbx`, `bench_tools.json`
- `Assets/Art/Models/Devices/HD_Phone.fbx`, `HD_Watch.fbx`, `devices.json`
- `Assets/Art/Models/CafeKit/LI_DeskCorner.fbx`, `LI_PickupRow.fbx`, `cafe_dressing.json`
- `Assets/Art/Models/CafeKit/LI_CondimentStation.fbx`, `LI_NapkinDispenser.fbx`, `LI_ToteBag.fbx`, `LI_Laptop_Open.fbx`, `cafe_livedin.json`
- `Assets/Art/Models/CafeKit/CK_BenchLamp.fbx` and `cafe_bar.json`. This is his own hand-edited file with one line changed: CK_BenchLamp `"tris": 964` → `2224`.
- `Assets/Art/Models/CafeKit/CK_OliveTree.fbx`, `cafe_props.json`
- `Assets/Art/Models/GraceHouse/GH_CoatStand.fbx`, `GH_InteriorDoor_Leaf.fbx`, `GH_BedroomDoor_Leaf.fbx`, `GH_CupSleeve.fbx`, `GH_CupBox.fbx`, `GH_Quilt_Asleep.fbx`, `GH_Frame_S.fbx`, `GH_Frame_M.fbx`, `GH_Frame_L.fbx`, `GH_Frame_XL.fbx` (the frames changed only their photo UVs)
- `Assets/Art/Models/MenuBoard.fbx`, `Assets/Art/Models/CashRegister.fbx`
- `Assets/Art/Textures/Cafe/LI_Screen.png` (new), `CF_AFrame.png` (replaces his), `CF_TillKeys.png` (new), `CF_TillScreen.png` (new)
- `Assets/Art/Textures/GraceHouse/GH_Photo.png` (new folder)
- `BlenderSource/BenchTools_v4.blend`, `HeroPhone_v3.blend`, `HeroWatch_v3.blend`, `CafeDressing_v1.blend`, `CafeLivedIn_v1.blend`, `CafeBar_v1.blend`, `CafeProps_v1.blend`, `GraceHouse_GroundFloor.blend` + `.json`, `GraceHouse_v2.blend` + `.json`, `CafeAFrame_v2.blend`, `CafeTill_v5.blend`
- `Assets/Editor/ArtFixBlenderSteps.cs`

## 2. Decisions and reasoning, finding by finding

For each: what changed and why; the renders; what keeps the code safe; whether it needs a Unity step.

- **a02, the cloth in Ace's hand** (`bench_tools.py`). Renders: `a02-before/after`, from the close-up camera's angle (58°, 0.58 m), cropped, mid-wipe (Press 1, Bunch 0.45).
  - I tried three "gathered knot with hanging corners" versions. Without the hand, each read as something else: popcorn with fangs, a paper lantern, a bow tie. A gathered neck of cloth reads as a duck's neck whenever the fist round it is out of sight.
  - What reads as a cloth is the tray's own folded cloth, which the audit said "looks right". So `BT_ClothHeld` is now that cloth, grabbed. It is two layers with soft rounded edges, turned 26°, its edges not quite straight. It is pinched up near the middle where the unseen fingers hold it, into a soft peak 25 mm high. Seven folds run down from the peak, and the corner nearest Ace lifts. Its wiping face is the flat cloth round the pinch. Its size is 52 × 56 mm.
  - Kept: the name, the one material slot `BT_Cloth`, the origin (the underside on the glass, z = 0), and the keys `Press` and `Bunch` with the same meaning.
    - `Press`: the cloth spreads 8 %, the peak sinks 15 %.
    - `Bunch`: the leading side (Unity +X) rolls up 3.5 mm, the trailing side drags 8 mm, the peak leans 6 mm back.
  - `ToolPickup` reads the vertices and key deltas each frame (`FarEdge`), so no code changes. The far edge it puts at the cursor is now the cloth's far corner.
  - 1152 → 1592 triangles. Unity step: Bench tools 1. It turns on blend-shape import and checks against `bench_tools.json`, which I delivered.
- **a12, the phone's insides** (`hero_phone.py`). Renders: `a12-*`.
  - In the same footprint: a green logic board with chips, a battery with a cream label (printed lines, "Pome", a barcode), a six-turn copper coil, and two amber ribbon cables.
  - Two new materials, `HD_Board` and `HD_FlexAmber`, come after the old six slots. Body: 1956 → 7596 triangles.
  - Unity step: Devices 1, then Devices 2. They make the materials and put all nine on the prefab's Body.
- **a14 + a62, the patio A-frame** (new `cafe_aframe.py`, the re-chalked `CF_AFrame.png`). Renders: `a14-a62-*`, side on and from the street, with the chalk face placed the way Street 1 places it.
  - The A-frame is now a true A. Two leaves, each 0.67 m wide, meet at a hinge 0.93 m up. Each leans 14°, so the feet stand flat 0.51 m apart. Each leaf has a chalkboard sunk 4 mm into a 40 mm frame. Two hinges and two stays hold it together.
  - Kept, because the scene and Street 1 depend on them:
    - the object `Decor_MenuBoard.001` and its mesh `Cube.002`;
    - the three slots in order: wood, chalkboard, then `DC_Chalk`. That slot now holds the hinges and stays. Street 1 painted it dark, which suits iron.
    - the old export: Z up, the −90° X turn on the object (checked in the FBX), the drawn size of 1/4.2.
  - a62: I looked first, as the brief asked. In the game the chalk reads dim from across the street, so I re-chalked his own picture rather than redrawing it. The lettering is unchanged; the chalk is now near-white, filled in and at full strength (11.2:1 against the slate). The double rule under ACE'S CAFÉ is now the one amber line (6.5:1). `draw_aframe` was changed to draw the same picture on a machine that has the fonts.
  - Unity step: Art fixes 2. The scene's A-frame draws a grain-UV copy of the mesh, so a reimport alone changes nothing. The step remakes that copy in place, fits the collider, and puts a chalk face on each board.
- **a20, Grace's coat stand** (`grace_house.py`). Renders: `a20-*`.
  - The coat, collar and scarf are gone. The hat and its band stay.
  - Its materials go from five to four: wood, brass, sage, red. 600 → 456 triangles.
  - Unity step: Art fixes 1. Her house's pieces are plain objects that copied their materials when they were built, so the step lists them again from the model.
- **a22, Grace asleep** (`grace_house_v2.py`). Renders: `a22-*`, from the bed's left side and foot.
  - Her head now rests on the left pillow, 3 cm into it, lying on its side. Seen from that side, the back of the head and a bun show, in a new `GH_Hair` (#C9A15A). The quilt is pulled over her shoulder.
  - The quilt's sides now drape. They roll softly over the mattress edge, and the hem swings in and out by up to 8 mm (shallow folds). The foot is tucked behind the footboard.
  - The hem stays at 0.30 m, where the furniture step expects the bottom.
  - Slots: quilt, sheet, then hair. 408 → 940 triangles.
  - Unity step: Art fixes 1. It makes `GH_Hair`, maps the quilt's model to it, and lists the scene quilt's materials again.
- **a23, the brush** (`bench_tools.py`). Renders: `a23-before/after/after-bend`.
  - The bristles are one tapered, grooved block with a ragged end.
  - Kept: the origin at the working tip, and the key `Bend`. 1540 → 1804 triangles.
  - Unity step: Bench tools 1.
- **a27, the watch** (`hero_watch.py`). Renders: `a27-*`.
  - The dial is geometry, not a texture. The small-seconds ring and its ticks had been built 0.1–0.3 mm under the dial's face. They are now on it, with longer quarter ticks.
  - The numeral 6 is left off, as railroad dials do, because the ring would cross it.
  - Unity step: Devices 1 and 2.
- **a29 and a44, Ace's desk corner** (`cafe_repairside.py`). Renders: `a29-*` (with a zoom), `a44-*`.
  - a29: the steel ruler has ticks every 5 mm and the numbers 5–25. They stand 0.5 mm proud so they don't flicker under the overhead camera.
  - a44: the cabinet's back has a moulded rim, two ribs, two keyholes and a white PARTS label.
  - Both are added after everything else, so the slot order is kept.
  - Unity step: Café dressing 1. It checks against `cafe_dressing.json`, which I delivered.
- **a31, the lamp's springs** (`cafe_bar.py`). Renders: `a31-*`.
  - They are now round coils: eight sides, a ridge every 8 mm, 1.2 mm deep. The old ones had five sides and six deep waves, so they read as a ribbon seen edge-on.
  - 964 → 2224 triangles.
  - Unity step: Kit 1. `cafe_bar.json` is his hand-edited file with only the lamp's triangle count changed.
- **a34, the till** (new `cafe_till.py`, `CF_TillKeys.png`, `CF_TillScreen.png`). Renders: `a34-*`, from Ace's side.
  - The till's shape was good, so it is not rebuilt: not a vertex moves.
  - The 20 key tops now carry labels from a 5 × 4 atlas: 7 8 9 CLR VOID / 4 5 6 TAX SUB / 1 2 3 CARD CASH / 0 00 . NO SALE TOTAL. They are upright for Ace. Print 10.8:1, TOTAL in Ace's green 5.8:1.
  - Both displays, Ace's and the customers', show "TOTAL $ 4.50" in drawn seven-segment digits (7.1:1).
  - Kept: the object `Equip_Register.001`, the mesh `RG_Plinth.002`, the old seven slots with two added after them, and the old Z-up export.
  - Unity step: Art fixes 3. It makes the two materials and maps the till's model to them.
- **a37, the condiment tray** (`cafe_livedin.py`). Renders: `a37-*`.
  - It now holds a napkin stack, packets in a caddy, a ribbed stack of lids, sleeves, and a shaker with a CINNAMON label.
  - 442 → 2474 triangles.
  - Unity step: Lived in 1.
- **a41 + a42, the pickup row** (`cafe_repairside.py`). Renders: `a41-a42-*` plus zooms. The final after images were rendered again from the delivered FBX.
  - The paper bags are open and crumpled.
  - The desk lamp is wrapped in paper twisted shut at both ends, with its base and shade showing and a tape band.
  - The controller is a real pad's size, with cream buttons. Coloured A/B/X/Y would be Xbox trade dress.
  - Unity step: Café dressing 1.
- **a48, the napkin dispenser**. Renders: `a48-*`. It has rounded edges and a new `LI_Chrome` material, and is 20 % smaller (0.13 × 0.08 × 0.12). Unity step: Lived in 1.
- **a49, the tote bag**. Renders: `a49-*`.
  - The first pass still stood up square, so I did a second. Now the right half of the top has given way (6 cm lower). The mouth sags 4 cm between the handles, the front folds forward, and the jumper shows 5 cm down inside the open mouth. The base is flat.
  - 292 → 420 triangles. Unity step: Lived in 1.
- **a50, the laptop**. Renders: `a50-*`.
  - `LI_Screen.png`: a dark title bar over a light page with a sidebar, a heading, lines and a picture. There are no words, because at the table's distance they would only be noise.
  - The glass is mapped 0..1. Unity step: Lived in 1 gives `LI_Screen` its texture.
- **a53, the olive tree** (`cafe_props.py`). Renders: `a53-*`.
  - The leaf cards are gone. Their random draws are still taken, so every leaf mass is exactly as it was: a trimmed crown.
  - 2334 → 1374 triangles. Unity step: Kit 1 (`cafe_props.json` delivered). Room 1's check also needs one size updated (section 4).
- **a55, the back door**: a Unity step, Art fixes 4.
  - The door is POLYGON City's `SM_Prop_Door_01`, which only exists on his Mac.
  - The step leaves Synty's files and import settings alone. It gives the two back doors in the scene a copy of their meshes where every face has its own normal, so the seam can't show. The copies are saved in `Assets/Art/ArtFixes`. No other door in the city changes.
- **a72, Grace's photos** (`grace_house.py`, `GH_Photo.png`). Renders: `a72-*`.
  - Each frame's photo face is mapped 0..1. The picture is drawn at the frames' 3:4 shape: a sky, a far shore, and three people standing close, as a soft, faded print.
  - One picture for every frame, as the finding said.
  - Unity step: Art fixes 1 puts the texture on `GH_Photo` and makes its colour white.
- **a73, the reunion cups** (`grace_house.py`). Renders: `a73-*`.
  - 13 rings, the narrow ones at 0.85, as the finding asked. I spaced them as a saw-tooth (each narrow ring a fifth of the way to the next rim), so the rims read as rims, like stacked cups.
  - The length and girth are the same, so the sleeve still lies on the box.
  - The box's three sleeves improve too: sleeve 164 → 260 triangles, box 528 → 816.
- **a74, Grace's doors** (`grace_house.py`, `grace_house_v2.py`). Renders: `a74-*`, face-on and at an angle.
  - I did both of the finding's options. The panels are sunk 8 mm, behind 12 mm sloped edges. Face-on, the slopes catch the light differently from the flat faces: that is the darker inner line, drawn by the light.
  - Each door is one closed mesh, with no booleans.
  - The stiles are wider (0.10 and 0.09 m) and the lock rail runs 0.82–1.02 m, so the knob's rose sits on wood, not over a sunk panel.
  - The hinges are now set into the edge. The old ones lay in the door's faces, two faces in one plane, which flickers.
  - Landing door 404 → 564 triangles, bedroom leaf 416 → 576. Sizes and slots are unchanged.
- **a47, the cross faces: not done.**
  - The fix needs a softer-brow copy of Synty's face texture, and nobody here can see it.
  - Unity step 5 is read-only. It finds the textures the customers' looks wear and works out where the face is on each from the heads' front faces. It saves a 4× crop of each face and writes a report, ready for the copy to be painted.
  - Once the picture exists, a small step can give each look's material a copy that uses it.

**Checks.**
- Every export's audit: nothing floating, no faces without area.
- My own check for two faces in one plane on the changed Grace pieces: the doors went from 4 and 6 such pairs to 0, and the quilt has 0. The large wall frame and the cup box keep the pairs they already had; I did not change them.
- Café exports report 0 problems.

## 3. Decisions his to overrule

1. **a02.** The cloth is the folded tray cloth pinched where the fingers hold it, not a gathered knot with hanging corners (why: section 2). It is a little bigger than the old wad. A visible hand would sell the grab better, but that is outside these fixes.
2. **a22.**
   - Her hair colour, #C9A15A, is my reading of the lab photo through her window under lamplight.
   - She faces the middle of the bed, so the bun shows from the bed's left side.
   - The made quilt is unchanged.
3. **a72.** One snapshot for every frame. The people have no faces drawn, because it is a small print seen across a room.
4. **a73.** The rings are spaced as a saw-tooth, not evenly.
5. **a74.** Both options at once. The stiles are wider and the lock rail higher.
6. **a14.**
   - The A is 0.93 m tall with a 14° lean.
   - The chalk slot now holds the iron (hinges, stays).
   - Street 1's Undo record doesn't know the café side's chalk face.
7. **a62.** The accent line is amber, not green.
8. **a31.** The springs are kept and made round, rather than deleted. The lamp more than doubles in triangles.
9. **a34.** The key words and the price (4.50) are my choice. The display glows a little (emission 0.45).
10. **a37.** The tray is now about 2.5k triangles. It can be thinned if that matters.
11. **a12.** The battery's brand reads "Pome": fictional, a wink at apples. The controller's buttons are cream to avoid Xbox trade dress.
12. **a55.** Flat copies on the two back doors only. The alternative was the Synty model's smoothing angle, which would change every such door in the city.
13. **a47.** Left as a read-only finder step rather than a blind edit of a picture nobody here can see.

## 4. Edits outside my files (exact)

1. `Assets/Editor/GraceHouseFurnitureSteps.cs`, the `Hers` table:
   - Change `("GH_Photo", "BFA88A", 0.25f),` to `("GH_Photo", "FFFFFF", 0.25f),   // 24 Oct 2026 (art audit a72): white under GH_Photo.png`.
     Why: `MakeHerMaterials` sets `_BaseColor` on every run, so without this, running the step again tints the photo sepia.
   - After `("GH_Mirror", "C9D6DE", 0.95f),` add `("GH_Hair", "C9A15A", 0.25f),   // 24 Oct 2026 (art audit a22): her head on the pillow`.
     Why: without it the step reports GH_Hair as unmapped and fails.
2. Same file, the `Pieces` table (the furniture check compares triangles and size):
   - `("GH_CoatStand", "coat stand with her hat", 456, new Vector3(0.47f, 1.89f, 0.412f)),`
   - `("GH_InteriorDoor_Leaf", "door", 564, new Vector3(0.804f, 2.01f, 0.164f)),`
   - `("GH_CupBox", "the reunion cups: a box of three sleeves (the stash)", 816, new Vector3(0.644f, 0.175f, 0.587f)),`
   - `("GH_CupSleeve", "one sleeve of twelve cups", 260, new Vector3(0.382f, 0.094f, 0.096f)),`
   - `("GH_BedroomDoor_Leaf", "one of the bedroom's pair of doors (0.70 m)", 576, new Vector3(0.699f, 2.19f, 0.164f)),`
   - `("GH_Quilt_Asleep", "the quilt with her asleep under it (0.30 up, like the other)", 940, new Vector3(1.462f, 0.515f, 1.708f)),`
3. `Assets/Editor/CafeRoomSteps.cs` line 206, the `Built` table for the Room 1 check: `["CK_OliveTree"] = (new Vector3(.733f, 1.636f, .639f), 0f),   // 24 Oct 2026: its leaf cards off (art audit a53)`.
4. Optional, comment only: `Assets/Scripts/ToolPickup.cs` line 47 says the held wad "is a smooth dome with no fold or loose corner, which is Blender work". After a02 that is no longer true. Suggested wording: "the held cloth was rebuilt on 26 Oct (art audit a02): the folded cloth, pinched where the fingers hold it."

## 5. On his Mac, in order

Copy the files in section 1 to the same paths, make the edits in section 4, open the café scene (`Assets/Playtests/AcesCafeLayoutPlaytest.unity`) and let Unity reimport. Then run:

1. `Fixit Fidget > Bench > Bench tools 1 - Import and check the tools (the models only)`.
   - Pass: all PASS. BT_Brush has 1804 triangles and key Bend; BT_ClothHeld has 1592 and keys Press and Bunch.
2. `Fixit Fidget > Bench > Devices 1 - Import and check the hero devices (the models only)`, then `Devices 2 - Fit the hero devices at real size`.
   - Pass: the phone's Body wears nine materials, including HD_Board and HD_FlexAmber.
3. `Fixit Fidget > Cafe redesign > Café dressing 1 - Import and check the pieces (the models only)`.
   - Pass: LI_DeskCorner and LI_PickupRow match `cafe_dressing.json`.
4. `Fixit Fidget > Cafe redesign > Lived in 1 - Import and check the lived-in pieces (the models only)`.
   - Pass: LI_Chrome is made, and LI_Screen wears LI_Screen.png.
5. `Fixit Fidget > Cafe redesign > Kit 1 - Import and check the café kit (the models only)`.
   - Pass: CK_BenchLamp 2224 and CK_OliveTree 1374 match the manifests.
6. `Fixit Fidget > Night > Break-ins - Grace's furniture: import and check (the models only)`.
   - Pass: "All good." This needs the edits in section 4.
7. `Fixit Fidget > Art fixes > 1 - Grace's house: her photos, her hair and the re-listed materials (scene)`.
   - Pass: "[Art fixes] Grace's house: DONE, n change(s)."
   - The first run should make about five changes. The report lists GH_CoatStand and GH_Quilt_Asleep each wearing the model's materials.
   - A second run: "DONE, 0 change(s)."
8. `Fixit Fidget > Art fixes > 2 - The patio A-frame: grain copy, collider and a chalk face each side (scene)`.
   - Pass: two boards of about 0.60 × 0.89 m, facing about 180° apart. The street face moves onto its board, and "CK_AFrameFace (patio A-frame, café side)" is added at a scale of about 0.97.
   - If it refuses with "not two boards", MenuBoard.fbx didn't take.
9. `Fixit Fidget > Art fixes > 3 - The till: key labels and the price on its screens (materials, import)`.
   - Pass: CF_TillKeys and CF_TillScreen are made in `Assets/Art/Materials/Cafe`, and CashRegister.fbx is mapped to them.
10. `Fixit Fidget > Art fixes > 4 - The back door: flat shading on its frame (scene)`.
    - Pass: each mesh under "Back door (inside)" and "Back door (outside)" now uses a "… - flat" copy.
11. `Fixit Fidget > Art fixes > 5 - Customer faces: find and photograph the shared face texture (read-only)`.
    - Pass: crops in `Logs/ArtFixes/faces-<time>/`, ready for the a47 paint job.
12. Save the scene.

Where to see each one in the game (lab photos):
- **Bench check:**
  - `13a-the-cloth-wiping` (a02);
  - the brush photos (a23);
  - `01-on-the-mat` and `04-screws-in-their-cups`, with the back off (a12);
  - the watch on the mat (a27);
  - first person at the bench (a29, a44 and the lamp's springs, a31).
- **Day 1 guide / conversation check** (first person at the till and the talk camera): the till's keys and screen (a34).
- **Café life overhead and first person at the counter and tables:** a37, a41, a42, a48, a49, a50, and the olive tree inside the door (a53).
- **The street, first person and overhead:** the A-frame (a14, a62).
- **Night grace-at-home-night:**
  - `16a`/`17a` and `06–09`: her front room, with the coat stand and photos (a20, a72);
  - `09d`, `10`, `11`: upstairs, her doors and her asleep (a74, a22).
- **Night two check (cups), `05-grace-kitchen-the-cups`:** the reunion cups (a73).
- **Night zero:** the back door from inside, in first person (a55).

## 6. Risks, and APIs I'm not sure of

- **C# (not compiled here; csparse clean).** The APIs I'm least sure of:
  - `Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include)` (the single-argument overload, per the house rule);
  - `ModelImporter.GetExternalObjectMap()` / `AddRemap` / `SaveAndReimport`;
  - `PrefabUtility.GetCorrespondingObjectFromSource` / `InstantiatePrefab(prefab, parent)`;
  - `EditorUtility.CopySerialized` on a mesh sub-asset;
  - `Mesh.GetUVs`/`SetUVs`/`SetColors(List<Color32>)`/`IndexFormat.UInt32`;
  - `MaterialGlobalIlluminationFlags.RealtimeEmissive`;
  - `TextureImporterCompression.CompressedHQ`;
  - `Graphics.Blit` + `ReadPixels` + `EncodeToPNG` (step 5).
- **Reading FBX meshes.** Steps 2 and 4 read mesh data from FBX meshes after `Object.Instantiate`, as `CafeFurnishing.GrainCopy` already does on his Mac. If Unity refuses because a mesh isn't readable, the step fails with that message and changes nothing.
- **The back door.** Its child structure is unknown here. Step 4 flattens every mesh under the two back doors. POLYGON assets are flat-shaded anyway, so nothing else should look different.
- **The A-frame.**
  - If Street 1 is undone, the café side's chalk face stays and has to be deleted by hand.
  - If Café furnishing is ever run again, it makes the grain copy itself from the new model, which is fine.
- **Grace's house.**
  - Running "Break-ins - Grace's furniture" again without the section 4 edits tints the photos sepia and fails on GH_Hair.
  - Building her house from scratch again (Break-ins 1) copies the new materials, which is fine.
- **The A-frame and the till keep their old export** (Z up, the axis turn on the object), checked in the FBX files. Unity names a model's parts after their names, which are unchanged, so the scene's references should hold. Worth a glance after the reimport: the A-frame should still stand upright with its street side facing the street.
- **Fonts.** `draw_cafe_textures.draw_aframe` (League Gothic, Yellowtail) wasn't run here. The delivered `CF_AFrame.png` is his own picture, re-chalked.
- **Triangle counts went up** on the condiment tray (2.5k), the lamp (2.2k) and the slept-in quilt (0.9k). All are small next to the hero devices.
- **The held cloth's far corner** now leads at the cursor, and the cloth covers a bit more of the phone below the cursor than the old wad did. Bench 8's "lens in sight" measure may move a little either way.
