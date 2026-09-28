using TMPro;
using UnityEngine;

// ---------------------------------------------------------------------------
// A house number on a small plaque by the front door (night walk part 4,
// claude/night-city-proposal.md §9). Shown by day and by night: the street's
// houses have addresses you can read, so "12 West Street" in Ace's notebook is
// a door you can find. Put up by Fixit Fidget > Night > Night walk 4a - Put up
// house numbers and street signs.
//
// The numbers are placeholders: change `number` here any time. A house with a
// regular's front door (a HomeDoor, e.g. Grace's) shows that door's number
// instead, so the plaque and the notebook can never disagree.
//
// The lettering is lit by the scene (a lit TextMesh Pro material): readable by
// day and under a street lamp, dark at night until Ace's torch finds it.
// ---------------------------------------------------------------------------
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class HouseNumber : MonoBehaviour
{
    [Tooltip("The number on the plaque. A placeholder: change it any time. A house with a regular's front door " +
             "(Home) shows that door's number instead.")]
    public string number = "";
    [Tooltip("The regular's front door on this house, if there is one: its number wins.")]
    public HomeDoor home;
    [Tooltip("The lettering on the plaque.")]
    public TMP_Text label;

    /// <summary>What the plaque says.</summary>
    public string Shown => home != null && !string.IsNullOrWhiteSpace(home.houseNumber) ? home.houseNumber.Trim() : (number ?? "").Trim();

    void OnEnable() => Refresh();

    void Update()
    {
        if (Application.isPlaying && Time.frameCount % 30 != 0) return;
        Refresh();
    }

    void Refresh()
    {
        if (label == null) return;
        string shown = Shown;
        if (label.text != shown) label.text = shown;
    }
}
