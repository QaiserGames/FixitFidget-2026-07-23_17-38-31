using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The six recipes are printed on the machine. This small caption describes only
// the current action; physical hands and the centred reticle carry the interaction.
//
// 19 OCT 2026 (playtest 7, his girlfriend: dyslexic, on a PlayStation pad; the reviewers' day-ui findings):
//   * no percentage anywhere (her: "Oh, so you put the percentage of how much it fills?"; him: "it's for my testing"): the
//     action line says "Pouring latte" and a small ring beside it fills with the pour (nothing to read);
//   * the controls are two short lines, icon and word, never cut off (the reviewers: "Left click · left hand ... Esc or
//     walk · step..." ended in an ellipsis at 1080p);
//   * with a filled cup in hand the line says who it is for ("Take the coffee to Beatriz"), not "Take a cup" (the reviewers:
//     "a newcomer will try to take another cup");
//   * the cup stack glows while the step is "take a cup" (white cups on a white counter were the least visible thing in
//     the frame; the reviewers: "exactly as the bench glows its tool");
//   * bigger type: the title 24, the action 24, the controls 20 (nothing a player reads under 20 px).
[RequireComponent(typeof(StationInteractable))]
public sealed class BeverageStation : MonoBehaviour
{
    private PlayerInteractor player;
    private PlayerCarry carry;
    private ConversationController dialogue;
    private StationInteractable station;
    private Canvas canvas;
    private CanvasScaler scaler;
    private TMP_Text title, action, controls;
    private Image ringTrack, ring;
    private BeverageSlot[] slots;
    private BeverageCupSupply[] supplies;
    private CupStack[] stacks;

    // The card and its lines (reference pixels before the scaler's factor; 1.2 x at 1080p).
    const float CardWidth = 600f, CardHeight = 176f, TitleSize = 24f, ActionSize = 24f, ControlsSize = 20f, RingSize = 30f;

    /// <summary>The action line as it reads now (labs).</summary>
    public string ActionText => action != null ? action.text : "";
    /// <summary>The controls' lines as they read now (labs).</summary>
    public string ControlsText => controls != null ? controls.text : "";
    /// <summary>The pour's ring is up, and how full it is (labs).</summary>
    public bool RingShowing => ring != null && ring.gameObject.activeInHierarchy;
    public float RingFill => ring != null ? ring.fillAmount : 0f;
    /// <summary>The cup stack is glowing (the step is "take a cup"; labs).</summary>
    public bool StackGlowing { get; private set; }

    private void Start()
    {
        station = GetComponent<StationInteractable>();
        player = FindAnyObjectByType<PlayerInteractor>();
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        dialogue = player != null ? player.GetComponent<ConversationController>() : null;
        slots = GetComponentsInChildren<BeverageSlot>();
        supplies = GetComponentsInChildren<BeverageCupSupply>();
        stacks = GetComponentsInChildren<CupStack>();
        canvas = RepairOverlayUI.Canvas("Drink station action", transform, 12);
        scaler = canvas.GetComponent<CanvasScaler>();
        Image cardImage = RepairOverlayUI.Panel("Current action", canvas.transform, Vector2.zero,
            new Vector2(CardWidth, CardHeight), RepairOverlayUI.Background);
        UiSkin.Round(cardImage, UiSkin.Radius);
        var card = cardImage.rectTransform;
        card.anchorMin = card.anchorMax = new Vector2(.5f, 0);
        card.pivot = new Vector2(.5f, 0);
        card.anchoredPosition = new Vector2(0, 20);
        title = RepairOverlayUI.Text("Focused object", card, new Vector2(12, -8),
            new Vector2(CardWidth - 24, 30), TitleSize, Color.white);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        action = RepairOverlayUI.Text("Next action", card, new Vector2(12 + RingSize + 6, -40),
            new Vector2(CardWidth - 24 - 2 * (RingSize + 6), 62), ActionSize, Color.white);
        action.alignment = TextAlignmentOptions.Center;
        action.fontStyle = UiSkin.SemiBold;
        action.textWrappingMode = TextWrappingModes.Normal;     // two lines at most; never cut off
        action.overflowMode = TextOverflowModes.Overflow;
        action.richText = true;
        controls = RepairOverlayUI.Text("Controls", card, new Vector2(12, -106),
            new Vector2(CardWidth - 24, 60), ControlsSize, RepairOverlayUI.Muted);
        controls.alignment = TextAlignmentOptions.Center;
        controls.textWrappingMode = TextWrappingModes.NoWrap;    // two lines of its own, never wrapped or cut (ControlsLines)
        controls.overflowMode = TextOverflowModes.Overflow;
        controls.richText = true;                                  // the icons are sprite tags
        controls.text = ControlsLines(true);
        BuildRing(card);
        Refresh();
    }

    // The pour's ring: a faint track and a gold ring that fills clockwise from the top, right of the action line.
    private void BuildRing(RectTransform card)
    {
        Sprite ringSprite = RingSprite();
        ringTrack = RepairOverlayUI.Panel("Pour ring track", card, new Vector2(CardWidth - 12 - RingSize, -41), new Vector2(RingSize, RingSize), UiSkin.BandLine);
        ringTrack.sprite = ringSprite;
        ringTrack.type = Image.Type.Simple;
        ringTrack.preserveAspect = true;
        ring = RepairOverlayUI.Panel("Pour ring", card, new Vector2(CardWidth - 12 - RingSize, -41), new Vector2(RingSize, RingSize), UiSkin.Gold);
        ring.sprite = ringSprite;
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0f;
        ringTrack.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);
    }

    private static Sprite ringSprite;

    // A ring, drawn once: 64 px, a 9 px stroke, its edges smoothed.
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 64;
        const float outer = 31f, inner = 22f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Pour ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[size * size];
        float centre = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(centre, centre));
                float a = Mathf.Clamp01(outer - d + .5f) * Mathf.Clamp01(d - inner + .5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        ringSprite.name = "Pour ring";
        ringSprite.hideFlags = HideFlags.DontSave;
        return ringSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ringSprite = null;

    private void LateUpdate() => Refresh();

    // In first person the card shows while the crosshair is on the dispenser (stations as reach, playtest 3), and
    // stays a moment after it slips off, so it doesn't flicker as the crosshair crosses the gaps between controls.
    private const float Linger = .5f;
    private float aimedAt = -10f;
    private bool shownPad;
    private bool shownCloseUp;
    private bool controlsWritten;

    private void Refresh()
    {
        if (canvas == null) return;
        if (player != null && player.AimingAtDrinks) aimedAt = Time.unscaledTime;
        bool here = player != null && (player.CurrentStation == station
            || player.CurrentStation == null && Time.unscaledTime - aimedAt < Linger);
        bool show = here && Time.timeScale > 0
            && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation);
        canvas.gameObject.SetActive(show);
        // The stack's glow follows the step whether or not the card is up (from above, the card is up only at the station).
        var carriedCup = carry != null ? carry.Carried as DrinkJob : null;
        bool takeACup = show && carry != null && carry.HasSpace && carriedCup == null
            && ShopInventory.Instance != null && ShopInventory.Instance.Cups > 0;
        SetStackGlow(takeACup);
        if (!show) return;
        scaler.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), .6f, 1.4f);
        bool closeUp = player.CurrentStation == station;
        bool pad = PadInput.UsingPad;
        if (!controlsWritten || pad != shownPad || closeUp != shownCloseUp)
        {
            controlsWritten = true; shownPad = pad; shownCloseUp = closeUp;
            controls.text = ControlsLines(closeUp);
        }
        var focused = player.Focused;
        var control = focused != null ? focused.GetComponentInParent<BeverageControl>() : null;
        var supply = focused != null ? focused.GetComponentInParent<BeverageCupSupply>() : null;
        var focusedCup = focused != null ? focused.GetComponentInParent<DrinkJob>() : null;
        BeverageSlot selected = control != null ? control.slot : null;
        if (selected == null && focusedCup != null)
            foreach (var slot in slots)
                if (slot.Cup == focusedCup) { selected = slot; break; }

        title.text = selected != null && selected.drink != null ? selected.drink.drinkName
            : supply != null ? supply.discard ? "Cup return / discard" : "Cup stack" : "Drink station";
        bool pouring = selected != null && selected.IsPouring;
        if (pouring)
        {
            // "Pouring latte", and the ring beside it fills with the pour: no number (her question, his "testing" answer).
            string line = "Pouring " + selected.drink.drinkName;
            if (action.text != line) action.text = line;
            ring.fillAmount = selected.Progress;
        }
        else if (carriedCup != null && !carriedCup.IsEmpty && !(supply != null && supply.discard) && !(selected != null && selected.Cup != null && !selected.Cup.IsEmpty))
        {
            // A filled cup in hand: who it is for, whatever the crosshair is on (the reviewers: with a full cup in hand the
            // card still said "Take a cup"). The cup return and a filled cup in a slot keep their own prompts.
            CustomerBrain who = WhoWants(carriedCup);
            action.text = who != null ? $"Take the {carriedCup.Drink.drinkName} to {who.CustomerName}"
                : $"Nobody has ordered a {carriedCup.Drink.drinkName} · the cup return takes it back";
        }
        else if (focused != null && (control != null || supply != null || selected != null))
            action.text = focused.Prompt;
        else if (carriedCup != null && carriedCup.IsEmpty)
            action.text = "Place the cup under a nozzle, then press its named paddle";
        else if (carry != null && !carry.HasSpace)
            action.text = "Both hands full · serve a drink or set an item down";
        else action.text = "Aim at the cup stack to take a cup";
        if (ring.gameObject.activeSelf != pouring) { ring.gameObject.SetActive(pouring); ringTrack.gameObject.SetActive(pouring); }
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        SetStackGlow(false);
    }

    // The customer waiting for the drink in hand (CanReceiveDrink reads the player's carry); the nearest when several are.
    private CustomerBrain wanting;
    private float wantingAskedAt = -1f;
    private DrinkJob wantingFor;

    private CustomerBrain WhoWants(DrinkJob cup)
    {
        if (cup != wantingFor || Time.unscaledTime - wantingAskedAt > .5f || wanting == null)
        {
            wantingFor = cup;
            wantingAskedAt = Time.unscaledTime;
            wanting = null;
            float nearest = float.PositiveInfinity;
            Vector3 here = player != null ? player.transform.position : transform.position;
            foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            {
                if (brain == null || !brain.CanReceiveDrink) continue;
                float d = (brain.transform.position - here).sqrMagnitude;
                if (d < nearest) { nearest = d; wanting = brain; }
            }
        }
        return wanting;
    }

    // Every frame: the glow breathes while it is on (CupStack.Glow), and goes the frame the step changes.
    private void SetStackGlow(bool on)
    {
        StackGlowing = on;
        if (supplies != null)
            foreach (BeverageCupSupply s in supplies)
                if (s != null && !s.discard) s.SetGlow(on);
        if (stacks != null)
            foreach (CupStack s in stacks)
                if (s != null) s.SetGlow(on);
    }

    // Two lines, icon and word, a pair of hints each: the hands, then use and (in the close-up) the way out. The close-up is
    // left by walking away (or B / Esc); in first person there is nothing to leave.
    private static string ControlsLines(bool closeUp)
    {
        string hands = $"{ControlHints.LeftHand}  left hand      {ControlHints.RightHand}  right hand";
        string use = $"{ControlHints.Interact}  use" + (closeUp ? $"      {ControlHints.Back}  or walk: step back" : "");
        return hands + "\n" + use;
    }
}
