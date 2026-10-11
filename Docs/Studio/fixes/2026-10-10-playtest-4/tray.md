*The 3D Art & Blender (the intake tray) lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

## Lead 6 (3D Art & Blender): the intake tray. Report

### 1. What I did
**The finding** (first hour, Counter, L): *"The intake shelf has no object of its own: devices lie on the bare stone in front of the till (slots at x −1.56…1.56, z 13.65), but the card says 'pick up … from the intake shelf'."*

- **The model.** I built `CK_IntakeTray` by script: a chestnut tray lined with felt in the brand green. It is walled on the customers' side and the two ends, and open on Ace's side. Size 0.360 × 0.020 × 0.305 m (across, up, deep), 204 triangles, 5 islands. The export audit found **0 problems**: nothing floating, no faces without area, nothing below the bottom. I re-imported the FBX and it measures right in Unity axes: x ±0.18, y 0–0.02, z −0.138…+0.167. Its two materials are `CC_Wood_Counter` and `CF_Felt`, and it carries metre box UVs.
- **The Unity step** puts one tray under each of the 5 intake slots, as that slot's child. Each tray gets a small runtime component, `IntakeTray`, that hides it until its slot can be used.
- **The brief assumed something that isn't so.** It expected an un-bought slot to be switched off. It isn't: `ItemSlotArea` keeps all 5 slot transforms active and only counts the first `baseSlots + ExtraShelfSlots` (3 + Shelf Space). So the hiding has to be done in code, and that is what `IntakeTray` does.
- **A new lab** checks the hiding in Play: it raises Shelf Space from level 0 to level 2 and back and counts the trays shown.
- Not done: no text texture and no `.mat` file. The step makes `CF_Felt.mat` from the manifest.

### 2. Decisions and reasoning
**Measured references:**
- Otto Frei's watch-service counter tray, sold "for bench work and retail service counters": 14¾ × 8¼ × 1 in (375 × 210 × 25 mm), with a velvet pad that keeps items from sliding. https://www.ottofrei.com/products/jewelry-tray-black-velvet-pad-full-size-14-3-4x8-1-4-1in-watch-bracelet-sizing
- Carver's solid-wood front-loading desk in-tray: 13.62 × 10.62 × 2.12 in (346 × 270 × 54 mm), with a "cutout in the front-loading tray". https://www.officecrave.com/carver-cw07212.html

**Sizes come from the game's numbers.** I measured them from the FBX vertices and the prefabs (`analysis/envelope.py`):
- A device lands at slot + restHeight, turned up to ±12° and moved up to ±2 cm (`PlacementJitter`, `CustomerBrain` 12 / 0.02).
- Worst cases, in the slot's own frame (+z toward the queue):
  - Grace's camera body: x ±0.160, z −0.135…+0.125.
  - Pocket watch: x ±0.095, z −0.095…+0.149.
  - Phones: x ±0.069, z ±0.096.
- The felt bed is x ±0.165 and z −0.133…+0.152, so every device stays inside the rims at its worst landing.
- Register v4 stands 0.146 m behind slot 2. The tray's open side ends 8 mm short of it.

**Height: no slot moves.**
- The stone is at 1.095 and the slots at 1.100. The felt's top is 8 mm over the stone, at 1.103.
- Gaps between each device's bottom and the felt:
  - hero phone: +1.2 mm
  - plain phone (PhoneJob): +1.0 mm
  - Grace's camera: 0.0 mm
- The camera's strap hangs at 30 mm, so it passes over the 20 mm rims.

**Cut list** (the purpose rule):
- No back rim: it would stand against the register, and Ace reaches in from that side.
- No brass "IN" plate or slot numbers: a plate is about 2 px from the overhead camera, and numbers in the world are the HUD's job.
- No ticket clip: the job's colour tag already ties a device to its ticket.
- No feet or corner brasses.

**What the renders show** (I looked at every one; `renders/0-contact-sheet.png`):
- From the 1080p game camera, each tray is about 14 × 9 px. Before, the devices were 2–5 px specks on pale stone; now each sits on a green patch you can see.
- In first person the trays read as wooden trays.
- The close views show the felt, the open end with its ramps, and clean corner joints.
- I fixed two things after the first renders: dark notches at the front corners (the rims now butt into each other), and misplaced screws in the render's stand-in watch.

**Decisions his to overrule:**
1. One tray per slot, not one long tray. Un-bought slots show nothing, and buying Shelf Space visibly adds a tray.
2. Open toward Ace, walled toward the customers: an in-tray.
3. The felt is brand green #2E7D5B in a new matte `CF_Felt` (smoothness 0.08). The existing `DC_Sticker_Green` (0.45) would catch the pendants' highlights and read as plastic. Green ties the tray to the bench mat and the REPAIRS sign.
4. The wood is chestnut (`CC_Wood_Counter` → "Cafe grain"), the room's one wood.
5. Size 0.36 × 0.305 × 0.02 m, set by Grace's camera at its worst landing. It sits between the two references.
6. The rims are 20 mm high (a jeweller's tray is 25 mm). Lower keeps the devices in view from Ace's eye.
7. No collider. Every pick in `PlayerInteractor` is a ray or an overlap against colliders, so the device keeps E, the crosshair and the aim help.
8. Not added to `CafeViewMode.overheadFixtures` (it must show from above) and not to `CafeRedesignRecord`. The trays stay on the counter at night.
9. Two new runtime files in my area that the brief didn't list: `IntakeTray.cs` and the lab `Diagnostics/IntakeTrayCheck.cs`.
10. `IntakeTray` keeps its own copy of the shelf's `baseSlots`, because that field is private. Step 0 and the lab both confirm the copy matches.

### 3. Files (all under /home/claude/cafe/s7/tray/)
- `Tools/Blender/cafe_intake.py`: builds the tray. Holds the references, the numbers, the purpose and cut list, and the device envelope.
- `Tools/Blender/export_cafe_intake.py`: writes the FBX, the manifest and the `.blend`, and prints the problem count.
- `Tools/Blender/review_cafe_intake.py`: the review renders, before and after. Its default output is `Logs/Blender/cafe_intake`.
- `Assets/Art/Models/CafeKit/CK_IntakeTray.fbx`: the tray (md5 4acb94a9…).
- `Assets/Art/Models/CafeKit/cafe_intake.json`: the manifest in the kit's format, plus felt_top, rim_top, felt_bed, footprint, open_side, device_envelope and the CF_Felt definition.
- `BlenderSource/CafeIntake_v1.blend`: the source file (goes in LFS).
- `Assets/Scripts/IntakeTray.cs` (new, runtime): hides the tray until its slot can be used, and offers `IntakeTray.Nearest(point)` for a marker.
- `Assets/Scripts/Diagnostics/IntakeTrayCheck.cs` (new lab, built on `PlayLab`).
- `Assets/Editor/Playtest4TraySteps.cs`: menus Tray 0, 1 and 2.
- `renders/`: 9 review renders plus `0-contact-sheet.png`.
- Not for the repo: `analysis/` (scene, prefab and envelope scripts; `compile-check/` with stubs and `compile.sh`) and `work/` (copies of the library and device files used for the renders).

### 4. Unity steps, in order
1. Copy the 9 repo files above. Then **Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)**. It should compile with no errors.
2. **Fixit Fidget › Playtest 4 › Tray › 0 - Check first (read-only)**. Report: `Logs/Playtest4/tray-0-last.txt`. Expect:
   - 5 slots at y 1.100, the stone 5.0 mm under each, 3 open from Day 1.
   - Each device's lines; against the felt: PhoneRepair +1.2 mm, PhoneJob +1.0, GraceReunionCamera 0.0, PocketWatch −14.7.
   - "import settings will be set" and "CF_Felt.mat will be made".
   - READ-ONLY: 0 failed.
3. **Fixit Fidget › Playtest 4 › Tray › 1 - Put a tray in every intake slot (scene)**. Expect:
   - "DONE"; CF_Felt.mat made; the import set.
   - 5 trays named `CK_IntakeTray (slot N)` under `IntakeShelf/Shelf_N`, each with bottom 1.095, rims 1.115, felt 1.103.
   - No colliders, nothing clashing, slot 2's tray about 8 mm from the register, every gameplay transform unmoved.
   - A FLAG line for the pocket watch (see §6).
   - Photos in `Logs/Playtest4/tray-1-photos-<time>/`: `1-before-1…5` and `2-after-1…6`. They show the hero phone, watch and camera on slots 0–2 for the photos only; those copies are never saved.
   - Look at the photos, then save the scene. A second run should say "NOTHING TO CHANGE: already done".

### 5. Labs, in order
1. **Fixit Fidget › Playtest 4 › Tray › 2 - The trays in play: Shelf Space shows its trays (lab, drives itself)**. About 15 s, then stop Play. Pass:
   - 5 trays, none with a collider.
   - The base count matches `ItemSlotArea.baseSlots` (3).
   - Shelf Space level 0, 1, 2 shows 3, 4, 5 trays, the right slots each time.
   - Back to level 0 shows 3.
   - Photos in `Logs/Playtest4Tray/trays-in-play-*`.
2. **Fixit Fidget › Playtest › Stations as reach - play check, overhead (lab, drives itself)**: 53/53. Then **… first person (lab, drives itself)**: 52/52. The line "the device on the intake shelf is what A picks up" must still pass.
3. **Fixit Fidget › Bench › Bench 3 - The bench - play check (lab, drives itself)**: 128/128, unchanged. The trays are not in the close-up.
4. **Fixit Fidget › Checks › Neighborhood refresh gameplay guards**: "PASS — 24 seats …", with layout and circulation PASS (no collider was added, so the routes are untouched).

Labs that could break: none by design. The no-missing-script guard needs `IntakeTray.cs` to compile.

### 6. Needs another lead's file
- **Bench lead** (it changes the watch on the bench too): `Assets/AssetsPrefabs/PocketWatch.prefab`, its JobBase `restHeight` from **0.01 to 0.025**. Its back's screw heads sit 21.7 mm under its anchor:
  - Today it sinks 6.7 mm into the bare stone.
  - On the tray it sinks 14.7 mm (the felt is 8 mm higher). It still reads as resting in the renders, because 19.5 mm of the case stays above the felt.
  - At 0.025 it rests 0.3 mm over the felt.
  - Done through a step: `Playtest4Shared.EditPrefab(run, "Assets/AssetsPrefabs/PocketWatch.prefab", root => { var so = new SerializedObject(root.GetComponent<JobBase>()); so.FindProperty("restHeight").floatValue = 0.025f; so.ApplyModifiedPropertiesWithoutUndo(); }, root => Mathf.Approximately(root.GetComponent<JobBase>().restHeight, 0.025f));`
  - My step works either way: today it prints a FLAG line, and after the fix it reports "rests on the felt".
- **UI lead** (the overhead pin "the intake tray"): don't pin over `IntakeShelf.Instance`. It sits at x −1.21, over the hidden upgrade slots. Pin over the device (`customer.ActiveJob.transform`), or use `IntakeTray.Nearest(job.transform.position)`. The words "the intake tray" match the object.

### 7. Open questions and flags
- **His call:** a small brass "IN" plate on the tray's back rim, if he wants a word on it (about 10 minutes of work).
- **Flag:** the device's rest heights don't match their meshes (+3…+4 mm for most, −11.7 mm for the watch). The tray absorbs this without moving any slot.
- **Flag:** at its worst landing, the camera body can overhang the felt's open edge by up to 2.3 mm. It then sits on the wooden lip and never touches a rim.
- **Flag:** I checked device footprints with mesh boxes. For the round watch that box test is too strict, so the step reports it as information; Blender measured it inside, vertex by vertex.
- **Flag:** I ran a stub compile of all three C# files together with `Playtest4Shared.cs` (`analysis/compile-check`): 0 errors. The stubs carry the real signatures, but this is not Unity's own compile.
- No new mechanic. The tray shows an existing rule (the slots and Shelf Space). No lock is touched.

### 8. Syntax check
```
OK    Assets/Scripts/IntakeTray.cs
OK    Assets/Scripts/Diagnostics/IntakeTrayCheck.cs
OK    Assets/Editor/Playtest4TraySteps.cs
exit 0
```