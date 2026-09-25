#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The drinks counter against the wall (24 Sept).
//
// WHAT WAS WRONG
//  The drink counter (the one the drink machine stands on) has stood 0.9 m out
//  from the back wall since the first layout, with a strip of bare floor behind
//  it that nobody could reach. The back bar finish filled that strip with a
//  second counter, so from the game camera the corner read as two counters, one
//  behind the other, the back one doing nothing.
//
// WHAT THIS DOES
//  1  Slides the whole drinks station back towards the wall until its front
//     lines up with the storage cabinet beside it: the drink machine with its
//     cup stack, basin, nozzles, stand point and station camera, the counter's
//     collider, its cabinet, top and brass pulls, and the bottles and mugs on
//     it. Everything moves together, so the station plays exactly as before -
//     the player stands, looks and reaches the same distances - and the staff
//     aisle in front of it gets 0.8 m wider.
//  2  Removes the extra counter that stood behind it.
//  3  Deepens the counter top and cabinet to meet the wall, and runs a thin oak
//     upstand along the wall where the top meets the tiles, so the storage
//     cabinet and the drinks counter read as one back bar under one backsplash.
//  4  Sets down the two syrup bottles that floated 3 cm above the top (they were
//     placed on the storage top when it was longer).
//  5  Re-bakes the customer routes and runs the cafe layout check, and compares
//     the stand point and the player's body there before and after.
//
// Nothing is saved: look at the photos (before and after), then save the scene
// to keep it, or run "Undo: drinks counter back where it was" (or reopen the
// scene without saving). The route bake is an asset and is re-baked by the undo.
public static class CafeDrinksCorner
{
    const string Menu = "Fixit Fidget/Cafe back bar/";
    const string Tag = "[Drinks corner] ";
    const string CafeRoot = "ACE'S CAFE - layout study 02";
    const string BackBarGroup = "17 - back bar finish";
    const string BackCounterName = "Back counter behind the drinks";
    const string AddedGroup = "Drinks counter against the wall";
    const string PullName = "Drink cabinet brass pull";
    const string UndoKey = "FixitFidget.CafeDrinksCorner.Record";

    [MenuItem(Menu + "2 - Put the drinks counter against the wall")]
    static void ApplyMenu() => Run("Against the wall", Apply);

    [MenuItem(Menu + "Photograph the drinks corner")]
    static void PhotoMenu() => Run("Photos", () => Photograph("drinks-corner"));

    [MenuItem(Menu + "Undo: drinks counter back where it was")]
    static void UndoMenu() => Run("Undo", Revert);

    [MenuItem(Menu + "2 - Put the drinks counter against the wall", true)]
    [MenuItem(Menu + "Photograph the drinks corner", true)]
    [MenuItem(Menu + "Undo: drinks counter back where it was", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string title, Func<string> step)
    {
        try { Debug.Log(Tag + title + ": " + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED (any half-done change was rolled back): " + e); }
    }

    // ------------------------------------------------------------------ apply

    static string Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the cafe scene first (" + AcesCafeLayoutSetup.ScenePath + ").");
        Transform cafe = Root(CafeRoot) ?? throw new InvalidOperationException(CafeRoot + " is missing.");
        Transform backBar = cafe.Find(BackBarGroup)
            ?? throw new InvalidOperationException(BackBarGroup + " is missing: run \"1 - Fix the counter overlap and finish the backsplash\" first.");
        if (backBar.Find(AddedGroup) != null)
            return "Already done (" + BackBarGroup + "/" + AddedGroup + " exists). Run the undo first to redo it.";

        Transform drinks = cafe.Find("04 - drinks work area") ?? throw new InvalidOperationException("04 - drinks work area is missing.");
        Transform dispenser = drinks.Find("Beverage dispenser") ?? throw new InvalidOperationException("Beverage dispenser is missing.");
        var station = dispenser.GetComponent<StationInteractable>();
        if (station == null) throw new InvalidOperationException("The dispenser has no StationInteractable.");
        Transform cabinetry = cafe.Find("Counter extensions and cabinetry") ?? throw new InvalidOperationException("Counter extensions and cabinetry is missing.");
        Transform top = cabinetry.Find("Drink countertop") ?? throw new InvalidOperationException("Drink countertop is missing.");
        Transform face = cabinetry.Find("Drink cabinet face") ?? throw new InvalidOperationException("Drink cabinet face is missing.");
        Transform storageTop = FindUnder(cafe, "CounterCabinetDetail - InteriorOak") ?? throw new InvalidOperationException("The storage cabinet's oak top is missing.");
        Transform wall = FindUnder(cafe, "Back plaster") ?? throw new InvalidOperationException("Back plaster is missing.");
        Transform tiles = backBar.Find("Subway tile backsplash") ?? throw new InvalidOperationException("The subway tile backsplash is missing.");
        Transform backCounter = backBar.Find(BackCounterName);
        var pulls = cafe.GetComponentsInChildren<Transform>(true).Where(t => t.name == PullName).ToList();

        Bounds topB = RendererBounds(top), storageB = RendererBounds(storageTop), tileB = RendererBounds(tiles);
        float wallFace = RendererBounds(wall).min.z;
        float delta = storageB.min.z - topB.min.z; // line the drink counter's front up with the storage top's
        if (delta < .3f || delta > 1.2f)
            throw new InvalidOperationException($"Unexpected distance to move ({delta:0.000} m); has the drinks counter been moved already? Nothing was changed.");
        if (topB.max.z + delta > wallFace + .001f)
            throw new InvalidOperationException("Moving the counter that far would push it into the wall; nothing was changed.");
        foreach (var t in new[] { top, face })
            if (Mathf.Abs(Vector3.Dot(t.forward, Vector3.forward)) < .99f)
                throw new InvalidOperationException(t.name + " is turned, so it can't be deepened along the wall safely; nothing was changed.");

        // Props standing on the top that belong to other groups (bottles, mugs).
        var props = PropsOn(cafe, topB, new[] { drinks, cabinetry, backBar }, pulls);
        var movers = new List<Transform> { drinks, top, face };
        movers.AddRange(pulls);
        movers.AddRange(props);
        movers = movers.Distinct().ToList();
        movers = movers.Where(m => !movers.Any(o => o != m && m.IsChildOf(o))).ToList(); // never move a child twice

        // Before: layout check, the stand point relative to the machine, the
        // player's body at the stand point, and photos.
        string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
        Vector3 standOffsetBefore = station.StandPoint != null ? station.StandPoint.position - dispenser.position : Vector3.zero;
        string bodyBefore = BodyAtStand(station);
        string photosBefore = Photograph("drinks-corner-before");

        var record = new Record { delta = delta };
        foreach (var m in movers)
            record.moved.Add(new Moved { id = GlobalObjectId.GetGlobalObjectIdSlow(m.gameObject).ToString(), position = m.position, scale = m.localScale, name = m.name });
        if (backCounter != null)
            foreach (Transform piece in backCounter)
                record.backCounter.Add(new Piece
                {
                    name = piece.name, position = piece.position, scale = piece.localScale,
                    collider = piece.GetComponent<Collider>() != null, isTop = piece.name == "Top",
                });
        EditorPrefs.SetString(UndoKey, JsonUtility.ToJson(record));

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Drinks counter against the wall");
        var notes = new StringBuilder();
        try
        {
            // 1  Everything slides back together.
            foreach (var m in movers)
            {
                Undo.RecordObject(m, "Move the drinks corner");
                m.position += Vector3.forward * delta;
                RecordPrefab(m);
            }
            Physics.SyncTransforms();
            notes.AppendLine($"Moved {delta * 100f:0.0} cm towards the wall: " + string.Join(", ", movers.Select(m => m.name)));

            // 2  The extra counter goes.
            if (backCounter != null)
            {
                Undo.DestroyObjectImmediate(backCounter.gameObject);
                notes.AppendLine("Removed " + BackBarGroup + "/" + BackCounterName);
            }

            // 3  Top and cabinet reach the wall; the fronts stay where they are.
            Bounds topNow = RendererBounds(top);
            StretchZ(top, topNow.min.z, wallFace - .002f);
            Bounds faceNow = RendererBounds(face);
            StretchZ(face, faceNow.min.z, wallFace - .01f);
            Bounds topAfter = RendererBounds(top);
            notes.AppendLine($"Counter top now z {topAfter.min.z:0.000}..{topAfter.max.z:0.000} (wall at {wallFace:0.000}); cabinet z {RendererBounds(face).min.z:0.000}..{RendererBounds(face).max.z:0.000}");

            var added = new GameObject(AddedGroup);
            Undo.RegisterCreatedObjectUndo(added, AddedGroup);
            added.transform.SetParent(backBar, false);
            added.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float surface = topAfter.max.y, tileBottom = tileB.min.y;
            if (tileBottom - surface > .004f)
            {
                Cube("Oak upstand", added.transform,
                    new Vector3(topAfter.center.x, (surface + tileBottom) * .5f, wallFace - .0125f),
                    new Vector3(topAfter.size.x, tileBottom - surface, .024f), FirstMaterial(top));
                notes.AppendLine($"Oak upstand along the wall, {(tileBottom - surface) * 100f:0.0} cm tall, closing the gap between the top and the tiles");
            }

            // 4  Props sit on the top, not above it.
            foreach (var p in props)
            {
                Bounds b = RendererBounds(p);
                float dy = surface - b.min.y;
                if (Mathf.Abs(dy) < .002f) continue;
                Undo.RecordObject(p, "Set down on the counter");
                p.position += Vector3.up * dy;
                RecordPrefab(p);
                notes.AppendLine($"{p.name} set down {-dy * 100f:0.0} cm onto the counter top");
            }

            // 5  Routes.
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            Physics.SyncTransforms();
            throw;
        }
        EditorSceneManager.MarkSceneDirty(scene);

        // After: the same checks.
        string layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
        Vector3 standOffsetAfter = station.StandPoint != null ? station.StandPoint.position - dispenser.position : Vector3.zero;
        string bodyAfter = BodyAtStand(station);
        Bounds machine = RendererBounds(dispenser.Find("Visual model") ?? dispenser);
        Transform shelf = FindUnder(cafe, "CoffeeShelf");
        var checks = new StringBuilder();
        checks.AppendLine($"Machine back to the tiles: {(tileB.min.z - machine.max.z) * 100f:0.0} cm" +
                          (shelf != null ? $"; machine lid to the coffee shelf above: {(RendererBounds(shelf).min.y - machine.max.y) * 100f:0.0} cm" : ""));
        checks.AppendLine($"Stand point relative to the machine: before {V(standOffsetBefore)}, after {V(standOffsetAfter)}" +
                          ((standOffsetAfter - standOffsetBefore).sqrMagnitude < 1e-6f ? " (unchanged)" : " (CHANGED)"));
        checks.AppendLine("Player body at the stand point: before [" + bodyBefore + "], after [" + bodyAfter + "]");
        checks.AppendLine("Layout check before: " + layoutBefore);
        checks.AppendLine("Layout check after:  " + layoutAfter);
        string photosAfter = Photograph("drinks-corner-after");
        return "\n" + notes + checks + "Photos: " + photosBefore + " and " + photosAfter +
               "\nNot saved yet: look at the photos, then save the scene to keep it.";
    }

    // Renderers resting on the counter top (bottom within 3 cm below to 6 cm
    // above the surface, centre over the top) that belong to other groups,
    // resolved to the smallest whole prop that contains them.
    static List<Transform> PropsOn(Transform cafe, Bounds top, Transform[] skip, List<Transform> pulls)
    {
        var found = new List<Transform>();
        foreach (var r in cafe.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            Transform t = r.transform;
            if (skip.Any(s => t.IsChildOf(s)) || pulls.Contains(t)) continue;
            Bounds b = r.bounds;
            if (b.size.x > 1.2f || b.size.z > 1.2f) continue;
            if (b.center.x < top.min.x + .01f || b.center.x > top.max.x - .01f || b.center.z < top.min.z + .01f || b.center.z > top.max.z - .01f) continue;
            if (b.min.y < top.max.y - .03f || b.min.y > top.max.y + .06f) continue;
            Transform prop = t;
            if (PrefabUtility.IsPartOfPrefabInstance(t))
            {
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (outer != null && outer.transform.IsChildOf(cafe))
                {
                    Bounds whole = RendererBounds(outer.transform);
                    if (whole.size.x <= 1.2f && whole.size.z <= 1.2f) prop = outer.transform;
                }
            }
            if (!found.Contains(prop)) found.Add(prop);
        }
        return found;
    }

    // Scales a transform along its local z (aligned with world z) so its
    // renderer bounds span [newMin, newMax] in world z.
    static void StretchZ(Transform t, float newMin, float newMax)
    {
        Undo.RecordObject(t, "Deepen " + t.name);
        Bounds current = RendererBounds(t);
        float factor = (newMax - newMin) / Mathf.Max(1e-4f, current.size.z);
        Vector3 s = t.localScale;
        t.localScale = new Vector3(s.x, s.y, s.z * factor);
        Bounds after = RendererBounds(t);
        t.position += Vector3.forward * (newMin - after.min.z);
        RecordPrefab(t);
    }

    // Non-trigger colliders the player's capsule would touch standing at the
    // drinks station's stand point (the player itself excluded).
    static string BodyAtStand(StationInteractable station)
    {
        if (station.StandPoint == null) return "no stand point";
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        var cc = player != null ? player.GetComponent<CharacterController>() : null;
        if (cc == null) return "no player controller";
        Physics.SyncTransforms();
        float radius = cc.radius * Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x), Mathf.Abs(cc.transform.lossyScale.z));
        float height = Mathf.Max(cc.height * Mathf.Abs(cc.transform.lossyScale.y), radius * 2f);
        Vector3 foot = station.StandPoint.position;
        foot.y = 0f;
        var hits = Physics.OverlapCapsule(foot + Vector3.up * (radius + .06f), foot + Vector3.up * (height - radius), radius, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c.GetComponentInParent<PlayerMovement>() == null && c.name != "Floor")
            .Select(c => c.name).Distinct().ToArray();
        bool onMesh = NavMesh.SamplePosition(station.StandPoint.position, out NavMeshHit hit, .22f, NavMesh.AllAreas);
        return (hits.Length == 0 ? "touches nothing" : "touches " + string.Join(", ", hits)) + (onMesh ? "; on the routes" : "; OFF the routes");
    }

    // ------------------------------------------------------------------ undo

    [Serializable] class Moved { public string id, name; public Vector3 position, scale; }
    [Serializable] class Piece { public string name; public Vector3 position, scale; public bool collider, isTop; }
    [Serializable]
    class Record
    {
        public float delta;
        public List<Moved> moved = new List<Moved>();
        public List<Piece> backCounter = new List<Piece>();
    }

    static string Revert()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath) throw new InvalidOperationException("Open the cafe scene first.");
        Transform cafe = Root(CafeRoot) ?? throw new InvalidOperationException(CafeRoot + " is missing.");
        Transform backBar = cafe.Find(BackBarGroup) ?? throw new InvalidOperationException(BackBarGroup + " is missing.");
        Transform added = backBar.Find(AddedGroup);
        if (added == null) return "Nothing to undo: the drinks counter was not moved by this tool.";
        string json = EditorPrefs.GetString(UndoKey, "");
        if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("The original positions were not recorded on this computer; reopen the scene without saving instead.");
        var record = JsonUtility.FromJson<Record>(json);
        Transform cabinetry = cafe.Find("Counter extensions and cabinetry");
        Transform top = cabinetry != null ? cabinetry.Find("Drink countertop") : null;
        Transform face = cabinetry != null ? cabinetry.Find("Drink cabinet face") : null;
        // Resolve everything first so a missing object leaves nothing half undone.
        var targets = new List<(Transform t, Moved m)>();
        foreach (var m in record.moved)
        {
            if (!GlobalObjectId.TryParse(m.id, out var id) || !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is GameObject go))
                throw new InvalidOperationException("Can't find " + m.name + " any more; reopen the scene without saving instead.");
            targets.Add((go.transform, m));
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Drinks counter back where it was");
        try
        {
            foreach (var (t, m) in targets)
            {
                Undo.RecordObject(t, "Restore " + t.name);
                t.localScale = m.scale;
                t.position = m.position;
                RecordPrefab(t);
            }
            Undo.DestroyObjectImmediate(added.gameObject);
            if (record.backCounter.Count > 0 && top != null && face != null)
            {
                var counter = new GameObject(BackCounterName);
                Undo.RegisterCreatedObjectUndo(counter, BackCounterName);
                counter.transform.SetParent(backBar, false);
                foreach (var p in record.backCounter)
                {
                    var piece = Cube(p.name, counter.transform, p.position, p.scale, FirstMaterial(p.isTop ? top : face));
                    if (p.collider) piece.AddComponent<BoxCollider>();
                }
            }
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return "Drinks counter put back where it was (" + record.moved.Count + " objects), back counter rebuilt, routes re-baked. "
               + AcesCafeLayoutSetup.ValidateLayout() + " Save the scene to keep that.";
    }

    // ------------------------------------------------------------------ photos

    public static string Photograph(string prefix)
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", prefix + "-" + stamp);
        Directory.CreateDirectory(folder);
        var hidden = new List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (var r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try
        {
            GameObject shop = GameObject.Find("CmShopCam");
            if (shop != null)
                CafeSecondPassSteps.Capture(Path.Combine(folder, "1-game-camera.png"), shop.transform.position,
                    shop.transform.position + shop.transform.forward * 30f, 39f, true);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "2-from-the-front-counter.png"), new Vector3(3.1f, 1.65f, 13.2f), new Vector3(3.1f, 1.15f, 18f), 64f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "3-front-left.png"), new Vector3(-0.8f, 1.75f, 13.4f), new Vector3(2.9f, 1.0f, 17.5f), 60f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "4-front-right.png"), new Vector3(6.6f, 1.75f, 13.6f), new Vector3(2.2f, 1.0f, 17.5f), 60f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "5-right-end.png"), new Vector3(6.7f, 1.45f, 16.2f), new Vector3(4.2f, 0.85f, 17.6f), 60f, false);
            var stationCam = Object.FindObjectsByType<CinemachineCamera>().FirstOrDefault(c => c.name == "Drink station camera");
            if (stationCam != null)
                CafeSecondPassSteps.Capture(Path.Combine(folder, "6-station-camera.png"), stationCam.transform.position,
                    stationCam.transform.position + stationCam.transform.forward * 3f, stationCam.Lens.FieldOfView, false);
            Plan(Path.Combine(folder, "7-plan.png"), Rect.MinMaxRect(-2.6f, 12.8f, 7.6f, 18.3f), 2.0f);
        }
        finally { foreach (var r in hidden) if (r != null) r.forceRenderingOff = false; }
        return folder;
    }

    // Orthographic plan of 'area' (x, z) cut at 'cut' metres, 80 px per metre.
    static void Plan(string path, Rect area, float cut)
    {
        const int ppm = 80;
        int w = Mathf.RoundToInt(area.width * ppm), h = Mathf.RoundToInt(area.height * ppm);
        var go = new GameObject("Temporary plan camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.orthographic = true;
            cam.orthographicSize = area.height * .5f;
            cam.aspect = area.width / area.height;
            cam.transform.SetPositionAndRotation(new Vector3(area.center.x, cut, area.center.y), Quaternion.Euler(90f, 0f, 0f));
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
    }

    // ------------------------------------------------------------------ helpers

    static GameObject Cube(string name, Transform parent, Vector3 centre, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, Quaternion.identity);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void RecordPrefab(Transform t)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    static Transform Root(string name) =>
        SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == name);

    static Transform FindUnder(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static Bounds RendererBounds(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException(t.name + " has no renderers to measure.");
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static Material FirstMaterial(Transform t) =>
        t.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);

    static string V(Vector3 v) => "(" + v.x.ToString("0.000", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.000", CultureInfo.InvariantCulture) + ", " + v.z.ToString("0.000", CultureInfo.InvariantCulture) + ")";
}
#endif
