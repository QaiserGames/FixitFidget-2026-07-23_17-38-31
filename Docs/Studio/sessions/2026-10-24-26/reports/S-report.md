# S2 report: the start screen, built in the engine (26 Oct 2026)

Copy `/root/wt24/S`, branch `work-S`, commit `daa8f02`. All 8 files pass `csparse` (clean). Nothing compiled.

## 1. Files
- **New `Assets/Scripts/TitleScreen.cs`**: the rule (`ShouldShow`), both faces, the camera, the holds, and leaving by reloading the scene.
- **New `Assets/Scripts/TitleMenuUI.cs`**: the slabs, the logo, the controls strip, the confirm and credits cards, the fade and the rewind overlay.
- **New `Assets/Scripts/Diagnostics/StartScreenLab.cs`**: the lab (a PlayLab).
- **New `Assets/Editor/StartScreenSteps.cs`**: Start screen 1 and its Undo, the dev toggle, and the lab menu.
- **Marked edits (`26 Oct 2026 (Start screen)`):**
  - `SaveManager.cs`: `Commit` refuses while `TitleScreen.Up`. Adds `StartNewGameKeepingACopy` and the static `KeepACopy`.
  - `GameBootstrap.cs`: the cold open lays today's café, `Apply(new SaveData(), true)`. No `RestoreRecap` under the title.
  - `CafeViewMode.cs`: `OverlayOwnsInput` also counts `TitleScreen.Up`.
  - `FreshGameMenu.cs`: Fresh game 1 sets `TitleScreen.SkipOnceKey`.

## 2. How it works
- **The rule.** `ShouldShow` says no:
  - a second time in the same Play session;
  - in any lab session (`CafeLab.Active`). Every lab menu that presses Play sets the CafeLab key (I grepped all 34).
  - after Fresh game 1.
  - in the editor while the toggle is off.
  The exception is its own lab, which asks with `LabKey`.
  When the rule says no, the component turns itself off in `Awake`, so every lab runs exactly as before.
- **Holds.** `Start` runs at order -150, before GameBootstrap. It:
  - turns DayClock off before its first Update, so no bell, no intro and no time passing;
  - turns off the customer spawner;
  - holds Ace (`PlayerMovement.Hold`) and the UI clock;
  - pins the money;
  - turns off every other screen-space canvas each frame;
  - puts its own CinemachineCamera at priority 60.
- **The diorama.** The camera orbits `isometricFocus` once every 120 s and eases in over 3 s. It also calls `CafeViewMode.OrbitTo` on the same bearing, so the walls cut away. `Framings[4]` is the one table, picked by `TierFor(stars)`: up to 2 stars the café, 3 the block, 4 the district, 5 the offer. A comment marks the hook for "a map of cities". The settled guests are seated, and walk-in patrons come on the spawner's own clock.
- **The cold open.** It fills about 80% of the seats at once with patron bodies, and stands 4 more in the queue slots. These come from the patron prefab, never customers, so Grace never appears. Steam puffs rise over the drinks machine on a world-space canvas. After 3 s it freezes at time scale 0, the warm grade comes up, the logo slams in, and the menu appears without Continue.
- **Leaving.** Continue dives to the door in 1.2 s (ease in), fades, and reloads the scene. New Game over a save shows the confirm card, then backs up and deletes the save. New Game with no save plays the 1.8 s rewind: the crowd goes, last in first out, then black, then a reload into a real new game.
  - The game had never reloaded its scene before. Unity runs AfterSceneLoad hooks once per Play session, so `Reload` runs every one of them again once the new scene is in. I checked all 40: each is safe to run twice.
  - Time scale is 1 before every reload.
- **Settings** opens the pause phone's Settings app. The title lets go of Ace for that one call, and the phone's button reads "Back".

## 3. To run on his Mac, in order
1. **Fixit Fidget › Start screen › Start screen 1.** Pass: `[Start screen] DONE. …`. Then **save the scene**: the reload loads it from disk.
2. **Press Play.** Pass: the diorama on his save. Continue lands on his checkpoint and the intro plays.
3. **Fixit Fidget › Start screen › Lab - The start screen (drives itself).** It takes about a minute.
   - Pass: `[Start screen] All N checks passed.`
   - The photos are 01–06 in `Logs/StartScreen/check-<time>/`. Judge them by eye.
   - Queue line for Q: `play | Fixit Fidget/Start screen/Lab - The start screen (drives itself) | \[Start screen\] (All \d+ checks passed|\d+ of \d+ checks FAILED) | 240`
4. **Any existing lab, e.g. the Pause check.** Pass: the same result as before, and no title.

## 4. Decisions he may want to overrule
- New Game over a save moves the save to `backups/` (copied and checked byte for byte, then deleted) rather than writing a fresh one, because the title never writes.
- With no save, the cold open shows on every start. Until the first Open Tomorrow it can show again.
- The confirm card selects **Keep playing** first, so the safe answer is under her thumb.
- Text sizes are at least 26 px, not 22, because the house's word check flags anything under 26.
- The logo uses the Bold cut with dilate. There is no ExtraBold file.
- A recap save shows the morning light, not the recap.
- Placeholder sounds:
  - `phone.tap` when a slab is selected;
  - `ui.confirm` when one is chosen;
  - `ticket.new` for the logo slam;
  - `intro.grind` for the rewind;
  - `bench.showleft` when something is refused.
- New Game from the diorama dives to the door; it has no rewind.

## 5. Changes outside my files
None needed. The lab-queue line above is Q's to add. I did not change DayIntro, because the disabled clock never starts the intro.

## 6. Risks
- **The scene reload is new to this game.** Cached statics could hold old objects (I checked the risky ones: they null-check). The lab proves the day after the reload: the intro, the clock, time scale 1, the money.
- **The tier framings are guesses.** The block, district and offer distances (66, 96 and 132 m) may show the edge of the built world.
- **The queue bodies are patrons with their brains and walking turned off.** Their resting pose depends on the animator's default state.
- **APIs I'm unsure compile:**
  - `EditorSceneManager.LoadSceneInPlayMode`;
  - `RuntimeInitializeOnLoadMethodAttribute.loadType`;
  - `TMP_FontAsset.fontWeightTable[7].regularTypeface` at runtime;
  - `logoText.fontMaterial` outline and underlay keywords;
  - `Undo.AddComponent<T>`.
- **PlayLab ignores error lines tagged with its own tag.** The title's single `LogError` (a failed backup) uses `[Start screen]`, so the lab wouldn't count it.

## 7. Left for later
- The night wink: the bins' lid lifting at Quit on a night save.
- The order-ticket menu for the cold open.
- A real logo.
- Music.
- A map of cities once branches exist.
