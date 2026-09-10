#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Local, opt-in prototype content. Original schedules, models, and materials
// are never rewritten. Existing generated content is respected on repeat runs.
public static class GraceShowcaseSetup
{
    public const string Folder = "Assets/GraceShowcase";
    public const string CameraPath = Folder + "/GraceReunionCamera.prefab";
    public const string PhotoPath = Folder + "/GraceReunionPhoto.prefab";

    [MenuItem("Fixit Fidget/Content/Grace showcase/Create prototype assets")]
    public static void CreateContent()
    {
        RequireStopped();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "GraceShowcase");
        Material body = MaterialAsset("CameraBody", new Color(0.11f, 0.14f, 0.16f));
        Material silver = MaterialAsset("CameraMetal", new Color(0.66f, 0.70f, 0.71f));
        Material glass = MaterialAsset("LensGlass", new Color(0.12f, 0.34f, 0.40f));
        Material dirt = MaterialAsset("Grime", new Color(0.39f, 0.28f, 0.16f));
        Material strap = MaterialAsset("OldStrap", new Color(0.31f, 0.14f, 0.065f));
        Material paper = MaterialAsset("PhotoPaper", new Color(0.95f, 0.91f, 0.81f));
        Material gold = MaterialAsset("GraceGold", new Color(0.83f, 0.64f, 0.23f));
        Material backdrop = MaterialAsset("PhotoBackdrop", new Color(0.29f, 0.49f, 0.45f));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath) == null)
        {
            GameObject camera = BuildCamera(body, silver, glass, dirt, strap);
            try { SaveNewPrefab(camera, CameraPath); }
            finally { UnityEngine.Object.DestroyImmediate(camera); }
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PhotoPath) == null)
        {
            GameObject photo = BuildPhoto(paper, gold, backdrop, dirt, body);
            try { SaveNewPrefab(photo, PhotoPath); }
            finally { UnityEngine.Object.DestroyImmediate(photo); }
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
        Debug.Log("[Grace showcase] Prototype camera and returned-photo assets are ready. "
            + "Run Install in open scene to use separate Day 1/2 schedule copies. Original schedules remain intact. "
            + "These are geometric placeholders for a local playtest, not final art.");
    }

    [MenuItem("Fixit Fidget/Content/Grace showcase/Install in open scene")]
    public static void InstallInOpenScene()
    {
        RequireStopped();
        var spawners = UnityEngine.Object.FindObjectsByType<CustomerSpawner>(FindObjectsInactive.Exclude);
        if (spawners.Length != 1) throw new InvalidOperationException("Open the shop scene with exactly one active CustomerSpawner.");
        CustomerSpawner spawner = spawners[0];
        using var serialized = new SerializedObject(spawner);
        SerializedProperty schedule = serialized.FindProperty("schedule");
        int firstIndex = -1, returnIndex = -1;
        DayDefinition first = null, second = null;
        for (int i = 0; i < schedule.arraySize; i++)
        {
            var day = schedule.GetArrayElementAtIndex(i).objectReferenceValue as DayDefinition;
            if (day != null && day.dayNumber == 1) { first = day; firstIndex = i; }
            if (day != null && day.dayNumber == 2) { second = day; returnIndex = i; }
        }
        if (first == null || second == null || first.featuredRegular == null || second.featuredRegular == null
            || first.featuredRegular.PersistentId != GraceCameraEpisode.ProfileId
            || second.featuredRegular.PersistentId != GraceCameraEpisode.ProfileId)
            throw new InvalidOperationException("The active schedule must already feature Grace on Day 1 and Day 2. No schedule was changed.");
        Transform anchor = FindPhotoAnchor();
        if (anchor == null) throw new InvalidOperationException("Select a scene transform for the photo mount, or provide a counter DropSpot.");

        Vector3 photoPosition = anchor.position + anchor.right * 0.48f + Vector3.up * 0.18f;
        Quaternion photoRotation = anchor.rotation;
        CreateContent();
        DayDefinition showcaseFirst = CopyDay(first, Folder + "/Day_01_GraceCamera.asset", true);
        DayDefinition showcaseReturn = CopyDay(second, Folder + "/Day_02_GraceReturn.asset", false);
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Grace prototype showcase");
        Undo.RecordObject(spawner, "Use Grace showcase schedule copies");
        schedule.GetArrayElementAtIndex(firstIndex).objectReferenceValue = showcaseFirst;
        schedule.GetArrayElementAtIndex(returnIndex).objectReferenceValue = showcaseReturn;
        serialized.ApplyModifiedProperties();
        if (UnityEngine.Object.FindAnyObjectByType<GracePhotoDisplay>() == null)
        {
            var photo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PhotoPath), spawner.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(photo, "Place Grace photo mount");
            photo.transform.SetPositionAndRotation(photoPosition, photoRotation);
            Selection.activeGameObject = photo;
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Grace showcase] Installed in the open scene. Save the scene to retain it. "
            + "Day numbers, arrival windows, pacing, and Day 1 lessons are copied unchanged. "
            + "Grace's first featured request is now the reunion camera; her next accepted return can leave a photo. "
            + "The selected photo mount may be moved to suit the counter. Scene installation supports Undo.");
    }

    private static Transform FindPhotoAnchor()
    {
        if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
            return Selection.activeTransform;
        foreach (DropSpot spot in UnityEngine.Object.FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
            if (spot.Kind == DropSpot.SpotKind.Counter) return spot.transform;
        return null;
    }

    private static DayDefinition CopyDay(DayDefinition source, string path, bool cameraVisit)
    {
        DayDefinition existing = AssetDatabase.LoadAssetAtPath<DayDefinition>(path);
        if (existing != null) return existing;
        var copy = UnityEngine.Object.Instantiate(source);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (cameraVisit)
        {
            copy.useFeaturedRepair = true;
            copy.featuredRepair = new FeaturedRepairRequest
            {
                devicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath),
                faultIndex = 0, storyEpisodeId = GraceCameraEpisode.EpisodeId
            };
            copy.intent += " Prototype: Grace's reunion camera; preserve her strap.";
        }
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    private static GameObject BuildCamera(Material body, Material metal, Material glass, Material grime, Material strap)
    {
        var root = new GameObject("GraceReunionCamera");
        root.SetActive(false);
        var repair = root.AddComponent<GraceCameraRepairJob>();
        repair.restHeight = 0.004f;
        var definition = root.AddComponent<DeviceDefinition>();
        definition.displayName = "reunion film camera";
        definition.storyEpisodeId = GraceCameraEpisode.EpisodeId;
        root.AddComponent<InspectableItem>();
        root.AddComponent<ItemInteractable>();
        Primitive(root.transform, "Camera body", new Vector3(0, .023f, 0), new Vector3(.24f, .046f, .16f), body);
        Primitive(root.transform, "Lens housing", new Vector3(-.065f, .055f, -.024f), new Vector3(.074f, .036f, .074f), metal, PrimitiveType.Cylinder);
        Primitive(root.transform, "Lens glass", new Vector3(-.065f, .093f, -.024f), new Vector3(.057f, .003f, .057f), glass, PrimitiveType.Cylinder);
        Primitive(root.transform, "Open film path", new Vector3(.056f, .049f, -.043f), new Vector3(.08f, .008f, .055f), metal);

        var lensDirt = Primitive(root.transform, "Lens grime", new Vector3(-.065f, .1f, -.024f), new Vector3(.038f, .007f, .039f), grime);
        lensDirt.AddComponent<GrimeSpot>();
        var filmDirt = Primitive(root.transform, "Film-path grime", new Vector3(.045f, .058f, -.043f), new Vector3(.028f, .007f, .033f), grime);
        filmDirt.AddComponent<GrimeSpot>();
        var edgeDirt = Primitive(root.transform, "Film-guide grime", new Vector3(.079f, .058f, -.043f), new Vector3(.020f, .007f, .032f), grime);
        edgeDirt.AddComponent<GrimeSpot>();
        var shutter = Primitive(root.transform, "Jammed shutter mechanism", new Vector3(.053f, .06f, .041f), new Vector3(.073f, .022f, .049f), metal);
        ReplaceablePart mechanism = shutter.AddComponent<ReplaceablePart>();
        var jammed = Primitive(shutter.transform, "Bent shutter blade", new Vector3(0, .56f, 0), new Vector3(.76f, .12f, .8f), grime);
        var working = Primitive(shutter.transform, "Working shutter blade", new Vector3(0, .56f, 0), new Vector3(.76f, .12f, .8f), glass);
        Set(mechanism, "partName", "Jammed shutter mechanism");
        Set(mechanism, "brokenVisual", jammed);
        Set(mechanism, "freshVisual", working);
        Set(repair, "shutter", mechanism);
        working.SetActive(false);

        // Separate from scored tasks: the request promises to keep this object.
        var oldStrap = new GameObject("KEEP - scratched sentimental strap");
        oldStrap.transform.SetParent(root.transform, false);
        Primitive(oldStrap.transform, "Upper strap", new Vector3(-.162f, .026f, -.063f), new Vector3(.083f, .009f, .017f), strap);
        Primitive(oldStrap.transform, "Lower strap", new Vector3(-.162f, .026f, .063f), new Vector3(.083f, .009f, .017f), strap);
        Primitive(oldStrap.transform, "Worn loop", new Vector3(-.20f, .026f, 0), new Vector3(.018f, .009f, .14f), strap);
        for (int i = 0; i < 3; i++)
            Primitive(oldStrap.transform, "Old scratch " + i, new Vector3(-.20f, .032f, -.027f + i * .018f), new Vector3(.015f, .001f, .002f), metal);
        definition.faults = new[]
        {
            new DeviceFault
            {
                type = FaultType.Mechanical,
                description = "dirty lens and film path, jammed shutter; keep the scratched strap",
                payout = 40, enableObjects = new[] { lensDirt, filmDirt, edgeDirt, shutter }
            }
        };
        root.SetActive(true);
        return root;
    }

    private static GameObject BuildPhoto(Material paper, Material gold, Material backdrop, Material grime, Material dark)
    {
        var root = new GameObject("GraceReunionPhoto");
        var display = root.AddComponent<GracePhotoDisplay>();
        var art = new GameObject("Placeholder reunion photograph");
        art.transform.SetParent(root.transform, false);
        Primitive(art.transform, "Paper", Vector3.zero, new Vector3(.28f, .33f, .006f), paper);
        Primitive(art.transform, "Reunion garden", new Vector3(0, .028f, -.006f), new Vector3(.245f, .24f, .004f), backdrop);
        // Clearly symbolic people until authored photo art exists. Grace occupies
        // the center of the actual in-world keepsake, making the callback visible.
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * .072f;
            Material person = i == 1 ? gold : dark;
            Primitive(art.transform, i == 1 ? "Grace - finally in frame" : "Family " + i,
                new Vector3(x, .008f, -.013f), new Vector3(.045f, .083f, .007f), person);
            Primitive(art.transform, "Face " + i, new Vector3(x, .072f, -.015f), new Vector3(.043f, .043f, .012f), person, PrimitiveType.Sphere);
        }
        var smudge = Primitive(art.transform, "Imperfect print smudge", new Vector3(.081f, -.045f, -.022f), new Vector3(.068f, .025f, .004f), grime);
        var label = new GameObject("Photo caption");
        label.transform.SetParent(art.transform, false);
        label.transform.localPosition = new Vector3(0, -.11f, -.014f);
        TextMesh caption = label.AddComponent<TextMesh>();
        caption.anchor = TextAnchor.MiddleCenter;
        caption.alignment = TextAlignment.Center;
        caption.fontSize = 48;
        caption.characterSize = .013f;
        caption.color = new Color(.15f, .13f, .10f);
        caption.text = "FINALLY IN THE FRAME\nGrace's family reunion";
        Set(display, "photograph", art);
        Set(display, "smudge", smudge);
        Set(display, "caption", caption);
        smudge.SetActive(false);
        art.SetActive(false);
        return root;
    }

    private static GameObject Primitive(Transform parent, string name, Vector3 position, Vector3 scale,
        Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static Material MaterialAsset(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No compatible material shader is available.");
        var material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SaveNewPrefab(GameObject root, string path)
    {
        if (System.IO.File.Exists(path) || System.IO.File.Exists(path + ".meta"))
            throw new InvalidOperationException("Refusing to overwrite an existing asset at " + path);
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success) throw new InvalidOperationException("Could not create " + path);
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        using var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(UnityEngine.Object target, string field, string value)
    {
        using var serialized = new SerializedObject(target);
        serialized.FindProperty(field).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireStopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before authoring Grace's showcase.");
    }
}
#endif
