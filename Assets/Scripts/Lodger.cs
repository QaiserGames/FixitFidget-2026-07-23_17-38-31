using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS (6 Oct 2026; claude/the-man-at-the-bins-story.md, all four of Mansoor's calls)
//
// At night he lives in the dumpster behind the café (NightZeroSet). On Night 0 he is hidden in it until Ace bins the
// bag (NightZero stages that); after that he stands up in it, chest above the rim, the far lid open behind him, all
// night: that is where Ace finds him, hears him and brings him what he asked for.
//
//   * His body: the café's patron body wearing a city look nobody else wears (NightZeroSet.look), its brain removed
//     (as the night's neighbours: NightNeighbours). His lines are barks pinned to his head (Barks); his gestures are
//     the café people's Mixamo beats (NpcBeats), when the library is there.
//   * When Ace passes (within 7 m, no scene playing) he says something now and then (the Night lines' pools, throttled
//     by BarkRules): "Not yet." while his errand is out, "Over here." when Ace has the thing in hand, "Good." after.
//   * The return (the five beats' third): Ace brings the thing back (NightGive, in front of the far half) and he plays
//     the return scene; on its lines the thing leaves Ace's hand for his corner, he turns it to face the street, and
//     he pays: a lesson and a page (LodgerStory). Ace answers once, as in the deal.
//   * His corner: what Ace has brought him stands on the trash can's lid beside the dumpster, at night only.
//   * Round the back the camera looks at the bins from the street (CafeViewMode's house view, NightZeroSet.view*):
//     from the front, the café's back wall stands between the camera and the dumpster.
//
// Made at nightfall by NightCycle while a night runs, gone with it. Nothing here runs by day.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class Lodger : MonoBehaviour
{
    public static Lodger Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>Is he at the bins tonight? Once met, every night; on Night 0, hidden until the lid lifts.</summary>
    public static bool Expected(bool nightZero) =>
        NightZeroSet.Instance != null && (nightZero || SaveManager.Instance != null && SaveManager.Instance.Night.MetHim);

    /// <summary>He's at the bins for tonight: hidden in the dumpster (Night 0), or standing up in it.</summary>
    public static Lodger Arrive(bool hidden)
    {
        if (Instance != null) return Instance;
        NightZeroSet set = NightZeroSet.Instance;
        if (set == null) return null;
        var go = new GameObject("The man at the bins (while the night runs)");
        Lodger man = go.AddComponent<Lodger>();
        man.Set(set, hidden);
        return man;
    }

    /// <summary>What his lines are pinned to: his body (his head is found from it).</summary>
    public Transform Speaker => body != null ? body.transform : transform;
    /// <summary>Standing up in the dumpster (not hidden in it).</summary>
    public bool Up { get; private set; }
    /// <summary>The return scene is playing.</summary>
    public bool Returning { get; private set; }
    /// <summary>His body found its look (else he wears the patron body as it is, or nothing at all).</summary>
    public string Look => visual != null ? visual.ActiveAppearanceName : body != null ? "the patron body" : "none";
    /// <summary>Lines he has said in passing tonight (reports).</summary>
    public int Passing { get; private set; }
    public bool InBinsView => inView;

    NightZeroSet set;
    GameObject body;
    PolygonNpcVisual visual;
    NpcBeats beats;
    Transform ace;
    AceBody aceBody;
    CafeViewMode view;
    float riseFrom, riseTo, riseT = 1f, riseSeconds = .9f;
    float yaw, yawTo;
    bool inView;
    float nextLook;
    Coroutine turningGnome;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (inView && view != null) view.ExitHouseView();
        if (set != null)
        {
            if (set.cornerGnome != null) set.cornerGnome.SetActive(false);
            set.SetLid(set.farLid, 0f);
            set.SetLid(set.nearLid, 0f);
        }
        if (Returning) PlayerMovement.Release(this);
    }

    void Set(NightZeroSet bins, bool hidden)
    {
        set = bins;
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        ace = player != null ? player.transform : null;
        aceBody = player != null ? player.GetComponent<AceBody>() : null;
        view = player != null ? player.GetComponent<CafeViewMode>() : null;
        MakeBody();
        yaw = yawTo = set.inside != null ? set.inside.eulerAngles.y : 0f;
        if (hidden) Hide();
        else StandUp(instantly: true);
        ShowCorner(turned: true);
    }

    // ---------- his body ----------

    // The café's patron body, made inside a switched-off holder so nothing of the café's wakes up in it (no brain, no
    // navigation agent off the navigation mesh), in his own look.
    void MakeBody()
    {
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        GameObject prefab = spawner != null ? spawner.PatronPrefab : null;
        if (prefab == null) { Debug.LogWarning("[The man at the bins] No patron prefab to make his body from: he speaks, but nobody is seen."); return; }
        var holder = new GameObject("(the man, getting ready)");
        holder.SetActive(false);
        body = Instantiate(prefab, holder.transform);
        body.name = "The man at the bins (placeholder look)";
        foreach (NavMeshAgent agent in body.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        CustomerBrain customer = body.GetComponent<CustomerBrain>();
        PatronBrain patron = body.GetComponent<PatronBrain>();
        if (customer != null) customer.enabled = false;
        if (patron != null) patron.enabled = false;
        // The patron's own walk-in outfits (NpcVisualVariants) would switch one on again over his look when it starts:
        // off before he wakes, gone after.
        NpcVisualVariants outfits = body.GetComponent<NpcVisualVariants>();
        if (outfits != null) outfits.enabled = false;
        visual = body.GetComponent<PolygonNpcVisual>();
        if (visual != null && set.look != null) visual.Configure(new[] { set.look }, 0f, 0, 0f);
        body.transform.SetParent(transform, false);
        Destroy(holder);
        if (customer != null) Destroy(customer);
        if (patron != null) Destroy(patron);
        if (outfits != null) Destroy(outfits);
        foreach (Interactable talk in body.GetComponents<Interactable>()) Destroy(talk);
        foreach (PatienceBar bar in body.GetComponentsInChildren<PatienceBar>(true)) bar.gameObject.SetActive(false);
        foreach (Transform child in body.transform)
            if (child.name == "SpeechBubble") child.gameObject.SetActive(false);
        Animator animator = body.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        beats = body.GetComponent<NpcBeats>();
    }

    // Out of sight: well below the street, where nothing can show him (the lid is shut, the dumpster's empty half too).
    void Hide()
    {
        Up = false;
        riseT = 1f;
        if (body != null && set.inside != null) body.transform.SetPositionAndRotation(set.inside.position + Vector3.down * 3f, Quaternion.Euler(0f, yaw, 0f));
        set.SetLid(set.farLid, 0f);
    }

    /// <summary>He stands up in the dumpster, under the far lid (Night 0's moment; NightZero opens the lid as he does).</summary>
    public void StandUp(bool instantly = false, float seconds = .9f)
    {
        Up = true;
        if (set.inside == null) return;
        riseTo = set.inside.position.y;
        if (instantly || body == null)
        {
            riseT = 1f;
            if (body != null) body.transform.position = set.inside.position;
            set.SetLid(set.farLid, 1f);
            return;
        }
        // From crouched in the bin, his head under the rim.
        riseFrom = riseTo - 1f;
        body.transform.position = new Vector3(set.inside.position.x, riseFrom, set.inside.position.z);
        riseSeconds = Mathf.Max(.1f, seconds);
        riseT = 0f;
    }

    /// <summary>He turns to face <paramref name="point"/> (unhurried).</summary>
    public void Face(Vector3 point)
    {
        if (body == null) return;
        Vector3 d = point - body.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > .01f) yawTo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    /// <summary>One of the café people's Mixamo beats ("Looking Around"), if the library is there and he holds still.</summary>
    public bool Beat(string clip, float seconds = 0f) => beats != null && beats.PlayClip(clip, seconds);

    /// <summary>Ace answered: a nod for a warm reply, a shake of the head for a cold one.</summary>
    public void Heard(int warmth)
    {
        if (warmth > 0) Beat(NpcBeats.Clip.NodYes);
        else if (warmth < 0) Beat(NpcBeats.Clip.HeadShake);
    }

    void Update()
    {
        NightWalk night = NightWalk.Instance;
        if (night == null || !night.Active) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        if (body != null)
        {
            if (riseT < 1f)
            {
                riseT = Mathf.Min(1f, riseT + dt / riseSeconds);
                float t = 1f - (1f - riseT) * (1f - riseT) * (1f - riseT);   // up quickly, settling
                Vector3 p = body.transform.position;
                body.transform.position = new Vector3(p.x, Mathf.Lerp(riseFrom, riseTo, t), p.z);
            }
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, yawTo)) > .1f)
            {
                yaw = Mathf.MoveTowardsAngle(yaw, yawTo, Mathf.Max(90f * dt, Mathf.Abs(Mathf.DeltaAngle(yaw, yawTo)) * (1f - Mathf.Exp(-6f * dt))));
                body.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
        if (Time.unscaledTime < nextLook) return;
        nextLook = Time.unscaledTime + .25f;
        BinsView();
        Passing += InPassing() ? 1 : 0;
    }

    // ---------- the camera round the back ----------

    void BinsView()
    {
        if (view == null || ace == null) return;
        Vector3 d = ace.position - set.Bins;
        d.y = 0f;
        float far = d.magnitude;
        bool round = !view.AceInsideCafe && far <= set.viewRadius;
        if (round && !inView)
        {
            inView = true;
            view.EnterHouseView(set.viewYaw, set.viewPitch, set.viewDistance);
        }
        else if (inView && (view.AceInsideCafe || far > set.viewRadius + 1.5f))
        {
            inView = false;
            view.ExitHouseView();
        }
    }

    // ---------- in passing ----------

    // A line now and then as Ace passes (the pools throttle him: a speaker waits 20 s between his own lines).
    bool InPassing()
    {
        if (!Up || body == null || ace == null || Barks.ScenePlaying || Returning || NightZero.Pending) return false;
        Vector3 d = ace.position - body.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 49f) return false;
        Face(ace.position);
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        string errand = LodgerStory.Errand(ledger);
        NightCarry carry = NightCarry.Current;
        string pool = errand.Length == 0 ? LodgerStory.Done
            : carry != null && carry.HeldId == errand ? LodgerStory.Beckon : LodgerStory.Waiting;
        bool said = Barks.SayFrom(Speaker, LodgerStory.SpeakerId, pool);
        if (said && pool == LodgerStory.Beckon) Beat(NpcBeats.Clip.Beckoning);
        return said;
    }

    // ---------- the return ----------

    /// <summary>Can Ace hand him <paramref name="thing"/> now? (It's his errand, he's up, nothing else is playing.)</summary>
    public bool CanTake(string thing)
    {
        if (!Up || Returning || Barks.ScenePlaying || string.IsNullOrEmpty(thing)) return false;
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        return ledger != null && LodgerStory.Errand(ledger) == thing && ledger.HasTrophy(thing);
    }

    /// <summary>Ace hands him <paramref name="thing"/>: the return scene. False if he can't take it now.</summary>
    public bool Receive(string thing)
    {
        if (!CanTake(thing)) return false;
        Returning = true;
        returned = thing;
        paid = false;
        PlayerMovement.Hold(this);
        if (ace != null) Face(ace.position);
        if (aceBody != null && body != null) aceBody.FaceToward(body.transform.position);
        bool playing = Barks.Play(LodgerStory.ReturnScene, Who, onDone: Returned, onLineId: OnReturnLine,
            onReply: (choice, reply) => Replied(choice, reply));
        PlayerMovement.Release(this);   // the scene holds Ace now, or nothing does
        if (!playing)
        {
            // No return scene in the Night lines (Barks 1 not run yet): what it does still happens, without the words.
            OnReturnLine(LodgerStory.TakesItLine);
            OnReturnLine(LodgerStory.TurnsItLine);
            OnReturnLine(LodgerStory.LessonLine);
            OnReturnLine(LodgerStory.PageLine);
            Returned();
        }
        return true;
    }

    string returned = "";
    bool paid;

    Transform Who(string speakerId) => speakerId == LodgerStory.SpeakerId ? Speaker : null;

    void Replied(NightLines.Choice choice, int reply)
    {
        int warmth = reply == 0 ? choice.first.warmth : choice.second.warmth;
        if (SaveManager.Instance != null) SaveManager.Instance.Night.Warm(warmth);
        Heard(warmth);
    }

    // What happens on the return's lines: he takes it (it leaves Ace's hand for his corner), turns it to face the
    // street, and pays (the lesson, then the page).
    void OnReturnLine(string id)
    {
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (ledger == null) return;
        if (id == LodgerStory.TakesItLine)
        {
            NightCarry carry = NightCarry.Current;
            if (carry != null && carry.HeldId == returned) carry.Drop();
            if (ledger.Give(returned)) Sfx.Play("lodger.takes", set.cornerGnome != null ? set.cornerGnome.transform.position : Speaker.position);
            ShowCorner(turned: false);
            TrophyShelf.RefreshAll();
        }
        else if (id == LodgerStory.TurnsItLine)
        {
            if (turningGnome != null) StopCoroutine(turningGnome);
            turningGnome = StartCoroutine(TurnTheGnome());
        }
        else if (id == LodgerStory.LessonLine)
        {
            LodgerStory.Lesson lesson = LodgerStory.FindLesson(LodgerStory.LessonFor(returned));
            if (lesson != null && ledger.Learn(lesson.id))
            {
                Sfx.Play2D("lodger.lesson");
                NightCycle.Note(lesson.learned, 6f);
            }
        }
        else if (id == LodgerStory.PageLine)
        {
            NotebookFactData page = LodgerStory.PageFor(returned);
            Notebook notebook = SaveManager.Instance.Notebook;
            if (page != null && notebook != null && notebook.Learn(page, NotebookHooks.Today))
            {
                Sfx.Play2D("notebook.page");
                NightCycle.Note($"In his pages: \"{page.text}\" ({ControlHints.NotebookPage})", 6f);
            }
            paid = true;
        }
    }

    void Returned()
    {
        // Whatever the lines did or didn't say, the errand is paid in full.
        if (!paid)
        {
            OnReturnLine(LodgerStory.TakesItLine);
            OnReturnLine(LodgerStory.LessonLine);
            OnReturnLine(LodgerStory.PageLine);
        }
        ShowCorner(turned: true);
        Returning = false;
        returned = "";
    }

    // ---------- his corner ----------

    // What Ace has brought him stands on the trash can's lid (night only). Set down facing the alley; on his line he
    // turns it to face the street.
    void ShowCorner(bool turned)
    {
        if (set.cornerGnome == null) return;
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        bool given = ledger != null && ledger.HasGiven(NightThings.GraceGnome);
        if (set.cornerGnome.activeSelf != given) set.cornerGnome.SetActive(given);
        if (given && turned && turningGnome == null) set.cornerGnome.transform.localRotation = Quaternion.identity;
        else if (given && !turned) set.cornerGnome.transform.localRotation = Quaternion.Euler(0f, -set.gnomeTurn, 0f);
    }

    IEnumerator TurnTheGnome()
    {
        Transform gnome = set.cornerGnome != null ? set.cornerGnome.transform : null;
        if (gnome == null) yield break;
        Beat(NpcBeats.Clip.Thinking);
        Quaternion from = gnome.localRotation, to = Quaternion.identity;
        for (float t = 0f; t < 1f; t += Time.deltaTime / .7f)
        {
            gnome.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        gnome.localRotation = to;
        Sfx.Play("lodger.gnome", gnome.position);
        turningGnome = null;
    }

    public string Describe() =>
        $"The man at the bins: {(body == null ? "no body" : Up ? "standing up in the dumpster" : "hidden in it")}, look {Look}, " +
        $"{(Returning ? "the return playing, " : "")}{Passing} line(s) in passing; the bins view {(inView ? "on" : "off")}.";
}
