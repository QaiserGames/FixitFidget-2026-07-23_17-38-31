# uiux: checklist v2, builder's report (14 Oct 2026)

Worktree `/root/cafe/build/uiux`, branch `b-uiux`, on `431f081`. Every `.cs` passes `cs_syntax_check.py`. No scene or prefab changes: everything is made while playing.

## 1. What changed

**Commits:** `7871628` budgets, outline, shader · `b331266` BenchCards · `25be903` BenchGlow · `1c8e676` HUD, guide, tray · `11e93ac` checks, lab, menu, metas · `2e2b67d` guide re-reads on pickup.

| File | Change |
|---|---|
| `Assets/Scripts/WordBudget.cs` | Adds `BenchFirstRepair 25` (T7), `BenchSteady 24`, `BenchSteadyAim 15`, `BenchAlert 12`, `CardLineWords 8`, `CardLineChars 46`, `BenchVerb 2`, `BenchFlip 4`, `FirstRepairControls 11`, `ControlsLine 6`, `HelpHint 2`, `FinishedRow 6`. Still pure. |
| `Assets/Scripts/BenchCards.cs` | Rewritten. **The paper card** sits top right at (-24, -132), 360–480 wide, with tape, her words, step icons (current 56 px on a disc with its verb, done ones ticked, pips), the flip, and the finished line. **The controls card** is 11 words on the first repair. Tab or View: a press calls `BenchWork.Flash`; a 0.35 s hold shows the full card. **The help hint** reads "[Tab] Help". **The stuck clock**: a pulse at 12 s, the controls card back at 20 s, T17's one automatic flash. The 1.5 s ticked-but-unfinished flash. `checklist.tick` plays 0.12 s after a tick. Lab statics, pure word builders and `LabControls`. Keeps every static other files read. The old PlayerPrefs counter is gone. |
| `Assets/Scripts/BenchGlow.cs` (new) | The mint glow breathes every 1.6 s on the next thing to touch: the tool in its stand, then outlines on parts and screen rings on screws, holes and dirt. It is dashed when hidden, the whole device glows for face-down work, and the stuck pulse is 2.2× white to mint. "What's left" is gold on `Flashed`: steady outlines and rings, two waves per spot, a white flash when nothing is left. Plays `bench.showleft`. Adds `BenchRing` (a MaskableGraphic). |
| `Assets/Scripts/BenchOutline.cs` | The pointer's layer (`Set`: hover, busy) over the guide's (`Glow`), each with colour, width (`_Width` per hull) and x-ray; shown by `forceRenderingOff` (header says why); `Guide`, `Left`, `PointerOn`. |
| `Assets/Data/Resources/Bench outline.shader` | Adds the "Hidden" pass (SRPDefaultUnlit, ZTest Greater, front faces, rim only, diagonal 4 px dashes at 1080p, 70 %), drawn only when `_XRay` = 1. |
| `Assets/Scripts/ShopUI.cs` | No prompt line at the bench. |
| `Assets/Scripts/HudCorners.cs` | At the bench "TODAY", the sign's under-line and the day line fade out (0.2 s). |
| `Assets/Scripts/TicketRailUI.cs`, `JobTicket.cs` | At the bench, tabs become 160 × 44 chips (face, tape, 104 × 8 bar; a ringing call keeps icon and seconds), pulsing at ≤ 25 % patience; right reserve 512; no details. Adds `InService`. |
| `Assets/Scripts/DayOneGuideUI.cs` | No guide card at the bench. The impatience card uses bench words (`BenchReassureWords`, no title) at bottom-centre 26. Adds `CarryingToBench`. `RepairBenchAction`, `Aim`, `Hold` and `Select` are deleted. A pickup re-reads the step at once. |
| `Assets/Scripts/DropInteractable.cs` | Adds `Takes`, `TrayWords`, `PutBackWords`, `LiftedFromTray`, and the runtime `IntakeTrayWatch`, which records a device lifted from the tray. |
| `Assets/Editor/BenchWordsChecks.cs` (new) | Fixit Fidget › Checks › Bench words (§6 A, plus the screens added up). |
| `Assets/Scripts/Diagnostics/BenchWordsCheck.cs` (new), `Assets/Editor/BenchWordsMenu.cs` (new) | Fixit Fidget › Bench › Bench 6 - Words on the bench screen (lab, a check). |
| `.meta` for the 4 new scripts | Fresh GUIDs. |

Untouched: `ControlHints`, `UiSkin`, `PausePhone`.

## 2. The Producer's Unity steps

There are no editor steps: nothing in the scene changes. After the compile:
1. Fixit Fidget › Checks › Pick up scripts changed from outside (reimport), outside Play mode.
2. Merge after the bench builder: my files need its contract code (`BenchWork`, the `Step` fields, `IsSeen`, the `BenchStage` fields, `BenchStageContract`).

## 3. Labs and checks, in order

1. **Fixit Fidget › Checks › Bench words** (edit mode). Pass line: `[Bench words] PASS: all N checks.` Report: `Logs/BenchWords/edit-check-<time>.txt`. Expect named FAILs until the days builder's `FaultSpeech` rows and the camera split land: the watch's debris line (Beatriz, 10 words) and the camera's old one-fault label (13 words). Once they land, every line must be ≤ 8 words and ≤ 46 characters.
2. **Fixit Fidget › Checks › Day 1 onboarding**, after patch B.
3. **Bench 6**, with the café scene open, the Game view **1920 × 1080** and Unity in front. Pass line: `[Bench words lab] All N checks passed.` Then again at **2560 × 1600**. Read the "Totals" block: S1 ≤ 25 (both inputs), S2–S4 ≤ 24 (a note when over 15), S6 alert ≤ 12. It waits up to about 4.5 min for Grace's real visit. With Unity in the background, S5 and the cursor-on-the-lens hover are left out (the hover then counts as 1).
4. Day 1 guide (patch A), Bench 5 (patch D), Bench 3 (patch E), Ticket layout, Controller.

## 4. Deviations from the spec

- **The Producer's calls applied:** `BenchFirstRepair = 25` (not 39). The first-repair card is the 11-word version from call 6, and "(hold: controls)" appears only on the held card. Call 10: the glow is always on, with no setting. Direct grab: the mouse card says "[Drag a tool] Use it", and no line says "click it in the caddy".
- **The current step's pips** follow its verb on one line ("Unscrew ●●○○"); other steps' pips sit under their icons. The space under the big icon is the verb's.
- **"Done" icons** are tinted grey at 80 %, not desaturated (there is no UI shader for it). The check badge carries the state.
- **The bench impatience card has no title** at the bench. It is 9 words, and with a title it would not fit U6's 88 px.
- **"What's left" outlines thin out** (the outline pass is opaque, so it can't fade); the mint glow steps aside for its 1.5 s.
- **`bench.showleft`** plays in `BenchGlow`'s `Flashed` handler, whoever calls `Flash`; if `BenchWork.Flash` plays it too, the bank's 0.05 s gap plays it once.
- **"Put it back"** is tracked in `DropInteractable` (a once-a-frame tray watch over `TicketRailUI.InService`), not `PlayerCarry.LiftedFrom`, which no contract defines: no patch for the scale builder.
- **The guide keeps `RepairAction`'s "Fixed! Press E…"**: never drawn at the bench, but `DayOneOnboardingChecks.cs:194-197` reads it.
- **`BenchWordsChecks.cs` is in `Assets/Editor/`** (the spec's path). It adds check 7: the screens added up.
- **Bench 6:** S2 borrows Grace as the watch's owner (a named line). S3b's dashed check runs only if a target is out of sight. Pad words come from the input flag (works unfocused). In labs the controls card sits 92 px up, over the IMGUI banner, so its U5 check is skipped.

**Spec against the code:**
- `DropInteractable` is in **no** scene or prefab (GUID `e30c9239…` has 0 references in `AcesCafeLayoutPlaytest.unity`). The café's tray prompt is `StationInteractable.cs:172-182` (the counter's drop spot), so patch C is the one that fixes First Hour #4.
- §5.1 row 9 counts the S1 hover as "Lens", 1 word. §5.10 item 7 says "needs the cloth" when the wrong tool is in hand. At S1 nothing is in hand. If the bench's hover says "Lens / Needs the cloth", S1 is 28 words, over 25.

## 5. Patches for other owners

**A. `Assets/Scripts/Diagnostics/Day1GuideCheck.cs` (days)**

A1, before:
```csharp
        Check(!grace.JobReady && !prompt.Contains("Hand it back") && !prompt.Contains("unfixed"),
            $"...and E won't give it straight back to her unfixed (the bottom line: \"{(prompt.Length > 0 ? prompt : "nothing")}\")");
```
After (adds a check below):
```csharp
        Check(!grace.JobReady && !prompt.Contains("Hand it back") && !prompt.Contains("unfixed"),
            $"...and E won't give it straight back to her unfixed (the bottom line: \"{(prompt.Length > 0 ? prompt : "nothing")}\")");
        // Checklist v2 §5.7: on the carry step the intake tray doesn't offer it back either; the line shows only "In hand".
        Check(!prompt.Contains(DropInteractable.IntakeTrayWords) && !prompt.Contains(DropInteractable.PutBackWords)
              && Plain(shop.PromptLine).Contains("In hand"),
            $"...nor does the intake tray (the line: \"{Plain(shop.PromptLine)}\")");
```
A2, before:
```csharp
        Rect card = guide.CardOnScreen;
        float hud = HudScale();
        Check(guide.CardShowing && guide.CardAtBench, "at the bench the card sits near the work, over the bottom prompt (not in the top left)");
        Check(Mathf.Abs(card.center.x - Screen.width * .5f) < 2f && card.yMin >= 150f * hud && card.yMax <= Screen.height * .45f,
            $"...centred, from {card.yMin / hud:0} to {card.yMax / hud:0} px up at 1080p (the prompt's top is about 142)");
```
After:
```csharp
        // Checklist v2 §5.6: no guide card at the bench; the bench's own card names her and the step's tool glows.
        Check(!guide.CardShowing, "at the bench there is no guide card: the bench's card and the glow are the guide");
        Check(BenchCards.Showing && BenchCards.JobLine.StartsWith("Grace:"), $"...the bench's card names her: \"{BenchCards.JobLine}\" / \"{BenchCards.CurrentVerb}\"");
        Check(BenchGlow.GlowingTool != null && BenchCards.CurrentStep != null && BenchGlow.GlowingTool.tool == BenchCards.CurrentStep.tool,
            $"...and the step's tool glows in its stand ({(BenchGlow.GlowingTool != null ? BenchGlow.GlowingTool.displayName : "nothing glows")})");
```
A3, before:
```csharp
        yield return Await(() => BenchCards.ControlsText.Contains("[Click a tool]"), 2f);
        string controls = BenchCards.ControlsText;
        Check(controls.Contains("[Click a tool]") && controls.Contains("[Drag]") && controls.Contains("[Right-click]") && BenchCards.ControlsInFull,
            $"the mouse's controls card, in full, with its real bindings: \"{Plain(controls)}\"");
```
After:
```csharp
        yield return Await(() => BenchCards.ControlsText.Contains("[Drag a tool]"), 2f);
        string controls = BenchCards.ControlsText;
        Check(controls.Contains("[Drag a tool]") && controls.Contains("[Right-click]") && controls.Contains("[Tab]") && BenchCards.ControlsShowing,
            $"the first repair's controls card, in the mouse's words: \"{Plain(controls)}\"");
```

**B. `Assets/Editor/DayOneOnboardingChecks.cs` (days)**

Before:
```csharp
        Require(calm.Contains("Walter") && calm.Contains("press E") && calm.Length <= 90, $"The impatience card says how to reassure (\"{calm}\").");
```
After (adds two checks below):
```csharp
        Require(calm.Contains("Walter") && calm.Contains("press E") && calm.Length <= 90, $"The impatience card says how to reassure (\"{calm}\").");
        string atBench = DayOneGuideUI.BenchReassureWords("Walter");
        Require(atBench.Contains("Walter") && atBench.Contains("[Right-click]") && atBench.Contains("[E]") && WordBudget.Words(atBench) <= WordBudget.BenchAlert,
            $"At the bench the impatience card says to step back first, in the bench's keys (\"{atBench}\").");
        Require(DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, true) == DropInteractable.PutBackWords
                && DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, false) == DropInteractable.IntakeTrayWords,
            "At the intake tray a device lifted from it reads \"Put it back\", any other \"Put it on the intake tray\".");
```

**C. `Assets/Scripts/StationInteractable.cs` (cine)**

C1, before: `            return c != null && c.IsCarrying && dropSpot != null;`
After: `            return c != null && c.IsCarrying && dropSpot != null && DropInteractable.Takes(dropSpot, c.Carried);`

C2, before:
```csharp
            if (!dropSpot.CanAccept(c.Carried)) return "No room here";
            // The intake tray: "Return item" read like giving it back to its owner (DropInteractable).
            return dropSpot.Kind == DropSpot.SpotKind.Counter ? DropInteractable.IntakeTrayWords : "Set down";
```
After:
```csharp
            // The intake tray: "Return item" read like giving it back to its owner; "Put it back" for a device lifted from it;
            // nothing on Day 1's carry step (DropInteractable, checklist v2 §5.7). The bench: "Set down"; full: "No room here".
            return DropInteractable.TrayWords(dropSpot, c.Carried);
```
C3, before: `        if (c == null || !c.IsCarrying || dropSpot == null) return;`
After: `        if (c == null || !c.IsCarrying || dropSpot == null || !DropInteractable.Takes(dropSpot, c.Carried)) return;`

**D. `Assets/Scripts/Diagnostics/BenchViewProbe.cs` (bench).** Rebase onto their Bench 5 rewrite if needed.

D1, before:
```csharp
        Check(BenchCards.Showing, $"{name}: the checklist and the controls card are up");
        Check(!BenchCards.StripOnScreen.Overlaps(device2D) && !BenchCards.CardOnScreen.Overlaps(device2D),
            $"{name}: the cards cover none of it (it {Px(device2D)}; the checklist {Px(BenchCards.StripOnScreen)}; the controls {Px(BenchCards.CardOnScreen)})");
        string list = Plain(BenchCards.ChecklistText), controls = Plain(BenchCards.ControlsText);
        string device_ = job.Record != null ? job.Record.deviceName.ToLowerInvariant() : "";
        Check(list.ToLowerInvariant().Contains(device_) && BenchCards.Steps != null && BenchCards.Steps.Any(step => step.current),
            $"{name}: the checklist names it and gives the step to do now: \"{list}\"");
        Check(controls.Contains("Turn it") && controls.Contains("Flip it over") && controls.Contains("Pick it up and step back"),
            $"{name}: the controls card says how to turn it, flip it and take it away: \"{controls}\"");
```
After:
```csharp
        Check(BenchCards.Showing, $"{name}: the job card is up");
        Check(!BenchCards.CardOnScreen.Overlaps(device2D) && !BenchCards.ControlsOnScreen.Overlaps(device2D) && !BenchCards.HintOnScreen.Overlaps(device2D),
            $"{name}: the cards cover none of it (it {Px(device2D)}; the job card {Px(BenchCards.CardOnScreen)}; the controls {Px(BenchCards.ControlsOnScreen)}; the hint {Px(BenchCards.HintOnScreen)})");
        string list = Plain(BenchCards.ChecklistText), controls = Plain(BenchCards.HeldControlsText);
        Check(BenchCards.CurrentVerb.Length > 0 && BenchCards.Steps != null && BenchCards.Steps.Any(step => step.current),
            $"{name}: the job card gives the step to do now: \"{list}\"");
        Check(controls.Contains("Turn it") && controls.Contains("Flip it over") && controls.Contains("Pick it up"),
            $"{name}: the full controls card says how to turn it, flip it and take it away: \"{controls}\"");
```
D2: replace each `controls = Plain(BenchCards.ControlsText);` and `string controls = Plain(BenchCards.ControlsText);` in `PadButtons()` (three lines) with `HeldControlsText`. Also replace `+ "]  Pick it up and step back"` with `+ "]  Pick it up"`.

**E. `Assets/Scripts/Diagnostics/BenchLab.cs` (bench).** Rebase onto their Bench 3 rewrite.

E1: in `string card = Plain(BenchCards.ControlsText);` and `card = Plain(BenchCards.ControlsText);`, replace `ControlsText` with `HeldControlsText`.

E2, before:
```csharp
            string list = Plain(BenchCards.ChecklistText);
            Check(list.Contains("Take out the 2 screws") && list.Contains("flip it over"),
                $"face up, the checklist's step is the screws, and it says they're on the other side: \"{list}\"");
```
After:
```csharp
            Check(BenchCards.CurrentIcon == "screwdriver" && BenchCards.CurrentVerb.Contains("Flip it over"),
                $"face up, the card's step is the screwdriver, and it says to flip it over: \"{BenchCards.CurrentIcon}\" / \"{BenchCards.CurrentVerb}\"");
```
E3, before:
```csharp
            string list = Plain(BenchCards.ChecklistText);
            Check(list.Contains("hold on each one") && !list.Contains("flip it over"),
                $"back up, the step says how instead: \"{list}\"");
```
After:
```csharp
            Check(BenchCards.CurrentVerb == "Unscrew", $"back up, the card's verb is \"{BenchCards.CurrentVerb}\" (the screws are in reach)");
```

**F. `Assets/Scripts/RecapPhone.cs` (no owner; needs the bench builder's `GameSettings.Vibration`).** This is Systems' decision 13, the Vibration row that "UI/UX places". After the `"swapsticks");` line, add:
```csharp
        // Pad rumble at the bench (14 Oct, Systems' decision 13: "feel the impact"); on by default. PLACEHOLDER COPY.
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Vibration", "On a pad: the bench's tools rumble as they work.", GameSettings.Vibration,
            () => Flip(() => GameSettings.Vibration = !GameSettings.Vibration), "vibration");
```

**CustomerBrain (npc): none needed** (the chips hide `TabLines`).

## 6. Risks

**Most likely to fail to compile:**
- My code calls the contract members exactly as written, so any drift in the bench builder's names breaks `BenchCards`, `BenchGlow` and both checks. The members: `BenchWork.Flash/WorkLeft/Progressed/TargetDone/Flashed/Kind/Item`, `Step.verb/icon/targets/left/total`, `ItemInspector.IsSeen`, `BenchStage.cups/padSmall/padLarge/wellSmall/wellLarge/scrapSlots/toolSlots`, and `BenchStageContract.Rect/Zone`.
- `VertexHelper.AddVert(Vector2, Color32, Vector2.zero)`, the same call shape as `SupportCallIcon`.
- The shader's second pass: if URP skips the SRPDefaultUnlit pass, hidden targets are simply not dashed. Nothing errors.

**Most likely to misbehave:**
- **S1 has zero slack:** 8 + 1 + 11 + 1 + 4 = 25. A bark, a two-word hover or a ringing call's seconds (13 px: also under 16) puts it over.
- **At 15 fps:** the first whole-device glow (face-down work) builds a hull per renderer in one frame, a one-time hitch per device. Rings rebuild their mesh every frame while breathing (≤ about 20 rings). The card lays out only while something moves. Every timer runs on `UiClock` (frame-rate independent).
- **The stuck clock** trusts `BenchWork.Progressed`. If scrubbing doesn't raise it, the 12 s pulse can fire mid-scrub.
- **"Put it back"** needs the device's customer to have a tab (`InService`); otherwise E falls back to "Put it on the intake tray".

## 7. Open items, in order

1. Apply patches A–E, plus F once `GameSettings.Vibration` exists.
2. The bench's hover words must be the part's name only when nothing is in hand, or S1 is over 25 (§4).
3. The Controller check "View at the bench doesn't switch the view" (§6 C) is not written; it is in cine's `ControllerChecks`.
4. The circuit step shows the fallback disc. The board's own tile sprite is not wired.
5. Bench 6 drives no stock-chip alert (S6 is the impatience card).
6. Flagged by the spec and still the Producer's call: the pause phone can't open at the bench, and the "In hand" line repeats the fault label off the bench.
