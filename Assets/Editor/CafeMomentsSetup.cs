#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Café moments (26 Sept): the small things NPCs do that make the room feel
// lived in. See CafeMoment and NpcPose.
//
// "1 - Place" adds, under the café root, in the open scene:
//  * Sofa seats   - up to two TableSeats on each window-lounge sofa, Snap To Seat
//                   on, Ambient Only (patrons only; the customers' seat pool and
//                   the patron valve are unchanged). Seats keep clear of props
//                   lying on the sofa (the book, the throw).
//  * Bookshelf    - one browse spot in front of the reading-nook bookcase, with a
//                   copy of the lounge's book as the book people take.
//  * Leans        - one per Loiter waiting spot that has a wall close behind it:
//                   the customer waiting there leans on the wall instead of
//                   standing on the mark. Patience drain is unchanged.
//  * Patron leans - up to three free wall spots away from the door, the counter
//                   and everyone's places, for patrons who'd rather stand, or who
//                   find every seat taken.
//  * Lounge fix   - the lounge table and tub chair are lower than the NavMesh's
//                   0.75 m step height, so the bake treats them as floor and
//                   NPCs could path straight through the tub chair. A carving
//                   NavMeshObstacle on each fixes that at runtime, no re-bake.
//
// Everything is one Undo step and nothing is saved: check the placement in the
// Scene view (gizmos: blue sofa seats, orange shelf, green leans), move anything
// that looks wrong, then save the scene yourself. "2 - Remove" takes it all out.
public static class CafeMomentsSetup
{
    const string Menu = "Fixit Fidget/NPC/Cafe moments ";
    const string Tag = "[Cafe moments] ";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    const string RootName = "20 - cafe moments (NPC idle spots)";

    // Body and placement measurements, metres.
    const float SeatHeight = .45f;          // sofa seat surface above the floor
    const float SeatInFromFront = .3f;      // hips this far back from the sofa's front edge
    const float SeatSpacing = .9f;          // between two sitters on one sofa
    const float PropClearance = .45f;       // from a book or throw lying on the seat
    const float LeanFeetFromWall = .34f;    // with NpcPose's 6 degrees, shoulders just touch
    const float BrowseFeetFromShelf = .42f;
    const float StandClear = .3f;           // stand points: nothing solid within this radius
    const float CarveClear = .55f;          // stand points: this far from anything carving the NavMesh at runtime

    // Colliders that carve the NavMesh once the game runs (the lounge fix), so
    // the edit-time NavMesh still shows floor there. Stand points keep clear.
    static readonly List<Collider> carving = new List<Collider>();

    [MenuItem(Menu + "1 - Place sofa, bookshelf and lean spots")]
    static void PlaceMenu() => Place();

    [MenuItem(Menu + "2 - Remove them")]
    static void RemoveMenu() => Remove();

    [MenuItem(Menu + "1 - Place sofa, bookshelf and lean spots", true)]
    [MenuItem(Menu + "2 - Remove them", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void Place()
    {
        Transform cafe = FindAnywhere(CafeRootName);
        Transform parentForRoot = cafe;
        if (FindAnywhere(RootName) != null)
        {
            EditorUtility.DisplayDialog("Cafe moments", "Cafe moments are already placed in this scene. " +
                                        "Run \"2 - Remove them\" first to place them again.", "OK");
            return;
        }

        Physics.SyncTransforms();
        Vector3 door = DoorPoint();
        var report = new StringBuilder();
        var taken = new List<Vector3>(PlacesPeopleStand());

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place cafe moments");

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Place cafe moments");
        if (parentForRoot != null) root.transform.SetParent(parentForRoot, false);

        int sofaSeats = 0, leans = 0, patronLeans = 0;
        bool shelf = false;

        // First, so every stand point below keeps clear of what it will carve.
        int carved = CarveLoungeFurniture(report);

        foreach (string sofaName in new[] { "Front window banquette", "Reading window banquette" })
        {
            Transform sofa = FindAnywhere(sofaName);
            if (sofa == null) { report.AppendLine("- Sofa \"" + sofaName + "\" not found: no seats there."); continue; }
            sofaSeats += PlaceSofaSeats(sofa, root.transform, door, taken, report);
        }

        Transform bookcase = FindAnywhere("Oak neighborhood bookcase");
        if (bookcase == null) report.AppendLine("- Bookcase \"Oak neighborhood bookcase\" not found: no bookshelf spot.");
        else shelf = PlaceBookshelf(bookcase, root.transform, door, taken, report);

        foreach (WaitingSpot spot in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
        {
            if (spot is TableSeat || spot.Kind != WaitingSpot.SpotKind.Loiter) continue;
            if (PlaceLoiterLean(spot, root.transform, report)) leans++;
        }

        patronLeans = PlacePatronLeans(root.transform, door, taken, report);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        string summary = $"Placed {sofaSeats} sofa seat(s), {(shelf ? 1 : 0)} bookshelf spot, {leans} lean(s) at " +
                         $"waiting spots and {patronLeans} patron lean(s); {carved} lounge piece(s) now carve the NavMesh.";
        Debug.Log(Tag + summary + (report.Length > 0 ? "\n" + report : "") +
                  "\nCheck the gizmos in the Scene view, move anything that looks wrong, then save the scene. " +
                  "Run Fixit Fidget > Cafe furnishing > Map the circulation to confirm the player can still get round.", root);
        EditorUtility.DisplayDialog("Cafe moments", summary + "\n\nNothing is saved yet. Details are in the Console. " +
                                    "Check the spots in the Scene view, then save the scene.", "OK");
    }

    public static void Remove()
    {
        Transform root = FindAnywhere(RootName);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove cafe moments");
        int removed = 0;
        if (root != null) { Undo.DestroyObjectImmediate(root.gameObject); removed++; }
        foreach (string piece in new[] { "Lounge table", "Tub chair" })
        {
            Transform t = FindAnywhere(piece);
            NavMeshObstacle obstacle = t != null ? t.GetComponent<NavMeshObstacle>() : null;
            if (obstacle != null) { Undo.DestroyObjectImmediate(obstacle); removed++; }
        }
        Undo.CollapseUndoOperations(group);
        if (removed > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(Tag + (removed > 0 ? "Removed the cafe moments and the lounge obstacles. Nothing saved."
                                     : "Nothing to remove."));
    }

    // ------------------------------------------------------------------ sofa

    static int PlaceSofaSeats(Transform sofa, Transform root, Vector3 door, List<Vector3> taken, StringBuilder report)
    {
        if (!RendererBounds(sofa, out Bounds bounds)) { report.AppendLine("- " + sofa.name + ": no mesh to measure."); return 0; }
        Vector3 facing = FrontOf(sofa, bounds.center, bounds);
        Vector3 along = Vector3.Cross(Vector3.up, facing);
        float front = Extent(bounds, facing), half = Extent(bounds, along);
        float floor = FloorHeight(bounds.center);
        Vector3 centre = Flat(bounds.center, floor);

        // Props lying on this sofa (the book, the throw): not part of the sofa, small, inside its box.
        var props = new List<Vector3>();
        Bounds grown = bounds;
        grown.Expand(new Vector3(.1f, .3f, .1f));
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.transform.IsChildOf(sofa) || !grown.Contains(r.bounds.center)) continue;
            if (r.bounds.size.x > .7f || r.bounds.size.z > .7f) continue;
            if (r.bounds.center.y < floor + .3f || r.bounds.center.y > floor + .8f) continue;   // on the seat, not the back cushions
            props.Add(Flat(r.bounds.center, floor));
        }

        var chosen = new List<float>();
        for (int pass = 0; pass < 2; pass++)
        {
            float best = float.NaN, bestScore = float.NegativeInfinity;
            for (float o = -half + .45f; o <= half - .45f + 1e-3f; o += .05f)
            {
                Vector3 hip = centre + facing * (front - SeatInFromFront) + along * o;
                float fromProps = float.PositiveInfinity;
                foreach (Vector3 p in props) fromProps = Mathf.Min(fromProps, Vector3.Distance(hip, p));
                if (fromProps < PropClearance) continue;
                bool tooClose = false;
                foreach (float c in chosen) if (Mathf.Abs(c - o) < SeatSpacing) tooClose = true;
                if (tooClose) continue;
                // Prefer a quarter of the way in from each end, and room from any prop.
                float score = -Mathf.Abs(Mathf.Abs(o) - half * .5f) + Mathf.Min(fromProps, 1.5f) * .3f;
                if (score > bestScore) { bestScore = score; best = o; }
            }
            if (!float.IsNaN(best)) chosen.Add(best);
        }

        int placed = 0;
        foreach (float o in chosen)
        {
            Vector3 hipFloor = centre + facing * (front - SeatInFromFront) + along * o;
            if (!FindStandPoint(hipFloor, facing, new[] { .95f, 1.15f, .8f }, door, out Vector3 stand, out string why))
            {
                report.AppendLine($"- {sofa.name}: skipped a seat at {Fmt(hipFloor)} ({why}).");
                continue;
            }
            var seatGo = new GameObject($"Sofa seat ({sofa.name}, {placed + 1})");
            Undo.RegisterCreatedObjectUndo(seatGo, "Place cafe moments");
            seatGo.transform.SetParent(root, false);
            seatGo.transform.SetPositionAndRotation(hipFloor, Quaternion.LookRotation(facing));
            Transform standT = Child(seatGo.transform, "StandPoint", stand, Quaternion.LookRotation(-facing));
            Transform poseT = Child(seatGo.transform, "SeatPose", hipFloor + Vector3.up * SeatHeight, Quaternion.LookRotation(facing));
            Transform cupT = Child(seatGo.transform, "CupSpot", hipFloor + facing * .7f + Vector3.up * SeatHeight, Quaternion.LookRotation(facing));

            TableSeat seat = Undo.AddComponent<TableSeat>(seatGo);
            var so = new SerializedObject(seat);
            Set(so, "kind", (int)WaitingSpot.SpotKind.Seat);
            Set(so, "drainMultiplier", .6f);
            Set(so, "ambientOnly", true);
            Set(so, "snapToSeat", true);
            Set(so, "standPoint", standT);
            Set(so, "seatPose", poseT);
            Set(so, "cupSpot", cupT);
            so.ApplyModifiedPropertiesWithoutUndo();

            CafeMoment moment = Undo.AddComponent<CafeMoment>(seatGo);
            var mo = new SerializedObject(moment);
            Set(mo, "kind", (int)CafeMoment.Kind.Couch);
            Set(mo, "seat", seat);
            Set(mo, "bookRest", poseT);
            mo.ApplyModifiedPropertiesWithoutUndo();

            taken.Add(stand);
            placed++;
            report.AppendLine($"- Sofa seat on {sofa.name} at {Fmt(poseT.position)}, stand point {Fmt(stand)}.");
        }
        return placed;
    }

    // ------------------------------------------------------------------ bookshelf

    static bool PlaceBookshelf(Transform bookcase, Transform root, Vector3 door, List<Vector3> taken, StringBuilder report)
    {
        Collider solid = bookcase.GetComponent<Collider>();
        Bounds bounds;
        if (solid != null) bounds = solid.bounds;
        else if (!RendererBounds(bookcase, out bounds)) { report.AppendLine("- Bookcase: nothing to measure."); return false; }

        Vector3 facing = FrontOf(bookcase, bounds.center, bounds);
        float front = Extent(bounds, facing);
        float floor = FloorHeight(bounds.center);
        Vector3 centre = Flat(bounds.center, floor);
        Vector3 feet = centre + facing * (front + BrowseFeetFromShelf);
        if (!FindStandPoint(centre + facing * front, facing, new[] { .95f, 1.1f, 1.25f }, door, out Vector3 stand, out string why))
        {
            report.AppendLine("- Bookcase: no free floor in front of it (" + why + ").");
            return false;
        }

        var go = new GameObject("Bookshelf browse (" + bookcase.name + ")");
        Undo.RegisterCreatedObjectUndo(go, "Place cafe moments");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(stand, Quaternion.LookRotation(-facing));
        Transform standT = Child(go.transform, "StandPoint", stand, Quaternion.LookRotation(-facing));
        Transform poseT = Child(go.transform, "Feet at the shelf", feet, Quaternion.LookRotation(-facing));

        // The book people take: a copy of the one left on the lounge sofa, kept hidden here.
        GameObject template = null;
        Transform source = FindAnywhere("Book left on the sofa");
        Transform sourceBook = source != null ? source.Find("Book 1") : null;
        if (sourceBook == null) sourceBook = source;
        if (sourceBook != null)
        {
            template = Object.Instantiate(sourceBook.gameObject);
            Undo.RegisterCreatedObjectUndo(template, "Place cafe moments");
            template.name = "Book to read (template)";
            template.transform.SetParent(go.transform, true);
            template.transform.SetPositionAndRotation(poseT.position + Vector3.up * 1.2f, sourceBook.rotation);
            foreach (Collider c in template.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            template.SetActive(false);
        }

        CafeMoment moment = Undo.AddComponent<CafeMoment>(go);
        var mo = new SerializedObject(moment);
        Set(mo, "kind", (int)CafeMoment.Kind.Bookshelf);
        Set(mo, "standPoint", standT);
        Set(mo, "posePoint", poseT);
        Set(mo, "bookTemplate", template);
        mo.ApplyModifiedPropertiesWithoutUndo();

        taken.Add(stand);
        report.AppendLine($"- Bookshelf spot at {Fmt(stand)}" +
                          (template != null ? ", book copied from the lounge." : ", placeholder book (lounge book not found)."));
        return true;
    }

    // ------------------------------------------------------------------ leans

    static bool PlaceLoiterLean(WaitingSpot spot, Transform root, StringBuilder report)
    {
        Vector3 stand = spot.StandPoint.position;
        if (!FindWall(stand, 1.4f, out RaycastHit wall, out Vector3 feet)) return false;
        float step = Flat(feet - stand, 0f).magnitude;
        if (step > 1f)
        {
            report.AppendLine($"- {spot.name}: the wall is {step:0.00} m from the spot, too far to lean. Standing as before.");
            return false;
        }

        var go = new GameObject("Lean (" + spot.name + ")");
        Undo.RegisterCreatedObjectUndo(go, "Place cafe moments");
        go.transform.SetParent(root, false);
        go.transform.position = stand;
        Transform poseT = Child(go.transform, "Feet against the wall", feet, Quaternion.LookRotation(Flat(wall.normal, 0f).normalized));

        CafeMoment moment = Undo.AddComponent<CafeMoment>(go);
        var mo = new SerializedObject(moment);
        Set(mo, "kind", (int)CafeMoment.Kind.Lean);
        Set(mo, "standPoint", spot.StandPoint);
        Set(mo, "posePoint", poseT);
        Set(mo, "waitingSpot", spot);
        mo.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine($"- {spot.name}: customers waiting here lean on {wall.collider.name} ({step:0.00} m step).");
        return true;
    }

    // Free stretches of wall on the customers' side of the counter, away from the
    // door and from everybody's places. Conservative on purpose; move or delete
    // any that land somewhere you'd rather keep clear.
    static int PlacePatronLeans(Transform root, Vector3 door, List<Vector3> taken, StringBuilder report)
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        CounterLine(door, out Vector3 inward, out float counterAt);
        var candidates = new List<(Vector3 stand, Vector3 feet, Vector3 normal, string wall)>();
        var seen = new HashSet<Vector3Int>();
        foreach (Vector3 vertex in mesh.vertices)
        {
            if (!NavMesh.FindClosestEdge(vertex, out NavMeshHit edge, NavMesh.AllAreas)) continue;
            Vector3 stand = edge.position;
            if (Mathf.Abs(stand.y - door.y) > .3f) continue;                                   // floor only
            if (!seen.Add(Vector3Int.RoundToInt(stand * 2f))) continue;                         // one per half metre
            if (Vector3.Dot(Flat(stand - door, 0f), inward) > counterAt - .8f) continue;        // not at or behind the counter
            if (Flat(stand - door, 0f).magnitude < 3f) continue;                                // not by the door
            if (Nearest(stand, taken) < 1.6f) continue;                                         // not on anyone's place
            if (!ClearOfCarving(stand)) continue;
            if (!FindWall(stand, .9f, out RaycastHit wall, out Vector3 feet)) continue;
            if (Flat(feet - stand, 0f).magnitude > 1f) continue;
            if (!Reachable(door, stand)) continue;
            candidates.Add((stand, feet, wall.normal, wall.collider.name));
        }

        int placed = 0;
        var used = new List<Vector3>();
        while (placed < 3)
        {
            int best = -1;
            float bestSpread = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float spread = Mathf.Min(Nearest(candidates[i].stand, taken), Nearest(candidates[i].stand, used));
                if (Nearest(candidates[i].stand, used) < 3f) continue;
                if (spread > bestSpread) { bestSpread = spread; best = i; }
            }
            if (best < 0) break;

            var c = candidates[best];
            var go = new GameObject($"Patron lean ({placed + 1})");
            Undo.RegisterCreatedObjectUndo(go, "Place cafe moments");
            go.transform.SetParent(root, false);
            go.transform.position = c.stand;
            Transform standT = Child(go.transform, "StandPoint", c.stand, Quaternion.LookRotation(Flat(c.normal, 0f).normalized));
            Transform poseT = Child(go.transform, "Feet against the wall", c.feet, Quaternion.LookRotation(Flat(c.normal, 0f).normalized));
            CafeMoment moment = Undo.AddComponent<CafeMoment>(go);
            var mo = new SerializedObject(moment);
            Set(mo, "kind", (int)CafeMoment.Kind.Lean);
            Set(mo, "standPoint", standT);
            Set(mo, "posePoint", poseT);
            mo.ApplyModifiedPropertiesWithoutUndo();

            used.Add(c.stand);
            taken.Add(c.stand);
            placed++;
            report.AppendLine($"- Patron lean on {c.wall} at {Fmt(c.feet)}.");
        }
        if (placed == 0) report.AppendLine("- No free wall found for patron leans (they'll hover and go when the seats are full, as before).");
        return placed;
    }

    // ------------------------------------------------------------------ lounge fix

    static int CarveLoungeFurniture(StringBuilder report)
    {
        carving.Clear();
        int carved = 0;
        foreach (string piece in new[] { "Lounge table", "Tub chair" })
        {
            Transform t = FindAnywhere(piece);
            BoxCollider box = t != null ? t.GetComponent<BoxCollider>() : null;
            if (box == null) { report.AppendLine("- " + piece + ": not found (or no box collider), not carved."); continue; }
            carving.Add(box);
            if (t.GetComponent<NavMeshObstacle>() != null) continue;
            NavMeshObstacle obstacle = Undo.AddComponent<NavMeshObstacle>(t.gameObject);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            carved++;
        }
        return carved;
    }

    // ------------------------------------------------------------------ helpers

    // The nearest wall-like surface behind a spot: casts round it at shoulder height.
    static bool FindWall(Vector3 stand, float range, out RaycastHit wall, out Vector3 feet)
    {
        wall = default;
        feet = stand;
        float best = float.PositiveInfinity;
        Vector3 from = stand + Vector3.up * 1.1f;
        for (int i = 0; i < 16; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            if (!Physics.Raycast(from, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (IsPerson(hit.collider) || Mathf.Abs(hit.normal.y) > .3f || !IsWallLike(hit.collider)) continue;
            if (hit.distance < best) { best = hit.distance; wall = hit; }
        }
        if (float.IsInfinity(best)) return false;

        Vector3 normal = Flat(wall.normal, 0f).normalized;
        feet = Flat(wall.point, stand.y) + normal * LeanFeetFromWall;

        // Nothing but the wall where the body will be.
        foreach (Collider c in Physics.OverlapCapsule(feet + Vector3.up * .3f, feet + Vector3.up * 1.5f, .22f, ~0, QueryTriggerInteraction.Ignore))
            if (c != wall.collider && !IsPerson(c)) return false;
        return true;
    }

    // A stand point in front of something: on the NavMesh, clear of solid things, reachable from the door.
    static bool FindStandPoint(Vector3 frontEdge, Vector3 facing, float[] distances, Vector3 door, out Vector3 stand, out string why)
    {
        why = "no NavMesh in front of it";
        foreach (float d in distances)
        {
            Vector3 probe = frontEdge + facing * d;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .3f, NavMesh.AllAreas)) continue;
            stand = hit.position;
            if (Physics.CheckCapsule(stand + Vector3.up * .3f, stand + Vector3.up * 1.6f, StandClear, ~0, QueryTriggerInteraction.Ignore)
                && !OnlyPeopleAt(stand))
            { why = "furniture in the way"; continue; }
            if (!ClearOfCarving(stand)) { why = "too close to the lounge table or tub chair"; continue; }
            if (!Reachable(door, stand)) { why = "not reachable from the door"; continue; }
            return true;
        }
        stand = default;
        return false;
    }

    static bool ClearOfCarving(Vector3 stand)
    {
        foreach (Collider c in carving)
            if (c != null && Flat(c.ClosestPoint(stand) - stand, 0f).magnitude < CarveClear) return false;
        return true;
    }

    static bool OnlyPeopleAt(Vector3 stand)
    {
        foreach (Collider c in Physics.OverlapCapsule(stand + Vector3.up * .3f, stand + Vector3.up * 1.6f, StandClear, ~0, QueryTriggerInteraction.Ignore))
            if (!IsPerson(c)) return false;
        return true;
    }

    // Something a person would lean on: at least shoulder height and wider than
    // a body - a wall, a window, the bookcase. Not a stool, a lamp or a coat stand.
    static bool IsWallLike(Collider c)
    {
        Bounds b = c.bounds;
        return b.size.y >= 1.5f && Mathf.Max(b.size.x, b.size.z) >= 1f;
    }

    static bool IsPerson(Collider c) =>
        c.GetComponentInParent<NavMeshAgent>() != null || c.GetComponentInParent<CharacterController>() != null;

    // Which side of this object faces the café floor: its forward, or the reverse.
    static Vector3 FrontOf(Transform t, Vector3 centre, Bounds bounds)
    {
        Vector3 f = Flat(t.forward, 0f);
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        f.Normalize();
        float reach = Extent(bounds, f) + .9f;
        bool ahead = NavMesh.SamplePosition(centre + f * reach, out _, .4f, NavMesh.AllAreas);
        bool behind = NavMesh.SamplePosition(centre - f * reach, out _, .4f, NavMesh.AllAreas);
        return behind && !ahead ? -f : f;
    }

    static float Extent(Bounds b, Vector3 dir) => Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;

    static float FloorHeight(Vector3 near) =>
        NavMesh.SamplePosition(near, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position.y : 0f;

    static bool RendererBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    static Vector3 DoorPoint()
    {
        GameObject spawn = GameObject.Find("SpawnPoint");
        Vector3 at = spawn != null ? spawn.transform.position : Vector3.zero;
        return NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : at;
    }

    // Direction from the door into the café, and how far along it the counter queue stands.
    static void CounterLine(Vector3 door, out Vector3 inward, out float counterAt)
    {
        inward = Vector3.forward;
        counterAt = float.PositiveInfinity;
        CounterQueue queue = Object.FindAnyObjectByType<CounterQueue>();
        if (queue == null || queue.SlotCount == 0) return;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < queue.SlotCount; i++) sum += queue.SlotPoint(i).position;
        Vector3 centre = sum / queue.SlotCount;
        Vector3 toCounter = Flat(centre - door, 0f);
        if (toCounter.sqrMagnitude < 1e-4f) return;
        inward = toCounter.normalized;
        counterAt = toCounter.magnitude;
    }

    // Everywhere somebody already has a place: waiting spots, seats, queue slots, station stand points.
    static IEnumerable<Vector3> PlacesPeopleStand()
    {
        foreach (WaitingSpot s in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
            yield return s.StandPoint.position;
        CounterQueue queue = Object.FindAnyObjectByType<CounterQueue>();
        if (queue != null)
            for (int i = 0; i < queue.SlotCount; i++) yield return queue.SlotPoint(i).position;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name == "StandPoint" || t.name == "CounterStandPoint") yield return t.position;
    }

    static bool Reachable(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    static float Nearest(Vector3 p, List<Vector3> others)
    {
        float best = float.PositiveInfinity;
        foreach (Vector3 o in others) best = Mathf.Min(best, Flat(p - o, 0f).magnitude);
        return best;
    }

    static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Place cafe moments");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        return go.transform;
    }

    static Transform FindAnywhere(string name)
    {
        foreach (GameObject top in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindDeep(top.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void Set(SerializedObject so, string field, Object value) => Prop(so, field).objectReferenceValue = value;
    static void Set(SerializedObject so, string field, bool value) => Prop(so, field).boolValue = value;
    static void Set(SerializedObject so, string field, float value) => Prop(so, field).floatValue = value;
    static void Set(SerializedObject so, string field, int enumIndex) => Prop(so, field).enumValueIndex = enumIndex;

    static SerializedProperty Prop(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.InvalidOperationException(Tag + so.targetObject.GetType().Name + " has no field " + field);
        return p;
    }

    static Vector3 Flat(Vector3 v, float y) => new Vector3(v.x, y, v.z);
    static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.z:0.00})";
}
#endif
