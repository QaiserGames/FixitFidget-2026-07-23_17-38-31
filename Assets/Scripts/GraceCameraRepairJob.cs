using UnityEngine;

// Existing scrub/tweezers controls and normal task grades; a clean camera with
// a broken shutter cannot produce a usable photograph. The strap is preserved
// scenery, never a replacement task or an invisible bonus objective.
//
// DAYS 1-3 (14 Oct 2026; specs/days-1-3.md §5, specs/systems-forgiving-bench.md §7 step 11). His words after playtest 5:
// "god it is so hard to get the camera perfectly repaired, i stayed all day on the repair". The camera now has two faults:
// fault 0 is Day 1's clean (the three spots, with the shutter part switched off and a resting copy of it in its place, a look,
// never a task), fault 1 is Day 2's jammed shutter (the part on, the spots off: the camera arrives clean). R1: the shutter
// gates the grade only when it is one of today's tasks, so Day 1's clean grades by its spots alone (3 Perfect, 2 Good,
// 1 Passable); a camera with no fault applied (both on, as the prefab is built) still needs the shutter, as before.
public class GraceCameraRepairJob : RepairJob
{
    [SerializeField] private ReplaceablePart shutter;
    public ReplaceablePart Shutter => shutter;

    /// <summary>The shutter is one of today's tasks: its part is switched on (DeviceDefinition.ApplyFault: fault 1, or no fault).</summary>
    public bool ShutterIsTask => shutter != null && shutter.gameObject.activeSelf;

    public override float Quality => ShutterIsTask && !shutter.IsReplaced ? 0f : base.Quality;

    public string TaskSummary
    {
        get
        {
            var remaining = GetComponentsInChildren<GrimeSpot>();
            string cleaning = remaining.Length == 0 ? "Lens and film path clean"
                : "Clean: " + string.Join(", ", System.Array.ConvertAll(remaining,
                    spot => spot.name.Replace(" grime", "").Replace("-", " ").ToLowerInvariant()));
            // Day 1's clean has no shutter to mention: it isn't today's job (R1).
            if (!ShutterIsTask) return cleaning;
            return cleaning + (shutter.IsReplaced ? " · shutter repaired" : " · replace shutter");
        }
    }
}
