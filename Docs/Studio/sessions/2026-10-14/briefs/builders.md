# Builders' brief — 14 Oct 2026, wave 2: turn the specs into code (read in full before you touch a file)

*From the Producer. You are one of seven builders working at the same time, each in your own git worktree with your own files. The Producer alone integrates on his Mac, compiles in Unity and runs the labs. You never touch the Mac, Unity or `/root/cafe/repo`.*

## 1. Read first, in this order
1. `/root/cafe/brief/common.md` (the whole session's brief: his notes, decisions, locks, rules).
2. **`/root/cafe/feel/feel-targets-2026-10-14.md` — binding.** The feel targets T1–T21 your code must meet, the Producer's calls on every open question (they override the specs where they differ: e.g. screws are **3.8 mm**, the cloth stays in hand when let go over the device, the first-repair card is ≤ 25 words), and §3, **the field names every builder uses** so parallel work meets.
3. **`/root/cafe/brief/contracts.md` — binding.** The exact signatures where your code meets another builder's (DeviceDefinition, BenchStage, BenchStageContract, BenchWork, RepairChecklist.Step, ItemInspector, PatronSpawner, the imported files' paths).
4. Your spec(s) in `/root/cafe/specs/` (named in your prompt), in full. Skim the others' "What he will see" and any section your prompt points to.
5. The Bible's §2 (locks), §6 (repo map, conventions) and §7 (hygiene lessons): `<your worktree>/Docs/Studio/BIBLE.md`.
6. Every file you change, in full, before you change it. Then its callers (`grep -rn`).

## 2. Where you work
- **Your worktree** (a full checkout of `playtest-2` at the Mac's head) is named in your prompt: `/root/cafe/build/<name>/`, branch `b-<name>`. Edit only there. Commit there as you go, small commits with plain messages: `git -C <worktree> add <your files> && git -C <worktree> commit -m "…"` (author: keep the default). Never `push`, never touch other worktrees or `/root/cafe/repo`.
- **You own only the files your prompt lists** (plus new files you create in the folders it names). For any change another owner's file needs, write it under **"Patches for other owners"** in your report as an exact before/after block (file, the unique old text, the new text). The Producer applies them. Never edit a file you don't own, even "just one line".
- **No git command that rewrites the tree** (`checkout --`, `reset`, `stash`, `clean`, `restore`).

## 3. How the code must be written
- **Unity 6000.5.2f1, C# 9, URP, Cinemachine 3.1.7 (`Unity.Cinemachine`, `CinemachineCamera`), Input System 1.19.** Use only APIs you can see used elsewhere in the repo or that exist in Unity 6 (`Rigidbody.linearVelocity`, `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`, `FindFirstObjectByType`, …). When unsure of an API, copy the pattern from the repo. **Nobody can compile in the cloud**: every compile error costs the Producer a round trip on his Mac, so read signatures, match types, and check every member you call exists (`grep`).
- **Syntax check every `.cs` you change:** `python3 /root/cafe/tools/cs_syntax_check.py <files>` (tree-sitter; it parses only, no types; contextual keywords used as names, like a variable called `with` or `required`, can show as false ERRORs). Fix every real error.
- **Frame-rate independence** (rule 8): his Mac runs at 15–20 fps. Motion in closed form from elapsed time, never a stepped spring; nothing that depends on a frame landing inside a window; input edges read per frame. Flights use game time and never pause the clock (lock).
- **No physics in the core loop** (rule 7). No rigidbody on anything the player must rely on.
- **Serialized fields** (Bible §7): a new default never reaches a field the scene or a prefab already holds; if you change a field's meaning, rename it (and use `[FormerlySerializedAs]` only when the old value should carry over). Scene and prefab changes are made by **editor steps** (`Assets/Editor/*Steps.cs`, a menu item under `Fixit Fidget › …`), never by editing `.unity`/`.prefab` YAML by hand — except a data `.asset` whose fields you read in full (Day assets), where a careful YAML edit is allowed and must be listed.
- **An editor step** records what it did (to the Console and a report under `Logs/…`), groups its work under one Undo, is idempotent (a second run says "already done" and changes nothing), photographs before and after when it changes what he sees, saves the scene or prefab, and refuses to run in Play mode.
- **A lab** follows the repo's `PlayLab` pattern (virtual pad, photos and a report in `Logs/…`, a menu item). Write its timings generous: they are guesses until the Mac runs them (13 Oct lesson). Labs must measure what he would see at his frame rate (rule 8).
- **Save data:** never add, remove or change a save field (lock). If you think you must, stop and report it.
- **Comments** explain why, and quote his note when a change answers one (Bible §6).
- **Names are contracts** (Bible §6): upgrade asset names, the empties' names, cue names (Audio's table, exactly), the scene's object names other code finds by name (`grep` before renaming anything).

## 4. Your report (your final message, and the file `/root/cafe/build/reports/<name>.md`)
1. **What changed:** every file (new / changed / deleted) with one line each; the commits.
2. **The Producer's Unity steps, in order:** each menu item to run after the compile, what it should print, what to look for. Mark any step that must run with the scene open, and any that must never run twice.
3. **The labs and checks** to run, in order, with their pass lines.
4. **Deviations from the spec** and why; anything in the spec you found wrong against the code (file:line).
5. **Patches for other owners** (exact before/after).
6. **Risks:** what is most likely to fail to compile (the APIs you were least sure of), and what is most likely to misbehave at 15 fps.
7. **Open items** you didn't finish, in order of importance.
Keep it under 2,500 words; the code is the deliverable.

## 5. Time
About 2–3 hours. Build the spec's core first, its labs second, polish never. If you run short, finish what compiles cleanly and list the rest under Open items. Don't stop early to ask questions: decide (closest to his notes), say so under Deviations, and carry on.
