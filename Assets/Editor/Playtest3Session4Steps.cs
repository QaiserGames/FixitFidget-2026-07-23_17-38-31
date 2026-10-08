#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE BENCH, V2 (7 Oct 2026, evening; claude/bench-spec-v2.md): THE STAGE IN THE SCENE, AND THE LAB
//
//   Fixit Fidget > Bench > Bench 2 - Build the bench stage (the scene)
//       Under the Workbench's InspectRig: a cutting mat the loose parts land on, the magnetic parts tray, and the tool
//       caddy with the five tool models standing in it (ToolPickup each; the cloth is new). The old column of tinted
//       cylinders is switched off, not deleted. BenchStage is added to the rig and wired. The scene is saved.
//   Fixit Fidget > Bench > Bench 2 - Undo: back to the tool column
//   Fixit Fidget > Bench > Bench 3 - The bench - play check (lab, drives itself)
//       A lab session on a fresh Day 1: the check puts a phone on the bench and works it with a virtual pad: the face-on
//       presentation, the spin's settle and the flip, the zoom, every screw held out and watched fall, the cover pried,
//       dragged off and dropped, the broken part pinched out and dropped in the tray, the fresh one seated, the cover
//       dragged home, every hole held until its screw is back; the clock, the catch, the placeholder sounds (the grime
//       and the glass are 4b). Report and photos: Logs/Bench/bench-check-<time>/.
// ---------------------------------------------------------------------------
static class Playtest3Session4Steps
{
    const string Menu = "Fixit Fidget/Bench/";
    const string Tag = "[Bench v2] ";
    const string ModelFolder = "Assets/Art/Models/BenchTools";
    const string MaterialFolder = "Assets/Art/Materials/Bench";
    const string StageName = "Stage (v2)";

    // The close-up camera and the inspect point as the scene had them before the tabletop (Undo puts them back).
    static readonly Vector3 OldCameraLocal = new Vector3(0f, .25f, -.55f);
    static readonly Vector3 OldCameraEuler = new Vector3(20f, 0f, 0f);
    static readonly Vector3 OldInspectPointLocal = Vector3.zero;
    // The tabletop (8 Oct): the camera 0.58 m from the table point, looking down at it at 58°.
    const float CameraPitch = 58f, CameraDistance = .58f;
    // The bench's three slots as the scene had them (in the Workbench's frame), for Undo. With the tabletop they sit on
    // the mat: the first at the table point, so a device set down is already where the close-up works on it, the other
    // two at the mat's front corners (a device set down used to appear on the tool organiser half a metre to the left:
    // Mansoor, 8 Oct, "this random prop on the table whenever I place an item").
    static readonly (string name, Vector3 local)[] OldSlots =
    {
        ("Slot_0", new Vector3(-.362f, .55f, -.176f)), ("Slot_1", new Vector3(0f, .55f, 0f)), ("Slot_2", new Vector3(.337f, .55f, 0f)),
    };
    static readonly (string name, Vector3 fromTable)[] MatSlots =
    {
        ("Slot_0", Vector3.zero), ("Slot_1", new Vector3(-.12f, 0f, -.115f)), ("Slot_2", new Vector3(.09f, 0f, -.125f)),
    };

    static readonly (ToolType tool, string file, string name, Color tint)[] Tools =
    {
        (ToolType.Brush, "BT_Brush", "Cleaning Brush", new Color(0.7882353f, 0.63529414f, 0.15294118f)),
        (ToolType.Screwdriver, "BT_Screwdriver", "Screwdriver", new Color(0.2901961f, 0.49803922f, 0.7490196f)),
        (ToolType.Pry, "BT_PryTool", "Pry Tool", new Color(0.7607843f, 0.33333334f, 0.23921569f)),
        (ToolType.Tweezers, "BT_Tweezers", "Tweezers", new Color(0.49803922f, 0.6901961f, 0.4117647f)),
        (ToolType.Cloth, "BT_Cloth", "Glass Cloth", new Color(0.44f, 0.61f, 0.77f)),
    };

    // ------------------------------------------------------------------ the stage

    [MenuItem(Menu + "Bench 2 - Build the bench stage (the scene)")]
    static void BuildStage()
    {
        if (!CafeSceneOpen()) return;
        BenchRig rig = UnityEngine.Object.FindAnyObjectByType<BenchRig>(FindObjectsInactive.Include);
        if (rig == null) { Debug.LogError(Tag + "No BenchRig (the Workbench's InspectRig) in the scene."); return; }
        Transform rigT = rig.transform;
        Transform existing = rigT.Find(StageName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Bench 2 - Build the bench stage");

        // The real bench top, by a ray down through the rig (the first stage was laid off the rig and floated 22 cm above
        // the wood: Mansoor, 8 Oct, "the tray or the tools … are floating on the table"). The stage's own pieces are gone
        // by now, so the ray meets the bench.
        Physics.SyncTransforms();
        float benchTop = rigT.position.y - .35f;
        if (Physics.Raycast(rigT.position + Vector3.up * .05f, Vector3.down, out RaycastHit top, 2f, ~0, QueryTriggerInteraction.Ignore))
            benchTop = top.point.y;
        else Debug.LogWarning(Tag + "No bench under the inspect rig: the mat is laid 35 cm under the rig.");
        float matTop = benchTop + .006f - rigT.position.y;   // rig-local y of the mat's top (the rig is unturned, world scale 1)

        var stage = new GameObject(StageName);
        Undo.RegisterCreatedObjectUndo(stage, "stage");
        stage.transform.SetParent(rigT, false);

        Material matMaterial = MatMaterial();
        Material steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/DC_SteelDark.mat");
        Material steelLight = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/DC_Steel.mat");
        if (steelLight == null) steelLight = steel;
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/CC_Wood_Espresso.mat");

        // The mat: a slab on the bench top under the inspect point; the device lies on it. Its collider is thicker than
        // it looks and reaches down, so a falling screw can't slip through it.
        const float matZ = -.02f;     // a touch toward the camera, clear of the props standing at the back of the bench
        GameObject mat = Slab("Mat", stage.transform, new Vector3(0f, matTop - .003f, matZ), new Vector3(.60f, .006f, .36f), matMaterial);
        var matBox = mat.GetComponent<BoxCollider>();
        matBox.center = new Vector3(0f, -2.5f, 0f);
        matBox.size = new Vector3(1f, 6f, 1f);
        mat.AddComponent<Rigidbody>().isKinematic = true;        // a kinematic slab: continuous collision sees it
        mat.GetComponent<Collider>().sharedMaterial = SlipperyMat();

        // The parts tray: a floor and four low walls, to the right of the device; its magnet zone a little above the floor.
        // 16 x 16 cm inside: the stand-in phone's screen is 12 cm long and has to lie flat in it whichever way it lands
        // (the first tray was 13 x 10 and the screen lay across its wall, 7 Oct), and the fresh screen at the device's
        // side and the broken one dropped beside it have to lie side by side across it, 6 cm wide each (8 Oct).
        var tray = new GameObject("Parts tray");
        Undo.RegisterCreatedObjectUndo(tray, "tray");
        tray.transform.SetParent(stage.transform, false);
        tray.transform.localPosition = new Vector3(.225f, matTop + .003f, matZ);
        const float tw = .17f, td = .17f;     // outside
        GameObject trayFloor = Slab("Floor", tray.transform, Vector3.zero, new Vector3(tw, .006f, td), steelLight);   // light, so a dark part reads in it
        trayFloor.GetComponent<Collider>().sharedMaterial = SlipperyMat();
        float h = .024f, t = .005f;
        Slab("Wall N", tray.transform, new Vector3(0f, h * .5f, td * .5f - t * .5f), new Vector3(tw, h, t), steel);
        Slab("Wall S", tray.transform, new Vector3(0f, h * .5f, -td * .5f + t * .5f), new Vector3(tw, h, t), steel);
        Slab("Wall E", tray.transform, new Vector3(tw * .5f - t * .5f, h * .5f, 0f), new Vector3(t, h, td), steel);
        Slab("Wall W", tray.transform, new Vector3(-tw * .5f + t * .5f, h * .5f, 0f), new Vector3(t, h, td), steel);
        var zoneGo = new GameObject("Magnet zone");
        Undo.RegisterCreatedObjectUndo(zoneGo, "zone");
        zoneGo.transform.SetParent(tray.transform, false);
        var zone = zoneGo.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, .03f, 0f);
        zone.size = new Vector3(tw - t * 2f, .07f, td - t * 2f);

        // The tool caddy: a wooden block to the left, the five tools standing in it, points down, leaning back a little.
        var caddy = Slab("Tool caddy", stage.transform, new Vector3(-.245f, matTop + .0175f, matZ - .02f), new Vector3(.12f, .035f, .08f), wood);
        var slots = new Transform[Tools.Length];
        for (int i = 0; i < Tools.Length; i++)
        {
            var slot = new GameObject("Slot " + Tools[i].tool);
            Undo.RegisterCreatedObjectUndo(slot, "slot");
            // Under the stage (unscaled), not the scaled slab: a turned child of a stretched cube would come out sheared.
            slot.transform.SetParent(stage.transform, false);
            float x = Mathf.Lerp(-.045f, .045f, i / (float)(Tools.Length - 1));
            // 12 mm into the caddy's top (the tool's tip is its origin), so each tool stands in its hole.
            slot.transform.localPosition = caddy.transform.localPosition + new Vector3(x, .0175f - .012f, 0f);
            // The handle leans back (toward +Z, away from the inspection camera at -Z); flat tools show their flat side (+Z) to the camera.
            slot.transform.localRotation = Quaternion.AngleAxis(18f, Vector3.right) * Quaternion.AngleAxis(180f, Vector3.up);
            slots[i] = slot.transform;
        }

        // The tools: the models, each a ToolPickup with a box to click on; the old cylinders switched off.
        int placed = 0;
        for (int i = 0; i < Tools.Length; i++)
        {
            var (tool, file, name, tint) = Tools[i];
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{file}.fbx");
            if (asset == null) { Debug.LogWarning(Tag + $"{file}.fbx is not in {ModelFolder}: run Bench tools 1 first; the {name} is skipped."); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, slots[i]);
            Undo.RegisterCreatedObjectUndo(go, "tool");
            go.name = "Tool_" + tool;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var pickup = go.AddComponent<ToolPickup>();
            pickup.tool = tool;
            pickup.displayName = name;
            pickup.tint = tint;
            // A box round the whole model to click on (the FBX has no colliders), at least 14 mm across so a thin tool is hittable.
            Bounds b = LocalBounds(go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = new Vector3(Mathf.Max(b.size.x, .014f), Mathf.Max(b.size.y, .02f), Mathf.Max(b.size.z, .014f));
            placed++;
        }
        foreach (Transform child in rigT)
            if (child.name.StartsWith("Tool_", StringComparison.Ordinal) && child.GetComponent<MeshFilter>() != null && child.GetComponent<ToolPickup>() != null)
            {
                Undo.RecordObject(child.gameObject, "old tool off");
                child.gameObject.SetActive(false);
            }

        BenchStage bench = rig.GetComponent<BenchStage>();
        if (bench == null) bench = Undo.AddComponent<BenchStage>(rig.gameObject);
        Undo.RecordObject(bench, "wire");
        bench.Wire(mat.transform, trayFloor.transform, zone, caddy.transform, slots);
        EditorUtility.SetDirty(bench);

        // The tabletop: the inspect point on the mat (the device lies there) and the close-up camera above it, looking
        // down at it, instead of the old 20° camera 55 cm back from a device hanging in the air.
        Transform point = rigT.Find("InspectPoint");
        Transform camera = rigT.Find("CM_InspectCam");
        if (point != null && camera != null)
        {
            Undo.RecordObject(point, "inspect point");
            Undo.RecordObject(camera, "close-up camera");
            point.localPosition = new Vector3(0f, matTop, matZ);
            float pitch = CameraPitch * Mathf.Deg2Rad;
            camera.localPosition = point.localPosition + new Vector3(0f, Mathf.Sin(pitch) * CameraDistance, -Mathf.Cos(pitch) * CameraDistance);
            camera.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
        }
        else Debug.LogWarning(Tag + "InspectPoint or CM_InspectCam not found under the rig: the camera is left as it was.");

        // The bench's slots onto the mat.
        Vector3 tableWorld = rigT.TransformPoint(new Vector3(0f, matTop, matZ));
        int moved = 0;
        foreach (var (name, fromTable) in MatSlots)
        {
            Transform slot = FindDeep(rigT.parent, name);
            if (slot == null) continue;
            Undo.RecordObject(slot, "bench slot");
            slot.position = tableWorld + fromTable;
            slot.rotation = Quaternion.identity;
            moved++;
        }
        if (moved < MatSlots.Length) Debug.LogWarning(Tag + $"Only {moved} of the bench's 3 slots were found under the Workbench.");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log(Tag + $"The bench stage is built on the bench top (y {benchTop:0.000}): the mat, the parts tray (magnetic), the caddy with {placed} tools; " +
                  "the inspect point on the mat and the close-up camera above it; the old tool column is off. Saved. Undo: Bench 2 - Undo: back to the tool column.");
    }

    [MenuItem(Menu + "Bench 2 - Undo: back to the tool column")]
    static void UndoStage()
    {
        if (!CafeSceneOpen()) return;
        BenchRig rig = UnityEngine.Object.FindAnyObjectByType<BenchRig>(FindObjectsInactive.Include);
        if (rig == null) return;
        Transform stage = rig.transform.Find(StageName);
        if (stage != null) Undo.DestroyObjectImmediate(stage.gameObject);
        foreach (Transform child in rig.transform)
            if (child.name.StartsWith("Tool_", StringComparison.Ordinal) && child.GetComponent<MeshFilter>() != null)
            {
                Undo.RecordObject(child.gameObject, "old tool on");
                child.gameObject.SetActive(true);
            }
        BenchStage bench = rig.GetComponent<BenchStage>();
        if (bench != null) Undo.DestroyObjectImmediate(bench);
        Transform point = rig.transform.Find("InspectPoint");
        Transform camera = rig.transform.Find("CM_InspectCam");
        if (point != null) { Undo.RecordObject(point, "inspect point"); point.localPosition = OldInspectPointLocal; }
        if (camera != null) { Undo.RecordObject(camera, "camera"); camera.localPosition = OldCameraLocal; camera.localRotation = Quaternion.Euler(OldCameraEuler); }
        foreach (var (name, local) in OldSlots)
        {
            Transform slot = FindDeep(rig.transform.parent, name);
            if (slot == null) continue;
            Undo.RecordObject(slot, "bench slot");
            slot.localPosition = local;
            slot.localRotation = Quaternion.identity;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log(Tag + "The tool column is back, the stage is gone, the camera and the inspect point are where they were. Saved.");
    }

    static GameObject Slab(string name, Transform parent, Vector3 localPos, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Material MatMaterial()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "Bench");
        string path = MaterialFolder + "/BM_Mat.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BM_Mat" };
            m.SetColor("_BaseColor", new Color(.31f, .42f, .54f));     // a cutting mat's blue-grey
            m.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    // The mat's and the tray's surface: a cutting mat grips (a screw that lands stops where it lands, rather than sliding
    // and spinning for seconds, 8 Oct), with a little give.
    static PhysicsMaterial SlipperyMat()
    {
        string path = MaterialFolder + "/Bench mat (physics).physicMaterial";
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm == null)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "Bench");
            pm = new PhysicsMaterial("Bench mat");
            AssetDatabase.CreateAsset(pm, path);
        }
        pm.dynamicFriction = .4f; pm.staticFriction = .5f; pm.bounciness = .08f;
        pm.frictionCombine = PhysicsMaterialCombine.Average; pm.bounceCombine = PhysicsMaterialCombine.Average;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static Bounds LocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<MeshFilter>(true);
        bool first = true;
        var b = new Bounds();
        foreach (MeshFilter f in renderers)
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 local = root.InverseTransformPoint(f.transform.TransformPoint(corner));
                if (first) { b = new Bounds(local, Vector3.zero); first = false; } else b.Encapsulate(local);
            }
        }
        return b;
    }

    static bool CafeSceneOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play first."); return false; }
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ the lab

    [MenuItem(Menu + "Bench 3 - The bench - play check (lab, drives itself)")]
    static void BenchLabStart()
    {
        if (!CafeSceneOpen()) return;
        if (UnityEngine.Object.FindAnyObjectByType<BenchStage>(FindObjectsInactive.Include) == null)
        {
            Debug.LogError(Tag + "No bench stage in the scene: run Bench 2 - Build the bench stage first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(BenchLab.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "The bench - play check: a lab session on a fresh Day 1 that drives the bench by itself with a virtual gamepad. Keep the Game view " +
                  $"in front and leave the mouse and keyboard alone until it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.GetInt(BenchLab.PendingKey, 0) != 0) { PlayerPrefs.DeleteKey(BenchLab.PendingKey); PlayerPrefs.Save(); }
    }
}
#endif
