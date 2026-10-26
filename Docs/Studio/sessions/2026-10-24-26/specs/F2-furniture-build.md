# Builder F2: build the café that grows, buy and place furniture (24 Oct 2026)

You are the Systems & Mechanics builder, with Designer F's spec in hand. Read `/root/review24/builders-common.md` first, then `/root/review24/his-answers-2026-10-24.md`. Your copy: `/root/wt24/F`. Your progress log: `/root/review24/reports/F-progress.md`. Your report: `/root/review24/reports/F-report.md`.

## What you build
Designer F's spec, approved by him for building: `Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md` in your copy, 885 lines. Read all of it.

- **The file list and build order: §4.10.** The spec planned five builders; you are all five. Build Parts 1–5 in the spec's merge order: Part 1 (the runtime and the save), then 4 (the conversion, the prefabs, the sheets, the bay's back), then 2 (Arrange mode), then 3 (the Shop's way in), then 5 (the labs).
- **The contract between the parts: §4.11.** Keep it exactly, because the other parts and the labs are written against it.
- **The proof: §5.**

His answers, applied (they win over the spec where they differ):
- **f1:** you can arrange only while the café is closed: at every closing, as often as you like.
- **f2:** a 25 cm grid with quarter turns.
- **f3:** a piece sells back for half its price; moving is free.
- **f4:** Tier 2 (Marble & Brass) is later, not v1. The save format is ready for it.
- **f5:** customers don't react to décor. Seats count; décor is looks.
- **r2:** the furniture spec's prices: bistro $80, square $90, window counter $150, banquette bay $140, lounge corner $160, communal $360.
- **r1: Day 1's half-finished café has 10 open seats, not the spec's 3.** That is P's version (P's spec §8.1):
  - open: the repair bar's 3 stools, the lounge's 2 tub chairs, the window counter's 3 stools, and one bistro 2-top;
  - under dust sheets: the west banquette bays, the communal table and the second bistro 2-top (16 seats).

  Rework §1.1, §3, §4.6–4.7 and the new-game layout to match.
- **r3:** two settled guests on Day 1, so the spec's "no café guests on Day 1" no longer holds. P2 (building Days 1–3 at the same time) owns the patrons' numbers; you make sure the open seats exist and that settled guests can use placed pieces' seats.
- **The cart lives only in the intro** (his choice of 23 Oct): Day 1's intro line becomes the spec's "Years at a coffee cart bought this place. Now make it a café." (§1.1, `DayIntro.LineForDay(1)`). This is yours. P2 doesn't touch `DayIntro`.

## Also yours (Claude's calls; his to overrule)
- **c12, kept by him:** "Nobody is sent to wait standing at the window counter until its stools can be sat on. Next step: make those 3 stools sit, like the repair bar's."
  - The window counter's stools are open on Day 1, so make them sit in your conversion: Snap To Seat on, the body raised the way NPC wave 2's "Wave 2 1 - The repair-bar stools sit" step raises the repair bar's (`Assets/Editor/NpcWave2Steps.cs`, `CafeSeatsSteps.cs`).
  - Then `SeatChoice`'s Standing Wait lifts by itself, and the Officer's favourite (a stool at the window counter) wakes up.
  - Check `NpcSitLiveCheck` ("Sit 4") and Life 4 still hold; their headers explain the window counter's stools.
- **Art finding a80, "Tier 1 shop furniture comes in flat":** give these models the café's grain materials, as the café kit does, so the pieces you sell look like the café's own.
- **Art finding a56, "The banquette's missing back":** the bay's back (your Part 4: "close the back with one plain panel, or in Unity put a thin dark box behind it").
- **The art audit's notes on what you place** (§4.9): read `/root/review24/art-answers-table.md`, and `/root/review23/decisions/art-items.json` for the detail. These findings touch pieces you convert, but are the Unity art builder's, who comes after you:
  - a51 the pale lounge rug;
  - a58 the patio table's glowing top;
  - a59 the boulder plants in the door planters;
  - a61 the chunky patio chairs;
  - a48 the napkin boxes;
  - a43 customers' knees inside the repair bar.

  Don't fix them; just don't make them harder to fix. Keep names stable and say in your report where each piece now lives.

## Who owns what (P2 builds Days 1–3 at the same time)
- **Yours:**
  - every file in §4.10: the furniture runtime, `FurnitureRules`, `DustSheet`, `BanquetteRun`, Arrange mode and its UI, the furniture steps, the data and prefabs;
  - `GameBootstrap`'s one line;
  - `CafeViewMode`'s `OverlayOwnsInput`;
  - `RecapUI`'s hide-and-return;
  - `DayIntro`'s Day 1 line;
  - your labs.
- **P2's:** the days' data and spawner, the four waiting jobs (the side project with its thrift shelf, clearing tables, chat, restock with its back shelf), the seat choice (`WaitingArea`, `SeatChoice`), the support call. P2's two new props (the thrift shelf by the bench, the back shelf behind the drinks machine) are not furniture and can't be moved; your floor map must treat them as fixed, like the repair bar.
- **Shared:** keep your edits small, additive, and marked with a `// 24 Oct 2026 (Furniture)` comment; the integrator merges by hand.
  - `SaveData.cs`, `SaveManager.cs` (your layout; P2's thrift find);
  - `RecapPhone.cs` (your YOUR CAFÉ card and Arrange button in `BuildShop`; P2's thrift find card and Tonight line);
  - `PatronSpawner.cs` (your settled guests' fallback by S's tags and no warning for a switched-off seat; P2's guest numbers);
  - `DayShapeCheck.cs` (your half-finished café menus; P2's day bars and seat medians);
  - `CafeLab.cs` (your new-game lab save);
  - `ControlHints.cs` (additive verbs).
- **The 23 Oct merge changed things since F wrote the spec.** Check every line number it cites before you trust it:
  - J's juice is in `RecapPhone.cs`: use `UiTween` for anything that moves;
  - S's seat tags are merged: `SeatTags.cs`, `SeatTagsSteps.cs`; the scene has the areas laid;
  - D's drinks line is in `BeverageStation.cs` and `ShopUI.cs`;
  - A's aim help is in `AimAssist.cs` and `PlayerInteractor.cs`.

## The scene
You can't open Unity. Your conversion is editor steps (the spec's Furniture 0–3 and their undo), run on his Mac by the integrator after Seat tags 1 and Art tidy 1, which are already in the scene. Get every name and path right from `/root/ref24/Assets/Playtests/AcesCafeLayoutPlaytest.unity` and the house steps that built the café (`CafeFurnishing.cs`, `CafeSeatsSteps.cs`, `CafeLivedInSteps.cs`, `CafeDressingSteps.cs`, `SeatTagsSteps.cs`).
- The NavMesh: the spec says carving, not a runtime re-bake (§4.3). Check what the scene's NavMesh setup really is.
- The dust sheet: the spec allows a Unity quad or Blender. Choose what can be made here and say how it looks.

## The proof on his Mac
List the steps and labs in the order he must run them, with what a pass looks like:
- Furniture 0 (check first) → 1 → 2 → 3;
- save the scene;
- your rules check;
- the place/move/sell lab;
- the save and load lab;
- an old save and a new game;
- the Day 1–3 labs on the half-finished café;
- Sit 4, Life 4, Seat choice rules, Waiting space reservations, Café life.

Say the new expected counts. Mind the ones that move because the café starts half-finished: the Day labs' seat medians are reported, not judged, under 10 seats in 4 zones (S's `RoomAtLeast`).

## Done means
- Parts 1–5 are built, his answers are applied, and c12, a80 and a56 are done.
- `csparse` is clean on every file.
- The report lists every decision his to overrule.
- The report has an exact merge note for each shared file.
