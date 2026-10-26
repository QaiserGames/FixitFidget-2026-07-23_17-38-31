# Builder S2: the start screen, built in the engine (26 Oct 2026)

Read `/root/review24/builders-common.md` first (the house rules, especially "Labs", "Unity 6.5" and "Readable words"). Then read `/root/review24/designs/R4/start-research.md` §3 (Overcooked), §4 (the patterns) and §5 Seeds 1 and 6.

**Your copy:** `/root/wt24/S`, branch `work-S`, from today's integration tree.
**Your report:** `/root/review24/reports/S-report.md`, with a progress log in `/root/review24/reports/S-progress.md`.

## Why, and what he said
- **He rejected three drawn concepts** (a till roll, a cassette deck, a scene by torchlight). His words: "i hate all the starting scenes… remember this follows ace life as he franchises. it's not just set on grace or san francisco, or a random boombox or whatever. it needs to fit the cozy but chaotic theme. let's see how over cooked does it".
- **He also called drawn mockups "AI art slop".** So this time we build the real thing in Unity, from the game's own scene, and he judges it by playing it: "ill trust your judgement and will assess how it looks once finished".
- **Claude's pick from the research:**
  - **Seed 1, "The Diorama That Grows",** every time a save exists.
  - **Seed 6, "Rush-Hour Cold Open",** only the very first time (no save yet).
- **Never use:** Grace; anything that's only San Francisco; a recorder, boombox or tape.

## What the player sees

### A) A save exists: Seed 1, the diorama
- **The scene** is the café exactly as saved: its layout (CafeLayout, already applied at boot) and its time of day's light.
- **Live but harmless.**
  - Settled guests and walk-in patrons come and sit. The street's walkers and cars move.
  - No customers come to the counter. No money changes. The clock doesn't run. Nothing is saved.
- **The camera** is the overhead rig, pulled high, slowly orbiting the café: about one turn every 120 s, easing, never jerky.
- **The franchise follows Ace.** How high and wide the camera sits comes from the save's progress, kept in one small table, one step per franchise tier:
  - first days: the café and its street;
  - the block;
  - the district;
  - the franchise offer: the widest view.

  Read the tier from what the phone's Franchise app already uses (stars and reputation; grep `BuildFranchise`). Branches don't exist yet. Leave a clear hook for "a map of cities" when they do.
- **The menu** is bottom-left. Each item is one word:
  - **Continue,** with a sub-line "Day 3 · <the tier's name from the Franchise app>";
  - **New Game;**
  - **Settings;**
  - **Credits;**
  - **Quit.**
- **The logo** is top-left: "Fix It Fiasco" in Baloo 2 ExtraBold, cream with a thick dark outline and a soft warm shadow, tilted a few degrees. Mark it as a placeholder for his girlfriend's logo.
- **Continue:** the camera dives toward the café door in about 1.2 s (ease in), then a fade. Reload the scene and boot normally, exactly today's path: the save loads and the morning intro plays as now.

### B) No save at all: Seed 6, the cold open (first start only)
- **The scene** is a glimpse of the future: today's full café (the 28-seat layout, format 0, held in memory and **never written**) in mid-rush.
  - Most seats are taken by patrons, there's a queue at the counter, and the drinks machine steams.
  - Ace stands behind the counter, idle.
  - Use a livelier, lower 3/4 angle than the diorama's.
- **After about 3 s** of live bustle, everything **freezes** (time scale 0).
  - The logo slams in like an order ticket: drops, squashes, settles in about 300 ms.
  - A warm grade tints the frozen frame.
  - The menu appears, without Continue.
- **New Game** plays **"Before it got like this."**: a 1.5–2 s fast-rewind effect, made as a UI overlay.
  - Streaks or scanlines, a "◀◀", and those words.
  - The crowd fades out, then fade to black.
  - Then the real new game: the half-finished café, Day 1, and its intro as today.
- **The next start** finds a save, so it's Seed 1 from then on.

## Feel and look (the Overcooked lesson: a calm menu with the chaos one beat away)

**Slabs**
- Chunky rounded slabs: cream fill `#F7EFE6`, a thick dark outline `#1F1B17` (about 4 px at 1080p), and a soft drop shadow. Draw them in code with UiSkin, as the project does.
- Words are Baloo 2 Bold, 34–40 px.

**The selected slab**
- It pops and squashes: scale 1.0 → 1.08 → 0.97 → 1.0 over about 180 ms, ease out back (UiTween).
- Its fill turns honey `#F0B54C` with dark words, keeping contrast ≥ 4.5:1.
- It plays a soft click from the existing Sfx sounds. Pick the nicest existing one, and list it as a placeholder.

**Rules**
- Nothing under 22 px.
- Contrast ≥ 4.5:1 against the real pixels behind.
- A controls strip at the bottom uses ControlHints' icons ("✕ Choose · ○ Back"), never "[E]".

**Input**
- Pad: up and down to move, ✕ to choose, ○ for back.
- Mouse: hover and click.
- Keyboard: arrows, Enter, Esc.
- His girlfriend is dyslexic and plays on a PlayStation pad, so the first item is selected the moment the menu appears.

**Menu items**
- **New Game with a save:** a confirm card, "Start a new café? Your café now is kept as a backup.", with [Start new] and [Keep playing].
  - Before anything changes, keep a copy of the save the way the editor's "Fresh game 1" menu does (Assets/Editor/FreshGameMenu.cs). Write the runtime-safe part as a small runtime helper, and leave the editor menu working.
- **Settings:** opens the game's existing Settings (the phone's Settings app) over the title. Back returns to the menu.
- **Credits:** a simple card: "Fix It Fiasco", "Made by Mansoor Qaiser", and a placeholder line for the art credit. Don't invent names. Back returns.
- **Quit:** `Application.Quit()`. In the editor it stops Play.

## Safety: the hard rules
- **The title never writes or changes any save.**
  - Guard SaveManager's writes while the title is up.
  - Continue and New Game leave the title by reloading the scene, so nothing from the title's sandbox leaks into the day.
- **The title never shows in any lab or lab session.** That covers CafeLab.Active, every PlayLab, and anything started from a "Fixit Fidget" lab menu.
  - Put the rule in one place, `TitleScreen.ShouldShow`.
  - Add a dev toggle in the editor: "Fixit Fidget › Start screen › Show it in the editor's Play (on/off)", default on, kept in EditorPrefs.
- **Every existing lab must run exactly as before.** Grep how labs boot: PlayLab, CafeLab, `StartDaysLab`, the Fresh game menus.
- **Time scale.** Nothing the title does may leave time scale at anything but 1 when the day starts.

## Building it
- **New files:**
  - `Assets/Scripts/TitleScreen.cs`: the mode, the flow and the camera. Add the UI too, or put it in a `TitleMenuUI.cs`.
  - `Assets/Editor/StartScreenSteps.cs`: one step, "Start screen 1 - Put the start screen in the café scene (scene, with Undo)", in the house's step style, ending with a DONE line.
  - The lab below.
- **Small marked edits** (`// 26 Oct 2026 (Start screen)`) only where you must, e.g.:
  - `GameBootstrap.cs`: the gate;
  - `SaveManager.cs`: no writes while the title is up, plus the backup helper;
  - `DayIntro.cs` / `CafeViewMode.cs`: hold off while the title is up.
- **Ownership:** HudCorners, RecapPhone (except opening its Settings app), the furniture and Days scripts belong to others. Read them, don't change them, unless you must; then mark the edit and list it.

## The lab: what proves it on his Mac
**"Fixit Fidget › Start screen › Lab - The start screen (drives itself)"**
- It runs in the lab's own save folder and never touches his save.
- It checks Seed 1 with a lab save:
  - the title shows;
  - the first item is selected;
  - the Continue sub-line is right;
  - the camera moves (the orbit);
  - no save file is written while the title is up;
  - Continue reloads into the day at its checkpoint, with time scale 1.
- It checks Seed 6 with no save:
  - it freezes within 4 s;
  - New Game plays the rewind;
  - Day 1 begins in the half-finished café, with the intro.
- It checks words ≥ 22 px and contrast with the house's own text checks.
- It takes photos: `01-diorama`, `02-menu-selected`, `03-cold-open-live`, `04-cold-open-frozen`, `05-rewind`, `06-day-1`. He judges by eye, so frame them well.
- Its last line is "[Start screen] All N checks passed." or "[Start screen] N of M checks FAILED. <folder>".

## Check before you finish
- Run `python3 /root/tools/csparse.py` on every file. Nothing can be compiled here, so be careful with Unity APIs and check every project member you call by grepping its definition.
- Commit in your copy.
- **Be economical:** he asked us to preserve usage. Grep before you read, and don't re-read.
- Don't use any remote-device or Mac tools, and don't spawn agents.

## Report (under 700 words)
- The files.
- How each part works.
- The steps and labs to run on his Mac in order, with what a pass looks like.
- The decisions he may want to overrule.
- The risks.
- What's deliberately left for later: the night wink (the bins' lid lifting at Quit on a night save), the order-ticket menu for the cold open, a real logo, music.
