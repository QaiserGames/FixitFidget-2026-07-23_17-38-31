using System;

// ---------------------------------------------------------------------------
// CAFÉ REPUTATION: THE RULES
//
// Written up in claude/reputation-spec.md (decided 25 Sept 2026). The whole
// system is four rules:
//
//   1. Every customer who leaves a review moves the café's reputation:
//      Loved it +2 · Liked it +1 · Fine 0 · Let down -1 · Never again -2.
//   2. Stars arrive at 5, 30, 120, 300 and 580 reputation. A star you've
//      earned stays earned.
//   3. (Night side, not built yet.) Getting caught starts a scandal: one star
//      off for 5, 7, then 10 days, and 20 reputation lost.
//   4. Five stars and no scandal brings the franchise offer.
//
// WHY POINTS AND NOT AN AVERAGE
//
// Declining, running out of stock and a full shelf leave no review, because
// the recap already treats them as "a choice, not a failure". Under an
// average, that would make turning away every hard job the safest way to
// protect your score. Under points, a job you can finish is worth +2, and
// taking on more than you can finish is what costs you. That's the decision
// the counter is supposed to be about.
//
// No Unity types in here: Tests/ReputationRules compiles this file on its own,
// with stand-ins for the two enums it borrows (JobGrade, LostReason).
// ---------------------------------------------------------------------------

/// <summary>A customer's verdict on their visit. The number is the "stars" a
/// review site would show, but players see words and faces, so that "stars"
/// only ever means the café's own rating.</summary>
public enum Review
{
    None = 0,        // no review: declined, out of stock, shelf full
    NeverAgain = 1,  // -2
    LetDown = 2,     // -1
    Fine = 3,        //  0
    LikedIt = 4,     // +1
    LovedIt = 5      // +2
}

/// <summary>Why the verdict came out the way it did. Picks the review line.</summary>
public enum ReviewReason
{
    None,
    LovedRepair,
    LovedDrink,
    WaitedLong,
    PassableRepair,
    WaitedAndPassable,
    HelpedButUnhappy,
    WalkedOutInQueue,
    UnservedAtClose,
    WalkedOutAfterAccepting,
    RejectedRepair
}

/// <summary>The facts CustomerBrain.Depart already knows, and nothing else.</summary>
public struct ReviewFacts
{
    public bool happy;           // left satisfied
    public bool served;          // got something: a drink or a repair
    public bool accepted;        // you took their job
    public bool repairVisit;     // they came with a device (not only for a drink)
    public bool repairReturned;  // a repair was handed back, so grade means something
    public JobGrade grade;
    public LostReason reason;    // why an unhappy visit ended
    public float patienceAtExit; // 0..1
}

public struct Judgement
{
    public Review review;
    public ReviewReason reason;
    public Judgement(Review review, ReviewReason reason) { this.review = review; this.reason = reason; }
}

public static class ReputationRules
{
    /// <summary>Under this much patience when they left counts as "waited a long time".</summary>
    public const float LongWait = 0.25f;

    public const int MaxStars = 5;

    /// <summary>Reputation needed for stars 1 to 5. Tuned for 7-minute days as
    /// multiples of P, the reputation a good player earns on a normal mid-game
    /// day (~18 in the spec's simulation): 0.3P, 1.7P, 6.7P, 17P, 32P. Measure P
    /// from DayLogs once 7-minute days are playable and rescale all five.</summary>
    public static readonly int[] StarThresholds = { 5, 30, 120, 300, 580 };

    /// <summary>Working names (placeholders for the writers), index = stars.</summary>
    public static readonly string[] StarNames =
    {
        "Just opened",
        "Open for business",
        "Known on the street",
        "Neighbourhood favourite",
        "Talk of the district",
        "Best in the city"
    };

    public static Judgement Judge(ReviewFacts f)
    {
        // Handing a device back unfixed is the worst outcome, however politely
        // the visit ended.
        if (f.repairReturned && f.grade == JobGrade.Rejected)
            return new Judgement(Review.NeverAgain, ReviewReason.RejectedRepair);

        if (f.happy)
        {
            // NaN compares false, so a broken patience value never costs a step.
            bool waited = f.patienceAtExit < LongWait;
            bool passable = f.repairReturned && f.grade == JobGrade.Passable;
            if (waited && passable) return new Judgement(Review.Fine, ReviewReason.WaitedAndPassable);
            if (passable) return new Judgement(Review.LikedIt, ReviewReason.PassableRepair);
            if (waited) return new Judgement(Review.LikedIt, ReviewReason.WaitedLong);
            return new Judgement(Review.LovedIt, f.repairVisit ? ReviewReason.LovedRepair : ReviewReason.LovedDrink);
        }

        // Got a drink, didn't get the repair (or the other way round).
        if (f.served) return new Judgement(Review.LetDown, ReviewReason.HelpedButUnhappy);

        switch (f.reason)
        {
            // A choice, or not your fault today: no review at all.
            case LostReason.Declined:
            case LostReason.OutOfStock:
            case LostReason.ShelfFull:
                return new Judgement(Review.None, ReviewReason.None);

            case LostReason.StormedOutWaiting:
                return f.accepted
                    ? new Judgement(Review.NeverAgain, ReviewReason.WalkedOutAfterAccepting)
                    : new Judgement(Review.LetDown, ReviewReason.WalkedOutInQueue);

            case LostReason.StillInShopAtClose:
                return new Judgement(Review.LetDown, ReviewReason.UnservedAtClose);

            default: // StormedOutInQueue, and anything added later
                return new Judgement(Review.LetDown, ReviewReason.WalkedOutInQueue);
        }
    }

    public static Review Verdict(ReviewFacts f) => Judge(f).review;

    /// <summary>+2, +1, 0, -1, -2, and 0 for no review.</summary>
    public static int Points(Review review) =>
        review == Review.None ? 0 : Math.Max(-2, Math.Min(2, (int)review - 3));

    /// <summary>Stars for this much reputation. Never fewer than already
    /// earned: bad days slow you down, they don't take stars away.</summary>
    public static int StarsFor(int reputation, int alreadyEarned)
    {
        int stars = Math.Max(0, Math.Min(MaxStars, alreadyEarned));
        while (stars < MaxStars && reputation >= StarThresholds[stars]) stars++;
        return stars;
    }

    /// <summary>Reputation needed for the next star, or -1 at five stars.</summary>
    public static int NextThreshold(int stars) =>
        stars >= MaxStars ? -1 : StarThresholds[Math.Max(0, stars)];

    /// <summary>Reputation at which the current star was reached (0 for none).</summary>
    public static int CurrentThreshold(int stars) =>
        stars <= 0 ? 0 : StarThresholds[Math.Min(MaxStars, stars) - 1];

    public static string NameOf(int stars) => StarNames[Math.Max(0, Math.Min(MaxStars, stars))];

    public static string Label(Review review) => review switch
    {
        Review.LovedIt => "Loved it",
        Review.LikedIt => "Liked it",
        Review.Fine => "Fine",
        Review.LetDown => "Let down",
        Review.NeverAgain => "Never again",
        _ => "No review"
    };

    /// <summary>Fills {name}, {thing} and {drink} in a review line. A line that
    /// starts with a token still starts with a capital letter.</summary>
    public static string Fill(string template, string name, string thing, string drink)
    {
        if (string.IsNullOrEmpty(template)) return "";
        string line = template
            .Replace("{name}", Attribution(name))
            .Replace("{thing}", string.IsNullOrWhiteSpace(thing) ? "thing" : InSentence(thing))
            .Replace("{drink}", string.IsNullOrWhiteSpace(drink) ? "coffee" : InSentence(drink));
        return line.Length > 0 && char.IsLower(line[0]) ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
    }

    /// <summary>A review as the recap quotes it: “line” — name. Non-breaking
    /// spaces keep the signature on the same line as the end of the quote, so
    /// a name never wraps onto a line of its own. Null for an empty line.</summary>
    public static string Quote(string line, string name)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        return "\u201C" + line.Trim() + "\u201D\u00A0\u2014\u00A0" + Attribution(name).Replace(' ', '\u00A0');
    }

    /// <summary>Who signs a review. Once the visit roster runs out of names it
    /// calls people "Walk-in 1", "Walk-in 2" and so on (CustomerVisitRoster.TakeName).
    /// A quote signed like that reads as a placeholder, so it says "a walk-in".</summary>
    public static string Attribution(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "a customer";
        name = name.Trim();
        return name.StartsWith("Walk-in ", StringComparison.Ordinal) ? "a walk-in" : name;
    }

    /// <summary>Devices and drinks are named in Title Case in the data
    /// ("Pocket Watch", "Hot Chocolate"). Inside a sentence they read in lower
    /// case: "my pocket watch". Only words written as a capital followed by
    /// lower-case letters change, so names like "TV" or "iPod" keep their spelling.</summary>
    public static string InSentence(string noun)
    {
        if (string.IsNullOrWhiteSpace(noun)) return "";
        string[] words = noun.Trim().Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (word.Length < 2 || !char.IsUpper(word[0])) continue;
            bool restLower = true;
            for (int c = 1; c < word.Length && restLower; c++)
                restLower = !char.IsLetter(word[c]) || char.IsLower(word[c]);
            if (restLower) words[i] = char.ToLowerInvariant(word[0]) + word.Substring(1);
        }
        return string.Join(" ", words);
    }
}
