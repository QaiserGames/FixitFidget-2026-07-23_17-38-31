#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// The entrance doors' "floating" brass pulls (23 Sept second-pass item).
//
// Cause, measured on 27 Sept (Fixit Fidget > Neighborhood refresh > Second
// pass > Photograph the entrance doors): in the authored EntranceRefresh.fbx
// each open door leaf was assembled at two different angles. The stiles, the
// hinges and the pulls hang together and meet the door frame, but the glass,
// the rails, the bottom panel, the glazing beads and the kick plate are turned
// 24 degrees the other way about the leaf's centre. From above each door is an
// X, and the pulls sit up to 0.2 m off the glass they should be fixed to.
//
// Line up the entrance door leaves: turns that infill (only the pieces within
// 6 cm of a door's glass, within the leaf's width) about the leaf's centre
// until it lies on the line through the two stiles. Works on copies of the
// FBX meshes saved beside it ("EntranceRefresh aligned"); the FBX itself is not
// touched. Idempotent: it always starts from the FBX's own meshes.
//
// Put the original entrance doors back: reverts the four mesh overrides.
// Visual only: the entrance has no colliders and is not part of the NavMesh.
// ---------------------------------------------------------------------------
public static class EntranceDoorAlign
{
    const string EntranceName = "Entrance with rounded brass pulls";
    const string MenuRoot = "Fixit Fidget/Neighborhood refresh/Second pass/";
    const string Tag = "[Entrance doors] ";
    static readonly string[] MovableParts = { "InteriorOak", "InteriorCream", "InteriorBrass", "InteriorGlass" };

    const float PlaneBand = 0.06f;     // m either side of a door's glass: that leaf's own infill
    const float HalfLeaf = 0.53f;      // m either side of the glass centre, along the leaf
    const float MinTurn = 5f, MaxTurn = 45f;

    sealed class Island
    {
        public MeshFilter Filter;
        public int[] Vertices;
        public Vector3[] World;
        public Vector3 Centre;
        public float Height;
    }

    sealed class Door
    {
        public Vector3 GlassCentre, Across, Along, Pivot;
        public float TurnDegrees;
        public Quaternion Turn;
        public readonly List<Island> Infill = new();
    }

    [MenuItem(MenuRoot + "Line up the entrance door leaves (fixes the floating pulls)")]
    static void Align()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play Mode first."); return; }
        var log = new StringBuilder(Tag + "Line up the entrance door leaves\n");
        try
        {
            if (!TryFindParts(out Transform entrance, out Dictionary<string, MeshFilter> filters, log)) { Debug.LogError(log.ToString()); return; }

            // Always start from the FBX's own meshes.
            var sources = new Dictionary<string, Mesh>();
            foreach (var pair in filters)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(pair.Value);
                Mesh mesh = source != null ? source.sharedMesh : pair.Value.sharedMesh;
                if (mesh == null) { Debug.LogError(log + $"{pair.Key}: no mesh."); return; }
                if (mesh.vertices.Length != mesh.vertexCount)
                {
                    Debug.LogError(log + $"{pair.Key}: Unity would not hand over the mesh's vertices. Nothing changed.");
                    return;
                }
                sources[pair.Key] = mesh;
            }

            var islands = new Dictionary<string, List<Island>>();
            foreach (var pair in filters) islands[pair.Key] = Islands(pair.Value, sources[pair.Key]);

            List<Door> doors = FindDoors(islands, log);
            if (doors == null) { Debug.LogError(log.ToString()); return; }

            // Each infill piece belongs to the nearest door whose glass band it lies in.
            foreach (string part in MovableParts)
                foreach (Island island in islands[part])
                {
                    Door owner = null;
                    float best = float.MaxValue;
                    foreach (Door door in doors)
                    {
                        if (!InBand(island, door)) continue;
                        float d = Horizontal(island.Centre - door.GlassCentre).magnitude;
                        if (d < best) { best = d; owner = door; }
                    }
                    owner?.Infill.Add(island);
                }

            // Turn the infill, in world space, then back into each mesh's own space.
            var moved = new Dictionary<string, (Vector3[] vertices, Vector3[] normals, Vector4[] tangents)>();
            foreach (string part in MovableParts)
            {
                Mesh mesh = sources[part];
                Transform t = filters[part].transform;
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector4[] tangents = mesh.tangents;
                foreach (Door door in doors)
                    foreach (Island island in door.Infill.Where(i => i.Filter == filters[part]))
                        foreach (int index in island.Vertices)
                        {
                            Vector3 world = t.TransformPoint(vertices[index]);
                            vertices[index] = t.InverseTransformPoint(door.Pivot + door.Turn * (world - door.Pivot));
                            if (normals.Length == vertices.Length)
                                normals[index] = t.InverseTransformDirection(door.Turn * t.TransformDirection(normals[index])).normalized;
                            if (tangents.Length == vertices.Length)
                            {
                                Vector3 turned = t.InverseTransformDirection(door.Turn * t.TransformDirection(tangents[index])).normalized;
                                tangents[index] = new Vector4(turned.x, turned.y, turned.z, tangents[index].w);
                            }
                        }
                moved[part] = (vertices, normals, tangents);
            }

            foreach (Door door in doors)
            {
                string pieces = string.Join(", ", door.Infill.GroupBy(i => i.Filter.name.Replace("Entrance - Interior", ""))
                    .Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}"));
                log.AppendLine($"Door at {door.GlassCentre:F2}: infill turned {door.TurnDegrees:+0.0;-0.0} deg about {Horizontal(door.Pivot):F3} ({pieces}).");
            }
            if (doors.Any(d => d.Infill.Count == 0)) { Debug.LogError(log + "A door had no infill to turn. Nothing changed."); return; }

            // Check before writing anything: every turned piece now lies on its stiles' line.
            foreach (Door door in doors)
            {
                Vector3 across = door.Turn * door.Across;
                float worst = 0f;
                foreach (Island island in door.Infill)
                {
                    Transform t = island.Filter.transform;
                    Vector3[] vertices = moved[MovableParts.First(p => filters[p] == island.Filter)].vertices;
                    foreach (int index in island.Vertices)
                        worst = Mathf.Max(worst, Mathf.Abs(Vector3.Dot(t.TransformPoint(vertices[index]) - door.Pivot, across)));
                }
                log.AppendLine($"  after: the turned pieces lie within {worst * 1000f:0} mm of the stiles' centre line (the leaf is 90 mm thick).");
                if (worst > PlaneBand + 0.005f) { Debug.LogError(log + "The check failed. Nothing changed."); return; }
            }

            // Save the copies beside the FBX and point the entrance at them.
            string fbxPath = AssetDatabase.GetAssetPath(sources[MovableParts[0]]);
            string folder = Path.Combine(Path.GetDirectoryName(fbxPath) ?? "Assets", "EntranceRefresh aligned").Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Line up the entrance door leaves");
            foreach (string part in MovableParts)
            {
                Mesh copy = Object.Instantiate(sources[part]);
                copy.name = sources[part].name + " (doors lined up)";
                copy.vertices = moved[part].vertices;
                if (moved[part].normals.Length == copy.vertexCount) copy.normals = moved[part].normals;
                if (moved[part].tangents.Length == copy.vertexCount) copy.tangents = moved[part].tangents;
                copy.RecalculateBounds();

                string path = $"{folder}/Entrance - {part} (doors lined up).asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh asset;
                if (existing != null)
                {
                    EditorUtility.CopySerialized(copy, existing);
                    existing.name = copy.name;
                    Object.DestroyImmediate(copy);
                    EditorUtility.SetDirty(existing);
                    asset = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(copy, path);
                    asset = copy;
                }

                MeshFilter filter = filters[part];
                Undo.RecordObject(filter, "Line up the entrance door leaves");
                filter.sharedMesh = asset;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
            AssetDatabase.SaveAssets();

            var scene = entrance.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            log.AppendLine($"Meshes: {folder}/. The FBX is unchanged. {(saved ? "Saved " + scene.path + "." : "Could not save the scene: save it with Ctrl+S.")}");
            log.AppendLine("Undo: Fixit Fidget > Neighborhood refresh > Second pass > Put the original entrance doors back.");
            Debug.Log(log.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError(log + "FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(MenuRoot + "Put the original entrance doors back")]
    static void PutBack()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play Mode first."); return; }
        var log = new StringBuilder(Tag + "Put the original entrance doors back\n");
        if (!TryFindParts(out Transform entrance, out Dictionary<string, MeshFilter> filters, log)) { Debug.LogError(log.ToString()); return; }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Put the original entrance doors back");
        foreach (string part in MovableParts)
        {
            MeshFilter filter = filters[part];
            if (!PrefabUtility.GetObjectOverrides(entrance.gameObject).Any(o => o.instanceObject == filter)) continue;
            PrefabUtility.RevertObjectOverride(filter, InteractionMode.UserAction);
            log.AppendLine($"{part}: back to the FBX mesh.");
        }
        var scene = entrance.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine(EditorSceneManager.SaveScene(scene) ? "Saved " + scene.path + "." : "Could not save the scene: save it with Ctrl+S.");
        Debug.Log(log.ToString());
    }

    [MenuItem(MenuRoot + "Line up the entrance door leaves (fixes the floating pulls)", true)]
    [MenuItem(MenuRoot + "Put the original entrance doors back", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ---------------------------------------------------------------------

    static bool TryFindParts(out Transform entrance, out Dictionary<string, MeshFilter> filters, StringBuilder log)
    {
        entrance = CafeSecondPassSteps.InScene<Transform>().FirstOrDefault(t => t.name == EntranceName);
        filters = new Dictionary<string, MeshFilter>();
        if (entrance == null) { log.AppendLine("No '" + EntranceName + "' in the open scene. Nothing changed."); return false; }
        foreach (string part in MovableParts)
        {
            MeshFilter filter = entrance.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Entrance - " + part);
            if (filter == null) { log.AppendLine("The entrance has no 'Entrance - " + part + "'. Nothing changed."); return false; }
            filters[part] = filter;
        }
        return true;
    }

    // Connected pieces of a mesh: vertices joined by triangles, or sharing a position.
    static List<Island> Islands(MeshFilter filter, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        var parent = Enumerable.Range(0, vertices.Length).ToArray();
        int Root(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[a] = b; }
        for (int i = 0; i + 2 < triangles.Length; i += 3) { Join(triangles[i], triangles[i + 1]); Join(triangles[i], triangles[i + 2]); }
        var byPosition = new Dictionary<Vector3, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (byPosition.TryGetValue(vertices[i], out int other)) Join(i, other);
            else byPosition[vertices[i]] = i;
        }
        Transform t = filter.transform;
        return Enumerable.Range(0, vertices.Length).GroupBy(Root).Select(g =>
        {
            int[] indices = g.ToArray();
            Vector3[] world = indices.Select(i => t.TransformPoint(vertices[i])).ToArray();
            Vector3 sum = Vector3.zero;
            foreach (Vector3 w in world) sum += w;
            return new Island
            {
                Filter = filter, Vertices = indices, World = world, Centre = sum / world.Length,
                Height = world.Max(w => w.y) - world.Min(w => w.y)
            };
        }).ToList();
    }

    // The two door panes, their glass planes, and the line through each leaf's two stiles.
    static List<Door> FindDoors(Dictionary<string, List<Island>> islands, StringBuilder log)
    {
        List<Island> panes = islands["InteriorGlass"].Where(i => i.Height > 1.2f).ToList();
        if (panes.Count != 2) { log.AppendLine($"Expected 2 door panes, found {panes.Count}. Nothing changed."); return null; }
        var doors = new List<Door>();
        foreach (Island pane in panes)
        {
            var door = new Door { GlassCentre = pane.Centre };
            // Horizontal spread of the pane: its long direction is the leaf.
            float xx = 0, xz = 0, zz = 0;
            foreach (Vector3 w in pane.World)
            {
                float dx = w.x - pane.Centre.x, dz = w.z - pane.Centre.z;
                xx += dx * dx; xz += dx * dz; zz += dz * dz;
            }
            float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
            door.Along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            door.Across = Vector3.Cross(Vector3.up, door.Along).normalized;

            List<Island> stiles = islands["InteriorOak"].Where(i =>
                i.Height > 2.3f && i.Height < 2.55f
                && Horizontal(i.Centre - pane.Centre).magnitude < 0.7f
                && i.World.Max(w => Mathf.Abs(Vector3.Dot(w - pane.Centre, door.Across))) > PlaneBand).ToList();
            if (stiles.Count != 2) { log.AppendLine($"Door at {pane.Centre:F2}: expected 2 stiles, found {stiles.Count}. Nothing changed."); return null; }

            Vector3 line = Horizontal(stiles[1].Centre - stiles[0].Centre);
            if (Vector3.Dot(line, door.Along) < 0f) line = -line;
            float turn = Vector3.SignedAngle(door.Along, line, Vector3.up);
            if (Mathf.Abs(turn) < MinTurn || Mathf.Abs(turn) > MaxTurn)
            {
                log.AppendLine($"Door at {pane.Centre:F2}: the glass and the stiles differ by {turn:0.0} deg, outside {MinTurn}-{MaxTurn}. Nothing changed.");
                return null;
            }
            door.TurnDegrees = turn;
            door.Turn = Quaternion.AngleAxis(turn, Vector3.up);
            Vector3 middle = (stiles[0].Centre + stiles[1].Centre) * 0.5f;
            door.Pivot = new Vector3(middle.x, pane.Centre.y, middle.z);
            doors.Add(door);
        }
        return doors;
    }

    static bool InBand(Island island, Door door) =>
        island.World.All(w =>
            Mathf.Abs(Vector3.Dot(w - door.GlassCentre, door.Across)) <= PlaneBand
            && Mathf.Abs(Vector3.Dot(w - door.GlassCentre, door.Along)) <= HalfLeaf);

    static Vector3 Horizontal(Vector3 v) => new(v.x, 0f, v.z);
}
#endif
