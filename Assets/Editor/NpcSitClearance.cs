#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Measures whether seated NPC bodies pass through the chair they sit on (and
// the table in front of it), for every body look across the sit clips, and
// compares seat placements and chair models side by side.
//
// "Inside" is decided the honest way: rays from each body vertex are counted
// against the real chair and table meshes (odd = inside). Nothing in the scene
// changes; temporary actors and colliders are removed afterwards.
//
// Report: <project>/Logs/NpcSit/clearance-<time>.txt
public static class NpcSitClearance
{
    private const string Menu = "Fixit Fidget/NPC/Sit 5 - Measure bodies against the chairs";
    private const int ProbeLayer = 31;
    private static readonly Vector3[] Rays = { Vector3.right, Vector3.back, Vector3.up };

    private sealed class Obstacle
    {
        public string label;           // "chair" or "table"
        public GameObject probe;       // temporary collider copy
        public Bounds bounds;
    }

    private sealed class Tally
    {
        public int arm, torso, legs, head, table;
        public float armDepth, torsoDepth, legDepth;
        // "region bone, part of the chair" -> vertex-poses, to see what touches what.
        public readonly Dictionary<string, int> where = new(StringComparer.Ordinal);
        public int Total => arm + torso + legs + head;
        public void Add(Tally t)
        {
            arm += t.arm; torso += t.torso; legs += t.legs; head += t.head; table += t.table;
            armDepth = Mathf.Max(armDepth, t.armDepth); torsoDepth = Mathf.Max(torsoDepth, t.torsoDepth); legDepth = Mathf.Max(legDepth, t.legDepth);
            foreach (var pair in t.where) where[pair.Key] = (where.TryGetValue(pair.Key, out int n) ? n : 0) + pair.Value;
        }
        public void Note(string key) => where[key] = (where.TryGetValue(key, out int n) ? n : 0) + 1;
    }

    [MenuItem("Fixit Fidget/NPC/Sit 6 - Photograph where bodies pass through the chair")]
    private static void Show()
    {
        try { Debug.Log("[NPC sit] Clearance photos: " + Photograph()); }
        catch (Exception exception) { Debug.LogError("[NPC sit] Clearance photos FAILED: " + exception); }
    }

    // Hips this far behind the seat centre for the Sit 6 photos (NaN: the prefab's own setting).
    public static float PhotoHipBehind = float.NaN;

    // Marks every body vertex found inside the chair (red) or table (blue) for
    // a few bodies and poses, then photographs them from behind, the side and
    // above, and with the chair hidden.
    public static string Photograph()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Idle.anim");
        var talk = AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Talk.anim");
        TableSeat seat = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(s => s.SnapToSeat && s.transform.parent != null)
            .OrderBy(s => s.transform.parent.name, StringComparer.Ordinal).ThenBy(s => s.name, StringComparer.Ordinal).First();
        var probes = new List<GameObject>();
        var actors = new List<GameObject>();
        var markers = new List<GameObject>();
        bool backfaces = Physics.queriesHitBackfaces;
        string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcSit")),
            "clearance-photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var notes = new StringBuilder();
        try
        {
            Physics.queriesHitBackfaces = true;
            var obstacles = Obstacles(seat, false, probes);
            Physics.SyncTransforms();
            // Drawn on top of everything, so markers inside the chair still show.
            var red = new Material(Shader.Find("Hidden/Internal-Colored")) { color = Color.red, hideFlags = HideFlags.HideAndDontSave };
            red.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            red.renderQueue = 4000;
            var blue = new Material(red) { color = new Color(.1f, .4f, 1f), hideFlags = HideFlags.HideAndDontSave };
            // The chair itself, to photograph the body with the chair taken away.
            Vector3 seatCentre = seat.SeatPose.position;
            var chairRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
                .Where(r => r.enabled && new Vector2(r.bounds.center.x - seatCentre.x, r.bounds.center.z - seatCentre.z).magnitude < .33f
                         && r.bounds.max.y < 1.3f && r.bounds.min.y < .3f).ToArray();
            var customer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
            var patron = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Patron.prefab");
            foreach (var (prefab, look, clip, at) in new[] { (customer, -1, talk, 0f), (patron, 11, talk, 0f), (patron, 13, idle, 0f), (patron, 9, talk, .25f) })
            {
                GameObject actor = Spawn(prefab, look, actors);
                float behind = float.IsNaN(PhotoHipBehind)
                    ? new SerializedObject(actor.GetComponent<NpcSeating>()).FindProperty("hipBehindSeatCentre").floatValue : PhotoHipBehind;
                Place(actor, seat, behind);
                Sample(actor, clip, at * clip.length);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null) visual.Follow();
                var inside = Inside(actor, obstacles);
                notes.AppendLine((look < 0 ? "Original body" : "Look " + look) + $" {clip.name} @{at:0.00}, hips {behind * 100f:+0;-0} cm behind: " + inside.Count + " vertices inside; bones: "
                    + string.Join(", ", inside.GroupBy(v => v.bone).OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key + " " + g.Count())));
                int step = Mathf.Max(1, inside.Count / 500);
                for (int i = 0; i < inside.Count; i += step)
                {
                    var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    m.hideFlags = HideFlags.HideAndDontSave;
                    Object.DestroyImmediate(m.GetComponent<Collider>());
                    m.transform.position = inside[i].position;
                    m.transform.localScale = Vector3.one * .015f;
                    m.GetComponent<Renderer>().sharedMaterial = inside[i].label == "table" ? blue : red;
                    markers.Add(m);
                }
                // Bake the sampled pose into plain meshes before leaving animation mode.
                var baked = PolygonNpcSetup.BakePose(actor);
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                try
                {
                    Vector3 hip = seat.SeatPose.position + Vector3.up * .35f;
                    Vector3 forward = actor.transform.forward, right = actor.transform.right;
                    string who = (look < 0 ? "original" : "look" + look) + "-" + (clip == talk ? "talk" : "idle");
                    CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-1-behind.png"), hip - forward * 1.3f + Vector3.up * .45f, hip, 45f, false);
                    CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-2-side.png"), hip + right * 1.3f + Vector3.up * .15f, hip, 45f, false);
                    CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-3-above.png"), hip + Vector3.up * 1.6f - forward * .2f, hip, 45f, false);
                    CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-4-behind-left.png"), hip - forward * 1.0f - right * .7f + Vector3.up * .5f, hip, 45f, false);
                    foreach (var r in chairRenderers) r.forceRenderingOff = true;
                    try
                    {
                        CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-5-no-chair-side.png"), hip + right * 1.3f + Vector3.up * .15f, hip, 45f, false);
                        CafeSecondPassSteps.Capture(Path.Combine(folder, who + "-6-no-chair-behind.png"), hip - forward * 1.3f + Vector3.up * .45f, hip, 45f, false);
                    }
                    finally { foreach (var r in chairRenderers) r.forceRenderingOff = false; }
                }
                finally { PolygonNpcSetup.UnbakePose(baked); }
                foreach (var m in markers) Object.DestroyImmediate(m);
                markers.Clear();
                actors.Remove(actor);
                Object.DestroyImmediate(actor);
            }
            Object.DestroyImmediate(red);
            Object.DestroyImmediate(blue);
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (var m in markers) if (m != null) Object.DestroyImmediate(m);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            foreach (var probe in probes.ToArray()) Discard(probe, probes);
        }
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), notes.ToString());
        return folder + "\n" + notes;
    }

    private static List<(Vector3 position, string bone, string label)> Inside(GameObject actor, List<Obstacle> obstacles)
    {
        var result = new List<(Vector3, string, string)>();
        var visual = actor.GetComponent<PolygonNpcVisual>();
        IEnumerable<SkinnedMeshRenderer> skins = visual != null && visual.VisualInstance != null
            ? visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
            : actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled);
        int mask = 1 << ProbeLayer;
        foreach (SkinnedMeshRenderer skin in skins)
        {
            if (skin == null || skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
            Mesh world = PolygonNpcSetup.SkinToWorld(skin);
            Vector3[] vertices = world.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!Inside(vertices[v], mask, out _, out Collider hit)) continue;
                string label = obstacles.FirstOrDefault(o => o.probe == hit.gameObject)?.label ?? "chair";
                string bone = weights.Length == vertices.Length ? Dominant(weights[v], skin.bones) : "";
                result.Add((vertices[v], bone, label));
            }
            Object.DestroyImmediate(world);
        }
        return result;
    }

    [MenuItem("Fixit Fidget/NPC/Sit 8 - Export seated bodies and chairs for plotting")]
    private static void ExportMenu()
    {
        try { Debug.Log("[NPC sit] Export: " + Export()); }
        catch (Exception exception) { Debug.LogError("[NPC sit] Export FAILED: " + exception); }
    }

    // Writes the chair, table and a few seated bodies as OBJ files in the seat's
    // own frame (origin at the seat surface centre, +z towards the table, +x to
    // the sitter's right), plus every joint position, for side and top plots.
    public static string Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        TableSeat seat = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(s => s.SnapToSeat && s.transform.parent != null)
            .OrderBy(s => s.transform.parent.name, StringComparer.Ordinal).ThenBy(s => s.name, StringComparer.Ordinal).First();
        string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcSit")),
            "export-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var probes = new List<GameObject>();
        var actors = new List<GameObject>();
        var notes = new StringBuilder();
        try
        {
            var customer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
            var patron = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Patron.prefab");
            GameObject frameActor = Spawn(customer, -1, actors);
            float behind = new SerializedObject(frameActor.GetComponent<NpcSeating>()).FindProperty("hipBehindSeatCentre").floatValue;
            Place(frameActor, seat, behind);
            Matrix4x4 toSeat = Matrix4x4.TRS(seat.SeatPose.position, frameActor.transform.rotation, Vector3.one).inverse;
            notes.AppendLine($"Seat {Describe(seat)}: surface centre {seat.SeatPose.position}, facing {frameActor.transform.eulerAngles.y:0} deg, " +
                             $"hips {behind * 100f:0} cm behind the centre, floor at y {toSeat.MultiplyPoint3x4(seat.StandPoint.position).y:0.000} in seat space");
            actors.Remove(frameActor);
            Object.DestroyImmediate(frameActor);

            foreach (var (label, original) in new[] { ("chair-timber", false), ("chair-owner", true) })
            {
                var obstacles = Obstacles(seat, original, probes);
                WriteObj(Path.Combine(folder, label + ".obj"),
                    obstacles.Where(o => o.label == "chair").Select(o => o.probe.GetComponent<MeshCollider>().sharedMesh), toSeat);
                if (!original)
                    WriteObj(Path.Combine(folder, "table.obj"),
                        obstacles.Where(o => o.label == "table").Select(o => o.probe.GetComponent<MeshCollider>().sharedMesh), toSeat);
                foreach (var o in obstacles) Discard(o.probe, probes);
            }

            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Idle.anim");
            var talk = AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Talk.anim");
            foreach (var (prefab, look) in new[] { (customer, -1), (patron, 0), (patron, 2), (patron, 5), (patron, 9), (patron, 13) })
                foreach (var (poseName, clip, at) in new[] { ("idle", idle, 0f), ("talk", talk, .5f) })
                {
                    GameObject actor = Spawn(prefab, look, actors);
                    Place(actor, seat, behind);
                    Sample(actor, clip, at * clip.length);
                    var visual = actor.GetComponent<PolygonNpcVisual>();
                    if (visual != null) visual.Follow();
                    string who = look < 0 ? "original" : "look" + look;
                    WriteBody(Path.Combine(folder, $"body-{who}-{poseName}.obj"), actor, toSeat);
                    actors.Remove(actor);
                    Object.DestroyImmediate(actor);
                }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            foreach (var probe in probes.ToArray()) Discard(probe, probes);
        }
        File.WriteAllText(Path.Combine(folder, "notes.txt"), notes.ToString());
        return folder + "\n" + notes;
    }

    private static void AppendPoint(StringBuilder text, string prefix, Vector3 p) =>
        text.Append(prefix).Append(' ').Append(p.x.ToString("0.####", CultureInfo.InvariantCulture)).Append(' ')
            .Append(p.y.ToString("0.####", CultureInfo.InvariantCulture)).Append(' ')
            .Append(p.z.ToString("0.####", CultureInfo.InvariantCulture)).Append('\n');

    private static void WriteObj(string path, IEnumerable<Mesh> meshes, Matrix4x4 toSeat)
    {
        var text = new StringBuilder();
        int offset = 1, part = 0;
        foreach (Mesh mesh in meshes)
        {
            text.Append("o part").Append(part++).Append('\n');
            Vector3[] vertices = mesh.vertices;
            foreach (Vector3 v in vertices) AppendPoint(text, "v", toSeat.MultiplyPoint3x4(v));
            int[] t = mesh.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3)
                text.Append("f ").Append(t[i] + offset).Append(' ').Append(t[i + 1] + offset).Append(' ').Append(t[i + 2] + offset).Append('\n');
            offset += vertices.Length;
        }
        File.WriteAllText(path, text.ToString());
    }

    private static void WriteBody(string path, GameObject actor, Matrix4x4 toSeat)
    {
        var visual = actor.GetComponent<PolygonNpcVisual>();
        IEnumerable<SkinnedMeshRenderer> skins = visual != null && visual.VisualInstance != null
            ? visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
            : actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled);
        var vertexText = new StringBuilder();
        var groups = new[] { new StringBuilder(), new StringBuilder(), new StringBuilder(), new StringBuilder() };
        int offset = 1;
        foreach (SkinnedMeshRenderer skin in skins)
        {
            if (skin == null || skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
            Mesh world = PolygonNpcSetup.SkinToWorld(skin);
            Vector3[] vertices = world.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Transform[] bones = skin.bones;
            var region = new int[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                region[v] = weights.Length == vertices.Length ? Region(Dominant(weights[v], bones)) : 1;
                AppendPoint(vertexText, "v", toSeat.MultiplyPoint3x4(vertices[v]));
            }
            int[] t = world.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3)
                groups[region[t[i]]].Append("f ").Append(t[i] + offset).Append(' ').Append(t[i + 1] + offset).Append(' ').Append(t[i + 2] + offset).Append('\n');
            offset += vertices.Length;
            Object.DestroyImmediate(world);
        }
        var text = new StringBuilder().Append(vertexText);
        string[] names = { "arm", "torso", "legs", "head" };
        for (int g = 0; g < names.Length; g++) text.Append("g ").Append(names[g]).Append('\n').Append(groups[g]);
        File.WriteAllText(path, text.ToString());

        var joints = new StringBuilder();
        Transform city = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        foreach (Transform t in actor.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Renderer>() != null) continue;
            AppendPoint(joints, (city != null && t.IsChildOf(city) ? "city " : "rig ") + t.name.Replace(' ', '_'), toSeat.MultiplyPoint3x4(t.position));
        }
        File.WriteAllText(Path.ChangeExtension(path, ".joints.txt"), joints.ToString());
    }

    [MenuItem("Fixit Fidget/NPC/Sit 7 - Compare city bodies with the rig they follow")]
    private static void CompareFollow()
    {
        try { Debug.Log("[NPC sit] Follow comparison: " + FollowReport()); }
        catch (Exception exception) { Debug.LogError("[NPC sit] Follow comparison FAILED: " + exception); }
    }

    // For each mapped limb, the angle between where the actor's own bone points
    // and where the city body's matching bone points, in the sit and walk poses.
    public static string FollowReport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var patron = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Patron.prefab");
        var clipsToTry = new List<(string name, AnimationClip clip, float at)>
        {
            ("sit idle", AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Idle.anim"), 0f),
            ("sit talk", AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/Npc Sitting Talk.anim"), .4f),
        };
        var animator = patron.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c != null && (c.name.ToLowerInvariant().Contains("walk") || c.name.ToLowerInvariant().Contains("idle")) && clipsToTry.All(t => t.clip != c))
                    clipsToTry.Add((c.name, c, .3f));
        var pairs = new[] { ("UpperArm.L", "LowerArm.L", "Shoulder_L", "Elbow_L"), ("LowerArm.L", "Wrist.L", "Elbow_L", "Hand_L"),
                            ("UpperArm.R", "LowerArm.R", "Shoulder_R", "Elbow_R"), ("LowerArm.R", "Wrist.R", "Elbow_R", "Hand_R"),
                            ("UpperLeg.L", "LowerLeg.L", "UpperLeg_L", "LowerLeg_L"), ("LowerLeg.L", "Foot.L", "LowerLeg_L", "Ankle_L"),
                            ("Abdomen", "Neck", "Spine_01", "Neck") };
        var report = new StringBuilder();
        var actors = new List<GameObject>();
        try
        {
            foreach (int look in new[] { 0, 3, 13 })
            {
                GameObject actor = Spawn(patron, look, actors);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual == null || visual.VisualInstance == null) { report.AppendLine("Look " + look + ": no city body"); continue; }
                var src = Index(actor.transform, visual.VisualInstance.transform);
                var dst = Index(visual.VisualInstance.transform, null);
                foreach (var (name, clip, at) in clipsToTry)
                {
                    if (clip == null) continue;
                    Sample(actor, clip, at * clip.length);
                    visual.Follow();
                    report.Append("Look " + look + ", " + name + ":");
                    foreach (var (a, b, ta, tb) in pairs)
                    {
                        if (!src.ContainsKey(a) || !src.ContainsKey(b) || !dst.ContainsKey(ta) || !dst.ContainsKey(tb)) { report.Append(" " + a + " missing;"); continue; }
                        Vector3 ds = src[b].position - src[a].position, dt = dst[tb].position - dst[ta].position;
                        Vector3 local = actor.transform.InverseTransformDirection(ds.normalized), localT = actor.transform.InverseTransformDirection(dt.normalized);
                        report.Append($" {a} {Vector3.Angle(ds, dt):0}deg (rig {Fmt(local)} city {Fmt(localT)});");
                    }
                    report.AppendLine();
                }
                actors.Remove(actor);
                Object.DestroyImmediate(actor);
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
        }
        string path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcSit")),
            "follow-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        File.WriteAllText(path, report.ToString());
        return path + "\n" + report;
    }

    private static string Fmt(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";

    private static Dictionary<string, Transform> Index(Transform root, Transform exclude)
    {
        var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (exclude != null && t.IsChildOf(exclude)) continue;
            if (!map.ContainsKey(t.name)) map[t.name] = t;
        }
        return map;
    }

    [MenuItem(Menu)]
    private static void Run()
    {
        try { Debug.Log("[NPC sit] Clearance: " + Measure()); }
        catch (Exception exception) { Debug.LogError("[NPC sit] Clearance FAILED: " + exception); }
    }

    public static string Measure(float[] offsets = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        offsets ??= new[] { .06f, .02f, 0f, -.01f, -.02f, -.04f };
        var clips = new[] { "Npc Sit Down", "Npc Sitting Idle", "Npc Sitting Talk", "Npc Stand Up" }
            .ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcSitAnimations.Folder + "/" + n + ".anim"));
        if (clips.Values.Any(c => c == null)) throw new InvalidOperationException("Run Sit 1 first.");
        var poses = new (string clip, float at)[]
        {
            ("Npc Sit Down", 1f), ("Npc Sitting Idle", 0f), ("Npc Sitting Idle", .5f),
            ("Npc Sitting Talk", 0f), ("Npc Sitting Talk", .25f), ("Npc Sitting Talk", .5f), ("Npc Sitting Talk", .75f),
            ("Npc Stand Up", 0f),
        };

        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(s => s.SnapToSeat && s.transform.parent != null)
            .OrderBy(s => s.transform.parent.name, StringComparer.Ordinal).ThenBy(s => s.name, StringComparer.Ordinal).ToArray();
        if (seats.Length == 0) throw new InvalidOperationException("No sitting seats in the open scene.");
        // Two seats facing opposite ways at the same table.
        TableSeat first = seats[0];
        TableSeat second = seats.Where(s => s.transform.parent == first.transform.parent && s != first)
            .OrderByDescending(s => (s.SeatPose.position - first.SeatPose.position).sqrMagnitude).FirstOrDefault() ?? first;

        var report = new StringBuilder();
        var probes = new List<GameObject>();
        var actors = new List<GameObject>();
        bool backfaces = Physics.queriesHitBackfaces;
        int onProbeLayer = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Count(c => c.gameObject.layer == ProbeLayer);
        try
        {
            Physics.queriesHitBackfaces = true;
            var customer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
            var patron = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Patron.prefab");
            var patronVisual = patron.GetComponent<PolygonNpcVisual>();
            int looks = patronVisual != null ? patronVisual.AppearanceCount : 0;
            var bodies = new List<(GameObject prefab, int look, string name)> { (customer, -1, "Original body") };
            for (int i = 0; i < looks; i++) bodies.Add((patron, i, "Look " + i));

            var chairSets = new List<(string name, Func<TableSeat, List<Obstacle>> build)>
            {
                ("Current timber chairs", seat => Obstacles(seat, useOriginalChair: false, probes)),
                ("Original owner chairs", seat => Obstacles(seat, useOriginalChair: true, probes)),
            };
            report.AppendLine($"Seats: {Describe(first)} and {Describe(second)}; {bodies.Count} bodies x {poses.Length} poses per placement.");
            if (onProbeLayer > 0) report.AppendLine($"WARNING: {onProbeLayer} scene colliders already use layer {ProbeLayer}; results may include them.");

            var worstBodies = new Dictionary<string, Tally>();
            foreach (var (setName, build) in chairSets)
            {
                var sets = new[] { first, second }.Distinct().Select(s => (seat: s, obstacles: build(s))).ToArray();
                // Both seats' probes are present at once (they share the table), so
                // a hit is labelled by whichever list made the probe it struck.
                var labels = new Dictionary<GameObject, string>();
                foreach (var set in sets) foreach (var o in set.obstacles) labels[o.probe] = o.label;
                if (sets.Any(s => s.obstacles.All(o => o.label != "chair")))
                {
                    report.AppendLine($"{setName}: no chair mesh found at the seat, skipped.");
                    foreach (var s in sets) foreach (var o in s.obstacles) Discard(o.probe, probes);
                    continue;
                }
                Physics.SyncTransforms();
                report.AppendLine();
                report.AppendLine("== " + setName + " ==");
                foreach (float behind in offsets)
                {
                    var total = new Tally();
                    var byPose = new Dictionary<string, Tally>();
                    foreach (var body in bodies)
                    {
                        var perBody = new Tally();
                        foreach (var (seat, obstacles) in sets)
                        {
                            GameObject actor = Spawn(body.prefab, body.look, actors);
                            Place(actor, seat, behind);
                            foreach (var (clip, at) in poses)
                            {
                                AnimationClip c = clips[clip];
                                Sample(actor, c, at * c.length);
                                var visual = actor.GetComponent<PolygonNpcVisual>();
                                if (visual != null) visual.Follow();
                                Tally t = Count(actor, obstacles, seat, labels);
                                perBody.Add(t);
                                string poseKey = clip + " @" + at.ToString("0.00", CultureInfo.InvariantCulture);
                                if (!byPose.TryGetValue(poseKey, out Tally pt)) byPose[poseKey] = pt = new Tally();
                                pt.Add(t);
                            }
                            actors.Remove(actor);
                            Object.DestroyImmediate(actor);
                        }
                        total.Add(perBody);
                        string key = setName + " @" + behind.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " " + body.name;
                        worstBodies[key] = perBody;
                    }
                    report.AppendLine($"hips {behind * 100f:+0;-0} cm behind the seat centre: body inside the chair {total.Total} vertex-poses " +
                        $"(arms/shoulders {total.arm}, torso {total.torso}, legs {total.legs}, head {total.head}); inside the table {total.table}; " +
                        $"deepest arm {total.armDepth * 100f:0.0} cm, torso {total.torsoDepth * 100f:0.0} cm, legs {total.legDepth * 100f:0.0} cm");
                    report.AppendLine("      where: " + string.Join(", ", total.where.OrderByDescending(pair => pair.Value).Take(12).Select(pair => pair.Key + " " + pair.Value)));
                    foreach (var pair in byPose)
                        report.AppendLine($"      {pair.Key}: arms {pair.Value.arm}, torso {pair.Value.torso}, legs {pair.Value.legs}, table {pair.Value.table}");
                }
                foreach (var s in sets) foreach (var o in s.obstacles) Discard(o.probe, probes);
            }

            report.AppendLine();
            report.AppendLine("---- Worst bodies (arms/shoulders inside the chair, current placement first) ----");
            foreach (var pair in worstBodies.OrderByDescending(p => p.Value.arm).Take(24))
                report.AppendLine($"{pair.Key}: arms {pair.Value.arm} (deepest {pair.Value.armDepth * 100f:0.0} cm), torso {pair.Value.torso}, legs {pair.Value.legs}, table {pair.Value.table}");
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            foreach (var probe in probes.ToArray()) Discard(probe, probes);
        }
        string path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcSit")),
            "clearance-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString());
        return path + "\n" + report;
    }

    // The chair (and table) meshes around a seat, as temporary colliders.
    private static List<Obstacle> Obstacles(TableSeat seat, bool useOriginalChair, List<GameObject> probes)
    {
        var result = new List<Obstacle>();
        Vector3 centre = seat.SeatPose.position;
        var zone = new Bounds(new Vector3(centre.x, .6f, centre.z), new Vector3(2.4f, 1.4f, 2.4f));
        var seatRenderer = seat.GetComponent<MeshRenderer>();
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Bounds b = WorldBounds(filter);
            if (!b.Intersects(zone)) continue;
            if (b.size.y > 1.6f || b.min.y > .3f) continue;              // floor-standing furniture only
            Vector2 offset = new Vector2(b.center.x - centre.x, b.center.z - centre.z);
            bool chair = offset.magnitude < .33f && b.max.y < 1.3f;
            bool table = !chair && seat.CupSpot != null
                && new Vector2(b.center.x - seat.CupSpot.position.x, b.center.z - seat.CupSpot.position.z).magnitude < .7f
                && b.max.y > .6f && b.max.y < 1f;
            if (renderer == seatRenderer)
            {
                if (!useOriginalChair) continue;                          // hidden original, unless comparing it
                chair = true; table = false;
            }
            else if (chair && useOriginalChair) continue;                 // the replacement chair stands where the original does
            if (!renderer.enabled && renderer != seatRenderer) continue;
            if (!chair && !table) continue;
            result.Add(Probe(renderer, filter.sharedMesh, chair ? "chair" : "table", probes));
        }
        return result;
    }

    // The mesh is copied into world space, so any parent scale or rotation is exact.
    private static Obstacle Probe(Renderer renderer, Mesh mesh, string label, List<GameObject> probes)
    {
        Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
        var world = new Mesh { name = "Clearance probe mesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = mesh.indexFormat };
        world.vertices = vertices;
        var triangles = new List<int>();
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
            if (mesh.GetTopology(sub) == MeshTopology.Triangles) triangles.AddRange(mesh.GetTriangles(sub));
        world.SetTriangles(triangles, 0);
        world.RecalculateBounds();
        var probe = new GameObject("Clearance probe") { hideFlags = HideFlags.HideAndDontSave, layer = ProbeLayer };
        probe.AddComponent<MeshCollider>().sharedMesh = world;
        probes.Add(probe);
        return new Obstacle { label = label, probe = probe, bounds = world.bounds };
    }

    private static void Discard(GameObject probe, List<GameObject> probes)
    {
        probes.Remove(probe);
        if (probe == null) return;
        var collider = probe.GetComponent<MeshCollider>();
        if (collider != null && collider.sharedMesh != null) Object.DestroyImmediate(collider.sharedMesh);
        Object.DestroyImmediate(probe);
    }

    private static GameObject Spawn(GameObject prefab, int look, List<GameObject> actors)
    {
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        actors.Add(actor);
        var visual = actor.GetComponent<PolygonNpcVisual>();
        if (visual != null)
        {
            if (look >= 0 && visual.AppearanceCount > 0) visual.ApplyAppearance(look % visual.AppearanceCount);
            else visual.RemoveAppearance();
            // As NpcSeating does in play: the body fits itself to the chair once the rig's hips are down.
            visual.Seated = true;
        }
        return actor;
    }

    // NpcSeating's own placement, with the hips moved along the seat.
    private static void Place(GameObject actor, TableSeat seat, float behind)
    {
        var seating = actor.GetComponent<NpcSeating>();
        seating.Placement(seat, seat.StandPoint.position.y, out Vector3 feet, out Quaternion facing);
        float current = new SerializedObject(seating).FindProperty("hipBehindSeatCentre").floatValue;
        feet += facing * Vector3.forward * (current - behind);
        actor.transform.SetPositionAndRotation(feet, facing);
    }

    private static void Sample(GameObject actor, AnimationClip clip, float time)
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(actor, clip, Mathf.Min(time, clip.length - 1e-4f));
        AnimationMode.EndSampling();
    }

    private static Tally Count(GameObject actor, List<Obstacle> obstacles, TableSeat seat, Dictionary<GameObject, string> labels)
    {
        Quaternion toSeat = Quaternion.Inverse(actor.transform.rotation);
        Vector3 seatCentre = seat.SeatPose.position;
        var tally = new Tally();
        var visual = actor.GetComponent<PolygonNpcVisual>();
        IEnumerable<SkinnedMeshRenderer> skins = visual != null && visual.VisualInstance != null
            ? visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
            : actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled);
        int mask = 1 << ProbeLayer;
        Bounds chairBounds = Envelope(obstacles, "chair"), tableBounds = Envelope(obstacles, "table");
        foreach (SkinnedMeshRenderer skin in skins)
        {
            if (skin == null || skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
            Mesh world = PolygonNpcSetup.SkinToWorld(skin);
            Vector3[] vertices = world.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Transform[] bones = skin.bones;
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                bool nearChair = chairBounds.size != Vector3.zero && chairBounds.Contains(p);
                bool nearTable = tableBounds.size != Vector3.zero && tableBounds.Contains(p);
                if (!nearChair && !nearTable) continue;
                if (!Inside(p, mask, out float depth, out Collider hit)) continue;
                string label = labels.TryGetValue(hit.gameObject, out string l) ? l : "chair";
                string bone = weights.Length == vertices.Length ? Dominant(weights[v], bones) : skin.rootBone != null ? skin.rootBone.name : "";
                Vector3 local = toSeat * (p - seatCentre);
                string part = label == "table" ? (local.y > .15f ? "table top" : "table base")
                    : local.y < .02f ? (local.z > .12f ? "seat front" : "seat") : local.z < -.1f ? (local.y > .28f ? "back top" : "back low") : "chair other";
                tally.Note(bone + " in " + part);
                if (label == "table") { tally.table++; continue; }
                switch (Region(bone))
                {
                    case 0: tally.arm++; tally.armDepth = Mathf.Max(tally.armDepth, depth); break;
                    case 1: tally.torso++; tally.torsoDepth = Mathf.Max(tally.torsoDepth, depth); break;
                    case 2: tally.legs++; tally.legDepth = Mathf.Max(tally.legDepth, depth); break;
                    default: tally.head++; break;
                }
            }
            Object.DestroyImmediate(world);
        }
        return tally;
    }

    private static Bounds WorldBounds(MeshFilter filter)
    {
        Bounds local = filter.sharedMesh.bounds;
        Matrix4x4 m = filter.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return b;
    }

    private static Bounds Envelope(List<Obstacle> obstacles, string label)
    {
        var list = obstacles.Where(o => o.label == label).ToList();
        if (list.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Bounds b = list[0].bounds;
        foreach (var o in list) b.Encapsulate(o.bounds);
        b.Expand(.02f);
        return b;
    }

    // Odd crossings along every probe ray = inside a closed mesh. Depth is the
    // shortest way out along those rays.
    private static bool Inside(Vector3 p, int mask, out float depth, out Collider collider)
    {
        depth = float.MaxValue;
        collider = null;
        foreach (Vector3 direction in Rays)
        {
            int crossings = 0;
            Vector3 origin = p;
            float firstHit = -1f;
            Collider firstCollider = null;
            for (int i = 0; i < 24; i++)
            {
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Collide)) break;
                if (firstHit < 0f) { firstHit = Vector3.Distance(p, hit.point); firstCollider = hit.collider; }
                crossings++;
                origin = hit.point + direction * 2e-4f;
            }
            if (crossings % 2 == 0) return false;
            depth = Mathf.Min(depth, firstHit);
            collider ??= firstCollider;
        }
        return collider != null;
    }

    private static string Dominant(BoneWeight w, Transform[] bones)
    {
        int index = w.boneIndex0;
        float best = w.weight0;
        if (w.weight1 > best) { best = w.weight1; index = w.boneIndex1; }
        if (w.weight2 > best) { best = w.weight2; index = w.boneIndex2; }
        if (w.weight3 > best) { index = w.boneIndex3; }
        return index >= 0 && index < bones.Length && bones[index] != null ? bones[index].name : "";
    }

    // 0 arm/shoulder, 1 torso, 2 legs, 3 head/other.
    private static int Region(string bone)
    {
        string b = bone.ToLowerInvariant();
        if (b.Contains("shoulder") || b.Contains("clavicle") || b.Contains("arm") || b.Contains("elbow") || b.Contains("hand")
            || b.Contains("wrist") || b.Contains("finger") || b.Contains("thumb") || b.Contains("index") || b.Contains("middle")
            || b.Contains("ring") || b.Contains("pinky")) return 0;
        if (b.Contains("leg") || b.Contains("knee") || b.Contains("ankle") || b.Contains("foot") || b.Contains("toe")
            || b.Contains("thigh") || b.Contains("calf")) return 2;
        if (b.Contains("head") || b.Contains("eye") || b.Contains("jaw")) return 3;
        return 1;
    }

    private static string Describe(TableSeat seat) => seat.transform.parent.name + "/" + seat.name;
}
#endif
