#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// NPC life pass 2: the one-off project, scene and prefab changes, as repeatable
// menu steps (Fixit Fidget > Café life > Pass 2 - ...). Every step is safe to
// run again.
//
//  0  Ace against the NPC bodies. The NPC prefabs' capsules used to share the
//     Default layer with the furniture, so whenever a body in a crowd overlapped
//     Ace's CharacterController, the controller pushed itself out of the capsule
//     on its next Move - the "shove" (1.2 m in the doorway stress run). The
//     capsules move to an NPC layer that the Player layer does not collide with;
//     the controller is never displaced by a body again. Bodies keep steering
//     round Ace (his non-carving NavMeshObstacle, radius raised to match his
//     capsule) and give way to him when they do touch (PersonalSpace). Seated
//     bodies go back to Default while in the chair, so Ace still cannot walk
//     through a sitting customer. No carving, no rigidbodies, no other
//     collision changed.
public static class CafeNpcPass2Setup
{
    const string Menu = "Fixit Fidget/Café life/Pass 2 - ";
    const string Tag = "[Café NPC pass 2] ";
    public const string NpcLayerName = "NPC";
    public const int NpcLayerPreferred = 8;
    const string PlayerLayerName = "Player";
    const float PlayerObstacleRadius = .4f;
    static readonly string[] Prefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };

    // ------------------------------------------------------------ step 0

    [MenuItem(Menu + "0 Ace vs NPC bodies (NPC layer, collision matrix, prefabs, obstacle)")]
    public static void Step0AceCollision()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        try
        {
            int npc = EnsureLayer(NpcLayerName, NpcLayerPreferred, log);
            int player = LayerMask.NameToLayer(PlayerLayerName);
            if (player < 0) throw new Exception("No '" + PlayerLayerName + "' layer in the project.");
            IgnoreCollision(player, npc, log);

            foreach (string path in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = 0;
                    if (root.layer != npc) { root.layer = npc; changed++; }
                    foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                        if (c.gameObject.layer != npc) { c.gameObject.layer = npc; changed++; }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine($"{path}: root and {root.GetComponentsInChildren<Collider>(true).Length} collider object(s) on layer {npc} ({changed} changed).");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            PlayerMovement ace = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (ace != null)
            {
                var cc = ace.GetComponent<CharacterController>();
                var obstacle = ace.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = Undo.AddComponent<NavMeshObstacle>(ace.gameObject);
                Undo.RecordObject(obstacle, "Ace obstacle");
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = PlayerObstacleRadius;
                obstacle.height = cc != null ? cc.height : 2f;
                obstacle.center = cc != null ? cc.center : Vector3.zero;
                obstacle.carving = false;
                EditorUtility.SetDirty(obstacle);
                if (ace.gameObject.layer != player)
                {
                    Undo.RecordObject(ace.gameObject, "Ace layer");
                    ace.gameObject.layer = player;
                    EditorUtility.SetDirty(ace.gameObject);
                }
                log.AppendLine($"Player '{ace.name}': layer {ace.gameObject.layer}, controller radius {(cc != null ? cc.radius : 0f):0.00}, obstacle radius {obstacle.radius:0.00} (carving off).");
                EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);
                EditorSceneManager.SaveScene(ace.gameObject.scene);
            }
            else log.AppendLine("No PlayerMovement in the open scene: Ace's obstacle left as it is.");

            AssetDatabase.SaveAssets();
            log.AppendLine(DescribeMatrix(player, npc));
            Debug.Log(Tag + "Step 0 done.\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Step 0 failed: " + e.Message + "\n" + log);
        }
    }

    [MenuItem(Menu + "Report Ace vs NPC layers")]
    public static void Report()
    {
        int npc = LayerMask.NameToLayer(NpcLayerName), player = LayerMask.NameToLayer(PlayerLayerName);
        var sb = new StringBuilder();
        sb.AppendLine($"Layers: {PlayerLayerName}={player}, {NpcLayerName}={npc}.");
        if (npc >= 0 && player >= 0) sb.AppendLine(DescribeMatrix(player, npc));
        foreach (string path in Prefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) sb.AppendLine($"{path}: layer {go.layer} ({LayerMask.LayerToName(go.layer)}).");
        }
        PlayerMovement ace = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (ace != null)
        {
            var o = ace.GetComponent<NavMeshObstacle>();
            var cc = ace.GetComponent<CharacterController>();
            sb.AppendLine($"Ace: layer {ace.gameObject.layer}, controller r {(cc != null ? cc.radius : 0f):0.00} h {(cc != null ? cc.height : 0f):0.00}, obstacle {(o != null ? $"r {o.radius:0.00} carving {o.carving}" : "none")}.");
        }
        Debug.Log(Tag + "\n" + sb);
    }

    // The layer list lives in ProjectSettings/TagManager.asset; the Tags and
    // Layers inspector edits the same serialized object.
    static int EnsureLayer(string name, int preferred, StringBuilder log)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) { log.AppendLine($"Layer '{name}' exists: {existing}."); return existing; }
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) throw new Exception("ProjectSettings/TagManager.asset not loadable.");
        var so = new SerializedObject(assets[0]);
        SerializedProperty layers = so.FindProperty("layers");
        if (layers == null || !layers.isArray) throw new Exception("TagManager has no 'layers' array.");
        int index = -1;
        if (preferred >= 8 && preferred < layers.arraySize && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferred).stringValue)) index = preferred;
        for (int i = 8; index < 0 && i < layers.arraySize; i++)
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) index = i;
        if (index < 0) throw new Exception("No free user layer for '" + name + "'.");
        layers.GetArrayElementAtIndex(index).stringValue = name;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        int check = LayerMask.NameToLayer(name);
        log.AppendLine($"Layer '{name}' created at {index} (lookup says {check}).");
        return check >= 0 ? check : index;
    }

    // Physics.IgnoreLayerCollision edits the PhysicsManager's matrix (the one the
    // Physics settings window shows). It is written to ProjectSettings/
    // DynamicsManager.asset with the next save; the file is read back to prove
    // it, and patched directly if the editor did not write it.
    static void IgnoreCollision(int a, int b, StringBuilder log)
    {
        Physics.IgnoreLayerCollision(a, b, true);
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
        if (assets != null && assets.Length > 0) EditorUtility.SetDirty(assets[0]);
        AssetDatabase.SaveAssets();
        bool runtime = Physics.GetIgnoreLayerCollision(a, b);
        bool onDisk = MatrixOnDiskIgnores(a, b, out string state);
        log.AppendLine($"Collision {LayerMask.LayerToName(a)}({a}) x {LayerMask.LayerToName(b)}({b}): ignored in the editor = {runtime}, on disk = {onDisk} {state}.");
        if (!onDisk && PatchMatrixOnDisk(a, b))
        {
            AssetDatabase.Refresh();
            log.AppendLine($"  DynamicsManager.asset patched on disk; on disk now = {MatrixOnDiskIgnores(a, b, out _)}.");
        }
    }

    const string DynamicsPath = "ProjectSettings/DynamicsManager.asset";
    const string MatrixKey = "m_LayerCollisionMatrix: ";

    static bool MatrixOnDiskIgnores(int a, int b, out string state)
    {
        state = "";
        if (!File.Exists(DynamicsPath)) { state = "(no file)"; return false; }
        string text = File.ReadAllText(DynamicsPath);
        int at = text.IndexOf(MatrixKey, StringComparison.Ordinal);
        if (at < 0) { state = "(no matrix in file)"; return false; }
        int start = at + MatrixKey.Length;
        int end = text.IndexOfAny(new[] { '\r', '\n' }, start);
        string hex = text.Substring(start, (end < 0 ? text.Length : end) - start).Trim();
        if (hex.Length < 32 * 8) { state = $"(matrix is {hex.Length} hex chars)"; return false; }
        uint wa = Word(hex, a), wb = Word(hex, b);
        bool ignoredA = (wa & (1u << b)) == 0, ignoredB = (wb & (1u << a)) == 0;
        state = $"(word {a} = {wa:x8}, word {b} = {wb:x8})";
        return ignoredA && ignoredB;
    }

    // Each layer's row is a little-endian uint32, dumped as raw bytes.
    static uint Word(string hex, int layer)
    {
        uint v = 0;
        for (int i = 0; i < 4; i++) v |= (uint)Convert.ToByte(hex.Substring(layer * 8 + i * 2, 2), 16) << (8 * i);
        return v;
    }

    static string WordHex(uint v)
    {
        var sb = new StringBuilder(8);
        for (int i = 0; i < 4; i++) sb.Append(((v >> (8 * i)) & 0xff).ToString("x2"));
        return sb.ToString();
    }

    static bool PatchMatrixOnDisk(int a, int b)
    {
        if (!File.Exists(DynamicsPath)) return false;
        string text = File.ReadAllText(DynamicsPath);
        int at = text.IndexOf(MatrixKey, StringComparison.Ordinal);
        if (at < 0) return false;
        int start = at + MatrixKey.Length;
        int end = text.IndexOfAny(new[] { '\r', '\n' }, start);
        if (end < 0) end = text.Length;
        string hex = text.Substring(start, end - start).Trim();
        if (hex.Length < 32 * 8) return false;
        var chars = new StringBuilder(hex);
        uint wa = Word(hex, a) & ~(1u << b), wb = Word(hex, b) & ~(1u << a);
        chars.Remove(a * 8, 8).Insert(a * 8, WordHex(wa));
        chars.Remove(b * 8, 8).Insert(b * 8, WordHex(wb));
        File.WriteAllText(DynamicsPath, text.Substring(0, start) + chars + text.Substring(end));
        return true;
    }

    // ------------------------------------------------------------ lounge geometry

    // Renderer and collider bounds of the window lounge, the NavMesh next to it
    // and the existing seats, so the sofa and banquette seats can be placed
    // from numbers rather than by eye.
    [MenuItem(Menu + "Report lounge geometry (sofas, banquette, table, NavMesh)")]
    public static void ReportLounge()
    {
        var sb = new StringBuilder();
        string[] names = { "Front window banquette", "Reading window banquette", "Banquette timber base", "Moss seat cushion",
            "Lounge table", "Tub chair", "Floor lamp (front sofa)", "Floor lamp (rear sofa)", "Lounge kilim", "Book left on the sofa",
            "Folded throw", "Window ledge", "Left window sill", "Hanging plant (between the sofas)" };
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            bool wanted = false;
            foreach (string n in names) if (t.name == n || t.name.StartsWith(n, StringComparison.Ordinal)) wanted = true;
            if (!wanted) continue;
            sb.AppendLine($"'{t.name}' at {V(t.position)} yaw {t.eulerAngles.y:0} scale {V(t.lossyScale)} active {t.gameObject.activeInHierarchy}");
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"   renderer '{r.name}' enabled {r.enabled}: min {V(r.bounds.min)} max {V(r.bounds.max)}");
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                sb.AppendLine($"   collider '{c.name}' ({c.GetType().Name}) enabled {c.enabled}: min {V(c.bounds.min)} max {V(c.bounds.max)}");
        }
        // Existing table seats, for reference (stand point, seat pose, cup spot).
        foreach (TableSeat seat in UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include))
            sb.AppendLine($"Seat '{seat.name}' ({seat.transform.parent?.name}): stand {V(seat.StandPoint.position)} yaw {seat.StandPoint.eulerAngles.y:0}, pose {V(seat.SeatPose.position)}, cup {V(seat.CupSpot.position)}, snap {seat.SnapToSeat}");
        foreach (WaitingSpot spot in UnityEngine.Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Include))
            if (!(spot is TableSeat)) sb.AppendLine($"Spot '{spot.name}' {spot.Kind}: stand {V(spot.StandPoint.position)} yaw {spot.StandPoint.eulerAngles.y:0} active {spot.isActiveAndEnabled}");
        // NavMesh along the lounge: is the floor in front of the sofas walkable, and how far out?
        for (float z = 1.5f; z <= 8.6f; z += .5f)
        {
            var row = new StringBuilder($"z {z,4:0.0}:");
            for (float x = -6.9f; x <= -4.9f; x += .2f)
            {
                bool on = NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit hit, .12f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y) < .3f;
                row.Append(on ? " #" : " .");
            }
            sb.AppendLine(row.ToString());
        }
        sb.AppendLine("(columns x = -6.9 .. -4.9 in 0.2 m steps; # = NavMesh within 0.12 m)");
        // Any NavMesh above the floor over the sofas (a cushion a body could stand on)?
        for (float z = 1.5f; z <= 8.6f; z += .5f)
        {
            var row = new StringBuilder($"z {z,4:0.0} heights:");
            for (float x = -7.1f; x <= -5.5f; x += .2f)
            {
                bool on = NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit hit, 1.2f, NavMesh.AllAreas);
                row.Append(on ? $" {hit.position.y,5:0.00}" : "   ---");
            }
            sb.AppendLine(row.ToString());
        }
        sb.AppendLine("(columns x = -7.1 .. -5.5 in 0.2 m steps: NavMesh height nearest to y 0.5 within 1.2 m)");
        // Where the NavMesh is by area over the lounge: any Not Walkable islands standing in?
        sb.AppendLine($"NavMesh triangulation: {NavMesh.CalculateTriangulation().vertices.Length} vertices.");
        Debug.Log(Tag + "Lounge geometry:\n" + sb);
    }

    // ------------------------------------------------------------ step 3: profiles

    public const string ProfileFolder = "Assets/Data/NpcProfiles";
    public const string LibraryPath = ProfileFolder + "/NpcProfileLibrary.asset";
    const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";

    // The five archetypes. Subtle on purpose: the biggest speed difference is
    // 18 % either way, and every number is presentation only.
    struct Archetype
    {
        public string name; public float weight, walkSpeed, accel, turn, urgency, space, looseness, sway, idleFreq, idleDur, look, gesture, social, shy, react;
        public NpcMovementProfile.WalkStyle walk; public NpcMovementProfile.IdleStyle idle;
    }

    static readonly Archetype[] Archetypes =
    {
        new Archetype { name = "Relaxed", weight = 1.2f, walkSpeed = .86f, accel = .8f, turn = .8f, urgency = .2f, space = .01f, looseness = 1.2f, sway = 1.4f, idleFreq = 1.3f, idleDur = 1.4f, look = .7f, gesture = .4f, social = .5f, shy = 0f, react = .35f, walk = NpcMovementProfile.WalkStyle.Relaxed, idle = NpcMovementProfile.IdleStyle.Relaxed },
        new Archetype { name = "Hurried", weight = 1f, walkSpeed = 1.18f, accel = 1.4f, turn = 1.25f, urgency = .9f, space = 0f, looseness = .8f, sway = .8f, idleFreq = .6f, idleDur = .6f, look = .35f, gesture = .3f, social = .3f, shy = 0f, react = .1f, walk = NpcMovementProfile.WalkStyle.Brisk, idle = NpcMovementProfile.IdleStyle.Normal },
        new Archetype { name = "Shy", weight = 1f, walkSpeed = .95f, accel = .9f, turn = .95f, urgency = .5f, space = .06f, looseness = .9f, sway = 1f, idleFreq = .9f, idleDur = .9f, look = .3f, gesture = .15f, social = .15f, shy = .8f, react = .3f, walk = NpcMovementProfile.WalkStyle.Normal, idle = NpcMovementProfile.IdleStyle.Normal },
        new Archetype { name = "Social", weight = 1.1f, walkSpeed = 1.02f, accel = 1f, turn = 1.05f, urgency = .5f, space = 0f, looseness = 1.1f, sway = 1.3f, idleFreq = 1.2f, idleDur = 1f, look = .8f, gesture = .7f, social = .9f, shy = 0f, react = .15f, walk = NpcMovementProfile.WalkStyle.Normal, idle = NpcMovementProfile.IdleStyle.Relaxed },
        new Archetype { name = "Distracted", weight = .9f, walkSpeed = .92f, accel = .9f, turn = .85f, urgency = .35f, space = .02f, looseness = 1.6f, sway = 1.6f, idleFreq = 1.4f, idleDur = 1.2f, look = .9f, gesture = .35f, social = .45f, shy = 0f, react = .7f, walk = NpcMovementProfile.WalkStyle.Relaxed, idle = NpcMovementProfile.IdleStyle.Normal },
    };

    [MenuItem(Menu + "3 Movement profiles (5 archetypes, library, prefabs, Grace)")]
    public static void Step3Profiles()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        try
        {
            EnsureFolder(ProfileFolder);
            var made = new List<NpcMovementProfile>();
            foreach (Archetype a in Archetypes)
            {
                string path = $"{ProfileFolder}/NpcProfile_{a.name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<NpcMovementProfile>(path);
                bool created = profile == null;
                if (created) { profile = ScriptableObject.CreateInstance<NpcMovementProfile>(); AssetDatabase.CreateAsset(profile, path); }
                profile.displayName = a.name; profile.weight = a.weight;
                profile.walkSpeed = a.walkSpeed; profile.acceleration = a.accel; profile.turnSpeed = a.turn; profile.walkStyle = a.walk;
                profile.urgency = a.urgency; profile.personalSpace = a.space;
                profile.idleStyle = a.idle; profile.facingLooseness = a.looseness; profile.standingSway = a.sway;
                profile.idleFrequency = a.idleFreq; profile.idleDuration = a.idleDur;
                profile.lookTendency = a.look; profile.gestureLikelihood = a.gesture; profile.sociability = a.social; profile.shyness = a.shy; profile.reactionDelay = a.react;
                EditorUtility.SetDirty(profile);
                made.Add(profile);
                log.AppendLine($"{a.name}: {(created ? "created" : "updated")} (walk x{a.walkSpeed:0.00}, {a.walk} walk, {a.idle} idle).");
            }
            var library = AssetDatabase.LoadAssetAtPath<NpcProfileLibrary>(LibraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<NpcProfileLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            library.profiles = made.ToArray();
            EditorUtility.SetDirty(library);

            // Prefabs: NpcSocial (with the library) and NpcPosture on both.
            foreach (string path in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var social = root.GetComponent<NpcSocial>();
                    bool addedSocial = social == null;
                    if (addedSocial) social = root.AddComponent<NpcSocial>();
                    var so = new SerializedObject(social);
                    so.FindProperty("library").objectReferenceValue = library;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    bool addedPosture = root.GetComponent<NpcPosture>() == null;
                    if (addedPosture) root.AddComponent<NpcPosture>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine($"{path}: NpcSocial {(addedSocial ? "added" : "kept")} (library set), NpcPosture {(addedPosture ? "added" : "kept")}.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // Grace keeps the same manner every visit: relaxed, as her lines are.
            var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
            if (grace != null)
            {
                var so = new SerializedObject(grace);
                SerializedProperty prop = so.FindProperty("movementProfile");
                if (prop != null)
                {
                    prop.objectReferenceValue = made.Find(m => m.displayName == "Relaxed");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(grace);
                    log.AppendLine("Grace: movement profile Relaxed (her dialogue, memory and visit pattern untouched).");
                }
            }
            else log.AppendLine("Regular_Grace.asset not found; no regular profile set.");
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "Step 3 done.\n" + log);
        }
        catch (Exception e) { Debug.LogError(Tag + "Step 3 failed: " + e.Message + "\n" + log + e); }
    }

    // ------------------------------------------------------------ step 4: sofa and banquette seats

    const string LoungeSeatsName = "Lounge seats (pass 2)";

    // Seat poses from the lounge geometry report (26 Sept): both sofas' cushion
    // tops at y 0.47, front edge at x -6.42; a person sits 0.24 m in from the
    // edge (hips at x -6.66) with the feet on the floor just past it. The front
    // sofa has the coffee table in front of it (z 2.85-3.75), so its two seats
    // are entered from either end of the table; the reading sofa is open. The
    // tub chair at the table is a normal chair seat. The book on the reading
    // sofa moves 20 cm along so it sits between the two seats.
    struct LoungeSeat
    {
        public string name; public Vector3 stand, pose, cup; public Vector3? face; public TableSeat.SitStyle style; public float clearance;
    }

    static readonly LoungeSeat[] LoungeSeats =
    {
        new LoungeSeat { name = "Front sofa - seat A", stand = new Vector3(-5.92f, 0f, 2.45f), pose = new Vector3(-6.66f, .47f, 2.92f), cup = new Vector3(-6.0f, .435f, 2.98f), style = TableSeat.SitStyle.Bench, clearance = .3f },
        new LoungeSeat { name = "Front sofa - seat B", stand = new Vector3(-5.92f, 0f, 4.42f), pose = new Vector3(-6.66f, .47f, 3.68f), cup = new Vector3(-6.0f, .435f, 3.62f), style = TableSeat.SitStyle.Bench, clearance = .3f },
        new LoungeSeat { name = "Reading sofa - seat A", stand = new Vector3(-5.92f, 0f, 6.17f), pose = new Vector3(-6.66f, .47f, 6.17f), cup = new Vector3(-6.68f, .507f, 6.55f), face = new Vector3(-4.6f, 0f, 6.1f), style = TableSeat.SitStyle.Bench, clearance = .3f },
        new LoungeSeat { name = "Reading sofa - seat B", stand = new Vector3(-5.92f, 0f, 6.92f), pose = new Vector3(-6.66f, .47f, 6.92f), cup = new Vector3(-6.70f, .535f, 7.33f), face = new Vector3(-4.6f, 0f, 7.0f), style = TableSeat.SitStyle.Bench, clearance = .3f },
        new LoungeSeat { name = "Tub chair", stand = new Vector3(-5.05f, 0f, 2.38f), pose = new Vector3(-5.10f, .423f, 3.3f), cup = new Vector3(-5.68f, .435f, 3.3f), style = TableSeat.SitStyle.Chair, clearance = .3f },
    };

    [MenuItem(Menu + "4 Sofa, banquette and tub chair seats (lounge)")]
    public static void Step4LoungeSeats()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        try
        {
            Transform parent = null;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "Window upholstered seating") { parent = t; break; }
            if (parent == null) throw new Exception("No 'Window upholstered seating' object in the open scene.");
            Transform group = parent.Find(LoungeSeatsName);
            if (group == null)
            {
                group = new GameObject(LoungeSeatsName).transform;
                Undo.RegisterCreatedObjectUndo(group.gameObject, "Lounge seats");
                group.SetParent(parent, false);
            }
            int made = 0, kept = 0;
            foreach (LoungeSeat spec in LoungeSeats)
            {
                Transform seatObject = group.Find(spec.name);
                if (seatObject == null)
                {
                    seatObject = new GameObject(spec.name).transform;
                    Undo.RegisterCreatedObjectUndo(seatObject.gameObject, "Lounge seat");
                    seatObject.SetParent(group, false);
                    made++;
                }
                else kept++;
                Vector3 toRoom = spec.face ?? spec.cup; toRoom -= spec.pose; toRoom.y = 0f;
                seatObject.SetPositionAndRotation(spec.pose, Quaternion.LookRotation(toRoom.normalized, Vector3.up));
                Transform stand = Child(seatObject, "StandPoint"), pose = Child(seatObject, "SeatPose"), cup = Child(seatObject, "CupSpot");
                Vector3 standFacing = spec.pose - spec.stand; standFacing.y = 0f;
                if (!NavMesh.SamplePosition(spec.stand, out NavMeshHit hit, .6f, NavMesh.AllAreas)) log.AppendLine($"  WARNING: {spec.name} stand point {V(spec.stand)} is not within 0.6 m of the NavMesh.");
                stand.SetPositionAndRotation(hit.hit ? hit.position : spec.stand, Quaternion.LookRotation(standFacing.normalized, Vector3.up));
                pose.SetPositionAndRotation(spec.pose, seatObject.rotation);
                cup.SetPositionAndRotation(spec.cup, Quaternion.identity);
                Transform face = null;
                if (spec.face.HasValue) { face = Child(seatObject, "FaceTowards"); face.position = spec.face.Value; }

                var seat = seatObject.GetComponent<TableSeat>();
                if (seat == null) seat = Undo.AddComponent<TableSeat>(seatObject.gameObject);
                var so = new SerializedObject(seat);
                so.FindProperty("kind").enumValueIndex = (int)WaitingSpot.SpotKind.Seat;
                so.FindProperty("standPoint").objectReferenceValue = stand;
                so.FindProperty("drainMultiplier").floatValue = .55f;
                so.FindProperty("cupSpot").objectReferenceValue = cup;
                so.FindProperty("snapToSeat").boolValue = true;
                so.FindProperty("seatPose").objectReferenceValue = pose;
                so.FindProperty("sitStyle").enumValueIndex = (int)spec.style;
                so.FindProperty("faceTowards").objectReferenceValue = face;
                so.FindProperty("clearanceRadius").floatValue = spec.clearance;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(seat);
                log.AppendLine($"{spec.name}: {spec.style}, stand {V(stand.position)}, pose {V(spec.pose)}, cup {V(spec.cup)}.");
            }
            // The book between the two reading-sofa seats.
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "Book left on the sofa" && Mathf.Abs(t.position.z - 6.35f) < .05f)
                {
                    Undo.RecordObject(t, "Book between the seats");
                    t.position = new Vector3(t.position.x, t.position.y, 6.55f);
                    EditorUtility.SetDirty(t);
                    log.AppendLine("Moved 'Book left on the sofa' 20 cm along the sofa (z 6.35 -> 6.55), between the two seats.");
                }
            EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
            EditorSceneManager.SaveScene(group.gameObject.scene);
            log.Insert(0, $"{made} seats created, {kept} updated under '{LoungeSeatsName}'.\n");
            Debug.Log(Tag + "Step 4 done.\n" + log);
        }
        catch (Exception e) { Debug.LogError(Tag + "Step 4 failed: " + e.Message + "\n" + log + e); }
    }

    // ------------------------------------------------------------ step 5: lounge furniture and the NavMesh

    // The lounge set (coffee table 0.43 m, tub chair, sofas) was furnished
    // without NavMesh modifiers. The routes bake collects physics colliders
    // with a 0.08 m voxel and a 0.4 m step, so the table top and the sofa
    // cushions became a walkable plateau with ramps up from the floor - a
    // patron walking to a sofa could step onto it (and one did: the life check
    // found a sitter 31 cm in the air, because the hand-over happened while the
    // agent stood on the table top). The dining tables never had this problem:
    // their FURNITURE group carries a Not Walkable modifier. This step gives
    // the furnishing-pass groups and the sofas the same, and re-bakes.
    [MenuItem(Menu + "5 Lounge furniture Not Walkable and re-bake the routes")]
    public static void Step5LoungeNavMesh()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        try
        {
            string[] groups = { "18 - cafe furnishing", "Window upholstered seating", "Window banquette - future seated animation" };
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (Array.IndexOf(groups, t.name) < 0) continue;
                var modifier = t.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                bool added = modifier == null;
                if (added) modifier = Undo.AddComponent<Unity.AI.Navigation.NavMeshModifier>(t.gameObject);
                Undo.RecordObject(modifier, "Not walkable");
                modifier.overrideArea = true;
                modifier.area = 1;   // Not Walkable
                modifier.ignoreFromBuild = false;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
                log.AppendLine($"'{t.name}': NavMeshModifier Not Walkable ({(added ? "added" : "kept")}), {t.GetComponentsInChildren<Collider>(true).Length} colliders under it.");
            }
            // The lounge seats' own objects have no colliders; nothing to exclude.
            string before = HeightsOverLounge();
            AcesCafeLayoutSetup.BakeRoutes();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            log.AppendLine("Re-baked the cafe routes.");
            log.AppendLine("NavMesh above the floor over the lounge, before: " + before);
            log.AppendLine("NavMesh above the floor over the lounge, after:  " + HeightsOverLounge());
            foreach (TableSeat seat in UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
                if (seat.transform.parent != null && seat.transform.parent.name == LoungeSeatsName)
                {
                    bool on = NavMesh.SamplePosition(seat.StandPoint.position, out NavMeshHit hit, .2f, NavMesh.AllAreas);
                    log.AppendLine($"  '{seat.name}' stand point: {(on ? $"on the NavMesh at y {hit.position.y:0.00}" : "NOT on the NavMesh within 0.2 m")}.");
                }
            Debug.Log(Tag + "Step 5 done.\n" + log);
        }
        catch (Exception e) { Debug.LogError(Tag + "Step 5 failed: " + e.Message + "\n" + log + e); }
    }

    // Count of sample points over the lounge (z 2-8.5, x -7.1..-5.5) whose nearest NavMesh is more than 0.2 m up.
    static string HeightsOverLounge()
    {
        int raised = 0, total = 0;
        float highest = 0f;
        for (float z = 2f; z <= 8.6f; z += .25f)
            for (float x = -7.1f; x <= -5.5f; x += .2f)
            {
                total++;
                if (NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit hit, 1.2f, NavMesh.AllAreas) && hit.position.y > .2f) { raised++; highest = Mathf.Max(highest, hit.position.y); }
            }
        return $"{raised} of {total} sample points find NavMesh above 0.2 m (highest {highest:0.00} m)";
    }

    static Transform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        Undo.RegisterCreatedObjectUndo(t.gameObject, "Lounge seat part");
        t.SetParent(parent, false);
        return t;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

    static string DescribeMatrix(int player, int npc)
    {
        bool onDisk = MatrixOnDiskIgnores(player, npc, out string state);
        return $"Matrix: Player x NPC ignored = {Physics.GetIgnoreLayerCollision(player, npc)} (editor), {onDisk} (disk) {state}; " +
               $"NPC x Default ignored = {Physics.GetIgnoreLayerCollision(npc, 0)}; Player x Default ignored = {Physics.GetIgnoreLayerCollision(player, 0)}.";
    }
}
#endif
