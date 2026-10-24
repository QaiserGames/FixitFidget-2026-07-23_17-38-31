# Builder C — the box coats out, and the phone's tray and mat (23 Oct 2026)

Copy: `/root/wt23/C`. Rules: `/root/review23/builders-common.md` (read it first). Photos you can open: `/root/review23/photos/` (the bench ones start `Bench_`, the café's `CafeLife_`).

## His notes (verbatim)
- "I did notice that the mat shows an inconsistency when working with the phone. Like the tray on the right and the bottom of the mat kind of look off."
- "I also noticed there's clothes on the on chairs in the cafe that just look like squares and rectangles together."

## Part 1: the coats
- **Where they come from.** `Assets/Editor/CafeLivedInSteps.cs` (~line 1835) places "a coat on a chair's back" on the two banquette chairs farthest to walk to, as children of their chairs. `Assets/Editor/CafeFurnishing.cs` (`CoatStand(...)`, ~line 301) builds the waiting nook's coat stand. `CafeTidySteps.cs` describes it as "a coat rack with two squares (its scarf and Ace's tote)".
- **What they look like.** Boxes. In the overhead photos (`CafeLife_*`) they read as dark rectangles on the chairs.
- **What to build.** An editor step, with an undo step, that takes them out of the scene. Follow the house style for steps: a menu item under `Fixit Fidget/...`, a report, Undo-registered, and the "take them out again" pattern; look at how other steps do it, e.g. `CafeDressing3Steps.cs`.
  - Prefer **switching off** (SetActive false, Undo-recorded) over deleting, so he can bring any back.
  - Make sure no runtime script switches them back on. Grep for the names and for anything that "shows" lived-in props (the NPC lead's cups, laptops, coats "follow who sits there later").
  - Make sure re-running the lived-in step won't put them back (a guard, or a note in its report).
- **The coat stand:** take off its two boxes (the scarf and the tote) and keep the stand if the stand itself reads as a stand. Decide from the code's shapes and say why.
- **Anything else** built from plain boxes in the same dressing passes that reads as programmer art (look for primitive cubes and capsules standing in for soft things: clothes, bags, cushions): list them in your report. Don't remove them; the art audit and he decide.

## Part 2: the phone's tray and the mat
Open these photos:
- `Bench_bench-check-2026-10-20_011610__07-broken-screen-in-the-tray.jpg`
- `...__06-cover-in-the-well.jpg`
- `...__04-screws-in-their-cups.jpg`
- `...__01-on-the-mat.jpg`
- `...__15-grace-camera-0-on-the-mat.jpg` (the camera's job, for comparison)

What reads wrong with the phone:
- **The back cover's well** is a thick **black** frame; every other tray on the mat is aluminium or warm grey.
- It **runs off the mat's right edge** onto the bench's wood, and off the screen's right edge.
- The new screen's **pad overlaps** its left side.
- **The mat's bottom edge**, with its ruler numbers, is cut off by the bottom of the screen; the strip under it reads like a mistake.

The stage:
- `Assets/Scripts/BenchStage.cs`: `wellSmall`, `wellLarge`, `padSmall`, `padLarge`, the bays' rooms; Art A1 note: "the pad 80 × 166 at x 90..170, the well 80 × 166 at x 182..262".
- `Assets/Editor/BenchStageImportSteps.cs`: the imported stage model and its empties `Well_Small`, `Well_Large`, `Pad_Small`, `Pad_Large`, `Cup_1..6`.
- `Assets/Editor/ForgivingBenchSteps.cs`: the forgiving bench's boxes, e.g. `Box("Cover well", ...)` at ~line 275, the settle onto the bench top.
- `Assets/Editor/BenchFramingSteps.cs`: the close-up's framing.
- Bench 5: `BenchViewProbe`, "the close-up's view".

Find what the black frame actually is (the well model, a material, a box) and why it's black, and where the camera's framing puts the mat's bottom edge. Then propose and build the smallest fix that makes the phone's layout read as one set with the camera's and the watch's:
- the well in the same metal as the other trays, no thick black rim;
- everything on the mat: well, pads, cups and trays inside the mat's border with a margin, nothing hanging over its edge;
- no overlaps between bays;
- the mat's near edge either inside the frame with its border showing as a border, or cleanly cut, not a half ruler.

Constraints:
- The empties are where the auto-tray flies pieces. Moving a bay means moving its empty, and every lab that knows bay positions or rooms must still pass:
  - Bench 3: "the bench", 199 checks;
  - Bench 5: "the close-up's view", 147;
  - Bench 6: "words on the bench", 130;
  - Bench 8: "the clumsy hand", the gate, 220.

  Read their checks before you move anything.
- If the fix needs the stage model changed in Blender, say so. Do what Unity can do now (materials, positions, framing) and write the Blender change as a separate item. Blender sessions are separate from Unity sessions, his rule.
- Every change is a step he can undo. Nothing is changed by hand in a scene.

## Files you own
New step file(s) in `Assets/Editor/` (e.g. `ArtTidySteps.cs`), and the guard in `CafeLivedInSteps.cs` if you add one. For the bench, the step(s) that move or restyle the bays and framing; `BenchStage.cs` only if a runtime constant (room sizes) must change. The bench labs only where their expected positions must follow, and say exactly what and why.

Don't edit `BeverageStation.cs`, `AimAssist.cs`, `RecapPhone.cs`, `WaitingArea.cs`, `NightNotebook.cs` or `UiPins.cs` (other builders own them).

## The proof
On his Mac the integrator will:
1. run your step(s) and save the scene;
2. run Bench 3, 5, 6 and 8;
3. look at the photos of the phone on the mat and of the café from above.

Say which photos show the change, and what a pass looks like.
