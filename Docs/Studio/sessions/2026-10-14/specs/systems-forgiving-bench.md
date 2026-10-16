# Systems: the forgiving bench, and the clumsy-hand lab

*Systems & Mechanics lead (Opus 5.5, max), 14 Oct 2026, playtest 5's "forgiving bench" session, wave 1b. Read at `playtest-2` (Mac head `fab65d3`, clone `431f081`). Spec only: no code, no Unity, no Mac. Built in the Producer's Unity A. Every distance here is relative to a device, a piece, a flight or a spot, or in 1080p screen pixels; none assumes today's 2× camera or 3× watch.*

## What he will see

He grabs the yellow cloth off its rest: it is under his cursor at once, a soft wad, no lag. He drags it onto the lens and rubs. The cloth presses flat and bunches toward the way he pushes. The camera shifts a millimetre or two under his hand. The rub's hiss gets louder the faster he goes. The first pass smears the fingerprints into haze, the next two clear it, and it gleams. The cloth hops back onto its rest by itself. He grabs the brush and two short scrubs take the film path. On Day 2 he holds the tweezers on the jammed shutter. It lifts with a little wobble and flies into the steel scrap tray with a clink. He holds on the bright new shutter waiting on the pale pad: it flies over, hovers over its seat, and the same hold presses it in with a snap. On the phone, each screw he unscrews pops out of its hole and arcs into its own cup with a tink. The back cover pops where he pried it, lifts off and lands in the dark well. Nothing ever lies loose on the mat, falls through the table or hides under the device. Putting things back is holding on where they go. If he loses track of a speck, Tab flashes what's left.

---

## 1. What I did

- Read the common brief, the addendum and my brief. Read my role file and the Bible (§1, §2, §4, §5, §7). Read the four Project docs and both 13 Oct playtester reports. Read the five specs that are in (UI/UX, Art, Audio, Cinematics, NPC), where they touch the bench.
- Read the bench code in full: `ItemInspector`, `BenchInteractable` (`BenchHand`), `ToolPickup`, `Screw` (and `ScrewSocket`), `ScrewTarget`, `RemovablePart`, `ReplaceablePart` (and `LoosePart`), `GrimeSpot`, `BenchStage`, `RepairChecklist`, `BenchCards`, `JobBase`, `RepairJob`, `GraceCameraRepairJob`, `DeviceDefinition`, `DeviceBounds`, `DeviceWake`, `GamePointer`, `PadCursor`, `PadInput`, `AimAssist`, `ControlHints`, `PlayLab`, `BenchLab` (Bench 3) and `Playtest3Session4Steps`. Also read the bench parts of `Juice`, `PlayerInteractor`, the labs and the edit checks. I dumped the three device prefabs' bench components with a script.
- Ran two scratch simulations (not repo code):
  - **Cleaning:** a port of `GrimeSpot`'s own dirt and smudge masks, used to calibrate the clean width and strength so that a spot takes two to four strokes (§4.4.5).
  - **Time:** Fitts's law over Art's mat layout, with published mouse and thumbstick throughputs (§4.8).
- One outside source, for the clumsy hand's pointing constants (Natapov, Castellucci & MacKenzie, GI 2009).
- Not done: Unity work and code (this wave forbids both).
- The Technical Director's (TD's) one-scale spec had not landed when I finished.
- The feel sheet landed at 07:36, while I was writing, and was revised at 07:46. I read both before finishing and compared the spec against its targets T1–T13 (last section). Three changes came from it:
  - I adopted its T13: one hidden frame is a failure;
  - its T2 made me trim the cover's beat and the broken part's lift;
  - the revised T1 (a screw's flight ≤ 0.35 s) gave screws their own faster flight.

## 2. The decisions and their reasoning

| Call | My pick | Why | Rejected |
|---|---|---|---|
| **The swap: his "click, then hold" vs UI/UX's "one hold"** | **One mechanism that does both.** A press on the fresh part (any length) starts its flight to the seat at once. If the button is still down when it arrives, the same hold presses it in (0.35 s). If it was let go, the part waits, hovering over its seat, and a hold on it presses it in. | It is the grammar the screws already use. Today a hold on an empty hole fetches the screw and the same hold drives it in. Let go mid-fetch and the screw hovers until the next hold (`ScrewSocket.HoldTick`, `Screw.cs:409-414`; `FetchRoutine`, `:224-257`). Both of their descriptions are true of it, so the card keeps one verb ("Press in"). Audio's `part.fresh` fires on the press frame either way. | **Click-then-hold only.** It costs two inputs and a cursor trip to the seat. It also forces a click/hold boundary: what does a 0.3 s press do? A clumsy press is ambiguous. **One continuous hold only** (let go early and the part goes back). It punishes exactly the click he described. |
| **The broken part: off on release, or after a lift time** | **After a lift time.** 0.25 s of hold pinches it. It lifts with today's wobble (0.25 s) and flies to the bin whether or not the button is still down. | One short hold commits it. Nothing hangs in the air waiting for a release: a held button with nothing happening reads as "stuck". | **On release.** The part would hover for as long as he holds, which is dead time, and an early release mid-lift would need its own rule. |
| **Cover: off by itself after the pop, or on one more click** | **By itself.** Pop, a 0.15 s beat at the popped angle, then it lifts and flies to its well. | His approved change says "everything after that is automatic", and the pop is the physical moment he keeps. One input for the whole cover. | **One more click.** It adds an input that teaches nothing. |
| **Cover back on** | A press on the cover in its well (or on the open face, at the "Cover on" step) sends it home with a snap. **With this job's work left under it, it takes a 0.4 s hold** ("close it anyway"). | One click in the common case. A stray click from the fresh-part pad next to it can never close the device over unfinished work. | A press always: one slip and he has to pry it off again (about 1.6 s). A gate that forbids closing: hand-back needs reassembly (`JobBase.CanHandBack`), so "close it anyway" must exist. |
| **Screw cups** | **One cup per screw, in removal order.** Each screw takes the lowest-numbered free cup that no resting pose of this device covers. When none is free, it joins its own job's last cup. | Each screw gets its own clean tink in an empty cup, and the screws are countable. The cup that empties when a hole is held shows which screw went back. Six cups hold the phone's 2 and the watch's 4. | **Grouped, one cup per device.** The screws heap and their count can't be read. |
| **Cloth and brush: what "let go" does** | Press on the tool: it is in the hand, under the cursor, with no lag. Over its own dirt, while held, it rubs. **Let go over the device and it lifts but stays in the hand. Let go anywhere else and it goes home.** A tap on the tool also keeps it. **It goes home by itself the moment its last spot on this device clears.** | His words: "point at the cloth … it should just grab and i just drag it across an object". A release mid-wipe is a lift, not putting the cloth down. If every release sent the cloth home, a first-timer would make a trip to the rest after every stroke: 6–10 lifts on Day 1's clean, about 0.7 s each. Going home after the last spot gives today's lift-off moment (the gleam and the wake, seen clear) for free. | **Home on every release** (decision 4 read literally): a trip per lift. **Sticky as today**: the tool stays on the cursor after its work is done. |
| **Clean width** | **Relative to the spot.** The brush's half-width is 0.75 × the spot's radius and takes 100 % of the painted dirt per pass at its core. The cloth's half-width is 1.0 × the spot's radius at 80 % per pass, with smear first. There is a floor of 10 px and a cap at the tool's own contact width. **At 90 % the next pass finishes the spot.** | Measured on the game's own masks with strokes placed blind (pessimistic): median **3 strokes**, 90th percentile **4**, and **93–95 %** of spots within four (§4.4.5). Today the width is fixed in metres (`GrimeSpot.cs:45`), so the 2× camera and the 3× watch clean at a half and a third of it. | **Today's metres at real scale.** The ratio to the spot swings from 0.27 (the phone's 36 mm grime under the 4.8 mm brush: 11+ strokes) to 1.0 (the cloth on the lens). **0.55 × radius:** a median of 5–6 strokes. |
| **Hit areas** | **Screen-space picking.** Screws and holes are hit within **28 px** of their centre. Parts, covers and tools are hit within their outline plus **12–16 px**. Priority goes to the current step's targets, then the nearest. A hold stays on its target until release. | Real-proportion screws are 8–16 px wide on screen (Art A8, at 3.1–4.0 px/mm). Today's picking is a physics ray on each collider (`ItemInspector.cs:461`), which a real-size screw defeats. | Bigger colliders. They would block each other, catch the tools and still be metres-based. |
| **Physics** | **None at the bench.** No piece is ever a rigidbody. The magnet goes. The catch stays only as a guard that logs an error if it ever trips. | Rule 7: no physics in the core loop. Every way a piece was lost (§4.7) came from a rigidbody. | Keeping physics as a flourish: a bouncing screw is exactly what tunnels at 15 fps. |

## 3. Decisions his to overrule

1. The swap's press: any press flies the part, a held press presses it in, and a press let go early leaves it hovering (§4.3).
2. The broken part flies to the bin after its 0.25 s pinch, whether or not the button is still down.
3. The press-in hold lasts **0.35 s**.
4. The cover flies to its well by itself, 0.15 s after its pop.
5. The cover goes back on with one press, or with a 0.4 s hold when this job's work is left under it.
6. One cup per screw, in removal order; no cup that the device covers at rest.
7. Cloth and brush: let go on the device and the tool stays in hand; let go elsewhere and it goes home; it goes home by itself after its last spot. This is not decision 4's literal "let go → back to its place".
8. The playtest-4 cloth lift-off is dropped. Its job (showing the gleam and the wake) moves to the auto-home.
9. The clean widths and strengths in §4.4.5, plus the 90 % finisher.
10. Grease smears on the first pass, then clears.
11. Hit radii: 28 px for screws and holes; outline + 12 px for covers, + 14 px for parts, + 16 px for dirt and tools.
12. A mouse spin always settles to the nearest quarter turn (`faceSnapWithin` 22° → 45°), so the device only ever rests square.
13. Pad rumble, and a new **Vibration** settings row (on by default; UI/UX places it).
14. The "New part" word pop is retired (`ReplaceablePart.cs:170`): the pad and the glow carry it.
15. Scrap can't be picked up any more (today: "Hold to move it").

---

## 4. The mechanism, verb by verb

### 4.0 What every verb shares

**4.0.1 The compartments (data on `BenchStage`).** Art's mat FBX carries the empties (art spec A1). `BenchStage` gets these fields, filled by the new stage step (§7, step 14):

| Field | From | Holds |
|---|---|---|
| `Transform matFrame` | `BM_RepairMat` root | Converts mat-millimetres (Art's frame: +x the player's right, +y away) to the world |
| `Transform[] cups` (6) | `Cup_1`…`Cup_6` | One screw each, lying on its side, axis ±30° off radial (more than one only when stacked) |
| `Transform padSmall, padLarge` | `Pad_Small`, `Pad_Large` | The fresh part waiting, flat, ±4° askew |
| `Transform wellSmall, wellLarge` | `Well_Small`, `Well_Large` | A cover, outer face up |
| `Transform scrapTray`; `Transform[] scrapSlots` (3, children of the tray) | `BT_ScrapTray` | Broken parts, flat. Slots on the tray's right two-thirds |
| `Transform toolRest`; `Transform[] toolSlots` (5, ToolType order, as today) | `BT_ToolRest` | The tools at rest |

**Per device** (Art's table A1), a new serialisable `BenchLayout` on `DeviceDefinition`. Mat millimetres, read at `BeginInspection`. The tray and the rest move to them; the device lies at `rest`.

| Field | Phone | Camera | Watch |
|---|---|---|---|
| `rest` | (−30, −14) | (−82, 0) | (−20, 0) |
| `toolRestCentreX` | −135 | −239 | −115 |
| `scrapTray` | (−30, +133) | (−5, +115) | (−20, +107) |

**Slot rule.** A piece takes the first free slot of its kind whose footprint lies outside all four resting quarter-turn footprints of its own device, plus 6 mm. Rotations come from `MeasureSlab`. Claims are per job and are released when the piece goes home.
- Cups are numbered near to far, left column first (Art A1).
- **Phone:** its quarter-turned footprint spans x −104 … +44, which covers the left cup column (27–45 mm). So its screws take cups 4–6 (computed, not hard-coded).
- **Watch:** all six cups are free to it.
- **Fresh parts:** take the smaller pad that fits. If both pads hold other jobs' stock, the stock of the job opened longest ago is put away (deactivated) and comes back when its close-up reopens.
- **Fallback:** a cup stacks: +0.6 × head diameter up, ±0.3 × head diameter jitter.

**4.0.2 The flight (one routine for every piece).** `BenchStage.Fly(piece, toPose, kind)`, advanced in `BenchStage.Update` with `Time.deltaTime`. Pausing freezes it; the clock runs, as locked. No coroutines per piece.

| | Rule |
|---|---|
| **Path** | A quadratic Bézier from the launch point P0 to the landing point P2. The control point is the midpoint raised by **h = 0.35 × the flat distance P0→P2**. That reads as an arc at 58°: a rise projects at cos 58° ≈ 0.53. |
| **Duration** | **T = clamp(0.24 s + 0.00025 s/px × L, 0.30 s, 0.55 s)**, where L is the on-screen distance P0→P2 in 1080p px. 300 px takes 0.32 s, 600 px 0.39 s, 900 px 0.47 s. **Screws fly faster:** T = clamp(0.20 s + 0.00025 s/px × L, 0.22 s, 0.35 s), so 300 px takes 0.28 s and 500 px 0.33 s (the feel sheet's T1: a flight ≤ 0.35 s; ReStory's screws jump in one frame). |
| **Easing** | u(t) = 0.35·t + 0.65·(1 − (1 − t)³): a quick launch and a gentle landing. Position = Bézier(u). |
| **Rotation** | Slerp from the launch pose to the landing pose on u. A screw adds 1.5 tumbles about the horizontal axis across its path, fading over the last 25 %. A broken part adds a ±15° wobble fading over the flight. Covers and fresh parts turn smoothly, without a tumble. |
| **Cues** | The launch cue on the first frame, the landing cue on the last (Audio §7.5). Never a physics contact. |
| **Settle** | A scripted hop on landing: up and down over 0.11 s. A screw rises 0.25 × its head diameter (Audio's tink with its bounce tick at 55 ms); a cover 0.04 × its short side; a part 0.05 × its short side. |
| **Frame rate** | The pose is a pure function of elapsed / T, so 15 fps and 60 fps land on the same frame-for-time. The last frame sets the exact landing pose. |
| **Input** | A flight never blocks input. The next press can begin while a screw is still in the air. |

**4.0.3 Piece states.** Each removable piece (screw, cover, broken part, fresh part) is in one of: **OnDevice**, **Flying**, **InSlot**, **Hovering** (the fresh part, or a fetched screw), **Pressing**. None is ever dynamic: `Rigidbody` components are removed from the bench pieces. Ownership is unchanged: `JobBase.RegisterDetached` / `RegisterLoose`. So the hand-back gate (`JobBase.cs:39`) and the cleanup are untouched.

**4.0.4 BenchStage's magnet and catch.**
- **The magnet** (`BenchStage.cs:134-154`) is deleted with the tray zone.
- **Deleted with it:** `Watch`, `Forget`, `SettledInTray`, `TrayDropPoint`, `FreeSpotOnMat`, `InTray` (`:49-97`, `:164-185`). Their callers move to the slot API.
- **The catch** (`:124-132`) becomes **a guard**. A bench piece in InSlot more than 2 mm from its slot, or more than 25 cm below the mat, is snapped back to its slot and logs an error. Every lab fails on that error, because under this design it can't happen.

### 4.1 Screws

**What he will see.** The driver turns; the screw backs out of its own hole along its own axis, ticking every quarter turn. At the last thread: a click, a little pop up its axis, and it arcs into its cup with a tink. To put it back, he holds on the empty hole. The screw lifts out of its cup, hovers over the hole, and the same hold drives it home with a snap.

**Mechanism.**
1. **Out.** The hold and the turn are unchanged (`Screw.Turn`, `Screw.cs:139-155`; the phone's and watch's `turnDuration` 0.45 s).
   - Ticks every quarter turn: `Progress × turns × 4` (Audio §7.4), not every turn as at `:146`.
   - **`Free()`** (`:167-202`) no longer adds a rigidbody (`:180-188`), no longer sets the pop velocities (`:195-200`) and no longer calls `Watch` (`:201`).
   - It plays `screw.free`, then a scripted pop: up the screw's `Axis` by 1.5 × its head height over 0.06 s, ease-out.
   - It claims a cup (§4.0.1) and calls `Fly(screw, cupPose)` with `screw.fly` / `screw.drop`.
   - It still unparents itself and stays registered as detached; the hole (`ScrewSocket`) appears as today.
2. **Collision sounds retired.** `OnCollisionEnter` (`:204-213`) is deleted, and `screw.bounce` with it (Audio §7.3).
3. **Back in.** The hold on the hole (`ScrewSocket.HoldTick`, `:409-414`) or on the screw in its cup (`ScrewTarget`, `:72-77`) runs today's fetch, now from the cup.
   - The fetch takes **the screw's T × 0.8** (minimum 0.22 s) with `screw.fetch` at launch, and ends hovering at the hole's mouth (`FetchRoutine`, `:224-257`, with the flight's curve).
   - The same hold then turns it home: ticks every quarter, then `screw.seat`. The cup is released.
   - Let go mid-fetch and the fetch completes; the screw hovers, and the next hold turns it (today's rule, kept).
4. **The hole** (`ScrewSocket.Make`, `:334-371`). The two cylinders (`:346-361`) become Art's decal: a quad 1.15 × the head's diameter, `BT_ScrewHole.png` (A8). A thin box stays for the physics ray's surface point. Hit testing is the picker's (§4.6).
5. **Lengths become relative** (TD's models): `threadLength` (0.006 m today) becomes 2.0 × the head's diameter, or the model's shank length if it carries one.

### 4.2 Covers

**What he will see.** The pry sits in the seam; he holds; a creak; the edge pops up. A beat later the cover lifts off and lands, outer face up, in the dark well. To close up, he clicks the cover in its well: it flies home and clips on. With work still inside, a click does nothing and a short hold closes it anyway.

**Mechanism.**
1. **The pry is unchanged:** hold 0.45 s, `cover.creak`, the lever (`RemovablePart.HoldBegin/HoldTick/HoldEnd`, `:304-326`). Let go early and it slips back 0.35 (`:325`). The pop (`:337-370`) is unchanged.
2. **After the pop:**
   - a 0.15 s beat at the popped angle;
   - a lift along `Outward` by 0.25 × the cover's short side over 0.10 s, with `cover.lift`. Audio skips it within 0.15 s of the pop; here it starts at exactly 0.15 s, so it plays;
   - the cover claims a well (the smaller one its footprint fits) and calls `Fly` with `cover.clack` on landing, outer face up, its long axis along the bay.
   - State **Removed**: detached, as today (`Detach`, `:431-436`).
3. **Deleted:** the grab path (`Grabbable`, `GrabBegin/Move/End`, `:374-419`), `NearHome` with its `AtDepthOf` gate (`:423-429`), `Drop` (`:438-450`) and `OnCollisionEnter` (`:452-458`). Covers have no `snapDistance` any more.
4. **Back on.** A press on the cover in its well (any tool, or the bare hand), or a press on the device's open face when the current step is "Cover on", while its seat faces the camera.
   - If no work of this job is left beneath (`WorkBeneathToDo` false, and no part of this job with `CoveredBy == this` unreplaced), it flies home (`Fly` to its seat pose) and snaps: `part.on`, today's `SnapHome` end (`:460-486`) without the rigidbody lines.
   - If work is left beneath, the press needs a **0.4 s hold**; the hover gives UI/UX's "why not".
   - If its seat faces down, nothing happens, and the hover's "why not" is "Flip it over first" (the card already says flip).
5. **`Activate()`** (the labs' hand, `:490-549`): seated means a self-pry, the pop, the beat, then the flight to the well. In its well, it flies home. In edit mode (no frames), every flight completes at once: the same states, the same slots.

### 4.3 The two-hold swap

**What he will see.** He holds the tweezers on the broken part. A quarter second in, they pinch, and it lifts out with a wobble and flies into the steel tray with a clink. He holds on the bright new part on its pad. It flies over and hovers just above its seat, aligned. If he is still holding, it presses down and snaps in. If he only clicked, it waits there, hovering and glowing, until he holds on it.

**Mechanism.**
1. **The broken part** (`ReplaceablePart`).
   - The hold pinches as today: `pinchDuration` 0.25 s, `part.pinch` on the press frame (`:175-200`).
   - Let go before it takes and the pinch keeps its progress, decaying at 0.5 per second. Today it resets to 0 (`:199`).
   - At 1, `Lift()` (`:229-241`) unparents it and registers it detached. It plays `part.lift` and rises along `Outward` by 0.6 × its thickness + 0.15 × its short side over 0.25 s, with today's wobble (±6° at 42 rad/s, decaying: `:253-255`).
   - Then it claims a scrap slot and calls `Fly` with `part.drop` on landing. The hold ends by itself: `HoldTick` returns true.
2. **The fresh part** (`LoosePart`) is presented on its pad when the close-up first opens: `PresentFresh` (`:158-171`), with no rigidbody and no `Watch` (`LoosePart.Make`, `:386-433`, minus the physics).
   - Any press on it with the tweezers, **while the seat is empty and facing the camera**, starts its flight at once: `part.fresh` on the press frame (Audio). The target is a hover pose: the seat's pose raised along `Outward` by 0.5 × its thickness + 0.1 × its short side, rotation aligned to the seat.
   - **Arrival with the button down:** the press-in runs while held. Progress goes 0→1 over **0.35 s** as the part descends; the last 15 % of the travel is a snap. At 1: `SeatFresh()` (`:305-330`), `part.replace`, the sparkle.
   - **Released before arrival:** it completes the flight and hovers, bobbing ±0.04 × its thickness at 2 Hz. A hold on it, or on its seat, presses it in.
   - **Released mid-press:** the progress is kept, decaying at 0.5 per second, so it rises back slowly. A hold finishes it.
   - A press while the broken part is still in, or while the seat faces down, does nothing. The hover gives the part's own "why not".
3. **Deleted from the player's path:**
   - the carry at depth: `ReplaceablePart.Follow` (`:243-256`), `HeldLift` (`:39`) and `LoosePart.HoldTick`'s carry (`:467-476`);
   - the release drop: `Drop`/`Release` (`:259-276`) and `LoosePart.HoldEnd`'s drop (`:480-489`);
   - the seat gates: `seatDistance` (`:34`), `SeatWouldTake` (`:286-290`) and `AtDepthOf` (`:293-302`);
   - the grab of loose parts (`:204-227`): **scrap can't be moved** (today, "Hold to move it", `:129`).
   - `BenchHand.PointAtDepth` (`BenchInteractable.cs:33-40`) loses its last caller and goes too.
4. **`Activate()`** (`:334-365`) runs the same beats by itself: the pinch, the lift, the flight to the bin, the fresh part's flight, then a self-timed press-in of 0.35 s. In edit mode, every flight completes at once.
5. **Grace's camera's fresh part** is Art's `Clean shutter mechanism` (A7) once it lands. Until then it is today's `Working shutter blade`. The mechanism doesn't care which.

### 4.4 Cloth and brush by direct grab

**4.4.1 The grab rule** (all five tools share it; the rubbing is the cloth's and the brush's).

| Input | Result |
|---|---|
| Press on a tool at rest (within its hit area, §4.6) | It is in the hand on that frame: `tool.pick`, the previous tool goes home |
| Held, moved | Its working tip is **exactly** under the cursor every frame (no spring for position) |
| Held over its own dirt (cloth or brush) | It presses down and rubs (§4.4.3) |
| Released over the device | It lifts and stays in the hand |
| Released over its own place, or a tap (press and release within 0.30 s, the cursor moved under 24 px) | It stays in the hand ("[Click a tool] Take it", the card's words) |
| Released anywhere else (the mat, empty space) | It goes home: a 0.2 s flight, `tool.down` on landing |
| Its last spot on this device clears | It lifts (0.15 s) and goes home, held or not. The press is consumed until the button is up |
| Right-click / B, another tool taken, LB/RB, the close-up ends | It goes home, as today |
| Pad | LB/RB still cycle the tools (sticky, as today); A or RT held on dirt presses it down. A press on a tool with the pad's cursor follows the rows above |

Point tools (driver, pry, tweezers) keep their holds: a hold begins only on a press on its target (today, `HandleBenchPress`, `ItemInspector.cs:1229-1251`). A driver dragged across the device does not turn the screws it passes over.

**4.4.2 The tool in the hand** (`ToolPickup`).
- **Position:** the tip sits at the cursor's surface point. Over a dirt spot that is the cursor ray ∩ the spot's plane, so a tiny spot and a 15 fps frame jump both behave.
- **Hovering** (held but not over its dirt, or taken but not pressed): 0.1 × the tool's contact width above the surface.
- **Rotation and shape keys:** eased in closed form, ω = 14 /s. Turns can lag; the tip never does.
- **Deleted:** the eased target and the spring (`Follow` `:181-184`, `Spring` `:270-280`), the cloth's fold lead and lift (`:69-76`, `:213-236`, `LiftOff`/`Land` `:294-309`) and the `padSize` rescale (`:58`, `ApplyPad` `:325-329`). Art's held mesh replaces the scale trick.
- **The cloth:** at rest it is `BT_Cloth`. In the hand it swaps to `BT_ClothHeld` (A4); the pick-up motion hides the swap.
  - It lies flat on the surface normal. Its +x follows the stroke **axis** (direction modulo 180°, eased).
  - Key `Press` = 1 while held on the device, 0.4 while held in the air, 0 while lifted.
  - Key `Bunch` = clamp01((s − 60) / 840), eased ω 14, where s is the stroke speed (§4.4.3).
  - At a reversal, s passes near 0, so `Bunch` is about 0. The cloth's local x-scale flips sign there, and the bunch rises again on the new leading edge with no visible jump. That gives one key for both directions; Art's A4 has one `Bunch` key.
- **The brush:** its chisel lies across the stroke axis. Key `Bend` (A5) = clamp01(s / 700) while pressed, mirrored at reversals the same way. Its handle tilts ±12° against the velocity.
- **Pad, held still on dirt:** the footprint orbits the cursor at 0.35 × its clean radius, 2 turns a second. That is today's `MotionOffset` (`:363-377`), made relative.

**4.4.3 Feeling the impact: one number.** `BenchFeel.StrokeSpeed` = **Audio's s**, verbatim (audio spec §7.1):
- `raw = |GamePointer.Delta| / dt × 1080 / Screen.height`;
- add `padScrubRate × 1080 / Screen.height` while the pad's cursor is pressing in place (`ItemInspector.cs:96`, 180);
- smoothed in closed form: `s += (raw − s)(1 − e^(−dt/0.06))`.

That one number drives all of these:

| What | Rule |
|---|---|
| **The rub loops** (`brush.rub`, `cloth.rub`) | Audio's mapping, unchanged. Volume `v = (clamp01((s − 40)/960))^0.6`; pitch `0.92 + 0.16 × clamp01(s/1200)`. Brush grit ×1.0 when dirt came off in the last 0.15 s, else ×0.55. Start on contact (`Sfx.Loop`, fade 0.06 s); stop 0.12 s after release or leaving the device. |
| **The squeak** | Audio §7.2, unchanged: the smoothed direction reverses (dot < −0.3·\|v\|·\|vPrev\|), s ≥ 250, and grease came off in the last 0.15 s. Replaces the 0.42 s timer (`GrimeSpot.cs:400`). |
| **The look** | The cloth's `Bunch`, the brush's `Bend` (above). |
| **The device budges** | An offset in the mat plane along the smoothed stroke direction, up to **6 px** on screen at 1080p (converted at the device's depth), scaled by clamp01(s/900). It springs back critically damped (ω = 20, closed form, like `ItemInspector.Coast`'s tilt). It is added in `ItemInspector.Pose` on top of the device's rest point and never changes yaw, tilt or face. |
| **Pad rumble** | While rubbing: low motor = 0.10 + 0.25 × v, high motor = 0.05 + 0.30 × v × (dirt came off in the last 0.15 s ? 1 : 0.4). Pulses of 60 ms: screw free (0.15/0.35), cover pop (0.35/0.45), press-in snap (0.45/0.60), spot clean (0.20/0.50, 120 ms), tool grab (0.10/0.20, 40 ms). Zero on release (within 0.1 s), at pause, on focus loss, at the close-up's end, or with the new Vibration setting off. Through a new `PadInput.Rumble(low, high)` and `PadInput.StopRumble()`. |
| **Clean** | The gleam and `cloth.gleam` (today, `GrimeSpot.cs:536-541`) or `grime.clean`, then the auto-home (§4.4.1). |

**4.4.4 The press pass, and smear first** (`GrimeSpot.Erase`, `:428-463`, replaced by a **pass-based** erase).
- Per frame, the tool's footprint (a disc of clean radius r) sweeps the segment from last frame's contact to this frame's.
- For each texel within r of the segment: **removed = start[i] × s_tool × f(d) × chord / fullChord**.
  - f(d) is today's falloff: 1 inside 0.6 r, linear to 0 at r.
  - chord is the length of the segment inside the texel's r-disc; fullChord = 2·√(r² − d²), d being the texel's distance from the segment's line.
- So one full pass over a texel removes exactly **s_tool × f(d)** of its painted dirt: at any frame rate, any speed and any zoom. Today the removal depends on how many 6 px pieces fit under the disc (`Scrub`, `:365-405`), so it changes with screen size and zoom.
- **Smear first (smudges only):** each texel keeps a `smear` value 0→1.
  - A pass over a texel whose smear is below 1 removes only 0.15 × the normal amount, raises smear by 2 × f, whitens its RGB 30 % toward (0.95, 0.96, 0.97) and blurs its alpha along the stroke: `a[i] = 0.6 a[i] + 0.4 a[i − dir]`, only among texels with start > 0. Total grease is unchanged, so `Progress` stays honest.
  - Passes after that clear at s_cloth. On screen, the prints turn into streaky haze, then go.

**4.4.5 The widths, and the 90 % finisher** (calibrated by simulation on ports of `PaintDirt`/`PaintSmudge`).
- **Spot radius:** R = ½ × √(sx × sz), the patch's geometric-mean radius in metres (`lossyScale`).
- **Clean radius:** r = clamp(k × R, 10 px on screen, the tool's contact cap), with the cap at **0.6 × the tool model's contact width**: the brush's chisel is 24 mm (`bench_tools.py:173-175`), so 14.4 mm; Art's held cloth pad is 45 mm, so 27 mm.

| Tool | k | s_tool (per pass) | Smear | Strokes to clean (400 random spots, blind placement ±0.45 R): median / 90th percentile / share ≤ 4 |
|---|---|---|---|---|
| Brush (dirt) | **0.75** | **1.0** | — | **3 / 4 / 93 %** |
| Cloth (smudge) | **1.0** | **0.8** | first pass | **3 / 4 / 95 %** |
| (rejected) brush | 0.55 | 1.0 | — | 4 / 7 / 63 % |
| (rejected) cloth | 0.85 | 1.0 | first pass | 3 / 5 / 90 % |

- **The 90 % finisher:** when `Progress` crosses 0.90, the spot arms. It completes (today's `Clean`, `:524-552`) once the footprint's centre has travelled **0.6 × the spot's diameter** of path over the patch, or after 0.4 s of a pad's in-place rub over it.
- `cleanAt` 0.97 still completes at once.
- The minimum is 2 strokes: no single ordinary pass reaches 90 %.
- **What each spot gets** (real proportions; today's prefab sizes ÷ 2 for the camera, ÷ 3 for the watch):

| Spot | R | Tool | r |
|---|---|---|---|
| Lens | 9.6 mm | Cloth | 9.6 mm |
| Film path | 7.6 mm | Brush | 5.7 mm |
| Film guide | 6.3 mm | Brush | 4.7 mm |
| Watch grime | 8.3 mm | Brush | 6.3 mm |
| Phone grime | 18 mm | Brush | 13.5 mm (under the 14.4 mm cap) |

  - No spot on the three devices hits the cap today. It exists for a bigger spot Art might author later: the fully cleaned core of a stroke (1.2 r) must stay narrower than the tool that made it.
- **Retired:** `StrokePixels` (`:43`), `BrushRadius`/`ClothRadius` (`:45`), `ItemInspector.scrubPower` (scene 0.5, `:86`) and `ScrubTravel`'s pixel model (`:990-995`). Wide Brush's upgrade multiplier moves onto k: ×1.25 per level, never above the cap. The upgrade stays.

**4.4.6 Playtest 4's cloth lift-off: dropped.** It existed so the gleam and the waking screen were seen with the cloth clear (`ToolPickup.cs:27-36`). The auto-home after the last spot gives that moment for every device, and mid-job the cloth stays where his hand is.

### 4.5 Show dirt (UI/UX's Flash)

UI/UX owns the binding (Tab/View: a tap; a hold of 0.35 s shows the controls) and the look (gold, rings, outlines). This is the mechanism and its data.
- **`BenchWork.Flash(JobBase job, float seconds = 1.5f)`** (new static, `Assets/Scripts/BenchWork.cs`):
  - calls `GrimeSpot.Flash(seconds)` on every spot of the job that is not clean and **not under a seated or popped cover**;
  - raises `BenchWork.Flashed(job, seconds)` for UI/UX's `BenchGlow`;
  - plays `bench.showleft` (Audio) on the press frame. A second call restarts it.
- **`GrimeSpot.Flash`:**
  - on its first frame, it uploads the mask once with every texel whose alpha is above 0.02 raised to at least 0.6 (texels at 0 stay 0), so the tiniest specks show, as PowerWash's indicator does;
  - it switches `_EMISSION` on with `_EmissionColor` = gold `#F2A61F` × a curve: 0→1.5 over 0.1 s, held to 0.5 s, eased to 0 by 1.5 s. Transparency multiplies it by each texel's own alpha, so only dirt glows;
  - at the end it re-uploads the true mask and switches emission off. Strokes during the flash keep the boost;
  - it runs on `Time.unscaledTime`.
- **Data for the rings and the outlines.** `BenchWork.WorkLeft(job, List<BenchWork.Item>)`: every target with work left in any step, as (target, kind, screen centre, screen radius, hidden?, remaining share for spots). Hidden is "under a cover" or "not in sight" (the `Seen` ray, `ItemInspector.cs:1139-1155`). A spot under a cover lists its **cover** instead.
- **`Step.targets`** (UI/UX's ask; built in `RepairChecklist.Build` from the parts' states, so it stays true under the auto-tray). A piece in flight is never a target. `left` and `total` count them, for the pips.

| Step | Targets |
|---|---|
| Unscrew | the cover's screws still in |
| Pry off | the cover, seated or popped |
| Brush / Wipe | its spots not clean and not under a cover |
| Lift out | the broken part, in its seat |
| Press in | the fresh part (on its pad or hovering) and its seat |
| Cover on | the cover, in its well |
| Screws back | that cover's empty holes |

- **The stuck timer's event** (UI/UX's ask). `BenchWork.Progressed(job)`, throttled to 4 a second, on any work advance:
  - a hold's progress rising;
  - a spot's `Progress` +2 %;
  - a piece landing;
  - a target done;
  - a step done.

  Plus `BenchWork.TargetDone(job, target)` on the frame a target finishes, so the card ticks without waiting for its 0.1 s poll.

### 4.6 Hit areas and hover

**The picker** (new, in `ItemInspector`; its pure math in `BenchRules`, §7 step 1).
- Each frame it projects every candidate into screen space at 1080p reference pixels (× `Screen.height` / 1080). Candidates are the job's `BenchInteractable`s, its loose pieces, its `GrimeSpot`s and the tools at rest.
- It measures the cursor's **normalised distance**: the distance to the shape ÷ its hit radius, 0 at the centre and 1 at the edge.

| Target | Hit shape |
|---|---|
| Screw (in, out, in its cup) or empty hole | A circle at its work point, radius **max(28 px, projected head radius + 10 px)** |
| Cover (seated, or in its well) | The convex hull of its projected bounds + **12 px** |
| Part (broken in its seat; fresh on its pad or hovering) | Hull + **14 px** |
| Dirt spot | Its projected patch disc + **16 px**; rubbing engages when the footprint overlaps the patch |
| Tool at rest | Hull + **16 px** |

**Eligible**, in this order:
- its own `CanInteract`. A target that can't act is still hoverable, for UI/UX's "why not";
- not under a seated or popped cover. That is new for spots: `GrimeSpot.CoveredBy` is the `RemovablePart` of the same job that lists it in `faultsBeneath`, or else the one whose seated box, extended 0.5 × its thickness inward, contains the spot's centre. The watch's `Grime_Extra_Watch_1/2` are only in by geometry: unverified until the lab runs;
- facing the camera: dot(work normal, to the camera) > −0.17. A screw on the underside is never hit; the card says flip;
- in sight (the `Seen` ray), **unless it is one of the current step's targets**. Those are hit through an occluder, and UI/UX draws them dashed.

**Priority**, smallest normalised distance within each class:
1. the current step's targets (UI/UX's `Step.targets`) that the tool in hand can act on;
2. other targets the tool in hand can act on;
3. the current step's targets it can't (the hover says what is needed, and the glow is on the tool);
4. everything else.

**Further rules.**
- **A hold or a grab owns its target until release**, wherever the cursor drifts (today: `holdTarget`, `:475-479`). Drift and jitter can't break it.
- The physics ray stays for the **surface point**: the rub's contact, the pry's nearest seam (the ray ∩ the cover's plane), the rotate gesture's "empty space" (`:1234`).
- The mouse and the pad get the same hit areas.
- **Pad aim help:** today's snap within 60 px on stick release stays (`PadCursor.cs:47`, `:149-154`). Its points come from the picker's eligible targets, not the physics-ray grime. New friction: within a current-step target's hit radius the cursor moves at 0.55× (only with Aim help on). D-pad "next part" orders the current step's targets first, **including occluded ones**; today `Seen` drops them (`:1124`, `:1131`).

**Hidden parts are reached** three ways:
- **Under a cover:** take the cover off. The gate is kept.
- **Behind another piece from this view:** the current step's targets are hit through it and glow dashed, and the D-pad goes to them.
- **On the face turned down:** the card says flip; never through the device.

With the slot rule (§4.0.1), no piece the player will touch again can lie under the device.

### 4.7 Nothing is ever lost or hidden

| Today (each from the code) | Why it can't happen now |
|---|---|
| A freed screw is a 3 g rigidbody (`Screw.cs:180-188`). At 15–20 fps it tunnels through the mat or the device, and the catch (`BenchStage.cs:124-132`) puts it "over the tray", out of sight on the way | No rigidbodies. A scripted flight ends on its cup's pose by construction |
| A screw lands beside a tall device, behind it from 58°, or under the camera's overhang (the pop throws it "beside the device", `:190-199`) | Cups are fixed, outside every resting footprint of the device (§4.0.1), in frame (Bench 5, §6) |
| A loose screw lying on the device is shoved or buried when the device turns or flips (`Pose` teleports the device's colliders through it) | Nothing lies on the device. Pieces are unparented into slots off its footprint |
| The broken part carried "at depth" looks over the tray but is really over or behind the device; let go, it falls straight down: his "kept dropping a part of the camera under the camera" (`ReplaceablePart.cs:243-256`, common brief §4) | No carry. It flies to the bin |
| A thin part tunnels through the table at a low frame rate and reappears "over the tray": his "going through the table" | No physics |
| The fresh part waits in the tray under a long device's overhang (fixed 13 Oct by moving it to the far side, `:410-422`) | Its pad starts at x +90. The nearest any device reaches at rest is the quarter-turned phone at x +44: 46 mm clear (Art A1). Bench 5 checks it at every turn |
| The fresh part let go off its seat falls somewhere, maybe under the device (`LoosePart.HoldEnd`, `:486-488`) | Never dropped. It hovers over its seat |
| The magnet lets pieces settle on each other (`BenchStage.cs:134-154`) | One slot per piece. Scrap alone may stack, and it is finished with |
| A cover dropped on the mat lands on screws or the fresh part, or slides off the edge and is caught "over the tray" | It flies to its own well |
| Scrap or a loose cover can be grabbed and dropped anywhere (`:204-227`; `RemovablePart.cs:374-419`) | Scrap can't be moved. A cover only goes to its well or its seat |
| The cloth pad covers what it wipes (about 18 % of the phone's glass, measured: `bench-spec-v2.md` §2.4) | Still true while wiping, as with a real cloth. It leaves by itself when its work is done (§4.4.1) |
| Zoomed in, a piece flies out of frame | Not lost: its slot is in the default frame, and zooming out shows it. The flight's sound still lands. Accepted, since the zoom is his to undo |

**Caught by the lab** (the feel sheet's T13). The clumsy-hand lab watches every piece of the job every frame.
- **Hidden:** a piece at rest (InSlot, Hovering or OnDevice-removed) that is out of the frustum (zoom ≤ 0.3) or occluded by the device or another piece (the camera ray hits something else first), for even **one frame**. The tool in the hand doesn't count as an occluder: it is the player's own hand. Flights don't count either: they are under 0.6 s and end in view.
- **Lost:** a piece in no valid state.
- Both must be 0 (§5).

### 4.8 The time budget (for the Days 1–3 lead)

**Method.**
- Inputs are listed in order. Each pointing time is **MT = 0.15 s + ID / TP**, with **ID = log2(D/W + 1)**.
- Throughputs: **mouse 3.78 bit/s, analogue stick 1.48 bit/s** (Natapov, Castellucci & MacKenzie, GI 2009, ISO 9241-9).
- **W is the visual width the eye aims at, not the hit area.** A first-timer doesn't know the hit area is generous. On a pad with aim help, W is +120 px (the 60 px snap).
- Distances come from Art's mat layout (A1) at 3.4 px/mm (phone, camera) and 4.0 px/mm (watch), with y foreshortened ×0.85.
- Thinking time: 1.2 s at a new step, 0.4 s between targets of one step, 2.5 s for the first bench screen ever. Holds are the code's own (§4.1–4.3).
- Rubbing is 4 passes per spot (3 plus the finisher) at 0.42 s each, a first-timer's scrub.
- "Steady" is a first-timer who knows the next step from the glow. **p50 = steady × 1.6; p90 = steady × 2.5**: my assumptions, to be replaced by playtest 6's timestamps.

**Grace's camera, Day 1 clean** (lens, film path, film guide; no screws, no cover; mouse):

| # | Input | Seconds |
|---|---|---|
| 1 | First look at the bench (card, glow on the cloth) | 2.50 |
| 2 | Grab the cloth: 576 px, W 255, ID 1.7 | 0.70 |
| 3 | Drag onto the lens: 494 px, W 65, ID 3.1 | 0.97 |
| 4 | Wipe: 3 passes + finisher, 4 × 0.42 | 1.68 |
| 5 | The cloth goes home itself; the brush glows (think) | 1.20 |
| 6 | Grab the brush: 314 px, W 82, ID 2.3 | 0.85 |
| 7 | Drag onto the film path: 494 px, W 51, ID 3.4 | 1.05 |
| 8 | Brush: 4 × 0.42 | 1.68 |
| 9 | Slide onto the film guide: 61 px, ID 1.3, + 0.4 | 0.90 |
| 10 | Brush: 4 × 0.42 | 1.68 |
| 11 | The wake + "Fixed!" (0.3 + 2.1, watched) | 2.40 |
| | **Steady 15.6 s** (pad 17.2 s) · **p50 25 s · p90 39 s** | |

**Grace's camera, Day 2 swap** (mouse):

| # | Input | Seconds |
|---|---|---|
| 1 | First look | 1.50 |
| 2 | Take the tweezers: 511 px, ID 2.9 | 1.01 |
| 3 | Onto the mechanism: 609 px, W 88, ID 3.0, + 0.4 | 1.34 |
| 4 | Hold: pinch 0.25 (it lifts and flies by itself) | 0.25 |
| 5 | See the fresh part glow (think) | 1.20 |
| 6 | Onto it: 627 px, ID 3.0 | 0.95 |
| 7 | One hold: flight 0.40 + press-in 0.35 | 0.75 |
| 8 | The wake + "Fixed!" | 2.40 |
| | **Steady 9.4 s** (pad 9.3 s) · **p50 15 s · p90 24 s** | |

**The phone's cracked screen** (22 inputs). The rows are compressed: one row per verb, the count × the mean per input (full list in the scratch run).

| Verb | Inputs | Seconds |
|---|---|---|
| First look, three flips (R/Y, 0.32 s each), the face-up "Fixed!" | 4 + end | 1.5 + 0.92 + 1.22 + 1.22 + 2.5 = 7.36 |
| Tools: driver, pry, tweezers, driver | 4 | 1.60 + 1.60 + 0.87 + 1.34 = 5.41 |
| 2 screws out: point (W 8 px, a 2.4 mm head) + 0.45 s hold; the second includes 0.4 s to find it | 4 | 1.68 + 0.45 + 1.68 + 0.45 = 4.26 |
| Pry: point the edge + 0.45 s | 2 | 0.69 + 0.45 = 1.14 |
| Screen out: point + 0.25 s | 2 | 0.52 + 0.25 = 0.77 |
| New screen: point + one hold (0.45 flight + 0.35) | 2 | 1.05 + 0.80 = 1.85 |
| Cover home: one press | 1 | 1.28 |
| 2 screws back: point + hold (fetch 0.25 + turn 0.45) | 4 | 1.68 + 0.70 + 1.68 + 0.70 = 4.76 |
| | **Steady ≈ 27 s** (pad 24 s) · **p50 43 s · p90 67 s** | |

**The watch's seized movement** (31 inputs): 4 screws out at 0.45 s, the pry, 2 grime spots by brush, the mainspring swap, the back home, 4 screws back. Tiny visual heads (W 16 px) make each screw's pointing about 1.2–1.5 s. **Steady 37.8 s** (pad 33.5 s, where the 60 px snap helps more than the stick hurts) · **p50 60 s · p90 95 s**.

**What it means for the day.**
- **Day 1's first repair:** p90 39 s hands-on, against the feel target of under 3 minutes for a first-timer. A margin of about 4.5×.
- **A 180 s day** (`DayClock.dayLengthSeconds` in the scene) holds about **two bench jobs** for a first-timer, with walking and talking. Day 2's camera swap plus Walter's watch is p90 24 + 95 s = 119 s hands-on.
- **A 420 s ship day** holds 5–6.
- The Days 1–3 lead's call.

---

## 5. The clumsy-hand lab

**Where.** `Fixit Fidget › Bench › Bench 8 - The clumsy hand (lab, drives itself)`. Menu in the new `Assets/Editor/ForgivingBenchSteps.cs`. Lab `Assets/Scripts/Diagnostics/ClumsyBenchLab.cs` (a `PlayLab`). Hand `Assets/Scripts/Diagnostics/ClumsyHand.cs`: pure, seeded, no Unity calls but `Time`. Bench 6 is UI/UX's words lab; Bench 7 is the stage step (§7, step 14). Report and photos go to `Logs/BenchClumsy/<time>/`.

**The real input path, never a shortcut.**
- **Mouse:** a virtual `Mouse` device is added and made current. The real mouse is disabled for the run (`InputSystem.DisableDevice`, restored in `Restore`) so a nudge can't take over `Mouse.current`. Every frame a `MouseState` (position, delta, buttons) is queued. `GamePointer` reads it as it reads his mouse (`GamePointer.cs:17-55`).
- **Pad:** the `PlayLab` virtual gamepad. Stick, A, RT, LB, RB, Y, B, X, View and D-pad states are queued, and the real `PadCursor` moves the cursor.
- The hand **never** calls `Activate`, `GlideTo`, `Scrub(float)` or any part's method. It knows only what a player sees: the current step's `targets` (the glow), the hover readout, the tools' screen positions, and whether a spot is still drawn.

**The cursor model** (seeded; one seed per run, written in the report):

| Trait | Mouse | Pad |
|---|---|---|
| Reaction before each move | Lognormal, median 0.30 s (σ 0.25). +0.6 s the first time a verb appears in the run | Same |
| Movement | Fitts: MT = 0.15 + ID/3.78 (W = the target's **visual** width). Primary submovement amplitude D × N(1.05, 0.07): overshoot on average; lateral error N(0, 0.04 D). Then a corrective submovement (its own Fitts time) to the target ± N(0, max(3 px, 0.15 W)) | Rate control: ramp to deflection min(1, √(D/600)) in 0.08 s, angular error N(0, 6°); let go when the remaining distance < speed × 0.12 s, with a late release N(0.10, 0.03) s (overshoot ≈ speed × lag), then small corrective pushes (deflection 0.3–0.4) until within 10 px or snapped |
| Speed profile | Minimum-jerk along the submovement (positions sampled from real time each frame, so 15 fps makes real-sized jumps) | Through `PadCursor` (1150 px/s at full deflection, curve 1.6) |
| Jitter | Tremor N(0, 1.2 px) per frame at 60 fps (× √(dt/0.0167)); during a hold, a random-walk drift of 8 px/s | Stick noise ±0.03 |
| Near-miss presses | **20 %** of presses fire at the end of the primary submovement, before the correction | 20 % fire before the snap's glide lands |
| Press length | Clicks lognormal, median 0.11 s (0.06–0.25). Holds last until the hand sees the result + 0.2 s. **10 %** of holds let go early, at 40–80 % of the need, and re-hold 0.3 s later | Same, on A (70 %) or RT (30 %) |
| Releases | The cursor slips 4–12 px in a random direction over the last 2 frames before every release | Stick nudge 0.15 in the last 2 frames |
| Wrong tool | **15 %** of new steps take the neighbouring tool first. The hand notices from the hover (≤ 0.6 s) and takes the right one | 15 %: one LB/RB press too many or too few |
| Tool choice (pad) | — | 70 % LB/RB, 30 % cursor to the tool + A. 25 % of new steps use D-pad right ("next part") |
| Scrubbing | Back and forth over the spot's projected disc: amplitude 1.3 × its diameter, 2.2 Hz, axis random per spot ±15°. Each stroke's lateral offset is U(−0.45, 0.45) R, blind. Half the strokes aim at what is still drawn. Stops 0.25 s after the spot is gone | Hold A/RT, waggle the stick ±0.5 at 2 Hz, plus the in-place rub |
| Flips | Reads the card's flip; presses R / Y | Y |

**What it plays.** The four repairs:
1. Grace's camera, Day 1 clean (fault 0 after the split, §7 step 11);
2. Grace's camera, Day 2 swap (fault 1);
3. the phone's cracked screen;
4. the watch's seized movement.

Each runs **with the mouse and with the pad**, **at 60 fps and at 15 fps**: 16 runs, one seed each. "Full" (a second menu item) runs three seeds each, for a release candidate.
- **60 fps:** `QualitySettings.vSyncCount = 0`, `Application.targetFrameRate = 60`. A run measuring under 40 fps mean is marked "not at 60" but must still pass.
- **15 fps:** target 15 plus a sleep of 0–25 ms per frame, a jittered 12–18 fps, never better than his Mac (rule 8).
- Each run: a fresh lab save, the café empty (the spawner off, as Bench 3 does), the device set on the bench by the lab, the close-up opened with the real E. Timing starts on the close-up's first frame.

**What it reports.**
- **Per step:** the time from the step becoming current to done; presses; **misses** (a press that acted on nothing or on a target not in the step); **retries** (an early-released hold resumed; a wrong tool corrected); the near-miss presses and how far off each landed (px); the strokes per spot.
- **Per run:** total time vs budget; **pieces lost** and **pieces hidden** (§4.7); guard trips; frame time (mean, 95th percentile); cue timing (each action's cue requested on the action's frame, Audio §8); a photo at each step's end.
- **Files:** a timeline `timeline.csv` (t, frame, cursor x, y, buttons, hovered target, event) and `report.txt` with the 16 runs side by side.

**Pass bars** (every run, both frame rates, mouse and pad):

| Bar | Camera clean | Camera swap | Phone screen | Watch seized |
|---|---|---|---|---|
| Finished, Perfect, within | **45 s** | **30 s** | **75 s** | **110 s** |
| Pieces lost / hidden | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |

- Every near-miss press within a target's hit radius acted on that target (100 %).
- No step had more than 3 misses.
- Early-released holds kept their progress: 0 resets.
- Strokes per spot: median ≤ 3, every spot ≤ 6, and ≥ 90 % of spots within 4.
- The 60 fps and 15 fps times differ by ≤ 25 % per step, beyond whole frames.
- 0 guard trips; nothing logged an error.

If the clumsy hand can't finish Day 1's and Day 2's repairs within their bars, the build is not ready (rule 3), whatever else is green.

## 6. Existing labs and checks that change

| Lab or check | What changes |
|---|---|
| **Bench 3** (`BenchLab.cs`) | **Act one (the phone, pad):** the turn, peek, swap and zoom checks stay. The screws now pop, fly to cups 4–5 and land with `screw.drop` on the flight's last frame, in view and never a rigidbody, replacing "falls and comes to rest" (`:382-385`, `:398-399`). The cover flies to the large well after its pop, replacing the drag and drop (`:449-465`). The broken screen flies to the scrap tray, not grabbable, replacing the carry and the magnet (`:471-502`). The fresh screen is done twice: one hold (flight, hover, press-in; `part.fresh` on the press frame, `part.replace` on the seat), and a 3-frame click that leaves it hovering, then a hold. This replaces the pinch-carry-release (`:504-523`). A press on the cover in its well, plus on a fresh job a press that does nothing and a 0.4 s hold that closes it anyway, replaces the drag home (`:538-547`). The holes fetch their screws from their cups. "The catch" becomes "no guard trips". **Act two:** the brush by direct grab (press on it at rest, drag over a spot, let go on the device, still in hand). Strokes per spot ≤ 4; the 90 % finisher; the cloth by LB/RB (the pad path) with smear first, then clean; the cloth home after its smudge, replacing the lift checks (`:803-842`). s rises with cursor speed and the loop's volume follows it (Audio §8). The budge stays ≤ 6 px and is back to 0 within 0.3 s. `BenchWork.Flash` lights the remaining spots, then restores. The raster sweep (`:719-725`) shrinks to 4 passes. **Act three:** Grace's camera **by its verbs**, not the check's hand (`:904`, `:910`): fault 0 (clean) then fault 1 (swap), one "Fixed!" each, the strap kept |
| **Bench 5** (`BenchViewProbe.cs`) | For each device, every quarter turn and both faces: every slot its pieces may use, the tool rest's five tools, its pad and its scrap slots are in frame, outside UI/UX's U1–U5, and not covered by the device (a ray from the camera to each slot's centre hits the slot). It reports the device's projected footprint against the compartments |
| **Stations** (`StationsCheck.cs:500-520`) | Its per-part `Activate()` stays. Each call now waits until the piece has landed (no `IsBusy`, the state InSlot or OnDevice) before the next. Added: no bench piece holds a `Rigidbody` at any point of the run |
| **Aim help** (`AimHelpCheck.cs`) | Snap points come from the picker's eligible targets, replacing the physics-ray grime (`:391-420`). D-pad "next part" reaches an occluded current-step target. Friction: over a current-step target the cursor moves ≤ 0.6× its open speed. `:210`'s `Activate` setup stays |
| **Day 1 guide** (`Day1GuideCheck.cs`) | Day 1's featured job is the clean-only camera (fault 0). Everything else at the bench is UI/UX's |
| **Juice** (`JuiceCheck.cs`) | One ending per repair, face up, unchanged. "New part" no longer pops when the fresh part is presented. `OnFixed` fires once for each of the four repairs |
| **Grace camera tweezers interaction** (`GraceRepairInteractionChecks.cs:85-112`) | Edit mode: `HoldTick` pinches; flights complete at once, so the jammed mechanism is in a scrap slot (Removed). `PresentFresh` puts the fresh part on its pad; `SeatFresh` replaces it. Scrap is **not** grabbable now (`:106-107` flips) |
| **Grace camera content** (`ContinuationIntegrationChecks.cs:66-90`) | With the faults split, "working but dirty → Passable" can't happen in one job. It becomes fault 0 graded by spots (all 3 Perfect, 2 Good, 1 Passable) and fault 1: swap Perfect, untouched Rejected. Narrative's photo outcome must say which job's grade it reads (Flags) |
| **Cleaning reachability** (`DeviceCleaningReachabilityChecks.cs`) | Reachability is "eligible in the picker" from the default view or after one flip, with covers off. Strokes go through the pass-based erase |
| **Console tests** (`Tests/`) | A new `Tests/BenchRules` covers the picker math, the flight curve, the duration rule, the slot rule, pass accounting and the 90 % finisher (pure C#, §7 step 1) |

## 7. The build plan

**Before Unity A.**
- **The TD's one-scale method.** Every rule here is relative, so the mechanism works at today's scales too. Only Bench 5's framing numbers wait on it.
- **Art's pieces.** `BM_RepairMat` with its empties, `BT_ToolRest`, `BT_ScrapTray`, `BT_ClothHeld` with `Press`/`Bunch`, the brush's `Bend` and `BT_ScrewHole.png`. **None blocks the build:** step 14 builds stand-ins with the same empties at Art's coordinates until the FBX lands.

**Files, in build order:**

1. **`Assets/Scripts/BenchRules.cs`** (new, pure static) and **`Tests/BenchRules/`** (new console project):
   - hit shapes and normalised distance;
   - the four priority classes;
   - the flight's position, rotation, duration and easing;
   - the slot rule (footprints at four quarter turns);
   - the pass-based erase coverage (chord / fullChord), smear and the 90 % finisher arming.
2. **`BenchStage.cs`** (rewrite):
   - the compartment fields and slot claims (§4.0.1);
   - the `BenchLayout` read and placement: the rest and the tray move;
   - the `Fly` scheduler (§4.0.2) and the guard.
   - Deleted: the magnet, the tray zone, `Watch`/`Forget`/`SettledInTray`/`TrayDropPoint`/`FreeSpotOnMat`/`InTray`.
   - `Wire(...)` takes the new fields.
3. **`DeviceDefinition.cs`:** `BenchLayout benchLayout` (§4.0.1). **The three prefabs:** Art's values.
4. **`Screw.cs`:** §4.1. Free → flight; no rigidbody or collision; quarter-turn ticks; fetch from the cup; the hole decal; relative thread length.
5. **`RemovablePart.cs`:** §4.2. The auto-flight after the pop; the press or 0.4 s hold home; the grab, drop and physics deleted; `Activate` through the flights.
6. **`ReplaceablePart.cs`** (with `LoosePart`): §4.3. The lift-time flight to the bin; the fresh part on its pad, one-press flight, hover, 0.35 s press-in; the carry, depth and seat gates deleted; scrap fixed; `Activate` through the same; "New part" retired.
7. **`GrimeSpot.cs`:**
   - the relative r with its floor and cap; the pass-based erase; smear first; the 90 % finisher;
   - `Flash` (§4.5); `CoveredBy`; remaining data;
   - the squeak rule replacing `:400`; `cloth.wipe` per frame retired (`:399`; the labs' whole-spot hand at `:518` may keep it, Audio §7.3).
8. **`ToolPickup.cs`:**
   - the direct grab (§4.4.1–4.4.2): exact tip, eased rotation, hovering height;
   - the held cloth swap and its keys, with the mirror at reversals; the brush's bend;
   - auto-home after the last spot; the pad's in-place orbit;
   - deleted: the spring, the eased target, the lift, the pad rescale.
9. **`BenchFeel.cs`** (new): s, the rub loops, the squeak trigger, the budge offset and the rumble (§4.4.3).
10. **`BenchWork.cs`** (new): `Flash`, `WorkLeft`, `Progressed`, `TargetDone`, `Flashed` (§4.5).
11. **`GraceCameraRepairJob.cs`** and **`GraceReunionCamera.prefab`:**
    - **two faults** in `DeviceDefinition.faults`: fault 0, "the lens and film path are dirty", enables the 3 spots with the shutter part off; fault 1, "the shutter's still jammed", enables the shutter part with the spots off;
    - `Quality` gates on the shutter **only when the shutter part is active** (`:10`).
    - **The data:** `Day_01_GraceCamera.asset` keeps `faultIndex: 0` (`:50`). In `Day_02_GraceReturn.asset`, `useFeaturedRepair` goes from 0 to 1 (`:47`) and `featuredRepair` is set to the camera prefab, `faultIndex: 1`, `storyEpisodeId: grace_reunion_camera_v1` (`:48-51`).
    - The episode memory and lines are Narrative's and Days 1–3's (Flags).
12. **`PadInput.cs`:** `Rumble(low, high)`, `StopRumble()` on `PadInput.Pad`. **`GameSettings.cs`:** `Vibration` (PlayerPrefs, default on).
13. **`ItemInspector.cs`:**
    - the picker and priority (§4.6) feeding hover, press and aim help;
    - the grab rule with tap, drag and release (§4.4.1);
    - `HandleBenchPress` routed to the new verbs; the scrub branch (`:535-564`) calling the pass-based scrub with the ray–plane contact;
    - `TableTopPoint` uses `benchLayout.rest`, plus the budge in `Pose`;
    - `faceSnapWithin` set to 45. The scene holds 22 (`AcesCafeLayoutPlaytest.unity:155827`), and a new default doesn't reach a field the scene already holds (Bible §7), so step 14 writes it;
    - the D-pad includes occluded current-step targets; pad friction;
    - `BenchWork` events from hold progress;
    - deleted: `AfterScrub`'s lift (`:999-1004`), `ScrubTravel` (`:990-995`), `scrubPower`.
14. **`Assets/Editor/ForgivingBenchSteps.cs`** (new):
    - **`Bench 7 - The forgiving stage (scene)`**: places the mat FBX (or a stand-in slab with Art's empties), the tool rest and the scrap tray under the `InspectRig`, switches off (not deletes) the old mat, tray and caddy, and wires `BenchStage`;
    - moves the bench's three slots **off the mat** (the bench top past the mat's right edge, then behind the scrap tray; the TD and Environment confirm), so a waiting device never covers a compartment;
    - records what it did, has an Undo, photographs before and after, and saves the scene;
    - and **`Bench 8 - The clumsy hand`** (§5).
15. **`RepairChecklist.cs`:** `Step.targets`, `left`, `total` (§4.5). UI/UX adds `verb` and `icon` in Unity B, so nothing is rebuilt twice.
16. **The labs:** `ClumsyHand.cs`, `ClumsyBenchLab.cs` (new), then §6's changes in that table's order.
17. **`SoundBank.Defaults()`:** Audio's new entries (§7.6 of their spec), so the hooks above have bank cues.

**No save field changes.** Bench pieces and mid-repair state are not saved; the day saves at Open Tomorrow, and jobs end with the day. `BenchLayout` lives on prefabs.

**Sessions.** About **3 Unity sessions** (±1), one at a time on the Mac:
- **A1:** steps 1–6 and 14, then Bench 3's acts one and three.
- **A2:** steps 7–13 and 15, then Bench 3's act two, and Aim help.
- **A3:** step 16 and the remaining §6 rows; the first Mac runs at 15 fps; tuning only of numbers this spec names.

Add one session if the TD's rescale lands in the same Unity A.

## 8. What can break, and how we'd know

| Risk | How we'd know | Answer |
|---|---|---|
| The phone's quarter-turned footprint covers the left cup column (x 27–45 in Art's layout) | Bench 5's slot coverage line | The slot rule already keeps its screws in cups 4–6. If Art moves things, the rule recomputes |
| The TD's framing pushes a compartment off-frame or under the card (U3) | Bench 5 / Bench 6 name the rect | Fix the framing (UI/UX §7 says the same) |
| Generous hit areas swallow the rotate gesture's "empty space" | Bench 3's drag-to-turn line; his playtest | Turning is also the stick, and the card teaches R/Y. Shrink the 12–16 px margins first, never the screws' 28 |
| Two targets' circles overlap (the watch's screws at 84 px apart vs 56 px circles: clear; a future device may not be) | `BenchRules` test on each prefab: the minimum spacing between same-step targets ≥ 1.1 × the summed radii, else a warning | The priority's normalised distance resolves overlaps anyway |
| The cloth's skinned shape keys cost too much on the Mac | The clumsy hand's frame time at 15 fps | Art's fallback: three meshes swapped (A4) |
| The virtual mouse fights the real one (Mac) | The lab's first line: is `Mouse.current` the virtual one each frame | The real mouse is disabled for the run, restored in `Restore`, which always runs |
| `targetFrameRate` is ignored in the editor | The report's measured fps | The 15 fps sleep-jitter caps it regardless |
| The covered-by geometry misses a spot (the watch's grime) | A covered spot hittable while the back is on: the reachability check | Add it to the cover's `faultsBeneath` (data) |
| Day 1's camera "wakes" by firing a shutter the story says is jammed | Bench 3 act three's cue counts | Narrative and Audio decide; flagged |
| An unverified line number moved on the Mac (`fab65d3`) | The build session reads before it edits | Every claim cites the clone at `431f081` |

## 9. Against the other four specs

| Spec | Agree | Disagree / note |
|---|---|---|
| **UI/UX** | `Step.targets` (built here), `Flash(job, 1.5)` (§4.5), hover priority from the step (§4.6), a progress event, the bindings (unchanged except the Tab/View tap), the stage contract U1–U6 (Bench 5 checks the compartments against it) | **Their "one hold flies it over and presses it in" is kept**, and his click works too (§2). Their glow is "the fresh part, in its spot or over its seat": both states exist. Their "Cover on" glow is the cover in the well: that is my primary target |
| **Art** | The empties as a naming contract, per-device data (`BenchLayout`), the steel scrap tray, the tools lying on a rest, the cloth's three states, `Bend`, the hole decal, the clean mechanism | **Their footprint rule is for the square-on pose only.** A quarter-turned phone covers cups 1–3. My slot rule handles it. Art may want the phone's rest 10 mm further left so a mouse spin can't brush the left column. **One `Bunch` key suffices** (mirrored at reversals, §4.4.2) |
| **Audio** | Every hook exactly: s, the loops, the squeak, cues on the flight's first and last frames, collision sounds and `screw.bounce` retired, quarter-turn ticks | `cloth.lift` is retired with the lift (the auto-home plays `tool.down`). `part.fresh` is "a click" in their table: it fires on any press's first frame |
| **Cinematics** | The bench shots parked until the mat and one scale land. Their `bench.screw` ended on the tray; it would end on a cup now | — |
| **NPC** | `ItemInspector.FocusedItem` and `StationInteractable.BenchHolds`, which their owner-watches-the-repair reads, are unchanged | — |

## 10. Open questions (his)

1. **Cloth and brush on release.** Let go on the device and the tool stays in hand; it goes home by itself when its work is done (my pick). Or it goes home on every release, decision 4 as written.
2. **Day 1's camera wake.** With the shutter still jammed, should it only wind (no fire)? That is the story's call; one line in `DeviceWake` either way.

## 11. Flags

- **The Bible's pillar line** (§1: "The screw comes out of the hole you clicked and falls") and my role file's mission ("…and falls") now contradict his approved auto-tray: it flies home. They need his wording change; the Producer edits.
- **`bench-spec-v2.md`** §2.1–2.3 and §7 ("the magnetic tray and the catch", "parts lie where you drop them") are superseded by his decisions 1–2. The Producer should mark them.
- **No new mechanic,** in my reading: every verb exists, and these are his approved changes 1, 2, 4 and 5. **Brushing the line, his to strike:**
  - the 0.4 s hold to close over unfinished work (a detail of "close it anyway", which exists);
  - the tools going home by themselves after their work;
  - the stock put away when the pads are full;
  - the Vibration settings row (a setting, not a verb).
- **Locks:** the clock runs (flights use game time and never pause it); devices are dumb; "objects are objects": the cups' printed 1–6 are the mat's texture, not game state; asset names untouched; no save field touched.
- **Rule 7 holds fully:** no rigidbody remains at the bench.
- **Narrative and Days 1–3 must decide:**
  - which job's grade drives Grace's photo outcome once the camera is two jobs (`GraceCameraEpisode.PhotoOutcome`, `graceCameraGrade`);
  - whether Day 2's camera arrives clean (my data assumes yes: fault 1 has no spots).
- **Found on the way, not fixed:** `ScrewSocket`'s hover box is the head's width × 2.2 (`Screw.cs:364-366`). After one scale that is a 5 mm target: the picker makes it moot.
- **Paths:** `~` is `/root`. The spec is at `/root/cafe/specs/systems-forgiving-bench.md`; the scratch simulations are in my session's scratchpad (not the repo).

## Sources

- Mouse 3.78 bit/s and analogue stick 1.48 bit/s, ISO 9241-9 throughput: Natapov, Castellucci & MacKenzie, "ISO 9241-9 Evaluation of Video Game Controllers", Graphics Interface 2009, [yorku.ca/mack/gi2009.html](https://www.yorku.ca/mack/gi2009.html).
- ReStory, PowerWash Simulator, Unpacking and Assemble with Care: as cited in the common brief §5.

## Against the feel sheet (`/root/cafe/feel/feel-sheet-2026-10-14.md`: landed 07:36, revised 07:46; both read before finishing)

Its five-line answer is this spec's premise: ReStory **looks physical and behaves scripted**. Screws reach the dish with no carry, drags are rigid and flat, and nothing was loose in 106 s. Its §7 says the 7 Oct reading (physics, depth) was wrong. This spec removes both.

| Its target | This spec | Verdict |
|---|---|---|
| **T1** (revised 07:46) Screw: first touch to resting in its cup ≤ 1.3 s; a flight ≤ 0.35 s; a flight never blocks the next input; 0 lost | Hold 0.45 s (the phone's and watch's `turnDuration`) + pop 0.06 s + a screw flight of 0.28–0.33 s for Art's cup distances = **≈ 0.82 s**. Never above 0.35 s: screws have their own faster duration, added after reading the revision. Input is never blocked (§4.0.2); 0 lost by construction | Met. Even the script's 0.9 s base hold, which no device uses, gives 0.9 + 0.06 + 0.35 = 1.31 s, at the line |
| **T2** Part release to resting: flight ≤ 0.6 s, never below the mat | Flights 0.30–0.55 s, on an arc above the mat. Before the flight: the cover's 0.15 s beat + 0.10 s lift; the broken part's 0.25 s wobbling lift | Met for the flight. The beat and lift are the visible "it popped / it's out" moments, ≤ 0.25 s; trimmed from 0.32 s after reading it |
| **T3** Fresh part: one click + one hold; any click in its silhouette counts; seated ≤ 0.3 s after the hold | A click flies it (hull + 14 px counts); the hold presses it in; the seat **is** the hold's end (0 s) | Met, and one hold alone also works (UI/UX's ask) |
| **T4** Input to first motion or sound ≤ 1 frame | The tool is under the cursor on the press frame; every cue on its action's frame (§4.0.2, §4.4.3) | Met |
| **T5** Hover same frame; hit area ≥ 2× the visible head | Picker every frame; 28 px radius = 56 px across, against 8–14 px heads (4–7×) | Met |
| **T6** A spot in 2–4 strokes, ≤ 3 s; footprint ≥ 40 % of the spot's diameter | Median 3, 90th percentile 4; ≈ 1.7 s at a first-timer's scrub; footprint 75 % (brush) and 100 % (cloth) of the spot's diameter | Met. Note: ReStory's 45 % ring cleans **whole parts**; our spots are smaller patches, so ours is wider relative to the dirt, as it should be |
| **T8** Screw head ≥ 12 px at the default framing | My budget assumed 8 px (2.4 mm phone heads, Art A8), which is pessimistic. The sheet suggests 3–3.5 mm heads | **Agree with the sheet: the TD's call.** The picker doesn't depend on it; bigger heads only make Fitts faster (§4.8) |
| **T9** Device ≥ 35 % of frame height; the watch at a 1.5× default | Hit areas and flights are in screen px or relative, so they hold at any framing | Agree; the TD's and Art's call |
| **T10** Day 1's first repair: practised ≤ 45 s, first-timer ≤ 90 s, clumsy hand ≤ 120 s | Steady 15.6 s, p50 25 s, p90 39 s; **clumsy-hand bar 45 s** | **I keep a tighter lab bar (45 s).** The arithmetic says a hand that needs more than about 2× steady is meeting friction, not distance, and the lab's job is to catch friction. If the Producer adopts 120 s as the feel target, it still contains 45 |
| **T11** First repair ≤ 30 % of Day 1 (≤ 54 s at 180 s) | p90 39 s = 22 % | Met |
| **T12** "Fixed!" ≤ 1.0 s after the last seat | Unchanged, 0.3 s | Met |
| **T13** 0 frames with a removed piece hidden under or behind the device | Adopted as the lab's rule (§4.7): **one frame** at rest is a failure. The tool in hand and flights are excluded | Adopted; I had allowed 0.5 s |

**Two things it saw that this spec doesn't copy.**
1. **ReStory's parts are dragged by hand, with no tool switch** (its §5.6). Ours keep the pry and the tweezers. They are this game's existing verbs, and he praised the tool feel ("the screwdriver felt the best"). Taking them away would be a design change beyond his list. The clumsy hand measures their cost: 0.35–1.6 s per tool pick (§4.8).
2. **ReStory's screws jump to the dish in one frame** (measured at 60 fps in the revision: in the hole at 2:55.833, in the dish at 2:55.867). Ours fly for 0.28–0.35 s. That costs no time (input is never blocked), and a visible flight is his "nothing removed can end up out of sight" made literal. At 15 fps it is still 4–5 frames of arc rather than a pop. If he prefers ReStory's snap, the screw's duration floor is one number.

**One thing it saw that this spec adopts now:** the pale **lanes** and the corner **dish** both say "every removed piece has a place you can see". That is the slot rule (§4.0.1).

**Not mine, passed on:** its pink-red outline on a device while a part is carried over it (meaning unverified) has no equivalent here, because nothing is carried.
