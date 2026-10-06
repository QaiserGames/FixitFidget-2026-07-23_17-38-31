using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// BARKS: SHORT LINES NEAR WHOEVER SAYS THEM (claude/foundation-pass-build-plan.md §5, agreed 5 Oct 2026)
//
// The night's cast speaks through these (conversations don't open once the day is over): the man at the
// bins, Ace muttering, a neighbour at a window, and in chunk C Grace's "Hm?". By day the man's morning
// visit uses them too.
//
// What a line looks like (option A of the mock-up, Mansoor's pick):
//   * pinned to the speaker's head on screen, the same size at any zoom and in first person (30 px at
//     1080p, the subtitles' size), on a dark rounded band with a thin bar in the speaker's colour; no
//     name: the bar and the position say who;
//   * heard through walls (it's a sound, not a sign); off screen, it clamps to the screen's edge with a
//     small arrow pointing to the speaker;
//   * Ace's own lines sit over Ace's head overhead, and at the bottom of the screen in first person;
//   * hidden while a conversation, the recap, the notebook or a station's close-up has the screen, and
//     the clock stops while hidden (nothing is missed).
// Who says what, when: BarkRules (reading time, ambient throttle, pools) and the Night lines asset.
//
//   Barks.Say(speaker, id, text)        a line now (a reaction, a one-off); heard within Hearing of Ace
//   Barks.SayFrom(speaker, id, situation) an ambient line from the speaker's pool, throttled
//   Barks.SayAce(text)                  Ace muttering
//   Barks.Play(sceneId, who)            a scene from the asset: lines in order; Ace held if the scene says
//   Barks.PlayLines(lines, hold, who)   the same, from code
//
// Ace answers (6 Oct 2026, claude/the-man-at-the-bins-story.md §4): where a held scene from the asset has a choice
// after one of its lines, the scene waits once that line is up and Ace's two replies come up as chips where Ace's
// own line would be (over Ace's head; at the bottom in first person): 1 or 2 on the keyboard, X or Y on a pad
// (the pad's labels follow the pad). The reply picked is said by Ace, then the line said back, and the scene goes
// on; the caller hears which (to nudge the man's warmth). E never picks one: a press meant to move a line on
// can't answer for Ace.
//
// Built in code on a screen canvas of its own (nothing in the scene changes). One object per Play session.
// The pinning (head point, edge clamp, arrow) is what chunk C's mark ("?" over Grace) will use too.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(900)]
public sealed class Barks : MonoBehaviour
{
    public const string AceId = "ace";
    const int ViewCount = 4;

    [Header("How it looks (reference pixels: the HUD's 1920 x 1080)")]
    public float fontSize = 30f;
    [Tooltip("The widest a band can be; longer lines wrap to a second line, then end with an ellipsis.")]
    public float maxWidth = 560f;
    public Color bandColour = new Color(.055f, .055f, .07f, .76f);
    public Color textColour = new Color(.96f, .95f, .92f, 1f);
    [Tooltip("Lines in a scene that someone else has answered stay up at this strength until they go.")]
    [Range(.2f, 1f)] public float answeredAlpha = .55f;
    [Header("Where it sits")]
    [Tooltip("Above the head bone (metres).")]
    public float headClearance = .32f;
    [Tooltip("Kept clear at the screen's edges, for the clock at the top and the controls line at the bottom (reference pixels).")]
    public float edgeMargin = 18f, topReserve = 92f, bottomReserve = 120f;
    [Tooltip("Ace's own lines in first person sit here (reference pixels from the bottom; above the night's note).")]
    public float selfLineY = 330f;
    [Header("Timing")]
    public float fadeIn = .15f, fadeOut = .3f;
    [Tooltip("A scene's line can be moved on with E once it has been up this long (seconds).")]
    public float sceneMinLine = .45f;
    [Tooltip("A short beat after each scene line's reading time before the next one.")]
    public float sceneBeat = .25f;
    [Header("Who hears it")]
    [Tooltip("Ambient lines and reactions farther than this from Ace aren't heard (metres). A scene's lines always are.")]
    public float hearing = 12f;

    // ---- the parts of one line on screen ----
    sealed class View
    {
        public RectTransform root, bandRect, accentRect, textRect, tailRect, arrowRect;
        public CanvasGroup group;
        public Image band, accent, tail, arrow;
        public TextMeshProUGUI text;
        public bool active, self, scene, answered, clamped, slotted, tailShown;
        public Transform speaker;
        public string speakerId = "", line = "";
        public float shownAt, holdUntil, alpha;
        public Vector2 size, anchor;
        public Rect placed;
    }

    sealed class ScenePlay
    {
        public readonly List<string> speakers = new List<string>();
        public readonly List<string> texts = new List<string>();
        public readonly List<string> ids = new List<string>();   // the lines' ids ("" for a line made in code)
        public NightLines.Scene from;                             // the asset's scene (for its choices), or null
        public bool hold;
        public Func<string, Transform> who;
        public Action<int, string> onLine;
        public Action<string> onLineId;
        public Action<NightLines.Choice, int> onReply;
        public Action onDone;
        public int index = -1;
        public float startedAt, lineStarted, lineEnds;
        public NightLines.Choice choosing;                        // waiting for Ace's reply to the line that's up
        public float choiceFrom;                                  // when the replies come up (seconds of barks)
    }

    // Ace's two replies while a scene waits for one (the chips).
    sealed class Chip
    {
        public RectTransform root;
        public Image band, accent;
        public TextMeshProUGUI text;
        public Vector2 size;
    }

    public static Barks Instance { get; private set; }
    /// <summary>The rules this session's barks follow (the dials can be changed live).</summary>
    public BarkRules Rules { get; } = new BarkRules();

    Canvas canvas;
    RectTransform canvasRect;
    readonly View[] views = new View[ViewCount];
    readonly View[] order = new View[ViewCount + 1];   // the lines up, and the replies' chips while Ace answers
    readonly Chip[] chips = new Chip[2];
    View chipStack;                                     // where the chips sit, placed like one of Ace's lines
    CanvasGroup chipsGroup;
    bool chipsUp;
    Sprite rounded, triangle;
    Material textMaterial;
    TMP_FontAsset font;
    Camera cam;
    CafeViewMode viewMode;
    Transform ace;
    ScenePlay scene;
    float clock;            // seconds of barks: stops while they're hidden
    bool hidden, wasNight;
    readonly Dictionary<Transform, Transform> heads = new Dictionary<Transform, Transform>();
    readonly Dictionary<Transform, float> headSearched = new Dictionary<Transform, float>();
    readonly Dictionary<Transform, Renderer[]> bodies = new Dictionary<Transform, Renderer[]>();

    // ================================================================== the API

    /// <summary>The barks of this Play session (made on first use; nothing outside Play).</summary>
    public static Barks Ensure()
    {
        if (Instance != null) return Instance;
        if (!Application.isPlaying) return null;
        var go = new GameObject("Barks (this Play session)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        return go.AddComponent<Barks>();
    }

    /// <summary>
    /// A line now, from <paramref name="speaker"/> (a reaction, a one-off): not throttled, but only heard within
    /// Hearing of Ace. <paramref name="seconds"/> 0 means its reading time. False if nobody heard it.
    /// </summary>
    public static bool Say(Transform speaker, string speakerId, string text, float seconds = 0f)
    {
        Barks b = Ensure();
        return b != null && b.Show(speaker, speakerId, text, seconds, scene: false, self: false, needHearing: true);
    }

    /// <summary>An ambient line from the speaker's pool for <paramref name="situation"/> (Night lines), throttled.</summary>
    public static bool SayFrom(Transform speaker, string speakerId, string situation)
    {
        Barks b = Ensure();
        NightLines lines = NightLines.Current;
        if (b == null || lines == null || speaker == null) return false;
        IReadOnlyList<NightLines.Line> pool = lines.Pool(speakerId, situation);
        if (pool.Count == 0 || !b.MayAmbient(speaker, speakerId)) return false;
        int i = b.Rules.Next(NightLines.PoolKey(speakerId, situation), pool.Count);
        return b.Ambient(speaker, speakerId, pool[i].text);
    }

    /// <summary>An ambient line with this text, throttled like a pool's (for code-made lines and the checks).</summary>
    public static bool SayAmbient(Transform speaker, string speakerId, string text)
    {
        Barks b = Ensure();
        return b != null && speaker != null && b.MayAmbient(speaker, speakerId) && b.Ambient(speaker, speakerId, text);
    }

    /// <summary>Ace's own line (Ace muttering): over Ace's head overhead, at the bottom of the screen in first person.</summary>
    public static bool SayAce(string text, float seconds = 0f)
    {
        Barks b = Ensure();
        return b != null && b.Show(b.Ace, AceId, text, seconds, scene: false, self: true, needHearing: false);
    }

    /// <summary>
    /// Plays a scene from the Night lines asset: its lines in order, each for its reading time (E moves on
    /// sooner), Ace held still if the scene says so. <paramref name="who"/> gives each speaker's transform (Ace
    /// is found by itself). <paramref name="onLineId"/> hears each line's id as it comes up (what happens on a
    /// line: the notebook changing hands), and <paramref name="onReply"/> which reply Ace picked at a choice (0 or
    /// 1). False if another scene is playing or the scene isn't there.
    /// </summary>
    public static bool Play(string sceneId, Func<string, Transform> who, Action<int, string> onLine = null, Action onDone = null,
                            Action<string> onLineId = null, Action<NightLines.Choice, int> onReply = null)
    {
        Barks b = Ensure();
        NightLines lines = NightLines.Current;
        NightLines.Scene s = lines != null ? lines.FindScene(sceneId) : null;
        if (b == null || s == null || b.scene != null) return false;
        var play = new ScenePlay { hold = s.holdAce, who = who, onLine = onLine, onDone = onDone, onLineId = onLineId, onReply = onReply, from = s };
        foreach (string id in s.lines)
        {
            NightLines.Line line = lines.FindLine(id);
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
            play.speakers.Add(line.speaker);
            play.texts.Add(line.text);
            play.ids.Add(line.id);
        }
        return b.Begin(play);
    }

    /// <summary>A scene from code: <paramref name="speakerIds"/> and <paramref name="texts"/> pair up, in order.</summary>
    public static bool PlayLines(IReadOnlyList<string> speakerIds, IReadOnlyList<string> texts, bool holdAce, Func<string, Transform> who,
                                 Action<int, string> onLine = null, Action onDone = null)
    {
        Barks b = Ensure();
        if (b == null || b.scene != null || speakerIds == null || texts == null) return false;
        var play = new ScenePlay { hold = holdAce, who = who, onLine = onLine, onDone = onDone };
        for (int i = 0; i < Mathf.Min(speakerIds.Count, texts.Count); i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i])) continue;
            play.speakers.Add(speakerIds[i]);
            play.texts.Add(texts[i]);
            play.ids.Add("");
        }
        return b.Begin(play);
    }

    public static bool ScenePlaying => Instance != null && Instance.scene != null;
    /// <summary>The scene waits for Ace's reply (its chips are up, or about to be).</summary>
    public static bool Choosing => Instance != null && Instance.scene != null && Instance.scene.choosing != null;
    /// <summary>The replies' chips are on screen and can be picked.</summary>
    public static bool ChipsUp => Instance != null && Instance.chipsUp;
    /// <summary>The text of reply 0 or 1 while Ace is choosing (for the checks), or "".</summary>
    public static string ReplyText(int reply)
    {
        NightLines.Choice c = Instance != null && Instance.scene != null ? Instance.scene.choosing : null;
        NightLines.Line line = c != null && NightLines.Current != null ? NightLines.Current.FindLine(reply == 0 ? c.first.line : c.second.line) : null;
        return line != null ? line.text : "";
    }
    /// <summary>Pick reply 0 or 1 now, as 1 or 2 would (for the checks). False if Ace isn't choosing.</summary>
    public static bool Choose(int reply)
    {
        if (!Choosing || reply < 0 || reply > 1) return false;
        Instance.Pick(reply);
        return true;
    }
    /// <summary>
    /// The frame a scene that held Ace ended: the press that moved its last line on was the scene's, so nothing else
    /// takes it that frame (PlayerInteractor: "Call it a night" never answers the deal's last E).
    /// </summary>
    public static int SceneEndedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSceneEnd() => SceneEndedFrame = -1;

    /// <summary>Where the replies' chips are on screen, in pixels (for the checks); empty while they're down.</summary>
    public static Rect ChipsOnScreen
    {
        get
        {
            if (Instance == null || !Instance.chipsUp || Instance.chipStack == null || Instance.chipStack.root == null) return Rect.zero;
            var corners = new Vector3[4];
            Instance.chipStack.root.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }

    /// <summary>The line the scene is on (0-based; -1 with no scene).</summary>
    public static int SceneLine => Instance != null && Instance.scene != null ? Instance.scene.index : -1;
    /// <summary>Move the scene on, as E does (for the checks). Not while Ace is choosing: only a reply moves that on.</summary>
    public static void Advance() { if (Instance != null && Instance.scene != null && Instance.scene.choosing == null) Instance.NextLine(); }
    public static void StopScene() { if (Instance != null) Instance.EndScene(false); }
    /// <summary>Every line off the screen, the scene stopped.</summary>
    public static void ClearAll() { if (Instance != null) Instance.Clear(); }
    /// <summary>How many lines are up (fading ones included).</summary>
    public static int Showing => Instance != null ? Instance.CountShowing() : 0;
    /// <summary>Barks are hidden right now (a conversation, the recap, the notebook, a close-up, a pause).</summary>
    public static bool Hidden => Instance != null && Instance.hidden;

    /// <summary>For the checks: where the line of <paramref name="speaker"/> is, in screen pixels.</summary>
    public static bool TryGetShown(Transform speaker, out Shown shown)
    {
        shown = default;
        if (Instance == null) return false;
        foreach (View v in Instance.views)
        {
            if (!v.active || v.speaker != speaker) continue;
            float s = Instance.canvas != null && Instance.canvas.scaleFactor > 0f ? Instance.canvas.scaleFactor : 1f;
            shown = new Shown
            {
                text = v.line, alpha = v.group.alpha, clamped = v.clamped, slotted = v.slotted, tail = v.tailShown,
                arrow = v.arrowRect.gameObject.activeSelf,
                anchor = v.anchor * s,
                band = new Rect(v.placed.x * s, v.placed.y * s, v.placed.width * s, v.placed.height * s),
                tip = new Vector2(v.placed.center.x, v.placed.y - (v.tailShown ? TailHeight : 0f)) * s,
            };
            return true;
        }
        return false;
    }

    public struct Shown
    {
        public string text;
        public float alpha;
        public bool clamped, slotted, tail, arrow;
        /// <summary>The speaker's head point projected (screen pixels), or the slot for Ace in first person.</summary>
        public Vector2 anchor;
        /// <summary>The band (screen pixels, from the bottom left) and the tail's tip under it.</summary>
        public Rect band;
        public Vector2 tip;
    }

    /// <summary>The head point a line is pinned to (for the checks and chunk C's mark).</summary>
    public static Vector3 HeadPointOf(Transform speaker) => Ensure() != null ? Instance.HeadPoint(speaker) : speaker != null ? speaker.position : Vector3.zero;

    // ================================================================== the life of a line

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
    }

    void OnEnable() => Canvas.willRenderCanvases += Place;

    void OnDisable()
    {
        Canvas.willRenderCanvases -= Place;
        if (scene != null && scene.hold) PlayerMovement.Release(scene);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (rounded != null) { Destroy(rounded.texture); Destroy(rounded); }
        if (triangle != null) { Destroy(triangle.texture); Destroy(triangle); }
        if (textMaterial != null) Destroy(textMaterial);
    }

    Transform Ace
    {
        get
        {
            if (ace == null)
            {
                PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
                ace = player != null ? player.transform : null;
            }
            return ace;
        }
    }

    bool MayAmbient(Transform speaker, string speakerId) => Heard(speaker) && Rules.MayAmbient(speakerId, clock, scene != null);

    bool Ambient(Transform speaker, string speakerId, string text)
    {
        if (!Show(speaker, speakerId, text, 0f, scene: false, self: false, needHearing: true)) return false;
        Rules.SaidAmbient(speakerId, clock);
        return true;
    }

    bool Heard(Transform speaker)
    {
        Transform a = Ace;
        if (speaker == null) return false;
        if (a == null || speaker == a) return true;
        Vector3 d = speaker.position - a.position;
        return d.sqrMagnitude <= hearing * hearing;
    }

    bool Show(Transform speaker, string speakerId, string text, float seconds, bool scene, bool self, bool needHearing)
    {
        if (string.IsNullOrWhiteSpace(text) || speaker == null && !self) return false;
        if (needHearing && !Heard(speaker)) return false;
        if (self && speaker == null) speaker = transform;   // no Ace in this scene: the line still shows (bottom slot)
        // One line per speaker: their new line takes their old one's place.
        View v = null;
        foreach (View x in views) if (x.active && x.speaker == speaker) { v = x; break; }
        if (v == null) foreach (View x in views) if (!x.active) { v = x; break; }
        if (v == null)
        {
            // All in use: the oldest line that isn't part of a scene goes (or, failing that, the oldest).
            foreach (View x in views) if (!x.scene && (v == null || x.shownAt < v.shownAt)) v = x;
            if (v == null) foreach (View x in views) if (v == null || x.shownAt < v.shownAt) v = x;
        }
        v.active = true;
        v.self = self;
        v.scene = scene;
        v.answered = false;
        v.speaker = speaker;
        v.speakerId = speakerId ?? "";
        v.line = text;
        v.shownAt = clock;
        v.holdUntil = clock + (seconds > 0f ? seconds : Rules.ReadingSeconds(text));
        if (v.alpha <= .01f) v.alpha = 0f;
        v.root.gameObject.SetActive(true);
        Dress(v);
        Sfx.Play(Sound(speakerId), HeadPoint(speaker));
        return true;
    }

    string Sound(string speakerId)
    {
        NightLines lines = NightLines.Current;
        NightLines.Speaker s = lines != null ? lines.FindSpeaker(speakerId) : null;
        return s != null && !string.IsNullOrEmpty(s.sound) ? s.sound : "bark." + speakerId;
    }

    Color ColourOf(string speakerId)
    {
        NightLines lines = NightLines.Current;
        NightLines.Speaker s = lines != null ? lines.FindSpeaker(speakerId) : null;
        if (s != null) return s.colour;
        return speakerId == AceId ? new Color(.31f, .70f, .53f) : new Color(.75f, .75f, .78f);
    }

    float viewSearched = -10f;

    void Update()
    {
        if (viewMode == null && Time.unscaledTime - viewSearched > 2f)
        {
            viewSearched = Time.unscaledTime;
            viewMode = FindAnyObjectByType<CafeViewMode>();
        }
        // The night ended (or began): what was said in it stays in it.
        bool night = NightWalk.Instance != null && NightWalk.Instance.Active;
        if (night != wasNight) { wasNight = night; if (scene == null) Clear(); }
        hidden = ShouldHide();
        if (canvas != null && canvas.enabled == hidden) canvas.enabled = !hidden;
        if (hidden) return;
        float dt = Time.unscaledDeltaTime;
        clock += dt;
        StepScene();
        foreach (View v in views)
        {
            if (!v.active) continue;
            float target = clock < v.holdUntil ? (v.answered ? answeredAlpha : 1f) : 0f;
            float rate = 1f / Mathf.Max(.01f, target > v.alpha ? fadeIn : fadeOut);
            v.alpha = Mathf.MoveTowards(v.alpha, target, rate * dt);
            v.group.alpha = v.alpha;
            if (clock >= v.holdUntil && v.alpha <= 0f) Retire(v);
        }
        if (chipsUp && chipsGroup.alpha < 1f) chipsGroup.alpha = Mathf.MoveTowards(chipsGroup.alpha, 1f, dt / Mathf.Max(.01f, fadeIn));
    }

    bool ShouldHide()
    {
        if (Time.timeScale <= 0f) return true;
        if (ConversationController.AnyOpen) return true;
        if (DayClock.Instance != null && DayClock.Instance.RecapOwnsInput) return true;
        NightWalk night = NightWalk.Instance;
        if (night != null && night.Active && night.NotebookPage != null && night.NotebookPage.Open) return true;
        // A station's close-up (the bench, the counter, an item): neither the overhead view nor walking first person.
        if (viewMode != null && viewMode.isActiveAndEnabled && !viewMode.OverheadShown && !viewMode.WalkingFirstPerson) return true;
        return false;
    }

    void Retire(View v)
    {
        v.active = false;
        v.speaker = null;
        v.line = "";
        v.alpha = 0f;
        v.group.alpha = 0f;
        v.root.gameObject.SetActive(false);
    }

    void Clear()
    {
        EndScene(false);
        foreach (View v in views) if (v.active) Retire(v);
    }

    int CountShowing()
    {
        int n = 0;
        foreach (View v in views) if (v.active) n++;
        return n;
    }

    // ================================================================== scenes

    bool Begin(ScenePlay play)
    {
        if (play.texts.Count == 0) return false;
        scene = play;
        play.startedAt = clock;
        if (play.hold) PlayerMovement.Hold(play);
        NextLine();
        return true;
    }

    void StepScene()
    {
        if (scene == null) return;
        if (scene.choosing != null) { StepChoice(); return; }
        // E moves a scene on only while it holds Ace; otherwise E is Ace's (the scene keeps its reading pace).
        bool asked = scene.hold && clock - scene.startedAt >= .3f && clock - scene.lineStarted >= sceneMinLine && MoveOnPressed();
        if (asked || clock >= scene.lineEnds) NextLine();
    }

    static bool MoveOnPressed()
    {
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        return Application.isFocused && (keys != null && (keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame
                                                         || keys.numpadEnterKey.wasPressedThisFrame)
                                         || mouse != null && mouse.leftButton.wasPressedThisFrame)
               || PadInput.Pressed(PadButton.South);
    }

    // Waiting for Ace's reply: the line being answered stays up; a moment after it came up (or at once, with E) the
    // two replies come up, and a moment after that 1 / 2 (X / Y) picks one.
    void StepChoice()
    {
        ScenePlay play = scene;
        foreach (View v in views)
            if (v.active && v.scene && !v.answered && v.holdUntil < clock + .5f) v.holdUntil = clock + .5f;
        if (!chipsUp)
        {
            if (clock < play.choiceFrom && !(clock - play.lineStarted >= sceneMinLine && MoveOnPressed())) return;
            ShowChips(play.choosing);
            play.choiceFrom = clock;
            return;
        }
        if (clock - play.choiceFrom < .2f || !Application.isFocused) return;
        Keyboard keys = Keyboard.current;
        int pick = -1;
        if (keys != null && (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) || PadInput.Pressed(PadButton.West)) pick = 0;
        else if (keys != null && (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) || PadInput.Pressed(PadButton.North)) pick = 1;
        if (pick >= 0) Pick(pick);
    }

    // Ace says the reply picked, then the line said back (if any), and the scene goes on from there.
    void Pick(int reply)
    {
        ScenePlay play = scene;
        NightLines.Choice c = play != null ? play.choosing : null;
        if (c == null) return;
        play.choosing = null;
        HideChips();
        NightLines lines = NightLines.Current;
        NightLines.Reply r = reply == 0 ? c.first : c.second;
        int at = play.index + 1;
        NightLines.Line said = lines != null && r != null ? lines.FindLine(r.line) : null;
        if (said != null && !string.IsNullOrWhiteSpace(said.text)) Insert(play, at++, AceId, said.text, said.id);
        NightLines.Line back = lines != null && r != null ? lines.FindLine(r.answer) : null;
        if (back != null && !string.IsNullOrWhiteSpace(back.text)) Insert(play, at, back.speaker, back.text, back.id);
        Sfx.Play2D("bark.choose");
        play.onReply?.Invoke(c, reply);
        NextLine();
    }

    static void Insert(ScenePlay play, int at, string speaker, string text, string id)
    {
        play.speakers.Insert(at, speaker);
        play.texts.Insert(at, text);
        play.ids.Insert(at, id ?? "");
    }

    // Both replies are lines that exist and say something: otherwise the scene doesn't stop for them.
    static bool CanAnswer(NightLines.Choice c)
    {
        NightLines lines = NightLines.Current;
        if (c == null || lines == null || c.first == null || c.second == null) return false;
        NightLines.Line a = lines.FindLine(c.first.line), b = lines.FindLine(c.second.line);
        return a != null && b != null && !string.IsNullOrWhiteSpace(a.text) && !string.IsNullOrWhiteSpace(b.text);
    }

    void NextLine()
    {
        ScenePlay play = scene;
        if (play == null) return;
        play.index++;
        if (play.index >= play.texts.Count) { EndScene(true); return; }
        string speakerId = play.speakers[play.index], text = play.texts[play.index];
        Transform speaker = play.who != null ? play.who(speakerId) : null;
        if (speaker == null && speakerId == AceId) speaker = Ace;
        // The lines already up in this scene stay, a little faded, until they've had their time.
        foreach (View v in views) if (v.active && v.scene) v.answered = true;
        float reading = Rules.ReadingSeconds(text);
        bool self = speakerId == AceId;
        Show(speaker, speakerId, text, reading + sceneBeat + 2f, scene: true, self: self, needHearing: false);
        play.lineStarted = clock;
        play.lineEnds = clock + reading + sceneBeat;
        string id = play.ids[play.index];
        // Does Ace answer this one? Then the scene waits for the reply (held scenes from the asset only).
        NightLines.Choice choice = play.hold && play.from != null ? play.from.ChoiceAfter(id) : null;
        if (CanAnswer(choice))
        {
            play.choosing = choice;
            play.choiceFrom = clock + Mathf.Min(reading, .9f);
        }
        play.onLine?.Invoke(play.index, text);
        if (id.Length > 0) play.onLineId?.Invoke(id);
    }

    void EndScene(bool finished)
    {
        ScenePlay play = scene;
        if (play == null) return;
        scene = null;
        play.choosing = null;
        HideChips();
        if (play.hold)
        {
            PlayerMovement.Release(play);
            SceneEndedFrame = Time.frameCount;
        }
        // The scene's lines fade now rather than linger.
        foreach (View v in views) if (v.active && v.scene) v.holdUntil = Mathf.Min(v.holdUntil, clock + .6f);
        if (finished) play.onDone?.Invoke();
    }

    // ================================================================== where a line sits

    const float TailHeight = 10f, TailGap = 6f, PadLeft = 25f, PadRight = 18f, PadY = 7f, ChipGap = 14f;

    // Runs just before the canvases are drawn, after every camera has moved this frame.
    void Place()
    {
        if (canvas == null || hidden) return;
        int n = 0;
        foreach (View v in views) if (v.active) order[n++] = v;
        if (n == 0) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return;
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 size = canvasRect.rect.size;
        var safe = new Rect(edgeMargin, bottomReserve, size.x - 2f * edgeMargin, size.y - bottomReserve - topReserve);
        bool firstPerson = viewMode != null && viewMode.WalkingFirstPerson;

        for (int i = 0; i < n; i++)
        {
            View v = order[i];
            v.slotted = v.self && (firstPerson || v.speaker == transform);
            bool behind = false;
            Vector2 anchor;
            if (v.slotted) anchor = new Vector2(size.x * .5f, selfLineY - TailGap - TailHeight);
            else
            {
                Vector3 p = cam.WorldToScreenPoint(HeadPoint(v.speaker));
                if (p.z < 0f)
                {
                    // Behind the camera: mirror it, and push it off the bottom so it clamps there with its arrow.
                    behind = true;
                    p.x = Screen.width - p.x;
                    p.y = -Screen.height;
                }
                anchor = new Vector2(p.x / scale, p.y / scale);
            }
            v.anchor = anchor;
            var band = new Rect(anchor.x - v.size.x * .5f, anchor.y + TailGap + TailHeight, v.size.x, v.size.y);
            bool inside = !behind && safe.Contains(anchor) && band.xMin >= safe.xMin && band.xMax <= safe.xMax && band.yMax <= safe.yMax;
            v.clamped = !v.slotted && !inside;
            if (!inside) band = Inside(band, safe);
            v.placed = band;
        }

        // Lines that would overlap stack upward, the lower one keeping its place.
        for (int i = 1; i < n; i++)
        {
            View v = order[i];
            int j = i - 1;
            while (j >= 0 && order[j].placed.y > v.placed.y) { order[j + 1] = order[j]; j--; }
            order[j + 1] = v;
        }
        for (int i = 1; i < n; i++)
        {
            View v = order[i];
            for (int j = 0; j < i; j++)
            {
                Rect other = order[j].placed;
                if (v.placed.Overlaps(other)) v.placed.y = other.yMax + 6f;
            }
        }

        // Ace's replies: beside the line they answer (to its right, or its left near the screen's edge), level with
        // its top. Never over Ace or the speaker: from the street's camera the two stand one behind the other.
        if (chipsUp)
        {
            View asking = null;
            for (int i = 0; i < n; i++)
                if (order[i].scene && !order[i].answered && (asking == null || order[i].placed.y > asking.placed.y)) asking = order[i];
            Vector2 chips = chipStack.size;
            Rect at;
            if (asking != null)
            {
                Rect line = asking.placed;
                float x = line.xMax + ChipGap;
                if (x + chips.x > safe.xMax) x = line.xMin - ChipGap - chips.x;
                at = Inside(new Rect(x, line.yMax - chips.y, chips.x, chips.y), safe);
            }
            else at = Inside(new Rect(size.x * .5f - chips.x * .5f, selfLineY, chips.x, chips.y), safe);
            chipStack.root.anchoredPosition = new Vector2(at.center.x, at.y);
        }

        for (int i = 0; i < n; i++)
        {
            View v = order[i];
            Rect band = v.placed;
            // The tail shows when the band still sits right over its speaker's head.
            v.tailShown = !v.clamped && !v.slotted && Mathf.Abs(band.y - (v.anchor.y + TailGap + TailHeight)) < .5f
                          && Mathf.Abs(band.center.x - v.anchor.x) < .5f;
            v.root.anchoredPosition = new Vector2(band.center.x, band.y);
            v.tailRect.gameObject.SetActive(v.tailShown);
            v.arrowRect.gameObject.SetActive(v.clamped);
            if (v.clamped)
            {
                Vector2 centre = band.center, to = v.anchor - centre;
                if (to.sqrMagnitude < 1f) to = Vector2.down;
                to.Normalize();
                // Out from the band's edge toward the speaker.
                float half = band.width * .5f, halfH = band.height * .5f;
                float tx = Mathf.Abs(to.x) > 1e-4f ? half / Mathf.Abs(to.x) : float.MaxValue;
                float ty = Mathf.Abs(to.y) > 1e-4f ? halfH / Mathf.Abs(to.y) : float.MaxValue;
                Vector2 edge = to * Mathf.Min(tx, ty);
                v.arrowRect.anchoredPosition = new Vector2(edge.x, halfH + edge.y) + to * 10f;
                v.arrowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            }
        }
    }

    static Rect Inside(Rect band, Rect safe)
    {
        float x = Mathf.Clamp(band.x, safe.xMin, Mathf.Max(safe.xMin, safe.xMax - band.width));
        float y = Mathf.Clamp(band.y, safe.yMin, Mathf.Max(safe.yMin, safe.yMax - band.height));
        return new Rect(x, y, band.width, band.height);
    }

    /// <summary>The point a line is pinned to: the head bone (a Humanoid's, or a POLYGON body's "Head") plus Head Clearance, else the top of what's drawn.</summary>
    Vector3 HeadPoint(Transform speaker)
    {
        if (speaker == null) return Vector3.zero;
        bool known = heads.TryGetValue(speaker, out Transform head);
        if (!known || head == null && Time.unscaledTime - headSearched[speaker] > 2f || head != null && !head.gameObject.activeInHierarchy)
        {
            head = FindHead(speaker);
            heads[speaker] = head;
            headSearched[speaker] = Time.unscaledTime;
        }
        if (head != null) return head.position + Vector3.up * headClearance;
        if (!bodies.TryGetValue(speaker, out Renderer[] parts))
        {
            parts = speaker.GetComponentsInChildren<Renderer>(true);
            bodies[speaker] = parts;
        }
        float top = float.NegativeInfinity;
        foreach (Renderer r in parts)
            if (r != null && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
        Vector3 at = speaker.position;
        return new Vector3(at.x, (top > float.NegativeInfinity ? top : at.y + 2f) + .15f, at.z);
    }

    static Transform FindHead(Transform speaker)
    {
        // A Humanoid body that's drawn (Ace's Sidekick): its own head bone.
        foreach (Animator a in speaker.GetComponentsInChildren<Animator>())
        {
            if (!a.isHuman || a.avatar == null || !a.avatar.isValid) continue;
            SkinnedMeshRenderer skin = a.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || !skin.enabled) continue;
            Transform h = a.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return h;
        }
        // A POLYGON body (the café's people, the night's neighbours): the drawn mesh's bone called Head.
        foreach (SkinnedMeshRenderer s in speaker.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!s.enabled) continue;
            foreach (Transform b in s.bones)
                if (b != null && string.Equals(b.name, "Head", StringComparison.OrdinalIgnoreCase)) return b;
        }
        return null;
    }

    // ================================================================== building the screen

    void Build()
    {
        var canvasObject = new GameObject("Barks screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;   // under the night's note and card (90)
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        canvasRect = canvasObject.GetComponent<RectTransform>();
        font = HudFont();
        rounded = RoundedSprite();
        triangle = TriangleSprite();
        for (int i = 0; i < ViewCount; i++) views[i] = MakeView(i);
        MakeChips();
    }

    // Ace's two replies: a small band each, the key in Ace's colour, stacked (the first on top) in one holder that
    // Place puts where Ace's own line would go.
    void MakeChips()
    {
        chipStack = new View { self = true, active = true };
        chipStack.root = new GameObject("Ace's replies", typeof(RectTransform)).GetComponent<RectTransform>();
        chipStack.root.SetParent(canvasRect, false);
        chipStack.root.anchorMin = chipStack.root.anchorMax = Vector2.zero;
        chipStack.root.pivot = new Vector2(.5f, 0f);
        chipsGroup = chipStack.root.gameObject.AddComponent<CanvasGroup>();
        chipsGroup.blocksRaycasts = false;
        chipsGroup.interactable = false;
        for (int i = 0; i < chips.Length; i++)
        {
            var c = new Chip();
            c.root = Part("Reply " + (i + 1), chipStack.root, new Vector2(.5f, 0f), new Vector2(.5f, 0f));
            c.band = c.root.gameObject.AddComponent<Image>();
            c.band.sprite = rounded;
            c.band.type = Image.Type.Sliced;
            c.band.color = bandColour;
            c.band.raycastTarget = false;
            RectTransform accent = Part("Ace's colour", c.root, Vector2.zero, new Vector2(0f, .5f));
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.offsetMin = new Vector2(8f, 7f);
            accent.offsetMax = new Vector2(13f, -7f);
            c.accent = accent.gameObject.AddComponent<Image>();
            c.accent.raycastTarget = false;
            RectTransform line = Part("Text", c.root, Vector2.zero, Vector2.zero);
            line.anchorMin = Vector2.zero;
            line.anchorMax = Vector2.one;
            line.offsetMin = new Vector2(PadLeft, PadY);
            line.offsetMax = new Vector2(-PadRight, -PadY);
            c.text = line.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) c.text.font = font;
            if (textMaterial != null) c.text.fontSharedMaterial = textMaterial;
            c.text.fontSize = fontSize * .9f;
            c.text.color = textColour;
            c.text.alignment = TextAlignmentOptions.MidlineLeft;
            c.text.textWrappingMode = TextWrappingModes.NoWrap;
            c.text.overflowMode = TextOverflowModes.Ellipsis;
            c.text.richText = true;
            c.text.raycastTarget = false;
            chips[i] = c;
        }
        chipStack.root.gameObject.SetActive(false);
    }

    // Sized to their words once, when they come up (never per frame).
    void ShowChips(NightLines.Choice choice)
    {
        NightLines lines = NightLines.Current;
        if (choice == null || lines == null) return;
        Color colour = ColourOf(AceId);
        string hex = ColorUtility.ToHtmlStringRGB(colour);
        float textMax = maxWidth - PadLeft - PadRight;
        float lineHeight = fontSize * .9f * 1.32f;
        float width = 0f, height = 0f;
        const float Gap = 6f;
        for (int i = 0; i < chips.Length; i++)
        {
            Chip c = chips[i];
            NightLines.Line line = lines.FindLine(i == 0 ? choice.first.line : choice.second.line);
            string key = i == 0 ? ControlHints.Say("1", PadInput.Label(PadButton.West)) : ControlHints.Say("2", PadInput.Label(PadButton.North));
            c.text.text = $"<color=#{hex}><b>{key}</b></color>   {(line != null ? line.text : "")}";
            c.accent.color = colour;
            Vector2 preferred = c.text.GetPreferredValues(c.text.text, textMax, 0f);
            c.size = new Vector2(Mathf.Min(textMax, Mathf.Ceil(preferred.x) + 2f) + PadLeft + PadRight, lineHeight + 2f * PadY);
            c.root.sizeDelta = c.size;
            width = Mathf.Max(width, c.size.x);
            height += c.size.y + (i > 0 ? Gap : 0f);
        }
        // The second reply at the bottom, the first above it.
        chips[1].root.anchoredPosition = Vector2.zero;
        chips[0].root.anchoredPosition = new Vector2(0f, chips[1].size.y + Gap);
        chipStack.size = new Vector2(width, height);
        chipStack.root.sizeDelta = chipStack.size;
        chipsGroup.alpha = 0f;
        chipStack.root.gameObject.SetActive(true);
        chipsUp = true;
    }

    void HideChips()
    {
        chipsUp = false;
        if (chipStack != null && chipStack.root != null) chipStack.root.gameObject.SetActive(false);
    }

    View MakeView(int index)
    {
        var v = new View();
        v.root = new GameObject("Bark " + index, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        v.root.SetParent(canvasRect, false);
        v.root.anchorMin = v.root.anchorMax = Vector2.zero;
        v.root.pivot = new Vector2(.5f, 0f);
        v.group = v.root.GetComponent<CanvasGroup>();
        v.group.alpha = 0f;
        v.group.blocksRaycasts = false;
        v.group.interactable = false;

        v.bandRect = Part("Band", v.root, new Vector2(.5f, 0f), new Vector2(.5f, 0f));
        v.band = v.bandRect.gameObject.AddComponent<Image>();
        v.band.sprite = rounded;
        v.band.type = Image.Type.Sliced;
        v.band.color = bandColour;
        v.band.raycastTarget = false;

        v.accentRect = Part("Speaker's colour", v.bandRect, new Vector2(0f, 0f), new Vector2(0f, 0f));
        v.accentRect.anchorMin = new Vector2(0f, 0f);
        v.accentRect.anchorMax = new Vector2(0f, 1f);
        v.accentRect.pivot = new Vector2(0f, .5f);
        v.accentRect.offsetMin = new Vector2(8f, 7f);
        v.accentRect.offsetMax = new Vector2(13f, -7f);
        v.accent = v.accentRect.gameObject.AddComponent<Image>();
        v.accent.raycastTarget = false;

        v.textRect = Part("Line", v.bandRect, new Vector2(0f, 0f), new Vector2(0f, 0f));
        v.textRect.anchorMin = Vector2.zero;
        v.textRect.anchorMax = Vector2.one;
        v.textRect.offsetMin = new Vector2(PadLeft, PadY);
        v.textRect.offsetMax = new Vector2(-PadRight, -PadY);
        v.text = v.textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) v.text.font = font;
        if (textMaterial == null) textMaterial = Shadow(v.text);
        if (textMaterial != null) v.text.fontSharedMaterial = textMaterial;
        v.text.fontSize = fontSize;
        v.text.color = textColour;
        v.text.alignment = TextAlignmentOptions.MidlineLeft;
        v.text.textWrappingMode = TextWrappingModes.Normal;
        v.text.overflowMode = TextOverflowModes.Ellipsis;
        v.text.maxVisibleLines = 2;
        v.text.richText = false;
        v.text.raycastTarget = false;

        // The triangle sprite points right: 10 long by 18 across, turned about its middle to point down, its base
        // along the band's bottom edge.
        v.tailRect = Part("Tail", v.root, new Vector2(.5f, 0f), new Vector2(.5f, .5f));
        v.tailRect.sizeDelta = new Vector2(TailHeight, 18f);
        v.tailRect.anchoredPosition = new Vector2(0f, -TailHeight * .5f + .5f);
        v.tailRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
        v.tail = v.tailRect.gameObject.AddComponent<Image>();
        v.tail.sprite = triangle;
        v.tail.color = bandColour;
        v.tail.raycastTarget = false;

        v.arrowRect = Part("Arrow", v.root, new Vector2(.5f, 0f), new Vector2(.5f, .5f));
        v.arrowRect.sizeDelta = new Vector2(16f, 20f);
        v.arrow = v.arrowRect.gameObject.AddComponent<Image>();
        v.arrow.sprite = triangle;
        v.arrow.raycastTarget = false;
        v.arrowRect.gameObject.SetActive(false);

        v.root.gameObject.SetActive(false);
        return v;
    }

    // Sizes the band to its line (once per line, never per frame).
    void Dress(View v)
    {
        Color colour = ColourOf(v.speakerId);
        v.accent.color = colour;
        v.arrow.color = colour;
        v.text.text = v.line;
        float textMax = maxWidth - PadLeft - PadRight;
        Vector2 preferred = v.text.GetPreferredValues(v.line, textMax, 0f);
        float lineHeight = fontSize * 1.32f;
        float width = Mathf.Min(textMax, Mathf.Ceil(preferred.x) + 2f);
        float height = Mathf.Min(lineHeight * 2f, Mathf.Max(lineHeight, Mathf.Ceil(preferred.y)));
        v.size = new Vector2(width + PadLeft + PadRight, height + 2f * PadY);
        v.bandRect.sizeDelta = v.size;
        v.bandRect.anchoredPosition = Vector2.zero;
        v.root.sizeDelta = v.size;
    }

    static RectTransform Part(string name, Transform parent, Vector2 anchor, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        return rect;
    }

    // A rounded box (9-sliced), drawn once.
    static Sprite RoundedSprite()
    {
        const int size = 32, radius = 10;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + .5f)));
            }
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                                   new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // A triangle pointing right (the tail and the arrow turn it).
    static Sprite TriangleSprite()
    {
        const int w = 20, h = 20;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + .5f) / w, half = .5f * (1f - u);
                float dy = Mathf.Abs((y + .5f) / h - .5f);
                float edge = (half - dy) * h;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge + .5f)));
            }
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // The subtitles' soft shadow (ConversationUI), on one copy of the font's material shared by every line.
    static Material Shadow(TMP_Text text)
    {
        Material shared = text.fontSharedMaterial;
        if (shared == null || !shared.HasProperty(ShaderUtilities.ID_UnderlayColor)) return null;
        var material = new Material(shared) { name = shared.name + " (bark shadow)", hideFlags = HideFlags.DontSave };
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, .85f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .45f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.45f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, .25f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .35f);
        return material;
    }

    // The HUD's own lettering (as NightCycle and NightNotebook use).
    static TMP_FontAsset HudFont()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        if (any == null) any = FindAnyObjectByType<TextMeshProUGUI>();
        return any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
    }
}
