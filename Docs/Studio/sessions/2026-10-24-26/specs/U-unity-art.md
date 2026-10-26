# Builder U: the art audit's Unity fixes (26 Oct 2026)

You are the Art Direction and Environment lead, building in Unity code. Read `/root/review24/builders-common.md` first, then `/root/review24/his-answers-2026-10-24.md`. Your copy is `/root/wt24/U`. Your progress log is `/root/review24/reports/U-progress.md`, and your report is `/root/review24/reports/U-report.md`.

**Be economical.** He asked us to preserve usage. Read only what you need, grep before reading whole files, and don't re-read files.

## The findings
The art audit's findings are in `/root/review23/decisions/art-items.json`, with pictures in `/root/review23/decisions/img/<id>-*.jpg` and the reviewers' detail in `/root/review23/findings/art-R1..R4.md`. The short table is `/root/review24/art-answers-table.md`. He said "Yes, do that" to all of them except a40 (leave the syrup labels).

On 26 Oct he added notes (his words in quotes):
- **a01, a second person inside each customer:** "that is a problem that i only see when you record a session, but when i play the game myself, i never really see anyone inside another person, but if it is a problem, then fix it."
  - It may be a lab artifact, for example something the labs or the recorder spawn, or a body the labs bake for photos (`PolygonNpcSetup.SkinToWorld`, `CafeLifeRecorder`).
  - Find the real cause in the code before fixing anything.
  - If it only happens in labs, fix the lab and say so. If it happens in the game, fix the game.
- **a04, seated customers lose their heads:** "that i never have seen, so if that is an issue that you notice, definitely fix it." Diagnose it first (the reviewer's guess was 'Update When Offscreen' on the head, hair and hat renderers), then fix it only if it's real.
- **a05, the black lid behind the bins man:** "never have seen that, so if it still is an issue, that lets definitely fix that." Check it the same way.
- **a10, the drinks machine's button words:** not yours. He wants the buttons redesigned ("its just words on a square in different colors, it doesn't give aces aesthetics"). A designer is drawing options, so don't touch the machine's buttons.
- **a11, the milky glass on the bench screen:** paused, so don't do it.

## Yours
All of these are the Unity-side findings: materials, colours, switches, swaps, camera aims, settings and positions.

a01*, a04*, a05*, a06, a07, a08, a09, a13, a15, a16, a17, a18, a19, a21, a24, a25, a26, a30, a32, a33, a35, a36, a38, a39, a43, a45, a46, a51, a52, a54, a58, a59, a60, a61, a65, a69, a70, a71, and a75 (only check what the black block is, then report it).

\* investigate first.

**Not yours:**
- The Blender builder (B2) has the models and textures, including the Unity steps for a47 (faces), a55 (the back door's seam) and a72 (Grace's photos).
- The Days builder (P2) has a03 (the support call's phone).
- The furniture builder (F2) has a56 and a80.
- "Leave it" findings: a28, a40, a57, a63, a64, a66, a67, a68.

## How
- **Every scene, prefab or material change is an editor step.** Put them in your own new file `Assets/Editor/ArtFixUnitySteps.cs`, one menu item per group under `Fixit Fidget/Art fixes/`. Each step:
  - is idempotent and has an undo step;
  - writes a report under `Logs/ArtFixes/`;
  - photographs the result from the angle the finding was seen at, if you can do that cheaply with the house's photo helpers;
  - ends with a DONE line.
- **Find objects by name and component anywhere in the scene, not by full path.** The furniture builder's Furniture 1 step moves the café's own pieces under a new parent ("31 - furniture"), and his Mac may run either first. Grep `/root/ref24` (the scene and prefabs as they are now) for the exact names, materials and GUIDs.
- **Runtime code** (only where a finding needs it, e.g. a08's drink colours, a36's conversation camera, a01 if it's a game bug):
  - keep the edits small;
  - mark them `// 26 Oct 2026 (Art fixes)`;
  - don't touch files the Days or furniture builders own: `CustomerSpawner`, `WaitingArea`, `SeatTags`, `TableSeat`, `DirtyCup`, `RecapPhone`, `SaveData`, `SaveManager`, `PatronSpawner`, `DayOneGuideUI`, the new furniture and thrift scripts. If one of them must change, write the exact edit in your report.
- **a43 (the stools 10 cm back) changes the hand-over reach.** `Fixit Fidget/Checks/Probe - the bench's hand-over reach` must still say "from every one of them stool 1 is handed over to", and Life 4 and Sit 4 must still sit people on the stools. Work out from the code and the scene whether 10 cm keeps that true. If it doesn't, move them less, and say so.
- **a45 (URP shadows)** is a settings asset (`/root/ref24/Assets/Settings/`). Change it through a step, not by hand.
- Run `python3 /root/tools/csparse.py` on every file.

## Report
For each finding, give:
- what you found (for a01, a04 and a05, whether it's real, and the cause);
- what the step or code does;
- which step it is;
- how to see it on his Mac (which lab photo shows it).

Then the order to run your steps on the Mac, and the undo.
