# Current position and identity assessment — September 22, 2026

> **September 23 update.** Both open playtest reports below now have a measured cause and a fix; only the owner's feel and live Perfect replays remain. First-person stutter: instant movement plus a 1 mm Min Move Distance that, at ~240 fps, swallowed movement after every stop. Grace's camera capped at Good: the lens glass's capsule collider enclosed the lens grime, so the brush could never reach it. Details, numbers and next steps are in the September 23 entry of Current-Handoff-2026-09-12.md.

This dated assessment supersedes older next-step instructions below. It completes the interrupted September 21 review. It does not approve a new appearance, authorize implementation, or replace the GDD with another roadmap.

**Subsequent backup completed:** after this assessment, the owner explicitly authorized a full checkpoint and push on codex/grace-showcase-baseline. **7f9fc8e** was pushed and verified on GitHub, including the two previously local commits plus saved scene/prefab/material/log changes and direction documents in Docs/ProjectDirection. Main remains **7f2dbe7**. Only Unity recovery files remain untracked; they are preserved in the verified local safety snapshot. This follow-up changes version history/documentation only, not gameplay behavior; backup is not quality sign-off. This completion record accompanies a subsequent documentation-only commit.

**Finding:** Fix It Fiasco already has a specific identity in its design and authored story. Its presentation does not consistently make that identity visible. The useful next investment is one recognizable person and their meaningful service encounter, carried through a consistent visual language. More scenery or more systems would not address the largest gap found in this review.

## Where the project actually stands

| Area | Current evidence | Honest status |
|---|---|---|
| Service foundation | Repairs, drinks, carrying, queue/seating, pressure, daily recap and checkpoint code exist; prior owner runs completed multiple days. Latest Day01 records three people served and four completed orders. | Substantial playable prototype. Whole-loop shipping reliability is not certified. |
| Grace customer showcase / GDD M1 | Authored reunion camera, strap requirement, latte, story lines, remembered outcomes and returned-photo logic exist. Local fixes and hints are checkpointed through e334eb2. | Implemented prototype; open repair complaint and incomplete continuous return/reload verification. M1 remains open. |
| Character presentation | Live asset inspection found one authored regular profile, Grace, with all five portrait slots unassigned. Runtime screenshots show repeated placeholder customers. | Emotional identity is stronger in writing/data than on screen. |
| Visual Style Lock 01 | First-character production guide exists. Owner's girlfriend's illustration is authoritative; owner wants to learn by making the model himself. | Guide stage. No portrait-derived finished model or accepted world-wide style lock established. |
| Public-facing slice | Existing room and systems provide material for it. No newly measured fresh-player session, standalone frame pacing, full audio review or integrated final-art encounter. | Not yet demonstrated as a polished public slice. |

There are two naming layers in the notes: GDD milestones M0–M4, and the recent foundation/style/visit/return/presentation work order. They describe different scales. The current work belongs around **M1, making one customer memorable**, with visual preproduction proceeding alongside documented reliability issues. The art-direction correction did not erase gameplay progress or complete M1. Later GDD animation/boundary/campaign work remains future scope.

## The identity to protect

The GDD's hierarchy is **people first, place second, service actions third**. The intended experience is a welcoming shop, overlapping needs that create funny pressure, and a returning person who makes the earlier effort matter. Repairs give the relationships a physical object; drinks create competing obligations and small acts of care.

Grace already demonstrates this: a reunion camera matters because of a family event; the scratched strap matters because of her husband; a later photograph carries the consequence. Preserve imperfect outcomes as part of that story. The implementation currently describes a family-reunion photograph with Grace, not a newly approved Ace-and-Grace composition.

The strongest visual anchor is the owner's chosen **illustration-led stylized 3D** direction: preserve the artist's silhouettes, exaggeration, proportions, clothing, hair masses and personality, with clean dimensional materials. The Meshy experiment is translation reference only. The retrieved conversation confirms the owner's preference but does not expose the actual attached images; likeness, exact proportions and unseen views still require those references.

The previous silver-hair/plum-cardigan Grace proposal is superseded. Labeling it a proposal did not make it the correct continuation of the owner's character-design work. Future art direction should help the owner and artist express their designs, with explicit decisions kept separate from suggestions.

## Presentation diagnosis from the actual project

1. **Recognition is the largest missing link.** Grace has authored lines and memory, but no assigned portrait expressions. Repeated placeholder bodies weaken the sense of a neighborhood of particular people. A distinctive face, matching silhouette and a small readable performance would reveal existing design value without adding a relationship system.
2. **The default composition emphasizes the setting more than the premise.** Both the latest stopped-scene capture and earlier runtime overview devote substantial space to roads, facades and open floor. The repair surface and the customer's possession read small. The oak, sage joinery, warm palette and neighborhood are useful assets; a later framing/light/prop-hierarchy study should make a person, repair object and café service legible together. No room rebuild is proposed here.
3. **The assets use different levels of simplification.** Earlier close-up gameplay captures combine angular placeholder people, photographic wood grain, decorative rug detail, simple foliage and more detailed joinery. The character reference should determine the simplification level for a representative bench, cup, furniture item and foliage study. More geometric detail by itself is not the quality target.
4. **The interface needs one visual hierarchy.** Existing Canvas UI supports dialogue, portraits, tickets and repair states. Captures show plain white status text over scenery, dark dialogue/repair panels and paper-like tickets. Preserve their useful information and later agree one typography, contrast, spacing and status treatment. Ordinary screenshots should make person → request → current action → consequence easy to follow.
5. **Tactile completion and performance remain uneven.** Cup filling and screw/part motion offer useful physical feedback. Grime shrinks, then disappears with a placeholder completion log; replacement swaps its visual and still carries an audio TODO. Walking/interaction animation hooks exist, but do not establish expressive storytelling or convincing seated performance. Audio quality and in-game audibility were not listened to in this audit.

These findings support a focused presentation pass, not a claim that every mechanic is already polished. A recognizable ten-second capture should eventually communicate a particular customer, their unusual repair request, a satisfying action and a small competing demand. The later return supplies the emotional payoff across the slice. This is a review criterion, not a trailer produced or a new content mandate.

## Open playtest record — deferred at the owner's request

| Report | Evidence and uncertainty | Later completion criterion |
|---|---|---|
| Grace's camera repair still only earns Good | September 21 Day01 CSV records the exact reunion-camera episode as Good; summary has zero Perfect. Current authored tasks are three grime spots and shutter replacement. One remaining grime spot is consistent with the grade, but obstruction, residual grime, brush behavior or other input/state issues are not diagnosed. Existing GraceRepairInteractionChecks removes grime directly before its Perfect assertion. | Reproduce using normal brush/tool input; identify the specific unfinished task and why it remains; make the result clear; demonstrate Perfect and an intentional partial result. No grading shortcut. |
| First-person stutter during square movement and reversals | Owner reproduced up → left → down → right → up and back-and-forth movement. Source moves the controller in Update and follows player position in LateUpdate. No frame-time capture or reproduction this turn. The earlier station-look sensitivity work concerns a different path. | Compare unobstructed and near-furniture movement, camera ownership/update timing, and measured frame pacing. Choose a fix after distinguishing directional feel from collision corrections or actual hitches. Confirm smoothness without sluggish input. |

Both are real open player-experience reports. Neither is being dismissed because an automated check passed. Neither was fixed or newly playtested in this assessment. They remain visible while the owner works on creative direction.

## The bounded continuation

**Immediate creative step:** resume the existing first-character guide with the actual illustration and Meshy reference available for comparison. Identify a few silhouette/proportion relationships and coach the owner through a simple Blender blockout. The owner models; the assistant reviews and explains. Do not generate a substitute character or invent Grace's appearance.

After that character language is accepted, the previously discussed representative bench, cup, one furniture object and foliage can test whether objects belong to the same world. They are individual future tasks, not permission to produce the set now. An expressive Grace visit and a complete saved return then provide the quality proof before wider cast/environment expansion. The two deferred reliability reports must be resolved before public-slice sign-off, but are not homework blocking this assessment or reference discussion.

For each task, state what changes, why, which existing area is in scope and where it ends. Keep creative choices with the owner, preserve the artist's authority over established designs, and treat casual continuation as continuation of the named task.

## Evidence, limits and workspace state

Reviewed: GDD v4.1 extraction; current handoff and guide; text of **Fix It Fiasco Feedback**; actual source/prefab/profile data; earlier first-person and runtime overview captures; latest Day01 logs; live scene and portrait inventory. On September 22 Unity was stopped and unpaused in `Assets/Playtests/AcesCafeLayoutPlaytest.unity`, with a clean in-memory scene. The saved scene still differs from Git; these are distinct states. The temporary initial connection failure cleared as Unity finished starting.

Current branch is `codex/grace-showcase-baseline`, local HEAD `e334eb2`. Pre-existing changed scenes, PhoneRepair prefab, day logs and deleted duplicate materials were preserved. No game files, saves, commits or branches changed. This assessment changes only the two existing workspace notes and adds an evidence screenshot. No tests, fresh Play Mode journey, performance benchmark or listening test were run.

Key anchors: [Grace profile](D:/FixitFidget/Assets/Data/Regulars/Regular_Grace.asset:20), [latest recorded Grace result](D:/FixitFidget/DayLogs/AcesCafeLayout/Day01_customers.csv:3), [repair checker limitation](D:/FixitFidget/Assets/Editor/GraceRepairInteractionChecks.cs:59), [cleaning response](D:/FixitFidget/Assets/Scripts/GrimeSpot.cs:16), [replacement response](D:/FixitFidget/Assets/Scripts/ReplaceablePart.cs:30), [portrait fallback](D:/FixitFidget/Assets/Scripts/ConversationUI.cs:66), [first-character guide](</C:/Users/Mansoor Qaiser/GDD fix it fiasco/Visual-Style-Lock-01-Character-Guide.md>), [September 22 editor camera capture](</C:/Users/Mansoor Qaiser/GDD fix it fiasco/validation/identity-audit-2026-09-22-editor.png>), [earlier first-person gameplay capture](D:/FixitFidget/Assets/Screenshots/aces-interior-first-person-review.png).

The latest image is an editor camera render with no runtime customers or overlay HUD. Earlier gameplay captures provide runtime visual context, not evidence of a new playtest. The older repository roadmap and historical sections below retain their dates and may describe superseded states.

---

# Grace showcase baseline — September 19, 2026

**Latest owner direction:** proceed without requiring an owner playtest. Creative preproduction has moved forward to the reviewable [Grace Counter Corner Style Lock](</C:/Users/Mansoor Qaiser/GDD fix it fiasco/Grace-Counter-Corner-Style-Lock.md>) and its concept board. Remaining gameplay uncertainties below still apply; they are not a reason to block the proposal or start another broad technical pass. The owner's approval-before-substantial-visual-implementation boundary remains in force. No Unity changes were made for the proposal.

**Status: bounded fixes applied and committed; the complete normal-input journey is not yet verified. Phase 2 visual implementation has not begun.** This section supersedes the older audit's statements that inspection collection is absent and the Grace blade checker fails.

## September 19 continuation — missing Day 1 and Day 2 hints restored

**Confirmed regression:** the guide required `IsGuidedOpening` and explicitly excluded any day other than Day 1. Disabling the showcase's old opening sequence therefore also hid its guidance. Day 2 could never pass the existing UI gate.

The six-file local checkpoint **e334eb2** separates optional featured-visit guidance from the old opening sequence. Only the two existing Grace showcase day assets opt in. The spawner exposes its featured visitor without changing arrival rules. Guidance follows the repair and outstanding drink independently, recognizes a matching drink in either hand, and resets each morning. Completed inspection now teaches E collection. Hints retain their six-second duration and conversation/recap suppression. No new runtime script or art asset was added; no save or gameplay rule changed in this continuation.

**Verification:** Unity compilation passed. DayOneOnboardingChecks passed timing, day isolation, default opt-out, the two showcase asset settings, pending drink after repair return, and E collection wording. In the running café scene, Day 1's panel was active with its Grace intake hint. A deliberately selected Day 2 runtime fixture displayed its return hint; its screenshot was inspected and text did not overflow. This fixture is not proof of a saved Day 1-to-Day 2 return. Screenshot: `validation/grace-day2-hint.png`.

**Owner playtest evidence:** September 19 Day01 log records Grace's reunion camera accepted and served at Good grade. The four-drink summary, other customer rows and Grace's paid departure support successful latte service. September 17 Day02 logs are older and must not be paired with this run. Perfect brushing, collection by normal E input and the durable photo callback remain unverified.

**Current state:** Unity stopped; original `playtest-aces-cafe.json`, `DayLogs/AcesCafeLayout` and startingDay=1 restored. The active scene was already dirty on entry and remains dirty; its unsaved owner edits were preserved, with a separate safety copy in validation. No scene was saved over for this fix. Unrelated owner files remain outside the commit. Nothing pushed. The next task remains the short two-day acceptance route below, followed by the style proposal for review—not another technical or environment expansion.

## Findings, changes and evidence

| Issue | Classification before this pass | Smallest change / current evidence |
|---|---|---|
| Grace blade targeting / Perfect | Failed editor check confirmed; current player-facing blade defect not reproduced | Existing parent-collider resolver was already present. The checker read blade bounds before synchronizing moved transforms. Synchronization now precedes the read, fixture is near the origin, unnecessary edit-mode Awake is removed. Checker passes; actual three-spot brushing and Perfect handoff still need player verification. No camera geometry or grading change. |
| “Not yet” | Confirmed UX clarity problem | Repaired replacement says “Already replaced”; covered replacement names its prerequisite. Grace's inspection prompt lists remaining cleaning surfaces and shutter state. Unrelated repair types retain their existing prompts. |
| Pickup during inspection | Confirmed source defect | E routes to existing item availability/carry rules. Inspection releases before pickup; existing pickup steps out of the station. Full hands leave inspection intact; partial repairs remain collectable. Compilation and independent source review passed; live collection is not yet verified. |
| Unused cup return | Existing behavior; discoverability problem; reported input failure unverified | Refund already exists at the basin. Caption is now “Cup return / discard”; full-hand prompt mentions spare-cup return. Placement and named paddle remain separate. Focused checker passes, but both-hand return has not been verified through normal input in this pass. |
| Cups across recap / checkpoint | Confirmed source-level consistency defect | Closing settles cups before the existing save: empty unlocked cups refund stock; filled and currently pouring cups receive no refund; all outstanding cups, slot references, streams and paused pour/completion audio clear. Focused check covers exact balances and repeat settlement. Actual Continue versus quit/reload remains unverified. Save format/architecture unchanged. |
| Grace two-visit schedule | Confirmed configuration mismatch / late-arrival risk | Existing showcase Day 1 now features Grace first, skips its separate guided opening, permits her existing latte wish and caps service customers at two. Day 2 features her first and caps at two. Ordinary Grace visits become drink visits; explicit Day 1 camera override remains. This shared profile choice also affects future ordinary Grace visits. Stable identity, history, episode and partial consequences are preserved. |
| Safe checkpoint | Completed | Local branch codex/grace-showcase-baseline; pre-fix checkpoint 948892a; owner's intervening commit e39e33f retained; focused fix commit 3233348. No push. |

On the current four-task Grace prefab, the shutter must be repaired for any positive grade. One shutter plus two cleaned spots = Good (.75); the third cleaning spot is still required for Perfect. That explains a plausible Good/“Not yet” confusion, but does not prove it was the exact cause in the owner's earlier playtest.

## What was verified

- Actual Unity runtime/editor compilation completed with no compile errors.
- GraceRepairInteractionChecks PASS: real blade ray/resolution, required-tool and cover gates, completion to Perfect after its fixture removes grime, visual swap and strap retention. This does not test real brushing or the new E collection.
- BeverageFeelChecks PASS: separate placement/pour, stock debit, independent sections, fill/stream, locked pickup, freshness/cold rules and closing settlement. Closing fixture includes two unused cups, one prepared drink and one active pour; balances match and repeat settlement does not refund twice. Its serialization comparison is not a live reload test.
- RecapSaveChecks PASS.
- CustomerMemoryChecks FAILS one pre-existing presentation-policy assertion: it expects Happy on Accepted at low patience; current ExpressionAt intentionally includes Accepted among states that become Impatient. No evidence this changes saved camera history. Kept out of the reliability patch and reported rather than silently weakening the check.
- Isolated live Play Mode: Grace spawned naturally as the first service customer with the reunion camera and Latte configured; she reached intake. A second service customer also arrived. Simulated F through the Input System entered the counter station. Player position was assisted; this is not an uncoached walking test.
- Simulated mouse aiming was not reliable through the editor test driver. Intake acceptance, actual brush work, E collection, drink serving, handoff, both recaps and return/photo were therefore NOT completed as a continuous normal-input test. The interrupted/restarted attempts do not count as success.

## Current state and remaining acceptance gate

At the end of the original baseline pass, Unity was stopped and the active scene was clean. The later hint continuation above supersedes that editor-state note: the owner's newly unsaved scene edits were preserved. Normal save/log settings are restored to playtest-aces-cafe.json and DayLogs/AcesCafeLayout. Unrelated modifications/deletions in scenes, PhoneRepair and three duplicate-material pairs, plus owner day logs, remain outside both focused commits.

There are fourteen existing files in the fix commit: nine runtime files, two existing editor checks, and three existing data assets. No new runtime script, model, scene, system or art asset was introduced for Phase 1. Unity rewrapped/reordered serialized text in Grace's profile; the only authored profile value changed is primaryVisitKind.

**Foundation verdict: not yet verified enough to sign off as the trusted showcase.** The remaining work is one isolated fresh Day 1 -> Day 2 acceptance run, not another systems pass:

1. Accept Grace, repair all three cleaning spots and the shutter, serve her latte, then collect with E while inspecting and hand back. Also check one partial repair and full-hand refusal.
2. Return two unused cups through the selected hands; verify placement does not pour. Close with spare/prepared cups and compare Continue with reloading the same recap.
3. From Day 1 recap, reload and continue to Grace's Day 2 return. Read/reopen the callback before accepting, then accept and observe the correct photo. Finish Day 2 and reload its recap to prove the keepsake is durable. Existing saves are day checkpoints; quitting mid-day does not commit that day's photo claim.

Retain existing Perfect/Good -> clear photo, Passable -> imperfect photo, and rejected/no successful handoff -> missed photo outcomes. No new audio set, listening verification, portrait art, UI restyle or environment pass has been performed. Once the acceptance gate passes, stop technical expansion and present Grace Counter Corner Style Lock for owner review before substantial visual implementation.

---
# Creative audit and vertical-slice roadmap — September 17, 2026

This is the requested first deliverable after the owner's creative-direction brief. Further implementation is paused for direction review. The interior work saved before that brief remains available in `D:/FixitFidget/Assets/Playtests/AcesCafeLayoutPlaytest.unity`; it is an art-development pass, not an approved final style or a shipping-quality slice.

## Evidence and limits

I inspected the current Unity scene, actual project scripts and assets, Git state, the extracted GDD, the approved Layout Study 02, and current game captures. Two independent read-only reviews covered gameplay/persistence and UI/audio. Facts below distinguish source implementation, visible scene evidence, targeted checks and recommendations. This was not a fresh uninterrupted 15–20 minute player session, a listening test or a performance benchmark.

The scene is saved, stopped and clean. The earlier interior compilation passed. Its navigation checks passed 26/26 destinations and 23/23 stationary occupied customer approaches, using the current 0.50 m player radius. Moving-crowd comfort still needs playtesting. An existing isolated Grace interaction check was run during this audit and FAILED its first visible-blade ray assertion; it also produced an edit-mode material-instantiation warning. Do not repeat the older claim that Grace's normal repair is verified. The check's preview scene was cleaned up; the active scene remains unchanged.

## The three biggest bottlenecks

1. **People have more identity in data than in their presentation.** Grace has memory, authored dialogue, a signature drink and a photo callback, but zero of her five portrait slots are assigned. The customer animation controller contains Idle, Walk and Interact; the scripts do not translate impatience, storytelling or sitting into a corresponding expressive performance. The repeated placeholder people overwhelm the identity gained from better furniture.
2. **The interaction feedback does not reliably explain the state of the task.** “Not yet” still hides the reason a repair action is unavailable. The Grace check currently fails. Tool actions often lack sound or a convincing physical completion response. Audio configuration can make important sounds much quieter in the distant isometric camera. These issues affect confidence and satisfaction, even when the underlying transaction works.
3. **The project has expanded before proving one complete quality standard.** There is no demonstrated, integrated 15–20 minute sequence combining final character presentation, tactile repair, readable pressure, a saved return and a memorable callback. The camera often gives the surrounding city as much visual importance as the shop. Uncommitted work spans several passes. A dependable, reviewable slice is the production bottleneck; another neighborhood expansion would not address it.

The owner's diagnosis—more game than identity—is substantially supported. I would qualify it: the loop functions, but interaction clarity and the showcase's reliability still need evidence. Art alone cannot certify those.

## A. What Fix It Fiasco currently is

It is an existing service-management prototype with a physical repair layer and a meaningful regular-customer foundation. It already contains the mechanisms for the desired sequence: a comfortable shop, competing obligations, and a person who returns with a consequence of your earlier work.

| Area | What the project actually contains |
| --- | --- |
| Player and cameras | CharacterController movement, two-item carrying, nearby/aimed interactions, isometric and optional first-person walking, separate station/inspection/conversation ownership, wall cutaways and fixture visibility. |
| Customers and seating | Active customers own jobs, drinks, patience and queue positions. Ambient patrons use shared seating but do not own tickets or keep the day open. The current café has 16 service seats. |
| Repairs | Fault definitions select physical tasks: cleaning, screws/covers, replacement parts, circuit puzzles and human-fault interactions. Quality and physical reassembly are separate; rushing a repair can earn less rather than requiring perfection. |
| Drinks | Six drink assets, physical cups, independently started pours, ingredient spending, visible liquid and freshness. Cooling reduces tips; cold drinks cannot be served. Empty-cup return is already implemented. |
| Social pressure | Repair customers can also order a drink. Drinks and seating affect patience. Reassurance has diminishing returns and a cost. Completing one obligation does not erase another. |
| Regulars | One CustomerProfile asset: Grace. Stable identity, visits, relationship values, outcome-aware return dialogue, remembered requests for quiet and camera/photo episode facts exist. This is a foundation for relationships, not a finished cast or campaign. |
| Days and economy | Five main authored day definitions, phase-based pacing, a guided first drink/repair, recap, stock/restock, six catalogue upgrades and day logs. Separate Grace showcase schedule assets exist. |
| Saving | Day-end and next-day checkpoints, retained backup, protected handling of unreadable/newer saves and purchase persistence. Full mid-day scene restoration is not implemented. |
| UI | Repair tickets, a wrapping ticket rail, state-sensitive teaching hints, conversation/portrait hooks, repair/drink overlays, recap and upgrade screens. |

There is no verified separate daily-objective system. Day-design intentions should not be confused with objectives shown to players. The GDD's larger cast, campaign and release features are targets, not completed work.

## B. What works and should be protected

**The best design is the relationship between time, quality and people.** A partly fixed object can be returned, a drink can help a waiting repair customer, and service outcomes affect later dialogue. That is more distinctive than merely processing restaurant orders.

**Grace's current episode has a specific emotional object.** The camera is needed for a family reunion; its scratched strap belonged to her husband and must remain. A later print reflects what happened. The implemented keepsake is Grace's family-reunion photograph, not a photograph of Ace and Grace together. Earlier owner feedback liked the latter idea; that difference deserves an explicit story decision, not a silent rewrite.

**The bookkeeping is worth preserving.** Customer obligations, separate ambient patrons, seat reservations, stable IDs and safer day checkpoints solve real production problems. Do not replace them because another architecture sounds cleaner. The ticket already has the information required by the new repair-slip art direction.

**Some physical feedback is already useful.** The owner likes the cup shape and visible fill. Screws spin, lift and arc toward the tray. Broken/fresh parts already swap. The shop has a visible day progression, and the broad circulation footprint has passed focused checks.

**The café now has a reusable material and shape foundation.** Oak, sage joinery, warm plaster, the bookcase, shaped upholstery, the new entrance and patterned runner can support the proposed style. They are assets to refine and judge, not a reason to restart the whole room.

## C. What still looks or feels like a prototype

- The repeated humanoid and cylinder player dominate screenshots. Ambient patrons reach a state called Sitting but mostly stop and face the table; their body does not convincingly occupy the furniture.
- Furniture and wall props are improving, but their finish is uneven. The front/left windows have layered joinery; the right window wall retains its earlier treatment. The new glazing looks too hazy in the close-up capture. The floor's photographic grain, simple illustrations, detailed architectural trim and angular people do not yet share one deliberate simplification level.
- The saved entrance has actual modeled door leaves, glazing, panels, hinges and pulls, held open for service. It has collision, but no door-opening interaction or animated closing system. Pastries, shelf cups and wall tools are scenery, not new gameplay.
- The latest repair wall has recognizable tool shapes, but the camera/device itself is still a functional presentation prototype. More tiny background tools will not compensate for a weak close-up repair.
- Money, stock and time remain plain white text over bright scenery. Paper tickets, dark conversation panels and charcoal/mint repair overlays have different visual languages. Portrait hooks exist without Grace's art.
- Repair feedback can be ambiguous: a covered part and an already-completed part can both lead to “Not yet.” Cleaning ends by deleting grime and logging a message; replacement parts swap instantly. Their key sound/feedback hooks remain unfinished.
- The room's lighting includes 14 shadowless point lights and one shadowed directional light. Warmth exists, but large ceiling light pools and weak local contact can flatten the result. Bloom, tonemapping, vignette and color adjustments are already active; adding more post-processing is not a diagnosis.
- The street occupies considerable space in the default framing. It establishes San Francisco, but does not immediately tell a stranger that repairing meaningful possessions is the heart of the game.

## D. Visual identity diagnosis

The problem is **inconsistent emphasis and incomplete human performance**, not simply low polygon counts. The set, characters, typography and feedback currently look like work from different production stages. The eye notices the repeated customers and large empty floor before it notices Grace's story or a satisfying repair.

Our recent passes improved individual objects, but spread effort across the neighborhood before proving one complete scene of play. A larger asset library would increase that spread. The next standard should be judged through one actual camera composition and one encounter.

The approved mockup is a useful composition reference, not a literal production specification. Its strongest features are warm wood, framed openings, upholstered seating, art at several scales, an identifiable workbench and a clear central route. Its appealing image also benefits from people posed and lit for that view. Importing more geometry cannot reproduce that part by itself.

## E. Recommended art direction

**Working direction: a storybook repair-shop diorama, grounded in a lived-in San Francisco café.** Believable object anatomy, soft edges where hands touch, readable exaggerated silhouettes, restrained materials and expressive people. The Schedule I reference communicates the owner's preference for stylized believability; Ace's own identity should come from repaired objects and remembered people.

| Discipline | Proposed rule |
| --- | --- |
| Architecture | Give windows, doors and cupboards a few meaningful layers: frame, recess, hardware and shadow gap. Keep openings broad enough for gameplay sightlines. Match the right wall to the selected storefront language before adding another architectural style. |
| Materials | Use broad, quiet surfaces with controlled grain/roughness. Wood should read at normal gameplay distance without covering every surface in photographic noise. Glass should preserve the street view; brass accents mark handled parts. |
| Palette | Warm cream/plaster and oak carry most of the room; moss/sage anchors joinery and upholstery. Muted ochre and terracotta provide small accents. Reserve stronger contrast for people, active tools and meaningful objects. These are direction rules, not a locked final swatch sheet. |
| Lighting | One legible warm/cool relationship: soft daylight through windows, warm task lighting at work surfaces. Refine contact, shadow placement and exposure before increasing bloom or adding lights. Check the same materials in daytime and evening. |
| Characters | Distinguish a regular through head/hair shape, outfit mass, posture and one accessory. Keep facial geometry modest. A strong silhouette and gesture have more value than realistic skin. |
| Animation | Calm states have breathing and small purposeful motion. Pressure adds a look, a shift of weight or a foot tap. Let a reaction have anticipation and recovery; do not loop frantic comedy across everyone. Sitting must visibly match the chair. |
| Portraits | Portrait and model share hairstyle, clothes, accessory, palette and posture. Start from one neutral design with the owner's girlfriend, then test it beside a 3D blockout before completing five expressions. |
| Props | Detail comes from use: a polished handle, a repair tray with compartments, a worn strap, an annotated returned photograph. Each cluster needs a function or history. Leave the active repair/pour/hand-off surfaces clear. |
| UI | Extend the existing repair-ticket language: warm paper, dark ink, clean body text, restrained accent, a clear result stamp. Decorative handwriting belongs in short notes, never essential instructions or small counters. |
| VFX | A brief glint, dust release or gentle liquid response should confirm an action. Avoid continuous particles that hide the task or compete with customers. |
| Audio | Build a compact palette of tactile clicks, soft liquid, cup contact, a recognizable arrival bell and restrained customer reactions. Important cues must remain audible in both camera modes. Establish priority and perspective before collecting a large library. |

**Blender and Unity have different jobs.** Blender should produce the reusable authored models, UVs and animations. Unity is where scale, gameplay distance, lighting, materials, contact, interaction and performance are judged. Do not perfect the entire café in Blender before testing it in Unity. Export and judge one representative cluster early, then propagate its rules.

Useful reference lessons, interpreted through this project: [Assemble with Care](https://www.assemblegame.com/) connects repairable possessions with people; [Coffee Talk](https://www.togeproductions.com/project/coffee-talk/) uses drink service to support listening and relationships; [Minami Lane](https://doottinygames.itch.io/minami-lane) is a useful example of a deliberately small management setting. These suggest priorities for Ace, not layouts or content to copy.

## F. First hero screenshot plan

**Choose Grace at the intake counter, with the repair bench behind one shoulder, the beverage station behind the other, and the left window edge establishing the neighborhood.** Use the existing room. Frame the counter and service strip rather than the full road network; the runner should lead toward this area.

The composition should have three levels of attention: Grace and her camera first; the active repair/drink areas second; personal evidence on the wall third. Show one other waiting customer to communicate pressure. Furniture elsewhere should remain quieter.

Proposed changes within this one area:

1. Refine the counter's materials and near-contact shadows; select one final window/glass treatment and consistent wall finish.
2. Stage Grace with a recognizable silhouette and camera case. Keep the sentimental camera/strap readable against a quiet work mat.
3. Place the photo mount where its later arrival is noticeable. Keep the surrounding art restrained so the keepsake has visual importance.
4. Use the existing repair tray, tools and coffee shelf as small purposeful groups. Remove or move conflicting decorative items rather than filling every gap.
5. Bring the ticket and conversation panel into the same paper-and-ink family, with readable text over a calm backing.

**Acceptance:** at the normal playable camera distance, a new viewer identifies a repair café and finds the customer, workbench and drink station within a few seconds. The scene must also survive a real first-person interaction and an evening view. A posed marketing render alone does not pass.

## G. Grace as the style-lock character

Grace is the best candidate because her behavior and consequence already exist. Her actual profile describes a retired school photographer with warm manners and exacting standards; she tells stories, orders a latte and can remember being asked to let Ace concentrate.

Agree on one silhouette sheet with the owner and portrait artist: hair mass, posture, outfit shape, glasses/accessory if desired, and a camera/case. A mustard/ochre accent can connect her existing theme color to the shop without making her blend into the furniture. Those costume details remain proposals for the owner's taste.

Start with a neutral portrait and simple 3D proportion study together. Then complete the five existing slots: neutral, delighted, worried, impatient and surprised. Different eye shapes, brows, head angle and shoulder posture should carry the expression; mouth changes alone are insufficient.

The first animation set should support the actual encounter: recognizable walk/idle, presentation of the camera, one storyteller gesture, one clear impatience reaction and a warm return/thank-you beat. Reuse the existing interaction ownership and conversation system. Defer a large character framework and cast-wide bespoke animation.

The target is recognition before her name appears, understandable emotion during service, and a visible change when she returns. Preserve the family-reunion and sentimental-strap facts unless the owner explicitly chooses a different story.

## H. Gameplay presentation and reliability opportunities

| Existing system | Small, meaningful next improvement | Evidence / limit |
| --- | --- | --- |
| Grace camera | Reproduce the user's exact cursor/tool sequence; expose why a task is blocked; distinguish completed, covered and wrong-tool states. Then verify partial grades and Perfect. | The existing isolated checker failed at the blade-ray assertion today. This is a blocker to claiming verification, not proof of the player's exact root cause. |
| Cleaning/replacement | Pair visible progress with a short scrub/contact sound and a decisive final mechanical response. Let the camera shutter test communicate success. | GrimeSpot still logs a placeholder completion; ReplaceablePart contains an audio TODO. Preserve task accounting. |
| Drink preparation | Clarify returning an unused cup and active-hand selection; verify the fill, freshness ring and collection in both views. | Independent brewing, perishability and empty-cup stock return already exist. Do not rebuild them as new features. |
| Audio perspective | Test phone and drink cues from overhead and first person, then choose a consistent listener/mix policy. | Main Camera carries the listener roughly 34 m from the café focus; phone is fully spatial with 16 m max distance, pour is mostly spatial with 4.5 m max distance. Audibility risk inferred from configuration; no live listening result yet. |
| Customer patience | Connect existing patience states to one look/pose/reaction and matching portrait. Make comedy arise from understandable neglect. | The current animation controller only has Idle, Walk and Interact. A numerical mood change does not produce a full body performance. |
| Day/Grace scheduling | Prove the selected first-visit and return windows under slow play and a full queue. | Featured arrival waits for a free open-hours slot; guided lessons can delay it. The GDD says Days 2/3, while the local showcase uses Day 1/2 assets. Align one reviewed schedule. |
| Checkpoints | Verify recap → next day and recap → quit/load produce the same stock/available-cup policy. | SaveData stores stock, not loose prepared cups; StartDay clears people. Potential inconsistency needs a focused test. Do not call the whole save system broken. |
| Upgrades | Fix or remove Wide Brush from the slice offer until its effect is demonstrable. | It is in the live catalogue; ScrubSpeedMultiplier has no consumer in Assets/Scripts, and ItemInspector uses its fixed scrubPower. Source-level wiring defect, not a measured timing comparison. |

The completed-item pickup complaint remains a normal-input verification item. Do not close it based on an unrelated handoff or scripted state change. Likewise, a passing stationary route check does not certify moving crowds.

## I. Scope cuts and production discipline

Pause further street expansion, more vehicles, outdoor foliage replacement, a larger prop library, pastry gameplay, cat behavior, additional drinks/devices, more regulars, a long campaign and console work. Keep the approved independent hands/rigging session separate. No multiplayer, backend rewrite or general architecture refactor is justified by this audit.

The public slice needs clear PC controls, readable small-resolution UI, audible state changes, a recoverable save checkpoint and measured frame pacing. Complete controller support, broad remapping/accessibility coverage, localization, launch-scale save migration, content volume, platform certification and storefront publishing belong to later release preparation. Choose minimum slice accessibility deliberately—legible text, non-color-only state, adjustable sound and tolerable camera motion—rather than building a large settings suite first.

Source control needs a checkpoint before another implementation milestone. The current branch is `codex/device-scaffold`, not main. Git reports 88 status entries spanning prior gameplay/art work, logs and new assets; that count includes untracked directories, not 88 newly authored scripts. Do not sweep everything into one commit. Review ownership, separate meaningful changes from captures/logs, then make focused checkpoints and a development branch for the agreed slice. No branch switch, reset, commit or push was performed during this audit.

## J. Ordered roadmap to a 15–20 minute vertical slice

| Order | Priority | One milestone and exit condition |
| --- | --- | --- |
| 1 | **Must Have** | **Trustworthy showcase baseline.** Checkpoint current work; reproduce Grace's targeting/grade problem, completed-item collection, unused-cup return and the prepared-cup checkpoint question. Choose one featured schedule. Pass the normal player journey and a saved return. Keep each fix small. |
| 2 | **Must Have** | **Grace's counter corner style lock.** Review one composition, material palette, lighting and prop density in Unity, alongside one neutral Grace portrait/3D study. Owner and portrait artist approve the relationship between them. Stop adding art if this comparison does not improve identity enough. |
| 3 | **Must Have** | **One expressive visit.** Finish Grace's five portraits and limited animation set; make the camera repair and one drink satisfying through visible action, sound and clear task/result feedback. Reuse the ticket and conversation systems. |
| 4 | **Must Have** | **A complete playable return.** Assemble a short introduction, manageable service pressure, Grace's repair, recap and return/photo consequence. Verify at least a successful and an imperfect path, including quit/load. The sequence must work without the developer explaining it. |
| 5 | **Must Have** | **Public-slice check.** Time fresh-player sessions to 15–20 minutes, measure frame pacing in a standalone PC build, check active orders at 1080p and a smaller resolution, verify audible cues in both views and record a short representative capture. A new player should recall Grace and the consequence of the repair. |
| After proof | **Should Have** | A second contrasting regular or one short systemic social mishap, a restrained room-ambience layer, one additional repair family if it reveals something new, and polish outside the hero view where players actually walk. Add only what improves tested sessions. |
| After the slice | **Later** | Wider cast/campaign, broader environment and foliage, final Ace/hands pipeline, extensive recipes/upgrades, full controller/Deck work, launch settings/accessibility/localization, and consoles after PC viability. |

The existing 180-second days do not establish a 15–20 minute experience. Measure dialogue, learning, repairs, service and recap time before changing day duration. Use the existing day definitions to curate the slice; do not build a new scheduler merely to fit a target runtime.

Suggested emotional pacing, to test rather than treat as a finished schedule: the player first understands the shop and completes a simple action; Grace gives the camera meaning; one competing demand produces controlled pressure; the repair and handoff give relief; her later return makes the earlier effort matter. The callback is the endpoint, not an invitation to keep adding features before review.

## Source anchors for review

- [Current profile and five expression slots](D:/FixitFidget/Assets/Scripts/CustomerProfile.cs:46), [Grace's assigned profile](D:/FixitFidget/Assets/Data/Regulars/Regular_Grace.asset:15), [episode facts and dialogue](D:/FixitFidget/Assets/Scripts/GraceCameraEpisode.cs:31), [photo display](D:/FixitFidget/Assets/Scripts/GracePhotoDisplay.cs:23).
- [Repair quality and reassembly](D:/FixitFidget/Assets/Scripts/JobBase.cs:40), [task capture](D:/FixitFidget/Assets/Scripts/RepairJob.cs:25), [ambiguous blocked-action feedback](D:/FixitFidget/Assets/Scripts/ItemInspector.cs:205), [existing interaction checker](D:/FixitFidget/Assets/Editor/GraceRepairInteractionChecks.cs:42).
- [Patron sitting state](D:/FixitFidget/Assets/Scripts/PatronBrain.cs:147), [customer animation](D:/FixitFidget/Assets/Scripts/CustomerBrain.cs:792), [featured scheduling](D:/FixitFidget/Assets/Scripts/CustomerSpawner.cs:218).
- [Independent cup/pour](D:/FixitFidget/Assets/Scripts/BeverageSlot.cs:39), [unused-cup return](D:/FixitFidget/Assets/Scripts/BeverageCupSupply.cs:24), [freshness](D:/FixitFidget/Assets/Scripts/DrinkFreshness.cs:16).
- [Ticket information](D:/FixitFidget/Assets/Scripts/JobTicket.cs:25), [conversation input/presentation](D:/FixitFidget/Assets/Scripts/ConversationController.cs:115), [pour audio](D:/FixitFidget/Assets/Scripts/BeveragePourAudio.cs:22), [phone audio](D:/FixitFidget/Assets/Scripts/HoldCallPresentation.cs:17).
- [Checkpoint protection](D:/FixitFidget/Assets/Scripts/SaveCheckpointStorage.cs:18), [save scope](D:/FixitFidget/Assets/Scripts/SaveData.cs:60), [upgrade multiplier](D:/FixitFidget/Assets/Scripts/UpgradeManager.cs:80).
- [Current interior overview](D:/FixitFidget/Assets/Screenshots/aces-cafe-interior-overview.png), [window seating close-up](D:/FixitFidget/Assets/Screenshots/aces-cafe-interior-window-seats.png), [approved mockup](</C:/Users/Mansoor Qaiser/.codex/generated_images/01a08d07-3029-7ba2-9283-b48b1bff1baf/exec-971b89e7-19a6-4f98-81a2-ba10c2ae9ce6.png>).

---

# Earlier playtest feedback and roadmap — September 12, 2026

September 12 screenshot addendum: keep art separate and preserve the first-person cup presentation the owner likes. Fix the slanting dispenser look and mirrored printed labels; make the second carried item readable beside the current capsule if simple. These small changes were applied; camera/carry checks and isolated delivery transactions passed. Final isometric visual inspection was interrupted by the owner's Escape stop signal. Sensitivity acceptance and Grace's normal-tool repair remain open. See Current-Handoff-2026-09-12.md for the latest verified state.

This document records the owner's latest playtest feedback and the resulting acceptance criteria. It is an assessment and implementation brief, not a claim that the changes below have been completed or verified. The implementation report must separately state what was changed, tested in Unity, and still needs a player judgment.

## Current assessment

The game is improving in specific, observable ways. The owner can now recognize the cup and enjoys the visible stream and rising liquid. The shop already supports a playable service loop and a previous five-day run. Grace's returned photograph prompted an emotional response from both the owner and his girlfriend. These are useful signs: a service action is becoming satisfying, and a customer consequence is becoming memorable.

The current presentation still breaks the connection between player intention and game response. Floating cups, jitter, unclear hand selection, restricted looking, and uncertain repair feedback all make the player work to understand basic actions. Those are high-priority defects in the experience. A new model alone cannot resolve them.

The requested level of quality should mean responsive controls, stable movement, readable physical interactions, carefully chosen sound, consistent visual craft, and memorable character moments. Achieving that in one compact sequence is a useful production target. The present prototype is not yet a polished vertical slice.

## Every feedback point and its acceptance criterion

| Owner feedback | Direction and evidence needed |
| --- | --- |
| The visible pour is enjoyable. | Preserve a continuous stream connecting nozzle and cup, with liquid rising throughout the pour. Check from the intended camera angles and while another section is running. |
| The new cup finally reads as a cup. | Preserve its open rim, readable volume and liquid surface. Holding, placing and collecting should keep its orientation and apparent size coherent. |
| The pour sounds like a buzz. | Replace the tonal impression with a soft liquid texture, modest splashes and an unobtrusive completion cue. Listening in isolation and during normal shop activity is necessary; code checks cannot certify a cozy sound. Allow the final chosen sound to be assigned without rewriting the interaction. |
| The hands HUD is unwanted. | Remove the persistent inventory-style hands panel. Communicate possession through the visible cups/devices and hand pose. Keep only brief interaction guidance where needed. |
| The owner wants visible first-person arms and hands. | Give the beverage view visible hand support for both carried items. Any temporary hand geometry must be described as a prototype. Final Ace anatomy, sleeve design, skinning, grasp poses and animation remain character-art work until they exist and are inspected. |
| Two cups float above the shop after leaving the station. | In isometric play, attach items to stable character-relative hand positions. Camera motion must not place cups above the room. The same items must remain physically associated with the player through entry, exit, walking and turning. |
| Carried items stutter or lag. | Update carried poses in a consistent order with the character and camera. Test stationary turns, diagonal travel, changes of direction and camera transitions with two items. A placement fix is not enough to certify smooth motion. |
| Drink-name labels below the cups are ugly. | Put the permanent drink names on the colored physical controls, with sufficient size, contrast and line layout. The latest instruction explicitly revises the earlier blanket preference for projected information: machine identity belongs on the machine here. Temporary prompts and changing status can still be projected. |
| The circular freshness bar was liked and should return. | Restore an independent freshness ring for each completed drink. It should begin full when pouring finishes, remain readable when carried, and distinguish Fresh, Cooling and Cold. Its meaning must stay separate from fill progress; the visible liquid already communicates filling. |
| The dispenser's upper colors merge awkwardly. | Give the upper shell and trim a clear seam, shape change or controlled shadow gap. Inspect the actual material/lighting combination in the shop. A different color alone may leave the same structural ambiguity. |
| The prop should feel cozy, fun and animated. | Use coherent rounded forms, clear functional parts and restrained responses such as a paddle press, cup placement settle and completion response. Protect aiming accuracy and readability while adding motion. The machine should remain easy to use repeatedly. |
| The player cannot comfortably turn their head in the beverage view. | Permit deliberate looking around the station while keeping pointing and interaction understandable. Verify look, interact, hand selection, pause and exit together, including consistency with the other workstations. |
| Cup selection feels wrong. | Make the active hand physically apparent and switching predictable. Picking up a second cup must not create an unexplained placement action. Keep two shared carry slots for cups and repair objects. Test both pickup orders and one occupied hand. |
| The desired sequence is pickup, placement, then choosing the named dispenser control. | Preserve that deliberate sequence: enter, aim at the cup stack and see Pick up, take up to two cups, place one under a nozzle, press its named colored control, watch it fill, collect, then leave. This supersedes a combined place-and-immediately-pour action if that action skips the player's explicit dispensing choice. |
| Grace's tweezers step produces an unclear message and Perfect seems impossible. | Reproduce the camera repair through normal player input. Identify the actual error rather than guessing from the paraphrase. Prove that fixing the shutter and every cleaning task can award Perfect; partial and failed outcomes must remain meaningful. State recovery instructions clearly if a tool action is invalid. |
| Grace returning with a photograph is loved. | Preserve the physical keepsake, remembered visit and outcome-dependent return. This is evidence in favor of investing in customer consequences. The owner's phrase “a photo of us together” should inform the intended keepsake, but the existing checkpoint describes a symbolic reunion family photo with Grace. Verify which image is actually present before claiming it already depicts Ace and Grace together. |
| Earlier, the second bench item could not be placed without backing away. | Keep this in the regression pass. From the same sensible standing position, place two devices consecutively in either order without accidentally picking the first one back up. The newest feedback does not establish that this test passed. |

## Preserve the service rules

Two items share the player's carrying capacity, regardless of whether they are drinks or repair objects. Pours remain independent. Players can prepare drinks before an order exists. A completed cup occupies its nozzle until collected. Dispensing without a cup consumes ingredients and visibly wastes the pour. Cooling drinks retain the established reduced-tip behavior; cold drinks cannot be served and require deliberate disposal. This presentation pass should not change the fuse challenge.

## What the owner did well

- Described concrete moments of friction: taking a second cup, leaving the camera, aiming at a control, and trying the tweezers. These reports can become reproducible checks.
- Identified successful elements as well as failures. That protects the visible fill, readable cup and photograph from being discarded during a redesign.
- Described a full intended interaction sequence. This is more actionable than asking for a generally better dispenser.
- Tested the transitions between viewpoints. Those transitions exposed an ownership and motion problem that a static screenshot could not reveal.
- Noticed an emotional response to Grace's keepsake. That supports the GDD's customer-first direction and gives the team a concrete moment to strengthen.

## How to make the next test more useful

For each failure, capture the last action, the expected response and the actual response. Exact error text or a short clip is especially useful for the tweezers and motion defects. These are helpful records for future testing, not prerequisites for the current investigation.

Separate three decisions when evaluating a change: does it work reliably, is it clear on first use, and is it enjoyable to repeat? For example, a pour can fill correctly while still sounding unpleasant. Test one improvement set before changing freshness timings, customer arrivals or rewards, so the reason for a better or worse experience remains visible.

After this interaction pass, invite someone who has not been coached through the station or Grace's story. Observe what they try before explaining the intended controls. Ask what they remember about Grace afterward. The owner's and girlfriend's enthusiasm is encouraging; it does not replace a fresh-player recognition check.

## Roadmap order

1. **Finish the focused reliability and feel pass.** Resolve physical carrying, station looking, cup placement/selection, labels, freshness, sound and the camera-repair blocker. Verify transitions and the earlier bench placement problem. Follow with a short normal-pressure playtest.
2. **Finish M1: Grace's customer showcase.** Complete the working camera-to-return sequence, clear grading, remembered focus boundary, readable keepsake and persistence. Resolve the deliberately open schedule, reward and retry decisions at the appropriate authoring stage. The finish line is a new player remembering who Grace is, why her camera matters and what changed when she returned.
3. **Run comparable complete days once the revised interaction is stable.** Check pending drinks, waste, satisfaction and subjective workload before making economy or timing decisions. Preserve campaign progress while using the isolated test setup.
4. **Advance to M2: the character and ejection animation gate.** Prove Ace/customer rig support, holding and locomotion compatibility, grab/shove/recovery, contact, camera framing and transitions in engine. The current carrying presentation can provide usable attachment points, but it does not certify a finished character rig.
5. **M3: boundary showcase.** Build the deliberate warning/ejection encounter, witness response, cleanup and callback on the proven animation foundation.
6. **M4: cohesive vertical slice.** Bring Days 1–5, three regulars, two or three devices and two drinks to consistent quality, with accessibility and external testing. The six-drink design remains a broader target; a larger menu does not complete the slice.

M0 has evidence of a functioning baseline; it still needs regression protection as controls change. M1 is the active milestone. M2 and later milestones should not be marked complete by the presence of placeholder hands, a dispenser model or individual passing checks. Repeatable content production, demo and campaign milestones follow the vertical slice.

## References

- [September 10 assessment](./Fix-It-Fiasco-Assessment-2026-09-10.md)
- `D:/FixitFidget/roadmap-progress.md` — September 9 checkpoint.
- `D:/FixitFidget/continuation-checkpoint.md` — September 10 recovered prototype and acceptance gates.
- Owner's September 12 playtest notes and request for complete, articulate follow-through.
