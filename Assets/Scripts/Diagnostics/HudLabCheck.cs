using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE HUD AT EVERY HOUR - PLAY CHECK (26 Oct 2026 (HUD 3))
//
// A lab day on the HUD's own save (Day 2, $240 in the till, one star; your playtest save is not used), the autopilot off so
// no real ticket muddles the counts. It photographs his design A as built: the window top centre by day, late afternoon,
// last orders, closing and night (and the placeholder city, Seattle); the rail with 1, 3, 5, 7 and 9 tickets (stand-in
// customers, never a second row, every ticket visible with its icons); a payment with a tip mid-flight, landing and on the
// receipt; a star landing on the café front; the bench strip; first person; and a purchase on the Arrange screen's till (the
// same jar). Every photo's words are measured (ReviewScreen) and the HUD's own are held to the floor: 22 px and 4.5:1, or a
// shadow. Report and photos: Logs/HUD/hud-<time>/. Its last line is "[HUD] All N checks passed." or "[HUD] N of M checks FAILED. <folder>".
// ---------------------------------------------------------------------------
public sealed class HudLabCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Hud.Check";

    protected override string Title => "The HUD at every hour - play check";
    protected override string Tag => "[HUD]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[HUD] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("HUD check (this Play session only)");
        go.AddComponent<HudLabCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "HUD",
            $"hud-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    DayClock clock;
    HudCorners corners;
    TicketRailUI rail;
    PlayerInteractor player;
    CafeViewMode view;
    readonly List<CustomerBrain> standIns = new List<CustomerBrain>();
    GameObject fixture;
    ArrangeUI arrange;
    int wordsMeasured, wordsFailed;
    readonly List<string> wordFailures = new List<string>();

    static readonly (string name, string device, string fault, float patience, bool drink)[] Cast =
    {
        ("Grace", "reunion film camera", "Dirty Lens and Film Path", .9f, false),
        ("Tomas", "Phone", "Cracked Screen", .7f, true),
        ("Officer", "", "", .55f, true),
        ("Priya", "Pocket Watch", "Debris Inside, Won't Tell Time", .4f, false),
        ("Valerie", "Phone", "Battery Won't Hold", .2f, true),
        ("Ravi", "Pocket Watch", "Stuck Crown", .8f, true),
        ("Sam", "Phone", "Dead Speaker", .65f, false),
        ("Nina", "reunion film camera", "Jammed Shutter", .5f, true),
        ("Omar", "Phone", "Water Inside", .3f, false),
    };

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver && Camera.main != null, 40f, "the lab's day is open");
        if (!lastWait) yield break;
        clock = DayClock.Instance;
        yield return Until(() => HudCorners.Instance != null && HudCorners.Instance.Showing, 20f, "the HUD's corners are made and up");
        if (!lastWait) yield break;
        corners = HudCorners.Instance;
        rail = FindAnyObjectByType<TicketRailUI>();
        player = FindAnyObjectByType<PlayerInteractor>();
        view = player != null ? player.GetComponent<CafeViewMode>() : null;
        yield return Seconds(1f);

        // ---------- 1. the window and the column, by day ----------
        Rect win = OnScreen(corners.WindowRect);
        Check(Mathf.Abs(win.center.x - Screen.width * .5f) < Screen.width * .02f && win.yMax > Screen.height * .9f,
            $"the window sits top centre ({win.xMin:0}-{win.xMax:0} x {win.yMin:0}-{win.yMax:0} of {Screen.width} x {Screen.height})");
        Check(corners.CityShown == HudCity.SanFrancisco, $"its city is {corners.CityShown} (San Francisco: the Golden Gate, the Pyramid, the fog, the Ladies, a cable car)");
        Check(corners.ClockLine.Contains(Weekdays.Label(clock.Day)), $"the time and the day on its sill: \"{corners.ClockLine}\"");
        Check(corners.ClockLineSize >= 21.9f && corners.ClockGap >= 10f, $"...the day at {corners.ClockLineSize:0.#} px, {corners.ClockGap:0} px to spare with the time at its widest");
        Rect cash = OnScreen(corners.CashRect), sign = OnScreen(corners.SignRect);
        Check(cash.xMax > Screen.width * .8f && sign.xMax > Screen.width * .8f && sign.yMin >= cash.yMax - 1f,
            $"the column top right: the sign ({sign.yMin:0}-{sign.yMax:0} up) over the jar's board ({cash.yMin:0}-{cash.yMax:0})");
        Check(corners.SignWord == "OPEN" && corners.FaceShowing == "OPEN", $"the sign reads {corners.SignWord}");
        Check(corners.TakingsShown == "$" + clock.Earned && corners.CafeFrontShowing, $"today's takings read {corners.TakingsShown}; the little café front sits at the end of the stars' road ({corners.StarsShown} gold)");
        Check(corners.TopRightBottom > HudCorners.ColumnTop + 230f && corners.TopRightBottom < 320f, $"the column's bottom is {corners.TopRightBottom:0} px down (about 290)");
        Check(DayOneGuideSafe(), "the Now line's widest line stops short of the window (DayOneGuideUI.CompactWidth 740 at 24 in; the window starts 780 in)");
        yield return Shot("01-day-9am");

        // ---------- 2. the tickets: 1, 3, 5, 7, 9: one row, every ticket visible, big icons, the ring ----------
        if (rail == null) Check(false, "the ticket rail is in the scene");
        else
        {
            yield return TicketsAt(1, "02-ticket-1", JobTicket.Form.Wide);
            yield return TicketsAt(3, "03-tickets-3", JobTicket.Form.Wide);
            yield return TicketsAt(5, "04-tickets-5", JobTicket.Form.Narrow);
            yield return TicketsAt(7, "05-tickets-7", JobTicket.Form.Narrow);
            yield return TicketsAt(9, "06-tickets-9", JobTicket.Form.Narrow);
            ClearStandIns();
            yield return Seconds(.3f);
        }

        // ---------- 3. a payment with a tip: the bill flies from the pay pop, lands, counts; the coin; the receipt ----------
        HudMoney money = corners.Money;
        int before = clock.Earned;
        Transform payer = player != null ? player.transform : Camera.main.transform;
        ShopEconomy.Instance.AddMoney(15);
        clock.RecordServed(12, 3, false);
        Juice.Money(payer, 12, 3);
        yield return Seconds(.45f + .18f + .22f);
        Check(money.FlightUp, "0.85 s after the pay: the bill is in the air from the pay pop toward the jar");
        yield return Shot("07-paid-bill-in-flight");
        yield return Seconds(.45f + .66f + .25f - (.45f + .18f + .22f) - .1f);
        Check(corners.TakingsPunch > 1.005f || corners.TakingsShown != "$" + before, $"landed: the figure kicks and counts ({corners.TakingsPunch:0.00}x, {corners.TakingsShown})");
        yield return Shot("08-paid-landed-counting");
        yield return Seconds(.5f);
        Check(money.ReceiptShowing && money.ReceiptText.Contains("+$12"), $"the receipt prints the pay: \"{money.ReceiptText}\"");
        yield return Seconds(.3f);
        Check(money.ReceiptText.Contains("tip +$3"), $"...then the tip's line after the coin has landed: \"{money.ReceiptText}\"");
        yield return Shot("09-tip-on-the-receipt");
        yield return Seconds(1.4f);
        Check(corners.TakingsShown == "$" + clock.Earned && corners.EdgesShown == Mathf.Min(10, clock.Earned / 24),
            $"...and lands on today's ${clock.Earned}, the jar filled {corners.EdgesShown} step(s) of ten");
        yield return Seconds(.6f);
        Check(!money.ReceiptShowing, "the receipt has torn off (about 2.4 s after the pay)");

        // ---------- 4. a star lands: the drop, the glint down the road, the café front jumps and lights ----------
        if (corners.StarsShown > 0)
        {
            corners.LabStarLands();
            yield return Seconds(.95f);
            Check(corners.FranchiseLine.Contains("New star"), $"a star lands: the toast says \"{corners.FranchiseLine}\"");
            yield return Shot("10-star-lands");
            yield return Seconds(1.2f);
        }
        else Note("the lab save has no star: the landing isn't photographed");

        // ---------- 5. first person, and the bench strip ----------
        if (view != null && view.SetFirstPerson(true))
        {
            yield return Seconds(.7f);
            Check(corners.Showing, "the same HUD in first person");
            yield return Shot("11-first-person");
            view.SetFirstPerson(false);
            yield return Seconds(.5f);
        }
        else Note("first person could not be entered here (no camera or a close-up up): not photographed");
        corners.LabFold(true);
        yield return Seconds(.45f);
        Check(corners.BenchStrip && corners.TopRightBottom < 120f, $"folded for the bench: the strip ends {corners.TopRightBottom:0} px down (110)");
        yield return Shot("12-bench-strip");
        corners.LabFold(false);
        yield return Seconds(.4f);

        // ---------- 6. the hours: late afternoon (a rush), last orders, closing, night; the placeholder city ----------
        yield return StandIns(5);
        corners.LabPreview(16.42f, false);
        yield return Seconds(.4f);
        yield return Shot("13-late-afternoon-rush");
        corners.LabPreview(float.NaN, false);
        ClearStandIns();
        float lastOrdersAt = DayLengthSeconds() / Mathf.Max(1f, clock.ClosingHour - clock.OpeningHour);
        SetRemaining(Mathf.Min(clock.TimeRemaining, lastOrdersAt * .7f));
        yield return Until(() => corners.LastOrders, 3f, "an hour before closing the sign turns to LAST ORDERS");
        yield return Seconds(1.2f);
        Check(corners.DialWarmth > .2f, $"...and the window's sky has warmed toward golden hour ({corners.DialWarmth:0.00})");
        yield return Shot("14-last-orders");
        SetRemaining(.5f);
        yield return Until(() => corners.SignClosed, 4f, "at closing the sign flips over to CLOSED");
        yield return Seconds(1.1f);
        Check(corners.SunSet && corners.MoonShowing, "...the sun has gone down behind the city and the moon is up");
        yield return Shot("15-closing");
        corners.LabPreview(23.08f, true);
        yield return Seconds(.4f);
        Check(corners.ShowsNight && corners.ClockLine.Contains("night") && corners.ClockLine.Contains("11:04"), $"by night only the window, top centre: \"{corners.ClockLine}\"");
        yield return Shot("16-night");
        corners.LabCity(HudCity.Seattle);
        corners.LabPreview(18.5f, false);
        yield return Seconds(.4f);
        Check(corners.CityShown == HudCity.Seattle, $"the city layer swaps: {corners.CityShown} (the placeholder next city)");
        yield return Shot("17-seattle-placeholder");
        corners.LabCity(HudCity.SanFrancisco);
        corners.LabPreview(float.NaN, false);
        yield return Seconds(.3f);

        // ---------- 7. the Arrange screen's till: the same jar; a purchase leaves it ----------
        arrange = ArrangeUI.Create();
        arrange.Open();
        arrange.Till(200, 10);
        yield return Seconds(.4f);
        Check(arrange.MoneyShown == 200 && arrange.Jar != null, $"the Arrange screen's till is the jar: ${arrange.MoneyShown}");
        arrange.Puff(new Vector2(Screen.width * .5f, Screen.height * .45f));
        arrange.Till(120, 12);
        yield return Seconds(.36f);
        Check(arrange.Jar.FlightUp || arrange.Jar.DigitsColour != UiSkin.Paper, "a purchase: the bill leaves the jar for the piece and the figure counts down in plum");
        yield return Shot("18-arrange-spend");
        yield return Seconds(.5f);
        Check(arrange.Jar.ReceiptText.Contains("-$80"), $"...the receipt prints the spend: \"{arrange.Jar.ReceiptText}\"");
        yield return Seconds(.6f);
        Check(arrange.MoneyShown == 120, $"...and the till lands on $120 ({arrange.MoneyShown})");
        arrange.Close();

        // ---------- 8. the words in every photo ----------
        Check(wordsMeasured > 0 && wordsFailed == 0,
            $"every HUD word in every photo is at least 22 px and 4.5:1 or shadowed: {wordsMeasured} measured, {wordsFailed} under" +
            (wordFailures.Count > 0 ? " (" + string.Join("; ", wordFailures.Take(6)) + ")" : ""));
    }

    protected override void Restore()
    {
        ClearStandIns();
        if (corners != null) { corners.LabPreview(float.NaN, false); corners.LabFold(false); corners.LabCity(HudCity.SanFrancisco); }
        if (arrange != null) { arrange.Close(); Destroy(arrange.gameObject); }
    }

    // ---------- the tickets ----------

    IEnumerator TicketsAt(int count, string photo, JobTicket.Form form)
    {
        yield return StandIns(count);
        yield return Seconds(.55f);
        float railEnd = TicketRailUI.RailEnd(1920f);
        Check(rail.Count == count && rail.FormNow == form && rail.RightEdge <= railEnd + .5f,
            $"{count} ticket(s): one row of {rail.FormNow} dockets ending {railEnd - rail.RightEdge:0} px short of the window ({rail.Count} on the rail)");
        int withIcons = 0, inRow = 0;
        foreach (JobTicket t in rail.Tickets)
        {
            if (t == null) continue;
            if (t.Items.Contains("item_") || t.Items.Contains("drink_")) withIcons++;
            var r = (RectTransform)t.transform;
            if (Mathf.Abs(r.anchoredPosition.y) < 1f) inRow++;
        }
        Check(withIcons == count && inRow == count, $"...every ticket is in the one row and shows its items as icons ({withIcons} with icons, {inRow} in the row)");
        JobTicket urgent = rail.Tickets.FirstOrDefault(t => t != null && t.Target != null && t.Target.PatienceFraction < .5f);
        if (urgent != null) Check(urgent.ClockShowing && urgent.RingFill < .5f, $"...{urgent.Target.CustomerName}'s ring is {urgent.RingFill:P0} and the clock icon is up (losing patience)");
        yield return Shot(photo);
    }

    IEnumerator StandIns(int count)
    {
        if (fixture == null) { fixture = new GameObject("HUD check stand-ins"); fixture.SetActive(false); }
        DrinkDefinition[] drinks = Resources.FindObjectsOfTypeAll<DrinkDefinition>();
        for (int i = standIns.Count; i < count && i < Cast.Length; i++)
        {
            var (name, device, fault, patience, drink) = Cast[i];
            var go = new GameObject("Stand-in " + name);
            go.transform.SetParent(fixture.transform, false);
            var brain = go.AddComponent<CustomerBrain>();
            var identity = go.AddComponent<CustomerIdentity>();
            identity.SetupWalkIn(null, name);
            identity.Say(CustomerIdentity.Beat.Accepted);
            Set(brain, "identity", identity);
            Set(brain, "state", CustomerBrain.State.Waiting);
            Set(brain, "serviceMax", 100f);
            Set(brain, "patienceLeft", patience * 100f);
            if (device.Length > 0)
            {
                Set(brain, "record", new Job { kind = JobKind.Repair, deviceName = device, faultDescription = fault });
                Set(brain, "activeJob", go.AddComponent<RepairJob>());
            }
            else if (drinks.Length > 0)
            {
                // A drink customer: the job is the drink itself.
                Set(brain, "record", new Job { kind = JobKind.Drink, deviceName = "", faultDescription = "", drink = drinks[i % drinks.Length] });
                Set(brain, "activeJob", go.AddComponent<RepairJob>());
                Set(brain, "drinkOrdered", true);
            }
            else
            {
                Set(brain, "record", new Job { kind = JobKind.Repair, deviceName = "Phone", faultDescription = "Cracked Screen" });
                Set(brain, "activeJob", go.AddComponent<RepairJob>());
            }
            if (drink && drinks.Length > 0) { Set(brain, "drinkOrdered", true); Set(brain, "drinkWish", drinks[(i * 2 + 1) % drinks.Length]); }
            SetProperty(brain, "JobColor", Color.HSVToRGB(Mathf.Repeat(.07f + i * .13f, 1f), .55f, .9f));
            standIns.Add(brain);
            rail.LabStandIn(brain);
        }
        yield return null;
    }

    void ClearStandIns()
    {
        if (rail != null) rail.LabClear(standIns);
        foreach (CustomerBrain b in standIns) if (b != null) Destroy(b.gameObject);
        standIns.Clear();
    }

    // ---------- a photo, with its words held to the floor ----------

    IEnumerator Shot(string name)
    {
        yield return Photo(name);
        MeasureWords(name);
    }

    void MeasureWords(string name)
    {
        try
        {
            string path = Path.Combine(folder, name + ".text.json");
            if (!File.Exists(path)) { Note($"{name}: no word measure beside the photo (the review screen is off?)"); return; }
            var photo = JsonUtility.FromJson<ReviewScreen.PhotoCheck>(File.ReadAllText(path));
            int under = 0;
            foreach (ReviewScreen.TextMeasure m in photo.items)
            {
                if (!Ours(m) || m.alpha < .5f || string.IsNullOrWhiteSpace(m.text)) continue;
                wordsMeasured++;
                bool big = m.px1080 >= 21.5f;
                bool clear = !m.backgroundKnown || m.contrast < 0f || m.contrast >= 4.5f || m.outlined || m.underlay;
                if (big && clear) continue;
                under++;
                wordsFailed++;
                if (wordFailures.Count < 12) wordFailures.Add($"{name}: \"{Short(m.text)}\" {m.px1080:0.#} px, {m.contrast:0.0}:1");
            }
            Note($"{name}: {photo.texts} texts measured, {under} HUD word(s) under the floor");
        }
        catch (Exception e) { Note($"{name}: the word measure could not be read ({e.Message})"); }
    }

    static bool Ours(ReviewScreen.TextMeasure m) =>
        (m.root ?? "").StartsWith("HUD corners", StringComparison.Ordinal) || m.owner == "HudCorners" || m.owner == "JobTicket" || m.owner == "ArrangeUI"
        || (m.path ?? "").Contains("Arrange canvas");

    // ---------- the plumbing ----------

    static bool DayOneGuideSafe()
    {
        FieldInfo f = typeof(DayOneGuideUI).GetField("CompactWidth", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        float width = f != null ? Convert.ToSingle(f.GetRawConstantValue()) : 800f;
        return UiSkin.Margin + width <= 1920f * .5f - HudWindow.Width * .5f;
    }

    float DayLengthSeconds() =>
        (float)(typeof(DayClock).GetField("dayLengthSeconds", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(clock) ?? 180f);

    // The lab moves the day on: what's left of it (the clock's own countdown), as the pause lab does.
    void SetRemaining(float seconds)
    {
        PropertyInfo left = typeof(DayClock).GetProperty("TimeRemaining", BindingFlags.Instance | BindingFlags.Public);
        left?.GetSetMethod(true)?.Invoke(clock, new object[] { Mathf.Max(0f, seconds) });
        Note($"the clock moved on: {seconds:0.0} s of the day left ({ShopUI.FormatHour(clock.CurrentHour)})");
    }

    static void Set(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(target, value);
    }

    static void SetProperty(object target, string property, object value)
    {
        PropertyInfo p = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        p?.GetSetMethod(true)?.Invoke(target, new[] { value });
    }

    static Rect OnScreen(RectTransform rect)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
