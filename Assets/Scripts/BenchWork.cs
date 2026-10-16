using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BENCH'S WORK, FOR WHOEVER SHOWS IT (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.5;
// contracts §4)
//
// UI/UX's glow, its "what's left" and its stuck timer (uiux-checklist-v2.md §5.4-5.5) read the work from here:
//   * Progressed(job): any work advancing (a hold's progress rising, a spot 2 % cleaner, a piece landing in its place, a target
//     or a step done), at most four times a second: the stuck timer's "something happened";
//   * TargetDone(job, target): the frame a target finishes (a screw out or home, a cover off or on, a part out or in, a spot
//     clean), so the card ticks without waiting for its poll;
//   * Flash(job): PowerWash's dirt indicator, his "what's left" (Tab / View): every spot still dirty and not under a cover lights
//     gold for 1.5 s with its tiniest specks raised, Flashed tells the glow, bench.showleft plays;
//   * WorkLeft(job, into): every target with work left in any step, where it is on screen and whether it is hidden.
// ---------------------------------------------------------------------------
public static class BenchWork
{
    public enum Kind { Screw, Hole, Cover, Part, FreshPart, Spot }

    public struct Item
    {
        public Component target;
        public Kind kind;
        public Vector2 screenCentre;     // pixels, Unity screen space (origin bottom-left)
        public float screenRadius;       // pixels: the target's projected size (the glow adds its own margin)
        public bool hidden;              // under a cover, or not in sight from the close-up
        public float remaining;          // a spot's dirt left (0..1); 1 for anything else
    }

    public static event System.Action<JobBase, float> Flashed;        // after Flash
    public static event System.Action<JobBase> Progressed;            // throttled to 4 per second
    public static event System.Action<JobBase, Component> TargetDone; // the frame a target finishes

    const float ProgressEvery = .25f;
    static float lastProgressAt = -10f;
    static JobBase lastProgressJob;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Flashed = null;
        Progressed = null;
        TargetDone = null;
        lastProgressAt = -10f;
        lastProgressJob = null;
        LastFlashAt = -10f;
        LastProgressAt = -10f;
        Flashes = 0;
    }

    /// <summary>Work on <paramref name="job"/> moved on (the pieces and the inspector call this; raised at most 4 a second).</summary>
    public static void NoteProgress(JobBase job)
    {
        if (job == null) return;
        LastProgressAt = Time.unscaledTime;
        if (job == lastProgressJob && Time.unscaledTime - lastProgressAt < ProgressEvery) return;
        lastProgressAt = Time.unscaledTime;
        lastProgressJob = job;
        try { Progressed?.Invoke(job); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    /// <summary>A target of <paramref name="job"/> finished this frame (it counts as progress too).</summary>
    public static void NoteTargetDone(JobBase job, Component target)
    {
        if (job == null) return;
        try { TargetDone?.Invoke(job, target); }
        catch (System.Exception e) { Debug.LogException(e); }
        lastProgressAt = -10f;     // a target done is always news to the stuck timer
        NoteProgress(job);
    }

    /// <summary>When work last moved on, unthrottled (the inspector's own stuck test, labs).</summary>
    public static float LastProgressAt { get; private set; } = -10f;
    /// <summary>When "what's left" last fired, and how many times (labs).</summary>
    public static float LastFlashAt { get; private set; } = -10f;
    public static int Flashes { get; private set; }

    // ---------- "what's left" (§4.5) ----------

    /// <summary>
    /// "What's left" (§4.5; UI/UX's Tab / View): every spot of <paramref name="job"/> not clean and not under a seated or popped
    /// cover lights gold for <paramref name="seconds"/> (GrimeSpot.Flash: its faintest specks raised to be seen), Flashed tells
    /// the glow (UI/UX's BenchGlow draws the rings and outlines), and bench.showleft plays on this, the press's, frame. A second
    /// call restarts it.
    /// </summary>
    public static void Flash(JobBase job, float seconds = 1.5f)
    {
        if (job == null) return;
        seconds = Mathf.Max(.6f, seconds);
        job.GetComponentsInChildren(spots);
        foreach (GrimeSpot spot in spots)
            if (spot != null && spot.isActiveAndEnabled && !spot.IsClean && spot.CoveredBy == null) spot.Flash(seconds);
        spots.Clear();
        LastFlashAt = Time.unscaledTime;
        Flashes++;
        if (Application.isPlaying) Sfx.Play2D("bench.showleft");
        try { Flashed?.Invoke(job, seconds); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    /// <summary>
    /// Every target of <paramref name="job"/> with work left in any of its steps (RepairChecklist's targets), with where it is on
    /// screen (the main camera), how big, whether it is hidden (under a cover, or not in the close-up's sight) and, for a spot,
    /// how much of its dirt is left. A spot under a cover lists its cover instead. Clears <paramref name="into"/> first.
    /// </summary>
    public static void WorkLeft(JobBase job, List<Item> into)
    {
        if (into == null) return;
        into.Clear();
        if (job == null) return;
        RepairChecklist.Build(job, steps);
        listed.Clear();
        Camera cam = Camera.main;
        foreach (RepairChecklist.Step step in steps)
        {
            if (step.done || step.targets == null) continue;
            foreach (Component target in step.targets) Add(into, target, cam);
        }
        job.GetComponentsInChildren(spots);
        foreach (GrimeSpot spot in spots)
        {
            if (spot == null || spot.IsClean || !spot.isActiveAndEnabled) continue;
            RemovablePart cover = spot.CoveredBy;
            if (cover != null) Add(into, cover, cam);
        }
        spots.Clear();
        listed.Clear();
    }

    static readonly List<RepairChecklist.Step> steps = new List<RepairChecklist.Step>();
    static readonly List<GrimeSpot> spots = new List<GrimeSpot>();
    static readonly HashSet<Component> listed = new HashSet<Component>();

    static void Add(List<Item> into, Component target, Camera cam)
    {
        if (target == null || !target.gameObject.activeInHierarchy || !listed.Add(target)) return;
        var item = new Item { target = target, kind = KindOf(target), remaining = 1f };
        Vector3 centre;
        float worldRadius;
        switch (target)
        {
            case GrimeSpot spot:
                centre = spot.Centre;
                worldRadius = spot.Radius;
                item.remaining = 1f - spot.Progress;
                break;
            case Screw screw:
                centre = screw.HeadPoint;
                worldRadius = screw.HeadRadius;
                break;
            default:
                if (DeviceBounds.TryGet(target.gameObject, out Bounds b)) { centre = b.center; worldRadius = b.extents.magnitude; }
                else { centre = BenchInteractable.BoundsCentre(target.transform); worldRadius = .005f; }
                break;
        }
        bool covered = target is GrimeSpot g && g.CoveredBy != null;
        ItemInspector inspector = ItemInspector.Instance;
        item.hidden = covered || inspector != null && ItemInspector.Working && !inspector.IsSeen(target.transform);
        if (cam != null)
        {
            Vector3 s = cam.WorldToScreenPoint(centre);
            item.screenCentre = s;
            if (s.z > 0f)
            {
                // The radius as it shows at that depth (a sphere of the world radius, seen side on).
                Vector3 edge = cam.WorldToScreenPoint(centre + cam.transform.right * worldRadius);
                item.screenRadius = ((Vector2)edge - (Vector2)s).magnitude;
            }
        }
        into.Add(item);
    }

    static Kind KindOf(Component target) => target switch
    {
        Screw _ => Kind.Screw,
        ScrewTarget _ => Kind.Screw,
        ScrewSocket _ => Kind.Hole,
        RemovablePart _ => Kind.Cover,
        ReplaceablePart _ => Kind.Part,
        LoosePart _ => Kind.FreshPart,
        _ => Kind.Spot,
    };

    /// <summary>The job a piece belongs to: the device it sits on, or (off the device) the job that registered it.</summary>
    public static JobBase JobOf(Component piece)
    {
        if (piece == null) return null;
        JobBase job = piece.GetComponentInParent<JobBase>();
        if (job != null) return job;
        foreach (JobBase j in Object.FindObjectsByType<JobBase>(FindObjectsSortMode.None))
            foreach (GameObject loose in j.LooseParts)
                if (loose != null && (loose == piece.gameObject || piece.transform.IsChildOf(loose.transform))) return j;
        return null;
    }
}
