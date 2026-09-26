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
// ---------------------------------------------------------------------------
public static class CafeLab
{
    public const string PendingKey = "FixitFidget.CafeLab.Pending";
    public const string AutopilotKey = "FixitFidget.CafeLab.Autopilot";
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

    [Header("Autopilot pacing (seconds)")]
    public Vector2 reachCounter = new Vector2(2.5f, 6f);   // from arriving at the counter to being heard
    public Vector2 betweenConversations = new Vector2(2f, 4f);
    public Vector2 drinkVisit = new Vector2(15f, 35f);      // accepted drink order -> leaves happy
    public Vector2 repairVisit = new Vector2(30f, 80f);     // accepted repair -> leaves happy
    [Range(0f, 1f)] public float declineShare = .08f;

    // What the autopilot has planned for each customer.
    private readonly Dictionary<CustomerBrain, float> hearAt = new();
    private readonly Dictionary<CustomerBrain, float> finishAt = new();
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
        Debug.Log($"[Café lab] Lab session: save {CafeLab.SaveFileName}, day logs {CafeLab.LogFolderName}. " +
                  $"Autopilot {(Autopilot ? "on" : "off")}. The real playtest save is untouched.", this);
    }

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
                    bool drink = c.Record != null && c.Record.kind == JobKind.Drink;
                    finishAt[c] = now + Range(drink ? drinkVisit : repairVisit);
                    Note($"took {c.CustomerName}'s {(drink ? "drink" : "repair")}");
                }
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

    private GUIStyle style;

    private void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, richText = true };
        string text = $"<b>CAFÉ LAB</b>  test save, nothing here touches your playtest save" +
                      $"\nautopilot {(Autopilot ? "ON" : "off")}";
        if (Time.time - lastActionAt < 6f) text += $"\n{lastAction}";
        GUI.Box(new Rect(10, Screen.height - 86, 520, 76), text, style);
    }
}
