# The Mac run, 23 Oct 2026 (integ23 adb4fe4, 37 files deployed and md5-checked at 16:47 UTC)

Unity menus through computer_app_menu on com.unity3d.UnityEditor5.x (background). Check `playing` first (EDIT). Lab end lines with `lmark` then `labwait <=30` polls. Scene: Assets/Playtests/AcesCafeLayoutPlaytest.unity.

## 0. Compile
1. Fixit Fidget › Checks › Pick up scripts changed from outside (reimport). Read Logs/Compile/last.txt; any error → fix in /root/integ23, redeploy with /root/tools/stage23.py, md5 check, reimport again.

## 1. Scene and asset steps (in this order; each a report)
1. Bench › **Bench 7b - The old tray's dark frame off the mat (scene)**: saves the scene. Pass: "[Bench 7b] The old tray's dark frame is off the mat." and report "All good." (5 PASS rows; camera/watch lines are info; the watch's tray-over-pad is a NOTE).
2. Bench › **Bench 9 - Frame each device (prefabs)**: pass "N prefab(s) framed, P PASS, 0 FAIL". Camera ≈ y −175.7 "numbers whole", nothing written; phone ≈ −172.0 "A HALF RULER" → "under the rule … Taken."; "saved the framings as they were … framing-before-near-edge.json" before its CHANGE. Undo menu: "Bench 9 - Undo the near-edge rule: put back each device's framing from before it (prefabs)".
3. Cafe redesign › **Art tidy 1 - The coats out: off the chairs and the coat stand (scene, with Undo)**, then File › Save. Pass: "DONE. Art tidy 1 - The coats out", no FAIL.
4. Cafe redesign › **Seat tags 0 - Check first** ("CHECKED: ready"; 28 to tag, 6 areas) → **Seat tags 1 - Tag the seats and the room** ("DONE"; Window 11, Lounge 7, Bar 6, Communal 8, Quiet 2, NearDoor 4) → File › Save. (Undo order: Seat tags 1 first, then Art tidy 1.)
5. Night › **Notebook pictures 1 - Photograph the notebook's subjects (Edit Mode)**: "12 photographed, 0 kept, 1 couldn't" (cones) is a PASS; look at the contact sheet. The PNGs, index.json and metas get committed.
6. On the Mac, Inspector: the HUD prompt text's Font Style (D's finding 8): if Bold, BeverageStation.Line() should use FontStyles.Bold.

## 2. Edit-mode checks
- Checks › Night notebook rules → "PASS: 323 assertions".
- Checks › Seat choice rules (café open) → "All 59 checks passed" (50 without the café).
- Checks › Waiting space reservations → "The café scene: N passed, 0 failed".
- Playtest 4 › UI › 2 - Settings: the bench's sticks and the morning (reads Describe, calls Reload).

## 3. Play labs, one at a time (expected)
| Lab | Expect |
|---|---|
| Playtest › Aim help - play check | All 53 (57 if every bench case runs) |
| Playtest › Stations as reach - first person / overhead | 101 / 102 |
| Playtest 4 › UI › 1 - Day 1 guide | 81 |
| Playtest › Juice - Photograph the feedback | 50 |
| Playtest 4 › Bench › 2 - The dispenser's frame | 14 |
| Bench › Bench 3 / 5 / 6 | 199 / 147 / 130 |
| Bench › Bench 8 - The clumsy hand | 213/220 with the 7 known misses (maybe 214) |
| Recap phone › Play check | 136 (photo 05b mid-motion) |
| Playtest › The phone by day - pause and HUD check | 113 |
| Checks › Night notebook (Play Mode, lab session) | Notebook check PASS, then Notebook page 78 (71 without the word check) |
| Night › Night 0 - Play check | 122 (photo 02b in first person) |
| Night › Night 1 - Play check, keeping a straight face | as before |
| Days › Lab - Day 1 / 2 / 3 at the counter, 3 runs each | Day 1: 24 runs 1–2, 27 from run 3 (medians); Day 2: 23/26; Day 3: 22–23/26 |
| NPC › Life 4 0 then Life 4 | middle, east, west, then a floor seat |
| NPC › Sit 4; Checks › Hands on it; Checks › Probe - the bench's hand-over reach; Dialogue › Conversation play check; NPC › Café life check | as before |
| Checks › Controller (Play Mode) › Run controller check | as before |

## 4. Then
- Review › Screens 1: no new S1/S2 except Hands (24 px) / Step back (22 px) S2 (his call c3).
- Look at: Bench 3's 01/04/06/07 and 15; Stations' drink-2/drink-3; Art tidy 1's 2-after; notebook page 01–07; recap 05b; Night 0 02b.
- Commit on the Mac (gitl.sh), session notes in Docs/Studio/sessions/2026-10-23.md and the Project, push reminder.
