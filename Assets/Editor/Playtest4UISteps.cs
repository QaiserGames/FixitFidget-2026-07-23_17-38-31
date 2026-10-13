#if UNITY_EDITOR
using System;
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
//     customer's patience bar and the devices' job markers. Since 12 Oct it also reports the patience bar's family (one
//     number, UiPins.BarScale: the bar, the job's dot, the names over heads).
//   Fixit Fidget > Playtest 4 > UI > 1 - Day 1 guide - play check (lab, drives itself)
//     A fresh Day 1 in the lab (the lab's own save; your playtest save is not used), driven by Day1GuideCheck: the cards,
//     the pins, the names and the bars at each step of Grace's visit (and the Officer's coffee, if he comes in time),
//     photographed at 1920 x 1080, with close-ups of her head (name, bar, dot) since 12 Oct. About three minutes. Report and
//     photos: Logs/UI/day1-guide-<time>/. Play mode ends by itself when it reports.
//   Fixit Fidget > Playtest 4 > UI > 2 - Settings: the bench's sticks and the morning (edit mode, instant)   (12-13 Oct)
//     The two settings the other leads read, as the Producer's contract has them: GameSettings.SwapBenchSticks (off unless
//     turned on; FixitFidget.Settings.SwapBenchSticks, 0/1) and GameSettings.DayIntro (DayIntroMode Full, Short, Off: Full
//     unless chosen; FixitFidget.Settings.DayIntro, 0/1/2; a number it doesn't know reads as Full). Each value goes in and
//     reads back after GameSettings.Reload, and Describe() names them. PlayerPrefs only, put back exactly as they were
//     (a key that wasn't there is removed again). Report: Logs/Playtest4/ui-2-settings-<time>.txt.
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
            $"Customer.prefab has its PatienceBar (drawn {UiPins.BarWidth:0} x {UiPins.BarHeight:0} px over the head while playing; the strip in the world is put away)");
        foreach (string path in new[] { "Assets/AssetsPrefabs/PocketWatch.prefab", "Assets/AssetsPrefabs/PhoneRepair.prefab", "Assets/AssetsPrefabs/DrinkCup.prefab" })
        {
            GameObject device = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            run.Check(device != null && device.GetComponentInChildren<JobMarker>(true) != null,
                $"{path} has its JobMarker (a colour tag while playing; its \"#1\" label never shows)");
        }

        // The patience bar's family (12 Oct, the findings' §4): one number sets it. Whole pixels, the dot standing out beside
        // the bar, the names over heads bigger than both, and the bar no smaller than it was on 10 Oct.
        run.Info($"the patience bar's family at 1080p: {UiPins.Family()}; 1.3 is 72 x 9, dot 12, name 20; 1.5 is 84 x 10, dot 14, name 22");
        run.Check(Whole(UiPins.BarWidth) && Whole(UiPins.BarHeight) && Whole(UiPins.TagDot) && Whole(UiPins.NameSize) && Whole(UiPins.PinTextSize),
            "every size in it is a whole pixel");
        run.Check(UiPins.BarWidth >= 56f && UiPins.BarHeight >= 7f && UiPins.TagDot > UiPins.BarHeight && UiPins.NameSize > UiPins.TagDot
                  && UiPins.PinTextSize < UiPins.NameSize && UiPins.NameSize - UiPins.PinTextSize <= 1f,
            "the bar at least 10 Oct's 56 x 7; the dot taller than the bar; the name bigger than the dot; Day 1's pins a pixel under the name");
    });

    static bool Whole(float pixels) => Mathf.Abs(pixels - Mathf.Round(pixels)) < .001f;

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

    // ------------------------------------------------------------------ 2: the two settings (12-13 Oct)

    [MenuItem(Menu + "2 - Settings: the bench's sticks and the morning (edit mode, instant)")]
    static void SettingsCheck()
    {
        var run = new Playtest4Shared.Run("UI - the bench's sticks and the morning", "ui-2-settings");
        run.Text.AppendLine($"{run.Title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})   PlayerPrefs only, put back as they were");
        string swapKey = GameSettings.SwapBenchSticksKey, introKey = GameSettings.DayIntroKey;
        bool hadSwap = PlayerPrefs.HasKey(swapKey), hadIntro = PlayerPrefs.HasKey(introKey);
        int swapWas = PlayerPrefs.GetInt(swapKey, 0), introWas = PlayerPrefs.GetInt(introKey, 0);
        run.Info($"as found: {swapKey} {(hadSwap ? swapWas.ToString() : "(not set)")}, {introKey} {(hadIntro ? introWas.ToString() : "(not set)")}");
        try
        {
            run.Section("The contract (the Cinematics and Systems A leads read these names)");
            run.Check(swapKey == "FixitFidget.Settings.SwapBenchSticks" && introKey == "FixitFidget.Settings.DayIntro",
                $"kept under {swapKey} and {introKey}");
            run.Check((int)DayIntroMode.Full == 0 && (int)DayIntroMode.Short == 1 && (int)DayIntroMode.Off == 2
                      && Enum.GetNames(typeof(DayIntroMode)).SequenceEqual(new[] { "Full", "Short", "Off" }),
                "DayIntroMode is Full, Short, Off, kept as 0, 1, 2");

            run.Section("A machine that never set them");
            PlayerPrefs.DeleteKey(swapKey);
            PlayerPrefs.DeleteKey(introKey);
            GameSettings.Reload();
            run.Check(!GameSettings.SwapBenchSticks, "the bench's sticks as they are (SwapBenchSticks off)");
            run.Check(GameSettings.DayIntro == DayIntroMode.Full, "the whole morning (DayIntro Full)");

            run.Section("Each value in, and read back as kept");
            foreach (bool on in new[] { true, false })
            {
                GameSettings.SwapBenchSticks = on;
                int kept = PlayerPrefs.GetInt(swapKey, -1);
                GameSettings.Reload();
                run.Check(GameSettings.SwapBenchSticks == on && kept == (on ? 1 : 0), $"SwapBenchSticks {(on ? "on" : "off")}: kept as {kept}, read back {(GameSettings.SwapBenchSticks ? "on" : "off")}");
            }
            foreach (DayIntroMode mode in new[] { DayIntroMode.Short, DayIntroMode.Off, DayIntroMode.Full })
            {
                GameSettings.DayIntro = mode;
                int kept = PlayerPrefs.GetInt(introKey, -1);
                GameSettings.Reload();
                run.Check(GameSettings.DayIntro == mode && kept == (int)mode, $"DayIntro {mode}: kept as {kept}, read back {GameSettings.DayIntro}");
            }

            run.Section("What it does with a number it doesn't know");
            foreach (int stray in new[] { 3, 7, -1 })
            {
                PlayerPrefs.SetInt(introKey, stray);
                GameSettings.Reload();
                run.Check(GameSettings.DayIntro == DayIntroMode.Full, $"a kept {stray} reads as Full");
            }
            GameSettings.DayIntro = (DayIntroMode)9;
            run.Check(GameSettings.DayIntro == DayIntroMode.Full && PlayerPrefs.GetInt(introKey, -1) == (int)DayIntroMode.Full,
                "a mode it doesn't know, set in code, is kept as Full");

            run.Section("The report line");
            GameSettings.SwapBenchSticks = true;
            GameSettings.DayIntro = DayIntroMode.Short;
            string said = GameSettings.Describe();
            run.Check(said.Contains("swap the sticks at the bench on") && said.Contains("the morning Short"), $"Describe() names them: \"{said}\"");
        }
        catch (Exception e)
        {
            run.Check(false, "the check ran to its end (it stopped: " + e.Message + ")");
        }
        finally
        {
            if (hadSwap) PlayerPrefs.SetInt(swapKey, swapWas);
            else PlayerPrefs.DeleteKey(swapKey);
            if (hadIntro) PlayerPrefs.SetInt(introKey, introWas);
            else PlayerPrefs.DeleteKey(introKey);
            PlayerPrefs.Save();
            GameSettings.Reload();
            run.Info($"put back: {swapKey} {(PlayerPrefs.HasKey(swapKey) ? PlayerPrefs.GetInt(swapKey).ToString() : "(not set)")}, " +
                     $"{introKey} {(PlayerPrefs.HasKey(introKey) ? PlayerPrefs.GetInt(introKey).ToString() : "(not set)")}");
        }
        run.Text.AppendLine().AppendLine($"{run.Passes} passed, {run.Failures} failed.");
        string report = Playtest4Shared.Write(run);
        if (run.Failures == 0) Debug.Log($"[Playtest 4] UI - settings: PASS, {run.Passes} checks. Report: {report}");
        else Debug.LogError($"[Playtest 4] UI - settings: {run.Failures} of {run.Passes + run.Failures} FAILED. Report: {report}");
    }

    [MenuItem(Menu + "2 - Settings: the bench's sticks and the morning (edit mode, instant)", true)]
    static bool CanSettingsCheck() => Playtest4Shared.NotPlaying();

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
