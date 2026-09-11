#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// This installer is explicit and undoable. The authored FBX owns the visible anatomy;
// separate invisible targets make the controls forgiving without enlarging the prop.
public static class BeverageStationSetup
{
    private const string ModelPath = "Assets/Art/Models/BeverageDispenserV2.fbx";
    private const string MaterialFolder = "Assets/Art/Materials/BeverageDispenserV2";
    private static readonly Vector3 CupRowCentre = new Vector3(0, .087f, -.196f);

    [MenuItem("Fixit Fidget/Content/Install six-drink dispenser in open scene")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before installing the dispenser.");
        Scene scene = SceneManager.GetActiveScene();
        var existing = UnityEngine.Object.FindObjectsByType<BeverageStation>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Where(item => item.gameObject.scene == scene).ToArray();
        var old = UnityEngine.Object.FindObjectsByType<EspressoMachine>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).FirstOrDefault(item => item.gameObject.scene == scene);
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) throw new InvalidOperationException("Import " + ModelPath + " first.");
        if (old == null && existing.Length == 0)
            throw new InvalidOperationException("Open the shop scene containing the espresso machine.");

        string[] names = { "Coffee", "Tea", "HotChocolate", "Espresso", "Americano", "Latte" };
        var drinks = new DrinkDefinition[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            drinks[i] = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(
                "Assets/AssetsPrefabs/Drinks/Drink_" + names[i] + ".asset");
            if (drinks[i] == null || drinks[i].cupPrefab == null ||
                drinks[i].cupPrefab.GetComponent<DrinkJob>() == null)
                throw new InvalidOperationException("Missing drink or cup prefab: " + names[i]);
        }

        Vector3 position;
        Quaternion rotation;
        // A second installation preserves the placement the artist chose for this version.
        var previousV2 = existing.FirstOrDefault(item => item.transform.Find("Visual model") != null);
        if (previousV2 != null)
        {
            position = previousV2.transform.position;
            rotation = previousV2.transform.rotation;
        }
        else if (old != null)
        {
            var oldCup = new SerializedObject(old).FindProperty("cupSlot").objectReferenceValue as Transform;
            Vector3 front = Vector3.ProjectOnPlane(old.transform.forward, Vector3.up).normalized;
            if (front.sqrMagnitude < .1f) front = Vector3.back;
            // The old cup establishes which face is the working side. The new model faces -Z.
            if (oldCup != null && Vector3.Dot(oldCup.position - old.transform.position, front) < 0)
                front = -front;
            rotation = Quaternion.LookRotation(-front, Vector3.up);
            Vector3 oldCupPosition = oldCup != null ? oldCup.position : old.transform.position;
            position = oldCupPosition - rotation * CupRowCentre;
        }
        else
        {
            var previous = existing[0];
            rotation = previous.transform.rotation;
            position = previous.transform.position;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install compact beverage dispenser");
        try
        {
            // Replace the old generated station as one undoable operation, never duplicate it.
            foreach (var prior in existing) Undo.DestroyObjectImmediate(prior.gameObject);
            var root = new GameObject("Beverage dispenser");
            Undo.RegisterCreatedObjectUndo(root, "Create compact beverage dispenser");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = Vector3.one;
            var station = root.AddComponent<StationInteractable>();
            root.AddComponent<BeverageStation>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Visual model";
            AlignModel(model.transform);
            AssignModelMaterials(model);
            // The case stays solid; controls use their own broad, invisible hit areas.
            var body = new GameObject("Dispenser body collider");
            body.transform.SetParent(root.transform, false);
            var bodyCollider = body.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0, .34f, .1f);
            bodyCollider.size = new Vector3(1.07f, .59f, .20f);

            var stand = new GameObject("Drink stand point").transform;
            stand.SetParent(root.transform, false);
            stand.localPosition = new Vector3(0, -position.y, -.95f);
            var cameraObject = new GameObject("Drink station camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, .77f, -1.28f);
            cameraObject.transform.LookAt(root.transform.TransformPoint(new Vector3(0, .30f, -.10f)));
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Priority = 0;
            camera.Lens.FieldOfView = 58;
            camera.Lens.NearClipPlane = .03f;
            station.ConfigureBeverageView(camera, stand);

            for (int i = 0; i < drinks.Length; i++)
            {
                float x = (i - 2.5f) * .158f;
                var section = new GameObject(drinks[i].drinkName);
                section.transform.SetParent(root.transform, false);
                var slot = section.AddComponent<BeverageSlot>();
                slot.drink = drinks[i];
                var cupPoint = new GameObject("Cup point").transform;
                cupPoint.SetParent(section.transform, false);
                cupPoint.localPosition = new Vector3(x, .087f, -.196f);
                slot.cupPoint = cupPoint;

                var zone = HitTarget("Cup and nozzle area", section.transform,
                    new Vector3(x, .246f, -.20f), new Vector3(.151f, .335f, .20f));
                var control = zone.AddComponent<BeverageControl>();
                control.slot = slot;
                control.dispenseButton = false;
                // A deliberate paddle press retains cup-less dispensing and ingredient waste.
                var button = HitTarget("Dispense paddle", section.transform,
                    new Vector3(x, .465f, -.172f), new Vector3(.093f, .092f, .038f));
                var buttonControl = button.AddComponent<BeverageControl>();
                buttonControl.slot = slot;
                buttonControl.dispenseButton = true;

                var pour = new GameObject("Visible pour");
                pour.transform.SetParent(section.transform, false);
                pour.transform.localPosition = new Vector3(x, .288f, -.196f);
                var stream = pour.AddComponent<LineRenderer>();
                stream.useWorldSpace = false;
                stream.sharedMaterial = MaterialAsset("BF_Pour_" + names[i], drinks[i].cupColor, 0, .25f, true);
                stream.positionCount = 2;
                stream.SetPosition(0, Vector3.zero);
                stream.SetPosition(1, new Vector3(0, -.105f, 0));
                stream.startWidth = .009f;
                stream.endWidth = .013f;
                stream.numCapVertices = 4;
                stream.numCornerVertices = 3;
                stream.enabled = false;
                stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                slot.stream = stream;
            }
            var supply = HitTarget("Cup stack", root.transform, new Vector3(-.635f, .17f, -.103f),
                new Vector3(.17f, .32f, .24f));
            supply.AddComponent<BeverageCupSupply>().cupPrefab = drinks[0].cupPrefab;
            var discard = HitTarget("Discard basin", root.transform, new Vector3(.635f, .105f, -.11f),
                new Vector3(.18f, .18f, .27f));
            discard.AddComponent<BeverageCupSupply>().discard = true;

            // The former separate CupStack sits directly on KitchenCounter. Preserve that
            // dedicated leaf object inactive so the new caddy is the one visible supply.
            foreach (var legacyCups in UnityEngine.Object.FindObjectsByType<CupStack>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (legacyCups.gameObject.scene != scene || legacyCups.transform.childCount != 0
                    || Vector3.Distance(legacyCups.transform.position, root.transform.position) > 2f) continue;
                Undo.RecordObject(legacyCups.gameObject, "Preserve old cup supply inactive");
                legacyCups.gameObject.SetActive(false);
            }

            if (old != null)
            {
                Undo.RecordObject(old, "Disable previous brewer");
                old.enabled = false;
                // The current shop's EspressoMachine is a dedicated top-level prop.
                // Never deactivate a shared counter or a station hierarchy if another scene differs.
                bool dedicated = old.gameObject.name == "EspressoMachine" && old.transform.parent == null
                    && old.GetComponentsInChildren<StationInteractable>(true).Length == 0
                    && old.GetComponentsInChildren<DropSpot>(true).Length == 0;
                if (dedicated)
                {
                    Undo.RecordObject(old.gameObject, "Preserve old espresso prop inactive");
                    old.gameObject.SetActive(false);
                }
                else
                {
                    var renderer = old.GetComponent<Renderer>();
                    if (renderer != null) { Undo.RecordObject(renderer, "Hide previous brewer"); renderer.enabled = false; }
                    var collider = old.GetComponent<Collider>();
                    if (collider != null) { Undo.RecordObject(collider, "Disable previous brewer collider"); collider.enabled = false; }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            Debug.Log("[Beverage dispenser] Installed compact six-valve model: 1.45m overall footprint, "
                + "1.08m case, 0.64m height. Previous espresso prop preserved. F enters/leaves; "
                + "point at a broad drink section to prepare or collect; C switches hands. "
                + "Use the paddle for deliberate dispensing without a cup. Scene changes are undoable.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    private static GameObject HitTarget(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);
        target.transform.localPosition = position;
        target.AddComponent<BoxCollider>().size = size;
        return target;
    }

    private static Transform Marker(Transform model, string name)
    {
        var result = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (result == null) throw new InvalidOperationException("Dispenser model is missing " + name);
        return result;
    }

    private static void AlignModel(Transform model)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        Transform origin = Marker(model, "AnchorBase");
        Transform front = Marker(model, "AnchorFront");
        Transform up = Marker(model, "AnchorUp");
        Vector3 localOrigin = model.InverseTransformPoint(origin.position);
        Vector3 frontVector = model.InverseTransformPoint(front.position) - localOrigin;
        Vector3 upVector = model.InverseTransformPoint(up.position) - localOrigin;
        float importedMetre = upVector.magnitude;
        if (importedMetre <= .0001f) throw new InvalidOperationException("Invalid dispenser model scale.");
        model.localScale = Vector3.one / importedMetre;
        model.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up)
            * Quaternion.Inverse(Quaternion.LookRotation(frontVector, upVector));
        model.localPosition = -(model.localRotation * (localOrigin / importedMetre));
        // Export/import unit or axis mistakes are caught before the scene is left installed.
        var cup = Marker(model, "CupPoint_Coffee");
        Vector3 cupLocal = model.parent.InverseTransformPoint(cup.position);
        if ((cupLocal - new Vector3(-.395f, .087f, -.196f)).sqrMagnitude > .000025f)
            throw new InvalidOperationException("Dispenser FBX axes or units do not match its control points: " + cupLocal);
    }

    private static void AssignModelMaterials(GameObject model)
    {
        var palette = new Dictionary<string, Color>
        {
            { "BF_Enamel", new Color(.075f, .19f, .175f) },
            { "BF_Cream", new Color(.87f, .80f, .62f) },
            { "BF_Steel", new Color(.38f, .44f, .44f) },
            { "BF_Chrome", new Color(.67f, .73f, .72f) },
            { "BF_Shadow", new Color(.021f, .034f, .034f) },
            { "BF_Paper", new Color(.95f, .91f, .80f) },
            { "BF_Coffee", new Color(.36f, .20f, .10f) },
            { "BF_Tea", new Color(.41f, .58f, .27f) },
            { "BF_Cocoa", new Color(.60f, .29f, .17f) },
            { "BF_Espresso", new Color(.22f, .27f, .27f) },
            { "BF_Americano", new Color(.30f, .49f, .56f) },
            { "BF_Latte", new Color(.81f, .60f, .29f) }
        };
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i] != null ? materials[i].name.Replace(" (Instance)", "") : "BF_Enamel";
                if (!palette.TryGetValue(name, out Color color)) color = palette["BF_Enamel"];
                float metallic = name == "BF_Chrome" ? .87f : name == "BF_Steel" ? .72f
                    : name == "BF_Enamel" ? .22f : .05f;
                float smoothness = name == "BF_Chrome" ? .8f : name == "BF_Steel" ? .65f
                    : name == "BF_Paper" ? .3f : .56f;
                materials[i] = MaterialAsset(name, color, metallic, smoothness);
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Material MaterialAsset(string name, Color color, float metallic, float smoothness, bool unlit = false)
    {
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The dispenser requires the Universal Render Pipeline shaders.");
        mat = new Material(shader) { name = name, color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        string parent = path.Substring(0, split);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
    }
}
#endif
