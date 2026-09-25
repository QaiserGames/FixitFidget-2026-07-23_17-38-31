#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Café furnishing pass (24 Sept).
//
// The room had its gameplay objects and architecture but read as a sparse,
// functional hall. This furnishes and layers it without moving anything that
// gameplay depends on (tables, seats, waiting spots, queue, stations, cup spots).
// Everything is built from assets already in the project: our own furniture
// library (Assets/Art/Models - lounge tables, tub chairs, bar tables and stools,
// mugs, plants, the A-frame, the counter sign), the reading bookcase's book
// colours, the café palette materials, a few POLYGON City / Generic props, and
// rugs, mats and a chalkboard menu authored for this pass in the café palette
// (Assets/Art/CafeFurnishing).
//
// Zones, so each part of the room has its own character:
//  A  Window lounge   - kilim rug in front of the two window sofas, a low table and
//                        a tub chair at the front sofa, a book and a folded throw on
//                        the rear sofa, a floor lamp at each end.
//  B  Reading nook    - round rug under the table by the bookcase, a plant and
//                        books on the bookcase.
//  C  Window bar      - two high-tops with stools along the courtyard windows,
//                        plants and books on the sill.
//  D  Entrance and    - the A-frame chalkboard out on the patio; a coat stand (hat,
//     waiting nook      scarf, Ace's tote) between the two right-hand waiting spots.
//  E  Counter cubbies - coffee bags, bean jars, stacked mugs, books and a crate of
//                        repair parts in some of the counter's open bays (the rest
//                        stay free for the player's own things later).
//  F  Work areas      - rubber mats at the drink station and the repair bench, a
//                        parts cabinet with a radio behind the bench, coffee sacks
//                        in the back corner.
//  G  Signs and menu  - the café sign on the counter and a chalkboard menu with the
//                        six drinks (prices from the drink definitions) and repairs.
//
// Rules the placement is checked against (the run is refused if any fails):
//  * Floor furniture keeps clear of every seat stand point (0.75 m), waiting spot
//    (0.8 m), queue slot (0.9 m), station stand point (0.8 m) and the customer
//    spawn (1 m), and stays out of the door-to-counter lane.
//  * Floor furniture has a box collider, so the player bumps into it and the
//    customer routes walk round it; the routes are re-baked, and the layout check
//    (every destination reachable) and the occupied-circulation check (every
//    customer position reachable by the player with the room full) must still pass,
//    and every customer position must keep at least 40% of the floor it could be
//    served from, or everything is rolled back. Placement was planned on the
//    check's own reach map ("Map the circulation"): with the room full the player
//    gets around by a few narrow strips - along the sofas, along the right-hand
//    windows, inside the front windows and between the tables - so floor pieces
//    only go where the map shows those strips don't need the floor.
//  * Rugs, mats and small props have no colliders and never change the routes.
//  * Nothing is placed on the dining tables: their cup spots belong to gameplay.
//
// Nothing is saved. Look at the photos (before and after), then save the scene,
// or run "Undo: remove the furnishing" (or reopen the scene without saving).
public static class CafeFurnishing
{
    const string Menu = "Fixit Fidget/Cafe furnishing/";
    const string Tag = "[Cafe furnishing] ";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    public const string RootName = "18 - cafe furnishing";
    const string Art = "Assets/Art/CafeFurnishing";
    const string TextureFolder = Art + "/Textures";
    const string MaterialFolder = Art + "/Materials";
    const string MeshAssetPath = Art + "/Furnishing meshes.asset";
    const string PalettePath = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";
    const string Models = "Assets/Art/Models/";
    const string City = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/Props/";

    [MenuItem(Menu + "1 - Furnish the cafe (rugs, lounge, window bar, detail)")]
    static void FurnishMenu() => Run("Furnish", Furnish);

    [MenuItem(Menu + "Photograph the cafe (game camera, counter camera, each zone)")]
    static void PhotoMenu() => Run("Photos", () => Photograph("furnishing-now"));

    [MenuItem(Menu + "Undo: remove the furnishing")]
    static void RemoveMenu() => Run("Remove", Remove);

    [MenuItem(Menu + "Map the circulation (where the player can reach with every customer in place)")]
    static void MapMenu() => Run("Circulation map", () => CirculationMap("circulation-now"));

    [MenuItem(Menu + "1 - Furnish the cafe (rugs, lounge, window bar, detail)", true)]
    [MenuItem(Menu + "Photograph the cafe (game camera, counter camera, each zone)", true)]
    [MenuItem(Menu + "Undo: remove the furnishing", true)]
    [MenuItem(Menu + "Map the circulation (where the player can reach with every customer in place)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string title, Func<string> step)
    {
        try { Debug.Log(Tag + title + ": " + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED (nothing half-done was kept): " + e); }
    }

    // ------------------------------------------------------------------ state for one run

    static Dictionary<string, Material> palette;
    static readonly Dictionary<string, Material> own = new Dictionary<string, Material>();
    static readonly Dictionary<Mesh, Mesh> grainCopies = new Dictionary<Mesh, Mesh>();
    static readonly List<(Transform t, string what)> floorPieces = new List<(Transform, string)>();
    static readonly List<(Vector3 p, float r, string what)> keepClear = new List<(Vector3, float, string)>();
    static readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> meshCache = new Dictionary<Mesh, (Vector3[], int[])>();
    static readonly List<string> placed = new List<string>();
    static Transform root;

    // ------------------------------------------------------------------ furnish

    static string Furnish()
    {
        CityPackChecks.RequireScene();
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName)
            ?? throw new InvalidOperationException(CafeRootName + " is missing.");
        if (cafe.transform.Find(RootName) != null)
            return "Already furnished (" + RootName + " exists). Run \"Undo: remove the furnishing\" first to redo it.";
        LoadMaterials();
        BuildKeepClear();
        floorPieces.Clear(); placed.Clear(); meshCache.Clear(); grainCopies.Clear();

        string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
        string circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        string mapBefore = CirculationMap("circulation-before");
        var approachBefore = new Dictionary<string, int>(lastApproach);
        string photosBefore = Photograph("furnishing-before");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Furnish the cafe");
        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Furnish the cafe");
        root.SetParent(cafe.transform, false);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        string layoutAfter, circulationAfter, mapAfter = null;
        try
        {
            WindowLounge(Zone("A - Window lounge"));
            ReadingNook(Zone("B - Reading nook"));
            WindowBar(Zone("C - Window bar"));
            Entrance(Zone("D - Entrance and waiting nook"));
            CounterCubbies(Zone("E - Counter cubbies"));
            WorkAreas(Zone("F - Work areas"));
            SignsAndMenu(Zone("G - Signs and menu"));
            CheckFloorClearance();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
            circulationAfter = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
            bool layoutOk = layoutAfter.Contains("PASS") || !layoutBefore.Contains("PASS");
            bool circulationOk = circulationAfter.Contains("PASS") || !circulationBefore.Contains("PASS");
            if (!layoutOk || !circulationOk)
                throw new InvalidOperationException("The furniture made a customer route or delivery approach worse, so nothing was kept.\n"
                    + "Before: " + layoutBefore + "\n        " + circulationBefore + "\nAfter:  " + layoutAfter + "\n        " + circulationAfter);
            mapAfter = CirculationMap("circulation-after");
            string thin = ThinApproaches(approachBefore, lastApproach);
            if (thin != null)
                throw new InvalidOperationException("The furniture left too little floor to serve some customers from, so nothing was kept (map: " + mapAfter + "):\n" + thin);
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            // The refused layout's reach map, so the reason can be read rather than guessed.
            if (mapAfter == null)
            {
                try { Debug.Log(Tag + "Refused layout mapped: " + CirculationMap("circulation-refused")); }
                catch (Exception mapError) { Debug.LogWarning(Tag + "Could not map the refused layout: " + mapError.Message); }
            }
            Undo.RevertAllDownToGroup(group);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            throw;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        string photosAfter = Photograph("furnishing-after");
        var margins = approachBefore.Where(kv => kv.Value > 0)
            .Select(kv => (kv.Key, kept: (lastApproach.TryGetValue(kv.Key, out int n) ? n : 0) / (float)kv.Value))
            .OrderBy(m => m.kept).Take(3).Select(m => $"{m.Key} {m.kept:P0}");

        var report = new StringBuilder("\n");
        foreach (Transform zone in root)
        {
            int pieces = zone.childCount;
            report.AppendLine($"{zone.name}: {pieces} pieces");
        }
        report.AppendLine("Floor furniture with colliders: " + string.Join(", ", floorPieces.Select(f => f.what)));
        report.AppendLine("Layout check before: " + layoutBefore);
        report.AppendLine("Layout check after:  " + layoutAfter);
        report.AppendLine("Circulation before:  " + circulationBefore);
        report.AppendLine("Circulation after:   " + circulationAfter);
        report.AppendLine("Photos: " + photosBefore + " and " + photosAfter);
        report.AppendLine("Reach maps: " + mapBefore + " and " + mapAfter);
        report.AppendLine("Least delivery-approach floor kept: " + string.Join("; ", margins));
        report.Append("Not saved yet: look at the photos, then save the scene to keep it.");
        return report.ToString();
    }

    static string Remove()
    {
        CityPackChecks.RequireScene();
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName)
            ?? throw new InvalidOperationException(CafeRootName + " is missing.");
        var existing = cafe.transform.Find(RootName);
        if (existing == null) return "Nothing to remove: " + RootName + " is not in the scene.";
        Undo.DestroyObjectImmediate(existing.gameObject);
        Physics.SyncTransforms();
        AcesCafeLayoutSetup.BakeRoutes();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Removed the furnishing and re-baked the routes. " + AcesCafeLayoutSetup.ValidateLayout() + " Nothing saved.";
    }

    // ------------------------------------------------------------------ zones

    static void WindowLounge(Transform zone)
    {
        // Kilim in front of the two window sofas (x -6.55..-4.55, z 1.55..7.95),
        // tucked just under their front edge, so the lounge set stands on it.
        Rug(zone, "Lounge kilim", own["Rug kilim"], new Vector3(-5.55f, 0f, 4.75f), new Vector2(2.0f, 6.4f), 0f, true);
        // One conversation set, at the front sofa. The strip between the sofas and the
        // left tables is the player's only way to the window-side seats and the
        // bookcase waiting spot when the room is full; the reach map shows the part
        // south of the gap between the tables is a dead end, so only that part is
        // furnished (a set at the rear sofa cut two customers off). Centred on the
        // sofa; any further south and the chair crowds the window-side seat of the
        // front-left table.
        const float z = 3.3f;
        Furniture(zone, Models + "Table_Lounge.fbx", -5.85f, z, 90f, "Lounge table");
        Furniture(zone, Models + "Chair_TubChair.fbx", -5.02f, z, -84f, "Tub chair", fabric: "InteriorOchre");
        // On the table: a tray with a latte and a little plant, a book beside it.
        // (Heights come from the tray's floor under each item, not its rim.)
        Tray(zone, "Serving tray", -5.84f, z - .2f, 90f);
        Prop(zone, City + "SM_Prop_LargeSign_Coffee_01.prefab", -5.80f, z - .27f, 200f, .065f, "Latte cup", below: .6f);
        Prop(zone, Models + "Plant.fbx", -5.90f, z - .10f, 35f, 1f, "Little plant", below: .6f);
        Books(zone, "Book pair", -5.86f, z + .26f, 8f, 2, .6f);
        // The rear sofa, between its two cushions: a book left open-side down on the
        // seat and a folded throw.
        Books(zone, "Book left on the sofa", -6.70f, 6.35f, 74f, 1, .6f);
        Throw(zone, "Folded throw", -6.74f, 7.35f, 3f);
        FloorLamp(zone, "Floor lamp (front sofa)", -6.95f, 1.38f);
        FloorLamp(zone, "Floor lamp (rear sofa)", -6.95f, 8.72f);
        // Over the gap between the sofas: a hanging plant, and a mug someone left on
        // the window ledge under it; a plant on the ledge past the front sofa.
        HangingPlant(zone, "Hanging plant (between the sofas)", -7.05f, 5.0f, 70f);
        SillProp(zone, Models + "Mug.fbx", -7.50f, 5.02f, 200f, 1f, "Mug left on the ledge");
        SillProp(zone, Models + "Plant.fbx", -7.50f, .80f, 40f, 1.2f, "Ledge plant");
    }

    static void ReadingNook(Transform zone)
    {
        RoundRug(zone, "Reading table rug", own["Rug round"], new Vector3(-3f, 0f, 9f), 1.5f);
        // On top of the bookcase (1.65 m): a plant and books lying flat.
        Prop(zone, Models + "Plant.fbx", -7.15f, 9.48f, 20f, 1.3f, "Bookcase plant", below: 1.8f);
        Books(zone, "Bookcase books", -7.16f, 10.3f, 90f, 3, 1.8f);
    }

    static void WindowBar(Transform zone)
    {
        // Close to the wall (its face is x 7.40) and between the right tables' seat
        // rows: the strip along these windows is how the player gets from the counter
        // gate to the front of the room, and these spots keep each nearby customer's
        // delivery approach at about 80% or more of what it was (reach map).
        const float x = 6.62f;
        foreach (var (z, label) in new[] { (7.0f, "gallery"), (9.6f, "courtyard") })
        {
            Furniture(zone, Models + "Table_Bar.fbx", x, z, 15f, "High-top (" + label + ")");
            Furniture(zone, Models + "Stool_Bar.fbx", x, z - .62f, 6f, "Bar stool (" + label + ", south)");
            Furniture(zone, Models + "Stool_Bar.fbx", x, z + .62f, 172f, "Bar stool (" + label + ", north)");
        }
        Prop(zone, City + "SM_Prop_Soda_01.prefab", x - .08f, 7.08f, 0f, .35f, "Iced coffee", below: 1.2f);
        Prop(zone, City + "SM_Prop_SmartPhone_01.prefab", x + .08f, 6.91f, 25f, .55f, "Phone", below: 1.2f);
        Prop(zone, City + "SM_Prop_LargeSign_Coffee_01.prefab", x - .05f, 9.50f, 160f, .065f, "Latte cup", below: 1.2f);
        Prop(zone, City + "SM_Prop_Newspaper_01.prefab", x + .07f, 9.72f, -15f, .5f, "Newspaper", below: 1.2f);
        // Courtyard window sill (0.86 m).
        Prop(zone, Models + "Plant.fbx", 7.31f, 1.05f, 10f, 1.2f, "Sill plant", below: 1f);
        Prop(zone, Models + "Plant.fbx", 7.31f, 4.55f, 70f, 1.2f, "Sill plant", below: 1f);
        Prop(zone, Models + "Plant.fbx", 7.31f, 6.95f, 140f, 1.1f, "Sill plant", below: 1f);
        Books(zone, "Sill books", 7.29f, 10.45f, 0f, 2, 1f);
        Prop(zone, Models + "Mug.fbx", 7.30f, 10.78f, 300f, 1f, "Sill mug", below: 1f);
        // Between the two high-tops, above the stools.
        HangingPlant(zone, "Hanging plant (window bar)", 7.05f, 8.3f, -15f);
    }

    static void Entrance(Transform zone)
    {
        // Nothing stands on the floor by the door: the band inside the front windows
        // is the only way to the front seats when the room is full (a coat stand and
        // a plant there each cut a customer off). The coats go to the waiting nook by
        // the right-hand windows instead, between its two waiting spots - a pocket of
        // floor the reach map shows no path needs - turned so the tote faces the room.
        CoatStand(zone, "Coat stand (waiting nook)", 6.9f, 2.5f, 290f);
        // Plants hung inside the front windows either side of the door frame the way
        // in without touching the floor (vines stop above head height).
        HangingPlant(zone, "Hanging plant (door, left)", -2.6f, .35f, 15f);
        HangingPlant(zone, "Hanging plant (door, right)", 2.6f, .35f, -40f);
        // The A-frame out on the patio beside the right-hand planter, in line with the
        // bistro set on the other side of the door, turned to the zebra crossing that
        // car-park customers use. It stays clear of the walks to the door (the one from
        // the east street passes 0.3 m south of it) and of the open door leaf.
        Furniture(zone, Models + "MenuBoard.fbx", 3.6f, -2.0f, 200f, "Patio A-frame", scale: 4.2f);
    }

    static void CounterCubbies(Transform zone)
    {
        // Bays on the customer side of the front counter (shelf ~0.18-0.20 m,
        // opening faces the queue). Items sit well inside the 0.68 m deep bays.
        const float z = 13.74f, shelf = .5f;
        // Intake counter, left bay: retail bags of the house beans standing in a row,
        // labels to the queue.
        CoffeeBag(zone, "Coffee bag (house)", -1.50f, z, -4f, TopAt(new Vector2(-1.50f, z), shelf, null), "InteriorTerracotta");
        CoffeeBag(zone, "Coffee bag (decaf)", -1.31f, z, 3f, TopAt(new Vector2(-1.31f, z), shelf, null), "InteriorSage");
        CoffeeBag(zone, "Coffee bag (single origin)", -1.12f, z, -2f, TopAt(new Vector2(-1.12f, z), shelf, null), "InteriorOchre");
        // Middle bay: jars of beans.
        foreach (float x in new[] { -.45f, -.15f, .15f }) Jar(zone, "Bean jar", x, z, TopAt(new Vector2(x, z), shelf, null));
        // Right bay: stacked mugs (turned so their handles show) and a plant.
        foreach (var (x, yaw) in new[] { (.70f, 25f), (.88f, -30f) })
        {
            var low = Prop(zone, Models + "Mug.fbx", x, z, yaw, 1f, "Mug", below: shelf);
            Prop(zone, Models + "Mug.fbx", x, z, yaw + 12f, 1f, "Mug (stacked)", surface: TopOf(low) - .01f);
        }
        Prop(zone, Models + "Plant.fbx", 1.20f, z, 0f, 1f, "Cubby plant", below: shelf);
        // Right-hand counter: books and a crate; the far bay holds repair parts.
        Books(zone, "Cubby books", 2.18f, z, 0f, 4, shelf);
        Prop(zone, Gen + "SM_Gen_Prop_Crate_03.prefab", 2.52f, z, 12f, .26f, "Crate", below: shelf);
        Prop(zone, Gen + "SM_Gen_Prop_Crate_03.prefab", 4.05f, z, -8f, .30f, "Parts crate", below: shelf);
        Prop(zone, Gen + "SM_Gen_Prop_Rope_03.prefab", 4.38f, z, 30f, .18f, "Cable coil", below: shelf, material: M("InteriorInk"));
        // Left-hand counters: a bag and a jar; a plant and books.
        CoffeeBag(zone, "Coffee bag (house)", -3.45f, z, 6f, TopAt(new Vector2(-3.45f, z), shelf, null), "InteriorTerracotta");
        Jar(zone, "Bean jar", -3.18f, z, TopAt(new Vector2(-3.18f, z), shelf, null));
        Prop(zone, Models + "Plant.fbx", -6.20f, z, 0f, 1.1f, "Cubby plant", below: shelf);
        Books(zone, "Cubby books", -5.92f, z, 0f, 2, shelf);
    }

    static void WorkAreas(Transform zone)
    {
        // Rubber mats where the player stands at the drink station and the bench.
        Mat(zone, "Drink station mat", new Vector3(3.07f, 0f, 16.35f), new Vector2(1.5f, .75f));
        Mat(zone, "Repair bench mat", new Vector3(-3.3f, 0f, 15.55f), new Vector2(1.5f, .75f));
        // Parts cabinet in the strip behind the repair bench, a radio and a cable on it.
        var cabinet = PartsCabinet(zone, "Parts cabinet", -3.3f, 17.72f);
        float top = TopOf(cabinet);
        Radio(zone, "Radio", -3.58f, 17.73f, 8f, top);
        Prop(zone, Gen + "SM_Gen_Prop_Rope_03.prefab", -2.98f, 17.74f, 60f, .20f, "Cable coil", surface: top, material: M("InteriorInk"));
        Prop(zone, Gen + "SM_Gen_Prop_Cardboard_Box_04.prefab", -3.25f, 17.76f, 84f, .5f, "Parts box", surface: top);
        // Coffee sacks in the back corner past the drink counter.
        Furniture(zone, Gen + "SM_Gen_Prop_Sack_Stack_01.prefab", 5.55f, 17.4f, 12f, "Coffee sacks");
        Prop(zone, Gen + "SM_Gen_Prop_Sack_01.prefab", 6.28f, 16.95f, -20f, .9f, "Coffee sack", below: .05f);
    }

    static void SignsAndMenu(Transform zone)
    {
        // On the right-hand counter, past the line of sight from every queue slot to
        // the chalkboard menu (at 2.52 m it hid the menu from the first slot).
        Prop(zone, Models + "SignAcesCafe.fbx", 3.85f, 13.62f, 180f, 1.6f, "Ace's Cafe counter sign", below: 1.3f);
        // Back wall past the cat portrait, under the clock, clear of the corner plant.
        MenuBoard(zone, "Chalkboard menu", 5.97f, 1.62f, 17.99f, 1.0f, .68f);
    }

    // ------------------------------------------------------------------ pieces from the project

    sealed class Piece { public GameObject go; public Quaternion baseRotation; public Bounds bounds; }

    // Instantiates an asset under the zone, stood up if it is one of our Z-up Blender
    // exports, with its colliders, lights and scripts switched off; 'bounds' are its
    // renderer bounds at the origin with no turn (the pivot's frame).
    static Piece Spawn(Transform zone, string path, string name, float scale)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Missing asset " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, zone);
        go.name = name;
        go.transform.localScale = go.transform.localScale * scale;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Bounds b = RendererBounds(go.transform);
        Quaternion baseRotation = CafeFurnishingCatalog.IsZUp(b) ? CafeFurnishingCatalog.Upright : Quaternion.identity;
        go.transform.rotation = baseRotation;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var s in go.GetComponentsInChildren<MonoBehaviour>(true)) if (s != null) s.enabled = false;
        // Some pack meshes list more materials than they have submeshes.
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null && r.sharedMaterials.Length > f.sharedMesh.subMeshCount)
                r.sharedMaterials = r.sharedMaterials.Take(f.sharedMesh.subMeshCount).ToArray();
        }
        return new Piece { go = go, baseRotation = baseRotation, bounds = RendererBounds(go.transform) };
    }

    // Puts the piece's footprint centre at (x, z), its bottom on 'surface', turned 'yaw'.
    static void Place(Piece p, float x, float z, float yaw, float surface)
    {
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 c = turn * new Vector3(p.bounds.center.x, 0f, p.bounds.center.z);
        p.go.transform.rotation = turn * p.baseRotation;
        p.go.transform.position = new Vector3(x - c.x, surface - p.bounds.min.y, z - c.z);
        RecordAll(p.go);
        placed.Add(p.go.name);
    }

    // Floor furniture: on the floor (or the rug under it), with a box collider.
    static GameObject Furniture(Transform zone, string path, float x, float z, float yaw, string name, string fabric = null, float scale = 1f)
    {
        var p = Spawn(zone, path, name, scale);
        Remap(p.go, fabric);
        // Box collider in the piece's own space, from its bounds at the origin.
        var box = p.go.AddComponent<BoxCollider>();
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var corner in Corners(p.bounds))
        {
            Vector3 l = p.go.transform.InverseTransformPoint(corner);
            min = Vector3.Min(min, l); max = Vector3.Max(max, l);
        }
        box.center = (min + max) * .5f;
        box.size = max - min;
        float floor = TopAt(new Vector2(x, z), .05f, p.go.transform);
        Place(p, x, z, yaw, float.IsNegativeInfinity(floor) ? 0f : floor);
        floorPieces.Add((p.go.transform, name));
        return p.go;
    }

    // A small prop on whatever surface is under (x, z) - measured from the real
    // triangles, below 'below' metres - or on the given surface height.
    static GameObject Prop(Transform zone, string path, float x, float z, float yaw, float scale, string name,
                           float below = 2f, float? surface = null, Material material = null)
    {
        var p = Spawn(zone, path, name, scale);
        if (material != null)
            foreach (var r in p.go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        else Remap(p.go, null);
        float y = surface ?? TopAt(new Vector2(x, z), below, p.go.transform);
        if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Nothing to stand " + name + " on at (" + x + ", " + z + ").");
        Place(p, x, z, yaw, y);
        return p.go;
    }

    // Our library's flat woods become the room's grain woods (the round tables
    // use the same ones); a copy of the mesh gets box-projected UVs in metres so
    // the grain runs at the same scale as on the tables and counters.
    static void Remap(GameObject go, string fabric)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false, wood = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name;
                if (n.StartsWith("CC_Wood_") && palette.TryGetValue("Cafe grain - " + n, out var grain)) { mats[i] = grain; changed = wood = true; }
                else if (n == "DC_Fabric" && fabric != null) { mats[i] = M(fabric); changed = true; }
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            var f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null && (wood || fabric != null)) f.sharedMesh = GrainCopy(f, go.transform);
        }
    }

    static Mesh GrainCopy(MeshFilter f, Transform pieceRoot)
    {
        if (grainCopies.TryGetValue(f.sharedMesh, out var done)) return done;
        var source = f.sharedMesh;
        var mesh = Object.Instantiate(source);
        mesh.name = source.name + " - grain UV";
        var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
        // The piece sits at the origin, stood up, with no turn: world space here is
        // the piece's upright frame in metres.
        Matrix4x4 w = f.transform.localToWorldMatrix;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = w.MultiplyPoint3x4(v[i]) - pieceRoot.position;
            Vector3 d = n.Length > i ? w.MultiplyVector(n[i]).normalized : Vector3.up;
            uv[i] = Mathf.Abs(d.y) > .6f ? new Vector2(p.x, p.z) : Mathf.Abs(d.x) > .6f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        mesh = SaveMesh(mesh);
        grainCopies[source] = mesh;
        return mesh;
    }

    // ------------------------------------------------------------------ pieces built here, in the café palette

    static GameObject FloorLamp(Transform zone, string name, float x, float z)
    {
        float floor = TopAt(new Vector2(x, z), .05f, null);
        var lamp = Node(zone, name, new Vector3(x, float.IsNegativeInfinity(floor) ? 0f : floor, z), 0f);
        Cyl(lamp, "Weighted base", new Vector3(0f, .018f, 0f), .34f, .036f, M("InteriorInk"));
        Cyl(lamp, "Base collar", new Vector3(0f, .05f, 0f), .09f, .03f, M("InteriorBrass"));
        Cyl(lamp, "Pole", new Vector3(0f, .76f, 0f), .026f, 1.40f, M("InteriorBrass"));
        Ball(lamp, "Switch knuckle", new Vector3(0f, 1.44f, 0f), .05f, M("InteriorBrass"));
        Cyl(lamp, "Drum shade", new Vector3(0f, 1.53f, 0f), .42f, .30f, own["Shade"]);
        Cyl(lamp, "Shade trim (top)", new Vector3(0f, 1.681f, 0f), .426f, .012f, M("InteriorBrass"));
        Cyl(lamp, "Shade trim (bottom)", new Vector3(0f, 1.379f, 0f), .426f, .012f, M("InteriorBrass"));
        Cyl(lamp, "Warm glow", new Vector3(0f, 1.371f, 0f), .39f, .004f, M("Warm lamp"));
        FloorCollider(lamp, name, new Vector3(0f, .9f, 0f), new Vector3(.34f, 1.8f, .34f));
        return lamp.gameObject;
    }

    static void CoatStand(Transform zone, string name, float x, float z, float yaw)
    {
        var wood = M("Cafe grain - CC_Wood_Espresso"); var brass = M("InteriorBrass");
        var stand = Node(zone, name, new Vector3(x, 0f, z), yaw);
        Box(stand, "Foot (x)", new Vector3(0f, .025f, 0f), new Vector3(.52f, .05f, .06f), wood);
        Box(stand, "Foot (z)", new Vector3(0f, .025f, 0f), new Vector3(.06f, .05f, .52f), wood);
        foreach (var d in new[] { new Vector3(.24f, 0f, 0f), new Vector3(-.24f, 0f, 0f), new Vector3(0f, 0f, .24f), new Vector3(0f, 0f, -.24f) })
            Box(stand, "Foot cap", d + new Vector3(0f, .006f, 0f), new Vector3(.065f, .012f, .065f), brass);
        Cyl(stand, "Post", new Vector3(0f, .92f, 0f), .05f, 1.74f, wood);
        Ball(stand, "Finial", new Vector3(0f, 1.8f, 0f), .075f, wood);
        var tips = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            var peg = Node(stand, "Peg " + (i + 1), new Vector3(0f, 1.6f, 0f), 45f + i * 90f, local: true);
            Cyl(peg, "Arm", new Vector3(0f, .049f, .07f), .022f, .17f, wood, new Vector3(55f, 0f, 0f));
            Ball(peg, "Knob", new Vector3(0f, .098f, .139f), .034f, brass);
            tips[i] = stand.InverseTransformPoint(peg.TransformPoint(new Vector3(0f, .098f, .139f)));
        }
        // A hat on top, a scarf over one peg, an Ace's tote on another. Each hangs
        // with its broad face along its peg, so from outside the stand it faces you
        // (the tote's patch on its outward face); peg 1 points 45 degrees, peg 4 315.
        var hat = Node(stand, "Hat", new Vector3(0f, 1.8f, 0f), 30f, local: true);
        hat.localRotation = Quaternion.Euler(8f, 30f, 4f);
        Cyl(hat, "Brim", new Vector3(0f, .0f, 0f), .30f, .012f, M("InteriorInk"));
        Cyl(hat, "Crown", new Vector3(0f, .05f, 0f), .17f, .09f, M("InteriorInk"));
        Cyl(hat, "Band", new Vector3(0f, .018f, 0f), .174f, .022f, M("InteriorTerracotta"));
        var scarf = Node(stand, "Scarf", tips[0] + new Vector3(0f, -.25f, 0f), 45f, local: true);
        Box(scarf, "Scarf", Vector3.zero, new Vector3(.085f, .5f, .02f), M("InteriorTerracotta"));
        Box(scarf, "Scarf stripe", new Vector3(0f, -.17f, 0f), new Vector3(.087f, .04f, .022f), M("InteriorCream"));
        Box(scarf, "Scarf stripe", new Vector3(0f, -.2f, 0f), new Vector3(.087f, .015f, .022f), M("InteriorCream"));
        // The patch is on the bag's -z face: turned 135 degrees, that face points along
        // peg 4 (315 degrees), away from the post.
        var tote = Node(stand, "Tote bag", tips[3] + new Vector3(0f, -.28f, 0f), 135f, local: true);
        Box(tote, "Bag", Vector3.zero, new Vector3(.30f, .34f, .07f), M("Paper"));
        Box(tote, "Ace's patch", new Vector3(0f, .02f, -.036f), new Vector3(.12f, .08f, .004f), M("InteriorSage"));
        foreach (float sx in new[] { -.08f, .08f })
            Box(tote, "Strap", new Vector3(sx * .5f, .21f, 0f), new Vector3(.015f, .14f, .01f), M("Paper"), new Vector3(0f, 0f, sx > 0 ? 20f : -20f));
        FloorCollider(stand, name, new Vector3(0f, .93f, 0f), new Vector3(.52f, 1.86f, .52f));
    }

    static Transform Tray(Transform zone, string name, float x, float z, float yaw)
    {
        float y = TopAt(new Vector2(x, z), .6f, null);
        var tray = Node(zone, name, new Vector3(x, y, z), yaw);
        var oak = M("Cafe grain - CC_Wood_Counter");
        Box(tray, "Base", new Vector3(0f, .006f, 0f), new Vector3(.38f, .012f, .26f), oak);
        Box(tray, "Rim", new Vector3(0f, .02f, .124f), new Vector3(.38f, .03f, .012f), oak);
        Box(tray, "Rim", new Vector3(0f, .02f, -.124f), new Vector3(.38f, .03f, .012f), oak);
        Box(tray, "Rim", new Vector3(.184f, .02f, 0f), new Vector3(.012f, .03f, .26f), oak);
        Box(tray, "Rim", new Vector3(-.184f, .02f, 0f), new Vector3(.012f, .03f, .26f), oak);
        return tray;
    }

    // A wool throw folded in two on a seat: terracotta with two cream stripes
    // running over both folds.
    static void Throw(Transform zone, string name, float x, float z, float yaw)
    {
        float y = TopAt(new Vector2(x, z), .6f, null);
        if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Nothing to put " + name + " on at (" + x + ", " + z + ").");
        var t = Node(zone, name, new Vector3(x, y, z), yaw);
        var wool = M("InteriorTerracotta");
        Box(t, "Lower fold", new Vector3(0f, .017f, 0f), new Vector3(.40f, .034f, .30f), wool);
        Box(t, "Upper fold", new Vector3(.01f, .048f, -.008f), new Vector3(.38f, .028f, .282f), wool);
        foreach (float sx in new[] { -.115f, .135f })
        {
            Box(t, "Stripe (lower)", new Vector3(sx, .017f, 0f), new Vector3(.026f, .036f, .304f), M("InteriorCream"));
            Box(t, "Stripe (upper)", new Vector3(sx, .048f, -.008f), new Vector3(.026f, .030f, .286f), M("InteriorCream"));
        }
    }

    // A retail bag of beans: kraft paper, rolled top with a tin tie, a coloured label
    // with a cream band on the front (-z, towards the queue at no turn).
    static void CoffeeBag(Transform zone, string name, float x, float z, float yaw, float y, string label)
    {
        if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Nothing to put " + name + " on at (" + x + ", " + z + ").");
        var bag = Node(zone, name, new Vector3(x, y, z), yaw);
        var kraft = own["Kraft"];
        Box(bag, "Bag", new Vector3(0f, .075f, 0f), new Vector3(.11f, .15f, .07f), kraft);
        Box(bag, "Rolled top", new Vector3(0f, .163f, 0f), new Vector3(.112f, .026f, .03f), kraft);
        Box(bag, "Tin tie", new Vector3(0f, .163f, -.016f), new Vector3(.118f, .008f, .004f), M("InteriorBrass"));
        Box(bag, "Label", new Vector3(0f, .07f, -.036f), new Vector3(.08f, .07f, .003f), M(label));
        Box(bag, "Label band", new Vector3(0f, .07f, -.0378f), new Vector3(.08f, .014f, .002f), M("Paper"));
    }

    // A planter hung from the ceiling (3.2 m) on cream macramé cords: brass hook and
    // ring, a terracotta pot, a mound of leaves and vines trailing over the rim. The
    // vines stop at about 2 m, above head height, and nothing has a collider, so it
    // never touches a path. Vine lengths and turns vary a little, deterministically.
    static void HangingPlant(Transform zone, string name, float x, float z, float yaw)
    {
        const float ceiling = 3.2f, potBottom = 2.32f, potH = .17f, potD = .24f;
        var hang = Node(zone, name, new Vector3(x, 0f, z), yaw);
        var cord = M("InteriorCream");
        var leaf = own["Foliage"];
        var leafDark = own["Foliage dark"];
        float top = potBottom + potH;
        var ring = new Vector3(0f, ceiling - .075f, 0f);
        Cyl(hang, "Ceiling hook", new Vector3(0f, ceiling - .01f, 0f), .06f, .02f, M("InteriorBrass"));
        Cyl(hang, "Hook stem", new Vector3(0f, ceiling - .045f, 0f), .012f, .05f, M("InteriorBrass"));
        Ball(hang, "Ring", ring, .04f, M("InteriorBrass"));
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 30f) * Mathf.Deg2Rad;
            var rim = new Vector3(Mathf.Sin(a) * (potD * .5f + .006f), top - .03f, Mathf.Cos(a) * (potD * .5f + .006f));
            Vector3 along = ring - rim;
            var c = Cyl(hang, "Cord", (ring + rim) * .5f, .012f, along.magnitude, cord);
            c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
            Ball(hang, "Knot", Vector3.Lerp(ring, rim, .6f), .026f, cord);
            // The cords run on under the pot to a tassel.
            var under = new Vector3(0f, potBottom - .05f, 0f);
            Vector3 down = under - rim;
            var u = Cyl(hang, "Cord (under)", (rim + under) * .5f, .011f, down.magnitude, cord);
            u.transform.localRotation = Quaternion.FromToRotation(Vector3.up, down.normalized);
        }
        Cyl(hang, "Tassel", new Vector3(0f, potBottom - .11f, 0f), .03f, .12f, cord);
        Cyl(hang, "Pot", new Vector3(0f, potBottom + potH * .5f, 0f), potD, potH, M("InteriorTerracotta"));
        Cyl(hang, "Pot rim", new Vector3(0f, top - .012f, 0f), potD + .022f, .024f, M("InteriorTerracotta"));
        Cyl(hang, "Soil", new Vector3(0f, top - .016f, 0f), potD - .03f, .01f, M("Cafe grain - CC_Wood_Espresso"));
        Primitive(hang, "Leaves", PrimitiveType.Sphere, new Vector3(0f, top + .02f, 0f), new Vector3(.30f, .13f, .28f), leaf, new Vector3(0f, 20f, 0f));
        Primitive(hang, "Leaves", PrimitiveType.Sphere, new Vector3(.06f, top + .06f, -.03f), new Vector3(.19f, .12f, .17f), leafDark, new Vector3(0f, 50f, 8f));
        Primitive(hang, "Leaves", PrimitiveType.Sphere, new Vector3(-.07f, top + .05f, .05f), new Vector3(.16f, .10f, .15f), leaf, new Vector3(0f, -30f, -6f));
        int seed = Mathf.Abs(Mathf.RoundToInt(x * 131f + z * 71f));
        for (int v = 0; v < 7; v++)
        {
            float a = (v * 51f + 20f + seed % 30) * Mathf.Deg2Rad;
            float length = .22f + ((seed + v * 37) % 5) * .055f; // .22 - .44 m: ends at ~2.0 m or higher
            var start = new Vector3(Mathf.Sin(a) * potD * .52f, top - .005f, Mathf.Cos(a) * potD * .52f);
            Vector3 dir = new Vector3(Mathf.Sin(a) * .16f, -1f, Mathf.Cos(a) * .16f).normalized;
            Vector3 end = start + dir * length;
            var stem = Cyl(hang, "Vine", (start + end) * .5f, .012f, length, leafDark);
            stem.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            int leaves = 3 + v % 3;
            for (int k = 1; k <= leaves; k++)
            {
                Vector3 p = Vector3.Lerp(start, end, k / (float)leaves) + new Vector3(k % 2 == 0 ? .016f : -.016f, 0f, k % 2 == 0 ? -.01f : .01f);
                Primitive(hang, "Leaf", PrimitiveType.Sphere, p, new Vector3(.055f, .026f, .042f), k % 2 == 0 ? leaf : leafDark, new Vector3(0f, v * 40f + k * 25f, 28f));
            }
        }
    }

    // A small prop on a window ledge, only if the ledge is really there (a surface
    // between 0.6 and 1.0 m under the point) - never dropped on the floor behind a sofa.
    static void SillProp(Transform zone, string path, float x, float z, float yaw, float scale, string name)
    {
        float y = TopAt(new Vector2(x, z), 1f, null);
        if (y < .6f) { Debug.LogWarning(Tag + "No ledge under " + name + " at (" + x + ", " + z + "); left it out."); return; }
        Prop(zone, path, x, z, yaw, scale, name, surface: y);
    }

    // Books lying flat: a cover in one of the reading shelf's colours, pages
    // showing on the fore-edge. Sizes and turns vary a little, deterministically.
    static Transform Books(Transform zone, string name, float x, float z, float yaw, int count, float below)
    {
        string[] covers = { "Reading shelf - BookTerracotta", "Reading shelf - BookBlue", "Reading shelf - BookOchre", "Reading shelf - BookInk", "Reading shelf - BookPaper" };
        float y = TopAt(new Vector2(x, z), below, null);
        if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Nothing to put " + name + " on at (" + x + ", " + z + ").");
        var stack = Node(zone, name, new Vector3(x, y, z), yaw);
        int seed = Mathf.Abs(Mathf.RoundToInt(x * 1000f + z * 7919f));
        float h = 0f;
        for (int i = 0; i < count; i++)
        {
            int s = seed / (i + 1) + i * 37;
            float w = .15f + (s % 5) * .012f, d = .21f + (s / 5 % 4) * .012f, t = .026f + (s / 20 % 3) * .008f;
            var book = Node(stack, "Book " + (i + 1), new Vector3(0f, h, 0f), ((s / 60 % 7) - 3) * 3f, local: true);
            string cover = covers[(s + i) % covers.Length];
            Box(book, "Cover", new Vector3(0f, t * .5f, 0f), new Vector3(w, t, d), M(palette.ContainsKey(cover) ? cover : "InteriorTerracotta"));
            Box(book, "Pages", new Vector3(.004f, t * .5f, 0f), new Vector3(w - .002f, t - .008f, d - .01f), M("Paper"));
            h += t;
        }
        return stack;
    }

    static void Jar(Transform zone, string name, float x, float z, float y)
    {
        if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Nothing to put " + name + " on at (" + x + ", " + z + ").");
        var jar = Node(zone, name, new Vector3(x, y, z), 0f);
        Cyl(jar, "Beans", new Vector3(0f, .056f, 0f), .088f, .11f, M("Cafe grain - CC_Wood_Espresso"));
        Cyl(jar, "Glass", new Vector3(0f, .07f, 0f), .1f, .14f, own["Jar glass"]);
        Cyl(jar, "Lid", new Vector3(0f, .152f, 0f), .106f, .024f, M("InteriorOak"));
        Ball(jar, "Knob", new Vector3(0f, .168f, 0f), .03f, M("InteriorOak"));
    }

    static Transform PartsCabinet(Transform zone, string name, float x, float z)
    {
        float floor = TopAt(new Vector2(x, z), .05f, null);
        var cab = Node(zone, name, new Vector3(x, float.IsNegativeInfinity(floor) ? 0f : floor, z), 0f);
        const float W = 1.10f, D = .45f, H = .86f;
        Box(cab, "Plinth", new Vector3(0f, .03f, .01f), new Vector3(W - .04f, .06f, D - .04f), M("InteriorInk"));
        Box(cab, "Carcass", new Vector3(0f, .06f + (H - .06f) * .5f, 0f), new Vector3(W, H - .06f, D), M("InteriorSage"));
        Box(cab, "Oak top", new Vector3(0f, H + .02f, -.01f), new Vector3(W + .04f, .04f, D + .04f), M("Cafe grain - CC_Wood_Counter"));
        const int cols = 3, rows = 3;
        float dw = (W - .08f) / cols, dh = (H - .06f - .08f) / rows;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float cx = -W * .5f + .04f + dw * (c + .5f), cy = .06f + .04f + dh * (r + .5f);
                Box(cab, $"Drawer {r + 1}-{c + 1}", new Vector3(cx, cy, -D * .5f - .006f), new Vector3(dw - .022f, dh - .022f, .012f), M("InteriorMoss"));
                Box(cab, $"Pull {r + 1}-{c + 1}", new Vector3(cx, cy + dh * .2f, -D * .5f - .02f), new Vector3(.09f, .014f, .018f), M("InteriorBrass"));
                Box(cab, $"Label {r + 1}-{c + 1}", new Vector3(cx, cy - dh * .12f, -D * .5f - .0135f), new Vector3(.07f, .035f, .003f), M("Paper"));
            }
        FloorCollider(cab, name, new Vector3(0f, (H + .04f) * .5f, 0f), new Vector3(W + .04f, H + .04f, D + .04f));
        return cab;
    }

    static void Radio(Transform zone, string name, float x, float z, float yaw, float y)
    {
        var radio = Node(zone, name, new Vector3(x, y, z), yaw);
        var walnut = M("Cafe grain - CC_Wood_Espresso");
        Box(radio, "Body", new Vector3(0f, .11f, 0f), new Vector3(.34f, .19f, .13f), M("InteriorCream"));
        Box(radio, "Plinth", new Vector3(0f, .008f, 0f), new Vector3(.35f, .016f, .136f), walnut);
        Cyl(radio, "Speaker", new Vector3(-.07f, .11f, -.066f), .13f, .006f, M("InteriorInk"), new Vector3(90f, 0f, 0f));
        for (int i = -1; i <= 1; i++)
            Box(radio, "Grille bar", new Vector3(-.07f, .11f + i * .03f, -.07f), new Vector3(.12f, .006f, .004f), M("InteriorBrass"));
        Box(radio, "Dial window", new Vector3(.085f, .14f, -.066f), new Vector3(.12f, .035f, .004f), M("Paper"));
        Box(radio, "Dial needle", new Vector3(.07f, .14f, -.069f), new Vector3(.004f, .03f, .002f), M("InteriorTerracotta"));
        Cyl(radio, "Knob", new Vector3(.05f, .07f, -.07f), .035f, .018f, M("InteriorInk"), new Vector3(90f, 0f, 0f));
        Cyl(radio, "Knob", new Vector3(.12f, .07f, -.07f), .035f, .018f, M("InteriorInk"), new Vector3(90f, 0f, 0f));
        Box(radio, "Handle", new Vector3(0f, .225f, 0f), new Vector3(.2f, .014f, .03f), walnut);
        Box(radio, "Handle post", new Vector3(-.093f, .212f, 0f), new Vector3(.014f, .03f, .03f), walnut);
        Box(radio, "Handle post", new Vector3(.093f, .212f, 0f), new Vector3(.014f, .03f, .03f), walnut);
        Cyl(radio, "Aerial", new Vector3(.15f, .33f, .04f), .008f, .26f, M("InteriorBrass"), new Vector3(0f, 0f, -18f));
    }

    static void MenuBoard(Transform zone, string name, float x, float y, float wallZ, float width, float height)
    {
        var board = Node(zone, name, new Vector3(x, y, wallZ), 0f);
        var oak = M("Cafe grain - CC_Wood_Counter");
        const float frame = .05f, depth = .035f;
        Box(board, "Backing", new Vector3(0f, 0f, -.009f), new Vector3(width, height, .018f), M("InteriorInk"));
        var face = new GameObject("Chalkboard face");
        face.transform.SetParent(board, false);
        face.transform.localPosition = new Vector3(0f, 0f, -.019f);
        face.AddComponent<MeshFilter>().sharedMesh = SaveMesh(QuadMesh("Chalkboard face", width - frame * 1.2f, height - frame * 1.2f));
        face.AddComponent<MeshRenderer>().sharedMaterial = own["Menu"];
        Box(board, "Frame top", new Vector3(0f, height * .5f - frame * .5f, -depth * .5f), new Vector3(width, frame, depth), oak);
        Box(board, "Frame bottom", new Vector3(0f, -height * .5f + frame * .5f, -depth * .5f), new Vector3(width, frame, depth), oak);
        Box(board, "Frame left", new Vector3(-width * .5f + frame * .5f, 0f, -depth * .5f), new Vector3(frame, height - frame * 2f, depth), oak);
        Box(board, "Frame right", new Vector3(width * .5f - frame * .5f, 0f, -depth * .5f), new Vector3(frame, height - frame * 2f, depth), oak);
        Box(board, "Chalk ledge", new Vector3(0f, -height * .5f - .012f, -.045f), new Vector3(width * .9f, .024f, .07f), oak);
        Cyl(board, "Chalk", new Vector3(-.2f, -height * .5f + .006f, -.05f), .012f, .07f, M("Paper"), new Vector3(0f, 0f, 90f));
        Cyl(board, "Chalk", new Vector3(-.1f, -height * .5f + .006f, -.055f), .012f, .06f, M("Butter yellow"), new Vector3(0f, 25f, 90f));
    }

    // ------------------------------------------------------------------ rugs and mats

    static void Rug(Transform zone, string name, Material material, Vector3 centre, Vector2 size, float yaw, bool fringe)
    {
        float floor = TopAt(new Vector2(centre.x, centre.z), .05f, null);
        var rug = Node(zone, name, new Vector3(centre.x, float.IsNegativeInfinity(floor) ? 0f : floor, centre.z), yaw);
        MeshPart(rug, "Rug", SaveMesh(SlabMesh(name, size.x, size.y, .008f, new Vector2(.004f, .5f))), material);
        if (!fringe) return;
        foreach (float end in new[] { -1f, 1f })
        {
            var strip = new GameObject("Fringe");
            strip.transform.SetParent(rug, false);
            strip.transform.localPosition = new Vector3(0f, .004f, end * (size.y * .5f + .035f));
            strip.transform.localRotation = Quaternion.Euler(0f, end > 0 ? 0f : 180f, 0f);
            MeshPart(strip.transform, "Fringe threads", SaveMesh(FlatMesh("Rug fringe " + size.x.ToString("0.0", CultureInfo.InvariantCulture), size.x - .12f, .07f)), own["Rug fringe"]);
        }
    }

    static void RoundRug(Transform zone, string name, Material material, Vector3 centre, float radius)
    {
        float floor = TopAt(new Vector2(centre.x, centre.z), .05f, null);
        var rug = Node(zone, name, new Vector3(centre.x, float.IsNegativeInfinity(floor) ? 0f : floor, centre.z), 0f);
        MeshPart(rug, "Rug", SaveMesh(DiscMesh(name, radius, .008f, 64)), material);
    }

    static void Mat(Transform zone, string name, Vector3 centre, Vector2 size)
    {
        float floor = TopAt(new Vector2(centre.x, centre.z), .05f, null);
        var mat = Node(zone, name, new Vector3(centre.x, float.IsNegativeInfinity(floor) ? 0f : floor, centre.z), 0f);
        MeshPart(mat, "Mat", SaveMesh(SlabMesh("Rubber mat " + size.x.ToString("0.00", CultureInfo.InvariantCulture), size.x, size.y, .012f, new Vector2(.01f, .5f))), own["Mat"]);
    }

    static void MeshPart(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // A thin slab: the top carries the whole texture (u across x, v along z); the
    // sides take one texel from the outer border. No bottom face (it lies on the floor).
    static Mesh SlabMesh(string name, float width, float length, float thickness, Vector2 edgeUV)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float hx = width * .5f, hz = length * .5f, h = thickness;
        Face(v, uv, t, new[] { new Vector3(-hx, h, -hz), new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz) },
             new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) }, Vector3.up);
        var e = new[] { edgeUV, edgeUV, edgeUV, edgeUV };
        Face(v, uv, t, new[] { new Vector3(-hx, 0, -hz), new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz), new Vector3(hx, 0, -hz) }, e, Vector3.back);
        Face(v, uv, t, new[] { new Vector3(hx, 0, hz), new Vector3(hx, h, hz), new Vector3(-hx, h, hz), new Vector3(-hx, 0, hz) }, e, Vector3.forward);
        Face(v, uv, t, new[] { new Vector3(-hx, 0, hz), new Vector3(-hx, h, hz), new Vector3(-hx, h, -hz), new Vector3(-hx, 0, -hz) }, e, Vector3.left);
        Face(v, uv, t, new[] { new Vector3(hx, 0, -hz), new Vector3(hx, h, -hz), new Vector3(hx, h, hz), new Vector3(hx, 0, hz) }, e, Vector3.right);
        return Build(name, v, uv, t);
    }

    static Mesh DiscMesh(string name, float radius, float thickness, int segments)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector2 edge = new Vector2(.5f + .497f, .5f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = 2f * Mathf.PI * i / segments, a1 = 2f * Mathf.PI * (i + 1) / segments;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, thickness, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, thickness, Mathf.Sin(a1) * radius);
            Vector3 c = new Vector3(0f, thickness, 0f);
            Tri(v, uv, t, c, p0, p1, new Vector2(.5f, .5f), Planar(p0, radius), Planar(p1, radius), Vector3.up);
            Vector3 q0 = new Vector3(p0.x, 0f, p0.z), q1 = new Vector3(p1.x, 0f, p1.z);
            Vector3 outward = new Vector3(Mathf.Cos((a0 + a1) * .5f), 0f, Mathf.Sin((a0 + a1) * .5f));
            Face(v, uv, t, new[] { q0, p0, p1, q1 }, new[] { edge, edge, edge, edge }, outward);
        }
        return Build(name, v, uv, t);
    }

    static Vector2 Planar(Vector3 p, float radius) => new Vector2(.5f + p.x / (2f * radius), .5f + p.z / (2f * radius));

    // A single upright quad facing -z (towards the room when it hangs on the back wall).
    static Mesh QuadMesh(string name, float width, float height)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float hx = width * .5f, hy = height * .5f;
        Face(v, uv, t, new[] { new Vector3(-hx, -hy, 0), new Vector3(-hx, hy, 0), new Vector3(hx, hy, 0), new Vector3(hx, -hy, 0) },
             new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) }, Vector3.back);
        return Build(name, v, uv, t);
    }

    // A flat strip lying on the floor (fringe), threads running along z from its
    // near edge; both sides render through the cut-out material.
    static Mesh FlatMesh(string name, float width, float length)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float hx = width * .5f, hz = length * .5f;
        Face(v, uv, t, new[] { new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz) },
             new[] { new Vector2(0, 1), new Vector2(0, 0), new Vector2(width / .25f, 0), new Vector2(width / .25f, 1) }, Vector3.up);
        return Build(name, v, uv, t);
    }

    // Adds a quad (a, b, c, d in order round its edge) facing 'normal'.
    static void Face(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3[] q, Vector2[] quv, Vector3 normal)
    {
        Tri(v, uv, t, q[0], q[1], q[2], quv[0], quv[1], quv[2], normal);
        Tri(v, uv, t, q[0], q[2], q[3], quv[0], quv[2], quv[3], normal);
    }

    // Adds a triangle wound so that it faces 'normal' (Unity renders clockwise).
    static void Tri(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 normal)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { (b, c) = (c, b); (ub, uc) = (uc, ub); }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); uv.Add(ua); uv.Add(ub); uv.Add(uc);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Mesh Build(string name, List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    // Meshes made here live in one asset file so the scene stays small; a re-run
    // updates them in place.
    static Mesh SaveMesh(Mesh mesh)
    {
        EnsureFolder(Art);
        var existing = AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath).OfType<Mesh>().FirstOrDefault(m => m.name == mesh.name);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) == null) AssetDatabase.CreateAsset(mesh, MeshAssetPath);
        else AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
        return mesh;
    }

    // ------------------------------------------------------------------ materials

    static Material M(string name) => palette.TryGetValue(name, out var m) ? m
        : throw new InvalidOperationException("Café palette material missing: " + name);

    static void LoadMaterials()
    {
        palette = AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Material>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
        foreach (string need in new[] { "Cafe grain - CC_Wood_Counter", "Cafe grain - CC_Wood_Espresso", "InteriorOak", "InteriorSage", "InteriorCream",
                                        "InteriorBrass", "InteriorInk", "InteriorMoss", "InteriorTerracotta", "InteriorOchre", "InteriorGlass", "Paper", "Warm lamp", "Butter yellow" })
            M(need);
        own.Clear();
        own["Rug kilim"] = TextureMaterial("Rug - lounge kilim", "Rug_LoungeKilim.png", .06f, false, true);
        own["Rug round"] = TextureMaterial("Rug - reading round", "Rug_ReadingRound.png", .06f, false, true);
        own["Rug fringe"] = TextureMaterial("Rug - fringe", "Rug_Fringe.png", .04f, true, false);
        own["Mat"] = TextureMaterial("Mat - rubber", "Mat_Rubber.png", .22f, false, true);
        own["Menu"] = TextureMaterial("Menu - chalkboard", "Menu_Chalkboard.png", .1f, false, false);
        own["Shade"] = DerivedMaterial("Lamp shade glow", "InteriorCream", m =>
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, .72f, .40f) * .55f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
        own["Jar glass"] = DerivedMaterial("Jar glass", "InteriorGlass", m =>
        {
            Color c = m.GetColor("_BaseColor"); c.a = .22f;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", .82f);
        });
        own["Kraft"] = DerivedMaterial("Kraft paper", "Paper", m => { m.SetColor("_BaseColor", new Color(.66f, .50f, .33f)); m.SetFloat("_Smoothness", .08f); });
        own["Foliage"] = DerivedMaterial("Foliage", "InteriorMoss", m => { m.SetColor("_BaseColor", new Color(.36f, .53f, .27f)); m.SetFloat("_Smoothness", .12f); });
        own["Foliage dark"] = DerivedMaterial("Foliage dark", "InteriorMoss", m => { m.SetColor("_BaseColor", new Color(.24f, .39f, .20f)); m.SetFloat("_Smoothness", .12f); });
    }

    static Material TextureMaterial(string name, string file, float smoothness, bool cutout, bool allowResize)
    {
        string texPath = TextureFolder + "/" + file;
        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter
            ?? throw new InvalidOperationException("Missing texture " + texPath);
        bool changed = false;
        var wrap = cutout ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importer.wrapMode != wrap) { importer.wrapMode = wrap; changed = true; }
        if (importer.anisoLevel < 4) { importer.anisoLevel = 4; changed = true; }
        if (!allowResize && importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
        if (cutout && !importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (changed) importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            EnsureFolder(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        if (cutout)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", .5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.SetFloat("_Cull", 0f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material DerivedMaterial(string name, string from, Action<Material> setup)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            EnsureFolder(MaterialFolder);
            material = new Material(M(from)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        setup(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ------------------------------------------------------------------ checks

    static void BuildKeepClear()
    {
        keepClear.Clear();
        foreach (var spot in CityPackChecks.InScene<WaitingSpot>())
        {
            if (!spot.gameObject.activeInHierarchy) continue;
            Vector3 p = spot.StandPoint != null ? spot.StandPoint.position : spot.transform.position;
            keepClear.Add((p, spot is TableSeat ? .75f : .80f, spot.name + (spot is TableSeat ? " (seat stand point)" : " (waiting spot)")));
        }
        foreach (var queue in CityPackChecks.InScene<CounterQueue>())
            foreach (Transform slot in queue.transform) keepClear.Add((slot.position, .90f, "queue " + slot.name));
        foreach (var s in CityPackChecks.InScene<StationInteractable>())
            if (s.StandPoint != null) keepClear.Add((s.StandPoint.position, .80f, s.name + " stand point"));
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) keepClear.Add((spawn.transform.position, 1f, "customer spawn"));
    }

    static void CheckFloorClearance()
    {
        var problems = new List<string>();
        Physics.SyncTransforms();
        foreach (var (t, what) in floorPieces)
        {
            var collider = t.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => c.enabled);
            // The box is measured from its own transform, not collider.bounds: a
            // collider added before its piece was moved keeps reporting the old
            // (origin) bounds until physics next syncs.
            Bounds b = collider != null ? WorldBox(collider) : RendererBounds(t);
            foreach (var k in keepClear)
            {
                float dx = Mathf.Max(Mathf.Abs(k.p.x - b.center.x) - b.extents.x, 0f);
                float dz = Mathf.Max(Mathf.Abs(k.p.z - b.center.z) - b.extents.z, 0f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < k.r) problems.Add($"{what} is {d:0.00} m from {k.what} (needs {k.r:0.00} m)");
            }
            if (b.max.x > -1f && b.min.x < 1f && b.max.z > -1.2f && b.min.z < 12.5f) problems.Add(what + " stands in the door-to-counter lane");
        }
        if (problems.Count > 0)
            throw new InvalidOperationException("Furniture would get in the way of gameplay, so nothing was kept:\n" + string.Join("\n", problems));
    }

    // The occupied-circulation check, cell for cell (the same grid, customer envelopes,
    // player capsule and 2.05 m reach as AcesCafeLayoutSetup.ValidateOccupiedCirculationV2),
    // written out with the colliders blocking each cell, each customer position's
    // status and the footprint of every collider in the room, so a refused layout can
    // be read rather than guessed at: Logs/CafeFurnishing/<label>-<time>.json.
    // It changes nothing in the scene.
    static string CirculationMap(string label)
    {
        Physics.SyncTransforms();
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Named(string n) => all.FirstOrDefault(t => t.name == n) ?? throw new InvalidOperationException("Missing layout reference: " + n);
        var player = Named("Player").GetComponent<CharacterController>()
            ?? throw new InvalidOperationException("Player CharacterController is missing.");
        var targets = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude)
            .Where(s => s.gameObject.scene == scene).Select(s => s.StandPoint)
            .Concat(Named("CounterQueue").Cast<Transform>()).Where(t => t != null).Distinct().ToArray();
        var occupied = targets.Select(t => new Vector2(t.position.x, t.position.z)).ToArray();
        float radius = player.radius * Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z));
        float height = Mathf.Max(player.height * Mathf.Abs(player.transform.lossyScale.y), radius * 2f);
        const float step = .20f, minX = -7.2f, minZ = -3.8f, standingRadius = .65f, reach = 2.05f;
        const int cols = 73, rows = 109;
        float floorY = Named("Floor").position.y;
        float bodyClearance = radius + standingRadius + .03f;
        var start = new Vector2(player.transform.position.x, player.transform.position.z);
        var state = new char[cols * rows];
        var blockers = new Dictionary<int, List<string>>();
        var hits = new Collider[64];
        int seed = -1;
        float seedDistance = .75f * .75f;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < cols; x++)
        {
            int i = z * cols + x;
            var point = new Vector2(minX + x * step, minZ + z * step);
            bool envelope = occupied.Any(p => (point - p).sqrMagnitude < bodyClearance * bodyClearance);
            var bottom = new Vector3(point.x, floorY + .06f + radius, point.y);
            var top = new Vector3(point.x, floorY + .06f + height - radius, point.y);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + .03f, hits, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = count == hits.Length;
            var names = new List<string>();
            for (int h = 0; h < count; h++)
            {
                if (hits[h] == null || hits[h].GetComponentInParent<PlayerMovement>() != null) continue;
                blocked = true;
                if (names.Count < 4) names.Add(ShortPath(hits[h].transform));
            }
            if (names.Count > 0) blockers[i] = names;
            state[i] = envelope ? 'E' : blocked ? 'C' : '.';
            if (state[i] != '.') continue;
            float distance = (point - start).sqrMagnitude;
            if (distance < seedDistance) { seedDistance = distance; seed = i; }
        }
        if (seed >= 0)
        {
            var frontier = new Queue<int>();
            frontier.Enqueue(seed);
            state[seed] = 'R';
            while (frontier.Count > 0)
            {
                int i = frontier.Dequeue(), x = i % cols, z = i / cols;
                foreach (int next in new[] { x > 0 ? i - 1 : -1, x < cols - 1 ? i + 1 : -1, z > 0 ? i - cols : -1, z < rows - 1 ? i + cols : -1 })
                    if (next >= 0 && state[next] == '.') { state[next] = 'R'; frontier.Enqueue(next); }
            }
        }

        var json = new StringBuilder("{\n");
        json.Append($"\"label\":\"{label}\",\"minX\":{F(minX)},\"minZ\":{F(minZ)},\"step\":{F(step)},\"cols\":{cols},\"rows\":{rows},");
        json.Append($"\"radius\":{F(radius)},\"height\":{F(height)},\"bodyClearance\":{F(bodyClearance)},\"reach\":{F(reach)},");
        json.Append($"\"floorY\":{F(floorY)},\"start\":[{F(start.x)},{F(start.y)}],\"seed\":{seed},\n\"cells\":\"{new string(state)}\",\n\"targets\":[");
        int reachable = 0;
        lastApproach.Clear();
        for (int t = 0; t < targets.Length; t++)
        {
            float best = float.PositiveInfinity;
            int approach = 0;
            for (int i = 0; i < state.Length; i++)
            {
                if (state[i] != 'R') continue;
                float distance = (new Vector2(minX + i % cols * step, minZ + i / cols * step) - occupied[t]).magnitude;
                best = Mathf.Min(best, distance);
                if (distance <= reach) approach++;
            }
            bool ok = best <= reach;
            if (ok) reachable++;
            lastApproach[ShortPath(targets[t])] = approach;
            json.Append(t == 0 ? "\n" : ",\n").Append($"{{\"name\":\"{Esc(targets[t].parent.name + "/" + targets[t].name)}\",\"path\":\"{Esc(ShortPath(targets[t]))}\",")
                .Append($"\"x\":{F(occupied[t].x)},\"z\":{F(occupied[t].y)},\"nearest\":{(float.IsInfinity(best) ? "null" : F(best))},")
                .Append($"\"approachCells\":{approach},\"reachable\":{(ok ? "true" : "false")}}}");
        }
        json.Append("],\n\"blockers\":{");
        bool first = true;
        foreach (var kv in blockers)
        {
            json.Append(first ? "\n" : ",\n").Append($"\"{kv.Key}\":[{string.Join(",", kv.Value.Select(n => "\"" + Esc(n) + "\""))}]");
            first = false;
        }
        // Every collider over the grid that a walking player could touch (bottom under
        // the capsule's top): an oriented box for box colliders, else world bounds.
        json.Append("},\n\"colliders\":[");
        first = true;
        float gridMaxX = minX + (cols - 1) * step, gridMaxZ = minZ + (rows - 1) * step;
        foreach (var c in CityPackChecks.InScene<Collider>())
        {
            if (!c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger || c.GetComponentInParent<PlayerMovement>() != null) continue;
            Bounds b = c is BoxCollider box ? WorldBox(box) : c.bounds;
            if (b.max.x < minX - 1f || b.min.x > gridMaxX + 1f || b.max.z < minZ - 1f || b.min.z > gridMaxZ + 1f) continue;
            if (b.min.y > floorY + .06f + height || b.max.y < floorY + .02f) continue;
            // Footprint: the outline (convex hull) of the box's eight corners seen from
            // above - a box may be turned any way (our Z-up exports stand on their side).
            var corners = new List<Vector3>();
            if (c is BoxCollider bc)
            {
                Matrix4x4 m = bc.transform.localToWorldMatrix;
                Vector3 e = bc.size * .5f;
                var points = new List<Vector2>();
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p = m.MultiplyPoint3x4(bc.center + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z));
                    points.Add(new Vector2(p.x, p.z));
                }
                foreach (var p in Hull(points)) corners.Add(new Vector3(p.x, 0f, p.y));
            }
            else
            {
                corners.Add(new Vector3(b.min.x, 0f, b.min.z)); corners.Add(new Vector3(b.max.x, 0f, b.min.z));
                corners.Add(new Vector3(b.max.x, 0f, b.max.z)); corners.Add(new Vector3(b.min.x, 0f, b.max.z));
            }
            json.Append(first ? "\n" : ",\n").Append($"{{\"name\":\"{Esc(ShortPath(c.transform))}\",\"type\":\"{c.GetType().Name}\",")
                .Append($"\"y\":[{F(b.min.y)},{F(b.max.y)}],\"xz\":[{string.Join(",", corners.Select(p => "[" + F(p.x) + "," + F(p.z) + "]"))}]}}");
            first = false;
        }
        json.Append("]\n}\n");
        Directory.CreateDirectory(CafeFurnishingCatalog.LogRoot);
        string file = Path.Combine(CafeFurnishingCatalog.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".json");
        File.WriteAllText(file, json.ToString());
        return $"{file} ({reachable}/{targets.Length} customer positions reachable)";
    }

    // Delivery-approach cells per customer position from the last map: reachable
    // cells within 2.05 m of it, with every customer in place.
    static readonly Dictionary<string, int> lastApproach = new Dictionary<string, int>();
    const float MinApproachKept = .4f;

    // Passing the circulation check can hinge on one cell of floor; this also asks
    // that every customer keeps a fair share (40%) of the floor they could be served
    // from before. Returns the positions that fall short, or null.
    static string ThinApproaches(Dictionary<string, int> before, Dictionary<string, int> after)
    {
        var thin = new List<string>();
        foreach (var kv in before)
        {
            if (kv.Value == 0) continue;
            int now = after.TryGetValue(kv.Key, out int n) ? n : 0;
            int needed = Mathf.CeilToInt(kv.Value * MinApproachKept);
            if (now < needed) thin.Add($"{kv.Key}: {kv.Value} -> {now} approach cells (needs {needed})");
        }
        return thin.Count == 0 ? null : string.Join("\n", thin);
    }

    // Convex hull (monotone chain), counter-clockwise.
    static List<Vector2> Hull(List<Vector2> points)
    {
        var p = points.Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        if (p.Count < 3) return p;
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var hull = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (var v in pass == 0 ? p : Enumerable.Reverse(p))
            {
                while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], v) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(v);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }

    static string ShortPath(Transform t)
    {
        var parts = new List<string>();
        for (var p = t; p != null && parts.Count < 3; p = p.parent) parts.Insert(0, p.name);
        return string.Join("/", parts);
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static Bounds WorldBox(BoxCollider c)
    {
        Matrix4x4 m = c.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(c.center), Vector3.zero);
        Vector3 e = c.size * .5f;
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(c.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
        return b;
    }

    static void FloorCollider(Transform piece, string name, Vector3 centre, Vector3 size)
    {
        var box = piece.gameObject.AddComponent<BoxCollider>();
        box.center = centre;
        box.size = size;
        floorPieces.Add((piece, name));
    }

    // ------------------------------------------------------------------ surfaces

    // The highest visible surface under (x, z) no higher than 'below', from the real
    // triangles of every mesh over the point (combined meshes' big bounds can't fake
    // a surface; rugs, the staff floor inset and props placed a moment ago all count).
    static float TopAt(Vector2 p, float below, Transform ignore)
    {
        float best = float.NegativeInfinity;
        var world = new List<Vector3>();
        foreach (var f in CityPackChecks.InScene<MeshFilter>())
        {
            if (ignore != null && f.transform.IsChildOf(ignore)) continue;
            var r = f.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
            Bounds b = r.bounds;
            if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.z || p.y > b.max.z || b.min.y > below) continue;
            if (!meshCache.TryGetValue(f.sharedMesh, out var data)) meshCache[f.sharedMesh] = data = (f.sharedMesh.vertices, f.sharedMesh.triangles);
            Matrix4x4 m = f.transform.localToWorldMatrix;
            world.Clear();
            foreach (var v in data.vertices) world.Add(m.MultiplyPoint3x4(v));
            for (int i = 0; i + 2 < data.triangles.Length; i += 3)
            {
                Vector3 a = world[data.triangles[i]], c = world[data.triangles[i + 1]], d = world[data.triangles[i + 2]];
                float det = (c.z - d.z) * (a.x - d.x) + (d.x - c.x) * (a.z - d.z);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float l1 = ((c.z - d.z) * (p.x - d.x) + (d.x - c.x) * (p.y - d.z)) / det;
                float l2 = ((d.z - a.z) * (p.x - d.x) + (a.x - d.x) * (p.y - d.z)) / det;
                float l3 = 1f - l1 - l2;
                if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                float y = l1 * a.y + l2 * c.y + l3 * d.y;
                if (y <= below && y > best) best = y;
            }
        }
        return best;
    }

    static float TopOf(GameObject go) => RendererBounds(go.transform).max.y;
    static float TopOf(Transform t) => RendererBounds(t).max.y;

    // ------------------------------------------------------------------ small helpers

    static Transform Zone(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    // A group at a world position and turn (or a local one inside another group).
    static Transform Node(Transform parent, string name, Vector3 position, float yaw, bool local = false)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        if (local) { t.localPosition = position; t.localRotation = Quaternion.Euler(0f, yaw, 0f); }
        else t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        placed.Add(name);
        return t;
    }

    static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, Vector3 euler = default) =>
        Primitive(parent, name, PrimitiveType.Cube, centre, size, material, euler);

    // Unity's cylinder is 1 wide and 2 tall.
    static GameObject Cyl(Transform parent, string name, Vector3 centre, float diameter, float height, Material material, Vector3 euler = default) =>
        Primitive(parent, name, PrimitiveType.Cylinder, centre, new Vector3(diameter, height * .5f, diameter), material, euler);

    static GameObject Ball(Transform parent, string name, Vector3 centre, float diameter, Material material) =>
        Primitive(parent, name, PrimitiveType.Sphere, centre, Vector3.one * diameter, material, default);

    static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 centre, Vector3 size, Material material, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centre;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    static Bounds RendererBounds(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // Prefab-instance edits made by script are recorded, so they survive a reload.
    static void RecordAll(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            foreach (var c in t.GetComponents<Component>())
                if (c != null && (c is Collider || c is Light || c is Behaviour || c is Renderer || c is MeshFilter)
                    && PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }

    // ------------------------------------------------------------------ photos

    public static string Photograph(string label)
    {
        string folder = Path.Combine(CafeFurnishingCatalog.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var hidden = new List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (var r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try
        {
            GameObject shop = GameObject.Find("CmShopCam");
            if (shop != null)
                CafeSecondPassSteps.Capture(Path.Combine(folder, "01-game-camera.png"), shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
            var counter = Object.FindObjectsByType<CinemachineCamera>().FirstOrDefault(c => c.name == "CmCounterCam");
            if (counter != null)
                CafeSecondPassSteps.Capture(Path.Combine(folder, "02-counter-camera.png"), counter.transform.position,
                    counter.transform.position + counter.transform.forward * 5f, counter.Lens.FieldOfView, false);
            foreach (var view in Views)
                CafeSecondPassSteps.Capture(Path.Combine(folder, view.name + ".png"), view.from, view.to, view.fov, false);
        }
        finally { foreach (var r in hidden) if (r != null) r.forceRenderingOff = false; }
        return folder;
    }

    static readonly (string name, Vector3 from, Vector3 to, float fov)[] Views =
    {
        ("03-window-lounge", new Vector3(-1.9f, 1.55f, 1.0f), new Vector3(-6.2f, .55f, 5.2f), 62f),
        ("04-lounge-close", new Vector3(-3.7f, 1.25f, 1.0f), new Vector3(-5.9f, .45f, 2.9f), 55f),
        ("05-reading-nook", new Vector3(-2.0f, 1.6f, 12.2f), new Vector3(-6.6f, .9f, 8.4f), 62f),
        ("06-window-bar", new Vector3(2.4f, 1.6f, 12.3f), new Vector3(6.8f, .9f, 7.2f), 62f),
        ("07-entrance", new Vector3(.6f, 1.6f, 4.6f), new Vector3(.2f, 1.45f, -1.2f), 72f),
        ("08-patio", new Vector3(4.6f, 1.7f, -5.4f), new Vector3(1.4f, .6f, -1.0f), 58f),
        ("09-counter-cubbies", new Vector3(-.4f, 1.05f, 10.4f), new Vector3(-.4f, .42f, 13.6f), 66f),
        ("10-work-areas", new Vector3(.9f, 1.7f, 14.6f), new Vector3(-3.4f, .9f, 17.6f), 64f),
        ("11-back-corner", new Vector3(2.6f, 1.6f, 14.4f), new Vector3(6.2f, 1.1f, 17.8f), 62f),
        ("12-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.1f, 12f), 72f),
        ("13-waiting-nook", new Vector3(3.2f, 1.6f, 5.4f), new Vector3(6.9f, 1.0f, 2.3f), 62f),
        ("14-rear-sofa", new Vector3(-4.2f, 1.3f, 9.4f), new Vector3(-6.8f, .5f, 6.8f), 58f),
    };
}
#endif
