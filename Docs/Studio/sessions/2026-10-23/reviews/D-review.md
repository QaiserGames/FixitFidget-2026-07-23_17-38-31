# Review of builder D: the drinks machine's prompt (23 Oct 2026)

**Verdict: ready after these fixes.** Builder D's work compiles, and no lab reads what changed. It can go to the Mac once the ShopUI step-aside goes in with it (finding 1, D's own two lines) and once he answers the 24/22 px question (finding 2). The integrator's a9496a0 is ready as it is.

The diff is one file, `Assets/Scripts/BeverageStation.cs` (+509 / -123), with no new files. Base 76df1b4 (= Mac 8033112). I had not seen the build.

## What I checked

**Syntax.** `python3 /root/tools/csparse.py Assets/Scripts/BeverageStation.cs` gave "clean".

**Types (stronger than the syntax check).**
- I compiled the new file with `mcs` (Mono 6.8) against stubs of every member it uses. Each stub copies the repo's real declaration: type, parameters, static or instance, access.
  - From the project: `UiSkin.Text` (returns `TextMeshProUGUI`), `Paint`, `ScreenCanvas`, `Shadowed(TMP_Text)`, `SemiBold` (a `FontStyles`); `ShopUI.PromptLine`; `PlayerInteractor.Focused`, `CurrentStation`, `AimingAtDrinks`; `PlayerCarry.Carried`, `Count`, `Capacity`, `GetItem`, `GetHandItem`; `BeverageSlot.Cup`, `IsPouring`, `Progress`, `drink`; `BeverageControl.slot`, `dispenseButton`; `BeverageCupSupply.discard`, `SetGlow`; `CupStack.SetGlow`; `DrinkJob.Locked`, `Drink`, `IsEmpty`, `CanHandBack`; `CustomerBrain.CanReceiveDrink`, `WantedDrink`, `CustomerName`; `ShopInventory.Cups`, `CanBrew`; `PadInput.UsingPad`, `Icon(string,string)`; `ControlHints.Interact`, `LeftHand`, `RightHand`, `Back`; `JobTicket.DrinkIconName`.
  - From TMP and Unity UI: `TMP_Text.GetPreferredValues(string,float,float)` and `Image.Origin360`.
- Result: 0 errors.
- Control: renaming one member gave CS1061, so the check does catch mistakes. Stubs are in `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/stubD/`.

**The rules' compile traps.**
- No CS0136: no local clashes with an enclosing scope. A parameter called `name` in the static `Line` and `RingImage` is legal.
- No usings are missing.
- No `GetInstanceID`, no `FindObjectsSortMode`, no `UnityEditor`.
- Quotes inside an interpolation hole: the same pattern already compiles on his Mac (`PlayerInteractor.cs:357`).

**What reads the changed code.**
- I grepped `Assets/` and `Tools/` for `ActionText`, `ControlsText`, `RingShowing`, `RingFill`, `StackGlowing`, `HandsShowing`, `StepBackShowing` and `LineUp`. I also searched for the canvas and child names ("Drink station action", "Next action", "Focused object", "Current action", "Controls") and for reflection into `BeverageStation`.
- Nothing outside the file reads any of them.
- Day 1's "in its close-up the card says … cup stack" (`Day1GuideCheck.cs:493`) reads `DayOneGuideUI.CardText`, not this file.
- `CarryInteractionChecks.cs:96` only adds the component; `Start` never runs in edit mode.
- Four labs photograph the machine: Stations, Day 1 guide, Aim help and the dispenser's frame. None of them asserts on the drinks prompt, so none was weakened. They should pass as before.

**Line lengths.** I measured every line with the real font, `Assets/Fonts/Baloo2/Baloo2-SemiBold.ttf`, using fontTools at 34 px. The longest is about 636 px, "This Hot Chocolate is cold: pour it away" plus an icon of about 37 px. Ace's held cups leave a gap of about 714 px, so every line fits (Phase 1 had this as a finding; it is withdrawn).

## Builder D: findings, worst first

**1. [Major] `Assets/Scripts/BeverageStation.cs:248`, and report §4 "Required: none".** In first person, the half-second linger fails whenever Ace holds anything. The line drops out and ShopUI's line flashes up instead, unless ShopUI steps aside.

*Why it happens:*
- `lingering` (line 235) requires `player.Focused == null`. With no focus, `PlayerInteractor.UsingDrinks` is false (`PlayerInteractor.cs:375,391`).
- So `ShopUI.Update` skips its drinks branch (`ShopUI.cs:294`). It builds its own line, starting "In hand  …" from `carry.Summary` (`ShopUI.cs:336,344`).
- `PromptLine` is then not empty, and line 248 sets `up = false`.

*When he would see it:*
- **Crossing a gap with a cup in hand.** The housing between the controls stops the crosshair ray (`PlayerInteractor.cs:679`). Carrying the new cup from the stack to a nozzle, the drinks line vanishes. ShopUI's "In hand  Cup" pops in, then they swap back.
  - That is the flicker the linger exists to stop (lines 221-223).
  - At base the dark card stayed through the gap and hid ShopUI's flash behind it.
  - The report only says ShopUI's line and the strip "flash up". It doesn't say the drinks line itself drops out.
- **On a pad or mouse press.** LB, RB, A and the clicks are handled inside `PlayerInteractor.Update` (`:515-518`, `:532`), and every press ends in `ClearFocus()` (`:816-817`). So focus is null for the rest of that frame.
  - If ShopUI's Update runs after PlayerInteractor's, the line blinks out for one frame on every take. Neither script sets an execution order, so it may or may not happen.
  - His girlfriend plays on a pad.
- The close-up from above is not affected.

*The builder's ShopUI lines are correct.* I checked both:
- `ShopUI.cs:193`: `bool closeUp = interactor != null && (interactor.InCloseUp || interactor.UsingDrinks) || BeverageStation.LineUp;`
- `ShopUI.cs:294`: `if (interactor.UsingDrinks || BeverageStation.LineUp)`

They compile: one assembly, no asmdef, and `LineUp` is `public static bool`. They work: ShopUI reads `LineUp` as of the last LateUpdate and clears its line. `PromptLine` stays empty, so the drinks line holds and the two never deadlock. The crosshair expression in that branch is right for the linger.

*Fix:* make those two lines **required**, applied with this change after ShopUI's owner has landed their edit. If they can't go in this session, the fallback is `bool up = show;` (ShopUI's 26 px "In hand" line then overlaps for at most 0.5 s). Either way, the comment at lines 221-223 should say the linger depends on it.

**2. [Minor; Major in Phase 1, the report handles it] The spec's proof can't pass as written.** The spec wants "no S1 or S2 for the prompt's words" and also asks for the hands at 22-24 px.
- The word check grades anything under 26 px as S2 (`ReviewScreen.cs:50,331`). `HandsSize = 24f, StepBackSize = 22f` (line 64), so both are S2 in every drinks photo.
- The report says so (§2's table, §5's "S2 only", "Question for him") and reads "the prompt's words" as the action line. That reading redefines the pass, so he must OK it.
- *Fix:* his answer. Keep 24/22 (the café strip's own 22 is S2 in every photo; 226 S2s on 20 Oct), or set `HandsSize = 26f, StepBackSize = 26f`.

**3. [Minor] `BeverageStation.cs:318` (also 475-476, 490-500, 371), and report §2 "no per-frame garbage in the common path".** The pour, the most common moment at the machine, allocates every frame.
- `inSlot = slot.Cup` is worked out before the pour's early return.
- `BeverageSlot.Cup` calls `PlayerCarry.Contains`, a lambda that captures its argument (`BeverageSlot.cs:20-28`, `PlayerCarry.cs:141`). That makes a closure each frame a slot holds a cup.
- `OnlyHand` calls `GetHandItem`, another capturing lambda (`PlayerCarry.cs:140`), every frame while only the other hand can act.
- It is still far less than base, so this is about the claim being true, not a regression.
- *Fix:* move the three lines after the pour block; that compiles against the stubs.
  ```csharp
  DrinkJob inSlot = slot != null ? slot.Cup : null;
  bool filledInSlot = inSlot != null && !inSlot.IsEmpty;
  bool atReturn = supply != null && supply.discard;
  ```
  Then soften the comment at 475-476 and the report to "almost none".

**4. [Minor] `BeverageStation.cs:575` — "No cup under Latte" drops the waste warning, but A still pours.**
- `TryPour` pours with no cup and takes the beans: "Debit exactly once at the button press, even if there is no cup" (`BeverageSlot.cs:44`).
- The old prompt said "ingredients will be wasted". A newcomer presses A, the universal "use", on the paddle and loses beans with no warning.
- *Fix* (compiles): `case Kind.NoCupUnder: return $"No cup under {drink}: pouring wastes it";`

**5. [Minor] `BeverageStation.cs:68-69` — numbers that depend on ShopUI but don't say so.**
- `ActionMiddle = 200` is read off photos; the café prompt's place lives in the scene.
- 20/30/18/7 copy ShopUI's private strip constants (`ShopUI.cs:140`). If the HUD overhaul moves the strip, the Step back plate stays behind.
- *Fix:* one comment naming the coupling. Later, ask ShopUI's owner for read-only accessors.

**6. [Minor] Report §5 has two inaccuracies.**
- **Lab counts.** With the optional StationsCheck lines, Stations becomes 101/101 (first person) and 102/102 (overhead). That is 3 checks for each of the 2 `MakeDrink` calls. The table still says 95 and 96.
- **Predicted words in two photos.**
  - `drink-2a-the-whole-dispenser` reads "Take a cup from the stack first" only if the crosshair meets a cup pad. A paddle gives "No cup under …", and the housing gives no line at all, because nothing has been aimed at yet.
  - `3-overhead-at-the-stand-point-before-E` won't show "(E)". The lab touches a pad first (`DispenserFrameCheck.cs:94`), so it is the pad's A.

**7. [Minor] Report §3 leaves two behaviour choices out of "Decisions his to overrule".** One is that the linger now gives way to the café's "In hand" line. The other is that the paddle's waste warning is gone. §3.10 lists only the words.

**8. [Minor] `BeverageStation.cs:27,123` — the weight match is measured from photos, not from the scene.** The report measured stems at 0.139 em, the same as SemiBold. ShopUI never sets the prompt's style; the scene does (`ShopUI.cs:85-92`). *Fix:* on the Mac, Inspector → HUD prompt text → Font Style. If it is Bold, use `FontStyles.Bold` in `Line()`.

**9. [Minor] `BeverageStation.cs:342,558` — "Nobody ordered this one: pour it away" can show for someone who did order.**
- `WhoWants` keeps only `CanReceiveDrink` customers, which means Settling or Waiting (`CustomerBrain.cs:421`).
- So a customer who ordered but is mid-bark ("Speaking") reads as nobody. Base said "Nobody has ordered" in the same case, but the new line tells him to throw the drink away.
- It is rare. *Fix:* soften to "Nobody's waiting for this one".

**Withdrawn after Phase 2:**
- The "33 letters" line lengths: they fit, measured above.
- The drink picture instead of A: it is decision 7 in the report.
- The failed LB/RB hand choice: it is in the report's §6.

## Builder D's report: claims checked

| Claim | Result |
|---|---|
| Only `BeverageStation.cs` changed, +509 −123 | True (`git status`, `--stat`) |
| csparse clean; mcs 0 errors | True; my own stub compile also gives 0 errors |
| 28 states simulated, 28/28 | 28 `Show` cases in the harness (`scratchpad/sim/run.cs`). It also prints the linger giving way ("up False"), so the builder saw finding 1. It doesn't model the press-frame `ClearFocus` or ShopUI's update order. |
| No lab reads the drinks prompt (Stations, Day 1, Aim help, Juice, BenchLab, BenchViewProbe, BenchWordsCheck) | True. The only matches are `BenchCards.ControlsText` and `RecapPhone.FocusRingShowing`. |
| All five members kept; three added | True |
| Old scaler capped at 1.4 and shrank above 1260 px tall | True (old line 160: 1.2/1080 > 1.4/H when H > 1260) |
| Every line fits; longest 704 px of a ~714 px gap | Fits. With the real font I measure about 636 px, so their figure is on the safe side. |
| 20 Oct counts 95, 96, 81, 26, 50; dispenser frame 14 (15 Oct) | True (`sessions/2026-10-20.md:99-100`, `2026-10-15.md:34`) |
| Menu paths, `Logs/` folders, photo names | All exist as written (`Playtest3Session2Steps`, `Playtest4UISteps`, `Playtest4BenchSteps`, `JuiceSteps`, `ReimportChanged`, `ReviewScreenGather`) |
| ShopUI lines 193 and 294 | Correct line numbers; compile and work (finding 1). Should be required, not recommended. |
| Optional StationsCheck lines | Compile: `drinks` and `drink` are in scope, `RecapPhone.Plain` is public static, `Frames` and `Check` exist. They would pass at those moments: both hands are free at the stack, the line has been rewritten two frames into the pour, and the drinker is Waiting. The counts change (finding 6). |
| At Beatriz the café line says "(A) Serve the Coffee" | True (`CustomerInteractable.cs:63`) |

## The integrator's commit a9496a0 (`/root/integ23`): the night's "You" only from above, and the Night 0 check

**Compiles.**
- csparse is clean on `UiPins.cs` and `NightZeroCheck.cs`.
- The members exist: `CafeViewMode.OverheadShown` is public (`CafeViewMode.cs:196`), `PlayerCarry.Instance` is public static, and UiPins already has a `player` field (`:165`).
- The new field `view` clashes with nothing (`views` and `ViewOf` are different names).
- In NightZeroCheck, `UiPins.SelfAtNight`/`SelfShowing`, `SetFirstPerson` and `FirstPersonSelected` are public, and `Until`, `Seconds`, `Note` and `Check` are the lab's own helpers. The new locals (`selfSecondsLeft`, `doorEye`) are unique in `Run()`.
- No banned API.

**Does what its comments say.**
- `OverheadShown` is `!firstPerson && !AtStation` and no conversation, bench item, counter phone or intro. So "You" is covered from the frame V is pressed.
- `selfAtNightUntil` keeps running, so it comes back from above inside its 7 s (`NightCycle.SelfPinSeconds`).
- Day 1's "You" is only asked for from above already (`DayOneGuideUI.cs:940`), so the day is unchanged.
- `Day1GuideCheck` forces the overhead view before its "You" checks (`:111-114`), so nothing else breaks.

**The lab check works as written.**
- It runs about 2 s after nightfall, inside the 7 s window.
- `Showing` needs `placed`, which becomes false the first frame after the switch, so two frames are enough.
- It fails loudly ("NOT PROVEN") if the seconds ran out, so it is not weakened.
- The second wait (main camera more than 5 m from Ace, 3 s) fits the brain's ~0.75 s blend.
- The rest of Night 0 is driven by position, so the first person's turn of Ace (`CafeViewMode.cs:617`) changes nothing later.

Findings:
1. **[Minor] `UiPins.cs:446,464` with `:591-596` — coming back from first person, "You" can still sit at the screen's edge for part of the blend.**
   - `OverheadShown` turns true on the V press, while Cinemachine is still blending out of Ace's eye. `PlacePin` pins an off-screen head point to the edge, so inside the 7 s the pin can sit at the top edge, bobbing, for part of that ~0.75 s. That is the reported artifact, smaller.
   - The lab waits until the camera is more than 5 m away (`NightZeroCheck.cs:204-206`), so it never looks at the blend.
   - *Fix* in `PlacePin`, after `want = s.z > 0f;`:
     ```csharp
     if (pin == self) want &= s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;   // "You" points at Ace: off the screen it waits
     ```
2. **[Minor] `NightZeroCheck.cs:207-211` — "it's there again" is promised but not checked, and there is no photo for him.**
   - The UiPins header promises it, but the lab only writes a note. At about 3 s into a 7 s window it can be a check: `Check(UiPins.Instance.SelfShowing, "back in the view from above inside its seconds, \"You\" is there again");`
   - Nothing photographs the first-person moment he complained about. Add `yield return Photo("02b-night-0-in-first-person");` after the first check.
3. **[Minor] Expected count not updated.** Night 0 was 118/118 on 20 Oct; it is now 121/121, and the same +3 applies to the "Grace walked out" run. The commit says "three checks more" but gives no total. The header line "96 where it was 95" is old history.
