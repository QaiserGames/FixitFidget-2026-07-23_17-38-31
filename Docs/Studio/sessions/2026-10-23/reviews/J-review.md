# Review of builder J (phone juice), 23 Oct 2026

Reviewer: a separate agent who did not see it built. Base 76df1b4 (= Mac 8033112). The work is the uncommitted diff in `/root/wt23/J`: `Assets/Scripts/RecapPhone.cs` (+719/−28), `Assets/Scripts/UiTween.cs` (new, 234 lines), `Assets/Scripts/Diagnostics/RecapPhoneCheck.cs`, `Assets/Scripts/Diagnostics/PauseCheck.cs`. Only J's own files are touched (PausePhone.cs unchanged).

## Phase 1: my own reading (written before I opened the builder's report)

### What I did to check it
- Read every hunk and the code around it: all of RecapPhone.cs that the motion touches (build, Open/Step/OpenPause/ClosePause, Update/LateUpdate/HandleInput, the ring, the scroll, Rebuild, UpdateTabs, every app builder that makes a button, SliderRow, SwitchRow, ChoiceRow, Pill, Pressable, Place/Bottom/Box), plus PausePhone.cs, RecapUI.cs, PadInput.cs, GameSettings.cs, SoundBank.cs, Sfx.cs/SoundPlayer.cs (the tap's de-duplication), BenchFeel.cs (the rumble), PlayLab.cs, and both labs end to end.
- `python3 /root/tools/csparse.py` on all four files: clean.
- **Type-checked UiTween.cs for real** with Mono's `mcs -langversion:7.2` against minimal UnityEngine stubs (Object with Unity's `==`, Component, RectTransform, CanvasGroup, Graphic, Vector4, Color with both implicit conversions): it compiles. That settles the Color/Vector4 conversions, the `ref`/`in` uses (`ref Motion m = ref list[i]`, `Apply(in list[i], End(in list[i]))`), the nested struct in `static` signatures, and the pattern match in `On`.
- RecapPhone.cs can't be compiled here (base code uses C# 8 switch expressions and dozens of game types), so I listed every identifier the added lines use (358) and resolved each one by grep or against the Unity API. Every new member exists; `UiTween.Kind`/`UiTween.Ease` are always qualified, so they never collide with RecapPhone's own `Kind` property; `Math.Sign` has `using System`; no `OnDisable`/`Step`/`Closing`/`FocusRing` name is declared twice. I checked each changed method for CS0136 (a name declared in a nested block and again in the enclosing one, including a later declaration): none. Nested MonoBehaviours added with `AddComponent` (RootWatch, WheelGlide, Nudge) have precedent that already runs on his Mac (`ReviewScreen.Runner`, `RecapPhoneCheck.MoveProbe`).
- `Place()` and `Bottom()` now honour the pivot. I checked every one of the 29 `Place` call sites and every `Bottom` argument, and every `pivot =` in the file: all existing callers use top-left boxes (Box, Words, Picture, Tag), so their results are unchanged. The only rects that change pivot are the ones J centres on purpose (tab icons, the pill, a pressed button and its shadow, the Close button), and those all go through the pivot-aware `Place`.
- Traced every `Looking(...)` in both labs (the reads of a position, a size, a colour or a photo) back to a preceding `Settle()` with nothing in between that starts motion. All of them are covered, so the new "nothing read a mid-motion position" checks should pass, and they would catch a regression if a read ever lands mid-motion.
- Checked who else reads the phone: only the two labs and PausePhone read `Root`; no other lab counts `phone.tap`; the Night 0/1/2 labs open on a restored recap (GameBootstrap runs at -100, so RecapUI's `Show()` lands in the phone's own made frame and the phone is simply there, with no rise to photograph); nothing else sends wheel events to the phone.

### Answers to the five things to look hardest for
1. **Compile errors:** none found (see above). No duplicate locals, no `ref`/`in` misuse, no struct-copy mistakes (`Knob k = knobs[i]; ...; knobs[i] = k;` throughout; the motions use `ref` locals), the Color/Vector4 conversions exist in both directions, and no usings are missing.
2. **Behaviour:**
   - *Delays or eats an input?* Every press path (`Pressed`, `Tap`, `Open`, `RevealKey`, `Refuse`) calls `FinishMotion()` first, so a press during a motion acts on the first press. The one narrow gap is mouse-down versus click (finding 4).
   - *Settled stuck false?* No. Every motion, glide and knob ends by time. While Root is off, Update finishes whatever is left; RootHidden clears the ring, scroll, velocity and closing. The stick's speed decays by a closed form to under StickRest in about 0.16 s. Every lab wait also has a 1 s timeout, so a stuck flag would fail a check, not hang the lab. One caveat for labs only: `MotionScale = 0` would freeze the clock. Only the recap lab sets it (1/16, then 1), and `Finish()` puts it back on any exit.
   - *Outlives its object?* No. `Step` drops a motion whose target is destroyed (Unity's `==`) and finishes one whose object is switched off. Rebuild deactivates the old pieces before `Destroy`, so their motions end the same frame. `knobs` is cleared on every build.
   - *Unscaled time?* Yes: the motion clock, the stick easing, the rumble timer, ScrollRect and the Selectable tints all run on unscaled time.
   - *Drop-away leaves the phone dead or invisible?* No. Both ways out reset everything: `RootHidden` (alpha 1, raycasts and interactable on, body at rest) and `BeginRise` (raycasts and interactable on, and a rise from wherever the drop had got to). Selectables recompute their group state on enable.
3. **Per-frame allocations:** none in Update, LateUpdate, `Step`, `StepKnobs`, `StepScroll` or `PlaceFocusRing`. There are no closures, LINQ or boxing there: the eases are a switch, motions are structs in an array, `TryGetComponent`/`AddComponent<CanvasGroup>` run only at build time, and the per-button lambda `() => Pressed(button)` is made once per build.
4. **Labs:** every position, size, colour and photo read waits for `Settled`, and no existing check is weakened. The one changed check (PauseCheck's Moment, "puts it away and gives the game back") is split in two: the game given back is checked at once as before, and the phone off is checked after a ≤1 s settle, because the drop is new by design. The new timing checks are sound at 15 fps; my working is under finding 7.
5. **Spec and rules:** within the owned files, the dates are right, no player-facing words were added, and no public member was removed. Spec deviations are findings 1, 3 and 5.

### Findings, worst first

There is no Blocker or Major in Phase 1. Everything below is Minor (polish, a spec deviation for him to accept, or cleanup).

**1. [Minor] `Assets/Scripts/RecapPhone.cs:926-934` (PlaceFocusRing) with `:969-983` (RingAround): the pad's ring vanishes when the D-pad moves to a button wholly outside the app's window, and then reappears without gliding.**
- Evidence: `show` requires `RingAround(...)`, which returns false when less than 8 units of the button are in the window. The `!show` branch then sets `ringOn = ringGliding = false; ringFor = null`. When the scroll glide brings the button in, `!ringOn` places the ring "simply there". So moving down past the window's edge blinks the ring out and back instead of gliding. Spec 3 asks: "The focus ring glides from the old button to the new one ... When focus moves off the screen, the scroll follows smoothly."
- Fix: when the only reason for hiding is clipping, keep the old ring state, so that when the button comes into view the ring glides from where it last was:
```csharp
bool inView = false;
bool candidate = !closing && PadInput.UsingPad && target != null && selected.activeInHierarchy && target.IsChildOf(Root.transform);
if (candidate) inView = RingAround(target, out centre, out size);
bool show = candidate && inView;
if (focusRing.gameObject.activeSelf != show) focusRing.gameObject.SetActive(show);
if (!show)
{
    // Merely scrolled out of the window (the glide is bringing it in): keep ringOn/ringFor/ringCentre, so it glides from where it
    // was drawn; but no glide is "running" while it's hidden (Settled must not wait on a ring nobody sees).
    if (candidate) ringGliding = false;
    else { ringOn = ringGliding = false; ringFor = null; }
    return;
}
```
- The labs are unaffected: both ring checks run after `Settle()` with the button fully in view.

**2. [Minor] `Assets/Scripts/RecapPhone.cs:1101-1109` (Now) with Rebuild in the open frame: on a slow Mac most of the rise and the first rows' arrival is skipped.**
- Evidence:
  - The frame the phone comes out draws the motion's start: `Add` applies `from`, so the rise starts at alpha 0 and the phone is invisible.
  - The app is built in that frame's LateUpdate (dozens of TextMesh Pro texts measured), which makes the next frame long.
  - `Now()` then advances by that whole frame. With OutBack, a 50-80 ms frame puts the rise at about 80-99% on the first frame anyone sees: `OutBack(0.28) ≈ 0.80`, `OutBack(0.44) ≈ 0.99`. Alpha is already 1 by then (RiseFade is 0.09 s).
  - Bible §7: "The labs can run at 15–20 fps there ... anything stepped per frame ... must be written for that." On his Mac the rise will read as a pop.
- Smallest fix: don't let the build frame's own time count:
```csharp
int builtFrame = -10;            // set in Rebuild: builtFrame = Time.frameCount;
float Now()
{
    if (motionFrame != Time.frameCount)
    {
        motionFrame = Time.frameCount;
        float dt = Time.unscaledDeltaTime;
        if (Time.frameCount == builtFrame + 1) dt = Mathf.Min(dt, 1f / 60f);   // the frame after a build: its own build time isn't the motion's
        motionNow += dt * Mathf.Max(0f, MotionScale);
    }
    return motionNow;
}
```
- This deliberately makes the clock run slower than real time for one frame, so it eats into the labs' realtime grace, and the pause lab could fail its rise check at 15 fps. If he takes this fix, widen both grace terms by a frame: RecapPhoneCheck.cs:544 and PauseCheck.cs:355 `took <= .25f + 3f * longest + .001f`. Otherwise, treat this as something to look at on his Mac first: if the rise reads fine there, leave it.

**3. [Minor] `Assets/Scripts/RecapPhone.cs:94-98` (header) and RecapUI.cs:264/308 (spec deviation): the closing recap phone does not drop away on "Close up for the night".**
- Evidence: spec 7 says "It rises from the bottom with a slight overshoot and settles; it drops away on close." Only the pause phone drops (`ClosePause → PutAway`). RecapUI still does `panel.SetActive(false)`. The header says so plainly ("The closing recap still goes at once when Close up is pressed").
- It's a defensible call: RecapUI isn't J's file, the night's fade follows immediately, and labs wait on `!RecapUI.Showing`. But it is his to accept. It belongs under "Decisions his to overrule". If he wants the drop, the outside edit is: in RecapUI.OnNextDay and OpenTomorrow, call `phone.PutAway()` instead of `panel.SetActive(false)` when `phone != null`. The panel would then stay on for 0.14 s. RecapPhoneCheck's `Until(!RecapUI.Showing, 5 s)` already tolerates that, and NightOneCheck.cs:188 reads it only after `Until(Phase.Night)`. J's own version of this edit, with its caveat, is checked in Phase 2.

**4. [Minor] `Assets/Scripts/RecapPhone.cs:2798` (Pressable) with `:1264-1272` (Pressed): a mouse press finishes the motion only on the click (pointer up), not on pointer down.**
- Evidence: InputSystemUIInputModule sends `pointerClick` only if the release is over the same click handler as the press. During the first ~50 ms of a slide, the content moves up to ~90 units sideways (OutQuint from 147). A press that starts on a small price pill can therefore be released over something else and lose its click. Spec: "a press during a transition finishes the transition at once and acts."
- It's practically rare: a human clicks well after 50 ms. The pad and the keys are unaffected.
- Fix: add one handler, created with the other nested helpers and added in `Pressable` (`rect.gameObject.AddComponent<PressDown>().phone = this;`):
```csharp
sealed class PressDown : MonoBehaviour, IPointerDownHandler
{
    internal RecapPhone phone;
    public void OnPointerDown(PointerEventData data) { if (phone != null && !phone.closing) phone.FinishMotion(); }
}
```

**5. [Minor] `Assets/Scripts/RecapPhone.cs:1235` (RowsArrive): with more than five rows in the window, the cascade's beat drops below the spec's 25-35 ms (Settings, about 8 rows, gets ~14 ms).**
- Evidence: `stagger = Mathf.Min(RowStagger, RowLastStart / (rows - 1))` with RowLastStart 0.10 s.
- The spec itself conflicts here: "25–35 ms after the one before ... cap the whole cascade at about 0.3 s" against "an app switch settles within 0.25 s". J chose the 0.25 s. Fine, but it is a decision for him. If he wants a steadier beat: `RowLastStart = .11f` with `RowSeconds = .13f` keeps 0.24 s and gives ~16 ms at 8 rows. Nothing else needs to change.

**6. [Minor] `Assets/Scripts/RecapPhone.cs:1271` (Pressed) against `:709, :1014, :2276, :2483, :2645` (cleanup): every button whose action already plays `phone.tap` now asks for it twice in the same frame.**
- Affected actions: ToggleDetails, Tap (via More and the low-stock line), TogglePerson, Flip (every switch and pill), PressQuit.
- Evidence: SoundPlayer drops a cue asked again within its `minGap` (SoundPlayer.cs:144-146; `phone.tap` has minGap .05, SoundBank.cs:185), so the second request is silent and nothing is heard twice. But the second call is now dead code.
- The Shop's rule ("tap only if the purchase went through", lines 2249/2255) no longer holds, because Pressed taps first. A purchase can only fail if the day isn't over, which the recap phone never sees.
- Fix: delete the now-redundant `Sfx.Play2D("phone.tap")` in ToggleDetails, TogglePerson, Flip and PressQuit (keep Tap's, which the tabs need). Or leave them and note it. No lab counts `phone.tap`.

**7. [Minor / note] The labs' new timing checks, and three small lab notes.**
- **Sound, and not flaky at 15 fps.**
  - RecapPhoneCheck.cs:544 and PauseCheck.cs:355 compare the observed time against `0.25 + 2 × the longest frame`.
  - The motion ends at most 0.24 s of motion clock after the frame it began (rows' last start 0.10 + 0.14).
  - The coroutine sees `Settled` one frame after the clock passes that end. So `took < 0.24 + dt(k) + dt(k+1) + (c' - c)`, where c is how far into its frame `began` was read.
  - In the recap lab, c includes the Shop build, so c' - c < 0. In the pause lab, c includes `pause.Open()`. Both stay under the limit. At a steady 15 fps the recap check gives about 0.30-0.33 s against a limit of 0.384.
- **PauseCheck.cs:366** ("switching to Settings slides it in and settles within 0.25 s") checks only the schedule (`MotionLeft`) at the frame of the switch, not that it was seen still within 0.25 s. It is not flaky, but its message claims a little more than it checks. Acceptable; the recap lab does the full version.
- **RecapPhoneCheck.cs:1052:** the deliberate mid-motion photo 05b skips `ReviewScreen.Scan`. The rules say every photo runs the word check. The reason is sound (fading words would read as faint), and the comment says so. The integrator should just expect 05b to have no word report in the gather.
- **Nit:** turning Vibration on gives no confirming buzz, because `Pressed` buzzes before `Flip` turns the setting on. Calling `Buzz()` again at the end of `Flip` when `GameSettings.Vibration` has just turned on would give one.

### Things I checked that are right (so the integrator needn't)
- `UiTween`:
  - The eases are continuous at their joins. The squish dips to 0.94, overshoots to 1.03 and ends at exactly 1. The pop ends at 1. The shake ends at its start. OutBack(1.2) overshoots by about 5.3%.
  - One motion per object and kind, replaced in place. `Finish` puts delayed rows at their end.
  - The capacity of 96 covers a full Settings cascade (about 47 motions).
- The phone's clock:
  - It advances once per frame, by whoever asks first. PausePhone runs at -450, so its Open and Resume and the phone's own Update and LateUpdate share one time per frame.
  - A motion begun in a coroutine draws its start that frame.
  - A rebuild during a pill or colour motion is overwritten by `Step` in the same LateUpdate, so nothing flickers.
- The drop:
  - Clicks pass through to the café.
  - A selected button can't be pressed (`interactable = false`, and PausePhone also clears the selection).
  - Esc can take the phone out again, and it rises from where it got to.
  - No code outside the labs depends on Root going off at once.
- The wheel: ScrollRect's sensitivity is 0, so the ScrollRect's own OnScroll still runs but moves nothing. WheelGlide applies the same reading of the notch × 18, as a glide, and a drag still belongs to the ScrollRect (StepScroll gives way when the content moves under it).
- Sliders: the knob and fill anchors match what Unity's Slider writes for LeftToRight with a non-filled fill. A D-pad step, a click and a drag all glide.
- Rumble:
  - `PadInput.Rumble`/`StopRumble` exist, and the phone stops only a rumble it started itself (`buzzUntil`).
  - It respects `GameSettings.Vibration`, `UsingPad` and `Application.isFocused`.
  - (0.10, 0.18) for 0.05 s is the bench's lightest pulse (ToolPickup is .10/.20/.04).
- The disabled-price shake (Nudge, an `IPointerClickHandler` beside the Button):
  - Unity calls every click handler on the GameObject; the Button ignores the click because it isn't interactable, and Nudge shakes it.
  - A pad can never select a disabled button (WireNavigation skips them).
  - No sound: SoundBank has no soft "no" cue. The nearest, `circuit.fizzle`, is a World-bus, 3D bench sound. Correct per the spec's "only if a suitable sound exists".
- Photos: every existing photo name is kept in both labs, and 05b-shop-sliding-in is added.

## Phase 2: the builder's report, checked

Read only after Phase 1 was written. The claims, one by one:

**The check counts are right: 136 and 113.**
- I counted the base labs' nominal path by hand.
- RecapPhoneCheck at 76df1b4:
  - Run's nominal path has 127 checks, and Start adds the playtest-save check: 128.
  - The path assumes the sample Day 3's guests paid, the takings were still counting at the first look, the guessed person has more than one fact, an upgrade is affordable, S reached the game, and no other pad was plugged in.
  - J adds 8 `Check` call sites and removes none. All 8 are on that path: the five in TheMotion, the two ring checks after PadSteps' early exits, and the final tally. That makes 136.
- PauseCheck at 76df1b4:
  - Run 32, three Moments of 15, the apps 26, the close-up 2, PlayLab's "nothing logged an error" 1: 106.
  - J adds 6 call sites and removes 1. The Moment's split runs three times, so that is +7 checks: 113.
- The pass lines match the labs' own logs (`[Recap phone check] All 136 checks passed.`, `[Pause check] All 113 checks passed.`).
- The three menu paths exist exactly as written (RecapPhoneSteps.cs:25/31, Playtest3Session3Steps.cs:20/23, NightOneSteps.cs:44/325).

**The allocation review is right.** I found nothing per frame that it missed. The event-time allocations it lists are the only ones: a CanvasGroup per arriving piece, the per-button closure, and the shadow's `name`.

**"Every existing public member is kept": right.** Diffing the `public` lines from base to J shows additions only.

**"`PausePhone.cs` unchanged; its header is still true": right.** "Putting it away gives everything back exactly as it was, at the end of that frame" still holds: the game is back that frame, and only the picture drops.

**Wrong or misleading in the report:**
- **Decision 13** says "one condition in `KnobFollows` skips the glide while dragging". There is no such condition. `KnobFollows` (RecapPhone.cs:1330-1343) always glides, so the knob does trail a mouse drag by up to 0.1 s, as the same line also says. If he wants the drag to stick to the pointer, it needs code. The smallest version: a tiny `IBeginDragHandler`/`IEndDragHandler` component on the slider that sets a `dragging` flag, and in `KnobFollows`, when it's set, `k.shown = k.to = slider.normalizedValue; k.gliding = false; DrawKnob(in k);`.
- **Risks** says "a hitch over 0.25 s fails the timing checks honestly". It doesn't. The realtime grace is two of the longest frames seen, so a hitch widens its own grace, and the only part a hitch can't touch is `scheduled` (the phone's own timing). This is harmless and in the lenient direction; just don't expect a hitch to show up there.
- **Risks** names "MoveProbe, BenchRing" as nested MonoBehaviours already in the repo. BenchRing is a top-level `MaskableGraphic` in BenchGlow.cs. The real precedents are `RecapPhoneCheck.MoveProbe` and `ReviewScreen.Runner`, and they do prove `AddComponent` of a private nested MonoBehaviour works on his Mac.
- **"RecapPhone.cs (+747 / −20)"**: numstat says +719 / −28 (747 is the two added together). Trivial.
- **Missed in the report**, so they should be added under risks or decisions: Phase 1's findings 1, 2, 4 and 6.
  - 1: the ring vanishes for a target wholly out of view.
  - 2: on a slow first frame the rise is skipped.
  - 4: the click finishes motion but the pointer-down doesn't.
  - 6: the duplicate tap calls. Decision 7 covers only the failed-purchase half.

**"Changes outside my files", each checked:**
1. **RecapUI.cs, the recap's drop (optional; J recommends not).**
   - *Does it compile?* Yes. `PutAway()` and `Closing` are public, and `Showing` can read `current.phone`: it's a private field of the same class, read from its own static member.
   - *Would it work?* Mostly.
   - The NightOneCheck.cs:188 wait isn't needed: that check runs only after `Until(Phase.Night)`, long after a 0.14 s drop. It's harmless.
   - The new `Showing` also changes what BenchGlow, UiPins, ShopUI, Juice and `PausePhone.Busy()` see for those 0.14 s (the HUD would come back while the phone drops). That seems fine, but it is a change.
   - **One gap:** if `RecapUI.Show()` ran while the phone was still dropping, `panel.SetActive(true)` would be a no-op. RootWatch's OnEnable wouldn't fire, the drop would finish, and the recap would vanish. Today no path calls Show within 0.14 s of a PutAway (ShowAgain and ContinueAfterNight come after a whole night). If it's ever applied, give RecapPhone a public way to take a dropping phone out again (e.g. `public void TakeOut() { if (closing) BeginRise(); }`) and call it in `Show()`.
2. **SoundBank.cs, `phone.nope`: compiles and works as described.**
   - `C(name, bus, threeD, volume, when)` matches the signature (SoundBank.cs:98).
   - A cue with no file plays nothing: `PlaceholderSounds.For` returns null for an unknown name.
   - Sound bank 2 checks Audio's table against the code, not the code's extra cues against the table, so it won't fail.
   - The bank asset only gains the cue when Sound bank 1 refills it; until then `Bank.Find` is null, and it's still silent.
   - The `Sfx.Play2D("phone.nope")` line goes in RecapPhone.cs, J's own file. Leaving it out follows the spec ("only if a suitable sound exists"). Sounds are his to do, per his notes.
3. **`UiTween.cs.meta`: right.** Unity makes it on import; commit it with the file.
4. **Builder A's Aim help row: the pointers are right.**
   - The `aimhelp` SwitchRow is at RecapPhone.cs:2433-2434, and the morning's `ChoiceRow` is at 2453 with the signature J describes.
   - No lab uses the key `aimhelp`. PauseCheck's pad route starts from `vibration`, below it, so it is unaffected.
5. **Builder N's notebook pictures: the advice is right.** Children of the card squish and arrive with it; `FinishCard` must keep the shadow directly before its card; `Place` and `Bottom` take any pivot.

## Summary for the integrator

- It should compile on Unity 6000.5.2f1. UiTween.cs type-checks with a real compiler. Every new identifier in RecapPhone.cs and the labs resolves, and nothing is declared twice.
- Both labs should report 136 and 113. They can't hang: every wait has a 1 s cap, and their new tallies would catch any read made mid-motion.
- Nothing outside J's four files has to change.
- For him to decide, plus the report's own list:
  - the recap phone stays instant on Close up (finding 3);
  - the cascade's beat shrinks below 25 ms on long apps (finding 5).
- Worth folding in, small and local, but not gating:
  - finding 1 (the ring's glide into a scrolled-off button);
  - finding 2 (the rise skipped on a slow first frame; look on his Mac first, and widen the labs' grace if it's applied);
  - finding 4 (finish on pointer down);
  - finding 6 (the duplicate taps).

**Verdict: ready to integrate.** There is no Blocker or Major. Findings 1, 2, 4 and 6 are small polish he can take now or after he has seen the motion on his Mac.
