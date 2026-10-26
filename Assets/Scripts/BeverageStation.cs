using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE DRINKS MACHINE'S PROMPT
//
// The six recipes are printed on the machine. This prompt says only what the button does now; the physical hands and the
// centred reticle carry the rest. It shows in the machine's close-up from above (E at it: StationCloseUp) and, in first
// person, while the crosshair is on one of its controls (stations as reach, playtest 3).
//
// 19 OCT 2026 (playtest 7, his girlfriend: dyslexic, on a PlayStation pad; the reviewers' day-ui findings), still true:
//   * no percentage anywhere (her: "Oh, so you put the percentage of how much it fills?"; him: "it's for my testing"): the
//     line says "Pouring Latte" and a small gold ring beside it fills with the pour (nothing to read);
//   * nothing is cut off (the reviewers: "Left click · left hand ... Esc or walk · step..." ended in an ellipsis at 1080p):
//     every line is one line that never wraps and overflows its wide box rather than clip;
//   * with a filled cup in hand the line says who it is for ("Take the Coffee to Beatriz"), not "Take a cup" (the
//     reviewers: "a newcomer will try to take another cup");
//   * the cup stack glows while the step is "take a cup" (white cups on a white counter were the least visible thing in
//     the frame; the reviewers: "exactly as the bench glows its tool");
//   * nothing a player reads under 20 px.
//
// 23 OCT 2026 (his own playtest of 22 Oct). Of the café's "Prepare drinks" line: "that does look so clean. I love that
// look." Of the machine: "I just don't like it when we're working on the drink station, because that just looks really
// clustered as well. Looks really blocky and big." So the dark card is gone (720 x 210 px at 1080p, four lines: the
// thing's name, the action, the two hands, "use"), and the machine speaks in the café prompt's own look: ShopUI's prompt
// line, which is the skin's font at its semibold weight, its cream, its one shadow and no plate, at the bottom middle, at
// the same height:
//   * ONE LINE, the button's icon first: "(A) Take a cup", "(A) Pour Latte", 34 px. The icon is a button only when that
//     button does it right here. A line that says what to do next has none ("Look at the cup stack to take a cup"). A line
//     about the drink in hand has the drink's picture, as the ticket and the called order do ("Take the Coffee to
//     Beatriz": A serves it at Beatriz, where the café's own line says so; at the machine A would take a cup or pour). While
//     it pours, the gold ring is in the icon's place;
//   * THE HANDS ONLY WHEN THEY MATTER: when either hand could take a thing, or put a cup back, "(LB) left  (RB) right" in a
//     small line under it, 24 px; otherwise nothing. When only one hand can, its button is the line's icon;
//   * NO NAME: the "Cup stack" / "Latte" over the action is dropped. The line names its thing ("Take a cup", "Pour Latte",
//     "under Latte"), the paddles print theirs, and what the crosshair is on is outlined (a cup pad) or glows (the stack);
//   * STEP BACK last and smallest, in the close-up only (in first person there is nothing to leave): "(B) Step back", 22 px
//     on the dark plate the café's controls strip uses, where the strip sits (the strip steps aside in every close-up:
//     ShopUI);
//   * ONE LINE AT A TIME: ShopUI clears its own prompt and its controls strip while the machine is in use
//     (PlayerInteractor.UsingDrinks), and in the half second this line stays after the crosshair slips off the machine onto
//     nothing (LineUp). Should ShopUI ever not step aside, this line gives way the moment the café's line has words of its
//     own ("In hand ..."): two lines never overlap.
// ---------------------------------------------------------------------------
[RequireComponent(typeof(StationInteractable))]
public sealed class BeverageStation : MonoBehaviour
{
    private PlayerInteractor player;
    private PlayerCarry carry;
    private ConversationController dialogue;
    private StationInteractable station;
    private ShopUI shop;
    private Canvas canvas;
    private TMP_Text action, hands, stepBack;
    private RectTransform stepBackPlate;
    private Image ringTrack, ring;
    private BeverageSlot[] slots;
    private BeverageCupSupply[] supplies;
    private CupStack[] stacks;

    // Sizes, reference pixels of the skin's 1920 x 1080 canvas (so 1080p pixels on any screen). The action line is the top
    // of the readable-words rule's 28-34 for a screen's main line (the café's own prompt is the scene's 36); the hands are a
    // secondary line; Step back is the café strip's 22, on its plate. The ring is as tall as an icon beside 34 px words.
    const float ActionSize = 34f, HandsSize = 24f, StepBackSize = 22f, RingSize = 36f, RingGap = 12f;
    // Places, from the bottom middle: the action line's middle where the café's prompt line's words are (about 200 px up at
    // 1080p in the labs' photos), the hands under it, clear of its descenders and icons; Step back on the plate the café's
    // controls strip draws (ShopUI's StripBottom 20, StripHeight 30, StripPadX 18, StripPadY 7: the same strip, on purpose).
    // COUPLED TO ShopUI, BY HAND: ActionMiddle is read off photos of the café's prompt, whose place is the scene's, and the
    // strip's four numbers copy ShopUI's private constants (ShopUI.cs, beside StripSize). If the HUD moves the prompt or the
    // strip, move these with it (or give ShopUI read-only accessors and read them here).
    const float ActionMiddle = 200f, HandsMiddle = 154f, LineWidth = 1600f;
    const float StripBottom = 20f, StripHeight = 30f, StripPadX = 18f, StripPadY = 7f;

    /// <summary>The action line as it reads now, its icon's sprite tag first when it has one (labs).</summary>
    public string ActionText => action != null ? action.text : "";
    /// <summary>
    /// The controls as they read now (labs): the hands line while the hand choice matters, and Step back in the close-up,
    /// one per line; "" when neither is up. (Until 23 Oct 2026: the card's two lines, the hands and "use", always.)
    /// </summary>
    public string ControlsText
    {
        get
        {
            string h = HandsShowing ? hands.text : "", s = StepBackShowing ? stepBack.text : "";
            return h.Length > 0 && s.Length > 0 ? h + "\n" + s : h + s;
        }
    }
    /// <summary>The two hands are offered under the line: either hand could take the thing, or put a cup back (labs).</summary>
    public bool HandsShowing => hands != null && hands.gameObject.activeInHierarchy;
    /// <summary>Step back is offered (the close-up from above; labs).</summary>
    public bool StepBackShowing => stepBackPlate != null && stepBackPlate.gameObject.activeInHierarchy;
    /// <summary>The pour's ring is up, and how full it is (labs).</summary>
    public bool RingShowing => ring != null && ring.gameObject.activeInHierarchy;
    public float RingFill => ring != null ? ring.fillAmount : 0f;
    /// <summary>The cup stack is glowing (the step is "take a cup"; labs).</summary>
    public bool StackGlowing { get; private set; }
    /// <summary>A drinks line is on the screen now: the café's prompt line (ShopUI) may step aside for it.</summary>
    public static bool LineUp => lineOwner != null;
    private static BeverageStation lineOwner;

    private void Start()
    {
        station = GetComponent<StationInteractable>();
        player = FindAnyObjectByType<PlayerInteractor>();
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        dialogue = player != null ? player.GetComponent<ConversationController>() : null;
        shop = FindAnyObjectByType<ShopUI>();
        slots = GetComponentsInChildren<BeverageSlot>();
        supplies = GetComponentsInChildren<BeverageCupSupply>();
        stacks = GetComponentsInChildren<CupStack>();
        canvas = UiSkin.ScreenCanvas("Drink station action", transform, 12);
        Transform root = canvas.transform;
        BuildRing(root);
        action = Line("Next action", root, ActionSize, ActionMiddle);
        hands = Line("Hands", root, HandsSize, HandsMiddle);
        hands.gameObject.SetActive(false);
        BuildStepBack(root);
        Refresh();
    }

    // A line in the café prompt's look (ShopUI dresses its prompt so: the skin's font, its cream, its one shadow; semibold, the
    // weight of the prompt's words in the labs' photos), centred at the bottom middle, never wrapped and never cut (it
    // overflows its wide box).
    private static TMP_Text Line(string name, Transform parent, float size, float middle)
    {
        TextMeshProUGUI text = UiSkin.Text(name, parent, size, UiSkin.BandText, TextAlignmentOptions.Center, UiSkin.SemiBold);
        Place(text.rectTransform, middle, new Vector2(LineWidth, size * 2f));
        UiSkin.Shadowed(text);
        return text;
    }

    // Anchored at the bottom middle, its middle `middle` reference pixels up.
    private static void Place(RectTransform rect, float middle, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, middle);
        rect.sizeDelta = size;
    }

    // The pour's ring: a faint track and a gold ring that fills clockwise from the top, in the icon's place while it pours.
    private void BuildRing(Transform parent)
    {
        Sprite sprite = RingSprite();
        ringTrack = RingImage("Pour ring track", parent, sprite, UiSkin.BandLine);
        ring = RingImage("Pour ring", parent, sprite, UiSkin.Gold);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0f;
        ringTrack.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);
    }

    private static Image RingImage(string name, Transform parent, Sprite sprite, Color colour)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, ActionMiddle, new Vector2(RingSize, RingSize));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    // Step back: the café's controls strip's look (its 22 px semibold words on the skin's dark rounded plate), in its place.
    private void BuildStepBack(Transform parent)
    {
        stepBackPlate = new GameObject("Step back plate", typeof(RectTransform)).GetComponent<RectTransform>();
        stepBackPlate.SetParent(parent, false);
        stepBackPlate.anchorMin = stepBackPlate.anchorMax = new Vector2(.5f, 0f);
        stepBackPlate.pivot = new Vector2(.5f, 0f);
        stepBackPlate.anchoredPosition = new Vector2(0f, StripBottom - StripPadY);
        stepBackPlate.sizeDelta = new Vector2(StripPadX * 2f, StripHeight + StripPadY * 2f);
        UiSkin.Paint(stepBackPlate, UiSkin.Band, UiSkin.Radius);
        stepBack = UiSkin.Text("Step back", stepBackPlate, StepBackSize, UiSkin.BandText, TextAlignmentOptions.Center, UiSkin.SemiBold);
        RectTransform words = stepBack.rectTransform;
        words.anchorMin = Vector2.zero;
        words.anchorMax = Vector2.one;
        words.offsetMin = Vector2.zero;
        words.offsetMax = Vector2.zero;
        stepBackPlate.gameObject.SetActive(false);
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
    private static void ResetStatics()
    {
        ringSprite = null;
        lineOwner = null;
    }

    private void LateUpdate() => Refresh();

    // In first person the line shows while the crosshair is on the machine (stations as reach, playtest 3), and stays a moment
    // after it slips off onto nothing, so it doesn't flicker as the crosshair crosses the gaps between controls (the housing
    // between them stops the crosshair's ray) or in the frame of a press (PlayerInteractor clears its focus after each one).
    // While it stays, its words stay as they were on the last control; it goes at once when the crosshair lands on something
    // else. THE LINGER DEPENDS ON ShopUI STEPPING ASIDE (its "|| BeverageStation.LineUp", 23 Oct 2026): with no focus,
    // UsingDrinks is false, and with anything in hand ShopUI would build its own "In hand ..." line, so this one would give
    // way, drop out and come back at every gap and every press.
    private const float Linger = .5f;
    private float aimedAt = -10f;
    private Wording said;
    private bool written;

    private void Refresh()
    {
        if (canvas == null) return;
        bool aiming = player != null && player.AimingAtDrinks;
        if (aiming) aimedAt = Time.unscaledTime;
        bool closeUp = player != null && player.CurrentStation == station;
        bool lingering = player != null && !aiming && player.CurrentStation == null && player.Focused == null
                         && Time.unscaledTime - aimedAt < Linger;
        bool show = (closeUp || aiming || lingering) && Time.timeScale > 0
                    && (DayClock.Instance == null || !DayClock.Instance.DayOver)
                    && (dialogue == null || !dialogue.InConversation);
        // The stack's glow follows the step while the machine is in use (from above: in its close-up only), whether or not
        // the line has given way to the café's. `usual` is what E / A acts with: the hand last chosen or picked into.
        JobBase usual = carry != null ? carry.Carried : null;
        DrinkJob carriedCup = usual as DrinkJob;
        bool takeACup = show && carry != null && carry.HasSpace && carriedCup == null
                        && ShopInventory.Instance != null && ShopInventory.Instance.MachineCups > 0;   // 24 Oct 2026 (Days): the machine's
        SetStackGlow(takeACup);
        // One line at a time: lingering, this line gives way the moment the café's prompt line has words of its own.
        bool up = show && (!lingering || shop == null || string.IsNullOrEmpty(shop.PromptLine));
        if (canvas.gameObject.activeSelf != up) canvas.gameObject.SetActive(up);
        if (up) lineOwner = this;
        else if (lineOwner == this) lineOwner = null;
        if (!up) return;
        if (!lingering || !written)
        {
            Wording next = Decide(usual, closeUp);
            if (!written || !next.Same(said))
            {
                said = next;
                written = true;
                Write(said);
            }
            else said.slot = next.slot;
        }
        // The ring fills with its pour, every frame (no number: her question, his "testing" answer).
        if (said.kind == Kind.Pouring && said.slot != null) ring.fillAmount = said.slot.Progress;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (lineOwner == this) lineOwner = null;
        SetStackGlow(false);
    }

    // ------------------------------------------------------------------ what the line says

    // The line's icon: E / A (InteractButton), a hand's own button (0 the left, 1 the right), none, the drink's picture, or the
    // pour's ring (drawn beside the words, not in them).
    private const int InteractButton = -1, NoButton = -2, DrinkPicture = -3, RingInPlace = -4;

    private enum Kind
    {
        None, Pouring, TakeTo, Unwanted, Cold, PutBack, PourAway, ReturnHere, OutOfCups, HandsFull, TakeACup, TakeAnother,
        ToPaddle, TakeDrink, TakeColdDrink, PlaceCup, CupFirst, ReadyUnder, ColdUnder, OutOfBeans, NoCupUnder, Pour, ToNozzle,
        ToStack, NoDrink,
        // 24 Oct 2026 (Days; restock by hand, Designer P's §2.5): a crate from the back shelf in hand.
        Refill, ToStackRefill,
    }

    // What the line says, as values: which words, the drink and the person they name, its icon, the hands, Step back, and the
    // device in hand (its icons, "Aim" or "Look"). Worked out every frame without building a word; the words are built only
    // when it changes (as ShopUI's prompt is: WHY THE HUD REMEMBERS WHAT IT LAST SHOWED). The slot is the one pouring, for
    // the ring's fill; it isn't compared.
    private struct Wording
    {
        public Kind kind;
        public DrinkDefinition drink;
        public CustomerBrain who;
        public int button;
        public bool hands, stepBack, pad;
        public BeverageSlot slot;

        public bool Same(Wording other) => kind == other.kind && drink == other.drink && who == other.who
            && button == other.button && hands == other.hands && stepBack == other.stepBack && pad == other.pad;
    }

    private Wording Decide(JobBase usual, bool closeUp)
    {
        var w = new Wording { button = NoButton, stepBack = closeUp, pad = PadInput.UsingPad };
        DrinkJob carriedCup = usual as DrinkJob;
        Interactable focused = player.Focused;
        BeverageControl control = focused != null ? focused.GetComponentInParent<BeverageControl>() : null;
        BeverageCupSupply supply = focused != null ? focused.GetComponentInParent<BeverageCupSupply>() : null;
        DrinkJob focusedCup = focused != null ? focused.GetComponentInParent<DrinkJob>() : null;
        BeverageSlot slot = control != null ? control.slot : null;
        if (slot == null && focusedCup != null)
            foreach (BeverageSlot each in slots)
                if (each != null && each.Cup == focusedCup) { slot = each; break; }
        w.slot = slot;

        // Pouring: "Pouring Latte", and the ring beside it fills with the pour.
        if (slot != null && slot.IsPouring && slot.drink != null)
        {
            w.kind = Kind.Pouring;
            w.drink = slot.drink;
            w.button = RingInPlace;
            return w;
        }
        // After the pour, not before it: BeverageSlot.Cup asks PlayerCarry.Contains, whose lambda makes a little garbage, and
        // the pour is the longest moment at the machine.
        DrinkJob inSlot = slot != null ? slot.Cup : null;
        bool filledInSlot = inSlot != null && !inSlot.IsEmpty;
        bool atReturn = supply != null && supply.discard;

        // A filled cup in hand: who it is for, whatever the crosshair is on (the reviewers: with a full cup in hand the card
        // still said "Take a cup"). The cup return and a filled cup in a slot keep their own lines. A cold one can't be served
        // (DrinkJob.CanHandBack), so it says so instead of "Nobody's waiting".
        if (carriedCup != null && !carriedCup.IsEmpty && !atReturn && !filledInSlot)
        {
            w.drink = carriedCup.Drink;
            w.button = DrinkPicture;
            if (!carriedCup.CanHandBack) w.kind = Kind.Cold;
            else
            {
                w.who = WhoWants(carriedCup);
                w.kind = w.who != null ? Kind.TakeTo : Kind.Unwanted;
            }
            return w;
        }

        if (atReturn) return AtReturn(w, usual);
        if (supply != null) return AtStack(w);
        if (slot != null)
            return control != null && control.dispenseButton ? AtPaddle(w, slot, inSlot) : AtCupPad(w, slot, inSlot, usual);

        // Nothing of the machine's under the crosshair (in the close-up, its crosshair on the housing): the next step.
        if (carriedCup != null) w.kind = Kind.ToNozzle;            // the cup in hand is empty here (a filled one is above)
        else if (StockCrate.InHand(carry) != null) w.kind = Kind.ToStackRefill;   // 24 Oct 2026 (Days): the crate goes to the stack
        else if (HandsFor(Fit.Free) == 0) w.kind = Kind.HandsFull;
        else if (!CupsInStock) w.kind = Kind.OutOfCups;
        else w.kind = Kind.ToStack;
        return w;
    }

    // The cup return: an empty cup goes back to the stock, a drink is poured away. Which cup is the hand's: E / A uses the
    // hand last chosen or picked into (`usual`, PlayerCarry.Carried); with cups in both hands the two hands are offered.
    private Wording AtReturn(Wording w, JobBase usual)
    {
        int count = HandsFor(Fit.Cup);
        DrinkJob cup = Fits(usual, Fit.Cup) ? (DrinkJob)usual : null;
        int button = InteractButton;
        int only = cup == null && count == 1 ? OnlyHand(Fit.Cup) : -1;
        if (only >= 0)
        {
            // E's hand holds something else (a device): the hand with the cup is the line's button.
            cup = carry.GetHandItem(only) as DrinkJob;
            button = only;
        }
        if (cup == null)
        {
            w.kind = Kind.ReturnHere;
            return w;
        }
        w.kind = cup.IsEmpty ? Kind.PutBack : Kind.PourAway;
        w.drink = cup.Drink;
        w.button = button;
        w.hands = count >= 2;
        return w;
    }

    // The cup stack: a cup into a free hand (either, when both are free). With an empty cup already in hand it says "another"
    // (the stack's glow has gone by then): the crosshair is still on the stack after the first cup, and "Take a cup" again
    // reads as the next step (the reviewers' worry of 19 Oct, "a newcomer will try to take another cup"), while a second cup
    // is still one press away for two orders at once.
    private Wording AtStack(Wording w)
    {
        // 24 Oct 2026 (Days): a crate from the back shelf in either hand: E fills the machine back to 12 of each
        // (BeverageCupSupply.Interact), whatever the other hand holds.
        if (StockCrate.InHand(carry) != null)
        {
            w.kind = Kind.Refill;
            w.button = InteractButton;
            return w;
        }
        int free = HandsFor(Fit.Free);
        if (free == 0) w.kind = Kind.HandsFull;
        else if (!CupsInStock) w.kind = Kind.OutOfCups;
        else
        {
            w.kind = HandsFor(Fit.EmptyCup) > 0 ? Kind.TakeAnother : Kind.TakeACup;
            w.button = InteractButton;
            w.hands = free >= 2;
        }
        return w;
    }

    // A paddle pours its drink into whatever is under its nozzle (the machine dispenses with no cup there: a lock); the hands
    // don't come into it. With its drink poured and waiting under it, the paddle won't pour again: the line says the drink is
    // there to take (the Day 1 card's "Latte is ready"), in the next line's own verb.
    private static Wording AtPaddle(Wording w, BeverageSlot slot, DrinkJob inSlot)
    {
        w.drink = slot.drink;
        if (slot.drink == null) w.kind = Kind.NoDrink;
        else if (inSlot != null && !inSlot.IsEmpty) w.kind = inSlot.CanHandBack ? Kind.ReadyUnder : Kind.ColdUnder;
        else if (!CanBrew(slot.drink)) w.kind = Kind.OutOfBeans;
        else if (inSlot == null) w.kind = Kind.NoCupUnder;
        else
        {
            w.kind = Kind.Pour;
            w.button = InteractButton;
        }
        return w;
    }

    // A cup pad under a nozzle, or the cup standing on it.
    private Wording AtCupPad(Wording w, BeverageSlot slot, DrinkJob inSlot, JobBase usual)
    {
        w.drink = slot.drink;
        if (slot.drink == null)
        {
            w.kind = Kind.NoDrink;
            return w;
        }
        if (inSlot != null && inSlot.IsEmpty)
        {
            // An empty cup waits under the nozzle: the next step is its paddle. (E here would take the cup back; the line says
            // what the player who just put it there wants next, as the old card's "aim at the named paddle to pour" did.)
            w.kind = CanBrew(slot.drink) ? Kind.ToPaddle : Kind.OutOfBeans;
            return w;
        }
        if (inSlot != null)
        {
            // A drink ready under its nozzle: into a free hand (either, when both are free).
            int free = HandsFor(Fit.Free);
            if (free == 0)
            {
                w.kind = Kind.HandsFull;
                return w;
            }
            w.kind = inSlot.CanHandBack ? Kind.TakeDrink : Kind.TakeColdDrink;
            w.button = InteractButton;
            w.hands = free >= 2;
            return w;
        }
        // An empty pad: an empty cup in hand goes under it. Two empty cups are the same cup, so the hands never matter here;
        // when E's hand holds something else, the hand with the empty cup is the line's button.
        if (HandsFor(Fit.EmptyCup) > 0)
        {
            w.kind = Kind.PlaceCup;
            int only = Fits(usual, Fit.EmptyCup) ? -1 : OnlyHand(Fit.EmptyCup);
            w.button = only >= 0 ? only : InteractButton;
            return w;
        }
        w.kind = HandsFor(Fit.Free) == 0 ? Kind.HandsFull : Kind.CupFirst;
        return w;
    }

    // 24 Oct 2026 (Days): the machine's own cups (restock by hand: the rest wait on the back shelf).
    private static bool CupsInStock => ShopInventory.Instance != null && ShopInventory.Instance.MachineCups > 0;
    private static bool CanBrew(DrinkDefinition drink) => ShopInventory.Instance != null && ShopInventory.Instance.CanBrew(drink);

    // What a hand must hold to do it: nothing (to take), a cup that isn't locked in the machine (to return), an empty one (to
    // place under a nozzle).
    private enum Fit { Free, Cup, EmptyCup }

    private static bool Fits(JobBase item, Fit fit) =>
        fit == Fit.Free ? item == null : item is DrinkJob cup && !cup.Locked && (fit == Fit.Cup || cup.IsEmpty);

    // How many of Ace's hands could do it. Counted over what he holds, not hand by hand: PlayerCarry.GetHandItem's lambda
    // makes garbage each call, and this runs every frame at the machine. So a frame at the machine makes almost none: only
    // OnlyHand (below, while just the other hand can act) and BeverageSlot.Cup (a pad or paddle with a cup) still make a little.
    private int HandsFor(Fit fit)
    {
        if (carry == null) return 0;
        int held = carry.Count;
        if (fit == Fit.Free) return Mathf.Max(0, carry.Capacity - held);
        int count = 0;
        for (int i = 0; i < held; i++)
            if (Fits(carry.GetItem(i), fit)) count++;
        return count;
    }

    // The one hand (0 the left, 1 the right) holding what fits, when exactly one does; -1 otherwise. Asked only when E's
    // hand can't do it.
    private int OnlyHand(Fit fit)
    {
        int found = -1;
        for (int side = 0; carry != null && side < carry.Capacity; side++)
        {
            if (!Fits(carry.GetHandItem(side), fit)) continue;
            if (found >= 0) return -1;
            found = side;
        }
        return found;
    }

    // ------------------------------------------------------------------ the words

    // Built only when what the line says changed (Refresh).
    private void Write(Wording w)
    {
        string words = WordsFor(w);
        string icon = IconFor(w);
        string line = icon.Length > 0 ? icon + "  " + words : words;     // the icon, two spaces, the words: ShopUI's prompt line
        action.text = line;
        bool pouring = w.kind == Kind.Pouring;
        if (ring.gameObject.activeSelf != pouring)
        {
            ring.gameObject.SetActive(pouring);
            ringTrack.gameObject.SetActive(pouring);
        }
        // The ring takes the icon's place: the ring, a gap and the words centred together, as an icon and its words are.
        float shift = 0f;
        if (pouring)
        {
            float wordsWidth = action.GetPreferredValues(line, 4000f, 0f).x;
            shift = (RingSize + RingGap) * .5f;
            var at = new Vector2(-(wordsWidth + RingGap) * .5f, ActionMiddle);
            ring.rectTransform.anchoredPosition = at;
            ringTrack.rectTransform.anchoredPosition = at;
        }
        action.rectTransform.anchoredPosition = new Vector2(shift, ActionMiddle);

        if (hands.gameObject.activeSelf != w.hands) hands.gameObject.SetActive(w.hands);
        hands.text = w.hands ? HandsLine() : "";

        if (stepBackPlate.gameObject.activeSelf != w.stepBack) stepBackPlate.gameObject.SetActive(w.stepBack);
        string back = w.stepBack ? ControlHints.Back + "  Step back" : "";
        if (stepBack.text != back)
        {
            stepBack.text = back;
            if (back.Length > 0)
            {
                // The plate as wide as its words, as the café's strip lays its plate (ShopUI.Strip).
                float backWidth = stepBack.GetPreferredValues(back, 4000f, 0f).x;
                stepBackPlate.sizeDelta = new Vector2(Mathf.Ceil(backWidth) + StripPadX * 2f, StripHeight + StripPadY * 2f);
            }
        }
    }

    // 24 Oct 2026 (Days): the back shelf has cups (or beans) a refill would bring.
    private static bool OnTheShelf(bool cups) => ShopInventory.Instance != null
        && (cups ? ShopInventory.Instance.ShelfCups : ShopInventory.Instance.ShelfBeans) > 0;

    // The words, in the café's plain voice: what the button does here, or the next step. Each drink is named as the game writes
    // it everywhere (its paddle, the ticket, the Day 1 card): "Pour Latte", "Take the Coffee to Beatriz". Kept short: Ace's
    // held cups are drawn at 28 % and 72 % of the screen's width, at the line's height (PlayerCarry.HandCentre), about 700 px
    // apart at 1080p: some 33 letters at 34 px fit between them.
    private static string WordsFor(Wording w)
    {
        string drink = w.drink != null ? w.drink.drinkName : "drink";
        string look = w.pad ? "Aim" : "Look";      // the Day 1 card's words: a pad aims, a mouse looks
        switch (w.kind)
        {
            case Kind.Pouring: return "Pouring " + drink;
            case Kind.TakeTo: return $"Take the {drink} to {(w.who != null ? w.who.CustomerName : "them")}";
            // "waiting", not "ordered": WhoWants counts only customers who can take it now (CanReceiveDrink), and one who
            // ordered it but is saying their parting line (Speaking: leaving) can't (the review of 23 Oct 2026). The drink's
            // picture names it; "for it", not "for this one", so the line fits between the held cups (above).
            case Kind.Unwanted: return "Nobody's waiting for it: pour it away";
            case Kind.Cold: return $"This {drink} is cold: pour it away";
            case Kind.PutBack: return "Put the cup back";
            case Kind.PourAway: return $"Pour away the {drink}";
            case Kind.ReturnHere: return "Spare cups go back here";
            // 24 Oct 2026 (Days): with stock on the back shelf, the machine is only empty until a refill.
            case Kind.OutOfCups: return OnTheShelf(true) ? "No cups: refill from the back shelf" : "Out of cups: restock at closing";
            case Kind.HandsFull: return "Both hands are full";           // short: both cups are drawn either side of it
            case Kind.TakeACup: return "Take a cup";
            case Kind.TakeAnother: return "Take another cup";
            case Kind.ToPaddle: return $"{look} at the {drink} paddle to pour";
            case Kind.TakeDrink: return $"Take the {drink}";
            case Kind.TakeColdDrink: return $"Take the cold {drink}";
            case Kind.PlaceCup: return $"Place the cup under {drink}";
            case Kind.CupFirst: return "Take a cup from the stack first";
            case Kind.ReadyUnder: return $"{drink} is ready: take the cup";
            case Kind.ColdUnder: return $"This {drink} is cold: take the cup";
            case Kind.OutOfBeans: return OnTheShelf(false) ? "No beans: refill from the back shelf" : "Out of beans: restock at closing";
            case Kind.Refill: return BeverageCupSupply.RefillWords;                       // 24 Oct 2026 (Days)
            case Kind.ToStackRefill: return $"{look} at the cup stack to refill it";
            // E on this paddle still pours, onto the tray, and takes the beans (BeverageSlot.TryPour debits at the press, cup or
            // none), so the line says so, as the old "ingredients will be wasted" did. No icon: a warning, not an invitation.
            // Worded to fit between the held cups at the longest name ("Hot Chocolate", 703 px).
            case Kind.NoCupUnder: return $"No cup: pouring wastes the {drink}";
            case Kind.Pour: return "Pour " + drink;
            case Kind.ToNozzle: return "Place the cup under a nozzle";
            case Kind.ToStack: return $"{look} at the cup stack to take a cup";
            case Kind.NoDrink: return "No drink here";
            default: return "";
        }
    }

    // The icon in front: E / A, a hand's button, the drink's picture (the ticket's: JobTicket.DrinkLine), or none.
    private static string IconFor(Wording w)
    {
        switch (w.button)
        {
            case InteractButton: return ControlHints.Interact;
            case 0: return ControlHints.LeftHand;
            case 1: return ControlHints.RightHand;
            case DrinkPicture: return PadInput.Icon(JobTicket.DrinkIconName(w.drink), "");
            default: return "";
        }
    }

    // The two hands, an icon and a word each, spaced as the café strip spaces its hints (ShopUI.Hint).
    private static string HandsLine() => ControlHints.LeftHand + "  left      " + ControlHints.RightHand + "  right";

    // ------------------------------------------------------------------ who the drink is for

    // The customer waiting for this drink in hand (CanReceiveDrink reads the player's hands; WantedDrink is this cup's drink,
    // so with a Latte and a Coffee in hand each names its own); the nearest when several are. Asked at most twice a second,
    // an answer of nobody included (until 23 Oct 2026 it searched the scene every frame while nobody wanted the drink).
    private CustomerBrain wanting;
    private bool wantingNobody;
    private float wantingAskedAt = -1f;
    private DrinkJob wantingFor;

    private CustomerBrain WhoWants(DrinkJob cup)
    {
        if (cup != wantingFor || Time.unscaledTime - wantingAskedAt > .5f || !wantingNobody && wanting == null)
        {
            wantingFor = cup;
            wantingAskedAt = Time.unscaledTime;
            wanting = null;
            float nearest = float.PositiveInfinity;
            Vector3 here = player != null ? player.transform.position : transform.position;
            foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            {
                if (brain == null || !brain.CanReceiveDrink || brain.WantedDrink != cup.Drink) continue;
                float d = (brain.transform.position - here).sqrMagnitude;
                if (d < nearest) { nearest = d; wanting = brain; }
            }
            wantingNobody = wanting == null;
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
}
