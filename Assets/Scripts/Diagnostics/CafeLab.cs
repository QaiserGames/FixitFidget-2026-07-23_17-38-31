using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE CAFÉ LAB
//
// A play session for testing the café's people (movement, seating, crowds)
// that never touches the real playtest save or its day logs:
//
//   * the checkpoint is playtest-cafe-lab.json, next to the real one;
//   * the day logs go to DayLogs/CafeLab;
//   * a banner in the corner says LAB the whole time.
//
// Started from the editor (Fixit Fidget > Café life > Lab). It lasts one Play
// session: the next ordinary Play is an ordinary playtest again. In a build it
// can never switch on.
//
// While it runs, the lab can also serve customers by itself (the autopilot) and
// stage the stress tests: a burst of customers at the door, a full room of
// patrons, everyone leaving at once, a blocked aisle, Ace standing in the way.
//
// PLAYTEST 4 (10 Oct 2026; the veteran's "How the instruments bend the
// numbers"): the autopilot never did the Human fault (Tomas stood 50.7 s at the
// till and stormed out) and never dialled a support call ("[E] Call support"
// sat on screen from 0:35 to 2:05), and its 30-80 s repairs were the dice that
// decided every standing customer. Now it works the counter phone and the
// support line the way a player does, through the game's own calls (the mute
// switch, CompleteCounterRepair; dial, answer, pick up, hand back), and pays
// for them the way the game does. And a session can run at a person's pace
// (CafeLab.HumanSpeedKey; Café life > Lab > Play a lab session - Day 5,
// autopilot at human speed): repairs 45-90 s, drinks 20-40 s, a 3-6 s
// reaction, one pair of hands (a job starts when the last one is done).
// ---------------------------------------------------------------------------
public static class CafeLab
{
    public const string PendingKey = "FixitFidget.CafeLab.Pending";
    public const string AutopilotKey = "FixitFidget.CafeLab.Autopilot";
    /// <summary>Playtest 4: the next lab session's autopilot works at a person's pace. Read once, then forgotten, so the
    /// other lab menus (which never set it) keep the instant, tireless server they always had.</summary>
    public const string HumanSpeedKey = "FixitFidget.CafeLab.HumanSpeed";
    public const string SaveFileName = "playtest-cafe-lab.json";
    public const string LogFolderName = "DayLogs/CafeLab";

    /// <summary>True for the whole of a lab Play session, and never otherwise.</summary>
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReadRequest()
    {
        Active = false;
#if UNITY_EDITOR
        if (PlayerPrefs.GetInt(PendingKey, 0) == 1)
        {
            Active = true;
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
        }
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartDirector()
    {
        if (!Active) return;
        var go = new GameObject("Café lab (this Play session only)");
        var director = go.AddComponent<CafeLabDirector>();
#if UNITY_EDITOR
        director.Autopilot = PlayerPrefs.GetInt(AutopilotKey, 1) == 1;
        director.HumanSpeed = PlayerPrefs.GetInt(HumanSpeedKey, 0) == 1;
        if (PlayerPrefs.HasKey(HumanSpeedKey))
        {
            PlayerPrefs.DeleteKey(HumanSpeedKey);
            PlayerPrefs.Save();
        }
#endif
    }
}

/// <summary>
/// Runs a lab session: the banner, the autopilot and the stress tests. Only ever
/// created by CafeLab, and only in the editor.
/// </summary>
public sealed class CafeLabDirector : MonoBehaviour
{
    public static CafeLabDirector Instance { get; private set; }

    [Tooltip("Serve customers automatically, the way a steady player would: one at a time at the " +
             "counter, then each visit ends after a believable wait.")]
    public bool Autopilot = true;

    [Tooltip("Playtest 4: the autopilot at a person's pace - the Human Speed numbers below, one pair of hands (a job is " +
             "started when the last one is done, and talking at the counter holds the hands up too).")]
    public bool HumanSpeed;

    [Header("Autopilot pacing (seconds)")]
    public Vector2 reachCounter = new Vector2(2.5f, 6f);   // from arriving at the counter to being heard
    public Vector2 betweenConversations = new Vector2(2f, 4f);
    public Vector2 drinkVisit = new Vector2(15f, 35f);      // accepted drink order -> leaves happy
    public Vector2 repairVisit = new Vector2(30f, 80f);     // accepted repair -> leaves happy
    [Range(0f, 1f)] public float declineShare = .08f;

    [Header("The counter phone and the support line (seconds; playtest 4)")]
    [Tooltip("From taking a phone with the Human fault to flipping its mute switch (the counter phone opens as the " +
             "conversation closes).")]
    public Vector2 phoneBeat = new Vector2(1.5f, 3f);
    [Tooltip("The switch's confirmation before the phone goes back, as the counter phone holds it (CounterRepairView).")]
    public float phoneConfirm = 1.2f;
    [Tooltip("From taking a support call to dialling it, and from its ring to answering (never past its window).")]
    public Vector2 callBeat = new Vector2(2.5f, 6f);

    [Header("At human speed (seconds; playtest 4)")]
    public Vector2 humanReaction = new Vector2(3f, 6f);
    public Vector2 humanDrinkVisit = new Vector2(20f, 40f);
    public Vector2 humanRepairVisit = new Vector2(45f, 90f);
    [Tooltip("At human speed, how long talking at the counter holds the hands up.")]
    public float humanConversation = 4f;

    // What the autopilot has planned for each customer.
    private readonly Dictionary<CustomerBrain, float> hearAt = new();
    private readonly Dictionary<CustomerBrain, float> finishAt = new();
    // The counter phone and the support line (playtest 4): when to look at them next, and when to answer a ringing call.
    private readonly Dictionary<CustomerBrain, float> stepAt = new();
    private readonly Dictionary<CustomerBrain, float> answerAt = new();
    // At human speed: when the one pair of hands is free again.
    private float handsFreeAt;
    private float nextConversationAllowed;
    private float nextTick;

    // A blocked aisle, when one is staged.
    private GameObject blocker;
    private float blockerUntil;

    private string lastAction = "";
    private float lastActionAt = -99f;

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly MethodInfo FinishAndLeaveMethod = typeof(CustomerBrain).GetMethod("FinishAndLeave", Any);
    private static readonly MethodInfo StormOutMethod = typeof(CustomerBrain).GetMethod("StormOut", Any);
    private static readonly FieldInfo WasServedField = typeof(CustomerBrain).GetField("wasServed", Any);
    private static readonly FieldInfo CustomerStateField = typeof(CustomerBrain).GetField("state", Any);
    private static readonly MethodInfo PatronLeaveMethod = typeof(PatronBrain).GetMethod("Leave", Any);
    private static readonly MethodInfo PatronSpawnMethod = typeof(PatronSpawner).GetMethod("Spawn", Any);
    private static readonly MethodInfo CustomerSpawnMethod = typeof(CustomerSpawner).GetMethod("Spawn", Any);

    private void Awake()
    {
        Instance = this;
        string missing = "";
        if (FinishAndLeaveMethod == null) missing += " CustomerBrain.FinishAndLeave";
        if (StormOutMethod == null) missing += " CustomerBrain.StormOut";
        if (CustomerStateField == null) missing += " CustomerBrain.state";
        if (PatronLeaveMethod == null) missing += " PatronBrain.Leave";
        if (PatronSpawnMethod == null) missing += " PatronSpawner.Spawn";
        if (CustomerSpawnMethod == null) missing += " CustomerSpawner.Spawn";
        if (missing.Length > 0)
            Debug.LogWarning("[Café lab] Some lab actions are unavailable; these members were renamed or removed:" + missing, this);
    }

    // Logged from Start, not Awake: the autopilot flag is set right after
    // AddComponent, so Awake would always report the default.
    private void Start()
    {
        if (HumanSpeed)
        {
            reachCounter = humanReaction;
            drinkVisit = humanDrinkVisit;
            repairVisit = humanRepairVisit;
            phoneBeat = humanReaction;
            callBeat = humanReaction;
        }
        string pilot = !Autopilot ? "off"
            : !HumanSpeed ? "on"
            : $"on, at human speed (repairs {Span(repairVisit)}, drinks {Span(drinkVisit)}, reacting in {Span(reachCounter)}, one job at a time)";
        Debug.Log($"[Café lab] Lab session: save {CafeLab.SaveFileName}, day logs {CafeLab.LogFolderName}. " +
                  $"Autopilot {pilot}. The real playtest save is untouched.", this);
    }

    private static string Span(Vector2 range) => $"{range.x:0}-{range.y:0} s";

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (blocker != null && Time.time >= blockerUntil) ClearBlocker();
        if (!Autopilot || Time.time < nextTick) return;
        nextTick = Time.time + .25f;
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return;
        RunAutopilot();
    }

    // ---------- the autopilot ----------

    private void RunAutopilot()
    {
        CustomerBrain[] customers = FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude);
        float now = Time.time;

        // Forget anyone who has gone.
        Prune(hearAt);
        Prune(finishAt);
        Prune(stepAt);
        Prune(answerAt);

        foreach (CustomerBrain c in customers)
        {
            if (c == null || !c.isActiveAndEnabled || c.IsLeaving) continue;

            // Heard at the counter one at a time, like a player working the line.
            if (c.CanHearIntake && !hearAt.ContainsKey(c))
                hearAt[c] = Mathf.Max(now + Range(reachCounter), nextConversationAllowed);

            if (hearAt.TryGetValue(c, out float at) && at <= now && c.CanHearIntake)
            {
                hearAt.Remove(c);
                nextConversationAllowed = now + Range(betweenConversations);
                c.HearIntake();
                bool decline = UnityEngine.Random.value < declineShare || !c.CanAcceptJob;
                if (decline)
                {
                    c.RefuseJob();
                    Note($"declined {c.CustomerName}");
                }
                else
                {
                    c.AcceptJob();
                    // At human speed the conversation holds the hands up, whatever they were doing.
                    if (HumanSpeed) handsFreeAt = Mathf.Max(handsFreeAt, now) + humanConversation;
                    bool drink = c.Record != null && c.Record.kind == JobKind.Drink;
                    if (c.IsCounterRepair)
                    {
                        // The Human fault: the counter phone opens as the conversation closes; the switch is flipped there.
                        stepAt[c] = now + Range(phoneBeat);
                        Note($"took {c.CustomerName}'s phone (the mute switch, at the counter)");
                    }
                    else if (c.ActiveJob is HoldCallJob)
                    {
                        stepAt[c] = now + Range(callBeat);
                        Note($"took {c.CustomerName}'s support call (dials it)");
                    }
                    else
                    {
                        finishAt[c] = Work(now, Range(drink ? drinkVisit : repairVisit));
                        Note($"took {c.CustomerName}'s {(drink ? "drink" : "repair")}");
                    }
                }
                continue;
            }

            // The counter phone or the support line: its next step, through the game's own calls.
            if (stepAt.TryGetValue(c, out float step) && step <= now)
            {
                stepAt.Remove(c);
                WorkTheLine(c, now);
                continue;
            }

            // Their visit is over: they got what they came for and leave happy.
            if (finishAt.TryGetValue(c, out float done) && done <= now && c.InService)
            {
                finishAt.Remove(c);
                WasServedField?.SetValue(c, true);
                FinishAndLeaveMethod?.Invoke(c, new object[] { "Thanks, see you soon!", true });
                Note($"{c.CustomerName} is done and leaving");
            }
        }
    }

    // When a job of this long, accepted now, is done: at once in parallel (the instant, tireless server), or at human
    // speed after the jobs before it (one pair of hands).
    private float Work(float now, float seconds)
    {
        if (!HumanSpeed) return now + seconds;
        float start = Mathf.Max(now, handsFreeAt);
        handsFreeAt = start + seconds;
        return handsFreeAt;
    }

    // The counter phone (the Human fault) and the support line (the hold call), worked as a player works them
    // (playtest 4): the mute switch flipped (PhysicalToggle.Activate's call) and the phone given back after its
    // confirmation (CounterRepairView's call); the call dialled and answered (PhoneInteractable's call), then picked up
    // and handed back (PlayerCarry.TryPickUp, CustomerBrain.CompleteJob). Paid the way the game pays.
    private void WorkTheLine(CustomerBrain c, float now)
    {
        if (c.IsCounterRepair)
        {
            HumanFault fault = c.HumanConversation;
            if (fault == null) { Note($"{c.CustomerName}'s phone has no mute switch to flip"); return; }
            if (!fault.Finished)
            {
                if (fault.Flip(c))
                {
                    stepAt[c] = now + phoneConfirm;
                    Note($"flipped {c.CustomerName}'s mute switch");
                }
                else Note($"couldn't flip {c.CustomerName}'s mute switch");
                return;
            }
            string thanks = c.CompleteCounterRepair(c.ActiveJob);
            Note(string.IsNullOrEmpty(thanks) ? $"couldn't give {c.CustomerName} their phone back" : $"gave {c.CustomerName} their phone back, ringing");
            if (HumanSpeed) handsFreeAt = Mathf.Max(handsFreeAt, now) + 1f;
            return;
        }

        if (!(c.ActiveJob is HoldCallJob call)) return;
        if (call.IsComplete)
        {
            HandBackTheCall(c, call, now);
            return;
        }
        if (call.CurrentPhase == HoldCallRun.State.NeedsDialing && call.CanActivate)
        {
            call.Activate();
            Note(call.MissedCalls > 0 ? $"redialled {c.CustomerName}'s support line" : $"dialled {c.CustomerName}'s support line");
            if (HumanSpeed) handsFreeAt = Mathf.Max(handsFreeAt, now) + 1.5f;
        }
        else if (call.CurrentPhase == HoldCallRun.State.Ringing)
        {
            // Answered after a reaction, never past the end of its ring.
            if (!answerAt.TryGetValue(c, out float answer))
                answerAt[c] = answer = now + Mathf.Min(Range(callBeat), Mathf.Max(0f, call.SecondsRemaining - .75f));
            if (answer <= now && call.CanActivate)
            {
                answerAt.Remove(c);
                call.Activate();
                Note($"answered {c.CustomerName}'s support call");
                if (HumanSpeed) handsFreeAt = Mathf.Max(handsFreeAt, now) + 1.5f;
            }
        }
        // On hold, connecting, or just answered: look again shortly.
        stepAt[c] = now + (call.IsComplete ? Range(callBeat) : .5f);
    }

    // The finished call goes back to its owner as a player gives it back: picked up off the shelf, then E on them.
    private void HandBackTheCall(CustomerBrain c, HoldCallJob call, float now)
    {
        PlayerCarry hands = PlayerCarry.Current;
        if (hands == null || (!hands.Contains(call) && !hands.TryPickUp(call)))
        {
            stepAt[c] = now + 1f;   // hands full: again in a moment
            return;
        }
        string said = c.CompleteJob();
        if (string.IsNullOrEmpty(said))
        {
            Note($"couldn't hand {c.CustomerName} their phone back");
            return;
        }
        Note($"gave {c.CustomerName} their phone back, connected");
        if (HumanSpeed) handsFreeAt = Mathf.Max(handsFreeAt, now) + 2f;
        // Still owed a coffee they asked for: the autopilot makes no drinks, so that part of the visit ends the usual way.
        if (!c.IsLeaving && c.HasDrinkOrder) finishAt[c] = Work(now, Range(drinkVisit));
    }

    private static void Prune<T>(Dictionary<CustomerBrain, T> table)
    {
        if (table.Count == 0) return;
        var gone = new List<CustomerBrain>();
        foreach (var pair in table)
            if (pair.Key == null || pair.Key.IsLeaving) gone.Add(pair.Key);
        foreach (CustomerBrain c in gone) table.Remove(c);
    }

    private static float Range(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

    // ---------- stress tests ----------

    /// <summary>Customers walk in through the door together (up to the free counter slots).</summary>
    public int SendCustomersNow(int count)
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        CounterQueue queue = FindAnyObjectByType<CounterQueue>();
        if (spawner == null || CustomerSpawnMethod == null) return 0;
        int room = queue != null ? queue.FreeSlotCount : count;
        count = Mathf.Min(count, room);
        // Straight in at the door, not from the car park: a burst means "together".
        CafeArrivals arrivals = FindAnyObjectByType<CafeArrivals>();
        bool arrivalsOn = arrivals != null && arrivals.enabled;
        if (arrivals != null) arrivals.enabled = false;
        try
        {
            for (int i = 0; i < count; i++) CustomerSpawnMethod.Invoke(spawner, null);
        }
        finally
        {
            if (arrivals != null) arrivals.enabled = arrivalsOn;
        }
        Note($"sent {count} customers in at once");
        return count;
    }

    /// <summary>Patrons walk in through the door together, ignoring the seats kept for customers.</summary>
    public int SendPatronsNow(int count)
    {
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        if (spawner == null || PatronSpawnMethod == null) return 0;
        CafeArrivals arrivals = FindAnyObjectByType<CafeArrivals>();
        bool arrivalsOn = arrivals != null && arrivals.enabled;
        if (arrivals != null) arrivals.enabled = false;
        try
        {
            for (int i = 0; i < count; i++) PatronSpawnMethod.Invoke(spawner, null);
        }
        finally
        {
            if (arrivals != null) arrivals.enabled = arrivalsOn;
        }
        Note($"sent {count} patrons in at once");
        return count;
    }

    /// <summary>Every patron gets up and leaves at the same moment.</summary>
    public int PatronsLeaveNow()
    {
        if (PatronLeaveMethod == null) return 0;
        int n = 0;
        foreach (PatronBrain p in FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude))
        {
            PatronLeaveMethod.Invoke(p, null);
            n++;
        }
        Note($"{n} patrons leave at once");
        return n;
    }

    /// <summary>Every customer's visit ends at the same moment (served and happy).</summary>
    public int CustomersLeaveNow()
    {
        int n = 0;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (c.IsLeaving) continue;
            if (c.InService) FinishAndLeaveMethod?.Invoke(c, new object[] { "Right, I'm off.", true });
            else StormOutMethod?.Invoke(c, null);
            n++;
        }
        finishAt.Clear();
        hearAt.Clear();
        Note($"{n} customers leave at once");
        return n;
    }

    /// <summary>Drops a crate in the aisle for a while: people must find another way round.</summary>
    public void BlockAisle(Vector3 at, Vector3 size, float seconds)
    {
        ClearBlocker();
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Café lab - blocked aisle";
        blocker.transform.position = at + Vector3.up * size.y * .5f;
        blocker.transform.localScale = size;
        var renderer = blocker.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(.85f, .35f, .2f);
        var obstacle = blocker.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        blockerUntil = Time.time + seconds;
        Note($"blocked the aisle at {at.x:0.0},{at.z:0.0} for {seconds:0}s");
    }

    public void ClearBlocker()
    {
        if (blocker != null) Destroy(blocker);
        blocker = null;
    }

    /// <summary>Puts Ace somewhere (a doorway, the middle of a queue) to see how people cope.</summary>
    /// <summary>
    /// Ace walks through the given points in order with his own controller and
    /// input path (a scripted stick, not a teleport), pausing briefly at each.
    /// Used for the "Ace through a populated café" footage and stress runs.
    /// </summary>
    public void WalkAce(Vector3[] points, float pauseSeconds = 1.5f)
    {
        if (walk != null) StopCoroutine(walk);
        walk = StartCoroutine(WalkAceRoutine(points, pauseSeconds));
    }

    private Coroutine walk;

    private System.Collections.IEnumerator WalkAceRoutine(Vector3[] points, float pause)
    {
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace == null) yield break;
        CafeViewMode view = ace.GetComponent<CafeViewMode>();
        Note($"Ace walks {points.Length} legs");
        foreach (Vector3 p in points)
        {
            float deadline = Time.time + 25f;
            while (Time.time < deadline)
            {
                Vector3 to = p - ace.transform.position;
                to.y = 0f;
                if (to.magnitude < 0.3f) break;
                // Same yaw the keys are read against, so the walk is honest.
                float yaw = view != null && view.isActiveAndEnabled ? view.MovementYaw : 45f;
                Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * to.normalized;
                ace.ScriptedInput = new Vector2(local.x, local.z);
                yield return null;
            }
            ace.ScriptedInput = null;
            Vector3 at = ace.transform.position;
            Note($"Ace at {at.x:0.0},{at.z:0.0} (leg to {p.x:0.0},{p.z:0.0})");
            yield return new WaitForSeconds(pause);
        }
        walk = null;
    }

    private void OnDisable()
    {
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace != null) ace.ScriptedInput = null;
    }

    public void MoveAce(Vector3 at, float yaw)
    {
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace == null) return;
        var controller = ace.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        ace.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (controller != null) controller.enabled = was;
        Note($"moved Ace to {at.x:0.0},{at.z:0.0}");
    }

    private void Note(string what)
    {
        lastAction = what;
        lastActionAt = Time.time;
        Debug.Log($"[Café lab] {Time.time:0.0}s: {what}", this);
    }

    // ---------- the banner ----------

    // In the bottom right corner, sized like the HUD (its canvas is 1920 x 1080, scaled to the screen):
    // clear of the controls hint and a conversation's portrait (bottom left), the prompt, the
    // conversation's lines and the straight-face meter (bottom middle), the clock (top right) and the
    // night's notebook page (right). Ace's replies sit in this corner while a conversation is open (the
    // dialogue pass), so the banner steps aside until it closes.
    private const float BannerWidth = 520f, BannerHeight = 72f, BannerMargin = 10f;
    private GUIStyle style;

    private void OnGUI()
    {
        if (ConversationController.AnyOpen) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, richText = true };
        string text = $"<b>CAFÉ LAB</b>  test save, nothing here touches your playtest save" +
                      $"\nautopilot {(Autopilot ? HumanSpeed ? "ON, at human speed" : "ON" : "off")}";
        if (Time.time - lastActionAt < 6f) text += $"\n{lastAction}";
        float scale = Mathf.Sqrt(Screen.width / 1920f * (Screen.height / 1080f));
        if (scale <= 0f) return;
        Matrix4x4 before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.Box(new Rect(width - BannerWidth - BannerMargin, height - BannerHeight - BannerMargin, BannerWidth, BannerHeight), text, style);
        GUI.matrix = before;
    }
}
