using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// PLACE, MOVE AND SELL: A PLAY CHECK (24 Oct 2026; Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md §5.2, with his
// answers of 24 Oct: f1 arrange only while closed, f2 the 25 cm grid and quarter turns, f3 half back on a sale, moving free)
//
// Fixit Fidget > Furniture > Lab - Place, move and sell (FurnitureLabMenus): a lab save of today's café at Day 2's closing
// with $1,000 (written the old way, before the layout existed: the café's 17 own pieces, 28 seats). [Furniture]:
//   1. The phone's Shop shows the YOUR CAFÉ card; its button (its own onClick, as a player presses it) opens Arrange: the
//      camera at Arrange's view, the strip up, nobody left in the café.
//   2. The bistro at (2.75, 1.75), by the front window (the spec's (3.55, 0.80) isn't on the 25 cm grid and is too close to
//      the front plants since the café's own pieces count: the rules' twin found this spot): green; placed, the till
//      1,000 -> 920, seats 28 -> 30, WaitingArea's registered spots and free seats up by 2, the save on disk lists it.
//   3. The square table at (1.0, 9.5), in the aisle: red, the plate "Keep the way from the door clear"; ✕ does nothing.
//   4. The new bistro picked up and put down at (-2.75, 2.0) (the spec's (-3.70, 1.05) is in the reading corner: green
//      there only in a café without it): green, no money moves, seats unchanged. Turned: the ghost's verdict equals a
//      direct FurnitureRules.Check (red there: no room to sit). ○ puts it back where it was.
//   5. Hold □ on it (a virtual pad): nothing at 0.4 s; then sold, the till +$40, seats 28.
//   6. One of the café's own sold, the north bistro: +$40, seats 26, its two seats out of the registry.
//      Then a bistro bought again by the front window, for step 9.
//   7. ○ with nothing in hand leaves: back on the phone's Shop; NpcLifeProps and the cut-away walls' candidates counted.
//  10. (Run here, at the closing, before the night: the café is empty.) The crammed café, today's 17 pieces and six
//      bistros where the rules' twin put them one after another nearest the counter (furniture-spec.md §3.2 had five; with
//      the lamp fixed there's room for a sixth), loaded through CafeLayout.Apply: 40 seats, both guards pass, and the worst
//      of the next 30 frames is under 50 ms (the carving's cost; his Mac's number is what counts, reported every run). The
//      layout before it is loaded back.
//   8. Close up, the night skipped (RecapUI.ContinueAfterNight), Day 3 opens: from the SpawnPoint, a complete
//      NavMesh.CalculatePath to every stand point, waiting spot and queue place, the carving in place on every piece that's
//      on and off on every piece that isn't.
//   9. The day with the lab's autopilot; patrons sent in to fill the room (CafeLabDirector.SendPatronsNow): somebody sits
//      at one of the bought bistro's seats.
// The playtest save is never written (the lab's own save is the only file the game writes). Photos: the strip, the green
// ghost, the red ghost and its plate, the placed table, the hold ring, the crammed café, a placed seat in use; the words
// in each measured (PlayLab.Photo). Report and photos: Logs/Furniture/place-move-sell-<time>/.
// 26 Oct 2026 (Icons 2): with the icon sheet in place, the YOUR CAFÉ card shows the seven kinds' pictures in a row (Day 3's
// two dimmed at Day 2's closing) and every card on the strip wears its kind's picture on the taller card: two checks more.
//
// AtClosing (below) is the half-finished café's evening for DayShapeCheck (§5.5): Fixit Fidget > Days > Lab - Day N at the
// counter (half-finished café) runs the day on the café a new game opens (his answer r1: 10 seats), and at Day 1's
// closing the Shop's badge and card, Arrange from its button, and the bistro placed where the ghost first waits.
// 26 Oct 2026 (Days and first table): Day 1's first table is free (FurnitureRules.PriceToday; on his Mac a $70 Day 1 couldn't
// buy the $80 bistro). AtClosing now proves the money moved right both ways: the card and the plate show "$80" struck and
// "Free today", it is placed for $0 (the seats 10 -> 12, the sheets folded as before), the next table is full price again
// (the square $90, the bistro $80; the plant $35 throughout), the free table sells back for $0 (never $40), and placed again
// it is still $0 (a sale that gave nothing back makes a rebuy a move).
// ---------------------------------------------------------------------------
public sealed class FurnitureCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Furniture.Check";
    /// <summary>The Days labs on the half-finished café: DayShapeCheck reads it (FurnitureLabMenus sets it).</summary>
    public const string HalfFinishedKey = "FixitFidget.Furniture.HalfFinished";
    public const string PlaytestSaveName = "playtest-aces-cafe.json";
    public const int StartMoney = 1000;
    public const float WorstFrameMs = 50f;
    public const string BistroKind = "Bistro table", SquareKind = "Square table";
    const string NorthBistro = "scene:Bistro table, north";

    static readonly Vector3 GreenSpot = new Vector3(2.75f, 0f, 1.75f);
    static readonly Vector3 AisleSpot = new Vector3(1f, 0f, 9.5f);
    static readonly Vector3 MoveSpot = new Vector3(-2.75f, 0f, 2f);
    // The cram: six bistros, square, where the rules' twin found room one after another nearest the counter (FurnitureRulesCheck 6).
    static readonly Vector3[] Cram =
    {
        new Vector3(-3.25f, 0f, 10f), new Vector3(2.75f, 0f, 8.75f), new Vector3(3f, 0f, 5.5f),
        new Vector3(3f, 0f, 3.75f), new Vector3(-2.5f, 0f, 2.75f), new Vector3(-2.75f, 0f, 1.75f),
    };

    protected override string Title => "Furniture: place, move and sell - play check";
    protected override string Tag => "[Furniture]";

    DateTime playtestBefore;

#if UNITY_EDITOR
    // Asked for by the editor (FurnitureLabMenus) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Furniture] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Furniture check (this Play session only)");
        var check = go.AddComponent<FurnitureCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Furniture", $"place-move-sell-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    protected override IEnumerator Run()
    {
        playtestBefore = PlaytestSaveTime();
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapUI.Showing && CafeLayout.Instance != null
                                 && ArrangeMode.Instance != null && ShopEconomy.Instance != null, 20f,
            "the lab opened on a closed day with its recap on screen");
        if (!lastWait) yield break;
        yield return null;   // the phone builds its app before the frame is drawn
        CafeLayout layout = CafeLayout.Instance;
        ArrangeMode arrange = ArrangeMode.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        RecapUI recap = FindAnyObjectByType<RecapUI>();
        RecapPhone phone = recap != null ? recap.Phone : null;
        FurnitureDefinition bistro = layout.Catalogue != null ? layout.Catalogue.Find(BistroKind) : null;
        FurnitureDefinition square = layout.Catalogue != null ? layout.Catalogue.Find(SquareKind) : null;
        Check(phone != null && bistro != null && square != null,
              "the recap is Ace's phone (Use Phone is on); the catalogue has the bistro and the square table (Furniture 1 and 2)");
        if (phone == null || bistro == null || square == null) yield break;
        Check(layout.AppliedFrom == CafeLayout.Source.OldSave && layout.Seats == 28 && till.Money == StartMoney && DayClock.Instance.Day == 2,
              $"today's café from a lab save written the old way: {layout.AppliedFrom} (OldSave), {layout.Seats} seats (28), ${till.Money} " +
              $"(${StartMoney}), Day {DayClock.Instance.Day} (2)");

        // ---------- 1. the Shop's card, and its button ----------
        phone.Open(RecapPhone.App.Shop);
        yield return Until(() => phone.Settled && phone.Current == RecapPhone.App.Shop, 5f, "1. the phone is on the Shop");
        string shop = phone.ContentText();
        Button button = phone.ContentButton("arrange");
        Check(shop.Contains("YOUR CAFÉ") && shop.Contains("28 seats") && button != null && button.interactable,
              $"1. the Shop shows the YOUR CAFÉ card (\"YOUR CAFÉ\" {Seen(shop, "YOUR CAFÉ")}, \"28 seats\" {Seen(shop, "28 seats")}; " +
              $"its button {(button == null ? "missing" : button.interactable ? "pressable" : "greyed")})");
        // 26 Oct 2026 (Icons 2): the card shows what Arrange sells as pictures, Day 2's five at full and Day 3's two dimmed.
        if (PadInput.IconsAvailable)
        {
            string row = phone.YourCafeIcons;
            int shownKinds = row.Length > 0 ? row.Split(';').Length : 0;
            Check(shownKinds == NoteIcons.FurnitureKinds.Length && row.Contains(NoteIcons.FurnBistro + " on sale") && row.Contains(NoteIcons.FurnBanquette + " on sale")
                  && row.Contains(NoteIcons.FurnCounter + " on sale") && row.Contains(NoteIcons.FurnLounge + " Day 3") && row.Contains(NoteIcons.FurnCommunal + " Day 3"),
                  $"...with the {NoteIcons.FurnitureKinds.Length} kinds' pictures in a row, Day 3's two dimmed ({(row.Length > 0 ? row : "none drawn")})");
        }
        else Note("no icon sheet (Fixit Fidget › UI › Button icons): the YOUR CAFÉ card and the strip show words only; their pictures weren't checked");
        yield return Photo("01-shop-your-cafe");
        if (button == null) yield break;
        button.onClick.Invoke();
        yield return Until(() => ArrangeMode.Active, 3f, "...its button (its own onClick) opens Arrange");
        if (!lastWait) yield break;
        yield return Seconds(ArrangeMode.EaseSeconds + .4f);
        Camera cam = Camera.main;
        int inside = FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Length + FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Length;
        float far = cam != null ? Vector3.Distance(cam.transform.position, new Vector3(0f, 0f, 6.5f)) : 0f;
        float down = cam != null ? cam.transform.forward.y : 0f;
        bool strip = arrange.Screen != null && arrange.Screen.Showing && arrange.Screen.Strip != null && arrange.Screen.Strip.gameObject.activeInHierarchy;
        Check(cam != null && far > 12f && down < -.3f && strip && inside == 0,
              $"...the camera at Arrange's view ({far:0.0} m from the room's middle, looking down {down:0.00}), the strip {(strip ? "up" : "NOT up")}, " +
              $"{inside} people left in the café");
        Note("the strip: " + (arrange.Screen != null ? arrange.Screen.CardsLine : "none"));
        // 26 Oct 2026 (Icons 2): every card wears its kind's picture (none "(none)"), on the taller card.
        if (PadInput.IconsAvailable)
        {
            string pictures = arrange.Screen != null ? arrange.Screen.IconsLine : "";
            int cards = pictures.Length > 0 ? pictures.Split(';').Length : 0;
            Check(cards == NoteIcons.FurnitureKinds.Length && !pictures.Contains("(none)") && arrange.Screen != null && arrange.Screen.CardHeight > 120f,
                  $"...each of the {cards} cards wears its kind's picture, the cards {(arrange.Screen != null ? arrange.Screen.CardHeight : 0f):0} px tall ({pictures})");
        }
        yield return Photo("02-arrange-strip");

        // ---------- 2. a bistro by the front window ----------
        int registered = WaitingArea.RegisteredSpots, free = WaitingArea.FreeSeats, seats = layout.Seats, money = till.Money;
        arrange.Choose(bistro);
        arrange.CursorTo(GreenSpot);
        yield return Face(arrange, 0f);   // a card keeps the last turn: the spot is the twin's, square to the room
        FurnitureRules.Result green = arrange.Recheck();
        yield return Frames(2);
        bool ghostGreen = arrange.World != null && arrange.World.GhostShowing && !arrange.World.GhostRed;
        Check(green.Fits && ghostGreen && Near(arrange.GhostPivot, GreenSpot),
              $"2. the bistro's ghost at {P(GreenSpot)} by the front window: {green}; the ghost {(ghostGreen ? "green" : "NOT green")} at {P(arrange.GhostPivot)}");
        yield return Photo("03-green-ghost");
        bool placed = arrange.PlaceNow();
        yield return Frames(3);
        PlacedPiece made = layout.Pieces.LastOrDefault(p => p != null && !p.FromScene);
        Check(placed && made != null && till.Money == money - bistro.price && layout.Seats == seats + 2,
              $"...placed: the till ${money} -> ${till.Money} (${money - bistro.price}), seats {seats} -> {layout.Seats} ({seats + 2})");
        Check(WaitingArea.RegisteredSpots == registered + 2 && WaitingArea.FreeSeats == free + 2,
              $"...its two seats in the registry: spots {registered} -> {WaitingArea.RegisteredSpots}, free seats {free} -> {WaitingArea.FreeSeats} (each up by 2)");
        SaveData onDisk = ReadLabSave();
        bool listed = made != null && onDisk != null && onDisk.cafe != null && onDisk.cafe.format == CafeLayoutData.CurrentFormat
                      && onDisk.cafe.pieces != null && onDisk.cafe.pieces.Any(d => d != null && d.id == made.Id && d.kind == BistroKind);
        Check(listed, $"...the save on disk lists it ({(made != null ? made.Id : "nothing made")}; format {(onDisk != null && onDisk.cafe != null ? onDisk.cafe.format : -1)}, " +
                      $"{(onDisk != null && onDisk.cafe != null && onDisk.cafe.pieces != null ? onDisk.cafe.pieces.Length : 0)} pieces listed)");
        yield return Photo("04-placed");
        if (made == null) yield break;

        // ---------- 3. the square table in the aisle ----------
        money = till.Money;
        arrange.Choose(square);
        arrange.CursorTo(AisleSpot);
        yield return Face(arrange, 0f);
        FurnitureRules.Result aisle = arrange.Recheck();
        yield return Frames(3);   // the plate follows the ghost
        string plate = arrange.Screen != null ? arrange.Screen.PlateLine : "";
        bool red = arrange.World != null && arrange.World.GhostRed;
        Check(aisle.verdict == FurnitureRules.Verdict.Aisle && red && plate.Contains("Keep the way from the door clear"),
              $"3. a square table at {P(AisleSpot)}, in the aisle: {aisle}; the ghost {(red ? "red" : "NOT red")}, the plate \"{Plain(plate)}\"");
        yield return Photo("05-red-ghost-plate");
        int bought = layout.Pieces.Count(p => p != null && !p.FromScene);
        bool refused = !arrange.PlaceNow();
        yield return Frames(2);
        Check(refused && till.Money == money && layout.Pieces.Count(p => p != null && !p.FromScene) == bought,
              $"...✕ does nothing there ({(refused ? "refused" : "PLACED")}): the till ${money} -> ${till.Money}");
        arrange.BackNow();   // the card down
        yield return Frames(2);

        // ---------- 4. moved, then turned ----------
        money = till.Money;
        seats = layout.Seats;
        yield return PointAt(arrange, made);
        bool picked = arrange.Pointed == made && arrange.PlaceNow() && arrange.Carried == made;
        Check(picked, $"4. ✕ on the new bistro picks it up ({(arrange.Carried == made ? "carried" : "not carried")})");
        arrange.CursorTo(MoveSpot);
        yield return Frames(2);
        FurnitureRules.Result moved = arrange.Recheck();
        bool put = moved.Fits && arrange.PlaceNow();
        yield return Frames(2);
        Check(put && Near(made.Pivot, MoveSpot) && till.Money == money && layout.Seats == seats,
              $"...put down at {P(MoveSpot)}: {moved}; now at {P(made.Pivot)}; the till ${money} -> ${till.Money} (moving is free), seats {seats} -> {layout.Seats}");
        yield return PointAt(arrange, made);
        bool up = arrange.Pointed == made && arrange.PlaceNow();
        bool turned = up && arrange.TurnNow();
        yield return Frames(2);
        FurnitureRules.Result ghostSays = arrange.Recheck();
        FurnitureRules.Result direct = FurnitureRules.Check(layout.Map, layout.Pieces, made.Kind, arrange.GhostPivot, arrange.GhostYaw, made);
        Check(turned && Mathf.Abs(Mathf.DeltaAngle(arrange.GhostYaw, made.Yaw + 90f)) < .5f && ghostSays.verdict == direct.verdict && ghostSays.words == direct.words,
              $"...turned a quarter ({arrange.GhostYaw:0}°): the ghost says {ghostSays}; a direct FurnitureRules.Check says {direct}");
        arrange.BackNow();   // back exactly where it was
        yield return Frames(2);
        Check(arrange.InHand == ArrangeMode.Hand.Nothing && made.On && Near(made.Pivot, MoveSpot) && Mathf.Abs(Mathf.DeltaAngle(made.Yaw, 0f)) < .5f,
              $"...○ puts it back where it was ({P(made.Pivot)}, {made.Yaw:0}°)");

        // ---------- 5. hold □ on it ----------
        PlugInPad("Furniture lab pad");
        yield return Frames(2);
        money = till.Money;
        yield return PointAt(arrange, made);
        Check(arrange.Pointed == made, "5. the cursor on the new bistro points at it");
        PadHold(new GamepadState().WithButton(GamepadButton.West));
        yield return Seconds(.4f);
        float early = arrange.SellProgress, ring = arrange.Screen != null ? arrange.Screen.HoldShown : 0f;
        bool still = made != null && layout.Pieces.Contains(made) && till.Money == money;
        yield return Photo("06-hold-ring");
        Check(still && early > 0f && early < 1f, $"...held 0.4 s: nothing sold yet (the hold {early:P0}, its ring {ring:P0}), the till ${till.Money}");
        yield return Await(() => made == null || !layout.Pieces.Contains(made), 1.5f);
        bool soldIt = lastWait;
        PadRelease();
        yield return Frames(3);
        Check(soldIt && till.Money == money + bistro.SellBack && layout.Seats == 28,
              $"...then sold (at {ArrangeMode.SellHoldSeconds:0.0} s): the till ${money} -> ${till.Money} (+${bistro.SellBack}), seats {layout.Seats} (28)");

        // ---------- 6. one of the café's own ----------
        PlacedPiece north = layout.Find(NorthBistro);
        money = till.Money;
        registered = WaitingArea.RegisteredSpots;
        bool sold = false;
        if (north != null)
        {
            yield return PointAt(arrange, north);
            sold = arrange.Pointed == north && arrange.SellNow();
        }
        yield return Frames(3);
        Check(sold && north != null && !north.On && till.Money == money + bistro.SellBack && layout.Seats == 26 && WaitingArea.RegisteredSpots == registered - 2,
              $"6. the café's own north bistro sold: {(north == null ? "NOT FOUND" : north.On ? "still on" : "switched off")}, the till ${money} -> ${till.Money} " +
              $"(+${bistro.SellBack}), seats {layout.Seats} (26), registered spots {registered} -> {WaitingArea.RegisteredSpots}");

        // A bistro again by the front window, for tomorrow's step 9.
        arrange.Choose(bistro);
        arrange.CursorTo(GreenSpot);
        yield return Face(arrange, 0f);
        bool again = arrange.Recheck().Fits && arrange.PlaceNow();
        yield return Frames(2);
        made = layout.Pieces.LastOrDefault(p => p != null && !p.FromScene);
        string madeId = made != null ? made.Id : "";
        Check(again && made != null && layout.Seats == 28 && till.Money == StartMoney - bistro.price,
              $"...a bistro bought again at {P(GreenSpot)} for tomorrow ({madeId}): seats {layout.Seats} (28), the till ${till.Money} (${StartMoney - bistro.price})");

        // ---------- 7. leave ----------
        arrange.BackNow();
        yield return Until(() => !ArrangeMode.Active && RecapUI.Showing, 3f, "7. ○ with nothing in hand leaves Arrange, back on the phone");
        yield return Seconds(.4f);
        string after = phone.ContentText();
        Check(phone.Current == RecapPhone.App.Shop && after.Contains("28 seats"), $"...on the Shop, its card saying \"28 seats\" ({Seen(after, "28 seats")})");
        Note($"after leaving: NpcLifeProps {NpcLifeProps.LaptopCount} laptops and {NpcLifeProps.BagCount} bags; the cut-away walls' candidates {CutawayCandidates()}");

        // ---------- 10. the crammed café, at the closing ----------
        CafeLayoutData before = layout.Snapshot();
        var cram = new List<PlacedPieceData>();
        foreach (PlacedPiece p in layout.OwnPieces) if (p != null) cram.Add(DataOf(p));
        for (int i = 0; i < Cram.Length; i++)
            cram.Add(new PlacedPieceData { id = "p" + (90 + i), kind = BistroKind, x = Cram[i].x, z = Cram[i].z, yaw = 0f });
        layout.Apply(new SaveData { cafe = new CafeLayoutData { format = CafeLayoutData.CurrentFormat, pieces = cram.ToArray(), nextId = 100 } }, true);
        float worst = 0f;
        for (int i = 0; i < 30; i++)
        {
            yield return null;
            worst = Mathf.Max(worst, Time.unscaledDeltaTime);
        }
        List<PlacedPiece> on = layout.Pieces.Where(p => p != null && p.On).ToList();
        FurnitureRules.Result crammed = FurnitureRules.CheckWholeLayout(layout.Map, on);
        Check(layout.Seats == 40 && on.Count == 23 && crammed.Fits && crammed.reached == crammed.places && crammed.served == crammed.servedOf,
              $"10. the crammed café (today's 17 pieces and six bistros, loaded): {layout.Seats} seats (40), {on.Count} pieces; both guards {crammed} " +
              $"({crammed.reached} of {crammed.places} reached, {crammed.served} of {crammed.servedOf} served; the twin: 50 and 47)");
        Check(worst * 1000f < WorstFrameMs, $"...the worst of the next 30 frames {worst * 1000f:0.0} ms (under {WorstFrameMs:0} ms on his Mac: the carving's cost)");
        yield return Photo("07-crammed");
        layout.Apply(new SaveData { cafe = before }, true);
        yield return Frames(3);
        made = layout.Find(madeId);
        Check(layout.Seats == 28 && made != null && made.On && north != null && !north.On, $"...the layout before it loaded back: {layout.Seats} seats (28), {madeId} on, the north bistro off");

        // ---------- 8. close up, skip the night, open tomorrow ----------
        int day = DayClock.Instance.Day;
        bool opened = recap != null && recap.ContinueAfterNight();
        yield return Until(() => DayClock.Instance.Day == day + 1 && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver, 10f,
            $"8. closed up with the night skipped: Day {day + 1} opens ({(opened ? "the recap's own way" : "it refused")})");
        if (!lastWait) yield break;
        yield return Seconds(1.5f);   // the carving settles once the world moves
        GameObject spawn = GameObject.Find("SpawnPoint");
        Vector3 from = spawn != null ? spawn.transform.position : new Vector3(0f, .06f, -1.2f);
        FurnitureRules.FloorMap map = FurnitureRules.BuildMap();
        var points = new List<(string what, Vector3 at)>();
        foreach (Vector3 w in map.waiting) points.Add(("a waiting spot", w));
        foreach (Vector3 q in map.queue) points.Add(("a queue place", q));
        foreach (Vector3 s in map.fixedSeatStands) points.Add(("a repair-bar stool", s));
        var stands = new List<Vector3>();
        foreach (PlacedPiece p in layout.Pieces)
        {
            if (p == null || !p.On) continue;
            stands.Clear();
            p.StandPoints(stands);
            foreach (Vector3 s in stands) points.Add((p.DisplayName, s));
        }
        var path = new NavMeshPath();
        bool startOk = NavMesh.SamplePosition(from, out NavMeshHit start, 1f, NavMesh.AllAreas);
        int complete = 0;
        string missed = "";
        foreach ((string what, Vector3 at) in points)
        {
            bool ok = startOk && NavMesh.SamplePosition(at, out NavMeshHit end, .5f, NavMesh.AllAreas)
                      && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (ok) complete++;
            else if (missed.Length == 0) missed = $"{what} at {P(at)}";
        }
        Check(spawn != null && points.Count > 0 && complete == points.Count,
              $"...from the SpawnPoint{(spawn == null ? " (NOT FOUND: from the door)" : "")} a complete path to every stand point, waiting spot and queue place: " +
              $"{complete} of {points.Count}" + (missed.Length > 0 ? $" (the first missed: {missed})" : ""));
        int carving = 0, wrong = 0;
        foreach (PlacedPiece p in layout.Pieces)
        {
            if (p == null) continue;
            foreach (NavMeshObstacle o in p.Obstacles)
            {
                if (o == null) continue;
                bool live = o.isActiveAndEnabled && o.carving;
                if (live != p.On) wrong++;
                else if (live) carving++;
            }
        }
        Check(carving > 0 && wrong == 0, $"...the carving in place: {carving} carving obstacles on the pieces that are on; {wrong} wrong (on a piece that's off, or off on one that's on)");

        // ---------- 9. the day: a placed seat in use ----------
        made = layout.Find(madeId);
        List<TableSeat> placedSeats = made != null ? made.Seats.Where(s => s != null).ToList() : new List<TableSeat>();
        CafeLabDirector director = CafeLabDirector.Instance;
        int sent = director != null ? director.SendPatronsNow(Mathf.Max(4, WaitingArea.FreeSeats + 2)) : 0;
        float sentAt = Time.realtimeSinceStartup;
        yield return Await(() => placedSeats.Any(s => s != null && s.Occupant != null && WaitingArea.HasArrived(s)), 150f);
        TableSeat used = placedSeats.FirstOrDefault(s => s != null && s.Occupant != null && WaitingArea.HasArrived(s));
        Check(used != null, "9. somebody sat at a bought bistro's seat: " + (used != null
            ? $"{used.Occupant.name} at {used.name}, {Time.realtimeSinceStartup - sentAt:0} s after {sent} patrons were sent in to fill the room"
            : $"nobody in {Time.realtimeSinceStartup - sentAt:0} s ({sent} patrons sent in; the director {(director != null ? "on" : "MISSING")})"));
        if (used != null) yield return Photo("08-placed-seat-in-use");

        Check(PlaytestSaveTime() == playtestBefore, $"the playtest save ({PlaytestSaveName}) was never written");
    }

    // ================================================================== the half-finished café's evening (DayShapeCheck)

    /// <summary>
    /// The half-finished café's closing, for DayShapeCheck's Days labs on it (furniture-spec.md §5.5, with his answers of 24 Oct):
    /// the seats the day had (10 / 12 / 14 on Days 1 / 2 / 3: his r1 and the week's model); on Day 1 the Shop's badge and
    /// YOUR CAFÉ card, Arrange from the card's button, and the bistro placed where the ghost first waits (green, $80, 12 seats,
    /// the sheet under it folded); on Day 2 the bay and the window counter on sale, on Day 3 the communal table and the lounge.
    /// </summary>
    public static IEnumerator AtClosing(int day, Action<bool, string> check, Action<string> note, Func<string, IEnumerator> photo)
    {
        float until = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < until && !(DayClock.Instance != null && DayClock.Instance.DayOver && RecapUI.Showing)) yield return null;
        RecapUI recap = FindAnyObjectByType<RecapUI>();
        RecapPhone phone = recap != null ? recap.Phone : null;
        CafeLayout layout = CafeLayout.Instance;
        ArrangeMode arrange = ArrangeMode.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        check(phone != null && layout != null && arrange != null && till != null && RecapUI.Showing,
              "the half-finished café's evening: the recap is up (Ace's phone), the café's layout and Arrange are there");
        if (phone == null || layout == null || arrange == null || till == null || layout.Catalogue == null) yield break;
        yield return null;
        int expected = day <= 1 ? 10 : day == 2 ? 12 : 14;
        int lying = layout.Sheets.Count(s => s != null && s.Lying);
        check(layout.AppliedFrom == CafeLayout.Source.SavedLayout && layout.Seats == expected,
              $"the day ran on the half-finished café: {layout.AppliedFrom} (SavedLayout), {layout.Seats} seats ({expected}: his answer r1, and the week's model " +
              $"for Days 2 and 3), {lying} dust sheets still lying");
        FurnitureDefinition Kind(string name) => layout.Catalogue.Find(name);
        if (day >= 2)
        {
            FurnitureDefinition bay = Kind("Banquette bay"), counter = Kind("Window counter"), lounge = Kind("Lounge corner"), communal = Kind("Communal table");
            bool two = layout.ForSale(bay) && layout.ForSale(counter);
            bool three = layout.ForSale(lounge) && layout.ForSale(communal);
            if (day == 2)
                check(two && !three, $"Day 2's closing: the bay and the window counter on sale ({(two ? "yes" : "NO")}); the lounge and the communal table not yet ({(three ? "ON SALE" : "not")})");
            else
                check(two && three, $"Day 3's closing: everything on sale, the communal table and the lounge corner with it ({(three ? "yes" : "NO")})");
            yield break;
        }

        // Day 1: the badge, the card, Arrange from its button, the first table.
        check(phone.BadgeShowing(RecapPhone.App.Shop) && phone.BadgeText(RecapPhone.App.Shop) == "!" && phone.ShopFirstTable,
              $"Day 1's closing: the Shop's badge says \"{phone.BadgeText(RecapPhone.App.Shop)}\" (\"!\": the first table waits)");
        phone.Open(RecapPhone.App.Shop);
        until = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < until && !(phone.Settled && phone.Current == RecapPhone.App.Shop)) yield return null;
        string shop = phone.ContentText();
        Button button = phone.ContentButton("arrange");
        check(shop.Contains("YOUR CAFÉ") && shop.Contains("Place your first table") && shop.Contains("10 seats") && button != null && button.interactable,
              $"...the YOUR CAFÉ card: \"10 seats\" {Seen(shop, "10 seats")}, \"Place your first table\" {Seen(shop, "Place your first table")}, " +
              $"its button {(button == null ? "missing" : button.interactable ? "pressable" : "greyed")}");
        yield return photo("90-day1-your-cafe");
        if (button == null) yield break;
        int money = till.Money;
        button.onClick.Invoke();
        until = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < until && !ArrangeMode.Active) yield return null;
        check(ArrangeMode.Active, "...its button opens Arrange");
        if (!ArrangeMode.Active) yield break;
        until = Time.realtimeSinceStartup + ArrangeMode.EaseSeconds + .4f;
        while (Time.realtimeSinceStartup < until) yield return null;
        FurnitureRules.Result first = arrange.Recheck();
        Vector3 at = arrange.GhostPivot;
        PlacedPiece template = arrange.Chosen != null ? FurnitureRules.TemplateOf(arrange.Chosen, arrange.ChosenVariant) : null;
        bool onSheet = template != null && layout.Sheets.Any(s => s != null && s.Lying && FurnitureRules.FoldsSheet(template.ShapeAt(at, arrange.GhostYaw), s.Area));
        check(arrange.Chosen != null && arrange.Chosen.name == BistroKind && first.Fits && onSheet,
              $"...the ghost first waits with the {(arrange.Chosen != null ? arrange.Chosen.displayName : "nothing")} at {P(at)} turned {arrange.GhostYaw:0}°: {first}, " +
              $"{(onSheet ? "on a dust sheet" : "NOT on a sheet")} (the rules' twin: (-4.75, 10.00) turned 90°, on the west banquette's north sheet)");   // 26 Oct 2026 (Mac run fix): a43 moved it from 10.25
        yield return photo("91-day1-first-ghost");
        // 26 Oct 2026 (Mac run fix): how many sheets this first table folds is the rules' own answer for where the ghost waits
        // (on his Mac, (-4.75, 10.00) turned folds the west banquette's two sheets: 5 -> 3), not a fixed one.
        int folds = template != null ? layout.Sheets.Count(s => s != null && s.Lying && FurnitureRules.FoldsSheet(template.ShapeAt(at, arrange.GhostYaw), s.Area)) : 0;
        // 26 Oct 2026 (Days and first table): the first table is free today, and its card and plate say so with the price struck.
        FurnitureDefinition bistroKind = Kind(BistroKind), squareKind = Kind(SquareKind), plantKind = Kind("Plant");
        string cardsBefore = arrange.Screen != null ? arrange.Screen.CardsLine : "";
        string plateBefore = arrange.Screen != null ? arrange.Screen.PlateLine : "";
        string freeCard = bistroKind != null ? $"{bistroKind.displayName} ${bistroKind.price} {FurnitureRules.FreeWords}" : "?";
        check(bistroKind != null && squareKind != null && FurnitureRules.PriceToday(bistroKind) == 0 && FurnitureRules.PriceToday(squareKind) == 0
              && (plantKind == null || FurnitureRules.PriceToday(plantKind) == plantKind.price)
              && cardsBefore.Contains(freeCard) && plateBefore.Contains($"<s>${bistroKind.price}</s> {FurnitureRules.FreeWords}"),
              $"...Day 1's first table is free: the bistro ${FurnitureRules.PriceToday(bistroKind)} and the square ${FurnitureRules.PriceToday(squareKind)} today " +
              $"(the plant ${(plantKind != null ? FurnitureRules.PriceToday(plantKind) : -1)}); the strip {(cardsBefore.Contains(freeCard) ? "shows" : "does NOT show")} \"{freeCard}\" " +
              $"and the plate {(plateBefore.Contains(FurnitureRules.FreeWords) ? "says" : "does NOT say")} it, the price struck (\"{plateBefore}\")");
        if (bistroKind == null || squareKind == null) yield break;
        bool placed = first.Fits && arrange.PlaceNow();
        until = Time.realtimeSinceStartup + .5f;
        while (Time.realtimeSinceStartup < until) yield return null;   // the sheet folds in real time
        int lyingAfter = layout.Sheets.Count(s => s != null && s.Lying);
        check(placed && till.Money == money && layout.Seats == 12 && folds >= 1 && lyingAfter == lying - folds,
              $"...placed there for $0: the till ${money} -> ${till.Money} (${money}), seats 10 -> {layout.Seats} (12), the sheets under it folded ({lying} -> {lyingAfter} lying; the rules say {folds})");
        PlacedPiece free = layout.Pieces.LastOrDefault(p => p != null && !p.FromScene && p.Kind == bistroKind);
        yield return null;
        string cardsAfter = arrange.Screen != null ? arrange.Screen.CardsLine : "";
        check(free != null && layout.WasFree(free) && layout.SellBackOf(free) == 0 && layout.TableBought
              && FurnitureRules.PriceToday(bistroKind) == bistroKind.price && FurnitureRules.PriceToday(squareKind) == squareKind.price
              && !cardsAfter.Contains(FurnitureRules.FreeWords) && cardsAfter.Contains($"{bistroKind.displayName} ${bistroKind.price}"),
              $"...then the next table is full price: the bistro ${FurnitureRules.PriceToday(bistroKind)} ({bistroKind.price}), the square ${FurnitureRules.PriceToday(squareKind)} ({squareKind.price}), " +
              $"no \"{FurnitureRules.FreeWords}\" on the strip; the free one sells back for ${(free != null ? layout.SellBackOf(free) : -1)} ($0, never ${bistroKind.SellBack})");
        if (arrange.Screen != null) note("Day 1's first table, the Now card: " + arrange.Screen.NowTitle + " / " + arrange.Screen.NowLine);
        yield return photo("92-day1-first-table");
        // 26 Oct 2026 (Days and first table): sold back, the free table gives $0; placed again where it was, it is still $0.
        if (free != null)
        {
            Vector3 freeAt = free.Pivot;
            float freeYaw = free.Yaw;
            int before = till.Money;
            bool sold = layout.Sell(free);
            yield return null;
            int afterSale = till.Money;
            PlacedPiece again = sold ? layout.Buy(bistroKind, freeAt, freeYaw) : null;
            yield return null;
            check(sold && afterSale == before && again != null && till.Money == before && layout.Seats == 12 && layout.WasFree(again),
                  $"...sold back it gives $0 (the till ${before} -> ${afterSale}), and placed again where it was it is still free " +
                  $"(the till ${till.Money}, seats {layout.Seats} (12); {(again != null ? "placed" : layout.LastResult.ToString())})");
        }
        arrange.BackNow();
        until = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < until && ArrangeMode.Active) yield return null;
        check(!ArrangeMode.Active && RecapUI.Showing && !phone.ShopFirstTable, "...○ leaves Arrange, back on the phone; the badge's first table is done");
    }

    // ================================================================== helpers

    // The new piece in hand turned to this many degrees (TurnNow a quarter at a time; a card keeps the turn it last had).
    IEnumerator Face(ArrangeMode arrange, float yaw)
    {
        yield return Frames(2);
        arrange.Recheck();
        for (int i = 0; i < 4 && Mathf.Abs(Mathf.DeltaAngle(arrange.GhostYaw, yaw)) > .5f; i++)
        {
            arrange.TurnNow();
            yield return Frames(1);
            arrange.Recheck();
        }
        yield return Frames(1);
    }

    IEnumerator PointAt(ArrangeMode arrange, PlacedPiece piece)
    {
        if (piece != null) arrange.CursorTo(piece.Pivot);
        yield return Frames(3);
    }

    static PlacedPieceData DataOf(PlacedPiece p) => new PlacedPieceData
    {
        id = p.Id,
        kind = p.Kind != null ? p.Kind.name : "",
        x = p.Pivot.x,
        z = p.Pivot.z,
        yaw = p.Yaw,
        tier = p.Tier,
        variant = p.Variant,
    };

    static SaveData ReadLabSave()
    {
        try { return SaveCheckpointStorage.Read(Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName)); }
        catch (Exception) { return null; }
    }

    static DateTime PlaytestSaveTime()
    {
        string path = Path.Combine(Application.persistentDataPath, PlaytestSaveName);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
    }

    static int CutawayCandidates()
    {
        FieldInfo field = typeof(CutawayWall).GetField("candidates", BindingFlags.Static | BindingFlags.NonPublic);
        return field != null && field.GetValue(null) is MeshRenderer[] found ? found.Length : -1;
    }

    static bool Near(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude < .002f;
    static string P(Vector3 p) => $"({p.x:0.00}, {p.z:0.00})";
    static string Seen(string text, string what) => text.Contains(what) ? "shown" : "MISSING";
    static string Plain(string rich) => Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " / ");
}
