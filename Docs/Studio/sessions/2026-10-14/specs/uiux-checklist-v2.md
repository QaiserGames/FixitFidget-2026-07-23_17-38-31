# Checklist v2: the paper card, the glow, "what's left", and the bench's word budget

*UI/UX lead (Opus 5.5, max), 14 Oct 2026, playtest 5's "forgiving bench" session. Read at `playtest-2` (Mac head `fab65d3`). Spec only: no code this wave. Built in the Producer's Unity B, after Systems' forgiving bench (Unity A).*

## What he will see

He opens Grace's camera at the bench and it fills the middle of the screen. Top right, under today's takings, is a small cream card with Grace's colour of tape on it: **Grace: "The lens and film path are dirty."** Under that are two little pictures of the bench's own tools: the cloth, big, with **Wipe** under it, and the brush, small, waiting its turn. On the bench the real cloth glows soft mint and breathes. He grabs it, rubs the lens, and the cloth picture ticks with a click. Then the brush glows. The big words at the bottom of the screen are gone. This once, four or five short lines in the bottom-right corner say what the buttons do. If he misses a speck, Tab (View on a pad) flashes everything still dirty in yellow. At the end he gets "Fixed! · Perfect", and the card says **[E] Take it to Grace**. From the second repair on, the bench shows about fifteen words, and the controls are one press of "[Tab] Help" away. At the till, right after he picks the camera up, E no longer offers to put it straight back.

## 1. What I did

- I read the common brief, my brief and role file, the Bible (§2, §4, §5), the four Project docs, and the code and checks the brief lists. I also read the stage builder, the close-up camera in the scene, `FaultSpeech`, `WordBudget`, `HudCorners`, `PausePhone`, `CafeViewMode` and Grace's camera prefab, to get positions, bindings and words exactly right.
- I counted every word on today's first-repair screen: **129 with a mouse, 138 with a pad**, or 119 without the HUD corners, which matches the First Hour's "about 120". This design brings it to **31 / 37** on the first repair and **about 15** in steady play.
- I checked three outside sources: ReStory's notepad placement, PowerWash's dirt indicator, and Xbox accessibility guideline 103 (sources at the end). The feel sheet had not landed when I finished (§10).
- Not done: the words themselves. Every line here is placeholder copy for his sister (the Content role owns the words).

## 2. The decisions and their reasoning

| Call | My pick | Why | Rejected |
|---|---|---|---|
| Where the card sits | **Top right, under today's takings** | ReStory's notepad is on the right. The band above the mat is the bench's back wall, with nothing to touch there. The tab rail already stops 380 px short of the right edge (`TicketRailUI.cs:29`). | **Top centre** (today's strip): from the 4th tab the rail (24 + 4 × 228 = 936 px) runs under a centred card, and a tall device's top lands there. **Bottom centre**: the mat's near edge, where the screw cups will likely sit. **A 3D ticket on the bench**: words in the world break the "objects are objects" lock, and they read badly at the camera's 58° tilt. |
| What the card says | **Name + her words** (one line, ≤ 8 words), then **one tool icon per step**: the current one big with a 1–2 word verb; pips for steps with several targets | The customer's spoken form of every fault already exists (`FaultSpeech.cs:29-44`). Icons carry the order with no words. Pips answer ReStory's "which screws are out?" complaint. | The fault label ("Cracked Screen": the Title Case the First Hour flagged). A sentence per step (today: 14 words). One icon per dirt spot (8 icons on the watch). |
| The help key | **Tab / View: a tap shows what's left (1.5 s); holding 0.35 s shows every control** | Tab is PowerWash's key for exactly this; its developers describe it as "flashing it yellow". Tab/View is already "hold for controls" (`BenchCards.cs:104`). One button covers "I'm stuck". | **D-pad right** (PowerWash's pad key): it is "next part" here. **LT / Space**: they boost a running circuit (`CircuitPuzzle.cs:135-136`). **R3 / F**: a second help button to learn. |
| Hint when the controls are hidden | **"[Tab] Help"** (2 words, bottom right) | The pause phone cannot open in a close-up (`PausePhone.cs:115-116`), so the bench screen is the only place its controls can be found. | **No hint**: a stuck stranger has no way back. **The card always up**: 17–23 words, every repair. |
| "First repair" | **Day 1, until the first repair is handed back** (`DayClock.Day == 1 && DayClock.Repairs == 0`; `DayClock.cs:112, 450`) | Worked out from the day, so every fresh game shows it, including to playtest 6's stranger. | Today's rule: three close-ups counted in PlayerPrefs (`BenchCards.cs:35-36, 95`). That count survives "Fresh game", so a stranger on his Mac would never see the card. |
| The glow | **The next thing to touch**: the step's tool in its stand until it is in hand, then the step's targets. Mint (`UiSkin.BrandBright`, which `UiSkin.cs:49` already names "a glow"), breathing every 1.6 s. An outline on parts, a ring on screws, holes and dirt. Dashed when hidden. | Shape + motion + colour: XAG 103 asks for a second channel beside colour. One signal family everywhere. | **A colour tint**: fails for colour-blind players and fights the dirt's own look. **Arrows**: words by other means. |
| Words at the bench | **Tabs 0 words; HUD corners quiet; the prompt line, "Clean: …" and the Day 1 card gone; hover = the part's name** (+ its own "why not" when blocked) | Says the job once, in one place. | Leaving the tabs as they are (16 words for Grace's alone) |
| The pickup | **"Put it back"** for a device lifted from this tray (any day). **On Day 1's carry step the tray doesn't offer it at all.** | E and the card agree on Day 1, and the words are honest every other day. | A lockout every day: a wrong pick from a full tray would then cost a walk away and back. A timer: an invisible rule. |

## 3. Decisions his to overrule

1. The card sits top right under the takings, not top centre as today.
2. The card's line is the customer's spoken words (`FaultSpeech`), ≤ 8 words with the name. Two of today's forms are too long (§5.2).
3. One icon per step, with pips (not one per dirt spot); at most 7 icons (the watch's seized movement).
4. The icons are renders of the bench's own models: flat colours, a dark outline, laid diagonally. His girlfriend could redraw them later.
5. Tab / View: a tap shows what's left, a hold shows the controls (rather than a separate key).
6. "[Tab] Help" stays on screen after the first repair.
7. "First repair" means Day 1, until a repair is handed back (no PlayerPrefs).
8. The first-repair card leaves out turning, flipping and zoom (Day 1's clean needs none of them).
9. At the bench, the tabs keep only the initial, the tape and the bar.
10. At the bench the HUD corners hide "TODAY", "COME IN" / "CLOSING AT …" and the day line.
11. No prompt line at the bench; the card's finished row carries the way out.
12. No Day 1 card at the bench. The impatience card gets bench words (today's names the wrong key).
13. The glow is always on, mint, breathing every 1.6 s; dashed when hidden; the whole device glows when the work is underneath.
14. Stuck: a pulse after 12 s with no progress, and the controls card returns after 20 s.
15. "What's left" uses PowerWash's yellow (our gold), lasts 1.5 s, and sends out sonar-style rings.
16. "Put it back" on any day; on Day 1's carry step, no offer to put it back at all.
17. The hover keeps a part's own "why not" ("Held in by the back cover…", which the First Hour asked to keep).
18. Alerts (impatience, a stock chip) have their own budget (≤ 12 words), outside the steady 25.

## 4. Unity steps

None this wave. The Producer's Unity B session carries them with the build.

## 5. The deliverable

### 5.1 The bench screen, word by word: before and after

How words are counted: `WordBudget.Words` (`WordBudget.cs:37-47`), the studio's existing rule: a word is a run between spaces that contains a letter or a digit. The screen measured: Grace's camera on Day 1, the first screen at the bench, with the cloth not yet taken and the cursor on the lens.

| # | Element (where the words come from) | Where, type size | Today, mouse | Today, pad | v2 |
|---|---|---|---|---|---|
| 1 | Grace's tab: name, mood, device, fault label (`JobTicket.cs:103-105`, `CustomerBrain.cs:583-608`) | top left, 17 / 11 / 14 px | 16 | 16 | chip: initial, tape, bar → **0** |
| 2 | Checklist header: "Grace's reunion film camera · dirty lens and film path, jammed shutter; keep the scratched strap" (`BenchCards.cs:119-124`) | top middle, 16 px | 15 | 15 | the card's line → **8** |
| 3 | Step + how: "Wipe the smudge / Cloth (click it in the caddy) · hold and wipe in circles" (`BenchCards.cs:126-136`) | 22 / 18 px | 14 | 11 | "Wipe" under the big icon → **1** |
| 4 | Chip row (`BenchCards.cs:143-155`) | 15 px | 10 | 10 | icons → **0** |
| 5 | Day 1 card: "DAY 1 · GRACE / Take the cloth (click it) to clean the smudge." (`DayOneGuideUI.cs:590, 967`) | bottom middle, 21 px | 12 | 13 | hidden at the bench → **0** |
| 6 | Prompt line "[E] Pick up item and step back" (`ItemInspector.cs:210`) | bottom middle, **36 px, the largest** (scene `AcesCafeLayoutPlaytest.unity:14112, 14181`) | 7 | 7 | gone at the bench → **0** |
| 7 | "Clean: lens, film path, film guide · replace shutter" (`GraceCameraRepairJob.cs:12-21`, appended at `ItemInspector.cs:209`) | 36 px | 8 | 8 | gone → **0** |
| 8 | Controls card (`BenchCards.cs:163-184`) | bottom right, 18 px | 33 | 44 | first-repair card → **17 / 23** |
| 9 | Hover on the lens: "Smudge / Needs the cloth" (`GrimeSpot.cs:53, 60-62`) | at the cursor | 4 | 4 | "Lens" → **1** |
| 10 | HUD corners: TODAY, $0, OPEN, COME IN, Monday · Day 1, 9:05 AM (`HudCorners.cs:180, 543-544, 566`) | top right, bottom left | 10 | 10 | quiet: "$0", "OPEN", "9:05 AM" → **4** |
| | **Total** | | **129** | **138** | **31 / 37** (< 40) |

Today the job is said **four times**: in the tab, in the header, in "Clean: …", and once more as the Day 1 card repeats the step. In v2 it is said **once**.

**Steady play** (Day 2, Walter's watch with the seized movement, unscrewing, a screw under the cursor):
- Today: about **65** words, plus about 6 for each extra tab.
- v2: card 8 ("Walter: 'It's seized up and won't wind.'" + "Unscrew"), hint 2, hover 1, corners 4 = **15**.
- Worst case in v2: the longest card line, a flip verb, a blocked part hovered, at last orders = 12 + 2 + 5 + 5 = **24** (< 25).

Budgets, added to `WordBudget` as constants:

| State | Budget |
|---|---|
| Steady | `BenchSteady = 24` |
| First repair | `BenchFirstRepair = 39` |
| Any one alert | `BenchAlert = 12` |

### 5.2 The paper card

**Place and size** (1080p canvas units; `UiSkin.ScreenCanvas` scales with 1920 × 1080 as reference, match 0.5, `UiSkin.cs:274-288`):
- **Anchor**: top right, pivot top right, at (-24, -132). 24 is `UiSkin.Margin`. 132 is the takings box's bottom edge (18 + 92, `HudCorners.cs:172`) plus 22 px of room for the "+$" pop that rises into the stack.
- **Width** fits the content: at least 360, at most 480. **Height** 142, or 168 if the line ever wraps (the edit check forbids that for every known fault).
- **Inside**: 14 px padding left and right, 12 top and bottom. The line is 19 px (name in bold, the quote regular), 26 px tall, with a 10 px gap under it. Then the icon row: the current icon 56 px on a 64 px `BrandSoft` disc, the others 36 px, 10 px apart. The verb sits 4 px under the current icon, 17 px bold, in `Ink`. Pips are 6 px dots, 4 px apart, 4 px under the smaller icons.
- **Paper**: `UiSkin.Paper` at 96 % alpha, corner radius 8, `UiSkin.DropShadow(card, 8, 3, .3)`.
- **Tape**: 44 × 14 px at the top-left corner, overhanging the edge by 6 px, rotated -7°, in the job's colour at 92 % (as the tab's tape, `JobTicket.cs:158-160`). The tab, the tape, the device's tag and the card now share one colour (hud-spec §4).

| | 1920 × 1080 | 2560 × 1440 | 2560 × 1600 (his Mac's 16:10) |
|---|---|---|---|
| Canvas | 1920 × 1080, ×1 | ×1.333 | 1821 × 1138, ×1.406 |
| Card | 360–480 × 142, 24 from the right, 132 from the top | 480–640 × 189 at 32, 176 | 506–675 × 200 at 34, 186 |
| Line / verb | 19 / 17 px | 25 / 23 | 27 / 24 |
| Current / other icons | 56 / 36 | 75 / 48 | 79 / 51 |

**The line**: `{Name}: "{Spoken}."`
- Name is `CustomerName` ("Officer", not "the Officer").
- Spoken is `FaultSpeech.Spoken(record.faultDescription)`, with a capital and a full stop.
- A device with no owner (labs) shows the quote alone.
- Rule: ≤ 8 words and ≤ 46 characters with the longest customer name in use.
- Two forms fail today:

| Fault | Today's form | Why it fails | Placeholder for his sister |
|---|---|---|---|
| The watch's debris | "there's grit inside and it won't keep time" | 9 words, 54 characters with "Beatriz" | "There's grit inside; it won't tick." |
| Grace's camera | "the lens is dirty and the shutter's jammed, but the strap stays" | 13 words | Day 1: "The lens and film path are dirty." Day 2: "The shutter's still jammed." |

  Narrative's Day 1 / Day 2 labels decide Grace's split.

**Steps, verbs, icons, glow targets.** `RepairChecklist.Step` gains `verb`, `icon`, `targets` (what is still to do) and `left` / `total` (for the pips). The order of steps is unchanged.

| Step today (`RepairChecklist.cs`) | Verb | Icon key | Glow once the tool is in hand |
|---|---|---|---|
| Take out the N screws (:90) | Unscrew | screwdriver | a ring on each screw still in |
| Pry off the cover (:92) | Pry off | pry | the cover |
| Brush off the grime (:115) | Brush | brush | a ring on each spot left |
| Wipe the smudge (:228) | Wipe | cloth | a ring on each spot left |
| Lift out the part (:127) | Lift out | tweezers | the broken part |
| Seat the new one (:129) | Press in | part_<device>, else newpart | the fresh part, in its spot or over its seat |
| Reconnect the circuit (:135) | Reconnect | circuit (the board's own tile) | none (the board has the screen) |
| Cover back on (:143) | Cover on | cover_<device>, else cover | the cover in the parts well |
| Screws back in (:147) | Screws back | screw | a ring on each empty hole |
| Any step whose targets are all face-down (`under`) | **[R] / [Y] Flip it over** | the step's icon + a flip badge | the whole device |

Until the step's tool is in hand, the tool glows in its stand (on a pad: whichever tool LB/RB has selected). Icon counts per job: Day 1's clean has 2, Day 2's swap 2, the phone's cracked screen 6, the watch's debris 5, the watch's seized movement 7 (the most).

**Icon states.** Shape carries each state, not colour alone:
- **Done**: 50 % desaturated, with a 16 px `Brand` disc and a white check at the lower right.
- **Current**: full colour, 56 px, on the soft disc, with the verb under it.
- **To do**: full colour at 55 % alpha.

**The card's states:**
- **Working**, as above.
- **Flip**: the verb reads "[Y] Flip it over" (4 words). This is the First Hour's liked "on the other side: flip it over (Y)" in fewer words.
- **Everything ticked but not finished** (today's 1.1 s "Look it over", `BenchCards.cs:141`): all checks, no words. If it lasts 1.5 s, "what's left" fires once by itself.
- **Finished**: the checks hold for 0.6 s, then the row folds into one line: a 36 px check and **[E] Take it to Grace** (on a pad [X], `ControlHints.TakeFromBench`).
  - Hands full: **Hands full: free a hand first**.
  - No owner: **[E] Pick it up**.
  - The first-repair controls card leaves at "Fixed!".

**Motion and sound.** Everything runs on `UiClock` (unscaled), so it is frame-rate independent.
- **In**: the card slides 40 px down and fades in over 0.25 s (ease-out-back). **Out**: it fades over 0.15 s.
- **A tick**:
  1. The current icon pops 1.0 → 1.18 → 1.0 (0.22 s).
  2. The check stamps in (1.6 → 1.0, 0.18 s).
  3. The next icon grows from 36 to 56 px (0.25 s) and its verb fades in.
  4. `checklist.tick` plays 0.12 s after the world's own sound: the last screw's tink first, then the card.
- **Pips** fill with a 0.12 s pop and no sound of their own (the world action already has one).
- **Refresh**: the card re-reads the steps every 0.1 s, and also on the frame a target finishes (Systems' event). Today it refreshes every 0.15 s with no event (`BenchCards.cs:107-108`).

### 5.3 Icons (the brief for the Blender lead)

- **Generic set (8)**: screwdriver, pry, tweezers, brush, cloth (folded, as it sits in the stand), screw (a Phillips head at real proportions, three-quarter view from above so the cross reads), newpart (a clean plate with one bright edge), cover (a plain rounded back plate).
- **Per device, if time allows (5)**:
  - part_phone (the new screen)
  - part_watch (the mainspring)
  - part_camera (Art's clean shutter mechanism, brief A4)
  - cover_phone
  - cover_watch

  The circuit icon is the circuit board's own tile sprite, so it needs no render.
- **Render** with a new `Tools/Blender/render_bench_icons.py` (headless, listed in `Tools/Blender/README.md`), from the same scripts that build the bench (`bench_tools.py`, `hero_*.py`), at the Technical Director's (TD's) new real proportions.
  - Orthographic camera. Long tools lie diagonally, tip to the lower left, filling 80 % of the diagonal.
  - 256 × 256 px on a transparent background.
  - Flat-shaded in their own palette colours, one warm key light from the upper left, no ground shadow.
  - A `#1F1B17` outline, 8 px at 256 px, so it stays ≥ 1 px at 36 px.
- **Output**: PNG (sRGB) to `Assets/Data/Resources/UI/BenchIcons/<key>.png`. Import as Sprite (2D and UI), mipmaps on, bilinear, max size 256, no compression.
- **A missing icon** shows as a plain `UiSkin.Circle` in `InkFaint`. The card still works, because the verb and the glow carry it.
- **Review**: one contact sheet with every icon at 36 and 56 px on the card's cream (#F7F3EE), for his eye.

### 5.4 The glow on the next part

The targets are in the table in §5.2. The look:

| | How it looks |
|---|---|
| **Parts, covers, tools** | The bench's outline hull (`BenchOutline.cs`) in `BrandBright` #7FD3A8. Width breathes 1.0× ↔ 1.5× of the hover outline (`_Width` 0.0035 per metre) and colour ↔ `BrandSoft` #DCEFE1, on a sine with a 1.6 s period. |
| **Screws, holes, dirt** | A screen-space ring (3 px stroke at 1080p) around the target's projected bounds + 10 px (radius at least 14 px). Same colour and breathing: radius ±3 px, alpha 0.7 ↔ 1. |
| **Hidden behind something** (`ItemInspector.Seen` is false, `ItemInspector.cs:1139-1155`) | The same, dashed (4 px on, 4 px off) at 70 %. Hulls get a second shader pass with the depth test reversed; rings are drawn dashed. This answers the KeenGamer wish to "highlight parts hidden under other parts". |
| **Face-down work** (`Step.under`) | The whole device's outline breathes, and the card says flip. |
| **Precedence on one part** | A hold under way → the gold "busy" outline (`BenchOutline.Busy`, `BenchOutline.cs:19`). Under the cursor → white hover. Otherwise → the glow. |
| **When it stops** | A target is done: its glow goes with the tick. A step is done: the next step's glow starts 0.3 s later, so the eye goes to the tick, then back. At "Fixed!" and when the close-up ends: no glow. |
| **Stuck** (no progress on the step for 12 s) | One bright pulse (width 2.2×, white → mint, 0.5 s), and the help hint pulses (1.0 → 1.15 → 1.0 with a gold edge, 0.6 s). At 20 s the controls card returns (the first-repair lines) until the next tick. |
| **Colour-blind** | Mint (glow), white (hover) and gold (busy, what's left) are told apart by motion (breathing vs steady vs expanding rings) and by shape (outline, ring, dashed), not by hue alone. XAG 103 asks for shape, pattern or icon beside colour; this adds motion. |

The glow is always on in v2, with no setting (see Open questions).

### 5.5 The help key: the controls card and "what's left"

**What's free at the bench** (audited from the code):

| | Bound now | Free |
|---|---|---|
| Pad | right stick: hand; left stick: turn / peek (swap setting); A / RT: use; LB / RB: tool; Y: flip; X: take it away; B: back; D-pad ↔: next part; D-pad ↕: zoom; View held: controls | **LT** (only boosts a running circuit), **R3** (`CafeViewMode` returns early in the close-up, `CafeViewMode.cs:197-199, 300`), **L3** (night sneak), **Start** (does nothing: the phone is blocked, `PausePhone.cs:115-116`) |
| Keyboard / mouse | move, click, hold, drag, right-click, scroll; R: flip; E: take it away; Tab held: controls | **F**, **Q** (refuse is skipped in the close-up, `PlayerInteractor.cs:391-401`), **Space** (circuit only), **WASD** (Ace's legs are stopped) |

**The binding** is Tab / View (`ControlHints.BenchHelp`, unchanged):
- **A press** fires "what's left" at once, without waiting for release.
- **Held 0.35 s**: the full controls card shows while held. Today it shows on the first frame (`BenchCards.cs:104`); the 0.35 s keeps a tap from flickering it.
- **On the first repair**, the short card below shows without any press, and leaves at "Fixed!".
- **After that**, one hint stays up: **"[Tab] Help"** or **"[View] Help"**, 17 px on a band chip, bottom right.

**Where the controls card sits**: bottom right, 24 from the right edge and 26 from the bottom, 400 wide, 18 px. Keys are in `BrandBright`, words in `BandText` on `Band` (as today). When a stock chip shows, the card rises above it. Today's card at (-24, 96) (`BenchCards.cs:214`) overlaps the chips at (-24, 26), 320 × 100 (`HudCorners.cs:299`).

**The first-repair card, in full.** The words follow `ControlHints` and whatever bindings Systems finally picks.

Mouse (17 words):
```
[Click a tool]  Take it
[Hold click]  Use it
[Right-click]  Step back
[Tab]  What's left (hold: controls)
```

Pad (23 words; with the stick swap on, "[Left stick]"):
```
[Right stick]  Move your hand
[LB / RB]  Pick a tool
[A / RT]  Hold to use
[B]  Step back
[View]  What's left (hold: controls)
```

Turning, flipping and zoom are left off, because Day 1's three spots all lie on the camera's top face (`GraceReunionCamera.prefab`: lens grime y .100, film path and film guide y .058, local up). The flip is taught by the step that needs it ("[Y] Flip it over").

**The held card** has the full list and no word budget:
- Pad: Move your hand · Pick a tool · Hold to use · Turn it (up/down: peek) · Flip it over · Next part · Zoom · Step back · Pick it up · What's left.
- Mouse: Take a tool · Use it · Turn it (drag; up/down: peek) · Flip it over · Zoom · Step back · Pick it up · What's left.

**"What's left": the look.** Systems specifies the mechanism.

| t (s) | What happens |
|---|---|
| 0 | `bench.showleft` plays: a soft rising shimmer, ≤ 0.4 s (Audio adds the cue) |
| 0–0.1 | every dirt texel still left brightens to gold (`UiSkin.Gold` #F2A61F, emissive 1.5) |
| 0.1–0.6 | one ring per spot left expands from its edge to 2× (3 px, gold, alpha 1 → 0); a second ring at 0.5 s |
| 0–1.5 | every part with work left, in any step, is outlined in gold, steady; hidden ones dashed |
| 0.5–1.5 | everything fades out (ease-out) |

- If nothing is left, the device's outline flashes white once (0.4 s).
- A second press restarts the flash.
- No rumble.
- It also fires once by itself after 1.5 s of "everything ticked, not finished".

### 5.6 The Day 1 guide at the bench

- In `DayOneGuideUI.Present` (:931-985), at the bench (`bench`, :962) there is no guide card. The card's big icon, its verb and the glow are the guide.
- The steps it used to say there (`RepairBenchAction`, :515-613, e.g. "Take the cloth (click it) to clean the smudge.") can never show. Delete them, and the word helpers only they use.
- **The impatience card stays at the bench, with bench words.** Today it says "press E to reassure" (`ReassureWords`, :653), but at the bench E/X takes the device away and A uses the tool.
  - Bench words: **"Grace is getting impatient. [Right-click] Step back, then [E]."** On a pad: [B] … [A]. 9 words.
  - Place: bottom centre, 26 px up. `BenchCardBottom` (:925) goes from 164 to 26, because the prompt it sat over is gone.
  - At most 12 s, once a day, as now.
- Off the bench, nothing changes.

### 5.7 The pickup prompt (First Hour #4)

1. **`PlayerCarry.TryPickUp`** (:237-275): before it releases the drop spots (:251), it remembers which spot held the device (`LiftedFrom`). It forgets at `PlaceAt` (:276), at hand-back and at consume.
2. **`DropInteractable.Prompt`** (:28-43) and **`StationInteractable.Prompt`** (:172-182): at the intake tray, a device lifted from this same tray reads **"Put it back"**, on any day. Any other device keeps "Put it on the intake tray".
3. **`DayOneGuideUI`** gets a static `CarryingToBench`: the job its card is telling Ace to carry, set where the card says "Carry the … to the repair bench" (:500-501) and null otherwise. In **`DropInteractable.IsAvailable`** (:15-22) and **`StationInteractable.IsAvailable`** (:163-171), the counter's spot is not available for that job.

The result on Day 1: after the pickup, the line shows only "In hand …", and E does nothing at the tray. The card (carry it to the bench) and E agree. Grace's own E stays quiet, because her `JobReady` is false before any work (`Day1GuideCheck.cs:275`).

### 5.8 The stage contract: what the 3D bench must leave free

These are the screen zones at the bench (1080p canvas units, measured from the top left). Three things must stay out of zones U1–U5, at both 16:9 and 16:10:
- the device, at every quarter turn and on both faces;
- the mat's cups, parts well, old-parts bin and fresh-part spot;
- every tool in the stand.

The TD's framing and Art's mat must respect this.

| Zone | x | y from the top | Holds |
|---|---|---|---|
| U1 | 24–1408 | 20–64 | the compact tabs (up to 8) |
| U2 | 1596–1896 | 18–110 | today's takings |
| U3 | 1416–1896 (16:10: 1317–1797) | 132–300 | the paper card |
| U4 | 24–256 | 864–1060 | the sign, the bar, the time |
| U5 | 1496–1896 | 740–1054 | the controls card (held, raised above a chip) / the help hint / the stock chips |
| U6 | 670–1250 | 966–1054 | the impatience alert (brief; it must miss only the device) |

**In practice**, either the device's top stays below 27 % of the screen height, or its width stays within 39 % of the screen at 16:9 (43 % at 16:10). The camera body turned sideways is the case that binds.

**Today's stage already clears every zone, with one exception.** Projecting `Playtest3Session4Steps.cs:42, 103, 117-118, 136` through the 40° lens (scene :223417, camera at 58°, 0.58 m) puts:
- the tray at x 1292–1820, y 369–740;
- the caddy at x 134–459, y 340–678.

The tray's lower edge just meets U5's top only when the held card is raised over a stock chip. Without a chip, there are 42 px to spare.

### 5.9 Wireframes (1080p; not to scale, coordinates as above)

**W1. First repair** (Day 1, Grace's camera, pad, the cloth not yet taken)
```
+----------------------------------------------------------+
|(G)=====  (O)=====                               $0       | tabs: initial+bar
|                                     +------------------+ |
|                                     |#  Grace: "The ...| | paper card
|                                     | [CLOTH] (brush)oo| | cloth big, brush
|                                     |  Wipe            | |  small, 2 pips
|                                     +------------------+ |
|  [stand]       +----------------+        [well]          |
|  cloth GLOWS   | GRACE'S CAMERA |        [cups] [bin]    |
|  (mint,        |  lens  path    |                        |
|   breathing)   +----------------+                        |
| +-------+                        +----------------------+|
| | OPEN  |                        |[Right stick] Move y..||
| |=======|                        |[LB / RB] Pick a tool ||
| |9:05 AM|                        |[A / RT] Hold to use  ||
| +-------+                        |[B] Step back         ||
|                                  |[View] What's left (..||
+----------------------------------------------------------+
```
Card: `Grace: "The lens and film path are dirty."` / [CLOTH, current] [BRUSH, oo] / `Wipe`. Words: card 9 + controls 23 + corners 4 + hover 0–1 = **36–37** (< 40). With a mouse: **31**. Once the cloth is in hand, its glow moves to rings on the lens.

**W2. Steady play** (Day 2, Walter's watch, mouse, 2 of 4 screws out)
```
+----------------------------------------------------------+
|(G)=====  (W)=====  (P)=====                     $65      |
|                                     +------------------+ |
|                                     |#  Walter: "It's .| |
|                                     |[DRV]oo..(pry)(br)| | driver big,
|                                     | Unscrew (tw)(new)| |  pips 2 of 4
|                                     +------------------+ |
|  [stand]       +----------------+        [well]          |
|                |  WATCH, back up |       [cups] 2 screws |
|                |  o   (o)   x  x|  <- rings on 2 left;  |
|                +----------------+    one hovered: white  |
| +-------+                                                |
| | OPEN  |                                    +---------+ |
| |1:40 PM|                                    |[Tab]Help| |
| +-------+                                    +---------+ |
+----------------------------------------------------------+
```
Card: `Walter: "It's seized up and won't wind."` / 7 icons / `Unscrew`. Hover: `Screw`. Words: 8 + 2 + 1 + 4 = **15** (< 25).

**W3. A hidden part.** (a) The phone, face up; its screws are on the back.
```
|                                     |#  Nina: "The scr.| |
|                                     |[DRV+flip](pry)...| |
|                                     | [Y] Flip it over | |
|                +----------------+                        |
|                |  PHONE (whole  |  <- whole outline      |
|                |  outline glows)|     breathes          |
```
(b) Day 2, Grace's swap: the fresh mechanism's spot is behind the camera body from this angle. Its glow draws **dashed** through the body. Card: `Grace: "The shutter's still jammed."` / (tweezers, ticked) [NEW SHUTTER] / `Press in`. Words: (a) 1 + 3 + 4 + 2 + 4 = **14**; (b) **13**.

**W4. Finished**
```
|                                     +------------------+ |
|                                     |# [check] [E] Take| |
|                                     |  it to Grace     | |
|                                     +------------------+ |
|                +----------------+                        |
|                |  CAMERA, face  |  "Fixed! · Perfect"   |
|                |  up (no glow)  |   (Juice, 2.1 s)      |
```
Words: 5 + 2 + 2 + 4 = **13**. Today: about 70, counting "Done. Pick it up and take it back", the prompt, the Day 1 card's "Fixed! Press E to pick up the item and step back from inspection." and the controls card.

**W5. Help held** (Tab or View held for 0.35 s): the full card in U5, ten lines on a pad. The press itself also fired "what's left". There is no word budget here; the overlap rules still apply.

### 5.10 Files and data, in build order

1. **`Assets/Scripts/WordBudget.cs`**: add `BenchSteady = 24`, `BenchFirstRepair = 39`, `BenchAlert = 12`, `CardLineWords = 8`, `CardLineChars = 46`. It stays pure (the console tests compile it).
2. **`Assets/Scripts/RepairChecklist.cs`**: `Step` gains `verb`, `icon`, `targets` (a list of components still to do), `left` and `total`; `Build` fills them (:65-153). Remove `how` once the old strip is gone. The order is unchanged.
3. **`Assets/Scripts/BenchCards.cs`** (rewrite; keep the class and the statics that labs and `ItemInspector.cs:1083` read):
   - the paper card (§5.2) and the controls card and help hint (§5.5);
   - the Tab / View tap and hold;
   - the first-repair rule from `DayClock`;
   - a lab override `LabControls` (null for the game's rule, true or false to force).
   - Lab reads: `JobLine`, `CurrentVerb`, `Icons`, `CardOnScreen`, `ControlsOnScreen`, `HintOnScreen`, `ControlsShowing`, `ControlsText`, `ChecklistText` (now the line + " / " + the verb), `Steps`, `Showing`.
   - Retire the 760 px strip (:116-157) and the PlayerPrefs counter (:35-36, :95).
4. **`Assets/Scripts/BenchGlow.cs`** (new; made at runtime like `BenchCards`): the glow (§5.4) and the "what's left" rings and outlines (§5.5). Its own canvas at sorting order 15, under `BenchCards` (20). No allocation per frame.
5. **`Assets/Scripts/BenchOutline.cs`**: `Set(target, on, colour, widthScale, xray)`, setting `_Width` per hull through the property block; colours `Guide` = `BrandBright` and `Left` = `Gold`.
6. **`Assets/Data/Resources/Bench outline.shader`**: a second pass with ZTest Greater, dashed in screen space (4 px on / 4 px off at 1080p), at 70 % of `_Color`, drawn only when `_XRay` = 1. The TD owns the shader.
7. **`Assets/Scripts/ItemInspector.cs`** (Systems' file; these are UI-word changes only):
   - `CollectionPrompt` (:202-212) no longer appends `TaskSummary`;
   - hover words (:470-510): the part's name only; its own "why not" when it is blocked; "needs the cloth" when the wrong tool is in hand; no "Hold to …" and no "(40%)";
   - `Seen` (:1139) becomes readable by `BenchGlow`.
8. **`Assets/Scripts/ShopUI.cs`**: no prompt line while `inspector.IsHoldingItem` (:270-305). The bench card carries the way out.
9. **`Assets/Scripts/DayOneGuideUI.cs`**: §5.6 and §5.7; delete `RepairBenchAction` (:515-613).
10. **`Assets/Scripts/PlayerCarry.cs`**, **`DropInteractable.cs`**, **`StationInteractable.cs`**: §5.7.
11. **`Assets/Scripts/TicketRailUI.cs`** + **`JobTicket.cs`**: the compact mode at the bench.
    - Chips are 160 × 44, holding the portrait (34 px), the tape and a 104 × 8 bar.
    - Name, mood, fault and drink are hidden. A ringing support call keeps its icon and seconds.
    - At ≤ 25 % patience the chip pulses (scale 1.0 ↔ 1.06 at 1 Hz).
    - At the bench the rail's right reserve is 512 (instead of 380, :29).
    - `SetCardSize` loses its 118 px minimum (:250) in compact mode.
12. **`Assets/Scripts/HudCorners.cs`**: while `ItemInspector.Working`, fade out over 0.2 s and back:
    - "TODAY" (:180);
    - the sign's under-line (:566);
    - the day line (:543).

    The sign's word, the bar and the time stay.
13. **`Assets/Scripts/FaultSpeech.cs`**: his sister's shorter forms (§5.2) and Narrative's Day 1 / Day 2 labels for Grace.
14. **`Assets/Data/Resources/UI/BenchIcons/*.png`** and **`Tools/Blender/render_bench_icons.py`** (the Blender lead).
15. **Audio cues**: `checklist.tick` (already in Audio's list), `bench.showleft` (new).

**Size**: about one session of UI work in Unity B (the card about 400 lines, the glow about 250, the checks about 450), plus the shader pass.

## 6. Checks

**A. Edit mode: `Fixit Fidget › Checks › Bench words`** (new, `Assets/Editor/BenchWordsChecks.cs`). It tests both keyboard and pad wording, forcing `PadInput.UsingPad` as `DayOneOnboardingChecks.cs:33-41` does. It passes when all of these hold:
1. Every card line ≤ 8 words and ≤ 46 characters, for each bench fault in `FaultSpeech` with the longest customer name in the profiles and archetypes.
2. Every verb ≤ 2 words; the flip line ≤ 4.
3. The first-repair controls card ≤ 17 words (mouse) and ≤ 23 (pad), each line ≤ 6.
4. The help hint ≤ 2 words; the finished row ≤ 6 with the longest name.
5. The bench impatience line ≤ 12, with the bench's own back key and E/A in the device's glyphs.
6. "Put it back" for a device lifted from the counter's spot, "Put it on the intake tray" for any other.

**B. Play lab: `Fixit Fidget › Bench › Bench 6 - Words on the bench screen`** (new, `Assets/Scripts/Diagnostics/BenchWordsCheck.cs`, a `PlayLab`, with its menu beside Bench 5's in `Assets/Editor/BenchViewProbeMenu.cs`).

What it counts is what the player sees: every active, enabled `TMP_Text` on a screen-space canvas, where
- the canvas is enabled,
- the `CanvasGroup` chain alpha is ≥ .5,
- the colour alpha is ≥ .5,
- and the rect is on screen.

Rich text is stripped and words are counted with `WordBudget.Words`. The tab's initial ("Customer initial") is skipped as an icon. It tests both mouse and pad wording (the pad only with Unity in front).

| State | Budget |
|---|---|
| S1 first repair: a fresh Day 1, Grace's camera, the cloth not taken, the cursor on the lens | < 40 |
| S2 steady: Day 2, the watch's seized movement, unscrewing, a screw hovered; again at last orders | < 25 |
| S3 hidden: the phone face up (flip); the camera's fresh part placed behind the body | < 25 |
| S4 finished, "Fixed!" up | < 25 |
| S5 help held | none (overlap only) |
| S6 each alert alone (bench impatience, a stock chip) | ≤ 12 for the alert |

In every state, at every quarter turn and flipped (as Bench 5 does, `BenchViewProbe.cs:214-229`):
- no text or card overlaps the device's projected box (`BenchViewProbe.cs:343-355`), the mat's compartments or the tools in the stand (BenchStage's new fields);
- the hover card never covers the cursor's point;
- the smallest text is ≥ 16 px at 1080p;
- in S1–S3 a glow target exists and is the step's: in S1 the cloth, then the lens; in S3(a) the whole device; in S3(b) dashed.

Each run reports every element with its words and rect, the total per state, and photos, to `Logs/BenchWords/check-<time>/`. **Run it twice**: with the Game view at 1920 × 1080 and at 2560 × 1600. Labs measure at the Game view's own size (`Day1GuideCheck.cs:113, 538`).

**C. Existing checks to change:**
- **Bench 3** (`BenchLab.cs:303-319, 333-350`): the controls card's words. Instead of "Take out the 2 screws" and "flip it over", check that the current icon is the screwdriver with "[Y] Flip it over", then "Unscrew" once the back is up.
- **Bench 5** (`BenchViewProbe.cs:203-211, 264-283`): the job card and hint rects instead of the strip; "Pick it up" instead of "Pick it up and step back".
- **Day 1 guide** (`Day1GuideCheck.cs:263-311`):
  - right after the pickup, no tray offer, and the line shows only "In hand";
  - at the bench, no guide card, the job card's line names Grace, the first-repair card is in mouse words, and the glow is on the cloth.
- **Day 1 onboarding** (`DayOneOnboardingChecks.cs:207-230`): add "Put it back" and the bench impatience words.
- **Controller**: no change, plus one added check that View at the bench doesn't change the view.

## 7. What can break, and how we'd know

- **A stage that puts the device or a cup under the card** (the TD's framing, Art's mat): Bench 6 fails and names the rects. Fix the framing, not the card.
- **A fault with no short spoken form**: the edit check names it. Until his sister writes one, the line wraps to two (the card grows 26 px): visible, not broken.
- **Icons late**: neutral discs plus the verbs. The words still pass.
- **A step with no glow target** (a part type the checklist doesn't map): the player has only the verb. Bench 6's glow assertion fails for that device.
- **`ItemInspector.cs:1083` reads `BenchCards.Steps`**: keep that read working, or the D-pad's "next part" breaks. Bench 3 and Aim help cover it.
- **The dashed pass** costs one extra draw per hidden glowing renderer (≤ ~10). Read the Mac's frame time in the lab report against 15–20 fps.
- **Tapping View in the café still switches the view.** By design; watch for it in playtest 6.
- **The old PlayerPrefs key** `FixitFidget.BenchCards.CloseUpsSeen` is left behind, harmless. No save field is touched.

## 8. Playtest 6: what to watch

Timestamp each of these from the recording:
1. From the close-up opening to the first touch on the cloth (target < 5 s). Does the cursor go to the glow first, or to the card?
2. Each tick on the first repair; the whole repair against the < 3 min target.
3. Does anyone press Tab / View unprompted? When? Does the flash find the film guide?
4. Day 2's swap: from the "Lift out" tick to the first touch on the fresh part (< 3 s).
5. Every "what do I do?" at the bench, with what was on screen at that moment.
6. After Day 1, how often is Tab / View held?
7. At the till after the pickup: a second E press? Does he hesitate or walk to the bench?
8. Anything the card hides, in his words, especially at the Mac's 16:10.
9. Afterwards, ask the stranger: "what was the first job?" (one sentence).
10. The journal's "Words & menus" score against playtest 5.

## 9. Open questions

1. **The glow for an experienced player**: always on (my pick), or a switch in Settings › Controls beside "Aim help"? His taste. The switch is one settings row, not a mechanic.

## 10. Flags

- **Locks**:
  - "Objects are objects, information is projected": the glow and the rings are colour and shape (decision 7 allows them), and the pips and numbers stay on the screen.
  - The clock still runs at the bench, and the sign, bar and time stay up.
  - No new mechanic: "what's left" and the glow are his approved changes (§6.4, §6.7); tap / hold on Tab / View is a binding, not a verb.
- **Fixed here** (found while reading):
  - The bench impatience card names the wrong keys.
  - The controls card overlaps the stock chips when stock runs low.
  - The controls card's PlayerPrefs counter survives "Fresh game".
- **Not fixed** (the Producer's call):
  - The pause phone can't open at the bench (`PausePhone.cs:115-116`), so a stranger can't pause mid-repair, and Start does nothing there.
  - The "In hand" line repeats the fault label off the bench (`ShopUI.cs:289`, `PlayerCarry.cs:43-61`).
- **What I need from the other leads:**
  - **Systems**:
    - keep `Step.targets` true under the auto-tray;
    - a "what's left" call (`Flash(job, 1.5)`) that pulses the remaining dirt;
    - hover priority from `Step.targets`;
    - **one hold on the fresh part does fly + press**, so "Press in" stays one verb;
    - a progress event for the stuck timer;
    - the final bindings (the cards follow `ControlHints`).
  - **TD (with Cinematics)**: framing within §5.8 at both aspects, and the shader pass.
  - **Art / Blender**: the icons (§5.3); the mat's compartments and tool stand outside U1–U5; mint must read on the slate-blue mat.
  - **Audio**: `bench.showleft`.
  - **Content**: every line here is placeholder.
  - **Narrative**: Grace's Day 1 / Day 2 labels within the card rule.
- **Feel sheet**: not at `~/cafe/feel/` when I finished. When it lands, compare ReStory's notepad (size, word count, icons or words, where it sits) and the dirt indicator's look with §5.2 and §5.5. A wordier notepad there doesn't change his "under 25".
- **Paths**: the briefs were in `/home/claude/cafe/brief/`; the repo and the specs folder are under `/root/cafe/`. This spec is at `/root/cafe/specs/uiux-checklist-v2.md`.

## Sources

- ReStory's notepad "on the right tracks the entire process": progameguides.com, *ReStory beginner's guide* (progameguides.com/?p=1114523).
- PowerWash Simulator's dirt indicator, Tab / D-pad right: progameguides.com, *Tips and tricks*. The developers' description, it "highlights where the remaining dirt is … by flashing it yellow": steamcommunity.com/app/1290000/discussions/2/3111403360720627988.
- Xbox Accessibility Guideline 103, colour paired with shape, pattern, icon or text: devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/103.
- The common brief §5: KeenGamer on ReStory's light HUD and the wish to highlight hidden parts.
