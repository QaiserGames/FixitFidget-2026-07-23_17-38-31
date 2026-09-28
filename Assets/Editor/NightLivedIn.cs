using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// NIGHT WALK 4a (set-up): THE LIVED-IN STREET (claude/night-city-proposal.md §9)
//
// Two Edit Mode steps, run from Fixit Fidget > Night (NightWalkSteps):
//
//   Build the lived-in windows: writes Assets/Playtests/AcesCafeLayout/Night walk - rooms.asset
//     (NightRooms). Every house round the café keeps all its window panes in one merged "Window
//     glass" mesh and its curtains in one "Curtain glow" mesh. This splits them into rooms: the
//     panes are grouped window by window (a bay's three panes are one room), numbered floor by
//     floor, and each room gets a lit pane (a quad 4 mm in front of each pane's outer face, its UVs
//     across and up the pane) and its own curtains. Plus the dark cap for the failing bulb (its
//     piece of the old lamps' merged glass, grown a little). New meshes, kept inside the asset,
//     made from the café's own authored geometry (never a Synty file), in each house's own space.
//     Edit Mode only: in Play Mode, static batching has merged the meshes.
//
//   Put up house numbers and street signs: a small plaque with a number beside each bay-window
//     house's front door (HouseNumber), and at each corner of the café's block two street-name
//     blades on top of the corner's signal post (StreetNameSign), named from District streets.
//     These are ordinary scene objects, seen by day too. Running it again replaces them; "Take
//     the house numbers and street signs down again" removes them (both undoable).
// ---------------------------------------------------------------------------
internal static class NightLivedIn
{
    public const string RoomsPath = "Assets/Playtests/AcesCafeLayout/Night walk - rooms.asset";
    const string PlaqueMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - house number plaque.mat";
    const string BladeMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - street sign.mat";
    const string LetteringMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - lit lettering.mat";
    const string LitLetteringShader = "TextMeshPro/SRP/TMP_SDF-URP Lit";

    const float PaneOut = .004f;      // a lit pane sits this far in front of its glass
    const float CapGrow = 1.06f;      // the dark cap is this much bigger than the lamp's glass

    // ================================================================== the rooms list

    sealed class Piece
    {
        public readonly List<int> triangles = new();   // index of each triangle's first corner
        public Bounds box;
        public bool started;
        public void Add(Vector3 p)
        {
            if (!started) { box = new Bounds(p, Vector3.zero); started = true; }
            else box.Encapsulate(p);
        }
    }

    public static NightRooms BuildRooms(NightWalk walk, StringBuilder report)
    {
        var rooms = new List<NightRooms.Room>();
        var curtainPaths = new List<string>();
        var glassPaths = new List<string>();
        var meshes = new List<Mesh>();
        int houses = 0, unmatchedCurtains = 0;

        var glassFilters = CityPackChecks.InScene<MeshFilter>()
            .Where(f => f.name == "Window glass" && f.gameObject.activeInHierarchy && f.sharedMesh != null
                        && f.transform.parent != null && f.transform.parent.name.IndexOf("house", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(f => PathOf(f.transform.parent), StringComparer.Ordinal)
            .ToList();
        foreach (MeshFilter glassFilter in glassFilters)
        {
            Transform house = glassFilter.transform.parent;
            var glassRenderer = glassFilter.GetComponent<MeshRenderer>();
            if (glassRenderer == null || !glassRenderer.enabled) continue;
            Bounds body = HouseBounds(house);
            Matrix4x4 toHouse = house.worldToLocalMatrix * glassFilter.transform.localToWorldMatrix;
            Vector3[] gv = glassFilter.sharedMesh.vertices.Select(v => toHouse.MultiplyPoint3x4(v)).ToArray();
            int[] gt = glassFilter.sharedMesh.triangles;
            List<Piece> panes = Pieces(gv, gt);
            if (panes.Count == 0) continue;

            // Window by window: panes that share a floor and touch (a bay's front and sides) are one room.
            List<List<int>> groups = GroupPanes(panes);
            var roomBoxes = groups.Select(g => { Bounds b = panes[g[0]].box; foreach (int i in g) b.Encapsulate(panes[i].box); return b; }).ToList();
            int[] floor = Floors(roomBoxes);
            var order = Enumerable.Range(0, groups.Count).OrderBy(i => floor[i]).ThenBy(i => roomBoxes[i].min.x).ToList();

            // Curtains: each piece goes to the room at its height nearest to it.
            Transform curtainT = house.Find("Curtain glow");
            MeshFilter curtainFilter = curtainT != null ? curtainT.GetComponent<MeshFilter>() : null;
            var curtainsOf = new Dictionary<int, List<Piece>>();
            Vector3[] cv = null, cn = null;
            Vector2[] cu = null;
            int[] ct = null;
            if (curtainFilter != null && curtainFilter.sharedMesh != null)
            {
                Matrix4x4 curtainToHouse = house.worldToLocalMatrix * curtainFilter.transform.localToWorldMatrix;
                Mesh cm = curtainFilter.sharedMesh;
                cv = cm.vertices.Select(v => curtainToHouse.MultiplyPoint3x4(v)).ToArray();
                cn = cm.normals.Length == cm.vertexCount ? cm.normals.Select(n => curtainToHouse.MultiplyVector(n).normalized).ToArray() : null;
                cu = cm.uv.Length == cm.vertexCount ? cm.uv : null;
                ct = cm.triangles;
                foreach (Piece piece in Pieces(cv, ct))
                {
                    int best = -1;
                    float bestDistance = float.MaxValue;
                    for (int r = 0; r < roomBoxes.Count; r++)
                    {
                        Bounds rb = roomBoxes[r];
                        float overlap = Mathf.Min(rb.max.y, piece.box.max.y) - Mathf.Max(rb.min.y, piece.box.min.y);
                        if (overlap < .5f * piece.box.size.y) continue;
                        float dx = Mathf.Max(0f, Mathf.Max(rb.min.x - piece.box.center.x, piece.box.center.x - rb.max.x));
                        float dz = Mathf.Max(0f, Mathf.Max(rb.min.z - piece.box.center.z, piece.box.center.z - rb.max.z));
                        float d = dx * dx + dz * dz;
                        if (d < bestDistance) { bestDistance = d; best = r; }
                    }
                    if (best < 0 || bestDistance > .6f * .6f) { unmatchedCurtains++; continue; }
                    if (!curtainsOf.TryGetValue(best, out var list)) curtainsOf[best] = list = new List<Piece>();
                    list.Add(piece);
                }
                if (curtainFilter.GetComponent<Renderer>() != null) curtainPaths.Add(PathOf(curtainFilter.transform));
            }

            bool shopHouse = house.name.IndexOf("shop house", StringComparison.OrdinalIgnoreCase) >= 0;
            var perFloor = new SortedDictionary<int, int>();
            int index = 0;
            foreach (int g in order)
            {
                var room = new NightRooms.Room
                {
                    house = house.name,
                    housePath = PathOf(house),
                    index = index,
                    floor = floor[g],
                    shopFront = shopHouse && floor[g] == 0,
                    centre = house.TransformPoint(roomBoxes[g].center),
                };
                room.panes = PaneQuads(groups[g].Select(i => panes[i]).ToList(), gv, gt, body.center);
                room.panes.name = $"{house.name} - room {index} - lit pane";
                meshes.Add(room.panes);
                if (curtainsOf.TryGetValue(g, out var pieces))
                {
                    room.curtains = CopyPieces(pieces, cv, cn, cu, ct);
                    room.curtains.name = $"{house.name} - room {index} - curtains";
                    meshes.Add(room.curtains);
                }
                rooms.Add(room);
                perFloor[room.floor] = perFloor.TryGetValue(room.floor, out int k) ? k + 1 : 1;
                index++;
            }
            glassPaths.Add(PathOf(glassFilter.transform));
            houses++;
            report.AppendLine($"  {house.name}: {panes.Count} panes in {groups.Count} rooms (" +
                              string.Join(", ", perFloor.Select(p => $"floor {p.Key}: {p.Value}")) + ")" +
                              (shopHouse ? ", ground floor a shop front" : "") +
                              (curtainFilter != null ? $", curtains for {curtainsOf.Count} rooms" : ", no curtains"));
        }

        // The failing bulb's dark cap.
        var caps = new List<NightRooms.Cap>();
        GameObject lamp = walk != null ? walk.nightOnly.FirstOrDefault(g => g != null && g.name == walk.flickeringLamp) : null;
        if (lamp != null)
        {
            NightRooms.Cap cap = LampCap(lamp.name, lamp.transform.position);
            if (cap != null)
            {
                caps.Add(cap);
                meshes.Add(cap.mesh);
                report.AppendLine($"  The failing bulb '{lamp.name}': a dark cap from '{cap.rendererPath}' ({cap.mesh.vertexCount} corners).");
            }
            else report.AppendLine($"  The failing bulb '{lamp.name}': no lamp glass found within 0.6 m of it (it flickers without a cap).");
        }
        else report.AppendLine($"  The failing bulb: no night-only object named '{walk?.flickeringLamp}'.");

        var asset = AssetDatabase.LoadAssetAtPath<NightRooms>(RoomsPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<NightRooms>();
            asset.name = "Night walk - rooms";
            AssetDatabase.CreateAsset(asset, RoomsPath);
        }
        foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(RoomsPath))
            if (sub is Mesh) Object.DestroyImmediate(sub, true);
        foreach (Mesh m in meshes) AssetDatabase.AddObjectToAsset(m, asset);
        asset.rooms = rooms.ToArray();
        asset.caps = caps.ToArray();
        asset.curtainPaths = curtainPaths.ToArray();
        asset.glassPaths = glassPaths.ToArray();
        asset.houses = houses;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        report.Insert(0, $"Rooms: {rooms.Count} in {houses} houses ({rooms.Count(r => r.shopFront)} shop fronts), " +
                         $"{rooms.Count(r => r.curtains != null)} with curtains; {curtainPaths.Count} merged curtains hidden at night; " +
                         $"{unmatchedCurtains} curtain pieces matched no window; {meshes.Count} meshes in '{RoomsPath}'.\n");
        return asset;
    }

    // The connected pieces of a mesh: corners at the same place (to a millimetre) or on one triangle.
    static List<Piece> Pieces(Vector3[] v, int[] t)
    {
        var parent = new int[v.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int a)
        {
            while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
            return a;
        }
        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a != b) parent[a] = b;
        }
        var at = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
            if (at.TryGetValue(key, out int j)) Union(i, j);
            else at[key] = i;
        }
        for (int k = 0; k + 2 < t.Length; k += 3) { Union(t[k], t[k + 1]); Union(t[k], t[k + 2]); }
        var byRoot = new Dictionary<int, Piece>();
        for (int k = 0; k + 2 < t.Length; k += 3)
        {
            int root = Find(t[k]);
            if (!byRoot.TryGetValue(root, out Piece piece)) byRoot[root] = piece = new Piece();
            piece.triangles.Add(k);
            piece.Add(v[t[k]]); piece.Add(v[t[k + 1]]); piece.Add(v[t[k + 2]]);
        }
        return byRoot.Values.ToList();
    }

    // Panes on the same floor (half their height overlapping) and touching (within 0.3 m across the
    // front, 1.1 m in depth: a bay's sides stand out from the wall) are one window, one room.
    static List<List<int>> GroupPanes(List<Piece> panes)
    {
        int n = panes.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int a)
        {
            while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
            return a;
        }
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                Bounds a = panes[i].box, b = panes[j].box;
                float overlap = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
                if (overlap < .5f * Mathf.Min(a.size.y, b.size.y)) continue;
                float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
                float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
                if (dx <= .3f && dz <= 1.1f) parent[Find(i)] = Find(j);
            }
        return Enumerable.Range(0, n).GroupBy(Find).Select(g => g.ToList()).ToList();
    }

    // Floor numbers from the rooms' sills: a new floor wherever the sills step up by more than half a metre.
    static int[] Floors(List<Bounds> boxes)
    {
        var sills = boxes.Select(b => b.min.y).Distinct().OrderBy(y => y).ToList();
        var floorOf = new Dictionary<float, int>();
        int floor = 0;
        for (int i = 0; i < sills.Count; i++)
        {
            if (i > 0 && sills[i] - sills[i - 1] > .5f) floor++;
            floorOf[sills[i]] = floor;
        }
        return boxes.Select(b => floorOf[b.min.y]).ToArray();
    }

    // A quad just in front of each pane's outer face (the largest face looking away from the house).
    static Mesh PaneQuads(List<Piece> panes, Vector3[] v, int[] t, Vector3 houseCentre)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        foreach (Piece pane in panes)
        {
            var area = new Dictionary<Vector3Int, float>();
            var members = new Dictionary<Vector3Int, List<int>>();
            foreach (int k in pane.triangles)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float twice = cross.magnitude;
                if (twice < 1e-8f) continue;
                Vector3 n = cross / twice;
                var key = new Vector3Int(Mathf.RoundToInt(n.x * 100f), Mathf.RoundToInt(n.y * 100f), Mathf.RoundToInt(n.z * 100f));
                area[key] = (area.TryGetValue(key, out float s) ? s : 0f) + twice * .5f;
                if (!members.TryGetValue(key, out var list)) members[key] = list = new List<int>();
                list.Add(k);
            }
            Vector3 outward = pane.box.center - houseCentre;
            outward.y = 0f;
            Vector3Int bestKey = default;
            float bestArea = -1f;
            foreach (var pair in area)
            {
                Vector3 n = new Vector3(pair.Key.x, pair.Key.y, pair.Key.z) / 100f;
                if (Mathf.Abs(n.y) > .5f || Vector3.Dot(new Vector3(n.x, 0f, n.z), outward) <= 0f) continue;
                if (pair.Value > bestArea) { bestArea = pair.Value; bestKey = pair.Key; }
            }
            if (bestArea <= 0f) continue;
            Vector3 face = new Vector3(bestKey.x, 0f, bestKey.z).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, face).normalized;
            float u0 = float.MaxValue, u1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue, depth = float.MinValue;
            foreach (int k in members[bestKey])
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = v[t[k + c]];
                    float u = Vector3.Dot(p, across);
                    u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                    y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                    depth = Mathf.Max(depth, Vector3.Dot(p, face));
                }
            Vector3 Corner(float u, float y) => across * u + Vector3.up * y + face * (depth + PaneOut);
            int start = verts.Count;
            verts.Add(Corner(u0, y0)); verts.Add(Corner(u0, y1)); verts.Add(Corner(u1, y1)); verts.Add(Corner(u1, y0));
            for (int i = 0; i < 4; i++) normals.Add(face);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
            // Facing out: a triangle's front is the side its (b - a) x (c - a) points to.
            bool flip = Vector3.Dot(Vector3.Cross(verts[start + 1] - verts[start], verts[start + 2] - verts[start]), face) < 0f;
            if (!flip) tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            else tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CopyPieces(List<Piece> pieces, Vector3[] v, Vector3[] n, Vector2[] uv, int[] t)
    {
        var map = new Dictionary<int, int>();
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        foreach (Piece piece in pieces)
            foreach (int k in piece.triangles)
                for (int c = 0; c < 3; c++)
                {
                    int i = t[k + c];
                    if (!map.TryGetValue(i, out int j))
                    {
                        j = verts.Count;
                        map[i] = j;
                        verts.Add(v[i]);
                        if (n != null) normals.Add(n[i]);
                        if (uv != null) uvs.Add(uv[i]);
                    }
                    tris.Add(j);
                }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        if (n != null) mesh.SetNormals(normals);
        if (uv != null) mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        if (n == null) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // The box round a house's own meshes (glass and curtains aside), in the house's space.
    static Bounds HouseBounds(Transform house)
    {
        Bounds b = new Bounds();
        bool started = false;
        foreach (MeshFilter f in house.GetComponentsInChildren<MeshFilter>(false))
        {
            if (f.sharedMesh == null || f.name == "Window glass" || f.name == "Curtain glow") continue;
            Matrix4x4 m = house.worldToLocalMatrix * f.transform.localToWorldMatrix;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!started) { b = new Bounds(p, Vector3.zero); started = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // The piece of a merged "Lamp glass" mesh at this lamp, grown a little: shown dark while the bulb is out.
    static NightRooms.Cap LampCap(string name, Vector3 at)
    {
        foreach (MeshFilter f in CityPackChecks.InScene<MeshFilter>())
        {
            if (f.name != "Lamp glass" || !f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
            Vector3[] local = f.sharedMesh.vertices;
            int[] t = f.sharedMesh.triangles;
            Vector3[] world = local.Select(p => f.transform.TransformPoint(p)).ToArray();
            foreach (Piece piece in Pieces(world, t))
            {
                Vector3 d = piece.box.center - at;
                d.y = 0f;
                if (d.magnitude > .6f || Mathf.Abs(piece.box.center.y - at.y) > 1.2f) continue;
                Mesh copy = CopyPieces(new List<Piece> { piece }, local, f.sharedMesh.normals.Length == local.Length ? f.sharedMesh.normals : null, null, t);
                Vector3[] cv = copy.vertices;
                Vector3 centre = copy.bounds.center;
                for (int i = 0; i < cv.Length; i++) cv[i] = centre + (cv[i] - centre) * CapGrow;
                copy.vertices = cv;
                copy.RecalculateBounds();
                copy.name = $"{name} - dark cap";
                return new NightRooms.Cap { name = name, rendererPath = PathOf(f.transform), mesh = copy };
            }
        }
        return null;
    }

    // ================================================================== house numbers and street signs

    const string PlaqueName = "House number (day and night)";
    const string SignName = "Street names (day and night)";
    // On the wall between the ground-floor window and the front door, clear of the bay above (house space).
    static readonly Vector2 PlaqueAt = new Vector2(.40f, 1.95f);
    static readonly Vector2 PlaqueSize = new Vector2(.30f, .20f);
    const float PlaqueDepth = .025f;
    const int FirstNumber = 12, NumberStep = 2;
    // The four streets round the café's block (their middle lines) and the blades' heights.
    const float WestX = -12.2f, EastX = 12.9f, FrontZ = -8f, BackZ = 23.5f;
    const float LowBlade = 3.08f, HighBlade = 3.34f, PoleTop = 3.52f;
    static readonly Vector3 BladeSize = new Vector3(1.3f, .22f, .025f);

    public static void PutUp(StringBuilder report)
    {
        int removed = TakeDown(null);
        if (removed > 0) report.AppendLine($"Took down the {removed} put up before.");
        Material plaque = LitMaterial(PlaqueMaterialPath, "Night walk - house number plaque", new Color(.09f, .12f, .2f), .45f);
        Material blade = LitMaterial(BladeMaterialPath, "Night walk - street sign", new Color(.08f, .33f, .19f), .35f);
        Material lettering = Lettering(report);
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) throw new InvalidOperationException("TextMesh Pro has no default font asset.");

        // House numbers: the bay-window houses, south to north. A regular's door shows its own number.
        var bayHouses = CityPackChecks.InScene<StreetDoor>()
            .Select(d => d.transform.parent)
            .Where(h => h != null && h.gameObject.activeInHierarchy && h.name.IndexOf("bay-window house", StringComparison.OrdinalIgnoreCase) >= 0)
            .Distinct().OrderBy(h => h.position.z).ToList();
        int number = FirstNumber;
        foreach (Transform house in bayHouses)
        {
            HomeDoor home = house.GetComponentInChildren<HomeDoor>(true);
            float wall = WallDepth(house, PlaqueAt, PlaqueSize);
            var root = new GameObject(PlaqueName);
            Undo.RegisterCreatedObjectUndo(root, "House number");
            root.transform.SetParent(house, false);
            root.transform.localPosition = new Vector3(PlaqueAt.x, PlaqueAt.y, wall + PlaqueDepth * .5f + .003f);
            root.transform.localRotation = Quaternion.identity;
            Box(root.transform, "Plaque", Vector3.zero, new Vector3(PlaqueSize.x, PlaqueSize.y, PlaqueDepth), plaque);
            TextMeshPro label = Lettering(root.transform, "Number", new Vector3(0f, 0f, PlaqueDepth * .5f + .002f), Quaternion.Euler(0f, 180f, 0f),
                                          new Vector2(PlaqueSize.x - .04f, PlaqueSize.y - .04f), font, lettering, new Color(.96f, .92f, .8f));
            var tag = root.AddComponent<HouseNumber>();
            tag.number = (number).ToString();
            tag.home = home;
            tag.label = label;
            label.text = tag.Shown;
            report.AppendLine($"  {house.name}: plaque '{tag.Shown}'{(home != null ? $" (from {home.homeId}'s door)" : " (a placeholder)")} at house ({PlaqueAt.x:0.00}, {PlaqueAt.y:0.00}), " +
                              $"wall at {wall:0.000} m, world ({root.transform.position.x:0.00}, {root.transform.position.y:0.00}, {root.transform.position.z:0.00}).");
            number += NumberStep;
        }

        // Street names: the signal post at each corner of the café's block, two blades on top.
        var posts = CityPackChecks.InScene<Transform>().Where(t => t.name == "Signal post" && t.gameObject.activeInHierarchy).ToList();
        var corners = new[]
        {
            (junction: new Vector2(WestX, FrontZ), across: "west", along: "front"),
            (junction: new Vector2(WestX, BackZ), across: "west", along: "back"),
            (junction: new Vector2(EastX, FrontZ), across: "east", along: "front"),
            (junction: new Vector2(EastX, BackZ), across: "east", along: "back"),
        };
        foreach (var corner in corners)
        {
            // The café's side of the junction: between the two streets that bound the block.
            Transform post = posts
                .Where(p => (new Vector2(p.position.x, p.position.z) - corner.junction).magnitude < 9f
                            && p.position.x > WestX && p.position.x < EastX && p.position.z > FrontZ && p.position.z < BackZ)
                .OrderBy(p => (new Vector2(p.position.x, p.position.z) - corner.junction).sqrMagnitude).FirstOrDefault();
            if (post == null) { report.AppendLine($"  Corner ({corner.junction.x}, {corner.junction.y}): no signal post on the café's side."); continue; }
            float top = post.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds.max.y).DefaultIfEmpty(2.64f).Max();
            Transform steel = post.Find("Slim steel post");
            Material pole = steel != null && steel.GetComponent<Renderer>() != null ? steel.GetComponent<Renderer>().sharedMaterial : blade;
            Vector3 at = new Vector3(post.position.x, 0f, post.position.z);

            var root = new GameObject(SignName);
            Undo.RegisterCreatedObjectUndo(root, "Street names");
            root.transform.SetParent(post, true);
            root.transform.SetPositionAndRotation(at, Quaternion.identity);
            float poleFrom = Mathf.Min(top, LowBlade - .2f);
            var extension = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            extension.name = "Pole extension";
            Object.DestroyImmediate(extension.GetComponent<Collider>());
            extension.transform.SetParent(root.transform, false);
            extension.transform.localPosition = new Vector3(0f, (poleFrom + PoleTop) * .5f, 0f);
            extension.transform.localScale = new Vector3(.055f, (PoleTop - poleFrom) * .5f, .055f);
            Quiet(extension.GetComponent<MeshRenderer>(), pole);

            var sign = root.AddComponent<StreetNameSign>();
            sign.blades = new[]
            {
                Blade(root.transform, corner.across, LowBlade, true, blade, lettering, font),    // along the north-south street
                Blade(root.transform, corner.along, HighBlade, false, blade, lettering, font),   // along the east-west street
            };
            report.AppendLine($"  Corner by ({corner.junction.x}, {corner.junction.y}): '{PathOf(post)}' at ({post.position.x:0.00}, {post.position.z:0.00}), " +
                              $"post top {top:0.00} m: blades '{sign.Says()}' at {LowBlade:0.00} and {HighBlade:0.00} m.");
        }
        EditorSceneManagerMarkDirty();
    }

    /// <summary>Removes every plaque and street sign put up here (undoable). Returns how many.</summary>
    public static int TakeDown(StringBuilder report)
    {
        var gone = CityPackChecks.InScene<HouseNumber>().Select(h => h.gameObject)
            .Concat(CityPackChecks.InScene<StreetNameSign>().Select(s => s.gameObject)).Distinct().ToList();
        foreach (GameObject go in gone) Undo.DestroyObjectImmediate(go);
        if (gone.Count > 0) EditorSceneManagerMarkDirty();
        report?.AppendLine($"Took down {gone.Count} house numbers and street signs.");
        return gone.Count;
    }

    static StreetNameSign.Blade Blade(Transform root, string streetId, float height, bool northSouth, Material material, Material lettering, TMP_FontAsset font)
    {
        Vector3 size = northSouth ? new Vector3(BladeSize.z, BladeSize.y, BladeSize.x) : BladeSize;
        GameObject blade = Box(root, northSouth ? "Blade (north-south street)" : "Blade (east-west street)", new Vector3(0f, height, 0f), size, material);
        float face = BladeSize.z * .5f + .002f;
        var text = new Vector2(BladeSize.x - .1f, BladeSize.y - .05f);
        Color white = new Color(.97f, .97f, .95f);
        TextMeshPro one, two;
        if (northSouth)
        {
            one = Lettering(blade.transform.parent, "Name (east side)", new Vector3(face, height, 0f), Quaternion.Euler(0f, -90f, 0f), text, font, lettering, white);
            two = Lettering(blade.transform.parent, "Name (west side)", new Vector3(-face, height, 0f), Quaternion.Euler(0f, 90f, 0f), text, font, lettering, white);
        }
        else
        {
            one = Lettering(blade.transform.parent, "Name (north side)", new Vector3(0f, height, face), Quaternion.Euler(0f, 180f, 0f), text, font, lettering, white);
            two = Lettering(blade.transform.parent, "Name (south side)", new Vector3(0f, height, -face), Quaternion.identity, text, font, lettering, white);
        }
        string name = StreetNames.Name(streetId);
        one.text = two.text = name;
        return new StreetNameSign.Blade { streetId = streetId, faces = new TMP_Text[] { one, two } };
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        Quiet(box.GetComponent<MeshRenderer>(), material);
        return box;
    }

    static void Quiet(MeshRenderer r, Material material)
    {
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static TextMeshPro Lettering(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, TMP_FontAsset font, Material material, Color colour)
    {
        var go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        var text = go.GetComponent<TextMeshPro>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.rectTransform.sizeDelta = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = .1f;
        text.fontSizeMax = 2.4f;
        text.color = colour;
        text.fontStyle = FontStyles.Bold;
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
        return text;
    }

    // How far out the wall (with its siding) stands where the plaque goes, in the house's space.
    static float WallDepth(Transform house, Vector2 at, Vector2 size)
    {
        float xMin = at.x - size.x * .5f, xMax = at.x + size.x * .5f, yMin = at.y - size.y * .5f, yMax = at.y + size.y * .5f;
        float depth = float.MinValue;
        foreach (MeshFilter f in house.GetComponentsInChildren<MeshFilter>(false))
        {
            if (f.sharedMesh == null || f.GetComponentInParent<StreetDoor>() != null || f.GetComponentInParent<HouseNumber>() != null) continue;
            if (f.name == "Window glass" || f.name == "Curtain glow" || f.name == "Doorway hall") continue;
            Matrix4x4 m = house.worldToLocalMatrix * f.transform.localToWorldMatrix;
            Vector3[] v = f.sharedMesh.vertices;
            int[] t = f.sharedMesh.triangles;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[t[k]]), b = m.MultiplyPoint3x4(v[t[k + 1]]), c = m.MultiplyPoint3x4(v[t[k + 2]]);
                float tx0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), tx1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float ty0 = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), ty1 = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                float tz0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), tz1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                if (tx1 < xMin || tx0 > xMax || ty1 < yMin || ty0 > yMax) continue;
                if (tz0 < -.3f || tz1 > .4f) continue;   // the front wall and its siding only (not the bay above, not the back)
                depth = Mathf.Max(depth, tz1);
            }
        }
        return depth > float.MinValue ? depth : .05f;
    }

    static Material LitMaterial(string path, string name, Color colour, float smoothness)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(lit) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = lit;
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Lettering lit by the scene (TextMesh Pro's URP Lit shader), so it is dark at night until a lamp or the
    // torch finds it; TextMesh Pro's own (unlit) material if that shader isn't there.
    static Material Lettering(StringBuilder report)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Shader lit = Shader.Find(LitLetteringShader);
        if (font == null || font.material == null) return null;
        if (lit == null)
        {
            report.AppendLine($"  Lettering: the shader '{LitLetteringShader}' was not found, so it uses TextMesh Pro's own (it will not dim at night).");
            return font.material;
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(LetteringMaterialPath);
        if (m == null)
        {
            m = new Material(font.material) { name = "Night walk - lit lettering" };
            AssetDatabase.CreateAsset(m, LetteringMaterialPath);
        }
        else m.CopyPropertiesFromMaterial(font.material);
        m.shader = lit;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        report.AppendLine($"  Lettering: '{LetteringMaterialPath}' ({lit.name}, from {font.name}).");
        return m;
    }

    static void EditorSceneManagerMarkDirty() =>
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

    internal static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
