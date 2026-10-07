using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS (6 Oct 2026; claude/the-man-at-the-bins-story.md, all four of Mansoor's calls)
//
// At night he lives in the dumpster behind the café (NightZeroSet), out of sight with its lid shut. He comes up for Ace
// (6 Oct 2026, Mansoor's playtest: "I liked it when ... his head came out when I put the trash bag inside the bin"; his
// call: "duck down, pop again"):
//   * Night 0: the reveal when the bag goes in (NightZero stages it: the lamp, the view pushing in, the lid creaking up);
//   * every night after: he pops out when the bag goes in (NightZero asks for Pop), three ways taking turns a night each
//     (a quick pop, a peek over the rim first, a slow rise), and has his say;
//   * his say done, he ducks back down and the lid drops (Duck);
//   * when Ace comes back carrying what he asked for, he pops up again as Ace gets within Near metres of his half, and
//     takes it (the return); a moment after it he's gone again; walked away from, he ducks too;
//   * walking past empty-handed, Ace hears him from inside the bin: his lines come from under the lid, which lifts a crack.
//
//   * His body: the café's patron body wearing a city look nobody else wears (NightZeroSet.look), its brain removed
//     (as the night's neighbours: NightNeighbours). His lines are barks pinned to his head (Barks), or to the lid while
//     he's in the bin; his gestures are the café people's Mixamo beats (NpcBeats), when the library is there.
//   * When Ace passes (within 7 m, no scene playing) he says something now and then (the Night lines' pools, throttled
//     by BarkRules): "Not yet." while his errand is out, "Over here." when Ace has the thing in hand, "Good." after.
//   * The return (the five beats' third): Ace brings the thing back (NightGive, in front of the far half) and he plays
//     the favour's return scene (LodgerStory.Favours); on its lines the thing leaves Ace's hand for his corner, he does
//     what it was for (turns the gnome to face the street; sets four of Grace's cups out on the crate, one by one), and
//     he pays: a lesson and a page. Ace answers once, as in the deal.
//   * His corner: what Ace has brought him, at night only: the gnome on the trash can's lid, the cups on the crate
//     beside it (NightZeroSet).
//   * Round the back the camera looks at the bins from the street (CafeViewMode's house view, NightZeroSet.view*):
//     from the front, the café's back wall stands between the camera and the dumpster.
//
// Made at nightfall by NightCycle while a night runs, gone with it. Nothing here runs by day.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class Lodger : MonoBehaviour
{
    /// <summary>How he comes up out of the dumpster on a night after the deal (they take turns, a night each).</summary>
    public enum PopStyle { Quick, Peek, Slow }

    /// <summary>Within this many metres of his half, with his errand's thing in hand, Ace brings him up.</summary>
    public const float Near = 4.5f;
    /// <summary>Up for nothing (Ace walked off without bringing it): this far away for this long, and he ducks.</summary>
    const float WalkedOff = 7f, WalkedOffSeconds = 2.5f;
    /// <summary>A moment after his say, or after the return, and he's back in the bin.</summary>
    public const float DuckAfterSay = .7f, DuckAfterReturn = 1.6f;

    public static Lodger Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>Is he at the bins tonight? Once met, every night; on Night 0 too, before the lid lifts.</summary>
    public static bool Expected(bool nightZero) =>
        NightZeroSet.Instance != null && (nightZero || SaveManager.Instance != null && SaveManager.Instance.Night.MetHim);

    /// <summary>He's at the bins for tonight: hidden in the dumpster (every night since 6 Oct), or standing up in it.</summary>
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

    /// <summary>How he pops up on a night after the deal: a quick pop on Night 1, a peek on Night 2, a slow rise on Night 3, and round again.</summary>
    public static PopStyle StyleFor(int night) => (PopStyle)(((night - 1) % 3 + 3) % 3);

    /// <summary>What his lines are pinned to: his body while he's up or on his way (his head is found from it), the lid while he's in the bin.</summary>
    public Transform Speaker => body != null && (Up || moving) ? body.transform : voice != null ? voice : transform;
    /// <summary>Standing up in the dumpster (on his way up counts), not in it.</summary>
    public bool Up { get; private set; }
    /// <summary>Out of sight in the dumpster with its lid shut (not on his way up or down).</summary>
    public bool Hidden => !Up && !moving;
    /// <summary>On his way up or down right now.</summary>
    public bool Moving => moving;
    /// <summary>The return scene is playing.</summary>
    public bool Returning { get; private set; }
    /// <summary>His body found its look (else he wears the patron body as it is, or nothing at all).</summary>
    public string Look => visual != null ? visual.ActiveAppearanceName : body != null ? "the patron body" : "none";
    /// <summary>Lines he has said in passing tonight (reports); those from inside the bin among them.</summary>
    public int Passing { get; private set; }
    public int FromTheBin { get; private set; }
    /// <summary>How he last came up, how many times tonight, and how many times he ducked back down (reports, checks).</summary>
    public PopStyle LastStyle { get; private set; }
    public int Pops { get; private set; }
    public int Ducks { get; private set; }
    /// <summary>How far Ace was from his half when he last popped up for him (the return), metres; -1 if not tonight.</summary>
    public float PoppedFor { get; private set; } = -1f;
    public bool InBinsView => inView;

    NightZeroSet set;
    GameObject body;
    PolygonNpcVisual visual;
    NpcBeats beats;
    Transform ace;
    Transform voice;
    AceBody aceBody;
    CafeViewMode view;
    float riseFrom, riseTo, riseT = 1f, riseSeconds = .9f;
    float yaw, yawTo;
    bool inView, moving;
    float nextLook, awaySince = -1f;
    Coroutine turningGnome, settingOut, motion;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (inView && view != null) view.ExitHouseView();
        if (set != null)
        {
            if (set.cornerGnome != null) set.cornerGnome.SetActive(false);
            if (set.cornerCups != null) set.cornerCups.SetActive(false);
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
        MakeVoice();
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

    // Where his lines come from while he's in the bin: over the middle of his lid. Barks pins a line with nothing drawn under
    // it 2.15 m over the point it's given, so the point sits that far under where the line should be (0.35 m over the lid).
    void MakeVoice()
    {
        var go = new GameObject("His voice (from inside the bin)");
        go.transform.SetParent(transform, false);
        voice = go.transform;
        Vector3 over = set.inside != null ? set.inside.position + Vector3.up * 1.5f : transform.position;
        Renderer lid = set.farLid != null ? set.farLid.GetComponentInChildren<Renderer>() : null;
        if (lid != null) over = new Vector3(lid.bounds.center.x, lid.bounds.max.y, lid.bounds.center.z);
        voice.position = over + Vector3.up * (.35f - 2.15f);
    }

    // Out of sight: well below the street, where nothing can show him (the lid is shut, the dumpster's empty half too).
    void Hide()
    {
        Up = false;
        moving = false;
        riseT = 1f;
        if (body != null && set.inside != null) body.transform.SetPositionAndRotation(set.inside.position + Vector3.down * 3f, Quaternion.Euler(0f, yaw, 0f));
        set.SetLid(set.farLid, 0f);
    }

    /// <summary>He stands up in the dumpster, under the far lid (Night 0's moment; NightZero opens the lid as he does).</summary>
    public void StandUp(bool instantly = false, float seconds = .9f)
    {
        StopMotion();
        Up = true;
        Pops++;
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

    /// <summary>
    /// He pops up out of the dumpster (a night after the deal, as the bag goes in; or for Ace coming back with what he asked
    /// for): <paramref name="style"/>, the lid flying up as he does. Seconds until he's standing (PopSeconds).
    /// </summary>
    public float Pop(PopStyle style)
    {
        if (set == null || set.inside == null) return 0f;
        if (Up && !moving) return 0f;
        StopMotion();
        Up = true;
        moving = true;
        LastStyle = style;
        Pops++;
        motion = StartCoroutine(Popping(style));
        return PopSeconds(style);
    }

    /// <summary>How long each way of coming up takes, seconds, from its start to him standing.</summary>
    public static float PopSeconds(PopStyle style) => style == PopStyle.Quick ? .36f : style == PopStyle.Peek ? 1.45f : 1.15f;

    /// <summary>He ducks back into the dumpster after <paramref name="after"/> seconds, and the lid drops. Never during the return.</summary>
    public void Duck(float after = 0f)
    {
        if (set == null || Returning || !Up && !moving) return;
        StopMotion();
        motion = StartCoroutine(Ducking(after));
    }

    void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;
        riseT = 1f;
    }

    IEnumerator Popping(PopStyle style)
    {
        float top = set.inside.position.y;
        float from = body != null ? Mathf.Min(body.transform.position.y, top - 1f) : top - 1f;
        if (from < top - 1.05f) from = top - 1f;   // from under the street (hidden): crouched under the rim
        Vector3 lidAt = set.farLid != null ? set.farLid.position : set.inside.position;
        if (ace != null) Face(ace.position);
        switch (style)
        {
            case PopStyle.Quick:
                // Lid and man together: the lid flies up, he springs up past his height and settles.
                Sfx.Play("lodger.pop", lidAt);
                StartCoroutine(LidTo(1f, .2f));
                yield return Rise(from, top, .34f, Ease.Back);
                break;
            case PopStyle.Peek:
                // The lid lifts a crack, his eyes come over the rim; a look at Ace; then up.
                Sfx.Play("bins.creak", lidAt);
                yield return LidTo(.3f, .28f);
                yield return Rise(from, top - .55f, .35f, Ease.Out);
                yield return new WaitForSeconds(.5f);
                Sfx.Play("lodger.pop", lidAt);
                StartCoroutine(LidTo(1f, .2f));
                yield return Rise(top - .55f, top, .3f, Ease.Back);
                break;
            default:
                // The slow one: the lid creaks right up, he rises, and takes the alley in.
                Sfx.Play("bins.creak", lidAt);
                StartCoroutine(LidTo(1f, .85f));
                yield return new WaitForSeconds(.22f);
                yield return Rise(from, top, .9f, Ease.Out);
                Beat(NpcBeats.Clip.LookingAround, 1f);
                break;
        }
        if (ace != null) Face(ace.position);
        moving = false;
        motion = null;
    }

    IEnumerator Ducking(float after)
    {
        if (after > 0f) yield return new WaitForSeconds(after);
        if (Returning || Barks.ScenePlaying) { motion = null; yield break; }
        moving = true;
        float top = set.inside != null ? set.inside.position.y : 0f;
        float from = body != null ? body.transform.position.y : top;
        yield return Rise(from, top - 1.05f, .3f, Ease.In);
        Vector3 lidAt = set.farLid != null ? set.farLid.position : transform.position;
        yield return LidTo(0f, .2f);
        Sfx.Play("bins.lid", lidAt);
        Ducks++;
        Hide();
        motion = null;
    }

    // While he's in the bin and says something, the lid lifts a crack and drops.
    IEnumerator Rattle()
    {
        yield return LidTo(.07f, .08f);
        yield return new WaitForSeconds(.2f);
        yield return LidTo(0f, .1f);
        if (set.farLid != null) Sfx.Play("bins.rattle", set.farLid.position);
        motion = null;
    }

    enum Ease { Out, In, Back }

    IEnumerator Rise(float from, float to, float seconds, Ease ease)
    {
        if (body == null || set.inside == null) yield break;
        Vector3 at = set.inside.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(.05f, seconds))
        {
            float e = ease == Ease.Back ? EaseOutBack(t) : ease == Ease.In ? t * t : 1f - (1f - t) * (1f - t) * (1f - t);
            body.transform.position = new Vector3(at.x, Mathf.LerpUnclamped(from, to, e), at.z);
            yield return null;
        }
        body.transform.position = new Vector3(at.x, to, at.z);
    }

    IEnumerator LidTo(float to, float seconds)
    {
        if (set.farLid == null) yield break;
        float from = set.LidOpen(set.farLid);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(.05f, seconds))
        {
            set.SetLid(set.farLid, Mathf.Lerp(from, to, 1f - (1f - t) * (1f - t)));
            yield return null;
        }
        set.SetLid(set.farLid, to);
    }

    // Past the end and back (a spring): 1.4 overshoots by about 8%.
    static float EaseOutBack(float t)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
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
        WatchForAce();
        Passing += InPassing() ? 1 : 0;
    }

    // ---------- coming up for Ace, and going back down ----------

    // With his errand's thing in hand and close to his half: up he comes. Up for nothing (it's not in Ace's hand any more,
    // Ace has walked off): back down after a while.
    void WatchForAce()
    {
        if (ace == null || set == null || Returning || NightZero.Pending || Barks.ScenePlaying || moving || motion != null)
        {
            awaySince = -1f;
            return;
        }
        Vector3 half = set.giveSpot != null ? set.giveSpot.position : set.inside != null ? set.inside.position : set.Bins;
        Vector3 d = ace.position - half;
        d.y = 0f;
        float far = d.magnitude;
        if (!Up)
        {
            awaySince = -1f;
            if (far <= Near && Bringing())
            {
                PoppedFor = far;
                Pop(PopStyle.Quick);
            }
            return;
        }
        if (far > WalkedOff && !Bringing())
        {
            if (awaySince < 0f) awaySince = Time.unscaledTime;
            else if (Time.unscaledTime - awaySince > WalkedOffSeconds) { awaySince = -1f; Duck(); }
        }
        else awaySince = -1f;
    }

    // Ace has in hand the thing he asked for (and it's Ace's to give).
    static bool Bringing()
    {
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (ledger == null) return false;
        string errand = LodgerStory.Errand(ledger);
        NightCarry carry = NightCarry.Current;
        return errand.Length > 0 && carry != null && carry.HeldId == errand && ledger.HasTrophy(errand);
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

    // A line now and then as Ace passes (the pools throttle him: a speaker waits 20 s between his own lines). From inside the
    // bin while he's down there: the lid lifts a crack as he says it.
    bool InPassing()
    {
        if (moving || body == null || ace == null || Barks.ScenePlaying || Returning || NightZero.Pending) return false;
        Vector3 d = ace.position - (Up ? body.transform.position : voice.position);
        d.y = 0f;
        if (d.sqrMagnitude > 49f) return false;
        if (Up) Face(ace.position);
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        string errand = LodgerStory.Errand(ledger);
        NightCarry carry = NightCarry.Current;
        string pool = errand.Length == 0 ? LodgerStory.Done
            : carry != null && carry.HeldId == errand ? LodgerStory.Beckon : LodgerStory.Waiting;
        bool said = Barks.SayFrom(Speaker, LodgerStory.SpeakerId, pool);
        if (!said) return false;
        if (Up)
        {
            if (pool == LodgerStory.Beckon) Beat(NpcBeats.Clip.Beckoning);
        }
        else
        {
            FromTheBin++;
            if (motion == null) motion = StartCoroutine(Rattle());
        }
        return true;
    }

    // ---------- the return ----------

    /// <summary>Can Ace hand him <paramref name="thing"/> now? (It's his errand, he's up, nothing else is playing.)</summary>
    public bool CanTake(string thing)
    {
        if (!Up || Returning || Barks.ScenePlaying || NightZero.Pending || string.IsNullOrEmpty(thing)) return false;
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        return ledger != null && LodgerStory.Errand(ledger) == thing && ledger.HasTrophy(thing);
    }

    /// <summary>Ace hands him <paramref name="thing"/>: the favour's return scene. False if he can't take it now.</summary>
    public bool Receive(string thing)
    {
        if (!CanTake(thing)) return false;
        Returning = true;
        returned = LodgerStory.FindFavour(thing);
        paid = false;
        PlayerMovement.Hold(this);
        if (ace != null) Face(ace.position);
        if (aceBody != null && body != null) aceBody.FaceToward(body.transform.position);
        bool playing = returned != null && Barks.Play(returned.returnScene, Who, onDone: Returned, onLineId: OnReturnLine,
            onReply: (choice, reply) => Replied(choice, reply));
        PlayerMovement.Release(this);   // the scene holds Ace now, or nothing does
        if (!playing)
        {
            // No return scene in the Night lines (Barks 1 not run yet): what it does still happens, without the words.
            if (returned != null)
                foreach (string line in new[] { returned.takes, returned.sets, returned.lesson, returned.page }) OnReturnLine(line);
            Returned();
        }
        return true;
    }

    LodgerStory.Favour returned;
    bool paid;

    Transform Who(string speakerId) => speakerId == LodgerStory.SpeakerId ? Speaker : null;

    void Replied(NightLines.Choice choice, int reply)
    {
        int warmth = reply == 0 ? choice.first.warmth : choice.second.warmth;
        if (SaveManager.Instance != null) SaveManager.Instance.Night.Warm(warmth);
        Heard(warmth);
    }

    // What happens on the return's lines: he takes it (it leaves Ace's hand for his corner), does what it was for
    // (turns the gnome to face the street; sets the cups out), and pays (the lesson, then the page).
    void OnReturnLine(string id)
    {
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (ledger == null || returned == null || string.IsNullOrEmpty(id)) return;
        string thing = returned.id;
        if (id == returned.takes)
        {
            NightCarry carry = NightCarry.Current;
            if (carry != null && carry.HeldId == thing) carry.Drop();
            if (ledger.Give(thing)) Sfx.Play("lodger.takes", CornerOf(returned) != null ? CornerOf(returned).transform.position : Speaker.position);
            ShowCorner(turned: false);
            TrophyShelf.RefreshAll();
        }
        else if (id == returned.sets)
        {
            if (returned.corner == "gnome")
            {
                if (turningGnome != null) StopCoroutine(turningGnome);
                turningGnome = StartCoroutine(TurnTheGnome());
            }
            else if (returned.corner == "cups")
            {
                if (settingOut != null) StopCoroutine(settingOut);
                settingOut = StartCoroutine(SetOutTheCups());
            }
        }
        else if (id == returned.lesson)
        {
            LodgerStory.Lesson lesson = LodgerStory.FindLesson(returned.teaches);
            if (lesson != null && ledger.Learn(lesson.id))
            {
                Sfx.Play2D("lodger.lesson");
                NightCycle.Note(lesson.learned, 6f);
            }
        }
        else if (id == returned.page)
        {
            // The page goes in with the notebook's sound; his line points at it, and the notebook (N) has it. No note:
            // the lesson's is the beat's one note (playtest 3). A page about something not in the game yet isn't paid.
            NotebookFactData page = LodgerStory.PageFor(thing, NightZero.InTheGame);
            Notebook notebook = SaveManager.Instance.Notebook;
            if (page != null && notebook != null && notebook.Learn(page, NotebookHooks.Today)) Sfx.Play2D("notebook.page");
            paid = true;
        }
    }

    void Returned()
    {
        // Whatever the lines did or didn't say, the favour is paid in full.
        if (!paid && returned != null)
        {
            OnReturnLine(returned.takes);
            OnReturnLine(returned.lesson);
            OnReturnLine(returned.page);
        }
        if (settingOut != null) { StopCoroutine(settingOut); settingOut = null; }
        ShowCorner(turned: true);
        Returning = false;
        returned = null;
        // Paid and done: a moment, and he's back in the bin.
        Duck(DuckAfterReturn);
    }

    // ---------- his corner ----------

    GameObject CornerOf(LodgerStory.Favour favour) =>
        favour == null ? null : favour.corner == "gnome" ? set.cornerGnome : favour.corner == "cups" ? set.cornerCups : null;

    // What Ace has brought him, at night only: the gnome on the trash can's lid (set down facing the alley; on his line
    // he turns it to face the street), the cups on the crate (on his line he sets them out, one by one). Turned: as
    // they stand once he's done with them.
    void ShowCorner(bool turned)
    {
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (set.cornerGnome != null)
        {
            bool given = ledger != null && ledger.HasGiven(NightThings.GraceGnome);
            if (set.cornerGnome.activeSelf != given) set.cornerGnome.SetActive(given);
            if (given && turned && turningGnome == null) set.cornerGnome.transform.localRotation = Quaternion.identity;
            else if (given && !turned) set.cornerGnome.transform.localRotation = Quaternion.Euler(0f, -set.gnomeTurn, 0f);
        }
        if (set.cornerCups != null && settingOut == null)
        {
            // Before he sets them out, the crate stands empty.
            bool shown = turned && ledger != null && ledger.HasGiven(NightThings.GraceCups);
            if (set.cornerCups.activeSelf != shown) set.cornerCups.SetActive(shown);
            if (shown)
                foreach (Transform cup in set.cornerCups.transform)
                    if (!cup.gameObject.activeSelf) cup.gameObject.SetActive(true);
        }
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

    // "Four cups. Old habit.": he sets them out on the crate one at a time, each popping into place with a clink.
    IEnumerator SetOutTheCups()
    {
        GameObject group = set.cornerCups;
        if (group == null) { settingOut = null; yield break; }
        Transform cups = group.transform;
        int count = cups.childCount;
        var sizes = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Transform cup = cups.GetChild(i);
            sizes[i] = cup.localScale;
            cup.gameObject.SetActive(false);
        }
        group.SetActive(true);
        Beat(NpcBeats.Clip.Thinking);
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(i == 0 ? .25f : .4f);
            Transform cup = cups.GetChild(i);
            cup.gameObject.SetActive(true);
            Sfx.Play("lodger.cup", cup.position);
            for (float t = 0f; t < 1f; t += Time.deltaTime / .14f)
            {
                cup.localScale = sizes[i] * Mathf.SmoothStep(.35f, 1f, t);
                yield return null;
            }
            cup.localScale = sizes[i];
        }
        settingOut = null;
    }

    /// <summary>The cups standing on the crate now (checks).</summary>
    public int CupsOut
    {
        get
        {
            if (set == null || set.cornerCups == null || !set.cornerCups.activeInHierarchy) return 0;
            int n = 0;
            foreach (Transform cup in set.cornerCups.transform) if (cup.gameObject.activeSelf) n++;
            return n;
        }
    }

    public string Describe() =>
        $"The man at the bins: {(body == null ? "no body" : moving ? (Up ? "on his way up" : "ducking") : Up ? "standing up in the dumpster" : "in the bin, the lid shut")}, look {Look}, " +
        $"{(Returning ? "the return playing, " : "")}up {Pops} time(s) tonight (last {LastStyle}), down {Ducks}; {Passing} line(s) in passing " +
        $"({FromTheBin} from inside the bin); the bins view {(inView ? "on" : "off")}.";
}
