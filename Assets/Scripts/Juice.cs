using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// JUICE: SMALL FEEDBACK THAT MAKES THINGS FEEL GOOD (6 Oct 2026; Mansoor's playtest: "it needs to start feeling a lot
// better in terms of juice"; his call: the first juice pass in code, the sounds later with the Sonniss files)
//
//   Juice.Emote(who, Juice.Icon.Heart)  a little badge pops up over someone's head and floats off: a heart for a perfect
//                                       repair, a star for a good one, a steaming cup for a drink, a tick when they're
//                                       reassured, dots when it's only so-so or they're let down, a grey cloud when it
//                                       comes back unfixed, a sweat drop when they get frustrated, an angry burst when
//                                       they're furious or walk out
//   Juice.Money(who, 6, 3)              "+$6" pops over them as they pay, "+$3 tip" with it, and both float up
//   Juice.Sparkle(point, big)           sparks burst from a point (a part fitted, a spot of grime gone; big: a repair done)
//   Juice.Words(point, "New part")      a word pops at a point and floats up, on a dark plate (playtest 4: it reads on
//                                       any background; "Fixed!" was pale green on the freshly lit white screen)
//   Juice.FixedMoment(...)              the repair's one ending: "Fixed! · Perfect", the big sparkle, the chime
//   Juice.BackTogether(...)             a device closed up with faults left: "Back together · Good", quietly
//   Juice.Dust(point)                   a puff of dust where the brush lifted some (4b)
//   Juice.Gleam(from, to)               a gleam sweeps along a line (the glass just wiped clean, 4b)
//   Juice.HandOver(item, to)            what Ace hands over flies to them in a short arc and pops into their hands
//                                       instead of vanishing (a copy of what's drawn; the real thing goes at once, as before)
//   Juice.Mark(point, Juice.Icon.Mess)  a badge that stays over a point until Juice.Unmark: the man at the bins' mess on a
//                                       table (playtest 3: his cups are a few pixels from the overhead camera, so the table
//                                       gets a used-cup badge that bobs over it until Ace clears it). Out of the way while
//                                       Ace is busy (talking, at a station or the counter's repair view, holding something
//                                       up to look at), paused, or once the day is over
//
// A repair on the bench gets its moments by itself (the item being worked on is watched). ONE ENDING (playtest 4, 10 Oct;
// the veteran: "a repair ends twice", "Fixed!" fired with the cover on the mat and both screws out, then "Back together ·
// Perfect" came after the climax). Now: the moment the LAST TASK completes (the last seat, the last fleck), the device
// wakes, quietly, with its own parts and sounds (DeviceWake: the phone's screen lights, the watch is wound and ticks, the
// camera's shutter fires) and no words; "Fixed! · Perfect", the big sparkle and the chime fire ONCE, when it is back in one
// piece with every fault fixed (JobBase.IsFinished): after the last screw for a device that was opened, at once (just
// after the wake) for one that never was. A device closed up with faults left gets the quiet "Back together · Good"
// (smaller, no sparkle), so closing it early is told before the customer tells it. This changes bench-spec-v2 §2.5 and §7
// ("Fixed!" on the last seat): the wake stays on the last seat; "Fixed!" moved to the true end.
//
// FACE UP FOR THE WORDS (12-13 Oct; the veteran's second run: after a screen swap the phone ends back up, its last screws are
// in its back, and "Fixed! · Perfect" landed on its back; his pick, the findings' §10.5: turn it face up for the moment).
// A device finished lying on its face is turned over first (ItemInspector.ShowFace, a third of a second), and the words,
// the sparkle and the chime come when it lies face up, placed on it then. OnFixed tells anyone who asks (the NPC lead's
// reactions) once, at that moment, with the job.
//
// LEVELLED UP (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.3): money pops in a chip of the
// brand's green (no more yellow), the badges have thicker symbols and a rim in their own colour, they bounce in on a
// spring, and a heart or a star throws a tiny burst of its colour as it lands. The lettering and its shadow are the UI
// skin's (UiSkin), and everything times itself on the UI's own clock (UiClock), so it holds while the phone pauses.
// Built in code on a screen canvas of its own, sorted under the barks; the icons are drawn in code too: nothing in the scene
// changes and no asset is added. Pinned the way barks are (Barks.HeadPointOf), the same size at any zoom. Hidden while the
// recap has the screen. Every call is safe anywhere: without a camera nothing shows. The sounds named here (Sfx) are
// silent until the sound bank has files.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(910)]
public sealed class Juice : MonoBehaviour
{
    /// <summary>The badges. Saved nowhere, so new ones go on the end.</summary>
    public enum Icon { Heart, Star, Cup, Tick, Dots, Cloud, Drop, Burst, Mess }

    const int PopCount = 16, SparkCount = 40, MarkerCount = 12;
    const float MarkerSize = 46f;
    const float EmoteLife = 1.5f, MoneyLife = 1.35f, WordsLife = 1.4f, SparkLife = .55f, BigSparkLife = .85f;
    const float IconSize = 58f, Rise = 46f;
    // The one ending is read, not glanced at: it stays up longer than a word; the quiet closing a little less.
    const float FixedLife = 2.1f, TogetherLife = 1.6f;
    // When the device wakes in the same frame as "Fixed!" (a device never opened: its last task is the end), the device
    // speaks first, with its own parts, and the words come a beat after (the phone's glow starts 0.25 s in).
    const float WakeLead = .3f;

    static Juice instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        sprites = null;
        dustSprite = null;
        nextMarker = 0;
        LastWords = "";
        LastWordsAt = -1f;
        LastWordsPoint = Vector3.zero;
        LastWordsContrast = 0f;
        DustPuffs = 0;
        Gleams = 0;
        Finishes = 0;
        Wakes = 0;
        OnFixed = null;
    }

    /// <summary>
    /// A repair at the bench has just ended ("Fixed!" is going up over the device, face up): once a repair, with its job.
    /// For whatever reacts to the moment (the room, 12 Oct: "nobody turns ... when a 'Fixed!' goes off at the repair bar").
    /// </summary>
    public static event System.Action<JobBase> OnFixed;

    sealed class Pop
    {
        public RectTransform root;
        public Image image, chip;
        public TextMeshProUGUI text;
        public CanvasGroup group;
        public bool active, pinned;
        public Transform who;          // pinned over this person's head, or
        public Vector3 point;          // at this point in the world
        public Vector2 offset;         // reference pixels from where it's pinned
        public float born, life;
        public bool clamp;             // words: kept inside the screen (a device zoomed in fills it)
    }

    sealed class Spark
    {
        public RectTransform root;
        public Image image;
        public bool active;
        public Vector3 point;
        public Vector2 velocity, offset;
        public float born, life, spin, size;
        public float gravity;          // screen pixels per second squared, downward when negative (a spark falls, dust rises)
        public bool soft;              // a dust mote: the soft disc, fading out rather than shrinking to a point
    }

    // A badge that stays where it's put until it's taken down (Mark, Unmark).
    sealed class Marker
    {
        public RectTransform root;
        public Image image;
        public CanvasGroup group;
        public bool active;
        public int id;
        public Vector3 point;
        public float born;
    }

    readonly Pop[] pops = new Pop[PopCount];
    readonly Spark[] sparks = new Spark[SparkCount];
    readonly Marker[] markers = new Marker[MarkerCount];
    static int nextMarker;
    Canvas canvas;
    RectTransform canvasRect;
    Camera cam;
    ItemInspector inspector;
    JobBase watched;
    bool watchedDone, watchedTogether, watchedFinished, hidden;
    float nextInspectorLook;
    // A repair finished lying on its face: its ending waits for the device to turn face up (ShowFace), a moment at most.
    JobBase finishing;
    float finishingDelay, finishingBy;

    static Sprite[] sprites;
    static Sprite sparkSprite, dustSprite;

    /// <summary>The last words popped (for checks; plain, without the colour tags), and when (UiClock).</summary>
    public static string LastWords { get; private set; } = "";
    public static float LastWordsAt { get; private set; } = -1f;
    /// <summary>Where in the world the last words were pinned (checks: "Fixed!" over the device's face, not its back).</summary>
    public static Vector3 LastWordsPoint { get; private set; }
    /// <summary>The last words' contrast with their plate, as WCAG reckons it (the plate over white, its worst case; checks).</summary>
    public static float LastWordsContrast { get; private set; }
    /// <summary>How many puffs of dust have gone up (checks).</summary>
    public static int DustPuffs { get; private set; }
    /// <summary>How many gleams have swept (checks).</summary>
    public static int Gleams { get; private set; }
    /// <summary>How many times the repair's one ending ("Fixed!") has fired (checks: once a repair).</summary>
    public static int Finishes { get; private set; }
    /// <summary>How many times a device at the bench has woken (its last task done; checks).</summary>
    public static int Wakes { get; private set; }

    // The words' plate: the skin's band behind text over the world (the HUD's own dark), so a word reads over a lit white
    // screen, the yellow cloth, the blue mat or the oak alike. The words keep their colour.
    static readonly Color Plate = UiSkin.Band;
    static readonly Color FixedColour = new Color(.62f, 1f, .7f, 1f);

    // ================================================================== what game code calls

    /// <summary>A badge pops up over <paramref name="who"/>'s head and floats off.</summary>
    public static void Emote(Transform who, Icon icon)
    {
        if (who == null || !Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        // One badge over a head at a time: a newer one takes the older one's place.
        foreach (Pop p in j.pops)
            if (p.active && p.who == who && p.image.enabled) p.active = false;
        Pop pop = j.Take();
        pop.image.enabled = true;
        pop.image.sprite = Sprites[(int)icon];
        pop.chip.enabled = false;
        pop.text.enabled = false;
        pop.root.sizeDelta = new Vector2(IconSize, IconSize);
        j.Show(pop, who, Vector3.zero, Vector2.zero, EmoteLife);
        Sfx.Play("juice.emote", who.position, .6f);
        // A heart or a star throws a tiny burst of its colour as it lands (round the badge's middle).
        if (icon == Icon.Heart || icon == Icon.Star)
            j.Burst(Barks.HeadPointOf(who), new Vector2(0f, IconSize * .5f), icon == Icon.Heart ? HeartColour : StarColour);
    }

    /// <summary>
    /// "+$<paramref name="amount"/>" pops over <paramref name="who"/> as they pay (with "+$tip tip"), and floats up. It
    /// comes a beat after their face (the badge lands first, the money a moment later, above and to the side, so the two
    /// don't crowd: Mansoor, 8 Oct, "they look a little clustered when they both come at the same time").
    /// </summary>
    public static void Money(Transform who, int amount, int tip = 0, float delay = .45f)
    {
        if (who == null || amount + tip <= 0 || !Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        Pop pop = j.Take();
        pop.image.enabled = false;
        pop.text.enabled = true;
        // In a chip of the brand's green: the money's colour everywhere now (the cash stack's pop is the same chip).
        pop.text.text = tip > 0 ? $"+${amount}  <size=72%><color={TipColour}>+${tip} tip</color></size>" : $"+${amount}";
        pop.text.fontSize = 32f;
        pop.text.color = UiSkin.Paper;
        pop.text.alignment = TextAlignmentOptions.Center;
        Vector2 words = pop.text.GetPreferredValues(pop.text.text, 999f, 999f);
        pop.root.sizeDelta = new Vector2(Mathf.Ceil(words.x) + 30f, 46f);
        pop.chip.enabled = true;
        pop.chip.color = UiSkin.Brand;
        j.Show(pop, who, Vector3.zero, new Vector2(84f, 34f), MoneyLife);
        pop.born += Mathf.Max(0f, delay);
    }

    static readonly string TipColour = UiSkin.HexOf(UiSkin.GoldSoft);
    static readonly Color HeartColour = new Color(.91f, .31f, .36f), StarColour = UiSkin.Gold;

    /// <summary>A word pops at <paramref name="point"/> and floats up ("New part"), on the dark plate.</summary>
    public static void Words(Vector3 point, string words, Color colour)
    {
        if (string.IsNullOrEmpty(words) || !Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        j.Say(point, words, words, colour, 40f, WordsLife, 0f);
    }

    /// <summary>
    /// The repair's one ending (playtest 4): the big sparkle at <paramref name="centre"/> (the device), "Fixed! · Perfect"
    /// over it at <paramref name="wordsAt"/> on the plate, and the chime; <paramref name="delay"/> seconds from now (a beat
    /// after the device wakes, when the two come together). Counted in <see cref="Finishes"/>.
    /// </summary>
    public static void FixedMoment(Vector3 centre, Vector3 wordsAt, JobGrade grade, float delay = 0f)
    {
        if (!Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        Finishes++;
        j.Sparks(centre, true, delay);
        // "Fixed!" in its own pale green (kept: it is the repair's colour), the grade after it in the grade's, smaller.
        string rich = $"Fixed!  <size=70%><color={UiSkin.HexOf(GradeColour(grade))}>· {grade}</color></size>";
        j.Say(wordsAt, "Fixed! · " + grade, rich, FixedColour, 46f, FixedLife, delay);
        if (delay > 0f) Sfx.Play2DLater("repair.fixed", delay);
        else Sfx.Play2D("repair.fixed");
    }

    /// <summary>
    /// A device closed up with faults left (playtest 4): "Back together · Good" at <paramref name="wordsAt"/>, smaller and
    /// quieter than the ending (no sparkle, a softer chime), so the grade it would go back at is told before it goes back.
    /// </summary>
    public static void BackTogether(Vector3 wordsAt, JobGrade grade)
    {
        if (!Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        string rich = $"Back together  <color={UiSkin.HexOf(GradeColour(grade))}>· {grade}</color>";
        j.Say(wordsAt, "Back together · " + grade, rich, UiSkin.BandText, 30f, TogetherLife, 0f);
        Sfx.Play2D("repair.ready", .6f);
    }

    /// <summary>A grade's own colour on the plate: Perfect gold, Good the brand's light green, less a warm grey.</summary>
    public static Color GradeColour(JobGrade grade) =>
        grade == JobGrade.Perfect ? UiSkin.Gold : grade == JobGrade.Good ? UiSkin.BrandBright : new Color(.78f, .78f, .74f, 1f);

    // A plated word pop: `rich` is drawn, `plain` is what the checks read (LastWords). The plate is the pop's chip, sized to
    // the words; the words are centred on it.
    void Say(Vector3 point, string plain, string rich, Color colour, float size, float life, float delay)
    {
        Pop pop = Take();
        pop.image.enabled = false;
        pop.text.enabled = true;
        pop.text.text = rich;
        pop.text.fontSize = size;
        pop.text.color = colour;
        pop.text.alignment = TextAlignmentOptions.Center;
        Vector2 measured = pop.text.GetPreferredValues(rich, 9999f, 9999f);
        pop.root.sizeDelta = new Vector2(Mathf.Ceil(measured.x) + size * .9f, Mathf.Ceil(size * 1.55f));
        pop.chip.enabled = true;
        pop.chip.color = Plate;
        Show(pop, null, point, new Vector2(0f, 26f), life);
        pop.born += Mathf.Max(0f, delay);
        pop.clamp = true;
        LastWords = plain;
        LastWordsAt = UiClock.Now;
        LastWordsPoint = point;
        LastWordsContrast = Contrast(colour, Plate);
    }

    /// <summary>Sparks burst from <paramref name="point"/>: a few for a step of a repair, many for a whole one.</summary>
    public static void Sparkle(Vector3 point, bool big = false)
    {
        if (!Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        j.Sparks(point, big, 0f);
    }

    void Sparks(Vector3 point, bool big, float delay)
    {
        int n = big ? 16 : 7;
        float now = UiClock.Now + Mathf.Max(0f, delay);
        for (int i = 0; i < n; i++)
        {
            Spark s = TakeSpark();
            float a = (i + Random.value * .6f) / n * Mathf.PI * 2f;
            float speed = (big ? 260f : 170f) * Random.Range(.65f, 1.1f);
            s.point = point;
            s.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            s.born = now - Random.value * .04f;
            s.life = (big ? BigSparkLife : SparkLife) * Random.Range(.8f, 1.1f);
            s.spin = Random.Range(-300f, 300f);
            s.size = (big ? 30f : 22f) * Random.Range(.7f, 1.15f);
            s.offset = Vector2.zero;
            s.gravity = -60f;
            s.soft = false;
            s.image.sprite = SparkSprite;
            s.image.color = i % 3 == 0 ? new Color(1f, .97f, .78f, 1f) : new Color(1f, .82f, .35f, 1f);
            s.active = true;
            s.root.gameObject.SetActive(true);
        }
        if (delay > 0f) Sfx.PlayLater(big ? "juice.sparkle.big" : "juice.sparkle", point, delay, .7f);
        else Sfx.Play(big ? "juice.sparkle.big" : "juice.sparkle", point, .7f);
    }

    /// <summary>A colour's contrast on the words' plate, as WCAG reckons it, the plate over white (its worst case): the
    /// steps report it for each colour the words come in.</summary>
    public static float ContrastOnPlate(Color text) => Contrast(text, Plate);

    /// <summary>"Fixed!"'s own pale green (kept: the repair's colour), for the steps' report.</summary>
    public static Color FixedWordsColour => FixedColour;

    // The contrast of a colour on the plate, as WCAG reckons it ((L1 + .05) / (L2 + .05)), the plate laid over white: its
    // worst case (a dark plate is at its lightest over the lit screen).
    static float Contrast(Color text, Color plate)
    {
        Color over = Color.Lerp(Color.white, new Color(plate.r, plate.g, plate.b, 1f), plate.a);
        float a = Luminance(text), b = Luminance(over);
        return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
    }

    static float Luminance(Color c) => .2126f * Linear(c.r) + .7152f * Linear(c.g) + .0722f * Linear(c.b);

    static float Linear(float v) => v <= .03928f ? v / 12.92f : Mathf.Pow((v + .055f) / 1.055f, 2.4f);

    /// <summary>A little puff of dust goes up from <paramref name="point"/>: the brush lifted some (4b).</summary>
    public static void Dust(Vector3 point)
    {
        if (!Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        DustPuffs++;
        float now = UiClock.Now;
        const int n = 4;
        for (int i = 0; i < n; i++)
        {
            Spark s = j.TakeSpark();
            float a = Mathf.PI * (.25f + .5f * (i + Random.value) / n);      // up and out, between 45° and 135°
            s.point = point;
            s.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(28f, 60f);
            s.born = now - Random.value * .03f;
            s.life = Random.Range(.4f, .6f);
            s.spin = Random.Range(-40f, 40f);
            s.size = Random.Range(12f, 20f);
            s.offset = Vector2.zero;
            s.gravity = 18f;                                                     // dust drifts up, slowing
            s.soft = true;
            s.image.sprite = DustSprite;
            s.image.color = new Color(.66f, .60f, .50f, .75f);
            s.active = true;
            s.root.gameObject.SetActive(true);
        }
    }

    /// <summary>A gleam sweeps from <paramref name="from"/> to <paramref name="to"/>: the glass just wiped clean (4b), starting
    /// <paramref name="delay"/> seconds from now (playtest 4: a beat, so the cloth lifting off the glass is clear of it).</summary>
    public static void Gleam(Vector3 from, Vector3 to, float delay = 0f)
    {
        if (!Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        Gleams++;
        float now = UiClock.Now + Mathf.Max(0f, delay);
        const int n = 9;
        for (int i = 0; i < n; i++)
        {
            Spark s = j.TakeSpark();
            float k = i / (float)(n - 1);
            s.point = Vector3.Lerp(from, to, k);
            s.velocity = new Vector2(0f, Random.Range(-8f, 8f));
            s.born = now + k * .22f;                                            // lit one after another along the line
            s.life = .34f;
            s.spin = Random.Range(-90f, 90f);
            s.size = (i == n / 2 ? 34f : 24f) * Random.Range(.9f, 1.1f);
            s.offset = Vector2.zero;
            s.gravity = 0f;
            s.soft = false;
            s.image.sprite = SparkSprite;
            s.image.color = i % 2 == 0 ? Color.white : new Color(.85f, .95f, 1f, 1f);
            s.active = true;
            s.root.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// What Ace hands over flies to <paramref name="to"/> in a short arc and pops into their hands. A copy of what's drawn
    /// makes the trip; call it just before the real thing is consumed or destroyed (it isn't touched).
    /// </summary>
    public static void HandOver(GameObject item, Transform to, float seconds = .36f)
    {
        if (item == null || to == null || !Application.isPlaying) return;
        Juice j = Ensure();
        if (j == null) return;
        GameObject ghost = Ghost(item);
        if (ghost != null) j.StartCoroutine(j.Fly(ghost, to, seconds));
    }

    /// <summary>
    /// A badge that stays over <paramref name="point"/> (a point in the world) until <see cref="Unmark"/>: it pops up,
    /// then bobs gently. The handle to take it down by, or 0 when nothing can show (no camera, or every marker in use).
    /// </summary>
    public static int Mark(Vector3 point, Icon icon)
    {
        if (!Application.isPlaying) return 0;
        Juice j = Ensure();
        if (j == null) return 0;
        foreach (Marker m in j.markers)
        {
            if (m.active) continue;
            m.active = true;
            m.id = ++nextMarker;
            m.point = point;
            m.born = UiClock.Now;
            m.image.sprite = Sprites[(int)icon];
            m.group.alpha = 0f;
            m.root.localScale = Vector3.zero;
            m.root.gameObject.SetActive(true);
            return m.id;
        }
        return 0;
    }

    /// <summary>Takes down the badge <see cref="Mark"/> put up (nothing for 0 or one already down).</summary>
    public static void Unmark(int id)
    {
        if (id == 0 || instance == null) return;
        foreach (Marker m in instance.markers)
            if (m.active && m.id == id)
            {
                m.active = false;
                m.root.gameObject.SetActive(false);
            }
    }

    // Whether the lasting badges keep off the screen: paused, the day over, someone talking to Ace, or Ace at a station
    // (the bench, the espresso machine), in the counter's repair view or holding something up to look at. Only asked
    // while a badge is up; Ace's parts are found once.
    PlayerInteractor busyPlayer;
    ItemInspector busyInspector;
    CounterRepairView busyCounter;

    bool MarksQuiet()
    {
        bool any = false;
        foreach (Marker m in markers) if (m != null && m.active) { any = true; break; }
        if (!any) return false;
        if (Time.timeScale <= 0f || ConversationController.AnyOpen) return true;
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return true;
        if (busyPlayer == null)
        {
            PlayerCarry carry = PlayerCarry.Instance;
            if (carry == null) return false;
            busyPlayer = carry.GetComponent<PlayerInteractor>();
            busyInspector = carry.GetComponent<ItemInspector>();
            busyCounter = carry.GetComponent<CounterRepairView>();
        }
        // Ace in a close-up (the drinks close-up, an item at the bench, the counter phone: PlayerInteractor.InCloseUp).
        return busyPlayer != null && busyPlayer.InCloseUp
            || busyInspector != null && busyInspector.IsHoldingItem
            || busyCounter != null && busyCounter.OwnsInput;
    }

    /// <summary>How many lasting badges are up (checks).</summary>
    public static int MarksUp
    {
        get
        {
            int n = 0;
            if (instance != null) foreach (Marker m in instance.markers) if (m != null && m.active) n++;
            return n;
        }
    }

    /// <summary>Whether the lasting badge <paramref name="id"/> is up, and where on the screen (pixels) it was last drawn.</summary>
    public static bool MarkShowing(int id, out Vector2 screen)
    {
        screen = default;
        if (id == 0 || instance == null) return false;
        float scale = instance.canvas.scaleFactor > 0f ? instance.canvas.scaleFactor : 1f;
        foreach (Marker m in instance.markers)
            if (m.active && m.id == id)
            {
                screen = m.root.anchoredPosition * scale;
                return m.group.alpha > .5f;
            }
        return false;
    }

    // ================================================================== for checks (JuiceCheck)

    /// <summary>How many badges and words are up now.</summary>
    public static int PopsUp
    {
        get
        {
            int n = 0;
            if (instance != null) foreach (Pop p in instance.pops) if (p != null && p.active) n++;
            return n;
        }
    }

    /// <summary>How many sparks are flying now.</summary>
    public static int SparksUp
    {
        get
        {
            int n = 0;
            if (instance != null) foreach (Spark s in instance.sparks) if (s != null && s.active) n++;
            return n;
        }
    }

    /// <summary>The sorting order of the screen it all draws on (under the barks' 80), or int.MinValue before anything showed.</summary>
    public static int SortingOrder => instance != null && instance.canvas != null ? instance.canvas.sortingOrder : int.MinValue;

    /// <summary>
    /// The newest pop over <paramref name="who"/>'s head as last drawn: its bottom centre in screen pixels, and the icon's
    /// name or the words. False: nothing is up over them.
    /// </summary>
    public static bool ShowingOver(Transform who, out Vector2 screen, out string what)
    {
        screen = default;
        what = "";
        if (instance == null || who == null) return false;
        float scale = instance.canvas.scaleFactor > 0f ? instance.canvas.scaleFactor : 1f;
        Pop newest = null;
        foreach (Pop p in instance.pops)
            if (p != null && p.active && p.who == who && (newest == null || p.born > newest.born)) newest = p;
        if (newest == null) return false;
        screen = newest.root.anchoredPosition * scale;
        what = newest.image.enabled ? IconName(newest.image.sprite) : newest.text.text;
        return true;
    }

    static string IconName(Sprite sprite)
    {
        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] == sprite) return ((Icon)i).ToString();
        return "badge";
    }

    // ================================================================== the canvas

    // The bench's watcher (the wake and the one ending) runs from the start of play, not from the first effect. Made on demand,
    // it first saw the lab's phone after the seat's sparkle (its first effect and its last task) and took it as done already:
    // the phone never woke (15 Oct, Bench 3). A first repair whose first effect is its last would have lost its wake too.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WatchFromTheStart() => Ensure();

    static Juice Ensure()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;
        var go = new GameObject("Juice (while playing)");
        instance = go.AddComponent<Juice>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    void OnEnable() => Canvas.willRenderCanvases += Place;

    void OnDisable() => Canvas.willRenderCanvases -= Place;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // A tiny burst of <colour> round a point (a badge landing): small sparks, a beat after it pops, to land with it.
    void Burst(Vector3 point, Vector2 offset, Color colour)
    {
        float now = UiClock.Now;
        const int n = 7;
        for (int i = 0; i < n; i++)
        {
            Spark s = TakeSpark();
            float a = (i + Random.value * .5f) / n * Mathf.PI * 2f;
            s.point = point;
            s.offset = offset;
            s.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(120f, 170f);
            s.born = now + .1f;
            s.life = .42f * Random.Range(.85f, 1.1f);
            s.spin = Random.Range(-240f, 240f);
            s.size = Random.Range(13f, 18f);
            s.gravity = -60f;
            s.soft = false;
            s.image.sprite = SparkSprite;
            s.image.color = i % 2 == 0 ? colour : Color.Lerp(colour, Color.white, .55f);
            s.active = true;
            s.root.gameObject.SetActive(true);
            s.image.enabled = false;
        }
    }

    void Build()
    {
        var canvasObject = new GameObject("Juice screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;   // under the barks (80) and the night's note and card (90)
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        canvasRect = canvasObject.GetComponent<RectTransform>();
        // The lasting badges first, so a pop or a spark draws over them.
        for (int i = 0; i < MarkerCount; i++)
        {
            var root = new GameObject("Marker " + i, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(.5f, 0f);
            root.sizeDelta = new Vector2(MarkerSize, MarkerSize);
            var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(root, false);
            Stretch(image.rectTransform);
            image.raycastTarget = false;
            image.preserveAspect = true;
            root.gameObject.SetActive(false);
            markers[i] = new Marker { root = root, image = image, group = root.GetComponent<CanvasGroup>() };
            markers[i].group.blocksRaycasts = false;
            markers[i].group.interactable = false;
        }
        for (int i = 0; i < PopCount; i++)
        {
            var root = new GameObject("Pop " + i, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(.5f, 0f);
            var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(root, false);
            Stretch(image.rectTransform);
            image.raycastTarget = false;
            image.preserveAspect = true;
            // The money's chip, behind its words (off for badges and words).
            var chip = new GameObject("Chip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            chip.rectTransform.SetParent(root, false);
            Stretch(chip.rectTransform);
            UiSkin.Round(chip, 23f);
            chip.raycastTarget = false;
            chip.enabled = false;
            var chipShadow = chip.gameObject.AddComponent<Shadow>();
            chipShadow.effectColor = new Color(0f, 0f, 0f, .32f);
            chipShadow.effectDistance = new Vector2(0f, -3f);
            var text = new GameObject("Words", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(root, false);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            UiSkin.UseFont(text);
            UiSkin.Shadowed(text);
            text.alignment = TextAlignmentOptions.Bottom;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            root.gameObject.SetActive(false);
            pops[i] = new Pop { root = root, image = image, chip = chip, text = text, group = root.GetComponent<CanvasGroup>() };
            pops[i].group.blocksRaycasts = false;
            pops[i].group.interactable = false;
        }
        for (int i = 0; i < SparkCount; i++)
        {
            var image = new GameObject("Spark " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(canvasRect, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.raycastTarget = false;
            image.sprite = SparkSprite;
            image.gameObject.SetActive(false);
            sparks[i] = new Spark { root = image.rectTransform, image = image };
        }
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    Pop Take()
    {
        Pop oldest = pops[0];
        foreach (Pop p in pops)
        {
            if (!p.active) return p;
            if (p.born < oldest.born) oldest = p;
        }
        return oldest;
    }

    Spark TakeSpark()
    {
        Spark oldest = sparks[0];
        foreach (Spark s in sparks)
        {
            if (!s.active) return s;
            if (s.born < oldest.born) oldest = s;
        }
        return oldest;
    }

    void Show(Pop pop, Transform who, Vector3 point, Vector2 offset, float life)
    {
        pop.who = who;
        pop.pinned = who != null;
        pop.point = point;
        pop.offset = offset;
        pop.born = UiClock.Now;
        pop.life = life;
        pop.clamp = false;
        pop.active = true;
        pop.group.alpha = 0f;
        pop.root.localScale = Vector3.zero;
        pop.root.gameObject.SetActive(true);
    }

    void Update()
    {
        bool hide = RecapUI.Showing;
        if (hide != hidden)
        {
            hidden = hide;
            canvas.enabled = !hide;
        }
        WatchTheBench();
        FinishOnceFaceUp();
    }

    // The item being worked on at the bench: the one ending (playtest 4; the header). Three beats, each once:
    //   its LAST TASK completes (quality 1: the last seat, the last fleck): the device wakes, quietly (DeviceWake: its own
    //     parts and sounds; the seat's or the spot's own small sparkle has just gone off); no words;
    //   it is FINISHED (every fault fixed, nothing off it: JobBase.IsFinished): "Fixed! · Perfect", the big sparkle and the
    //     chime. For a device that was opened that is the last screw home; for one that never was, it is the last task
    //     itself, and the words come a beat after the wake. A device finished lying on its face is turned face up first
    //     (12 Oct, §10.5), and the ending comes when it lies face up (FinishOnceFaceUp);
    //   it is back in one piece WITH FAULTS LEFT: "Back together · Good", quietly.
    // An item already finished (or already awake) when it is taken up shows none of them again.
    void WatchTheBench()
    {
        if (UiClock.Now >= nextInspectorLook && inspector == null)
        {
            nextInspectorLook = UiClock.Now + 2f;
            inspector = FindAnyObjectByType<ItemInspector>();
        }
        JobBase item = inspector != null ? inspector.FocusedItem : null;
        if (item != watched)
        {
            watched = item;
            watchedDone = item != null && item.Quality >= .999f;
            watchedTogether = item != null && item.CanHandBack;
            watchedFinished = watchedDone && watchedTogether;
            if (item != null && !(item is DrinkJob))
                Debug.Log($"[Juice] in the close-up: {item.name}, quality {item.Quality:0.00}, {(watchedTogether ? "in one piece" : "in pieces")}" +
                          (watchedDone ? " (its tasks done already: it won't wake again)" : ""));
            return;
        }
        if (item == null || item is DrinkJob) return;
        bool done = item.Quality >= .999f;
        bool together = item.CanHandBack;
        bool wokeNow = false;
        if (done && !watchedDone)
        {
            watchedDone = true;
            wokeNow = true;
            Wakes++;
            Debug.Log($"[Juice] {item.name}: its last task done (quality {item.Quality:0.00}): it wakes.");
            DeviceWake.Fire(item);
        }
        if (done && together && !watchedFinished)
        {
            watchedFinished = true;
            float delay = wokeNow ? WakeLead : 0f;
            // Finished lying on its face (the phone after a screen swap: its last screws are in its back): over it goes, and
            // the words wait for its face. Otherwise, now.
            if (inspector != null && inspector.ShowFace())
            {
                finishing = item;
                finishingDelay = delay;
                finishingBy = UiClock.Now + 1.5f;   // the flip takes a third of a second; this is only a net
            }
            else Finish(item, delay);
        }
        else if (together && !watchedTogether && !done && item.Quality > 0f)
            BackTogether(Above(item.gameObject), item.Grade);
        watchedTogether = together;
    }

    // The ending held back for the flip comes the frame the device lies face up (or is no longer the one in the close-up:
    // taken away mid-turn, it ends where it is), placed on the device as it lies then.
    void FinishOnceFaceUp()
    {
        if (finishing == null) return;
        bool stillTurning = inspector != null && inspector.FocusedItem == finishing && inspector.Flipping;
        if (stillTurning && UiClock.Now < finishingBy) return;
        JobBase job = finishing;
        finishing = null;
        Finish(job, finishingDelay);
    }

    // The one ending, on the device where it lies now: the big sparkle at its middle, the words over it, the chime; and the
    // word to the room (OnFixed), once.
    void Finish(JobBase item, float delay)
    {
        if (item == null) return;
        FixedMoment(Centre(item.gameObject), Above(item.gameObject), item.Grade, delay);
        try { OnFixed?.Invoke(item); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    // Just over the device as the camera sees it (the middle of its top edge on the screen), so the words never lie on the
    // glass that has just lit up or been wiped. Without a camera, its middle.
    Vector3 Above(GameObject go)
    {
        Vector3 centre = Centre(go);
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null || !RendererBounds(go, out Bounds b)) return centre;
        Vector3 c = cam.WorldToScreenPoint(centre);
        if (c.z <= 0f) return centre;
        float top = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 s = cam.WorldToScreenPoint(corner);
            if (s.z > 0f) top = Mathf.Max(top, s.y);
        }
        if (float.IsInfinity(top)) return centre;
        return cam.ScreenToWorldPoint(new Vector3(c.x, top, c.z));
    }

    // ================================================================== placing and moving them

    // Runs just before the canvases are drawn, after every camera has moved this frame (as the barks do).
    void Place()
    {
        if (canvas == null || hidden) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        float now = UiClock.Now;
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        // The lasting badges: up with the same spring as a pop, then a slow bob; out of the way while Ace is busy.
        bool quiet = MarksQuiet();
        foreach (Marker m in markers)
        {
            if (!m.active) continue;
            Vector3 s = cam != null ? cam.WorldToScreenPoint(m.point) : new Vector3(0f, 0f, -1f);
            if (quiet || s.z < 0f) { m.group.alpha = 0f; continue; }
            float t = now - m.born;
            float grow = Spring(t);
            m.root.localScale = new Vector3(grow, grow, 1f);
            m.group.alpha = t < .1f ? t / .1f : 1f;
            m.root.anchoredPosition = new Vector2(s.x / scale, s.y / scale + 5f * Mathf.Sin(t * 2.6f));
        }
        foreach (Pop p in pops)
        {
            if (!p.active) { if (p.root.gameObject.activeSelf) p.root.gameObject.SetActive(false); continue; }
            float t = now - p.born;
            if (t < 0f) { p.group.alpha = 0f; continue; }   // not yet: a pop timed to come a beat after another
            if (t >= p.life || p.pinned && p.who == null) { p.active = false; p.root.gameObject.SetActive(false); continue; }
            if (cam == null) { p.group.alpha = 0f; continue; }
            Vector3 world = p.who != null ? Barks.HeadPointOf(p.who) : p.point;
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z < 0f) { p.group.alpha = 0f; continue; }
            float k = t / p.life;
            // Up on a spring (past full size, a touch under, and settling), then drifting up, fading at the end.
            float grow = Spring(t);
            float up = Rise * (1f - (1f - k) * (1f - k));
            p.root.localScale = new Vector3(grow, grow, 1f);
            p.group.alpha = t < .08f ? t / .08f : k > .72f ? Mathf.Clamp01((1f - k) / .28f) : 1f;
            Vector2 at = new Vector2(s.x / scale, s.y / scale) + p.offset + new Vector2(0f, up);
            if (p.clamp)
            {
                // Words stay on the screen whole (the device zoomed in to fill it put its top edge, and the words over it,
                // off the top): kept 12 reference pixels inside each edge (the pivot is the bottom middle).
                Vector2 area = canvasRect.rect.size, size = p.root.sizeDelta;
                at.x = Mathf.Clamp(at.x, size.x * .5f + 12f, Mathf.Max(size.x * .5f + 12f, area.x - size.x * .5f - 12f));
                at.y = Mathf.Clamp(at.y, 12f, Mathf.Max(12f, area.y - size.y - 12f));
            }
            p.root.anchoredPosition = at;
        }
        foreach (Spark sp in sparks)
        {
            if (!sp.active) continue;
            float t = now - sp.born;
            if (t >= sp.life || cam == null) { sp.active = false; sp.root.gameObject.SetActive(false); continue; }
            if (t < 0f) { sp.image.enabled = false; continue; }   // a burst's beat before it goes
            Vector3 s = cam.WorldToScreenPoint(sp.point);
            if (s.z < 0f) { sp.image.enabled = false; continue; }
            sp.image.enabled = true;
            float k = t / sp.life;
            // Out fast and slowing, a little fall (a spark) or drift up (dust), shrinking to nothing (a spark) or fading (dust).
            Vector2 travelled = sp.velocity * (t - .5f * t * t / sp.life) + new Vector2(0f, sp.gravity * t * t);
            sp.root.anchoredPosition = new Vector2(s.x / scale, s.y / scale) + sp.offset + travelled;
            float size = sp.soft ? sp.size * (1f + .8f * k) : sp.size * (k < .15f ? k / .15f : 1f - (k - .15f) / .85f);
            sp.root.sizeDelta = new Vector2(size, size);
            sp.root.localRotation = Quaternion.Euler(0f, 0f, sp.spin * t);
            if (sp.soft) { Color c = sp.image.color; c.a = .75f * (1f - k) * (1f - k); sp.image.color = c; }
        }
    }

    // A damped spring from nothing to full size: past it (about 1.2 at 0.18 s), a touch under (0.96), and settled by
    // about half a second.
    static float Spring(float t) => t <= 0f ? 0f : t >= .6f ? 1f : 1f - Mathf.Exp(-9f * t) * Mathf.Cos(17f * t);

    // ================================================================== handing over

    // A copy of what's drawn of <item> (its meshes and their materials, where they are now), without anything that does
    // anything: no colliders, no scripts, no shadows.
    static GameObject Ghost(GameObject item)
    {
        MeshRenderer[] renderers = item.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return null;
        Vector3 centre = Centre(item);
        var ghost = new GameObject("Handed over (" + item.name + ")");
        ghost.transform.position = centre;
        int parts = 0;
        foreach (MeshRenderer r in renderers)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null) continue;
            var part = new GameObject(r.name);
            part.transform.SetParent(ghost.transform, false);
            part.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            part.transform.localScale = r.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
            MeshRenderer copy = part.AddComponent<MeshRenderer>();
            copy.sharedMaterials = r.sharedMaterials;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            parts++;
        }
        if (parts == 0) { Destroy(ghost); return null; }
        return ghost;
    }

    IEnumerator Fly(GameObject ghost, Transform to, float seconds)
    {
        Transform g = ghost.transform;
        Vector3 from = g.position;
        Quaternion turn = g.rotation;
        Vector3 size = g.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(.1f, seconds))
        {
            if (ghost == null) yield break;
            Vector3 hands = to != null ? Barks.HeadPointOf(to) + Vector3.down * .62f : from;
            float e = 1f - (1f - t) * (1f - t);
            g.position = Vector3.Lerp(from, hands, e) + Vector3.up * (.32f * 4f * t * (1f - t));
            g.rotation = turn * Quaternion.Euler(0f, 70f * e, 0f);
            float pop = t < .75f ? 1f : Mathf.Lerp(1f, 0f, (t - .75f) / .25f);
            g.localScale = size * pop;
            yield return null;
        }
        if (ghost != null)
        {
            if (to != null) Sparkle(Barks.HeadPointOf(to) + Vector3.down * .62f, false);
            Destroy(ghost);
        }
    }

    static Vector3 Centre(GameObject go) => RendererBounds(go, out Bounds b) ? b.center : go.transform.position;

    // What's drawn of a thing (its own solid parts: not its job tag, sprites, lines or particles; DeviceBounds), as one
    // world box. False: nothing is drawn. (12 Oct: with the phone's stray tag counted, "Fixed!" landed about 30 cm off it.)
    static bool RendererBounds(GameObject go, out Bounds b) => DeviceBounds.TryGet(go, out b);

    // ================================================================== the icons, drawn in code

    static Sprite[] Sprites => sprites ??= DrawIcons();
    static Sprite SparkSprite => sparkSprite != null ? sparkSprite : sparkSprite = DrawSpark();
    static Sprite DustSprite => dustSprite != null ? dustSprite : dustSprite = DrawDust();

    const int Px = 128;
    static readonly Color Paper = UiSkin.Paper;

    static Sprite[] DrawIcons()
    {
        var all = new Sprite[9];
        all[(int)Icon.Heart] = Badge(p => Heart(p), HeartColour);
        all[(int)Icon.Star] = Badge(p => Star(p, 5, .34f, .15f), StarColour);
        all[(int)Icon.Cup] = Badge(Cup, new Color(.55f, .37f, .24f));
        all[(int)Icon.Tick] = Badge(Tick, UiSkin.Brand);
        all[(int)Icon.Dots] = Badge(Dots, new Color(.52f, .55f, .6f));
        all[(int)Icon.Cloud] = Badge(Cloud, new Color(.55f, .6f, .68f));
        all[(int)Icon.Drop] = Badge(Drop, new Color(.35f, .64f, .91f));
        all[(int)Icon.Burst] = Badge(p => Star(p, 9, .36f, .22f), UiSkin.Red);
        all[(int)Icon.Mess] = Badge(Mess, new Color(.47f, .43f, .26f));
        return all;
    }

    // A round paper badge with a rim in its own colour (darker), the symbol inside in its colour, drawn a touch bolder
    // (dilated), and a soft light across the paper's top. Distances in the badge's own units: its radius is .5 (the
    // texture spans -.5 to .5).
    static Sprite Badge(System.Func<Vector2, float> symbol, Color colour)
    {
        var tex = new Texture2D(Px, Px, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "Juice badge",
        };
        var pixels = new Color[Px * Px];
        float aa = 1.2f / Px;
        Color rim = Color.Lerp(colour, Color.black, .18f);
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                var p = new Vector2((x + .5f) / Px - .5f, (y + .5f) / Px - .5f);
                float disc = p.magnitude - .45f;
                float shadow = (p + new Vector2(0f, .03f)).magnitude - .455f;
                Color c = new Color(0f, 0f, 0f, .3f * Mathf.Clamp01(.5f - shadow / (aa * 5f)));
                c = Over(c, rim, Mathf.Clamp01(.5f - disc / aa));
                Color paper = Color.Lerp(Paper, Color.white, Mathf.Clamp01(p.y * 2.2f));
                c = Over(c, paper, Mathf.Clamp01(.5f - (disc + .062f) / aa));
                c = Over(c, colour, Mathf.Clamp01(.5f - (symbol(p) - .016f) / aa));
                pixels[y * Px + x] = c;
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, Px, Px), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static Sprite DrawSpark()
    {
        var tex = new Texture2D(48, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixels = new Color[48 * 48];
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                var p = new Vector2((x + .5f) / 48f - .5f, (y + .5f) / 48f - .5f);
                float d = Star(p, 4, .48f, .1f);
                float glow = Mathf.Clamp01(1f - p.magnitude / .3f) * .35f;
                pixels[y * 48 + x] = new Color(1f, 1f, 1f, Mathf.Max(Mathf.Clamp01(.5f - d / (1.5f / 48f)), glow));
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // A soft round mote: solid in the middle, feathering out to nothing.
    static Sprite DrawDust()
    {
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                var p = new Vector2((x + .5f) / 32f - .5f, (y + .5f) / 32f - .5f);
                float d = p.magnitude / .5f;
                float a = Mathf.Clamp01(1f - d);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static Color Over(Color under, Color over, float a)
    {
        if (a <= 0f) return under;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color c = (over * a + under * under.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    // ---- the symbols, as signed distances (negative inside), in the badge's units

    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    // A heart (Inigo Quilez's exact heart), scaled into the badge.
    static float Heart(Vector2 p)
    {
        const float s = .36f;
        float x = Mathf.Abs(p.x) / s, y = (p.y + .2f) / s;
        float d;
        if (y + x > 1f) d = Mathf.Sqrt((x - .25f) * (x - .25f) + (y - .75f) * (y - .75f)) - Mathf.Sqrt(2f) / 4f;
        else
        {
            float m = Mathf.Max(x + y, 0f) * .5f;
            d = Mathf.Sqrt(Mathf.Min(x * x + (y - 1f) * (y - 1f), (x - m) * (x - m) + (y - m) * (y - m))) * (x - y > 0f ? 1f : -1f);
        }
        return d * s;
    }

    // A star of <points> points, one straight up, between radius <outer> (its points) and <inner> (between them).
    static float Star(Vector2 p, int points, float outer, float inner)
    {
        float an = Mathf.PI / points;
        float bn = Mathf.Repeat(Mathf.Atan2(p.x, p.y) + an, 2f * an) - an;
        float l = p.magnitude;
        var q = new Vector2(l * Mathf.Cos(bn), l * Mathf.Abs(Mathf.Sin(bn)));
        var tip = new Vector2(outer, 0f);
        var notch = new Vector2(inner * Mathf.Cos(an), inner * Mathf.Sin(an));
        Vector2 pa = q - tip, ba = notch - tip;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        float d = (pa - ba * h).magnitude;
        return ba.x * pa.y - ba.y * pa.x > 0f ? -d : d;
    }

    // A cup with its handle, three wisps of steam over it.
    static float Cup(Vector2 p)
    {
        float body = RoundBox(p, new Vector2(-.03f, -.1f), new Vector2(.15f, .12f), .045f);
        float handle = Mathf.Abs(Circle(p, new Vector2(.14f, -.08f), .065f)) - .025f;
        float steam = float.MaxValue;
        for (int i = -1; i <= 1; i++)
        {
            float x = -.03f + i * .085f;
            steam = Mathf.Min(steam, Segment(p, new Vector2(x - .015f, .07f), new Vector2(x + .015f, .2f), .022f));
        }
        return Mathf.Min(Mathf.Min(body, handle), steam);
    }

    // A used cup, no steam, standing in what it spilled, and a drop beside it: his mess on a table. A muddier colour than
    // the order's cup, and no steam, so the two don't read alike.
    static float Mess(Vector2 p)
    {
        float body = RoundBox(p, new Vector2(-.05f, -.01f), new Vector2(.12f, .11f), .04f);
        float handle = Mathf.Abs(Circle(p, new Vector2(.08f, 0f), .055f)) - .022f;
        float spill = RoundBox(p, new Vector2(.02f, -.15f), new Vector2(.22f, .035f), .035f);
        float drop = Circle(p, new Vector2(.2f, -.05f), .03f);
        return Mathf.Min(Mathf.Min(body, handle), Mathf.Min(spill, drop));
    }

    static float Tick(Vector2 p) =>
        Mathf.Min(Segment(p, new Vector2(-.17f, 0f), new Vector2(-.05f, -.13f), .045f),
                  Segment(p, new Vector2(-.05f, -.13f), new Vector2(.2f, .15f), .045f));

    static float Dots(Vector2 p)
    {
        float d = float.MaxValue;
        for (int i = -1; i <= 1; i++) d = Mathf.Min(d, Circle(p, new Vector2(i * .13f, -.02f), .052f));
        return d;
    }

    static float Cloud(Vector2 p)
    {
        float d = Circle(p, new Vector2(-.12f, -.03f), .1f);
        d = Mathf.Min(d, Circle(p, new Vector2(.02f, .04f), .13f));
        d = Mathf.Min(d, Circle(p, new Vector2(.15f, -.03f), .1f));
        d = Mathf.Min(d, RoundBox(p, new Vector2(.01f, -.08f), new Vector2(.22f, .06f), .06f));
        return d;
    }

    // A falling drop: a circle with a point on top.
    static float Drop(Vector2 p)
    {
        float round = Circle(p, new Vector2(0f, -.07f), .14f);
        Vector2 q = new Vector2(Mathf.Abs(p.x), p.y);
        float cone = Mathf.Max(Vector2.Dot(q - new Vector2(0f, .23f), new Vector2(.9f, .44f).normalized), -(p.y + .07f));
        return Mathf.Min(round, cone);
    }

}
