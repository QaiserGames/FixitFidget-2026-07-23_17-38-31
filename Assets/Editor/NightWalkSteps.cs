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
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// NIGHT WALK, PART 1: THE NIGHT'S LIGHTING (night step 4; claude/night-city-proposal.md)
//
//   Fixit Fidget > Night > Night walk 1 - Set up the night lighting
//     Adds "20 - Night walk" under the café layout, holding a NightWalk and everything that
//     exists only at night, all switched off:
//       * a light and a glowing bulb at the head of every POLYGON street lamp (the pole's arm
//         points over the road; the head is at its tip), and a light in each head of the old
//         neighbourhood's street lamps along the west street;
//       * the lists NightWalk lights at night: POLYGON building parts (each gets one of the
//         pack's five window masks, or stays dark, chosen from where it stands, so it is the
//         same every night), shop signs and billboards (they glow in their own colours), and
//         the one late spot: the corner shop at the south-west junction, across the front
//         street from the café, whose window glows and lights the pavement;
//       * a night look: a global Volume at weight 0 with more bloom and a cooler balance
//         (profile: Assets/Playtests/AcesCafeLayout/Night walk - night look.asset).
//     Running it again rebuilds the group. Nothing outside the group changes, so the day looks
//     exactly as before; save the scene yourself once the diff is read.
//   ... > Night walk - Take the night lighting out again: removes the group (undoable).
//   ... > Play the night walk (lab): a café lab session (test save, Day 5) that starts at night.
//   ... > Night walk - Photograph the night (Play Mode, night walk): the city photo views, at night,
//         with post-processing, to Logs/Night/night-photos-<time>/.
internal static class NightWalkSteps
{
    const string Tag = "[Night walk] ";
    const string Menu = "Fixit Fidget/Night/";
    const string LayoutRoot = "ACE'S CAFE - layout study 02";
    const string GroupName = "20 - Night walk";
    const string ProfilePath = "Assets/Playtests/AcesCafeLayout/Night walk - night look.asset";
    const string BulbMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - lamp bulb.mat";
    const string MaskFolder = "Assets/Synty/PolygonCity/Textures/";

    // Lamps are lit out to here (the survey box): the streets carry on past the 9 blocks.
    static readonly Rect LampArea = Rect.MinMaxRect(-80f, -75f, 80f, 90f);
    // The late spot: the corner shop at the south-west junction, across the front street from the café.
    static readonly Vector3 LateSpotNear = new Vector3(-18f, 0f, -14.8f);

    static readonly Color LampColour = new Color(1f, .80f, .56f);
    static readonly Color OldLampColour = new Color(1f, .74f, .46f);
    static readonly Color LateSpotColour = new Color(1f, .78f, .5f);

    // Street signs are painted, not lit; shop signs and billboards glow.
    static readonly string[] UnlitSigns = { "SM_Prop_Sign_Stop", "SM_Prop_Sign_GiveWay", "SM_Prop_Sign_Street", "SM_Prop_Sign_Warning",
                                            "SM_Prop_Sign_Arrow", "SM_Prop_Sign_Parking", "SM_Prop_Sign_Bustop", "SM_Prop_Sign_Attachment" };

    // ------------------------------------------------------------------ set up

    [MenuItem(Menu + "Night walk 1 - Set up the night lighting (lamps, windows, signs, late spot)")]
    static void SetUp()
    {
        var report = new StringBuilder();
        try
        {
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            if (layout == null) throw new InvalidOperationException($"No '{LayoutRoot}' in the scene.");

            Undo.SetCurrentGroupName("Night walk 1 - set up the night lighting");
            int undoGroup = Undo.GetCurrentGroup();
            var old = layout.transform.Find(GroupName);
            if (old != null) { Undo.DestroyObjectImmediate(old.gameObject); report.AppendLine("Rebuilt: the old night group was taken out first."); }

            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Night walk group");
            group.transform.SetParent(layout.transform, false);
            var walk = group.AddComponent<NightWalk>();
            var nightOnly = new List<GameObject>();
            var lampLights = new List<Light>();

            var renderers = CityPackChecks.InScene<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToList();

            // ---- POLYGON street lamps: a light and a bulb at the tip of each arm
            var lamps = new GameObject("Street lamps (night only)");
            lamps.transform.SetParent(group.transform, false);
            Material bulb = BulbMaterial();
            var poles = PrefabRoots(renderers, name => name.StartsWith("SM_Prop_LightPole_Base", StringComparison.Ordinal))
                .Where(root => LampArea.Contains(Flat2(root.transform.position))).ToList();
            foreach (var pole in poles.OrderBy(p => p.transform.position.x).ThenBy(p => p.transform.position.z))
            {
                Bounds b = BoundsOf(pole);
                Vector3 pivot = pole.transform.position;
                Vector3 arm = new Vector3(b.center.x - pivot.x, 0f, b.center.z - pivot.z);
                // The head is the far end of the arm: the pole's own vertices near the top, furthest out.
                // The bulb goes just under it, where it can be seen from the street.
                Vector3 head = LampHead(pole, pivot, b, out float headBottom);
                head.y = headBottom - .05f;
                var lamp = new GameObject($"Lamp ({pivot.x:0.0}, {pivot.z:0.0})");
                lamp.transform.SetParent(lamps.transform, false);
                lamp.transform.SetPositionAndRotation(head, Quaternion.LookRotation(Vector3.down, arm.magnitude > .3f ? arm.normalized : Vector3.forward));
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 116f;
                light.innerSpotAngle = 60f;
                light.range = 14f;
                light.intensity = 40f;
                light.color = LampColour;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                lampLights.Add(light);
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "Bulb";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(lamp.transform, false);
                glow.transform.localPosition = Vector3.zero;                  // just under the head (the light looks down its z)
                glow.transform.localScale = new Vector3(.36f, .36f, .08f);
                var mr = glow.GetComponent<MeshRenderer>();
                mr.sharedMaterial = bulb;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                lamp.SetActive(false);
                nightOnly.Add(lamp);
            }
            report.AppendLine($"POLYGON street lamps: {poles.Count} (a spot light and a bulb at each head, off by day).");

            // ---- the old neighbourhood's street lamps (their glass heads are one merged mesh)
            int oldHeads = 0;
            var glass = renderers.FirstOrDefault(r => r.name == "Lamp glass" && r.transform.parent != null && r.transform.parent.name == "Street furniture"
                                                       && PathOf(r.transform).Contains("cohesive neighborhood V4"));
            if (glass != null)
            {
                foreach (var headPoint in Clusters(glass, 1.2f))
                {
                    var lamp = new GameObject($"Old lamp ({headPoint.x:0.0}, {headPoint.z:0.0})");
                    lamp.transform.SetParent(lamps.transform, false);
                    lamp.transform.position = headPoint;
                    var light = lamp.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 9f;
                    light.intensity = 3.2f;
                    light.color = OldLampColour;
                    light.shadows = LightShadows.None;
                    lampLights.Add(light);
                    lamp.SetActive(false);
                    nightOnly.Add(lamp);
                    oldHeads++;
                }
            }
            report.AppendLine(glass != null
                ? $"Old neighbourhood street lamps: {oldHeads} heads found in '{PathOf(glass.transform)}' (a point light in each; their glass already glows by evening)."
                : "Old neighbourhood street lamps: their 'Lamp glass' mesh was not found, so they get no light.");

            // ---- windows: POLYGON building parts and a window mask each
            var masks = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<Texture>($"{MaskFolder}Emissive_0{i}.png")).ToArray();
            var buildingParts = new List<Renderer>();
            var patterns = new List<int>();
            int dark = 0;
            foreach (var r in renderers)
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
                var src = root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
                if (src == null || !src.name.StartsWith("SM_Bld_", StringComparison.Ordinal)) continue;
                int pattern = Pattern(root.transform.position);
                buildingParts.Add(r);
                patterns.Add(pattern);
                if (pattern < 0) dark++;
            }
            report.AppendLine($"POLYGON building parts: {buildingParts.Count} ({buildingParts.Count - dark} with lit windows at night, {dark} dark). " +
                              $"Window masks found: {masks.Count(m => m != null)} of 5" + (masks.All(m => m != null) ? "." : " (the Synty folder is missing: no lit windows)."));

            // ---- signs
            var signs = new List<Renderer>();
            foreach (var r in renderers)
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
                var src = root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
                if (src == null) continue;
                string n = src.name;
                bool sign = n.StartsWith("SM_Prop_Sign_", StringComparison.Ordinal) || n.StartsWith("SM_Prop_LargeSign_", StringComparison.Ordinal)
                            || n.StartsWith("SM_Prop_Billboard", StringComparison.Ordinal);
                if (sign && !UnlitSigns.Any(u => n.StartsWith(u, StringComparison.Ordinal))) signs.Add(r);
            }
            report.AppendLine($"Signs that glow at night: {signs.Count} ({string.Join(", ", signs.Select(s => SourceName(s)).GroupBy(n => n).Select(g => g.Count() + " " + g.Key))}).");

            // ---- the late spot
            var lateGlass = new List<Renderer>();
            var lateLights = new List<Light>();
            var shop = PrefabRoots(renderers, name => name.StartsWith("SM_Bld_Shop", StringComparison.Ordinal))
                .OrderBy(g => (g.transform.position - LateSpotNear).sqrMagnitude).FirstOrDefault();
            if (shop != null && (shop.transform.position - LateSpotNear).magnitude < 3f)
            {
                var shopRenderers = shop.GetComponentsInChildren<Renderer>(true);
                lateGlass.AddRange(shopRenderers.Where(r => r.sharedMaterials.Any(m => m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)));
                Bounds building = BoundsOf(shop);
                report.AppendLine($"The late spot: {PrefabUtility.GetCorrespondingObjectFromSource(shop).name} '{PathOf(shop.transform)}', pivot ({shop.transform.position.x:0.0}, {shop.transform.position.z:0.0}), " +
                                  $"bounds x {building.min.x:0.0}..{building.max.x:0.0}, z {building.min.z:0.0}..{building.max.z:0.0}; {lateGlass.Count} glass part(s) glow warm.");
                foreach (var r in lateGlass)
                    report.AppendLine($"    glass '{r.name}' bounds x {r.bounds.min.x:0.0}..{r.bounds.max.x:0.0}, y {r.bounds.min.y:0.0}..{r.bounds.max.y:0.0}, z {r.bounds.min.z:0.0}..{r.bounds.max.z:0.0}");
                // A light spills out of each side that fronts a street.
                foreach (var (point, facing) in StreetFaces(building))
                {
                    var spill = new GameObject($"Late spot - window light ({facing})");
                    spill.transform.SetParent(group.transform, false);
                    spill.transform.position = point;
                    var light = spill.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 7f;
                    light.intensity = 4f;
                    light.color = LateSpotColour;
                    light.shadows = LightShadows.None;
                    lateLights.Add(light);
                    spill.SetActive(false);
                    nightOnly.Add(spill);
                    report.AppendLine($"    a light spills onto the pavement on its {facing} side at ({point.x:0.0}, {point.z:0.0}).");
                }
            }
            else report.AppendLine("The late spot: no POLYGON shop found at the south-west junction.");

            // ---- the night look
            var profile = NightLook();
            var look = new GameObject("Night look (weight 0 by day)");
            look.transform.SetParent(group.transform, false);
            var volume = look.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = profile;
            report.AppendLine($"Night look: '{ProfilePath}' (bloom, a little brighter and cooler), on a global Volume at weight 0.");

            walk.nightOnly = nightOnly.ToArray();
            walk.lampLights = lampLights.ToArray();
            walk.buildingRenderers = buildingParts.ToArray();
            walk.windowPatterns = patterns.ToArray();
            walk.windowMasks = masks;
            walk.signRenderers = signs.ToArray();
            walk.lateSpotGlass = lateGlass.ToArray();
            walk.lateSpotLights = lateLights.ToArray();
            walk.nightLook = volume;

            // ---- part 2: the edges (so far: the patio's day fence; the night's collision is made at run time)
            var fence = PatioFence();
            walk.dayOnlyColliders = fence.ToArray();
            report.AppendLine($"The patio's invisible day fence (it keeps Ace in the café by day): {fence.Count} box collider(s) under " +
                              "'Window collision boundaries', switched off while the night runs: " +
                              string.Join("; ", fence.Select(c => { var p = c.transform.TransformPoint(((BoxCollider)c).center); return $"centre ({p.x:0.0}, {p.z:0.0})"; })) + ".");
            walk.nightCollision = NightCollisionList.Build(group.transform, report);
            EditorUtility.SetDirty(walk);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string folder = LogFolder("night-setup");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 1 - set up the night lighting\n\n" + report);
            Debug.Log(Tag + "Set up. Nothing is switched on by day; Fixit Fidget > Night > Play the night walk (lab) shows it. " +
                      "Read the scene diff before saving.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Set-up FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // The café's invisible fence round its patio keeps Ace in the café by day. The same object's other
    // boxes are the café room's own glass walls; they stay.
    static List<Collider> PatioFence()
    {
        var result = new List<Collider>();
        var holder = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "Window collision boundaries");
        if (holder == null) return result;
        foreach (var box in holder.GetComponents<BoxCollider>())
            if (box.enabled && !box.isTrigger && box.transform.TransformPoint(box.center).z < -.5f) result.Add(box);
        return result;
    }

    // Writes the list of what is solid by night again, and nothing else: the night group, its lamps and
    // its references stay exactly as they are (the NightWalk already points at the list asset, which is
    // rewritten in place). For after a change to the city, or to the rules of the list.
    [MenuItem(Menu + "Night walk 2 - Rebuild the list of what is solid by night (Edit Mode)")]
    static void RebuildSolidList()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit Mode only: in Play Mode, static batching has merged the city's meshes.");
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            var group = layout != null ? layout.transform.Find(GroupName) : null;
            var walk = group != null ? group.GetComponent<NightWalk>() : null;
            if (walk == null) throw new InvalidOperationException("No night group with a NightWalk: run Night walk 1 (the set-up) first.");
            var list = NightCollisionList.Build(group, report);
            if (walk.nightCollision != list)
            {
                Undo.RecordObject(walk, "Night walk - point at the list of what is solid by night");
                walk.nightCollision = list;
                EditorUtility.SetDirty(walk);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                report.AppendLine("The NightWalk now points at the list (the scene changed: save it).");
            }
            else report.AppendLine("The NightWalk already points at the list: the scene is unchanged.");
            string folder = LogFolder("night-solid-list");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 2 - rebuild the list of what is solid by night\n\n" + report);
            Debug.Log(Tag + "Rebuilt the list of what is solid by night.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Rebuilding the list FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Night walk - Take the night lighting out again")]
    static void TakeOut()
    {
        try
        {
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            var group = layout != null ? layout.transform.Find(GroupName) : null;
            if (group == null) { Debug.Log(Tag + "There is no night group to take out."); return; }
            Undo.DestroyObjectImmediate(group.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + "Took the night group out (Edit > Undo puts it back). The profile and bulb material assets stay; they are harmless.");
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    // ------------------------------------------------------------------ the lab

    [MenuItem(Menu + "Play the night walk (lab)")]
    static void PlayNight()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Night walk lab session (Day 5 test save {path}); your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Play the night walk (lab)", true)]
    static bool CanPlayNight() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A night request that never became a Play session must not make the next lab session a night.
    [InitializeOnLoadMethod]
    static void ClearStaleNightRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(NightWalk.PendingKey))
        {
            PlayerPrefs.DeleteKey(NightWalk.PendingKey);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------ photos

    [MenuItem(Menu + "Night walk - Photograph the night (Play Mode, night walk)")]
    static void Photograph()
    {
        try
        {
            if (!EditorApplication.isPlaying || NightWalk.Instance == null || !NightWalk.Instance.Active)
                throw new InvalidOperationException("Start Fixit Fidget > Night > Play the night walk (lab) first.");
            string folder = LogFolder("night-photos");
            var sb = new StringBuilder();
            foreach (var view in CityPackChecks.Views)
            {
                bool map = view.name.Contains("top-down");
                bool fog = RenderSettings.fog;
                if (map) RenderSettings.fog = false;
                try { Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, view.isometric); }
                finally { RenderSettings.fog = fog; }
                sb.AppendLine(view.name);
            }
            File.WriteAllText(Path.Combine(folder, "views.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(folder, "night-state.txt"), NightWalk.Instance.Describe());
            Debug.Log(Tag + $"Night photos ({CityPackChecks.Views.Length}) in {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Photos FAILED: " + e.Message + "\n" + e); }
    }

    // The day must look exactly the same with the night group in the scene as without it.
    // Run this in Edit Mode twice, once with the group and once after "Take the night lighting
    // out again" (Edit > Undo puts it back): the two sets of photos should match pixel for pixel.
    [MenuItem(Menu + "Night walk - Photograph the day (Edit Mode, before-and-after check)")]
    static void PhotographTheDay()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            bool withNight = layout != null && layout.transform.Find(GroupName) != null;
            string folder = LogFolder(withNight ? "day-photos-with-night-group" : "day-photos-without-night-group");
            var sb = new StringBuilder();
            foreach (var view in CityPackChecks.Views)
            {
                bool map = view.name.Contains("top-down");
                bool fog = RenderSettings.fog;
                if (map) RenderSettings.fog = false;
                try { Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, view.isometric); }
                finally { RenderSettings.fog = fog; }
                sb.AppendLine(view.name);
            }
            sb.AppendLine(withNight ? "night group: in the scene" : "night group: not in the scene");
            File.WriteAllText(Path.Combine(folder, "views.txt"), sb.ToString());
            Debug.Log(Tag + $"Day photos ({CityPackChecks.Views.Length}, night group {(withNight ? "in" : "out")}) in {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Day photos FAILED: " + e.Message + "\n" + e); }
    }

    // Any views, from a file: Logs/Night/views.json. By day in Edit Mode, at night in a night walk.
    // { "views": [ { "name": "front-street-west-end", "position": {"x":-24,"y":1.7,"z":-7.7},
    //                "target": {"x":-45,"y":1.5,"z":-7.7}, "fov": 62, "isometric": false, "noFog": false } ] }
    [Serializable] class ViewList { public ViewSpec[] views = Array.Empty<ViewSpec>(); }
    [Serializable] class ViewSpec { public string name = "view"; public Vector3 position; public Vector3 target; public float fov = 60f; public bool isometric; public bool noFog; }

    [MenuItem(Menu + "Night walk - Photograph the views in Logs - Night - views.json")]
    static void PhotographViews()
    {
        try
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", "views.json"));
            if (!File.Exists(file)) throw new FileNotFoundException("There is no Logs/Night/views.json.");
            var list = JsonUtility.FromJson<ViewList>(File.ReadAllText(file));
            bool night = EditorApplication.isPlaying && NightWalk.Instance != null && NightWalk.Instance.Active;
            string folder = LogFolder(night ? "views-night" : "views-day");
            foreach (var v in list.views)
            {
                bool fog = RenderSettings.fog;
                if (v.noFog) RenderSettings.fog = false;
                try { Capture(Path.Combine(folder, v.name + ".png"), v.position, v.target, v.fov, v.isometric); }
                finally { RenderSettings.fog = fog; }
            }
            File.Copy(file, Path.Combine(folder, "views.json"));
            if (night) File.WriteAllText(Path.Combine(folder, "night-state.txt"), NightWalk.Instance.Describe());
            Debug.Log(Tag + $"{list.views.Length} views ({(night ? "night" : "day")}) in {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Views FAILED: " + e.Message + "\n" + e); }
    }

    /// <summary>
    /// CafeSecondPassSteps.Capture, with post-processing on (bloom matters at night): the same views,
    /// the same cut-away walls hidden for the overhead ones.
    /// </summary>
    internal static void Capture(string path, Vector3 position, Vector3 target, float fov, bool isometric)
    {
        var hidden = new List<Renderer>();
        if (isometric)
        {
            var mode = CityPackChecks.InScene<CafeViewMode>().FirstOrDefault();
            if (mode != null)
            {
                hidden.AddRange(mode.overheadFixtures.Where(r => r != null && r.enabled));
                Vector3 centre = new Vector3(0f, 1.2f, 9f);
                foreach (var wall in mode.cutawayWalls.Where(r => r != null && r.enabled))
                {
                    Vector3 toCentre = centre - position;
                    if (wall.bounds.IntersectRay(new Ray(position, toCentre.normalized), out float hit) && hit < toCentre.magnitude - .02f)
                        hidden.Add(wall);
                }
            }
        }
        foreach (var r in hidden) r.forceRenderingOff = true;
        var go = new GameObject("Temporary night photo camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var rt = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 260f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Stable per place, the same every night: 0-4 a window mask, -1 (about one in six) dark.</summary>
    internal static int Pattern(Vector3 p)
    {
        unchecked
        {
            int x = Mathf.RoundToInt(p.x * 10f), y = Mathf.RoundToInt(p.y * 10f), z = Mathf.RoundToInt(p.z * 10f);
            uint h = (uint)(x * 73856093) ^ (uint)(y * 83492791) ^ (uint)(z * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            int k = (int)(h % 6u);
            return k < 5 ? k : -1;
        }
    }

    // The four streets round the café block (their middle lines), from the survey's lamps and crossings.
    static readonly float[] StreetsX = { -12.0f, 12.6f };
    static readonly float[] StreetsZ = { -7.7f, 23.2f };

    /// <summary>Points 1 m outside each side of a building that fronts one of the four streets (within 7 m of its middle line).</summary>
    static IEnumerable<(Vector3 point, string facing)> StreetFaces(Bounds b)
    {
        const float Near = 7f, Out = 1f, Y = 2.6f;
        if (StreetsX.Any(x => Mathf.Abs(b.max.x - x) < Near && b.max.x < x)) yield return (new Vector3(b.max.x + Out, Y, b.center.z), "east");
        if (StreetsX.Any(x => Mathf.Abs(b.min.x - x) < Near && b.min.x > x)) yield return (new Vector3(b.min.x - Out, Y, b.center.z), "west");
        if (StreetsZ.Any(z => Mathf.Abs(b.max.z - z) < Near && b.max.z < z)) yield return (new Vector3(b.center.x, Y, b.max.z + Out), "north");
        if (StreetsZ.Any(z => Mathf.Abs(b.min.z - z) < Near && b.min.z > z)) yield return (new Vector3(b.center.x, Y, b.min.z - Out), "south");
    }

    /// <summary>
    /// A POLYGON lamp's head: of the pole's vertices in its top metre, the ones furthest out from the
    /// pole (the tip of the arm). Returns their middle; headBottom is the lowest of them.
    /// </summary>
    static Vector3 LampHead(GameObject pole, Vector3 pivot, Bounds bounds, out float headBottom)
    {
        var top = new List<Vector3>();
        foreach (var f in pole.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            foreach (var v in f.sharedMesh.vertices)
            {
                Vector3 w = f.transform.TransformPoint(v);
                if (w.y > bounds.max.y - 1.2f) top.Add(w);
            }
        }
        headBottom = bounds.max.y - .35f;
        if (top.Count == 0) return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        float reach = top.Max(w => Flat2(w - pivot).magnitude);
        var head = top.Where(w => Flat2(w - pivot).magnitude > reach - .45f).ToList();
        headBottom = head.Min(w => w.y);
        Vector3 centre = Vector3.zero;
        foreach (var w in head) centre += w;
        return centre / head.Count;
    }

    static IEnumerable<GameObject> PrefabRoots(IEnumerable<Renderer> renderers, Func<string, bool> sourceName)
    {
        var seen = new HashSet<GameObject>();
        foreach (var r in renderers)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (root == null || !seen.Add(root)) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (src != null && sourceName(src.name)) yield return root;
        }
    }

    static string SourceName(Renderer r)
    {
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
        var src = root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        return src != null ? src.name : r.name;
    }

    static Bounds BoundsOf(GameObject root)
    {
        var rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Groups a merged mesh's vertices into the separate pieces it was merged from (cells of the given size).</summary>
    static List<Vector3> Clusters(Renderer renderer, float cell)
    {
        var result = new List<Vector3>();
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return result;
        var world = filter.sharedMesh.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
        var cells = new Dictionary<Vector2Int, List<Vector3>>();
        foreach (var v in world)
        {
            var key = new Vector2Int(Mathf.FloorToInt(v.x / cell), Mathf.FloorToInt(v.z / cell));
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
            list.Add(v);
        }
        var done = new HashSet<Vector2Int>();
        foreach (var start in cells.Keys)
        {
            if (!done.Add(start)) continue;
            var piece = new List<Vector3>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                piece.AddRange(cells[c]);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var n = new Vector2Int(c.x + dx, c.y + dz);
                        if (cells.ContainsKey(n) && done.Add(n)) queue.Enqueue(n);
                    }
            }
            Vector3 centre = Vector3.zero;
            foreach (var v in piece) centre += v;
            centre /= piece.Count;
            result.Add(centre);
        }
        return result;
    }

    // Unlit and brighter than white, so it reads as a light and the night look's bloom catches it.
    // (A Lit material's emission keyword did not survive being saved from script.)
    static Material BulbMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(BulbMaterialPath);
        if (m == null)
        {
            m = new Material(shader) { name = "Night walk - lamp bulb" };
            AssetDatabase.CreateAsset(m, BulbMaterialPath);
        }
        m.shader = shader;
        m.SetColor("_BaseColor", new Color(6f, 4.6f, 3f));
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Rebuilt every time, overriding only what the night changes (the rest comes from the café's own volume).
    static VolumeProfile NightLook()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null) AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var bloom = profile.Add<Bloom>(false);
        bloom.threshold.Override(.85f);
        bloom.intensity.Override(.9f);
        bloom.scatter.Override(.72f);
        var colour = profile.Add<ColorAdjustments>(false);
        colour.postExposure.Override(.35f);
        colour.saturation.Override(-6f);
        var balance = profile.Add<WhiteBalance>(false);
        balance.temperature.Override(-10f);
        foreach (var component in profile.components)
        {
            component.name = component.GetType().Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static Vector2 Flat2(Vector3 v) => new Vector2(v.x, v.z);

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
