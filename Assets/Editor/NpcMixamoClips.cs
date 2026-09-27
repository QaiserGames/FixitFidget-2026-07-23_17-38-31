#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Mixamo clips, batch 1, baked onto the café's NPC rig (27 Sept 2026).
//
// Mansoor: "can we find some more sitting and idle standing or leaning and
// frustrations and happy animations, all kinds when they come in". He approved
// 36 clips (downloaded from mixamo.com with his account: X Bot, without skin,
// FBX for Unity, 30 fps) in Assets/Art/Mixamo/Standing and /Sitting, and chose
// "bake + photo sheet first": nothing here is wired into the game.
//
// THE MIXAMO FILES, AND EVERYTHING BAKED FROM THEM, STAY OUT OF THE PUBLIC
// REPOSITORY. Assets/Art/Mixamo/ is git-ignored (Adobe's terms allow the clips
// in the game but not sharing the files), so the baked clips go to
// Assets/Art/Mixamo/Baked/ inside it. This script is tracked; on a copy
// without that folder it only says the clips are missing.
//
// BAKE (Mixamo 1). The same retargeting NpcGaitAnimations and NpcSitAnimations
// use for the Quaternius library, with Mixamo's skeleton:
//  * The rest pose is the X Bot's T-pose. Mixamo's files store it (each bone's
//    translation and pre-rotation, the same in every file), but Unity's import
//    leaves the bones in the clip's first frame, so the T-pose is written out
//    below (XBotRest) and checked against Unity's own import before each bake.
//    The two rest poses are matched, then every mapped bone takes Mixamo's
//    turn away from rest.
//  * The pelvis moves like Mixamo's hips, scaled by the ratio of the two rigs'
//    hip-joint heights; the feet go where Mixamo's feet go (same scale) and each
//    leg reaches them with two-bone IK. Clips stay in place.
//  * Limbs copy Mixamo's bone directions (as the library bakes do). Collarbones
//    are different: they take Mixamo's turn away from its own rest (a shrug
//    needs it), starting from the café rig's own collarbones, and the shoulder
//    joint may drop or swing back at most ShoulderSlack degrees from where the
//    café rig's T-pose has it, relative to the chest. That is the Pass 2b
//    lesson: collarbones rolled down and back sank the city bodies' shoulders
//    into their torsos.
//  * Seated clips are lined up with the café's chairs. The hip joints' average
//    position over the clip lands exactly where the café's own seated clips put
//    them (NpcSitData.seatedHip, fitted to the chairs by Sit 1), so NpcSeating's
//    placement works for them unchanged. The feet move sideways and forwards
//    with the hips and stay on the floor. Mixamo's seated hips sit anywhere from
//    57 to 79 cm up, and two clips sit 20-31 cm off the character's origin, so
//    every clip gets its own offset (the log lists them). As in Sit 1, hanging
//    upper arms are kept from swinging behind the back, where the city bodies'
//    sleeves would go through the chair's backrest.
//
// PHOTOS (Mixamo 2). A contact sheet for Mansoor to judge before anything is
// wired: every clip on a café customer in a city look, at three moments.
// Standing clips three side by side out in the street; seated clips in a real
// café chair, one photo per moment. The two phone clips hold POLYGON City's
// smartphone (SM_Prop_SmartPhone_01, Mansoor's choice) in the hand that holds
// it: the hand nearest the head on a call, the steadier hand when texting. It
// is placed for the photos only; the phone in the game comes with the wiring.
//
// Menu: Fixit Fidget > NPC > Mixamo 1 (bake), Mixamo 2 (photos). Re-running is safe.
public static class NpcMixamoClips
{
    const string Menu = "Fixit Fidget/NPC/";
    const string Tag = "[NPC Mixamo] ";
    public const string SourceRoot = "Assets/Art/Mixamo";
    public const string BakedRoot = SourceRoot + "/Baked";
    public static readonly string[] Groups = { "Standing", "Sitting" };
    const string BeachPath = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    const string CustomerPrefab = "Assets/AssetsPrefabs/Customer.prefab";
    const string PhonePrefab = "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_SmartPhone_01.prefab";
    const string ClipPrefix = "Npc Mx ";
    const float FrameRate = 30f;
    // Most a collarbone may lower or pull back the shoulder joint from the café rig's T-pose, degrees.
    const float ShoulderSlack = 4f;
    // Seated upper arms point at least this far forward (as NpcSitAnimations).
    const float ElbowForward = .02f;

    // Declared before Map: static fields are initialised in the order they are written.
    static readonly (string side, string s)[] Sides = { ("Left", "L"), ("Right", "R") };
    static readonly (string mixamo, string beach)[] Map = BuildMap();
    static readonly Dictionary<string, string> MixamoTip = BuildMixamoTips(), BeachTip = BuildBeachTips();

    // The X Bot's T-pose as Mixamo's files store it: each bone's local position (metres) and
    // rotation (its FBX pre-rotation), converted the way Unity converts an FBX file (X mirrored,
    // centimetres to metres). Read from the 36 files on 27 Sept 2026 (all identical; the four
    // earlier clips too). Mixamo 1 checks the bone lengths against every imported file and the
    // conversion against Unity's own import of "X Bot@Bored" (BoredFirstFrame) before using it.
    static readonly (string bone, float px, float py, float pz, float qx, float qy, float qz, float qw)[] XBotRest =
    {
        ("Hips", 0f, 1.042749f, 0f, 0.006459f, 0f, 0f, 0.999979f),
        ("Spine", 0f, 0.101824f, 0f, -0.080155f, 0f, 0f, 0.996782f),
        ("Spine1", 0f, 0.100027f, 0f, 0f, 0f, 0f, 1f),
        ("Spine2", 0f, 0.093221f, 0f, 0.012885f, 0f, 0f, 0.999917f),
        ("Neck", 0f, 0.168653f, 0f, 0f, 0f, 0f, 1f),
        ("Head", 0f, 0.093419f, 0.02841f, 0f, 0f, 0f, 1f),
        ("HeadTop_End", 0f, 0.209628f, 0.101229f, 0f, 0f, 0f, 1f),
        ("RightShoulder", 0.0457f, 0.111958f, -0.008066f, -0.48443f, -0.570964f, 0.526163f, -0.403087f),
        ("RightArm", 0f, 0.108382f, 0f, -0.024616f, -0.002562f, 0.103499f, 0.994322f),
        ("RightForeArm", 0f, 0.278415f, 0f, 0f, 0f, 0f, 1f),
        ("RightHand", 0f, 0.283288f, 0f, 0f, -0.000003f, 0f, 1f),
        ("RightHandThumb1", -0.026819f, 0.024648f, 0.01574f, 0.252089f, -0.060281f, 0.221704f, 0.940034f),
        ("RightHandThumb2", 0f, 0.04189f, 0f, 0f, 0.00117f, 0f, 0.999999f),
        ("RightHandThumb3", 0f, 0.034163f, 0f, 0f, 0.000807f, 0f, 1f),
        ("RightHandThumb4", 0f, 0.02575f, 0f, 0.005774f, 0.117509f, 0.048744f, 0.991858f),
        ("RightHandIndex1", -0.022598f, 0.091083f, 0.005179f, 0f, 0f, 0f, 1f),
        ("RightHandIndex2", 0f, 0.037f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandIndex3", 0f, 0.0285f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandIndex4", 0f, 0.027722f, 0f, 0f, -0.001003f, -0.000087f, 0.999999f),
        ("RightHandMiddle1", 0f, 0.095325f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandMiddle2", 0f, 0.037f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandMiddle3", 0f, 0.0295f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandMiddle4", 0f, 0.029466f, 0f, 0f, -0.000929f, -0.000167f, 1f),
        ("RightHandRing1", 0.018651f, 0.091036f, 0.000431f, 0f, 0f, 0f, 1f),
        ("RightHandRing2", 0f, 0.033793f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandRing3", 0f, 0.028897f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandRing4", 0f, 0.026388f, 0f, 0f, -0.000146f, 0.000799f, 1f),
        ("RightHandPinky1", 0.038063f, 0.080767f, 0.004867f, 0f, 0f, 0f, 1f),
        ("RightHandPinky2", 0f, 0.036f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandPinky3", 0f, 0.021f, 0f, 0f, 0f, 0f, 1f),
        ("RightHandPinky4", 0f, 0.021158f, 0f, 0f, -0.001569f, 0.000041f, 0.999999f),
        ("LeftShoulder", -0.045704f, 0.111956f, -0.008066f, 0.484423f, -0.57097f, 0.526162f, 0.40309f),
        ("LeftArm", 0f, 0.108377f, 0f, -0.024607f, 0.002562f, -0.103504f, 0.994321f),
        ("LeftForeArm", 0f, 0.278415f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHand", 0f, 0.283288f, 0f, 0f, -0.000001f, 0f, 1f),
        ("LeftHandThumb1", 0.026817f, 0.024661f, 0.015762f, 0.252061f, 0.060169f, -0.221736f, 0.940041f),
        ("LeftHandThumb2", 0f, 0.041871f, 0f, 0f, -0.001042f, 0f, 0.999999f),
        ("LeftHandThumb3", 0f, 0.034184f, 0f, 0f, -0.000745f, 0f, 1f),
        ("LeftHandThumb4", 0f, 0.025806f, 0f, 0.005153f, -0.122683f, -0.041652f, 0.991558f),
        ("LeftHandIndex1", 0.022599f, 0.091093f, 0.00518f, 0f, 0f, 0f, 1f),
        ("LeftHandIndex2", 0f, 0.037f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandIndex3", 0f, 0.0285f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandIndex4", 0f, 0.027749f, 0f, 0f, 0.000378f, 0.000013f, 1f),
        ("LeftHandMiddle1", 0f, 0.095334f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandMiddle2", 0f, 0.037f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandMiddle3", 0f, 0.0295f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandMiddle4", 0f, 0.029529f, 0f, 0f, 0.001025f, -0.000028f, 0.999999f),
        ("LeftHandRing1", -0.018651f, 0.091045f, 0.00043f, 0f, 0f, 0f, 1f),
        ("LeftHandRing2", 0f, 0.0315f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandRing3", 0f, 0.0295f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandRing4", 0f, 0.026443f, 0f, 0f, -0.000493f, -0.000016f, 1f),
        ("LeftHandPinky1", -0.038063f, 0.080778f, 0.004869f, 0f, 0f, 0f, 1f),
        ("LeftHandPinky2", 0f, 0.036f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandPinky3", 0f, 0.021f, 0f, 0f, 0f, 0f, 1f),
        ("LeftHandPinky4", 0f, 0.021255f, 0f, 0.000001f, 0.000784f, 0.000295f, 1f),
        ("RightUpLeg", 0.082078f, -0.067718f, -0.015122f, 0f, -0.010357f, -0.999946f, 0f),
        ("RightLeg", 0f, 0.443715f, 0f, -0.038091f, 0f, 0f, 0.999274f),
        ("RightFoot", 0f, 0.445278f, 0f, 0.45974f, 0f, 0f, 0.888053f),
        ("RightToeBase", 0f, 0.138169f, 0f, 0.335242f, 0f, 0f, 0.942132f),
        ("RightToe_End", 0f, 0.092781f, 0f, 0f, -0.011608f, 0f, 0.999933f),
        ("LeftUpLeg", -0.082078f, -0.067718f, -0.015122f, 0f, -0.010368f, -0.999946f, 0f),
        ("LeftLeg", 0f, 0.443714f, 0f, -0.038112f, 0f, 0f, 0.999273f),
        ("LeftFoot", 0f, 0.445278f, 0f, 0.459749f, 0f, 0f, 0.888049f),
        ("LeftToeBase", 0f, 0.138169f, 0f, 0.335241f, 0f, 0f, 0.942132f),
        ("LeftToe_End", 0f, 0.092781f, 0f, 0f, 0.011869f, 0f, 0.99993f),
    };

    static readonly (string bone, float qx, float qy, float qz, float qw)[] BoredFirstFrame =
    {
        ("Spine1", 0.024537f, 0.004362f, 0.001338f, 0.999689f), // 3 deg from rest
        ("Head", -0.026245f, 0.01533f, 0.003057f, 0.999533f), // 4 deg from rest
        ("LeftShoulder", 0.568956f, -0.508474f, 0.478569f, 0.434414f), // 14 deg from rest
        ("LeftArm", 0.47113f, 0.063343f, -0.319364f, 0.819775f), // 66 deg from rest
        ("LeftForeArm", 0f, 0f, -0.16516f, 0.986267f), // 19 deg from rest
        ("LeftHand", 0.153473f, -0.192194f, 0.040471f, 0.968437f), // 29 deg from rest
        ("LeftHandThumb1", 0.212576f, 0.055914f, -0.151522f, 0.963705f), // 10 deg from rest
        ("RightArm", 0.258985f, 0.079998f, 0.521867f, 0.808815f), // 63 deg from rest
        ("RightHandIndex1", 0.309498f, -0.004874f, -0.022661f, 0.950617f), // 36 deg from rest
        ("LeftUpLeg", 0.064435f, -0.076155f, -0.99467f, -0.026094f), // 11 deg from rest
        ("RightLeg", -0.240081f, -0.085169f, 0.017982f, 0.966842f), // 26 deg from rest
        ("RightFoot", 0.579261f, -0.029006f, -0.016401f, 0.814461f), // 17 deg from rest
    };

    [MenuItem(Menu + "Mixamo 1 - Bake the Mixamo clips onto the cafe rig")]
    static void BakeMenu() => Run("Bake", Bake);

    [MenuItem(Menu + "Mixamo 2 - Photograph the baked Mixamo clips (contact sheet)")]
    static void PhotoMenu() => Run("Photos", Photograph);

    // ------------------------------------------------------------------ bake

    public static string Bake()
    {
        RequireStopped();
        var beachAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BeachPath);
        Require(beachAsset != null, "Missing " + BeachPath);
        var sitData = AssetDatabase.LoadAssetAtPath<NpcSitData>(NpcSitAnimations.DataPath);
        Require(sitData != null, "Missing " + NpcSitAnimations.DataPath + " (run Fixit Fidget > NPC > Sit 1 first).");

        var sources = new List<(string group, string path)>();
        foreach (string group in Groups)
        {
            string folder = SourceRoot + "/" + group;
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                    && Path.GetDirectoryName(path).Replace('\\', '/') == folder)
                    sources.Add((group, path));
            }
        }
        Require(sources.Count > 0, "No Mixamo clips in " + SourceRoot + "/Standing or /Sitting. That folder is git-ignored, " +
                                   "so it only exists on the computer the clips were downloaded to.");
        sources = sources.OrderBy(s => Array.IndexOf(Groups, s.group)).ThenBy(s => s.path, StringComparer.Ordinal).ToList();

        var notes = new StringBuilder();
        int reimported = sources.Count(s => PrepareImport(s.path));
        notes.AppendLine($"{sources.Count} Mixamo clips ({sources.Count(s => s.group == "Standing")} standing, " +
                         $"{sources.Count(s => s.group == "Sitting")} seated); import settings changed on {reimported}.");
        notes.AppendLine(CheckConversion(sources.Select(s => s.path)));

        GameObject beach = Object.Instantiate(beachAsset);
        beach.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            beach.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            beach.transform.localScale = Vector3.one;
            foreach (var a in beach.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var B = Index(beach.transform);
            Require(B.ContainsKey("Root") && B["Root"].parent != null, "Cafe rig has no Root bone.");
            foreach (var (_, b) in Map) Require(B.ContainsKey(b), "Cafe rig has no bone " + b);

            // Café rig rest pose (its T-pose), straight from the skinned meshes' bind poses.
            var bind = new Dictionary<string, (Vector3 p, Quaternion r)>(StringComparer.Ordinal);
            foreach (var skin in beach.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                Transform[] bones = skin.bones;
                Matrix4x4[] poses = skin.sharedMesh.bindposes;
                for (int i = 0; i < bones.Length && i < poses.Length; i++)
                {
                    if (bones[i] == null || bind.ContainsKey(bones[i].name)) continue;
                    Matrix4x4 m = beach.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix * poses[i].inverse;
                    bind[bones[i].name] = (m.GetColumn(3), m.rotation);
                }
            }
            foreach (var (_, b) in Map) Require(bind.ContainsKey(b), "Cafe rig bone " + b + " has no bind pose.");
            var rigBones = beach.GetComponentsInChildren<Transform>(true)
                .Where(t => t != beach.transform && t.IsChildOf(B["Root"].parent) && t != B["Root"].parent).ToArray();
            foreach (Transform t in rigBones)
                if (bind.TryGetValue(t.name, out var bp))
                    t.SetPositionAndRotation(beach.transform.TransformPoint(bp.p), beach.transform.rotation * bp.r);
            var rest = rigBones.ToDictionary(t => t, t => (t.localPosition, t.localRotation));
            var restPos = rigBones.ToDictionary(t => t.name, t => beach.transform.InverseTransformPoint(t.position), StringComparer.Ordinal);
            var ankle = new Dictionary<string, Vector3>();
            foreach (string s in new[] { "L", "R" }) ankle[s] = B["LowerLeg." + s].InverseTransformPoint(B["Foot." + s].position);
            float hipB = (bind["UpperLeg.L"].p.y + bind["UpperLeg.R"].p.y) * .5f;
            var paths = rigBones.Where(t => !t.name.EndsWith("_end", StringComparison.Ordinal))
                .Select(t => (t, path: AnimationUtility.CalculateTransformPath(t, beach.transform))).ToArray();
            string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            notes.AppendLine($"Cafe rig: hip joints {hipB:0.000} up; seated clips are lined up with NpcSitData.seatedHip {V(sitData.seatedHip)}.");

            foreach (string group in Groups) EnsureFolder(BakedRoot + "/" + group);
            string lastRig = null;
            foreach (var (group, path) in sources)
            {
                bool seated = group == "Sitting";
                AnimationClip clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                Require(clip != null, Path.GetFileName(path) + " has no animation.");
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(model != null, "Cannot load " + path);
                GameObject mx = Object.Instantiate(model);
                mx.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    mx.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    mx.transform.localScale = Vector3.one;
                    foreach (var a in mx.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                    var U = IndexShort(mx.transform);
                    foreach (var (u, _) in Map) Require(U.ContainsKey(u), Path.GetFileName(path) + " has no bone " + u);

                    // Rest pose: the X Bot's T-pose from XBotRest, once the file is shown to be the X Bot
                    // (its bone lengths) and, on "Bored", the conversion to match Unity's import.
                    float worstLength = 0f;
                    foreach (var r in XBotRest)
                    {
                        Require(U.ContainsKey(r.bone), Path.GetFileName(path) + " has no bone " + r.bone + ": not Mixamo's X Bot skeleton.");
                        if (r.bone != "Hips") worstLength = Mathf.Max(worstLength, Vector3.Distance(U[r.bone].localPosition, new Vector3(r.px, r.py, r.pz)));
                    }
                    Require(worstLength < .002f, $"{Path.GetFileName(path)}: its bones differ from the X Bot's by up to {worstLength * 1000f:0.0} mm. " +
                                                 "Mixamo 1 knows only the X Bot's T-pose; download clips on the X Bot.");
                    foreach (var r in XBotRest)
                    {
                        U[r.bone].localPosition = new Vector3(r.px, r.py, r.pz);
                        U[r.bone].localRotation = new Quaternion(r.qx, r.qy, r.qz, r.qw);
                    }
                    var uRest = U.ToDictionary(kv => kv.Key, kv => (p: P(mx, kv.Value), r: R(mx, kv.Value)), StringComparer.Ordinal);
                    Quaternion frameU = Frame(uRest["LeftArm"].p, uRest["RightArm"].p, uRest["Hips"].p, uRest["Head"].p);
                    Quaternion frameB = Frame(bind["UpperArm.L"].p, bind["UpperArm.R"].p, bind["Body"].p, bind["Head"].p);
                    Quaternion align = frameB * Quaternion.Inverse(frameU);
                    float hipU = (uRest["LeftUpLeg"].p.y + uRest["RightUpLeg"].p.y) * .5f;
                    Require(hipU > 1e-4f, Path.GetFileName(path) + ": the rest pose's hips are not above the floor.");
                    float ratio = hipB / hipU;
                    var restInverse = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
                    var corrections = new List<string>();
                    foreach (var (u, b) in Map)
                    {
                        Quaternion fix = Quaternion.identity;
                        // Collarbones take Mixamo's turn away from its own rest, not its direction:
                        // the two rigs' collarbones point 37 degrees apart at rest, and copying the
                        // direction would roll the café rig's (and so the city bodies') shoulders
                        // down to Mixamo's. SettleCollarbone then limits how far they drop or pull back.
                        bool collarbone = b.StartsWith("Shoulder.", StringComparison.Ordinal);
                        if (!collarbone && MixamoTip.TryGetValue(u, out string ut) && BeachTip.TryGetValue(b, out string bt)
                            && uRest.ContainsKey(ut) && restPos.ContainsKey(bt))
                        {
                            Vector3 du = align * (uRest[ut].p - uRest[u].p);
                            Vector3 db = restPos[bt] - restPos[b];
                            if (du.sqrMagnitude > 1e-10f && db.sqrMagnitude > 1e-10f) fix = Quaternion.FromToRotation(du, db);
                            float angle = Quaternion.Angle(Quaternion.identity, fix);
                            if (angle > 3f) corrections.Add($"{b} {angle:0}");
                        }
                        restInverse[u] = Quaternion.Inverse(fix * align * uRest[u].r);
                    }
                    string rig = $"Mixamo rig: alignment {Quaternion.Angle(Quaternion.identity, align):0.0} deg, hip joints {hipU:0.000} -> scale {ratio:0.000}; " +
                                 "rest-pose corrections over 3 deg: " + (corrections.Count == 0 ? "none" : string.Join(", ", corrections));
                    if (rig != lastRig) { notes.AppendLine(rig); lastRig = rig; }

                    // Horizontal drift of the hips over the whole clip is removed (the clips are loops in place).
                    Vector3 driftPerSecond = Vector3.zero;
                    clip.SampleAnimation(mx, 0f);
                    Vector3 h0 = align * P(mx, U["Hips"]);
                    clip.SampleAnimation(mx, clip.length);
                    Vector3 h1 = align * P(mx, U["Hips"]);
                    Vector3 drift = h1 - h0; drift.y = 0f;
                    if (drift.magnitude > hipU * .02f && clip.length > .1f) driftPerSecond = drift / clip.length;

                    Vector3 anchor = Vector3.zero;
                    float worstReach = 0f, worstSlack = 0f;
                    int settled = 0, armFixes = 0;

                    void Pose(float time)
                    {
                        clip.SampleAnimation(mx, time);
                        foreach (var kv in rest) { kv.Key.localPosition = kv.Value.localPosition; kv.Key.localRotation = kv.Value.localRotation; }
                        Vector3 moved = align * (P(mx, U["Hips"]) - uRest["Hips"].p) - driftPerSecond * time;
                        Vector3 body = bind["Body"].p + moved * ratio + anchor;
                        foreach (var (u, b) in Map)
                        {
                            Transform bone = B[b];
                            bone.rotation = beach.transform.rotation * (align * R(mx, U[u]) * restInverse[u] * bind[b].r);
                            if (b == "Body") bone.position = beach.transform.TransformPoint(body);
                            else if (b == "Shoulder.L" || b == "Shoulder.R")
                            {
                                float excess = SettleCollarbone(beach.transform, B, bind, restPos, b.Substring(b.Length - 1));
                                if (excess > 0f) { settled++; worstSlack = Mathf.Max(worstSlack, excess); }
                            }
                        }
                        foreach (var (side, s) in Sides)
                        {
                            Vector3 footMoved = align * (P(mx, U[side + "Foot"]) - uRest[side + "Foot"].p) - driftPerSecond * time;
                            Vector3 target = bind["Foot." + s].p + footMoved * ratio + new Vector3(anchor.x, 0f, anchor.z);
                            Vector3 world = beach.transform.TransformPoint(target);
                            SolveLeg(B["UpperLeg." + s], B["LowerLeg." + s], ankle[s], world, beach.transform);
                            Vector3 reached = B["LowerLeg." + s].TransformPoint(ankle[s]);
                            worstReach = Mathf.Max(worstReach, Vector3.Distance(reached, world));
                            B["Foot." + s].position = reached;
                        }
                        if (seated) armFixes += ArmsForward(beach.transform, B);
                    }

                    int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * FrameRate) + 1);
                    string seat = "";
                    if (seated)
                    {
                        // Where this clip's hip joints sit on average, then move them onto the café's seat point.
                        Vector3 sum = Vector3.zero;
                        for (int f = 0; f < frames; f++)
                        {
                            Pose(Mathf.Min(clip.length, f / FrameRate));
                            sum += Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]);
                        }
                        Vector3 natural = sum / frames;
                        anchor = sitData.seatedHip - natural;
                        seat = $", hip joints {V(natural)} moved by {Cm(anchor)} onto the seat point";
                        worstReach = worstSlack = 0f;
                        settled = armFixes = 0;
                    }

                    var curves = paths.Select(_ => new List<Keyframe>[7].Select(__ => new List<Keyframe>()).ToArray()).ToArray();
                    var previous = new Quaternion[paths.Length];
                    for (int f = 0; f < frames; f++)
                    {
                        float time = Mathf.Min(clip.length, f / FrameRate);
                        Pose(time);
                        for (int i = 0; i < paths.Length; i++)
                        {
                            Transform t = paths[i].t;
                            Vector3 p = t.localPosition;
                            Quaternion q = t.localRotation;
                            if (f > 0 && Quaternion.Dot(previous[i], q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                            previous[i] = q;
                            float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                            for (int k = 0; k < 7; k++) curves[i][k].Add(new Keyframe(time, values[k]));
                        }
                    }
                    string name = ClipName(path);
                    var baked = new AnimationClip { name = name, frameRate = FrameRate };
                    var bindings = new List<EditorCurveBinding>();
                    var animationCurves = new List<AnimationCurve>();
                    for (int i = 0; i < paths.Length; i++)
                        for (int k = 0; k < 7; k++)
                        {
                            bindings.Add(EditorCurveBinding.FloatCurve(paths[i].path, typeof(Transform), properties[k]));
                            animationCurves.Add(Smooth(curves[i][k]));
                        }
                    AnimationUtility.SetEditorCurves(baked, bindings.ToArray(), animationCurves.ToArray());
                    baked.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(baked);
                    settings.loopTime = true;
                    settings.loopBlend = false;
                    AnimationUtility.SetAnimationClipSettings(baked, settings);
                    SaveClip(baked, BakedRoot + "/" + group + "/" + name + ".anim");
                    notes.AppendLine($"{group}/{name}: {clip.length:0.00} s, {frames} frames{seat}" +
                                     (driftPerSecond.sqrMagnitude > 0f ? $", hips drift {drift.magnitude / hipU * hipB * 100f:0.0} cm removed" : "") +
                                     $", foot IK miss {worstReach * 1000f:0.0} mm" +
                                     (settled > 0 ? $", collarbones held in {settled} frame-sides (up to {worstSlack:0} deg past the limit)" : "") +
                                     (armFixes > 0 ? $", upper arms kept off the backrest in {armFixes} arm-frames" : ""));
                }
                finally { Object.DestroyImmediate(mx); }
            }
        }
        finally { Object.DestroyImmediate(beach); }
        AssetDatabase.SaveAssets();
        string report = notes.ToString();
        WriteLog("bake.txt", report);
        return "\n" + report;
    }

    // Before anything is baked: does XBotRest's conversion match Unity's own import? Frame 0 of
    // "X Bot@Bored" sampled by Unity against the same frame converted by the table's rules.
    static string CheckConversion(IEnumerable<string> paths)
    {
        string path = paths.FirstOrDefault(p => Path.GetFileName(p).Equals("X Bot@Bored.fbx", StringComparison.OrdinalIgnoreCase));
        if (path == null) return "NOTE: \"X Bot@Bored\" is not among the clips, so the T-pose table's conversion was not checked against Unity's import (only bone lengths are).";
        AnimationClip clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(clip != null && model != null, "Cannot load " + path);
        GameObject mx = Object.Instantiate(model);
        mx.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            foreach (var a in mx.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var U = IndexShort(mx.transform);
            foreach (var b in BoredFirstFrame) Require(U.ContainsKey(b.bone), path + " has no bone " + b.bone);
            clip.SampleAnimation(mx, 0f);
            float worst = BoredFirstFrame.Max(b => Quaternion.Angle(U[b.bone].localRotation, new Quaternion(b.qx, b.qy, b.qz, b.qw)));
            Require(worst < 1f, $"Unity's import of {Path.GetFileName(path)} disagrees with the T-pose table's conversion by {worst:0.0} deg " +
                                $"(first frame, {BoredFirstFrame.Length} bones), so the table can't be trusted. Nothing was baked.");
            return $"T-pose table checked against Unity's import of {Path.GetFileName(path)}: the first frame agrees within {worst:0.00} deg on {BoredFirstFrame.Length} bones.";
        }
        finally { Object.DestroyImmediate(mx); }
    }

    // Keeps a collarbone from lowering or pulling back its shoulder joint more than
    // ShoulderSlack degrees from the café rig's T-pose, measured relative to the chest
    // (so a bow or a lean still carries the shoulders with it). Raising the shoulders
    // (a shrug) and bringing them forward stay as Mixamo has them. Returns how far
    // past the limit the clip went, degrees (0 when it stayed within it).
    static float SettleCollarbone(Transform root, Dictionary<string, Transform> B,
        Dictionary<string, (Vector3 p, Quaternion r)> bind, Dictionary<string, Vector3> restPos, string s)
    {
        Transform collar = B["Shoulder." + s], upper = B["UpperArm." + s], chest = B["Chest"];
        Quaternion chestTurn = Quaternion.Inverse(root.rotation) * chest.rotation * Quaternion.Inverse(bind["Chest"].r);
        Vector3 restDir = (restPos["UpperArm." + s] - restPos["Shoulder." + s]).normalized;
        Vector3 nowWorld = upper.position - collar.position;
        if (nowWorld.sqrMagnitude < 1e-10f || restDir.sqrMagnitude < 1e-10f) return 0f;
        // Into the chest's rest frame, where the T-pose direction is restDir.
        Vector3 now = Quaternion.Inverse(chestTurn) * root.InverseTransformDirection(nowWorld.normalized);

        float restElevation = Mathf.Asin(Mathf.Clamp(restDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float elevation = Mathf.Asin(Mathf.Clamp(now.y, -1f, 1f)) * Mathf.Rad2Deg;
        var flatRest = new Vector2(restDir.x, restDir.z);
        var flatNow = new Vector2(now.x, now.z);
        if (flatRest.sqrMagnitude < 1e-8f || flatNow.sqrMagnitude < 1e-8f) return 0f;
        flatRest.Normalize(); flatNow.Normalize();
        // Backwards is towards -Z for both sides.
        float restForward = Mathf.Asin(Mathf.Clamp(flatRest.y, -1f, 1f)) * Mathf.Rad2Deg;
        float forward = Mathf.Asin(Mathf.Clamp(flatNow.y, -1f, 1f)) * Mathf.Rad2Deg;
        float down = restElevation - elevation - ShoulderSlack;
        float back = restForward - forward - ShoulderSlack;
        if (down <= 0f && back <= 0f) return 0f;

        if (back > 0f) forward = restForward - ShoulderSlack;
        if (down > 0f) elevation = restElevation - ShoulderSlack;
        float sideSign = Mathf.Sign(flatNow.x != 0f ? flatNow.x : flatRest.x);
        float fz = Mathf.Sin(forward * Mathf.Deg2Rad);
        var flat = new Vector3(sideSign * Mathf.Sqrt(Mathf.Max(0f, 1f - fz * fz)), 0f, fz);
        float cosE = Mathf.Cos(elevation * Mathf.Deg2Rad), sinE = Mathf.Sin(elevation * Mathf.Deg2Rad);
        Vector3 wanted = new Vector3(flat.x * cosE, sinE, flat.z * cosE);
        Vector3 wantedWorld = root.TransformDirection(chestTurn * wanted);
        collar.rotation = Quaternion.FromToRotation(nowWorld.normalized, wantedWorld.normalized) * collar.rotation;
        return Mathf.Max(down, back);
    }

    // As NpcSitAnimations: a hanging upper arm that points backwards is swung forward
    // to straight down, keeping the forearm's direction, so the hands slide forward
    // along the thighs instead of the elbows going into the chair.
    static int ArmsForward(Transform root, Dictionary<string, Transform> B)
    {
        int fixes = 0;
        foreach (string s in new[] { "L", "R" })
        {
            Transform upper = B["UpperArm." + s], lower = B["LowerArm." + s];
            Vector3 dir = root.InverseTransformDirection((lower.position - upper.position).normalized);
            if (dir.z >= ElbowForward || dir.y > -.3f) continue;       // already forward, or a gesture
            var wanted = new Vector3(dir.x, 0f, ElbowForward);
            wanted.y = -Mathf.Sqrt(Mathf.Max(0f, 1f - wanted.x * wanted.x - wanted.z * wanted.z));
            Quaternion swing = Quaternion.FromToRotation(dir, wanted.normalized);
            Quaternion forearm = lower.rotation;
            upper.rotation = root.rotation * swing * Quaternion.Inverse(root.rotation) * upper.rotation;
            lower.rotation = forearm;
            fixes++;
        }
        return fixes;
    }

    // Mixamo's own files: a plain generic rig with every key kept, no materials.
    static bool PrepareImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        Require(importer != null, "Missing " + path);
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
        if (importer.animationCompression != ModelImporterAnimationCompression.Off) { importer.animationCompression = ModelImporterAnimationCompression.Off; changed = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        if (changed) importer.SaveAndReimport();
        return changed;
    }

    // "Assets/Art/Mixamo/Standing/X Bot@Head Nod Yes.fbx" -> "Npc Mx Head Nod Yes"
    static string ClipName(string path)
    {
        string file = Path.GetFileNameWithoutExtension(path);
        int at = file.IndexOf('@');
        return ClipPrefix + (at >= 0 ? file.Substring(at + 1) : file).Trim();
    }

    // ------------------------------------------------------------------ photos

    // Which three moments of a clip to photograph: near its start, its most different pose from
    // that, and the pose most different from both (so a quick gesture's peak is not missed).
    const int MomentSamples = 60;
    static readonly Vector3 StreetSpot = new Vector3(40f, 0f, -40f);   // out of the way of the café, as the gait photos
    const float Spacing = 1.15f;
    // Which flat side of the phone mesh is its screen (it faces away from the palm).
    const bool ScreenOnPositiveSide = true;

    public static string Photograph()
    {
        RequireStopped();
        var standing = LoadBaked("Standing");
        var sitting = LoadBaked("Sitting");
        Require(standing.Count + sitting.Count > 0, "Run Mixamo 1 first: nothing baked in " + BakedRoot + ".");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        Require(prefab != null, "Missing " + CustomerPrefab);
        var phone = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefab);
        string folder = Path.Combine(LogRoot, "photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var notes = new StringBuilder();
        notes.AppendLine("Moments: near the start, the pose most different from it, and the pose most different from both (hands and head), as fractions of each clip.");
        if (phone == null) notes.AppendLine("No " + PhonePrefab + ": the phone clips are photographed without a phone.");
        int number = 0;
        foreach (var clip in standing) PhotographStanding(clip, prefab, phone, folder, ++number, notes);
        if (sitting.Count > 0)
        {
            var chairs = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
                .Where(s => s.transform.parent != null && s.Style == TableSeat.SitStyle.Chair && s.SeatPose != s.StandPoint)
                .GroupBy(s => s.transform.parent).OrderByDescending(g => g.Count()).FirstOrDefault();
            Require(chairs != null, "No café chairs in the open scene (open AcesCafeLayoutPlaytest).");
            var table = chairs.OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            var others = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Select(s => s.SeatPose.position).ToArray();
            notes.AppendLine($"Seated clips on {table[0].name} ({table[0].transform.parent.name}).");
            foreach (var clip in sitting) PhotographSeated(clip, prefab, table[0], others, folder, ++number, notes);
        }
        File.WriteAllText(Path.Combine(folder, "photos.txt"), notes.ToString());
        return "Photos: " + folder + "\n" + notes;
    }

    static void PhotographStanding(AnimationClip clip, GameObject prefab, GameObject phone, string folder, int number, StringBuilder notes)
    {
        var actors = new List<GameObject>();
        var props = new List<GameObject>();
        var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
        string label = Label(clip);
        try
        {
            string hand = PhoneHand(clip, out string why);
            float[] moments = PickMoments(clip);
            string look = "";
            for (int m = 0; m < moments.Length; m++)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                actors.Add(actor);
                actor.transform.SetPositionAndRotation(StreetSpot + Vector3.right * (m * Spacing), Quaternion.identity);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null && visual.AppearanceCount > 0) visual.ApplyAppearance(Look(number, visual.AppearanceCount));
                if (visual != null) look = visual.ActiveAppearanceName;
                Sample(actor, clip, moments[m] * clip.length);
                if (visual != null) visual.Follow();
                if (hand != null && phone != null) props.Add(PlacePhone(actor, visual, phone, hand));
                baked.AddRange(PolygonNpcSetup.BakePose(actor));
            }
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            Vector3 centre = StreetSpot + Vector3.right * Spacing + Vector3.up * .95f;
            CafeSecondPassSteps.Capture(Path.Combine(folder, $"{number:00} {label}.png"), centre + new Vector3(1.1f, .35f, 5.6f), centre, 30f, false);
            notes.AppendLine($"{number:00} {label}: standing, {clip.length:0.0} s, {look}, at {Fractions(moments)}" +
                             (hand != null ? $"; phone in the {(hand == "L" ? "left" : "right")} hand ({why})" : ""));
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            PolygonNpcSetup.UnbakePose(baked);
            foreach (var p in props) if (p != null) Object.DestroyImmediate(p);
            foreach (var a in actors) if (a != null) Object.DestroyImmediate(a);
        }
    }

    static void PhotographSeated(AnimationClip clip, GameObject prefab, TableSeat seat, Vector3[] seatPoints, string folder, int number, StringBuilder notes)
    {
        string label = Label(clip);
        float[] moments = PickMoments(clip);
        var line = new StringBuilder($"{number:00} {label}: seated, {clip.length:0.0} s, at {Fractions(moments)}");
        for (int m = 0; m < moments.Length; m++)
        {
            GameObject actor = null;
            var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
            try
            {
                actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                var seating = actor.GetComponent<NpcSeating>();
                Require(seating != null, CustomerPrefab + " has no NpcSeating (run NPC > Sit 2).");
                float floor = seat.StandPoint.position.y;
                seating.Placement(seat, floor, out Vector3 feet, out Quaternion facing);
                actor.transform.SetPositionAndRotation(feet, facing);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null && visual.AppearanceCount > 0) visual.ApplyAppearance(Look(number, visual.AppearanceCount));
                if (visual != null) visual.Seated = true;
                Sample(actor, clip, moments[m] * clip.length);
                if (visual != null) visual.Follow();

                // How high the hips and the lowest mesh point are against the seat and the floor.
                var bones = Index(actor.transform);
                Vector3 hips = (bones["UpperLeg.L"].position + bones["UpperLeg.R"].position) * .5f;
                string bodyHips = "";
                if (visual != null && visual.VisualInstance != null)
                {
                    var city = Index(visual.VisualInstance.transform);
                    if (city.TryGetValue("UpperLeg_L", out Transform l) && city.TryGetValue("UpperLeg_R", out Transform r))
                        bodyHips = $", body hips {(l.position.y + r.position.y) * .5f - seat.SeatPose.position.y:+0.00;-0.00}";
                }
                float lowest = float.MaxValue;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                    var mesh = PolygonNpcSetup.SkinToWorld(skin);
                    foreach (var v in mesh.vertices) lowest = Mathf.Min(lowest, v.y);
                    Object.DestroyImmediate(mesh);
                }
                if (m == 0 && visual != null) line.Append(", " + visual.ActiveAppearanceName);
                line.Append($"; @{moments[m]:0.00}: rig hips {hips.y - seat.SeatPose.position.y:+0.00;-0.00} m above the seat{bodyHips}, lowest point {lowest - floor:+0.00;-0.00} m");

                baked.AddRange(PolygonNpcSetup.BakePose(actor));
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Vector3 a = seat.SeatPose.position;
                Vector3 forward = facing * Vector3.forward;
                Vector3 across = Vector3.Cross(Vector3.up, forward).normalized;
                // From above the table's corner, on whichever side of the chair has more room (farther from the
                // other seats), looking down past the neighbouring chairs at the whole person.
                Vector3 right = (across * 1.5f + forward * 1.3f).normalized, left = (-across * 1.5f + forward * 1.3f).normalized;
                Vector3 side = Room(a + right * 2.3f, a, seatPoints) >= Room(a + left * 2.3f, a, seatPoints) ? right : left;
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"{number:00} {label} {m + 1}.png"),
                    a + side * 2.3f + Vector3.up * 1.45f, a + Vector3.up * .5f + forward * .15f, 40f, true);
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                PolygonNpcSetup.UnbakePose(baked);
                if (actor != null) Object.DestroyImmediate(actor);
            }
        }
        notes.AppendLine(line.ToString());
    }

    // Distance from a camera spot to the nearest other seat (ignoring the one photographed).
    static float Room(Vector3 camera, Vector3 own, Vector3[] seatPoints)
    {
        float best = float.MaxValue;
        foreach (Vector3 p in seatPoints)
        {
            if ((p - own).sqrMagnitude < .01f) continue;
            best = Mathf.Min(best, Vector3.Distance(new Vector3(camera.x, p.y, camera.z), p));
        }
        return best;
    }

    static float[] PickMoments(AnimationClip clip)
    {
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BeachPath);
        if (rigAsset == null || clip.length < .05f) return new[] { .2f, .5f, .8f };
        GameObject rig = Object.Instantiate(rigAsset);
        rig.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            foreach (var a in rig.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var bones = Index(rig.transform);
            string[] parts = { "Wrist.L", "Wrist.R", "LowerArm.L", "LowerArm.R", "Head" };
            var poses = new Vector3[MomentSamples + 1][];
            for (int i = 0; i <= MomentSamples; i++)
            {
                clip.SampleAnimation(rig, clip.length * i / MomentSamples);
                poses[i] = parts.Select(n => rig.transform.InverseTransformPoint(bones[n].position)).ToArray();
            }
            float Distance(int a, int b)
            {
                float d = 0f;
                for (int k = 0; k < parts.Length; k++) d += Vector3.Distance(poses[a][k], poses[b][k]);
                return d;
            }
            int start = Mathf.RoundToInt(MomentSamples * .05f);
            int peak = start, second = start;
            for (int i = 0; i <= MomentSamples; i++) if (Distance(i, start) > Distance(peak, start)) peak = i;
            float best = -1f;
            for (int i = 0; i <= MomentSamples; i++)
            {
                float d = Mathf.Min(Distance(i, start), Distance(i, peak));
                if (d > best) { best = d; second = i; }
            }
            return new[] { start, peak, second }.Distinct().OrderBy(i => i)
                .Concat(new[] { MomentSamples / 2, MomentSamples - start }).Distinct().Take(3).OrderBy(i => i)
                .Select(i => (float)i / MomentSamples).ToArray();
        }
        finally { Object.DestroyImmediate(rig); }
    }

    static string Fractions(float[] moments) => string.Join(", ", moments.Select(m => m.ToString("0.00", CultureInfo.InvariantCulture)));

    // The two phone clips: which hand holds the phone - the one held nearer the head on average.
    static string PhoneHand(AnimationClip clip, out string why)
    {
        why = null;
        string label = Label(clip);
        bool call = label.IndexOf("Cell Phone", StringComparison.OrdinalIgnoreCase) >= 0;
        bool text = label.IndexOf("Texting", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!call && !text) return null;
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BeachPath);
        if (rigAsset == null) return null;
        GameObject rig = Object.Instantiate(rigAsset);
        rig.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            foreach (var a in rig.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var bones = Index(rig.transform);
            var toHead = new float[2];
            var travel = new float[2];
            var last = new Vector3[2];
            const int samples = 36;
            for (int i = 0; i <= samples; i++)
            {
                clip.SampleAnimation(rig, clip.length * i / samples);
                for (int h = 0; h < 2; h++)
                {
                    Vector3 w = bones[h == 0 ? "Wrist.L" : "Wrist.R"].position;
                    toHead[h] += Vector3.Distance(w, bones["Head"].position) / (samples + 1);
                    if (i > 0) travel[h] += Vector3.Distance(w, last[h]);
                    last[h] = w;
                }
            }
            // The phone is in the hand held up nearer the head, on a call and when texting alike (the first
            // photos showed Mixamo's texter holds it up in the hand that also moves more).
            int pick = toHead[0] <= toHead[1] ? 0 : 1;
            why = $"held nearer the head: left {toHead[0]:0.00} m, right {toHead[1]:0.00} m on average" +
                  (text ? $"; hands travel left {travel[0]:0.00} m, right {travel[1]:0.00} m" : "");
            return pick == 0 ? "L" : "R";
        }
        finally { Object.DestroyImmediate(rig); }
    }

    // Lays the phone in the palm: its long side along the fingers, its back on the
    // palm and its screen facing away from it. Placed in the world for one photo.
    static GameObject PlacePhone(GameObject actor, PolygonNpcVisual visual, GameObject prefab, string s)
    {
        var rig = Index(actor.transform);
        Transform wrist = rig["Wrist." + s], middle = rig["Middle2." + s], index = rig["Index2." + s], pinky = rig["Pinky2." + s];
        Vector3 along = (middle.position - wrist.position).normalized;
        Vector3 across = (index.position - pinky.position).normalized;
        Vector3 palm = (s == "R" ? Vector3.Cross(along, across) : Vector3.Cross(across, along)).normalized;
        Transform hand = wrist;
        if (visual != null && visual.VisualInstance != null)
        {
            var city = Index(visual.VisualInstance.transform);
            if (city.TryGetValue("Hand_" + s, out Transform cityHand)) hand = cityHand;
        }
        var phone = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach (Transform part in phone.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.HideAndDontSave;
        var filter = phone.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return phone;
        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 size = bounds.size;
        int longAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        int thinAxis = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
        Quaternion meshToRoot = Quaternion.Inverse(phone.transform.rotation) * filter.transform.rotation;
        Vector3 longLocal = meshToRoot * Axis(longAxis);
        Vector3 screenLocal = meshToRoot * (Axis(thinAxis) * (ScreenOnPositiveSide ? 1f : -1f));
        phone.transform.rotation = Quaternion.LookRotation(along, palm) * Quaternion.Inverse(Quaternion.LookRotation(longLocal, screenLocal));
        float scale = filter.transform.lossyScale.x;
        float reach = Vector3.Distance(wrist.position, middle.position) * .75f;
        Vector3 wantedCentre = hand.position + along * reach + palm * (size[thinAxis] * scale * .5f + .012f * actor.transform.lossyScale.y);
        phone.transform.position += wantedCentre - filter.transform.TransformPoint(bounds.center);
        return phone;
    }

    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    static List<AnimationClip> LoadBaked(string group)
    {
        string folder = BakedRoot + "/" + group;
        if (!AssetDatabase.IsValidFolder(folder)) return new List<AnimationClip>();
        return AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p))
            .Where(c => c != null).ToList();
    }

    static string Label(AnimationClip clip) => clip.name.StartsWith(ClipPrefix, StringComparison.Ordinal) ? clip.name.Substring(ClipPrefix.Length) : clip.name;

    // A spread of looks, so the sheet shows both women and men.
    static int Look(int number, int count) => count <= 0 ? 0 : (number * 7) % count;

    static void Sample(GameObject actor, AnimationClip clip, float time)
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(actor, clip, time);
        AnimationMode.EndSampling();
    }

    // ------------------------------------------------------------------ rig maps

    // Mixamo bone (without its "mixamorig:" prefix) -> café rig bone, parents before children.
    static (string, string)[] BuildMap()
    {
        var map = new List<(string, string)>
        {
            ("Hips", "Body"), ("Spine", "Abdomen"), ("Spine1", "Torso"), ("Spine2", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
        };
        foreach (var (side, s) in Sides)
        {
            map.Add((side + "Shoulder", "Shoulder." + s));
            map.Add((side + "Arm", "UpperArm." + s));
            map.Add((side + "ForeArm", "LowerArm." + s));
            map.Add((side + "Hand", "Wrist." + s));
            foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky" })
                for (int i = 1; i <= 3; i++) map.Add(($"{side}Hand{f}{i}", $"{f}{i + 1}.{s}"));
            for (int i = 1; i <= 3; i++) map.Add(($"{side}HandThumb{i}", $"Thumb{i}.{s}"));
        }
        foreach (var (side, s) in Sides)
        {
            map.Add((side + "UpLeg", "UpperLeg." + s));
            map.Add((side + "Leg", "LowerLeg." + s));
        }
        foreach (var (side, s) in Sides) map.Add((side + "Foot", "Foot." + s));
        return map.ToArray();
    }

    // Each limb bone's direction (towards this child) is matched between the two rest poses.
    static Dictionary<string, string> BuildMixamoTips()
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (side, _) in Sides)
        {
            tips[side + "Shoulder"] = side + "Arm";
            tips[side + "Arm"] = side + "ForeArm";
            tips[side + "ForeArm"] = side + "Hand";
            tips[side + "Hand"] = side + "HandMiddle1";
            tips[side + "UpLeg"] = side + "Leg";
            tips[side + "Leg"] = side + "Foot";
            // No tip for the foot, as in NpcSitAnimations: the two rigs' foot bones point
            // differently at rest (ankle to ball against flat along the sole).
            foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky", "Thumb" })
            {
                tips[$"{side}Hand{f}1"] = $"{side}Hand{f}2";
                tips[$"{side}Hand{f}2"] = $"{side}Hand{f}3";
            }
        }
        return tips;
    }

    static Dictionary<string, string> BuildBeachTips()
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string s in new[] { "L", "R" })
        {
            tips["Shoulder." + s] = "UpperArm." + s;
            tips["UpperArm." + s] = "LowerArm." + s;
            tips["LowerArm." + s] = "Wrist." + s;
            tips["Wrist." + s] = "Middle2." + s;
            tips["UpperLeg." + s] = "LowerLeg." + s;
            tips["LowerLeg." + s] = "Foot." + s;
            foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky" })
            {
                tips[$"{f}2.{s}"] = $"{f}3.{s}";
                tips[$"{f}3.{s}"] = $"{f}4.{s}";
            }
            tips["Thumb1." + s] = "Thumb2." + s;
            tips["Thumb2." + s] = "Thumb3." + s;
        }
        return tips;
    }

    // ------------------------------------------------------------------ helpers (as NpcGaitAnimations)

    static void SolveLeg(Transform upper, Transform lower, Vector3 ankleLocal, Vector3 target, Transform root)
    {
        Vector3 a = upper.position, b = lower.position, c = lower.TransformPoint(ankleLocal);
        float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
        if (l1 < 1e-5f || l2 < 1e-5f) return;
        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        Vector3 n = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : (c - a).normalized;
        Vector3 pole = (b - a) - Vector3.Dot(b - a, n) * n;
        if (pole.sqrMagnitude < 1e-10f) pole = root.forward - Vector3.Dot(root.forward, n) * n;
        pole.Normalize();
        float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 knee = a + n * x + pole * h;
        upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
        Vector3 ankleNow = lower.TransformPoint(ankleLocal);
        Vector3 end = a + n * d;
        lower.rotation = Quaternion.FromToRotation(ankleNow - lower.position, end - lower.position) * lower.rotation;
    }

    static AnimationCurve Smooth(List<Keyframe> keys)
    {
        var k = keys.ToArray();
        for (int i = 0; i < k.Length; i++)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(k.Length - 1, i + 1);
            float dt = k[b].time - k[a].time;
            float slope = dt > 1e-6f ? (k[b].value - k[a].value) / dt : 0f;
            k[i].inTangent = k[i].outTangent = slope;
        }
        return new AnimationCurve(k);
    }

    static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        EditorUtility.CopySerialized(clip, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(clip);
        return existing;
    }

    static Quaternion Frame(Vector3 leftArm, Vector3 rightArm, Vector3 pelvis, Vector3 head)
    {
        Vector3 right = (rightArm - leftArm).normalized;
        Vector3 up = head - pelvis;
        up = (up - Vector3.Dot(up, right) * right).normalized;
        return Quaternion.LookRotation(Vector3.Cross(right, up), up);
    }

    static Dictionary<string, Transform> Index(Transform root)
    {
        var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;
        return map;
    }

    // Mixamo bones by name without the namespace ("mixamorig:LeftArm" -> "LeftArm").
    static Dictionary<string, Transform> IndexShort(Transform root)
    {
        var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            string n = t.name;
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            if (!map.ContainsKey(n)) map[n] = t;
        }
        return map;
    }

    static Vector3 P(GameObject root, Transform t) => root.transform.InverseTransformPoint(t.position);
    static Quaternion R(GameObject root, Transform t) => Quaternion.Inverse(root.transform.rotation) * t.rotation;
    static Vector3 Mid(GameObject root, Transform a, Transform b) => (P(root, a) + P(root, b)) * .5f;
    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
    static string Cm(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:+0;-0;0}, {1:+0;-0;0}, {2:+0;-0;0}) cm", v.x * 100f, v.y * 100f, v.z * 100f);

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcMixamo"));

    static void WriteLog(string file, string text)
    {
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(Path.Combine(LogRoot, file), text);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void RequireStopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
