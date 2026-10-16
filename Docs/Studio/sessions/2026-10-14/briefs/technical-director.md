# Brief — Technical Director (Fable): one scale at the bench

## Read first
1. `~/cafe/brief/common.md` (all).
2. Your role file `~/cafe/repo/Docs/Studio/agents/technical-director.md`; the Bible §2, §4, §6.
3. Project docs: `claude/hero-models-spec.md`, `claude/hero-reference-sheet.md` (the measured real sizes), `claude/bench-spec-v2.md` §2.5 and §3, `claude/unity-import-guide.md`.
4. The Blender scripts that build the devices and tools: `~/cafe/repo/Tools/Blender/hero_phone.py`, `hero_watch.py`, `hero_camera.py`, `hero_lib.py`, `bench_tools.py`, `export_hero_devices.py`, `export_bench_tools.py`, and the manifests under `Assets/Art/Models/*/`.
5. The Unity side: the three device prefabs (`Assets/AssetsPrefabs/PhoneRepair.prefab`, `PocketWatch.prefab`, `Assets/GraceShowcase/GraceReunionCamera.prefab`), `Assets/Editor/HeroDeviceSteps.cs`, `BlenderKitSteps.cs`, `Playtest3Session4Steps.cs` (builds the mat, tray, caddy), `Playtest4BenchSteps.cs`, and every script that reads a size or a distance at the bench: `ItemInspector.cs` (framing, zoom, `DeviceBounds`), `BenchStage.cs`, `Screw.cs`, `RemovablePart.cs`, `ReplaceablePart.cs`, `GrimeSpot.cs`, `ToolPickup.cs`, `DeviceWake.cs`, `Juice.cs`; and where devices exist in the café (the intake shelf, carried in Ace's hands, `PlayerCarry.cs`, `IntakeShelf.cs`, `ItemSlotArea.cs`), and whether anything about a device's state is saved (`SaveData.cs`; Day 1's intent says "Preserve partial outcomes").

## The job
His call: devices, screws, holes, tools, tray and mat **in real proportion at the bench**, with the close-up filling the frame with the device; the café's overhead read must not suffer. Write the method before anything moves.

1. **The scale audit** — one table: every device, every part with a gameplay role (screws, covers, the replaceable parts, the grime/smudge patches), every tool, the mat, the tray, the caddy: current size in the game, the real size (from the reference sheet or a cited source), the factor, and **where that size lives** (a Blender script constant, a mesh, a prefab transform, a collider, a code constant). Confirm or correct the common brief's numbers (phone 1×, camera 2×, watch 3×, tools real).
2. **Options, compared:** (a) re-author the devices at real size (scripts, exports, prefabs, colliders, patches, seats); (b) keep the café's device as it is and **scale the device root only while it is at the bench** (a scale change hidden by the cut to the close-up), so the shelf and Ace's hands keep today's readable size; (c) **scale the bench rig per device** (mat, tray, tools, camera distance) so proportions are real in the close-up without touching devices; (d) anything better. For each: what changes, what breaks, cost in sessions, risks (physics — note the forgiving bench removes physics from the core loop — `lossyScale` reads, grime masks' texel density, the tools' grip poses, lighting/shadow bias at small scales, the close-up camera's near clip), and how it would be proven.
3. **Your recommendation**, with the reasoning and why each other option lost.
4. **Real screw and hole sizes** for each device at bench scale, with the screen math: at the recommended framing, how many pixels wide is a screw head and a hole at 1080p and 1440p? (The forgiving bench gives every target a generous invisible hit area; the visual must still read as a screw, not a dot.) Recommend sizes that read and are true enough.
5. **Close-up framing per device:** lens/distance so the device fills about 60 % of the frame's height or width (say which), with the mat's compartments in view; zoom limits.
6. **The stage plan:** stage by stage, what moves, which labs/checks prove it (name them), what Blender must re-export (if anything), the order relative to the Systems lead's forgiving-bench changes (who goes first and why), and the sessions.
7. **What a stranger's PC needs:** anything here with a performance cost.

Flags: locks brushed, save-format risks, anything that needs his eye.
Output: `~/cafe/specs/td-one-scale.md`. Your final message is the spec.
