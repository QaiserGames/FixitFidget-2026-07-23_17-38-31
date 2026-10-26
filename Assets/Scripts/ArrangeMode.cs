using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// ARRANGE THE CAFÉ (24 Oct 2026; furniture-spec.md §1.2, §1.3, §4.10 Part 2, §4.11; his answers of 24 Oct)
//
// His choice of 23 Oct, "Grow the café": at closing, from the phone's Shop (YOUR CAFÉ · Arrange the café), the player buys,
// places, moves, turns and sells furniture. His answers of 24 Oct: only while the café is closed, at every closing, as often
// as he likes (f1); a 25 cm grid with quarter turns (f2); a piece sells back for half, moving is free (f3). Every change goes
// through CafeLayout (the rules, the money, the save); this is the input, the camera and what's on screen.
//
// ENTERING (TryOpen, from the phone's Shop; CanOpen: the day is over and the night hasn't begun):
//   * the phone drops away (RecapUI.HideForArrange: the recap is still "showing", so nothing that hides at closing comes back);
//   * anyone still sitting leaves (DayClock.ClearTheShop: patrons stay seated through closing);
//   * the camera eases over half a second to the overhead's home angle at 26 m (CafeViewMode.OrbitTo each frame; first
//     person is put away and comes back on leaving); orbit and zoom work as by day: CafeViewMode's OverlayOwnsInput gives way
//     while Active, its V doesn't;
//   * the world holds still as at closing (time scale 0): everything here runs on real time;
//   * the strip rises with the first card on sale chosen (the bistro on Day 1), its ghost waiting at the nearest green spot to
//     the counter, on a dust sheet while any lies, so the first table folds a sheet away.
// IN HAND (what the controls strip shows, buttons as icons: ControlHints Place, Turn, SellHold, Browse, Back):
//   * a new piece: ✕ / click places it (paid, put down, saved at once), △ / R turns it a quarter (a plant: the next kind of
//     plant; a wall piece faces the room and doesn't turn), ○ / Esc puts the card down, L1 R1 / Q E browse the strip;
//   * nothing: the cursor points at a placed piece (an amber outline, its plate): ✕ picks it up, hold □ / F (0.6 s, a ring
//     fills) sells it back for half; ○ leaves;
//   * a placed piece picked up: ✕ puts it down anywhere green, free; ○ puts it back exactly where it was; hold □ sells it.
// The cursor is a point on the floor: the mouse's pointer on the floor; the left stick moves it relative to the camera, as
// Ace walks (CafeViewMode.MovementYaw); the D-pad (or the arrows, W A S D) a cell at a time. The ghost's pivot jumps cell to
// cell (FurnitureRules.Snap); a wall piece slides along the nearest side wall, and a bay snaps to join a bay.
// LEAVING (○ with nothing in hand, or Close): what's in hand goes back, the layout is already saved, what remembers the room
// looks again (CafeLayout.AfterArranging), the camera eases back, the phone comes back up on the Shop app.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class ArrangeMode : MonoBehaviour
{
    public static ArrangeMode Instance { get; private set; }
    /// <summary>Arrange is up (CafeViewMode's input exception).</summary>
    public static bool Active => Instance != null && Instance.open;

    /// <summary>The overhead's distance while arranging: a 25 cm cell is about 15 px wide at 1080p (inside the view's 24-48 m).</summary>
    public const float ViewDistance = 26f;
    public const float EaseSeconds = .5f, SellHoldSeconds = .6f, CursorSpeed = 3.6f;

    public enum Hand { Nothing, New, Moving }

    public event Action Opened, Closed;

    // ------------------------------------------------------------------ the contract (furniture-spec.md §4.11)

    /// <summary>The day is over and the night hasn't begun (and Arrange isn't already up).</summary>
    public bool CanOpen
    {
        get
        {
            CafeLayout layout = CafeLayout.Instance;
            return !open && layout != null && layout.Catalogue != null && CafeLayout.Closed && !DayIntro.Playing;
        }
    }

    /// <summary>The kind whose card is chosen with a new piece in hand (labs); null otherwise.</summary>
    public FurnitureDefinition Chosen => hand == Hand.New ? chosen : null;
    /// <summary>The ghost's verdict where it stands now: green or red, and the words (labs).</summary>
    public FurnitureRules.Result GhostResult { get; private set; }

    // ------------------------------------------------------------------ more, for the labs

    public Hand InHand => hand;
    public PlacedPiece Carried => hand == Hand.Moving ? moving : null;
    public PlacedPiece Pointed => hand == Hand.Nothing ? hover : null;
    public Vector3 CursorPoint => cursor;
    public Vector3 GhostPivot => pivot;
    public float GhostYaw => ghostYaw;
    public string ChosenVariant => variant;
    public ArrangeUI Screen => ui;
    public ArrangeGhost World => ghost;
    /// <summary>Pieces bought in this visit.</summary>
    public int Placed { get; private set; }
    /// <summary>The hold on sell: 0 to 1.</summary>
    public float SellProgress => Mathf.Clamp01(sellHeld / SellHoldSeconds);

    bool open;
    int openedFrame = -1;   // 26 Oct 2026 (review fix): the frame Arrange came up (see Update)
    Hand hand;
    FurnitureDefinition chosen;
    string variant = "";
    float yaw, ghostYaw, sellHeld;
    PlacedPiece moving, hover, lastTemplate;
    Vector3 cursor, pivot;
    bool poseKnown, dirty, sellArmed = true;
    int lastMoney = int.MinValue;
    string nowAfter = "";
    ArrangeUI ui;
    ArrangeGhost ghost;
    CafeViewMode view;
    bool wasFirstPerson;
    Vector3 angleBefore, easeFrom, easeTo;
    float easeStart = -1f;
    Vector2 lastMouse;
    bool mouseSeen;
    FurnitureRules.Shape homeShape;
    readonly List<FurnitureRules.Shape> scratch = new List<FurnitureRules.Shape>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ui != null) Destroy(ui.gameObject);
        if (ghost != null) Destroy(ghost.gameObject);
    }

    // ------------------------------------------------------------------ in and out

    /// <summary>From the phone's Shop: Arrange comes up (see the header). False when it can't (CanOpen).</summary>
    public bool TryOpen()
    {
        if (!CanOpen) return false;
        CafeLayout layout = CafeLayout.Instance;
        open = true;   // first: the view takes its change of camera now (Active lifts OverlayOwnsInput)
        openedFrame = Time.frameCount;
        Placed = 0;
        nowAfter = "";
        view = FindAnyObjectByType<CafeViewMode>();
        if (view != null)
        {
            wasFirstPerson = view.FirstPersonSelected;
            if (wasFirstPerson) view.SetFirstPerson(false);
            angleBefore = view.OverheadAngle;
            Vector3 home = view.OverheadHome;
            EaseTo(angleBefore, new Vector3(home.x, home.y, ViewDistance));
        }
        if (DayClock.Instance != null) DayClock.Instance.ClearTheShop();
        RecapUI.HideForArrange();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (ui == null) ui = ArrangeUI.Create();
        if (ghost == null) ghost = ArrangeGhost.Create(layout.Catalogue.ghostMaterial);
        ui.Open();
        ghost.Show(true);
        layout.Changed += OnLayoutChanged;
        _ = layout.Map;   // built now, once for this closing (physics asked once: FurnitureRules.BuildMap)

        hand = Hand.Nothing;
        moving = hover = lastTemplate = null;
        yaw = 0f;
        cursor = new Vector3(0f, 0f, 6f);
        FurnitureDefinition first = FirstForSale(layout);
        if (first != null)
        {
            chosen = first;
            variant = first.IsPlant ? first.FirstVariant : "";
            hand = Hand.New;
            cursor = StartingSpot(layout, first, variant, out yaw);
        }
        poseKnown = false;
        dirty = true;
        sellHeld = 0f;
        sellArmed = true;
        // The mouse takes the cursor only once it moves: until then the ghost waits where the search put it.
        Mouse mouseNow = Mouse.current;
        mouseSeen = mouseNow != null;
        if (mouseNow != null) lastMouse = mouseNow.position.ReadValue();
        Sfx.Play2D("phone.close");
        Refresh(layout);
        Opened?.Invoke();
        return true;
    }

    /// <summary>Back to the phone (see the header). What's in hand goes back where it was.</summary>
    public void Close()
    {
        if (!open) return;
        CafeLayout layout = CafeLayout.Instance;
        if (layout != null)
        {
            layout.Changed -= OnLayoutChanged;
            layout.AfterArranging();
        }
        hand = Hand.Nothing;
        moving = hover = null;
        if (ghost != null) ghost.Show(false);
        if (ui != null) ui.Close();
        if (view != null)
        {
            if (wasFirstPerson)
            {
                easeStart = -1f;
                view.SetFirstPerson(true);   // while still Active: the view takes it
            }
            else EaseTo(view.OverheadAngle, angleBefore);
        }
        open = false;
        RecapUI.ReturnFromArrange();
        Sfx.Play2D("phone.open");
        Closed?.Invoke();
    }

    void OnLayoutChanged() => dirty = true;

    // ------------------------------------------------------------------ every frame (real time: the world holds still)

    void Update()
    {
        StepEase();
        if (!open) return;
        CafeLayout layout = CafeLayout.Instance;
        if (layout == null || !CafeLayout.Closed) { Close(); return; }
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
        ReadCursor(dt);
        // 26 Oct 2026 (review fix): not on the frame it opened: the pad's ✕ that pressed "Arrange the café" (the UI's submit,
        // this same frame) would otherwise also place, buying the first bistro unseen.
        if (Time.frameCount != openedFrame) ReadButtons(dt);
        if (!open) return;   // ○ left
        Refresh(layout);
    }

    void ReadCursor(float dt)
    {
        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (mouse != null && cam != null && !PadInput.UsingPad)
        {
            Vector2 at = mouse.position.ReadValue();
            bool moved = !mouseSeen || (at - lastMouse).sqrMagnitude > .25f;
            lastMouse = at;
            mouseSeen = true;
            if (moved && (ui == null || !ui.OverUI(at)) && FloorPoint(cam, at, out Vector3 p)) cursor = p;
        }
        Basis(out Vector3 forward, out Vector3 right);
        Vector2 stick = PadInput.Curved(PadInput.LeftStick);
        if (stick != Vector2.zero) cursor += (right * stick.x + forward * stick.y) * CursorSpeed * dt;

        // A cell at a time, along the grid's axis nearest the camera's own directions.
        Keyboard keys = Keyboard.current;
        Vector3 step = Vector3.zero;
        if (PadInput.Pressed(PadButton.DpadUp) || keys != null && (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame)) step += Axis(forward);
        if (PadInput.Pressed(PadButton.DpadDown) || keys != null && (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame)) step -= Axis(forward);
        if (PadInput.Pressed(PadButton.DpadRight) || keys != null && (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame)) step += Axis(right);
        if (PadInput.Pressed(PadButton.DpadLeft) || keys != null && (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame)) step -= Axis(right);
        if (step != Vector3.zero) cursor = FurnitureRules.Snap(cursor) + step * FurnitureRules.Grid;
        Clamp();
    }

    void ReadButtons(float dt)
    {
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (click && ui != null)
        {
            Vector2 at = mouse.position.ReadValue();
            if (ui.OverUI(at))
            {
                FurnitureDefinition card = ui.CardAt(at);
                if (card != null) Choose(card);
                click = false;
            }
        }
        if (PadInput.Pressed(PadButton.South) || click) PlaceNow();
        if (PadInput.Pressed(PadButton.North) || keys != null && keys.rKey.wasPressedThisFrame) TurnNow();
        if (PadInput.Pressed(PadButton.LeftShoulder) || keys != null && keys.qKey.wasPressedThisFrame) Browse(-1);
        if (PadInput.Pressed(PadButton.RightShoulder) || keys != null && keys.eKey.wasPressedThisFrame) Browse(1);

        // Sell back: held for 0.6 s (let go early and nothing happens); after a sale, a fresh press for the next.
        bool held = PadInput.Held(PadButton.West) || keys != null && keys.fKey.isPressed;
        if (!held) { sellHeld = 0f; sellArmed = true; }
        else if (sellArmed && SellTarget != null)
        {
            sellHeld += dt;
            if (sellHeld >= SellHoldSeconds)
            {
                sellHeld = 0f;
                sellArmed = false;
                SellNow();
            }
        }

        bool back = PadInput.Pressed(PadButton.East) || keys != null && keys.escapeKey.wasPressedThisFrame || mouse != null && mouse.rightButton.wasPressedThisFrame;
        if (back) BackNow();
    }

    // ------------------------------------------------------------------ the verbs (the buttons call these; so may the labs)

    /// <summary>✕: place the new piece (bought), put the carried one down, or pick up the one pointed at. False if nothing happened.</summary>
    public bool PlaceNow()
    {
        if (!open) return false;
        CafeLayout layout = CafeLayout.Instance;
        UpdatePose(layout, true);
        switch (hand)
        {
            case Hand.New:
            {
                if (!GhostResult.Fits) { Refused(); return false; }
                PlacedPiece made = layout.Buy(chosen, pivot, ghostYaw, variant);
                if (made == null) { GhostResult = layout.LastResult; Refused(); return false; }
                Placed++;
                Sfx.Play2D("money.paid");
                Landed(made);
                nowAfter = made.SeatCount > 0 ? "Customers can sit there tomorrow." : "A plant is for looks. Seats bring the guests.";
                hand = Hand.Nothing;
                hover = made;
                return true;
            }
            case Hand.Moving:
            {
                PlacedPiece piece = moving;
                if (!GhostResult.Fits || !layout.Move(piece, pivot, ghostYaw))
                {
                    if (!layout.LastResult.Fits) GhostResult = layout.LastResult;
                    Refused();
                    return false;
                }
                Landed(piece);
                hand = Hand.Nothing;
                moving = null;
                hover = piece;
                return true;
            }
            default:
            {
                if (hover == null || !layout.PickUp(hover)) return false;
                moving = hover;
                hover = null;
                hand = Hand.Moving;
                yaw = moving.Yaw;
                cursor = moving.Pivot;
                homeShape = moving.Shape;
                poseKnown = false;
                Sfx.Play2D("item.pickup");
                return true;
            }
        }
    }

    /// <summary>△: a quarter turn of what's in hand (a new plant: the next kind of plant). False for a wall piece (it faces the room).</summary>
    public bool TurnNow()
    {
        if (!open) return false;
        FurnitureDefinition kind = KindInHand;
        if (kind == null) return false;
        if (kind.againstAWall) { Refused(); return false; }
        if (kind.IsPlant)
        {
            if (hand != Hand.New) return false;   // a placed plant keeps its kind; round, it has no turn
            variant = kind.NextVariant(variant);
        }
        else yaw = FurnitureRules.Quarter(yaw + 90f);
        poseKnown = false;
        Sfx.Play2D("phone.tap");
        return true;
    }

    /// <summary>Hold □ done: the piece pointed at, or the one in hand, sold back for half its price (f3).</summary>
    public bool SellNow()
    {
        if (!open) return false;
        CafeLayout layout = CafeLayout.Instance;
        PlacedPiece piece = SellTarget;
        if (piece == null) return false;
        Vector3 at = hand == Hand.Moving ? pivot : piece.Pivot;
        bool carried = hand == Hand.Moving;
        if (!layout.Sell(piece)) return false;
        Puff(at);
        Sfx.Play2D("money.tip");
        if (carried) { moving = null; hand = Hand.Nothing; }
        hover = null;
        dirty = true;
        return true;
    }

    /// <summary>○: put the carried piece back, put the card down, or (nothing in hand) leave.</summary>
    public void BackNow()
    {
        if (!open) return;
        CafeLayout layout = CafeLayout.Instance;
        switch (hand)
        {
            case Hand.Moving:
                layout.PutBack();
                hover = moving;
                moving = null;
                hand = Hand.Nothing;
                Sfx.Play2D("item.putdown");
                break;
            case Hand.New:
                hand = Hand.Nothing;
                Sfx.Play2D("phone.tap");
                break;
            default:
                Close();
                return;
        }
        dirty = true;
    }

    /// <summary>L1 / R1: the next card on sale that way along the strip (a piece carried goes back first).</summary>
    public void Browse(int by)
    {
        if (!open || by == 0) return;
        CafeLayout layout = CafeLayout.Instance;
        FurnitureDefinition[] kinds = layout.Catalogue.kinds;
        if (kinds == null || kinds.Length == 0) return;
        int at = chosen != null ? Array.IndexOf(kinds, chosen) : by > 0 ? -1 : 0;
        if (hand != Hand.New && chosen != null && at >= 0) at -= by;   // nothing in hand: the chosen card itself comes back first
        for (int i = 1; i <= kinds.Length; i++)
        {
            int j = ((at + by * i) % kinds.Length + kinds.Length) % kinds.Length;
            if (kinds[j] != null && layout.ForSale(kinds[j])) { Choose(kinds[j]); return; }
        }
    }

    /// <summary>A card chosen (L1 R1, a click on it; the labs): its ghost where the cursor is.</summary>
    public void Choose(FurnitureDefinition kind, string plant = "")
    {
        if (!open) return;
        CafeLayout layout = CafeLayout.Instance;
        if (kind == null || !layout.ForSale(kind)) { Refused(); return; }
        if (hand == Hand.Moving) { layout.PutBack(); moving = null; }
        chosen = kind;
        variant = kind.IsPlant ? (string.IsNullOrEmpty(plant) ? kind.FirstVariant : plant) : "";
        hand = Hand.New;
        hover = null;
        if (kind.againstAWall) yaw = 0f;
        poseKnown = false;
        Sfx.Play2D("phone.tap");
    }

    /// <summary>The cursor to a floor point (the labs; the mouse and the stick move it by themselves).</summary>
    public void CursorTo(Vector3 floorPoint)
    {
        cursor = new Vector3(floorPoint.x, 0f, floorPoint.z);
        Clamp();
        poseKnown = false;
    }

    /// <summary>The ghost's verdict asked again now (the labs, after CursorTo or Choose).</summary>
    public FurnitureRules.Result Recheck()
    {
        if (open) UpdatePose(CafeLayout.Instance, true);
        return GhostResult;
    }

    // ------------------------------------------------------------------ the ghost, the plate, the strip

    FurnitureDefinition KindInHand => hand == Hand.New ? chosen : hand == Hand.Moving && moving != null ? moving.Kind : null;
    PlacedPiece SellTarget => hand == Hand.Moving ? moving : hand == Hand.Nothing ? hover : null;

    void UpdatePose(CafeLayout layout, bool force)
    {
        FurnitureDefinition kind = KindInHand;
        if (kind == null || layout == null) return;
        PlacedPiece template = hand == Hand.Moving ? moving : FurnitureRules.TemplateOf(kind, variant);
        if (template == null)
        {
            GhostResult = new FurnitureRules.Result { verdict = FurnitureRules.Verdict.OffTheFloor, words = "Only on the café floor" };
            return;
        }
        Vector3 p;
        float y;
        if (kind.againstAWall)
        {
            FurnitureRules.WallPose(cursor.x, cursor.z, out p, out y);
            p.z = BanquetteRun.SnapToRun(layout.Pieces, template, p, y, moving);
        }
        else
        {
            p = FurnitureRules.Snap(cursor);
            y = yaw;
        }
        int money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0;
        bool same = poseKnown && !dirty && template == lastTemplate && money == lastMoney
                    && (p - pivot).sqrMagnitude < 1e-6f && Mathf.Abs(Mathf.DeltaAngle(y, ghostYaw)) < .1f;
        if (same && !force) return;
        pivot = p;
        ghostYaw = y;
        poseKnown = true;
        dirty = false;
        lastTemplate = template;
        lastMoney = money;
        GhostResult = hand == Hand.Moving
            ? FurnitureRules.Check(layout.Map, layout.Pieces, kind, template, p, y, moving, int.MaxValue)
            : FurnitureRules.Check(layout.Map, layout.Pieces, kind, template, p, y, null, money);
    }

    void Refresh(CafeLayout layout)
    {
        if (dirty && hand == Hand.Nothing) dirty = false;
        UpdatePose(layout, false);
        if (hand == Hand.Nothing) hover = PieceAt(layout, cursor);
        FurnitureDefinition kind = KindInHand;
        int money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0;
        Camera cam = Camera.main;

        // The room.
        if (kind != null && (hand == Hand.New || hand == Hand.Moving))
        {
            PlacedPiece template = hand == Hand.Moving ? moving : FurnitureRules.TemplateOf(kind, variant);
            GameObject source = hand == Hand.Moving ? (moving != null ? moving.gameObject : null) : kind.PrefabFor(variant);
            if (template != null && source != null)
            {
                FurnitureRules.Shape shape = template.ShapeAt(pivot, ghostYaw);
                ghost.Ghost(source, hand == Hand.Moving ? moving.Id : kind.name + "/" + variant, pivot, ghostYaw, shape, GhostResult.Fits, GhostResult.inTheWay);
            }
            else ghost.NoGhost();
        }
        else ghost.NoGhost();
        bool pointing = hand == Hand.Nothing && hover != null;
        ghost.Hover(pointing, pointing ? hover.Shape : default);
        ghost.Home(hand == Hand.Moving, homeShape);

        // The screen.
        ui.Cards(layout.Catalogue.kinds, Chosen, money, layout);
        ui.Till(money, layout.Seats);
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 1;
        ui.Now($"DAY {day} · YOUR CAFÉ", NowLine(layout, day));
        ui.Controls(ControlsLine(kind));
        if (cam != null && (kind != null && hand != Hand.Nothing || pointing))
        {
            Vector3 screen = cam.WorldToScreenPoint(ghost.Top);
            if (screen.z > 0f) Plate(new Vector2(screen.x, screen.y), kind);
            else ui.HidePlate();
        }
        else ui.HidePlate();
    }

    string NowLine(CafeLayout layout, int day)
    {
        if (nowAfter.Length > 0) return nowAfter;
        bool boughtAny = false;
        foreach (PlacedPiece p in layout.Pieces) if (p != null && !p.FromScene) { boughtAny = true; break; }
        if (day <= 1 && !boughtAny) return "Place your first table";
        return "Moving is free. Selling gives half back.";
    }

    string ControlsLine(FurnitureDefinition kind)
    {
        var b = new StringBuilder();
        void Item(string icon, string word, bool on = true)
        {
            if (b.Length > 0) b.Append("    ");
            if (!on) b.Append("<alpha=#66>");
            b.Append(icon).Append(' ').Append(word);
            if (!on) b.Append("<alpha=#FF>");
        }
        switch (hand)
        {
            case Hand.New:
                Item(ControlHints.Place, "Place", GhostResult.Fits);
                if (kind != null && kind.IsPlant) Item(ControlHints.Turn, "Next plant");
                else Item(ControlHints.Turn, "Turn", kind != null && !kind.againstAWall);
                Item(ControlHints.Back, "Back");
                Item(ControlHints.Browse, "Browse");
                break;
            case Hand.Moving:
                Item(ControlHints.Place, "Put down", GhostResult.Fits);
                Item(ControlHints.Turn, "Turn", kind != null && !kind.againstAWall && !kind.IsPlant);
                Item(ControlHints.SellHold, $"Hold to sell +${(CafeLayout.Instance != null ? CafeLayout.Instance.SellBackOf(moving) : 0)}");   // 26 Oct 2026 (Days and first table): $0 for the free table
                Item(ControlHints.Back, "Put back");
                break;
            default:
                if (hover != null)
                {
                    Item(ControlHints.Place, "Pick up");
                    Item(ControlHints.SellHold, $"Hold to sell +${(CafeLayout.Instance != null ? CafeLayout.Instance.SellBackOf(hover) : 0)}");
                }
                Item(ControlHints.Back, "Done");
                Item(ControlHints.Browse, "Browse");
                break;
        }
        return b.ToString();
    }

    void Plate(Vector2 screen, FurnitureDefinition kind)
    {
        string reason = "", line, sell = "";
        if (hand == Hand.New && kind != null)
        {
            if (!GhostResult.Fits) reason = GhostResult.words;
            string what = kind.IsPlant && variant.Length > 0 ? variant : kind.displayName;
            line = $"{what} · {SeatsWords(kind.seats)} · {FurnitureRules.PriceWords(kind)}";   // 26 Oct 2026 (Days and first table): "<s>$80</s> Free today" on Day 1's first table
        }
        else if (hand == Hand.Moving && moving != null)
        {
            if (!GhostResult.Fits) reason = GhostResult.words;
            line = $"{Name(moving)} · {SeatsWords(moving.SeatCount)} · moving is free";
            sell = $"{ControlHints.SellHold} Hold to sell it back: +${(CafeLayout.Instance != null ? CafeLayout.Instance.SellBackOf(moving) : 0)}";
        }
        else if (hover != null)
        {
            line = $"{Name(hover)} · {SeatsWords(hover.SeatCount)}";
            sell = $"{ControlHints.SellHold} Hold to sell it back: +${(CafeLayout.Instance != null ? CafeLayout.Instance.SellBackOf(hover) : 0)}";
        }
        else { ui.HidePlate(); return; }
        ui.Plate(screen, reason, line, sell, SellProgress);
    }

    static string Name(PlacedPiece p) => p.Kind != null && p.Kind.IsPlant && p.Variant.Length > 0 ? p.Variant : p.DisplayName;
    static string SeatsWords(int n) => n <= 0 ? "looks only" : n == 1 ? "1 seat" : n + " seats";

    // The piece whose footprint the cursor is on (the smallest, if two: a plant beside a table), from 12 cm off its edge.
    static PlacedPiece PieceAt(CafeLayout layout, Vector3 at)
    {
        PlacedPiece best = null;
        float bestArea = float.MaxValue;
        foreach (PlacedPiece p in layout.Pieces)
        {
            if (p == null || !p.On) continue;
            Bounds f = p.FootprintAt(p.Pivot, p.Yaw);
            if (at.x < f.min.x - .12f || at.x > f.max.x + .12f || at.z < f.min.z - .12f || at.z > f.max.z + .12f) continue;
            float area = f.size.x * f.size.z;
            if (area < bestArea) { bestArea = area; best = p; }
        }
        return best;
    }

    // ------------------------------------------------------------------ where the first ghost waits

    static FurnitureDefinition FirstForSale(CafeLayout layout)
    {
        if (layout.Catalogue == null || layout.Catalogue.kinds == null) return null;
        foreach (FurnitureDefinition k in layout.Catalogue.kinds) if (k != null && layout.ForSale(k)) return k;
        return null;
    }

    // The nearest green spot to the counter (the model's order: the highest z first, a little for being off the middle), on a
    // dust sheet while any lies (so the first table folds one away: the spec's §1.2). Every cell is tried with the quick rules
    // first; only the few that pass are asked the whole check (both guards).
    Vector3 StartingSpot(CafeLayout layout, FurnitureDefinition kind, string plant, out float startYaw)
    {
        startYaw = 0f;
        PlacedPiece template = FurnitureRules.TemplateOf(kind, plant);
        if (template == null) return new Vector3(0f, 0f, 6f);
        FurnitureRules.FloorMap map = layout.Map;
        scratch.Clear();
        scratch.AddRange(FurnitureRules.ShapesOf(layout.Pieces, null));
        var sheets = new List<DustSheet>();
        foreach (DustSheet s in layout.Sheets) if (s != null && s.Lying) sheets.Add(s);
        // The score in thousandths (it's whole there on the 25 cm grid), then x, z and the turn: one order every time, the one
        // the rules' Python twin used for the labs' expectations (Day 1: (-4.75, 10.25) turned, on sheet 2).
        var tries = new List<(Vector3 p, float y, int score)>();
        if (kind.againstAWall)
            for (float z = FurnitureRules.Grid; z < FurnitureRules.CounterFront; z += FurnitureRules.Grid)
                foreach (float x in new[] { FurnitureRules.WestWall, FurnitureRules.EastWall })
                {
                    FurnitureRules.WallPose(x, z, out Vector3 p, out float y);
                    tries.Add((p, y, Score(template, p, y, sheets)));
                }
        else
            for (float x = -7.25f; x <= 7.25f; x += FurnitureRules.Grid)
                for (float z = FurnitureRules.Grid; z < FurnitureRules.CounterFront; z += FurnitureRules.Grid)
                    foreach (float y in new[] { 0f, 90f })
                    {
                        Vector3 p = FurnitureRules.Snap(new Vector3(x, 0f, z));
                        tries.Add((p, y, Score(template, p, y, sheets)));
                    }
        tries.Sort((a, b) => a.score != b.score ? a.score.CompareTo(b.score)
                           : a.p.x != b.p.x ? a.p.x.CompareTo(b.p.x)
                           : a.p.z != b.p.z ? a.p.z.CompareTo(b.p.z) : a.y.CompareTo(b.y));
        int full = 0;
        foreach (var t in tries)
        {
            FurnitureRules.Shape shape = template.ShapeAt(t.p, t.y);
            shape.againstAWall = kind.againstAWall;
            if (!FurnitureRules.CheckLocal(map, scratch, shape).Fits) continue;
            if (++full > 60) break;
            if (!FurnitureRules.Check(map, scratch, shape).Fits) continue;
            startYaw = t.y;
            return t.p;
        }
        return new Vector3(0f, 0f, 6f);
    }

    // Lower is better: nearer the counter, off the middle a little, and on a sheet first.
    static int Score(PlacedPiece template, Vector3 p, float y, List<DustSheet> sheets)
    {
        float s = -p.z + .3f * Mathf.Abs(p.x);
        if (sheets.Count > 0)
        {
            Bounds footprint = template.FootprintAt(p, y);
            foreach (DustSheet sheet in sheets) if (sheet.Covers(footprint)) { s -= 100f; break; }
        }
        return Mathf.RoundToInt(s * 1000f);
    }

    // ------------------------------------------------------------------ the camera

    void EaseTo(Vector3 from, Vector3 to)
    {
        easeFrom = from;
        easeTo = to;
        easeStart = Time.unscaledTime;
    }

    void StepEase()
    {
        if (easeStart < 0f || view == null) return;
        float t = Mathf.Clamp01((Time.unscaledTime - easeStart) / EaseSeconds);
        float k = t * t * (3f - 2f * t);
        view.OrbitTo(Mathf.LerpAngle(easeFrom.x, easeTo.x, k), Mathf.Lerp(easeFrom.y, easeTo.y, k), Mathf.Lerp(easeFrom.z, easeTo.z, k));
        if (t >= 1f) easeStart = -1f;
    }

    // The camera's forward and right on the floor (as Ace walks: CafeViewMode.MovementYaw).
    void Basis(out Vector3 forward, out Vector3 right)
    {
        float yawDegrees = view != null ? view.MovementYaw : 0f;
        float r = yawDegrees * Mathf.Deg2Rad;
        forward = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        right = new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
    }

    static Vector3 Axis(Vector3 v) => Mathf.Abs(v.x) > Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));

    static bool FloorPoint(Camera cam, Vector2 screen, out Vector3 point)
    {
        point = default;
        Ray ray = cam.ScreenPointToRay(screen);
        var floor = new Plane(Vector3.up, Vector3.zero);
        if (!floor.Raycast(ray, out float d)) return false;
        point = ray.GetPoint(d);
        return true;
    }

    void Clamp()
    {
        cursor.x = Mathf.Clamp(cursor.x, FurnitureRules.RoomX0, FurnitureRules.RoomX1);
        cursor.z = Mathf.Clamp(cursor.z, FurnitureRules.RoomZ0, FurnitureRules.CounterFront);
        cursor.y = 0f;
    }

    // ------------------------------------------------------------------ feedback

    void Landed(PlacedPiece piece)
    {
        Sfx.Play2D("item.putdown");
        Puff(piece.Pivot);
        dirty = true;
    }

    void Puff(Vector3 at)
    {
        Camera cam = Camera.main;
        if (cam == null || ui == null) return;
        Vector3 screen = cam.WorldToScreenPoint(at + Vector3.up * .3f);
        if (screen.z > 0f) ui.Puff(new Vector2(screen.x, screen.y));
    }

    void Refused()
    {
        if (ui != null) ui.Refuse();
        Sfx.Play2D("circuit.fizzle");
    }
}
