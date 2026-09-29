#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// PLAYTEST 2, STEP 3 (the break-ins spec): a read-only look at Grace's house
//
//   Fixit Fidget > Night > Break-ins - Survey Grace's house (read-only)
//
// What the spec needs to know before rooms are drawn: the saffron house's shell (its box, its parts
// and what they're made of), the front door and its dark hall, the windows floor by floor (the night's
// rooms), whether anything is inside the shell today, and what is beside and behind it (a side
// street, a yard, the next house). Photos from the street, the corner, behind, above and inside go to
// <project>/Logs/Night/break-ins-survey-<time>/ with a report. Nothing in the scene changes: photos
// are rendered by a temporary camera (CafeSecondPassSteps.Capture), in the Edit Mode (day) lighting.
// ---------------------------------------------------------------------------
public static class BreakInsSurvey
{
    const string HouseName = "1 - Saffron bay-window house";
    const string RoomsPath = "Assets/Playtests/AcesCafeLayout/Night walk - rooms.asset";

    [MenuItem("Fixit Fidget/Night/Break-ins - Survey Grace's house (read-only)")]
    static void Run()
    {
        try { Survey(); }
        catch (Exception e) { Debug.LogError("[Break-ins survey] FAILED: " + e.Message + "\n" + e); }
    }

    [MenuItem("Fixit Fidget/Night/Break-ins - Survey Grace's house (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Survey()
    {
        Transform house = CafeSecondPassSteps.InScene<Transform>().FirstOrDefault(t => t.name == HouseName);
        if (house == null) { Debug.LogError("[Break-ins survey] No '" + HouseName + "' in the open scene."); return; }

        var log = new StringBuilder();
        log.AppendLine("Break-ins survey: Grace's house (" + HouseName + ")");
        log.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        log.AppendLine();

        // ---- the shell ----
        Renderer[] parts = house.GetComponentsInChildren<Renderer>(true);
        Bounds box = Box(parts.Where(r => r.enabled && r.gameObject.activeInHierarchy));
        log.AppendLine($"Box (what it draws): centre {box.center:F2}, size {box.size:F2}, min {box.min:F2}, max {box.max:F2}");
        log.AppendLine($"Transform: position {house.position:F2}, rotation {house.eulerAngles:F1}, scale {house.lossyScale:F2}; {house.childCount} children");
        log.AppendLine();
        log.AppendLine("Parts (path | active | mesh | tris | bounds | materials):");
        foreach (Renderer r in parts)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            string mats = string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name));
            log.AppendLine($"  {Path(r.transform, house)} | {(r.enabled && r.gameObject.activeInHierarchy ? "on" : "off")} | " +
                           $"{(mesh != null ? mesh.name : r.GetType().Name)} | {(mesh != null ? (mesh.triangles.Length / 3).ToString() : "-")} | " +
                           $"c {r.bounds.center:F2} s {r.bounds.size:F2} | {mats}");
        }
        foreach (Collider c in house.GetComponentsInChildren<Collider>(true))
            log.AppendLine($"  collider {Path(c.transform, house)} {c.GetType().Name} enabled={c.enabled} bounds c {c.bounds.center:F2} s {c.bounds.size:F2}");
        log.AppendLine();

        // ---- the front door and its dark hall ----
        StreetDoor door = CafeSecondPassSteps.InScene<StreetDoor>()
            .OrderBy(d => (d.DoorwayPoint - box.center).sqrMagnitude).FirstOrDefault();
        HomeDoor home = CafeSecondPassSteps.InScene<HomeDoor>().FirstOrDefault();
        if (door != null)
        {
            log.AppendLine($"Front door: '{Path(door.transform, null)}'; doorway {door.DoorwayPoint:F2}, hall {door.HallPoint:F2}, " +
                           $"outward {door.Outward:F2}, hinge {(door.hinge != null ? door.hinge.position.ToString("F2") : "-")}, " +
                           $"{door.waitSpots.Length} wait spots, {door.cutParts.Length} cut parts");
            foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true))
                log.AppendLine($"  door part {Path(r.transform, door.transform)}: c {r.bounds.center:F2} s {r.bounds.size:F2} ({string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))})");
        }
        else log.AppendLine("Front door: none found.");
        if (home != null) log.AppendLine($"Home door marker '{Path(home.transform, null)}' at {home.transform.position:F2}");
        log.AppendLine();

        // ---- the windows, floor by floor (the night's rooms) ----
        var rooms = AssetDatabase.LoadAssetAtPath<NightRooms>(RoomsPath);
        if (rooms != null)
        {
            var mine = rooms.rooms.Where(r => r.house == HouseName).OrderBy(r => r.index).ToList();
            log.AppendLine($"Night rooms (windows) of this house: {mine.Count}");
            foreach (NightRooms.Room room in mine)
            {
                Bounds panes = room.panes != null ? TransformBounds(house, room.panes.bounds) : new Bounds(room.centre, Vector3.zero);
                log.AppendLine($"  room {room.index}: floor {room.floor}, window centre {room.centre:F2}, panes c {panes.center:F2} s {panes.size:F2}, " +
                               $"curtains {(room.curtains != null ? "yes" : "no")}");
            }
        }
        else log.AppendLine("Night rooms asset not found at " + RoomsPath);
        log.AppendLine();

        // ---- inside the shell today ----
        float floorY = door != null ? door.DoorwayPoint.y : box.min.y;
        Bounds inside = new Bounds(box.center, box.size - new Vector3(.7f, .2f, .7f));
        log.AppendLine($"Inside the shell (box shrunk 0.35 m at the walls): {inside.min:F2} to {inside.max:F2}; the doorway's floor is at y {floorY:F2}");
        var within = new List<string>();
        foreach (Renderer r in CafeSecondPassSteps.InScene<Renderer>())
        {
            if (r.transform.IsChildOf(house) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.bounds.Intersects(inside)) within.Add($"  {Path(r.transform, null)}: c {r.bounds.center:F2} s {r.bounds.size:F2}");
        }
        log.AppendLine($"Other things drawn inside the shell: {within.Count}");
        foreach (string w in within.Take(60)) log.AppendLine(w);

        // The shell's own triangles inside that box, by height: a hollow shell has (almost) none.
        var bands = new SortedDictionary<int, int>();
        int total = 0;
        foreach (Renderer r in parts)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            Vector3[] v = mf.sharedMesh.vertices;
            int[] t = mf.sharedMesh.triangles;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 c = m.MultiplyPoint3x4((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f);
                if (!inside.Contains(c)) continue;
                total++;
                int band = Mathf.FloorToInt(c.y);
                bands[band] = bands.TryGetValue(band, out int n) ? n + 1 : 1;
            }
        }
        log.AppendLine($"The shell's own triangles inside that box: {total} (by height, 1 m bands: " +
                       string.Join(", ", bands.Select(b => $"{b.Key} m: {b.Value}")) + "). Unreadable meshes are skipped.");
        log.AppendLine();

        // ---- beside and behind ----
        Bounds south = new Bounds(new Vector3(box.center.x, 2f, box.min.z - 4f), new Vector3(box.size.x + 4f, 4f, 8f));
        Bounds west = new Bounds(new Vector3(box.min.x - 4f, 2f, box.center.z), new Vector3(8f, 4f, box.size.z + 4f));
        foreach (var (name, area) in new[] { ("south of it (the corner side)", south), ("behind it (west)", west) })
        {
            var found = CafeSecondPassSteps.InScene<Renderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(house) && r.bounds.Intersects(area))
                .Select(r => $"  {Path(r.transform, null)}: c {r.bounds.center:F2} s {r.bounds.size:F2}").Take(40).ToList();
            log.AppendLine($"Drawn {name}, {area.min:F1} to {area.max:F1}: {found.Count}{(found.Count == 40 ? "+" : "")}");
            foreach (string f in found) log.AppendLine(f);
        }
        Transform next = CafeSecondPassSteps.InScene<Transform>().FirstOrDefault(t => t.name == "2 - Dusty rose bay-window house");
        if (next != null)
        {
            Bounds nb = Box(next.GetComponentsInChildren<Renderer>(false));
            log.AppendLine($"Next door (2, dusty rose): box min {nb.min:F2}, max {nb.max:F2}; the gap to Grace's house along z: {nb.min.z - box.max.z:F2} m");
        }

        // ---- photos ----
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "Night", "break-ins-survey-" + stamp));
        Directory.CreateDirectory(folder);
        Vector3 mid = new Vector3(box.center.x, floorY + 1.4f, box.center.z);
        Vector3 front = new Vector3(box.max.x, floorY + 1.5f, box.center.z);
        var views = new List<(string name, Vector3 at, Vector3 look, float fov)>
        {
            ("b01-front", front + new Vector3(9f, .5f, 0f), front + Vector3.up * 1.5f, 60f),
            ("b02-the-corner", new Vector3(box.max.x + 7f, floorY + 3f, box.min.z - 8f), new Vector3(box.center.x, floorY + 3f, box.min.z), 60f),
            ("b03-the-south-side", new Vector3(box.center.x, floorY + 1.8f, box.min.z - 9f), new Vector3(box.center.x, floorY + 3f, box.min.z), 62f),
            ("b04-behind-from-above", new Vector3(box.min.x - 8f, box.max.y + 6f, box.center.z - 6f), new Vector3(box.min.x, floorY + 2f, box.center.z), 60f),
            ("b05-from-above", new Vector3(box.center.x + 2f, box.max.y + 16f, box.center.z - 1f), new Vector3(box.center.x, floorY, box.center.z), 55f),
            ("b06-inside-from-the-hall", (door != null ? door.HallPoint : front) + Vector3.up * 1.5f, mid + new Vector3(-2f, 0f, 1.5f), 75f),
            ("b07-inside-looking-at-the-front", mid + new Vector3(-1.8f, 0f, 0f), front, 80f),
            ("b08-inside-looking-back", mid + new Vector3(1.8f, 0f, 0f), new Vector3(box.min.x, mid.y, box.center.z), 80f),
            ("b09-inside-upstairs", mid + Vector3.up * 3f, new Vector3(box.max.x, mid.y + 3f, box.center.z), 80f),
        };
        foreach (var view in views)
            CafeSecondPassSteps.Capture(System.IO.Path.Combine(folder, view.name + ".png"), view.at, view.look, view.fov, false);

        File.WriteAllText(System.IO.Path.Combine(folder, "report.txt"), log.ToString());
        Debug.Log("[Break-ins survey] " + folder + "\n" + log);
    }

    static Bounds Box(IEnumerable<Renderer> renderers)
    {
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // A mesh's local bounds, carried into the world by a transform (the eight corners).
    static Bounds TransformBounds(Transform t, Bounds local)
    {
        Vector3 c = local.center, e = local.extents;
        Bounds b = new Bounds(t.TransformPoint(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(t.TransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
        return b;
    }

    static string Path(Transform t, Transform root)
    {
        var names = new List<string>();
        for (Transform p = t; p != null && p != root; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
#endif
