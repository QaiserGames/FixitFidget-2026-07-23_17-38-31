# Builder H3: the HUD, built in the engine (26 Oct 2026)

You are the Creative Director and senior Unity UI engineer for Fix It Fiasco, a cozy-but-chaotic 3D café-and-repair game (Unity 6000.5.2f1, URP, UGUI drawn in code with UiSkin and UiTween, Baloo 2). The developer judges by playing and by eye, and his bar is high: he called drawn mockups "AI art slop". This time the HUD is built in the game, and he will judge it in his own Day 1.

## Read first
1. `/root/review24/builders-common.md`: the house rules ("Labs", "Unity 6.5", "Readable words").
2. `/root/review24/his-answers-2026-10-24.md`: the HUD part, round 1 and his note (verbatim).
3. `/root/review24/designs/M2/README.md`: round 2's design. Read it in full: its research on money in 15 games with timings, San Francisco in the window, the tickets, and the "level above". Look at its pictures in `/root/review24/designs/M2/` and its code in `src/`.
4. `/root/review24/round3-answers/answers/hud.json`: his round 3 answers. In short:
   - **h1, the money:** "the till". His words: "im only picking till because its the only good option given, however i still am not a huge fan of the work and would like a better revised one".
   - **h2, the tickets:** "No, change it". His words: "i like the way the tickets patience bar is set up, its a circle that circles around their name which is honestly very smart but i still would like everyones ticket to be visibly seen and have nice big bold icons for each item, like a coffee, tea, etc, clock".
   - **h3:** keep the receipt (the pay and the tip in words for about 1.6 s).
   - **h4:** Seattle stays the placeholder next city.
   - **h5:** one till everywhere. Arrange's "in the till" row becomes this money object.
5. `/root/review24/reports/I2-report.md`: the tickets' item icons are now on the sheet. They are `drink_*`, `item_phone`, `item_watch`, `item_camera` and `item_clock`. `NoteIcons.ForDevice`, `JobTicket.DrinkIconName` and `NoteIcons.SpriteOf(name)` cut any sheet icon as a Sprite.

## What to build (his design A, "the Shop window", one level above)
1. **The tickets come first:** his clearest note.
   - Every ticket is visible at once, at 1, 3, 5 and 7 and beyond, and **never wraps into a second row** (his 24 Oct "No, find another way"). A rail down the left edge, compact tickets or a fan all work, as long as every ticket can be read.
   - Each ticket shows its items as **big bold icons**: the drinks, the device, and the clock.
   - Each ticket keeps **the patience ring that circles the customer's name**, which he loves.
   - Read `JobTicket.cs` and `TicketRailUI.cs` for what a ticket must show: who, the job, how urgent, and the job's colour. Keep everything the existing labs check, unless a check measures the old layout. In that case, update the check to the new layout without weakening it.
2. **The money: a better one than "the till".**
   - Use M2's research, plus your own judgment and taste, and make it feel like money in the best-loved cozy games. Ace's café comes first.
   - It's built in the engine and fully juicy:
     - the icon and figure;
     - how a payment arrives, with the flight from where it was earned;
     - the count-up and the punch;
     - a tip as a bonus beat;
     - the receipt for about 1.6 s;
     - spending that looks clearly different from earning.
   - Give the timings in ms with real easing.
   - Arrange mode's till row (ArrangeUI) uses this same money object.
3. **The window time piece, top centre.**
   - An arched café window. Its glass holds a living sky over a small code-drawn San Francisco that's unmistakable at that size: the Transamerica Pyramid, the Golden Gate's towers and so on (see M2).
   - The time sits under it, 30–34 px.
   - The city is **one swappable layer**, with Seattle as the placeholder for the next city.
   - At night the time stays top centre.
   - No goal line and no "today".
4. **The painted sign, top right.** It keeps the old sign's sway, its flip, and its bounce at closing.
5. **The franchise stars.** Five pips ending in a little café front, which animates when a star lands.
6. **"One level above":** depth and material (painted wood, enamel, glass, paper), light from the top left, soft shadows, tiny life, and restraint. Nothing he'd call "a cluster".

## Rules
- **Readable:** Baloo 2; words ≥ 22 px (the house's checks use 26); contrast ≥ 4.5:1 against the real pixels behind.
- **Players:** his girlfriend is dyslexic and plays on a PlayStation pad.
- **One family:** the HUD keeps a family look with the Now line top left, the prompt line he loves, and the controls strip.
- **The bench close-up** has its own compact strip; keep one.
- **Files:** you own HudCorners.cs, JobTicket.cs, TicketRailUI.cs and the money's UI, plus any new files you add.
  - Small, marked edits elsewhere only where you must: ArrangeUI's till row, and the places where payment events come from.
  - Mark every edit `// 26 Oct 2026 (HUD 3)`.
- **Labs:** every existing HUD-touching lab must still pass. Grep PauseCheck, the Day 1 guide check, the Recap phone check, Juice and anything that reads HudCorners or the tickets.
  - Add a lab, "Fixit Fidget › HUD › Lab - The HUD at every hour (drives itself)". It photographs the HUD by day, late afternoon, last orders, closing and night, in first person, at the bench, with 1, 3, 5 and 7 tickets, and with a payment, a tip and a purchase mid-animation.
  - Its last line is "[HUD] All N checks passed." or "[HUD] N of M checks FAILED. <folder>".
- **Code:** nothing can be compiled here. Grep every project member you call, and run `python3 /root/tools/csparse.py` on every changed file. Commit in your copy, `/root/wt24/H` (branch work-H).

## Report
Write `/root/review24/reports/H3-report.md` and return it, in under 700 words:
- what you built, part by part, with the money's spec in ms;
- the files;
- the steps and labs to run on his Mac, in order;
- what a pass looks like;
- the decisions he may want to overrule;
- the risks.

Don't use any remote-device or Mac tools, and don't spawn agents.
