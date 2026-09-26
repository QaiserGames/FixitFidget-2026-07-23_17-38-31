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
        sb.Append("]\n}\n");

        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CafeLife");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "map.json");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Café life] Map written: {path} ({tri.vertices.Length} NavMesh vertices, {tri.indices.Length / 3} triangles).");
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
