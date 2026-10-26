using UnityEngine;

public class ItemInteractable : Interactable
{
    private JobBase job;

    private void Awake()
    {
        job = GetComponentInParent<JobBase>();
        if (job == null) job = GetComponent<JobBase>();
    }

    /// <summary>The device or cup this picks up.</summary>
    public JobBase Job => job;

    /// <summary>
    /// A repair sitting on the bench: it can be worked on where it is, hands full or not (stations as reach, playtest 3:
    /// E on it from above, click or RT on it in first person; PlayerInteractor decides which).
    /// 24 Oct 2026 (Days, the side project): a thrift find on the bench's corner counts as owned (SideProject: nobody's job).
    /// </summary>
    public bool OnBench => job != null && !(job is DrinkJob)
                           && (job.Owner != null && !job.Owner.IsCounterRepair || SideProject.IsSideProject(job))
                           && StationInteractable.BenchHolds(job);

    /// <summary>A thrift find (24 Oct 2026): worked on at the bench's corner, never carried off (it sells at closing).</summary>
    public bool IsFind => SideProject.IsSideProject(job);

    /// <summary>Nothing left to do on it: every fault fixed and nothing off it (what "Pick up (Fixed)" means).</summary>
    public bool Fixed => job != null && job.IsComplete && job.CanHandBack;

    /// <summary>A free hand to take it in.</summary>
    public static bool HandFree
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;
            return carry != null && carry.HasSpace;
        }
    }

    public override bool IsAvailable
    {
        get
        {
            if (job == null) return false;
            if (job.Owner != null && job.Owner.IsCounterRepair) return false;

            // A cup locked in the machine isn't yours to take yet.
            DrinkJob drink = job as DrinkJob;
            if (drink != null && drink.Locked) return false;

            // 24 Oct 2026 (Days): a find (nobody's) is worked on on its corner of the bench, never picked up; on the thrift
            // shelf E is the shelf's ("Work on your thrift watch"), and fixed it is the shelf's again.
            if (IsFind) return OnBench && !Fixed;

            if (drink == null && job.Owner == null) return false;

            // On the bench it can be worked on with full hands; anywhere else it's only ever picked up.
            return OnBench || HandFree;
        }
    }

    public override string Prompt => IsFind ? WorkOnFindWords(job)
        : job is DrinkJob cup && !cup.IsEmpty
        ? $"Take {cup.Drink.drinkName} ({cup.FreshnessStage.ToString().ToLowerInvariant()})"
        : !HandFree ? "Hands full"
        : OnBench && Fixed ? "Pick up (Fixed)" : "Pick up";

    /// <summary>"Work on your thrift watch" (24 Oct 2026).</summary>
    static string WorkOnFindWords(JobBase find)
    {
        SideProject marker = SideProject.Of(find);
        return "Work on " + (marker != null ? marker.YourThing.Replace("Your", "your") : "your thrift find");
    }

    public override void Interact(PlayerInteractor player)
    {
        // 24 Oct 2026 (Days): a find is never carried: E on it works on it, in either view.
        if (IsFind)
        {
            if (OnBench && !Fixed) player.WorkOn(this);
            return;
        }

        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        if (!carry.TryPickUp(job)) return;

        // Taking something from the drinks close-up keeps the close-up (the next cup is made there); anything else
        // taken while in one means walking somewhere.
        if (player.IsAtStation && player.CurrentStation.GetComponent<BeverageStation>() == null) player.ExitStation();
    }
}
