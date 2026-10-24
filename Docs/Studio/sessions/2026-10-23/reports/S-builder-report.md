# Builder S: seats chosen the way people choose them. Report (23 Oct 2026)

(As handed back by builder S, second run on Opus, resumed after a usage cut-off. Copy: /root/wt23/S, uncommitted. csparse clean on every changed file. Nothing compiled or run here; the evidence is a Python model and hand traces. Life 4 and the Waiting space check fail until the two required outside edits in §4 are applied.)

## 1. What I did
- `Assets/Scripts/SeatTags.cs` (new): the `SeatTag` flags; the `SeatTags` component (tags for a seat or a room area); `SeatChoice` (every number, the table of leanings, the favourites, the score). Pure code, testable without Play.
- `Assets/Scripts/WaitingArea.cs`: `Claim` uses `PickBest` (the scored seat) instead of `PickNearest`; same API. `ClaimRepairBar`, `FreeSeats`, `Release`, `HasArrived` unchanged in code. `nearlyAsNear` now read as points. Header and tooltips updated.
- `Assets/Scripts/TableSeat.cs`: adds `Tags` and `Zone` (through SeatTags). Comments otherwise.
- `Assets/Scripts/CustomerBrain.cs`: in `TryTakeWaitingSpot` (1639), `ClaimRepairBar` is asked only when `StaysAtTheBar` is true. Adds `public bool StaysAtTheBar` (1741) and `public WaitingSpot ClaimedSpot` (1744).
- `Assets/Scripts/PatronBrain.cs`: one comment at line 234. No code change.
- `Assets/Scripts/Diagnostics/DayShapeCheck.cs`: the seat-variety part only (records every claimed seat; judges Days 1–3, reports Day 5).
- `Assets/Editor/SeatTagsSteps.cs` (new): Seat tags 0 (check first) and Seat tags 1 (with Undo), through `CafeRedesignShared.Step`.
- `Assets/Editor/SeatChoiceRules.cs` (new): an edit-mode rules check, `Fixit Fidget/Checks/Seat choice rules`.
- `WaitingSpot.cs` unchanged.

## 2. Decisions and reasoning (claims to verify)
- **The score**, in points (one point ≈ 3 m of walking): leanings from the walk-in's personality (`archetypeName`) plus everyone's movement profile (Cheerful: window; Chatty: communal, minds strangers half as much; Sentimental: soft seats and quiet corner; Rushed / Hurried: walk ×3 / ×1.5 and seats by the door; Shy: quiet corner, avoids communal; long repairs (Mechanical / Bureaucratic) +2 for soft seats). Favourites +20. Personal space −3 for a stranger at the next seat (within 1.15 m), −1.5 at the same table (within 1.9 m). The walk −0.3/m along the NavMesh. Repair-bar stool −1.5 for anyone without a bench job. Near-equals within `nearlyAsNear` (0.6 points) shared at random. Routes measured best-first with a straight-line bound minus 0.5 m of slack (same result as measuring every route). No seat names in the score.
- **For F:** tag set `Window`, `Lounge`, `Bar`, `Communal`, `Quiet`, `NearDoor` (flags). A `SeatTags` component on the seat or any parent (nearest up the parents wins); room areas (a `SeatTags` with non-zero `Area` is a box) add tags to every seat whose sit point is inside. `TableSeat.Tags` recomputed on each read. Default for an untagged seat: sit point > 0.6 m above its stand point → `Bar`; `SitStyle.Bench` → `Lounge`; else none (plus areas). A seat placed at runtime: `WaitingSpot.OnEnable` → `WaitingArea.Register`; scored from its next claim. `SeatTags.Set(tags, zone, areaSize)` for code.
- **For P:** a dirty seat is never scored or chosen (`TableSeat.IsAvailable` = `Occupant == null && !IsDirty`, TableSeat.cs:129; `PickBest` skips unavailable spots, WaitingArea.cs:181; `FreeSeats` counts only available seats).
- **Repair customers:** `StaysAtTheBar` true for every regular; for a walk-in true unless soft-seat leaning > 2 + `watches` (Impatient and Rushed have `watches 2`). Model: Day 1 none sit elsewhere; Days 2–3 about 40%; Days 4–5 about 25%. Those who stay use `ClaimRepairBar` exactly as before.
- **Hand-back for a customer seated away from the bar:** nothing tests the seat. `CustomerBrain.JobReady` (615–632); `CustomerInteractable.IsAvailable` (12–16) and `FloorAvailable` (27–30); overhead `PlayerInteractor.FindBest` (550–572) → sphere of `reach` 2.2 m (51, 558) → `FindFloorTargetAmong` (701–748, filter at 719); first person `FindFirstPersonFloorTarget` (658–682) → `Reachable` (693–694); `CustomerInteractable.Interact` 112 → `CompleteJob` (CustomerBrain.cs:2093). Day 1's card: "Take it to {name}. {E} Hand it back." (DayOneGuideUI.cs:636, `Over` at 781).
- **Reassure:** `CanReassure` (645–649) has no seat test; in FloorAvailable (CustomerInteractable.cs:30); Interact 115–118 → `Reassure` (CustomerBrain.cs:2207). Within 2.2 m, so a walk to them. While their device is in Ace's hands, their patience stops at 10% (`HandsOnFloor`, 703–719).
- **Seat tags 1:** adds a `SeatTags` to each of the café's 28 seats by the name prefixes Seats 1, Lived in 2 and Café dressing 3 gave them; lays 6 areas under `30 - cafe redesign/Seat tags 1 - the room's areas` (west window, east window x 5.9–7.7, the front window either side of the door, by the door, the quiet corner). Moves nothing, re-bakes nothing, uses the redesign's Undo list. Undo order: run Art tidy 1 first, then Seat tags 1; undo Seat tags 1 first.
- **Day lab thresholds (Days 1–3):** areas laid (precondition, a Check); ≥ 4 different seats across ≥ 3 zones; no seat > 45% of the sittings; drink customers in ≥ 2 zones (when ≥ 2 of them). Day 5 reported, not judged; a café with fewer than 10 seats in 4 zones reported, not judged. From 3,000 simulated Day 1s: new scorer all-together ≈ 97% (old nearest rule 0%). About 3 runs in 100 can fail by chance.
- **Worked example** (from the till (1.6, 12.5), empty café): nobody in particular → lounge west chair −1.10, then four near-equal seats in two zones; Cheerful+Distracted → window counter N +2.73; Chatty+Social → communal E4 +2.75; Cheerful+Relaxed → banquette N cushion +1.98; Chatty+Hurried → communal E4 +0.38; Officer → window counter N +18.23. Seated in turn: Officer window counter N; Cheerful+Distracted banquette N chair; Chatty+Social communal E4; Cheerful+Relaxed middle cushion; Chatty+Hurried E1. The rules check's section 6 asserts this.

## 3. Decisions his to overrule
1. The tags per zone (repair bar: Bar; banquette cushion: Lounge+Window; banquette chairs: Window; communal: Communal, south stools also NearDoor; bistro: none, south 2-top NearDoor; window counter: Bar+Window; reading corner: Lounge+Window+Quiet; lounge: Lounge).
2. The area bounds (SeatTagsSteps `Areas`).
3. Every number in `SeatChoice`.
4. Favourites: Grace = a soft seat by the window; the Officer = any bar stool, but the repair bar's stools are kept for repairs, so he takes the window counter.
5. Long repairs are the Mechanical and Bureaucratic faults.
6. Regulars always stay at the bar on their own bench jobs.
7. Day 1 repairs never sit elsewhere; from Day 2 about 2 in 5 do.
8. `nearlyAsNear` 0.6 read as points.
9. The lab thresholds and the 10-seat / 4-zone room guard.
10. Patrons use the same scorer through `Claim`. Pairs, laptop guests and settled guests keep their own pickers.

## 4. Outside my files: exact edits (patch files; both pass `git apply --check` against /root/wt23/S)
**Required, or labs fail:** `/root/review23/reports/S-patches/S-required-outside-edits.patch`
- `Assets/Editor/WaitingSpaceChecks.cs`: TheSecondStool (252–253): the old `given == middle` fails under the new rule; replaced by two checks (the claim is a floor seat; `OnTheirWayNear(middle, customer) == 0` through reflection). The drink claim (326–347): its candidate set becomes "within `nearlyAsNear` points of the best `SeatChoice.Score` for a person with no leanings"; also asserts the claim is never a stool. Header and comments updated.
- `Assets/Editor/NpcWave2Steps.cs` (Life 4, ABenchCustomer): line 1018 `if (fresh.GoesToTheBench && fresh.StaysAtTheBar) { found(fresh); yield break; }`; line 1022's Note says why; comment at 1003.

**Recommended:** `/root/review23/reports/S-patches/S-recommended-guide-words.patch`
- `DayOneGuideUI`: add `WalkOverReassureWords(name)` = "{name} is getting impatient. Walk over, {E} Reassure."; WatchImpatience line 868 uses `ReassureWords` only when `c.ClaimedSpot` is a RepairBar stool.

## 5. Labs and menus on his Mac, in this order
1. Reimport. Unity makes three new .meta files.
2. Cafe redesign › Seat tags 0 - Check first (read-only): "CHECKED: ready"; "To tag: 28 seat(s); 0 already tagged; 0 unknown. Areas to lay: 6."
3. Seat tags 1 - Tag the seats and the room (scene, with Undo): All PASS, "DONE"; "28 seats tagged, 6 areas laid. Seats per tag: Window 11, Lounge 7, Bar 6, Communal 8, Quiet 2, NearDoor 4". Save the scene.
4. Checks › Seat choice rules: "[Seat choice rules] All 43 checks passed."
5. Apply the required patch, then Checks › Waiting space reservations: "The café scene: N passed, 0 failed".
6. Days › Lab - Day 1 / 2 / 3 at the counter: "All N checks passed"; Day 1 "Bench jobs: … 0 away".
7. Unaffected, should pass as before: Café life check; Stations as reach; Sit 4; Hands on it; Probe - the bench's hand-over reach; Conversation play check; Day 1 guide check.
8. NPC › Life 4 0 → Life 4 (needs the patch).

## 6. Risks and APIs
- `Resources.FindObjectsOfTypeAll<SeatTags>()` in Edit Mode filtered to `scene.IsValid()` with `enabled && activeInHierarchy`; `SerializedProperty.intValue` on a [Flags] enum field; named value-tuple array elements; `is T x`; `out _`.
- Best-first route search exact only if NavMesh routes ≥ straight line − 0.5 m.
- The Day labs mean nothing until Seat tags 1 is run and the scene saved (the precondition fails loudly).
- The rules check's section 6 uses a copy of the seat plan, with the lounge position estimated from photos.
- Cost per claim: reasons for every free spot (~40) plus a few routes.
- The window counter's stools are Snap To Seat off (Seats 1): its customers wait standing at the east window. Visible and new.
- Callers: `WaitingArea.Claim` — CustomerBrain:1641, PatronBrain:282, NpcLifeChecks:312, WaitingSpaceChecks 248 and 344 (patched). `ClaimRepairBar` — CustomerBrain:1640 (gated), WaitingSpaceChecks 313/320. `FreeSeats` — PatronSpawner:179, LodgerDay:133, CafeLifeRecorder:300, NpcSitLiveCheck:248. `WaitingSpot.Claim` per spot — PatronBrain 232/329/330/351, NpcSeating:1092, StationsCheck:806, NpcBeatChecks:166.

## Questions for him
1. The window counter's stools: make them sit (a small scene step)? Recommend yes.
2. The impatience card's "At the counter" for anyone seated across the room: apply the recommended patch? Recommend yes.
3. Day 2 as the first day repair customers sit away from the bar? Recommend yes.
