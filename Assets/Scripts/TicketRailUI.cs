using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE TABS, TOP LEFT (playtest 3, session 3, 7 Oct 2026; the HUD spec of 24 Aug, claude/hud-spec.md §3)
// The rail moved from the top middle to the corner the spec gave it, running left to right from the screen's left edge,
// and every tab is the same width whether there is one or six ("a ticket is a ticket": the count looks busy, not the
// cards). The rail stops short of the top right corner (today's takings, HudCorners) and wraps to a second row when a
// narrow screen can't fit them all. The rail itself never takes the pointer: only the cards do (their details).
public class TicketRailUI : MonoBehaviour
{
    [SerializeField] private JobTicket ticketPrefab;
    [SerializeField] private Transform rail;
    [SerializeField, Min(0)] private float topInset = 20;
    [SerializeField, Min(140)] private float minimumTicketWidth = 150;
    [SerializeField, Min(118)] private float ticketHeight = 118;
    private readonly List<JobTicket> ordered = new();
    private readonly List<CustomerBrain> stale = new();
    private LayoutGroup authoredLayout;
    private bool layoutWasEnabled;
    private RectTransform railRect;
    private Canvas canvas;

    /// <summary>Every tab's width (reference pixels), whatever the count.</summary>
    public const float CardWidth = 220f;
    /// <summary>The gap between tabs, across and down.</summary>
    public const float Gap = 8f;
    /// <summary>Kept clear on the right for today's takings (the cash stack, HudCorners), reference pixels.</summary>
    public const float RightReserve = 380f;

    /// <summary>
    /// How far down from the top of the screen the tabs reach (reference pixels: the rail's top inset and its rows), or 0
    /// with no tabs up. The Day 1 guide sits under it.
    /// </summary>
    public static float BottomEdge { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => BottomEdge = 0f;

    private void Start()
    {
        railRect = rail as RectTransform;
        if (railRect == null) return;
        canvas = rail.GetComponentInParent<Canvas>();
        authoredLayout = rail.GetComponent<LayoutGroup>();
        layoutWasEnabled = authoredLayout != null && authoredLayout.enabled;
        // Runtime wrapping only; the authored scene/prefab remains untouched.
        if (authoredLayout != null) authoredLayout.enabled = false;
        // The rail's own see-through image took the pointer over its whole width.
        Graphic backing = rail.GetComponent<Graphic>();
        if (backing != null) backing.raycastTarget = false;
    }

    private readonly Dictionary<CustomerBrain, JobTicket> tickets = new();

    private void Update()
    {
        // The rail is its own canvas object and nothing was telling it the day
        // had ended, so tickets kept drawing straight over the recap panel —
        // you couldn't read your own takings. Hide while the recap is up.
        if (rail != null && DayClock.Instance != null)
        {
            bool show = !DayClock.Instance.DayOver;
            if (rail.gameObject.activeSelf != show) rail.gameObject.SetActive(show);
            if (!show) { BottomEdge = 0f; return; }
        }

        // Who is in the café is looked up a few times a second, not every frame: the scene search
        // allocated an array every frame (30 Sept), and a ticket a tenth of a second late is invisible.
        if (Time.frameCount < nextScanFrame) { PruneStale(); return; }
        nextScanFrame = Time.frameCount + 6;
        CustomerBrain[] all = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude);

        // Add tickets for anyone whose job we've accepted.
        foreach (CustomerBrain b in all)
        {
            if (!b.InService || !b.HasJob) continue;
            if (tickets.ContainsKey(b)) continue;

            JobTicket t = Instantiate(ticketPrefab, rail);
            t.Bind(b);
            Sfx.Play2D("ticket.new");
            tickets.Add(b, t);
            ordered.Add(t);
            born[t] = UiClock.Now;
        }

        PruneStale();
    }

    private int nextScanFrame;

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
            if (tickets[b] != null) Destroy(tickets[b].gameObject);
            tickets.Remove(b);
        }
    }

    private void LateUpdate()
    {
        if (railRect == null || canvas == null || !rail.gameObject.activeInHierarchy) { BottomEdge = 0f; return; }
        if (authoredLayout != null) authoredLayout.enabled = false;
        float dt = UiClock.Delta;
        var canvasRect = canvas.transform as RectTransform;
        float width = canvasRect != null ? canvasRect.rect.width : 1920;
        float left = UiSkin.Margin;
        float available = Mathf.Max(Mathf.Max(140, minimumTicketWidth), width - left - RightReserve);
        float cardWidth = Mathf.Max(Mathf.Max(140, minimumTicketWidth), Mathf.Min(CardWidth, available));
        float cardHeight = Mathf.Max(118, ticketHeight);
        int count = ordered.Count;
        int columns = Mathf.Max(1, Mathf.FloorToInt((available + Gap) / (cardWidth + Gap)));
        int rows = Mathf.CeilToInt(count / (float)columns);
        float railWidth = Mathf.Min(count, columns) * (cardWidth + Gap) - Gap;
        float railHeight = rows == 0 ? 0 : rows * (cardHeight + Gap) - Gap;
        railRect.anchorMin = railRect.anchorMax = new Vector2(0, 1);
        railRect.pivot = new Vector2(0, 1);
        railRect.anchoredPosition = new Vector2(left, -topInset);
        railRect.sizeDelta = new Vector2(Mathf.Max(0f, railWidth), railHeight);
        BottomEdge = count == 0 ? 0f : topInset + railHeight;
        for (int i = 0; i < count; i++)
        {
            if (ordered[i] == null) continue;
            int row = i / columns, column = i % columns;
            var rect = (RectTransform)ordered[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            Vector2 slot = new Vector2(column * (cardWidth + Gap), -row * (cardHeight + Gap));
            rect.anchoredPosition = Arrive(ordered[i], rect, slot, dt);
            ordered[i].SetCardSize(cardWidth, cardHeight);
        }
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
        rect.localScale = new Vector3(s, s, 1f);
        return at + drop;
    }
}
