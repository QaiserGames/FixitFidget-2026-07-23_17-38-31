using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE FOUR THINGS TO DO WHILE WAITING, ONE LAB EACH (24 Oct 2026; Designer P's spec §2.2-§2.5 and §6.7, approved by him with
// his answers of 24 Oct). Fixit Fidget > Days > Lab - ... (WaitingJobsSteps) starts a lab session on the lab's own save (never
// his playtest save), the autopilot off: the lab is the player's hands, through the game's own calls.
//   * THE FIND GIVES WAY (Day 1; his p6): E on the thrift shelf flies the find to the bench's corner and opens the close-up; it
//     is half done (a spot cleaned, a screw out when it has one) when the day's first customer reaches the counter. Passes
//     when the close-up closes by itself within 1.5 s, the find's pieces stay where they were, the customer's device gets the
//     bench's main place (never the corner), and the find is finished after and goes back on its shelf.
//   * CLEAR THE TABLES (Day 3, a day with no guided visit, so the Now line's first quiet moment is at the open): a guest's used
//     cup on a table takes its seat until Ace clears it (the badge over it), the Now line offers it in a quiet moment ("Clear
//     the window table.", its pin over the table, nobody needing Ace), and one press clears it (the seat is free again).
//   * CHAT WITH A SEATED GUEST (Day 2): once the day's first customer is heard, a seated guest has a "…"; one press, they say
//     their line and the notebook learns its fact under who it's about; no conversation, nothing held. It asks for Day 2's
//     settled guests at the open (PatronSpawner.ForceOpeningGuests, put back after), as the real game seats them (26 Oct 2026).
//   * RESTOCK BY HAND (Day 2): the machine at 3, the back shelf's crate in one hand ("Take cups and beans"), put back with one
//     press, then "Refill the machine" at the cup stack fills it to 12 of each; two full hands get "Hands full"; an order is
//     never refused while the shelf has stock.
// Report and photos: Logs/Days/waiting-<mode>-<time>/. The last console line: "[Waiting jobs] All N checks passed." or
// "[Waiting jobs] N of M checks FAILED. <folder>".
// ---------------------------------------------------------------------------
public sealed class WaitingJobsCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Days.WaitingJobs.Check";
    public const string ModeKey = "FixitFidget.Days.WaitingJobs.Mode";
    public enum Mode { Find, Table, Chat, Refill }
    public Mode mode = Mode.Find;

    protected override string Title => $"The things to do while waiting: {Words(mode)} - play check";
    protected override string Tag => "[Waiting jobs]";

    public static string Words(Mode m) => m switch
    {
        Mode.Find => "the find gives way",
        Mode.Table => "clear the tables",
        Mode.Chat => "chat with a seated guest",
        _ => "restock by hand",
    };

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        int asked = PlayerPrefs.GetInt(ModeKey, 0);
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(ModeKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Waiting jobs] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Waiting jobs check (this Play session only)");
        var check = go.AddComponent<WaitingJobsCheck>();
        check.mode = Enum.IsDefined(typeof(Mode), asked) ? (Mode)asked : Mode.Find;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Days",
            $"waiting-{check.mode.ToString().ToLowerInvariant()}-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    PlayerCarry carry;
    PlayerInteractor player;
    DayOneGuideUI guide;
    bool forcedGuests, forcedGuestsWas;   // 26 Oct 2026 (Mac run fix): the chat lab's ask for settled guests, put back after

    // 26 Oct 2026 (Mac run fix): the settled guests' switch is static, so it goes back to what it was when the check ends.
    protected override void Restore()
    {
        if (forcedGuests) PatronSpawner.ForceOpeningGuests = forcedGuestsWas;
        forcedGuests = false;
    }

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver
                                 && PlayerCarry.Current != null && CafeLabDirector.Instance != null, 30f, "the lab's day is open");
        if (!lastWait) yield break;
        carry = PlayerCarry.Current;
        player = carry.GetComponent<PlayerInteractor>();
        guide = FindAnyObjectByType<DayOneGuideUI>();
        Check(player != null, "Ace is in the café");
        Check(!CafeLabDirector.Instance.Autopilot, "the autopilot is off: the lab is the player's hands");
        Note($"Day {DayClock.Instance.Day}: {Words(mode)}.");
        switch (mode)
        {
            case Mode.Find: yield return FindGivesWay(); break;
            case Mode.Table: yield return ClearTheTables(); break;
            case Mode.Chat: yield return ChatWithAGuest(); break;
            default: yield return RestockByHand(); break;
        }
    }

    // ---------- the find gives way (his p6) ----------

    IEnumerator FindGivesWay()
    {
        ThriftShelf shelf = null;
        yield return Until(() => (shelf = ThriftShelf.Instance) != null && shelf.Find != null && shelf.OnTheShelf, 10f,
            "today's thrift find is on its shelf by the bench (Fixit Fidget > Days > Props 1 and Thrift 1)");
        if (!lastWait) yield break;
        JobBase find = shelf.Find;
        ItemSlotArea bench = FindObjectsByType<ItemSlotArea>(FindObjectsInactive.Exclude).FirstOrDefault(a => a.SideSlot != null);
        Check(bench != null, "the bench has its corner for the find (the side slot)");
        if (bench == null) yield break;
        CafeLabDirector.Instance.MoveAce(bench.transform.position + bench.transform.forward * .9f, bench.transform.eulerAngles.y + 180f);
        yield return Seconds(.3f);
        Check(shelf.IsAvailable && shelf.Prompt == "Work on your thrift " + shelf.Thing, $"E on the shelf: \"{shelf.Prompt}\"");
        shelf.Interact(player);
        yield return Until(() => shelf.InTheCloseUp, 3f, "the find flies to the bench's corner and the close-up opens");
        // 26 Oct 2026 (integration): why not, when it doesn't (his Mac's afternoon run missed it once; the morning's passed).
        if (!lastWait)
        {
            ItemInspector whyInspector = ItemInspector.Instance;   // (named apart from the method's own "item" below: CS0136)
            ItemInteractable whyItem = find != null ? find.GetComponentInChildren<ItemInteractable>() : null;
            Note($"...why not: on the bench's corner {shelf.OnTheCorner}; its item on the bench {(whyItem != null && whyItem.OnBench)}; " +
                 $"the close-up holds {(whyInspector == null ? "(no inspector)" : whyInspector.FocusedItem != null ? whyInspector.FocusedItem.name : "nothing")}; " +
                 $"time scale {Time.timeScale:0.##}; the day over {(DayClock.Instance != null && DayClock.Instance.DayOver)}");
        }
        if (!lastWait) yield break;
        Check(bench.Holds(find) && shelf.OnTheCorner, "it sits on the bench's own corner, outside the customers' places");

        // Half done: a spot cleaned, and a screw out when it has one (its pieces must stay in their cups).
        GrimeSpot spot = find.GetComponentsInChildren<GrimeSpot>().FirstOrDefault();
        if (spot != null) spot.Scrub(1e6f);
        ScrewTarget screw = find.GetComponentsInChildren<ScrewTarget>().FirstOrDefault();
        if (screw != null) screw.Activate();
        yield return Seconds(1.5f);
        var pieces = find.DetachedParts.Where(p => p != null).Select(p => (part: p, at: p.transform.position)).ToList();
        Check(!find.IsFinished && (spot != null || screw != null), $"the find is half done ({(spot != null ? "a spot cleaned" : "")}" +
              $"{(screw != null ? (spot != null ? ", " : "") + "a screw out" : "")}; {pieces.Count} piece(s) off it)");
        yield return Photo("01-the-find-half-done");

        // The day's first customer reaches the counter: the close-up closes by itself within 1.5 s.
        yield return Until(() => Downtime.AtTheCounter != null, 45f, "the day's first customer reaches the counter");
        if (!lastWait) yield break;
        float reached = Time.time;
        CustomerBrain customer = Downtime.AtTheCounter;
        yield return Until(() => !shelf.InTheCloseUp, 3f, "the close-up closes by itself");
        float took = Time.time - reached;
        Check(took <= 1.5f, $"the close-up closed {took:0.00} s after {customer.CustomerName} reached the counter (within 1.5 s)");
        Check(shelf.GaveWayToday == 1, $"it gave way once ({shelf.GaveWayToday})");
        Check(pieces.All(p => p.part != null && (p.part.transform.position - p.at).magnitude < .002f) && find.DetachedParts.Count(p => p != null) == pieces.Count,
            $"the find's {pieces.Count} piece(s) stay where they were, in their cups");
        Check(shelf.OnTheCorner && !find.IsFinished, "the find waits on its corner as it was");
        yield return Photo("02-the-find-gives-way");

        // The customer's device: the bench's main place, never the corner.
        customer.HearIntake();
        if (customer.CanAcceptJob) customer.AcceptJob();
        yield return Until(() => customer.ActiveJob != null, 6f, $"{customer.CustomerName}'s device is taken in");
        JobBase theirs = customer.ActiveJob;
        if (theirs != null)
        {
            Transform place = bench.ClaimSlot(theirs);
            Check(place != null && place != bench.SideSlot, $"{customer.CustomerName}'s device gets one of the bench's main places, not the find's corner");
            bench.ReleaseSlot(theirs);
        }

        // The find is finished after, and goes back on its shelf.
        ItemInteractable item = find.GetComponentInChildren<ItemInteractable>();
        Check(item != null && player.WorkOn(item), "the find opens again from its corner");
        yield return Until(() => shelf.InTheCloseUp, 3f, "back in the close-up");
        if (screw != null) screw.Activate();   // the screw back in
        yield return Seconds(1f);
        CafeLabDirector.FinishByHand(find);
        yield return Until(() => find != null && find.IsFinished, 6f, "the find is finished (the labs' hand)");
        if (ItemInspector.Instance != null) ItemInspector.Instance.CancelInspection();
        yield return Until(() => shelf.Finished && shelf.OnTheShelf, 4f, "fixed, it goes back on the shelf (with its tick): it sells tonight");
        Check(Downtime.Today.findsFinished == 1, $"one find finished today ({Downtime.Today.findsFinished})");
        yield return Photo("03-the-find-back-on-its-shelf");
    }

    // ---------- clear the tables ----------

    IEnumerator ClearTheTables()
    {
        TableSeat seat = null;
        DirtyCup cup = null;
        foreach (TableSeat s in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (s == null || s.RepairBar || !s.IsAvailable) continue;
            cup = DirtyCup.Place(s, guests: true);
            if (cup != null) { seat = s; break; }
        }
        Check(cup != null && seat != null, $"a guest's used cup is left on a table ({(seat != null ? SeatTags.ZoneOf(seat) : "no table took one")})");
        if (cup == null) yield break;
        yield return Seconds(.5f);
        Check(cup.OnTheTable && !seat.IsAvailable, "its seat is taken by the cup until Ace clears it (nobody sits at a dirty table)");
        Check(cup.table != null && cup.table.Badge != 0 && Juice.MarkShowing(cup.table.Badge, out _), "the mess badge is over the table");
        Check(cup.IsAvailable && cup.Prompt == LodgerStory.ClearPrompt, $"E on it: \"{cup.Prompt}\"");

        // A quiet moment: at the open, before the day's first customer is inside, the Now line offers the table.
        Transform stand = player.CounterStation != null ? player.CounterStation.StandPoint : null;
        if (stand != null) CafeLabDirector.Instance.MoveAce(stand.position, stand.eulerAngles.y);
        string expect = DayOneGuideUI.TableWords(SeatTags.Of(seat));
        yield return Until(() => DayOneGuideUI.ShowingWaitingJob == Downtime.Job.Table, 30f,
            $"in a quiet moment the Now line offers the table (\"{expect}\")");
        if (lastWait && guide != null)
        {
            string line = guide.CardTitle.Length > 0 ? guide.CardText : guide.NowText;
            Check(line.Contains(expect) && !WordBudget.Over(guide.NowText.Length > 0 ? guide.NowText : expect, WordBudget.NowLine),
                $"its words: \"{NoteSubjects.Plain(line)}\"");
            Check(guide.PinLabel == DayOneGuideUI.TableName, $"its pin over the table: \"{guide.PinLabel}\"");
            // 26 Oct 2026 (Icons on notes): the table chip before the words, and the same chip on its pin.
            if (PadInput.IconsAvailable)
            {
                Check(guide.NowChip == NoteIcons.Table && guide.CardText.Contains(NoteIcons.Tag(NoteIcons.Table) + " <b>")
                      && NoteIcons.Tag(NoteIcons.Table).Length > 0, $"...with the table chip before it (drawn: \"{guide.NowChip}\")");
                UiPins pins = UiPins.Instance;
                if (pins != null && pins.GuideShowing) Check(pins.GuideIcon == NoteIcons.Table && pins.GuideText == DayOneGuideUI.TableName,
                    $"...and its pin wears the same chip (\"{pins.GuideIcon}\" {pins.GuideText})");
                else Note("the guide pin isn't up (not overhead?): its chip unchecked here");
            }
            Check(Downtime.NobodyNeedsAce, "nobody needed Ace while it showed");
            yield return Photo("01-the-table-on-the-now-line");
        }

        int before = Downtime.Today.tablesCleared;
        cup.Interact(player);
        yield return Seconds(.5f);
        Check(seat.IsAvailable, "cleared: the seat is free again");
        Check(Downtime.Today.tablesCleared == before + 1, $"one table cleared by Ace ({Downtime.Today.tablesCleared})");
        Check(MessTable.Open.Count == 0 || MessTable.Open.All(t => t.Cleared), "no table left with cups on it");
        yield return Until(() => DayOneGuideUI.ShowingWaitingJob != Downtime.Job.Table, 1f, "the Now line moves on");
        yield return Photo("02-the-table-cleared");
    }

    // ---------- chat with a seated guest ----------

    IEnumerator ChatWithAGuest()
    {
        // 26 Oct 2026 (Mac run fix): the real game seats Day 2's settled guests at the open (Step H moved NPC wave 2 to Days 2-3),
        // but PatronSpawner.SeatOpeningGuests skips them in any lab session that doesn't ask (ForceOpeningGuests), so the old
        // SeatOpeningGuestsNow() call here did nothing and only a walk-in (15-20 s from the door) could ever sit. Asked for here,
        // before the lab's first frame yields, so the spawner's own first frame of the day seats them as the game does.
        forcedGuestsWas = PatronSpawner.ForceOpeningGuests;
        PatronSpawner.ForceOpeningGuests = true;
        forcedGuests = true;
        // 26 Oct 2026 (Mac run fix): this line ran inside the lab's Start, before any Update, so GuestGossip had not had its
        // morning yet (it deals the day's lines in its first Update) and read 0. Wait for the morning; still exactly two lines.
        yield return Until(() => GuestGossip.Instance != null && GuestGossip.Instance.Untold.Count == 2, 3f, "today has two lines of gossip");
        GuestGossip gossip = GuestGossip.Instance;
        Note($"today's gossip: [{(gossip != null ? string.Join(", ", gossip.Untold) : "no GuestGossip")}]");
        if (gossip == null) yield break;
        PatronSpawner.SeatOpeningGuestsNow();   // once a morning: a no-op when the spawner's open already seated them
        yield return Until(() => FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Any(p => p.IsSeated && !p.IsLeaving), 25f,
            "a guest is seated (Day 2's settled guests at the open, or a walk-in given 25 s to come in and sit)");
        if (!lastWait) yield break;
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        Note($"seated: {(spawner != null ? spawner.OpeningGuestsSeated.Count : 0)} settled guest(s) at the open, " +
             $"{FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Count(p => p.IsSeated && !p.IsLeaving && !p.SettledGuest)} walk-in(s).");
        Check(gossip.Holding == 0, "no gossip before the day's first customer is heard (it never competes with the day's first card)");
        yield return Until(() => Downtime.AtTheCounter != null, 45f, "the day's first customer reaches the counter");
        if (!lastWait) yield break;
        CustomerBrain first = Downtime.AtTheCounter;
        first.HearIntake();
        if (first.CanAcceptJob) first.AcceptJob();
        yield return Until(() => Downtime.TeachingStarted, 3f, "the day's teaching has begun");
        gossip.DealNow();
        yield return Until(() => gossip.Holding >= 1, 5f, "a seated guest has a line");
        if (!lastWait) yield break;
        GossipChat chat = null;
        string id = "";
        foreach (string untold in gossip.Untold)
        {
            chat = gossip.ChatFor(untold);
            if (chat != null) { id = untold; break; }
        }
        Check(chat != null && chat.Guest != null && chat.IsAvailable && chat.Prompt == GossipChat.Words, $"E on the guest: \"{(chat != null ? chat.Prompt : "")}\"");
        if (chat == null) yield break;
        Check(Juice.MarksUp >= 1, "a \"…\" over them");
        PatronBrain guest = chat.Guest;
        CafeLabDirector.Instance.MoveAce(guest.transform.position + guest.transform.forward * 1.1f, guest.transform.eulerAngles.y + 180f);
        yield return Seconds(.4f);
        yield return Photo("01-a-guest-with-gossip");

        int chatsBefore = Downtime.Today.chats;
        chat.Interact(player);
        yield return Seconds(.3f);
        Notebook book = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = book != null ? book.Find(id) : null;
        Check(Barks.TryGetShown(guest.transform, out _), "they say their line in a bubble");
        Check(fact != null && fact.source == Notebook.Sources.Overheard && fact.sure == Notebook.Sureness.Likely,
            $"the notebook learns it, overheard and likely: \"{(fact != null ? fact.text : "")}\"");
        string key = fact != null ? NoteSubjects.KeyOf(fact) : "";
        Check(key == NoteSubjects.Grace || key == NoteSubjects.Officer || key == NoteSubjects.Lodger, $"filed under who it's about ({key})");
        Check(Downtime.Today.chats == chatsBefore + 1 && gossip.LastTold == id, "one chat counted");
        Check(!ConversationController.AnyOpen && (ItemInspector.Instance == null || !ItemInspector.Instance.IsHoldingItem),
            "no conversation and no camera: Ace is free at once");
        yield return Until(() => gossip.ChatFor(id) == null && !gossip.Holds(guest), 1f, "their \"…\" goes");
        yield return Photo("02-the-line-said");
    }

    // ---------- restock by hand ----------

    IEnumerator RestockByHand()
    {
        ShopInventory stock = ShopInventory.Instance;
        BackShelf shelf = BackShelf.Instance;
        BeverageCupSupply take = FindObjectsByType<BeverageCupSupply>(FindObjectsInactive.Exclude).FirstOrDefault(s => !s.discard);
        BeverageCupSupply back = FindObjectsByType<BeverageCupSupply>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.discard);
        Check(stock != null && shelf != null && take != null, "the stock, the back shelf (Fixit Fidget > Days > Props 1) and the cup stack are in the café");
        if (stock == null || shelf == null || take == null) yield break;
        stock.SetStock(stock.Cups, stock.Beans, 3, 3);
        int shelfCups = stock.ShelfCups, shelfBeans = stock.ShelfBeans;
        Check(stock.MachineCups == 3 && stock.RefillWanted && shelfCups > 0, $"the machine at 3 cups, {shelfCups} on the back shelf: a refill is wanted");
        Check(shelf.IsAvailable && shelf.Prompt == BackShelf.TakeWords, $"E on the back shelf: \"{shelf.Prompt}\"");
        shelf.Interact(player);
        yield return Seconds(.3f);
        Check(StockCrate.InHand(carry) != null && carry.Count == 1 && carry.Summary.Contains(StockCrate.Name), $"a crate in one hand (\"{carry.Summary}\")");
        Check(shelf.Prompt == BackShelf.PutBackWords, $"...and E there puts it back: \"{shelf.Prompt}\"");
        shelf.Interact(player);
        yield return Seconds(.3f);
        Check(StockCrate.InHand(carry) == null && stock.MachineCups == 3 && stock.ShelfCups == shelfCups, "put back with one press: nothing moved");

        shelf.Interact(player);
        yield return Seconds(.3f);
        Check(take.Prompt == BeverageCupSupply.RefillWords, $"at the cup stack with the crate: \"{take.Prompt}\"");
        yield return Photo("01-the-crate-at-the-machine");
        int refillsBefore = Downtime.Today.refills;
        take.Interact(player);
        yield return Seconds(.3f);
        Check(stock.MachineCups == ShopInventory.MachineHolds && stock.MachineBeans == ShopInventory.MachineHolds
              && stock.ShelfCups == shelfCups - 9 && stock.ShelfBeans == shelfBeans - 9,
            $"refilled: the machine back to 12 of each, 9 of each from the shelf ({stock.ShelfCups} cups, {stock.ShelfBeans} beans left there)");
        Check(StockCrate.InHand(carry) == null && Downtime.Today.refills == refillsBefore + 1, "the crate is used up; one refill counted");
        Check(!stock.RefillWanted, "no refill wanted now");

        // Two full hands: "Hands full".
        take.Interact(player);
        take.Interact(player);
        yield return Seconds(.3f);
        Check(carry.Count == 2 && !shelf.IsAvailable && shelf.Prompt == "Hands full", $"two cups in hand: \"{shelf.Prompt}\"");
        if (back != null) { back.Interact(player); back.Interact(player); }
        yield return Seconds(.3f);

        // An order is never refused while the shelf has stock.
        stock.SetStock(stock.Cups, stock.Beans, 0, 0);
        BeverageSlot slot = FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.drink != null && s.drink.beansCost > 0);
        Check(slot != null && stock.ShelfCups > 0 && stock.CanMake(slot.drink) && !stock.CanBrew(slot.drink),
            "the machine empty, the shelf not: an order is still taken (a refill brings it), never \"sorry, we're out\"");
        yield return Photo("02-refilled");
    }
}
