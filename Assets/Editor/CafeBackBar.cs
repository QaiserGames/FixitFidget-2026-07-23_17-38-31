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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The back bar, finished (24 Sept).
//
// WHAT WAS WRONG
//  * The middle storage cabinet (against the back wall, x -1.95..1.95) ran
//    40 cm into the drink counter that holds the drink machine (x 1.575..4.625,
//    which stands 0.9 m out from the wall). Their tops and cabinet faces passed
//    through each other at the corner, so the two counters looked melted
//    together.
//  * The tiled backsplash was a single 2.9 m patch floating 10 cm off the wall
//    at the storage's right end, behind nothing in particular.
//  * Behind the drink counter was a 0.9 m strip of bare floor against the wall.
//
// WHAT THIS DOES (decor only: gameplay objects, anchors and navigation are untouched)
//  1  Shortens the storage cabinet and its oak top so they stop 1 cm short of
//     the drink counter (the left edge stays where it was), and slides the
//     delivery box on it back onto the shortened top.
//  2  Builds a back counter along the wall behind the drink counter in the
//     same joinery as the drink counter, so the back wall reads as one
//     continuous back bar. It gets a box collider, like the other counters, so
//     the first-person player can't walk into it.
//  3  Closes the gap between the storage top and the wall with a strip of the
//     same top, and tiles the wall from counter height to 48 cm above it across
//     the whole back bar: subway tiles in a running bond, grout lines showing
//     the plaster behind, an oak cap on top and oak edge strips at both ends.
//     The old floating patch is hidden, not deleted.
//
// Nothing is saved: look at the photos, then save the scene (Ctrl+S) to keep it,
// or run "Undo the back bar finish" (or reopen the scene without saving).
public static class CafeBackBar
{
    const string Menu = "Fixit Fidget/Cafe back bar/";
    const string Tag = "[Back bar] ";
    const string GroupName = "17 - back bar finish";
    const string CafeRoot = "ACE'S CAFE - layout study 02";
    const string MeshFolder = "Assets/Art/CafeBackBar";
    const string TileMeshPath = MeshFolder + "/Back bar subway tiles.asset";
    const string UndoKey = "FixitFidget.CafeBackBar.Original";

    // Subway tiles: 15 x 7.5 cm, 5 mm joints, 6 mm proud of the wall.
    const float TileWidth = .15f, TileHeight = .075f, Joint = .005f, TileDepth = .006f;
    const int Rows = 6;

    [MenuItem(Menu + "1 - Fix the counter overlap and finish the backsplash")]
    static void ApplyMenu() => Run("Finish", Apply);

    [MenuItem(Menu + "Photograph the back bar")]
    static void PhotoMenu() => Run("Photos", () => Photograph("back-bar"));

    [MenuItem(Menu + "Undo the back bar finish")]
    static void UndoMenu() => Run("Undo", Revert);

    [MenuItem(Menu + "1 - Fix the counter overlap and finish the backsplash", true)]
    [MenuItem(Menu + "Photograph the back bar", true)]
    [MenuItem(Menu + "Undo the back bar finish", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string title, Func<string> step)
    {
        try { Debug.Log(Tag + title + ": " + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED (nothing half-done was kept if this was the finish step): " + e); }
    }

    // ------------------------------------------------------------------ finish

    static string Apply()
    {
        Transform cafe = Find(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first (" + CafeRoot + " is missing).");
        if (cafe.Find(GroupName) != null) return "Already finished (" + GroupName + " exists). Run \"Undo the back bar finish\" first to redo it.";

        Transform cabinetry = cafe.Find("Counter extensions and cabinetry") ?? throw new InvalidOperationException("Counter extensions and cabinetry is missing.");
        Transform storage = cabinetry.Find("Rear storage") ?? throw new InvalidOperationException("Rear storage is missing.");
        Transform drinkTop = cabinetry.Find("Drink countertop") ?? throw new InvalidOperationException("Drink countertop is missing.");
        Transform drinkFace = cabinetry.Find("Drink cabinet face") ?? throw new InvalidOperationException("Drink cabinet face is missing.");
        Transform detail = FindUnder(cafe, "CounterCabinetDetail") ?? throw new InvalidOperationException("CounterCabinetDetail (the storage's oak top and doors) is missing.");
        Transform splash = FindUnder(cafe, "RearTileSplash") ?? throw new InvalidOperationException("RearTileSplash is missing.");
        Transform box = FindUnder(cafe, "Drinks - Cardboard_Box_04");
        Transform wall = FindUnder(cafe, "Back plaster") ?? throw new InvalidOperationException("Back plaster is missing.");

        Bounds storageBounds = RendererBounds(storage), detailBounds = RendererBounds(detail), drinkBounds = RendererBounds(drinkTop);
        Bounds wallBounds = RendererBounds(wall);
        float wallFace = wallBounds.min.z;                       // 18.0
        float drinkLeft = drinkBounds.min.x;                     // 1.575
        float drinkBack = drinkBounds.max.z;                     // 17.075
        float topHeight = Mathf.Max(detailBounds.max.y, drinkBounds.max.y); // ~1.0
        if (drinkLeft > storageBounds.max.x) return "The storage already stops before the drink counter; nothing to fix.";
        // Both pieces are resized along world x; refuse (before touching anything) if either is turned.
        foreach (var t in new[] { storage, detail })
            if (Mathf.Abs(Vector3.Dot(t.right, Vector3.right)) < .99f)
                throw new InvalidOperationException(t.name + " is rotated, so it can't be resized along the wall safely; nothing was changed.");

        var tileMaterial = FirstMaterial(splash) ?? throw new InvalidOperationException("RearTileSplash has no material.");
        var topMaterial = FirstMaterial(drinkTop) ?? throw new InvalidOperationException("Drink countertop has no material.");
        var bodyMaterial = FirstMaterial(drinkFace) ?? throw new InvalidOperationException("Drink cabinet face has no material.");

        // Remember the originals for "Undo the back bar finish".
        var original = new Record
        {
            storagePosition = storage.localPosition, storageScale = storage.localScale,
            detailPosition = detail.localPosition, detailScale = detail.localScale,
            boxPosition = box != null ? box.position : Vector3.zero, hasBox = box != null,
        };
        EditorPrefs.SetString(UndoKey, JsonUtility.ToJson(original));

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Finish the back bar");
        var notes = new StringBuilder();

        // 1  Storage and its oak top end 1 cm short of the drink counter; left edges stay.
        float newRight = drinkLeft - .01f;
        ScaleAlongX(storage, storageBounds, storageBounds.min.x, newRight - .025f);
        ScaleAlongX(detail, detailBounds, detailBounds.min.x, newRight);
        notes.AppendLine($"Storage cabinet {storageBounds.min.x:0.00}..{storageBounds.max.x:0.00} -> {storageBounds.min.x:0.00}..{newRight - .025f:0.00}; " +
                         $"its top {detailBounds.min.x:0.00}..{detailBounds.max.x:0.00} -> {detailBounds.min.x:0.00}..{newRight:0.00} (drink counter starts at {drinkLeft:0.000})");
        if (box != null)
        {
            Bounds b = RendererBounds(box);
            float shift = Mathf.Min(0f, newRight - .1f - b.max.x);
            if (shift < 0f)
            {
                Undo.RecordObject(box, "Move delivery box");
                box.position += Vector3.right * shift;
                notes.AppendLine($"Delivery box moved {shift * 100f:0} cm to stay on the shortened top");
            }
        }

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Back bar group");
        group.transform.SetParent(cafe, false);
        group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 2  Back counter behind the drink counter: body + top, wall to 11 cm behind the drink counter.
        //    Superseded (24 Sept): "2 - Put the drinks counter against the wall" moves the drink
        //    counter itself to the wall and removes this piece. Once the drink counter stands
        //    against the wall there is no strip to fill, so nothing is built.
        float backFront = drinkBack + .11f, backLeft = drinkLeft, backRight = drinkBounds.max.x;
        if (wallFace - backFront > .25f)
        {
            var backCounter = new GameObject("Back counter behind the drinks");
            Undo.RegisterCreatedObjectUndo(backCounter, "Back counter");
            backCounter.transform.SetParent(group.transform, false);
            float bodyTop = topHeight - .04f;
            Cube("Body", backCounter.transform, new Vector3((backLeft + backRight) * .5f, bodyTop * .5f, (backFront + wallFace) * .5f),
                 new Vector3(backRight - backLeft - .03f, bodyTop, wallFace - backFront - .02f), bodyMaterial, collider: true);
            Cube("Top", backCounter.transform, new Vector3((backLeft + backRight) * .5f, bodyTop + .02f, (backFront - .02f + wallFace) * .5f),
                 new Vector3(backRight - backLeft, .04f, wallFace - backFront + .02f), topMaterial, collider: false);
            notes.AppendLine($"Back counter x {backLeft:0.00}..{backRight:0.00}, z {backFront:0.00}..{wallFace:0.00}, top at {topHeight:0.00} m (collider on the body)");
        }
        else notes.AppendLine("The drink counter already stands against the wall; no back counter needed");

        // 3  Top strip closing the gap between the storage top and the wall.
        float storageBack = detailBounds.max.z;
        if (wallFace - storageBack > .005f)
            Cube("Storage top to the wall", group.transform,
                 new Vector3((detailBounds.min.x + newRight) * .5f, topHeight - .015f, (storageBack + wallFace) * .5f),
                 new Vector3(newRight - detailBounds.min.x, .03f, wallFace - storageBack + .01f), topMaterial, collider: false);

        // 4  Tiles from counter height across the back bar, cap and end strips.
        float left = detailBounds.min.x, right = backRight, bottom = topHeight;
        float height = Rows * (TileHeight + Joint);
        var tiles = new GameObject("Subway tile backsplash");
        Undo.RegisterCreatedObjectUndo(tiles, "Backsplash");
        tiles.transform.SetParent(group.transform, false);
        tiles.transform.position = new Vector3(left, bottom, wallFace);
        Mesh mesh = TileMesh(right - left, Rows);
        tiles.AddComponent<MeshFilter>().sharedMesh = mesh;
        var tileRenderer = tiles.AddComponent<MeshRenderer>();
        tileRenderer.sharedMaterial = tileMaterial;
        Cube("Oak cap", group.transform, new Vector3((left + right) * .5f, bottom + height + .0125f, wallFace - .0125f),
             new Vector3(right - left + .02f, .025f, .025f), topMaterial, collider: false);
        Cube("Oak edge left", group.transform, new Vector3(left - .006f, bottom + height * .5f, wallFace - .008f),
             new Vector3(.012f, height, .016f), topMaterial, collider: false);
        Cube("Oak edge right", group.transform, new Vector3(right + .006f, bottom + height * .5f, wallFace - .008f),
             new Vector3(.012f, height, .016f), topMaterial, collider: false);
        notes.AppendLine($"Backsplash x {left:0.00}..{right:0.00}, y {bottom:0.00}..{bottom + height:0.00} on the wall at z {wallFace:0.00}: " +
                         $"{mesh.vertexCount / 20} tiles, oak cap and edges");

        // The old floating patch goes dark (kept, so undo is exact).
        foreach (var r in splash.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(r, "Hide old splash");
            r.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        notes.AppendLine("Old tile patch hidden: " + PathOf(splash));

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string photos = Photograph("back-bar-finished");
        return "\n" + notes + "Photos: " + photos + "\nNot saved yet: look at the photos, then Ctrl+S to keep it.";
    }

    // Moves and scales a transform along world x so the renderer bounds span [newMin, newMax].
    static void ScaleAlongX(Transform t, Bounds current, float newMin, float newMax)
    {
        Undo.RecordObject(t, "Resize " + t.name);
        float factor = (newMax - newMin) / Mathf.Max(1e-4f, current.size.x);
        Vector3 scale = t.localScale;
        t.localScale = new Vector3(scale.x * factor, scale.y, scale.z);
        Bounds after = RendererBounds(t);
        t.position += Vector3.right * (newMin - after.min.x);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    static GameObject Cube(string name, Transform parent, Vector3 centre, Vector3 size, Material material, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, Quaternion.identity);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // Running-bond subway tiles, local origin at the bottom-left corner on the
    // wall face, tiles standing out towards -z (into the room). Each tile is a
    // front face plus four thin edges so the joints catch the light; the joints
    // themselves show the wall behind. Saved as an asset so the scene stays small.
    static Mesh TileMesh(float width, int rows)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        float pitchX = TileWidth + Joint, pitchY = TileHeight + Joint;
        for (int row = 0; row < rows; row++)
        {
            float y0 = row * pitchY + Joint * .5f, y1 = y0 + TileHeight;
            float start = (row % 2 == 0 ? 0f : -pitchX * .5f) + Joint * .5f;
            for (float x = start; x < width; x += pitchX)
            {
                float x0 = Mathf.Max(x, Joint * .5f), x1 = Mathf.Min(x + TileWidth, width - Joint * .5f);
                if (x1 - x0 < .01f) continue;
                AddTile(vertices, normals, uvs, triangles, x0, x1, y0, y1, width, rows * pitchY);
            }
        }
        var mesh = new Mesh { name = "Back bar subway tiles" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        if (!AssetDatabase.IsValidFolder(MeshFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            AssetDatabase.CreateFolder("Assets/Art", "CafeBackBar");
        }
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TileMeshPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, TileMeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static void AddTile(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, float x0, float x1, float y0, float y1, float width, float height)
    {
        float z = -TileDepth;
        // Front, left, right, top, bottom (4 vertices each), clockwise when seen from -z (Unity front faces).
        Quad(v, n, uv, t, new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), Vector3.back, width, height);
        Quad(v, n, uv, t, new Vector3(x0, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, z), new Vector3(x0, y0, z), Vector3.left, width, height);
        Quad(v, n, uv, t, new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x1, y1, 0f), new Vector3(x1, y0, 0f), Vector3.right, width, height);
        Quad(v, n, uv, t, new Vector3(x0, y1, z), new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, z), Vector3.up, width, height);
        Quad(v, n, uv, t, new Vector3(x0, y0, 0f), new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y0, 0f), Vector3.down, width, height);
    }

    static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float width, float height)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int k = 0; k < 4; k++) n.Add(normal);
        foreach (var p in new[] { a, b, c, d }) uv.Add(new Vector2(p.x / Mathf.Max(width, 1e-3f), p.y / Mathf.Max(height, 1e-3f)));
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    // ------------------------------------------------------------------ undo

    [Serializable]
    class Record
    {
        public Vector3 storagePosition, storageScale, detailPosition, detailScale, boxPosition;
        public bool hasBox;
    }

    static string Revert()
    {
        Transform cafe = Find(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first.");
        Transform group = cafe.Find(GroupName);
        if (group == null) return "Nothing to undo: " + GroupName + " is not in the scene.";
        string json = EditorPrefs.GetString(UndoKey, "");
        if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("The original sizes were not recorded on this computer; reopen the scene without saving instead.");
        var original = JsonUtility.FromJson<Record>(json);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Undo the back bar finish");
        Transform cabinetry = cafe.Find("Counter extensions and cabinetry");
        Transform storage = cabinetry != null ? cabinetry.Find("Rear storage") : null;
        Transform detail = FindUnder(cafe, "CounterCabinetDetail");
        Transform splash = FindUnder(cafe, "RearTileSplash");
        Transform box = FindUnder(cafe, "Drinks - Cardboard_Box_04");
        if (storage != null) { Undo.RecordObject(storage, "Restore storage"); storage.localPosition = original.storagePosition; storage.localScale = original.storageScale; }
        if (detail != null)
        {
            Undo.RecordObject(detail, "Restore storage top");
            detail.localPosition = original.detailPosition; detail.localScale = original.detailScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(detail);
        }
        if (box != null && original.hasBox) { Undo.RecordObject(box, "Restore box"); box.position = original.boxPosition; }
        if (splash != null)
            foreach (var r in splash.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(r, "Show old splash");
                r.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        Undo.DestroyObjectImmediate(group.gameObject);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Back bar put back as it was. Save the scene to keep that.";
    }

    // ------------------------------------------------------------------ photos

    static string Photograph(string prefix)
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
            CafeSecondPassSteps.Capture(Path.Combine(folder, "1-front-left.png"), new Vector3(-3.6f, 2.5f, 12.6f), new Vector3(1.6f, 1.0f, 17.2f), 58f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "2-front-right.png"), new Vector3(5.2f, 2.5f, 12.8f), new Vector3(-0.6f, 1.0f, 17.3f), 58f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "3-junction-close.png"), new Vector3(0.6f, 1.75f, 15.1f), new Vector3(1.8f, 1.0f, 17.1f), 55f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "4-straight-on.png"), new Vector3(0f, 1.75f, 12.4f), new Vector3(0f, 1.25f, 18f), 64f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "5-drink-counter-back.png"), new Vector3(5.6f, 1.9f, 17.6f), new Vector3(1.5f, 0.9f, 16.6f), 60f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "6-tiles-close.png"), new Vector3(-0.2f, 1.45f, 16.3f), new Vector3(-0.9f, 1.2f, 18f), 50f, false);
            GameObject shop = GameObject.Find("CmShopCam");
            if (shop != null)
                CafeSecondPassSteps.Capture(Path.Combine(folder, "7-game-camera.png"), shop.transform.position,
                    shop.transform.position + shop.transform.forward * 30f, 39f, true);
        }
        finally { foreach (var r in hidden) if (r != null) r.forceRenderingOff = false; }
        return folder;
    }

    // ------------------------------------------------------------------ helpers

    static Transform Find(string name) =>
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

    static string PathOf(Transform t)
    {
        string result = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) result = p.name + "/" + result;
        return result;
    }
}
#endif
