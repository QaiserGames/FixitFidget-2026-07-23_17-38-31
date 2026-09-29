using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE RECAP AS ACE'S PHONE: THE LAB (claude/playtest-2-plan.md §9)
//
//   Fixit Fidget > Recap phone > Play from a sample Day 3 recap (lab)
//     A café lab session (a test save: your playtest save is not used) that opens on the recap of a
//     made-up Day 3: five reviews (+2 reputation, and the café's second star), $641 in the till, 12 cups
//     and 9 beans (low), Faster Machine at level 1, and Grace in the notebook, two facts new today. The
//     review lines are the game's own (Assets/Data/Reputation/ReviewLines). Its button leads into the night.
//   Fixit Fidget > Recap phone > Play check (lab, drives itself)
//     The same lab, driven by RecapPhoneCheck (about half a minute). Report and photos:
//     Logs/Recap/recap-phone-check-<time>/.
//
// Nothing in the scene changes, and the playtest save is never written.
// ---------------------------------------------------------------------------
internal static class RecapPhoneSteps
{
    const string Tag = "[Recap phone] ";
    const string Menu = "Fixit Fidget/Recap phone/";
    const string LinesPath = "Assets/Data/Reputation/ReviewLines.asset";

    [MenuItem(Menu + "Play from a sample Day 3 recap (lab)")]
    static void Play() => StartLab(false);

    [MenuItem(Menu + "Play check (lab, drives itself)")]
    static void PlayCheck() => StartLab(true);

    [MenuItem(Menu + "Play from a sample Day 3 recap (lab)", true)]
    [MenuItem(Menu + "Play check (lab, drives itself)", true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void StartLab(bool check)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, SampleDayThree());
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        if (check) PlayerPrefs.SetInt(RecapPhoneCheck.PendingKey, 1);
        else PlayerPrefs.DeleteKey(RecapPhoneCheck.PendingKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + (check
            ? "Play check: it drives the phone itself from Day 3's recap into the night (about half a minute). " +
              "Keep the Game view in front and leave the mouse, keyboard and pad alone."
            : "Day 3's recap on Ace's phone. Q/E switch apps, 1-4 jump, W/S scroll; Close up for the night leads into the night.")
            + $" Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A made-up Day 3 for the lab only, settled through the real reputation code, so its reviews, quotes and
    // cards are exactly what a played day would save.
    internal static SaveData SampleDayThree()
    {
        ReviewLines lines = AssetDatabase.LoadAssetAtPath<ReviewLines>(LinesPath);
        bool madeLines = lines == null;
        if (madeLines) lines = ScriptableObject.CreateInstance<ReviewLines>();
        try
        {
            var ledger = new ReputationLedger();
            ledger.Restore(29, 1, 3, false, null);   // one star this morning; +2 today makes 31: the second star
            ReviewEntry[] reviews =
            {
                new ReviewEntry { review = Review.LetDown, reason = ReviewReason.WalkedOutInQueue, name = "Walter", thing = "Americano" },
                new ReviewEntry { review = Review.LikedIt, reason = ReviewReason.WaitedLong, name = "Grace", thing = "Latte", regular = true },
                new ReviewEntry { review = Review.LovedIt, reason = ReviewReason.LovedDrink, name = "Walk-in 2", thing = "Hot Chocolate" },
                new ReviewEntry { review = Review.NeverAgain, reason = ReviewReason.WalkedOutAfterAccepting, name = "Priya", thing = "Pocket Watch" },
                new ReviewEntry { review = Review.LovedIt, reason = ReviewReason.LovedRepair, name = "Tomas", thing = "Phone" },
            };
            foreach (ReviewEntry review in reviews) ledger.Record(review);
            string Line(ReviewEntry entry)
            {
                string[] pool = lines.For(entry.reason);
                return pool == null || pool.Length == 0 ? null : ReputationRules.Fill(pool[1 % pool.Length], entry.name, entry.thing, entry.drink);
            }
            ledger.Settle(3, (entry, position) => ReputationRules.Quote(Line(entry), entry.name), Line);

            var recap = new RecapSaveData
            {
                day = 3, peopleServed = 3, customersLost = 2, turnedAway = 1, ordersCompleted = 4, repairs = 1, drinks = 3,
                perfect = 1, tips = 9, earned = 142, patronIncome = 10, closingTill = 641, elapsedSeconds = 360f,
            };
            ledger.WriteRecap(recap);
            return new SaveData
            {
                day = 3,
                money = 641,
                cups = 12,
                beans = 9,
                reputation = ledger.Reputation,
                starsEarned = ledger.StarsEarned,
                dayCompleted = true,
                recap = recap,
                upgradeNames = new[] { "Upgrade_FastMachine" },
                upgradeLevels = new[] { 1 },
                regularMemories = new[] { GraceMemory() },
                notebook = SampleNotebook(),
                night = new NightSaveData(),
            };
        }
        finally
        {
            if (madeLines) Object.DestroyImmediate(lines);
        }
    }

    // Grace's camera story is behind her (the reunion photo came out); she was in again today.
    static RegularMemoryData GraceMemory() => new RegularMemoryData
    {
        profileId = GraceCameraEpisode.ProfileId,
        visits = 3,
        relationship = 4,
        lastSeenDay = 3,
        lastVisitHappy = true,
        lastJobAccepted = true,
        lastVisitServed = true,
        lastGrade = nameof(JobGrade.Good),
        graceCameraAttempted = true,
        graceCameraReturned = true,
        graceCameraGrade = nameof(JobGrade.Good),
        graceCameraDay = 1,
        graceReturnAcknowledged = true,
        gracePhotoClaimed = true,
        gracePhotoVariant = GracePhotoOutcome.Clear.ToString(),
    };

    // What Ace knows about Grace: her camera story (Day 1), her house seen at night (Day 2, a hunch), and
    // today the photo and Barnaby, so the Notes badge has something to count.
    static NotebookFactData[] SampleNotebook()
    {
        var facts = new List<NotebookFactData>();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName))
        {
            fact.day = 1;
            facts.Add(fact);
        }
        NotebookFactData home = NotebookEntries.HomeSeen(NotebookEntries.GraceId, NotebookEntries.GraceName, "the saffron house", "12", "west",
            true, Notebook.Sureness.Hunch);
        home.day = 2;
        facts.Add(home);
        NotebookFactData photo = NotebookEntries.GraceReturn(NotebookEntries.GraceName, GracePhotoOutcome.Clear);
        photo.day = 3;
        facts.Add(photo);
        NotebookFactData barnaby = NightThings.Mentioned(NightThings.GnomeOfGrace, NotebookEntries.GraceName);
        barnaby.day = 3;
        facts.Add(barnaby);
        return facts.ToArray();
    }

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(RecapPhoneCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(RecapPhoneCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
