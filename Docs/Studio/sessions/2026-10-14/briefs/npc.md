# Brief — NPC & Characters (Opus): the stools, settled guests, and less "robotic"

## Read first
1. `~/cafe/brief/common.md` (all; scope freeze: no new behaviours beyond this list).
2. Your role file `~/cafe/repo/Docs/Studio/agents/npc-characters.md`; the Bible §2.
3. Project docs: `claude/npc-life-plan.md`, `claude/npc-behavior-map.md`, `claude/mixamo-clips-batch2.md`, `claude/occupancy-and-pacing.md`, `claude/session-2026-10-13-notes.md` (the life pass as built), `claude/studio/playtest-veteran-2026-10-13.md` (8 of 10 in the same red shirt; the stools).
4. Code: `WaitingArea.cs`, `WaitingSpot.cs`, `TableSeat.cs`, `NpcSeating.cs`, `CustomerBrain.cs` (seat choice), `PatronBrain.cs`, `PatronSpawner.cs`, `NpcVisualVariants.cs`, `NpcProfileLibrary.cs`, `NpcBeats.cs`, `NpcPosture.cs`, `NpcSocial.cs`, `NpcAttentionDirector` (wherever it is), `CafeArrivals.cs`; the checks `Sit 4`, `WaitingSpaceChecks.cs`, `NpcLifeChecks.cs`; the scene file (grep the repair-bar stools: their WaitingSpot/TableSeat settings, Snap To Seat, positions); `Assets/Data/NpcProfiles/*`.

## The job
1. **The repair-bar stools.** His girlfriend: "people wouldn't sit down on the bar stools that are infront of the repair station." Find the cause with file:line evidence (why two of three are never chosen; whether anyone can sit on any of them; Snap To Seat off; a stool claimed only once the customer has arrived, `WaitingArea.HasArrived`; the Stations lab sat people on the east stool only). Propose the fix: seat choice, and **how a stool sitter looks until the perch clip exists** (Mixamo batch 2 is his download, not done): is there an existing clip that reads as sitting on a high stool with a hip offset, or should stools stand-lean, or wait? Pick, with reasoning.
2. **Settled guests on Day 1.** Patrons already in the café when it opens, who never need Ace (reading, a laptop, chatting in a pair), using what exists (`PatronBrain`, laptop patrons, pairs, the reading corner). Config, not new behaviour: what changes in data, how many on Days 1–3, where they sit (not on seats customers need: the guards still pass), when they leave.
3. **Outfit variety.** Count the distinct looks among patrons and customers today (`NpcVisualVariants`, the city bodies' materials, the profiles), find why most wear the same red shirt, and propose the fix within existing assets (palette swaps, the variants that exist).
4. **"It seems robotic"** — his girlfriend on Grace and the people. Within the scope freeze, list the three cheapest changes to existing behaviour that would read least robotic from the overhead camera (e.g. timing jitter on beats, idles that don't sync, look-at targets), each with its file and cost. No new systems.

Deliverables: causes with evidence, the fixes (files, data, values), the checks that must still pass (the café's guards, Sit 4, Life 2/3, Waiting space), what to watch in playtest 6.
Output: `~/cafe/specs/npc-stools-guests-looks.md`. Your final message is the spec.
