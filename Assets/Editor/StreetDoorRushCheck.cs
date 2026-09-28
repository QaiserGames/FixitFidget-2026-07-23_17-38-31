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
// Rush hour at one front door (Play Mode, lab session; 27 Sept).
//
// Mansoor: "i see alot of congestion whenever some npcs try to go through doors,
// they just get stuck and then that causes everyother npc to also get stuck".
// Recordings showed why (claude/night-homes-spec.md, "Door jams"): a doorway fits
// one person, but people coming out and people going in used it at the same time,
// met face to face and each waited for the other; new walk-ins kept appearing on
// the same spot in the blocked hall, and everyone waiting outside stood on the same
// "stand aside" spot.
//
// The check recreates a busy spell at one door. Five people who have just crossed
// the road on their way home reach it a moment apart, just as five new walk-ins set
// off out of it (the five then walk to the café door and straight back home, so the
// door sees them again later). It follows all ten until they are home and reports:
//   * both ways at once: someone coming out and someone going in both in the door's
//     single-file stretch (the dark hall, the doorway, the stoop, and any path after
//     it too narrow for two people to pass) at the same time - and face to face (the
//     two of them within 1.5 m there);
//   * people inside each other (closer than 0.40 m), and three or more bunched up
//     (standing still, shoulder to shoulder: within 0.7 m);
//   * anyone teleported past a jam ("helped on") or cut short by the 2-minute backstop;
//   * the longest anyone stood still where you can see them, and how long each took;
//   * the door open whenever someone was in it.
// It films the door (Café life > door camera) and writes an arrivals trace.
// Lab session only: the playtest save is never touched. So that every run tests the
// same crowd, the café sends nobody new while it runs, and it starts once the lab's
// own walkers have cleared the door and the kerb (they still count if they come by).
// ---------------------------------------------------------------------------
public static class StreetDoorRushCheck
{
    const string ShopMenu = "Fixit Fidget/Checks/Street doors - rush hour at the shop door (Play Mode, lab session)";
    const string RoseMenu = "Fixit Fidget/Checks/Street doors - rush hour at the dusty rose door (Play Mode, lab session)";
    const int Each = 5;                  // people each way
    const float Limit = 170f;            // seconds (game time) until everyone must be home
    const float PassingRoom = .65f;      // a path narrower than this (left + right room) is single file
    const float Touching = .40f;         // centres closer than this: inside each other
    const float StillSpeed = .08f;
    const float Shoulder = .7f;          // standing this close: shoulder to shoulder

    static IEnumerator routine;
    static double resumeAt;
    static readonly StringBuilder report = new();
    static int checks, failures;
    static string folder, doorLabel;
    static readonly List<GameObject> spawned = new();
    static readonly List<Behaviour> paused = new();

    sealed class Walker
    {
        public string label;
        public bool outFirst;            // came out of the door first (then back in later)
        public GameObject npc;
        public float born, outAt = -1f, homeAt = -1f;
        public float stillSince = -1f, longestStill;
        public string longestWhere = "";
        public int unstuck;
        public Vector3 last;
    }

    [MenuItem(ShopMenu)] static void ShopRun() => Run("shop door", r => r.name.StartsWith("From the courtyard shop", StringComparison.Ordinal));
    [MenuItem(RoseMenu)] static void RoseRun() => Run("dusty rose door", r => r.name == HomeSetup.NeighbourRouteName);

    [MenuItem(ShopMenu, true)]
    [MenuItem(RoseMenu, true)]
    static bool CanRun() => EditorApplication.isPlaying && routine == null;

    static void Run(string label, Func<CafeArrivals.Route, bool> which)
    {
        report.Clear();
        checks = failures = 0;
        doorLabel = label;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "door-rush-" + label.Replace(' ', '-') + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        routine = Sequence(which);
        resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[Door rush] Rush hour at the {label} for up to {Limit:0} s: five people come out as five others arrive to go in.");
    }

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

    static IEnumerator Sequence(Func<CafeArrivals.Route, bool> which)
    {
        if (!CafeLab.Active) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab), so only the lab save is in play"); yield break; }
        CafeArrivals arrivals = CafeArrivals.Instance;
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        GameObject body = spawner != null ? new SerializedObject(spawner).FindProperty("customerPrefab").objectReferenceValue as GameObject : null;
        DayClock clock = DayClock.Instance;
        if (arrivals == null || body == null || clock == null) { Check(false, $"Everything is in place (arrivals {arrivals != null}, customer body {body != null}, clock {clock != null})"); yield break; }
        if (!clock.IsOpen) { Check(false, "The café is open (walk-ins only come while it is)"); yield break; }
        // Long enough for the whole rush (the same top-up the lab menu uses).
        if (clock.TimeRemaining < Limit + 40f)
            typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining))?.SetValue(clock, Limit + 60f);
        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime saveBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;

        CafeArrivals.Route[] routes = arrivals.EditorFootRoutes;
        int routeIndex = Array.FindIndex(routes, r => r != null && which(r));
        CafeArrivals.Route route = routeIndex >= 0 ? routes[routeIndex] : null;
        StreetDoor door = route != null && route.points.Length > 1 ? StreetDoor.Near(route.points[0], .7f) : null;
        Check(route != null && door != null, $"The {doorLabel} and its walking route are in the scene ({route?.name ?? "no route"})");
        if (route == null || door == null) yield break;

        // The single-file stretch: from the dark hall out through the doorway and the stoop, and
        // on while the path stays too narrow for two people to pass.
        int end = 1;
        while (end < route.points.Length - 1 && Room(route, end) < PassingRoom && Crossing(route, end) < 0) end++;
        var stretch = new List<Vector3> { door.HallPoint };
        for (int i = 0; i <= end; i++) stretch.Add(route.points[i]);
        // Face to face counts inside it, not at its very end, where the way already widens.
        var inner = new List<Vector3>(stretch);
        Vector3 last = inner[inner.Count - 1], before = inner[inner.Count - 2];
        inner[inner.Count - 1] = last + Flat(before - last).normalized * Mathf.Min(.4f, Flat(before - last).magnitude * .5f);
        // Where the people going home start: just over the road, at the start of the first crossing
        // on the route (or its far end).
        int from = route.points.Length - 1;
        for (int i = end; i < route.points.Length - 1; i++) if (Crossing(route, i) >= 0) { from = i; break; }
        Note($"Single-file stretch: the hall, the doorway and {end} segment(s) out to ({route.points[end].x:0.00}, {route.points[end].z:0.00}), " +
             $"{PathLength(stretch):0.0} m long. People going home start at ({route.points[from].x:0.00}, {route.points[from].z:0.00}).");

        // Nobody new from the café while the rush runs; then wait for the door and the kerb to clear.
        paused.Clear();
        foreach (Behaviour spawnerOf in new Behaviour[] { spawner, Object.FindAnyObjectByType<PatronSpawner>() })
            if (spawnerOf != null && spawnerOf.enabled) { spawnerOf.enabled = false; paused.Add(spawnerOf); }
        float clearBy = Time.time + 40f;
        while (Time.time < clearBy && !(Clear(door.DoorwayPoint, 6f) && Clear(route.points[from], 2f))) yield return .25f;
        Note(Clear(door.DoorwayPoint, 6f) && Clear(route.points[from], 2f)
            ? "The café sent nobody new meanwhile; the door and the kerb were clear when the rush began."
            : "The café sent nobody new meanwhile; the lab's walkers had not all cleared the door after 40 s, so the rush began anyway.");

        int cutShortBefore = arrivals.WalksCutShort;
        CafeLifeRecorder.StartView(doorLabel, Limit + 10f);
        if (CafeArrivalsRecorder.Recording) CafeArrivalsRecorder.Stop("a door rush check started");
        CafeArrivalsRecorder.Start(Limit + 10f);
        yield return 1f;

        var walkers = new List<Walker>();
        float t0 = Time.time;
        int homeGoers = 0, walkIns = 0;
        float nextHomeGoer = t0, walkInsAt = t0 + 1f;
        int bothWays = 0, faceToFace = 0, touching = 0, bunched = 0;
        float worstOpen = 1f;
        string bothNote = "", faceNote = "", touchNote = "", bunchNote = "";
        var lastFace = new Dictionary<string, float>();

        while (Time.time - t0 < Limit)
        {
            float now = Time.time;
            // Five people on their way home, at least 0.8 s apart and never on top of the last
            // one (they set off from the same kerb); five walk-ins all at once a second in.
            if (homeGoers < Each && now >= nextHomeGoer && Clear(route.points[from], .8f))
            {
                GameObject npc = Object.Instantiate(body);
                npc.name = $"Rush - going home {homeGoers + 1}";
                spawned.Add(npc);
                bool ok = arrivals.EditorDepartOnFoot(npc, CafeArrivals.Kind.Customer, routeIndex, from);
                if (ok) walkers.Add(new Walker { label = npc.name, npc = npc, born = now, last = npc.transform.position });
                else Check(false, npc.name + " set off for home");
                homeGoers++;
                nextHomeGoer = now + .8f;
            }
            if (walkIns == 0 && now >= walkInsAt)
            {
                for (int i = 0; i < Each; i++)
                {
                    GameObject npc = Object.Instantiate(body);
                    npc.name = $"Rush - walk-in {i + 1}";
                    spawned.Add(npc);
                    GameObject me = npc;
                    bool ok = arrivals.EditorArriveOnFoot(npc, CafeArrivals.Kind.Customer, routeIndex, () => CafeArrivals.TryDepart(me));
                    if (ok) walkers.Add(new Walker { label = npc.name, npc = npc, outFirst = true, born = now, last = npc.transform.position });
                    else Check(false, npc.name + " set off from the door");
                }
                walkIns = Each;
            }

            // Everyone near the door this moment (the rush and the lab's own visitors).
            var near = new List<(string name, Vector3 at, Vector3 v, bool outward, bool seen, NpcJourney j)>();
            foreach (NpcJourney j in NpcJourney.Active)
            {
                if (j == null || !j.isActiveAndEnabled) continue;
                // A walk-in is placed in the hall when it sets off and on its first step either
                // walks out or goes further in to wait for its turn: judge it from then on.
                if (j.Age < .25f) continue;
                Vector3 p = j.transform.position;
                if (Flat(p - door.DoorwayPoint).magnitude > 8f) continue;
                // Inside the house waiting for their turn (or behind the hall's back wall): not on the street.
                bool seen = !j.Unseen && door.Outside(p) > -(StreetDoorSteps.HallDepth - .05f);
                near.Add((j.name, p, j.Velocity, j.Arriving, seen, j));
            }

            // Both ways at once in the single-file stretch, and face to face there.
            var inStretch = near.Where(n => n.seen && DistanceToPath(inner, n.at) < .45f).ToList();
            foreach (var a in inStretch.Where(n => n.outward))
                foreach (var b in inStretch.Where(n => !n.outward))
                {
                    string key = a.name + "|" + b.name;
                    if (lastFace.TryGetValue(key, out float at) && now - at < 3f) continue;
                    lastFace[key] = now;
                    float apart = Flat(a.at - b.at).magnitude;
                    bothWays++;
                    if (bothNote.Length == 0) bothNote = $"{a.name} coming out and {b.name} going in, {apart:0.00} m apart at {now - t0:0.0} s";
                    if (apart > 1.5f) continue;
                    faceToFace++;
                    if (faceNote.Length == 0) faceNote = $"{a.name} coming out and {b.name} going in, {apart:0.00} m apart at {now - t0:0.0} s";
                }
            // Inside each other; three or more bunched up and standing still.
            for (int i = 0; i < near.Count; i++)
                for (int k = i + 1; k < near.Count; k++)
                {
                    if (!near[i].seen || !near[k].seen) continue;
                    float d = Flat(near[i].at - near[k].at).magnitude;
                    if (d >= Touching) continue;
                    string key = "t|" + near[i].name + "|" + near[k].name;
                    if (lastFace.TryGetValue(key, out float at) && now - at < 3f) continue;
                    lastFace[key] = now;
                    touching++;
                    if (touchNote.Length == 0) touchNote = $"{near[i].name} and {near[k].name}, {d:0.00} m apart at ({near[i].at.x:0.0}, {near[i].at.z:0.0}), {now - t0:0.0} s";
                }
            // (People waiting at a kerb for a crossing gather on the crossing's own terms: not the door's.)
            var still = near.Where(n => n.seen && Flat(n.v).magnitude < StillSpeed && n.j.KerbCrossing == null && n.j.CurrentCrossing == null).ToList();
            foreach (var s in still)
            {
                int around = still.Count(o => Flat(o.at - s.at).magnitude < Shoulder);
                if (around < 3) continue;
                string key = "b|" + s.name;
                if (lastFace.TryGetValue(key, out float at) && now - at < 5f) continue;
                lastFace[key] = now;
                bunched++;
                if (bunchNote.Length == 0) bunchNote = $"{around} people standing within {Shoulder:0.0} m of {s.name} at ({s.at.x:0.0}, {s.at.z:0.0}), {now - t0:0.0} s";
                break;
            }
            // The door is open whenever someone is in its plane.
            foreach (var n in near)
            {
                float o = door.Outside(n.at);
                if (o > -.05f && o < .25f && Flat(n.at - door.DoorwayPoint).magnitude < .8f) worstOpen = Mathf.Min(worstOpen, door.OpenAmount);
            }

            // Each of the rush's own people.
            foreach (Walker w in walkers)
            {
                if (w.homeAt > 0f) continue;
                if (w.npc == null) { w.homeAt = now; continue; }   // went in: home
                NpcJourney j = w.npc.GetComponent<NpcJourney>();
                if (j != null) w.unstuck = Mathf.Max(w.unstuck, j.Unstuck);
                Vector3 p = w.npc.transform.position;
                if (w.outFirst && w.outAt < 0f && Flat(p - route.points[end]).magnitude > 1.2f && door.Outside(p) > 1f && DistanceToPath(stretch, p) > .6f) w.outAt = now;
                bool seen = door.Outside(p) > -(StreetDoorSteps.HallDepth - .05f);
                bool isStill = j != null && Flat(j.Velocity).magnitude < StillSpeed;
                w.last = p;
                if (seen && isStill && j != null && j.isActiveAndEnabled)
                {
                    if (w.stillSince < 0f) w.stillSince = now;
                    float held = now - w.stillSince;
                    if (held > w.longestStill) { w.longestStill = held; w.longestWhere = $"({p.x:0.0}, {p.z:0.0})" + (j.Waiting ? ", waiting for " + j.WaitingFor : j.HeldBy.Length > 0 ? ", held by " + j.HeldBy : ""); }
                }
                else w.stillSince = -1f;
            }
            if (homeGoers >= Each && walkIns >= Each && walkers.All(w => w.homeAt > 0f)) break;
            yield return .05f;
        }

        float took = Time.time - t0;
        Note($"Took {took:0.0} s for all ten to get home (limit {Limit:0} s).");
        foreach (Walker w in walkers)
            Note($"{w.label}: " + (w.outFirst ? $"out and clear after {Fmt(w.outAt - w.born)}, " : "") + $"home after {Fmt(w.homeAt - w.born)}; " +
                 $"longest standing still in view {w.longestStill:0.0} s{(w.longestStill > 1f ? " at " + w.longestWhere : "")}" + (w.unstuck > 0 ? $"; teleported {w.unstuck}x" : ""));
        Check(walkers.Count == 2 * Each && walkers.All(w => w.homeAt > 0f), $"All ten got home ({walkers.Count(w => w.homeAt > 0f)} of {walkers.Count}) within {Limit:0} s");
        Check(walkers.Where(w => w.outFirst).All(w => w.outAt > 0f), $"All five walk-ins got out of the door and clear of it ({walkers.Count(w => w.outFirst && w.outAt > 0f)} of {Each})");
        Check(bothWays == 0, $"The single-file stretch was only ever used one way at a time ({bothWays} time(s) both ways{(bothNote.Length > 0 ? "; first: " + bothNote : "")})");
        Check(faceToFace == 0, $"Nobody met face to face in it ({faceToFace} time(s) within 1.5 m{(faceNote.Length > 0 ? "; first: " + faceNote : "")})");
        Check(touching == 0, $"Nobody walked or stood inside anybody near the door ({touching} time(s) closer than {Touching:0.00} m{(touchNote.Length > 0 ? "; first: " + touchNote : "")})");
        Check(bunched == 0, $"Nobody bunched up: never three standing shoulder to shoulder, within {Shoulder:0.0} m ({bunched} time(s){(bunchNote.Length > 0 ? "; first: " + bunchNote : "")})");
        int teleported = walkers.Count(w => w.unstuck > 0);
        Check(teleported == 0, $"Nobody had to be teleported past a jam ({teleported})");
        int cutShort = arrivals.WalksCutShort - cutShortBefore;
        Check(cutShort <= 0, $"No walk hit the 2-minute backstop ({Mathf.Max(0, cutShort)})");
        float longest = walkers.Count > 0 ? walkers.Max(w => w.longestStill) : 0f;
        Walker longestWalker = walkers.OrderByDescending(w => w.longestStill).FirstOrDefault();
        Check(longest <= 25f, $"Nobody stood still in view for more than 25 s (longest {longest:0.0} s{(longestWalker != null && longest > 1f ? ", " + longestWalker.label + " at " + longestWalker.longestWhere : "")})");
        Check(worstOpen >= .85f, $"The door was open whenever someone was in it ({worstOpen:0%} at worst)");
        DateTime saveAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(saveAfter == saveBefore, "The playtest save was not written");
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        routine = null;
        if (EditorApplication.isPlaying)
        {
            if (CafeLifeRecorder.Recording) CafeLifeRecorder.Stop("the door rush check finished");
            if (CafeArrivalsRecorder.Recording) CafeArrivalsRecorder.Stop("the door rush check finished");
            string trace = CafeArrivalsRecorder.Folder;
            if (!string.IsNullOrEmpty(trace)) Note("Arrivals trace: " + trace);
        }
        foreach (GameObject g in spawned) if (g != null) Object.Destroy(g);
        spawned.Clear();
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;   // the café sends people again
        paused.Clear();
        string summary = $"Door rush at the {doorLabel}: {checks - failures}/{checks} passed" + (failures > 0 ? $", {failures} FAILED" : "");
        report.Insert(0, summary + "\n\n");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        if (failures > 0) Debug.LogError("[Door rush] " + summary + "\n" + folder + "\n" + report);
        else Debug.Log("[Door rush] " + summary + "\n" + folder + "\n" + report);
    }

    // ---- helpers ----

    // Nobody (on a walk, or a street walker) within `radius` of the point.
    static bool Clear(Vector3 point, float radius)
    {
        foreach (NpcJourney j in NpcJourney.Active)
            if (j != null && j.isActiveAndEnabled && !j.Unseen && Flat(j.transform.position - point).magnitude < radius) return false;
        return true;
    }

    static float Room(CafeArrivals.Route route, int segment) =>
        (segment < route.roomLeft.Length ? route.roomLeft[segment] : .4f) + (segment < route.roomRight.Length ? route.roomRight[segment] : .4f);

    static int Crossing(CafeArrivals.Route route, int segment) =>
        segment < route.crossingAtSegment.Length ? route.crossingAtSegment[segment] : -1;

    static float PathLength(List<Vector3> path)
    {
        float sum = 0f;
        for (int i = 1; i < path.Count; i++) sum += Flat(path[i] - path[i - 1]).magnitude;
        return sum;
    }

    static float DistanceToPath(List<Vector3> path, Vector3 p)
    {
        float best = float.PositiveInfinity;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 a = Flat(path[i - 1]), e = Flat(path[i]) - a, q = Flat(p);
            float t = e.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, e) / e.sqrMagnitude) : 0f;
            best = Mathf.Min(best, (q - (a + e * t)).magnitude);
        }
        return best;
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    static void Note(string what) => report.AppendLine("      " + what);
    static string Fmt(float seconds) => seconds < 0f ? "—" : seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
#endif
