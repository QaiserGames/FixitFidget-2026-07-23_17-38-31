using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Editor-only. Writes a top-down map of the café floor for analysing Café life
/// recordings: the walkable NavMesh (as triangles), every seat and waiting spot,
/// the counter slots, the door and exit, and the footprint of everything solid
/// on the floor. Output: Logs/CafeLife/map.json (Logs is git-ignored).
///
/// Nothing in the scene changes. Run it in edit mode or play mode.
/// </summary>
public static class CafeMapExport
{
    // The café interior and the pavement in front of it.
    private static readonly Rect Area = new Rect(-10f, -4f, 20f, 20f);

    [MenuItem("Fixit Fidget/Café life/Export café map (for analysis)", false, 200)]
    public static void Export()
    {
        var sb = new StringBuilder(1 << 20);
        sb.Append("{\n");

        // --- the walkable floor
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        sb.Append("\"navmesh\":{\"vertices\":[");
        for (int i = 0; i < tri.vertices.Length; i++)
        {
            if (i > 0) sb.Append(',');
            Vec(sb, tri.vertices[i]);
        }
        sb.Append("],\"indices\":[");
        for (int i = 0; i < tri.indices.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(tri.indices[i]);
        }
        sb.Append("],\"areas\":[");
        for (int i = 0; i < tri.areas.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(tri.areas[i]);
        }
        sb.Append("]},\n");

        // --- bake settings (the agent the NavMesh was built for)
        sb.Append("\"agentTypes\":[");
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
            if (i > 0) sb.Append(',');
            sb.Append("{\"id\":").Append(s.agentTypeID)
              .Append(",\"name\":\"").Append(Escape(NavMesh.GetSettingsNameFromID(s.agentTypeID))).Append('"')
              .Append(",\"radius\":").Append(F(s.agentRadius))
              .Append(",\"height\":").Append(F(s.agentHeight))
              .Append(",\"climb\":").Append(F(s.agentClimb))
              .Append(",\"slope\":").Append(F(s.agentSlope)).Append('}');
        }
        sb.Append("],\n");

        // --- seats and waiting spots
        sb.Append("\"spots\":[");
        bool first = true;
        foreach (WaitingSpot spot in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Include))
        {
            if (!first) sb.Append(',');
            first = false;
            Transform stand = spot.StandPoint;
            sb.Append("\n{\"name\":\"").Append(Escape(PathOf(spot.transform))).Append('"')
              .Append(",\"type\":\"").Append(spot.GetType().Name).Append('"')
              .Append(",\"kind\":\"").Append(spot.Kind).Append('"')
              .Append(",\"active\":").Append(spot.isActiveAndEnabled ? "true" : "false")
              .Append(",\"stand\":"); Vec(sb, stand.position);
            sb.Append(",\"yaw\":").Append(F(stand.eulerAngles.y));
            if (spot is TableSeat seat)
            {
                sb.Append(",\"seat\":"); Vec(sb, seat.SeatPose.position);
                sb.Append(",\"cup\":"); Vec(sb, seat.CupSpot.position);
                sb.Append(",\"snap\":").Append(seat.SnapToSeat ? "true" : "false");
            }
            sb.Append('}');
        }
        sb.Append("],\n");

        // --- the counter slots
        sb.Append("\"slots\":[");
        first = true;
        foreach (CounterQueue queue in Object.FindObjectsByType<CounterQueue>(FindObjectsInactive.Include))
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform slot = queue.SlotPoint(i);
                if (slot == null) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"pos\":"); Vec(sb, slot.position);
                sb.Append(",\"yaw\":").Append(F(slot.eulerAngles.y)).Append('}');
            }
        sb.Append("],\n");

        // --- door and exit
        sb.Append("\"points\":{");
        first = true;
        foreach (string name in new[] { "ExitPoint", "Exit", "SpawnPoint", "Door", "CafeDoor", "DoorPoint" })
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(name).Append("\":"); Vec(sb, go.transform.position);
        }
        CafeArrivals arrivals = Object.FindAnyObjectByType<CafeArrivals>(FindObjectsInactive.Include);
        if (arrivals != null)
        {
            var door = new SerializedObject(arrivals).FindProperty("door");
            if (door != null && door.objectReferenceValue is Transform doorTransform)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("\"arrivalsDoor\":"); Vec(sb, doorTransform.position);
            }
        }
        foreach (Component spawner in new Component[]
                 {
                     Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include),
                     Object.FindAnyObjectByType<PatronSpawner>(FindObjectsInactive.Include)
                 })
        {
            if (spawner == null) continue;
            var so = new SerializedObject(spawner);
            foreach (string field in new[] { "exitPoint", "spawnPoint" })
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null || !(p.objectReferenceValue is Transform t)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(spawner.GetType().Name).Append('.').Append(field).Append("\":"); Vec(sb, t.position);
            }
        }
        sb.Append("},\n");

        // --- solid things on the floor (footprints)
        sb.Append("\"solids\":[");
        first = true;
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            Bounds b = c.bounds;
            if (b.max.y < 0.05f || b.min.y > 1.6f) continue;           // not at body height
            if (b.size.x > 25f || b.size.z > 25f) continue;              // the floor, the ground
            if (!Area.Overlaps(new Rect(b.min.x, b.min.z, b.size.x, b.size.z))) continue;
            if (c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue; // people
            if (!first) sb.Append(',');
            first = false;
            sb.Append("\n{\"name\":\"").Append(Escape(PathOf(c.transform))).Append('"')
              .Append(",\"type\":\"").Append(c.GetType().Name).Append('"');
            if (c is BoxCollider box)
            {
                // Oriented footprint: four corners of the box's bottom face.
                sb.Append(",\"corners\":[");
                Vector3 h = box.size * 0.5f;
                Vector3[] local =
                {
                    box.center + new Vector3(-h.x, -h.y, -h.z), box.center + new Vector3(h.x, -h.y, -h.z),
                    box.center + new Vector3(h.x, -h.y, h.z), box.center + new Vector3(-h.x, -h.y, h.z)
                };
                for (int i = 0; i < 4; i++)
                {
                    if (i > 0) sb.Append(',');
                    Vec(sb, box.transform.TransformPoint(local[i]));
                }
                sb.Append(']');
            }
            sb.Append(",\"min\":"); Vec(sb, b.min);
            sb.Append(",\"max\":"); Vec(sb, b.max);
            sb.Append('}');
        }
        sb.Append("],\n");

        // --- obstacles the navigation already knows about
        sb.Append("\"obstacles\":[");
        first = true;
        foreach (NavMeshObstacle o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include))
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"name\":\"").Append(Escape(PathOf(o.transform))).Append('"')
              .Append(",\"carve\":").Append(o.carving ? "true" : "false")
              .Append(",\"enabled\":").Append(o.isActiveAndEnabled ? "true" : "false")
              .Append(",\"pos\":"); Vec(sb, o.transform.position);
            sb.Append('}');
        }
        sb.Append("],\n");

        // --- routes from the door: can every spot be reached, how long is the
        // walk, and how tight is the tightest point on the way (twice the
        // distance from the path to the nearest NavMesh edge)?
        Vector3 doorAt = new Vector3(0f, 0.05f, -1.2f);
        if (NavMesh.SamplePosition(doorAt, out NavMeshHit doorHit, 1f, NavMesh.AllAreas)) doorAt = doorHit.position;
        sb.Append("\"routes\":[");
        first = true;
        var targets = new List<(string name, Vector3 at)>();
        foreach (WaitingSpot spot in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
            if (spot.isActiveAndEnabled) targets.Add((spot.transform.parent != null ? spot.transform.parent.name + "/" + spot.name : spot.name, spot.StandPoint.position));
        foreach (CounterQueue queue in Object.FindObjectsByType<CounterQueue>(FindObjectsInactive.Exclude))
            for (int i = 0; i < queue.SlotCount; i++)
                if (queue.SlotPoint(i) != null) targets.Add(("slot " + i, queue.SlotPoint(i).position));
        foreach (var target in targets)
        {
            if (!first) sb.Append(',');
            first = false;
            Route(sb, "door -> " + target.name, doorAt, target.at);
        }
        // From the middle counter slot to every seat: the walk after "accepted".
        CounterQueue firstQueue = Object.FindAnyObjectByType<CounterQueue>();
        if (firstQueue != null && firstQueue.SlotCount > 1 && firstQueue.SlotPoint(1) != null)
            foreach (var target in targets)
                if (!target.name.StartsWith("slot"))
                {
                    sb.Append(',');
                    Route(sb, "slot 1 -> " + target.name, firstQueue.SlotPoint(1).position, target.at);
                }
        sb.Append("],\n");

        // --- how far each stand point / slot is from the nearest NavMesh edge
        sb.Append("\"clearance\":[");
        first = true;
        foreach (var target in targets)
        {
            if (!first) sb.Append(',');
            first = false;
            float edge = NavMesh.FindClosestEdge(target.at, out NavMeshHit e, NavMesh.AllAreas) ? e.distance : -1f;
            bool on = NavMesh.SamplePosition(target.at, out NavMeshHit onHit, 0.1f, NavMesh.AllAreas);
            sb.Append("{\"name\":\"").Append(Escape(target.name)).Append("\",\"onMesh\":").Append(on ? "true" : "false")
              .Append(",\"edge\":").Append(F(edge)).Append('}');
        }
        sb.Append("],\n");

        // --- walkable width through a few fixed places, measured along a line
        sb.Append("\"widths\":[");
        first = true;
        foreach (var probe in new (string name, Vector3 at, Vector3 dir)[]
                 {
                     ("doorway (z -0.5)", new Vector3(0f, 0.05f, -0.5f), Vector3.right),
                     ("doorway (z -1.0)", new Vector3(0f, 0.05f, -1.0f), Vector3.right),
                     ("centre aisle between the top tables (z 9)", new Vector3(0f, 0.05f, 9f), Vector3.right),
                     ("centre aisle between the bottom tables (z 3.5)", new Vector3(0f, 0.05f, 3.5f), Vector3.right),
                     ("gap between the west tables (x -3, z 6.2)", new Vector3(-3f, 0.05f, 6.2f), Vector3.forward),
                     ("gap between the east tables (x 3, z 6.2)", new Vector3(3f, 0.05f, 6.2f), Vector3.forward),
                     ("west aisle beside the lounge (z 6.2)", new Vector3(-5.5f, 0.05f, 6.2f), Vector3.right),
                     ("east aisle (z 6.2)", new Vector3(5.5f, 0.05f, 6.2f), Vector3.right),
                     ("in front of the counter (z 11.6)", new Vector3(0f, 0.05f, 11.6f), Vector3.forward),
                     ("counter frontage width (z 11.6)", new Vector3(0f, 0.05f, 11.6f), Vector3.right),
                     ("below the bottom tables (z 1.2)", new Vector3(0f, 0.05f, 1.2f), Vector3.forward),
                     ("table 1 inner corner (-1.7, 7.7) across", new Vector3(-1.4f, 0.05f, 7.4f), new Vector3(1f, 0f, -1f).normalized),
                     ("table 2 inner corner (1.7, 7.7) across", new Vector3(1.4f, 0.05f, 7.4f), new Vector3(-1f, 0f, -1f).normalized),
                     ("table 3 inner corner (-1.7, 4.8) across", new Vector3(-1.4f, 0.05f, 5.1f), new Vector3(1f, 0f, 1f).normalized),
                     ("table 4 inner corner (1.7, 4.8) across", new Vector3(1.4f, 0.05f, 5.1f), new Vector3(-1f, 0f, 1f).normalized),
                 })
        {
            if (!first) sb.Append(',');
            first = false;
            float width = Width(probe.at, probe.dir, out Vector3 a, out Vector3 b2);
            sb.Append("{\"name\":\"").Append(Escape(probe.name)).Append("\",\"width\":").Append(F(width))
              .Append(",\"from\":"); Vec(sb, a); sb.Append(",\"to\":"); Vec(sb, b2); sb.Append('}');
        }
        sb.Append("]\n}\n");

        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CafeLife");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "map.json");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Café life] Map written: {path} ({tri.vertices.Length} NavMesh vertices, {tri.indices.Length / 3} triangles).");
    }

    private static readonly NavMeshPath scratchPath = new NavMeshPath();

    private static void Route(StringBuilder sb, string name, Vector3 from, Vector3 to)
    {
        Vector3 a = from, b = to;
        if (NavMesh.SamplePosition(from, out NavMeshHit ha, 0.5f, NavMesh.AllAreas)) a = ha.position;
        if (NavMesh.SamplePosition(to, out NavMeshHit hb, 0.5f, NavMesh.AllAreas)) b = hb.position;
        bool found = NavMesh.CalculatePath(a, b, NavMesh.AllAreas, scratchPath);
        float length = 0f, tightest = -1f;
        Vector3 tightAt = a;
        if (found && scratchPath.corners.Length > 1)
        {
            for (int i = 1; i < scratchPath.corners.Length; i++)
            {
                Vector3 p0 = scratchPath.corners[i - 1], p1 = scratchPath.corners[i];
                float seg = Vector3.Distance(p0, p1);
                length += seg;
                int steps = Mathf.Max(1, Mathf.CeilToInt(seg / 0.1f));
                for (int k = 0; k <= steps; k++)
                {
                    Vector3 p = Vector3.Lerp(p0, p1, (float)k / steps);
                    if (!NavMesh.FindClosestEdge(p, out NavMeshHit e, NavMesh.AllAreas)) continue;
                    if (tightest < 0f || e.distance < tightest) { tightest = e.distance; tightAt = p; }
                }
            }
        }
        sb.Append("\n{\"name\":\"").Append(Escape(name)).Append("\",\"status\":\"")
          .Append(found ? scratchPath.status.ToString() : "NoPath").Append("\",\"length\":").Append(F(length))
          .Append(",\"straight\":").Append(F(Vector3.Distance(a, b)))
          .Append(",\"tightestWidth\":").Append(F(tightest < 0f ? -1f : tightest * 2f)).Append(",\"tightAt\":");
        Vec(sb, tightAt);
        sb.Append('}');
    }

    // Walkable extent through `at` along +/-dir, in 2 cm steps.
    private static float Width(Vector3 at, Vector3 dir, out Vector3 from, out Vector3 to)
    {
        from = to = at;
        if (!NavMesh.SamplePosition(at, out NavMeshHit centre, 0.3f, NavMesh.AllAreas)) return 0f;
        Vector3 c = centre.position;
        from = to = c;
        dir.y = 0f;
        dir.Normalize();
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Vector3 last = c;
            for (float d = 0.02f; d <= 6f; d += 0.02f)
            {
                Vector3 p = c + dir * (d * sign);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.06f, NavMesh.AllAreas)) break;
                Vector3 flat = hit.position; flat.y = p.y;
                if (Vector3.Distance(flat, p) > 0.03f) break;
                last = p;
            }
            if (sign < 0) from = last; else to = last;
        }
        return Vector3.Distance(from, to);
    }

    private static void Vec(StringBuilder sb, Vector3 v) =>
        sb.Append('[').Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(']');

    private static string F(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
