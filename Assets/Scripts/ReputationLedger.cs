using System;
using System.Collections.Generic;

/// <summary>One review, as the recap quotes it.</summary>
public sealed class ReviewEntry
{
    public Review review;
    public ReviewReason reason;
    public string name = "";
    public string thing = "";   // the device they brought, or the drink they came for
    public string drink = "";   // a drink they wanted alongside a repair, if any
    public bool regular;
    /// <summary>A line already written (the planted review): used as it is, instead of one of the writers' lines.</summary>
    public string line = "";
}

/// <summary>
/// One of the day's reviews as the recap phone shows it (playtest 2, step 2): who signed it, the
/// verdict (1-5 stars), the kind of visit, and what they wrote, without quotation marks or a
/// signature. Every review gets one, in the order they were written.
/// </summary>
public sealed class ReviewCard
{
    public string name = "";    // as signed: "Grace", "a walk-in"
    public Review review;
    public ReviewReason reason;
    public string line = "";
    public bool regular;

    public int Stars => ReputationRules.StarsOf(review);
}

// ---------------------------------------------------------------------------
// The café's reputation between days and sessions, in the same shape as
// CustomerMemoryService: this class owns the numbers, SaveManager decides when
// and where they are saved, and DayClock decides when a day is settled.
//
// A day's reviews are collected as people leave and only count at closing
// (Settle). Settling is once per day: a recap that is resumed, or saved again
// after a purchase, never counts the same day twice.
//
// No Unity types: Tests/ReputationRules compiles this file on its own.
// ---------------------------------------------------------------------------
public sealed class ReputationLedger
{
    private readonly List<ReviewEntry> today = new();
    private readonly int[] counts = new int[6];            // index = (int)Review
    private readonly List<string> quotes = new();
    private readonly List<Review> quoteReviews = new();
    private readonly List<ReviewCard> cards = new();
    private readonly List<ReviewCard> live = new();   // today's cards as they come in (the phone by day)

    public int Reputation { get; private set; }
    public int StarsEarned { get; private set; }
    public int Day { get; private set; } = 1;

    /// <summary>True once today's reviews have been counted (from closing until tomorrow opens).</summary>
    public bool Settled { get; private set; }

    /// <summary>Today's reputation change: a running total until closing, then final.</summary>
    public int TodayChange { get; private set; }

    /// <summary>Stars held when today opened, so the recap can say "new star".</summary>
    public int StarsBefore { get; private set; }

    public IReadOnlyList<string> Quotes => quotes;
    public IReadOnlyList<Review> QuoteReviews => quoteReviews;

    /// <summary>Every review of the settled day, in the order they were written (the recap phone).
    /// Empty before closing, and for a recap saved before every review got a line.</summary>
    public IReadOnlyList<ReviewCard> Cards => cards;

    /// <summary>Today's reviews as cards, as they come in (playtest 3, session 3: the phone by day): each written when it
    /// is recorded, with the same line closing gives it (the writer is deterministic). Once settled, the settled cards.</summary>
    public IReadOnlyList<ReviewCard> TodayCards => Settled ? cards : live;

    public int Count(Review review) => review == Review.None ? 0 : counts[(int)review];

    public int ReviewCount
    {
        get { int n = 0; for (int i = 1; i < counts.Length; i++) n += counts[i]; return n; }
    }

    public bool EarnedStarToday => Settled && StarsEarned > StarsBefore;

    public int NextThreshold => ReputationRules.NextThreshold(StarsEarned);

    public void Restore(int reputation, int starsEarned, int day, bool dayCompleted, RecapSaveData recap)
    {
        Reputation = Math.Max(0, reputation);
        StarsEarned = ReputationRules.StarsFor(Reputation, starsEarned);
        ClearToday(day);

        if (!dayCompleted || recap == null) return;

        // Resuming a closed day: show what was settled, never settle it again.
        Settled = true;
        counts[(int)Review.LovedIt] = Math.Max(0, recap.reviewsLoved);
        counts[(int)Review.LikedIt] = Math.Max(0, recap.reviewsLiked);
        counts[(int)Review.Fine] = Math.Max(0, recap.reviewsFine);
        counts[(int)Review.LetDown] = Math.Max(0, recap.reviewsLetDown);
        counts[(int)Review.NeverAgain] = Math.Max(0, recap.reviewsNeverAgain);
        TodayChange = recap.reputationChange;
        StarsBefore = Math.Max(0, Math.Min(StarsEarned, recap.starsBefore));

        string[] savedQuotes = recap.reviewQuotes ?? Array.Empty<string>();
        int[] savedReviews = recap.reviewQuoteVerdicts ?? Array.Empty<int>();
        for (int i = 0; i < savedQuotes.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(savedQuotes[i])) continue;
            int verdict = i < savedReviews.Length ? savedReviews[i] : 0;
            quotes.Add(savedQuotes[i]);
            quoteReviews.Add(verdict >= 1 && verdict <= 5 ? (Review)verdict : Review.None);
        }

        // Every review, as the phone showed it (absent in recaps saved before the phone).
        string[] names = recap.reviewCardNames ?? Array.Empty<string>();
        string[] lines = recap.reviewCardLines ?? Array.Empty<string>();
        int[] verdicts = recap.reviewCardVerdicts ?? Array.Empty<int>();
        int[] reasons = recap.reviewCardReasons ?? Array.Empty<int>();
        bool[] regulars = recap.reviewCardRegulars ?? Array.Empty<bool>();
        int cardCount = Math.Min(lines.Length, verdicts.Length);
        for (int i = 0; i < cardCount; i++)
        {
            if (verdicts[i] < 1 || verdicts[i] > 5 || string.IsNullOrWhiteSpace(lines[i])) continue;
            int reason = i < reasons.Length ? reasons[i] : 0;
            cards.Add(new ReviewCard
            {
                name = i < names.Length && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : "a customer",
                review = (Review)verdicts[i],
                reason = Enum.IsDefined(typeof(ReviewReason), reason) ? (ReviewReason)reason : ReviewReason.None,
                line = lines[i],
                regular = i < regulars.Length && regulars[i]
            });
        }
    }

    /// <summary>Tomorrow has been saved: start collecting its reviews.</summary>
    public void BeginDay(int day) => ClearToday(day);

    /// <summary>Adds one review to today. Ignored after closing and for "no review". <paramref name="writeCard"/>, if
    /// given, writes its card's line now (the phone by day shows it at once).</summary>
    public bool Record(ReviewEntry entry, Func<ReviewEntry, string> writeCard = null)
    {
        if (Settled || entry == null || entry.review == Review.None) return false;
        today.Add(entry);
        counts[(int)entry.review]++;
        TodayChange += ReputationRules.Points(entry.review);
        if (writeCard != null) live.Add(CardOf(entry, writeCard(entry)));
        return true;
    }

    // A review's card: as signed, its verdict, and its line (or the verdict's label when no line was written).
    private static ReviewCard CardOf(ReviewEntry r, string line) => new ReviewCard
    {
        name = ReputationRules.Attribution(r.name),
        review = r.review,
        reason = r.reason,
        // A kind of visit with no lines written still shows its verdict.
        line = string.IsNullOrWhiteSpace(line) ? ReputationRules.Label(r.review) + "." : line.Trim(),
        regular = r.regular
    };

    /// <summary>Counts today's reviews into the café's reputation. Once per day.
    /// <paramref name="write"/> turns a quoted review into its line of text; it
    /// gets the review and its position (0 best, 1 worst, 2 a regular's).
    /// <paramref name="writeCard"/>, if given, writes the line every review shows
    /// on the recap phone (plain: no quotation marks, no signature).</summary>
    public void Settle(int day, Func<ReviewEntry, int, string> write, Func<ReviewEntry, string> writeCard = null)
    {
        if (Settled) return;

        Day = Math.Max(1, day);
        StarsBefore = StarsEarned;
        Reputation = Math.Max(0, Reputation + TodayChange);
        StarsEarned = ReputationRules.StarsFor(Reputation, StarsEarned);

        quotes.Clear();
        quoteReviews.Clear();
        List<ReviewEntry> picked = PickQuotes(today);
        for (int i = 0; i < picked.Count; i++)
        {
            string line = write != null ? write(picked[i], i) : null;
            if (string.IsNullOrWhiteSpace(line)) continue;
            quotes.Add(line);
            quoteReviews.Add(picked[i].review);
        }

        cards.Clear();
        if (writeCard != null)
            foreach (ReviewEntry r in today)
            {
                if (r == null || r.review == Review.None) continue;
                cards.Add(CardOf(r, writeCard(r)));
            }

        Settled = true;
    }

    public void WriteRecap(RecapSaveData recap)
    {
        if (recap == null) return;
        recap.reviewsLoved = counts[(int)Review.LovedIt];
        recap.reviewsLiked = counts[(int)Review.LikedIt];
        recap.reviewsFine = counts[(int)Review.Fine];
        recap.reviewsLetDown = counts[(int)Review.LetDown];
        recap.reviewsNeverAgain = counts[(int)Review.NeverAgain];
        recap.reputationChange = TodayChange;
        recap.starsBefore = StarsBefore;
        recap.reviewQuotes = quotes.ToArray();
        int[] verdicts = new int[quoteReviews.Count];
        for (int i = 0; i < verdicts.Length; i++) verdicts[i] = (int)quoteReviews[i];
        recap.reviewQuoteVerdicts = verdicts;

        recap.reviewCardNames = new string[cards.Count];
        recap.reviewCardLines = new string[cards.Count];
        recap.reviewCardVerdicts = new int[cards.Count];
        recap.reviewCardReasons = new int[cards.Count];
        recap.reviewCardRegulars = new bool[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            recap.reviewCardNames[i] = cards[i].name;
            recap.reviewCardLines[i] = cards[i].line;
            recap.reviewCardVerdicts[i] = (int)cards[i].review;
            recap.reviewCardReasons[i] = (int)cards[i].reason;
            recap.reviewCardRegulars[i] = cards[i].regular;
        }
    }

    /// <summary>Best review, worst review (only if it is worse than the best),
    /// then a regular's if neither of those was one. Ties go to regulars, then
    /// to whoever left first.</summary>
    public static List<ReviewEntry> PickQuotes(IReadOnlyList<ReviewEntry> reviews)
    {
        var picked = new List<ReviewEntry>(3);
        if (reviews == null || reviews.Count == 0) return picked;

        ReviewEntry best = null, worst = null;
        foreach (ReviewEntry r in reviews)
        {
            if (r == null || r.review == Review.None) continue;
            if (best == null || r.review > best.review || (r.review == best.review && r.regular && !best.regular)) best = r;
            if (worst == null || r.review < worst.review || (r.review == worst.review && r.regular && !worst.regular)) worst = r;
        }
        if (best == null) return picked;

        picked.Add(best);
        if (worst != null && worst.review < best.review) picked.Add(worst);

        if (!picked.Exists(p => p.regular))
        {
            for (int i = reviews.Count - 1; i >= 0; i--)
            {
                ReviewEntry r = reviews[i];
                if (r != null && r.regular && r.review != Review.None && !picked.Contains(r)) { picked.Add(r); break; }
            }
        }
        return picked;
    }

    /// <summary>
    /// The three review cards worth a line on the closing screen, by the rule the quotes use (PickQuotes): the best,
    /// the worst (only if worse than the best), then a regular's if neither was one. Ties go to regulars, then to
    /// whoever left first. Empty for none.
    /// </summary>
    public static List<ReviewCard> PickCards(IReadOnlyList<ReviewCard> cards)
    {
        var picked = new List<ReviewCard>(3);
        if (cards == null || cards.Count == 0) return picked;
        ReviewCard best = null, worst = null;
        foreach (ReviewCard c in cards)
        {
            if (c == null || c.review == Review.None) continue;
            if (best == null || c.review > best.review || (c.review == best.review && c.regular && !best.regular)) best = c;
            if (worst == null || c.review < worst.review || (c.review == worst.review && c.regular && !worst.regular)) worst = c;
        }
        if (best == null) return picked;
        picked.Add(best);
        if (worst != null && worst.review < best.review) picked.Add(worst);
        if (!picked.Exists(p => p.regular))
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                ReviewCard c = cards[i];
                if (c != null && c.regular && c.review != Review.None && !picked.Contains(c)) { picked.Add(c); break; }
            }
        return picked;
    }

    private void ClearToday(int day)
    {
        Day = Math.Max(1, day);
        today.Clear();
        Array.Clear(counts, 0, counts.Length);
        quotes.Clear();
        quoteReviews.Clear();
        cards.Clear();
        live.Clear();
        TodayChange = 0;
        Settled = false;
        StarsBefore = StarsEarned;
    }
}

// ---------------------------------------------------------------------------
// The recap's reputation text (TextMeshPro rich text). Kept here, away from
// RecapUI, so the console tests can read exactly what the player will see.
// The stars themselves are images in the recap; this is everything below them.
//
// Sized for the recap's middle column (640 wide, 24 pt; see ReputationSetup):
// the café's name for its stars at the size of the recap's own heading, the
// counts and quotes a little smaller, and each quote in a column of its own
// beside its verdict, so a long quote wraps under itself, not under the label.
// ---------------------------------------------------------------------------
public static class ReputationRecap
{
    const string Gold = "#FFC857";
    const string Grey = "#A6A6A6";
    const string Small = "<size=92%>";
    // Where quotes start, in ems of the quote's own size: clears "Never again".
    const string QuoteColumn = "<indent=6em>";

    public static string Build(ReputationLedger rep)
    {
        if (rep == null) return "";
        var sb = new System.Text.StringBuilder();

        sb.Append("<size=140%><b>").Append(ReputationRules.NameOf(rep.StarsEarned)).Append("</b></size>");
        if (rep.EarnedStarToday) sb.Append("   <color=").Append(Gold).Append("><b>New star!</b></color>");
        sb.Append('\n');

        int next = rep.NextThreshold;
        if (next < 0)
        {
            sb.Append("Five stars. Reputation ").Append(rep.Reputation).Append('\n');
        }
        else
        {
            sb.Append("Next star  ").Append(rep.Reputation).Append(" / ").Append(next)
              .Append("   <color=").Append(Grey).Append('>').Append(Bar(rep)).Append("</color>\n");
        }

        sb.Append('\n');
        if (rep.ReviewCount == 0)
        {
            sb.Append("<b>Today's reviews</b>   none\n");
            return sb.ToString().TrimEnd('\n');
        }

        sb.Append("<b>Today's reviews</b>   ").Append(Signed(rep.TodayChange)).Append('\n');
        sb.Append(Small).Append("Loved ").Append(rep.Count(Review.LovedIt))
          .Append(" · Liked ").Append(rep.Count(Review.LikedIt))
          .Append(" · Fine ").Append(rep.Count(Review.Fine))
          .Append(" · Let down ").Append(rep.Count(Review.LetDown))
          .Append(" · Never again ").Append(rep.Count(Review.NeverAgain)).Append("</size>\n");

        for (int i = 0; i < rep.Quotes.Count; i++)
        {
            Review verdict = i < rep.QuoteReviews.Count ? rep.QuoteReviews[i] : Review.None;
            sb.Append(Small).Append("<color=").Append(ColourOf(verdict)).Append('>').Append(ReputationRules.Label(verdict)).Append("</color>")
              .Append(QuoteColumn).Append("<i>").Append(rep.Quotes[i]).Append("</i></indent></size>\n");
        }
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Ten blocks from the current star's threshold to the next.</summary>
    public static string Bar(ReputationLedger rep)
    {
        int next = rep.NextThreshold;
        if (next < 0) return new string('▓', 10);
        int from = ReputationRules.CurrentThreshold(rep.StarsEarned);
        int span = Math.Max(1, next - from);
        int filled = Math.Max(0, Math.Min(10, (rep.Reputation - from) * 10 / span));
        return new string('▓', filled) + new string('░', 10 - filled);
    }

    public static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

    /// <summary>
    /// The recap phone's one line about the day (the Franchise app's "What changed today"), from its
    /// worst kind of review. Placeholder lines for the writers. Empty on a day with no reviews.
    /// </summary>
    public static string Lesson(IReadOnlyList<ReviewCard> cards)
    {
        if (cards == null || cards.Count == 0) return "";
        bool Any(ReviewReason reason) { foreach (ReviewCard c in cards) if (c.reason == reason) return true; return false; }
        if (Any(ReviewReason.Planted)) return "A one-star review from nobody you served. Someone is making a point.";
        if (Any(ReviewReason.WalkedOutAfterAccepting)) return "Walk-outs after you'd taken the job cost the most.";
        if (Any(ReviewReason.RejectedRepair)) return "A repair went back still broken. That costs the most.";
        if (Any(ReviewReason.WalkedOutInQueue)) return "Some gave up in the queue before you got to them.";
        if (Any(ReviewReason.UnservedAtClose)) return "Some were still waiting when you closed.";
        if (Any(ReviewReason.HelpedButUnhappy)) return "Someone got half of what they came for.";
        bool allGood = true;
        foreach (ReviewCard c in cards) if (c.review < Review.LikedIt) allGood = false;
        return allGood ? "Every review was a good one." : "Long waits and rough repairs brought the reviews down.";
    }

    public static string ColourOf(Review review) => review switch
    {
        Review.LovedIt => "#8FD694",
        Review.LikedIt => "#C7E6A1",
        Review.Fine => "#D9D9D9",
        Review.LetDown => "#F2B880",
        Review.NeverAgain => "#F28B82",
        _ => "#D9D9D9"
    };
}
