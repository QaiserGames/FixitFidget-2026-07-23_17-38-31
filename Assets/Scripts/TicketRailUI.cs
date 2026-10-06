using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TicketRailUI : MonoBehaviour
{
    [SerializeField] private JobTicket ticketPrefab;
    [SerializeField] private Transform rail;
    [SerializeField, Min(220)] private float maxRailWidth = 1000;
    [SerializeField, Min(0)] private float cornerReserve = 460;
    [SerializeField, Min(0)] private float topInset = 20;
    [SerializeField, Min(140)] private float minimumTicketWidth = 150;
    [SerializeField, Min(118)] private float ticketHeight = 118;
    private readonly List<JobTicket> ordered = new();
    private readonly List<CustomerBrain> stale = new();
    private LayoutGroup authoredLayout;
    private bool layoutWasEnabled;
    private RectTransform railRect;
    private Canvas canvas;

    private void Start()
    {
        railRect = rail as RectTransform;
        if (railRect == null) return;
        canvas = rail.GetComponentInParent<Canvas>();
        authoredLayout = rail.GetComponent<LayoutGroup>();
        layoutWasEnabled = authoredLayout != null && authoredLayout.enabled;
        // Runtime wrapping only; the authored scene/prefab remains untouched.
        if (authoredLayout != null) authoredLayout.enabled = false;
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
            if (!show) return;
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
            born[t] = Time.unscaledTime;
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
        if (railRect == null || canvas == null || !rail.gameObject.activeInHierarchy) return;
        if (authoredLayout != null) authoredLayout.enabled = false;
        float dt = Time.unscaledDeltaTime;
        var canvasRect = canvas.transform as RectTransform;
        float width = canvasRect != null ? canvasRect.rect.width : 1920;
        float available = Mathf.Max(220, Mathf.Min(maxRailWidth, width - cornerReserve * 2));
        int count = ordered.Count;
        int columns = Mathf.Max(1, Mathf.Min(Mathf.Max(1, count), Mathf.FloorToInt((available + 8) / (Mathf.Max(140, minimumTicketWidth) + 8))));
        float cardWidth = Mathf.Min(220, (available - (columns - 1) * 8) / columns);
        float cardHeight = Mathf.Max(118, ticketHeight);
        int rows = Mathf.CeilToInt(count / (float)columns);
        railRect.anchorMin = railRect.anchorMax = new Vector2(.5f, 1);
        railRect.pivot = new Vector2(.5f, 1);
        railRect.anchoredPosition = new Vector2(0, -topInset);
        railRect.sizeDelta = new Vector2(available, rows == 0 ? 0 : rows * (cardHeight + 8) - 8);
        for (int i = 0; i < count; i++)
        {
            if (ordered[i] == null) continue;
            int row = i / columns, column = i % columns;
            int inRow = Mathf.Min(columns, count - row * columns);
            float rowWidth = inRow * (cardWidth + 8) - 8;
            var rect = (RectTransform)ordered[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            Vector2 slot = new Vector2(-rowWidth / 2 + column * (cardWidth + 8), -row * (cardHeight + 8));
            rect.anchoredPosition = Arrive(ordered[i], rect, slot, dt);
            ordered[i].SetCardSize(cardWidth, cardHeight);
        }
    }
    private void OnDisable()
    {
        if (authoredLayout != null) authoredLayout.enabled = layoutWasEnabled;
    }

    // JUICE (6 Oct 2026, Mansoor's playtest): a new ticket drops onto the rail from above with a little spring instead of
    // appearing, and the others slide over to make room (or close the gap) rather than jumping.
    private readonly Dictionary<JobTicket, float> born = new();
    private readonly Dictionary<JobTicket, Vector2> shown = new();
    private const float DropSeconds = .32f, DropFrom = 46f;

    private Vector2 Arrive(JobTicket ticket, RectTransform rect, Vector2 slot, float dt)
    {
        if (!shown.TryGetValue(ticket, out Vector2 at)) at = slot;
        at = Vector2.Lerp(at, slot, 1f - Mathf.Exp(-14f * dt));
        if ((at - slot).sqrMagnitude < .25f) at = slot;
        shown[ticket] = at;
        float s = 1f;
        Vector2 drop = Vector2.zero;
        if (born.TryGetValue(ticket, out float since))
        {
            float t = Mathf.Clamp01((Time.unscaledTime - since) / DropSeconds);
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
