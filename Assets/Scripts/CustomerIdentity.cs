using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One thing Ace can ask about on this visit (CustomerIdentity.Topics): a line in the conversation's reply
/// list and the answer (the dialogue pass, claude/dialogue-skyrim-proposal.md §6.4). The same object all
/// visit long, so a topic stays greyed once asked.
/// </summary>
public sealed class TopicChoice
{
    public readonly string Id;
    /// <summary>What Ace says, in the reply list.</summary>
    public readonly string Ask;
    /// <summary>Their answer: one line per beat.</summary>
    public readonly string Answer;
    /// <summary>The night thing it's about (their mention of it), or null.</summary>
    public readonly NightThing Thing;
    public bool Asked { get; internal set; }
    /// <summary>Asking it put something new in Ace's notebook.</summary>
    public bool Taught { get; internal set; }

    public TopicChoice(string id, string ask, string answer, NightThing thing)
    {
        Id = id ?? "";
        Ask = ask ?? "";
        Answer = answer ?? "";
        Thing = thing;
    }
}

public class CustomerIdentity : MonoBehaviour
{
    // Only ever APPEND to this — it's serialized by index in DialogueSet lookups
    // and headed for save data with the memory pass.
    public enum Beat { Intake, Accepted, Completed, Declined, Reassured, StormedOut, OrderedDrink }

    public string DisplayName { get; private set; } = "Customer";
    public Color ThemeColor { get; private set; } = Color.white;
    public bool IsRegular => profile != null;
    public CustomerProfile Profile => profile;

    // Exposed for the day log, so a run can be read back as "the impatient
    // ones are the ones storming out" rather than just "three people left".
    public CustomerArchetype Archetype => archetype;
    public int Relationship { get; private set; }
    public bool HasMetBefore { get; private set; }
    public bool RemembersFocusBoundary => previousVisit != null && previousVisit.focusBoundarySet;
    /// <summary>This visit is Grace's camera episode (her intake is the authored story).</summary>
    public bool IsGraceCameraRequest => isGraceCameraRequest;
    public CustomerReturnOutcome ReturnOutcome => CustomerReturnPolicy.Classify(previousVisit);
    public PortraitExpression Expression { get; private set; } = PortraitExpression.Neutral;
    // Regulars have faces. Walk-ins fall back to a silhouette in the UI.
    public Sprite Portrait => PortraitAt(1f);

    private CustomerProfile profile;
    private CustomerArchetype archetype;
    private RegularMemoryData previousVisit;
    private Beat lastBeat = Beat.Intake;
    private string deviceName = "thing";
    private bool isGraceCameraRequest;
    // A visit for a drink only: the intake orders it by name ({a drink}, {drink}) rather than bringing
    // a device in (the repair lines would say "my Latte: broken").
    private bool drinkVisit;
    private string drinkName = "drink";
    // A face set for a line outside the usual beats (Feel: the morning after a night). The panel shows it
    // until their next line, however low their patience.
    private bool feltAside;
    // The dialogue pass: this visit's things to ask about (built when first asked for), whether they have
    // told Ace about their night thing yet, and a short line for the screen once the conversation closes.
    private List<TopicChoice> topics;
    private bool nightMentionHeard;
    private string closingNote = "";
    private const int MaxTopics = 2;

    // Lowercased on the way in, because it arrives as a ticket label
    // ("Cracked Screen") and comes out mid-sentence ("my phone, cracked
    // screen"). Capitals mid-line read as a UI string that leaked into speech.
    private string faultName = "something";

    public void SetupRegular(
        CustomerProfile p,
        int relationship = 0,
        bool hasMetBefore = false)
    {
        profile = p;
        archetype = null;
        DisplayName = p.characterName;
        ThemeColor = p.themeColor;
        Relationship = relationship;
        HasMetBefore = hasMetBefore;
        previousVisit = null;
        isGraceCameraRequest = false;
        drinkVisit = false;
        feltAside = false;
        ResetVisitTalk();
        lastBeat = Beat.Intake;
        Expression = PortraitExpression.Neutral;
    }

    public void SetupRegular(CustomerProfile p, RegularMemoryData memory)
    {
        SetupRegular(p, memory != null ? memory.relationship : 0, memory != null && memory.visits > 0);
        previousVisit = memory != null ? memory.Copy() : null;
    }

    public void SetupWalkIn(CustomerArchetype a, string name)
    {
        archetype = a;
        profile = null;
        DisplayName = name;
        ThemeColor = a != null ? a.moodColor : Color.white;
        Relationship = 0;
        HasMetBefore = false;
        previousVisit = null;
        isGraceCameraRequest = false;
        drinkVisit = false;
        feltAside = false;
        ResetVisitTalk();
        lastBeat = Beat.Intake;
        Expression = PortraitExpression.Neutral;
    }

    private void ResetVisitTalk()
    {
        topics = null;
        nightMentionHeard = false;
        closingNote = "";
    }

    // A ticket names it "Pocket Watch"; a sentence says "my pocket watch" (the dialogue pass: what
    // DeviceDefinition.displayName's tooltip always asked for).
    public void SetDevice(string device)
    {
        if (!string.IsNullOrEmpty(device)) deviceName = SpokenName(device);
    }

    /// <summary>A device's name as a sentence says it: "Pocket Watch" is "pocket watch"; a word in capitals ("TV") keeps them.</summary>
    public static string SpokenName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        string[] words = name.Trim().Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (word.Length < 2 || !char.IsUpper(word[0])) continue;
            string rest = word.Substring(1);
            if (rest == rest.ToLowerInvariant()) words[i] = char.ToLowerInvariant(word[0]) + rest;
        }
        return string.Join(" ", words);
    }

    /// <summary>What's actually wrong with it, for the {fault} token.</summary>
    public void SetFault(string fault)
    {
        if (!string.IsNullOrEmpty(fault)) faultName = fault.ToLowerInvariant();
    }

    public void SetStoryRequest(Job job)
    {
        isGraceCameraRequest = profile != null && job != null && job.kind == JobKind.Repair
            && GraceCameraEpisode.Matches(profile.PersistentId, job.storyEpisodeId);
        drinkVisit = job != null && job.kind == JobKind.Drink;
        if (drinkVisit)
        {
            string ordered = job.drink != null ? job.drink.drinkName : job.deviceName;
            drinkName = string.IsNullOrWhiteSpace(ordered) ? "drink" : ordered.Trim().ToLowerInvariant();
        }
        topics = null;   // the camera visit's topics depend on the request
    }

    private bool HasGraceReturn => GraceCameraEpisode.HasPendingReturn(previousVisit,
        DayClock.Instance != null ? DayClock.Instance.Day : 0);

    // Called only after the player accepts the return visit. Opening/reopening
    // dialogue is read-only and cannot silently award the shop a keepsake.
    // Since the dialogue pass the photo's news is said here, once her order is taken, in place of her
    // usual thanks; what happened (the print left for the shop) is a line on screen once the conversation
    // closes (TakeClosingNote), never a narrator in the speech panel.
    public string AcceptReturnMemento(string acceptedLine)
    {
        if (!HasGraceReturn || SaveManager.Instance == null
            || !SaveManager.Instance.AcknowledgeGraceReturn(profile, out GracePhotoOutcome outcome))
            return acceptedLine;
        previousVisit = SaveManager.Instance.MemoryFor(profile);
        NotebookHooks.GraceReturned(DisplayName, outcome);
        Feel(outcome == GracePhotoOutcome.Missed ? PortraitExpression.Worried : PortraitExpression.Happy);
        closingNote = GraceCameraEpisode.HandoffLine(outcome);
        string news = GraceCameraEpisode.ReturnNews(outcome);
        return string.IsNullOrWhiteSpace(news) ? acceptedLine : news;
    }

    /// <summary>A short line for the screen once the conversation closes (Grace's print), taken once; or "".</summary>
    public string TakeClosingNote()
    {
        string note = closingNote ?? "";
        closingNote = "";
        return note;
    }

    // ---------- things Ace can ask about (the dialogue pass) ----------

    /// <summary>
    /// What Ace can ask about on this visit, in the reply list's order (at most two): the regular's night
    /// thing on Grace's camera visit (the Night 1 slice), then their profile's topics. Walk-ins have none.
    /// </summary>
    public IReadOnlyList<TopicChoice> Topics()
    {
        if (topics != null) return topics;
        topics = new List<TopicChoice>();
        if (profile == null) return topics;
        NightThing thing = MentionableThing();
        if (thing != null && !string.IsNullOrWhiteSpace(thing.topic) && !string.IsNullOrWhiteSpace(thing.mention))
            topics.Add(new TopicChoice(thing.id, thing.topic, thing.mention, thing));
        foreach (ConversationTopic topic in profile.topics ?? System.Array.Empty<ConversationTopic>())
        {
            if (topics.Count >= MaxTopics) break;
            if (topic == null || string.IsNullOrWhiteSpace(topic.ask) || string.IsNullOrWhiteSpace(topic.answer)) continue;
            if (topic.visits == TopicVisits.FirstMeeting && HasMetBefore) continue;
            if (topic.visits == TopicVisits.Returning && !HasMetBefore) continue;
            topics.Add(new TopicChoice(topic.id, topic.ask, topic.answer, null));
        }
        return topics;
    }

    /// <summary>Ace asks about <paramref name="topic"/>: their answer, one line per beat. The notebook learns it once.</summary>
    public string Ask(TopicChoice topic)
    {
        if (topic == null) return "";
        topic.Asked = true;
        if (topic.Thing != null)
        {
            nightMentionHeard = true;
            if (NotebookHooks.HeardMention(DisplayName, topic.Thing)) topic.Taught = true;
        }
        // Being asked about themselves: they take it well, whatever their patience (the panel keeps it).
        Feel(PortraitExpression.Happy);
        return Format(topic.Answer);
    }

    /// <summary>
    /// A thing they meant to mention and Ace never asked about (the Night 1 slice: Grace's gnome), to say
    /// while they wait in a story's place; false once it's been said, asked about, or is already known.
    /// </summary>
    public bool PeekWaitingMention(out string line)
    {
        line = "";
        if (nightMentionHeard) return false;
        NightThing thing = MentionableThing();
        if (thing == null || string.IsNullOrWhiteSpace(thing.waitingMention)) return false;
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook != null && notebook.Knows(thing.id)) return false;
        line = Format(thing.waitingMention);
        return true;
    }

    /// <summary>They've said it (PeekWaitingMention): Ace notes it down, and they won't say it again this visit.</summary>
    public void MarkWaitingMentionSaid()
    {
        NightThing thing = MentionableThing();
        nightMentionHeard = true;
        if (thing != null) NotebookHooks.HeardMention(DisplayName, thing);
    }

    // Their night thing while it's still theirs to mention: only on Grace's camera visit (the Night 1
    // slice), and never once it's on Ace's shelf.
    private NightThing MentionableThing()
    {
        if (!isGraceCameraRequest || profile == null) return null;
        NightThing thing = NightThings.OwnedBy(profile.PersistentId);
        if (thing == null) return null;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        return night != null && night.HasTrophy(thing.id) ? null : thing;
    }

    /// <summary>
    /// The face for a line said outside the usual beats (the morning after a night: MorningFace). The
    /// conversation panel keeps it, even at low patience, until their next line (Say).
    /// </summary>
    public void Feel(PortraitExpression expression)
    {
        Expression = expression;
        feltAside = true;
    }

    public float PatienceMultiplier
    {
        get
        {
            float identityMultiplier =
                profile != null ? profile.patienceMultiplier :
                archetype != null ? archetype.patienceMultiplier : 1f;

            return identityMultiplier * RelationshipPatienceMultiplier;
        }
    }

    public float TipMultiplier
    {
        get
        {
            float identityMultiplier =
                profile != null ? profile.tipMultiplier :
                archetype != null ? archetype.tipMultiplier : 1f;

            return identityMultiplier * RelationshipTipMultiplier;
        }
    }

    // Mechanical trust stays modest; authored requests and story access are
    // the larger Loyal reward. Walk-ins never receive relationship modifiers.
    private float RelationshipPatienceMultiplier =>
        !IsRegular           ? 1f :
        Relationship <= -2   ? 0.90f :
        Relationship >= 5    ? 1.10f :
        Relationship >= 2    ? 1.05f :
                               1f;

    private float RelationshipTipMultiplier =>
        !IsRegular           ? 1f :
        Relationship <= -2   ? 0.75f :
        Relationship >= 5    ? 1.25f :
        Relationship >= 2    ? 1.15f :
                               1f;

    public WaitingSpot.SpotKind PreferredWaitKind =>
        profile != null ? profile.preferredWaitKind :
        archetype != null ? archetype.preferredWaitKind : WaitingSpot.SpotKind.Loiter;

    // How likely this person is to want a coffee WHILE waiting on a repair.
    // Zero when we know nothing about them, so an unconfigured archetype can't
    // silently flood the machine with orders.
    public float DrinkWishChance =>
        profile != null ? profile.drinkWishChance :
        archetype != null ? archetype.drinkWishChance : 0f;

    public string Say(Beat beat)
    {
        lastBeat = beat;
        feltAside = false;
        Expression = beat switch
        {
            Beat.Accepted or Beat.Completed => PortraitExpression.Happy,
            Beat.Declined => PortraitExpression.Worried,
            Beat.StormedOut => PortraitExpression.Impatient,
            Beat.Reassured => PortraitExpression.Surprised,
            _ => PortraitExpression.Neutral
        };

        // Her return visit asks for what she came in for first; the photo's news waits until Ace has
        // taken it (AcceptReturnMemento), so the request is short and one clear thing.
        if (beat == Beat.Intake && isGraceCameraRequest)
            return WithFocusCallback(GraceCameraEpisode.Intake, beat);
        // Her thanks when Ace takes the camera: the reveal, the episode's own line.
        if (beat == Beat.Accepted && isGraceCameraRequest)
            return GraceCameraEpisode.AcceptedLine;

        if (beat == Beat.Intake && profile != null && previousVisit != null)
        {
            CustomerReturnOutcome outcome = ReturnOutcome;
            if (outcome != CustomerReturnOutcome.FirstVisit && !CustomerReturnPolicy.AllowsWarmDialogue(outcome))
                Expression = PortraitExpression.Worried;
            // The memory callbacks bring a device in ("Today's patient is my {device}: {fault}"): a visit
            // for a drink skips them and orders it (below). Either way one line is picked.
            if (!drinkVisit)
            {
                string callback = PickValid(profile.returnMemoryLines?.For(outcome));
                if (!string.IsNullOrEmpty(callback)) return WithFocusCallback(Format(callback), beat);
            }
        }

        DialogueSet set = ResolveSet();
        if (set == null) return WithFocusCallback("", beat);

        string[] pool = beat switch
        {
            Beat.Intake     => drinkVisit ? DrinkOrder(set) : set.intake,
            Beat.Accepted   => set.accepted,
            Beat.Completed  => set.completed,
            Beat.Declined   => set.declined,
            Beat.Reassured  => set.reassured,
            Beat.StormedOut => set.stormedOut,
            Beat.OrderedDrink => set.orderedDrink,
            _ => null
        };

        // {device} and {fault} let one written line work across the whole
        // roster. Five personalities x seven beats is 35 lines that cover every
        // device-and-fault combination, instead of 35 per combination.
        //
        // {fault} matters more than it looks: it's how the player learns what
        // they're being asked to take on before they press E. A decline can't
        // be a real decision if every job is described identically.
        return WithFocusCallback(Format(PickValid(pool)), beat);
    }

    private string WithFocusCallback(string line, Beat beat)
    {
        // Preserve the honest grade/outcome callback; quiet is not forgiveness
        // for a failed repair, nor a replacement for the current intake request.
        if (beat != Beat.Intake || profile == null || !profile.storyteller
            || !RemembersFocusBoundary || string.IsNullOrWhiteSpace(profile.focusReturnLine)) return line;
        // Its own line on screen, after theirs (never a paragraph glued on).
        return string.IsNullOrWhiteSpace(line) ? Format(profile.focusReturnLine)
            : line + "\n" + Format(profile.focusReturnLine);
    }

    public string SayRepairCompleted(JobGrade grade)
    {
        string fallback = Say(Beat.Completed);
        if (grade == JobGrade.Passable) Expression = PortraitExpression.Worried;
        else if (grade == JobGrade.Rejected) Expression = PortraitExpression.Impatient;

        if (profile == null) return fallback;
        if (isGraceCameraRequest) return GraceCameraEpisode.CompletionLine(grade);
        if (grade == JobGrade.Passable)
            return Format(PickValid(profile.passableRepairLines, "It works, but it could use more care. I'll take it as it is."));
        if (grade == JobGrade.Rejected)
            return Format(PickValid(profile.rejectedRepairLines, "This still needs work. I'll take it back for now."));
        return fallback;
    }

    public string SayDrinkCompleted()
    {
        string fallback = Say(Beat.Completed);
        return profile != null ? Format(PickValid(profile.drinkCompletedLines, "Thank you for the drink.")) : fallback;
    }

    // Two readers ask two different questions of the same face.
    //
    // The TICKET RAIL asks "how is this person doing right now?" It is read
    // for the whole wait, so once patience is low the face turns impatient
    // whatever they last said - including after you took their job or they
    // ordered a drink. That is the warning the rail exists to give.
    public PortraitExpression ExpressionAt(float patienceFraction) =>
        (lastBeat == Beat.Intake || lastBeat == Beat.Accepted || lastBeat == Beat.OrderedDrink)
        && patienceFraction <= 0.25f ? PortraitExpression.Impatient : Expression;

    public Sprite PortraitAt(float patienceFraction) =>
        profile != null ? profile.PortraitFor(ExpressionAt(patienceFraction)) : null;

    // The CONVERSATION PANEL asks "how did they take what was just said?" It
    // is read the moment a line lands, so the response's face wins: someone
    // who has just said "thanks, I'll wait" is not shown scowling at you,
    // however low their patience. Only the intake line - spoken before you
    // have done anything - lets low patience show through.
    // A face set outside the beats (Feel: the morning after a night) is how they took what was just
    // said too, so it wins the same way.
    public PortraitExpression PanelExpressionAt(float patienceFraction) =>
        !feltAside && lastBeat == Beat.Intake && patienceFraction <= 0.25f ? PortraitExpression.Impatient : Expression;

    public Sprite PanelPortraitAt(float patienceFraction) =>
        profile != null ? profile.PortraitFor(PanelExpressionAt(patienceFraction)) : null;

    // {a drink} and {drink}: what they came in to order, lower case ("a latte", "an espresso").
    private string Format(string line) => (line ?? "")
        .Replace("{a drink}", WithArticle(drinkName)).Replace("{drink}", drinkName)
        .Replace("{device}", deviceName).Replace("{fault}", faultName);

    private static string WithArticle(string noun) =>
        string.IsNullOrEmpty(noun) ? "a drink" : ("aeiou".IndexOf(char.ToLowerInvariant(noun[0])) >= 0 ? "an " : "a ") + noun;

    // A visit for a drink opens by ordering it. A regular orders in their own words (their lines' drink
    // order: Grace's "A latte, please. No sugar."). A walk-in orders with their personality's drinkOrder
    // lines (the dialogue pass); a personality without any uses these.
    // PLACEHOLDER COPY: Mansoor rewrites it.
    private static readonly string[] DrinkOrderLines =
    {
        "Could I get {a drink}, please?",
        "Just {a drink} today, please.",
    };

    /// <summary>The shared placeholder drink orders (for the Dialogue rules check).</summary>
    public static IReadOnlyList<string> PlaceholderDrinkOrders => DrinkOrderLines;

    private string[] DrinkOrder(DialogueSet set) =>
        profile != null && HasAnyLine(set.orderedDrink) ? set.orderedDrink
        : profile == null && HasAnyLine(set.drinkOrder) ? set.drinkOrder
        : DrinkOrderLines;

    private static bool HasAnyLine(string[] pool)
    {
        if (pool == null) return false;
        foreach (string line in pool)
            if (!string.IsNullOrWhiteSpace(line)) return true;
        return false;
    }

    private static string PickValid(string[] pool, string fallback = "")
    {
        if (pool == null || pool.Length == 0) return fallback;
        int first = Random.Range(0, pool.Length);
        for (int i = 0; i < pool.Length; i++)
        {
            string line = pool[(first + i) % pool.Length];
            if (!string.IsNullOrWhiteSpace(line)) return line;
        }
        return fallback;
    }

    private DialogueSet ResolveSet()
    {
        if (profile != null)
        {
            bool warmAvailable = HasLines(profile.warmLines);
            bool latestVisitSupportsTrust = previousVisit == null || CustomerReturnPolicy.AllowsWarmDialogue(ReturnOutcome);
            if (HasMetBefore && Relationship >= 2 && latestVisitSupportsTrust && warmAvailable) return profile.warmLines;

            bool returnAvailable = HasLines(profile.returnLines);
            if (HasMetBefore && returnAvailable) return profile.returnLines;

            return profile.lines;
        }
        return archetype != null ? archetype.lines : null;
    }

    private static bool HasLines(DialogueSet set)
    {
        return set != null && set.intake != null && set.intake.Length > 0;
    }
}
