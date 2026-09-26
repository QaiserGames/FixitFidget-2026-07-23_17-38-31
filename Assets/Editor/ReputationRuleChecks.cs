using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for the café's reputation (claude/reputation-spec.md). Pure: no
// scene, no save file, no assets. Also compiled by Tests/ReputationRules.
public static class ReputationRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Reputation rules")]
    public static void Run()
    {
        int count = RunAll() + CheckUnityJson();
        Debug.Log("[Reputation rules] PASS: " + count + " assertions. No scene, save or asset changes.");
    }

    // Unity's own serializer, which is what the real save uses.
    static int CheckUnityJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }

        var old = JsonUtility.FromJson<SaveData>("{\"version\":4,\"day\":7,\"money\":300,\"dayCompleted\":true,\"recap\":{\"day\":7,\"earned\":50}}");
        old.ValidateAndMigrate();
        Check(old.version == SaveData.CurrentVersion && old.reputation == 0 && old.starsEarned == 0, "A v4 save migrates to zero reputation.");
        Check(old.recap != null && old.recap.reviewQuotes != null && old.recap.reviewQuoteVerdicts != null, "A v4 recap gets empty review arrays.");

        var ledger = new ReputationLedger();
        ledger.Restore(old.reputation, old.starsEarned, old.day, old.dayCompleted, old.recap);
        Check(ledger.Settled && ledger.ReviewCount == 0 && ledger.Quotes.Count == 0, "Resuming a pre-review recap shows no reviews and never settles again.");

        var played = new ReputationLedger();
        played.Restore(28, 1, 3, false, null);
        played.Record(new ReviewEntry { review = Review.LovedIt, reason = ReviewReason.LovedDrink, name = "Priya", thing = "latte" });
        played.Settle(3, (e, i) => "“Perfect latte.” — " + e.name);
        var save = new SaveData { day = 3, dayCompleted = true, recap = new RecapSaveData { day = 3 },
                                  reputation = played.Reputation, starsEarned = played.StarsEarned };
        played.WriteRecap(save.recap);
        var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        back.ValidateAndMigrate();
        Check(back.reputation == 30 && back.starsEarned == 2 && back.recap.reviewsLoved == 1 && back.recap.reputationChange == 2,
            "Reputation and today's reviews survive Unity's JSON.");
        Check(back.recap.reviewQuotes.Length == 1 && back.recap.reviewQuotes[0].Contains("Perfect latte"), "Quotes survive Unity's JSON, including curly quotes.");
        Check(back.TryCreateNextDay(out SaveData next) && next.reputation == 30 && next.starsEarned == 2 && next.recap == null,
            "Open Tomorrow carries reputation forward and leaves today's reviews behind.");
        return n;
    }
#endif

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }
        int Count(string s, string part)
        {
            int n = 0;
            for (int at = s.IndexOf(part, StringComparison.Ordinal); at >= 0; at = s.IndexOf(part, at + part.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        // ---------- verdicts ----------
        var reasons = (LostReason[])Enum.GetValues(typeof(LostReason));
        var grades = (JobGrade[])Enum.GetValues(typeof(JobGrade));
        foreach (bool happy in new[] { true, false })
        foreach (bool served in new[] { true, false })
        foreach (bool accepted in new[] { true, false })
        foreach (bool repairVisit in new[] { true, false })
        foreach (bool returned in new[] { true, false })
        foreach (JobGrade grade in grades)
        foreach (LostReason reason in reasons)
        foreach (float patience in new[] { 0f, 0.1f, 0.2499f, 0.25f, 0.6f, 1f, float.NaN })
        {
            var f = new ReviewFacts { happy = happy, served = served, accepted = accepted, repairVisit = repairVisit,
                                      repairReturned = returned, grade = grade, reason = reason, patienceAtExit = patience };
            Judgement j = ReputationRules.Judge(f);
            Check(ReputationRules.Verdict(f) == j.review, "Verdict and Judge agree.");
            Check((j.review == Review.None) == (j.reason == ReviewReason.None), "Every review has a reason, and only reviews do.");

            if (returned && grade == JobGrade.Rejected)
            {
                Check(j.review == Review.NeverAgain && j.reason == ReviewReason.RejectedRepair, "A repair handed back broken is Never again.");
                continue;
            }
            if (happy)
            {
                Check(j.review >= Review.Fine, "A happy customer never leaves a negative review.");
                bool waited = patience < ReputationRules.LongWait;
                bool passable = returned && grade == JobGrade.Passable;
                Review expected = waited && passable ? Review.Fine : waited || passable ? Review.LikedIt : Review.LovedIt;
                Check(j.review == expected, "Happy visits lose one step per problem: a long wait, a Passable repair.");
                if (expected == Review.LovedIt)
                    Check(j.reason == (repairVisit ? ReviewReason.LovedRepair : ReviewReason.LovedDrink), "Loved it says what was loved.");
                continue;
            }
            if (served)
            {
                Check(j.review == Review.LetDown && j.reason == ReviewReason.HelpedButUnhappy, "Helped but unhappy is Let down.");
                continue;
            }
            switch (reason)
            {
                case LostReason.Declined:
                case LostReason.OutOfStock:
                case LostReason.ShelfFull:
                    Check(j.review == Review.None, "Declined, out of stock and a full shelf leave no review.");
                    break;
                case LostReason.StormedOutWaiting:
                    Check(j.review == (accepted ? Review.NeverAgain : Review.LetDown), "Walking out on an accepted job is the worst walkout.");
                    break;
                default:
                    Check(j.review == Review.LetDown, "Queue walkouts and people still waiting at closing are Let down.");
                    break;
            }
        }

        Check(ReputationRules.Points(Review.LovedIt) == 2 && ReputationRules.Points(Review.LikedIt) == 1
            && ReputationRules.Points(Review.Fine) == 0 && ReputationRules.Points(Review.LetDown) == -1
            && ReputationRules.Points(Review.NeverAgain) == -2 && ReputationRules.Points(Review.None) == 0, "Review points are +2..-2.");

        // ---------- stars ----------
        int[] t = ReputationRules.StarThresholds;
        Check(t.Length == 5 && t[0] == 5 && t[4] == 580, "Thresholds as decided: 5, 30, 120, 300, 580.");
        for (int i = 1; i < t.Length; i++) Check(t[i] > t[i - 1], "Thresholds climb.");
        Check(ReputationRules.StarsFor(0, 0) == 0 && ReputationRules.StarsFor(4, 0) == 0 && ReputationRules.StarsFor(5, 0) == 1, "First star at 5.");
        Check(ReputationRules.StarsFor(299, 0) == 3 && ReputationRules.StarsFor(300, 0) == 4 && ReputationRules.StarsFor(99999, 0) == 5, "Stars follow the thresholds.");
        Check(ReputationRules.StarsFor(0, 3) == 3 && ReputationRules.StarsFor(-10, 2) == 2, "Earned stars stay earned.");
        Check(ReputationRules.StarsFor(0, 9) == 5 && ReputationRules.StarsFor(0, -3) == 0, "Stars stay within 0..5.");
        Check(ReputationRules.NextThreshold(0) == 5 && ReputationRules.NextThreshold(4) == 580 && ReputationRules.NextThreshold(5) == -1, "Next threshold.");
        Check(ReputationRules.CurrentThreshold(0) == 0 && ReputationRules.CurrentThreshold(3) == 120, "Current threshold.");
        Check(ReputationRules.NameOf(0) == "Just opened" && ReputationRules.NameOf(7) == ReputationRules.NameOf(5), "Star names clamp.");
        Check(ReputationRules.Fill("My {thing}, {name}, {drink}", "Alex", "radio", "") == "My radio, Alex, coffee", "Tokens fill, with fallbacks.");
        Check(ReputationRules.Fill("Took my {thing}, then forgot I existed.", "Grace", "Pocket Watch", "") == "Took my pocket watch, then forgot I existed."
            && ReputationRules.Fill("Best {drink} on the street.", "", "", "Hot Chocolate") == "Best hot chocolate on the street.",
            "Devices and drinks read in lower case inside a sentence.");
        Check(ReputationRules.InSentence("reunion film camera") == "reunion film camera" && ReputationRules.InSentence("TV") == "TV"
            && ReputationRules.InSentence("iPod") == "iPod" && ReputationRules.InSentence(" Phone ") == "phone" && ReputationRules.InSentence(null) == "",
            "Only Title Case words change case.");
        Check(ReputationRules.Fill("{thing} works again.", "", "Phone", "") == "Phone works again."
            && ReputationRules.Fill("{name} says hi.", "Walk-in 2", "", "") == "A walk-in says hi.",
            "A line that starts with a token still starts with a capital.");
        Check(ReputationRules.Attribution("Walk-in 3") == "a walk-in" && ReputationRules.Attribution(" Grace ") == "Grace"
            && ReputationRules.Attribution("") == "a customer" && ReputationRules.Attribution("Walker") == "Walker",
            "Quotes are signed by name; the roster's fallback names sign as \"a walk-in\".");
        Check(ReputationRules.Quote("Magic.", "Tomas") == "\u201CMagic.\u201D\u00A0\u2014\u00A0Tomas"
            && ReputationRules.Quote(" Slow. ", "Walk-in 4") == "\u201CSlow.\u201D\u00A0\u2014\u00A0a\u00A0walk-in"
            && ReputationRules.Quote(" ", "Grace") == null,
            "A quote is signed with non-breaking spaces, so the name never wraps alone.");

        // ---------- a day in the ledger ----------
        ReviewEntry E(Review r, string name, bool regular = false) =>
            new ReviewEntry { review = r, reason = r == Review.LovedIt ? ReviewReason.LovedRepair : ReviewReason.WalkedOutInQueue, name = name, thing = "watch", regular = regular };

        var ledger = new ReputationLedger();
        ledger.Restore(0, 0, 1, false, null);
        Check(!ledger.Record(new ReviewEntry { review = Review.None }), "No review, no record.");
        Check(ledger.Record(E(Review.LovedIt, "Priya")) && ledger.Record(E(Review.LovedIt, "Tomas")) && ledger.Record(E(Review.LetDown, "Saamin")),
            "Reviews record during the day.");
        Check(ledger.TodayChange == 3 && ledger.ReviewCount == 3 && ledger.Reputation == 0, "Reviews only count at closing.");
        var written = new List<int>();
        ledger.Settle(1, (e, i) => { written.Add(i); return e.name + " said so"; });
        Check(ledger.Settled && ledger.Reputation == 3 && ledger.StarsEarned == 0 && !ledger.EarnedStarToday, "Settled: +3, no star yet.");
        Check(ledger.Quotes.Count == 2 && ledger.QuoteReviews[0] == Review.LovedIt && ledger.QuoteReviews[1] == Review.LetDown, "Best then worst.");
        Check(written.Count == 2 && written[0] == 0 && written[1] == 1, "The writer gets the quote's position.");
        ledger.Settle(1, (e, i) => "again");
        Check(ledger.Reputation == 3 && ledger.Quotes[0] == "Priya said so", "Settling twice changes nothing.");
        Check(!ledger.Record(E(Review.LovedIt, "Late")) && ledger.ReviewCount == 3, "Nothing records after closing.");

        var recap = new RecapSaveData { day = 1 };
        ledger.WriteRecap(recap);
        var resumed = new ReputationLedger();
        resumed.Restore(ledger.Reputation, ledger.StarsEarned, 1, true, recap);
        Check(resumed.Settled && resumed.Reputation == 3 && resumed.TodayChange == 3 && resumed.Count(Review.LovedIt) == 2
            && resumed.Quotes.Count == 2 && resumed.Quotes[1] == "Saamin said so" && resumed.QuoteReviews[1] == Review.LetDown,
            "A resumed recap shows exactly what was settled.");
        resumed.Settle(1, (e, i) => "never");
        Check(resumed.Reputation == 3, "A resumed recap is never settled again.");

        ledger.BeginDay(2);
        Check(!ledger.Settled && ledger.ReviewCount == 0 && ledger.TodayChange == 0 && ledger.Quotes.Count == 0 && ledger.Reputation == 3 && ledger.Day == 2,
            "A new day starts empty and keeps the café's reputation.");
        ledger.Record(E(Review.LovedIt, "Ali"));
        ledger.Settle(2, (e, i) => "x");
        Check(ledger.Reputation == 5 && ledger.StarsEarned == 1 && ledger.EarnedStarToday && ledger.StarsBefore == 0, "First star arrives at 5.");

        // A bad day right after earning a star: reputation dips, the star stays.
        ledger.BeginDay(3);
        for (int i = 0; i < 6; i++) ledger.Record(E(Review.NeverAgain, "Walkout " + i));
        ledger.Settle(3, (e, i) => "x");
        Check(ledger.Reputation == 0 && ledger.StarsEarned == 1 && !ledger.EarnedStarToday, "Reputation floors at 0 and the earned star stays.");
        Check(ReputationRecap.Bar(ledger) == new string('░', 10), "Below the current star, the bar is empty.");

        // Declines never move reputation, however many.
        var steady = new ReputationLedger();
        steady.Restore(40, 2, 4, false, null);
        var decline = new ReviewFacts { reason = LostReason.Declined };
        for (int i = 0; i < 20; i++)
        {
            Judgement j = ReputationRules.Judge(decline);
            if (j.review != Review.None) steady.Record(new ReviewEntry { review = j.review, reason = j.reason });
        }
        steady.Settle(4, (e, i) => "x");
        Check(steady.Reputation == 40 && steady.ReviewCount == 0, "Declines never change reputation.");

        // ---------- quotes ----------
        var picks = ReputationLedger.PickQuotes(new[] { E(Review.LikedIt, "A"), E(Review.LovedIt, "B"), E(Review.LovedIt, "C", true), E(Review.Fine, "D") });
        Check(picks.Count == 2 && picks[0].name == "C" && picks[1].name == "D", "Ties for best go to regulars; the worst follows.");
        picks = ReputationLedger.PickQuotes(new[] { E(Review.LovedIt, "A"), E(Review.LovedIt, "B") });
        Check(picks.Count == 1 && picks[0].name == "A", "No worst when every review is the same; ties go to whoever left first.");
        picks = ReputationLedger.PickQuotes(new[] { E(Review.LovedIt, "A"), E(Review.LetDown, "B"), E(Review.LikedIt, "Grace", true), E(Review.Fine, "Grace2", true) });
        Check(picks.Count == 3 && picks[2].name == "Grace2", "A regular's review is added, the latest one.");
        Check(ReputationLedger.PickQuotes(Array.Empty<ReviewEntry>()).Count == 0 && ReputationLedger.PickQuotes(null).Count == 0, "No reviews, no quotes.");

        // ---------- the recap text ----------
        var fresh = new ReputationLedger();
        fresh.Restore(0, 0, 1, true, new RecapSaveData { day = 1 });
        string text = ReputationRecap.Build(fresh);
        Check(text.Contains("Just opened") && text.Contains("none") && text.Contains("0 / 5"), "An empty first recap reads sensibly.");
        var busy = new ReputationLedger();
        busy.Restore(290, 3, 9, false, null);
        busy.Record(E(Review.LovedIt, "Tomas")); busy.Record(E(Review.LovedIt, "Priya")); busy.Record(E(Review.LetDown, "Saamin"));
        busy.Record(new ReviewEntry { review = Review.LikedIt, reason = ReviewReason.WaitedLong, name = "Grace", regular = true });
        busy.Settle(9, (e, i) => "“" + e.name + " line”");
        text = ReputationRecap.Build(busy);
        Check(busy.Reputation == 294 && busy.StarsEarned == 3 && text.Contains("Neighbourhood favourite") && text.Contains("+4") && text.Contains("Loved 2")
            && text.Contains("Never again 0") && text.Contains("Tomas line") && text.Contains("Saamin line") && text.Contains("Grace line"),
            "A busy recap names the star, today's change, the counts and the quotes.");
        Check(Count(text, "<indent=") == busy.Quotes.Count && Count(text, "</indent>") == busy.Quotes.Count,
            "Each quote opens and closes its own column, so the next line starts at the left again.");
        foreach (string recapText in new[] { text, ReputationRecap.Build(fresh) })
            Check(Count(recapText, "<size=") == Count(recapText, "</size>") && Count(recapText, "<b>") == Count(recapText, "</b>")
                && Count(recapText, "<i>") == Count(recapText, "</i>") && Count(recapText, "<color=") == Count(recapText, "</color>"),
                "The recap's rich-text tags are balanced.");
        Check(ReputationRecap.Bar(busy).Length == 10, "The bar is ten blocks.");
        var top = new ReputationLedger();
        top.Restore(700, 5, 40, false, null);
        Check(ReputationRecap.Build(top).Contains("Five stars") && ReputationRecap.Bar(top) == new string('▓', 10), "Five stars reads as complete.");

        // ---------- save data ----------
        var save = new SaveData { reputation = -4, starsEarned = 12 };
        save.ValidateAndMigrate();
        Check(save.reputation == 0 && save.starsEarned == 5, "Out-of-range reputation is clamped on load.");
        var closed = new SaveData { day = 6, dayCompleted = true, recap = new RecapSaveData { day = 6, reviewsLoved = 3 }, reputation = 140, starsEarned = 3 };
        Check(closed.TryCreateNextDay(out SaveData tomorrow) && tomorrow.reputation == 140 && tomorrow.starsEarned == 3 && tomorrow.recap == null && tomorrow.day == 7,
            "Tomorrow keeps reputation and drops today's reviews.");

        return count;
    }
}
