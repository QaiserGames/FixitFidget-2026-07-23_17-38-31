using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// BARNABY, THE BLENDER MODEL (playtest 2, step 4; claude/playtest-2-plan.md)
//
//   Fixit Fidget > Night > Night 1 - Barnaby: use the Blender model (the prefab only)
//   Fixit Fidget > Night > Night 1 - Barnaby: back to simple shapes (the prefab only)
//
// Grace's gnome on her step and his copy on Ace's shelf are two instances of one prefab,
// "Night 1 - Barnaby the garden gnome.prefab". These steps change what is inside that prefab and nothing
// else: the scene file isn't touched, and both instances follow. Both looks stay in the prefab, one on:
//   * "Model": the FBX from Blender, Assets/Art/Models/Night/Barnaby.fbx. Its source is
//     BlenderSource/Night1_Barnaby.blend, built by Tools/Blender/barnaby.py (Blender 5.2). Its
//     materials are the placeholder's own ("Night 1 - hat" and so on, plus moss for his stone): the
//     Blender materials have the same names, and the importer maps each to its Unity material.
//   * "Simple shapes (placeholder)": the parts NightOneSteps builds.
// The prefab is opened with LoadPrefabContents and saved in place, so its root keeps its identity and the
// instances keep their overrides (the shelf copy: where it sits, which way it faces, hidden until taken).
// "Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene" builds the prefab the same way, with
// the model on when the FBX is there.
// Report and photos: Logs/Night/barnaby-model-<time>/.
internal static class BarnabyModelSteps
{
    const string Tag = "[Night 1] ";
    const string Menu = "Fixit Fidget/Night/";
    internal const string ModelPath = "Assets/Art/Models/Night/Barnaby.fbx";
    internal const string ModelChild = "Model";
    internal const string ShapesChild = "Simple shapes (placeholder)";

    // What the Night 1 set-up checked on Grace's top step: no wider than 0.24 m, no taller than 0.56 m.
    const float MaxWidth = .24f, MaxHeight = .56f;

    [MenuItem(Menu + "Night 1 - Barnaby: use the Blender model (the prefab only)")]
    static void UseModel() => Switch(true);

    [MenuItem(Menu + "Night 1 - Barnaby: back to simple shapes (the prefab only)")]
    static void UseShapes() => Switch(false);

    static void Switch(bool useModel)
    {
        var report = new StringBuilder("Night 1 - Barnaby: " + (useModel ? "the Blender model" : "simple shapes") + " (the prefab only)\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        string folder = LogFolder("barnaby-model");
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NightOneSteps.PrefabPath) == null)
                throw new InvalidOperationException("There is no Barnaby prefab yet (" + NightOneSteps.PrefabPath + "): run \"Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene\" first.");
            if (useModel && AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
                throw new InvalidOperationException("The model isn't in the project: " + ModelPath);

            Dictionary<string, Material> colours = NightOneSteps.Materials();
            if (useModel) Configure(colours, report);

            GameObject root = PrefabUtility.LoadPrefabContents(NightOneSteps.PrefabPath);
            try
            {
                Dress(root, useModel, report);
                PrefabUtility.SaveAsPrefabAsset(root, NightOneSteps.PrefabPath, out bool saved);
                Check(saved, "the prefab is saved in place (" + NightOneSteps.PrefabPath + ")");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // What the prefab is now.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NightOneSteps.PrefabPath);
            Transform model = prefab.transform.Find(ModelChild), shapes = prefab.transform.Find(ShapesChild);
            Check(shapes != null, "the simple shapes are kept in the prefab (" + (shapes != null ? shapes.childCount + " parts" : "missing") + ")");
            Check(shapes == null || shapes.gameObject.activeSelf != useModel, "the simple shapes are " + (useModel ? "off" : "on"));
            if (useModel)
            {
                Check(model != null && model.gameObject.activeSelf, "the Blender model is in the prefab and on");
                if (model != null) Describe(prefab.transform, model.gameObject, colours, report, Check);
            }
            else Check(model == null || !model.gameObject.activeSelf, "the Blender model is off (or not there)");

            Bounds all = RenderedBounds(prefab);
            report.AppendLine($"What shows, in the prefab's own space: {V(all.size)} m, from {V(all.min)} to {V(all.max)}.");
            Check(Mathf.Max(all.size.x, all.size.z) <= MaxWidth + .005f && all.max.y <= MaxHeight + .005f,
                  $"it fits the space checked on Grace's step (no wider than {MaxWidth} m, no taller than {MaxHeight} m)");
            Check(all.min.y > -.01f && all.min.y < .02f, $"it stands on its base ({all.min.y:0.000} m at the bottom)");

            // The instances in the scene follow the prefab.
            if (SceneManager.GetActiveScene().path == AcesCafeLayoutSetup.ScenePath)
            {
                NightTrophy trophy = CityPackChecks.InScene<NightTrophy>().FirstOrDefault(t => t.thingId == NightThings.GraceGnome);
                TrophyShelf shelf = CityPackChecks.InScene<TrophyShelf>().FirstOrDefault();
                Check(trophy != null && trophy.visual != null && PrefabUtility.GetCorrespondingObjectFromSource(trophy.visual) == prefab,
                      "Barnaby on Grace's step is the prefab");
                TrophyShelf.Slot slot = shelf != null ? shelf.slots.FirstOrDefault(s => s != null && s.thingId == NightThings.GraceGnome) : null;
                Check(slot != null && slot.shown != null && PrefabUtility.GetCorrespondingObjectFromSource(slot.shown) == prefab,
                      "his copy on Ace's shelf is the prefab");
                if (slot != null && slot.shown != null)
                    Check(!slot.shown.activeSelf, "the copy on the shelf is still hidden until he's taken");
                Photos(folder, trophy, slot?.shown, report);
            }
            else report.AppendLine("NOTE: the café scene isn't open, so no photos and no scene checks.");
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            report.AppendLine(useModel
                ? "Back to the simple shapes: Fixit Fidget > Night > Night 1 - Barnaby: back to simple shapes (the prefab only)."
                : "The Blender model again: Fixit Fidget > Night > Night 1 - Barnaby: use the Blender model (the prefab only).");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = Tag + "Barnaby: " + (useModel ? "the Blender model" : "simple shapes") + ". " + folder + "\n" + report;
            if (failures == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Barnaby's look FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    /// <summary>
    /// Puts both looks under the root: the simple shapes (every child that isn't the model) under
    /// ShapesChild, and a fresh instance of the FBX as ModelChild when it's in the project. One is on.
    /// Used on a prefab being built (NightOneSteps) and on one opened with LoadPrefabContents.
    /// </summary>
    internal static void Dress(GameObject root, bool useModel, StringBuilder report)
    {
        Transform shapes = root.transform.Find(ShapesChild);
        if (shapes == null)
        {
            shapes = new GameObject(ShapesChild).transform;
            shapes.SetParent(root.transform, false);
            foreach (Transform child in root.transform.Cast<Transform>().ToList())
                if (child != shapes && child.name != ModelChild) child.SetParent(shapes, false);
        }
        Transform old = root.transform.Find(ModelChild);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject model = null;
        if (fbx != null)
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = ModelChild;
            model.transform.localPosition = Vector3.zero;   // its turn and scale stay the importer's
            model.SetActive(useModel);
        }
        bool modelOn = useModel && model != null;
        shapes.gameObject.SetActive(!modelOn);
        report?.AppendLine(modelOn
            ? "Barnaby: the Blender model (" + ModelPath + "); the simple shapes stay in the prefab, off."
            : fbx == null ? "Barnaby: simple shapes (there is no Blender model at " + ModelPath + ")."
                          : "Barnaby: simple shapes; the Blender model stays in the prefab, off.");
    }

    // The FBX's import settings: a still prop at its real size, and each of its materials mapped to the
    // Unity material of the same name.
    static void Configure(Dictionary<string, Material> colours, StringBuilder report)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        var byName = colours.Values.ToDictionary(m => m.name, m => m);
        var inFile = new List<string>();
        foreach (Renderer r in AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m != null && !inFile.Contains(m.name)) inFile.Add(m.name);
        // Names already mapped show the Unity material's name, the same as the file's.
        var missing = new List<string>();
        foreach (string name in inFile)
        {
            if (byName.TryGetValue(name, out Material target))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), target);
            else missing.Add(name);
        }
        importer.SaveAndReimport();
        report.AppendLine($"The FBX's materials ({inFile.Count}): {string.Join(", ", inFile)}" +
                          (missing.Count > 0 ? $"; NOT mapped (no Unity material of that name): {string.Join(", ", missing)}" : "; all mapped."));
    }

    static void Describe(Transform prefabRoot, GameObject model, Dictionary<string, Material> colours, StringBuilder report, Action<bool, string> check)
    {
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        check(filters.Length == 1 && filters[0].sharedMesh != null, "the model is one mesh");
        if (filters.Length == 0 || filters[0].sharedMesh == null) return;
        Mesh mesh = filters[0].sharedMesh;
        Renderer renderer = filters[0].GetComponent<Renderer>();
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
        Matrix4x4 toRoot = prefabRoot.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
        report.AppendLine($"The model: mesh \"{mesh.name}\", {indices / 3} triangles, {mesh.subMeshCount} materials; " +
                          $"its mesh turned {V(toRoot.rotation.eulerAngles)} and scaled {V(toRoot.lossyScale)} in the prefab.");
        check(indices / 3 < 1000, $"under 1,000 triangles ({indices / 3})");
        Material[] used = renderer.sharedMaterials;
        var ours = new HashSet<Material>(colours.Values);
        check(used.Length > 0 && used.All(m => m != null && ours.Contains(m)),
              "every part wears one of the placeholder's own materials (" + string.Join(", ", used.Select(m => m != null ? m.name : "none")) + ")");
        // Which way he faces: his nose is in front, on +z (the way the prefab faces the street).
        int nose = Array.FindIndex(used, m => m != null && m.name == "Night 1 - nose");
        if (nose >= 0)
        {
            Vector3 at = toRoot.MultiplyPoint3x4(mesh.GetSubMesh(nose).bounds.center);
            check(at.z > .03f && Mathf.Abs(at.x) < .02f, $"he faces +z, the way the placeholder did (his nose at {V(at)})");
        }
        else check(false, "his nose was found (the material \"Night 1 - nose\")");
    }

    static Bounds RenderedBounds(GameObject prefab)
    {
        Bounds? total = null;
        // (A prefab asset isn't in a scene, so "active" is read up the parents, not from activeInHierarchy.)
        foreach (MeshFilter f in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null || !On(f.transform, prefab.transform)) continue;
            Bounds b = f.sharedMesh.bounds;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
            foreach (Vector3 corner in Corners(b))
            {
                Vector3 p = toRoot.MultiplyPoint3x4(corner);
                if (total == null) total = new Bounds(p, Vector3.zero);
                else { Bounds t = total.Value; t.Encapsulate(p); total = t; }
            }
        }
        return total ?? new Bounds();
    }

    static bool On(Transform t, Transform root)
    {
        for (Transform x = t; x != null; x = x == root ? null : x.parent)
            if (!x.gameObject.activeSelf) return false;
        return true;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    // By day, in Edit Mode: Grace's step from the street and close to, from above, and the shelf with him on
    // it (the copy is shown for the photo and hidden again). The scene isn't changed.
    static void Photos(string folder, NightTrophy trophy, GameObject copy, StringBuilder report)
    {
        if (trophy != null)
        {
            HomeDoor door = CityPackChecks.InScene<HomeDoor>().OrderBy(d => Vector3.Distance(d.transform.position, trophy.transform.position)).FirstOrDefault();
            Vector3 g = trophy.transform.position;
            Vector3 forward = Flat(trophy.transform.forward).normalized, right = Flat(trophy.transform.right).normalized;
            NightWalkSteps.Capture(Path.Combine(folder, "1-grace-step-from-the-street.png"), g + forward * 3.4f - right * 1.6f + Vector3.up * 1.6f, g + Vector3.up * .4f, 50f, false);
            NightWalkSteps.Capture(Path.Combine(folder, "2-grace-step-close.png"), g + forward * 1.1f + right * .45f + Vector3.up * .7f, g + Vector3.up * .28f, 42f, false);
            NightWalkSteps.Capture(Path.Combine(folder, "3-from-above-like-the-night-camera.png"), g + forward * 6.5f + Vector3.up * 9.5f, g, 38f, false);
            report.AppendLine($"Photos 1-3: Grace's step by day ({(door != null ? Vector3.Distance(Flat(door.transform.position), Flat(g)).ToString("0.00", CultureInfo.InvariantCulture) + " m from her door" : "her door not found")}).");
        }
        if (copy != null)
        {
            bool was = copy.activeSelf;
            copy.SetActive(true);
            try
            {
                Vector3 at = copy.transform.position;
                NightWalkSteps.Capture(Path.Combine(folder, "4-ace-shelf-with-barnaby.png"), at + new Vector3(.25f, -.15f, -2.7f), at + new Vector3(0f, .22f, 0f), 45f, false);
                NightWalkSteps.Capture(Path.Combine(folder, "5-ace-shelf-close.png"), at + new Vector3(.12f, .05f, -1.1f), at + new Vector3(0f, .25f, 0f), 40f, false);
            }
            finally { copy.SetActive(was); }
            report.AppendLine("Photos 4-5: Ace's shelf with Barnaby on it (shown for the photos only).");
        }
        report.AppendLine("Photos in " + folder);
    }

    static string LogFolder(string name)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            name + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
}
