# Playtest 5: your notes, what we found, what you decided, and the plan (14 Oct 2026)

*Written by Claude as the Producer, 14 Oct 2026, 06:30 Chicago, on the Mac's link (`playtest-2` at `fab65d3`, one commit ahead of GitHub). This doc is the playtest-5 plan, the way `claude/playtest-4-findings-and-proposals.md` was playtest 4's. Your calls are settled (§5); every one is yours to overrule in a line. The agents' briefs for this session are built from this doc.*

## 0. In one minute

- **Neither you nor your girlfriend could get past Day 1.** The bench was slow and unreliable (parts dropping under the camera and through the table), the cloth and brush were hard to use, the holes looked enormous, the checklist was a wall of words, the sounds were terrible, and the café felt empty.
- **Almost every note has a cause in the code** (§3): carried parts float at a fixed depth and drop as physics; three devices at three scales (phone 1×, camera 2×, watch 3×) with real-size tools; screw holes drawn at a deliberately oversized "cartoon" head size; the cloth a rigid block on a spring; **two** real sound files in the whole game; Grace's house written into the intro; the night camera snapping when the bins view ends. And one more: **the scene's day is 180 seconds** ("3 min for testing, 7 for ship"), so "all day on the repair" meant three minutes.
- **ReStory's feel is not realism. It is forgiveness and feedback** (§4). We copied its physics and missed both, and we have a clock it doesn't.
- **The bigger failure was the process**: robot hands in the labs and agents who can't hold a mouse said "Solid". The studio's rules change today (§6).
- **The plan** (§7): reference first, feel targets, specs, then the forgiving bench, one scale, sounds, a real intro level, a fuller café; then a recorded playtest 6 with you, your girlfriend and a stranger.

## 1. Your notes, in your words (14 Oct, 00:30)

> I tried playing just one day, and let me tell you, yea the items now are in the view and sitting on the mat, but god it is so hard to get the camera perfectly repaired, i stayed all day on the repair, either constantly fidgeting over the little handle spring thingy? i also have a question as to why the handle for the camera is just a rectangle, it doesn't flow like how a camera would, i also like how we ARE ALMOST THERE with how the grime and the dust looks and feels like, as well as moving the screws and or other parts, because moving it and putting them on the tray literally seems sooo hard, i kept dropping a part of the camera under the camera, idk it was going through the table, through the item, it was just really hard to get the camera repaired itself, i also noticed when in isometric view, and getting to the drink station, the overall look and feel is up and diagonal idk how to explain it just feels weird. I also have notes from my girlfriend when doing this small day playtest. She said WOWWWW i love the intro, but god the sounds are just terrible. she also pointed why is the angle of the intro of the coffee maker or whatever so bad, and the screw and the placing on the tray, like it doesn't look like an intro level yet, but she loved the span on aces cafe sign and other places. she said, the views of the intro should change every day, not as much really but some intros should be different where it has a different b roll or a roll? or whatever, like why are we zooming on ONLY ON GRACES house, do you get what i mean? like this game isn't ace vs grace, its just grace is the first npc that we're creating that has a meaning for ace, and adds life, but as for now it seems robotic. she pointed out that the cafe felt empty, as there were things missing and also noticed people wouldn't sit down on the bar stools that are infront of the repair station. Now to get to the repair station notes, i would prefer the cloth to actually open up and be used like a cloth when using the cloth, and i only mean like for example, when i point my crosshair at the point of the cloth, it should just grab and i just drag it across an object and for it to feel the impact, its hard to explain, but it was so hard to use the cloth and or brush. I honestly would say the screwdriver felt the best, but was ultimately confused as to why there were these ENOURMOUSE HOLES for the screwdrivers, it through away from the experience of doing a repair when seeing that the holes are ENERMOUSe and the screws are so tiny,and that doesn't mean to go say to make the screws enormous as well. i would also prefer the mat to also look like a repair mat. the items that are being repairs to automatically fall in to the tray to be used or reused, the checklist is terrible. so ultimately i say for the agents who are playtesting, i want them to first literally research exactly how the gameplay looks and works and feels like when playing REstory through url links and or youtube or whatever the case may be. There is also a weird camera close up bug at night when i zoom in in isometric view when talking to the random guy in the dumpster, i can zoom up pretty close to ace which is awesome but when i walk towards to graces house for example, the zoom INSTANTLY SHOOTS up ONLY if i try to zoom or touch the zoom button at all, if i dont press it nothing happens and it stays in this close view which is cool, but just pointing out for you to notice. for it to also look full, i would say to add those cool items you know that make it feel like a repair cafe like, actual goods and items that would be at a cafe repair store. like bunch of pair in a bin under a desk near ace (aces desk type and that doesnt mean go make a new desk asset, but like adding props, like paper, bins, post it notes, tape, ruler, coffee cups, to go cups, coffee syrup, all these things. All to sum it up though the agents who are playtesting really need to review how the gameplay feels and works, and then ultimately in general, the playtest couldn't even get passed day 1 for me, so how could i sell that to anyone else, we need to make sure that it feels cohesive and easy and also nice. I know that is a vague request, but really process and understand how current indie and or triple a games are making games of this caliber and take that knowledge and delegate these certain agents to the task at hand. and then let us make a playtest notes for me that don't require exactly what im asking here right now, but a playtest notes that are in general curious towards the progress of the completion of this game.

**What already works** (yours and hers): the screwdriver ("felt the best"), the grime and dust ("ARE ALMOST THERE"), the intro as an idea ("WOWWWW i love the intro"), the pan across Ace's Café sign.

## 2. What the logs say

- **Your run, 13 Oct, 20:43–20:48 on the Mac's clock** (Unity's log): a fresh Day 1 (Grace first with her camera, the Officer, walk-in watches), the intro on Full (grinder's beans → a screw → the pour → the storefront). **The whole day ran out at the bench**: every phase from Calm to last orders, 14 café guests paid $5 each, Play stopped at closing before the recap (so no day log). The scene's day is **180 s**.
- **A run on 13 Oct, 10:43–11:04** (yours or your girlfriend's; `DayLogs/AcesCafeLayout/Day01_*`, `Day02_*`, uncommitted on the Mac): Day 1 finished — Grace's camera *Perfect* (158 s in the shop), the Officer's coffee, Beatriz's watch "Debris Inside" *Perfect* (348 s of service), $256. **Day 2: nobody served** — Grace (her latte) and Walter's watch "Seized Movement" both walked out waiting, both "Never again".
- So the bench can be finished, but slowly and unreliably, and the part swap (the camera's shutter, the watch's mainspring) is where it breaks.

## 3. Each note and its cause

**The bench**
- **A1 · The camera took all day ("the little handle spring thingy").** Almost certainly the **jammed shutter mechanism**: a block with three brass gears, a lever and a coiled spring in the camera's open well. You pinch it out with the tweezers and drop it, then pick the fresh part from the tray, carry it to the well and let go within **1.6 cm** of its seat. Carried parts ride at the depth they were picked up at (4 cm toward the camera): on screen over the tray, in 3D above or behind the camera; let go, they fall straight down as physics objects, and thin ones can tunnel through at 15–20 fps (the bench has a "catch" for pieces that fall under the mat: it happens). The camera is modelled at 2× (33 cm with its strap) over a 17 cm tray, so parts hide under it. The fresh part is a single steel petal, nothing like the block that came out.
- **A2 · Day 1 opens on the hardest repair in the game** (Grace's camera: three dirt spots, a lens smudge and the swap), at 2× scale, in a 3-minute day.
- **A3 · "Fall into the tray by themselves."** Agreed (decision 1).
- **A4 · The cloth and brush.** The cloth is a rigid folded block in the model; in the hand a small pad trailing the cursor on a spring that hops up after each smudge; you click it to pick it, then hold Use. Clean widths are fixed in real metres (cloth ~2 cm, brush ~1 cm), so on the 2× camera and 3× watch they work at a half and a third; the last 3 % of every spot must come off.
- **A5 · Enormous holes, tiny screws.** The phone is real size but its screws are 8 mm heads (the model's notes: real is 1.2 mm, made big "so it can be hit from 50 cm"); the watch's 12 mm. The hole left behind is drawn at the head's full width as a dark disc with a near-black core.
- **A6 · Three scales at one bench** (the hero models were built to the old stand-ins' sizes so the labs wouldn't break).
- **A7 · The mat** is a plain 60 × 36 cm slab built on 7 Oct "until the devices session models them"; it never was. The tray is five steel slabs; the caddy a plain block.
- **A8 · The checklist**: on the first repair, a 760 px strip, the controls card, the Day 1 card, the ticket and the prompt line, about **120 words** at once.
- **A9 · The camera's "handle"** is the strap: flat slabs (83 × 17 mm bands and a 140 × 18 mm loop) lying straight. The scene names it "KEEP - scratched sentimental strap": Grace's piece.

**The café**
- **B1 · Empty**: Day 1 is a trickle by design (2–3 customers); the 13 lived-in props read in first person and almost vanish from the overhead camera.
- **B2 · Repair-café props**: your list, plus a small-parts cabinet, labelled bins, tagged repairs waiting for pickup, cable spools, a multimeter, the magnifier lamp, a whiteboard of today's jobs; café side a syrup rack, stacked cups and lids, sleeves, a milk jug, beans; and a couple of props that tell Ace's story.
- **B3 · Nobody on the repair-bar stools**: two of the three are never chosen (cause to find), and there's no "sit on a high stool" clip yet (Mixamo batch 2, your download).
- **B4 · "Robotic"**: 8 of 10 people wear the same red shirt (the playtesters' count).

**The camera**
- **C1 · The night zoom.** Round the back, the bins view allows zoom to 8 m; walking away should ease back out to the street's 24 m minimum, but **any camera input cancels that ease**, so you stay close until you touch zoom, when the 24 m limit applies in one frame and the camera shoots up.
- **C2 · The drink station "up and diagonal"**: not pinned yet; the Cinematics lead is finding it from the scene (a screenshot from you would settle it).

**The sound and the intro**
- **D1 · Two real sound files** in the whole game (the dispenser's pour and "cup ready"); everything else is synthesized in code from noise and sine waves.
- **D2 · The intro**: four slots with two or three shots each; "a morning Grace is coming early shows her house" (Day 2 shows a white, empty room through her window); close-ups on models not built for them (no beans in the grinder, a near-empty tray). Her favourite, the sign, is a wide shot.

## 4. The bar: what ReStory and three others actually do

- **ReStory** (6 Aug 2026, $19.99; 300k+ copies in a week; 95 % positive of 5,600+ reviews): one repeatable structure (unscrew → remove layer by layer → dirty parts one side, clean the other → brush → replace → reassemble in reverse → complete and send), with a **notepad** tracking dirty/broken/missing parts and the reassembly order. Screws go "in a nearby receptacle (just as you would do in real life), and slotting in pieces always rewards you with the appropriate (and satisfying) audio cues" (Automaton). **Forgiving on purpose**: a reviewer pulled a tiny screw from a cramped corner "even while it faced away from him. No fumbling required." **No clock.** Light HUD. Brush upgrades widen the stroke; the last screwdriver upgrade makes screws instant. Its weak spots: rotation that "slides past the angle I need or snaps back", cursor strain, unclear which screws are in or out, parts hidden under parts, small screws, getting lost in reassembly.
- **Assemble with Care** opens with a cassette respooled with a pencil; its best-loved moment is unscrewing a screw and dropping it in a bowl.
- **PowerWash Simulator**: a Dirt Indicator (Tab / D-pad right) "will show even the tiniest specks of filth"; no time limits in the core game.
- **Unpacking**: 14,000+ sounds recorded from the real objects; pick-up sounds short and punchy because slower ones felt laggy; sounds follow mouse speed.

## 5. Your calls, settled (14 Oct, 05:59: "for all your questions, i trust your judgement to do the best option, the option that is closest to my request")

| # | Call | Settled as | Why it's closest to your request |
|---|---|---|---|
| 1 | Auto-tray | **Yes.** Taking apart stays physical under your hand; every freed piece then flies home on a scripted arc (screws to cups, covers to the parts well, broken parts to the old-parts bin) | "automatically fall in to the tray"; nothing can drop under the camera again |
| 2 | Part swap | **Two holds**: lift the broken part out (it flies to the bin); click the new part, it hovers over its seat, hold to press it in | the swap is where both runs broke |
| 3 | Scale | **One real scale at the bench**, method first (Technical Director) | "the holes are ENERMOUSe and the screws are so tiny" |
| 4 | Cloth and brush | **Direct grab**: point, press, drag, let go; feel = look + sound/rumble + the device budging | "it should just grab and i just drag it across an object" |
| 5 | Screws | **Real proportions, big invisible hit areas**; the hole a small threaded opening | "that doesn't mean … make the screws enormous" |
| 6 | Mat | **A silicone repair mat** (grid, rulers, moulded screw cups, a parts well) in **cool slate blue**, so warm devices pop | "look like a repair mat"; the cups are the tray |
| 7 | Checklist | **A small paper card on screen**: tool icons that tick off, under 25 words on the bench screen; your "objects are objects" lock stays | "the checklist is terrible" |
| 8 | Day 1 | **Grace brings the camera for a lens and film clean only; she comes back on Day 2 for the shutter** (a return beat; her Day 1 latte lock stays); the day's length set for an intro level | "it doesn't look like an intro level yet"; keeps Grace first |
| 9 | Café | **Yes**: settled guests on Day 1, props dressed for the overhead camera, a couple of story props for Ace | "for it to also look full" |
| 10 | Night zoom | **Close zoom allowed outside at night; limits ease, never snap** | "i can zoom up pretty close to ace which is awesome" |
| 11 | Intro | **v2: neighbourhood B-roll**, Grace about one morning in six, wide and mid shots | "a different b roll … why are we zooming on ONLY ON GRACES house" |
| 12 | Agent rules | **Yes** (§6) | "the agents who are playtesting really need to review how the gameplay feels and works" |
| 13 | Scope freeze | **Yes**, until someone who isn't you finishes Day 3 and wants Day 4 | "couldn't even get passed day 1 … how could i sell that" |
| 14 | Playtest notes | **A playtest journal page** (§8) | "playtest notes … curious towards the progress of the completion" |
| 15 | Order | **The plan in §7** | — |

Your four details: **"handle" = the strap** (and the body's outline gets checked); **"feel the impact" = all three** (look, sound and rumble, the device moving); the drink-station screenshot: the Cinematics lead reproduces the view from the scene (send one if you catch it); **the machine: the Mac** (your run was on it).

## 6. The studio's rules from today

1. **Reference before work**: any lead whose task changes how the game feels studies real footage first and writes a feel sheet (per action: seconds, clicks, sound, motion, words on screen, forgiveness).
2. **Feel targets before building**, as numbers. To start: a first-timer finishes Day 1's first repair in under three minutes; under 25 words on the bench screen; a sound the instant every action happens; nothing removed ever out of sight; a dirt spot clears in two to four strokes; no action needs a precise drop.
3. **A clumsy hand in the labs**: a cursor that moves at human speed with overshoot, jitter, near-misses and off releases, reporting time and retries. If it can't finish Day 1 and Day 2, the build isn't ready, however green the rest is.
4. **The playtesters stop grading feel** from photos; they report risks for your hands and analyse your recordings.
5. **Your playtests are recorded**, and the agents timestamp every struggle.
6. **Nobody helps a playtester** while they play (Jesse Schell's rule).
7. **No physics in the core loop**: scripted motion for anything the player relies on; physics only as flourish that can't lose a piece.
8. **Labs prove what you'd see, at your frame rate** (15–20 fps on the Mac with Unity behind other windows).

## 7. The plan (one ordered list)

1. **The feel sheet** from real ReStory footage, and **the specs** from eight leads: the forgiving bench and the clumsy-hand lab (Systems), one scale (Technical Director), checklist v2 (UI/UX), Days 1–3 (Narrative with Progression's numbers), the bench's sounds (Audio), intro v2 + the drink view + the night zoom (Cinematics), the bench's look and the café full (Art Direction with Environment), stools, settled guests and outfits (NPC). *In parallel, in the cloud; the reference agent watches footage in the browser on your Mac.*
2. **Feel targets** from the feel sheet; **QA** reads every spec against the code, the locks and your notes.
3. **Blender pieces** (mat with cups, tool stand, cloth shapes, the curved strap, a clean shutter mechanism, screws in proportion, tool icons) and **the first real sounds**. *Cloud.*
4. **Unity A**: the forgiving bench, one scale, the clumsy-hand lab, the night zoom. **Unity B**: sounds in, checklist v2, Days 1–3. **Unity C**: the café props, settled guests, stools, outfits, intro v2, the drink view. *One at a time on your Mac, in the background, labs after each.*
5. **The checks** against the feel targets; the notes; **your push**; **playtest 6**, recorded: you, your girlfriend, and one person who has never seen the game.

## 8. The playtest notes (the same every time)

A journal page you and your girlfriend fill in on your phones after each playtest (link in the session's messages once built): the top line (date, who, Mac/PC, mouse/pad, how far); **how finished each part feels, 1–5** (bench, drinks, serving people, the café as a place, the people in it, night, story, sound and music, words and menus, overall look); **eight questions** (would a stranger pay $12.99 for this? where did it stop or start being fun? where did you think "what do I do?" what felt most unfinished? what was good enough to show a friend? what did you want to do that you couldn't? did you want to play the next day? the one fix before next time?); free notes; for a guest, "in one sentence, what is this game?" and where they got stuck. Every entry is kept, the scores are charted across playtests, and the agents read the entries directly.

**Recording (free):** Mac — Cmd+Shift+5 → Record Entire Screen → Options → your microphone → Record. PC — Win+G (turn the mic on in Capture), then Win+Alt+R to start and stop.

## Sources

ReStory: progameguides.com (beginner's guide), vgtimes.com (guide), cogconnected.com (review), keengamer.com (preview), automaton-media.com (demo impressions), steamdeckhq.com (preview), vaporlens.app (review summary), store.steampowered.com/app/3812600, nettosgameroom.com (300k in a week), 80.lv (budget in 12 hours). Assemble with Care: imore.com (review). PowerWash Simulator: progameguides.com (tips), kotaku.com (the devs on making cleaning fun), news.xbox.com (PowerWash 2). Unpacking: gamedeveloper.com (GDC 2022 audio talk). Playtesting: Jesse Schell, *Playtesting 101*. Repair mats: ifixit.com (Magnetic Project Mat).
