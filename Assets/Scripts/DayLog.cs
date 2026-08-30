using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE DAY LOG
//
// Every balance conversation about this game has so far been two people
// guessing. The recap panel shows six numbers, you press Open Tomorrow, and
// they're gone — so "did 6 customers feel frantic?" has never had an answer
// better than a memory of a feeling.
//
// This writes down what actually happened. One line per customer, one file per
// day, dropped next to the project so it's easy to find and easy to send on.
//
// IT CHANGES NOTHING. No rule, no timing, no behaviour. It reads state that
// already exists and writes text. If it ever stops earning its place, delete
// this file and the one call in CustomerBrain.Depart, and the game is exactly
// as it was.
//
// SETUP: drop this component on the GameManager object. That's the whole setup.
// ---------------------------------------------------------------------------

public class DayLog : MonoBehaviour
{
    public static DayLog Instance { get; private set; }

    [Tooltip("Turn off to stop writing files without removing the component. " +
             "Worth doing once you're past the measuring phase — otherwise " +
             "you'll quietly accumulate a file per day forever.")]
    [SerializeField] private bool enabledLogging = true;

    [Tooltip("Folder name. In the Editor this sits next to Assets/ in your " +
             "project root, so it's two clicks away in Explorer.")]
    [SerializeField] private string folderName = "DayLogs";

    [Tooltip("Also print the summary to the Console at the end of each day, " +
             "so you can read it without leaving Unity.")]
    [SerializeField] private bool echoToConsole = true;

    // One visit. Filled in as the customer leaves, never before — a visit that
    // hasn't ended yet has nothing useful to say about itself.
    private struct Visit
    {
        public string name;
        public bool   regular;
        public string character;     // profile or archetype asset name
        public string kind;          // Repair / Drink
        public string subject;       // what they brought, or what they ordered
        public string fault;
        public string faultFamily;
        public string drinkWish;     // a coffee wanted ALONGSIDE a repair
        public float  arrivedAt;
        public float  leftAt;
        public float  repairStartedAt;
        public bool   accepted;
        public bool   served;
        public string outcome;
        public string grade;
        public float  patienceAtExit;
        public string waitKind;
        public int    basePay;
        public int    tip;
    }

    private readonly List<Visit> visits = new();

    // Guards against a customer being written twice — once by Depart and once
    // by the end-of-day sweep.
    //
    // This holds the objects themselves rather than GetInstanceID(), which is
    // deprecated in this Unity version and renamed again in the next one.
    // Reference identity is what we actually mean, it needs no API that can be
    // deprecated under us, and the set is cleared at the end of every day so
    // nothing is kept alive longer than a single day's worth of customers.
    private readonly HashSet<CustomerBrain> logged = new();

    // A static list would survive between play sessions when Enter Play Mode
    // Options skips domain reload, so the first day of run two would open
    // holding run one's data. Same trap WaitingArea hit; same fix.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        if (DayClock.Instance != null) DayClock.Instance.OnDayEnded += WriteDay;
    }

    private void Start()
    {
        // DayClock assigns its Instance in Awake and may or may not have beaten
        // us there, so subscribe in both places and let the -= keep it honest.
        if (DayClock.Instance != null)
        {
            DayClock.Instance.OnDayEnded -= WriteDay;
            DayClock.Instance.OnDayEnded += WriteDay;
        }
    }

    private void OnDisable()
    {
        if (DayClock.Instance != null) DayClock.Instance.OnDayEnded -= WriteDay;
    }

    // ---------- collection ----------

    public static void Record(CustomerBrain brain, bool happy, LostReason reason,
                              bool served, bool accepted,
                              int basePay, int tip, JobGrade grade,
                              float repairStartedAt)
    {
        if (Instance == null || brain == null) return;
        Instance.Add(brain, happy, reason, served, accepted,
                     basePay, tip, grade, repairStartedAt);
    }

    private void Add(CustomerBrain brain, bool happy, LostReason reason,
                     bool served, bool accepted,
                     int basePay, int tip, JobGrade grade, float repairStartedAt)
    {
        if (!enabledLogging) return;
        if (!logged.Add(brain)) return;

        Job job = brain.Record;
        CustomerIdentity id = brain.Identity;

        // CustomerProfile is a ScriptableObject asset; CustomerArchetype is a
        // plain [Serializable] class configured inline on the spawner. So they
        // carry their label in different fields — profiles in characterName,
        // archetypes in archetypeName. Neither has a usable .name.
        string character = "unknown";
        if (id != null)
        {
            if (id.Profile != null) character = id.Profile.characterName;
            else if (id.Archetype != null) character = id.Archetype.archetypeName;
            else character = "no archetype";
        }

        DrinkDefinition wish = brain.WantedDrink;
        bool wishIsExtra = job != null && job.kind == JobKind.Repair && wish != null;

        visits.Add(new Visit
        {
            name            = brain.CustomerName,
            regular         = id != null && id.IsRegular,
            character       = character,
            kind            = job != null ? job.kind.ToString() : "none",
            subject         = job != null ? job.Subject : "",
            fault           = job != null && job.kind == JobKind.Repair ? job.faultDescription : "",
            faultFamily     = job != null && job.kind == JobKind.Repair ? job.faultType.ToString() : "",
            drinkWish       = wishIsExtra ? wish.drinkName : "",
            arrivedAt       = brain.ArrivedAt,
            leftAt          = DayClock.Instance != null ? DayClock.Instance.SecondsIntoDay : 0f,
            repairStartedAt = repairStartedAt,
            accepted        = accepted,
            served          = served,
            outcome         = happy ? "Served" : reason.ToString(),
            grade           = served && job != null && job.kind == JobKind.Repair
                                ? grade.ToString() : "",
            patienceAtExit  = brain.PatienceFraction,
            waitKind        = brain.WaitKind.HasValue ? brain.WaitKind.Value.ToString() : "",
            basePay         = basePay,
            tip             = tip
        });
    }

    // ---------- writing ----------

    private void WriteDay()
    {
        if (!enabledLogging) return;

        DayClock c = DayClock.Instance;
        if (c == null) return;

        // Anyone still standing in the shop when the bell goes. They never
        // reach Depart — StartDay clears them — so without this sweep the
        // people you most want to know about are the ones missing from the log.
        SweepRemaining();

        string dir = LogDirectory();
        Directory.CreateDirectory(dir);

        string stem = Path.Combine(dir, $"Day{c.Day:00}");
        string summary = BuildSummary(c);

        try
        {
            File.WriteAllText(stem + "_summary.txt", summary, Encoding.UTF8);
            File.WriteAllText(stem + "_customers.csv", BuildCsv(), Encoding.UTF8);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[DayLog] Couldn't write the log: {e.Message}");
        }

        if (echoToConsole) Debug.Log(summary);

        visits.Clear();
        logged.Clear();
    }

    private void SweepRemaining()
    {
        CustomerBrain[] left = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (CustomerBrain b in left)
        {
            if (b == null) continue;

            Add(b, false, LostReason.StillInShopAtClose,
                false, b.WasAccepted, 0, 0, JobGrade.Rejected, -1f);
        }
    }

    // In the Editor, Application.dataPath is <project>/Assets, so this lands in
    // the project root. In a build there's no project, so fall back to the
    // normal persistent path.
    private string LogDirectory()
    {
#if UNITY_EDITOR
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, folderName);
#else
        return Path.Combine(Application.persistentDataPath, folderName);
#endif
    }

    private string BuildCsv()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("name,regular,character,kind,subject,fault,fault_family,drink_wish," +
                      "arrived_s,left_s,in_shop_s,repair_s,accepted,served,outcome,grade," +
                      "patience_at_exit,wait_kind,base_pay,tip");

        foreach (Visit v in visits)
        {
            float inShop = Mathf.Max(0f, v.leftAt - v.arrivedAt);
            string repairSecs = v.repairStartedAt >= 0f
                ? F(Mathf.Max(0f, v.leftAt - v.repairStartedAt)) : "";

            sb.AppendLine(string.Join(",",
                Q(v.name), v.regular ? "yes" : "no", Q(v.character), Q(v.kind),
                Q(v.subject), Q(v.fault), Q(v.faultFamily), Q(v.drinkWish),
                F(v.arrivedAt), F(v.leftAt), F(inShop), repairSecs,
                v.accepted ? "yes" : "no", v.served ? "yes" : "no",
                Q(v.outcome), Q(v.grade), F(v.patienceAtExit), Q(v.waitKind),
                v.basePay.ToString(CultureInfo.InvariantCulture),
                v.tip.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    private string BuildSummary(DayClock c)
    {
        int arrived = visits.Count;
        int accepted = 0, stormedQueue = 0, stormedWaiting = 0, declined = 0;
        int outOfStock = 0, shelfFull = 0, stillIn = 0;
        float servedInShop = 0f;
        int servedCount = 0;
        float worstPatience = 1f;

        foreach (Visit v in visits)
        {
            if (v.accepted) accepted++;

            switch (v.outcome)
            {
                case "StormedOutInQueue":   stormedQueue++;   break;
                case "StormedOutWaiting":   stormedWaiting++; break;
                case "Declined":            declined++;       break;
                case "OutOfStock":          outOfStock++;     break;
                case "ShelfFull":           shelfFull++;      break;
                case "StillInShopAtClose":  stillIn++;        break;
            }

            if (v.served)
            {
                servedCount++;
                servedInShop += Mathf.Max(0f, v.leftAt - v.arrivedAt);
                if (v.patienceAtExit < worstPatience) worstPatience = v.patienceAtExit;
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"===== DAY {c.Day} =====");
        sb.AppendLine();
        sb.AppendLine($"Arrived                {arrived}");
        sb.AppendLine($"Accepted               {accepted}");
        sb.AppendLine($"People served          {c.Visitors}    <- humans, counted once each");
        sb.AppendLine($"Orders completed       {c.Served}   ({c.Repairs} repairs, {c.Drinks} drinks)");
        sb.AppendLine();
        sb.AppendLine("WHY PEOPLE LEFT UNSERVED");
        sb.AppendLine($"  Stormed out (queue)  {stormedQueue}    <- never even heard them");
        sb.AppendLine($"  Stormed out (wait)   {stormedWaiting}    <- took the job, didn't get back");
        sb.AppendLine($"  Still here at close  {stillIn}");
        sb.AppendLine($"  You declined         {declined}    <- a choice, not a failure");
        sb.AppendLine($"  Out of stock         {outOfStock}");
        sb.AppendLine($"  Shelf was full       {shelfFull}");
        sb.AppendLine();
        sb.AppendLine("GRADES");
        sb.AppendLine($"  Perfect              {c.Perfect}");
        sb.AppendLine($"  Good                 {c.Good}");
        sb.AppendLine($"  Passable             {c.Passable}");
        sb.AppendLine();
        sb.AppendLine("MONEY");
        sb.AppendLine($"  Earned               ${c.Earned}   (${c.Tips} of it tips)");
        sb.AppendLine($"  of which cafe walk-ins ${c.PatronIncome}");
        sb.AppendLine();

        if (servedCount > 0)
        {
            sb.AppendLine($"Average visit length   {servedInShop / servedCount:0.0}s");
            sb.AppendLine($"Closest call           {worstPatience * 100f:0}% patience left");
        }

        sb.AppendLine();
        sb.AppendLine("Per-customer detail is in the CSV beside this file.");
        return sb.ToString();
    }

    // Commas and quotes in a name would split a column in half and silently
    // shift every field after it.
    private static string Q(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string F(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);
}