# Scale builder — report (14 Oct, wave 2)

Worktree `/root/cafe/build/scale`, branch `b-scale`, 5 commits on `431f081`: `6113c66`, `24b3479`, `6c16da6`, `267d946`, `f457920`.

## 1. What changed

| File | | What |
|---|---|---|
| `Assets/Editor/BenchScaleAudit.cs` | new | `BenchScale` (shared by all my steps: the three devices and their k, the 3.8 mm screw constants, the run/report, the "before" snapshot, a small JSON reader for `devices.json`, measuring, a preview-scene photo studio) and **Fixit Fidget › Checks › Bench scale audit (read-only)** (TD §3/§7). |
| `Assets/Editor/HeroDeviceSteps.cs` | rewritten | **Fixit Fidget › Bench › Devices 2 - Fit the hero devices at real size (…)**: the fit (map from `devices.json` "targets", the old table as fallback) + the bake by hierarchy and rule + the `realSize` guard; refuses any device whose FBX isn't real yet. |
| `Assets/Editor/BenchFramingSteps.cs` | new | **Bench 9 - Frame each device (prefabs)** and **Bench 9b - Frame the watch closer (its cups and the rest only; when the clumsy hand asks)** (call 4). |
| `Assets/Editor/IntakeTrayRestCheck.cs` | new | **Fixit Fidget › Checks › Intake tray - every device rests on the felt (read-only)**. |
| `Assets/Scripts/PlayerCarry.cs` | changed | From above, a carried device is drawn ≥ 0.45 m long (cap 7×; cups keep 2.2×); bounds measured with `DeviceBounds.Counts`; `LiftedFrom` / `LiftedFromFor(item)` (UI/UX §5.7). |
| `BenchFeelSteps.cs`, `FaultExpansionSetup.cs`, `Playtest3Session3Steps.cs`, `Playtest4ProducerSteps.cs` | guards only | Refuse on a prefab with `DeviceDefinition.realSize` (2–5 lines each). |
| `GraceShowcaseSetup.cs` | guard only | A log line (see §4.13). |
| `*.cs.meta` ×3 | new | Fixed GUIDs for the new editor scripts (he works on two machines). |
| `JobMarker.cs` | unchanged | It already sizes its tag from its parent's lossy scale; the bake skips its subtree. |

Checked: `cs_syntax_check.py` OK on all 10 files; a Roslyn bind of the whole repo plus the bench builder's contract members (added to a scratch copy only) shows no error beyond the expected missing-Unity-type noise. A Python port of Bench 9's projection and search on Art's real-size layout finds feasible framings for all three devices (signs and conventions sound).

## 2. The Producer's Unity steps, in order

**Integrate the bench builder first.** My files call `DeviceDefinition.realSize`/`benchLayout`, `BenchStage`'s public fields and `MatToWorld`, and `BenchStageContract` exactly as contracts.md §1–3 gives them. They won't compile without those.

0. Compile, then **Checks › Pick up scripts changed from outside (reimport)**.
1. **Checks › Bench scale audit (read-only)**, the BEFORE run. Any scene; the stage and zone rows need the café scene open. It prints `N PASS, M FAIL`. These rows FAIL by design: the camera's body across (≈ 248 mm), the watch's case (≈ 150), the screws (8 and 12 mm heads), the holes (1.0 × head), the crown, back plate and mainspring, the camera and watch ÷ driver ratios, and "framed by Bench 9" ×3. It writes `Logs/Bench/scale/<Prefab>-before.json`. **Keep these files**: the after run compares against them.
2. **Checks › Intake tray - every device rests on the felt** (café scene open). Today: phone +1.2, watch +0.3, camera 0.0 mm → 3 PASS.
3. Optional now, safe: **Bench › Devices 2**. It prints `Nothing changed: 3 device(s) refused (their FBX aren't real size yet…)`, with each refusal's numbers (e.g. "its body is 248.00 mm across (real 120.00 mm: 2.07x)").
4. Optional now, after Bench 7: **Bench › Bench 9** (café scene open). This is a dry run while the prefabs aren't real size. You get the numbers and photos in `Logs/Bench/framing-<time>/`; nothing is written.

**When the Blender lead's wave 2 is copied in** (`HD_*.fbx` + `devices.json`):

5. **Bench › Devices 1** (the imports builder's) → PASS.
6. **Bench › Devices 2** → `Done: 3 prefab(s) saved…`. Per device you get the CHANGE lines of the fit, then "the bake × k, N line(s)" listing every object with its place before → after and its kind (hero piece / stand-in shaped by its scale / grouping node / logic / job tag left alone). Then each screw "back on its surface", `domeRadius`/`seatDistance`/`snapDistance` × k, `realSize: false → true`, the new `restHeight`, and PASS checks. Photos: `Logs/Bench/devices2-<time>/<label>-1-before.png`, `-2-after.png` (each device beside the real screwdriver at 58°). A second run reports "already real" and changes nothing, so it is safe to repeat. A prefab is saved only after its own checks pass. Prefab contents have no Unity Undo; git and the snapshot are the undo.
7. **Bench › Bench 9** (café scene open, Bench 7 done). It writes `cameraAim`, `cameraDistance`, `closeAim`, `fovClose` on each prefab, one Undo group each ("Bench 9 - frame the phone"). The search runs about 1–3 s per device. Photos: `<label>-zoom0-16x9.png`, `-zoom0-16x10`, `-zoom1-…`. Zones are orange, the device cyan, compartments green, or red when one fails.
8. **Checks › Bench scale audit**, the AFTER run → every row PASS. The exceptions are those named in §6.
9. **Checks › Intake tray** → 3 PASS.
10. **Only if Bench 8 (the clumsy hand) says the watch is too small: Bench › Bench 9b.** It re-frames the watch with only its screws' cups and the tool rest required. Its "LEFT OUT" lines name what goes off-frame (T13's risk). To go back, run Bench 9 again.

## 3. Labs and checks, with pass lines

- **Bench scale audit (after):** `… PASS, 0 FAIL`. Rows covered: bodies within ±7 % of real; screws Ø 3.80 × 1.33 ± 0.1 mm (box and mesh); hole = 1.15 × head ± 0.1 mm; every object at before × k ± 0.1 mm (screws by the face their head sits on); 0–1.2 mm over the felt; carried ≥ 0.45 m; framed by Bench 9; the stage 600 × 360, rest 110 × 230, tray 160 × 90; U1–U6 at 1080p equal to UI/UX §5.8 ±1 px.
- **Bench 9:** "T9 at zoom 0: x % ≥ 36 / 38 / 18 %", "T9 at zoom 1: ≥ 75 / 58 / 60 %", "T8: head ≥ 12 px at zoom 0", "≥ 15 px one notch in" (phone, watch).
- **Intake tray:** 3 PASS.
- Existing **CarryInteractionChecks** stays green (cups keep `overheadScale`).
- Bench 5, Bench 3, Stations, Day 1 guide, Grace tweezers and content, Juice, Aim help: the bench builder's. They re-run green after the scale (TD §7).
- In play: from above, carry the watch. It should read as an object about 0.45 m long (≈ 6.4×), and the phone about 3×.

## 4. Deviations from the spec, and the spec against the code

1. **Rest height: a surface rule, not × k (TD §6.4, §3 "after .010 / .0083 / .002").** The felt is 3.0 mm over the slot point and doesn't scale (`Playtest4TraySteps.cs:23-27`; the watch's 0.025 = 21.7 + 3.0 + 0.3 mm, `Playtest4ProducerSteps.cs:11-14`). Scaling by k would sink the watch 1.9 mm and the camera 1.5 mm into the felt, failing the TD's own 0–1.2 mm bar. Devices 2 instead sets `restHeight = 3.3 mm − lowest point`: every device sits 0.3 mm over the felt.
2. **Screws keep the face their head sits on, not their centre.** The Producer's 3.8 mm head is 1.33 mm tall, not the old head × k. Placed by its centre, the phone's head would hover 0.135 mm off the glass and the watch's 0.23 mm off the plate. The audit's "before × k" check compares screws the same way.
3. **Screw head 3.8 mm** (call 3): the head box is (.0038, .00133, .0038) on both devices; the watch's three case boxes are ÷ 3.
4. **The 9 Oct "moves" are dropped.** They are already in the prefabs, and the phone's screw moves would undo rule 2.
5. **The fit's map comes from `devices.json` "targets"** (wave 2 renames travel with the manifest), with the old table as fallback. A known prefab piece missing is a FAIL. A new export piece the prefab doesn't have yet (Art A7's clean mechanism) is reported, not made: its wiring is the bench and days builders'.
6. **Devices 2 refuses a device whose FBX isn't real.** The test is its body within ±7 % of the real one, and a screw head of 3.8 ± 0.1 mm. Until wave 2 it is a no-op. k is the prefab's authored scale (1, ⅓, ½), whatever the FBX says.
7. **Undo:** Devices 2 edits prefab contents (Unity has no Undo there; precedent `Playtest4Shared.EditPrefab`). Bench 9 writes through `SerializedObject` with one Undo group per prefab.
8. **Bench 9's 20 px margin is the frame's edges only; the zones are tested by overlap.** I read the TD's scratch model (`framing_np.ok_rect`), which produced the numbers T9's bars came from. Margins on the zones as well cost about 2 points.
9. **"The pose it opens in" is as set down on a bench slot**, which carries the rig's turn. The phone and watch open long side up the view, the camera across with its lens to the left, matching Art A1. Fill is measured there; the constraints are checked at all 8 poses.
10. **Compartments:**
    - cups by Systems' slot rule (Ø 18 + 6 mm clear of all four resting footprints; the first N free; phone → 4–6);
    - the smaller pad or well that fits (≤ 46 mm on both sides);
    - the scrap tray when the device has a replaceable part;
    - the tool rest always, including the tools (read from the scene, moved to `toolRestCentreX/Y`).
11. **Audit rows reported, not checked**, because a box can't judge them: the camera's height and depth (top-plate controls; Art A6 deepens it), the strap's width (A6 drapes it in an S), the shutter mechanism (its lever; 39.8 mm), the watch's 10.2 mm thickness (the TD keeps it), and the lens (Art's call).
12. **Found in `PlayerCarry.cs:264-266`:** the carried item's bounds counted the job tag's sprites. From 34 m the tag is about 0.4 m across, so it became the watch's "largest side" and §6.8's rule would have drawn a real watch at 2.2× (11 cm). It now uses `DeviceBounds.Counts`, the bench's rule since 12 Oct. That also centres the device itself in the hand.
13. **GraceShowcaseSetup:** it only ever builds a camera when none exists, so there is nothing to refuse. The guard is a log line saying a real-size camera is left as it is.

## 5. Patches for other owners

**(a) uiux — `Assets/Scripts/DropInteractable.cs`** (UI/UX §5.7, reads my `PlayerCarry.LiftedFrom`).

Old:
```csharp
    public const string IntakeTrayWords = "Put it on the intake tray";
```
New:
```csharp
    public const string IntakeTrayWords = "Put it on the intake tray";
    /// <summary>E at the intake tray with a device lifted from it (UI/UX §5.7). PLACEHOLDER COPY.</summary>
    public const string PutBackWords = "Put it back";
```
Old:
```csharp
            if (!spot.CanAccept(carry.Carried)) return "No room here";
```
New:
```csharp
            if (!spot.CanAccept(carry.Carried)) return "No room here";
            // A device lifted from this same tray goes back to it (UI/UX §5.7, First Hour #4).
            if (spot.Kind == DropSpot.SpotKind.Counter && carry.LiftedFrom == spot) return PutBackWords;
```

**(b) cine — `Assets/Scripts/StationInteractable.cs`** (apply with (a)).

Old:
```csharp
            if (!dropSpot.CanAccept(c.Carried)) return "No room here";
```
New:
```csharp
            if (!dropSpot.CanAccept(c.Carried)) return "No room here";
            if (dropSpot.Kind == DropSpot.SpotKind.Counter && c.LiftedFrom == dropSpot) return DropInteractable.PutBackWords;   // UI/UX §5.7
```

**(c) bench — a contract note, not a patch: `ItemInspector` must place `CM_InspectCam` exactly as Bench 9 assumes.** In the mat frame (`matFrame`'s pose):
- aim = `MatToWorld(cameraAim)` at height 0;
- camera at aim − (`matFrame.rotation × Euler(58, 0, 0) × forward`) × `cameraDistance`, FOV 40;
- zoom z: aim lerps to `closeAim`, the distance stays, FOV = 2·atan(tan 20° ÷ M^z) with M = tan 20° ÷ tan(fovClose ÷ 2);
- the device's box middle sits over `MatToWorld(rest)`, its lowest corner on the mat's top, opening at its set-down yaw squared.

Also needed:
- `MatToWorld` must be affine and work in Edit mode (Bench 9 samples it at (0,0), (1000,0) and (0,1000));
- the tool rest's and scrap tray's origins must be their centres.

## 6. Risks

**Compile (least sure first):**
1. `PreviewRenderUtility.Render(true, false)`, the two-argument overload. The repo only calls `Render(true)`. If it errors, use `Render(true)`: with a landscape frame the FOV isn't altered.
2. `Matrix4x4.rotation` / `.lossyScale` (Bench 9's photo proxies).
3. Any drift from the contract shapes (`benchLayout` read with `var`; no null checks, so a class or a struct both compile).
4. The audit calls `Screw.Awake` and `ScrewSocket.Make` by reflection. A changed signature is reported in the report, not a compile error.

**Behaviour:**
1. **T9 will probably miss for the phone and the watch by 1–4 points.** This is a data conflict, not a code bug. `toolRestCentreY = −60` (feel targets §3) is needed because at y 0 the rest overlaps the scrap tray (rest x −190…−80 against tray x −110…+50, both reaching y 88–115). But it puts the rest's near end at mat y −175, on the frame's bottom edge and under U4 at the TD's framing. The TD's model had the rest at y 0.

   My scratch model, with the TD's rules:

   | | Rest at y −60 (as specified) | Rest at y 0 |
   |---|---|---|
   | Phone | ≈ 33 % (bar 36) | ≈ 37 % |
   | Watch | ≈ 17 % (bar 18) | ≈ 18 % |
   | Watch with 9b | ≈ 19.5 %, leaving off the scrap tray, Well_Small and Pad_Small | — |

   Bench 9 measures the real meshes and names the binding item. The fix, if wanted, is data: move the phone's and watch's `scrapTray` about 40 mm to +x so the rest can sit at y −30 to 0.
2. **16:10** binds the phone's cover well at the mat's right edge (+262 mm). It goes through the bench builder's `BenchStageContract.Rect(…, 2560, 1600)`, whose 16:10 anchoring I could only guess at.
3. **At 15 fps:** PlayerCarry adds no per-frame work (the factor is fixed at pickup). The editor steps aren't frame-bound.
4. If the bench builder opens devices at a different yaw, only Bench 9's fill figure (not its constraints) is off.

## 7. Open items, in order

1. Devices 2 is untested on real files: wave 2 isn't here. If pieces are renamed, `devices.json` "targets" must carry the new names.
2. The tip-cross row needs `bench_tools.json` to carry `tip_cross` (the Blender lead). Until then it is an info line.
3. The hole row depends on the bench builder's `ScrewSocket.Make(screw, parent, localPos, localRot)`.
4. Spare bench slots in front of the mat (TD §6.9) are Bench 7's.
5. SMAA (TD §8) waits on the turning photos.
