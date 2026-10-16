# NPC builder — wave 2 report (14 Oct 2026)

Worktree `/root/cafe/build/npc`, branch `b-npc`. Spec: `specs/npc-stools-guests-looks.md` §5 steps 1–6 and §6. Feel targets T21, call 13. Every changed `.cs` passes `cs_syntax_check.py`; nothing was compiled.

## 1. What changed

| File | Change |
|---|---|
| `Scripts/TableSeat.cs` | New `repairBar`, `maxLift`, `approachSide`. The defaults leave every seat as it was. |
| `Scripts/WaitingArea.cs` | `ClaimRepairBar(occupant, benchPoint)`: the free stool nearest the bench. Two within 0.1 m go to the one nearer the customer. |
| `Scripts/CustomerBrain.cs` | Bench repairs (`GoesToTheBench`: all but the Human fault) try the bar first. Regulars never look round on the way in; walk-ins do with chance clamp01(0.9 − urgency). It sets `NpcSocial.Watching` while their device is on the bench or in the close-up. |
| `Scripts/NpcSeating.cs` | A seat's own `MaxLift` is met inside the clips, in closed form: the walk ends on the floor, the body rises from 25 % to 85 % of Sit Down and lowers from 15 % to 75 % of Stand Up. Uses the seat's own side step, and a turned stool is always walked on its side away from the counter. New `SitAtOnce(seat, phase)`. |
| `Scripts/PatronBrain.cs` | `InitSeated(exit, seat, phase)`. New `Day` and `SettledGuest`. `StayUntil` lifts the 240 s cap while they stay, with a 60 s backstop once they leave. Stays ×2 at a laptop and ×1.3 for a pair. |
| `Scripts/PatronSpawner.cs` | `openingGuests` and `SeatOpeningGuests()`; static `SeatOpeningGuestsNow()` (contract §7). Also called from its own day-change branch, which covers the intro being Off. `ForceOpeningGuests` lets a lab ask for them. Releases seats still held by another day's patrons. |
| `Scripts/NpcSocial.cs` | Beat `LookRepair` (appended to the enum) and `Watching`: weight 45 seated, 30 standing, 3–7 s, only when the device is in view. |
| `Scripts/PolygonNpcVisual.cs` | Looks are dealt least-worn among people in the café (compared by name, ties at random), not hashed. A name's sex comes through `RegisterNameSexes`. The CC0 share is a plain roll, its outfits dealt the same way, never for a woman's name. Optional palette swaps (`paletteBase`, `paletteAlternates`; off when empty). |
| `Scripts/NpcVisualVariants.cs` | `LabelOf(int)`. |
| `Editor/CafeSeatsSteps.cs` | Note :43-48 rewritten. A rerun of Seats 1 lays the bar's full data and checks it. |
| `Editor/WaitingSpaceChecks.cs` | Stools found by `RepairBar`; "the second stool" kept. New checks: the three are flagged; middle → east → west from every counter place, and a fourth bench job gets nothing from the bar; a drink claim from place 0 still gets a nearest seat. |
| `Editor/NpcSitLiveCheck.cs` (Sit 4) | The bar stools count as sat-on seats (hips within 5 cm). Feet are measured from the body's root on a raised seat. Facing is judged against `FacingPoint`. |
| `Editor/NpcWave2Steps.cs` + `.meta` (new) | Scene steps Wave 2 1/2/3 with their Undo menus, palette steps 6a/6b, lab Life 4. Also `RepairBarSeats`, the stool data Seats 1 shares. |

Commits:
- `7bf22b2`: steps 1–2;
- `406919f`: steps 3–4;
- `246d308`: steps 5–6;
- `072feed`: the steps, the lab and the checks;
- `1464a7b`: the turned-stool side fix.

The first four lack the session trailers, because amending them would rewrite history.

## 2. The Producer's Unity steps, in order

Apply §5's patches, compile, then run **Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)**.

Every Wave 2 step refuses in Play mode or in another scene. Each saves the scene and logs to `Logs/NpcWave2/<step>-<time>.txt`. A second run prints "Already done … Nothing changed."

1. **Open `AcesCafeLayoutPlaytest.unity`.**
2. **Fixit Fidget › NPC › Wave 2 1 - The repair-bar stools sit (scene).** It prints one line per stool:
   - "Seat (repair bar, west): Repair Bar False, Snap To Seat False … -> Repair Bar on, Snap To Seat on, Max Lift 0.32, Approach Side 0.50, Face Towards (-5.907, 1.000, 13.564) (west 45 degrees)";
   - the middle stool at (-3.493, 1, 13.564), east 45;
   - the east stool at (-2.493, 1, 13.564), east 45.

   No stand point moves.
3. **Wave 2 2 - Settled guests and the patrons' rhythm (scene).** It prints three guests, Days 1-3:
   - a reader, in the reading corner's N tub chair (or the S one);
   - a pair, at the banquette 2-top south (or middle);
   - a laptop worker, at communal east 1.

   They leave at 0.30, 0.40 and 0.55 (126, 168 and 231 s at 420 s). It also prints "Spawn Jitter: 0.35 -> 0.60". A NOTE appears if there is no `LI_Laptop_Open` at east 1.
4. **Wave 2 3 - The names' sexes (scene; after the CustomerSpawner patch).** It lists 16 names:
   - Woman: Valerie, Priya, Ingrid, Alishba, Nina, Beatriz, Grace;
   - Man: Deshawn, Tomas, Mansoor, Ali, Walter, Omar, Malcom;
   - Any: Yuki, Saamin.

   Without the patch it REFUSES and says why.
5. *Optional (step 6):* **Wave 2 6a - Palette survey and line-up (read-only).** It writes `palette-lineup-<time>.png` only if a look's body uses an atlas found in `Synty/PolygonCity/Materials/Alts`. After looking at the sheet:
   - run **6b** (prefabs);
   - strike off any alternate that turns skin or hair unnatural, in Palette Alternates on Customer and Patron.

Undo: Edit › Undo, or later `Wave 2 1/2/3 - Undo` and `6b - Undo`.

## 3. The labs and checks, in order

1. **Checks › Waiting space reservations** (Edit mode). Pass line: "The café scene: N passed, 0 failed". The new lines:
   - "from the counter's place 0/1/2 … take: …middle…, then …east…, then …west…";
   - "a fourth bench job nothing";
   - "a drink claim from place 0 gets … one of the nearest seats".
2. **NPC › Life 4 0 - Play its lab session (Day 5, I serve).** After the bell, run **Life 4 - Stools, settled guests, looks**. Pass line: "Life 4 - Stools, settled guests, looks: PASS n/n". It writes `Logs/NpcWave2/life4-<time>/` and takes about 4–6 minutes; keep hands off.
   - **A.** Three bench jobs take the middle, east and west stools. Each sits with:
     - hips ≤ 5 cm off;
     - the root ≤ 3 cm over the floor before Sit Down;
     - the head never more than 2 cm below its seated height.

     From behind the bench, each is in reach and is E's first target when frustrated. Reassure works; Ace moves 0.00 m. They stand and walk off with no stall over 3 s. A fourth gets a floor seat. Photos: from above, from the bench, and the sit strip every 0.2 s.
   - **B.** Four guests are seated in the frame they're placed, with no patron income. Also checked:
     - the laptop is open;
     - the pair chat within 30 s;
     - the idle phases are more than 0.10 apart;
     - each leaves within 0.03 of its share;
     - the pair leave within 1.4 s of each other.

     Overhead photo after 2 s.
   - **C.** 10 patrons: at most 1 repeated look. 20 patrons: no look on more than 2. Six walk-ins: every name's sex matched (needs the patch and Wave 2 3).
3. **Sit 4**, in a lab session: no worse than 16/16. **Sit 6** now photographs the stools; check the knees against the counter face.
4. These must hold at today's counts:
   - Stations A/B: 95/95 and 94/94. They also hand back over the counter to the now-seated stool customer.
   - Life 2: 15/15. Life 3: 22/22. Attention: 11/11. Café life: 32/32.
   - Day 1 guide: 74/74. Day shape C: 15/15. Door rushes: 12/12 and 12/12. Mixamo 5: no worse than 47/52.
   - Neighborhood guards: 26 seats. Café life setup: 54/54. Lived in 2: 36/36 and 33/33.
5. **By eye**, on a Fresh game with the intro Full: the four guests are already seated in the first wide shot, with no pop-in. This needs §5.3.

## 4. Deviations from the spec, and why

1. **The names' sexes go through a registry keyed by name, not through `CustomerIdentity`.** The CustomerSpawner's parallel list (the spec's data) is registered by a two-line patch. My code compiles without that patch, and `CustomerIdentity` needs no change.
2. **If the instant seat fails, the guest does the ordinary sit from their seat's stand point** instead of `Init(exit)`. They are never at the door (Grace arrives at 0 s) and never announce an arrival.
3. **The idle phases are spread a quarter apart (± an eighth), not random.** With four random phases, two fall within 0.1 on about 78 % of mornings, so §6 B would fail.
4. **`StayUntil` lifts the cap (the spec's own option).** Its §2.2 arithmetic misses that `bornAt` counts the intro's held seconds (`DayIntro.cs:520`). At 420 s the 0.55 guest lands at about 238 s, against the 240 s cap.
5. **New: a turned stool is walked on its side away from the counter (`AwayFromTable`).** At 45°, the fewer-neighbours rule put the middle stool's step-in point 6 cm inside the counter's front (with the east stool taken). It only applies to seats with their own Approach Side.
6. **`approachSide` covers the way out too,** since the brushing happens both ways.
7. **A rerun of Seats 1 lays the full bar data, not just `snap`.** Snap alone with the 0.12 m lift would seat people 14 cm low.
8. **Life 4 is editor-driven, like Life 2/3 and Sit 4,** not a PlayLab: it needs no pad and stays in my files. It runs on Day 5, which can bring four bench repairs in a row; Days 1–3 bring at most three by design. B widens the guests' days in Play mode only.
9. **The hand-back over the counter stays the Stations lab's.** Life 4 checks reach and reassure.
10. **A woman's name never keeps a CC0 body:** they are men's (`Art/PlaceholderNeighbors/README.md:3`).
11. **Spec flag confirmed:** Bible §7's "a stool is claimed only once the customer has arrived" is wrong. It is the Producer's to fix.

## 5. Patches for other owners

### 5.1 `Assets/Scripts/CustomerSpawner.cs` (days): needed for the name match (Wave 2 3, Life 4 C)

Old:
```csharp
    [SerializeField] private string[] firstNames =
    {
        "Marisol", "Deshawn", "Priya", "Tomas", "Ingrid", "Kenji",
        "Rosa", "Abdi", "Nina", "Walter", "Yuki", "Leandro"
    };
```
New:
```csharp
    [SerializeField] private string[] firstNames =
    {
        "Marisol", "Deshawn", "Priya", "Tomas", "Ingrid", "Kenji",
        "Rosa", "Abdi", "Nina", "Walter", "Yuki", "Leandro"
    };
    [Tooltip("Beside First Names, one per name: the body that name walks in wearing (Any, Woman, Man). His sister's to " +
             "correct with the names; a missing entry is Any. NPC wave 2 (14 Oct 2026): a man's name no longer walks in " +
             "wearing a woman's body. Filled by Fixit Fidget > NPC > Wave 2 3.")]
    [SerializeField] private PolygonNpcVisual.NameSex[] firstNameSexes = System.Array.Empty<PolygonNpcVisual.NameSex>();
```
Old (unique):
```csharp
        roster.Reset(firstNames, reserved);
```
New:
```csharp
        roster.Reset(firstNames, reserved);
        // Which body each name walks in wearing (NPC wave 2): PolygonNpcVisual deals named walk-ins their name's looks.
        PolygonNpcVisual.RegisterNameSexes(firstNames, firstNameSexes);
```
`CustomerIdentity.cs`: **no change** (the sex is looked up by the name it already holds).

### 5.2 `Assets/Editor/Playtest4SystemsSteps.cs` (days): stools by flag (spec §6)

Old (:219):
```csharp
        foreach (TableSeat stool in active.OfType<TableSeat>().Where(s => !s.SnapToSeat))
            run.Info($"    bar stool (Snap To Seat off: waits standing beside it): {Playtest4Shared.PathOf(stool.gameObject)}  " +
```
New:
```csharp
        foreach (TableSeat stool in active.OfType<TableSeat>().Where(s => s.RepairBar || !s.SnapToSeat))
            run.Info($"    bar stool ({(stool.RepairBar ? "the repair bar: they sit on it, raised" : "Snap To Seat off: waits standing beside it")}): {Playtest4Shared.PathOf(stool.gameObject)}  " +
```
Old (:402):
```csharp
        var stools = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s.isActiveAndEnabled && !s.SnapToSeat).ToList();
```
New:
```csharp
        var stools = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s.isActiveAndEnabled && (s.RepairBar || !s.SnapToSeat)).ToList();
```

### 5.3 `Assets/Scripts/DayIntro.cs` (cine): contract §7, apply only if cine's branch lacks it

Old (`HoldTheWorld`, :523-524):
```csharp
        patronSpawner = FindAnyObjectByType<PatronSpawner>();
        if (patronSpawner != null && patronSpawner.enabled) { patronSpawner.enabled = false; patronSpawnerHeld = true; }
```
New:
```csharp
        patronSpawner = FindAnyObjectByType<PatronSpawner>();
        PatronSpawner.SeatOpeningGuestsNow();   // the settled guests in their seats under the first shot (NPC wave 2, contract §7)
        if (patronSpawner != null && patronSpawner.enabled) { patronSpawner.enabled = false; patronSpawnerHeld = true; }
```

## 6. Risks

**Compile.** These are the calls I was least sure of:
- `animator.Update(0f)` in `SitAtOnce`, to pose the guest this frame (a real API, but unused elsewhere in the repo);
- `PreviewRenderUtility.EndStaticPreview()`, which I hold as `Texture`;
- `Undo.SetTransformParent` with three arguments;
- `SerializedProperty.intValue` on the `NameSex[]` array;
- the patches reference `PolygonNpcVisual.NameSex`, nested in my file.

Everything else copies patterns already in the repo.

**At 15 fps.**
- **The raised sit.** It is closed-form from the clip's time. If the clip drops the hips before 25 %, the head may dip and then rise. Life 4 A measures it. The fix is a shorter ease window; the fallback is Snap To Seat off on the three stools.
- **Knees at 45°.** They have about 3 cm to spare. Sit 6 shows it.
- **The seated stool body's collider sits 0.30 m higher.** Watch Stations Lab B's first-person crosshair.
- **An animator not ready in its first frame.** The guest does the ordinary 1.3 s sit instead. That hides under the intro, but shows with the intro Off. Life 4 B catches it.
- **A morning that opens in the same frame as `StartDay`.** Yesterday's patrons are still alive then. Their seats are released; a guest whose seats still aren't free is skipped with a warning.

## 7. Open items, most important first

1. **Confirm the DayIntro line (§5.3) on the integrated branch.** Without it, the guests appear as control returns.
2. **The perch clip is still not on batch 2 (spec flag).** Playtest 6: does the raised sit read as sitting or as floating?
3. **Palette swaps are wired but unverified.** The Synty pack isn't in the cloud; it all hinges on 6a.
4. **The window counter's stools could sit unturned with the same fields.** Not this wave.
5. **Life 4's waits are guesses** (45 s to reach the till, 25 s to sit) until the Mac runs it.
