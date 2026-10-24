# His playtest of 22 Oct (night): what's behind each note, my take, and the plan

*23 Oct 2026, 01:30 Chicago. Written by Claude as the Producer, before any work: his project rule is that everything is discussed first. Facts come from his own day logs on the Mac (`DayLogs/AcesCafeLayout/`), the code at `8033112`, and the labs' photos of 20 Oct. Nothing has been changed yet.*

> **Read with his answers (01:45 Chicago, at the end of `his-notes-2026-10-22.md`). Where they differ from this plan, his answers win:** Days 1 AND 2 are the teaching days (not "Day 2 is a real day"); "dead air" means the things to do while waiting for a customer, and he chose all four (side project, clearing tables, chatting with seated guests, restocking by hand); the café grows (no playable cart); seats count, décor is looks; playtest 8 comes after the furniture is in.

## What he said (grouped, his words where it matters)

**Loved:** the bench's help ("if you ever get lost whenever doing a repair, it kind of like highlights for you"); the night's circular sneak mode ("I really, really, really enjoy"); the "Prepare drinks" prompt with its icon ("that does look so clean. I love that look"); the open/closed sign's shake.

**Broken or off:**
1. The bench with the phone: "the tray on the right and the bottom of the mat kind of look off".
2. Coats on the café's chairs "just look like squares and rectangles together".
3. At night in first person, the yellow "You" bounces ("it's looking for it in the isometric view").
4. Aim help at the drinks machine: "I'm like locked to the cup... it's like fighting me".
5. The drinks station's prompt "looks really clustered... blocky and big".
6. Drink customers "only sit to the left of the couch"; repair customers "only sit on the right next to the bar stool".

**Wants:**
7. An agent to flag "any bad asset that's in the game... so that we can either take it out, edit it, or work on it".
8. Days 1–3 with less downtime: "I just wanted to stop after day two... the game leveling has to show... a lot better in the beginning".
9. (An idea, "you can bait me on this") Ace starts with a coffee cart, saves, buys the café; the tutorial happens at the cart.
10. Furniture the player buys and places: "tables, chairs, where the NPCs can sit down... their own freedom... simulation definitely sells".
11. Night notes with pictures: "it's just a cluster of words, it's word vomit".
12. Juice on the phone: "when moving to different like buttons or moving down to a different category... I want there to feel good".
13. A HUD overhaul: keep the sign's shake; the time top middle; a sun that really rises and sets ("sunrise, sunset, dawn, dusk"), not a dot on a half circle; the money "juicier"; "Franchise at 5 stars" less cluttered.
14. Agents that are thorough and double-check, and questions brought to him.
15. Sounds and music: he'll do them himself later.

## What the game and his own logs say

- **His Day 1 tonight:** 7 customers in 7 minutes: 2 repairs (Grace's camera, Nina's phone), 5 drinks. They arrived 30–86 s apart, so rarely did two need him at once: serve one, wait for the next. All Perfect, all "Loved it", $354.
- **Day 2 repeats Day 1.** Its only walk-in repair is the same gunked-up phone as Day 1's, plus six drinks. The watch and the cracked screen first appear on Day 3.
- **Then a spike.** His own Day 4 (20 Oct): 12 customers, 3 walked out of the queue. Three flat days, then a cliff.
- **The upgrades don't show.** Six of them, $100–$700, all tool speeds and capacity (Magnetic Driver, Wide Brush, Faster Machine, Bigger Bench, Shelf Space, Bulk Restock). He can afford one after Day 1, and nothing in the café looks different when he buys it.
- **Seats** (`WaitingArea`): a drink customer takes the *nearest* free seat to the counter. That has been the rule since 27 Sept: before that it was random, and people walking past empty chairs to the far side "read as automated". A repair customer takes the repair bar's stool nearest the bench, middle first: that came from her playtest 5 note on 14 Oct. So the same three or four seats get used all day.
- **"You" at night:** the night asks for one "You" pin over Ace for its first seconds. It never checks the view, so in first person it sits at the edge of the screen and bobs.
- **Aim help in first person** (`AimAssist`):
  - near anything usable, the view turns at 40 % of its speed;
  - within 7° it pulls toward that thing's middle at 22°/s;
  - let go of the stick within 4° and it settles onto the thing.

  At the drinks machine, the cup stack, the cup in hand, four nozzles, the paddles and the buttons sit a few degrees apart. So it never lets go: that's the "fighting me".
- **The drinks prompt:** a dark panel 720 × 210 px at 1080p, with four lines: "Cup stack / Take empty cup / [LB] left hand [RB] right hand / [A] use".
- **The bench with the phone:**
  - the back cover's well is the only black tray (the others are aluminium);
  - it runs off the mat's right edge and off the screen;
  - the new screen's pad overlaps it;
  - the screen cuts the mat's bottom edge and its ruler.
- **The coats:** "a coat on a chair's back", from the lived-in dressing pass. The coat stand is "a coat rack with two squares (its scarf and Ace's tote)". Both are built from boxes.
- **The phone:** apps switch instantly. Nothing on it animates except the money counting up.

## The market, for the two big questions

- **How long people give a demo.** Across 130 Steam demos, the median player played **14 minutes**. The top earners' demos held 38–65 minutes. No group's median reached the 30 minutes developers designed for ([How To Market A Game](https://howtomarketagame.com/2022/10/26/what-is-a-good-median-play-time-for-a-demo-benchmark/)). Our agreed demo is Days 1–3. **Most players will only ever see Day 1**, so Day 1 has to be the hook.
- **What the shop-builder sims that sold have in common.** Start small, buy furniture, place it yourself, expand:
  - **Supermarket Simulator** peaked at 38,056 players at our exact price, $12.99. That was the 11th biggest launch of 2024 by players. Its loop is "installing shelves and other furniture... expanding store space" ([WN Hub](https://wnhub.io/news/stores-and-publishing/item-43033)).
  - **TCG Card Shop Simulator**, one solo developer, sold 100K copies in three days and has reached over 4 million players. Shop revenue pays for "shop expansions, decorations" ([Outlook Respawn](https://respawn.outlookindia.com/gaming/gaming-originals/the-solo-dev-who-built-a-card-shop-game-and-watched-it-explode)).
  - **PlateUp!**, where you build the restaurant's layout, sold 1.5 million copies on Steam ([Game Developer](https://www.gamedeveloper.com/press-release/plateup-reaches-1-5-million-steam-copies-sold)).

  His instinct that "simulation sells" when the player builds the place is right, and the data backs it.

## My take, note by note

**1. The phone's tray and the mat: agree, a layout fix.**
- Give the back cover an aluminium well like the other trays, small enough to sit wholly on the mat.
- Move the new screen's pad so nothing overlaps.
- Frame the close-up so the mat's bottom edge and ruler read as a border, not a cut.
- The bench's view lab (Bench 5) checks the framing; its photos go to him.

**2. The coats: agree, take them out now.** The coats on the chairs and the coat stand's two squares go now. A real draped coat is a Blender job for later, only if he wants coats back. The art audit (7) will likely find more like these.

**3. "You" in first person: a bug.** It shows only when the overhead view is on, as Day 1's "You" already does.

**4. Aim help: agree it's too strong.** Four changes:
- It fades out as the stick is pushed harder: full help for a small nudge, none at full tilt.
- It pulls only toward what's ahead of the push, never sideways or backwards.
- The cup in Ace's hand is never a target.
- It is lighter at the drinks machine, where targets crowd.

A new lab measures it, from the cup in hand to the espresso nozzle on a scripted pad: the time it takes, and the frames where the view moved against the stick (which must be zero). One option for him: make the setting Light / Strong / Off instead of on/off, with Light as the default.

**5. The drinks prompt: agree.** It becomes the style he likes: one line with the button icon ("(A) Take a cup"), with the hand choice as two small icons only while it matters. No dark panel, no four lines. The thing's name sits beside it like the bench's "New screen" label.

**6. Seats: agree, but not random.** Random is what we had before 27 Sept, and it looked robotic. People in a café pick seats for reasons, so seats should be chosen the same way:
- **By character.** Some like the window, some the lounge, some the bar, some the long table.
- **By personal space.** People take the seat farthest from strangers when the café is quiet, as people really do, and fill in when it's busy.
- **By habit.** Regulars have a favourite seat: Grace by the window. That's character for free.
- **Repair customers mostly still take the repair bar** (her playtest 5 note, and reassuring from the bench depends on it), but some, by character, take a comfy seat elsewhere. Then you carry their device to them.

That's the "look for the customer" he wants. The ticket's name and the name pin already help find them. The Day labs count how many different seats a day uses.

**7. The art audit: agree, and it's the right first job.** It needs pictures of everything, so two sets:
- A photo tour: every prop group in the café, the street and the night houses, from the game's own overhead and first-person cameras.
- A model sheet of every model (the tool exists: `Model sheets 2`).

Art-direction reviewers rate every asset against the style (warm, flat-shaded, chunky) and the hero-item rule (what you see up close carries real detail). The result is a list with a picture per line: **keep / fix / replace / remove**, with the cost of each. He says yes or no per line; nothing is removed without his yes.

**8. Days 1–3: agree completely, and this is the most important note.** With a 14-minute median demo, Day 1 is the game for most players. The proposal:
- **Day 1 teaches everything once, in 7 minutes:**
  - the counter;
  - Grace's camera at the bench;
  - a walk-in repair;
  - two or three drinks;
  - the hand-back;
  - closing.

  The first purchase comes at the end of Day 1.
- **Day 2 is a real day:** a new device (the watch), customers who overlap (two waiting while you work), a rush in the afternoon. Never the same walk-in repair as Day 1.
- **Day 3 brings a new twist:** Grace's swap, the cracked screen, the first real rush, and the first star within reach.
- **No dead air.** When the counter is empty and nobody is waiting, the next customer comes sooner (the day still ends on time).
- **More repairs, fewer plain coffees** in Days 1–3. The bench is what he loves, so it shouldn't be rationed.
- **Something new every day, said in the recap** ("Tomorrow: your first pocket watch").
- **One option, flagged** because it touches the "no new mechanics" lock: the mortgage he mentioned is in the story but not the game. A payment due at the end of the first week, shown on the phone, would give Days 1–3 a goal and make the money matter from Day 1.

The Progression & Economy lead writes it day by day; he approves it before anything is built; the Day 1–3 labs and the playtester agents check it.

**9. The cart: the idea under it is right, but I'd push back on building it.**

*Why it's right:* "from a coffee cart to a franchise" is a great arc. It makes the growth he wants visible from the first minute. It's the "builds his repair/café store to becoming a whole building" of his own project brief.

*Why I'd build it differently:*
- **It teaches coffee first.** Coffee is the part that bored him. The repairs are the hook, and a cart delays them further.
- **It's a new location before Day 1.** New art, a new layout, a new tutorial and new tuning, before the three days we already have are fun. The demo players who give it 14 minutes would spend them at the cart and never reach the café.
- **The story lives at the café.** The man at the bins, the back door, Grace's street: a cart chapter needs its own nights or none.

*The same feeling, at a fraction of the cost:*
- **The café grows with you.** Ace has just bought a half-finished café with a mortgage. On Day 1 it's the counter, the bench, the drinks machine and the repair bar; the seating area is under dust sheets and boxes.
- **Every table, chair, sofa, lamp and plant is bought from the phone's Shop and placed by the player.** Each seat lets one more customer stay.
- **That's note 10 and the "leveling has to show" note in one feature.** It's Supermarket Simulator's loop in our café.
- **The cart lives in the intro:** twenty seconds of "years of selling coffee from a cart to afford this place", with the day-intro tech we already have.
- **If he still wants a playable cart later,** it works as a short prologue once the café's days are fun, or as a mode after launch.

**10. Furniture: agree, with one decision for him.**
- **The catalogue already exists:** 24 pieces in two tiers (Workshop, and Marble & Brass), built in August for "the between-days shop", same footprints so a Tier 2 upgrade swaps in place.
- **His 9 Oct note** made customization "just cosmetic", and "layout is cosmetic, not systemic" is a locked decision. Tonight he says the NPCs sit on what he places, which makes seats systemic.
- **My recommendation:** seats count, décor is looks. Every seat you place is one more customer who can stay; lamps, plants and rugs are yours to arrange, with no stats to optimize. That keeps it a café you shape, not a spreadsheet.
- **How it works:** an "Arrange" mode from the overhead view. Pick a piece, see its ghost snap to the floor, rotate it, green or red for fits or not, place it. Move it or sell it back later. The walking paths re-carve around it. Customers find whatever seats exist.
- **What it touches:** the café's lived-in dressing (cups and laptops tied to named seats), the labs that know seats by name, the NavMesh. It's the biggest feature since the scope freeze, so it gets a spec and mockups first, and probably its own session to build.

**11. Notebook pictures: agree.**
- **Each note becomes a small picture plus a few words.**
- **The pictures are "photos" the game takes of its own models:** the gnome, the yellow house, the man at the bins, Grace's cups. Pinned in like polaroids, so every new note gets one automatically.
- **If his girlfriend would like to draw them later** (she does the game's 2D art), her sketches drop into the same frames.
- **A place gets a tiny street map** with a pin (12 West Street).

**12. Phone juice: agree.** All of it lasts 0.2 s or less so it never slows him. The recap and pause labs wait for it to settle.
- apps slide left and right;
- the focus ring glides from button to button instead of jumping;
- a pressed button squishes and springs back with a tap sound;
- a tab's underline slides;
- rows cascade in;
- the pad gives a light rumble on a press.

**13. The HUD: agree, and mockups before code.**
- **Top centre: a small sky window** with the time under it, big.
  - The sky's colours move through the day: dawn peach, morning blue, golden hour, dusk violet, night navy with stars.
  - The sun rises from behind a tiny San Francisco rooftop line, arcs and sets; then the moon.
  - That's the "sun actually changing" instead of a dot on a half circle.
- **Top right: the sign** with its shake, and the money right under it as a juicy pill:
  - the bills icon;
  - the number rolling up;
  - each "+$5" flying into it with a coin chime.
- **The stars become five small pips under the money**, with the franchise as a little flag at the end of the row. The words "Franchise at 5 stars" show only when a star is earned and in the phone. The goal stays visible without the clutter.
- **One risk to watch:** playtest 7 asked for the franchise goal to be visible, and words were how we did it. Playtest 8 checks it still reads.
- **I'll render three versions over a real screenshot of the café**; he picks (or mixes) before a line is written.

**14. Thorough, double-checked: agreed, and here's the process.**
- **Each change gets a builder,** then a **separate reviewer agent** who has not seen it built and checks it against the spec.
- **Then the labs on his Mac.**
- **Then the two playtester agents** (the first hour: does it read; the veteran: does it feel right) go over the integrated build's photos **before he plays.**
- **Big design work** (pacing, furniture, seats, HUD) runs on Fable 5.1; routine fixes on Opus 5.5 (Bible §3.7).

**15. Sounds and music: parked, his.**

## The plan (one ordered list)

0. **Him: push from GitHub Desktop on the Mac.** GitHub still ends at `41f2f1a` (13 Oct). His `d9ee8be` and `8033112` are only on the Mac.
1. **Quick fixes**, one at a time in Unity, each reviewed, each with its lab:
   - "You" in first person;
   - aim help;
   - the drinks prompt;
   - the coats out;
   - the phone's tray and the mat.
2. **The art audit** (alongside 1; read-only): the photo tour and the model sheets, then a list with pictures for his yes or no.
3. **Seats chosen like people choose them.**
4. **The notebook's pictures.**
5. **The phone's juice.**
6. **The HUD:** three mockups, then his pick, then the build.
7. **Days 1–3:** the day-by-day spec, then his OK, then the build.
8. **The café grows with you** (buy and place furniture): the spec and mockups, then his OK, then the build. It probably gets its own session.
9. **The playtester agents** on the integrated build, then he plays, then **playtest 8 with her, recorded.**

**Parked:** a playable cart (the intro carries the story); sounds and music (his); the held cloth and a sofa (Blender).

## Decisions for him

1. **The cart:** grow the café instead (recommended), build the cart prologue, or park both until after playtest 8.
2. **Days 1–3:** teach everything in Day 1 and make Days 2–3 real days (recommended), keep three gentle days with less waiting, or drop the intro days.
3. **What furniture does:** seats count, décor is looks (recommended; this changes the 9 Oct "cosmetic" lock), all cosmetic, or every piece with stats.
4. **When playtest 8 happens:** after the fixes and the pacing (recommended; furniture follows), or after the furniture too.

Sources: [How To Market A Game: median demo play time](https://howtomarketagame.com/2022/10/26/what-is-a-good-median-play-time-for-a-demo-benchmark/) · [WN Hub: Supermarket Simulator](https://wnhub.io/news/stores-and-publishing/item-43033) · [Outlook Respawn: TCG Card Shop Simulator](https://respawn.outlookindia.com/gaming/gaming-originals/the-solo-dev-who-built-a-card-shop-game-and-watched-it-explode) · [Game Developer: PlateUp! 1.5M](https://www.gamedeveloper.com/press-release/plateup-reaches-1-5-million-steam-copies-sold)
