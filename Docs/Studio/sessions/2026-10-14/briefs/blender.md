# Brief — 3D Art & Blender (Opus): the bench's new pieces and the café's props, part 1

## Read first
1. `/root/cafe/brief/common.md` (all), then `/root/cafe/brief/wave1b-addendum.md`.
2. Your role file `/root/cafe/repo/Docs/Studio/agents/blender-3d.md`; the Bible §2 (purpose rule, real life first, brands fictional) and §6 (conventions: Unity (x, y, z) = Blender (−x, z, −y); a tool's origin is its working tip; names, origins and frames are contracts).
3. **Your build sheet: `/root/cafe/specs/art-bench-and-cafe.md`** (Art Direction's briefs, coordinates, colours, budgets and its ordered list for you). Also `/root/cafe/specs/uiux-checklist-v2.md` §5.3 (the icons' brief).
4. `/root/cafe/repo/Tools/Blender/README.md`, `hero_lib.py`, `fixit_blender.py`, `bench_tools.py`, `export_bench_tools.py`, `review_bench_tools.py`, `cafe_props.py`, `cafe_livedin.py`, `export_cafe_livedin.py`, `unity_mesh.py`, and the Unity import steps that read your exports (`/root/cafe/repo/Assets/Editor/BlenderKitSteps.cs`, `CafeLivedInSteps.cs`, `Playtest3Session4Steps.cs` for the bench stage), plus the manifests under `/root/cafe/repo/Assets/Art/Models/*/`.

## Setup
- Blender as a Python module: `pip install --break-system-packages bpy==5.2.2` (Python 3.13; the README says the scripts were first run this way). If the install fails, say so and stop at scripts plus a description.
- The fonts in `Tools/Blender/fonts/` are Git LFS pointers in this clone. Try `cd /root/cafe/repo && git lfs pull --include "Tools/Blender/fonts/*"`; if that fails, find the same OFL fonts (League Gothic, Yellowtail) as real TTF files from npm (for example an `@expo-google-fonts/*` package) and use those, with their licence files.
- **Work in a copy:** `cp -r /root/cafe/repo/Tools/Blender /root/cafe/work/blender/Tools/` and run from there. Never edit `/root/cafe/repo`.

## Build now (Art's ordered list, the items that don't wait for the Technical Director's scale decision)
1. **The silicone repair mat** with its grid/ruler texture, moulded screw cups, pad and well, the empties as named in the Art spec, and (per Audio's note) a small steel disc in the floor of each screw cup.
2. **The tool rest and the scrap tray.**
3. **The cloth's states** (folded on the rest; held as a wad; `Press` and `Bunch` shape keys) and the brush's `Bend` key. Keep `BT_Cloth` / `BT_Brush` names and the origin-at-the-working-tip contract.
4. **The eight generic icons** for the checklist card (UI/UX §5.3: screwdriver, pry, tweezers, brush, cloth, screw, newpart, cover) as 256 × 256 transparent PNGs, with a new `render_bench_icons.py`, and one contact sheet at 36 and 56 px on the card's cream `#F7F3EE`.
5. **Close-up review renders** of the new bench pieces from the close-up camera (58°, 0.58 m, FOV 40°, the rig turned as Art says), with today's phone, watch and camera meshes placed on the mat where Art's per-device table puts them, so his eye can judge the mat's colour and layout.
6. **The café kits** from Art's Part B (the repair-side kit, the café-side kit, the story textures) and **overhead review renders** from the real overhead (43°, 34 m, FOV 44°) — with the props on a neutral stand-in of the room if you can't build the room, and a close-up turntable of each.

**Not now** (they wait for the Technical Director's one-scale method): the camera's strap, body and clean shutter mechanism; the screws and hole decal; any device-specific icon.

## Deliver
- Complete scripts (new or changed) under `/root/cafe/work/blender/Tools/Blender/`, each runnable headless.
- Exports and manifests under `/root/cafe/work/blender/Assets/Art/Models/<Kit>/` mirroring where the Unity steps will read them (FBX per kit, the manifest JSON with sizes, triangles, children, empties, materials with colour/smoothness/metallic 0); the audit's `0 problem(s)`.
- Renders under `/root/cafe/work/blender/renders/` and the icons under `/root/cafe/work/blender/Assets/Data/Resources/UI/BenchIcons/`.
- A report at `/root/cafe/specs/blender-wave1.md` (Bible §5 contract): what you built, triangle counts, the renders' paths with one line each on what they show, the cut list, decisions his to overrule, and **what the Unity import steps must do** with each export (which prefab or scene object each mesh goes to, by name), so the Producer can write those steps.

Spend about 90–120 minutes. Look at every render yourself with the Read tool before you report, and fix what's wrong once.
