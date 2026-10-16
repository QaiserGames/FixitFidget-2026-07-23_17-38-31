# Integrator's brief — 14 Oct 2026, 11:35: one branch, ready for his Mac

*From the Producer. Seven builders wrote code in parallel worktrees; the Blender lead finished wave 2; QA read the specs. You merge it all into one branch that compiles, apply every cross-owner patch, implement the Producer's integration calls below, and hand back a deploy list and a run order. You never touch his Mac or Unity (the Producer deploys and runs everything there). You may edit any file in your worktree.*

## Read first
1. `/root/cafe/brief/builders.md`, `/root/cafe/brief/contracts.md`, `/root/cafe/feel/feel-targets-2026-10-14.md` (binding; note calls 21–23 and the restated T9/T20).
2. The seven builders' reports: `/root/cafe/build/reports/{bench,scale,days,npc,cine,imports,uiux}.md` (each §2 run order, §5 patches, §6 risks).
3. `/root/cafe/specs/blender-wave2.md` (the Blender lead's wave 2: §4.3 positions that moved, §5 what the Unity import must do) and `/root/cafe/specs/qa-2026-10-14.md` (QA's Blocker and Majors: B1 is answered by wave 2; M1, M2, M3, M6, M7 are answered below).

## 1. Merge
- `git -C /root/cafe/repo worktree add -b integ /root/cafe/build/integ 431f081`, then merge, in this order: `b-bench`, `b-scale`, `b-days`, `b-npc`, `b-intro`, `b-imports`, `b-uiux` (merge commits, no rebase). Ownership (contracts §10) decides any conflict; say how each conflict was resolved.
- Commit after the merges, then after each group of changes below, with plain messages.

## 2. Apply the cross-owner patches (exactly as written in the reports, adapted only where the merged code differs; say where)
- **npc** 5.1 (`CustomerSpawner.cs`), 5.2 (`Playtest4SystemsSteps.cs`); 5.3 only if `DayIntro.HoldTheWorld` lacks the call (cine says it has it).
- **days** §5: `DialogueRuleChecks.cs` (2 edits), `NotebookPlayCheck.cs`, `NightZero.cs`, `Diagnostics/NightOneCheck.cs` (4 edits).
- **imports** 5.1 (`BenchOutline.cs`: skinned meshes; check uiux's rewritten `BenchOutline`/`BenchGlow` and make both read `SkinnedMeshRenderer`s); 5.2 is already in bench (`bcda6e4`): verify.
- **cine** patch 1 (`CafeSoundscape.cs`): imports already wrote its own intro hook. Keep **one**: `DayIntro.FilmingInsideCafe` (the intro knows its shot) if it exists after the merge, else imports' version; delete the other path. Patch 2 (`DayIntroMenu.cs` text).
- **uiux** A (`Day1GuideCheck.cs`), B (`DayOneOnboardingChecks.cs`), C (`StationInteractable.cs`), D (`BenchViewProbe.cs`), E (`BenchLab.cs`), F (`RecapPhone.cs`, the Vibration row).
- **bench** 5.1 (`GraceRepairInteractionChecks.cs`).
- **scale** (a)/(b): **not applied** (call 22: one "Put it back" — uiux's `DropInteractable` words and patch C). Remove cine's reflection-based "Put it back" path in `StationInteractable` in favour of patch C. Keep scale's `PlayerCarry.LiftedFrom`, and make uiux's "lifted from this tray" read it if that is a small change (`PlayerCarry` records it at pickup, which is more reliable than a once-a-frame watch); otherwise keep uiux's watch and say so.

## 3. The Producer's integration calls (implement)
1. **The bench layout (call 21):** `toolRestCentreY` 0 for the phone, the camera and the watch; `scrapTray` phone **(6, 133)**, watch **(26, 107)**, camera unchanged. Change it wherever it is defined (the bench builder's `BenchLayout` presets / `LayoutOf`, Bench 7, any check that hardcodes it) and make Bench 9's search and Bench 5 use the new values. T9's bars in Bench 9 and Bench 5: zoom 0 phone ≥ 34 %, camera ≥ 35 %, watch ≥ 17 %; zoom 1 ≥ 55 % each. T8 ≥ 12 px at 1080p default.
2. **The cloth shows what it cleans (call 23, QA M3):** the held cloth's mesh sits with its far edge (up the screen, away from Ace) at the cursor; the clean footprint stays centred on the cursor; closed form, no lag; mirrored rubs keep it. Bench 8 (`ClumsyBenchLab`) adds a mid-wipe check that at least 40 % of the spot's projected disc is not covered by the cloth's projected bounds (or, if that is impractical, a mid-wipe photo per wipe step and a note).
3. **The night zoom's lab (T20 restated, QA M2):** `NightZoomCheck`'s per-frame bar on frames where a limit eases is `35 m/s × dt + 0.05 m`; on frames with no zoom input the distance never moves more than 3 m; add a mouse-scroll pass (a virtual mouse's scroll, the way `ClumsyBenchLab` makes its virtual mouse) if it fits in about 40 lines, else note it.
4. **The intro's new cues:** give `intro.lights` the `intro.lamp` file; give `intro.cup`, `intro.steps`, `intro.car` and `street.day` bank entries with no file (silent, listed by Sound bank 2 as "no file yet", never a FAIL). Nothing may log an error for a missing cue.
5. **Blender wave 2 into the tree** (`/root/cafe/specs/blender-wave2.md` §5): copy from `/root/cafe/work/blender/` the three device FBX at real size and `devices.json` v3 (to wherever `HeroDeviceSteps.cs` reads them), `BT_Screwdriver.fbx` and `bench_tools.json`, `Assets/Data/Resources/Bench/BT_ScrewHole.png`, the five new icons, `LI_BarMat.fbx` (the mug tree) and the café manifest with its new material, the changed `Tools/Blender/` scripts and README, and the new `.blend` sources (`BlenderSource/`). **Keep every existing `.meta`** for a replaced file (GUIDs). Follow `.gitattributes` (LFS) and record where the real bytes are.
6. **Devices 2 meets wave 2:** make the scale builder's Devices 2 (`HeroDeviceSteps.cs`) handle the v3 manifest: the strap's pieces (`UpperStrap`, `LowerStrap`, `WornLoop` → the prefab's strap children under `KEEP - scratched sentimental strap`), the `moves` (the anchors that rose 9 mm: lens, film path, film guide, shutter seat, body; the screws' new y), the three scratches onto the loop, colliders off the strap and scratches (Art A6), the camera's `restHeight` by the scale builder's surface rule, and **a new inactive child `Clean shutter mechanism`** at the shutter's seat with a collider, **wired as the camera's fresh part** in the bench builder's `ReplaceablePart` (so on Day 2 the fresh part reads as the same mechanism, clean). Say exactly what Devices 2 now does and in what order relative to the days builder's step B (the resting copy of the jammed shutter must be made after Devices 2).
7. **Contract drift:** wherever one builder's code calls another's and the signatures differ, fix it at the call (smallest change) and list it.

## 4. Prove it compiles (as far as the cloud can)
- Syntax: `python3 /root/cafe/tools/cs_syntax_check.py` on every changed `.cs`.
- Types: the Roslyn harness the builders made: `/tmp/claude-0/-home-claude/aa2ea824-6adc-551b-8de6-afdc6c4c9e0d/scratchpad/rcheck` (Program.cs; run `dotnet bin/Debug/net8.0/RCheck.dll <tree> ../stubs`) against its baseline `base_stub.txt` (made from the original tree; see `scratchpad/check.sh` for the comparison). Get the integrated tree to **0 new errors**; extend the stubs (`scratchpad/stubs/`) where a Unity API we use is missing from them, and list every stub you added (an API you add to a stub is an API you claim Unity 6 has: be sure).
- Console tests: `Tests/BenchRules`, `Tests/ContinuationRules` and any other `Tests/*` whose sources changed: `dotnet run`. All pass.

## 5. Hand back `/root/cafe/build/reports/integ.md` (and as your final message)
1. The merge: conflicts and how resolved; every patch applied (or adapted, and how); every call in §3 implemented, with files.
2. **The deploy list:** `git diff --name-status 431f081 integ`, grouped (code, editor, data assets, binaries); for each LFS binary, the path of its real bytes in the cloud; the total size of the binaries.
3. **The Mac run order**, one numbered list merged from the seven reports and wave 2: each menu item (exact path), what must be true before it (scene open and saved, not in Play mode, which step first), what it prints when it works, and which must never run twice. Edit-mode steps first, then checks, then the Play-mode labs (with their minutes).
4. The type-check result (new errors: 0, or each one left with why), the console tests' lines.
5. What you couldn't resolve, most important first.

About 75–100 minutes. Decide what the briefs leave open (closest to his notes), say so, carry on.
