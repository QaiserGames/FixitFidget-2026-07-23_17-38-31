# cine — intro v2, the drinks close-up, the night zoom (14 Oct, wave 2)

Worktree `/root/cafe/build/intro`, branch `b-intro`, on `431f081`. Every changed `.cs` passes `cs_syntax_check.py`. Nothing compiled (no Unity here); APIs were checked against existing uses in the repo.

## 1. What changed

| File | | What |
|---|---|---|
| `Assets/Scripts/CafeViewMode.cs` | changed | Night zoom: `nightZoomMin` (new field, 8 m, the scene takes the default) while following Ace outside; turns split into an angle part and a distance part (orbit/tilt cancel only the angle, zoom only the distance); limits never applied in one frame (`KeepInLimits`: a distance out of range eases back, smoothstep, ≤ 35 m/s); zoom input clamps to the range widened to include where it is (no snap); leaving a house or the bins keeps a zoom chosen inside; R3 eases (0.5 s angle) by day too. `OrbitTo` stays a hard set. New reads: `NightZoomMin`, `AngleEasing`, `DistanceEasing`, `LimitSpeed` (35). |
| `Assets/Scripts/Diagnostics/NightZoomCheck.cs` | new (+ .meta) | The lab (§4.3.4), every frame traced to `trace.csv`. |
| `Assets/Editor/ControllerChecks.cs` | changed | R3 eases: waits up to 1.5 s for home, and no frame over 35 m/s. |
| `Assets/Editor/CinematicsWave2Steps.cs` | new (+ .meta) | `Fixit Fidget › Cinematics › Drinks close-up - straight on (scene)`; the shared straight-on rule and checks; `Fixit Fidget › Night › Night zoom - from the bins to Grace's house (lab)`. |
| `Assets/Editor/Playtest4BenchSteps.cs` | changed | Bench 1's rule is now straight on (the grid search, "opens on the cup stack" and "far place stays" checks removed); `Shoot` made internal for reuse. |
| `Assets/Scripts/Diagnostics/DispenserFrameCheck.cs` | changed | The four new close-up checks (eye ±5 cm, heading 1°, roll < 0.05°; row slant ≤ 0.5°; smallest paddle ≥ 96 px; turned onto the stack: stack + Coffee + Tea in frame, crosshair on it); whole-face check kept; photos 1 (as it opens), 2 (blend at ~25/50/75% from home and from a 45° orbit), 3 (overhead at the stand point before E). New statics `RowSlant`, `ScreenPoint`, `NearStack`. |
| `Assets/Editor/BeverageStationSetup.cs` | changed | The 30 Sept installer places the camera by the same rule, so a re-install can't bring back another pose. |
| `Assets/Scripts/DayIntro.cs` | changed | Intro v2 (below); `PatronSpawner.SeatOpeningGuestsNow()` one line before the spawner is held; `FilmingInsideCafe` for Audio's hook. |
| `Assets/Scripts/Diagnostics/DayIntroCheck.cs` | changed | The lab per §4.1.9. |
| `Assets/Scripts/StationInteractable.cs` | changed | UI/UX §5.7: "Put it back" at the tray a device was lifted from; the tray not offered for the job the Day 1 card says to carry to the bench. |

**Intro v2 in `DayIntro.cs`.** Four slots: the street waking (1.8 s) → the café opening up (1.4) → someone or the day's own (1.4) → the open (1.4); controls back ≈ 6.8 s. 14 buildable shots: `street.row`, `street.storefront`, `street.across`, `bench.ready`, `counter.ready`, `cafe.lights`, `drinks.pour` (reframed straight on at the dispenser), `neighbour.front`, `neighbours.row`, `car.pass`, `patio.flower`, `house.grace` (reframed: her front from across the street, never her window), `street.front`, `street.sign`. Stubs that never frame: `regular.walkup`, `queue.door`, `police.car`, `rain.window`. Cut: both grinder shots. Parked (compiled out): `bench.screw`, `bench.wake`. Day 1 fixed: `street.row|bench.ready|drinks.pour|street.front`. A pure `Plan(day, tie, framable, history, rng, notes)`: weighted draw per slot (yesterday's shot weight 0 in slots 0–2, two mornings ago ½; slot 3 storefront 2:1 sign), never an order from the last six mornings (8 draws, then slot 0 swapped), the tie in slot 2. `TodaysTie(day)`: today's regular (featured ≤ 0.25 → story visit ≤ 0.25 → morning visitor → the officer's word), else a rush phase starting ≤ 0.2. Grace only when tied and in none of the last five mornings, never Day 1, so with today's data **Day 2 shows her house and Day 3 doesn't** (the old `TiedShotFor`, `DayIntro.cs:955-973`, is gone). History in PlayerPrefs `FixitFidget.DayIntro.History` (`day,grace,order;…`, last 8; entries for today or later dropped, so a fresh game isn't blocked by another save); the old `LastOrder` key read once. A shot whose moving subject isn't there at its cut redraws its slot; an empty slot stretches the rest to 5.3 s (≤ 2.4 s each); fewer than 2 shots → Short. J-cut: a shot's `LeadCue` plays 0.15 s before its picture. The café's lights and the bench lamp are held and given back; no gameplay object is copied or moved. Cues by Audio's names: `intro.sting` (under the title), `intro.sign`, `day.open`, `intro.lamp`, `drink.pour`; new `intro.cup`, `intro.lights`, `intro.steps`, `intro.car` (silent until Audio adds them).

**Commits:** `6000772` night zoom + drinks; `b6ceeaa` installer; `dd6d583` intro v2 + lab + pickup prompt; `0223837` lab fixes; `462aebf` metas.

## 2. The Producer's Unity steps, in order

Merge the **npc** branch before (or with) this one: `DayIntro.HoldTheWorld` calls `PatronSpawner.SeatOpeningGuestsNow()` (contract §7).

**Unity A — the night zoom** (after the compile; café scene open):
1. `Fixit Fidget › Night › Night zoom - from the bins to Grace's house (lab)`. Game view in front, hands off ~2 min; Play mode ends by itself. Prints `[Night zoom check] All N checks passed`; report, photos 1–6 and `trace.csv` in `Logs/Night/night-zoom-<time>/`. Look at photo 2 (walking away at 8 m) and the trace's `speed` column (≤ 35).
2. Play the scene, then `Fixit Fidget › Checks › Controller (Play Mode) › Run controller check` (Game view focused). `[Controller check] PASS`, including the two new R3 lines.

**Unity C — the drinks** (Edit mode, café scene open **and saved**):
3. `Fixit Fidget › Cinematics › Drinks close-up - straight on (scene)`. Prints `[Cinematics] Drinks close-up - straight on: done (N check(s) passed)`, **saves the scene**, photos `Logs/Cinematics/drinks-close-up-<time>/1-before.png, 2-after.png, 3-after-turned-onto-the-cup-stack.png`; report `Logs/Cinematics/drinks-close-up-last.txt` (the camera goes from local (0.325, 1.10, −1.52) to about (0, 0.657, −1.05), tilt ≈ 23.5°). One undo group; safe to run twice ("NOTHING TO CHANGE"); any failed check undoes it and saves nothing. (`Playtest 4 › Bench › 1` now applies the same rule: after step 3 it says nothing to change.)
4. `Fixit Fidget › Playtest 4 › Bench › 2 - The dispenser's frame - play check (lab, Play Mode)`.

**Unity C — intro v2:**
5. `Fixit Fidget › Cinematics › Day intro - a check (lab, drives itself)`: about 3 minutes now (the menu still says "a minute": patch 2). Best after Audio's cues and NPC's settled guests. Then `Day intro - play it` by hand, for his eyes and ears.

## 3. The labs and checks, in order, with their pass lines

1. **NightZoomCheck** — `All N checks passed`. Key lines: outside at night the limits read 8–48; from 40 m into the bins view with the right stick held, into 8–22 within 1.5 s; RT 1 s → 8.0 m (±0.05); walking away, 8.0 m (±0.1) to her pavement with no zoom pressed; RT changes nothing; LT at 22 m/s ±10%; R3 home within 1.5 s; into her house from 40 m within 1.5 s (or noted if her door/break-ins aren't there); by day 24–48 round the café's middle and R3 eases; **over every frame: ≤ 35 m/s (T20), no one-frame jump, never out of range > 1.5 s, limits 8–48 / 8–22.**
2. **Controller** — `PASS`, the R3 lines: home within 1.5 s, ≤ 35 m/s.
3. Re-run at their 13 Oct counts: Night 0 (96), Night 1 (77/80), Night 2 (115), Grace at home (133/24/31) and her window (49), the Night tour, Bark. Expected: the Night tour's and Bark's "nearest" night photos (and AceTurn's close photos) are now at 8 m, not 24.
4. **Drinks step** (step 3 above) — `done`, every check PASS.
5. **DispenserFrameCheck** — `All N checks passed`; photo `1-close-up-as-it-opens.jpg` level, the report printing the row's slant (≈ 0°) and the smallest paddle (≈ 111 px).
6. Stations (95), Day 1 guide (74), Controller, Pause: at their counts.
7. **DayIntroCheck** — `All N checks passed`. Key lines: Day 1 plays `street.row|bench.ready|drinks.pour|street.front`; Day 2 shows her front in slot 2; Day 3 shows no Grace; consecutive mornings differ in ≥ 2 slots; each shot's (a) nearest distance and (b)–(e) at its photo moments; the picker's thirty mornings (Day 1 canonical, no repeat within six, Grace on Days 2 and 9 only, every untied shot at least once, every shot in its own slot); the café's lights back; 6–8 s; the bell once; the skip ≤ 1 s; Short 2–3 s; Off. Notes (not failures): ties not built (`regular.walkup`, `queue.door`, `police.car`, `rain.window`); a passer-by shot that found nobody twice; each shot's worst frame.

## 4. Deviations from the spec, and why

1. **The limits' pull (§4.3.2 rule 2)** uses rule 4's eased distance part (smoothstep over max(0.5 s, 1.5·gap/35 m/s); ≤ 35 m/s) instead of the stepped `4·|off|` pull with its 140 m/s² ramp. Why: rule 8 (closed form from elapsed time), and it arrives in finite time: the `4·|off|` tail needs ≈ 1.45 s to come within 10 cm and ≈ 2.6 s within 1 mm, which brushes the lab's 1.5 s range bar. One law for turns, R3 and pulls.
2. **T20's "0 one-frame jumps > 1 m"** is read as "no frame moves more than 1 m *and* faster than the 35 m/s limit". At 15 fps an ease at 35 m/s moves 2.3 m a frame by design (today's 0.8 s house turn already moves 2.5 m a frame). A literal 1 m-per-frame cap would make every bins/house turn ≥ 2 s at his frame rate. The lab's speed bar is T20's 35 m/s, not the spec's 40. **His to overrule.**
3. **The caught and rain ties are never detected** (call 12; the scope freeze). With the spec's priority caught > regular, a detected but unbuildable caught tie would pre-empt Grace's Day 2 beat after a caught Night 1 and drop to B-roll. The stubs remain.
4. **The sting stays under the title** (Audio's table), not also "on frame 0" of `street.row` (§4.1.3): one sting a morning.
5. **The parked bench shots are compiled out** (`#if DAYINTRO_BENCH_CLOSEUPS`). They read `BenchStage.Mat/Tray/Caddy/ToolSlot`, which the bench builder is replacing (the tray is retired by decision 6); compiled, they risked breaking the build. `bench.ready` reads the lamp and `CK_RepairBench` only, for the same reason.
6. **`car.pass`** takes the car's direction from its lane (lane 5 westbound, lane 4 eastbound, §4.1.3), not a velocity read: StreetLife stores velocity only for walkers. Birds are excluded (StreetLife counts them as non-walkers).
7. **"The café's lights"** are `CafeDaylight.warmLights` inside `CafeDaylight.CafeInside` (both already public): Environment needn't add an accessor.
8. **(c) occlusion** isn't tested for shots whose subject is the frame's aim (the three street wides, `bench.ready`, `counter.ready`, `cafe.lights`); the report says so per shot.
9. **The dispenser lab's mid-blend photos are timed from the press**, not from `CinemachineBrain.ActiveBlend` (no use of it anywhere in the repo; I avoided an unverified API). Each photo prints its real fraction.
10. **The night zoom lab** proves "into a view from 40 m with the stick held" at the bins first (step A, the same `EnterHouseView` path), then at Grace's house when her door is there. The walk follows the café's navmesh; if Ace sticks for 1.5 s it nudges him 1.5 m (noted, not failed: the camera is what's measured).
11. **The pickup prompt's two reads are by name** (reflection, as `DayIntro` already reads `GameSettings.DayIntro`): `PlayerCarry.LiftedFrom` (scale's file) and `DayOneGuideUI.CarryingToBench` (uiux's) aren't in the contracts. Missing members read as nothing, so the prompt stays as today.
12. **The drinks step saves the scene** (the builders' brief), unlike the Playtest 4 steps; it refuses on an unsaved scene so the save holds only its change.

Spec vs code: none wrong. One inconsistency: §4.1.7 counts "the word visitor" (arrives at 0.3) as today's regular, while Days §8 says ≤ 0.25. I followed Cinematics; it only ties the unbuilt `regular.walkup`, so nothing shows today.

## 5. Patches for other owners

**Patch 1 — imports, `Assets/Scripts/CafeSoundscape.cs`** (Cinematics §4.1.10: during the intro the street bed follows the intro camera's place, not Ace's):
```
OLD:        bool inside = view != null && view.AceInsideCafe;
NEW:        // The day intro's B-roll (Cinematics, 14 Oct): while its shots play, inside/outside follows the intro's camera.
            bool inside = DayIntro.FilmingInsideCafe ?? (view != null && view.AceInsideCafe);
```
**Patch 2 — nobody's, `Assets/Editor/DayIntroMenu.cs`** (the check now plays about fifteen mornings):
```
OLD: "the mouse, keyboard and pad alone until it reports (about a minute); Play mode ends by itself. Your playtest save is not used.");
NEW: "the mouse, keyboard and pad alone until it reports (about three minutes); Play mode ends by itself. Your playtest save is not used.");
```
**Request — imports/Audio:** bank entries (or placeholders) for `intro.cup`, `intro.lights`, `intro.steps`, `intro.car` (spec §4.1.10; not in Audio's §3 table). Until then they are silent but counted.

**For scale and uiux (names my code reads):** `PlayerCarry.LiftedFrom` — a property, field or method (no arguments, or the `JobBase` in hand) giving the `DropSpot` (or a component on it) the device in hand was lifted from. `DayOneGuideUI.CarryingToBench` — static, the `JobBase` the card says to carry. Other names compile but do nothing.

## 6. Risks

**Compile:**
1. `PatronSpawner.SeatOpeningGuestsNow()` exists only with the npc branch (contract §7).
2. `public virtual Tie Tie => Tie.None;` and `Framing Framing => Framing.Mid` lean on C#'s same-name rule (valid C#). If Unity's compiler objects, rename the properties `TieKind` and `FramingKind`.
3. `StreetLife.Actor.isWalker` is `internal`. That's fine in one assembly; it breaks if StreetLife moves into an asmdef.
4. `DispenserFrameCheck.Face`, `RowSlant`, `NearStack` and `Playtest4BenchSteps.Shoot` (now internal) are shared by three files. They were changed together.

**At 15 fps:**
- `car.pass`: a car stopped by a signal lands off centre at the photo moment, failing (b). That is a real flaw in the shot.
- The DayIntroCheck's photos can land a frame late.
- The night lab's walk may need nudges.
- The dispenser's blend photos land within a frame of each mark (about 9% of the blend).
- The zoom itself is frame-rate independent: closed form, with the step capped at 0.1 s.
- If the free Day 4 morning drops a passer-by shot at its cut, its slot redraws, so no time is lost. An explicit storyboard order instead stretches to 5.3 s, which puts it at about 6.05 s: just inside the 6–8 s bar.

## 7. Open items, in order

1. Audio's four new intro cues (request above), and patch 1, so the street is heard outside.
2. Art Direction's eye on the storyboard photos (14 shots). The spec's numbers become the lab's (a)–(e) lines.
3. The built-later shots: `regular.walkup` (NPC cameo), `queue.door` (NPC + Systems + Director), `police.car` and `rain.window` (parked). Also `bench.screw` and `bench.wake`, which need re-aiming at the new mat before their `#if` comes off.
4. Ask him which drinks picture felt weird (the spec's Q1): show him the step's `1-before`/`2-after` and the lab's photos 1–3 at playtest 6.
5. The Bible's §2 camera list (seven cameras, plus the intro's at run time): the Producer's edit.
