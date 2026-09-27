#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Ace's notebook in the running game (claude/night-notebook-spec.md §6), in a
// café lab session: Grace's intake goes through the real hook into the real
// SaveManager's notebook, a repeat adds nothing, another regular's repair adds
// one fact and a walk-in none, the recap text reads right, and the recap has
// its block. Rules and save format are checked without Unity by
// Fixit Fidget > Checks > Night notebook rules (and Tests/NotebookRules).
//
// Leaves nothing behind: the notebook is put back exactly as it was, the test
// identities are destroyed, and the playtest save is never written (checked).
// Report: <project>/Logs/Night/notebook-check-<time>.txt.
// ---------------------------------------------------------------------------
public static class NotebookPlayCheck
{
    const string Menu = "Fixit Fidget/Checks/Night notebook (Play Mode, lab session)";
    const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";

    static readonly StringBuilder report = new();
    static int checks, failures;

    [MenuItem(Menu)]
    static void Run()
    {
        report.Clear();
        checks = failures = 0;
        var made = new System.Collections.Generic.List<Object>();
        SaveManager save = SaveManager.Instance;
        NotebookFactData[] before = save != null ? save.Notebook.Snapshot() : null;
        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime playtestBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        try
        {
            Sequence(made);
        }
        catch (Exception e)
        {
            Check(false, "Check stopped by an exception: " + e.Message);
            Debug.LogException(e);
        }
        finally
        {
            if (save != null && before != null) save.Notebook.Restore(before);
            foreach (Object o in made) if (o != null) Object.Destroy(o);
        }
        DateTime playtestAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(playtestAfter == playtestBefore, "The playtest save was not written");
        if (save != null) Check(save.Notebook.Count == (before?.Length ?? 0), $"The notebook is back as it was ({save.Notebook.Count} facts)");
        Finish();
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => EditorApplication.isPlaying;

    static void Sequence(System.Collections.Generic.List<Object> made)
    {
        if (!CafeLab.Active) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab), so only the lab save is in play"); return; }
        SaveManager save = SaveManager.Instance;
        DayClock clock = DayClock.Instance;
        if (save == null || clock == null) { Check(false, "The scene has a SaveManager and a DayClock"); return; }
        Notebook notebook = save.Notebook;
        int day = clock.Day;
        Note($"Lab day {day}; the notebook holds {notebook.Count} facts before the check.");

        var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
        if (grace == null) { Check(false, "Grace's profile exists at " + GracePath); return; }

        // ---- Grace's intake, through the real hook ----
        CustomerIdentity graceIdentity = MakeIdentity(made, "Grace (notebook check)");
        graceIdentity.SetupRegular(grace);
        var camera = new Job
        {
            kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "sticky shutter",
            storyEpisodeId = GraceCameraEpisode.EpisodeId
        };
        graceIdentity.SetDevice(camera.Subject);
        graceIdentity.SetFault(camera.faultDescription);
        graceIdentity.SetStoryRequest(camera);
        Check(graceIdentity.IsGraceCameraRequest, "A camera job with Grace's episode id is her camera episode");
        string[] ids = NotebookEntries.GraceIntake("Grace").Select(f => f.id).ToArray();
        int alreadyKnown = ids.Count(notebook.Knows);
        int learned = NotebookHooks.HeardIntake(graceIdentity, camera);
        Check(learned == ids.Length - alreadyKnown && ids.All(notebook.Knows),
            $"Hearing Grace's intake puts her four facts in the notebook ({learned} new, {alreadyKnown} already there)");
        Check(ids.All(id => notebook.Find(id).who == "grace" && notebook.Find(id).name == grace.characterName && notebook.Find(id).day <= day),
            $"They are filed under Grace (\"{grace.characterName}\"), dated today or earlier");
        Check(NotebookHooks.HeardIntake(graceIdentity, camera) == 0, "Hearing it again adds nothing");

        // ---- another regular, and a walk-in ----
        var other = ScriptableObject.CreateInstance<CustomerProfile>();
        made.Add(other);
        other.characterName = "Test Regular";
        typeof(CustomerProfile).GetField("persistentId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(other, "notebook-check-regular");
        CustomerIdentity regular = MakeIdentity(made, "Test Regular (notebook check)");
        regular.SetupRegular(other);
        var phone = new Job { kind = JobKind.Repair, deviceName = "Phone", faultDescription = "Cracked Screen" };
        regular.SetStoryRequest(phone);
        int count = notebook.Count;
        Check(NotebookHooks.HeardIntake(regular, phone) == 1 && notebook.Count == count + 1,
            "Another regular's repair adds one fact");
        NotebookFactData phoneFact = notebook.Facts.Last();
        Check(phoneFact.text == "Brought in a phone: cracked screen." && phoneFact.source == Notebook.Sources.Told,
            $"…in their own words (\"{phoneFact.text}\")");
        var drink = new Job { kind = JobKind.Drink };
        Check(NotebookHooks.HeardIntake(regular, drink) == 0, "A drink order adds nothing");
        CustomerIdentity walkIn = MakeIdentity(made, "Walk-in (notebook check)");
        walkIn.SetupWalkIn(null, "Walk-in 7");
        Check(NotebookHooks.HeardIntake(walkIn, phone) == 0 && notebook.Count == count + 1, "A walk-in adds nothing (anonymous by design)");

        // ---- the recap ----
        string block = NotebookRecap.Build(notebook, day);
        if (ids.Any(id => notebook.Find(id).day == day))
            Check(block.Contains("Grace: a camera with a scratched strap.") && block.Contains("so far)"),
                "Today's recap block lists Grace's facts and the running total");
        else Note("Grace's facts were already in the lab notebook from an earlier day, so today's block lists only the test regular.");
        Check(block.Contains("Test Regular: brought in a phone: cracked screen."), "…and the other regular's");
        RecapUI recap = Object.FindAnyObjectByType<RecapUI>(FindObjectsInactive.Include);
        var text = recap != null ? new SerializedObject(recap).FindProperty("notebookText").objectReferenceValue as TMP_Text : null;
        Check(text != null, "The recap has the notebook block (Fixit Fidget > Night > Add the notebook to the recap)");
        if (text != null) CheckLayout(text);
    }

    // The block sits between the reviews and the Open Tomorrow button, and a
    // long day (five facts that all wrap, "+1 more", the total) fits in it.
    static void CheckLayout(TMP_Text text)
    {
        var rect = (RectTransform)text.transform;
        Transform panel = rect.parent;
        Rect Local(RectTransform r)
        {
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            Vector3 low = panel.InverseTransformPoint(corners[0]), high = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }
        Rect block = Local(rect);
        if (panel.Find("Reputation") is RectTransform reputation)
            Check(block.yMax <= Local(reputation).yMin + 0.5f, $"The block starts under the reviews ({Local(reputation).yMin - block.yMax:0} units apart)");
        else Note("No Reputation block in this recap.");
        if (panel.Find("NextDayButton") is RectTransform button)
            Check(block.yMin >= Local(button).yMax - 0.5f, $"…and ends above Open Tomorrow ({block.yMin - Local(button).yMax:0} units apart)");
        else Note("No NextDayButton in this recap.");

        // Measured on a copy of the block under a hidden canvas of its own: the
        // recap is usually closed (so the block has never been laid out), and
        // the copy is gone again before the next frame.
        var day = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) day.Learn(fact, 1);
        day.Learn(NotebookEntries.RegularRepair("tomas", "Tomas", "reunion film camera", "sticky shutter, dirty lens and film path"), 1);
        day.Learn(NotebookEntries.RegularRepair("priya", "Priya", "Pocket Watch", "stopped"), 1);
        var host = new GameObject("Notebook check (measuring)", typeof(RectTransform), typeof(Canvas));
        host.GetComponent<Canvas>().enabled = false;
        try
        {
            GameObject copy = Object.Instantiate(text.gameObject, host.transform, false);
            copy.SetActive(true);
            var measured = copy.GetComponent<TMP_Text>();
            measured.text = NotebookRecap.Build(day, 1);
            measured.ForceMeshUpdate(true, true);
            int lines = measured.textInfo.lineCount;
            Check(!measured.isTextOverflowing && lines >= 1 + NotebookRecap.MaxLines + 2,   // heading, five facts, "+1 more", the total
                $"A long day fits without losing a line ({lines} lines at {measured.fontSize:0.#}pt)");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    static CustomerIdentity MakeIdentity(System.Collections.Generic.List<Object> made, string name)
    {
        var go = new GameObject(name);
        made.Add(go);
        return go.AddComponent<CustomerIdentity>();
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    static void Note(string what) => report.AppendLine("NOTE  " + what);

    static void Finish()
    {
        string verdict = failures == 0 ? "PASS" : "FAIL";
        string text = $"Night notebook check - {verdict} ({checks - failures} of {checks})\n{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{report}";
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"notebook-check-{DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.txt");
        File.WriteAllText(file, text);
        if (failures == 0) Debug.Log($"[Notebook check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
        else Debug.LogWarning($"[Notebook check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
    }
}
#endif
