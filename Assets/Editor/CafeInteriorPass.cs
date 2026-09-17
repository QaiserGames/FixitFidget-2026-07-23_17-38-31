#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One authored interior pass. Models remain editable in Blender; gameplay anchors stay in place.
public static class CafeInteriorPass
{
    const string Folder = "Assets/Playtests/AcesCafeLayout";
    const string RootName = "12 - authored cafe interior";
    static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();

    [MenuItem("Fixit Fidget/Ace's Cafe/Apply authored interior")]
    public static void ApplyMenu() => Debug.Log(Apply());

    public static string Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe layout scene.");
        if (Find(RootName)) return "Authored interior already applied.";
        foreach (string name in new[] { "CafeJoinery", "SoftBanquette", "CafeCounterDetails" })
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Interior/" + name + ".fbx"))
                throw new InvalidOperationException("Missing reviewed model: " + name);
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Author cafe interior");
        MakePalette();
        var root = Group(RootName, Find("ACE'S CAFE - layout study 02"));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Author cafe interior");
        var joinery = Import("CafeJoinery", root);
        joinery.name = "Layered windows, open doors and woven textiles";
        // FBX conversion reflects Blender's X axis. This authored room asset uses scene coordinates.
        joinery.localScale = new Vector3(-1, 1, 1);
        Remap(joinery);
        foreach (var r in joinery.GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterials.Any(m => m.name == "InteriorGlass"))
            { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        }

        var shell = Find("01 - room and windows");
        var replaced = new[] { "Left window sill", "Front left sill", "Front right sill", "Street window post", "Front window post", "Front window header", "Street window header" };
        foreach (var t in shell.GetComponentsInChildren<Transform>(true))
            if (replaced.Contains(t.name) || t.name == "Central woven runner" || t.name == "Runner border" || t.name == "Woven diamond") HideRenderer(t);

        var oldBench = Find("Window banquette - future seated animation");
        foreach (var r in oldBench.GetComponentsInChildren<Renderer>())
        { Undo.RecordObject(r, "Replace upholstered forms"); r.enabled = false; }
        // The original navigation footprint is retained. The body collider now covers the back as well.
        var baseCollider = oldBench.GetComponentInChildren<BoxCollider>();
        if (baseCollider)
        {
            Undo.RecordObject(baseCollider, "Fit banquette collision");
            baseCollider.center = new Vector3(0, .48f, 0); baseCollider.size = new Vector3(1, 2, 1);
        }
        var seating = Group("Window upholstered seating", root);
        foreach (float z in new[] { 3.3f, 6.7f })
        {
            var bench = FittedModel("SoftBanquette", null, seating, new Vector3(-6.82f, 0, z), 90);
            bench.name = z < 5 ? "Front window banquette" : "Reading window banquette";
        }
        var details = Group("Cafe service details", root);
        FittedModel("CafeCounterDetails", "PastryDisplay", details, new Vector3(-4.35f, 1.09f, 13.65f), 180);
        FittedModel("CafeCounterDetails", "CoffeeShelf", details, new Vector3(3.05f, 2.08f, 17.66f), 180);
        FittedModel("CafeCounterDetails", "RepairTray", details, new Vector3(-4.02f, 1.006f, 16.69f), 170);
        var tables = Find("05 - seating - four neighborhood tables").GetComponentsInChildren<Transform>()
            .Where(t => t.name == "Round table").OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToArray();
        for (int i = 0; i < tables.Length; i++)
        {
            var p = tables[i].position;
            FittedModel("CafeCounterDetails", "TableCaddy", details, new Vector3(p.x, .754f, p.z), i * 83);
        }
        // Proper physical door leaves, held open for the current service prototype. Central 2 m path remains clear.
        var doors = Group("Open door collision", root);
        foreach (int side in new[] { -1, 1 })
        {
            var d = Group(side < 0 ? "Left open door" : "Right open door", doors);
            d.position = new Vector3(side * 1.105f, 0, -.13f); d.rotation = Quaternion.Euler(0, -side * 102, 0);
            var c = d.gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(-side * .53f, 1.27f, 0); c.size = new Vector3(1.06f, 2.48f, .11f);
        }
        // Brass handles make the working drink cabinet read as cabinetry, without changing its interaction volumes.
        AddDrinkCabinetHandles(details);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        return "Interior authored: layered storefront, open double doors, patterned runner, two upholstered banquettes, pastry display, coffee shelf and four table caddies. Existing service anchors retained.";
    }

    public static string Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop the scene first.");
        var root = Find(RootName); if (!root) throw new InvalidOperationException("Apply the interior first.");
        MakePalette();
        var joinery = root.Find("Layered windows, open doors and woven textiles");
        Remap(joinery);
        var rugs = new Dictionary<string, Color>
        {
            ["InteriorMoss"] = new Color(.29f,.37f,.25f),
            ["InteriorCream"] = new Color(.68f,.62f,.44f),
            ["InteriorOchre"] = new Color(.57f,.46f,.27f),
            ["InteriorTerracotta"] = new Color(.49f,.30f,.22f)
        };
        var existing = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Cafe palette.asset").OfType<Material>().ToArray();
        var runner = joinery.Find("WovenRunner");
        foreach (var r in runner.GetComponentsInChildren<Renderer>())
        {
            string source = r.sharedMaterial.name;
            if (rugs.TryGetValue(source, out var color))
            {
                string name = "Woven runner - " + source;
                var m = existing.FirstOrDefault(a => a.name == name);
                if (!m) { m = new Material(palette[source]) { name = name }; AssetDatabase.AddObjectToAsset(m, Folder + "/Cafe palette.asset"); }
                m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .04f); Normal(m, "fabric_pattern_07_normal.jpg", .13f);
                r.sharedMaterial = m; EditorUtility.SetDirty(m);
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
        var view = Object.FindAnyObjectByType<CafeViewMode>();
        var newFixtures = joinery.Find("PendantFixtures").GetComponentsInChildren<Renderer>();
        foreach (var r in view.overheadFixtures.Where(r => r && !r.transform.IsChildOf(joinery)))
            { Undo.RecordObject(r, "Replace pendant discs"); r.enabled = false; }
        view.overheadFixtures = view.overheadFixtures.Where(r => r).Concat(newFixtures).Distinct().ToArray();
        EditorUtility.SetDirty(view);
        var details = root.Find("Cafe service details");
        if (!details.Find("RepairWallTools")) FittedModel("CafeCounterDetails", "RepairWallTools", details, new Vector3(-3.3f,1.575f,17.81f),180);
        foreach (var t in Find("06 - atmosphere placeholders").GetComponentsInChildren<Transform>(true))
            if (t.name == "Repair pegboard" || t.name == "Pegboard hook" || t.name == "Tool handle marker") HideRenderer(t);
        var picture = Find("Neighborhood color study");
        if (picture) foreach (var r in picture.GetComponentsInChildren<Renderer>()) { Undo.RecordObject(r, "Clear coffee shelf backdrop"); r.enabled = false; }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Finishes complete: softer botanical rug, shaped pendants, organized repair wall and clear coffee-shelf backdrop.";
    }

    static void AddDrinkCabinetHandles(Transform parent)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = 1.98f + i * .74f;
            var h = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            h.name = "Drink cabinet brass pull"; h.transform.SetParent(parent, false);
            h.transform.position = new Vector3(x, .70f, 16.029f); h.transform.localScale = new Vector3(.015f, .072f, .015f);
            Object.DestroyImmediate(h.GetComponent<Collider>()); h.GetComponent<Renderer>().sharedMaterial = palette["InteriorBrass"];
        }
    }

    static Transform FittedModel(string file, string part, Transform parent, Vector3 position, float yaw)
    {
        var holder = Group(part ?? file, parent);
        var imported = Import(file, holder);
        if (part != null)
        {
            var target = imported.GetComponentsInChildren<Transform>().First(t => t.name == part);
            // Unpack the model hierarchy so its modular part remains independently placeable.
            PrefabUtility.UnpackPrefabInstance(imported.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            target.SetParent(holder, true); target.localPosition = Vector3.zero;
            Object.DestroyImmediate(imported.gameObject); imported = target;
        }
        var rs = imported.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        imported.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        Remap(imported);
        holder.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        return holder;
    }

    static Transform Import(string name, Transform parent)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Interior/" + name + ".fbx");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        g.transform.localPosition = Vector3.zero;
        return g.transform;
    }

    static void Remap(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => palette.First(p => m.name.StartsWith(p.Key) || m.name.EndsWith(p.Key)).Value).ToArray();
            if (r.sharedMaterials.Any(m => m.name == "InteriorGlass")) r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void MakePalette()
    {
        palette.Clear();
        var path = Folder + "/Cafe palette.asset";
        var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
        string[] names = { "InteriorOak", "InteriorSage", "InteriorCream", "InteriorBrass", "InteriorInk", "InteriorMoss", "InteriorTerracotta", "InteriorOchre", "InteriorGlass", "InteriorPastry" };
        Color[] colors = { new Color(.94f,.90f,.81f), new Color(.26f,.38f,.30f), new Color(.87f,.80f,.65f), new Color(.65f,.48f,.24f), new Color(.10f,.17f,.16f), new Color(.45f,.56f,.39f), new Color(.62f,.33f,.23f), new Color(.69f,.51f,.27f), new Color(.70f,.84f,.79f,.025f), new Color(.77f,.46f,.20f) };
        for (int i = 0; i < names.Length; i++)
        {
            var m = existing.FirstOrDefault(a => a.name == names[i]);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = names[i], enableInstancing = true }; AssetDatabase.AddObjectToAsset(m, path); }
            m.SetColor("_BaseColor", colors[i]); m.SetFloat("_Smoothness", names[i] == "InteriorBrass" ? .46f : .19f);
            m.SetFloat("_Metallic", names[i] == "InteriorBrass" ? .65f : 0);
            if (names[i] == "InteriorOak")
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/oak_veneer_01_color.jpg"));
                Normal(m, "oak_veneer_01_normal.jpg", .13f);
            }
            if (names[i] == "InteriorMoss" || names[i] == "InteriorOchre") Normal(m, "fabric_pattern_07_normal.jpg", .16f);
            if (names[i] == "InteriorGlass")
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.SetFloat("_Smoothness", .63f);
                m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m); palette[names[i]] = m;
        }
    }

    static void Normal(Material m, string file, float strength)
    {
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + file));
        m.SetFloat("_BumpScale", strength); m.EnableKeyword("_NORMALMAP");
    }

    static void HideRenderer(Transform t)
    {
        var r = t.GetComponent<Renderer>(); if (!r) return;
        Undo.RecordObject(r, "Replace prototype visual"); r.enabled = false;
    }
    static Transform Group(string name, Transform parent) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform; }
    static Transform Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
}
#endif
