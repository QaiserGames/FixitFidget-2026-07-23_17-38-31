# Cinematics & Camera — intro v2, the drink-station view, the night zoom

*14 Oct 2026 · Cinematics & Camera lead (Opus) · for the Producer and QA · repo clone `playtest-2` at `fab65d3`, read-only. No code written, no Unity touched. Coordinates are world metres from the scene file (x east, y up, z north; the café's front faces −z at z −0.12). Frames were computed at 16:9 with each camera's own lens; the labs' photos must confirm them.*

## What he will see

Each morning now opens on the neighbourhood instead of machine close-ups. You might see the painted houses across the street with the café's blade sign, a neighbour walking past the windows, a car going by, the bench lamp blinking on, or a cup filling at the dispenser. Then comes the push in on the café's sign that she loved, the bell, and the camera lifting into your view. No two mornings in a row look the same. Grace appears about one morning in six, and only when she is actually coming in. At the drinks, pressing E from above opens a straight-on view from Ace's own eye height, with the six paddles level across the screen. Today's view comes from 44 cm above his eyes, turned 34° to one side. At night you can zoom in close anywhere outside. Walking away from the bins never makes the view jump: when a limit changes, the view eases into it.

---

## 1. What I did

- **Read:** the common brief, my brief, the Bible §2, my role file, `claude/playtest-4-findings-and-proposals.md` §6 and `claude/session-2026-10-13-notes.md`.
- **Code read in full:** `DayIntro.cs`, `DayIntroCheck.cs`, `CafeViewMode.cs`, `Lodger.BinsView`, the GraceHouse view, NightWalk's follow calls, and the stations code (`BeverageStation`, `StationInteractable`, `StationCloseUp`, `BeverageLook`, `PlayerInteractor`). Also the dispenser's step and lab (`Playtest4BenchSteps`, `DispenserFrameCheck`).
- **Scene, parsed:** I parsed the transforms in `AcesCafeLayoutPlaytest.unity` and measured four things:
  - the drinks close-up's camera and its 10 Oct origin;
  - the overhead's home view and the brain's blend;
  - every StreetLife walker route and car lane;
  - the café front, the houses and the street furniture.
- **Wrote:**
  - the intro's new pool: 18 shots, of which 14 can be built now and 4 wait on other leads (the 2 bench close-ups are parked outside the pool);
  - its selection rules, the change plan and the lab changes;
  - the drink view's cause, its fix and its lab photo;
  - the night zoom's rule and its lab.
- **Not done:** no photos (no Unity), no code.

## 2. The decisions and why

1. **Intro: a four-slot B-roll.** The slots are: the street waking (wide), the café opening up, someone (or the day's own shot), and the open (the sign and the bell). The pull-out is unchanged.
   - Why: her notes ask for the neighbourhood, for change from day to day, and for less Grace. The one shot she loved was the only one filmed at street distance.
   - Rejected: keeping the close-up pool and only adding street shots. The two shots she disliked were close-ups of models never built to be seen that close.
2. **Grace: tied and rationed.** A Grace shot plays only on a morning she is really coming in early, and at most once in any six mornings. Rejected: a 1-in-6 random Grace (it would show her on mornings she doesn't come, which makes the intro lie).
3. **The drinks close-up opens straight on, at Ace's eye, level.** The cause is a pose set by a 10 Oct step that put the camera at the top of its search grid, turned onto the cup stack (§4.2). Rejected options:
   - lowering today's pose (it stays 34° off-axis and the rows still slant);
   - pulling the camera 1.8 m back so turning keeps everything in frame (a camera floating behind Ace, with smaller paddles);
   - a cursor over a fixed frame (it changes how the drinks are worked: flagged, not proposed).
4. **The night zoom:**
   - close zoom (8 m) while following Ace, anywhere;
   - limits are pulled in softly every frame, never applied in one frame;
   - the turns split into an angle part and a distance part, so steering the view can no longer strand the distance.

   Rejected: an ease input can't cancel at all. The player would lose the stick for 0.8 s every time he passes the bins.

## 3. Decisions his to overrule

1. **The four slots and their lengths:** 1.8 / 1.4 / 1.4 / 1.4 s, controls back at about 6.8 s.
2. **Day 1's fixed order:** `street.row → bench.ready → drinks.pour → street.front`.
3. **Grace:** at most one morning in six, only when she's coming in early or has news, and never on Day 1.
4. **Bench close-ups parked:** `bench.screw` and `bench.wake` stay out until the new mat and the one-scale change land and their lab photos pass Art Direction.
5. **A "rush morning":** a day with a phase named "rush" that starts in the first fifth of the day. In today's data that is only Day 5. The Director may redefine it.
6. **The drinks trade-off:** the close-up goes straight on, accepting that turning to one end hides the other end.
7. **R3 eases** (0.5 s) by day too, instead of jumping.
8. **Leaving a house or the bins keeps a zoom chosen inside.** A zoom the house view set by itself is undone.
9. **The night-zoom lab's bar:** 40 m/s.

---

## 4. The deliverable

### 4.1 Intro v2: neighbourhood B-roll

#### 4.1.1 What they saw, and why (evidence)

- **His run's Day 1 order** was the canonical `grinder.beans | bench.screw | drinks.pour | street.front` (common §2). Three of the four are extreme close-ups:
  - **`grinder.beans`** (`DayIntro.cs:1198–1225`): a 32° lens about 15 cm from the hopper. The beans are one brown spun solid (`Tools/Blender/cafe_props.py`, the grinder's `beans` piece, "a heap of beans, not a cap"). At 15 cm it reads as abstract shapes, which is her "the angle of … the coffee maker … so bad".
  - **`bench.screw`** (`:1263–1394`): the shot ends on the tray (`trayLanding`, `:1325`), which is near-empty and sits on the plain slab mat. That is her "the screw and the placing on the tray … doesn't look like an intro level yet".
  - **`drinks.pour`** (`:1456–1582`): 16 cm over a procedural cup rim.
- **`street.front`** (`:1709–1742`) is the dolly toward the door with the café's name: her "span on aces cafe sign".
- **Day 2:** Grace is featured at 0 (`Assets/GraceShowcase/Day_02_GraceReturn.asset`: `featuredRegularArrivesAt: 0`). So `TiedShotFor` (`:955–969`) forces `house.grace` into beat 2 (`:919`). Its framing looks along her front toward the bay window (`:1695–1703`), and Day 2 shows an empty room through it (common §4). It is the only shot outside the café in the pool.
- **The pool** has 9 shots, and 6 of them are inside the café (`:869–872`). Meanwhile StreetLife runs 20 walkers, 24 cars on 8 lanes and 2 birds that no shot uses.

#### 4.1.2 The shape of a morning

| Slot | Content | Length | Starts | Class |
|---|---|---|---|---|
| 0 | the street waking | 1.8 s | 0.00 | wide |
| 1 | the café opening up | 1.4 s | 1.80 | mid / interior wide |
| 2 | someone, or the day's own | 1.4 s | 3.20 | mid / wide |
| 3 | the open: sign, bell at +0.15 s | 1.4 s | 4.60 | mid |
| — | pull-out to his view, title in | 0.75 s | 6.00 | brain blend (unchanged) |

- **Controls back at about 6.8 s.** The lab's bar stays 6–8 s; today's build lands at about 7.0 s.
- **Why these lengths:** a wide needs about 1.8 s to read, a mid 1.2–1.4 s.
- **Unchanged:** the fade from black over slot 0 (0.25 s), Short, Off, the skip and the title.
- **"Blended" (his word) in B-roll terms:**
  - one street bed runs under every exterior shot and ducks to the room tone inside;
  - J-cuts: each shot's first sound starts 0.15 s before its picture;
  - where a shot can choose, subjects cross left to right and every camera move pushes toward the café, so the last push ends on its name.

#### 4.1.3 The shot list

Summary first, details below. "Exists" means it can be built from what is in the scene today.

| Id | Slot | Len | Class | Subject distance | Exists |
|---|---|---|---|---|---|
| `street.row` | 0 (Day 1) | 1.8 | wide | 6–24 m | yes |
| `street.storefront` | 0 | 1.8 | wide | 6–18 m | yes |
| `street.across` | 0 | 1.8 | wide | 14–15 m | yes |
| `bench.ready` | 1 (Day 1) | 1.4 | mid | 2.5 m | yes |
| `counter.ready` | 1 | 1.4 | interior wide | 5–17 m | yes |
| `cafe.lights` | 1 | 1.4 | mid-wide | 4–21 m | yes (one read accessor) |
| `drinks.pour` (reframed) | 1 (Day 1: 2) | 1.4 | mid | 1.3 m | yes |
| `neighbour.front` | 2 | 1.4 | wide figure | 7.3–8.3 m | yes |
| `neighbours.row` | 2 | 1.4 | wide figure | ~7.6 m | yes (drops when nobody is in range) |
| `car.pass` | 2 | 1.4 | mid | 4.6 / 7.4 m | yes (a velocity read) |
| `patio.flower` | 2 | 1.4 | mid | 2.1 m | yes |
| `street.front` | 3 (Day 1) | 1.4 | mid | 5.8 m | yes (unchanged) |
| `street.sign` | 3 | 1.4 | mid | ~2.8 m | yes (unchanged) |
| `house.grace` (reframed) | 2, tied: Grace | 1.4 | wide | 8.2–8.6 m | yes |
| `regular.walkup` | 2, tied: regular | 1.4 | wide figure | 5–7 m | needs NPC |
| `queue.door` | 2, tied: rush | 1.4 | wide | ~10 m | needs NPC + Systems |
| `police.car` | 2, tied: caught | 1.4 | wide | ~12 m | needs Narrative + 3D + a saved fact |
| `rain.window` | 2, tied: rain | 1.4 | mid | — | needs Environment + Audio + a day field |

**The details:** camera place → look target, lens (vertical), move, what it frames, and the sound it cuts on.

**`street.row`** (slot 0, Day 1's opener)
- **Camera:** (−9.0, 1.7, −6.5) → (−13.0, 3.5, 14.0), 38°. It sits at the café's south-west corner, by the street-name post at (−7.0, −4.8).
- **Move:** dolly 0.8 m north and tilt up 2°, eased.
- **Frames:**
  - on the left, the painted row across the west street: house 2, Dusty rose, at 12 m; house 3, Sea green, at 18 m; house 4, Lavender, at 24 m;
  - on the right, the café's west wall and blade sign (6.3 m);
  - lanes 0 and 1 with their cars, and the three "Walking neighbor" on the houses' pavement;
  - the hill terraces at 46 m.
- **Grace's door** is 52° off axis: out of frame.
- **Sound:** the street bed from frame 0, and the sting's first hit on frame 0.

**`street.storefront`** (slot 0)
- **Camera:** (9.0, 1.8, −5.5), at the front pavement's east end → (−4.0, 1.6, −1.5), 40°.
- **Move:** pan left 6°.
- **Frames, near to far:**
  - the A-frame (6.2 m) and the east olive tree;
  - the door and fascia (10 m);
  - the patio table (13.6 m) and the blade sign (18 m);
  - the painted row past the junction.
- **Also in frame:** the front promenade neighbour, and lanes 4 and 5 on the left.
- **Grace's house** shows only as background at 25.7 m. That is allowed: the rule is ≥ 15 m in a non-Grace shot.
- **Sound:** the street bed; `intro.car` if a car crosses.

**`street.across`** (slot 0)
- **Camera:** (−2.0, 1.6, −3.0), on the patio west of the door → (2.0, 2.5, −16.6), 40°. The look target is "Front - 2 - neighborhood shop house".
- **Move:** push 0.5 m.
- **Frames:**
  - both lanes, the far pavement's walker;
  - the café's customer parking and its "Ace's Café customer parking" board (14.6 m);
  - the three shop houses across the street (13.8 m).
- **Sound:** the street bed.

**`bench.ready`** (slot 1, Day 1). This replaces `bench.lamp`'s 11–14 cm-high view.
- **Camera:** (−3.0, 1.35, 12.2) → (−4.5, 1.15, 14.6), 40°. That is a seated customer's eye, between the east and middle repair-bar stools.
- **Move:** push 0.3 m.
- **Frames:** the bench's mat and tools (2.5 m), the magnifier lamp, and the 72-drawer parts wall behind (6.3 m).
- **The lamp's blink-on is kept:** BenchLamp's flicker (`DayIntro.cs:1427`), with the light held and then restored.
- **Sound:** `intro.lamp` (exists) at 0.35 s, over the room tone.

**`counter.ready`** (slot 1)
- **Camera:** (0.6, 1.6, 1.4), just inside the door → (0.0, 1.45, 14.5), 42°.
- **Move:** dolly 0.6 m in.
- **Frames, the room's length:** the communal table (5 m), the pastry case (12.2 m), the till (13.2 m), the bench and its REPAIRS sign (13 m), the dispenser (15.8 m) and the menu board (16.6 m).
- **People:** Ace at the till if he is drawn; settled guests once NPC seats them.
- **Sound:** room tone, and `intro.cup` (new: a cup set down) at 0.4 s.

**`cafe.lights`** (slot 1)
- **Camera:** (−3.2, 1.55, −3.2), on the pavement → (−1.6, 1.4, 4.0), 36°, looking through the west gilded window. The camera is static.
- **The lights:** the café's own lights are held at 15% and raised to 100% over 0.5 s from 0.3 s: record, hold, restore, the same pattern as BenchLamp.
- **Frames:** the west olive tree in the foreground (4 m), the communal table (9.2 m), and the counter and back bar (17–21 m).
- **Sound:** `intro.lights` (new: a breaker clunk) at 0.3 s.
- **Needs:** a read accessor for the café's lights (CafeDaylight already dims them at night).

**`drinks.pour`** (reframed from the rim close-up to a mid; slot 1, but slot 2 on Day 1)
- **Camera:** (3.07, 1.6, 16.0), on the dispenser's axis, 0.5 m behind the stand point → (3.07, 1.25, 17.29), the face's middle, 40°. The camera is static.
- **Frames:** the whole face square, with Latte's cup filling on the right. It uses today's procedural cup and stream (`:1479–1516`) and matches the new drinks close-up (§4.2).
- **Sound:** `drink.pour`, which is the real `SoftPour.wav`, from 0.1 s.

**`neighbour.front`** (slot 2)
- **Subject:** the "Front promenade neighbor" (StreetLife, 0.84 m/s), who loops the café's own pavement at x −6…6, z −3.7…−4.7.
- **Camera:** on the far pavement at (clamp(walker x, −5, 5), 1.5, −11.8) → the walker's chest (y 1.3), 28°.
- **Move:** pan with him, the look-at smoothed over 0.2 s.
- **Framing:** at 8 m the frame is 4 m tall, so the figure fills about 44% of it. Behind him are the café's windows and fascia. A car in lanes 4 or 5 may cross the foreground.
- **Sound:** `intro.steps` (new: pavement footsteps), on the walk's cadence.

**`neighbours.row`** (slot 2)
- **Subject:** one of the three "Walking neighbor" (x −15.6…−16.2), only while one is at z 4…17, in front of houses 2–4. That window keeps Grace's door at least 34° off axis.
- **Camera:** (−8.4, 1.5, walker z − 1.5) → the walker's chest, 30°, panning with them.
- **Frames:** a neighbour on their own street with the painted fronts behind.
- **Sound:** the street bed and footsteps.
- **If nobody is in the window,** the shot drops.

**`car.pass`** (slot 2)
- **Subject:** the nearest moving car (> 2 m/s) on lane 5 (westbound, z −6.6) or lane 4 (eastbound, z −9.4) that will reach the frame's middle at 0.7 s.
- **Camera:** (car x + velocity·0.7, 1.2, −2.0), on the café's pavement → (same x, 0.9, −7.5), 40°. The camera is static; the car crosses in about 1.3 s.
- **Distance:** 4.6 m on lane 5 and 7.4 m on lane 4, both fine for the Synty cars at mid.
- **Behind:** the far pavement, the parking and the shop houses.
- **Sound:** `intro.car` (new: a pass-by, panned with the car, peaking at 0.7 s).
- **Needs:** the cars' velocity (§4.1.9). It drops when no car is in reach.

**`patio.flower`** (slot 2)
- **Camera:** (−2.6, 0.95, −3.4) → (−4.05, 0.8, −1.85), the outdoor bistro table, 38°.
- **Move:** push 0.3 m.
- **Frames:** the table's single flower in its bud vase (2.1 m), with the painted row soft behind at 15–24 m. If a settled guest sits there, they become the shot.
- **Sound:** the street bed.

**`street.front` and `street.sign`** (slot 3)
- **Unchanged** (`:1709–1742`, `:1745–1773`), except the length goes from 1.3 to 1.4 s.
- **Sound:** `intro.sign` at 0, and the bell (`day.open`) at 0.15 s.

**The day's own (slot 2, when tied):**

- **`house.grace` (reframed)**
  - **Camera:** (−8.6, 1.6, −2.5), across the west street at the café's corner → (−17.4, 2.3, −2.2), 32°.
  - **Move:** push 0.4 m.
  - **Frames:** her door (8.6 m), the bay (8.2 m) and Barnaby on her step (8.3 m). It never looks into her window.
  - **Sound:** the street bed.
- **`regular.walkup`:** today's regular (Grace or the Officer) walks the front pavement from the sidewalk bench (−6.1, −0.5) toward the door. It is a cameo: their look, their walk by mood, no brain, removed at the cut.
  - **Camera:** (2.5, 1.5, −3.0) → their chest, 32°, panning with them.
  - **Sound:** footsteps.
- **`queue.door`:** 3–4 of the day's first customers waiting at the door with their phones out. Camera (0.5, 1.5, −11.8) → (0, 1.2, −1.6), 30°.
- **`police.car`:** a fictional police car parked in the café's customer parking, in the bay at x 1.3 (straight across the zebra from the door). Camera (−1.5, 1.4, −3.2) → (1.3, 0.9, −15.0), 34°. A lane can't take a parked car: StreetLife's cars would drive through it.
- **`rain.window`:** rain on the gilded window with the street blurred behind. On a rainy morning, slot 0's shots run with the rain on.

#### 4.1.4 The day's own shots: which exist

- **Rain: does not exist.** There is no weather in the code (no rain, weather or puddle anywhere in `Assets/Scripts`). It needs:
  - Environment: rain VFX, a wet-pavement material swap and glass streaks;
  - Audio: a rain bed;
  - a day field: a new `DayDefinition` field, which is the Director's and TD's call.
- **A queue on a rush day: does not exist.** The spawner is held for the intro's length (`DayIntro.cs:521–524`), so nobody is outside. It needs:
  - NPC: bodies placed at the door during the intro, released at open;
  - Systems and the Director: a rush morning's first arrivals start at the door. That is a pacing change, not just a look.
- **The police car after a caught night: does not exist.** There is no police car in the repo. The Synty city pack is licensed and not in the clone, so whether it has one is unverified. It needs three things:
  - **Narrative's yes:** a car outside implies consequences. On 13 Oct "The cells come later" was cut because a newcomer read it as jail ahead.
  - **3D/Environment:** a fictional, legal-friendly livery.
  - **The fact itself:** `NightCycle.LastEnding == "caught"` (`NightCycle.cs:363`) is session-only. Surviving a save needs a save field (TD: migration and test).
- **Today's regular walking up: half exists.** The regulars' profiles and looks exist (Grace, the Officer); the walk-up cameo doesn't (NPC).

#### 4.1.5 Which models may be filmed closer than 1.2 m

- **Yes, after decisions 3 and 6 land:** the hero devices (`hero_phone.py`, `hero_camera.py`, `hero_watch.py`) and the bench tools (`bench_tools.py`). They were built from measured references for the bench close-up at 30–60 cm.
- **Yes, at 0.6 m or more:** the dispenser (`BeverageDispenserV2.fbx`, built for its own 1 m close-up) and the procedural cup and pour (`DrinkLiquidVisual`).
- **No:**
  - the grinder (`cafe_props.py`: one bean solid);
  - the café kit and lived-in props (`cafe_props.py`, `cafe_livedin.py`, built for the overhead and first person);
  - the signs (`cafe_signs.py`);
  - Grace's house (`grace_house_v2.py`, built for doll's-house distance);
  - the Synty city pack;
  - the NPC bodies (city bodies with no face rig: 2.5 m or more, never a face close-up).

#### 4.1.6 Cut and parked

- **Cut:** `grinder.beans` and `grinder.burr`.
- **Parked**, classes kept but out of the pool: `bench.screw` (it ends on the tray, which decision 6 retires) and `bench.wake` (the plain slab). They return as close-ups on hero models once the new mat and one scale exist and Art Direction passes the lab photos.
- **Reframed:** `bench.lamp` → `bench.ready`, `drinks.pour` → mid, and `house.grace` → her front from across the street.

#### 4.1.7 The selection rules

1. **Day 1 is fixed:** `street.row | bench.ready | drinks.pour | street.front`. It shows what the place is: the street, the bench, the drinks and the café's name. There is no Grace; she walks in first anyway.
2. **Other mornings: one shot per slot, a weighted draw.**
   - The base weight is 1.
   - In slots 0–2, a shot used yesterday gets weight 0, and one used two mornings ago gets 0.5.
   - Slot 3 draws `street.front` 2 : 1 over `street.sign`, with no yesterday rule.
   - Result: consecutive mornings differ in at least 2 of the 4 slots (3 when every shot is available).
3. **Never the same order as any of the last 6 mornings.** The picker redraws up to 8 times, then swaps slot 0 for another choice. This extends today's rule, which only checks the last morning (`:908–934`).
4. **The day's own:**
   - At most one tied shot per morning, always in slot 2.
   - Priority: caught > today's regular > rush > rain.
   - **Today's regular** is the first of these that applies:
     - the featured regular, if `featuredRegularArrivesAt ≤ 0.25`;
     - a story visit with `arrivesAt ≤ 0.25`;
     - `CustomerSpawner.MorningVisitor`;
     - the word visitor.

     This generalises `GraceEarly` (`:971–973`) to any regular.
   - **A rush morning** is a phase whose name contains "rush" and that starts at or before 0.2 of the day.
   - **A tie whose shot can't be prepared** falls back to the normal draw and is logged. The Director owns "story mornings" (their brief, item 5). If the Director adds a flag, the picker reads it instead.
5. **Grace:**
   - A Grace shot (`house.grace`, or `regular.walkup` when the regular is Grace) plays only when she is tied and no Grace shot played in the previous 5 mornings.
   - Never on Day 1.
   - So at most one morning in six. With today's data she shows on Day 2 and not on Day 3, even when she is Day 3's morning visitor.
6. **Missing subjects:** a shot whose subject is absent drops and its slot redraws. An empty slot is skipped, and the remaining shots stretch so the total is still at least 5.3 s (no shot over 2.4 s). Fewer than 2 shots means Short (today's rule).
7. **History lives in PlayerPrefs**, which is cosmetic and not the save. The key `FixitFidget.DayIntro.History` keeps the last 6 orders and which of them were Grace mornings. The old `LastOrder` key (`:57`) is read once, then replaced.

#### 4.1.8 Change plan: `Assets/Scripts/DayIntro.cs`

- **Data.** The `Shot` base class (`:1136–1193`) gains:
  - `Slot` (it was `Beat`);
  - `Framing` {Wide, Mid, Close};
  - `Subject` (the point the lab measures to);
  - `MinSubjectDistance`: Wide 4.0 m, Mid 1.2 m, the dispenser 0.6 m, Close only for hero models;
  - `Tie` {None, Caught, Regular, Rush, Rain};
  - `IsGrace`, `Weight`, and `LeadCue` (the J-cut).

  `PoolIds` (`:869–872`) and `AllShots` (`:874–881`) become one table, plus a `Parked` list.
- **Selection.** `PickShots` (`:883–942`) is rewritten per §4.1.7. `TiedShotFor` (`:955–969`) becomes `TodaysTie(day) → (tie, profile)`. Add a pure `Plan(day, ties, history)` so the lab can simulate without a camera.
- **Shots.** New classes in today's pattern (Prepare finds the anchors; Frame places the camera on unscaled time): StreetRow, StreetStorefront, StreetAcross, BenchReady (built from BenchLamp), CounterReady, CafeLights, NeighbourFront, NeighboursRow, CarPass and PatioFlower. DrinksPour and GraceHouseShot are reframed. Stubs return false until their parts exist: RegularWalkUp, QueueAtDoor, PoliceCar, RainOnGlass.
- **Anchors.** `SceneThings.Find` (`:996–1030`) adds these by name:
  - the StreetLife actors "Front promenade neighbor" and "Walking neighbor 1/2/3";
  - the café pieces "Outdoor bistro table", "CK_SidewalkBench", "CK_RepairBench", "CK_PartsWall", "CK_FrontCounter" and "CK_WindowGilding_L";
  - the neighbourhood's "2 - Dusty rose bay-window house" and "Front - 2 - neighborhood shop house".

  Grace's door stays `HomeDoor.Find("home.grace")`.
- **The photocopied props.** `Photocopy` (`:1073–1102`) stays, for the parked bench shots and later the cameo or the car. No live B-roll shot copies or moves a gameplay object. The intro only does two things to the scene, as today:
  - holds a light's intensity and restores it (BenchLamp, CafeLights);
  - adds its own props under its Props root, which are destroyed at the end (`:560`).
- **Timing.** The slot lengths are as in §4.1.2. Each shot's lead cue fires 0.15 s before its start (from the previous shot's clock).
- **Untouched:** the hold and release (`:518–545`), the screens (`:638–676`), the skip (`:461–492`), Short, Off, the title, and the blend handling (`:612–630`).

#### 4.1.9 The lab: `Assets/Scripts/Diagnostics/DayIntroCheck.cs`

1. **Canonical check** (`:212–214`): change it to `street.row|bench.ready|drinks.pour|street.front`.
2. **The storyboard** (`:128–138`) gets explicit orders that cover the pool:
   - Day 3: `street.storefront|counter.ready|neighbour.front|street.sign`
   - Day 4: `street.across|cafe.lights|car.pass|street.front`
   - Day 5: `street.row|drinks.pour|patio.flower|street.sign`
   - Day 2, Grace tied: `street.row|bench.ready|house.grace|street.front`
   - `neighbours.row` in a slot-2 run.

   A dynamic shot that drops (nobody in reach) retries once after 3 s, then is noted, not failed.
3. **New checks for every shot, at each photo moment:**
   - (a) the camera-to-subject distance stays at or above the shot's minimum for the whole shot;
   - (b) the subject point is inside the middle 80% of a 16:9 frame;
   - (c) a ray from the camera hits the subject first, or nothing;
   - (d) the camera isn't inside a collider (a 0.1 m sphere check);
   - (e) in a non-Grace shot, Grace's door is not in frame closer than 15 m.
4. **The picker, 30 mornings simulated with `Plan`:**
   - Day 1 is canonical;
   - no order repeats within any 6 consecutive mornings;
   - consecutive mornings differ in at least 2 slots;
   - Grace appears in at most 1 of any 6 consecutive mornings and never on Day 1. With Grace tied on Days 2, 3 and 9 she shows on Days 2 and 9 only;
   - every untied shot appears at least once;
   - each tie, forced once, lands in slot 2, or is noted "tie not built".
5. **Kept:** 6–8 s; the clock held; the screens faded and back; the bell once; the skip within 1 s; Short 2–3 s; Off; the title's margins; lines of 90 characters or fewer; nothing left behind. Add: the café's lights are back at their intensity.
6. **Frame time:** log each shot's worst frame. Street level draws more of the district than the overhead. Note anything over 100 ms at a cut. The intro's dt cap (`:338`) already keeps a hitch from jumping the picture.

#### 4.1.10 What I need from other leads

- **Environment & Scene**
  1. Nothing for the 14 shots that exist, apart from item 2.
  2. A read accessor for the café's own lights (CafeDaylight already knows them for the night).
  3. With Art Direction: confirm the light at the intro's hour reads as morning on the storefront and the painted row.
  4. Later, only if approved: the rain, and the fictional police car for the bay at x 1.3.
- **NPC & Characters**
  1. The regular's walk-up cameo: their look, the walk by mood, a 6 m path on the front pavement, no brain, removed at the cut. Or, with Systems, have the spawner's real first arrival start there.
  2. The queue at the door on a rush morning, with Systems and the Director.
  3. Your settled guests (your item 2) seated before the intro starts, so the interior shots aren't empty.
  4. The front promenade neighbour is the most-filmed extra: please don't give him the red shirt (your item 3).
  5. A read-only list of StreetLife actors with position and velocity, for `car.pass`. Or tell me a two-frame estimate is fine.
- **Audio**
  - **New cues:**
    - `intro.car`: a pass-by, at most 1.4 s, panned;
    - `intro.steps`: pavement footsteps;
    - `intro.lights`: a breaker clunk;
    - `intro.cup`: a cup set down.
  - **Kept:** `street.day` as the bed, `intro.lamp`, `drink.pour` (`SoftPour.wav`), `intro.sign`, `day.open` and `intro.sting`.
  - **Leave the intro:** `intro.grind`, `screw.*` and `phone.boot` (the bench keeps `screw.*`).
  - **Timing:** J-cuts lead by 0.15 s.
  - **One hook:** `CafeSoundscape.cs:99` picks inside or outside from `view.AceInsideCafe`, so during the intro the street bed stays muffled because Ace is inside. For the intro's length it should follow the intro camera's place.
- **Narrative Director**
  - Story mornings are yours.
  - Yes or no on the police car.
  - The day lines are unchanged.

#### 4.1.11 What can break, and how we'd know

- **A dynamic shot finds nobody** (walkers at the far end of their loop, no car near). It drops and its slot redraws; the lab counts the drops. If more than 1 morning in 3 drops a slot, widen the windows.
- **A car crosses in front of `neighbour.front`** (there are lanes between the camera and the walker). The lab's occlusion check catches it at the photo moment. A brief crossing is accepted.
- **Street shots cost frames on the Mac.** The lab logs each shot's worst frame.
- **On a morning after a night,** the first shot waits, framed, under the curtain (`:297–301`), as today.

---

### 4.2 The drink-station view from above

#### 4.2.1 What the camera does now (verified)

1. **From the overhead,** E within 1.5 m of the dispenser's stand point offers its close-up (`PlayerInteractor.cs:506–517`, `stationEnterRange` 1.5 at `:57`; `StationCloseUp.Interact`).
2. **`EnterStation`** (`PlayerInteractor.cs:863–913`) does four things:
   - moves Ace to the stand point (3.07, 0, 16.48) and turns him to it (`:872–903`);
   - hides his body (`:909`);
   - locks the cursor (`:911`);
   - raises the station camera to priority 20 (`StationInteractable.cs:197–200`).
3. **The live camera** changes from `CmShopCam` to the "Drink station camera".
   - `CmShopCam` has priority 10 and a 44° lens (scene `:101797`). It sits at (0, 23.79, −15.87); its home is yaw 0°, pitch 43°, 34 m from the café's middle (0, 0.6, 9).
   - The "Drink station camera" is a child of the dispenser, with a 58° lens.
4. **The brain blends** with its default: EaseInOut over 0.75 s, no custom blends (scene `:49611–49620`). From the home view that is a 38.6 m dive turning 34.5° left, peaking at about 77 m/s. Leaving reverses it.
5. **The overhead itself doesn't move.** CafeViewMode ignores input at a station (`CanChangeView`, `CafeViewMode.cs:121`; `:300`). It keeps its angle and distance, and the close-up puts the walls up and the hanging fixtures on (`:203–207`, `:641–648`).
6. **The close-up's look** (`BeverageLook`) turns from a rest taken from the camera's own transform (`BeverageLook.cs:39`, `:52–53`): ±52° across, 28° up, 34° down (`:86–87`), horizon level (`:103`).

**The rest pose (the cause).**
- **Where it is:** local (0.325, 1.10, −1.52) in the dispenser's frame, turned 34° left and 28.5° down (scene `:14268–14269`). In world terms that is (3.40, 2.09, 15.91), yaw −34.5°, pitch 28.5°.
  - It is 2.09 m above the floor, **44 cm above Ace's eye** (1.65 m, `eyeHeight`).
  - It is 0.33 m right of the dispenser's axis and 0.57 m behind his stand point.
- **How it got there:** on 10 Oct, `Fixit Fidget › Playtest 4 › Bench › 1 - Frame the whole dispenser` put it there (`Assets/Editor/Playtest4BenchSteps.cs:22–32`, `:261–272`).
  - The step searches a grid of poses that tops out at 1.10 m up (`GridY1`, `:44`) and 0.35 m right (`GridX1`, `:43`).
  - Every pose is "turned onto the cup stack's middle".
  - The goal was that the close-up opens with the crosshair on the cup stack and keeps every paddle in frame when turned to the far cup place. That answered the first-hour playtester's photo of Americano and Latte off the screen.
  - The chosen pose sits on the grid's ceiling.
- **What it shows** (computed at 16:9, 58°):
  - the cup stack at the centre of the screen;
  - the paddle row slanting **13.9°** down to the right, from Coffee at 55%/59% to Latte at 83%/47%;
  - the counter's front edge running corner to corner;
  - the near end 1.25 m away and the far end 1.64 m;
  - the smallest paddle 70 px tall at 1080p.

  That is "up and diagonal".
- **The 30 Sept installer pose** was frontal and level (`BeverageStationSetup.cs:101–108`): the paddle row level, paddles 86 px.

#### 4.2.2 Candidates for "up and diagonal", ranked

1. **The close-up's rest pose:** up (2.09 m) and diagonal (34.5°, rows slanting 13.9°). It is the picture he gets every single time from above. This is the main cause.
2. **The blend into the close-up.** It is a 0.75 s, 38.6 m dive that turns 34.5°, plus his orbit's yaw if he had orbited. Leaving climbs back the same way. This is the moving part of "up and diagonal", and it ends on candidate 1.
3. **Fixtures switching on at the blend's first frame.** The counter pendants are in `overheadFixtures`. The blend's path passes about 0.4 m over "Counter pendant 4" (3.21, 3.17, 13.75) at 93% of its length: a one- or two-frame flick, unverified. No cut-away wall is involved at the home yaw, because the back plaster (z 18.12) doesn't block the view from the south.
4. **A different reading of his words.** Before E is pressed, the drinks corner sits 68–75% up the screen at the home view, because by day the overhead circles the café's middle, not Ace (`CafeViewMode.cs:10–11`, `:158`). After an orbit to about 45°, the back bar runs diagonally. Fixing that means the overhead following Ace by day, which is a design change and his call. Not proposed.
5. **Not the cause:** the orbit itself. It doesn't move at the drinks.

#### 4.2.3 The fix

- **The new pose.** The Drink station camera's local pose becomes:
  - position (0, 0.657, −1.05): world (3.07, 1.65, 16.38), which is eye height, on the dispenser's axis, 10 cm behind the stand point;
  - heading: the dispenser's own;
  - tilt: 23.4° down onto the face's middle (3.07, 1.25, 17.29);
  - roll: 0;
  - lens: 58°, unchanged.
- **Result** (same rules as the lab):
  - the whole face in the 16:9 frame with 8.8% to spare at the least (the cup stack);
  - the paddle row level (0°);
  - the smallest paddle **111 px** (it was 70);
  - the dive from the home view no longer turns (−0.4° instead of 34.5°).
- **The trade-off:**
  - Turned onto the cup stack (33.8° left), Americano, Latte and the basin leave the frame.
  - Turned onto Latte's place (24.9° right), the cup stack and Coffee leave.
  - That is what turning your head does, and it is what Ace's own walking first-person view already does at the dispenser (`DispenserFrameCheck.cs:138–163` notes it).
  - The crosshair opens on the face's middle, so the first cup needs a turn left. The card already says "Aim at the cup stack to take a cup" (`BeverageStation.cs:88`).
- **The blend stays 0.75 s.** The bench close-up uses the same default (`ItemInspector.cs:300`, priority 30): one camera language.

#### 4.2.4 Files and data

- **Scene** `Assets/Playtests/AcesCafeLayoutPlaytest.unity`, object `…/04 - drinks work area/Beverage dispenser/Drink station camera`:
  - localPosition (0.325, 1.1, −1.52) → (0, 0.657, −1.05);
  - localRotation → a level look at the face's middle (local Euler about (23.4, 0, 0)).
  - Made by a step, not by hand: `Fixit Fidget › Cinematics › Drinks close-up - straight on (scene)`, with one undo group and before and after photos.
- **`Assets/Editor/Playtest4BenchSteps.cs`.** Step 1's rule changes to "frontal at eye height, 10 cm behind the stand point, onto the face's middle", so re-running it can never bring the diagonal back. Its checks (`:280–315`) follow: the "opens on the cup stack" and "turned onto the far place, everything stays" checks go.
- **`Assets/Scripts/Diagnostics/DispenserFrameCheck.cs`**, the close-up part (`:88–137`):
  - The "opens with the crosshair on the cup stack" check (`:100–101`) and the "turned onto the far cup place, the cup stack and every paddle stay" check (`:113–121`) are replaced by four checks:
    - the camera is at Ace's eye height ±5 cm, its heading within 1° of the dispenser's, roll under 0.05°;
    - the paddle row's slant on screen is 0.5° or less;
    - the smallest paddle is at least 96 px (it was 48);
    - turned onto the cup stack, the stack, Coffee and Tea are in frame and the crosshair is on the stack.
  - The whole-face-in-frame check (`:103–105`) is kept.
- **`BeverageLook.cs`:** no change; it takes its rest from the transform (`:39`).

#### 4.2.5 The lab photo, before and after

In `Logs/Bench/dispenser-frame-<time>/`, each at 1920×1080:

1. **The close-up as it opens.** Before: today's diagonal. After: straight on. The row's slant and the smallest paddle are printed under each.
2. **Mid-blend frames** at 25%, 50% and 75% of the 0.75 s, from the home view and from a 45° orbit.
3. **The overhead with Ace at the stand point, before E.** This is the control: it should be identical before and after.

#### 4.2.6 What a screenshot from him would settle

He named the place, but not the picture. One screenshot or phone photo taken at the moment it feels weird settles which of three he means:
- (a) the close-up just after E: this fix;
- (b) the overhead before E: the corner high on screen, a different and bigger question;
- (c) the swoop between them.

Cheaper still: show him the lab's photos 1–3 and ask "which of these felt weird?"

#### 4.2.7 What can break

- **Unaffected labs:**
  - Stations aims at the cup stack explicitly (`StationsCheck.cs:355–356`), and the stack is 33.8° from rest, within ±52°.
  - The Day 1 guide only reads the card (`Day1GuideCheck.cs:462–464`).
  - Controller only needs the stick to turn the look 5° or more (`ControllerChecks.cs:258–268`).
  - Pause (`PauseCheck.cs:493–503`).
- **The 10 Oct complaint returns** in photos turned onto the cup stack. This is expected; the lab notes it rather than failing.
- **Nothing stands at the new eye point:** Ace is hidden at a station (`PlayerInteractor.cs:909`), and the staff aisle is clear.

---

### 4.3 The night zoom (decision 10)

#### 4.3.1 The cause (verified; it matches the common brief §4)

- **At the bins:** `Lodger.BinsView` (`Lodger.cs:451–468`) calls `EnterHouseView(205°, 46°, 14 m)` once Ace is within 8 m of the bins (`NightZeroSet.cs:49–51`; scene `:77905–77908`). The limits become 8–22 m (`CafeViewMode.cs:191–192`; scene `:155981–155982`), and a 0.8 s turn runs (`:490–497`). He zooms in; 8 m is allowed.
- **Walking away:** past 9.5 m (`viewRadius` + 1.5), `ExitHouseView` (`:482–488`) runs.
  - It sets `inHouse = false`, which puts the limits back to 24–48 m. `followWithDayFraming` is on (scene `:155972`), so `DistanceMin` is `minimumDistance`, 24 (`:191`).
  - It starts a turn back to the outside angle and distance (`TurnTo`, stepped in `LateUpdate` at `:532` and `:499–508`).
- **The turn dies:** any camera input sets `turning = false`: middle-drag `:364`, scroll `:371`, right stick `:380`, triggers `:387`, R3 `:392`. With a pad you steer the right stick while walking, so the turn dies with the distance still at 8–14 m.
- **Nothing re-clamps:** `RefreshCameraPose` places the camera at whatever `isoDistance` is (`:541–555`). That's his "stays in this close view which is cool".
- **The snap:** the next zoom input clamps hard (`:388` triggers, `:372` scroll): `Mathf.Clamp(…, 24, 48)`. That is 8 → 24 m in one frame: 16 m in 67 ms at the Mac's 15 fps (240 m/s). That's his "INSTANTLY SHOOTS up ONLY if i try to zoom".
- **The same bug, reversed:** entering a house view from beyond 22 m and orbiting during the turn leaves the distance above 22 until the next zoom press snaps it down.

#### 4.3.2 The rule

1. **Close zoom outside at night, everywhere.** While following Ace, `DistanceMin` = 8 m, from a new field `nightZoomMin` (default 8; the house's own 8 stays separate). `DistanceMax` = 48 m outside (`maximumDistance`) and 22 m in a house view (`houseDistanceMax`).
   - By day nothing changes: 24–48 m around the café's middle.
   - With day framing off, the night framing's own 12–34 m keep their meaning. The scene has day framing on.
2. **Limits are never applied in one frame.** Every `LateUpdate`, after `StepTurn` and before `RefreshCameraPose`: when the distance is outside the range, it moves back toward it at up to 35 m/s. The speed ramps up over 0.25 s and slows over the last few metres. There is no overshoot at any frame time up to 0.1 s.
3. **Input never snaps.** The zoom paths (`:372`, `:388`) clamp to the range widened to include where the distance is now. They can't push it further out of range and never pull it in. A frame with zoom input skips the pull in rule 2, so the two never stack.
4. **Turns get two parts.** The angle part keeps `houseTurnSeconds` (0.8 s). The distance part eases (smoothstep) over max(0.5 s, 1.5·|Δ|/35 m/s), so it peaks at 35 m/s or less. Orbit and tilt input cancel only the angle part; zoom input cancels only the distance part.
5. **Leaving a house or the bins.** The angle eases back to the outside angle. The distance eases back to the outside distance only if he didn't zoom while inside (a flag set by zoom input while `inHouse`). If he did zoom, his distance stays. A zoom the view set by itself is undone.
6. **R3 (home) eases instead of jumping.** The angle takes 0.5 s; the distance follows rule 4.
   - Outside at night, home is the day's home that `FollowAce` carries over (yaw 0°, pitch 43°, 34 m).
   - In a house view, home is the house's own (set at `:475–478`).
   - By day it eases the same way (§3 item 7).
7. **Cuts stay cuts.** At the night's end, `FollowAce(false)` restores the day's view under the morning curtain (`NightWalk.cs:407`, `CafeViewMode.cs:452–460`). Unchanged.

The per-frame pull (rule 2), stated exactly:

```
// CafeViewMode.LateUpdate, after StepTurn(), before RefreshCameraPose() — every frame, day and night
float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
float lo = DistanceMin, hi = DistanceMax;
float off = isoDistance < lo ? lo - isoDistance : isoDistance > hi ? hi - isoDistance : 0f;  // signed metres back into range
if (Mathf.Abs(off) > .001f && !zoomedThisFrame)
{
    limitSpeed = Mathf.Min(35f, limitSpeed + 140f * dt);           // 0 → 35 m/s over 0.25 s
    float speed = Mathf.Min(limitSpeed, 4f * Mathf.Abs(off));       // slows over the last ~9 m
    isoDistance += Mathf.Sign(off) * Mathf.Min(Mathf.Abs(off), speed * dt);
}
else limitSpeed = 0f;
// Zoom paths (:372, :388): isoDistance = Mathf.Clamp(isoDistance - step,
//     Mathf.Min(DistanceMin, isoDistance), Mathf.Max(DistanceMax, isoDistance));
```

The same rule applies to pitch at 90°/s if its limits ever move. Today they don't (day framing is on).

#### 4.3.3 Files that change

- **`Assets/Scripts/CafeViewMode.cs`:**
  - `DistanceMin` and `DistanceMax` (`:191–192`);
  - the zoom paths and their `turning = false` (`:364–389`), split by part;
  - R3 (`:390–396`) becomes eased;
  - `ExitHouseView` (`:482–488`) gains the zoom-inside flag;
  - `TurnTo` and `StepTurn` (`:490–508`) get two parts;
  - `LateUpdate` (`:525–539`) gets the per-frame pull;
  - a new field `nightZoomMin` (default 8, range 4–24).

  `OrbitTo` (`:405–412`) stays a hard set for labs and scripted views. No scene change: the new field's default applies.
- **`Assets/Editor/ControllerChecks.cs:145–148`:** R3 now eases. Wait 0.6 s, then assert home, and add "no frame over 40 m/s".

#### 4.3.4 The lab: `NightZoomCheck`

**Menu:** `Fixit Fidget › Night › Night zoom - from the bins to Grace's house (lab)`. It is a PlayLab with the night labs' setup: a fresh lab night, the man met, Ace 5 m from the bins. A virtual pad drives it at the Mac's real 15–20 fps.

**Steps:**
1. **At the bins** (`Lodger.InBinsView` true): hold RT for 1 s. The distance reaches 8.0 m (±0.05). Photo.
2. **Walk to Grace's pavement** with the left stick (about 43 m from the dumpster at (4.4, 0, 18.9): west along the rear lane, south down the café's west side, across the west street). Hold the right stick left for 1.0 s from the frame the bins view ends.
3. **After the bins view:** with no zoom pressed, the distance stays at 8.0 m (±0.1). Photo while walking.
4. **At her pavement:**
   - RT for 0.3 s: no change, because 8 m is the minimum;
   - LT for 0.5 s: about +11 m (22 m/s ±10%);
   - R3: home (34 m, 0°, 43°) within 1.5 s.

   Photos after LT and after R3.
5. **Into her house** (her door opens for him, as in the GraceHouse lab): first set 40 m with LT, then walk in with the right stick held through the turn. The distance eases into 8–22 and arrives within 1.5 s.
6. **A day phase after the night:** the limits read 24–48 m around the café's middle, and R3 eases.

**Pass, every frame:**
- **Speed:** |Δdistance| / dt ≤ **40 m/s**. At 15 fps that is 2.7 m in one frame; the bug was 16 m.
- **Range:** the distance is never outside the limits for longer than the pull takes (1.5 s at most).
- **Limits:** they read 8–48 m outside and 8–22 m in a view.

**Report:** a CSV trace (time, dt, distance, limits, state), the photos, and pass/fail.

**Re-run:** Controller; Night 0, 1 and 2; Grace at home (133 / 24 / 31) and her window; the Night tour, whose "nearest" photos become 8 m outside, which is expected.

#### 4.3.5 What can break

- **The camera can sit inside a building's shell.** At 8 m and 43° the camera is 5.5 m up, so when Ace hugs a wall it can end up inside a shell. The see-through fades what stands between camera and Ace (`NightSeeThrough.cs:230`), but from inside a shell you see through its back faces. Watch the walking photo and the Night tour's nearest photos.
- **The café's cut-away walls at night** follow Ace's sightlines (`CafeViewMode.cs:578`, `:597–603`): fine at 8 m.
- **The Night tour reads the limits** (`NightTour.cs:113–124`): it photographs 8 m now. Expected.
- **Other labs** — Bark, Juice, AceTurn and Performance — use `OrbitTo` and the day's limits: unaffected.

---

## 5. Unity steps (for the Producer, one at a time)

1. **Unity A: the night zoom.** `CafeViewMode.cs` and the new `NightZoomCheck`, then the ControllerChecks wait. Run NightZoomCheck, Controller, Night 0/1/2, Grace at home and her window, and the Night tour. Pass: §4.3.4 green, the others at their 13 Oct counts.
2. **Unity C: the drinks.** The new step `Fixit Fidget › Cinematics › Drinks close-up - straight on (scene)`, with before and after photos; then `Playtest 4 › Bench › 2 - The dispenser's frame` with the new checks; then Stations, Day 1 guide, Controller and Pause. Pass: §4.2.4's checks, and photo 1 level.
3. **Unity C: intro v2.** `DayIntro.cs` and `DayIntroCheck.cs`, then `Fixit Fidget › Cinematics › Day intro - a check`. Best done after Audio's first cues and NPC's settled guests. Pass: §4.1.9, plus the storyboard for Art Direction's eye.

## 6. Checks

- **Intro:** DayIntroCheck (§4.1.9), plus "Day intro - play it" by hand.
- **Drinks:** DispenserFrameCheck (new close-up checks); Bench 1's new rule; Stations; Day 1 guide; Controller; Pause.
- **Night zoom:** NightZoomCheck (§4.3.4); Controller; the night labs.

## 7. Open questions (only he can answer)

1. **The drinks:** which picture felt weird? The close-up after E, the overhead before it, or the swoop? (Show him §4.2.5's photos 1–3.)
2. **The police car** the morning after being caught: yes or no? It implies a consequence the game doesn't have yet.
3. **Rain:** worth a day field and a VFX build, after Days 1–3 work?

## 8. Flags

1. **The Bible's camera list is out of date.** §2 names five Cinemachine cameras; the scene has seven: also `CmWalkingFirstPerson` and the Drink station camera. On top of those, the intro makes one at run time. This is documentation, not a conflict.
2. **A cursor-on-a-fixed-frame drinks view** would keep everything in frame, but it changes how the drinks are worked. That is beyond the 14 Oct list: flagged, not built.
3. **Rain and the police car need new data.**
   - Rain needs a new `DayDefinition` field (Director and TD).
   - The police car needs a saved "caught last night" fact (TD: migration and test). Until then it works only within one session.
4. **`queue.door` changes when the first customers arrive.** That is pacing (Director and Progression), not only looks.
5. **The intro's history is shared.** It lives in PlayerPrefs (per machine), so two saves share it. It's cosmetic.
6. **The workspace paths are split.** The brief is under `/home/claude/cafe/brief`; the repo and the specs folder are under `/root/cafe` (the shell's `~`). This spec is written to both `specs/` folders.
7. **The feel sheet** (`~/cafe/feel/feel-sheet-2026-10-14.md`) hadn't landed when I finished, so there is nothing to agree or disagree with yet. Where it covers cameras, compare it with §4.1.2 (lengths) and §4.3.2 (the 0.5 s eases).
