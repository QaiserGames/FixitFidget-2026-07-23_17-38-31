#if UNITY_EDITOR
using System;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Movement stability pass 1 (claude/cafe-npc-observations-2026-09-26.md):
// the one-off scene and asset changes, as repeatable menu steps.
//
//  1  A "Cafe NPC" NavMesh agent type with the radius the NPC prefabs really
//     have (0.35 m; the bake used Humanoid's 0.5 m), set on the cafe's
//     NavMeshSurface and on both NPC prefabs' agents.
//  2  Re-bake the cafe routes with it (the layout tool's own bake).
//  3  Ace becomes a non-carving NavMeshObstacle, both NPC prefabs get the
//     shared NpcLocomotion, and the animator's Walk state follows a WalkRate
//     parameter so the stride matches the speed.
//
// Every step is safe to run again.
public static class CafeNpcPass1Setup
{
    const string Menu = "Fixit Fidget/Café life/Pass 1 - ";
    const string Tag = "[Café NPC pass 1] ";
    public const string AgentTypeName = "Cafe NPC";
    const float AgentRadius = .35f, AgentHeight = 1.9f, AgentClimb = .4f, AgentSlope = 45f;
    static readonly string[] Prefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };
    const string ControllerPath = "Assets/CustomerAnimator(.controller";

    // ------------------------------------------------------------ step 1

    [MenuItem(Menu + "1 Agent type (Cafe NPC, radius 0.35) on the surface and prefabs")]
    public static void Step1AgentType()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        int id = FindOrCreateAgentType(log);
        if (id == int.MinValue) { Debug.LogError(Tag + log); return; }

        // The cafe's surface(s) in the open scene.
        int surfaces = 0;
        foreach (NavMeshSurface surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
        {
            if (surface.agentTypeID != id)
            {
                Undo.RecordObject(surface, "Cafe NPC agent type");
                surface.agentTypeID = id;
                EditorUtility.SetDirty(surface);
            }
            surfaces++;
            log.AppendLine($"Surface '{surface.name}': agent type {NavMesh.GetSettingsNameFromID(surface.agentTypeID)} ({surface.agentTypeID}).");
        }
        if (surfaces > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        // Both NPC prefabs.
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent == null) { log.AppendLine($"{path}: no NavMeshAgent."); continue; }
                agent.agentTypeID = id;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"{path}: agent type {id}, radius {agent.radius}, height {agent.height}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        if (surfaces > 0) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + "Step 1 done.\n" + log);
    }

    static int FindOrCreateAgentType(StringBuilder log)
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == AgentTypeName)
            {
                log.AppendLine($"Agent type '{AgentTypeName}' exists: id {s.agentTypeID}, radius {s.agentRadius}, height {s.agentHeight}, climb {s.agentClimb}.");
                return Configure(s.agentTypeID, log) ? s.agentTypeID : int.MinValue;
            }
        }
        NavMeshBuildSettings created = NavMesh.CreateSettings();
        log.AppendLine($"Created agent type id {created.agentTypeID}.");
        return Configure(created.agentTypeID, log) ? created.agentTypeID : int.MinValue;
    }

    // The agent list lives in the NavMeshProjectSettings singleton
    // (ProjectSettings/NavMeshAreas.asset); the Navigation window edits it the
    // same way.
    static bool Configure(int id, StringBuilder log)
    {
        UnityEngine.Object singleton = Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings");
        if (singleton == null) { log.AppendLine("No NavMeshProjectSettings singleton."); return false; }
        var so = new SerializedObject(singleton);
        so.Update();
        SerializedProperty list = so.FindProperty("m_Settings");
        SerializedProperty names = so.FindProperty("m_SettingNames");
        if (list == null)
        {
            log.AppendLine("NavMeshProjectSettings has no m_Settings; properties are:");
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true)) log.AppendLine("  " + it.propertyPath + " (" + it.propertyType + ")");
            return false;
        }
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty typeId = entry.FindPropertyRelative("agentTypeID");
            if (typeId == null || typeId.intValue != id) continue;
            entry.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            entry.FindPropertyRelative("agentHeight").floatValue = AgentHeight;
            entry.FindPropertyRelative("agentClimb").floatValue = AgentClimb;
            entry.FindPropertyRelative("agentSlope").floatValue = AgentSlope;
            if (names != null && i < names.arraySize) names.GetArrayElementAtIndex(i).stringValue = AgentTypeName;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(singleton);
            AssetDatabase.SaveAssets();
            NavMeshBuildSettings check = NavMesh.GetSettingsByID(id);
            log.AppendLine($"Agent type '{NavMesh.GetSettingsNameFromID(id)}' ({id}): radius {check.agentRadius}, height {check.agentHeight}, climb {check.agentClimb}, slope {check.agentSlope}.");
            return true;
        }
        log.AppendLine($"Agent type {id} not found in m_Settings ({list.arraySize} entries).");
        return false;
    }

    // ------------------------------------------------------------ step 2

    [MenuItem(Menu + "2 Rebake the cafe routes")]
    public static void Step2Rebake()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        NavMeshSurface surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null) { Debug.LogError(Tag + "No NavMeshSurface in the open scene."); return; }
        string before = Describe();
        AcesCafeLayoutSetup.BakeRoutes();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + "Step 2 done. Before:\n" + before + "\nAfter:\n" + Describe());
    }

    // ------------------------------------------------------------ step 3

    const string PlayerObstacleName = "Ace";
    const float PlayerObstacleRadius = .3f;

    [MenuItem(Menu + "3 Locomotion on the prefabs, Ace as an obstacle, WalkRate in the animator")]
    public static void Step3Components()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();

        // The walk clip's own speed, so WalkRate = speed / clipSpeed keeps the feet planted.
        float clipSpeed = MeasureWalkClipSpeed(log);

        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var agent = root.GetComponent<NavMeshAgent>();
                var loco = root.GetComponent<NpcLocomotion>();
                bool added = false;
                if (loco == null) { loco = root.AddComponent<NpcLocomotion>(); added = true; }
                var so = new SerializedObject(loco);
                float scale = root.transform.localScale.y;
                if (clipSpeed > 0f) so.FindProperty("walkClipSpeed").floatValue = clipSpeed * scale;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (agent != null)
                {
                    // The locomotion sets these at runtime too; the prefab values are what the Inspector shows.
                    agent.speed = so.FindProperty("baseSpeed").floatValue;
                    agent.acceleration = so.FindProperty("acceleration").floatValue;
                    agent.autoBraking = true;
                    agent.stoppingDistance = .3f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"{path}: NpcLocomotion {(added ? "added" : "kept")}, walkClipSpeed {so.FindProperty("walkClipSpeed").floatValue:0.00} (scale {scale:0.00}).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Ace: a non-carving obstacle, so agents steer round him instead of
        // walking into his capsule and shoving him. Non-carving means the
        // NavMesh itself is never cut around the player.
        PlayerMovement ace = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (ace != null)
        {
            var obstacle = ace.GetComponent<NavMeshObstacle>();
            bool added = false;
            if (obstacle == null) { obstacle = Undo.AddComponent<NavMeshObstacle>(ace.gameObject); added = true; }
            var cc = ace.GetComponent<CharacterController>();
            Undo.RecordObject(obstacle, "Ace obstacle");
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = PlayerObstacleRadius;
            obstacle.height = cc != null ? cc.height : 2f;
            obstacle.center = cc != null ? cc.center : new Vector3(0f, 1f, 0f);
            obstacle.carving = false;
            EditorUtility.SetDirty(obstacle);
            log.AppendLine($"Player '{ace.name}': NavMeshObstacle {(added ? "added" : "kept")} (capsule r {obstacle.radius}, h {obstacle.height:0.00}, carving off).");
        }
        else log.AppendLine("No PlayerMovement in the open scene: Ace's obstacle not added.");

        // Animator: a WalkRate parameter driving the Walk state's playback speed.
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        if (controller != null)
        {
            bool hasParam = false;
            foreach (var parameter in controller.parameters) if (parameter.name == "WalkRate") hasParam = true;
            if (!hasParam) controller.AddParameter(new AnimatorControllerParameter { name = "WalkRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            int wired = 0;
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                if (!child.state.name.EndsWith("Walk", StringComparison.Ordinal)) continue;
                child.state.speedParameterActive = true;
                child.state.speedParameter = "WalkRate";
                wired++;
            }
            EditorUtility.SetDirty(controller);
            log.AppendLine($"Animator: WalkRate {(hasParam ? "kept" : "added")}, {wired} walk state(s) follow it.");
        }
        else log.AppendLine("Animator controller not found at " + ControllerPath);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + "Step 3 done.\n" + log);
    }

    // The walk clip is in place, so while a foot is on the ground it slides
    // backwards at the speed the animation was made for. Sample the clip on
    // the rig, take each foot's backward speed while it is low (in stance),
    // and use the median. The prefabs scale the rig, so callers multiply by it.
    static float MeasureWalkClipSpeed(StringBuilder log)
    {
        const string beach = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
        AnimationClip walk = null;
        foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(beach))
            if (o is AnimationClip clip && clip.name.EndsWith("|Walk", StringComparison.Ordinal)) walk = clip;
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(beach);
        if (walk == null || rig == null) { log.AppendLine("Walk clip or rig not found; walkClipSpeed left as it is."); return -1f; }
        GameObject actor = UnityEngine.Object.Instantiate(rig);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            foreach (var a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var speeds = new System.Collections.Generic.List<float>();
            foreach (string footName in new[] { "Foot.L", "Foot.R" })
            {
                Transform foot = null;
                foreach (Transform t in actor.GetComponentsInChildren<Transform>(true)) if (t.name == footName) foot = t;
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
                    float v = (z[i - 1] - z[i]) / dt;   // backwards is positive
                    if (v > .2f) speeds.Add(v);
                }
            }
            if (speeds.Count < 4) { log.AppendLine("Walk clip: too few stance samples; walkClipSpeed left as it is."); return -1f; }
            speeds.Sort();
            float median = speeds[speeds.Count / 2];
            log.AppendLine($"Walk clip '{walk.name}': {walk.length:0.00} s, stance foot speed median {median:0.00} m/s over {speeds.Count} samples (rig at scale 1).");
            return median;
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
    }

    // ------------------------------------------------------------ report

    [MenuItem(Menu + "Report agent types, surface and prefabs")]
    public static void Report() => Debug.Log(Tag + "\n" + Describe());

    static string Describe()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
            sb.AppendLine($"Agent type '{NavMesh.GetSettingsNameFromID(s.agentTypeID)}' ({s.agentTypeID}): radius {s.agentRadius}, height {s.agentHeight}, climb {s.agentClimb}, slope {s.agentSlope}.");
        }
        foreach (NavMeshSurface surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
            sb.AppendLine($"Surface '{surface.name}': agent type {NavMesh.GetSettingsNameFromID(surface.agentTypeID)} ({surface.agentTypeID}), data {(surface.navMeshData != null ? surface.navMeshData.name : "none")}, geometry {surface.useGeometry}, voxel {(surface.overrideVoxelSize ? surface.voxelSize.ToString("0.###") : "default")}.");
        foreach (string path in Prefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var agent = go != null ? go.GetComponent<NavMeshAgent>() : null;
            if (agent != null) sb.AppendLine($"{path}: agent type {NavMesh.GetSettingsNameFromID(agent.agentTypeID)} ({agent.agentTypeID}), radius {agent.radius}, speed {agent.speed}, stopping {agent.stoppingDistance}.");
        }
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        sb.AppendLine($"NavMesh: {tri.vertices.Length} vertices, {tri.indices.Length / 3} triangles.");
        return sb.ToString();
    }
}
#endif
