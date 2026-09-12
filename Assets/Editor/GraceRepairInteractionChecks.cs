#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GraceRepairInteractionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Grace camera tweezers interaction")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GraceShowcaseSetup.CameraPath);
        Require(prefab != null, "The Grace camera prefab must be installed first.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousClock = DayClock.Instance;
        float previousTimeScale = Time.timeScale;
        var generatedMaterials = new HashSet<Material>();
        try
        {
            Instance<DayClock>(null); Time.timeScale = 1f;
            var host = new GameObject("Isolated Grace click checks"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = UnityEngine.Object.Instantiate(prefab, host.transform);
            camera.transform.SetPositionAndRotation(new Vector3(7000, 7000, 7000), Quaternion.identity);
            camera.GetComponent<DeviceDefinition>().ApplyFault(0);
            var job = camera.GetComponent<GraceCameraRepairJob>();
            var shutter = job.Shutter; Call(shutter, "Awake");
            foreach (Renderer renderer in shutter.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial != null && !AssetDatabase.Contains(renderer.sharedMaterial))
                    generatedMaterials.Add(renderer.sharedMaterial);
            var inspector = Child(host, "Inspector").AddComponent<ItemInspector>();
            Set(inspector, "focusedItem", job);
            var tweezers = Tool(host, ToolType.Tweezers);
            var brush = Tool(host, ToolType.Brush);
            var blade = shutter.transform.Find("Bent shutter blade").GetComponent<Collider>();
            Require(blade != null && blade.GetComponent<BenchInteractable>() == null,
                "The authored visible shutter blade reproduces the child-collider interaction bug.");
            Collider hit = FrontHit(camera, blade.bounds.center);
            Require(hit == blade, "A ray aimed at the visible blade first hits its child collider.");
            var resolved = Resolve(hit);
            Require(resolved.part == shutter && resolved.grime == null && resolved.tool == null,
                "The visible blade resolves to its containing replacement task.");
            Press(inspector, resolved);
            Require(!shutter.IsReplaced && !(bool)Get(inspector, "rotateGesture"),
                "Clicking the shutter with bare hands neither replaces it nor starts rotating the camera.");
            Press(inspector, Resolve(brush.GetComponentInChildren<Collider>()));
            Press(inspector, resolved);
            Require(inspector.CurrentTool == ToolType.Brush && !shutter.IsReplaced,
                "The brush cannot perform a tweezers replacement.");

            // Model the player's already-cleaned camera. Grime removal itself is
            // covered by the brush playtest; the regression here is the final
            // visible blade click that previously could never finish the repair.
            foreach (var grime in camera.GetComponentsInChildren<GrimeSpot>())
            {
                Require(Resolve(grime.GetComponent<Collider>()).grime == grime,
                    "All authored camera cleaning surfaces remain discoverable by the bench resolver.");
                UnityEngine.Object.DestroyImmediate(grime.gameObject);
            }
            Require(job.Quality == 0f, "A clean camera with a jammed shutter still cannot receive a passing grade.");
            Press(inspector, Resolve(tweezers.GetComponentInChildren<Collider>()));
            Require(inspector.CurrentTool == ToolType.Tweezers, "Clicking a tool's child handle selects the tweezers.");
            Time.timeScale = 0f; Press(inspector, resolved);
            Require(!shutter.IsReplaced, "Paused bench clicks cannot perform a replacement.");
            Time.timeScale = 1f;
            var clock = Child(host, "Ended day").AddComponent<DayClock>();
            typeof(DayClock).GetProperty("DayOver").SetValue(clock, true); Instance(clock);
            Press(inspector, resolved);
            Require(!shutter.IsReplaced, "The recap also blocks a queued replacement click.");
            Instance<DayClock>(null);
            Press(inspector, resolved);
            Require(shutter.IsReplaced && job.Grade == JobGrade.Perfect && job.IsComplete,
                "The same resolved click and selected-tool action used by the inspector finishes the cleaned camera as Perfect.");
            Require(!blade.gameObject.activeSelf && shutter.transform.Find("Working shutter blade").gameObject.activeSelf,
                "Repair visibly replaces the bent blade with the working blade.");
            Press(inspector, Resolve(shutter.transform.Find("Working shutter blade").GetComponent<Collider>()));
            Require(job.Grade == JobGrade.Perfect && camera.transform.Find("KEEP - scratched sentimental strap") != null,
                "Repeated clicks preserve the finished repair and original sentimental strap.");

            var cover = Child(host, "Closed cover").AddComponent<RemovablePart>();
            var covered = Child(cover.gameObject, "Covered replacement").AddComponent<ReplaceablePart>();
            Set(covered, "coveredBy", cover);
            var coveredHit = Child(covered.gameObject, "Visible replacement mesh").AddComponent<BoxCollider>();
            Press(inspector, Resolve(coveredHit));
            Require(!covered.IsReplaced, "Parent resolution does not bypass a closed cover's replacement gate.");
            typeof(RemovablePart).GetProperty("IsRemoved").SetValue(cover, true);
            Press(inspector, Resolve(coveredHit));
            Require(covered.IsReplaced, "An uncovered replacement still accepts its required tool.");
            var screw = Child(cover.gameObject, "Existing screw target").AddComponent<ScrewTarget>();
            var screwHit = Child(screw.gameObject, "Screw head mesh").AddComponent<BoxCollider>();
            Require(Resolve(screwHit).part == screw,
                "The nearest screw target wins over its parent cover, preserving the existing disassembly hierarchy.");
            Debug.Log("[Grace camera interaction] PASS: real blade collider ray, parent task resolution, child tool selection, wrong-tool/pause/recap gates, final tweezers click reaches Perfect, visual swap, strap preservation, covered parts and nearest screw targets. No scene or save changes.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var material in generatedMaterials) if (material != null) UnityEngine.Object.DestroyImmediate(material);
            Instance(previousClock); Time.timeScale = previousTimeScale;
        }
    }

    private static Collider FrontHit(GameObject root, Vector3 point)
    {
        Physics.SyncTransforms();
        var ray = new Ray(point + Vector3.up * .5f, Vector3.down);
        Collider nearest = null; float distance = float.PositiveInfinity;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
            if (collider.enabled && collider.Raycast(ray, out RaycastHit hit, 1f) && hit.distance < distance)
            { nearest = collider; distance = hit.distance; }
        return nearest;
    }
    private static ToolPickup Tool(GameObject parent, ToolType type)
    {
        var tool = Child(parent, type.ToString()).AddComponent<ToolPickup>(); tool.tool = type;
        Child(tool.gameObject, "Tool handle").AddComponent<BoxCollider>(); Call(tool, "Awake"); return tool;
    }
    private static (BenchInteractable part, GrimeSpot grime, ToolPickup tool) Resolve(Collider collider)
    {
        object[] args = { collider, null, null, null };
        typeof(ItemInspector).GetMethod("ResolveBenchHit", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return ((BenchInteractable)args[1], (GrimeSpot)args[2], (ToolPickup)args[3]);
    }
    private static void Press(ItemInspector inspector, (BenchInteractable part, GrimeSpot grime, ToolPickup tool) target)
        => typeof(ItemInspector).GetMethod("HandleBenchPress", Private).Invoke(inspector,
            new object[] { target.part, target.grime, target.tool, false });
    private static GameObject Child(GameObject parent, string name)
    { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void Instance<T>(T value) => typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
