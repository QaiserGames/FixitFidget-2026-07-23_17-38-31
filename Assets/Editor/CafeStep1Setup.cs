#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// One-click scene setup for café step 1.
//
//      Menu:  Fixit Fidget ▸ Set up Café Step 1
//
// Builds the intake shelf, wires the counter's drop spot, and lays out the
// waiting spots. Safe to run more than once — it reuses anything it already
// made rather than making a second copy.
//
// Everything it does is a normal editor action, so Ctrl+Z undoes it and
// nothing is saved until you save the scene.
//
// This file lives in an Editor folder, so it is NOT compiled into a build.
public static class CafeStep1Setup
{
    private const int ShelfSlotCount = 5;

    // Fraction of the counter's half-width the slots are allowed to use.
    // 1.0 would put the outer two items exactly on the edge, half hanging off.
    private const float ShelfInset = 0.78f;

    // How far above the counter surface an item's anchor sits.
    private const float ShelfLift = 0.10f;

    private static readonly (Vector3 pos, float yaw)[] WaitSpots =
    {
        (new Vector3(-3.5f, 0f,  7.5f), 160f),
        (new Vector3(-2.0f, 0f,  9.2f), 175f),
        (new Vector3( 2.0f, 0f,  9.2f), 185f),
        (new Vector3( 3.5f, 0f,  7.5f), 200f),
        (new Vector3(-5.0f, 0f, 11.0f), 165f),
        (new Vector3( 5.0f, 0f, 11.0f), 195f),
    };

    [MenuItem("Fixit Fidget/Set up Café Step 1")]
    public static void Run()
    {
        List<string> log = new List<string>();
        List<string> warn = new List<string>();

        // Resolve the counter by what it IS, not what it's called.
        // Assets/Art/Models/Counter.fbx has a root object called "Counter" too,
        // and searching by name grabs the mesh instead of the gameplay object.
        GameObject counter = FindCounter();
        if (counter == null)
        {
            EditorUtility.DisplayDialog("Café setup",
                "Couldn't find the counter.\n\nLooking for a StationInteractable " +
                "with 'Is Work Surface' unticked. Open SampleScene and try again.", "OK");
            return;
        }
        log.Add("Counter resolved as '" + counter.name + "'");

        // ---------- 1. the shelf object ----------

        GameObject shelf = Find("IntakeShelf");
        if (shelf == null)
        {
            shelf = new GameObject("IntakeShelf");
            Undo.RegisterCreatedObjectUndo(shelf, "Café setup");
            shelf.transform.position = counter.transform.position;
            log.Add("Created IntakeShelf");
        }
        else log.Add("Reused existing IntakeShelf");

        // ---------- 2. slot transforms ----------
        // The old ItemSpot0/1/2 under CounterQueue are now orphaned — they sit
        // at exactly the right height, so adopt them instead of making new ones.

        List<Transform> slots = new List<Transform>();
        CounterQueue queueComp = Object.FindAnyObjectByType<CounterQueue>();
        GameObject queue = queueComp != null ? queueComp.gameObject : null;

        // Measure the counter instead of assuming. Its BoxCollider is 1x1x1 with
        // the transform scaled x4, so the surface is only 4 units wide — slots
        // laid out beyond that end up hanging in the air off the end.
        Bounds top = SurfaceBounds(counter);
        float halfWidth = top.extents.x * ShelfInset;
        float slotY = top.max.y + ShelfLift;
        float slotZ = top.center.z - 0.05f;
        log.Add(string.Format("Counter surface measured: x {0:0.00} to {1:0.00}, top y {2:0.00}",
                              top.min.x, top.max.x, top.max.y));

        for (int i = 0; i < ShelfSlotCount; i++)
        {
            float t = ShelfSlotCount == 1 ? 0.5f : i / (float)(ShelfSlotCount - 1);
            float slotX = Mathf.Lerp(top.center.x - halfWidth, top.center.x + halfWidth, t);

            string wanted = "Shelf_" + i;
            Transform tr = shelf.transform.Find(wanted);

            if (tr == null && queue != null && i >= 1 && i <= 3)
            {
                // ItemSpot0 → Shelf_1, ItemSpot1 → Shelf_2, ItemSpot2 → Shelf_3
                Transform old = queue.transform.Find("ItemSpot" + (i - 1));
                if (old != null)
                {
                    Undo.SetTransformParent(old, shelf.transform, "Café setup");
                    old.name = wanted;
                    tr = old;
                    log.Add("Adopted ItemSpot" + (i - 1) + " as " + wanted);
                }
            }

            if (tr == null)
            {
                GameObject go = new GameObject(wanted);
                Undo.RegisterCreatedObjectUndo(go, "Café setup");
                go.transform.SetParent(shelf.transform, false);
                tr = go.transform;
                log.Add("Created " + wanted);
            }

            // Line them up cleanly whatever they were before.
            Undo.RecordObject(tr, "Café setup");
            tr.position = new Vector3(slotX, slotY, slotZ);
            tr.rotation = counter.transform.rotation;
            slots.Add(tr);
        }

        // ---------- 3. ItemSlotArea on the shelf ----------

        ItemSlotArea area = shelf.GetComponent<ItemSlotArea>();
        if (area == null)
        {
            area = Undo.AddComponent<ItemSlotArea>(shelf);
            log.Add("Added ItemSlotArea to IntakeShelf");
        }

        SerializedObject soArea = new SerializedObject(area);
        SerializedProperty pSlots = soArea.FindProperty("slots");
        pSlots.arraySize = slots.Count;
        for (int i = 0; i < slots.Count; i++)
            pSlots.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        soArea.FindProperty("baseSlots").intValue = 3;          // 3 now, 5 with upgrades
        soArea.FindProperty("capacitySource").enumValueIndex = 1; // Shelf
        soArea.ApplyModifiedPropertiesWithoutUndo();
        log.Add("ItemSlotArea: 5 slots, 3 usable, grows on ShelfCapacity");

        // ---------- 4. IntakeShelf component ----------

        IntakeShelf shelfComp = shelf.GetComponent<IntakeShelf>();
        if (shelfComp == null)
        {
            shelfComp = Undo.AddComponent<IntakeShelf>(shelf);
            log.Add("Added IntakeShelf component");
        }
        SerializedObject soShelf = new SerializedObject(shelfComp);
        soShelf.FindProperty("slotArea").objectReferenceValue = area;
        soShelf.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 4b. undo the damage from the name-matching version ----------

        foreach (DropSpot stray in Object.FindObjectsByType<DropSpot>(
                     FindObjectsInactive.Include))
        {
            if (stray == null || stray.gameObject == counter) continue;

            SerializedObject soStray = new SerializedObject(stray);
            if (soStray.FindProperty("slotArea").objectReferenceValue != area) continue;

            string strayName = stray.gameObject.name;
            Undo.DestroyObjectImmediate(stray);
            log.Add("Removed a stray DropSpot from '" + strayName + "' (earlier run put it on the wrong object)");
        }

        // ---------- 5. the counter's drop spot ----------

        DropSpot counterDrop = counter.GetComponent<DropSpot>();
        if (counterDrop == null)
        {
            counterDrop = Undo.AddComponent<DropSpot>(counter);
            log.Add("Added a DropSpot to Counter");
        }
        SerializedObject soDrop = new SerializedObject(counterDrop);
        soDrop.FindProperty("kind").enumValueIndex = 1;   // Counter
        soDrop.FindProperty("slotArea").objectReferenceValue = area;
        soDrop.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Counter DropSpot → IntakeShelf");

        // This was never wired, so you could never set anything down at the
        // counter. Fixing it here because the shelf makes it matter.
        StationInteractable counterStation = counter.GetComponent<StationInteractable>();
        if (counterStation != null)
        {
            SerializedObject soStation = new SerializedObject(counterStation);
            SerializedProperty pDrop = soStation.FindProperty("dropSpot");
            bool wasEmpty = pDrop.objectReferenceValue == null;
            pDrop.objectReferenceValue = counterDrop;
            soStation.ApplyModifiedPropertiesWithoutUndo();
            log.Add(wasEmpty
                ? "Counter StationInteractable.dropSpot was EMPTY — wired it up"
                : "Counter StationInteractable.dropSpot confirmed");
        }
        else
        {
            warn.Add("The counter has no StationInteractable — is '" + counter.name + "' really the counter?");
        }

        // ---------- 6. waiting spots ----------

        GameObject waitRoot = Find("WaitingArea");
        if (waitRoot == null)
        {
            waitRoot = new GameObject("WaitingArea");
            Undo.RegisterCreatedObjectUndo(waitRoot, "Café setup");
            log.Add("Created WaitingArea");
        }

        WaitingArea areaComp = waitRoot.GetComponent<WaitingArea>();
        if (areaComp == null)
        {
            areaComp = Undo.AddComponent<WaitingArea>(waitRoot);
            log.Add("Added WaitingArea component");
        }

        bool navMeshMissing = false;

        for (int i = 0; i < WaitSpots.Length; i++)
        {
            string wanted = "WaitSpot " + (i + 1);
            Transform t = waitRoot.transform.Find(wanted);
            if (t == null)
            {
                GameObject go = new GameObject(wanted);
                Undo.RegisterCreatedObjectUndo(go, "Café setup");
                go.transform.SetParent(waitRoot.transform, false);
                t = go.transform;
            }

            Vector3 target = WaitSpots[i].pos;

            // Snap onto the NavMesh so nobody walks to a spot they can't reach.
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                target = hit.position;
            else
                navMeshMissing = true;

            Undo.RecordObject(t, "Café setup");
            t.position = target;
            t.rotation = Quaternion.Euler(0f, WaitSpots[i].yaw, 0f);

            WaitingSpot spot = t.GetComponent<WaitingSpot>();
            if (spot == null) spot = Undo.AddComponent<WaitingSpot>(t.gameObject);

            SerializedObject soSpot = new SerializedObject(spot);
            soSpot.FindProperty("kind").enumValueIndex = 0;          // Loiter
            soSpot.FindProperty("drainMultiplier").floatValue = 1f;
            soSpot.FindProperty("standPoint").objectReferenceValue = null;
            soSpot.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("6 waiting spots placed" + (navMeshMissing ? " (some OFF the NavMesh)" : " and snapped to the NavMesh"));

        // Leave WaitingArea.spots empty — it collects children on Awake, so
        // anything you add later is picked up with no extra wiring.
        SerializedObject soWaitArea = new SerializedObject(areaComp);
        soWaitArea.FindProperty("spots").arraySize = 0;
        soWaitArea.ApplyModifiedPropertiesWithoutUndo();

        // ---------- warnings ----------

        if (navMeshMissing)
            warn.Add("At least one waiting spot isn't on the NavMesh. Customers will " +
                     "freeze instead of walking there. Bake the NavMesh (Window ▸ AI ▸ " +
                     "Navigation) and run this again.");

        if (Object.FindAnyObjectByType<CustomerSpawner>() == null)
            warn.Add("No CustomerSpawner in the scene?");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<b>Café step 1 setup</b>\n  • " + string.Join("\n  • ", log));
        foreach (string w in warn) Debug.LogWarning("Café setup: " + w);

        EditorUtility.DisplayDialog("Café setup",
            (warn.Count == 0
                ? "Done — everything wired.\n\nCheck the Console for the full list."
                : "Done, but with " + warn.Count + " warning(s).\n\nCheck the Console.")
            + "\n\nNothing is saved until you save the scene (Ctrl+S).",
            "OK");
    }

    // The solid part of the counter. Ignores trigger volumes — the counter also
    // carries a big trigger for the station zone, which is 2.5 deep and would
    // throw the layout off completely.
    private static Bounds SurfaceBounds(GameObject counter)
    {
        bool got = false;
        Bounds b = new Bounds(counter.transform.position, Vector3.zero);

        foreach (Collider c in counter.GetComponentsInChildren<Collider>())
        {
            if (c == null || c.isTrigger) continue;
            if (!got) { b = c.bounds; got = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!got)
        {
            foreach (Renderer r in counter.GetComponentsInChildren<Renderer>())
            {
                if (!got) { b = r.bounds; got = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        // Nothing measurable — fall back to the old hand-placed footprint.
        if (!got) b = new Bounds(new Vector3(0f, 0.5f, 5f), new Vector3(4f, 1f, 1f));

        return b;
    }

    private static GameObject FindCounter()
    {
        foreach (StationInteractable s in Object.FindObjectsByType<StationInteractable>(
                     FindObjectsInactive.Include))
            if (s != null && !s.IsWorkSurface) return s.gameObject;
        return null;
    }

    // ------------------------------------------------------------------
    // A read-only check. Changes nothing, just tells you what's wired.
    // ------------------------------------------------------------------
    [MenuItem("Fixit Fidget/Check Café Step 1")]
    public static void Check()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder("<b>Café step 1 — check</b>\n");
        int fails = 0;

        void Line(bool ok, string what)
        {
            if (!ok) fails++;
            sb.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        GameObject counter = FindCounter();
        Line(counter != null, "counter found" + (counter != null ? " ('" + counter.name + "')" : ""));

        IntakeShelf shelf = Object.FindAnyObjectByType<IntakeShelf>();
        Line(shelf != null, "IntakeShelf in scene");
        Line(shelf != null && shelf.HasRoom, "shelf reports free space");

        ItemSlotArea shelfArea = shelf != null ? shelf.GetComponent<ItemSlotArea>() : null;
        Line(shelfArea != null, "shelf has an ItemSlotArea");

        DropSpot counterDrop = counter != null ? counter.GetComponent<DropSpot>() : null;
        Line(counterDrop != null, "counter has a DropSpot");
        if (counterDrop != null)
        {
            SerializedObject so = new SerializedObject(counterDrop);
            Line(so.FindProperty("slotArea").objectReferenceValue == shelfArea,
                 "counter DropSpot points at the shelf");
            Line(so.FindProperty("kind").enumValueIndex == 1, "counter DropSpot kind = Counter");
        }

        StationInteractable station = counter != null ? counter.GetComponent<StationInteractable>() : null;
        if (station != null)
        {
            SerializedObject so = new SerializedObject(station);
            Line(so.FindProperty("dropSpot").objectReferenceValue == counterDrop,
                 "counter station points at its DropSpot");
        }

        int strays = 0;
        foreach (DropSpot d in Object.FindObjectsByType<DropSpot>(
                     FindObjectsInactive.Include))
        {
            if (d == null || d.gameObject == counter) continue;
            SerializedObject so = new SerializedObject(d);
            if (so.FindProperty("slotArea").objectReferenceValue == shelfArea) strays++;
        }
        Line(strays == 0, "no stray DropSpots pointing at the shelf" + (strays > 0 ? " (found " + strays + ")" : ""));

        WaitingArea wa = Object.FindAnyObjectByType<WaitingArea>();
        Line(wa != null, "WaitingArea in scene");

        WaitingSpot[] spots = Object.FindObjectsByType<WaitingSpot>(
            FindObjectsInactive.Include);
        Line(spots.Length > 0, spots.Length + " waiting spots");

        int offMesh = 0;
        foreach (WaitingSpot sp in spots)
            if (!NavMesh.SamplePosition(sp.StandPoint.position, out _, 0.6f, NavMesh.AllAreas)) offMesh++;
        Line(offMesh == 0, "every waiting spot is on the NavMesh" + (offMesh > 0 ? " (" + offMesh + " are NOT)" : ""));

        sb.AppendLine(fails == 0 ? "\nAll good — go play." : "\n" + fails + " problem(s). Run 'Set up Café Step 1' again.");
        if (fails == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include))
            if (go.name == name) return go;
        return null;
    }
}
#endif
