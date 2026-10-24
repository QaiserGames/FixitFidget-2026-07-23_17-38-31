# Builder A: aim help that helps without fighting. Report (23 Oct 2026)

(As handed back by builder A, second run on Opus. Copy: /root/wt23/A, uncommitted. csparse clean on all four files.)

Checked outside Unity: all four files compile under Mono `mcs` (C# 7.2, with `??=` rewritten for the scratch build only) against stubs shaped from the repo's declarations: 0 errors, 0 warnings. The real `AimAssist.Steer` and `PlayerInteractor`'s sizing ran on the dispenser's hit boxes (`BeverageStationSetup`) and the close-up camera, with working Unity maths; the 20 Oct `AimAssist` from git ran the same way for the "before" numbers. Re-run: `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/A-aim/tc/run.sh`.

## 1. What I did
- `Assets/Scripts/AimAssist.cs`: fades as the stick is pushed harder; pulls only toward targets ahead of the push (±40°); never slows the view off the target it is leaving, and never settles back onto it; half strength where targets crowd; levels Off / Light / Strong. New members: `AimHelpLevel`, `Strength`, `Level`, `StrengthKey`, `Reload`, `Tuning`, `Intent`, `Crowd`. `State` reports what it did each frame, for the lab. Every public member kept; `Steer`'s signature unchanged.
- `Assets/Scripts/PlayerInteractor.cs` (aim-target code only, +54/−7): `AimTargets` never returns anything Ace holds (`Carried`); each target names its `Interactable`; a target's size is its width as the eye sees it (`SeenHalfWidth`).
- `Assets/Scripts/GameSettings.cs`: adds `AimLevel` (Off / Light / Strong). `Reload` re-reads the aim help too. `Describe` says the level.
- `Assets/Scripts/Diagnostics/AimHelpCheck.cs`: a new drinks close-up section, the first-person friction check rewritten (1 check becomes 3), a full-push check, and the player's switch and strength put back at the end.
- `Assets/Editor/Playtest3Session2Steps.cs` (the launcher): unchanged. Same menu.

## 2. Decisions and reasoning (claims to verify)
- What locked him was neither guess as written. The cup in his hand was never a target: `PlayerCarry.TryPickUp` switches off every collider of what it picks up, and targets are found by colliders; `BeverageSlot.Cup` forgets a carried cup. What held him was the thing he was leaving: the cup stack he took the cup from, then the cup under the nozzle and its nozzle area.
- On the machine's real geometry the 20 Oct code: let go glided back to the nearest target, even one behind the push (twenty gentle nudges from the nozzle area or the cup toward the paddle, or from the stack toward Espresso, each ended where it began: 13.4°, 18.6°, 35.6° away); friction 0.4 at any push (targets 10° apart; a full push across the nozzles turned at 42°/s instead of 95); the pull worked up to 107° off the push, at up to 58% of the slowed speed (a steering thumb bent 31°; stack to Espresso's nozzle 2.47 s vs 1.10 s without help); the paddle counted 1.9° middle-to-edge (its 3 cm minimum; 3.8 cm deep) against the nozzle area's 4.6°.
- After the change (close-up, 60 and 15 fps): the nudges reach the paddle in 3–5, no frame moving back; the steering thumb takes 1.35 s at Light, 1.43 s at Strong, bends ≤ 3°, lands dead centre; a full push turns at 95°/s; pushing off a target's middle runs at the no-help speed.
- Fade curve: whole help up to 35% stick travel past the dead zone; none from 75%; smooth step between.
- "Leaving": the target the push began on gives no friction until its middle is clearly ahead again (0.25°).
- Settle kept whole at both strengths, with two limits: never glides back the way the push came unless the push went just past something (within half the push), the bench's rule from 15 Oct (`PadCursor.Nearest`); never glides off a target the crosshair is already on. Her case: a nudge stopping 2.5° off the paddle's edge lands on it in 0.23 s from below, left and right.
- Crowding: a gap under 6° edge to edge halves friction and pull, rising to whole help at 12°. Every target at the dispenser counts as crowded. Settle and D-pad steps stay whole.
- Levels: Light (new default), Strong (the 7 Oct numbers under the new rules), Off. Light: speed 0.7 over a lone target, pull 11°/s, a 2° fade, a 5° pull cone, the same settle.
- Saved keys: the old switch `FixitFidget.AimHelp` keeps its meaning (on/off); the strength under a new key `FixitFidget.AimHelp.Strength`, missing → Light. Turning the help off keeps the strength. All labs that toggle `Enabled` are unaffected.

## 3. Decisions his to overrule
1. The fade: 35% / 75% of the stick's travel, smooth step between.
2. The pull cone: ±40°.
3. Crowds: under 6° edge to edge means half help; whole at 12°.
4. Light's numbers: 0.7 / 2° / 5° / 11°/s. Strong keeps 0.4 / 3° / 7° / 22°/s. Both settle within 4°.
5. Overshoot recovery narrower: only within half the push.
6. A target's size is now its width as the eye sees it, for every first-person target except customers. Deep boxes seen at an angle grow (the cup stack 3.9° → 6.3°).
7. The bench's pointer help is the same at Light and Strong; only Off turns it off.
8. Lab bars: landing time ≤ 1.3× no-help + 0.25 s; friction onto a crowded pad < 0.95 of no-help at Light, < 0.85 at Strong (real code: 0.88 and 0.76); pushing off a target ≥ 0.97 of no-help; a full push ≥ 0.98 of no-help.
9. Settings row order Light / Strong / Off. The row's wording is placeholder.

## 4. Changes outside my files
**`RecapPhone.cs`, against J's version (/root/wt23/J). No edit made there.**

(a) Replace J's lines 2433–2434 (the `"aimhelp"` `SwitchRow`) with:
```csharp
        // Light, Strong or Off (23 Oct 2026, his note: "helpful, but not so overpowering that it's like fighting me"): Light is the
        // default and what "on" was before; Strong the 7 Oct numbers without the fighting (AimAssist). PLACEHOLDER COPY.
        cy = ChoiceRow(controls, cy, "Aim help", "On a pad: the view slows and settles on things. Strong pulls harder.",
            AimHelpWords, GameSettings.AimLevel.ToString(), "aimhelp", word => GameSettings.AimLevel = AimLevelOf(word));
```
(b) After `MorningOf` (about line 2474), add:
```csharp
    // The aim help's three settings in the row's order (23 Oct 2026), and their words on the pills: Light, Strong, Off.
    static readonly AimHelpLevel[] AimLevels = { AimHelpLevel.Light, AimHelpLevel.Strong, AimHelpLevel.Off };
    static readonly string[] AimHelpWords = Array.ConvertAll(AimLevels, level => level.ToString());

    static AimHelpLevel AimLevelOf(string word)
    {
        int at = Array.IndexOf(AimHelpWords, word);
        return at >= 0 ? AimLevels[at] : AimHelpLevel.Light;
    }
```
(c) Line 737, the `ContentButton` docs: change `"aimhelp"` to `"aimhelp:&lt;Light|Strong|Off&gt;"`.

Notes: no lab presses `"aimhelp"` (PauseCheck's route uses `"vibration"` and the morning's pills). It needs A's `AimAssist.cs` and `GameSettings.cs` merged first. Without it nothing breaks.

**`BeverageStation.cs`, D's version:** no change needed. It only reads `player.Focused`.

**The bench's pointer (spec item 7): no fight found, so no change.** It already got this cure on 15 Oct: `PadCursor.cs:204–218`. Its friction (0.55) applies only over the current step's target under the cursor (`ItemInspector.cs:1748`) and not mid-scrub (:1724). It has no pull.

## 5. Labs on his Mac
- **Fixit Fidget › Playtest › Aim help - play check (lab, drives itself)** — `[Aim help check] All 53 checks passed.` (57 when every bench screw case runs; on 20 Oct four were left out, 26 of 30). The one changed on purpose: the 7 Oct friction check pushed off the pad's middle and wanted it slower; replaced by three checks (onto the pad at Light, onto it at Strong, up off its middle). New photos `01b`, `06`–`12` in `Logs/AimHelp/aim-help-<time>/`.
- Re-run, unchanged: Stations as reach (first person / overhead); Checks › Controller (Play Mode) › Run controller check; Playtest 4 › Bench › 2 - The dispenser's frame; Bench › Bench 8 - The clumsy hand; Playtest 4 › UI › 2 - Settings: the bench's sticks and the morning (edit mode; reads `Describe`, calls `Reload`); the pause and recap labs after J's work and the row edit.

## 6. Risks, and APIs
- `public readonly struct Tuning` (C# 7.2); `object.ReferenceEquals` on `UnityEngine.Object`; a tuple-typed parameter `List<(string name, Interactable thing)>` to match `DispenserFrameCheck.Face`; lab reflection on `CafeViewMode`'s private `pitch`, `padLookYawSpeed`, `invertPadLookY`.
- Not testable here: the real scene's dispenser pose, the cup prefab's collider, the camera's one-frame lag, real frame pacing.
- Timing margins outside Unity: landing at 15 fps Light 1.40 s / Strong 1.47 s vs bar 1.68 s; cup to paddle 1.20 s vs 1.46 s; friction ratios 0.88 / 0.76 vs 0.95 / 0.85.
- A push starting off-centre on a target and crossing its middle: that target stops slowing it at the middle (about 0.85 → 1.0 at Light).
- The lab needs 2 cups in stock (a fresh Day 1 has them); it adds about 40 s.

## Questions for Mansoor
1. Settings: Aim help as Light / Strong / Off, Light by default? Recommend yes.
2. For playtest 8: start on Light? Recommend Light.
