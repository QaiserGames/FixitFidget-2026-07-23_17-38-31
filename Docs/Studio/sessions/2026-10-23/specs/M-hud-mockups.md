# Designer M — the HUD, three mockups for him to pick from (23 Oct 2026)

You are the UI/UX and art-direction lead for *Fix It Fiasco*. Read `/root/review23/builders-common.md` (§"The situation" and "Readable words") and `/root/review23/his-notes-2026-10-22.md` first. You don't change the game's code; you make pictures and a short write-up. Your copy for reading code: `/root/wt23/M` (read-only for you).

## His note (verbatim, the HUD part)
"I'm also, and this is a huge, huge note, I really wanted to say this, but I am not a fan of the way the HUD is looking. I really want to do a overall change of the way the HUD looks. I am a fan of the way that the close and opening sign kind of like does the shaking. Overall, I would like it to look a lot nicer, to look more unique and different with the opening sign. I just don't like the way that like it just looks like a cluster. 8 p.m. 35 today, franchise 5 stars, and it looks good, but I'd rather the timing be in the middle, on the top middle. And the way it looks, I would like the circle to kind of move as like how it would look like with the sunset, like so it looks like sunrise, sunset, dawn, dusk, and like the sun is actually changing, and not it just being a circle moving from moving like in a half circle. I do like the way that the money icon is showing. However, I would like it to look more juicy, to look better. The franchise 5 stars does look, it just looks a lot clustered. I would like it to look a lot more cleaner. I do like the way that we're adding how prepared drinks and that the logo looks, because that does look so clean. I love that look."

## What's there now
`Assets/Scripts/HudCorners.cs` (built by builder H on 20 Oct) draws one column in the top right:
- the hanging sign (OPEN on paper in brand green #2E7D5B with "Come in"; LAST ORDERS on honey #F0B54C; CLOSED on plum #7D3A44 with "See you tomorrow"), with its strings, sway, flip and shake;
- a shelf with a 96 × 48 sun dial (a dot on a half circle), the time (30 px), the day ("Monday · Day 1", 22 px);
- the takings: a bills icon and "$35" (40 px), "today" (20 px);
- five stars (22 px);
- "Franchise at 5 stars" (20 px).

The Now line (Day 1's guide, the step to do) is top left; tickets also stack top left; the controls strip is bottom centre; the prompt line ("(A) Prepare drinks", which he loves) is lower centre. Photos of today's HUD:
- `/root/review23/photos/Pause_pause-2026-10-20_031303__01-the-corners-by-day.jpg`
- `__20-last-orders.jpg`, `__30-the-sign-flips.jpg`, `__31-closed.jpg`
- `CafeLife_*` (the café from the overhead camera through a Day 1)
- `Stations_*` (first person)
- `Bench_*` (the bench strip mode at the top right)

The HUD must work in all of these:
- the café overhead, day and evening;
- first person;
- the bench close-up (the compact strip);
- the drinks close-up;
- at night: the café's sign is shut, the night has its own hour.

## The brief
Three different, genuinely distinct designs. Each keeps:
- the sign's shake (he loves it);
- the time top centre;
- a sky that really changes through the day;
- the money as the juiciest thing;
- the stars and franchise clean.

1. **Time, top centre, in a living sky.** The sky's colour moves through the day:
   - dawn peach-pink at opening (9 AM);
   - clear morning blue;
   - warm noon;
   - golden hour toward last orders;
   - dusk violet at closing (8 PM);
   - night navy with stars after.

   The sun rises from behind a horizon, arcs, warms in colour as it lowers, and sets; then the moon. Ideas, one per variant or mixed:
   - an **arched café-window** frame with a tiny San Francisco rooftop silhouette (bay-window houses, a hint of the Bay Bridge or a hill) and the time under it;
   - a **wide horizon ribbon** (a pill-shaped band) the sun travels along;
   - a **round porthole or clock face** whose face is the sky.

   The time is big and bold (28–34 px), the day name small.
2. **The sign, top right**, kept as the café's identity: the same states and colours (honey for last orders, plum for closed; never red; no screen tint). It can be refreshed (more unique: hand-painted shop-sign shape, a little brass bracket), but it is recognisably the same sign with the same shake.
3. **The money, right under the sign, juicy.** A pill with the bills icon (he likes the icon) and the number. Show in your mockup how a payment looks:
   - a coin or bill flies in;
   - the number rolls up with a bounce;
   - "+$12" pops and fades.

   Draw a 3-frame strip of the moment.
4. **Stars and franchise, clean.** Five small star pips; the franchise goal as a little flag, key or storefront icon at the end of the row. The words appear only when a star is earned and in the phone's Franchise app. Note the risk: playtest 7 said the franchise goal wasn't apparent, which is why the words were added. Your design must still make the goal readable; say how (e.g. the Day 1 card names it, and the flag animates when a star lands).
5. **Clean, not cluttered:** fewer boxes, more breathing room, one skin (the Baloo 2 font, the band dark #1F1B17 at 0.86, cream text #F7EFE6, brand green, amber #FFB238 for "next", gold for "what's left"). Nothing a player reads under 20 px, the time 28–34 px, contrast ≥ 4.5:1 (over the bright street by day, too).

## The pictures
Make them with Python (PIL, `pip install --break-system-packages pillow` if needed), drawn properly: anti-aliased shapes, gradients, real text in the game's font (`/root/wt23/M/Assets/Fonts/Baloo2/Baloo2-Bold.ttf`, `-SemiBold.ttf`).
- For each variant, a 1920×1080 mockup over a real screenshot of the café by day: `/root/review23/photos/CafeLife_days-1-3-day1-2026-10-20_031410__03-build.jpg` or `02-calm.jpg`.
- **Cover today's HUD column** (top right) and the old dial cleanly: a patch cloned from nearby rooftops, or a soft blur. The point is to see the new HUD without the old one.
- Draw the Now line and the prompt line as they are (or leave them in the photo).

Per variant, also make:
- a **sky strip**: 6 small crops of the time-and-sky element at 9 AM, 11 AM, 2 PM, 6:30 PM (golden), 8 PM (closing, dusk), 11 PM (night);
- the **payment moment** (3 frames);
- the **closing state** (the plum CLOSED sign with dusk), over `CafeLife_*06-closing-time.jpg` or `Pause_*31-closed.jpg`;
- the **bench strip** (how the HUD shrinks at the bench), over `Bench_*07-broken-screen-in-the-tray.jpg`.

Save everything under `/root/review23/mockups/` with clear names (`A-sky-window-cafe.png`, `A-sky-strip.png`, …).

## The write-up
Write `/root/review23/mockups/README.md`:
- the three variants, each in five lines: the idea, why it fits the game, what it costs to build in Unity UGUI (code-drawn sprites and gradients as the project does, or does it need art from his girlfriend, who draws the game's 2D art), and its risk;
- your recommendation and why;
- the questions he must answer to pick.

Return the README as your final message. Look at your own pictures before you finish (open them) and fix what's ugly or unreadable: he will judge them by eye.
