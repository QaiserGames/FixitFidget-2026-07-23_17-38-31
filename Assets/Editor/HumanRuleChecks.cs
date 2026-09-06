using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public static class HumanRuleChecks
{
    private static int assertions;
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Human conversation rules")]
    public static void RunInEditor() => Debug.Log($"[Human rules] PASS: {RunAll()} assertions.");
#endif
    public static int RunAll()
    {
        assertions = 0;
        ProgressAndFeedback();
        IndependentDevices();
        MalformedScenarios();
        return assertions;
    }

    private static HumanDialogueStep[] Scenario() => new[] {
        new HumanDialogueStep { customerLine = "Clue A", options = new[] { "A0", "A1", "A2" }, correctOption = 0, wrongResponse = "Feedback A" },
        new HumanDialogueStep { customerLine = "Clue B", options = new[] { "B0", "B1" }, correctOption = 1, wrongResponse = "Feedback B" },
        new HumanDialogueStep { customerLine = "Clue C", options = new[] { "C0", "C1", "C2" }, correctOption = 2, wrongResponse = "Feedback C" }
    };

    private static void ProgressAndFeedback()
    {
        var run = new HumanConversationRun(Scenario(), "Test passed");
        Require(run.IsValid && run.Count == 3 && run.Credits == 0 && !run.Finished, "Fresh conversation awards nothing.");
        Require(run.Line == "Clue A" && run.OptionCount == 3 && run.Option(2) == "A2", "First clue and its choices are available.");
        Require(!run.Choose(-1) && !run.Choose(3) && run.Mistakes == 0, "No-input/out-of-range cannot answer or penalize.");
        Require(run.Option(-1) == "" && run.Option(99) == "", "Invalid UI index is safe.");
        Require(run.Choose(2) && run.Credits == 0 && run.Line == "Feedback A" && run.Mistakes == 1, "Wrong answer explains the clue without advancing.");
        Require(run.Choose(0) && run.Credits == 1 && run.Line == "Clue B" && run.OptionCount == 2, "Correct answer moves to the next requirement.");
        Require(!run.Choose(2) && run.Credits == 1 && run.Mistakes == 1, "Third key ignored on a two-choice step.");
        for (int i = 0; i < 5; i++)
            Require(run.Choose(0) && run.Credits == 1 && !run.Finished, "Repeating the old answer never farms progress.");
        Require(run.Choose(1) && run.Credits == 2 && run.Line == "Clue C", "Confirmed requirement gives the second credit.");
        Require(run.Choose(2) && run.Finished && run.Credits == 3 && run.Line == "Test passed", "Completion requires every authored check.");
        Require(run.OptionCount == 0 && run.Option(0) == "" && !run.Choose(2) && run.Credits == 3,
            "Finished run cannot be re-completed or graded down by a stray key.");
        Require(run.Mistakes == 6, "Useful feedback costs attention, not a hidden grade penalty after success.");
    }

    private static void IndependentDevices()
    {
        var source = Scenario();
        var first = new HumanConversationRun(source, "Done");
        var second = new HumanConversationRun(source, "Done");
        first.Choose(0);
        Require(first.Credits == 1 && second.Credits == 0 && second.Line == "Clue A", "Two phones never share conversation progress.");
        source[1].options[1] = "Changed";
        source[1].correctOption = 0;
        source[1].customerLine = "Rewritten";
        source[1] = null;
        Require(first.Option(1) == "B1" && first.Line == "Clue B" && first.Choose(1) && first.Credits == 2,
            "A run snapshots text, options and answers; editing an asset cannot alter an accepted repair.");
        string savedLine = first.Line;
        for (int i = 0; i < 10; i++) Require(first.Line == savedLine && first.Credits == 2, "Reading or reopening does not reset or advance progress.");
    }

    private static void MalformedScenarios()
    {
        Reject(null, "Done");
        Reject(Array.Empty<HumanDialogueStep>(), "Done");
        Reject(Scenario(), " ");
        Reject(new HumanDialogueStep[9], "Done");
        for (int problem = 0; problem < 8; problem++)
        {
            var s = Scenario();
            switch (problem)
            {
                case 0: s[1] = null; break;
                case 1: s[1].customerLine = ""; break;
                case 2: s[1].wrongResponse = null; break;
                case 3: s[1].options = null; break;
                case 4: s[1].options = new[] { "Only one" }; break;
                case 5: s[1].options[1] = " "; break;
                case 6: s[1].correctOption = -1; break;
                case 7: s[1].correctOption = 2; break;
            }
            Reject(s, "Done");
        }
    }

    private static void Reject(HumanDialogueStep[] source, string ending)
    {
        var run = new HumanConversationRun(source, ending);
        Require(!run.IsValid && !string.IsNullOrEmpty(run.Error) && !run.Finished && run.Credits == 0,
            "Malformed content fails closed with a useful author error.");
        Require(run.Count == 0 && run.OptionCount == 0 && !run.Choose(0), "Invalid content cannot produce a free completed job.");
    }

    private static void Require(bool condition, string reason)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(reason);
    }
}
