*The Bench lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

## Lead 4, the Bench: report (playtest 4 fixes, 10 Oct)

All 14 delivered C# files pass the syntax check. Nine of them were also compiled with Roslyn against stand-ins for every Unity and project signature they use: BenchLab, DispenserFrameCheck, Playtest4BenchSteps, JobBase, RemovablePart, ScrewTarget, ToolPickup, ReplaceablePart, and ItemInspector's changed methods. Each stand-in signature was copied from the repo, and none of them gave an error. The other files were checked line by line against the repo: Juice, GrimeSpot, PlaceholderSounds, DeviceWake and JuiceCheck. Nothing has run in Unity yet.

### 1. What I did, finding by finding

**B1. One ending.** Veteran: *"'Fixed!' fires … the moment the new screen seats. The cover is still on the mat and both screws are still out … 'Back together · Perfect' then fires in orange."*
- **When the last task is done** (last part seated, last fleck cleaned), the device wakes quietly. It uses its existing wake (screen lights, watch winds, camera shutter fires) and its own sound. No words appear.
- **"Fixed! · Perfect" fires once**, with the big sparkle and the chime, when the device is back in one piece with every fault fixed. This is the new `JobBase.IsFinished`, which means `CanHandBack` and quality 1, so the grade shown is always Perfect.
  - Opened device: it fires on the last screw.
  - Device never opened (cleaning only): it fires 0.3 s after the wake, so the device "speaks" first.
- **A device closed up with faults left** gets a quiet "Back together · Good/Passable": smaller, no sparkle, a softer chime.
- **Same rule for the phone, the watch and Grace's camera.**
- **Unchanged:** `CanHandBack`, the grade formula, and payout, ticket, reaction and hand-back. Those all happen at hand-back, as before. The only readers of the "Fixed!" moment were Bench 3 and JuiceCheck, and both are updated. The Day 1 guide's "Fixed!" line (UI's file) fires on `IsComplete`, which is now the same moment.
- **Claude, please update the bench spec** (`claude/bench-spec-v2.md`):
  - §2.5: the device wakes at the last task; "Fixed! · grade" fires when it is back together with every fault fixed.
  - §7: the "'Fixed!' on the last seat" call is replaced.
  - §6 D: the build refuses to hand back a device with its cover off, and that rule is kept.
  - The status line.

**B2. "Hold to unscrew" on a finished device.** Both playtesters saw it.
- On a finished device a seated screw is `Done`. It can't be worked: the hover reads "Screw" with no verb, the driver held on it does nothing, `Activate` does nothing, and the pad's aim help skips it.
- The seated cover on a finished device shows no verb either.
- A device closed with faults left can still be opened.

**B3. The cloth hides the glass.** Veteran: *"The cloth covers about two-thirds of the screen … the smudge thinning and the gleam both happen under it."*
- **In the hand it is a pad.** The folded cloth is 75 × 90 mm and becomes 34 × 41 mm (45% of its size, 1.4× thicker), its far fold still leading the cursor. It trails on its spring, so the glass it has just wiped shows behind it. Expected cover: about 15% of the glass; the check's bar is 30%.
- **When a smudge comes clean it lifts off.**
  - It moves 35 mm up and 60 mm back toward Ace's hand, a little to the right, tipping 12°.
  - It rises in 0.45 s, stays up 1.1 s, then comes back down over 0.3 s.
  - A new "cloth.lift" swish plays (placeholder made in code).
  - Wiping another smudge brings it straight back down.
- **The gleam starts 0.15 s after the smudge clears**, on clear glass.
- I didn't use translucency: a see-through cloth reads as a ghost, not an object, and needs a transparent material.

**B4. "Fixed!" is pale green on a lit white screen.**
- Every word pop now sits on a dark plate (the HUD's band colour at 86%), and keeps its own colour.
- Contrast on the plate, measured over white (the worst case):

| Words | Contrast |
|---|---|
| "Fixed!" | 9.2:1 (it was 1.2:1 on the white screen) |
| Perfect gold | 5.4:1 |
| Good green | 6.2:1 |
| Passable grey | 6.5:1 |
| "Back together" | 9.8:1 |
| "New part" | 6.2:1 |

- The words sit just above the device's top edge on screen and are kept inside the screen.
- "Fixed!" stays up 2.1 s; "Back together" 1.6 s.

**B5. The pry tool stands upright.** Veteran: *"stands upright on the middle of the back like the screwdriver, instead of levering at the seam."*
- **Over a cover it can pry**, the pry sits with its blade in the seam nearest the cursor, the handle out across the seam at 32°.
- **Held**, the blade slides 3 mm under the edge and the handle levers down (up to 16°). The cover pops about the opposite edge.
- **Popped**, the blade stays under the lifted edge, including during the 0.15 s pop.
- **Elsewhere** it is held low at 38° toward Ace's right hand, never upright.

**B6. "Remove the back cover first" reads as a gate, not a cause.**
- Covered parts now give the cause:
  - "Cracked screen: Its cable plugs in under the back cover: take the cover off first" (65 characters).
  - The watch's mainspring: "It sits inside, under the back cover: take the cover off first" (61).
- With screws still in, the cover says "Held on by its screws: take them out first" instead of "Screws first".
- There is no unplug step (see flags).

**B7. The dispenser cuts off two drinks.** First hour: *"Only 4 of the 6 paddles are fully in view: Americano is cut by the screen's edge and Latte is off it."*
- **The cause.** Both evidence photos were taken after the Stations lab aims the crosshair at the cup stack, which a player must also do to take a cup. At rest the close-up already framed everything (about 18% margin). Aimed at the cup stack, Latte's paddle, its cup place and the discard basin go off screen.
- **The close-up (a scene step, "1 - Frame the whole dispenser").**
  - It now opens aimed at the cup stack, about 0.5 m further back and a little to the right.
  - All 14 targets are inside a 16:9 frame with at least 4% margin: six paddles, six cup places, the cup stack, the basin.
  - Turned onto Latte's cup place, the cup stack and every paddle stay in frame.
  - It stays inside BeverageLook's turning limits, with nothing solid in the way.
  - Why the cup stack: the crosshair picks from the screen's middle, and every drink starts with a cup, so the first press works with no look.
- **First person.** The look is the cause, not the stand point. Looking at the dispenser's middle from the stand point shows everything with about 10% margin. Nothing in first person sets the look; it's the player's own. So I made no scene change.

**B8. Anything else.**
- Grace's camera is now played in Bench 3 (the first-hour report asked for it).
- Not mine, so I routed them:
  - The "#2" job digit and the mouse tool line go to UI.
  - The patrons' "+$5" pop goes to Systems; JuiceCheck makes its own pops, so it doesn't depend on theirs.
  - "The bench reads as a wood strip from above / the REPAIRS sign" goes to UI or Tray (labels from above).
  - Upgrade values (Magnetic Driver, Wide Brush) are economy, not bench.

### 2. Decisions his to overrule
1. **One-ending details:** "Fixed! · Perfect" always shows the grade. For a never-opened device the words come 0.3 s after the wake.
2. **Dark plate on every word pop.** "New part" changes from the brand's dark green to its light green, because the dark green would sink into the plate (2.2:1).
3. **Cloth:** a 45% pad, 1.4× thick; the lift is 35 mm up and 60 mm back, held 1.1 s. That hold is longer than your 0.4–0.6 s: the lift itself takes 0.45 s, but the cloth stays clear until the screen is fully lit (about 1.1 s) and "Fixed!" is up. It is back on the glass at 1.85 s. The cloth lifts after every smudge, not only the last one.
4. **Pry angles:** 32° at the seam, 38° held elsewhere, blade 3 mm in, lever up to 16°.
5. **Hint wording** as quoted under B6. "Held on by its screws" applies the same cause-not-gate idea to the cover.
6. **Close-up start aimed at the cup stack.** The alternative is aiming at the middle of the paddles; both frame everything. Cost: the paddles are about 62 px tall at 1080p instead of 76 px at rest, but none leave the screen while working.
7. **First-person stand point not moved.**
   - To frame all six even while aimed at the cup stack, it would have to go about 0.46 m back.
   - That would widen the overlap between the dispenser's E offer and the counter's area from 0.145 m to 0.355 m. In that strip, E opens the dispenser instead of talking to the queue.
8. **A finished device can't be reopened at all.** Its screws and cover go quiet.

### 3. Files (all under `/home/claude/cafe/s7/bench/`)
- `Assets/Scripts/JobBase.cs`: adds `IsFinished`.
- `Assets/Scripts/Juice.cs`: one ending (quiet wake, `FixedMoment` once, quiet `BackTogether`). Plate behind words, words above the device and kept on screen, delayed sparks and gleam. Counters `Finishes`, `Wakes`, `LastWordsContrast`; `ContrastOnPlate` and `FixedWordsColour` for the steps.
- `Assets/Scripts/ScrewTarget.cs`: `Done`; a finished device's screws offer nothing.
- `Assets/Scripts/RemovablePart.cs` *(beyond my table; bench-only)*: `SeamAt`, `PryPose`, `Popping`; pop hinge from the seam; the screws hint; no verb on a finished device. Reflection names are unchanged, including `State`'s private setter.
- `Assets/Scripts/ReplaceablePart.cs`: `CoveredHint`; "New part" colour; only one shutter sound when Grace's camera wakes.
- `Assets/Scripts/ToolPickup.cs`: the cloth pad, `LiftOff`/`Land`, the pry held low, `FollowPose` at the seam.
- `Assets/Scripts/ItemInspector.cs`: lift after a clean wipe; land when wiping again; the pry at the seam (hover, hold and pop); hover text for finished screws.
- `Assets/Scripts/GrimeSpot.cs`: the gleam and its sound 0.15 s late (`GleamAfter`).
- `Assets/Scripts/PlaceholderSounds.cs` *(beyond my table; bench-only)*: adds the "cloth.lift" swish.
- `Assets/Scripts/DeviceWake.cs` *(beyond my table; comment only)*: the header now says the wake is quiet.
- `Assets/Scripts/Diagnostics/BenchLab.cs`: Bench 3 updated. About 160 checks (was 128) and 23 photos, timed by the clock rather than by frame counts. New act three: Grace's camera.
- `Assets/Scripts/Diagnostics/JuiceCheck.cs`: section 6 now uses `FixedMoment`, checks contrast, and adds a "Back together" check with photo 61.
- `Assets/Scripts/Diagnostics/DispenserFrameCheck.cs` *(new lab)*: the dispenser's frame, close-up and first person.
- `Assets/Editor/Playtest4BenchSteps.cs` *(new)*: 0 - Check first, 1 - Frame the whole dispenser (scene), 2 - the lab launcher.

Unchanged: GraceCameraRepairJob, RepairJob, StationCloseUp, JuiceSteps, BenchClothSteps, BenchFeelSteps, DeviceCleaningReachabilityChecks, GraceRepairInteractionChecks.

### 4. Unity steps, in order
1. **Copy the files in and let Unity compile.** Expect no errors.
2. **Fixit Fidget › Playtest 4 › Bench › 0 - Check first** (read-only). Report: `Logs/Playtest4/bench-0-check-first-last.txt`; photos in `Logs/Playtest4/bench-0-check-first-photos-<time>/` (1–5). You should see:
   - The contrasts listed under B4, all passing.
   - The two hints under B6.
   - The cloth pad ≈ 34 × 41 mm, and the "cloth.lift" sound present.
   - The close-up: now about 18% margin at rest, with Latte's paddle, its place and the basin cut when turned onto the cup stack. Then the planned pose.
   - First person: about 10% margin looking at the middle; 4 of 6 paddles aimed at the cup stack; the stand-point alternative (0.46 m back, overlap 0.145 → 0.355 m).
3. **Fixit Fidget › Playtest 4 › Bench › 1 - Frame the whole dispenser (scene).**
   - Expect "DONE, 1 change": `Drink station camera` local position goes from (0, 0.770, −1.280) to roughly (0.15–0.25, 0.75–0.85, −1.75…−1.85), turned onto the cup stack. The report prints the exact numbers.
   - About 37 checks pass.
   - Photos `1-before`, `2-after` and `3-after-on-the-far-cup-place` in `Logs/Playtest4/bench-1-frame-the-dispenser-photos-<time>/`. Look at them, then File › Save.
   - Run it again after saving: it should say "NOTHING TO CHANGE".

### 5. Labs, in order
1. **Fixit Fidget › Playtest 4 › Bench › 2 - The dispenser's frame - play check (lab, Play Mode)**, about 25 s, 13–14 checks. Output: `Logs/Bench/dispenser-frame-<time>/`.
   - Pass: the close-up opens with the crosshair on the cup stack and every target at least 4% inside the frame; the smallest paddle is at least 48 px; turning to Latte's cup place keeps the cup stack and the paddles in frame; in first person, looking at the middle shows all six paddles and the cup stack.
   - Photos: `1-close-up-start`, `2-close-up-on-the-far-cup-place` (only when aim help is on), `3-first-person-at-the-stand-point`, `4-first-person-on-the-cup-stack`.
   - A player sees: E at the dispenser opens on the cup stack with all six drinks, the cup places and the basin in view.
2. **Fixit Fidget › Bench › Bench 3 - The bench - play check (lab, drives itself)**, about 60 s (was 44), about 160 checks, 23 photos. Photos to look at:

| Photo | What it should show |
|---|---|
| `01b-the-cable-under-the-back` | The covered hint |
| `04b-the-pry-at-the-seam` | The pry low in the seam |
| `05-popped` | The blade under the lifted edge |
| `08-the-phone-wakes` | Lit screen, no words (renamed from 08-fresh-part-in) |
| `10-fixed-once` | One plated "Fixed! · Perfect" after the last screw |
| `10b-nothing-to-undo` | "Screw" with no verb |
| `13a-the-cloth-wiping` | The pad |
| `13b-the-cloth-lifts` | Cloth up, gleam on clear glass (replaces 13-wiped) |
| `14-clean-and-lit` | Lit screen, "Fixed!", cloth still up |
| `15`, `16` | Grace's camera before; after (shutter fired, one ending, strap on) |

3. **Fixit Fidget › Playtest › Juice - Photograph the feedback (lab, Play Mode)**, 1–3 min. `60-fixed` now shows "Fixed! · Perfect" on its plate; new `61-back-together`; two more checks.
4. **Existing checks my changes could affect.** A pass means they still pass as before:
   - **Checks › Grace camera tweezers interaction**: all reflection names are kept.
   - **Checks › Every device's grime can be brushed**: the edit-mode cleaning path is unchanged.
   - **Playtest › Stations as reach**, overhead and first person: "crosshair on the cup stack" is now immediate, and the overhead `drink-2` photo shows all six. First person is unchanged.
   - **Playtest › Aim help**: first person at the dispenser is unchanged, and the bench part uses a fresh device.
   - **Checks › Controller (Play Mode) › Run controller check**: the right-stick look stays within its limits from the new rest.
   - **Checks › Day 1 onboarding**.
   - The café redesign's Bar 2 survey checks the drinks stand point, which didn't move. Cameras aren't among the gameplay anchors other steps protect.

### 6. Needs another lead's file
Nothing is required. One optional evidence change for **Systems, `Diagnostics/StationsCheck.cs`, `MakeDrink`**: insert this just before `yield return AimAt(Point(stack));`.
```csharp
        if (firstPerson)
        {
            // The whole dispenser first, as a player walking up looks at it (playtest 4, B7: aimed at the cup stack the far paddles are off screen).
            Bounds face = new Bounds(Point(stack), Vector3.zero);
            foreach (BeverageControl c in drinks.GetComponentsInChildren<BeverageControl>()) face.Encapsulate(Point(c));
            yield return AimAt(face.center);
            yield return Photo($"{customer}-2a-the-whole-dispenser");
        }
```
Audio can give "cloth.lift" a real file later; the placeholder plays until then.

### 7. Open questions and flags
- **Unplug step (B6):** not built. A ReStory-style "unplug the flex" would be a new verb. Flag it if you want it in a later session.
- **Locks:** none touched (Grace's Day 1 latte, Night 0's deal at the bins).
- **BeverageLook's turning limits** (±52°, 28° up, 34° down) are hardcoded in a file nobody owns. Step 1 respects them and doesn't change them.
- **Overhead close-up:** Ace is hidden there (body hidden at a station), so moving the camera back puts nothing of him in view. The cup in hand is drawn at a fixed spot on the screen, so it doesn't move with the camera.

### 8. Syntax check output
```
OK    ./Assets/Editor/Playtest4BenchSteps.cs
OK    ./Assets/Scripts/DeviceWake.cs
OK    ./Assets/Scripts/Diagnostics/BenchLab.cs
OK    ./Assets/Scripts/Diagnostics/DispenserFrameCheck.cs
OK    ./Assets/Scripts/Diagnostics/JuiceCheck.cs
OK    ./Assets/Scripts/GrimeSpot.cs
OK    ./Assets/Scripts/ItemInspector.cs
OK    ./Assets/Scripts/JobBase.cs
OK    ./Assets/Scripts/Juice.cs
OK    ./Assets/Scripts/PlaceholderSounds.cs
OK    ./Assets/Scripts/RemovablePart.cs
OK    ./Assets/Scripts/ReplaceablePart.cs
OK    ./Assets/Scripts/ScrewTarget.cs
OK    ./Assets/Scripts/ToolPickup.cs
exit 0
```