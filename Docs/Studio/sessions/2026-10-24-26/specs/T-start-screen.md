# Designer T: a start screen in the spirit of Resident Evil 7's (24 Oct 2026)

You are the UI/UX lead, with the Narrative & Game Design director's hat on. Read these first:
- `/root/review24/builders-common.md`: "The situation" and "Readable words";
- `/root/review24/his-answers-2026-10-24.md`: his note on the calls, word for word.

You don't change the game's code. You research, then make pictures, short animations and a write-up for him to pick from. Your copy for reading code is `/root/wt24/T` (read it, don't change it). Your progress log: `/root/review24/reports/T-progress.md`. Your output folder: `/root/review24/designs/T/`.

## What he asked (verbatim)
> "We haven't done it yet, but i would also like there to be a start screen now, and i was playing resident evil 7 yesterday and i really really liked theirs, its a simple recorder that just scrolls down and says new game, load game, save game, etc all that stuff, you know, and i was thinking maybe we can do something like that but that fits the cozy chaotic cafe repair game where you franchise and also are sneaky ahaha, i know this game is a mouthfull, but i love the work we are doing"

## What the game has today
There is no start screen. The game boots straight into the café scene (`Assets/Scripts/GameBootstrap.cs` applies the save; `SaveManager.cs` holds one save, `playtest-aces-cafe.json`, written at the recap and when the next day starts). The morning intro plays (`DayIntro.cs`), then the day begins.
- **A new game** today is an editor menu ("Fixit Fidget › Playtest › Fresh game 1 - Start a new game (keeps your save as a copy)"; "Fresh game 2 - Bring my saved game back").
- **Settings** live in the pause phone (`PausePhone.cs`, `RecapPhone.cs`'s Settings rows, `GameSettings.cs`).
- **The skin:** `UiSkin.cs` (the Baloo 2 font in `Assets/Fonts/Baloo2/`; band dark #1F1B17, cream #F7EFE6, brand green #2E7D5B, amber #FFB238, honey #F0B54C, plum #7D3A44), and J's motion helper `UiTween.cs`.
- **The game's two faces:** the cozy-chaotic day (the café, repairs, drinks, the franchise goal), and the sneaky night (the man at the bins, the torch, sneaking into 12 West Street, the notebook).
- **The intro line** for Day 1 is becoming "Years at a coffee cart bought this place. Now make it a café." (the furniture build).

## The brief
1. **Find out exactly what he liked in Resident Evil 7's start screen.** Research its title and main menu:
   - what it looks like, and what the "recorder" is;
   - how the list scrolls, how it moves and sounds;
   - which items it has (Continue, New Game, Load Game, Bonus, Options, Exit...), and how it handles saves (the cassette tape recorders as save points);
   - why players and critics liked it.

   Use Game UI Database (gameuidatabase.com), Interface In Game, videos' descriptions, articles and Steam discussions. Describe it precisely, with sources. If you find the screen differs from his memory, say what it is and what he probably meant.
2. **Look at start screens that fit a cozy, chaotic, sneaky shop game**, for the contrast and what translates:
   - Untitled Goose Game (sneaky and cozy at once);
   - Stardew Valley, PlateUp!, Spiritfarer, A Short Hike, Dave the Diver, Cult of the Lamb (cute by day, dark by night), Coffee Talk;
   - one or two more you find.

   Note what each does in a line.
3. **Three concepts that turn RE7's idea into Ace's café:** one simple physical object whose list scrolls, cozy-chaotic by day with a sneaky edge. Some starting points (pick or replace them; make the three genuinely different):
   - the café's **till roll or receipt printer** that prints the menu as it feeds;
   - a **cassette recorder or old radio on Ace's bench** (his repair trade, and RE7's tape recorders), its counter clicking through the items;
   - the **order-ticket rail** where tickets slide;
   - **Ace's notebook** (the night's notebook);
   - the **chalk menu board**.

   For each concept:
   - **The look:** a 1920×1080 mockup with the logo ("Fix It Fiasco"), the list, and the background (the café from the street at dusk? the counter close-up? half day and half night?).
   - **How it moves:** an animated GIF of scrolling through the list, 2–4 s at 30 fps.
   - **How it's driven:** pad (up/down, Cross to choose, Circle to back) and mouse (hover, click, wheel), with the button icons as the game shows them.
   - **The items, mapped to what the game really has:** Continue (only with a save), New Game (with a "this replaces your café" confirm, and a backup kept as the editor menu does today), Settings (the same settings as the phone), Credits?, Quit. Say what his "load game, save game" means for a game with one save that saves itself. Propose, but don't invent save slots unless you argue for them.
   - **The day/night duality:** how the screen hints the sneaky side, e.g. the background or the object turns from day to night as the cursor moves, the torch, the man at the bins peeking.
   - **Sound cues** (described; he'll make the sounds later).
   - **What it costs to build:**
     - a boot scene, or an overlay in the café scene before the intro;
     - Unity UGUI code-drawn as the project does (`UiSkin`, `UiTween`), or a 3D object;
     - what needs his girlfriend's 2D art or a Blender model;
     - the risks (loading the café behind it, the save, the pad).
4. **Your recommendation**, why, and the few questions he must answer to pick.

## Readable and in the house style
- Baloo 2.
- Menu words 30–40 px at 1080p.
- Nothing under 22 px.
- Contrast ≥ 4.5:1 against the real pixels behind.
- It must work for his girlfriend: dyslexic, on a PlayStation pad.

Make the pictures in Python (Pillow; `pip install --break-system-packages pillow` if needed). Draw properly: anti-aliased shapes, gradients, real text in the game's font. For backgrounds, the game's own photos are in `/root/review23/photos/`: the café from the street, the night, the bench. Look at every picture and GIF yourself before you finish, and fix what's ugly or unreadable.

## The write-up
`/root/review24/designs/T/README.md`, starting with a phone-sized summary (under 200 words), then:
- RE7's screen, described with sources;
- the references;
- the three concepts;
- your pick;
- the questions.

Return the summary and the list of pictures as your final message.
