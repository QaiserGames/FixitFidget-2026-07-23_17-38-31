using System;
using TMPro;
using UnityEngine;

// ---------------------------------------------------------------------------
// A street-name sign at a corner of the café's block (night walk part 4,
// claude/night-city-proposal.md §9): two blades on top of the corner's signal
// post, each lying along the street it names. Shown by day and by night. Put
// up by Fixit Fidget > Night > Night walk 4a - Put up house numbers and street
// signs.
//
// The names come from District streets (Assets/Data/Resources/District
// streets.asset): rename a street there and the signs follow, like every
// address and notebook line. Each blade stores the street's id, never its name.
// ---------------------------------------------------------------------------
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StreetNameSign : MonoBehaviour
{
    [Serializable]
    public sealed class Blade
    {
        [Tooltip("An id from District streets (west, east, front, back).")]
        public string streetId = "";
        [Tooltip("The lettering on each side of the blade.")]
        public TMP_Text[] faces = Array.Empty<TMP_Text>();
    }

    public Blade[] blades = Array.Empty<Blade>();

    static bool namesLoaded;
    float nextCheck;

    void OnEnable()
    {
        // In Edit Mode nothing may have loaded the street names yet (Play loads them before the scene).
        if (!Application.isPlaying && !namesLoaded) namesLoaded = DistrictStreets.LoadFromResources();
        Refresh();
    }

    void Update()
    {
        if (Application.isPlaying && Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .5f;
        Refresh();
    }

    void Refresh()
    {
        if (blades == null) return;
        foreach (Blade blade in blades)
        {
            if (blade == null || blade.faces == null) continue;
            string name = StreetNames.Name(blade.streetId);
            foreach (TMP_Text face in blade.faces)
                if (face != null && face.text != name) face.text = name;
        }
    }

    /// <summary>"West Street / Front Street", for reports.</summary>
    public string Says()
    {
        if (blades == null) return "";
        var names = new System.Collections.Generic.List<string>();
        foreach (Blade blade in blades) if (blade != null) names.Add(StreetNames.Name(blade.streetId));
        return string.Join(" / ", names);
    }
}
