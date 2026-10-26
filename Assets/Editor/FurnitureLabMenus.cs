#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE FURNITURE LABS' MENUS (24 Oct 2026; Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md §5.2-5.5, with his answers
// of 24 Oct)
//
// Each starts one lab Play session (CafeLab: the lab's own save, playtest-cafe-lab.json, never his playtest save) with the
// save its check needs, then the check drives itself and reports ("[Furniture] All N checks passed." or "... FAILED. <folder>"):
//   Fixit Fidget > Furniture > Lab - Place, move and sell     FurnitureCheck: today's café at Day 2's closing, $1,000.
//   Fixit Fidget > Furniture > Lab - Save and load            FurnitureSaveCheck: the same save (an old save: no layout in it).
//   Fixit Fidget > Days > Lab - Day 1 / 2 / 3 at the counter (half-finished café)
//        DayShapeCheck with its day's bars (P2's), the autopilot at human speed, on the café a new game opens (his answer r1:
//        10 seats; CafeLab.HalfFinishedLayout, from the scene), with the week's bought pieces for Days 2 and 3, each where
//        Arrange's ghost first waits at that closing (the rules' twin): Day 1's bistro at (-4.75, 10.25) turned (folding the
//        west banquette's north sheet: 12 seats), Day 2's square table at (-2.25, 9.0) turned (folding the communal table's
//        sheet: 14 seats). Days 2 and 3 start from the save Playtest4SystemsSteps' Days labs give them (Grace's memory, the
//        median first-timer's $369 / $788), less what the furniture cost ($80; $170). At the closing, FurnitureCheck.AtClosing.
// Open the café scene first. Nothing here changes the scene.
// ---------------------------------------------------------------------------
internal static class FurnitureLabMenus
{
    const string Tag = "[Furniture labs] ";
    const string FurnitureMenu = "Fixit Fidget/Furniture/";
    const string DaysMenu = "Fixit Fidget/Days/";
    const string MenuPlace = FurnitureMenu + "Lab - Place, move and sell";
    const string MenuSave = FurnitureMenu + "Lab - Save and load";
    const string MenuDay1 = DaysMenu + "Lab - Day 1 at the counter (half-finished café)";
    const string MenuDay2 = DaysMenu + "Lab - Day 2 at the counter (half-finished café)";
    const string MenuDay3 = DaysMenu + "Lab - Day 3 at the counter (half-finished café)";

    // The week's model (furniture-spec.md §3.3: a bistro at Day 1's closing, a square table at Day 2's), where the ghost first waits.
    static readonly PlacedPieceData DayOneBistro = new PlacedPieceData { id = "p1", kind = "Bistro table", x = -4.75f, z = 10.25f, yaw = 90f };
    static readonly PlacedPieceData DayTwoSquare = new PlacedPieceData { id = "p2", kind = "Square table", x = -2.25f, z = 9f, yaw = 90f };
    const string DayOneSheet = "sheet-2", DayTwoSheet = "sheet-3";
    const int BistroPrice = 80, SquarePrice = 90;

    [MenuItem(MenuPlace, false, 40)]
    static void PlaceMoveSell() => Start(ClosingSave(), true, false, FurnitureCheck.PendingKey, null,
        "Furniture: place, move and sell, on today's café at Day 2's closing with $1,000 (about three minutes: it opens tomorrow and fills the room)");

    [MenuItem(MenuSave, false, 41)]
    static void SaveAndLoad() => Start(ClosingSave(), false, false, FurnitureSaveCheck.PendingKey, null,
        "Furniture: save and load a layout, an old save, a new game (about half a minute)");

    [MenuItem(MenuDay1, false, 33)]
    static void DayOne() => StartDay(1);

    [MenuItem(MenuDay2, false, 34)]
    static void DayTwo() => StartDay(2);

    [MenuItem(MenuDay3, false, 35)]
    static void DayThree() => StartDay(3);

    [MenuItem(MenuPlace, true)]
    [MenuItem(MenuSave, true)]
    [MenuItem(MenuDay1, true)]
    [MenuItem(MenuDay2, true)]
    [MenuItem(MenuDay3, true)]
    static bool CanStart() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Today's café at Day 2's closing (the recap up), the way a save from before the layout existed holds it (cafe format 0).
    static SaveData ClosingSave() => new SaveData
    {
        day = 2,
        money = FurnitureCheck.StartMoney,
        cups = 40,
        beans = 40,
        dayCompleted = true,
        recap = new RecapSaveData { day = 2, closingTill = FurnitureCheck.StartMoney },
    };

    static void StartDay(int day)
    {
        if (!InTheCafe()) return;
        CafeLayout layout = Object.FindAnyObjectByType<CafeLayout>();
        CafeLayoutData cafe = CafeLab.HalfFinishedLayout(layout);
        if (cafe == null || cafe.pieces.Length == 0 || cafe.sheetsLeft.Length == 0)
        {
            Debug.LogError(Tag + "The café has no half-finished layout yet: run Fixit Fidget > Cafe redesign > Furniture 1 and Furniture 3 first.");
            return;
        }
        var pieces = new List<PlacedPieceData>(cafe.pieces);
        var sheets = new List<string>(cafe.sheetsLeft);
        int spent = 0;
        if (day >= 2) { pieces.Add(Copy(DayOneBistro)); sheets.Remove(DayOneSheet); spent += BistroPrice; }
        if (day >= 3) { pieces.Add(Copy(DayTwoSquare)); sheets.Remove(DayTwoSheet); spent += SquarePrice; }
        cafe.pieces = pieces.ToArray();
        cafe.sheetsLeft = sheets.ToArray();
        cafe.nextId = day;

        SaveData save = day <= 1 ? new SaveData() : DaysSave(day, spent);
        save.cafe = cafe;
        Start(save, true, true, DayShapeCheck.PendingKey, () =>
            {
                PlayerPrefs.SetInt(DayShapeCheck.ShipLengthKey, 0);
                PlayerPrefs.SetInt(DayShapeCheck.DayKey, day);
                PlayerPrefs.SetInt(FurnitureCheck.HalfFinishedKey, 1);
            },
            $"Days 1-3 on the half-finished café: Day {day} at the counter ({10 + 2 * (day - 1)} seats), the autopilot at human speed, the scene's day");
    }

    // Days 2 and 3 as Playtest4SystemsSteps.StartDaysLab starts them (keep the two alike: P2 owns these numbers), less the furniture.
    static SaveData DaysSave(int day, int spent)
    {
        var grace = new RegularMemoryData
        {
            profileId = GraceCameraEpisode.ProfileId,
            visits = day - 1,
            relationship = 2 * (day - 1),
            lastSeenDay = day - 1,
            lastVisitHappy = true,
            lastJobAccepted = true,
            lastVisitServed = true,
            lastGrade = nameof(JobGrade.Perfect),
        };
        if (day >= 3)
        {
            grace.graceCameraAttempted = true;
            grace.graceCameraReturned = true;
            grace.graceCameraGrade = nameof(JobGrade.Perfect);
            grace.graceCameraDay = 2;
        }
        int money = (day == 2 ? 369 : 788) - spent;
        return new SaveData { day = day, money = money, cups = 40, beans = 40, dayCompleted = false, regularMemories = new[] { grace } };
    }

    static PlacedPieceData Copy(PlacedPieceData d) => new PlacedPieceData { id = d.id, kind = d.kind, x = d.x, z = d.z, yaw = d.yaw, tier = d.tier, variant = d.variant };

    static bool InTheCafe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (SceneManager.GetActiveScene().path == AcesCafeLayoutSetup.ScenePath) return true;
        Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
        return false;
    }

    // One lab Play session: the lab's own save, the lab switched on, its autopilot as asked, the check's own request.
    static void Start(SaveData save, bool autopilot, bool humanSpeed, string checkKey, Action more, string what)
    {
        if (!InTheCafe()) return;
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, save);
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, autopilot ? 1 : 0);
        if (humanSpeed) PlayerPrefs.SetInt(CafeLab.HumanSpeedKey, 1);
        else PlayerPrefs.DeleteKey(CafeLab.HumanSpeedKey);
        PlayerPrefs.SetInt(checkKey, 1);
        more?.Invoke();
        PlayerPrefs.Save();
        Debug.Log(Tag + what + ". Keep the Game view in front and leave the mouse, keyboard and pad alone until it reports." +
                  $" Lab save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A lab request that never became a Play session (a compile error, say) must not ride along with the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool any = false;
        foreach (string key in new[] { FurnitureCheck.PendingKey, FurnitureSaveCheck.PendingKey, FurnitureCheck.HalfFinishedKey })
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); any = true; }
        if (any) PlayerPrefs.Save();
    }
}
#endif
