# Builder J — juice on the phone (23 Oct 2026)

Copy: `/root/wt23/J`. Rules: `/root/review23/builders-common.md` (read it first). Photos: `/root/review23/photos/` (`Recap_*` is the closing phone, `Pause_*` is the phone by day).

## His note (verbatim)
"I also would like more juice to happen on the settings or on the phone, like when transitioning on, when moving to different like buttons or moving down to a different category on the phone. Like, I want there to be like juice, I want there to be a feeling, I want there to feel good, you know."

## What's there
`Assets/Scripts/RecapPhone.cs` (about 2,470 lines) builds the phone in code: the closing recap (Tonight, Reviews, Franchise, Shop, Notes) and, through `PausePhone`, the pause phone by day (Today, Notes, Settings) and at night (Notes, Settings).

- Mouse, wheel, Q/E or LB/RB to switch apps, 1–5 to jump, W/S or the stick to scroll, the D-pad to move between buttons with a gold focus ring, A to press.
- Today, apps switch instantly (`RebuildNow`). The focus ring jumps. Buttons only tint on hover and press (`Pressable` colour blocks). The money counts up (the one animation).
- There is a tap sound: `Sfx.Play2D("phone.tap")`. Look at `SoundBank.cs` for what else exists.
- The 20 Oct colour fixes (`TextOn`, the deeper words) stay.

## What to build: the phone should feel like a good phone
Everything is quick: no single motion over 0.2 s (0.12–0.18 s is the target), eased (ease-out back or quint for arrivals, ease-in for exits). It never delays input: a press during a transition finishes the transition at once and acts. All of it runs on unscaled time (the phone pauses the game).

1. **Switching apps.** The content slides sideways in the direction of the switch (Q/LB left, E/RB right; a tab click by its order), about a third of the screen's width, with a quick fade.
2. **The tab bar.** The active tab's colour and a small underline or pill slide to the new tab. The tab's icon gives a small pop (scale 1.0 → 1.15 → 1.0).
3. **The focus ring glides** from the old button to the new one, resizing on the way, instead of jumping. When focus moves off the screen, the scroll follows smoothly, not in a jump.
4. **Pressing.** A pressed button squishes (0.94) and springs back (overshoot to 1.03), with a tap sound. A pad press gets the same squish plus a short light rumble, if the game already has a rumble call (grep `Rumble`/`Haptics`/`Vibration` and respect the Vibration setting). A disabled button gives a tiny shake and a dull sound instead (only if a suitable sound exists; otherwise no sound).
5. **Rows arriving.** When an app opens, its cards cascade in: each card slides up a few pixels and fades in, 25–35 ms after the one before. Cap the whole cascade at about 0.3 s, whatever the number of cards.
6. **Scrolling.** Wheel and stick scrolling eases (a short glide to the target) rather than stepping.
7. **Opening and closing the phone.** It rises from the bottom with a slight overshoot and settles; it drops away on close. If a slide-in already exists, keep it and tune it.
8. **Sliders and toggles** in Settings: the knob follows smoothly, and a toggle's knob slides with a soft click.

Build a small tween helper the phone uses (e.g. `UiTween`, a static class driven from the phone's own `Update`, or a tiny `MonoBehaviour`), not a package. Keep allocations at zero per frame, as the existing code's comments demand.

## Labs: this is where it can break things
The recap lab (`RecapPhoneCheck.cs`, 128 checks) and the pause lab (`PauseCheck.cs`, 106 checks) press buttons, read positions and photograph the phone. With motion, a check that reads a position on the next frame may read a moving card.

- Give the phone a public `Settled` (no motion running) and make the labs wait for it (with a timeout) before they read positions or take photos. Both labs are yours to adjust in exactly those places.
- Add checks:
  - an app switch settles within 0.25 s;
  - a press during a transition still acts on the first press;
  - the focus ring ends exactly on the focused button;
  - nothing reads a mid-motion position.
- Photos: keep every existing photo name. Add one mid-transition photo for him to see the motion ("05b-shop-sliding-in").

## Files you own
`Assets/Scripts/RecapPhone.cs`, `Assets/Scripts/PausePhone.cs` (only if needed), a new helper file, `Assets/Scripts/Diagnostics/RecapPhoneCheck.cs` and `PauseCheck.cs` (only where they must wait for `Settled`, plus your new checks).

Builder A may propose a Settings row change (Aim help: Light / Strong / Off). Don't build that; the integrator will merge it after you.
