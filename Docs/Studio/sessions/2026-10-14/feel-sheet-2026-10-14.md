# Feel sheet — how ReStory's bench actually plays (14 Oct 2026)

*Reference researcher (playtester-veteran's hat), Opus 5.5. 07:14–08:08 on the Mac's clock. Built-in browser on his Mac only; no Unity, no other app, no sign-in, no downloads, no ads clicked (one mid-roll ad was left to play out muted). Frames were stepped by seeking a paused 1080p–1440p player and magnifying the video element up to 5×, so the timings below are read from video time: ±0.1 s, or ±1 frame where 60 fps is stated. Sound can't reach me: every sound beat is `[inferred]`.*

## The answer in five lines

1. **ReStory looks physical but is scripted underneath.** Parts are dragged rigidly under the cursor on a flat mat (no depth, no physics while held). Screws reach the brass dish without being carried. Reassembly snaps from a sloppy drop. In the first repair nothing was ever loose, under the device or lost (Zhain frame by frame; nothing seen in the other two players' sampled frames).
2. **The same first repair took 106 s, about 160 s and about 220 s** for three players (Zhain 1:32.5 → 3:18.5; Clear Gameplay 2:22 → 5:02; Gab Smolders, a blind launch-day let's play, ≈3:25 → ≈7:05, edited so a floor). It is 11 parts, about 7 screws and 5–6 parts cleaned: about 44 actions at **2.4–5 s each**. The first-timer took about twice the fastest player's time. **About 50 s of hers was two stalls:** how to clean, and what order things go back in.
3. **While working, the screen carries about 11 words** (three control hints plus a to-do card with one live percentage). The big notepad (about 35 words) slides in at the start and the end, and otherwise only when the player pulls it out.
4. **Cleaning is fast and coarse:** a brush ring about 45 % of the part's width took a whole front panel from 0 to 100 % in 6.5 s (9.5 s for the second player), with the panel turned to finish.
5. **The studio's 7 Oct reading got the core wrong.** It took "parts lie where you drop them" and "screws fall" to mean physics and depth. ReStory has neither in the core loop. Today's decisions (auto-tray, two-hold swap, no physics in the core loop) move us toward ReStory, not away from it.

---

## 1. What I watched

| Source | URL | Length | What I used |
|---|---|---|---|
| **ReStory**, Zhain Gaming, "RESTORY: CHILL ELECTRONICS REPAIRS Gameplay Walkthrough FULL GAME No Commentary + Ending" (35K views, release build) | https://www.youtube.com/watch?v=x6lq9h_5Xa0 | 9:56:58 | **0:00–3:35, frame by frame (about 120 frames):** the first repair, a "Pokia 3310" for Hashimoto, from hand-over to "Perfect!". Spot checks at 1:00:20 (the upgrade shop) and 2:00:30 (a console on the same bench). No chapter list. |
| **ReStory**, Clear Gameplay, "ReStory - Part 1 - Chill Electronics Repair Shop Simulator (4K 60FPS No Commentary)" (published 13 Aug 2026) | https://www.youtube.com/watch?v=dmKohpe50vo | 45:19 | **1:30–5:10** (the same first repair, a second player, used to check the timings, with screws at 60 fps), **7:00–9:45** (the second job, a "Clean · Repair") and **29:20–29:35** (sourcing parts from the parts box for an online build). The HUD clock read at 2:19, 5:10, 7:12 and 9:00. Sampled every 1/60–12 s. |
| **Assemble with Care**, FullThrough, "FULL WALKTHROUGH … NO COMMENTARY \| PART 1" | https://www.youtube.com/watch?v=1nBirC4YyeQ | 28:36 | 5:20–6:20 (the first job: the tape deck and the cassette respooled with a pencil); 20:50–21:00 and 25:00 (a film camera). |
| **PowerWash Simulator**, Chocoholie, "Career Mode … Part 1: Clean The Van" | https://www.youtube.com/watch?v=coKGAS9zmSk | 12:16 | HUD at 1:00–2:30 and 2:00, 5:00, 8:20, 9:20, 11:40 (job complete); the windscreen's part-complete beat stepped at 1:41.6–1:47. Plus Game Grumps' episode 2 (youtube.com/watch?v=HAAA5coek2Q): its title only, as evidence the show-dirt button is easy to miss (the moment itself not located). |
| **Unpacking**, SunkieOfficial, "Unpacking – 1997: Childhood Bedroom [No Commentary]" | https://www.youtube.com/watch?v=DyrBOrzbx4g | 8:06 | 1:00–8:00 sampled; a carry stepped at 2:01–2:03; the room's ending at 7:20–8:00. Plus text: the mechanicsofmagic.com review (misplaced items flagged red only when the boxes are empty) and the Game Developer GDC 2022 audio talk (via common brief §5). |
| **ReStory**, Gab Smolders, "ReStory: Chill Electronics Repairs [1]" (launch day, 6 Aug 2026; sponsored; an edited stream with captions; a **first-time player**) | https://www.youtube.com/watch?v=zgHwf7l2Mto | 1:34:27 | 3:00–7:15: frames every 5–20 s plus her auto-captions read every 4 s (paraphrased; one short quote). The first repair from first touch to the shop view, with her struggles timestamped (§2b). Edited, so her time is a floor. |
| Text, ReStory | progameguides.com and vgtimes.com beginner's guides (fetched today); the common brief §5's sources | — | The structure of the work, upgrades and the notepad. **Neither guide describes the inputs**; the footage is the only source for those. |
| Text, PowerWash | gamesradar.com/powerwash-simulator-tips | — | A cleaned item flashes; Show Dirt on d-pad right; the tablet's job list makes a chosen object flash. |
| Text, Unpacking | mechanicsofmagic.com (RWP week 7) and a Steam discussion (app 1135690) | — | When the red "wrong place" outline appears and how long it stays. |
| Steam page 3812600 | store.steampowered.com/app/3812600 (text via WebFetch; trailers not watched) | — | The description names taking apart, cleaning, replacing and reassembling with a toolset, and spare parts bought through an in-game Y2K web browser. **No time pressure is mentioned.** Reviews today: 95 % of 5,471 English reviews positive; 93 % of 1,423 in the last 30 days. |

Two of the three players were practised. The one blind first-timer (Gab Smolders) took **≈220 s for the first repair, 2.1× the fastest player's time** (edited footage, so a floor). Read the practised times below as floors.

---

## 2. ReStory's bench, action by action (the first repair; Zhain Gaming's video times unless marked Clear Gameplay)

**The frame.** The camera is seated at the window until he takes the device. At 1:32.5 the phone lifts and the camera swings straight down in about 0.7 s (1:32.5 → 1:33.2). From then on the camera is fixed top-down; only zoom moves (he zoomed about 2× once, at 2:07, to scrub the front panel). Layout:

- The blue gridded cutting mat fills about 85 % of the frame's width and about 82 % of its height.
- The device sits mid-mat at **about 41 % of the frame's height** (the phone: 183 of 450 px). That is **about 3.9 px/mm** at 1080p if it is drawn at the size of the 113 × 48 mm phone it parodies `[inferred]`.
- The left half of the mat has four pale horizontal lanes. The player laid every removed part in them, in rows.
- Around the mat: a **round brass screw dish at the mat's bottom-right corner**, the **parts box** (a crate) bottom-left that opens an inventory grid (R12), a wooden tray with the brush at bottom centre, a mouse on its pad top-left, a pen cup, a sticky note and the open manual top-right, and the notepad tucked off the right edge.
- Three control hints sit **top-left** ("Object rotation", "Zoom", "Hint", each with a mouse or key icon) and a close **X** top-right.
- The cursor is a white arrow that becomes a hand over anything grabbable and a white ring with the brush.

| # | Action | Input (s) | Time measured (video, ts) | What moves and how | Sound `[inferred]` | Words on screen | Forgiveness (what a miss costs) | Take / not take |
|---|---|---|---|---|---|---|---|---|
| R1 | Take the device onto the bench | click the device on the mat | camera seated → top-down in **0.7 s** (1:32.5 → 1:33.2) | the phone lifts; the camera swings to top-down; the notepad slides in from the right (1:33.6) | pick-up | notepad: model, price, days, services, portrait, "How to reassemble?" + 8–11 "Unknown Part" rows (~35 words) | none needed | **Take** the short swing into a fixed working view. |
| R2 | Hover | move | outline drawn on the same frame the cursor is over the part (1:33.2, 2:58, 3:14.5) | a **white outline** round the hovered part; the arrow turns into a hand | — | 0 | none | **Take.** Matches §6.5/§6.7 (hover reads, then the next target glows). |
| R3 | Take off a loose part (front shell, bezel, screen, back cover, battery, keypad…) | press on the part, **drag**, release; no tool | **≈1.3 s per part**, grab → drop → back on the device (shell 1:33.8 → 1:33.9 placed; bezel 1:35.1 under the cursor → 1:35.5 dropped; next part 1:35.9 → 1:36.4) | the part sits **rigidly under the cursor, no lag, flat, no depth**; dropped where released, lying flat in a lane | short pick-up and put-down `[inferred]` | 0 | a drop anywhere on the mat is fine; nothing can fall off or under | **Take** "rigid, flat, no depth, can't be lost". **Not** the carry: under our clock the auto-flight (§6.1) is better. |
| R4 | Turn the device over / on its side | the "Object rotation" binding (a mouse button and drag `[inferred]`) | flips at 1:38, 1:41 and 1:48 (Zhain); Clear Gameplay swung the phone ≈70° and back over **4.5 s** (2:33–2:37.5) | **free, continuous 3D rotation; no face snap seen.** The device rests at whatever angle it's left (≈20–30° off square at 2:37.5) | — | hint line | overshoot is the cost; reviews call rotation its weak spot | **Not take** free rotation. Ours (quarter turns, snap to a face, bench-spec §2.5) is already better. |
| R5 | See what's next | the **Hint** key `[inferred]` | 1:54.0: yellow rings on the 4 visible screws of the layer he's on; gone at 1:55. The same rings appear for the second player at 2:48 (Clear Gameplay) | coloured rings in the world, on the targets | — | 0 | — | **Take** (= §6.7's soft glow on the next thing to touch). |
| R6 | **Unscrew a screw** | press and **hold** on the screw; a **white progress bar** appears beside the hand cursor and fills | **hold ≈1.8–1.9 s per screw at base**, timed on two bars. Zhain: 1:56.4 empty → 1:58.3 full; the next bar starts 1:58.7. Clear Gameplay at 60 fps, magnified 5×: ≈25 % at 2:54.3, 45 % at 2:54.7, 70 % at 2:55.1, 93 % at 2:55.5, full at 2:55.8, screw gone at 2:55.85. So about **2.0–2.3 s per screw including the move to the next**. (Screw counts read off the dish suggested faster, but at that size they are unreliable; dropped.) Upgrades shorten the hold, then make it instant (guides) | the screw ends **in the brass dish at the mat's corner with no drag**; the cursor never left the phone. **No screw ever lay loose on the mat.** **It jumps there in one frame:** at 60 fps (Clear Gameplay) the head is in its hole at 2:55.833 and gone at 2:55.867, and a third mark is in the dish by 2:55.867. No frame shows it crossing the ≈450 px between. The two screws already in the dish shift as it lands (a small settle in the dish `[inferred]`) | a click on release, a tink in the dish `[inferred]` (Automaton: satisfying cues) | 0 | nothing to miss: no drop, no carry | **Take: hold → it goes to its cup by itself.** Exactly §6.1. Real-proportion heads read at about 14 px (see §4). |
| R7 | Lift out a board or housing | drag | 2:02.5 board to lane 3; 2:05.5 housing to lane 4 | as R3 | — | 0 | as R3 | as R3 |
| R8 | **Clean a part** | drag the dirty part into **the work spot mid-mat** (the device's silhouette). The to-do card names it and **the cursor is already the brush ring, with no tool pick** (Clear Gameplay 3:18 ring and card up → 3:19.3 ring on the keypad → 3:19.8 scrubbing with dust). Then **hold and scrub**; turn it to reach the back | Front panel **0 % (2:06.0) → 27 (2:07.5) → 40 (2:08.5) → 45 (2:09.5) → 51 (2:10.5) → 73 (2:11.5) → 86 (2:11.9) → 92 (2:12.2) → 100 (2:12.5)**: **6.5 s for the whole part**, average 15 %/s, no snap at the end (it reached 100 % by scrubbing, turned face-down). Back panel ≈8 s (2:18–2:26). Second player, same front panel: **3 % (3:28.5) → 27 → 45 → 49 (3:32) → 98 (3:37) → 100 % (3:38): ≈9.5 s**, turned on its edge for the rim (Clear Gameplay). | dust layer erased where the ring passes; **orange dust puffs** at the brush; the part tilts as you scrub | a bristle scrub `[inferred]` (KeenGamer: "clicks and squeaks") | **to-do card, bottom-right:** "TO-DO LIST / [part name] / 1) Remove dirt: 51 %" with a bar (~7 words) | none: progress only moves over dirt, so you just keep going | **Take** a big footprint, a per-part live % and "the cursor becomes the tool over the dirt, no pick step". Ring ≈ **45 % of the part's width** at default zoom (2:13.5). **Not take** the hidden "move it to the work spot first" step, which cost the first-timer ≈20 s (§2b). Our tool should work wherever the dirt already is. |
| R9 | Put a part back | drag the part from its lane onto the device and release | bezel: carried **≈¼ of the device's width off and tilted ≈10°** at 3:15.0 → **seated by 3:15.3**; front shell: grabbed 3:15.6 → seated 3:16.3 (**0.7 s** grab to seat) | the device body shows a **pink-red outline** whenever the cursor is over it, carrying a part or not (3:13.5 carrying; Clear Gameplay 2:55.9 and 3:01 not carrying). White = a part you can take; pink = the device body `[inferred]`. Once the device is apart, **a translucent white silhouette of it marks the rebuild spot mid-mat** (Gab Smolders 4:51; Clear Gameplay 4:35, 7:55). Release and the part **snaps home in ≤ 0.3 s**, settling from a tilt | a seat click `[inferred]`; Automaton names slotting-in cues | 0 | **a sloppy drop still seats**; order is guided by the notepad's list | **Take** the generous snap. Ours: §6.2's "click → flies to its seat → hold to press". |
| R10 | Screw back in | hold on the empty screw position; same bar | 3:00 and 3:02 (bars under the cursor at two positions in turn) | the screw comes from the dish by itself; **no carrying** | ticks `[inferred]` | 0 | — | **Take** (= bench-spec §6 A). |
| R11 | Finish | none: it fires when the last part seats | last part 3:16.3 → notepad slides in ≈3:17 → **"Perfect!" at 3:18.5 (≈2.2 s)** → back to the window by 3:21 | a handwritten white "Perfect!" floats over the device; the notepad shows "How to reassemble: 11/11" and "Services: ~~Clean~~" | a success sting `[inferred]` | ~35 (notepad) + 1 | — | **Take** one ending over the device. Ours fires faster (0.3 s); keep that. |
| R12 | **Source a replacement part** (Clear Gameplay, an online build of an "Autorolla Razor") | click the parts box → a cardboard **inventory panel** opens over the left of the mat: a model dropdown and a 3×3 grid of part icons, each named on hover ("Display Frame · Autorolla Razor"); click an icon | **≈1 s per part** (29:21 → 29:22 → 29:23: hover, click, the part is on the mat) | the chosen part **appears on the mat by itself**, beside the work (spawn or flight not caught `[inferred]`); the panel closes and the parts are laid out ready | a pick sound `[inferred]` | ≈5 (the model name and one hover label) | nothing to carry; a wrong pick just sits on the mat | **Take** "the fresh part is already waiting, marked, on the mat" (= §6.2's marked spot). Ours has no shopping step at the bench; don't add one. |
| — | **Whole first repair** | — | **106 s** (1:32.5 → 3:18.5: disassembly ≈32 s (1:33–2:05), cleaning ≈45 s (2:06–2:51), reassembly ≈24 s (2:52–3:16), ending 2 s); second player **≈160 s** (2:22 → 5:02 "Perfect!", Clear Gameplay); **first-timer ≈220 s** (Gab Smolders ≈3:25 → the finished phone ≈7:05, edited; §2b) | about **44 actions**: 11 parts out and 11 in, 6–7 screws out and 6 in, 5–6 cleans, about 4 rotations | — | **≈11 while working**; ~35 at start and end | **zero** losses, drops or re-dos seen | **2.4–3.6 s/action practised; ≈5 s/action first time.** |

### 2b. A first-timer's first repair, struggle by struggle (Gab Smolders, blind, launch day)

Her captions are paraphrased, with one short quote. Video times; the footage is edited, so gaps may be shortened.

| Time | What happens | Cost |
|---|---|---|
| ≈3:25 | Takes the phone; reads the notepad ("How to reassemble 11 of 11", 3:41); finds zoom (3:45) and admires the detail | — |
| 4:13 | First screws: notices with delight that **they go to the tray by themselves**: "they go in a little tray" | 0: a positive surprise |
| 4:25 | Stops to ask what to do next | ≈5 s |
| 4:45 | Everything is out; the notepad says the parts are dirty | — |
| **4:49–5:09** | **Stall 1, cleaning.** She knows what to clean but not how. She drags the front panel around the mat (it is at the top-right corner at 4:57) and wonders aloud whether to drag it somewhere. At 5:02 it is mid-mat with the brush ring, and at 5:09 she realises she is cleaning | **≈20 s** |
| 5:13–6:17 | Cleans part after part (half done at 5:13; zooms in for the keypad at 5:29; nothing dirty at 6:17) | — |
| 6:25 | Reads that the service was only "Clean" and starts reassembling | — |
| **6:37–7:01** | **Stall 2, order.** Tries to remember the order; offers a part that has to go in later and corrects herself | **≈25–30 s** |
| ≈7:05–7:13 | Phone whole; says she feels accomplished and asks if Snake works | the payoff |

**So:** the two places a first-timer lost time in ReStory were **how to start the cleaning verb** and **what goes back next**. The cleaning verb only works once the dirty part sits in the mid-mat work spot (R8), and nothing says so. Screws, drags and reassembly snaps cost nothing. Our §6.4 (direct grab on the cloth and brush, used where the dirt is) and §6.7 (the glow on the next thing to touch) aim exactly at those two stalls.

---

## 3. The other three, shorter

| Game · action | Input | Measured / seen | How it moves | Words | Forgiveness | Take |
|---|---|---|---|---|---|---|
| **Assemble with Care · the first job** (tape deck, then the cassette) | click and drag; a story card's "RESTORE" button starts it (5:18) | the whole job ≈ 5:20 → before 7:00 (**≈60–100 s**); the cassette is respooled with a pencil standing in its hub (6:20) | **no HUD at all**, just a dialogue line top-left ("…those batteries look like they need replacing…", 5:42); the finished deck **flies by itself** (no cursor in frame, 6:11) into a translucent outline slot at the left (6:14) | ≈10–15 (dialogue only) | the easiest verbs first: a battery swap, a pencil | **Take** "the first job is the easiest thing in the game" (§6.8 already does it). |
| **AwC · replace a part** (the deck's batteries) | drag out, drag in | **2 out + 2 in ≈11 s** (5:43 → 5:54), ≈2.7 s per move | old batteries are dragged out and **laid on the table** beside the opened cover (5:44.5–5:46). **The two spares wait in view** at bottom-right from the job's start (5:36) and are dragged into the compartment (5:50–5:54). The camera is fixed top-down; nothing falls | 1 dialogue line ("…need replacing. Luckily I have some spares", 5:42) | the fresh part is already out and visible; nothing to find | **Take** "the spare is already in view from the start". Ours: the fresh part in its marked spot (§6.2). |
| **AwC · unscrew → bowl** (the film camera) | click the screw | 20:52 screwdriver on its rest → 20:54 **the screwdriver is in the screw, upright** → 20:56 **the screw has risen out**, the driver back on its rest; the screws sit in a **round glass bowl, top-right** (20:52, 25:00) | the tool comes to the screw; the screw rises along its axis; the bowl keeps them; **no carry** | 0, plus a spoken reminder at 20:59 ("…make sure I screw everything back") | nothing to miss | **Take** "the tool comes to the screw, the screw goes to the bowl." Hints are objects too: a **paper assembly diagram** lies on the table (20:50). |
| **PowerWash · progress and the part-complete beat** | hold the trigger | ring top-left "13 % cleaned" (2:00) → 45 % (5:00) → 90 % (8:20) → 98 % (9:20) → **100 %, "Job Complete – Open the tablet to continue"** (11:40); money top-right $3 → $18.50 → $38 → $150. **A part finishing, measured:** the whole windscreen **flashes white-blue for ≈0.5 s** (not there at 1:41.6, full at 1:41.85, faded by 1:42.4); **the money ticks on the same frame** ($1.00 → $3.00); a toast "Windscreen Cleaned! +$2.00" sits top-right for **≤ 2.2 s** (gone by 1:44) | under the ring: **the aimed part's name and its own bar** ("Body · METAL"); **Show Dirt** on d-pad right; the tablet's job list makes a chosen unfinished object flash (GamesRadar) | ≈3 while working (part name, material, %); 3 + the amount in the toast | no time limit; the last specks can be *found* | **Take** the whole-part flash of ≈0.5 s with its chime and reward on the same frame, the per-part bar and a show-dirt button (= §6.4). **But the button is easy to miss:** Game Grumps found it only in their second episode, and named it in the title: "there's a see dirt button?!??" (youtube.com/watch?v=HAAA5coek2Q). Ours should be shown once by itself on the first repair (T17). |
| **Unpacking · placement** | press, drag, release | a sketchbook from the box onto the desk in **≈0.8 s** (2:02.2 in the box → 2:02.6 under the cursor → 2:03.0 lying on the desk) | carried **rigidly under the cursor**, laid flat on whatever surface it's released over; **no outline, tint or ghost seen** while carrying. Misplaced items get a **red outline only once the boxes are empty**, and it stays until the item is moved to its proper place (mechanicsofmagic; a Steam discussion on the photo pinned to the cork board). The room's end: a burst of yellow stars bottom-right (7:50), then the finished room framed as a photo (8:00) | pick-up sounds short and punchy because slower ones felt laggy; 14,000+ foley files (Game Developer, GDC 2022) | **0 while working**: only small icons in the corners | no nagging mid-task; correction at the end | **Take** "never punish mid-action; flag at the end", "pick-up sound on the first frame" and the ending as a picture of the finished thing. |

---

## 4. Our bench (13 Oct build) vs ReStory's, action by action

Code facts are from the common brief §4 and the repo; ReStory facts are from §2 above.

| Beat | ReStory | Ours (13 Oct) | Verdict |
|---|---|---|---|
| **Framing** | phone ≈ **41 %** of frame height, ≈3.9 px/mm; zoom about 2× to scrub | art spec's search: phone 38 %, camera 38 %, watch 24 % (addendum §3's summary); 3.1–4.0 px/mm (`specs/art-bench-and-cafe.md:171`) | **close for the phone and camera; the watch at 24 % is smaller** than anything ReStory shows. |
| **Screw head on screen** | ≈7 % of the phone's width → **≈14 px at 1080p** unzoomed (≈3.4 mm if the phone is real-size `[inferred]`) | art spec: a 2.4 mm head is **7–10 px** at default and reads as a screw only at ≥ 12 px (`art-bench-and-cafe.md:171`); today's cartoon heads are 8 mm (phone) and 12 mm (watch) (`hero_phone.py`, `hero_watch.py`) | **2.4 mm is below ReStory's visible size.** Suggest ≈3–3.5 mm heads (still real proportion) so the default framing gives ≥ 12 px. |
| **The hole** | a dark hex socket inside a silver ring, the head's width, visible while the screw is in | a disc the head's width with a black core at 0.9 × r (`Screw.cs:357`): an 8 mm black pit on a real-size phone | ours is the "ENORMOUS hole"; art spec's decal at half the head's width fixes it. |
| **Screw out** | hold (bar) **≈1.9 s** at base → into the dish in ≤ 1 frame; **0 loose screws** in 106 s | hold `turnDuration 0.9 s` (`Screw.cs:21`) → becomes a rigidbody, falls, rolls; magnetic tray; catch at 25 cm under the mat (common §4) | **ours is twice as fast to free** (right under a clock) **but riskier after** (physics, out-of-sight falls). §6.1 fixes the after. |
| **Part out** | rigid drag, flat, **≈1.3 s/part**, can't fall | pinch, held at grab depth − 4 cm on the cursor's ray (`ReplaceablePart.cs:39`, `:249`); released, it **falls from where it really is**; mass 0.01 can tunnel at 15–20 fps | **ours is slower, fussier and riskier**: depth where ReStory has none. |
| **Part in** | drop **≈¼ of the device's width off and tilted** → snaps in ≤ 0.3 s | must drop within `seatDistance 0.016 m` (`ReplaceablePart.cs:34`) after a sight-line projection (`:289`): **6.7 % of the 240 mm camera's width**; a fresh part that looks nothing like the old one | **ours needs a precise drop; ReStory needs none.** §6.2 fixes it. |
| **Cleaning** | ring ≈ **45 %** of the part's width; whole part in **6.5 s**; turn it to finish | `BrushRadius 4.8 mm`, `ClothRadius 9.5 mm` fixed in metres (`GrimeSpot.cs:45`), so a half and a third on the 2× camera and 3× watch; clean at 97 % (`GrimeSpot.cs:35`); the cloth trails on a spring and lifts off each smudge | **ours is slower and fussier** (small footprint on oversized devices, a lagging pad). §6.4 fixes it. |
| **Words on screen** | **≈11 while working** (3 hints + to-do card); ~35 when the notepad is out (start, end, on demand) | **≈120** on the first repair (common §4) | **ours ≈ 11× wordier.** §6.7's < 25 is right; ReStory shows ≤ 12 is possible. |
| **First-timer** | a blind first repair of **≈44 actions took ≈220 s** (≈5 s/action, including ≈50 s of stalls) and ended with her saying she felt accomplished (Gab Smolders, §2b) | Day 1's first repair today is the camera with a part swap; his run lost the whole 180 s day to it; T10 asks ≤ 90 s for a first-timer | **≈5 s/action is a fair first-timer rate.** At that rate T10's 90 s holds about **18 actions**, which is the size of Day 1's job (§6.8's lens and film-path clean). |
| **Clock** | **a clock runs during repairs, but nothing waits on it.** The seated view's clock read **09:42** before the first repair and **12:33** just after it (Clear Gameplay 2:19 → 5:10), **≈1 game-minute per real second**. The repair cost ≈2 h 51 m of a shop day that guides put at 08:00–midnight: **≈18 % of the day**. No customer patience; you can work on after closing (VGTimes) | the day is **180 s** (`DayClock.cs:28`) and runs during the repair, with customer patience on top | **the root clash:** ReStory's *practised* first repair (106–160 s) would be **59–89 %** of our whole Day 1, against ≈18 % of ReStory's own day. |
| **Sound** | a cue per beat (Automaton, KeenGamer) `[inferred]` | 2 real files; the rest synthesized (common §4) | ours is quieter and thinner; the audio spec covers it. |
| **Ending** | "Perfect!" ≈2.2 s after the last seat | wake at the last task, "Fixed!" 0.3 s later (bench-spec §2.5) | ours is faster; keep it. |

---

## 5. What makes ReStory's bench feel good, and where it's weak

**Ten things that make it feel good** (each from footage unless a source is named):

1. **Nothing gets lost.** 106 s, about 44 actions, zero loose screws, zero parts off the mat (1:33–3:18). None were seen in the second player's sampled frames either.
2. **Screws go to their dish by themselves.** One hold of ≈1.9 s, and the screw is in the dish within a frame while the cursor stays on the device (2:53.9–2:55.87, Clear Gameplay).
3. **Drags are rigid and lag-free.** The part sits exactly under the hand cursor (1:33.9, 1:35.1).
4. **No depth.** A fixed top-down camera over a flat mat; only zoom moves (1:33.2 onward).
5. **Reassembly forgives.** A drop a quarter of the device's width off and tilted still seats in ≤ 0.3 s (3:15.0 → 3:15.3).
6. **One verb per beat.** Drag parts, hold screws, scrub dirt. No tool-switch clicks seen for drags.
7. **Hover reads instantly.** A white outline and a hand cursor (1:33.2, 3:14.5); yellow rings on the current layer's screws (1:54.0).
8. **Cleaning is fast and coarse.** A ring about 45 % of the part's width; a whole panel in 6.5 s (2:06.0–2:12.5).
9. **Little text.** About 11 words while working. The big notepad appears at the start and the end (1:33.6, 3:17), and mid-work only when pulled out (Gab Smolders 4:51; Clear Gameplay 8:10).
10. **The device stays the hero.** It fills about 41 % of the frame, with parts laid around it in neat lanes, and one "Perfect!" over it at the end (3:18.5). Slotting pieces in rewards you with satisfying audio cues (Automaton).

**Its five weak spots:**

1. **Rotation overshoots** or snaps back (a player quoted in bench-spec v2 §1). In footage it is free and unsnapped: swung ≈70° and back over 4.5 s, left 20–30° off square (Clear Gameplay 2:33–2:37.5).
2. **The cleaning verb isn't obvious the first time.** The brush only works once the part is in the mid-mat work spot, and nothing says so. The blind first-timer lost ≈20 s dragging the dirty panel around before the brush started (Gab Smolders 4:49–5:09).
3. **Screw state is unclear**: you can't easily tell screwed from unscrewed, and parts hide under other parts (KeenGamer).
4. **Small screws** (SteamDeckHQ). Measured at about 14 px at default framing, just over the art spec's 12 px readability line.
5. **Getting lost in reassembly**: leftover parts mean you skipped a step (VGTimes, SteamDeckHQ). The first-timer lost ≈25–30 s on the order (Gab Smolders 6:37–7:01). The notepad covers about 30 % of the screen's width when it's out (1:30, 3:17).

*Also reported:* cursor strain over long sessions (Vaporlens summary).

---

## 6. Proposed feel targets (testable in the clumsy-hand lab and in his recorded playtest)

| # | Target | Number | ReStory's figure |
|---|---|---|---|
| T1 | Screw: first touch → resting in its cup | **≤ 1.3 s** at base (0.9 s hold + ≤ 0.35 s flight); **a flight never blocks the next input** (you can start the next screw while one is still flying); 0 carries; **0 lost or out of sight** in 20 clumsy runs | ≈1.9 s hold at base; hole → dish in **≤ 1 frame** (no flight drawn); 0 lost |
| T2 | Part (cover, loose part, broken part): release of the freeing verb → resting in its compartment | **≤ 0.6 s** flight; never below the mat's plane | ≈1.3 s, by hand |
| T3 | Fresh part in | **one click + one hold**; any click inside the fresh part's silhouette counts; seat ≤ 0.3 s after the hold completes | sloppy drop → ≤ 0.3 s snap |
| T4 | Input → first motion or sound | **≤ 1 frame at his frame rate** (≤ 66 ms at 15 fps); launch and landing cues on the flight's first and last frame | no visible lag in drags |
| T5 | Hover → outline or glow | **same frame**; the hit area ≥ 2× the visible head | same frame |
| T6 | Dirt spot | **2–4 strokes, ≤ 3 s**; a whole part ≤ 7 s; footprint ≥ 40 % of the spot's on-screen diameter at default framing | whole panel 6.5–9.5 s, ring ≈45 % of the part |
| T7 | Words on the bench screen | **≤ 25** with the first-repair controls card; **≤ 12** while working without it | ≈11 working; ≈35 when the notepad is out |
| T8 | Screw head on screen at default framing | **≥ 12 px** (phone and camera), ≥ 15 px after zoom | ≈14 px |
| T9 | Device in the frame | **≥ 35 % of frame height** at default (watch included, or a default 1.5× for it) | ≈41 % |
| T10 | Day 1's first repair (Grace's lens and film-path clean) | **≤ 18 player actions**; practised **≤ 45 s**; first-timer **≤ 90 s**; clumsy hand **≤ 120 s** (inside common §7's 3 min) | 44 actions: 106–160 s practised (2.4–3.6 s/action); ≈220 s first time (≈5 s/action) |
| T11 | Repair vs day | a first-timer's first repair takes **≤ 50 %** of Day 1's real length. At today's 180 s that is T10's 90 s, so **Day 1 must not get shorter than 180 s**; a 7-min ship day makes it ≈21 % | ≈18 % of the shop day, with no one waiting |
| T12 | Finish beat | "Fixed!" **≤ 1.0 s** after the last seat (ours 0.3 s: keep) | ≈2.2 s |
| T13 | Nothing removed out of sight | **0** frames with a removed piece hidden under or behind the device, across every clumsy run | 0 in 106 s |
| T14 | A part or spot finished | a whole-part gleam of **≈0.5 s**, with its chime on the **same frame** as the progress reaching done; any text ≤ 3 words and gone in ≤ 2 s | PowerWash: flash ≈0.5 s, money on the same frame, toast ≤ 2.2 s |
| T15 | Device rotation | quarter turns that **always settle square on a face** (0 rests off-square), each ≤ 0.3 s | ReStory: free, unsnapped (its weak spot) |
| T16 | Stalls (no valid action, not knowing what's next), timed in his recordings | **no stall over 10 s**, and **≤ 15 % of the repair** spent stalled, for a first-timer | ReStory's first-timer: ≈20 s and ≈25–30 s stalls, ≈23 % of her repair |
| T17 | Show-dirt is found | on the first repair, **the show-dirt flash fires once by itself** after ≈3 s of scrubbing with no progress, with its key named on the card; after that only on the button. *(A behaviour of the approved §6.4 button, not a new mechanic; his to overrule.)* | PowerWash: a well-known channel missed the button for a whole episode |

---

## 7. Where the studio's 7 Oct beliefs (bench-spec v2 §1) were wrong

1. **"Screws fall into a collection tray … real physics objects, not a flight to a slot."** Wrong for the core loop. In the first repair no screw ever lay loose or fell. Each reached the dish at the mat's corner while the cursor stayed on the phone. At 60 fps a screw goes from hole to dish in one frame with no path drawn (Clear Gameplay 2:55.833 → 2:55.867). **Physics lives only inside the dish:** the screws already there shift as one lands. That fits players' "bug out from the tray", and it is physics as flourish (common §7.7).
   - Our planned arc (§6.1) is slower than ReStory's jump but shows where the piece went. Keep it short (≤ 0.35 s) and never let it block input.
2. **"Parts are physical and go where you put them … each where the player dropped it."** True only as a flat 2D drag: rigid under the cursor, no physics while held, no depth, laid flat. The studio built 3D depth-carrying and a physics release from it (`ReplaceablePart.cs:39`, `:249`), which is the opposite of ReStory's feel and the source of "dropping a part under the camera".
3. **"Copy ReStory means physical causality: the screw … falls; parts lie where you drop them."** ReStory's satisfaction is *looks physical, behaves scripted*: rigid drags, a generous snap (about a quarter of the device's width), auto-collected screws, a coarse brush. Today's rules (§7.7 "no physics in the core loop", §6.1, §6.2) are the correction.
4. **"Control hints bottom-left: Object rotation, Zoom."** They are **top-left**, and there are three: Object rotation, Zoom, **Hint**. Yellow rings on the current layer's screws appear in both videos (1:54.0; 2:48), probably from Hint `[inferred]`.
5. **Missed on 7 Oct:**
   - the **brass screw dish** at the mat's corner;
   - the **hand cursor** and the **hold bar** for screws;
   - the **pink-red outline** on the device body under the cursor (white is for parts);
   - the **parts box** inventory grid, from which a picked part lands on the mat by itself;
   - the to-do card's **per-part percentage**, bottom-right;
   - the notepad's **"How to reassemble n/11"** parts list, which slides in at the start and the end and on demand;
   - the mat's **pale lanes** where parts get laid out;
   - the **translucent silhouette of the device** marking where it gets rebuilt; it is also the work spot where a dirty part must sit before the cursor becomes the brush (no tool pick).
6. **"There is no clock at all."** Half wrong. ReStory has an in-game clock that **runs during repairs** at ≈1 game-minute per real second (09:42 → 12:33 across the first repair, Clear Gameplay 2:19 → 5:10). What it lacks is **pressure**: no patience, no storm-outs, and work can go on after closing. So the lesson for our locked "the clock runs throughout the repair" is not "remove the clock". It is "make one repair a small slice of the day" (T11).
7. **Repairs that need a part are a longer loop in ReStory** (seen, not timed). The second job (an "Atari CX40", services "Clean · Repair") lists **"ON DESK: 6"** parts in the notepad with a state icon each (8:10). The customer says new parts can't be had and to find a donor device on the in-game marketplace (9:15). The parts box stores harvested parts ("Empty box for parts", 8:10). Our two-hold swap with the fresh part already waiting (§6.2) is far shorter, and right for a clocked café.
8. **Right on 7 Oct:** a top-down blue gridded mat; a white hover outline; dirt as an erasable layer that only progresses over dirt, with a "Remove dirt: n %" card and bar; real-looking internals; turning the part to find dirt; zooming in to work.

## Against the specs already in (addendum §3)

- **Art (`art-bench-and-cafe.md`).** Agree with its framing finding: ReStory's phone sits at about 41 % of the frame against the art spec's 38 %. **Disagree on head size:** 2.4 mm gives 7–10 px at default, under its own 12 px line (`:171`). ReStory's heads are about 7 % of the device's width (about 14 px). Suggest about 3–3.5 mm. The watch at 24 % (`:301`) is below anything ReStory shows; give it a 1.5× default.
- **UI/UX (`uiux-checklist-v2.md`).** Agree with < 25 words, the glow on the next target (ReStory's Hint rings) and one hold to fly and press the fresh part (`:501`). ReStory works at about 11 words, so there is room under 25.
- **Audio (`audio-bench-sounds.md`).** Agree with a cue on every flight's launch and landing (`:110–113`) and pick-ups within 20 ms (`:68`, Unpacking). Footage can't confirm ReStory's own sound timings.

*Car Mechanic Simulator (optional, text only):* a magicgameworld.com fix guide implies removed parts go straight into the inventory: you sort it "By Arrival" to find what you just took off. Not seen in footage.

*Unverified or not measured:* the button bound to ReStory's rotation; any sound; the Steam trailers; how a broken part leaves the bench in ReStory (the guides say a trash bin early on, a shredder later; not seen).
