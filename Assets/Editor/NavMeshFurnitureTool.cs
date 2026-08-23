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
    private const string SURFACE  = "Unity.AI.Navigation.NavMeshSurface";
    private const string ASSETMGR = "Unity.AI.Navigation.Editor.NavMeshAssetManager";

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

    [MenuItem("Fixit Fidget/NavMesh/2 · Re-bake NavMesh")]
    public static void Rebake()
    {
        System.Type surfaceType = FindType(SURFACE);
        if (surfaceType == null) { NoPackage(); return; }

        Object[] surfaces = Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
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

        Object[] surfaces = Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
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
            ? Object.FindObjectsByType(modType, FindObjectsSortMode.None).Length : 0;

        lines.Add((mods > 0 ? "✔  " : "✘  ") + mods + " NavMeshModifier(s) — " +
                  (mods == 0
                      ? "every mesh in the room is walkable, INCLUDING TABLE TOPS"
                      : "furniture is excluded from the walkable surface"));

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
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject go in all)
            if (go.name == name) return go;

        return null;
    }
}
