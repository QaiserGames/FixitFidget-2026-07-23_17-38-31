# Builder K report: the drinks machine's buttons, built (26 Oct 2026)

## The files (`/root/wt24/K`, branch work-K, 3 commits)
- `Tools/Blender/drink_buttons.py`, `export_drink_buttons.py`, `review_drink_buttons.py`: the caps built from code, exported, rendered on the real machine from the close-up's pose.
- `Assets/Art/Models/DrinkButtons/DB_Cap_<Name>.fbx` (6, no .meta) + `DB_DrinkButtons.json`; `BlenderSource/DrinkButtons_v1.blend`.
- `Assets/Editor/DrinkButtonsSteps.cs` (new): the step and the lab's menu. `Assets/Scripts/Diagnostics/DrinkButtonsCheck.cs` (new): the lab.
- `BeveragePaddleMotion.cs`, `BeverageStationSetup.cs`: comments only. `Tools/Blender/README.md`: rows.
- Renders and README: `/root/review24/designs/K/` (start with `K-before-after-1to1.png`).

No lab measured the old labels: Stations, Dispenser frame and Aim help measure the hit boxes, untouched.

## What it is
Per drink one cap (92×98 mm, 30 mm proud, rounded) in the machine's own `BF_<Drink>` colour (the tray chips'), a cream 72 mm label, and the drink's picture in 3D relief: the ticket's icon (`draw_button_icons.drink`) rebuilt from its own pixel coordinates at 0.54 mm/px, each part on an ink plate that is its outline. Built in the paddle's frame, it parents under `<Name> ceramic paddle` with no offset: press, slots, pour, hit boxes, c2 targets and prompt keep working. Press 9 mm (was 6). No words (see Decisions).

## On his Mac, in order
1. Copy `Assets/Art/Models/DrinkButtons/`; let Unity import.
2. Café scene open: **Fixit Fidget › Drinks › Buttons 1 - The machine's new buttons (scene and prefab, with Undo)**. Import settings: scale 1, file units, no axis bake, no animation, materials by name (six BF_ existing; nine DB_ made in `Assets/Art/Materials/BeverageDispenserV2`, URP Lit, smoothness .45). No textures, so no sRGB/compression question. Pass: `[Drink buttons] Buttons 1: DONE, 45 change(s), 51 check(s) passed. Save the scene.` plus two photos in `Logs/Drinks/buttons-1-<time>/`. Second run: `DONE, 0 change(s)`.
3. **Fixit Fidget › Drinks › Lab - The machine's buttons (drives itself)** (about a minute). Pass: `[Drink buttons] All N checks passed.`, 13 photos in `Logs/Drinks/drink-buttons-<time>/`: the whole machine, each button at rest and pressed-and-pouring (Latte with a cup). Checked: six different picture meshes, six colours, cap ≥80 px and picture ≥42 px at 1080p, the press and return, the line naming the drink, every machine/prompt word ≥26 px.
4. Re-run Stations first person, Dispenser frame, Aim help: unchanged.

## Decisions his to overrule
- **No words on the buttons.** A 26 px em is 26 mm at 0.94 m; "Hot Chocolate" in Baloo 2 Bold ≈180 mm, "Americano" 130, "Espresso" 110, on a 92 mm button at 158 mm pitch. One size for six can't fit; the prompt line carries the word (34 px).
- The machine's six colours kept (Espresso's slate is dull; it matches its chip).
- A cream label under every picture (a brown mug on a brown cap vanishes); the dark backing is the socket.
- Two-segment bevel on the cap's front edges (house rule: one): the part stared at from 0.9 m.
- Old tile, grip and "Printed" labels hidden, not deleted.

## Outside my files (optional wording, not made)
- `BeverageStation.cs:37` comment "the paddles print theirs" → "the paddles show the drink's picture"; `:598` and `DayOneGuideUI.cs:626-627` "{drink} paddle" → "{drink} button"; `BeverageControl.cs:20` "the named paddle" → "its picture button".

## Risks
- Nothing compiled here; csparse clean on all four C# files, members grepped. Unsure only of `PrefabUtility.InstantiatePrefab(Object, Scene)` and whether Unity merges the single FBX root node (renderers are found by name either way).
- Orientation relies on the house mapping (Blender −Y front → Unity +Z; the dispenser turned 180° under its root); the step's checks (cap on its hit box, front at 160–185 mm) undo it if wrong.
- URP may light the caps darker than Cycles; the pictures' contrast doesn't depend on it. The lab's 42 px picture bar is close for Espresso (46 mm).
