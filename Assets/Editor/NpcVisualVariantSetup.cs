using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit editor operation; never runs automatically or changes a player location.</summary>
public static class NpcVisualVariantSetup
{
    private const string Folder = "Assets/Art/PlaceholderNeighbors";
    private const string VisualRootName = "Temporary CC0 walk-in appearances";
    private static readonly string[] ModelNames = { "Casual_2", "Casual_Hoodie", "Farmer", "Suit", "Worker" };
    private static readonly string[] PrefabPaths = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };

    public static string PrepareModels()
    {
        foreach (string name in ModelNames)
        {
            string path = $"{Folder}/Models/{name}.fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Model is not imported: " + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        return string.Join("\n", ModelNames.Select(name =>
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Models/{name}.fbx");
            return name + ": " + string.Join(", ", model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(r => r.name + " / " + r.bones.Length + " bones / " + r.sharedMesh.vertexCount + " vertices"));
        }));
    }

    public static string ApplyToPrefabs()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring appearances.");
        List<string> reports = new List<string>();
        foreach (string path in PrefabPaths)
        {
            GameObject actor = PrefabUtility.LoadPrefabContents(path);
            try
            {
                reports.Add(ApplyToActor(actor));
                PrefabUtility.SaveAsPrefabAsset(actor, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", reports);
    }

    public static string ApplyToActor(GameObject actor, int fixedAppearance = -1)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Editor authoring only.");
        Transform oldRoot = actor.transform.Find(VisualRootName);
        NpcVisualVariants oldSelector = actor.GetComponent<NpcVisualVariants>();
        if (oldSelector != null) oldSelector.ApplyAppearance(-1);
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
        if (oldSelector != null) UnityEngine.Object.DestroyImmediate(oldSelector);

        var snapshot = actor.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && !(c is Transform) && !(c is Renderer))
            .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        SkinnedMeshRenderer[] original = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (original.Length == 0) throw new InvalidOperationException(actor.name + " has no original skinned renderers.");
        var boneMap = actor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());

        // Reject mismatching skeletons before changing the actor. Name matching alone
        // is insufficient: the bind-pose transforms must agree with the existing rig.
        GameObject[] models = ModelNames.Select(name =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Models/{name}.fbx")).ToArray();
        foreach (GameObject model in models) ValidateRig(actor, model, boneMap, original);

        GameObject holder = new GameObject(VisualRootName);
        holder.transform.SetParent(actor.transform, false);
        List<NpcVisualVariants.Appearance> choices = new List<NpcVisualVariants.Appearance>();
        foreach (GameObject model in models)
        {
            GameObject group = new GameObject(model.name);
            group.transform.SetParent(holder.transform, false);
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer source in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                GameObject visual = new GameObject(source.name);
                visual.layer = original[0].gameObject.layer;
                visual.transform.SetParent(group.transform, false);
                visual.transform.localPosition = model.transform.InverseTransformPoint(source.transform.position);
                visual.transform.localRotation = Quaternion.Inverse(model.transform.rotation) * source.transform.rotation;
                Vector3 scale = source.transform.lossyScale;
                Vector3 parentScale = model.transform.lossyScale;
                visual.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                SkinnedMeshRenderer renderer = visual.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials.Select(PaletteMaterial).ToArray();
                renderer.bones = source.bones.Select(b => boneMap[b.name]).ToArray();
                renderer.rootBone = boneMap[source.rootBone.name];
                renderer.localBounds = source.localBounds;
                renderer.quality = source.quality;
                renderer.shadowCastingMode = original[0].shadowCastingMode;
                renderer.receiveShadows = original[0].receiveShadows;
                renderer.updateWhenOffscreen = original[0].updateWhenOffscreen;
                renderer.enabled = false;
                renderers.Add(renderer);
            }
            choices.Add(new NpcVisualVariants.Appearance { label = model.name, renderers = renderers.ToArray() });
        }
        NpcVisualVariants selector = actor.AddComponent<NpcVisualVariants>();
        selector.Configure(original, choices.ToArray(), fixedAppearance);
        foreach (var item in snapshot)
            if (EditorJsonUtility.ToJson(item.Key) != item.Value)
                throw new InvalidOperationException("Gameplay component unexpectedly changed: " + item.Key.GetType().Name);
        EditorUtility.SetDirty(actor);
        return actor.name + ": " + choices.Count + " appearances; original " + snapshot.Count
            + " gameplay/UI/animation components preserved exactly.";
    }

    // Compares where every bone sat when each mesh was BOUND (the meshes'
    // bind poses, in actor-root space), not where the bones happen to sit now.
    //
    // The first version compared live bone transforms and failed on 23 Sept
    // ("Body differs by 0.0078"): Beach.fbx is imported with its animations, so
    // Unity stores its bones in an animation pose (hips 7.8 mm lower), while the
    // new models are imported without animation and sit in the rest pose. Read
    // straight from the FBX files, all 80 bones and every bind matrix of Beach
    // and Casual_2 are identical, so that difference was a pose, not a rig.
    // Bind poses are what skinning actually uses, and they do not depend on
    // pose, so this is the check that answers "same skeleton?".
    private static void ValidateRig(GameObject actor, GameObject model, Dictionary<string, Transform> bones,
        SkinnedMeshRenderer[] originals)
    {
        if (model == null) throw new InvalidOperationException("Missing appearance model.");
        var boundOriginal = BindSkeleton(actor.transform, originals);
        if (boundOriginal.Count == 0) throw new InvalidOperationException(actor.name + ": original body has no bind poses to compare.");

        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(model.name + " has no skinned meshes.");
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.rootBone == null || !bones.ContainsKey(renderer.rootBone.name))
                throw new InvalidOperationException(model.name + ": root bone does not match.");
            foreach (Transform source in renderer.bones)
                if (source == null || !bones.ContainsKey(source.name))
                    throw new InvalidOperationException(model.name + ": absent or ambiguous bone " + (source != null ? source.name : "null"));
        }

        int compared = 0; float worst = 0f; string worstBone = null;
        foreach (var bone in BindSkeleton(model.transform, renderers))
        {
            if (!boundOriginal.TryGetValue(bone.Key, out Matrix4x4 expected)) continue;
            compared++;
            for (int i = 0; i < 16; i++)
            {
                float difference = Mathf.Abs(bone.Value[i] - expected[i]) / Mathf.Max(1f, Mathf.Abs(expected[i]));
                if (difference > worst) { worst = difference; worstBone = bone.Key; }
            }
        }
        if (compared == 0) throw new InvalidOperationException(model.name + ": shares no bound bones with " + actor.name + ".");
        if (worst > 0.002f)
            throw new InvalidOperationException(model.name + ": bound to a different skeleton (" + worstBone + " differs by " + worst + ").");
    }

    // Bone name -> the bone's transform at bind time, relative to root.
    private static Dictionary<string, Matrix4x4> BindSkeleton(Transform root, IEnumerable<SkinnedMeshRenderer> renderers)
    {
        var result = new Dictionary<string, Matrix4x4>();
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            Matrix4x4 rendererToRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Transform[] rendererBones = renderer.bones;
            for (int i = 0; i < rendererBones.Length && i < bindposes.Length; i++)
                if (rendererBones[i] != null && !result.ContainsKey(rendererBones[i].name))
                    result[rendererBones[i].name] = rendererToRoot * bindposes[i].inverse;
        }
        return result;
    }

    private static Material PaletteMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("Appearance mesh is missing a material.");
        string directory = Folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder(Folder, "Materials");
        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
        string key = ColorUtility.ToHtmlStringRGBA(color);
        string path = directory + "/Character_" + key + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = "Character " + key };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static string ValidatePrefabs()
    {
        List<string> reports = new List<string>();
        foreach (string path in PrefabPaths)
        {
            GameObject actor = PrefabUtility.LoadPrefabContents(path);
            try
            {
                NpcVisualVariants selector = actor.GetComponent<NpcVisualVariants>();
                if (selector == null || selector.AppearanceCount != ModelNames.Length)
                    throw new InvalidOperationException(path + ": appearance selector is incomplete.");
                Animator animator = actor.GetComponentInChildren<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException(path + ": existing Animator/controller is missing.");
                for (int i = 0; i < selector.AppearanceCount; i++)
                {
                    if (!selector.ApplyAppearance(i)) throw new InvalidOperationException(path + ": invalid appearance " + i);
                    SkinnedMeshRenderer[] visible = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled).ToArray();
                    if (visible.Length == 0 || visible.Any(r => r.bones.Length == 0 || r.bones.Any(b => b == null)))
                        throw new InvalidOperationException(path + ": appearance has missing bones.");
                    if (visible.Any(r => r.sharedMaterials.Any(m => m == null || m.shader == null)))
                        throw new InvalidOperationException(path + ": appearance has missing materials.");
                }
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips.Distinct()
                    .Where(c => c != null && (c.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0
                        || c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)).Take(2).ToArray();
                if (clips.Length == 0) throw new InvalidOperationException(path + ": no idle/walk clips available to verify skinning.");
                foreach (AnimationClip clip in clips)
                {
                    clip.SampleAnimation(animator.gameObject, clip.length * 0.35f);
                    selector.ApplyAppearance(-1);
                    Bounds baseline = BakedBounds(actor);
                    for (int i = 0; i < selector.AppearanceCount; i++)
                    {
                        selector.ApplyAppearance(i);
                        Bounds appearance = BakedBounds(actor);
                        if (appearance.size.y < baseline.size.y * 0.75f || appearance.size.y > baseline.size.y * 1.25f
                            || Vector3.Distance(appearance.center, baseline.center) > 0.35f
                            || appearance.size.x > baseline.size.x * 1.8f || appearance.size.z > baseline.size.z * 1.8f)
                            throw new InvalidOperationException(path + ": deformed skinning bounds for " + selector.ActiveAppearanceName + " in " + clip.name);
                    }
                }
                selector.ApplyAppearance(-1);
                reports.Add(path + ": all five variants have complete meshes, materials and original skeleton references; baked skinning passed " + clips.Length + " idle/walk poses.");
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
        }
        return string.Join("\n", reports);
    }

    private static Bounds BakedBounds(GameObject actor)
    {
        Bounds bounds = default;
        bool first = true;
        foreach (SkinnedMeshRenderer renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled))
        {
            Mesh baked = new Mesh();
            try
            {
                // useScale: true, so the baked vertices are in the renderer's full
                // local space. Without it they omit the renderer's scale (100 on
                // these Quaternius meshes) and localToWorldMatrix re-applies it,
                // inflating every box 100x, which made the 0.35 m centre tolerance
                // fail on millimetre differences (23 Sept, "deformed skinning bounds").
                renderer.BakeMesh(baked, true);
                Matrix4x4 toActor = actor.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 point = toActor.MultiplyPoint3x4(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }
        if (first) throw new InvalidOperationException(actor.name + ": no visible baked geometry.");
        return bounds;
    }
}
