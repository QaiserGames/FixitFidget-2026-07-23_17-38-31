# Review of builder C (23 Oct 2026): the coats out, the phone's tray and mat

I review builder C's work. I did not see it built. The work is the uncommitted diff in `/root/wt23/C`, against 76df1b4 (= his Mac's 8033112).
- New: `Assets/Editor/ArtTidySteps.cs` (Art tidy 1) and `Assets/Editor/BenchStageTidySteps.cs` (Bench 7b).
- Changed: `BenchFramingSteps.cs` (Bench 9), `CafeLivedInSteps.cs` (the guard) and `ForgivingBenchSteps.cs` (Bench 7).

Phase 1 was written before I opened the builder's report. Phase 2 checked the report's claims. Each finding says which phase found it.

## Findings, worst first

**1. [Major] `Assets/Editor/BenchFramingSteps.cs:862` — the near-edge rule re-frames the camera too, and the report doesn't say so.**
- **What's wrong.** The rule also re-frames Grace's camera, the Day 1 hero repair whose layout view the spec used as the reference. The report doesn't tell him (P1; P2 confirms).
- **The evidence.**
  - Only a plain framing that meets *rule 1* is kept (`if (Strict.Keeps(plain.cut)) return plain;`).
  - Every other cut, including "the numbers whole" (rule 2), goes through `Solve(Strict)` first. Its framing is written whenever it keeps the bars.
  - The report says "The camera already meets rule 2" (C-builder-report.md:42). By this code that is not enough to keep it.
  - The camera has no screws (no T8). Its plain fill is about 40 % against a 35 % bar, and its zoom-limit fill is set by the strap, not by the distance. So rule 1 is almost surely "Taken": the camera about 15–25 mm further back and about 3–5 % smaller, with a sliver of bench at the bottom.
  - The watch, the device Bench 9b exists to make bigger, goes to rule 1 by the builder's own model (about 20 % → 19 %).
  - So all three prefabs change. Decision 3 (:47) quantifies only the phone. The Bench 8 fallback note (:70) names only "a phone or watch run". Bench 8 is the gate, and its camera clean and swap runs will play on a framing nobody has run.
  - This is more than the spec's "smallest fix".
- **The smallest fix.** Either keep a plain framing that already meets rule 2, so only a true half ruler is re-framed:
  ```csharp
  if (Strict.Keeps(plain.cut) || NumbersWhole.Keeps(plain.cut))
  {
      run.Check(true, $"the near edge reads at zoom 0: {EdgeWords(plain.cut)}");
      return plain;
  }
  ```
  Or keep the code and fix the report:
  - Decision 3 says the camera is re-framed too, with its expected size change.
  - The Bench 8 note covers the camera runs.
  - He decides.

**2. [Major] `Assets/Editor/BenchFramingSteps.cs:133-136` — the undo re-runs the old search; it doesn't restore the old values.**
- **What's wrong.** "Bench 9 - Undo the near-edge rule" re-runs the plain search. It does not put back the values the rule overwrote. The report claims it "gives exactly the old numbers" (:42); that is unverified (P1; P2 confirms).
- **The evidence.**
  - It is exact only if every input to the search is what it was when Bench 9 last wrote the prefabs. The inputs are the three prefabs' measured shapes, `BenchStageContract`'s zones U1–U6, the rest with its tools, the tray and the mat.
  - That last write was about 14–15 Oct (the 19 Oct reviewers used its `Bench__framing` photos).
  - The report's "Plain (now)" row (:38) is the builder's own model. The stored values aren't in the copy.
  - The overwritten values survive only as text: the "was" part of `Write`'s report line.
  - The same gap makes §5 step 2 wrong (:61). A near-edge FAIL leaves the framing "as the plain search frames it", not "as it was".
  - If today's HUD work moves the bench zones, the "undo" writes a new framing.
- **The smallest fix.**
  - In `Write`, before the first change, save the four fields (`cameraAim`, `cameraDistance`, `closeAim`, `fovClose`) per prefab path to `Logs/Bench/framing-before-near-edge.json`, if that file doesn't exist yet.
  - Make `FrameEachPlain` restore from it.
  - At least, print one line per device: `the plain search gives the stored framing: {(stored == plain ? "yes" : "NO: stored aim … at … m")}`. Then the integrator knows whether the undo is exact.

**3. [Minor] `/root/review23/reports/C-builder-report.md:63` — Bench 8's stated pass is wrong.**
- **What's wrong.** The report says Bench 8 passes with "[Tag] All N checks passed … (220)" (P2).
- **The evidence.** On 20 Oct Bench 8 was 213/220, with seven known misses: pad timing parity, pad strokes 4 vs 3, one hidden frame, one near-miss (`Docs/Studio/sessions/2026-10-20.md`, lab table). Told to expect "All 220", the integrator will read the usual result as a regression.
- **The smallest fix.** Pass = the same seven known kinds and nothing new. It may be six: the "hidden" frame may have been the v2 walls standing in front of the well.

**4. [Minor] `Assets/Editor/BenchFramingSteps.cs:131` — Bench 9b now runs the near-edge rule too.**
- **What's wrong.** `FrameWatchCloser() => Frame(true)` defaults `nearEdge` to true. So 9b, the Producer's call 4 "closest default" for the watch, is pushed back again to show the mat's edge (P1).
- **The evidence.** The report's "If the watch was ever framed by 9b, run 9b after" (:61) no longer brings back the closer framing he had.
- **The smallest fix.** `static void FrameWatchCloser() => Frame(true, false);`

**5. [Minor] `Assets/Scripts/DeviceDefinition.cs:116` (found by `BenchStageTidySteps.cs:331-346`) — the phone's old-parts tray has no margin at the far edge.**
- **What's wrong.** It sits 0.9 mm in from the mat's far edge (`ForPhone.scrapTray` (6, 133)). The spec asks for "inside the mat's border with a margin" (P1; P2: the report lists it at :51 but offers no fix).
- **The evidence.** `Layouts()` only prints "(tight)"; its Check asks margin ≥ 0. From the 58° camera the tray's 22 mm rim reads as over the edge (photo 07, top).
- **The smallest fix.** Offer it next to Q3 as an optional outside change:
  - `ForPhone` `scrapTray = new Vector2(6f, 128f)` (5.9 mm in; still 4 mm clear of the pad and 6 mm clear of the rest);
  - then Bench 7 re-run, Bench 9, and Bench 5's call-21 comment.

**6. [Minor] `Assets/Editor/BenchFramingSteps.cs:666-669` — a rule framing is accepted at zero margin.**
- **What's wrong.** A rule framing is taken with no slack over a bar (P1).
- **The evidence.**
  - The builder's own table (:39) has rule 1 for the phone at T8 12.05 px against 12. So which framing ships depends on 0.05 px of the step's model.
  - Bench 5 reads the phone's real screws higher (about 13 px), so that lab is safe. But T9 and the watch have no such cushion.
- **The smallest fix.** Optional, in `Solve`:
  ```csharp
  float s = rule != null ? 1f : 0f;
  f.t9 = fill >= bar.x + .005f * s;
  f.t8 = !screws || f.head >= 12f + .3f * s;
  f.t9Close = f.HasClose && f.fill1 >= bar.y + .005f * s;
  f.t8Notch = !screws || f.HasClose && f.headNotch >= 15f + .3f * s;
  ```

**7. [Minor] `Assets/Editor/BenchFramingSteps.cs:282-284` — the rule turns itself off silently.**
- **What's wrong.** If Art's mat has no renderer with `BM_Print`, the rule is switched off with only an info line. Bench 9 then reports all PASS while the phone keeps its half ruler (P1).
- **The smallest fix.**
  ```csharp
  if (!mat.ruler && stage.matFrame.name == "BM_RepairMat")
      run.Check(false, "Art's mat has no BM_Print renderer: the near-edge rule couldn't run");
  ```

**8. [Minor] `Assets/Editor/BenchFramingSteps.cs:104` — `EdgeClearMm = 3`: rule 1 leaves a thin wood sliver.**
- **What's wrong.** Rule 1 shows the mat's edge with 3 mm of bench under it: about 10–12 px of wood at the screen's very bottom. That can read as another "strip that reads like a mistake" (P1).
- **The smallest fix.** None in code. Name it among his calls. 8–10 mm (about 30 px) reads as an edge, for about 15 mm more distance.

**9. [Minor] `Assets/Editor/BenchStageTidySteps.cs:50,148` — the undo's words don't fit a stage built today.**
- **What's wrong.** "As Bench 7 left it: the walls on, the floor off" holds only for a stage built before today. A stage built by today's Bench 7 switches the group off with the Floor still `activeSelf`, so 7b's undo also shows the Floor (P1). Harmless.
- **The smallest fix.** Word it "the group back on, as it was".

**10. [Minor] `Assets/Editor/BenchFramingSteps.cs:17,133,599,690,797,855`, `Assets/Editor/ForgivingBenchSteps.cs:22` and `Assets/Editor/CafeLivedInSteps.cs:1819,2735` — dates without the year.**
- **What's wrong.** Comments say "23 Oct". The rule is "Dates in comments: '23 Oct 2026'" (P1).
- **The smallest fix.** Add the year.

**Resolved by the report (P1 items it answers):**
- **The well is not metal.** The spec's "the well in the same metal" is decision 2, Q2 and Blender item B2. B2 (`bench_stage.py:258`'s well floor `'BM_Recess'` → `'BM_Steel'`) is the right place, because `BM_Recess` is shared with the cups' walls.
- **Bench 7b saves the scene.** The report's order runs 7b first, then Art tidy 1 and a save, so 7b never saves Art tidy's unreviewed change.
- **Bench 9 is about three times slower.** The report says so.

**Verdict: ready after these fixes.**
- Part 1 (the coats), Bench 7b and Bench 7 are sound and can go to the Mac as they are.
- Before Bench 9 runs on the Mac:
  - fix 1: scope the rule, or tell him the camera changes too;
  - fix 2: an undo that restores, or at least says whether it is exact;
  - the report's Bench 8 pass line (3).

---

## Phase 1: what I checked and found sound (written before the report)

**Compile.**
- `csparse.py` reports all five files clean.
- Every identifier exists with the right shape (grepped):
  - `CafeRedesignShared`: `Step` (its named arguments `guards`, `rebake`, `roomPhotos`), `UndoStep(title, label, refuse, rebake)`, `SwitchOff`, `Note`, `Photo`, `GameCameraPhoto`, `Find`, `PathOf`, `RendererBounds(Transform, bool)`, `BoundsText`, and `Run.Cafe`, `Require`, `Check`.
  - The prop names: `NpcLifeProps.LaptopName`, `BagName` and `BinStationName` (public const). `CafeTidy.putAway` (a `GameObject[]`; Linq `Contains`).
  - The bench types: `BenchRig`, `BenchOutlineHull`, `ToolPickup` and `JobBase`. `BenchStage`'s fields, `MatToWorld`, `WorldToMat`, `MatUp`, `MatAway`, `MatRotation`, `MatTop` and `LegacyTray` (editor-only, used from editor code).
  - `BenchLayout` (a class, so `!= null` is legal): `ForKind`, `IsSet`, `HasFraming`, `cameraAim` and `cameraDistance`. `ItemInspector.Pitch` and `LayoutFov` (public const). `BenchScale.Devices` and `.label` / `.prefab`. `AcesCafeLayoutSetup.ScenePath`.
  - The APIs: `FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)` (the 6.5 form) and `Rect.Overlaps`.
- No CS0136:
  - `FrameDevice` declares `head`, `closeAim`, `fovClose`, `fill1`, `notchFov` and `headNotch` once each. The old `out` declarations are gone.
  - `Solve`'s `out` variables are method-scope and unique.
  - The new `for (int i…)` in `SearchLayout.Try` is in a sibling block.
  - Reused lambda names are in sibling scopes, or are lambdas (C# 8+ shadowing).
- The report's `oldTray` clash in Bench 7 is really fixed (`ForgivingBenchSteps.cs:109` vs `v2Tray` at :190).
- `?? stage.LegacyTray` is fine: `OldTray` returns a true null.
- 7b's local functions `Check` and `Info` passed as `Action<bool,string>` and `Action<string>` are legal.

**Coats.**
- The prefix `LI_Coat_OnChair (seat ` finds the two placed coats (`CafeLivedInSteps.cs:1837`), not the FBX's inner mesh.
- `Scarf` and `Tote bag` are direct children of the stand (`CafeFurnishing.cs:537,543`). The path matches `CafeTidySteps.cs:85`.
- The survey's three checks pass in his scene:
  - `Primitive()` destroys each box's collider, and the stand's collider is on the stand;
  - there are no MonoBehaviours;
  - no prop-like names.
- The NavMesh bakes `PhysicsColliders` (`AcesCafeLayoutSetup.cs:604`), so `rebake: false` is right.
- The undo switches back on exactly what was on. A redo is refused ("already done").
- The guard:
  - the coats fall into `p.Off`, and both readers of it (the Note's count, and the "in use" photo that would have switched them on) are fixed;
  - `After()` checks `activeSelf == x.active`;
  - `WorldBox` measures inactive pieces.
- The shared "latest first" rule makes Art tidy 1 come off before Lived in 2's undo deletes the coats.

**The v2 tray.**
- The diagnosis is right:
  - Bench 2 built "Parts tray" = Floor + Wall N/S/E/W (`DC_SteelDark`, 24 mm, each a BoxCollider) + a trigger "Magnet zone" (`Playtest3Session4Steps.cs:115-133`);
  - it wired only the Floor (:195);
  - Bench 7 switched off only `LegacyTray`;
  - photos 01/04/06/07 and the camera's 15 show the walls, as do the 19 Oct findings bench-a S2 and bench-b S2.
- Switching the group off hides nothing the bench uses:
  - nothing reads the walls, "Magnet zone" or "Parts tray";
  - there is no `OnTriggerEnter` in `Assets/Scripts`;
  - `Wire` drops `trayZone`;
  - after Bench 7, `Tray`, `Caddy` and `Mat` return `scrapTray`, `toolRest` and `matFrame`;
  - `Holds()` refuses if any place, tool or device is under the group.
- The walls' colliders were in the sight lines (`ItemInspector.Seen`, the cursor's surface ray, Bench 8's `Blocked`). Removing them can only clear those lines.
- 7b is Undo-recorded in one group. A re-run changes nothing. Its undo restores his scene exactly (the walls on, the Floor still off). The photo moves of the rest and tray are restored in a `finally`.

**Bench 9's geometry.**
- The frame's bottom edge (Pitch + fov/2 = 78°, the same at 16:9 and 16:10) meets the mat along a line of constant mat y.
- The cut is linear in distance (about −0.35 mm/mm), so `WithRule`'s extrapolation is exact and rounds on the safe side. The builder's table checks out: (−9) at 0.463 m gives −172.0; (−15) at 0.478 m gives −183.2.
- The ruler's constants match `bench_stage_textures.py`: edge −178.5, inner line −166, tick tips −174, numbers' feet −173. `BM_Print` is the material Bench stage 1 makes.
- For one aim, fill falls with distance, so the nearest rule-keeping distance is the best one. Every rule framing still passes `Fits` with all targets.
- With no rule, `Solve(null)` is the old search line for line.

**Labs.**
- Bench 3: `InView` (the centre in the viewport) and the zoom FOV ratios. No bay moved.
- Bench 5:
  - T9 and T8 hold. T8 is measured on the phone's real screws at its near corners, which read above Bench 9's middle-of-device number.
  - The places stay clear of the zones by Bench 9's own constraint.
  - The waiting watch at mat y −265 stays out of frame with the bottom at −183 (the bottom ray at its top is about −186).
- Bench 6: zone and overlap checks. The pulled-back frame shrinks everything toward the aim.
- Bench 8: see findings 1 and 3.

**Rules.**
- Only owned files changed, plus the two allowed new files. `ForgivingBenchSteps.cs` is Bench 7, "the step that restyles the bays". None of the other builders' files are touched.

## Phase 2: the report's claims, checked

- **"Nothing brings the coats back": true.**
  - At runtime: only `NpcLifeProps` switches props on, and only by the laptop, bag and bin names. `NpcCup` moves its own cups. `NightWalk.nightOnly` holds street objects. No script names coats.
  - In the editor:
    - the only switch-ons are Art tidy 1's own undo and temporary photo copies (`CafeDressing3Steps` and `ReviewModelSheets` work on copies or preview instances);
    - Lived in 2's redo places the coats off;
    - Tidy 3's undo brings back only the bare stand.
- **The menu order (§5): sound.**
  - The order (reimport, 7b, which saves; Bench 9; Art tidy 1 then save; labs) has no dependency problems, and it avoids 7b saving Art tidy's unreviewed change.
  - The reimport menu exists (`ReimportChanged.cs:22`).
  - Two corrections: "run 9b after" (finding 4) and "left as it was" (finding 2).
  - Unity's own menu display order isn't claimed, and doesn't matter.
- **"None moved": true.** No empty, bay, room or `BenchStage.cs` constant changed. The *framing* moves, for up to three devices (finding 1).
- **"Still passes because …", lab by lab.**
  - **Bench 3:** right. Its checks read the empties and rooms; `InView` is lenient; `BenchTopUnder` skips the rig.
  - **Bench 5:** right in substance. The T8 argument holds for the phone, and `UnderTheDevice` reads only the device's colliders. Not mentioned but also fine: the second-slot "out of frame" check.
  - **Bench 6:** right.
  - **Bench 8:** the reason given covers only the walls ("can only help": true). The framing change is moved to "Risks", and there only for the phone and watch. Its expected "All 220" is wrong (finding 3).
- **Spot checks that hold.**
  - The three symptoms the walls cause (the black frame, past the mat's right edge, across the pad).
  - The phone tray's 0.9 mm (Art A3's 89.2 mm tray).
  - The optional watch tray (26, 131): 2.4 mm to the pad, 6.4 mm to the rest, 2.9 mm to the far edge.
  - The comment lines to update.
  - Bench 7b's pass lines match the code (the "All good." last line, the five PASS rows).
  - B2's line exists.
  - The list of other box-built soft things is plausible. "Folded throw" (`CafeFurnishing.cs:249`), the coffee bags (off since Tidy 3, `CafeTidySteps.cs:92-94`) and the hat (cylinders) are in the code as described.
