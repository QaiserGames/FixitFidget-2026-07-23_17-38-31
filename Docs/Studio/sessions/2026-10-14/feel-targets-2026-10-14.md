# Feel targets and the Producer's calls — 14 Oct 2026, 09:30

*The Producer, from the feel sheet (`/root/cafe/feel/feel-sheet-2026-10-14.md`) and the nine specs in `/root/cafe/specs/`. Studio rule 2: these are written before anything is built, and every builder and lab works to them. He delegated every open question (14 Oct, 05:59: "for all your questions, i trust your judgement to do the best option, the option that is closest to my request"), so each call below took the option closest to his notes; each stays his to overrule. Where this file and a spec disagree, this file wins.*

## 1. The targets

"Default framing" = the TD's layout view, zoom 0 (`td-one-scale.md` §5). "Clumsy" = Systems' clumsy hand, Bench 8 (`systems-forgiving-bench.md` §5): every run, mouse and pad, 60 fps and a jittered 15 fps. "His recording" = playtest 6, timestamped by the agents.

| # | Target | Number | Proved by |
|---|---|---|---|
| T1 | Screw: first touch → resting in its cup | **≤ 1.3 s** at base (0.9 s hold + ≤ 0.35 s flight); a flight never blocks the next input; 0 carries; 0 lost or out of sight | Bench 8 per-step trace; Bench 3 |
| T2 | Cover, loose or broken part: end of the freeing verb → resting in its compartment | **≤ 0.6 s** flight, never below the mat's plane | Bench 8 trace; Bench 3 |
| T3 | Fresh part in | **One press flies it** (any press inside its silhouette plus the hit margin); a press still held on arrival presses it in (0.35 s); a click leaves it hovering over its seat until a hold. Seat ≤ 0.3 s after the hold completes. **0 precise drops anywhere** | Bench 8; Bench 3 act three |
| T4 | Input → first motion or sound | **≤ 1 frame** at his frame rate (≤ 66 ms at 15 fps); every flight's launch and landing cue on its first and last frame | Bench 8 cue timing (Audio §8) |
| T5 | Hover | outline or glow on the **same frame**; hit radius = max(28 px, projected head + 10 px) for screws and holes; outline + 12–16 px for covers, parts, dirt and tools (Systems §4.6) | Aim help; Bench 8 near-miss rate (100 % of near-misses inside a radius act) |
| T6 | A dirt spot | **2–4 strokes and ≤ 3 s** (median ≤ 3, every spot ≤ 6, ≥ 90 % within 4); a whole part ≤ 7 s; the tool's footprint ≥ 40 % of the spot's on-screen diameter at default framing | Bench 8 strokes per spot; Bench 3 act two |
| T7 | Words on the bench screen (`WordBudget.Words`) | **≤ 25 on the first repair with the controls card up, mouse and pad**; **≤ 15** in steady play; ≤ 24 worst case; an alert ≤ 12 on top | "Bench words" check, both inputs |
| T8 | Screw head on screen | **≥ 12 px at 1080p**, default framing (phone and watch; Grace's camera has no screws on Days 1–2); ≥ 15 px after one zoom notch | Bench 9 photos; scale audit |
| T9 | Device in the frame, default | **phone ≥ 34 %, camera ≥ 35 %, watch ≥ 17 %** of frame height (restated 11:30 after QA M1, the scale builder and the Blender lead measured the real layout), with every compartment the job uses, the rest and the scrap tray in frame and clear of U1–U5, at 16:9 and 16:10; zoom 1: ≥ 55 % for each | Bench 9; Bench 5 |
| T10 | Day 1's first repair (Grace's lens and film-path clean) | **≤ 18 actions**; practised ≤ 45 s; **first-timer ≤ 90 s** (his recording); **clumsy ≤ 45 s** on every run | Bench 8; his recording |
| T11 | First repair vs the day | a first-timer's first repair ≤ 50 % of Day 1 (the 420 s day puts it at ≈ 17 %) | Days check; his recording |
| T12 | Finish | "Fixed!" ≤ 1.0 s after the last seat (today's 0.3 s stays) | Bench 3 |
| T13 | Nothing out of sight | **0 frames** with a removed piece under or behind the device, below the mat, or outside the frame, across every clumsy run; 0 guard trips | Bench 8 (lost / hidden counters) |
| T14 | A part or spot finished | a whole-part gleam ≈ 0.5 s with its chime on the **same frame** as the progress reaching done; any text ≤ 3 words, gone ≤ 2 s | Juice; Bench 8 cue timing |
| T15 | Turning the device | quarter turns that **always** settle square (0 rests off-square), each ≤ 0.3 s | Bench 3; Controller |
| T16 | Stalls | none > 10 s; ≤ 15 % of a repair stalled (first-timer) | his recording |
| T17 | "What's left" is found | on the first repair it fires once by itself after ≥ 3 s of scrubbing with no progress (or UI/UX's 12 s stuck pulse, whichever comes first), with its key on the card; after that only on Tab / View | Day 1 guide; his recording |
| T18 | Sound | every cue Audio supplied a file for plays the file (0 synthesized placeholders on the bench, the till, the drinks and the intro); every bench verb has a cue | "Sound bank" check (Audio §8) |
| T19 | Days 1–3 | Day 1 and Day 2: **0 customers lost** for a first-timer and for the clumsy hand; Day 3: ≤ 1 lost (median); Grace handed back on Day 1 by ≈ 1:22 (median) | Days check (§10 of `days-1-3.md`); his recording |
| T20 | The night zoom | on a frame where a limit eases, the distance moves ≤ 35 m/s × dt + 0.05 m; on a frame with no zoom input it never moves > 3 m; the 8 → 24 m snap never recurs; close zoom works everywhere outside at night; mouse scroll and pad both tested (restated 11:30 after QA M2: at 15 fps, 35 m/s is 2.3 m a frame) | `NightZoomCheck` |
| T21 | The café from above | every new prop's smaller projected side ≥ 12 px at 1080p from the overhead (43°, 34 m, FOV 44°); 4 settled guests at the open on Days 1–3; repair customers use the repair-bar stools first | Art's overhead check; NPC checks |

## 2. The calls (the open questions in the specs)

1. **Cloth and brush on release (Systems Q1): Systems' rule.** Let go over the device and the tool stays in hand; let go anywhere else and it goes home; it goes home by itself when its last spot clears. Decision 4 said "let go — it goes back to its place"; this amends it. Why: his words are "grab and drag … feel the impact"; under "every release goes home", a player who lifts the button between strokes (a trackpad, a tired hand) makes a trip to the rest for every stroke, and ReStory's own rule is that over the dirt the cursor already is the tool.
2. **The swap's press (UI/UX vs decision 2): Systems' unified press** (T3). A click and a hold both work; nothing to learn.
3. **Screw heads (TD Q2): 3.8 mm, not 3.0.** The hole is Art's decal at 1.15 × the head (4.4 mm) with a bore of 0.48 × the head (1.8 mm); the driver's tip cross 0.6 × the head (2.3 mm; it is 2.6 mm today); the 2 mm floor on the hole's radius goes (the TD's flag, `Screw.cs:341`). Why:
   - He called today's screws "so tiny" at 20.7 px; 3.0 mm would draw them at 9.4 px at 1080p, under the 12 px where the TD says the cross starts to read and where a reviewer already called ReStory's 14 px screws "a little small" (on a Steam Deck, ≈ 10 px). 3.8 mm draws them at ≈ 12 px (T8).
   - "Don't make the screws enormous": 3.8 mm is under half of today's 8 mm, an M2 pan head (a real size), 5.4 % of the phone's width against ReStory's ≈ 7 %.
   - His real complaint is the hole against the screw. The bore at 0.48 × the head fixes that at either size.
4. **The watch's framing (TD Q1, Art Q1, feel sheet T9): the TD's 19 % with everything in view;** one wheel notch or D-pad up takes it past 60 %. Why: "nothing removed can end up out of sight" (rule 2, T13) and "easy" outrank fill. **Lab-gated:** if Bench 8's watch runs miss their bar at zoom 0, Bench 9 searches for the closest default that still keeps the job's cups and the rest in frame, and the watch opens there.
5. **Day 1's camera wake (Systems Q2): winds, no shutter fire** (Days R4). On Day 2 it winds and fires.
6. **The first-repair words (UI/UX's 31 / 37 against decision 7's < 25): cut to ≤ 25 on both inputs.** Direct grab makes "take a tool" and "use it" one verb, so the first-repair card drops to 11 words. Placeholder copy (Content polishes; `WordBudget.BenchFirstRepair = 25`):
   - mouse: `[Drag a tool]  Use it` · `[Right-click]  Step back` · `[Tab]  What's left` → 11 (with the card's line 8, "Wipe" 1, the hover 1 and the quiet corners 4: 25). "(hold: controls)" lives only on the held card;
   - pad: `[Right stick]  Move` · `[A]  Use` · `[B]  Step back` · `[View]  What's left` → 11 (25).
   Steady play stays at UI/UX's 15 (T7).
7. **Grace's Day 1 latte (Days Q1): none.** The lock's words; the played Day 1 asset is changed to match (`suppressRepairDrinkWishes: 1`). Her latte comes on Day 2, while she waits for the shutter: his "ordering … beverages while they wait for their repair".
8. **The walk-in repair list (Days Q2): yes,** `DayDefinition.walkInRepairs` (data, no save field).
9. **The bar mat at 9 px from above (Blender Q1): add a small mug tree** (≈ 0.25 m) to C3. It is one of his "coffee cups".
10. **The glow (UI/UX Q1): always on;** no settings row this session.
11. **"Fixed!" (Audio Q1): the chimes with a faint bell under them;** the café's bell keeps meaning "someone's here".
12. **The intro (Cinematics Q2, Q3): no police car** until the night has that consequence (Days §8); **rain parked** (scope freeze). Cinematics Q1 (which drink picture felt weird) is answered by the cause it found; his playtest 6 confirms.
13. **Settled guests (NPC Q1): Days 1–3 only** (the NPC lead's default). NPC Q2 (the names' sexes) goes to his sister with the notes.
14. **The day: 420 s, every day** (Days). **Days 4–5 get a stopgap so Day 4 is not a cliff for him:** their arrival gaps × 420/180 and patience ×1.0 (data only; the Days builder). Proper Days 4–5 tuning waits for the scope freeze to lift.
15. **The lab bar:** Systems' 45 s / 30 s / 75 s / 110 s gate the build (stricter than the feel sheet's 120 s).
16. **The pry and the tweezers stay** (decision 1: "taking apart stays physical and under your hand").
17. **Screws fly a short arc** (0.28–0.35 s; it shows where the screw went), never blocking input; not ReStory's one-frame jump.
18. **T17's one automatic "what's left"** on the first repair: adopted (a behaviour of the approved button).
19. **Adopted as written:** Systems' 15 overrulable decisions (§3), including the Vibration settings row (on by default) and "New part" retired; the TD's (§10) except the screw size; Days' 12 (§3); Blender's 12 (§3); UI/UX's 18 (§3) except the first-repair card's words; NPC's, Cinematics', Audio's and Art's as written.
20. **Superseded:** `bench-spec-v2.md` §2.1–2.3 and §7 (the magnet, the catch, "parts lie where you drop them"); the hero spec's "2× / 3×" rule; bench-spec v2 §2.5's zoom wording. **The Bible's pillar line** ("the screw comes out of the hole you clicked and falls") becomes "… and flies to its cup" (decision 1). The Producer edits both at the session's end.

21. **The bench layout (11:30, after QA M1, the scale builder and the Blender lead found the tool rest at y −60 pushes tools into U4 or off the frame):** every device's tool rest sits at `toolRestCentreY` **0**; the phone's scrap tray moves to **(6, 133)** and the watch's to **(26, 107)** mat mm (the camera's stays (−5, 115)). T9 restated to match.
22. **One "Put it back"** (three builders wrote one each): UI/UX's `DropInteractable` words and `StationInteractable` patch C; the scale builder's `PlayerCarry.LiftedFrom` is the source of where a device was lifted from.
23. **The cloth shows what it cleans (QA M3):** the held cloth sits with its far edge at the cursor (playtest 4's fold lead, closed form, no lag), so the spot's clearing is seen; at least 40 % of a spot uncovered mid-wipe.

## 3. Field names every builder uses (so parallel work meets)

- `DeviceDefinition.realSize` (bool; the TD's guard).
- `DeviceDefinition.benchLayout` (`BenchLayout`, Systems §4.0.1) with Systems' fields, plus the TD's `cameraAim` (Vector2, mat mm), `cameraDistance` (float, m), `closeAim` (Vector2, mat mm), `fovClose` (float, °), plus the Blender lead's `toolRestCentreY` (float, mat mm; phone −60, camera 0, watch −60).
- `RepairChecklist.Step.targets`, `left`, `total` (Systems); `verb`, `icon` (UI/UX).
- `BenchWork.Flash(job, seconds)`, `WorkLeft`, `Progressed`, `TargetDone`, `Flashed` (Systems §4.5).
- The empties: `Cup_1..6`, `Pad_Small`, `Pad_Large`, `Well_Small`, `Well_Large` (mat); `Seat_Screwdriver`, `Seat_Pry`, `Seat_Tweezers`, `Seat_Brush`, `Seat_Cloth` (rest); `Slot_1..3` (scrap tray).
- Sound cue names: Audio's cue table (`audio-bench-sounds.md` §3), exactly.
- Screw head 3.8 mm (`0.0038`), hole decal 1.15 × head, bore 0.48 × head, shank 0.55 × head, driver tip 0.6 × head.
