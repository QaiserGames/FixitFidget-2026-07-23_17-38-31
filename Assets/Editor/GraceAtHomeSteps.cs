#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// BREAK-INS, CHUNK C: GRACE AT HOME (6 Oct 2026; claude/chunk-c-grace-at-home-plan.md)
//
//   Fixit Fidget > Night > Break-ins 6 - Put Grace at home (scene)
//   Fixit Fidget > Night > Break-ins 6 - Take Grace back out (scene)
//   Fixit Fidget > Night > Break-ins 7 - Grace at home: her night and her eyes and ears (lab, a check)
//   Fixit Fidget > Night > Break-ins 7 - Grace at home: her Thursday (lab, a check)
//
// Put (Edit Mode, with undo; save the scene afterwards; safe to run again, it refreshes what it found):
//   * GraceAtHome on her rooms' root, beside GraceHouse, given what her night uses: her look (her profile's stand-in
//     look, Character_BusinessWoman), her lamps by name (Break-ins 1 and 5 made them: the standard lamp, the bedside lamp,
//     the landing light, the kitchen light), the TV and its screen, her front window's curtains, the made quilt, the
//     bedroom doors' hinges and her armchair;
//   * a light for the TV (night only, off in the scene: it flickers while she watches);
//   * the slept-in quilt (GH_Quilt_Asleep, built in chunk A) on the bed where the made one is, switched off (it shows
//     while she's in bed).
// Nothing else in the house changes. Take Grace back out removes all three.
//
// The labs use a test save only (Day 5, a Friday: her usual night; Day 4, a Thursday: her odd one); the playtest save is
// never touched. The checks (GraceAtHomeCheck) report to Logs/Night/grace-at-home-<mode>-<time>/.
// ---------------------------------------------------------------------------
internal static class GraceAtHomeSteps
{
    const string Tag = "[Break-ins 6] ";
    const string Menu = "Fixit Fidget/Night/";
    const string HouseName = "1 - Saffron bay-window house";
    const string ProfilePath = "Assets/Data/Regulars/Regular_Grace.asset";
    const string LookFolder = "Assets/Art/CityNeighbors/Prefabs/";
    const string AsleepQuiltPath = "Assets/Art/Models/GraceHouse/GH_Quilt_Asleep.fbx";
    const string TvLightName = "The TV's light (night)";
    const string AsleepQuiltName = "GH_Quilt_Asleep";
    // Plan metres: the bed (GraceHouseSteps: GH_Bed at X 4.41, Y 3.283, turned -90, on the first floor at 2.40); the TV's
    // light just in front of its screen.
    const float BedX = 4.41f, BedY = 3.283f, Up = 2.40f, BedTurn = -90f;
    static readonly Vector3 TvLightAt = new Vector3(4.80f, 2.83f, .85f);

    [MenuItem(Menu + "Break-ins 6 - Put Grace at home (scene)")]
    static void Put()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName) ?? throw new InvalidOperationException($"'{HouseName}' isn't in the scene.");
            Transform root = house.Find(GraceHouseSteps.RootName) ?? throw new InvalidOperationException("Grace's house isn't built inside yet (Break-ins 1).");
            GraceHouse runner = root.GetComponent<GraceHouse>() ?? throw new InvalidOperationException("Her rooms' root has no GraceHouse.");
            if (root.GetComponent<GraceHouseFinish>() == null)
                throw new InvalidOperationException("Her house hasn't had its lamps and window finished yet (Break-ins 5): the kitchen light and the curtains come from it.");

            // Everything is found and checked before anything changes.
            Light Lamp(string name) =>
                runner.nightLights.FirstOrDefault(l => l != null && l.name == name) ?? throw new InvalidOperationException($"No lamp called '{name}' in her house.");
            Light standard = Lamp("Her standard lamp (night)"), bedside = Lamp("Her bedside lamp (night)");
            Light landing = Lamp("The landing light (night)"), kitchen = Lamp("The kitchen light (night)");
            Transform tv = Find(root, "GH_TV") ?? throw new InvalidOperationException("No GH_TV in her front room.");
            Renderer tvRenderer = tv.GetComponent<Renderer>() ?? throw new InvalidOperationException("Her TV has no renderer.");
            if (!tvRenderer.sharedMaterials.Any(m => m != null && m.name.StartsWith("GH_TV_Screen", StringComparison.Ordinal)))
                report.AppendLine("  (her TV has no GH_TV_Screen material: its light flickers, the screen itself stays dark)");
            Transform armchair = Find(root, "GH_Armchair") ?? throw new InvalidOperationException("No GH_Armchair in her front room.");
            Transform made = Find(root, "GH_Quilt_Made") ?? throw new InvalidOperationException("No GH_Quilt_Made on her bed.");
            Transform west = Find(root, "Bedroom door, west leaf"), east = Find(root, "Bedroom door, east leaf");
            if (west == null || east == null) throw new InvalidOperationException("Her bedroom doors aren't there (Break-ins 1 makes them).");
            Transform curtainsT = house.Find("Front window curtains") ?? throw new InvalidOperationException("Her front window has no 'Front window curtains' (Break-ins 1).");
            Renderer curtains = curtainsT.GetComponent<Renderer>();
            if (curtains == null || curtains.sharedMaterials.Length < 2 || curtains.sharedMaterials.Length % 2 != 0)
                report.AppendLine("  (her front window's curtains aren't split by Break-ins 5: they'll glow all night, as before)");
            CustomerProfile profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(ProfilePath) ?? throw new InvalidOperationException($"No profile at {ProfilePath}.");
            string lookName = profile.StandInLook;
            GameObject look = lookName.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameObject>(LookFolder + lookName + ".prefab") : null;
            if (look == null) report.AppendLine($"  (her look '{lookName}' isn't at {LookFolder}: she'll wear the patron's own outfits; POLYGON City is only on your machine)");
            GameObject quiltAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AsleepQuiltPath) ?? throw new InvalidOperationException($"{AsleepQuiltPath} isn't there (chunk A built it).");
            MeshFilter quiltSource = quiltAsset.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null)
                ?? throw new InvalidOperationException($"{AsleepQuiltPath} has no mesh.");

            Undo.SetCurrentGroupName("Put Grace at home");
            int group = Undo.GetCurrentGroup();
            GraceAtHome home = root.GetComponent<GraceAtHome>();
            bool again = home != null;
            if (home == null) home = Undo.AddComponent<GraceAtHome>(root.gameObject);
            else Undo.RecordObject(home, "Put Grace at home");
            var added = new List<GameObject>(home.addedByStep.Where(g => g != null));

            // The TV's light, in front of its screen: cool, off in the scene (she switches it on with the TV).
            Transform ground = runner.groundFloor != null ? runner.groundFloor : root;
            Transform tvLightT = Find(root, TvLightName);
            Light tvLight;
            if (tvLightT == null)
            {
                var go = new GameObject(TvLightName);
                Undo.RegisterCreatedObjectUndo(go, "Put Grace at home");
                go.transform.SetParent(ground, false);
                go.transform.localPosition = new Vector3(-TvLightAt.x, TvLightAt.z, TvLightAt.y);
                tvLight = go.AddComponent<Light>();
                tvLight.type = LightType.Point;
                tvLight.color = new Color(.55f, .7f, 1f);
                tvLight.intensity = 1.1f;
                tvLight.range = 4f;
                tvLight.shadows = LightShadows.None;
                tvLight.enabled = false;
                added.Add(go);
                report.AppendLine("The TV's light: added in front of the screen (cool, 1.1 over 4 m, no shadows), off in the scene.");
            }
            else
            {
                tvLight = tvLightT.GetComponent<Light>();
                report.AppendLine("The TV's light: already there.");
            }

            // The slept-in quilt, where the made one is, as Break-ins 1 places its pieces.
            Transform asleepT = made.parent != null ? made.parent.Find(AsleepQuiltName) : null;
            GameObject asleep;
            if (asleepT == null)
            {
                asleep = new GameObject(AsleepQuiltName);
                Undo.RegisterCreatedObjectUndo(asleep, "Put Grace at home");
                asleep.transform.SetParent(made.parent, false);
                Matrix4x4 toRoot = quiltAsset.transform.worldToLocalMatrix * quiltSource.transform.localToWorldMatrix;
                Quaternion yaw = Quaternion.Euler(0f, -BedTurn, 0f);
                asleep.transform.localPosition = new Vector3(-BedX, Up, BedY) + yaw * (Vector3)toRoot.GetColumn(3);
                asleep.transform.localRotation = yaw * toRoot.rotation;
                asleep.AddComponent<MeshFilter>().sharedMesh = quiltSource.sharedMesh;
                var r = asleep.AddComponent<MeshRenderer>();
                r.sharedMaterials = quiltSource.GetComponent<Renderer>().sharedMaterials;
                asleep.SetActive(false);
                added.Add(asleep);
                float apart = Vector3.Distance(asleep.transform.position, made.position);
                report.AppendLine($"The slept-in quilt: on the bed where the made one is ({apart:0.000} m between their origins), switched off (on while she's in bed).");
            }
            else
            {
                asleep = asleepT.gameObject;
                report.AppendLine("The slept-in quilt: already there.");
            }

            home.house = runner;
            home.look = look;
            home.standardLamp = standard;
            home.bedsideLamp = bedside;
            home.landingLight = landing;
            home.kitchenLight = kitchen;
            home.tvLight = tvLight;
            home.tv = tvRenderer;
            home.frontCurtains = curtains;
            home.quiltMade = made.gameObject;
            home.quiltAsleep = asleep;
            home.westLeaf = west;
            home.eastLeaf = east;
            home.armchair = armchair;
            home.addedByStep = added.ToArray();
            EditorUtility.SetDirty(home);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            report.AppendLine($"Grace at home: {(again ? "refreshed" : "added")} on her rooms' root. Her look: {(look != null ? look.name : "none")}; " +
                              $"lamps: {standard.name}, {bedside.name}, {landing.name}, {kitchen.name}; the TV {tv.name}; her front window's curtains " +
                              $"({curtains.sharedMaterials.Length} materials); the bedroom doors {west.name} and {east.name} " +
                              $"(open at {west.localEulerAngles.y:0}° and {east.localEulerAngles.y:0}°, shut at {home.westClosedYaw:0}° and {home.eastClosedYaw:0}°); her armchair.");
            Debug.Log(Tag + "Grace is at home at night from now on. Save the scene (Ctrl+S); then Break-ins 7's checks.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting Grace at home FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 6 - Take Grace back out (scene)")]
    static void TakeOut()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            GraceAtHome home = Object.FindAnyObjectByType<GraceAtHome>(FindObjectsInactive.Include);
            if (home == null) { Debug.Log(Tag + "Grace isn't at home in this scene."); return; }
            Undo.SetCurrentGroupName("Take Grace back out");
            int group = Undo.GetCurrentGroup();
            int removed = 0;
            foreach (GameObject go in home.addedByStep)
                if (go != null) { Undo.DestroyObjectImmediate(go); removed++; }
            Undo.DestroyObjectImmediate(home);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(Tag + $"Grace is out again: GraceAtHome and the {removed} thing(s) Break-ins 6 added are gone; her lamps light all night as before. Save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking Grace back out FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 6 - Put Grace at home (scene)", true)]
    [MenuItem(Menu + "Break-ins 6 - Take Grace back out (scene)", true)]
    [MenuItem(Menu + "Break-ins 7 - Grace at home: her night and her eyes and ears (lab, a check)", true)]
    [MenuItem(Menu + "Break-ins 7 - Grace at home: her Thursday (lab, a check)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the labs

    [MenuItem(Menu + "Break-ins 7 - Grace at home: her night and her eyes and ears (lab, a check)")]
    static void CheckHerNight() => StartCheck(GraceAtHomeCheck.Mode.Night, 5);

    [MenuItem(Menu + "Break-ins 7 - Grace at home: her Thursday (lab, a check)")]
    static void CheckHerThursday() => StartCheck(GraceAtHomeCheck.Mode.Thursday, 4);

    // As Break-ins 3's lab (Ace on the pavement at her door at 11 PM, the break-ins on), on a test save whose day picks her
    // night: Day 5 is a Friday (her usual night), Day 4 a Thursday.
    static void StartCheck(GraceAtHomeCheck.Mode mode, int day)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
            if (Object.FindAnyObjectByType<GraceAtHome>(FindObjectsInactive.Include) == null)
                throw new InvalidOperationException("Grace isn't at home in this scene yet: Break-ins 6 - Put Grace at home, then save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        var facts = new List<NotebookFactData>();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName)) { fact.day = 1; facts.Add(fact); }
        SaveCheckpointStorage.Write(path, new SaveData
        {
            day = day, money = 600, cups = 40, beans = 40, dayCompleted = false, notebook = facts.ToArray(),
            night = new NightSaveData { nights = 1, trophies = new[] { NightThings.GraceGnome } },
        });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.SetInt(GraceHouse.LabKey, 1);
        PlayerPrefs.SetInt(GraceAtHomeCheck.PendingKey, (int)mode);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Grace at home, a check ({(mode == GraceAtHomeCheck.Mode.Thursday ? "her Thursday" : "her night and her eyes and ears")}): " +
                  $"a lab session on a test save (Day {day}, {Weekdays.Name(day)}; {path}); your playtest save is not used. Leave the mouse and " +
                  "keyboard alone for a few minutes; the photos and the report go to Logs/Night/grace-at-home-<mode>-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(GraceAtHomeCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(GraceAtHomeCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }

    static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = Find(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
#endif
