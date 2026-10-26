using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE PHONE BY DAY: THE PAUSE AND THE HUD'S CORNERS - PLAY CHECK (playtest 3, session 3, 7 Oct 2026;
// claude/playtest-3-sessions-2-6-plan.md §3.5)
//
// A lab day, three moments: early, the middle of the day, and last orders (the clock moved on for the last two). At each,
// something that runs out is put up first (a note, a badge over a customer), the phone comes up (Esc), and for three
// seconds nothing moves and nothing expires: the clock, Ace, the customers and their patience, the note, the badge, the
// HUD's sway and pulse. Then it goes away (Esc, the bottom button, or a pad's B) and the day carries on where it was: the
// clock moves again and the note runs out. On the way: the apps (Today, Notes, Settings), a slider and a switch that
// change the settings and are put back, Quit asking again (never pressed twice), the phone refusing to come up over a
// close-up (Esc steps out of the close-up first), and the HUD's corners (the takings, the sign's three faces, the tabs,
// a low-stock chip). Photos of each, for the look. The lab's own save; your playtest save is not used.
//
// Playtest 4 (10 Oct 2026): the clock line under the sign keeps the day and the time apart on every weekday; Today says
// what the café's next star needs (not "no stars yet") and what Ace's work earned apart from the café's guests.
//
// 12-13 Oct 2026 (his answers to the findings doc, §1b and §6): the two settings the other leads read. Settings › CONTROLS'
// "Swap the sticks at the bench" flips GameSettings.SwapBenchSticks; the new MORNINGS section (above PICTURE) has "The
// morning", Full / Short / Off, and each pill sets GameSettings.DayIntro. Every change is kept in PlayerPrefs at once and
// reads back as kept (GameSettings.Reload, then read again), the switch and the pills showing it. A pad reaches the morning
// the way it reaches the picture's quality: its routes, then a virtual pad's D-pad and A. Both settings are put back.
//
// 19 Oct 2026 (her playtest 7; his answers): the HUD is one column top right: the sign, then on a dark shelf the sun dial
// with the time and the day, the takings with the stack against the figure, and the café's stars with "Franchise at 5
// stars". The checks follow it: the sign and the takings both top right, the sign above the takings; the sun where the
// day's clock is; the stars as the ledger holds them; a low-stock chip that says when to buy, within the alert budget; at
// last orders the dial warms and the hud.lastorders chime is asked for, and there is no screen tint any more; at closing the
// sign turns to CLOSED without ever showing OPEN on the way (the reviewers saw "OPEN · COME IN" at 8:00 PM), the sign's flap
// (intro.sign) is asked for, and the sun sets below the dial with the moon up.
//
// 23 Oct 2026 (the phone moves; his note: "I want there to be a feeling, I want there to feel good"): the phone rises as it comes
// out and drops away as it goes. The game is given back at once, so that is checked straight away, as ever (the phone may still
// be dropping); that the phone has gone is checked once it has dropped. Every read of the phone's look (a switch's or a pill's colour, the ring) and every photo
// first waits for the phone to be still (RecapPhone.Settled, up to a second), the report counts those reads, and a check at the
// end fails if any was made while it still moved. New: it rises and settles within 0.25 s; switching to Settings settles within
// 0.25 s; the pad's ring ends exactly on the morning's Short.
// ---------------------------------------------------------------------------
public sealed class PauseCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Pause.Check";

    const string SwapKey = "swapsticks";
    const string PauseNote = "A note that runs out in two seconds.";
    // The switch just above the morning's row (20 Oct: since 15 Oct "Vibration" sits under "Swap the sticks", so the route down
    // onto Full starts from it; the swap switch is still checked by its own steps above).
    const string AboveMorningKey = "vibration";
    static readonly DayIntroMode[] Mornings = { DayIntroMode.Full, DayIntroMode.Short, DayIntroMode.Off };

    protected override string Title => "The phone by day (the pause) and the HUD's corners - play check";
    protected override string Tag => "[Pause check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Pause check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Pause check (this Play session only)");
        go.AddComponent<PauseCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Pause",
            $"pause-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    DayClock clock;
    PausePhone pause;
    PlayerInteractor player;
    HudCorners corners;
    // The settings as they were, put back at the end whatever happens.
    float masterWas = -1f;
    bool invertWas, invertTouched;
    bool swapWas, swapTouched;
    DayIntroMode introWas;
    bool introTouched;
    // 23 Oct 2026: the reads of the phone's look (colours, the ring, photos), and any made while it still moved.
    int looks;
    readonly System.Collections.Generic.List<string> lookedMoving = new System.Collections.Generic.List<string>();

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        clock = DayClock.Instance;
        yield return Until(() => PausePhone.Instance != null && PausePhone.Instance.Phone != null && HudCorners.Instance != null, 5f,
            "the phone by day and the HUD's corners are made");
        if (!lastWait) yield break;
        pause = PausePhone.Instance;
        corners = HudCorners.Instance;
        player = FindAnyObjectByType<PlayerInteractor>();
        Check(RecapPhone.PauseSortingOrder == 95 && pause.Phone.GetComponent<Canvas>().sortingOrder == 95,
            "the phone by day draws over everything but the pad's cursor (95)");

        // ---------- the HUD's column, by day ----------
        yield return Seconds(1.5f);
        Check(corners.Showing && corners.BottomShowing, "the HUD is up by day: the window top centre, the column top right (the sign, the jar, the stars) and the chips' corner");
        Check(corners.SignWord == "OPEN" && corners.FaceShowing == "OPEN" && !corners.LastOrders && !corners.SignClosed, $"the sign reads {corners.SignWord}");
        Check(corners.ClockLine.Contains(Weekdays.Label(clock.Day)), $"on the window's sill top centre, the time and the day: \"{corners.ClockLine}\"");   // 26 Oct 2026 (HUD 3)
        Check(corners.ClockLineSize >= 21.9f, $"...the day line at {corners.ClockLineSize:0.#} px (24, or 22 before a long weekday goes short, never less: her \"way too small\"; 26 Oct 2026, Readability)");
        // Playtest 4 (the first hour: "Wednesday · Day 3 9:09 AM" ran together): the day line and the time each have room,
        // measured with the time at its widest, on today's line and on every weekday's (a long day number too).
        Check(corners.ClockGap >= 10f, $"...with room to spare between them on the sill ({corners.ClockGap:0} px with the time at its widest)");
        float least = float.MaxValue;
        string tightest = "";
        foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 7, 10, 100 })
        {
            float gap = corners.GapFor(Weekdays.Label(d), "12:00 PM");
            if (gap < least) { least = gap; tightest = Weekdays.Label(d); }
        }
        Check(least >= 10f, $"...and on every weekday's line (the tightest, \"{tightest}   12:00 PM\", keeps {least:0} px)");
        // The sun dial: the sun where the day's clock is, up (not set), no moon yet.
        Check(Mathf.Abs(corners.SunProgress - clock.NormalizedDay) < .03f && !corners.SunSet && !corners.MoonShowing,
            $"the sun is {corners.SunProgress:0.00} of the way through its day in the window (the day is {clock.NormalizedDay:0.00} through); no moon");   // 26 Oct 2026 (HUD 3): the window, not the dial
        Check(corners.DialWarmth < .05f, "...and the sky is still the day's (it warms toward golden hour in the last hour)");
        Check(corners.TakingsShown == "$" + clock.Earned, $"today's takings read {corners.TakingsShown} (today ${clock.Earned})");
        // The café's stars, and the goal under them (her: "You have no idea, right? No.").
        ReputationLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Reputation : null;
        int starsHeld = ledger != null ? Mathf.Clamp(ledger.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        // 26 Oct 2026 (HUD 3): "Franchise at 5 stars" is gone (his answer, 24 Oct); the goal is the tiny café front at the
        // end of the stars' road, and on Day 1 a 6 s hint, "Five stars to franchise", under the column.
        Check(corners.StarsShown == starsHeld && corners.CafeFrontShowing,
            $"the café's stars: {corners.StarsShown} of {ReputationRules.MaxStars} gold (the ledger holds {starsHeld}), and the little café front at the road's end");
        Check(clock.Day != 1 || corners.FranchiseLine.ToLowerInvariant().Contains("franchise"),
            $"...on Day 1 the goal is said once under the column: \"{corners.FranchiseLine}\"");
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        Check(!HudText(hud, "moneyText") && !HudText(hud, "clockText") && !HudText(hud, "stockText"),
            "the old money, clock and stock lines are put away by day (the column carries them)");
        RectTransform cash = corners.CashRect, sign = corners.SignRect;
        Rect cashOn = OnScreen(cash), signOn = OnScreen(sign);
        Check(cashOn.xMax > Screen.width * .8f && cashOn.yMin > Screen.height * .55f && signOn.xMax > Screen.width * .8f && signOn.yMax > Screen.height * .85f
              && signOn.yMin >= cashOn.yMax - 1f,
            $"the sign sits top right ({signOn.xMin:0}-{signOn.xMax:0} x {signOn.yMin:0}-{signOn.yMax:0}) with the takings under it " +
            $"({cashOn.xMin:0}-{cashOn.xMax:0} x {cashOn.yMin:0}-{cashOn.yMax:0}) of {Screen.width} x {Screen.height}");
        // 26 Oct 2026 (HUD 3): the column is one hung piece about 266 px tall now (the sign, the jar's board, the stars), not 358.
        Check(corners.TopRightBottom > HudCorners.ColumnTop + 230f && corners.TopRightBottom < 320f && !corners.BenchStrip,
            $"the column's bottom edge, for whatever sits under it, is {corners.TopRightBottom:0} px down (reference pixels; not folded for the bench)");
        yield return Shot("01-the-corners-by-day");

        // A payment: the stack and the figure.
        int before = clock.Earned;
        ShopEconomy.Instance.AddMoney(30);
        clock.RecordServed(30, 0, false);   // a customer's payment (patrons pay quietly since playtest 4)
        yield return Seconds(.15f);
        Check(corners.TakingsPunch > 1.01f || corners.TakingsShown != "$" + before, $"a payment punches the figure ({corners.TakingsPunch:0.00}x, {corners.TakingsShown})");
        yield return Shot("02-paid");
        yield return Seconds(1f);
        Check(corners.TakingsShown == "$" + clock.Earned && corners.BillShowing && corners.EdgesShown == Mathf.Min(10, clock.Earned / 24),
            $"...and lands on today's ${clock.Earned}, money in the jar, filled {corners.EdgesShown} step(s) of ten (one for every $24)");   // 26 Oct 2026 (HUD 3): the jar

        // Low stock: a chip, bottom right.
        ShopInventory stock = ShopInventory.Instance;
        int cupsWas = stock != null ? stock.Cups : 0;
        if (stock != null)
        {
            int beansWas = stock.Beans;
            stock.SetStock(3, beansWas);
            yield return Seconds(.6f);
            Check(corners.CupsChipShowing && corners.ChipsText.Contains("cups") && corners.ChipsText.Contains("3 left"), $"low on cups: a chip, bottom right (\"{corners.ChipsText}\")");
            // 19 Oct (her: "Low on beans... How do you know?... You buy it up through your phone. Oh, I wouldn't know."): it
            // says when to buy, inside the alert's word budget.
            Check(corners.ChipsText.Contains("Restock at closing") && !WordBudget.Over(corners.ChipsText, WordBudget.BenchAlert),
                $"...and it says when to buy, in {WordBudget.Report(corners.ChipsText, WordBudget.BenchAlert)}");
            Rect chipsOn = OnScreen(corners.ChipsRect);
            Check(chipsOn.xMax > Screen.width * .8f && chipsOn.yMin < Screen.height * .3f, "...in the bottom right corner");
            yield return Shot("03-low-on-cups");
            stock.SetStock(cupsWas, beansWas);
            yield return Seconds(.3f);
            Check(!corners.CupsChipShowing, "restocked, the chip goes");
        }

        // ---------- moment 1: early in the day ----------
        yield return Moment("early", pressEsc: true, closeWith: "Esc");

        // The apps, settings and Quit (while it's up).
        yield return TheApps();

        // ---------- moment 2: the middle of the day ----------
        SetRemaining(clock.TimeRemaining > 95f ? 90f : clock.TimeRemaining);
        yield return Seconds(1.5f);
        yield return Moment("midday", pressEsc: true, closeWith: "button");

        // ---------- not over a close-up ----------
        yield return NotOverACloseUp();

        // ---------- moment 3: last orders ----------
        float lastOrdersAt = DayLengthSeconds() / Mathf.Max(1f, clock.ClosingHour - clock.OpeningHour);
        int chimesBefore = Sfx.Requested("hud.lastorders");
        SetRemaining(Mathf.Min(clock.TimeRemaining, lastOrdersAt * .7f));
        yield return Until(() => corners.LastOrders, 3f, "an hour before closing the sign turns to LAST ORDERS");
        yield return Seconds(1.2f);
        Check(corners.SignWord == "LAST ORDERS" && corners.FaceShowing == "LAST ORDERS", $"...it reads \"{corners.SignWord}\" (the face showing: \"{corners.FaceShowing}\")");
        Check(Sfx.Requested("hud.lastorders") > chimesBefore, "...with the soft chime (hud.lastorders asked for)");
        // 19 Oct: no screen tint any more (his: the orange tint "doesnt fit the overall cozy aesthetic"); the dial warms instead.
        Check(corners.DialWarmth > .2f, $"...and the window's sky has warmed toward golden hour ({corners.DialWarmth:0.00}; no screen tint)");
        yield return Shot("20-last-orders");
        yield return Moment("last orders", pressEsc: false, closeWith: "pad");

        // ---------- closing ----------
        int flapsBefore = Sfx.Requested("intro.sign");
        SetRemaining(.5f);
        yield return Until(() => corners.SignClosed, 4f, "at closing the sign flips over to CLOSED");
        // Through the whole turn the face on screen is the old one (LAST ORDERS) and then CLOSED, never OPEN (the reviewers
        // saw "OPEN · COME IN" at 8:00 PM, mid-turn: the faces are set before the card turns now).
        bool sawOpen = false, sawLastOrders = false, photographed = false;
        float turnStarted = UiClock.Now;
        while (UiClock.Now - turnStarted < .9f)
        {
            string facing = corners.FaceShowing;
            if (facing == "OPEN") sawOpen = true;
            if (facing == "LAST ORDERS") sawLastOrders = true;
            if (!photographed && UiClock.Now - turnStarted >= .35f) { photographed = true; yield return Shot("30-the-sign-flips"); }
            yield return null;
        }
        Check(!sawOpen && sawLastOrders, $"...the turn shows LAST ORDERS, then CLOSED, never OPEN ({(sawOpen ? "OPEN was seen" : "no OPEN")}; {(sawLastOrders ? "LAST ORDERS on the way" : "LAST ORDERS not seen")})");
        yield return Seconds(.4f);
        Check(corners.SignWord == "CLOSED" && corners.FaceShowing == "CLOSED", $"...it reads \"{corners.SignWord}\" once it has turned");
        Check(Sfx.Requested("intro.sign") > flapsBefore, "...with the sign's own wooden flap (intro.sign asked for; the day's bell is DayClock's)");
        Check(corners.SunSet && corners.MoonShowing, "...the sun has gone down behind the city and the moon is up in the window");
        yield return Shot("31-closed");
        bool up = pause.Open();
        Check(up, "the phone still comes up while the café finishes up (it isn't the recap yet)");
        yield return Frames(2);
        if (up) yield return Shot("32-phone-after-closing");
        if (PausePhone.Paused) { pause.Resume(); yield return Frames(2); }
        // 23 Oct: every read above of the phone's look waited for it to be still.
        Check(looks > 0 && lookedMoving.Count == 0,
            $"nothing read a mid-motion position: {looks} reads of the phone's colours, its ring and the photos, " +
            (lookedMoving.Count == 0 ? "all with the phone still" : $"{lookedMoving.Count} while it moved ({string.Join("; ", lookedMoving)})"));
    }

    // ---------- a moment: something running out, the phone up, nothing moves, the phone away, the day goes on ----------

    IEnumerator Moment(string when, bool pressEsc, string closeWith)
    {
        // Wait for someone in the café, to watch them hold still.
        yield return Await(() => Customers().Any(), 20f);
        CustomerBrain someone = Customers().FirstOrDefault();
        NightCycle.Note(PauseNote, 2f);
        if (someone != null) Juice.Emote(someone.transform, Juice.Icon.Star);
        yield return Seconds(.25f);
        View before = ViewNow();

        // Up: Esc (keyboard) or Start (a pad).
        if (pressEsc) yield return Key(UnityEngine.InputSystem.Key.Escape);
        else
        {
            PlugInPad("Pause check pad");
            yield return PadPress(GamepadButton.Start);
        }
        if (!PausePhone.Paused)
        {
            Note($"{when}: the key didn't reach the game (the Game view didn't have the keyboard?); the phone brought up directly.");
            pause.Open();
            yield return Frames(2);
        }
        Check(PausePhone.Paused && Time.timeScale == 0f && UiClock.Held, $"{when}: {(pressEsc ? "Esc" : "Start")} brings the phone up and the game holds still");
        yield return Frames(2);

        // Everything as it stands, now that it's held. (The frame the key landed in still ran on: its time was counted
        // before the key arrived. So the stillness is measured from here, not from before the key.)
        float hour = clock.CurrentHour, left = clock.TimeRemaining, ui = UiClock.Now;
        Vector3 ace = player.transform.position;
        Vector3 at = someone != null ? someone.transform.position : Vector3.zero;
        float patience = someone != null ? someone.PatienceFraction : 0f;
        int pops = Juice.PopsUp;
        float sway = corners.SignRect != null ? SwayOf() : 0f, sun = corners.SunProgress;
        View held = ViewNow();
        RecapPhone phone = pause.Phone;
        Check(phone.Root.activeInHierarchy && phone.Current == RecapPhone.App.Reviews && phone.LabelOf(phone.Current) == "Today"
            && string.Join(",", phone.VisibleApps) == "Reviews,Notes,Settings",
            $"{when}: it opens on Today, with Notes and Settings (tabs: {string.Join(", ", phone.VisibleApps.Select(a => phone.LabelOf(a)))})");
        yield return Frames(2);
        string today = phone.ContentText();
        Check(today.Contains("earned so far today") && today.Contains("In the till") && today.Contains("$" + clock.Earned),
            $"{when}: Today shows today's takings (${clock.Earned}) and what's in the till");
        // Playtest 4: under the café's stars, what the next one needs (today's reviews counted in), not "no stars yet"; and
        // what Ace's work earned apart from the café's guests.
        ReputationLedger rep = SaveManager.Instance != null ? SaveManager.Instance.Reputation : null;
        string starsLine = RecapPhone.LiveStarsLine(rep);
        Check(today.Contains(starsLine) && !today.Contains("no stars yet"), $"{when}: under the café's stars, \"{starsLine}\"");
        string split = RecapPhone.TakingsSplit(clock.Earned, clock.PatronIncome);
        Check(split.Length == 0 ? !today.Contains("from café guests") : today.Contains(split),
            split.Length == 0 ? $"{when}: no guests have paid: no split line" : $"{when}: \"{split}\"");
        yield return Shot($"1{(when == "early" ? 0 : when == "midday" ? 1 : 2)}-phone-{when.Replace(' ', '-')}");

        yield return Seconds(3f);
        Check(Mathf.Abs(clock.CurrentHour - hour) < .0001f && Mathf.Abs(clock.TimeRemaining - left) < .0001f,
            $"{when}: three seconds later the clock hasn't moved ({ShopUI.FormatHour(clock.CurrentHour)}; {Mathf.Abs(clock.TimeRemaining - left):0.0000} s of the day went)");
        Check((player.transform.position - ace).sqrMagnitude < 1e-6f, $"{when}: nor has Ace ({(player.transform.position - ace).magnitude:0.000} m)");
        if (someone != null)
            Check((someone.transform.position - at).sqrMagnitude < 1e-6f && Mathf.Abs(someone.PatienceFraction - patience) < 1e-5f,
                $"{when}: nor {someone.CustomerName}, whose patience is where it was ({patience:0.000}; moved {(someone.transform.position - at).magnitude:0.000} m)");
        Check(UiClock.Now - ui < .3f, $"{when}: the screen's own clock stood still ({UiClock.Now - ui:0.00} s of it passed)");
        Check(NoteUp() && Juice.PopsUp >= Mathf.Min(1, pops), $"{when}: the two-second note and the badge are still up");
        Check(Mathf.Abs(Mathf.DeltaAngle(SwayOf(), sway)) < .01f && Mathf.Abs(corners.SunProgress - sun) < .001f,
            $"{when}: the sign's sway and the sun in the window held too ({Mathf.DeltaAngle(sway, SwayOf()):0.000}°, {corners.SunProgress - sun:0.0000})");
        View later = ViewNow();
        Check(Same(before, held) && Same(held, later),
            $"{when}: the view behind the phone holds still (before {before}; up {held}; three seconds on {later})");

        // Away.
        if (closeWith == "Esc") yield return Key(UnityEngine.InputSystem.Key.Escape);
        else if (closeWith == "button") { phone.CloseButton.onClick.Invoke(); yield return Frames(2); }
        else yield return PadPress(GamepadButton.East);
        if (PausePhone.Paused)
        {
            Note($"{when}: closing by {closeWith} didn't reach the game; put away directly.");
            pause.Resume();
            yield return Frames(2);
        }
        // 23 Oct: the game is given back at once (checked now, as ever); the phone drops away over a moment, and has gone once it
        // has (checked when it's still).
        Check(!PausePhone.Paused && Time.timeScale > 0f && !UiClock.Held,
            $"{when}: {closeWith} gives the game back at once (the phone {(phone.Closing ? "dropping away as it goes" : "already gone")})");
        yield return Settle();
        Looking("the phone put away");
        Check(!phone.Root.activeInHierarchy, $"{when}: {closeWith} puts it away (dropped out of sight, and off)");
        yield return Seconds(2.6f);
        Check(clock.CurrentHour > hour + .0001f, $"{when}: the day carries on ({ShopUI.FormatHour(hour)} to {ShopUI.FormatHour(clock.CurrentHour)})");
        // 20 Oct (builder N): a note stays at least NightCycle.NoteSeconds (2.5 s and 0.45 s a word, a slow reader's time),
        // whatever it asks for; it was up 0.25 s before the pause, so the rest of its time is waited out here.
        float rest = NightCycle.NoteSeconds(PauseNote) - .25f - 2.6f;
        if (rest > 0f) yield return Seconds(rest + .3f);
        Check(!NoteUp(), $"{when}: and the note runs out now, after the pause ({NightCycle.NoteSeconds(PauseNote):0.0} s of reading time)");
        if (pad != null) PadRelease();
    }

    // Today, Notes, Settings: switching, a slider and a switch that change the settings (put back), Quit asking again.
    IEnumerator TheApps()
    {
        if (!pause.Open()) { Check(false, "the phone comes up for the apps"); yield break; }
        RecapPhone phone = pause.Phone;
        // 23 Oct: it rises out of the pocket, under way the frame it comes up, and is still within 0.25 s (its first app's rows
        // arrive in the same time). Seen from here it reads still within three frames of its end: its clock moves a frame at a time,
        // this reads it before the frame's last step, and the frame after its first app is built counts at most a 60th of a second
        // of the motion (RecapPhone.Now). So the grace is the three longest frames seen.
        bool rising = !phone.Settled;
        float began = Time.realtimeSinceStartup, longest = 0f;
        while (!phone.Settled && Time.realtimeSinceStartup - began < 1f)
        {
            yield return null;
            longest = Mathf.Max(longest, Time.unscaledDeltaTime);
        }
        float took = Time.realtimeSinceStartup - began;
        Check(rising && phone.Settled && took <= .25f + 3f * longest + .001f,
            $"the phone rises as it comes up and reads still {took:0.000} s later: within 0.25 s (frames here up to {longest * 1000f:0} ms)");
        yield return Frames(2);
        yield return Key(UnityEngine.InputSystem.Key.E);
        if (phone.Current != RecapPhone.App.Notes) { Note("E didn't reach the game; Notes opened directly."); phone.Open(RecapPhone.App.Notes); }
        yield return Frames(2);
        Check(phone.Current == RecapPhone.App.Notes && phone.ContentText().Contains("Notes"), "E opens Notes");
        yield return Shot("13-phone-notes");
        phone.Open(RecapPhone.App.Settings);
        // 23 Oct: the switch's whole motion (the slide, the pill, the rows arriving) begins this frame; it's over within 0.25 s.
        float switchLeft = phone.MotionLeft;
        Check(!phone.Settled && switchLeft <= .25f + .001f, $"switching to Settings slides it in and settles within 0.25 s ({switchLeft:0.000} s of motion)");
        yield return Frames(2);
        string settings = phone.ContentText();
        int mornings = settings.IndexOf("MORNINGS", StringComparison.Ordinal), picture = settings.IndexOf("PICTURE", StringComparison.Ordinal);
        Check(settings.Contains("SOUND") && settings.Contains("CONTROLS") && mornings >= 0 && picture > mornings && settings.Contains("Quit"),
            "Settings: sound, controls, the morning (above the picture), the picture, and Quit");
        yield return Shot("14-phone-settings");

        Slider master = phone.ContentSlider("master");
        Check(master != null && Mathf.Abs(master.value - GameSettings.Master) < .001f, $"the master volume's slider reads the setting ({GameSettings.Master:0.00})");
        if (master != null)
        {
            masterWas = GameSettings.Master;
            master.value = .4f;
            Check(Mathf.Abs(GameSettings.Master - .4f) < .001f && Mathf.Abs(AudioListener.volume - .4f) < .001f,
                "moving it sets the master volume, at once (the listener's volume)");
            master.value = masterWas;
            masterWas = -1f;
        }
        Button invert = phone.ContentButton("invert");
        Check(invert != null, "invert Y has its switch");
        if (invert != null)
        {
            invertWas = GameSettings.InvertY;
            invertTouched = true;
            invert.onClick.Invoke();
            yield return Frames(2);
            Check(GameSettings.InvertY != invertWas, $"its switch turns invert Y {(GameSettings.InvertY ? "on" : "off")}");
            Button back = phone.ContentButton("invert");
            if (back != null) back.onClick.Invoke();
            yield return Frames(2);
            Check(GameSettings.InvertY == invertWas, "and back");
            invertTouched = false;
        }

        // The two settings the other leads read (12-13 Oct).
        yield return TheBenchSticks(phone);
        yield return TheMorning(phone);

        Button quit = phone.ContentButton("quit");
        Check(quit != null && phone.ContentText().Contains("isn't kept"), "Quit says plainly that the day so far isn't kept");
        if (quit != null)
        {
            quit.onClick.Invoke();   // once only: the second press would quit
            yield return Frames(2);
            Check(phone.QuitArmed && phone.ContentText().Contains("Press again to quit"), "a first press of Quit asks again, in red");
            phone.RevealKey("quit");   // scrolled to it, for the photo
            yield return Frames(2);
            yield return Shot("15-quit-asks-again");
            phone.Open(RecapPhone.App.Reviews);
            yield return Frames(2);
            Check(!phone.QuitArmed, "opening another app takes the question back");
        }
        pause.Resume();
        yield return Frames(3);
    }

    // Settings › CONTROLS: "Swap the sticks at the bench" flips GameSettings.SwapBenchSticks, kept in PlayerPrefs at once
    // (FixitFidget.Settings.SwapBenchSticks: 0 or 1) and read back as kept, the switch showing it; then back as it was.
    IEnumerator TheBenchSticks(RecapPhone phone)
    {
        string text = phone.ContentText();
        Button swap = phone.ContentButton(SwapKey);
        Check(swap != null && text.Contains("Swap the sticks at the bench") && text.Contains("Left stick moves your hand, right stick turns the device."),
            "CONTROLS: \"Swap the sticks at the bench\", its line, and its switch");
        if (swap == null) yield break;
        swapWas = GameSettings.SwapBenchSticks;
        swapTouched = true;
        yield return Settle();   // 23 Oct: a switch's colour is read once nothing moves (invert's knob may still be sliding back)
        Check(SwitchOn(phone, SwapKey) == swapWas, $"...the switch shows the setting as it stands ({(swapWas ? "on" : "off")})");
        swap.onClick.Invoke();
        yield return Frames(2);
        bool now = GameSettings.SwapBenchSticks;
        int kept = PlayerPrefs.GetInt(GameSettings.SwapBenchSticksKey, -1);
        Check(now != swapWas && kept == (now ? 1 : 0), $"its switch turns it {(now ? "on" : "off")}, kept at once ({GameSettings.SwapBenchSticksKey} = {kept})");
        GameSettings.Reload();
        yield return Settle();   // its knob slides across as its colour turns: the colour is read where it ends
        Check(GameSettings.SwapBenchSticks == now && SwitchOn(phone, SwapKey) == now, "...read back as kept, and the switch shows it");
        phone.RevealKey(SwapKey);
        yield return Frames(2);
        yield return Shot("14a-settings-swap-the-sticks");
        Button back = phone.ContentButton(SwapKey);
        if (back != null) back.onClick.Invoke();
        yield return Frames(2);
        GameSettings.Reload();
        Check(GameSettings.SwapBenchSticks == swapWas && PlayerPrefs.GetInt(GameSettings.SwapBenchSticksKey, -1) == (swapWas ? 1 : 0),
            "and back, kept and read back");
        // swapTouched stays up: Restore puts it back once more at the end, whatever a later step pressed.
    }

    // Settings › MORNINGS: "The morning", Full / Short / Off. Each pill sets GameSettings.DayIntro, kept in PlayerPrefs at once
    // (FixitFidget.Settings.DayIntro: 0, 1 or 2) and read back as kept, its pill in green and the others not; a whole turn
    // from Short. Then a pad, and back as it was.
    IEnumerator TheMorning(RecapPhone phone)
    {
        string text = phone.ContentText();
        Check(text.Contains("The morning") && text.Contains("A few seconds of the café waking up, every day. Short: just the title."),
            "MORNINGS: \"The morning\", with its line");
        introWas = GameSettings.DayIntro;
        introTouched = true;
        yield return Settle();   // 23 Oct: the pills' colours are read once nothing moves
        Check(OnlyPillOn(phone, introWas), $"...three pills, Full / Short / Off, the one in force ({introWas}) in green");
        foreach (DayIntroMode mode in new[] { DayIntroMode.Short, DayIntroMode.Off, DayIntroMode.Full })
        {
            Button pill = phone.ContentButton("morning:" + mode);
            if (pill == null) { Check(false, $"\"{mode}\" has its pill"); yield break; }
            pill.onClick.Invoke();
            yield return Frames(2);
            int kept = PlayerPrefs.GetInt(GameSettings.DayIntroKey, -1);
            GameSettings.Reload();
            yield return Settle();   // the pressed pill squishes and springs back
            Check(GameSettings.DayIntro == mode && kept == (int)mode && OnlyPillOn(phone, mode),
                $"\"{mode}\" sets the morning to {mode}: kept as {kept}, read back as {GameSettings.DayIntro}, its pill in green");
            if (mode != DayIntroMode.Short) continue;
            phone.RevealKey("morning:Short");
            yield return Frames(2);
            yield return Shot("14b-settings-the-morning");
        }
        yield return PadToTheMorning(phone);
        GameSettings.DayIntro = introWas;
        phone.RebuildNow();
        yield return Frames(2);
        GameSettings.Reload();
        yield return Settle();
        Check(GameSettings.DayIntro == introWas && PlayerPrefs.GetInt(GameSettings.DayIntroKey, -1) == (int)introWas && OnlyPillOn(phone, introWas),
            $"and back to {introWas}");
        // introTouched stays up: Restore puts it back once more at the end.
    }

    // A pad reaches the morning the way it reaches the picture's quality. First the routes the D-pad follows (the phone's
    // WireNavigation): down from the switch above onto Full, along the row to Short and Off, down to the quality's pills
    // (Short over Medium) and back up. Then the D-pad and A themselves, on a virtual pad added for this and removed after.
    IEnumerator PadToTheMorning(RecapPhone phone)
    {
        Button swap = phone.ContentButton(AboveMorningKey) ?? phone.ContentButton(SwapKey), full = phone.ContentButton("morning:Full"), shortPill = phone.ContentButton("morning:Short"),
            off = phone.ContentButton("morning:Off"), medium = phone.ContentButton("quality:Medium");
        if (swap == null || full == null || shortPill == null || off == null || medium == null)
        {
            Check(false, "the pad's routes: the switch, the morning's three pills and the quality's Medium are all on the phone");
            yield break;
        }
        Check(swap.navigation.selectOnDown == full && full.navigation.selectOnUp == swap && full.navigation.selectOnRight == shortPill
              && shortPill.navigation.selectOnLeft == full && shortPill.navigation.selectOnRight == off && off.navigation.selectOnRight == null
              && shortPill.navigation.selectOnDown == medium && medium.navigation.selectOnUp == shortPill,
            "the pad's routes: down from the switch onto Full, along to Short and Off, down to the quality's (Short over Medium), and back up");

        PlugInPad("Pause check pad (settings)");
        // The UI's D-pad steps come through PlayerInput's actions, which listen to the devices of the control scheme it last
        // switched to: a player's first press on a pad switches it; a pad made in code a moment ago may not have been yet (as
        // the recap phone's check found), so the check switches it, as that first press would.
        var playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null && !playerInput.devices.Contains(pad))
        {
            bool switched = playerInput.SwitchCurrentControlScheme(pad);
            Note($"PlayerInput switched to the check's pad ({(switched ? playerInput.currentControlScheme : "it wouldn't")})");
        }
        // One press up first makes the pad the device in use (the phone keeps a pad's selection only then, and up moves nothing
        // a slider would take as a change); then the pad starts on the switch above the row.
        yield return PadPress(GamepadButton.DpadUp);
        EventSystem events = EventSystem.current;
        Button from = phone.ContentButton(AboveMorningKey) ?? phone.ContentButton(SwapKey);
        if (events != null && from != null) events.SetSelectedGameObject(from.gameObject);
        yield return Frames(2);
        Check(PadInput.UsingPad && from != null && Selected() == from.gameObject, $"a pad, on the switch above the row (on {Name(Selected())})");
        yield return PadPress(GamepadButton.DpadDown);
        Button fullNow = phone.ContentButton("morning:Full");
        bool onFull = fullNow != null && Selected() == fullNow.gameObject;
        Check(onFull, $"the D-pad goes down onto Full (on {Name(Selected())}; the editor {(Application.isFocused ? "focused" : "NOT focused")})");
        Button shortNow = null;
        if (onFull)
        {
            yield return PadPress(GamepadButton.DpadRight);
            shortNow = phone.ContentButton("morning:Short");
            Check(shortNow != null && Selected() == shortNow.gameObject, $"...right onto Short (on {Name(Selected())})");
        }
        // A presses whatever is selected: only on Short, or a lost step would press something else (a switch, a quality).
        if (shortNow == null || Selected() != shortNow.gameObject)
        {
            Note("the pad isn't on Short: A isn't pressed, and the rest of the pad's steps are skipped (the routes above still hold)");
            InputSystem.RemoveDevice(pad);
            pad = null;
            yield return Frames(2);
            yield break;
        }
        yield return PadPress(GamepadButton.South);
        yield return Frames(2);
        GameSettings.Reload();
        Button shortAfter = phone.ContentButton("morning:Short");
        yield return Settle();   // 23 Oct: the pill's squish, and the ring that glided along the row
        Check(GameSettings.DayIntro == DayIntroMode.Short && OnlyPillOn(phone, DayIntroMode.Short) && shortAfter != null && Selected() == shortAfter.gameObject,
            $"...and A sets the morning to Short ({GameSettings.DayIntro}), the pad staying on it as the phone builds again (on {Name(Selected())})");
        Check(RingOn(phone, shortAfter != null ? shortAfter.gameObject : null, out string ringWhat),
            "the pad's gold ring glided along the row and ends exactly on Short: " + ringWhat);
        yield return Shot("14c-settings-the-morning-on-a-pad");
        InputSystem.RemoveDevice(pad);
        pad = null;
        yield return Frames(2);
    }

    // A switch row's switch is green when it's on (RecapPhone.SwitchRow paints its track the brand's green). A read of the phone's
    // look (23 Oct: its colour turns as its knob slides across), so it's counted (Looking).
    bool SwitchOn(RecapPhone phone, string key)
    {
        Looking("the switch " + key);
        return Green(phone.ContentButton(key));
    }

    // The morning's pill for this mode is green, and the other two aren't (RecapPhone.ChoiceRow). Counted as a read too.
    bool OnlyPillOn(RecapPhone phone, DayIntroMode mode)
    {
        Looking("the morning's pills");
        return Mornings.All(m => Green(phone.ContentButton("morning:" + m)) == (m == mode));
    }

    // 23 Oct 2026: waits until nothing on the phone moves (RecapPhone.Settled: no rise or drop, slide, rows arriving, squish, knob,
    // or the ring's glide), up to a second of real time, so what's read next is where things come to rest.
    IEnumerator Settle()
    {
        RecapPhone phone = pause != null ? pause.Phone : null;
        float until = Time.realtimeSinceStartup + 1f;
        while (phone != null && !phone.Settled && Time.realtimeSinceStartup < until) yield return null;
    }

    // A read of the phone's look (a colour, the ring, a photo): counted, and kept if the phone was still moving (the check at the
    // end: "nothing read a mid-motion position").
    void Looking(string what)
    {
        looks++;
        RecapPhone phone = pause != null ? pause.Phone : null;
        if (phone != null && !phone.Settled) lookedMoving.Add(what);
    }

    // A photo once the phone is still (23 Oct): PlayLab's own (its words checked as ever), after the phone's motion has ended.
    // With the phone away (the HUD's photos) nothing waits.
    IEnumerator Shot(string name)
    {
        yield return Settle();
        Looking("photo " + name);
        yield return Photo(name);
    }

    // The pad's gold ring sits exactly on its button once the phone is still: its middle on the button's middle and 9 canvas
    // units bigger (RecapPhone's RingPad, half either side), each within half a pixel (23 Oct: it glides there now).
    bool RingOn(RecapPhone phone, GameObject target, out string what)
    {
        Looking("the ring around " + Name(target));
        RectTransform ring = phone.FocusRing;
        if (target == null || ring == null || !ring.gameObject.activeInHierarchy)
        {
            what = $"no ring on screen, or nothing selected ({Name(target)})";
            return false;
        }
        Rect r = OnScreen(ring), b = OnScreen((RectTransform)target.transform);
        float margin = 9f * phone.Root.transform.lossyScale.x;
        float off = Vector2.Distance(r.center, b.center), dw = r.width - b.width - margin, dh = r.height - b.height - margin;
        what = $"{off:0.0} px from its middle, its margin {dw:+0.0;-0.0;0} px wide and {dh:+0.0;-0.0;0} px tall of {margin:0.0} px";
        return off <= .5f && Mathf.Abs(dw) <= .5f && Mathf.Abs(dh) <= .5f;
    }

    static bool Green(Button button)
    {
        var face = button != null ? button.targetGraphic as Image : null;
        if (face == null) return false;
        Color c = face.color, g = UiSkin.Brand;
        return Mathf.Abs(c.r - g.r) + Mathf.Abs(c.g - g.g) + Mathf.Abs(c.b - g.b) < .02f;
    }

    static GameObject Selected() => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    static string Name(GameObject go) => go != null ? go.name : "nothing";

    // The drinks close-up: Esc steps out of it, and the phone doesn't come up over it.
    IEnumerator NotOverACloseUp()
    {
        StationInteractable drinks = player.DrinksStation;
        if (drinks == null) { Note("no drinks station to try the close-up with"); yield break; }
        player.EnterStation(drinks);
        yield return Frames(4);
        if (!player.IsAtStation) { Note("the drinks close-up didn't open; skipped"); yield break; }
        Check(!pause.Open(), "the phone won't come up over a close-up (the drinks)");
        yield return Key(UnityEngine.InputSystem.Key.Escape);
        yield return Frames(3);
        if (player.IsAtStation) { Note("Esc didn't reach the close-up; left it directly"); player.ExitStation(); yield return Frames(3); }
        Check(!player.IsAtStation && !PausePhone.Paused, "Esc steps out of the close-up first, and the phone stays put away");
        yield return Seconds(.5f);
    }

    protected override void Restore()
    {
        if (PausePhone.Paused) pause.Resume();
        if (masterWas >= 0f) GameSettings.Master = masterWas;
        if (invertTouched) GameSettings.InvertY = invertWas;
        if (swapTouched) GameSettings.SwapBenchSticks = swapWas;
        if (introTouched) GameSettings.DayIntro = introWas;
        GameSettings.Save();
        if (Time.timeScale <= 0f && !RecapUI.Showing) Time.timeScale = 1f;
    }

    // ---------- helpers ----------

    // The view: the main camera's place, its turn and lens, and the Cinemachine camera that's live (blending or not).
    struct View
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public string live;
        public override string ToString() =>
            $"{live} at ({position.x:0.00}, {position.y:0.00}, {position.z:0.00}), pitch {rotation.eulerAngles.x:0.0}°, yaw {rotation.eulerAngles.y:0.0}°, fov {fov:0.0}";
    }

    static View ViewNow()
    {
        Camera cam = Camera.main;
        if (cam == null) return new View { live = "(no camera)" };
        var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        string live = brain != null && brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "(no live camera)";
        if (brain != null && brain.IsBlending) live += " (blending)";
        return new View { position = cam.transform.position, rotation = cam.transform.rotation, fov = cam.fieldOfView, live = live };
    }

    static bool Same(View a, View b) =>
        a.live == b.live && (a.position - b.position).sqrMagnitude < .0004f && Quaternion.Angle(a.rotation, b.rotation) < .5f
        && Mathf.Abs(a.fov - b.fov) < .1f;

    // A key pressed and let go on the keyboard the Game view reads.
    IEnumerator Key(UnityEngine.InputSystem.Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) yield break;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
    }

    static System.Collections.Generic.IEnumerable<CustomerBrain> Customers() =>
        FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Where(c => c != null && c.isActiveAndEnabled && !c.IsLeaving);

    static bool NoteUp()
    {
        NightCycle cycle = NightCycle.Instance;
        if (cycle == null) return false;
        var box = typeof(NightCycle).GetField("noteBox", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(cycle) as RectTransform;
        return box != null && box.gameObject.activeInHierarchy;
    }

    float SwayOf()
    {
        Transform swing = corners.SignRect != null ? corners.SignRect.Find("Swing") : null;
        return swing != null ? swing.localEulerAngles.z : 0f;
    }

    static bool HudText(ShopUI hud, string field)
    {
        var text = hud != null ? typeof(ShopUI).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hud) as TMPro.TMP_Text : null;
        return text != null && text.gameObject.activeInHierarchy;
    }

    float DayLengthSeconds() =>
        (float)(typeof(DayClock).GetField("dayLengthSeconds", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(clock) ?? 180f);

    // The lab moves the day on: what's left of it (the clock's own countdown).
    void SetRemaining(float seconds)
    {
        PropertyInfo left = typeof(DayClock).GetProperty("TimeRemaining", BindingFlags.Instance | BindingFlags.Public);
        left?.GetSetMethod(true)?.Invoke(clock, new object[] { Mathf.Max(0f, seconds) });
        Note($"the clock moved on: {seconds:0.0} s of the day left ({ShopUI.FormatHour(clock.CurrentHour)})");
    }

    static Rect OnScreen(RectTransform rect)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
