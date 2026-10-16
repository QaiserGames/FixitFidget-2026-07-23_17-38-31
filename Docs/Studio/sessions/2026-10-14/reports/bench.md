# Bench builder: the forgiving bench (14 Oct 2026, wave 2)

Worktree `/root/cafe/build/bench`, branch `b-bench`, 5 commits on `431f081`:
- `477a03f` A1: places, flights, screws, covers, the swap, BenchRules, the contract, Bench 7;
- `d0c596e` A2: the picker, direct grab, press pass, feel, what's left, checklist fields, lens zoom;
- `9352a9d` A3: the clumsy hand, Bench 8, §6's lab changes;
- `bcda6e4` Bench 2's click box measures skinned meshes (imports' note 5.2);
- `cab304e` Bench 8 switches his mouse back on even if Play is stopped mid-run.

What I checked:
- Every `.cs` passes `cs_syntax_check.py`.
- A Roslyn bind against Unity stubs shows 0 new errors on my branch.
- Merged with the other six branches' heads (460 files), the bind shows no error at any call into my files. What remains are gaps in my Unity stubs.
- `Tests/BenchRules` runs: `Bench rules PASS: 55 assertions.`

Nothing has run in Unity.

## 1. What changed

| File | | What |
|---|---|---|
| `BenchRules.cs` | new | Pure maths: hit areas and the 4 priority classes, the flight (arc, timing, ease, hop), the slot rule, the press pass, smear, the 90 % finisher, stroke speed. Self-check shared with the console test. |
| `BenchStage.cs` | rewritten | The mat's places (6 cups, 2 pads, 2 wells, 3 scrap slots, 5 tool seats) and their claims. The rest and tray move to each device's layout. One flight scheduler: closed form, game time, cues on the first and last frame; lands at once in edit mode. The guard; `MatToWorld`/`WorldToMat`/`MatRotation`. Magnet, tray zone and `Watch`/`Forget` deleted; `Mat`/`Tray`/`Caddy`/`ToolSlot` kept. |
| `BenchStageContract.cs` | new | U1–U6 in pixels at any size, `ZoneAt`, `OverlapsHud`. |
| `BenchWork.cs` | new | `Flash`, `WorkLeft`, `Progressed` (≤ 4/s), `TargetDone`, `Flashed`, `LastProgressAt`. |
| `BenchFeel.cs` | new | Stroke speed s (60 ms closed form), rub loops, squeak, budge (≤ 6 px, ω 20), rumble. |
| `DeviceDefinition.cs` | changed | `realSize`, `benchLayout` (`BenchLayout`, presets, `LayoutOf`). |
| `Screw.cs` | changed | Quarter-turn ticks. Free = a pop and a flight to its cup. Fetched back from the cup. Art's hole decal (1.15×/0.48× the head; a drawn fallback until `Bench/BT_ScrewHole` lands; no floor). No rigidbody. |
| `RemovablePart.cs` | changed | Pry and pop kept; a beat, a lift, a flight to its well. A press sends it home (a 0.4 s hold over unfinished work). Drag, drop and physics deleted. |
| `ReplaceablePart.cs` (+`LoosePart`) | changed | A pinch, then the lift and a flight to the scrap tray. The fresh part waits on its pad: one press flies it over, a hold presses it in (0.35 s). Scrap can't be moved. |
| `GrimeSpot.cs` | changed | Press pass with a relative radius (≥ 10 px), smear first, 90 % finisher, gold `Flash`, `CoveredBy`, `PartName` ("Lens"). The squeak rule replaces the per-frame `cloth.wipe`. |
| `ToolPickup.cs` | rewritten | Direct grab with the tip exactly at the cursor and the turn eased (ω 14). Hover height. Held cloth with Press/Bunch; brush Bend; mirrored at reversals. Home by a 0.2 s flight (`tool.down`), with a lift first after the last spot. |
| `ItemInspector.cs` | rewritten | The picker; tap/drag/release grab rule; the rub through `GrimeSpot.Pass` (the pad orbits in place); holds on the new verbs; BenchWork events. Lens zoom placed from the layout (TD §5.3, §6.7). Hover words (UI/UX §5.10 item 7). D-pad reaches hidden step targets. Pad friction. T17's 3 s flash. `IsSeen`, `AimPoints`. |
| `RepairChecklist.cs` | changed | `Step.verb`, `icon`, `targets`, `left`, `total`. |
| `PadInput.cs`, `PadCursor.cs`, `GameSettings.cs` | changed | `Rumble`/`StopRumble`; `Friction`; `Vibration` (PlayerPrefs, default on). |
| `BenchInteractable.cs` | changed | `PointAtDepth` removed. |
| `Editor/ForgivingBenchSteps.cs` | new | Bench 7, Bench 8 (+ Full), Checks › Bench rules. |
| `Editor/Playtest3Session4Steps.cs` | changed | Bench 3 refuses before Bench 7; `LocalBounds` counts skinned meshes. |
| `Editor/DeviceCleaningReachabilityChecks.cs` | changed | Picker eligibility at 4 quarter turns × 2 faces; strokes through the press pass. |
| `Diagnostics/ClumsyHand.cs`, `ClumsyBenchLab.cs` | new | Systems §5. |
| `BenchLab.cs`, `BenchViewProbe.cs`, `StationsCheck.cs`, `AimHelpCheck.cs`, `JuiceCheck.cs` | changed | Systems §6. |
| `Tests/BenchRules/` | new | Console test. Plus `.meta` files (fixed GUIDs) for the 6 new scripts. |

Unchanged: `GamePointer`, `JobBase`, `Juice`, `InspectableItem`, `BenchViewProbeMenu`. No save field, scene YAML or prefab YAML was touched.

## 2. The Producer's Unity steps, in order

0. **Merge bench first.** Scale and uiux call its contract code.
1. Compile, then **Checks › Pick up scripts changed from outside (reimport)**.
2. If possible, run the imports builder's **Bench tools 1** and **Bench stage 1** first, so Bench 7 places Art's models. Without them it builds stand-ins.
3. **Bench › Bench 7 - The forgiving stage (scene).** The café scene must be open, outside Play, with Bench 2's stage present (it refuses otherwise).
   - Prints `[Bench 7] Done: BM_RepairMat, BT_ToolRest and BT_ScrapTray under InspectRig/Stage (v2)… Device layouts: 4 set…`.
   - Report and four photos in `Logs/Bench/forgiving-stage-<time>/`.
   - In the report, check:
     - "Tools onto the rest's seats: 5 moved";
     - "3 switched off" (the v2 slab, tray and caddy);
     - "worst N mm (must be under 1 mm)";
     - `faceSnapWithin: 22 -> 45`;
     - three device slots moved to mat y −265.
   - An error here means the FBX lacks Art's empties or came in turned.
   - A warning "no bench top within 15 mm under …" names a device slot for Environment to confirm.
   - The scene gets one Undo group. The prefabs' layouts have no Undo (git reverts them).
   - Safe to run twice: the second run says "Already done". Run it again once the FBX land, and it swaps the stand-ins.
4. The days builder's **Days 1–3 - the intro level (data)**. Bench 3's act three and Bench 8's camera-swap runs need the camera's two faults.
5. With the days merge, apply **§5.1** below to their tweezers check.
6. With the uiux merge, apply uiux's **patches D and E** to my `BenchViewProbe.cs` and `BenchLab.cs`. They apply exactly as written to `cab304e`. Not before: they read members only their `BenchCards` has. Also apply their **F** (the Vibration row; `GameSettings.Vibration` exists).
7. Scale's **Bench 9** (after Bench 7, when the devices are real size), then repeat §3's steps 4, 5 and 9.

## 3. Labs and checks, in order

1. **Checks › Bench rules**: `[Bench rules] PASS: 55 assertions (…)`.
2. **Checks › Every device's grime can be brushed** (edit mode): `[Device cleaning] PASS: N grime spots across M devices…`.
3. **Checks › Grace camera tweezers interaction** (after §5.1): `[Grace camera interaction] PASS: …`.
4. **Bench › Bench 3** (scene open): `[Bench check] All N checks passed.`
   - Screws fly to their cups with `screw.drop`; the cover to its well; the scrap to the tray.
   - The fresh part: a 3-frame click, then a hold. The cover home by a press.
   - Act two: the brush by direct grab (≤ 4 strokes), the cloth by RB (smear first), each tool home after its last spot, the budge ≤ 6 px and back within 0.3 s, `Flash`.
   - Act three: Grace's camera by its verbs, one "Fixed! · Perfect" per fault, the strap kept. Before the days data step it plays the clean and the swap as one job and says so.
5. **Bench › Bench 5**: `[Bench view check] All N checks passed.` Every place a device's pieces can use, the tools, pad and scrap slots must be in frame, clear of U1–U5 and not under the device, at every quarter turn and on both faces. **Expect place FAILs until Bench 9 frames the devices.** Each one names the place, the turn and the zone.
6. **Playtest › Stations as reach** (overhead, then first person): `[Stations check] All N checks passed.` New line: "no bench piece held a rigidbody at any point of the work".
7. **Playtest › Aim help**: `[Aim help check] All N checks passed.` New lines: the cursor moves ≤ 0.6× as fast over a step target, and D-pad right reaches a screw hidden behind a block.
8. **Playtest › Juice**: `[Juice check] All N checks passed.` Each of the four bench repairs ends once with "Fixed! · Perfect".
9. **Bench › Bench 8 - The clumsy hand** (scene open, after Bench 7 and the days data step). It enters Play itself; about 15 minutes. **Full** runs three seeds each, about 45 minutes, for a release candidate. Keep the Game view in front, hands off; his mouse is switched off during the mouse runs.
   - Pass: `[Clumsy hand] All N checks passed.`
   - Output in `Logs/BenchClumsy/<time>/`: `report.txt` (16 runs side by side, then each step), `timeline.csv`, a photo per run.
   - The bars, every run: finished and Perfect within 45 / 30 / 75 / 110 s; 0 lost or hidden; every near-miss inside a hit area acts; ≤ 3 misses a step; 0 resets; strokes median ≤ 3, max ≤ 6, ≥ 90 % within 4; 60 vs 15 fps within 25 % per step; 0 guard trips and errors.
   - **If either camera repair misses its bar, the build isn't ready.**
   - The run that decides is the one after Bench 9: before it, a piece at rest out of frame counts as hidden.

## 4. Deviations, and the spec against the code

- **Step 11 and step 17 aren't mine.**
  - Step 11 (the camera's two faults) is the days builder's.
  - Step 17's `SoundBank.Defaults` belongs to imports (contracts §8/§10); their report adds the 15 entries.
  - Step 15's `verb`/`icon`: I built them, because contracts §5 gives them to bench. The spec had UI/UX adding them.
- **Device slots go in front of the mat** (TD §6.9: mat (−150 | −15 | +120, −265), outside every close-up frame), not "past the mat's right edge, behind the scrap tray" (Systems §7 step 14). The TD's rule is the later one; Bench 7 checks that the bench top is there.
- **Bench 7 also writes the prefabs' layouts** (rest, tool rest, tray; step 3) for PhoneRepair, PhoneJob, the camera and the watch. Bench 9 writes the camera fields.
- **A covered replaceable part stays hoverable** so the hover can say "Held in by the back cover…" (Bench 3 checks this line), but a press on it does nothing. Spots under a cover are out of the picker entirely, as §4.6 says.
- **With nothing in hand the hover never adds "Needs the …"**; only a blocked part gives its why-not. This keeps uiux's S1 at 25 words (their open item 2).
- **T17**: "scrubbing" means a rub tool pressed and moving faster than 40 px/s. "First repair" means Day 1 with 0 repairs, as uiux reads it. Both sides listen to `Flashed`, so at most one automatic flash per job.
- **Bench 8 uses one fresh lab save per session, not one per run.** Each run gets a full day ahead (`DayClock.TimeRemaining` via its private setter) and an empty café. Reloading 16–48 times would cost his Mac minutes per run.
- **15 fps** is a sleep cap jittered to 12–18 fps. 60 fps is vSync 0 at target 60. Both are restored, now even when Play is stopped.
- **Spec line drift:** the tweezers lines that §6 places at `:106-107` are at `:101-102` and `:118-119` in the days builder's version.

## 5. Patches for other owners

**5.1 days: `Assets/Editor/GraceRepairInteractionChecks.cs`.** In edit mode a pinch held 0.3 s finishes (0.25 s pinch). The flight lands at once, so the part is already in the tray (Removed), and scrap can't be moved (decision 15). I bound the patched file against the merged tree: no new error.

Before:
```csharp
            Require(shutter.State == ReplaceablePart.PartState.HeldBroken && shutter.transform.parent == null,
                "Holding the tweezers on the shutter pinches the jammed mechanism out of the camera.");
```
After:
```csharp
            Require(shutter.State == ReplaceablePart.PartState.Removed
                    && (shutter.transform.parent == null || BenchStage.Instance != null && BenchStage.Instance.PlaceOf(shutter) != null),
                "Holding the tweezers on the shutter pinches the jammed mechanism out; it flies to the old-parts tray (edit mode: at once).");
```
Before:
```csharp
            Require(shutter.Prompt == "Hold to move it" && shutter.Grabbable && !shutter.Holdable && shutter.RequiredTool == ToolType.Hand && shutter.DisplayName.StartsWith("Old "),
                "A finished shutter's old mechanism is scrap: a loose piece any tool moves, not another step (8 Oct: so it can never bury the fresh part).");
```
After:
```csharp
            Require(shutter.Prompt == "" && !shutter.Grabbable && !shutter.Holdable && !shutter.CanInteract && shutter.DisplayName.StartsWith("Old "),
                "A finished shutter's old mechanism is scrap: it lies in the old-parts tray and can't be moved (the forgiving bench, Systems' decision 15), never another step.");
```
I read the rest of their check against my code and it holds: the reflection names (`ResolveBenchHit`, `HandleBenchPress`, `focusedItem`, `ClearTool`, `Awake`), `CoverState.Loose`, `freshVisual`, `PresentFresh` leaving the fresh part unparented, `SeatFresh`, and the edit-mode `Activate`/`Scrub` used by "Grace camera content" and "Days 1–3".

**5.2** uiux's D, E and F: see §2 step 6. Nothing else needs a patch.
- Cine's bench shots are compiled out. When they are re-aimed: `BenchStage.Tray` is now the scrap tray and screws land in `cups`.
- Scale's contract note (c) matches `ItemInspector.PlaceCamera`/`HandleZoom`/`Place`. `MatToWorld` is affine and works in edit mode, and both stand-ins have their origin at the centre.

## 6. Risks

**Most likely to fail to compile** (first use in the repo; everything else is type-checked):
1. Bench 8's virtual mouse: `new MouseState { position, delta }.WithButton(MouseButton.Left, b)`, `InputSystem.AddDevice<Mouse>()`, `DisableDevice`/`EnableDevice`, `MakeCurrent()`. `KeyboardState` and `QueueStateEvent` are already used in the repo.
2. `Gamepad.SetMotorSpeeds` (PadInput.Rumble).
3. Bench 7's `PrefabUtility.LoadPrefabContents` with `FindProperty("benchLayout.rest")`.

**Most likely to misbehave at 15 fps:**
- The pad's rub in place: the orbit moves 48° a frame, so it covers a little less path and cleans a little slower. It shows in Bench 8's pad strokes. The number to tune is `BenchRules.PadOrbitTurns`.
- Stroke speed is smoothed over 60 ms against 67 ms frames, so the rub loop's volume and the cloth's bunch step frame to frame. Mirroring resets the keys, so no stale fold shows.
- Screw flights last 3–5 frames: steppy, but they land exactly.
- Holds and sub-frame clicks are safe: a press counts for at least its own frame.

**Other:**
- Bench 8's bars are guesses until the Mac runs them. If the watch is the only miss, scale's Bench 9b is the lever (Producer's call 4).
- `bench.showleft` is requested by both `BenchWork.Flash` and uiux's `BenchGlow`. The bank's 0.05 s gap plays it once.
- Stopping any PlayLab mid-run leaves its virtual pad until the next domain reload (true of every lab).

## 7. Open items, in order

1. **Bench 8 doesn't time each action's cue against its frame** (T4/T14's "cue timing"). It checks that the retired cues stay silent. The flight plays its cues on its first and last frame by construction (`BenchStage.Launch`/`Land`), and Bench 3 checks that each cue sounded.
2. The spacing warning in Systems §8 isn't in `BenchRules`' test: on each prefab, same-step targets should be ≥ 1.1 × their summed radii apart.
3. Re-run Bench 7 when `BM_RepairMat`, `BT_ToolRest`, `BT_ScrapTray` and `BT_ClothHeld` land, and check that `Resources/Bench/BT_ScrewHole.png` is in.
4. Bench 5 and Bench 8 give their real verdict only after scale's Devices 2 and Bench 9.
