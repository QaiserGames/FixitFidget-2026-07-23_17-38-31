#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Sitting for the café's NPCs.
//
// SOURCE: the sit-down, seated idle, seated talk and stand-up clips of
// Quaternius' Universal Animation Library (CC0), Assets/ThirdParty/Quaternius_UAL.
// That library uses its own rig, so the clips are baked onto the café's NPC rig
// (Quaternius Beach, whose feet hang from the rig root, IK-style):
//
//  * Every mapped bone gets the same turn away from its rest pose (in the
//    character's space) as the library bone, after matching the two rest poses
//    (the library's A_TPose clip against the café rig's bind pose).
//  * The pelvis (café "Body", which carries both the spine and the legs) moves
//    like the library's hips: sideways and forwards scaled by the ratio of the
//    two rigs' hip heights, and downwards by whatever brings the café rig's hip
//    joints onto the café's own chairs (measured from the open scene's table
//    seats and the NPC prefab's scale). The library was made for a slightly
//    higher seat relative to its mannequin.
//  * The feet go where the library's feet go (same sideways scale), and each
//    leg is solved with two-bone IK to reach them, knee bending the way the
//    library's knee bends. So feet stay planted while the hips lower.
//
// Menu: Fixit Fidget > NPC > Sit 1 (bake), Sit 2 (wire animator, prefabs and
// seats), Sit 3 (photos of seated NPCs for review). Re-running is safe.
public static class NpcSitAnimations
{
    const string Menu = "Fixit Fidget/NPC/";
    // Seated upper arms point at least this far forward (0 = straight down).
    const float ElbowForward = .02f;
    const string Tag = "[NPC sit] ";
    public const string UalPath = "Assets/ThirdParty/Quaternius_UAL/AnimationLibrary_Unity_Standard.fbx";
    const string BeachPath = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    public const string Folder = "Assets/Art/NpcAnimations";
    public const string DataPath = Folder + "/NpcSitData.asset";
    const string ControllerPath = "Assets/CustomerAnimator(.controller";
    static readonly string[] Prefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };
    const float FrameRate = 30f;

    // Library clip -> baked clip asset name, looping.
    static readonly (string source, string file, bool loop)[] Clips =
    {
        ("Sitting_Enter", "Npc Sit Down", false),
        ("Sitting_Idle_Loop", "Npc Sitting Idle", true),
        ("Sitting_Talking_Loop", "Npc Sitting Talk", true),
        ("Sitting_Exit", "Npc Stand Up", false),
    };

    // Library bone -> café rig bone, parents before children. The café rig's
    // "Hips" (a short bone between Body and Abdomen) keeps its rest turn.
    static readonly (string ual, string beach)[] Map = BuildMap();

    static (string, string)[] BuildMap()
    {
        var map = new List<(string, string)>
        {
            ("DEF-hips", "Body"), ("DEF-spine.001", "Abdomen"), ("DEF-spine.002", "Torso"), ("DEF-spine.003", "Chest"),
            ("DEF-neck", "Neck"), ("DEF-head", "Head"),
        };
        foreach (string s in new[] { "L", "R" })
        {
            map.Add(("DEF-shoulder." + s, "Shoulder." + s));
            map.Add(("DEF-upper_arm." + s, "UpperArm." + s));
            map.Add(("DEF-forearm." + s, "LowerArm." + s));
            map.Add(("DEF-hand." + s, "Wrist." + s));
            foreach (var (from, to) in new[] { ("f_index", "Index"), ("f_middle", "Middle"), ("f_ring", "Ring"), ("f_pinky", "Pinky") })
                for (int i = 1; i <= 3; i++) map.Add(($"DEF-{from}.0{i}.{s}", $"{to}{i + 1}.{s}"));
            for (int i = 1; i <= 3; i++) map.Add(($"DEF-thumb.0{i}.{s}", $"Thumb{i}.{s}"));
        }
        foreach (string s in new[] { "L", "R" })
        {
            map.Add(("DEF-thigh." + s, "UpperLeg." + s));
            map.Add(("DEF-shin." + s, "LowerLeg." + s));
        }
        foreach (string s in new[] { "L", "R" }) map.Add(("DEF-foot." + s, "Foot." + s));
        return map.ToArray();
    }

    // Each limb bone's direction (towards this child) is matched between the two
    // rest poses, so a slightly different T-pose can't bend the result.
    static readonly Dictionary<string, string> UalTip = BuildTips(true), BeachTip = BuildTips(false);

    static Dictionary<string, string> BuildTips(bool ual)
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string s in new[] { "L", "R" })
        {
            if (ual)
            {
                tips["DEF-shoulder." + s] = "DEF-upper_arm." + s;
                tips["DEF-upper_arm." + s] = "DEF-forearm." + s;
                tips["DEF-forearm." + s] = "DEF-hand." + s;
                tips["DEF-hand." + s] = "DEF-f_middle.01." + s;
                tips["DEF-thigh." + s] = "DEF-shin." + s;
                tips["DEF-shin." + s] = "DEF-foot." + s;
                // No tip for the foot: the library's foot bone runs ankle -> ball
                // (sloping down) while the café rig's lies flat along the sole, so
                // matching their directions tipped the seated toes 27 degrees into
                // the floor. Both rest poses stand flat, so the foot takes the
                // library's turn from rest as it is.
                foreach (string f in new[] { "f_index", "f_middle", "f_ring", "f_pinky", "thumb" })
                {
                    tips[$"DEF-{f}.01.{s}"] = $"DEF-{f}.02.{s}";
                    tips[$"DEF-{f}.02.{s}"] = $"DEF-{f}.03.{s}";
                }
            }
            else
            {
                tips["Shoulder." + s] = "UpperArm." + s;
                tips["UpperArm." + s] = "LowerArm." + s;
                tips["LowerArm." + s] = "Wrist." + s;
                tips["Wrist." + s] = "Middle2." + s;
                tips["UpperLeg." + s] = "LowerLeg." + s;
                tips["LowerLeg." + s] = "Foot." + s;
                foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky" })
                {
                    tips[$"{f}2.{s}"] = $"{f}3.{s}";
                    tips[$"{f}3.{s}"] = $"{f}4.{s}";
                }
                tips["Thumb1." + s] = "Thumb2." + s;
                tips["Thumb2." + s] = "Thumb3." + s;
            }
        }
        return tips;
    }

    [MenuItem(Menu + "Sit 1 - Bake sit animations (library clips onto the cafe rig)")]
    static void BakeMenu() => Run("Bake", Bake);

    [MenuItem(Menu + "Sit 2 - Wire sitting (animator, NPC prefabs, table seats)")]
    static void WireMenu() => Run("Wire", Wire);

    [MenuItem(Menu + "Sit 3 - Photograph seated NPCs")]
    static void PhotoMenu() => Run("Photos", Photograph);

    [MenuItem(Menu + "Sit 9 - Apply seat placement and personal space to the NPC prefabs")]
    static void PlacementMenu() => Run("Placement", ApplyPlacement);

    // Writes NpcSeating.DefaultHipBehindSeatCentre (where the hips land along
    // the seat, measured with Sit 5) into both NPC prefabs, and gives each a
    // PersonalSpace so walking NPCs can't merge into one another. Nothing else
    // on the prefabs changes.
    public static string ApplyPlacement()
    {
        RequireStopped();
        var notes = new StringBuilder();
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var seating = root.GetComponent<NpcSeating>();
                Require(seating != null, path + " has no NpcSeating (run Sit 2 first).");
                notes.AppendLine(Path.GetFileName(path) + ": " + SetPlacement(seating)
                    + (EnsurePersonalSpace(root) ? ", PersonalSpace added" : ", PersonalSpace already there"));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "\n" + notes;
    }

    static bool EnsurePersonalSpace(GameObject root)
    {
        if (root.GetComponent<PersonalSpace>() != null) return false;
        root.AddComponent<PersonalSpace>();
        return true;
    }

    static string SetPlacement(NpcSeating seating)
    {
        var so = new SerializedObject(seating);
        var behind = so.FindProperty("hipBehindSeatCentre");
        float before = behind.floatValue;
        behind.floatValue = NpcSeating.DefaultHipBehindSeatCentre;
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"hips {before * 100f:+0;-0} -> {NpcSeating.DefaultHipBehindSeatCentre * 100f:+0;-0} cm behind the seat centre";
    }

    // ------------------------------------------------------------------ bake

    public static string Bake()
    {
        RequireStopped();
        PrepareLibraryImport();
        var ualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UalPath);
        var beachAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BeachPath);
        Require(ualAsset != null, "Missing " + UalPath);
        Require(beachAsset != null, "Missing " + BeachPath);
        var library = AssetDatabase.LoadAllAssetRepresentationsAtPath(UalPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToDictionary(c => c.name.Contains("|") ? c.name.Substring(c.name.IndexOf('|') + 1) : c.name, c => c);
        Require(library.ContainsKey("A_TPose"), "The library has no A_TPose clip.");
        foreach (var c in Clips) Require(library.ContainsKey(c.source), "The library has no " + c.source + " clip.");

        GameObject ual = Object.Instantiate(ualAsset);
        GameObject beach = Object.Instantiate(beachAsset);
        ual.hideFlags = beach.hideFlags = HideFlags.HideAndDontSave;
        var notes = new StringBuilder();
        try
        {
            foreach (var go in new[] { ual, beach })
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            }
            var U = Index(ual.transform);
            var B = Index(beach.transform);
            foreach (var (u, b) in Map)
            {
                Require(U.ContainsKey(u), "Library rig has no bone " + u);
                Require(B.ContainsKey(b), "Cafe rig has no bone " + b);
            }

            // Café rig rest pose (its T-pose), straight from the skinned meshes' bind poses.
            var bind = new Dictionary<string, (Vector3 p, Quaternion r)>(StringComparer.Ordinal);
            foreach (var skin in beach.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                Transform[] bones = skin.bones;
                Matrix4x4[] poses = skin.sharedMesh.bindposes;
                for (int i = 0; i < bones.Length && i < poses.Length; i++)
                {
                    if (bones[i] == null || bind.ContainsKey(bones[i].name)) continue;
                    Matrix4x4 m = beach.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix * poses[i].inverse;
                    bind[bones[i].name] = (m.GetColumn(3), m.rotation);
                }
            }
            foreach (var (_, b) in Map) Require(bind.ContainsKey(b), "Cafe rig bone " + b + " has no bind pose.");
            var rigBones = beach.GetComponentsInChildren<Transform>(true)
                .Where(t => t != beach.transform && t.IsChildOf(B["Root"].parent) && t != B["Root"].parent).ToArray();
            foreach (Transform t in rigBones)
                if (bind.TryGetValue(t.name, out var b))
                    t.SetPositionAndRotation(beach.transform.TransformPoint(b.p), beach.transform.rotation * b.r);
            var rest = rigBones.ToDictionary(t => t, t => (t.localPosition, t.localRotation));
            var restPos = rigBones.ToDictionary(t => t.name, t => beach.transform.InverseTransformPoint(t.position), StringComparer.Ordinal);
            var ankle = new Dictionary<string, Vector3>();
            foreach (string s in new[] { "L", "R" }) ankle[s] = B["LowerLeg." + s].InverseTransformPoint(B["Foot." + s].position);

            // Library rest pose.
            library["A_TPose"].SampleAnimation(ual, 0f);
            var uRest = U.ToDictionary(kv => kv.Key, kv => (p: P(ual, kv.Value), r: R(ual, kv.Value)), StringComparer.Ordinal);

            // Face both rigs the same way and scale by leg length.
            Quaternion frameU = Frame(uRest["DEF-upper_arm.L"].p, uRest["DEF-upper_arm.R"].p, uRest["DEF-hips"].p, uRest["DEF-head"].p);
            Quaternion frameB = Frame(bind["UpperArm.L"].p, bind["UpperArm.R"].p, bind["Body"].p, bind["Head"].p);
            Quaternion align = frameB * Quaternion.Inverse(frameU);
            // Hip-joint height, not leg length: the café rig's foot bone sits at the
            // sole while the library's sits at the ankle, so leg lengths disagree.
            float hipU = (uRest["DEF-thigh.L"].p.y + uRest["DEF-thigh.R"].p.y) * .5f;
            float hipB = (bind["UpperLeg.L"].p.y + bind["UpperLeg.R"].p.y) * .5f;
            float ratio = hipB / hipU;
            float drop = ratio; // vertical pelvis scale, fitted to the café chairs below
            notes.AppendLine($"Rig alignment {Quaternion.Angle(Quaternion.identity, align):0.0} deg, hip height library {hipU:0.000} / cafe {hipB:0.000} -> scale {ratio:0.000}");
            notes.AppendLine($"Cafe shin: to ankle {Vector3.Distance(bind["LowerLeg.L"].p, bind["Foot.L"].p):0.000}, to its end bone {Vector3.Distance(restPos["LowerLeg.L"], restPos.TryGetValue("LowerLeg.L_end", out var e) ? e : restPos["LowerLeg.L"]):0.000}");

            var restInverse = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
            var corrections = new List<string>();
            foreach (var (u, b) in Map)
            {
                Quaternion fix = Quaternion.identity;
                if (UalTip.TryGetValue(u, out string ut) && BeachTip.TryGetValue(b, out string bt)
                    && uRest.ContainsKey(ut) && restPos.ContainsKey(bt))
                {
                    Vector3 du = align * (uRest[ut].p - uRest[u].p);
                    Vector3 db = restPos[bt] - restPos[b];
                    if (du.sqrMagnitude > 1e-8f && db.sqrMagnitude > 1e-8f) fix = Quaternion.FromToRotation(du, db);
                    float angle = Quaternion.Angle(Quaternion.identity, fix);
                    if (angle > 3f) corrections.Add($"{b} {angle:0}");
                }
                restInverse[u] = Quaternion.Inverse(fix * align * uRest[u].r);
            }
            notes.AppendLine("Rest-pose corrections over 3 deg: " + (corrections.Count == 0 ? "none" : string.Join(", ", corrections)));

            float worstReach = 0f;
            void Pose(AnimationClip clip, float time)
            {
                clip.SampleAnimation(ual, time);
                foreach (var kv in rest) { kv.Key.localPosition = kv.Value.localPosition; kv.Key.localRotation = kv.Value.localRotation; }
                Vector3 moved = align * (P(ual, U["DEF-hips"]) - uRest["DEF-hips"].p);
                Vector3 body = bind["Body"].p + new Vector3(moved.x * ratio, moved.y * drop, moved.z * ratio);
                foreach (var (u, b) in Map)
                {
                    Transform bone = B[b];
                    bone.rotation = beach.transform.rotation * (align * R(ual, U[u]) * restInverse[u] * bind[b].r);
                    if (b == "Body") bone.position = beach.transform.TransformPoint(body);
                }
                foreach (string s in new[] { "L", "R" })
                {
                    Vector3 target = bind["Foot." + s].p + align * (P(ual, U["DEF-foot." + s]) - uRest["DEF-foot." + s].p) * ratio;
                    Vector3 world = beach.transform.TransformPoint(target);
                    SolveLeg(B["UpperLeg." + s], B["LowerLeg." + s], ankle[s], world, beach.transform);
                    Vector3 reached = B["LowerLeg." + s].TransformPoint(ankle[s]);
                    worstReach = Mathf.Max(worstReach, Vector3.Distance(reached, world));
                    B["Foot." + s].position = reached;
                }
            }

            // Fit the sitting depth to the café's chairs: the hip joints end a
            // thigh's thickness above the seat. Linear in the vertical scale, so
            // one measurement pass gives the exact factor.
            MeasureChairs(out float seatHeight, out float npcScale, notes);
            float seatedTarget = (seatHeight + NpcSeating.DefaultHipAboveSeat * npcScale) / npcScale;
            Pose(library["Sitting_Enter"], 0f);
            float standing = Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]).y;
            Pose(library["Sitting_Idle_Loop"], 0f);
            float seatedFirst = Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]).y;
            // Seated hip height = constant + pelvis drop x vertical scale, so one step lands exactly.
            float pelvisDrop = (align * (P(ual, U["DEF-hips"]) - uRest["DEF-hips"].p)).y;
            if (Mathf.Abs(pelvisDrop) > 1e-3f) drop += (seatedTarget - seatedFirst) / pelvisDrop;
            Pose(library["Sitting_Idle_Loop"], 0f);
            notes.AppendLine($"Chairs {seatHeight:0.000} m, NPC scale {npcScale:0.00}: seated hip joints {seatedFirst:0.000} -> {Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]).y:0.000} (target {seatedTarget:0.000}) model units, vertical scale {drop:0.000}");

            // UAL's seated hands rest on the thighs with the elbows a little behind
            // the back (upper arms about 17 degrees back). On the slim café rig that
            // is harmless; the city bodies' thicker sleeves then pass through the
            // chair's backrest. While the NPC is down (or going down), swing each
            // hanging upper arm forward to at most straight down, keeping the
            // forearm's direction, so the hands slide forward along the thighs
            // instead of the elbows going into the chair. Standing frames are
            // untouched, so the clips still meet the walk and idle clips exactly.
            float seatedHips = seatedTarget;
            int armFixes = 0;
            void ArmsForward()
            {
                float hipY = Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]).y;
                float weight = Mathf.Clamp01(Mathf.InverseLerp(standing, seatedHips, hipY));
                if (weight <= 0f) return;
                foreach (string s in new[] { "L", "R" })
                {
                    Transform upper = B["UpperArm." + s], lower = B["LowerArm." + s];
                    Vector3 dir = beach.transform.InverseTransformDirection((lower.position - upper.position).normalized);
                    if (dir.z >= ElbowForward || dir.y > -.3f) continue;       // already forward, or a gesture
                    var wanted = new Vector3(dir.x, 0f, ElbowForward);
                    wanted.y = -Mathf.Sqrt(Mathf.Max(0f, 1f - wanted.x * wanted.x - wanted.z * wanted.z));
                    Quaternion swing = Quaternion.FromToRotation(dir, Vector3.Slerp(dir, wanted.normalized, weight));
                    Quaternion forearm = lower.rotation;
                    upper.rotation = beach.transform.rotation * swing * Quaternion.Inverse(beach.transform.rotation) * upper.rotation;
                    lower.rotation = forearm;
                    armFixes++;
                }
            }

            EnsureFolder(Folder);
            var baked = new Dictionary<string, AnimationClip>();
            foreach (var c in Clips)
            {
                AnimationClip source = library[c.source];
                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * FrameRate) + 1);
                var paths = rigBones.Where(t => !t.name.EndsWith("_end", StringComparison.Ordinal))
                    .Select(t => (t, path: AnimationUtility.CalculateTransformPath(t, beach.transform))).ToArray();
                var curves = paths.Select(_ => new List<Keyframe>[7].Select(__ => new List<Keyframe>()).ToArray()).ToArray();
                var previous = new Quaternion[paths.Length];
                for (int f = 0; f < frames; f++)
                {
                    float time = Mathf.Min(source.length, f / FrameRate);
                    Pose(source, time);
                    ArmsForward();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        Transform t = paths[i].t;
                        Vector3 p = t.localPosition;
                        Quaternion q = t.localRotation;
                        if (f > 0 && Quaternion.Dot(previous[i], q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        previous[i] = q;
                        float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                        for (int k = 0; k < 7; k++) curves[i][k].Add(new Keyframe(time, values[k]));
                    }
                }
                var clip = new AnimationClip { name = c.file, frameRate = FrameRate };
                string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                var bindings = new List<EditorCurveBinding>();
                var animationCurves = new List<AnimationCurve>();
                for (int i = 0; i < paths.Length; i++)
                    for (int k = 0; k < 7; k++)
                    {
                        bindings.Add(EditorCurveBinding.FloatCurve(paths[i].path, typeof(Transform), properties[k]));
                        animationCurves.Add(Smooth(curves[i][k]));
                    }
                AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), animationCurves.ToArray());
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = c.loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                baked[c.source] = SaveClip(clip, Folder + "/" + c.file + ".anim");
                notes.AppendLine($"{c.file}: {source.length:0.00} s, {frames} frames, {paths.Length} bones");
            }
            notes.AppendLine($"Largest foot IK miss: {worstReach * 1000f:0.0} mm");
            notes.AppendLine($"Upper arms swung forward in {armFixes} arm-frames (elbows no further back than straight down while seated).");

            // Measurements for NpcSeating, in the café rig's model space.
            Pose(library["Sitting_Idle_Loop"], 0f);
            Vector3 hip = Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]);
            Vector3 feet = Mid(beach, B["Foot.L"], B["Foot.R"]);
            Vector3 knee = Mid(beach, B["LowerLeg.L"], B["LowerLeg.R"]);
            Pose(library["Sitting_Enter"], 0f);
            float standingHip = Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]).y;
            var data = AssetDatabase.LoadAssetAtPath<NpcSitData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<NpcSitData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.enterLength = library["Sitting_Enter"].length;
            data.exitLength = library["Sitting_Exit"].length;
            data.seatedHip = hip;
            data.seatedFeet = feet;
            data.standingHipHeight = standingHip;
            data.retargetScale = ratio;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            notes.AppendLine($"Seated (model units, NPC scale not applied): hips {V(hip)}, knees {V(knee)}, feet {V(feet)}; standing hips {standingHip:0.000}");
        }
        finally
        {
            Object.DestroyImmediate(ual);
            Object.DestroyImmediate(beach);
        }
        string report = notes.ToString();
        WriteLog("bake.txt", report);
        return "\n" + report;
    }

    // Seat height from the open scene's table seats (seat surface above the
    // chair's floor), NPC scale from the customer prefab.
    static void MeasureChairs(out float seatHeight, out float npcScale, StringBuilder notes)
    {
        var heights = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include)
            .Where(s => s.SeatPose != s.StandPoint).Select(s => s.SeatPose.position.y - s.transform.position.y)
            .Where(h => h > .2f && h < .9f).OrderBy(h => h).ToArray();
        seatHeight = heights.Length > 0 ? heights[heights.Length / 2] : .45f;
        if (heights.Length == 0) notes.AppendLine("No table seats in the open scene: assuming 0.45 m chairs.");
        var customer = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[0]);
        npcScale = customer != null ? customer.transform.localScale.y : 1.1f;
        if (npcScale < .1f) npcScale = 1f;
    }

    // The library ships Z-up with many takes; import it as a plain generic rig.
    static void PrepareLibraryImport()
    {
        var importer = AssetImporter.GetAtPath(UalPath) as ModelImporter;
        Require(importer != null, "Missing " + UalPath);
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
        if (importer.animationCompression != ModelImporterAnimationCompression.Off) { importer.animationCompression = ModelImporterAnimationCompression.Off; changed = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    // Two-bone IK: bend the leg so its ankle reaches the target, keeping the
    // knee on the side it already bends towards.
    static void SolveLeg(Transform upper, Transform lower, Vector3 ankleLocal, Vector3 target, Transform root)
    {
        Vector3 a = upper.position, b = lower.position, c = lower.TransformPoint(ankleLocal);
        float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
        if (l1 < 1e-5f || l2 < 1e-5f) return;
        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        Vector3 n = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : (c - a).normalized;
        Vector3 pole = (b - a) - Vector3.Dot(b - a, n) * n;
        if (pole.sqrMagnitude < 1e-10f) pole = root.forward - Vector3.Dot(root.forward, n) * n;
        pole.Normalize();
        float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 knee = a + n * x + pole * h;
        upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
        Vector3 ankleNow = lower.TransformPoint(ankleLocal);
        Vector3 end = a + n * d;
        lower.rotation = Quaternion.FromToRotation(ankleNow - lower.position, end - lower.position) * lower.rotation;
    }

    static AnimationCurve Smooth(List<Keyframe> keys)
    {
        var k = keys.ToArray();
        for (int i = 0; i < k.Length; i++)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(k.Length - 1, i + 1);
            float dt = k[b].time - k[a].time;
            float slope = dt > 1e-6f ? (k[b].value - k[a].value) / dt : 0f;
            k[i].inTangent = k[i].outTangent = slope;
        }
        return new AnimationCurve(k);
    }

    static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        // Keep the asset (and its GUID in the animator) and replace its contents.
        EditorUtility.CopySerialized(clip, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(clip);
        return existing;
    }

    static Quaternion Frame(Vector3 leftArm, Vector3 rightArm, Vector3 pelvis, Vector3 head)
    {
        Vector3 right = (rightArm - leftArm).normalized;
        Vector3 up = head - pelvis;
        up = (up - Vector3.Dot(up, right) * right).normalized;
        return Quaternion.LookRotation(Vector3.Cross(right, up), up);
    }

    static Dictionary<string, Transform> Index(Transform root)
    {
        var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;
        return map;
    }

    static Vector3 P(GameObject root, Transform t) => root.transform.InverseTransformPoint(t.position);
    static Quaternion R(GameObject root, Transform t) => Quaternion.Inverse(root.transform.rotation) * t.rotation;
    static Vector3 Mid(GameObject root, Transform a, Transform b) => (P(root, a) + P(root, b)) * .5f;

    // ------------------------------------------------------------------ wire

    public static string Wire()
    {
        RequireStopped();
        var data = AssetDatabase.LoadAssetAtPath<NpcSitData>(DataPath);
        Require(data != null, "Run Sit 1 first: " + DataPath + " is missing.");
        var clip = Clips.ToDictionary(c => c.source, c => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + c.file + ".anim"));
        foreach (var c in clip) Require(c.Value != null, "Run Sit 1 first: baked clip for " + c.Key + " is missing.");
        var notes = new StringBuilder();

        // Animator: Seated / Talking parameters and four states.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null, "Missing " + ControllerPath);
        EnsureParameter(controller, "Seated");
        EnsureParameter(controller, "Talking");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = Find(machine, "CharacterArmature|Idle"), walk = Find(machine, "CharacterArmature|Walk");
        Require(idle != null && walk != null, "The customer animator has no Idle / Walk state.");
        AnimatorState sitDown = EnsureState(machine, "Sit Down", clip["Sitting_Enter"], new Vector3(260f, 440f));
        AnimatorState sitting = EnsureState(machine, "Sitting", clip["Sitting_Idle_Loop"], new Vector3(520f, 440f));
        AnimatorState talk = EnsureState(machine, "Sitting Talk", clip["Sitting_Talking_Loop"], new Vector3(780f, 440f));
        AnimatorState standUp = EnsureState(machine, "Stand Up", clip["Sitting_Exit"], new Vector3(520f, 560f));
        var ours = new HashSet<AnimatorState> { sitDown, sitting, talk, standUp };
        foreach (var state in new[] { idle, walk })
            foreach (var t in state.transitions.Where(t => ours.Contains(t.destinationState)).ToArray()) state.RemoveTransition(t);
        foreach (var state in ours)
            foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
        // Sitting wins over walking/idling transitions in the same frame.
        Prepend(idle, Transition(idle, sitDown, .2f, ("Seated", true)));
        Prepend(walk, Transition(walk, sitDown, .2f, ("Seated", true)));
        var toSitting = Transition(sitDown, sitting, .15f);
        toSitting.hasExitTime = true; toSitting.exitTime = .97f;
        Transition(sitDown, standUp, .2f, ("Seated", false));
        Transition(sitting, talk, .45f, ("Talking", true));
        Transition(talk, sitting, .45f, ("Talking", false));
        Transition(sitting, standUp, .2f, ("Seated", false));
        Transition(talk, standUp, .2f, ("Seated", false));
        var toIdle = Transition(standUp, idle, .25f);
        toIdle.hasExitTime = true; toIdle.exitTime = .95f;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        notes.AppendLine("Animator: Seated/Talking parameters; Sit Down -> Sitting <-> Sitting Talk -> Stand Up -> Idle.");

        // NPC prefabs get NpcSeating.
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var seating = root.GetComponent<NpcSeating>();
                bool added = seating == null;
                if (added) seating = root.AddComponent<NpcSeating>();
                seating.Data = data;
                SetPlacement(seating);
                EnsurePersonalSpace(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.AppendLine(Path.GetFileName(path) + (added ? ": NpcSeating added" : ": NpcSeating updated"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Every table seat in the open scene becomes a real chair.
        int seats = 0;
        foreach (var seat in Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(seat);
            var flag = so.FindProperty("snapToSeat");
            if (flag == null || flag.boolValue) { seats++; continue; }
            Undo.RecordObject(seat, "Seats sit");
            flag.boolValue = true;
            so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(seat);
            seats++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        notes.AppendLine(seats + " table seats have Snap To Seat on (save the scene to keep it).");
        WriteLog("wire.txt", notes.ToString());
        return "\n" + notes;
    }

    static void EnsureParameter(AnimatorController controller, string name)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
    }

    static AnimatorState Find(AnimatorStateMachine machine, string name) =>
        machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    static AnimatorState EnsureState(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = Find(machine, name) ?? machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration, params (string parameter, bool value)[] conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.canTransitionToSelf = false;
        foreach (var c in conditions) t.AddCondition(c.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, c.parameter);
        return t;
    }

    static void Prepend(AnimatorState state, AnimatorStateTransition transition)
    {
        var list = state.transitions.Where(t => t != transition).ToList();
        list.Insert(0, transition);
        state.transitions = list.ToArray();
    }

    // ------------------------------------------------------------------ photos

    // Seats real NPC prefabs at one table in the open scene, poses them with the
    // baked clips, and photographs them (original body and a POLYGON body).
    public static string Photograph()
    {
        RequireStopped();
        var data = AssetDatabase.LoadAssetAtPath<NpcSitData>(DataPath);
        Require(data != null, "Run Sit 1 and Sit 2 first.");
        var clip = Clips.ToDictionary(c => c.source, c => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + c.file + ".anim"));
        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(s => s.transform.parent != null).GroupBy(s => s.transform.parent)
            .OrderByDescending(g => g.Count()).First().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        Require(seats.Length >= 3, "No table with three seats in the open scene.");
        string folder = Path.Combine(LogRoot, "photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var actors = new List<GameObject>();
        var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
        var notes = new StringBuilder();
        try
        {
            var poses = new (string prefab, int look, string source, float at)[]
            {
                ("Assets/AssetsPrefabs/Customer.prefab", -1, "Sitting_Idle_Loop", 0f),
                ("Assets/AssetsPrefabs/Patron.prefab", 0, "Sitting_Talking_Loop", .35f),
                ("Assets/AssetsPrefabs/Patron.prefab", 3, "Sitting_Enter", .55f),
            };
            for (int i = 0; i < poses.Length && i < seats.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(poses[i].prefab);
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                actors.Add(actor);
                var seating = actor.GetComponent<NpcSeating>();
                Require(seating != null, poses[i].prefab + " has no NpcSeating (run Sit 2).");
                float floor = seats[i].StandPoint.position.y;
                seating.Placement(seats[i], floor, out Vector3 feet, out Quaternion facing);
                actor.transform.SetPositionAndRotation(feet, facing);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null && poses[i].look >= 0 && visual.AppearanceCount > 0)
                    visual.ApplyAppearance(poses[i].look % visual.AppearanceCount);
                else if (visual != null) visual.RemoveAppearance();
                if (visual != null) visual.Seated = true;
                AnimationClip c = clip[poses[i].source];
                Sample(actor, c, poses[i].at * c.length);
                if (visual != null) visual.Follow();
                baked.AddRange(PolygonNpcSetup.BakePose(actor));

                // How high are the seated hips and the lowest foot point, against the seat and floor?
                var bones = Index(actor.transform);
                Vector3 hips = (bones["UpperLeg.L"].position + bones["UpperLeg.R"].position) * .5f;
                string bodyHips = "";
                if (visual != null && visual.VisualInstance != null)
                {
                    var city = Index(visual.VisualInstance.transform);
                    if (city.TryGetValue("UpperLeg_L", out Transform l) && city.TryGetValue("UpperLeg_R", out Transform r))
                        bodyHips = $", city body hip joints {(l.position.y + r.position.y) * .5f - seats[i].SeatPose.position.y:+0.00;-0.00} m above seat";
                }
                float lowest = float.MaxValue;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                    var mesh = PolygonNpcSetup.SkinToWorld(skin);
                    foreach (var v in mesh.vertices) lowest = Mathf.Min(lowest, v.y);
                    Object.DestroyImmediate(mesh);
                }
                notes.AppendLine($"{seats[i].name}: {Path.GetFileNameWithoutExtension(poses[i].prefab)} {(visual != null && visual.VisualInstance != null ? visual.ActiveAppearanceName : "original body")}, " +
                    $"{poses[i].source} @{poses[i].at:0.00}: feet {V(feet)}, rig hip joints {hips.y - seats[i].SeatPose.position.y:+0.00;-0.00} m above seat{bodyHips}, lowest mesh point {lowest - seats[i].transform.position.y:+0.00;-0.00} m from the chair's floor");
            }
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();

            Vector3 table = seats.Aggregate(Vector3.zero, (sum, s) => sum + s.SeatPose.position) / seats.Length;
            Vector3 a = seats[0].SeatPose.position, b = seats[1].SeatPose.position;
            Vector3 across = Vector3.Cross(Vector3.up, (a - table).normalized);
            var views = new (string name, Vector3 position, Vector3 target, float fov)[]
            {
                ("1-side", a + across * 2.6f + Vector3.up * .9f, a + Vector3.up * .55f, 45f),
                ("2-three-quarter", table + (a - table).normalized * 3.4f + across * 1.8f + Vector3.up * 1.9f, table + Vector3.up * .6f, 50f),
                ("3-second-seat", b + Vector3.Cross(Vector3.up, (b - table).normalized) * 2.4f + Vector3.up * 1f, b + Vector3.up * .6f, 48f),
                ("4-overhead", table + new Vector3(-7f, 9f, -7f), table + Vector3.up * .5f, 32f),
                // Behind the chair, where arms and shoulders meet the backrest.
                ("5-behind", a + (a - table).normalized * 1.7f + Vector3.up * 1.35f, a + Vector3.up * .8f, 45f),
                ("6-behind-close", a + (a - table).normalized * 1.05f + across * .45f + Vector3.up * 1.2f, a + Vector3.up * .82f, 50f),
                ("7-second-behind", b + (b - table).normalized * 1.3f - Vector3.Cross(Vector3.up, (b - table).normalized) * .5f + Vector3.up * 1.25f, b + Vector3.up * .8f, 50f),
            };
            foreach (var v in views)
                CafeSecondPassSteps.Capture(Path.Combine(folder, v.name + ".png"), v.position, v.target, v.fov, true);
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            PolygonNpcSetup.UnbakePose(baked);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
        }
        WriteLog("photos.txt", notes.ToString());
        return "Photos: " + folder + "\n" + notes;
    }

    static void Sample(GameObject actor, AnimationClip clip, float time)
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(actor, clip, time);
        AnimationMode.EndSampling();
    }

    // ------------------------------------------------------------------ helpers

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcSit"));

    static void WriteLog(string file, string text)
    {
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(Path.Combine(LogRoot, file), text);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);

    static void RequireStopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
