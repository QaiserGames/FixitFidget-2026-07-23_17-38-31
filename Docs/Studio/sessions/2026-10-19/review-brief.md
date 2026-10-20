# Fresh-eyes review of Fix It Fiasco — brief for one reviewer

You have never seen this game. That is the point: you are the eyes the studio lacked. Its developer (Mansoor, solo, new to Unity) asked for "something that reviews EVERYTHING AND EVERYTHING, because these silly mistakes I want to get ahead of" after his girlfriend's playtest (19 Oct 2026) found a camera strap that is see-through in the game, and after she said she "felt overall lost most of the time" and didn't want to keep playing.

## The game (so you know what you're looking at)
Fix It Fiasco is a 3D repair-café sim (Unity, URP; PC, Steam 2027). You are Ace, broke, with a mortgage, running a small repair café in San Francisco. By DAY customers bring broken devices (a phone, a pocket watch, a film camera) and order drinks; you take the device at the counter, carry it to the bench, repair it in a first-person close-up (unscrew, pry, swap the broken part, clean with a cloth and brush), hand it back, and make drinks at a drinks machine. The overhead view is isometric for walking and delivering; first-person close-ups wherever Ace uses his hands (bench, drinks, conversations). By NIGHT a man hiding behind the café's bins makes Ace run petty errands (take a garden gnome called Barnaby from Grace's front yard, mess with a neighbour's cups, sneak into houses); a notebook holds the night's notes. Grace is the recurring regular (her camera comes in on Day 1 for a clean, Day 2 for the shutter). The goal of the whole game is to franchise the café. Tone: "chaos with a hint of cozy": a hectic counter, a cozy warm-grey flat-shaded look, a little mischief at night. Everything should read at a glance for a newcomer on a gamepad.

## What you have
A folder of photos taken automatically by the game's self-driving checks ("labs") on 13–15 Oct 2026, of the build she played. Each sub-folder is one lab; its `report.txt` says what the lab did and often what each photo is meant to show (the photo's file name says it too). Notes on the photos:
- They are the real game rendered at the lab's screen size, usually 1920x1080, scaled to 1280 px wide for you.
- Some photos are "model" shots in a neutral preview scene (a model on a mat, no café) — these are for judging the model itself.
- A lab drives with a virtual pad/cursor; a small cursor or ring may show. A banner like "CAFÉ LAB test save…" is lab-only. Mark anything you think is lab-only as such rather than as a bug — but DO flag any debug-looking text (percentages, internal names, ids) that a player would see.

## Look at every photo through three lenses
1. **QA — is anything broken?** Missing or see-through parts (example the studio already found: the camera strap's leather is invisible from above while its stitching and edges float in the air — look for that class everywhere: details floating with no body under them, surfaces you can see through, holes in objects), parts clipping into other parts, floating objects (gaps under things that should rest), wrong scale between things that sit together, z-fighting/flicker, stretched or missing textures, pink/missing materials, the camera inside a wall or something blocking the view of what matters, text cut off or overflowing its box, UI overlapping UI or covering the thing the player needs, anything too dark or blown out to see, placeholder or debug text.
2. **First-timer — would I know what to do?** Read each photo as a newcomer who is dyslexic and on a PlayStation controller (she is exactly that). Where the player must act: can you tell within ~2 seconds what to do next and where? Is the thing you need the most visible thing on screen? Is the highlight on it visible against what's behind it? Can every word be read at a glance (size on a 1080p screen, boldness, contrast against what's behind it)? Are button names written as words ("Square", "R2") where an icon would read faster? Is there too much to read at once (more than ~12 words in one place)? Is any important instruction likely to disappear before a slow reader finishes it (judge from context and the report)?
3. **Art — does it fit?** Cozy, warm, flat-shaded chunky style with a warm-grey palette; one consistent UI skin (fonts, colours, corners). Flag programmer-art looks (plain grey/white boxes, default fonts, harsh pure red/green), anything off-style, a room that reads empty or lifeless, harsh or muddy lighting.

## Rules
- Specific or silent: every finding names the photo (folder/file), WHERE in the frame (e.g. "top-right", "the strap's right run"), and WHAT you see. Never "the UI feels cluttered" without the elements.
- These are locked design decisions, not bugs: Grace orders no latte on Day 1 on purpose; the overhead camera is isometric and the hands-on views are first-person; screws fly to their cups by themselves (scripted, no physics); the repair mat is slate blue on purpose; the HUD carries numbers and the world carries colour and shape.
- No new features or mechanics in your fixes: say what's wrong and the smallest fix.
- Say when you're unsure (`confidence: low`) instead of guessing.
- Severity: **S1** = breaks the game's look or would leave a newcomer stuck/lost; **S2** = clearly noticeable, should be fixed before the next playtest; **S3** = polish.

## Output — write your findings file AND return the same content as your final message
Write `/root/review/findings/<your batch name>.md` with:
1. One line: your batch name and how many photos you looked at (you must open every photo in your list; say if any failed to open).
2. **Findings**, most severe first, one per bullet, in exactly this form:
   `- [S1|S2|S3] [QA|READ|ART] <folder>/<file> — <where in frame>: <what you see>. Why it matters: <one line>. Smallest fix: <one line>. (confidence: high|med|low)`
   If the same problem shows in several photos, list it once with every photo named.
3. **Patterns** — at most 5 lines: problems that recur across photos.
4. **Looks right** — at most 5 lines, so nobody "fixes" them.
Plain words, short sentences, numbers over adjectives.
