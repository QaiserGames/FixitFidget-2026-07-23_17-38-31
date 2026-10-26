using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// SAVE AND LOAD A LAYOUT; AN OLD SAVE AND A NEW GAME: A PLAY CHECK (24 Oct 2026; furniture-spec.md §5.3 and §5.4, with his
// answers of 24 Oct: r1 the half-finished café's 10 open seats, f3 half back on a sale, f4 the tier saved from day one)
//
// Fixit Fidget > Furniture > Lab - Save and load (FurnitureLabMenus): a lab save of today's café at Day 2's closing with
// $1,000, written the old way (no layout in it). [Furniture save]:
//   1. An old save: today's café (OldSave), 28 seats, the café's 17 own pieces on, nothing bought, no sheets; both guards
//      pass. Saved: format 1 listing the 17. Loaded again: the same 17 at the same poses (1 mm, 0.1°).
//   2. A layout (§5.3): the lounge by the counter and the communal table sold (in today's café no table turned a quarter
//      fits anywhere: the rules' twin), a bistro, a square table turned and a monstera bought, each where the twin found
//      room first nearest the counter, and the south bistro moved: 22 seats, the money right, both guards pass.
//   3. Saved, then loaded from the lab save on disk (CafeLayout.Apply, as GameBootstrap does at a load; the scene isn't
//      reloaded): every piece's id, kind, pivot (1 mm), turn (0.1°), tier and variant as before; the lounge and the
//      communal table off; the money, the seats and the next piece's number as before; no piece twice; both guards pass.
//   3b. 26 Oct 2026 (Days and first table): Day 1's free first table in the save (PlacedPieceData.free): the bought bistro
//      marked free and loaded sells back for $0 (never $40), the other pieces for half as before, and the layout's snapshot
//      (through JSON, as the save file holds it) keeps the mark on it and on nothing else; an older save (no mark) is unchanged.
//   4. A new game (CafeLayout.Apply with no save, as a first run): the half-finished café, 10 seats (his r1), the café's 9
//      open pieces, the five dust sheets lying; its guards pass with the sheets' boxes solid. Day 1's two settled guests
//      are P2's (his r3) and not judged here.
//   5. A save with an unknown kind: that piece is skipped with one warning, everything else loads.
//   6. The layout back, then tomorrow opened (the night skipped): the morning's save holds the same layout.
// The playtest save is never written. Report: Logs/Furniture/save-and-load-<time>/.
// ---------------------------------------------------------------------------
public sealed class FurnitureSaveCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Furniture.SaveCheck";
    const string Unknown = "Rocking chair";

    protected override string Title => "Furniture: save and load a layout, an old save, a new game - play check";
    protected override string Tag => "[Furniture save]";

    DateTime playtestBefore;
    int warnings;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Furniture save] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Furniture save check (this Play session only)");
        var check = go.AddComponent<FurnitureSaveCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Furniture", $"save-and-load-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // One piece as the layout holds it (on pieces only: one that's off isn't in the save either).
    sealed class Pose
    {
        public string id, kind, variant;
        public Vector3 at;
        public float yaw;
        public int tier;
    }

    protected override IEnumerator Run()
    {
        playtestBefore = File.Exists(PlaytestPath) ? File.GetLastWriteTimeUtc(PlaytestPath) : DateTime.MinValue;
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapUI.Showing && CafeLayout.Instance != null
                                 && SaveManager.Instance != null && ShopEconomy.Instance != null, 20f,
            "the lab opened on a closed day with its recap on screen");
        if (!lastWait) yield break;
        yield return null;
        CafeLayout layout = CafeLayout.Instance;
        SaveManager saves = SaveManager.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        FurnitureCatalogue kinds = layout.Catalogue;
        FurnitureDefinition bistro = kinds != null ? kinds.Find("Bistro table") : null;
        FurnitureDefinition square = kinds != null ? kinds.Find("Square table") : null;
        FurnitureDefinition plant = kinds != null ? kinds.Find("Plant") : null;
        Check(bistro != null && square != null && plant != null, "the catalogue has the bistro, the square table and the plant (Furniture 1 and 2)");
        if (bistro == null || square == null || plant == null) yield break;

        // ---------- 1. an old save ----------
        Dictionary<string, Pose> scene = Poses(layout);
        FurnitureRules.Result whole = FurnitureRules.CheckWholeLayout(layout.Map, On(layout));
        int lying = layout.Sheets.Count(s => s != null && s.Lying);
        int bought = layout.Pieces.Count(p => p != null && !p.FromScene);
        Check(layout.AppliedFrom == CafeLayout.Source.OldSave && layout.Seats == 28 && scene.Count == 17 && bought == 0 && lying == 0,
              $"1. an old save (no layout in it): {layout.AppliedFrom} (OldSave), {layout.Seats} seats (28), {scene.Count} of the café's own pieces on (17), " +
              $"{bought} bought, {lying} dust sheets lying (0)");
        Check(whole.Fits && whole.reached == whole.places && whole.served == whole.servedOf,
              $"...both guards pass: {whole} ({whole.reached} of {whole.places} reached, {whole.served} of {whole.servedOf} served; the twin: 38 and 35)");
        bool saved = saves.TrySaveRecap();
        SaveData written = ReadLabSave();
        bool seventeen = written != null && written.cafe != null && written.cafe.format == CafeLayoutData.CurrentFormat && written.cafe.pieces != null
                         && written.cafe.pieces.Length == 17 && written.cafe.pieces.All(d => d != null && d.id.StartsWith("scene:", StringComparison.Ordinal));
        Check(saved && seventeen, $"...saved: format {(written != null && written.cafe != null ? written.cafe.format : -1)} (1), listing " +
                                  $"{(written != null && written.cafe != null && written.cafe.pieces != null ? written.cafe.pieces.Length : 0)} of the café's own pieces (17)");
        if (written == null) yield break;
        layout.Apply(written, true);
        yield return Frames(2);
        string same = Differences(scene, Poses(layout));
        Check(same.Length == 0 && layout.Seats == 28 && layout.AppliedFrom == CafeLayout.Source.SavedLayout,
              $"...loaded again: the same 17 at the same poses (1 mm, 0.1°), {layout.Seats} seats (28)" + same);

        // ---------- 2. a layout ----------
        int money = till.Money;
        PlacedPiece lounge = layout.Find("scene:Lounge corner, by the counter");
        PlacedPiece communal = layout.Find("scene:Communal table, west of the aisle");
        PlacedPiece south = layout.Find("scene:Bistro table, south");
        bool soldBoth = lounge != null && communal != null && south != null && layout.Sell(lounge) && layout.Sell(communal);
        // 26 Oct 2026 (Mac run fix): (3.00, 11.25), not 11.50. FurnitureRules reads the front counter's MeshCollider bounds, and
        // its mesh has a foot rail on the customers' side to z 13.16 (the stone's edge, which the twin had, is at 13.30): at
        // 11.50 the north chair's stand point (z 12.84) was 0.32 m from it, under the 0.40 m way round a seat (NoRoomToSit on
        // his Mac). The twin, with the rail and a43's repair-bar stools 10 cm back, finds 11.25 first; the other buys, the
        // till, the 22 seats and 32/29 are as before.
        PlacedPiece b = layout.Buy(bistro, new Vector3(3f, 0f, 11.25f), 90f);
        string sb = Said(b, layout.LastResult);
        PlacedPiece s2 = layout.Buy(square, new Vector3(-2.5f, 0f, 10.25f), 90f);
        string ss = Said(s2, layout.LastResult);
        PlacedPiece m = layout.Buy(plant, new Vector3(4f, 0f, 12.5f), 0f, "Monstera");
        string sm = Said(m, layout.LastResult);
        bool movedSouth = south != null && layout.Move(south, new Vector3(-2f, 0f, 7f), 90f);
        string smv = movedSouth ? "fits" : layout.LastResult.ToString();
        Check(soldBoth && b != null && s2 != null && m != null && movedSouth,
              $"2. the lounge by the counter and the communal table sold ({(soldBoth ? "yes" : "NO")}); bought a bistro at (3.00, 11.25) turned: {sb}; " +
              $"a square table at (-2.50, 10.25) turned: {ss}; a monstera at (4.00, 12.50): {sm}; the south bistro moved to (-2.00, 7.00) turned: {smv}");
        if (!soldBoth) yield break;
        int expect = money + lounge.Kind.SellBack + communal.Kind.SellBack - bistro.price - square.price - plant.price;
        Check(till.Money == expect && layout.Seats == 22, $"...the till ${money} -> ${till.Money} (${expect}), seats {layout.Seats} (22)");
        whole = FurnitureRules.CheckWholeLayout(layout.Map, On(layout));
        Check(whole.Fits && whole.reached == whole.places && whole.served == whole.servedOf,
              $"...both guards pass: {whole} ({whole.reached} of {whole.places} reached, {whole.served} of {whole.servedOf} served; the twin: 32 and 29)");
        Dictionary<string, Pose> mine = Poses(layout);
        int moneyThen = till.Money, seatsThen = layout.Seats, nextThen = layout.NextId;

        // ---------- 3. saved, then loaded ----------
        saved = saves.TrySaveRecap();
        SaveData file = ReadLabSave();
        Check(saved && file != null && file.cafe != null && file.cafe.format == CafeLayoutData.CurrentFormat, "3. saved to the lab save");
        if (file == null) yield break;
        layout.Apply(file, true);
        yield return Frames(2);
        string diff = Differences(mine, Poses(layout));
        Check(diff.Length == 0, "...loaded from it: every piece's id, kind, pivot (1 mm), turn (0.1°), tier and variant as before" + diff);
        lounge = layout.Find("scene:Lounge corner, by the counter");
        communal = layout.Find("scene:Communal table, west of the aisle");
        int ids = layout.Pieces.Count(p => p != null), distinct = layout.Pieces.Where(p => p != null).Select(p => p.Id).Distinct().Count();
        Check(lounge != null && !lounge.On && communal != null && !communal.On && file.money == moneyThen && till.Money == moneyThen
              && layout.Seats == seatsThen && layout.NextId == nextThen && ids == distinct,
              $"...the lounge and the communal table off; the money ${file.money} on disk, ${till.Money} in the till (${moneyThen}); seats {layout.Seats} ({seatsThen}); " +
              $"the next piece p{layout.NextId} (p{nextThen}); {ids} pieces, {distinct} ids (no piece twice)");
        whole = FurnitureRules.CheckWholeLayout(layout.Map, On(layout));
        Check(whole.Fits, $"...both guards pass: {whole} ({whole.reached} of {whole.places}, {whole.served} of {whole.servedOf})");

        // ---------- 3b. Day 1's free table in the save (26 Oct 2026, Days and first table) ----------
        PlacedPiece paidBistro = layout.Pieces.FirstOrDefault(p => p != null && !p.FromScene && p.Kind == bistro);
        Check(paidBistro != null && !layout.WasFree(paidBistro) && layout.SellBackOf(paidBistro) == bistro.SellBack,
              $"3b. the bistro bought at full price sells back for ${(paidBistro != null ? layout.SellBackOf(paidBistro) : -1)} (${bistro.SellBack}, half)");
        SaveData marked = ReadLabSave();
        PlacedPieceData mark = marked != null && marked.cafe != null && marked.cafe.pieces != null && paidBistro != null
            ? marked.cafe.pieces.FirstOrDefault(d => d != null && d.id == paidBistro.Id) : null;
        if (mark != null)
        {
            mark.free = true;
            marked.ValidateAndMigrate();
            layout.Apply(marked, true);
            yield return Frames(2);
            PlacedPiece freeOne = layout.Find(mark.id);
            CafeLayoutData snap = JsonUtility.FromJson<CafeLayoutData>(JsonUtility.ToJson(layout.Snapshot()));
            int marks = snap != null && snap.pieces != null ? snap.pieces.Count(d => d != null && d.free) : -1;
            bool keptMark = snap != null && snap.pieces != null && snap.pieces.Any(d => d != null && d.id == mark.id && d.free);   // (named apart from 5's "kept": CS0136)
            PlacedPiece otherSquare = layout.Pieces.FirstOrDefault(p => p != null && !p.FromScene && p.Kind == square);
            Check(freeOne != null && layout.WasFree(freeOne) && layout.SellBackOf(freeOne) == 0 && keptMark && marks == 1
                  && otherSquare != null && layout.SellBackOf(otherSquare) == square.SellBack,
                  $"...marked free in the save, it loads free: sells back for ${(freeOne != null ? layout.SellBackOf(freeOne) : -1)} ($0, never ${bistro.SellBack}); " +
                  $"the square still ${(otherSquare != null ? layout.SellBackOf(otherSquare) : -1)} (${square.SellBack}); the snapshot keeps the mark on it ({(keptMark ? "yes" : "NO")}) and on {marks} piece(s) (1)");
            layout.Apply(file, true);   // the layout as saved (no mark): what 5 and 6 compare against
            yield return Frames(2);
            PlacedPiece back = layout.Find(mark.id);
            Check(back != null && !layout.WasFree(back) && layout.SellBackOf(back) == bistro.SellBack,
                  $"...an older save (no mark) is as it was: the same bistro sells back for ${(back != null ? layout.SellBackOf(back) : -1)} (${bistro.SellBack})");
        }
        else Check(false, "3b. the lab save lists the bought bistro (to mark it free)");

        // ---------- 4. a new game ----------
        layout.Apply(null, false);
        yield return Frames(2);
        List<PlacedPiece> open = On(layout);
        lying = layout.Sheets.Count(s => s != null && s.Lying);
        bought = layout.Pieces.Count(p => p != null && !p.FromScene);
        bool theOpenSet = open.Count == layout.Opening.Count && layout.Opening.All(p => p != null && p.On);
        Check(layout.AppliedFrom == CafeLayout.Source.NewGame && layout.Seats == 10 && open.Count == 9 && theOpenSet && lying == 5 && bought == 0,
              $"4. a new game: {layout.AppliedFrom} (NewGame), {layout.Seats} seats (10: his answer r1), {open.Count} of the café's own pieces on (9, the new game's own: " +
              $"{(theOpenSet ? "yes" : "NO")}), {lying} dust sheets lying (5), {bought} bought");
        Note("the new game's pieces: " + string.Join("; ", open.Select(p => p.name)));
        FurnitureRules.Result half = FurnitureRules.CheckWholeLayout(layout.Map, open);
        Check(half.Fits && half.reached == half.places && half.served == half.servedOf,
              $"...its guards pass with the sheets' boxes solid: {half} ({half.reached} of {half.places} reached, {half.served} of {half.servedOf} served; the twin: 20 and 17)");
        Note("Day 1's two settled guests (his answer r3) are P2's numbers: not judged here.");
        yield return Photo("01-new-game");

        // ---------- 5. an unknown kind ----------
        SaveData odd = ReadLabSave();
        if (odd != null && odd.cafe != null)
        {
            var list = new List<PlacedPieceData>(odd.cafe.pieces ?? new PlacedPieceData[0])
            {
                new PlacedPieceData { id = "p77", kind = Unknown, x = 0f, z = 5f, yaw = 0f },
            };
            odd.cafe.pieces = list.ToArray();
            warnings = 0;
            Application.logMessageReceived += CountWarnings;
            try { layout.Apply(odd, true); }
            finally { Application.logMessageReceived -= CountWarnings; }
            yield return Frames(2);
            string rest = Differences(mine, Poses(layout));
            Check(warnings == 1 && layout.Find("p77") == null && rest.Length == 0,
                  $"5. a save naming an unknown kind ('{Unknown}'): skipped with {warnings} warning(s) (1); everything else loaded as before" + rest);
        }
        else Check(false, "5. the lab save could be read back for the unknown kind");

        // ---------- 6. tomorrow ----------
        layout.Apply(file, true);
        yield return Frames(2);
        RecapUI recap = FindAnyObjectByType<RecapUI>();
        int day = DayClock.Instance.Day;
        bool opened = recap != null && recap.ContinueAfterNight();
        yield return Until(() => DayClock.Instance.Day == day + 1 && DayClock.Instance.IsOpen, 10f,
            $"6. tomorrow opened with the night skipped: Day {day + 1} ({(opened ? "the recap's own way" : "it refused")})");
        SaveData morning = ReadLabSave();
        string kept = morning != null && morning.cafe != null ? Differences(mine, Poses(morning.cafe)) : " (NO SAVE READ)";
        Check(morning != null && morning.day == day + 1 && kept.Length == 0,
              $"...the morning's save (Day {(morning != null ? morning.day : -1)}) holds the same layout" + kept);

        Check((File.Exists(PlaytestPath) ? File.GetLastWriteTimeUtc(PlaytestPath) : DateTime.MinValue) == playtestBefore,
              $"the playtest save ({FurnitureCheck.PlaytestSaveName}) was never written");
    }

    void CountWarnings(string message, string stack, LogType type)
    {
        if (type == LogType.Warning && message != null && message.Contains("unknown furniture kind")) warnings++;
    }

    // ================================================================== helpers

    static string PlaytestPath => Path.Combine(Application.persistentDataPath, FurnitureCheck.PlaytestSaveName);

    static List<PlacedPiece> On(CafeLayout layout) => layout.Pieces.Where(p => p != null && p.On).ToList();

    static Dictionary<string, Pose> Poses(CafeLayout layout)
    {
        var all = new Dictionary<string, Pose>();
        foreach (PlacedPiece p in layout.Pieces)
            if (p != null && p.On)
                all[p.Id] = new Pose { id = p.Id, kind = p.Kind != null ? p.Kind.name : "", variant = p.Variant, at = p.Pivot, yaw = p.Yaw, tier = p.Tier };
        return all;
    }

    static Dictionary<string, Pose> Poses(CafeLayoutData data)
    {
        var all = new Dictionary<string, Pose>();
        foreach (PlacedPieceData d in data.pieces ?? new PlacedPieceData[0])
            if (d != null)
                all[d.id] = new Pose { id = d.id, kind = d.kind, variant = d.variant ?? "", at = new Vector3(d.x, 0f, d.z), yaw = d.yaw, tier = d.tier };
        return all;
    }

    // "" when the two hold the same pieces the same way; else the first few differences, each with what and where.
    static string Differences(Dictionary<string, Pose> was, Dictionary<string, Pose> now)
    {
        var said = new List<string>();
        foreach (Pose a in was.Values)
        {
            if (!now.TryGetValue(a.id, out Pose b)) { said.Add($"{a.id} missing"); continue; }
            float metres = new Vector2(a.at.x - b.at.x, a.at.z - b.at.z).magnitude;
            float turn = Mathf.Abs(Mathf.DeltaAngle(a.yaw, b.yaw));
            if (a.kind != b.kind) said.Add($"{a.id}'s kind {a.kind} -> {b.kind}");
            if (metres > .001f) said.Add($"{a.id} moved {metres * 1000f:0.0} mm");
            if (turn > .1f) said.Add($"{a.id} turned {turn:0.00}°");
            if (a.tier != b.tier) said.Add($"{a.id}'s tier {a.tier} -> {b.tier}");
            if ((a.variant ?? "") != (b.variant ?? "")) said.Add($"{a.id}'s variant {a.variant} -> {b.variant}");
        }
        foreach (string id in now.Keys) if (!was.ContainsKey(id)) said.Add($"{id} extra");
        return said.Count == 0 ? "" : " (" + string.Join("; ", said.Take(6)) + (said.Count > 6 ? $"; {said.Count - 6} more" : "") + ")";
    }

    static string Said(PlacedPiece made, FurnitureRules.Result result) => made != null ? $"bought ({made.Id})" : result.ToString();

    static SaveData ReadLabSave()
    {
        try { return SaveCheckpointStorage.Read(Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName)); }
        catch (Exception) { return null; }
    }
}
