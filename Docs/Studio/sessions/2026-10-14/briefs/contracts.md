# Contracts between the builders (14 Oct, wave 2) — fixed; code against them exactly

*Seven builders write at the same time. These are the only places one builder's code calls another's. The owner builds it exactly so; the others call it exactly so. If you are the owner and the spec forces a different shape, keep these signatures as wrappers and say so in your report.*

## 1. `DeviceDefinition` (owner: **bench**)
```csharp
// Added to the existing class (Assets/Scripts/DeviceDefinition.cs). Prefab data, not save data.
[Tooltip("Set by Devices 2 once the prefab is baked at real size (the TD's guard).")]
public bool realSize;
public BenchLayout benchLayout = new BenchLayout();

[System.Serializable]
public class BenchLayout   // mat millimetres, Art's frame: +x the player's right, +y away from him
{
    public Vector2 rest;              // where the device lies (Systems §4.0.1)
    public float toolRestCentreX;     // Systems
    public float toolRestCentreY;     // the Blender lead (phone −60, camera 0, watch −60)
    public Vector2 scrapTray;         // Systems
    public Vector2 cameraAim;         // the TD (§5.2); mat mm
    public float cameraDistance;      // the TD; metres. 0 = unset: the bench keeps the scene's framing
    public Vector2 closeAim;          // the TD (§5.3); mat mm
    public float fovClose;            // the TD; degrees at zoom 1. 0 = unset
}
```

## 2. `BenchStage` (owner: **bench**) — public fields, read by Bench 9 (scale) and Bench 6 (uiux)
`public static BenchStage Instance { get; }`, and public fields `Transform matFrame; Transform[] cups (6); Transform padSmall, padLarge, wellSmall, wellLarge; Transform scrapTray; Transform[] scrapSlots (3); Transform toolRest; Transform[] toolSlots (5, ToolType order)`; `public Vector3 MatToWorld(Vector2 mm, float heightMetres = 0f)`.

## 3. `BenchStageContract` (owner: **bench**; new `Assets/Scripts/BenchStageContract.cs`)
```csharp
public static class BenchStageContract   // UI/UX §5.8's zones the 3D bench must leave free
{
    public enum Zone { U1, U2, U3, U4, U5, U6 }
    public static readonly Zone[] All = { Zone.U1, Zone.U2, Zone.U3, Zone.U4, Zone.U5, Zone.U6 };
    // Pixels, Unity screen space (origin bottom-left), for the given screen size; both 16:9 and 16:10 handled.
    public static Rect Rect(Zone zone, int screenWidth, int screenHeight);
}
```

## 4. `BenchWork` (owner: **bench**; new `Assets/Scripts/BenchWork.cs`, Systems §4.5)
```csharp
public static class BenchWork
{
    public enum Kind { Screw, Hole, Cover, Part, FreshPart, Spot }
    public struct Item { public Component target; public Kind kind; public Vector2 screenCentre; public float screenRadius; public bool hidden; public float remaining; }
    public static event System.Action<JobBase, float> Flashed;        // after Flash
    public static event System.Action<JobBase> Progressed;            // throttled to 4 per second
    public static event System.Action<JobBase, Component> TargetDone; // the frame a target finishes
    public static void Flash(JobBase job, float seconds = 1.5f);
    public static void WorkLeft(JobBase job, List<Item> into);        // clears `into` first
}
```

## 5. `RepairChecklist.Step` (owner: **bench**) — added fields (UI/UX §5.10 item 2)
`public string verb; public string icon; public List<Component> targets = new List<Component>(); public int left; public int total;` — `icon` is the Resources name under `UI/BenchIcons/` (`screwdriver`, `pry`, `tweezers`, `brush`, `cloth`, `screw`, `newpart`, `cover`, or a device-specific `part_*` / `cover_*` when one exists, else the generic).

## 6. `ItemInspector` (owner: **bench**)
Keeps `public static bool Working`, `public static ItemInspector Instance`, `public bool IsHoldingItem`. Adds `public bool IsSeen(Transform thing)` (the existing private `Seen` ray, centre = the thing's renderer bounds centre) for BenchGlow. Keeps every public member other files read today (grep before changing one).

## 7. `PatronSpawner` (owner: **npc**)
`public static void SeatOpeningGuestsNow()` — finds the scene's spawner and seats today's opening guests at once; idempotent; a no-op on a day with none. **cine** calls it from `DayIntro.HoldTheWorld`, one line before the spawner is disabled (NPC §2.1); **npc** also calls it at the open when the intro is Off or skipped (guarded by the idempotence).

## 8. Files the importers deliver (owner: **imports**; the paths are fixed)
- `Assets/Art/Models/BenchStage/BM_RepairMat.fbx` (+ `bench_stage.json`), empties `Cup_1..6`, `Pad_Small`, `Pad_Large`, `Well_Small`, `Well_Large`, built in the bench rig's own frame (import with no turn).
- `Assets/Art/Models/BenchTools/BT_ToolRest.fbx` (empties `Seat_Screwdriver`, `Seat_Pry`, `Seat_Tweezers`, `Seat_Brush`, `Seat_Cloth`), `BT_ScrapTray.fbx` (`Slot_1..3`), `BT_ClothHeld.fbx` (blend shapes `Press`, `Bunch`), `BT_Brush.fbx` (blend shape `Bend`), `BT_Cloth.fbx`, `BT_Screwdriver.fbx`, `BT_PryTool.fbx`, `BT_Tweezers.fbx`.
- Materials made by "Bench stage 1" in `Assets/Art/Materials/Bench/` (`BM_Silicone`, `BM_Recess`, `BM_Pad`, `BM_Print`, `BM_Ink`, `BM_Steel`).
- Icons: `Assets/Data/Resources/UI/BenchIcons/<name>.png` → `Resources.Load<Sprite>("UI/BenchIcons/<name>")`.
- The screw-hole decal (from the Blender lead's wave 2, later): `Assets/Data/Resources/Bench/BT_ScrewHole.png` → `Resources.Load<Texture2D>("Bench/BT_ScrewHole")`; code must fall back to a generated texture while it's missing.
- Sounds: `Assets/Audio/CC0/…`; every cue in Audio's table (`audio-bench-sounds.md` §3) has a `SoundBank.Defaults()` entry (imports). Code calls cues only by those names.

## 9. The scene's objects
Nobody renames a scene object another script finds by name. New scene objects are made by your own editor step under a parent named for your feature (e.g. `InspectRig/Stage (v2)` for the bench builder).

## 10. Who owns which file (edit only your own; everything else goes as a patch in your report)
Paths are under `Assets/Scripts/` unless shown; find each file with `git ls-files | grep`. "New" files may be created only by their owner. A file not listed belongs to nobody this wave: don't edit it; if you must, put the change in your report as a patch.

| Builder | Worktree | Owns |
|---|---|---|
| **bench** (Systems A1–A3 + the TD's ItemInspector camera/zoom + UI/UX's ItemInspector and RepairChecklist items + Audio's code hooks) | `/root/cafe/build/bench` | `BenchRules.cs` (new), `BenchStage.cs`, `BenchStageContract.cs` (new), `BenchWork.cs` (new), `BenchFeel.cs` (new), `DeviceDefinition.cs`, `Screw.cs`, `RemovablePart.cs`, `ReplaceablePart.cs` (+ `LoosePart`), `GrimeSpot.cs`, `ToolPickup.cs`, `PadInput.cs`, `PadCursor.cs`, `GamePointer.cs`, `GameSettings.cs`, `ItemInspector.cs`, `RepairChecklist.cs`, `JobBase.cs`, `Juice.cs`, `BenchInteractable.cs`, `InspectableItem.cs`; `Diagnostics/ClumsyHand.cs` (new), `Diagnostics/ClumsyBenchLab.cs` (new), `BenchLab.cs`, `BenchViewProbe.cs`, `AimHelpCheck.cs`, `JuiceCheck.cs`, `StationsCheck.cs`, `DeviceCleaningReachabilityChecks.cs`; `Assets/Editor/ForgivingBenchSteps.cs` (new), `Assets/Editor/Playtest3Session4Steps.cs`, `Assets/Editor/BenchViewProbeMenu.cs`; `Tests/BenchRules/` (new) |
| **scale** (the TD's one scale, editor side) | `/root/cafe/build/scale` | `Assets/Editor/HeroDeviceSteps.cs`, `Assets/Editor/BenchScaleAudit.cs` (new), `Assets/Editor/BenchFramingSteps.cs` (new), an intake-tray check (new), `PlayerCarry.cs`, `JobMarker.cs`, and only the refuse-on-real-size guards in `BenchFeelSteps.cs`, `GraceShowcaseSetup.cs`, `FaultExpansionSetup.cs`, `Playtest3Session3Steps.cs`, `Playtest4ProducerSteps.cs` |
| **days** (Days 1–3, Grace's two visits, Systems step 11) | `/root/cafe/build/days` | `GraceCameraRepairJob.cs`, `GraceCameraEpisode.cs`, `CustomerIdentity.cs`, `NotebookHooks.cs`, `NotebookEntries.cs`, `DeviceWake.cs`, `DayDefinition.cs`, `DayClock.cs`, `CustomerSpawner.cs`, `CustomerVisitRoster.cs`, `CustomerMemoryService.cs`, `FaultSpeech.cs`, `SaveManager.cs` (when a visit is recorded only; no field change), `GracePhotoDisplay.cs`, `FeaturedRepairRequest.cs`, `UpgradeEconomyPass.cs`; the Day assets (`Assets/Data/Days/*`, `Assets/GraceShowcase/*.asset`), `Assets/Data/Regulars/*.asset`; checks `ContinuationRuleChecks`, `Tests/ContinuationRules`, `DayOneOnboardingChecks`, `DayShapeCheck`, `UpgradeEconomyChecks`, `Day1GuideCheck`, `GraceRepairInteractionChecks`, `ContinuationIntegrationChecks`, `Playtest4SystemsSteps`, `CafeLifeRecorder`, `NightTwoCheck`, `NightTwoSteps`, `NightOneSteps`; `Assets/Editor/DaysOneToThreeSteps.cs` (new) |
| **npc** (stools, settled guests, looks, less robotic) | `/root/cafe/build/npc` | `NpcSeating.cs`, `WaitingArea.cs`, `TableSeat.cs`, `PatronBrain.cs`, `PatronSpawner.cs`, `CustomerBrain.cs`, `PolygonNpcVisual.cs`, `NpcSocial.cs`, `NpcVisualVariants.cs`, `NpcLocomotion.cs`, `NpcAttentionDirector.cs`, `CounterQueue.cs`, `PolygonNpcSetup.cs`, `PolygonCityDressing.cs`, `CityPackCatalog.cs`, `GraceAtHome.cs`, `Assets/Editor/CafeSeatsSteps.cs`, `AcesCafeLayoutSetup.cs`, `NpcSitLiveCheck.cs`, `WaitingSpaceChecks.cs`, `NpcSitData.asset`, `Assets/Editor/NpcWave2Steps.cs` (new) |
| **cine** (intro v2, the drink-station view, the night zoom) | `/root/cafe/build/intro` | `DayIntro.cs`, `DayIntroCheck.cs`, `CafeViewMode.cs`, `Lodger.cs`, `NightZoomCheck.cs` (new), `PlayerInteractor.cs`, `StationInteractable.cs`, `BeverageStation.cs`, `BeverageLook.cs`, `BeverageStationSetup.cs`, `DispenserFrameCheck.cs`, `ControllerChecks.cs`, `Playtest4BenchSteps.cs`, `PauseCheck.cs`; `NightZeroSet.cs`, `NightWalk.cs`, `NightTour.cs`, `NightSeeThrough.cs`, `NightCycle.cs` (only for the zoom); `Assets/Editor/CinematicsWave2Steps.cs` (new) |
| **imports** (Blender wave 1 into Unity; the sounds into Unity) | `/root/cafe/build/imports` | `Assets/Editor/BlenderKitSteps.cs`, `Assets/Editor/CafeLivedInSteps.cs`, `Assets/Editor/BenchStageImportSteps.cs` (new), `Assets/Editor/CafeDressingSteps.cs` (new), `SoundBank.cs`, `SoundSetup.cs`, `SoundPlayer.cs`, `PlaceholderSounds.cs`, `CafeSoundscape.cs`, the audio and bank assets, `Tools/Blender/*` (copied in), every art and audio file it copies in, `.gitignore` |
| **uiux** (checklist v2) | `/root/cafe/build/uiux` | `WordBudget.cs`, `BenchCards.cs`, `BenchGlow.cs` (new), `BenchOutline.cs`, `Assets/Data/Resources/Bench outline.shader`, `ShopUI.cs`, `DayOneGuideUI.cs`, `DropInteractable.cs`, `TicketRailUI.cs`, `JobTicket.cs`, `HudCorners.cs`, `UiSkin.cs`, `ControlHints.cs`, `PausePhone.cs`, `Diagnostics/BenchWordsCheck.cs` (new), `BenchWordsChecks.cs` (new, edit-mode), `Assets/Editor/BenchWordsMenu.cs` (new) |

The scene `Assets/Playtests/AcesCafeLayoutPlaytest.unity` and the device prefabs are changed only by editor steps, which the Producer runs one at a time.
