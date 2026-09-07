using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A stable, readable call tray. Size depends on the screen, never camera
// distance. Rows belong to specific calls; the marker identifies the phone.
public sealed class SupportCallHUD : MonoBehaviour
{
    [SerializeField, Range(.8f, 1.5f)] private float uiScale = 1f;
    [SerializeField, Min(0f)] private float topInset = 210f;
    [SerializeField, Min(0f)] private float rightInset = 24f;
    private readonly Dictionary<HoldCallPresentation, Row> rows = new();
    private readonly List<HoldCallPresentation> stale = new();
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform tray;
    private Camera cam;
    private PlayerInteractor player;
    private const float Width = 320f, Height = 96f, Gap = 8f;

    private sealed class Row
    {
        public RectTransform root, marker;
        public TMP_Text name, status, time, action, markerText;
        public Image stripe, fill;
        public HoldCallRun.State phase = (HoldCallRun.State)(-1);
        public float duration;
    }

    public static void EnsureExists()
    {
        if (FindAnyObjectByType<SupportCallHUD>() == null)
            new GameObject("Support call HUD").AddComponent<SupportCallHUD>();
    }

    private void Awake()
    {
        cam = Camera.main; player = FindAnyObjectByType<PlayerInteractor>();
        canvas = RepairOverlayUI.Canvas("Call overlay", transform, 65);
        scaler = canvas.GetComponent<CanvasScaler>();
        tray = RepairOverlayUI.Rect("Call tray", canvas.transform, Vector2.one, Vector2.one, Vector2.zero, new Vector2(Width, 0));
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        bool visible = Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);
        canvas.gameObject.SetActive(visible);
        if (!visible) return;
        if (cam == null) cam = Camera.main;
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();

        // At the default UI scale, action text is 16px at 720p; six calls fit.
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), .8f, 1.6f) * uiScale;
        int count = 0;
        foreach (var call in HoldCallPresentation.Live) if (call != null && call.Job != null && call.Job.CanOperate) count++;
        float requiredHeight = topInset + count * (Height + Gap) + 32;
        scale = Mathf.Min(scale, Screen.height / Mathf.Max(1, requiredHeight));
        scaler.scaleFactor = Mathf.Max(.5f, scale);
        tray.anchoredPosition = new Vector2(-rightInset, -topInset);
        tray.sizeDelta = new Vector2(Width, count * (Height + Gap));
        int index = 0;
        foreach (var call in HoldCallPresentation.Live)
        {
            if (call == null || call.Job == null || !call.Job.CanOperate) continue;
            if (!rows.TryGetValue(call, out var row)) { row = BuildRow(); rows.Add(call, row); }
            row.root.anchoredPosition = new Vector2(0, -index++ * (Height + Gap));
            Refresh(row, call);
        }
        stale.Clear();
        foreach (var pair in rows)
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.Job == null || !pair.Key.Job.CanOperate)
                stale.Add(pair.Key);
        foreach (var call in stale)
        {
            Destroy(rows[call].root.gameObject); Destroy(rows[call].marker.gameObject); rows.Remove(call);
        }
    }

    private Row BuildRow()
    {
        var row = new Row();
        row.root = RepairOverlayUI.Panel("Call", tray, Vector2.zero, new Vector2(Width, Height), RepairOverlayUI.Background).rectTransform;
        row.stripe = RepairOverlayUI.Panel("Customer colour", row.root, Vector2.zero, new Vector2(4, Height), Color.white);
        row.name = RepairOverlayUI.Text("Customer", row.root, new Vector2(14, -5), new Vector2(225, 24), 18, RepairOverlayUI.Muted);
        row.status = RepairOverlayUI.Text("State", row.root, new Vector2(14, -28), new Vector2(215, 30), 25, Color.white);
        row.time = RepairOverlayUI.Text("Seconds", row.root, new Vector2(232, -20), new Vector2(74, 42), 32, Color.white);
        row.time.alignment = TextAlignmentOptions.MidlineRight;
        row.action = RepairOverlayUI.Text("Next action", row.root, new Vector2(14, -60), new Vector2(292, 25), 20, RepairOverlayUI.Muted);
        var track = RepairOverlayUI.Panel("Time track", row.root, new Vector2(14, -89), new Vector2(292, 3), new Color(.20f, .25f, .26f));
        row.fill = RepairOverlayUI.Panel("Time", track.transform, Vector2.zero, new Vector2(0, 3), RepairOverlayUI.Mint);
        row.marker = RepairOverlayUI.Panel("Phone location", canvas.transform, Vector2.zero, new Vector2(52, 32), RepairOverlayUI.Background).rectTransform;
        row.marker.anchorMin = row.marker.anchorMax = new Vector2(.5f, .5f); row.marker.pivot = new Vector2(.5f, 0);
        row.markerText = RepairOverlayUI.Text("Job number", row.marker, Vector2.zero, new Vector2(52, 32), 21, Color.white);
        row.markerText.alignment = TextAlignmentOptions.Center;
        return row;
    }

    private void Refresh(Row row, HoldCallPresentation source)
    {
        var job = source.Job;
        bool ringing = job.CurrentPhase == HoldCallRun.State.Ringing;
        bool focused = player != null && player.Focused != null && player.Focused.GetComponentInParent<HoldCallJob>() == job;
        if (row.phase != job.CurrentPhase) { row.phase = job.CurrentPhase; row.duration = Mathf.Max(.01f, job.SecondsRemaining); }
        Color accent = ringing ? RepairOverlayUI.Amber : job.IsComplete ? RepairOverlayUI.Mint : Color.white;
        row.name.text = job.Owner != null ? $"#{job.Owner.JobNumber}  {job.Owner.CustomerName}" : "Support phone";
        row.stripe.color = job.Owner != null ? job.Owner.JobColor : Color.white;
        row.status.text = job.CurrentPhase switch
        {
            HoldCallRun.State.NeedsDialing => job.MissedCalls > 0 ? "Missed call" : "Ready to call",
            HoldCallRun.State.Connecting => "Connecting",
            HoldCallRun.State.OnHold => "On hold",
            HoldCallRun.State.Ringing => "Answer now",
            _ => "Call resolved"
        };
        bool timed = job.CurrentPhase == HoldCallRun.State.OnHold || ringing;
        row.time.text = timed ? $"{Mathf.CeilToInt(job.SecondsRemaining)}s" : "";
        row.status.color = row.time.color = accent;
        string direction = Direction(job.transform.position);
        row.action.text = focused && !string.IsNullOrEmpty(player.CurrentPrompt) ? "[E] " + player.CurrentPrompt
            : ringing ? "Go to phone · " + direction
            : job.IsComplete ? "Return phone to customer"
            : job.CurrentPhase == HoldCallRun.State.OnHold ? "Free to work elsewhere"
            : job.CurrentPhase == HoldCallRun.State.Connecting ? "Contacting support"
            : job.MissedCalls > 0 ? "Redial at the phone" : "Start at the phone";
        float fraction = Mathf.Clamp01(job.SecondsRemaining / row.duration);
        row.fill.rectTransform.sizeDelta = new Vector2(timed ? 292 * (ringing ? fraction : 1 - fraction) : 0, 3);
        row.fill.color = ringing ? RepairOverlayUI.Amber : RepairOverlayUI.Mint;
        // A restrained brightness pulse, not a flashing full-screen alarm.
        row.status.alpha = ringing ? .85f + .15f * Mathf.Sin(Time.time * 5) : 1f;
        Vector3 point = cam != null ? cam.WorldToViewportPoint(job.transform.position + Vector3.up * .12f) : Vector3.back;
        bool showMarker = (ringing || focused) && point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1;
        row.marker.gameObject.SetActive(showMarker);
        if (showMarker)
        {
            Vector2 pixel = new Vector2(point.x * Screen.width, point.y * Screen.height);
            var canvasRect = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pixel, null, out var local);
            local += Vector2.up * 10;
            local.x = Mathf.Clamp(local.x, canvasRect.rect.xMin + 30, canvasRect.rect.xMax - 30);
            local.y = Mathf.Clamp(local.y, canvasRect.rect.yMin + 4, canvasRect.rect.yMax - 36);
            row.marker.anchoredPosition = local;
            row.markerText.text = job.Owner != null ? "#" + job.Owner.JobNumber : "Call";
            row.markerText.color = accent;
        }
    }

    private string Direction(Vector3 position)
    {
        if (cam == null) return "at the shelf";
        Vector3 point = cam.WorldToViewportPoint(position);
        return point.z < 0 ? "behind you" : point.x < .35f ? "left" : point.x > .65f ? "right" : "ahead";
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
    }
}
