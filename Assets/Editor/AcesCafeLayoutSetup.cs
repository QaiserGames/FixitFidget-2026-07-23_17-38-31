using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One editor-only layout recipe. The result is an ordinary editable scene;
// nothing generates or rearranges the cafe during a player's session.
public static class AcesCafeLayoutSetup
{
    public const string ScenePath = "Assets/Playtests/AcesCafeLayoutPlaytest.unity";
    private const string SourcePath = "Assets/Playtests/ServiceInteractionPlaytest.unity";
    private const string AssetFolder = "Assets/Playtests/AcesCafeLayout";
    private static readonly Dictionary<string, Material> palette = new();
    private static Transform room;

    [MenuItem("Fixit Fidget/Ace's Cafe/Open layout playtest")]
    public static void Open()
    {
        RequireStoppedAndSaved();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
        else EditorSceneManager.OpenScene(ScenePath);
        FrameScene();
    }

    public static string Build()
    {
        RequireStoppedAndSaved();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Layout already exists. Open it to edit; rebuilding must not erase your changes.");
        var source = SceneManager.GetActiveScene();
        if (source.path != SourcePath)
            throw new InvalidOperationException("Open the saved ServiceInteractionPlaytest scene first.");
        if (!EditorSceneManager.SaveScene(source, ScenePath, true))
            throw new InvalidOperationException("Could not create the separate cafe scene.");
        EditorSceneManager.OpenScene(ScenePath);
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Playtests", "AcesCafeLayout");
        MakePalette();
        room = new GameObject("ACE'S CAFE - layout study 02").transform;
        BuildShell();
        RelocateStations();
        BuildSeating();
        BuildAtmosphere();
        ConfigurePlaytest();
        BakeRoutes();
        SnapSeatApproaches();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        FrameScene();
        return ValidateLayout();
    }

    private static void RequireStoppedAndSaved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before editing the layout.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("The current scene has unsaved changes; save or review those first.");
    }

    private static GameObject Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
        .FirstOrDefault(t => t.name == name)?.gameObject
        ?? throw new InvalidOperationException("Missing layout reference: " + name);

    private static void MakePalette()
    {
        palette.Clear();
        AddMaterial("Honey oak", new Color(.56f, .32f, .14f));
        AddMaterial("Oat plaster", new Color(.86f, .79f, .65f));
        AddMaterial("Sage joinery", new Color(.23f, .34f, .27f));
        AddMaterial("Moss upholstery", new Color(.30f, .40f, .25f));
        AddMaterial("Terracotta", new Color(.66f, .27f, .16f));
        AddMaterial("Butter yellow", new Color(.88f, .65f, .28f));
        AddMaterial("Ink", new Color(.065f, .085f, .085f));
        AddMaterial("Paper", new Color(.93f, .88f, .76f));
        AddMaterial("Bay blue", new Color(.22f, .43f, .49f));
        AddMaterial("Pavement", new Color(.43f, .44f, .40f));
        AddMaterial("Warm lamp", new Color(1f, .79f, .44f));
        palette["Warm lamp"].EnableKeyword("_EMISSION");
        palette["Warm lamp"].SetColor("_EmissionColor", new Color(1f, .61f, .24f) * 1.4f);
    }

    // All colors share one asset container, keeping the Project view compact.
    private static void AddMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .22f);
        string path = AssetFolder + "/Cafe palette.asset";
        if (palette.Count == 0) AssetDatabase.CreateAsset(material, path);
        else AssetDatabase.AddObjectToAsset(material, path);
        palette.Add(name, material);
    }

    private static Transform Group(string name, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent == null ? room : parent, false);
        return t;
    }

    private static GameObject Shape(string name, Vector3 position, Vector3 size, string color,
        Transform parent, bool solid = false, PrimitiveType type = PrimitiveType.Cube)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = position;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = palette[color];
        if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    private static void BuildShell()
    {
        var shell = Group("01 - room and windows");
        var floor = Find("Floor");
        floor.transform.position = new Vector3(0, 0, 7.5f);
        floor.transform.localScale = new Vector3(1.28f, 1, 1.5f);
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_Floor_Wood.mat");
        foreach (string name in new[] { "Wall_Back", "Wall_Front", "Wall_Left", "Wall_Right" })
            Object.DestroyImmediate(Find(name));
        // Back and right walls define the room. Street-facing walls have open
        // window bays, keeping the isometric view readable without a new camera system.
        Shape("Back plaster", new Vector3(0, 1.6f, 15.12f), new Vector3(13.05f, 3.2f, .24f), "Oat plaster", shell, true);
        Shape("Right plaster", new Vector3(6.52f, 1.6f, 7.5f), new Vector3(.24f, 3.2f, 15.25f), "Oat plaster", shell, true);
        Shape("Left rear plaster", new Vector3(-6.52f, 1.6f, 12.4f), new Vector3(.24f, 3.2f, 5.45f), "Oat plaster", shell, true);
        Shape("Left window sill", new Vector3(-6.52f, .38f, 4.85f), new Vector3(.24f, .76f, 9.7f), "Sage joinery", shell, true);
        Shape("Front left sill", new Vector3(-3.8f, .38f, -.12f), new Vector3(5.2f, .76f, .24f), "Sage joinery", shell, true);
        Shape("Front right sill", new Vector3(3.8f, .38f, -.12f), new Vector3(5.2f, .76f, .24f), "Sage joinery", shell, true);
        // Invisible full-height window colliders prevent walking through a bay.
        var glass = new GameObject("Window collision boundaries");
        glass.transform.SetParent(shell, false);
        WindowCollider(glass, new Vector3(-6.52f, 1.65f, 4.85f), new Vector3(.18f, 3.3f, 9.7f));
        WindowCollider(glass, new Vector3(-3.8f, 1.65f, -.12f), new Vector3(5.2f, 3.3f, .18f));
        WindowCollider(glass, new Vector3(3.8f, 1.65f, -.12f), new Vector3(5.2f, 3.3f, .18f));
        WindowCollider(glass, new Vector3(0, 1.6f, -2.55f), new Vector3(13.2f, 3.2f, .12f));
        WindowCollider(glass, new Vector3(-6.57f, 1.6f, -1.25f), new Vector3(.12f, 3.2f, 2.5f));
        WindowCollider(glass, new Vector3(6.57f, 1.6f, -1.25f), new Vector3(.12f, 3.2f, 2.5f));
        foreach (float z in new[] { 0f, 3.2f, 6.4f, 9.7f })
            Shape("Street window post", new Vector3(-6.52f, 1.6f, z), new Vector3(.18f, 3.2f, .18f), "Sage joinery", shell);
        foreach (float x in new[] { -6.4f, -3.9f, -1.2f, 1.2f, 3.9f, 6.4f })
            Shape("Front window post", new Vector3(x, 1.6f, -.12f), new Vector3(.16f, 3.2f, .18f), "Sage joinery", shell);
        Shape("Front window header", new Vector3(0, 3.16f, -.12f), new Vector3(13, .14f, .24f), "Sage joinery", shell);
        Shape("Street window header", new Vector3(-6.52f, 3.16f, 4.85f), new Vector3(.24f, .14f, 9.7f), "Sage joinery", shell);
        Shape("Skirting - back", new Vector3(0, .13f, 14.94f), new Vector3(12.8f, .26f, .08f), "Sage joinery", shell);
        Shape("Skirting - right", new Vector3(6.36f, .13f, 7.5f), new Vector3(.08f, .26f, 15), "Sage joinery", shell);
        var detailing = Group("Floor grain and runner", shell);
        detailing.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        Shape("Central woven runner", new Vector3(0, .008f, 4.85f), new Vector3(1.9f, .015f, 6.4f), "Sage joinery", detailing);
        foreach (float x in new[] { -.82f, .82f })
            Shape("Runner border", new Vector3(x, .019f, 4.85f), new Vector3(.035f, .006f, 6.15f), "Butter yellow", detailing);
        for (int i = 0; i < 12; i++)
        {
            var motif = Shape("Woven diamond", new Vector3(0, .021f, 2.05f + i * .50f), new Vector3(.18f, .008f, .18f), "Butter yellow", detailing);
            motif.transform.localRotation = Quaternion.Euler(0, 45, 0);
        }
        Shape("Entry apron", new Vector3(0, -.12f, -1.25f), new Vector3(13.1f, .24f, 2.5f), "Pavement", shell, true);
        Shape("Staff floor inset", new Vector3(0, .009f, 12.9f), new Vector3(12.65f, .015f, 4.05f), "Pavement", detailing);
    }

    private static void WindowCollider(GameObject g, Vector3 center, Vector3 size)
    { var c = g.AddComponent<BoxCollider>(); c.center = center; c.size = size; }

    private static Transform MoveAssembly(string name, Vector3 oldPivot, Vector3 newPivot, float yaw, params string[] members)
    {
        var group = Group(name);
        group.position = oldPivot;
        foreach (var member in members) Find(member).transform.SetParent(group, true);
        group.rotation = Quaternion.Euler(0, yaw, 0);
        group.position = newPivot;
        return group;
    }

    private static void RelocateStations()
    {
        var intake = MoveAssembly("02 - intake and queue", new Vector3(0, .5f, 5), new Vector3(0, .5f, 10.6f), 180,
            "Counter", "CmCounterCam", "CM_ConversationCam", "CounterQueue", "CounterStand", "IntakeShelf");
        var repair = MoveAssembly("03 - repair work area", new Vector3(-6, .5f, 5), new Vector3(-3.3f, .5f, 13.55f), 0,
            "Workbench", "CM_BenchCam");
        MoveAssembly("04 - drinks work area", new Vector3(4.5f, .5f, 5), new Vector3(3.1f, .5f, 13.55f), 0,
            "KitchenCounter", "Beverage dispenser");
        var fitout = Group("Counter extensions and cabinetry");
        Shape("Left counter wing", new Vector3(-4.2f, .49f, 10.6f), new Vector3(4.4f, .98f, 1), "Honey oak", fitout, true);
        Shape("Right counter wing", new Vector3(3.4f, .49f, 10.6f), new Vector3(2.8f, .98f, 1), "Honey oak", fitout, true);
        foreach (var wing in new[] { new Vector2(-4.2f, 4.4f), new Vector2(3.4f, 2.8f) })
            Shape("Wing countertop", new Vector3(wing.x, 1.025f, 10.6f), new Vector3(wing.y + .04f, .09f, 1.08f), "Honey oak", fitout);
        for (int i = 0; i < 21; i++)
            Shape("Counter front batten", new Vector3(-6.25f + i * .53f, .51f, 10.08f), new Vector3(.025f, .83f, .035f), "Sage joinery", fitout);
        var bench = Find("Workbench");
        bench.GetComponent<Renderer>().enabled = false;
        Shape("Repair tabletop", new Vector3(-3.3f, .955f, 13.55f), new Vector3(2.2f, .09f, 1.05f), "Honey oak", fitout);
        foreach (float x in new[] { -4.23f, -2.37f })
            Shape("Repair cabinet", new Vector3(x, .45f, 13.6f), new Vector3(.30f, .90f, .83f), "Sage joinery", fitout);
        Find("KitchenCounter").GetComponent<Renderer>().enabled = false;
        Shape("Drink cabinet face", new Vector3(3.1f, .45f, 13.55f), new Vector3(3, .9f, 1), "Sage joinery", fitout);
        Shape("Drink countertop", new Vector3(3.1f, .97f, 13.55f), new Vector3(3.05f, .06f, 1.05f), "Honey oak", fitout);
        Shape("Rear storage", new Vector3(0, .48f, 14.35f), new Vector3(3.9f, .96f, .95f), "Sage joinery", fitout, true);
        var photo = Find("GraceReunionPhoto").transform;
        photo.position = new Vector3(.48f, 2.1f, 14.88f);
        photo.rotation = Quaternion.identity;
        Find("SpawnPoint").transform.position = new Vector3(0, .06f, -1.2f);
        Find("PatronSpawnPoint").transform.position = new Vector3(0, .06f, -1.2f);
        Find("Player").transform.SetPositionAndRotation(new Vector3(0, 1, 12.15f), Quaternion.identity);
        var points = Find("WaitingArea").GetComponentsInChildren<WaitingSpot>(true).OrderBy(x => x.name).ToArray();
        var positions = new[] { new Vector3(-4.8f, .06f, 9.25f), new Vector3(-3.3f, .06f, 9.25f),
            new Vector3(3.3f, .06f, 9.25f), new Vector3(4.8f, .06f, 9.25f),
            new Vector3(-4.8f, .06f, 1.0f), new Vector3(4.8f, .06f, 1.0f) };
        for (int i = 0; i < points.Length; i++)
            points[i].transform.SetPositionAndRotation(positions[i], Quaternion.identity);
    }

    private static void BuildSeating()
    {
        var old = Find("FURNITURE");
        var tableTemplate = Find("Table_Round4 (1)");
        var chairTemplate = Find("Chair_Cafe (4)");
        var seats = Group("05 - seating - four neighborhood tables");
        var centers = new[] { new Vector3(-3.15f, 0, 7.05f), new Vector3(3.15f, 0, 7.05f),
            new Vector3(-3.15f, 0, 2.85f), new Vector3(3.15f, 0, 2.85f) };
        for (int t = 0; t < centers.Length; t++)
        {
            var group = Group("Table " + (t + 1), seats);
            var table = Object.Instantiate(tableTemplate, group);
            table.name = "Round table";
            table.transform.position = centers[t];
            for (int i = 0; i < 4; i++)
            {
                var chair = Object.Instantiate(chairTemplate, group);
                chair.name = "Seat " + (i + 1);
                var seat = chair.GetComponent<TableSeat>();
                Vector3 outward = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                Quaternion facing = Quaternion.LookRotation(-outward);
                Quaternion delta = facing * Quaternion.Inverse(seat.StandPoint.rotation);
                chair.transform.rotation = delta * chair.transform.rotation;
                chair.transform.position = centers[t] + outward * .93f;
                seat.StandPoint.SetPositionAndRotation(centers[t] + outward * 1.60f + Vector3.up * .06f, facing);
                seat.SeatPose.SetPositionAndRotation(centers[t] + outward * .93f + Vector3.up * .45f, facing);
                seat.CupSpot.SetPositionAndRotation(centers[t] + outward * .40f + Vector3.up * .80f, facing);
            }
        }
        Object.DestroyImmediate(old);
        var nook = Group("Window banquette - future seated animation", seats);
        Shape("Banquette timber base", new Vector3(-5.82f, .24f, 5.0f), new Vector3(.87f, .48f, 6.7f), "Honey oak", nook, true);
        Shape("Moss seat cushion", new Vector3(-5.72f, .51f, 5.0f), new Vector3(.73f, .13f, 6.6f), "Moss upholstery", nook);
        Shape("Window back cushion", new Vector3(-6.12f, .78f, 5.0f), new Vector3(.18f, .72f, 6.6f), "Moss upholstery", nook);
        foreach (float z in new[] { 2.0f, 4.2f, 6.3f, 8.0f })
            Shape("Loose cushion", new Vector3(-5.94f, .74f, z), new Vector3(.29f, .36f, .44f), "Paper", nook);
    }

    private static void BuildAtmosphere()
    {
        var decor = Group("06 - atmosphere placeholders");
        decor.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var ceiling = Shape("Inside-only ceiling", new Vector3(0, 3.25f, 7.5f),
            new Vector3(1.28f, 1, 1.5f), "Oat plaster", decor, false, PrimitiveType.Plane);
        ceiling.transform.localRotation = Quaternion.Euler(180, 0, 0);
        ceiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Flat framed shapes stand in for owner-selected local art, not new UI.
        Art(decor, "Bay sunset print", new Vector3(-.9f, 2.15f, 14.90f), 0, 1.0f, 1.28f, 0);
        Art(decor, "Neighborhood color study", new Vector3(2.9f, 2.30f, 14.90f), 0, 1.4f, .94f, 1);
        Art(decor, "Cat portrait study", new Vector3(5.0f, 2.1f, 14.90f), 0, .85f, 1.15f, 2);
        Art(decor, "Right wall local art", new Vector3(6.36f, 1.95f, 6.0f), 90, 1.45f, 1.8f, 1);
        Art(decor, "Small neighborhood print", new Vector3(6.36f, 2.15f, 3.5f), 90, .85f, 1.15f, 0);
        Shape("Repair pegboard", new Vector3(-3.3f, 2.1f, 14.9f), new Vector3(2.8f, 1.1f, .08f), "Honey oak", decor);
        for (int i = 0; i < 8; i++)
        {
            Shape("Pegboard hook", new Vector3(-4.4f + i * .32f, 2.1f, 14.82f), new Vector3(.025f, .20f + (i % 3) * .12f, .025f), "Ink", decor);
            Shape("Tool handle marker", new Vector3(-4.4f + i * .32f, 2.0f, 14.80f), new Vector3(.055f, .13f, .045f), "Terracotta", decor);
        }
        foreach (float x in new[] { -4.9f, 1.45f, 5.4f })
        {
            Shape("Wall lamp stem", new Vector3(x, 2.65f, 14.65f), new Vector3(.06f, .06f, .5f), "Sage joinery", decor);
            Shape("Lamp shade", new Vector3(x, 2.57f, 14.40f), new Vector3(.34f, .12f, .34f), "Sage joinery", decor, false, PrimitiveType.Cylinder);
            Shape("Warm lamp diffuser", new Vector3(x, 2.46f, 14.40f), new Vector3(.26f, .025f, .26f), "Warm lamp", decor, false, PrimitiveType.Cylinder);
        }
        var street = Group("07 - street context - visual only");
        street.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        Shape("Street-side pavement", new Vector3(-7.4f, -.12f, 7), new Vector3(1.5f, .24f, 17), "Pavement", street);
        var road = Shape("Sloping San Francisco street", new Vector3(-10.3f, -.40f, 7), new Vector3(4, .20f, 26), "Ink", street);
        road.transform.localRotation = Quaternion.Euler(-3.5f, 0, 0);
        for (int i = 0; i < 4; i++)
        {
            float z = -1f + i * 5.3f;
            float y = (z - 7) * .06f;
            Shape("Neighbor facade " + i, new Vector3(-14, 2.2f + y, z), new Vector3(2.4f, 5.2f, 4.7f), i % 2 == 0 ? "Oat plaster" : "Terracotta", street);
            for (int w = 0; w < 3; w++)
                Shape("Neighbor bay window", new Vector3(-12.76f, 2.3f + y, z - 1.5f + w * 1.5f), new Vector3(.08f, 1.3f, .8f), "Bay blue", street);
        }
        foreach (float x in new[] { -1.85f, 1.85f })
        {
            Shape("Entry planter", new Vector3(x, .23f, -.75f), new Vector3(.52f, .23f, .52f), "Terracotta", street, false, PrimitiveType.Cylinder);
            Shape("Small entry greenery", new Vector3(x, .64f, -.75f), new Vector3(.65f, .55f, .65f), "Moss upholstery", street, false, PrimitiveType.Sphere);
        }
        var light = Find("Directional Light").GetComponent<Light>();
        light.color = new Color(1, .88f, .71f);
        light.intensity = 1.7f;
        light.transform.rotation = Quaternion.Euler(48, 120, 0);
        var warm = Find("Light_ShopWarmth").GetComponent<Light>();
        warm.transform.position = new Vector3(0, 4.5f, 10);
        warm.range = 16;
        warm.intensity = 2.0f;
        warm.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.60f, .56f, .48f);
    }

    private static void Art(Transform parent, string name, Vector3 position, float yaw, float width, float height, int motif)
    {
        var frame = Group(name, parent);
        frame.localPosition = position;
        frame.localRotation = Quaternion.Euler(0, yaw, 0);
        Shape("Oak frame", Vector3.zero, new Vector3(width, height, .07f), "Honey oak", frame);
        Shape("Paper mount", new Vector3(0, 0, -.042f), new Vector3(width - .1f, height - .1f, .015f), "Paper", frame);
        if (motif == 2)
        {
            Shape("Cat silhouette", new Vector3(0, -.05f, -.058f), new Vector3(width * .43f, height * .46f, .013f), "Ink", frame, false, PrimitiveType.Sphere);
            Shape("Cat head", new Vector3(0, height * .19f, -.065f), new Vector3(width * .40f, width * .40f, .015f), "Ink", frame, false, PrimitiveType.Sphere);
            foreach (float x in new[] { -.12f, .12f })
            {
                var ear = Shape("Cat ear", new Vector3(x, height * .33f, -.061f), new Vector3(.13f, .13f, .016f), "Ink", frame);
                ear.transform.localRotation = Quaternion.Euler(0, 0, 45);
                Shape("Cat eye", new Vector3(x * .7f, height * .19f, -.08f), new Vector3(.04f, .03f, .01f), "Butter yellow", frame);
            }
        }
        else
        {
            Shape("Color field", new Vector3(0, -height * .16f, -.057f), new Vector3(width * .74f, height * .40f, .012f), "Bay blue", frame);
            Shape("Sun or abstract circle", new Vector3(-width * .13f, height * .15f, -.066f), new Vector3(width * .43f, width * .43f, .016f), "Butter yellow", frame, false, PrimitiveType.Sphere);
            var slash = Shape("Hill or abstract form", new Vector3(width * .11f, -height * .08f, -.078f), new Vector3(width * .28f, height * .61f, .016f), "Terracotta", frame);
            slash.transform.localRotation = Quaternion.Euler(0, 0, motif == 0 ? -17 : 32);
        }
    }

    private static void ConfigurePlaytest()
    {
        var save = new SerializedObject(Object.FindAnyObjectByType<SaveManager>());
        save.FindProperty("useInteractionPlaytestSave").boolValue = true;
        save.FindProperty("interactionPlaytestSaveName").stringValue = "playtest-aces-cafe.json";
        save.ApplyModifiedPropertiesWithoutUndo();
        var log = new SerializedObject(Object.FindAnyObjectByType<DayLog>());
        log.FindProperty("folderName").stringValue = "DayLogs/AcesCafeLayout";
        log.ApplyModifiedPropertiesWithoutUndo();
        var clock = new SerializedObject(Object.FindAnyObjectByType<DayClock>());
        clock.FindProperty("startingDay").intValue = 1;
        clock.ApplyModifiedPropertiesWithoutUndo();
        var cam = Find("CmShopCam").GetComponent<CinemachineCamera>();
        // Same 45-degree direction as movement. A steady whole-room view lets
        // this playtest assess the layout; first-person station views are retained.
        cam.Target.TrackingTarget = null;
        cam.GetComponent<CinemachineFollow>().enabled = false;
        cam.transform.rotation = Quaternion.Euler(50, 45, 0);
        cam.transform.position = new Vector3(0, .4f, 7.3f) - cam.transform.forward * 30;
        var lens = cam.Lens;
        lens.FieldOfView = 39;
        lens.NearClipPlane = .05f;
        cam.Lens = lens;
        var main = Find("Main Camera").GetComponent<Camera>();
        main.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        main.fieldOfView = lens.FieldOfView;
        // Removed original wall renderers are no longer fade targets.
        var fader = new SerializedObject(main.GetComponent<CameraWallFader>());
        fader.FindProperty("occluders").arraySize = 0;
        fader.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void BakeRoutes()
    {
        if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Bake only the stopped cafe layout scene.");
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        surface.RemoveData();
        surface.navMeshData = null; // Never update the original scene's shared bake.
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = .08f;
        // Interaction volumes and the player's starting body are not furniture.
        // Exclude them during this editor bake, restoring their exact states.
        var excluded = Object.FindObjectsByType<Collider>().Where(c => c.enabled &&
            (c.isTrigger || c.GetComponentInParent<PlayerMovement>() != null)).ToArray();
        try
        {
            foreach (var collider in excluded) collider.enabled = false;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
        }
        finally
        {
            foreach (var collider in excluded) if (collider != null) collider.enabled = true;
            Physics.SyncTransforms();
        }
        if (surface.navMeshData == null) throw new InvalidOperationException("Cafe navigation bake failed.");
        string path = AssetFolder + "/Cafe routes.asset";
        // Re-bakes update the dedicated asset without replacing its GUID.
        var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (saved == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
        else
        {
            var fresh = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(fresh, saved);
            EditorUtility.SetDirty(saved);
            surface.navMeshData = saved;
            Object.DestroyImmediate(fresh);
            surface.AddData();
        }
        EditorUtility.SetDirty(surface);
    }

    public static string ValidateLayout()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open the cafe layout.");
        var failures = new List<string>();
        var origin = Find("SpawnPoint").transform.position;
        var targets = Object.FindObjectsByType<WaitingSpot>().Select(s => s.StandPoint)
            .Concat(Find("CounterQueue").transform.Cast<Transform>())
            .Concat(Object.FindObjectsByType<StationInteractable>().Select(s => s.StandPoint)).Where(t => t != null).ToArray();
        int paths = 0;
        foreach (var target in targets)
        {
            if (!NavMesh.SamplePosition(target.position, out var hit, .22f, NavMesh.AllAreas))
            { failures.Add(target.parent.name + "/" + target.name + " off navigation"); continue; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                failures.Add(target.parent.name + "/" + target.name + " unreachable");
            else paths++;
        }
        string result = "Cafe layout: " + Object.FindObjectsByType<TableSeat>().Length
            + " seats; " + paths + "/" + targets.Length + " destination paths complete. "
            + (failures.Count == 0 ? "PASS" : string.Join("; ", failures));
        Debug.Log(result);
        return result;
    }

    public static void SnapSeatApproaches()
    {
        if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Adjust only the stopped cafe layout scene.");
        foreach (var seat in Object.FindObjectsByType<TableSeat>())
            if (NavMesh.SamplePosition(seat.StandPoint.position, out var hit, .35f, NavMesh.AllAreas))
                seat.StandPoint.position = hit.position;
    }

    private static void FrameScene()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        view.maximized = false;
        view.orthographic = true;
        view.drawGizmos = false;
        view.LookAtDirect(new Vector3(0, .5f, 7.5f), Quaternion.Euler(50, 45, 0), 12);
        view.Repaint();
    }
}
