// Small, non-persistent sequencing policy. A visit ending advances the lesson,
// not a key press (or an assumption that the customer was served successfully).
public sealed class DayOneOpening
{
    public enum Step { Inactive, Drink, Repair, Complete }

    public Step Current { get; private set; }
    public bool VisitInProgress { get; private set; }
    public bool IsActive => Current == Step.Drink || Current == Step.Repair;

    public bool AllowsFeatured(float dayFraction, float earliestArrival) =>
        !IsActive && dayFraction >= earliestArrival;

    public void Reset(bool enabled)
    {
        Current = enabled ? Step.Drink : Step.Inactive;
        VisitInProgress = false;
    }

    public bool TryStartVisit()
    {
        if (!IsActive || VisitInProgress) return false;
        VisitInProgress = true;
        return true;
    }

    // Declining, timing out, and being removed all end the attempt too. Never
    // strand the player waiting for a destroyed customer or reward a failure.
    public bool FinishVisit()
    {
        if (!VisitInProgress) return false;
        VisitInProgress = false;
        Current = Current == Step.Drink ? Step.Repair : Step.Complete;
        return true;
    }
}
