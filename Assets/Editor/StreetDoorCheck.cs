#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Checks for the real front doors (StreetDoorSteps, StreetDoor).
//
// Doors 3 (edit mode, read-only): each building has its hinged door, a clear
// doorway (nothing of the building left across the opening or in the hall), a
// hall, and a door whose swing stays inside the doorway and the hall; which
// walking routes start at which door. Photos of each door.
//
// Street doors (Play Mode, lab session; the playtest save is never touched):
// Grace from the saffron house, a walk-in from the dusty rose house and one
// from the courtyard shop each come out of their door and, at the café's door,
// turn straight back and go in again. For every door: it is open before anyone
// passes through it, it closes behind them once they are clear, and on the way
// in they stay in the hall until it has shut - or, when someone else is waiting
// to use the door, go straight on in (taking turns, 27 Sept). Photos in
// Logs/Night/doors-<time>/.
//
// Doors 3 also checks the waiting spots Doors 4 marked at each door walk-ins
// use: at least two, each free of everything and out of the way. The rush-hour
// checks (a crowd at one door) are in StreetDoorRushCheck.
// ---------------------------------------------------------------------------
public static class StreetDoorCheck
{
    const string EditMenu = "Fixit Fidget/Night/Doors 3 - Check the front doors (read-only)";
    const string PlayMenu = "Fixit Fidget/Checks/Street doors (Play Mode, lab session)";
    const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";

    // ================================================================== edit mode

    [MenuItem(EditMenu)]
    static void CheckMenu()
    {
        string folder = NewFolder("doors-check-");
        var report = new StringBuilder();
        int problems = 0;
        void Line(bool ok, string what) { if (!ok) problems++; report.AppendLine((ok ? "ok       " : "PROBLEM  ") + what); }
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var doors = new List<StreetDoor>();
            CafeArrivals routesOwner = Object.FindAnyObjectByType<CafeArrivals>(FindObjectsInactive.Include);
            List<CafeParkingLot.Obstacle> standing = routesOwner != null ? CafeParkingLot.Obstacles(routesOwner.transform) : null;
            var lanes = CafeParkingLot.LaneSegments();
            foreach (string name in StreetDoorSteps.Buildings)
            {
                Transform building = StreetDoorSteps.FindOptional(name);
                if (building == null) { Line(false, name + " is in the scene"); continue; }
                StreetDoorSteps.Recipe r = StreetDoorSteps.RecipeFor(building);
                Transform doorObject = building.Find(StreetDoorSteps.DoorName);
                StreetDoor door = doorObject != null ? doorObject.GetComponent<StreetDoor>() : null;
                if (door == null || door.hinge == null || door.doorway == null || door.hall == null)
                {
                    Line(false, $"{name}: has a hinged front door with its doorway and hall markers");
                    continue;
                }
                doors.Add(door);
                MeshFilter leaf = door.hinge.GetComponentInChildren<MeshFilter>(true);
                Line(leaf != null && leaf.sharedMesh != null && leaf.GetComponent<Collider>() != null, $"{name}: the door leaf has its mesh and a collider");
                if (leaf == null || leaf.sharedMesh == null) continue;

                // Closed, the leaf is exactly where the old door was.
                Bounds closed = InBuilding(building, leaf.transform, leaf.sharedMesh.bounds);
                Line(Mathf.Abs(closed.min.x - r.left) < .01f && Mathf.Abs(closed.max.x - r.right) < .01f && Mathf.Abs(closed.min.z - r.hingeZ) < .01f,
                    $"{name}: closed, the door sits where it was (x {closed.min.x:0.000}–{closed.max.x:0.000}, from z {closed.min.z:0.000})");
                Line(door.hinge.localRotation == Quaternion.identity, $"{name}: the door is closed in the scene");

                // The doorway and hall are clear: nothing of the building across them. The top
                // 8 cm (just under the head) is reported separately: on the bay-window houses the
                // bay's carved support bracket above the door dips 6 cm below the head at its
                // front corner, as it always overlapped the old frame block.
                var passage = new Bounds();
                passage.SetMinMax(new Vector3(r.left + .03f, r.floor + .05f, -StreetDoorSteps.HallDepth + .05f),
                                  new Vector3(r.right - .03f, r.top - .08f, r.frameFront - .03f));
                string blocking = Blocking(building, passage, door);
                Line(blocking == null, $"{name}: the doorway and hall are clear up to 8 cm under the head{(blocking != null ? " — " + blocking + " is in the way" : "")}");
                var underHead = new Bounds();
                underHead.SetMinMax(new Vector3(r.left + .03f, r.top - .08f, -StreetDoorSteps.HallDepth + .05f),
                                    new Vector3(r.right - .03f, r.top - .005f, r.frameFront - .03f));
                string trim = Blocking(building, underHead, door);
                if (trim != null) report.AppendLine($"info     {name}: just under the head: {trim}");

                Transform hall = building.Find(StreetDoorSteps.HallName);
                Line(hall != null && hall.GetComponent<MeshFilter>() != null && hall.GetComponent<MeshFilter>().sharedMesh != null
                     && hall.GetComponent<MeshFilter>().sharedMesh.triangles.Length == 30, $"{name}: the dark hall is there (5 faces)");

                // The swing stays inside the doorway and the hall.
                string swing = Swing(building, door, leaf, r);
                Line(swing == null, $"{name}: the door's swing ({door.openAngle:0}°) stays inside the doorway and the hall{(swing != null ? " — " + swing : "")}");

                float hallIn = Vector3.Dot(door.hall.position - door.doorway.position, door.transform.forward);
                Line(hallIn < -1f, $"{name}: people wait {-hallIn:0.00} m inside the doorway");

                // Where people on their way in wait for their turn (Doors 4), if walk-ins use this door.
                CafeArrivals.Route used = StreetDoorSteps.RouteFrom(door, routesOwner);
                if (used != null && standing != null)
                {
                    Transform[] marks = (door.waitSpots ?? Array.Empty<Transform>()).Where(t => t != null).ToArray();
                    Line(marks.Length >= 2, $"{name}: {marks.Length} waiting spot(s) for people on their way in (Doors 4 marks them)");
                    foreach (Transform mark in marks)
                    {
                        string why = StreetDoorSteps.SpotProblem(mark.position, door, used, routesOwner, standing, lanes);
                        Line(why == null, $"{name}: {mark.name} is free and out of the way{(why != null ? " — " + why : "")}");
                    }
                }

                Vector3 d = door.DoorwayPoint, o = door.Outward;
                CafeSecondPassSteps.Capture(Path.Combine(folder, Safe(name) + ".png"), d + o * 4.2f + Vector3.up * 1.7f, d + Vector3.up * 1.1f, 50f, false);
            }

            // Which walking routes start at which door.
            CafeArrivals arrivals = Object.FindAnyObjectByType<CafeArrivals>(FindObjectsInactive.Include);
            if (arrivals != null)
                foreach (CafeArrivals.Route route in arrivals.EditorFootRoutes.Where(x => x != null && x.points.Length > 0))
                {
                    StreetDoor at = Nearest(doors, route.points[0], .7f);
                    report.AppendLine($"info     \"{route.name}\" starts at " + (at != null ? at.transform.parent.name + "'s door" : "no front door"));
                }
            report.Insert(0, (problems == 0 ? "Street doors: all clear" : $"Street doors: {problems} problem(s)") + "\n\n");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            if (problems > 0) Debug.LogError("[Street doors] Check: " + problems + " problem(s). " + folder + "\n" + report);
            else Debug.Log("[Street doors] Check: all clear. " + folder + "\n" + report);
        }
        catch (Exception e) { Debug.LogError("[Street doors] Check FAILED: " + e.Message + "\n" + report + "\n" + e); }
    }

    [MenuItem(EditMenu, true)]
    static bool CanCheck() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static Bounds InBuilding(Transform building, Transform from, Bounds local)
    {
        Vector3 c = local.center, e = local.extents;
        var b = new Bounds(building.InverseTransformPoint(from.TransformPoint(c)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            b.Encapsulate(building.InverseTransformPoint(from.TransformPoint(c + corner)));
        }
        return b;
    }

    // What of the building's own faces is inside the passage (sampled), or null: each mesh
    // with the extent of its intruding points.
    static string Blocking(Transform building, Bounds passage, StreetDoor door)
    {
        var found = new List<string>();
        foreach (MeshFilter filter in building.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.transform.IsChildOf(door.transform)) continue;
            if (filter.name == StreetDoorSteps.HallName) continue;
            Mesh mesh = filter.sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            Matrix4x4 toBuilding = building.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var hit = new Bounds();
            int hits = 0;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 a = toBuilding.MultiplyPoint3x4(v[t[k]]), b = toBuilding.MultiplyPoint3x4(v[t[k + 1]]), c = toBuilding.MultiplyPoint3x4(v[t[k + 2]]);
                var box = new Bounds(a, Vector3.zero);
                box.Encapsulate(b);
                box.Encapsulate(c);
                if (!box.Intersects(passage)) continue;
                const int steps = 10;
                for (int i = 0; i <= steps; i++)
                    for (int j = 0; j <= steps - i; j++)
                    {
                        Vector3 p = a + (b - a) * (i / (float)steps) + (c - a) * (j / (float)steps);
                        if (!passage.Contains(p)) continue;
                        if (hits++ == 0) hit = new Bounds(p, Vector3.zero); else hit.Encapsulate(p);
                    }
            }
            if (hits > 0)
                found.Add($"{filter.name} ({hit.min.x:0.000}..{hit.max.x:0.000} across, {hit.min.y:0.000}..{hit.max.y:0.000} up, {hit.min.z:0.000}..{hit.max.z:0.000} out)");
        }
        return found.Count > 0 ? string.Join("; ", found) : null;
    }

    // Turns the leaf through its swing; null if every point of it stays inside the doorway and the hall.
    static string Swing(Transform building, StreetDoor door, MeshFilter leaf, StreetDoorSteps.Recipe r)
    {
        Vector3[] vertices = leaf.sharedMesh.vertices;   // hinge space (the leaf is not moved under its hinge)
        Vector3 hinge = building.InverseTransformPoint(door.hinge.position);
        for (float angle = 0f; angle <= door.openAngle + .01f; angle += 3f)
        {
            Quaternion turn = Quaternion.Euler(0f, angle, 0f);
            foreach (Vector3 v in vertices)
            {
                Vector3 p = hinge + turn * (leaf.transform.localRotation * v + leaf.transform.localPosition);
                if (p.x < r.left - .015f || p.x > r.right + .015f) return $"at {angle:0}° part of it is {p.x:0.000} across (the doorway is {r.left:0.000}–{r.right:0.000})";
                if (p.z < -StreetDoorSteps.HallDepth) return $"at {angle:0}° it goes through the back of the hall";
            }
        }
        return null;
    }

    static StreetDoor Nearest(List<StreetDoor> doors, Vector3 point, float within)
    {
        StreetDoor best = null;
        float bestSq = within * within;
        foreach (StreetDoor d in doors)
        {
            Vector3 v = d.DoorwayPoint - point;
            v.y = 0f;
            if (v.sqrMagnitude <= bestSq) { bestSq = v.sqrMagnitude; best = d; }
        }
        return best;
    }

    // ================================================================== play mode

    static IEnumerator routine;
    static double resumeAt;
    static readonly StringBuilder playReport = new();
    static int checks, failures;
    static string playFolder;
    static NotebookFactData[] notebookBefore;
    static readonly List<GameObject> walkers = new();

    sealed class Watch
    {
        public string who;
        public StreetDoor door;
        public GameObject npc;
        public CafeArrivals.Comings came;
        public float started;
        public bool opened, closedAfterOut, wentBackIn, closedWithThemInside, photoOut, photoIn;
        public float worstPassAmount = 1f;     // the door's opening when they were in its plane (1 = fully open)
        public float outAt = -1f, turnedAt = -1f, goneAt = -1f;
        public float lastInside = -1f;
        public float closedWhenGone = -1f;
        public bool wasOutside;
        public bool othersNow, othersWhenGone;   // someone else waiting for or using the door (taking turns)
    }

    [MenuItem(PlayMenu)]
    static void PlayRun()
    {
        playReport.Clear();
        checks = failures = 0;
        playFolder = NewFolder("doors-");
        routine = PlaySequence();
        resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[Street doors] Play check running for about a minute: three people come out of their front doors, walk to the café door and go home again.");
    }

    [MenuItem(PlayMenu, true)]
    static bool CanPlayRun() => EditorApplication.isPlaying && routine == null;

    static void Tick()
    {
        if (routine == null) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) { Check(false, "Play Mode stayed on until the check finished"); Finish(); return; }
        if (EditorApplication.timeSinceStartup < resumeAt) return;
        bool more;
        try { more = routine.MoveNext(); }
        catch (Exception e) { Check(false, "Check stopped by an exception: " + e.Message); Debug.LogException(e); more = false; }
        if (!more) { Finish(); return; }
        if (routine.Current is float seconds) resumeAt = EditorApplication.timeSinceStartup + seconds;
    }

    static IEnumerator PlaySequence()
    {
        if (!CafeLab.Active) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab), so only the lab save is in play"); yield break; }
        CafeArrivals arrivals = CafeArrivals.Instance;
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        SaveManager save = SaveManager.Instance;
        GameObject body = spawner != null ? new SerializedObject(spawner).FindProperty("customerPrefab").objectReferenceValue as GameObject : null;
        var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
        if (arrivals == null || body == null || save == null || grace == null)
        {
            Check(false, $"Everything is in place (arrivals {arrivals != null}, the customer body {body != null}, save {save != null}, Grace {grace != null})");
            yield break;
        }
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) { Check(false, "The café is open (arrivals only walk in while it is)"); yield break; }
        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime saveBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        notebookBefore = save.Notebook.Snapshot();

        CafeArrivals.Route[] routes = arrivals.EditorFootRoutes;
        int Route(Func<CafeArrivals.Route, bool> which) => Array.FindIndex(routes, x => x != null && which(x));
        int rose = Route(x => x.name == HomeSetup.NeighbourRouteName);
        int shop = Route(x => x.name.StartsWith("From the courtyard shop", StringComparison.Ordinal));
        Check(StreetDoor.All.Count == StreetDoorSteps.Buildings.Length, $"Every building's door is in play ({StreetDoor.All.Count} of {StreetDoorSteps.Buildings.Length})");
        Check(rose >= 0 && shop >= 0, $"The walk-ins' routes are there (dusty rose {rose}, courtyard shop {shop})");
        if (rose < 0 || shop < 0) yield break;

        var watches = new List<Watch>();
        Watch Start(string who, GameObject npc, int route)
        {
            walkers.Add(npc);
            var w = new Watch { who = who, npc = npc, started = Time.time };
            bool ok = route < 0
                ? CafeArrivals.TryArrive(npc, CafeArrivals.Kind.Customer, () => CafeArrivals.TryDepart(npc))
                : arrivals.EditorArriveOnFoot(npc, CafeArrivals.Kind.Customer, route, () => CafeArrivals.TryDepart(npc));
            w.came = arrivals.Today.LastOrDefault();
            w.door = ok ? StreetDoor.Near(npc.transform.position, 3f) : null;
            Check(ok && w.door != null, $"{who} sets off from a front door ({w.door?.transform.parent.name ?? "none"})");
            if (w.door != null)
            {
                // In the dark hall - or, when someone else is using the door, further in, waiting
                // unseen for their turn (taking turns, 27 Sept).
                NpcJourney walk = npc.GetComponent<NpcJourney>();
                bool waitingInside = walk != null && walk.Unseen;
                Check((waitingInside || Vector3.Distance(Flat(npc.transform.position), Flat(w.door.HallPoint)) < .3f) && w.door.Outside(npc.transform.position) < -1f,
                    $"…starting inside, {(waitingInside ? "waiting unseen for their turn at the door" : "in the dark hall")} ({-w.door.Outside(npc.transform.position):0.00} m in)");
            }
            watches.Add(w);
            return w;
        }

        GameObject graceBody = Object.Instantiate(body);
        graceBody.name = "Grace (doors check)";
        graceBody.GetComponent<CustomerIdentity>().SetupRegular(grace, save.MemoryFor(grace));
        Start("Grace", graceBody, -1);
        GameObject roseBody = Object.Instantiate(body);
        roseBody.name = "Walk-in from the dusty rose house (doors check)";
        Start("A walk-in from the dusty rose house", roseBody, rose);
        GameObject shopBody = Object.Instantiate(body);
        shopBody.name = "Walk-in from the courtyard shop (doors check)";
        Start("A walk-in from the courtyard shop", shopBody, shop);

        // Follow everyone until they are all home again (or two minutes pass).
        float limit = Time.time + 120f;
        while (Time.time < limit && watches.Any(w => w.door != null && w.goneAt < 0f))
        {
            foreach (Watch w in watches.Where(x => x.door != null && x.goneAt < 0f)) Observe(w);
            yield return .05f;
        }

        foreach (Watch w in watches.Where(x => x.door != null))
        {
            Note($"{w.who}: out after {Fmt(w.outAt - w.started)}, turned for home after {Fmt(w.turnedAt - w.started)}, gone in after {Fmt(w.goneAt - w.started)}.");
            Check(w.opened, $"{w.who}: the door opened for them");
            Check(w.worstPassAmount >= .85f, $"…and was open ({w.worstPassAmount:0%} at worst) whenever they were in the doorway");
            Check(w.closedAfterOut, "…and closed behind them once they were clear");
            Check(w.wentBackIn && w.goneAt > 0f, "…they came back and went in");
            Check(w.closedWhenGone >= 0f && (w.closedWhenGone < .01f || w.othersWhenGone),
                w.othersWhenGone ? $"…and went straight on in, as someone else was waiting for the door (door {Mathf.Max(0f, w.closedWhenGone):0%} open then)"
                                 : $"…and only left the game once it had shut behind them (door {Mathf.Max(0f, w.closedWhenGone):0%} open then)");
        }
        // The lab's own visitors keep walking in and out of these doors meanwhile, so a door may
        // be open (or just closing) for one of them; what must never happen is a door left open
        // with nobody using it. Within three seconds every door is shut or in use.
        bool settled = false;
        for (float waited = 0f; waited < 3f && !settled; waited += .25f)
        {
            settled = StreetDoor.All.All(d => d.IsClosed || d.Held);
            if (!settled) yield return .25f;
        }
        foreach (StreetDoor d in StreetDoor.All.Where(d => !d.IsClosed))
            Note($"{d.transform.parent.name}'s door is {d.OpenAmount:0%} open{(d.Held ? ", in use by someone" : ", nobody using it")}.");
        Check(settled, "Every door is shut again, unless someone is using it right now");
        save.Notebook.Restore(notebookBefore);
        DateTime saveAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(saveAfter == saveBefore, "The playtest save was not written");
    }

    static void Observe(Watch w)
    {
        StreetDoor door = w.door;
        if (w.npc == null)
        {
            if (w.goneAt < 0f)
            {
                w.goneAt = Time.time;
                w.wentBackIn = w.wasOutside;
                w.closedWhenGone = door.OpenAmount;   // they may only leave the game once it has shut ...
                w.othersWhenGone = w.othersNow;       // ... unless someone else was waiting for the door
            }
            return;
        }
        Vector3 at = w.npc.transform.position;
        float outside = door.Outside(at);
        NpcJourney journey = w.npc.GetComponent<NpcJourney>();
        w.othersNow = journey != null && door.OthersUsing(journey);
        if (door.OpenAmount > .5f) w.opened = true;
        // In the door's plane (where the closed leaf would be): the door must be open.
        if (outside > -.05f && outside < .25f && Vector3.Distance(Flat(at), Flat(door.DoorwayPoint)) < .8f)
            w.worstPassAmount = Mathf.Min(w.worstPassAmount, door.OpenAmount);
        if (outside > .3f && w.outAt < 0f) w.outAt = Time.time;
        if (outside > 0f) w.wasOutside = true;
        if (w.outAt > 0f && !w.closedAfterOut && w.turnedAt < 0f && door.IsClosed && outside > 1.5f) w.closedAfterOut = true;
        if (w.came != null && w.came.wentTo.Length > 0 && w.turnedAt < 0f) w.turnedAt = Time.time;
        // Photos: coming out, and going in.
        if (!w.photoOut && outside > .05f && outside < .6f && w.turnedAt < 0f)
        {
            w.photoOut = true;
            Vector3 d = door.DoorwayPoint, o = door.Outward;
            Vector3 side = Vector3.Cross(Vector3.up, o).normalized;
            CafeSecondPassSteps.Capture(Path.Combine(playFolder, Safe(w.who) + " - coming out.png"), d + o * 3.2f + side * 1.4f + Vector3.up * 1.8f, d + Vector3.up * 1.0f, 55f, false);
        }
        if (!w.photoIn && w.turnedAt > 0f && outside < .1f && outside > -.6f)
        {
            w.photoIn = true;
            Vector3 d = door.DoorwayPoint, o = door.Outward;
            Vector3 side = Vector3.Cross(Vector3.up, o).normalized;
            CafeSecondPassSteps.Capture(Path.Combine(playFolder, Safe(w.who) + " - going in.png"), d + o * 3.2f - side * 1.4f + Vector3.up * 1.8f, d + Vector3.up * 1.0f, 55f, false);
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        routine = null;
        foreach (GameObject g in walkers) if (g != null) Object.Destroy(g);
        walkers.Clear();
        if (EditorApplication.isPlaying && SaveManager.Instance != null && notebookBefore != null) SaveManager.Instance.Notebook.Restore(notebookBefore);
        string summary = $"Street doors check: {checks - failures}/{checks} passed" + (failures > 0 ? $", {failures} FAILED" : "");
        playReport.Insert(0, summary + "\n\n");
        File.WriteAllText(Path.Combine(playFolder, "report.txt"), playReport.ToString());
        if (failures > 0) Debug.LogError("[Street doors] " + summary + "\n" + playFolder + "\n" + playReport);
        else Debug.Log("[Street doors] " + summary + "\n" + playFolder + "\n" + playReport);
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        playReport.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    static void Note(string what) => playReport.AppendLine("      " + what);

    // ================================================================== helpers

    static string NewFolder(string prefix)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            prefix + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
    static string Fmt(float seconds) => seconds < 0f ? "—" : seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
#endif
