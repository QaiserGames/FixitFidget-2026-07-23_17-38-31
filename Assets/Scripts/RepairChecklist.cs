using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE REPAIR'S CHECKLIST (12 Oct 2026)
//
// Mansoor, after his playtest: "we could add a specific journal like menu, for when dealing with repairs, and having a
// checklist so that the player can always know what to do or handle." Agreed: the checklist now, at the bench; a repair
// book (the journal menu) later. claude/playtest-4-findings-and-proposals.md §5.
//
// The job's steps, worked out from the device's real parts and this job's fault, in the order the work goes (the bench's own
// order, ItemInspector.WorkOrder):
//   1. a cover's screws out and the cover off, when this job's work lies under it (RemovablePart.WorkBeneathToDo, or a part
//      of this job whose CoveredBy it is), or once it has been opened anyway;
//   2. the grime (the brush) and the smudges (the cloth), counted once when the checklist first sees the job (a spot
//      destroys itself when it's clean); while both are left, the one the device's first dirty spot wants comes first, as
//      the Day 1 guide picks it;
//   3. each part out and the fresh one in (the tweezers);
//   4. the circuit;
//   5. the cover back on and its screws back in (the lists closing steps for every cover it opens).
// Each step knows from the parts themselves whether it's done; the current step is the first not done. Nothing here is a
// rule: the bench works as before, in any order its parts allow. BenchCards draws it.
// ---------------------------------------------------------------------------
public static class RepairChecklist
{
    public sealed class Step
    {
        /// <summary>The step in full ("Pry off the back cover").</summary>
        public string text;
        /// <summary>The step in two or three words, for the row of all steps ("Cover off").</summary>
        public string chip;
        /// <summary>The tool it wants.</summary>
        public ToolType tool;
        /// <summary>How, in a few words ("hold at its edge").</summary>
        public string how;
        public bool done;
        public bool current;
    }

    // What a job's device had when the checklist first saw it, and what has happened to its covers since.
    sealed class Plan
    {
        public RemovablePart[] covers;
        public ReplaceablePart[] parts;
        public string[] partNames;
        public CircuitPuzzle[] circuits;
        public int dirt, smudges;
        public readonly HashSet<RemovablePart> opened = new();    // screws out or the cover up, at any time
        public readonly HashSet<RemovablePart> removed = new();   // off the device, at any time
    }

    static readonly Dictionary<JobBase, Plan> plans = new();
    static readonly List<JobBase> gone = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => plans.Clear();

    /// <summary>The steps of <paramref name="job"/>'s repair, as they stand now (cleared and filled).</summary>
    public static void Build(JobBase job, List<Step> steps)
    {
        steps.Clear();
        if (job == null) return;
        Plan plan = PlanFor(job);

        // 1. The covers to open: this job's work lies under them (the cover's own list of faults beneath, or a part of this
        // job that names it as its cover), or they've been opened (a cover opened for nothing still goes back on).
        var covers = new List<RemovablePart>();
        foreach (RemovablePart cover in plan.covers)
        {
            if (cover == null) continue;
            if (cover.IsRemoved) plan.removed.Add(cover);
            if (cover.State != RemovablePart.CoverState.Seated || AnyOut(cover)) plan.opened.Add(cover);
            bool partBeneath = false;
            foreach (ReplaceablePart part in plan.parts)
                if (part != null && part.CoveredBy == cover && !part.IsReplaced) partBeneath = true;
            if (plan.opened.Contains(cover) || cover.WorkBeneathToDo || partBeneath) covers.Add(cover);
        }
        foreach (RemovablePart cover in covers)
        {
            string name = Lower(cover.DisplayName);
            bool off = plan.removed.Contains(cover);
            int screws = Count(cover);
            if (screws > 0)
                Add(steps, screws == 1 ? "Take out the screw" : $"Take out the {screws} screws", "Screws out", ToolType.Screwdriver,
                    "hold on each one", off || AllOut(cover));
            Add(steps, $"Pry off the {name}", Short(name) + " off", ToolType.Pry,
                cover.IsPopped ? "lift it off with your hand" : "hold at its edge", off);
        }

        // 2. The grime and the smudges. A done one first; while both are left, the one the device's first dirty spot wants (the
        // Day 1 guide names that spot's tool too: the two never point at different tools, the lab's photo, 12 Oct).
        int dirtLeft = 0, smudgesLeft = 0;
        GrimeSpot firstLeft = null;
        foreach (GrimeSpot spot in job.GetComponentsInChildren<GrimeSpot>())
        {
            if (spot == null || spot.IsClean) continue;
            if (firstLeft == null) firstLeft = spot;
            if (spot.Tool == ToolType.Cloth) smudgesLeft++;
            else dirtLeft++;
        }
        bool smudgesFirst = plan.dirt > 0 && plan.smudges > 0
            && (smudgesLeft == 0 || dirtLeft > 0 && firstLeft != null && firstLeft.Tool == ToolType.Cloth);
        if (smudgesFirst) Smudges(steps, plan, smudgesLeft);
        if (plan.dirt > 0)
            Add(steps, plan.dirt == 1 ? "Brush off the grime" : $"Brush off the grime ({plan.dirt - Mathf.Min(dirtLeft, plan.dirt)} of {plan.dirt})",
                "Grime off", ToolType.Brush, "hold and scrub over it", dirtLeft == 0);
        if (!smudgesFirst) Smudges(steps, plan, smudgesLeft);

        // 3. The parts: the broken one out, the fresh one in.
        for (int i = 0; i < plan.parts.Length; i++)
        {
            ReplaceablePart part = plan.parts[i];
            if (part == null) continue;
            string name = plan.partNames[i];
            bool replaced = part.IsReplaced;
            Add(steps, $"Lift out the {name}", "Old part out", ToolType.Tweezers, "hold on it, let go over the tray",
                replaced || part.State != ReplaceablePart.PartState.InPlace);
            Add(steps, "Seat the new one from the tray", "New part in", ToolType.Tweezers, "pinch it, let go over the empty seat", replaced);
        }

        // 4. The circuit.
        foreach (CircuitPuzzle circuit in plan.circuits)
            if (circuit != null)
                Add(steps, "Reconnect the circuit", "Circuit", ToolType.Hand, "the tiles, in order", circuit.Finished);

        // 5. Closing up, the last cover opened first.
        for (int i = covers.Count - 1; i >= 0; i--)
        {
            RemovablePart cover = covers[i];
            bool opened = plan.opened.Contains(cover);
            bool seated = cover.State == RemovablePart.CoverState.Seated;
            Add(steps, $"Put the {Lower(cover.DisplayName)} back on", Short(Lower(cover.DisplayName)) + " on", ToolType.Hand,
                "drag it home: it snaps on", opened && seated);
            int screws = Count(cover);
            if (screws > 0)
                Add(steps, screws == 1 ? "Put the screw back in" : "Put the screws back in", "Screws in", ToolType.Screwdriver,
                    "hold on each hole", opened && seated && !AnyOut(cover));
        }

        foreach (Step step in steps)
            if (!step.done) { step.current = true; break; }
    }

    /// <summary>The step to do now, or null when every step is done.</summary>
    public static Step Current(List<Step> steps)
    {
        foreach (Step step in steps) if (step.current) return step;
        return null;
    }

    static Plan PlanFor(JobBase job)
    {
        if (plans.TryGetValue(job, out Plan plan)) return plan;
        // Forget the jobs that are gone (handed back, walked out with).
        gone.Clear();
        foreach (JobBase j in plans.Keys) if (j == null) gone.Add(j);
        foreach (JobBase j in gone) plans.Remove(j);

        plan = new Plan();
        var covers = new List<RemovablePart>(job.GetComponentsInChildren<RemovablePart>());
        var parts = new List<ReplaceablePart>(job.GetComponentsInChildren<ReplaceablePart>());
        // Pieces already off the device (a close-up before this one): still this job's.
        foreach (GameObject piece in job.DetachedParts)
        {
            if (piece == null) continue;
            foreach (RemovablePart c in piece.GetComponentsInChildren<RemovablePart>()) if (!covers.Contains(c)) covers.Add(c);
            foreach (ReplaceablePart p in piece.GetComponentsInChildren<ReplaceablePart>()) if (!parts.Contains(p)) parts.Add(p);
        }
        foreach (GameObject piece in job.LooseParts)
        {
            if (piece == null) continue;
            foreach (ReplaceablePart p in piece.GetComponentsInChildren<ReplaceablePart>()) if (!parts.Contains(p)) parts.Add(p);
        }
        plan.covers = covers.ToArray();
        plan.parts = parts.ToArray();
        plan.partNames = new string[plan.parts.Length];
        for (int i = 0; i < plan.parts.Length; i++)
        {
            string name = plan.parts[i].DisplayName ?? "part";
            if (name.StartsWith("Old ")) name = name.Substring(4);
            plan.partNames[i] = Lower(name);
        }
        plan.circuits = job.GetComponentsInChildren<CircuitPuzzle>();
        foreach (GrimeSpot spot in job.GetComponentsInChildren<GrimeSpot>())
        {
            if (spot == null || spot.IsClean) continue;
            if (spot.Tool == ToolType.Cloth) plan.smudges++;
            else plan.dirt++;
        }
        plans[job] = plan;
        return plan;
    }

    static void Smudges(List<Step> steps, Plan plan, int smudgesLeft)
    {
        if (plan.smudges <= 0) return;
        Add(steps, plan.smudges == 1 ? "Wipe the smudge" : $"Wipe the smudges ({plan.smudges - Mathf.Min(smudgesLeft, plan.smudges)} of {plan.smudges})",
            "Smudge off", ToolType.Cloth, "hold and wipe in circles", smudgesLeft == 0);
    }

    static void Add(List<Step> steps, string text, string chip, ToolType tool, string how, bool done) =>
        steps.Add(new Step { text = text, chip = chip, tool = tool, how = how, done = done });

    static int Count(RemovablePart cover)
    {
        int n = 0;
        foreach (Screw s in cover.HeldBy) if (s != null) n++;
        return n;
    }

    static bool AnyOut(RemovablePart cover)
    {
        foreach (Screw s in cover.HeldBy) if (s != null && s.IsOut) return true;
        return false;
    }

    static bool AllOut(RemovablePart cover)
    {
        foreach (Screw s in cover.HeldBy) if (s != null && !s.IsOut) return false;
        return true;
    }

    static string Lower(string s) => string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant();

    // "back cover" → "Cover"; "back plate" → "Plate": the last word, capitalised, for the row of steps.
    static string Short(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Cover";
        string last = name.Trim();
        int space = last.LastIndexOf(' ');
        if (space >= 0) last = last.Substring(space + 1);
        return char.ToUpperInvariant(last[0]) + last.Substring(1);
    }

    /// <summary>The tool's name as the bench's caddy says it.</summary>
    public static string ToolName(ToolType tool) => tool switch
    {
        ToolType.Screwdriver => "Screwdriver",
        ToolType.Pry => "Pry tool",
        ToolType.Tweezers => "Tweezers",
        ToolType.Brush => "Brush",
        ToolType.Cloth => "Cloth",
        _ => "Hands",
    };
}
