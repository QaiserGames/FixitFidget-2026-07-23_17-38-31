#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// THE ART AUDIT'S UNITY FIXES (26 Oct 2026; review24 brief U, the art audit of 23 Oct, /root/review23/decisions/art-items.json)
//
// His answer (24 Oct 2026) to the 97 findings: "Yes, do that" for a01-a75, except a40 (the syrup labels: leave them). His
// notes of 26 Oct: a01 "if it is a problem, then fix it"; a04 "if that is an issue that you notice, definitely fix it"; a05
// "if it still is an issue, that lets definitely fix that"; a10 (the drinks machine's buttons) is being redesigned and a11
// (the bench screen's glass) is paused, so neither is touched here.
//
// The findings that need the scene, a prefab, a material or a setting changed are here, one menu item per group:
//
//   Fixit Fidget > Art fixes > 1 - People: twin walkers apart, more colours for the guests      a15, a46
//   Fixit Fidget > Art fixes > 2 - The bench close-up                                           a24, a30, a32, a33
//   Fixit Fidget > Art fixes > 3 - The counter and the drinks machine                           a09, a35, a36, a38, a39
//   Fixit Fidget > Art fixes > 4 - The café room                                                a13, a43, a51, a52, a54
//   Fixit Fidget > Art fixes > 5 - The patio and the shopfront                                  a07, a58, a59, a60, a61
//   Fixit Fidget > Art fixes > 6 - The streets                                                  a06, a16, a65 (a19: a survey)
//   Fixit Fidget > Art fixes > 7 - Grace's house                                                a17, a18, a21, a70, a71 (a75: a look)
//   Fixit Fidget > Art fixes > 8 - The bins at night                                            a05
//   Fixit Fidget > Art fixes > 9 - Softer window shadows (URP settings)                         a45
//   Fixit Fidget > Art fixes > Check - one body each, heads kept (Play mode)                    a01, a04 (read-only)
// and an "Undo" for each. The runtime fixes (a01, a04, a08, a25, a26, a36, a69) are in the code, marked "26 Oct 2026 (Art fixes)".
//
// HOW EACH STEP BEHAVES. Through the café redesign's shared step (CafeRedesignShared.Step): Edit Mode, the café scene, the
// survey first (it finds everything by NAME anywhere in the scene, never by a full path: Furniture 1 moves the café's own
// pieces under "31 - furniture", and his Mac may run it before or after these), one undo group, a record on the café's
// root so the step's Undo can put it back after a save, the step's checks, the photos before and after from where the
// finding was seen, the report in Logs/ArtFixes/<label>-<time>/report.txt (and <label>-last.txt), one DONE line. A step
// already done in this scene doesn't run again (idempotent). What the shared record can't hold (a renderer's shadow switch,
// a component's field, a material or settings asset, a prefab's palette) is kept in Logs/ArtFixes/<label>-undo.json, and the
// step's Undo puts it back as well. The shared rule stands: only the latest of the redesign's steps can be undone, newest
// first (these steps count among them). Nothing found in the scene is deleted: it is switched off. Save the scene after.
// Materials the steps make go in Assets/Art/Materials/ArtFixes/, meshes in Assets/Art/Models/ArtFixes/; an Undo leaves them
// (nothing uses them then).
// ---------------------------------------------------------------------------
internal static class ArtFixUnitySteps
{
    const string Menu = "Fixit Fidget/Art fixes/";
    const string MaterialsFolder = "Assets/Art/Materials/ArtFixes";
    const string ModelsFolder = "Assets/Art/Models/ArtFixes";
    const string GeometryAsset = ModelsFolder + "/Art fixes geometry.asset";
    // Shared.Step writes its reports under Logs/CafeRedesign/<label>; "../ArtFixes/" puts them in Logs/ArtFixes instead.
    const string LogPrefix = "../ArtFixes/";
    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ArtFixes"));

    static string F(float f, string format = "0.000") => f.ToString(format, CultureInfo.InvariantCulture);

    // ================================================================== the steps' menu items

    const string T1 = "Art fixes 1 - People: twin walkers apart, more colours for the guests";
    const string T2 = "Art fixes 2 - The bench close-up";
    const string T3 = "Art fixes 3 - The counter and the drinks machine";
    const string T4 = "Art fixes 4 - The cafe room";
    const string T5 = "Art fixes 5 - The patio and the shopfront";
    const string T6 = "Art fixes 6 - The streets";
    const string T7 = "Art fixes 7 - Grace's house";
    const string T8 = "Art fixes 8 - The bins at night";
    const string T9 = "Art fixes 9 - Softer window shadows (URP settings)";
    const string L1 = "art-fix-1-people", L2 = "art-fix-2-bench", L3 = "art-fix-3-counter-drinks", L4 = "art-fix-4-cafe-room",
                 L5 = "art-fix-5-patio", L6 = "art-fix-6-streets", L7 = "art-fix-7-grace-house", L8 = "art-fix-8-bins", L9 = "art-fix-9-shadows";

    [MenuItem(Menu + "1 - People: twin walkers apart, more colours for the guests (scene and prefabs, with Undo)")]
    static void Do1() => Run(T1, L1, People, PeoplePhotos, guards: false, rebake: false);
    [MenuItem(Menu + "1 - Undo (scene and prefabs)")]
    static void Undo1() => UndoRun(T1, L1, rebake: false);

    [MenuItem(Menu + "2 - The bench close-up: lamp shadows, desk colours, the strip, the donor box (scene, with Undo)")]
    static void Do2() => Run(T2, L2, Bench, BenchPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "2 - Undo (scene)")]
    static void Undo2() => UndoRun(T2, L2, rebake: false);

    [MenuItem(Menu + "3 - The counter and the drinks machine: cups, bulbs, talk camera, rails, badge (scene, with Undo)")]
    static void Do3() => Run(T3, L3, CounterAndDrinks, CounterPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "3 - Undo (scene)")]
    static void Undo3() => UndoRun(T3, L3, rebake: false);

    [MenuItem(Menu + "4 - The cafe room: plaster, stools back, rug, ledge, mats (scene, with Undo; routes re-baked)")]
    static void Do4() => Run(T4, L4, CafeRoom, RoomPhotos, guards: true, rebake: true, roomPhotos: true);
    [MenuItem(Menu + "4 - Undo (scene; routes re-baked)")]
    static void Undo4() => UndoRun(T4, L4, rebake: true);

    [MenuItem(Menu + "5 - The patio and the shopfront: flower, table, brass, planters, chairs (scene and materials, with Undo)")]
    static void Do5() => Run(T5, L5, Patio, PatioPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "5 - Undo (scene and materials)")]
    static void Undo5() => UndoRun(T5, L5, rebake: false);

    [MenuItem(Menu + "6 - The streets: West Street's trees, the hill streets, the island's bushes (scene, with Undo)")]
    static void Do6() => Run(T6, L6, Streets, StreetPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "6 - Undo (scene)")]
    static void Undo6() => UndoRun(T6, L6, rebake: false);

    [MenuItem(Menu + "7 - Grace's house: yellow paint, porch lantern, lampshades, door panel, side wall (scene and materials, with Undo)")]
    static void Do7() => Run(T7, L7, GraceHouse, GracePhotos, guards: false, rebake: false);
    [MenuItem(Menu + "7 - Undo (scene and materials)")]
    static void Undo7() => UndoRun(T7, L7, rebake: false);

    [MenuItem(Menu + "8 - The bins at night: the open lid's underside (scene, with Undo)")]
    static void Do8() => Run(T8, L8, Bins, BinsPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "8 - Undo (scene)")]
    static void Undo8() => UndoRun(T8, L8, rebake: false);

    [MenuItem(Menu + "9 - Softer window shadows (URP settings, with Undo)")]
    static void Do9() => Run(T9, L9, Shadows, ShadowPhotos, guards: false, rebake: false);
    [MenuItem(Menu + "9 - Undo (URP settings)")]
    static void Undo9() => UndoRun(T9, L9, rebake: false);

    [MenuItem(Menu + "1 - People: twin walkers apart, more colours for the guests (scene and prefabs, with Undo)", true)]
    [MenuItem(Menu + "1 - Undo (scene and prefabs)", true)]
    [MenuItem(Menu + "2 - The bench close-up: lamp shadows, desk colours, the strip, the donor box (scene, with Undo)", true)]
    [MenuItem(Menu + "2 - Undo (scene)", true)]
    [MenuItem(Menu + "3 - The counter and the drinks machine: cups, bulbs, talk camera, rails, badge (scene, with Undo)", true)]
    [MenuItem(Menu + "3 - Undo (scene)", true)]
    [MenuItem(Menu + "4 - The cafe room: plaster, stools back, rug, ledge, mats (scene, with Undo; routes re-baked)", true)]
    [MenuItem(Menu + "4 - Undo (scene; routes re-baked)", true)]
    [MenuItem(Menu + "5 - The patio and the shopfront: flower, table, brass, planters, chairs (scene and materials, with Undo)", true)]
    [MenuItem(Menu + "5 - Undo (scene and materials)", true)]
    [MenuItem(Menu + "6 - The streets: West Street's trees, the hill streets, the island's bushes (scene, with Undo)", true)]
    [MenuItem(Menu + "6 - Undo (scene)", true)]
    [MenuItem(Menu + "7 - Grace's house: yellow paint, porch lantern, lampshades, door panel, side wall (scene and materials, with Undo)", true)]
    [MenuItem(Menu + "7 - Undo (scene and materials)", true)]
    [MenuItem(Menu + "8 - The bins at night: the open lid's underside (scene, with Undo)", true)]
    [MenuItem(Menu + "8 - Undo (scene)", true)]
    [MenuItem(Menu + "9 - Softer window shadows (URP settings, with Undo)", true)]
    [MenuItem(Menu + "9 - Undo (URP settings)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== running a step

    // Each step is a survey (finds and checks; nothing changes) that returns the change to make. The change runs inside the
    // shared step's undo group; the extra record (what the shared record can't hold) is written once the step has passed.
    static Saved pending;
    static string pendingLabel;

    static void Run(string title, string label, Func<Shared.Run, Action<Shared.Run>> survey, Action<Shared.Run> photos,
                    bool guards, bool rebake, bool roomPhotos = false)
    {
        Action<Shared.Run> change = null;
        pending = Load(label);
        pendingLabel = label;
        Shared.Step(title, LogPrefix + label,
            run => change = survey(run),
            run => change(run),
            run =>
            {
                if (run.Phase == "2-after") Flush(run);   // only reached when the step passed: the extra record, the assets saved
                if (photos != null) photos(run);
            },
            guards: guards, rebake: rebake, roomPhotos: roomPhotos);
        pending = null;
    }

    static void Flush(Shared.Run run)
    {
        if (pending == null) return;
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(UndoFile(pendingLabel), JsonUtility.ToJson(pending, true));
        AssetDatabase.SaveAssets();
        run.Info($"The extra record (what the shared record can't hold): {pending.items.Count} item(s), in {UndoFile(pendingLabel)}.");
    }

    static void UndoRun(string title, string label, bool rebake)
    {
        Transform cafe = CafeRoot();
        bool had = cafe != null && Shared.RecordOf(cafe, title) != null;
        if (had) Shared.UndoStep(title, LogPrefix + label, null, rebake);
        if (had && cafe != null && Shared.RecordOf(cafe, title) != null) return;   // refused (a later step first): its message is in the Console
        Saved saved = Load(label);
        if (saved.items.Count == 0)
        {
            Debug.Log($"[{title}] {(had ? "UNDONE (scene). Save the scene." : "Nothing to undo: it hasn't been done here.")}");
            return;
        }
        var lines = new List<string>();
        for (int i = saved.items.Count - 1; i >= 0; i--)
        {
            try { lines.Add(Restore(saved.items[i])); }
            catch (Exception e) { lines.Add($"  could not put back {saved.items[i].what}: {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        string file = UndoFile(label);
        try { File.Move(file, file + ".undone-" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)); } catch (Exception) { }
        if (SceneManager.GetActiveScene().IsValid()) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string text = $"UNDONE: {title}. Put back {saved.items.Count} item(s) the shared record doesn't hold:\n" + string.Join("\n", lines) + "\nSave the scene.";
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(Path.Combine(LogRoot, label + "-undo-last.txt"), text);
        Debug.Log($"[{title}] " + text);
    }

    static Transform CafeRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Shared.CafeRootName);
        return root != null ? root.transform : null;
    }

    // ================================================================== the extra record

    [Serializable]
    sealed class Saved { public List<Item> items = new List<Item>(); }

    [Serializable]
    sealed class Item
    {
        public string kind;      // "prop": a property of a scene object or an asset; "asset": a whole asset (material, settings) as JSON;
                                 // "palette": a prefab's palette (Palette Base | the alternates' GUIDs)
        public string id;        // GlobalObjectId, or the prefab's path for "palette"
        public string path;      // the property's path ("prop")
        public string type;      // its SerializedPropertyType ("prop")
        public string value;     // the value before
        public string what;      // in words, for the report
    }

    static string UndoFile(string label) => Path.Combine(LogRoot, label + "-undo.json");

    static Saved Load(string label)
    {
        try
        {
            string file = UndoFile(label);
            if (File.Exists(file)) return JsonUtility.FromJson<Saved>(File.ReadAllText(file)) ?? new Saved();
        }
        catch (Exception e) { Debug.LogWarning("[Art fixes] couldn't read the extra record: " + e.Message); }
        return new Saved();
    }

    static string Id(Object o) => o != null ? GlobalObjectId.GetGlobalObjectIdSlow(o).ToString() : "";

    static Object FromId(string id)
    {
        if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId g)) return null;
        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g);
    }

    static bool Has(string kind, string id, string path) => pending.items.Any(i => i.kind == kind && i.id == id && i.path == path);

    // A property of a scene object (or an asset) set, its value before kept in the extra record (the first value only: a
    // second run never overwrites what it was before the first). Recorded in the step's undo group too.
    static void SetProp(Shared.Run run, Object target, string propertyPath, Action<SerializedProperty> set, string why)
    {
        Require(run, target != null, "SetProp on nothing: " + why);
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(propertyPath);
        Require(run, p != null, $"{target.name} has no property {propertyPath} ({target.GetType().Name}).");
        string before = Encode(p);
        set(p);
        if (Encode(p) == before) return;
        string id = Id(target);
        if (!Has("prop", id, propertyPath))
            pending.items.Add(new Item { kind = "prop", id = id, path = propertyPath, type = p.propertyType.ToString(), value = before, what = $"{target.name}: {propertyPath} ({why})" });
        so.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        run.Info($"{target.name}: {propertyPath} set ({why}).");
    }

    // A whole asset (a material, a pipeline asset) changed by `edit`, the asset before kept as JSON.
    static void EditAsset(Shared.Run run, Object asset, Action edit, string why)
    {
        Require(run, asset != null && AssetDatabase.Contains(asset), "EditAsset on something that isn't an asset: " + why);
        string id = Id(asset);
        string before = EditorJsonUtility.ToJson(asset);
        Undo.RecordObject(asset, run.Title);
        edit();
        if (EditorJsonUtility.ToJson(asset) == before) return;
        if (!Has("asset", id, "")) pending.items.Add(new Item { kind = "asset", id = id, path = "", value = before, what = $"{asset.name} ({why})" });
        EditorUtility.SetDirty(asset);
        run.Info($"{AssetDatabase.GetAssetPath(asset)}: changed ({why}).");
    }

    static string Restore(Item item)
    {
        if (item.kind == "palette")
        {
            string[] parts = (item.value ?? "").Split('|');
            Material[] alternates = parts.Length > 1 && parts[1].Length > 0
                ? parts[1].Split(',').Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m => m != null).ToArray()
                : Array.Empty<Material>();
            WritePalette(item.id, parts[0], alternates);
            return $"  {item.what}: put back";
        }
        Object target = FromId(item.id);
        if (target == null) return $"  {item.what}: no longer there (nothing to put back)";
        if (item.kind == "asset")
        {
            Undo.RecordObject(target, "Art fixes - Undo");
            EditorJsonUtility.FromJsonOverwrite(item.value, target);
            EditorUtility.SetDirty(target);
            return $"  {item.what}: put back";
        }
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(item.path);
        if (p == null) return $"  {item.what}: the property is gone";
        Decode(p, item.value);
        so.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        return $"  {item.what}: put back";
    }

    static string Encode(SerializedProperty p)
    {
        var c = CultureInfo.InvariantCulture;
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask: return p.intValue.ToString(c);
            case SerializedPropertyType.Boolean: return p.boolValue ? "1" : "0";
            case SerializedPropertyType.Float: return p.floatValue.ToString("R", c);
            case SerializedPropertyType.String: return p.stringValue ?? "";
            case SerializedPropertyType.Color: { Color v = p.colorValue; return string.Join(",", new[] { v.r, v.g, v.b, v.a }.Select(f => f.ToString("R", c))); }
            case SerializedPropertyType.Vector2: { Vector2 v = p.vector2Value; return string.Join(",", new[] { v.x, v.y }.Select(f => f.ToString("R", c))); }
            case SerializedPropertyType.Vector3: { Vector3 v = p.vector3Value; return string.Join(",", new[] { v.x, v.y, v.z }.Select(f => f.ToString("R", c))); }
            case SerializedPropertyType.Quaternion: { Quaternion v = p.quaternionValue; return string.Join(",", new[] { v.x, v.y, v.z, v.w }.Select(f => f.ToString("R", c))); }
            case SerializedPropertyType.ObjectReference: return Id(p.objectReferenceValue);
            default: throw new InvalidOperationException($"{p.propertyPath}: a {p.propertyType} can't be recorded");
        }
    }

    static void Decode(SerializedProperty p, string s)
    {
        var c = CultureInfo.InvariantCulture;
        float[] Floats() => s.Split(',').Select(x => float.Parse(x, c)).ToArray();
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask: p.intValue = int.Parse(s, c); break;
            case SerializedPropertyType.Boolean: p.boolValue = s == "1"; break;
            case SerializedPropertyType.Float: p.floatValue = float.Parse(s, c); break;
            case SerializedPropertyType.String: p.stringValue = s; break;
            case SerializedPropertyType.Color: { float[] f = Floats(); p.colorValue = new Color(f[0], f[1], f[2], f[3]); break; }
            case SerializedPropertyType.Vector2: { float[] f = Floats(); p.vector2Value = new Vector2(f[0], f[1]); break; }
            case SerializedPropertyType.Vector3: { float[] f = Floats(); p.vector3Value = new Vector3(f[0], f[1], f[2]); break; }
            case SerializedPropertyType.Quaternion: { float[] f = Floats(); p.quaternionValue = new Quaternion(f[0], f[1], f[2], f[3]); break; }
            case SerializedPropertyType.ObjectReference: p.objectReferenceValue = FromId(s); break;
        }
    }

    // ================================================================== finding things (by name, anywhere in the scene)

    static void Require(Shared.Run run, bool ok, string reason) => run.Require(ok, reason);

    static IEnumerable<Transform> Everything(Shared.Run run) =>
        run.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));

    static List<Transform> Named(Shared.Run run, string name) => Everything(run).Where(t => t.name == name).ToList();

    static List<Transform> Starting(Shared.Run run, string prefix) => Everything(run).Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    // The one called `name` whose parent is called `parentName` (anywhere), or null.
    static Transform Under(Shared.Run run, string parentName, string name) =>
        Everything(run).FirstOrDefault(t => t.name == name && t.parent != null && t.parent.name == parentName);

    // A material on a renderer in the scene, by name (the palettes' sub-assets are found where they're used).
    static Material SceneMaterial(Shared.Run run, string name) =>
        Shared.InScene<Renderer>(run).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == name);

    static Material KitMaterial(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // A material of the art fixes' own (Assets/Art/Materials/ArtFixes/<name>.mat): made once from `source`, then `look`
    // applied (the same look on every run, so a second run changes nothing).
    static Material Derived(Shared.Run run, string name, Material source, Action<Material> look)
    {
        Require(run, source != null, $"There's no material to make '{name}' from.");
        EnsureFolder(MaterialsFolder);
        string path = MaterialsFolder + "/" + string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '\'' ? '-' : ch)) + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(source) { name = name };
            AssetDatabase.CreateAsset(m, path);
            run.Info($"made {path} (from {source.name})");
        }
        m.name = name;
        look(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Colour(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static Material Lit(Shared.Run run, string name, Color colour, float smoothness)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Require(run, lit != null, "URP's Lit shader wasn't found.");
        var seed = new Material(lit);
        try
        {
            return Derived(run, name, seed, m =>
            {
                m.shader = lit;
                Colour(m, colour);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            });
        }
        finally { Object.DestroyImmediate(seed); }
    }

    // Every slot of `r` wearing a material `which` picks, swapped for `to` (through the shared record).
    static int SwapSlots(Shared.Run run, Renderer r, Func<Material, bool> which, Material to, string why)
    {
        if (r == null || to == null) return 0;
        Material[] mats = r.sharedMaterials;
        int n = 0;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != to && which(mats[i])) { mats[i] = to; n++; }
        if (n > 0) Shared.Repaint(run, r, mats, why);
        return n;
    }

    static bool Named(Material m, string name) => m != null && m.name == name;

    static Bounds WorldBounds(Transform root, bool includeInactive = true)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(includeInactive).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // A copy of a scene piece (a Kenney tree or bush as the CC0 refresh placed it, with its palette), sized to `size` (world
    // metres, the renderers' box), its foot at `foot`, turned `yaw`. Colliders and scripts are left out.
    static GameObject Copy(Shared.Run run, Transform template, Transform parent, string name, Vector3 foot, float yaw, Vector3 size)
    {
        GameObject go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);   // 26 Oct 2026 (compile fix: not "b", the bounds below)
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = template.lossyScale;
        if (parent != null) go.transform.localScale = Vector3.Scale(template.lossyScale, Inverse(parent.lossyScale));
        Bounds b = WorldBounds(go.transform);
        if (b.size.x > 1e-4f && b.size.y > 1e-4f && b.size.z > 1e-4f)
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z));
        b = WorldBounds(go.transform);
        go.transform.position += foot - new Vector3(b.center.x, b.min.y, b.center.z);
        go.transform.RotateAround(foot, Vector3.up, yaw);
        Shared.Made(run, go);
        return go;
    }

    static Vector3 Inverse(Vector3 v) => new Vector3(Mathf.Abs(v.x) > 1e-6f ? 1f / v.x : 1f, Mathf.Abs(v.y) > 1e-6f ? 1f / v.y : 1f, Mathf.Abs(v.z) > 1e-6f ? 1f / v.z : 1f);

    // A café-kit model (Assets/Art/Models/CafeKit/<piece>.fbx, materials as Kit 1 mapped them) placed with its origin at
    // `foot`, its front (+Z) turned `yaw` degrees from north.
    static GameObject Kit(Shared.Run run, string piece, Transform parent, string name, Vector3 foot, float yaw)
    {
        string path = Shared.KitModels + "/" + piece + ".fbx";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(run, asset != null, $"{path} isn't imported (Fixit Fidget > Cafe redesign > Kit 1).");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
        Shared.Made(run, go);
        return go;
    }

    static GameObject Box(Shared.Run run, Transform parent, string name, Vector3 world, Vector3 size, Quaternion rotation, Material m,
                          PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(world, rotation);
        go.transform.localScale = Vector3.Scale(size, Inverse(parent != null ? parent.lossyScale : Vector3.one));
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (parent == null || !run.Record.made.Any(x => x != null && go.transform.IsChildOf(x.transform) && x != go)) Shared.Made(run, go);
        return go;
    }

    // A group of the art fixes' own (made, so the Undo deletes it with everything in it).
    static Transform Group(Shared.Run run, Transform parent, string name)
    {
        Transform there = parent != null ? parent.Find(name) : null;
        if (there != null) return there;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Shared.Made(run, go);
        return go.transform;
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        EnsureFolder(ModelsFolder);
        if (AssetDatabase.LoadMainAssetAtPath(GeometryAsset) == null) { AssetDatabase.CreateAsset(mesh, GeometryAsset); return mesh; }
        Mesh old = AssetDatabase.LoadAllAssetsAtPath(GeometryAsset).OfType<Mesh>().FirstOrDefault(m => m.name == mesh.name && m != mesh);
        if (old != null && AssetDatabase.IsSubAsset(old)) Object.DestroyImmediate(old, true);
        else if (old != null)
        {
            // The main object has this name: copy into it.
            old.Clear();
            old.SetVertices(mesh.vertices);
            old.subMeshCount = mesh.subMeshCount;
            CopyChannels(mesh, old);
            for (int s = 0; s < mesh.subMeshCount; s++) old.SetTriangles(mesh.GetTriangles(s), s);
            old.RecalculateBounds();
            EditorUtility.SetDirty(old);
            Object.DestroyImmediate(mesh);
            return old;
        }
        AssetDatabase.AddObjectToAsset(mesh, GeometryAsset);
        return mesh;
    }

    static void CopyChannels(Mesh from, Mesh to)
    {
        if (from.normals.Length == from.vertexCount) to.normals = from.normals;
        if (from.tangents.Length == from.vertexCount) to.tangents = from.tangents;
        if (from.colors.Length == from.vertexCount) to.colors = from.colors;
        var uv = new List<Vector4>();
        for (int ch = 0; ch < 4; ch++) { from.GetUVs(ch, uv); if (uv.Count == from.vertexCount) to.SetUVs(ch, uv); }
    }

    // The connected parts (corners at the same place are one corner) of one sub-mesh: each part is a list of triangle
    // starts into that sub-mesh's index list.
    static List<List<int>> Parts(Mesh mesh, int sub)
    {
        Vector3[] v = mesh.vertices;
        int[] tris = mesh.GetTriangles(sub);
        var weld = new int[v.Length];
        var keyOf = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = (Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
            if (!keyOf.TryGetValue(key, out int w)) keyOf[key] = w = keyOf.Count;
            weld[i] = w;
        }
        var parent = Enumerable.Range(0, keyOf.Count).ToArray();
        int Root(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = Root(weld[tris[t]]);
            for (int k = 1; k < 3; k++) { int b = Root(weld[tris[t + k]]); if (a != b) parent[b] = a; }
        }
        var parts = new Dictionary<int, List<int>>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            int r = Root(weld[tris[t]]);
            if (!parts.TryGetValue(r, out List<int> list)) parts[r] = list = new List<int>();
            list.Add(t);
        }
        return parts.Values.ToList();
    }

    // A part's box: in the mesh's own space, or in the world through `toWorld`.
    static Bounds PartBounds(Mesh mesh, int sub, List<int> part, Matrix4x4? toWorld = null)
    {
        Vector3[] v = mesh.vertices;
        int[] tris = mesh.GetTriangles(sub);
        Vector3 P(int i) => toWorld.HasValue ? toWorld.Value.MultiplyPoint3x4(v[i]) : v[i];
        var b = new Bounds(P(tris[part[0]]), Vector3.zero);
        foreach (int t in part) for (int k = 0; k < 3; k++) b.Encapsulate(P(tris[t + k]));
        return b;
    }

    // The bench's station (the work surface with a stand point), as the hand-over probe finds it.
    static StationInteractable BenchStation() =>
        Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.IsWorkSurface && s.StandPoint != null && s.GetComponent<BeverageStation>() == null);

    // ================================================================== 1 - People (a15, a46)

    const string AltsFolder = "Assets/Synty/PolygonCity/Materials/Alts";
    static readonly string[] OwnerPrefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };

    static Action<Shared.Run> People(Shared.Run run)
    {
        // ---- a15: twin walkers. The street's walkers (StreetLife's actors with a fixed city look): no two in one look, and no
        // two on the same route starting at the same point. ----
        var regularsLooks = new HashSet<string>(AssetDatabase.FindAssets("t:CustomerProfile").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CustomerProfile>).Where(p => p != null && !string.IsNullOrEmpty(p.StandInLook)).Select(p => p.StandInLook));
        run.Info($"a15: the regulars' stand-in looks (never given to a walker): {(regularsLooks.Count > 0 ? string.Join(", ", regularsLooks) : "none")}.");
        var walkers = new List<(StreetLife life, int index, PolygonNpcVisual visual, string look)>();
        var lookPlan = new List<(PolygonNpcVisual visual, int to, string from, string toName)>();
        var phasePlan = new List<(StreetLife life, int index, float to, float from)>();
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            for (int i = 0; i < life.actors.Count; i++)
            {
                StreetLife.Actor a = life.actors[i];
                if (a == null || a.actor == null) continue;
                PolygonNpcVisual v = a.actor.GetComponentInChildren<PolygonNpcVisual>(true);
                if (v == null) continue;
                int fixedIndex = new SerializedObject(v).FindProperty("fixedAppearance").intValue;
                if (fixedIndex < 0) continue;
                string look = LookName(v, Effective(v, fixedIndex, regularsLooks));
                walkers.Add((life, i, v, look));
            }
        run.Info($"a15: {walkers.Count} street walker(s) with a fixed look: " + string.Join("; ", walkers.Select(w => $"{w.visual.name} in {w.look}")));
        var used = new HashSet<string>(walkers.Select(w => w.look));
        var seen = new HashSet<string>();
        foreach (var w in walkers)
        {
            if (seen.Add(w.look)) continue;
            // A twin: the least-used look of the same body (a woman's for a woman's), never a regular's stand-in.
            bool woman = PolygonNpcVisual.IsWomansLook(w.look);
            var so = new SerializedObject(w.visual);
            SerializedProperty list = so.FindProperty("appearancePrefabs");
            int pick = -1;
            for (int k = 0; k < list.arraySize && pick < 0; k++)
            {
                var look = list.GetArrayElementAtIndex(k).objectReferenceValue as GameObject;
                if (look == null || used.Contains(look.name) || regularsLooks.Contains(look.name)) continue;
                if (PolygonNpcVisual.IsWomansLook(look.name) != woman) continue;
                pick = k;
            }
            if (pick < 0) { run.Info($"a15: no free {(woman ? "woman's" : "man's")} look left for {w.visual.name}; it keeps {w.look}."); continue; }
            string toName = ((GameObject)list.GetArrayElementAtIndex(pick).objectReferenceValue).name;
            used.Add(toName);
            seen.Add(toName);
            lookPlan.Add((w.visual, pick, w.look, toName));
        }
        // Same route (the same first waypoint), start points less than a fifth of the route apart: the later one moved on by half.
        for (int i = 0; i < walkers.Count; i++)
            for (int j = 0; j < i; j++)
            {
                StreetLife.Actor a = walkers[i].life.actors[walkers[i].index], b = walkers[j].life.actors[walkers[j].index];
                if (a.waypoints.Length == 0 || b.waypoints.Length == 0 || a.waypoints[0] != b.waypoints[0]) continue;
                float gap = Mathf.Abs(Mathf.DeltaAngle(a.startPhase * 360f, b.startPhase * 360f)) / 360f;
                if (gap >= .2f || phasePlan.Any(p => p.life == walkers[i].life && p.index == walkers[i].index)) continue;
                float to = a.openRoute ? Mathf.Repeat(b.startPhase + .45f, .95f) : Mathf.Repeat(b.startPhase + .5f, 1f);
                phasePlan.Add((walkers[i].life, walkers[i].index, to, a.startPhase));
            }
        run.Info($"a15: {lookPlan.Count} walker(s) to change look: " + string.Join("; ", lookPlan.Select(p => $"{p.visual.name}: {p.from} -> {p.toName}")) +
                 $"; {phasePlan.Count} start point(s) to move apart.");

        // ---- a46: more colours for the guests: the POLYGON City atlas's alternates as palette swaps on Customer and Patron. ----
        string atlas = null;
        var alternates = new List<Material>();
        if (!AssetDatabase.IsValidFolder(AltsFolder)) run.Info($"a46: {AltsFolder} isn't in this project: no palette swaps (nothing changes for a46).");
        else
        {
            List<Material> alts = AssetDatabase.FindAssets("t:Material", new[] { AltsFolder }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m != null).OrderBy(m => m.name, StringComparer.Ordinal).ToList();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string look in PolygonNpcSetup.WalkInLooks)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(look));
                if (prefab == null) continue;
                foreach (string n in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())
                    counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
            }
            // The atlas most looks are drawn with; its alternates are the pack's other colourings of the same atlas (same
            // family: the name without its last letter, "..._01_A" -> "..._01_"), matched loosely because the 14 Oct survey's
            // exact match found none on his Mac (no palette was ever written to the prefabs).
            foreach (var kv in counts.OrderByDescending(kv => kv.Value))
            {
                string family = Family(kv.Key);
                List<Material> same = alts.Where(m => m.name != kv.Key && Family(m.name) == family).ToList();
                if (same.Count == 0) continue;
                atlas = kv.Key;
                alternates = same;
                break;
            }
            run.Info($"a46: the looks' materials: {string.Join(", ", counts.Select(kv => $"{kv.Key} x{kv.Value}"))}. " +
                     (atlas != null ? $"The atlas {atlas}, its alternates: {string.Join(", ", alternates.Select(m => m.name))}."
                                    : $"None of them has alternates in {AltsFolder} ({string.Join(", ", alts.Select(m => m.name))}): no palette swaps."));
        }

        return r =>
        {
            foreach (var p in lookPlan)
                SetProp(r, p.visual, "fixedAppearance", sp => sp.intValue = p.to, $"a15 twin walkers: {p.from} -> {p.toName}");
            foreach (var p in phasePlan)
            {
                int index = p.index;
                float to = p.to;
                SetProp(r, p.life, $"actors.Array.data[{index}].startPhase", sp => sp.floatValue = to, $"a15 twin walkers: start {F(p.from, "0.00")} -> {F(to, "0.00")} of the route");
            }
            if (atlas != null)
                foreach (string path in OwnerPrefabs)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    PolygonNpcVisual v = asset != null ? asset.GetComponent<PolygonNpcVisual>() : null;
                    if (v == null) { r.Info($"a46: {path} has no PolygonNpcVisual: skipped."); continue; }
                    var so = new SerializedObject(v);
                    string baseBefore = so.FindProperty("paletteBase").stringValue;
                    SerializedProperty list = so.FindProperty("paletteAlternates");
                    var before = new List<string>();
                    for (int i = 0; i < list.arraySize; i++)
                        before.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(list.GetArrayElementAtIndex(i).objectReferenceValue)));
                    if (baseBefore == atlas && before.Count == alternates.Count) { r.Info($"a46: {path} already has the palette."); continue; }
                    if (!Has("palette", path, ""))
                        pending.items.Add(new Item { kind = "palette", id = path, path = "", value = baseBefore + "|" + string.Join(",", before), what = $"{Path.GetFileNameWithoutExtension(path)}'s palette (a46)" });
                    // A prefab is saved at once (the step's rollback can't take it back), so its record is written at once too.
                    Directory.CreateDirectory(LogRoot);
                    File.WriteAllText(UndoFile(pendingLabel), JsonUtility.ToJson(pending, true));
                    WritePalette(path, atlas, alternates.ToArray());
                    r.Info($"a46: {Path.GetFileNameWithoutExtension(path)}: Palette Base '{atlas}', {alternates.Count} alternate(s). The guests' looks are dealt as before; " +
                           "the colouring is dealt fewest-first among people in the same look (PolygonNpcVisual.DealPalette).");
                }
            r.Check(lookPlan.All(p => new SerializedObject(p.visual).FindProperty("fixedAppearance").intValue == p.to), "a15: every twin walker wears another look now");
        };
    }

    static string Family(string name) => string.IsNullOrEmpty(name) || name.Length < 2 ? name : name.Substring(0, name.Length - 1);

    static int Effective(PolygonNpcVisual v, int index, HashSet<string> standIns)
    {
        var list = new SerializedObject(v).FindProperty("appearancePrefabs");
        int count = list.arraySize;
        if (index < 0 || index >= count) return index;
        for (int step = 0; step < count; step++)
        {
            int i = (index + step) % count;
            var look = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (look == null || !standIns.Contains(look.name)) return i;
        }
        return index;
    }

    static string LookName(PolygonNpcVisual v, int index)
    {
        var list = new SerializedObject(v).FindProperty("appearancePrefabs");
        return index >= 0 && index < list.arraySize && list.GetArrayElementAtIndex(index).objectReferenceValue != null
            ? list.GetArrayElementAtIndex(index).objectReferenceValue.name : "(none)";
    }

    // Palette Base and Palette Alternates on a prefab's PolygonNpcVisual (as NPC Wave 2 6b writes them).
    static void WritePalette(string path, string atlas, Material[] alternates)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            PolygonNpcVisual visual = root.GetComponent<PolygonNpcVisual>();
            if (visual == null) return;
            var so = new SerializedObject(visual);
            so.FindProperty("paletteBase").stringValue = atlas ?? "";
            SerializedProperty list = so.FindProperty("paletteAlternates");
            list.arraySize = alternates.Length;
            for (int i = 0; i < alternates.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = alternates[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void PeoplePhotos(Shared.Run run)
    {
        // The morning intro's first frame (street.row, DayIntro): West Street's pavement.
        Shared.Photo(run, "a15-west-street-pavement", new Vector3(-9f, 1.7f, -6.5f), new Vector3(-13f, 3.5f, 14f), 38f);
    }

    // ================================================================== 2 - The bench close-up (a24, a30, a32, a33)

    static Action<Shared.Run> Bench(Shared.Run run)
    {
        // a24: the magnifier lamp's own shadow on the wood and the mat (its light casts none; the room's lights cast its shape).
        Transform lamp = Starting(run, "CK_BenchLamp").FirstOrDefault(t => t.gameObject.activeInHierarchy) ?? Starting(run, "CK_BenchLamp").FirstOrDefault();
        Require(run, lamp != null, "a24: no CK_BenchLamp in the scene (Bar 1 places it).");
        Renderer[] lampParts = lamp.GetComponentsInChildren<Renderer>(true);
        run.Info($"a24: {Shared.PathOf(lamp)}: {lampParts.Length} renderer(s), casting shadows: {lampParts.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)}.");

        // a30: Ace's desk corner's loud colours toward the kit's sage, brick and mustard (this one desk; the materials stay).
        Transform desk = Starting(run, "LI_DeskCorner").FirstOrDefault();
        Require(run, desk != null, "a30: no LI_DeskCorner in the scene (Cafe dressing 1 places it).");
        Renderer[] deskParts = desk.GetComponentsInChildren<Renderer>(true);
        run.Info($"a30: {Shared.PathOf(desk)}: materials {string.Join(", ", deskParts.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())}.");

        // a32: the strip under the bench top: the bench's under-shelf light strip (CF_LightStrip, emission 1.6: the one pure white
        // square) and the power strip's sockets (DC_Ceramic, near-white on a dark bar). R4 read it as a power strip that can stay;
        // R3 as blank labels. Kept, toned: the sockets a dark socket grey, the strip glowing a soft warm.
        Transform bench = Starting(run, "CK_RepairBench").FirstOrDefault();
        Require(run, bench != null, "a32: no CK_RepairBench in the scene (Bar 1 places it).");
        Renderer[] benchParts = bench.GetComponentsInChildren<Renderer>(true);
        int sockets = benchParts.Sum(r => r.sharedMaterials.Count(m => Named(m, "DC_Ceramic"))), strips = benchParts.Sum(r => r.sharedMaterials.Count(m => Named(m, "CF_LightStrip")));
        run.Info($"a32: {Shared.PathOf(bench)}: DC_Ceramic slot(s) {sockets}, CF_LightStrip slot(s) {strips}.");
        Require(run, sockets + strips > 0, "a32: the bench wears neither DC_Ceramic nor CF_LightStrip: is it the 20 Oct bench?");

        // a33: the donor parts box under the bench: switched off (a Blender tangle of cable and phones only if he wants it back).
        Transform donor = Starting(run, "LI_DonorParts").FirstOrDefault();
        run.Info(donor != null ? $"a33: {Shared.PathOf(donor)} ({(donor.gameObject.activeSelf ? "on" : "already off")})." : "a33: no LI_DonorParts in the scene: nothing to switch off.");

        return r =>
        {
            foreach (Renderer part in lampParts)
                if (part.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    SetProp(r, part, "m_CastShadows", p => p.intValue = 0, "a24: the magnifier lamp casts no shadow into the close-up (\"lamp shadows like stains on the bench\")");

            Material brick = Lit(r, "Art fix a30 - desk spool brick", new Color(.56f, .29f, .22f), .35f);
            Material sage = Lit(r, "Art fix a30 - desk sage", new Color(.42f, .50f, .40f), .35f);
            Material mustard = Lit(r, "Art fix a30 - desk mustard", new Color(.74f, .58f, .25f), .35f);
            foreach (Renderer part in deskParts)
            {
                SwapSlots(r, part, m => Named(m, "LI_WireRed"), brick, "a30: the red spool toward the kit's brick");
                SwapSlots(r, part, m => Named(m, "LI_WireGreen") || Named(m, "DC_Sticker_Green"), sage, "a30: the green spool and the mug toward the kit's sage");
                SwapSlots(r, part, m => Named(m, "LI_Yellow"), mustard, "a30: the yellow meter toward the kit's mustard");
            }

            Material socket = Lit(r, "Art fix a32 - bench socket grey", new Color(.34f, .33f, .31f), .3f);
            Material soft = Derived(r, "Art fix a32 - bench light strip (soft)", KitMaterial("Assets/Art/Materials/Cafe/CF_LightStrip.mat") ?? socket, m =>
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(.55f, .45f, .33f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            foreach (Renderer part in benchParts)
            {
                SwapSlots(r, part, m => Named(m, "DC_Ceramic"), socket, "a32: the power strip's sockets a dark socket grey, not blank white labels");
                SwapSlots(r, part, m => Named(m, "CF_LightStrip"), soft, "a32: the under-shelf strip a soft warm glow, not a blown-out white square");
            }

            if (donor != null) Shared.SwitchOff(r, donor.gameObject, "a33: the donor parts box under the bench (plain boxes, \"yes: take it out\")");

            r.Check(lampParts.All(p => p.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "a24: the magnifier lamp casts no shadow");
            r.Check(!deskParts.SelectMany(p => p.sharedMaterials).Any(m => Named(m, "LI_WireRed") || Named(m, "LI_Yellow")), "a30: no pure red spool or yellow meter on the desk");
            r.Check(donor == null || !donor.gameObject.activeSelf, "a33: the donor parts box is off (not deleted)");
        };
    }

    static void BenchPhotos(Shared.Run run)
    {
        StationInteractable bench = BenchStation();
        Vector3 stand = bench != null && bench.StandPoint != null ? bench.StandPoint.position : new Vector3(-4.2f, 0f, 15.1f);
        Vector3 into = bench != null && bench.StandPoint != null ? Vector3.ProjectOnPlane(bench.StandPoint.forward, Vector3.up).normalized : Vector3.back;
        if (into.sqrMagnitude < .5f) into = Vector3.back;
        Vector3 eye = new Vector3(stand.x, stand.y + 1.62f, stand.z);
        Shared.Photo(run, "a24-a30-a32-the-bench-in-first-person", eye - into * .1f, eye + into * .9f + Vector3.down * .75f, 60f);
        Shared.Photo(run, "a32-a33-under-the-bench", eye + Vector3.down * .4f, eye + into * .7f + Vector3.down * 1.4f, 60f);
    }

    // ================================================================== 3 - The counter and the drinks machine (a09, a35, a36, a38, a39)

    sealed class BulbCut { public MeshFilter filter; public int sub; public Mesh mesh; public Bounds bulb; public Material material; public Renderer renderer; }

    static Action<Shared.Run> CounterAndDrinks(Shared.Run run)
    {
        // a09: the cup and lid station: its tubes brushed steel (the green sticker band already on them stays), its board walnut.
        Transform station = Starting(run, "LI_CupStation").FirstOrDefault();
        Require(run, station != null, "a09: no LI_CupStation in the scene (Cafe dressing 1 places it).");
        MeshFilter stationFilter = station.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null
            && f.GetComponent<MeshRenderer>().sharedMaterials.Any(m => Named(m, "CF_Black")));
        Require(run, stationFilter != null, "a09: LI_CupStation wears no CF_Black (the black tubes and board).");
        MeshRenderer stationRenderer = stationFilter.GetComponent<MeshRenderer>();
        int black = Array.FindIndex(stationRenderer.sharedMaterials, m => Named(m, "CF_Black"));
        Require(run, stationFilter.sharedMesh.isReadable, "a09: LI_CupStation's mesh can't be read (Read/Write off in its import).");
        List<List<int>> blackParts = Parts(stationFilter.sharedMesh, black);
        Matrix4x4 toWorld = stationFilter.transform.localToWorldMatrix;
        // The board: the part with the widest footprint (world x by z) among the lowest; the rest are the tubes (and their caps).
        List<int> board = blackParts.OrderByDescending(p => { Bounds b = PartBounds(stationFilter.sharedMesh, black, p, toWorld); return b.size.x * b.size.z - b.min.y * 10f; }).First();
        run.Info($"a09: CF_Black on {Shared.PathOf(stationFilter.transform)} is {blackParts.Count} part(s); the board is the widest ({board.Count} triangles, " +
                 $"{Shared.BoundsText(PartBounds(stationFilter.sharedMesh, black, board, toWorld))}), the tubes the other {blackParts.Count - 1}.");
        Require(run, blackParts.Count >= 2, "a09: CF_Black is one piece here: the board and the tubes can't be told apart (a Blender split, then).");

        // a35: the counter lamps' blocky egg bulb: the pendant mesh's small part on its own axis, below the shade's top, taken out
        // and replaced by a smooth sphere of the same size (every renderer that shares the mesh: the counter's and the tables').
        var pendants = Shared.InScene<MeshFilter>(run).Where(f => f.sharedMesh != null && f.name.StartsWith("Counter pendant", StringComparison.Ordinal)).ToList();
        Require(run, pendants.Count > 0, "a35: no 'Counter pendant' in the scene (Cafe furnishing > 4 hangs them).");
        Mesh pendantMesh = pendants[0].sharedMesh;
        Require(run, pendantMesh.isReadable, "a35: the pendant's mesh can't be read.");
        (int sub, List<int> part, Bounds box) bulb = FindBulb(run, pendantMesh);
        var users = Shared.InScene<MeshFilter>(run).Where(f => f.sharedMesh == pendantMesh).ToList();
        run.Info($"a35: the pendant mesh '{pendantMesh.name}' is on {users.Count} lamp(s) ({string.Join(", ", users.Take(8).Select(u => u.name))}{(users.Count > 8 ? ", ..." : "")}); " +
                 $"its bulb: sub-mesh {bulb.sub}, {bulb.part.Count} triangles, {Shared.BoundsText(bulb.box)} in the mesh's space.");

        // a36: the talk camera 20 cm further from Ace (ConversationController's Shoulder Offset, as the code's default now is).
        ConversationController[] talks = Shared.InScene<ConversationController>(run);
        run.Info($"a36: ConversationController(s): {talks.Length}.");

        // a38: the dispenser's two "Cup caddy rail" parts: switched off.
        List<Transform> rails = Starting(run, "Cup caddy rail");
        run.Info($"a38: {rails.Count} cup caddy rail(s): {string.Join("; ", rails.Select(Shared.PathOf))}.");
        Require(run, rails.Count > 0, "a38: no 'Cup caddy rail' (the drinks machine's model BeverageDispenserV2).");

        // a39: a small enamel ACE'S badge in the middle of the machine's plain cream header.
        Transform header = Named(run, "Ivory curved header").FirstOrDefault();
        Transform dispenser = header != null ? header.GetComponentInParent<BeverageStation>(true)?.transform : null;
        if (dispenser == null) dispenser = Named(run, "Beverage dispenser").FirstOrDefault();
        Require(run, header != null && dispenser != null, "a39: no 'Ivory curved header' under the beverage dispenser.");
        Material enamel = KitMaterial("Assets/Art/Materials/BeverageDispenserV2/BF_Enamel.mat");
        Require(run, enamel != null, "a39: BF_Enamel (the machine's own green enamel) isn't at Assets/Art/Materials/BeverageDispenserV2.");
        Require(run, TMP_Settings.defaultFontAsset != null, "a39: TextMesh Pro has no default font asset.");

        return r =>
        {
            // a09
            Mesh split = Object.Instantiate(stationFilter.sharedMesh);
            split.name = stationFilter.sharedMesh.name + " (board and tubes apart, art fix a09)";
            int[] tris = stationFilter.sharedMesh.GetTriangles(black);
            var boardTris = new List<int>();
            var tubeTris = new List<int>();
            var boardSet = new HashSet<int>(board);
            for (int t = 0; t < tris.Length; t += 3) (boardSet.Contains(t) ? boardTris : tubeTris).AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            int subs = split.subMeshCount;
            split.subMeshCount = subs + 1;
            split.SetTriangles(boardTris, black);
            split.SetTriangles(tubeTris, subs);
            split = SaveMesh(split);
            SetProp(r, stationFilter, "m_Mesh", p => p.objectReferenceValue = split, "a09: the board and the tubes as two sub-meshes");
            Material walnut = KitMaterial("Assets/Art/Materials/T2_Walnut.mat");
            Material steel = KitMaterial("Assets/Art/Materials/DC_Steel.mat");
            Require(r, walnut != null && steel != null, "a09: T2_Walnut or DC_Steel isn't in Assets/Art/Materials.");
            Material[] mats = stationRenderer.sharedMaterials.ToList().Concat(new[] { steel }).ToArray();
            mats[black] = walnut;
            Shared.Repaint(r, stationRenderer, mats, "a09: the board walnut, the tubes brushed steel (\"the black cup and lid station\")");

            // a35
            Mesh noBulb = Object.Instantiate(pendantMesh);
            noBulb.name = pendantMesh.name + " (no blocky bulb, art fix a35)";
            int[] all = pendantMesh.GetTriangles(bulb.sub);
            var keep = new List<int>();
            var cut = new HashSet<int>(bulb.part);
            for (int t = 0; t < all.Length; t += 3) if (!cut.Contains(t)) keep.AddRange(new[] { all[t], all[t + 1], all[t + 2] });
            noBulb.SetTriangles(keep, bulb.sub);
            noBulb = SaveMesh(noBulb);
            CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            foreach (MeshFilter f in users)
            {
                MeshRenderer mr = f.GetComponent<MeshRenderer>();
                Material bulbMaterial = mr != null && bulb.sub < mr.sharedMaterials.Length ? mr.sharedMaterials[bulb.sub] : null;
                SetProp(r, f, "m_Mesh", p => p.objectReferenceValue = noBulb, "a35: the pendant without its blocky bulb");
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.name = "Smooth bulb (art fix a35)";
                sphere.transform.SetParent(f.transform, false);
                sphere.transform.localPosition = bulb.box.center;
                sphere.transform.localRotation = Quaternion.identity;
                float d = Mathf.Max(bulb.box.size.x, bulb.box.size.z);
                sphere.transform.localScale = new Vector3(d, Mathf.Max(d, bulb.box.size.y), d);
                MeshRenderer sr = sphere.GetComponent<MeshRenderer>();
                sr.sharedMaterial = bulbMaterial;
                if (mr != null) { sr.shadowCastingMode = mr.shadowCastingMode; sr.receiveShadows = mr.receiveShadows; }
                Shared.Made(r, sphere);
                // The overhead view hides the pendants (CafeViewMode's Overhead Fixtures): the bulb goes with its pendant.
                if (view != null && mr != null && view.overheadFixtures.Contains(mr))
                    Shared.AddToList(r, view, "overheadFixtures", sr, "a35: the smooth bulb hides with its pendant in the overhead");
            }

            // a36
            foreach (ConversationController c in talks)
                SetProp(r, c, "shoulderOffset", p => p.vector3Value = ConversationController.ShoulderOffsetDefault,
                        "a36: the talk camera about 20 cm further from Ace's shoulder (\"Ace's head fills the talk camera\")");

            // a38
            foreach (Transform rail in rails)
                Shared.SwitchOff(r, rail.gameObject, "a38: a loose rod by the cup stack (\"two stray rods\"), touching nothing");

            // a39
            Bounds hb = header.GetComponent<Renderer>() != null ? header.GetComponent<Renderer>().bounds : WorldBounds(header);
            // The machine faces its root's -Z (BeverageStationSetup.AlignModel: the model's front is turned to Vector3.back).
            Vector3 front = -dispenser.forward;
            Vector3 centre = hb.center;
            float reach = Mathf.Abs(Vector3.Dot(hb.extents, new Vector3(Mathf.Abs(front.x), Mathf.Abs(front.y), Mathf.Abs(front.z))));
            Vector3 face = centre + front * (reach + .004f);
            var badge = new GameObject("ACE'S badge (art fix a39)");
            badge.transform.SetParent(dispenser, false);
            badge.transform.position = face;
            badge.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            Shared.Made(r, badge);
            Material rim = KitMaterial("Assets/Art/Materials/BeverageDispenserV2/BF_Chrome.mat") ?? enamel;
            Box(r, badge.transform, "Badge rim (chrome)", face - front * .001f, new Vector3(.17f, .055f, .004f), badge.transform.rotation, rim);
            Box(r, badge.transform, "Badge enamel", face + front * .0015f, new Vector3(.16f, .045f, .004f), badge.transform.rotation, enamel);
            var words = new GameObject("Badge words");
            words.transform.SetParent(badge.transform, false);
            words.transform.position = face + front * .0045f;
            words.transform.rotation = badge.transform.rotation;
            var text = words.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = "ACE'S";
            text.fontSize = 5;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = new Color(.87f, .80f, .62f);   // the machine's own cream (BF_Cream)
            text.rectTransform.sizeDelta = new Vector2(10, 3);
            text.ForceMeshUpdate();
            Vector3 size = text.textBounds.size;
            float fitted = Mathf.Min(.13f / Mathf.Max(.001f, size.x), .032f / Mathf.Max(.001f, size.y));
            words.transform.localScale = Vector3.one * fitted;
            Renderer tr = text.GetComponent<Renderer>();
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;

            r.Check(rails.All(t => !t.gameObject.activeSelf), "a38: the cup caddy rails are off (not deleted)");
            r.Check(users.All(f => f.sharedMesh == noBulb), "a35: every pendant on the shared mesh lost its blocky bulb");
            r.Check(talks.All(c => new SerializedObject(c).FindProperty("shoulderOffset").vector3Value == ConversationController.ShoulderOffsetDefault), "a36: the talk camera's offset is the new one");
        };
    }

    // The bulb in a pendant: the small part near the lamp's vertical axis, egg-shaped (not a thin cord), under the shade's top.
    static (int, List<int>, Bounds) FindBulb(Shared.Run run, Mesh mesh)
    {
        Bounds all = mesh.bounds;
        var candidates = new List<(int sub, List<int> part, Bounds b)>();
        for (int s = 0; s < mesh.subMeshCount; s++)
            foreach (List<int> part in Parts(mesh, s))
            {
                Bounds b = PartBounds(mesh, s, part);
                float width = Mathf.Max(b.size.x, b.size.z);
                bool onAxis = Mathf.Abs(b.center.x - all.center.x) < all.size.x * .08f && Mathf.Abs(b.center.z - all.center.z) < all.size.z * .08f;
                bool small = width < Mathf.Max(all.size.x, all.size.z) * .45f && width > Mathf.Max(all.size.x, all.size.z) * .04f;
                bool egg = b.size.y > width * .7f && b.size.y < width * 2.4f;
                bool low = b.max.y < all.max.y - all.size.y * .1f;
                if (onAxis && small && egg && low) candidates.Add((s, part, b));
            }
        Require(run, candidates.Count > 0, $"a35: no bulb-shaped part found in '{mesh.name}' (near its axis, egg-shaped, small): nothing changed.");
        // Of those, the lowest (a cap or a socket sits higher, at the shade's crown).
        var pick = candidates.OrderBy(c => c.b.center.y).First();
        if (candidates.Count > 1) run.Info($"a35: {candidates.Count} bulb-shaped part(s); the lowest is the bulb (the others: {string.Join("; ", candidates.Skip(1).Select(c => Shared.BoundsText(c.b)))}).");
        return (pick.sub, pick.part, pick.b);
    }

    static void CounterPhotos(Shared.Run run)
    {
        Transform dispenser = Object.FindAnyObjectByType<BeverageStation>(FindObjectsInactive.Exclude)?.transform;
        Vector3 at = dispenser != null ? dispenser.position + Vector3.up * .4f : new Vector3(3.07f, 1.4f, 17.43f);
        Vector3 front = dispenser != null ? -dispenser.forward : Vector3.back;
        Shared.Photo(run, "a09-a38-a39-the-drinks-machine-head-on", at + front * 1.05f + Vector3.up * .25f, at, 50f);
        Shared.Photo(run, "a09-the-cup-station-from-ace", at + front * .95f + Vector3.up * .3f + Vector3.right * .5f, at + Vector3.left * .9f, 55f);
        GameObject pendant = GameObject.Find("Counter pendant 1");
        if (pendant != null) Shared.Photo(run, "a35-the-counter-lamp", pendant.transform.position + new Vector3(.9f, -1.3f, -1.5f), pendant.transform.position + Vector3.down * .2f, 40f);
    }

    // ================================================================== 4 - The café room (a13, a43, a51, a52, a54)

    const float StoolsBack = .10f;

    static Action<Shared.Run> CafeRoom(Shared.Run run)
    {
        // a13 and a52: the courtyard-facing windows' photo plaster and pale ledge (that group's own renderers only; the street's
        // copies of those materials stay).
        Transform plaster = Under(run, "V3 - courtyard-facing cafe windows", "Cloud plaster");
        Transform ledge = Under(run, "V3 - courtyard-facing cafe windows", "Cream trim");
        Material oat = SceneMaterial(run, "Oat plaster");
        Material sage = SceneMaterial(run, "Sage joinery");
        Require(run, plaster != null && plaster.GetComponent<Renderer>() != null, "a13: no 'V3 - courtyard-facing cafe windows/Cloud plaster' renderer.");
        Require(run, ledge != null && ledge.GetComponent<Renderer>() != null, "a52: no 'V3 - courtyard-facing cafe windows/Cream trim' renderer.");
        Require(run, oat != null, "a13: the back wall's 'Oat plaster' isn't on anything in the scene.");
        Require(run, sage != null, "a52: the café's 'Sage joinery' isn't on anything in the scene.");

        // a43: the repair bar's stools and their sit points 10 cm back from the bar. Their cups stay on the bar (CupSpot); the
        // stand point, the seat and the face-towards point go with the stool. The hand-over probe measures each stool's seat from
        // the bench's stand point: the east stool was 3.75 m out, 3.85 m now, inside the 4.1 m the bench reaches across
        // (PlayerInteractor: Across Counter Reach 3.5 + 0.6); stool 1, the nearest, gains at most 0.1 m on ~2.8 m.
        var seats = Shared.InScene<TableSeat>(run).Where(s => s.RepairBar).ToList();
        Require(run, seats.Count > 0, "a43: no repair-bar seats (TableSeat with Repair Bar) in the scene.");
        var moves = new List<(Transform t, Vector3 by)>();
        StationInteractable bench = BenchStation();
        foreach (TableSeat seat in seats)
        {
            Transform cup = seat.transform.Find("CupSpot");
            Transform pose = seat.SeatPose;
            Vector3 toBar = cup != null && pose != null ? Vector3.ProjectOnPlane(cup.position - pose.position, Vector3.up) : Vector3.ProjectOnPlane(seat.transform.forward, Vector3.up);
            Require(run, toBar.sqrMagnitude > 1e-4f, $"a43: can't tell which way {seat.name}'s bar is.");
            Vector3 by = -toBar.normalized * StoolsBack;
            string label = seat.name.StartsWith("Seat (repair bar, ", StringComparison.Ordinal) ? seat.name.Substring("Seat (repair bar, ".Length).TrimEnd(')') : null;
            Transform stool = label != null ? Named(run, $"CK_Stool_Bar (repair bar, {label})").FirstOrDefault() : null;
            if (stool == null) run.Info($"a43: no stool model named 'CK_Stool_Bar (repair bar, {label})' for {seat.name}: only its sit points move.");
            else moves.Add((stool, by));
            foreach (string child in new[] { "SeatPose", "StandPoint", "FaceTowards" })
            {
                Transform c = seat.transform.Find(child);
                if (c != null) moves.Add((c, by));
            }
            if (bench != null && bench.StandPoint != null && pose != null)
            {
                Vector3 d = Vector3.ProjectOnPlane(pose.position + by - bench.StandPoint.position, Vector3.up);
                run.Info($"a43: {seat.name}: {F(d.magnitude, "0.00")} m from the bench's stand point after the move (the bench reaches 4.1 m across).");
                Require(run, d.magnitude <= 4.0f, $"a43: {seat.name} would be {F(d.magnitude, "0.00")} m from the bench's stand point: too near the 4.1 m reach. Nothing changed.");
            }
        }

        // a51: the lounge rug: deeper (toward #C49A6C) and a third bigger, so the chairs' front feet stand on it.
        Transform rug = Starting(run, "LI_Rug_Corner").FirstOrDefault();
        Require(run, rug != null, "a51: no LI_Rug_Corner in the scene.");
        Renderer[] rugParts = rug.GetComponentsInChildren<Renderer>(true);

        // a54: the black rubber mats on the staff floor: a dark brown rubber, so from above they read as mats, not holes.
        var mats = new List<Renderer>();
        foreach (string name in new[] { "Repair bench mat", "Drink station mat" })
            foreach (Transform t in Named(run, name)) mats.AddRange(t.GetComponentsInChildren<Renderer>(true));
        run.Info($"a54: the staff floor's mats: {mats.Count} renderer(s), materials {string.Join(", ", mats.SelectMany(m => m.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())}.");

        return r =>
        {
            Renderer pr = plaster.GetComponent<Renderer>();
            Shared.Repaint(r, pr, pr.sharedMaterials.Select(_ => oat).ToArray(), "a13: the back wall's flat Oat plaster, not the street's photo plaster (\"boards leaning on the glass\")");
            Renderer lr = ledge.GetComponent<Renderer>();
            Shared.Repaint(r, lr, lr.sharedMaterials.Select(_ => sage).ToArray(), "a52: the window ledge in the café's sage joinery, not a glowing pale plank");

            foreach ((Transform t, Vector3 by) in moves)
                Shared.Move(r, t, t.position + by, t.rotation, "a43: 10 cm back from the repair bar (\"customers' knees inside the bar\")");

            Material rugMaterial = rugParts.SelectMany(p => p.sharedMaterials).FirstOrDefault(m => Named(m, "LI_RugCorner"));
            if (rugMaterial != null)
            {
                Material deeper = Derived(r, "Art fix a51 - lounge rug (deeper)", rugMaterial, m => Colour(m, new Color(.80f, .60f, .42f)));
                foreach (Renderer p in rugParts) SwapSlots(r, p, m => Named(m, "LI_RugCorner"), deeper, "a51: the rug a deeper warm tan, not a pale sheet of paper");
            }
            else r.Info("a51: the rug doesn't wear LI_RugCorner: only scaled.");
            Shared.Move(r, rug, rug.position, rug.rotation, "a51: a third bigger (its scale before is kept)");
            Undo.RecordObject(rug, r.Title);
            Vector3 s0 = rug.localScale;
            rug.localScale = new Vector3(s0.x * 1.35f, s0.y, s0.z * 1.35f);

            Material rubber = Lit(r, "Art fix a54 - staff mat rubber (dark brown)", new Color(.21f, .16f, .13f), .22f);
            foreach (Renderer m in mats) SwapSlots(r, m, x => x != null, rubber, "a54: the staff floor's mat a dark brown rubber (\"black mats read as holes\")");

            r.Check(pr.sharedMaterials.All(m => m == oat), "a13: the courtyard windows' plaster is Oat plaster");
            r.Check(lr.sharedMaterials.All(m => m == sage), "a52: the courtyard windows' ledge is Sage joinery");
            r.Check(moves.Count > 0, $"a43: {moves.Count} stool(s) and sit point(s) moved 10 cm back");
        };
    }

    static void RoomPhotos(Shared.Run run)
    {
        Shared.GameCameraPhoto(run, "a51-a54-game-camera");
        Shared.Photo(run, "a13-a52-east-windows-from-the-bench", new Vector3(-3.5f, 1.6f, 15f), new Vector3(5.5f, 1.1f, 9f), 60f);
        Shared.Photo(run, "a43-the-repair-bar-stools", new Vector3(-6.6f, 1.1f, 11.2f), new Vector3(-4.2f, .7f, 13.2f), 50f);
    }

    // ================================================================== 5 - The patio and the shopfront (a07, a58, a59, a60, a61)

    static Action<Shared.Run> Patio(Shared.Run run)
    {
        // a07: the patio's flower: a Kenney yellow flower (as the pocket gardens' flower_yellowA) in a small terracotta pot (the
        // bud vase, cut down to a pot); the yellow ball is switched off.
        Transform ball = Named(run, "Single flower").FirstOrDefault();
        Transform vase = Named(run, "Terracotta bud vase").FirstOrDefault();
        Transform flowerTemplate = Named(run, "flower_yellowA").FirstOrDefault(t => t.parent != null && t.parent.name != "flower_yellowA");
        Require(run, ball != null && vase != null, "a07: the patio's 'Single flower' and 'Terracotta bud vase' aren't in the scene.");
        Require(run, flowerTemplate != null, "a07: no Kenney flower_yellowA in the scene to copy (the CC0 refresh's pocket gardens).");

        // a58: the patio table's glowing marble: its slot tinted the counter's warm grey (this table only).
        Transform table = Starting(run, "T2_Table_BistroRound").FirstOrDefault();
        Require(run, table != null, "a58: no T2_Table_BistroRound (the patio's table).");
        Material marble = KitMaterial("Assets/Art/Materials/T2_Marble.mat");
        // a60: the Tier 2 brass paler and softer (the material: every T2 piece wears it).
        Material brass = KitMaterial("Assets/Art/Materials/T2_Brass.mat");
        Require(run, marble != null && brass != null, "a58/a60: T2_Marble or T2_Brass isn't in Assets/Art/Materials.");

        // a59: the door planters' boulders: covered by the Kenney bushes the street's pocket gardens use, three a trough.
        List<Transform> planters = Named(run, "Patio - Planter_01");
        Transform bushTemplate = Named(run, "plant_bushDetailed").FirstOrDefault(t => t.parent != null && t.parent.name != "plant_bushDetailed");
        Require(run, planters.Count > 0, "a59: no 'Patio - Planter_01' by the door.");
        Require(run, bushTemplate != null, "a59: no Kenney plant_bushDetailed in the scene to copy.");

        // a61: the patio's two chunky Kenney chairs: the café's own spindle chairs in their places, facing the table.
        List<Transform> chunky = Named(run, "Patio rounded timber chair");
        run.Info($"a61: {chunky.Count} Kenney patio chair(s): {string.Join("; ", chunky.Select(c => Shared.V(c.position)))}.");

        return r =>
        {
            Transform group = Group(r, Shared.StepGroup(r, "Art fixes"), "Patio (art fixes a07, a59, a61)");

            Shared.SwitchOff(r, ball.gameObject, "a07: the yellow ball on a red tube (\"reads as a chess pawn\")");
            Bounds vb = WorldBounds(vase);
            float tableTop = vb.min.y;
            Shared.Move(r, vase, new Vector3(vase.position.x, tableTop + vb.size.y * .22f, vase.position.z), vase.rotation, "a07: the bud vase cut down to a small pot (its scale before is kept)");
            Undo.RecordObject(vase, r.Title);
            vase.localScale = new Vector3(vase.localScale.x * 1.15f, vase.localScale.y * .45f, vase.localScale.z * 1.15f);
            Vector3 potTop = new Vector3(vase.position.x, WorldBounds(vase).max.y, vase.position.z);
            Copy(r, flowerTemplate, group, "Patio flower (Kenney flower_yellowA, art fix a07)", potTop + Vector3.down * .015f, 20f, new Vector3(.16f, .2f, .16f));

            Material warmGrey = Derived(r, "Art fix a58 - T2 marble, patio warm grey", marble, m =>
            {
                Colour(m, new Color(.71f, .68f, .63f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .5f);
            });
            foreach (Renderer p in table.GetComponentsInChildren<Renderer>(true))
                SwapSlots(r, p, m => Named(m, "T2_Marble"), warmGrey, "a58: the patio table's top the counter's warm grey, not a glowing white blob");

            EditAsset(r, brass, () =>
            {
                Colour(brass, new Color(.80f, .69f, .45f));
                if (brass.HasProperty("_Smoothness")) brass.SetFloat("_Smoothness", .72f);
            }, "a60: the Tier 2 brass a little paler and softer (\"reads as mustard paint\")");

            foreach (Transform planter in planters)
            {
                Bounds pb = WorldBounds(planter);
                bool alongX = pb.size.x >= pb.size.z;
                float length = alongX ? pb.size.x : pb.size.z, depth = alongX ? pb.size.z : pb.size.x;
                for (int i = 0; i < 3; i++)
                {
                    float along = (i - 1) * length * .3f;
                    Vector3 foot = new Vector3(pb.center.x + (alongX ? along : 0f), pb.min.y + pb.size.y * .45f, pb.center.z + (alongX ? 0f : along));
                    Copy(r, bushTemplate, group, $"Door planter bush {i + 1} (Kenney plant_bushDetailed, art fix a59)", foot, 40f + i * 73f,
                         new Vector3(length * .42f, pb.size.y * .75f + .1f, depth * 1.05f));
                }
            }

            Transform patioTable = Named(r, "Outdoor bistro table").FirstOrDefault();
            foreach (Transform c in chunky)
            {
                Vector3 toTable = patioTable != null ? Vector3.ProjectOnPlane(patioTable.position - c.position, Vector3.up) : -c.forward;
                float yaw = Mathf.Atan2(toTable.x, toTable.z) * Mathf.Rad2Deg;
                Kit(r, "CK_Chair_Spindle", group, $"Patio spindle chair (CK_Chair_Spindle, art fix a61, x {F(c.position.x, "0.0")})", new Vector3(c.position.x, c.position.y, c.position.z), yaw);
                Shared.SwitchOff(r, c.gameObject, "a61: the chunky Kenney patio chair (the café's own spindle chair stands there now)");
            }

            r.Check(!ball.gameObject.activeSelf, "a07: the yellow ball is off (not deleted)");
            r.Check(chunky.All(c => !c.gameObject.activeSelf), "a61: the Kenney patio chairs are off");
        };
    }

    static void PatioPhotos(Shared.Run run)
    {
        // The Day 4-5 intro's patio.flower shot (DayIntro.PatioFlower: from (-2.6, 0.95, -3.4) at the table, 40°).
        Shared.Photo(run, "a07-a58-a61-the-patio-flower-shot", new Vector3(-2.6f, .95f, -3.4f), new Vector3(-4.05f, .85f, -1.85f), 40f);
        Shared.Photo(run, "a59-the-door-planters", new Vector3(0f, 1.6f, -5.5f), new Vector3(0f, .5f, -.6f), 60f);
        Shared.GameCameraPhoto(run, "a58-a59-game-camera");
    }

    // ================================================================== 6 - The streets (a06, a16, a65; a19 surveyed)

    static Action<Shared.Run> Streets(Shared.Run run)
    {
        // a06: West Street's two ball trees (CafeStreetUpgrade.StreetFurniture: a "Planter clay" trunk and three "Leaf green" balls,
        // combined by material under "10 - cohesive neighborhood V4/Street furniture"; those two materials are on nothing else
        // there): off, and a Kenney oak in each place, as the courtyard has.
        Transform furniture = Everything(run).FirstOrDefault(t => t.name == "Street furniture" && t.parent != null && t.parent.name == "10 - cohesive neighborhood V4");
        Transform leaves = furniture != null ? furniture.Find("Leaf green") : null, trunks = furniture != null ? furniture.Find("Planter clay") : null;
        Transform oak = Named(run, "tree_oak").FirstOrDefault(t => t.parent != null && t.parent.name != "tree_oak");
        Require(run, leaves != null && trunks != null, "a06: no 'Leaf green' and 'Planter clay' under '10 - cohesive neighborhood V4/Street furniture'.");
        Require(run, oak != null, "a06: no Kenney tree_oak in the scene to copy (the CC0 refresh's courtyard trees).");
        var treeFeet = new List<Vector3>();
        foreach (float z in new[] { -13f, 33f }) treeFeet.Add(new Vector3(-17.05f, FootOf(trunks, -17.05f, z), z));
        run.Info($"a06: the two ball trees' feet: {string.Join(", ", treeFeet.Select(Shared.V))}.");

        // a16: the uphill streets as bare ramps: their first 30 m lined with homes like the hill's (CafeStreetUpgrade.FinishV4Backdrop's
        // "Hill home": the same boxes, roofs, windows and materials), in the gaps between the hill's two rows, both sides of both
        // streets.
        Transform hill = Named(run, "V4 - homes on the distant slope").FirstOrDefault();
        Require(run, hill != null, "a16: no 'V4 - homes on the distant slope'.");
        Material Hill(string n) => hill.Find(n) != null && hill.Find(n).GetComponent<Renderer>() != null ? hill.Find(n).GetComponent<Renderer>().sharedMaterial : null;
        Material wall = Hill("Distant neighborhood"), wallAlt = Hill("Cloud plaster"), roof = Hill("Slate roof"), glass = Hill("Window glass");
        Require(run, wall != null && roof != null && glass != null, "a16: the hill homes' materials (Distant neighborhood, Slate roof, Window glass) weren't found.");

        // a65: the car park island's five ball shrubs: off, five Kenney bushes in their places.
        Transform island = Named(run, "Planted island").FirstOrDefault();
        List<Transform> balls = island != null ? island.Cast<Transform>().Where(t => t.name == "Low sage shrub").ToList() : new List<Transform>();
        Transform bush = Named(run, "plant_bushDetailed").FirstOrDefault(t => t.parent != null && t.parent.name != "plant_bushDetailed");
        Require(run, balls.Count > 0, "a65: no 'Planted island' with 'Low sage shrub' balls.");
        Require(run, bush != null, "a65: no Kenney plant_bushDetailed to copy.");

        // a19: the two blank orange building sides in the Day 3-6 pull-outs and titles: which ones they are needs the frames.
        SurveyBlankSides(run);

        return r =>
        {
            Shared.SwitchOff(r, leaves.gameObject, "a06: West Street's ball-tree canopies (\"three smooth green balls on an orange pipe\")");
            Shared.SwitchOff(r, trunks.gameObject, "a06: West Street's ball-tree trunks");
            Transform streets = Group(r, Shared.StepGroup(r, "Art fixes"), "Streets (art fixes a06, a16, a65)");
            for (int i = 0; i < treeFeet.Count; i++)
                Copy(r, oak, streets, $"West Street tree {i + 1} (Kenney tree_oak, art fix a06)", treeFeet[i], 37f + i * 110f, new Vector3(2.6f, 4.4f, 2.5f));

            Transform homes = Group(r, streets, "Homes up the hill streets (a16)");
            int n = 0;
            foreach (float streetX in new[] { -12.2f, 12.9f })
                foreach (float side in new[] { -1f, 1f })
                    foreach (float z in new[] { 51f, 65f })
                    {
                        float x = streetX + side * 8.2f, baseY = (z - 37f) * .2f - .35f, height = 4.6f + ((n * 3) % 5) * .7f;
                        Material w = n % 3 == 0 && wallAlt != null ? wallAlt : wall;
                        Box(r, homes, $"Hill home {n + 1}", new Vector3(x, baseY + height * .5f, z), new Vector3(5.2f, height, 6.6f), Quaternion.identity, w);
                        Box(r, homes, $"Hill roof {n + 1}", new Vector3(x, baseY + height + .1f, z), new Vector3(5.4f, .2f, 6.75f), Quaternion.identity, roof);
                        float faceX = x - side * 2.62f;   // the side facing the street
                        for (int k = 0; k < 3; k++)
                            Box(r, homes, $"Hill window {n + 1}.{k + 1}", new Vector3(faceX, baseY + height - 1f, z - 2.1f + k * 2.1f),
                                new Vector3(.035f, 1.05f, .72f), Quaternion.identity, glass);
                        for (int k = 0; k < 2; k++)
                            Box(r, homes, $"Hill window {n + 1}.{k + 4}", new Vector3(x - 1.2f + k * 2.4f, baseY + height - 1f, z - 3.32f), new Vector3(.72f, 1.05f, .035f), Quaternion.identity, glass);
                        n++;
                    }

            foreach (Transform b in balls)
            {
                Vector3 foot = new Vector3(b.position.x, WorldBounds(b).min.y, b.position.z);
                Copy(r, bush, streets, $"Island bush (Kenney plant_bushDetailed, art fix a65)", foot, Mathf.Repeat(b.position.x * 97f, 360f), new Vector3(.5f, .42f, .5f));
                Shared.SwitchOff(r, b.gameObject, "a65: a smooth sage ball at the island's tree");
            }

            r.Check(!leaves.gameObject.activeSelf && !trunks.gameObject.activeSelf, "a06: the ball trees are off (not deleted)");
            r.Check(balls.All(b => !b.gameObject.activeSelf), "a65: the island's ball shrubs are off");
            r.Check(n == 8, $"a16: {n} homes line the hill streets' first 30 m");
        };
    }

    // The lowest point of a combined mesh near (x, z) (a trunk's foot), or 0.
    static float FootOf(Transform combined, float x, float z)
    {
        MeshFilter f = combined.GetComponent<MeshFilter>();
        if (f == null || f.sharedMesh == null) return 0f;
        if (!f.sharedMesh.isReadable) return combined.GetComponent<Renderer>() != null ? combined.GetComponent<Renderer>().bounds.min.y : 0f;
        float best = float.PositiveInfinity;
        foreach (Vector3 v in f.sharedMesh.vertices)
        {
            Vector3 w = combined.TransformPoint(v);
            if (Mathf.Abs(w.x - x) < .6f && Mathf.Abs(w.z - z) < .6f) best = Mathf.Min(best, w.y);
        }
        return float.IsPositiveInfinity(best) ? 0f : best;
    }

    // a19 (read-only): from the game camera, the POLYGON City buildings ("Building - ...") with the most wall facing it.
    static void SurveyBlankSides(Shared.Run run)
    {
        GameObject shop = GameObject.Find("CmShopCam");
        if (shop == null) { run.Info("a19: no CmShopCam: the survey of building sides wasn't made."); return; }
        Physics.SyncTransforms();
        var count = new Dictionary<Transform, int>();
        Transform cam = shop.transform;
        float fov = 39f, aspect = 16f / 9f;
        for (int iy = 0; iy < 27; iy++)
            for (int ix = 0; ix < 48; ix++)
            {
                float u = (ix + .5f) / 48f * 2f - 1f, v = (iy + .5f) / 27f * 2f - 1f;
                float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                Vector3 dir = (cam.forward + cam.right * u * t * aspect + cam.up * v * t).normalized;
                if (!Physics.Raycast(cam.position, dir, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > .4f) continue;   // walls only
                Transform b = hit.collider.transform;
                while (b != null && !b.name.StartsWith("Building - ", StringComparison.Ordinal)) b = b.parent;
                if (b == null) continue;
                count[b] = count.TryGetValue(b, out int c) ? c + 1 : 1;
            }
        if (count.Count == 0) { run.Info("a19: from the game camera no POLYGON City building wall was hit (their colliders may be off): see the photos."); return; }
        run.Info("a19 (a survey; nothing changes): the buildings with the most wall facing the game camera (rays of 48 x 27): " +
                 string.Join("; ", count.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{Shared.PathOf(kv.Key)} {kv.Value}")) +
                 ". Turning the two blocks whose blank sides show is his call once he has seen these (the report).");
    }

    static void StreetPhotos(Shared.Run run)
    {
        Shared.Photo(run, "a06-a16-street-row-first-frame", new Vector3(-9f, 1.7f, -6.5f), new Vector3(-13f, 3.5f, 14f), 38f);
        Shared.Photo(run, "a06-west-street-north", new Vector3(-14.5f, 1.6f, 22f), new Vector3(-17f, 2.8f, 33f), 55f);
        Shared.Photo(run, "a16-the-hill-streets-from-above", new Vector3(0f, 26f, 10f), new Vector3(0f, 6f, 55f), 55f);
        Shared.Photo(run, "a65-the-car-park-island", new Vector3(5.6f, 1.6f, -10.5f), new Vector3(5.6f, .3f, -15.3f), 45f);
        Shared.GameCameraPhoto(run, "a19-game-camera");
    }

    // ================================================================== 7 - Grace's house (a17, a18, a21, a70, a71; a75 looked at)

    const string GraceHouseName = "1 - Saffron bay-window house";

    static Action<Shared.Run> GraceHouse(Shared.Run run)
    {
        Transform house = Named(run, GraceHouseName).FirstOrDefault();
        Require(run, house != null, $"a17: no '{GraceHouseName}'.");
        Renderer paint = house.Find("Street palette") != null ? house.Find("Street palette").GetComponent<Renderer>() : null;
        Require(run, paint != null, "a17: her house has no 'Street palette' renderer (its painted walls).");
        Material saffron = paint.sharedMaterials.FirstOrDefault(m => m != null);
        run.Info($"a17: her walls wear {string.Join(", ", paint.sharedMaterials.Select(m => m != null ? m.name : "none"))}.");

        // a18: her porch lamp, a bare glowing ball 0.28 m across: shrunk to a bulb, inside a small iron lantern (by day too).
        Transform ballLamp = Starting(run, "Her porch lamp").FirstOrDefault();
        Material iron = SceneMaterial(run, "Ironwork");
        Require(run, ballLamp != null, "a18: no 'Her porch lamp ...' (Night guidance put it there).");
        Require(run, iron != null, "a18: the street's 'Ironwork' isn't on anything in the scene.");

        // a21: the lampshades drawn on both sides (GH_Lampshade: her floor lamp, her bedside lamp, the house kit's pendant).
        Material shade = KitMaterial("Assets/Art/Materials/GraceHouse/GH_Lampshade.mat");
        Require(run, shade != null, "a21: Assets/Art/Materials/GraceHouse/GH_Lampshade.mat isn't there.");

        // a70: her door's panel, in the house paint ("cardboard taped on"): the door's own colour, a shade lighter.
        Transform leaf = house.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Door leaf");
        Renderer leafRenderer = leaf != null ? leaf.GetComponent<Renderer>() : null;
        Require(run, leafRenderer != null, "a70: her door has no 'Door leaf' renderer (Front door (opens)/Hinge).");
        Material[] leafMats = leafRenderer.sharedMaterials;
        int panel = Array.FindIndex(leafMats, m => m != null && saffron != null && (m == saffron || m.name == "Street palette" || m.name.StartsWith("Saffron", StringComparison.Ordinal)));
        Material door = leafMats.FirstOrDefault(m => m != null && (panel < 0 || m != leafMats[panel]));
        run.Info($"a70: her door leaf wears {string.Join(", ", leafMats.Select(m => m != null ? m.name : "none"))}; the panel is slot {panel}.");

        // a71: the house's open side: the clapboard lips down it too (the front's: 3.5 cm courses every 25 cm, the house paint).
        Bounds hb = paint.bounds;
        float height = hb.max.y - house.position.y;
        Transform neighbour = Named(run, "2 - Dusty rose bay-window house").FirstOrDefault();
        float openSide = neighbour != null ? -Mathf.Sign(house.InverseTransformPoint(neighbour.position).x) : 1f;
        run.Info($"a71: her house is {F(height, "0.00")} m tall; its open side is local x {(openSide > 0 ? "+" : "-")}2.8 (away from {(neighbour != null ? neighbour.name : "no neighbour found")}).");

        // a75 (a look; "leave it as it is"): what is dark at the foot of her stairs.
        Transform stairs = Named(run, "GH_Stairs_L").FirstOrDefault();
        if (stairs != null)
        {
            var r0 = stairs.GetComponent<Renderer>();
            run.Info("a75 (nothing changes): the stairs' model GH_Stairs_L wears " + (r0 != null ? string.Join(", ", r0.sharedMaterials.Select(m => m != null ? m.name : "none")) : "nothing") +
                     ". The black block at its foot is the stairs' own side, where a cupboard would be (GH_UnderStairsDoor is in the kit but not placed), in " +
                     "CC_Wood_Espresso, the darkest wood, unlit at night under the dark upstairs; 'Upper flight's side (the cupboard front)' is only a collider.");
        }

        return r =>
        {
            Material yellow = Derived(r, "Saffron - Grace's clear yellow (art fix a17)", saffron, m =>
            {
                // Her own clear warm yellow (about #E9B43C); the plaster only for its bumps (its normal map stays, its colour goes).
                Colour(m, new Color(.914f, .706f, .235f));
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            });
            Shared.Repaint(r, paint, paint.sharedMaterials.Select(m => m == saffron ? yellow : m).ToArray(),
                           "a17: her house its own clear warm yellow (\"the yellow house\" reads brown at night, dull ochre by day)");

            Bounds bb = WorldBounds(ballLamp);
            Vector3 c = bb.center;
            Shared.Move(r, ballLamp, ballLamp.position, ballLamp.rotation, "a18: the glowing ball shrunk to a bulb (its scale before is kept)");
            Undo.RecordObject(ballLamp, r.Title);
            float k = .09f / Mathf.Max(.01f, bb.size.y);
            ballLamp.localScale *= k;
            Transform lantern = Group(r, house, "Her porch lantern (art fix a18)");
            Quaternion q = house.rotation;
            Box(r, lantern, "Lantern cap", c + Vector3.up * .11f, new Vector3(.2f, .035f, .2f), q, iron);
            Box(r, lantern, "Lantern finial", c + Vector3.up * .145f, new Vector3(.05f, .04f, .05f), q, iron, PrimitiveType.Cylinder);
            Box(r, lantern, "Lantern base", c + Vector3.down * .09f, new Vector3(.15f, .025f, .15f), q, iron);
            foreach (float x in new[] { -.065f, .065f })
                foreach (float z in new[] { -.065f, .065f })
                    Box(r, lantern, "Lantern frame", c + q * new Vector3(x, .01f, z), new Vector3(.014f, .2f, .014f), q, iron);

            EditAsset(r, shade, () =>
            {
                if (shade.HasProperty("_Cull")) shade.SetFloat("_Cull", 0f);   // Render Face: Both
                shade.doubleSidedGI = true;
            }, "a21: the lampshade drawn on both sides (\"from below its inside is missing\")");

            if (panel >= 0 && door != null)
            {
                Material lighter = Derived(r, "Art fix a70 - Grace's door panel (a shade lighter)", door, m =>
                {
                    Color dc = door.HasProperty("_BaseColor") ? door.GetColor("_BaseColor") : door.color;
                    Colour(m, Color.Lerp(dc, Color.white, .12f));
                });
                Material[] mats = leafRenderer.sharedMaterials;
                mats[panel] = lighter;
                Shared.Repaint(r, leafRenderer, mats, "a70: her door's panel in the door's own colour, a shade lighter, a sunk panel (not \"cardboard taped on\")");
            }
            else r.Info("a70: no house-paint panel on her door leaf: nothing changed for a70.");

            Transform lips = Group(r, house, "Side clapboard lips (art fix a71)");
            float sideX = openSide * 2.825f;
            for (float y = .8f; y < height - .25f; y += .25f)
                Box(r, lips, "Side clapboard lip", house.TransformPoint(new Vector3(sideX, y, -2.1f)), new Vector3(.05f, .035f, 4.2f), house.rotation, yellow);

            r.Check(paint.sharedMaterials.Contains(yellow), "a17: her walls wear her own yellow");
            r.Check(shade.GetFloat("_Cull") == 0f, "a21: GH_Lampshade draws both sides");
        };
    }

    static void GracePhotos(Shared.Run run)
    {
        HomeDoor door = Shared.InScene<HomeDoor>(run).FirstOrDefault(d => d.name.StartsWith("Grace's front door", StringComparison.Ordinal));
        Vector3 at = door != null ? door.transform.position + Vector3.up * 1.4f : new Vector3(-17.4f, 2.3f, -2.2f);
        // The Day 2 intro's house.grace shot (DayIntro.GraceHouseShot, moved 1 m on 26 Oct for a69), then her step, then the cross street.
        Shared.Photo(run, "a17-a69-a70-her-house-the-day-2-shot", new Vector3(-8.6f, 1.6f, -1.5f), new Vector3(-17.4f, 2.3f, -1.2f), 32f);
        Shared.Photo(run, "a18-a70-her-door-from-her-step", at + new Vector3(3.2f, .2f, 0f), at + Vector3.up * .4f, 55f);
        Transform house = Named(run, GraceHouseName).FirstOrDefault();
        if (house != null) Shared.Photo(run, "a71-her-side-wall-from-the-cross-street", house.TransformPoint(new Vector3(9f, 1.6f, -8f)), house.TransformPoint(new Vector3(2.8f, 3f, -2f)), 55f);
    }

    // ================================================================== 8 - The bins at night (a05)

    static Action<Shared.Run> Bins(Shared.Run run)
    {
        // a05, real: the cut lids are one atlas colour top and bottom (every corner of both lid meshes has the same UV, 0.076,
        // 0.011, read from Assets/Art/NightZero/Dumpster - lids cut free.asset), so the "pure black" underside is light, not
        // paint: opened, it faces the street and the camera, away from the wall lamp, lit by the night's ambient alone. The
        // lids get their own copy of the skip's material that glows faintly with its own colour (its atlas as the emission
        // map), so the underside reads as the lid's dark green at night; by day the glow is lost in the light.
        NightZeroSet set = Shared.InScene<NightZeroSet>(run).FirstOrDefault();
        Require(run, set != null && set.nearLid != null && set.farLid != null, "a05: no NightZeroSet with its two lids (Night 0's set-up).");
        Renderer[] lids = set.nearLid.GetComponentsInChildren<Renderer>(true).Concat(set.farLid.GetComponentsInChildren<Renderer>(true)).ToArray();
        Require(run, lids.Length > 0, "a05: the lids have no renderers.");
        Material skip = lids.SelectMany(l => l.sharedMaterials).FirstOrDefault(m => m != null);
        Require(run, skip != null, "a05: the lids wear no material.");
        run.Info($"a05: {lids.Length} lid renderer(s), wearing {skip.name} (shader {skip.shader.name}); emission {(skip.HasProperty("_EmissionColor") ? "supported" : "NOT supported: a URP Lit copy is made")}.");

        return r =>
        {
            Material glow = Derived(r, "Art fix a05 - dumpster lids (lit from within, faintly)", skip, m =>
            {
                if (!m.HasProperty("_EmissionColor"))
                {
                    Texture main = skip.mainTexture;
                    m.shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (main != null) m.SetTexture("_BaseMap", main);
                }
                Texture atlas = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", atlas);
                m.SetColor("_EmissionColor", new Color(.16f, .16f, .16f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            foreach (Renderer lid in lids)
                SwapSlots(r, lid, m => m == skip, glow, "a05: the open lid's underside its own dark green at night, not a black hole behind the man");
            r.Check(lids.All(l => l.sharedMaterials.All(m => m == glow || m != skip)), "a05: both lids wear the faint glow");
        };
    }

    static void BinsPhotos(Shared.Run run)
    {
        NightZeroSet set = Shared.InScene<NightZeroSet>(run).FirstOrDefault();
        if (set == null) return;
        // The reveal camera's own place (the lid's moment), with the far lid opened for the photo and shut again.
        Transform cam = set.revealCamera != null ? set.revealCamera.transform : null;
        Quaternion was = set.farLid.localRotation;
        try
        {
            set.SetLid(set.farLid, 1f);
            if (cam != null) Shared.Photo(run, "a05-the-lid-from-the-reveal-camera", cam.position, cam.position + cam.forward * 4f, 40f);
            Vector3 bins = set.Bins;
            Shared.Photo(run, "a05-the-bins-from-the-alley", bins + set.transform.forward * 3.2f + Vector3.up * 1.6f, bins + Vector3.up * 1.4f, 50f);
        }
        finally { set.farLid.localRotation = was; }
    }

    // ================================================================== 9 - Softer window shadows (a45)

    const string HighPipeline = "Assets/Settings/PC_RPAsset.asset";

    static Action<Shared.Run> Shadows(Shared.Run run)
    {
        // a45: stair-stepped window shadows. The High preset (PC_RPAsset, the default level) already has soft shadows (High);
        // its 50 m of shadow over four cascades leaves the café, 20-28 m from the overhead camera, in the last cascade at about
        // 26 mm a shadow texel. 35 m and a 4096 shadow map make it about 9 mm. Medium and Low are QualityPresetSteps' copies
        // (its table sets them; re-running Performance 2 would undo a change to them), so only High changes here.
        Object pipeline = AssetDatabase.LoadMainAssetAtPath(HighPipeline);
        Require(run, pipeline != null, $"a45: {HighPipeline} isn't there.");
        var so = new SerializedObject(pipeline);
        foreach (string p in new[] { "m_MainLightShadowmapResolution", "m_ShadowDistance", "m_SoftShadowsSupported", "m_SoftShadowQuality", "m_ShadowCascadeCount" })
        {
            SerializedProperty sp = so.FindProperty(p);
            run.Info($"a45: {p} = {(sp == null ? "(not found)" : sp.propertyType == SerializedPropertyType.Float ? F(sp.floatValue, "0.##") : sp.intValue.ToString(CultureInfo.InvariantCulture))}");
        }
        Require(run, so.FindProperty("m_MainLightShadowmapResolution") != null && so.FindProperty("m_ShadowDistance") != null, "a45: the pipeline asset has no shadow settings by those names.");

        return r =>
        {
            EditAsset(r, pipeline, () =>
            {
                var s = new SerializedObject(pipeline);
                s.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
                s.FindProperty("m_ShadowDistance").floatValue = 35f;
                SerializedProperty soft = s.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                s.ApplyModifiedPropertiesWithoutUndo();
            }, "a45: the sun's shadow map 4096 and 35 m of shadow (\"jagged window shadows on the floor\")");
            var check = new SerializedObject(pipeline);
            r.Check(check.FindProperty("m_MainLightShadowmapResolution").intValue == 4096 && Mathf.Approximately(check.FindProperty("m_ShadowDistance").floatValue, 35f),
                    "a45: High's shadows: 4096, 35 m");
        };
    }

    static void ShadowPhotos(Shared.Run run)
    {
        Shared.GameCameraPhoto(run, "a45-game-camera");
        Shared.Photo(run, "a45-the-floor-by-the-windows", new Vector3(-2f, 1.6f, 2f), new Vector3(-5.5f, 0f, 6f), 60f);
    }

    // ================================================================== the Play-mode check (a01, a04)

    const string CheckMenu = Menu + "Check - one body each, heads kept (Play mode, read-only)";

    [MenuItem(CheckMenu, true)]
    static bool Playing() => EditorApplication.isPlaying;

    // In Play (a day under way, people in the café): a01, nobody's own CC0 body is drawn while a city look is on; a04, every
    // seated person's drawn parts each have a culling box that holds the person's head.
    [MenuItem(CheckMenu, false, 900)]
    static void CheckBodies()
    {
        const string tag = "[Art fixes check]";
        var lines = new List<string>();
        int checks = 0, failed = 0;
        void Check(bool ok, string text) { checks++; if (!ok) failed++; lines.Add((ok ? "  PASS  " : "  FAIL  ") + text); }
        PolygonNpcVisual[] people = Object.FindObjectsByType<PolygonNpcVisual>(FindObjectsInactive.Exclude);
        foreach (PolygonNpcVisual p in people)
        {
            if (p.VisualInstance == null) continue;
            int extra = p.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Count(s => s.enabled && s.gameObject.activeInHierarchy && !s.transform.IsChildOf(p.VisualInstance.transform));
            Check(extra == 0, $"a01 {p.name}: one body drawn (its own CC0 parts drawn: {extra})");
            NpcSeating seat = p.GetComponent<NpcSeating>();
            if (seat == null || !seat.IsSeated) continue;
            Transform head = p.VisualInstance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase));
            if (head == null) continue;
            foreach (SkinnedMeshRenderer s in p.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (s.enabled) Check(s.bounds.Contains(head.position), $"a04 {p.name} (seated): {s.name}'s culling box holds the head");
        }
        string verdict = checks == 0 ? $"{tag} Nothing to check: no one in a city look (start a day and let people in)."
                       : failed == 0 ? $"{tag} All {checks} checks passed." : $"{tag} {failed} of {checks} checks FAILED. {LogRoot}";
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(Path.Combine(LogRoot, "check-bodies-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt"), verdict + "\n" + string.Join("\n", lines));
        if (failed > 0) Debug.LogError(verdict + "\n" + string.Join("\n", lines)); else Debug.Log(verdict + "\n" + string.Join("\n", lines));
    }
}
#endif
