using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// READ-ONLY. Measures the shop and writes down what it finds.
//
// WHY THIS EXISTS
//
// Unity scenes are stored as prefab-override lists, and reading a world
// position out of that file by hand is unreliable — twice this week it gave an
// answer that contradicted what was plainly on screen. Rather than guess a
// third time, ask Unity, which is the only thing that actually knows.
//
// WHAT IT TOUCHES
//
// Nothing in the scene. No Undo, no components added or removed, no transforms
// moved, no bake. The single side effect is one text file, Assets/RoomMeasure.txt,
// so the report can be read outside the Console. Delete it whenever you like.
public static class RoomMeasure
{
    private const string ReportPath = "Assets/RoomMeasure.txt";

    // The customer NavMeshAgent radius, and the bake radius from the Humanoid
    // agent type. A gap narrower than 2x the agent radius can't be walked
    // through; a gap narrower than 2x the bake radius doesn't get a NavMesh at
    // all. Both matter and they are not the same number.
    private const float AgentRadius = 0.35f;
    private const float BakeRadius = 0.5f;

    [MenuItem("Fixit Fidget/Room/Measure (read-only)")]
    public static void Measure()
    {
        StringBuilder sb = new StringBuilder();

        Line(sb, "==================================================");
        Line(sb, " ROOM MEASURE   scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        Line(sb, "==================================================");
        Line(sb, "");

        GameObject floor = Find("Floor");
        GameObject counter = Find("Counter");
        GameObject spawn = Find("SpawnPoint");

        // ---------- the shell ----------

        Line(sb, "--- FLOOR AND WALLS ---");

        Bounds floorB = default;
        bool haveFloor = false;
        if (floor != null && TryBounds(floor, out floorB))
        {
            haveFloor = true;
            Line(sb, string.Format("Floor        x {0,7:F2} .. {1,7:F2}    z {2,7:F2} .. {3,7:F2}    ({4:F1} x {5:F1} m = {6:F0} m2)",
                floorB.min.x, floorB.max.x, floorB.min.z, floorB.max.z,
                floorB.size.x, floorB.size.z, floorB.size.x * floorB.size.z));
        }
        else Line(sb, "Floor        NOT FOUND (looked for an object named 'Floor')");

        bool wallMinZ = false, wallMaxZ = false, wallMinX = false, wallMaxX = false;

        foreach (GameObject go in AllObjects())
        {
            if (!go.name.StartsWith("Wall")) continue;
            if (!TryBounds(go, out Bounds wb)) continue;

            Line(sb, string.Format("{0,-12} x {1,7:F2} .. {2,7:F2}    z {3,7:F2} .. {4,7:F2}",
                Trim(go.name, 12), wb.min.x, wb.max.x, wb.min.z, wb.max.z));

            if (!haveFloor) continue;
            if (wb.center.z < floorB.center.z - floorB.size.z * 0.3f) wallMinZ = true;
            if (wb.center.z > floorB.center.z + floorB.size.z * 0.3f) wallMaxZ = true;
            if (wb.center.x < floorB.center.x - floorB.size.x * 0.3f) wallMinX = true;
            if (wb.center.x > floorB.center.x + floorB.size.x * 0.3f) wallMaxX = true;
        }

        if (haveFloor)
        {
            Line(sb, "");
            Line(sb, "Enclosure:   -x " + YesNo(wallMinX) + "   +x " + YesNo(wallMaxX) +
                     "   -z " + YesNo(wallMinZ) + "   +z " + YesNo(wallMaxZ));
        }

        Line(sb, "");

        // ---------- the counter, and which side is which ----------

        Line(sb, "--- THE COUNTER LINE ---");

        float counterFront = 0f;
        bool customerSideIsPlusZ = true;
        bool haveCounter = false;

        if (counter != null && TryBounds(counter, out Bounds cb))
        {
            haveCounter = true;

            // Which side do customers arrive from? Whatever side the spawn
            // point is on. Everything else is derived from that rather than
            // assumed, so this still reads correctly if the shop is ever
            // rotated or rebuilt facing the other way.
            customerSideIsPlusZ = spawn == null || spawn.transform.position.z > cb.center.z;

            counterFront = customerSideIsPlusZ ? cb.max.z : cb.min.z;

            Line(sb, string.Format("Counter      x {0,7:F2} .. {1,7:F2}    z {2,7:F2} .. {3,7:F2}",
                cb.min.x, cb.max.x, cb.min.z, cb.max.z));
            Line(sb, "Customer side is " + (customerSideIsPlusZ ? "+z" : "-z") +
                     "   (front face at z = " + counterFront.ToString("F2") +
                     ", staff side is everything past it)");
        }
        else Line(sb, "Counter      NOT FOUND");

        if (spawn != null)
            Line(sb, string.Format("SpawnPoint   ({0,6:F2}, {1,6:F2})", spawn.transform.position.x, spawn.transform.position.z));

        // Queue clearance — the number that can silently break intake.
        CounterQueue queue = Object.FindFirstObjectByType<CounterQueue>();
        if (queue != null && haveCounter)
        {
            Line(sb, "");
            float nearest = float.MaxValue;
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform s = queue.SlotPoint(i);
                if (s == null) continue;

                bool onMesh = NavMesh.SamplePosition(s.position, out NavMeshHit sh, 1f, NavMesh.AllAreas);
                float gap = Mathf.Abs(s.position.z - counterFront);
                nearest = Mathf.Min(nearest, gap);

                Line(sb, string.Format("Slot{0}        ({1,6:F2}, {2,6:F2})   gap to counter face {3:F2} m   navmesh {4}",
                    i, s.position.x, s.position.z, gap, onMesh ? "OK (" + sh.distance.ToString("F2") + " m off)" : "*** NOT ON NAVMESH ***"));
            }

            if (nearest < float.MaxValue)
            {
                Line(sb, "");
                Line(sb, string.Format("Tightest counter->slot gap: {0:F2} m.  Bake radius {1:F2} m, agent radius {2:F2} m.",
                    nearest, BakeRadius, AgentRadius));
                if (nearest < BakeRadius)
                    Line(sb, "*** WARNING: below the bake radius. Any new geometry here can cut the queue off the NavMesh. ***");
                else if (nearest < BakeRadius * 1.5f)
                    Line(sb, "    Tight. Adding walls or props near the counter needs a re-bake and a re-check.");
            }
        }

        Line(sb, "");

        // ---------- where people are sent ----------

        Line(sb, "--- WAITING SPOTS ---");
        Line(sb, "(a spot on the staff side is one that sends customers behind your counter)");
        Line(sb, "");

        WaitingSpot[] spots = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int seatCustomer = 0, seatStaff = 0, loiterCustomer = 0, loiterStaff = 0;
        int offMesh = 0, unreachable = 0;

        List<string> spotLines = new List<string>();

        foreach (WaitingSpot s in spots)
        {
            if (s == null) continue;

            Vector3 p = s.StandPoint.position;
            bool staffSide = haveCounter && (customerSideIsPlusZ ? p.z < counterFront : p.z > counterFront);

            bool onMesh = NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas);
            if (!onMesh) offMesh++;

            string route = "no spawn point";
            if (spawn != null)
            {
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(spawn.transform.position, onMesh ? hit.position : p,
                                          NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    route = string.Format("walk {0,5:F1} m", PathLength(path));
                }
                else
                {
                    route = "*** UNREACHABLE ***";
                    unreachable++;
                }
            }

            if (s.Kind == WaitingSpot.SpotKind.Seat) { if (staffSide) seatStaff++; else seatCustomer++; }
            else { if (staffSide) loiterStaff++; else loiterCustomer++; }

            spotLines.Add(string.Format("  [{0,-6}] {1,-28} ({2,7:F2}, {3,7:F2})  {4,-8}  {5}  {6}",
                s.Kind, Trim(Path(s.transform), 28), p.x, p.z,
                staffSide ? "STAFF" : "customer",
                onMesh ? "mesh ok " : "OFF-MESH",
                route));
        }

        spotLines.Sort();
        foreach (string l in spotLines) Line(sb, l);

        Line(sb, "");
        Line(sb, string.Format("Seats:  {0} on the customer side, {1} on the STAFF side", seatCustomer, seatStaff));
        Line(sb, string.Format("Loiter: {0} on the customer side, {1} on the STAFF side", loiterCustomer, loiterStaff));
        Line(sb, string.Format("Off the NavMesh: {0}    Unreachable from spawn: {1}", offMesh, unreachable));
        Line(sb, "");

        // ---------- the furniture ----------

        Line(sb, "--- FURNITURE ---");

        GameObject furnitureRoot = Find("FURNITURE");
        if (furnitureRoot == null) Line(sb, "No object named FURNITURE found.");
        else
        {
            Bounds all = default;
            bool first = true;
            int pieces = 0, staffPieces = 0;
            List<string> fLines = new List<string>();

            foreach (Transform child in furnitureRoot.transform)
            {
                if (!TryBounds(child.gameObject, out Bounds b)) continue;

                pieces++;
                if (first) { all = b; first = false; } else all.Encapsulate(b);

                bool staffSide = haveCounter &&
                    (customerSideIsPlusZ ? b.center.z < counterFront : b.center.z > counterFront);
                if (staffSide) staffPieces++;

                fLines.Add(string.Format("  {0,-26} ({1,7:F2}, {2,7:F2})  {3}",
                    Trim(child.name, 26), b.center.x, b.center.z, staffSide ? "STAFF" : "customer"));
            }

            fLines.Sort();
            foreach (string l in fLines) Line(sb, l);

            if (!first)
            {
                Line(sb, "");
                Line(sb, string.Format("{0} pieces.  Bounds x {1,7:F2} .. {2,7:F2}   z {3,7:F2} .. {4,7:F2}",
                    pieces, all.min.x, all.max.x, all.min.z, all.max.z));
                Line(sb, string.Format("{0} of {1} sit on the STAFF side of the counter.", staffPieces, pieces));
            }
        }

        Line(sb, "");

        // ---------- how much of the floor is actually used ----------

        Line(sb, "--- USED SPACE vs FLOOR ---");

        Bounds used = default;
        bool haveUsed = false;
        foreach (string n in new[] { "Counter", "Workbench", "EspressoMachine", "KitchenCounter",
                                     "IntakeShelf", "SpawnPoint", "FURNITURE", "WaitingArea", "CounterQueue" })
        {
            GameObject go = Find(n);
            if (go == null) continue;
            if (!TryBounds(go, out Bounds b))
                b = new Bounds(go.transform.position, Vector3.one);
            if (!haveUsed) { used = b; haveUsed = true; } else used.Encapsulate(b);
        }

        foreach (WaitingSpot s in spots)
            if (s != null) { if (!haveUsed) { used = new Bounds(s.StandPoint.position, Vector3.one); haveUsed = true; } else used.Encapsulate(s.StandPoint.position); }

        if (haveUsed)
        {
            Line(sb, string.Format("Everything that matters fits inside  x {0,7:F2} .. {1,7:F2}   z {2,7:F2} .. {3,7:F2}   ({4:F1} x {5:F1} m)",
                used.min.x, used.max.x, used.min.z, used.max.z, used.size.x, used.size.z));

            if (haveFloor)
            {
                float usedArea = used.size.x * used.size.z;
                float floorArea = floorB.size.x * floorB.size.z;
                Line(sb, string.Format("Floor is {0:F0} m2. Used span is {1:F0} m2. Dead floor: {2:F0}%.",
                    floorArea, usedArea, 100f * (1f - usedArea / floorArea)));
            }
        }

        Line(sb, "");
        Line(sb, "==================================================");

        string report = sb.ToString();

        File.WriteAllText(ReportPath, report);
        AssetDatabase.Refresh();

        Debug.Log(report);
        Debug.Log("[Room measure] Written to " + ReportPath + " — nothing in the scene was changed.");
    }

    // ---------- helpers ----------

    private static void Line(StringBuilder sb, string s) => sb.Append(s).Append('\n');

    private static string YesNo(bool b) => b ? "walled" : "*** OPEN ***";

    private static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "~";

    private static float PathLength(NavMeshPath path)
    {
        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return total;
    }

    // Renderer bounds are already in world space, which is the entire point of
    // asking Unity instead of reading the file.
    private static bool TryBounds(GameObject go, out Bounds b)
    {
        b = default;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;

        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    private static string Path(Transform t)
    {
        string s = t.name;
        while (t.parent != null && t.parent.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject go in AllObjects())
            if (go.name == name) return go;
        return null;
    }

    private static GameObject[] AllObjects() =>
        Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
}
