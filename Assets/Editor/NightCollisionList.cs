using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// NIGHT WALK 1 (set-up), PART 2: THE LIST OF WHAT IS SOLID BY NIGHT
//
// Writes Assets/Playtests/AcesCafeLayout/Night walk - collision.asset: every fixed mesh
// near the streets that Ace could touch, and where it sits. NightWalk gives each one
// exact collision while the night runs (see NightCollision). The rule: the neighbourhood
// is solid exactly where it looks solid. So a mesh is on the list when it is
//
//   * visible (an enabled renderer that draws, not a shadow-only one) and in the scene by day;
//   * low enough to touch Ace: its lowest point no higher than 2.4 m;
//   * near the streets (x -56..57, z -52..55: the 9 blocks and a margin round them);
//   * without collision of its own (the café's furniture, the night's own props and anything
//     else already solid keep theirs), and not text;
//   * made of at least one real triangle (corners apart, not in a line): PhysX can't make
//     collision from a mesh without one, and such a mesh draws nothing;
//   * not part of the café room (Ace's day workplace keeps its own collision), not a person,
//     not a car on a route, not Ace, not the night's own objects.
//
// Written in Edit Mode, where every renderer still has its own mesh (Play Mode's static
// batching merges them). It refers to meshes; it never copies one.
// ---------------------------------------------------------------------------
internal static class NightCollisionList
{
    public const string AssetPath = "Assets/Playtests/AcesCafeLayout/Night walk - collision.asset";
    public static readonly Rect Area = Rect.MinMaxRect(-56f, -52f, 57f, 55f);
    public const float Below = 2.4f;

    public static NightCollision Build(Transform nightGroup, StringBuilder report)
    {
        var skip = new List<Transform>();
        if (nightGroup != null) skip.Add(nightGroup);
        foreach (var life in CityPackChecks.InScene<StreetLife>())
            foreach (var actor in life.actors)
                if (actor != null && actor.actor != null) skip.Add(actor.actor);
        foreach (var c in CityPackChecks.InScene<CharacterController>()) skip.Add(c.transform);
        foreach (var a in CityPackChecks.InScene<UnityEngine.AI.NavMeshAgent>()) skip.Add(a.transform);
        foreach (var v in CityPackChecks.InScene<PolygonNpcVisual>()) skip.Add(v.transform);

        Rect cafe = CafeDaylight.CafeInside;
        var pieces = new List<NightCollision.Piece>();
        long triangles = 0;
        int sheared = 0, ownCollision = 0, inCafe = 0, high = 0, flat = 0;
        var triangleCache = new Dictionary<Mesh, bool>();
        var noTriangle = new List<string>();
        var byGroup = new Dictionary<string, (int count, long tris)>();
        foreach (var filter in CityPackChecks.InScene<MeshFilter>())
        {
            if (!filter.gameObject.activeInHierarchy) continue;
            var r = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (r == null || !r.enabled || mesh == null || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
            Bounds b = r.bounds;
            if (b.max.x < Area.xMin || b.min.x > Area.xMax || b.max.z < Area.yMin || b.min.z > Area.yMax) continue;
            if (b.min.y > Below) { high++; continue; }
            if (b.min.x >= cafe.xMin && b.max.x <= cafe.xMax && b.min.z >= cafe.yMin && b.max.z <= cafe.yMax) { inCafe++; continue; }
            if (HasOwnCollision(filter.gameObject)) { ownCollision++; continue; }
            if (IsText(filter.gameObject) || skip.Any(s => filter.transform.IsChildOf(s))) continue;
            var body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;
            if (!CanCollide(mesh, triangleCache)) { flat++; noTriangle.Add($"'{mesh.name}' on {PathOf(filter.transform)}"); continue; }
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            if (Sheared(m)) sheared++;
            long t = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) t += mesh.GetSubMesh(s).indexCount / 3;
            triangles += t;
            pieces.Add(new NightCollision.Piece
            {
                mesh = mesh,
                position = filter.transform.position,
                rotation = filter.transform.rotation,
                scale = filter.transform.lossyScale,
                from = PathOf(filter.transform),
            });
            string group = GroupOf(filter.transform);
            byGroup[group] = byGroup.TryGetValue(group, out var g) ? (g.count + 1, g.tris + t) : (1, t);
        }

        var asset = AssetDatabase.LoadAssetAtPath<NightCollision>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<NightCollision>();
            asset.name = "Night walk - collision";
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        asset.pieces = pieces.ToArray();
        asset.triangles = triangles;
        asset.sheared = sheared;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        report.AppendLine($"Solid by night: {pieces.Count} meshes ({triangles:N0} triangles) listed in '{AssetPath}'; they get exact collision while the night runs.");
        report.AppendLine($"  Left off: {ownCollision} already solid on their own, {inCafe} inside the café room (its own collision), {high} too high to touch Ace (lowest point above {Below} m), " +
                          $"{flat} without a single real triangle (collision can't be made from them).");
        foreach (string which in noTriangle.Take(10)) report.AppendLine($"    no real triangle: {which}");
        if (noTriangle.Count > 10) report.AppendLine($"    ... and {noTriangle.Count - 10} more.");
        if (sheared > 0) report.AppendLine($"  {sheared} of them sit under a sheared transform; their collision copies the scale only approximately.");
        foreach (var pair in byGroup.OrderByDescending(p => p.Value.tris).Take(12))
            report.AppendLine($"    {pair.Value.count,4} meshes, {pair.Value.tris,9:N0} triangles: {pair.Key}");
        return asset;
    }

    // Collision needs at least one real triangle: three corners apart and not in a line. PhysX refuses a
    // mesh with none ("must have at least one non-degenerate triangle"), and a mesh like that draws
    // nothing anyway. Judged by the triangle's shape, never its size: a door pull's tiny triangles are
    // real. (The first version tested the size, and left 15 small, real meshes off the list.)
    internal static bool CanCollide(Mesh mesh, Dictionary<Mesh, bool> cache)
    {
        if (mesh == null) return false;
        if (cache != null && cache.TryGetValue(mesh, out bool known)) return known;
        bool any = false;
        try
        {
            var v = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount && !any; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var t = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length && !any; i += 3)
                    any = RealTriangle(v[t[i]], v[t[i + 1]], v[t[i + 2]]);
            }
        }
        catch (Exception) { any = true; }   // unreadable here: let PhysX decide
        if (cache != null) cache[mesh] = any;
        return any;
    }

    static bool RealTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, bc = c - b;
        float lab = ab.sqrMagnitude, lac = ac.sqrMagnitude;
        const float CornersApart = 1e-12f;   // closer than a micrometre: the same corner
        if (lab <= CornersApart || lac <= CornersApart || bc.sqrMagnitude <= CornersApart) return false;
        // Not in a line: the angle at a is more than about 0.0006° (sin² > 1e-10), well above float noise.
        return Vector3.Cross(ab, ac).sqrMagnitude > 1e-10f * lab * lac;
    }

    static bool HasOwnCollision(GameObject go)
    {
        foreach (var c in go.GetComponents<Collider>())
            if (c != null && c.enabled && !c.isTrigger) return true;
        return false;
    }

    static bool IsText(GameObject go) =>
        go.GetComponents<Component>().Any(c => c != null && c.GetType().Name.StartsWith("TextMeshPro", StringComparison.Ordinal));

    // A transform the collider can't copy with position, rotation and scale alone.
    static bool Sheared(Matrix4x4 m)
    {
        Vector3 x = m.GetColumn(0), y = m.GetColumn(1), z = m.GetColumn(2);
        if (x.sqrMagnitude < 1e-12f || y.sqrMagnitude < 1e-12f || z.sqrMagnitude < 1e-12f) return false;
        x.Normalize(); y.Normalize(); z.Normalize();
        return Mathf.Abs(Vector3.Dot(x, y)) > 1e-3f || Mathf.Abs(Vector3.Dot(y, z)) > 1e-3f || Mathf.Abs(Vector3.Dot(x, z)) > 1e-3f;
    }

    static string GroupOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts.Take(Math.Min(3, parts.Count - 1)));
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
