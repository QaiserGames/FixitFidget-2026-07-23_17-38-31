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
//     out in lower case: "my pocket watch: there's grit inside and it won't keep time");
//   * Ace's replies are under 40 characters (aim for 30).
// Then Grace's Day 1 conversation, as data: her request, the question about her plans, her thanks.
//
// Playtest 4 (10 Oct 2026, the first-hour playtester: "Walk-ins read their fault labels aloud"), two more rules:
//   1. every fault label the game can roll (every DeviceFault on every device a day, the café's walk-ins, a regular or a
//      featured repair can bring, and on the device prefabs) has a spoken form (FaultSpeech), and that form is a lower-case
//      clause with no full stop that isn't the label;
//   2. every line that names the device or the fault ({device}, {fault}), filled in with every fault a visit can roll,
//      reads as speech: no label said word for word, no " - ", no Words In Title Case, never over 150 characters, "a"/"an"
//      right. And it's written so it can: {fault} follows "my {device}:" (or "—" or ","), because it's said as a clause;
//      {device} has "my", "this", "the" or "your" before it.
// And the review lines on the phone (ReviewLines): one line each, only {name}, {thing} and {drink}, "a"/"an" right with
// every device and drink ("Came for a espresso" was one).
// Read-only: nothing in the scene, the assets or any save changes. Needs the café scene open (for the
// walk-ins, who live on its CustomerSpawner). Fixit Fidget › Playtest 4 › Content › 0 lists every fill, line by line.
// ---------------------------------------------------------------------------
public static class DialogueRuleChecks
{
    public const int HardLimit = 150;
    public const int SoftLimit = 100;
    public const int BubbleAim = 90;
    public const int ReplyLimit = 40;
    public const int ReplyAim = 30;
    /// <summary>A spoken form longer than this makes a 90-character line hard to keep (a warning).</summary>
    public const int SpokenAim = 50;

    const string Tag = "[Dialogue rules] ";
    static readonly string[] KnownTokens = { "{device}", "{fault}", "{a drink}", "{drink}" };
    static readonly string[] ReviewTokens = { "{name}", "{thing}", "{drink}" };
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
            CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
            if (spawner == null) throw new InvalidOperationException("The café scene has no CustomerSpawner.");
            CheckWalkIns(lint, spawner);
            CheckRegulars(lint);
            CheckEpisodeAndNight(lint);
            CheckReplies(lint);
            List<RolledFault> faults = RolledFaults(spawner);
            CheckFaults(lint, spawner, faults);
            CheckReviews(lint, faults);
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

    /// <summary>The café's walk-in personalities (on the scene's CustomerSpawner).</summary>
    public static CustomerArchetype[] WalkIns(CustomerSpawner spawner)
    {
        if (spawner == null) return Array.Empty<CustomerArchetype>();
        FieldInfo field = typeof(CustomerSpawner).GetField("archetypes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field?.GetValue(spawner) as CustomerArchetype[] ?? Array.Empty<CustomerArchetype>();
    }

    static void CheckWalkIns(Lint lint, CustomerSpawner spawner)
    {
        CustomerArchetype[] archetypes = WalkIns(spawner);
        if (archetypes.Length == 0) throw new InvalidOperationException("The CustomerSpawner has no walk-in personalities.");
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
        lint.Speech("Grace's shutter request, after a good clean", GraceCameraEpisode.ShutterIntake(true));
        lint.Speech("Grace's shutter request, otherwise", GraceCameraEpisode.ShutterIntake(false));
        lint.Speech("Grace's thanks for taking the camera again", GraceCameraEpisode.ShutterAcceptedLine);
        foreach (JobGrade shutterGrade in Enum.GetValues(typeof(JobGrade)))
            lint.Bubble($"Grace's shutter hand-back ({shutterGrade})", GraceCameraEpisode.ShutterCompletionLine(shutterGrade));
        foreach (GracePhotoOutcome outcome in new[] { GracePhotoOutcome.Clear, GracePhotoOutcome.Imperfect, GracePhotoOutcome.Missed })
        {
            lint.Speech($"Grace's photo news ({outcome})", GraceCameraEpisode.ReturnNews(outcome));
            lint.Screen($"the line on screen after her return ({outcome})", GraceCameraEpisode.HandoffLine(outcome));
        }
        foreach (JobGrade grade in Enum.GetValues(typeof(JobGrade)))
            lint.Bubble($"Grace's camera hand-back ({grade})", GraceCameraEpisode.CompletionLine(grade));
        foreach (NightThing thing in NightThings.All)
        {
            // A thing nobody talks about by day has no question to ask (the reunion cups: Ace first hears of them from the
            // man at the bins, NightThings). One that is talked about must have its question.
            if (!string.IsNullOrWhiteSpace(thing.topic) || !string.IsNullOrWhiteSpace(thing.mention))
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

    // ---------- faults, said (playtest 4) ----------

    /// <summary>
    /// A fault the game can roll: its device's name, its label, where it comes from, and whether lines with {device} and
    /// {fault} can say it (a device a visit rolls) or only a featured story visit that speaks in its own words (Grace's camera).
    /// </summary>
    public sealed class RolledFault
    {
        public string device = "", label = "", from = "";
        public bool templated;
    }

    /// <summary>
    /// Every fault on every device the game can roll: the café's walk-ins' devices (the CustomerSpawner), every day's, every
    /// regular's own device, every day's featured repair, and any device prefab in Assets/AssetsPrefabs or Assets/GraceShowcase.
    /// </summary>
    public static List<RolledFault> RolledFaults(CustomerSpawner spawner)
    {
        var devices = new List<(GameObject prefab, bool templated, string from)>();
        void Add(GameObject prefab, bool templated, string from)
        {
            if (prefab == null || prefab.GetComponent<DeviceDefinition>() == null) return;
            int at = devices.FindIndex(d => d.prefab == prefab);
            if (at < 0) devices.Add((prefab, templated, from));
            else if (templated && !devices[at].templated) devices[at] = (prefab, true, from);
        }
        if (spawner != null)
        {
            FieldInfo field = typeof(CustomerSpawner).GetField("devicePrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field?.GetValue(spawner) is GameObject[] pool)
                foreach (GameObject device in pool) Add(device, true, "the café's walk-ins");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:DayDefinition"))
        {
            var day = AssetDatabase.LoadAssetAtPath<DayDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (day == null) continue;
            foreach (GameObject device in day.devices ?? Array.Empty<GameObject>()) Add(device, true, day.name);
            FeaturedRepairRequest featured = day.featuredRepair;
            if (featured != null && featured.devicePrefab != null)
                Add(featured.devicePrefab, string.IsNullOrEmpty(featured.storyEpisodeId), day.name + "'s featured repair");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile != null && profile.preferredDevice != null) Add(profile.preferredDevice, true, profile.characterName + "'s own");
        }
        string[] folders = new[] { "Assets/AssetsPrefabs", "Assets/GraceShowcase" }.Where(AssetDatabase.IsValidFolder).ToArray();
        if (folders.Length > 0)
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
                Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)), false, "a device prefab");

        var faults = new List<RolledFault>();
        foreach (var (prefab, templated, from) in devices)
        {
            DeviceDefinition definition = prefab.GetComponent<DeviceDefinition>();
            foreach (DeviceFault fault in definition.faults ?? Array.Empty<DeviceFault>())
                if (fault != null && !string.IsNullOrWhiteSpace(fault.description))
                    faults.Add(new RolledFault
                    {
                        device = definition.displayName, label = fault.description.Trim(), from = from + " · " + prefab.name, templated = templated,
                    });
        }
        return faults;
    }

    /// <summary>A line that names the device or the fault ({device}, {fault}), and where it's said.</summary>
    public sealed class FaultLine
    {
        public string where = "", line = "";
    }

    /// <summary>Every line that says {device} or {fault}: the walk-ins' (the café scene's CustomerSpawner) and every regular's.</summary>
    public static List<FaultLine> FaultLines(CustomerSpawner spawner)
    {
        var found = new List<FaultLine>();
        void One(string where, string line)
        {
            if (!string.IsNullOrWhiteSpace(line) && (line.Contains("{fault}") || line.Contains("{device}")))
                found.Add(new FaultLine { where = where, line = line });
        }
        void Pool(string where, string[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++) One($"{where} [{i}]", pool[i]);
        }
        void Lines(string who, DialogueSet set)
        {
            if (set == null) return;
            Pool(who + ", request", set.intake);
            Pool(who + ", taking it", set.accepted);
            Pool(who + ", hand-back", set.completed);
            Pool(who + ", turned away", set.declined);
            Pool(who + ", reassured", set.reassured);
            Pool(who + ", storming out", set.stormedOut);
            Pool(who + ", a drink while waiting", set.orderedDrink);
            Pool(who + ", ordering a drink", set.drinkOrder);
        }
        foreach (CustomerArchetype archetype in WalkIns(spawner))
            if (archetype != null) Lines("walk-in " + archetype.archetypeName, archetype.lines);
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null) continue;
            string who = profile.characterName;
            Lines(who + " (first visits)", profile.lines);
            Lines(who + " (returning)", profile.returnLines);
            Lines(who + " (trusted)", profile.warmLines);
            if (profile.returnMemoryLines != null)
                foreach (CustomerReturnOutcome outcome in Enum.GetValues(typeof(CustomerReturnOutcome)))
                    Pool($"{who}, remembering ({outcome})", profile.returnMemoryLines.For(outcome));
            Pool(who + ", passable hand-back", profile.passableRepairLines);
            Pool(who + ", rejected hand-back", profile.rejectedRepairLines);
            Pool(who + ", drink served", profile.drinkCompletedLines);
            Pool(who + ", story", profile.storyLines);
            One(who + ", asked to let Ace focus", profile.focusReply);
            One(who + ", remembering the quiet", profile.focusReturnLine);
            foreach (ConversationTopic topic in profile.topics ?? Array.Empty<ConversationTopic>())
                if (topic != null) One($"{who}, topic {topic.id} (answer)", topic.answer);
        }
        return found;
    }

    /// <summary>One fault line read with every fault a visit can roll: what it says, and what's wrong (empty: it reads).</summary>
    public sealed class Reading
    {
        public FaultLine line;
        /// <summary>What's wrong with how the line is written, whatever fills it ("" when nothing is).</summary>
        public string slot = "";
        public readonly List<(string said, string problem)> said = new List<(string said, string problem)>();
        /// <summary>The longest line on screen once filled in, and what it says.</summary>
        public int longest;
        public string longestSaid = "";
        public bool Reads => slot.Length == 0 && said.All(s => s.problem.Length == 0);
    }

    /// <summary>Each line filled in, the way the game fills it (CustomerIdentity), with every fault a visit can roll.</summary>
    public static List<Reading> ReadFaultLines(List<FaultLine> lines, List<RolledFault> faults)
    {
        List<string> labels = faults.Select(f => f.label).Distinct().ToList();
        List<RolledFault> rolled = faults.Where(f => f.templated).ToList();
        var readings = new List<Reading>();
        var root = new GameObject("Dialogue rules check (faults)") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var identity = root.AddComponent<CustomerIdentity>();
            foreach (FaultLine line in lines)
            {
                var reading = new Reading { line = line, slot = SlotProblem(line.line) };
                foreach (RolledFault fault in rolled)
                {
                    string said = Say(identity, line.line, fault.device, fault.label);
                    string problem = "";
                    if (FaultSpeech.ReadsLikeALabel(said, labels, out string why)) problem = why;
                    foreach (string raw in said.Split('\n'))
                    {
                        string beat = raw.Trim();
                        if (beat.Length > reading.longest) { reading.longest = beat.Length; reading.longestSaid = beat; }
                        if (problem.Length == 0 && beat.Length > HardLimit) problem = $"is {beat.Length} characters, over {HardLimit}";
                    }
                    if (problem.Length == 0)
                    {
                        string device = CustomerIdentity.SpokenName(fault.device), spoken = FaultSpeech.Spoken(fault.label);
                        problem = ArticleProblem(line.line, token => token == "device" ? device : token == "fault" ? spoken : "");
                    }
                    reading.said.Add((said, problem));
                }
                readings.Add(reading);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        return readings;
    }

    // A line said through the game's own filling (CustomerIdentity.Format, via a walk-in with that one line).
    static string Say(CustomerIdentity identity, string line, string device, string label)
    {
        identity.SetupWalkIn(new CustomerArchetype { lines = new DialogueSet { intake = new[] { line } } }, "Dialogue rules");
        identity.SetDevice(device);
        identity.SetFault(label);
        return identity.Say(CustomerIdentity.Beat.Intake);
    }

    static readonly string[] DeviceWords = { "my", "this", "the", "your", "her", "his", "our", "their", "that" };

    /// <summary>
    /// What's wrong with how a line is written for its tokens, or "": {fault} is said as a clause ("the screen's cracked"), so it
    /// follows "my {device}:" (or "—" or ","), never "This is" or "has"; {device} has "my", "this", "the" or "your" before it.
    /// </summary>
    public static string SlotProblem(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";
        for (int at = line.IndexOf("{fault}", StringComparison.Ordinal); at >= 0; at = line.IndexOf("{fault}", at + 1, StringComparison.Ordinal))
        {
            string before = line.Substring(0, at).TrimEnd();
            char last = before.Length > 0 ? before[before.Length - 1] : '\0';
            if (last != ':' && last != '—' && last != '–' && last != ',')
                return "{fault} is said as a clause (\"the screen's cracked\"), so it follows \"my {device}:\" (or \"—\", or \",\"), " +
                       $"not \"{LastWords(before, 2)}\"";
        }
        for (int at = line.IndexOf("{device}", StringComparison.Ordinal); at >= 0; at = line.IndexOf("{device}", at + 1, StringComparison.Ordinal))
        {
            string word = LastWords(line.Substring(0, at), 1).ToLowerInvariant();
            if (Array.IndexOf(DeviceWords, word) < 0)
                return $"{{device}} is said with \"my\", \"this\", \"the\" or \"your\" before it (\"my phone\"), not \"{(word.Length > 0 ? word : "nothing")}\"";
        }
        return "";
    }

    static string LastWords(string text, int count)
    {
        string[] words = (text ?? "").Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Skip(Math.Max(0, words.Length - count)));
    }

    static readonly Regex ArticleBeforeToken = new Regex(@"\b([Aa]n?) \{(device|fault|thing|drink)\}", RegexOptions.Compiled);

    /// <summary>"a"/"an" before a token that doesn't suit what fills it ("a espresso"), or "". <paramref name="fill"/> gives a token's
    /// words by its name ("device", "fault", "thing", "drink").</summary>
    public static string ArticleProblem(string line, Func<string, string> fill)
    {
        if (string.IsNullOrEmpty(line)) return "";
        foreach (Match m in ArticleBeforeToken.Matches(line))
        {
            string article = m.Groups[1].Value.ToLowerInvariant(), value = fill(m.Groups[2].Value) ?? "";
            if (value.Length == 0) continue;
            bool vowel = "aeiou".IndexOf(char.ToLowerInvariant(value[0])) >= 0;
            if (article == "a" && vowel || article == "an" && !vowel)
                return $"says \"{m.Groups[1].Value} {value}\"";
        }
        return "";
    }

    /// <summary>What's wrong with a fault's spoken form, or "": it follows "my {device}:", so it's a lower-case clause with no full
    /// stop, and it isn't the label.</summary>
    public static string SpokenFormProblem(string label, string said)
    {
        if (string.IsNullOrWhiteSpace(said)) return "is empty";
        if (char.IsUpper(said[0])) return "starts with a capital (it follows \"my {device}:\", so it's lower case)";
        char end = said.TrimEnd()[said.TrimEnd().Length - 1];
        if (end == '.' || end == '!' || end == '?') return "ends a sentence (the line goes on after it: no full stop)";
        if (string.Equals(said.Trim(), (label ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return "is the label itself";
        if (FaultSpeech.ReadsLikeALabel(said, new[] { label }, out string why)) return why;
        if (said.Contains("{")) return "has a token in it";
        return "";
    }

    static void CheckFaults(Lint lint, CustomerSpawner spawner, List<RolledFault> faults)
    {
        if (faults.Count == 0)
        {
            lint.Fail("faults: no device the game can roll has a fault (the spawner's devices, the days', the regulars', the device prefabs)");
            return;
        }
        // Rule 1: every fault label the game can roll has a spoken form, and it is one.
        foreach (IGrouping<string, RolledFault> label in faults.GroupBy(f => f.label, StringComparer.OrdinalIgnoreCase))
        {
            lint.Faults++;
            RolledFault fault = label.First();
            if (!FaultSpeech.TryGet(fault.label, out string said))
            {
                lint.Fail($"fault \"{fault.label}\" ({fault.device}, {fault.from}): no spoken form, so customers would say the label (add it to FaultSpeech)");
                continue;
            }
            string problem = SpokenFormProblem(fault.label, said);
            if (problem.Length > 0) lint.Fail($"fault \"{fault.label}\": its spoken form \"{said}\" {problem}");
            // Only a fault that lines fill in has to leave them room (Grace's camera speaks in her own words).
            else if (said.Length > SpokenAim && label.Any(f => f.templated))
                lint.Warn($"fault \"{fault.label}\": its spoken form is {said.Length} characters (aim for {SpokenAim} or less, so a line keeps to 90): \"{said}\"");
        }
        // Rule 2: every line that names the device or the fault reads as speech, filled with every fault a visit can roll.
        foreach (Reading reading in ReadFaultLines(FaultLines(spawner), faults))
        {
            lint.FaultLines++;
            lint.Fills += reading.said.Count;
            if (reading.slot.Length > 0) lint.Fail($"{reading.line.where}: {reading.slot}: \"{Short(reading.line.line)}\"");
            foreach (IGrouping<string, (string said, string problem)> wrong in reading.said.Where(s => s.problem.Length > 0).GroupBy(s => s.problem))
                lint.Fail($"{reading.line.where}: filled in, it {wrong.Key} ({wrong.Count()} of {reading.said.Count}): \"{Short(wrong.First().said)}\"");
            if (reading.longest > SoftLimit && reading.longest <= HardLimit)
                lint.Warn($"{reading.line.where}: up to {reading.longest} characters once filled in (aim for 90): \"{Short(reading.longestSaid)}\"");
        }
    }

    // ---------- the review lines on the phone (playtest 4) ----------

    static void CheckReviews(Lint lint, List<RolledFault> faults)
    {
        // {thing} is what they came for: a device's name or a drink's, said in a sentence (ReputationRules.InSentence).
        var things = faults.Select(f => f.device).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
        var drinks = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:DrinkDefinition"))
        {
            var drink = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (drink != null && !string.IsNullOrWhiteSpace(drink.drinkName)) drinks.Add(drink.drinkName);
        }
        things.AddRange(drinks);
        string aDrink = drinks.Count > 0 ? drinks[0] : "coffee";
        foreach (string guid in AssetDatabase.FindAssets("t:ReviewLines"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var reviews = AssetDatabase.LoadAssetAtPath<ReviewLines>(path);
            if (reviews == null) continue;
            foreach (ReviewReason reason in Enum.GetValues(typeof(ReviewReason)))
                foreach (string line in reviews.For(reason) ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    lint.Reviews++;
                    string where = $"review ({reason})";
                    if (line.Contains("\n")) lint.Fail($"{where}: a review is one line: \"{Short(line)}\"");
                    if (line.Length > HardLimit) lint.Fail($"{where}: {line.Length} characters, over {HardLimit}: \"{Short(line)}\"");
                    string bare = line;
                    foreach (string token in ReviewTokens) bare = bare.Replace(token, "x");
                    if (bare.Contains("{") || bare.Contains("}")) lint.Fail($"{where}: a token the phone doesn't fill in (it fills {{name}}, {{thing}}, {{drink}}): \"{Short(line)}\"");
                    foreach (string thing in things)
                    {
                        string said = ReputationRules.InSentence(thing), drinkSaid = ReputationRules.InSentence(aDrink);
                        string problem = ArticleProblem(line, token => token == "thing" ? said : token == "drink" ? drinkSaid : "");
                        if (problem.Length == 0) continue;
                        lint.Fail($"{where}: filled in, it {problem}: \"{ReputationRules.Fill(line, "Priya", thing, aDrink)}\"");
                        break;
                    }
                }
        }
    }

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
                kind = JobKind.Repair, deviceName = "reunion film camera", faultDescription = "Dirty Lens and Film Path",
                faultType = FaultType.Cleaning, storyEpisodeId = GraceCameraEpisode.EpisodeId   // Day 1 is the clean (14 Oct)
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
            // Playtest 4: the fault is said the way a person says it (FaultSpeech), never its label.
            const string Debris = "Debris Inside, Won't Tell Time";
            identity.SetDevice("Pocket Watch");
            identity.SetFault(Debris);
            Require(FaultSpeech.TryGet(Debris, out string debris) && identity.Say(CustomerIdentity.Beat.Intake) == "My pocket watch: " + debris + ".",
                $"A walk-in says the device the way a sentence does (\"my pocket watch\") and the fault the way a person does (\"{debris}\"), not its label.");
            Require(CustomerIdentity.SpokenName("TV") == "TV", "…and a name in capitals keeps them (\"TV\").");
            identity.SetFault("Jammed Gears");
            Require(identity.Say(CustomerIdentity.Beat.Intake) == "My pocket watch: jammed gears.",
                "A label FaultSpeech doesn't know is said in lower case, as before.");

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

    static string Short(string line)
    {
        string one = (line ?? "").Replace("\n", " / ");
        return one.Length <= 90 ? one : one.Substring(0, 87) + "...";
    }

    // ---------- the rules ----------

    sealed class Lint
    {
        public int Lines, Replies, Failures;
        public int Faults, FaultLines, Fills, Reviews;
        readonly List<string> problems = new List<string>();
        readonly List<string> warnings = new List<string>();

        public void Warn(string what) => warnings.Add(what);

        public void Fail(string what)
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
            if (Faults > 0) text.Append($"; faults: {Faults} labels, each with a spoken form checked; {FaultLines} lines that name a device or a fault, filled in {Fills} ways");
            if (Reviews > 0) text.Append($"; {Reviews} review lines");
            if (graceChecks > 0) text.Append($"; Grace's Day 1 conversation: {graceChecks} checks passed");
            text.Append(". Read-only: no scene, asset or save changes.");
            foreach (string problem in problems) text.Append("\n  PROBLEM  ").Append(problem);
            foreach (string warning in warnings) text.Append("\n  warning  ").Append(warning);
            return text.ToString();
        }
    }
}
