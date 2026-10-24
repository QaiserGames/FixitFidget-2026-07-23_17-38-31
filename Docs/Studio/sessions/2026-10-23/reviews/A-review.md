# Review of builder A (aim help), 23 Oct 2026

Reviewer: independent, has not seen it built. Diff: `git -C /root/wt23/A diff` against 76df1b4 (= Mac 8033112).
Files: `Assets/Scripts/AimAssist.cs`, `Assets/Scripts/PlayerInteractor.cs` (aim targets only), `Assets/Scripts/GameSettings.cs`,
`Assets/Scripts/Diagnostics/AimHelpCheck.cs`. The editor launcher (`Assets/Editor/Playtest3Session2Steps.cs`) is unchanged and
still fits (same PendingKey, same menu).

## How I checked

- Read all four files whole, and their neighbours: CafeViewMode, BeverageLook (the close-up's caller of `Steer`, the view his
  note is about), PadInput, PlayerCarry, BeverageStation/Control/Slot/CupSupply, CupStack, PadCursor, ItemInspector's
  aim-help part, PlayLab, DispenserFrameCheck.Face, the installer (BeverageStationSetup), and every caller of what A changed.
- `csparse.py`: all four files clean. I then checked every identifier, member and signature the new code uses against the repo.
- I built my own numerical model of the drinks close-up and first person outside Unity, before reading A's report. It uses the
  installer's own hit boxes (six places 15.1 x 33.5 x 20 cm, six paddles 9.3 x 9.2 x 3.8 cm, the stack, the basin; 0.158 m
  apart) and the straight-on camera (1.65 m, 10 cm behind the stand point, tilted onto the face's middle, the look's ±52° /
  -28..34° limits). It includes the stick chain (the Input System's 0.125..0.925 dead zone, PadInput's 0.2, the 1.6 curve),
  the base `Steer` and A's `Steer` line for line, and the lab's own thumbs. The cup prefab isn't in the copy: I assumed an
  8 x 11.2 x 8 cm box on the cup point. Scripts: `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/model/`.

## Findings (worst first)

No Blockers. Nothing fails to compile, nothing breaks a caller, and nothing changes what E or the crosshair acts on. The
fix works on the stroke of his note: under 7 Oct, twenty gentle nudges (stick 0.45 for 0.25 s) from the placed cup toward
the paddle never left the cup; now five nudges land on the paddle. Everything below is Minor.

[Minor] Assets/Scripts/AimAssist.cs:401-405 and 473-487 — "Nothing slows the view off the target it is leaving" (header line
33, report §1 and §2) is not true in the exact case of his note. Only the single target nearest the crosshair counts as
"left" when a push begins. A cup standing in a slot sits inside its own nozzle area's hit box, so off the placed cup the
nozzle area isn't "left". It goes on slowing the view up past its own middle, to its far edge plus the margin. — Model, a
gentle push (stick 0.45) from the placed cup up to Espresso's paddle:
- at Light, the nozzle area slows the view on 110 frames, down to 0.85 of the stick's speed (0.91 on average over the first
  13°);
- at Strong, on 126 frames, down to 0.70 (0.84 on average).

The lab can't see it: its time bar allows 1.3x + 0.25 s, and A's own harness pushed off the nozzle area's middle, not the
cup's. — Smallest fix: when a push begins, mark every target within its friction margin as left (a fixed array, no
allocation), and skip each one in `NearestNotLeft` while its middle is behind the push:
```csharp
// State (Reset and StepTo already clear hasLeaving, which gates this)
internal readonly Target[] leavingAll = new Target[4];
internal int leavingCount;
// Steer, where a push begins (in place of the single 'on' test)
state.leavingCount = 0;
for (int i = 0; i < count && state.leavingCount < state.leavingAll.Length; i++)
{
    float a = AngleTo(eye, view, targets[i], out float s);
    if (a <= s + tune.frictionMargin) state.leavingAll[state.leavingCount++] = targets[i];
}
state.hasLeaving = state.leavingCount > 0;
if (state.hasLeaving) state.leaving = targets[Nearest(eye, view, targets, out _, out _)];   // Leaving (checks) unchanged
// NearestNotLeft: in place of `if (state.hasLeaving && Same(targets[i], state.leaving) && ...) continue;`
bool left = false;
if (state.hasLeaving)
    for (int k = 0; k < state.leavingCount && !left; k++) left = Same(targets[i], state.leavingAll[k]);
if (left && Vector2.Dot(Offset(view, Angles(eye, targets[i].point)), way) < LeaveSlack) continue;
```
In the model this cuts the frames slowed by the cup's nozzle area from 110 to 43 (Light) and from 126 to 47 (Strong). The
average speed over the first 13° goes to 0.99 at both strengths. Her small nudges and the "just past" settle still pass at
15 fps.

[Minor] Assets/Scripts/Diagnostics/AimHelpCheck.cs:869 (also 847, 784) — The pushes' time limits assume pad look speed 1. The
lab plays under the player's own Settings, and the slider goes down to 0.5. — Model at pad look x0.5: `JustPast` needs 8.55° of
travel at stick 0.45, about 3.2°/s, but stops at 2 s still on the paddle (2.73° from its middle, inside its 3.05° edge).
There is no overshoot, so nothing settles back, and the check FAILS though the help is fine, at 15, 20 and 60 fps. At x1 and
x2 everything passes. `LetGoShort` at 9° uses 2.8 s of its 3 s at x0.5. A false FAIL on his Mac would send the integrator
chasing a bug that isn't there. — Smallest fix:
`float until = Time.realtimeSinceStartup + 2f / Mathf.Max(.25f, GameSettings.PadLookScale);` in `JustPast` and `SmallNudge`,
and `3f / ...` in `LetGoShort`.

[Minor] Assets/Scripts/GameSettings.cs:202 / AimAssist.cs:181 — Every machine, hers included, drops from the 7 Oct help to
Light. At the crowded machine, friction at Light is 0.85 (0.7 at Strong, 0.4 under 7 Oct), and the pull is a quarter of the
7 Oct rate. Her case is carried by the settle, which is the same at both strengths: in the model every small-nudge and
just-past case passes at Light. But Strong can only be reached through the phone's row, and that edit is outside this diff
(RecapPhone belongs to J). — No code fix. The integrator must land the Settings row with this change, or Light is everyone's
only "on". "Her default becomes Light for playtest 8" belongs in *Decisions his to overrule*; the report only asks it as
question 2.

[Minor] Assets/Scripts/Diagnostics/AimHelpCheck.cs:683-685 and 747-756 — The spec's headline count, "frames the view moved
against the stick", can't fail on the 7 Oct help either. In the model the old help scores 0 on both legs and 0 in 37,000 random
frames. Its pull could lean at most 0.29 of itself against the push, and the slowed turn was always at least 1.2 times the
pull. So the check is sound, but it proves nothing about his complaint. What he felt shows up as bend, slowing and gliding
back. The bend (the angle between the view's motion and the stick's own way) reached 46.8° under 7 Oct; it is 3.1° at Light
and 7.1° at Strong. The checks that do tell old from new are already there and pass in the model: the leg times (7 Oct 2.47 s
against a 1.71 s bar), let go short at 9° (7 Oct: 13 frames back) and the full push (7 Oct 43°/s against 95). — Smallest fix:
count bend in `Watch` and add it to the same check:
```csharp
// Push: public int bent;   in Watch, after the 'against' line:
if (alone.sqrMagnitude > 1e-8f && moved.sqrMagnitude > 1e-6f && Vector2.Angle(moved, alone) > 15f) result.bent++;
// Leg's second Check: ... && helped.bent == 0, and say it in the message
```

[Minor] Assets/Scripts/AimAssist.cs:510-534 — After one long push that is let go part-way, the settle can still glide back a
few degrees. The "overshoot" allowance is the bench's rule (PadCursor.Nearest): a target behind by less than half the push.
— Model, a single gentle push from the cup stack diagonally to Espresso's paddle, let go on each frame: 69 of 237 release
points glide back 3 to 10°, onto Tea's nozzle area or Hot chocolate's paddle (7 Oct: 401 of 801). On the stroke of his
note (the placed cup up to the paddle) it is 1 of 141, a 0.16° band 10.4° above the cup (7 Oct: 160 of 226). Nudge by nudge
it hardly happens: on the same diagonal, half-way nudges of 0.25 s land in 6 to 8 nudges, at most 1 of 20 settling back
(7 Oct: stuck, 20 of 20). A lists the rule as decision 5. — No change needed now. Smallest action: tell him in the
decision what it still does ("let go part-way through one long push and it may slide back onto what you just crossed"). If
he still feels pulled in playtest 8, the next step is no back-settle where targets crowd:
`if (behind && (Crowd(eye, targets, i) < 1f || off.sqrMagnitude > .25f * pushed * pushed)) continue;`. That costs the
"just past the paddle" case, so it is his call.

[Minor] Assets/Scripts/PlayerInteractor.cs:1116-1146 — `SeenHalfWidth` changes the size of every target that isn't a person,
not just the drinks machine. That covers first person by day and at night (CafeViewMode asks AimTargets at night too): the
bench, the tray, the shelves, the night's things. A flat thing seen at an angle used to be a 3 cm floor; now it's its real
width as seen (a 40 x 30 cm tray at 1.5 m goes from about 1.2° to about 3°). Its settle and friction zones grow with it. A
lists this as decision 6, but the lab only measures the dispenser and customers (whose size is unchanged), so nothing on his
Mac checks the first-person bench or counter after it. — More correct, and no caller breaks. Smallest action: the integrator
tries one first-person nudge at the bench. If he wants the minimum instead:
`IsDrinksThing(it) ? SeenHalfWidth(b, eye) : Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z))`.

[Minor] Assets/Scripts/PlayerInteractor.cs:1092-1098 — New garbage in the help's per-frame path. While Ace holds anything,
`Carried` calls `PlayerCarry.Contains`, which is `hands.Exists(h => h.item == item)`, a capturing lambda: a closure and a
delegate per call. That's one call per target that belongs to a job (a cup in a slot, a device on the bench or tray), on every
frame the stick moves. The project counts this kind of thing (PlayerCarry: "no lambda: no garbage a frame"; the 30 Sept
pass). — Smallest fix, inside A's file:
```csharp
for (int h = 0; h < carry.Count; h++) if (carry.GetItem(h) == job) return true;
return false;
```

### Checked and fine

- **Compile.** Every member the new code uses exists with that signature:
  - `DispenserFrameCheck.Face.Of` and its `List<(string name, Interactable thing)>`, whose tuple names match `Espresso(...)`;
  - `BeverageCupSupply : Interactable`, passed as `from`;
  - `BeverageLook.Aim`, `.stickSpeed` and `.StepAimTo`;
  - CafeViewMode's private `yaw`, `pitch`, `padLookYawSpeed` and `invertPadLookY`, read by reflection (all are fields);
  - `PlayerInteractor.EnterStation` and `CurrentStation`;
  - `PlayerCarry.GetHandItem`, `Summary`, `Consume` and `UseAutomaticHand`;
  - `ShopInventory.ReturnCup` and `DrinkDefinition.drinkName`.

  The readonly struct `Tuning` is legal. There are no duplicate locals in enclosing scopes: the two `off`s in `Steer`, and the
  `i`, `level` and `r` loops, are siblings, not nested. The parameter `pad` in `Friction(Interactable pad, ...)` hides
  PlayLab's `Gamepad pad` field; that's legal, and `PadHold` still uses the field. There is no `GetInstanceID` and no
  `FindObjectsSortMode`. `DispenserFrameCheck` is runtime code outside `#if UNITY_EDITOR`, so the runtime lab may use it.
- **Never against the stick.** It holds by construction: friction only scales the turn (0.4 to 1), and the pull lies within
  40° of it. There were 0 such frames in 37,000 random ones across 15 to 60 fps, both strengths, including at the look's
  limits.
- **A full push is free.** Full deflection is 1.0 after both dead zones, so `Intent` = 0 exactly. The view turns at 95.0°/s
  with help and without, at 60 and at 15 fps.
- **Her small nudge.** It settles from below, left and right at both strengths, in 0.23 s at 60 fps and 0.33 s at 15 fps,
  inside the 0.5 s window. "Just past" settles back.
- **Frame rate.** Friction scales the per-frame turn, the pull is clamped by `rate*dt`, and the glide is `1 - exp(-dt/τ)`. The
  results are the same from 15 to 60 fps.
- **NaN and divide by zero.** None found:
  - zero stick returns early, and `turn.normalized` is guarded by the `1e-10` test;
  - zero-size targets and the eye inside a target are handled;
  - a line of sight straight up or down takes `SeenHalfWidth`'s fallback, and the views clamp pitch to 75° and 80°;
  - `Crowd` with one target gives `Clamp01(∞)` = 1.
- **The D-pad step.** `StepTo` and the glide branch are unchanged. `Next` uses screen points, so the new sizes don't matter.
- **Saves and settings.**
  - An old machine with PrefKey 1 and no StrengthKey comes out Light, and the 19 Oct one-time reset turns the help on at
    Light.
  - Labs that only toggle `Enabled` are untouched: StationsCheck and ClumsyBenchLab use only the switch, PadCursor reads only
    `Enabled`, and DispenserFrameCheck's glide doesn't depend on strength.
  - `GameSettings.Reload` now also re-reads AimAssist. That's safe, because both setters write PlayerPrefs at once
    (PauseCheck and Playtest4UISteps call it).
  - `Describe()` now says "aim help Light", and no caller parses that word (Playtest4UISteps looks only for the sticks and
    the morning).
- **PlayerInteractor.** `TargetOf`, `SeenHalfWidth` and `Carried` are reached only from `AimTargets`. Its callers are
  CafeViewMode (first person, day and night), BeverageLook, StepAim and the lab. FindBest, FindStationTarget, the night's pick
  and E are untouched. Held things were never found anyway, since TryPickUp turns their colliders off, so `Carried` changes
  nothing today, as A says.
- **The lab.**
  - It puts back both the switch and the strength, in Restore and in OnDisable.
  - Replacing the 7 Oct friction check is justified: it asserted the very drag he complained of (a firm push off the pad's
    middle, slowed). The replacement still proves friction exists when approaching a pad: 0.88x at Light and 0.76x at Strong
    in the model, against bars of 0.95 and 0.85.
  - No other existing check is weakened; the Settles and the D-pad check now simply run at Light.
  - At pad look x1 the time bars hold at 15 to 20 fps in the model: the legs have 0.3 s of margin or more, and the settle
    windows 0.17 s.

## Phase 2: the builder's report, checked

- **What locked him: confirmed.** The cup in his hand was never a target: `PlayerCarry.TryPickUp` (line 337) switches off
  every collider, the overlap queries find only enabled colliders, and `BeverageSlot.Cup` forgets a carried cup. Under 7 Oct
  he was held by three things:
  - the settle gliding back to whatever was nearest, behind or not;
  - friction at 0.4 at any push;
  - the sideways pull.

  One framing quibble: the report says "neither guess as written", but the spec's second guess (the placed cup pulling the
  crosshair back down) was right in substance; the stack was the extra.
- **Before and after numbers: re-derived in my own model, all match.**
  - Before: twenty 0.25 s nudges each end where they began, 13.4° / 18.6° / 35.6° from the goal (mine 13.5° / 18.7° /
    35.7°). The full push across the nozzles was 42.3°/s (mine 43.1°/s). The pull worked up to 107° off the push (acos -0.3)
    at 58% of the slowed speed (22 / (95 x 0.4)). Stack to Espresso's place took 2.47 s, against 1.10 s with no help (mine
    2.47 s against 1.12 s). The paddle counted 1.9° and the nozzle area 4.6° (mine 1.91° / 4.57°).
  - After: 3 / 5 / 4 nudges (mine the same). Stack to place takes 1.35 s at Light and 1.43 s at Strong (mine the same). A full
    push turns at 95°/s. At 15 fps, 1.40 / 1.47 s against a bar of about 1.7 s.
  - A's harness, still in the session scratchpad, agrees with mine to within 0.02° on every size.
- [Minor] /root/review23/reports/A-builder-report.md §1-2 (and AimAssist.cs:33) — Two claims are overstated: "never slows
  the view off the target it is leaving" and "pushing off a target's middle runs at the no-help speed". — They hold off the
  nozzle area's middle, which is what A's harness measured ("gentle push up off Espresso's place"). They don't hold off the
  placed cup's middle, which is his case: there the cup's own nozzle area slows 110 frames at Light. — Fix: do the first
  finding; otherwise reword both lines to "off the one target nearest the crosshair". A smaller one: "never glides off a
  target the crosshair is already on" is true for gliding back, but it can glide forward off the cup onto its nozzle
  area's middle (harmless).
- **The check count, 53: right.** The base lab had 30 checks at most, and 26 ran on 20 Oct (four bench cases left out).
  The new lab drops 1 (the 7 Oct friction check) and adds 3 (friction), 1 (the full push in first person), 23 (the
  drinks section) and 1 (back in first person). That is 57 at most, and 53 with the same four left out.
- **The RecapPhone row edit against J's file: compiles and works.** I applied (a), (b) and (c) to a copy of
  `/root/wt23/J/Assets/Scripts/RecapPhone.cs` at
  `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/review-A-recap/RecapPhone.cs`. It parses clean.
  - J's lines 2433-2434 are the `"aimhelp"` SwitchRow.
  - J's `ChoiceRow(card, cy, label, detail, string[] choices, string current, string key, Action<string> choose)` matches
    the call.
  - `using System;` is there, `MorningOf` ends at 2474, and line 737 holds the key list.
  - No member is named `AimLevels`, `AimHelpWords` or `AimLevelOf`, and the static initialisers come in the right order, as
    `MorningWords` does.
  - The pills' keys become `aimhelp:Light|Strong|Off`, and no lab presses `"aimhelp"` (PauseCheck's route runs from
    `"vibration"` to the morning, below this row).
  - J's rows-arriving cascade caps the last row's start at 0.10 s (`RowLastStart`), so the taller Controls card keeps
    PauseCheck's 0.25 s bar.
  - It needs A's `AimAssist.cs` and `GameSettings.cs` first, as the report says.
- **The other claims hold.**
  - D's BeverageStation only reads `player.Focused` and adds no colliders or targets.
  - The bench's pointer applies its 0.55 friction (`ItemInspector.PadFriction`) only over the hovered step target (line
    1748), never mid-scrub (lines 1724-1727), and has no pull. The fight isn't there; I agree.
  - No editor check pins the old constants: CinematicsWave2Steps uses only `AimAssist.Angles`.

**Verdict: ready after these fixes.** Do the first two before it goes to his Mac: count every start target as left, and scale
the lab's time limits by pad look speed. Land J's Settings row with it. The rest are small improvements or his decisions.
