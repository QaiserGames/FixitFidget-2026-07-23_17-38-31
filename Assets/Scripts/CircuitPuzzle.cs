using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

// A diagnostic projection belonging to one device. The run persists when its view closes.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class CircuitPuzzle : MonoBehaviour
{
    [Header("Board")]
    [SerializeField, Range(2, 6)] private int gridWidth = 4;
    [SerializeField, Range(1, 6)] private int gridHeight = 4;
    [SerializeField, Min(1)] private int scrambledTiles = 4;
    [SerializeField, Min(2)] private int minimumRouteTiles = 6;
    [SerializeField, Min(2)] private int maximumRouteTiles = 9;
    [Tooltip("0 creates a fresh layout per job. Set a nonzero seed to reproduce a board.")]
    [SerializeField] private int layoutSeed;
    [Header("Timing")]
    [SerializeField, Min(0.25f)] private float secondsPerTile = 4f;
    [Tooltip("Retry speed through locked tiles. Unverified tiles retain their full interval.")]
    [SerializeField, Min(0.05f)] private float secondsPerVerifiedTile = 0.3f;

    [Tooltip("Hold Space to push the charge along this many times faster.\n\n" +
             "Playtest 2026-09-06: the board was solved in about a second and " +
             "then the player stood watching a timer with no decision left in " +
             "it. Waiting was never the cost — the cost is your hands being " +
             "here instead of at the counter.\n\n" +
             "It is NOT a skip. The charge still checks every connection and " +
             "still stops dead at a mistake, but four times sooner, so a tile " +
             "you thought was straight and isn't gives you no time to catch it. " +
             "Confident? Floor it. Not sure? Let it walk.\n\n" +
             "No extra grade penalty — the existing retry cost already prices " +
             "failure, and charging twice for one mistake would make the button " +
             "not worth pressing.")]
    [SerializeField, Range(1f, 8f)] private float fastForwardMultiplier = 4f;
    [Header("Presentation")]
    [Tooltip("Required asset reference so the board shader is included in player builds.")]
    [SerializeField] private Material boardMaterial;
    [SerializeField, Range(0.2f, 0.5f)] private float viewportHeight = 0.36f;

    private readonly Color plateColor = new Color(0.035f, 0.09f, 0.12f);
    private readonly Color wireColor = new Color(0.18f, 0.65f, 0.72f);
    private readonly Color liveColor = new Color(0.3f, 0.92f, 0.62f);
    private readonly Color warningColor = new Color(1f, 0.58f, 0.18f);
    private readonly List<CircuitTile> tiles = new List<CircuitTile>();
    private readonly List<Renderer[]> wires = new List<Renderer[]>();
    private MaterialPropertyBlock tint;
    private CircuitRun run;
    private JobBase job;
    private ItemInspector inspector;
    private Camera viewCamera;
    private Transform panel, marker;
    private GameObject hud;
    private RectTransform hudRect;
    private TextMeshProUGUI status;
    private TextMeshProUGUI instruction, progressLabel, resultLabel, retryCost;
    private RectTransform progressFill;
    private Button retry;
    private TextMeshProUGUI retryLabel;
    private Rect boardScreenRect;
    private int lastReached = -1;
    private bool lastHalted;
    private bool visible;
    private bool boosting;

    public CircuitRun Run { get { EnsureInitialized(); return run; } }
    public int TotalTasks => Run.Count;
    public int RemainingTasks => Run.RemainingTasks;
    public bool Finished => Run.Finished;
    public bool IsBeingInspected => isActiveAndEnabled && job != null && inspector != null
        && inspector.isActiveAndEnabled && inspector.FocusedItem == job
        && inspector.IsAtWorkbench && Time.timeScale > 0f
        && !(DayClock.Instance != null && DayClock.Instance.DayOver);

    private void Awake() => EnsureInitialized();

    // Rules initialize independently of Awake order, rendering, and camera availability.
    public void EnsureInitialized()
    {
        if (run != null) return;
        gridWidth = Mathf.Clamp(gridWidth, 2, 6);
        gridHeight = Mathf.Clamp(gridHeight, 1, 6);
        job = GetComponentInParent<JobBase>();
        run = new CircuitRun(gridWidth, gridHeight, Mathf.Max(1, scrambledTiles), secondsPerTile,
            layoutSeed == 0 ? Guid.NewGuid().GetHashCode() : layoutSeed,
            minimumRouteTiles, maximumRouteTiles, secondsPerVerifiedTile);
    }

    private void LateUpdate()
    {
        if (inspector == null) inspector = FindAnyObjectByType<ItemInspector>();
        if (viewCamera == null) viewCamera = Camera.main;
        bool watching = IsBeingInspected && viewCamera != null;
        if (watching && panel == null) BuildView();
        watching &= panel != null && hud != null;
        SetVisible(watching);
        if (!watching) return;
        PlaceProjection();

        // Scaling the delta rather than the rules keeps CircuitRun free of Unity
        // and of any notion of "fast" — it still advances at most one tile per
        // call, so a frame hitch under boost can't skip past a bad connection
        // before the player sees it.
        boosting = FastForwardHeld;
        run.Tick(Time.deltaTime * (boosting ? fastForwardMultiplier : 1f), true);
        if (run.Reached != lastReached || run.Halted != lastHalted) Refresh();
        UpdateMarker();
        UpdateStatus();
    }

    // Only while this board is the thing you're looking at. Leaving the bench,
    // the recap opening, or a paused clock all end the boost, because
    // IsBeingInspected already covers every one of those and LateUpdate returns
    // before this is read.
    private bool FastForwardHeld =>
        UnityEngine.InputSystem.Keyboard.current != null
        && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed;

    public bool ContainsHudPoint(Vector2 point) => visible && hudRect != null
        && RectTransformUtility.RectangleContainsScreenPoint(hudRect, point);
    public bool ContainsBoardPoint(Vector2 point) => visible && boardScreenRect.Contains(point);

    // Use the displayed plane: fixed-step physics poses can lag the inspection camera.
    public CircuitTile TileAtScreenPoint(Vector2 point)
    {
        if (!visible || !IsBeingInspected || panel == null || viewCamera == null) return null;
        Ray ray = viewCamera.ScreenPointToRay(point);
        var plane = new Plane(panel.forward, panel.position);
        if (!plane.Raycast(ray, out float distance)) return null;
        Vector3 local = panel.InverseTransformPoint(ray.GetPoint(distance));
        for (int i = 0; i < tiles.Count; i++)
        {
            Vector3 centre = CellPosition(i);
            if (Mathf.Abs(local.x - centre.x) <= 0.48f && Mathf.Abs(local.y - centre.y) <= 0.48f)
                return tiles[i];
        }
        return null;
    }

    private void SetVisible(bool on)
    {
        visible = on;
        if (panel != null && panel.gameObject.activeSelf != on) panel.gameObject.SetActive(on);
        if (hud != null && hud.activeSelf != on) hud.SetActive(on);
    }

    public void HideForInspection() => SetVisible(false);
    private void OnDisable() => SetVisible(false);
    private void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
        if (hud != null) Destroy(hud);
    }

    private void BuildView()
    {
        EnsureInitialized();
        if (boardMaterial == null)
        {
            Debug.LogError("CircuitPuzzle needs its board material assigned. No repair credit was awarded.", this);
            enabled = false;
            return;
        }
        panel = new GameObject("Circuit projection").transform;
        panel.gameObject.SetActive(false);
        MakeBlock(panel, new Vector3(0f, 0f, 0.2f),
            new Vector3(gridWidth + 1.8f, gridHeight + 0.8f, 0.08f), plateColor);
        for (int i = 0; i < run.Count; i++)
        {
            var tileRoot = new GameObject($"Signal tile {i + 1}").transform;
            tileRoot.SetParent(panel, false);
            tileRoot.localPosition = CellPosition(i);
            Renderer backing = MakeBlock(tileRoot, Vector3.zero, new Vector3(0.94f, 0.94f, 0.06f), plateColor * 1.7f);
            BoxCollider hit = tileRoot.gameObject.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.96f, 0.96f, 0.3f);
            var wire = new GameObject("Rotating wire").transform;
            wire.SetParent(tileRoot, false);
            wire.localPosition = new Vector3(0, 0, -0.09f);
            MakeBlock(wire, Vector3.zero, new Vector3(0.19f, 0.19f, 0.025f), wireColor);
            foreach (CircuitRun.Side side in new[] { CircuitRun.Side.Up, CircuitRun.Side.Right, CircuitRun.Side.Down, CircuitRun.Side.Left })
            {
                if ((run.RequiredOpenings(i) & side) == 0) continue;
                Vector3 dir = side == CircuitRun.Side.Up ? Vector3.up : side == CircuitRun.Side.Down ? Vector3.down
                    : side == CircuitRun.Side.Left ? Vector3.left : Vector3.right;
                bool vertical = dir.y != 0;
                MakeBlock(wire, dir * 0.22f, vertical ? new Vector3(0.12f, 0.46f, 0.025f)
                    : new Vector3(0.46f, 0.12f, 0.025f), wireColor);
                // Fixed port marks show the requested route even when the wire is wrong.
                MakeBlock(tileRoot, dir * 0.46f + Vector3.back * 0.1f, new Vector3(0.09f, 0.09f, 0.025f), Color.white);
            }
            MakeLabel(tileRoot, (i + 1).ToString(), new Vector3(-0.3f, 0.3f, -0.19f));
            CircuitTile tile = tileRoot.gameObject.AddComponent<CircuitTile>();
            tile.Build(this, i, wire, backing, plateColor * 1.7f);
            tiles.Add(tile);
            wires.Add(wire.GetComponentsInChildren<Renderer>());
        }
        MakeLabel(panel, "IN", CellPosition(0) + Vector3.left * 0.95f);
        MakeLabel(panel, "OUT", CellPosition(run.Count - 1) + Vector3.right * 0.95f);
        marker = MakeBlock(panel, CellPosition(0) + Vector3.back * 0.25f,
            new Vector3(0.16f, 0.16f, 0.035f), Color.white).transform;
        BuildHud();
        Refresh();
    }

    private Vector3 CellPosition(int index)
    {
        var cell = run.Position(index);
        return new Vector3(cell.X - (gridWidth - 1) * 0.5f, cell.Y - (gridHeight - 1) * 0.5f, 0f);
    }

    // Camera-facing projection, independent of the device's imported scale and rotation.
    private void PlaceProjection()
    {
        float depth = Mathf.Max(viewCamera.nearClipPlane + 0.25f, 0.6f);
        float worldHeight = viewCamera.orthographic ? viewCamera.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float tileScale = Mathf.Min(worldHeight * viewportHeight / (gridHeight + 0.8f),
            worldHeight * viewCamera.aspect * 0.62f / (gridWidth + 1.8f));
        panel.SetPositionAndRotation(viewCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.58f, depth)), viewCamera.transform.rotation);
        panel.localScale = Vector3.one * tileScale;
        Vector3 lo = viewCamera.WorldToScreenPoint(panel.TransformPoint(new Vector3(-(gridWidth + 1.8f) / 2f, -(gridHeight + 0.8f) / 2f, 0f)));
        Vector3 hi = viewCamera.WorldToScreenPoint(panel.TransformPoint(new Vector3((gridWidth + 1.8f) / 2f, (gridHeight + 0.8f) / 2f, 0f)));
        boardScreenRect = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
    }

    private Renderer MakeBlock(Transform parent, Vector3 pos, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Circuit visual";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        Collider collider = go.GetComponent<Collider>();
        collider.enabled = false; // Destroy is deferred; never leave one-frame ray blockers.
        Destroy(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = boardMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Colorize(renderer, color);
        return renderer;
    }

    private void Colorize(Renderer renderer, Color color)
    {
        if (tint == null) tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
        renderer.SetPropertyBlock(tint);
    }

    private void MakeLabel(Transform parent, string text, Vector3 pos)
    {
        var go = new GameObject("Circuit label", typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos + Vector3.back * 0.13f;
        TextMeshPro label = go.GetComponent<TextMeshPro>();
        label.text = text; label.fontSize = 2f; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(0.75f, 0.4f);
        label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    public void Refresh()
    {
        if (run == null) return;
        lastReached = run.Reached; lastHalted = run.Halted;
        for (int i = 0; i < wires.Count; i++)
        {
            Color color = i < run.BestReached ? liveColor : run.Halted && i == run.Reached ? warningColor : wireColor;
            foreach (Renderer renderer in wires[i]) Colorize(renderer, color);
            if (run.IsLocked(i)) tiles[i].SetHighlight(false);
        }
    }

    private void UpdateMarker()
    {
        if (marker == null) return;
        int next = Mathf.Min(run.Reached, run.Count - 1);
        Vector3 from = run.Reached == 0 ? CellPosition(0) + Vector3.left * 0.6f : CellPosition(run.Reached - 1);
        marker.localPosition = (run.Finished || run.Halted ? CellPosition(next)
            : Vector3.Lerp(from, CellPosition(next), run.StepProgress)) + Vector3.back * 0.25f;
    }

    private void BuildHud()
    {
        hud = new GameObject("Circuit controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hud.SetActive(false);
        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        hudRect = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        hudRect.SetParent(hud.transform, false);
        hudRect.anchorMin = hudRect.anchorMax = new Vector2(0.5f, 0f);
        hudRect.pivot = new Vector2(0.5f, 0f);
        hudRect.anchoredPosition = new Vector2(0, 24); hudRect.sizeDelta = new Vector2(760, 148);
        hudRect.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.08f, 0.97f);
        status = HudTopText(new Vector2(22, -16), new Vector2(400, 30), 25);
        resultLabel = HudTopText(new Vector2(422, -18), new Vector2(316, 28), 20);
        resultLabel.alignment = TextAlignmentOptions.Right;
        instruction = HudTopText(new Vector2(22, -54), new Vector2(716, 30), 21);
        instruction.color = new Color(0.86f, 0.87f, 0.82f);
        progressLabel = HudTopText(new Vector2(22, -108), new Vector2(716, 24), 18);
        progressLabel.color = new Color(0.7f, 0.78f, 0.76f);
        var track = new GameObject("Verified progress", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        track.SetParent(hudRect, false);
        track.anchorMin = track.anchorMax = track.pivot = new Vector2(0, 1);
        track.anchoredPosition = new Vector2(22, -94); track.sizeDelta = new Vector2(716, 6);
        track.GetComponent<Image>().color = new Color(0.18f, 0.23f, 0.23f);
        track.GetComponent<Image>().raycastTarget = false;
        progressFill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        progressFill.SetParent(track, false);
        progressFill.anchorMin = Vector2.zero; progressFill.anchorMax = new Vector2(0, 1);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        progressFill.GetComponent<Image>().color = liveColor;
        progressFill.GetComponent<Image>().raycastTarget = false;
        retryCost = HudText(hudRect, new Vector2(22, 18), new Vector2(444, 40), 18);
        retryCost.alignment = TextAlignmentOptions.MidlineLeft;
        retryCost.color = new Color(0.86f, 0.87f, 0.82f);
        RectTransform button = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        button.SetParent(hudRect, false); button.anchorMin = button.anchorMax = Vector2.zero;
        button.pivot = Vector2.zero; button.anchoredPosition = new Vector2(488, 18); button.sizeDelta = new Vector2(250, 40);
        button.GetComponent<Image>().color = new Color(0.14f, 0.27f, 0.32f);
        retry = button.GetComponent<Button>(); retry.targetGraphic = button.GetComponent<Image>();
        retry.onClick.AddListener(RetryFromButton);
        retryLabel = HudText(button, Vector2.zero, button.sizeDelta, 20);
        retryLabel.alignment = TextAlignmentOptions.Center;
    }

    private TextMeshProUGUI HudTopText(Vector2 pos, Vector2 size, float fontSize)
    {
        var text = HudText(hudRect, pos, size, fontSize);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
        return text;
    }

    private static TextMeshProUGUI HudText(Transform parent, Vector2 pos, Vector2 size, float fontSize)
    {
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.zero;
        text.rectTransform.pivot = Vector2.zero; text.rectTransform.anchoredPosition = pos;
        text.rectTransform.sizeDelta = size; text.fontSize = fontSize; text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void RetryFromButton()
    {
        if (!IsBeingInspected || !run.Retry()) return;
        Refresh(); UpdateStatus();
    }

    private void UpdateStatus()
    {
        status.text = run.Finished ? "Signal restored"
            : run.Halted ? "Signal blocked"
            : boosting ? $"Reconnect the circuit  ({fastForwardMultiplier:0}x)"
            : "Reconnect the circuit";
        status.color = run.Finished ? liveColor : run.Halted ? warningColor : Color.white;
        // The fast-forward hint is offered at the moment it's worth something —
        // when every remaining tile is already straight and the board is doing
        // nothing but spending the player's time. Teaching it there means it
        // arrives as an answer to a problem they're currently having, rather
        // than as one more line of chrome to read on the first board.
        bool waitingOnly = !run.Finished && !run.Halted && run.RouteClear;

        instruction.text = run.Finished ? "Circuit complete. Finish any remaining repairs."
            : run.Halted ? $"Connect the white ports on tile {run.Reached + 1}, then retry."
            : boosting ? "Fast-forwarding. The charge still stops at a bad connection."
            : waitingOnly ? "Wires all connected — hold SPACE to speed the charge up."
            : run.Reached < run.BestReached ? "Rechecking locked wires. Prepare the next connection."
            : "Click wires to connect the white ports. Hold SPACE to speed up.";

        instruction.color = boosting ? liveColor
            : waitingOnly ? new Color(1f, 0.92f, 0.55f)
            : Color.white;
        progressLabel.text = $"Verified {run.BestReached} / {run.Count}";
        resultLabel.text = $"Repair result: {job.Grade}";
        progressFill.anchorMax = new Vector2((float)run.BestReached / run.Count, 1);
        hudRect.sizeDelta = new Vector2(760, run.Halted ? 208 : 148);
        retry.gameObject.SetActive(run.Halted);
        retryCost.gameObject.SetActive(run.Halted);
        retry.interactable = run.Halted;
        if (run.Halted)
        {
            retryLabel.text = "Retry (-1 point)";
            retryCost.text = $"Circuit points after retry: at most {run.NextRetryCeiling}/{run.Count}";
        }
    }
}