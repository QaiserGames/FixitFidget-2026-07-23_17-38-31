#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Walk and idle variety for the café's NPCs (pass 2).
//
// SOURCE: Quaternius' Universal Animation Library (CC0), already in the project
// at Assets/ThirdParty/Quaternius_UAL (downloaded 24 Sept 2026 for the sit
// clips; see its License.txt). Nothing new was downloaded. Its Walk_Loop,
// Walk_Formal_Loop, Idle_Loop and Idle_Talking_Loop are baked onto the café's
// NPC rig (Quaternius Beach) the same way NpcSitAnimations bakes the sit clips:
// every mapped bone takes the library bone's turn from its rest pose after the
// two rest poses are matched, the pelvis travels like the library's hips scaled
// by the hip-height ratio, and the feet are placed where the library's feet go
// with two-bone leg IK. Loops stay in place (any drift in the library's hips
// over the loop is removed).
//
// Each walk's natural ground speed is then measured (median stance-foot speed,
// as Pass 1 measured the Beach walk) and written with the clips to
// Assets/Art/NpcAnimations/NpcGaitData.asset, which NpcLocomotion reads so the
// stride still matches the floor whichever walk a person has.
//
// COLLARBONES STAY SQUARE (pass 2b, 27 Sept 2026). The library's clips turn
// the collarbones down and back as the arms come in from its A-pose. Copied
// onto the city bodies' longer collarbones (PolygonNpcVisual) that swung each
// shoulder joint down into the torso: people with the relaxed or brisk walk
// (and the relaxed idle and standing talk) looked hunched, their shoulders
// "placed weirdly". PolygonNpcVisual already squares the shoulders for the sit
// clips, but only while seated. So these standing clips keep the café rig's
// own collarbones (their T-pose turn, following the chest), as the Beach walk
// does; the arms still swing exactly as the library's do (every other bone is
// retargeted in world space, so the upper arm does not depend on its parent).
// The bake log says how far the library had turned them.
//
// Menu: Fixit Fidget > NPC > Gait 1 (bake), Gait 2 (wire the animator: WalkStyle
// / IdleStyle blend trees and a Standing Talk state), Gait 3 (photos).
public static class NpcGaitAnimations
{
    const string Menu = "Fixit Fidget/NPC/";
    const string Tag = "[NPC gait] ";
    const string UalPath = "Assets/ThirdParty/Quaternius_UAL/AnimationLibrary_Unity_Standard.fbx";
    const string BeachPath = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    public const string Folder = "Assets/Art/NpcAnimations";
    public const string DataPath = Folder + "/NpcGaitData.asset";
    const string ControllerPath = "Assets/CustomerAnimator(.controller";
    const float FrameRate = 30f;

    // Library clip -> baked clip asset name.
    static readonly (string source, string file)[] Clips =
    {
        ("Walk_Loop", "Npc Walk Relaxed"),
        ("Walk_Formal_Loop", "Npc Walk Brisk"),
        ("Idle_Loop", "Npc Idle Relaxed"),
        ("Idle_Talking_Loop", "Npc Standing Talk"),
    };

    static readonly (string ual, string beach)[] Map = BuildMap();
    static readonly Dictionary<string, string> UalTip = BuildTips(true), BeachTip = BuildTips(false);

    [MenuItem(Menu + "Gait 1 - Bake walk and idle variations (library clips onto the cafe rig)")]
    static void BakeMenu() => Run("Bake", Bake);

    [MenuItem(Menu + "Gait 2 - Wire the walk and idle styles and Standing Talk into the animator")]
    static void WireMenu() => Run("Wire", Wire);

    [MenuItem(Menu + "Gait 3 - Photograph the walk and idle variations")]
    static void PhotoMenu() => Run("Photos", Photograph);

    // ------------------------------------------------------------------ bake

    public static string Bake()
    {
        RequireStopped();
        var ualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UalPath);
        var beachAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BeachPath);
        Require(ualAsset != null, "Missing " + UalPath);
        Require(beachAsset != null, "Missing " + BeachPath);
        var library = AssetDatabase.LoadAllAssetRepresentationsAtPath(UalPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToDictionary(c => c.name.Contains("|") ? c.name.Substring(c.name.IndexOf('|') + 1) : c.name, c => c);
        Require(library.ContainsKey("A_TPose"), "The library has no A_TPose clip.");
        foreach (var c in Clips) Require(library.ContainsKey(c.source), "The library has no " + c.source + " clip.");
        AnimationClip beachWalk = null, beachIdle = null;
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(BeachPath))
        {
            if (o is AnimationClip clip && clip.name.EndsWith("|Walk", StringComparison.Ordinal)) beachWalk = clip;
            if (o is AnimationClip idle && idle.name.EndsWith("|Idle", StringComparison.Ordinal)) beachIdle = idle;
        }
        Require(beachWalk != null && beachIdle != null, "Beach.fbx has no Walk / Idle clip.");

        GameObject ual = Object.Instantiate(ualAsset);
        GameObject beach = Object.Instantiate(beachAsset);
        ual.hideFlags = beach.hideFlags = HideFlags.HideAndDontSave;
        var notes = new StringBuilder();
        var baked = new Dictionary<string, AnimationClip>();
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

            // Café rig rest pose from the skinned meshes' bind poses.
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

            library["A_TPose"].SampleAnimation(ual, 0f);
            var uRest = U.ToDictionary(kv => kv.Key, kv => (p: P(ual, kv.Value), r: R(ual, kv.Value)), StringComparer.Ordinal);
            Quaternion frameU = Frame(uRest["DEF-upper_arm.L"].p, uRest["DEF-upper_arm.R"].p, uRest["DEF-hips"].p, uRest["DEF-head"].p);
            Quaternion frameB = Frame(bind["UpperArm.L"].p, bind["UpperArm.R"].p, bind["Body"].p, bind["Head"].p);
            Quaternion align = frameB * Quaternion.Inverse(frameU);
            float hipU = (uRest["DEF-thigh.L"].p.y + uRest["DEF-thigh.R"].p.y) * .5f;
            float hipB = (bind["UpperLeg.L"].p.y + bind["UpperLeg.R"].p.y) * .5f;
            float ratio = hipB / hipU;
            notes.AppendLine($"Rig alignment {Quaternion.Angle(Quaternion.identity, align):0.0} deg, hip height library {hipU:0.000} / cafe {hipB:0.000} -> scale {ratio:0.000}");

            var restInverse = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
            foreach (var (u, b) in Map)
            {
                Quaternion fix = Quaternion.identity;
                if (UalTip.TryGetValue(u, out string ut) && BeachTip.TryGetValue(b, out string bt)
                    && uRest.ContainsKey(ut) && restPos.ContainsKey(bt))
                {
                    Vector3 du = align * (uRest[ut].p - uRest[u].p);
                    Vector3 db = restPos[bt] - restPos[b];
                    if (du.sqrMagnitude > 1e-8f && db.sqrMagnitude > 1e-8f) fix = Quaternion.FromToRotation(du, db);
                }
                restInverse[u] = Quaternion.Inverse(fix * align * uRest[u].r);
            }

            float worstReach = 0f;
            float collarboneTurn = 0f;
            int collarboneSamples = 0;
            // Hips drift over an in-place loop (if any) is removed: the clip must end where it began.
            Vector3 driftPerSecond = Vector3.zero;
            void Pose(AnimationClip clip, float time)
            {
                clip.SampleAnimation(ual, time);
                foreach (var kv in rest) { kv.Key.localPosition = kv.Value.localPosition; kv.Key.localRotation = kv.Value.localRotation; }
                Vector3 moved = align * (P(ual, U["DEF-hips"]) - uRest["DEF-hips"].p) - driftPerSecond * time;
                Vector3 body = bind["Body"].p + moved * ratio;
                foreach (var (u, b) in Map)
                {
                    Transform bone = B[b];
                    Quaternion retargeted = beach.transform.rotation * (align * R(ual, U[u]) * restInverse[u] * bind[b].r);
                    if (b == "Shoulder.L" || b == "Shoulder.R")
                    {
                        // Kept at the rig's own turn (reset above, now following the chest).
                        collarboneTurn += Quaternion.Angle(retargeted, bone.rotation);
                        collarboneSamples++;
                        continue;
                    }
                    bone.rotation = retargeted;
                    if (b == "Body") bone.position = beach.transform.TransformPoint(body);
                }
                foreach (string s in new[] { "L", "R" })
                {
                    Vector3 footMoved = align * (P(ual, U["DEF-foot." + s]) - uRest["DEF-foot." + s].p) - driftPerSecond * time;
                    Vector3 target = bind["Foot." + s].p + footMoved * ratio;
                    Vector3 world = beach.transform.TransformPoint(target);
                    SolveLeg(B["UpperLeg." + s], B["LowerLeg." + s], ankle[s], world, beach.transform);
                    Vector3 reached = B["LowerLeg." + s].TransformPoint(ankle[s]);
                    worstReach = Mathf.Max(worstReach, Vector3.Distance(reached, world));
                    B["Foot." + s].position = reached;
                }
            }

            EnsureFolder(Folder);
            string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            var paths = rigBones.Where(t => !t.name.EndsWith("_end", StringComparison.Ordinal))
                .Select(t => (t, path: AnimationUtility.CalculateTransformPath(t, beach.transform))).ToArray();
            foreach (var c in Clips)
            {
                AnimationClip source = library[c.source];
                // Drift of the library's hips over the whole loop, horizontal only.
                driftPerSecond = Vector3.zero;
                source.SampleAnimation(ual, 0f);
                Vector3 h0 = align * P(ual, U["DEF-hips"]);
                source.SampleAnimation(ual, source.length);
                Vector3 h1 = align * P(ual, U["DEF-hips"]);
                Vector3 drift = h1 - h0; drift.y = 0f;
                if (drift.magnitude > .02f && source.length > .1f) driftPerSecond = drift / source.length;

                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * FrameRate) + 1);
                var curves = paths.Select(_ => new List<Keyframe>[7].Select(__ => new List<Keyframe>()).ToArray()).ToArray();
                var previous = new Quaternion[paths.Length];
                for (int f = 0; f < frames; f++)
                {
                    float time = Mathf.Min(source.length, f / FrameRate);
                    Pose(source, time);
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
                settings.loopTime = true;
                settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                baked[c.source] = SaveClip(clip, Folder + "/" + c.file + ".anim");
                notes.AppendLine($"{c.file}: {source.length:0.00} s, {frames} frames, {paths.Length} bones" +
                                 (driftPerSecond.sqrMagnitude > 0f ? $", hips drift {drift.magnitude * 100f:0.0} cm/loop removed" : ""));
            }
            notes.AppendLine($"Largest foot IK miss: {worstReach * 1000f:0.0} mm");
            notes.AppendLine($"Collarbones kept at the cafe rig's own turn; the library's were turned " +
                             $"{(collarboneSamples > 0 ? collarboneTurn / collarboneSamples : 0f):0.0} deg from it on average");
        }
        finally
        {
            Object.DestroyImmediate(ual);
            Object.DestroyImmediate(beach);
        }

        // Natural speeds, measured on the rig at scale 1.
        var data = AssetDatabase.LoadAssetAtPath<NpcGaitData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<NpcGaitData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        var walks = new List<NpcGaitData.Walk>();
        foreach (var (name, clip) in new[] { ("Normal (Beach walk)", beachWalk), ("Relaxed (UAL Walk_Loop)", baked["Walk_Loop"]), ("Brisk (UAL Walk_Formal_Loop)", baked["Walk_Formal_Loop"]) })
        {
            float speed = MeasureStanceSpeed(clip, beachAsset, out string how);
            walks.Add(new NpcGaitData.Walk { name = name, clip = clip, clipSpeed = speed > .2f ? speed : 1.35f });
            notes.AppendLine($"{name}: {how}");
        }
        data.walks = walks.ToArray();
        data.idles = new[] { beachIdle, baked["Idle_Loop"] };
        data.standingTalk = baked["Idle_Talking_Loop"];
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        string report = notes.ToString();
        WriteLog("gait-bake.txt", report);
        return "\n" + report;
    }

    // The walk clip is in place, so while a foot is on the ground it slides
    // backwards at the speed the animation was made for: median backward foot
    // speed while the foot is low (in stance). Same method as Pass 1 step 3.
    static float MeasureStanceSpeed(AnimationClip walk, GameObject rigAsset, out string how)
    {
        GameObject actor = Object.Instantiate(rigAsset);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            foreach (var a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var speeds = new List<float>();
            foreach (string footName in new[] { "Foot.L", "Foot.R" })
            {
                Transform foot = null;
                foreach (var t in actor.GetComponentsInChildren<Transform>(true)) if (t.name == footName) foot = t;
                if (foot == null) continue;
                int n = Mathf.Max(24, Mathf.RoundToInt(walk.length * 60f));
                var z = new float[n + 1]; var y = new float[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    walk.SampleAnimation(actor, walk.length * i / n);
                    Vector3 local = actor.transform.InverseTransformPoint(foot.position);
                    z[i] = local.z; y[i] = local.y;
                }
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i <= n; i++) { minY = Mathf.Min(minY, y[i]); maxY = Mathf.Max(maxY, y[i]); }
                float low = minY + (maxY - minY) * .25f;
                float dt = walk.length / n;
                for (int i = 1; i <= n; i++)
                {
                    if (y[i] > low || y[i - 1] > low) continue;
                    float v = (z[i - 1] - z[i]) / dt;
                    if (v > .2f) speeds.Add(v);
                }
            }
            if (speeds.Count < 4) { how = "too few stance samples; 1.35 m/s assumed"; return -1f; }
            speeds.Sort();
            float median = speeds[speeds.Count / 2];
            how = $"{walk.length:0.00} s, stance foot speed median {median:0.00} m/s over {speeds.Count} samples (rig at scale 1)";
            return median;
        }
        finally { Object.DestroyImmediate(actor); }
    }

    // ------------------------------------------------------------------ wire

    public static string Wire()
    {
        RequireStopped();
        var data = AssetDatabase.LoadAssetAtPath<NpcGaitData>(DataPath);
        Require(data != null && data.walks != null && data.walks.Length >= 3, "Run Gait 1 first: " + DataPath + " is missing or incomplete.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null, "Missing " + ControllerPath);
        var notes = new StringBuilder();

        EnsureFloat(controller, "WalkStyle");
        EnsureFloat(controller, "IdleStyle");
        EnsureFloat(controller, "WalkRate");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = Find(machine, "CharacterArmature|Idle"), walk = Find(machine, "CharacterArmature|Walk");
        Require(idle != null && walk != null, "The customer animator has no Idle / Walk state.");

        // Walk: a 1D tree over WalkStyle, one clip per style; WalkRate scales the whole state.
        BlendTree walkTree = EnsureTree(controller, walk, "Walk styles", "WalkStyle");
        SetChildren(walkTree, data.walks.Select(w => w.clip).ToArray());
        walk.speedParameterActive = true;
        walk.speedParameter = "WalkRate";
        // Idle: a 1D tree over IdleStyle.
        BlendTree idleTree = EnsureTree(controller, idle, "Idle styles", "IdleStyle");
        SetChildren(idleTree, data.idles);
        notes.AppendLine($"Walk: {walkTree.children.Length} styles ({string.Join(", ", data.walks.Select(w => w.name))}); Idle: {idleTree.children.Length} styles.");

        // Standing Talk: from Idle while Talking and not Seated; back when it stops; out to Walk / Sit Down like Idle.
        if (data.standingTalk != null)
        {
            AnimatorState talk = Find(machine, "Standing Talk") ?? machine.AddState("Standing Talk", new Vector3(260f, 300f));
            talk.motion = data.standingTalk;
            talk.writeDefaultValues = true;
            foreach (var t in talk.transitions.ToArray()) talk.RemoveTransition(t);
            foreach (var t in idle.transitions.Where(t => t.destinationState == talk).ToArray()) idle.RemoveTransition(t);
            AnimatorStateTransition toTalk = idle.AddTransition(talk);
            toTalk.hasExitTime = false; toTalk.hasFixedDuration = true; toTalk.duration = .3f; toTalk.canTransitionToSelf = false;
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            toTalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Seated");
            // Keep the sit-down transition first (it was prepended by Sit 2).
            var order = idle.transitions.Where(t => t != toTalk).ToList();
            int sitIndex = order.FindIndex(t => t.destinationState != null && t.destinationState.name == "Sit Down");
            order.Insert(sitIndex >= 0 ? sitIndex + 1 : 0, toTalk);
            idle.transitions = order.ToArray();
            AnimatorStateTransition back = talk.AddTransition(idle);
            back.hasExitTime = false; back.hasFixedDuration = true; back.duration = .35f;
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            AnimatorStateTransition toWalk = talk.AddTransition(walk);
            toWalk.hasExitTime = false; toWalk.hasFixedDuration = true; toWalk.duration = .2f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsWalking");
            AnimatorState sitDown = Find(machine, "Sit Down");
            if (sitDown != null)
            {
                AnimatorStateTransition toSit = talk.AddTransition(sitDown);
                toSit.hasExitTime = false; toSit.hasFixedDuration = true; toSit.duration = .2f;
                toSit.AddCondition(AnimatorConditionMode.If, 0f, "Seated");
            }
            notes.AppendLine("Standing Talk: Idle -> (Talking, not Seated) -> Standing Talk -> Idle / Walk / Sit Down.");
        }
        EditorUtility.SetDirty(controller);

        // The prefabs' locomotion reads the gait data.
        foreach (string path in new[] { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var loco = root.GetComponent<NpcLocomotion>();
                if (loco == null) loco = root.AddComponent<NpcLocomotion>();
                var so = new SerializedObject(loco);
                so.FindProperty("gait").objectReferenceValue = data;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.AppendLine(Path.GetFileName(path) + ": NpcLocomotion.gait set.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        WriteLog("gait-wire.txt", notes.ToString());
        return "\n" + notes;
    }

    static void EnsureFloat(AnimatorController controller, string name)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = name == "WalkRate" ? 1f : 0f });
    }

    static AnimatorState Find(AnimatorStateMachine machine, string name) =>
        machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    // The state's motion becomes a 1D blend tree (kept when it already is one),
    // stored inside the controller asset.
    static BlendTree EnsureTree(AnimatorController controller, AnimatorState state, string name, string parameter)
    {
        if (state.motion is BlendTree existing && existing.name == name)
        {
            existing.blendParameter = parameter;
            existing.useAutomaticThresholds = false;
            return existing;
        }
        var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = parameter, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, controller);
        state.motion = tree;
        return tree;
    }

    static void SetChildren(BlendTree tree, Motion[] motions)
    {
        while (tree.children.Length > 0) tree.RemoveChild(0);
        for (int i = 0; i < motions.Length; i++)
            if (motions[i] != null) tree.AddChild(motions[i], i);
        var children = tree.children;
        for (int i = 0; i < children.Length; i++) { children[i].timeScale = 1f; children[i].threshold = i; }
        tree.children = children;
    }

    // ------------------------------------------------------------------ photos

    // Three customers side by side, one per walk style, mid-stride, from the
    // front and the side; and the two idles. For judging the mapping by eye.
    public static string Photograph()
    {
        RequireStopped();
        var data = AssetDatabase.LoadAssetAtPath<NpcGaitData>(DataPath);
        Require(data != null, "Run Gait 1 first.");
        string folder = Path.Combine(LogRoot, "gait-photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var actors = new List<GameObject>();
        var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
        var notes = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
        Vector3 origin = new Vector3(40f, 0f, -40f);   // out of the way of the café
        try
        {
            var poses = new List<(AnimationClip clip, string label, float at)>();
            for (int i = 0; i < data.walks.Length; i++) poses.Add((data.walks[i].clip, data.walks[i].name, .3f));
            for (int i = 0; i < data.idles.Length; i++) poses.Add((data.idles[i], "Idle " + i, .5f));
            if (data.standingTalk != null) poses.Add((data.standingTalk, "Standing talk", .4f));
            for (int i = 0; i < poses.Count; i++)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                actors.Add(actor);
                actor.transform.SetPositionAndRotation(origin + Vector3.right * (i * 1.1f), Quaternion.identity);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null && visual.AppearanceCount > 0) visual.ApplyAppearance(i % visual.AppearanceCount);
                if (poses[i].clip != null) Sample(actor, poses[i].clip, poses[i].at * poses[i].clip.length);
                if (visual != null) visual.Follow();
                baked.AddRange(PolygonNpcSetup.BakePose(actor));
                notes.AppendLine($"{i}: {poses[i].label} @ {poses[i].at:0.00}");
            }
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            Vector3 centre = origin + Vector3.right * ((poses.Count - 1) * .55f) + Vector3.up * .9f;
            CafeSecondPassSteps.Capture(Path.Combine(folder, "1-front.png"), centre + new Vector3(0f, .6f, 5.2f), centre, 42f, true);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "2-side.png"), centre + new Vector3(6.5f, .5f, 0f), centre, 40f, true);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "3-three-quarter.png"), centre + new Vector3(3.5f, 1.2f, 4.2f), centre, 42f, true);
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            PolygonNpcSetup.UnbakePose(baked);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
        }
        WriteLog("gait-photos.txt", notes.ToString());
        return "Photos: " + folder + "\n" + notes;
    }

    static void Sample(GameObject actor, AnimationClip clip, float time)
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(actor, clip, time);
        AnimationMode.EndSampling();
    }

    // ------------------------------------------------------------------ rig maps (as NpcSitAnimations)

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

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcGait"));

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
