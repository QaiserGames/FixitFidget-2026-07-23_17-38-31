# Days builder: Days 1–3 as an intro level (14 Oct 2026)

Worktree `/root/cafe/build/days`, branch `b-days`, commits on top of `431f081` (`fab65d3`):
`99bb30e` core runtime · `189d465` the data step and the Days 1–3 check · `0eb5e1b` checks and labs · `d3ba458` T11 line.

## 1. What changed

**Runtime (Assets/Scripts)**
- `DayDefinition.cs`: new `walkInRepairs` (`FeaturedRepairRequest[]`, authoring data, no save field) and `WalkInRepairsOn(day)` (own day only, never on a repeat).
- `CustomerSpawner.cs`: a walk-in who brings a device gets the day's next entry, in order; once used up, walk-ins come for drinks. Reset each morning. `RollJob`'s signature unchanged (reflection callers).
- `GraceCameraEpisode.cs`: `GraceCameraVisit {None, Clean, Shutter}`, `VisitFor`, `CleanWentWell`, `PhotoGrade` (R3), Day 1 and Day 2 lines (§5.4, placeholders), `VisitLines()` for the checks. Still pure (five console projects compile it).
- `CustomerIdentity.cs` (R2): `GraceVisit`; intake, thanks and hand-back lines per visit; Day 2's intake reads her memory from before the visit. No photo news on a camera visit (old saves). `SetupWalkIn`/`SetupRegular` untouched (room for the NPC builder's looks patch).
- `CustomerMemoryService.cs` + `SaveManager.cs` (R3): only the shutter visit records the photo's attempt, grade downgraded to Passable after a poor or missing clean; her memory is read before the visit is written. No save field changed.
- `NotebookHooks.cs` (R2): her camera facts are learned on the clean visit only.
- `GraceCameraRepairJob.cs` (R1): `ShutterIsTask` (the part switched on); the shutter gates the grade only then. TaskSummary drops the shutter on Day 1.
- `DeviceWake.cs` (R4, call 5): `ShutterReady`; with no working blade in the camera it only winds (`camera.wind`, no `camera.shutter`).
- `FaultSpeech.cs`: "Dirty Lens and Film Path" → "the lens and film path are dirty"; "Jammed Shutter" → "the shutter's jammed"; debris → "there's grit inside; it won't tick" (UI/UX §5.2). The old long camera row is gone. Card lines: Grace 8 words/42 chars, 4/30; Beatriz 7/46.
- `DayClock.cs`: code default 420 with comment; read-only `DayLengthSeconds`. The field's name is kept (four labs read it by reflection).

**Editor and checks**
- `Assets/Editor/DaysOneToThreeSteps.cs` (new): the steps (§2 below) and `Fixit Fidget › Checks › Days 1–3`.
- `ContinuationRuleChecks.cs`: 3 spec cases plus the 8-case downgrade table and the 90-character rule (console test: 107 assertions, PASS).
- `ContinuationIntegrationChecks.cs` ("Grace camera content"): fault 0 graded by spots with the shutter off; fault 1 Rejected untouched, Perfect swapped.
- `GraceRepairInteractionChecks.cs`: the shutter camera uses fault 1; the spots are brushed on a second, fault-0 camera (Perfect, R1). The lines the bench builder will patch (hold, swap, scrap) are unchanged.
- `Day1GuideCheck.cs`: the clock moves to the Officer's authored `arrivesAt` (+.01) instead of a hard-coded .36.
- `UpgradeEconomyChecks.cs`: patrons are counted by those in the café at once (stays read from the Patron prefab), not 14 a day. The walk-in list is in its day simulation.
- `Playtest4SystemsSteps.cs`: Lab C renamed "(the scene's day)"; new `Fixit Fidget › Days › Lab - Day 1/2/3 at the counter` (Day 2–3 lab saves carry Grace's Perfect clean, the Day 2 attempt, median money).
- `Diagnostics/DayShapeCheck.cs`: `dayNumber` with §10's bars (§3 below) and T11. Each run appends to `Logs/CafeLife/days-1-3-runs.csv`, and the report gives that day's medians.
- No day asset, prefab or scene was edited by hand: all of it goes through the step.

## 2. The Producer's Unity steps, in order

Run these after the bench builder's Bench 7 and the scale builder's camera rescale. Step B copies the jammed assembly as it then stands.

1. Compile. Open `Assets/Playtests/AcesCafeLayoutPlaytest.unity` and save it, so it has no unsaved changes (the step refuses otherwise).
2. **`Fixit Fidget › Days › Days 1–3 - the intro level (data)`** (scene open). Console: `[Days 1-3] Days 1-3 - the intro level (data)…: done (N change(s), M check(s) passed). Report: Logs/Days/days-1-3-all-<time>.txt`. The report lists items 1–25 (spec §9) and 26–27 (call 14), each old → new, then PASS lines.
   - It saves the five day assets, `GraceReunionCamera.prefab` and the scene.
   - Any FAIL undoes everything and saves nothing. A NOTE line means a value wasn't the expected old one: it was set anyway, except the Days 4–5 gaps, which are left alone so they are never scaled twice.
   - A second run prints "already done". Safe to run twice.
   - The same groups, one at a time: `A - The day: 420 s (scene)`, `B - Grace's camera: the clean and the shutter (prefab)`, `C – E` the three days, `F - Days 4-5: the 420 s stopgap`.
   - Look at DayClock: Day Length Seconds = 420. Look at the camera prefab: a new inactive child `Shutter mechanism (resting)` beside `Jammed shutter mechanism`.
   - The prefab change has no Unity undo; git reverts it.
3. **`Fixit Fidget › Checks › Days 1–3`** (scene open) → `Days 1–3 check: PASS (n passed, 0 failed)`, report `Logs/Days/check-days-1-3-last.txt`.
4. `Fixit Fidget › Checks › Day 1 onboarding` → PASS.
5. `Fixit Fidget › Checks › Grace episode and drink freshness rules` → PASS 107 assertions.
6. `Fixit Fidget › Checks › Grace camera content` → PASS.
7. `Fixit Fidget › Checks › Upgrade economy - the ladder and the first purchase` → PASS. Patrons should now read about $175 on Day 1, not $70.
8. Apply the patches in §5, then `Checks › Dialogue rules` → PASS.
9. `Fixit Fidget › Checks › Grace camera tweezers interaction`. Run it after the bench builder's own patches to this file, because it drives their new hold and swap.

## 3. Labs and checks, with pass lines

- **Checks › Days 1–3** passes when:
  - the schedule plays GraceCamera / GraceReturn / TheRealGame as Days 1–3;
  - the day is 420 s;
  - camera fault 0 turns on 3 spots + the resting shutter and not the part, and grades Rejected → Passable → Good → Perfect by spots (R1), with `ShutterReady` false (R4);
  - fault 1 turns on the part only, is Rejected untouched, Perfect swapped, `ShutterReady` true;
  - both faults make the episode's job (Clean / Shutter visit, $40 / $60);
  - every walk-in entry makes a job with an empty episode id;
  - no entry or featured repair needs a tool its day hasn't taught (RepairChecklist's tools; the circuit, the counter fault and a call wait for Day 4);
  - 200 rolls through the spawner's own `RollJob` bring exactly the day's list, in order, then only drinks;
  - Grace's lines are ≤ 90 characters a line; the card lines are ≤ 8 words and ≤ 46 characters;
  - Days 4–5 have the stopgap values.
- **Bench 8 (the bench builder's)** is the gate: Grace's clean ≤ 45 s, her swap ≤ 30 s.
- **`Fixit Fidget › Days › Lab - Day N at the counter`** (one Play session, about 8 min, the autopilot at human speed) passes when:
  - Day 1: first walk-in at the till ≥ 2:00; Grace handed back ≤ 2:00; T11: the first repair ≤ 50 % of the day; 0 storm-outs; the day ends ≤ 7:30; walk-ins brought only "Gunked Up".
  - Day 2: first walk-in ≥ 1:30; Grace ≤ 2:00; 0 storm-outs; the day ends ≤ 7:30; only "Gunked Up".
  - Day 3: her latte served ≤ 1:30; ≤ 1 storm-out (median over the runs file once it has 3 or more runs); the day ends ≤ 8:30; only debris → screen → seized, in order.
  - The spec asks for 20 runs a day. The report prints the medians so far.
- **Re-run:** Day 1 guide; Night 0, 1, 2; Grace at home and her window; Stations; Day intro; Reputation rules; Night 2 (Grace is now Day 3's featured opener, which its "Grace comes in first on Day 3" expects).

## 4. Deviations, and errors in the spec

- `GraceCameraEpisode.VisitFor(bool cleaningFault)`, not `VisitFor(FaultType)`. HomeRules, NightRules, NotebookRules and StorytellerRules compile this file without `FaultType.cs`. All five console projects build and pass here.
- `walkInRepairs` applies to walk-ins only (`profile == null`; the scene's `regulars` is empty anyway). It applies only on the asset's own day, so a repeated last day never runs out of bench jobs. When the list is used up, the guided lesson's forced repair falls back to the old pool; everyone else gets a drink.
- R4 is read from the camera itself: no working blade in it, no fire. There is no new state.
- R1 reads `shutter.gameObject.activeSelf`, the fault's own switch.
- Added: no photo news while the camera is in her hands. An old save's Day 1 attempt meets the new Day 2: the news waits for Day 3.
- The stopgap gaps are rounded to 0.1 s:
  - Day 4: 18/12/7/16 → 42/28/16.3/37.3;
  - Day 5: 16/7/26/6/15 → 37.3/16.3/60.7/14/35;
  - patience .95 → 1.0 and .9 → 1.0.
- "20 runs a day" is one run per Play session with a cumulative CSV, not 20 automatic runs.
- The data steps take no before/after photos. Nothing changes on screen until fault 0 is applied at runtime; Bench 5's Day 1 camera photo shows the resting shutter.
- `CafeLifeRecorder`, `NightOneSteps`, `NightTwoSteps`, `UpgradeEconomyPass`, `NotebookEntries`: unchanged (the spec says leave them).
- **Spec errors (found against the code):**
  - §7 says the Night 1 lab "still runs". It doesn't: `NightOneCheck.cs:383-421` expects her Day 2 latte and the photo news on Day 2, and `:284-285` expects Grace as Day 3's morning visitor. Patch below.
  - §5.6 says Night 0's "walked out" version is unchanged. But the hand-back is always "happy" (`CustomerBrain.cs:2119`), so a Rejected clean was caught only through the camera attempt, which R3 removes from Day 1. `NightZero.GraceLetDownOn` (`NightZero.cs:319-325`) needs a patch.
  - `DialogueRuleChecks.cs:535-545` and `NotebookPlayCheck.cs:81-85` build Grace's job with the default fault type (Mechanical, now the shutter visit) and expect Day 1's lines and facts. Patches below.
  - Systems step 11's spoken form "the shutter's still jammed" was replaced by Days' "the shutter's jammed" (§5.4).

## 5. Patches for other owners

**`Assets/Editor/DialogueRuleChecks.cs`** (nobody's). Two edits.

Edit 1, old:
```
                kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "jammed shutter",
                storyEpisodeId = GraceCameraEpisode.EpisodeId
```
new:
```
                kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "Dirty Lens and Film Path",
                faultType = FaultType.Cleaning, storyEpisodeId = GraceCameraEpisode.EpisodeId   // Day 1 is the clean (14 Oct)
```
Edit 2, old:
```
        lint.Speech("Grace's thanks for taking the camera", GraceCameraEpisode.AcceptedLine);
```
new:
```
        lint.Speech("Grace's thanks for taking the camera", GraceCameraEpisode.AcceptedLine);
        lint.Speech("Grace's shutter request, after a good clean", GraceCameraEpisode.ShutterIntake(true));
        lint.Speech("Grace's shutter request, otherwise", GraceCameraEpisode.ShutterIntake(false));
        lint.Speech("Grace's thanks for taking the camera again", GraceCameraEpisode.ShutterAcceptedLine);
        foreach (JobGrade shutterGrade in Enum.GetValues(typeof(JobGrade)))
            lint.Bubble($"Grace's shutter hand-back ({shutterGrade})", GraceCameraEpisode.ShutterCompletionLine(shutterGrade));
```

**`Assets/Editor/NotebookPlayCheck.cs`** (nobody's). Old:
```
            kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "sticky shutter",
            storyEpisodeId = GraceCameraEpisode.EpisodeId
```
new:
```
            kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "Dirty Lens and Film Path",
            faultType = FaultType.Cleaning, storyEpisodeId = GraceCameraEpisode.EpisodeId   // her story is told on Day 1's clean
```

**`Assets/Scripts/NightZero.cs`** (nobody's). Old:
```
        if (grace.lastSeenDay == day && !grace.lastVisitHappy) return true;
```
new:
```
        if (grace.lastSeenDay == day && !grace.lastVisitHappy) return true;
        // Days 1-3 (14 Oct): Day 1's clean records no photo attempt (R3); a camera handed back still grubby shows in her grade.
        if (grace.lastSeenDay == day && grace.lastGrade == nameof(JobGrade.Rejected)) return true;
```

**`Assets/Scripts/Diagnostics/NightOneCheck.cs`** (nobody's). Four edits.

(a) Old:
```
        if (recapDay == 2)
            Check(asMorningVisitor || FindGrace() != null, "Grace is due first thing, as the morning's visitor (Day 3 has no featured regular)");
```
new:
```
        bool asFeatured = spawner != null && spawner.Today != null && spawner.Today.featuredRegular != null
                          && spawner.Today.featuredRegular.PersistentId == Grace;
        if (recapDay == 2)
            Check(asMorningVisitor || asFeatured || FindGrace() != null, "Grace is due first thing (Day 3's featured regular since 14 Oct)");
```
(b) Old:
```
            Check(Line().IndexOf("latte", StringComparison.OrdinalIgnoreCase) >= 0 && !Line().Contains("{")
                  && Line().IndexOf("reunion", StringComparison.OrdinalIgnoreCase) < 0,
                $"her usual Day 2 visit follows: her latte first, in her own words (\"{Short(Line())}\")");
```
new:
```
            Check(Line().IndexOf("shutter", StringComparison.OrdinalIgnoreCase) >= 0 && !Line().Contains("{"),
                $"her Day 2 visit follows: her camera's shutter, in her own words (\"{Short(Line())}\")");
```
(c) Old:
```
        yield return Until(() => conversation.ReplyTexts.Contains(AceReplies.TakeDrink), 12f,
            $"Ace's replies come up once she has said it: \"{AceReplies.TakeDrink}\" (a drink, not a job)");
        Check(conversation.Highlighted == 0 && conversation.ReplyTexts.Count > 0 && conversation.ReplyTexts[0] == AceReplies.TakeDrink,
```
new:
```
        string take = morning == 2 ? AceReplies.TakeRepair : AceReplies.TakeDrink;   // Day 2 brings the camera (14 Oct)
        yield return Until(() => conversation.ReplyTexts.Contains(take), 12f, $"Ace's replies come up once she has said it: \"{take}\"");
        Check(conversation.Highlighted == 0 && conversation.ReplyTexts.Count > 0 && conversation.ReplyTexts[0] == take,
```
(d) Old:
```
            conversation.ChooseReply(0);   // what E on "Coming right up." does
            yield return null;
            string news = GraceCameraEpisode.ReturnNews(GracePhotoOutcome.Clear);
            Check(conversation.Closing && conversation.CurrentLine == news,
                $"once her order is taken she tells Ace about the photo and gives the print (\"{Short(conversation.CurrentLine)}\")");
            Check(saves.Notebook.Knows("grace.reunion.photo"), "the notebook has the photo");
            yield return Seconds(2.5f);
            yield return Photo("11-the-photo-after-the-order");
            yield return Until(() => !conversation.InConversation, 25f, "the conversation closes by itself once she has said it");
            yield return Seconds(.4f);
            string note = NoteShowing();
            Check(note == GraceCameraEpisode.HandoffLine(GracePhotoOutcome.Clear), $"a line on screen says what happened (\"{note}\")");
            Check(NoteClearOfPrompt(), "…above the prompt at the bottom of the screen, never on it");
            yield return Photo("12-the-print-left-for-the-shop");
```
new:
```
            conversation.ChooseReply(0);   // "I'll take a look."
            yield return null;
            Check(conversation.CurrentLine == GraceCameraEpisode.ShutterAcceptedLine,
                $"taking the camera, her Day 2 thanks; the photo's news waits for Day 3 (\"{Short(conversation.CurrentLine)}\")");
            Check(!saves.Notebook.Knows("grace.reunion.photo"), "no photo in the notebook yet");
            yield return Until(() => !conversation.InConversation, 25f, "the conversation closes");
```

**For the bench builder (FYI, not a patch):**
- `BenchLab` act three must swap the shutter on fault 1. On fault 0 the jammed part is inactive, and `Activate()` there would start a coroutine on an inactive object.
- Their patches to `GraceRepairInteractionChecks.cs` should target the hold/swap lines, which I left as they were.

## 6. Risks

- **Compile.** `DaysOneToThreeSteps.cs` builds clean in a dotnet harness against the real `Playtest4Shared`, `DayDefinition`, `DeviceDefinition`, `Job`, `FeaturedRepairRequest`, `GraceCameraRepairJob` and `GraceCameraEpisode`, with Unity stubbed (the harness catches injected errors). It uses `Playtest4Shared.Run`'s `internal` lists (no asmdefs: one editor assembly). The other edits are syntax-checked and reviewed by eye.
- **The bench rewrite.** "Grace camera content", the tweezers check and Checks › Days 1–3 call `ReplaceablePart.Activate()` in edit mode and `GrimeSpot.Scrub`. If the bench builder changes those, these checks fail at run time, not at compile time. The tweezers check also uses `HoldTick`/`HoldEnd`/`PartState.HeldBroken` at compile time.
- **The resting copy.** If the scale or bench steps re-bake the camera after step B, re-run B: it compares a signature (meshes, materials, poses) and re-copies.
- **15 fps.** I added no motion. The wind-only wake returns before its 0.5 s wait. DayShapeCheck samples every 0.1 s.
- **A data step skipped.** Without step B, Day 2's featured repair (fault 1) is invalid. The spawner logs it and Grace falls back to her latte, so it degrades gracefully.

## 7. Open items, in order

1. Apply §5's patches (Night 1 lab, Dialogue rules, Notebook play check, Night 0's let-down).
2. 20 runs of each Days lab (about 2.5 h of Mac time per day): the T19 medians.
3. Re-run step B when Art's A7 jammed assembly lands.
4. Day 3 patience 1.6 → 1.4 once Bench 8 is within its bars (spec §4.3).
5. Minor: `NotebookEntries.Backfill` teaches her four camera facts dated `graceCameraDay` (now Day 2) if Day 1's intake was never heard.
6. The upgrade ladder rescale after playtest 6 (spec decision 8).
