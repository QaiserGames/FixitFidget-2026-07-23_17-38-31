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
//
// A step also knows when its parts lie on the face turned down (Under; 12-13 Oct, his controller playtest: "rotating and
// just trying to flip the phone ... was near impossible" and nothing said when to): the card then says to flip the device
// over instead of "hold on each one", and the aim help's "next part" (ItemInspector) follows the same order as this list.
//
// CHECKLIST V2 AND THE FORGIVING BENCH (14 Oct 2026; uiux-checklist-v2.md §5.2, §5.10 item 2; systems-forgiving-bench.md §4.5;
// contracts §5). Each step also carries its verb ("Unscrew") and icon key (a name under Resources/UI/BenchIcons/), its TARGETS
// (the pieces still to do in it, in the state they are in now: the glow lights them, the bench's picker puts them first, the
// D-pad goes to them even when something hides them), and left / total for the card's pips. Built from the parts' own states
// every time, so it stays true while pieces fly home by themselves; a piece in flight is never a target.
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
        /// <summary>Every part this step works on lies on the face turned down: the device has to be flipped over first.</summary>
        public bool under;
        public bool done;
        public bool current;
        /// <summary>The step in one or two words, under its icon on the card ("Unscrew", "Press in").</summary>
        public string verb;
        /// <summary>Its icon: a Resources name under UI/BenchIcons/ (screwdriver, pry, tweezers, brush, cloth, screw, newpart,
        /// cover, circuit, or a device's own part_* / cover_* when that icon exists).</summary>
        public string icon;
        /// <summary>What is still to do in it, as it is now (screws still in, the cover, spots not clean and not under a cover, the
        /// broken part in its seat, the fresh part on its pad or hovering, the cover in its well, the empty holes).</summary>
        public List<Component> targets = new List<Component>();
        /// <summary>The pips: how many of the step's pieces are still to do, of how many.</summary>
        public int left;
        public int total;
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
    static void ResetStatics() { plans.Clear(); iconExists.Clear(); }

    /// <summary>The steps of <paramref name="job"/>'s repair, as they stand now (cleared and filled).</summary>
    public static void Build(JobBase job, List<Step> steps)
    {
        steps.Clear();
        if (job == null) return;
        Plan plan = PlanFor(job);
        string device = DeviceKind(job);

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
            {
                Step unscrew = Add(steps, screws == 1 ? "Take out the screw" : $"Take out the {screws} screws", "Screws out", ToolType.Screwdriver,
                    "hold on each one", off || AllOut(cover), ScrewsUnder(cover, false), "Unscrew", "screwdriver");
                // The screws still in (a screw backing out is still in until it frees).
                if (!unscrew.done) foreach (Screw s in cover.HeldBy) if (s != null && !s.IsOut) unscrew.targets.Add(s);
                Pips(unscrew, screws, unscrew.done ? 0 : screws - OutCount(cover));
            }
            Step pry = Add(steps, $"Pry off the {name}", Short(name) + " off", ToolType.Pry,
                cover.IsPopped ? "it lifts off by itself" : "hold at its edge", off, !off && Under(cover.Outward), "Pry off", "pry");
            if (!off && (cover.State == RemovablePart.CoverState.Seated || cover.State == RemovablePart.CoverState.Popped)) pry.targets.Add(cover);
            Pips(pry, 1, pry.done ? 0 : 1);
        }

        // 2. The grime and the smudges. A done one first; while both are left, the one the device's first dirty spot wants (the
        // Day 1 guide names that spot's tool too: the two never point at different tools, the lab's photo, 12 Oct).
        // A spot is on the underside when it lies below the device's middle (a patch's own up isn't its side: the watch's
        // debris, reached from the back, is authored with its up toward the dial). A spot under a cover is not a target (the
        // cover is: it is in the cover's own steps); it is still left.
        int dirtLeft = 0, smudgesLeft = 0, dirtUnder = 0, smudgesUnder = 0;
        GrimeSpot firstLeft = null;
        Vector3 middle = DeviceBounds.TryGet(job.gameObject, out Bounds box) ? box.center : job.transform.position;
        var dirtTargets = new List<Component>();
        var smudgeTargets = new List<Component>();
        foreach (GrimeSpot spot in job.GetComponentsInChildren<GrimeSpot>())
        {
            if (spot == null || spot.IsClean) continue;
            if (firstLeft == null) firstLeft = spot;
            bool under = Under(BenchInteractable.BoundsCentre(spot.transform) - middle);
            bool covered = spot.CoveredBy != null;
            if (spot.Tool == ToolType.Cloth) { smudgesLeft++; if (under) smudgesUnder++; if (!covered) smudgeTargets.Add(spot); }
            else { dirtLeft++; if (under) dirtUnder++; if (!covered) dirtTargets.Add(spot); }
        }
        bool smudgesFirst = plan.dirt > 0 && plan.smudges > 0
            && (smudgesLeft == 0 || dirtLeft > 0 && firstLeft != null && firstLeft.Tool == ToolType.Cloth);
        if (smudgesFirst) Smudges(steps, plan, smudgesLeft, smudgesUnder, smudgeTargets);
        if (plan.dirt > 0)
        {
            Step brush = Add(steps, plan.dirt == 1 ? "Brush off the grime" : $"Brush off the grime ({plan.dirt - Mathf.Min(dirtLeft, plan.dirt)} of {plan.dirt})",
                "Grime off", ToolType.Brush, "drag the brush over it", dirtLeft == 0, dirtLeft > 0 && dirtUnder == dirtLeft, "Brush", "brush");
            brush.targets.AddRange(dirtTargets);
            Pips(brush, plan.dirt, Mathf.Min(dirtLeft, plan.dirt));
        }
        if (!smudgesFirst) Smudges(steps, plan, smudgesLeft, smudgesUnder, smudgeTargets);

        // 3. The parts: the broken one out, the fresh one in.
        for (int i = 0; i < plan.parts.Length; i++)
        {
            ReplaceablePart part = plan.parts[i];
            if (part == null) continue;
            string name = plan.partNames[i];
            bool replaced = part.IsReplaced;
            bool seatUnder = Under(part.Outward);
            Step lift = Add(steps, $"Lift out the {name}", "Old part out", ToolType.Tweezers, "hold on it",
                replaced || part.State != ReplaceablePart.PartState.InPlace, seatUnder, "Lift out", "tweezers");
            if (!lift.done) lift.targets.Add(part);
            Pips(lift, 1, lift.done ? 0 : 1);
            Step press = Add(steps, "Seat the new one", "New part in", ToolType.Tweezers, "hold on it: it flies over and presses in", replaced, seatUnder,
                "Press in", DeviceIcon("part_", device, "newpart"));
            // The fresh part on its pad or hovering over its seat (never while it flies). Its seat is the picker's too.
            LoosePart fresh = part.Fresh;
            if (!replaced && fresh != null && fresh.isActiveAndEnabled && !fresh.Flying) press.targets.Add(fresh);
            Pips(press, 1, replaced ? 0 : 1);
        }

        // 4. The circuit.
        foreach (CircuitPuzzle circuit in plan.circuits)
            if (circuit != null)
            {
                Step reconnect = Add(steps, "Reconnect the circuit", "Circuit", ToolType.Hand, "the tiles, in order", circuit.Finished, false, "Reconnect", "circuit");
                Pips(reconnect, 1, reconnect.done ? 0 : 1);
            }

        // 5. Closing up, the last cover opened first.
        for (int i = covers.Count - 1; i >= 0; i--)
        {
            RemovablePart cover = covers[i];
            bool opened = plan.opened.Contains(cover);
            bool seated = cover.State == RemovablePart.CoverState.Seated;
            Step on = Add(steps, $"Put the {Lower(cover.DisplayName)} back on", Short(Lower(cover.DisplayName)) + " on", ToolType.Hand,
                "click it in its well: it flies home", opened && seated, !seated && Under(cover.Outward), "Cover on", DeviceIcon("cover_", device, "cover"));
            if (cover.InWell) on.targets.Add(cover);
            Pips(on, 1, on.done ? 0 : 1);
            int screws = Count(cover);
            if (screws > 0)
            {
                Step back = Add(steps, screws == 1 ? "Put the screw back in" : "Put the screws back in", "Screws in", ToolType.Screwdriver,
                    "hold on each hole", opened && seated && !AnyOut(cover), ScrewsUnder(cover, true), "Screws back", "screw");
                // The empty holes (the driver fetches each screw from its cup), once the cover they hold is home.
                if (!back.done && seated)
                    foreach (Screw s in cover.HeldBy)
                        if (s != null && s.IsOut && s.Socket != null && s.Socket.isActiveAndEnabled) back.targets.Add(s.Socket);
                Pips(back, screws, back.done ? 0 : OutCount(cover));
            }
        }

        foreach (Step step in steps)
            if (!step.done) { step.current = true; break; }
    }

    /// <summary>The step to do now, or null when every step is done.</summary>
    public static Step Current(IReadOnlyList<Step> steps)
    {
        if (steps == null) return null;
        foreach (Step step in steps) if (step.current) return step;
        return null;
    }

    /// <summary>
    /// A part's outward points down: it lies on the face turned to the mat, and the device has to be flipped over to reach it
    /// (within 45° of straight down, so a peeked edge, 35° up, still reads as the face it is on).
    /// </summary>
    public static bool Under(Vector3 outward) => Vector3.Dot(outward.normalized, Vector3.down) > .7f;

    // Every screw of the cover still to be worked (in, to come out; or out, to go back in) sits on the face turned down.
    static bool ScrewsUnder(RemovablePart cover, bool goingIn)
    {
        bool any = false;
        foreach (Screw s in cover.HeldBy)
        {
            if (s == null || s.IsOut != goingIn) continue;
            any = true;
            if (!Under(s.Axis)) return false;
        }
        return any;
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

    static void Smudges(List<Step> steps, Plan plan, int smudgesLeft, int smudgesUnder, List<Component> targets)
    {
        if (plan.smudges <= 0) return;
        Step wipe = Add(steps, plan.smudges == 1 ? "Wipe the smudge" : $"Wipe the smudges ({plan.smudges - Mathf.Min(smudgesLeft, plan.smudges)} of {plan.smudges})",
            "Smudge off", ToolType.Cloth, "drag the cloth over it", smudgesLeft == 0, smudgesLeft > 0 && smudgesUnder == smudgesLeft, "Wipe", "cloth");
        wipe.targets.AddRange(targets);
        Pips(wipe, plan.smudges, Mathf.Min(smudgesLeft, plan.smudges));
    }

    static Step Add(List<Step> steps, string text, string chip, ToolType tool, string how, bool done, bool under, string verb, string icon)
    {
        var step = new Step { text = text, chip = chip, tool = tool, how = how, done = done, under = under && !done, verb = verb, icon = icon };
        steps.Add(step);
        return step;
    }

    static void Pips(Step step, int total, int left)
    {
        step.total = Mathf.Max(0, total);
        step.left = Mathf.Clamp(left, 0, step.total);
    }

    // The device's own icons (part_phone, cover_watch, ...) when they exist, else the generic one (UI/UX §5.3: the per-device
    // set is "if time allows"). Looked up once per name.
    static readonly Dictionary<string, bool> iconExists = new();

    static string DeviceIcon(string prefix, string device, string generic)
    {
        if (string.IsNullOrEmpty(device)) return generic;
        string key = prefix + device;
        if (!iconExists.TryGetValue(key, out bool exists))
        {
            exists = Resources.Load<Sprite>("UI/BenchIcons/" + key) != null || Resources.Load<Texture2D>("UI/BenchIcons/" + key) != null;
            iconExists[key] = exists;
        }
        return exists ? key : generic;
    }

    // Which of the bench's devices this is, by its own name ("Phone", "Pocket Watch", "reunion film camera"), or "".
    static string DeviceKind(JobBase job)
    {
        DeviceDefinition definition = job != null ? job.GetComponent<DeviceDefinition>() : null;
        string name = definition != null ? Lower(definition.displayName) : "";
        if (name.Contains("watch")) return "watch";
        if (name.Contains("phone")) return "phone";
        if (name.Contains("camera")) return "camera";
        return "";
    }

    static int OutCount(RemovablePart cover)
    {
        int n = 0;
        foreach (Screw s in cover.HeldBy) if (s != null && s.IsOut) n++;
        return n;
    }

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
