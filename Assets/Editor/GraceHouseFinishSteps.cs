#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// GRACE'S HOUSE: THE LAMPS AND THE WINDOW AT NIGHT (5 Oct 2026; claude/house-interiors-plan.md §8–§9, the
// lighting pass, route A)
//
//   Fixit Fidget > Night > Break-ins 5 - Grace's house: the lamps and the window at night (scene)
//   Fixit Fidget > Night > Break-ins 5 - Take the finish back out (scene)
//
// The slop audit's photos (Logs/Night/house-close-ups-2026-10-05_031614) showed two things about her rooms
// that no prop fixes: her front window glowed orange INTO the room (its curtains wear the street's glow on
// both sides), and her lamps made no pools (the night's ambient lit every wall evenly, and the lamps cast no
// shadows). So, on the house as built by Break-ins 1 (nothing is rebuilt):
//   * the curtains' mesh is split by which way each face looks: the faces toward the street keep the glow,
//     the faces into the room (and the edges) wear a plain curtain fabric of the same colour, no glow;
//   * her standard lamp and bedside lamp cast soft shadows and reach a little further; a small warm light
//     goes under the kitchen cupboards (the kitchen was the darkest corner); the landing light stays;
//   * CafeDaylight's Indoor Ambient (0.35 by default) takes the rest: while Ace is inside, the night's
//     ambient goes down to it and the rooms are dark between the lamps (GraceHouse.Enter/Leave).
// With undo, and a record (GraceHouseFinish) so it can all be taken out again. Save the scene afterwards.
// ---------------------------------------------------------------------------
internal static class GraceHouseFinishSteps
{
    const string Tag = "[Break-ins 5] ";
    const string Menu = "Fixit Fidget/Night/";
    const string HouseName = "1 - Saffron bay-window house";
    const string CurtainsName = "Front window curtains";
    const string AssetPath = "Assets/Playtests/AcesCafeLayout/Grace's house - finish.asset";
    const string MaterialFolder = "Assets/Art/Materials/GraceHouse";

    [MenuItem(Menu + "Break-ins 5 - Grace's house: the lamps and the window at night (scene)")]
    static void Apply()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName) ?? throw new InvalidOperationException($"'{HouseName}' isn't in the scene.");
            Transform root = house.Find(GraceHouseSteps.RootName) ?? throw new InvalidOperationException("Grace's house isn't built inside yet (Break-ins 1).");
            GraceHouse runner = root.GetComponent<GraceHouse>() ?? throw new InvalidOperationException("Her rooms' root has no GraceHouse.");
            if (root.GetComponent<GraceHouseFinish>() != null) { Debug.Log(Tag + "The finish is already on (Break-ins 5 - Take the finish back out undoes it)."); return; }
            Transform curtainsT = house.Find(CurtainsName) ?? throw new InvalidOperationException($"Her house has no '{CurtainsName}' (Break-ins 1 makes it).");
            MeshFilter curtains = curtainsT.GetComponent<MeshFilter>();
            var curtainsRenderer = curtainsT.GetComponent<MeshRenderer>();
            if (curtains == null || curtains.sharedMesh == null || curtainsRenderer == null) throw new InvalidOperationException("The curtains have no mesh.");
            if (!curtains.sharedMesh.isReadable) throw new InvalidOperationException("The curtains' mesh can't be read.");

            Undo.SetCurrentGroupName("Grace's house: the lamps and the window at night");
            int group = Undo.GetCurrentGroup();
            var finish = Undo.AddComponent<GraceHouseFinish>(root.gameObject);

            // ---- the window: the glow toward the street only ----
            Material[] before = curtainsRenderer.sharedMaterials;
            Mesh split = SplitByFacing(curtains.sharedMesh, Vector3.forward, out int outFaces, out int inFaces);
            split.name = curtains.sharedMesh.name + " (glow to the street)";
            var materials = new List<Material>(before);
            foreach (Material m in before) materials.Add(Fabric(m));
            finish.curtains = curtains;
            finish.curtainsBefore = curtains.sharedMesh;
            finish.curtainsMaterialsBefore = before;
            Undo.RecordObject(curtains, "Grace's house finish");
            Undo.RecordObject(curtainsRenderer, "Grace's house finish");
            curtains.sharedMesh = split;
            curtainsRenderer.sharedMaterials = materials.ToArray();
            Save(split);
            report.AppendLine($"Her front window's curtains: {outFaces} faces keep the glow toward the street, {inFaces} faces into the room wear plain fabric ({materials.Count - before.Length} fabric material(s)).");

            // ---- the window: the clapboard lips' backs ----
            // The lips (the siding's boards, 0.035 tall every 0.25 m from 0.8 m up) run across the front wall; Break-ins 1
            // cut their fronts, tops and bottoms out of the window but not their backs (z 0, looking into the house), and
            // from inside those showed as bars across the glass. Cut here, on the wall as it is.
            Transform paletteT = house.Find("Street palette");
            MeshFilter palette = paletteT != null ? paletteT.GetComponent<MeshFilter>() : null;
            if (palette != null && palette.sharedMesh != null && palette.sharedMesh.isReadable)
            {
                var wall = new GraceHouseSteps.Surgery(palette.sharedMesh);
                const float x0 = -2.175f, x1 = .075f;   // the glass, in house space (as Break-ins 1 cut it)
                int lipBacks = 0;
                for (int k = 0; k < 6; k++)
                {
                    float y = .8f + .25f * k, lo = y - .0175f, hi = y + .0175f;
                    lipBacks += wall.CutHole(2, 0f, -1, Rect.MinMaxRect(x0, lo - .01f, x1, hi + .01f));
                }
                if (lipBacks > 0)
                {
                    Mesh cutWall = wall.ToMesh(palette.sharedMesh.name + " (lips' backs out of the window)");
                    finish.palette = palette;
                    finish.paletteBefore = palette.sharedMesh;
                    Undo.RecordObject(palette, "Grace's house finish");
                    palette.sharedMesh = cutWall;
                    Save(cutWall);
                }
                report.AppendLine($"The clapboard lips across her window: {lipBacks} back face(s) cut out, so nothing crosses the glass from inside.");
            }
            else report.AppendLine("  (no readable 'Street palette' wall: the lips' backs are left as they are)");

            // ---- the lamps ----
            var changed = new List<Light>();
            var intensity = new List<float>();
            var range = new List<float>();
            var shadows = new List<LightShadows>();
            void Tune(string name, float newIntensity, float newRange, LightShadows newShadows)
            {
                Light l = runner.nightLights.FirstOrDefault(x => x != null && x.name == name);
                if (l == null) { report.AppendLine($"  (no lamp called '{name}' to tune)"); return; }
                changed.Add(l); intensity.Add(l.intensity); range.Add(l.range); shadows.Add(l.shadows);
                Undo.RecordObject(l, "Grace's house finish");
                l.intensity = newIntensity;
                l.range = newRange;
                l.shadows = newShadows;
                if (newShadows != LightShadows.None)
                {
                    l.shadowStrength = .85f;
                    l.shadowBias = .03f;
                    l.shadowNormalBias = .3f;
                    // URP's own settings on the light, as its Inspector would add them.
                    if (l.GetComponent<UniversalAdditionalLightData>() == null) Undo.AddComponent<UniversalAdditionalLightData>(l.gameObject);
                }
                report.AppendLine($"  {name}: {newIntensity:0.0} over {newRange:0.0} m, {newShadows.ToString().ToLowerInvariant()} shadows.");
            }
            report.AppendLine("Her lamps:");
            Tune("Her standard lamp (night)", 2.0f, 5.5f, LightShadows.Soft);
            Tune("Her bedside lamp (night)", 1.5f, 4.0f, LightShadows.Soft);
            finish.changedLights = changed.ToArray();
            finish.intensityBefore = intensity.ToArray();
            finish.rangeBefore = range.ToArray();
            finish.shadowsBefore = shadows.ToArray();

            // The kitchen light: under the wall cupboards (GH_WallCupboards at X 3.64, on the back wall, 1.45 m up), a
            // warm strip's worth, no shadows. Plan metres into the rooms' space: (-X, z, Y).
            Transform ground = runner.groundFloor != null ? runner.groundFloor : root;
            Light kitchen = Lamp(ground, "The kitchen light (night)", 3.64f, .50f, 1.42f, new Color(1f, .90f, .72f), .9f, 2.8f);
            finish.addedLights = new[] { kitchen };
            finish.nightLightsBefore = runner.nightLights;
            Undo.RecordObject(runner, "Grace's house finish");
            runner.nightLights = runner.nightLights.Concat(new[] { kitchen }).ToArray();
            EditorUtility.SetDirty(runner);
            report.AppendLine("  The kitchen light (night): 0.9 over 2.8 m under the wall cupboards, added.");

            EditorUtility.SetDirty(finish);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            CafeDaylight daylight = Object.FindAnyObjectByType<CafeDaylight>();
            report.AppendLine(daylight != null
                ? $"Indoors at night the ambient goes down to {daylight.indoorAmbient:0.00} of the street's (CafeDaylight > Indoor Ambient)."
                : "No CafeDaylight in the scene: the indoor ambient has nothing to act on.");
            Debug.Log(Tag + "The finish is on. Save the scene to keep it (Ctrl+S); then the slop audit again for the after photos.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The finish FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 5 - Take the finish back out (scene)")]
    static void TakeOut()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            GraceHouseFinish finish = Object.FindAnyObjectByType<GraceHouseFinish>(FindObjectsInactive.Include);
            if (finish == null) { Debug.Log(Tag + "The finish isn't on."); return; }
            GraceHouse runner = finish.GetComponent<GraceHouse>();
            Undo.SetCurrentGroupName("Take Grace's house finish back out");
            int group = Undo.GetCurrentGroup();
            if (finish.curtains != null && finish.curtainsBefore != null)
            {
                var r = finish.curtains.GetComponent<MeshRenderer>();
                Undo.RecordObject(finish.curtains, "Take the finish out");
                finish.curtains.sharedMesh = finish.curtainsBefore;
                if (r != null) { Undo.RecordObject(r, "Take the finish out"); r.sharedMaterials = finish.curtainsMaterialsBefore; }
            }
            if (finish.palette != null && finish.paletteBefore != null)
            {
                Undo.RecordObject(finish.palette, "Take the finish out");
                finish.palette.sharedMesh = finish.paletteBefore;
            }
            for (int i = 0; i < finish.changedLights.Length; i++)
            {
                Light l = finish.changedLights[i];
                if (l == null) continue;
                Undo.RecordObject(l, "Take the finish out");
                if (i < finish.intensityBefore.Length) l.intensity = finish.intensityBefore[i];
                if (i < finish.rangeBefore.Length) l.range = finish.rangeBefore[i];
                if (i < finish.shadowsBefore.Length) l.shadows = finish.shadowsBefore[i];
            }
            if (runner != null)
            {
                Undo.RecordObject(runner, "Take the finish out");
                runner.nightLights = finish.nightLightsBefore.Where(l => l != null).ToArray();
                EditorUtility.SetDirty(runner);
            }
            foreach (Light l in finish.addedLights) if (l != null) Undo.DestroyObjectImmediate(l.gameObject);
            Undo.DestroyObjectImmediate(finish);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(Tag + "The finish is taken back out: the curtains, the lamps and the night lights are as Break-ins 1 left them. Save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking the finish out FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 5 - Grace's house: the lamps and the window at night (scene)", true)]
    [MenuItem(Menu + "Break-ins 5 - Take the finish back out (scene)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // The mesh in two halves per material: the faces looking the given way (the street, +z in the house's space)
    // first, as the original sub-meshes; then the same sub-meshes again holding every other face.
    static Mesh SplitByFacing(Mesh source, Vector3 toward, out int outFaces, out int inFaces)
    {
        Vector3[] v = source.vertices;
        var mesh = new Mesh { name = source.name };
        mesh.indexFormat = source.indexFormat;
        mesh.SetVertices(v);
        if (source.normals.Length == v.Length) mesh.SetNormals(source.normals);
        if (source.tangents.Length == v.Length) mesh.SetTangents(source.tangents);
        if (source.uv.Length == v.Length) mesh.SetUVs(0, source.uv);
        if (source.uv2.Length == v.Length) mesh.SetUVs(1, source.uv2);
        if (source.colors.Length == v.Length) mesh.SetColors(source.colors);
        int subs = source.subMeshCount;
        mesh.subMeshCount = subs * 2;
        outFaces = inFaces = 0;
        for (int s = 0; s < subs; s++)
        {
            int[] t = source.GetTriangles(s);
            var outside = new List<int>();
            var inside = new List<int>();
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 n = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
                if (Vector3.Dot(n.normalized, toward) > .5f) { outside.AddRange(new[] { t[k], t[k + 1], t[k + 2] }); outFaces++; }
                else { inside.AddRange(new[] { t[k], t[k + 1], t[k + 2] }); inFaces++; }
            }
            mesh.SetTriangles(outside, s);
            mesh.SetTriangles(inside, subs + s);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    // The plain curtain: the glowing material's own colour, no glow, matte. One per glowing material, kept as an asset.
    static Material Fabric(Material glow)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "GraceHouse");
        string path = MaterialFolder + "/GH_Curtain_Fabric" + (glow != null ? " - " + Safe(glow.name) : "") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = glow != null ? new Material(glow) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .1f);
        if (m.HasProperty("_BaseColor"))
        {
            // Not the glow's saffron (lit by her lamp a foot away it read as an orange wall, the audit's first tell):
            // a plain oat, the colour of fabric with the light behind it, not in it.
            Color c = m.GetColor("_BaseColor");
            ColorUtility.TryParseHtmlString("#CDBBA0", out Color oat);
            m.SetColor("_BaseColor", new Color(oat.r, oat.g, oat.b, c.a));
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Light Lamp(Transform parent, string name, float X, float Y, float z, Color colour, float intensity, float range)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Grace's house finish");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-X, z, Y);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = colour;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.enabled = false;   // on at night (GraceHouse)
        return l;
    }

    static void Save(Mesh mesh)
    {
        mesh.UploadMeshData(false);
        if (AssetDatabase.LoadMainAssetAtPath(AssetPath) == null) AssetDatabase.CreateAsset(mesh, AssetPath);
        else AssetDatabase.AddObjectToAsset(mesh, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath);
    }

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_');
        return sb.ToString().Trim();
    }
}
#endif
