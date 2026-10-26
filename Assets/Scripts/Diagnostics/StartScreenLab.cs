using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE START SCREEN - PLAY CHECK (26 Oct 2026; review24 spec S, "The lab")
//
//   Fixit Fidget › Start screen › Lab - The start screen (drives itself)   (StartScreenSteps writes the lab save and starts it)
//
// A lab session in the lab's own save (playtest-cafe-lab.json; his playtest save is never read or written), the autopilot off,
// the morning's intro forced on, and the start screen asked for by its lab key (TitleScreen.ShouldShow lets it show in this
// lab session and no other). One Play session, both faces:
//
//   SEED 1, a lab save on Day 3 with 2 stars: the title shows as the diorama; the first item is selected the moment the menu
//   appears; Continue's sub-line is "Day 3 · Known on the street"; the camera orbits (it turns and moves); nothing reaches
//   the disk while it is up (the save's own writer is asked to write, and refuses; the file's bytes and time are unchanged; no
//   customer, a still clock, the takings as loaded); photos 01-diorama and 02-menu-selected (one step down: New Game honey).
//   Continue (a pad's Cross) reloads the scene into Day 3 at its checkpoint: the intro plays, time scale is 1, the clock runs.
//   SEED 6: the lab save is taken away (the lab's own file only) and the title asked for once more (TitleScreen.AgainForLab):
//   the cold open, a full café with a queue (photo 03-cold-open-live), frozen within 4 s at time scale 0 (04-cold-open-frozen),
//   no Continue, New Game selected; New Game plays "Before it got like this." (05-rewind) and Day 1 begins in the
//   half-finished café (a new game: CafeLayout's NewGame) with its intro (06-day-1 after it), time scale 1, no file written.
//   Words: every photo of the title measures its own words (ReviewScreen, as every lab photo does); the title's own texts
//   (TitleMenuUI's) must have no S1 or S2, none under 22 px and contrast 4.5 or more where the background is known and the
//   text has no outline.
//
// Report and photos: Logs/StartScreen/check-<time>/. Its last line: "[Start screen] All N checks passed." or
// "[Start screen] N of M checks FAILED. <folder>". The lab's object outlives the scene's reloads (DontDestroyOnLoad).
// ---------------------------------------------------------------------------
public sealed class StartScreenLab : PlayLab
{
    public const string PendingKey = "FixitFidget.StartScreen.Check";
    public const int LabDay = 3, LabStars = 2, LabReputation = 40, LabMoney = 240;

    protected override string Title => "The start screen (the diorama, the cold open, the rewind) - play check";
    protected override string Tag => "[Start screen]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Start screen] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        // The settled guests sit in this lab session too (labs skip them unless asked), and the morning's intro plays after
        // Continue and New Game, as in the real game.
        PatronSpawner.ForceOpeningGuests = true;
        DayIntro.ForceOn = true;
        var go = new GameObject("Start screen check (this Play session only)");
        DontDestroyOnLoad(go);
        go.AddComponent<StartScreenLab>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "StartScreen",
            $"check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    static string LabSavePath => Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);

    protected override void Restore()
    {
        DayIntro.ForceOn = false;
        PatronSpawner.ForceOpeningGuests = false;
        Time.timeScale = 1f;
    }

    protected override IEnumerator Run()
    {
        Check(CafeLab.Active && SaveManagerFile() == CafeLab.SaveFileName,
            $"a lab session on the lab's own save ({CafeLab.SaveFileName}; his playtest save is not used)");
        yield return Seed1();
        yield return Seed6();
    }

    // ------------------------------------------------------------------ Seed 1: the diorama

    IEnumerator Seed1()
    {
        yield return Until(() => TitleScreen.Up && TitleScreen.Current == TitleScreen.Mode.Diorama && TitleScreen.Instance != null
            && TitleScreen.Instance.Menu != null && TitleScreen.Instance.Menu.MenuShowing, 15f, "with a save, the title shows as the diorama");
        if (!lastWait) yield break;
        TitleScreen title = TitleScreen.Instance;
        TitleMenuUI menu = title.Menu;
        List<string> items = menu.Items;
        Check(string.Join(",", items) == "Continue,New Game,Settings,Credits,Quit", $"the menu: {string.Join(" · ", items)}");
        Check(menu.SelectedAtShow == 0, $"the first item is selected the moment the menu appears (slab {menu.SelectedAtShow})");
        string want = $"Day {LabDay} · {ReputationRules.NameOf(LabStars)}";
        Check(menu.ContinueSubLine == want, $"Continue's sub-line reads \"{menu.ContinueSubLine}\" (want \"{want}\")");
        Check(title.Tier == TitleScreen.TierFor(LabStars) && title.Tier == 0,
            $"the camera's framing is the save's tier ({title.Tier}: {TitleScreen.Framings[title.Tier].what})");
        Check(menu.StripText.Contains(ControlHints.Confirm) && menu.StripText.Contains(ControlHints.Back) && !menu.StripText.Contains("["),
            $"the controls strip uses the hints' icons (\"{Short(menu.StripText)}\")");

        // Nothing reaches the disk: the save's own writer is asked to write, and refuses.
        FileInfo before = new FileInfo(LabSavePath);
        bool existed = before.Exists;
        long bytes = existed ? before.Length : -1;
        DateTime written = existed ? before.LastWriteTimeUtc : DateTime.MinValue;
        int filesBefore = FilesBeside();
        bool refused = false;
        try
        {
            MethodInfo commit = typeof(SaveManager).GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic);
            refused = commit != null && SaveManager.Instance != null && !(bool)commit.Invoke(SaveManager.Instance, new object[] { SaveManager.Instance.Loaded });
        }
        catch (Exception e) { Note("asking the save to write failed: " + e.Message); }
        Check(existed && refused, "while the title is up the save's own writer refuses (SaveManager.Commit)");

        int moneyAt = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : -1;
        float clockAt = DayClock.Instance != null ? DayClock.Instance.TimeRemaining : -1f;
        float yawAt = title.OrbitYaw;
        Vector3 camAt = title.CameraTransform != null ? title.CameraTransform.position : Vector3.zero;
        yield return Seconds(3.5f);
        yield return Photo("01-diorama");
        yield return WordCheck("01-diorama");
        float turned = Mathf.Abs(Mathf.DeltaAngle(yawAt, title.OrbitYaw));
        float moved = title.CameraTransform != null ? Vector3.Distance(camAt, title.CameraTransform.position) : 0f;
        Check(turned > 2f && turned < 20f && moved > .5f, $"the camera orbits: {turned:0.0}° and {moved:0.0} m in 3.5 s (one turn every 120 s, eased in)");
        Check(title.CameraTransform != null && title.CameraTransform.position.y > 20f, $"...pulled high ({(title.CameraTransform != null ? title.CameraTransform.position.y : 0f):0} m up)");
        Check(FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Length == 0, "no customer comes to the counter");
        Check(DayClock.Instance != null && !DayClock.Instance.enabled && Mathf.Approximately(DayClock.Instance.TimeRemaining, clockAt), "the clock doesn't run");
        Check(ShopEconomy.Instance != null && ShopEconomy.Instance.Money == moneyAt && moneyAt == LabMoney, $"no money changes (${moneyAt})");
        Note($"{FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Length} patrons in the café and coming in");

        // One step down: New Game takes the honey.
        PlugInPad("Start screen check pad");
        yield return PadPress(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown);
        yield return Seconds(.35f);
        Check(menu.SelectedId == TitleMenuUI.NewGameId, $"the D-pad moves the selection down (now {menu.SelectedId})");
        yield return Photo("02-menu-selected");
        yield return WordCheck("02-menu-selected");
        yield return PadPress(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadUp);
        yield return Seconds(.3f);
        Check(menu.SelectedId == TitleMenuUI.ContinueId, "...and back up to Continue");

        FileInfo after = new FileInfo(LabSavePath);
        Check(after.Exists && after.Length == bytes && after.LastWriteTimeUtc == written && FilesBeside() == filesBefore,
            "no save file was written while the title was up (the lab save's bytes and time, and the folder, as they were)");

        // Continue: the dive, the fade, the reload, the day at its checkpoint.
        int reloads = TitleScreen.Reloads;
        yield return PadPress(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        yield return Until(() => !TitleScreen.Up && TitleScreen.Reloads > reloads && DayClock.Instance != null, 10f, "Continue leaves the title and reloads the scene");
        if (!lastWait) yield break;
        yield return Until(() => DayIntro.Playing, 6f, "...the morning's intro plays as now");
        DayIntro.RequestSkip();
        yield return Until(() => !DayIntro.Playing, 15f, "...and gives the day back");
        yield return Seconds(1f);
        float t0 = DayClock.Instance != null ? DayClock.Instance.TimeRemaining : 0f;
        yield return Seconds(1f);
        DayClock clock = DayClock.Instance;
        Check(clock != null && clock.Day == LabDay && clock.enabled && clock.IsOpen && clock.TimeRemaining < t0,
            $"the day is Day {(clock != null ? clock.Day : 0)} at its checkpoint and its clock runs");
        Check(Mathf.Approximately(Time.timeScale, 1f), $"time scale is {Time.timeScale:0.##}");
        Check(SaveManager.Instance != null && SaveManager.Instance.HasSave && ShopEconomy.Instance != null && ShopEconomy.Instance.Money == LabMoney,
            "the save loaded as it was (the takings as saved)");
        Check(!TitleScreen.Up && TitleScreen.Instance != null && !TitleScreen.Instance.enabled, "the title stays away for the rest of the session");
        FileInfo still = new FileInfo(LabSavePath);
        Check(still.Exists && still.Length == bytes && still.LastWriteTimeUtc == written, "the lab save is still untouched");
    }

    // ------------------------------------------------------------------ Seed 6: the cold open

    IEnumerator Seed6()
    {
        // The lab's own file, and only in its lab session: a first start with no save.
        if (CafeLab.Active && SaveManagerFile() == CafeLab.SaveFileName && File.Exists(LabSavePath)) File.Delete(LabSavePath);
        Check(!File.Exists(LabSavePath), "the lab save is taken away (a first start: no save at all)");
        bool again = false;
#if UNITY_EDITOR
        again = TitleScreen.AgainForLab();
#endif
        Check(again, "the title is asked for once more in this lab session (the scene reloads)");
        if (!again) yield break;
        yield return Until(() => TitleScreen.Up && TitleScreen.Current == TitleScreen.Mode.ColdOpen && TitleScreen.Instance != null, 15f,
            "with no save, the title is the cold open");
        if (!lastWait) yield break;
        float shownAt = Time.realtimeSinceStartup;
        TitleScreen title = TitleScreen.Instance;
        yield return Seconds(1.4f);
        Check(!TitleScreen.Frozen && Time.timeScale > .5f, "it is live at first (the rush moves)");
        Check(title.CrowdSeated >= 8 && title.CrowdQueued >= 1, $"mid-rush: {title.CrowdSeated} seated, {title.CrowdQueued} in the queue");
        Check(CafeLayout.Instance == null || CafeLayout.Instance.AppliedFrom == CafeLayout.Source.OldSave,
            $"today's full café, in memory ({(CafeLayout.Instance != null ? CafeLayout.Instance.Seats : 0)} seats)");
        yield return Photo("03-cold-open-live");
        yield return Until(() => TitleScreen.Frozen, 4f, "it freezes");
        float frozeAfter = Time.realtimeSinceStartup - shownAt;
        Check(lastWait && frozeAfter <= 4f, $"...within 4 s ({frozeAfter:0.0} s)");
        Check(Time.timeScale == 0f, "...at time scale 0");
        yield return Until(() => title.Menu != null && title.Menu.MenuShowing, 3f, "the menu appears");
        yield return Seconds(.7f);
        TitleMenuUI menu = title.Menu;
        Check(string.Join(",", menu.Items) == "New Game,Settings,Credits,Quit", $"...without Continue: {string.Join(" · ", menu.Items)}");
        Check(menu.SelectedAtShow == 0 && menu.SelectedId == TitleMenuUI.NewGameId, "...New Game selected the moment it appears");
        Check(menu.LogoSettled, "the logo has slammed in and settled");
        yield return Photo("04-cold-open-frozen");
        yield return WordCheck("04-cold-open-frozen");
        Check(!File.Exists(LabSavePath), "nothing was saved (no save file)");

        int reloads = TitleScreen.Reloads;
        PlugInPad("Start screen check pad");
        yield return PadPress(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        yield return Until(() => TitleScreen.Rewinding, 2f, "New Game plays the rewind");
        yield return Seconds(.8f);
        Check(menu.RewindWords == TitleMenuUI.RewindLine, $"...\"{menu.RewindWords}\"");
        yield return Photo("05-rewind");
        yield return WordCheck("05-rewind");
        yield return Until(() => !TitleScreen.Up && TitleScreen.Reloads > reloads && DayClock.Instance != null, 8f, "...then the real new game begins (the scene reloads)");
        if (!lastWait) yield break;
        yield return Until(() => DayIntro.Playing, 6f, "Day 1's intro plays as today");
        DayClock clock = DayClock.Instance;
        Check(clock != null && clock.Day == 1, $"it is Day {(clock != null ? clock.Day : 0)}");
        Check(CafeLayout.Instance != null && CafeLayout.Instance.AppliedFrom == CafeLayout.Source.NewGame,
            $"in the half-finished café ({(CafeLayout.Instance != null ? CafeLayout.Instance.AppliedFrom.ToString() : "no layout")}, {(CafeLayout.Instance != null ? CafeLayout.Instance.Seats : 0)} seats)");
        DayIntro.RequestSkip();
        yield return Until(() => !DayIntro.Playing, 15f, "...and the intro gives the day back");
        yield return Seconds(1.5f);
        Check(Mathf.Approximately(Time.timeScale, 1f) && DayClock.Instance != null && DayClock.Instance.enabled, $"time scale 1 and the clock on ({Time.timeScale:0.##})");
        yield return Photo("06-day-1");
        Check(!File.Exists(LabSavePath), "still no save file (the game saves at Open Tomorrow)");
    }

    // ------------------------------------------------------------------ helpers

    // The file SaveManager reads in this session (its private rule, asked by reflection, so the lab never guesses it).
    static string SaveManagerFile()
    {
        try
        {
            PropertyInfo name = typeof(SaveManager).GetProperty("SaveFileName", BindingFlags.Instance | BindingFlags.NonPublic);
            return SaveManager.Instance != null && name != null ? name.GetValue(SaveManager.Instance) as string : "";
        }
        catch (Exception) { return ""; }
    }

    static int FilesBeside()
    {
        try { return Directory.GetFiles(Application.persistentDataPath).Length; }
        catch (Exception) { return -1; }
    }

    // The photo's word check, read back (NotebookPageCheck's way): the title's own texts only.
    IEnumerator WordCheck(string name)
    {
        string file = Path.Combine(folder, name + ".text.json");
        if (!ReviewScreen.Enabled || !File.Exists(file))
        {
            Note($"{name}: the word check is off this session (or wrote nothing), so the title's words weren't measured");
            yield break;
        }
        ReviewScreen.PhotoCheck check = null;
        try { check = JsonUtility.FromJson<ReviewScreen.PhotoCheck>(File.ReadAllText(file)); }
        catch (Exception e) { Note($"{name}: its word check couldn't be read ({e.Message})"); }
        if (check == null) yield break;
        List<ReviewScreen.TextMeasure> mine = check.items.Where(m => m.owner == nameof(TitleMenuUI)).ToList();
        List<ReviewScreen.TextMeasure> bad = mine.Where(m => m.severity == 1 || m.severity == 2 || m.px1080 < 22f
            || m.backgroundKnown && !m.outlined && m.contrast >= 0f && m.contrast < 4.5f).ToList();
        float least = mine.Count > 0 ? mine.Min(m => m.px1080) : 0f;
        Check(mine.Count > 0 && bad.Count == 0,
            $"{name}: the title's {mine.Count} texts read (no S1 or S2, none under 22 px, contrast 4.5+; the smallest {least:0} px)" +
            (bad.Count > 0 ? ": " + string.Join("; ", bad.Select(m => $"S{m.severity} \"{Short(m.text)}\" {m.px1080:0} px {m.contrast:0.0}:1 {m.reasons}")) : ""));
    }
}
