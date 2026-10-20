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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// REVIEW 1 - MODEL SHEETS: EVERY MODEL THE WAY UNITY DRAWS IT, FROM EVERY SIDE, EVERY PART MEASURED (19 Oct 2026)
//
//   Fixit Fidget > Review > Model sheets 1 - Devices and bench tools from every side (edit mode, read-only)
//   Fixit Fidget > Review > Model sheets 2 - Every model in Assets∕Art∕Models (edit mode, read-only, slow)
//   Fixit Fidget > Review > Model sheets 3 - The selected prefab or model (edit mode, read-only)
//   Fixit Fidget > Review > Model sheets 4 - Every piece in the open scene (edit mode, read-only, slow)
//
// Why. His girlfriend's playtest (19 Oct) found Grace's camera strap see-through in the game: the leather's faces point the
// wrong way, so Unity, which draws front faces only, culls them, while the stitches and edges (their own meshes) float in
// the air. It looked right in the Blender review renders because Blender draws both sides. Every check before it said
// "All good", because they checked that a part was there and the right size, never what a player would actually SEE.
// His note: "something that reviews EVERYTHING AND EVERYTHING, because these silly mistakes I want to get ahead of."
// This is the model half of that review.
//
// What it does, per subject (a prefab, an FBX, or one piece of the open scene), in a preview scene, never the open one:
//   * the SHEET: eight views in its real materials (the four sides, top, bottom, two three-quarters), one PNG;
//   * the X-RAY: the same eight views with every drawn part in its own flat colour, back faces culled as the game does, so
//     an inside-out part is simply missing from it;
//   * the FACE TEST, the heart of it: every part on its own, from 14 directions, its front faces white and its back faces
//     red, every other part black in front of it, so only what a player could see of it is counted. A direction where a
//     quarter or more of what shows is red is SEE-THROUGH (the strap); a part that shows from no direction is NEVER SEEN
//     (the purpose rule: "every part has to mean something");
//   * the material and renderer checks: an empty material slot, a broken shader, a transparent base colour, a Render Face
//     set to Back or Both, shadows only, a mirrored scale, a mesh with no triangles, a part under 0.2 mm.
// Output: Logs/Review/models-<time>/: the sheets, a picture per flagged part (the real view beside the face view from the
// worst direction), report.txt (flags first, then every part's numbers) and flags.json for anything that reads them.
//
// Read-only, all four of them: nothing is saved, no importer or prefab is touched, no Undo is recorded. The open scene is
// copied into the preview scene one group at a time (Model sheets 4) and never marked dirty. Every material and texture
// made here is destroyed on the way out (BenchScale.Studio only cleans what it made itself). None of them run in Play mode.
// ---------------------------------------------------------------------------
internal static class ReviewModelSheets
{
    const string Tag = "[Review] ";
    const string Title = "Review 1 - model sheets";
    const string MenuRoot = "Fixit Fidget/Review/";
    // Unity reads every "/" in a menu path as a submenu, so the folder in Model sheets 2's name is spelt with the division
    // slash (U+2215): it reads the same and makes no submenus.
    const string Menu1 = MenuRoot + "Model sheets 1 - Devices and bench tools from every side (edit mode, read-only)";
    const string Menu2 = MenuRoot + "Model sheets 2 - Every model in Assets\u2215Art\u2215Models (edit mode, read-only, slow)";
    const string Menu3 = MenuRoot + "Model sheets 3 - The selected prefab or model (edit mode, read-only)";
    const string Menu4 = MenuRoot + "Model sheets 4 - Every piece in the open scene (edit mode, read-only, slow)";

    const string ShaderName = "Hidden/FixitFidget/ReviewFace";
    const string ShaderPath = "Assets/Editor/Review/ReviewFace.shader";
    const string ModelsFolder = "Assets/Art/Models";
    const string PrefabsFolder = "Assets/AssetsPrefabs";
    // The mat (BM_RepairMat.fbx) lives beside the stage's manifest, not with the tools (ForgivingBenchSteps.MatPath), and
    // Model sheets 1 is meant to include it: so its folder is read too.
    const string StageModels = "Assets/Art/Models/BenchStage";

    const int ViewWidth = 640, ViewHeight = 480;    // one view of a sheet; a sheet is 4 × 2 of them (2560 × 960)
    const int FaceSize = 160;                       // the face test's renders
    const int FlagSize = 320;                       // a flagged part's picture: the real view beside the face view
    const int Separator = 4;
    const int MinPixels = 30;                       // a direction counts when this many pixels (of 160 × 160) show the part
    const float SeeThrough = .25f, Severe = .5f;    // back share: flagged from a quarter, S1 from a half
    const int GroupCap = 400;                       // a scene group over this many renderers is split by its direct children
    const float SheetFov = 30f, SheetMargin = 1.12f;
    const float Golden = .6180339887f;              // the x-ray's hue step: no two neighbours alike

    static readonly int CullId = Shader.PropertyToID("_Cull"), FrontId = Shader.PropertyToID("_Front"), BackId = Shader.PropertyToID("_Back");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface"), BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip"), CutoffId = Shader.PropertyToID("_Cutoff");
    static readonly Color32 Dark = new Color32(24, 24, 26, 255), Ink = new Color32(255, 255, 255, 255);

    // The sheet's eight views: where the camera stands (from the subject's middle) and which way is up in the picture.
    static readonly (string name, Vector3 dir, Vector3 up)[] SheetViews =
    {
        ("+X", Vector3.right, Vector3.up), ("-X", Vector3.left, Vector3.up), ("+Z", Vector3.forward, Vector3.up), ("-Z", Vector3.back, Vector3.up),
        ("TOP", Vector3.up, Vector3.forward), ("BOTTOM", Vector3.down, Vector3.forward),
        ("3/4 +X+Y+Z", Corner(1f, 1f, 1f), Vector3.up), ("3/4 -X+Y-Z", Corner(-1f, 1f, -1f), Vector3.up),
    };

    // The face test's 14 directions: the six axes and the eight corners of the cube.
    static readonly (string name, Vector3 dir)[] Directions =
    {
        ("+X", Vector3.right), ("-X", Vector3.left), ("TOP (+Y)", Vector3.up), ("BOTTOM (-Y)", Vector3.down), ("+Z", Vector3.forward), ("-Z", Vector3.back),
        ("+X+Y+Z", Corner(1f, 1f, 1f)), ("+X+Y-Z", Corner(1f, 1f, -1f)), ("-X+Y+Z", Corner(-1f, 1f, 1f)), ("-X+Y-Z", Corner(-1f, 1f, -1f)),
        ("+X-Y+Z", Corner(1f, -1f, 1f)), ("+X-Y-Z", Corner(1f, -1f, -1f)), ("-X-Y+Z", Corner(-1f, -1f, 1f)), ("-X-Y-Z", Corner(-1f, -1f, -1f)),
    };

    static Vector3 Corner(float x, float y, float z) => new Vector3(x, y, z).normalized;

    // ======================================================================= the menus

    [MenuItem(Menu1, false, 10)] static void Sheets1() => Run("Model sheets 1", Menu1, Gather1);
    [MenuItem(Menu1, true)] static bool Can1() => !EditorApplication.isPlayingOrWillChangePlaymode;
    [MenuItem(Menu2, false, 11)] static void Sheets2() => Run("Model sheets 2", Menu2, Gather2);
    [MenuItem(Menu2, true)] static bool Can2() => !EditorApplication.isPlayingOrWillChangePlaymode;
    [MenuItem(Menu3, false, 12)] static void Sheets3() => Run("Model sheets 3", Menu3, GatherSelected);
    [MenuItem(Menu3, true)] static bool Can3() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.objects.Any(o => o is GameObject g && EditorUtility.IsPersistent(g));
    [MenuItem(Menu4, false, 13)] static void Sheets4() => Run("Model sheets 4", Menu4, GatherScene);
    [MenuItem(Menu4, true)] static bool Can4() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string name, string menu, Func<List<string>, List<Subject>> gather)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + name + " runs in Edit mode only (it is read-only): stop Play mode first."); return; }
        var notes = new List<string>();
        List<Subject> subjects;
        try
        {
            EditorUtility.DisplayProgressBar(Title, "Gathering the subjects...", 0f);
            subjects = gather(notes);
        }
        catch (Exception e) { Debug.LogError(Tag + name + ": couldn't gather the subjects: " + e.Message + "\n" + e); return; }
        finally { EditorUtility.ClearProgressBar(); }
        if (subjects.Count == 0)
        {
            Debug.LogWarning(Tag + name + ": nothing to review." + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
            return;
        }
        var report = new Report(name, menu);
        report.notes.AddRange(notes);
        var c = new Context { report = report, count = subjects.Count };
        try
        {
            c.face = Face.Make();
            for (int i = 0; i < subjects.Count; i++)
            {
                c.index = i;
                Subject s = subjects[i];
                report.subjects.Add(s);
                s.file = report.Unique(Safe(s.label));
                c.Progress(Inv($"{i + 1}/{subjects.Count}: {s.label}"), 0f);
                // A Studio per subject, as the other photo steps do: its preview scene is closed with the subject in it,
                // so nothing placed is ever destroyed twice and nothing of one subject can show in the next.
                c.studio = new BenchScale.Studio();
                try
                {
                    c.studio.Utility.camera.cullingMask = -1;   // every layer: a part is a part whatever layer it was put on
                    ReviewSubject(s, c);
                }
                finally
                {
                    c.studio.Dispose();
                    c.studio = null;
                }
            }
        }
        catch (OperationCanceledException) { report.cancelled = true; }
        catch (Exception e) { report.error = e.ToString(); }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (c.studio != null) c.studio.Dispose();
            if (c.face != null) c.face.Dispose();
            report.Write();
        }
    }

    // ======================================================================= the subjects

    sealed class Subject
    {
        public string label, file, source;
        public GameObject asset;                     // Model sheets 1-3: the prefab or model
        public GameObject sceneRoot;                 // Model sheets 4: the group's root in the open scene
        public HashSet<Renderer> sceneRenderers;     // Model sheets 4: the group's own renderers (a nested prefab's belong to its own group)
        public bool fromScene;
        public readonly List<Part> parts = new List<Part>();
        public readonly List<Flag> flags = new List<Flag>();
        public readonly List<string> notes = new List<string>();
        public readonly Dictionary<string, int> skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        public Vector3 sheetCentre;                  // the middle of what is drawn of it; radius: the whole subject (every part with a mesh)
        public float radius, sheetRadius;
        public string sheet, xray;                   // the files, once made
    }

    static List<Subject> Gather1(List<string> notes)
    {
        var list = new List<Subject>();
        foreach (BenchScale.Device d in BenchScale.Devices) AddAsset(list, d.prefab, notes, "BenchScale.Devices: " + d.label);
        foreach (string folder in new[] { BlenderKitSteps.ToolModels, StageModels, BlenderKitSteps.DeviceModels })
            foreach (string path in AssetsIn(folder, false, ".fbx")) AddAsset(list, path, notes, folder);
        return list;
    }

    static List<Subject> Gather2(List<string> notes)
    {
        var list = new List<Subject>();
        foreach (string path in AssetsIn(ModelsFolder, true, ".fbx")) AddAsset(list, path, notes, ModelsFolder);
        foreach (string path in AssetsIn(PrefabsFolder, true, ".prefab")) AddAsset(list, path, notes, PrefabsFolder);
        return list;
    }

    static List<Subject> GatherSelected(List<string> notes)
    {
        var list = new List<Subject>();
        foreach (Object o in Selection.objects)
        {
            if (!(o is GameObject go)) { notes.Add((o != null ? o.name : "(null)") + ": not a GameObject; skipped."); continue; }
            if (!EditorUtility.IsPersistent(go)) { notes.Add(go.name + ": a scene object, not an asset (Model sheets 4 reviews the open scene); skipped."); continue; }
            // A child of a model or prefab selected in the Project window: its asset is reviewed whole.
            AddAsset(list, AssetDatabase.GetAssetPath(go), notes, "selected");
        }
        return list;
    }

    static void AddAsset(List<Subject> list, string path, List<string> notes, string why)
    {
        if (string.IsNullOrEmpty(path) || list.Any(s => s.source == path)) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { notes.Add(path + ": not a prefab or model in the project (" + why + "); skipped."); return; }
        list.Add(new Subject { asset = asset, source = path, label = Path.GetFileNameWithoutExtension(path) });
    }

    /// <summary>The assets with <paramref name="extension"/> in a folder (all of its subfolders too when recursive), sorted.</summary>
    static List<string> AssetsIn(string folder, bool recursive, string extension)
    {
        var paths = new List<string>();
        if (!AssetDatabase.IsValidFolder(folder)) return paths;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            if (!recursive && (Path.GetDirectoryName(path) ?? "").Replace('\\', '/') != folder) continue;
            if (!paths.Contains(path)) paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    // ---- the open scene, in groups: a prefab instance is one group (one per prefab asset is tested), a plain object with
    // ---- its plain descendants another; anything over the cap is split by its direct children.

    sealed class Group { public Transform root; public List<Renderer> renderers; public string prefabPath, key; }

    static List<Subject> GatherScene(List<string> notes)
    {
        var list = new List<Subject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            var groups = new List<Group>();
            foreach (GameObject root in scene.GetRootGameObjects()) Gather(root.transform, false, groups, null, null);
            int added = 0;
            foreach (Group g in groups)
            {
                if (g.key != null && !seen.Add(g.key)) continue;   // one instance per prefab asset
                var s = new Subject
                {
                    sceneRoot = g.root.gameObject, sceneRenderers = new HashSet<Renderer>(g.renderers), fromScene = true, label = g.root.name,
                    source = "scene " + scene.name + ": " + BenchScale.PathIn(null, g.root) + (g.prefabPath != null ? " (prefab " + g.prefabPath + ")" : ""),
                };
                if (!g.root.gameObject.activeInHierarchy) s.notes.Add("switched off in the scene (reviewed as it would be when switched on)");
                list.Add(s);
                added++;
            }
            notes.Add(Inv($"scene {scene.name}: {added} groups from {groups.Count} ({groups.Count - added} more instances of the same prefabs skipped)"));
        }
        return list;
    }

    static bool IsOutermostPrefabRoot(GameObject go) =>
        PrefabUtility.IsAnyPrefabInstanceRoot(go) && !PrefabUtility.IsPrefabAssetMissing(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go;

    // The renderers of a subtree. Inside a prefab instance everything counts (a nested prefab is the same instance); in a
    // plain subtree a prefab instance's root is handed back as "nested" and left for its own group.
    static void Collect(Transform t, bool insidePrefab, List<Renderer> own, List<Transform> nested)
    {
        own.AddRange(t.GetComponents<Renderer>());
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (!insidePrefab && IsOutermostPrefabRoot(c.gameObject)) { nested.Add(c); continue; }
            Collect(c, insidePrefab, own, nested);
        }
    }

    static void Gather(Transform t, bool insidePrefab, List<Group> groups, string prefabPath, string key)
    {
        if ((t.gameObject.hideFlags & HideFlags.DontSave) != 0) return;   // an editor helper, not the scene's
        if (!insidePrefab && IsOutermostPrefabRoot(t.gameObject))
        {
            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            key = prefabPath;
            insidePrefab = true;
        }
        var own = new List<Renderer>();
        var nested = new List<Transform>();
        Collect(t, insidePrefab, own, nested);
        if (own.Count > GroupCap)
        {
            // Too big for one subject (the whole café would be thousands of renders): what sits on this object is one small
            // group, and each child is gathered on its own (a prefab child becomes its own group there).
            var self = new List<Renderer>(t.GetComponents<Renderer>());
            if (self.Count > 0) groups.Add(new Group { root = t, renderers = self, prefabPath = prefabPath, key = key });
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                Gather(c, insidePrefab, groups, prefabPath, key != null ? key + "/" + c.name : null);
            }
            return;
        }
        if (own.Count > 0) groups.Add(new Group { root = t, renderers = own, prefabPath = prefabPath, key = key });
        foreach (Transform n in nested) Gather(n, false, groups, null, null);
    }

    // ======================================================================= the parts

    sealed class Part
    {
        public Renderer renderer; public Mesh mesh; public string path, meshName;
        public bool active, enabled, mirrored, lodHidden;   // active: every object from it up to the root is on, as authored
        public bool clear;                                  // every material a URP Transparent surface: a player sees through it
        public int lod, tris;
        public Bounds bounds; public float radius; public Vector3 lossyScale;
        public Material[] materials; public ShadowCastingMode shadows;
        public Material[] targetSlots, occluderSlots;
        public readonly List<DirResult> directions = new List<DirResult>();
        public DirResult worst; public bool neverSeen; public string faceNote;
        public readonly List<Flag> flags = new List<Flag>();
        public Color xray; public bool drawnInXray;
        /// <summary>What a player sees of it as authored: on, its renderer on, a mesh to draw, and not a far LOD.</summary>
        public bool Drawn => active && enabled && mesh != null && !lodHidden;
    }

    sealed class DirResult
    {
        public string dir; public Vector3 vector; public int white, red;
        public int Counted => white + red;
        public float Share => Counted > 0 ? (float)red / Counted : 0f;
    }

    sealed class Flag
    {
        public int severity; public string subject, part, kind, detail, from = "", image = "";
        public readonly List<DirResult> directions = new List<DirResult>();
    }

    /// <summary>A renderer that is one of the model's own parts: a mesh (or skinned mesh), not a text, sprite, line, trail,
    /// particle, UI element or the job tag (DeviceBounds.Counts' rule: those float about a thing, they aren't it).</summary>
    static bool IsPart(Renderer r, out Mesh mesh, out string why)
    {
        mesh = null;
        why = null;
        if (r.transform is RectTransform) { why = "UI"; return false; }
        if (r.GetComponent<TMPro.TMP_Text>() != null) { why = "text"; return false; }
        if (r.GetComponentInParent<JobMarker>(true) != null) { why = "job tag"; return false; }
        if (r is SkinnedMeshRenderer skinned) { mesh = skinned.sharedMesh; return true; }
        if (r is MeshRenderer)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            mesh = mf != null ? mf.sharedMesh : null;
            return true;
        }
        why = r.GetType().Name;   // a sprite, a line, a trail, particles: named as what it is
        return false;
    }

    static Part MakePart(Renderer r, Mesh mesh, Transform root, Face face)
    {
        var p = new Part
        {
            renderer = r, mesh = mesh, path = PathOf(root, r.transform), meshName = mesh != null ? mesh.name : "(none)",
            active = ActiveWithin(root, r.transform), enabled = r.enabled,
            tris = Triangles(mesh), materials = r.sharedMaterials, shadows = r.shadowCastingMode,
            lossyScale = r.transform.lossyScale,
            // An odd number of negative axes turns the mesh inside out; Unity flips the winding back for a mirrored
            // transform, so it draws right, but a Blender export of the same thing may not.
            mirrored = r.transform.localToWorldMatrix.determinant < 0f,
        };
        if (p.materials == null) p.materials = new Material[0];
        // Glass: a Transparent surface hides nothing in the game, so it hides nothing in the face test either (or the
        // watch's spring under its glass would count as never seen); it is still tested for its own faces.
        p.clear = p.materials.Length > 0 && p.materials.All(m => m != null && m.HasProperty(SurfaceId) && Mathf.RoundToInt(m.GetFloat(SurfaceId)) == 1);
        int slots = Mathf.Max(1, Mathf.Max(mesh != null ? mesh.subMeshCount : 0, p.materials.Length));
        p.targetSlots = Enumerable.Repeat(face.target, slots).ToArray();
        p.occluderSlots = Enumerable.Repeat(face.occluder, slots).ToArray();
        return p;
    }

    static string PathOf(Transform root, Transform t)
    {
        string path = BenchScale.PathIn(root, t);
        return path.Length > 0 ? path : "(root)";
    }

    static bool ActiveWithin(Transform root, Transform t)
    {
        for (Transform x = t; x != null && x != root; x = x.parent) if (!x.gameObject.activeSelf) return false;
        return true;
    }

    /// <summary>Switches on every object from <paramref name="t"/> up to the root that is off; returns them, to switch back.</summary>
    static List<GameObject> Activate(Transform root, Transform t)
    {
        var switched = new List<GameObject>();
        for (Transform x = t; x != null && x != root; x = x.parent)
            if (!x.gameObject.activeSelf) { x.gameObject.SetActive(true); switched.Add(x.gameObject); }
        return switched;
    }

    // Index counts, not vertices: an imported mesh may not be readable, and the renders need no vertex access.
    static int Triangles(Mesh mesh)
    {
        if (mesh == null) return 0;
        long n = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            uint count = mesh.GetIndexCount(i);
            switch (mesh.GetTopology(i))
            {
                case MeshTopology.Triangles: n += count / 3; break;
                case MeshTopology.Quads: n += count / 2; break;
            }
        }
        return (int)Math.Min(n, int.MaxValue);
    }

    /// <summary>The mesh's box carried into the world by its transform (8 corners): a switched-off renderer has no bounds of its own.</summary>
    static Bounds WorldBounds(Mesh mesh, Transform t)
    {
        Bounds b = mesh.bounds;
        Matrix4x4 m = t.localToWorldMatrix;
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    static void Measure(Subject s)
    {
        foreach (Part p in s.parts)
        {
            if (p.mesh == null) { p.bounds = new Bounds(p.renderer.transform.position, Vector3.zero); p.radius = 0f; continue; }
            p.bounds = WorldBounds(p.mesh, p.renderer.transform);
            p.radius = Mathf.Max(p.bounds.extents.magnitude, 1e-4f);
        }
    }

    static bool Union(IEnumerable<Part> parts, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Part p in parts)
        {
            if (!any) { bounds = p.bounds; any = true; } else bounds.Encapsulate(p.bounds);
        }
        return any;
    }

    // ======================================================================= one subject

    sealed class Context
    {
        public Report report; public BenchScale.Studio studio; public Face face; public int index, count;

        public void Progress(string info, float within)
        {
            float p = (index + Mathf.Clamp01(within)) / Mathf.Max(1, count);
            if (EditorUtility.DisplayCancelableProgressBar(Title, info, p)) throw new OperationCanceledException();
        }
    }

    static void ReviewSubject(Subject s, Context c)
    {
        GameObject instance = s.fromScene ? Copy(s.sceneRoot, c.studio, s) : c.studio.Place(s.asset);
        if (instance == null) { s.notes.Add("couldn't be placed in the preview scene"); return; }
        var xrayMaterials = new List<Material>();
        try
        {
            Transform root = instance.transform;
            if (!instance.activeSelf) { instance.SetActive(true); s.notes.Add("its root is switched off as authored: switched on for the review"); }
            // A camera or listener inside a prefab would render or listen on its own in the preview scene, and a canvas (a
            // patience bar, a bubble) draws through CanvasRenderers, which no renderer list sees: its white would be counted
            // as front faces. Off, all of them: they're not the model.
            foreach (Camera cam in instance.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (AudioListener ear in instance.GetComponentsInChildren<AudioListener>(true)) ear.enabled = false;
            foreach (Canvas ui in instance.GetComponentsInChildren<Canvas>(true)) ui.enabled = false;

            // ---- the parts: every renderer, on or off, and what is left out ----
            Renderer[] all = instance.GetComponentsInChildren<Renderer>(true);
            HashSet<Renderer> mine = s.fromScene ? MapGroup(s, all) : null;
            var others = new List<(Renderer r, bool enabled)>();
            foreach (Renderer r in all)
            {
                Mesh mesh = null;
                string why = mine != null && !mine.Contains(r) ? "another group's (reviewed with its own)" : null;
                if (why == null && IsPart(r, out mesh, out why)) { s.parts.Add(MakePart(r, mesh, root, c.face)); continue; }
                others.Add((r, r.enabled));
                s.skipped[why] = s.skipped.TryGetValue(why, out int n) ? n + 1 : 1;
            }
            // LOD groups: the review is of LOD 0, what the player sees up close; a renderer only in a farther level isn't drawn.
            foreach (LODGroup group in instance.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                if (lods == null || lods.Length == 0) continue;
                group.ForceLOD(0);
                var first = new HashSet<Renderer>((lods[0].renderers ?? new Renderer[0]).Where(r => r != null));
                for (int level = 1; level < lods.Length; level++)
                    foreach (Renderer r in lods[level].renderers ?? new Renderer[0])
                    {
                        if (r == null || first.Contains(r)) continue;
                        Part p = s.parts.FirstOrDefault(x => x.renderer == r);
                        if (p != null && !p.lodHidden) { p.lodHidden = true; p.lod = level; }
                    }
            }
            if (s.parts.Count == 0) s.notes.Add("no mesh renderers in it: nothing to draw or test");

            // ---- its place: the lowest point of what's drawn on y = 0, its middle on the origin ----
            Measure(s);
            bool anyMesh = Union(s.parts.Where(p => p.mesh != null), out Bounds whole);
            if (!anyMesh) whole = new Bounds(root.position, Vector3.zero);
            if (!Union(s.parts.Where(p => p.Drawn), out Bounds shown)) shown = whole;
            var delta = new Vector3(shown.center.x, shown.min.y, shown.center.z);
            root.position -= delta;
            Measure(s);
            shown.center -= delta;
            whole.center -= delta;
            s.radius = Mathf.Max(whole.extents.magnitude, .001f);
            s.sheetCentre = shown.center;
            s.sheetRadius = Mathf.Max(shown.extents.magnitude, .001f);

            // ---- the checks that need no render ----
            foreach (Part p in s.parts) { CheckMaterials(p, s, c.report); CheckRenderer(p, s, c.report); }

            // ---- the face test: every part alone in white and red, the others black in front of it, from 14 directions ----
            foreach (var (r, _) in others) r.enabled = false;        // a text, a sprite, a ring: not the model, and not black
            foreach (Part p in s.parts) SetOccluder(p);
            for (int i = 0; i < s.parts.Count; i++)
            {
                Part p = s.parts[i];
                c.Progress(Inv($"{c.index + 1}/{c.count} {s.label} - face test {i + 1}/{s.parts.Count}: {p.path}"), .05f + .75f * i / Mathf.Max(1, s.parts.Count));
                if (p.mesh == null || p.tris == 0) { p.faceNote = "no mesh to test"; continue; }
                if (p.lodHidden) { p.faceNote = Inv($"LOD {p.lod}: not drawn at LOD 0, not tested"); continue; }
                if (p.shadows == ShadowCastingMode.ShadowsOnly) { p.faceNote = "shadows only: nothing to see"; continue; }
                List<GameObject> switchedOn = Activate(root, p.renderer.transform);
                p.renderer.enabled = true;
                p.renderer.sharedMaterials = p.targetSlots;
                FaceTest(p, s, c);
                SetOccluder(p);
                foreach (GameObject g in switchedOn) g.SetActive(false);
            }
            foreach (Part p in s.parts) RestoreAuthored(p);
            foreach (var (r, enabled) in others) r.enabled = enabled;

            // ---- the sheets (a scene group only when something in it is flagged: the scene is slow enough as it is) ----
            if (anyMesh && (!s.fromScene || s.flags.Count > 0))
            {
                c.Progress(Inv($"{c.index + 1}/{c.count} {s.label} - the sheets"), .82f);
                s.sheet = Sheet(s, c, false);
                foreach (var (r, _) in others) r.enabled = false;
                for (int i = 0; i < s.parts.Count; i++)
                {
                    Part p = s.parts[i];
                    p.xray = Color.HSVToRGB(Mathf.Repeat(i * Golden, 1f), .65f, .95f);
                    if (!p.Drawn || p.clear) continue;   // the x-ray is of the solids; a glass drawn solid would hide them
                    Material m = c.face.Flat(CullMode.Back, p.xray, p.xray, false);
                    xrayMaterials.Add(m);
                    p.renderer.sharedMaterials = Enumerable.Repeat(m, p.targetSlots.Length).ToArray();
                    p.drawnInXray = true;
                }
                s.xray = Sheet(s, c, true);
                foreach (Part p in s.parts) RestoreAuthored(p);
                foreach (var (r, enabled) in others) r.enabled = enabled;
            }

            // ---- a picture for every flagged part: the real view beside the face view, from the worst direction ----
            int pictures = 0;
            foreach (Part p in s.parts)
            {
                if (p.mesh == null || p.worst == null) continue;
                if (!p.flags.Any(f => f.severity <= 2 || f.directions.Count > 0)) continue;
                c.Progress(Inv($"{c.index + 1}/{c.count} {s.label} - flag picture: {p.path}"), .9f);
                FlagImage(p, s, c, root, others);
                pictures++;
            }
            if (pictures > 0) s.notes.Add(Inv($"{pictures} flag picture(s)"));
        }
        finally
        {
            // The instance goes with the Studio's preview scene; the x-ray's materials are this method's own.
            foreach (Material m in xrayMaterials) if (m != null) Object.DestroyImmediate(m);
        }
    }

    static void SetOccluder(Part p)
    {
        p.renderer.enabled = p.Drawn && !p.clear;
        if (p.mesh != null) p.renderer.sharedMaterials = p.occluderSlots;
    }

    static void RestoreAuthored(Part p)
    {
        p.renderer.sharedMaterials = p.materials;
        p.renderer.enabled = p.enabled;
    }

    /// <summary>A scene group's copy: the preview scene's own, never the open scene's. Unity 6 instantiates straight into
    /// a scene; the copy is given the original's world pose and scale (a clone of a child would keep only its local ones).</summary>
    static GameObject Copy(GameObject original, BenchScale.Studio studio, Subject s)
    {
        Scene preview = studio.Utility.lights[0].gameObject.scene;   // the Studio's lamp lives in the preview scene
        GameObject copy = null;
#if UNITY_6000_0_OR_NEWER
        try { copy = Object.Instantiate(original, new InstantiateParameters { scene = preview, worldSpace = true }); }
        catch (Exception e) { s.notes.Add("instantiating into the preview scene failed (" + e.Message + "): copied and moved instead"); }
#endif
        if (copy == null) copy = Object.Instantiate(original);
        if (copy.scene != preview) SceneManager.MoveGameObjectToScene(copy, preview);
        Transform o = original.transform, t = copy.transform;
        t.SetPositionAndRotation(o.position, o.rotation);
        t.localScale = o.lossyScale;
        copy.name = original.name;
        // The copy's scripts have had their Awake and OnEnable; from here nothing in the preview scene should keep acting
        // (a Cinemachine camera registering itself, a lab ticking): off, every one. Rendering needs none of them.
        foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) b.enabled = false;
        return copy;
    }

    /// <summary>The copy's renderers that are this group's: the copy keeps the original's order, so they match by index.</summary>
    static HashSet<Renderer> MapGroup(Subject s, Renderer[] copy)
    {
        Renderer[] original = s.sceneRoot.GetComponentsInChildren<Renderer>(true);
        if (original.Length != copy.Length)
        {
            s.notes.Add(Inv($"the copy has {copy.Length} renderers, the original {original.Length} (a script made or removed some): every renderer of the copy is reviewed"));
            return null;
        }
        var mine = new HashSet<Renderer>();
        for (int i = 0; i < copy.Length; i++) if (s.sceneRenderers.Contains(original[i])) mine.Add(copy[i]);
        return mine;
    }

    // ======================================================================= the checks

    // A part whose mesh and material a script makes while playing (20 Oct: the watch's crystal smudge, a GrimeSpot, lays
    // its dirt patch in Awake). In the editor it has no mesh and an empty slot by design; the game draws it. The first run
    // called that two S1s; a review that cries wolf stops being read, so it is noted (S3) and the bench labs check it.
    static bool BuiltWhilePlaying(Part p) => p.renderer != null && p.renderer.GetComponent<GrimeSpot>() != null;

    static void CheckMaterials(Part p, Subject s, Report rep)
    {
        Material[] mats = p.materials;
        int subs = p.mesh != null ? p.mesh.subMeshCount : 0;
        if (p.mesh == null && BuiltWhilePlaying(p)) return;   // CheckRenderer notes it
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] == null) rep.Raise(s, p, 1, "EMPTY MATERIAL SLOT", Inv($"material slot {i + 1} of {mats.Length} is empty: Unity draws that submesh in magenta (or not at all)"));
        if (p.mesh != null && mats.Length < subs)
            rep.Raise(s, p, 2, "FEWER MATERIALS THAN SUBMESHES", Inv($"{mats.Length} material(s) for {subs} submeshes: the submeshes past the last slot have no material of their own"));
        foreach (Material m in mats.Where(x => x != null).Distinct())
        {
            Shader sh = m.shader;
            if (sh == null || !sh.isSupported || sh.name.Contains("InternalErrorShader") || ShaderUtil.ShaderHasError(sh))
            {
                rep.Raise(s, p, 1, "BROKEN SHADER", "material " + m.name + ": shader " + (sh != null ? sh.name : "(none)") + " is missing, unsupported or failed to compile: it draws magenta");
                continue;
            }
            // URP's Lit family: Surface Type Transparent with a faded base colour is see-through (and writes no depth).
            if (m.HasProperty(SurfaceId) && Mathf.RoundToInt(m.GetFloat(SurfaceId)) == 1)
            {
                float a = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId).a : m.HasProperty(ColorId) ? m.GetColor(ColorId).a : 1f;
                if (a < .98f) rep.Raise(s, p, 2, "SEE-THROUGH MATERIAL", Inv($"material {m.name}: Transparent surface with base colour alpha {a:0.00}: the part is see-through"));
            }
            if (m.HasProperty(AlphaClipId) && m.GetFloat(AlphaClipId) > .5f && m.HasProperty(CutoffId) && m.GetFloat(CutoffId) > .5f)
                rep.Raise(s, p, 3, "ALPHA CLIP", Inv($"material {m.name}: alpha clipping with threshold {m.GetFloat(CutoffId):0.00}: anything fainter than that in the texture's alpha is cut away"));
            if (m.HasProperty(CullId))
            {
                // URP's Render Face: Front is Cull Back (2, normal), Back is Cull Front (1: the inside only), Both is Cull Off (0).
                int cull = Mathf.RoundToInt(m.GetFloat(CullId));
                if (cull == (int)CullMode.Off)
                    rep.Raise(s, p, 3, "RENDER FACE BOTH", "material " + m.name + ": Render Face Both (Cull Off): both sides drawn, so an inside-out face looks right with this material and wrong with any other; twice the fill");
                else if (cull == (int)CullMode.Front)
                    rep.Raise(s, p, 2, "RENDER FACE BACK", "material " + m.name + ": Render Face Back (Cull Front): Unity draws only the back of its faces, so it looks inside-out");
            }
        }
    }

    static void CheckRenderer(Part p, Subject s, Report rep)
    {
        if (p.mesh == null && BuiltWhilePlaying(p))
            rep.Raise(s, p, 3, "BUILT WHILE PLAYING", "a GrimeSpot: its dirt patch (mesh and material) is made when the game starts, so the editor shows none; the bench labs see it drawn");
        else if (p.mesh == null) rep.Raise(s, p, 1, "NO MESH", "no mesh on it (the MeshFilter or SkinnedMeshRenderer is empty): nothing is drawn");
        else if (p.tris == 0) rep.Raise(s, p, 1, "NO TRIANGLES", "mesh " + p.meshName + " has 0 triangles: nothing is drawn");
        else
        {
            Vector3 size = p.bounds.size;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < .0002f) rep.Raise(s, p, 3, "TINY", BenchScale.MmV(size) + ": under 0.2 mm across, too small to ever see");
        }
        if (p.shadows == ShadowCastingMode.ShadowsOnly && p.active && p.enabled)
            rep.Raise(s, p, 2, "SHADOWS ONLY", "Cast Shadows is Shadows Only: the player sees its shadow, never it");
        if (p.mirrored)
            rep.Raise(s, p, 3, "MIRRORED", "scale " + BenchScale.V(p.lossyScale) + ": an odd number of negative axes, so it is mirrored; Unity flips its faces back for you, Blender exports may not");
    }

    // ======================================================================= the face test

    static void FaceTest(Part p, Subject s, Context c)
    {
        Camera cam = c.studio.Utility.camera;
        Color was = cam.backgroundColor;
        cam.backgroundColor = Color.black;   // black like the occluders: only the part's own white and red are counted
        try
        {
            foreach (var (name, dir) in Directions)
            {
                Texture2D shot = ShootPart(p, s, c, dir, FaceSize);
                Count(shot, out int white, out int red, out float scale);
                Object.DestroyImmediate(shot);
                p.directions.Add(new DirResult { dir = name, vector = dir, white = Mathf.RoundToInt(white / scale), red = Mathf.RoundToInt(red / scale) });
            }
        }
        finally { cam.backgroundColor = was; }
        List<DirResult> seen = p.directions.Where(d => d.Counted >= MinPixels).ToList();
        if (seen.Count == 0)
        {
            p.neverSeen = true;
            p.worst = p.directions.OrderByDescending(d => d.Counted).First();
            Flag never = c.report.Raise(s, p, 3, "NEVER SEEN",
                "from no direction do 30 pixels of it show: hidden behind the other parts as authored, or too small. Fine if it only shows once something is taken off; otherwise the purpose rule asks what it is for",
                "nowhere");
            never.directions.Add(p.worst);
            return;
        }
        p.worst = seen.OrderByDescending(d => d.Share).First();
        if (p.worst.Share < SeeThrough) return;
        List<DirResult> over = seen.Where(d => d.Share >= SeeThrough).OrderByDescending(d => d.Share).ToList();
        string also = over.Count > 1 ? " (also " + string.Join(", ", over.Skip(1).Select(d => Inv($"from {d.dir} {d.Share * 100f:0}%"))) + ")" : "";
        // A material that draws both sides (Render Face Both) shows the back faces in the game too: the flip is then a
        // choice to look at, not a hole, so it is told apart from the strap's case.
        bool bothSides = p.materials.Length > 0 && p.materials.All(m => m != null && m.HasProperty(CullId) && Mathf.RoundToInt(m.GetFloat(CullId)) == (int)CullMode.Off);
        Flag flag = bothSides
            ? c.report.Raise(s, p, 3, "BACK FACES, DRAWN BOTH SIDES",
                Inv($"from {p.worst.dir}: {p.worst.Share * 100f:0}% of what you'd see of it is the back of its faces, but its material draws both sides (Render Face Both), so the game shows it; fine if that is meant{also}"), p.worst.dir)
            : c.report.Raise(s, p, p.worst.Share >= Severe ? 1 : 2, "SEE-THROUGH",
                Inv($"from {p.worst.dir}: {p.worst.Share * 100f:0}% of what you'd see of it is the back of its faces - the game draws front faces only, so from that side it's see-through/invisible{also}"), p.worst.dir);
        flag.directions.AddRange(over);
    }

    /// <summary>The part framed from a direction: the camera outside the whole subject on the line from the part's middle,
    /// the field of view fitted to the part's sphere (so a screw fills the frame as the body does), near and far from the distances.</summary>
    static Texture2D ShootPart(Part p, Subject s, Context c, Vector3 dir, int size)
    {
        float d = s.radius * 2.2f + p.radius;
        float fov = Mathf.Clamp(2f * Mathf.Asin(Mathf.Clamp01(p.radius * 1.15f / d)) * Mathf.Rad2Deg, 1.5f, 60f);
        Camera cam = c.studio.Utility.camera;
        // The nearest any of the subject can come to the camera is 0.2 R + r; the farthest 4.2 R + r.
        cam.nearClipPlane = Mathf.Max(1e-4f, s.radius * .08f);
        cam.farClipPlane = d + s.radius * 2.5f + p.radius;
        Vector3 from = p.bounds.center + dir * d;
        Quaternion look = Quaternion.LookRotation(-dir, Mathf.Abs(dir.y) > .99f ? Vector3.forward : Vector3.up);
        return c.studio.Shoot(from, look, fov, size, size);
    }

    /// <summary>White (front) and red (back) pixels, anti-aliasing tolerant: red is r > .5 and g < .35, white r > .5 and g > .5;
    /// the rest is black or an edge. The texture may be the request × the screen's point scale: scale says how many times.</summary>
    static void Count(Texture2D tex, out int white, out int red, out float scale)
    {
        white = 0;
        red = 0;
        Color32[] px = tex.GetPixels32();
        foreach (Color32 p in px)
        {
            if (p.r < 128) continue;
            if (p.g < 90) red++;
            else if (p.g > 127) white++;
        }
        scale = Mathf.Max(1e-6f, px.Length / (float)(FaceSize * FaceSize));
    }

    // ======================================================================= the pictures

    static string Sheet(Subject s, Context c, bool xray)
    {
        Camera cam = c.studio.Utility.camera;
        Color was = cam.backgroundColor;
        if (xray) cam.backgroundColor = new Color(.03f, .03f, .04f);
        try
        {
            float dist = SheetMargin * s.sheetRadius / Mathf.Sin(SheetFov * .5f * Mathf.Deg2Rad);   // the sphere fits the height, 12 % spare
            cam.nearClipPlane = Mathf.Max(1e-4f, dist - s.sheetRadius * 1.5f);
            cam.farClipPlane = dist + s.sheetRadius * 1.5f;
            var canvas = new Picture(4 * ViewWidth, 2 * ViewHeight, Dark);
            for (int k = 0; k < SheetViews.Length; k++)
            {
                var (name, dir, up) = SheetViews[k];
                Texture2D shot = c.studio.Shoot(s.sheetCentre + dir * dist, Quaternion.LookRotation(-dir, up), SheetFov, ViewWidth, ViewHeight);
                int x = (k % 4) * ViewWidth, y = (k / 4) * ViewHeight;
                canvas.Blit(Fit(shot, ViewWidth, ViewHeight), ViewWidth, ViewHeight, x, y);
                Object.DestroyImmediate(shot);
                canvas.Label(name, x + 8, y + 8, 2);
            }
            for (int col = 1; col < 4; col++) canvas.Fill(col * ViewWidth - Separator / 2, 0, Separator, canvas.H, Dark);
            canvas.Fill(0, ViewHeight - Separator / 2, canvas.W, Separator, Dark);
            canvas.Label(s.label + (xray ? "  X-RAY: EACH PART ITS OWN COLOUR, BACK FACES CULLED AS THE GAME DOES" : "  REAL MATERIALS"), 8, 2 * ViewHeight - 24, 2);
            string file = s.file + (xray ? "-xray.png" : "-sheet.png");
            canvas.Save(Path.Combine(c.report.folder, file));
            return file;
        }
        finally { cam.backgroundColor = was; }
    }

    static void FlagImage(Part p, Subject s, Context c, Transform root, List<(Renderer r, bool enabled)> others)
    {
        Camera cam = c.studio.Utility.camera;
        Color was = cam.backgroundColor;
        Texture2D real = null, faces = null;
        List<GameObject> switchedOn = null;
        try
        {
            // Left: as authored in its real materials, the part itself switched on so there is something to look at.
            foreach (var (r, _) in others) r.enabled = false;
            switchedOn = Activate(root, p.renderer.transform);
            p.renderer.enabled = true;
            real = ShootPart(p, s, c, p.worst.vector, FlagSize);
            // Right: the face test's own view.
            foreach (Part q in s.parts) SetOccluder(q);
            p.renderer.enabled = true;
            p.renderer.sharedMaterials = p.targetSlots;
            cam.backgroundColor = Color.black;
            faces = ShootPart(p, s, c, p.worst.vector, FlagSize);
            var canvas = new Picture(2 * FlagSize + Separator, FlagSize, Dark);
            canvas.Blit(Fit(real, FlagSize, FlagSize), FlagSize, FlagSize, 0, 0);
            canvas.Blit(Fit(faces, FlagSize, FlagSize), FlagSize, FlagSize, FlagSize + Separator, 0);
            canvas.Label("REAL  FROM " + p.worst.dir, 6, 6, 2);
            canvas.Label(p.neverSeen ? "FACES  NEVER SEEN" : Inv($"FACES  BACK {p.worst.Share * 100f:0}%"), FlagSize + Separator + 6, 6, 2);
            canvas.Label("WHITE FRONT  RED BACK  BLACK THE REST", FlagSize + Separator + 6, FlagSize - 16, 1);
            canvas.Label(p.path.Length > 50 ? p.path.Substring(0, 49) + "." : p.path, 6, FlagSize - 16, 1);
            string file = c.report.Unique("flag-" + s.file + "-" + Safe(p.path)) + ".png";
            canvas.Save(Path.Combine(c.report.folder, file));
            foreach (Flag f in p.flags) f.image = file;
        }
        finally
        {
            cam.backgroundColor = was;
            foreach (Part q in s.parts) RestoreAuthored(q);
            if (switchedOn != null) foreach (GameObject g in switchedOn) g.SetActive(false);
            foreach (var (r, enabled) in others) r.enabled = enabled;
            if (real != null) Object.DestroyImmediate(real);
            if (faces != null) Object.DestroyImmediate(faces);
        }
    }

    /// <summary>The texture at a size of our own (the preview may come back larger on a Retina screen): a box average per pixel.</summary>
    static Color32[] Fit(Texture2D src, int w, int h)
    {
        Color32[] s = src.GetPixels32();
        int sw = src.width, sh = src.height;
        var d = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            int y0 = y * sh / h, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / h);
            for (int x = 0; x < w; x++)
            {
                int x0 = x * sw / w, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / w);
                int r = 0, g = 0, b = 0, n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                    {
                        Color32 p = s[yy * sw + xx];
                        r += p.r; g += p.g; b += p.b; n++;
                    }
                d[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        }
        return d;
    }

    /// <summary>A picture drawn pixel by pixel. Coordinates are from the top left as on paper; Unity's rows run from the
    /// bottom, so they are turned over on the way in and out.</summary>
    sealed class Picture
    {
        public readonly int W, H;
        readonly Color32[] px;

        public Picture(int w, int h, Color32 fill)
        {
            W = w; H = h;
            px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            px[(H - 1 - y) * W + x] = c;
        }

        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, y + j, c);
        }

        /// <summary>A block of Unity pixels (rows from the bottom) with its top left at (x, y).</summary>
        public void Blit(Color32[] src, int sw, int sh, int x, int y)
        {
            for (int j = 0; j < sh; j++) for (int i = 0; i < sw; i++) Set(x + i, y + (sh - 1 - j), src[j * sw + i]);
        }

        public void Label(string text, int x, int y, int scale)
        {
            Fill(x - scale, y - scale, text.Length * 6 * scale + scale, 9 * scale, new Color32(0, 0, 0, 255));
            foreach (char ch in text.ToUpperInvariant())
            {
                if (Glyphs.TryGetValue(ch, out string rows))
                    for (int r = 0; r < 7; r++)
                    {
                        int bits = Convert.ToInt32(rows.Substring(r * 2, 2), 16);
                        for (int col = 0; col < 5; col++)
                            if ((bits & (16 >> col)) != 0) Fill(x + col * scale, y + r * scale, scale, scale, Ink);
                    }
                x += 6 * scale;
            }
        }

        public void Save(string path)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                BenchScale.SavePng(tex, path);
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }

    // A 5 × 7 pixel font for the labels (seven rows top to bottom, five bits each, the left pixel the highest bit): the
    // pictures are read on their own, away from the report, so each view says which it is.
    static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        ['A'] = "0E11111F111111", ['B'] = "1E11111E11111E", ['C'] = "0E11101010110E", ['D'] = "1E11111111111E", ['E'] = "1F10101E10101F",
        ['F'] = "1F10101E101010", ['G'] = "0E11101711110F", ['H'] = "1111111F111111", ['I'] = "0E04040404040E", ['J'] = "0702020202120C",
        ['K'] = "11121418141211", ['L'] = "1010101010101F", ['M'] = "111B1515111111", ['N'] = "11111915131111", ['O'] = "0E11111111110E",
        ['P'] = "1E11111E101010", ['Q'] = "0E11111115120D", ['R'] = "1E11111E141211", ['S'] = "0F10100E01011E", ['T'] = "1F040404040404",
        ['U'] = "1111111111110E", ['V'] = "11111111110A04", ['W'] = "1111111515150A", ['X'] = "11110A040A1111", ['Y'] = "1111110A040404",
        ['Z'] = "1F01020408101F", ['0'] = "0E11131519110E", ['1'] = "040C040404040E", ['2'] = "0E11010204081F", ['3'] = "1F02040201110E",
        ['4'] = "02060A121F0202", ['5'] = "1F101E0101110E", ['6'] = "0608101E11110E", ['7'] = "1F010204080808", ['8'] = "0E11110E11110E",
        ['9'] = "0E11110F01020C", ['+'] = "0004041F040400", ['-'] = "0000001F000000", ['/'] = "01010204081010", ['%'] = "18190204081303",
        ['.'] = "00000000000C0C", [':'] = "000C0C000C0C00", [' '] = "00000000000000", ['('] = "02040808080402", [')'] = "08040202020408",
        ['#'] = "0A0A1F0A1F0A0A", ['_'] = "0000000000001F", [','] = "000000000C0408",
    };

    // ======================================================================= the shader and its materials

    sealed class Face : IDisposable
    {
        public Shader shader;
        public Material target, occluder;    // white front / red back with no culling; black both ways, culled as the game does
        readonly List<Material> made = new List<Material>();

        public static Face Make()
        {
            Shader found = Shader.Find(ShaderName);
            if (found == null) found = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (found == null) throw new InvalidOperationException("the review's shader " + ShaderName + " isn't in the project: is " + ShaderPath + " there, and has Unity picked it up (Assets > Refresh)?");
            if (ShaderUtil.ShaderHasError(found) || !found.isSupported)
            {
                string why = string.Join("; ", ShaderUtil.GetShaderMessages(found).Select(m => m.message));
                throw new InvalidOperationException(ShaderName + " doesn't compile on this machine: " + why);
            }
            var face = new Face { shader = found };
            face.target = face.Flat(CullMode.Off, Color.white, Color.red, true);
            face.occluder = face.Flat(CullMode.Back, Color.black, Color.black, true);
            return face;
        }

        public Material Flat(CullMode cull, Color front, Color back, bool keep)
        {
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "ReviewFace " + cull };
            m.SetFloat(CullId, (float)cull);
            m.SetColor(FrontId, front);
            m.SetColor(BackId, back);
            if (keep) made.Add(m);
            return m;
        }

        public void Dispose()
        {
            foreach (Material m in made) if (m != null) Object.DestroyImmediate(m);
            made.Clear();
        }
    }

    // ======================================================================= the report

    sealed class Report
    {
        public readonly string name, menu, folder, relative;
        public readonly List<Subject> subjects = new List<Subject>();
        public readonly List<Flag> flags = new List<Flag>();
        public readonly List<string> notes = new List<string>();
        public bool cancelled;
        public string error;
        readonly HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "report", "flags" };

        public Report(string name, string menu)
        {
            this.name = name;
            this.menu = menu;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            relative = "Logs/Review/models-" + stamp;
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Review", "models-" + stamp));
            Directory.CreateDirectory(folder);
        }

        public string Unique(string stem)
        {
            string result = stem;
            for (int n = 2; !files.Add(result); n++) result = stem + "-" + n.ToString(CultureInfo.InvariantCulture);
            return result;
        }

        public Flag Raise(Subject s, Part p, int severity, string kind, string detail, string from = "")
        {
            var f = new Flag { severity = severity, subject = s.label, part = p.path, kind = kind, detail = detail, from = from };
            flags.Add(f);
            s.flags.Add(f);
            p.flags.Add(f);
            return f;
        }

        public void Write()
        {
            int parts = subjects.Sum(s => s.parts.Count);
            int s1 = flags.Count(f => f.severity == 1), s2 = flags.Count(f => f.severity == 2), s3 = flags.Count(f => f.severity == 3);
            var t = new StringBuilder();
            t.AppendLine(Title.ToUpperInvariant() + "   " + menu);
            t.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "   Unity " + Application.unityVersion + "   " + folder);
            t.AppendLine(Inv($"{subjects.Count} subjects, {parts} parts, {flags.Count} flags (S1 {s1}, S2 {s2}, S3 {s3})"));
            if (cancelled) t.AppendLine("CANCELLED: the report holds what was done up to then" + (subjects.Count > 0 ? " (during " + subjects[subjects.Count - 1].label + ")" : "") + ".");
            if (error != null) t.AppendLine("ERROR: the review stopped (see the end).");
            foreach (string n in notes) t.AppendLine("note: " + n);
            t.AppendLine();
            t.AppendLine("How to read it. Each subject stands in a preview scene the way Unity draws it, its lowest point on the ground.");
            t.AppendLine("  <subject>-sheet.png: eight views in its real materials. <subject>-xray.png: every drawn part in its own colour,");
            t.AppendLine("  back faces culled as the game does, so an inside-out part is simply missing. The face test: every part on its");
            t.AppendLine("  own from 14 directions, front faces white, back faces red, the other parts black in front of it; the back share");
            t.AppendLine("  is red / (red + white) where at least 30 pixels of it show (160 x 160 renders). SEE-THROUGH: a quarter or more");
            t.AppendLine("  of what you'd see is the back of its faces (S1 from a half). NEVER SEEN: it shows from no direction. A part whose");
            t.AppendLine("  materials are all URP Transparent is \"clear\": it hides nothing in the test and is left out of the x-ray.");
            t.AppendLine("  flag-<subject>-<part>.png: the real view beside the face view, from the worst direction. flags.json: the same flags.");
            t.AppendLine();
            t.AppendLine("FLAGS FIRST (S1: a player sees it wrong now; S2: likely wrong, look; S3: for information)");
            if (flags.Count == 0) t.AppendLine("   none");
            foreach (Flag f in flags.OrderBy(x => x.severity))
                t.AppendLine("S" + f.severity.ToString(CultureInfo.InvariantCulture) + "  " + f.subject + "  |  " + f.part + "  |  " + f.kind + "  |  " + f.detail
                             + (f.from.Length > 0 ? "  |  from " + f.from : "") + (f.image.Length > 0 ? "  |  " + f.image : ""));
            t.AppendLine();
            t.AppendLine("SUBJECTS");
            foreach (Subject s in subjects)
            {
                t.AppendLine();
                t.AppendLine("== " + s.label + "   " + s.source);
                int off = s.parts.Count(p => !p.active), disabled = s.parts.Count(p => p.active && !p.enabled);
                string skipped = s.skipped.Count > 0 ? "; skipped " + string.Join(", ", s.skipped.Select(kv => kv.Value.ToString(CultureInfo.InvariantCulture) + " " + kv.Key)) : "";
                t.AppendLine(Inv($"   {s.parts.Count} parts ({off} switched off as authored, {disabled} with the renderer off), {s.flags.Count} flags; its sphere r {s.radius * 1000f:0.0} mm{skipped}"));
                if (s.sheet != null) t.AppendLine("   sheet " + s.sheet + "   x-ray " + s.xray);
                foreach (string n in s.notes) t.AppendLine("   note: " + n);
                if (s.parts.Count == 0) continue;
                t.AppendLine("   " + Col("part", 44) + Col("tris", 8) + Col("size", 28) + Col("back", 6) + Col("from", 14) + Col("active/on", 12) + "materials (shader)");
                foreach (Part p in s.parts)
                {
                    string back = p.neverSeen ? "none" : p.worst != null ? Inv($"{p.worst.Share * 100f:0}%") : "-";
                    string from = p.neverSeen ? "never seen" : p.worst != null ? p.worst.dir : p.faceNote ?? "-";
                    string on = (p.active ? "on" : "OFF") + "/" + (p.enabled ? "on" : "OFF") + (p.lodHidden ? " LOD" + p.lod.ToString(CultureInfo.InvariantCulture) : "") + (p.clear ? " clear" : "");
                    string mats = string.Join(", ", p.materials.Select(m => m == null ? "(empty)" : m.name + " (" + (m.shader != null ? m.shader.name : "no shader") + ")"));
                    t.AppendLine("   " + Col(p.path, 44) + Col(p.tris.ToString(CultureInfo.InvariantCulture), 8) + Col(p.mesh != null ? BenchScale.MmV(p.bounds.size) : "-", 28)
                                 + Col(back, 6) + Col(from, 14) + Col(on, 12) + mats);
                }
                if (s.xray != null)
                    t.AppendLine("   x-ray: " + string.Join(", ", s.parts.Select(p => (p.drawnInXray ? "#" + ColorUtility.ToHtmlStringRGB(p.xray) : p.clear ? "clear, left out" : "not drawn") + " " + p.path)));
            }
            if (error != null) { t.AppendLine(); t.AppendLine("ERROR"); t.AppendLine(error); }
            File.WriteAllText(Path.Combine(folder, "report.txt"), t.ToString());
            File.WriteAllText(Path.Combine(folder, "flags.json"), FlagsJson());

            string line = Inv($"{Tag}{name}: {subjects.Count} subjects, {parts} parts, {flags.Count} flagged (S1 {s1}, S2 {s2}, S3 {s3}). {relative}")
                          + (cancelled ? "  (cancelled: the report holds what was done)" : "");
            if (error != null) Debug.LogError(line + "  ERROR, see report.txt:\n" + error);
            else if (s1 > 0) Debug.LogWarning(line);
            else Debug.Log(line);
        }

        string FlagsJson()
        {
            var j = new StringBuilder("[\n");
            List<Flag> sorted = flags.OrderBy(x => x.severity).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                Flag f = sorted[i];
                j.Append("  {\"severity\": \"S").Append(f.severity.ToString(CultureInfo.InvariantCulture)).Append("\", \"subject\": ").Append(Json(f.subject))
                 .Append(", \"part\": ").Append(Json(f.part)).Append(", \"kind\": ").Append(Json(f.kind)).Append(", \"detail\": ").Append(Json(f.detail))
                 .Append(", \"from\": ").Append(Json(f.from)).Append(", \"directions\": [");
                for (int k = 0; k < f.directions.Count; k++)
                {
                    DirResult d = f.directions[k];
                    if (k > 0) j.Append(", ");
                    j.Append("{\"dir\": ").Append(Json(d.dir)).Append(", \"backShare\": ").Append(d.Share.ToString("0.000", CultureInfo.InvariantCulture))
                     .Append(", \"pixels\": ").Append(d.Counted.ToString(CultureInfo.InvariantCulture)).Append('}');
                }
                j.Append("], \"image\": ").Append(Json(f.image)).Append('}').Append(i + 1 < sorted.Count ? ",\n" : "\n");
            }
            j.Append("]\n");
            return j.ToString();
        }
    }

    // ======================================================================= words

    static string Inv(FormattableString f) => FormattableString.Invariant(f);

    static string Col(string s, int width) => s.Length >= width ? s.Substring(0, width - 2) + ". " : s.PadRight(width);

    static string Json(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>A name as a file name: letters, digits, '-' and '.', the rest '_', at most 60 characters.</summary>
    static string Safe(string name)
    {
        var sb = new StringBuilder();
        foreach (char ch in name ?? "")
        {
            char c = char.IsLetterOrDigit(ch) || ch == '-' || ch == '.' ? ch : '_';
            if (c == '_' && sb.Length > 0 && sb[sb.Length - 1] == '_') continue;
            sb.Append(c);
        }
        string s = sb.ToString().Trim('_', '.');
        if (s.Length > 60) s = s.Substring(0, 60).TrimEnd('_', '.');
        return s.Length > 0 ? s : "unnamed";
    }
}
#endif
