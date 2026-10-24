# Builder J: juice on the phone. Report (23 Oct 2026)

(As handed back by builder J. Copy: /root/wt23/J, uncommitted; `git add -N` on the new file so `git diff` shows `UiTween.cs`.)

`python3 /root/tools/csparse.py` says **clean** for all four files.

## 1. What I did
- `Assets/Scripts/UiTween.cs` (new, 234 lines): a small tween helper. Its motions are structs in an array, its eases are a switch, and a colour rides in a Vector4. It allocates nothing per frame. `Finish()` puts every motion at its end at once.
- `Assets/Scripts/RecapPhone.cs` (+747 / −20): all the phone's motion. New public members: `Settled`, `MotionLeft`, `MotionScale` (labs only), `FocusRing`, `Closing` and `PutAway()`. Every existing public member is kept.
- `Assets/Scripts/Diagnostics/RecapPhoneCheck.cs`: waits for `Settled` before every read of a position, size, colour or photo. Adds 8 checks and the photo `05b-shop-sliding-in`.
- `Assets/Scripts/Diagnostics/PauseCheck.cs`: the same waits, plus 7 checks.
- `PausePhone.cs`: unchanged, because it wasn't needed. Its header is still true.

## 2. Decisions and reasoning
- **The engine.** The phone owns one `UiTween.Set` and steps it in LateUpdate, after any rebuild that frame. Its clock is unscaled time × `MotionScale`, moved on once a frame. A motion shows its start value the frame it is added. There is one motion per object and kind.
- **Input never waits.** Every press, tab, Q/E, number key and `Open` first calls `FinishMotion()`, which ends all motion at once. Then the press acts.
- **1, switching apps.** The new app slides in from a third of the display's width (0.16 s, out-quint). Q/LB brings it from the left and E/RB from the right, even when the switch wraps. A tab or number key goes by its order. The fade comes from the rows' own fades (point 5). The old app is not animated out: the phone has one content area, rebuilt in place.
- **2, the tab bar.** A soft accent pill (58×30) sits behind the open tab's icon. It slides to the new tab (0.18 s, gentle out-back). The two tabs' colours cross-fade (0.14 s), ending on the same colours as before. The new icon pops 1 → 1.15 → 1 (0.16 s). Coming from Tonight, which has no bar, the pill is simply there.
- **3, the ring.** It glides from button to button in 0.12 s (out-quint), resizing, and lands exactly on the button. While a button scrolls in, the ring hugs the part inside the window. Before, it hid until the button was fully visible. The scroll follows in a 0.14 s glide.
- **4, pressing.** Pressable now adds `Pressed` as the first listener of every phone button. It squishes 0.94 → 1.03 → 1 (0.18 s) about the button's middle and plays the tap. Close up keeps its own sounds instead of the tap. A card squishes together with its shadow. If the press rebuilds the app, the squish carries on on the new button with the same key. **Rumble:** `PadInput.Rumble(0.10, 0.18)` for 0.05 s, the bench's lightest pulse; only when the pad is in use, `GameSettings.Vibration` is on and the app has focus; stopped on real time. **Disabled button:** only a mouse click can reach one (a pad never selects it). It shakes 5 units over 0.18 s with no sound. Nothing in SoundBank fits a soft "no".
- **5, rows arriving.** Each child of the app's content is a piece. Pieces that overlap in height are one row (a card and its shadow). Only rows in the window move. Each rises 12 units and fades in over 0.14 s, 25 ms after the one before. The gap shrinks so the last row starts by 0.10 s, so everything is done by 0.24 s.
- **6, scrolling.** Wheel: each notch is the scroll rect's old 18 units, read the same way, but as a 0.14 s glide; notches in a row add up; the scroll rect's own wheel is set to 0; drags and flings are unchanged. W/S and the stick: the speed eases toward its target (0.04 s time constant), the same at 15 or 60 fps; it stops at once at an end.
- **7, opening and closing.** Opening: the phone rises 240 units in 0.18 s with about a 5% overshoot, and everything fades in over 0.09 s. Pause phone closing: it drops 200 units in 0.14 s and fades out; the game is already running again (PausePhone gives it back that frame); on the way down the phone takes no clicks, keys or pad (its CanvasGroup turns off `blocksRaycasts` and `interactable`); brought out again mid-drop, it rises from where it got to. Recap resumed at closing: appears at once. Recap's Close up: still instant, because RecapUI switches it off itself.
- **8, Settings.** A switch's knob slides across (0.14 s, out-back) as its track's colour turns; the tap is the click. A slider's knob follows its value in 0.10 s. The setting itself changes at once.
- **`Settled`** is true when none of these is running: a tween, the ring's glide, a scroll glide or easing, the drop, a knob, or the scroll rect's own fling.
- **Layout.** `Place` and `Bottom` now handle a box with any pivot, and `CentrePivot` re-centres a box without moving it. So squishes and pops turn about the middle while layout code still sets corners.
- **Allocation review.** The per-frame code has no closures, LINQ or boxing (Update, LateUpdate, `Step`, `StepKnobs`, `StepScroll`, `PlaceFocusRing`, `Settled`, `MotionLeft`). Allocations happen only on events: a CanvasGroup on each arriving row (once per app opening); the `Pressed` closure, once per button at build time; a name read when a card squishes.

## 3. Decisions his to overrule (each one constant in the "motion" block near the top of RecapPhone.cs)
1. Slide direction: Q/LB from the left, E/RB from the right (one sign in `Open`).
2. Slide size: a third of the display's width, 0.16 s.
3. Tab bar: a pill rather than an underline; AccentSoft; pop 1.15 in 0.16 s.
4. Cascade: a 12-unit rise, 0.14 s each, 25 ms apart, last start by 0.10 s, only rows in view.
5. Ring: 0.12 s glide; it now shows on a button that is partly in view.
6. Squish: 0.94 / 1.03 over 0.18 s on every button, Close up included. Tabs pop instead.
7. Tap on every press: a purchase that fails now taps too.
8. Rumble: 0.10 / 0.18 for 0.05 s, on pad presses only.
9. Refused press: a silent 5-unit shake.
10. Rise and drop: rise 240 units / 0.18 s (overshoot `UiTween.Back` = 1.2); drop 200 units / 0.14 s.
11. Recap close: stays instant.
12. Scrolling: wheel glide 0.14 s; key and stick easing 0.04 s.
13. Knobs: switch 0.14 s, slider 0.10 s (the slider's knob trails a mouse drag by up to 0.1 s; one condition in `KnobFollows` skips the glide while dragging).
14. 05b photo: motion slowed 16×, taken about 0.035–0.04 s into the switch; not word-checked (it would flag the fading words as faint).
15. Vibration's line: left as it is, although the phone now rumbles too.

## 4. Changes outside my files (all optional; I made none)
- **RecapUI.cs, only if the recap should drop away too (recommend not).** At line 264 and line 308, change `panel.SetActive(false);` to `if (phone != null) phone.PutAway(); else panel.SetActive(false);`. Change `Showing` to `current != null && current.panel != null && current.panel.activeInHierarchy && (current.phone == null || !current.phone.Closing)`. Then `NightOneCheck.cs` line 188 ("the recap is put away") must wait 0.15 s first.
- **SoundBank.cs, a soft "no" for a refused press.** After `phone.tap` (line 185) add `C("phone.nope", SoundBus.UI, false, .15f, "A price out of reach pressed on the phone: a soft, dull tick, quieter than phone.tap."),`. Then add `Sfx.Play2D("phone.nope");` at the end of `RecapPhone.Refuse`. A cue without a file is silent.
- **`UiTween.cs.meta`:** Unity creates it on import on his Mac. Commit it with the file.
- **Builder A's Aim help (Light / Strong / Off).** Where: `RecapPhone.BuildSettings`, replacing the `"aimhelp"` SwitchRow at lines 2433–2434. What: a `ChoiceRow(controls, cy, "Aim help", "<A's line>", <three words>, <current>, "aimhelp", word => <set it>)`, shaped like the morning's row at line 2453. The motion comes for free, and nothing in my code depends on the key `aimhelp`.
- **Builder N's Notes pictures.** Where: `BuildNotes` → `PersonCard` / `PersonRow`. Best as children of the card, so they squish, arrive and cast a shadow with it. Keep `FinishCard`'s shadow right before its card, where the squish looks for it. Set `anchoredPosition` by hand only while building; `Place` and `Bottom` work with any pivot.

## 5. Labs that prove it, and what a pass looks like
- **Fixit Fidget › Recap phone › Play check (lab, drives itself)** — `[Recap phone check] All 136 checks passed.` (128 + 8). `Logs/Recap/recap-phone-check-<time>/`. Every old photo plus `05b-shop-sliding-in.jpg`.
- **Fixit Fidget › Playtest › The phone by day - pause and HUD check (lab, drives itself)** — `[Pause check] All 113 checks passed.` (106 + 7).
- **Fixit Fidget › Night › Night 1 - Play check, keeping a straight face (lab, drives itself)** — one run to confirm it still passes; its `01-day-1-recap` photo should show the whole phone.

New checks: Recap lab — an app switch settles within 0.25 s (the phone's own timing, and a measured end-to-end time with two frames' grace); 05b was taken mid-motion; a tab clicked mid-slide acts at once; Details pressed while its row is arriving opens, and a second press closes it; back on Shop, the phone is still again; the ring is exactly on Close up, and exactly on the restock; "nothing read a mid-motion position" (a tally of every read). Pause lab — the rise settles within 0.25 s; the switch to Settings settles within 0.25 s; the ring is exactly on Short; the same tally; the close check split in two: "gives the game back at once" (straight away) and "puts it away" (after Settled), in each of the 3 moments.

Existing checks that now wait for `Settled` (none weakened; checks that only read words or state were left alone): RecapPhoneCheck — nothing else draws over the phone; nothing to scroll (Tonight); the split "in its place" (×2); every line fits the phone's width (Tonight, Reviews, Franchise, Shop, Notes list, Notes open); no gap where it would be; still one screen, every line fitting (×2); More / Details / the Notes tab / a person's row / the restock / the upgrade / Close up can be clicked; Reviews is longer than the window; S scrolls down; W scrolls back up; Tonight again: nothing to scroll; the gold ring shows; scrolled into view with the ring; LB and the right stick scroll it; with the pad gone, the ring goes; all 15 existing photos. PauseCheck — all 16 photos (through `Shot`; the photos with the phone away don't wait); puts it away (the split, ×3); the switch shows the setting / reads back as kept; three pills, the one in force in green; each mode's pill in green (×3); back to the old setting; A sets the morning to Short. The HUD corner positions are not the phone and are unchanged.

## 6. Risks, and APIs I'm unsure compile
- APIs to watch: `ref` locals and `in` parameters (C# 7.2+); `Component.TryGetComponent`; `CanvasGroup.interactable` blocking Submit and Move during the drop; `ScrollRect.velocity`; extra `IScrollHandler` / `IPointerClickHandler` components on the same object as the ScrollRect or Button (ExecuteEvents calls every handler on the object); private nested MonoBehaviours added with AddComponent (the repo already does this: MoveProbe, BenchRing); implicit Color ↔ Vector4.
- Trackpads send many small scroll steps; each glides the same distance the old jump did.
- Slow machine: a hitch over 0.25 s fails the timing checks honestly.
- 05b photo: if the editor spends more than 3 s compiling shaders before it, the slowed motion ends first and the 05b check fails. Run it again.
- Arriving rows and the pad: a row on its way is read 12 units low when the pad jumps to a new app; at worst less margin, never cut off.
- Rumble: `StopRumble` stops any pad rumble, but nothing else rumbles while the phone is up.
- Row fades call Unity's canvas-group callbacks for a few frames: no garbage, a little CPU.

## Questions for Mansoor
1. Should the closing recap also drop away when you press Close up? Recommend no.
2. A soft "no" sound for a price you can't afford yet? Recommend yes (two lines of code, §4).
3. Vibration's line: change it to "On a pad: the bench's tools and the phone's buttons rumble."? Recommend yes.
