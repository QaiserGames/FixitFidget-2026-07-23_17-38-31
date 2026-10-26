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
//
// CLEAR OF ACE (19 Oct 2026, playtest 7). From the overhead camera Ace stands behind the counter, which is "above" the
// customer at the till and the tray on screen, so the name pinned over their head sat on his head and shoulders in 15
// of the reviewers' photos ("the player's own character vanishes under a label at the spot they use most"). When the
// chip over the thing would cover Ace's head, it hangs under the thing instead (its anchor pushed toward the camera until
// the chip's top is at the thing's point): a name badge on a customer's chest, a label on the counter's front under a
// device. The words (22 px, UiPins.NameSize) don't change.
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
                if (string.IsNullOrEmpty(describedName)) { pins.HideFocus(); hangingBelow = false; }
                else
                {
                    Transform over = describedOver;
                    Vector3 point = describedPoint;
                    ClearOfAce(pins, ref over, ref point);
                    pins.ShowFocus(describedName, over, point, describedOver, describedPoint);   // drawn there, about them
                }
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
        hangingBelow = false;
    }

    // ---------- clear of Ace (see the header) ----------

    private bool hangingBelow;
    private const float ChipGap = 4f, PointerAndGap = 12f, Hysteresis = 16f;

    // The pin's anchor: over the thing (as given), or, when the chip there would cover Ace's head on screen, a point toward
    // the camera so the chip hangs under the thing instead.
    private void ClearOfAce(UiPins pins, ref Transform over, ref Vector3 point)
    {
        Camera cam = Camera.main;
        if (cam == null || interactor == null) { hangingBelow = false; return; }
        Vector3 anchor = over != null ? Barks.HeadPointOf(over) : point;
        Vector3 ace = view != null ? view.EyePoint : interactor.transform.position + Vector3.up * 1.6f;
        Vector3 anchorOnScreen = cam.WorldToScreenPoint(anchor), aceOnScreen = cam.WorldToScreenPoint(ace);
        if (anchorOnScreen.z <= 0f || aceOnScreen.z <= 0f) { hangingBelow = false; return; }
        // The chip's size as last drawn (reference pixels times the canvas's scale), or a usual one before it has been.
        float scale = Mathf.Sqrt(Mathf.Max(.1f, Screen.width / 1920f) * Mathf.Max(.1f, Screen.height / 1080f));
        Rect drawn = pins.FocusOnScreen;
        float chipWidth = drawn.width > 0f ? drawn.width : 160f * scale, chipHeight = drawn.height > 0f ? drawn.height : (UiPins.NameSize + 18f) * scale;
        float grow = hangingBelow ? Hysteresis * scale : 0f;   // once hanging, it stays until Ace is well clear (no flapping)
        var above = new Rect(anchorOnScreen.x - chipWidth / 2f - grow, anchorOnScreen.y + ChipGap * scale - grow, chipWidth + 2f * grow, chipHeight + 2f * grow);
        var head = new Rect(aceOnScreen.x - 14f * scale, aceOnScreen.y - 16f * scale, 28f * scale, 40f * scale);
        if (!above.Overlaps(head)) { hangingBelow = false; return; }
        // Toward the camera (down the screen) until the chip's top is just under the thing's point.
        Vector3 toward = -cam.transform.forward;
        toward.y = 0f;
        if (toward.sqrMagnitude < 1e-4f) { hangingBelow = false; return; }
        toward.Normalize();
        float perMetre = anchorOnScreen.y - cam.WorldToScreenPoint(anchor + toward).y;   // screen pixels dropped per metre
        if (perMetre < 1f) { hangingBelow = false; return; }
        float need = chipHeight + PointerAndGap * scale;
        point = anchor + toward * (need / perMetre);
        over = null;
        hangingBelow = true;
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
        // 24 Oct 2026 (Days): the thrift shelf by the bench, with today's find on it: "Thrift watch".
        if (thing is ThriftShelf shelf && shelf.Find != null)
        {
            point = TopOf(shelf.Find.gameObject) + Vector3.up * .12f;
            SideProject marker = SideProject.Of(shelf.Find);
            return marker != null ? marker.PinWords : "Thrift find";
        }
        if (thing is ItemInteractable item && item.Job != null)
        {
            point = TopOf(item.Job.gameObject) + Vector3.up * .12f;
            if (item.Job is DrinkJob cup)
                return cup.Drink != null ? cup.Drink.drinkName : "Cup";
            string subject = item.Job.Record != null ? item.Job.Record.Subject : "";
            if (string.IsNullOrWhiteSpace(subject)) subject = "Device";
            CustomerBrain owner = item.Job.Owner;
            // 24 Oct 2026 (Days, the side project): a thrift find is nobody's: "Thrift watch".
            SideProject find = owner == null ? SideProject.Of(item.Job) : null;
            if (find != null) return find.PinWords;
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

    /// <summary>The top middle of what's drawn of a thing (its own solid parts: DeviceBounds), or its position.</summary>
    public static Vector3 TopOf(GameObject thing) => thing == null ? Vector3.zero : DeviceBounds.Top(thing);

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
