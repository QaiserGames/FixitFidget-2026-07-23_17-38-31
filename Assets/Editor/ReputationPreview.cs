#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// Shows a made-up busy day in the recap's reputation block, so the layout and
// the review lines can be checked without playing a whole day.
//
// Play mode only, with the end-of-day recap on screen. Nothing is saved: the
// preview only changes what the block shows, and leaving Play mode clears it.
//
// It quotes the LONGEST line of each kind, with the longest device name in the
// game, and earns a star on the day. If this fits, any real day fits. Handy
// after editing Assets/Data/Reputation/ReviewLines.
// ---------------------------------------------------------------------------
public static class ReputationPreview
{
    const string Menu = "Fixit Fidget/Reputation/Preview a busy day in the recap (Play mode, recap open)";
    const string LinesPath = "Assets/Data/Reputation/ReviewLines.asset";

    [MenuItem(Menu)]
    static void Preview()
    {
        RecapUI recap = Object.FindAnyObjectByType<RecapUI>(FindObjectsInactive.Include);
        if (recap == null) return;
        var so = new SerializedObject(recap);
        var text = so.FindProperty("reputationText").objectReferenceValue as TMP_Text;
        if (text == null)
        {
            Debug.LogWarning("[Reputation preview] The recap has no reputation block. Run Fixit Fidget > Reputation > Add stars and reviews to the recap first.");
            return;
        }

        ReviewLines lines = AssetDatabase.LoadAssetAtPath<ReviewLines>(LinesPath);
        if (lines == null) lines = ScriptableObject.CreateInstance<ReviewLines>();

        // 28 reputation and one star this morning; +4 today crosses 30.
        var day = new ReputationLedger();
        day.Restore(28, 1, 5, false, null);
        void Add(Review review, ReviewReason reason, string name, string thing, bool regular = false) =>
            day.Record(new ReviewEntry { review = review, reason = reason, name = name, thing = thing, regular = regular });
        Add(Review.LovedIt, ReviewReason.LovedRepair, "Tomas", "Phone");
        Add(Review.LovedIt, ReviewReason.LovedDrink, "Priya", "Hot Chocolate");
        Add(Review.LikedIt, ReviewReason.WaitedLong, "Grace", "reunion film camera", regular: true);
        Add(Review.LetDown, ReviewReason.WalkedOutInQueue, "Omar", "Americano");
        Add(Review.NeverAgain, ReviewReason.WalkedOutAfterAccepting, "Walk-in 3", "reunion film camera");
        Add(Review.LovedIt, ReviewReason.LovedRepair, "Alishba", "Pocket Watch");
        day.Settle(5, (entry, position) =>
        {
            string[] pool = lines.For(entry.reason);
            if (pool == null || pool.Length == 0) return null;
            string longest = pool.Select(l => ReputationRules.Fill(l, entry.name, entry.thing, entry.drink))
                                 .OrderByDescending(l => l.Length).First();
            return ReputationRules.Quote(longest, entry.name);
        });

        text.text = ReputationRecap.Build(day);

        SerializedProperty stars = so.FindProperty("reputationStars");
        var earnedSprite = so.FindProperty("starEarnedSprite").objectReferenceValue as Sprite;
        var emptySprite = so.FindProperty("starEmptySprite").objectReferenceValue as Sprite;
        Color earnedColor = so.FindProperty("starEarnedColor").colorValue;
        Color emptyColor = so.FindProperty("starEmptyColor").colorValue;
        for (int i = 0; i < stars.arraySize; i++)
        {
            if (stars.GetArrayElementAtIndex(i).objectReferenceValue is not Image star) continue;
            bool earned = i < day.StarsEarned;
            star.color = earned ? earnedColor : emptyColor;
            Sprite sprite = earned ? earnedSprite : emptySprite;
            if (sprite != null) star.sprite = sprite;
        }

        Debug.Log($"[Reputation preview] A sample day: reputation {day.Reputation}, {day.StarsEarned} stars (new star today), " +
                  $"{day.ReviewCount} reviews, {day.Quotes.Count} quotes (the longest line of each kind). Nothing saved; leave Play mode to clear it.");
    }

    [MenuItem(Menu, true)]
    static bool RecapOpen()
    {
        if (!EditorApplication.isPlaying) return false;
        RecapUI recap = Object.FindAnyObjectByType<RecapUI>(FindObjectsInactive.Include);
        if (recap == null) return false;
        var panel = new SerializedObject(recap).FindProperty("panel").objectReferenceValue as GameObject;
        return panel != null && panel.activeInHierarchy;
    }
}
#endif
