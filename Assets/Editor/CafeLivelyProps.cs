#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Ace's Cafe interior, POLYGON details (23 Sept). Step 0 surveys the room -
// every renderer, collider, gameplay anchor and the baked NavMesh inside it,
// plus a measured plan photo and wall elevations - so the dressing step can
// place props by measurement: never on a seat, cup spot, stand point or
// waiting spot, never where customers walk, and wall items only on the two
// walls the overhead view never cuts away.
public static class CafeLivelyProps
{
    const string Menu = "Fixit Fidget/City pack/";
    const string Tag = "[Cafe props] ";
    // Interior of the cafe room plus the patio in front of the doors.
    static readonly Rect Room = Rect.MinMaxRect(-7.8f, -3.6f, 7.8f, 18.4f);
    const int PlanPixelsPerMeter = 40;

    [MenuItem(Menu + "Cafe 0 - Survey the cafe interior (plan, anchors, NavMesh)")]
    static void SurveyMenu() => Run("Survey", Survey);

    public static string Survey()
    {
        CityPackChecks.RequireScene();
        string folder = Path.Combine(CityPackCatalog.LogRoot, "cafe-survey-" + Stamp());
        Directory.CreateDirectory(folder);
        var sb = new StringBuilder();
        sb.AppendLine("# kind\tpath\tmin\tmax\textra");
        int renderers = 0, colliders = 0, anchors = 0, triangles = 0;
        foreach (var r in CityPackChecks.InScene<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
            Bounds b = r.bounds;
            if (!InRoom(b) || b.min.y > 4.5f) continue;
            string mats = string.Join(",", r.sharedMaterials.Where(m => m != null).Select(m => m.name));
            sb.Append("R\t").Append(PathOf(r.transform)).Append('\t').Append(V(b.min)).Append('\t').Append(V(b.max)).Append('\t').Append(mats).AppendLine();
            renderers++;
        }
        foreach (var c in CityPackChecks.InScene<Collider>())
        {
            if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds b = c.bounds;
            if (!InRoom(b) || b.min.y > 4.5f) continue;
            sb.Append("C\t").Append(PathOf(c.transform)).Append('\t').Append(V(b.min)).Append('\t').Append(V(b.max)).Append('\t')
              .Append(c.isTrigger ? "trigger" : "solid").Append(' ').Append(c.GetType().Name).AppendLine();
            colliders++;
        }
        foreach (var spot in CityPackChecks.InScene<WaitingSpot>())
        {
            var extra = new StringBuilder(spot.Kind.ToString());
            if (spot.StandPoint != null) extra.Append(" stand=").Append(V(spot.StandPoint.position));
            if (spot is TableSeat seat)
            {
                if (seat.CupSpot != null) extra.Append(" cup=").Append(V(seat.CupSpot.position));
                if (seat.SeatPose != null) extra.Append(" pose=").Append(V(seat.SeatPose.position));
            }
            sb.Append("W\t").Append(PathOf(spot.transform)).Append('\t').Append(V(spot.transform.position)).Append('\t').Append(V(spot.transform.forward)).Append('\t').Append(extra).AppendLine();
            anchors++;
        }
        // Every other scripted object in the room: stations, interactables, spawn points.
        foreach (var m in CityPackChecks.InScene<MonoBehaviour>())
        {
            if (m == null || m is WaitingSpot || !m.gameObject.activeInHierarchy) continue;
            Vector3 p = m.transform.position;
            if (!Room.Contains(new Vector2(p.x, p.z))) continue;
            string type = m.GetType().Name;
            if (type == "PolygonNpcVisual" || type == "NpcVisualVariants") continue;
            sb.Append("M\t").Append(PathOf(m.transform)).Append('\t').Append(V(p)).Append('\t').Append(V(m.transform.forward)).Append('\t').Append(type).AppendLine();
        }
        var mesh = NavMesh.CalculateTriangulation();
        for (int i = 0; i + 2 < mesh.indices.Length; i += 3)
        {
            Vector3 a = mesh.vertices[mesh.indices[i]], b = mesh.vertices[mesh.indices[i + 1]], c = mesh.vertices[mesh.indices[i + 2]];
            Vector3 centre = (a + b + c) / 3f;
            if (!Room.Contains(new Vector2(centre.x, centre.z))) continue;
            sb.Append("N\t").Append(V(a)).Append('\t').Append(V(b)).Append('\t').Append(V(c)).Append('\t').Append(mesh.areas[i / 3]).AppendLine();
            triangles++;
        }
        File.WriteAllText(Path.Combine(folder, "survey.tsv"), sb.ToString());
        string plans = Plan(Path.Combine(folder, "plan-2.6m.png"), 2.6f) + ", " + Plan(Path.Combine(folder, "plan-1.3m.png"), 1.3f);
        foreach (var view in Elevations)
            CafeSecondPassSteps.Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, false);
        return "Survey: " + renderers + " renderers, " + colliders + " colliders, " + anchors + " waiting spots/seats, " + triangles
            + " NavMesh triangles; " + plans + "; " + Elevations.Length + " elevations.\nEvidence: " + folder;
    }

    static readonly (string name, Vector3 position, Vector3 target, float fov)[] Elevations =
    {
        ("e1-north-counters", new Vector3(0f, 1.65f, 5.5f), new Vector3(0f, 1.2f, 18f), 70f),
        ("e2-east-wall", new Vector3(-4.5f, 1.65f, 9f), new Vector3(7.4f, 1.3f, 9f), 75f),
        ("e3-west-wall", new Vector3(4.5f, 1.65f, 9f), new Vector3(-7.4f, 1.3f, 9f), 75f),
        ("e4-south-entrance", new Vector3(0f, 1.65f, 13f), new Vector3(0f, 1.2f, 0f), 75f),
        ("e5-repair-bench", new Vector3(-3.3f, 1.9f, 12.2f), new Vector3(-3.6f, 0.9f, 17.5f), 70f),
        ("e6-drinks-counter", new Vector3(3.1f, 1.9f, 12.2f), new Vector3(3.4f, 0.9f, 17.5f), 70f),
        ("e7-patio", new Vector3(3f, 1.8f, -6f), new Vector3(-3f, 0.6f, -1f), 70f),
    };

    // A measured orthographic plan: 40 px per metre over the room rect, the
    // camera at 'cut' metres looking down, so everything above the cut (ceiling,
    // pendants, upper walls) is clipped by the near plane.
    static string Plan(string path, float cut)
    {
        int w = Mathf.RoundToInt(Room.width * PlanPixelsPerMeter), h = Mathf.RoundToInt(Room.height * PlanPixelsPerMeter);
        var go = new GameObject("Temporary plan camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.orthographic = true;
            cam.orthographicSize = Room.height * .5f;
            cam.aspect = Room.width / Room.height;
            cam.transform.SetPositionAndRotation(new Vector3(Room.center.x, cut, Room.center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = .01f;
            cam.farClipPlane = cut + 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.1f, .1f, .12f);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
        return Path.GetFileName(path) + " (" + w + "x" + h + ", origin " + V(new Vector3(Room.xMin, 0f, Room.yMin)) + ")";
    }

    // ------------------------------------------------------------ dressing

    public const string RootName = "16 - POLYGON cafe details";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    const string CityProps = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string GenProps = "Assets/Synty/PolygonGeneric/Prefabs/Props/";

    enum On { Floor, Surface, Wall }

    // What goes where. Positions are the prop's pivot (x, z) in the cafe;
    // 'On' decides the height: the floor, the top of the furniture under it
    // (measured, not typed in), or a wall point given as (x, y, z).
    static readonly (string group, string prefab, On on, Vector3 at, float yaw, float scale)[] Dressing =
    {
        // Tall plants in the corners and along the courtyard windows, clear of
        // the waiting spots and of every walking line to the tables.
        ("Plants", CityProps + "SM_Prop_PotPlant_02", On.Floor, new Vector3(-6.9f, 0f, .56f), 20f, 1f),
        ("Plants", CityProps + "SM_Prop_PotPlant_01", On.Floor, new Vector3(6.92f, 0f, 2.1f), 70f, 1f),
        ("Plants", CityProps + "SM_Prop_PotPlant_02", On.Floor, new Vector3(6.92f, 0f, 7.7f), 140f, 1f),
        ("Plants", CityProps + "SM_Prop_PotPlant_01", On.Floor, new Vector3(6.92f, 0f, 11.4f), 200f, 1f),
        ("Plants", CityProps + "SM_Prop_PotPlant_02", On.Floor, new Vector3(-6.9f, 0f, 12.15f), 260f, 1f),
        ("Plants", CityProps + "SM_Prop_PotPlant_02", On.Floor, new Vector3(6.9f, 0f, 17.42f), 310f, 1f),
        // Staff corner past the repair bench: supplies on a shelf, a crate, boxes.
        ("Back of house", CityProps + "SM_Prop_ShopInterior_Shelf_01", On.Floor, new Vector3(-6.45f, 0f, 17.44f), 180f, 1f),
        ("Back of house", GenProps + "SM_Gen_Prop_Crate_03", On.Floor, new Vector3(-6.78f, 0f, 15.2f), 8f, 1f),
        ("Back of house", GenProps + "SM_Gen_Prop_Cardboard_Box_02", On.Surface, new Vector3(-6.74f, 0f, 15.18f), -14f, .9f),
        ("Back of house", GenProps + "SM_Gen_Prop_Cardboard_Box_04", On.Floor, new Vector3(-5.95f, 0f, 15.05f), 22f, 1f),
        // Front counter, left: a bread board and cups beside the pastry dome;
        // the intake shelf in the middle stays clear.
        ("Counter", GenProps + "SM_Gen_Prop_Plate_01", On.Surface, new Vector3(-6.35f, 0f, 13.74f), 0f, .6f),
        ("Counter", GenProps + "SM_Gen_Prop_Food_Bread_01", On.Surface, new Vector3(-6.35f, 0f, 13.74f), 25f, .55f),
        ("Counter", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(-5.55f, 0f, 13.62f), 30f, .7f),
        ("Counter", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(-5.38f, 0f, 13.8f), 150f, .7f),
        ("Counter", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(-5.22f, 0f, 13.62f), 260f, .7f),
        ("Counter", CityProps + "SM_Prop_Newspaper_01", On.Surface, new Vector3(-3.05f, 0f, 13.78f), 12f, .85f),
        // Front counter, right end (past the hand-over area): plates and a plant.
        ("Counter", GenProps + "SM_Gen_Prop_Plate_01", On.Surface, new Vector3(3.85f, 0f, 13.74f), 0f, .55f),
        ("Counter", GenProps + "SM_Gen_Prop_Plate_01", On.Surface, new Vector3(3.85f, 0f, 13.74f), 15f, .55f),
        ("Counter", GenProps + "SM_Gen_Prop_Plate_01", On.Surface, new Vector3(3.85f, 0f, 13.74f), 40f, .55f),
        ("Counter", CityProps + "SM_Prop_PotPlant_01", On.Surface, new Vector3(4.45f, 0f, 13.72f), 90f, .55f),
        // Drinks counter: syrup bottles left of the cup stack, mugs right of the basin.
        // (24 Sept: the drinks counter now stands 0.815 m further back, against the
        // wall - see CafeDrinksCorner - so these sit 0.815 m further back too, and
        // the delivery box sits where the back bar finish slid it, on the shortened
        // storage top.)
        ("Drinks", GenProps + "SM_Gen_Prop_Bottle_01", On.Surface, new Vector3(1.74f, 0f, 17.695f), 0f, .55f),
        ("Drinks", GenProps + "SM_Gen_Prop_Bottle_03", On.Surface, new Vector3(1.9f, 0f, 17.745f), 40f, .55f),
        ("Drinks", GenProps + "SM_Gen_Prop_Bottle_02", On.Surface, new Vector3(2.06f, 0f, 17.675f), 80f, .55f),
        ("Drinks", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(4.05f, 0f, 17.675f), 200f, .7f),
        ("Drinks", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(4.24f, 0f, 17.735f), 250f, .7f),
        ("Drinks", GenProps + "SM_Gen_Prop_Mug_01", On.Surface, new Vector3(4.43f, 0f, 17.655f), 300f, .7f),
        // Rear cabinet: today's delivery.
        ("Drinks", GenProps + "SM_Gen_Prop_Cardboard_Box_04", On.Surface, new Vector3(1.25f, 0f, 17.36f), 90f, .9f),
        // Back wall clock, right of the cat portrait and its lamp.
        ("Walls", GenProps + "SM_Gen_Prop_Clock_01", On.Wall, new Vector3(6.5f, 2.35f, 17.995f), 180f, 1f),
        // Patio: planters either side of the open doors.
        ("Patio", CityProps + "SM_Prop_Planter_01", On.Floor, new Vector3(-3.35f, 0f, -.68f), 0f, 1f),
        ("Patio", CityProps + "SM_Prop_Planter_01", On.Floor, new Vector3(3.35f, 0f, -.68f), 180f, 1f),
    };

    [MenuItem(Menu + "Cafe 1 - Dress the cafe (POLYGON props)")]
    static void DressMenu() => Run("Dress", Dress);

    [MenuItem(Menu + "Cafe - Undo: remove the POLYGON cafe details")]
    static void RemoveMenu() => Run("Remove", Remove);

    [MenuItem(Menu + "Cafe - Photograph the cafe")]
    static void PhotoMenu() => Run("Photos", () => Photograph("now"));

    public static Transform FindRoot()
    {
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == CafeRootName);
        return cafe != null ? cafe.transform.Find(RootName) : null;
    }

    public static string Dress()
    {
        CityPackChecks.RequireScene();
        CityPackChecks.Require(FindRoot() == null, "The cafe is already dressed. Use Cafe - Undo first.");
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().First(r => r.name == CafeRootName);
        // Everything a customer or the player stands on, sits at or puts a cup on.
        var keepClear = new List<(Vector3 p, float r, string what)>();
        foreach (var spot in CityPackChecks.InScene<WaitingSpot>())
        {
            keepClear.Add((spot.transform.position, .8f, spot.name));
            if (spot.StandPoint != null) keepClear.Add((spot.StandPoint.position, .7f, spot.name + " stand point"));
            if (spot is TableSeat seat && seat.CupSpot != null) keepClear.Add((seat.CupSpot.position, .35f, spot.name + " cup spot"));
        }
        foreach (var m in CityPackChecks.InScene<MonoBehaviour>())
        {
            string type = m != null ? m.GetType().Name : "";
            if (type == "ItemSlotArea" || type == "DropSpot" || type == "IntakeShelf" || type == "BenchRig" || type == "BeverageStation" || type == "CounterQueue")
                keepClear.Add((m.transform.position, type == "CounterQueue" ? 1.2f : .55f, m.name + " (" + type + ")"));
        }
        foreach (var photo in CityPackChecks.InScene<MonoBehaviour>().Where(b => b != null && b.GetType().Name == "GracePhotoDisplay"))
            keepClear.Add((photo.transform.position, .6f, "Grace photo"));

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Dress the cafe");
        var root = new GameObject(RootName).transform;
        root.SetParent(cafe.transform, false);
        root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
        var placed = new List<string>();
        var refused = new List<string>();
        meshCache.Clear();
        try
        {
            foreach (var item in Dressing)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefab + ".prefab");
                CityPackChecks.Require(prefab != null, "Missing purchased prefab " + item.prefab);
                var parent = root.Find(item.group) ?? NewGroup(root, item.group);
                // Height first (from the real geometry, earlier props included, so
                // a loaf lands on its plate and a box on its crate), then the prop.
                Vector2 xz = new Vector2(item.at.x, item.at.z);
                float y = item.at.y;
                if (item.on == On.Floor) y = TopAt(xz, .2f);
                else if (item.on == On.Surface) y = TopAt(xz, 1.3f);
                CityPackChecks.Require(!float.IsNegativeInfinity(y), "Nothing to stand " + item.prefab + " on at " + xz);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.localScale = Vector3.one * item.scale;
                go.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
                // Some pack meshes list more materials than they have submeshes (an
                // extra draw of the last submesh and a static-batching warning).
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && r.sharedMaterials.Length > filter.sharedMesh.subMeshCount)
                        r.sharedMaterials = r.sharedMaterials.Take(filter.sharedMesh.subMeshCount).ToArray();
                }
                string name = Path.GetFileName(item.prefab).Replace("SM_Gen_Prop_", "").Replace("SM_Prop_", "");
                go.transform.position = new Vector3(item.at.x, y, item.at.z);
                // Its footprint must not reach a seat, stand point, cup spot, slot or the queue.
                Bounds b = BoundsOf(go);
                var clash = keepClear.FirstOrDefault(k => Vector2.Distance(new Vector2(k.p.x, k.p.z), Closest(b, k.p)) < k.r);
                if (clash.what != null)
                {
                    refused.Add(name + " at " + V(go.transform.position) + " (too close to " + clash.what + ")");
                    Object.DestroyImmediate(go);
                    continue;
                }
                if (item.on == On.Floor && OpenFloor(b))
                {
                    refused.Add(name + " at " + V(go.transform.position) + " (stands in open walking space)");
                    Object.DestroyImmediate(go);
                    continue;
                }
                go.name = item.group + " - " + name;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    if (PrefabUtility.IsPartOfPrefabInstance(t))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                    }
                    foreach (var c in t.GetComponents<Component>())
                        if ((c is Collider || c is Light || c is Behaviour || c is Renderer) && PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                }
                placed.Add(name);
            }
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Dress the cafe");
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            throw;
        }
        string photos = Photograph("dressed");
        return "Cafe dressed with " + placed.Count + " POLYGON props (" + string.Join(", ", placed.GroupBy(n => n).Select(g => g.Count() + " " + g.Key)) + ")."
            + (refused.Count > 0 ? "\nLeft out (" + refused.Count + "):\n" + string.Join("\n", refused) : "\nNothing had to be left out.")
            + "\nNo colliders, lights or scripts; NavMesh ignores them. Nothing saved.\n" + photos;
    }

    public static string Remove()
    {
        CityPackChecks.RequireScene();
        var root = FindRoot();
        CityPackChecks.Require(root != null, "There are no POLYGON cafe details to remove.");
        Undo.DestroyObjectImmediate(root.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Removed the POLYGON cafe details. Nothing saved.";
    }

    static Transform NewGroup(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> meshCache = new Dictionary<Mesh, (Vector3[], int[])>();

    // The highest visible surface under (x, z) that is no higher than 'below':
    // every triangle of every mesh renderer over the point is tested, so a
    // combined mesh's huge bounds never fake a surface, and the 17 mm staff
    // floor, a counter top or a plate placed a moment ago are all found exactly.
    static float TopAt(Vector2 p, float below)
    {
        float best = float.NegativeInfinity;
        var world = new List<Vector3>();
        foreach (var f in CityPackChecks.InScene<MeshFilter>())
        {
            var r = f.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
            Bounds b = r.bounds;
            if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.z || p.y > b.max.z || b.min.y > below) continue;
            if (!meshCache.TryGetValue(f.sharedMesh, out var data)) meshCache[f.sharedMesh] = data = (f.sharedMesh.vertices, f.sharedMesh.triangles);
            Matrix4x4 m = f.transform.localToWorldMatrix;
            world.Clear();
            foreach (var v in data.vertices) world.Add(m.MultiplyPoint3x4(v));
            for (int i = 0; i + 2 < data.triangles.Length; i += 3)
            {
                Vector3 a = world[data.triangles[i]], c = world[data.triangles[i + 1]], d = world[data.triangles[i + 2]];
                float det = (c.z - d.z) * (a.x - d.x) + (d.x - c.x) * (a.z - d.z);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float l1 = ((c.z - d.z) * (p.x - d.x) + (d.x - c.x) * (p.y - d.z)) / det;
                float l2 = ((d.z - a.z) * (p.x - d.x) + (a.x - d.x) * (p.y - d.z)) / det;
                float l3 = 1f - l1 - l2;
                if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                float y = l1 * a.y + l2 * c.y + l3 * d.y;
                if (y <= below && y > best) best = y;
            }
        }
        return best;
    }

    // A floor prop in open walking space: its centre is on the NavMesh and
    // more than 0.6 m from any NavMesh edge (so not against a wall or furniture).
    static bool OpenFloor(Bounds b)
    {
        if (!NavMesh.SamplePosition(b.center, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return false;
        if (Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(b.center.x, b.center.z)) > .05f) return false;
        return NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance > .6f;
    }

    static Vector2 Closest(Bounds b, Vector3 p) => new Vector2(Mathf.Clamp(p.x, b.min.x, b.max.x), Mathf.Clamp(p.z, b.min.z, b.max.z));

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    static readonly (string name, Vector3 position, Vector3 target, float fov, bool isometric)[] Views =
    {
        ("d01-game-camera", new Vector3(-10.51f, 23.79f, -13.54f), new Vector3(-1.24f, 3.33f, 6.35f), 44f, true),
        ("d02-counters", new Vector3(0f, 1.65f, 5.5f), new Vector3(0f, 1.2f, 18f), 70f, false),
        ("d03-east-windows", new Vector3(-4.5f, 1.65f, 9f), new Vector3(7.4f, 1.1f, 9f), 75f, false),
        ("d04-west-windows", new Vector3(4.5f, 1.65f, 9f), new Vector3(-7.4f, 1.1f, 9f), 75f, false),
        ("d05-entrance", new Vector3(0f, 1.65f, 13f), new Vector3(0f, 1.0f, 0f), 75f, false),
        ("d06-drink-station-camera", new Vector3(3.073f, 1.763f, 15.335f), new Vector3(3.07f, 1.05f, 16.6f), 62f, false),
        ("d07-behind-the-counter", new Vector3(5.8f, 1.7f, 14.6f), new Vector3(-6f, .9f, 16.8f), 75f, false),
        ("d08-patio", new Vector3(3f, 1.8f, -6f), new Vector3(-3f, 0.6f, -1f), 70f, false),
        ("d09-counter-close", new Vector3(-4.8f, 1.7f, 11.6f), new Vector3(-5.6f, 1.05f, 13.8f), 60f, false),
    };

    public static string Photograph(string label)
    {
        string folder = Path.Combine(CityPackCatalog.LogRoot, "cafe-photos-" + label + "-" + Stamp());
        foreach (var view in Views)
        {
            Vector3 position = view.position, target = view.target;
            // The drink station camera moves with the station (24 Sept: it now
            // stands against the wall), so photograph from wherever it is now.
            if (view.name == "d06-drink-station-camera")
            {
                var cam = CityPackChecks.InScene<Transform>().FirstOrDefault(t => t.name == "Drink station camera");
                if (cam != null) { position = cam.position; target = cam.position + cam.forward * 1.5f; }
            }
            CafeSecondPassSteps.Capture(Path.Combine(folder, view.name + ".png"), position, target, view.fov, view.isometric);
        }
        return "Photos: " + folder;
    }

    static bool InRoom(Bounds b) => b.max.x > Room.xMin && b.min.x < Room.xMax && b.max.z > Room.yMin && b.min.z < Room.yMax;

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (var p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);
    static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);

    static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
