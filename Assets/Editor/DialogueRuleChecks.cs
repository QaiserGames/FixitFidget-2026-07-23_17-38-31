using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE DIALOGUE RULES (the dialogue pass, claude/dialogue-skyrim-proposal.md §6.2)
//
// Reads every line the game says and holds it to the rules that keep talking feeling like Skyrim rather
// than a novel (GDD v4.1's "line length budget"):
//   * a line on screen is never over 150 characters (Skyrim's own cap); over 100 is listed as a warning;
//   * more to say is another line (a "\n"), never a paragraph ("\n\n");
//   * something said out loud in the room (a bubble) is one line;
//   * every {token} is one the game fills in, and no sentence starts with one ({device} and {fault} come
//     out in lower case: "my pocket watch: debris inside");
//   * Ace's replies are under 40 characters (aim for 30).
// Then Grace's Day 1 conversation, as data: her request, the question about her plans, her thanks.
// Read-only: nothing in the scene, the assets or any save changes. Needs the café scene open (for the
// walk-ins, who live on its CustomerSpawner).
// ---------------------------------------------------------------------------
public static class DialogueRuleChecks
{
    public const int HardLimit = 150;
    public const int SoftLimit = 100;
    public const int BubbleAim = 90;
    public const int ReplyLimit = 40;
    public const int ReplyAim = 30;

    const string Tag = "[Dialogue rules] ";
    static readonly string[] KnownTokens = { "{device}", "{fault}", "{a drink}", "{drink}" };
    static readonly Regex TokenStartsSentence = new Regex(@"(^|[.!?…]\s+)\{", RegexOptions.Compiled);

    [MenuItem("Fixit Fidget/Checks/Dialogue rules")]
    public static void Run()
    {
        var lint = new Lint();
        int grace = 0;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
                throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first: the walk-ins' lines live on its CustomerSpawner.");
            CheckWalkIns(lint);
            CheckRegulars(lint);
            CheckEpisodeAndNight(lint);
            CheckReplies(lint);
            grace = CheckGraceDayOne();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "FAIL: " + e.Message + "\n" + lint.Report(grace));
            return;
        }
        if (lint.Failures == 0) Debug.Log(Tag + "PASS: " + lint.Report(grace));
        else Debug.LogError(Tag + "FAIL: " + lint.Report(grace));
    }

    // ---------- where the lines are ----------

    static void CheckWalkIns(Lint lint)
    {
        CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        if (spawner == null) throw new InvalidOperationException("The café scene has no CustomerSpawner.");
        FieldInfo field = typeof(CustomerSpawner).GetField("archetypes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var archetypes = field?.GetValue(spawner) as CustomerArchetype[];
        if (archetypes == null || archetypes.Length == 0) throw new InvalidOperationException("The CustomerSpawner has no walk-in personalities.");
        foreach (CustomerArchetype archetype in archetypes)
        {
            if (archetype == null) continue;
            string who = "walk-in " + archetype.archetypeName;
            Set(lint, who, archetype.lines);
            if (archetype.lines == null || !Any(archetype.lines.drinkOrder))
                lint.Warn($"{who}: no drink orders of its own, so it orders with the shared placeholder lines " +
                          "(Fixit Fidget > Dialogue > Walk-in lines).");
        }
    }

    static void CheckRegulars(Lint lint)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null) continue;
            string who = profile.characterName;
            Set(lint, who + " (first visits)", profile.lines);
            Set(lint, who + " (returning)", profile.returnLines);
            Set(lint, who + " (trusted)", profile.warmLines);
            CustomerReturnDialogue memory = profile.returnMemoryLines;
            if (memory != null)
                foreach (CustomerReturnOutcome outcome in Enum.GetValues(typeof(CustomerReturnOutcome)))
                    Speech(lint, $"{who}, remembering ({outcome})", memory.For(outcome));
            Bubbles(lint, who + ", passable hand-back", profile.passableRepairLines);
            Bubbles(lint, who + ", rejected hand-back", profile.rejectedRepairLines);
            Bubbles(lint, who + ", drink served", profile.drinkCompletedLines);
            Bubbles(lint, who + ", story", profile.storyLines);
            lint.Bubble(who + ", asked to let Ace focus", profile.focusReply);
            lint.Speech(who + ", remembering the quiet", profile.focusReturnLine);
            foreach (ConversationTopic topic in profile.topics ?? Array.Empty<ConversationTopic>())
            {
                if (topic == null) continue;
                lint.Reply($"{who}, topic {topic.id}", topic.ask);
                lint.Speech($"{who}, topic {topic.id} (answer)", topic.answer);
            }
        }
    }

    static void CheckEpisodeAndNight(Lint lint)
    {
        lint.Speech("Grace's camera request", GraceCameraEpisode.Intake);
        lint.Speech("Grace's thanks for taking the camera", GraceCameraEpisode.AcceptedLine);
        foreach (GracePhotoOutcome outcome in new[] { GracePhotoOutcome.Clear, GracePhotoOutcome.Imperfect, GracePhotoOutcome.Missed })
        {
            lint.Speech($"Grace's photo news ({outcome})", GraceCameraEpisode.ReturnNews(outcome));
            lint.Screen($"the line on screen after her return ({outcome})", GraceCameraEpisode.HandoffLine(outcome));
        }
        foreach (JobGrade grade in Enum.GetValues(typeof(JobGrade)))
            lint.Bubble($"Grace's camera hand-back ({grade})", GraceCameraEpisode.CompletionLine(grade));
        foreach (NightThing thing in NightThings.All)
        {
            lint.Reply(thing.id + ", the question", thing.topic);
            lint.Speech(thing.id + ", the mention", thing.mention);
            lint.Bubble(thing.id + ", mentioned while waiting", thing.waitingMention);
            lint.Speech(thing.id + ", the complaint", thing.complaint);
            lint.Speech(thing.id + ", kept a straight face", thing.held);
            lint.Speech(thing.id + ", cracked", thing.cracked);
            lint.Screen(thing.id + ", taken", thing.takenNote);
            lint.Screen(thing.id + ", taken (unknown)", thing.takenNoteUnknown);
        }
        foreach (string line in CustomerIdentity.PlaceholderDrinkOrders)
            lint.Speech("a walk-in's placeholder drink order", line);
    }

    static void CheckReplies(Lint lint)
    {
        foreach (string reply in AceReplies.All) lint.Reply("Ace's reply", reply);
    }

    // A dialogue set: what's said in the conversation (several lines allowed) and in the room (one each).
    static void Set(Lint lint, string who, DialogueSet set)
    {
        if (set == null) return;
        Speech(lint, who + ", request", set.intake);
        Speech(lint, who + ", taking it", set.accepted);
        Speech(lint, who + ", ordering a drink", set.drinkOrder);
        Bubbles(lint, who + ", hand-back", set.completed);
        Bubbles(lint, who + ", turned away", set.declined);
        Bubbles(lint, who + ", reassured", set.reassured);
        Bubbles(lint, who + ", storming out", set.stormedOut);
        Bubbles(lint, who + ", a drink while waiting", set.orderedDrink);
    }

    static void Speech(Lint lint, string where, string[] pool)
    {
        foreach (string line in pool ?? Array.Empty<string>()) lint.Speech(where, line);
    }

    static void Bubbles(Lint lint, string where, string[] pool)
    {
        foreach (string line in pool ?? Array.Empty<string>()) lint.Bubble(where, line);
    }

    static bool Any(string[] pool) => pool != null && pool.Any(s => !string.IsNullOrWhiteSpace(s));

    // ---------- Grace's Day 1 conversation, as data ----------

    static int CheckGraceDayOne()
    {
        int n = 0;
        void Require(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>("Assets/Data/Regulars/Regular_Grace.asset");
        Require(grace != null, "Grace's profile is at Assets/Data/Regulars/Regular_Grace.asset.");
        var root = new GameObject("Dialogue rules check") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            NightThing gnome = NightThings.GnomeOfGrace;
            var identity = root.AddComponent<CustomerIdentity>();
            var camera = new Job
            {
                kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "jammed shutter",
                storyEpisodeId = GraceCameraEpisode.EpisodeId
            };
            identity.SetupRegular(grace);
            identity.SetDevice(camera.Subject);
            identity.SetFault(camera.faultDescription);
            identity.SetStoryRequest(camera);
            Require(identity.IsGraceCameraRequest, "Her camera job is her camera episode.");
            Require(identity.Say(CustomerIdentity.Beat.Intake) == GraceCameraEpisode.Intake, "Her Day 1 request is the episode's two short lines.");
            IReadOnlyList<TopicChoice> topics = identity.Topics();
            Require(topics.Count >= 1 && topics[0].Ask == gnome.topic && topics[0].Thing == gnome,
                $"On her camera visit Ace can ask \"{gnome.topic}\".");
            Require(identity.PeekWaitingMention(out string waiting) && waiting == gnome.waitingMention,
                "Not asked yet: she has Barnaby to mention while she waits.");
            Require(identity.Ask(topics[0]) == gnome.mention && topics[0].Asked,
                "Asked: she answers with the mention, and the question is marked asked.");
            Require(!identity.PeekWaitingMention(out _), "…and she won't mention him again while she waits.");
            Require(ReferenceEquals(identity.Topics(), topics) && identity.Topics()[0].Asked, "The question stays asked all visit.");
            Require(identity.Say(CustomerIdentity.Beat.Accepted) == GraceCameraEpisode.AcceptedLine, "Her thanks when Ace takes it is the reveal.");

            identity.SetupWalkIn(new CustomerArchetype { lines = new DialogueSet { intake = new[] { "My {device}: {fault}." } } }, "Walk-in");
            Require(identity.Topics().Count == 0, "A walk-in has nothing to ask about.");
            identity.SetDevice("Pocket Watch");
            identity.SetFault("Debris Inside");
            Require(identity.Say(CustomerIdentity.Beat.Intake) == "My pocket watch: debris inside.",
                "A walk-in says the device the way a sentence does (\"my pocket watch\", not \"my Pocket Watch\").");
            Require(CustomerIdentity.SpokenName("TV") == "TV", "…and a name in capitals keeps them (\"TV\").");

            identity.SetupWalkIn(new CustomerArchetype
            {
                lines = new DialogueSet { intake = new[] { "My {device}: {fault}." }, drinkOrder = new[] { "Just {a drink}. Quick." } }
            }, "Walk-in");
            var espresso = new Job { kind = JobKind.Drink, deviceName = "Espresso" };
            identity.SetDevice(espresso.Subject);
            identity.SetFault(espresso.faultDescription);
            identity.SetStoryRequest(espresso);
            Require(identity.Say(CustomerIdentity.Beat.Intake) == "Just an espresso. Quick.", "A walk-in orders a drink in its personality's own words.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        return n;
    }

    // ---------- the rules ----------

    sealed class Lint
    {
        public int Lines, Replies, Failures;
        readonly List<string> problems = new List<string>();
        readonly List<string> warnings = new List<string>();

        public void Warn(string what) => warnings.Add(what);

        void Fail(string what)
        {
            Failures++;
            problems.Add(what);
        }

        // Said in the conversation: several lines are fine, each short.
        public void Speech(string where, string line) => Said(where, line, false);

        // Said out loud in the room: one line.
        public void Bubble(string where, string line) => Said(where, line, true);

        void Said(string where, string line, bool bubble)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Lines++;
            if (line.Contains("\n\n")) Fail($"{where}: a paragraph (a blank line inside): \"{Short(line)}\"");
            if (bubble && line.Contains("\n")) Fail($"{where}: said in the room, so one line only: \"{Short(line)}\"");
            foreach (string raw in line.Replace("\r", "").Split('\n'))
            {
                string beat = raw.Trim();
                if (beat.Length == 0)
                {
                    if (!line.Contains("\n\n")) Fail($"{where}: an empty line at the start or end: \"{Short(line)}\"");
                    continue;
                }
                if (beat.Length > HardLimit) Fail($"{where}: {beat.Length} characters, over {HardLimit}: \"{Short(beat)}\"");
                else if (beat.Length > SoftLimit) Warn($"{where}: {beat.Length} characters (aim for 90): \"{Short(beat)}\"");
                else if (bubble && beat.Length > BubbleAim) Warn($"{where}: {beat.Length} characters for a bubble (aim for 60-90): \"{Short(beat)}\"");
                if (TokenStartsSentence.IsMatch(beat)) Fail($"{where}: a sentence starts with a token: \"{Short(beat)}\"");
            }
            string filled = line;
            foreach (string token in KnownTokens) filled = filled.Replace(token, "x");
            if (filled.Contains("{") || filled.Contains("}")) Fail($"{where}: a token the game doesn't fill in: \"{Short(line)}\"");
        }

        // One of Ace's replies (or a question Ace can ask).
        public void Reply(string where, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) { Fail($"{where}: empty"); return; }
            Replies++;
            if (line.Contains("\n")) Fail($"{where}: a reply is one line: \"{line}\"");
            if (line.Length > ReplyLimit) Fail($"{where}: {line.Length} characters, over {ReplyLimit}: \"{line}\"");
            else if (line.Length > ReplyAim) Warn($"{where}: {line.Length} characters (aim for under {ReplyAim}): \"{line}\"");
        }

        // A short line on screen that isn't speech (a note): one line, not too long.
        public void Screen(string where, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Lines++;
            if (line.Contains("\n")) Fail($"{where}: a note on screen is one line: \"{Short(line)}\"");
            if (line.Length > HardLimit) Fail($"{where}: {line.Length} characters, over {HardLimit}: \"{Short(line)}\"");
        }

        public string Report(int graceChecks)
        {
            var text = new StringBuilder();
            text.Append($"{Lines} lines and {Replies} replies read, {Failures} problem(s), {warnings.Count} warning(s)");
            if (graceChecks > 0) text.Append($"; Grace's Day 1 conversation: {graceChecks} checks passed");
            text.Append(". Read-only: no scene, asset or save changes.");
            foreach (string problem in problems) text.Append("\n  PROBLEM  ").Append(problem);
            foreach (string warning in warnings) text.Append("\n  warning  ").Append(warning);
            return text.ToString();
        }

        static string Short(string line)
        {
            string one = (line ?? "").Replace("\n", " / ");
            return one.Length <= 90 ? one : one.Substring(0, 87) + "...";
        }
    }
}
