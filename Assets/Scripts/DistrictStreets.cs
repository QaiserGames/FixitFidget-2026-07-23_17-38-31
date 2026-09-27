using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// The café district's street names, in one place you can edit any time
// (Mansoor, 27 Sept: "just put a placeholder for now and have it where i can
// always edit the street names").
//
// The asset lives at Assets/Data/Resources/District streets.asset. Select it and
// type a new name: every address and notebook line that mentions the street
// shows the new name, including lines written before the rename (they store the
// street's id, not its name; see StreetNames). Never change an id once it is
// used: homes and saved notebooks refer to streets by id.
// ---------------------------------------------------------------------------
[CreateAssetMenu(fileName = ResourceName, menuName = "Fixit Fidget/District streets")]
public sealed class DistrictStreets : ScriptableObject
{
    public const string ResourceName = "District streets";

    [Serializable]
    public sealed class Street
    {
        [Tooltip("Never change once used: homes and saved notebook lines refer to the street by this id.")]
        public string id = "";
        [Tooltip("What the street is called. Rename it any time; everything that shows it follows.")]
        public string name = "";
        [Tooltip("Which street this is, for whoever edits this list. Not shown in the game.")]
        [TextArea(1, 3)] public string where = "";
    }

    [Tooltip("Placeholder names for now. Rename freely; keep the ids.")]
    public Street[] streets =
    {
        new Street { id = "west", name = "West Street", where = "Between the café's street-side windows and the bay-window houses (Grace's saffron house is on it)." },
        new Street { id = "east", name = "East Street", where = "Past the courtyard, in front of the courtyard shops." },
        new Street { id = "front", name = "Front Street", where = "In front of the café's door, with the café's car park across it." },
        new Street { id = "back", name = "Back Street", where = "Behind the café, at the far end of the west and east streets." },
    };

    public string NameOf(string id)
    {
        foreach (Street street in streets)
            if (street != null && string.Equals(street.id?.Trim(), id?.Trim(), StringComparison.OrdinalIgnoreCase))
                return street.name;
        return "";
    }

    /// <summary>Makes these the names the game shows (StreetNames).</summary>
    public void Apply()
    {
        StreetNames.Clear();
        if (streets == null) return;
        foreach (Street street in streets)
            if (street != null) StreetNames.Set(street.id, street.name);
    }

    private void OnEnable() => Apply();
    // Renaming a street in the Inspector takes effect at once, even in Play Mode.
    private void OnValidate() => Apply();

    /// <summary>Loads the district's street names (Resources/District streets). False if the asset is missing.</summary>
    public static bool LoadFromResources()
    {
        DistrictStreets asset = Resources.Load<DistrictStreets>(ResourceName);
        if (asset == null) return false;
        asset.Apply();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadForPlay() => LoadFromResources();
}
