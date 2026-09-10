#if UNITY_EDITOR
using System;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene installation is explicit and undoable; existing scene/prefab files are not rewritten by a code update.
public static class BeverageStationSetup
{
    [MenuItem("Fixit Fidget/Content/Install six-drink dispenser in open scene")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var existing = UnityEngine.Object.FindAnyObjectByType<BeverageStation>();
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        var old = UnityEngine.Object.FindAnyObjectByType<EspressoMachine>();
        if (old == null) throw new InvalidOperationException("Open the shop scene containing the espresso machine.");
        var source = new SerializedObject(old);
        var oldCup = source.FindProperty("cupSlot").objectReferenceValue as Transform;
        string[] names = { "Coffee", "Tea", "HotChocolate", "Espresso", "Americano", "Latte" };
        var drinks = new DrinkDefinition[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            drinks[i] = AssetDatabase.LoadAssetAtPath<DrinkDefinition>("Assets/AssetsPrefabs/Drinks/Drink_" + names[i] + ".asset");
            if (drinks[i] == null || drinks[i].cupPrefab == null || drinks[i].cupPrefab.GetComponent<DrinkJob>() == null)
                throw new InvalidOperationException("Missing drink or cup prefab: " + names[i]);
        }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install beverage dispenser");
        var root = new GameObject("Beverage dispenser prototype");
        Undo.RegisterCreatedObjectUndo(root, "Create dispenser");
        root.transform.position = oldCup != null ? oldCup.position : old.transform.position + Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(old.transform.forward, Vector3.up);
        root.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > .01f ? forward : Vector3.forward);
        var station = root.AddComponent<StationInteractable>(); root.AddComponent<BeverageStation>();
        Material dark = MaterialAsset("Dispenser dark", new Color(.09f, .13f, .14f));
        Material light = MaterialAsset("Dispenser cream", new Color(.87f, .84f, .73f));
        Shape("Dispenser wall", root.transform, new Vector3(0, .3f, .22f), new Vector3(2.7f, .65f, .2f), dark, false);
        var stand = new GameObject("Drink stand point").transform; stand.SetParent(root.transform, false);
        stand.localPosition = new Vector3(0, -root.transform.position.y, -1.25f);
        var cameraObject = new GameObject("Drink station camera"); cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 1.15f, -2.6f);
        cameraObject.transform.LookAt(root.transform.TransformPoint(new Vector3(0, .2f, 0)));
        var camera = cameraObject.AddComponent<CinemachineCamera>(); camera.Priority = 0;
        camera.Lens.FieldOfView = 62;
        station.ConfigureBeverageView(camera, stand);
        for (int i = 0; i < drinks.Length; i++)
        {
            var section = new GameObject(drinks[i].drinkName); section.transform.SetParent(root.transform, false);
            section.transform.localPosition = new Vector3((i - 2.5f) * .43f, 0, 0);
            var slot = section.AddComponent<BeverageSlot>(); slot.drink = drinks[i];
            var pad = Shape("Cup pad", section.transform, new Vector3(0, -.025f, -.12f), new Vector3(.32f, .05f, .32f), light, true);
            var padControl = pad.AddComponent<BeverageControl>(); padControl.slot = slot;
            var point = new GameObject("Cup point").transform; point.SetParent(section.transform, false); point.localPosition = new Vector3(0, 0, -.12f); slot.cupPoint = point;
            var button = Shape("Dispense button", section.transform, new Vector3(0, .4f, .06f), new Vector3(.28f, .14f, .09f), light, true);
            var buttonControl = button.AddComponent<BeverageControl>(); buttonControl.slot = slot; buttonControl.dispenseButton = true;
            Label(drinks[i].drinkName, section.transform, new Vector3(0, .6f, -.005f), .32f);
            var nozzle = Shape("Nozzle", section.transform, new Vector3(0, .27f, -.1f), new Vector3(.06f, .1f, .12f), dark, false);
            var stream = nozzle.AddComponent<LineRenderer>(); stream.useWorldSpace = false;
            stream.sharedMaterial = MaterialAsset("Pour " + i, drinks[i].cupColor);
            stream.positionCount = 2; stream.SetPosition(0, Vector3.zero); stream.SetPosition(1, new Vector3(0, -2, 0));
            stream.startWidth = stream.endWidth = .12f; stream.enabled = false; slot.stream = stream;
        }
        var supply = Shape("Empty cups", root.transform, new Vector3(-1.6f, .05f, -.12f), new Vector3(.28f, .15f, .32f), light, true);
        supply.AddComponent<BeverageCupSupply>().cupPrefab = drinks[0].cupPrefab;
        Label("CUPS", root.transform, new Vector3(-1.6f, .3f, -.1f), .3f);
        var discard = Shape("Discard tray", root.transform, new Vector3(1.6f, .015f, -.12f), new Vector3(.3f, .08f, .36f), dark, true);
        discard.AddComponent<BeverageCupSupply>().discard = true;
        Label("DISCARD", root.transform, new Vector3(1.6f, .3f, -.1f), .3f);
        Undo.RecordObject(old, "Disable old order-bound brewer"); old.enabled = false;
        EditorSceneManager.MarkSceneDirty(root.scene); Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
        Debug.Log("[Beverage dispenser] Installed six independent slots. Scene changes are undoable. F enters; point + E uses pads/buttons; C switches hands; F leaves. Position the prototype to suit your counter before saving. Ingredients use the existing shared Beans stock; timing is editable on each drink asset.");
    }
    private static GameObject Shape(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Label(string label, Transform parent, Vector3 pos, float width)
    {
        var go = new GameObject(label + " label"); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var text = go.AddComponent<TextMeshPro>(); text.text = label; text.fontSize = 1.1f;
        text.alignment = TextAlignmentOptions.Center; text.color = new Color(.95f, .94f, .85f);
        text.rectTransform.sizeDelta = new Vector2(width, .16f); text.raycastTarget = false;
    }
    private static Material MaterialAsset(string name, Color color)
    {
        const string folder = "Assets/BeveragePrototype";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "BeveragePrototype");
        string path = folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color = color;
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
}
#endif
