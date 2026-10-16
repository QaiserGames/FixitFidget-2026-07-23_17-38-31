using System.Text;
using UnityEditor;
using UnityEngine;

// 15 Oct: Bench tools 1 found no keys (blend shapes) on the held cloth and the brush although their FBX carry them and
// importBlendShapes is on. This probe reimports each of the two FBX under a few importer settings, prints what Unity made of
// each (renderer kind, blend shape count), and puts the settings back as they were. It changes nothing in the end.
static class KeyImportProbe
{
    static readonly string[] Paths =
    {
        "Assets/Art/Models/BenchTools/BT_ClothHeld.fbx",
        "Assets/Art/Models/BenchTools/BT_Brush.fbx",
    };

    [MenuItem("Fixit Fidget/Checks/Keys probe - why the held cloth and the brush lose their blend shapes (puts everything back)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Keys probe] Edit Mode only."); return; }
        var sb = new StringBuilder("[Keys probe]\n");
        foreach (string path in Paths)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { sb.AppendLine(path + ": no ModelImporter"); continue; }
            var was = (imp.animationType, imp.importBlendShapes, imp.importBlendShapeNormals, imp.importNormals, imp.isReadable,
                       imp.weldVertices, imp.optimizeMeshPolygons, imp.optimizeMeshVertices, imp.keepQuads);
            sb.AppendLine($"{path}: as it is: rig {imp.animationType}, keys {imp.importBlendShapes}, key normals {imp.importBlendShapeNormals}, " +
                          $"normals {imp.importNormals}, readable {imp.isReadable}, weld {imp.weldVertices}, keepQuads {imp.keepQuads}");
            sb.AppendLine("  now:            " + Describe(path));
            Try(sb, path, "rig Generic", i => i.animationType = ModelImporterAnimationType.Generic);
            Try(sb, path, "rig None, key normals None", i => { i.animationType = ModelImporterAnimationType.None; i.importBlendShapeNormals = ModelImporterNormals.None; });
            Try(sb, path, "rig None, key normals Calculate", i => { i.animationType = ModelImporterAnimationType.None; i.importBlendShapeNormals = ModelImporterNormals.Calculate; });
            Try(sb, path, "rig Legacy", i => { i.animationType = ModelImporterAnimationType.Legacy; i.importBlendShapeNormals = was.importBlendShapeNormals; });
            Try(sb, path, "rig None, no weld", i => { i.animationType = ModelImporterAnimationType.None; i.weldVertices = false; });
            // put it back
            imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = was.animationType; imp.importBlendShapes = was.importBlendShapes; imp.importBlendShapeNormals = was.importBlendShapeNormals;
            imp.importNormals = was.importNormals; imp.isReadable = was.isReadable; imp.weldVertices = was.weldVertices;
            imp.optimizeMeshPolygons = was.optimizeMeshPolygons; imp.optimizeMeshVertices = was.optimizeMeshVertices; imp.keepQuads = was.keepQuads;
            imp.SaveAndReimport();
            sb.AppendLine("  put back:       " + Describe(path));
        }
        Debug.Log(sb.ToString());
    }

    static void Try(StringBuilder sb, string path, string label, System.Action<ModelImporter> set)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        set(imp);
        imp.importBlendShapes = true;
        imp.SaveAndReimport();
        sb.AppendLine($"  {label,-32}: " + Describe(path));
    }

    static string Describe(string path)
    {
        var parts = new StringBuilder();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is Mesh m) parts.Append($"mesh '{m.name}' {m.vertexCount} verts, {m.blendShapeCount} key(s)" +
                                          (m.blendShapeCount > 0 ? " [" + Names(m) + "]" : "") + "; ");
            else if (o is Renderer r) parts.Append($"{r.GetType().Name} on '{r.name}'; ");
        }
        return parts.ToString();
    }

    static string Names(Mesh m)
    {
        var n = new StringBuilder();
        for (int i = 0; i < m.blendShapeCount; i++) n.Append((i > 0 ? ", " : "") + m.GetBlendShapeName(i));
        return n.ToString();
    }
}
