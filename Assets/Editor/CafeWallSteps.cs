#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Scene steps for the café walls (27 Sept; Mansoor: "the wall with the painting
// doesn't have a glass side", and the back and tan walls vanish when the camera
// turns, which CafeViewMode now answers by sliding them down to sill height).
//
//  * Glaze the painting wall: one pane of the café's own window glass (the same
//    material and settings as the street-side windows) across the courtyard
//    windows' open bays. It sits inside the posts and piers, so it only shows
//    in the openings. The two paintings stay on their plaster piers. No collider:
//    the wall's existing collider still keeps people in, and sightlines pass.
//
//  * Give the back-bar cup shelf its own mesh. The V3 pass merged the shelf, its
//    brackets and its six mugs into the same meshes as the café's front sign and
//    the banquette books, so the shelf couldn't hide when the back wall slides
//    down, and would float. The merged meshes are split in two: the part inside
//    the shelf's box becomes "Back bar cup shelf"; the rest keeps its place.
//    Nothing moves or changes colour. The new meshes are saved in
//    Assets/Playtests/AcesCafeLayout/Back bar cup shelf (own mesh).asset.
//
// Each step has an undo menu below it, and Edit > Undo works too. Save the
// scene afterwards (Ctrl+S).
// ---------------------------------------------------------------------------
public static class CafeWallSteps
{
    const string Menu = "Fixit Fidget/Neighborhood refresh/Second pass/";
    const string Tag = "[Cafe walls] ";
    const string Folder = "Assets/Playtests/AcesCafeLayout";
    const string StreetGeometry = Folder + "/Street geometry.asset";
    const string SplitAsset = Folder + "/Back bar cup shelf (own mesh).asset";
    const string WindowsGroup = "V3 - courtyard-facing cafe windows";
    const string DetailsGroup = "V3 - cafe perimeter details";
    const string GlassName = "Courtyard window glass";
    const string GlassReference = "StreetJoinery - InteriorGlass";
    const string ShelfName = "Back bar cup shelf";
    const string WithoutSuffix = " (without the cup shelf)";

    // The glass: on the wall's centre line, from the sill top (0.78) to the
    // header (2.88), from inside the corner post (z 0) to inside the repair-end
    // pier (which starts at z 16.375).
    static readonly Vector3 GlassCentre = new(7.52f, 1.83f, 8.225f);
    static readonly Vector3 GlassSize = new(.012f, 2.10f, 16.45f);

    // Everything of the cup shelf: the board (x -1.03..1.23, y 1.38..1.48),
    // brackets and wall mounts (y 1.18..1.59), mugs (y 1.47..1.62), all at
    // z 17.64..17.96. The coffee-bean jars on the counter (z 17.32) stay out.
    static readonly Bounds ShelfBox = new(new Vector3(.1f, 1.43f, 17.775f), new Vector3(2.7f, .66f, .55f));

    // ---------------- the glass ----------------

    [MenuItem(Menu + "Glaze the painting wall (courtyard windows)")]
    static void Glaze()
    {
        try
        {
            RequireScene();
            Transform windows = Find(WindowsGroup);
            if (windows.Find(GlassName) != null) { Debug.Log(Tag + "The painting wall is already glazed."); return; }
            Renderer reference = SceneRenderers().FirstOrDefault(r => r.name == GlassReference);
            if (reference == null) throw new InvalidOperationException($"The street-side window glass ({GlassReference}) is missing, so there is nothing to match.");
            if ((windows.lossyScale - Vector3.one).sqrMagnitude > 1e-6f || Quaternion.Angle(windows.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException($"{WindowsGroup} has been moved, turned or scaled; the glass would not line up.");

            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.name = GlassName;
            glass.layer = reference.gameObject.layer;
            glass.transform.SetParent(windows, false);
            glass.transform.SetPositionAndRotation(GlassCentre, Quaternion.identity);
            glass.transform.localScale = GlassSize;
            var renderer = glass.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = reference.sharedMaterials;
            renderer.shadowCastingMode = reference.shadowCastingMode;
            renderer.receiveShadows = reference.receiveShadows;
            renderer.lightProbeUsage = reference.lightProbeUsage;
            renderer.reflectionProbeUsage = reference.reflectionProbeUsage;
            renderer.renderingLayerMask = reference.renderingLayerMask;
            GameObjectUtility.SetStaticEditorFlags(glass, GameObjectUtility.GetStaticEditorFlags(reference.gameObject));
            Undo.RegisterCreatedObjectUndo(glass, "Glaze the painting wall");
            EditorSceneManager.MarkSceneDirty(glass.scene);
            Selection.activeGameObject = glass;
            Debug.Log(Tag + $"Glazed the painting wall: {GlassName}, {GlassSize.y:0.00} m tall and {GlassSize.z:0.00} m long, " +
                      $"material {string.Join(", ", reference.sharedMaterials.Where(m => m != null).Select(m => m.name))}, " +
                      $"shadows {reference.shadowCastingMode}. The paintings stay on their piers. Save the scene (Ctrl+S).");
        }
        catch (Exception e) { Debug.LogError(Tag + "Glazing FAILED: " + e.Message); }
    }

    [MenuItem(Menu + "Take the glass out of the painting wall")]
    static void Unglaze()
    {
        try
        {
            RequireScene();
            Transform glass = Find(WindowsGroup).Find(GlassName);
            if (glass == null) { Debug.Log(Tag + "The painting wall has no added glass."); return; }
            Scene scene = glass.gameObject.scene;
            Undo.DestroyObjectImmediate(glass.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(Tag + "Took the added glass out of the painting wall. Save the scene (Ctrl+S).");
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    // ---------------- the cup shelf ----------------

    [MenuItem(Menu + "Give the back-bar cup shelf its own mesh (so it hides with the back wall)")]
    static void SplitShelf()
    {
        try
        {
            RequireScene();
            Transform details = Find(DetailsGroup);
            if (details.Find(ShelfName) != null) { Debug.Log(Tag + "The cup shelf already has its own mesh."); return; }
            MeshFilter[] merged = details.GetComponentsInChildren<MeshFilter>(true).Where(f => f.transform.parent == details).ToArray();
            foreach (MeshFilter f in merged)
                if (f.transform.localPosition.sqrMagnitude > 1e-8f || f.transform.localRotation != Quaternion.identity || f.transform.localScale != Vector3.one)
                    throw new InvalidOperationException($"{f.name} has been moved inside {DetailsGroup}; nothing was changed.");

            // Split every merged mesh that has part of the shelf in it.
            var pieces = new List<(MeshFilter filter, Mesh shelf, Mesh rest)>();
            foreach (MeshFilter filter in merged)
            {
                Mesh source = filter.sharedMesh;
                if (source == null || filter.GetComponent<MeshRenderer>() == null) continue;
                if (!source.isReadable) throw new InvalidOperationException($"{source.name} can't be read; nothing was changed.");
                if (!Split(source, filter.transform, out Mesh shelf, out Mesh rest)) continue;
                shelf.name = ShelfName + " - " + filter.name;
                rest.name = source.name + WithoutSuffix;
                pieces.Add((filter, shelf, rest));
            }
            if (pieces.Count == 0) throw new InvalidOperationException("No part of the cup shelf was found in the merged meshes; nothing was changed.");

            // Save the new meshes (reusing ones from an earlier run, never deleting).
            for (int i = 0; i < pieces.Count; i++)
                pieces[i] = (pieces[i].filter, Keep(pieces[i].shelf), Keep(pieces[i].rest));
            AssetDatabase.SaveAssets();

            var shelfGroup = new GameObject(ShelfName);
            shelfGroup.transform.SetParent(details, false);
            shelfGroup.layer = details.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(shelfGroup, "Give the cup shelf its own mesh");
            int triangles = 0;
            foreach ((MeshFilter filter, Mesh shelf, Mesh rest) in pieces)
            {
                var source = filter.GetComponent<MeshRenderer>();
                var part = new GameObject(shelf.name) { layer = filter.gameObject.layer };
                part.transform.SetParent(shelfGroup.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = shelf;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.lightProbeUsage = source.lightProbeUsage;
                renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                renderer.renderingLayerMask = source.renderingLayerMask;
                GameObjectUtility.SetStaticEditorFlags(part, GameObjectUtility.GetStaticEditorFlags(filter.gameObject));
                Undo.RegisterCreatedObjectUndo(part, "Give the cup shelf its own mesh");
                Undo.RecordObject(filter, "Give the cup shelf its own mesh");
                filter.sharedMesh = rest;
                if (PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                triangles += shelf.triangles.Length / 3;
            }
            EditorSceneManager.MarkSceneDirty(details.gameObject.scene);
            Debug.Log(Tag + $"The cup shelf has its own mesh: {pieces.Count} parts ({string.Join(", ", pieces.Select(p => p.filter.name))}), " +
                      $"{triangles} triangles, under {DetailsGroup}/{ShelfName}. Everything else stays in the merged meshes. Save the scene (Ctrl+S).");
        }
        catch (Exception e) { Debug.LogError(Tag + "Cup shelf FAILED: " + e.Message); }
    }

    [MenuItem(Menu + "Put the cup shelf back into the merged meshes")]
    static void MergeShelf()
    {
        try
        {
            RequireScene();
            Transform details = Find(DetailsGroup);
            Mesh[] originals = AssetDatabase.LoadAllAssetsAtPath(StreetGeometry).OfType<Mesh>().ToArray();
            int restored = 0;
            foreach (MeshFilter filter in details.GetComponentsInChildren<MeshFilter>(true).Where(f => f.transform.parent == details))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.name.EndsWith(WithoutSuffix, StringComparison.Ordinal)) continue;
                string name = mesh.name.Substring(0, mesh.name.Length - WithoutSuffix.Length);
                Mesh original = originals.FirstOrDefault(m => m.name == name);
                if (original == null) throw new InvalidOperationException($"The original merged mesh \"{name}\" is missing from {StreetGeometry}.");
                Undo.RecordObject(filter, "Put the cup shelf back");
                filter.sharedMesh = original;
                if (PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                restored++;
            }
            Transform shelf = details.Find(ShelfName);
            if (shelf != null) Undo.DestroyObjectImmediate(shelf.gameObject);
            EditorSceneManager.MarkSceneDirty(details.gameObject.scene);
            Debug.Log(Tag + $"Put the cup shelf back: {restored} merged meshes restored{(shelf != null ? ", the separate shelf removed" : "")}. Save the scene (Ctrl+S).");
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    [MenuItem(Menu + "Glaze the painting wall (courtyard windows)", true)]
    [MenuItem(Menu + "Take the glass out of the painting wall", true)]
    [MenuItem(Menu + "Give the back-bar cup shelf its own mesh (so it hides with the back wall)", true)]
    [MenuItem(Menu + "Put the cup shelf back into the merged meshes", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Triangles whose centre (in the world) is inside the shelf's box go to
    // "shelf", the rest to "rest". False when none of them is in the box.
    static bool Split(Mesh source, Transform space, out Mesh shelf, out Mesh rest)
    {
        shelf = rest = null;
        Vector3[] vertices = source.vertices;
        int subMeshes = source.subMeshCount;
        var inside = new List<int>[subMeshes];
        var outside = new List<int>[subMeshes];
        int found = 0;
        for (int s = 0; s < subMeshes; s++)
        {
            inside[s] = new List<int>();
            outside[s] = new List<int>();
            int[] triangles = source.GetTriangles(s);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 centre = space.TransformPoint((vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f);
                List<int> into = ShelfBox.Contains(centre) ? inside[s] : outside[s];
                into.Add(triangles[t]); into.Add(triangles[t + 1]); into.Add(triangles[t + 2]);
                if (into == inside[s]) found++;
            }
        }
        if (found == 0) return false;
        shelf = Build(source, inside);
        rest = Build(source, outside);
        return true;
    }

    // A mesh with only the given triangles and the vertices they use.
    static Mesh Build(Mesh source, List<int>[] triangles)
    {
        var map = new Dictionary<int, int>();
        var order = new List<int>();
        foreach (List<int> list in triangles)
            foreach (int index in list)
                if (!map.ContainsKey(index)) { map[index] = order.Count; order.Add(index); }
        var mesh = new Mesh { indexFormat = order.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        Vector3[] vertices = source.vertices;
        mesh.SetVertices(order.Select(i => vertices[i]).ToList());
        Vector3[] normals = source.normals;
        if (normals.Length == source.vertexCount) mesh.SetNormals(order.Select(i => normals[i]).ToList());
        Vector4[] tangents = source.tangents;
        if (tangents.Length == source.vertexCount) mesh.SetTangents(order.Select(i => tangents[i]).ToList());
        Color[] colors = source.colors;
        if (colors.Length == source.vertexCount) mesh.SetColors(order.Select(i => colors[i]).ToList());
        for (int channel = 0; channel < 4; channel++)
        {
            var uvs = new List<Vector2>();
            source.GetUVs(channel, uvs);
            if (uvs.Count == source.vertexCount) mesh.SetUVs(channel, order.Select(i => uvs[i]).ToList());
        }
        mesh.subMeshCount = triangles.Length;
        for (int s = 0; s < triangles.Length; s++)
            mesh.SetTriangles(triangles[s].Select(i => map[i]).ToList(), s, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Stores a new mesh in the split asset, or returns the identical one an
    // earlier run stored under the same name. Nothing is ever deleted.
    static Mesh Keep(Mesh mesh)
    {
        Mesh existing = AssetDatabase.LoadAllAssetsAtPath(SplitAsset).OfType<Mesh>().FirstOrDefault(m => m.name == mesh.name);
        if (existing != null && existing.vertexCount == mesh.vertexCount && existing.triangles.Length == mesh.triangles.Length)
        {
            Object.DestroyImmediate(mesh);
            return existing;
        }
        if (AssetDatabase.LoadMainAssetAtPath(SplitAsset) == null) AssetDatabase.CreateAsset(mesh, SplitAsset);
        else
        {
            if (existing != null) mesh.name += " " + DateTime.Now.ToString("HHmmss");
            AssetDatabase.AddObjectToAsset(mesh, SplitAsset);
        }
        return mesh;
    }

    // ---------------- helpers ----------------

    static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
    }

    static Renderer[] SceneRenderers() => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();

    static Transform Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)
        ?? throw new InvalidOperationException(name + " is missing from the scene.");
}
#endif
