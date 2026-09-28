#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
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
//    with the hips, and the resting foot is raised onto the height the café's
//    own sit clip rests its ankles at (NpcSitData.seatedFeet; Mixamo's came out
//    5-10 cm under our floor on the city bodies), no foot going lower than that
//    (27 Sept, Step B). Mixamo's seated hips sit anywhere from
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
// WIRE (Mixamo 3, Step B, 27 Sept 2026). Mansoor chose "32 clips + fingers + ear
// fix" and "Sofas only, drop Seated Idle". The clips play as whole-body beats
// through NpcBeats (see there for when). This step builds what it needs:
//  * the customer animator (tracked) gets a second layer, "Beats", weight 0,
//    with one state per clip. Each state holds an EMPTY placeholder clip
//    ("Beat slot - Thankful") kept inside the animator asset, so the public
//    repository never contains Mixamo's motion;
//  * NpcBeatLibrary (git-ignored, in Assets/Art/Mixamo/Baked/Resources) says which
//    baked clip fills which placeholder, which hand holds the phone in the two
//    phone clips, and which phone (POLYGON City's smartphone); at run time a
//    shared override controller swaps the clips in;
//  * the Customer and Patron prefabs get the NpcBeats component.
// "Seated Idle" is left out (Mansoor, 27 Sept). A copy of the project without the
// Mixamo folder keeps the layer and the placeholders but no library: NpcBeats does
// nothing there and everyone uses the café's own clips.
//
// PHOTOS OF THE WIRING (Mixamo 4): before and after pictures of the fingers and of
// the hand at the face (the phone call), the sofa-only clips on a sofa, and the
// café's own clips with fingers, for Mansoor to judge before he plays.
//
// Menu: Fixit Fidget > NPC > Mixamo 1 (bake), Mixamo 2 (photos), Mixamo 3 (wire),
// Mixamo 4 (photos of the wiring). Re-running is safe.
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
                    // Seated clips: how far the feet are raised so the resting foot stands where the
                    // café's own sit clip puts its ankles (NpcSitData.seatedFeet), and from then on
                    // no foot goes lower than that. Mixamo's seated feet came out 5-10 cm under our
                    // floor on the city bodies (Mixamo 2 photos; Sit 4 and Mixamo 5 in play).
                    float feetLift = 0f;
                    bool feetFitted = false;
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
                            Vector3 target = bind["Foot." + s].p + footMoved * ratio + new Vector3(anchor.x, feetLift, anchor.z);
                            if (feetFitted) target.y = Mathf.Max(target.y, sitData.seatedFeet.y - SeatedFootSlack);
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
                        // Where this clip's hip joints sit on average, then move them onto the café's seat point;
                        // and how high its lower ankle rests, then raise (or lower) the feet onto the café's own.
                        Vector3 sum = Vector3.zero;
                        float lowerAnkle = 0f;
                        for (int f = 0; f < frames; f++)
                        {
                            Pose(Mathf.Min(clip.length, f / FrameRate));
                            sum += Mid(beach, B["UpperLeg.L"], B["UpperLeg.R"]);
                            lowerAnkle += Mathf.Min(beach.transform.InverseTransformPoint(B["Foot.L"].position).y,
                                                    beach.transform.InverseTransformPoint(B["Foot.R"].position).y);
                        }
                        Vector3 natural = sum / frames;
                        anchor = sitData.seatedHip - natural;
                        feetLift = Mathf.Clamp(sitData.seatedFeet.y - lowerAnkle / frames, -.15f, .15f);
                        feetFitted = true;
                        seat = $", hip joints {V(natural)} moved by {Cm(anchor)} onto the seat point, feet {(feetLift >= 0f ? "raised" : "lowered")} {Mathf.Abs(feetLift) * 100f:0.0} cm onto the café's resting height";
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

    // ------------------------------------------------------------------ wire (Step B)

    const string ControllerPath = "Assets/CustomerAnimator(.controller";
    const string PatronPrefab = "Assets/AssetsPrefabs/Patron.prefab";
    public const string LibraryPath = BakedRoot + "/Resources/" + NpcBeatLibrary.ResourceName + ".asset";
    const string LoungeGroup = "Lounge seats (pass 2)";
    // Mansoor, 27 Sept: "Seated Idle is dropped, since its hands are made for a table closer than ours."
    static readonly string[] Dropped = { "Seated Idle" };

    [MenuItem(Menu + "Mixamo 3 - Wire the clip beats (animator layer, library, prefabs)")]
    static void WireMenu() => Run("Wire", Wire);

    [MenuItem(Menu + "Mixamo 4 - Photograph the wiring (fingers, phone call, sofa)")]
    static void WirePhotoMenu() => Run("Wiring photos", PhotographWiring);

    public static string Wire()
    {
        RequireStopped();
        var notes = new StringBuilder();
        var baked = Groups.SelectMany(g => LoadBaked(g).Select(c => (group: g, clip: c))).ToList();
        Require(baked.Count > 0, "Run Mixamo 1 first: nothing baked in " + BakedRoot + ". That folder is git-ignored, so it only " +
                                 "exists on the computer the clips were downloaded to.");
        var wired = baked.Where(b => !Dropped.Contains(Label(b.clip))).ToList();
        notes.AppendLine($"{wired.Count} clips wired ({wired.Count(w => w.group == "Standing")} standing, {wired.Count(w => w.group == "Sitting")} seated); " +
                         $"left out: {string.Join(", ", baked.Where(b => Dropped.Contains(Label(b.clip))).Select(b => Label(b.clip)))}.");

        // 1. The animator: empty placeholder clips inside the (tracked) controller asset,
        //    and the Beats layer with one state per clip.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null, "Missing " + ControllerPath);
        var existing = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<AnimationClip>()
            .Where(c => c.name.StartsWith(NpcBeatLibrary.SlotPrefix, StringComparison.Ordinal))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
        var slots = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        int newSlots = 0;
        foreach (var (_, clip) in wired)
        {
            string label = Label(clip);
            if (!existing.TryGetValue(NpcBeatLibrary.SlotPrefix + label, out AnimationClip slot))
            {
                slot = new AnimationClip { name = NpcBeatLibrary.SlotPrefix + label, frameRate = FrameRate };
                AssetDatabase.AddObjectToAsset(slot, controller);
                newSlots++;
            }
            slots[label] = slot;
        }
        int removedSlots = 0;
        foreach (var kv in existing)
            if (!slots.ContainsValue(kv.Value)) { AssetDatabase.RemoveObjectFromAsset(kv.Value); Object.DestroyImmediate(kv.Value, true); removedSlots++; }

        int layerIndex = Array.FindIndex(controller.layers, l => l.name == NpcBeatLibrary.LayerName);
        bool newLayer = layerIndex < 0;
        if (newLayer)
        {
            controller.AddLayer(NpcBeatLibrary.LayerName);
            layerIndex = controller.layers.Length - 1;
        }
        var layers = controller.layers;
        layers[layerIndex].defaultWeight = 0f;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layerIndex].avatarMask = null;
        layers[layerIndex].iKPass = false;
        layers[layerIndex].syncedLayerIndex = -1;
        controller.layers = layers;
        AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;
        AnimatorState rest = FindState(machine, NpcBeatLibrary.RestState) ?? machine.AddState(NpcBeatLibrary.RestState, new Vector3(300f, 40f));
        rest.motion = null;
        rest.writeDefaultValues = false;
        machine.defaultState = rest;
        int standingRow = 0, seatedRow = 0;
        foreach (var (group, clip) in wired)
        {
            string label = Label(clip);
            bool seated = group == "Sitting";
            var position = new Vector3(seated ? 620f : 300f, 120f + 50f * (seated ? seatedRow++ : standingRow++));
            AnimatorState state = FindState(machine, label) ?? machine.AddState(label, position);
            state.motion = slots[label];
            state.writeDefaultValues = false;
            state.speed = 1f;
            foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
        }
        var names = new HashSet<string>(wired.Select(w => Label(w.clip)), StringComparer.Ordinal);
        foreach (var child in machine.states.ToArray())
            if (child.state != rest && !names.Contains(child.state.name)) machine.RemoveState(child.state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        notes.AppendLine($"Animator: layer '{NpcBeatLibrary.LayerName}' {(newLayer ? "added" : "updated")} (index {layerIndex}, weight 0, override), " +
                         $"{wired.Count} beat states + '{NpcBeatLibrary.RestState}'; placeholders: {newSlots} new, {slots.Count - newSlots} kept, {removedSlots} removed. " +
                         "The placeholders are empty clips inside the animator asset: no Mixamo motion is in the tracked file.");

        // 2. The library (git-ignored).
        EnsureFolder(BakedRoot + "/Resources");
        var library = AssetDatabase.LoadAssetAtPath<NpcBeatLibrary>(LibraryPath);
        bool created = library == null;
        if (created)
        {
            library = ScriptableObject.CreateInstance<NpcBeatLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        var entries = new List<NpcBeatLibrary.Entry>();
        foreach (var (group, clip) in wired)
        {
            string hand = PhoneHand(clip, out string why);
            entries.Add(new NpcBeatLibrary.Entry
            {
                name = Label(clip),
                clip = clip,
                seated = group == "Sitting",
                phoneHand = hand == "L" ? NpcBeatLibrary.Hand.Left : hand == "R" ? NpcBeatLibrary.Hand.Right : NpcBeatLibrary.Hand.None,
            });
            if (hand != null) notes.AppendLine($"  {Label(clip)}: phone in the {(hand == "L" ? "left" : "right")} hand ({why}).");
        }
        library.entries = entries.ToArray();
        library.phone = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefab);
        // Every city look's ears (for the phone on a call), measured on its head mesh.
        var ears = new List<NpcBeatLibrary.Ears>();
        string looks = DescribeLooks(ears);
        library.ears = ears.ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        notes.AppendLine($"Library: {LibraryPath} {(created ? "created" : "updated")}, {entries.Count} entries, phone " +
                         (library.phone != null ? library.phone.name : "MISSING (" + PhonePrefab + ")") + ". Git-ignored with the Mixamo folder.");
        var unusedByCode = entries.Select(e => e.name).Where(n => !BeatNamesInCode.Contains(n)).ToList();
        var missingForCode = BeatNamesInCode.Where(n => !entries.Any(e => e.name == n)).ToList();
        if (missingForCode.Count > 0) notes.AppendLine("NOTE: NpcBeats uses clips the library does not have: " + string.Join(", ", missingForCode));
        notes.AppendLine("Wired but not used yet (waiting for lean spots, Step C): " + (unusedByCode.Count > 0 ? string.Join(", ", unusedByCode) : "none"));

        // 3. The prefabs get NpcBeats.
        foreach (string path in new[] { CustomerPrefab, PatronPrefab })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<NpcBeats>() != null) { notes.AppendLine(Path.GetFileName(path) + ": NpcBeats already there"); continue; }
                root.AddComponent<NpcBeats>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.AppendLine(Path.GetFileName(path) + ": NpcBeats added");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 4. Seats: where the sofa-only and table-only clips may play.
        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include);
        if (seats.Length == 0) notes.AppendLine("Seats: none in the open scene (open AcesCafeLayoutPlaytest to see which have a table in front).");
        else
        {
            var noTable = seats.Where(s => !NpcBeats.TableInFront(s)).ToList();
            var lounge = seats.Where(s => s.transform.parent != null && s.transform.parent.name == LoungeGroup).ToList();
            notes.AppendLine($"Seats: {seats.Length}; {seats.Length - noTable.Count} with a table in front (Tapping Fingers there), " +
                             $"{noTable.Count} without (Sitting Laughing and Sitting Thumbs Up there): " +
                             string.Join(", ", noTable.Select(s => (s.transform.parent != null ? s.transform.parent.name + "/" : "") + s.name)));
            bool matches = lounge.Count == noTable.Count && lounge.All(noTable.Contains);
            notes.AppendLine(matches ? $"  That is exactly the {lounge.Count} lounge seats (sofas and tub chair)."
                                     : $"  NOTE: the lounge group has {lounge.Count} seats; the table test differs from it.");
        }

        // 5. Fingers and ears on every city look.
        notes.AppendLine(looks);
        string report = notes.ToString();
        WriteLog("wire.txt", report);
        return "\n" + report;
    }

    // The clips NpcBeats picks by name (kept here so the wiring can say if one is missing).
    static readonly string[] BeatNamesInCode =
    {
        NpcBeats.Clip.BreathingIdle, NpcBeats.Clip.WeightShift, NpcBeats.Clip.LookingAround, NpcBeats.Clip.Bored, NpcBeats.Clip.Thinking,
        NpcBeats.Clip.HoldingIdle, NpcBeats.Clip.Texting, NpcBeats.Clip.PhoneCall, NpcBeats.Clip.HeadShake, NpcBeats.Clip.Pouting,
        NpcBeats.Clip.AngryGesture, NpcBeats.Clip.Dismissing, NpcBeats.Clip.Disappointed, NpcBeats.Clip.Shrugging, NpcBeats.Clip.Thankful,
        NpcBeats.Clip.RelievedSigh, NpcBeats.Clip.HappyIdle, NpcBeats.Clip.HappyHand, NpcBeats.Clip.Excited, NpcBeats.Clip.Laughing,
        NpcBeats.Clip.Greeting, NpcBeats.Clip.NodYes,
        NpcBeats.Clip.SitBreathing, NpcBeats.Clip.SitHandsOnThighs, NpcBeats.Clip.SitLookAround, NpcBeats.Clip.SitTalking,
        NpcBeats.Clip.SitTalkShort, NpcBeats.Clip.SitLaughing, NpcBeats.Clip.SitImpatient, NpcBeats.Clip.SitTapping,
        NpcBeats.Clip.SitAngry, NpcBeats.Clip.SitThumbsUp, NpcBeats.Clip.Beckoning,
    };

    // Where a city look's ears are, in its head bone's own space. On the head mesh (the vertices
    // the head bone mostly moves, in the T-pose): at the height of the Eyes bone, the furthest-out
    // points on each side, halfway between the front and the back of the head at that height.
    static bool MeasureEars(PolygonNpcVisual visual, out Vector3 left, out Vector3 right, out string note)
    {
        left = right = Vector3.zero;
        Transform body = visual.VisualInstance.transform;
        var bones = Index(body);
        if (!bones.TryGetValue("Head", out Transform head)) { note = "not measured (no Head bone)"; return false; }
        bones.TryGetValue("Eyes", out Transform eyesBone);
        Matrix4x4? headBind = null;
        Vector3? eyes = null;
        var points = new List<Vector3>();
        foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = skin.sharedMesh;
            if (mesh == null) continue;
            Transform[] skinBones = skin.bones;
            Matrix4x4[] poses = mesh.bindposes;
            int h = Array.IndexOf(skinBones, head);
            if (h < 0 || h >= poses.Length) continue;
            Matrix4x4 toRoot = body.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            if (headBind == null) headBind = toRoot * poses[h].inverse;
            int e = eyesBone != null ? Array.IndexOf(skinBones, eyesBone) : -1;
            if (eyes == null && e >= 0 && e < poses.Length) eyes = (toRoot * poses[e].inverse).GetColumn(3);
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            if (weights.Length != vertices.Length) continue;
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight w = weights[i];
                float onHead = (w.boneIndex0 == h ? w.weight0 : 0f) + (w.boneIndex1 == h ? w.weight1 : 0f)
                             + (w.boneIndex2 == h ? w.weight2 : 0f) + (w.boneIndex3 == h ? w.weight3 : 0f);
                if (onHead >= .5f) points.Add(toRoot.MultiplyPoint3x4(vertices[i]));
            }
        }
        if (headBind == null || points.Count < 20) { note = $"not measured ({points.Count} head vertices)"; return false; }
        Vector3 headAt = headBind.Value.GetColumn(3);
        float top = points.Max(p => p.y);
        float eyeY = eyes.HasValue ? eyes.Value.y : headAt.y + (top - headAt.y) * .4f;
        float band = Mathf.Max(.015f, (top - headAt.y) * .06f);
        var ring = points.Where(p => Mathf.Abs(p.y - eyeY) < band).ToList();
        if (ring.Count < 8) { note = $"not measured ({ring.Count} vertices at eye height)"; return false; }
        float minX = ring.Min(p => p.x), maxX = ring.Max(p => p.x), z = (ring.Min(p => p.z) + ring.Max(p => p.z)) * .5f;
        Matrix4x4 intoHead = headBind.Value.inverse;
        right = intoHead.MultiplyPoint3x4(new Vector3(maxX, eyeY, z));
        left = intoHead.MultiplyPoint3x4(new Vector3(minX, eyeY, z));
        float scale = body.localScale.x;
        note = $"at eye height ({(eyes.HasValue ? "Eyes bone" : "estimated")}), {(eyeY - headAt.y) * scale * 100f:0} cm above the head bone, " +
               $"head {(maxX - minX) * scale * 100f:0} cm wide there";
        return true;
    }

    // Left hand, T-pose: how far each finger bone is from the wrist (hand) bone, in the actor's
    // units, for the rig and for this body (scaled as it is fitted to the rig).
    static string FingerReach(GameObject actor, PolygonNpcVisual visual)
    {
        Transform body = visual.VisualInstance.transform;
        var rigSkins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => !r.transform.IsChildOf(body)).ToList();
        var bodySkins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();
        var rig = BindPositions(actor.transform, rigSkins);
        var own = BindPositions(body, bodySkins);
        float scale = body.localScale.x;
        string Reach(Dictionary<string, Vector3> bones, string hand, IEnumerable<string> names, float k) =>
            bones.TryGetValue(hand, out Vector3 h)
                ? string.Join(" ", names.Where(bones.ContainsKey).Select(n => $"{n} {Vector3.Distance(bones[n], h) * k * 100f:0.0}"))
                : "(no " + hand + ")";
        var rigNames = new[] { "Thumb1.L", "Thumb2.L", "Thumb3.L", "Index1.L", "Index2.L", "Index3.L", "Index4.L", "Middle1.L", "Middle2.L", "Middle3.L", "Middle4.L" };
        var bodyNames = new[] { "Thumb_01", "Thumb_02", "Thumb_03", "IndexFinger_01", "IndexFinger_02", "IndexFinger_03", "IndexFinger_04",
                                "Finger_01", "Finger_02", "Finger_03", "Finger_04" };
        var bodyNamesL = bodyNames.Select(n => n + "_L");
        return "left-hand bones from the wrist, cm: rig " + Reach(rig, "Wrist.L", rigNames, 1f)
             + " | body " + Reach(own, "Hand_L", own.ContainsKey("Thumb_01") ? bodyNames : bodyNamesL, scale);
    }

    // Every skinned bone's T-pose position in <root>'s space (first skinned mesh that uses it).
    static Dictionary<string, Vector3> BindPositions(Transform root, List<SkinnedMeshRenderer> skins)
    {
        var map = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var skin in skins)
        {
            if (skin == null || skin.sharedMesh == null) continue;
            Transform[] bones = skin.bones;
            Matrix4x4[] poses = skin.sharedMesh.bindposes;
            for (int i = 0; i < bones.Length && i < poses.Length; i++)
            {
                if (bones[i] == null || map.ContainsKey(bones[i].name)) continue;
                map[bones[i].name] = (root.worldToLocalMatrix * skin.transform.localToWorldMatrix * poses[i].inverse).GetColumn(3);
            }
        }
        return map;
    }

    // A bone's children a few levels down, as "A(B(C) D)".
    static string Tree(Transform t, int depth)
    {
        if (depth <= 0 || t.childCount == 0) return "";
        var parts = new List<string>();
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            string inner = Tree(c, depth - 1);
            parts.Add(c.name + (inner.Length > 0 ? "(" + inner + ")" : ""));
        }
        return string.Join(" ", parts);
    }

    static AnimatorState FindState(AnimatorStateMachine machine, string name) =>
        machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    // Every city look on the customer prefab: how many finger bones it copies, and for a look
    // that copies fewer than 18, what its hands hold (so a different naming can be mapped).
    static string DescribeLooks(List<NpcBeatLibrary.Ears> ears)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        if (prefab == null) return "Looks: no " + CustomerPrefab;
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var visual = actor.GetComponent<PolygonNpcVisual>();
            if (visual == null || visual.AppearanceCount == 0) return "Looks: the customer prefab has no city looks.";
            var lines = new List<string>();
            var fingers = new List<int>();
            var described = new HashSet<string>();
            for (int i = 0; i < visual.AppearanceCount; i++)
            {
                if (!visual.ApplyAppearance(i)) { lines.Add($"{i}: could not be applied"); continue; }
                fingers.Add(visual.FingerCount);
                string line = $"{visual.ActiveAppearanceName}: {visual.FingerCount} finger bones";
                if (MeasureEars(visual, out Vector3 earL, out Vector3 earR, out string earNote))
                    ears.Add(new NpcBeatLibrary.Ears { look = visual.ActiveAppearanceName, left = earL, right = earR });
                line += "; ears " + earNote;
                // What the hands hold, once per kind of look that copies fewer than all 22;
                // and once per kind, how far along the hand each finger bone sits (to check which
                // rig bone each one follows).
                string family = visual.ActiveAppearanceName.Split('_').Take(3).Aggregate((a, b) => a + "_" + b);
                if (described.Add("reach " + family.Split('_')[0]))
                {
                    line += "; " + FingerReach(actor, visual);
                    var own = Index(visual.VisualInstance.transform);
                    if (own.TryGetValue("Head", out Transform bodyHead)) line += "; under its Head: " + Tree(bodyHead, 2);
                }
                if (visual.FingerCount < 22 && described.Add(family + visual.FingerCount))
                {
                    var bones = Index(visual.VisualInstance.transform);
                    foreach (string hand in new[] { "Hand_L", "Hand_R" })
                        line += $"; under {hand}: " + (bones.TryGetValue(hand, out Transform h) ? Tree(h, 3) : "(no such bone)");
                    if (bones.TryGetValue("Hand_L", out Transform left))
                    {
                        var skin = visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        var skinned = skin.SelectMany(r => r.bones).Where(b => b != null && b != left && b.IsChildOf(left)).Select(b => b.name).Distinct().ToList();
                        line += $"; skinned bones under Hand_L: {skinned.Count} ({string.Join(", ", skinned.Take(12))})";
                    }
                }
                lines.Add(line);
            }
            visual.RemoveAppearance();
            var rig = Index(actor.transform);
            string rigHands = string.Join("; ", new[] { "Wrist.L", "Wrist.R", "Head" }.Select(w => w + ": " + (rig.TryGetValue(w, out Transform t) ? Tree(t, 3) : "(none)")));
            return $"Looks: {fingers.Count} city looks; finger bones copied {fingers.Min()}-{fingers.Max()} (22 = thumb, index finger and the three-finger chain, " +
                   $"joint for joint on both hands).\n  Rig hands: {rigHands}\n  " + string.Join("\n  ", lines);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    // ------------------------------------------------------------------ photos of the wiring

    static readonly Vector3 WiringSpot = new Vector3(40f, 0f, -44f);   // beside the Step A photo spot, out of the café

    public static string PhotographWiring()
    {
        RequireStopped();
        var library = AssetDatabase.LoadAssetAtPath<NpcBeatLibrary>(LibraryPath);
        Require(library != null && library.entries.Length > 0, "Run Mixamo 3 first: " + LibraryPath + " is missing.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        Require(prefab != null, "Missing " + CustomerPrefab);
        string folder = Path.Combine(LogRoot, "wiring-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var notes = new StringBuilder();
        notes.AppendLine("Each pair: left BEFORE (open hands, copied turns only), right AFTER (fingers copied, hands at the face placed on the face).");
        AnimationClip Beat(string n) => library.Find(n)?.clip;
        int shot = 0;

        // Standing clips where the hands matter.
        foreach (var (clipName, moments) in new (string, float[])[]
                 {
                     (NpcBeats.Clip.PhoneCall, new[] { .2f, .5f, .8f }),
                     (NpcBeats.Clip.Texting, new[] { .5f }),
                     (NpcBeats.Clip.Greeting, null),
                     (NpcBeats.Clip.Thinking, null),
                     (NpcBeats.Clip.Dismissing, null),
                     (NpcBeats.Clip.Thankful, null),
                     (NpcBeats.Clip.Excited, null),
                     (NpcBeats.Clip.HeadShake, null),
                 })
        {
            AnimationClip clip = Beat(clipName);
            if (clip == null) { notes.AppendLine(clipName + ": not in the library"); continue; }
            float[] at = moments ?? PickMoments(clip).Skip(1).Take(1).ToArray();
            foreach (float m in at)
                notes.AppendLine(PhotographPair(clip, clipName, m, prefab, library, folder, ++shot, clipName == NpcBeats.Clip.PhoneCall));
        }

        // The café's own clips with fingers (every NPC shows this change).
        foreach (string own in new[] { "Npc Idle Relaxed", "Npc Walk Brisk", "Npc Standing Talk" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/NpcAnimations/" + own + ".anim");
            if (clip != null) notes.AppendLine(PhotographPair(clip, own, .35f, prefab, library, folder, ++shot, false));
        }
        var beachIdle = AssetDatabase.LoadAllAssetsAtPath(BeachPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "CharacterArmature|Idle");
        if (beachIdle != null) notes.AppendLine(PhotographPair(beachIdle, "CharacterArmature Idle", .5f, prefab, library, folder, ++shot, false));

        // Seated: the sofa-only clips on a lounge seat, and table clips at a café chair.
        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude);
        TableSeat sofa = seats.Where(s => !NpcBeats.TableInFront(s)).OrderBy(s => s.Style == TableSeat.SitStyle.Bench ? 0 : 1).ThenBy(s => s.name).FirstOrDefault();
        TableSeat chair = seats.Where(s => NpcBeats.TableInFront(s) && s.Style == TableSeat.SitStyle.Chair && s.SeatPose != s.StandPoint)
                               .OrderBy(s => s.name, StringComparer.Ordinal).FirstOrDefault();
        if (sofa == null || chair == null) notes.AppendLine("Seated photos skipped: open AcesCafeLayoutPlaytest (no lounge seat or café chair found).");
        else
        {
            foreach (string clipName in new[] { NpcBeats.Clip.SitLaughing, NpcBeats.Clip.SitThumbsUp, NpcBeats.Clip.SitTalking, NpcBeats.Clip.SitHandsOnThighs })
            {
                AnimationClip clip = Beat(clipName);
                if (clip != null) notes.AppendLine(PhotographSeatedWiring(clip, clipName, prefab, sofa, folder, ++shot, true));
            }
            foreach (string clipName in new[] { NpcBeats.Clip.SitTapping, NpcBeats.Clip.Beckoning, NpcBeats.Clip.SitTalkShort, NpcBeats.Clip.SitImpatient })
            {
                AnimationClip clip = Beat(clipName);
                if (clip != null) notes.AppendLine(PhotographSeatedWiring(clip, clipName, prefab, chair, folder, ++shot, false));
            }
        }
        File.WriteAllText(Path.Combine(folder, "photos.txt"), notes.ToString());
        return "Wiring photos: " + folder + "\n" + notes;
    }

    // Two customers side by side in the same city look and pose: without and with the Step B
    // fingers and hands-at-the-face, close up on the upper body (the head, for the phone call).
    static string PhotographPair(AnimationClip clip, string label, float moment, GameObject prefab, NpcBeatLibrary library, string folder, int number, bool faceCloseUp)
    {
        var actors = new List<GameObject>();
        var props = new List<GameObject>();
        var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
        string line = $"{number:00} {label} at {moment:0.00}";
        try
        {
            var entry = library.entries.FirstOrDefault(e => e.clip == clip);
            string hand = entry == null || entry.phoneHand == NpcBeatLibrary.Hand.None ? null : entry.phoneHand == NpcBeatLibrary.Hand.Left ? "L" : "R";
            var faces = new float[3];
            float fromEar = -1f;
            int count = faceCloseUp ? 3 : 2;
            for (int k = 0; k < count; k++)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                actors.Add(actor);
                // The camera looks back at them from the front, so -X is its right: BEFORE left, AFTER right.
                actor.transform.SetPositionAndRotation(WiringSpot + Vector3.left * (k * .95f), Quaternion.identity);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                // The third one (the call only) is the café rig itself, in its own body (as Grace is).
                if (k == 2) visual = null;
                if (visual != null)
                {
                    var so = new SerializedObject(visual);
                    so.FindProperty("copyFingers").boolValue = k == 1;
                    so.FindProperty("handsToFace").boolValue = k == 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if (visual.AppearanceCount > 0) visual.ApplyAppearance(Look(number, visual.AppearanceCount));
                }
                Sample(actor, clip, moment * clip.length);
                if (visual != null)
                {
                    visual.Follow();
                    if (k == 1) line += $", {visual.ActiveAppearanceName} ({visual.FingerCount} finger bones)";
                }
                if (hand != null && library.phone != null)
                {
                    GameObject phoneGo = PlacePhone(actor, visual, library.phone, hand);
                    props.Add(phoneGo);
                    // On the call, the AFTER body holds the phone against its own ear (as NpcBeats does in play).
                    Transform cityHand = visual != null ? visual.CityHand(hand == "L") : null;
                    var filter = phoneGo.GetComponentInChildren<MeshFilter>();
                    if (label == NpcBeats.Clip.PhoneCall && k == 1 && cityHand != null && filter != null && filter.sharedMesh != null
                        && library.TryGetEars(visual.ActiveAppearanceName, out Vector3 earL, out Vector3 earR))
                    {
                        phoneGo.transform.SetParent(cityHand, true);
                        Bounds b = filter.sharedMesh.bounds;
                        float thin = Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z)) * filter.transform.lossyScale.x;
                        float length = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * filter.transform.lossyScale.x;
                        // The speaker, as NpcBeats: most of the way from the centre to the end towards the fingertips.
                        var rigBones = Index(actor.transform);
                        Vector3 along = (rigBones["Middle2." + hand].position - rigBones["Wrist." + hand].position).normalized;
                        Vector3 speaker = cityHand.InverseTransformPoint(filter.transform.TransformPoint(b.center) + along * (length * .5f * .65f));
                        visual.HoldPhoneToEar(hand == "L", 1f, hand == "L" ? earL : earR, speaker, thin * .5f);
                        visual.Follow();
                        faces[k] = Mathf.Max(visual.HandsAtFace.x, visual.HandsAtFace.y);
                        fromEar = visual.PhoneFromEar;
                    }
                }
                baked.AddRange(PolygonNpcSetup.BakePose(actor));
            }
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (faceCloseUp)
            {
                var head = Index(actors[0].transform)["Head"];
                Vector3 centre = WiringSpot + Vector3.left * .95f + Vector3.up * (head.position.y + .02f);
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"{number:00} {label} {moment:0.00}.png"), centre + new Vector3(.2f, .05f, 2.8f), centre, 36f, false);
                line += $"; left to right: BEFORE, AFTER (phone at the ear: {faces[1]:0.00}, phone {(fromEar >= 0f ? $"{fromEar * 100f:0.0} cm from its spot by the ear" : "not moved")}), the café rig's own body";
            }
            else
            {
                Vector3 centre = WiringSpot + Vector3.left * .475f + Vector3.up * 1.2f;
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"{number:00} {label} {moment:0.00}.png"), centre + new Vector3(.25f, .15f, 2.9f), centre, 34f, false);
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            PolygonNpcSetup.UnbakePose(baked);
            foreach (var p in props) if (p != null) Object.DestroyImmediate(p);
            foreach (var a in actors) if (a != null) Object.DestroyImmediate(a);
        }
        return line;
    }

    // A seated clip on a real seat at three moments, fingers on, from in front of the seat.
    static string PhotographSeatedWiring(AnimationClip clip, string label, GameObject prefab, TableSeat seat, string folder, int number, bool lounge)
    {
        float[] moments = PickMoments(clip);
        var line = new StringBuilder($"{number:00} {label} on {(seat.transform.parent != null ? seat.transform.parent.name + "/" : "")}{seat.name} " +
                                     $"({(lounge ? "no table" : "table")}), at {Fractions(moments)}");
        for (int m = 0; m < moments.Length; m++)
        {
            GameObject actor = null;
            var baked = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
            try
            {
                actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                var seating = actor.GetComponent<NpcSeating>();
                Require(seating != null, CustomerPrefab + " has no NpcSeating.");
                float floor = FloorBelow(seat.StandPoint.position);
                seating.Placement(seat, floor, out Vector3 feet, out Quaternion facing);
                actor.transform.SetPositionAndRotation(feet, facing);
                var visual = actor.GetComponent<PolygonNpcVisual>();
                if (visual != null && visual.AppearanceCount > 0) visual.ApplyAppearance(Look(number, visual.AppearanceCount));
                if (visual != null) visual.Seated = true;
                Sample(actor, clip, moments[m] * clip.length);
                if (visual != null) visual.Follow();
                baked.AddRange(PolygonNpcSetup.BakePose(actor));
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Vector3 a = seat.SeatPose.position;
                Vector3 forward = facing * Vector3.forward;
                Vector3 across = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 camera = lounge ? a + forward * 2.1f + across * .7f + Vector3.up * 1.2f
                                        : a + (across * 1.5f + forward * 1.3f).normalized * 2.3f + Vector3.up * 1.45f;
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"{number:00} {label} {m + 1}.png"), camera, a + Vector3.up * .5f + forward * .15f, 40f, true);
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                PolygonNpcSetup.UnbakePose(baked);
                if (actor != null) Object.DestroyImmediate(actor);
            }
        }
        return line.ToString();
    }

    // The floor under a point (the lounge seats keep their object at cushion height).
    static float FloorBelow(Vector3 p)
    {
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * .3f, Vector3.down, 3f).OrderBy(h => h.distance))
            if (hit.normal.y > .7f && hit.point.y < p.y + .05f) return hit.point.y;
        return p.y;
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

    // A seated clip's foot may dip this much below the café's resting ankle height (a shuffle), no more.
    const float SeatedFootSlack = .01f;

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
