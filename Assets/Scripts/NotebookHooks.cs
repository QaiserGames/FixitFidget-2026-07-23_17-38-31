// ---------------------------------------------------------------------------
// Where the café's day feeds Ace's notebook (claude/night-notebook-spec.md).
// Two moments, both things Ace has just been told at the counter:
//   * a regular's intake: Grace's camera story, or any other regular's repair;
//   * Grace's return, once her job is accepted and she leaves the print.
// Walk-ins are anonymous by design and never get entries. Without a
// SaveManager (a bare test scene) nothing is recorded.
// ---------------------------------------------------------------------------
public static class NotebookHooks
{
    public static int Today => DayClock.Instance != null ? DayClock.Instance.Day : 0;

    /// <summary>The player has just heard <paramref name="identity"/>'s intake for <paramref name="job"/>.</summary>
    public static int HeardIntake(CustomerIdentity identity, Job job)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || identity == null || !identity.IsRegular || identity.Profile == null) return 0;

        int learned = 0;
        if (identity.IsGraceCameraRequest)
        {
            foreach (NotebookFactData fact in NotebookEntries.GraceIntake(identity.DisplayName))
                if (notebook.Learn(fact, Today)) learned++;
        }
        else if (job != null && job.kind == JobKind.Repair)
        {
            NotebookFactData fact = NotebookEntries.RegularRepair(identity.Profile.PersistentId, identity.DisplayName,
                job.deviceName, job.faultDescription);
            if (fact != null && notebook.Learn(fact, Today)) learned++;
        }
        return learned;
    }

    /// <summary>Grace's return has been accepted and her photo outcome settled.</summary>
    public static bool GraceReturned(string name, GracePhotoOutcome outcome)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NotebookEntries.GraceReturn(name, outcome);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }
}
