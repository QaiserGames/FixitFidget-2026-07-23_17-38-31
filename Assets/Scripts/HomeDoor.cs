using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// A regular's front door (night step 3, claude/night-homes-spec.md).
//
// Sits on the doorstep, facing the street. A regular whose profile has this
// door's Home Id always walks out of it to the café and back in afterwards
// (CafeArrivals uses the walking route with the same id). When Ace sees them
// at this door, the notebook gets where they live (NotebookHooks.SawAtHome).
//
// The address is a placeholder for now: the house number here, and the street
// by its id in District streets, where the name can be changed at any time.
//
// Playtest 7 (19 Oct 2026; "What's saffron how?"): Grace's house looks like "the
// yellow house", the one way the night names it (LodgerStory: "12 West Street, the
// yellow house with the porch light"), never "the saffron house": a colour word that
// doesn't read at night, and the street had two of them. HomeSetup writes it; the
// notebook's address line reads "Went into the yellow house at 12 West Street." (9
// words: the page's budget is 12).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class HomeDoor : MonoBehaviour
{
    /// <summary>Grace's house as Ace sees it (HomeSetup writes it onto her door; LodgerStory names it the same way, with its porch light).</summary>
    public const string GraceLooks = "the yellow house";

    [Tooltip("Stable id; a regular's profile points at it (Home Id). Never change it once saves exist.")]
    public string homeId = "home.grace";
    [Tooltip("The house as Ace would describe it at a glance, e.g. \"the yellow house\". What's on screen, not canon.")]
    public string looks = GraceLooks;
    [Tooltip("The house number, as written on the door. A placeholder for now.")]
    public string houseNumber = "12";
    [Tooltip("The street it's on: an id from District streets (Assets/Data/Resources), e.g. west.")]
    public string streetId = "west";

    private static readonly List<HomeDoor> doors = new();

    /// <summary>"12 West Street", with the street's current name.</summary>
    public string Address
    {
        get
        {
            string street = StreetNames.Name(streetId);
            return string.IsNullOrWhiteSpace(houseNumber) ? street : houseNumber.Trim() + " " + street;
        }
    }

    public Vector3 DoorPoint => transform.position;

    public static IReadOnlyList<HomeDoor> All => doors;

    /// <summary>The enabled door with this id, or null.</summary>
    public static HomeDoor Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (HomeDoor door in doors)
            if (door != null && door.homeId == id.Trim()) return door;
        return null;
    }

    private void OnEnable() { if (!doors.Contains(this)) doors.Add(this); }
    private void OnDisable() => doors.Remove(this);

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, .72f, .2f, .9f);
        Vector3 at = transform.position;
        Gizmos.DrawWireCube(at + Vector3.up * 1.1f, new Vector3(.2f, 2.2f, 1f));
        Gizmos.DrawLine(at + Vector3.up * .05f, at + Vector3.up * .05f + transform.forward * HomeRules.WatchRadius);
    }
}
