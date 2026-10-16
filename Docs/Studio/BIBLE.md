# The Studio Bible — Fix It Fiasco

*Version 2.4, 14 October 2026; §7 rewritten 15 October 2026. Every lead in the studio reads this file first, before its own role file, before any task. The Producer keeps §7 (current state) true; everything else changes only when Mansoor says so.*

*What changed on 15 Oct: §7 only (the build ran on his Mac; Day 1 played by hand; three fixes; the labs), and §8 marks `claude/bench-spec-v2.md` superseded.*

*What changed in 2.4 (14 Oct), after playtest 5, which neither he nor his girlfriend could get past Day 1:* **the studio's rules (§4.1)**, starting with reference footage before work and no physics in the core loop; **his fifteen calls of 14 Oct** (§2, "The forgiving bench"; he delegated them: "for all your questions, i trust your judgement to do the best option, the option that is closest to my request"); the bench pillar's wording (§1: the screw flies to its cup); the bench's conventions (§6); the models (§3.7: Fable ran out mid-session, so every lead ran on Opus 5.5 at max effort, by his rule of 9 Oct).*

*What changed in 2.3 (12 Oct, 21:00): the models (§3.7). With his Fable usage back, his rule is Fable 5.1 for anything big that needs proper thinking and Opus 5.5 for the other agents; the role files' `model:` lines carry each lead's default and the Producer picks per task.*

*What changed in 2.2 (12 Oct): a **Cinematics & Camera** lead joins at rank 4, beside UI/UX and Audio, at his request after playtest 4's first day ("we should have someone in the department that could help with intros and cutscenes and other montages"). It owns the camera language of the whole game (the conversation camera, the bench's framing, the views and their blends), the day intro he asked for, montages, staged scenes and the trailer capture (§9, `cinematics-camera.md`).*

*What changed in 2.1 (10 Oct): two playtesters join the roster at his request ("make sure we also have agents whose soul jobs are to playtest and see if it feels and reads as a solid game"): `playtester-first-hour` (does it read, to a newcomer) and `playtester-veteran` (does it feel right, against the three pillars). They play every build that changes what the player sees or does, before he plays it, by the method in `Docs/Studio/PLAYTEST-PROTOCOL.md`, and they build nothing (§9, §10).*

*What changed from version 1 (9 Oct, 02:00): the roster grows from 12 to 14 leads (Progression & Economy and a Technical Director are new, because he named "game leveling" and "overall structure" as departments of their own); every lead runs on the best model available, Opus 5.5 at max effort until he has Fable usage again (§3.7); the leads are ranked by role with story and game design first (§3.8); research, design and Blender work may run in parallel, anything that touches Unity runs one lead at a time (§3.9); §7 is rewritten for tonight; the Coordinator is now called the Producer.*

---

## 0. How to use this file

- You are one lead in a small studio built around one person. **Mansoor decides everything.** You propose, build, critique and research; you never decide for him, and you never present a choice you made as settled. Choices you had to make to finish a task go in your output under *Decisions his to overrule*.
- Read in this order: this file → your role file (`Docs/Studio/agents/<role>.md`) → the documents your role file lists under *Reads first* → the task.
- When this file and a project doc disagree, say so in your output and follow this file. When this file and Mansoor disagree, Mansoor is right and this file needs a change.
- Dates matter. The project moves daily; a document from September may describe something since rebuilt. The repo handoff (`Docs/ProjectDirection/Current-Handoff-2026-09-12.md`, newest entry at the top despite the file name) is the most current account of the build; the Project docs (`claude/session-*-notes.md`) are the most current account of the thinking.
- **Where you run.** On his machines you are a Claude Code subagent loaded from `.claude/agents/` (installed there from `Docs/Studio/agents/`, §10). In a claude.ai session (the cloud, linked to his Mac) Claude hands you this Bible and your role file in the prompt; the repo paths still apply, the repo is on his Mac (`~/Documents/FixitFidget`), and only the session that launched you touches Unity.

---

## 1. The game in one page

**Fix It Fiasco** (working title also *Ace's Café*) is a 3D repair-café management sim for PC, built solo in Unity by Mansoor (QaiserGames) for a **Steam launch in 2027 at $12.99**, wishlist target 7,000–15,000 before launch. His girlfriend does the 2D art; his sister writes and playtests dialogue.

**The pitch.** Ace is broke, has a mortgage, and runs a small repair café in a San Francisco district: by day customers bring in broken devices (a phone, a pocket watch, a camera) and order drinks while they wait; Ace juggles the counter, the bench and the drinks station under pressure. The café is the district's information hub: Ace learns where people live, what they own, what they lie about. Then one night a man hiding behind the café's bins makes Ace a deal: run his petty night errands (take a thing, play a prank, get into a house) or else. Favours, never money. By night Ace sneaks the same neighbourhood as a nosy, petty menace, and gets back before morning. The dream is to franchise and move on to a new city — the project's first line was "builds his repair/cafe store to becoming a whole building/monopoly". The twist is that the man follows: every new café has its first bin night.

**The three pillars, in his words (7 Oct):** "it has to feel and run like reStory when dealing with the repairs, the hecticness from just orders coming in, and then the mysterious but thief aspect at night while franchising the cafe."
1. **The bench is held to reStory's bar.** The screw comes out of the hole you clicked and flies to its cup; nothing removed can end up out of sight or under the device; the cracked screen looks like a cracked screen; every part of a device means something. (14 Oct: ReStory *looks* physical but behaves scripted, frame by frame: screws reach their dish by themselves, drags are rigid with no depth, a sloppy drop still snaps home; `claude/playtest-5-notes-and-plan.md`.)
2. **The day is hectic.** Orders keep coming; the pressure is the queue, the shelf, the drinks, the clock (*PlateUp!*, not a cozy-only sim).
3. **The night is mysterious and a little criminal.** Petty theft, pranks, break-ins, the man at the bins, the officer; nerve, not horror.

**Tone: "chaos with a hint of cozy."** Tense service days, calmer evenings, nights with nerve. Not GTA, not a horror game, not a cozy-only sim. Storytelling he admires: *Breaking Bad*, *Better Call Saul*: ordinary people, pressure, consequences.

**Positioning.** Against *PlateUp!* (pressure, a loop you get better at), not against cozy repair sims like *ReStory*. The repair fantasy sells (*ReStory*, 300k+ copies); the differentiator is pressure and a story with teeth.

**The demo target** is Days 1–3, three devices, Grace (the recurring regular with memory and a return arc) and the first nights.

**Characters that exist:** Ace (player); Grace (regular, storyteller, has a house the player can enter at night); the Officer (regular, day and night); the man at the bins (the hitman; the deal); Barnaby (Grace's garden gnome, Night 1's errand); walk-in archetypes and patrons.

---

## 2. Locked decisions (do not reopen without Mansoor)

Design and systems:
- The GDD is the source of truth for design and roadmap. *See §7 for which version exists where.*
- **No new mechanics (October 2026).** The game needs foundation, content, audio and polish, not systems. A proposal that adds a verb, a resource, a meter or a loop is flagged, not built.
- Job = customer + device + fault, a data record created before the item spawns.
- Devices are dumb (no dialogue); customers own voice and memory.
- Dialogue gates the decision: no action until the line has finished revealing.
- Handbacks are quick transactions, not conversations.
- StormOut uses world-space bubbles; intake/accept/decline use the panel.
- Patience bar hidden on arrival, visible only while waiting; no patience bars inside conversations.
- The circuit puzzle's charge moves only while the player is at the bench; retries cost a grade tile, not seconds.
- The Human fault is a ten-second breather, not a minigame: the one fault that costs no thinking; protect that role.
- The beverage station dispenses whether or not a cup is present (physical-world logic).
- Saves happen at Open Tomorrow (after the day increment), never at recap open.
- Upgrade asset names are the save identity; never rename them post-ship.
- **Objects are objects, information is projected.** World space carries colour and shape; the HUD carries numbers. Never mix the two.
- Layout is cosmetic, not systemic (see §3.5 for what customization means under this rule).
- Grace's Day 1 latte suppression is intentional, not a bug.
- **The purpose rule.** Every part of a model must mean something: a part stays only if the player works it, it tells how the thing works or what is wrong, it makes the silhouette, or it is this world's texture (`claude/hero-models-spec.md` §5). His words (8 Oct): "this will be an indie game, every part has to mean something, every item on the tool has to be there for a purpose."
- **Real life first.** His words (8 Oct): "inspect these items in real life and see how they look and incorporate that with these assets." Hero objects are built from measured references (`claude/hero-reference-sheet.md`), then cut to purpose.
- A collider is the piece's solid, never its bounds, wherever other pieces sit in a recess of it.
- **The forgiving bench (his calls of 14 Oct, delegated to the Producer's judgement, `claude/playtest-5-notes-and-plan.md`):** taking apart stays physical and under the hand, everything after it is automatic (each freed piece flies home on a scripted arc: screws to the mat's cups, covers to the well, broken parts to the scrap tray); the fresh part flies to its seat on one press and a hold presses it in (no carrying, no dropping, no depth); **one scale** (every device at real size, at the bench and in the café; screws 3.8 mm heads, holes a small threaded opening); the cloth and the brush by direct grab; a slate-blue silicone repair mat with moulded compartments; the checklist as one small paper card on screen, under 25 words; **no physics in the core loop** (scripted motion for anything the player must rely on; physics only as flourish that can never lose a piece). Day 1 is Grace's lens and film-path clean; she returns on Day 2 for the shutter. The day is 420 s.
- **Brands are fictional and legal-friendly**, with a playful nod to the real ones where it's funny (his words, 8 Oct: "fictional brands exist, lets just make sure its legal friendly, we can make a playword with famous corporations"). Example: the phone's maker is Pome (a leaf, no fruit, no bite). No real logo, trade dress or name.

Camera (this caused a serious disagreement once; get it right):
- Five Cinemachine cameras: `CmShopCam` (isometric default for moving, delivering, customer interaction), `CmCounterCam`, `CM_BenchCam`, `CM_InspectCam`, `CM_ConversationCam` (first-person, when engaging a station).
- Isometric for moving and delivering; first-person wherever Ace does something with his hands. Since September `CafeViewMode` also offers an optional walking first-person view (V) because Mansoor likes having it. Station, inspection and conversation cameras unchanged.
- By day, stations are *reach*, not docking: E talks, the hands work the drinks, a click works the bench. F is the torch at night.
- Walk-in customers should look and feel like the characters in *Peak*: fun, cozy.

Story (chosen 5 October 2026):
- The frame: a broke Ace with a mortgage; the man behind the bins; favours, never money; petty errands (take things, pranks, break-ins), escalating.
- Franchising: **Ace moves on, and the man follows.** A new city is a new street, new houses, the same man at the new café's bins on its first bin night. The night (houses, favours, the officer, the notebook) is a kit fed by city data, like the café.
- The night narrative must stick, not feel loose; the night's world must be explorable inside the houses, not "decorated with shapes."

---

## 3. Decisions made in building the studio (9 October 2026)

Mansoor handed these to Claude's judgement. They are recorded so every lead works from the same reading, and so he can overrule any of them in one line.

**3.1 "Game leveling" means progression, and it has its own lead.** The player's growth: money, the upgrade shop, unlocks, the difficulty ramp across days, the onboarding economy, how the café grows toward the franchise. Owned by **Progression & Economy** as *tuning and pacing of what exists*. Level layout (the café's floor, the district) belongs to Environment & Scene. XP-style player levels would be a new mechanic and are not on the table.

**3.2 "Overall structure" is split three ways.** The shape of the *experience* (day loop, night loop, how days escalate, where the story beats land, what the franchise goal means) → **Narrative & Game Design Director**. The shape of the *code and the product* (how 229 flat scripts are organised, assemblies, builds, performance, save safety at the format level, Steamworks, platforms) → **Technical Director**. The shape of the *studio* (who works when, the one list, the notes) → **Producer**.

**3.3 The Producer proposes; it never decides.** Mansoor is the yes and the no at the end of everything. The Producer's whole job is to make his decisions easy: reorient, propose one ordered list, flag anything that would add a mechanic, write the session notes.

**3.4 Franchising: one city built with love at launch; the move is the ending of Chapter 1.** The launch build is Chapter 1 (the San Francisco district) complete and dense, and it *ends* with the franchise move: Ace arrives in the new city, and the first bin night proves the man followed. That beat is the game's hook for Chapter 2 and the proof that the café kit and night kit are data-driven. Chapter 2's content (the second city's street, houses, regulars) is a post-launch chapter unless time allows. Reasoning: players are excited by depth they can feel and by a promise visibly kept, not by two thin cities; the twist lands harder as an ending than as a feature list; a solo developer with a 2027 date and a no-new-mechanics rule cannot build two cities to the standard of one. (`claude/franchise-ready-cafe.md` §4.2 — identical café or a local look per city — stays open for Mansoor.)

**3.5 Furniture customization is cosmetic, with physical truth, and it travels.** The player arranges and swaps the furniture library in their café. What is placed is real in the world: fewer tables means more room to walk, a seat is where a customer sits, the NavMesh follows. There are no hidden stats: no throughput, reputation or mood numbers from layout. The one systemic touch is narrative: the café is the one warm, safe place in a game whose nights are dangerous, so the customization is where the player's love shows, and *your* layout is what franchises to the next city (`claude/franchise-ready-cafe.md`). A placement mode is UI, not a new mechanic.

**3.6 Generated reference imagery is allowed, scoped.** Mood, palette and composition boards only, for discussion and for briefing the 2D artist. Never "copy this" references for the portrait style, and nothing generated ships in the game.

**3.7 Models: Fable 5.1 for the thinking, Opus 5.5 for the rest.** (14 Oct: his Fable usage ran out as the morning's leads launched; by his rule of 9 Oct every lead, builder, reviewer and QA pass of that session ran on Opus 5.5 at max effort.) His words (9 Oct, 20:12): "i want each agent to be highly trained meaning the best language model, fable5.1" and "i am out of fable use, so lets overall run opus 5.5 max"; then (12 Oct, 21:00), with Fable back: "make sure to utilize opus 5.5 only for the other agents, anything big that requires actual proper thinking make sure its fable 5.1". So, from 12 Oct:
- **The rule is per task, and the Producer applies it when it launches a lead:** anything big that needs proper thinking (a design, a spec, a diagnosis, an architecture, a verdict on direction or feel, a large change to the code) runs on **Fable 5.1**; routine work (a small fix with a known cause, a draft from a given outline, a lookup, file hygiene, a build step that follows a plan) runs on **Opus 5.5**. Both at `effort: max`. The main session that runs the studio is on Fable.
- **The role files' `model:` line is the default when he runs a lead himself from Claude Code**, set by what that lead mostly does: `fable` for the Producer, the Narrative & Game Design Director, Systems & Mechanics, Progression & Economy, Content & Dialogue, the Technical Director, QA & Critic and the Genre Veteran (its verdict on feel); `opus` for Environment & Scene, Art Direction, NPC & Characters, 3D Art & Blender, UI/UX, Audio, Cinematics & Camera, Marketing & Steam and the First Hour. The per-task rule overrides the default either way (the NPC lead's architecture of the life pass ran on Fable; a Fable lead's chore runs on Opus). The split is the Producer's call of 12 Oct, his to redraw.
- **To move the whole studio at once** (if he says so): `export CLAUDE_CODE_SUBAGENT_MODEL=fable CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1` in the shell that starts Claude Code, for one session; or the Producer changes every `model:` line under `Docs/Studio/agents/` (the command is in `producer.md`) and this section, and he re-installs the roster (§10).
- No lead is ever downgraded to save cost without his say-so, and no big task runs on Opus because Fable is slower. Fan-out (several instances of one lead on parallel tasks) runs on the task's model.

**3.8 Priority: story and game design first.** His words: "i want these agents to be prioritized based on their role, story game design, honestly every aspect matters so so so much." Every department matters; the ranking says three things only: (a) who works first when a session's list is drawn up, (b) whose call stands when two leads disagree inside a task, until Mansoor rules, (c) whose output the others build on. The ranks are in §9.
- A higher rank never overrides a lower rank on that lead's own craft: the Director decides what a scene must *do*; Art Direction decides how it *looks* within that; Systems decides what it *costs*; QA can flag anything and decides nothing.
- **Any lead can stop the line on a lock (§2) or the shippability redirect (§4).** Rank doesn't protect a proposal that breaks a lock.

**3.9 Parallel work, one editor.** His words: "i was thinking of doing this while continuing our work for better streamline." Research, design, writing, review and Blender work (models by script, in the cloud) may run in parallel. Anything that touches the Unity project's scenes, prefabs or the same scripts runs **one lead at a time**, because there is one editor on one Mac, the labs need Unity in front, and two leads changing one scene is how work gets lost. The Producer's list says which items are parallel and which are serialized.

---

## 4. Ways of working (how Mansoor wants every session run)

- **Discuss before building.** "I want everything to be first discussed about before coding or doing any type of work so that both our ideas are one plane." When he asks to discuss, nothing is built. Mechanism first, code second.
- **Once a plan is agreed, build straight through.** His 7 Oct rule for build sessions: no stops for approval mid-session; Claude makes the visual calls and lists them as *Decisions his to overrule*; he judges at the playtest. A short report after each session.
- **Complete files, never diffs.** Partial edits are hard to follow in Unity. A changed script is delivered whole.
- **One ordered list of next steps**, not parallel threads for him to manage. Narrow scope when too many workstreams are open.
- **Explain the reasoning.** Every recommendation carries its why, the alternatives considered, and what was rejected. Be extremely thorough in reasoning and in step-by-step guidance.
- **Debate, don't agree.** He prefers pushback to agreement. Say when he is wrong and why. Then do what he decides.
- **Evidence for direction.** Verdicts about the game's direction are backed by market research and statistics (what sold, and why), with sources, not opinion alone.
- **Step-by-step with Unity open.** Unity steps are numbered, name the exact menu, window, field and value, and say what he should see when the step worked.
- **Blender sessions and Unity sessions are separate.** Anything needing Blender (models, furniture, characters, atmosphere) is its own session; Unity sessions are code and mechanics.
- **Commits.** On the Mac, Claude commits with his name (`git -c user.name="Mansoor Qaiser" -c user.email="q.mansoor123@gmail.com" commit …`); he pushes with GitHub Desktop. Do not nag him to merge branches. Do remind him to push.
- **Sync.** At the start of every session: Fetch/Pull before any work. At the end: Commit + Push. He works on two machines (Windows PC `D:\FixitFidget`, the build/shipping machine; Apple Silicon MacBook Pro `~/Documents/FixitFidget`), meets on GitHub, and switches often.
- **Reorient him at the start of a session.** He loses track of his own progress between sessions; the audit is the first thing he sees.
- **Keep redirecting toward shippability.** His own pattern: expressive, enjoyable structural work pulls ahead of necessary unblocking work (content, audio, economy). Name it when it happens.
- **He plays and judges feel.** Agents cannot press Play, see the Game view, hear audio or judge "wow." His playtest notes are the ground truth, kept in his words (`claude/playtest-*-notes.md`); the labs are the next best thing.
- **Past Claude errors he caught, never repeat:** asserting a first-person-only camera architecture; flagging intentional design (Grace's latte suppression) as a bug; writing code when only discussion was requested; building blocky stand-ins and calling them done; smooth-shaded, glossy "kid toy" hero models next to a flat-shaded warm café; a phone's front glass 13 mm thick ("doesn't look real"); a crack drawn as a texture instead of looking cracked; geometry outside the body that read as a SIM card on the phone's back; a tool built as if it had a point (the cloth buried in the caddy); a café counter made of one module repeated; lines where words should be on signs.

**4.1 The studio's rules from 14 October (binding).** Written after playtest 5 ("the playtest couldn't even get passed day 1 for me, so how could i sell that to anyone else"), and approved with his calls that morning:
1. **Reference before work.** A lead whose task changes how the game feels studies real footage or sources first (for the bench: how ReStory actually plays, from video, frame by frame). Playtesters research the reference game before they judge ours.
2. **Feel targets before building.** The Producer writes numbered targets from the reference (`T1…`), and every build and lab works to them (`feel/feel-targets-<date>.md` in the session's folder; the Project's `claude/playtest-5-notes-and-plan.md`).
3. **A clumsy hand in the labs.** The bench lab (Bench 8) plays the repairs through the real input path with a human-speed cursor (reaction, overshoot, jitter, near-misses, early releases, a wrong tool now and then), on mouse and pad, at 60 fps and at a jittered 15 fps. If it can't finish Day 1's and Day 2's repairs inside their bars, the build isn't ready, whatever else is green.
4. **Playtesters stop grading feel from photos.** They report risks for his hands and analyse his recordings.
5. **His playtests are recorded** (screen and voice), and the agents timestamp every struggle.
6. **Nobody helps a playtester** while they play.
7. **No physics in the core loop** (also a lock, §2).
8. **Labs prove what he would see, at his frame rate** (15–20 fps on the Mac with Unity in the background), not a proxy.
Also from that session: specs are written in parallel and checked by a QA read; builders write code in parallel in separate worktrees, each owning its files, against written contracts; a separate reviewer reads each builder's code before it reaches the Mac; one integrator merges; the Producer alone runs the Mac.

---

## 5. The output contract (every lead, every deliverable)

Return, in this order:

1. **What I did** — two to five lines. What was asked, what was produced, what was not done and why.
2. **The decision and its reasoning** — the recommendation, the why, the alternatives considered and why each was rejected. For direction calls: evidence with sources.
3. **Decisions his to overrule** — every choice made to finish the task that Mansoor did not explicitly make.
4. **The deliverable** — complete files, each with its full repo path, never diffs. Specs, drafts and research as complete documents.
5. **Unity steps** (when anything touches the editor) — numbered, exact, with what he should see.
6. **Checks** — which labs or editor checks (`Fixit Fidget ›` menu) prove it, and what a pass looks like; or what to play and watch for.
7. **Open questions** — the fewest possible, each one a decision only he can make.
8. **Flags** — any line in this Bible or in a locked decision the task brushed against; any new mechanic smuggled in by the request; any contradiction between documents you noticed.

Style: plain words, short sentences, no filler, no praise, no restating the request. Numbers over adjectives. Write for someone reading on a phone at 2 a.m.

---

## 6. Repo map (branch `playtest-2`)

```
Assets/Scripts/            229 runtime scripts, flat (plus Scripts/Diagnostics, 22: the labs)
Assets/Editor/             148 editor scripts: build steps and labs under the menu  Fixit Fidget › …
Assets/Data/               Days/ (Day_01…05 definitions), Regulars/ (Grace, Officer), NpcProfiles/,
                           HumanFaults/, Reputation/ (ReviewLines), Resources/ (Night lines, Bench outline shader)
Assets/AssetsPrefabs/      device prefabs (PhoneRepair …), Drinks/ (Drink_*.asset: names and prices)
Assets/Playtests/AcesCafeLayoutPlaytest.unity   THE café scene (café + district); Playtests/AcesCafeLayout/ its assets
Assets/Scenes/SampleScene.unity                 the older scene, second in the build list
Assets/Art/Models/         BenchTools/, Devices/, GraceHouse/, HouseKit/, Night/   (FBX + manifests)
Assets/Art/Materials/      URP Lit; the warm-grey palette families (CC_, DC_, T2_, GH_, BT_, HD_)
Assets/Audio/              Dispenser/ (two WAVs) and CC0/ (14 Oct: 96 CC0 files for 45 cues, manifest.json, licences/);
                           a cue with no file falls back to PlaceholderSounds, made in code
Tools/Blender/             31 parametric model scripts (hero_lib.py, bench_tools.py, hero_*.py, house_kit.py,
                           grace_house*.py, export_*.py, review_*.py); README.md lists them
BlenderSource/             the .blend sources (LFS)
Tests/                     standalone console test projects: CircuitRules, HumanRules, HoldCallRules,
                           ContinuationRules, StorytellerRules, GraceRules
Docs/ProjectDirection/     Current-Handoff-2026-09-12.md (the running handoff, newest entry first),
                           Feedback-and-Roadmap-2026-09-12.md, Visual-Style-Lock-01-Character-Guide.md, City-Asset-Setup.md
Docs/Studio/               this Bible; sessions/<date>.md (the Producer's notes)
Docs/Studio/agents/        the studio roster (15 leads, 2 playtesters), the copy every session edits; installed into
                           .claude/agents/ on each machine, where Claude Code loads it (§10; that folder is not committed)
CLAUDE.md                  the short version of this Bible, loaded by Claude Code in this repo
DayLogs/                   telemetry CSVs per day with summaries
Logs/                      lab photos and reports (local, not committed); Logs/Compile/last.txt, the last compile
*.md at the root           per-feature review and playtest checklists; roadmap-progress.md, continuation-checkpoint.md
Claude outputs/            review images that came in with a sync; his to keep or drop
_to_delete/                leftovers set aside on the Mac; should not be committed
```

Ignored, licensed, copied by hand between machines: `Assets/Synty`, `Art/Mixamo`, `Art/CityNeighbors`, `DownloadCache`, `ThirdParty/Quaternius_UAL/Humanoid` (`claude/mac-licensed-assets-handoff.md`).

Engine: Unity 6000.5.2f1, URP (Forward+ on PC), Cinemachine 3.1.7, Input System 1.19, AI Navigation 2.0.13, TextMesh Pro. Blender 5.2, headless scripts (or `bpy` as a Python module in the cloud). Git via GitHub Desktop with LFS.

**Conventions found in the code:** comments explain *why*, not what, and quote his note when a change answers one; a lab (`*Lab`, `PlayLab` base) drives itself with a virtual pad and photographs its checks into `Logs/`; a step (`*Steps.cs`) builds or changes the scene from a menu item, records what it did, and has an Undo; a step photographs before and after; dialogue lines aim for 90 characters and never exceed 150 (`Fixit Fidget › Checks › Dialogue rules`); a tool's origin is its working tip; Unity (x, y, z) = Blender (−x, z, −y); `Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)` after files change on disk, never during Play mode.

**The bench's conventions (14 Oct).**
- **One scale.** Every device is real size (`DeviceDefinition.realSize`, set by **Bench › Devices 2**, which bakes a prefab once and refuses to bake it twice); Blender exports real size (`hero_lib.scale_build`, `AUTHORED_SCALE` per script; `devices.json` says `"units": "real"`). Screws are 3.8 mm heads; a hole is a decal (`Resources/Bench/BT_ScrewHole`, 1.15 × the head, a bore of 0.48 × the head). Carried devices are drawn at least 0.45 m long from above (`PlayerCarry`).
- **The stage** lives under `InspectRig/Stage (v2)` (**Bench 7**): the mat `BM_RepairMat` built in the rig's own frame with empties `Cup_1..6`, `Pad_Small/Large`, `Well_Small/Large`; the rest `BT_ToolRest` (`Seat_*`); the scrap tray `BT_ScrapTray` (`Slot_1..3`). Positions are mat millimetres through `BenchStage.MatToWorld`; each device's place is its `BenchLayout` (rest, tool rest, scrap tray; the close-up's aim, distance, `closeAim`, `fovClose`, written by **Bench 9**).
- **Nothing at the bench is a rigidbody.** Every freed piece flies by `BenchStage.Fly` (a closed-form arc from game time, its launch cue on the first frame and its landing cue on the last); the guard snaps back and logs an error for any piece found away from its slot, and every lab fails on that error.
- **Hit areas are picked in screen space** (`BenchRules`: 28 px for screws and holes; the outline plus 12–16 px for the rest; the current step's targets first).
- **The bench labs:** Bench 3 the play check, Bench 5 the close-up's view, Bench 6 the words on screen, Bench 7 the stage step, Bench 8 the clumsy hand, Bench 9 the framing; **Checks › Bench rules** the pure maths (also `Tests/BenchRules`).
- **Sounds are called by Audio's cue names** (`SoundBank.Defaults`, filled from `Assets/Audio/CC0/manifest.json` by **Sound › Sound bank 1**, checked by **Sound bank 2**).

---

## 7. Current state — 15 October 2026, 19:30 Chicago (the Producer re-verifies this at the start of every session)

- **Live branch: `playtest-2`.** His last commit is `fab65d3` (13 Oct). Today's commit on his Mac, under his name, carries the 14 Oct build (playtest 5's answer, "the forgiving bench"), the 15 Oct fixes and both days' notes: **his push (GitHub Desktop) is next.** The cloud's working branch is `integ`; the Mac's tree is what ships.
- **Built 14 Oct, from playtest 5** (neither he nor his girlfriend got past Day 1; his notes verbatim in `claude/playtest-5-notes-and-plan.md` §1; his fifteen calls in §2, delegated: "i trust your judgement to do the best option, the option that is closest to my request"): ReStory studied from footage first (`2026-10-14/feel-sheet-2026-10-14.md`), the feel targets T1–T21 and the Producer's 23 calls (`feel-targets-2026-10-14.md`), nine specs, seven builders, three code reviews (`Docs/Studio/sessions/2026-10-14/`). **The bench:** nothing is a rigidbody (§6); a screw flies to its cup; 3.8 mm heads in small threaded holes; the cloth and the brush grabbed and dragged, in hand over the device, home by themselves after their last spot; the swap one press; the checklist a small ticket with tool icons that tick; the slate-blue repair mat with rulers, cups and a parts well. **The days:** 420 s each; Days 1–3 an intro level, one new job type a day; Grace's camera in two visits (Day 1 the clean, Day 2 the shutter and her latte, Day 3 the photo). **The café:** the repair-bar stools taken first by bench customers, settled guests on Days 1–3, the room dressed. **The day intro** draws from street B-roll, Grace's house on her mornings only. **The night zoom** eases, never snaps. **The dispenser** framed square. **Sounds:** a recorded CC0 bank on every bench action (48 voices). **The playtest journal** (https://claude.ai/artifact/Mk8oWoBSKLtJvoGdobbPwK).
- **Run on his Mac, 15 Oct** (Unity 6000.5.2f1; lab saves only, his save untouched; `sessions/2026-10-15.md`): five compile fixes for the new Unity; Phase A's steps and Phase B's checks all passing; the repair mat settled onto the bench top (it sat 6 mm inside it). **Bench 8, the clumsy hand, gates the build and passes:** 149/154, every run Perfect within its bar, 0 lost; with a pad Grace's film path went from 161 s to 11.6 s (no aim help mid-scrub, a pad's rub reaches 1.35×, the snap never undoes a push). The stools' sit no longer sinks (`NpcSeating.LiftWhileSitting`). Day 1's intro bench shot from Ace's side (it looked through the frosted guard).
- **Day 1 played by hand (15 Oct evening)** found what no lab could, all fixed and checked: **E hands a finished device back over the bench from wherever the bench is in Ace's reach** (it worked only within 2 m of the bench's stand point, where every lab stood him; a player stops at the till's end of it, and Grace left "Never again" with her fixed camera in his hands: `PlayerInteractor.BehindTheBench`, the reach over the bench measured from its stand point; the probe 101 → 181 of 181 places); **nobody walks out on a device in Ace's hands** (patience still drains, the tip and "Loved it" still pay speed, but stops at 10 % of the bar while it's in the close-up or carried: `CustomerBrain.HandsOnFloor`); the drinks card says "You're at it" only when E is the close-up.
- **The labs (15 Oct):** Bench 3 191/191; Bench 5 147/147; Bench 6 109/109 (1920 × 1080 and 2560 × 1600); Bench 8 149/154 (the gate); Stations 96/96 and 95/95 (and again after the bench's reach: 96/96, 95/95); Aim help 26/26; Juice 41/41; Day 1 guide 77/77 (and again: 77/77); Night zoom 34/34; Controller 29/29; Life 4 51/51; Sit 4 16/16; the dispenser's frame 14/14; Day intro 191/191; Days 1/2/3 19/20, 16/20, 19/19 with **0 storm-outs** on all three; Life 2 15/15; Life 3 23/25; Attention 10/10; Café life 31/32; Night 0 96/96; Night 1 77/77; Night 2 115/115; Grace at home 133/133; the bench's hand-over probe PASS; Hands on it 12/12; rules (Bark, Reputation, Night 1, Grace at home, Notebook, Bench) PASS. **Not run on 15 Oct:** the door rushes, Mixamo 5, the night notebook and the recap phone in Play, Night 1's cracking, Grace's window and Thursday.
- **Open, named:** the cloth still reads as a yellow lump (Art: his "open up and be used like a cloth"); the café looks sparse from the overhead camera (T21); the conversation-to-overhead blend passes through the café's wall (a moment of street); the magnifier lamp's ring of shadow over the close-up; the "In hand" line names the fault after "Fixed!"; the patrons' relaxed walk (Life 3: 5 of 6) and a look at "Fixed!" (0 of 1); Café life's walk-clip flicker (11 in 16.6 min, likely a long session's leftovers); the pad's strokes per spot (3–4 against 3); two parked intro shots (`car.pass`, `street.across`); Day 2 ran to 7:56 serving the two inside at closing; Mixamo 5 was 47/52 on 13 Oct (a shoe 2.5 cm into the floor on the tub chairs).
- **Next, in order:** (1) **his push** (GitHub Desktop); (2) **playtest 6**, recorded: him, his girlfriend, one stranger, each on a fresh Day 1 (`sessions/2026-10-14/playtest-6-instructions.md`, updated 15 Oct), the journal after each; the scope stays frozen until someone who isn't him finishes Day 3 and wants Day 4; (3) the cloth as a cloth; (4) the café full from above; (5) the open items above, after playtest 6 says which matter.
- **Awaiting his verdict:** the calls of 14 Oct (the feel targets' 23) and of 15 Oct (the session notes: the pad's reach, the cloth's offset, the stools' lift, Day 1's bench shot, the parked shots, the hands-on floor, the bench as one work place). Older: the café as built (the redesign's calls, 10 Oct) and the fix leads' calls; the skylights at night; goosenecks over the fascia; the devices' real sizes; Baloo 2 for the UI font.
- **Lagging, named for weeks:** the sounds by ear (a recorded bank exists; nobody has listened on his speakers but him: playtest 6's step 1); the UI font (LiberationSans); every new line since 10 Oct is placeholder copy for his sister; the night's content after Night 2 (Night 3's cones); GDD v4 not consolidated.
- **Story foundation:** chosen 5 October (§2). Night 0, Night 1, the favours, stalling, the officer, Grace at home (her window now) are built and lab-checked. Night 3's cones are not built. GDD v4 is not consolidated (the Project holds `FixitFiasco_GDD_v3.md` and `claude/scope-addendum-v3.1.md`). **Mansoor: if a v4/v4.1 file exists, add it to the Project before the Narrative Director consolidates.**
- **Hygiene:** the Mac's shell has git and git-lfs in `$HOME/bin` (not on the default PATH) and no GitHub credentials (he pushes with GitHub Desktop); its git lacks his LFS filter settings, so a commit from the shell passes them (`-c filter.lfs.clean=… smudge=… process=… required=true`) and is checked for LFS pointers before he pushes; commits from the shell leave `.git/*.lock` files it can delete only with his permission (asked once per session). **Copies to the Mac go under unique names and are checked by md5 on arrival** (the bridge caches by file name: `/root/cafe/tools/dep.sh` in the cloud), and only in Edit mode. **Labs run only with Unity in front** (runInBackground is off: a lab started behind another window runs nothing for minutes). Most `PlayLab` labs leave Play mode on; the Day 1 guide, the day intro, the night labs and Hands on it end it themselves: read the state before toggling Play or reimporting (a reimport during Play reloads the domain under it). A changed default for a field the scene already holds keeps its old value in the open scene until it reloads. Labs that measure one thing for a long time (the door rushes, the attention check, Life 2) want a fresh session. The Synty package helper dialog appears after asset changes (Skip, then OK). `Logs/_pack2/` (309 MB) and `Logs/_deploy/` are disposable; `_to_delete/`, `Claude outputs/` and `Assets/_Recovery/` are in the repo.
- **Rules the Producer learned (15 Oct), for every lead:** a check that depends on where Ace stands maps every place a player can stand, not the designer's one point (the probe pattern); before changing how a number moves, read everything that reads it (the tip reads patience); play the day by hand, slowly, before calling a build ready: the labs drive the game through its own calls and never fumble.
- **The Mac runs the game badly**; by his instruction nobody fixates on that. The labs can run at 15–20 fps there (Bench 8 runs every case at 15 fps on purpose): anything stepped per frame (a spring, a raster of cursor jumps) must be written for that.
- **Corrections to the Project memory notes:** the reputation mechanic is *built* on the day side (`ReputationLedger`, `ReputationRules`, reviews on the phone), not parked; the recap is Ace's phone; F is the night torch, not a station key; the `PlayerCarryHUD` overlay is retired for physical hands; stations are reach, not docking.

---

## 8. Document map (what each role reads first)

All `claude/*` paths are Project docs. Repo paths are as in §6.

**Foundation and story**
- `claude/foundation-the-fixers-notebook.md` — the foundation question (why Ace), answered 5 Oct
- `claude/the-man-at-the-bins-story.md` — the man, the deal, the arc
- `claude/night-0-and-the-favours-spec.md`, `claude/session-3-favours-stalling-officer.md` — the favours, stalling, the officer
- `claude/ace-after-dark.md`, `claude/night-city-proposal.md`, `claude/night-1-slice.md`, `claude/night-homes-spec.md`, `claude/night-notebook-spec.md` — the night, in the order it was designed
- `claude/break-ins-spec.md`, `claude/break-ins-chunk-a-handoff.md`, `claude/chunk-c-grace-at-home-plan.md`, `claude/house-interiors-plan.md` — break-ins and houses
- `claude/franchise-ready-cafe.md` — the copyable café, the franchise calls
- `claude/reputation-spec.md` — stars, reviews, the chapter's end condition
- `FixitFiasco_GDD_v3.md`, `claude/scope-addendum-v3.1.md` — the last written GDD and its addendum
- `claude/foundation-pass-build-plan.md` — how the foundation was to be built

**Playtests and feel (his verdicts, in his words)**
- `claude/session-2026-09-30-playtest-2-notes.md`, `claude/playtest-2-plan.md`
- `claude/playtest-3-notes-and-plan.md`, `claude/playtest-3-sessions-2-6-plan.md`, `claude/playtest-4-notes.md`
- `claude/session-2026-10-0[5-9]*-notes.md` — the daily thinking, 5–9 October (on 9 Oct: morning, afternoon, evening, night, late)
- `Docs/ProjectDirection/Feedback-and-Roadmap-2026-09-12.md` — the 17 and 22 Sept identity assessments and vertical-slice roadmap
- repo root `*-playtest.md`, `*-review.md`, `*-checklist.md` — per-feature acceptance records

**Dialogue and characters**
- `claude/dialogue-skyrim-proposal.md` — the dialogue pass: subtitles, short lines, Ace's replies
- `claude/npc-behavior-map.md`, `claude/npc-life-plan.md`, `claude/npc-pass1-movement-results-2026-09-26.md`, `claude/npc-pass2-life-results-2026-09-27.md`, `claude/cafe-npc-observations-2026-09-26.md`
- `claude/mixamo-clips-batch1.md` — the animation clips in use
- `Docs/ProjectDirection/Visual-Style-Lock-01-Character-Guide.md`
- code: `DialogueSet.cs`, `Barks.cs`, `BarkRules.cs`, `NightLines.cs`, `ReviewLines.cs`, `CustomerArchetype.cs`, `CustomerStoryteller.cs`, `CustomerMemoryService.cs`, `Assets/Data/Regulars/*`, `Assets/Data/NpcProfiles/*`

**Art, models and environment**
- `claude/art-style-guide.md` — the rules (chunky, flat-shaded, warm-grey palette, one-segment bevels)
- `claude/hero-models-spec.md`, `claude/hero-reference-sheet.md`, `claude/session-2026-10-08-hero-notes.md` — the hero standard, its references, how it was reached
- `claude/studio/cafe-sf-research-and-redesign.md` — the San Francisco café research and the proposed redesign (9 Oct; awaiting his answers)
- `claude/furniture-library.md`, `claude/furniture-catalog.md`, `claude/cafe-furnishing-pass.md`, `claude/cafe-realism-pass.md`
- `claude/cafe-architecture-spec.md`, `claude/cafe-step1-unity-setup.md`, `claude/cafe-step2-seating.md`, `claude/cafe-car-park-and-arrivals.md`
- `claude/session-2026-09-23-polygon-city.md`, `claude/session-2026-09-23-neighborhood-refresh.md` — the district
- `claude/unity-import-guide.md`, `claude/mesh-audit-method.md`, `Tools/Blender/README.md`
- `claude/mac-licensed-assets-handoff.md`

**Bench, HUD, sound, camera, systems**
- `claude/playtest-4-findings-and-proposals.md` — his controller playtest of 12 Oct: the bench's view, the pad at the bench, the conversation camera, the replies, the checklist, the day intro, the lived-in café, the NPC life pass
- `claude/bench-spec-v2.md` — **superseded 14 Oct** by `Docs/Studio/sessions/2026-10-14/specs/systems-forgiving-bench.md` and `feel-targets-2026-10-14.md` (its physics-based bench is what playtest 5 couldn't get past; kept for history)
- `claude/hud-spec.md` — the HUD's corners, the one skin
- `claude/sound-plan.md` — the sound plan and the Sonniss pass
- `claude/occupancy-and-pacing.md` — seats, queues, pacing
- `claude/code-audit-2026-08-23.md` — the last code audit
- repo `roadmap-progress.md`, `continuation-checkpoint.md`

---

## 9. The roster

Fifteen leads and two playtesters, one role file each in `Docs/Studio/agents/` (installed into `.claude/agents/`, §10). One definition per department; when a job parallelises, several instances of one lead run with different tasks. Models: **Fable 5.1 for anything big that needs proper thinking, Opus 5.5 for the rest, every one at effort `max`** (§3.7: the role file's `model:` line is the default, the task decides).

| Rank | Role | File | Mission in one line |
|---|---|---|---|
| — | **Producer** (the coordinator) | `producer.md` | Reorient him, propose the one ordered list, guard the locks, run the session loop, write the notes, keep §7 true. Never decides. |
| 1 | **Narrative & Game Design Director** | `narrative-director.md` | The game's spine and its design: why Ace, the day/night loop, the arc, the pacing across days, the franchise ending; GDD v4; evidence-backed direction. The studio's creative lead under Mansoor. |
| 2 | **Systems & Mechanics** | `systems-mechanics.md` | Make every built system right: the bench, the café loop, the night's rules, bugs, labs. No new mechanics. |
| 2 | **Progression & Economy** ("game leveling") | `progression-economy.md` | Money, prices, the upgrade shop, unlocks, the difficulty ramp across days, the onboarding economy, the road to the franchise. Tuning with the arithmetic shown. |
| 2 | **Content & Dialogue** | `content-dialogue.md` | The words: archetypes, lines, barks, night lines, reviews, Grace's stories, the Officer, the man; fault and device content as data. Drafts for his sister. |
| 3 | **Environment & Scene** | `environment-scene.md` | The world in Unity: the café's layout and dressing, the district, the houses, lighting, the night's look; the café kit (`CafeSite`). |
| 3 | **Art Direction** | `art-direction.md` | The look: the style guide, palette, the purpose rule applied to every piece, briefs for the 2D artist, the portrait style, reference boards, the capsule art brief. |
| 3 | **NPC & Characters** | `npc-characters.md` | How people move, wait, sit, react and remember: customers, patrons, regulars, the night's people; animation wiring; the *Peak* feel. |
| 3 | **3D Art & Blender** | `blender-3d.md` | Models by script in the house style, built from measured references: hero props, furniture, the café's pieces, characters' bodies; exports, manifests, review renders. |
| 4 | **UI/UX** | `ui-ux.md` | The HUD, the ticket rail, the phone, the notebook, tooltips, dialogue UI, onboarding readability, the font, accessibility. |
| 4 | **Audio** | `audio.md` | The sound plan made real: the cue list, the Sonniss pass, the mixer, the café's ambience, the night's tension, music direction. |
| 4 | **Cinematics & Camera** (12 Oct) | `cinematics-camera.md` | The camera language of the whole game (the conversation camera, the bench's framing, the views and their blends), the day intro, montages, staged scenes, title timing, the trailer capture with Marketing. Timeline, Cinemachine, the Recorder. |
| 5 | **Technical Director** | `technical-director.md` | The code's and the product's structure: folders and assemblies, performance, save format safety, builds on Windows, Steamworks, platforms, the repo's hygiene. |
| 5 | **QA & Critic** | `qa-critic.md` | Adversarial reads of every big draft, spec, model and build before Mansoor sees it; lab coverage; polish checklists; playtest plans. Deliberately harsh. Reviews from day one. |
| 5 | **Marketing & Steam** | `marketing-steam.md` | Positioning against *PlateUp!*, the hook, the Steam page, tags, capsule brief, trailer beats, devlogs, Next Fest timing, wishlist math. Cited. |
| — | **Playtester: the First Hour** | `playtester-first-hour.md` | Plays as a stranger who bought it an hour ago: does it **read** — what the place is, what to do next, every sign, prompt and HUD element, where a newcomer gets lost, the two-hour refund line. Builds nothing. |
| — | **Playtester: the Genre Veteran** | `playtester-veteran.md` | Plays as a *PlateUp!* / *ReStory* / stealth veteran: does it **feel** right against the three pillars — the bench, the hectic day, the night — with the pacing numbers from the day logs. Builds nothing. |

Rules:
- **Rank decides the order of work and settles disagreements between leads until he rules (§3.8).** It never lets a lead do another lead's job.
- **At most three leads write to the Unity project in one session**, one at a time (§3.9). Research, design, writing and Blender leads can run alongside.
- **QA reads every big or direction-setting deliverable before Mansoor does**, whatever the rank of its author.
- **The playtesters play every build that changes what the player sees or does before Mansoor does**, one at a time in Unity (they drive only the labs, the café lab's autopilot and the recorder; never a step, never a save), and their notes merge into his playtest notes. They are outside the ranks: they outrank no lead and block nothing; a lead decides what to do with their findings. Their method, and its honest limits, are in `Docs/Studio/PLAYTEST-PROTOCOL.md`: they judge from frames, logs and words, and mark what only his hands can settle.
- A lead that needs another department's output says so in *Open questions* instead of doing that department's job badly.
- The Producer can run several instances of one lead (fan-out) for parallel tasks: one archetype each, one house each, one model each. The lead's own instance reads all the drafts for one voice before anything reaches Mansoor.

---

## 10. The session loop

1. **He syncs.** Fetch/Pull on the machine he sat down at. (The Producer asks; this is the one nag that is welcome.)
2. **Producer: reorientation audit.** Reads the repo handoff's newest entry, the last session notes, `git log`, and this file's §7; writes what changed since he last looked, what is pending, and what is blocking. Updates §7 if the facts moved.
3. **Producer: the one ordered list.** Proposes which leads run this session, in rank order unless there's a reason not to, with the reason for each, marking which items run in parallel and which touch Unity (serialized), honouring §2's no-new-mechanics rule and §4's shippability redirect. He approves, reorders or rejects.
4. **Leads run.** In parallel where independent. Each returns the §5 contract.
5. **The playtesters play** the integrated build (after step 7 when the build changed what the player sees or does: `playtester-first-hour`, then `playtester-veteran`, one at a time in Unity), and their notes go into his playtest notes before he plays. **QA reads** anything large or direction-setting before he does.
6. **Discussion.** Anything that changes direction goes to him as a discussion first (§4), with the reasoning, the alternatives and the open questions.
7. **He integrates, or Claude does on his Mac**, with numbered Unity steps and the labs that prove it.
8. **Producer: session notes** to `Docs/Studio/sessions/<date>.md` (repo) and `claude/session-<date>-notes.md` (Project): what was done, what he decided, what is next, what each lead got wrong (→ its corrections log).
9. **Commit, and he pushes.** The Producer asks.

**Where things are stored.** Canonical: the repo (`CLAUDE.md`, `Docs/Studio/`: the Bible, the roster in `Docs/Studio/agents/`, the session notes), so both machines and a cloud session read the same branch.

**Installing the roster** (once per machine, and again after a pull that changed `Docs/Studio/agents/`). Claude Code loads subagents from `.claude/agents/`; a cloud session isn't allowed to write into a `.claude` folder on his machine, so the roster lives in `Docs/Studio/agents/` and he copies it in. From the repo folder:
- Mac (Terminal): `mkdir -p .claude/agents && cp Docs/Studio/agents/*.md .claude/agents/`
- Windows (PowerShell): `New-Item -ItemType Directory -Force .claude\agents; Copy-Item Docs\Studio\agents\*.md .claude\agents\`
`.claude/agents/` is in `.gitignore`: the repo carries one copy of the roster, not two. Outputs for his review (audits, drafts, research, GDD v4): the Project docs, so they open on his phone; the studio's documents live under `claude/studio/`. Learning: each role file's *Corrections log*, appended by the Producer from his verdicts; nothing a lead "learns" persists otherwise. (Claude Code also offers `memory: project` in a role file's frontmatter for a per-lead memory; an optional upgrade once the corrections logs prove too slow.)

---

## 11. Glossary (so leads use his words)

Ace (player) · Grace (the regular) · the Officer · the man / the man at the bins (the hitman) · Barnaby (the gnome) · the deal · a favour (an errand for the man) · the notebook (Ace's night notes) · the straight face / the meter (lying to Grace) · Night 0 / Night 1 · a bin night · the recap phone / the pause phone · the ticket rail · the tabs · the cash stack · the hanging sign · the bench, the counter, the drinks station (the three-station spine) · the intake shelf (where accepted devices wait, on the counter left of the till) · the close-up (first-person station view) · the overhead (isometric view) · a lab (a self-driving play check) · a step (a menu item that builds the scene) · the hero set (tools and devices) · the stage (the bench's mat, tray and caddy) · the house kit · the warm-grey palette · a walk-in / a patron / a regular · the district (the SF chapter) · the copyable café / `CafeSite` · the content pass · the purpose rule · the repair bar (proposed, 9 Oct: the bench in the counter, facing the room) · playtest 4 · Pome (the phone's maker) · 4a / 4b / session 5 / session 6 (the bench and café roadmap).
