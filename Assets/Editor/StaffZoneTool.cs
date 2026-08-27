using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// One job: stop customers standing in the strip between the service counter
// and the espresso machine.
//
// WHY THIS IS A NavMeshObstacle AND NOT A MODIFIER VOLUME
//
// A NavMeshModifierVolume changes what gets BAKED, so every adjustment costs a
// re-bake, and the result isn't visible until you run one. That loop is how the
// last attempt went wrong — a bad guess at the bounds wasn't obvious until the
// whole café had been deleted from the walkable floor.
//
// A NavMeshObstacle carves the NavMesh at RUNTIME. Nothing is baked, nothing in
// the scene is restructured, and the box draws itself in the Scene view. Get it
// wrong and you drag it; delete it and the room is exactly as it was. It also
// can't touch the player, who is a CharacterController and never consults the
// NavMesh at all.
//
// Deliberately its own file with no reflection and no package types — stock
// UnityEngine.AI only, so nothing here can break the other editor tools.
public static class StaffZoneTool
{
    private const string ZONE = "StaffZone_NoEntry";
    private const string OLD_VOLUME = "StaffOnly_NoCustomers";

    // Breathing room around the two objects, so there's somewhere to stand
    // rather than a box skin-tight to the equipment.
    private const float Margin = 0.6f;

    [MenuItem("Fixit Fidget/NavMesh/Block staff zone (no bake)")]
    public static void Build()
    {
        GameObject counter = Find("Counter");
        GameObject machine = Find("EspressoMachine");

        if (counter == null || machine == null)
        {
            EditorUtility.DisplayDialog("Staff zone",
                "Couldn't find both 'Counter' and 'EspressoMachine' in the scene.\n\n" +
                "Rename them to match, or select the two objects you want spanned " +
                "and use the box handles after this creates it.", "OK");
            if (counter == null && machine == null) return;
        }

        // Span exactly the two things you named — measured, not inferred.
        Bounds area;
        if (counter != null && machine != null)
        {
            area = Bounds(counter);
            area.Encapsulate(Bounds(machine));
        }
        else
        {
            area = Bounds(counter != null ? counter : machine);
        }

        area.Expand(new Vector3(Margin * 2f, 0f, Margin * 2f));

        // Remove the baked-volume attempt so there's only ever one mechanism
        // deciding where customers may stand.
        GameObject stale = Find(OLD_VOLUME);
        if (stale != null)
        {
            Undo.DestroyObjectImmediate(stale);
            Debug.Log("[Staff zone] Removed the old " + OLD_VOLUME + " volume — " +
                      "re-bake once to clear it out of the baked NavMesh.");
        }

        GameObject go = Find(ZONE);
        if (go == null)
        {
            go = new GameObject(ZONE);
            Undo.RegisterCreatedObjectUndo(go, "Staff zone");
        }

        Undo.RecordObject(go.transform, "Staff zone");
        go.transform.position = new Vector3(area.center.x, 0f, area.center.z);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        NavMeshObstacle obs = go.GetComponent<NavMeshObstacle>();
        if (obs == null) obs = Undo.AddComponent<NavMeshObstacle>(go);

        Undo.RecordObject(obs, "Staff zone");
        obs.shape = NavMeshObstacleShape.Box;
        obs.center = new Vector3(0f, 1f, 0f);
        obs.size = new Vector3(area.size.x, 2f, area.size.z);
        obs.carving = true;
        obs.carveOnlyStationary = true;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        EditorUtility.DisplayDialog("Staff zone",
            "Created '" + ZONE + "' and selected it.\n\n" +
            "It spans your counter and espresso machine plus " + Margin + " m:\n\n" +
            "   x " + area.min.x.ToString("F1") + " to " + area.max.x.ToString("F1") + "\n" +
            "   z " + area.min.z.ToString("F1") + " to " + area.max.z.ToString("F1") + "\n\n" +
            "NO BAKING. It carves at runtime — press Play and customers already " +
            "can't enter it. Drag it or edit Size on the Nav Mesh Obstacle and " +
            "the carve follows immediately.\n\n" +
            "It can't affect you: the player is a CharacterController and never " +
            "uses the NavMesh.\n\n" +
            "To undo all of this: delete the object.", "OK");
    }

    [MenuItem("Fixit Fidget/NavMesh/Remove staff zone")]
    public static void Remove()
    {
        GameObject go = Find(ZONE);
        if (go == null)
        {
            EditorUtility.DisplayDialog("Staff zone", "No " + ZONE + " in the scene.", "OK");
            return;
        }

        Undo.DestroyObjectImmediate(go);
        EditorUtility.DisplayDialog("Staff zone",
            "Removed. The floor is exactly as it was — nothing was baked.", "OK");
    }

    // ---------- helpers ----------

    private static Bounds Bounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name == name) return go;
        return null;
    }
}
