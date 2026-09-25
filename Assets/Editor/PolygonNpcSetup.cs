#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// POLYGON City looks for walk-ins, patrons and street neighbours (23 Sept).
//
//   1  Builds one light prefab per look from the purchased Synty characters:
//      one body, no colliders, scripts or active Animator, original materials.
//      Saved under Assets/Art/CityNeighbors, which .gitignore keeps out of the
//      public repository together with Assets/Synty.
//   2  Line-up photo: every look copying the cafe's own idle, walk and
//      interact clips through PolygonNpcVisual's bone follower, next to the
//      original body, so retargeting problems are visible before anyone plays.
//   3  Adds PolygonNpcVisual to the Customer and Patron prefabs. Named regulars
//      keep their authored bodies; a fifth of anonymous walk-ins keep the CC0
//      placeholder looks; everyone else gets a POLYGON look picked from their
//      name. Every other component on the two prefabs is proven unchanged.
public static class PolygonNpcSetup
{
    const string Menu = "Fixit Fidget/City pack/";
    const string Tag = "[City pack NPCs] ";
    public const string Folder = "Assets/Art/CityNeighbors";
    public const string PrefabFolder = Folder + "/Prefabs";
    const string BeachModel = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    static readonly string[] OwnerPrefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };
    public const float KeepOriginalShare = .2f;

    // Everyday civilians for walk-ins: eight women, nine men.
    public static readonly string[] WalkInLooks =
    {
        "Character_BusinessWoman", "Character_Female_Coat", "Character_Female_Jacket",
        "SM_Gen_Chr_Business_Female_01", "SM_Gen_Chr_Street_Female_01", "SM_Gen_Chr_Street_Female_02",
        "SM_Gen_Chr_Street_Female_03", "SM_Gen_Chr_Street_Female_04",
        "Character_BusinessMan_Shirt", "Character_BusinessMan_Suit", "Character_Male_Hoodie", "Character_Male_Jacket",
        "SM_Gen_Chr_Business_Male_01", "SM_Gen_Chr_Street_Male_01", "SM_Gen_Chr_Street_Male_02",
        "SM_Gen_Chr_Street_Male_03", "SM_Gen_Chr_Street_Male_04",
    };
    // Officers only walk the beat outside; they never queue for a repair.
    public static readonly string[] StreetOnlyLooks = { "Character_Female_Police", "Character_Male_Police" };
    public static IEnumerable<string> AllLooks => WalkInLooks.Concat(StreetOnlyLooks);
    public static string LookPath(string name) => PrefabFolder + "/" + name + ".prefab";

    [MenuItem(Menu + "NPC looks 1 - Build the POLYGON looks")]
    static void BuildMenu() => Run("Build looks", PrepareVisualPrefabs);

    [MenuItem(Menu + "NPC looks 2 - Line-up photo (idle, walk, interact)")]
    static void LineupMenu() => Run("Line-up", () => Lineup(Path.Combine(CityPackCatalog.LogRoot, "npc-lineup-" + Stamp() + ".png")));

    [MenuItem(Menu + "NPC looks 3 - Give walk-ins and patrons the new looks")]
    static void ApplyMenu() => Run("Walk-ins and patrons", ApplyToPrefabs);

    [MenuItem(Menu + "NPC looks - Undo: remove the new looks from walk-ins and patrons")]
    static void RemoveMenu() => Run("Remove looks", RemoveFromPrefabs);

    public static string PrepareVisualPrefabs()
    {
        RequireStopped();
        EnsureFolder(PrefabFolder);
        var report = new List<string>();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string name in AllLooks)
            {
                string sourcePath = FindSynty(name);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), preview);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    // Synty keeps every character of a pack in each prefab; only one is active.
                    foreach (Transform child in go.transform.Cast<Transform>().ToArray())
                        if (!child.gameObject.activeSelf && child.GetComponentInChildren<Renderer>(true) != null)
                            Object.DestroyImmediate(child.gameObject);
                    foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) Object.DestroyImmediate(c);
                    foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                    var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Require(skinned.Length > 0, name + " has no skinned body.");
                    var boneNames = new HashSet<string>(skinned.SelectMany(s => s.bones).Where(b => b != null).Select(b => b.name));
                    var missing = PolygonNpcVisual.TargetBones.Where(b => !boneNames.Contains(b)).ToArray();
                    Require(missing.Length == 0, name + " is missing bones: " + string.Join(", ", missing));
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported
                            && !m.shader.name.Contains("InternalError")), name + " has a material this pipeline can't draw.");
                        if (renderer is SkinnedMeshRenderer s) s.updateWhenOffscreen = false;
                    }
                    Bounds b = skinned[0].bounds;
                    foreach (var s in skinned.Skip(1)) b.Encapsulate(s.bounds);
                    go.name = name;
                    PrefabUtility.SaveAsPrefabAsset(go, LookPath(name));
                    report.Add(name + ": " + skinned.Length + " skinned part(s), bind height " + b.size.y.ToString("F2", CultureInfo.InvariantCulture) + " m");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        AssetDatabase.SaveAssets();
        return "Built " + report.Count + " POLYGON looks in " + PrefabFolder + ":\n" + string.Join("\n", report);
    }

    public static string ApplyToPrefabs()
    {
        RequireStopped();
        var looks = LoadLooks(WalkInLooks);
        var report = new List<string>();
        foreach (string path in OwnerPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = Snapshot(root);
                var visual = root.GetComponent<PolygonNpcVisual>();
                if (visual == null) visual = root.AddComponent<PolygonNpcVisual>();
                visual.Configure(looks, KeepOriginalShare, -1, 0f);
                // Prove the actor's skeleton binds, then leave the prefab as authored.
                var identity = root.GetComponent<CustomerIdentity>();
                bool regular = identity != null && identity.IsRegular;
                if (!regular)
                {
                    Require(visual.ApplyAppearance(0), root.name + ": the new look could not bind to this skeleton.");
                    visual.RemoveAppearance();
                }
                var after = Snapshot(root);
                foreach (var pair in before)
                    Require(after.TryGetValue(pair.Key, out string now) && now == pair.Value,
                        "Unexpected change on " + root.name + ": " + pair.Key);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add(Path.GetFileNameWithoutExtension(path) + ": " + looks.Length + " POLYGON looks, "
                    + Mathf.RoundToInt(KeepOriginalShare * 100) + "% of anonymous walk-ins keep a CC0 look; "
                    + before.Count + " existing components unchanged" + (regular ? " (prefab identity is a regular; bind proof skipped)." : "; skeleton bind proven."));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    public static string RemoveFromPrefabs()
    {
        RequireStopped();
        var report = new List<string>();
        foreach (string path in OwnerPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.GetComponent<PolygonNpcVisual>();
                if (visual != null) { Object.DestroyImmediate(visual, true); PrefabUtility.SaveAsPrefabAsset(root, path); report.Add(path + ": removed"); }
                else report.Add(path + ": nothing to remove");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return string.Join("\n", report);
    }

    public static GameObject[] LoadLooks(IEnumerable<string> names)
    {
        var looks = names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(LookPath(n))).ToArray();
        Require(looks.All(l => l != null), "Build the POLYGON looks first (City pack > NPC looks 1).");
        return looks;
    }

    // Each look copying the cafe's own clips, beside the original body.
    public static string Lineup(string file) => Lineup(file, AllLooks.ToArray(),
        new[] { ("CharacterArmature|Idle", .35f), ("CharacterArmature|Walk", .15f), ("CharacterArmature|Walk", .65f), ("CharacterArmature|Interact", .45f) }, 150, 230, 5.6f);

    [MenuItem(Menu + "NPC looks - Close-up walk cycle photo")]
    static void CloseUpMenu() => Run("Walk close-up", () => Lineup(Path.Combine(CityPackCatalog.LogRoot, "npc-walk-closeup-" + Stamp() + ".png"),
        new[] { "Character_BusinessWoman", "SM_Gen_Chr_Street_Male_02", "Character_Male_Police" },
        new[] { ("CharacterArmature|Walk", 0f), ("CharacterArmature|Walk", .25f), ("CharacterArmature|Walk", .5f), ("CharacterArmature|Walk", .75f), ("CharacterArmature|Interact", .5f) }, 300, 420, 5.2f));

    public static string Lineup(string file, string[] lookNames, (string, float)[] poses, int W, int H, float distance)
    {
        RequireStopped();
        var beach = AssetDatabase.LoadAssetAtPath<GameObject>(BeachModel);
        var clips = AssetDatabase.LoadAllAssetsAtPath(BeachModel).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToDictionary(c => c.name);
        foreach (var pose in poses) Require(clips.ContainsKey(pose.Item1), "Missing clip " + pose.Item1);
        var names = new List<string> { null };
        names.AddRange(lookNames);
        var looks = LoadLooks(lookNames);
        var sheet = new Texture2D(names.Count * W, poses.Length * H, TextureFormat.RGB24, false);
        sheet.SetPixels(Enumerable.Repeat(new Color(.22f, .22f, .24f), sheet.width * sheet.height).ToArray());
        var preview = new PreviewRenderUtility();
        var notes = new List<string>();
        try
        {
            preview.camera.fieldOfView = 24f;
            preview.camera.nearClipPlane = .05f;
            preview.camera.farClipPlane = 50f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.80f, .82f, .85f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
            preview.lights[1].intensity = .6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(20f, 220f, 0f);
            preview.ambientColor = new Color(.45f, .45f, .5f);
            for (int col = 0; col < names.Count; col++)
            {
                var actor = preview.InstantiatePrefabInScene(beach);
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                actor.transform.localScale = Vector3.one * 1.1f;
                PolygonNpcVisual visual = null;
                if (names[col] != null)
                {
                    visual = actor.AddComponent<PolygonNpcVisual>();
                    visual.Configure(new[] { looks[col - 1] }, 0f, 0, 0f);
                    if (!visual.ApplyAppearance(0)) notes.Add(names[col] + ": FAILED to bind");
                }
                for (int row = 0; row < poses.Length; row++)
                {
                    var clip = clips[poses[row].Item1];
                    Sample(actor, clip, poses[row].Item2 * clip.length);
                    if (visual != null) visual.Follow();
                    if (col == 0)
                    {
                        var bones = actor.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t, StringComparer.Ordinal);
                        if (bones.TryGetValue("Foot.L", out var fl) && bones.TryGetValue("Foot.R", out var fr) && bones.TryGetValue("Wrist.L", out var wl))
                            notes.Add("original " + poses[row].Item1 + " @" + poses[row].Item2.ToString("F2", CultureInfo.InvariantCulture)
                                + ": feet apart " + Vector3.Distance(fl.position, fr.position).ToString("F2", CultureInfo.InvariantCulture)
                                + " m, left wrist " + wl.position.ToString("F2"));
                    }
                    Vector3 focus = new Vector3(0f, 1.05f, 0f);
                    preview.camera.transform.position = focus + Quaternion.Euler(-8f, 25f, 0f) * Vector3.forward * distance;
                    preview.camera.transform.LookAt(focus);
                    // Several renders happen inside one editor frame, and skinned
                    // meshes only re-skin once per frame: render baked copies.
                    var baked = BakePose(actor);
                    try
                    {
                        preview.BeginStaticPreview(new Rect(0, 0, W, H));
                        preview.Render(true);
                        var shot = preview.EndStaticPreview();
                        sheet.SetPixels(col * W, (poses.Length - 1 - row) * H, W, H, shot.GetPixels());
                        Object.DestroyImmediate(shot);
                    }
                    finally { UnbakePose(baked); }
                }
                if (visual != null && visual.VisualInstance != null)
                {
                    // Feet should meet the ground in the walk: report the lowest vertex.
                    Sample(actor, clips["CharacterArmature|Walk"], .15f * clips["CharacterArmature|Walk"].length);
                    visual.Follow();
                    float lowest = float.MaxValue, highest = float.MinValue;
                    foreach (var s in visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh = SkinToWorld(s);
                        foreach (var v in mesh.vertices)
                        {
                            lowest = Mathf.Min(lowest, v.y); highest = Mathf.Max(highest, v.y);
                        }
                        Object.DestroyImmediate(mesh);
                    }
                    notes.Add(names[col] + ": walking height " + (highest - lowest).ToString("F2", CultureInfo.InvariantCulture)
                        + " m, lowest point " + lowest.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " m");
                }
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(actor);
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            preview.Cleanup();
        }
        sheet.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllBytes(file, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        string text = "Line-up: " + file + "\nColumns: original, " + string.Join(", ", lookNames) + "\nRows: " + string.Join(", ", poses.Select(p => p.Item1 + " @" + p.Item2)) + "\n" + string.Join("\n", notes);
        File.WriteAllText(Path.ChangeExtension(file, ".txt"), text);
        return text;
    }

    // Poses a Mecanim (generic) clip onto a rig in edit mode. AnimationMode is
    // the editor's own sampler; it restores the rig when the mode ends, which
    // happens after the caller has copied the pose (Follow) and rendered it.
    static void Sample(GameObject actor, AnimationClip clip, float time)
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(actor, clip, time);
        AnimationMode.EndSampling();
    }

    // Replaces every visible skinned mesh under root by a static copy of its
    // current pose (same scene and materials) for the renders that follow.
    // Skinned meshes only re-skin once per editor frame, and several renders
    // happen inside one frame, so without this every render shows one pose.
    internal static List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)> BakePose(GameObject root)
    {
        var result = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
        if (root == null) return result;
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.forceRenderingOff || skin.sharedMesh == null) continue;
            var mesh = SkinToWorld(skin);
            var copy = new GameObject("Posed " + skin.name) { hideFlags = HideFlags.HideAndDontSave };
            if (copy.scene != root.scene) SceneManager.MoveGameObjectToScene(copy, root.scene);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = skin.sharedMaterials;
            renderer.shadowCastingMode = skin.shadowCastingMode;
            skin.forceRenderingOff = true;
            result.Add((skin, copy, mesh));
        }
        return result;
    }

    internal static void UnbakePose(List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)> baked)
    {
        foreach (var b in baked)
        {
            if (b.skin != null) b.skin.forceRenderingOff = false;
            if (b.copy != null) Object.DestroyImmediate(b.copy);
            if (b.mesh != null) Object.DestroyImmediate(b.mesh);
        }
        baked.Clear();
    }

    // The skinned mesh in its current pose, in world space, computed on the CPU
    // with the skinning formula itself (weighted bone matrix x bind pose). This
    // avoids BakeMesh, whose output space depends on the renderer's scale
    // conventions (the Quaternius body came out a hundred times too small).
    internal static Mesh SkinToWorld(SkinnedMeshRenderer skin)
    {
        Mesh source = skin.sharedMesh;
        Transform[] bones = skin.bones;
        Matrix4x4[] bindposes = source.bindposes;
        BoneWeight[] weights = source.boneWeights;
        Vector3[] vertices = source.vertices, normals = source.normals;
        bool hasNormals = normals.Length == vertices.Length;
        bool skinned = weights.Length == vertices.Length && bindposes.Length > 0;
        var matrices = new Matrix4x4[bindposes.Length];
        for (int i = 0; i < matrices.Length; i++)
            matrices[i] = (i < bones.Length && bones[i] != null ? bones[i].localToWorldMatrix : skin.transform.localToWorldMatrix) * bindposes[i];
        Matrix4x4 rigid = skin.transform.localToWorldMatrix;
        var world = new Vector3[vertices.Length];
        var worldNormals = new Vector3[hasNormals ? vertices.Length : 0];
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 n = hasNormals ? normals[v] : Vector3.zero;
            if (!skinned)
            {
                world[v] = rigid.MultiplyPoint3x4(vertices[v]);
                if (hasNormals) worldNormals[v] = rigid.MultiplyVector(n).normalized;
                continue;
            }
            BoneWeight w = weights[v];
            Vector3 p = Vector3.zero, q = Vector3.zero;
            float total = 0f;
            Blend(matrices, w.boneIndex0, w.weight0, vertices[v], n, ref p, ref q, ref total);
            Blend(matrices, w.boneIndex1, w.weight1, vertices[v], n, ref p, ref q, ref total);
            Blend(matrices, w.boneIndex2, w.weight2, vertices[v], n, ref p, ref q, ref total);
            Blend(matrices, w.boneIndex3, w.weight3, vertices[v], n, ref p, ref q, ref total);
            world[v] = total > 1e-6f ? p / total : rigid.MultiplyPoint3x4(vertices[v]);
            if (hasNormals) worldNormals[v] = total > 1e-6f ? q.normalized : rigid.MultiplyVector(n).normalized;
        }
        var mesh = new Mesh { name = source.name + " (posed)", indexFormat = source.indexFormat };
        mesh.vertices = world;
        if (hasNormals) mesh.normals = worldNormals;
        var uv = new List<Vector4>();
        for (int channel = 0; channel < 4; channel++)
        {
            source.GetUVs(channel, uv);
            if (uv.Count == vertices.Length) mesh.SetUVs(channel, uv);
        }
        var colors = source.colors32;
        if (colors.Length == vertices.Length) mesh.colors32 = colors;
        mesh.subMeshCount = source.subMeshCount;
        for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s, false);
        mesh.RecalculateBounds();
        if (source.tangents.Length == vertices.Length && hasNormals) mesh.RecalculateTangents();
        return mesh;
    }

    static void Blend(Matrix4x4[] matrices, int bone, float weight, Vector3 vertex, Vector3 normal, ref Vector3 p, ref Vector3 n, ref float total)
    {
        if (weight <= 0f || bone < 0 || bone >= matrices.Length) return;
        p += matrices[bone].MultiplyPoint3x4(vertex) * weight;
        n += matrices[bone].MultiplyVector(normal) * weight;
        total += weight;
    }

    static Dictionary<string, string> Snapshot(GameObject root)
    {
        var result = new Dictionary<string, string>();
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component is PolygonNpcVisual) continue;
            string key = AnimationUtility.CalculateTransformPath(component.transform, root.transform) + ":" + component.GetType().Name;
            int n = 0;
            while (result.ContainsKey(key + "#" + n)) n++;
            result[key + "#" + n] = EditorJsonUtility.ToJson(component);
        }
        return result;
    }

    static string FindSynty(string name)
    {
        string path = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Synty" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.Ordinal));
        Require(path != null, "Missing purchased character " + name + " (import POLYGON City first).");
        return path;
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void RequireStopped() => Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);

    static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
