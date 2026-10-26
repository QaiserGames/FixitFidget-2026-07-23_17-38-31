# Designer M2: the HUD, round 2 of design A, "one more level above" (24 Oct 2026)

You are the UI/UX and art-direction lead. Read these first:
- `/root/review24/builders-common.md`: "The situation" and "Readable words";
- `/root/review24/his-answers-2026-10-24.md`: the HUD part, word for word.

You don't change the game's code. You make pictures, short animations and a write-up. Your copy for reading code is `/root/wt24/M` (read it, don't change it). Your progress log: `/root/review24/reports/M2-progress.md`. Your output folder: `/root/review24/designs/M2/`.

## Where round 1 left it
On 23 Oct Designer M drew three HUDs over the game's own photos:
- the README is `/root/review23/mockups/README.md`;
- the pictures are `/root/review23/mockups/*.png`;
- the drawing code (Python and Pillow) is `/root/review23/mockups/src/`: `hud.py`, `sky.py`, `variants.py`, `skin.py`, `patch.py`, `make_all.py`, and the word measurements in `measurements.json`.

**He picked A, the Shop window:**
- an arched café window top centre, its glass a living sky over a small code-drawn San Francisco, with the time under it;
- a painted-board sign top right that keeps the old sign's sway, flip and closing bounce;
- the money under the sign as a green tag;
- five star pips ending in a little café front for the franchise goal.

Start from A's code and pictures. Read M's README in full, including the measurements, so you keep what worked: words ≥ 22 px, the time 32 px, every word measured against the real pixels under it.

## His answers and his note
- **Time at night:** top centre, as drawn.
- **"Franchise at 5 stars" goes.** He chose "enough without it: Day 1's card, a 6 s hint, the star toasts, the phone". Show how the goal stays readable: the café front animates when a star lands; Day 1's card names the goal.
- **The word "today" under the money goes.**
- **The skyline:** the code-drawn one is fine for now (his girlfriend may draw one later).
- **The 4th ticket:** "With the time at top centre, a 4th ticket starts a second row. OK?" His answer: **"No, find another way."**
- His note, verbatim:

  > "for the artwork that we selected for the hud, for example the skyline in the option a, make sure its apparent that we are in san francisco, i like this because we can always change the hud to match the cafe is as of currently during the franchising. I also am not a fan of the way the cash just overall looks, and feels, lets see how other games make their money icon and overall feel with the juice and the animation for these cash logo, and lets replicate it with aces cafe in mind. I feel like the overall idea is getting there, we just need one more revision on this hud that we selected together, to just go one more level above this, and i know it sounds vague, but we are close."

## The brief
1. **Research the money first, with evidence.** He wants verdicts backed by research and numbers.
   - Look at how 10–15 games show and animate money: the icon, where it sits, the count-up, the punch, sounds, particles, the flight from where it was earned, tips or combos.
   - Cover the genre and the best-loved "juicy" ones, for example: Stardew Valley, Animal Crossing: New Horizons, PlateUp!, Overcooked 2, Good Pizza Great Pizza, Dave the Diver, Moonlighter, Cult of the Lamb, Balatro, Two Point Hospital/Campus, Potion Craft, Travellers Rest, Supermarket Simulator, Coffee Talk (what it does instead), Spiritfarer.
   - Add the game-feel talks: "Juice it or lose it" (Jonasson & Purho, GDC 2012), "The art of screenshake" (Jan Willem Nijman, 2013), Steve Swink's *Game Feel*.
   - Use the web: Game UI Database (gameuidatabase.com), Interface In Game, developer posts, GDC talks, Steam reviews that mention the money's feel.
   - For each game: what it does, in timings where you can find or estimate them (ms), and what makes it feel good.
   - Then the principles you take from them, and what fits Ace's café, a small repair café, cozy with a hint of chaos (a tip jar? a till's ding? coins on a saucer? a receipt?).
   - Cite your sources with links in the README.
2. **Redesign the cash, inside A.** Draw 2–3 treatments of the money, each one fully specified, and recommend one:
   - the icon, the figure, the plate (or none);
   - how a payment arrives (from the counter, the bench's hand-back, a guest's table);
   - the count-up, the punch, the colours, the particles;
   - a tip as a bonus beat;
   - how a purchase (spending) looks, as distinct from earning.

   Give timings in ms and easing names, so a builder can make it exactly. Make each one an **animated GIF**, 2–3 s at 30 fps, over the real café photo, so he can feel the juice and not just read about it. Keep his old "I do like the way that the money icon is showing" in mind: he liked the bills icon on 22 Oct and doesn't like how the cash looks and feels now. Say how each treatment answers both.
3. **San Francisco, unmistakable, in A's window.** At the window's real size (about 372 × 161 px for the whole piece), the skyline must say San Francisco at a glance:
   - **Landmarks, some of:** the Transamerica Pyramid, the Golden Gate Bridge's international-orange towers and cables, Coit Tower, Sutro Tower, the Bay Bridge, the Salesforce Tower, the Painted Ladies' bay-window rooflines, hills with a cable car, fog rolling in. Pick what reads at that size; don't crowd it.
   - **Draw it at the six hours** (9 AM, 11 AM, 2 PM, 6:30 PM, 8 PM, 11 PM), lights and fog included.
   - **His franchise idea:** the skyline is the city the café is in, so it changes as he franchises. Show the mechanism: the skyline as one swappable layer, with a second city's skyline as an example. Pick a likely next city and say it's a placeholder.
4. **The tickets, with no second row, ever.** Today, tickets stack top left. With the time top centre, round 1 found three fit and a 4th would wrap.
   - Design a layout that never wraps, at 1, 3, 5 and 7 tickets. For example: overlapping fanned tickets that spread as space allows, compact chips with the most urgent one open, a rail down the left edge, or a "+2" chip. Show each count.
   - Mind how a player reads tickets: which customer, which job, how urgent, the job's colour.
   - Read `Assets/Scripts/JobTicket.cs` and its header for what a ticket must show.
5. **"One level above."** He says the idea is getting there and wants one more level; make it the best HUD a cozy shop game has:
   - **Depth and material:** painted wood, enamel, glass, paper; light from the top left; a soft shadow.
   - **Tiny life:** the sign's sway (kept), steam or a blink in the window, the sky's slow change, a star landing.
   - **One family with the rest of the screen:** the Now line top left, the prompt line he loves ("Prepare drinks"), the controls strip at the bottom.
   - **Restraint:** nothing he'd call "a cluster".

   Show it in the café by day (bright street), late afternoon, last orders, closing, at night (time top centre with stars), first person, and the bench's compact strip.
6. **Readable:** the Baloo 2 font in the copy (`Assets/Fonts/Baloo2/`); the time 30–34 px; nothing a player reads under 22 px; contrast ≥ 4.5:1 against the real pixels under it. Reuse M's measuring code, and measure every word again.

## Pictures and animations
- 1920×1080 PNGs over the real photos M used (the paths are in M's README and `src/`).
- GIFs for the money treatments, the star landing, and the sign's sway at last orders.
- A one-page comparison of round 1's A against round 2.
- Look at every picture and every animation yourself before you finish, and fix what's ugly, unclear or unreadable. He judges by eye.

## The write-up
`/root/review24/designs/M2/README.md`, starting with a phone-sized summary (under 200 words), then:
- the research and its sources;
- the cash treatments and your pick, with why;
- San Francisco and the franchise swap;
- the tickets;
- the "level above" changes, part by part;
- what it costs to build in Unity UGUI (the project draws its UI in code: `UiSkin`, `UiTween`, code-drawn sprites), and what would need his girlfriend's 2D art;
- the few questions he must answer to approve it.

Return the summary and the list of pictures as your final message.
