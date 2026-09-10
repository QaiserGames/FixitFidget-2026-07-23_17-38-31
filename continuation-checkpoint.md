# Fix It Fiasco — recovered work and playtest checkpoint

Prepared September 10, 2026. Review branch: `codex/device-scaffold`.

## Where the project stands

The shop loop is playable and has already supported your five-day test. The current milestone remains **M1: Grace's customer showcase**. Its finish line is a player remembering Grace, understanding why the camera matters, and recognizing a consequence when she returns. This update supplies more of that playable prototype and the beverage design you asked for. It does not certify a polished showcase or a finished game.

The interrupted conversation did publish the earlier batch: commit `de1c45be69b890a4e6118fd128ace07273006903` contains compact tickets, patron spacing/recovery fixes, clarified logs, and featured repair requests. Your latest local checkout already contained that commit. Those changes were not lost; the completion message was missing.

## What this update changes

### 1. Grace's camera and the photograph she brings back

- Adds a deliberately authored reunion-camera request. It explains the family reunion, three cleaning tasks across the lens/film path, a jammed shutter, and the request to preserve the scratched strap.
- Provides an Editor tool that creates an actual camera-shaped prototype prefab using existing cleaning and tweezers interactions. The strap remains visible and is not a replacement task. This is geometric prototype art; final Blender work is still needed.
- A broken shutter produces a Rejected result even if you clean everything. Fixing only the shutter gives Passable; fixing it and most cleaning gives Good; all tasks gives Perfect. This prevents a pristine but unusable camera from producing a successful reunion photograph.
- Stores the outcome of this exact episode separately from a customer's most recent generic repair. A perfect watch repair cannot invent a reunion photo. A later missed drink cannot erase an already returned camera's grade.
- On a later day's accepted return, Perfect/Good yields a clear photograph, Passable yields a visibly smudged print and an honest joke, and Rejected/lost yields a missed-moment callback without a photograph.
- The photo is a physical shop keepsake, with a simple symbolic family image and Grace in the middle. The state persists through the existing day-end checkpoint. Merely opening/reopening intake does not grant it, and accepting again cannot duplicate it.
- Existing focus-boundary memory stays intact. Asking for quiet still affects her return.
- Adds `story_episode`, `camera_grade`, and `photo_variant` to the customer CSV, alongside the previously added storyteller/focus fields.
- Save format advances to version 4. Older saves keep progress and do not receive invented camera history. Returning to older code after saving in version 4 requires your earlier backup.

The setup tool uses **separate copies of your currently authored Day 1/Day 2 schedule assets**. It preserves their timing and lessons. The GDD's eventual Day 2/Day 3 schedule is still a deliberate later decision. No automatic campaign reset is included. The first camera job has a provisional $40 base payout; economy tuning remains open. Pride rewards and a fully authored retry visit remain future work.

### 2. Customer identity on compact tickets

- Each ticket gains a small portrait, name, and mood cue. It reads the existing expression slots, falls back to the neutral portrait, and uses an initial when no artwork is assigned. No final portraits were generated in this pass.
- Keeps patience visible and support-call countdowns explicit.
- Gives secondary drinks their own row so a long repair description cannot hide an outstanding drink.
- Keeps the 118-unit card height and existing responsive rail sizing. Five or six cards can still fit one row at the earlier 1920-wide canvas configuration; narrower canvases may wrap.
- Full task/constraint text is available in a temporary hover panel when the pointer is available. The expanded panel closes on pointer exit.

This implements the direction of keeping the rail useful for urgency while making it a row of people. A journal is **not implemented**. The next journal decision should follow an actual recognition test; it should add customer history and preferences rather than become another mandatory screen during a rush. Do not remove important repair constraints from tickets until their alternative presentation is proven.

### 3. Six independent drink sections

- Adds Coffee, Tea, Hot Chocolate, Espresso, Americano, and Latte sections through an explicit scene setup tool.
- Each section is a separate object with its own cup position and pour state. No order is required and the cup is not assigned to a customer in advance.
- Take a cup, place it under a section, then press that section's dispense button. A no-cup press spends the ingredients and visibly pours without creating a free cup. Its prompt states the waste before you act.
- Each section runs independently, so coffee and tea can fill at the same time. An active pour cannot be charged twice. A filled cup occupies its section until lifted.
- A prebrewed servable drink still satisfies intake availability after unspent stock runs out; the shop should not claim a shortage while that drink is ready.
- Freshness starts when filling finishes. Each new-station drink has its own world-space circular meter: Fresh, Cooling, Cold. Current starting values are **30 seconds fresh, 60 seconds until cold**, editable per drink. These are test values, not balanced release timings.
- Cooling drinks remain servable with half the otherwise-calculated tip. Base drink price is unchanged. Cold drinks cannot be served; the prompt says they have gone cold.
- Discarding is deliberate. A filled discarded cup and its ingredients are lost. An unused empty cup may be returned to stock. There is no automatic deletion that clears clogged sections for you.
- The new station enables two carried items total, including devices. The latest pickup is selected; **C** switches the selected item. Serving, placing, and discarding act on that item. A small readout identifies it.
- Enter the dispenser view once with **F**. Point at a supply, pad, or button and press **E**; prepare several drinks, then **F** to leave. Taking cups at this station keeps you in its view. This is one entry and exit per visit.
- Day-one guidance recognizes the dispenser workflow and cold cups.

**Stock scope:** cups and the existing shared ingredient pool are used. The existing UI still calls the latter “Beans.” Per-drink ingredient costs already determine consumption. This pass does not add six independent inventories or new purchasing screens. Choosing a shared beans/milk/dry-goods model, changing labels consistently, and adjusting restock presentation remain explicit follow-up design work.

**Presentation scope:** the dispenser wall, controls, pour stream, and camera are functional prototypes generated in Unity. Counter placement, cup spacing, readability, and camera framing require live review. The current espresso artwork remains available; the installer disables its old order-bound brewing component in the scene.

## Verification completed

- All runtime and Editor C# sources compile against the installed Unity 6000.5.2f1 and real package assemblies. There are existing obsolete-API warnings; no compilation errors in the validated source.
- All five standalone suites pass: circuit, support calls, Human repairs, storyteller/memory, and the new continuation rules.
- The new suite includes 66 assertions for freshness boundaries, independent cup timers, no same-day or duplicate photo grant, exact episode identity, grade outcomes, memory copies and checkpoint restoration. An additional JSON check verifies older-save migration preserves progress without inventing an episode.

**Not yet verified:** Unity asset import, Editor integration-check execution, Play Mode, rendered UI, camera framing, navigation, controller behavior, or a player build. C# compilation and rules passing are not a substitute for these. No fresh-player recognition test has occurred.

## How to set up the new content

1. Use the review branch and let Unity finish importing. Start with a clean Console; record any new red error before testing gameplay.
2. Preserve your current save before a two-day story test. A copied scene still uses the same save location. This update does not delete or reset it. If your current save is past the featured days, use a backed-up test save or a separate test profile; do not reset your only campaign save just to test this update.
3. Open a **copy** of your shop scene for the initial setup. Run **Fixit Fidget > Content > Grace showcase > Create prototype assets**.
4. Select the transform where you want the photograph mounted, or let the tool use a counter DropSpot. Run **Fixit Fidget > Content > Grace showcase > Install in open scene**. This switches that scene to separate Day 1/Day 2 copies and places the photo mount. Check its position.
5. Run **Fixit Fidget > Content > Install six-drink dispenser in open scene**. Select/move the generated root to fit the counter and inspect the stand point and camera. The scene change is undoable. Save the test scene when satisfied with placement.
6. Outside Play Mode, run these checks under **Fixit Fidget > Checks**: **Grace episode and drink freshness rules**, **Dispenser stock and two-hand transfers**, **Grace camera content**, **Compact portrait tickets**, plus the earlier **Featured repair requests**, **Waiting space reservations**, and storyteller checks.

The new Editor checks are provided and compiled; they have not been executed here. If one reports failure, keep its exact message. Do not assume the old expected assertion counts apply to every newly extended suite.

## First playtest: short and focused

Do this before committing to another five-day balance run. For nonpersistent experiments, stop Play Mode before the day ends; for the actual return-memory test, use the backed-up test save and allow the checkpoint.

| Test | Expected result | Record if it fails |
| --- | --- | --- |
| Enter dispenser, make coffee and tea together | One station entry, independent pours, both cups collectible | Which control/camera transition failed |
| Dispense without a cup | One ingredient charge, visible wasted pour, no cup appears | Stock before/after and section |
| Press again during a pour | No second charge or second drink | Stock and number of presses |
| Leave filled cup under nozzle | Cannot start another drink there until it is lifted | Whether the existing cup was overwritten |
| Take two cups; try a third; press C; place one | Two-item limit; selected item moves; other stays carried | Item order and selected readout |
| Carry a drink and a repair | Both count toward the same limit; each can be delivered | Any wrong handoff or lost item |
| Let one cup cool while another is fresh | Independent ring states; cooling cup serves with reduced tip | Cup ages, tip, display state |
| Let a cup become cold | Delivery blocked with clear prompt; manual discard loses cup | Whether customer was charged or cup vanished automatically |
| Pause, use a station, leave it, close the day | No extra spending, stuck cursor, stranded locked cup, or unwanted input | Exact sequence |
| Five/six customers including a support call and extra drink | Floor remains visible; portrait/name/patience/call/drink readable | Screenshot at your normal resolution |
| Hover a long ticket, then leave it | Full request appears temporarily and disappears | Any clipping or stuck panel |
| People pass occupied chairs | No repeat of the prior body overlap/stalling problem | Both people and nearby waiting spots |

## Grace's two-visit test

1. In the prepared test schedule, meet Grace and confirm she requests the **camera**, the reunion is mentioned, and the strap constraint is readable at intake and in the repair detail.
2. Let her speak at least once, then request **Let me focus**. Continue repairing and confirm her chatter stops without a new punishment.
3. On the first run, fix the shutter and clean all grime. Return the camera, finish any requested drink, and close the day. Check the CSV camera grade and the existing checkpoint.
4. On her next featured visit, hear the reunion callback and remembered quiet. Accept: the clear photograph should appear once at its mount, with Grace in the frame.
5. Close the day, reload, and confirm the same photo remains. Reopening dialogue must not create another reward.
6. In separate backed-up test runs, try a working but dirty camera, a broken shutter, and a camera never returned. Expect the imperfect print or missed moment as specified above. A generic repair from an old save must not award a photo.

## What to work on next, in order

1. **Fix live regressions from the focused test.** Prioritize wrong handoffs, stock duplication, locked cups, save issues, obscured tickets, and camera/control problems. Add targeted regression coverage for actual failures.
2. **Review the feel of the beverage loop.** Decide whether six timers create satisfying planning or excessive monitoring, whether 30/60 seconds is fair, and whether the station camera/point-and-E interaction feels natural. Then decide the ingredient model and restock labels. Do not tune arrivals and freshness simultaneously.
3. **Finish Grace's presentation and story delivery.** Replace camera/strap/photo placeholders with approved artwork, provide readable portrait expressions, frame the keepsake, and check dialogue length and delivery under real shop pressure. Decide the final Day 2/Day 3 schedule, Pride reward, and explicit retry hook.
4. **Test recognition with someone new.** After the two-visit sequence, ask who Grace was, why her object mattered, what you asked her to do, and what changed on her return. If they recall only tasks, improve staging and quiet moments before adding more cast members.
5. **Design the journal from that result.** Keep urgency on the rail. A journal can hold current visitors, discovered preferences, brief biographies, previous outcomes, and keepsakes. Set the pause policy and when information unlocks before implementing it.
6. **Run five comparable complete days after the new loop stabilizes.** Record satisfaction, pending drink losses, stock waste, repair time, customer recognition, and subjective workload. Existing logs do not yet count every wasted pour or freshness state at service; add that telemetry before using logs alone to balance the dispenser.
7. **Complete M1, then advance the next gate.** Prove player/customer animation for the boundary encounter before implementing ejection. Expand into a cohesive vertical slice afterward. Additional repair verbs and device families remain backlog items; the fuse gameplay stays as currently designed.

The immediate next session should be the setup and focused test above. Success means a concrete list of observed problems or a clean route into Grace's recognition test—not another unrelated feature batch.
