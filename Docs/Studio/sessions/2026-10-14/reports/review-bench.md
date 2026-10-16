# Review: the forgiving bench, before the deploy (14 Oct 2026)

Reviewer, read-only. Branches `b-bench` @ `cab304e` and `b-scale` @ `f457920`, against `431f081`.

**What I read:**
- the briefs: common §3, feel targets T1–T21 and calls 1–23, contracts, Systems §4–§5, TD §5–§6, both builder reports;
- in `b-bench`, every changed runtime file in full: BenchStage, BenchRules, BenchWork, BenchFeel, Screw, RemovablePart, ReplaceablePart/LoosePart, GrimeSpot, ToolPickup, ItemInspector, RepairChecklist, DeviceDefinition, BenchStageContract, and the PadInput/PadCursor/GameSettings diffs;
- the `b-bench` labs: ClumsyBenchLab, ClumsyHand, ForgivingBenchSteps, PlayLab, and the StationsCheck, JuiceCheck, BenchViewProbe and cleaning-check diffs;
- in `b-scale`: PlayerCarry, the five guards, the run path of Devices 2 and the write path of Bench 9.

Nothing was compiled or run. Each finding was traced through the code. Prefab sizes come from the repo's YAML.

**Checked and found OK:**
- **Input timing:** a click shorter than a frame (`held = UseHeld || pressed`, ItemInspector.cs:599) and a release between frames.
- **The fresh part:** click, hold, flight, hover, press-in and the 0.5/s decay.
- **Closing mid-flight:** when the close-up closes or the day ends, `EndJob` completes the flights to the device.
- **Tools:** the cloth/brush release rule and the auto-home with `consumedUntilUp`. The cover's press vs its 0.4 s hold.
- **Edit mode:** there is no `BenchStage.Instance`, so flights land at once through `Land(null, …)`.
- **Statics:** `BenchWork` statics reset at SubsystemRegistration, and ItemInspector unsubscribes in OnDisable.
- **His mouse:** Bench 8 restores it on every exit path (Finish→Restore, and OnDisable when Play is stopped).
- **Prefab editing:** prefab contents are unloaded in `finally` in Bench 7, Devices 2, Bench 9 and the audit. Devices 2 refuses safely until wave 2.
- **Bench 7's slot search:** it finds `BenchSlots/Slot_1` before the scrap tray's `Slot_1` (I checked the scene's order).
- **Framing today:** the scene's close-up camera (58°, 0.58 m, FOV 40) frames every rest, cup, pad, well and tool rest. Bench 9 can't write until wave 2.

---

## Blocker

### B1. The guard trips on the landing hop of every cover and part (and today's watch screws)

**Where:** `Assets/Scripts/BenchStage.cs:871` (`Guard`); the hop is at `:776-784`.

**Effect:** every cover and part landing logs a red error, and Bench 3 and Bench 8 cannot pass.

**The mechanism:**
- After `Arrive`, the flight stays in the list for its 0.11 s hop, with `f.arrived = true` and `claim.landed = true`.
- `Guard()` runs after `Advance()` in the same `Update` (`:707-712`). It skips a piece only while `IsFlying`, which means `!f.arrived` (`:562-567`).
- So it reads the hop as a move. Over 2 mm (`SlotDrift`) it does three things: `GuardTrips++`, `Debug.LogError("[Bench stage] The guard set … back …")`, and it snaps the piece back. On the next frame the hop lifts the piece again, and the guard trips again.

**Hop heights from the code, with the repo's prefab sizes:**

| Piece | Rule | Hop |
|---|---|---|
| Phone back cover | 0.04 × 67.6 mm (`RemovablePart.cs:459`) | 2.7 mm |
| Phone screen | 0.05 × 62.6 mm (`ReplaceablePart.cs:246`) | 3.1 mm |
| Watch back plate | 0.04 × 129.6 mm | 5.2 mm |
| Watch mainspring | 0.05 × 48 mm | 2.4 mm |
| Watch screws, until Devices 2 | 0.25 × 12 mm (`Screw.cs:215`) | 3.0 mm |
| Grace's jammed mechanism at today's 2× (collider 79.5 × 34.5 × 49 mm) | 0.05 × 49 mm | ≈ 2.45 mm |

Every one is over 2 mm. The guard trips on 2–3 frames of each landing at 60 fps, and on most landings at 15 fps.

**What happens:**
- His Console gets a red error on every cover and part landing. If Error Pause is on, Play stops there.
- The hop never shows.
- Bench 3's "0 guard trips" (`BenchLab.cs:574`) and "nothing logged an error" fail on the phone.
- Bench 8's "no guard trips, nothing logged an error" (`ClumsyBenchLab.cs:1100`) fails on the phone and watch runs, and on the camera-swap runs. So the camera-swap gate cannot go green.

**Fix** (BenchStage.cs:871):
```csharp
// before
            if (!c.landed || IsFlying(c.piece) || !c.piece.gameObject.activeInHierarchy) continue;
// after
            if (!c.landed || HasFlight(c.piece) || !c.piece.gameObject.activeInHierarchy) continue;   // the landing hop is the flight's own motion
```
Then add this after `IsFlying` (after `:567`):
```csharp
    // Still in the flight list: in the air, or hopping on landing (the guard must not read the hop as a move).
    bool HasFlight(Component piece)
    {
        foreach (Flight f in flights) if (f.piece == piece.transform) return true;
        return false;
    }
```

---

## Major

### M1. Bench 7 writes the superseded layouts to the phone and the watch (call 21), and a re-run can't correct them

**Where:** `Assets/Scripts/DeviceDefinition.cs:111, 113`; `Assets/Editor/ForgivingBenchSteps.cs:113`.

**The mismatch:**
- The presets are the 9:30 numbers: the phone's and the watch's tool rest at `toolRestCentreY = -60`, the phone's scrap tray at (−30, 133) and the watch's at (−20, 107).
- Call 21 (11:30, after QA M1; Blender wave 2 §3) puts every rest at y 0, the phone's tray at (6, 133) and the watch's at (26, 107).
- Bench 7 writes the presets into PhoneRepair, PhoneJob and PocketWatch (`LayOutDevices`, `:422-455`). It also places the scene's rest from `ForPhone` (`:143-149`).

**Scenario:**
- The Producer runs Bench 7, and the prefabs get y −60.
- Bench 9 frames from the prefab's layout (`BenchFramingSteps.cs:377`). The rest's near end lands at mat y −175, on the frame's bottom edge and under U4.
- T9 misses. The scale builder's own model gives the phone ≈ 33 % against a 36 % bar, and the watch ≈ 17 % against 18 %.
- Walter's watch (Day 2) then opens with its tools at the frame's edge, under the time and bar HUD.
- Correcting the presets later doesn't help. A second Bench 7 takes the "Already done" branch, which calls `LayOutDevices(report, apply: false)` and returns.

**Fix:**
```csharp
// DeviceDefinition.cs:111, 113 — before
    public static BenchLayout ForPhone => new BenchLayout { rest = new Vector2(-30f, -14f), toolRestCentreX = -135f, toolRestCentreY = -60f, scrapTray = new Vector2(-30f, 133f) };
    public static BenchLayout ForWatch => new BenchLayout { rest = new Vector2(-20f, 0f), toolRestCentreX = -115f, toolRestCentreY = -60f, scrapTray = new Vector2(-20f, 107f) };
// after (call 21)
    public static BenchLayout ForPhone => new BenchLayout { rest = new Vector2(-30f, -14f), toolRestCentreX = -135f, toolRestCentreY = 0f, scrapTray = new Vector2(6f, 133f) };
    public static BenchLayout ForWatch => new BenchLayout { rest = new Vector2(-20f, 0f), toolRestCentreX = -115f, toolRestCentreY = 0f, scrapTray = new Vector2(26f, 107f) };
```
```csharp
// ForgivingBenchSteps.cs:113 — before
            string layouts = LayOutDevices(report, apply: false);
// after (a re-run brings the prefabs to today's layouts; it writes only what differs)
            string layouts = LayOutDevices(report, apply: true);
```

### M2. The held cloth covers the lens it is wiping (call 23 / QA M3 not built)

**Where:** `Assets/Scripts/ToolPickup.cs:270`.

**The cause:**
- `HoldAt` puts the cloth's origin exactly on the contact point.
- `BT_ClothHeld`'s origin is "the pad's face on the glass" (blender-wave1.md:267), which is the middle of a 44 × 49 mm pad.
- Day 1's lens spot is 19 mm across (38 mm on today's 2× camera).

**Scenario:**
- Day 1's first verb: he takes the cloth and wipes the lens.
- The pad sits centred on the cursor. The smear turning to haze, the clearing and most of the gleam all happen under it, on every frame.
- That is the opposite of call 23: the far edge at the cursor, and ≥ 40 % of the spot uncovered mid-wipe. It lands on his top complaint ("so hard to use the cloth").

**Why the fix is safe:** the rub is computed from the cursor's ray (`ItemInspector.Rub` → `spot.Contact(ray, …)`). Moving only the drawn pad changes nothing in the cleaning.

**Fix** (ToolPickup.cs:270):
```csharp
// before
        Vector3 position = tip + n * (down ? 0f : HoverHeight);
// after
        Vector3 position = tip + n * (down ? 0f : HoverHeight);
        if (PressedFlat && camera != null)
        {
            // Call 23 (QA M3): the pad sits toward the player with its far edge at the cursor, so the wiped glass shows beyond it.
            Vector3 toPlayer = Vector3.ProjectOnPlane(-camera.transform.up, n);
            if (toPlayer.sqrMagnitude > 1e-6f) position += toPlayer.normalized * (.5f * BenchRules.ClothContact);
        }
```
QA's lab half is also missing: Bench 8 photographing mid-wipe and checking that ≥ 40 % of the lens is uncovered. It belongs in `ClumsyBenchLab.ScrubSpot`.

### M3. Bench 8 doesn't measure strokes per spot (T6): `GrimeSpot.Passes` misses most of the hand's strokes

**Where:** `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs:917`.

**Why `Passes` undercounts:**
- `Passes` (GrimeSpot.cs:474-485) counts only when a frame's direction turns back by dot < −0.2.
- The start of a stroke never counts:
  - `Rub` calls `StrokeFrom` whenever the footprint comes onto a spot with a last ray (ItemInspector.cs:767). That sets `hasLast`, so `newStroke` is false.
  - A press made directly on the spot returns at `length < 1e-6` on its first frame, before counting.
- The hand moves its stroke sideways at exactly the turn. `Scrub.At` picks a new across-offset when `s` changes (ClumsyHand.cs:267-276), which is when `−cos` is at ±1.
  - The turn frame's motion is that sideways jump (0.1–1 R, tens of px), against only 1–3 px of along-motion.
  - So dot ≈ 0 on both sides of the turn frame, and the reversal isn't counted.
- At 60 fps, tremor near the turns (σ ≈ 1.7 px a frame) adds spurious reversals.

**Effect:** the bar "median ≤ 3, max ≤ 6, ≥ 90 % within 4" can pass on spots that took 6–8 strokes, or fail on noise. The lab's gate for T6 isn't measuring T6.

**Fix** (ClumsyBenchLab.cs:917): count the hand's own strokes (its half-cycles) until the spot came clean.
```csharp
// before
                if (!counted && spot != null) { run.strokes.Add(spot.Passes); counted = true; }
// after
                if (!counted)
                {
                    float hz = usePad ? ClumsyHand.PadWaggleHz : ClumsyHand.ScrubHz;
                    run.strokes.Add(Mathf.FloorToInt((Time.realtimeSinceStartup - scrub.StartedAt) * 2f * hz) + 1);
                    counted = true;
                }
```

---

## Minor

### m1. The hover outline stays on the seated fresh part for good

**Where:** `ReplaceablePart.cs:470` (`LoosePart.Seated` → `Destroy(this)`); `ItemInspector.cs:1853-1859`.

**What happens:**
- During the press-in hold, `SetBenchHover(holdTarget)` outlines the LoosePart (BenchOutline hulls on the fresh visual).
- Seating destroys the component but not its GameObject.
- On the next frame, `SetBenchHover` compares the destroyed reference with Unity's `==`, which says it equals null. `SetHighlight(false)` is never called.
- The new shutter or screen keeps the hover outline through "Fixed!" and the hand-back. It reads as "touch this next".

**Fix:**
```csharp
// before (ReplaceablePart.cs:470-472)
    public void Seated()
    {
        seated = true;
// after
    public void Seated()
    {
        SetHighlight(false);   // the outline off before this component goes; ItemInspector can't reach a destroyed one
        seated = true;
```

### m2. Pieces in the recessed cups and wells read as "not seen"

**Where:** `ItemInspector.cs:1750-1766` (`Seen`); `ForgivingBenchSteps.cs:280-285`.

**The cause:**
- The mat FBX imports with no colliders (imports branch, `BlenderKitSteps.cs:388`). So Bench 7 gives the mat a solid 600 × 5 × 360 mm box.
- Cup floors are at 1.8 mm and well floors at 2.0 mm.
- The middles of the phone's 2.4 mm cover in its well (≈ 3.2 mm) and of real-size screws in cups (≈ 3.7 mm) lie inside that box, so the camera's ray meets the mat first.

**Effect:**
- `IsSeen` returns false. UI/UX's glow then draws the "Cover on" target dashed, as if hidden (contracts §6; `BenchWork.WorkLeft`'s `hidden`).
- The cover in its well and the screws in cups can't be hovered outside their own step.
- Bench 8's `Hidden` allows 3 mm, so T13 doesn't catch this.

**Fix:**
```csharp
// before (ItemInspector.cs:1763)
            if (h.distance < nearest) { nearest = h.distance; first = h.collider.transform; }
// after
            if (h.distance > length - .004f) continue;   // a hit within 4 mm of its middle is its own bed (a cup's or well's floor)
            if (h.distance < nearest) { nearest = h.distance; first = h.collider.transform; }
```

### m3. On a pad, a tool chosen with LB/RB goes home after an A-press held off the device

**Where:** `ItemInspector.cs:706`.

**What happens:**
- §4.4.1's pad row says LB/RB tools are sticky; only a press that took the tool follows the grab rule.
- `Released` applies the rule to every press. With the cloth taken by RB, an A-press on the mat held more than 0.3 s (or moved more than 24 px) sends the cloth home, and he has to cycle to it again.

**Fix:**
```csharp
// before
        if (tool == null || tool.IsStandIn || tool.Returning) return;
// after
        if (tool == null || tool.IsStandIn || tool.Returning) return;
        if (GamePointer.PadCursorActive && !pressTookTool) return;   // LB/RB tools stay in hand (§4.4.1, pad row)
```

### m4. Bench 8 never plays T3's click-then-hold

**Where:** `ClumsyBenchLab.cs:786-854` (`HoldOn`).

**What's missing:**
- Every press on the fresh part is held until the part is replaced.
- The 10 % early let-go can't fire during the flight, because `HoldProgress` is 0 until the part hovers.
- So "a click leaves it hovering until a hold" is proved only by Bench 3's 3-frame click on the phone. Grace's Day 2 swap is never clicked.

**Fix:** make about 1 in 3 fresh-part presses a `Click(hand.ClickSeconds())`, followed by a `HoldOn` on the hovering part.

---

## Notes for the Producer (not bugs)

- **The 2× camera, until Devices 2:** its quarter-turned footprint (strap included) reaches the scrap tray. The slot rule finds no clear scrap slot and quietly falls back to Slot_3, where the old mechanism can lie under the turned camera. This goes away with the bake.
- **Bench 8 vs B1:** with B1 unfixed, Bench 8's camera-swap verdict is decided by the guard, not by the hand.
