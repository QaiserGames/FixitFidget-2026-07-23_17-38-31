# Imports builder: report (14 Oct 2026, wave 2)

Worktree `/root/cafe/build/imports`, branch `b-imports`, 7 commits on `431f081`. All eight changed `.cs` files pass `cs_syntax_check.py`. Nothing has been compiled or run in Unity.

## 1. What changed

**Code**
- `Assets/Editor/BlenderKitSteps.cs` (changed): **Bench tools 1** now:
  - imports blend shapes for any piece whose manifest lists `blend_shapes_order` (BT_ClothHeld, BT_Brush), with normals Import, and makes those meshes readable;
  - counts `SkinnedMeshRenderer`s as well as `MeshFilter`s in its checks;
  - checks each key's name, order and largest vertex move;
  - checks each `Seat_*` against the manifest's `quaternion_xyzw`, and that empties carry no renderer;
  - takes two more photos: the rest with each tool on its seat, and the keys at 0 and 100.

  `Run` gains an optional materials maker and is now internal, along with `Configure`, `MeshesOf`, `Studio`, `LogFolder`, `Transformed` and `V`.
- `Assets/Editor/BenchStageImportSteps.cs` (new): **Bench stage 1** (the mat through `Run`, its six BM_ materials made with `CafeLivedInSteps.Define`) and **Bench icons 1** (UI sprites), plus `BenchIconImport`, a postprocessor that makes anything imported into `UI/BenchIcons/` a sprite.
- `Assets/Editor/CafeDressingSteps.cs` (new): **Café dressing 1**: Import, Dress (scene), Undo, and Check from above (read-only, T21).
- `Assets/Editor/CafeLivedInSteps.cs` (changed): Kit 1's helpers made `internal` with no behaviour change (`Define`, `ConfigureTexture`, `ConfigureModel`, `ProjectMaterials`, `KitOneMaterials`, `Json`, `Str`/`Num`/`TryVec`, `Scenery`, `Shrunk`, `WorldBox`, `Anchors`). New `Scenery.Along`, a ray against the drawn scene.
- `Assets/Editor/SoundSetup.cs` (changed): **Sound bank 1** (§6 import settings plus the fill from the manifest, §7.7) and **Sound bank 2** (the "Sound bank" check, §8 and T18). The existing check's bed rule now accepts Compressed In Memory up to 30.5 s, which is the room tone.
- `Assets/Scripts/SoundBank.cs` (changed): 15 new `Defaults()` entries (§7.6) using Audio's settings and `when` texts. Rewritten notes for `screw.drop`, `cover.clack`, `part.drop`, `cloth.squeak`, `screw.bounce` (retired) and `repair.fixed` (call 11). `C()` gains `volumeWobble`. Every cue in Audio's §3 table, every manifest cue and every cue the code plays now has an entry (131 in all).
- `Assets/Scripts/SoundPlayer.cs` (changed): `UsesFile(cue)` is the single rule that `Play` follows and the check reads. Adds `VoicesPlaying`.
- `Assets/Scripts/CafeSoundscape.cs` (changed): while `DayIntro.Playing`, the beds follow the intro camera's place (the hook Cinematics §4.1.10 asked for). `door.bell` and `cafe.room` needed no code; they play once the bank is filled.
- `.gitignore` (changed): `/Recordings/` (Audio §5, flag 7).
- `PlaceholderSounds.cs`: left unchanged on purpose (see §4).

**Files copied in** (binaries through LFS, as `.gitattributes` asks)
- Into `Assets/Art/Models/`:
  - `BenchStage/BM_RepairMat.fbx` and `bench_stage.json` (new);
  - `BenchTools/`: `BT_ClothHeld`, `BT_ToolRest`, `BT_ScrapTray` (new); the five tools replaced (their `.meta` files kept); `bench_tools.json` v3;
  - `CafeKit/`: eleven `LI_*.fbx` and `cafe_dressing.json`.
- Pictures: `Assets/Art/Textures/Bench/` (2 PNG), `Textures/Cafe/LI_*.png` (4), `Assets/Data/Resources/UI/BenchIcons/` (8).
- Sound: `Assets/Audio/CC0/` holds 96 WAVs, `manifest.json` and `licences/` (5 files).
- `BlenderSource/`: `BenchStage_v1`, `BenchTools_v4` and `CafeDressing_v1.blend`.
- `Tools/Blender/`:
  - 10 new scripts and 5 changed (`bench_tools`, `export_bench_tools`, `fixit_blender`, `hero_lib`, `README`);
  - fonts: only Inter (3) and Caveat-Bold, with their OFL texts. The repo's own LFS fonts (Yellowtail, League Gothic) are untouched.

**Commits:** `cf0bf2f` (files), `1f07b53` (steps 1, 2, 5), `f39f764` and `7525dc4` (café), `f18307e` (sound), `725db15` (README).

**Getting the binaries to the Mac.** Git holds LFS pointers only. The real bytes are in the worktree and in `/root/cafe/repo/.git/lfs/objects`. Copy these folders over before opening Unity:
- `Assets/Art/Models/{BenchStage,BenchTools,CafeKit}`
- `Assets/Art/Textures/{Bench,Cafe}`
- `Assets/Data/Resources/UI`
- `Assets/Audio/CC0`
- `BlenderSource/*_v1|v4.blend`
- `Tools/Blender/fonts/{Inter*,Caveat*}`

The five replaced tool FBXs must keep their existing `.meta` files, so the scene's tool instances keep their GUIDs.

## 2. The Producer's Unity steps, in order

None of these runs in Play mode; each refuses there.

1. **Compile.** The icons import as sprites on their own through `BenchIconImport`.
2. **Fixit Fidget › Bench › Bench tools 1 - Import and check the tools (the models only).**
   - Expect: `[Bench tools] Imported and checked.`, `8 pieces, 7618 triangles in all`, every line PASS.
   - BT_ClothHeld should show keys `Press, Bunch` and BT_Brush key `Bend`, each with a move of a few mm.
   - The seats should show "turned (275, 0, 0)", and Seat_Cloth (0, 270, 0).
   - Look at photos 4 (each tool on its seat) and 6 (the keys) in `Logs/Bench/bench-tools-*`.
   - The scene's `Tool_Brush` now draws with a SkinnedMeshRenderer (see patch 5.1).
3. **Bench › Bench stage 1 - Import and check the mat (the model only).**
   - Expect: `1 pieces, 2142 triangles`, ten empties "at (…)" within 1 mm, and the six BM_ materials (BM_Silicone "white under BM_Grid_color, tiled 20"; BM_Rulers up to 4096 px).
   - In the photo, each cup should carry its dark 3.8 mm disc.
4. **Bench › Bench icons 1 - Import the checklist's icons as sprites.** Expect `Already sprites…` and 8 PASS lines (`finds a 256 x 256 sprite`).
5. **Cafe redesign › Café dressing 1 - Import and check the pieces (the models only).** Expect: 11 pieces, about 12,510 triangles, 25 materials made (12 existing ones found by name), all PASS.
6. **Scene open** (`AcesCafeLayoutPlaytest`): **Café dressing 1 - Dress the café (scene).**
   - Expect: `DONE`, followed by `The scene is saved.`
   - In the survey, read each piece's "goes at … (on what is drawn under it …)" line and any `LEFT OUT:` reason.
   - Expected: S2 is brought about 4 cm out of the door and moved off the STAFF ONLY plate (see §4).
   - It is safe to run twice: the second run says "Already done".
   - Photos: `Logs/CafeRedesign/cafe-dressing-1-*` (10 before and after, including 2× crops from above) and the room's 14.
7. **Night › Night walk 2 - Rebuild the list of what is solid by night (Edit Mode)**, then save (as after Lived in 2).
8. **Café dressing 1 - Check from above (read-only).**
   - Expect PASS for each piece's "nothing passes through it" and "no collider" lines and for the bottles being off.
   - R1, R2, R4, R5, C1 and C2 should be ≥ 12 px. **C3 is expected to FAIL at about 25 × 9 px until the mug tree** (call 9), so the summary reads `CHECKED: 1 problem(s)`.
   - The close-up line should read "no dressing piece in its frame".
9. **Sound › Sound bank 1 - Import the CC0 sounds and fill the bank.** Expect `45 cue(s) filled … 35 cue(s) added … 96 file setting(s) changed`, assuming the Mac's bank matches `fab65d3`. Undo is available.
10. **Sound › Sound bank 2 - Check the bank against Audio's table (read-only).** Expect `PASS: all 45 of Audio's cues play their files (T18)`. The info line lists the five cues that still have only a placeholder, each with its reason.
11. **By hand** (Audio §6 step 8): Project Settings › Audio, set DSP Buffer Size to **Good latency** and Max Real Voices to **48**. Put the buffer back if the Mac crackles.
12. Save the project and commit the Mac's new `.meta` files, the materials and `Sound bank.asset`.

**Before step 9** he should hear Audio's 85 s preview reel, which is not in the repo: `/root/cafe/audio/out/preview/all-cues-at-suggested-volume.wav`.

## 3. Labs and checks

- **Bench tools 1, Bench stage 1, Bench icons 1, Café dressing 1 Import:** all lines PASS, as in §2.
- **Café dressing 1 Dress:** passes if the step is not rolled back. Its own checks run after the change (each piece where planned, no collider, nothing passes through, the bottles off, no anchor moved), and the layout and circulation guards must be no worse.
- **Check from above (T21):** ≥ 12 px for R1, R2, R4, R5, C1, C2 and C3. C3 waits for wave 2.
- **Sound bank 2 (T18):**
  - "T18: 45 of the 45 cues Audio supplied play their files";
  - "every bench verb has its cue with a file" (30 cues);
  - "the drinks never synthesize";
  - "the till (money.paid) plays its file";
  - the intro's kept cues play their files;
  - "every file imported as Audio §6 asks";
  - licences present; recordings ignored.
- **The bench lab and the clumsy hand** (cue timing, loops alive while pressed): owned by the bench builder. `SoundPlayer.VoicesPlaying` is there for the load check.

## 4. Deviations, and spec errors found

1. **Café dressing places are searched, not assumed** (Lived in 2's rule). This matters most for S2:
   - The STAFF ONLY plate sits on the back door's room face at z 17.898, read off the leaf's mesh (`CafeBarSteps.cs:1076-1080`).
   - The manifest puts the notice's back at z 17.94 (4 cm inside the leaf) and spans it over the plate at 1.36–1.70 m.
   - Hung pieces are therefore snapped to the flat face behind them (five rays, within 5 cm) and moved up or down in 5 cm steps until nothing passes through. Top and floor pieces settle on what is drawn under them (within 2 cm) and move up to 5 cm toward the room or sideways.
   - A piece with no good place is **left out and named**, and the rest still go in. I chose this over Lived in's all-or-nothing because his note is "look full".
2. **S2 is not parented to the door:** the door doesn't swing (`CafeBarSteps.cs:1089-1090`: "No hinge or animator"; going through is a blink). The door is never renamed.
3. **S1 may sit under the till** by design. The till (`'Register v4 (…)'`) is the one exception to "nothing passes through" for that piece.
4. **Four menu items, not one "Import and place":**
   - the import is an asset step with no scene undo, as Lived in 1 and 2 are split;
   - **the Dress step saves the scene** (builders' brief §3), even though the shared step's own report text says "Not saved";
   - the routes aren't re-baked (they bake from colliders, `AcesCafeLayoutSetup.cs:604`, and nothing here has one). The guards still run before and after.
5. **The 10-pixel check lives in the read-only check**, not as a rollback condition, because C3 is known to be 3 px short until wave 2.
6. **Keyed meshes are made readable**, so the step can measure each key. `BM_Rulers` keeps its full 4096 px (Kit 1's clamp caps at 2048).
7. **`hero_lib.py` copied too** (`scale_build`, `PART_FACES`). The Blender spec's §4.1 table omits it. It doesn't change any existing export.
8. **Audio §6 step 8 stays manual.** In 6000.5 I can't tell whether the dropdown writes `m_DSPBufferSize` or `m_RequestedDSPBufferSize` (`ProjectSettings/AudioManager.asset`), so I didn't script it.
9. **Sound bank 1 keeps numbers tuned by ear.** It fills a cue's numbers only when that cue's files are new; a re-run changes nothing.
10. **`PlaceholderSounds` unchanged.** A cue with files never reaches it (`UsesFile`). Its five remaining placeholder-only cues are listed by Sound bank 2: `screw.bounce` and `cloth.wipe` (retired), `part.off` (never played), `intro.grind`, and `intro.pour` (only without `SoftPour.wav`).
11. **Spec notes:**
    - Audio flag 7: `.gitignore` already ignored `/[Rr]ecordings/` (the Unity template's line). My line makes it explicit.
    - R1 is 0.325 m tall in the manifest, against Art's "all ≤ 0.25 m so the lower drawer row shows". Photo 05 will show whether it covers the drawers; this is for Art.
    - The Blender spec's §4.2 table orders R1's size as across × deep × up, while the manifest uses across × up × deep. The steps measure the mesh either way.

## 5. Patches for other owners

**5.1 uiux: `Assets/Scripts/BenchOutline.cs` (`Build`).** The brush and the held cloth now draw with a `SkinnedMeshRenderer`, so without this patch they get no hover outline (T5).

Old:
```csharp
        foreach (MeshRenderer source in target.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (source.GetComponent<BenchOutlineHull>() != null) continue;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
```
New:
```csharp
        foreach (Renderer source in target.GetComponentsInChildren<Renderer>(false))
        {
            if (source.GetComponent<BenchOutlineHull>() != null) continue;
            // The brush and the held cloth have keys since 14 Oct: Unity draws them with a SkinnedMeshRenderer, no MeshFilter.
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else if (source is MeshRenderer) { MeshFilter filter = source.GetComponent<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
            if (mesh == null) continue;
```
Then replace `Smoothed(filter.sharedMesh)` with `Smoothed(mesh)`, and `Mathf.Max(1, filter.sharedMesh.subMeshCount)` with `Mathf.Max(1, mesh.subMeshCount)`. The same applies to the new `BenchGlow` if it reads `MeshFilter`s.

**5.2 bench: `Assets/Editor/Playtest3Session4Steps.cs` (`LocalBounds`)**, so a re-run gives the brush its click box.

Old:
```csharp
        var renderers = root.GetComponentsInChildren<MeshFilter>(true);
        bool first = true;
        var b = new Bounds();
        foreach (MeshFilter f in renderers)
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
```
New:
```csharp
        bool first = true;
        var b = new Bounds();
        // MeshFilters and SkinnedMeshRenderers alike (the brush has its Bend key since 14 Oct)
        foreach (var (node, mesh, _) in BlenderKitSteps.MeshesOf(root.gameObject))
        {
            Transform f = node;
            Bounds mb = mesh.bounds;
```

**5.3 bench (note):** drive the keys with `SkinnedMeshRenderer.SetBlendShapeWeight(mesh.GetBlendShapeIndex("Press"|"Bunch"|"Bend"), 0..100)`. The stroke runs along the model's +X. The rest's seat poses come in as Euler (275, 0, 0), and Seat_Cloth as (0, 270, 0).

## 6. Risks

- **Compile:**
  - least sure: `ModelImporter.importBlendShapeNormals` (`BlenderKitSteps.cs`, in `Configure`). If it doesn't exist, delete that one line; only the keyed meshes' shading changes;
  - next: `Mesh.GetBlendShapeFrameVertices(…, 5 args)` and the `AudioImporterSampleSettings` members (`loadType`, `compressionFormat`, `quality`, `sampleRateSetting`). Preload and normalize go through `SerializedObject` by their serialized names, so they can't break the compile.
- **Unity behaviour:**
  - reimporting BT_Brush with keys swaps the scene's `Tool_Brush` renderer to skinned. Its ToolPickup and box collider stay; its hover outline needs patch 5.1;
  - Café dressing may leave a piece out. The reason is printed and the rest still go in;
  - the survey's mesh tests can take some seconds on the Mac.
- **At 15 fps:** nothing of mine runs per frame except `CafeSoundscape` reading `Camera.main` during the intro. The rub loops' fades are the existing `MoveTowards(dt/fade)`, which is frame-rate independent.

## 7. Open items

1. Everything above is unrun in Unity. The likeliest first-run issues are a refused or left-out café piece and the one importer property.
2. Wave 2 from the Blender lead:
   - the mug tree on `LI_BarMat`: copy it over the old file, rerun Café dressing 1's Import and Check from above, which should then pass C3;
   - `Assets/Data/Resources/Bench/BT_ScrewHole.png` (a default texture import works);
   - the `part_*`/`cover_*` icons, which the postprocessor makes sprites.
3. Audio §6 step 8 by hand; his audition of the preview reel.
4. For others to settle:
   - the style guide's line for `BM_Silicone #4F70A0` (Art);
   - Content's placeholder words on the four LI_ pictures, and a check that "Pennywhistle Mortgage Co." isn't a real lender.
