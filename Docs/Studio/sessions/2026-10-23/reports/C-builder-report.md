# Builder C report: the box coats out, and the phone's tray and mat (23 Oct 2026)

(As handed back by builder C. Copy: /root/wt23/C, uncommitted.)

Three files are changed and two are new. `python3 /root/tools/csparse.py` says "clean" for all five. csparse only checks syntax. On the final re-read I found and fixed a real compile error it can't see: Bench 7 already had a local named `oldTray`, and my new local had the same name. It is now `v2Tray`. After that I re-read every new line for clashing names and members that don't exist.

## 1. What I did (files)
- **NEW `Assets/Editor/ArtTidySteps.cs`** (Art tidy 1): switches off the two coats on the banquette chairs and the coat stand's scarf and tote. It is recorded and has an Undo menu. It runs through `CafeRedesignShared.Step`.
- **NEW `Assets/Editor/BenchStageTidySteps.cs`** (Bench 7b): switches off the old v2 parts tray in his scene, which is the black "well frame". It checks, takes photos before and after, writes a report, saves the scene, and has an Undo menu.
- **`Assets/Editor/ForgivingBenchSteps.cs`** (Bench 7): it now switches off the whole v2 tray group, not just its floor. This is the root cause. A 2-line change plus a 4-line comment; header updated.
- **`Assets/Editor/BenchFramingSteps.cs`** (Bench 9): each layout view is now also searched under a "near edge" rule, and a rule framing is only taken if no feel-target bar is lost. It adds a new undo menu for the plain search. Header updated.
- **`Assets/Editor/CafeLivedInSteps.cs`** (the guard): Lived in 2 now places its two coats switched off. Its header, Info line, Note and "in use" photo are updated to match.
- No lab was edited. No bay or empty moved. `BenchStage.cs` is untouched. None of the files other builders own were touched.

## 2. Decisions and reasoning
### Part 1: the coats
- **What he saw.** Lived in 2 (13 Oct) puts two coats (`LI_Coat_OnChair (seat …)`, from cafe_livedin.py) over the two banquette café chairs. From above they read as dark slate rectangles.
- **Switched off, not deleted.** Each coat is recorded on the café root, and the chairs stay.
- **Nothing brings them back.** `NpcLifeProps` only finds `LI_Laptop_Open`, `LI_ToteBag` and `LI_BinStation` by name, and no runtime script mentions coats. The step's survey checks three things and refuses if any fails: no script under them, no collider, no prop-like name. Since nothing under them is solid, routes can't change. The layout and circulation guards run before and after, and routes are not re-baked.
- **The guard.** Lived in 2 now places the coats with `active = false`. They are fitted and checked as before, so a better coat could take the same places, and redoing Lived in 2 never brings them back.
- **The coat stand stays.** I judged it from the code's shapes: a 1.74 m post with a ball finial, four pegs at 55° with brass knobs, and a cross foot with brass caps, so it reads as a stand. Its hat is three cylinders, not boxes, so it stays too. Its two box-built things go off: the Scarf (3 boxes) and the Tote bag (a bag box, a patch, 2 straps). The stand itself has been off since Playtest 3's tidy (9 Oct), and I left it off.

### Part 2: the phone's tray and the mat
- **The black "well" is not the well.** It is the four walls of the v2 magnetic parts tray from Bench 2 (7 Oct): the group "Parts tray", a floor, four DC_SteelDark walls 170 × 170 × 24 mm, and a magnet zone.
- **Why it was still there.** Bench 7 switched off `stage.LegacyTray`. Bench 2 had wired that to the tray's floor only, so the four walls stayed standing on the new mat at mat x 140..310, y −85..85.
- **That one object causes three of his four symptoms:** the thick black frame; it runs 10 mm past the mat's right edge onto the wood; its west wall at x 140..145 lies across the pad (x 90..170) and hid the fresh screen's right third. This matches the 19 Oct findings: bench-a S2 ("find the rim object … and disable it too"), bench-b S2, and two bench-b S3 items.
- **The real well** is moulded into Art's mat: x 182..262, 3 mm deep, with a 2 mm by 0.8 mm lip, in the deep-blue BM_Recess like the cups' walls. It sits 10 mm right of the pad and 36 mm inside the right edge.
- **The fix.** Switch off the whole group: Bench 7b for his existing scene, and Bench 7 itself for a fresh stage. Nothing is moved and no material changes. Bench 7b refuses if anything the bench uses is under the group (a place, the rest, the tray, a tool or a device). Its report checks the phone's layout: every place on the mat's top, and no overlaps. It only lists the camera's and the watch's layouts.
- **The near edge.** Bench 9 never looked at where the frame's bottom lands on the mat. For the phone it landed at mat y −172, through the ruler's numbers (photo 01). The near ruler, from bench_stage_textures.py: its edge at −178.5, ticks up to −174, numbers from −173 to −170, inner line at −166. The rule tries, in order:
  1. the ruler whole, with the mat's edge and 3 mm of bench under it; or no ruler at all, cut 3 mm inside its line;
  2. the numbers whole, with the ticks running off the bottom (how the camera's 20 Oct photo looks);
  3. otherwise the plain framing, plus a FAIL row naming the bar each rule would cost.
  Each rule is taken only if it keeps every bar the plain framing meets: T9 at zoom 0 and at the zoom limit, T8 at zoom 0 and one notch in.
- **My rough model of Bench 9 (it reproduced the plain framing):**

| Phone framing | Aim (mm) | Distance | Frame height | Screw head | Frame bottom |
|---|---|---|---|---|---|
| Plain (now) | (18..20, −9) | 0.463 m | 37.5 % | 12.5 px | y −172 |
| Rule 1 | (7..9, −15) | 0.478 m | 35.8 % (bar 34) | 12.05 px (bar 12) | y −183 |
| Rule 2 | (15..16, −10) | 0.468 m | 37.0 % | 12.4 px | y −174.7 |

  If T8 lands just under 12 px on the Mac, Bench 9 takes rule 2 by itself. The same rule applies to the camera and the watch, so the three read as one set. The model suggests the watch takes rule 1 (about 20 % → 19 %, bar 17). The camera already meets rule 2. "Bench 9 - Undo the near-edge rule" re-frames all three with the plain search, which gives exactly the old numbers.

## 3. Decisions his to overrule
1. The black frame is switched off entirely (the whole v2 tray group), not recoloured or lowered.
2. The well stays Art's moulded blue recess with no rim. A floor in the trays' metal needs Blender (item B2).
3. The near-edge rule: rule 1 first (edge in view), rule 2 as the fallback, and applied to all three devices. This makes the phone about 5 % smaller on screen.
4. The coats are switched off (not deleted), and Lived in 2 now places them off.
5. The coat stand and its hat are kept. Only the scarf and the tote go off, and the stand stays off as Tidy 3 left it.
6. I changed Bench 7's own switch-off. Its header said the v2 tray was off, which wasn't true.
7. Layout findings I didn't touch (the Producer's call 21, in `DeviceDefinition.cs`): the phone's old-parts tray rim is 0.9 mm inside the flat top's far edge; the watch's tray rim lies over the pad's far-left corner, about 16 × 22 mm (visual only, since `Pad_Small` is at (116, −58)); the camera's tool rest is 4.5 mm from the left edge and lies over the left ruler.

## 4. Outside my files
- **Required:** nothing.
- **Optional, only if he says yes to Q3:** move the watch's tray off the pad. In `Assets/Scripts/DeviceDefinition.cs`, `BenchLayout.ForWatch`: change `scrapTray = new Vector2(26f, 107f)` to `new Vector2(26f, 131f)` (2.4 mm to the pad, 6.4 mm to the rest, 2.9 mm to the far edge). Update the comments that say "(26, 107)": the doc comment above `ForPhone`, the `BenchViewProbe.cs` comment near line 261, and the `BenchFramingSteps.cs` comment near line 803. Then re-run Bench 7 (its re-run writes the prefab layouts), then Bench 9. Bench 5 follows by itself, since it reads `ForKind`.
- **For S and the integrator:** Art tidy 1 joins the café redesign's list, and only the latest redesign step can be undone. Whichever of S's seat-tagging step and Art tidy 1 runs last must be undone first.

## 5. Menus on his Mac, in order, and what a pass looks like
0. Copy the five files in, then run **Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)**. Open `Assets/Playtests/AcesCafeLayoutPlaytest.unity`.
1. **Fixit Fidget › Bench › Bench 7b - The old tray's dark frame off the mat (scene)**. It saves the scene itself. Pass: the console says "[Bench 7b] The old tray's dark frame is off the mat." and `report.txt` ends "All good." PASS rows: the group is off; nothing else of the v2 stage is drawn; nothing but the mat stands on its right side; phone places all on the mat's top; phone, no two places overlap. Photos in `Logs/Bench/frame-off-<time>/`.
2. **Fixit Fidget › Bench › Bench 9 - Frame each device (prefabs)**. Pass: the final line has 0 FAIL, and each device has a PASS row "the near edge reads at zoom 0: …". If the watch was ever framed by 9b, run 9b after. A FAIL near-edge row means no rule kept that device's bars; its framing is left as it was; the fix is then Blender item B1.
3. **Fixit Fidget › Cafe redesign › Art tidy 1 - The coats out: off the chairs and the coat stand (scene, with Undo)**, then **save the scene** (this step doesn't save). Pass: "DONE. Art tidy 1 - The coats out" with no FAIL. Photos in `Logs/CafeRedesign/art-tidy-1-coats-<time>/` and `Logs/CafeFurnishing/art-tidy-1-coats-before|after-<time>/`.
4. The four labs, each ending "[Tag] All N checks passed." with the same counts as before: Bench 3 - The bench - play check (199); Bench 5 - The close-up's view (147); Bench 6 - Words on the bench screen (130); Bench 8 - The clumsy hand (220).

Photos that show the change: Bench 3's `01-on-the-mat`, `04-screws-in-their-cups`, `06-cover-in-the-well`, `07-broken-screen-in-the-tray`, plus `15-grace-camera-0-on-the-mat`; Art tidy 1's `2-after-03-plan-from-above` and `2-after-02-game-camera`. A pass: no black frame; the cover in a blue recess with a thin lip; the pad fully clear; nothing past the mat's right edge; the ruler whole (or, under rule 2, numbers whole and ticks running off); the banquette chairs bare.

## 6. Risks, and APIs I'm unsure of
- Every API I use is already used in this codebase: `FindAnyObjectByType<T>(FindObjectsInactive.Include)`, `FindObjectsByType<T>(FindObjectsInactive.Exclude)`, `GetComponentInParent<T>(true)`, Undo groups, `EditorSceneManager.SaveScene`, and `Camera.Render` into a RenderTexture in edit mode (as Bench 7 does).
- Under rule 1 the phone is about 4 % smaller. Bench 5's T8 measures the phone's real screws (about 13 px on 19 Oct), so it reads above Bench 9's number.
- Bench 8: the clumsy hand's aim noise is partly in fixed pixels, so slightly smaller targets mean slightly more misses. If a phone or watch run regresses, run "Bench 9 - Undo the near-edge rule" and Bench 8 again.
- Bench 9 runs about three times longer (up to three searches per device).
- A pulled-back frame shows more bench beside the mat; every place is still kept clear of the HUD zones.
- Bench 7b's right-side scan skips the stage's own pieces, the devices, and anything taller than 0.3 m or wider than 0.8 m. An unexpected prop there gives a FAIL row that names it; the change is kept.

## Bays and empties
None moved. Bench 3: the auto-tray landing checks read the unchanged empties and rooms; in-view checks hold because Bench 9 keeps every place in frame and clear of zones U1–U5 by 20 px at 16:9 and 16:10; `BenchTopUnder` ignores everything under the rig. Bench 5: the call 21 layouts are unchanged; T9 and T8 hold because Bench 9 only takes a framing that keeps those bars; `UnderTheDevice` reads only the device's colliders. Bench 6: card zones unchanged; its stage points lie inside Bench 9's targets. Bench 8: its `Blocked` rays lose the walls as blockers, which can only help; the close-up's own rays also stop hitting the walls.

## Blender change (separate item)
- **B1**, only if Bench 9 gives a near-edge FAIL, or he wants the phone at its old size (Q1): in `bench_stage_textures.py` `rulers()`, have row A draw only its inner rule (no ticks, no numbers), or move the near ruler in from the edge; re-export with `export_bench_stage.py`, copy `BM_RepairMat.fbx` and `bench_stage.json` in, reimport, run Bench stage 1; set Bench 9's constants `NearRulerInnerMm` and `NumbersFootMm` to the new print; run Bench 9 again.
- **B2**, only if he wants a metal well (Q2): in `bench_stage.py`, change the well's floor face `mb.face(well_loop(0.0, T - WELL_DEPTH), 'BM_Recess')` to `'BM_Steel'`; re-export, reimport, run Bench stage 1, then Bench 3 for the photos.
- A real draped coat would be a Blender job too, only if he wants coats back.

## Other box-built soft things (listed only, not removed)
1. The coat stand's hat: three cylinders. Kept, and the stand is off.
2. The "Folded throw" on the rear window sofa (CafeFurnishing): off since Seats 1.
3. Four "Coffee bag" bags in the counter cubbies: off since Tidy 3.
4. The first layout's window banquette cushions (AcesCafeLayoutSetup): off since Seats 1.
5. Four hanging plants with squashed-sphere leaves: off since Room 1.
6. "Small entry greenery": a sphere in each entry planter outside the door. No step switches it off, so it is probably still on.
7. Street windows' "Curtain" panels (flat boxes, CafeStreetUpgrade).
8. Barnaby's capsule coat and cone hat (NightOneSteps): a stand-in that gives way to the Blender model.

## Questions for him
- **Q1. The ruler, or a bigger phone?** Recommendation: keep the rule; do B1 only if the phone feels too small.
- **Q2. The well: moulded blue or a steel floor (B2)?** Recommendation: look at the after photo first.
- **Q3. Move the watch's tray off the pad's corner?** Recommendation: leave it for now.
