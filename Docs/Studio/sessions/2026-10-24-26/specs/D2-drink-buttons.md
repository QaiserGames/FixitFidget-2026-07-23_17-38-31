# Designer D2: the drinks machine's buttons, three designs for him to pick from (26 Oct 2026)

You are the Art Direction and UI/UX lead. Read these first:
- `/root/review24/builders-common.md`: "The situation" and "Readable words";
- the drinks machine part of `/root/review24/his-answers-2026-10-24.md`.

You don't change the game's code. Your copy for reading is `/root/wt24/D` (read it, don't change it). Your output folder is `/root/review24/designs/D2/`.

**Be economical.** He asked us to preserve usage. Aim for three strong pictures, not a gallery.

## What he said (26 Oct, verbatim)
"the drink machines button, lets get one thing also noted, i am really not a fan of the way the buttons look, like lets be honest in a sense of a cozy chaotic game, its just words on a square in different colors, it doesn't give aces aesthetics you know."

The art audit's a10 had found the six labels auto-sized to different sizes, with HOT CHOCOLATE the smallest (`/root/review23/decisions/art-items.json`, a10, and its pictures in `/root/review23/decisions/img/a10-*.jpg`). He wants more than a size fix: buttons that look like Ace's café.

## What's there
- **The machine and its six drinks** (Americano, Espresso, Latte, Coffee, Tea, Hot Chocolate). In your copy, grep `BeverageStation`, `BeveragePaddleMotion`, `BeverageSlot`, `BeverageControl`, `DrinkLiquidVisual`, and the machine's prefab and objects in `/root/ref24`.
- **How the player uses it:** first person at the machine, the aim help's bigger targets there (c2), and D's prompt line ("(A) Pour Latte").
- **Photos:**
  - `/root/review23/photos/Stations_*` and the dispenser-frame photos;
  - on the Mac's 23 Oct run: `/mnt/user-data/uploads/FixitFidget/Logs/...` (a few).
- **The game's six drink icons** are already drawn, in the button icon sheet: `Tools/UI/draw_button_icons.py` in your copy. Load it read-only.
- **The café's look:** sage joinery, oak, brick, mustard, brass. See the art-style notes in `Docs/Studio/BIBLE.md` and the café kit's colours in `Tools/Blender/fixit_blender.py`.

## The brief
Three different designs for the six buttons. Each one:
- reads at a glance for his girlfriend (dyslexic, on a PlayStation pad), with the drink's picture, never only colour or only a word;
- is chunky, friendly and a little chaotic, like the rest of the café;
- looks like Ace built or fixed it (he's a repair man);
- keeps the machine's working parts: the pour animation, the paddle motion, and the slots under it.

Ideas to start from (pick or replace them):
- enamel push-buttons with the drink icons, in brass bezels;
- hand-lettered tape labels;
- a diner-style row of lever paddles with icon caps;
- a chalk menu strip above the paddles with little light-up icons.

For each design:
- a mockup painted over the machine's real first-person photo (1920×1080), plus a close crop;
- how it looks pressed and while pouring;
- what it costs to build: a Blender model change (the machine is built by a script in `Tools/Blender/`, or a pack model; find which), materials, TMP labels or sprite icons;
- the risks.

Recommend one, and say why.

Make the pictures in Python with Pillow, as the other designers did (see `/root/review24/designs/M2/src/` for a drawing kit you can reuse read-only). Look at each picture yourself and fix anything ugly or unreadable.

## Write-up
`/root/review24/designs/D2/README.md`:
- a phone-sized summary (under 120 words);
- the three designs;
- your pick;
- one or two questions for him.

Return the summary and the file list as your final message.
