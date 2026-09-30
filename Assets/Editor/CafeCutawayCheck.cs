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
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// The overhead view's cut-away walls in the running game (CafeViewMode and
// CutawayWall), in a café lab session so the playtest save is never touched.
//
// It turns the overhead camera all the way round (and lower and closer, where
// the back wall gets in the way), and at every stop checks that:
//   * a wall in the way fades to a ghost above the sill, never goes: it wears
//     its see-through copy (the dither shader), stays drawn and still casts its
//     whole shadow (30 Sept: a fade, not the slide it used to be);
//   * pictures, shelves and lamps on a faded wall fade with it, while things on
//     the counters and the floor stay solid;
//   * a wall out of the way is back exactly as it was, its own materials on;
//   * at the usual angle nothing is cut;
//   * wobbling the camera across the edge doesn't make a wall flicker;
//   * first person puts every wall straight back.
// Photos from the game camera and a report go to Logs/CafeWalls/cutaway-<time>/.
// The camera is put back where it was at the end.
// ---------------------------------------------------------------------------
public static class CafeCutawayCheck
{
    const string Menu = "Fixit Fidget/Checks/Wall cut-away (Play Mode, lab session)";
    const float Settle = 1.2f; // fade (0.35 s) + rise delay (0.5 s) + margin

    static IEnumerator routine;
    static double resumeAt;
    static readonly StringBuilder report = new();
    static int checks, failures;
    static string folder;

    [MenuItem(Menu)]
    static void Run()
    {
        report.Clear();
        checks = failures = 0;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeWalls",
            "cutaway-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        routine = Sequence();
        resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[Cut-away check] Running for about a minute; leave the Game view alone until it reports.");
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => EditorApplication.isPlaying && routine == null;

    static void Tick()
    {
        if (routine == null) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying)
        {
            Check(false, "Play Mode stayed on until the check finished");
            Finish();
            return;
        }
        if (EditorApplication.timeSinceStartup < resumeAt) return;
        bool more;
        try { more = routine.MoveNext(); }
        catch (Exception e)
        {
            Check(false, "Check stopped by an exception: " + e.Message);
            Debug.LogException(e);
            more = false;
        }
        if (!more) { Finish(); return; }
        if (routine.Current is float seconds) resumeAt = EditorApplication.timeSinceStartup + seconds;
    }

    static IEnumerator Sequence()
    {
        if (!CafeLab.Active) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab), so only the lab save is in play"); yield break; }
        CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>();
        if (view == null) { Check(false, "The scene has a CafeViewMode"); yield break; }
        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime saveBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Vector3 start = view.OverheadAngle;
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        yield return Settle;

        CutawayWall[] cuts = view.CutawayWalls.Where(c => c != null).ToArray();
        Check(cuts.Length >= 2, $"The drawn cut-away walls are set up ({string.Join(", ", cuts.Select(c => c.Wall.name))})");
        Check(SeeThroughMaterials.Dithered, "The see-through shader is in hand (dotted, not blended)");
        foreach (CutawayWall cut in cuts)
            Check(cut.Fades, $"{cut.Wall.name} can fade{(cut.Fades ? $" (sill {cut.SillTop - cut.BaseY:0.00} m up, ghost {cut.Ghost:0.00})" : " (" + cut.Problem + ")")}");
        CutawayWall back = cuts.FirstOrDefault(c => c.Wall.name == "Back plaster");
        CutawayWall tan = cuts.FirstOrDefault(c => c.Wall.name == "Left rear plaster");

        // ---- the usual angle ----
        view.OrbitTo(45, 50, 34);
        yield return Settle;
        Check(cuts.All(c => !c.Lowered), "At the usual overhead angle no wall is cut");
        CheckStates(view, "usual angle");
        Photo("1-usual-angle-45.png");

        // ---- all the way round, as the game starts, then low and close ----
        var down = new Dictionary<CutawayWall, int>();
        foreach (CutawayWall c in cuts) down[c] = 0;
        var sweep = new List<Vector3>();
        for (int yaw = 0; yaw < 360; yaw += 15) sweep.Add(new Vector3(yaw, 50, 34));
        for (int yaw = 60; yaw <= 300; yaw += 30) sweep.Add(new Vector3(yaw, 38, 26));
        Vector3? backDownAt = null, tanDownAt = null;
        Vector3 previous = sweep[0];
        bool first = true;
        (CutawayWall wall, Vector3 up, Vector3 down)? edge = null;
        var wasDown = cuts.ToDictionary(c => c, c => false);
        foreach (Vector3 angle in sweep)
        {
            view.OrbitTo(angle.x, angle.y, angle.z);
            yield return Settle;
            string label = $"yaw {angle.x:0}, tilt {angle.y:0}, distance {angle.z:0}";
            CheckStates(view, label, quiet: true);
            foreach (CutawayWall c in cuts)
            {
                if (c.Lowered) down[c]++;
                if (edge == null && !first && angle.y == previous.y && angle.z == previous.z && c.Lowered != wasDown[c])
                    edge = (c, c.Lowered ? previous : angle, c.Lowered ? angle : previous);
                wasDown[c] = c.Lowered;
            }
            if (back != null && back.Lowered && backDownAt == null) backDownAt = angle;
            if (tan != null && tan.Lowered && tanDownAt == null) tanDownAt = angle;
            Note($"{label}: " + string.Join(", ", cuts.Select(c => $"{c.Wall.name} {(c.Lowered ? $"faded (keep {c.Keep:0.00}, {c.Decor.Count(SeeThroughMaterials.IsWorn)} things with it)" : "solid")}")));
            previous = angle;
            first = false;
        }
        Check(back == null || down[back] > 0, $"Turning round fades the back wall somewhere ({(back != null ? down[back] : 0)} of {sweep.Count} stops)");
        Check(tan == null || down[tan] > 0, $"…and the tan rear wall ({(tan != null ? down[tan] : 0)} of {sweep.Count} stops)");

        // ---- the back wall down: what hides and what stays ----
        if (backDownAt is Vector3 b)
        {
            view.OrbitTo(b.x, b.y, b.z);
            yield return Settle;
            Photo("2-back-wall-down.png");
            CheckDecor(back);
        }
        if (tanDownAt is Vector3 t)
        {
            view.OrbitTo(t.x, t.y, t.z);
            yield return Settle;
            Photo("3-tan-rear-wall-down.png");
        }
        view.OrbitTo(135, 40, 28);
        yield return Settle;
        Photo("4-both-rear-walls-turn-135.png");
        CheckStates(view, "yaw 135, tilt 40, distance 28");

        // ---- wobbling across an edge ----
        if (edge.HasValue)
        {
            var (wall, upAt, downAt) = edge.Value;
            // Close in on the edge, then wobble a degree either side of it.
            Vector3 lo = upAt, hi = downAt;
            for (int i = 0; i < 6; i++)
            {
                Vector3 mid = new(Mathf.LerpAngle(lo.x, hi.x, .5f), lo.y, lo.z);
                view.OrbitTo(45, 50, 34);  // start each probe from up
                yield return Settle;
                view.OrbitTo(mid.x, mid.y, mid.z);
                yield return Settle;
                if (wall.Lowered) hi = mid; else lo = mid;
            }
            float edgeYaw = Mathf.LerpAngle(lo.x, hi.x, .5f);
            float step = Mathf.Sign(Mathf.DeltaAngle(lo.x, hi.x));
            view.OrbitTo(edgeYaw + step * 1.5f, lo.y, lo.z);
            yield return Settle;
            bool startedDown = wall.Lowered;
            int rises = 0;
            bool last = wall.Lowered;
            for (int i = 0; i < 24; i++)
            {
                view.OrbitTo(edgeYaw + (i % 2 == 0 ? -1f : 1f) * step * 1.2f, lo.y, lo.z);
                yield return .1f;
                if (last && !wall.Lowered) rises++;
                last = wall.Lowered;
            }
            Check(startedDown && rises == 0,
                $"Wobbling 1.2° either side of {wall.Wall.name}'s edge (yaw {edgeYaw:0.0}) for 2.4 s keeps it faded: {rises} returns");
            view.OrbitTo(45, 50, 34);
            yield return .2f;
            Check(wall.Lowered, "…and turning back to the usual angle doesn't bring it back instantly");
            yield return Settle;
            Check(!wall.Lowered, $"…but it is back within {Settle + .2f:0.0} s");
        }
        else Check(false, "The sweep found an edge where a wall goes down, to wobble across");

        // ---- first person ----
        if (backDownAt is Vector3 again)
        {
            view.OrbitTo(again.x, again.y, again.z);
            yield return Settle;
        }
        if (view.SetFirstPerson(true))
        {
            yield return .1f;
            Check(cuts.All(c => !c.Lowered && !c.Worn), "First person puts every wall straight back, its own materials on");
            CheckStates(view, "first person");
            view.SetFirstPerson(false);
            yield return Settle;
        }
        else Note("First person wasn't available just then (a station, a conversation or an overlay), so that step was skipped.");

        view.OrbitTo(start.x, start.y, start.z);
        yield return Settle;
        DateTime saveAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(saveAfter == saveBefore, "The playtest save was not written");
    }

    // Every cut-away wall is either solid and exactly as built, or faded (or on its way): wearing
    // its see-through copy, still drawn, still casting its whole shadow. Never simply gone.
    static void CheckStates(CafeViewMode view, string where, bool quiet = false)
    {
        foreach (CutawayWall c in view.CutawayWalls)
        {
            if (c == null || !c.Fades) continue;
            string name = c.Wall.name;
            if (c.Lowered)
            {
                bool drawn = c.Worn && c.Wall.enabled && !c.Wall.forceRenderingOff && c.Wall.shadowCastingMode != ShadowCastingMode.ShadowsOnly
                             && c.Wall.sharedMaterials.All(WearsDots);
                bool settled = c.Progress >= 1f && Mathf.Abs(c.Keep - c.Ghost) < .01f && Mathf.Abs(c.SillTop - (c.BaseY + .78f)) < .03f;
                if (!quiet || !drawn || !settled)
                    Check(drawn && settled, $"{where}: {name} is faded to its ghost above the sill (keep {c.Keep:0.00}, sill at {c.SillTop - c.BaseY:0.00} m), still drawn and casting its shadow");
            }
            else
            {
                bool asBuilt = !c.Worn && c.Wall.enabled && !c.Wall.forceRenderingOff && c.Wall.shadowCastingMode != ShadowCastingMode.ShadowsOnly
                               && !c.Wall.sharedMaterials.Any(WearsDots)
                               && c.Decor.All(r => r == null || !SeeThroughMaterials.IsWorn(r) && !r.forceRenderingOff);
                if (!quiet || !asBuilt)
                    Check(asBuilt, $"{where}: {name} is solid, drawn as built, its own materials on, nothing on it faded");
            }
        }
    }

    static bool WearsDots(Material m) => m != null && m.shader != null && m.shader.name == SeeThroughMaterials.DitherShaderName;

    static void CheckDecor(CutawayWall back)
    {
        Renderer[] faded = back.Decor.Where(r => r != null && SeeThroughMaterials.IsWorn(r)).ToArray();
        Note("Faded with the back wall: " + string.Join(", ", faded.Select(Path3).Distinct()));
        Check(faded.Length > 0, $"Things fixed to the back wall fade with it ({faded.Length} pieces)");
        Check(faded.All(r => r.forceRenderingOff || r.sharedMaterials.All(WearsDots)), "…each wearing the dots (or hidden, when its shader has none)");
        foreach (string expected in new[] { "Chalkboard face", "Walls - Clock_01", "Subway tile backsplash", "Lamp shade", "CoffeeShelf_InteriorOak", "RepairWallTools_InteriorOak" })
            Check(faded.Any(r => r.name == expected), $"…including {expected}");
        bool shelf = faded.Any(r => r.name.StartsWith("Back bar cup shelf", StringComparison.Ordinal));
        Check(shelf, "…including the back-bar cup shelf (needs Second pass > Give the back-bar cup shelf its own mesh)");
        Check(faded.All(r => r.GetComponentInParent<Interactable>(true) == null && r.GetComponentInParent<Rigidbody>(true) == null),
            "Nothing faded is something Ace or a customer uses");
        // Things standing on the counters and the floor stay solid.
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        foreach (string stays in new[] { "Drinks - Bottle_01", "Drink countertop", "Carcass", "Body", "Coffee sacks" })
        {
            Renderer r = all.FirstOrDefault(x => x.name == stays && Vector3.Distance(x.bounds.center, new Vector3(x.bounds.center.x, 1f, 17.6f)) < 1.2f);
            if (r != null) Check(!SeeThroughMaterials.IsWorn(r) && !r.forceRenderingOff, $"{stays} ({Path3(r)}) stays solid: it stands on a counter or the floor");
        }
    }

    static void Photo(string file)
    {
        Camera cam = Camera.main;
        if (cam == null) { Note("No main camera for " + file); return; }
        var rt = RenderTexture.GetTemporary(1440, 900, 24);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cam.targetTexture;
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

    static string Path3(Renderer r)
    {
        string path = r.name;
        Transform t = r.transform.parent;
        for (int i = 0; i < 2 && t != null; i++, t = t.parent) path = t.name + "/" + path;
        return path;
    }

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
        string summary = $"Wall cut-away check: {checks - failures}/{checks} passed" + (failures > 0 ? $", {failures} FAILED" : "");
        report.Insert(0, summary + "\n\n");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        if (failures > 0) Debug.LogError("[Cut-away check] " + summary + "\n" + folder + "\n" + report);
        else Debug.Log("[Cut-away check] " + summary + "\n" + folder + "\n" + report);
    }
}
#endif
