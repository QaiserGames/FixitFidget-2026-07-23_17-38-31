# A2 report: the start screen reviewed and polished (26 Oct 2026)

`/root/wt24/A2`, branch `work-A2`, commits `4d2c68d` and `88a6a90` on the builder's `daa8f02`. `TitleScreen.cs` and `TitleMenuUI.cs` changed, both `csparse` clean, marked `26 Oct 2026 (Start screen polish)`.

## 1. Fixed (A)

- **The HUD showed over the diorama.** The builder hid screens with `canvas.enabled = false` at order -150; `HudCorners` re-enables its canvas every LateUpdate at order 0, so the corners came back each frame. Now DayIntro's way: a CanvasGroup at alpha 0 on every root canvas, re-asserted at -150 and by a nested `LateHider` at 1600, restored in `Leave`.
- **A stale static after the reload.** I checked every static holding scene objects (~60). One fails: `NpcLifeProps.searched`, a once-a-session survey keeping the laptops, bags and bin station, so laptop guests and the bin drop would fail after the reload. `Reload` and `AfterReload` call `NpcLifeProps.Refresh()`.
- **Settings let the UI clock go.** `PausePhone.Resume` calls `UiClock.Release()`, one hold for everyone. Held again when the phone closes.
- **Words on a 16:10 Mac.** The canvas scales by shape: 26 px measures 24.7 at 1080p on 1440×900, under the house's 26 line, so the lab's word check would fail. Sub-line and strip are 28; slabs 500 wide.

Verified as built: `Commit` is the only save writer and refuses while `Up`; New Game over a save copies, proves, then deletes, behind the card; the cold open's layout is in memory only; all 35 lab menus set the CafeLab key; all 40 AfterSceneLoad hooks are safe twice; Continue reloads to a normal boot (intro, time scale 1); pad, mouse, keyboard, `Select(0)` at show; Cinemachine 3 calls match DayIntro's.

## 2. Polished (B)

- **Framing.** Pitch 47/53/59/63, distance 40/62/88/112 (was 50–66, 42–132): the 20 Oct overhead reads as a diorama near 43°; the widest row keeps the towers at the edge, never bare ground. The look-at point sits 14% left, so the café stands right of the menu at every bearing.
- **Cold open.** Lower and nearer (31°→30°, 22→19.5 m), eased push-in; on the freeze the camera drops back 0.3 m and settles in 120 ms with `cup.set`, then the logo slams.
- **Up from black** (0.8 s / 0.5 s): no furniture or crowd popping in.
- **Vignette** drawn in code (.22 diorama, .12 live rush, .34 frozen) under the words.
- **Slabs**: a 4 px darker lip along the bottom and a deeper shadow, a thick tile.
- **Rewind punch**: a white flash gone in 0.2 s, the band drops in on out-back, the shiver settles from 5 px to 2.
- **Continue/New Game**: the menu slips away as the dive starts; the fade overlaps its last 0.4 s.
- **Sound**: the research's cup-clink per move (`cup.set`, soft, flat); `ui.confirm` on choose. All placeholders.

## 3. On his Mac

1. Start screen 1, save the scene, Play on his save: no HUD corner, the café right of the slabs; try a 1440×900 Game view.
2. The lab: `[Start screen] All N checks passed.`; photos 01, 04, 05.
3. Continue: the intro plays; a laptop guest still gets a laptop.
4. Settings from the title, Back; the cold open freezes again.

## 4. Recommendations

- The orbit shows the bins every 60 s: keep it as the wink, or clamp to a 200° arc over the storefront.
- `cup.set`, `ticket.new` and `intro.grind` are stand-ins; a freeze stop, a rewind whirr and a menu tick are three short files.
- The warm grade is a flat 20% orange; a Volume grade would be richer.
- Nested `[DefaultExecutionOrder(1600)]` is legal; if ignored, only UiPins could show, and by day it shows nothing on the title.
