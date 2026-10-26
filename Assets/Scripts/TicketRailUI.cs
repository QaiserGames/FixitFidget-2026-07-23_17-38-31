using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE TICKETS, TOP LEFT: ONE ROW, ALWAYS (26 Oct 2026 (HUD 3); playtest 3, session 3, 7 Oct 2026; the HUD spec of 24 Aug,
// claude/hud-spec.md §3)
// The rail runs left to right from the screen's left edge and stops 16 px short of the window top centre (HudWindow: its
// left edge is 780 px in at 1080p). His answer (24 Oct) to "a 4th ticket starts a second row. OK?": "No, find another way";
// and (26 Oct) every ticket must be visible, with big bold icons. So:
//   * up to three tickets are WIDE dockets (236 px: the face in its ring, the name, the icons, the fault in words);
//   * from four, every ticket is a NARROW docket (96 px: the face in its ring over the name over the icons), all the same,
//     all open, 8 px apart: seven fit before the window with room to spare;
//   * from eight the pitch closes so the rail never reaches the window: the dockets overlap a little, the newest on top,
//     every face whole until ten.
// A ticket changes form in about a quarter of a second (its width eases; its words come back 120 ms after it has opened).
// At the bench every ticket is a 160 x 44 chip in the stage's zone U1 (the face in its ring, the icons, the tape; no words:
// checklist v2, his playtest 5), the rail keeping 512 px clear on the right. The rail itself never takes the pointer: only
// the cards do (their details).
public class TicketRailUI : MonoBehaviour
{
    [SerializeField] private JobTicket ticketPrefab;
    [SerializeField] private Transform rail;
    [SerializeField, Min(0)] private float topInset = 20;
    [SerializeField, Min(140)] private float minimumTicketWidth = 150;   // kept for the scene's serialized field; the dockets' widths are JobTicket's
    [SerializeField, Min(118)] private float ticketHeight = 118;         // kept for the scene's serialized field; the dockets' height is JobTicket.Height
    private readonly List<JobTicket> ordered = new();
    private readonly List<CustomerBrain> stale = new();
    private LayoutGroup authoredLayout;
    private bool layoutWasEnabled;
    private RectTransform railRect;
    private Canvas canvas;

    /// <summary>A wide docket's width (reference pixels): up to three tickets.</summary>
    public const float CardWidth = JobTicket.WideWidth;
    /// <summary>A narrow docket's width: from four tickets, every ticket.</summary>
    public const float NarrowWidth = JobTicket.NarrowWidth;
    /// <summary>A docket's height, its torn foot included.</summary>
    public const float TicketHeight = JobTicket.Height;
    /// <summary>The gap between dockets.</summary>
    public const float Gap = 8f;
    /// <summary>The rail ends this far short of the window top centre (HudWindow).</summary>
    public const float WindowGap = 16f;
    /// <summary>Up to this many tickets are wide dockets; from one more, every ticket is narrow.</summary>
    public const int WideUpTo = 3;

    // AT THE BENCH, CHIPS (checklist v2, 14 Oct 2026; his playtest 5: "the checklist is terrible"; Grace's tab alone was 16
    // words on the first repair's screen). While the close-up is up every tab is a 160 x 44 chip, no words. The rail keeps
    // 512 px clear on the right at the bench, so up to eight chips stay in the stage's zone U1 (24-1408 px across, 20-64 px
    // down) and clear of the job card under the takings.
    public const float CompactWidth = 160f, CompactHeight = 44f, BenchRightReserve = 512f;

    /// <summary>Where the rail must end on a canvas this wide (reference pixels): 16 px short of the window's left edge.</summary>
    public static float RailEnd(float canvasWidth) => canvasWidth * .5f - HudWindow.Width * .5f - WindowGap;

    /// <summary>
    /// How far down from the top of the screen the tabs reach (reference pixels: the rail's top inset and the dockets'
    /// height), or 0 with no tabs up. The Day 1 guide sits under it.
    /// </summary>
    public static float BottomEdge { get; private set; }

    /// <summary>
    /// The customers with a tab now (their job taken, still in service), in the order their tabs came: whose devices can lie
    /// on the intake tray (DropInteractable watches it with this list; no scene search).
    /// </summary>
    public static IReadOnlyList<CustomerBrain> InService => inService;
    private static readonly List<CustomerBrain> inService = new();

    /// <summary>The form every ticket has now (labs).</summary>
    public JobTicket.Form FormNow { get; private set; } = JobTicket.Form.Wide;
    /// <summary>How many tickets are on the rail.</summary>
    public int Count => ordered.Count;
    /// <summary>The rail's right edge now (reference pixels from the screen's left), or 0 with no tabs.</summary>
    public float RightEdge { get; private set; }
    /// <summary>The tickets, in the order they came (labs).</summary>
    public IReadOnlyList<JobTicket> Tickets => ordered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BottomEdge = 0f;
        inService.Clear();
    }

    private void Start()
    {
        railRect = rail as RectTransform;
        _ = minimumTicketWidth + ticketHeight;   // the scene's old fields, kept so its serialized values still load
        if (railRect == null) return;
        canvas = rail.GetComponentInParent<Canvas>();
        authoredLayout = rail.GetComponent<LayoutGroup>();
        layoutWasEnabled = authoredLayout != null && authoredLayout.enabled;
        // Runtime layout only; the authored scene/prefab remains untouched.
        if (authoredLayout != null) authoredLayout.enabled = false;
        // The rail's own see-through image took the pointer over its whole width.
        Graphic backing = rail.GetComponent<Graphic>();
        if (backing != null) backing.raycastTarget = false;
    }

    private readonly Dictionary<CustomerBrain, JobTicket> tickets = new();

    private void Update()
    {
        // The rail is its own canvas object and nothing was telling it the day had ended, so tickets kept drawing straight
        // over the recap panel. Hide while the recap is up.
        if (rail != null && DayClock.Instance != null)
        {
            bool show = !DayClock.Instance.DayOver;
            if (rail.gameObject.activeSelf != show) rail.gameObject.SetActive(show);
            if (!show) { BottomEdge = 0f; return; }
        }

        // Who is in the café is looked up a few times a second, not every frame: the scene search allocated an array every
        // frame (30 Sept), and a ticket a tenth of a second late is invisible.
        if (Time.frameCount < nextScanFrame) { PruneStale(); return; }
        nextScanFrame = Time.frameCount + 6;
        CustomerBrain[] all = FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude);

        // Add tickets for anyone whose job we've accepted.
        foreach (CustomerBrain b in all)
        {
            if (!b.InService || !b.HasJob) continue;
            if (tickets.ContainsKey(b)) continue;
            Add(b);
            Sfx.Play2D("ticket.new");
        }

        PruneStale();
    }

    private int nextScanFrame;

    private JobTicket Add(CustomerBrain b)
    {
        JobTicket t = Instantiate(ticketPrefab, rail);
        t.Bind(b);
        tickets.Add(b, t);
        ordered.Add(t);
        born[t] = UiClock.Now;
        if (!inService.Contains(b)) inService.Add(b);
        return t;
    }

    // Remove tickets whose customer is gone or done (every frame: a finished ticket must go at once).
    private void PruneStale()
    {
        stale.Clear();
        foreach (var kv in tickets)
            if (kv.Key == null || !kv.Key.InService || !kv.Key.HasJob) stale.Add(kv.Key);
        if (stale.Count == 0) return;
        foreach (CustomerBrain b in stale)
        {
            ordered.Remove(tickets[b]);
            born.Remove(tickets[b]);
            shown.Remove(tickets[b]);
            widths.Remove(tickets[b]);
            if (tickets[b] != null) Destroy(tickets[b].gameObject);
            tickets.Remove(b);
            inService.Remove(b);
        }
        inService.RemoveAll(c => c == null);
    }

    private void LateUpdate()
    {
        if (railRect == null || canvas == null || !rail.gameObject.activeInHierarchy) { BottomEdge = 0f; return; }
        if (authoredLayout != null) authoredLayout.enabled = false;
        float dt = UiClock.Delta;
        var canvasRect = canvas.transform as RectTransform;
        float width = canvasRect != null ? canvasRect.rect.width : 1920;
        float left = UiSkin.Margin;
        bool bench = ItemInspector.Working;
        int count = ordered.Count;
        float available = bench ? Mathf.Max(CompactWidth, width - left - BenchRightReserve) : Mathf.Max(NarrowWidth, RailEnd(width) - left);
        JobTicket.Form form = bench ? JobTicket.Form.Chip : count <= WideUpTo ? JobTicket.Form.Wide : JobTicket.Form.Narrow;
        // Wide dockets too, if the canvas is narrow: the three would run past the window; then they go narrow.
        if (form == JobTicket.Form.Wide && count * CardWidth + (count - 1) * Gap > available) form = JobTicket.Form.Narrow;
        FormNow = form;
        float cardWidth = bench ? CompactWidth : form == JobTicket.Form.Wide ? CardWidth : NarrowWidth;
        float cardHeight = bench ? CompactHeight : TicketHeight;
        // The pitch: a gap of 8 while the row fits; past that the dockets overlap, the newest on top (later siblings draw later).
        float pitch = cardWidth + Gap;
        if (count > 1 && count * cardWidth + (count - 1) * Gap > available) pitch = Mathf.Max(cardWidth * .55f, (available - cardWidth) / (count - 1));
        float railWidth = count == 0 ? 0f : (count - 1) * pitch + cardWidth;
        railRect.anchorMin = railRect.anchorMax = new Vector2(0, 1);
        railRect.pivot = new Vector2(0, 1);
        railRect.anchoredPosition = new Vector2(left, -topInset);
        railRect.sizeDelta = new Vector2(Mathf.Max(0f, railWidth), cardHeight);
        BottomEdge = count == 0 ? 0f : topInset + cardHeight;
        RightEdge = count == 0 ? 0f : left + railWidth;
        for (int i = 0; i < count; i++)
        {
            if (ordered[i] == null) continue;
            JobTicket t = ordered[i];
            var rect = (RectTransform)t.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            t.SetForm(form);
            // The width eases between forms (about a quarter of a second); the chip's at once.
            float w = Eased(t, cardWidth, bench, dt);
            rect.sizeDelta = new Vector2(w, bench ? cardHeight : cardHeight - JobTicket.FootHeight);
            Vector2 slot = new Vector2(i * pitch, 0f);
            rect.anchoredPosition = Arrive(t, rect, slot, dt);
            t.SetCardSize(w, cardHeight);
        }
    }

    private readonly Dictionary<JobTicket, float> widths = new();

    private float Eased(JobTicket ticket, float want, bool atOnce, float dt)
    {
        if (atOnce || !Application.isPlaying || !widths.TryGetValue(ticket, out float w)) { widths[ticket] = want; return want; }
        w = Mathf.Lerp(w, want, 1f - Mathf.Exp(-16f * dt));
        if (Mathf.Abs(w - want) < .5f) w = want;
        widths[ticket] = w;
        return w;
    }

    private void OnDisable()
    {
        if (authoredLayout != null) authoredLayout.enabled = layoutWasEnabled;
        BottomEdge = 0f;
    }

    // JUICE (6 Oct 2026, Mansoor's playtest): a new ticket drops onto the rail from above with a little spring instead of
    // appearing, and the others slide over to make room (or close the gap) rather than jumping. On the UI's own clock
    // (UiClock), so it holds while the phone pauses the game; out of Play (the layout check) every card is put straight
    // in its slot.
    private readonly Dictionary<JobTicket, float> born = new();
    private readonly Dictionary<JobTicket, Vector2> shown = new();
    private const float DropSeconds = .32f, DropFrom = 46f;

    private Vector2 Arrive(JobTicket ticket, RectTransform rect, Vector2 slot, float dt)
    {
        if (!Application.isPlaying)
        {
            shown[ticket] = slot;
            born.Remove(ticket);
            rect.localScale = Vector3.one;
            return slot;
        }
        if (!shown.TryGetValue(ticket, out Vector2 at)) at = slot;
        at = Vector2.Lerp(at, slot, 1f - Mathf.Exp(-14f * dt));
        if ((at - slot).sqrMagnitude < .25f) at = slot;
        shown[ticket] = at;
        float s = 1f;
        Vector2 drop = Vector2.zero;
        if (born.TryGetValue(ticket, out float since))
        {
            float t = Mathf.Clamp01((UiClock.Now - since) / DropSeconds);
            float u = t - 1f;
            float spring = 1f + 2.4f * u * u * u + 1.4f * u * u;   // past the end and back
            drop = new Vector2(0f, DropFrom * (1f - spring));
            s = Mathf.LerpUnclamped(.86f, 1f, spring);
            if (t >= 1f) born.Remove(ticket);
        }
        s *= ticket.PulseScale;   // under a quarter of patience left the docket pulses (M2 §4; the chip's rule since checklist v2)
        rect.localScale = new Vector3(s, s, 1f);
        return at + drop;
    }

    // ---------- the labs ----------

    /// <summary>
    /// The HUD lab: a ticket for a stand-in customer (an inactive CustomerBrain the lab made up; the rail's scan never sees it,
    /// so it stays until LabClear). Counted in InService like any ticket.
    /// </summary>
    public JobTicket LabStandIn(CustomerBrain brain)
    {
        if (brain == null) return null;
        return tickets.TryGetValue(brain, out JobTicket had) ? had : Add(brain);
    }

    /// <summary>The HUD lab: every stand-in's ticket taken off the rail.</summary>
    public void LabClear(IEnumerable<CustomerBrain> standIns)
    {
        foreach (CustomerBrain b in standIns)
        {
            if (b == null || !tickets.TryGetValue(b, out JobTicket t)) continue;
            ordered.Remove(t);
            born.Remove(t);
            shown.Remove(t);
            widths.Remove(t);
            if (t != null) Destroy(t.gameObject);
            tickets.Remove(b);
            inService.Remove(b);
        }
    }
}
