using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE NIGHT LINES (claude/foundation-pass-build-plan.md §5.7)
//
// Every bark the night's cast says, in one asset his sister can edit in the Inspector:
// Assets/Data/Resources/Night lines.asset (Fixit Fidget > Night > Barks 1 makes it and adds what's
// missing, never overwriting a line that's there).
//
//   * Speakers: an id the code uses ("lodger", "ace", "grace", "officer", "neighbour"), a name for the
//     Inspector, the colour of the bar beside their lines, and a sound cue (empty: "bark.<id>").
//   * Lines: a stable id ("lodger.night0.01"), the speaker, the situation it belongs to ("night0.deal",
//     "lodger.cold", "grace.notice"), and the text. Lines of one speaker and one situation make a pool:
//     the world says one of them at a time (BarkRules: every line once before any repeats).
//   * Scenes: a few lines in order, between speakers (Night 0's deal), and whether Ace is held still
//     while it plays.
// The writing rule is 60 characters a line (two lines on screen); the Bark rules check warns above it.
// All of it is placeholder copy until Mansoor and his sister write the cast.
// ---------------------------------------------------------------------------
[CreateAssetMenu(fileName = ResourceName, menuName = "Fixit Fidget/Night lines")]
public sealed class NightLines : ScriptableObject
{
    public const string ResourceName = "Night lines";

    [Serializable]
    public sealed class Speaker
    {
        [Tooltip("The id the game uses: lodger, ace, grace, officer, neighbour.")]
        public string id = "";
        [Tooltip("For the Inspector and the checks; never shown on screen.")]
        public string name = "";
        [Tooltip("The thin bar beside their lines.")]
        public Color colour = Color.white;
        [Tooltip("The sound cue played with each line; empty means bark.<id> (silent until a file is picked).")]
        public string sound = "";
    }

    [Serializable]
    public sealed class Line
    {
        [Tooltip("Stable: the game and the scenes refer to it. Rename only with the scenes that use it.")]
        public string id = "";
        public string speaker = "";
        [Tooltip("Lines of one speaker and one situation make a pool the world picks from.")]
        public string situation = "";
        [TextArea(1, 3)] public string text = "";
    }

    [Serializable]
    public sealed class Scene
    {
        public string id = "";
        [Tooltip("Hold Ace still while it plays (the camera stays free). E moves it on sooner.")]
        public bool holdAce = true;
        [Tooltip("Line ids, in order.")]
        public string[] lines = Array.Empty<string>();
    }

    public Speaker[] speakers = Array.Empty<Speaker>();
    public Line[] lines = Array.Empty<Line>();
    public Scene[] scenes = Array.Empty<Scene>();

    static NightLines loaded;
    static bool searched;

    /// <summary>The asset in Resources (null if it hasn't been made: Fixit Fidget > Night > Barks 1).</summary>
    public static NightLines Current
    {
        get
        {
            if (loaded == null && !searched)
            {
                searched = true;
                loaded = Resources.Load<NightLines>(ResourceName);
            }
            return loaded;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ForgetLoaded()
    {
        loaded = null;
        searched = false;
    }

    readonly Dictionary<string, Speaker> speakerById = new Dictionary<string, Speaker>(StringComparer.Ordinal);
    readonly Dictionary<string, Line> lineById = new Dictionary<string, Line>(StringComparer.Ordinal);
    readonly Dictionary<string, Scene> sceneById = new Dictionary<string, Scene>(StringComparer.Ordinal);
    readonly Dictionary<string, List<Line>> pools = new Dictionary<string, List<Line>>(StringComparer.Ordinal);
    bool indexed;

    void OnEnable() => indexed = false;
    void OnValidate() => indexed = false;

    void Index()
    {
        if (indexed) return;
        indexed = true;
        speakerById.Clear(); lineById.Clear(); sceneById.Clear(); pools.Clear();
        foreach (Speaker s in speakers) if (s != null && !string.IsNullOrEmpty(s.id)) speakerById[s.id] = s;
        foreach (Line l in lines)
        {
            if (l == null || string.IsNullOrEmpty(l.id)) continue;
            lineById[l.id] = l;
            if (string.IsNullOrWhiteSpace(l.text)) continue;
            string key = PoolKey(l.speaker, l.situation);
            if (!pools.TryGetValue(key, out List<Line> pool)) pools[key] = pool = new List<Line>();
            pool.Add(l);
        }
        foreach (Scene s in scenes) if (s != null && !string.IsNullOrEmpty(s.id)) sceneById[s.id] = s;
    }

    public static string PoolKey(string speaker, string situation) => (speaker ?? "") + "/" + (situation ?? "");

    public Speaker FindSpeaker(string id)
    {
        Index();
        return id != null && speakerById.TryGetValue(id, out Speaker s) ? s : null;
    }

    public Line FindLine(string id)
    {
        Index();
        return id != null && lineById.TryGetValue(id, out Line l) ? l : null;
    }

    public Scene FindScene(string id)
    {
        Index();
        return id != null && sceneById.TryGetValue(id, out Scene s) ? s : null;
    }

    /// <summary>The lines one speaker has for one situation (never null; empty if none).</summary>
    public IReadOnlyList<Line> Pool(string speaker, string situation)
    {
        Index();
        return pools.TryGetValue(PoolKey(speaker, situation), out List<Line> pool) ? pool : (IReadOnlyList<Line>)Array.Empty<Line>();
    }
}
