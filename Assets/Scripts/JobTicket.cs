using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class JobTicket : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text jobText;
    [SerializeField] private Image patienceFill;
    [SerializeField] private Image background;
    private Image portrait, portraitFrame;
    private TMP_Text portraitInitial, moodText, drinkText;
    private RectTransform callLine, detailPanel;
    private SupportCallIcon callIcon;
    private TMP_Text callState, callSeconds, detailText;
    private bool wasSupport, pointerOver;
    private float cardWidth = 220, cardHeight = 118;
    // The UI skin (playtest 3, session 3): the phone's cream paper and dark ink, the customer's colour as a strip of tape
    // across the top corner (the spec's tab, claude/hud-spec.md §3), one chunky patience meter on a track.
    private static readonly Color Ink = UiSkin.Ink;
    private static readonly Color MutedInk = UiSkin.InkSoft;
    private static readonly Color UrgentInk = UiSkin.Red;
    private Image tape, patienceTrack;
    private bool skinned;
    // At the bench (checklist v2, 14 Oct 2026): a chip of 160 x 44 with the face, the tape and the bar, no words (TicketRailUI
    // says why). A ringing support call keeps its icon and its seconds.
    private bool compact;

    public CustomerBrain Target { get; private set; }

    /// <summary>The tab is a chip (at the bench): the face, the tape and the bar; no name, mood, fault or drink.</summary>
    public bool Compact => compact;

    /// <summary>
    /// The chip's pulse at the bench under a quarter of patience left: 1.0 to 1.06 and back once a second (the rail multiplies
    /// the tab's scale by it); 1 otherwise.
    /// </summary>
    public float PulseScale => compact && Target != null && Target.PatienceFraction <= .25f
        ? 1f + .03f * (1f - Mathf.Cos(UiClock.Now * Mathf.PI * 2f)) : 1f;

    public void SetCompact(bool on)
    {
        if (compact == on) return;
        compact = on;
        ConfigureLayout();
        Refresh();
    }

    public void Bind(CustomerBrain brain)
    {
        Target = brain;
        wasSupport = false;
        pointerOver = false;
        if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        EnsureIdentity();
        ConfigureLayout();
        if (Target == null) return;
        if (slotText != null) slotText.text = brain.CustomerName;
        if (background != null)
        {
            // Only the small card takes pointer hits; the room shows through its paper a little.
            background.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .93f);
            background.raycastTarget = true;
        }
        if (tape != null)
        {
            Color c = brain.JobColor;
            tape.color = new Color(c.r, c.g, c.b, .92f);
        }
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (Target == null) return;
        float patience = Target.PatienceFraction;
        RefreshIdentity(patience);
        var call = Target.ActiveJob as HoldCallJob;
        if (call != null)
        {
            wasSupport = true;
            var phase = call.CurrentPhase;
            bool ringing = phase == HoldCallRun.State.Ringing;
            if (callLine == null)
            {
                BuildCallLine();
                if (compact) ConfigureLayout();
            }
            // A chip at the bench shows the call only while it rings (its icon and its seconds).
            if (compact && ringing != compactRinging)
            {
                compactRinging = ringing;
                ConfigureLayout();
            }
            callLine.gameObject.SetActive(!compact || ringing);
            if (jobText != null && !compact)
            {
                Place(jobText.rectTransform, new Vector2(8, -38), new Vector2(cardWidth - 16, 18));
                jobText.textWrappingMode = TextWrappingModes.NoWrap;
                jobText.text = Target.Record != null ? Target.Record.deviceName : "Support phone";
            }
            SetDrinkLine(Target.HasDrinkOrder && Target.WantedDrink != null
                ? "+ " + Target.WantedDrink.drinkName : "", 56);
            callIcon.Show(phase, Time.time);
            callState.text = phase switch
            {
                HoldCallRun.State.NeedsDialing => call.MissedCalls > 0 ? "Missed call" : "Call support",
                HoldCallRun.State.Connecting => "Connecting",
                HoldCallRun.State.OnHold => "On hold",
                HoldCallRun.State.Ringing => "Answer now",
                _ => "Resolved"
            };
            callSeconds.text = phase == HoldCallRun.State.Ringing || phase == HoldCallRun.State.OnHold
                ? Mathf.CeilToInt(call.SecondsRemaining) + "s"
                : phase == HoldCallRun.State.Done ? "Return phone"
                : phase == HoldCallRun.State.NeedsDialing ? (call.MissedCalls > 0 ? "Redial" : "Dial phone") : "…";
            callState.color = phase == HoldCallRun.State.Ringing ? UiSkin.BrandBright : UiSkin.BandText;
        }
        else
        {
            if (callLine != null) callLine.gameObject.SetActive(false);
            if (compactRinging)
            {
                compactRinging = false;
                ConfigureLayout();
            }
            // A long fault must never push a voiced secondary drink off the card.
            bool secondaryDrink = Target.Record != null && Target.Record.kind == JobKind.Repair
                && Target.HasDrinkOrder && Target.WantedDrink != null;
            SetDrinkLine(secondaryDrink ? "+ " + Target.WantedDrink.drinkName : "", cardHeight - 28);
            if (jobText != null && !compact)
            {
                Place(jobText.rectTransform, new Vector2(8, -40),
                    new Vector2(cardWidth - 16, cardHeight - (secondaryDrink ? 70 : 54)));
                jobText.textWrappingMode = TextWrappingModes.Normal;
                // Delivery removes the repair obligation, even if a drink remains.
                jobText.text = Target.Record != null && Target.Record.kind == JobKind.Repair && Target.ActiveJob == null
                    ? "" : secondaryDrink ? Target.Record.deviceName + "\n" + Target.Record.faultDescription
                    : wasSupport && Target.ActiveJob == null ? "" : Target.TabLines;
            }
        }
        if (patienceFill != null)
        {
            patienceFill.fillAmount = patience;
            patienceFill.color = patience < .5f ? Color.Lerp(UiSkin.Red, UiSkin.Gold, patience * 2f)
                : Color.Lerp(UiSkin.Gold, UiSkin.Brand, (patience - .5f) * 2f);
        }
        // Details use the pointer only when existing game controls have released it; never from a chip at the bench (the
        // bench's cursor passes over the tabs, and the details are words).
        bool showDetails = !compact && pointerOver && Cursor.lockState != CursorLockMode.Locked;
        if (showDetails) RefreshDetails(call);
        else if (detailPanel != null) detailPanel.gameObject.SetActive(false);
    }

    private void EnsureIdentity()
    {
        if (portrait != null) return;
        Skin();
        portraitFrame = RepairOverlayUI.Panel("Customer portrait frame", transform,
            new Vector2(6, -3), new Vector2(34, 34), MutedInk);
        UiSkin.Round(portraitFrame, 9f);
        portrait = RepairOverlayUI.Panel("Customer portrait", portraitFrame.transform,
            new Vector2(1, -1), new Vector2(32, 32), Color.white);
        portrait.preserveAspect = true;
        portraitInitial = RepairOverlayUI.Text("Customer initial", portraitFrame.transform,
            Vector2.zero, new Vector2(34, 34), 22, Ink);
        portraitInitial.alignment = TextAlignmentOptions.Center;
        portraitInitial.fontStyle = FontStyles.Bold;
        moodText = RepairOverlayUI.Text("Customer mood", transform,
            new Vector2(46, -24), new Vector2(cardWidth - 54, 14), 11, MutedInk);
        moodText.textWrappingMode = TextWrappingModes.NoWrap;
        drinkText = RepairOverlayUI.Text("Drink obligation", transform,
            new Vector2(8, -90), new Vector2(cardWidth - 16, 18), 14, Ink);
        drinkText.textWrappingMode = TextWrappingModes.NoWrap;
        drinkText.fontStyle = FontStyles.Bold;
        foreach (TMP_Text t in new[] { portraitInitial, moodText, drinkText }) UiSkin.UseFont(t);
    }

    // The card in the skin, once: rounded paper with a soft shadow, the tape, the meter's track, the skin's lettering.
    private void Skin()
    {
        if (skinned) return;
        skinned = true;
        if (background != null)
        {
            UiSkin.Round(background, 10f);
            var shadow = background.GetComponent<Shadow>();
            if (shadow == null) shadow = background.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .3f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }
        // Stuck over the card's top edge, as tape is.
        tape = RepairOverlayUI.Panel("Tape (the customer's colour)", transform, new Vector2(cardWidth - 52, 9), new Vector2(40, 13), Color.white);
        UiSkin.Round(tape, 2f);
        tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
        if (patienceFill != null)
        {
            // The fill is a plain bar on a rounded track drawn just behind it.
            patienceFill.sprite = null;
            patienceTrack = RepairOverlayUI.Panel("Patience track", patienceFill.transform.parent, Vector2.zero, Vector2.one, UiSkin.Track);
            patienceTrack.transform.SetSiblingIndex(patienceFill.transform.GetSiblingIndex());
            UiSkin.Round(patienceTrack, 4f);
        }
        foreach (TMP_Text t in new[] { slotText, jobText }) UiSkin.UseFont(t);
        if (slotText != null) slotText.fontStyle = FontStyles.Bold;
    }

    private void RefreshIdentity(float patience)
    {
        EnsureIdentity();
        var identity = Target.Identity;
        Sprite face = identity != null ? identity.PortraitAt(patience) : null;
        portrait.sprite = face;
        portrait.enabled = face != null;
        portraitInitial.gameObject.SetActive(face == null);
        string name = Target.CustomerName;
        portraitInitial.text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        var expression = identity != null ? identity.ExpressionAt(patience) : PortraitExpression.Neutral;
        // Urgency stays readable with a neutral-only set or no portrait art.
        moodText.text = patience <= .25f ? "Losing patience" : expression switch
        {
            PortraitExpression.Happy => "Pleased",
            PortraitExpression.Worried => "Worried",
            PortraitExpression.Impatient => "Impatient",
            PortraitExpression.Surprised => "Reassured",
            _ => "Waiting"
        };
        moodText.color = patience <= .25f ? UrgentInk : MutedInk;
        Color theme = identity != null ? identity.ThemeColor : Target.JobColor;
        portraitFrame.color = patience <= .25f ? new Color(.94f, .47f, .32f)
            : Color.Lerp(new Color(.90f, .93f, .90f), theme, .4f);
    }

    private void SetDrinkLine(string value, float offset)
    {
        drinkText.text = value;
        drinkText.gameObject.SetActive(!compact && !string.IsNullOrEmpty(value));
        Place(drinkText.rectTransform, new Vector2(8, -offset), new Vector2(cardWidth - 16, 16));
    }

    // The chip at the bench: the face on the left (where it always is), the tape on the top right, the bar beside the face (lower
    // while a ringing call shows its icon and seconds over it); no words.
    private bool compactRinging;

    private void ConfigureCompact()
    {
        foreach (TMP_Text t in new[] { slotText, jobText, moodText, drinkText })
            if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
        if (patienceFill != null)
        {
            patienceFill.raycastTarget = false;
            RectTransform track = patienceFill.transform.parent as RectTransform;
            RectTransform bar = track != null && track != transform ? track : patienceFill.rectTransform;
            Place(bar, new Vector2(46, compactRinging ? -30 : -18), new Vector2(cardWidth - 56, 8));
            if (patienceTrack != null) Place(patienceTrack.rectTransform, bar.anchoredPosition + new Vector2(-1, 1), bar.sizeDelta + new Vector2(2, 2));
        }
        if (tape != null) Place(tape.rectTransform, new Vector2(cardWidth - 52, 9), new Vector2(40, 13));
        if (callLine != null)
        {
            Place(callLine, new Vector2(44, -3), new Vector2(cardWidth - 50, 24));
            if (callIcon != null) { callIcon.rectTransform.anchoredPosition = new Vector2(12, -12); callIcon.rectTransform.sizeDelta = new Vector2(20, 20); }
            if (callState != null) callState.gameObject.SetActive(false);
            if (callSeconds != null) Place(callSeconds.rectTransform, new Vector2(26, -4), new Vector2(cardWidth - 80, 16));
        }
    }

    private void ConfigureLayout()
    {
        if (compact)
        {
            ConfigureCompact();
            return;
        }
        foreach (TMP_Text t in new[] { slotText, jobText, moodText })
            if (t != null && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
        if (callState != null && !callState.gameObject.activeSelf) callState.gameObject.SetActive(true);
        if (callIcon != null) { callIcon.rectTransform.anchoredPosition = new Vector2(15, -16); callIcon.rectTransform.sizeDelta = new Vector2(28, 28); }
        if (slotText != null)
        {
            Place(slotText.rectTransform, new Vector2(46, -3), new Vector2(cardWidth - 54, 22));
            slotText.enableAutoSizing = false; slotText.fontSize = 17; slotText.color = Ink;
            slotText.overflowMode = TextOverflowModes.Ellipsis;
            slotText.textWrappingMode = TextWrappingModes.NoWrap;
            slotText.alignment = TextAlignmentOptions.TopLeft;
            slotText.raycastTarget = false;
        }
        if (jobText != null)
        {
            jobText.enableAutoSizing = false; jobText.fontSize = 14; jobText.color = Ink;
            jobText.overflowMode = TextOverflowModes.Ellipsis;
            jobText.alignment = TextAlignmentOptions.TopLeft;
            jobText.raycastTarget = false;
        }
        if (moodText != null)
            Place(moodText.rectTransform, new Vector2(46, -24), new Vector2(cardWidth - 54, 14));
        if (patienceFill != null)
        {
            patienceFill.raycastTarget = false;
            RectTransform track = patienceFill.transform.parent as RectTransform;
            Place(track != null && track != transform ? track : patienceFill.rectTransform,
                new Vector2(8, -cardHeight + 10), new Vector2(cardWidth - 16, 7));
            if (patienceTrack != null)
            {
                // Round the bar: a pixel of track shows on every side.
                RectTransform bar = track != null && track != transform ? track : patienceFill.rectTransform;
                Place(patienceTrack.rectTransform, bar.anchoredPosition + new Vector2(-1, 1), bar.sizeDelta + new Vector2(2, 2));
            }
        }
        if (tape != null) Place(tape.rectTransform, new Vector2(cardWidth - 52, 9), new Vector2(40, 13));
        if (callLine != null)
        {
            Place(callLine, new Vector2(8, -72), new Vector2(cardWidth - 16, 32));
            Place(callState.rectTransform, new Vector2(32, 0), new Vector2(cardWidth - 52, 16));
            Place(callSeconds.rectTransform, new Vector2(32, -16), new Vector2(cardWidth - 52, 16));
        }
    }

    public void SetCardSize(float width, float height)
    {
        // A tab is at least 140 x 118; a chip at the bench is its own size (TicketRailUI.CompactWidth x CompactHeight).
        float nextWidth = compact ? width : Mathf.Max(140, width), nextHeight = compact ? height : Mathf.Max(118, height);
        if (Mathf.Approximately(cardWidth, nextWidth) && Mathf.Approximately(cardHeight, nextHeight)) return;
        cardWidth = nextWidth; cardHeight = nextHeight;
        ConfigureLayout();
        Refresh();
    }

    private void BuildCallLine()
    {
        Image callBand = RepairOverlayUI.Panel("Support call obligation", transform, new Vector2(8, -72),
            new Vector2(cardWidth - 16, 32), UiSkin.Band);
        UiSkin.Round(callBand, 8f);
        callLine = callBand.rectTransform;
        var rect = RepairOverlayUI.Rect("Phone", callLine, new Vector2(0, 1), new Vector2(.5f, .5f),
            new Vector2(15, -16), new Vector2(28, 28));
        callIcon = rect.gameObject.AddComponent<SupportCallIcon>(); callIcon.raycastTarget = false;
        callState = RepairOverlayUI.Text("Call state", callLine, new Vector2(32, 0), new Vector2(cardWidth - 52, 16), 13, Color.white);
        callSeconds = RepairOverlayUI.Text("Countdown", callLine, new Vector2(32, -16), new Vector2(cardWidth - 52, 16), 13, UiSkin.BandFaint);
        callState.textWrappingMode = callSeconds.textWrappingMode = TextWrappingModes.NoWrap;
        UiSkin.UseFont(callState);
        UiSkin.UseFont(callSeconds);
    }

    private void RefreshDetails(HoldCallJob call)
    {
        if (detailPanel == null)
        {
            Image detailBand = RepairOverlayUI.Panel("Full ticket details", transform, Vector2.zero,
                new Vector2(320, 80), UiSkin.Band);
            UiSkin.Round(detailBand, UiSkin.Radius);
            detailPanel = detailBand.rectTransform;
            detailText = RepairOverlayUI.Text("Full task text", detailPanel, new Vector2(12, -10),
                new Vector2(296, 60), 16, UiSkin.BandText);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            UiSkin.UseFont(detailText);
            // Draw above neighbours without a raycaster or input capture.
            var layer = detailPanel.gameObject.AddComponent<Canvas>();
            var parentCanvas = GetComponentInParent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 1 : 1;
        }
        detailPanel.gameObject.SetActive(true);
        string obligations = Target.Record != null && Target.Record.kind == JobKind.Repair && Target.ActiveJob == null
            ? Target.HasDrinkOrder && Target.WantedDrink != null ? "+ " + Target.WantedDrink.drinkName : ""
            : Target.TabLines;
        detailText.text = Target.CustomerName + "\n" + obligations
            + (call != null ? "\n" + callState.text + " · " + callSeconds.text : "")
            + "\nPatience: " + Mathf.CeilToInt(Target.PatienceFraction * 100f) + "%";
        float height = detailText.GetPreferredValues(detailText.text, 296, 0).y + 20;
        Place(detailPanel, new Vector2((cardWidth - 320) * .5f, -cardHeight - 5), new Vector2(320, height));
        Place(detailText.rectTransform, new Vector2(12, -10), new Vector2(296, height - 20));
        KeepDetailsOnCanvas();
    }

    private void KeepDetailsOnCanvas()
    {
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null) return;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, detailPanel);
        Rect safe = canvasRect.rect;
        float dx = bounds.min.x < safe.xMin + 8 ? safe.xMin + 8 - bounds.min.x
            : bounds.max.x > safe.xMax - 8 ? safe.xMax - 8 - bounds.max.x : 0;
        float dy = bounds.min.y < safe.yMin + 8 ? safe.yMin + 8 - bounds.min.y : 0;
        detailPanel.position += canvasRect.TransformVector(new Vector3(dx, dy, 0));
    }

    public void OnPointerEnter(PointerEventData eventData) => pointerOver = true;
    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        if (detailPanel != null) detailPanel.gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        pointerOver = false;
        if (detailPanel != null) detailPanel.gameObject.SetActive(false);
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
