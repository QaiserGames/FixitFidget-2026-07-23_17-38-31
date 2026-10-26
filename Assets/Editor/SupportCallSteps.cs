#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE SUPPORT CALL'S PHONE: THE DETAILED PHONE'S PARTS (24 Oct 2026; the art audit's a03, "The support-call phone is a grey
// block": "give the support-call phone the detailed phone's parts, then rerun Bench 7's layout and check it fits the intake
// tray". His answers: a03 "yes, do that"; p4, the support call "on Day 3, once it has its real phone model").
//
//   Fixit Fidget > Days > Support call 1 - The support call's phone: the detailed parts (prefab)
//   Fixit Fidget > Days > Support call 1 - Undo: the grey block back (prefab)
//
// PhoneJob.prefab (the support call: a HoldCallJob, "Call support" / "Answer support" / "Pick up the phone" on its Cube's
// PhoneInteractable) was a 7 x 1.2 x 14 cm default cube in the default grey. This step keeps everything the call runs on and
// only changes what it looks like:
//   * "Phone (the detailed model)": copies of the hero phone's own parts from PhoneRepair.prefab (the body, the back cover,
//     the intact glass, the two screws), at their poses, mesh and materials only: no collider, no part, no script, so nothing
//     on it can be hovered, grabbed or graded (the call is the job, never the bench);
//   * the job's colour tag (a copy of the phone's JobMarker): the support phone on the intake tray carries its customer's
//     colour like every other device (IntakeShelf: "the job number and colour are what tie it back to a person");
//   * the Cube stays (its PhoneInteractable and BoxCollider are the call's E and click), resized to the phone's own box, its
//     grey renderer switched off;
//   * the rest height becomes the walk-in phone's (the same parts, the same lowest point), so it lies on the intake tray's
//     felt exactly as the walk-in phone does; realSize on (the parts are the one-scale phone's).
// Then Bench 7 (Fixit Fidget > Bench > Bench 7 - The forgiving stage) can be run again: it already lays PhoneJob out as the
// phone (ForgivingBenchSteps' table), and the step's own checks measure the fit against the walk-in phone and, with the café
// scene open, against the intake tray itself.
//
// Prefab contents have no undo in Unity: the step edits the loaded contents, runs its checks, and saves only when they all
// pass (nothing is saved otherwise); the Undo item takes it back to the grey block. Idempotent: a second run changes nothing.
// Report: Logs/Days/support-call-1-<time>.txt and -last.txt, and one PASS/DONE line in the Console.
// ---------------------------------------------------------------------------
public static class SupportCallSteps
{
    const string Menu = "Fixit Fidget/Days/";
    const string Tag = "[Support call 1] ";
    public const string MenuStep = Menu + "Support call 1 - The support call's phone: the detailed parts (prefab)";
    public const string MenuUndo = Menu + "Support call 1 - Undo: the grey block back (prefab)";

    public const string CallPath = PacingSteps.CallPath, PhonePath = PacingSteps.PhonePath;
    /// <summary>The detailed model's root under PhoneJob.</summary>
    public const string ModelName = "Phone (the detailed model)";
    const string CubeName = "Cube";
    // The hero phone's parts the support phone shows (PhoneRepair.prefab): the closed phone with its intact glass.
    static readonly string[] Parts = { "Body", "BackCover", "Screen/Glass", "Screw0", "Screw1" };
    const string GlassFallback = "Screen/Fresh";
    // The grey block as it was (for the Undo).
    static readonly Vector3 CubeScaleWas = new Vector3(.07f, .012f, .14f);
    const float RestHeightWas = .01f;

    /// <summary>
    /// Has the support call's phone the detailed phone's parts (this step has run)? The days' data asks before putting the call on
    /// Day 3 (his answer p4): the model's root with at least three meshes drawn, and the grey block no longer drawn.
    /// </summary>
    public static bool HasRealModel(GameObject callPrefab)
    {
        if (callPrefab == null) return false;
        Transform model = callPrefab.transform.Find(ModelName);
        if (model == null) return false;
        int drawn = model.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null);
        Transform cube = callPrefab.transform.Find(CubeName);
        MeshRenderer grey = cube != null ? cube.GetComponent<MeshRenderer>() : null;
        return drawn >= 3 && (grey == null || !grey.enabled);
    }

    [MenuItem(MenuStep, false, 41)]
    static void Step() => Perform(false);

    [MenuItem(MenuUndo, false, 42)]
    static void UndoStep() => Perform(true);

    [MenuItem(MenuStep, true)]
    [MenuItem(MenuUndo, true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    sealed class Report
    {
        public readonly System.Text.StringBuilder text = new System.Text.StringBuilder();
        public int passes, failures, changes;
        public bool Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            text.AppendLine((ok ? "  PASS   " : "  FAIL   ") + what);
            return ok;
        }
        public void Info(string line) => text.AppendLine("  " + line);
        public void Changed(string line) { changes++; text.AppendLine("  CHANGE " + line); }
    }

    static void Perform(bool undo)
    {
        string title = undo ? "Support call 1 - Undo: the grey block back (prefab)" : "Support call 1 - The support call's phone: the detailed parts (prefab)";
        var r = new Report();
        r.text.AppendLine($"{title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        GameObject contents = null;
        bool saved = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            GameObject call = AssetDatabase.LoadAssetAtPath<GameObject>(CallPath);
            GameObject phone = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
            if (call == null) throw new InvalidOperationException($"No prefab at {CallPath}. Nothing changed.");
            if (phone == null) throw new InvalidOperationException($"No prefab at {PhonePath}. Nothing changed.");
            if (call.GetComponent<HoldCallJob>() == null || call.GetComponent<DeviceDefinition>() == null)
                throw new InvalidOperationException($"{CallPath} has no HoldCallJob or DeviceDefinition on its root: not the support call this step knows. Nothing changed; tell Claude.");
            Transform cubeAsset = call.transform.Find(CubeName);
            if (cubeAsset == null || cubeAsset.GetComponent<PhoneInteractable>() == null || cubeAsset.GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException($"{CallPath} has no \"{CubeName}\" with its PhoneInteractable and BoxCollider (the call's E): nothing changed; tell Claude.");
            var sources = new List<(string path, Transform t)>();
            foreach (string p in Parts)
            {
                Transform t = phone.transform.Find(p);
                if (t == null && p == "Screen/Glass") t = phone.transform.Find(GlassFallback);
                if (t == null || t.GetComponent<MeshFilter>() == null || t.GetComponent<MeshFilter>().sharedMesh == null || t.GetComponent<MeshRenderer>() == null)
                    throw new InvalidOperationException($"{PhonePath} has no drawn \"{p}\" (the hero phone's parts: run Fixit Fidget › Bench › Devices 2 first). Nothing changed.");
                sources.Add((PathIn(phone.transform, t), t));
            }
            Transform markerSource = phone.GetComponentsInChildren<JobMarker>(true).Select(m => m.transform).FirstOrDefault();
            float phoneRest = phone.GetComponent<JobBase>() != null ? phone.GetComponent<JobBase>().restHeight : .0091f;
            r.Info($"survey: the support call's phone is {(HasRealModel(call) ? "already the detailed model" : "the grey block")}; the walk-in phone's rest height {phoneRest:0.0000} m; " +
                   $"parts to copy: {string.Join(", ", sources.Select(s => s.path))}{(markerSource != null ? "; and its colour tag (JobMarker)" : "")}");

            contents = PrefabUtility.LoadPrefabContents(CallPath);
            if (undo) TakeBack(r, contents);
            else Build(r, contents, sources, markerSource, phoneRest);

            r.text.AppendLine().AppendLine("Checks");
            if (undo) CheckGrey(r, contents);
            else CheckReal(r, contents, phone, phoneRest);

            if (r.failures > 0) throw new InvalidOperationException($"{r.failures} check(s) failed: nothing was saved (the prefab is as it was).");
            if (r.changes > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, CallPath);
                saved = true;
                r.Info("saved prefab " + CallPath);
            }
            r.text.AppendLine().AppendLine(r.changes == 0 ? $"NOTHING TO CHANGE: already done. {r.passes} check(s) passed."
                : $"DONE: {r.changes} change(s); {r.passes} check(s) passed; saved." +
                  (undo ? "" : " Next: Fixit Fidget › Bench › Bench 7 again (it lays PhoneJob out as the phone), then Fixit Fidget › Days › G (Day 3 gets the call)."));
            Debug.Log(Tag + $"{(r.changes == 0 ? "PASS" : "DONE")}: {title} ({r.changes} change(s), {r.passes} check(s) passed). Report: {Write(r, undo)}");
        }
        catch (Exception e)
        {
            r.text.AppendLine().AppendLine("REFUSED: " + e.Message + (saved ? "" : " (nothing was saved)"));
            Debug.LogError(Tag + $"{title}: {e.Message}  Report: {Write(r, undo)}");
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static string PathIn(Transform root, Transform t)
    {
        var names = new List<string>();
        for (Transform x = t; x != null && x != root; x = x.parent) names.Add(x.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Days");

    static string Write(Report r, bool undo)
    {
        Directory.CreateDirectory(LogRoot);
        string label = undo ? "support-call-1-undo" : "support-call-1";
        string path = Path.Combine(LogRoot, $"{label}-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(path, r.text.ToString());
        File.WriteAllText(Path.Combine(LogRoot, $"{label}-last.txt"), r.text.ToString());
        return path;
    }

    // ---------- the change ----------

    static void Build(Report r, GameObject root, List<(string path, Transform t)> sources, Transform markerSource, float phoneRest)
    {
        Transform model = root.transform.Find(ModelName);
        string want = string.Join("\n", sources.Select(s => Signature(s.path, s.t)));
        string have = model != null ? string.Join("\n", Copies(model).Select(c => Signature(c.path, c.t))) : "";
        if (model != null && have == want) r.Info($"\"{ModelName}\": already the hero phone's {sources.Count} parts");
        else
        {
            bool again = model != null;
            if (again) Object.DestroyImmediate(model.gameObject);
            model = new GameObject(ModelName).transform;
            model.SetParent(root.transform, false);
            model.gameObject.layer = root.layer;
            foreach (var (path, t) in sources) CopyPart(model, path, t);
            r.Changed($"\"{ModelName}\": {(again ? "re-copied (the hero phone changed)" : "made")}: {string.Join(", ", sources.Select(s => s.path))} (meshes and materials only: no collider, no part, no script)");
        }

        if (markerSource != null && root.GetComponentsInChildren<JobMarker>(true).Length == 0)
        {
            GameObject marker = Object.Instantiate(markerSource.gameObject, root.transform, false);
            marker.name = markerSource.name;
            marker.transform.localPosition = markerSource.localPosition;
            marker.transform.localRotation = markerSource.localRotation;
            marker.transform.localScale = markerSource.localScale;
            // Left on, as on the phone: its Awake hides it at spawn, and CustomerBrain's Show turns it on in the customer's colour.
            marker.SetActive(markerSource.gameObject.activeSelf);
            r.Changed("the job's colour tag: a copy of the phone's JobMarker (hidden at spawn, shown in the customer's colour, as on every device)");
        }
        else r.Info(markerSource == null ? "the phone has no JobMarker to copy (the support phone keeps none)" : "the job's colour tag: already there");

        // The Cube: the call's E and click, now the phone's own box; its grey block no longer drawn.
        Transform cube = root.transform.Find(CubeName);
        Bounds box = ModelBox(root.transform, model);
        Vector3 centre = box.center, size = new Vector3(Mathf.Max(.001f, box.size.x), Mathf.Max(.001f, box.size.y), Mathf.Max(.001f, box.size.z));
        if (Near(cube.localPosition, centre) && Near(cube.localScale, size) && Quaternion.Angle(cube.localRotation, Quaternion.identity) < .01f)
            r.Info($"the Cube (the call's E): already the phone's box ({Mm(size)} at {Mm(centre)})");
        else
        {
            string was = $"{Mm(cube.localScale)} at {Mm(cube.localPosition)}";
            cube.localPosition = centre;
            cube.localRotation = Quaternion.identity;
            cube.localScale = size;
            r.Changed($"the Cube (the call's E and click): {was} → the phone's own box, {Mm(size)} at {Mm(centre)}");
        }
        MeshRenderer grey = cube.GetComponent<MeshRenderer>();
        if (grey != null && grey.enabled)
        {
            grey.enabled = false;
            r.Changed("the Cube's grey renderer: switched off (kept, not deleted: the Undo turns it back on)");
        }
        else r.Info("the Cube's grey renderer: already off");

        HoldCallJob job = root.GetComponent<HoldCallJob>();
        if (Mathf.Abs(job.restHeight - phoneRest) > 1e-5f)
        {
            r.Changed($"HoldCallJob.restHeight: {job.restHeight:0.0000} → {phoneRest:0.0000} m (the walk-in phone's: the same parts, the same lowest point, so it lies on the intake tray's felt as the phone does)");
            job.restHeight = phoneRest;
        }
        else r.Info($"HoldCallJob.restHeight: already {phoneRest:0.0000} m");

        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        if (!def.realSize)
        {
            def.realSize = true;
            r.Changed("DeviceDefinition.realSize: false → true (the one-scale phone's parts)");
        }
        else r.Info("DeviceDefinition.realSize: already true");
    }

    static void CopyPart(Transform model, string path, Transform source)
    {
        // The path's parents as nodes of their own (the glass sits in the Screen's frame), with their poses.
        string[] names = path.Split('/');
        Transform parent = model;
        Transform walk = source.root;
        for (int i = 0; i < names.Length; i++)
        {
            walk = walk.Find(names[i]);
            Transform node = parent.Find(names[i]);
            if (node == null)
            {
                node = new GameObject(names[i]).transform;
                node.SetParent(parent, false);
                node.gameObject.layer = walk.gameObject.layer;
                node.localPosition = walk.localPosition;
                node.localRotation = walk.localRotation;
                node.localScale = walk.localScale;
            }
            parent = node;
        }
        MeshFilter mf = source.GetComponent<MeshFilter>();
        MeshRenderer mr = source.GetComponent<MeshRenderer>();
        parent.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        MeshRenderer copy = parent.gameObject.AddComponent<MeshRenderer>();
        copy.sharedMaterials = mr.sharedMaterials;
        copy.shadowCastingMode = mr.shadowCastingMode;
        copy.receiveShadows = mr.receiveShadows;
    }

    // Every drawn node under the model, by its path inside it.
    static List<(string path, Transform t)> Copies(Transform model) =>
        model.GetComponentsInChildren<MeshRenderer>(true).Where(m => m.GetComponent<MeshFilter>() != null)
            .Select(m => (PathIn(model, m.transform), m.transform)).OrderBy(c => Array.IndexOf(Parts, c.Item1) < 0 ? 99 : Array.IndexOf(Parts, c.Item1)).ToList();

    // What a part looks like in its device: its path, mesh, materials and pose in the root's frame.
    static string Signature(string path, Transform t)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        Transform root = t;
        while (root.parent != null && root.name != ModelName) root = root.parent;
        Matrix4x4 m = (root.name == ModelName ? root.worldToLocalMatrix : t.root.worldToLocalMatrix) * t.localToWorldMatrix;
        string mats = mr != null ? string.Join(",", mr.sharedMaterials.Select(x => x != null ? x.name : "-")) : "-";
        string p = path == GlassFallback ? "Screen/Glass" : path;
        return $"{p} {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} {mats} {((Vector3)m.GetColumn(3)).ToString("F5")}";
    }

    // The detailed model's box in the root's frame (its meshes' bounds).
    static Bounds ModelBox(Transform root, Transform model)
    {
        bool any = false;
        Bounds b = default;
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return any ? b : new Bounds(Vector3.zero, CubeScaleWas);
    }

    static void TakeBack(Report r, GameObject root)
    {
        Transform model = root.transform.Find(ModelName);
        if (model != null)
        {
            Object.DestroyImmediate(model.gameObject);
            r.Changed($"\"{ModelName}\": removed");
        }
        else r.Info($"\"{ModelName}\": already gone");
        JobMarker[] markers = root.GetComponentsInChildren<JobMarker>(true);
        foreach (JobMarker m in markers)
        {
            Object.DestroyImmediate(m.gameObject);
            r.Changed("the copied colour tag: removed");
        }
        Transform cube = root.transform.Find(CubeName);
        if (cube != null)
        {
            if (!Near(cube.localPosition, Vector3.zero) || !Near(cube.localScale, CubeScaleWas))
            {
                cube.localPosition = Vector3.zero;
                cube.localRotation = Quaternion.identity;
                cube.localScale = CubeScaleWas;
                r.Changed($"the Cube: back to {Mm(CubeScaleWas)} at the root");
            }
            MeshRenderer grey = cube.GetComponent<MeshRenderer>();
            if (grey != null && !grey.enabled) { grey.enabled = true; r.Changed("the Cube's grey renderer: on again"); }
        }
        HoldCallJob job = root.GetComponent<HoldCallJob>();
        if (Mathf.Abs(job.restHeight - RestHeightWas) > 1e-5f) { r.Changed($"HoldCallJob.restHeight: {job.restHeight:0.0000} → {RestHeightWas:0.0000}"); job.restHeight = RestHeightWas; }
        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        if (def.realSize) { def.realSize = false; r.Changed("DeviceDefinition.realSize: true → false"); }
    }

    // ---------- the checks ----------

    static void CheckReal(Report r, GameObject root, GameObject phone, float phoneRest)
    {
        HoldCallJob job = root.GetComponent<HoldCallJob>();
        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        Transform cube = root.transform.Find(CubeName);
        Transform model = root.transform.Find(ModelName);
        r.Check(job != null && def != null && def.faults != null && def.faults.Length == 1 && def.faults[0] != null && def.faults[0].type == FaultType.Bureaucratic,
            "the call is as it was: a HoldCallJob with its one Bureaucratic fault (\"Service disconnected\"): the hold, the ring, the hand-back unchanged");
        PhoneInteractable press = cube != null ? cube.GetComponent<PhoneInteractable>() : null;
        BoxCollider hit = cube != null ? cube.GetComponent<BoxCollider>() : null;
        r.Check(press != null && hit != null && hit.enabled && !hit.isTrigger && cube.gameObject.activeSelf,
            "the Cube keeps its PhoneInteractable and its solid BoxCollider: E and click still \"Call support\", \"Answer support\", \"Pick up the phone\"");
        MeshRenderer grey = cube != null ? cube.GetComponent<MeshRenderer>() : null;
        r.Check(grey == null || !grey.enabled, "the grey block is no longer drawn (art a03)");
        if (!r.Check(model != null, $"\"{ModelName}\" exists")) return;
        MeshRenderer[] drawn = model.GetComponentsInChildren<MeshRenderer>(true);
        r.Check(drawn.Length == Parts.Length && drawn.All(x => x.enabled && x.gameObject.activeInHierarchy && x.GetComponent<MeshFilter>().sharedMesh != null),
            $"the detailed model draws the hero phone's {drawn.Length} parts ({string.Join(", ", drawn.Select(x => PathIn(model, x.transform)))}; {Parts.Length} wanted)");
        r.Check(model.GetComponentsInChildren<Collider>(true).Length == 0 && model.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
            "the detailed model has no collider and no script: nothing on it to hover, grab or grade");
        r.Check(HasRealModel(root), "HasRealModel says yes (the days' data puts the call on Day 3: Fixit Fidget › Days › G)");
        Bounds box = ModelBox(root.transform, model);
        r.Check(hit != null && Near(cube.localPosition, box.center) && Near(cube.localScale, box.size),
            $"the call's collider is the phone's own box ({Mm(box.size)})");

        // The fit: as the walk-in phone lies on the intake tray (BenchScale's own measures, the intake tray check's).
        float callBottom = BenchScale.Bottom(root), phoneBottom = BenchScale.Bottom(phone);
        float callOver = job.restHeight + callBottom, phoneOver = phoneRest + phoneBottom;
        r.Check(Mathf.Abs(job.restHeight - phoneRest) < 1e-5f && Mathf.Abs(callOver - phoneOver) < .0002f,
            $"it rests where the walk-in phone rests: its lowest point {1000f * callOver:+0.0;-0.0} mm over the slot point, the phone's {1000f * phoneOver:+0.0;-0.0} mm (0.2 mm allowed)");
        bool callFoot = Foot(root, out Bounds callB), phoneFoot = Foot(phone, out Bounds phoneB);
        r.Check(callFoot && phoneFoot && Mathf.Abs(callB.size.x - phoneB.size.x) < .002f && Mathf.Abs(callB.size.z - phoneB.size.z) < .002f,
            $"its footprint is the walk-in phone's ({(callFoot ? Mm(callB.size) : "-")} against {(phoneFoot ? Mm(phoneB.size) : "-")}; 2 mm allowed): it fits the intake tray as the phone does");
        FitsTheTray(r, callFoot ? callB : box);
        r.Check(def.realSize, "DeviceDefinition.realSize is on");
    }

    // With the café scene open: the phone's footprint inside slot 0's tray (its renderers' box), at the slot's rotation.
    static void FitsTheTray(Report r, Bounds foot)
    {
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            r.Info("the intake tray itself: not measured (open the café scene and run this again to measure it; the walk-in phone's fit above stands for it)");
            return;
        }
        IntakeShelf shelf = Object.FindAnyObjectByType<IntakeShelf>(FindObjectsInactive.Include);
        ItemSlotArea area = shelf != null ? shelf.GetComponent<ItemSlotArea>() : null;
        SerializedProperty slots = area != null ? new SerializedObject(area).FindProperty("slots") : null;
        Transform slot = slots != null && slots.arraySize > 0 ? slots.GetArrayElementAtIndex(0).objectReferenceValue as Transform : null;
        IntakeTray tray = slot != null ? slot.GetComponentInChildren<IntakeTray>(true) : null;
        Renderer[] trayRenderers = tray != null ? tray.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        if (slot == null || trayRenderers.Length == 0)
        {
            r.Info("the intake tray itself: no tray under the intake's slot 0 to measure (reported only)");
            return;
        }
        Bounds trayBox = trayRenderers[0].bounds;
        foreach (Renderer x in trayRenderers) trayBox.Encapsulate(x.bounds);
        // The phone's footprint at the slot, in the world (its corners turned by the slot).
        bool inside = true;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? foot.min.x : foot.max.x, 0f, i < 2 ? foot.min.z : foot.max.z);
            Vector3 world = slot.position + slot.rotation * corner;
            inside &= world.x >= trayBox.min.x && world.x <= trayBox.max.x && world.z >= trayBox.min.z && world.z <= trayBox.max.z;
        }
        r.Check(inside, $"on the intake's slot 0 its footprint ({Mm(foot.size)}) lies inside the tray ({Mm(trayBox.size)}, world): it fits the intake tray (a03)");
    }

    static void CheckGrey(Report r, GameObject root)
    {
        Transform cube = root.transform.Find(CubeName);
        MeshRenderer grey = cube != null ? cube.GetComponent<MeshRenderer>() : null;
        r.Check(root.transform.Find(ModelName) == null && grey != null && grey.enabled && Near(cube.localScale, CubeScaleWas),
            "the grey block is back as it was (no detailed model; the Cube drawn at 7 x 1.2 x 14 cm)");
        r.Check(!HasRealModel(root), "HasRealModel says no (Fixit Fidget › Days › G leaves the call off Day 3)");
    }

    // What's drawn of a device in its root's frame: its switched-on meshes (switched on up to the root: works on a prefab asset
    // as on loaded contents), the colour tag and any text left out (DeviceBounds' rule).
    static bool Foot(GameObject root, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.transform is RectTransform || mf.GetComponentInParent<JobMarker>(true) != null) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;
            bool on = true;
            for (Transform x = mf.transform; x != null; x = x.parent)
            {
                on &= x.gameObject.activeSelf;
                if (x == root.transform) break;
            }
            if (!on) continue;
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return any;
    }

    static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;

    static string Mm(Vector3 v) => $"{v.x * 1000f:0.0} x {v.y * 1000f:0.0} x {v.z * 1000f:0.0} mm";
}
#endif
