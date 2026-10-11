using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ---------------------------------------------------------------------------
// WHO (OR WHAT) E ACTS ON, BY NAME
//
// In first person the name sits under the crosshair, as it always has: whoever the crosshair is on.
//
// In the overhead view (playtest 4, 10 Oct 2026; the first-hour playtester's #3) the label fixed under the middle of the
// screen named someone the player couldn't find: "Grace" over the empty floor while she stood at the counter, "Walter"
// over a seated patron. Now the name is pinned over whoever E will act on (UiPins, the way the badges are pinned), and
// when Ace stands at a station it says what the station is, in words a player knows: "Repair bench", "Drinks",
// "Intake tray"; a device says whose it is ("Grace's reunion film camera"). The prompt line at the bottom still says the
// verb.
// The name is worked out again only when what E acts on changes (and a few times a second for something that moves), so
// nothing is built every frame.
// ---------------------------------------------------------------------------
public class FocusNameUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private ItemInspector inspector;
    [SerializeField] private ConversationController conversation;
    [SerializeField] private float fadeSpeed = 8f;

    private CafeViewMode view;
    private Interactable described;
    private string describedName = "";
    private Transform describedOver;
    private Vector3 describedPoint;
    private float describeAgainAt;

    private static DropSpot[] drops;
    private static readonly Dictionary<GameObject, Vector3> stationTops = new Dictionary<GameObject, Vector3>();
    private static readonly Vector3 Above = Vector3.up * .25f;

    /// <summary>The stations' names, in words a player knows (the Day 1 guide's pins say them too).</summary>
    public const string BenchName = "Repair bench", TrayName = "Intake tray", DrinksName = "Drinks";

    /// <summary>What the pin over the café says now ("" when none), and over whom (null for a thing).</summary>
    public string PinnedName { get; private set; } = "";
    public Transform PinnedOver { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        drops = null;
        stationTops.Clear();
    }

    private void Update()
    {
        if (view == null && interactor != null) view = interactor.GetComponent<CafeViewMode>();
        bool busy = inspector != null && inspector.IsHoldingItem;
        // The panel already shows who we're talking to — no need to say it twice.
        bool talking = conversation != null && conversation.InConversation;
        // From above, the name is pinned over what E acts on; in first person it sits under the crosshair.
        bool pinned = view != null && view.OverheadShown;

        string who = "";
        if (pinned)
        {
            Interactable focused = !busy && !talking && interactor != null ? interactor.Focused : null;
            float now = Time.unscaledTime;
            if (focused != described || now >= describeAgainAt)
            {
                described = focused;
                describeAgainAt = now + .25f;
                describedName = Describe(focused, out describedOver, out describedPoint);
            }
            UiPins pins = string.IsNullOrEmpty(describedName) && UiPins.Instance == null ? null : UiPins.Ensure();
            if (pins != null)
            {
                if (string.IsNullOrEmpty(describedName)) pins.HideFocus();
                else pins.ShowFocus(describedName, describedOver, describedPoint);
            }
            PinnedName = describedName;
            PinnedOver = string.IsNullOrEmpty(describedName) ? null : describedOver;
        }
        else
        {
            if (UiPins.Instance != null) UiPins.Instance.HideFocus();
            described = null;
            PinnedName = "";
            PinnedOver = null;
            who = interactor != null ? interactor.FocusName : "";
        }

        bool show = !pinned && !busy && !talking && !string.IsNullOrEmpty(who);

        if (show && label != null) label.text = who;

        // Unscaled: the recap pauses the game (timeScale 0). Scaled time froze
        // the fade, so resuming a closed day left the label's placeholder
        // ("New Text") showing in the middle of the recap.
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        if (UiPins.Instance != null) UiPins.Instance.HideFocus();
    }

    /// <summary>
    /// The name of what E acts on and where it is pinned: a person over their head (<paramref name="over"/>), a station or
    /// a thing over a point. "" when it has no name worth pinning (the prompt line says the verb anyway).
    /// </summary>
    public static string Describe(Interactable thing, out Transform over, out Vector3 point)
    {
        over = null;
        point = Vector3.zero;
        if (thing == null) return "";

        CustomerBrain person = thing.GetComponent<CustomerBrain>();
        if (person != null)
        {
            over = person.transform;
            return person.CustomerName;
        }
        if (thing is StationCloseUp offer)
        {
            point = StationTop(offer.station != null ? offer.station.gameObject : offer.gameObject) + Above;
            return DrinksName;
        }
        if (thing is StationInteractable station)
        {
            // Over the top of what's drawn of the station (the bench with its mat and caddy, the counter with the tray).
            point = StationTop(station.gameObject) + Above;
            return station.IsDrinks ? DrinksName : station.IsWorkSurface ? BenchName : TrayName;
        }
        if (thing is DropInteractable drop)
        {
            DropSpot spot = drop.GetComponent<DropSpot>();
            point = StationTop(drop.gameObject) + Above;
            return spot != null && spot.Kind == DropSpot.SpotKind.Counter ? TrayName : BenchName;
        }
        if (thing is ItemInteractable item && item.Job != null)
        {
            point = TopOf(item.Job.gameObject) + Vector3.up * .12f;
            if (item.Job is DrinkJob cup)
                return cup.Drink != null ? cup.Drink.drinkName : "Cup";
            string subject = item.Job.Record != null ? item.Job.Record.Subject : "";
            if (string.IsNullOrWhiteSpace(subject)) subject = "Device";
            CustomerBrain owner = item.Job.Owner;
            return owner != null ? $"{owner.CustomerName}'s {ReputationRules.InSentence(subject)}" : subject;
        }
        return "";
    }

    // A station doesn't move: the top of what's drawn of it, found once a Play session.
    static Vector3 StationTop(GameObject station)
    {
        if (station == null) return Vector3.zero;
        if (stationTops.TryGetValue(station, out Vector3 top)) return top;
        top = TopOf(station);
        stationTops[station] = top;
        return top;
    }

    /// <summary>The top middle of what's drawn of a thing (its renderers' bounds), or its position.</summary>
    public static Vector3 TopOf(GameObject thing)
    {
        if (thing == null) return Vector3.zero;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? new Vector3(b.center.x, b.max.y, b.center.z) : thing.transform.position;
    }

    /// <summary>The bench's or the counter's drop spot (they don't come and go: found once a Play session).</summary>
    public static DropSpot SpotOf(DropSpot.SpotKind kind)
    {
        if (drops == null || drops.Length == 0 || drops[0] == null)
            drops = FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude);
        foreach (DropSpot d in drops)
            if (d != null && d.Kind == kind) return d;
        return null;
    }
}
