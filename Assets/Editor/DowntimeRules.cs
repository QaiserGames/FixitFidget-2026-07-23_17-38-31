#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// FIXIT FIDGET > CHECKS > DOWNTIME RULES (24 Oct 2026; Designer P's spec §6.7, the pure half of Part 7)
//
// The rules of the four things to do while waiting and the arrivals around them, checked without Play, a scene, a save or an
// asset change (each fixture is made in memory and thrown away):
//   * the Now line's order (a refill, a dirty table, a guest with gossip, the find) and its words: each line within
//     WordBudget.NowLine (10), the button as its icon, never "[E]" or "Press"; Day 1's find "clean it while you wait" (his p1);
//   * the cap's retry only with one customer step or fewer; the quiet nudge never in the opening, before the featured visit
//     is over, or after last orders (P's §3.1);
//   * the gossip: ids, budgets (the line 10 words, the fact 7 so "(likely)" fits the page's 8), filed under who it's about
//     (Grace, the Officer, the man at the bins: never "someone"), never mistaken for a thing of the night, and the days' ids;
//   * a find never takes a customer's place on the bench, and a crate is never set down there (ItemSlotArea);
//   * restock by hand: the machine's 12 and the back shelf, a refill, the old save's split, an order never refused while the
//     shelf has stock (ShopInventory.CanMake);
//   * the thrift find's save record (additive) and its sale shares.
// One line at the end: "[Downtime rules] All N checks passed." or "[Downtime rules] N of M checks FAILED."
// ---------------------------------------------------------------------------
public static class DowntimeRules
{
    const string Tag = "[Downtime rules]";

    [MenuItem("Fixit Fidget/Checks/Downtime rules")]
    public static void Run()
    {
        int checks = 0, failed = 0;
        var lines = new List<string>();
        void Check(bool ok, string what)
        {
            checks++;
            if (!ok) { failed++; Debug.LogError($"{Tag} FAILED: {what}"); }
            lines.Add((ok ? "pass  " : "FAIL  ") + what);
        }

        try { Order(Check); } catch (Exception e) { Check(false, "the Now line's order and words threw: " + e.Message); }
        try { Arrivals(Check); } catch (Exception e) { Check(false, "the arrival rules threw: " + e.Message); }
        try { Gossip(Check); } catch (Exception e) { Check(false, "the gossip rules threw: " + e.Message); }
        try { BenchCorner(Check); } catch (Exception e) { Check(false, "the bench's corner threw: " + e.Message); }
        try { Stock(Check); } catch (Exception e) { Check(false, "restock by hand threw: " + e.Message); }
        try { Thrift(Check); } catch (Exception e) { Check(false, "the thrift record threw: " + e.Message); }

        Debug.Log($"{Tag} The rules:\n" + string.Join("\n", lines));
        Debug.Log(failed == 0 ? $"{Tag} All {checks} checks passed." : $"{Tag} {failed} of {checks} checks FAILED.");
    }

    // ---------- the Now line: the order and the words ----------

    static void Order(Action<bool, string> Check)
    {
        Check((int)Downtime.Job.None == 0 && (int)Downtime.Job.Refill < (int)Downtime.Job.Table
              && (int)Downtime.Job.Table < (int)Downtime.Job.Gossip && (int)Downtime.Job.Gossip < (int)Downtime.Job.Find,
            "the waiting jobs' order is P's: a refill, a dirty table, a guest with gossip, the find");

        var words = new List<(string what, string line)>
        {
            ("refill (cups)", DayOneGuideUI.RefillWords(true, false)),
            ("refill (beans)", DayOneGuideUI.RefillWords(false, true)),
            ("refill (both)", DayOneGuideUI.RefillWords(true, true)),
            ("the crate in hand", DayOneGuideUI.CarryCrateWords),
            ("no cups at the machine", DayOneGuideUI.NoCupsLeftWords),
            ("no beans at the machine", DayOneGuideUI.NoBeansLeftWords),
        };
        var tags = new[] { SeatTag.None, SeatTag.Window, SeatTag.Lounge, SeatTag.Bar, SeatTag.Communal, SeatTag.Quiet, SeatTag.NearDoor,
                           SeatTag.Window | SeatTag.Lounge };
        foreach (SeatTag t in tags)
        {
            words.Add(("a table " + t, DayOneGuideUI.TableWords(t)));
            words.Add(("a guest " + t, DayOneGuideUI.GossipWords(t)));
        }
        foreach (string thing in new[] { "watch", "phone", "camera" })
        {
            words.Add(("Day 1's find, a " + thing, DayOneGuideUI.FindWords(1, thing)));
            words.Add(("a later find, a " + thing, DayOneGuideUI.FindWords(2, thing)));
        }
        foreach ((string what, string line) in words)
        {
            Check(WordBudget.Words(line) > 0 && !WordBudget.Over(line, WordBudget.NowLine),
                $"the Now line's {what}: \"{line}\" is {WordBudget.Report(line, WordBudget.NowLine)}");
            Check(!line.Contains("[") && !line.Contains("Press "), $"the Now line's {what} names no button in words (icons only)");
        }
        foreach (SeatTag t in tags)
            Check(DayOneGuideUI.GossipWords(t).Contains(ControlHints.Interact), $"the chat line ({t}) shows the interact button as its icon");
        Check(DayOneGuideUI.FindWords(1, "watch") == "Your thrift watch: clean it while you wait.",
            "his p1: Day 1's find is a clean, \"Your thrift watch: clean it while you wait.\"");
        Check(WordBudget.Words(DayOneGuideUI.OnTheWayWords("Valentina")) == 4 && WordBudget.Words(DayOneGuideUI.OnHoldLead) == 2,
            "the leads are short: \"Valentina's on the way.\" (4 words), \"On hold.\" (2); a line keeps its lead only when both fit 10");
        Check(DayOneGuideUI.QuietTitle == DayOneGuideUI.QuietTitle.ToUpperInvariant() && WordBudget.Words(DayOneGuideUI.QuietTitle) <= 4,
            $"the first time each shows, the card's title is \"{DayOneGuideUI.QuietTitle}\"");

        // The place words (S's seat tags, P's §2.1).
        Check(SeatChoice.PlaceWords(SeatTag.Window) == "by the window" && SeatChoice.PlaceWords(SeatTag.Lounge) == "in the lounge"
              && SeatChoice.PlaceWords(SeatTag.Bar) == "at the bar" && SeatChoice.PlaceWords(SeatTag.Communal) == "at the long table"
              && SeatChoice.PlaceWords(SeatTag.Quiet) == "in the quiet corner" && SeatChoice.PlaceWords(SeatTag.NearDoor) == "by the door"
              && SeatChoice.PlaceWords(SeatTag.None) == "" && SeatChoice.PlaceWords(SeatTag.Window | SeatTag.Lounge) == "by the window",
            "the places are named as P's §2.1 has them (a window seat in the lounge is \"by the window\")");
    }

    // ---------- the arrivals (P's §3.1) ----------

    static void Arrivals(Action<bool, string> Check)
    {
        Check(CustomerSpawner.RetrySoon(0) && CustomerSpawner.RetrySoon(1) && !CustomerSpawner.RetrySoon(2) && !CustomerSpawner.RetrySoon(4),
            "the cap's retry (1.5 s, not a whole gap) only while Ace has one customer step left or none");
        Check(CustomerSpawner.NudgeAllowed(false, false, true, false), "the quiet nudge may act once the featured visit is over, before last orders");
        Check(!CustomerSpawner.NudgeAllowed(true, false, true, false) && !CustomerSpawner.NudgeAllowed(false, true, true, false),
            "never during the guided opening or its visit");
        Check(!CustomerSpawner.NudgeAllowed(false, false, false, false), "never before today's featured visit is over (Day 1 protects the first repair)");
        Check(!CustomerSpawner.NudgeAllowed(false, false, true, true), "never after last orders");
    }

    // ---------- the gossip (P's §2.4) ----------

    static void Gossip(Action<bool, string> Check)
    {
        IReadOnlyList<NotebookEntries.Gossip> table = NotebookEntries.GossipTable;
        Check(table.Count == 4 && table.Select(g => g.id).Distinct().Count() == table.Count, "four lines of gossip, each its own id");
        Check(NotebookEntries.GossipOfficerWho == OfficerStory.ProfileId && NotebookEntries.GossipLodgerWho == LodgerStory.PagesWho,
            "the gossip's people are the notebook's own (the Officer's profile, the man at the bins' pages)");
        var things = NightThings.All.Select(t => t.id).ToList();
        foreach (NotebookEntries.Gossip g in table)
        {
            Check(g.id.StartsWith("gossip.", StringComparison.Ordinal) && !things.Any(t => g.id == t || g.id.StartsWith(t + ".", StringComparison.Ordinal)),
                $"{g.id}: a gossip id, never a thing of the night's (his ask still puts the cups in the notebook)");
            Check(!WordBudget.Over(g.line, NotebookEntries.GossipLineWords) && WordBudget.Words(g.line) > 0,
                $"{g.id}: the guest's line \"{g.line}\" is {WordBudget.Report(g.line, NotebookEntries.GossipLineWords)}");
            Check(!WordBudget.Over(g.fact, NotebookEntries.GossipFactWords) && !WordBudget.Over(g.fact, WordBudget.Page),
                $"{g.id}: the fact \"{g.fact}\" is {WordBudget.Report(g.fact, NotebookEntries.GossipFactWords)} (so \"(likely)\" fits the page's line)");
            NotebookFactData fact = NotebookEntries.Overheard(g);
            string key = NoteSubjects.KeyOf(fact);
            Check(fact != null && fact.source == Notebook.Sources.Overheard && fact.sure == Notebook.Sureness.Likely,
                $"{g.id}: overheard, likely");
            Check(key == NoteSubjects.Grace || key == NoteSubjects.Officer || key == NoteSubjects.Lodger,
                $"{g.id}: filed under who it's about ({key}), never a walk-in or \"someone\"");
            Check(NotebookEntries.GossipFor(g.id) == g, $"{g.id}: found by its id");
        }
        Check(NotebookEntries.GossipFor("gossip.nobody") == null && NotebookEntries.GossipFor(null) == null, "an unknown id is no gossip");
        Check(PacingSteps.Day2Gossip.Length == 2 && PacingSteps.Day3Gossip.Length == 2
              && PacingSteps.Day2Gossip.Concat(PacingSteps.Day3Gossip).All(id => NotebookEntries.GossipFor(id) != null),
            "the days' data names two lines each on Days 2 and 3, all in the table (Day 1 none)");
        var book = new Notebook();
        NotebookFactData once = NotebookEntries.Overheard(NotebookEntries.GossipFor(NotebookEntries.GossipGraceCups));
        Check(book.Learn(once, 2) && !book.Learn(NotebookEntries.Overheard(NotebookEntries.GossipFor(NotebookEntries.GossipGraceCups)), 2)
              && book.Count == 1, "the notebook learns a line once, however often it's heard");
    }

    // ---------- the bench's corner (P's §2.2: a customer's device always has the main place) ----------

    static void BenchCorner(Action<bool, string> Check)
    {
        var made = new List<GameObject>();
        try
        {
            var benchGo = new GameObject("Downtime rules: bench"); made.Add(benchGo);
            ItemSlotArea bench = benchGo.AddComponent<ItemSlotArea>();
            Transform a = new GameObject("Slot A").transform, b = new GameObject("Slot B").transform, side = new GameObject("Slot_Side").transform;
            a.SetParent(benchGo.transform); b.SetParent(benchGo.transform); side.SetParent(benchGo.transform);
            Set(bench, "slots", new[] { a, b });
            Set(bench, "baseSlots", 2);
            Set(bench, "sideSlot", side);
            JobBase Job(string name, bool find)
            {
                var go = new GameObject("Downtime rules: " + name); made.Add(go);
                JobBase job = go.AddComponent<RepairJob>();
                if (find) go.AddComponent<SideProject>();
                return job;
            }
            JobBase first = Job("a customer's phone", false), second = Job("a customer's watch", false), third = Job("a third customer's", false);
            JobBase thrift = Job("the thrift find", true);
            Check(bench.ClaimSlot(thrift) == side && bench.SideSlot == side, "the find goes to the bench's corner");
            Check(bench.ClaimSlot(first) == a && bench.ClaimSlot(second) == b, "both customers' devices still have the bench's two main places");
            Check(bench.ClaimSlot(third) == null, "a third customer's device waits (the corner is never a customer's)");
            bench.ReleaseSlot(thrift);
            Check(bench.ClaimSlot(third) == null && bench.SideSlotFree(thrift), "the corner free, still never a customer's");
            var crateGo = new GameObject("Downtime rules: crate"); made.Add(crateGo);
            Check(bench.ClaimSlot(crateGo.AddComponent<StockCrate>()) == null, "a crate of cups and beans is never set down on the bench");
            Check(bench.Holds(first) && !bench.Holds(thrift), "the bench knows what it holds");
        }
        finally
        {
            foreach (GameObject g in made) if (g != null) UnityEngine.Object.DestroyImmediate(g);
        }
    }

    // ---------- restock by hand (P's §2.5) ----------

    static void Stock(Action<bool, string> Check)
    {
        var host = new GameObject("Downtime rules: stock");
        DrinkDefinition latte = ScriptableObject.CreateInstance<DrinkDefinition>();
        try
        {
            latte.cupsCost = 1;
            latte.beansCost = 2;
            ShopInventory stock = host.AddComponent<ShopInventory>();
            Call(stock, "Awake");
            stock.SetStock(20, 20);
            Check(stock.MachineCups == ShopInventory.MachineHolds && stock.ShelfCups == 8 && stock.Cups == 20 && stock.Beans == 20,
                "the start: 12 of each in the machine, 8 on the back shelf, 20 in all");
            int took = 0;
            while (stock.TakeCup() && took < 30) took++;
            Check(took == ShopInventory.MachineHolds && stock.MachineCups == 0 && stock.Cups == 8, "drinks take the machine's cups only: 12, then none");
            Check(stock.MachineLow && stock.RefillWanted && stock.CanRefill, "the machine at 3 or fewer with stock on the shelf: a refill is wanted");
            Check(stock.CanMake(latte), "an order is still taken: a refill brings the shelf's (never \"sorry, we're out\" while the shelf has some)");
            int refillsBefore = Downtime.Today.refills;
            Check(stock.Refill() && stock.MachineCups == 8 && stock.ShelfCups == 0 && stock.MachineBeans == ShopInventory.MachineHolds,
                "a refill fills the machine back to 12 from the shelf (8 cups was all it had)");
            Check(Downtime.Today.refills == refillsBefore + 1 && !stock.Refill(), "a refill is counted; one with nothing to bring does nothing");
            while (stock.TakeCup()) { }
            Check(!stock.CanMake(latte) && stock.Cups == 0, "the machine and the shelf both empty: now it's \"sorry, we're out\"");
            stock.SetStock(30, 30, 5, 1);
            Check(stock.MachineCups == 5 && stock.ShelfCups == 25 && stock.MachineBeans == 1 && stock.ShelfBeans == 29 && !stock.CanBrew(latte)
                  && stock.CanMake(latte), "a saved split comes back as it was (5 cups and 1 shot in the machine): a latte waits for a refill, the order is still taken");
            stock.SetStock(3, 3, -1, -1);
            Check(stock.MachineCups == 3 && stock.ShelfCups == 0, "an older save (no split): the machine is filled from the stock");
            stock.SetStock(40, 40, 30, 30);
            Check(stock.MachineCups == ShopInventory.MachineHolds && stock.ShelfCups == 28, "the machine never holds more than 12");

            var old = JsonUtility.FromJson<SaveData>("{\"version\":5,\"day\":2,\"money\":139,\"cups\":14,\"beans\":9}");
            old.ValidateAndMigrate();
            Check(old.machineCups == -1 && old.machineBeans == -1 && old.cups == 14, "an older save has no split: -1, its stock unchanged");
            var saved = new SaveData { day = 2, cups = 20, beans = 9, machineCups = 25, machineBeans = 4 };
            saved.ValidateAndMigrate();
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));
            Check(back.machineCups == 20 && back.machineBeans == 4, "the machine's share is saved, never more than the stock");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(latte);
        }
    }

    // ---------- the thrift find's record and its sale ----------

    static void Thrift(Action<bool, string> Check)
    {
        var old = JsonUtility.FromJson<SaveData>("{\"version\":5,\"day\":3,\"money\":200}");
        old.ValidateAndMigrate();
        Check(old.thrift != null && !old.thrift.known && old.thrift.fault == -1 && old.thrift.prefab == "",
            "an older save has no thrift find (the shelf gets the day's own)");
        var closed = new SaveData { day = 1, dayCompleted = true, recap = new RecapSaveData { day = 1 } };
        closed.thrift = new ThriftSaveData { known = true, prefab = "PhoneRepair", fault = 0, forDay = 2, sale = 75 };
        Check(closed.TryCreateNextDay(out SaveData next) && next.thrift != closed.thrift && next.thrift.prefab == "PhoneRepair"
              && next.thrift.forDay == 2 && next.thrift.sale == 75, "Open Tomorrow carries the find forward in a copy of its own");
        var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(closed));
        round.ValidateAndMigrate();
        Check(round.thrift.known && round.thrift.fault == 0 && round.thrift.forDay == 2, "the find survives Unity's JSON");
        Check(Mathf.Abs(SideProject.SaleShare(JobGrade.Perfect) - 1f) < .001f && Mathf.Abs(SideProject.SaleShare(JobGrade.Good) - .8f) < .001f
              && SideProject.SaleShare(JobGrade.Rejected) <= 0f, "a find sells for its price when Perfect, 80 % when Good, nothing when rejected");
        Check(ThriftShelf.SellsFrom == JobGrade.Good, "it sells at closing at Good or better; less waits for tomorrow");
        Check(WordBudget.Words(SideProject.FinishedWords) <= SideProject.CardWords, $"the card's finished line: \"{SideProject.FinishedWords}\"");
        foreach (string label in new[] { SideProject.ThriftWatchFault, "Cracked Screen", "Gunked Up, Won't Charge", "Frozen Boot Sequence - Reconnect Signal",
                                         "Debris Inside, Won't Tell Time", "Seized Movement" })
        {
            string card = "Your thrift watch: " + SideProject.WordsFor(label).card + ".";
            Check(WordBudget.Words(card) <= SideProject.CardWords && card.Length <= SideProject.CardCharacters,
                $"the bench card's line for {label}: \"{card}\" ({WordBudget.Words(card)} words, {card.Length} characters)");
        }
    }

    // ---------- helpers ----------

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, Any);
        if (f == null) throw new MissingFieldException(target.GetType().Name, field);
        f.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        MethodInfo m = target.GetType().GetMethod(method, Any);
        if (m == null) throw new MissingMethodException(target.GetType().Name, method);
        m.Invoke(target, null);
    }
}
#endif
