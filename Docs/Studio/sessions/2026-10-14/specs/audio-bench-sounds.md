# Audio: the bench's first real sounds

*Audio lead, 14 Oct 2026 (cloud). Answers playtest 5's "god the sounds are just terrible". Files are in `~/cafe/audio/`: outputs in `out/`, downloads and their licences in `src/`, the build in `tools/`. **Nobody has heard these sounds yet: I can't play audio.** Mansoor listens to `out/preview/all-cues-at-suggested-volume.wav` (85 s) before anything is imported.*

## What he will see (and hear)

At the bench, every touch answers on the frame it happens.

**Screws.** The driver ticks quickly as the screw backs out and clicks when it comes free. The screw lands in its cup with a bright little tink and a tiny second bounce, the way a real one does.

**Covers and parts.** A cover strains, pops and lands softly in the parts well. The broken part clinks into the old-parts tin. The new part goes in with a click, a snap and a thock.

**Cleaning.** The cloth hisses under his hand, louder and brighter the faster he rubs, and squeaks when he changes direction on greasy glass. The brush rasps the same way and sounds grittier while dust is coming off. A clean spot rings one soft glockenspiel note; clean glass gleams with a run of bar chimes.

**The finish.** The device wakes in its own voice: two soft notes for the phone, a winding and a ticking for the watch, a wind and a "ka-chick" for Grace's camera. Then "Fixed!" rings three rising chimes.

**The café.** A quiet hum sits under everything. The door has its own little brass bell. The till takes money with no cha-ching. The morning opens on a soft piano chord in the same key as the bell and "Fixed!".

## 1. What I did

- **Read and mapped.** Read the brief, my role file, the Bible, `claude/sound-plan.md` and the 15 audio scripts, and mapped every cue the code plays (§3, §11).
- **Tested what this workspace can reach.** Only GitHub (git and raw files), npm and PyPI. Kenney.nl, OpenGameArt, Freesound, Sonniss, archive.org, Wikimedia, itch.io and Hugging Face all return 403 at the proxy.
- **Built the files.** I found five CC0 sources on GitHub and read each licence in the source itself. From them I built **96 files for 45 cues** (mono, 44.1 kHz, 16-bit), plus a manifest, the licences, a preview reel and a spectrogram sheet.
  - 42 cues are made from real recordings.
  - 3 are procedural (Plan C, §4.3).
  - No file comes from a source whose licence I couldn't read.
- **Wrote the rest of the brief.** His 20-minute recording list (§5), the import steps (§6) and the code hooks (§7).
- **Not done.** The café's murmur, cups, coins, the grinder, footsteps and the night. Nothing reachable fits them; they wait for his recordings or the Sonniss pass.

## 2. The decision and its reasoning

**Sources: CC0 only, real recordings first.** CC0 means no credits are owed, and the files can sit in the public repo, which the Sonniss files can't (sound plan §3).

Kept:

| Source | What it gives the bench |
|---|---|
| Kenney's UI SFX Set | real mechanical switch and mouse clicks |
| Kenney's Interface Sounds | only its 10–20 ms ticks and one click |
| Kenney's City Builder kit | placement taps |
| Sonic Pi's samples (Freesound CC0 originals) | a brass bell, a till, a swoosh |
| Versilian Community Sample Library (VCSL) | real triangles, woodblocks, guiro, cabasa, shakers, ratchets, slapstick, glockenspiel, hand chimes, Nepalese brass bells, mark trees, wine glasses, upright piano, vibraphone |

Rejected:

| Source | Why |
|---|---|
| Minetest Game's sounds | mostly CC BY-SA 3.0, so share-alike would apply to our edits |
| ESC-50 | non-commercial licence |
| `sfxmint`, `react-sounds` | their files sit on hosts this workspace can't reach, so I couldn't see them |
| `uisfx` | CC0 (verified), but procedural UI tones; kept in `src/` for UI/UX, not used |
| Kenney's interface "drop" and "scratch" | on the spectrogram they are synthetic pitch sweeps and an FM wobble: the "cartoon boings" and "stock UI bleeps" the sound plan bans. I had picked them, then cut them. |

**Foley by substitution.** No CC0 recording of a screw in a cup or a cloth on glass was reachable. So each cue is built the way foley artists work, from a real recording of something else:

| Real recording | What it stands in for |
|---|---|
| a muted triangle hit, pitched down | a small steel screw landing on steel |
| a slapstick's crack | a plastic clip letting go |
| a guiro scrape | the clip straining |
| a woodblock dropped an octave | a plastic shell landing on silicone |
| a cabasa's beads | a stiff brush's grit |
| a rubbed wine glass | glass friction (the squeak) |

Every recipe is in the manifest and in `tools/build_sounds.py`.

**The lessons from the reference games, applied.**
- *Unpacking:* every hand action's sound reaches its loudest within 20 ms of its first sample (measured on all 46 hand-action files), and the grab sound is 40 ms long ("slower ones felt laggy"). The rubs follow the hand's speed, as Unpacking's shake followed the mouse.
- *ReStory:* a satisfying sound on every slot-in (`part.replace`, `part.on`, `screw.seat`) and a tink for every screw in its receptacle.

**The cup: metal into metal (a tink), not metal into silicone (a tup).** Three reasons:
1. **It reads.** The tink's energy sits at 3–6 kHz, above the café's murmur and room tone (0.2–2 kHz), so it is heard at low volume with the café full. A silicone tup sits right inside the murmur and gets lost.
2. **It's the loved moment.** The tink in the bowl is the beat players love in ReStory and Assemble with Care.
3. **It fits the code.** It keeps the "magnetic tray" language the code already uses.

The cost: Art Direction puts a small steel disc in the floor of each moulded screw cup (a magnetic-mat detail), so the sound is true to the model. If the cups stay all-silicone, the tink still holds once a cup has a screw in it (screw on screw). The quieter tick 55 ms after the first is the screw's bounce.

**Loops that follow the hand, not one-shots every frame.** Today the brush and the cloth fire one-shots every 0.09–0.42 s while dirt comes off (`GrimeSpot.cs:395–400`). That stutters and can't follow the stroke. Instead, a 2 s seamless loop plays while the tool is pressed, and its volume and pitch track the stroke's speed (§7.1).

**One small musical family in A major, as the sound plan asked.** These cues share the same notes, so the café has its own sound the way it has its own sign:
- the door bell: Nepalese brass bells with a faint E and A chime under them;
- "Fixed!": hand chimes A–E–A;
- the phone's wake: vibraphone A then E;
- the day's opening bell;
- the intro's sting: piano A, A, C#, E.

**The brass bell.** My role file's log (9 Oct) suggested a bell at the repair bar on "Fixed!", as Fix-It Clinics do. I put the brass bell on the door instead, as the café's identity, and only a faint brass strike (16 dB down) under "Fixed!". Reason: a full bell on every repair, plus the device's own wake, plus the chime, would be three endings for one moment, and playtest 4 asked for one ending. His to overrule.

## 3. The cue table

**Columns.**
- **Cue**: its name in the bank.
- **Fires**: today's code, or the new moment.
- **Must say**: what it tells the player.
- **Len · built from**: length, then how it's made. ×n is the number of variations; "st" is semitones.
- **Voices · gap · varies**: how many can sound at once, the shortest gap between repeats, and the per-play pitch wobble.
- **Today**:
  - *placeholder*: synthesized in `PlaceholderSounds.cs`;
  - *silent*: no file and no placeholder;
  - *not in asset*: the committed bank asset lacks the cue;
  - *no bank entry*: no bank anywhere has the cue.

Each cue's suggested volume is in the manifest: a starting point set 3 dB under its target, so any cue can be turned up.

### Screws
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `screw.turn` | each **quarter** turn of the hold (`Screw.cs:146–150`; change in §7.4) | the driver bites, it's moving | 20–40 ms · Kenney ticks and clicks ×5 | 3 · 0.03 s · ±6 % (optional rise with progress, §7.4) | placeholder, 2 per screw |
| `screw.free` | the last thread lets go (`Screw.cs:172`) | it's out | 0.18–0.21 s · switch click + muted-triangle ring 16 dB under ×3 | 3 · 0.05 · ±4 % | placeholder; not in asset |
| `screw.fly` | first frame of the flight to its cup (new) | follow it | 0.1 s · swoosh up 9–11 st ×2; volume 0.04 | 3 · 0.04 · ±6 % | new |
| `screw.drop` | last frame of the flight: in its cup (moves from the collision at `Screw.cs:212`) | safe, in its place | 0.22 s · muted triangle −6..+1 st, contact click, bounce at +55 ms ×5 | 4 · 0.05 · ±5 % | placeholder |
| `screw.seat` | driven home (`Screw.cs:269`) | tight, done | 0.15–0.32 s · switch click + placement thud ×3 | 2 · 0.05 · ±4 % | placeholder |
| `screw.fetch` | start of the cup-to-hole flight (`Screw.cs:233`) | it's coming back | 0.16 s · tick + swoosh ×2 | 2 · 0.05 · ±4 % | placeholder; not in asset |
| `screw.bounce` | retired: no physics in the core loop (rule 7) | — | — | — | placeholder |

### Covers
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `cover.creak` | the pry goes into the seam (`RemovablePart.cs:311`) | something is giving | 0.24–0.3 s · guiro scrape up 4–6 st, 0.7–5 kHz ×3 | 1 · 0.2 · ±5 % | placeholder; not in asset |
| `cover.pop` | the clip lets go (`RemovablePart.cs:369`) | it popped | 0.19–0.29 s · slapstick + switch click ×3 | 2 · 0.1 · ±4 % | placeholder; not in asset |
| `cover.lift` | the flight to the well starts (`:391`, `:530`); skip if within 0.15 s of the pop | on its way | 0.2 s · swoosh + soft click ×2 | 2 · 0.1 · ±4 % | placeholder; not in asset |
| `cover.clack` | it lands in the parts well (moves from the collision at `:457`) | put away, safe | 0.1 s · placement tap under 2.4 kHz + woodblock −14 st ×3 | 3 · 0.1 · ±6 % | placeholder; not in asset |
| `part.on` | it flies home and clips on (`:484`) | closed, clipped in | 0.24–0.29 s · switch click + lowered slapstick ×3 | 2 · 0.1 · ±4 % | placeholder |

### The two-hold swap
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `part.pinch` | the tweezers close (`ReplaceablePart.cs:181`, `:451`) | got it | 30–50 ms · mouse click and release up 3–4 st, a tick ×3 | 2 · 0.05 · ±5 % | placeholder; not in asset |
| `part.lift` | the broken part lifts with its wobble (`:240`) | the broken bit is out | 0.13 s · swoosh up 8–9 st + tick ×2 | 2 · 0.08 · ±4 % | placeholder; not in asset |
| `part.drop` | it lands in the old-parts bin (today the landCue at `BenchStage.cs:121`) | out of the way, with the scrap | 0.17 s · triangle clink −7..−9 st, shaker rattle, tin body, thud ×3 | 2 · 0.08 · ±4 % | placeholder; not in asset |
| `part.fresh` | a click on the fresh part: it flies over and hovers (new) | the new part is ready | 0.21 s · swoosh up 6–7 st, then a settle tick ×2 | 1 · 0.1 · ±4 % | new |
| `part.replace` | the press-in hold completes (`:324`) | it went in, properly | 0.15–0.24 s · click, then a snap and a thock 12 ms later ×3 | 1 · 0.1 · ±4 % | placeholder |

### The device
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `device.snap` | it settles after a quarter turn or a peek (`ItemInspector.cs:789`, `:830`, `:890`) | flat again (quietly: it fires constantly) | 0.1 s · placement tap under 1.5 kHz + woodblock −16 st ×3 | 2 · 0.1 · ±6 % | placeholder (a 600 Hz sine); not in asset |
| `device.flip` | it's flipped over (`:873`) | turned over | 0.3 s · swoosh landing in a soft tap ×2 | 1 · 0.3 · ±4 % | placeholder; not in asset |
| `phone.boot` | the phone wakes (`DeviceWake.cs:64`) | alive again | 0.95 s · vibraphone A (880 Hz), then E (1318 Hz) | 1 · 0.5 · none | placeholder; no bank entry |
| `watch.wind` | the crown is wound in three pulls (`:89`) | being wound | 0.79 s · three ratchet cranks an octave up, at 0 / 0.31 / 0.62 s | 1 · 0.5 · ±4 % | placeholder; no bank entry |
| `watch.tick` | then it ticks (`:107`) | it's alive | 2.45 s · tick and tock 5 a second, fading | 1 · 0.5 · none | placeholder; no bank entry |
| `camera.wind` | the film advance is wound (`:115`) | being readied | 0.3 s · ratchet crank up 3–4 st ×2 | 1 · 0.3 · ±4 % | placeholder; no bank entry |
| `camera.shutter` | it fires (`:117`; `ReplaceablePart.cs:329`) | an old film camera works | 0.27–0.36 s · a bright click, then a low clack 37 ms later ×2 | 1 · 0.2 · ±2 % | placeholder |

### Tools and cleaning
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `tool.pick` | a tool comes into the hand: a press on the cloth or brush, or LB/RB (`ToolPickup.cs:144`) | in your hand, instantly | 40 ms · UI clicks down 0–3 st ×3 | 2 · 0.06 · ±5 % | placeholder |
| `tool.down` | it lands back in its stand (`:145`, at the end of its flight home) | put back | 0.1 s · placement taps ×3 | 2 · 0.06 · ±5 % | placeholder; not in asset |
| `brush.rub` | **loop** while the brush is pressed on the device (§7.1) | the bristles are working | 2 s seamless · 180 grains a second of cabasa rubs and a shaker, 1.1–8 kHz | 1 loop · volume and pitch follow speed | new |
| `grime.scrub` | dust actually came off (`GrimeSpot.cs:395`) | dirt is coming off here | 70–80 ms · shaker and cabasa grains ×3 | 2 · 0.12 · ±8 % | placeholder |
| `cloth.rub` | **loop** while the cloth is pressed on glass (§7.1) | the cloth is wiping | 2 s seamless · Plan C noise + fibre flutter + real-glass grains | 1 loop · volume and pitch follow speed | new; replaces `cloth.wipe` |
| `cloth.squeak` | the stroke reverses while grease comes off (§7.2; today a 0.42 s timer, `:400`) | the glass is coming clean | 90–120 ms · wine-glass friction with a pitch glide ×4 | 1 · 0.18 · ±5 % | placeholder; not in asset |
| `cloth.lift` | the cloth lifts off a clean smudge (`ToolPickup.cs:298`), if Systems keeps the lift | lifted clear | 0.16 s · Plan C swish ×2 | 1 · 0.2 · ±4 % | placeholder; no bank entry |
| `cloth.gleam` | the gleam sweeps the glass (`GrimeSpot.cs:541`, 0.15 s late) | spotless | 0.48 s · mark-tree run + soft glockenspiel 8 dB under ×2 | 1 · 0.3 · ±3 % | placeholder; not in asset |
| `grime.clean` | a dirt spot is clean (`:543`) | that spot is done | 0.65–0.7 s · one glockenspiel note (G or C) ×2 | 2 · 0.2 · ±2 % | placeholder |
| `juice.sparkle` | every `Juice.Sparkle` (`Juice.cs:331`): always with `grime.clean` or `part.replace` | (a sweetener) | 0.24–0.3 s · tiny glockenspiel or mark-tree slice ×2; volume 0.04 | 2 · 0.08 · ±4 % | placeholder |
| `bench.showleft` | the "what's left" (show-dirt) button is pressed (new; UI/UX's name) | here's what's left | 0.38 s · soft rising mark-tree shimmer, 2D | 1 · 0.5 · none | new |

### The finish and the checklist
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `checklist.tick` | a step's icon ticks off (checklist v2), 0.12 s after the action's own sound | one more thing done | 60–70 ms · guiro slice up 7–8 st (a pencil "tsk") ×2, 2D | 1 · 0.15 · ±5 % | new |
| `repair.fixed` | "Fixed!" (`Juice.cs:253–254`) | you fixed it | 1.35 s · hand chimes A–E–A at 0 / 0.11 / 0.22 s + brass strike 16 dB under, 2D | 1 · 0.5 · none | placeholder |
| `juice.sparkle.big` | with "Fixed!" (`Juice.cs:249`) | (a sweetener) | 0.8 s · mark-tree run | 1 · 0.3 · ±2 % | placeholder |
| `repair.ready` | "Back together · Good" (`Juice.cs:268`) | back together, not perfect | 0.7 s · one E chime, 2D | 1 · 0.5 · none | placeholder; no bank entry |

### The café's neighbours
| Cue | Fires | Must say | Len · built from | Voices · gap · varies | Today |
|---|---|---|---|---|---|
| `door.bell` | anyone crosses the doorway (`CafeSoundscape.cs:188`) | someone came in | 1.2–1.25 s · two Nepalese brass bells 0.12–0.13 s apart, an E/A chime under ×2; 3D, full to 2.5 m, gone at 24 m | 2 · 1.5 s (a group rings once) · ±2 % | silent |
| `day.open` | the café opens (`DayIntro.cs:513`, `DayClock.cs:225`) | we're open | 1.9 s · brass bell + hand bell + A/E chimes, 2D | 1 · 2.0 · none | silent |
| `money.paid` | paid (`CustomerBrain.cs:1960`, `:2052`) | money in, softly | 1.0 s · the till's drawer, its bell filtered out (low-pass at 2.2 kHz) | 2 · 0.15 · ±4 % | silent |
| `cafe.room` | the day's bed (`CafeSoundscape.cs:109`) | a small, warm room | 30 s seamless · Plan C: air, a 60 Hz fridge hum and its harmonics, ventilation | bed | silent |
| `intro.sting` | under the day's title (`DayIntro.cs:391`, `:444`, `:473`) | a new day at Ace's | 1.6 s · piano A 440, A 880, C# 1114, E 1321 rolled 0.11 s apart, a vibraphone A and a high chime on top | 1 · 1.0 · none | placeholder; not in asset |
| `intro.sign` | the OPEN sign turns (`DayIntro.cs:1722`, `:1757`) | the sign turns | 0.17 s · woodblock, two knocks 55 ms apart | 1 · 0.5 · ±2 % | placeholder; not in asset |
| `intro.lamp` | the magnifier lamp comes on (`DayIntro.cs:1411`) | the bench is ready | 70 ms · switch click + LED tick | 1 · 0.5 · ±2 % | placeholder; not in asset |

**Not in this pass** (placeholder or silent until his recordings or Sonniss): `cafe.murmur`, `street.day`, `cup.*`, `drink.*`, `item.pickup` and `item.putdown`, `money.tip`, `ticket.new`, `chair.*`, `intro.grind`, `intro.pour`, footsteps, the circuit's cues and the night's.

**Under load.** The player has 24 voices; when they're full, the oldest voice that isn't UI makes way (`SoundPlayer.cs:165–182`). The bench's cues last 0.03–0.4 s, so even a quick run of actions holds about 6 at once. Unity's real-voice limit is 32 (`m_RealVoiceCount` in `ProjectSettings/AudioManager.asset`). A busy café heard from the bench can pass that:
- 3 beds;
- 2–4 dispensers, at 2 sources each;
- the loops;
- the pool of one-shots.

When it does, Unity drops the least audible sources. §6 step 8 raises the limit to 48.

## 4. The files

### 4.1 Where they are
- **`~/cafe/audio/out/files/`**: 96 WAVs named `<cue>_<nn>.wav`; mono, 44.1 kHz, 16-bit, peak −1 dBFS; 5.7 MB.
- **`~/cafe/audio/out/manifest.json`**: for each cue:
  - its files and their lengths;
  - when it fires and what it must say;
  - the exact recipe;
  - every source file used, with repo, commit, licence and licence file;
  - the Freesound originals, where there are any;
  - the suggested bank settings: volume, bus, 2D/3D, near/far, gap, voices, wobble, loop.
- **`~/cafe/audio/out/licences/`**: each source's licence text.
- **`~/cafe/audio/out/preview/`**:
  - `all-cues-at-suggested-volume.wav` (85 s);
  - `timestamps.txt`, saying which file plays when;
  - `spectrograms-first-file-per-cue.png`.
- **`~/cafe/audio/src/<source>/`**: the downloads, each with its licence beside it.
- **`~/cafe/audio/tools/`**: `build_sounds.py`, which remakes every file exactly, plus the checks (`verify.py`, `analyse.py`, `contact_sheet.py`).

### 4.2 Licences (each read in the source)
| Source (commit) | Licence, in its own words | Used for |
|---|---|---|
| Kenney, UI SFX Set, via `github.com/Calinou/kenney-ui-audio` (8c3d81b) | "License (Creative Commons Zero, CC0) … You may use these assets in personal and commercial projects." (`LICENSE.txt`) | clicks: free, seat, pop, snap, pinch, grab, shutter, lamp |
| Kenney, Interface Sounds 1.0, via `github.com/Calinou/kenney-interface-sounds` (4596a49) | "License: (Creative Commons Zero, CC0)" (`LICENSE.txt`) | 10–20 ms ticks and one contact click only |
| Kenney, Starter Kit City Builder, `github.com/KenneyNL/Starter-Kit-City-Builder` (4535092) | "Assets included in this package (2D sprites, 3D models and sound effects) are CC0 licensed" (`README.md`, line 70; the code is MIT and not used) | placement taps |
| Sonic Pi samples, `github.com/sonic-pi-net/sonic-pi` `etc/samples` (f4c6804) | "from http://freesound.org and have also been placed in the public domain via the Creative Commons 0 License" (`README.md`, each original linked) | brass bell (Freesound 221515), till (209578), swoosh (415580) |
| VCSL, Versilian Studios, `github.com/sgossner/VCSL` (c1ea7bc) | the CC0 1.0 legal code (`LICENSE`); "you can do whatever you want with these sounds (even make commercial software), no royalties, no credit" (`README.md`) | metal, wood, friction, bells, chimes, piano, vibraphone |
| uisfx 0.4.0 (npm), `LICENSE-AUDIO` from its GitHub | "dedicated to the public domain under the Creative Commons CC0 1.0" (its generated audio) | not used; kept for UI/UX |

None of them requires a credit. A "thanks to Kenney and Versilian Studios" line in the game's credits is a courtesy.

### 4.3 How good they are, honestly
- **Checked, but not heard.** I checked by measurement (length, level, decay, pitch, clicks at the ends, loop seams, DC: all pass) and by eye on spectrograms. The spectrograms caught three synthetic sources I had chosen; they're gone.
- **Some swaps will be wrong to the ear.** That's what the preview is for: he marks each cue keep or swap before import.
- **Recorded and derived (42 cues).** Real recordings of other objects, cut and pitched. The clicks, bells, chimes and piano should be clearly better than the placeholders. The swaps to judge first are the guiro for the clip's creak, the cabasa for the brush, and the triangle for the screw cup.
- **Procedural, Plan C (`cloth.rub`, `cloth.lift`, `cafe.room`).** These are noise by nature, so synthesis gets close.
  - `cloth.rub` is continuous and follows the hand, which is most of the feel. Up close it reads as "a soft hiss" more than "a cloth": it has no real fibre crumple and no irregular stick-slip.
  - The room tone is a believable floor of fridge hum and air, but has none of a real room's small life.
  - His shots 3 and 10 replace them.

## 5. His recording list (20 minutes, phone)

**Set-up (2 minutes).**
1. iPhone Voice Memos: Settings › Apps › Voice Memos › Audio Quality › **Lossless**.
2. Phone on silent and in Airplane Mode, so nothing pings.
3. A quiet room at night, door shut. No music, no talking.
4. Hold the phone's bottom edge (the mic) **15 cm** from the object, the same for every take.
5. **One recording per shot: 3 takes in it, 2 s of silence between takes.**
6. Rename each memo to the name below.
7. AirDrop them to the Mac into **`~/Documents/FixitFidget/Recordings/`**. Make that folder first, and see Flag 7 before using it.

The Producer cuts and imports them.

**Must: the bench (12 minutes)**
| # | Name | The sound | How |
|---|---|---|---|
| 1 | `rec01_screw_cup` | a small screw landing in a metal cup | drop a small screw from 10 cm into a metal bottle cap or small metal bowl; 10 drops |
| 2 | `rec02_screwdriver` | a driver backing a screw out | a small screwdriver, three slow turns out of wood or plastic, the phone 10 cm from the screw |
| 3 | `rec03_cloth_glass` | **microfibre on glass (the most important)** | a glasses cloth on a phone screen or window: 10 s of slow strokes, 10 s fast, then quick direction changes (the squeaks) |
| 4 | `rec04_brush` | a stiff brush on plastic | a toothbrush or small paintbrush on a TV remote or keyboard: 10 s slow, 10 s fast |
| 5 | `rec05_clip_pop` | a plastic clip popping and snapping | pry a remote's battery cover open and click it shut; pull a pen cap off and push it on; 5 of each |
| 6 | `rec06_tweezers` | tweezers closing | close them 10 times, 5 cm from the phone |

**If there's time (8 minutes)**
| # | Name | The sound | How |
|---|---|---|---|
| 7 | `rec07_parts_tin` | a small part into a tin | drop a key or bottle cap from 10 cm into a mug or tin; 10 times |
| 8 | `rec08_set_down` | a phone set down on a soft mat | lay a phone on a mouse mat or folded towel, 10 times (`device.snap`) |
| 9 | `rec09_coins` | coins | 3 coins into a mug, then into a glass jar; 5 of each |
| 10 | `rec10_room` | his kitchen's quiet | 2 minutes at night, fridge running, nobody moving (`cafe.room`) |
| 11 | `rec11_bell` | any small bell | a desk or keyring bell, or a glass tapped with a spoon; 5 times |
| 12 | `rec12_watch` | a watch ticking, a crown winding | the watch 3 cm from the phone for 10 s; wind its crown 3 times if it has one |
| 13 | `rec13_grinder` | the coffee grinder | 3 s, 3 times (`intro.grind`) |
| 14 | `rec14_steps_pavement` | his own steps on a pavement | outside, phone at hip height, 20 steps at a walk (Cinematics' `intro.steps`) |
| 15 | `rec15_car_pass` | a car passing | at the kerb of a quiet street, 3 cars going by (Cinematics' `intro.car`) |

Skip anything he doesn't have. The list doesn't include recording people in a real café: the murmur waits for the Sonniss restaurant ambiences.

## 6. Import (the Producer; Unity open, not in Play mode)

1. **Make the cues exist first.** Systems' bank entries land first (§7.6). Then run **Fixit Fidget › Sound › Create or update the sound bank**. The Console should say **35** cues were added: the 20 the committed asset lacks, plus 15 new entries.
2. **Bring the files in.** In the Project window make `Assets/Audio/CC0/`, and drag in `out/files/*.wav`, `manifest.json` and `licences/`. Don't use `Assets/Audio/Licensed/`: that folder is git-ignored for the Sonniss files. CC0 files can be committed.
3. **Default import settings.** Select all the WAVs, then in the Inspector:
   - **Force To Mono** on (they're mono already);
   - **Normalize** off;
   - **Load In Background** off;
   - Default platform: **Load Type** Decompress On Load, **Preload Audio Data** on, **Compression Format** ADPCM, **Sample Rate Setting** Preserve Sample Rate;
   - **Apply**.
4. **The tonal ones.** Select every file of `repair.fixed`, `repair.ready`, `door.bell`, `day.open`, `intro.sting`, `grime.clean`, `cloth.gleam`, `juice.sparkle`, `juice.sparkle.big`, `phone.boot` and `bench.showleft`. Set **Compression Format** to Vorbis, **Quality** 80, then **Apply**. ADPCM's hiss hides under noisy sounds but is audible on pure tones.
5. **The room tone.** Select `cafe.room_01`, set **Load Type** Compressed In Memory, Vorbis, Quality 60, then **Apply**. It's 30 s long. The two rub loops stay ADPCM and Decompress On Load: they're short, they play constantly, and they must stay seamless.
6. **Fill the bank.** Run **Fixit Fidget › Sound › Fill the bank from the manifest** (§7.7). Its report says "45 cues filled". To do it by hand instead:
   - select `Assets/Data/Resources/Sound bank.asset`;
   - for each cue the manifest names, set **Clips** to its files;
   - set **Volume**, **Near**, **Far**, **Min Gap**, **Max At Once**, **Pitch Wobble**, **Volume Wobble**, **Three D** and **Bus** to the manifest's `bank` values.
7. **Check.** Run **Fixit Fidget › Sound › Check the sound setup (read-only)**. `Logs/Sound/sound-check-*.txt` should list 45 cues with files and no "<-" advice lines.
8. **Audio settings.** In **Edit › Project Settings › Audio**:
   - **DSP Buffer Size**: Good latency (today it's Best performance, `m_DSPBufferSize: 1024`);
   - **Max Real Voices**: 48 (today 32).

   If the Mac crackles under load, put the buffer back.
9. **Play it.** Play a Day 1 bench (the camera clean), then Day 2's shutter swap, with the café busy. Then run the listening test (sound plan §7.7): a quiet moment and a rush, from both views.

A cue's placeholder stops the moment the cue has a file (`SoundPlayer.cs:132`). Nothing else changes.

## 7. The code hooks (Systems builds them; no code here)

**7.1 The rubs follow the hand (`brush.rub`, `cloth.rub`).**

*Where:* the bench's scrub branch (`ItemInspector.cs:535–564` today, or wherever Systems' direct grab puts it).

*The number:* the **stroke speed in reference pixels per second**, i.e. the pointer's on-screen speed while the tool is pressed on the device, scaled to 1080p.
- `raw = |GamePointer.Delta| / dt × 1080 / Screen.height`.
- While a pad scrubs in place, add `padScrubRate × 1080 / Screen.height`. That's the same rate the dirt uses (`ItemInspector.cs:993`).
- Smooth it in closed form, so 15 fps and 60 fps agree: `s += (raw − s) × (1 − exp(−dt / 0.06))`.

*Why pixels, not metres:* it's his hand's speed he feels (Unpacking tied its shake to mouse speed). Pixels at 1080p don't depend on the screen, and they won't change when the Technical Director rescales the devices.

*The mapping:*
- **Volume:** `v = pow(clamp01((s − 40) / 960), 0.6)`. Silent below 40 px/s; full at 1,000 px/s, a brisk scrub.
- **Pitch:** `p = 0.92 + 0.16 × clamp01(s / 1200)`.
- **Brush grit:** multiply `v` by 1.0 if dirt came off in the last 0.15 s, else by 0.55. `GrimeSpot.Scrub` returns whether dirt came off (`GrimeSpot.cs:404`).

*Its life:*
- On press, start it with `Sfx.Loop(cue, toolTip)` and set `FadeSeconds = 0.06`.
- Each frame, set `Volume = v` and `Source.pitch = p` (`SfxLoop` exposes both, `SoundPlayer.cs:363–368`).
- On release, or when the tip leaves the device, call `Stop(0.12)`.
- `Sfx.Loop` returns null when the cue has no file (`SoundPlayer.cs:191`): guard for that.

**7.2 The squeak.** Replace the 0.42 s timer (`GrimeSpot.cs:400`). Play `cloth.squeak` when all three hold:
- the smoothed stroke direction reverses: `dot(v, vPrev) < −0.3 × |v| × |vPrev|`;
- the speed is at least 250 px/s;
- grease came off in the last 0.15 s.

The bank's 0.18 s gap stops it chattering.

**7.3 What to retire.** Remove these calls; don't just leave them without files, because a call with no file still plays its placeholder.
- `cloth.wipe` at `GrimeSpot.cs:399`. The labs' whole-spot hand (`:518`) may keep it.
- The collision sounds at `Screw.cs:204–213` and `RemovablePart.cs:452–458` (this includes `screw.bounce`), and `BenchStage`'s land cue (`BenchStage.cs:114–122`). They go with the physics.

**7.4 Screw ticks.**
- *Tick every quarter turn:* `Screw.cs:146` counts `Progress × turns × 4`. That's 12 ticks in a 0.9 s screw instead of 2.
- *Optional and small:* add a `pitch` argument to `Sfx.Play` and `SoundPlayer.Play`, multiplied in at `SoundPlayer.cs:145`. The ticks can then rise from 0.94 to 1.06 with `Progress` as the screw backs out, and fall as it seats: progress you can hear.

**7.5 The flights.** Every scripted flight plays its launch cue on its first frame and its landing cue on its last frame: the frame the piece arrives, never a physics contact.

| Moment | Cues, in order |
|---|---|
| a screw comes out | `screw.free` → `screw.fly` → `screw.drop` |
| a screw goes back | `screw.fetch` → (it hovers) → the ticks → `screw.seat` |
| a cover comes off | `cover.pop` → `cover.lift` (skip it within 0.15 s of the pop) → `cover.clack` |
| a cover goes home | `part.on` on arrival |
| the broken part | `part.pinch` → `part.lift` → `part.drop` as it lands in the bin |
| the fresh part | `part.fresh` on the click → `part.replace` when the press-in completes |
| a tool | `tool.pick` on the grab frame → `tool.down` as it lands in its stand |
| the checklist | `Sfx.Play2DLater("checklist.tick", 0.12f)` when an icon ticks |
| show dirt | `Sfx.Play2D("bench.showleft")` on the press |

**7.6 Bank entries.** Add these to `SoundBank.Defaults()` (`SoundBank.cs:101–254`), each with the manifest's `when` text and settings:
- *new:* `screw.fly`, `part.fresh`, `brush.rub`, `cloth.rub`, `checklist.tick`, `bench.showleft`;
- *already played by the code with no entry:* `cloth.lift`, `cloth.wipe`, `repair.ready`, `phone.boot`, `watch.wind`, `watch.tick`, `camera.wind`, `cup.bin`, `mess.clear`.

Also rewrite the `when` notes of `screw.drop` (its cup), `cover.clack` (the well) and `part.drop` (the old-parts bin).

**7.7 The bank filler (an editor step; optional; saves about 100 drags).** Add **Fixit Fidget › Sound › Fill the bank from the manifest** to `Assets/Editor/SoundSetup.cs`. It:
- reads `Assets/Audio/CC0/manifest.json`;
- for each cue, finds its clips by file name under `Assets/Audio/CC0/` and sets them and the manifest's `bank` values;
- supports Undo, and writes a report to `Logs/Sound/`;
- never touches a cue the manifest doesn't name.

## 8. Checks (what proves it)

- **The sound check** (§6 step 7): 45 cues with files, no import advice lines, no missing cues.
- **The bench lab and the clumsy-hand lab** (rule 3), at 60 fps and at 15 fps:
  - each action's cue is requested (`Sfx.Requested`) on the **same frame** as the action: free, landing, pop, snap, grab;
  - `screw.drop`, `cover.clack` and `part.drop` are requested on the flight's last frame;
  - `screw.bounce` and `cloth.wipe` are never requested;
  - the rub loop is alive while the tool is pressed and gone within 0.15 s of release;
  - the logged `s` against the loop's volume rises steadily, with no jump bigger than 0.1 between frames at 15 fps.
- **Load:** a bench session with the café busy keeps the player's voices in use at 24 or fewer, and Unity's playing sources at 48 or fewer (Profiler › Audio).
- **His ears:** the preview reel first (keep or swap each cue), then the game, in a quiet moment and in a rush, from overhead and at the bench. As always, that is the ground truth.

## 9. Decisions his to overrule

1. The screw cups are metal (a tink; a steel disc in each cup), not silicone (a tup).
2. Every screw flight has a very quiet 0.1 s whoosh (volume 0.04). Silence it if three in a row clutter.
3. The old-parts bin sounds like a small tin: a clink and a rattle.
4. The brass bell is on the door, tuned into the café's A major. "Fixed!" gets only a faint brass strike, not a bench bell of its own.
5. "Fixed!" is three rising chimes (A–E–A), 1.35 s. "Back together" is one soft E.
6. The checklist ticks with a pencil "tsk" 0.12 s after the action (UI/UX's spec times it the same). The "what's left" button shimmers.
7. The till is its drawer only, with the bell filtered out (no cha-ching). No coins until shot 9.
8. The phone wakes with two soft vibraphone notes, A then E.
9. The driver ticks every quarter turn (12 per screw), optionally rising in pitch.
10. The volumes are computed starting points, 3 dB under target (in the manifest), to be set by ear.
11. The CC0 files go in `Assets/Audio/CC0/` and are committed, which the licence allows. The Sonniss files stay git-ignored.
12. The DSP buffer goes to Good latency, and the real-voice limit to 48.

## 10. Open question

1. "Fixed!": keep the chimes with a faint bell under them? Or give the bench its own brass bell ring, as Fix-It Clinics do, as a bigger moment?

## 11. Flags

1. **The committed bank is behind the code.**
   - `Assets/Data/Resources/Sound bank.asset` at `fab65d3` has 96 cues; `SoundBank.Defaults()` has 116.
   - All 20 missing cues belong to the bench v2 or the intro: `screw.free`, `screw.bounce`, `screw.fetch`, `cover.*`, `part.pinch`, `part.lift`, `part.drop`, `tool.down`, `device.*`, `cloth.squeak`, `cloth.gleam`, `intro.*`.
   - "Create or update the sound bank" hasn't been run since 7 Oct. The Mac's working copy may differ: unverified.
   - Files can't be attached to cues that aren't there, so §6 step 1 comes first.
2. **Nine cues are played with no bank entry anywhere:** `cloth.wipe`, `cloth.lift`, `repair.ready`, `phone.boot`, `watch.wind`, `watch.tick`, `camera.wind`, `cup.bin`, `mess.clear`. And `part.off` is in `Defaults()` but never played.
3. **No mixer exists.** `Sound bank.asset` line 15 reads `mixer: {fileID: 0}`, and there's no `Assets/Audio/Game.mixer`. The sound plan's 5-minute step (§4.2) never happened, so everything plays straight to the speakers. That's fine for this pass; the mixer is needed before any ducking.
4. **Kenney's "Interface Sounds" are mostly synthesized UI sounds.** Its drops and scratches are sweeps and FM wobbles. Only its 10–20 ms ticks and one click are used.
5. **No new mechanic.** Every cue marks an event the game already has, or one his approved changes create (common brief §6: the flights, the two-hold swap, the direct grab, show dirt, checklist v2). Pitch rising with progress and the speed-following rubs tell the player nothing the screen doesn't.
6. **The feel sheet** (`~/cafe/feel/feel-sheet-2026-10-14.md`) wasn't there when I finished (06:52 on this machine's clock).
   - It could disagree with me on "the longest time between an input and its sound".
   - In my design, the sound is requested on the input's own frame.
   - Today's output delay is 1,024-sample buffers, of which Unity queues several: roughly 40–90 ms (unverified on the Mac). Add up to one frame on top: 50–67 ms at 15–20 fps.
   - §6 step 8 halves the buffer part.
7. **`~/Documents/FixitFidget/Recordings/` is inside the repo's working tree.** The repo is `~/Documents/FixitFidget`. Add `/Recordings/` to `.gitignore` before he drops files there, or GitHub Desktop will offer to commit his raw takes.
8. **The Sonniss pass** (sound plan §3, §7) is still pending; these CC0 files bridge the gap. GDC 2018 and 2020 remain the source for the café's murmur, cups, the grinder and the night.
9. **Cinematics' intro v2** (`~/cafe/specs/cinematics-intro-drinks-zoom.md`) asks for new cues: `intro.car`, `intro.cup`, `intro.lights`, `intro.steps` and a rain bed. None is made in this pass, because nothing reachable fits. They come from the Sonniss pass, or from his shots 14 and 15 for the steps and the car. The cues it keeps (`intro.sting`, `intro.sign`, `intro.lamp`, `day.open`) are all made here. The sting's first note sits on frame 0, as that spec asks.
10. **Unverified:**
   - I can't hear, and I didn't run Unity.
   - The Mac's bank asset may differ from the committed one.
   - The import-setting names are Unity 6's as I know them.
11. **Paths.** `~` in this workspace is `/root`, and the brief files were under `/home/claude/cafe/brief/`. Every output is under `/root/cafe/`.

## Files produced

- **This spec:** `/root/cafe/specs/audio-bench-sounds.md`.
- **The sounds:** `/root/cafe/audio/out/files/`, 96 WAVs for 45 cues:

  | Group | Files |
  |---|---|
  | Screws | `screw.turn` ×5, `screw.free` ×3, `screw.fly` ×2, `screw.drop` ×5, `screw.seat` ×3, `screw.fetch` ×2 |
  | Covers | `cover.creak` ×3, `cover.pop` ×3, `cover.lift` ×2, `cover.clack` ×3, `part.on` ×3 |
  | The swap | `part.pinch` ×3, `part.lift` ×2, `part.drop` ×3, `part.fresh` ×2, `part.replace` ×3 |
  | The device | `device.snap` ×3, `device.flip` ×2, `phone.boot`, `watch.wind`, `watch.tick`, `camera.wind` ×2, `camera.shutter` ×2 |
  | Tools and cleaning | `tool.pick` ×3, `tool.down` ×3, `brush.rub` (loop), `grime.scrub` ×3, `cloth.rub` (loop), `cloth.squeak` ×4, `cloth.lift` ×2, `cloth.gleam` ×2, `grime.clean` ×2, `juice.sparkle` ×2, `bench.showleft` |
  | The finish | `checklist.tick` ×2, `repair.fixed`, `juice.sparkle.big`, `repair.ready` |
  | The café | `door.bell` ×2, `day.open`, `money.paid`, `cafe.room` (loop), `intro.sting`, `intro.sign`, `intro.lamp` |

- **Alongside them:**
  - `/root/cafe/audio/out/manifest.json`;
  - `/root/cafe/audio/out/licences/` (5 licence files);
  - `/root/cafe/audio/out/preview/`: the 85 s reel, its timestamps and the spectrogram sheet.
- **The sources:** `/root/cafe/audio/src/`, six sources with their licence files beside them: `kenney-ui-audio`, `kenney-interface-sounds`, `kenney-city-builder-kit`, `sonic-pi-samples`, `vcsl`, and `uisfx` (not used).
- **The build:** `/root/cafe/audio/tools/`: `build_sounds.py` and its checks.
