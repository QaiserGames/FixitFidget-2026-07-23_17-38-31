using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE NIGHT AFTER THE DAY, AND THE MORNING AFTER THE NIGHT (the Night 1 slice; the night walk's
// ending, claude/night-city-proposal.md §3.5)
//
//   * After the recap, its button reads "Close up for the night" (RecapUI): the screen goes dark,
//     the café is emptied, and the night walk begins where Ace stands (NightWalk).
//   * Ace calls it a night just inside the café's door (E there, or A / Cross: NightDoorway) once Ace has
//     been out (never on the way out), or the night ends by itself at dawn (4 AM on the night's clock)
//     and Ace hurries home.
//   * The screen goes dark again, the night is put away (NightWalk.End), Ace is back where the day
//     starts, and the recap's own Open Tomorrow runs (RecapUI.ContinueAfterNight): the next morning is
//     saved with what the night did (NightLedger), and opens.
//
// Nothing is saved during a night: quitting mid-night comes back to the recap, and the night again.
// Nightfall and the morning are guarded (Safely): an error there is logged, and the screen never stays
// dark. A night that couldn't begin goes straight on to tomorrow; if tomorrow can't open, the recap
// comes back (its button then opens tomorrow).
// The night can be switched off on the NightWalk (Night Follows The Day); a scene without a NightWalk
// goes straight on to the next day, as before. A night started some other way (the editor's night
// walk lab) is taken over too: the doorway and dawn work there, and ending it lets that day run.
//
// Made while playing (never saved in the scene); it also draws the fades, the captions and the
// night's short notes ("Barnaby is coming home with Ace").
//
// The man at the bins (6 Oct 2026): at nightfall he's at the bins behind the café once Ace has met him, or, the
// first time, hidden in the dumpster while Night 0 opens the night at the café's back door (Lodger, NightZero).
// Until the deal is made, Ace can't call it a night. The back door is a blink (Through): a dip to black, and Ace
// is on its other side.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightCycle : MonoBehaviour
{
    public enum Phase { Day, Dusk, Night, Dawn }

    /// <summary>Just inside the café's door (it opens onto the front street at about x 0.1, z 0).</summary>
    public static readonly Vector3 DoorwayCentre = new Vector3(.12f, 1f, 1.4f);
    public static readonly Vector3 DoorwaySize = new Vector3(2.8f, 2.2f, 2f);

    public static NightCycle Instance { get; private set; }

    // Where Ace starts the day: the scene's own start, remembered as it loads. Ace goes home there.
    static Vector3 homePosition;
    static Quaternion homeRotation;
    static bool haveHome;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        haveHome = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        // What an earlier Play session left behind. Until 29 Sept the cycle and its doorway were marked
        // DontSave, and in the editor such an object outlives its Play session: a night stopped with its note
        // on screen kept that note in the Game view in Edit Mode, and over the next session's recap. Put away
        // before this session's cycle is made.
        foreach (NightCycle old in Resources.FindObjectsOfTypeAll<NightCycle>())
            if (old != null && old != Instance) Destroy(old.gameObject);
        foreach (NightDoorway old in Resources.FindObjectsOfTypeAll<NightDoorway>())
            if (old != null) Destroy(old.gameObject);

        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        haveHome = ace != null;
        if (haveHome)
        {
            homePosition = ace.transform.position;
            homeRotation = ace.transform.rotation;
        }
        // Ready for any night in this scene, including one the editor's lab starts at load.
        if (FindAnyObjectByType<NightWalk>() != null) Ensure();
    }

    public Phase Now { get; private set; } = Phase.Day;
    public bool Running => Now != Phase.Day;
    /// <summary>
    /// Ace can call it a night (at the café's door): the night is on, a moment has passed since it began
    /// (the press that closed the recap mustn't also end it), and Ace has been out since.
    /// </summary>
    public bool CanCallItANight => Now == Phase.Night && Time.unscaledTime >= readyAt && beenOut && !NightZero.Pending && !blinking;

    /// <summary>
    /// "Call it a night", and what happens to the thing in Ace's hand if Ace does: the man's errand, still to give him,
    /// goes on Ace's shelf for now ("Call it a night (Barnaby goes on the shelf)").
    /// </summary>
    public static string CallItANightPrompt
    {
        get
        {
            NightCarry carry = NightCarry.Current;
            NightThing thing = carry != null && carry.Holding ? NightThings.Find(carry.HeldId) : null;
            if (thing == null) return "Call it a night";
            Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
            string name = notebook != null && notebook.Knows(thing.id) ? thing.name : thing.unknownName;
            // Read every frame while Ace is at a door: made again only when the name changes.
            if (!ReferenceEquals(name, shelfPromptFor)) { shelfPromptFor = name; shelfPrompt = $"Call it a night ({name} goes on the shelf)"; }
            return shelfPrompt;
        }
    }
    static string shelfPromptFor, shelfPrompt = "";
    /// <summary>Ace has left the café since this night began (reports and checks).</summary>
    public bool BeenOut => beenOut;
    /// <summary>Nights begun in this Play session, and how the last one ended (reports).</summary>
    public int NightsThisSession { get; private set; }
    public string LastEnding { get; private set; } = "";
    public bool HasDoorway => doorway != null;

    RecapUI recap;
    NightDoorway doorway;
    CafeViewMode view;
    float readyAt;
    bool ending;
    bool beenOut;
    bool blinking;

    CanvasGroup curtain;
    TMP_Text title, subtitle, note;
    RectTransform noteBox;
    float noteUntil;

    /// <summary>
    /// True when the recap's button should lead into a night: a night walk is in the scene, switched
    /// on for after the day (Night Follows The Day), and not already running.
    /// </summary>
    public static bool FollowsTheDay
    {
        get
        {
            NightWalk walk = NightWalk.Instance;
            return walk != null && walk.isActiveAndEnabled && walk.followsTheDay && !walk.Active;
        }
    }

    public static NightCycle Ensure()
    {
        if (Instance != null) return Instance;
        // An ordinary object of the scene, so it ends with the Play session (see OnSceneLoaded).
        var go = new GameObject("Night cycle (while playing)");
        return go.AddComponent<NightCycle>();
    }

    /// <summary>A short line on screen for a few seconds (at night: "Barnaby is coming home with Ace").</summary>
    public static void Note(string text, float seconds = 4.5f)
    {
        if (string.IsNullOrWhiteSpace(text) || !Application.isPlaying) return;
        Ensure().ShowNote(text, seconds);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildScreen();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- nightfall ----------

    /// <summary>
    /// The recap's button: the night begins. False when there's no night to walk (the recap then goes
    /// straight on to tomorrow, as before).
    /// </summary>
    public static bool BeginAfterRecap(RecapUI from)
    {
        if (!FollowsTheDay || DayClock.Instance == null || !DayClock.Instance.DayOver) return false;
        NightCycle cycle = Ensure();
        if (cycle.Running) return false;
        cycle.recap = from;
        cycle.StartCoroutine(cycle.Dusk());
        return true;
    }

    IEnumerator Dusk()
    {
        Now = Phase.Dusk;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        yield return Fade(1f, .6f);
        // Night 0 (the first night, until the man at the bins is met) opens with one last job instead.
        bool zero = NightZero.Due;
        Caption($"Night {day}", zero ? "The café is closed. One last job: the bins, out the back."
            : "The café is closed. Walk where you like, and come back in through the café's door to call it a night.");
        if (!Safely("nightfall", Nightfall))
        {
            // No night, then: put away whatever of it began, and on to tomorrow.
            LastEnding = "could not begin (see the Console)";
            Safely("putting the night away", PutTheNightAway);
            yield return Tomorrow();
            yield break;
        }
        // The man at the bins: hidden in the dumpster on Night 0, which opens the night at the back door; standing in
        // it once met.
        Safely("the man at the bins", () =>
        {
            Lodger man = Lodger.Expected(zero) ? Lodger.Arrive(hidden: zero) : null;
            if (zero && NightZero.Begin(man) == null) Debug.LogWarning("[Night] Night 0 couldn't begin; the night goes on without it.");
        });
        yield return new WaitForSecondsRealtime(1.4f);
        BeginNight();
        yield return Fade(0f, .9f);
        Caption("", "");
        bool first = SaveManager.Instance == null || SaveManager.Instance.Night.Nights == 0;
        if (NightZero.Pending) Note("Last job of the day: the bins. Out the back door.", 6f);
        else if (first)
            Note($"What Ace learned today is in the notebook ({ControlHints.NotebookPage}). " +
                 $"{ControlHints.Torch} is the torch. Back inside the café's door, {ControlHints.Interact} calls it a night.", 9f);
    }

    static void Nightfall()
    {
        if (DayClock.Instance != null) DayClock.Instance.ClearTheShop();
        Time.timeScale = 1f;
        NightWalk walk = NightWalk.Instance;
        walk.QuietTheStreet();
        walk.Begin();
    }

    // The night is on: the doorway to call it a night, a moment before it can be used (the button
    // that closed the recap mustn't also end the night), and only once Ace has been out (Update).
    void BeginNight()
    {
        Now = Phase.Night;
        readyAt = Time.unscaledTime + 1f;
        beenOut = false;
        NightsThisSession++;
        MakeTheDoorway();
    }

    void Update()
    {
        NightWalk walk = NightWalk.Instance;
        if (Now == Phase.Day && walk != null && walk.Active)
        {
            // A night begun some other way (the editor's night walk lab): take it over.
            recap = null;
            BeginNight();
        }
        else if (Now == Phase.Night)
        {
            if (walk == null || !walk.Active)
            {
                // Ended some other way (a check, leaving Play Mode).
                RemoveTheDoorway();
                Now = Phase.Day;
            }
            else
            {
                // The café's door calls it a night only on the way back in, never on the way out.
                if (!beenOut)
                {
                    if (view == null) view = FindAnyObjectByType<CafeViewMode>();
                    beenOut = view == null || !view.AceInsideCafe;
                }
                if (walk.Hour >= walk.nightEndsAt - .001f) EndTheNight(true);
            }
        }
        if (note != null && noteBox.gameObject.activeSelf && Time.unscaledTime >= noteUntil) noteBox.gameObject.SetActive(false);
    }

    // ---------- the morning ----------

    /// <summary>Ace calls it a night (NightDoorway, just inside the café's door). False when it can't be done now.</summary>
    public bool CallItANight()
    {
        if (!CanCallItANight) return false;
        EndTheNight(false);
        return true;
    }

    void EndTheNight(bool dawn)
    {
        if (ending) return;
        ending = true;
        StartCoroutine(Morning(dawn));
    }

    IEnumerator Morning(bool dawn)
    {
        Now = Phase.Dawn;
        LastEnding = dawn ? "dawn" : "called it a night";
        // A note from the night ("Barnaby is coming home with Ace") doesn't stay on over the morning.
        HideNote();
        Safely("the night's last sound", () => Sfx.Play2D(dawn ? "night.dawn" : "night.home"));
        yield return Fade(1f, dawn ? 1.4f : .7f);
        Caption(dawn ? "Dawn" : "Home",
            dawn ? "The sky pales, and Ace hurries home before the street wakes." : "Ace calls it a night.");
        Safely("putting the night away", PutTheNightAway);
        Safely("the night's record", () => { if (SaveManager.Instance != null) SaveManager.Instance.Night.CameHome(); });
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Tomorrow();
    }

    // After the night (or a night that couldn't begin): the recap's own Open Tomorrow. The morning is
    // saved with the night's doings, then opens; if it can't be saved, the recap comes back with the
    // error, and its button goes straight to tomorrow. Whatever happens, the screen comes back and the
    // cycle returns to the day.
    IEnumerator Tomorrow()
    {
        DayClock clock = DayClock.Instance;
        bool tomorrow = false;
        bool opened = Safely("opening tomorrow", () =>
        {
            if (clock != null && clock.DayOver)
                tomorrow = recap != null ? recap.ContinueAfterNight() : clock.TryNextDay();
        });
        // An error opening tomorrow: the recap again, rather than a closed day with no way on.
        if (!opened && clock != null && clock.DayOver && recap != null)
            Safely("showing the recap again", recap.ShowAgain);
        if (tomorrow) Caption($"Day {clock.Day}", "Morning");
        yield return new WaitForSecondsRealtime(tomorrow ? 1.3f : .2f);
        yield return Fade(0f, .9f);
        Caption("", "");
        recap = null;
        Now = Phase.Day;
        ending = false;
    }

    void PutTheNightAway()
    {
        RemoveTheDoorway();
        NightWalk walk = NightWalk.Instance;
        if (walk != null) walk.End();
        GoHome();
    }

    // One step of nightfall or the morning. An error is logged and the rest carries on: a coroutine
    // that throws stops where it is, and the screen would stay dark for good.
    static bool Safely(string what, System.Action step)
    {
        try
        {
            step?.Invoke();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Night] Something went wrong with {what}; carrying on. {e.GetType().Name}: {e.Message}");
            Debug.LogException(e);
            return false;
        }
    }

    // Back where the day starts (behind the counter), the way PlayerInteractor moves Ace: with the
    // CharacterController off for the jump.
    static void GoHome()
    {
        if (haveHome) Put(homePosition, homeRotation);
    }

    /// <summary>Ace, put at <paramref name="spot"/> and facing its way (the body too, at once): the back door, Night 0's start.</summary>
    public static void Put(Transform spot)
    {
        if (spot != null) Put(spot.position, Quaternion.Euler(0f, spot.eulerAngles.y, 0f));
    }

    static void Put(Vector3 position, Quaternion rotation)
    {
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace == null) return;
        var controller = ace.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        ace.transform.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = was;
        ace.ClearInput();
        AceBody body = ace.GetComponent<AceBody>();
        if (body != null) body.FaceToward(position + rotation * Vector3.forward, snap: true);
    }

    /// <summary>
    /// Through a door at night (the café's back door): a quick dip to black, and Ace is at <paramref name="to"/>, facing
    /// its way. False while another is under way, or with no night on.
    /// </summary>
    public static bool Through(Transform to)
    {
        if (to == null || Instance == null || Instance.Now != Phase.Night || Instance.blinking || Instance.ending) return false;
        Instance.StartCoroutine(Instance.Blink(to));
        return true;
    }

    IEnumerator Blink(Transform to)
    {
        blinking = true;
        PlayerMovement.Hold(this);
        Safely("the door's sound", () => Sfx.Play2D("night.door"));
        yield return Fade(1f, .16f);
        Safely("going through the door", () => Put(to));
        yield return new WaitForSecondsRealtime(.08f);
        yield return Fade(0f, .24f);
        PlayerMovement.Release(this);
        blinking = false;
    }

    void MakeTheDoorway()
    {
        if (doorway != null) return;
        var go = new GameObject("Call it a night (while the night runs)");
        go.transform.position = DoorwayCentre;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = DoorwaySize;
        doorway = go.AddComponent<NightDoorway>();
    }

    void RemoveTheDoorway()
    {
        if (doorway != null) Destroy(doorway.gameObject);
        doorway = null;
    }

    // ---------- the screen: a fade, a caption, a note ----------

    IEnumerator Fade(float to, float seconds)
    {
        float from = curtain.alpha;
        curtain.blocksRaycasts = to > .5f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            curtain.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        curtain.alpha = to;
    }

    void Caption(string big, string small)
    {
        if (title != null) title.text = big;
        if (subtitle != null) subtitle.text = small;
    }

    void ShowNote(string text, float seconds)
    {
        if (note == null) return;
        note.text = text;
        noteBox.gameObject.SetActive(true);
        noteUntil = Time.unscaledTime + Mathf.Max(1f, seconds);
    }

    void HideNote()
    {
        if (noteBox != null) noteBox.gameObject.SetActive(false);
    }

    void BuildScreen()
    {
        var canvasObject = new GameObject("Night cycle screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        TMP_FontAsset font = HudFont();

        RectTransform curtainRect = Stretch("Curtain", canvasObject.transform);
        curtain = curtainRect.gameObject.AddComponent<CanvasGroup>();
        curtain.alpha = 0f;
        curtain.blocksRaycasts = false;
        curtain.interactable = false;
        var black = curtainRect.gameObject.AddComponent<Image>();
        black.color = new Color(.02f, .02f, .035f, 1f);
        title = Label("Title", curtainRect, new Vector2(0f, 40f), new Vector2(1400f, 110f), 76f, new Color(.95f, .92f, .85f), font);
        subtitle = Label("Subtitle", curtainRect, new Vector2(0f, -40f), new Vector2(1300f, 90f), 30f, new Color(.72f, .7f, .66f), font);

        // Above the prompt ("[E]  Take Barnaby", "[F]  Serve at counter": bottom middle, about 180-220 up),
        // so a note never sits on it: by day too, when Grace's print is noted as her conversation closes.
        noteBox = new GameObject("Note", typeof(RectTransform)).GetComponent<RectTransform>();
        noteBox.SetParent(canvasObject.transform, false);
        noteBox.anchorMin = noteBox.anchorMax = new Vector2(.5f, 0f);
        noteBox.pivot = new Vector2(.5f, 0f);
        noteBox.anchoredPosition = new Vector2(0f, 250f);
        noteBox.sizeDelta = new Vector2(1180f, 64f);
        var backing = noteBox.gameObject.AddComponent<Image>();
        backing.color = new Color(.075f, .07f, .065f, .86f);
        backing.raycastTarget = false;
        note = Label("Text", noteBox, Vector2.zero, new Vector2(1140f, 60f), 25f, new Color(.95f, .92f, .85f), font);
        noteBox.gameObject.SetActive(false);
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color colour, TMP_FontAsset font)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    // The HUD's own lettering, so the screen looks like part of the game (as NightNotebook does).
    static TMP_FontAsset HudFont()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        if (any == null) any = FindAnyObjectByType<TextMeshProUGUI>();
        return any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
    }

    public string Describe() =>
        $"Night cycle: {Now}; {NightsThisSession} night(s) this session, the last one ended: {(LastEnding.Length > 0 ? LastEnding : "not yet")}; " +
        $"call it a night {(HasDoorway ? "inside the café's door" + (beenOut ? "" : " (once Ace has been out)") : "not offered")}; follows the day: {FollowsTheDay}." +
        (NightZero.Instance != null ? " " + NightZero.Instance.Describe() : "") + (Lodger.Instance != null ? " " + Lodger.Instance.Describe() : "");
}
