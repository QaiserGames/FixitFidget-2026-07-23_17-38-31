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
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ACE'S SIDEKICK BODY (29 Sept 2026: claude/break-ins-spec.md section 8, the Sidekick Ace)
//
//   Fixit Fidget > Night > Ace's body 4 - Put on Ace's Sidekick body (from the Character Creator)
//   Fixit Fidget > Night > Ace's body 4 - Take off the Sidekick body (back to the stand-in)
//   Fixit Fidget > Night > Ace's body 5 - Sneaking: fit the crouch clips to Ace (scene)   (break-ins chunk B, 30 Sept)
//
// Putting it on fills in Ace's AceBody (see AceBody.cs) with Ace's own character: the prefab the Sidekick
// Character Creator exported as "Ace" (Assets/Synty/SidekickCharacters/Characters/Ace, git-ignored), and
// the free animation library's idle, walk and run as Humanoid clips, so they fit that body. For those it
// makes a copy of the library imported as Humanoid (Assets/ThirdParty/Quaternius_UAL/Humanoid, git-ignored:
// a 20 MB file anyone can remake with this step); the library's own import, which the café's sit clips
// come from, is not touched. The copy keeps only what Ace uses: idle, walk, jog and sprint, and the crouch
// for later (chunk B), looped and in place.
//
// Then it measures, in a preview scene (never in the café scene): how tall Ace stands as exported, each
// clip's natural speed and stride phase on this body (the stance foot's backward speed, the same method as
// the stand-in's), and which run suits Ace's own speed best. It sizes Ace to the café people's height
// (the stand-in's, look 11 on the café rig), and photographs Ace moving, Ace beside a café person, and the
// face (open, blinking, glancing). Before measuring, both steps straighten the clips on Ace (Straighten, 6 Oct 2026): each
// one's import Offset is set so it goes, or stands, straight along Ace's forward. The stand-in's settings stay, as the fallback where the Sidekick files
// are missing. The only change to the scene is AceBody's fields (undo). Report and photos:
// Logs/Night/ace-sidekick-<time>/.
internal static class AceSidekickSteps
{
    const string Menu = "Fixit Fidget/Night/";
    const string Tag = "[Ace's body] ";
    const string PutOnName = "Ace's body 4 - Put on Ace's Sidekick body (from the Character Creator)";
    const string TakeOffName = "Ace's body 4 - Take off the Sidekick body (back to the stand-in)";
    internal const string SidekickPrefab = "Assets/Synty/SidekickCharacters/Characters/Ace/Ace.prefab";
    internal const string LibrarySource = "Assets/ThirdParty/Quaternius_UAL/AnimationLibrary_Unity_Standard.fbx";
    internal const string LibraryFolder = "Assets/ThirdParty/Quaternius_UAL/Humanoid";
    internal const string LibraryHumanoid = LibraryFolder + "/AnimationLibrary_Humanoid.fbx";
    const string IdleTake = "Idle_Loop", WalkTake = "Walk_Loop";
    const string CrouchIdleTake = "Crouch_Idle_Loop", CrouchWalkTake = "Crouch_Fwd_Loop";
    const string CrouchName = "Ace's body 5 - Sneaking: fit the crouch clips to Ace (scene)";
    static readonly string[] RunTakes = { "Jog_Fwd_Loop", "Sprint_Loop" };
    // What the copy keeps: idle, walk, the two runs (one is picked), and the crouch for chunk B.
    static readonly string[] Takes = { IdleTake, WalkTake, "Jog_Fwd_Loop", "Sprint_Loop", "Crouch_Idle_Loop", "Crouch_Fwd_Loop" };
    // The café people's height if it can't be measured (look 11 on the café rig at 1.1, measured on 29 Sept).
    const float CafePeopleFallback = 2.04f;

    [MenuItem(Menu + PutOnName)]
    static void PutOn()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = AceBodySteps.FindAce();
            AceBody body = ace.GetComponent<AceBody>();
            if (body == null)
                throw new InvalidOperationException("Ace has no AceBody yet. Put on the stand-in first (Ace's body 1): it stays as the fallback. Nothing was changed.");
            GameObject sidekick = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickPrefab);
            if (sidekick == null)
                throw new InvalidOperationException(SidekickPrefab + " isn't there. Export Ace from the Sidekick Character Creator first " +
                                                    "(Export Character as FBX, saved as Ace in Assets/Synty/SidekickCharacters/Characters). Nothing was changed.");
            Animator prefabAnimator = sidekick.GetComponentInChildren<Animator>(true);
            Avatar avatar = prefabAnimator != null ? prefabAnimator.avatar : null;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException($"{sidekick.name} has no working Humanoid avatar " +
                                                    $"({(avatar == null ? "none" : !avatar.isValid ? "not valid" : "not Humanoid")}). Nothing was changed.");

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "ace-sidekick-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            failedReport = report;
            failedFolder = folder;
            report.AppendLine("Ace's Sidekick body, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            report.AppendLine($"Body: {SidekickPrefab}. Its avatar '{avatar.name}' is valid and Humanoid: {Mapped(avatar)}.");

            // The animation library as Humanoid: a copy, so the café's own import stays as it is.
            Dictionary<string, AnimationClip> clips = HumanoidLibrary(report);

            // Measure, then photograph, in a preview scene.
            float aceSpeed = AceSpeed(ace);
            Measured m;
            string photos;
            using (var studio = new Studio())
            {
                clips = Straighten(studio, sidekick, body.sidekickFootIK, clips, report);
                m = Measure(studio, sidekick, clips, body.sidekickFootIK, aceSpeed, folder, report);
                photos = Photos(studio, sidekick, clips, m, body.sidekickFootIK, folder);
            }

            Undo.RecordObject(body, "Put on Ace's Sidekick body");
            body.sidekick = sidekick;
            body.sidekickIdle = clips[IdleTake];
            body.sidekickWalk = clips[WalkTake];
            body.sidekickRun = clips[m.runTake];
            body.sidekickScale = m.scale;
            body.sidekickWalkSpeed = m.walk.speed;
            body.sidekickRunSpeed = m.run.speed;
            body.sidekickWalkLeftForward = m.walk.leftForward;
            body.sidekickRunLeftForward = m.run.leftForward;
            body.sidekickHeight = m.natural * m.scale;
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);

            report.AppendLine();
            report.AppendLine($"Set AceBody on '{ace.name}': Ace wears the Sidekick body by day and night (By Day is {(body.byDay ? "on" : "off")}), " +
                              $"at {m.scale:0.000}x, {m.natural * m.scale:0.00} m tall; clips {IdleTake}, {WalkTake}, {m.runTake}; foot IK {(body.sidekickFootIK ? "on" : "off")}; " +
                              $"the face {(body.sidekickFace ? "lives (AceFace)" : "is still (Sidekick Face is off)")}. Save the scene to keep it (Ctrl+S).");
            report.AppendLine($"The stand-in ({(body.look != null ? body.look.name : "none")}) stays set as the fallback; while the Sidekick body is on, " +
                              "that look is back in the walk-ins' pool.");
            report.AppendLine();
            report.AppendLine(photos);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(Tag + $"Ace's Sidekick body is on: {m.natural * m.scale:0.00} m tall ({m.scale:0.000}x), walk {m.walk.speed * m.scale:0.00} m/s, " +
                      $"{m.runTake} {m.run.speed * m.scale:0.00} m/s. Save the scene to keep it. {folder}\n{report}");
        }
        catch (Exception e)
        {
            if (failedReport != null && failedFolder != null)
            {
                failedReport.AppendLine().AppendLine("FAILED: " + e.Message);
                File.WriteAllText(Path.Combine(failedFolder, "report.txt"), failedReport.ToString());
            }
            Debug.LogError(Tag + "Put on the Sidekick body FAILED: " + e.Message + (failedFolder != null ? " Report so far: " + failedFolder : "") + "\n" + e);
        }
        finally { failedReport = null; failedFolder = null; }
    }

    // The report so far, written out if the step fails part way (the numbers help find out why).
    static StringBuilder failedReport;
    static string failedFolder;

    [MenuItem(Menu + TakeOffName)]
    static void TakeOff()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = AceBodySteps.FindAce();
            AceBody body = ace.GetComponent<AceBody>();
            if (body == null || body.sidekick == null) { Debug.Log(Tag + "Ace has no Sidekick body; nothing to take off."); return; }
            Undo.RecordObject(body, "Take off Ace's Sidekick body");
            body.sidekick = null;
            body.sidekickIdle = body.sidekickWalk = body.sidekickRun = null;
            body.sidekickCrouchIdle = body.sidekickCrouchWalk = null;
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);
            Debug.Log(Tag + "The Sidekick body is off: Ace wears the stand-in (" + body.LookName + ") again. " +
                      "Ace's body 4 puts it back. Save the scene to keep it.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Take off the Sidekick body FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + PutOnName, true)]
    [MenuItem(Menu + TakeOffName, true)]
    [MenuItem(Menu + CrouchName, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ------------------------------------------------------------------ sneaking: the crouch (break-ins chunk B)

    // Ace's body 5: the library's two crouch loops on Ace's Sidekick body (they are already in the Humanoid copy).
    // Measures the crouch walk the way the walk and the run were measured (the stance foot's backward speed and
    // when the left foot is furthest forward), and how tall Ace is crouched; photographs Ace standing, crouched
    // and crouch walking; and sets AceBody's crouch fields (undo). Save the scene to keep them.
    [MenuItem(Menu + CrouchName)]
    static void FitCrouchMenu()
    {
        string folder = null;
        var report = new StringBuilder();
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = AceBodySteps.FindAce();
            AceBody body = ace.GetComponent<AceBody>();
            if (body == null || body.sidekick == null)
                throw new InvalidOperationException("Ace has no Sidekick body yet (Ace's body 4 puts it on). Nothing was changed.");
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "ace-sneak-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            report.AppendLine("Ace sneaking: the crouch clips on Ace's Sidekick body, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Dictionary<string, AnimationClip> clips = HumanoidLibrary(report);
            float scale = body.sidekickScale > .01f ? body.sidekickScale : 1f;
            Crouched c;
            using (var studio = new Studio())
            {
                clips = Straighten(studio, body.sidekick, body.sidekickFootIK, clips, report);
                c = FitCrouch(studio, body.sidekick, clips, scale, body.sidekickFootIK, folder, report);
            }

            Undo.RecordObject(body, "Fit Ace's crouch clips");
            body.sidekickCrouchIdle = clips[CrouchIdleTake];
            body.sidekickCrouchWalk = clips[CrouchWalkTake];
            body.sidekickCrouchSpeed = c.walk.speed;
            body.sidekickCrouchLeftForward = c.walk.leftForward;
            body.sidekickCrouchHeight = c.crouched * scale;
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);

            float sneak = SneakSpeed(ace);
            report.AppendLine();
            report.AppendLine($"Set AceBody on '{ace.name}': crouch clips {CrouchIdleTake} and {CrouchWalkTake}; the crouch walk's natural speed " +
                              $"{c.walk.speed:0.00} m/s at scale 1, {c.walk.speed * scale:0.00} m/s on Ace ({scale:0.000}x), so at Ace's sneaking " +
                              $"{sneak:0.0} m/s it plays at {sneak / Mathf.Max(.01f, c.walk.speed * scale):0.00}x; Ace crouched is {c.crouched * scale:0.00} m tall " +
                              $"(standing {c.standing * scale:0.00} m). Save the scene to keep it (Ctrl+S).");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(Tag + $"Ace can crouch: the crouch walk plays at {sneak / Mathf.Max(.01f, c.walk.speed * scale):0.00}x at {sneak:0.0} m/s; " +
                      $"{c.crouched * scale:0.00} m tall crouched. Save the scene to keep it. {folder}\n{report}");
        }
        catch (Exception e)
        {
            if (folder != null)
            {
                report.AppendLine().AppendLine("FAILED: " + e.Message);
                File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            }
            Debug.LogError(Tag + "Fit the crouch clips FAILED: " + e.Message + (folder != null ? " Report so far: " + folder : "") + "\n" + e);
        }
    }

    struct Crouched { public Gait walk; public float standing, crouched; }

    static Crouched FitCrouch(Studio studio, GameObject sidekick, Dictionary<string, AnimationClip> clips, float scale, bool footIK,
        string folder, StringBuilder report)
    {
        foreach (string take in new[] { CrouchIdleTake, CrouchWalkTake })
            if (!clips.ContainsKey(take)) throw new InvalidOperationException($"{LibraryHumanoid} has no clip '{take}'. AceBody wasn't changed.");
        var c = new Crouched();
        GameObject actor = studio.Add(sidekick);
        try
        {
            using var poser = new Poser(actor, footIK);
            // Heights at scale 1 (the posed bounds are in the world, and the actor is at 1 here).
            poser.Pose(clips[IdleTake], clips[IdleTake].length * .35f);
            c.standing = PosedBounds(actor).size.y;
            poser.Pose(clips[CrouchIdleTake], clips[CrouchIdleTake].length * .35f);
            c.crouched = PosedBounds(actor).size.y;
            c.walk = HumanGait(poser, clips[CrouchWalkTake], Path.Combine(folder, "ace-" + CrouchWalkTake + ".csv"));
            report.AppendLine();
            report.AppendLine("The crouch on Ace (Humanoid, scale 1):");
            report.AppendLine($"  standing {c.standing:0.00} m, crouched ({CrouchIdleTake}) {c.crouched:0.00} m, {c.crouched / Mathf.Max(.01f, c.standing) * 100f:0}% of standing");
            report.AppendLine("  " + c.walk.how);
            if (c.walk.facing < 0)
                throw new InvalidOperationException($"On Ace the crouch walk faces -Z, backwards for AceBody: see the import settings of {LibraryHumanoid}. AceBody wasn't changed.");
            if (c.walk.speed <= .1f)
                throw new InvalidOperationException($"Couldn't measure the crouch walk's speed ({c.walk.how}). AceBody wasn't changed.");
            if (c.crouched < .4f || c.crouched > c.standing * .95f)
                throw new InvalidOperationException($"The crouch doesn't look like a crouch ({c.crouched:0.00} m against {c.standing:0.00} m standing). AceBody wasn't changed.");

            // Photos at Ace's real size: standing, crouched (front, three-quarters, side), and the crouch walk, four moments.
            // The face holds the jaw shut, as AceFace does in play (the clips have no jaw).
            actor.transform.localScale = Vector3.one * scale;
            AceFace face = actor.AddComponent<AceFace>();
            bool faceBound = face.Bind(actor.transform, poser.Animator);
            void Hold() { if (faceBound) face.Apply(0f, Vector2.zero); }
            const int W = 300, H = 420;
            var sheet = new Sheet(4, 2, W, H);
            Vector3 middle = new Vector3(0f, 1.05f, 0f);
            poser.Pose(clips[IdleTake], clips[IdleTake].length * .35f);
            Hold();
            sheet.Put(0, 0, studio.Shot(middle, 25f, 8f, 5.2f, 24f, W, H));
            AnimationClip still = clips[CrouchIdleTake], walk = clips[CrouchWalkTake];
            (float yaw, float at)[] crouchedShots = { (25f, .35f), (60f, .35f), (90f, .35f) };
            for (int i = 0; i < crouchedShots.Length; i++)
            {
                poser.Pose(still, still.length * crouchedShots[i].at);
                Hold();
                sheet.Put(i + 1, 0, studio.Shot(middle, crouchedShots[i].yaw, 8f, 5.2f, 24f, W, H));
            }
            for (int i = 0; i < 4; i++)
            {
                poser.Pose(walk, Mathf.Repeat(c.walk.leftForward + i * .25f, 1f) * walk.length);
                Hold();
                sheet.Put(i, 1, studio.Shot(middle, 60f, 8f, 5.2f, 24f, W, H));
            }
            string photo = Path.Combine(folder, "1-crouch.png");
            sheet.Save(photo);
            report.AppendLine();
            report.AppendLine("Photo: " + photo);
            report.AppendLine($"  Top: Ace standing ({IdleTake}); crouched ({CrouchIdleTake}) from the front-left, three-quarters and the side. " +
                              $"Bottom: the crouch walk ({CrouchWalkTake}), four moments a quarter of a stride apart.");
            return c;
        }
        finally { studio.Remove(actor); }
    }

    static float SneakSpeed(PlayerMovement ace)
    {
        SerializedProperty speed = new SerializedObject(ace).FindProperty("sneakSpeed");
        return speed != null && speed.floatValue > .1f ? speed.floatValue : 1.6f;
    }

    // ------------------------------------------------------------------ the Humanoid copy of the library

    // offsets: each take's Root Transform Rotation Offset, degrees (Straighten works them out). Null keeps the ones the copy has.
    static Dictionary<string, AnimationClip> HumanoidLibrary(StringBuilder report, IReadOnlyDictionary<string, float> offsets = null)
    {
        if (AssetDatabase.LoadMainAssetAtPath(LibrarySource) == null)
            throw new InvalidOperationException(LibrarySource + " isn't there (the free animation library, kept in the repository). Nothing was changed.");
        bool made = false;
        if (AssetDatabase.LoadMainAssetAtPath(LibraryHumanoid) == null)
        {
            PolygonNpcSetup.EnsureFolder(LibraryFolder);
            if (!AssetDatabase.CopyAsset(LibrarySource, LibraryHumanoid))
                throw new InvalidOperationException($"Couldn't copy {LibrarySource} to {LibraryHumanoid}. Nothing else was changed.");
            made = true;
        }
        var importer = AssetImporter.GetAtPath(LibraryHumanoid) as ModelImporter;
        if (importer == null) throw new InvalidOperationException(LibraryHumanoid + " isn't a model Unity can import.");

        // Humanoid, with its own avatar (the library's T-pose), no materials.
        bool setUp = false;
        if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; setUp = true; }
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; setUp = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; setUp = true; }
        if (!importer.importAnimation) { importer.importAnimation = true; setUp = true; }
        if (setUp)
        {
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(LibraryHumanoid);
        }

        // Only the clips Ace uses; looped; in place, with the body's sway, bob and turn kept in the pose
        // (Ace's capsule does the moving, and AceBody plays each clip at the rate that fits).
        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        Dictionary<string, float> kept = CurrentOffsets(importer);
        var wanted = new List<ModelImporterClipAnimation>();
        foreach (string take in Takes)
        {
            ModelImporterClipAnimation clip = defaults.FirstOrDefault(c => c.takeName == take || c.takeName.EndsWith("|" + take, StringComparison.Ordinal));
            if (clip == null)
                throw new InvalidOperationException($"{LibrarySource} has no take '{take}' (takes: {string.Join(", ", defaults.Select(c => c.takeName))}).");
            clip.name = take;
            clip.loopTime = true;
            clip.loopPose = false;
            clip.cycleOffset = 0f;
            clip.mirror = false;
            // Facing: the library's body faces -Z as made (backwards, for Unity); "Body Orientation" turns the root to
            // the body's own forward, so on Ace the clips face +Z, the way AceBody turns the body toward where Ace goes.
            // That forward is the body's at the clip's first frame, so a loop that starts mid-twist comes out turned:
            // each clip's Offset turns it straight again (Straighten; 6 Oct 2026, the jog ran 28° off).
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = false;
            clip.rotationOffset = offsets != null && offsets.TryGetValue(take, out float given) ? given
                                : kept.TryGetValue(take, out float had) ? had : 0f;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.heightOffset = 0f;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            wanted.Add(clip);
        }
        bool clipsChanged = Signature(importer.clipAnimations) != Signature(wanted);
        if (clipsChanged)
        {
            importer.clipAnimations = wanted.ToArray();
            importer.SaveAndReimport();
        }

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(LibraryHumanoid);
        Avatar avatar = all.OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException($"{LibraryHumanoid}'s Humanoid avatar didn't come out right " +
                                                $"({(avatar == null ? "none" : !avatar.isValid ? "not valid" : "not Humanoid")}); see its import settings (Rig tab). " +
                                                "AceBody wasn't changed.");
        Dictionary<string, AnimationClip> clips = all.OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
        foreach (string take in Takes)
        {
            if (!clips.TryGetValue(take, out AnimationClip clip)) throw new InvalidOperationException($"{LibraryHumanoid} has no clip '{take}' after its import.");
            if (!clip.humanMotion) throw new InvalidOperationException($"'{take}' in {LibraryHumanoid} didn't import as a Humanoid clip.");
        }
        report.AppendLine($"Clips: {LibraryHumanoid} ({(made ? "copied now from" : "the copy of")} {LibrarySource}; " +
                          $"{(setUp || clipsChanged ? "set to Humanoid and re-imported" : "already set up")}). Its avatar '{avatar.name}' is valid and Humanoid: {Mapped(avatar)}.");
        report.AppendLine("  Kept: " + string.Join(", ", Takes.Select(t => $"{t} {clips[t].length:0.00} s")) + ". All looped, in place (root turn, height and position " +
                          "baked into the pose), and facing the body's own forward (the library's body faces -Z as made).");
        return clips;
    }

    static string Signature(IEnumerable<ModelImporterClipAnimation> clips) => string.Join("|", clips.Select(c =>
        string.Format(CultureInfo.InvariantCulture, "{0};{1};{2};{3};{4}{5}{6}{7}{8}{9}{10}{11}{12}{13};{14:0.###}", c.name, c.takeName, c.firstFrame, c.lastFrame,
            c.loopTime, c.loopPose, c.mirror, c.lockRootRotation, c.keepOriginalOrientation, c.lockRootHeightY, c.keepOriginalPositionY,
            c.heightFromFeet, c.lockRootPositionXZ, c.keepOriginalPositionXZ, c.rotationOffset)));

    // Each take's Offset as the copy has it now (0 for a take it hasn't got).
    static Dictionary<string, float> CurrentOffsets(ModelImporter importer)
    {
        var offsets = new Dictionary<string, float>();
        foreach (ModelImporterClipAnimation c in importer.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>())
            if (!string.IsNullOrEmpty(c.name) && !offsets.ContainsKey(c.name)) offsets[c.name] = c.rotationOffset;
        foreach (string take in Takes)
            if (!offsets.ContainsKey(take)) offsets[take] = 0f;
        return offsets;
    }

    // A few of the avatar's bones, to see what the automatic mapping picked.
    static string Mapped(Avatar avatar)
    {
        string[] wanted = { "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftUpperArm", "LeftHand", "LeftUpperLeg", "LeftFoot", "LeftToes", "LeftEye", "RightEye", "Jaw" };
        HumanBone[] bones = avatar.humanDescription.human ?? Array.Empty<HumanBone>();
        var parts = new List<string>();
        foreach (string w in wanted)
        {
            HumanBone b = bones.FirstOrDefault(h => h.humanName == w);
            parts.Add(w + "=" + (string.IsNullOrEmpty(b.boneName) ? "-" : b.boneName));
        }
        return $"{bones.Length} bones mapped ({string.Join(", ", parts)})";
    }

    static float AceSpeed(PlayerMovement ace)
    {
        SerializedProperty speed = new SerializedObject(ace).FindProperty("moveSpeed");
        return speed != null && speed.floatValue > .1f ? speed.floatValue : 5f;
    }

    // ------------------------------------------------------------------ straight on Ace

    // The takes that go, straightened by where the planted foot slides; and the ones that stand, by where the hips and
    // shoulders face.
    static readonly string[] Going = { WalkTake, "Jog_Fwd_Loop", "Sprint_Loop", CrouchWalkTake };
    static readonly string[] Standing = { IdleTake, CrouchIdleTake };
    const float StraightWithin = 1.5f;   // degrees

    // Each clip, as Ace plays it, must go (walking, running, crouch walking) or face (standing, crouched still) straight along
    // Ace's own forward, the way AceBody turns him. The copy's "Body Orientation" root takes the body's facing at the clip's
    // first frame, so a loop that starts mid-twist comes out turned on Ace: on 6 Oct 2026 the jog went 28° off, the sprint 20°
    // and the walk 7°, and Ace ran diagonally (Mansoor's report). This measures each on Ace, sets each clip's Offset (Root
    // Transform Rotation) to cancel it, re-imports the copy, and measures again. Already straight: nothing changes.
    static Dictionary<string, AnimationClip> Straighten(Studio studio, GameObject sidekick, bool footIK, Dictionary<string, AnimationClip> clips, StringBuilder report)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(LibraryHumanoid);
        Dictionary<string, float> offsets0 = CurrentOffsets(importer);
        Dictionary<string, float> angles0 = TakeAngles(studio, sidekick, footIK, clips);
        report.AppendLine();
        report.AppendLine("Straight on Ace: how far each clip goes (walking, running, crouch walking) or faces (standing, crouched still) off his own " +
                          "forward, + to his right; in brackets each clip's Offset (Root Transform Rotation), degrees:");
        report.AppendLine("  as the copy had them: " + Angles(angles0, offsets0));
        if (angles0.Values.All(a => Mathf.Abs(a) <= StraightWithin))
        {
            report.AppendLine($"  all within {StraightWithin}°: nothing to straighten.");
            return clips;
        }
        // Cancel each angle, re-import and measure again. Which way an Offset turns a clip comes from the most turned one.
        Dictionary<string, float> offsets1 = Takes.ToDictionary(t => t, t => offsets0[t] - (angles0.TryGetValue(t, out float a) ? a : 0f));
        clips = HumanoidLibrary(new StringBuilder(), offsets1);
        Dictionary<string, float> angles1 = TakeAngles(studio, sidekick, footIK, clips);
        string most = angles0.OrderByDescending(kv => Mathf.Abs(kv.Value)).First().Key;
        float turn = Mathf.DeltaAngle(angles0[most], angles1[most]) / Mathf.DeltaAngle(offsets0[most], offsets1[most]);
        report.AppendLine($"  an Offset turns a clip {turn:+0.00;-0.00}° a degree ({most}: {angles0[most]:+0.0;-0.0}° to {angles1[most]:+0.0;-0.0}°)");
        Dictionary<string, float> offsets = offsets1, angles = angles1;
        if (angles1.Values.Any(a => Mathf.Abs(a) > StraightWithin))
        {
            if (Mathf.Abs(Mathf.Abs(turn) - 1f) > .25f)
                throw new InvalidOperationException($"An Offset turned {most} by {turn:0.00}° a degree, so the clips can't be straightened this way: see the " +
                                                    $"import settings of {LibraryHumanoid} (Root Transform Rotation). AceBody wasn't changed.");
            float way = Mathf.Sign(turn);
            offsets = Takes.ToDictionary(t => t, t => offsets0[t] - (angles0.TryGetValue(t, out float a) ? a : 0f) / way);
            clips = HumanoidLibrary(new StringBuilder(), offsets);
            angles = TakeAngles(studio, sidekick, footIK, clips);
        }
        report.AppendLine("  straightened: " + Angles(angles, offsets));
        string[] still = angles.Where(kv => Mathf.Abs(kv.Value) > StraightWithin).Select(kv => $"{kv.Key} {kv.Value:+0.0;-0.0}°").ToArray();
        if (still.Length > 0)
            throw new InvalidOperationException($"Couldn't straighten {string.Join(", ", still)} to within {StraightWithin}°. AceBody wasn't changed.");
        return clips;
    }

    // How far each take goes or faces off Ace's own forward, on Ace (degrees, + to his right).
    static Dictionary<string, float> TakeAngles(Studio studio, GameObject sidekick, bool footIK, Dictionary<string, AnimationClip> clips)
    {
        var angles = new Dictionary<string, float>();
        GameObject actor = studio.Add(sidekick);
        try
        {
            using var poser = new Poser(actor, footIK);
            foreach (string take in Going)
            {
                Gait g = HumanGait(poser, clips[take], null);
                if (g.speed <= .1f) throw new InvalidOperationException($"Couldn't measure which way '{take}' goes on Ace ({g.how}). AceBody wasn't changed.");
                angles[take] = g.facing > 0 ? g.travel : Mathf.DeltaAngle(0f, g.travel + 180f);
            }
            foreach (string take in Standing) angles[take] = TorsoFacing(poser, clips[take]);
        }
        finally { studio.Remove(actor); }
        return angles;
    }

    // Where the body faces over a clip, against the root's forward: square to the hips and to the shoulders, averaged over the
    // clip (degrees, + to the right).
    static float TorsoFacing(Poser poser, AnimationClip clip)
    {
        Animator a = poser.Animator;
        Transform lh = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rh = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform ls = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), rs = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (lh == null || rh == null || ls == null || rs == null) throw new InvalidOperationException("Ace's avatar has no upper legs or upper arms mapped.");
        Transform root = a.transform;
        int n = Mathf.Max(24, Mathf.RoundToInt(clip.length * 30f));
        double sin = 0d, cos = 0d;
        for (int i = 0; i < n; i++)
        {
            poser.Pose(clip, clip.length * i / n);
            foreach ((Transform l, Transform r) in new[] { (lh, rh), (ls, rs) })
            {
                Vector3 ahead = Vector3.Cross(root.InverseTransformDirection(r.position - l.position), Vector3.up);
                double yaw = Math.Atan2(ahead.x, ahead.z);
                sin += Math.Sin(yaw);
                cos += Math.Cos(yaw);
            }
        }
        return (float)(Math.Atan2(sin, cos) * Mathf.Rad2Deg);
    }

    static string Angles(Dictionary<string, float> angles, Dictionary<string, float> offsets) =>
        string.Join(", ", angles.Select(kv => $"{kv.Key} {kv.Value:+0.0;-0.0}° ({(offsets.TryGetValue(kv.Key, out float o) ? o : 0f):0.0})"));

    // ------------------------------------------------------------------ measuring

    // facing: +1 when the body faces +Z (the stance foot slides toward -Z), -1 when it faces -Z.
    // travel: which way the clip goes against the body's own forward, degrees, + to its right (where the planted foot slides, reversed).
    struct Gait { public float speed, leftForward, hipsDrift, travel; public int facing; public string how; }

    struct Measured
    {
        public float natural, width, target, scale, idleLowest, runLowestMin, runLowestMax, aceSpeed;
        public Gait walk, run;
        public string runTake, targetHow;
    }

    static Measured Measure(Studio studio, GameObject sidekick, Dictionary<string, AnimationClip> clips, bool footIK, float aceSpeed, string folder, StringBuilder report)
    {
        var m = new Measured { aceSpeed = aceSpeed };
        GameObject actor = studio.Add(sidekick);
        try
        {
            using var poser = new Poser(actor, footIK);
            // Standing, as exported (scale 1).
            poser.Pose(clips[IdleTake], clips[IdleTake].length * .35f);
            Bounds standing = PosedBounds(actor);
            m.natural = standing.size.y;
            m.width = Mathf.Max(standing.size.x, standing.size.z);
            m.idleLowest = standing.min.y;
            if (m.natural < .5f) throw new InvalidOperationException($"Couldn't measure Ace standing ({m.natural:0.00} m tall). AceBody wasn't changed.");

            // Each clip's natural speed and stride phase on this body; and, to compare, the library's clips on its own body.
            report.AppendLine();
            report.AppendLine("The library's clips on its own body (Generic, the café's import):");
            report.Append(SourceStrides(folder));
            m.walk = HumanGait(poser, clips[WalkTake], Path.Combine(folder, "ace-" + WalkTake + ".csv"));
            var runs = RunTakes.Select(t => (take: t, gait: HumanGait(poser, clips[t], Path.Combine(folder, "ace-" + t + ".csv")))).ToArray();
            report.AppendLine("The same clips on Ace (Humanoid):");
            report.AppendLine("  " + m.walk.how);
            foreach (var r in runs) report.AppendLine("  " + r.gait.how);
            if (m.walk.facing < 0 || runs.Any(r => r.gait.facing < 0))
                throw new InvalidOperationException("On Ace the clips face -Z, backwards for AceBody (which turns the body's +Z toward where Ace goes): " +
                                                    $"see the import settings of {LibraryHumanoid} (Root Transform Rotation). AceBody wasn't changed.");
            if (m.walk.speed <= .2f || runs.Any(r => r.gait.speed <= .2f))
                throw new InvalidOperationException($"Couldn't measure the clips' speeds (walk: {m.walk.how}; " +
                                                    string.Join("; ", runs.Select(r => r.take + ": " + r.gait.how)) + "). AceBody wasn't changed.");

            // Size: the café people's height.
            m.target = CafePeopleHeight(out m.targetHow);
            m.scale = m.target / m.natural;

            // The run whose natural speed at this size is nearest Ace's own (so it plays nearest its own rate).
            var best = runs.OrderBy(r => Mathf.Abs(Mathf.Log(aceSpeed / (r.gait.speed * m.scale)))).First();
            m.runTake = best.take;
            m.run = best.gait;

            // The feet on the floor: the lowest point of the body over the run, at scale 1.
            m.runLowestMin = float.MaxValue;
            m.runLowestMax = float.MinValue;
            AnimationClip runClip = clips[m.runTake];
            for (int i = 0; i < 8; i++)
            {
                poser.Pose(runClip, runClip.length * i / 8f);
                float low = PosedBounds(actor).min.y;
                m.runLowestMin = Mathf.Min(m.runLowestMin, low);
                m.runLowestMax = Mathf.Max(m.runLowestMax, low);
            }

            report.AppendLine();
            report.AppendLine($"As exported: {m.natural:0.00} m tall standing, {m.width:0.00} m across, lowest point {m.idleLowest * 100f:+0.0;-0.0} cm.");
            report.AppendLine($"Size: {m.target:0.00} m, the café people's height ({m.targetHow}), so {m.scale:0.000}x.");
            report.AppendLine($"Walk: {m.walk.how}. At {m.scale:0.000}x: {m.walk.speed * m.scale:0.00} m/s.");
            foreach (var r in runs)
                report.AppendLine($"{r.take}: {r.gait.how}. At {m.scale:0.000}x: {r.gait.speed * m.scale:0.00} m/s, so at Ace's {aceSpeed:0.0} m/s it would play at " +
                                  $"{aceSpeed / (r.gait.speed * m.scale):0.00}x{(r.take == m.runTake ? " (picked: nearest its own rate)" : "")}.");
            report.AppendLine($"Feet on the floor over the {m.runTake} cycle: the body's lowest point is between {m.runLowestMin * 100f:+0.0;-0.0} and " +
                              $"{m.runLowestMax * 100f:+0.0;-0.0} cm at scale 1 (0 is the floor; the stance foot should reach about 0).");
            return m;
        }
        finally { studio.Remove(actor); }
    }

    // The clip is in place, so while a foot is on the ground it slides back at the speed the clip was made for: the median
    // backward speed of a low foot (the stand-in's method). Also when the left foot is furthest forward, and how far the hips
    // travel over one cycle (in place: about 0). One cycle is n samples; the clips loop, so the step after the last is the first.
    // Every sample goes to a CSV beside the report (time, left foot, right foot, hips: x y z in the body's own space).
    static Gait HumanGait(Poser poser, AnimationClip clip, string csv)
    {
        Animator a = poser.Animator;
        Transform left = a.GetBoneTransform(HumanBodyBones.LeftFoot), right = a.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        if (left == null || right == null || hips == null) return new Gait { how = "the avatar has no feet or hips" };
        Transform root = a.transform;
        int n = Mathf.Max(24, Mathf.RoundToInt(clip.length * 60f));
        var feet = new Vector3[2, n];
        var hip = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            poser.Pose(clip, clip.length * i / n);
            feet[0, i] = root.InverseTransformPoint(left.position);
            feet[1, i] = root.InverseTransformPoint(right.position);
            hip[i] = root.InverseTransformPoint(hips.position);
        }
        return Stride(clip.name, clip.length, feet, hip, csv);
    }

    // The same, from positions already sampled (the Humanoid clips on Ace, or the library's own clips on its own body).
    static Gait Stride(string name, float length, Vector3[,] feet, Vector3[] hip, string csv)
    {
        int n = hip.Length;
        float dt = length / n;
        if (!string.IsNullOrEmpty(csv))
        {
            var rows = new StringBuilder("t,left_x,left_y,left_z,right_x,right_y,right_z,hips_x,hips_y,hips_z\n");
            for (int i = 0; i < n; i++)
                rows.AppendLine(string.Join(",", new[] { i * dt, feet[0, i].x, feet[0, i].y, feet[0, i].z, feet[1, i].x, feet[1, i].y, feet[1, i].z, hip[i].x, hip[i].y, hip[i].z }
                    .Select(v => v.ToString("0.#####", CultureInfo.InvariantCulture))));
            File.WriteAllText(csv, rows.ToString());
        }
        // While a foot is low it is on the ground, sliding back: toward -Z if the body faces +Z, toward +Z if it faces -Z.
        var back = new List<float>();
        var ahead = new List<float>();
        Vector2 backWay = Vector2.zero, aheadWay = Vector2.zero;   // the slides summed (x, z)
        for (int f = 0; f < 2; f++)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++) { minY = Mathf.Min(minY, feet[f, i].y); maxY = Mathf.Max(maxY, feet[f, i].y); }
            float low = minY + (maxY - minY) * .25f;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                if (feet[f, i].y > low || feet[f, next].y > low) continue;
                float v = (feet[f, next].z - feet[f, i].z) / dt, side = (feet[f, next].x - feet[f, i].x) / dt;
                if (v < -.2f) { back.Add(-v); backWay += new Vector2(side, v); }
                else if (v > .2f) { ahead.Add(v); aheadWay += new Vector2(side, v); }
            }
        }
        int facing = back.Count >= ahead.Count ? 1 : -1;
        List<float> speeds = facing > 0 ? back : ahead;
        // Which way the clip goes against the body's own forward: the planted foot's slides summed, reversed.
        Vector2 slid = facing > 0 ? backWay : aheadWay;
        float goes = slid.sqrMagnitude > 1e-6f ? Mathf.Atan2(-slid.x * facing, -slid.y * facing) * Mathf.Rad2Deg : 0f;
        int forward = 0;
        for (int i = 1; i < n; i++) if (facing * (feet[0, i].z - feet[0, forward].z) > 0f) forward = i;
        // How far the hips go over one cycle: from the first sample to the last, plus one more step at that rate.
        Vector3 travel = (hip[n - 1] - hip[0]) * n / Mathf.Max(1, n - 1);
        float drift = new Vector2(travel.x, travel.z).magnitude;
        string where = $"faces {(facing > 0 ? "+Z" : "-Z")}, goes {goes:+0.0;-0.0}° off straight ahead, hips travel {drift * 100f:0.0} cm over a cycle";
        if (speeds.Count < 4)
            return new Gait { hipsDrift = drift, travel = goes, facing = facing, how = $"'{name}' {length:0.00} s: too few stance samples ({speeds.Count} of {n}); {where}" };
        speeds.Sort();
        float median = speeds[speeds.Count / 2];
        return new Gait
        {
            speed = median,
            leftForward = (float)forward / n,
            hipsDrift = drift,
            travel = goes,
            facing = facing,
            how = $"'{name}' {length:0.00} s, stance foot {median:0.00} m/s over {speeds.Count} of {n} samples (scale 1), " +
                  $"left foot furthest forward at {(float)forward / n:0.00} of the cycle, {where}" + (drift > .05f ? " (NOT in place)" : ""),
        };
    }

    // The library's own clips on its own body (Generic, as imported for the café): whether each take runs in place as made.
    static string SourceStrides(string folder)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(LibrarySource);
        if (model == null) return "The library's own body isn't there.";
        Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(LibrarySource).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
        GameObject actor = Object.Instantiate(model);
        actor.hideFlags = HideFlags.HideAndDontSave;
        var lines = new StringBuilder();
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            foreach (Animator an in actor.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            Transform[] all = actor.GetComponentsInChildren<Transform>(true);
            Transform left = all.FirstOrDefault(x => x.name == "DEF-foot.L"), right = all.FirstOrDefault(x => x.name == "DEF-foot.R");
            Transform hips = all.FirstOrDefault(x => x.name == "DEF-hips");
            if (left == null || right == null || hips == null) return "The library's body has no DEF-foot.L, DEF-foot.R or DEF-hips.";
            foreach (string take in new[] { WalkTake }.Concat(RunTakes))
            {
                AnimationClip clip = clips.Values.FirstOrDefault(c => c.name == take || c.name.EndsWith("|" + take, StringComparison.Ordinal));
                if (clip == null) { lines.AppendLine($"  {take}: not in the library's own import."); continue; }
                int n = Mathf.Max(24, Mathf.RoundToInt(clip.length * 60f));
                var feet = new Vector3[2, n];
                var hip = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    clip.SampleAnimation(actor, clip.length * i / n);
                    feet[0, i] = actor.transform.InverseTransformPoint(left.position);
                    feet[1, i] = actor.transform.InverseTransformPoint(right.position);
                    hip[i] = actor.transform.InverseTransformPoint(hips.position);
                }
                lines.AppendLine("  As made: " + Stride(clip.name, clip.length, feet, hip, Path.Combine(folder, "source-" + take + ".csv")).how);
            }
        }
        finally { Object.DestroyImmediate(actor); }
        return lines.ToString();
    }

    // The stand-in's height: look 11 on the café rig at the café people's size, standing.
    static float CafePeopleHeight(out string how)
    {
        GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(AceBodySteps.RigModel);
        GameObject look = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(AceBodySteps.Look));
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(AceBodySteps.RigModel).OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == AceBodySteps.IdleName);
        if (rig != null && look != null && idle != null)
        {
            float scale = AceBodySteps.CafePeopleScale();
            float height = AceBodySteps.ProveLook(rig, look, idle, scale, out _, out _);
            if (height > 1f)
            {
                how = $"{look.name} on the café rig at {scale:0.00}, standing";
                return height;
            }
        }
        how = $"the measured {CafePeopleFallback:0.00} m from 29 Sept (the stand-in look or the café rig is missing)";
        return CafePeopleFallback;
    }

    static Bounds PosedBounds(GameObject actor)
    {
        bool any = false;
        Bounds b = default;
        foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!skin.enabled || skin.sharedMesh == null) continue;
            Mesh posed = Studio.SkinWithShapes(skin);
            try
            {
                if (posed.vertexCount == 0) continue;
                if (!any) { b = posed.bounds; any = true; }
                else b.Encapsulate(posed.bounds);
            }
            finally { Object.DestroyImmediate(posed); }
        }
        return b;
    }

    // ------------------------------------------------------------------ photos

    static string Photos(Studio studio, GameObject sidekick, Dictionary<string, AnimationClip> clips, Measured m, bool footIK, string folder)
    {
        var notes = new StringBuilder();
        Vector3 body = new Vector3(0f, 1.05f, 0f);
        const int W = 300, H = 420;
        var sheet = new Sheet(4, 2, W, H);
        GameObject person = null, ace = null;
        try
        {
            // A café person first: the stand-in (look 11) on the café rig at the café people's size, standing.
            PolygonNpcVisual visual = null;
            AnimationClip cafeIdle = null;
            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(AceBodySteps.RigModel);
            GameObject look = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(AceBodySteps.Look));
            if (rig != null && look != null)
            {
                cafeIdle = AssetDatabase.LoadAllAssetsAtPath(AceBodySteps.RigModel).OfType<AnimationClip>().FirstOrDefault(c => c.name == AceBodySteps.IdleName);
                person = studio.Add(rig);
                person.transform.localScale = Vector3.one * AceBodySteps.CafePeopleScale();
                foreach (Animator a in person.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                visual = person.AddComponent<PolygonNpcVisual>();
                visual.Configure(new[] { look }, 0f, 0, 0f);
                if (!visual.ApplyAppearance(0)) { studio.Remove(person); person = null; visual = null; }
            }
            void StandPerson(float x)
            {
                person.transform.position = new Vector3(x, 0f, 0f);
                if (cafeIdle != null) cafeIdle.SampleAnimation(person, cafeIdle.length * .35f);
                visual.Follow();
            }
            if (person != null)
            {
                StandPerson(0f);
                sheet.Put(0, 0, studio.Shot(body, 25f, 8f, 5.2f, 24f, W, H));
                person.SetActive(false);
            }

            // Ace, at the size chosen, posed the way AceBody plays the clips.
            ace = studio.Add(sidekick);
            ace.transform.localScale = Vector3.one * m.scale;
            using var poser = new Poser(ace, footIK);
            AceFace face = ace.AddComponent<AceFace>();
            bool faceBound = face.Bind(ace.transform, poser.Animator);
            // What the jaw does without the face (the Humanoid neutral), for the report: pose the idle, measure, then hold it.
            Transform jawBone = poser.Animator.GetBoneTransform(HumanBodyBones.Jaw);
            if (jawBone != null)
            {
                Quaternion rest = jawBone.localRotation;
                poser.Pose(clips[IdleTake], clips[IdleTake].length * .35f);
                notes.AppendLine($"The jaw under the Humanoid clips alone: {Quaternion.Angle(rest, jawBone.localRotation):0.0}° from shut; AceFace holds it shut.");
            }
            AnimationClip idle = clips[IdleTake], walk = clips[WalkTake], run = clips[m.runTake];

            // 1. Moving: the café person (above), then Ace standing, walking (each foot forward) and running (four moments).
            (AnimationClip clip, float at)[] poses =
            {
                (idle, .35f), (walk, m.walk.leftForward), (walk, m.walk.leftForward + .5f),
                (run, m.run.leftForward), (run, m.run.leftForward + .25f), (run, m.run.leftForward + .5f), (run, m.run.leftForward + .75f),
            };
            for (int i = 0; i < poses.Length; i++)
            {
                poser.Pose(poses[i].clip, Mathf.Repeat(poses[i].at, 1f) * poses[i].clip.length);
                face.Apply(0f, Vector2.zero);
                int cell = i + 1;
                sheet.Put(cell % 4, cell / 4, studio.Shot(body, 25f, 8f, 5.2f, 24f, W, H));
            }
            string moves = Path.Combine(folder, "1-moves.png");
            sheet.Save(moves);
            notes.AppendLine("Photo 1: " + moves);
            notes.AppendLine($"  Top: {(person != null ? "a café person (the stand-in, look 11, on the café rig)" : "(no café person: the stand-in look is missing)")}, " +
                             $"Ace standing ({IdleTake}), Ace walking ({WalkTake}: the left foot forward, then the right). " +
                             $"Bottom: Ace running ({m.runTake}), four moments a quarter of a stride apart.");

            // 2. Beside a café person: both standing, front and three-quarters. The camera looks back from +Z,
            // so +X is on the left of the picture: the café person there, Ace on the right.
            if (person != null)
            {
                person.SetActive(true);
                StandPerson(.55f);
                ace.transform.position = new Vector3(-.55f, 0f, 0f);
                poser.Pose(idle, idle.length * .35f);
                face.Apply(0f, Vector2.zero);
                var pair = new Sheet(2, 1, 560, 620);
                pair.Put(0, 0, studio.Shot(body, 0f, 6f, 7f, 24f, 560, 620));
                pair.Put(1, 0, studio.Shot(body, 35f, 6f, 7f, 24f, 560, 620));
                string beside = Path.Combine(folder, "2-beside-a-cafe-person.png");
                pair.Save(beside);
                notes.AppendLine("Photo 2: " + beside);
                notes.AppendLine($"  Left, a café person (look 11 on the café rig, {m.target:0.00} m); right, Ace ({m.natural * m.scale:0.00} m). Front, then three-quarters.");
                person.SetActive(false);
                ace.transform.position = Vector3.zero;
            }

            // 3. The face: open; shut with the lower lid at 0, 50 and 100; glancing right, left, up and down (the glances' own reach).
            if (faceBound)
            {
                poser.Pose(idle, idle.length * .35f);
                Transform eyeL = poser.Animator.GetBoneTransform(HumanBodyBones.LeftEye), eyeR = poser.Animator.GetBoneTransform(HumanBodyBones.RightEye);
                Transform head = poser.Animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 eyes = eyeL != null && eyeR != null ? (eyeL.position + eyeR.position) * .5f
                    : head != null ? head.position + Vector3.up * .08f * m.scale : new Vector3(0f, 1.8f, 0f);
                float lower = face.lowerLid;
                Vector2 reach = face.glanceReach;
                (float shut, float lowerLid, Vector2 look)[] faces =
                {
                    (0f, lower, Vector2.zero), (1f, 0f, Vector2.zero), (1f, 50f, Vector2.zero), (1f, 100f, Vector2.zero),
                    (0f, lower, new Vector2(reach.x, 0f)), (0f, lower, new Vector2(-reach.x, 0f)), (0f, lower, new Vector2(0f, -reach.y)), (0f, lower, new Vector2(0f, reach.y)),
                };
                var faceSheet = new Sheet(4, 2, 320, 320);
                for (int i = 0; i < faces.Length; i++)
                {
                    face.lowerLid = faces[i].lowerLid;
                    face.Apply(faces[i].shut, faces[i].look);
                    faceSheet.Put(i % 4, i / 4, studio.Shot(eyes + Vector3.down * .03f * m.scale, 0f, 0f, .5f * m.scale, 24f, 320, 320));
                }
                face.lowerLid = lower;
                face.Apply(0f, Vector2.zero);
                string faceFile = Path.Combine(folder, "3-face.png");
                faceSheet.Save(faceFile);
                notes.AppendLine("Photo 3: " + faceFile);
                notes.AppendLine($"  Top: eyes open; shut with the upper lid at {face.upperLid:0} and the lower lid at 0, 50 and 100 (AceFace uses {lower:0}). " +
                                 $"Bottom: glancing right, left, up and down, {reach.x:0}° and {reach.y:0}° (the glances' reach).");
                notes.AppendLine($"  The face found: {face.Parts}.");
            }
            else notes.AppendLine("Photo 3 (the face): skipped, AceFace found neither the lid shapes nor the eyes: " + face.Parts);
        }
        finally
        {
            if (ace != null) studio.Remove(ace);
            if (person != null) studio.Remove(person);
        }
        return notes.ToString();
    }

    // ------------------------------------------------------------------ tools

    // Poses a Humanoid body in edit mode the way AceBody plays it at runtime: a PlayableGraph on its own Animator,
    // evaluated by hand, with foot IK as AceBody has it.
    sealed class Poser : IDisposable
    {
        public Animator Animator { get; }
        readonly bool footIK;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;

        public Poser(GameObject actor, bool footIK)
        {
            this.footIK = footIK;
            Animator = actor.GetComponentInChildren<Animator>(true);
            if (Animator == null) throw new InvalidOperationException(actor.name + " has no Animator.");
            Animator.runtimeAnimatorController = null;
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.Rebind();
            graph = PlayableGraph.Create("Ace's body (set-up)");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "pose", Animator);
        }

        public void Pose(AnimationClip clip, float time)
        {
            if (playable.IsValid()) playable.Destroy();
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(footIK);
            playable.SetSpeed(0);
            output.SetSourcePlayable(playable);
            playable.SetTime(time);
            graph.Evaluate(0f);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }

    // A grid of pictures, saved as one PNG.
    sealed class Sheet
    {
        readonly Texture2D texture;
        readonly int w, h, rows;

        public Sheet(int columns, int rows, int w, int h)
        {
            this.w = w;
            this.h = h;
            this.rows = rows;
            texture = new Texture2D(columns * w, rows * h, TextureFormat.RGB24, false);
            texture.SetPixels(Enumerable.Repeat(new Color(.22f, .22f, .24f), texture.width * texture.height).ToArray());
        }

        public void Put(int column, int row, Texture2D shot)
        {
            Color[] pixels;
            if (shot.width == w && shot.height == h) pixels = shot.GetPixels();
            else
            {
                // The picture came back another size (the screen's scaling): fit it to the cell.
                pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        pixels[y * w + x] = shot.GetPixelBilinear((x + .5f) / w, (y + .5f) / h);
            }
            texture.SetPixels(column * w, (rows - 1 - row) * h, w, h, pixels);
            Object.DestroyImmediate(shot);
        }

        public void Save(string file)
        {
            texture.Apply();
            File.WriteAllBytes(file, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }

    // The café people's line-up studio (PolygonNpcSetup.Lineup): the same camera, lights and background.
    sealed class Studio : IDisposable
    {
        readonly PreviewRenderUtility preview = new PreviewRenderUtility();
        readonly List<GameObject> actors = new List<GameObject>();

        public Studio()
        {
            preview.camera.fieldOfView = 24f;
            preview.camera.nearClipPlane = .02f;
            preview.camera.farClipPlane = 50f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.80f, .82f, .85f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
            preview.lights[1].intensity = .6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(20f, 220f, 0f);
            preview.ambientColor = new Color(.45f, .45f, .5f);
        }

        public GameObject Add(GameObject prefab)
        {
            GameObject actor = preview.InstantiatePrefabInScene(prefab);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            actors.Add(actor);
            return actor;
        }

        public void Remove(GameObject actor)
        {
            actors.Remove(actor);
            if (actor != null) Object.DestroyImmediate(actor);
        }

        // One picture of what's active: the camera yaw degrees round from the front (+Z), pitch degrees above, distance metres from focus.
        public Texture2D Shot(Vector3 focus, float yaw, float pitch, float distance, float fov, int w, int h)
        {
            preview.camera.fieldOfView = fov;
            preview.camera.transform.position = focus + Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * distance;
            preview.camera.transform.LookAt(focus);
            // Several renders happen inside one editor frame, and skinned meshes re-skin only once a frame: render posed copies.
            var baked = new List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)>();
            try
            {
                foreach (GameObject actor in actors)
                    if (actor != null && actor.activeInHierarchy) Bake(actor, baked);
                preview.BeginStaticPreview(new Rect(0, 0, w, h));
                preview.Render(true);
                return preview.EndStaticPreview();
            }
            finally
            {
                foreach (var b in baked)
                {
                    if (b.skin != null) b.skin.forceRenderingOff = false;
                    if (b.copy != null) Object.DestroyImmediate(b.copy);
                    if (b.mesh != null) Object.DestroyImmediate(b.mesh);
                }
            }
        }

        static void Bake(GameObject root, List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)> baked)
        {
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.forceRenderingOff || skin.sharedMesh == null) continue;
                Mesh mesh = SkinWithShapes(skin);
                var copy = new GameObject("Posed " + skin.name) { hideFlags = HideFlags.HideAndDontSave };
                if (copy.scene != root.scene) SceneManager.MoveGameObjectToScene(copy, root.scene);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = skin.sharedMaterials;
                renderer.shadowCastingMode = skin.shadowCastingMode;
                skin.forceRenderingOff = true;
                baked.Add((skin, copy, mesh));
            }
        }

        // PolygonNpcSetup.SkinToWorld with the blend shapes first: the Sidekick body's build (its body shapes) and the
        // face (the lids) are blend shapes, which that skips. Each shape at its weight, between its frames as Unity blends them.
        internal static Mesh SkinWithShapes(SkinnedMeshRenderer skin)
        {
            Mesh source = skin.sharedMesh;
            Vector3[] vertices = source.vertices, normals = source.normals;
            bool hasNormals = normals.Length == vertices.Length;
            int shapeCount = source.blendShapeCount;
            if (shapeCount > 0)
            {
                var dv = new Vector3[vertices.Length];
                var dn = new Vector3[vertices.Length];
                var dt = new Vector3[vertices.Length];
                for (int s = 0; s < shapeCount; s++)
                {
                    float weight = skin.GetBlendShapeWeight(s);
                    if (Mathf.Abs(weight) < 1e-4f) continue;
                    int frames = source.GetBlendShapeFrameCount(s);
                    if (frames == 0) continue;
                    int b = 0;
                    while (b < frames && source.GetBlendShapeFrameWeight(s, b) < weight) b++;
                    if (b == 0 || b == frames)
                    {
                        int f = b == 0 ? 0 : frames - 1;
                        float fw = source.GetBlendShapeFrameWeight(s, f);
                        AddFrame(source, s, f, fw > 1e-4f ? weight / fw : 1f, vertices, normals, hasNormals, dv, dn, dt);
                    }
                    else
                    {
                        float wa = source.GetBlendShapeFrameWeight(s, b - 1), wb = source.GetBlendShapeFrameWeight(s, b);
                        float t = wb - wa > 1e-4f ? (weight - wa) / (wb - wa) : 1f;
                        AddFrame(source, s, b - 1, 1f - t, vertices, normals, hasNormals, dv, dn, dt);
                        AddFrame(source, s, b, t, vertices, normals, hasNormals, dv, dn, dt);
                    }
                }
                if (hasNormals)
                    for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            }

            Transform[] bones = skin.bones;
            Matrix4x4[] bindposes = source.bindposes;
            BoneWeight[] weights = source.boneWeights;
            bool skinned = weights.Length == vertices.Length && bindposes.Length > 0;
            var matrices = new Matrix4x4[bindposes.Length];
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = (i < bones.Length && bones[i] != null ? bones[i].localToWorldMatrix : skin.transform.localToWorldMatrix) * bindposes[i];
            Matrix4x4 rigid = skin.transform.localToWorldMatrix;
            var world = new Vector3[vertices.Length];
            var worldNormals = new Vector3[hasNormals ? vertices.Length : 0];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 n = hasNormals ? normals[v] : Vector3.zero;
                if (!skinned)
                {
                    world[v] = rigid.MultiplyPoint3x4(vertices[v]);
                    if (hasNormals) worldNormals[v] = rigid.MultiplyVector(n).normalized;
                    continue;
                }
                BoneWeight w = weights[v];
                Vector3 p = Vector3.zero, q = Vector3.zero;
                float total = 0f;
                Blend(matrices, w.boneIndex0, w.weight0, vertices[v], n, ref p, ref q, ref total);
                Blend(matrices, w.boneIndex1, w.weight1, vertices[v], n, ref p, ref q, ref total);
                Blend(matrices, w.boneIndex2, w.weight2, vertices[v], n, ref p, ref q, ref total);
                Blend(matrices, w.boneIndex3, w.weight3, vertices[v], n, ref p, ref q, ref total);
                world[v] = total > 1e-6f ? p / total : rigid.MultiplyPoint3x4(vertices[v]);
                if (hasNormals) worldNormals[v] = total > 1e-6f ? q.normalized : rigid.MultiplyVector(n).normalized;
            }
            var mesh = new Mesh { name = source.name + " (posed)", indexFormat = source.indexFormat };
            mesh.vertices = world;
            if (hasNormals) mesh.normals = worldNormals;
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 4; channel++)
            {
                source.GetUVs(channel, uv);
                if (uv.Count == vertices.Length) mesh.SetUVs(channel, uv);
            }
            Color32[] colors = source.colors32;
            if (colors.Length == vertices.Length) mesh.colors32 = colors;
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s, false);
            mesh.RecalculateBounds();
            if (source.tangents.Length == vertices.Length && hasNormals) mesh.RecalculateTangents();
            return mesh;
        }

        static void AddFrame(Mesh source, int shape, int frame, float amount, Vector3[] vertices, Vector3[] normals, bool hasNormals,
            Vector3[] dv, Vector3[] dn, Vector3[] dt)
        {
            if (Mathf.Abs(amount) < 1e-5f) return;
            source.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
            for (int i = 0; i < vertices.Length; i++) vertices[i] += dv[i] * amount;
            if (hasNormals)
                for (int i = 0; i < normals.Length; i++) normals[i] += dn[i] * amount;
        }

        static void Blend(Matrix4x4[] matrices, int bone, float weight, Vector3 vertex, Vector3 normal, ref Vector3 p, ref Vector3 n, ref float total)
        {
            if (weight <= 0f || bone < 0 || bone >= matrices.Length) return;
            p += matrices[bone].MultiplyPoint3x4(vertex) * weight;
            n += matrices[bone].MultiplyVector(normal) * weight;
            total += weight;
        }

        public void Dispose()
        {
            foreach (GameObject actor in actors)
                if (actor != null) Object.DestroyImmediate(actor);
            actors.Clear();
            preview.Cleanup();
        }
    }
}
#endif
