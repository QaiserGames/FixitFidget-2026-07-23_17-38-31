using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4 FIXES: CONTENT & DIALOGUE (10 Oct 2026; the playtester agents' reports, Docs/Studio/playtests/*-2026-10-10.md,
// and claude/playtest-4-notes.md). Menu: Fixit Fidget › Playtest 4 › Content ›
//
//   0 - Check first (read-only): every fault label the game can roll and how a customer says it (FaultSpeech); every line
//       that names a device or a fault, filled in with every fault a visit can roll, flagged where it stops reading; what
//       steps 1-4 would change; the conversation's timings, in seconds.
//   1 - Walk-ins speak their faults (scene): the three walk-in requests that can't take a spoken fault ("This is {fault}",
//       "It's {fault}", "Sorry — {device}, {fault}") are rewritten, each only while it still reads as it did.
//   2 - Grace's callbacks and a review line (assets): her seven memory callbacks say "My {device}: {fault}." on a line of its
//       own (three had "this {device} has {fault}", which can't take a clause; four ran to 108-114 characters once filled);
//       the review "Came for a {thing}" becomes "Came for the {thing}" ("a espresso"). Each only while it still reads as it did.
//   3 - The man's line on a night Grace walked out (night lines): "Grace walked out. Café owners who talk don't last." and
//       the deal's sibling scene that says it (LodgerStory.DealSceneLetDown), copied from the deal as it is now; again if the
//       deal is reshaped. The deal itself is never touched.
//   4 - Conversations: a quick second E counts (scene): ConversationController.inputDelay 0.2 → 0 (the frame that opened
//       the conversation still never counts: ConversationController).
//   Lab - Night 0 after Grace walked out (lab, drives itself): the Night 0 play check from a made-up Day 1 on which Grace
//       walked out with her camera unfixed; it checks the deal says "Grace walked out." and is otherwise the deal.
// Every step runs through Playtest4Shared: survey, one undo group, checks in numbers, undone if a check fails, a report in
// Logs/Playtest4/, the scene never saved (look, then File › Save), nothing deleted. Each is safe to run twice ("already").
// All new words are PLACEHOLDER COPY for Mansoor's sister.
// ---------------------------------------------------------------------------
public static class Playtest4ContentSteps
{
    const string Area = Playtest4Shared.Menu + "Content/";
    const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";
    const string ReviewsPath = "Assets/Data/Reputation/ReviewLines.asset";
    const string NightLinesPath = "Assets/Data/Resources/Night lines.asset";

    // ---------- step 1: the walk-ins' requests that can't take a spoken fault ----------

    static readonly (string personality, string field, string was, string now, string why)[] WalkInFixes =
    {
        ("Impatient", "intake", "This is {fault}. Can you do it now?", "Quick one. My {device}: {fault}. Can you do it now?",
            "\"This is the screen's cracked\" doesn't read: a spoken fault is a clause, after \"my {device}:\""),
        ("Rushed", "intake", "Sorry — {device}, {fault}. I'm late.", "Sorry — my {device}: {fault}. I'm late.",
            "\"Sorry — phone, the screen's cracked\": the device without \"my\""),
        ("Sentimental", "intake", "It's {fault}. This {device} means a lot to me.", "My {device}: {fault}. It means a lot to me.",
            "\"It's the screen's cracked\" doesn't read (the playtester's \"It's Calls light up the screen…\")"),
        ("Impatient", "orderedDrink", "Coffee. While I'm standing here.", "Coffee. While I wait.",
            "Impatient customers wait seated since playtest 4 (Systems S1); PLACEHOLDER COPY"),
    };

    // ---------- step 2: Grace's callbacks, and one review line ----------

    static readonly (string field, string was, string now, string why)[] GraceFixes =
    {
        ("returnMemoryLines.missedVisit",
            "We ran out of time last visit. I'd like to try again: this {device} has {fault}.",
            "We ran out of time last visit. I'd like to try again.\nMy {device}: {fault}.",
            "\"this phone has the screen's cracked\" doesn't read"),
        ("returnMemoryLines.declinedVisit",
            "You couldn't take my job last time. Have you room today? My {device} has {fault}.",
            "You couldn't take my job last time. Have you room today?\nMy {device}: {fault}.",
            "\"My phone has the screen's cracked\" doesn't read"),
        ("returnMemoryLines.capacityRefusal",
            "You didn't have what you needed last time. Shall we try again? My {device} has {fault}.",
            "You didn't have what you needed last time. Shall we try again?\nMy {device}: {fault}.",
            "\"My phone has the screen's cracked\" doesn't read"),
        ("returnMemoryLines.imperfectRepair",
            "The last repair had rough edges. More care this time? My {device}: {fault}.",
            "The last repair had rough edges. More care this time?\nMy {device}: {fault}.",
            "114 characters once filled in: the request on a line of its own"),
        ("returnMemoryLines.rejectedRepair",
            "I took the last repair home unfinished. This time, my {device}: {fault}.",
            "I took the last repair home unfinished.\nThis time, my {device}: {fault}.",
            "111 characters once filled in: the request on a line of its own"),
        ("returnMemoryLines.incompleteService",
            "We only sorted part of my order last time. Today, my {device}: {fault}.",
            "We only sorted part of my order last time.\nToday, my {device}: {fault}.",
            "110 characters once filled in: the request on a line of its own"),
        ("returnMemoryLines.servedVisit",
            "It's good to see a familiar counter. Today it's my {device}: {fault}.",
            "It's good to see a familiar counter.\nToday it's my {device}: {fault}.",
            "108 characters once filled in: the request on a line of its own"),
    };

    static readonly (string field, string was, string now, string why)[] ReviewFixes =
    {
        ("lovedDrink", "Came for a {thing}, stayed for the vibes.", "Came for the {thing}, stayed for the vibes.",
            "\"Came for a espresso\" and \"a americano\""),
    };

    // ================================================================== 0 - Check first

    [MenuItem(Area + "0 - Check first (read-only)", false, 0)]
    static void CheckFirst() => Playtest4Shared.Survey("Playtest 4 › Content › 0 - Check first", "content-0-check-first", run =>
    {
        CustomerSpawner spawner = Playtest4Shared.One<CustomerSpawner>(run);
        List<DialogueRuleChecks.RolledFault> faults = DialogueRuleChecks.RolledFaults(spawner);

        run.Section("Faults the game can roll, and how a customer says them (FaultSpeech; the ticket keeps the label)");
        foreach (IGrouping<string, DialogueRuleChecks.RolledFault> label in faults.GroupBy(f => f.label, StringComparer.OrdinalIgnoreCase))
        {
            DialogueRuleChecks.RolledFault f = label.First();
            bool has = FaultSpeech.TryGet(f.label, out string said);
            string problem = has ? DialogueRuleChecks.SpokenFormProblem(f.label, said) : "no spoken form: customers would say the label";
            run.Check(problem.Length == 0, $"{f.device} · \"{f.label}\" → \"{(has ? said : f.label.ToLowerInvariant())}\"" +
                                           $"   ({string.Join("; ", label.Select(x => x.from).Distinct())}{(f.templated ? "" : "; said in its own words")})" +
                                           (problem.Length > 0 ? "   " + problem : ""));
        }

        run.Section("Every line that names a device or a fault, filled in with every fault a visit can roll");
        ReportReadings(run, DialogueRuleChecks.ReadFaultLines(DialogueRuleChecks.FaultLines(spawner), faults), everyFill: true);

        run.Section("Step 1 would change (the walk-ins' requests on the café scene's CustomerSpawner)");
        foreach (var fix in WalkInFixes) Plan(run, spawner, WalkInPath(spawner, fix.personality, fix.was, fix.field), WalkInPath(spawner, fix.personality, fix.now, fix.field),
            $"walk-in {fix.personality}", fix.was, fix.now, fix.why);

        run.Section("Step 2 would change (Grace's callbacks: " + GracePath + "; a review: " + ReviewsPath + ")");
        var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
        var reviews = AssetDatabase.LoadAssetAtPath<ReviewLines>(ReviewsPath);
        if (grace == null) run.Info("Grace's profile isn't at " + GracePath + ": step 2 will refuse.");
        else foreach (var fix in GraceFixes) Plan(run, grace, ElementPath(grace, fix.field, fix.was), ElementPath(grace, fix.field, fix.now), "Grace " + fix.field, fix.was, fix.now, fix.why);
        if (reviews == null) run.Info("The review lines aren't at " + ReviewsPath + ": step 2 will refuse.");
        else foreach (var fix in ReviewFixes) Plan(run, reviews, ElementPath(reviews, fix.field, fix.was), ElementPath(reviews, fix.field, fix.now), "review " + fix.field, fix.was, fix.now, fix.why);

        run.Section("Step 3: the man's line on a night Grace's visit went wrong (" + NightLinesPath + ")");
        var lines = AssetDatabase.LoadAssetAtPath<NightLines>(NightLinesPath);
        if (lines == null) run.Info("No Night lines asset: step 3 will refuse (Fixit Fidget › Night › Barks 1 makes it).");
        else
        {
            NightLines.Scene deal = SceneIn(lines, LodgerStory.DealScene), sibling = SceneIn(lines, LodgerStory.DealSceneLetDown);
            NightLines.Line likes = LineIn(lines, LodgerStory.GraceLine), walked = LineIn(lines, LodgerStory.GraceWalkedOutLine);
            run.Info($"the deal: {(deal != null ? string.Join(", ", deal.lines) : "MISSING")}");
            run.Info($"{LodgerStory.GraceLine}: \"{likes?.text ?? "MISSING"}\"");
            run.Info($"{LodgerStory.GraceWalkedOutLine}: {(walked != null ? "\"" + walked.text + "\"" : "not there yet (step 3 adds \"" + BarkSteps.GraceWalkedOutText + "\")")}");
            string problem = BarkRuleChecks.LetDownProblem(lines);
            run.Info($"{LodgerStory.DealSceneLetDown}: {(sibling == null ? "not there yet (step 3 copies the deal)" : problem.Length == 0 ? "there, and the deal with one line swapped" : "there, but " + problem)}");
        }

        run.Section("Step 4 and the conversation's pace (seconds; the scene's own values)");
        ConversationController controller = Playtest4Shared.One<ConversationController>(run);
        ConversationUI ui = Playtest4Shared.One<ConversationUI>(run);
        var so = new SerializedObject(controller);
        float inputDelay = run.Prop(so, "inputDelay").floatValue, closingPause = run.Prop(so, "closingPause").floatValue;
        var uiSo = new SerializedObject(ui);
        Vector2 beat = run.Prop(uiSo, "listenBeat").vector2Value;
        float reading = run.Prop(uiSo, "readingSpeed").floatValue;
        run.Info($"ConversationController.inputDelay {inputDelay:0.##} (step 4 makes it 0: a press inside it was thrown away); closingPause {closingPause:0.##}");
        run.Info($"ConversationUI.listenBeat {beat.x:0.##}-{beat.y:0.##} s, readingSpeed {reading:0.#} characters a second");
        foreach (CustomerArchetype walkIn in DialogueRuleChecks.WalkIns(spawner))
        {
            if (walkIn?.lines == null) continue;
            string intake = Longest(walkIn.lines.intake), accepted = Longest(walkIn.lines.accepted);
            float listen = Listen(intake.Length + 20, beat), close = Listen(accepted.Length, beat) + closingPause + Mathf.Clamp(accepted.Length / 30f, .4f, 2.5f);
            run.Info($"walk-in {walkIn.archetypeName}: their request's replies come up by themselves after about {listen:0.00} s; " +
                     $"their thanks closes by itself after about {close:0.0} s (\"{accepted}\")");
        }
    });

    // ================================================================== 1 - Walk-ins speak their faults (scene)

    [MenuItem(Area + "1 - Walk-ins speak their faults (scene)", false, 1)]
    static void WalkInsSpeak()
    {
        CustomerSpawner spawner = null;
        var before = new Dictionary<string, int>();
        Playtest4Shared.Step("Playtest 4 › Content › 1 - Walk-ins speak their faults (scene)", "content-1-walk-ins-speak",
            survey: run =>
            {
                spawner = Playtest4Shared.One<CustomerSpawner>(run);
                CustomerArchetype[] walkIns = DialogueRuleChecks.WalkIns(spawner);
                run.Require(walkIns.Length > 0, "The CustomerSpawner has no walk-in personalities.");
                foreach (var fix in WalkInFixes)
                    run.Require(walkIns.Any(a => a != null && a.archetypeName == fix.personality), $"No walk-in personality called {fix.personality} on the CustomerSpawner.");
                foreach (CustomerArchetype a in walkIns)
                    if (a != null) before[a.archetypeName] = a.lines?.intake?.Length ?? 0;
                foreach (var fix in WalkInFixes) Plan(run, spawner, WalkInPath(spawner, fix.personality, fix.was, fix.field), WalkInPath(spawner, fix.personality, fix.now, fix.field),
                    $"walk-in {fix.personality}", fix.was, fix.now, fix.why);
            },
            change: run =>
            {
                foreach (var fix in WalkInFixes)
                {
                    string path = WalkInPath(spawner, fix.personality, fix.was, fix.field);
                    if (path != null) run.SetString(spawner, path, fix.now, $"walk-in {fix.personality}, {(fix.field == "intake" ? "request" : fix.field)}");
                }
            },
            checks: run =>
            {
                foreach (var fix in WalkInFixes)
                    run.Check(WalkInPath(spawner, fix.personality, fix.was, fix.field) == null,
                        $"walk-in {fix.personality}: \"{fix.was}\" is gone (rewritten{(WalkInPath(spawner, fix.personality, fix.now, fix.field) != null ? " as \"" + fix.now + "\"" : " since, by someone else")})");
                foreach (CustomerArchetype a in DialogueRuleChecks.WalkIns(spawner))
                    if (a != null && before.TryGetValue(a.archetypeName, out int count))
                        run.Check((a.lines?.intake?.Length ?? 0) == count, $"walk-in {a.archetypeName}: still {count} request lines");
                List<DialogueRuleChecks.FaultLine> walkInLines = DialogueRuleChecks.FaultLines(spawner).Where(l => l.where.StartsWith("walk-in ", StringComparison.Ordinal)).ToList();
                run.Check(walkInLines.Count > 0, $"{walkInLines.Count} walk-in lines name a device or a fault");
                ReportReadings(run, DialogueRuleChecks.ReadFaultLines(walkInLines, DialogueRuleChecks.RolledFaults(spawner)), everyFill: false);
            });
    }

    // ================================================================== 2 - Grace's callbacks and a review line (assets)

    [MenuItem(Area + "2 - Grace's callbacks and a review line (assets)", false, 2)]
    static void GraceAndReviews()
    {
        CustomerProfile grace = null;
        ReviewLines reviews = null;
        CustomerSpawner spawner = null;
        Playtest4Shared.Step("Playtest 4 › Content › 2 - Grace's callbacks and a review line (assets)", "content-2-grace-and-reviews",
            survey: run =>
            {
                spawner = Playtest4Shared.One<CustomerSpawner>(run);
                grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
                reviews = AssetDatabase.LoadAssetAtPath<ReviewLines>(ReviewsPath);
                run.Require(grace != null, "Grace's profile isn't at " + GracePath + ".");
                run.Require(reviews != null, "The review lines aren't at " + ReviewsPath + ".");
                foreach (var fix in GraceFixes) Plan(run, grace, ElementPath(grace, fix.field, fix.was), ElementPath(grace, fix.field, fix.now), "Grace " + fix.field, fix.was, fix.now, fix.why);
                foreach (var fix in ReviewFixes) Plan(run, reviews, ElementPath(reviews, fix.field, fix.was), ElementPath(reviews, fix.field, fix.now), "review " + fix.field, fix.was, fix.now, fix.why);
            },
            change: run =>
            {
                foreach (var fix in GraceFixes)
                {
                    string path = ElementPath(grace, fix.field, fix.was);
                    if (path == null) continue;
                    run.SetString(grace, path, fix.now, "Grace " + fix.field);
                    run.SaveAsset(grace);   // saved only once every check has passed
                }
                foreach (var fix in ReviewFixes)
                {
                    string path = ElementPath(reviews, fix.field, fix.was);
                    if (path == null) continue;
                    run.SetString(reviews, path, fix.now, "review " + fix.field);
                    run.SaveAsset(reviews);
                }
            },
            checks: run =>
            {
                foreach (var fix in GraceFixes)
                    run.Check(ElementPath(grace, fix.field, fix.was) == null, $"Grace {fix.field}: the old line is gone");
                foreach (var fix in ReviewFixes)
                    run.Check(ElementPath(reviews, fix.field, fix.was) == null, $"review {fix.field}: \"{fix.was}\" is gone");
                // What Customer memory's check reads in these lines (CustomerMemoryChecks): still there.
                run.Check(Any(grace.returnMemoryLines?.missedVisit, "ran out of time"), "Grace's missed-visit callback still says \"ran out of time\" (Customer memory's check reads it)");
                run.Check(Any(grace.returnMemoryLines?.rejectedRepair, "unfinished"), "her rejected-repair callback still says \"unfinished\"");
                run.Check(Any(grace.returnMemoryLines?.servedVisit, "familiar counter"), "her served-visit callback still says \"familiar counter\"");
                List<DialogueRuleChecks.FaultLine> hers = DialogueRuleChecks.FaultLines(spawner)
                    .Where(l => l.where.StartsWith(grace.characterName + " ", StringComparison.Ordinal) || l.where.StartsWith(grace.characterName + ",", StringComparison.Ordinal)).ToList();
                run.Check(hers.Count > 0, $"{hers.Count} of Grace's lines name a device or a fault");
                ReportReadings(run, DialogueRuleChecks.ReadFaultLines(hers, DialogueRuleChecks.RolledFaults(spawner)), everyFill: false);
                // Every review line, with every device and every drink: "a"/"an" right.
                List<string> things = DialogueRuleChecks.RolledFaults(spawner).Select(f => f.device).Distinct().ToList();
                foreach (string guid in AssetDatabase.FindAssets("t:DrinkDefinition"))
                {
                    var drink = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (drink != null && !string.IsNullOrWhiteSpace(drink.drinkName)) things.Add(drink.drinkName);
                }
                int read = 0;
                var wrong = new List<string>();
                foreach (ReviewReason reason in Enum.GetValues(typeof(ReviewReason)))
                    foreach (string line in reviews.For(reason) ?? Array.Empty<string>())
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        read++;
                        foreach (string thing in things)
                        {
                            string said = ReputationRules.InSentence(thing);
                            string problem = DialogueRuleChecks.ArticleProblem(line, token => token == "thing" || token == "drink" ? said : "");
                            if (problem.Length > 0) { wrong.Add($"{reason}: {problem} (\"{ReputationRules.Fill(line, "Priya", thing, thing)}\")"); break; }
                        }
                    }
                run.Check(wrong.Count == 0, $"every review line ({read}) reads with every device and drink ({things.Count}): " + (wrong.Count == 0 ? "\"a\"/\"an\" right" : string.Join("; ", wrong)));
            });
    }

    // ================================================================== 3 - The man's line on a night Grace walked out (night lines)

    [MenuItem(Area + "3 - The man's line on a night Grace walked out (night lines)", false, 3)]
    static void GraceWalkedOut()
    {
        NightLines lines = null;
        string dealBefore = "", likesBefore = "";
        int lineCount = 0, sceneCount = 0;
        bool addLine = false, addScene = false;
        Playtest4Shared.Step("Playtest 4 › Content › 3 - The man's line on a night Grace walked out (night lines)", "content-3-grace-walked-out",
            survey: run =>
            {
                lines = AssetDatabase.LoadAssetAtPath<NightLines>(NightLinesPath);
                run.Require(lines != null, "No Night lines asset at " + NightLinesPath + " (Fixit Fidget › Night › Barks 1 makes it).");
                NightLines.Scene deal = SceneIn(lines, LodgerStory.DealScene);
                run.Require(deal != null && deal.lines != null, "The Night lines have no deal (" + LodgerStory.DealScene + ").");
                int places = deal.lines.Count(id => id == LodgerStory.GraceLine);
                run.Require(places == 1, $"The deal says '{LodgerStory.GraceLine}' {places} times, not once: it has been reshaped, so there's no one line to swap. Nothing changed.");
                NightLines.Line likes = LineIn(lines, LodgerStory.GraceLine);
                run.Require(likes != null, $"The deal names '{LodgerStory.GraceLine}' but the asset has no such line.");
                dealBefore = Describe(deal);
                likesBefore = likes.text;
                lineCount = lines.lines.Length;
                sceneCount = lines.scenes.Length;
                NightLines.Line walked = LineIn(lines, LodgerStory.GraceWalkedOutLine);
                NightLines.Scene sibling = SceneIn(lines, LodgerStory.DealSceneLetDown);
                addLine = walked == null;
                addScene = sibling == null;
                run.Info($"the deal: {dealBefore}");
                run.Info($"{LodgerStory.GraceLine}: \"{likes.text}\" (stays as it is)");
                run.Info(walked != null ? $"{LodgerStory.GraceWalkedOutLine}: already \"{walked.text}\" (left as it is)" : $"{LodgerStory.GraceWalkedOutLine}: to add, \"{BarkSteps.GraceWalkedOutText}\"");
                string problem = BarkRuleChecks.LetDownProblem(lines);
                run.Info(sibling == null ? $"{LodgerStory.DealSceneLetDown}: to copy from the deal"
                    : problem.Length == 0 ? $"{LodgerStory.DealSceneLetDown}: already the deal with one line swapped"
                    : $"{LodgerStory.DealSceneLetDown}: to copy from the deal again ({problem})");
            },
            change: run =>
            {
                run.Record(lines);
                run.OnFail(() => lines.Reindex());
                NightLines.Scene deal = SceneIn(lines, LodgerStory.DealScene);
                NightLines.Line likes = LineIn(lines, LodgerStory.GraceLine);
                if (addLine)
                {
                    var list = lines.lines.ToList();
                    list.Add(new NightLines.Line
                    {
                        id = LodgerStory.GraceWalkedOutLine, speaker = LodgerStory.SpeakerId,
                        situation = string.IsNullOrEmpty(likes.situation) ? LodgerStory.DealScene : likes.situation, text = BarkSteps.GraceWalkedOutText,
                    });
                    lines.lines = list.ToArray();
                    run.Changed($"{NightLinesPath}: line {LodgerStory.GraceWalkedOutLine} added: \"{BarkSteps.GraceWalkedOutText}\"");
                }
                NightLines.Scene copy = SiblingOf(deal);
                NightLines.Scene sibling = SceneIn(lines, LodgerStory.DealSceneLetDown);
                bool changed = addLine;
                if (sibling == null)
                {
                    var list = lines.scenes.ToList();
                    list.Add(copy);
                    lines.scenes = list.ToArray();
                    changed = true;
                    run.Changed($"{NightLinesPath}: scene {LodgerStory.DealSceneLetDown} added: {Describe(copy)}");
                }
                else if (BarkRuleChecks.LetDownProblem(lines).Length > 0)
                {
                    string was = Describe(sibling);
                    sibling.holdAce = copy.holdAce;
                    sibling.lines = copy.lines;
                    sibling.choices = copy.choices;
                    changed = true;
                    run.Changed($"{NightLinesPath}: scene {LodgerStory.DealSceneLetDown} copied from the deal again: {was} → {Describe(sibling)}");
                }
                else run.Info($"{LodgerStory.DealSceneLetDown}: already the deal with one line swapped");
                lines.Reindex();
                if (changed) run.SaveAsset(lines);   // saved only once every check has passed
            },
            checks: run =>
            {
                NightLines.Scene deal = SceneIn(lines, LodgerStory.DealScene), sibling = SceneIn(lines, LodgerStory.DealSceneLetDown);
                NightLines.Line likes = LineIn(lines, LodgerStory.GraceLine), walked = LineIn(lines, LodgerStory.GraceWalkedOutLine);
                run.Check(deal != null && Describe(deal) == dealBefore, "the deal itself is exactly as it was (its lines, its hold, its choices)");
                run.Check(likes != null && likes.text == likesBefore, $"\"{likesBefore}\" is exactly as it was");
                run.Check(walked != null && walked.speaker == LodgerStory.SpeakerId && !string.IsNullOrWhiteSpace(walked.text),
                    $"{LodgerStory.GraceWalkedOutLine} is the man's: \"{walked?.text}\"");
                if (walked != null)
                {
                    run.Info($"…{walked.text.Length} characters (the night lines aim for {BarkRules.LengthRule}: the Bark rules warn above it; \"{likesBefore}\" is {likesBefore.Length})");
                    run.Check(!WordBudget.Over(walked.text, WordBudget.SceneLine), $"…{WordBudget.Report(walked.text, WordBudget.SceneLine)} (a scene line's budget)");
                }
                string problem = BarkRuleChecks.LetDownProblem(lines);
                run.Check(sibling != null && problem.Length == 0, $"{LodgerStory.DealSceneLetDown} is the deal with '{LodgerStory.GraceLine}' swapped for '{LodgerStory.GraceWalkedOutLine}' and nothing else" +
                                                                   (problem.Length > 0 ? ": " + problem : ""));
                run.Check(sibling != null && sibling.choices != null && sibling.choices.Length == (deal?.choices?.Length ?? 0),
                    $"…with the deal's {deal?.choices?.Length ?? 0} choices (Ace answers twice, as in the deal)");
                run.Check(lines.lines.Length == lineCount + (addLine ? 1 : 0) && lines.scenes.Length == sceneCount + (addScene ? 1 : 0),
                    $"nothing else added: {lines.lines.Length} lines ({lineCount} before), {lines.scenes.Length} scenes ({sceneCount} before)");
                run.Check(lines.FindScene(LodgerStory.DealSceneLetDown) == sibling, "the game finds it (NightZero plays it on a night Grace's visit went wrong)");
            });
    }

    // ================================================================== 4 - Conversations: a quick second E counts (scene)

    [MenuItem(Area + "4 - Conversations: a quick second E counts (scene)", false, 4)]
    static void QuickSecondPress()
    {
        ConversationController controller = null;
        float closingPause = 0f;
        Playtest4Shared.Step("Playtest 4 › Content › 4 - Conversations: a quick second E counts (scene)", "content-4-quick-second-press",
            survey: run =>
            {
                controller = Playtest4Shared.One<ConversationController>(run);
                var so = new SerializedObject(controller);
                float was = run.Prop(so, "inputDelay").floatValue;
                closingPause = run.Prop(so, "closingPause").floatValue;
                run.Info($"ConversationController.inputDelay {was:0.##} s: a press that soon after opening was thrown away " +
                         "(the veteran's \"hear them out, hear them out\"); the frame that opened it never counts, whatever this is");
            },
            change: run => run.SetFloat(controller, "inputDelay", 0f, "ConversationController.inputDelay (seconds a quick second E was thrown away)"),
            checks: run =>
            {
                var so = new SerializedObject(controller);
                run.Check(Mathf.Approximately(run.Prop(so, "inputDelay").floatValue, 0f), "inputDelay is 0: a second E counts from the next frame");
                run.Check(Mathf.Approximately(run.Prop(so, "closingPause").floatValue, closingPause), $"closingPause is untouched ({closingPause:0.##} s)");
            });
    }

    [MenuItem(Area + "0 - Check first (read-only)", true)]
    [MenuItem(Area + "1 - Walk-ins speak their faults (scene)", true)]
    [MenuItem(Area + "2 - Grace's callbacks and a review line (assets)", true)]
    [MenuItem(Area + "3 - The man's line on a night Grace walked out (night lines)", true)]
    [MenuItem(Area + "4 - Conversations: a quick second E counts (scene)", true)]
    [MenuItem(Area + "Lab - Night 0 after Grace walked out (lab, drives itself)", true)]
    static bool NotPlaying() => Playtest4Shared.NotPlaying();

    // ================================================================== Lab - Night 0 after Grace walked out

    [MenuItem(Area + "Lab - Night 0 after Grace walked out (lab, drives itself)", false, 20)]
    static void NightZeroAfterGraceWalkedOut()
    {
        const string Tag = "[Playtest 4 › Content] ";
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        if (Object.FindAnyObjectByType<NightZeroSet>(FindObjectsInactive.Include) == null)
        {
            Debug.LogError(Tag + "There are no bins in the scene (Fixit Fidget › Night › Bins 1).");
            return;
        }
        var lines = AssetDatabase.LoadAssetAtPath<NightLines>(NightLinesPath);
        if (lines == null || SceneIn(lines, LodgerStory.DealSceneLetDown) == null)
        {
            Debug.LogError(Tag + "The deal's sibling isn't in the Night lines yet: run Fixit Fidget › Playtest 4 › Content › 3 first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, GraceWalkedOutDayOne());
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightZeroCheck.PendingKey, 1);
        PlayerPrefs.SetInt(NightZeroCheck.GraceWalkedOutKey, 1);
        PlayerPrefs.DeleteKey(NightOneCheck.PendingKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Night 0 after Grace walked out: the Night 0 play check from a made-up Day 1 on which Ace took Grace's camera and " +
                  "she walked out waiting (\"Never again\"). It drives itself through the bins, the deal (which should say \"Grace walked out.\"), " +
                  "the gnome and the return, to Grace's visit on Day 2 (three or four minutes). Keep the Game view in front and leave the mouse " +
                  $"and keyboard alone. Test save {path}; your playtest save is not used. Report: Logs/Night/night-zero-check-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    // A made-up Day 1 for the lab only (the lab's own save; the playtest save is never touched): Ace took Grace's camera, she told
    // him about Barnaby and the reunion, and she walked out waiting. The day is closed and its recap is on screen.
    static SaveData GraceWalkedOutDayOne()
    {
        var facts = new List<NotebookFactData>();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName))
        {
            fact.day = 1;
            facts.Add(fact);
        }
        NotebookFactData barnaby = NightThings.Mentioned(NightThings.GnomeOfGrace, NotebookEntries.GraceName);
        barnaby.day = 1;
        facts.Add(barnaby);
        var grace = new RegularMemoryData
        {
            profileId = GraceCameraEpisode.ProfileId,
            visits = 1,
            relationship = -2,
            lastSeenDay = 1,
            lastVisitHappy = false,
            lastJobAccepted = true,
            lastVisitServed = false,
            lastLossReason = nameof(LostReason.StormedOutWaiting),
            lastGrade = "",
            graceCameraAttempted = true,
            graceCameraReturned = false,
            graceCameraGrade = "",
            graceCameraDay = 1,
        };
        return new SaveData
        {
            day = 1,
            money = 36,
            cups = 18,
            beans = 18,
            dayCompleted = true,
            recap = new RecapSaveData
            {
                day = 1, peopleServed = 2, customersLost = 1, ordersCompleted = 2, drinks = 2, tips = 2, earned = 36,
                closingTill = 36, elapsedSeconds = 360f,
            },
            regularMemories = new[] { grace },
            notebook = facts.ToArray(),
            night = new NightSaveData(),
        };
    }

    // A request for the walked-out lab that never became a Play session must not make the next Night 0 check expect it.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(NightZeroCheck.GraceWalkedOutKey))
        {
            PlayerPrefs.DeleteKey(NightZeroCheck.GraceWalkedOutKey);
            PlayerPrefs.Save();
        }
    }

    // ---------- helpers ----------

    // What a fix would do, in the survey: change it (as it was), already done, or left alone (rewritten since).
    static void Plan(Playtest4Shared.Run run, Object target, string wasPath, string nowPath, string what, string was, string now, string why)
    {
        if (wasPath != null) run.Info($"to change  {what}: \"{Flat(was)}\" → \"{Flat(now)}\"   ({why})");
        else if (nowPath != null) run.Info($"already    {what}: \"{Flat(now)}\"");
        else run.Info($"left alone {what}: rewritten since (neither \"{Flat(was)}\" nor the fix), so the step won't touch it; the readings below say whether it reads");
    }

    // The readings: one check per line (it reads with every fault, or what's wrong), and each fill when asked.
    static void ReportReadings(Playtest4Shared.Run run, List<DialogueRuleChecks.Reading> readings, bool everyFill)
    {
        foreach (DialogueRuleChecks.Reading reading in readings)
        {
            string wrong = reading.slot.Length > 0 ? reading.slot
                : string.Join("; ", reading.said.Where(s => s.problem.Length > 0).Select(s => s.problem).Distinct());
            run.Check(reading.Reads, $"{reading.line.where}: \"{Flat(reading.line.line)}\" " +
                                     (reading.Reads ? $"reads with every fault ({reading.said.Count}); up to {reading.longest} characters a line"
                                                    : "does NOT read: " + wrong));
            if (!everyFill && reading.Reads) continue;
            foreach (var (said, problem) in reading.said)
                run.Info($"      {(problem.Length > 0 ? "WRONG " : "      ")}{Flat(said)}" + (problem.Length > 0 ? "   (" + problem + ")" : ""));
        }
    }

    // A walk-in personality's line with this text in one of its line lists (the request, "intake", unless said), as a
    // property path on the spawner, or null.
    static string WalkInPath(CustomerSpawner spawner, string personality, string text, string field = "intake")
    {
        var so = new SerializedObject(spawner);
        SerializedProperty archetypes = so.FindProperty("archetypes");
        if (archetypes == null || !archetypes.isArray) return null;
        for (int i = 0; i < archetypes.arraySize; i++)
        {
            SerializedProperty archetype = archetypes.GetArrayElementAtIndex(i);
            if (archetype.FindPropertyRelative("archetypeName")?.stringValue != personality) continue;
            SerializedProperty list = archetype.FindPropertyRelative("lines")?.FindPropertyRelative(field);
            if (list == null || !list.isArray) continue;
            for (int j = 0; j < list.arraySize; j++)
                if (list.GetArrayElementAtIndex(j).stringValue == text) return $"archetypes.Array.data[{i}].lines.{field}.Array.data[{j}]";
        }
        return null;
    }

    // An element of a string array field (e.g. "returnMemoryLines.missedVisit") with this text, as a property path, or null.
    static string ElementPath(Object target, string field, string text)
    {
        if (target == null) return null;
        var so = new SerializedObject(target);
        SerializedProperty array = so.FindProperty(field);
        if (array == null || !array.isArray) return null;
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).stringValue == text) return $"{field}.Array.data[{i}]";
        return null;
    }

    // The arrays, not the index (a step may have just changed them).
    static NightLines.Scene SceneIn(NightLines lines, string id) => lines?.scenes?.FirstOrDefault(s => s != null && s.id == id);
    static NightLines.Line LineIn(NightLines lines, string id) => lines?.lines?.FirstOrDefault(l => l != null && l.id == id);

    // The deal's sibling: its lines with Grace's line swapped, its hold, a copy of its choices (with the swap, should one follow her line).
    static NightLines.Scene SiblingOf(NightLines.Scene deal)
    {
        string Swap(string id) => id == LodgerStory.GraceLine ? LodgerStory.GraceWalkedOutLine : id;
        NightLines.Reply Copy(NightLines.Reply r) => r == null ? new NightLines.Reply() : new NightLines.Reply { line = r.line, answer = r.answer, warmth = r.warmth };
        return new NightLines.Scene
        {
            id = LodgerStory.DealSceneLetDown,
            holdAce = deal.holdAce,
            lines = (deal.lines ?? Array.Empty<string>()).Select(Swap).ToArray(),
            choices = (deal.choices ?? Array.Empty<NightLines.Choice>()).Where(c => c != null)
                .Select(c => new NightLines.Choice { after = Swap(c.after), first = Copy(c.first), second = Copy(c.second) }).ToArray(),
        };
    }

    static string Describe(NightLines.Scene s) =>
        s == null ? "(none)" : $"[{string.Join(", ", s.lines ?? Array.Empty<string>())}] {(s.holdAce ? "holds Ace" : "doesn't hold Ace")}; choices " +
                              string.Join(" ", (s.choices ?? Array.Empty<NightLines.Choice>()).Select(c => c == null ? "(none)"
                                  : $"after {c.after}: {c.first?.line}→{c.first?.answer} ({c.first?.warmth}) / {c.second?.line}→{c.second?.answer} ({c.second?.warmth})"));

    static bool Any(string[] pool, string words) => pool != null && pool.Any(l => l != null && l.Contains(words));

    static string Longest(string[] pool) => (pool ?? Array.Empty<string>()).Where(l => !string.IsNullOrWhiteSpace(l)).OrderByDescending(l => l.Length).FirstOrDefault() ?? "";

    // ConversationUI's listening beat for a line this long (0.25 s + a hundredth of a second a character, within listenBeat).
    static float Listen(int length, Vector2 beat) => Mathf.Clamp(.25f + length / 100f, Mathf.Min(beat.x, beat.y), Mathf.Max(beat.x, beat.y));

    static string Flat(string line) => (line ?? "").Replace("\n", " / ");
}
