#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4: THE UI LEAD'S STEPS AND LAB (10 Oct 2026)
//
//   Fixit Fidget > Playtest 4 > UI > 0 - Check first (read-only)
//     Nothing in the scene changes for the UI fixes: the pins over the café (names, patience bars, Day 1's pins), the
//     guide's cards, the job colour tags and the HUD's words are all made or changed while playing. This survey says the
//     scene and prefabs have what they hang from: the HUD's wiring, the scene's Day 1 (guided), its stations, the
//     customer's patience bar and the devices' job markers.
//   Fixit Fidget > Playtest 4 > UI > 1 - Day 1 guide - play check (lab, drives itself)
//     A fresh Day 1 in the lab (the lab's own save; your playtest save is not used), driven by Day1GuideCheck: the cards,
//     the pins, the names and the bars at each step of Grace's visit (and the Officer's coffee, if he comes in time),
//     photographed at 1920 x 1080. About three minutes. Report and photos: Logs/UI/day1-guide-<time>/. Play mode ends by
//     itself when it reports.
// ---------------------------------------------------------------------------
public static class Playtest4UISteps
{
    const string Menu = Playtest4Shared.Menu + "UI/";
    const string Tag = "[Playtest 4 UI] ";

    [MenuItem(Menu + "0 - Check first (read-only)")]
    static void CheckFirst() => Playtest4Shared.Survey("UI - check first", "ui-0-check-first", run =>
    {
        run.Info("Nothing in the scene changes for the UI fixes: what follows is what they hang from.");

        ShopUI hud = Playtest4Shared.One<ShopUI>(run);
        var hudFields = new SerializedObject(hud);
        foreach (string field in new[] { "promptText", "viewHintText", "interactor", "inspector", "conversation" })
        {
            SerializedProperty p = hudFields.FindProperty(field);
            run.Check(p != null && p.objectReferenceValue != null,
                $"ShopUI.{field} is wired ({Playtest4Shared.Describe(p != null ? p.objectReferenceValue : null)})");
        }
        FocusNameUI focus = Playtest4Shared.One<FocusNameUI>(run);
        var focusFields = new SerializedObject(focus);
        foreach (string field in new[] { "label", "group", "interactor" })
        {
            SerializedProperty p = focusFields.FindProperty(field);
            run.Check(p != null && p.objectReferenceValue != null, $"FocusNameUI.{field} is wired (the crosshair's name in first person)");
        }

        CustomerSpawner spawner = Playtest4Shared.One<CustomerSpawner>(run);
        SerializedProperty schedule = new SerializedObject(spawner).FindProperty("schedule");
        DayDefinition first = schedule != null && schedule.isArray && schedule.arraySize > 0
            ? schedule.GetArrayElementAtIndex(0).objectReferenceValue as DayDefinition : null;
        run.Check(first != null && first.dayNumber == 1 && (first.GuidesFeaturedVisitOn(1) || first.GuidesOpeningOn(1)),
            $"the scene's Day 1 ({Playtest4Shared.Describe(first)}) is a guided day: the guide's cards and pins run on it");
        if (first != null)
        {
            run.Info($"its cards are in full {first.openingHintDuration:0.#} s, then compact; back in full after {DayOneHintTimer.StuckAfter:0} s more when stuck");
            foreach (StoryVisit visit in first.StoryVisitsOn(1))
                if (visit != null && visit.who != null)
                    run.Info($"also on Day 1: {visit.who.characterName} at {visit.arrivesAt:0.00} of the day ({visit.who.primaryVisitKind}" +
                             $"{(visit.who.preferredDrink != null ? ", " + visit.who.preferredDrink.drinkName : "")}): the guide's cards follow that visit too");
        }

        StationInteractable[] stations = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Include);
        run.Check(stations.Any(s => s != null && s.IsWorkSurface && s.StandPoint != null), $"a repair bench with a stand point (pinned \"{FocusNameUI.BenchName}\")");
        run.Check(stations.Any(s => s != null && s.GetComponent<BeverageStation>() != null), $"a drinks machine (pinned \"{FocusNameUI.DrinksName}\")");
        run.Check(Object.FindObjectsByType<DropSpot>(FindObjectsInactive.Include).Any(d => d != null && d.Kind == DropSpot.SpotKind.Counter),
            $"the counter's drop spot (pinned \"{FocusNameUI.TrayName}\")");

        GameObject customer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
        run.Check(customer != null && customer.GetComponentInChildren<PatienceBar>(true) != null,
            "Customer.prefab has its PatienceBar (drawn 56 x 7 px over the head while playing; the strip in the world is put away)");
        foreach (string path in new[] { "Assets/AssetsPrefabs/PocketWatch.prefab", "Assets/AssetsPrefabs/PhoneRepair.prefab", "Assets/AssetsPrefabs/DrinkCup.prefab" })
        {
            GameObject device = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            run.Check(device != null && device.GetComponentInChildren<JobMarker>(true) != null,
                $"{path} has its JobMarker (a colour tag while playing; its \"#1\" label never shows)");
        }
    });

    [MenuItem(Menu + "0 - Check first (read-only)", true)]
    static bool CanCheckFirst() => Playtest4Shared.NotPlaying();

    [MenuItem(Menu + "1 - Day 1 guide - play check (lab, drives itself)")]
    static void PlayDayOneGuide()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(Day1GuideCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Day 1 guide play check: a fresh Day 1 in the lab that drives Grace's visit by itself (and the Officer's " +
                  "coffee if he comes in time), about three minutes. Keep the Game view in front (16:9 is best) and leave the " +
                  $"mouse and keyboard alone. Test save {path}; your playtest save is not used. Play mode ends by itself.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "1 - Day 1 guide - play check (lab, drives itself)", true)]
    static bool CanPlayDayOneGuide() => Playtest4Shared.NotPlaying();

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(Day1GuideCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(Day1GuideCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
