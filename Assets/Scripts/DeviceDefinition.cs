using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DeviceFault
{
    public FaultType type = FaultType.Mechanical;

    [Tooltip("Plain language, used on the ticket and in dialogue. e.g. 'cracked screen'")]
    public string description = "broken";

    [Tooltip("Fault components to switch ON when this fault is chosen. Everything listed on OTHER faults gets switched off.")]
    public GameObject[] enableObjects;

    public int payout = 25;
}

public class DeviceDefinition : MonoBehaviour
{
    [Tooltip("Used in dialogue via the {device} token. e.g. 'pocket watch'")]
    public string displayName = "thing";

    [Tooltip("Stable episode marker for an explicitly featured request. Does not enroll random repairs in a story.")]
    public string storyEpisodeId = "";

    public DeviceFault[] faults;

    // ---------- the forgiving bench (14 Oct 2026; contracts §1). Prefab data, not save data. ----------

    [Tooltip("Set by Devices 2 once the prefab is baked at real size (the TD's guard).")]
    public bool realSize;

    [Tooltip("Where this device lies on the repair mat and where the bench's rest, old-parts tray and close-up camera go for it, " +
             "in the mat's millimetres (Art's frame: +x the player's right, +y away from him). Filled by Bench 7 (Systems' numbers) " +
             "and Bench 9 (the camera's).")]
    public BenchLayout benchLayout = new BenchLayout();

    public DeviceFault GetFault(int index)
    {
        if (faults == null || faults.Length == 0) return null;
        return faults[Mathf.Clamp(index, 0, faults.Length - 1)];
    }

    public int RandomFaultIndex()
    {
        if (faults == null || faults.Length == 0) return 0;
        return Random.Range(0, faults.Length);
    }

    // Switch on this fault's parts, switch off every other fault's parts.
    public void ApplyFault(int index)
    {
        if (faults == null || faults.Length == 0) return;

        index = Mathf.Clamp(index, 0, faults.Length - 1);
        var all = new HashSet<GameObject>();
        var selected = new HashSet<GameObject>();
        for (int i = 0; i < faults.Length; i++)
        {
            if (faults[i] == null || faults[i].enableObjects == null) continue;
            foreach (GameObject g in faults[i].enableObjects)
            {
                if (g == null) continue;
                all.Add(g);
                if (i == index) selected.Add(g);
            }
        }
        // Shared objects must stay on if the chosen fault needs them, regardless
        // of where the other fault appears in the list.
        foreach (GameObject g in all) g.SetActive(selected.Contains(g));
        RepairJob repair = GetComponent<RepairJob>();
        if (repair != null) repair.CaptureTasks();
    }

    /// <summary>The device's bench layout when it has one filled in, else the phone's (a device nobody laid out yet).</summary>
    public static BenchLayout LayoutOf(JobBase job)
    {
        DeviceDefinition definition = job != null ? job.GetComponent<DeviceDefinition>() : null;
        return definition != null && definition.benchLayout != null && definition.benchLayout.IsSet ? definition.benchLayout : BenchLayout.Fallback;
    }
}

// ---------------------------------------------------------------------------
// A DEVICE'S PLACE AT THE BENCH (14 Oct 2026; systems-forgiving-bench.md §4.0.1, td-one-scale.md §5.2, blender-wave1.md §5
// step 3; the field names are contracts §1). Mat millimetres, Art's frame: +x the player's right, +y away from him, origin
// the mat's middle. Read when the close-up opens (ItemInspector.BeginInspection, BenchStage.BeginJob): the device lies at
// `rest`, the tool rest and the old-parts tray move to it, the close-up camera frames it. Prefab data, not save data.
// ---------------------------------------------------------------------------
[System.Serializable]
public class BenchLayout   // mat millimetres, Art's frame: +x the player's right, +y away from him
{
    public Vector2 rest;              // where the device lies (Systems §4.0.1)
    public float toolRestCentreX;     // Systems
    public float toolRestCentreY;     // the Blender lead; 0 for every device (the Producer's call 21)
    public Vector2 scrapTray;         // Systems
    public Vector2 cameraAim;         // the TD (§5.2); mat mm
    public float cameraDistance;      // the TD; metres. 0 = unset: the bench keeps the scene's framing
    public Vector2 closeAim;          // the TD (§5.3); mat mm
    public float fovClose;            // the TD; degrees at zoom 1. 0 = unset

    /// <summary>
    /// Filled in: a tool rest at x 0 would lie on the device, so a layout whose rest is still at 0 has never been set (a prefab
    /// from before the forgiving bench, or one Bench 7 hasn't reached).
    /// </summary>
    public bool IsSet => toolRestCentreX != 0f;

    /// <summary>The TD's framing is filled in (Bench 9): the close-up places its camera from it.</summary>
    public bool HasFraming => cameraDistance > 0f;

    /// <summary>
    /// Art's three layouts (A1; Systems §4.0.1's table), with the Producer's call 21 (14 Oct, 11:30; the Blender lead's wave 2
    /// §2): at the TD's tighter framings a tool rest 60 mm toward Ace fell into U4 (bottom left) for the phone and the watch,
    /// so every rest is centred (y 0), and the phone's and the watch's old-parts trays move right, (-30, 133) -> (6, 133) and
    /// (-20, 107) -> (26, 107), clear of the centred rest. The camera's is unchanged.
    /// </summary>
    public static BenchLayout ForPhone => new BenchLayout { rest = new Vector2(-30f, -14f), toolRestCentreX = -135f, toolRestCentreY = 0f, scrapTray = new Vector2(6f, 133f) };
    public static BenchLayout ForCamera => new BenchLayout { rest = new Vector2(-82f, 0f), toolRestCentreX = -239f, toolRestCentreY = 0f, scrapTray = new Vector2(-5f, 115f) };
    public static BenchLayout ForWatch => new BenchLayout { rest = new Vector2(-20f, 0f), toolRestCentreX = -115f, toolRestCentreY = 0f, scrapTray = new Vector2(26f, 107f) };

    /// <summary>The preset for a device kind ("phone", "camera", "watch"; anything else the phone's), as Bench 7 writes it.</summary>
    public static BenchLayout ForKind(string kind) => kind == "camera" ? ForCamera : kind == "watch" ? ForWatch : ForPhone;

    /// <summary>Same place (rest, tool rest, tray) as <paramref name="other"/>, within a hundredth of a millimetre; the camera fields aren't compared.</summary>
    public bool SamePlaceAs(BenchLayout other) =>
        other != null && (rest - other.rest).sqrMagnitude < 1e-4f && Mathf.Abs(toolRestCentreX - other.toolRestCentreX) < .01f
        && Mathf.Abs(toolRestCentreY - other.toolRestCentreY) < .01f && (scrapTray - other.scrapTray).sqrMagnitude < 1e-4f;

    /// <summary>A device nobody laid out yet lies where the phone does (the commonest shape at the bench).</summary>
    public static BenchLayout Fallback => ForPhone;

    /// <summary>Copies another layout's Systems fields (rest, tool rest, tray), keeping this one's camera fields.</summary>
    public void CopyPlaceFrom(BenchLayout other)
    {
        if (other == null) return;
        rest = other.rest;
        toolRestCentreX = other.toolRestCentreX;
        toolRestCentreY = other.toolRestCentreY;
        scrapTray = other.scrapTray;
    }

    public override string ToString() =>
        $"rest ({rest.x:0}, {rest.y:0}), tool rest ({toolRestCentreX:0}, {toolRestCentreY:0}), tray ({scrapTray.x:0}, {scrapTray.y:0}), " +
        (HasFraming ? $"camera aim ({cameraAim.x:0}, {cameraAim.y:0}) at {cameraDistance:0.000} m, close aim ({closeAim.x:0}, {closeAim.y:0}), fov close {fovClose:0.0}°" : "the scene's framing");
}
