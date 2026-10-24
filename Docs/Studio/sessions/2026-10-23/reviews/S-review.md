# Review: builder S, seats chosen the way people choose them (23 Oct 2026)

Reviewer: a separate agent who did not see the build.

**What I read:**
- the rules, his notes, the spec and the CafeLife photos;
- the whole diff in `/root/wt23/S` (base 76df1b4 = Mac 8033112), the three new files and every touched file in full;
- their neighbours, and S's two outside patches.

**How I checked compile.** Unity isn't available here. I grepped every identifier the new code uses. I applied both
patches to copies (`git apply --check` too) and ran `csparse.py` on everything.

**How I checked behaviour.** I wrote an independent Python model of the scorer. It copies `SeatChoice` number for
number, uses the café's 28 seats as `CafeSeatsSteps.MakeSeats` and `CafeLivedInSteps` place them, and runs a simplified
Day 1: the Officer, Grace, Cheerful and Chatty walk-ins, 55 % drinks, one pair of hands, patrons from the door. My
model's walks are straight lines, so its numbers are a second opinion, not ground truth.

Phase 1 below was written before I opened the builder's report. Phase 2's findings are marked "(Phase 2)".

## Findings, worst first

**[Major] `Assets/Scripts/SeatTags.cs:310` (the Officer's favourite), `:290` (Cheerful window +3), `:438`;
`Assets/Scripts/WaitingArea.cs:208` — the scorer now sends people to the window counter, whose three stools nobody can
sit on. The Officer waits standing there on every visit, and so do about half of Day 1's Cheerful drink customers. This
ships badly in playtest 8.**
- *Evidence.*
  - The window counter's stools are `snap: false` (`CafeSeatsSteps.cs:301`). `NpcSeating.CanSit`/`TrySit` refuse
    them (`NpcSeating.cs:261, 366`), so the customer stands beside the stool for the whole wait. Seat tags 1 tags them
    `Bar | Window`.
  - The Officer's favourite is `SeatTag.Bar` at +20 points, about 66 m of walking.
  - Cheerful's window is +3, about 10 m of walking.
  - Recomputing the rules' own quiet Day 1: Cheerful with a Hurried, Social or Distracted manner chooses
    `window counter north`. Those manners are weights 3.0 of 5.2, so 58 % of Cheerful walk-ins.
  - In my Day 1 model (3,000 days):
    - the Officer took a window-counter stool on 3,000 of 3,000 days (the old rule: the lounge's tub chair, 3,000 of
      3,000);
    - 40 % of drink sittings were standing waits there (old rule: 0 %);
    - every day had at least one.
  - The Officer is Day 1's first customer (the guided drink). He comes back from Day 3 for his question
    (`OfficerStory`, `fromDay = 3`), and Day 2 can bring him as a random regular (`regularChance 0.15`).
  - Her playtest 5 note was "people wouldn't sit down on the bar stools"; NPC wave 2 fixed that for the repair bar
    only.
  - No lab catches it: Sit 4 only NOTEs window-counter standers (`NpcSitLiveCheck.cs:487-489`), and the Day labs count
    a stand as a sitting.
- *Smallest fix (S's files).* A seat nobody sits on is overflow only, and a favourite never points at one. The penalty
  reads `SnapToSeat`, so it switches itself off once the stools sit, and seats he places later get the same rule.

  ```csharp
  // SeatChoice (SeatTags.cs)
  /// <summary>A seat nobody sits on (Snap To Seat off: the window counter's stools until they sit) is waited at standing: her
  /// playtest 5 note ("people wouldn't sit down on the bar stools"). As bad as a stranger at the next seat: overflow only.</summary>
  public const float StandingWait = 3f;
  // struct Seat: add  public bool standing;  and the constructor parameter  bool standing = false  (this.standing = standing)
  public static float Reasons(Person who, Seat seat)
  {
      float r = who.Pull(seat.tags);
      if (who.favourite != SeatTag.None && !seat.standing && (seat.tags & who.favourite) == who.favourite) r += Favourite;
      if (seat.repairBar && !who.benchJob) r -= KeptForRepairs;
      if (seat.standing) r -= StandingWait;
      return r;
  }
  // Favourites: the Officer's is the window counter's stools, so it sleeps until they sit (a bare Bar would now put him
  // on a repair-bar stool every day, +20 against the -1.5 kept for repairs):
  (OfficerStory.ProfileId, SeatTag.Bar | SeatTag.Window),
  // WaitingArea.cs:208
  var place = new SeatChoice.Seat(PlaceOf(s), seat != null ? seat.Tags : SeatTag.None, seat != null && seat.RepairBar,
                                  standing: seat != null && !seat.SnapToSeat);
  ```

  In my model this takes standing waits from 40 % of drink sittings to 0 %. The Officer then sits at the west
  banquette or in the lounge. The Day lab's four seat numbers stay at about 95 %.
  
  Update `SeatChoiceRules` §3 (`:207-217`) and §6 (`:481`, `:501`): the Officer has no favourite while the counter's
  stools don't sit. Add the rule "a seat nobody sits on is only taken when nothing near as good is free".
  
  The real follow-up is the NPC lead's, so name it in the report: make the window counter's stools sit, as wave 2 did
  for the repair bar. CafeSeatsSteps' header already says they "could sit unturned with the same fields": Snap To Seat
  on, Max Lift 0.32, Approach Side 0.50. Prove it with Sit 4 and Life 4.

**[Major] `Assets/Scripts/Diagnostics/DayShapeCheck.cs:534-539` (Checks) and `:70-74` (the header's figures) — four
statistical thresholds are judged on one day's ~7 random sittings, so a good build FAILs by chance. The share check is
mostly the repair bar's by-design middle stool. A 3 % flake isn't acceptable here, and I can't confirm it is only 3 %.**
- *Evidence.*
  - Each run's FAIL lands in the lab's last line, and the header asks the integrator to rerun and "read two in a row".
    A Day lab is about nine minutes. Even at the claimed 3 %, about 9 % of three-day sessions carry a false FAIL, and
    the spec's 20 runs a day bring about 0.6 a day (1.8 over the three days). The Days 1-3 bars in the same report then get read through noise.
  - The busiest-seat share (`:521-522`) counts bench jobs at the repair bar. `ClaimRepairBar` gives every bench job the
    middle stool when it's free, by design, so three or four non-overlapping bench jobs break 45 % whatever the scorer
    does.
  - My independent model doesn't reproduce the header's numbers:

    | Sittings a day | Scored seats pass all four | Old rule passes |
    |---|---|---|
    | 12 | 98 % | 59 % |
    | 8 | 85 % | 34 % |
    | 6 | 72 % | 23 % |

    The header says the old rule passes 0 %. The answer turns on how many customers a Day 1 brings and how they
    overlap, which one run can't settle.
- *Smallest fix, exact numbers.*
  - **Per run, Check only what can't be chance.**
    - Keep `Check(areas > 0, …)`.
    - Add `Check(sittings.Count > 0, …)`.
    - NOTE the four numbers with their targets.
  - **Measure the scorer.** Take the share over `sittings.Where(s => !(s.benchJob && !s.awayFromTheBar))`, the
    sittings it chose.
  - **Add four columns at the end of the runs CSV row** (`DaysBars`, `:610`, after pass/fail, so the existing
    `c.Length >= 7` reads still work): distinct seats, zones, the scorer's busiest share, drink zones.
  - **Judge on the median of the day's last 3 runs that carry those columns**, once 3 exist. That's this lab's own
    pattern for Day 3's storm-outs (`:620-621`):
    - median seats ≥ 4;
    - median zones ≥ 3;
    - median scorer-share ≤ 0.50;
    - median drink zones ≥ 2.
    
    A median of 3 fails with probability 3p²(1−p) + p³: 0.26 % at p = 3 %, 6 % at p = 15 %. Use 5 runs if he can
    spare them: 2.7 % at p = 15 %.
  - **Give the deterministic proof in Edit Mode.** When the café scene is open, run `SeatChoiceRules` §6 on its live
    seats instead of the hard-coded fixture:
    - their real `Tags`, `SeatPose` and `StandPoint`;
    - walks from `WaitingArea.WalkingDistance` (by reflection, as the Waiting space check does) from queue place 0;
    - judge each of Day 1's ten sorts' near-equal **set**, not a random pick.

**[Minor] `/root/review23/reports/S-patches/S-recommended-guide-words.patch` (DayOneGuideUI.cs:706, :868) — the
impatience card says "At the counter, E Reassure." on any day, which is true only on a repair-bar stool, and S moves
more people away from the counter — the patch is right — integrate it as required.**
- *Evidence.*
  - S's change seats every drink customer and, on Days 2-3, some bench jobs across the room.
  - The patch keys on `c.ClaimedSpot is TableSeat stool && stool.RepairBar`, and keeps `Day1GuideCheck.cs:377`
    (Grace at the bar).
  - It parses clean and applies clean.
- *Fix.* Integrate it. Optionally add a `DayOneOnboardingChecks` line:
  `Require(DayOneGuideUI.WalkOverReassureWords("Walter").Contains(ControlHints.Interact) && DayOneGuideUI.WalkOverReassureWords("Walter").Length <= 90, …)`.

**[Minor] `Assets/Scripts/WaitingArea.cs:291-304` (with `CustomerBrain.cs:1649-1653`, `:1136-1139`, `:1878`) — a best
seat that can't be reached is re-picked every second, so the customer stands at the counter while reachable seats sit
free.**
- *Evidence.*
  - With no complete route, `WalkingDistance` falls back to straight × 1.5 + 2 m. The Officer's +20, a window +3 or
    the reading corner's 4 still outweigh that.
  - So `CanReach` fails, the seat is released, and the 1 s retry picks it again.
  - `GiveUpOnSpot`'s "try somewhere else" re-picks the seat it just let go.
  - The old rule had this in principle, but the inflation usually demoted the seat. Now reasons keep it on top.
- *Fix.*

  ```csharp
  if (NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
  {
      if (NavMesh.CalculatePath(start.position, to, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete)
      {
          int count = route.GetCornersNonAlloc(corners);
          float length = 0f;
          for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
          return length;
      }
      return float.PositiveInfinity;   // a NavMesh here, and no way there: never the best while any seat can be reached
  }
  return Flat(to - from).magnitude * 1.5f + 2f;   // no NavMesh at all (Edit Mode): the pessimistic straight line, as before
  ```

  `PickBest` stays exact. An infinite walk scores −∞, and an all-unreachable room gives the old random pick.

**[Minor] `Assets/Editor/SeatChoiceRules.cs:207`, `:481` — the Officer is modelled as Relaxed, but in the game he's
Distracted.**
- *Evidence.*
  - `Regular_Officer.asset` has `movementProfile: {fileID: 0}`, so `NpcSocial.AssignProfile` calls
    `library.ForKey("officer")`.
  - FNV-1a("officer") = 3249485689, and 3249485689 mod 5 = 4.
  - `CafeNpcPass2Setup.cs:293-297` orders the library Relaxed, Hurried, Shy, Social, Distracted.
  - Grace's authored Relaxed is right.
  - Today the result is the same seat (+19.73 against +18.23).
- *Fix.* `SeatChoice.Make(manner: "Distracted", regular: OfficerStory.ProfileId)` in both places, with that reason in a
  comment.

**[Minor] `Assets/Scripts/WaitingArea.cs:40-45` — `nearlyAsNear` changed from metres to points, so the scene's 0.6
widened the tie range from 0.6 m to 2 m for most walkers.**
- *Evidence.* It's 0.67 m for the Rushed and 0.44 m for Rushed+Hurried. In my model no claim walks more than 2 m past a
  free seat with the same tags and strangers, so the 27 Sept rule holds. But it's a feel change, and the report lists
  it only as "read as points".
- *Fix.* Put it under *Decisions his to overrule* with the dial: 0.3 points is about 1 m.

**[Minor] `Assets/Scripts/WaitingArea.cs:173`, `SeatTags.cs:247-248` — "Exact" holds only when the route starts within
`StraightSlack` (0.5 m) of the claimer, but `SamplePosition` searches 1.5 m.**
- *Evidence.* Agents on the mesh snap within centimetres, so it's true in play.
- *Fix.* Comment only: "exact for anyone standing on the NavMesh".

**[Minor] `Assets/Scripts/WaitingArea.cs:238-239` — "Sort allocates nothing for it" isn't so.**
- *Evidence.* `List<T>.Sort(Comparison<T>)` in Unity's Mono reference source wraps the delegate in a comparer: one
  small allocation per claim, never per frame.
- *Fix.* Comment: "allocates one comparer, not a closure".

**[Minor] `Assets/Editor/SeatChoiceRules.cs:126-129` — "a busy room still seats them" can't fail: `Best` returns an
index whenever seats exist.**
- *Fix.* Assert that the pick is the nearest of the four, which proves crowding ranks seats rather than refusing them.
  Or drop the check.

**[Minor] `Assets/Editor/SeatTagsSteps.cs:55` — Seats 1's record name is hard-coded.**
- *Fix.* `const string Seats1 = CafeSeatsSteps.Title;`. It's an internal class in the same assembly.

**[Minor] `Assets/Scripts/SeatTags.cs:237` — the comment says counter stools are "0.9-1.0 m apart", but the window
counter's are 1.2 m apart (`CafeSeatsSteps.cs:222`), so they count as one table (1.5), not the next seat (3).**
- *Fix.* Keep `NextToWithin` at 1.15, because 1.25 would catch the bistro's across seat at 1.18 m. Correct the comment.

**[Minor] (Phase 2) `/root/review23/reports/S-builder-report.md` §2 and §5.6 — the simulation claims can't be
checked.**
- *Evidence.*
  - "≈ 97 % (old nearest rule 0 %)" and "about 3 runs in 100" give none of the model's inputs: customers per Day 1,
    overlap, patrons, walks.
  - My model contradicts the old rule's 0 % and the scored seats' 97 % at the header's own seven sittings (table above).
  - §5.6 promises the Day labs "All N checks passed", which by the report's own figure isn't true every run.
  - The same numbers sit in DayShapeCheck's header comment, which must stay true.
- *Fix.* With the median-of-runs change above, replace the header's numbers with what is judged and why. If figures
  are kept, give the model's inputs.

**[Minor] (Phase 2) report §2, "Hand-back… `CompleteJob` (CustomerBrain.cs:2093)" and "`Reassure` (CustomerBrain.cs:2207)"
— both are one line early.**
- *Evidence.* `public string CompleteJob()` is at 2094 and `public void Reassure()` at 2208; lines 2093 and 2207 are
  blank. Every other cited line in the trace checks out (see below).
- *Fix.* Correct the two numbers.

**[Minor] (Phase 2) report §6, callers — the per-spot `WaitingSpot.Claim` list misses some callers.**
- *Evidence.* It misses `NpcLifeChecks.cs:303` and `WaitingSpaceChecks.cs:64, 65, 66, 68, 70, 77, 93, 230`. These are
  holders and synthetic claims, and none depend on the scorer. The `WaitingArea.Claim`, `ClaimRepairBar` and
  `FreeSeats` lists are complete.
- *Fix.* Add them.

**[Minor] (Phase 2) report §3 item 4 and §6 — the window counter is understated.**
- *Evidence.* The report says the window counter's stools are "Snap To Seat off … visible and new". It doesn't say the
  Officer's favourite and the Cheerful leaning now send people there. That's the main visible consequence of the
  change (first Major).
- *Fix.* Say it plainly under *Decisions his to overrule*, with the scorer-side fix above.

## Checked and sound (Phase 1)

**Compile.**
- `csparse` is clean on all eight diff files and on the three patched files. Both patches pass `git apply --check`.
- Every identifier exists as used:
  - `GraceCameraEpisode.ProfileId` and `OfficerStory.ProfileId` (consts);
  - `CustomerIdentity.Archetype/IsRegular/Profile.PersistentId`, `CustomerArchetype.archetypeName`,
    `NpcSocial.Profile.Name`;
  - `Job.faultType`, `FaultType.Mechanical/Bureaucratic`;
  - `CustomerBrain.Identity/Record/CustomerName`;
  - `TableSeat.Style/SitStyle.Bench/SetDirty(GameObject)/Clean()`;
  - `WaitingArea.Release/Unregister`;
  - every `CafeRedesignShared` helper used, with its signature: `Step`, `Check`, `UndoStep(…, rebake:)`,
    `AddComponent<T>`, `StepGroup`, `Made`, `Note`, `Done`, `Records`, `InScene`, `PathOf`, `V`,
    `Run.Require/Info/Line/Check/Cafe`;
  - `PlayLab.Check/Note/report`, `DayShapeCheck.Clock`, `DayOneGuideUI.E`.
- No CS0136: every reused name is in a sibling scope or a lambda.
- No `UnityEditor` in the runtime files.
- `[Flags]` is used with `&`/`|`, and the step writes `intValue`.
- `FindObjectsByType<T>(FindObjectsInactive.Exclude)` is used without a sort mode, and there's no `GetInstanceID`.
- No asmdefs.

**Behaviour.**
- A free seat is always given: pass 1 is non-empty, `Claim` re-checks the same conditions, and crowding never refuses.
- Dirty seats are never chosen: `IsAvailable` is checked in `PickBest:181`, `ClaimRepairBar:142` and
  `WaitingSpot.Claim:84`, and rules §5 tests it through `WaitingArea.Claim`.
- Repair customers keep the bar: all of Day 1's (Cheerful and Chatty with cleans) and every regular. On Days 2-3,
  60-76 % of walk-in bench jobs stay.
- Nobody walks more than 2 m past a seat as good. In my model the median walk past the nearest free seat is 3.1 m, with
  a reason.
- Someone seated away from the bar can be served:
  - hand-back: `FloorAvailable` includes `JobReady`, which needs only the device in Ace's hands;
  - reassure: `FloorAvailable` includes `CanReassure`;
  - the patience bar and the job's colour dot are over every waiting head (`UiPins`);
  - only the across-the-counter shortcut is tied to the bar.
- Patrons and the lodger claim through `Claim` unchanged.
- `StationsCheck` (it holds the room but the stools), `NpcLifeChecks`, `NpcBeatChecks`, Sit 4, Hands on it, the
  conversation lab and the hand-over probe don't depend on which seat the scorer picks.

**Cost.**
- A cached `NavMeshPath` with `GetCornersNonAlloc`: no allocation per route.
- Routes per claim in my model: mean 3-6, 90th percentile 5-11, worst about 22. The old code always did 8.
- Nothing per frame.

**The editor step.**
- Its eight name rules match `RepairBarSeats.SeatName`, `CafeSeatsSteps.MakeSeats` ("… on the banquette" before
  "… chair"), `CafeLivedInSteps.cs:1290-1291` and `CafeDressing3Steps.cs:634`: 3 + 6 + 8 + 4 + 3 + 2 + 2 = 28 seats in
  7 zones.
- The six areas give exactly the step's own result table.
- One undo group: the seats' SeatTags are recorded as `addedComponents`, the group and its six areas as `made`, so
  `UndoStep` removes exactly those after a save.
- A second run is refused ("Already done"), and so is a left-behind group.
- Nothing solid is added, so no re-bake.

**Art tidy 1 (builder C).** It only switches off the banquette chairs' coats and the coat stand's scarf and tote. No
object overlaps; only the redesign's newest-first undo order connects the two steps, and both builders document it.

**The required patches.**
- *Waiting space.* It reads `OnTheirWayNear(middle) == 0` in the real scene, which is the 12 Oct mechanism, and expects
  a floor seat for a claimer with no repair. That's the new design: bench jobs reach the stools through
  `ClaimRepairBar`, still proven middle, east, west, then nothing.
- *The drink claim.* For a claimer with no leanings it equals `PickBest` in an empty room: one of the nearest seats
  within 2 m, never a stool.
- *Life 4.* The `StaysAtTheBar` filter keeps the stools' sit checks and the fourth's floor seat. With 12 tries a
  stayer comes well over 99 % of the time.

**The edit-mode rules.** §1-§5 are meaningful. §5 goes through `WaitingArea.Claim` in a preview scene and relies on
`isActiveAndEnabled` in Edit Mode, as the Waiting space check already does. §6 is a model (straight walks, the lounge
guessed), and says so. All 43 pass in my model.

## Phase 2: the builder's report checked

- **Callers** (§6). `WaitingArea.Claim` (CustomerBrain:1641, PatronBrain:282, NpcLifeChecks:312, WaitingSpaceChecks
  248/344), `ClaimRepairBar` (CustomerBrain:1640, WaitingSpaceChecks 313/320) and `FreeSeats` (PatronSpawner:179,
  LodgerDay:133, CafeLifeRecorder:300, NpcSitLiveCheck:248) are all right and complete. The per-spot list is short of
  nine lines (above).
- **The hand-back trace** (§2). Correct except two off-by-one lines:
  - `JobReady` 615-632, `IsAvailable` 12-16, `FloorAvailable` 27-30;
  - `FindBest` 550-572, `reach` 51, the sphere 558;
  - `FindFloorTargetAmong` 701-748 with the filter at 719;
  - `FindFirstPersonFloorTarget` 658-682, `Reachable` 693-694;
  - `Interact` 112 and 115-118;
  - `CanReassure` 645-649;
  - DayOneGuideUI 636 and `Over` 781;
  - `CompleteJob` is 2094, not 2093, and `Reassure` is 2208, not 2207.
  
  Its conclusion holds: nothing in the hand-back or the reassure tests the seat.
- **The worked example** (§2). Re-derived exactly with straight-line walks from (1.6, 12.5):
  - nobody: −1.10 at the lounge's west chair, with four near-equals in two zones;
  - Cheerful+Distracted: window counter N, +2.73;
  - Chatty+Social: communal E4, +2.75;
  - Cheerful+Relaxed: banquette N cushion, +1.98;
  - Chatty+Hurried: communal E4, +0.38;
  - the Officer: window counter N, +18.23;
  - seated in turn: window counter N, banquette N chair, communal E4, middle cushion, E1.
  
  Two catches. As the game rolls him (Distracted) the Officer scores +19.73 at the same stool. And three of
  Cheerful+Distracted's four near-equals, and all three of the Officer's, are window-counter stools, where they'd
  stand.
- **The check counts.**
  - "All 43 checks passed" is right: 44 `Check(` calls, one of them only in the exception catch.
  - The Seat tags 1 counts are right: Window 11, Lounge 7, Bar 6, Communal 8, Quiet 2, NearDoor 4; 28 to tag, 6 areas.
- **The simulation claims.** They can't be re-derived as given, and an independent model disagrees (above).
- **Repair-bar shares.** "Day 1 none sit elsewhere; Days 2-3 about 40 %; Days 4-5 about 25 %" matches the leaning
  table (12/30 and 12/50 sorts), assuming half the repairs are long.
- **"Life 4 and the Waiting space check fail until the required edits are applied."** True. The old `given == middle`
  and the 0.6 m nearest set both fail under the scorer, and Life 4 would take a Sentimental bench job who doesn't
  stay. So the required patch must land in the same integration as S's diff.

**Verdict: ready after these fixes.**
- Must fix before playtest 8:
  - the window counter's standing waits (the scorer-side penalty, and the Officer's favourite sleeping until the
    stools sit);
  - the Day labs' seat-variety Checks (per-run notes, and medians over runs).
- Integrate together with S's diff: the required patch, and the guide-words patch.
- The rest are comment and report corrections.
