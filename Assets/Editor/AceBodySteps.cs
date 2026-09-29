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

// ACE'S STAND-IN BODY (29 Sept 2026: claude/break-ins-spec.md section 8, calls 1, 6 and d)
//
//   Fixit Fidget > Night > Ace's body 1 - Put on Ace's stand-in body (look 11, the green jacket)
//   Fixit Fidget > Night > Ace's body 1 - Take off Ace's stand-in body
//   Fixit Fidget > Night > Ace's body 2 - Try it in the café (lab, Day 5, you serve)
//   Fixit Fidget > Night > Ace's body 3 - Who wears Ace's look (Play Mode, a check)
//
// Putting it on gives Ace (the player object) an AceBody (see AceBody.cs for what it does) and fills
// it in: the look (Character_Male_Jacket from the walk-ins' looks), the café rig (Quaternius Beach,
// at the café people's own size), its idle, walk and run, and each clip's natural speed and stride
// phase, measured here the same way as the café people's walks (the stance foot's backward speed).
// It first proves the look copies the rig, and photographs it standing, walking and running.
// The only change to the scene is that component on Ace; taking it off removes it (both undo).
// At night the body is on in every night (the Night 1 slice and the labs alike); by day only in the
// café lab above. Report and photos: Logs/Night/ace-body-<time>/.
internal static class AceBodySteps
{
    const string Menu = "Fixit Fidget/Night/";
    const string Tag = "[Ace's body] ";
    const string Look = "Character_Male_Jacket";   // look 11, the green jacket (call d)
    const string RigModel = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    const string CafePerson = "Assets/AssetsPrefabs/Customer.prefab";
    const string IdleName = "CharacterArmature|Idle", WalkName = "CharacterArmature|Walk", RunName = "CharacterArmature|Run";

    [MenuItem(Menu + "Ace's body 1 - Put on Ace's stand-in body (look 11, the green jacket)")]
    static void PutOn()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = FindAce();
            GameObject look = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(Look));
            if (look == null)
                throw new InvalidOperationException($"{PolygonNpcSetup.LookPath(Look)} isn't there. The POLYGON looks are built by " +
                                                    "Fixit Fidget > City pack > NPC looks 1 (they need the purchased Synty art); nothing was changed.");
            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigModel);
            if (rig == null) throw new InvalidOperationException(RigModel + " isn't there; nothing was changed.");
            Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(RigModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            foreach (string name in new[] { IdleName, WalkName, RunName })
                if (!clips.ContainsKey(name)) throw new InvalidOperationException($"{RigModel} has no clip '{name}'; nothing was changed.");
            float scale = CafePeopleScale();

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "ace-body-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            report.AppendLine("Ace's stand-in body, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            report.AppendLine($"Look: {look.name} (look 11, the green jacket). Rig: {RigModel} at {scale:0.00}, the café people's size.");

            // Each clip's natural speed and where its left foot is furthest forward (so walk and run keep step).
            Gait walk = MeasureGait(clips[WalkName], rig), run = MeasureGait(clips[RunName], rig);
            if (walk.speed <= .2f || run.speed <= .2f)
                throw new InvalidOperationException($"Couldn't measure the clips' speeds (walk: {walk.how}; run: {run.how}); nothing was changed.");
            report.AppendLine($"Walk: {walk.how}. At {scale:0.00}: {walk.speed * scale:0.00} m/s.");
            report.AppendLine($"Run: {run.how}. At {scale:0.00}: {run.speed * scale:0.00} m/s, so at Ace's 5 m/s it plays at {5f / (run.speed * scale):0.00}x.");

            // The look must copy the rig; measure it standing.
            float height = ProveLook(rig, look, clips[IdleName], scale, out float width, out string bind);
            report.AppendLine(bind);
            report.AppendLine($"Standing (idle): {height:0.00} m tall, {width:0.00} m across. The capsule Unity moves is " +
                              $"{2f * (ace.GetComponent<CharacterController>().radius + ace.GetComponent<CharacterController>().skinWidth):0.00} m wide, " +
                              $"{ace.GetComponent<CharacterController>().height:0.00} m tall.");

            // Photos: the café rig as it is, and look 11, standing, walking and running.
            string sheet = Path.Combine(folder, "look-11-idle-walk-run.png");
            string photoNotes = PolygonNpcSetup.Lineup(sheet, new[] { Look }, new[]
            {
                (IdleName, .35f), (WalkName, .15f), (WalkName, .65f), (RunName, 0f), (RunName, .25f), (RunName, .5f), (RunName, .75f),
            }, 300, 420, 5.2f);

            AceBody body = ace.GetComponent<AceBody>();
            bool added = body == null;
            if (added) body = Undo.AddComponent<AceBody>(ace.gameObject);
            else Undo.RecordObject(body, "Put on Ace's stand-in body");
            body.look = look;
            body.rig = rig;
            body.rigScale = scale;
            body.idleClip = clips[IdleName];
            body.walkClip = clips[WalkName];
            body.runClip = clips[RunName];
            body.walkClipSpeed = walk.speed;
            body.runClipSpeed = run.speed;
            body.walkLeftForward = walk.leftForward;
            body.runLeftForward = run.leftForward;
            body.standingHeight = height;
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);

            report.AppendLine();
            report.AppendLine((added ? "Added" : "Updated") + $" AceBody on '{ace.name}'. Save the scene to keep it (Ctrl+S).");
            report.AppendLine("At night Ace wears it in every night; by day only in 'Ace's body 2 - Try it in the café (lab)'. " +
                              "Look 11 leaves the walk-ins' pool while Ace has it (CustomerProfile.ReserveStandInLook).");
            report.AppendLine();
            report.AppendLine("Photo: " + sheet);
            report.AppendLine(photoNotes);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(Tag + $"Ace's stand-in body is on: {look.name}, {height:0.00} m tall; walk {walk.speed * scale:0.00} m/s, " +
                      $"run {run.speed * scale:0.00} m/s. Save the scene to keep it. {folder}\n{report}");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Put on FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Ace's body 1 - Take off Ace's stand-in body")]
    static void TakeOff()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = FindAce();
            AceBody body = ace.GetComponent<AceBody>();
            if (body == null) { Debug.Log(Tag + "Ace has no stand-in body; nothing to take off."); return; }
            Scene scene = ace.gameObject.scene;
            Undo.DestroyObjectImmediate(body);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(Tag + "Ace's stand-in body is off: Ace is the capsule again. Save the scene to keep it.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Take off FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Ace's body 2 - Try it in the café (lab, Day 5, you serve)")]
    static void CafeLab()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = FindAce();
            if (ace.GetComponent<AceBody>() == null)
                throw new InvalidOperationException("Ace has no stand-in body yet (Ace's body 1 - Put on Ace's stand-in body).");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        // The café lab's own test save (the playtest save is never touched), Day 5, and the body by day for this session.
        string path = Path.Combine(Application.persistentDataPath, global::CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(global::CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(global::CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(AceBody.LabKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Café lab: Day 5, you serve, and Ace wears the stand-in body by day for this session. " +
                  "The test save is " + path + "; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // In any Play session: Ace's look is kept out of the walk-ins' pool, and no other city body wears it.
    [MenuItem(Menu + "Ace's body 3 - Who wears Ace's look (Play Mode, a check)")]
    static void WhoWears()
    {
        AceBody ace = Object.FindAnyObjectByType<AceBody>();
        if (ace == null || ace.look == null) { Debug.LogWarning(Tag + "Ace has no stand-in body."); return; }
        int bodies = 0;
        var wearing = new List<string>();
        foreach (PolygonNpcVisual v in Object.FindObjectsByType<PolygonNpcVisual>(FindObjectsInactive.Include))
        {
            if (v == ace.Visual || v.ActiveAppearance < 0) continue;
            bodies++;
            if (v.ActiveAppearanceName == ace.look.name) wearing.Add(v.name);
        }
        bool kept = CustomerProfile.IsStandInLook(ace.look.name);
        string line = $"{ace.look.name}: kept out of the pool: {(kept ? "yes" : "NO")}; Ace {(ace.Worn ? "wears it now" : "doesn't wear it right now (by day outside the café lab)")}; " +
                      $"{wearing.Count} of the {bodies} other city bodies wear it" + (wearing.Count > 0 ? ": " + string.Join(", ", wearing) : ".");
        // A warning, not an error, if something is wrong: the Console's Error Pause would stop Play Mode.
        if (kept && wearing.Count == 0) Debug.Log(Tag + "Who wears Ace's look: all clear. " + line);
        else Debug.LogWarning(Tag + "Who wears Ace's look: PROBLEM. " + line);
    }

    [MenuItem(Menu + "Ace's body 3 - Who wears Ace's look (Play Mode, a check)", true)]
    static bool Playing() => EditorApplication.isPlaying;

    [MenuItem(Menu + "Ace's body 1 - Put on Ace's stand-in body (look 11, the green jacket)", true)]
    [MenuItem(Menu + "Ace's body 1 - Take off Ace's stand-in body", true)]
    [MenuItem(Menu + "Ace's body 2 - Try it in the café (lab, Day 5, you serve)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A request that never became a Play session must not put the body on in the next ordinary Play.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(AceBody.LabKey))
        {
            PlayerPrefs.DeleteKey(AceBody.LabKey);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------ helpers

    static PlayerMovement FindAce()
    {
        PlayerMovement[] found = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include);
        if (found.Length != 1) throw new InvalidOperationException($"Expected Ace (one PlayerMovement) in the scene, found {found.Length}.");
        if (found[0].GetComponent<CafeViewMode>() == null || found[0].GetComponent<CharacterController>() == null)
            throw new InvalidOperationException($"'{found[0].name}' has no CafeViewMode or CharacterController.");
        return found[0];
    }

    // The café people are the rig at their prefab's root scale (1.1).
    static float CafePeopleScale()
    {
        GameObject person = AssetDatabase.LoadAssetAtPath<GameObject>(CafePerson);
        float s = person != null ? person.transform.localScale.y : 1.1f;
        return s > .1f ? s : 1.1f;
    }

    struct Gait { public float speed, leftForward, hipsDrift; public string how; }

    // The clip is in place, so while a foot is on the ground it slides back at the speed the clip was made for:
    // the median backward speed of a low foot (the same method as the café people's walks, NpcGaitAnimations).
    // Also: when in the cycle the left foot is furthest forward, and whether the hips drift over a cycle.
    static Gait MeasureGait(AnimationClip clip, GameObject rigAsset)
    {
        GameObject actor = Object.Instantiate(rigAsset);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            foreach (Animator a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            Transform[] all = actor.GetComponentsInChildren<Transform>(true);
            Transform left = all.FirstOrDefault(t => t.name == "Foot.L"), right = all.FirstOrDefault(t => t.name == "Foot.R");
            Transform hips = all.FirstOrDefault(t => t.name == "Hips");
            if (left == null || right == null || hips == null) return new Gait { how = "the rig has no Foot.L, Foot.R or Hips" };
            int n = Mathf.Max(24, Mathf.RoundToInt(clip.length * 60f));
            var z = new float[2, n + 1];
            var y = new float[2, n + 1];
            Vector3 hipsStart = default, hipsEnd = default;
            for (int i = 0; i <= n; i++)
            {
                clip.SampleAnimation(actor, clip.length * i / n);
                for (int f = 0; f < 2; f++)
                {
                    Vector3 p = actor.transform.InverseTransformPoint((f == 0 ? left : right).position);
                    z[f, i] = p.z;
                    y[f, i] = p.y;
                }
                Vector3 h = actor.transform.InverseTransformPoint(hips.position);
                if (i == 0) hipsStart = h;
                if (i == n) hipsEnd = h;
            }
            var speeds = new List<float>();
            float dt = clip.length / n;
            for (int f = 0; f < 2; f++)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i <= n; i++) { minY = Mathf.Min(minY, y[f, i]); maxY = Mathf.Max(maxY, y[f, i]); }
                float low = minY + (maxY - minY) * .25f;
                for (int i = 1; i <= n; i++)
                {
                    if (y[f, i] > low || y[f, i - 1] > low) continue;
                    float v = (z[f, i - 1] - z[f, i]) / dt;
                    if (v > .2f) speeds.Add(v);
                }
            }
            int forward = 0;
            for (int i = 1; i < n; i++) if (z[0, i] > z[0, forward]) forward = i;
            float drift = new Vector2(hipsEnd.x - hipsStart.x, hipsEnd.z - hipsStart.z).magnitude;
            if (speeds.Count < 4) return new Gait { how = $"{clip.name}: too few stance samples" };
            speeds.Sort();
            float median = speeds[speeds.Count / 2];
            return new Gait
            {
                speed = median,
                leftForward = (float)forward / n,
                hipsDrift = drift,
                how = $"'{clip.name}' {clip.length:0.00} s, stance foot {median:0.00} m/s over {speeds.Count} samples (rig at scale 1), " +
                      $"left foot furthest forward at {(float)forward / n:0.00} of the cycle, hips drift {drift * 100f:0.0} cm over a cycle",
            };
        }
        finally { Object.DestroyImmediate(actor); }
    }

    // In a preview scene: the rig at the café people's size with the look copying it, standing (idle).
    static float ProveLook(GameObject rig, GameObject look, AnimationClip idle, float scale, out float width, out string bind)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject actor = (GameObject)PrefabUtility.InstantiatePrefab(rig, preview);
        PolygonNpcVisual visual = null;
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one * scale;
            foreach (Animator a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            visual = actor.AddComponent<PolygonNpcVisual>();
            visual.Configure(new[] { look }, 0f, 0, 0f);
            if (!visual.ApplyAppearance(0))
                throw new InvalidOperationException($"{look.name} couldn't copy the café rig's skeleton; nothing was changed.");
            idle.SampleAnimation(actor, idle.length * .35f);
            visual.Follow();
            bool any = false;
            Bounds b = default;
            foreach (SkinnedMeshRenderer skin in visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh posed = PolygonNpcSetup.SkinToWorld(skin);
                try
                {
                    if (posed.vertexCount == 0) continue;
                    if (!any) { b = posed.bounds; any = true; }
                    else b.Encapsulate(posed.bounds);
                }
                finally { Object.DestroyImmediate(posed); }
            }
            width = any ? Mathf.Max(b.size.x, b.size.z) : 0f;
            bind = $"The look copies the rig: {visual.FingerCount} finger bones as well as the body's 20.";
            return any ? b.size.y : 0f;
        }
        finally
        {
            if (visual != null) visual.RemoveAppearance();
            Object.DestroyImmediate(actor);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
#endif
