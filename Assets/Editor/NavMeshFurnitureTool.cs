using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only. Stops customers walking over the furniture.
//
// THE BUG THIS FIXES
//
// The NavMeshSurface on `Floor` is set to Collect Objects = All, Use Geometry =
// Render Meshes, Default Area = Walkable. That means the bake takes EVERY mesh
// in the room — tables, chairs, benches, the lot — and generates walkable
// navmesh on top of each one.
//
// The Humanoid agent type's default Step Height is 0.75 m. A chair seat is
// ~0.45 m and a tabletop ~0.72 m, so both are "a step up" to the pathfinder.
// Customers path straight over the furniture, which reads as walking over an
// invisible rock. And NavMesh.SamplePosition happily reports a chair seat as
// the nearest valid floor, which is why waiting customers stand ON chairs.
//
// THE FIX: tag the furniture as Not Walkable and re-bake. Then it carves a
// hole instead of making a platform.
//
// Everything here talks to the AI Navigation package through reflection, so
// this file compiles whether or not that package is present — a missing
// package gives you a readable dialog instead of breaking every editor script
// in the project.
public static class NavMeshFurnitureTool
{
    private const string UNDO = "NavMesh furniture";
    private const int NotWalkable = 1;      // Unity's built-in area index

    private const string MODIFIER = "Unity.AI.Navigation.NavMeshModifier";
    private const string VOLUME   = "Unity.AI.Navigation.NavMeshModifierVolume";
    private const string SURFACE  = "Unity.AI.Navigation.NavMeshSurface";
    private const string ASSETMGR = "Unity.AI.Navigation.Editor.NavMeshAssetManager";

    private const string STAFF_BLOCK = "StaffOnly_NoCustomers";

    // Standing room to leave around the equipment.
    private const float StaffMargin = 1.0f;

    // Used when nothing is selected — the working nook, not half the room.
    private static readonly string[] DefaultStaffObjects =
        { "KitchenCounter", "EspressoMachine" };

    // ==================================================================

    [MenuItem("Fixit Fidget/NavMesh/1 · Mark furniture Not Walkable")]
    public static void MarkFurniture()
    {
        GameObject furniture = FindInScene("FURNITURE");

        if (furniture == null)
        {
            EditorUtility.DisplayDialog("NavMesh",
                "No object called FURNITURE in the scene.\n\n" +
                "Select your tables and chairs by hand and use " +
                "'Mark selection Not Walkable' instead.", "OK");
            return;
        }

        // NavMeshModifier is hierarchical — putting one on FURNITURE covers
        // every table and chair underneath it. One component, whole café.
        int n = Apply(new[] { furniture });

        EditorUtility.DisplayDialog("NavMesh",
            (n > 0 ? "FURNITURE is now Not Walkable." : "FURNITURE was already marked.") +
            "\n\nIt's hierarchical, so every table and chair under it is covered.\n\n" +
            "Deliberately NOT touching the Counter, Workbench or walls — they're " +
            "over 1 m tall, so nobody can step onto them anyway, and carving " +
            "around the counter risks cutting the queue slots off the NavMesh.\n\n" +
            "NOW RE-BAKE: run '2 · Re-bake NavMesh', or select Floor and press " +
            "Bake on its NavMeshSurface.", "OK");
    }

    [MenuItem("Fixit Fidget/NavMesh/Mark selection Not Walkable")]
    public static void MarkSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("NavMesh", "Nothing selected.", "OK");
            return;
        }

        int n = Apply(Selection.gameObjects);
        EditorUtility.DisplayDialog("NavMesh",
            n + " object(s) marked Not Walkable.\n\nRe-bake to see the change.", "OK");
    }

    [MenuItem("Fixit Fidget/NavMesh/Clear Not Walkable from selection")]
    public static void ClearSelection()
    {
        System.Type modType = FindType(MODIFIER);
        if (modType == null) { NoPackage(); return; }

        int n = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            Component c = go.GetComponent(modType);
            if (c == null) continue;
            Undo.DestroyObjectImmediate(c);
            n++;
        }

        EditorUtility.DisplayDialog("NavMesh", n + " modifier(s) removed. Re-bake.", "OK");
    }

    // ==================================================================

    // ==================================================================
    // 2 · the staff area
    // ==================================================================

    [MenuItem("Fixit Fidget/NavMesh/2 · Create staff-area block")]
    public static void CreateStaffBlock()
    {
        System.Type volType = FindType(VOLUME);
        if (volType == null) { NoPackage(); return; }

        // ---- WHAT TO BLOCK: whatever you point at ----
        //
        // The first version drew one line across the room — everything behind
        // the counter's front face. That is the right rule for a normal shop
        // and completely wrong for this one: all 37 pieces of café furniture
        // sit at z between -10.4 and +2.4, BEHIND the service counter, on the
        // same side as the kitchen. So "behind the counter" deleted the café
        // and left the kitchen as the only walkable floor.
        //
        // There is no clean line through this room, so stop looking for one.
        // Wrap only the objects that actually make up the work area.

        List<GameObject> targets = new List<GameObject>();

        foreach (GameObject sel in Selection.gameObjects)
            if (sel != null && sel.GetComponentInChildren<Renderer>() != null) targets.Add(sel);

        bool fromSelection = targets.Count > 0;

        if (!fromSelection)
        {
            foreach (string n in DefaultStaffObjects)
            {
                GameObject g = FindInScene(n);
                if (g != null && g.GetComponentInChildren<Renderer>() != null) targets.Add(g);
            }
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("NavMesh",
                "Nothing to wrap.\n\n" +
                "Select the objects that make up your work area — the kitchen " +
                "counter, the espresso machine, anything else customers should " +
                "not be able to stand at — and run this again.", "OK");
            return;
        }

        Bounds area = WorldBounds(targets[0]);
        for (int i = 1; i < targets.Count; i++) area.Encapsulate(WorldBounds(targets[i]));

        // Room to stand and work behind the equipment, without reaching so far
        // that it swallows a chair.
        area.Expand(new Vector3(StaffMargin * 2f, 0f, StaffMargin * 2f));

        Vector3 centre = new Vector3(area.center.x, 2f, area.center.z);
        Vector3 size = new Vector3(area.size.x, 5f, area.size.z);

        // ---- would this strand anybody? Say so BEFORE they bake ----

        List<string> caught = new List<string>();
        foreach (WaitingSpot s in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Include))
        {
            if (s == null) continue;
            Vector3 p = s.StandPoint.position;
            if (p.x > area.min.x && p.x < area.max.x && p.z > area.min.z && p.z < area.max.z)
                caught.Add(s.name);
        }

        if (caught.Count > 0 &&
            !EditorUtility.DisplayDialog("NavMesh",
                "This would cut " + caught.Count + " waiting spot(s) off the floor:\n\n" +
                string.Join(", ", caught.ToArray()) + "\n\n" +
                "Customers who claim those would walk toward somewhere they can't " +
                "reach. Block it anyway, or cancel and pick fewer objects?",
                "Block it anyway", "Cancel"))
            return;

        GameObject go = FindInScene(STAFF_BLOCK);
        if (go == null)
        {
            go = new GameObject(STAFF_BLOCK);
            Undo.RegisterCreatedObjectUndo(go, UNDO);
        }

        Undo.RecordObject(go.transform, UNDO);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        Component vol = go.GetComponent(volType);
        if (vol == null) vol = Undo.AddComponent(go, volType);

        SerializedObject so = new SerializedObject(vol);
        SerializedProperty pSize = so.FindProperty("m_Size");
        SerializedProperty pCentre = so.FindProperty("m_Center");
        SerializedProperty pArea = so.FindProperty("m_Area");

        if (pSize != null)   pSize.vector3Value   = size;
        if (pCentre != null) pCentre.vector3Value = centre;
        if (pArea != null)   pArea.intValue       = NotWalkable;
        so.ApplyModifiedProperties();

        Debug.Log($"[NavMesh] Staff block wraps {targets.Count} object(s): " +
                  $"x {area.min.x:F2}..{area.max.x:F2}, z {area.min.z:F2}..{area.max.z:F2}. " +
                  $"Centre {centre}, size {size}.");

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        SceneView.FrameLastActiveSceneView();

        EditorUtility.DisplayDialog("NavMesh",
            "Staff area blocked, and selected in the Hierarchy.\n\n" +
            "Wrapped " + targets.Count + " object(s)" +
            (fromSelection ? " from your selection" : " (KitchenCounter, EspressoMachine)") +
            ", plus " + StaffMargin + " m of standing room:\n\n" +
            "   x " + area.min.x.ToString("F1") + " to " + area.max.x.ToString("F1") + "\n" +
            "   z " + area.min.z.ToString("F1") + " to " + area.max.z.ToString("F1") + "\n\n" +
            "It's invisible and it can't affect you — the player is a " +
            "CharacterController and never touches the NavMesh.\n\n" +
            "TO CHANGE IT: select the objects you want wrapped and run this " +
            "again. It re-sizes rather than making a second one.\n\n" +
            "NEXT: '3 · Re-bake NavMesh', then SAVE THE SCENE (Ctrl+S) — " +
            "baking writes the NavMesh asset but not the volume itself.", "OK");
    }

    [MenuItem("Fixit Fidget/NavMesh/3 · Re-bake NavMesh")]
    public static void Rebake()
    {
        System.Type surfaceType = FindType(SURFACE);
        if (surfaceType == null) { NoPackage(); return; }

        Object[] surfaces = Object.FindObjectsByType(surfaceType, FindObjectsInactive.Exclude);
        if (surfaces.Length == 0)
        {
            EditorUtility.DisplayDialog("NavMesh",
                "No NavMeshSurface in the scene — nothing to bake.", "OK");
            return;
        }

        try
        {
            System.Type mgrType = FindType(ASSETMGR);
            object instance = mgrType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            MethodInfo bake = mgrType.GetMethod("StartBakingSurfaces",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            bake.Invoke(instance, new object[] { surfaces });

            EditorUtility.DisplayDialog("NavMesh",
                "Baked " + surfaces.Length + " surface(s).\n\n" +
                "Look at the Scene view: the blue NavMesh should now have HOLES " +
                "where your tables and chairs are, and no blue on top of them.\n\n" +
                "Then re-run Café Step 2 ▸ 3 · Make seats from selection so the " +
                "Stand Points move off the chairs and onto real floor.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[NavMesh] Automatic bake failed: " + e.Message);
            EditorUtility.DisplayDialog("NavMesh",
                "Couldn't bake automatically.\n\n" +
                "Do it by hand — it's one click:\n\n" +
                "1. Select 'Floor' in the Hierarchy\n" +
                "2. Find the NavMesh Surface component in the Inspector\n" +
                "3. Press Bake at the bottom of it", "OK");
        }
    }

    // ==================================================================

    [MenuItem("Fixit Fidget/NavMesh/Check")]
    public static void Check()
    {
        List<string> lines = new List<string>();

        System.Type surfaceType = FindType(SURFACE);
        System.Type modType = FindType(MODIFIER);

        if (surfaceType == null) { NoPackage(); return; }

        Object[] surfaces = Object.FindObjectsByType(surfaceType, FindObjectsInactive.Exclude);
        lines.Add((surfaces.Length > 0 ? "✔  " : "✘  ") + surfaces.Length + " NavMeshSurface(s)");

        foreach (Object s in surfaces)
        {
            SerializedObject so = new SerializedObject(s);
            int collect = so.FindProperty("m_CollectObjects").enumValueIndex;
            int geom = so.FindProperty("m_UseGeometry").enumValueIndex;
            string[] collectNames = { "All", "Volume", "Children" };
            string[] geomNames = { "Render Meshes", "Physics Colliders" };

            lines.Add("   " + ((Component)s).gameObject.name +
                      ": Collect = " + collectNames[Mathf.Clamp(collect, 0, 2)] +
                      ", Geometry = " + geomNames[Mathf.Clamp(geom, 0, 1)]);
        }

        int mods = modType != null
            ? Object.FindObjectsByType(modType, FindObjectsInactive.Exclude).Length : 0;

        lines.Add((mods > 0 ? "✔  " : "✘  ") + mods + " NavMeshModifier(s) — " +
                  (mods == 0
                      ? "every mesh in the room is walkable, INCLUDING TABLE TOPS"
                      : "furniture is excluded from the walkable surface"));

        GameObject block = FindInScene(STAFF_BLOCK);
        Line(lines, block != null, block != null
            ? "staff area is blocked to customers"
            : "no staff-area block — customers can walk round the counter");

        // THE CHECK THAT MATTERS AFTER RESIZING THAT VOLUME. Cut too deep and a
        // seat's stand point lands in dead space; whoever claims it walks
        // toward somewhere unreachable and the jam detector has to rescue them.
        // Far better to hear about it here than to watch it at runtime.
        WaitingSpot[] spots = Object.FindObjectsByType<WaitingSpot>(
            FindObjectsInactive.Exclude);

        List<string> stranded = new List<string>();
        foreach (WaitingSpot s in spots)
        {
            if (s == null) continue;
            if (!UnityEngine.AI.NavMesh.SamplePosition(
                    s.StandPoint.position, out _, 0.6f, UnityEngine.AI.NavMesh.AllAreas))
                stranded.Add(s.name);
        }

        Line(lines, stranded.Count == 0, stranded.Count == 0
            ? "all " + spots.Length + " waiting spots and seats are reachable"
            : stranded.Count + " STRANDED off the NavMesh: " +
              string.Join(", ", stranded.ToArray()));

        string body = string.Join("\n", lines);
        Debug.Log("[NavMesh check]\n" + body);
        EditorUtility.DisplayDialog("NavMesh — check", body, "OK");
    }

    // ==================================================================
    // helpers
    // ==================================================================

    private static int Apply(GameObject[] targets)
    {
        System.Type modType = FindType(MODIFIER);
        if (modType == null) { NoPackage(); return 0; }

        int n = 0;

        foreach (GameObject go in targets)
        {
            if (go == null) continue;

            Component mod = go.GetComponent(modType);
            if (mod == null)
            {
                mod = Undo.AddComponent(go, modType);
                n++;
            }

            SerializedObject so = new SerializedObject(mod);
            SerializedProperty over = so.FindProperty("m_OverrideArea");
            SerializedProperty area = so.FindProperty("m_Area");

            if (over != null) over.boolValue = true;
            if (area != null) area.intValue = NotWalkable;

            so.ApplyModifiedProperties();
        }

        return n;
    }

    private static void NoPackage()
    {
        EditorUtility.DisplayDialog("NavMesh",
            "Couldn't find Unity's AI Navigation package types.\n\n" +
            "Do it by hand instead:\n\n" +
            "1. Select FURNITURE in the Hierarchy\n" +
            "2. Add Component → Nav Mesh Modifier\n" +
            "3. Tick 'Override Area', set Area to 'Not Walkable'\n" +
            "4. Select Floor → NavMesh Surface → Bake", "OK");
    }

    private static void Line(List<string> lines, bool ok, string text)
    {
        lines.Add((ok ? "✔  " : "✘  ") + text);
    }

    private static Bounds WorldBounds(GameObject root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.one);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static System.Type FindType(string fullName)
    {
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type t = a.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static GameObject FindInScene(string name)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include);

        foreach (GameObject go in all)
            if (go.name == name) return go;

        return null;
    }
}
