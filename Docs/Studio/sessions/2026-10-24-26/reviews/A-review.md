# Reviewer A: U (Unity art fixes), B2 (Blender art fixes), Q (lab queue), 26 Oct 2026

Copy `/root/rv24/A`, commit `7a44894`. Every file I changed: csparse clean.

## Fixed
- `Assets/Scripts/DrinkLiquidVisual.cs`: `OnDestroy` now releases the a08 crema's `middleMaterial`. Before this, every drink made leaked one material.
- B2's edits outside its files (B-report §4, items 1–4), applied exactly as written:
  - `GraceHouseFurnitureSteps.cs`: GH_Photo is now FFFFFF and GH_Hair is added to `Hers`. The six `Pieces` rows have their new triangle counts and sizes.
  - `CafeRoomSteps.cs`: CK_OliveTree is (.733, 1.636, .639).
  - `ToolPickup.cs`: the comment says the cloth was rebuilt.

## Checked and sound
- **a01 (PolygonNpcVisual).**
  - `hiddenRenderers` is taken before the city look is made, so it never holds the look's own renderers. The loop only runs while `instance != null`.
  - AceBody's Sidekick body and city look are separate objects that are destroyed when Ace changes body. GraceAtHome hides bodies with `forceRenderingOff`, not `enabled`. Nothing turns a hidden CC0 part back on on purpose.
  - So it can never hide a person's only body.
  - Turning off `NpcVisualVariants` only stops its `Start`, which is all it does.
  - Cost: about 25 `enabled` reads per person per frame. That is fine.
- **a04.** The bounds are widened once, with no per-frame cost. AceBody's height comes from `standingHeight` (2.04 in the scene), not from the bounds.
- **DayIntro.**
  - F2's Day 1 line and U's a08 and a69 edits are both there.
  - a69 geometry checked: the post goes from 0.53 m to 1.53 m off the camera's line.
- **ConversationController (a36).** At 0.87 m, the probe's "< 1 m from the eyes" check still holds.
- **GrimeSpot (a26).** Only a colour changes.
- **Bench outline shader (a25).** Plain float3 maths in the hidden pass's vertex shader. It compiles in URP. Moving the vertex along the eye ray leaves its screen position unchanged.
- **a43 against the hand-over probe.** The probe uses stool 1 only (the nearest, about 2.9 m after the move) against Across Counter Reach 3.5. The seat and the stool model are siblings, so nothing moves twice.
- **The steps don't undo each other.**
  - U7 / B2 Models 1: they touch different renderers and materials. `MakeHerMaterials` never resets `_Cull`.
  - U5 / B2 Models 2: they share no objects.
  - P2's shelves are empty groups and match none of U's name searches.
  - U4 finds the rug by name prefix, so either order with Furniture 1 works. The napkin boxes (a48) are B2's FBX, with no material overrides in the scene.
- **LabQueue.**
  - The state lives only in SessionState. The listener is re-hooked in both domains.
  - The phase is set before `ExecuteMenuItem`, so an end line logged inside the menu call is caught.
  - The ready line only counts while playing.
  - `ExitPlaymode` is called only when `isPlaying`, so it never toggles Play blind.
  - The summary is rewritten after every entry.
  - The default list matches the spec line for line (37 entries, checked by script). The end-line formats I spot-checked match the code.

## Not fixed
- **Medium. Order dependency with F2's Furniture 2** (`ArtFixUnitySteps.cs` steps 3 and 4). Furniture 2 copies the scene's pieces into prefabs, including the lounge rug (a51). If U3 or U4 runs after it, bought pieces keep the old look. Fix: run U 1–9 before Furniture 2 (the order is below).
- **Low. B2's five steps have no Undo menu** (`ArtFixBlenderSteps.cs`). Only Ctrl+Z works, and only before the scene is saved. Importer remaps and texture imports can't be undone at all. That is allowed by the house rule ("registers Undo"), but worth knowing.
- **Low. `UndoRun` can half-undo** (`ArtFixUnitySteps.cs:174`). If the café scene isn't the open one, it still restores the asset part of the extra record (T2_Brass, the URP asset, the prefab palettes) but not the scene part. Fix: return early when `CafeRoot()` is null, unless the label is step 9's.
- **Low. a04 makes every body part's `renderer.bounds` the whole body plus 0.35 m** (`PolygonNpcVisual.cs`). Code that measures a person from renderer bounds now reads them about 0.7 m bigger. That includes Barks' fallback head point (used only with no Head bone) and `AceBody.Tallness` (used only if `standingHeight` is 0). Nothing in normal play hits this.
- **Low. Some frame time in Play mode** (`LabQueue.cs`). `Current` re-parses the entry and builds a Regex on every editor update. Caching the parsed entry by index would remove it. This only matters if a lab measures frame time.

## The order on his Mac
1. Copy in B2's files. Run Checks > Pick up scripts changed from outside (reimport). The compile must have 0 errors.
2. B2's imports:
   - Bench tools 1;
   - Devices 1, then Devices 2;
   - Café dressing 1;
   - Lived in 1;
   - Kit 1;
   - Night > Break-ins - Grace's furniture.
3. Art fixes > **Models** > 1 to 5. B-report §5 still names these "Art fixes > 1…"; the integration moved them under Models.
4. Art fixes > 1 to 9, after any re-run of Night 0's step (it rebuilds the lids, which undoes a05).
5. F2's Furniture 0, 1, 2 and 3, and P2's Props 1, as their reports say. Furniture 1 after U4 means F's own rules check the moved stools too.
6. Save the scene.
7. Checks > Probe - the bench's hand-over reach. Then Life 4 and Sit 4. Then, in Play, Art fixes > Check - one body each, heads kept (Play mode, read-only).
8. Undo, newest first: the shared "latest first" rule covers U's and F's steps together.

## Lab expectations at risk
- Life 4 and Sit 4: the cup stays on the bar while the seat moves 10 cm back, so the cup reach may look stretched.
- Bench 8 "lens in sight": B2's new cloth.
- The queue's last entry (`\[Review screens\]`) always counts as FAIL under the PASS rule. Read it as "it ran".
- Never run NPC > Wave 2 6b again: it overwrites a46's palettes.
- If Street 1 is ever undone, the A-frame's café-side face has to be deleted by hand.

## Needs his decision
- a46 matches the palette alternates loosely, by family name, not by exact name.
- a45 changes High only: 35 m of shadow, so wide street shots lose shadows past 35 m.
- The LabQueue Review-screens regex: accept FAIL there as "ran", or match its final line instead.
