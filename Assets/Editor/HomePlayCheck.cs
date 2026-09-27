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
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Grace's home in the running game (claude/night-homes-spec.md §5), in a café
// lab session so the playtest save is never touched.
//
// Twice, a Grace (her real profile on the café's customer body) steps out of
// her front door, walks over to the café's door and (in this check) straight
// back home and in:
//   * with the overhead camera on her doorstep: Ace notices her coming out, and
//     the notebook gets "Came out of the saffron house at 12 West Street." as a
//     hunch; with that forgotten while she's at the café, seeing her go back in
//     gives "Went into the saffron house at 12 West Street." (the rule that a
//     second sighting the same day adds nothing is in Checks > Home rules);
//   * with the camera turned away: nothing is written, coming or going.
// Both walks must start in her doorway on her own route and end back inside.
// First, for playtesting, it notes where her doorstep can be seen from: the
// overhead camera turned right round at the current zoom and zoomed right out
// (seen, out of the frame, or hidden behind what), and the same for the dusty
// rose house next door to compare.
// The notebook, the camera and the scene are put back as they were.
// Report and photos: Logs/Night/home-check-<time>/.
// ---------------------------------------------------------------------------
public static class HomePlayCheck
{
    const string Menu = "Fixit Fidget/Checks/Grace's home (Play Mode, lab session)";
    const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";
    const string HomeFact = "grace.home";
    const float WalkLimit = 90f; // seconds for one way (her walk is about 17 m, with a crossing)

    static IEnumerator routine;
    static double resumeAt;
    static readonly StringBuilder report = new();
    static int checks, failures;
    static string folder;
    static NotebookFactData[] notebookBefore;
    static GameObject walker;

    [MenuItem(Menu)]
    static void Run()
    {
        report.Clear();
        checks = failures = 0;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "home-check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        routine = Sequence();
        resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[Home check] Running for a minute or two (Grace walks to the café and back twice); leave the Game view alone until it reports.");
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => EditorApplication.isPlaying && routine == null;

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

    static IEnumerator Sequence()
    {
        if (!CafeLab.Active) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab), so only the lab save is in play"); yield break; }
        SaveManager save = SaveManager.Instance;
        CafeArrivals arrivals = CafeArrivals.Instance;
        CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>();
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
        HomeDoor door = grace != null ? HomeDoor.Find(grace.HomeId) : null;
        GameObject body = spawner != null ? new SerializedObject(spawner).FindProperty("customerPrefab").objectReferenceValue as GameObject : null;
        if (save == null || arrivals == null || view == null || grace == null || door == null || body == null)
        {
            Check(false, $"Everything is in place (save {save != null}, arrivals {arrivals != null}, view {view != null}, " +
                         $"Grace {grace != null}, her door {door != null}, the customer body {body != null})");
            yield break;
        }
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) { Check(false, "The café is open (arrivals only walk in while it is)"); yield break; }
        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime saveBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Notebook notebook = save.Notebook;
        notebookBefore = notebook.Snapshot();
        Vector3 angleBefore = view.OverheadAngle;
        if (view.FirstPersonSelected)
        {
            view.SetFirstPerson(false);
            yield return 2.5f; // let the camera blend back overhead
        }
        Note($"Grace's home: {door.looks}, {door.Address}; door at {door.DoorPoint:F2}. Today is Day {NotebookHooks.Today}.");
        Check(door.Address == door.houseNumber + " " + StreetNames.Name(door.streetId) && !door.Address.Contains("{"),
            $"Her address reads \"{door.Address}\" with the street's current name");

        // Where can Ace see her doorstep from? The overhead camera turned right
        // round, at the zoom it was left at and zoomed right out; the same for the
        // dusty rose house next door (the walk-ins' door) to compare. Information
        // for playtesting; the two walks below use the angles found here.
        Vector3 doorstep = door.DoorPoint + Vector3.up * 1.2f;
        float usualZoom = angleBefore.z, farZoom = 48f;
        Sight now = Look(doorstep, angleBefore.x, angleBefore.z);
        Note($"From the view as it was (turn {angleBefore.x:0}°, tilt {angleBefore.y:0}°, {angleBefore.z:0} m): her doorstep is {now.Describe()}.");
        var usual = new List<Sight>();
        var far = new List<Sight>();
        for (IEnumerator s = Sweep(view, doorstep, usualZoom, usual); s.MoveNext();) yield return s.Current;
        for (IEnumerator s = Sweep(view, doorstep, farZoom, far); s.MoveNext();) yield return s.Current;
        Note($"Turning the overhead camera round (tilt 50°, 10° steps), her doorstep at {usualZoom:0} m: {Summary(usual)}.");
        Note($"…zoomed right out ({farZoom:0} m): {Summary(far)}.");
        CafeArrivals.Route neighbours = arrivals.EditorFootRoutes.FirstOrDefault(r => r != null && r.name == HomeSetup.NeighbourRouteName && r.points.Length > 0);
        CafeArrivals.Route hers = arrivals.EditorFootRoutes.FirstOrDefault(r => r != null && r.homeId == door.homeId && r.points.Length > 0);
        if (neighbours != null && hers != null)
        {
            // Same house model: the same step from the doorway out to the door's face.
            Vector3 roseDoorstep = neighbours.points[0] + (door.DoorPoint - hers.points[0]) + Vector3.up * 1.2f;
            var roseUsual = new List<Sight>();
            var roseFar = new List<Sight>();
            for (IEnumerator s = Sweep(view, roseDoorstep, usualZoom, roseUsual); s.MoveNext();) yield return s.Current;
            for (IEnumerator s = Sweep(view, roseDoorstep, farZoom, roseFar); s.MoveNext();) yield return s.Current;
            view.OrbitTo(angleBefore.x, angleBefore.y, angleBefore.z);
            yield return .05f;
            Note($"For comparison, the dusty rose house's doorstep: from the view as it was, {Look(roseDoorstep, angleBefore.x, angleBefore.z).Describe()}; " +
                 $"at {usualZoom:0} m {Summary(roseUsual)}; at {farZoom:0} m {Summary(roseFar)}.");
        }

        Sight? watch = Best(usual.Where(v => v.Seen), v => -v.centre) ?? Best(far.Where(v => v.Seen), v => -v.centre);
        Sight? away = Best(usual.Where(v => v.outside > .1f), v => v.outside) ?? Best(far.Where(v => v.outside > .1f), v => v.outside);
        Check(watch != null, "There is an overhead angle with her doorstep in view" +
                             (watch != null ? $" (turn {watch.Value.yaw}°, {watch.Value.distance:0} m)" : ""));
        Check(away != null, "…and one looking away from it, her doorstep well out of the frame" +
                            (away != null ? $" (turn {away.Value.yaw}°, {away.Value.distance:0} m: {away.Value.Describe()})" : ""));
        if (watch == null || away == null) { Restore(view, angleBefore); yield break; }

        // ---- watching: out of her door, over to the café's door, straight back in ----
        Forget(notebook);
        view.OrbitTo(watch.Value.yaw, 50, watch.Value.distance);
        yield return .3f;
        GameObject her = walker = MakeGrace(body, grace);
        Check(CafeArrivals.TryArrive(her, CafeArrivals.Kind.Customer, () => CafeArrivals.TryDepart(her)), "Grace sets off for the café on foot");
        CafeArrivals.Comings came = arrivals.Today.LastOrDefault();
        Check(came != null && came.cameFrom == HomeSetup.GraceRouteName && came.car.Length == 0,
            $"…from her own front door, never by car (\"{came?.cameFrom}\")");
        Check(Flat(her.transform.position - door.DoorPoint).magnitude < 1f, "…starting in her doorway");
        yield return .8f;
        Photo("1-watching-grace-come-out.png");
        yield return 2.2f;
        NotebookFactData seen = notebook.Find(HomeFact);
        Check(seen != null && seen.sure == Notebook.Sureness.Hunch && seen.source == Notebook.Sources.Seen && seen.day == NotebookHooks.Today
              && seen.text.StartsWith("Came out of " + door.looks, StringComparison.Ordinal),
            $"With her doorstep in view, Ace notices: \"{seen?.text}\" ({seen?.sure})");
        if (seen != null)
        {
            string line = NotebookRecap.Line(seen);
            Note("Recap line: " + line);
            Check(line.Contains(door.Address) && line.Contains("(hunch)"), "The recap line gives her address with the street's name, marked as a hunch");
        }
        Check(came != null && came.seenAtHome, "The day's comings and goings note that she was seen at her door");

        // At the café's door this check sends her straight home (in the game she'd come in first).
        float took = 0;
        while (her != null && came != null && came.wentTo.Length == 0 && took < WalkLimit) { yield return .5f; took += .5f; }
        Check(came != null && came.wentTo.Length > 0, $"She walks over to the café's door and turns for home ({took:0} s)");
        NotebookFactData kept = notebook.Find(HomeFact);
        Check(kept != null && kept == seen && kept.text.StartsWith("Came out of", StringComparison.Ordinal) && kept.sure == Notebook.Sureness.Hunch,
            "…with the sighting still one hunch, as written at her door");
        Forget(notebook); // so her going back in is noticed on its own
        took = 0;
        bool photographed = false;
        while (her != null && took < WalkLimit)
        {
            if (!photographed && Flat(her.transform.position - door.DoorPoint).magnitude < 2f)
            {
                Photo("2-watching-grace-go-back-in.png");
                photographed = true;
            }
            yield return .25f;
            took += .25f;
        }
        Check(her == null, $"…and goes back in: gone from the street ({took:0} s after turning for home)");
        NotebookFactData wentIn = notebook.Find(HomeFact);
        Check(wentIn != null && wentIn.sure == Notebook.Sureness.Hunch && wentIn.source == Notebook.Sources.Seen
              && wentIn.text.StartsWith("Went into " + door.looks, StringComparison.Ordinal) && wentIn.text.Contains(door.houseNumber),
            $"Seen going back in, Ace notices that too: \"{wentIn?.text}\" ({wentIn?.sure})");
        Check(notebook.Count == notebookBefore.Count(f => f.id != HomeFact) + 1, "…as the same one fact about where she lives");

        // ---- looking away: the same walk, unseen ----
        Forget(notebook);
        view.OrbitTo(away.Value.yaw, 50, away.Value.distance);
        yield return .3f;
        GameObject again = walker = MakeGrace(body, grace);
        Check(CafeArrivals.TryArrive(again, CafeArrivals.Kind.Customer, () => CafeArrivals.TryDepart(again)), "Grace sets off again");
        CafeArrivals.Comings second = arrivals.Today.LastOrDefault();
        yield return .8f;
        Photo("3-looking-away.png");
        yield return 2.2f;
        Check(notebook.Find(HomeFact) == null, "With the camera turned away, nothing is written as she comes out");
        took = 0;
        while (again != null && took < 2 * WalkLimit) { yield return .5f; took += .5f; }
        Check(again == null, $"She walks to the café's door and back in again ({took:0} s)");
        Check(notebook.Find(HomeFact) == null && second != null && !second.seenAtHome, "…and going back in unseen writes nothing either");

        Restore(view, angleBefore);
        yield return .5f;
        Check(notebook.Count == notebookBefore.Length, $"The notebook is back as it was ({notebook.Count} facts)");
        DateTime saveAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(saveAfter == saveBefore, "The playtest save was not written");
    }

    // What the overhead camera makes of a point at one angle.
    struct Sight
    {
        public int yaw;
        public float distance;
        public bool inFrame;
        public string blocker;   // the nearest solid thing in the way, or null
        public string edge;      // which way out of the frame, if it is
        public float centre;     // how far from the middle of the frame (0 = dead centre)
        public float outside;    // how far outside the frame (≤ 0 = inside)

        public bool Seen => inFrame && blocker == null;
        public string Describe() => Seen ? "seen" : !inFrame ? "out of the frame (" + edge + ")" : "hidden behind " + blocker;
    }

    static Sight Look(Vector3 point, float yaw, float distance)
    {
        Camera cam = Camera.main;
        Vector3 v = cam.WorldToViewportPoint(point);
        bool inFrame = HomeSightings.InFrame(cam, point);
        Collider blocker = inFrame ? HomeSightings.Blocker(cam, point, null) : null;
        bool behind = v.z <= cam.nearClipPlane;
        return new Sight
        {
            yaw = Mathf.RoundToInt(yaw),
            distance = distance,
            inFrame = inFrame,
            blocker = blocker != null ? NameOf(blocker.transform) : null,
            edge = behind ? "behind the camera" : v.y < 0 ? "below the bottom edge" : v.y > 1 ? "above the top edge"
                 : v.x < 0 ? "off the left edge" : v.x > 1 ? "off the right edge" : "right at the edge",
            centre = new Vector2(v.x - .5f, v.y - .5f).magnitude,
            outside = behind ? 10f : Mathf.Max(Mathf.Max(-v.x, v.x - 1f), Mathf.Max(-v.y, v.y - 1f)),
        };
    }

    // The overhead camera turned right round in 10° steps at tilt 50°.
    static IEnumerator Sweep(CafeViewMode view, Vector3 point, float distance, List<Sight> into)
    {
        for (int yaw = 0; yaw < 360; yaw += 10)
        {
            view.OrbitTo(yaw, 50, distance);
            yield return .05f;
            into.Add(Look(point, yaw, distance));
        }
    }

    static Sight? Best(IEnumerable<Sight> sights, Func<Sight, float> score)
    {
        Sight? best = null;
        foreach (Sight s in sights)
            if (best == null || score(s) > score(best.Value)) best = s;
        return best;
    }

    static string Summary(List<Sight> sweep)
    {
        List<int> seen = sweep.Where(s => s.Seen).Select(s => s.yaw).ToList();
        List<int> outOfFrame = sweep.Where(s => !s.inFrame).Select(s => s.yaw).ToList();
        List<Sight> hidden = sweep.Where(s => s.inFrame && s.blocker != null).ToList();
        string text = $"seen at {Turns(seen)} ({seen.Count} of {sweep.Count})";
        if (outOfFrame.Count > 0) text += $"; out of the frame at {Turns(outOfFrame)}";
        if (hidden.Count > 0)
            text += $"; hidden at {Turns(hidden.Select(s => s.yaw).ToList())} (behind {string.Join(", ", hidden.Select(s => s.blocker).Distinct().Take(4))})";
        return text;
    }

    // "120–200°, 300°" for turns in 10° steps; a run across north (350° → 0°) is one run.
    static string Turns(List<int> yaws)
    {
        if (yaws.Count == 0) return "no turn";
        if (yaws.Count == 36) return "every turn";
        var runs = new List<(int from, int to)>();
        foreach (int y in yaws.OrderBy(y => y))
        {
            if (runs.Count > 0 && runs[runs.Count - 1].to + 10 == y) runs[runs.Count - 1] = (runs[runs.Count - 1].from, y);
            else runs.Add((y, y));
        }
        if (runs.Count > 1 && runs[0].from == 0 && runs[runs.Count - 1].to == 350)
        {
            runs[0] = (runs[runs.Count - 1].from, runs[0].to);
            runs.RemoveAt(runs.Count - 1);
        }
        return string.Join(", ", runs.Select(r => r.from == r.to ? r.from + "°" : r.from + "–" + r.to + "°"));
    }

    // "Tree 3 (V3 - street trees)": the thing and the group it belongs to.
    static string NameOf(Transform t) => t.root != t ? $"{t.name} ({t.root.name})" : t.name;

    // Grace's real profile on the café's customer body: exactly what the spawner makes.
    static GameObject MakeGrace(GameObject body, CustomerProfile grace)
    {
        GameObject go = Object.Instantiate(body);
        go.name = "Grace (home check)";
        CustomerIdentity identity = go.GetComponent<CustomerIdentity>();
        identity.SetupRegular(grace, SaveManager.Instance != null ? SaveManager.Instance.MemoryFor(grace) : null);
        return go;
    }

    static void Forget(Notebook notebook) => notebook.Restore(notebookBefore.Where(f => f.id != HomeFact).ToArray());

    static void Restore(CafeViewMode view, Vector3 angle)
    {
        if (walker != null) Object.Destroy(walker);
        walker = null;
        SaveManager save = SaveManager.Instance;
        if (save != null && notebookBefore != null) save.Notebook.Restore(notebookBefore);
        if (view != null) view.OrbitTo(angle.x, angle.y, angle.z);
    }

    static void Photo(string file)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var rt = RenderTexture.GetTemporary(1440, 900, 24);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active, previousTarget = cam.targetTexture;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
        }
        finally
        {
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(texture);
        }
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    static void Note(string what) => report.AppendLine("      " + what);

    static void Finish()
    {
        EditorApplication.update -= Tick;
        routine = null;
        if (EditorApplication.isPlaying) Restore(Object.FindAnyObjectByType<CafeViewMode>(), Object.FindAnyObjectByType<CafeViewMode>()?.OverheadAngle ?? new Vector3(45, 50, 34));
        string summary = $"Grace's home check: {checks - failures}/{checks} passed" + (failures > 0 ? $", {failures} FAILED" : "");
        report.Insert(0, summary + "\n\n");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        if (failures > 0) Debug.LogError("[Home check] " + summary + "\n" + folder + "\n" + report);
        else Debug.Log("[Home check] " + summary + "\n" + folder + "\n" + report);
    }
}
#endif
