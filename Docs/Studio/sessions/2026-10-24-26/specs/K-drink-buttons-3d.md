# Builder K: the drinks machine's buttons, built in the engine (26 Oct 2026)

You are the Art Director and senior Unity/Blender lead for Fix It Fiasco, a cozy-but-chaotic 3D café-and-repair game (Unity 6000.5.2f1, URP). The developer judges by playing and by eye. He is away from his computer, and he will test this once everything is in place.

## His words
- **24 Oct, art finding a10:** the six labels auto-size to different sizes, and HOT CHOCOLATE is the smallest.
- **26 Oct:** "i am really not a fan of the way the buttons look, like lets be honest in a sense of a cozy chaotic game, its just words on a square in different colors, it doesn't give aces aesthetics you know."
- **26 Oct, on the three drawn redesigns (brass and enamel; Ace's fix with paddles and stickers; taps and chalk):** "they are all so terrible, it doesn't look like a game, more like ai art slop".

So no drawn mockups this time. Build the buttons on the real machine, in the café's light, and check them in the game's own photos.

## Read first
- `/root/review24/builders-common.md`: the house rules.
- `/root/review24/designs/D2/README.md`: the rejected round. Learn from it; don't repeat it. Note its facts:
  - the machine is `Assets/Art/Models/BeverageDispenserV2.fbx`;
  - `Assets/Editor/BeverageStationSetup.cs` has `fitted = Min(.082/width, .043/height)`, the per-label auto-size behind a10.
- `/root/review24/designs/R4/start-research.md` §3: how Overcooked's objects read. Chunky, clear silhouettes, bold colour, nothing fussy.
- The code: grep `BeverageStation`, `BeveragePaddleMotion`, `BeverageSlot`, `BeverageControl`, `DrinkLiquidVisual`. The drink icons are in `Tools/UI/draw_button_icons.py` and on the TMP sprite sheet (`drink_*`).
- `/root/ref26`: his Mac's current scene, prefabs and assets, read-only (builders-common says ref24; ref26 is newer and wins). The machine's model is `/root/ref26/Assets/Art/Models/BeverageDispenserV2.fbx`; its materials are in `/root/ref26/Assets/BeveragePrototype/`.
- How it looks today, in the game's own lab photos from his Mac (look at them first):
  - first person at the machine: `/mnt/user-data/uploads/FixitFidget/Logs/Stations/stations-first-person-2026-10-26_065921/drink-2-at-the-dispenser.jpg`, `drink-2a-the-whole-dispenser.jpg` and `drink-3-the-drink-made.jpg`;
  - overhead: `/mnt/user-data/uploads/FixitFidget/Logs/Stations/stations-overhead-2026-10-26_113545/drink-2-at-the-dispenser.jpg`.

  Today it has square coloured paddles with words in three different sizes ("COFFEE", a big "TEA", a tiny "HOT CHOCOLATE"), on a dark fascia under an "ACE'S" plate.
- **Your files:**
  - `BeveragePaddleMotion.cs`;
  - `Assets/Editor/BeverageStationSetup.cs`;
  - `StationsCheck.cs` and the other drink-machine labs and checks;
  - new files (your step, your lab, your Blender script under `Tools/Blender/`).
- **Not yours (others are building in them right now):**
  - the HUD, the phone and the UI words files: builder R;
  - the bench's logic and `PadInput`: builder N;
  - the furniture and the Day labs: builder Y.

  If one must change, write the exact edit in your report.

## The bar: it must look like a shipped cozy game's object, not a diagram
- Think of how the best-loved cozy cooking and shop games make machines. Overcooked's dispensers, PlateUp!'s appliances and Good Pizza's toppings are all chunky, toy-like, readable in silhouette, with a bold colour per item and a big pictogram.
- Ace is a repair man in a small café. His machine can look lovingly kept, maybe a little modified. Keep it clean and charming, never messy.
- **Readable for his girlfriend:** she is dyslexic and plays on a PlayStation pad.
  - Each drink is told apart by its picture first, then colour.
  - A word is optional, and if there is one it is the same size on all six, at least 26 px on screen at 1080p in first person at the machine.
- **Keep the machine's working parts:** the pour, the paddle press motion, the slots, the hit areas, the aim help's bigger targets (c2), and the prompt line ("Pour Latte").

## What to make
- **The buttons as 3D models, in Blender** (bpy is available here; see how `Tools/Blender` scripts build and export, and copy their conventions). Their caps carry the drink pictures as a texture or decal, drawn from the same pictures the game's icons use.
- **The materials** in the café's palette (`Tools/Blender/fixit_blender.py`).
- **A Unity editor step, in a new file**: "Fixit Fidget › Drinks › Buttons 1 - The machine's new buttons (scene and prefab, with Undo)". It:
  - fits the new buttons under the existing press transforms, so the motion and hit areas keep working;
  - retires the old word labels;
  - ends with a DONE line;
  - takes photos from the first-person view at the machine and from the overhead view.
- **Keep the existing labs passing:** grep the Stations lab, the Dispenser frame check and the aim help. If a check measured the old labels, update it to the new buttons without weakening it.
- **A lab**, "Fixit Fidget › Drinks › Lab - The machine's buttons (drives itself)", that photographs:
  - each button at rest, pressed and pouring, in first person;
  - the whole machine from the counter.
  It checks that every drink's button is told apart by its picture and that any words meet the floor. Its last line is "[Drink buttons] All N checks passed." or "N of M checks FAILED. <folder>".

## Look at your own work
- Render the models in Blender with lighting close to the café's (warm, from the windows and the pendants), at the first-person distance.
- Look at each render yourself, and redo anything that reads as a diagram, a sticker sheet or clip art.
- Save the renders and a short README (what, why, the references you followed) to `/root/review24/designs/K/`.

## Rules
- Your copy is `/root/wt24/K` (branch work-K).
- Mark edits `// 26 Oct 2026 (Drink buttons)`.
- Run `python3 /root/tools/csparse.py` on every C# file, and grep the definition of every member you call. Nothing compiles here.
- A model change reaches his Mac as files: the FBX and textures you export go under `Assets/Art/` in your copy, with no `.meta` (Unity makes them). Say in your report which import settings your Unity step sets (e.g. the texture's sRGB, its compression, the model's scale).
- **Be economical:** he asked us to preserve usage. Grep first, read only the ranges you need, and keep renders few and purposeful. Keep a short progress log at `/root/review24/reports/K-progress.md`.
- Commit in your copy (this wins over builders-common's "commit nothing").
- Don't use any remote-device or Mac tools, and don't spawn agents.
- Report to `/root/review24/reports/K-report.md`, under 600 words: the files; the Mac steps and labs in order; what a pass looks like; the decisions he may overrule; the risks.
