# Builder B2: the art audit's Blender and texture fixes (24 Oct 2026)

You are the 3D Art & Blender lead. Read `/root/review24/builders-common.md` first, then `/root/review24/his-answers-2026-10-24.md`. Your copy: `/root/wt24/B`. Your progress log: `/root/review24/reports/B-progress.md`. Your report: `/root/review24/reports/B-report.md`.

## What happened
On 23 Oct four art reviewers looked at every model, texture and lit corner of the game in the labs' photos and wrote 97 findings (`/root/review23/decisions/art-items.json`, with pictures in `/root/review23/decisions/img/`, and the raw findings in `/root/review23/findings/art-R1..R4.md`). He answered "Yes, do that" to every finding in the game now, except a40 (leave the syrup labels). His note: "nothing else, i will give notes after this extensive session is complete". The table of all 75 with each recommended fix: `/root/review24/art-answers-table.md`.

## Yours: the findings whose fix is a model or a texture
| id | finding | the recommended fix (short) |
|---|---|---|
| a12 | The phone's insides (S2) | a green circuit board with a few chips, a labelled battery, two ribbon cables and a copper charging coil, in the same footprint (`hero_phone.py`) |
| a14 + a62 | The patio A-frame only half stands; its chalk is dim | rebuild as a true A (both sides from one hinge, both feet down, a chalk face each side); chalk near-white #E8E4DA with one amber or green line (contrast ≥ 4.5:1) |
| a20 | The coat and scarf on Grace's stand | delete the coat and the two scarf bars, keep the hat and stand |
| a22 | Grace asleep has no head | the back of a head in her hair colour on the left pillow; soften the quilt's sides |
| a23 | The brush's bristles | one tapered block with a few grooves; keep the `Bend` shape key and the origin at the working tip (`bench_tools.py`: the bench's brush animation depends on both) |
| a27 | The watch's small seconds hand | a small ring with 12 ticks under it, in the dial texture (`hero_watch.py`) |
| a29 | The bare ruler on Ace's desk mat | marks and numbers (look at the picture first: this is the steel ruler on Ace's desk, lower left in first person at the bench, not the repair mat's printed rulers) |
| a31 | The magnifier lamp's flat springs | round coil tubes, or delete them |
| a34 | The till's blank keys and screen | key labels and a price on the screen |
| a37 | The condiment tray's plain blocks | sugar packets in a caddy, a stack of lids, a labelled shaker |
| a41 + a42 | The wrapped repairs on the pickup row; the game controller in its bag | the lamp's paper twisted shut with shade and base showing, bag tops crumpled; the controller bigger in its bag |
| a44 | Plain grey box seen from the stools | a back panel with a rim and a label on Ace's small-parts drawers |
| a47 | Every customer looks cross | a softer brow and eye version of the shared face texture (it's Synty's: never overwrite a Synty file; make a new texture and say which materials should use it) |
| a48 | The plain napkin boxes on tables | rounded edges, a chrome or wood body, about 20 % smaller |
| a49 | The stiff tote bags on the floor | a slumped canvas bag with an opening and a fold |
| a50 | The laptop screens look dead | a two-tone screen texture: a dark title bar over a page |
| a53 | The spiky olive trees by the door | delete the spiky leaf cards so the soft balls read as a trimmed tree |
| a55 | A shading seam on the back door | mark the frame's edges sharp (or set the smoothing angle in the import settings: then it's a Unity step, say so) |
| a72 | Grace's empty photo frames | one small painted snapshot (a sky and two or three people) on the photo material |
| a73 | The reunion cups look like a tube | the sleeve with 13 rings and a 0.85 narrow radius (not 7 and 0.94); the cup box's sleeves improve too |
| a74 | Grace's doors look like flat slabs | sink the panels about 8 mm, or add a darker inner line |

Not yours:
- a56 (the banquette's back) goes to the furniture builder;
- a28 (the grey sheet on Grace's camera) is "leave it";
- the Unity-side findings go to the Unity art builder after you.

## How
1. **For each finding, find what builds the model.** Usually that's a script in `Tools/Blender/` (its README lists them: the hero devices, the bench tools, the café kit `LI_*`, Grace's house `GH_*`, the signs, the props). The model that's in the game now is in `/root/ref24/Assets/Art/Models/` (FBX) with its `.meta` (import settings). The scene and prefabs say which model a thing uses (grep `/root/ref24`).
2. **Look before you change.** Open the finding's pictures (`/root/review23/decisions/img/<id>-*.jpg`) and render the current model yourself the way the game sees it. Several reviewers disagreed on a few (a41, a49, a62: "look first"); where a reviewer said it reads fine, decide and say why.
3. **Change the script and rebuild the model.**
   - Use the same file name and path, so Unity reimports it in place and nothing that references it breaks.
   - Keep everything that code relies on: object and child names, empties, shape keys, origins and pivots, material slot names and order, the real-size scale (`hero_lib.scale_build`), and the audit (`fixit_blender.py`: no part floating more than 2 mm).
   - Grep `Assets/Scripts` and `Assets/Editor` for every name you might touch. The bench's tools, devices and mat are used by name by the bench code and the labs (`BenchStage`, `BenchTools`, `ToolAttach`, the hero device prefabs' `moves` in `devices.json`).
4. **Textures** are drawn in Python (Pillow), as the repo's `draw_*_textures.py` and `bench_stage_textures.py` do, at the sizes the models already use. A finding that needs a texture assigned to a material that doesn't have one yet needs a Unity step: write it as one editor step file of your own (`Assets/Editor/ArtFixBlenderSteps.cs`, a menu item under `Fixit Fidget/Art fixes/...`, idempotent, with Undo, a report and a DONE line) that assigns your new textures and materials by name. Grep `/root/ref24` for the material and object names.
5. **Render a check picture of every fixed piece**, before and after, from the angle the finding was seen at (the game's overhead camera is in the README: 43°, 34 m, a 44° lens; the bench's close-up 58°, 0.58 m, 40°). Put them in `/root/review24/designs/B-renders/<id>-before.png`, `<id>-after.png`, and a contact sheet of all of them. Look at every one yourself and fix what reads wrong: he will judge them by eye, and so will the reviewer.

## Deliverables in your copy
- The changed scripts.
- The rebuilt FBX files at their real paths (`Assets/Art/Models/...`) and textures (`Assets/Art/Textures/...` or `Assets/Data/Resources/...`), so the integrator can copy them to his Mac.
- The `.blend` sources where the README says they're kept (`BlenderSource/`).
- `Assets/Editor/ArtFixBlenderSteps.cs` if needed.

## Report
For each finding:
- the file(s) changed;
- what you did, and why if you chose differently from the reviewer;
- the before and after renders;
- what keeps the code safe (names and keys kept);
- whether it needs a Unity step.

Then:
- the list of files the integrator copies to the Mac, with their Mac paths;
- the steps to run there, and how to check each in the game (which lab photo shows it).
