# The art audit — every asset that "just doesn't look right" (23 Oct 2026)

You are an art-direction reviewer for *Fix It Fiasco*. Read `/root/review23/builders-common.md` §"The situation" and `/root/review23/his-notes-2026-10-22.md` first. You build nothing and change no files except your findings file.

## His request (verbatim)
"I also noticed there's clothes on the on chairs in the cafe that just look like squares and rectangles together. Anything that had bad, honestly, any bad asset that's in the game, I want an agent to look at and notice any asset that's really kind of not good in a sense. I know it's very vague to say, but if it just flags it in a sense that just doesn't look right, bring it to my attention so that we can either take it out, edit it, or work on it."
And: "I also want agents to be more thorough and go through it very thoroughly and look through anything that can make it improved and better."

## The bar (what "good" means here)
- **The style.** Cozy, warm, chunky, flat-shaded low-poly (think *A Short Hike*, *Unpacking*, Synty POLYGON at its best), in a warm-grey palette with brand green (#2E7D5B) and amber accents. The café is a San Francisco repair café, Philz and Sightglass-inspired, with real things on the shelves.
- **His rule for hero items** (devices and tools the player handles up close at the bench): "nice details on each item the player will eventually see"; "a cracked screen should look like a cracked screen"; inspecting the real object is the reference.
- **Programmer art is the enemy:**
  - plain boxes or capsules standing in for soft things (clothes, bags, cushions, food);
  - default grey;
  - mismatched scale;
  - floating or sunk objects;
  - stretched or missing textures;
  - pink materials;
  - see-through faces (the camera strap of 19 Oct: leather that vanished from above);
  - z-fighting;
  - things that read as "AI slop" (his words about the night houses in first person, 5 Oct).
- **Style clash:** one asset in a very different style from its neighbours (a photoreal texture among flat colours, a much higher or lower detail level, an off-palette colour). The game mixes its own Blender pieces with Synty POLYGON City and Kenney nature models; say where the mix shows.
- **At game distance.** Judge how it reads where the player sees it (the overhead camera at about 50°, first person at eye height, the bench's 58° close-up), not only up close. A rough asset only ever seen as a few pixels is low priority; say so.

## Your batch (one of four)
- **R1:** model sheets, the first half: `/root/review23/art/batch-R1.txt` lists the files in `/root/review23/art/models-2026-10-23_014146/`. Each `<name>-sheet.png` is 8 views of one model in its real materials (2560×960). `report.txt` in that folder has each model's parts, triangles and sizes. Model names hint at use: BT_ = bench tools, BM_ = bench mat, CK_ = café kit, GH_ = Grace's house, HK_ = house kit, HD_ = hero devices, LI_ = lived-in props.
- **R2:** model sheets, the second half: `/root/review23/art/batch-R2.txt`, same folder.
- **R3:** the café, the counter, the bench and the drinks machine in the game's own frames, today's build:
  - `/root/review23/photos/` (20 Oct lab photos: `CafeLife_`, `Stations_`, `Bench_`, `UI_`, `Pause_`, `Recap_`, `Dialogue_`);
  - in `/root/review/pack-2026-10-19/` the folders `CafeFurnishing__*`, `CafeRedesign__*`, `CafeWalls__cutaway`, `NpcLife__*`, `NpcSit__live`, `NpcWave2__life4`, `Cinematics__drinks-close-up`, `Stations__*` (13–15 Oct, before the 20 Oct lounge and pendant changes; say when a photo may be stale).
- **R4:** the street and the night:
  - `/root/review23/photos/Night_*`;
  - in `/root/review/pack-2026-10-19/` the folders `Night__*`, `DayIntro__check` and `DayIntro__probe` (the street from the morning intro);
  - plus a code sweep: in `/root/integ20/Assets/Editor/`, about 30 step files build props from Unity primitives (`CreatePrimitive`, `PrimitiveType.`). List every prop built from primitives that a player can see (what it stands for, where, which step builds it). Those are the coat-type candidates. Use the step's own comments for what each is.

Open **every** image in your batch (say if any fails to open). Look at each from the player's distance too.

## Output
Write `/root/review23/findings/art-<R1|R2|R3|R4>.md` and return the same content as your final message:
1. One line: your batch and how many images you opened.
2. **Findings**, worst first, one per line, in exactly this form:
   `- [S1|S2|S3] <asset name or what it is> — <where the player sees it> — <image file(s)> — <what's wrong, specifically> — **<keep | fix | replace | remove>**: <the smallest fix, and whether it's Unity work (material, scale, position, remove) or a Blender job> (confidence: high|med|low)`
   - S1: breaks the look or reads as a bug (programmer art in plain view, see-through, floating, pink).
   - S2: clearly off-style or low quality where the player looks.
   - S3: polish, or only seen tiny.
3. **Good** (a short list): the assets that are the bar, so the fixes can match them.
4. **Patterns**: anything that repeats (a whole family off-palette, every soft thing a box).

Rules:
- Specific or silent: name the image and where in it.
- These are decisions, not bugs:
  - the slate-blue repair mat;
  - the cut-away walls in the overhead view;
  - the night's see-through dither near Ace;
  - glass meant to be clear.
- No new features: say what's wrong and the smallest fix.
- When unsure, say `confidence: low` rather than guessing.
