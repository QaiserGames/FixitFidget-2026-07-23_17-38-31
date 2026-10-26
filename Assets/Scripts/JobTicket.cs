using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// ---------------------------------------------------------------------------
// A TICKET ON THE RAIL (26 Oct 2026 (HUD 3); first skinned 7 Oct 2026 for playtest 3, session 3; remade 19 Oct after her
// playtest 7)
//
// His note (26 Oct, on round 2's folding dockets): "i like the way the tickets patience bar is set up, its a circle that
// circles around their name which is honestly very smart but i still would like everyones ticket to be visibly seen and have
// nice big bold icons for each item, like a coffee, tea, etc, clock". And 24 Oct: a fourth ticket must never start a second
// row ("No, find another way").
//
// So a ticket is a paper docket (cream, a torn foot, the customer's colour as tape over its top-left corner) that says WHO,
// WHAT and HOW URGENT without a sentence:
//   WHO         the face (or the initial) inside the PATIENCE RING: the ring empties clockwise from the top as patience goes,
//               green, then gold, then red; under a quarter the ring and the face's rim turn red and the docket pulses once a
//               second; the name beside it (24 px bold) or under it (22 px);
//   WHAT        BIG BOLD ICONS of each item, 36 px: the device (a phone, a watch, a camera: NoteIcons.ForDevice, the icon
//               sheet's item_*), then each drink (the cup: JobTicket.DrinkIconName, drink_*), and A CLOCK (item_clock) once
//               patience is under half, red and pulsing under a quarter: "they've waited long enough". Without the sheet an
//               icon is a dark chip with the item's initial;
//   THE WORDS   the fault as a sentence ("Dirty lens and film path", 24 px since 26 Oct (Readability), two lines at most) only on the wide docket; the
//               hover details carry the whole job in words on any form.
// Three forms, chosen by the rail (TicketRailUI): WIDE (236 x 144, up to three tickets), NARROW (96 x 144: the face and ring
// over the name over the icons; from four tickets, every ticket the same, all visible, one row) and the CHIP at the bench
// (160 x 44: the face in its ring, the icons, the tape, no words; a ringing support call keeps its icon and seconds). The
// prefab's own parts (the background, SlotText, JobText, PatienceFill) are reused; everything else is made in code.
// ---------------------------------------------------------------------------
public class JobTicket : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum Form { Wide, Narrow, Chip }

    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text jobText;
    [SerializeField] private Image patienceFill;
    [SerializeField] private Image background;

    // Sizes (reference pixels).
    public const float WideWidth = 236f, NarrowWidth = 96f, Height = 144f, FootHeight = 10f;
    public const float RingSize = 52f, RingStroke = 4.5f, FaceSize = 40f, ChipRingSize = 36f, ChipFaceSize = 28f;
    public const float IconSize = 36f, IconGap = 6f, ChipIconSize = 28f, NameSize = 24f, NarrowNameSize = 22f, FaultSize = 24f;   // 26 Oct 2026 (Readability): the fault 22 to 24
    private static readonly Color Ink = UiSkin.Ink;
    private static readonly Color UrgentInk = UiSkin.Red;
    private static readonly Color UrgentInitial = new Color(.557f, .165f, .125f);   // #8E2A20: 6.3:1 on UiSkin.RedSoft (26 Oct 2026, Readability)

    private Image portrait, portraitFrame, ringTrack, tape, foot;
    private TMP_Text portraitInitial;
    private RectTransform ring, callLine, detailPanel, iconsRow;
    private SupportCallIcon callIcon;
    private TMP_Text callState, callSeconds, detailText;
    private CanvasGroup wordsGroup;
    private readonly Image[] itemIcons = new Image[3];
    private readonly TMP_Text[] itemInitials = new TMP_Text[3];
    private readonly string[] itemNames = new string[3];
    private Image clockIcon;
    private TMP_Text clockInitial;
    private bool wasSupport, pointerOver, built, compactRinging;
    private float cardWidth = WideWidth, cardHeight = Height, wordsShownAt = -10f;
    private Form form = Form.Wide, laidOut = (Form)(-1);
    private float laidOutWidth = -1f;

    public CustomerBrain Target { get; private set; }

    /// <summary>The form the rail gave this ticket.</summary>
    public Form Shape => form;
    /// <summary>The tab is a chip (at the bench): the face in its ring, the icons and the tape; no words.</summary>
    public bool Compact => form == Form.Chip;
    /// <summary>The icons the ticket shows now, in order ("item_camera, drink_latte, item_clock"); a chip with an initial counts by its name.</summary>
    public string Items
    {
        get
        {
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < itemIcons.Length; i++)
                if (itemIcons[i] != null && itemIcons[i].gameObject.activeSelf && !string.IsNullOrEmpty(itemNames[i])) { if (b.Length > 0) b.Append(", "); b.Append(itemNames[i]); }
            if (clockIcon != null && clockIcon.gameObject.activeSelf) { if (b.Length > 0) b.Append(", "); b.Append(NoteIcons.ItemClock); }
            return b.ToString();
        }
    }
    /// <summary>The ring's fill (the patience) as drawn.</summary>
    public float RingFill => patienceFill != null ? patienceFill.fillAmount : 0f;
    /// <summary>True while the clock icon is up (patience under half).</summary>
    public bool ClockShowing => clockIcon != null && clockIcon.gameObject.activeSelf;

    /// <summary>
    /// The pulse under a quarter of patience left: 1.0 to 1.03 and back once a second (the rail multiplies the ticket's scale
    /// by it); 1 otherwise.
    /// </summary>
    public float PulseScale => Target != null && Target.PatienceFraction <= .25f
        ? 1f + .015f * (1f - Mathf.Cos(UiClock.Now * Mathf.PI * 2f)) : 1f;

    /// <summary>The chip at the bench, or back to the rail's form (kept for the older callers; the rail uses SetForm).</summary>
    public void SetCompact(bool on) => SetForm(on ? Form.Chip : form == Form.Chip ? Form.Wide : form);

    public void SetForm(Form wanted)
    {
        if (form == wanted) return;
        bool wordsBack = wanted == Form.Wide && form != Form.Wide;
        form = wanted;
        if (wordsBack) wordsShownAt = UiClock.Now + .12f;   // the words fade in 120 ms after the docket has opened, so text never squashes
        ConfigureLayout();
        Refresh();
    }

    public void Bind(CustomerBrain brain)
    {
        Target = brain;
        wasSupport = false;
        pointerOver = false;
        if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        EnsureBuilt();
        ConfigureLayout();
        if (Target == null) return;
        if (slotText != null) slotText.text = brain.CustomerName;
        if (background != null)
        {
            // Only the card takes pointer hits; the room shows through its paper a little.
            background.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .94f);
            background.raycastTarget = true;
        }
        if (foot != null) foot.color = background != null ? background.color : UiSkin.Paper;
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
        bool ringing = false;
        if (call != null)
        {
            wasSupport = true;
            var phase = call.CurrentPhase;
            ringing = phase == HoldCallRun.State.Ringing;
            if (callLine == null) BuildCallLine();
            if (form == Form.Chip && ringing != compactRinging) { compactRinging = ringing; ConfigureLayout(); }
            callLine.gameObject.SetActive(form != Form.Chip || ringing);
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
            if (compactRinging) { compactRinging = false; ConfigureLayout(); }
        }
        // The words (the wide docket only): the fault as a sentence; nothing once the repair has been handed back.
        if (jobText != null)
        {
            bool delivered = Target.Record != null && Target.Record.kind == JobKind.Repair && Target.ActiveJob == null;
            string words = form == Form.Wide && call == null ? (delivered || (wasSupport && Target.ActiveJob == null) ? "" : Lines()) : "";
            if (jobText.text != words) jobText.text = words;
            if (wordsGroup != null) wordsGroup.alpha = form == Form.Wide ? Mathf.Clamp01((UiClock.Now - wordsShownAt) / .12f) : 0f;
        }
        RefreshItems(patience, call != null);
        // The ring: it empties clockwise from the top; green, then gold, then red.
        if (patienceFill != null)
        {
            patienceFill.fillAmount = patience;
            patienceFill.color = patience < .5f ? Color.Lerp(UiSkin.Red, UiSkin.Gold, patience * 2f)
                : Color.Lerp(UiSkin.Gold, UiSkin.Brand, (patience - .5f) * 2f);
        }
        // Details use the pointer only when existing game controls have released it; never from a chip at the bench (the
        // bench's cursor passes over the tabs, and the details are words).
        bool showDetails = form != Form.Chip && pointerOver && Cursor.lockState != CursorLockMode.Locked;
        if (showDetails) RefreshDetails(call);
        else if (detailPanel != null) detailPanel.gameObject.SetActive(false);
    }

    // ---------- the pieces, made once ----------

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;
        if (background != null)
        {
            UiSkin.Round(background, 6f);
            var shadow = background.GetComponent<Shadow>();
            if (shadow == null) shadow = background.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .32f);
            shadow.effectDistance = new Vector2(2f, -5f);
        }
        // The torn foot under the paper (tiled teeth), then the tape over the top-left corner.
        foot = RepairOverlayUI.Panel("Torn foot", transform, new Vector2(0f, -(Height - FootHeight)), new Vector2(WideWidth, FootHeight), UiSkin.Paper);
        foot.sprite = HudArt.TornFoot();
        foot.type = Image.Type.Tiled;
        tape = RepairOverlayUI.Panel("Tape (the customer's colour)", transform, new Vector2(10f, 7f), new Vector2(40f, 13f), Color.white);
        UiSkin.Round(tape, 2f);
        tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
        // The ring: the prefab's fill image, radial now, over a faint track; the face in a circle inside it.
        ring = RepairOverlayUI.Rect("Ring", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(RingSize, RingSize));
        ringTrack = RepairOverlayUI.Panel("Patience track", ring, Vector2.zero, new Vector2(RingSize, RingSize), UiSkin.Track);
        ringTrack.sprite = HudArt.Ring((int)RingSize, RingStroke);
        if (patienceFill != null)
        {
            patienceFill.transform.SetParent(ring, false);
            patienceFill.sprite = HudArt.Ring((int)RingSize, RingStroke);
            patienceFill.type = Image.Type.Filled;
            patienceFill.fillMethod = Image.FillMethod.Radial360;
            patienceFill.fillOrigin = (int)Image.Origin360.Top;
            patienceFill.fillClockwise = true;
            patienceFill.raycastTarget = false;
            Fill(patienceFill.rectTransform);
        }
        portraitFrame = RepairOverlayUI.Panel("Customer portrait frame", ring, new Vector2((RingSize - FaceSize) * .5f, -(RingSize - FaceSize) * .5f), new Vector2(FaceSize, FaceSize), UiSkin.Paper);
        portraitFrame.sprite = UiSkin.Circle;
        var mask = portraitFrame.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        portrait = RepairOverlayUI.Panel("Customer portrait", portraitFrame.transform, Vector2.zero, new Vector2(FaceSize, FaceSize), Color.white);
        portrait.preserveAspect = true;
        portraitInitial = RepairOverlayUI.Text("Customer initial", portraitFrame.transform, Vector2.zero, new Vector2(FaceSize, FaceSize), 22, Ink);
        portraitInitial.alignment = TextAlignmentOptions.Center;
        portraitInitial.fontStyle = FontStyles.Bold;
        // The icons row: three big bold pictures (the device, the drink, a second drink) and the clock.
        iconsRow = RepairOverlayUI.Rect("Items", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -36f), new Vector2(156f, IconSize));
        for (int i = 0; i < itemIcons.Length; i++)
        {
            itemIcons[i] = RepairOverlayUI.Panel("Item " + (i + 1), iconsRow, new Vector2(i * (IconSize + IconGap), 0f), new Vector2(IconSize, IconSize), Color.white);
            itemIcons[i].preserveAspect = true;
            itemInitials[i] = RepairOverlayUI.Text("Initial", itemIcons[i].transform, Vector2.zero, new Vector2(IconSize, IconSize), 22, UiSkin.Paper);
            itemInitials[i].alignment = TextAlignmentOptions.Center;
            itemInitials[i].fontStyle = FontStyles.Bold;
            itemInitials[i].gameObject.SetActive(false);
            itemIcons[i].gameObject.SetActive(false);
        }
        clockIcon = RepairOverlayUI.Panel("Clock", iconsRow, Vector2.zero, new Vector2(IconSize, IconSize), Color.white);
        clockIcon.preserveAspect = true;
        clockInitial = RepairOverlayUI.Text("Initial", clockIcon.transform, Vector2.zero, new Vector2(IconSize, IconSize), 22, UiSkin.Paper);
        clockInitial.alignment = TextAlignmentOptions.Center;
        clockInitial.fontStyle = FontStyles.Bold;
        clockInitial.gameObject.SetActive(false);
        clockIcon.gameObject.SetActive(false);
        // The words: the name, and the fault in a group that fades in.
        foreach (TMP_Text t in new[] { slotText, jobText, portraitInitial }) UiSkin.UseFont(t);
        if (slotText != null) { slotText.fontStyle = FontStyles.Bold; slotText.richText = false; }
        if (jobText != null)
        {
            wordsGroup = jobText.gameObject.GetComponent<CanvasGroup>();
            if (wordsGroup == null) wordsGroup = jobText.gameObject.AddComponent<CanvasGroup>();
            wordsGroup.blocksRaycasts = false; wordsGroup.interactable = false;
        }
        // Drawn in this order: the foot and the tape under the paper's parts? No: the tape over the paper's corner, the foot under.
        foot.transform.SetAsFirstSibling();
        if (background != null) background.transform.SetAsFirstSibling();
        tape.transform.SetAsLastSibling();
    }

    private static void Fill(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }

    private void RefreshIdentity(float patience)
    {
        EnsureBuilt();
        var identity = Target.Identity;
        Sprite face = identity != null ? identity.PortraitAt(patience) : null;
        portrait.sprite = face;
        portrait.enabled = face != null;
        portraitInitial.gameObject.SetActive(face == null);
        string name = Target.CustomerName;
        portraitInitial.text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        // The disc behind the face (seen round a portrait's edges, and under the initial): the customer's colour, pale; red-rimmed under a quarter.
        Color theme = identity != null && identity.ThemeColor != Color.white ? identity.ThemeColor : Target.JobColor;   // a walk-in with no mood colour wears the job's
        portraitFrame.color = patience <= .25f ? Color.Lerp(new Color(.98f, .86f, .84f), theme, .25f) : Color.Lerp(new Color(.93f, .95f, .93f), theme, .25f);   // 26 Oct 2026 (his Mac): a paler face, so the dark initial reads (a lilac face read 3.4:1)
        // 26 Oct 2026 (Readability; the HUD lab: "'V' 24.3 px, 3.4:1" on Valerie's ticket). With no portrait the initial IS the face,
        // so it sits on a solid disc, not a tint of the customer's colour (the tape still carries it): the paper (Ink on it 15.5:1),
        // or under a quarter the pale red with a deep red letter (6.3:1; the skin's red on it was 3.9). The ring says the rest.
        if (face == null) portraitFrame.color = patience <= .25f ? UiSkin.RedSoft : UiSkin.Paper;
        portraitInitial.color = patience <= .25f ? UrgentInitial : Ink;
        if (ringTrack != null) ringTrack.color = patience <= .25f ? new Color(UiSkin.Red.r, UiSkin.Red.g, UiSkin.Red.b, .35f) : UiSkin.Track;
    }

    // The items as icons: the device, then each drink; the clock once patience is under half (red and pulsing under a quarter).
    private void RefreshItems(float patience, bool onCall)
    {
        Job record = Target.Record;
        int n = 0;
        string device = record != null && record.kind == JobKind.Repair ? NoteIcons.ForDevice(record.deviceName) : "";
        if (record != null && record.kind == JobKind.Repair && Target.ActiveJob != null && !string.IsNullOrEmpty(record.deviceName)) Item(n++, device.Length > 0 ? device : "item_" + record.deviceName, record.deviceName);
        if (Target.HasDrinkOrder && Target.WantedDrink != null) Item(n++, DrinkIconName(Target.WantedDrink), Target.WantedDrink.drinkName);
        for (int i = n; i < itemIcons.Length; i++) { itemIcons[i].gameObject.SetActive(false); itemNames[i] = ""; }
        bool clock = patience < .5f && !(form == Form.Narrow && onCall);
        if (clockIcon.gameObject.activeSelf != clock) clockIcon.gameObject.SetActive(clock);
        if (clock)
        {
            float size = form == Form.Chip ? ChipIconSize : IconSize;
            clockIcon.rectTransform.anchoredPosition = new Vector2(n * (size + IconGap), 0f);
            Sprite s = NoteIcons.SpriteOf(NoteIcons.ItemClock);
            clockIcon.sprite = s;
            bool urgent = patience <= .25f;
            float pulse = urgent ? .5f + .5f * (1f - Mathf.Cos(UiClock.Now * Mathf.PI * 2f)) * .5f : 1f;
            clockIcon.color = s != null ? (urgent ? Color.Lerp(Color.white, new Color(1f, .45f, .4f), .6f + .4f * pulse) : Color.white)
                                        : (urgent ? Color.Lerp(UiSkin.Red, UrgentInk, pulse) : UiSkin.InkSoft);
            clockInitial.gameObject.SetActive(s == null);
            clockInitial.text = "!";
        }
        float rowWidth = (n + (clock ? 1 : 0)) * ((form == Form.Chip ? ChipIconSize : IconSize) + IconGap) - IconGap;
        // Narrow: the row is centred under the name.
        if (form == Form.Narrow) iconsRow.anchoredPosition = new Vector2((cardWidth - Mathf.Max(0f, rowWidth)) * .5f, -90f);
    }

    private void Item(int i, string icon, string label)
    {
        Image image = itemIcons[i];
        float size = form == Form.Chip ? ChipIconSize : IconSize;
        image.rectTransform.anchoredPosition = new Vector2(i * (size + IconGap), 0f);
        image.rectTransform.sizeDelta = new Vector2(size, size);
        itemInitials[i].rectTransform.sizeDelta = new Vector2(size, size);
        if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
        if (itemNames[i] == icon && image.sprite != null) return;
        itemNames[i] = icon;
        Sprite s = NoteIcons.SpriteOf(icon);
        image.sprite = s;
        if (s != null)
        {
            image.color = Color.white;
            UiSkin.Paint(image.rectTransform, Color.white, 0f);
            image.sprite = s;
            itemInitials[i].gameObject.SetActive(false);
        }
        else
        {
            // Without the sheet: a dark chip with the item's initial.
            UiSkin.Round(image, 8f);
            image.color = new Color(Ink.r, Ink.g, Ink.b, .85f);
            itemInitials[i].text = string.IsNullOrEmpty(label) ? "?" : label.Trim().Substring(0, 1).ToUpperInvariant();
            itemInitials[i].gameObject.SetActive(true);
        }
    }

    // The tab's lines: the device and the fault as a sentence ("Dirty lens and film path"); a drink job's subject and detail
    // (CustomerBrain.TabLines).
    private string Lines()
    {
        Job record = Target.Record;
        if (record == null) return "";
        if (record.kind == JobKind.Drink) return Target.TabLines;
        return SentenceCase(record.faultDescription);
    }

    /// <summary>A Title Case label as a sentence: "Dirty Lens and Film Path" is "Dirty lens and film path" (the words a person
    /// keeps capitalised, like USB, stay).</summary>
    public static string SentenceCase(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "";
        string s = ReputationRules.InSentence(label.Trim());
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    }

    /// <summary>The drink's icon + its name (19 Oct 2026, her: "it needs to be bigger, huh? With images?"); the plain
    /// "+ Latte" where the icon sheet isn't there. (The drinks machine's line and the details still use it.)</summary>
    public static string DrinkLine(DrinkDefinition drink)
    {
        if (drink == null) return "";
        string icon = PadInput.Icon(DrinkIconName(drink), "");
        return (icon.Length > 0 ? icon + " " : "+ ") + drink.drinkName;
    }

    /// <summary>The icon sheet's name for a drink, from its name: drink_latte, drink_coffee, drink_tea, drink_espresso,
    /// drink_hotchocolate, drink_americano ("" for a drink the sheet doesn't know).</summary>
    public static string DrinkIconName(DrinkDefinition drink)
    {
        if (drink == null || string.IsNullOrEmpty(drink.drinkName)) return "";
        string n = drink.drinkName.ToLowerInvariant().Replace(" ", "");
        if (n.Contains("latte")) return "drink_latte";
        if (n.Contains("espresso")) return "drink_espresso";
        if (n.Contains("americano")) return "drink_americano";
        if (n.Contains("chocolate") || n.Contains("cocoa")) return "drink_hotchocolate";
        if (n.Contains("tea")) return "drink_tea";
        if (n.Contains("coffee")) return "drink_coffee";
        return "";
    }

    // ---------- the three forms ----------

    private void ConfigureLayout()
    {
        EnsureBuilt();
        if (form == laidOut && Mathf.Approximately(cardWidth, laidOutWidth)) return;
        laidOut = form;
        laidOutWidth = cardWidth;
        bool chip = form == Form.Chip, wide = form == Form.Wide;
        float ringSize = chip ? ChipRingSize : RingSize, faceSize = chip ? ChipFaceSize : FaceSize;
        // The ring and the face.
        Vector2 ringAt = chip ? new Vector2(4f, -4f) : wide ? new Vector2(10f, -10f) : new Vector2((cardWidth - ringSize) * .5f, -8f);
        Place(ring, ringAt, new Vector2(ringSize, ringSize));
        Place(ringTrack.rectTransform, Vector2.zero, new Vector2(ringSize, ringSize));
        Place(portraitFrame.rectTransform, new Vector2((ringSize - faceSize) * .5f, -(ringSize - faceSize) * .5f), new Vector2(faceSize, faceSize));
        Place(portrait.rectTransform, Vector2.zero, new Vector2(faceSize, faceSize));
        Place(portraitInitial.rectTransform, Vector2.zero, new Vector2(faceSize, faceSize));
        portraitInitial.fontSize = chip ? 22f : 24f;   // 26 Oct 2026 (his Mac): 24 on the dockets; (Readability) 22 on the bench's chip, was 17
        // The foot, the tape.
        Place(foot.rectTransform, new Vector2(0f, -(cardHeight - FootHeight)), new Vector2(cardWidth, FootHeight));
        foot.gameObject.SetActive(!chip);
        Place(tape.rectTransform, chip ? new Vector2(cardWidth - 52f, 9f) : new Vector2(8f, 7f), new Vector2(40f, 13f));
        tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, chip ? -7f : -7f);
        // The name.
        if (slotText != null)
        {
            slotText.gameObject.SetActive(!chip);
            slotText.enableAutoSizing = false;
            slotText.fontSize = wide ? NameSize : NarrowNameSize;
            slotText.color = Ink;
            slotText.overflowMode = TextOverflowModes.Ellipsis;
            slotText.textWrappingMode = TextWrappingModes.NoWrap;
            slotText.alignment = wide ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            slotText.raycastTarget = false;
            Place(slotText.rectTransform, wide ? new Vector2(70f, -6f) : new Vector2(4f, -62f), wide ? new Vector2(cardWidth - 80f, 30f) : new Vector2(cardWidth - 8f, 28f));
        }
        // The fault's words (wide only).
        if (jobText != null)
        {
            jobText.gameObject.SetActive(wide);
            jobText.enableAutoSizing = false;
            jobText.fontSize = FaultSize;
            jobText.color = Ink;
            jobText.fontStyle = UiSkin.SemiBold;
            jobText.lineSpacing = -6f;
            jobText.overflowMode = TextOverflowModes.Ellipsis;
            jobText.textWrappingMode = TextWrappingModes.Normal;
            jobText.alignment = TextAlignmentOptions.TopLeft;
            jobText.raycastTarget = false;
            Place(jobText.rectTransform, new Vector2(12f, -80f), new Vector2(cardWidth - 24f, 54f));
        }
        // The icons row.
        float iconSize = chip ? ChipIconSize : IconSize;
        Place(iconsRow, chip ? new Vector2(ringSize + 10f, -(cardHeight - iconSize) * .5f) : wide ? new Vector2(70f, -36f) : new Vector2(4f, -90f), new Vector2(3f * (iconSize + IconGap), iconSize));
        for (int i = 0; i < itemIcons.Length; i++)
        {
            Place(itemIcons[i].rectTransform, new Vector2(i * (iconSize + IconGap), 0f), new Vector2(iconSize, iconSize));
            Place(itemInitials[i].rectTransform, Vector2.zero, new Vector2(iconSize, iconSize));
            itemInitials[i].fontSize = 22f;   // 26 Oct 2026 (Readability): was 16 on a chip, 20 on a docket (the HUD lab's floor is 21.5)
        }
        Place(clockIcon.rectTransform, Vector2.zero, new Vector2(iconSize, iconSize));
        Place(clockInitial.rectTransform, Vector2.zero, new Vector2(iconSize, iconSize));
        // The support call's band.
        if (callLine != null)
        {
            if (chip)
            {
                Place(callLine, new Vector2(ringSize + 8f, -3f), new Vector2(cardWidth - ringSize - 14f, 24f));
                callIcon.rectTransform.anchoredPosition = new Vector2(12f, -12f); callIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);
                callState.gameObject.SetActive(false);
                Place(callSeconds.rectTransform, new Vector2(26f, -2f), new Vector2(cardWidth - ringSize - 44f, 20f));
            }
            else if (wide)
            {
                Place(callLine, new Vector2(12f, -82f), new Vector2(cardWidth - 24f, 36f));
                callIcon.rectTransform.anchoredPosition = new Vector2(18f, -18f); callIcon.rectTransform.sizeDelta = new Vector2(28f, 28f);
                callState.gameObject.SetActive(true);
                Place(callState.rectTransform, new Vector2(38f, -2f), new Vector2(cardWidth - 150f, 32f));
                Place(callSeconds.rectTransform, new Vector2(cardWidth - 110f, -2f), new Vector2(86f, 32f));
                callSeconds.alignment = TextAlignmentOptions.MidlineRight;
            }
            else
            {
                Place(callLine, new Vector2(4f, -90f), new Vector2(cardWidth - 8f, 36f));
                callIcon.rectTransform.anchoredPosition = new Vector2(18f, -18f); callIcon.rectTransform.sizeDelta = new Vector2(28f, 28f);
                callState.gameObject.SetActive(false);
                Place(callSeconds.rectTransform, new Vector2(36f, -2f), new Vector2(cardWidth - 44f, 32f));
                callSeconds.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
    }

    public void SetCardSize(float width, float height)
    {
        float nextWidth = form == Form.Chip ? width : Mathf.Max(NarrowWidth, width), nextHeight = form == Form.Chip ? height : Mathf.Max(Height, height);
        if (Mathf.Approximately(cardWidth, nextWidth) && Mathf.Approximately(cardHeight, nextHeight)) return;
        cardWidth = nextWidth; cardHeight = nextHeight;
        ConfigureLayout();
        Refresh();
    }

    private void BuildCallLine()
    {
        Image callBand = RepairOverlayUI.Panel("Support call obligation", transform, new Vector2(12f, -82f), new Vector2(cardWidth - 24f, 36f), UiSkin.Band);
        UiSkin.Round(callBand, 8f);
        callLine = callBand.rectTransform;
        var rect = RepairOverlayUI.Rect("Phone", callLine, new Vector2(0, 1), new Vector2(.5f, .5f), new Vector2(18f, -18f), new Vector2(28f, 28f));
        callIcon = rect.gameObject.AddComponent<SupportCallIcon>(); callIcon.raycastTarget = false;
        callState = RepairOverlayUI.Text("Call state", callLine, new Vector2(38f, -2f), new Vector2(cardWidth - 150f, 32f), 22, Color.white);
        callState.fontStyle = FontStyles.Bold;
        callSeconds = RepairOverlayUI.Text("Countdown", callLine, new Vector2(cardWidth - 110f, -2f), new Vector2(86f, 32f), 22, UiSkin.BandFaint);
        callSeconds.fontStyle = FontStyles.Bold;
        callState.textWrappingMode = callSeconds.textWrappingMode = TextWrappingModes.NoWrap;
        foreach (TMP_Text t in new[] { callState, callSeconds }) { t.enableAutoSizing = true; t.fontSizeMin = 22f; t.fontSizeMax = 24f; }   // 26 Oct 2026 (Readability): 16-22 to 22-24
        UiSkin.UseFont(callState);
        UiSkin.UseFont(callSeconds);
        laidOut = (Form)(-1);
        ConfigureLayout();
    }

    private void RefreshDetails(HoldCallJob call)
    {
        if (detailPanel == null)
        {
            Image detailBand = RepairOverlayUI.Panel("Full ticket details", transform, Vector2.zero, new Vector2(320, 80), UiSkin.Band);
            UiSkin.Round(detailBand, UiSkin.Radius);
            detailPanel = detailBand.rectTransform;
            detailText = RepairOverlayUI.Text("Full task text", detailPanel, new Vector2(12, -10), new Vector2(296, 60), 22, UiSkin.BandText);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.richText = true;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            UiSkin.UseFont(detailText);
            // Draw above neighbours without a raycaster or input capture.
            var layer = detailPanel.gameObject.AddComponent<Canvas>();
            var parentCanvas = GetComponentInParent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 1 : 1;
        }
        detailPanel.gameObject.SetActive(true);
        // The whole job in words: the name, the device and the fault (as a sentence) and the drink; a call's state.
        bool delivered = Target.Record != null && Target.Record.kind == JobKind.Repair && Target.ActiveJob == null;
        string obligations = delivered ? "" : FullLines();
        if (Target.HasDrinkOrder && Target.WantedDrink != null && (delivered || Target.Record == null || Target.Record.kind == JobKind.Repair))
            obligations += (obligations.Length > 0 ? "\n" : "") + "+ " + Target.WantedDrink.drinkName;
        detailText.text = Target.CustomerName + "\n" + obligations
            + (call != null ? "\n" + callState.text + " · " + callSeconds.text : "");
        float height = detailText.GetPreferredValues(detailText.text, 296, 0).y + 20;
        Place(detailPanel, new Vector2((cardWidth - 320) * .5f, -cardHeight - 5), new Vector2(320, height));
        Place(detailText.rectTransform, new Vector2(12, -10), new Vector2(296, height - 20));
        KeepDetailsOnCanvas();
    }

    // The details' words: the device and its fault; a drink job's subject and detail.
    private string FullLines()
    {
        Job record = Target.Record;
        if (record == null) return "";
        if (record.kind == JobKind.Drink) return Target.TabLines;
        return record.deviceName + "\n" + SentenceCase(record.faultDescription);
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
