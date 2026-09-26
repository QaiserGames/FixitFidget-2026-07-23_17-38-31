using UnityEngine;

// ---------------------------------------------------------------------------
// What customers write about their visit, quoted in the end-of-day recap.
//
// DRAFT LINES. Every line below is a placeholder for the writers to replace:
// edit them in the ReviewLines asset (Assets/Data/Reputation), not here. The
// defaults only fill a new asset, or stand in when a scene has none.
//
// Tokens: {name} the customer, {thing} what they brought (or the drink they
// came for), {drink} a drink they wanted alongside a repair. Keep lines short:
// the recap gives each one a single line of about fifty characters.
// ---------------------------------------------------------------------------
[CreateAssetMenu(fileName = "ReviewLines", menuName = "FixitFiasco/Review Lines")]
public class ReviewLines : ScriptableObject
{
    [Header("Loved it (+2)")]
    [TextArea(1, 2)] public string[] lovedRepair =
    {
        "My {thing} works again. I could cry.",
        "Fixed my {thing} while I sat there. Magic.",
        "In broken, out working. Ten out of ten."
    };
    [TextArea(1, 2)] public string[] lovedDrink =
    {
        "Best {thing} on the street. Don't tell anyone.",
        "Perfect {thing}. Back tomorrow.",
        "Came for a {thing}, stayed for the vibes."
    };

    [Header("Liked it (+1)")]
    [TextArea(1, 2)] public string[] waitedLong =
    {
        "Took a while, but my {thing} is sorted.",
        "Worth the wait. Just about.",
        "Slow, but they got there."
    };
    [TextArea(1, 2)] public string[] passableRepair =
    {
        "My {thing} mostly works. Mostly.",
        "Fixed-ish. I'll take it.",
        "It works if I hold it at an angle."
    };

    [Header("Fine (0)")]
    [TextArea(1, 2)] public string[] waitedAndPassable =
    {
        "Long wait, and my {thing} still rattles.",
        "It's fine. It's... fine.",
        "Half an afternoon for half a repair."
    };

    [Header("Let down (-1)")]
    [TextArea(1, 2)] public string[] helpedButUnhappy =
    {
        "Nice coffee. Still no {thing}.",
        "Got half of what I came for.",
        "Lovely place. Shame about my {thing}."
    };
    [TextArea(1, 2)] public string[] walkedOutInQueue =
    {
        "Stood in line forever. Nobody noticed me.",
        "The queue didn't move, so I did.",
        "Waited, waved, gave up."
    };
    [TextArea(1, 2)] public string[] unservedAtClose =
    {
        "They closed before they got to me.",
        "Still waiting when the lights went off."
    };

    [Header("Never again (-2)")]
    [TextArea(1, 2)] public string[] walkedOutAfterAccepting =
    {
        "Took my {thing}, then forgot I existed.",
        "Handed over my {thing}. Waited. And waited.",
        "Never again. I'm taking my {thing} elsewhere."
    };
    [TextArea(1, 2)] public string[] rejectedRepair =
    {
        "Gave me my {thing} back, still broken.",
        "Still broken. At least it was free."
    };

    public string[] For(ReviewReason reason) => reason switch
    {
        ReviewReason.LovedRepair => lovedRepair,
        ReviewReason.LovedDrink => lovedDrink,
        ReviewReason.WaitedLong => waitedLong,
        ReviewReason.PassableRepair => passableRepair,
        ReviewReason.WaitedAndPassable => waitedAndPassable,
        ReviewReason.HelpedButUnhappy => helpedButUnhappy,
        ReviewReason.WalkedOutInQueue => walkedOutInQueue,
        ReviewReason.UnservedAtClose => unservedAtClose,
        ReviewReason.WalkedOutAfterAccepting => walkedOutAfterAccepting,
        ReviewReason.RejectedRepair => rejectedRepair,
        _ => null
    };
}
