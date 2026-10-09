#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ---------------------------------------------------------------------------
// GRACE'S COVER (playtest 3 session 5, 9 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §6.1)
//
//   Fixit Fidget > Night > Break-ins 8 - Grace's cover: her breakfast bar, the boxes, real colliders (scene)
//   Fixit Fidget > Night > Break-ins 8 - Take Grace's cover back out (scene)
//   Fixit Fidget > Night > Break-ins 8 - Grace's cover (lab, a check)          (in GraceAtHomeSteps, beside her other labs)
//
// His playtest 3: "crouch behind real cover; the hide button got him caught". The hide buttons are out (GraceAtHome); Grace
// aims at Ace's capsule, so a crouched Ace (1.0 m) behind anything 1.1 m tall is hidden from more than about 1.5 m. This
// step puts the cover in her house, as built by Break-ins 1 (nothing else moves), with undo and a record (GraceHouseCover):
//
//   * her breakfast bar (GH_KitchenIsland, 1.12 m, from the Blender session) behind her armchair, its doors toward the
//     street, at plan X 2.45-3.55, Y 2.47-3.10: from her kitchen and the way across to her chair, a crouched Ace behind it
//     (by her front window, or in the entry) is out of sight; from the entry side it is not (cover has a side). Out of
//     the kitchen lane on purpose: the front room past her armchair was already a dead end for Ace's 0.77 m, so the bar
//     takes floor nobody walked, and every way of hers keeps clear of it (GraceHouseMap, the rule checks);
//   * the boxes she's sorting (GH_BoxStack, 1.15 m) at the foot of her bed, against it: woken, looking round from her
//     pillow, she doesn't see a crouched Ace on their far side;
//   * real colliders: her armchair, her standard lamp, the coat stand, her bed and the dressing table each had a box the
//     size of their whole bounds (the lamp and the coat stand were invisible pillars, the armchair's box filled the air
//     over its seat, and her eye in bed was inside the bed's box, where a ray ignores the collider it starts in); they,
//     the bar and the boxes now collide as they look (mesh colliders of their own meshes);
//   * GraceHouse's furniture list: each tall piece and what stands on it (the kettle on the counter, the quilts on her
//     bed), which fades like her walls while it stands between the camera and Ace.
//
// The report measures the cover with rays, in Edit Mode, from her eyes. Safe to run again (it refreshes what it finds).
// Save the scene afterwards. Every piece keeps a collider of its own, so the night's list of what is solid (Night walk 2)
// doesn't change.
// ---------------------------------------------------------------------------
internal static class GraceCoverSteps
{
    const string Tag = "[Break-ins 8] ";
    const string Menu = "Fixit Fidget/Night/";
    const string HouseName = "1 - Saffron bay-window house";
    const string ModelFolder = "Assets/Art/Models/GraceHouse/";
    const string UndoName = "Grace's cover";
    const float Up = 2.40f;

    // Plan metres (GraceHouseMap.Furniture has the footprints): the bar's middle and turn (0: its doors toward the street);
    // the boxes' (90: their long side across the foot of the bed).
    const float BarX = 3.00f, BarY = 2.785f, BarTurn = 0f;
    const float BoxesX = 3.194f, BoxesY = 2.98f, BoxesTurn = 90f;
    const string Bar = "GH_KitchenIsland", Boxes = "GH_BoxStack";
    static readonly string[] Reshape = { "GH_Armchair", "GH_StandardLamp", "GH_CoatStand", "GH_Bed", "GH_DressingTable" };

    // Her furniture that fades (GraceHouse.furniture): each piece and its floor; what stands on it is found by its bounds.
    static readonly (string piece, string name, int storey)[] Faders =
    {
        ("GH_KitchenCounter_Short", "the kitchen counter", 0), ("GH_Fridge", "the fridge", 0), ("GH_Armchair", "her armchair", 0),
        ("GH_StandardLamp", "her standard lamp", 0), ("GH_Sideboard", "the sideboard", 0), ("GH_CoatStand", "the coat stand", 0),
        (Bar, "her breakfast bar", 0),
        ("GH_Bed", "her bed", 1), ("GH_BedsideTable", "the bedside table", 1), ("GH_Wardrobe", "the wardrobe", 1),
        ("GH_DressingTable", "the dressing table", 1), (Boxes, "the boxes at the foot of her bed", 1),
    };
    // Never faded with a piece: the rugs, the doors, the stairs, the TV and its cabinet (GraceAtHome swaps the screen's
    // material while she watches, and the two stand against the wall where nothing can be behind them).
    static readonly string[] NeverFaded = { "GH_Rug", "GH_BedroomDoor", "GH_InteriorDoor", "GH_Stairs", "GH_TV" };

    // Where the report measures the cover from (plan metres; Z is her eye's height over the floor she's on).
    static readonly Vector3 KettleEye = new Vector3(3.40f, 1.05f, 1.65f), KitchenEye = new Vector3(3.90f, 1.45f, 1.65f);
    static readonly Vector3 CrossingEye = new Vector3(4.60f, 2.10f, 1.65f), PillowEye = new Vector3(5.05f, 3.28f, Up + .75f);
    static readonly Vector3 BehindTheBar = new Vector3(2.30f, 3.50f, 0f), BehindTheBoxes = new Vector3(2.55f, 2.98f, Up);

    [MenuItem(Menu + "Break-ins 8 - Grace's cover: her breakfast bar, the boxes, real colliders (scene)")]
    static void Put()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName) ?? throw new InvalidOperationException($"'{HouseName}' isn't in the scene.");
            Transform root = house.Find(GraceHouseSteps.RootName) ?? throw new InvalidOperationException("Grace's house isn't built inside yet (Break-ins 1).");
            GraceHouse runner = root.GetComponent<GraceHouse>();
            if (runner == null) throw new InvalidOperationException("Her rooms' root has no GraceHouse.");
            Transform frontRoom = root.Find("Ground floor/Front room") ?? throw new InvalidOperationException("No 'Ground floor/Front room' in her house (Break-ins 1).");
            Transform bedroom = root.Find("First floor/Bedroom") ?? throw new InvalidOperationException("No 'First floor/Bedroom' in her house (Break-ins 1).");

            // Everything is found and checked before anything changes.
            var (barAsset, barMesh, barToRoot) = Model(Bar);
            var (boxesAsset, boxesMesh, boxesToRoot) = Model(Boxes);
            GraceHouseMap.Footprint barWants = GraceHouseMap.Furniture[GraceHouseMap.BreakfastBar];
            GraceHouseMap.Footprint boxesWant = GraceHouseMap.Furniture[GraceHouseMap.Boxes];
            Rect barGoes = Footprint(barMesh.sharedMesh.bounds, barToRoot, BarX, BarY, BarTurn);
            Rect boxesGo = Footprint(boxesMesh.sharedMesh.bounds, boxesToRoot, BoxesX, BoxesY, BoxesTurn);
            Expect(barGoes, barWants, "her breakfast bar");
            Expect(boxesGo, boxesWant, "the boxes");
            var reshape = new List<Transform>();
            foreach (string name in Reshape)
            {
                Transform t = Find(root, name) ?? throw new InvalidOperationException($"No {name} in her house (Break-ins 1).");
                MeshFilter shape = t.GetComponent<MeshFilter>();
                if (shape == null || shape.sharedMesh == null) throw new InvalidOperationException($"{name} has no mesh.");
                reshape.Add(t);
            }
            bool pillowWasInside = Inside(runner.World(PillowEye.x, PillowEye.y, PillowEye.z));

            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            GraceHouseCover record = root.GetComponent<GraceHouseCover>();
            bool again = record != null;
            if (record == null)
            {
                record = Undo.AddComponent<GraceHouseCover>(root.gameObject);
                record.furnitureBefore = runner.furniture ?? Array.Empty<GraceHouse.Piece>();
            }
            else Undo.RecordObject(record, UndoName);

            // ---- the bar and the boxes ----
            Transform bar = Place(frontRoom, Bar, barAsset, barMesh, barToRoot, BarX, BarY, 0f, BarTurn);
            Transform boxes = Place(bedroom, Boxes, boxesAsset, boxesMesh, boxesToRoot, BoxesX, BoxesY, Up, BoxesTurn);
            record.added = new[] { bar.gameObject, boxes.gameObject };
            report.AppendLine($"Her breakfast bar ({Bar}): behind her armchair, its doors toward the street, at plan X {barGoes.xMin:0.00}-{barGoes.xMax:0.00}, " +
                              $"Y {barGoes.yMin:0.00}-{barGoes.yMax:0.00}, {MeshTop(barMesh.sharedMesh, barToRoot):0.00} m tall.");
            report.AppendLine($"The boxes ({Boxes}): at the foot of her bed, against it, at plan X {boxesGo.xMin:0.000}-{boxesGo.xMax:0.000}, " +
                              $"Y {boxesGo.yMin:0.00}-{boxesGo.yMax:0.00}, {MeshTop(boxesMesh.sharedMesh, boxesToRoot):0.00} m tall.");

            // ---- real colliders ----
            var reshaped = new List<Transform>(record.reshaped.Where(t => t != null));
            var centres = new List<Vector3>(record.boxCentreBefore);
            var sizes = new List<Vector3>(record.boxSizeBefore);
            while (centres.Count < reshaped.Count) centres.Add(Vector3.zero);
            while (sizes.Count < reshaped.Count) sizes.Add(Vector3.zero);
            foreach (Transform t in reshape)
            {
                BoxCollider box = t.GetComponent<BoxCollider>();
                if (box != null)
                {
                    if (!reshaped.Contains(t))
                    {
                        reshaped.Add(t);
                        centres.Add(box.center);
                        sizes.Add(box.size);
                    }
                    Undo.DestroyObjectImmediate(box);
                }
                Shape(t);
            }
            Shape(bar);
            Shape(boxes);
            record.reshaped = reshaped.ToArray();
            record.boxCentreBefore = centres.ToArray();
            record.boxSizeBefore = sizes.ToArray();
            report.AppendLine($"Real colliders (their own meshes): {string.Join(", ", reshape.Select(t => t.name))}, the bar and the boxes " +
                              $"({reshaped.Count} bounds box(es) on record, to put back).");

            // ---- the furniture that fades ----
            var hung = new HashSet<Renderer>(runner.walls.Where(w => w != null).SelectMany(w => w.hanging ?? Array.Empty<Renderer>()));
            var candidates = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("GH_", StringComparison.Ordinal) && !hung.Contains(r)
                            && !NeverFaded.Any(n => r.name.StartsWith(n, StringComparison.Ordinal))
                            && !Faders.Any(f => f.piece == r.name))
                .ToList();
            // What stands on what: a thing goes with the piece it rests on (it starts within that piece's height, 10 cm or
            // more over its foot, their boxes touching), the one under its middle if there are two (a frame on the sideboard,
            // beside the standard lamp's shade), in plan metres.
            var mains = new List<(string pieceName, string name, int storey, Renderer main, Vector3 min, Vector3 max)>();
            foreach (var (pieceName, name, storey) in Faders)
            {
                Transform t = Find(root, pieceName);
                Renderer main = t != null ? t.GetComponent<Renderer>() : null;
                if (main == null) { report.AppendLine($"  (no {pieceName} in her house: {name} doesn't fade)"); continue; }
                var (min, max) = PlanBox(runner, main);
                mains.Add((pieceName, name, storey, main, min, max));
            }
            var standing = mains.ToDictionary(m => m.main, m => new List<Renderer> { m.main });
            foreach (Renderer r in candidates)
            {
                var (cMin, cMax) = PlanBox(runner, r);
                Vector3 middle = (cMin + cMax) * .5f;
                int best = -1;
                float bestArea = float.MaxValue;
                bool bestHolds = false;
                for (int i = 0; i < mains.Count; i++)
                {
                    var m = mains[i];
                    bool touch = cMin.x <= m.max.x + .06f && cMax.x >= m.min.x - .06f && cMin.y <= m.max.y + .06f && cMax.y >= m.min.y - .06f
                                 && cMin.z <= m.max.z + .06f && cMax.z >= m.min.z - .06f;
                    if (!touch || cMin.z < m.min.z + .10f || cMin.z > m.max.z + .05f) continue;
                    bool holds = middle.x >= m.min.x && middle.x <= m.max.x && middle.y >= m.min.y && middle.y <= m.max.y;
                    float area = (m.max.x - m.min.x) * (m.max.y - m.min.y);
                    if (best < 0 || holds && !bestHolds || holds == bestHolds && area < bestArea)
                    {
                        best = i;
                        bestArea = area;
                        bestHolds = holds;
                    }
                }
                if (best >= 0) standing[mains[best].main].Add(r);
            }
            var pieces = new List<GraceHouse.Piece>();
            foreach (var m in mains)
            {
                List<Renderer> parts = standing[m.main];
                pieces.Add(new GraceHouse.Piece { name = m.name, storey = m.storey, parts = parts.ToArray() });
                report.AppendLine($"  fades: {m.name} (floor {m.storey}){(parts.Count > 1 ? ", with " + string.Join(", ", parts.Skip(1).Select(r => r.name)) : "")}");
            }
            Undo.RecordObject(runner, UndoName);
            runner.furniture = pieces.ToArray();
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(record);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            report.Insert(0, $"Her furniture that fades in the overhead view: {pieces.Count} pieces.\n");

            // ---- the cover, measured from her eyes (Edit Mode: her rooms are solid as built) ----
            Physics.SyncTransforms();
            report.AppendLine();
            report.AppendLine("The cover, measured from her eyes (a crouched Ace: his 1.0 m capsule's head 0.80 m and chest 0.55 m; standing: 1.80 and 1.10 m):");
            report.AppendLine(Sight(runner, "from the kettle", KettleEye, BehindTheBar, 0f));
            report.AppendLine(Sight(runner, "from the kitchen", KitchenEye, BehindTheBar, 0f));
            report.AppendLine(Sight(runner, "from the way across to her chair", CrossingEye, BehindTheBar, 0f));
            report.AppendLine(Sight(runner, "woken, from her pillow", PillowEye, BehindTheBoxes, Up));
            bool pillowInside = Inside(runner.World(PillowEye.x, PillowEye.y, PillowEye.z));
            report.AppendLine($"Her eye in bed: {(pillowInside ? "STILL inside a collider" : "in the open over her pillow")} " +
                              $"({(pillowWasInside ? "it was inside the bed's bounds box" : "it was already in the open")}).");
            Debug.Log(Tag + $"Grace's cover {(again ? "refreshed" : "put in")}. Save the scene (Ctrl+S); then Break-ins 8's lab and Break-ins 7's checks.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting Grace's cover in FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 8 - Take Grace's cover back out (scene)")]
    static void TakeOut()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            GraceHouseCover record = UnityEngine.Object.FindAnyObjectByType<GraceHouseCover>(FindObjectsInactive.Include);
            if (record == null) { Debug.Log(Tag + "Grace's cover isn't in this scene."); return; }
            GraceHouse runner = record.GetComponent<GraceHouse>();
            Undo.SetCurrentGroupName("Take Grace's cover back out");
            int group = Undo.GetCurrentGroup();
            int removed = 0, restored = 0;
            foreach (GameObject go in record.added)
                if (go != null) { Undo.DestroyObjectImmediate(go); removed++; }
            for (int i = 0; i < record.reshaped.Length; i++)
            {
                Transform t = record.reshaped[i];
                if (t == null) continue;
                MeshCollider shape = t.GetComponent<MeshCollider>();
                if (shape != null) Undo.DestroyObjectImmediate(shape);
                BoxCollider box = t.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(t.gameObject);
                box.center = i < record.boxCentreBefore.Length ? record.boxCentreBefore[i] : Vector3.zero;
                box.size = i < record.boxSizeBefore.Length ? record.boxSizeBefore[i] : Vector3.one;
                restored++;
            }
            if (runner != null)
            {
                Undo.RecordObject(runner, "Take Grace's cover back out");
                runner.furniture = record.furnitureBefore ?? Array.Empty<GraceHouse.Piece>();
                EditorUtility.SetDirty(runner);
            }
            Undo.DestroyObjectImmediate(record);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(Tag + $"Grace's cover is out: {removed} piece(s) gone, {restored} bounds box(es) back, her furniture no longer fades. Save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking Grace's cover back out FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 8 - Grace's cover: her breakfast bar, the boxes, real colliders (scene)", true)]
    [MenuItem(Menu + "Break-ins 8 - Take Grace's cover back out (scene)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== placing

    static (GameObject asset, MeshFilter mesh, Matrix4x4 toRoot) Model(string model)
    {
        string path = ModelFolder + model + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException($"{path} isn't there (Fixit Fidget > Night > Grace's cover 1 imports it).");
        MeshFilter source = asset.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null)
            ?? throw new InvalidOperationException($"{path} has no mesh.");
        return (asset, source, asset.transform.worldToLocalMatrix * source.transform.localToWorldMatrix);
    }

    // As Break-ins 1 places a piece (GraceHouseSteps.Piece): by plan (X, Y, z, turn; turn 0 faces the street, 90 north), in
    // the rooms' own space (-X, z, Y). Made once; moved back to its place when the step runs again.
    static Transform Place(Transform parent, string model, GameObject asset, MeshFilter source, Matrix4x4 toRoot, float X, float Y, float z, float turn)
    {
        Transform t = parent.Find(model);
        if (t == null)
        {
            var go = new GameObject(model);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            t = go.transform;
        }
        else Undo.RecordObject(t, UndoName);
        Quaternion yaw = Quaternion.Euler(0f, -turn, 0f);
        t.localPosition = new Vector3(-X, z, Y) + yaw * (Vector3)toRoot.GetColumn(3);
        t.localRotation = yaw * toRoot.rotation;
        t.localScale = Vector3.one;
        MeshFilter f = t.GetComponent<MeshFilter>();
        Undo.RecordObject(f, UndoName);
        f.sharedMesh = source.sharedMesh;
        MeshRenderer r = t.GetComponent<MeshRenderer>();
        Undo.RecordObject(r, UndoName);
        r.sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
        return t;
    }

    // A mesh collider of the piece's own mesh, in place of whatever it had.
    static void Shape(Transform t)
    {
        foreach (BoxCollider box in t.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(box);
        MeshCollider shape = t.GetComponent<MeshCollider>();
        if (shape == null) shape = Undo.AddComponent<MeshCollider>(t.gameObject);
        Undo.RecordObject(shape, UndoName);
        shape.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
        shape.convex = false;
        shape.isTrigger = false;
    }

    // The plan footprint a model would have, placed at (X, Y) turned by turn: its mesh's box, through the same transform.
    static Rect Footprint(Bounds mesh, Matrix4x4 toRoot, float X, float Y, float turn)
    {
        Quaternion yaw = Quaternion.Euler(0f, -turn, 0f);
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x, (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
            Vector3 local = new Vector3(-X, 0f, Y) + yaw * toRoot.MultiplyPoint3x4(corner);   // the rooms' space: (-X, z, Y)
            x0 = Mathf.Min(x0, -local.x); x1 = Mathf.Max(x1, -local.x);
            y0 = Mathf.Min(y0, local.z); y1 = Mathf.Max(y1, local.z);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static float MeshTop(Mesh mesh, Matrix4x4 toRoot)
    {
        float top = float.MinValue, bottom = float.MaxValue;
        Bounds m = mesh.bounds;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? m.min.x : m.max.x, (i & 2) == 0 ? m.min.y : m.max.y, (i & 4) == 0 ? m.min.z : m.max.z);
            float y = toRoot.MultiplyPoint3x4(corner).y;
            top = Mathf.Max(top, y);
            bottom = Mathf.Min(bottom, y);
        }
        return top - bottom;
    }

    // Placed where the map says it stands (GraceHouseMap.Furniture, which her ways are checked against), within 3 cm.
    static void Expect(Rect goes, GraceHouseMap.Footprint wants, string what)
    {
        float off = Mathf.Max(Mathf.Max(Mathf.Abs(goes.xMin - wants.X0), Mathf.Abs(goes.xMax - wants.X1)),
                              Mathf.Max(Mathf.Abs(goes.yMin - wants.Y0), Mathf.Abs(goes.yMax - wants.Y1)));
        if (off > .03f)
            throw new InvalidOperationException($"{what} would stand at plan X {goes.xMin:0.000}-{goes.xMax:0.000}, Y {goes.yMin:0.000}-{goes.yMax:0.000}, " +
                                                $"not where her map has it ({wants.name}: X {wants.X0:0.000}-{wants.X1:0.000}, Y {wants.Y0:0.000}-{wants.Y1:0.000}); nothing was changed.");
    }

    // The box a renderer's mesh fills, in plan metres (X, Y, z), whichever way her house stands in the world.
    static (Vector3 min, Vector3 max) PlanBox(GraceHouse runner, Renderer r)
    {
        MeshFilter f = r.GetComponent<MeshFilter>();
        Bounds m = f != null && f.sharedMesh != null ? f.sharedMesh.bounds : new Bounds(r.transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
        Matrix4x4 w = r.transform.localToWorldMatrix;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? m.min.x : m.max.x, (i & 2) == 0 ? m.min.y : m.max.y, (i & 4) == 0 ? m.min.z : m.max.z);
            Vector3 p = runner.Plan(w.MultiplyPoint3x4(corner));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    // ================================================================== measuring

    static readonly Collider[] Overlap = new Collider[8];
    static readonly RaycastHit[] Hits = new RaycastHit[16];

    static bool Inside(Vector3 world) => Physics.OverlapSphereNonAlloc(world, .02f, Overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) > 0;

    // From her eye to a crouched Ace's head and chest at a spot (and a standing one's): what's in the way, if anything.
    static string Sight(GraceHouse runner, string what, Vector3 eyePlan, Vector3 acePlan, float floor)
    {
        Vector3 eye = runner.World(eyePlan.x, eyePlan.y, eyePlan.z);
        string Point(float up)
        {
            Vector3 to = runner.World(acePlan.x, acePlan.y, floor + up);
            string hit = FirstHit(eye, to);
            return $"{up:0.00} m {(hit == null ? "SEEN" : "hidden by " + hit)}";
        }
        return $"  {what} ({eyePlan.x:0.00}, {eyePlan.y:0.00}) to ({acePlan.x:0.00}, {acePlan.y:0.00}): crouched {Point(.80f)}, {Point(.55f)}; standing {Point(1.80f)}, {Point(1.10f)}";
    }

    static string FirstHit(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        int n = Physics.RaycastNonAlloc(from, d / length, Hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        string name = null;
        for (int i = 0; i < n; i++)
            if (Hits[i].collider != null && Hits[i].distance < nearest) { nearest = Hits[i].distance; name = Hits[i].collider.name; }
        return name;
    }

    static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = Find(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
#endif
