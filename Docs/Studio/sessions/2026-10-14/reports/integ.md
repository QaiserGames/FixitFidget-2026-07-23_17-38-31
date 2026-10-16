# Integration report: one branch for his Mac (14 Oct 2026)

*From the integrator, for the Producer.*

**The branch:**
- `integ` in the worktree `/root/cafe/build/integ`, started from `431f081`, now at **`cc16dca`**.
- It has 7 merge commits and 16 integration commits.
- The worktree is clean, and all 149 binaries in it hold their real bytes.
- Nothing was pushed. `/root/cafe/repo`'s own working tree was not touched; the only operation on the repo was `git worktree add`.

**What the cloud could prove:**
- **Syntax:** all 108 changed `.cs` files parse.
- **Type check:** the Roslyn harness shows **0 new errors**. That holds against `base_stub.txt`, and against the original tree re-checked with the extended stubs.
- **Console tests:** **12 of 12** projects pass.
- **Not proven:** nothing has run in Unity. Section 5 lists what only the Mac can prove.

**Beyond the brief:**
- The three code reviews arrived at 11:55, after the brief. They found:
  - one Blocker: the bench guard trips on every landing;
  - several Majors.
- I applied the Blocker and every Major that has a fix of a few lines. They are in one commit (`cc16dca`), so `git revert cc16dca` takes them out.
- I did not apply the Minors. §1.4 lists them.

---

## 1. The merge, the patches, the calls

### 1.1 The merge: no conflicts

| # | Branch @ tip | Merge commit | Paths changed |
|---|---|---|---|
| 1 | `b-bench` @ `cab304e` | `6817b7d` | 37 |
| 2 | `b-scale` @ `f457920` | `cbc5245` | 13 |
| 3 | `b-days` @ `d3ba458` | `5fb0259` | 19 |
| 4 | `b-npc` @ `1464a7b` | `915d08b` | 14 |
| 5 | `b-intro` @ `462aebf` | `fc929e0` | 12 |
| 6 | `b-imports` @ `7525dc4` | `48c46c7` | 172 |
| 7 | `b-uiux` @ `2e2b67d` | `5418288` | 18 |

- The seven branches change 285 paths, and no path is changed by two branches. So git had nothing to resolve, and the ownership table (contracts §10) was never needed.
- The merge order satisfied every "merge X first" note:
  - bench before scale and uiux;
  - npc before intro.
- In the type check, the merged tree had one real compile error (`NpcWave2Steps.cs:1010`, fixed under call 7). Every other new error was a Unity API missing from the harness's stubs (§4).

### 1.2 The cross-owner patches

| Patch | File(s) | How | Commit |
|---|---|---|---|
| npc 5.1 | `Assets/Scripts/CustomerSpawner.cs` | As written: the `firstNameSexes` field, and `PolygonNpcVisual.RegisterNameSexes(firstNames, firstNameSexes)` after `roster.Reset(…)` | `14047f3` |
| npc 5.2 | `Assets/Editor/Playtest4SystemsSteps.cs` | As written, at the merged lines (228 and 414) | `14047f3` |
| npc 5.3 | `Assets/Scripts/DayIntro.cs` | **Not applied.** `HoldTheWorld` already calls `PatronSpawner.SeatOpeningGuestsNow()` (line 627), one statement before it holds the spawner. | – |
| days §5 | `Assets/Editor/DialogueRuleChecks.cs` (2 edits), `Assets/Editor/NotebookPlayCheck.cs`, `Assets/Scripts/NightZero.cs`, `Assets/Scripts/Diagnostics/NightOneCheck.cs` (4 edits) | As written | `2103056` |
| imports 5.1 | `Assets/Scripts/BenchOutline.cs` | **Adapted** (details below the table) | `65dd701` |
| imports 5.2 | – | Verified: already in bench's `Playtest3Session4Steps.LocalBounds` (`bcda6e4`) | – |
| cine 1 | `Assets/Scripts/CafeSoundscape.cs` | One intro hook kept, `DayIntro.FilmingInsideCafe` (it exists after the merge): `bool inside = DayIntro.FilmingInsideCafe ?? (view != null && view.AceInsideCafe);`. Imports' `Camera.main` path is deleted. | `dd5d612` |
| cine 2 | `Assets/Editor/DayIntroMenu.cs` | "about a minute" → "about three minutes" | `dd5d612` |
| uiux A, B, D, E, F | `Diagnostics/Day1GuideCheck.cs`, `Editor/DayOneOnboardingChecks.cs`, `Diagnostics/BenchViewProbe.cs`, `Diagnostics/BenchLab.cs`, `RecapPhone.cs` (the Vibration row) | As written | `7101f00` |
| uiux C | `Assets/Scripts/StationInteractable.cs` | Applied with call 22 (below the table) | `f0873e4` |
| bench 5.1 | `Assets/Editor/GraceRepairInteractionChecks.cs` | As written. For call 6 its fresh-part lookup now reads the part's own `freshVisual`. | `6301ad6`, `91a8a86` |
| scale (a)/(b) | – | **Not applied** (call 22) | – |

**imports 5.1, adapted.** The patch was written for the old `BenchOutline`, which uiux rewrote. I applied the same rule to the rewrite:
- `found` is now a `List<Renderer>`;
- the mesh comes from a `SkinnedMeshRenderer`'s `sharedMesh`, or from a `MeshRenderer`'s `MeshFilter`;
- everything else is unchanged.

This is identical to review-days-uiux M1's fix. `BenchGlow` already measures every `Renderer`, so it needed no change.

**Call 22, one "Put it back":**
- Cine's reflection path in `StationInteractable` is gone (`PutItBackWords` and the `PickupReads` reflection class).
- Patch C is in:
  - availability needs `DropInteractable.Takes(dropSpot, carried)`;
  - the prompt is `DropInteractable.TrayWords(dropSpot, carried)`;
  - `Interact` has the same guard;
  - the statics are cleared at load.
- Scale's `PlayerCarry.LiftedFrom` is kept, and uiux's "lifted from this tray" reads it:
  - `DropInteractable.LiftedFrom(spot, item)` returns `PlayerCarry.Current.LiftedFromFor(item) == spot`, which is recorded at the pickup;
  - uiux's once-a-frame `IntakeTrayWatch` (with `Boot` and its lists) is removed.
- Day 1's carry step still holds the tray back: `DropInteractable.Takes` refuses the counter tray for `DayOneGuideUI.CarryingToBench` directly, without reflection.
- review-days-uiux m4 advised skipping C. Call 22 decided otherwise, and C as applied keeps everything b-intro's version did.

### 1.3 The Producer's calls (§3)

**1. The bench layout (call 21)** (`967d2c4`, completed in `49d8b08`)
- `Assets/Scripts/DeviceDefinition.cs` presets:

  | Device | Rest | Tool rest (x, y) | Scrap tray |
  |---|---|---|---|
  | Phone | (−30, −14) | (−135, **0**) | **(6, 133)** |
  | Watch | (−20, 0) | (−115, **0**) | **(26, 107)** |
  | Camera (unchanged) | (−82, 0) | (−239, 0) | (−5, 115) |

  New helpers: `BenchLayout.ForKind(kind)` and `SamePlaceAs(other)`.
- **Bench 7** (`Assets/Editor/ForgivingBenchSteps.cs`) writes these presets into PhoneRepair, PhoneJob, the camera and the watch. Its "Already done" branch only reported the layouts; now it writes what differs (`LayOutDevices(apply: true)`, review-bench M1). A re-run therefore corrects a prefab laid out earlier.
- **Bench 9** (`Assets/Editor/BenchFramingSteps.cs`):
  - T9 bars: phone .34 / .55, camera .35 / .55, watch .17 / .55 (zoom 0 / zoom 1);
  - it frames only call 21's layout; otherwise: "Run Bench 7 again (it writes it), then Bench 9; nothing done";
  - T8: ≥ 12 px at zoom 0, and ≥ 15 px one notch in (the scale builder's) is kept.
- **Bench 5** (`Assets/Scripts/Diagnostics/BenchViewProbe.cs`): after each device's first photo it runs a framing pass:
  - the layout is call 21's;
  - a note if Bench 9 hasn't framed the device;
  - T9 at zoom 0 (34 / 35 / 17 %): the projected height of the corners of the counted renderers' mesh bounds, over the screen height;
  - T8 for the phone and the watch: the smallest 3.8 mm head across, in px at 1080p;
  - zoom 1 (≥ 55 %): the inspector's zoom is set to 1 and held 1 s; then a photo `…-1b-zoom-limit`, and back to 0.
- `bench_stage.json`'s `per_device` (wave 2) carries the same numbers. No other file hardcodes the old layout (searched).

**2. The cloth shows what it cleans (call 23, QA M3)** (`25c1a48`)
- `Assets/Scripts/ToolPickup.cs`: with the cloth pressed flat, `LeadWithFarEdge` moves **only the held model**, so the wad's far edge (up the screen, away from Ace) sits at the cursor.
  - "Up" is the screen's up, made perpendicular to the ray at the tip and laid onto the surface.
  - The far edge is measured every frame from the held mesh's vertices, with its Press/Bunch keys at their current weights, through its world matrix. A mirrored rub therefore keeps it.
  - If the mesh can't be read, the renderers' bounds are used instead.
  - It is closed form: no smoothing, no lag.
- The tip, the contact and the clean footprint (`ItemInspector.Rub`, from the cursor's ray) don't move; they stay centred on the cursor.
- **Bench 8** (`Assets/Scripts/Diagnostics/ClumsyBenchLab.cs`) measures every cloth wipe:
  - It samples the frames where the spot is 25–75 % clean and the cloth is pressed.
  - On each such frame it measures the share of the spot's projected disc outside the cloth's projected screen bounds, from 192 samples spread evenly over the disc. The figure is averaged over the wipe.
  - It takes one mid-wipe photo per run, `…-mid-wipe-<spot>-<pct>pct`.
  - It adds the row "cloth: spot seen mid-wipe" and the check "every wipe ≥ 40 %". A wipe too fast to sample gets a Note.

**3. The night zoom's lab (T20 restated, QA M2)** (`534751c`, completed in `49d8b08`)
- `Assets/Scripts/Diagnostics/NightZoomCheck.cs`:
  - On a frame where a limit eases (this frame or the last), the distance moves ≤ **35 m/s × dt + 0.05 m**.
  - On a frame with no zoom input (this frame or the last), it moves ≤ **3 m**.
  - The fastest frame is now a Note: a wheel notch is 1.6 m in one frame by design.
- **Mouse-scroll pass** (about 40 lines): a virtual `Mouse`'s wheel.
  - At the bins: a notch out (to 9.6 m) and back (8.0 m).
  - At her pavement: a notch in, which changes nothing and must not snap.
  - The virtual mouse is removed on Restore and in `OnDisable`.
- `Assets/Scripts/CafeViewMode.cs` (review-npc-cine M2, the view's half): the ease step is capped at 3/35 s instead of 0.1 s.
  - A hitch frame now moves the distance at most 3 m. Before the cap, a 100 ms frame mid-ease moved 3.49 m, which the new lab check would fail on his Mac.
  - 15 fps is unchanged.

**4. The intro's new cues** (`0f8b767`)
- `Assets/Scripts/SoundBank.cs` Defaults: `intro.lights`, `intro.cup`, `intro.steps`, `intro.car`. Each is UI bus, flat, .3, 1.5 / 12 m, gap .5, one at once. `street.day` was already there.
- `Assets/Editor/SoundSetup.cs`:
  - **Sound bank 1** gives `intro.lights` the files of `intro.lamp`, but only while Audio's manifest has none of its own. Its head line counts "N given another cue's file for now".
  - **Sound bank 2** checks that `intro.lights` plays a file. It lists `intro.cup, intro.steps, intro.car, street.day` as "no file yet (silent, by design until Audio's next pass; never a FAIL)".
- A missing cue logs nothing. I traced `Sfx.Play`, `SoundPlayer.Play`, `PlaceholderSounds` and `SoundBank.Find`: a cue with no clips and no placeholder is silent without a log.

**5. Blender wave 2 into the tree** (`8f1a54f`, from `/root/cafe/work/blender/`)
- **Copied:**
  - `Assets/Art/Models/Devices/{HD_Phone,HD_Watch,HD_Camera}.fbx` and `devices.json` v3 (the folder `HeroDeviceSteps` reads);
  - `Assets/Art/Models/BenchTools/BT_Screwdriver.fbx` (its `bench_tools.json` is byte-identical, so it is unchanged);
  - `Assets/Data/Resources/Bench/BT_ScrewHole.png`;
  - the five icons `Assets/Data/Resources/UI/BenchIcons/{part_phone,part_watch,part_camera,cover_phone,cover_watch}.png`;
  - `Assets/Art/Models/CafeKit/LI_BarMat.fbx` (the mug tree) and `cafe_dressing.json` (with `LI_MugGreen`);
  - `Assets/Art/Models/BenchStage/bench_stage.json` (the call 21 table);
  - the changed `Tools/Blender/*.py` and `README.md`, and the new `bench_screw_hole.py` and `review_devices_wave2.py`;
  - `BlenderSource/{HeroCamera_v3,HeroPhone_v3,HeroWatch_v3,BenchTools_v4,CafeDressing_v1}.blend` (at the repo root, so Unity never imports them).
- **Not copied** (byte-identical re-exports, or not for the repo): `BM_RepairMat.fbx`, `BenchStage_v1.blend`, `bench_tools.json`, fonts, `__pycache__`, `out/icon-renders`.
- **Metadata and storage:**
  - Every existing `.meta` is kept; no `.meta` is in the diff for a replaced file.
  - Binaries go through LFS per `.gitattributes`.
  - Where the real bytes are: §2.
- `Assets/Editor/BenchStageImportSteps.cs`: the hole decal's import settings from wave 2 §5.4 (Default, sRGB, alpha is transparency, mipmaps, bilinear, Clamp, max 256, uncompressed).
  - The postprocessor applies them on import.
  - **Bench icons 1** re-applies them and checks `Resources.Load<Texture2D>("Bench/BT_ScrewHole")`.

**6. Devices 2 meets wave 2** (`91a8a86`)

**What Devices 2 does now.** The menu item is `Fixit Fidget › Bench › Devices 2 - Fit the hero devices at real size (PhoneRepair, PocketWatch, GraceReunionCamera)`. Per device, in this order:
1. It reads `devices.json` v3: the map (`targets`), `moves` with the scratches' places, `newObjects` and `strapNoCollider`.
2. It refuses a device whose FBX isn't real yet: the body must be within ±7 % and the head 3.8 ± 0.1 mm.
   - **The camera's real width is now 129 mm with its lugs.**
   - The v3 body mesh is 134 mm with lugs. Against 120 mm that is 1.12×, so it would have been refused.
3. On the first run only, it writes the "before" snapshot and a photo.
4. **The fit.**
   - Each FBX piece goes into its prefab objects, including the strap: `UpperStrap` → `Upper strap`, `LowerStrap` → `Lower strap`, `WornLoop` → `Worn loop` (the manifest's targets).
   - `CleanMechanism` → **`Clean shutter mechanism`** is made under `GraceReunionCamera` (its `createUnder`), with a BoxCollider fitted to its mesh.
5. **The bake × k** (first run only), then `realSize = true`.
6. **v3:**
   - Every moved object goes to its `to` place, carrying what is under it:
     - **on the camera**, up 9 mm with the front face: the body (now 32 mm deep, its back where it was), the lens housing, the lens glass, the lens grime, the open film path, the film-path grime, the film-guide grime, and the Jammed shutter mechanism (the shutter's seat);
     - the new strap: Upper strap and Lower strap at their lugs, and the Worn loop behind the lens half;
     - the three Old scratches, onto the loop;
     - **on the phone (2 screws) and the watch (4 screws)**: the heads, onto their surfaces.
   - `Clean shutter mechanism` goes to (0.0265, 0.039, 0.0205) and is **switched off**.
   - **Every collider is removed** from Upper strap, Lower strap, Worn loop and Old scratch 0–2.
   - **The camera's shutter is wired:**
     - `ReplaceablePart.freshVisual` = Clean shutter mechanism;
     - `brokenVisual` = the Jammed shutter mechanism itself;
     - `DeviceWake` = ShutterFires with [Clean shutter mechanism].
   - The Working shutter blade stays in the prefab, inactive and unused.
7. **`restHeight`** comes from the moved device: its lowest point (now the loop) sits 3.3 mm over its slot point, 0.3 mm over the felt.
8. **The checks.** The prefab is saved only if every one passes:
   - every mapped piece is fitted, and every mesh is present;
   - the head boxes are right, and `realSize` is set;
   - every object is at before × k ± 0.1 mm, or at its v3 place with what is under it carried;
   - the new piece has its mesh and box, is off, and is in its place;
   - no collider is left on the strap list;
   - the shutter is wired;
   - the rest height is right.
9. It saves the prefab and takes an after photo.

A second run says "already real": the fit and v3 find everything in place, and it reports "nothing to change".

**Related changes in the same commit:**
- `DeviceWake.WindAndFire` blinks only a part whose name contains "blade". The clean mechanism is heard (`camera.shutter`), not spun.
- The scale audit's "after" comparison expects v3's places too.

**Order against the days builder's step B:**
- Step B (`Fixit Fidget › Days › B - Grace's camera: the clean and the shutter (prefab)`, or the whole `Days 1–3 - the intro level (data)`) copies the jammed assembly into `Shutter mechanism (resting)`.
- **B now refuses while the camera isn't real size**: "run … Devices 2 first (after Devices 1), then this. Nothing changed."
- So it can only run **after** Devices 2, and it copies the baked, risen assembly.
- B copies only active children, so the unused Working shutter blade never appears in the resting copy.
- If Devices 2 ever moves the assembly again after B, re-run B: it compares a signature and re-copies.

**7. Contract drift** (`81eeeb2`, plus the call commits)
- `Assets/Editor/NpcWave2Steps.cs:1010`: **a real compile error** that surfaced only in the merged tree.
  - In `g != null ? (g.firstDay, g.lastDay) : (0, 0)`, C# 9 drops the tuple's element names because the second arm is unnamed.
  - Fix: both arms are named.
- `Assets/Editor/Playtest4BenchSteps.cs` (`Playtest 4 › Bench › 0 - Check first`) read `ToolPickup.padSize`/`padThickness`. b-bench deleted both fields, so `run.Prop` would have thrown.
  - Now, when the fields are gone, it checks that `BT_ClothHeld` is under the cloth.
  - The contact is `BenchRules.ClothContact`, 45 mm; Bench 8 measures the rest.
- The other drift fixes are listed with their patch or call:
  - `BenchOutline` and skinned renderers (imports 5.1);
  - the tweezers check's fresh part (call 6);
  - the camera's real width (call 6).
- **Scans that found nothing more:**
  - all 81 reflection targets on repo types (`typeof(X).GetField/GetMethod/GetProperty("…")`) exist;
  - every serialized name read through `FindProperty` on a repo type exists. The only unresolved name is Unity's `AudioImporter` `defaultSettings.preloadAudioData`, which is read through `SerializedObject` on purpose.

### 1.4 Beyond the brief: the 11:55 reviews

**Applied:**

| Finding | Fix | Commit |
|---|---|---|
| review-bench **B1** (Blocker): the guard reads every landing hop as a move, logs a red error per landing, and fails Bench 3 and Bench 8 | `BenchStage.Guard` skips a piece still in the flight list (`HasFlight`) | `cc16dca` |
| review-bench M1: superseded presets; a Bench 7 re-run can't correct them | = call 21, plus Bench 7's re-run writing the layouts | `967d2c4`, `49d8b08` |
| review-bench M2: the cloth covers the lens | = call 23 (far-edge lead, rather than the review's half-pad offset) | `25c1a48` |
| review-bench **M3**: Bench 8's T6 counted `GrimeSpot.Passes`, which misses strokes | It counts the hand's own half-cycles until the spot is clean | `cc16dca` |
| review-bench **m1**: the hover outline stays on a seated fresh part (Day 2's new shutter keeps reading as "touch this next") | `LoosePart.Seated` turns its outline off first | `cc16dca` |
| review-days-uiux M1: no glow on the skinned brush | = imports 5.1, adapted | `65dd701` |
| review-days-uiux **M2**: `Checks › Bench words` could never pass after the merge | `FaultSpeech`: the phone's Human fault is said "calls light up, but it never rings" (8 words, 46 characters with "Officer"; placeholder copy for his sister) | `cc16dca` |
| review-npc-cine **M1**: the whole-game playtest opens with no settled guests | `DayIntro.LabWanted`; `PatronSpawner.SeatOpeningGuests` follows the intro's lab rule | `cc16dca` |
| review-npc-cine M2: the lab can't fail T20's 3 m clause, and the view breaks it on a hitch | Lab = call 3; the view's 3/35 s step cap | `534751c`, `49d8b08` |
| review-npc-cine m1: no wheel in the lab | = call 3's mouse-scroll pass | `534751c` |

**Not applied** (Minors, for their owners):
- **review-bench:**
  - m2: pieces in the recessed cups and wells read as "not seen";
  - m3: on a pad, a tool chosen with LB/RB goes home after an A-press held off the device;
  - m4: Bench 8 never plays the fresh part's click-then-hold.
- **review-days-uiux:**
  - m1: T17's automatic "what's left" fires again on each return to the bench;
  - m2: 27 words while a covered part is hovered (a copy call);
  - m3: per-frame allocations at the bench;
  - m4: superseded by call 22.
- **review-npc-cine:**
  - m2: a laptop guest can sit without a laptop;
  - m3: a dropped passer-by shot leaves its lead cue playing. This is silent for now: `intro.steps` and `intro.car` have no file.
  - m4: the NPC scene steps save a dirty scene and don't undo a half-made change. The run order saves the scene before each of them.
  - m5: the stool sit's "float" is checked only from below.

### 1.5 The commits on `integ` (oldest first)

| Commit | What |
|---|---|
| `6817b7d`, `cbc5245`, `5fb0259`, `915d08b`, `fc929e0`, `48c46c7`, `5418288` | The seven merges |
| `14047f3` | npc patches |
| `2103056` | days patches |
| `65dd701` | imports 5.1 |
| `dd5d612` | The intro hook and DayIntroMenu |
| `7101f00` | uiux A, B, D, E, F |
| `f0873e4` | Call 22 and patch C |
| `6301ad6` | bench 5.1 |
| `967d2c4` | Call 21 |
| `25c1a48` | Call 23 |
| `534751c` | T20 |
| `0f8b767` | Call 4 |
| `8f1a54f` | Wave 2 |
| `91a8a86` | Call 6 |
| `81eeeb2` | Drift |
| `49d8b08` | Calls 21 and 3, completed |
| `cc16dca` | Review fixes |

The commit-trailer instruction (`Co-Authored-By` / `Claude-Session`) reached me after `81eeeb2`:
- **The first 21 of these 23 commits lack the trailers.**
- I did not rewrite them, under the no-history-rewriting rule.
- `49d8b08` and `cc16dca` carry them.

---

## 2. The deploy list

`git diff --name-status 431f081 integ` lists **311 paths**:

| Group | Paths |
|---|---|
| Code | 71 |
| Editor | 37 |
| Data assets | 28 |
| Tools/Blender | 23 |
| Tests | 3 |
| Binaries (Git LFS) | 149 |
| **Total** | **311** |

**Where the binaries' real bytes are** (each checked by sha256 against its LFS oid):
- **All 149** are real files in the integ worktree, at `/root/cafe/build/integ/<path>`.
  - The merge had left 133 of them there as LFS pointer text, because smudge is `--skip` here.
  - I replaced each pointer with its object from the repo's LFS store and checked it. `git add` refreshed the index's stat data, and `git status` is clean.
  - So the integ worktree can be copied as it stands.
- **All 149** are also in the repo's LFS store, at `/root/cafe/repo/.git/lfs/objects/<oid[0:2]>/<oid[2:4]>/<oid>`.
- At their source:
  - **133** are at `/root/cafe/build/imports/<path>`;
  - **16** (wave 2) are at `/root/cafe/work/blender/<path>`.
- **Total size of the binaries: 11,845,923 bytes (11.3 MiB).**

**Unity makes the missing metadata on import:**
- 142 new files under `Assets/` have no `.meta`: imports' wave 1 files, wave 2's new files, and three new editor scripts (`BenchStageImportSteps.cs`, `CafeDressingSteps.cs`, `DaysOneToThreeSteps.cs`).
- Six new folders have none either.
- Nothing refers to any of them by GUID; they load by path.
- Commit the Mac's new `.meta` files afterwards (imports §2 step 12).

**Code (runtime, compiled into the game): 71 (9 A, 62 M)**

M `Assets/Data/Resources/Bench outline.shader`, M `Assets/Scripts/BenchCards.cs`, A `Assets/Scripts/BenchFeel.cs`, A `Assets/Scripts/BenchGlow.cs`, M `Assets/Scripts/BenchInteractable.cs`, M `Assets/Scripts/BenchOutline.cs`, A `Assets/Scripts/BenchRules.cs`, M `Assets/Scripts/BenchStage.cs`, A `Assets/Scripts/BenchStageContract.cs`, A `Assets/Scripts/BenchWork.cs`, M `Assets/Scripts/CafeSoundscape.cs`, M `Assets/Scripts/CafeViewMode.cs`, M `Assets/Scripts/CustomerBrain.cs`, M `Assets/Scripts/CustomerIdentity.cs`, M `Assets/Scripts/CustomerMemoryService.cs`, M `Assets/Scripts/CustomerSpawner.cs`, M `Assets/Scripts/DayClock.cs`, M `Assets/Scripts/DayDefinition.cs`, M `Assets/Scripts/DayIntro.cs`, M `Assets/Scripts/DayOneGuideUI.cs`, M `Assets/Scripts/DeviceDefinition.cs`, M `Assets/Scripts/DeviceWake.cs`, M `Assets/Scripts/Diagnostics/AimHelpCheck.cs`, M `Assets/Scripts/Diagnostics/BenchLab.cs`, M `Assets/Scripts/Diagnostics/BenchViewProbe.cs`, A `Assets/Scripts/Diagnostics/BenchWordsCheck.cs`, A `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs`, A `Assets/Scripts/Diagnostics/ClumsyHand.cs`, M `Assets/Scripts/Diagnostics/Day1GuideCheck.cs`, M `Assets/Scripts/Diagnostics/DayIntroCheck.cs`, M `Assets/Scripts/Diagnostics/DayShapeCheck.cs`, M `Assets/Scripts/Diagnostics/DispenserFrameCheck.cs`, M `Assets/Scripts/Diagnostics/JuiceCheck.cs`, M `Assets/Scripts/Diagnostics/NightOneCheck.cs`, A `Assets/Scripts/Diagnostics/NightZoomCheck.cs`, M `Assets/Scripts/Diagnostics/StationsCheck.cs`, M `Assets/Scripts/DropInteractable.cs`, M `Assets/Scripts/FaultSpeech.cs`, M `Assets/Scripts/GameSettings.cs`, M `Assets/Scripts/GraceCameraEpisode.cs`, M `Assets/Scripts/GraceCameraRepairJob.cs`, M `Assets/Scripts/GrimeSpot.cs`, M `Assets/Scripts/HudCorners.cs`, M `Assets/Scripts/ItemInspector.cs`, M `Assets/Scripts/JobTicket.cs`, M `Assets/Scripts/NightZero.cs`, M `Assets/Scripts/NotebookHooks.cs`, M `Assets/Scripts/NpcSeating.cs`, M `Assets/Scripts/NpcSocial.cs`, M `Assets/Scripts/NpcVisualVariants.cs`, M `Assets/Scripts/PadCursor.cs`, M `Assets/Scripts/PadInput.cs`, M `Assets/Scripts/PatronBrain.cs`, M `Assets/Scripts/PatronSpawner.cs`, M `Assets/Scripts/PlayerCarry.cs`, M `Assets/Scripts/PolygonNpcVisual.cs`, M `Assets/Scripts/RecapPhone.cs`, M `Assets/Scripts/RemovablePart.cs`, M `Assets/Scripts/RepairChecklist.cs`, M `Assets/Scripts/ReplaceablePart.cs`, M `Assets/Scripts/SaveManager.cs`, M `Assets/Scripts/Screw.cs`, M `Assets/Scripts/ShopUI.cs`, M `Assets/Scripts/SoundBank.cs`, M `Assets/Scripts/SoundPlayer.cs`, M `Assets/Scripts/StationInteractable.cs`, M `Assets/Scripts/TableSeat.cs`, M `Assets/Scripts/TicketRailUI.cs`, M `Assets/Scripts/ToolPickup.cs`, M `Assets/Scripts/WaitingArea.cs`, M `Assets/Scripts/WordBudget.cs`

**Editor (menu steps, checks): 37 (11 A, 26 M)**

M `Assets/Editor/BenchFeelSteps.cs`, A `Assets/Editor/BenchFramingSteps.cs`, A `Assets/Editor/BenchScaleAudit.cs`, A `Assets/Editor/BenchStageImportSteps.cs`, A `Assets/Editor/BenchWordsChecks.cs`, A `Assets/Editor/BenchWordsMenu.cs`, M `Assets/Editor/BeverageStationSetup.cs`, M `Assets/Editor/BlenderKitSteps.cs`, A `Assets/Editor/CafeDressingSteps.cs`, M `Assets/Editor/CafeLivedInSteps.cs`, M `Assets/Editor/CafeSeatsSteps.cs`, A `Assets/Editor/CinematicsWave2Steps.cs`, M `Assets/Editor/ContinuationIntegrationChecks.cs`, M `Assets/Editor/ContinuationRuleChecks.cs`, M `Assets/Editor/ControllerChecks.cs`, M `Assets/Editor/DayIntroMenu.cs`, M `Assets/Editor/DayOneOnboardingChecks.cs`, A `Assets/Editor/DaysOneToThreeSteps.cs`, M `Assets/Editor/DeviceCleaningReachabilityChecks.cs`, M `Assets/Editor/DialogueRuleChecks.cs`, M `Assets/Editor/FaultExpansionSetup.cs`, A `Assets/Editor/ForgivingBenchSteps.cs`, M `Assets/Editor/GraceRepairInteractionChecks.cs`, M `Assets/Editor/GraceShowcaseSetup.cs`, M `Assets/Editor/HeroDeviceSteps.cs`, A `Assets/Editor/IntakeTrayRestCheck.cs`, M `Assets/Editor/NotebookPlayCheck.cs`, M `Assets/Editor/NpcSitLiveCheck.cs`, A `Assets/Editor/NpcWave2Steps.cs`, M `Assets/Editor/Playtest3Session3Steps.cs`, M `Assets/Editor/Playtest3Session4Steps.cs`, M `Assets/Editor/Playtest4BenchSteps.cs`, M `Assets/Editor/Playtest4ProducerSteps.cs`, M `Assets/Editor/Playtest4SystemsSteps.cs`, M `Assets/Editor/SoundSetup.cs`, M `Assets/Editor/UpgradeEconomyChecks.cs`, M `Assets/Editor/WaitingSpaceChecks.cs`

**Data assets (manifests, .meta, licences, .gitignore): 28 (25 A, 3 M)**

M `.gitignore`, A `Assets/Art/Models/BenchStage/bench_stage.json`, M `Assets/Art/Models/BenchTools/bench_tools.json`, A `Assets/Art/Models/CafeKit/cafe_dressing.json`, M `Assets/Art/Models/Devices/devices.json`, A `Assets/Audio/CC0/licences/kenney-city-builder-kit__README.md`, A `Assets/Audio/CC0/licences/kenney-interface-sounds__LICENSE.txt`, A `Assets/Audio/CC0/licences/kenney-ui-audio__LICENSE.txt`, A `Assets/Audio/CC0/licences/sonic-pi-samples__README.md`, A `Assets/Audio/CC0/licences/vcsl__LICENSE`, A `Assets/Audio/CC0/manifest.json`, A `Assets/Editor/BenchFramingSteps.cs.meta`, A `Assets/Editor/BenchScaleAudit.cs.meta`, A `Assets/Editor/BenchWordsChecks.cs.meta`, A `Assets/Editor/BenchWordsMenu.cs.meta`, A `Assets/Editor/CinematicsWave2Steps.cs.meta`, A `Assets/Editor/ForgivingBenchSteps.cs.meta`, A `Assets/Editor/IntakeTrayRestCheck.cs.meta`, A `Assets/Editor/NpcWave2Steps.cs.meta`, A `Assets/Scripts/BenchFeel.cs.meta`, A `Assets/Scripts/BenchGlow.cs.meta`, A `Assets/Scripts/BenchRules.cs.meta`, A `Assets/Scripts/BenchStageContract.cs.meta`, A `Assets/Scripts/BenchWork.cs.meta`, A `Assets/Scripts/Diagnostics/BenchWordsCheck.cs.meta`, A `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs.meta`, A `Assets/Scripts/Diagnostics/ClumsyHand.cs.meta`, A `Assets/Scripts/Diagnostics/NightZoomCheck.cs.meta`

**Tools/Blender (not imported by Unity): 23 (14 A, 9 M)**

M `Tools/Blender/README.md`, A `Tools/Blender/bench_screw_hole.py`, A `Tools/Blender/bench_stage.py`, A `Tools/Blender/bench_stage_textures.py`, M `Tools/Blender/bench_tools.py`, A `Tools/Blender/cafe_cafeside.py`, A `Tools/Blender/cafe_repairside.py`, A `Tools/Blender/cafe_wave1_textures.py`, A `Tools/Blender/export_bench_stage.py`, M `Tools/Blender/export_bench_tools.py`, A `Tools/Blender/export_cafe_wave1.py`, M `Tools/Blender/export_hero_devices.py`, M `Tools/Blender/fixit_blender.py`, A `Tools/Blender/fonts/Caveat-OFL.txt`, A `Tools/Blender/fonts/Inter-OFL.txt`, M `Tools/Blender/hero_camera.py`, M `Tools/Blender/hero_lib.py`, M `Tools/Blender/hero_phone.py`, M `Tools/Blender/hero_watch.py`, A `Tools/Blender/render_bench_icons.py`, A `Tools/Blender/review_bench_stage.py`, A `Tools/Blender/review_cafe_wave1.py`, A `Tools/Blender/review_devices_wave2.py`

**Tests (console, not in Unity): 3 (3 A, 0 M)**

A `Tests/BenchRules/.gitignore`, A `Tests/BenchRules/BenchRules.csproj`, A `Tests/BenchRules/Program.cs`

**Binaries (Git LFS): 149 (138 A, 11 M), 11,845,923 bytes (11.3 MiB)**

| | Path | Bytes | Real bytes (besides `/root/cafe/build/integ/<path>` and the LFS store) |
|---|---|---|---|
| A | `Assets/Art/Models/BenchStage/BM_RepairMat.fbx` | 59,580 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/BenchTools/BT_Brush.fbx` | 57,100 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/BenchTools/BT_Cloth.fbx` | 64,364 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/BenchTools/BT_ClothHeld.fbx` | 64,956 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/BenchTools/BT_PryTool.fbx` | 23,564 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/BenchTools/BT_ScrapTray.fbx` | 21,900 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/BenchTools/BT_Screwdriver.fbx` | 36,828 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Art/Models/BenchTools/BT_ToolRest.fbx` | 28,876 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/BenchTools/BT_Tweezers.fbx` | 31,452 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_BarMat.fbx` | 86,604 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_CupStation.fbx` | 46,924 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_DeskCorner.fbx` | 80,716 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_DonorParts.fbx` | 44,828 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_FinalNotice.fbx` | 19,180 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_GlassNotes.fbx` | 16,828 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_MortgageLetter.fbx` | 15,612 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_PartsDelivery.fbx` | 28,748 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_PickupRow.fbx` | 67,820 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_SyrupRack.fbx` | 54,620 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Models/CafeKit/LI_Whiteboard.fbx` | 34,812 | `/root/cafe/build/imports/<path>` |
| M | `Assets/Art/Models/Devices/HD_Camera.fbx` | 426,108 | `/root/cafe/work/blender/<path>` |
| M | `Assets/Art/Models/Devices/HD_Phone.fbx` | 130,412 | `/root/cafe/work/blender/<path>` |
| M | `Assets/Art/Models/Devices/HD_Watch.fbx` | 400,236 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Art/Textures/Bench/BM_Grid_color.png` | 3,929 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Textures/Bench/BM_Rulers.png` | 56,449 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Textures/Cafe/LI_FinalNotice.png` | 67,573 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Textures/Cafe/LI_Mortgage.png` | 89,119 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Textures/Cafe/LI_Tags.png` | 17,573 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Art/Textures/Cafe/LI_Whiteboard.png` | 84,094 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/Bench/BT_ScrewHole.png` | 23,067 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/brush.png` | 25,636 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/cloth.png` | 23,883 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/cover.png` | 28,707 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/cover_phone.png` | 22,703 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/cover_watch.png` | 31,743 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/newpart.png` | 28,969 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/part_camera.png` | 38,826 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/part_phone.png` | 23,703 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/part_watch.png` | 49,569 | `/root/cafe/work/blender/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/pry.png` | 17,798 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/screw.png` | 28,699 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/screwdriver.png` | 19,524 | `/root/cafe/build/imports/<path>` |
| A | `Assets/Data/Resources/UI/BenchIcons/tweezers.png` | 13,827 | `/root/cafe/build/imports/<path>` |
| A | `BlenderSource/BenchStage_v1.blend` | 128,441 | `/root/cafe/build/imports/<path>` |
| A | `BlenderSource/BenchTools_v4.blend` | 220,555 | `/root/cafe/work/blender/<path>` |
| A | `BlenderSource/CafeDressing_v1.blend` | 322,122 | `/root/cafe/work/blender/<path>` |
| M | `BlenderSource/HeroCamera_v3.blend` | 362,221 | `/root/cafe/work/blender/<path>` |
| M | `BlenderSource/HeroPhone_v3.blend` | 175,656 | `/root/cafe/work/blender/<path>` |
| M | `BlenderSource/HeroWatch_v3.blend` | 399,921 | `/root/cafe/work/blender/<path>` |
| A | `Tools/Blender/fonts/Caveat-Bold.ttf` | 257,316 | `/root/cafe/build/imports/<path>` |
| A | `Tools/Blender/fonts/Inter-Bold.otf` | 626,056 | `/root/cafe/build/imports/<path>` |
| A | `Tools/Blender/fonts/Inter-Regular.otf` | 605,092 | `/root/cafe/build/imports/<path>` |
| A | `Tools/Blender/fonts/Inter-SemiBold.otf` | 624,244 | `/root/cafe/build/imports/<path>` |

Audio, 96 files, all A, 5,586,840 bytes, real bytes also at `/root/cafe/build/imports/<path>`: bench.showleft_01.wav (33,560), brush.rub_01.wav (176,444), cafe.room_01.wav (2,646,044), camera.shutter_01.wav (31,512), camera.shutter_02.wav (23,376), camera.wind_01.wav (28,268), camera.wind_02.wav (26,504), checklist.tick_01.wav (5,932), checklist.tick_02.wav (5,602), cloth.gleam_01.wav (43,262), cloth.gleam_02.wav (41,498), cloth.lift_01.wav (15,038), cloth.lift_02.wav (13,274), cloth.rub_01.wav (176,444), cloth.squeak_01.wav (9,746), cloth.squeak_02.wav (10,628), cloth.squeak_03.wav (7,982), cloth.squeak_04.wav (8,864), cover.clack_01.wav (8,608), cover.clack_02.wav (8,608), cover.clack_03.wav (10,668), cover.creak_01.wav (22,658), cover.creak_02.wav (26,504), cover.creak_03.wav (21,212), cover.lift_01.wav (19,448), cover.lift_02.wav (17,684), cover.pop_01.wav (22,264), cover.pop_02.wav (25,382), cover.pop_03.wav (17,184), day.open_01.wav (167,624), device.flip_01.wav (26,248), device.flip_02.wav (28,308), device.snap_01.wav (8,608), device.snap_02.wav (8,608), device.snap_03.wav (10,668), door.bell_01.wav (110,294), door.bell_02.wav (105,884), grime.clean_01.wav (61,782), grime.clean_02.wav (57,374), grime.scrub_01.wav (7,100), grime.scrub_02.wav (6,218), grime.scrub_03.wav (6,218), intro.lamp_01.wav (6,472), intro.sign_01.wav (15,478), intro.sting_01.wav (141,164), juice.sparkle.big_01.wav (70,604), juice.sparkle_01.wav (26,504), juice.sparkle_02.wav (21,212), money.paid_01.wav (88,244), part.drop_01.wav (14,684), part.drop_02.wav (14,684), part.drop_03.wav (14,684), part.fresh_01.wav (19,242), part.fresh_02.wav (18,262), part.lift_01.wav (12,392), part.lift_02.wav (11,510), part.on_01.wav (25,480), part.on_02.wav (20,904), part.on_03.wav (23,456), part.pinch_01.wav (4,454), part.pinch_02.wav (4,454), part.pinch_03.wav (2,496), part.replace_01.wav (20,904), part.replace_02.wav (13,218), part.replace_03.wav (19,184), phone.boot_01.wav (83,834), repair.fixed_01.wav (119,114), repair.ready_01.wav (61,782), screw.drop_01.wav (19,448), screw.drop_02.wav (19,448), screw.drop_03.wav (19,448), screw.drop_04.wav (19,448), screw.drop_05.wav (19,448), screw.fetch_01.wav (15,038), screw.fetch_02.wav (14,156), screw.fly_01.wav (9,746), screw.fly_02.wav (8,864), screw.free_01.wav (17,586), screw.free_02.wav (18,960), screw.free_03.wav (16,138), screw.seat_01.wav (13,218), screw.seat_02.wav (26,322), screw.seat_03.wav (28,596), screw.turn_01.wav (4,012), screw.turn_02.wav (1,924), screw.turn_03.wav (3,718), screw.turn_04.wav (2,828), screw.turn_05.wav (2,628), tool.down_01.wav (8,608), tool.down_02.wav (8,608), tool.down_03.wav (8,608), tool.pick_01.wav (3,572), tool.pick_02.wav (3,494), tool.pick_03.wav (3,572), watch.tick_01.wav (216,134), watch.wind_01.wav (69,722).

---

## 3. The Mac run order

**Read this first:**
- The scene is `Assets/Playtests/AcesCafeLayoutPlaytest.unity`.
- **Not in Play mode** for any step until Phase C. A script reimport during Play breaks the labs' pools (9 Oct).
- **Never twice:** no step below does harm when run twice. Each says "Already done", "already real" or "nothing to change", or writes the same values again.
- **Order matters for three steps:**
  - The scale audit's BEFORE run (18) must come before Devices 2 (20). After Devices 2 it audits instead of snapshotting, which is harmless.
  - **Days 1–3 (data) (21) must come after Devices 2.** Before it, step B refuses, and the all-in-one step then undoes all its groups.
  - **Never run `Bench › Bench 4` after Devices 2.** It refuses the watch and the camera by design and logs an error. Its results have been in the prefabs since 9 Oct, so it is not in this order.
- Running a step out of order does no damage; it refuses and says why.

### Phase A: the edit-mode steps, in this order

0. **Bring `integ` onto the Mac**: all 311 paths. Take the binaries from `/root/cafe/build/integ`, where they are real bytes, never from a pointer copy. Unity should not be in Play mode while the files land.
1. **Compile.** Wait for the compile to finish: 0 errors in the Console.
   - Then run `Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)`.
   - It reimports any script newer than the last compile.
   - Safe to run twice.
2. `Fixit Fidget › Bench › Bench tools 1 - Import and check the tools (the models only)`.
   - **Before:** step 1.
   - **Prints:** `[Bench tools] Imported and checked.`, "8 pieces, 7618 triangles in all", every line PASS.
   - The keys: BT_ClothHeld `Press, Bunch`, BT_Brush `Bend`.
   - The seats: "turned (275, 0, 0)", Seat_Cloth (0, 270, 0).
   - The screwdriver's new tip is still 990 tris.
3. `Fixit Fidget › Bench › Bench stage 1 - Import and check the mat (the model only)`.
   - **Prints:** `[Bench stage] Imported and checked.`, `1 pieces, 2142 triangles`.
   - The ten empties are "at (…)" within 1 mm.
   - The six BM_ materials are listed.
4. `Fixit Fidget › Bench › Bench icons 1 - Import the checklist's icons as sprites`.
   - **Prints:** `[Bench icons] The icons are sprites.`, with 8 PASS lines (`finds a 256 x 256 sprite`).
   - Then "Also in the folder (device-specific icons, same settings): cover_phone, cover_watch, part_camera, part_phone, part_watch."
   - Then a PASS for the screw hole's decal (`Resources.Load<Texture2D>("Bench/BT_ScrewHole")` finds it).
5. `Fixit Fidget › Bench › Devices 1 - Import and check the hero devices (the models only)`.
   - **Before:** wave 2's FBX are in, from step 0.
   - **Prints:** `[Hero devices] Imported and checked.`
     - phone: 6 meshes, 4,936 tris;
     - watch: 6, 18,178;
     - **camera: 11, 16,892**;
     - every child at its `at` ±1 mm, all PASS (wave 2 §5.1).
6. `Fixit Fidget › Cafe redesign › Café dressing 1 - Import and check the pieces (the models only)`.
   - **Prints:** 11 pieces, about 12,500 triangles, all PASS.
   - The materials made from the manifest now include `LI_MugGreen`.
7. `Fixit Fidget › Sound › Sound bank 1 - Import the CC0 sounds and fill the bank`.
   - **Before:** ideally he has heard Audio's preview reel, `/root/cafe/audio/out/preview/all-cues-at-suggested-volume.wav`.
   - **Prints:** `45 cue(s) filled, 0 given new files, 0 already had theirs …; 39 cue(s) added to the bank; 1 given another cue's file for now; 96 file setting(s) changed.`
     - This assumes the Mac's bank is the committed one.
     - 39 is imports' 35 plus call 4's four.
   - A second run: "Already done". Undo is available.
8. `Fixit Fidget › Sound › Sound bank 2 - Check the bank against Audio's table (read-only)`.
   - **Prints:** `PASS: all 45 of Audio's cues play their files (T18); every bench verb has its cue; every file imported as Audio asked.`
   - In the report:
     - `PASS intro.lights plays a file (intro.lamp's for now…)`;
     - "no file yet (silent, by design…; never a FAIL): intro.cup, intro.steps, intro.car, street.day".
9. **By hand:** in Project Settings › Audio, set DSP Buffer Size to **Good latency** and Max Real Voices to **48**. If the Mac crackles, put the buffer size back.

**Scene steps.** Open the café scene and **save it before each step**:
- the drinks step (17) and the days step (21) refuse a dirty scene;
- the NPC steps would save someone else's unsaved change as their own (review m4).

10. `Fixit Fidget › Cafe redesign › Café dressing 1 - Dress the café (scene)`.
    - **Prints:** `DONE` and `The scene is saved.`
    - Read any `LEFT OUT:` line. S2 moves about 4 cm out of the door.
    - Second run: "Already done".
11. `Fixit Fidget › Night › Night walk 2 - Rebuild the list of what is solid by night (Edit Mode)`, then **File › Save**.
12. `Fixit Fidget › Cafe redesign › Café dressing 1 - Check from above (read-only)`.
    - **Prints:** a PASS for each piece's "nothing passes through it" and "no collider" lines, and the bottles off.
    - **C3 (the mug tree on LI_BarMat) should now PASS at about 25 × 13 px.** Before wave 2 it was the expected FAIL.
    - The close-up line: "no dressing piece in its frame".
13. `Fixit Fidget › Bench › Bench 7 - The forgiving stage (scene)`.
    - **Before:** steps 2–3, so it places Art's models and not stand-ins. Bench 2's `Stage (v2)` is present (it is in the committed scene).
    - **Prints:** `[Bench 7] Done: BM_RepairMat, BT_ToolRest and BT_ScrapTray under InspectRig/Stage (v2)… Device layouts: 4 set…`.
    - In the report:
      - "Tools onto the rest's seats: 5 moved";
      - "3 switched off";
      - "worst N mm" under 1 mm;
      - `faceSnapWithin: 22 -> 45`;
      - three device slots at mat y −265.
    - It writes call 21's layouts into the four prefabs and saves the scene.
    - Second run: "Already done … the scene is unchanged. Device layouts: 0 set, 4 already right."
14. `Fixit Fidget › NPC › Wave 2 1 - The repair-bar stools sit (scene)`.
    - **Prints:** one line per stool, e.g. "Seat (repair bar, west): … -> Repair Bar on, Snap To Seat on, Max Lift 0.32 … (west 45 degrees)". The middle and east stools face east 45.
    - It saves the scene.
15. `Fixit Fidget › NPC › Wave 2 2 - Settled guests and the patrons' rhythm (scene)`.
    - **Prints:** three guests for Days 1–3: a reader, a pair, and a laptop worker at communal east 1.
    - They leave at 0.30 / 0.40 / 0.55 of the day.
    - Also "Spawn Jitter: 0.35 -> 0.60".
16. `Fixit Fidget › NPC › Wave 2 3 - The names' sexes (scene; after the CustomerSpawner patch)`.
    - The patch is in.
    - **Prints:** 16 names: 7 Woman, 7 Man, 2 Any.
17. `Fixit Fidget › Cinematics › Drinks close-up - straight on (scene)`.
    - **Before:** the scene is saved.
    - **Prints:** `[Cinematics] Drinks close-up - straight on: done (N check(s) passed)`. The camera moves to about local (0, 0.657, −1.05).
    - It saves the scene.

**The devices at real size** (prefabs; the café scene stays open):

18. `Fixit Fidget › Checks › Bench scale audit (read-only)`: **the BEFORE run.**
    - **Prints:** `[One scale] Bench scale audit …: N PASS, M FAIL (read-only). Before Devices 2 the camera, watch, screw and hole rows FAIL by design; …`
    - It writes `Logs/Bench/scale/<Prefab>-before.json`. **Keep these.**
19. `Fixit Fidget › Checks › Intake tray - every device rests on the felt (read-only)`.
    - **Prints:** `[One scale] Intake tray check: every device rests on the felt (read-only): 3 PASS, 0 FAIL (read-only).`
20. `Fixit Fidget › Bench › Devices 2 - Fit the hero devices at real size (PhoneRepair, PocketWatch, GraceReunionCamera)`.
    - **Before:** step 5 passed.
    - **Prints:** `[One scale] Devices 2 - Fit the hero devices at real size: Done: 3 prefab(s) saved, 0 refused, the rest already real. Next: Bench 9, then the scale audit (after).`
    - For the camera:
      - a "v3: N object(s) to new places …; new: Clean shutter mechanism; no collider: Upper strap, …" line;
      - the CHANGE lines for the moves, the colliders and the shutter wiring;
      - the PASS checks.
    - Photos: `Logs/Bench/devices2-<time>/`.
    - Second run: "nothing to change".
21. `Fixit Fidget › Days › Days 1–3 - the intro level (data)`.
    - **Before:** step 20 (B refuses otherwise) and step 13; the scene is saved.
    - **Prints:** `[Days 1-3] … done (N change(s), M check(s) passed). Report: Logs/Days/days-1-3-all-<time>.txt`.
    - The camera prefab gains an inactive `Shutter mechanism (resting)`, and DayClock's Day Length Seconds is 420.
    - Any FAIL undoes everything.
    - Second run: "already done".
22. `Fixit Fidget › Bench › Bench 9 - Frame each device (prefabs)`.
    - **Before:** steps 13 and 20.
    - **Prints:** `[One scale] Bench 9 - Frame each device (prefabs): 3 prefab(s) framed, N PASS, 0 FAIL …`.
    - Per device:
      - "T9 at zoom 0: x % ≥ 34 / 35 / 17 %";
      - "T8: … ≥ 12" (phone, watch);
      - "T9 at zoom 1: ≥ 55 %";
      - "T8: one notch in … ≥ 15".
    - Photos: `Logs/Bench/framing-<time>/`.
    - Re-runnable.
23. `Fixit Fidget › Checks › Bench scale audit (read-only)`: **the AFTER run.**
    - **Prints:** `… PASS, 0 FAIL`, except the rows the scale builder names in their §6.
    - The camera's body row reads "with its lugs", 129 mm ±7 %.
24. `Fixit Fidget › Checks › Intake tray - every device rests on the felt (read-only)`.
    - **Prints:** `3 PASS, 0 FAIL`.

### Phase B: the edit-mode checks (read-only; any order after Phase A)

25. `Fixit Fidget › Checks › Bench rules`: `[Bench rules] PASS: 55 assertions (…)`.
26. `Fixit Fidget › Checks › Every device's grime can be brushed`: `[Device cleaning] PASS: N grime spots across M devices…`.
27. `Fixit Fidget › Checks › Grace camera tweezers interaction`: `[Grace camera interaction] PASS: …`.
    - After step 21.
    - The fresh part is now the clean mechanism.
28. `Fixit Fidget › Checks › Days 1–3`: `Days 1–3 check: PASS (n passed, 0 failed)`.
29. `Fixit Fidget › Checks › Day 1 onboarding`: PASS.
30. `Fixit Fidget › Checks › Grace episode and drink freshness rules`: PASS, 107 assertions.
31. `Fixit Fidget › Checks › Grace camera content`: PASS.
32. `Fixit Fidget › Checks › Upgrade economy - the ladder and the first purchase`: PASS. Patrons read about $175 on Day 1.
33. `Fixit Fidget › Checks › Dialogue rules`: PASS.
34. `Fixit Fidget › Checks › Bench words`: `[Bench words] PASS: all N checks.` This is now passable (review M2).
35. `Fixit Fidget › Checks › Waiting space reservations`: "The café scene: N passed, 0 failed".
36. `Fixit Fidget › Playtest 4 › Bench › 0 - Check first`: its cloth line now checks `BT_ClothHeld` under the cloth.

### Phase C: the Play-mode labs

Keep the Game view in front and your hands off. Each lab enters and leaves Play itself unless the step says otherwise. Minutes are the builders' own figures. Where none was given, the time is the gap between this lab's start and the next one's on 12 Oct, an upper bound; "not stated" means neither exists.

37. `Fixit Fidget › Bench › Bench 3 - The bench - play check (lab, drives itself)`: **≤ 4 min** (12 Oct).
    - `[Bench check] All N checks passed.`
    - With B1 fixed: 0 guard trips, nothing logged red.
38. `Fixit Fidget › Bench › Bench 5 - The close-up's view (lab, a check)`: **≤ 2 min** (12 Oct), a little more now with the framing pass.
    - `[Bench view check] All N checks passed.`
    - New rows: the layout is call 21's; T9 at zoom 0 (34 / 35 / 17 %) and zoom 1 (55 %); T8 ≥ 12 px.
    - **It needs steps 13 and 22.**
39. `Fixit Fidget › Playtest › Stations as reach - play check, overhead (lab, drives itself)`, then `… first person (lab, drives itself)`: **≤ 2.5 min each** (12 Oct).
    - `[Stations check] All N checks passed`: 95 and 94 at 13 Oct's counts, plus "no bench piece held a rigidbody".
40. `Fixit Fidget › Playtest › Aim help - play check (lab, drives itself)`: **≤ 7 min** (12 Oct). `[Aim help check] All N checks passed.`
41. `Fixit Fidget › Playtest › Juice - Photograph the feedback (lab, Play Mode)`: **not stated.**
    - `[Juice check] All N checks passed.`
    - Each of the four bench repairs ends once with "Fixed! · Perfect".
42. `Fixit Fidget › Playtest 4 › UI › 1 - Day 1 guide - play check (lab, drives itself)`: **≤ 2.5 min** (12 Oct). Passes (74 on 13 Oct, with patch A).
43. `Fixit Fidget › Bench › Bench 6 - Words on the bench screen (lab, a check)`: **up to ≈ 4.5 min, run twice.**
    - Run it at Game view **1920 × 1080**, then at **2560 × 1600**, with Unity in front.
    - `[Bench words lab] All N checks passed.`
44. `Fixit Fidget › Night › Night zoom - from the bins to Grace's house (lab)`: **≈ 2 min. Hands off the mouse and the pad**: it drives a virtual mouse's wheel.
    - `[Night zoom check] All N checks passed`, including:
      - "T20: on every frame where a limit eases … at most 35 m/s × the frame + 0.05 m";
      - "T20: on a frame with no zoom input the distance never moves more than 3 m";
      - the two wheel lines.
45. Press Play, then `Fixit Fidget › Checks › Controller (Play Mode) › Run controller check` with the Game view focused: **not stated.**
    - `[Controller check] PASS`, including the two R3 lines.
46. `Fixit Fidget › Playtest 4 › Bench › 2 - The dispenser's frame - play check (lab, Play Mode)`: **not stated.**
    - All N checks passed.
    - The row's slant ≈ 0°; the smallest paddle ≈ 111 px.
47. `Fixit Fidget › Cinematics › Day intro - a check (lab, drives itself)`: **≈ 3 min.** All N checks passed.
    - Then `Fixit Fidget › Cinematics › Day intro - play it (lab, Day 1 by hand, intro on)`, for his eyes and ears.
48. `Fixit Fidget › NPC › Life 4 0 - Play its lab session (Day 5, I serve)`. After the bell, run `Fixit Fidget › NPC › Life 4 - Stools, settled guests, looks (Play Mode, Life 4's lab session)`: **4–6 min.**
    - `Life 4 - Stools, settled guests, looks: PASS n/n`.
49. `Fixit Fidget › NPC › Sit 4 - Live sit check (Play Mode)`, in a lab session: **not stated.** No worse than 16/16.
50. `Fixit Fidget › Days › Lab - Day 1 at the counter (the autopilot at human speed)`, then Day 2, then Day 3: **≈ 8 min each.**
    - The report prints the medians so far.
    - The spec wants 20 runs a day eventually.
51. `Fixit Fidget › Bench › Bench 8 - The clumsy hand (lab, drives itself)`: **≈ 15 min.** For a release candidate, the `…, full` variant: **≈ 45 min**.
    - **Before:** steps 13, 21 and 22. Before step 22, a piece at rest out of frame counts as hidden.
    - `[Clumsy hand] All N checks passed.`
    - New rows:
      - "cloth: spot seen mid-wipe" ≥ 40 % on every wipe;
      - T6 strokes now counted from the hand's own strokes.
    - **If either camera repair misses its bar, the build isn't ready.**
52. **Only if Bench 8's watch runs miss:** `Fixit Fidget › Bench › Bench 9b - Frame the watch closer (its cups and the rest only; when the clumsy hand asks)`, then Bench 8 again. Bench 9 undoes 9b.
53. **Re-runs at their 13 Oct counts:**
    - Night 0 (96), Night 1 (77 / 80), Night 2 (115);
    - Grace at home (133 / 24 / 31) and her window (49);
    - `Checks › Night notebook (Play Mode, lab session)`, Recap phone (128);
    - Life 2 (15), Life 3 (22), Attention (11), Café life (32);
    - Day shape C (15), the door rushes (12 / 12), Mixamo 5 (≥ 47/52);
    - Bark and Reputation rules.
54. **By eye:** `Fixit Fidget › Playtest › Play the whole game from Day 1 (test save)`.
    - The four settled guests are already in their seats in the intro's first wide shot (review-npc-cine M1's fix).
    - Day 1's lens wipe shows the glass beyond the cloth (call 23).

---

## 4. The type check and the console tests

**Syntax.** `python3 /root/cafe/tools/cs_syntax_check.py` on every changed `.cs`: **108 of 108 OK**.

**Types: 0 new errors.**
- The tool is the Roslyn harness, `scratchpad/rcheck`: `dotnet bin/Debug/net8.0/RCheck.dll /root/cafe/build/integ ../stubs`.
- The comparison follows `scratchpad/check.sh`: by file, code and message.

| Run | Errors listed | New |
|---|---|---|
| `integ` @ `cc16dca` against `base_stub.txt` | 1,074 | **0** |
| The original tree (`431f081`'s Assets/Scripts and Assets/Editor) with the extended stubs, against `base_stub.txt` | 1,076 | **0** (the new stubs introduce nothing) |
| `integ` against that regenerated baseline | 1,074 | **0** |

- The 1,074 remaining errors are all gaps in the stubs, and every one is already in the original tree.
- Examples: `LineRenderer`, `Physics.OverlapBox`, `Keyboard` keys, `Scene ==`.
- Straight after the merge the harness showed 128 new errors, and 148 with the integration's own code. One was real (`NpcWave2Steps.cs:1010`, call 7). Every other one was a Unity API missing from the stubs, and the additions below close them all.

**The stubs I added** (`scratchpad/stubs/`; my additions are marked `// integ`; backup in `scratchpad/integ/stubs-before/`). Each is a real Unity 6 API with Unity's signature.

In `UnityEngineStub.cs`:
- `Vector4`: the implicit conversions Vector3 → Vector4, Vector2 → Vector4, Vector4 → Vector3, Vector4 → Vector2.
- `Quaternion.ToString(string)`.
- `Matrix4x4`: the fields `m00` … `m33`, `rotation`, `lossyScale`, `GetColumn(int)`, and `static Perspective(float fov, float aspect, float zNear, float zFar)`.
- `Random.State` (struct) and `Random.state`.
- `Physics`:
  - `const int DefaultRaycastLayers = -5`;
  - the two existing `RaycastAll` overloads, `(Ray, …)` and `(Vector3, Vector3, …)`, given Unity's defaults (`maxDistance = ∞`, `layerMask = DefaultRaycastLayers`, `queryTriggerInteraction = UseGlobal`).
- `RectTransform`: `offsetMin`, `offsetMax`, `GetWorldCorners(Vector3[])`.
- `SkinnedMeshRenderer`: `BakeMesh(Mesh)`, `BakeMesh(Mesh, bool useScale)`.
- `MaterialPropertyBlock`: `SetColor(int, Color)`, `SetFloat(int, float)`, `SetTexture(int, Texture)`.
- `Mesh`:
  - `isReadable`, `subMeshCount`;
  - `GetIndexCount(int)` returning `uint`, `GetIndices(int)`;
  - `GetVertices(List<Vector3>)`;
  - `GetBlendShapeFrameCount(int)`, `GetBlendShapeFrameWeight(int, int)`;
  - `GetBlendShapeFrameVertices(int, int, Vector3[], Vector3[], Vector3[])`.
- `Material.GetTextureScale(string)`.
- `Texture2D.SetPixels(int x, int y, int blockWidth, int blockHeight, Color[] colors)`.
- `Camera.clearFlags` and `Camera.backgroundColor`, with `enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }`.
- `SceneManagement.Scene.isDirty` and `SceneManager.MoveGameObjectToScene(GameObject, Scene)`.
- uGUI's graphic base, for `BenchRing` and `SupportCallIcon`, which derive from `MaskableGraphic`:
  - `UnityEngine.EventSystems.UIBehaviour : MonoBehaviour`;
  - `UnityEngine.UI.Graphic`: virtual `color`, virtual `raycastTarget`, `rectTransform`, `canvas`, `canvasRenderer`, virtual `material`, `SetVerticesDirty()`, `SetAllDirty()`, `SetMaterialDirty()`, `GetPixelAdjustedRect()`, and `protected virtual OnPopulateMesh(VertexHelper)`;
  - `MaskableGraphic : Graphic` with `maskable`;
  - `VertexHelper`: `currentVertCount`, `Clear()`, `AddVert(Vector3, Color32, Vector4)`, `AddTriangle(int, int, int)`.
  - `Canvas` and `CanvasRenderer` are left unstubbed, as before.

In `UnityEditorStub.cs`:
- `Undo.RevertAllDownToGroup(int)`.
- `AssetDatabase`: `FindAssets(string)`, `Contains(Object)`, `SaveAssetIfDirty(Object)`, `StartAssetEditing()`, `StopAssetEditing()`.
- `PrefabUtility`: `InstantiatePrefab(Object, Scene destinationScene)`, `IsPartOfPrefabInstance(Object)`, `RecordPrefabInstancePropertyModifications(Object)`.
- `EditorUtility.CreateGameObjectWithHideFlags(string, HideFlags, params Type[])`.
- `EditorApplication.CallbackFunction` (the delegate) and `EditorApplication.delayCall`.
- `SerializedProperty`: `arraySize`, `GetArrayElementAtIndex(int)`, `enumValueIndex`, `propertyType`, `serializedObject`.
- `enum SerializedPropertyType`, with Unity's values from `Generic = -1` to `ManagedReference = 24`.
- `EditorSceneManager.NewPreviewScene()`.

**The harness's blind spots.** It ignores unresolved types and names (CS0246, CS0103 and kin), so anything typed with an unstubbed Unity type is unchecked. That includes most of the Input System, TMPro, Cinemachine and `PreviewRenderUtility`. The builders' first-use risks remain, and only Unity's compile settles them:
- `PreviewRenderUtility.Render(true, false)`;
- `ModelImporter.importBlendShapeNormals`;
- Bench 8's and the night lab's virtual `Mouse` and `MouseState`.

**The console tests.**
- Every `Tests/*` project was run with `dotnet run`, at `cc16dca`, from out-of-tree copies of its `.csproj` in `scratchpad/integ/tests/`. No `bin/` or `obj/` was left in the tree.
- The first seven have sources that changed; the last five were run as well.

```
BenchRules         Bench rules PASS: 55 assertions.
ContinuationRules  Continuation PASS: 107 episode/freshness assertions and old-save JSON migration.
BarkRules          Barks PASS: 505 rule assertions. Unity integration not run.
HomeRules          Homes PASS: 38 home rule assertions; 62 notebook rule assertions still pass; 2 save-format assertions. Unity integration not run.
NightRules         Night 1 PASS: 489 rule assertions; 6 save-format assertions. Unity integration not run.
NotebookRules      Notebook PASS: 62 rule assertions; 2 save-format assertions. Unity integration not run.
StorytellerRules   Storyteller PASS: 58 timing checks; 485 memory checks. Unity integration not run.
CircuitRules       Circuit rules PASS: 58804 assertions.
GraceRules         Grace at home PASS: 1603 assertions. Unity integration not run.
HoldCallRules      Support call rules PASS: 891949 assertions.
HumanRules         Human rules PASS: 52 assertions.
ReputationRules    Reputation PASS: 19701 rule assertions; 5 save-format assertions. Unity integration not run.
```

---

## 5. What I couldn't resolve (most important first)

1. **Nothing has run in Unity.** The first proof is the Mac's compile: step 1, 0 errors. After that, the steps and labs in order. All of call 6's v3 stage, call 23's lead and Bench 8's new measure are type-checked and read through, but never executed. The weakest points:
   - **Devices 2 on the real files.** `FbxIsReal` must now accept the camera at 134 mm against 129 mm (+3.9 %). The FBX piece names (`UpperStrap`, `LowerStrap`, `WornLoop`, `CleanMechanism`) must match `devices.json`'s targets. "Every object at before × k, or at its v3 place with its children carried" must hold to 0.1 mm. If a check fails, Devices 2 saves nothing and names the object.
   - **The cloth's lead** reads its mesh's vertices and its keys' deltas. Bench tools 1 (step 2) imports the keyed meshes (BT_ClothHeld, BT_Brush) as readable, so after step 2 the lead uses the exact mesh. Before that, it falls back to the renderer bounds, which still put the far edge at the cursor but more coarsely.
   - **Bench 8's 40 % measure** uses the cloth's projected screen rectangle, which over-covers a skewed wad. A miss is therefore conservative: look at the mid-wipe photos before changing the cloth.
2. **T9 may still miss for the watch** (bar 17 %; the scale builder's model gave ≈ 17–18 % with the rest at y 0). Bench 9 names the binding item, and Bench 9b is the lever (step 52).
3. **The 11:55 reviews' Minors are not applied** (§1.4). The most visible of them in play:
   - T17's "what's left" repeating on each return to the bench (days-uiux m1);
   - pieces in cups reading as hidden (bench m2);
   - the 27-word hover over a covered part (days-uiux m2, a copy call).
4. **The intro's four new cues are silent** (`intro.cup`, `intro.steps`, `intro.car`, `street.day`) until Audio's next pass. `intro.lights` borrows `intro.lamp`'s file.
5. **`.meta` files:** 142 new assets and 6 new folders get their `.meta` from the Mac's Unity. Commit them back afterwards, as imports §2 step 12 says.
6. **Commit trailers:** 21 of my 23 commits lack the `Co-Authored-By` / `Claude-Session` lines. The instruction came after them, and I did not rewrite history.
7. **Carried open items from the builders that only Mac time closes:**
   - 20 runs of each Days lab, for the T19 medians;
   - the NPC palette alternates (wave 2 6a/6b; the Synty pack isn't in the cloud);
   - the perch clip;
   - Audio §6 step 8 by hand.
