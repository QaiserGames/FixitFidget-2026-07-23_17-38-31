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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE ART AUDIT'S BLENDER FIXES, THE UNITY HALF (24-26 Oct 2026; his answer to the 23 Oct art audit: "Yes, do that" for
// a01-a75 but a40). The models and pictures were rebuilt in Blender and Pillow (Tools/Blender: grace_house.py,
// grace_house_v2.py, cafe_aframe.py, cafe_till.py, cafe_art_fix_textures.py); copied over the old files at the same paths,
// they reimport in place. What a reimport can't do by itself is here, one menu item a job:
//
//   Fixit Fidget > Art fixes > Models > 1 - Grace's house: her photos, her hair and the re-listed materials (scene)
//       GH_Photo (every photo in her frames) takes GH_Photo.png, a painted snapshot (a72); GH_Hair is made (her head on
//       the pillow, a22) and GH_Quilt_Asleep.fbx maps to it; and the two of her pieces whose materials changed get them
//       again from the model: her house's pieces are plain objects that copied their materials when Break-ins 1 built
//       them, so the coat stand (a20: its coat and scarf off, five materials now four) and the slept-in quilt (a third
//       material, her hair) would otherwise draw the wrong colours on the wrong parts.
//   ... > 2 - The patio A-frame: grain copy, collider and a chalk face each side (scene)
//       MenuBoard.fbx is a true A now (a14). The café's A-frame doesn't draw the model's own mesh: Café furnishing made a
//       copy with the grain's UVs (Furnishing meshes.asset, "Cube.002 - grain UV"); this makes that copy again from the
//       new model, the same way, in place (so the scene's reference holds), fits the box collider to it, and lays
//       CK_AFrameFace on both boards, measured as Street 1 measures them: the street side's face moves onto the new
//       board, and the café side gets one of its own (one face each side).
//   ... > 3 - The till: key labels and the price on its screens (materials, import)
//       CashRegister.fbx's key tops and displays are on two new materials (a34): CF_TillKeys and CF_TillScreen are made
//       in Assets/Art/Materials/Cafe with their pictures and the till's import maps to them. No scene change: the
//       till in the café is the model as imported.
//   ... > 4 - The back door: flat shading on its frame (scene)
//       The back door is POLYGON City's SM_Prop_Door_01 (a55: "The frame's inner face shows a lighter triangle, a
//       shading seam where it should look flat"). Synty's files stay as they are: the two back doors in the scene get a
//       copy of their meshes with every face's own normal (Assets/Art/ArtFixes), so the seam can't show. Every other
//       SM_Prop_Door_01 in the city is untouched.
//   ... > 5 - Customer faces: find and photograph the shared face texture (read-only)
//       a47 ("Every face has heavy black brows angled down and a flat mouth, so up close all the guests look cross") asks
//       for a softer copy of Synty's shared face texture, which nobody outside his Mac can see. This finds the textures
//       the customers' looks wear, works out where on each the face is (from the heads' own front faces), and saves a
//       close crop of each face with a report, for the copy to be painted from. It changes nothing.
//
// Each step: Edit Mode only; finds things by name and path, as the house steps do; one Undo group; safe to run again (a
// second run changes nothing); a report in Logs/ArtFixes/<step>-<time>/report.txt; one last console line,
// "[Art fixes] <step>: DONE, <n> change(s)." or "... FAILED: <why>". Save the scene afterwards.
// ---------------------------------------------------------------------------
internal static class ArtFixBlenderSteps
{
    const string Menu = "Fixit Fidget/Art fixes/Models/";   // 26 Oct 2026 (integration): its own submenu, so its 1-5 never sit among the Unity art fixes' 1-9
    const string Tag = "[Art fixes] ";
    const string Lit = "Universal Render Pipeline/Lit";

    // ------------------------------------------------------------------ menus

    [MenuItem(Menu + "1 - Grace's house: her photos, her hair and the re-listed materials (scene)")]
    static void GraceHouseMenu() => Run("Grace's house", "grace-house", GraceHouseStep);

    [MenuItem(Menu + "2 - The patio A-frame: grain copy, collider and a chalk face each side (scene)")]
    static void AFrameMenu() => Run("The patio A-frame", "a-frame", AFrameStep);

    [MenuItem(Menu + "3 - The till: key labels and the price on its screens (materials, import)")]
    static void TillMenu() => Run("The till", "till", TillStep);

    [MenuItem(Menu + "4 - The back door: flat shading on its frame (scene)")]
    static void BackDoorMenu() => Run("The back door", "back-door", BackDoorStep);

    [MenuItem(Menu + "5 - Customer faces: find and photograph the shared face texture (read-only)")]
    static void FacesMenu() => Run("Customer faces", "faces", FacesStep);

    // One step: its undo group, its report, its last line.
    static void Run(string title, string label, Func<StringBuilder, string, int> step)
    {
        var report = new StringBuilder(Tag + title + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        string folder = LogFolder(label);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Art fixes - " + title);
            int group = Undo.GetCurrentGroup();
            int changes = step(report, folder);
            Undo.CollapseUndoOperations(group);
            if (changes > 0)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    if (s.isLoaded) EditorSceneManager.MarkSceneDirty(s);
                }
            AssetDatabase.SaveAssets();
            string line = $"{Tag}{title}: DONE, {changes} change(s).{(changes > 0 ? " Save the scene." : "")} {folder}";
            report.AppendLine().AppendLine(line);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            Debug.Log(line);
        }
        catch (Exception e)
        {
            string line = $"{Tag}{title}: FAILED: {e.Message} {folder}";
            report.AppendLine().AppendLine(line).AppendLine(e.ToString());
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.LogError(report.ToString());
            Debug.LogError(line);
        }
    }

    // ------------------------------------------------------------------ 1. Grace's house

    const string GraceModels = "Assets/Art/Models/GraceHouse/";
    const string GraceMaterials = "Assets/Art/Materials/GraceHouse";
    const string PhotoTexture = "Assets/Art/Textures/GraceHouse/GH_Photo.png";
    // GH_Hair: the golden blonde of her stand-in look (Character_BusinessWoman) under her lamps, as grace_house_v2.py has it.
    static readonly Color HairColour = new Color32(0xC9, 0xA1, 0x5A, 0xFF);
    const float HairSmoothness = .25f;
    // Her pieces whose material slots changed on 24 Oct: their copies in the scene take the model's materials again.
    static readonly string[] Relisted = { "GH_CoatStand", "GH_Quilt_Asleep" };

    static int GraceHouseStep(StringBuilder report, string folder)
    {
        int changes = 0;
        Shader lit = Shader.Find(Lit) ?? throw new InvalidOperationException("The URP Lit shader isn't there.");

        // a72: the snapshot on the photo material (every frame shares it; the frames' photos are mapped 0..1 now)
        Texture2D photo = Picture(PhotoTexture, 512, TextureWrapMode.Clamp, report, ref changes);
        Material photoMat = AssetDatabase.LoadAssetAtPath<Material>(GraceMaterials + "/GH_Photo.mat")
            ?? throw new InvalidOperationException(GraceMaterials + "/GH_Photo.mat isn't there: run Break-ins - Grace's furniture first.");
        if (photoMat.GetTexture("_BaseMap") != photo || photoMat.GetColor("_BaseColor") != Color.white)
        {
            Undo.RecordObject(photoMat, "Art fixes - GH_Photo");
            photoMat.SetTexture("_BaseMap", photo);
            photoMat.SetColor("_BaseColor", Color.white);          // white, so the picture shows its own colours
            EditorUtility.SetDirty(photoMat);
            changes++;
            report.AppendLine("GH_Photo: GH_Photo.png on it, its colour white (it was the flat sepia BFA88A).");
        }
        else report.AppendLine("GH_Photo: already wears GH_Photo.png.");

        // a22: her hair, and the slept-in quilt mapped to it
        string hairPath = GraceMaterials + "/GH_Hair.mat";
        Material hair = AssetDatabase.LoadAssetAtPath<Material>(hairPath);
        if (hair == null)
        {
            hair = new Material(lit) { name = "GH_Hair", enableInstancing = true };
            hair.SetColor("_BaseColor", HairColour);
            hair.SetFloat("_Smoothness", HairSmoothness);
            hair.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(hair, hairPath);
            changes++;
            report.AppendLine($"GH_Hair: made at {hairPath} (#C9A15A, smoothness {HairSmoothness:0.00}).");
        }
        else report.AppendLine("GH_Hair: already there.");
        changes += Remap(GraceModels + "GH_Quilt_Asleep.fbx", "GH_Hair", hair, report);

        // a20, a22: the scene's copies of the two pieces take the model's materials again
        MeshFilter[] filters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
        foreach (string model in Relisted)
        {
            string path = GraceModels + model + ".fbx";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException(path + " isn't there.");
            MeshFilter source = asset.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null)
                ?? throw new InvalidOperationException(path + " has no mesh.");
            Material[] want = source.GetComponent<Renderer>().sharedMaterials;
            if (want.Length != source.sharedMesh.subMeshCount)
                throw new InvalidOperationException($"{model}: the model has {source.sharedMesh.subMeshCount} parts but {want.Length} materials.");
            int found = 0;
            foreach (MeshFilter f in filters)
            {
                if (f.sharedMesh != source.sharedMesh) continue;
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (r == null) continue;
                found++;
                Material[] have = r.sharedMaterials;
                if (have.SequenceEqual(want)) continue;
                Undo.RecordObject(r, "Art fixes - " + model);
                r.sharedMaterials = want;
                changes++;
                report.AppendLine($"{PathOf(f.transform)}: {have.Length} materials ({Names(have)}) -> {want.Length} ({Names(want)}).");
            }
            report.AppendLine($"{model}: {found} in the open scene(s) wear the model's {want.Length} materials ({Names(want)}).");
        }
        return changes;
    }

    // ------------------------------------------------------------------ 2. the patio A-frame

    const string AFramePath = "18 - cafe furnishing/D - Entrance and waiting nook/Patio A-frame";
    const string MenuBoardModel = "Assets/Art/Models/MenuBoard.fbx";
    const string FurnishingMeshes = "Assets/Art/CafeFurnishing/Furnishing meshes.asset";
    const string StreetFaceName = "CK_AFrameFace (patio A-frame, street side)";
    const string CafeFaceName = "CK_AFrameFace (patio A-frame, café side)";
    // CafeStreetSteps' own numbers: the chalk face kit is 0.60 x 0.90 at scale 1, laid inside the board's edges by 5 mm,
    // 1 mm proud of the board (and of any chalk-slot faces over it).
    static readonly Vector2 FaceSize = new Vector2(.6f, .9f);
    const float FaceInset = .005f, FaceProud = .001f;

    static int AFrameStep(StringBuilder report, string folder)
    {
        int changes = 0;
        Transform frame = FindPath(AFramePath) ?? throw new InvalidOperationException($"'{AFramePath}' isn't in the open scene.");
        MeshFilter filter = frame.GetComponent<MeshFilter>() ?? throw new InvalidOperationException("The patio A-frame has no mesh of its own.");
        MeshRenderer renderer = frame.GetComponent<MeshRenderer>() ?? throw new InvalidOperationException("The patio A-frame has no renderer.");
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(MenuBoardModel) ?? throw new InvalidOperationException(MenuBoardModel + " isn't there.");
        MeshFilter source = asset.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null)
            ?? throw new InvalidOperationException(MenuBoardModel + " has no mesh.");
        if (source.sharedMesh.subMeshCount != 3)
            throw new InvalidOperationException($"{MenuBoardModel} has {source.sharedMesh.subMeshCount} parts; the rebuilt A-frame has three (wood, chalkboard, iron).");

        // The grain copy, as CafeFurnishing.GrainCopy makes it: the piece stood up at the origin with no turn, at its scale,
        // UVs boxed in metres (the room's grain at the tables' scale).
        Mesh fresh = Object.Instantiate(source.sharedMesh);
        fresh.name = source.sharedMesh.name + " - grain UV";
        Matrix4x4 w = Matrix4x4.TRS(Vector3.zero, source.transform.rotation, frame.lossyScale);
        Vector3[] v = fresh.vertices;
        Vector3[] n = fresh.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = w.MultiplyPoint3x4(v[i]);
            Vector3 d = n.Length > i ? w.MultiplyVector(n[i]).normalized : Vector3.up;
            uv[i] = Mathf.Abs(d.y) > .6f ? new Vector2(p.x, p.z) : Mathf.Abs(d.x) > .6f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }
        fresh.uv = uv;
        fresh.RecalculateTangents();
        Mesh grain = filter.sharedMesh;
        if (grain == null || AssetDatabase.GetAssetPath(grain) != FurnishingMeshes)
            grain = AssetDatabase.LoadAllAssetsAtPath(FurnishingMeshes).OfType<Mesh>().FirstOrDefault(m => m.name == fresh.name);
        if (grain != null && SameMesh(grain, fresh))
        {
            Object.DestroyImmediate(fresh);
            report.AppendLine($"The grain copy ({grain.name}): already made from the true A ({grain.vertexCount} vertices).");
        }
        else if (grain != null)
        {
            Undo.RecordObject(grain, "Art fixes - the A-frame's grain copy");
            int before = grain.vertexCount;
            EditorUtility.CopySerialized(fresh, grain);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(grain);
            changes++;
            report.AppendLine($"The grain copy ({grain.name}, {FurnishingMeshes}): made again from the true A, in place ({before} -> {grain.vertexCount} vertices).");
        }
        else
        {
            if (AssetDatabase.LoadMainAssetAtPath(FurnishingMeshes) == null) AssetDatabase.CreateAsset(fresh, FurnishingMeshes);
            else AssetDatabase.AddObjectToAsset(fresh, FurnishingMeshes);
            grain = fresh;
            changes++;
            report.AppendLine($"The grain copy: made ({grain.name}, in {FurnishingMeshes}).");
        }
        if (filter.sharedMesh != grain)
        {
            Undo.RecordObject(filter, "Art fixes - the A-frame's grain copy");
            filter.sharedMesh = grain;
            changes++;
            report.AppendLine("The patio A-frame draws the grain copy.");
        }

        // Its collider, the box Café furnishing gave it from its bounds, fitted to the new shape.
        BoxCollider box = frame.GetComponent<BoxCollider>();
        if (box != null)
        {
            Bounds b = grain.bounds;
            if ((box.center - b.center).sqrMagnitude > 1e-10f || (box.size - b.size).sqrMagnitude > 1e-10f)
            {
                Undo.RecordObject(box, "Art fixes - the A-frame's collider");
                box.center = b.center;
                box.size = b.size;
                changes++;
            }
            report.AppendLine($"Its box collider: {V(Vector3.Scale(box.size, frame.lossyScale))} m (the A's feet {F(b.size.y * frame.lossyScale.y)} m apart).");
        }
        else report.AppendLine("It has no box collider (Café furnishing gives it one): none added.");

        // The chalk faces, one on each board, measured as Street 1 measures the boards.
        Material[] mats = renderer.sharedMaterials;
        int boardSlot = Array.FindIndex(mats, m => m != null && m.name.Contains("Chalkboard"));
        int chalkSlot = Array.FindIndex(mats, m => m != null && m.name.Contains("Chalk") && !m.name.Contains("Chalkboard"));
        if (boardSlot < 0) throw new InvalidOperationException($"The patio A-frame wears no chalkboard ({Names(mats)}).");
        List<Board> boards = MeasureBoards(frame, grain, boardSlot, chalkSlot);
        report.AppendLine($"Its boards: {boards.Count} ({string.Join("; ", boards.Select(x => $"{F(x.width)} x {F(x.height)} m facing {F(Heading(x.normal), "0")}"))}).");
        if (boards.Count != 2) throw new InvalidOperationException($"The patio A-frame has {boards.Count} chalkboards facing out, not two: is MenuBoard.fbx the true A?");
        Transform street = FindNamed(StreetFaceName) ?? throw new InvalidOperationException($"'{StreetFaceName}' isn't in the scene (Street 1 puts it there).");
        GameObject facePrefab = PrefabUtility.GetCorrespondingObjectFromSource(street.gameObject)
            ?? throw new InvalidOperationException($"'{StreetFaceName}' isn't an instance of CK_AFrameFace's model.");
        foreach (Board b in boards)
        {
            bool streetSide = Mathf.Abs(Mathf.DeltaAngle(Heading(b.normal), 180f)) <= 90f;
            Transform face = streetSide ? street : FindNamed(CafeFaceName);
            if (face == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(facePrefab, street.parent);
                Undo.RegisterCreatedObjectUndo(go, "Art fixes - the A-frame's café-side face");
                go.name = CafeFaceName;
                Renderer mine = go.GetComponentInChildren<Renderer>(), theirs = street.GetComponentInChildren<Renderer>();
                if (mine != null && theirs != null) mine.sharedMaterials = theirs.sharedMaterials;
                face = go.transform;
                changes++;
                report.AppendLine($"{CafeFaceName}: added beside the street side's, under {PathOf(street.parent)}.");
            }
            float s = Mathf.Min((b.width - 2f * FaceInset) / FaceSize.x, (b.height - 2f * FaceInset) / FaceSize.y);
            Quaternion turn = Quaternion.LookRotation(b.normal, b.up);
            Vector3 at = b.centre - b.up * (FaceSize.y * s * .5f) + b.normal * (Mathf.Max(0f, b.proud) + FaceProud);
            Vector3 parentScale = face.parent != null ? face.parent.lossyScale : Vector3.one;
            Vector3 scale = new Vector3(s / parentScale.x, s / parentScale.y, s / parentScale.z);
            if ((face.position - at).sqrMagnitude > 1e-8f || Quaternion.Angle(face.rotation, turn) > .01f || (face.localScale - scale).sqrMagnitude > 1e-8f)
            {
                Undo.RecordObject(face, "Art fixes - the A-frame's chalk faces");
                face.SetPositionAndRotation(at, turn);
                face.localScale = scale;
                changes++;
            }
            report.AppendLine($"{face.name}: on the board facing {F(Heading(b.normal), "0")}, leaning {F(Vector3.Angle(b.up, Vector3.up), "0.0")} degrees, scale {F(s)}, {F((Mathf.Max(0f, b.proud) + FaceProud) * 1000f, "0.0")} mm proud.");
        }
        report.AppendLine("(Street 1's Undo knows only the street side's face: if Street 1 is ever undone, delete the café side's by hand.)");
        return changes;
    }

    sealed class Board
    {
        public Vector3 normal, up, right, centre;
        public float width, height, proud;
    }

    // The boards: the chalkboard's triangles grouped by the way they face (8 degrees); each big group's outward side is the
    // side away from the A's middle; of the groups facing out the same way, the farthest out is the board's face; how far
    // the chalk slot's faces over it stand proud (CafeStreetSteps.PlanAFrame and MeasureBoard, the same rules).
    static List<Board> MeasureBoards(Transform frame, Mesh mesh, int boardSlot, int chalkSlot)
    {
        Matrix4x4 m = frame.localToWorldMatrix;
        Vector3[] v = mesh.vertices;
        List<Vector3[]> Tris(int slot)
        {
            var list = new List<Vector3[]>();
            if (slot < 0 || slot >= mesh.subMeshCount) return list;
            int[] t = mesh.GetTriangles(slot);
            for (int i = 0; i + 2 < t.Length; i += 3)
                list.Add(new[] { m.MultiplyPoint3x4(v[t[i]]), m.MultiplyPoint3x4(v[t[i + 1]]), m.MultiplyPoint3x4(v[t[i + 2]]) });
            return list;
        }
        var groups = new List<(Vector3 normalSum, Vector3 centroidSum, float area, List<Vector3> points)>();
        foreach (Vector3[] t in Tris(boardSlot))
        {
            Vector3 c = Vector3.Cross(t[1] - t[0], t[2] - t[0]);
            float area = c.magnitude * .5f;
            if (area < 1e-7f) continue;
            Vector3 n = c / (area * 2f);
            int k = groups.FindIndex(g => Vector3.Angle(g.normalSum.normalized, n) < 8f);
            if (k < 0) { groups.Add((Vector3.zero, Vector3.zero, 0f, new List<Vector3>())); k = groups.Count - 1; }
            var gg = groups[k];
            gg.points.AddRange(t);
            groups[k] = (gg.normalSum + n * area, gg.centroidSum + (t[0] + t[1] + t[2]) / 3f * area, gg.area + area, gg.points);
        }
        Vector3 middle = frame.GetComponent<Renderer>().bounds.center;
        var faces = new List<(Vector3 outward, Vector3 centroid, List<Vector3> points)>();
        foreach (var g in groups.Where(g => g.area >= .04f))
        {
            Vector3 n = g.normalSum.normalized, c = g.centroidSum / g.area;
            float side = Vector3.Dot(n, c - middle);
            if (Mathf.Abs(side) < .01f) continue;
            Vector3 outward = side > 0f ? n : -n;
            int k = faces.FindIndex(f => Vector3.Angle(f.outward, outward) < 8f);
            if (k < 0) faces.Add((outward, c, g.points));
            else if (Vector3.Dot(c - faces[k].centroid, outward) > 0f) faces[k] = (outward, c, g.points);
        }
        List<Vector3[]> chalk = Tris(chalkSlot);
        var boards = new List<Board>();
        foreach (var (outward, centroid, points) in faces)
        {
            Vector3 n = outward.normalized;
            Vector3 up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
            if (up.magnitude < .5f) continue;
            up.Normalize();
            Vector3 right = Vector3.Cross(up, n);
            float r0 = float.PositiveInfinity, r1 = float.NegativeInfinity, u0 = float.PositiveInfinity, u1 = float.NegativeInfinity;
            foreach (Vector3 p in points)
            {
                float a = Vector3.Dot(p - centroid, right), b = Vector3.Dot(p - centroid, up);
                r0 = Mathf.Min(r0, a); r1 = Mathf.Max(r1, a); u0 = Mathf.Min(u0, b); u1 = Mathf.Max(u1, b);
            }
            var board = new Board
            {
                normal = n, up = up, right = right, width = r1 - r0, height = u1 - u0,
                centre = centroid + right * ((r0 + r1) * .5f) + up * ((u0 + u1) * .5f),
            };
            foreach (Vector3[] t in chalk)
                foreach (Vector3 p in t)
                {
                    Vector3 d = p - board.centre;
                    if (Mathf.Abs(Vector3.Dot(d, right)) <= board.width * .5f + .02f && Mathf.Abs(Vector3.Dot(d, up)) <= board.height * .5f + .02f && Vector3.Dot(d, n) > -.01f)
                        board.proud = Mathf.Max(board.proud, Vector3.Dot(d, n));
                }
            boards.Add(board);
        }
        return boards;
    }

    // ------------------------------------------------------------------ 3. the till

    const string TillModel = "Assets/Art/Models/CashRegister.fbx";
    const string CafeMaterials = "Assets/Art/Materials/Cafe";
    // name, picture, its longest side, smoothness, how much it glows (the display is backlit)
    static readonly (string name, string picture, int size, float smoothness, float glow)[] TillMaterials =
    {
        ("CF_TillKeys", "Assets/Art/Textures/Cafe/CF_TillKeys.png", 1024, .30f, 0f),
        ("CF_TillScreen", "Assets/Art/Textures/Cafe/CF_TillScreen.png", 512, .55f, .45f),
    };

    static int TillStep(StringBuilder report, string folder)
    {
        int changes = 0;
        Shader lit = Shader.Find(Lit) ?? throw new InvalidOperationException("The URP Lit shader isn't there.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TillModel) == null) throw new InvalidOperationException(TillModel + " isn't there.");
        if (!AssetDatabase.IsValidFolder(CafeMaterials)) throw new InvalidOperationException(CafeMaterials + " isn't there (the café's CF_ materials live in it).");
        foreach (var (name, picture, size, smoothness, glow) in TillMaterials)
        {
            Texture2D tex = Picture(picture, size, TextureWrapMode.Clamp, report, ref changes);
            string path = CafeMaterials + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool made = mat == null;
            if (made)
            {
                mat = new Material(lit) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(mat, path);
                changes++;
            }
            bool right = mat.GetTexture("_BaseMap") == tex && mat.GetColor("_BaseColor") == Color.white && Mathf.Approximately(mat.GetFloat("_Smoothness"), smoothness)
                         && (glow <= 0f || (mat.IsKeywordEnabled("_EMISSION") && mat.GetTexture("_EmissionMap") == tex));
            if (!right)
            {
                Undo.RecordObject(mat, "Art fixes - " + name);
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", 0f);
                if (glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", tex);
                    mat.SetColor("_EmissionColor", new Color(glow, glow, glow, 1f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(mat);
                if (!made) changes++;
            }
            report.AppendLine($"{name}: {(made ? "made" : right ? "already right" : "brought up to date")} at {path} ({Path.GetFileName(picture)}{(glow > 0f ? $", glowing {glow:0.00}" : "")}).");
            changes += Remap(TillModel, name, mat, report);
        }
        return changes;
    }

    // ------------------------------------------------------------------ 4. the back door

    const string BinsGroup = "24 - The bins (Night 0)";
    static readonly string[] BackDoors = { "Back door (inside)", "Back door (outside)" };
    const string FlatFolder = "Assets/Art/ArtFixes";
    const string FlatSuffix = " - flat";

    static int BackDoorStep(StringBuilder report, string folder)
    {
        int changes = 0;
        Transform bins = FindNamed(BinsGroup) ?? throw new InvalidOperationException($"'{BinsGroup}' isn't in the open scene (Bins 1 makes it).");
        foreach (string name in BackDoors)
        {
            Transform door = bins.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
                ?? throw new InvalidOperationException($"'{name}' isn't under '{BinsGroup}'.");
            foreach (MeshFilter f in door.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = f.sharedMesh;
                if (mesh == null) continue;
                if (mesh.name.EndsWith(FlatSuffix, StringComparison.Ordinal))
                {
                    report.AppendLine($"{PathOf(f.transform)}: already flat ({mesh.name}).");
                    continue;
                }
                Mesh flat = FlatCopy(mesh, report, ref changes);
                Undo.RecordObject(f, "Art fixes - the back door");
                f.sharedMesh = flat;
                changes++;
                report.AppendLine($"{PathOf(f.transform)}: {mesh.name} -> {flat.name} ({mesh.vertexCount} -> {flat.vertexCount} vertices, every face its own normal).");
            }
        }
        return changes;
    }

    // A copy of the mesh with no vertex shared between triangles, so every face is lit by its own normal (the POLYGON
    // look, without the seam); kept in Assets/Art/ArtFixes and reused by both doors.
    static Mesh FlatCopy(Mesh mesh, StringBuilder report, ref int changes)
    {
        if (!AssetDatabase.IsValidFolder(FlatFolder)) AssetDatabase.CreateFolder("Assets/Art", "ArtFixes");
        string path = $"{FlatFolder}/{Safe(mesh.name)}{FlatSuffix}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        Mesh src = Object.Instantiate(mesh);
        Vector3[] v = src.vertices;
        var uvs = new List<List<Vector2>>();
        for (int ch = 0; ch < 4; ch++)
        {
            var list = new List<Vector2>();
            src.GetUVs(ch, list);
            uvs.Add(list.Count == v.Length ? list : null);
        }
        Color32[] colours = src.colors32;
        bool hasColours = colours != null && colours.Length == v.Length;
        var pos = new List<Vector3>();
        var outUv = new List<List<Vector2>> { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
        var outColours = new List<Color32>();
        var subs = new List<int[]>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] t = src.GetTriangles(s);
            var tri = new int[t.Length];
            for (int i = 0; i < t.Length; i++)
            {
                tri[i] = pos.Count;
                pos.Add(v[t[i]]);
                for (int ch = 0; ch < 4; ch++) if (uvs[ch] != null) outUv[ch].Add(uvs[ch][t[i]]);
                if (hasColours) outColours.Add(colours[t[i]]);
            }
            subs.Add(tri);
        }
        var flat = new Mesh { name = mesh.name + FlatSuffix };
        if (pos.Count > 65000) flat.indexFormat = IndexFormat.UInt32;
        flat.SetVertices(pos);
        for (int ch = 0; ch < 4; ch++) if (uvs[ch] != null) flat.SetUVs(ch, outUv[ch]);
        if (hasColours) flat.SetColors(outColours);
        flat.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) flat.SetTriangles(subs[s], s);
        flat.RecalculateNormals();
        flat.RecalculateTangents();
        flat.RecalculateBounds();
        Object.DestroyImmediate(src);
        AssetDatabase.CreateAsset(flat, path);
        changes++;
        report.AppendLine($"Made {path}.");
        return flat;
    }

    // ------------------------------------------------------------------ 5. customer faces (read-only)

    const string LookFolder = "Assets/Art/CityNeighbors/Prefabs";

    static int FacesStep(StringBuilder report, string folder)
    {
        if (!AssetDatabase.IsValidFolder(LookFolder)) throw new InvalidOperationException($"{LookFolder} isn't there (POLYGON City's looks are only on his machine).");
        var byTexture = new Dictionary<Texture, (List<string> looks, Rect face)>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { LookFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject look = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (look == null || !look.name.StartsWith("Character_", StringComparison.Ordinal)) continue;
            foreach (SkinnedMeshRenderer smr in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                Rect face = FaceRect(smr.sharedMesh);
                foreach (Material m in smr.sharedMaterials)
                {
                    if (m == null) continue;
                    Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                    if (tex == null) continue;
                    if (!byTexture.TryGetValue(tex, out var entry)) byTexture[tex] = entry = (new List<string>(), face);
                    entry.looks.Add($"{look.name} ({m.name})");
                }
            }
        }
        if (byTexture.Count == 0) throw new InvalidOperationException($"No Character_ look in {LookFolder} wears a texture.");
        foreach (var pair in byTexture)
        {
            Texture tex = pair.Key;
            Rect r = pair.Value.face;
            report.AppendLine($"{AssetDatabase.GetAssetPath(tex)} ({tex.width} x {tex.height}): worn by {pair.Value.looks.Count}: {string.Join(", ", pair.Value.looks.Take(12))}{(pair.Value.looks.Count > 12 ? ", ..." : "")}");
            report.AppendLine($"  the faces' front in UV: u {F(r.xMin)}-{F(r.xMax)}, v {F(r.yMin)}-{F(r.yMax)}" + (r.width > 0f ? "" : " (not found on the meshes)"));
            if (r.width <= 0f) continue;
            string file = Path.Combine(folder, Safe(tex.name) + "-face.png");
            SaveCrop(tex, r, 4, file);
            report.AppendLine($"  photographed: {file}");
        }
        report.AppendLine();
        report.AppendLine("Nothing was changed. The softer copy (a47): paint it from the texture above into a new file (never over Synty's), " +
                          "with the brows thinner and level or a touch raised, the eyes a little rounder and a small smile; then a copy of each " +
                          "look's material takes it as _BaseMap and the looks wear the copies (a step of its own, once the picture exists).");
        return 0;
    }

    // Where the faces are on the texture: the UVs of the head's front (the top 13 % of the bind-pose mesh, facing +Z).
    static Rect FaceRect(Mesh mesh)
    {
        Mesh m = Object.Instantiate(mesh);
        try
        {
            Vector3[] v = m.vertices, n = m.normals;
            Vector2[] uv = m.uv;
            if (v.Length == 0 || uv.Length != v.Length || n.Length != v.Length) return Rect.zero;
            float top = v.Max(p => p.y), bottom = v.Min(p => p.y), headFrom = top - .13f * (top - bottom);
            float u0 = 1f, u1 = 0f, v0 = 1f, v1 = 0f;
            int found = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].y < headFrom || n[i].z < .6f) continue;
                u0 = Mathf.Min(u0, uv[i].x); u1 = Mathf.Max(u1, uv[i].x); v0 = Mathf.Min(v0, uv[i].y); v1 = Mathf.Max(v1, uv[i].y);
                found++;
            }
            return found >= 3 ? Rect.MinMaxRect(u0, v0, u1, v1) : Rect.zero;
        }
        finally { Object.DestroyImmediate(m); }
    }

    // A crop of a texture (any import settings: drawn through a render texture), enlarged without smoothing, as a PNG.
    static void SaveCrop(Texture tex, Rect uv, int zoom, string file)
    {
        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture before = RenderTexture.active;
        var full = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            full.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            full.Apply();
        }
        finally
        {
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(rt);
        }
        int x0 = Mathf.Clamp(Mathf.FloorToInt(uv.xMin * tex.width) - 4, 0, tex.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(uv.yMin * tex.height) - 4, 0, tex.height - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(uv.xMax * tex.width) + 4, x0 + 1, tex.width), y1 = Mathf.Clamp(Mathf.CeilToInt(uv.yMax * tex.height) + 4, y0 + 1, tex.height);
        int w = x1 - x0, h = y1 - y0;
        var crop = new Texture2D(w * zoom, h * zoom, TextureFormat.RGBA32, false);
        for (int y = 0; y < h * zoom; y++)
            for (int x = 0; x < w * zoom; x++)
                crop.SetPixel(x, y, full.GetPixel(x0 + x / zoom, y0 + y / zoom));
        crop.Apply();
        File.WriteAllBytes(file, crop.EncodeToPNG());
        Object.DestroyImmediate(full);
        Object.DestroyImmediate(crop);
    }

    // ------------------------------------------------------------------ helpers

    // A picture's import settings (a colour picture, mipmapped, clamped, its size): the texture, after any reimport.
    static Texture2D Picture(string path, int size, TextureWrapMode wrap, StringBuilder report, ref int changes)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new InvalidOperationException(path + " isn't in the project: copy the art fixes' pictures over first.");
        bool right = importer.textureType == TextureImporterType.Default && importer.sRGBTexture && importer.mipmapEnabled
                     && importer.wrapMode == wrap && importer.maxTextureSize == size && importer.anisoLevel >= 4;
        if (!right)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.maxTextureSize = size;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            changes++;
            report.AppendLine($"{Path.GetFileName(path)}: imported as a colour picture, {size} px, clamped, mipmapped.");
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException(path + " didn't import as a texture.");
    }

    // A model's material by name mapped to a project material (the importer's remap), reimported if it changed.
    static int Remap(string modelPath, string materialName, Material target, StringBuilder report)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter ?? throw new InvalidOperationException(modelPath + " isn't a model.");
        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
        if (importer.GetExternalObjectMap().TryGetValue(id, out Object have) && have == target)
        {
            report.AppendLine($"{Path.GetFileName(modelPath)}: its {materialName} already maps to {AssetDatabase.GetAssetPath(target)}.");
            return 0;
        }
        importer.AddRemap(id, target);
        importer.SaveAndReimport();
        report.AppendLine($"{Path.GetFileName(modelPath)}: its {materialName} now maps to {AssetDatabase.GetAssetPath(target)} (reimported).");
        return 1;
    }

    static bool SameMesh(Mesh a, Mesh b)
    {
        if (a.vertexCount != b.vertexCount || a.subMeshCount != b.subMeshCount) return false;
        Vector3[] va = a.vertices, vb = b.vertices;
        Vector2[] ua = a.uv, ub = b.uv;
        if (ua.Length != ub.Length) return false;
        for (int i = 0; i < va.Length; i++)
            if ((va[i] - vb[i]).sqrMagnitude > 1e-12f || (ua.Length > i && (ua[i] - ub[i]).sqrMagnitude > 1e-12f)) return false;
        return true;
    }

    // An object by its path from a root of an open scene ("a/b/c"), active or not.
    static Transform FindPath(string path)
    {
        string[] parts = path.Split('/');
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (GameObject root in s.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != parts[parts.Length - 1]) continue;
                    Transform at = t;
                    bool match = true;
                    for (int k = parts.Length - 1; k >= 0; k--)
                    {
                        if (at == null || at.name != parts[k]) { match = false; break; }
                        at = at.parent;
                    }
                    if (match) return t;
                }
        }
        return null;
    }

    // The first object of that name in the open scenes, active or not.
    static Transform FindNamed(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (GameObject root in s.GetRootGameObjects())
            {
                Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (t != null) return t;
            }
        }
        return null;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform at = t; at != null; at = at.parent) parts.Insert(0, at.name);
        return string.Join("/", parts);
    }

    static float Heading(Vector3 n) => Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;

    static string Names(Material[] mats) => string.Join(", ", mats.Select(m => m != null ? m.name : "none"));

    static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    static string F(float x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);

    static string LogFolder(string name)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ArtFixes",
            name + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
#endif
