using System;
using UnityEngine;

/// <summary>
/// Temporary walk-in appearances. Only renderers change: the original skeleton,
/// Animator, interaction targets, navigation and patience UI stay on the actor.
/// Authored regulars keep their existing appearance until their own art is ready.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcVisualVariants : MonoBehaviour
{
    [Serializable]
    public sealed class Appearance
    {
        public string label;
        public SkinnedMeshRenderer[] renderers = Array.Empty<SkinnedMeshRenderer>();
    }

    [SerializeField] private SkinnedMeshRenderer[] originalRenderers = Array.Empty<SkinnedMeshRenderer>();
    [SerializeField] private Appearance[] appearances = Array.Empty<Appearance>();
    [SerializeField] private int fixedAppearance = -1;
    private static uint anonymousSequence;

    public int AppearanceCount => appearances.Length;
    public int ActiveAppearance { get; private set; } = -1;
    public string ActiveAppearanceName => ActiveAppearance >= 0
        ? appearances[ActiveAppearance].label : "Original";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSequence() => anonymousSequence = 0;

    private void Start()
    {
        if (fixedAppearance >= 0)
        {
            ApplyAppearance(fixedAppearance);
            return;
        }

        // Spawners assign identity immediately after Instantiate, before Start.
        CustomerIdentity identity = GetComponent<CustomerIdentity>();
        if (identity != null && identity.IsRegular)
        {
            ApplyAppearance(-1);
            return;
        }

        string identityName = identity != null ? identity.DisplayName : null;
        uint choice = string.IsNullOrEmpty(identityName) || identityName == "Customer"
            ? anonymousSequence++ : StableHash(identityName);
        ApplyAppearance(appearances.Length == 0 ? -1 : (int)(choice % (uint)appearances.Length));
    }

    public void Configure(SkinnedMeshRenderer[] originals, Appearance[] choices, int fixedIndex = -1)
    {
        originalRenderers = originals ?? Array.Empty<SkinnedMeshRenderer>();
        appearances = choices ?? Array.Empty<Appearance>();
        fixedAppearance = fixedIndex;
        ApplyAppearance(fixedIndex);
    }

    public bool ApplyAppearance(int index)
    {
        bool valid = index >= 0 && index < appearances.Length
            && appearances[index] != null && appearances[index].renderers != null
            && appearances[index].renderers.Length > 0;
        if (valid)
            foreach (SkinnedMeshRenderer renderer in appearances[index].renderers)
                if (renderer == null || renderer.sharedMesh == null || renderer.rootBone == null)
                    valid = false;

        ActiveAppearance = valid ? index : -1;
        foreach (SkinnedMeshRenderer renderer in originalRenderers)
            if (renderer != null) renderer.enabled = !valid;
        for (int i = 0; i < appearances.Length; i++)
            if (appearances[i] != null && appearances[i].renderers != null)
                foreach (SkinnedMeshRenderer renderer in appearances[i].renderers)
                    if (renderer != null) renderer.enabled = valid && i == index;
        return valid;
    }

    private static uint StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
