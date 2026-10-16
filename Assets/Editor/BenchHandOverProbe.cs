#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// PROBE - THE BENCH'S HAND-OVER REACH (15 Oct 2026; Edit Mode, read-only)
//
// Day 1 played by hand: Ace walked from the till until the bench offered "Set down", worked Grace's camera from there, and
// with it fixed in his hands E still said "Set down", never "Hand it back": Grace on the repair bar's middle stool was outside
// the across-the-counter rules, which counted Ace behind the bench only within Bench Range (2 m) of its stand point
// (PlayerInteractor.BehindTheBench). She left "Never again" while he held her finished camera.
//
// This maps the staff side around the bench every 0.25 m at Ace's height: where the bench is in Ace's reach (where E sets a
// device down on it or works on it), whether the build counts him on the staff side there (its own StaffSide, through
// reflection), what the 14 Oct rule said, and whether each repair-bar stool is then in front of the counter and within
// Across Counter Reach (E hands over to someone sitting there). Nothing in the scene changes: the interactor's station
// fields are filled in memory only, as its Awake fills them when playing. Report: Logs/Probes/bench-handover-<time>.txt.
// ---------------------------------------------------------------------------
public static class BenchHandOverProbe
{
    const string MenuPath = "Fixit Fidget/Checks/Probe - the bench's hand-over reach (edit mode, read-only)";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float Step = .25f;

    [MenuItem(MenuPath, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ---------- Hands on it: the play check (HandsOnCheck) ----------

    const string HandsOnMenu = "Fixit Fidget/Checks/Hands on it - play check (lab, drives itself)";

    [MenuItem(HandsOnMenu, true)]
    static bool CanPlayHandsOn() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(HandsOnMenu, false, 401)]
    static void PlayHandsOn()
    {
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError("[Hands on check] Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        string path = System.IO.Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(HandsOnCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[Hands on check] A fresh Day 1 in the lab: Grace's camera in the close-up and in Ace's hands with her patience low, " +
                  $"about a minute and a half. Keep the Game view in front. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleHandsOnRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(HandsOnCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(HandsOnCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }

    [MenuItem(MenuPath, false, 400)]
    static void Run()
    {
        var log = new StringBuilder();
        void Line(string s) { log.AppendLine(s); }
        Line($"Probe - the bench's hand-over reach, {DateTime.Now:yyyy-MM-dd HH:mm:ss} (Edit Mode, read-only)");

        PlayerInteractor ace = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
        if (ace == null) { Finish(log, "No PlayerInteractor in the open scene: open the café scene first."); return; }

        // The stations, found as PlayerInteractor.Awake finds them.
        StationInteractable bench = null, counter = null;
        foreach (StationInteractable s in UnityEngine.Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
        {
            if (s == null || s.StandPoint == null) continue;
            if (s.GetComponent<BeverageStation>() != null) continue;
            if (s.IsWorkSurface) { if (bench == null) bench = s; }
            else if (counter == null) counter = s;
        }
        if (bench == null) { Finish(log, "No work-surface station with a stand point (the bench)."); return; }
        Type t = typeof(PlayerInteractor);
        t.GetField("benchStation", Any)?.SetValue(ace, bench);
        if (counter != null) t.GetField("counterStation", Any)?.SetValue(ace, counter);
        t.GetField("queue", Any)?.SetValue(ace, UnityEngine.Object.FindAnyObjectByType<CounterQueue>());
        float reach = Read(t, ace, "reach", 2.2f), benchRange = Read(t, ace, "benchRange", 2f);
        float across = Read(t, ace, "acrossCounterReach", 3.5f), counterRange = Read(t, ace, "counterRange", 2.5f);
        MethodInfo behindBench = t.GetMethod("BehindTheBench", Any), towardCustomers = t.GetMethod("TowardCustomers", Any);
        if (behindBench == null || towardCustomers == null)
        { Finish(log, "PlayerInteractor.BehindTheBench or TowardCustomers is gone (renamed?): nothing to probe."); return; }

        Transform stand = bench.StandPoint;
        Vector3 facing = Flat(stand.forward).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
        float y = ace.transform.position.y, floorY = stand.position.y;   // the reach is measured from Ace's middle, as the game does
        Vector3 origin = new Vector3(stand.position.x, y, stand.position.z);
        Line($"Bench: {Path(bench.transform)}; stand point {V(stand.position)}, facing {V(facing)} (into the room)");
        if (counter != null) Line($"Till: {Path(counter.transform)}; stand point {V(counter.StandPoint.position)}; Counter Range {counterRange:0.00} m");
        Line($"Reach {reach:0.00} m (E on the bench from above), Bench Range {benchRange:0.00} m, Across Counter Reach {across:0.00} m; Ace's middle at y {y:0.00}, now at {V(ace.transform.position)}; the floor at y {floorY:0.00}");

        List<TableSeat> stools = UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(s => s != null && s.RepairBar).OrderBy(s => Flat(Seat(s) - stand.position).magnitude).ToList();
        if (stools.Count == 0) Line("No repair-bar stools in the scene.");
        for (int i = 0; i < stools.Count; i++)
        {
            Vector3 d = Flat(Seat(stools[i]) - origin);
            Line($"Stool {i + 1}{(i == 0 ? " (the bench's: Grace's, the nearest first)" : "")}: {stools[i].name} at {V(Seat(stools[i]))}; " +
                 $"{Vector3.Dot(d, side):+0.00;-0.00} m along the counter, {Vector3.Dot(d, facing):+0.00;-0.00} m out from the stand point");
        }

        Physics.SyncTransforms();
        var hits = new Collider[256];
        int lateralSteps = Mathf.RoundToInt(6f / Step), backSteps = Mathf.RoundToInt(2.5f / Step), outSteps = Mathf.RoundToInt(.75f / Step);
        int reachable = 0, reachableStaff = 0, reachableHandOver = 0, wasStaff = 0, gap = 0, pastLine = 0;
        var gapAt = new List<string>();
        var map = new StringBuilder();
        map.AppendLine("Map (rows: metres out from the stand point toward the room, top = furthest out; columns: -6 .. +6 m along the counter):");
        map.AppendLine("  B bench in reach, staff side, hands over to stool 1   b bench in reach, staff side, stool 1 out of reach");
        map.AppendLine("  X bench in reach, NOT staff side (E says \"Set down\", never \"Hand it back\")   o staff side, bench out of reach");
        map.AppendLine("  . free, neither   # blocked   (blank: no floor)   S the stand point");
        for (int f = outSteps; f >= -backSteps; f--)
        {
            var row = new StringBuilder();
            for (int l = -lateralSteps; l <= lateralSteps; l++)
            {
                Vector3 p = origin + side * (l * Step) + facing * (f * Step);
                if (l == 0 && f == 0) { row.Append('S'); continue; }
                Vector3 feet = new Vector3(p.x, floorY, p.z);
                if (!Physics.Raycast(feet + Vector3.up * .4f, Vector3.down, out RaycastHit floor, .8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                { row.Append(' '); continue; }
                if (Blocked(feet, ace.transform)) { row.Append('#'); continue; }
                bool inReach = BenchInReach(p, reach, bench, hits);
                // The build's own rules (PlayerInteractor.AcrossTheCounterTarget's geometry): over the till's counter from Ace,
                // over the bench from its stand point (15 Oct); the 14 Oct rule beside them.
                bool till = ace.IsBehindCounter(p);
                object[] args = { p, Vector3.zero };
                bool behind = (bool)behindBench.Invoke(ace, args);
                Vector3 toward = (Vector3)args[1];
                bool staff = till || behind;
                bool old = till || Flat(p - stand.position).magnitude <= benchRange && Vector3.Dot(Flat(p - stand.position), facing) <= .5f;
                bool handOver = false;
                if (stools.Count > 0)
                {
                    Vector3 seat = Seat(stools[0]);
                    if (till && counter != null)
                        handOver = Vector3.Dot(Flat(seat - counter.StandPoint.position), (Vector3)towardCustomers.Invoke(ace, null)) > .5f
                                   && Flat(seat - p).magnitude <= across;
                    if (behind)
                        handOver |= Vector3.Dot(Flat(seat - stand.position), toward) > .5f && Flat(seat - stand.position).magnitude <= across;
                }
                if (inReach)
                {
                    row.Append(!staff ? 'X' : handOver ? 'B' : 'b');
                    // Ace's middle can't be within his radius (0.28 m) of the bench's front: the rows from +0.50 out are the
                    // counter's end beside the bench, the room's side of its line. Counted apart.
                    if (f * Step > .25f + 1e-4f) { if (!staff || !handOver) pastLine++; continue; }
                    reachable++;
                    if (staff) reachableStaff++;
                    if (handOver) reachableHandOver++;
                    if (old) wasStaff++;
                    if (!staff) { gap++; if (gapAt.Count < 12) gapAt.Add($"({l * Step:+0.00;-0.00} along, {f * Step:+0.00;-0.00} out)"); }
                }
                else row.Append(staff ? 'o' : '.');
            }
            map.AppendLine($"{f * Step,6:+0.00;-0.00} |{row}|");
        }
        Line("");
        Line($"Where the bench is in Ace's reach, behind its line (up to +0.25 m out): {reachable} points. The build counts him on the " +
             $"staff side at {reachableStaff} (the 14 Oct rule: {wasStaff}); from {reachableHandOver} of them E hands over to someone on stool 1. " +
             $"From +0.50 m out (the counter's end beside the bench, the room's side of its line) {pastLine} points don't, counted apart.");
        Line(gap == 0 ? "PASS  wherever E reaches the bench, E reaches across it."
                      : $"FAIL  {gap} points reach the bench but are not the staff side, e.g. {string.Join(", ", gapAt)}");
        Line(reachable > 0 && reachableHandOver == reachable ? "PASS  from every one of them stool 1 is handed over to."
                      : $"NOTE  stool 1 is out of the hand-over from {reachable - reachableHandOver} of them.");
        Line("");
        log.Append(map);
        Finish(log, null);
    }

    static float Read(Type t, object o, string field, float fallback)
    {
        FieldInfo f = t.GetField(field, Any);
        return f != null && f.GetValue(o) is float v ? v : fallback;
    }

    static Vector3 Seat(TableSeat s) => s.SeatPose != null ? s.SeatPose.position : s.transform.position;

    static bool BenchInReach(Vector3 p, float reach, StationInteractable bench, Collider[] hits)
    {
        int n = Physics.OverlapSphereNonAlloc(p, reach, hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
            if (hits[i] != null && hits[i].GetComponentInParent<Interactable>() == bench) return true;
        return false;
    }

    // Ace's body (a 0.28 m capsule from knee to chest, from his feet) would be inside something solid that isn't his.
    static bool Blocked(Vector3 p, Transform ace)
    {
        Collider[] inside = Physics.OverlapCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.4f, .28f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        foreach (Collider c in inside) if (c != null && !c.transform.IsChildOf(ace)) return true;
        return false;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    static void Finish(StringBuilder log, string refusal)
    {
        if (refusal != null) log.AppendLine("REFUSED  " + refusal);
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "Probes"));
        Directory.CreateDirectory(folder);
        string file = System.IO.Path.Combine(folder, $"bench-handover-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(file, log.ToString());
        File.WriteAllText(System.IO.Path.Combine(folder, "bench-handover-last.txt"), log.ToString());
        Debug.Log("[Bench hand-over probe] " + (refusal ?? "done") + ": " + file);
    }
}
#endif
