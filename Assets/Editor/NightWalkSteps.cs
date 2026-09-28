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
//
// NIGHT WALK, PART 4a: ASLEEP BUT LIVED-IN (claude/night-city-proposal.md §9)
//   ... > Night walk 4a - Build the lived-in windows: the houses' rooms, window by window (NightLivedIn).
//   ... > Night walk 4a - Put up house numbers and street signs / Take them down again (day and night).
//   ... > Night walk 4a - Check the lived-in street (read-only): what is set up, and at night the rooms' timetable.
//   ... > Night walk 4a - Photograph the night over the hours (Play Mode): the clock moved on, photos at each hour.
//   ... > Night walk 4a - Send the neighbours home now / Move the clock on an hour (Play Mode): for checks.
//   ... > Night walk 4a - Watch a neighbour come home (Play Mode): photos of one walking home and letting themselves in.
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

            // ---- part 2: the road works that close the 8 street ends at night (NightEdges; the
            // corners closed for good are ordinary scenery outside this group, and stay as they are)
            nightOnly.Add(NightEdges.BuildRoadWorks(group.transform, report));

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
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false, notebook = LabNotebook() });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Night walk lab session (Day 5 test save {path}); your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Play the night walk (lab)", true)]
    static bool CanPlayNight() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Test data for the lab only (the playtest save is never touched): what Grace told Ace on her camera
    // visit, and her house, seen twice, so "likely". The notebook page at night (N) shows it.
    static NotebookFactData[] LabNotebook()
    {
        var facts = new List<NotebookFactData>();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName))
        {
            fact.day = 3;
            facts.Add(fact);
        }
        HomeDoor door = SceneManager.GetActiveScene().IsValid()
            ? CityPackChecks.InScene<HomeDoor>().FirstOrDefault(d => d.homeId == "home.grace") : null;
        NotebookFactData home = NotebookEntries.HomeSeen(NotebookEntries.GraceId, NotebookEntries.GraceName,
            door != null ? door.looks : "the saffron house", door != null ? door.houseNumber : "12", door != null ? door.streetId : "west",
            true, Notebook.Sureness.Likely);
        home.day = 3;
        home.surerDay = 4;
        facts.Add(home);
        return facts.ToArray();
    }

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

    // ------------------------------------------------------------------ part 4a: the lived-in street

    [MenuItem(Menu + "Night walk 4a - Build the lived-in windows (rooms list, Edit Mode)")]
    static void BuildLivedInWindows()
    {
        var report = new StringBuilder();
        try
        {
            CityPackChecks.RequireScene();
            NightWalk walk = TheNightWalk();
            NightRooms rooms = NightLivedIn.BuildRooms(walk, report);
            if (walk.rooms != rooms)
            {
                Undo.RecordObject(walk, "Night walk 4a - point at the rooms list");
                walk.rooms = rooms;
                EditorUtility.SetDirty(walk);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                report.AppendLine("The NightWalk now points at the rooms list (the scene changed: save it).");
            }
            else report.AppendLine("The NightWalk already points at the rooms list: the scene is unchanged.");
            string folder = LogFolder("night-rooms");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 4a - build the lived-in windows\n\n" + report);
            Debug.Log(Tag + "Built the rooms list. Nothing changes by day; at night the houses are lit room by room.\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "Building the rooms list FAILED: " + e.Message + "\n" + report + "\n" + e); }
    }

    [MenuItem(Menu + "Night walk 4a - Put up house numbers and street signs (day and night, Edit Mode)")]
    static void PutUpNumbersAndSigns()
    {
        var report = new StringBuilder();
        try
        {
            CityPackChecks.RequireScene();
            Undo.SetCurrentGroupName("Night walk 4a - house numbers and street signs");
            int undoGroup = Undo.GetCurrentGroup();
            NightLivedIn.PutUp(report);
            Undo.CollapseUndoOperations(undoGroup);
            string folder = LogFolder("night-numbers-and-signs");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 4a - house numbers and street signs\n\n" + report);
            Debug.Log(Tag + "Put up house numbers and street signs (seen by day too; Edit > Undo takes them down). Save the scene once the diff is read.\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "House numbers and street signs FAILED: " + e.Message + "\n" + report + "\n" + e); }
    }

    [MenuItem(Menu + "Night walk 4a - Take the house numbers and street signs down again")]
    static void TakeDownNumbersAndSigns()
    {
        try
        {
            CityPackChecks.RequireScene();
            var report = new StringBuilder();
            NightLivedIn.TakeDown(report);
            Debug.Log(Tag + report + " (Edit > Undo puts them back; the three materials stay, they are harmless.)");
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    [MenuItem(Menu + "Night walk 4a - Check the lived-in street (read-only)")]
    static void CheckLivedIn()
    {
        var report = new StringBuilder();
        try
        {
            NightWalk walk = CityPackChecks.InScene<NightWalk>().FirstOrDefault();
            if (walk == null) throw new InvalidOperationException("No NightWalk in the scene: run Night walk 1 first.");
            NightRooms rooms = walk.rooms;
            if (rooms == null) report.AppendLine("Rooms list: NOT SET (run Night walk 4a - Build the lived-in windows).");
            else
            {
                report.AppendLine($"Rooms list '{AssetDatabase.GetAssetPath(rooms)}': {rooms.rooms.Length} rooms in {rooms.houses} houses, " +
                                  $"{rooms.caps.Length} dark caps, {rooms.curtainPaths.Length} merged curtains to hide at night.");
                foreach (var house in rooms.rooms.Where(r => r != null).GroupBy(r => r.housePath))
                {
                    var list = house.ToList();
                    bool found = NightHomes.FindByPath(house.Key) != null;
                    report.AppendLine($"  {list[0].house}: {list.Count} rooms on {list.Select(r => r.floor).Distinct().Count()} floors, " +
                                      $"{list.Count(r => r.shopFront)} shop fronts, {list.Count(r => r.curtains != null)} with curtains, " +
                                      $"{list.Count(r => r.panes == null)} without a pane mesh{(found ? "" : "; THE HOUSE IS NOT IN THE SCENE")}");
                }
            }
            NightWalk.HomeHours hours = walk.homeHours;
            HomeDoor grace = CityPackChecks.InScene<HomeDoor>().FirstOrDefault(d => d.homeId == hours.graceHomeId);
            if (grace == null) report.AppendLine($"Grace's door '{hours.graceHomeId}': NOT FOUND (her house stays dark).");
            else
            {
                Transform house = grace.transform.parent;
                bool listed = rooms != null && rooms.rooms.Any(r => r != null && house != null && r.housePath == NightLivedIn.PathOf(house) && r.index == hours.graceRoom);
                report.AppendLine($"Grace's door '{grace.name}' ({grace.Address}) on '{(house != null ? house.name : "?")}': room {hours.graceRoom} lit until " +
                                  $"{NightHomes.Clock(hours.graceBedtime)}, {(listed ? "in the rooms list" : "NOT in the rooms list")}.");
            }
            report.AppendLine($"Houses: {hours.litAtStart:P0} of the rooms lit at {NightHomes.Clock(walk.nightHour)}, downstairs out by {NightHomes.Clock(hours.downstairsOutBy)}, " +
                              $"all by {NightHomes.Clock(hours.lastLightsOut)}; {hours.tvRooms} TV rooms; {hours.wakes} rooms where someone gets up later.");
            report.AppendLine($"City: {walk.cityLitAtStart:P0} of {walk.buildingRenderers.Length} building parts lit at the start, {walk.cityNightOwls:P0} night owls, " +
                              $"the rest dark by {NightHomes.Clock(walk.cityLastLightsOut)}; {walk.cityTvWindows} TVs. Clock: {NightHomes.Clock(walk.nightHour)} to " +
                              $"{NightHomes.Clock(walk.nightEndsAt)} in {walk.nightMinutes:0.#} minutes.");
            foreach (NightWalk.Neighbour n in walk.neighbours)
            {
                Transform house = CityPackChecks.InScene<StreetDoor>().Select(d => d.transform.parent).FirstOrDefault(h => h != null && h.name == n.house);
                StreetDoor door = house != null ? house.GetComponentInChildren<StreetDoor>(true) : null;
                string walkText = n.walk.Length == 0 ? "NO WALK" : $"{n.walk.Length} points, {Length(n.walk):0.0} m";
                if (door != null && n.walk.Length > 0)
                {
                    Vector3 stoop = door.DoorwayPoint + door.Outward * n.stoop;
                    Vector3 last = n.walk[n.walk.Length - 1];
                    walkText += $", then {Flat2(stoop - last).magnitude:0.0} m to the step and {n.stoop:0.0} m up to the door";
                }
                report.AppendLine($"Neighbour: {n.name}, home at {NightHomes.Clock(n.comesHomeAt)} to '{n.house}' " +
                                  $"({(house == null ? "HOUSE NOT FOUND" : door == null ? "NO FRONT DOOR" : "door at " + Vector(door.DoorwayPoint))}); walk {walkText}; " +
                                  $"lights {n.downstairsFor:0.00} h, then upstairs {n.upstairsFor:0.00} h.");
            }
            GameObject lamp = walk.nightOnly.FirstOrDefault(g => g != null && g.name == walk.flickeringLamp);
            bool cap = rooms != null && rooms.caps.Any(c => c != null && c.name == walk.flickeringLamp && c.mesh != null);
            report.AppendLine($"Broken: the failing bulb '{walk.flickeringLamp}': {(lamp != null ? "found" : "NOT FOUND")}, dark cap {(cap ? "in the rooms list" : "none")}.");
            GameObject stutter = walk.nightOnly.FirstOrDefault(g => g != null && g.name == walk.stutteringLamp);
            report.AppendLine($"Broken: the stuttering street lamp '{walk.stutteringLamp}': " +
                              (stutter != null ? $"found, {stutter.GetComponentsInChildren<Light>(true).Length} light, {stutter.GetComponentsInChildren<Renderer>(true).Length} bulb" : "NOT FOUND") + ".");
            Renderer sign = walk.signRenderers.Where(r => r != null).OrderBy(r => Flat2(r.bounds.center - walk.brokenSignNear).sqrMagnitude).FirstOrDefault();
            report.AppendLine(sign != null
                ? $"Broken: the sign that cuts out: '{NightLivedIn.PathOf(sign.transform)}', {Flat2(sign.bounds.center - walk.brokenSignNear).magnitude:0.0} m from the point given."
                : "Broken: no glowing signs.");
            var numbers = CityPackChecks.InScene<HouseNumber>();
            report.AppendLine($"House numbers: {numbers.Length}" + string.Concat(numbers.Select(h =>
                $"\n  '{h.Shown}' on {(h.transform.parent != null ? h.transform.parent.name : "?")}{(h.home != null ? " (" + h.home.Address + ")" : "")}")));
            var signs = CityPackChecks.InScene<StreetNameSign>();
            report.AppendLine($"Street signs: {signs.Length}" + string.Concat(signs.Select(s =>
                $"\n  '{s.Says()}' at ({s.transform.position.x:0.0}, {s.transform.position.z:0.0})")));
            if (EditorApplication.isPlaying && NightWalk.Instance != null && NightWalk.Instance.Active)
            {
                report.AppendLine().AppendLine("Tonight so far:").AppendLine(NightWalk.Instance.Describe());
                if (NightWalk.Instance.Homes != null) report.AppendLine("The rooms' timetable:").Append(NightWalk.Instance.Homes.Timetable());
            }
            string folder = LogFolder("lived-in-check");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 4a - check the lived-in street\n\n" + report);
            Debug.Log(Tag + "Lived-in street check (" + folder + "):\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "Check FAILED: " + e.Message + "\n" + report + "\n" + e); }
    }

    // Three views of how the night changes: the bay-window houses across the west street, the courtyard
    // shops across the east street, and the whole city.
    static readonly (string name, Vector3 position, Vector3 target, float fov)[] HourViews =
    {
        ("west-street-houses", new Vector3(-4.5f, 12.5f, 7f), new Vector3(-17.5f, 3.5f, 7f), 62f),
        ("courtyard-shops", new Vector3(7.5f, 11f, -1.5f), new Vector3(19.5f, 3.5f, 1.5f), 62f),
        ("city-overview", new Vector3(-55f, 48f, -60f), new Vector3(0f, 0f, 10f), 55f),
    };
    static readonly float[] PhotoHours = { 23.1f, 23.9f, 24.6f, 25.4f, 26.3f, 27.9f };

    [MenuItem(Menu + "Night walk 4a - Photograph the night over the hours (Play Mode, night walk)")]
    static void PhotographTheHours()
    {
        NightWalk walk = NightWalk.Instance;
        float before = walk != null ? walk.Hour : 0f;
        try
        {
            if (!EditorApplication.isPlaying || walk == null || !walk.Active)
                throw new InvalidOperationException("Start Fixit Fidget > Night > Play the night walk (lab) first.");
            string folder = LogFolder("night-hours");
            var notes = new StringBuilder();
            foreach (float hour in PhotoHours)
            {
                walk.SetHour(hour);
                string stamp = TwentyFour(hour);
                foreach (var view in HourViews)
                    Capture(Path.Combine(folder, $"{stamp}-{view.name}.png"), view.position, view.target, view.fov, false);
                notes.AppendLine($"==== {NightHomes.Clock(hour)} ====").AppendLine(walk.Describe());
            }
            notes.AppendLine("Neighbours due before the last photo's hour were counted home (they skip their walk this session).");
            File.WriteAllText(Path.Combine(folder, "night-state.txt"), notes.ToString());
            Debug.Log(Tag + $"Night over the hours: {PhotoHours.Length} hours x {HourViews.Length} views in {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Photos over the hours FAILED: " + e.Message + "\n" + e); }
        finally { if (walk != null && walk.Active) walk.SetHour(before); }
    }

    [MenuItem(Menu + "Night walk 4a - Send the neighbours home now (Play Mode, night walk)")]
    static void SendNeighboursHome()
    {
        NightWalk walk = NightWalk.Instance;
        if (!EditorApplication.isPlaying || walk == null || !walk.Active || walk.NeighbourWalks == null)
        { Debug.LogError(Tag + "Start Fixit Fidget > Night > Play the night walk (lab) first."); return; }
        walk.NeighbourWalks.SendHomeNow(walk.Hour);
        Debug.Log(Tag + "The neighbours still out set off now (each appears once the camera can't see their corner).");
    }

    // The core moment, photographed: the next neighbour still out (one whose corner the game camera can't
    // see, if there is one) sets off now, and a camera across the street from their house takes a photo
    // every second and a half until they are in and their light is on. Photos and notes go to
    // Logs/Night/neighbour-watch-*/. Only neighbours still out can be watched: in a new lab session all are.
    static bool watching;
    static int watchIndex, watchShots;
    static double watchNext, watchUntil, watchHomeAt;
    static string watchFolder;
    static Vector3 watchCamera, watchTarget;
    static StringBuilder watchNotes;

    [MenuItem(Menu + "Night walk 4a - Watch a neighbour come home (Play Mode, night walk)")]
    static void WatchANeighbour()
    {
        NightWalk walk = NightWalk.Instance;
        try
        {
            if (!EditorApplication.isPlaying || walk == null || !walk.Active || walk.NeighbourWalks == null)
                throw new InvalidOperationException("Start Fixit Fidget > Night > Play the night walk (lab) first.");
            if (watching) throw new InvalidOperationException("Already watching one.");
            NightNeighbours walks = walk.NeighbourWalks;
            int which = walks.NextOut(true);
            bool inView = which < 0;
            if (inView) which = walks.NextOut();
            if (which < 0) throw new InvalidOperationException("Everyone is home already: start a new lab session to watch one.");
            NightWalk.Neighbour n = walk.neighbours[which];
            Transform house = CityPackChecks.InScene<StreetDoor>().Select(d => d.transform.parent).FirstOrDefault(h => h != null && h.name == n.house);
            StreetDoor door = house != null ? house.GetComponentInChildren<StreetDoor>(true) : null;
            Vector3 from = n.walk[0], to = door != null ? door.DoorwayPoint : n.walk[n.walk.Length - 1];
            Vector3 mid = (from + to) * .5f;
            Vector3 outward = door != null ? door.Outward : Vector3.right;
            Vector3 along = to - from;
            along.y = 0f;
            along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.forward;
            // Across the street from their house, raised, looking back at the pavement and the front door.
            watchCamera = mid + outward * 10f + Vector3.up * 7.5f - along * 3f;
            watchTarget = mid + outward * .8f + Vector3.up * 1.2f + along * 1.5f;
            walks.SendHome(which, walk.Hour);
            watchFolder = LogFolder("neighbour-watch");
            watchIndex = which;
            watchShots = 0;
            watchNext = EditorApplication.timeSinceStartup + .3;
            watchUntil = EditorApplication.timeSinceStartup + 110;
            watchHomeAt = -1;
            watchNotes = new StringBuilder().AppendLine($"Watching {n.name} ({n.house}) come home from {NightHomes.Clock(walk.Hour)}.");
            if (inView) watchNotes.AppendLine("Every neighbour's corner was in the game camera's view: they set off once it isn't (walk Ace away).");
            watching = true;
            EditorApplication.update += Watch;
            Debug.Log(Tag + $"Watching {n.name} come home: a photo every 1.5 s to {watchFolder}" +
                      (inView ? " (their corner is in view: they set off once it isn't)." : "."));
        }
        catch (Exception e) { Debug.LogError(Tag + "Watch FAILED: " + e.Message); }
    }

    static void Watch()
    {
        NightWalk walk = NightWalk.Instance;
        double now = EditorApplication.timeSinceStartup;
        if (!EditorApplication.isPlaying || walk == null || !walk.Active || walk.NeighbourWalks == null) { StopWatching("the night ended first"); return; }
        if (now < watchNext) return;
        watchNext = now + 1.5;
        string state = walk.NeighbourWalks.StateOf(watchIndex);
        bool home = state.StartsWith("home", StringComparison.Ordinal) || state.StartsWith("came", StringComparison.Ordinal);
        if (home && watchHomeAt < 0) watchHomeAt = now;
        try { Capture(Path.Combine(watchFolder, $"{watchShots:00}-{TwentyFour(walk.Hour)}.png"), watchCamera, watchTarget, 55f, false); }
        catch (Exception e) { watchNotes.AppendLine("photo failed: " + e.Message); }
        Vector3? at = walk.NeighbourWalks.BodyOf(watchIndex);
        watchNotes.AppendLine($"{watchShots:00} {NightHomes.Clock(walk.Hour)}: {state}" + (at.HasValue ? $" at ({at.Value.x:0.00}, {at.Value.z:0.00})" : ""));
        watchShots++;
        if (watchHomeAt >= 0 && now - watchHomeAt > 3.5) StopWatching("in, and their light is on");
        else if (now > watchUntil) StopWatching("stopped after 110 s");
    }

    static void StopWatching(string why)
    {
        EditorApplication.update -= Watch;
        watching = false;
        try
        {
            watchNotes?.AppendLine($"Stopped: {why}.");
            if (NightWalk.Instance != null && NightWalk.Instance.Active) watchNotes?.AppendLine().AppendLine(NightWalk.Instance.Describe());
            if (watchFolder != null) File.WriteAllText(Path.Combine(watchFolder, "notes.txt"), watchNotes?.ToString() ?? "");
            Debug.Log(Tag + $"Neighbour watch: {why}; {watchShots} photos in {watchFolder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Neighbour watch notes FAILED: " + e.Message); }
    }

    [MenuItem(Menu + "Night walk 4a - Move the clock on an hour (Play Mode, night walk)")]
    static void MoveTheClockOn()
    {
        NightWalk walk = NightWalk.Instance;
        if (!EditorApplication.isPlaying || walk == null || !walk.Active)
        { Debug.LogError(Tag + "Start Fixit Fidget > Night > Play the night walk (lab) first."); return; }
        walk.SetHour(walk.Hour + 1f);
        Debug.Log(Tag + $"The clock now reads {NightHomes.Clock(walk.Hour)}.");
    }

    static NightWalk TheNightWalk()
    {
        var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
        var group = layout != null ? layout.transform.Find(GroupName) : null;
        var walk = group != null ? group.GetComponent<NightWalk>() : null;
        if (walk == null) throw new InvalidOperationException("No night group with a NightWalk: run Night walk 1 (the set-up) first.");
        return walk;
    }

    static string TwentyFour(float hour)
    {
        int minutes = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f + .001f);
        return $"{minutes / 60:00}{minutes % 60:00}";
    }

    static float Length(Vector3[] points)
    {
        float sum = 0f;
        for (int i = 1; i < points.Length; i++) sum += Flat2(points[i] - points[i - 1]).magnitude;
        return sum;
    }

    static string Vector(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

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

    // Part 3's check: Ace walks out of the café and round the 9 blocks by itself, overhead with the camera
    // turned so buildings stand in the way, then a stretch in first person (NightTour). Photos, a trail and a
    // report go to Logs/Night/night-tour-*. Hands off the mouse while it runs (about a minute and a half).
    [MenuItem(Menu + "Night walk 3 - Walk the tour (Play Mode, night walk)")]
    static void WalkTheTour()
    {
        try
        {
            if (!EditorApplication.isPlaying || NightWalk.Instance == null || !NightWalk.Instance.Active)
                throw new InvalidOperationException("Start Fixit Fidget > Night > Play the night walk (lab) first.");
            if (UnityEngine.Object.FindAnyObjectByType<NightTour>() != null)
                throw new InvalidOperationException("A tour is already walking.");
            var go = new GameObject("Night tour (this Play session only)");
            go.AddComponent<NightTour>().folder = LogFolder("night-tour");
        }
        catch (Exception e) { Debug.LogError(Tag + "Tour FAILED: " + e.Message); }
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
