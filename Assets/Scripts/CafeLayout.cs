using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE CAFÉ'S FURNITURE: WHAT'S WHERE, AND EVERY BUY, MOVE AND SALE (24 Oct 2026; furniture-spec.md §4.1, §4.5, §4.11; his
// answers of 24 Oct)
//
// His choice of 23 Oct: "Grow the café" (the cart only in the intro), "Seats count, décor is looks". One of these in the café
// scene, on GameManager beside SaveManager (Fixit Fidget > Cafe redesign > Furniture 1 adds it). It knows every piece (the
// café's own, on or off, and the bought ones), applies the save at start (GameBootstrap), takes the save's snapshot
// (SaveManager.CaptureState), and does every buy, move and sale for Arrange the café (ArrangeMode only asks it).
//
// THE SAVE (SaveData.cafe, additive, no version bump):
//   * no save file at all: a NEW GAME, the half-finished café (his answer of 24 Oct, r1): 10 seats open, the repair bar's 3
//     stools, the lounge's 2 tub chairs, the window counter's 3 stools and the north bistro 2-top (the Opening list), the door's
//     olive trees, the fig, the two monsteras and the front-right rubber plant; every other piece of the café's own off;
//     every dust sheet lying;
//   * a save with no layout in it (format 0: every save from before 24 Oct, and the lab saves today's labs write): TODAY'S CAFÉ,
//     every piece of its own on where the scene has it, no sheets, nothing bought. The café his saves were played in, exactly;
//   * format 1: what's listed is on at its saved pose; the café's own pieces that aren't listed are off (sold); bought pieces
//     are made from their kind's prefab; the sheets listed lie, the rest are gone. An unknown kind is skipped with a warning.
//     26 Oct 2026 (Days and first table): a bought piece listed "free" was Day 1's free first table (FurnitureRules.PriceToday):
//     it sells back for $0, never half of a price nobody paid. Older saves have no such field: everything sells for half.
// Only HasSave tells a new game from an old save: a new game's SaveData is format 0 too.
//
// WHEN (his answers of 24 Oct): arranging only while the café is closed, at every closing, as often as you like (f1); a 25 cm
// grid with quarter turns (f2, FurnitureRules.Grid); a piece sells back for half its price, moving is free (f3); Tier 2 later,
// every piece's tier saved from day one (f4); customers don't react to décor (f5). Every buy, move and sale saves at once,
// like the Shop's purchases (UpgradeShopUI.PersistPurchase: "Only save after a successful transaction... Never charge on
// load"): the lock "the game saves at Open Tomorrow" already has this exception for what's bought at closing.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class CafeLayout : MonoBehaviour
{
    public static CafeLayout Instance { get; private set; }

    [Tooltip("The kinds for sale (Assets/Data/Furniture/Furniture catalogue.asset; Furniture 1 makes it, Furniture 2 fills it).")]
    [SerializeField] private FurnitureCatalogue catalogue;

    [Tooltip("The café's own pieces (Furniture 1): on in an old save, sold ones off.")]
    [SerializeField] private PlacedPiece[] ownPieces = new PlacedPiece[0];

    [Tooltip("The café's own pieces that are on in a new game: the half-finished café (his answer of 24 Oct, r1: 10 seats open).")]
    [SerializeField] private PlacedPiece[] opening = new PlacedPiece[0];

    [Tooltip("The dust sheets (Furniture 3): lying in a new game, gone in an old save.")]
    [SerializeField] private DustSheet[] sheets = new DustSheet[0];

    [Tooltip("Where bought pieces go in the hierarchy (Furniture 1 makes 31 - furniture/Bought pieces).")]
    [SerializeField] private Transform boughtParent;

    /// <summary>How the layout came in: a new game, an old save (today's café), or a saved layout.</summary>
    public enum Source { NotApplied, NewGame, OldSave, SavedLayout }

    readonly List<PlacedPiece> pieces = new List<PlacedPiece>();
    int nextId = 1;
    int fixedSeats;
    Vector3 homePivot;
    float homeYaw;
    FurnitureRules.FloorMap map;
    // 26 Oct 2026 (Days and first table): the bought pieces that cost nothing (Day 1's first table), by save id.
    readonly HashSet<string> freeIds = new HashSet<string>();

    // ------------------------------------------------------------------ the contract (furniture-spec.md §4.11)

    public FurnitureCatalogue Catalogue => catalogue;
    /// <summary>Every piece, on and off: the café's own and the bought ones.</summary>
    public IReadOnlyList<PlacedPiece> Pieces => pieces;
    /// <summary>Seats on the pieces that are on, plus the seats that aren't furniture (the repair bar's 3).</summary>
    public int Seats
    {
        get
        {
            int n = fixedSeats;
            foreach (PlacedPiece p in pieces) if (p != null && (p.On || p == InHand)) n += p.SeatCount;
            return n;
        }
    }

    /// <summary>After every buy, move or sale.</summary>
    public event Action Changed;

    // ------------------------------------------------------------------ more, for Arrange and the labs

    /// <summary>How the layout came in (labs and the report).</summary>
    public Source AppliedFrom { get; private set; } = Source.NotApplied;
    /// <summary>The piece picked up in Arrange, off until it's put down (saved where it was until then).</summary>
    public PlacedPiece InHand { get; private set; }
    /// <summary>Where Ace starts the day (the scene's Player, read at load): the occupied-circulation guard's seed.</summary>
    public Vector3 AceStart { get; private set; } = new Vector3(0f, 1f, 15.15f);
    /// <summary>The last buy's or move's verdict (labs; the ghost shows its own).</summary>
    public FurnitureRules.Result LastResult { get; private set; }
    /// <summary>The café's own pieces that are on in a new game.</summary>
    public IReadOnlyList<PlacedPiece> Opening => opening ?? new PlacedPiece[0];
    /// <summary>The dust sheets, lying or gone.</summary>
    public IReadOnlyList<DustSheet> Sheets => sheets ?? new DustSheet[0];
    /// <summary>The next bought piece's number.</summary>
    public int NextId => nextId;
    /// <summary>Seats that aren't furniture (the repair bar's three).</summary>
    public int FixedSeats => fixedSeats;

    /// <summary>The floor map for this closing (built once, on the first question; AfterArranging drops it).</summary>
    public FurnitureRules.FloorMap Map => map ??= FurnitureRules.BuildMap();

    /// <summary>
    /// 26 Oct 2026 (Days and first table): a table has been bought this game (a bought piece with seats, in the café or in hand),
    /// so Day 1's free table is taken (FurnitureRules.PriceToday). One sold again is gone: its $0 back makes a rebuy a move.
    /// </summary>
    public bool TableBought
    {
        get
        {
            foreach (PlacedPiece p in pieces)
                if (p != null && !p.FromScene && p.Kind != null && p.Kind.seats > 0) return true;
            return false;
        }
    }

    /// <summary>26 Oct 2026 (Days and first table): this piece cost nothing (Day 1's first table): it sells back for $0.</summary>
    public bool WasFree(PlacedPiece piece) => piece != null && !piece.FromScene && freeIds.Contains(piece.Id);

    /// <summary>What selling this piece back gives (f3: half its price; $0 for Day 1's free table). 26 Oct 2026.</summary>
    public int SellBackOf(PlacedPiece piece)
    {
        if (piece == null || piece.Kind == null) return 0;
        return WasFree(piece) ? 0 : piece.Kind.SellBack;
    }

    /// <summary>The café is closed (the day is over) and no night walk is on: the only time furniture moves (f1).</summary>
    public static bool Closed
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (clock == null || !clock.DayOver) return false;
            return NightWalk.Instance == null || !NightWalk.Instance.Active;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        Instance = this;
        // Arrange the café lives beside the layout it arranges (made while playing, never saved).
        if (GetComponent<ArrangeMode>() == null) gameObject.AddComponent<ArrangeMode>();
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace != null) AceStart = ace.transform.position;
        pieces.Clear();
        foreach (PlacedPiece p in ownPieces) if (p != null && !pieces.Contains(p)) pieces.Add(p);
        CountFixedSeats();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Set by Furniture 1 and 3 (the scene's own pieces, the new game's, the sheets, where bought pieces go).</summary>
    public void Wire(FurnitureCatalogue kinds, PlacedPiece[] own, PlacedPiece[] newGame, DustSheet[] dustSheets, Transform bought)
    {
        catalogue = kinds;
        ownPieces = own ?? new PlacedPiece[0];
        opening = newGame ?? new PlacedPiece[0];
        sheets = dustSheets ?? new DustSheet[0];
        boughtParent = bought;
    }

    /// <summary>Set by Furniture 3 (and its Undo): the dust sheets only.</summary>
    public void WireSheets(DustSheet[] dustSheets) => sheets = dustSheets ?? new DustSheet[0];

    /// <summary>The café's own pieces as the scene holds them (on or off; the steps and the labs, also in Edit Mode).</summary>
    public IReadOnlyList<PlacedPiece> OwnPieces => ownPieces ?? new PlacedPiece[0];

    // ------------------------------------------------------------------ the save

    /// <summary>The save into the café (GameBootstrap.Start, before the clock is restored).</summary>
    public void Apply(SaveData data, bool hasSave)
    {
        // Anything bought in an earlier Apply (a lab reloading the layout) goes; the café's own are decided below.
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            PlacedPiece p = pieces[i];
            if (p == null) { pieces.RemoveAt(i); continue; }
            if (p.FromScene) continue;
            pieces.RemoveAt(i);
            p.SetOn(false);
            Destroy(p.gameObject);
        }
        foreach (PlacedPiece p in ownPieces) if (p != null && !pieces.Contains(p)) pieces.Add(p);
        InHand = null;
        map = null;
        nextId = 1;
        freeIds.Clear();   // 26 Oct 2026 (Days and first table)

        CafeLayoutData cafe = data != null ? data.cafe : null;
        if (!hasSave)
        {
            AppliedFrom = Source.NewGame;
            var open = new HashSet<PlacedPiece>(opening ?? new PlacedPiece[0]);
            foreach (PlacedPiece p in ownPieces) if (p != null) p.SetOn(open.Contains(p));
            foreach (DustSheet s in Sheets) if (s != null) s.SetLying(true);
        }
        else if (cafe == null || cafe.format <= 0)
        {
            AppliedFrom = Source.OldSave;
            foreach (PlacedPiece p in ownPieces) if (p != null) p.SetOn(true);
            foreach (DustSheet s in Sheets) if (s != null) s.SetLying(false);
        }
        else
        {
            AppliedFrom = Source.SavedLayout;
            var listed = new Dictionary<string, PlacedPieceData>();
            foreach (PlacedPieceData d in cafe.pieces ?? new PlacedPieceData[0])
                if (d != null && !string.IsNullOrEmpty(d.id) && !listed.ContainsKey(d.id)) listed[d.id] = d;   // no piece twice
            var own = new HashSet<string>();
            foreach (PlacedPiece p in ownPieces)
            {
                if (p == null) continue;
                own.Add(p.Id);
                if (listed.TryGetValue(p.Id, out PlacedPieceData d))
                {
                    p.Init(p.Kind, p.Id, true, d.tier, string.IsNullOrEmpty(d.variant) ? p.Variant : d.variant);
                    p.SetPose(new Vector3(d.x, 0f, d.z), d.yaw);
                    p.SetOn(true);
                }
                else p.SetOn(false);
            }
            int highest = 0;
            foreach (KeyValuePair<string, PlacedPieceData> entry in listed)
            {
                PlacedPieceData d = entry.Value;
                if (own.Contains(d.id)) continue;
                if (d.id.StartsWith("scene:", StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[Café layout] The save lists the café's own piece '{d.id}', which isn't in this scene: skipped.", this);
                    continue;
                }
                FurnitureDefinition kind = catalogue != null ? catalogue.Find(d.kind) : null;
                GameObject prefab = kind != null ? kind.PrefabFor(d.variant) : null;
                if (prefab == null)
                {
                    Debug.LogWarning($"[Café layout] The save names an unknown furniture kind '{d.kind}' ({d.id}) — was an asset renamed? That piece was skipped.", this);
                    continue;
                }
                PlacedPiece made = Make(kind, prefab, d.id, d.tier, d.variant, new Vector3(d.x, 0f, d.z), d.yaw);
                if (made != null) pieces.Add(made);
                if (made != null && d.free) freeIds.Add(d.id);   // 26 Oct 2026 (Days and first table): Day 1's free table
                if (d.id.Length > 1 && d.id[0] == 'p' && int.TryParse(d.id.Substring(1), out int n)) highest = Mathf.Max(highest, n);
            }
            var left = new HashSet<string>(cafe.sheetsLeft ?? new string[0]);
            foreach (DustSheet s in Sheets) if (s != null) s.SetLying(left.Contains(s.Id));
            nextId = Mathf.Max(Mathf.Max(1, cafe.nextId), highest + 1);
        }
        BanquetteRun.Refresh(pieces);
        Physics.SyncTransforms();
        CountFixedSeats();
    }

    /// <summary>The layout for the save (SaveManager.CaptureState): every piece that is on (a piece in hand where it was picked up).</summary>
    public CafeLayoutData Snapshot()
    {
        var list = new List<PlacedPieceData>();
        foreach (PlacedPiece p in pieces)
        {
            if (p == null) continue;
            bool inHand = p == InHand;
            if (!p.On && !inHand) continue;
            Vector3 at = inHand ? homePivot : p.Pivot;
            float yaw = inHand ? homeYaw : p.Yaw;
            list.Add(new PlacedPieceData
            {
                id = p.Id,
                kind = p.Kind != null ? p.Kind.name : "",
                x = Round(at.x, 1000f),
                z = Round(at.z, 1000f),
                yaw = Round(Mathf.Repeat(yaw, 360f), 100f),
                tier = p.Tier,
                variant = p.Variant,
                free = WasFree(p),   // 26 Oct 2026 (Days and first table)
            });
        }
        var left = new List<string>();
        foreach (DustSheet s in Sheets) if (s != null && s.Lying) left.Add(s.Id);
        return new CafeLayoutData { format = CafeLayoutData.CurrentFormat, pieces = list.ToArray(), sheetsLeft = left.ToArray(), nextId = nextId };
    }

    // ------------------------------------------------------------------ buying, moving, selling (closed only: f1)

    /// <summary>On sale at the day's closing: from its kind's first day on (Tier 2 is never sold in v1: f4).</summary>
    public bool ForSale(FurnitureDefinition kind)
    {
        if (kind == null || catalogue == null || catalogue.Find(kind.name) != kind) return false;
        if (kind.PrefabFor(kind.FirstVariant) == null) return false;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 1;
        return day >= kind.forSaleFromDay;
    }

    /// <summary>Buys a piece and puts it down: the rules and the money first, then it's paid, placed and saved. Null if not.</summary>
    public PlacedPiece Buy(FurnitureDefinition kind, Vector3 pivot, float yaw, string variant = "")
    {
        if (!Closed || !ForSale(kind) || InHand != null) return null;
        string plant = kind.IsPlant && string.IsNullOrEmpty(variant) ? kind.FirstVariant : variant ?? "";
        GameObject prefab = kind.PrefabFor(plant);
        PlacedPiece template = prefab != null ? prefab.GetComponent<PlacedPiece>() : null;
        if (template == null) return null;
        int money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0;
        LastResult = FurnitureRules.Check(Map, pieces, kind, template, pivot, yaw, null, money);
        if (!LastResult.Fits || ShopEconomy.Instance == null) return null;
        // 26 Oct 2026 (Days and first table): today's price (Day 1's first table is free), read before the piece exists.
        int price = FurnitureRules.PriceToday(kind);
        if (price > 0) ShopEconomy.Instance.AddMoney(-price);
        PlacedPiece made = Make(kind, prefab, "p" + nextId, 1, plant, Flat(pivot), yaw);
        nextId++;
        if (made == null)
        {
            if (price > 0) ShopEconomy.Instance.AddMoney(price);   // nothing was made: nothing is charged
            return null;
        }
        if (price == 0 && kind.price > 0) freeIds.Add(made.Id);
        pieces.Add(made);
        AfterChange(made);
        return made;
    }

    /// <summary>Moves a piece (free): the rules first, then it's put down there and saved. False if not.</summary>
    public bool Move(PlacedPiece piece, Vector3 pivot, float yaw)
    {
        if (!Closed || piece == null || !pieces.Contains(piece) || piece.Kind == null) return false;
        LastResult = FurnitureRules.Check(Map, pieces, piece.Kind, piece, pivot, yaw, piece, int.MaxValue);
        if (!LastResult.Fits) return false;
        piece.SetPose(Flat(pivot), yaw);
        piece.SetOn(true);
        if (InHand == piece) InHand = null;
        AfterChange(piece);
        return true;
    }

    /// <summary>Sells a piece back for half its price (f3): a bought one goes, one of the café's own is switched off. Saved.</summary>
    public bool Sell(PlacedPiece piece)
    {
        if (!Closed || piece == null || !pieces.Contains(piece)) return false;
        int refund = SellBackOf(piece);   // 26 Oct 2026 (Days and first table): $0 for Day 1's free table
        freeIds.Remove(piece.Id);
        if (InHand == piece) InHand = null;
        piece.SetOn(false);
        if (!piece.FromScene)
        {
            pieces.Remove(piece);
            Destroy(piece.gameObject);
        }
        if (ShopEconomy.Instance != null && refund > 0) ShopEconomy.Instance.AddMoney(refund);
        BanquetteRun.Refresh(pieces);
        Physics.SyncTransforms();
        Persist();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Picks a placed piece up (Arrange's ✕ on it): it goes off, its cups cleared, until it's put down (Move) or back (PutBack).</summary>
    public bool PickUp(PlacedPiece piece)
    {
        if (!Closed || piece == null || !pieces.Contains(piece) || !piece.On) return false;
        if (InHand != null) PutBack();
        InHand = piece;
        homePivot = piece.Pivot;
        homeYaw = piece.Yaw;
        piece.SetOn(false);
        BanquetteRun.Refresh(pieces);
        return true;
    }

    /// <summary>The piece in hand back exactly where it was (Arrange's ○), even off the grid. Nothing changes on disk.</summary>
    public void PutBack()
    {
        PlacedPiece p = InHand;
        if (p == null) return;
        InHand = null;
        p.SetPose(homePivot, homeYaw);
        p.SetOn(true);
        BanquetteRun.Refresh(pieces);
        Physics.SyncTransforms();
    }

    /// <summary>Where the piece in hand was picked up from.</summary>
    public void HomeOf(out Vector3 pivot, out float yaw)
    {
        pivot = homePivot;
        yaw = homeYaw;
    }

    /// <summary>Arrange has closed: what remembers the room looks again (the laptops and totes, the cut-away walls' fixtures), the caps.</summary>
    public void AfterArranging()
    {
        if (InHand != null) PutBack();
        NpcLifeProps.Refresh();
        CutawayWall.ForgetCandidates();
        CutawayWall.PrepareCandidates();
        BanquetteRun.Refresh(pieces);
        Physics.SyncTransforms();
        map = null;
    }

    /// <summary>The seats of the pieces that are on (labs).</summary>
    public void SeatsOn(List<TableSeat> into)
    {
        if (into == null) return;
        foreach (PlacedPiece p in pieces)
            if (p != null && p.On)
                foreach (TableSeat s in p.Seats) if (s != null) into.Add(s);
    }

    /// <summary>A piece by its save id (labs).</summary>
    public PlacedPiece Find(string id)
    {
        foreach (PlacedPiece p in pieces) if (p != null && p.Id == id) return p;
        return null;
    }

    // ------------------------------------------------------------------ the plumbing

    PlacedPiece Make(FurnitureDefinition kind, GameObject prefab, string id, int tier, string variant, Vector3 pivot, float yaw)
    {
        GameObject go = Instantiate(prefab, Flat(pivot), Quaternion.Euler(0f, Mathf.Repeat(yaw, 360f), 0f), boughtParent);
        go.name = $"{(kind != null ? kind.displayName : prefab.name)} (bought, {id})";
        PlacedPiece piece = go.GetComponent<PlacedPiece>();
        if (piece == null)
        {
            Debug.LogError($"[Café layout] {prefab.name} has no PlacedPiece at its root: Furniture 2 builds the prefabs.", this);
            Destroy(go);
            return null;
        }
        piece.Init(kind, id, false, tier, variant);
        piece.SetPose(Flat(pivot), yaw);
        if (!go.activeSelf) go.SetActive(true);
        return piece;
    }

    // After a buy or a move: the sheets it landed on fold away, the caps, the colliders, the save, the event.
    void AfterChange(PlacedPiece piece)
    {
        // Its footprint on a sheet, or one of its people standing on one: that sheet folds away, its boxes with it.
        FurnitureRules.Shape shape = piece.Shape;
        foreach (DustSheet s in Sheets)
            if (s != null && s.Lying && FurnitureRules.FoldsSheet(shape, s.Area)) s.FoldAway();
        BanquetteRun.Refresh(pieces);
        Physics.SyncTransforms();
        Persist();
        Changed?.Invoke();
    }

    // Saved at once, as the Shop's purchases are. If it fails, the change stays on screen and the phone shows the save's error
    // (SaveManager.LastSaveError); nothing is charged twice (the money moved once, with the piece).
    static void Persist()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.TrySaveRecap();
        else Debug.LogError("[Save] A furniture change could not be saved: no SaveManager.");
    }

    void CountFixedSeats()
    {
        fixedSeats = 0;
        foreach (TableSeat s in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
            if (s != null && s.isActiveAndEnabled && s.GetComponentInParent<PlacedPiece>(true) == null) fixedSeats++;
    }

    static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);
    static float Round(float v, float by) => Mathf.Round(v * by) / by;
}
