# Review: b-days and b-uiux before the Mac deploy (14 Oct 2026)

Reviewer, read-only. Read: common §3 and §9, the feel targets, contracts, both specs, both builders' reports, `git diff 431f081 b-days` and `git diff 431f081 b-uiux` in full, and the unchanged code they call (repo, b-bench, b-scale, b-intro, b-imports). Nothing compiled or run; every finding below was traced by reading the code.

**Verdict.** No Blockers in either branch. Two Majors: the glow and the hover outline never draw on the brush (b-uiux), and the "Bench words" edit check can't pass after the merge (both branches). Four Minors. The Days 1–3 runtime (R1–R4, the walk-in list, the latte, the data step, saves) holds up. See "Verified clean".

---

## Blockers

None found.

---

## Majors

### M1. The glow and the hover outline skip skinned renderers, so the brush never glows (T5, decision 7)

**Where:** `Assets/Scripts/BenchOutline.cs:67` and `:189–206` (b-uiux).

**What happens:** `Build` collects only `MeshRenderer`s, then skips any without a `MeshFilter`. Since the imports merge the brush draws with a `SkinnedMeshRenderer`. b-bench's `ToolPickup.cs:122` reads it as "the brush's own (Bend)", the imports report §5.1 says the same, and b-bench commit `bcda6e4` notes it has no MeshFilter. So the brush gets zero hulls.

**Scenario:** Day 1, Grace's camera. The lens is wiped and the step becomes **Brush**. `BenchGlow.Guide` calls `Hull(brush)`, nothing is drawn, and `BenchGlow.GlowingTool` still reports the brush, so every lab reads green. The same holds for:
- the white hover on the brush (`BenchInteractable.cs:81` → `BenchOutline.Set`);
- Day 1's walk-in phone (brush spots);
- Day 3's watch debris.

The imports builder's patch 5.1 was written against the old `BenchOutline` and doesn't apply to this rewrite.

**Fix:**

Before (`:67`):
```csharp
    static readonly List<MeshRenderer> found = new List<MeshRenderer>();
```
After:
```csharp
    static readonly List<Renderer> found = new List<Renderer>();
```

Before (`:189–206`):
```csharp
        foreach (MeshRenderer source in found)
        {
            if (source == null || source.GetComponent<BenchOutlineHull>() != null) continue;
            // Not a job tag nor a text (DeviceBounds' rule): the whole device's glow outlines the device, not its tag.
            if (source.GetComponentInParent<JobMarker>(true) != null || source.GetComponent<TMPro.TMP_Text>() != null) continue;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            ...
            go.AddComponent<MeshFilter>().sharedMesh = Smoothed(filter.sharedMesh);
            var renderer = go.AddComponent<MeshRenderer>();
            int subMeshes = Mathf.Max(1, filter.sharedMesh.subMeshCount);
```
After:
```csharp
        foreach (Renderer source in found)
        {
            if (source == null || source.GetComponent<BenchOutlineHull>() != null) continue;
            // Not a job tag nor a text (DeviceBounds' rule): the whole device's glow outlines the device, not its tag.
            if (source.GetComponentInParent<JobMarker>(true) != null || source.GetComponent<TMPro.TMP_Text>() != null) continue;
            // The brush and the held cloth have keys since 14 Oct: Unity draws them skinned, with no MeshFilter.
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else if (source is MeshRenderer) { MeshFilter filter = source.GetComponent<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
            if (mesh == null) continue;
            ...
            go.AddComponent<MeshFilter>().sharedMesh = Smoothed(mesh);
            var renderer = go.AddComponent<MeshRenderer>();
            int subMeshes = Mathf.Max(1, mesh.subMeshCount);
```
Sprite, line and particle renderers fall out on the type test. Don't use `?.` on the `GetComponent`: in the editor a missing component is a fake null.

**Lab to add:** Bench 6 S1 should also assert that a hull renders on the brush once the lens is clean, not only that `GlowingTool` is set.

### M2. "Fixit Fidget › Checks › Bench words" can't pass after the merge

**Where:**
- `Assets/Editor/BenchWordsChecks.cs:126–145` (CardLines) and `:245–246` (Budgets), b-uiux;
- the cause is `Assets/Scripts/FaultSpeech.cs:35`, b-days.

**What happens:** The check builds a card line for every fault of every bench device, using the longest profile name ("Officer"). The phone's fault 3 (the Human fault) gives `Officer: "Calls light up the screen, but it never rings."`, which is 10 words and 57 characters against 8 and 46, so it FAILs. The same line is the longest card line, so the synthetic worst case becomes 10 + 4 + 2 + 5 + 5 = 26 against 24, a second FAIL.

**Scenario:** The Producer's step 1 in the uiux report expects `PASS` once the days rows land, and gets `2 checks FAILED` every time. This is not a Days 1–3 player bug. It stalls the deploy runbook.

**Fix (b-days, placeholder copy):**

Before (`FaultSpeech.cs:35`):
```csharp
        ("Calls light up the screen, but the phone never rings", "calls light up the screen, but it never rings"),
```
After:
```csharp
        ("Calls light up the screen, but the phone never rings", "calls light up, but it never rings"),
```
- The card becomes `Officer: "Calls light up, but it never rings."`: 7 words, 46 characters.
- The longest line is then Grace's clean (8), so the worst case is 8 + 4 + 2 + 5 + 5 = 24 and both checks pass.
- I recomputed every other bench fault with "Officer"; all are ≤ 8 words and ≤ 46 characters. Frozen and debris sit at exactly 46.

---

## Minors

### m1. T17's automatic "what's left" fires again on every return to the bench

**Where:** `Assets/Scripts/BenchCards.cs:304, 349, 398, 580` (b-uiux).

**What happens:** `flashedThisJob` is reset in `Begin`, which runs each time the close-up opens. b-bench's own 3 s flash keeps `flashedFor` per job (`ItemInspector.cs:834`), so the two disagree.

**Scenario:** Day 1. The player idles 12 s at the camera (flash #1), right-clicks out to look at Grace, comes back and idles again: flash #2, and so on. T17 says "once by itself; after that only on Tab / View".

**Fix:**

Before (`:304`):
```csharp
    bool refreshNow, wasWanted, stuckPulsed, stuckControls, flashedThisJob, tickedFlashed, finishedFolded, lineWraps;
```
After:
```csharp
    bool refreshNow, wasWanted, stuckPulsed, stuckControls, tickedFlashed, finishedFolded, lineWraps;
    JobBase flashedJob;   // the last job "what's left" showed for, kept across close-ups: T17's automatic flash is once a job
```

Before (`:349`): `if (job != null && job == shownFor) flashedThisJob = true;`
After: `if (job != null) flashedJob = job;`

Before (`:398`): `stuckPulsed = stuckControls = flashedThisJob = tickedFlashed = finishedFolded = false;`
After: `stuckPulsed = stuckControls = tickedFlashed = finishedFolded = false;`

Before (`:580`): `if (FirstRepairRule() && !flashedThisJob) BenchWork.Flash(job, FlashSeconds);`
After: `if (FirstRepairRule() && flashedJob != job) BenchWork.Flash(job, FlashSeconds);`

### m2. Words over T7's steady worst case (≤ 24) while a covered part is hovered

**Where:**
- the words: `ReplaceablePart.cs:176–180` (b-bench's `CoveredHint`), returned by `ItemInspector.Words` and drawn as the hover's second line (`HoverTooltipUI.cs:77`);
- the assumption: b-uiux's budget check takes it as 5 words (`BenchWordsChecks.cs:47`, `BlockedHoverWords = 5`).

**Scenario:** Day 3, the cracked-screen phone, face up. Hovering the screen shows "Cracked screen" plus "Held in by the back cover. Take the cover off first" (13 words). With the card (4), "[R] Flip it over" (4), "[Tab] Help" (2) and the corners (4), that is **27 words**. Bench 6's S3 doesn't hover a covered part, so no lab sees it.

**Fix (b-bench's file; content call, placeholder copy):** Drop the second sentence. The phone case then reads 4 + 4 + 2 + 8 + 4 = 22.

Before:
```csharp
            return $"Held in by the {c}. Take the cover off first";
        ...
            return $"Inside, under the {c}. Take the cover off first";
        return $"Under the {c}. Take the cover off first";
```
After:
```csharp
            return $"Held in by the {c}";
        ...
            return $"Inside, under the {c}";
        return $"Under the {c}";
```
Also set `BlockedHoverWords` to the real worst case (8), and compute the check's worst case only for devices that have covered parts. Grace's 8-word line never meets a covered part, so the synthetic sum overstates it.

### m3. Allocation every frame at the bench

**Where:** `BenchCards.cs:594–595` and `:612` (b-uiux), and `:90` via `RepairChecklist.Current` (b-bench).

**What happens:**
- `Controls()` builds a string key every frame while a card is up: all of the first repair.
- In steady play it calls `HintWords()` every frame, which allocates two interpolated strings.
- `CurrentStep` loops over an `IReadOnlyList<Step>`, which boxes the enumerator. `Stuck()` and `BenchGlow.Guide()` both read it every frame, which contradicts BenchGlow's "nothing allocated per frame".

**Fix:**

Before (`:90`):
```csharp
    public static RepairChecklist.Step CurrentStep => Instance != null ? RepairChecklist.Current(Instance.steps) : null;
```
After:
```csharp
    public static RepairChecklist.Step CurrentStep => Instance != null && Instance.currentIndex >= 0 && Instance.currentIndex < Instance.steps.Count
        ? Instance.steps[Instance.currentIndex] : null;   // Refresh keeps currentIndex = the first current step
```

Before (`:307`): `... finishedWords = "", controlsKey = "", hintKey = "";`
After: `... finishedWords = "";` plus `int controlsKey = -1, hintKey = -1;`

Before (`:594–596`):
```csharp
        string key = mode == ControlsMode.None ? "" : (mode == ControlsMode.Held ? "H" : "S") + (pad ? PadInput.Label(PadButton.South) : "M")
            + (GameSettings.SwapBenchSticks ? "s" : "");
        if (key != controlsKey)
```
After:
```csharp
        int key = mode == ControlsMode.None ? 0
            : 1 + (int)mode * 2 + (pad ? 8 : 0) + (GameSettings.SwapBenchSticks ? 16 : 0) + ((int)PadInput.Kind << 5);
        if (key != controlsKey)
```

Before (`:611–615`):
```csharp
        bool hintOn = mode == ControlsMode.None;
        string hintWords = hintOn ? HintWords() : "";
        if (hintWords != hintKey)
        {
            hintKey = hintWords;
            if (hintOn)
            {
```
After:
```csharp
        bool hintOn = mode == ControlsMode.None;
        int hintNow = hintOn ? 1 + (pad ? 2 : 0) + ((int)PadInput.Kind << 2) : 0;
        if (hintNow != hintKey)
        {
            hintKey = hintNow;
            if (hintOn)
            {
                string hintWords = HintWords();
```

### m4. The uiux report's patch C is stale against b-intro: don't apply it

**What happens:** b-intro's `StationInteractable.cs:166–260` already does §5.7. It shows "Put it back" from `PlayerCarry.LiftedFrom`, the scale builder's field, which is the source the Producer's call 22 named. It also holds the tray back on the carry step by reading `DayOneGuideUI.CarryingToBench` through reflection. I checked that this works with b-uiux's static property. Patch C's "before" lines (for example `return c != null && c.IsCarrying && dropSpot != null;`) no longer exist there.

**Consequence:** b-uiux's `IntakeTrayWatch` (`DropInteractable.cs:113–151`) still runs every frame for nothing. `carry.Contains` also allocates a closure for each lifted device each frame. It does no harm.

**Fix:** Skip patch C. Optionally drop `Boot()` at `DropInteractable.cs:113–120`.

---

## Verified clean (read end to end)

**Grace arrives, with the right fault:**
- Days 1–3 feature her at 0, and she is the opener.
- Day 1 is fault 0 and Day 2 fault 1, with a matching episode id (`grace_reunion_camera_v1` on the prefab).
- On Day 3, `useFeaturedRepair` is off and `Regular_Grace.primaryVisitKind` is DrinkOnly, so she can never roll a watch from `devices`. The Officer is DrinkOnly too.

**Her Day 1 latte:** `suppressRepairDrinkWishes: 1` (step C) reaches `RollDrinkWish` (`CustomerSpawner.cs:750`) on Day 1 only. On Day 2 her wish chance of 1 brings the latte.

**R1–R4:**
- R1: `ShutterIsTask` reads the jammed part's own `activeSelf`, and the `ReplaceablePart` sits on the switched object.
- R2: `VisitFor` follows the fault type.
- R3: `before` is a copy (`CustomerMemoryService.Read` → `Copy()`), read before `RecordVisit`. `PhotoGrade` downgrades correctly, and `graceCameraDay` = 2 pays out on Day 3.
- R4: `parts[0]` is the working blade under the inactive jammed part. b-bench's `PresentFresh` skips an inactive part, so Day 1 winds only.
- b-bench's `RepairChecklist`/`RepairJob` ignore the inactive shutter, so no tweezers icon appears on Day 1. Spots 1, 2 and 3 of 3 grade Passable, Good and Perfect against the 0.66 boundary.

**The walk-in list:**
- It resets each morning and applies on its own day only.
- It is taken in order by walk-ins only (`profile == null`). Once it runs out, everyone gets drinks.
- The Day 1 timeline works out: Officer spawned at 75.5 s, first walk-in at the till ≈ 2:21.

**Saves:** No save field touched. His Day 5 save sees only the stopgap. An old Day 2 save waits on the photo (`AcceptReturnMemento`'s early return) and repays on Day 3.

**The data step:**
- Values: the "was" numbers match all five assets; Day 2's `featuredRegularArrivesAt` is already 0.
- Idempotent: the stopgap only scales from the 180 s values, and the faults and the resting copy compare against a signature, so a second run is "already".
- Safety: the resting copy has no collider and no MonoBehaviour, and it can't be doubled under the same parent. A failure anywhere undoes all groups and unloads the prefab unsaved.

**Bench contracts:** Every member b-uiux calls exists in b-bench with matching types:
- `BenchWork` events, `Item` and `Kind`;
- the `Step` fields and `Current`;
- `IsSeen`, `FocusedItem`, `CurrentTool`, `HoldTarget`;
- the `BenchStage` fields and `BenchStageContract`.

Every `BenchCards` static that the other branches' labs read is still there. b-bench doesn't bind Tab, so there's no double flash. `bench.showleft` is played twice, and the bank's 0.05 s `minGap` collapses it.

**Tab at 15 fps:** The press uses `wasPressedThisFrame`, and the 0.35 s hold is timed on `UiClock`. Tab and View are dead outside the close-up (`CafeViewMode.CanChangeView`).

**Cards and events:**
- The card fades and disables its canvas.
- The glow's hulls go off on close-up end, "Fixed!", pause, recap and `OnDisable`.
- Events are unsubscribed in `OnDisable` and nulled at subsystem registration.

## Watch at deploy

- **If step B refuses** (the camera no longer has exactly 3 spots after Bench 7 and the rescale), "All" undoes everything. Day 1's camera is then the old Mechanical fault, `VisitFor(false)` makes Day 1 play the **shutter** visit's lines, and R3 records the photo on Day 1. Read the B report before pressing Play.
- The days report's §5 patches are required:
  - without NightZero's, a Rejected Day 1 clean won't play Night 0's "walked out";
  - without NightOneCheck's, that lab fails.
