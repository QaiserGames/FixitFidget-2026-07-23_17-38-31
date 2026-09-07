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

        CustomerBrain[] all = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // Add tickets for anyone whose job we've accepted.
        foreach (CustomerBrain b in all)
        {
            if (!b.InService || !b.HasJob) continue;
            if (tickets.ContainsKey(b)) continue;

            JobTicket t = Instantiate(ticketPrefab, rail);
            t.Bind(b);
            tickets.Add(b, t);
            ordered.Add(t);
        }

        // Remove tickets whose customer is gone or done.
        stale.Clear();
        foreach (var kv in tickets)
            if (kv.Key == null || !kv.Key.InService || !kv.Key.HasJob) stale.Add(kv.Key);

        foreach (CustomerBrain b in stale)
        {
            ordered.Remove(tickets[b]);
            if (tickets[b] != null) Destroy(tickets[b].gameObject);
            tickets.Remove(b);
        }
    }

    private void LateUpdate()
    {
        if (railRect == null || canvas == null || !rail.gameObject.activeInHierarchy) return;
        if (authoredLayout != null) authoredLayout.enabled = false;
        var canvasRect = canvas.transform as RectTransform;
        float width = canvasRect != null ? canvasRect.rect.width : 1920;
        float available = Mathf.Max(220, Mathf.Min(maxRailWidth, width - cornerReserve * 2));
        int columns = Mathf.Max(1, Mathf.FloorToInt((available + 12) / 232));
        int count = ordered.Count;
        int rows = Mathf.CeilToInt(count / (float)columns);
        railRect.anchorMin = railRect.anchorMax = new Vector2(.5f, 1);
        railRect.pivot = new Vector2(.5f, 1);
        railRect.anchoredPosition = new Vector2(0, -topInset);
        railRect.sizeDelta = new Vector2(available, rows == 0 ? 0 : rows * 184 - 12);
        for (int i = 0; i < count; i++)
        {
            if (ordered[i] == null) continue;
            int row = i / columns, column = i % columns;
            int inRow = Mathf.Min(columns, count - row * columns);
            float rowWidth = inRow * 232 - 12;
            var rect = (RectTransform)ordered[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(220, 172);
            rect.anchoredPosition = new Vector2(-rowWidth / 2 + column * 232, -row * 184);
        }
    }
    private void OnDisable()
    {
        if (authoredLayout != null) authoredLayout.enabled = layoutWasEnabled;
    }
}
