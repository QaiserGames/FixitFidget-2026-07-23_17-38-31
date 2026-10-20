# Builder B report — the bench (19/20 Oct 2026). Copy: /root/wt/B. All eight priorities done in order; every changed file passes the tree-sitter C# parse (18 files, 0 syntax problems). ItemInspector.cs untouched (one edit described in §4).

## 1. What I did (files changed, one line each)
- `Assets/Scripts/RepairChecklist.cs` — a spot is "on the face turned down" by its own facing (`GrimeSpot.Normal`), not by its middle against the device box's middle (the watch's crystal now says "Flip it over"); "Cover on" ticks only once the cover has been off and is home, "Screws back" only once every freed screw is in; a hole target is listed by its object being active, not by its component's `enabled`; header updated.
- `Assets/Scripts/GrimeSpot.cs` — `Normal` wakes the spot if asked before Awake (the editor's checklist check), so the facing is right whoever asks first.
- `Assets/Scripts/Screw.cs` — `ScrewSocket.Sync` (driven by `Screw.Update`): a hole is SHOWN only while its screw is out and its plate is on the device; PICKABLE (component enabled) only in a screwdriver step or with no step; `PlateSeated`, `Shown`, `StepTakesHoles` added; header updated.
- `Assets/Scripts/BenchOutline.cs` — `Guide` is amber #FFB238 (`GuideSoft` #FFD27A, `GuideInk` = Ink warmed 25 % toward amber); a second, wider hull per renderer; new `Glow(target, on, inner, innerWidth, outer, outerWidth, xray)` two-tone overload (x-ray dashes on the amber hull only); the one-tone `Glow` and the pointer layer unchanged.
- `Assets/Scripts/BenchGlow.cs` — rewritten around the two tones: 1 s breath (amber → soft amber, outer width 1.7→2.0×), rings two-tone (2 px ink inside 3 px amber, alpha never under 0.7), the next tool's SEAT ringed on screen (`SeatRinged`), "what's left" gold two-tone, the stuck pulse white→amber; NEW put-down mark: while Ace carries a bench-bound device and no close-up is open, a rounded two-band mark on the mat at the device's layout rest point (world, URP Unlit transparent) plus a screen ring ≥ 26 px radius (`PutDownShowing`, `PutDownPoint`); `BenchRing` gained an inner band.
- `Assets/Scripts/PlayerCarry.cs` — read-only `BenchBound` (the carried repair that still needs the bench; not a cup, not a Human/counter repair, not finished) and `CarriedSize(item)` (its real size at pickup).
- `Assets/Scripts/Juice.cs` — `BackTogether(wordsAt, grade, missed)` adds "Missed: wipe the crystal" as a 75 % second line and stays 2.8 s; `Money(..., JobBase repair = null)` adds "Perfect pays $94" (via new `PerfectPays`) and stays 2.4 s; `Say` sizes the plate to two lines; `LastWordsSecondLine`, `LastMoneySecondLine` for labs; the bench watcher passes `RepairJob.MissedWords()`.
- `Assets/Scripts/RepairJob.cs` — `MissedWords()`: what is left by its verbs ("wipe the crystal", "brush the film path", "fit the new screen", "reconnect the circuit"; two, then "and N more").
- `Assets/Scripts/ReplaceablePart.cs` — `ReplaceablePart.Honest(name)` strips the fault word; `LoosePart.DisplayName` is "New screen" / "New shutter mechanism", never "New cracked screen".
- `Assets/Scripts/BenchCards.cs` — sizes: quote 22, verb 28 bold, controls 22 bold, hint 22 bold, finished line 24 bold, disc 58, pads 10 (card height ≤ 168 = zone U3 with a two-line quote); "2 left" (22 px) replaces the pips; only the current icon wears the disc; `K`/`KP` carry no brackets; every key through `ControlHints`/`PadInput.Label` (mouse: `ControlHints.Use` + "Drag a tool", `ControlHints.Cancel` + "Step back"; pad: D-pad left/right icons for "Next part"); "up/down: peek" → "up/down tilts it"; the quote's height is measured, not guessed; new readers `CurrentLeft`, `FinishedRich`, `LeftWords`.
- `Assets/Scripts/HoverTooltipUI.cs` — rewritten: "Needs the X" only when X is the current step's tool (`OnlyThisStepsTool`); one line tall when the action is empty; name 26 px bold, action 22 px bold; placed above-left of the cursor first, then above-right, below-left, below-right, the first corner that covers neither the device's screen rect nor a screw cup (else the least covering), always on screen; layout groups/fitters on the scene's card disabled; `PlateOnScreen` for labs.
- `Assets/Scripts/ToolPickup.cs` — the cloth's keys: Press 0.55 in the air (was 0.4), Bunch capped at 0.45 while rubbing (`PressInAir`, `BunchMost`).
- `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs` (Bench 8) — fifth repair "Watch debris" (mouse and pad, 60 and 15 fps, bar 110 s); per run: the hover never names a screw hole in a brush/cloth step (`WatchTheHover`, every frame); the Debris run: brush first, then the Wipe step asked for a flip (`StepLog.flips`) and finished.
- `Assets/Scripts/Diagnostics/BenchLab.cs` (Bench 3) — card checks via `ControlHints` helpers on the rich text; new checks: the put-down mark while the phone is carried (and gone once set down), holes shown-but-unpickable and no "Screw hole" hover in the pry step, holes hidden with the cover in its well, the fresh screen glowing amber on its pad and named "New screen", the brush's seat ringed.
- `Assets/Scripts/Diagnostics/BenchWordsCheck.cs` (Bench 6) — a virtual pad is plugged in before the pad words are measured (the flag alone was reset by `PadInput.Track`); S1 checks the card follows the device and uses icons (no "["); S4/S6 keys through helpers on the rich text; the bench's own cards ≥ 22 px and the verb 28.
- `Assets/Scripts/Diagnostics/BenchViewProbe.cs` (Bench 5) — pad card checks through `ControlHints` on the rich text; the swap must change the card.
- `Assets/Scripts/Diagnostics/JuiceCheck.cs` — "Back together · Passable" with "Missed: wipe the crystal"; `PerfectPays` on a job of the bench's (and nothing at Perfect).
- `Assets/Editor/BenchWordsChecks.cs` — the bracket checks become helper checks on the rich text; the impatience line counted plain.

## 2. The decisions and their reasoning
- **P1 the watch.** The old test compared the spot's middle with the drawn box's middle; the bow and crown pull the watch's box middle 9 mm toward the bow, so the crystal smudge was 68° off "down" (0.37 against 0.7). `GrimeSpot.Normal` is the side the dirt shows on, already computed in `LayThePatch` and already what the picker and the rub use, so the three now agree. The lab case went into Bench 8 (not Bench 3): it already plays the card's own words through the real input path on mouse and pad at 60 and 15 fps, so a card that says "Wipe" with the smudge underneath leaves the hand scrubbing nothing and fails the bar. The hole-hover check runs on every run, not only the watch's.
- **P2 holes.** I chose HIDE over RE-PARENT: hiding is one rule on the hole with no new geometry assumptions (a plate with a non-uniform scale, a screw that threads into the body beside the cover, the edit-mode flights the editor checks run); re-parenting would also have put pickable holes on the plate in its well. During the pop (7°) the plate still covers the holes; they vanish as it lifts and reappear exactly where they were when it clips home. Pickability is a per-step switch because the picker (ItemInspector, not mine) skips only a part that is not `isActiveAndEnabled`; the step is read from the inspector's own checklist so the picker and the hole agree.
- **P3 one "next" look.** Two hulls, not a shader change: the outline shader isn't mine, and an inverted hull's back faces lie nearer the camera the narrower it is, so a narrow ink hull drawn with depth sits against the part and the wider amber one outside it, on any surface (ink 9.5:1 on the pad, 9.3:1 on the cloth; amber 2.8:1 on the slate mat — one of the two always reads). Ink is warmed 25 % toward the amber so it reads as the amber's shadow (the spec's "~70 %"), since the opaque shader cannot blend. The seat ring is on the screen canvas, which no tray wall can hide. The put-down mark uses the device's own layout rest point (`DeviceDefinition.LayoutOf`), the only place the game knows the device will lie; a screen ring with a 26 px floor covers the overhead where the whole mat is ~20 px. "What's left" stays gold but gains the ink line: unified in look, told from "next" by what it does (everything, for 1.5 s), which is the accessibility rule the file already followed.
- **P4 missed step.** "Fixed!" only ever fires at Perfect, so the line went on the plate that does fire below Perfect, "Back together · Passable", and the pay pop at the hand-back. `Juice.Money` finds the repair through the payer's `CustomerBrain.ActiveJob` and only trusts it when the graded pay equals the amount paid, so Grace's latte paid while her camera sits on the bench never says "Perfect pays"; passing the job explicitly (§4) removes the heuristic.
- **P5 names and ticks.** The tick bug was `plan.opened` (set when any screw is out) standing in for "was off"; `plan.removed` already existed. The one-highlight bug was the shrinking slot keeping a fading disc for 0.25 s. The fault-word strip is a short list of adjectives; the broken part keeps its word (it is cracked), the fresh one and the "fit the new …" line lose it.
- **P6 hover.** Placement tries four corners with the cursor as the pivot and scores coverage of the device rect plus the cups' rims; the plate is sized from TMP's preferred sizes so it is one line tall with no action.
- **P8 cloth.** Only the key weights were mine; the held wad is a smooth dome with no fold, which needs the model (bench_tools / BT_ClothHeld).

## 3. Decisions his to overrule
1. Hide the holes with the plate off (not re-parent them to the plate).
2. Amber #FFB238 / soft #FFD27A, ink warmed 25 % toward it; inner line 0.65×, outer 1.7→2.0× the hover width; breath 1.0 s; rings alpha 0.7→1.
3. "What's left" keeps gold, two-tone (unified look, told by behaviour).
4. The put-down mark marks the layout's rest point, footprint + 20 mm, ink 4 mm + amber 7–10 mm; screen ring ≥ 26 px. It shows whenever a bench-bound device is carried, every day, not only Day 1.
5. The missed line goes on "Back together", 2.8 s; the pay pop's second line 2.4 s, 75 % size in GoldSoft.
6. Card sizes: 22/28/22/22/24; disc 58, pads 10 (to keep zone U3); "2 left" in InkSoft bold.
7. Mouse first-repair card: "Drag a tool" after `ControlHints.Use`'s icon; "up/down: peek" → "up/down tilts it"; the held card's mat line "Drag the mat to turn it".
8. Hover: name 26 bold, action 22 bold; corner order above-left, above-right, below-left, below-right.
9. Cloth: Press in air 0.55, Bunch cap 0.45.
10. Bench 8's Debris bar 110 s (the seized watch's).

## 4. Outside my files (exact edits)
**A. `Assets/Scripts/ItemInspector.cs` (priority 7, tools go home) — replace `OnTargetDone` (~803-807) and `ToolHomeAfterLastSpot` (~816-833) with:**
```csharp
    private void OnTargetDone(JobBase job, Component target)
    {
        if (job != null && job == focusedItem) checkToolHome = true;   // 19 Oct: every tool, not only the cloth and the brush
        stepsAt = -10f;   // the steps change with it
    }

    // The tool in the hand whose step is done flies home (the cloth and the brush did after their last spot; 19 Oct, the
    // reviewers: "at 'Fixed!' the last tool stays where it was dropped", the driver across the fixed phone's screen). A
    // rub tool goes when none of its spots is left; any other when the next step wants another tool, or the job is done.
    private void ToolHomeAfterLastSpot()
    {
        checkToolHome = false;
        if (currentToolPickup == null || currentToolPickup.Returning || holdTarget != null) return;
        if (IsRubTool(currentTool))
        {
            focusedItem.GetComponentsInChildren(spotsBuffer);
            bool left = false;
            foreach (GrimeSpot spot in spotsBuffer)
                if (spot != null && spot.isActiveAndEnabled && !spot.IsClean && spot.Tool == currentTool) { left = true; break; }
            spotsBuffer.Clear();
            if (left) return;
        }
        else
        {
            RepairChecklist.Build(focusedItem, steps);
            RepairChecklist.Step next = RepairChecklist.Current(steps);
            stepsAt = -10f;
            if (!focusedItem.IsFinished && next != null && next.tool == currentTool) return;
        }
        EndRub();
        if (UseHeldRaw) consumedUntilUp = true;
        pressLive = false;
        ToolPickup tool = currentToolPickup;
        currentToolPickup = null;
        currentTool = ToolType.Hand;
        tool.SetSelected(false, liftFirst: true);
    }
```
**B. `Assets/Scripts/CustomerBrain.cs` line 2111 (optional, removes the pay-match heuristic):** `Juice.Money(transform, basePay, tip);` → `Juice.Money(transform, basePay, tip, repair: activeJob);` (the drink's call at 2020 stays as it is).
**C. The PocketWatch prefab:** rename the grime object "Dookie" (e.g. "Movement grime") — it is player-visible in the hover and now in "Missed: brush the dookie".
**D. Builder T's contract I wrote against:** `ControlHints.Use/Cancel/Flip/Back/BenchHelp/BenchUse/BenchZoom/Tools/TakeFromBench/BenchHandStick/BenchTurnStick` and `PadInput.Label(PadButton.South/DpadLeft/DpadRight)` returning sprite tags with a word fallback. I did not call `ControlHints.Word(...)` (its signature isn't fixed in the spec); the labs compare the rich text against the same helpers instead.

## 5. Labs and what a pass looks like (Fixit Fidget › Bench, after "Pick up scripts changed from outside")
- **Bench 8 (clumsy hand):** 20 runs (was 16). Every run "finished, Perfect, within … s", "the hover never named a screw hole during a brush or cloth step"; the four Watch debris runs also "the grit brushed first, then the card asked for a flip before the crystal's wipe, and the wipe was done (… 1 flip(s) …)". Photos `4m15-…-step02-Brush`, `-stepNN-Wipe` (the watch face up, the cloth on the crystal).
- **Bench 3:** PASS lines "carried, the phone's place on the mat is marked for it (… mm from its rest point)", "set down, the mark on the mat is gone", "the pry step: the empty hole shows on the cover but can't be picked", "…the cursor over it reads "Back cover", not "Screw hole"", "the cover off, its holes go with it", "the fresh screen waiting on its pad wears the next-thing glow, amber over ink", "the fresh part is named by the part … "New screen"", "the brush is next: it glows on the rest and its seat is ringed"; photo `00-carried-from-above` shows the amber ring on the bench, `07-broken-screen-in-the-tray` the amber/ink outline on the pad.
- **Bench 6:** "S1, pad: the controls card follows the pad", "S1, …: no bracket round a key", "the bench's cards read at 22 px or more", "the verb under the current icon is 28 px", "S4: … its key an icon"; the pad photo now shows the pad's words.
- **Bench 5:** the three pad-card PASS lines through ControlHints.
- **Juice:** "closed up with a step missed: "Back together · Passable" / "Missed: wipe the crystal"", "paid $N (Passable) the pay pop would add "Perfect pays $M"", "fixed to Perfect, the pay pop adds nothing"; photo `62-back-together-missed`.
- **Checks › Bench words:** PASS with keys "as icons".
- **By hand:** carry a phone from the till: an amber ring breathes on the bench from above and a rounded amber/ink mark on the mat in first person; on the watch's Debris job, after brushing the grit the card shows the flip badge and "Flip it over", Triangle/R turns it, the crystal cleans, Perfect.

## 6. Risks and APIs I'm unsure compile (no Unity here)
- `Mesh.SetVertices(Vector3[], int start, int length)` and `Mesh.SetTriangles(int[], int submesh, bool calculateBounds)` (BenchGlow.MatMark) — both exist since 2019.3; if the array overload complains, use `SetVertices(List<Vector3>)`.
- `Shader.Find("Universal Render Pipeline/Unlit")` at runtime (the put-down mark): fine in the editor; in a build it needs the shader included (same risk as GrimeSpot's Lit). Fallback "Unlit/Color" handled.
- `Behaviour.enabled` on a `ScrewSocket` (no Update of its own): `isActiveAndEnabled` follows it in my experience; if the picker still hits a disabled hole, switch `Sync` to disable the hole's `BoxCollider` plus a `CanInteract` false (the hover would then show the "why not" rather than nothing).
- The two-hull outline doubles the hull objects per outlined renderer (the tweezers' leaves, a cover): a few dozen extra small renderers at the bench; on concave meshes the second hull shows the same self-overlap artefacts as the first, in amber.
- If Baloo 2 lands (builder T), its line height is ~1.6× the size: the card's quote height is now measured, but a two-line quote plus the 28 px verb will exceed zone U3's 168 px and Bench 6's "job card lies in U3" check; then shrink `Disc` to 52 or raise U3 by ~24 px in `BenchStageContract` (and re-run Bench 9).
- `HoverTooltipUI` assumes the scene's hover canvas is Screen Space Overlay (as the old code did) and that `panel`'s children are only the two texts; a background child image would be left where it is.
- The Bench 6 pad measure plugs in an Xbox-type virtual pad: it proves the card follows the device and uses icons, not that a DualSense gets PlayStation glyphs (ControllerChecks, builder T's).
- `ReplaceablePart.Honest` strips a fixed list of adjectives; a part name starting with an unlisted fault word keeps it.