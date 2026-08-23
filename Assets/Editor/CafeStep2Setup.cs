using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Editor-only tools for Café Step 2 — seating.
//
// REWRITTEN 2026-08-23. The first version built its own tables, which was
// wrong: the café was already furnished. A chair you placed is a MESH, not a
// seat — customer logic only sees objects carrying a WaitingSpot/TableSeat,
// because that's what registers with WaitingArea. So the job of this tool is
// to turn furniture you already own into seats, not to add more furniture.
//
// Menu items, in the order you'd use them:
//
//   Fixit Fidget ▸ Café Step 2 ▸ 1 · Remove sample tables
//        deletes FURNITURE/CafeTables, the two tables the old version made
//
//   Fixit Fidget ▸ Café Step 2 ▸ 2 · Tune shop
//        raises maxCustomers, retires two loiter spots, tidies the shelf
//
//   Fixit Fidget ▸ Café Step 2 ▸ 3 · Make seats from selection
//        select chairs in the Hierarchy → they become claimable seats
//
//   Fixit Fidget ▸ Café Step 2 ▸ Clear seats from selection
//   Fixit Fidget ▸ Café Step 2 ▸ Check
//
// Everything is undoable and nothing is saved until you press Ctrl+S.
public static class CafeStep2Setup
{
    private const string UNDO = "Café step 2";

    // ---------- tuning ----------

    private const float SeatDrain = 0.6f;    // architecture spec §3.1 — sitting is calm

    // How far behind the chair the customer stands, since there's no sit
    // animation yet. Far enough not to be inside the chair, close enough to
    // read as "at this table".
    private const float StandBack = 0.55f;

    // How far to look for the table a chair belongs to.
    private const float TableSearchRadius = 2.5f;

    private const int   NewMaxCustomers = 6;
    private const float NewSpawnInterval = 6f;

    private static readonly string[] RetireLoiterSpots = { "WaitSpot 5", "WaitSpot 6" };

    // ==================================================================
    // 1 · undo the damage from the first version
    // ==================================================================

    [MenuItem("Fixit Fidget/Café Step 2/1 · Remove sample tables")]
    public static void RemoveSampleTables()
    {
        GameObject furniture = FindInScene("FURNITURE");
        Transform tables = furniture != null ? furniture.transform.Find("CafeTables") : null;

        if (tables == null)
        {
            EditorUtility.DisplayDialog("Café Step 2",
                "No FURNITURE/CafeTables found — nothing to remove.", "OK");
            return;
        }

        int seats = tables.GetComponentsInChildren<TableSeat>(true).Length;
        Undo.DestroyObjectImmediate(tables.gameObject);

        EditorUtility.DisplayDialog("Café Step 2",
            "Removed FURNITURE/CafeTables (" + seats + " sample seats).\n\n" +
            "Your own furniture is untouched. Next: run 3 · Make seats from selection.",
            "OK");
    }

    // ==================================================================
    // 2 · the parts of step 2 that had nothing to do with furniture
    // ==================================================================

    [MenuItem("Fixit Fidget/Café Step 2/2 · Tune shop")]
    public static void TuneShop()
    {
        List<string> log = new List<string>();

        Undo.SetCurrentGroupName(UNDO);
        int group = Undo.GetCurrentGroup();

        // --- population cap ---
        //
        // THE BIG ONE. CustomerSpawner counts EVERY living CustomerBrain —
        // queued, settling, waiting, walking to the door. At 3, someone who
        // accepted a job and walked off was still one of your three, so the
        // counter slot freed up and nobody arrived to fill it. Step 1's whole
        // benefit was invisible.

        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null)
        {
            log.Add("✘  No CustomerSpawner in the scene");
        }
        else
        {
            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty pMax = so.FindProperty("maxCustomers");
            SerializedProperty pInt = so.FindProperty("spawnInterval");

            int oldMax = pMax.intValue;
            float oldInt = pInt.floatValue;

            pMax.intValue = NewMaxCustomers;
            pInt.floatValue = NewSpawnInterval;
            so.ApplyModifiedProperties();

            log.Add("maxCustomers " + oldMax + " → " + NewMaxCustomers);
            log.Add("spawnInterval " + oldInt + "s → " + NewSpawnInterval + "s");
        }

        // --- retire two loiter spots so seats are actually contested ---

        int retired = 0;
        foreach (string name in RetireLoiterSpots)
        {
            GameObject go = FindInScene(name);
            if (go == null || !go.activeSelf) continue;
            Undo.RecordObject(go, UNDO);
            go.SetActive(false);
            retired++;
        }
        if (retired > 0) log.Add(retired + " far loiter spots disabled (reversible — just tick them on)");

        // --- tidy the shelf ---

        int killed = CleanOrphanShelfSlots();
        if (killed > 0) log.Add(killed + " orphaned Slot objects removed from IntakeShelf");

        Undo.CollapseUndoOperations(group);

        string body = "• " + string.Join("\n• ", log) + "\n\nCtrl+S to keep, Ctrl+Z to undo.";
        Debug.Log("[Café step 2 · tune] " + body);
        EditorUtility.DisplayDialog("Café Step 2 — tune shop", body, "OK");
    }

    // ==================================================================
    // 3 · the actual point: your chairs become seats
    // ==================================================================

    [MenuItem("Fixit Fidget/Café Step 2/3 · Make seats from selection")]
    public static void MakeSeatsFromSelection()
    {
        GameObject[] picked = Selection.gameObjects;

        if (picked == null || picked.Length == 0)
        {
            EditorUtility.DisplayDialog("Café Step 2",
                "Nothing selected.\n\n" +
                "Select the chairs and stools you want customers to use — in the " +
                "Hierarchy, click the first and ctrl-click the rest — then run this again.\n\n" +
                "Pick the whole chair object, not a mesh inside it.",
                "OK");
            return;
        }

        Undo.SetCurrentGroupName(UNDO);
        int group = Undo.GetCurrentGroup();

        List<string> log = new List<string>();
        int made = 0, updated = 0, offMesh = 0, noTable = 0;

        foreach (GameObject chair in picked)
        {
            if (chair == null) continue;

            // THE GUARD. Selecting the Player by accident used to give you a
            // seat that WALKS AROUND: a customer claims it, paths to wherever
            // you were standing at that instant, and waits there while you
            // carry the seat off. Nothing crashes, which is what makes it so
            // confusing to diagnose.
            string reject = RejectReason(chair);
            if (reject != null)
            {
                log.Add("✘ skipped " + chair.name + " — " + reject);
                continue;
            }

            // Skip anything that's already a waiting spot of another kind —
            // turning a loiter marker into a seat by accident would be a
            // confusing way to lose one.
            WaitingSpot existing = chair.GetComponent<WaitingSpot>();
            if (existing != null && !(existing is TableSeat))
            {
                log.Add("skipped " + chair.name + " — already a " + existing.Kind + " spot");
                continue;
            }

            // ---- which table does this chair belong to? ----

            GameObject table = FindTableFor(chair);
            Vector3 chairPos = chair.transform.position;

            Vector3 faceDir;
            float tableTopY;

            if (table != null)
            {
                Bounds tb = WorldBounds(table);
                Vector3 flat = tb.center - chairPos;
                flat.y = 0f;

                // A chair sitting exactly on the table's centre line gives us
                // nothing to aim at; fall back to its own forward.
                faceDir = flat.sqrMagnitude > 0.0004f ? flat.normalized : chair.transform.forward;
                tableTopY = tb.max.y;
            }
            else
            {
                faceDir = chair.transform.forward;
                tableTopY = chairPos.y + 0.75f;
                noTable++;
            }

            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 0.0001f) faceDir = Vector3.forward;
            faceDir.Normalize();

            Quaternion look = Quaternion.LookRotation(faceDir, Vector3.up);
            Bounds cb = WorldBounds(chair);

            // ---- the component ----

            TableSeat seat = chair.GetComponent<TableSeat>();
            if (seat == null) { seat = Undo.AddComponent<TableSeat>(chair); made++; }
            else updated++;

            // ---- StandPoint: behind the chair, facing the table ----

            Vector3 standPos;
            bool found = FindFloorBehind(chairPos, faceDir, out standPos);

            GameObject stand = ChildAt(chair.transform, "StandPoint", standPos, look);

            if (!found)
            {
                offMesh++;
                log.Add("⚠ " + chair.name + " — no walkable floor behind it");
            }

            // ---- SeatPose: placeholder until a sit animation exists ----

            GameObject pose = ChildAt(chair.transform, "SeatPose",
                                      new Vector3(chairPos.x, cb.center.y, chairPos.z), look);

            // ---- CupSpot: on the table, in front of this chair ----

            float reach = table != null
                ? Mathf.Max(0.15f, Vector3.Distance(new Vector3(chairPos.x, 0, chairPos.z),
                    new Vector3(WorldBounds(table).center.x, 0, WorldBounds(table).center.z)) * 0.6f)
                : 0.35f;

            Vector3 cupPos = chairPos + faceDir * reach;
            cupPos.y = tableTopY + 0.01f;

            GameObject cup = ChildAt(chair.transform, "CupSpot", cupPos, look);

            // ---- wire it ----

            SerializedObject so = new SerializedObject(seat);
            so.FindProperty("kind").enumValueIndex = (int)WaitingSpot.SpotKind.Seat;
            so.FindProperty("drainMultiplier").floatValue = SeatDrain;
            so.FindProperty("standPoint").objectReferenceValue = stand.transform;
            so.FindProperty("seatPose").objectReferenceValue = pose.transform;
            so.FindProperty("cupSpot").objectReferenceValue = cup.transform;
            so.FindProperty("snapToSeat").boolValue = false;
            so.ApplyModifiedProperties();

            log.Add("✔ " + chair.name + (table != null ? "  → faces " + table.name : "  → NO TABLE FOUND, used its own forward"));
        }

        Undo.CollapseUndoOperations(group);

        string body =
            made + " new seat(s), " + updated + " updated.\n\n" +
            string.Join("\n", log);

        if (noTable > 0)
            body += "\n\n⚠ " + noTable + " chair(s) had no table within " + TableSearchRadius +
                    "m. Their Stand Point may face the wrong way — check the green arrows " +
                    "in the Scene view and rotate the StandPoint child if needed.";

        if (offMesh > 0)
            body += "\n\n⚠ " + offMesh + " Stand Point(s) are off the NavMesh. Those seats are " +
                    "unreachable — customers will freeze. Window ▸ AI ▸ Navigation ▸ Bake, " +
                    "or nudge the StandPoint onto blue floor.";

        body += "\n\nCtrl+S to keep, Ctrl+Z to undo.";

        Debug.Log("[Café step 2 · seats] " + body);
        EditorUtility.DisplayDialog("Café Step 2 — seats", body, "OK");
    }

    [MenuItem("Fixit Fidget/Café Step 2/Clear seats from selection")]
    public static void ClearSeatsFromSelection()
    {
        int removed = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || go.GetComponent<TableSeat>() == null) continue;
            StripSeat(go);
            removed++;
        }

        EditorUtility.DisplayDialog("Café Step 2",
            removed + " seat(s) turned back into ordinary furniture.", "OK");
    }

    [MenuItem("Fixit Fidget/Café Step 2/Clear ALL seats in scene")]
    public static void ClearAllSeats()
    {
        TableSeat[] seats = Object.FindObjectsByType<TableSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (seats.Length == 0)
        {
            EditorUtility.DisplayDialog("Café Step 2", "No seats in the scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Café Step 2",
                "Remove all " + seats.Length + " TableSeat(s) in the scene?\n\n" +
                "The furniture itself is untouched — only the seat components and " +
                "the StandPoint / SeatPose / CupSpot children they created.",
                "Remove them", "Cancel"))
            return;

        foreach (TableSeat s in seats) StripSeat(s.gameObject);

        EditorUtility.DisplayDialog("Café Step 2",
            seats.Length + " seat(s) removed. Ctrl+Z undoes it.", "OK");
    }

    // ==================================================================
    // helpers
    // ==================================================================

    // Things that must never become a seat. Everything here either moves, is
    // already something else, or would silently break another system.
    private static string RejectReason(GameObject go)
    {
        if (go.GetComponent<CharacterController>() != null)   return "it's the player — it MOVES";
        if (go.GetComponent<NavMeshAgent>() != null)          return "it moves (NavMeshAgent)";
        if (go.GetComponent<CustomerBrain>() != null)         return "it's a customer";
        if (go.GetComponent<PlayerInteractor>() != null)      return "it's the player";
        if (go.GetComponent<PlayerCarry>() != null)           return "it's the player";
        if (go.GetComponent<PlayerMovement>() != null)        return "it's the player";
        if (go.GetComponent<StationInteractable>() != null)   return "it's a station (counter / bench)";
        if (go.GetComponent<Camera>() != null)                return "it's a camera";
        if (go.GetComponent<ItemSlotArea>() != null)          return "it's a slot area (shelf / bench)";
        return null;
    }

    // Removes a seat and only the children WE made. A child that carries other
    // components or has children of its own is left alone — the Workbench has
    // a real StandPoint that a station depends on, and eating that would be a
    // very quiet way to break the bench.
    private static void StripSeat(GameObject go)
    {
        TableSeat seat = go.GetComponent<TableSeat>();
        if (seat != null) Undo.DestroyObjectImmediate(seat);

        string[] names = { "StandPoint", "SeatPose", "CupSpot",
                           "Seat_StandPoint", "Seat_SeatPose", "Seat_CupSpot" };

        foreach (string n in names)
        {
            Transform t = go.transform.Find(n);
            if (t == null) continue;
            if (!IsOurMarker(t)) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    // A bare Transform with nothing on it and nothing under it.
    private static bool IsOurMarker(Transform t)
    {
        return t.childCount == 0 && t.GetComponents<Component>().Length == 1;
    }

    // Prefer a parent named like a table (a chair parented under its table is
    // an explicit statement of intent), then the nearest table-ish renderer,
    // then the nearest renderer of any kind that isn't seating.
    private static GameObject FindTableFor(GameObject chair)
    {
        Transform p = chair.transform.parent;
        for (int i = 0; i < 2 && p != null; i++, p = p.parent)
            if (LooksLikeTable(p.name)) return p.gameObject;

        Vector3 here = chair.transform.position;

        GameObject bestNamed = null; float bestNamedD = float.MaxValue;
        GameObject bestAny = null;   float bestAnyD = float.MaxValue;

        Renderer[] all = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Renderer r in all)
        {
            if (r.transform.IsChildOf(chair.transform)) continue;
            if (chair.transform.IsChildOf(r.transform)) continue;

            // The player and customers have renderers too, and a chair facing
            // wherever you happened to be standing when you ran this is a very
            // annoying bug to track down later.
            if (r.GetComponentInParent<CharacterController>() != null) continue;
            if (r.GetComponentInParent<CustomerBrain>() != null) continue;

            GameObject candidate = TopOfModel(r.transform, chair.transform);

            Vector3 d = candidate.transform.position - here;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > TableSearchRadius || dist < 0.01f) continue;

            if (LooksLikeTable(candidate.name))
            {
                if (dist < bestNamedD) { bestNamedD = dist; bestNamed = candidate; }
            }
            else if (!LooksLikeSeating(candidate.name))
            {
                if (dist < bestAnyD) { bestAnyD = dist; bestAny = candidate; }
            }
        }

        return bestNamed != null ? bestNamed : bestAny;
    }

    // Walk up out of an FBX's internal mesh children to the object you'd
    // actually click in the Hierarchy.
    private static GameObject TopOfModel(Transform t, Transform stopBefore)
    {
        Transform cur = t;
        while (cur.parent != null
               && cur.parent != stopBefore
               && !cur.parent.name.Equals("FURNITURE")
               && cur.parent.parent != null)
        {
            if (LooksLikeTable(cur.name) || LooksLikeSeating(cur.name)) break;
            cur = cur.parent;
        }
        return cur.gameObject;
    }

    private static bool LooksLikeTable(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("table") || n.Contains("desk");
    }

    private static bool LooksLikeSeating(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("chair") || n.Contains("stool") || n.Contains("seat") || n.Contains("bench");
    }

    // Walks outward from the chair, away from the table, until it finds real
    // walkable floor. Two rules matter here and both came from a bug:
    //
    // 1. THE VERTICAL GUARD. The old version called SamplePosition once with a
    //    1.5 m radius, which cheerfully returned the top of the chair seat —
    //    that's valid NavMesh while the furniture bakes as Walkable. Customers
    //    stood ON the chairs. Anything more than 35 cm off the chair's own
    //    floor height is now rejected as "that's furniture, not floor".
    //
    // 2. THE OUTWARD SEARCH. Once furniture is correctly Not Walkable, the
    //    bake carves a hole around it that's at least the agent radius wide
    //    (0.5 m for the Humanoid type). A fixed 0.55 m offset lands INSIDE
    //    that hole, so it has to keep stepping out until it finds floor.
    private static bool FindFloorBehind(Vector3 chairPos, Vector3 faceDir, out Vector3 result)
    {
        for (float d = StandBack; d <= 2.0f; d += 0.15f)
        {
            Vector3 probe = chairPos - faceDir * d;

            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.45f, NavMesh.AllAreas))
                continue;

            if (Mathf.Abs(hit.position.y - chairPos.y) > 0.35f) continue;   // that's furniture

            Vector2 a = new Vector2(hit.position.x, hit.position.z);
            Vector2 b = new Vector2(probe.x, probe.z);
            if (Vector2.Distance(a, b) > 0.45f) continue;                   // snapped somewhere silly

            result = hit.position;
            return true;
        }

        // Nothing found — leave the marker where it would ideally go so it's
        // visible in the Scene view, and report it.
        result = chairPos - faceDir * StandBack;
        return false;
    }

    private static GameObject ChildAt(Transform parent, string name, Vector3 worldPos, Quaternion worldRot)
    {
        Transform existing = parent.Find(name);

        // DON'T CLOBBER SOMEONE ELSE'S CHILD. The Workbench already has a
        // StandPoint that StationInteractable depends on; moving it would
        // break the bench in a way nothing would report. If a child of this
        // name exists and isn't a bare marker we made, use a prefixed name.
        if (existing != null && !IsOurMarker(existing))
        {
            name = "Seat_" + name;
            existing = parent.Find(name);
        }

        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UNDO);
            go.transform.SetParent(parent, true);
        }

        Undo.RecordObject(go.transform, UNDO);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private static Bounds WorldBounds(GameObject root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.5f);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // Step 1's script made Slot0..Slot4 under IntakeShelf; the manual walk-
    // through then made Shelf_0..Shelf_4. Only the Shelf_ ones are wired.
    private static int CleanOrphanShelfSlots()
    {
        GameObject shelf = FindInScene("IntakeShelf");
        if (shelf == null) return 0;

        ItemSlotArea area = shelf.GetComponent<ItemSlotArea>();
        if (area == null) return 0;

        HashSet<Transform> used = new HashSet<Transform>();
        SerializedObject so = new SerializedObject(area);
        SerializedProperty slots = so.FindProperty("slots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            Transform t = slots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t != null) used.Add(t);
        }

        List<GameObject> doomed = new List<GameObject>();
        foreach (Transform child in shelf.transform)
        {
            if (used.Contains(child)) continue;
            if (child.childCount > 0) continue;
            if (child.GetComponents<Component>().Length > 1) continue;
            if (!child.name.StartsWith("Slot")) continue;
            doomed.Add(child.gameObject);
        }

        foreach (GameObject go in doomed) Undo.DestroyObjectImmediate(go);
        return doomed.Count;
    }

    private static GameObject FindInScene(string name)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject go in all)
            if (go.name == name) return go;

        return null;
    }

    // ==================================================================

    [MenuItem("Fixit Fidget/Café Step 2/Check")]
    public static void Check()
    {
        List<string> lines = new List<string>();

        TableSeat[] seats = Object.FindObjectsByType<TableSeat>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Line(lines, seats.Length > 0, seats.Length + " active TableSeat(s)");

        int offMesh = 0, badKind = 0;
        foreach (TableSeat s in seats)
        {
            if (s.Kind != WaitingSpot.SpotKind.Seat) badKind++;
            if (!NavMesh.SamplePosition(s.StandPoint.position, out _, 0.6f, NavMesh.AllAreas)) offMesh++;
        }

        Line(lines, badKind == 0, "every seat has kind = Seat");
        Line(lines, offMesh == 0, offMesh == 0
            ? "every Stand Point is on the NavMesh"
            : offMesh + " Stand Point(s) OFF the NavMesh — those seats freeze customers");

        WaitingSpot[] all = Object.FindObjectsByType<WaitingSpot>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int loiter = 0;
        foreach (WaitingSpot s in all) if (s.Kind == WaitingSpot.SpotKind.Loiter) loiter++;
        lines.Add("   " + seats.Length + " seats and " + loiter + " loiter spots ACTIVE");

        CustomerSpawner sp = Object.FindAnyObjectByType<CustomerSpawner>();
        if (sp == null) Line(lines, false, "CustomerSpawner in the scene");
        else
        {
            int max = new SerializedObject(sp).FindProperty("maxCustomers").intValue;
            Line(lines, max >= NewMaxCustomers, "maxCustomers = " + max +
                 (max < NewMaxCustomers ? "  ← still capping the shop" : ""));
        }

        Line(lines, Object.FindAnyObjectByType<WaitingArea>() != null, "WaitingArea in scene");

        GameObject furniture = FindInScene("FURNITURE");
        bool leftovers = furniture != null && furniture.transform.Find("CafeTables") != null;
        Line(lines, !leftovers, leftovers
            ? "FURNITURE/CafeTables still present — run 1 · Remove sample tables"
            : "no leftover sample tables");

        string body = string.Join("\n", lines);
        Debug.Log("[Café step 2 check]\n" + body);
        EditorUtility.DisplayDialog("Café Step 2 — check", body, "OK");
    }

    private static void Line(List<string> lines, bool ok, string text)
    {
        lines.Add((ok ? "✔  " : "✘  ") + text);
    }
}
