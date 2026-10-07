using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
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
// ---------------------------------------------------------------------------
public sealed class PauseCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Pause.Check";

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

        // ---------- the HUD's corners, by day ----------
        yield return Seconds(1.5f);
        Check(corners.Showing && corners.BottomShowing, "the corners are up by day: today's takings top right, the sign bottom left");
        Check(corners.SignWord == "OPEN" && !corners.LastOrders && !corners.SignClosed, $"the sign reads {corners.SignWord}");
        Check(corners.ClockLine.Contains(Weekdays.Label(clock.Day)), $"under it, the day and the time: \"{corners.ClockLine}\"");
        Check(corners.TakingsShown == "$" + clock.Earned, $"today's takings read {corners.TakingsShown} (today ${clock.Earned})");
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        Check(!HudText(hud, "moneyText") && !HudText(hud, "clockText") && !HudText(hud, "stockText"),
            "the old money, clock and stock lines are put away by day (the corners carry them)");
        RectTransform cash = corners.CashRect, sign = corners.SignRect;
        Rect cashOn = OnScreen(cash), signOn = OnScreen(sign);
        Check(cashOn.xMax > Screen.width * .8f && cashOn.yMax > Screen.height * .8f && signOn.xMin < Screen.width * .2f && signOn.yMin < Screen.height * .2f,
            $"the stack sits top right ({cashOn.xMin:0}-{cashOn.xMax:0} x {cashOn.yMin:0}-{cashOn.yMax:0}) and the sign bottom left " +
            $"({signOn.xMin:0}-{signOn.xMax:0} x {signOn.yMin:0}-{signOn.yMax:0}) of {Screen.width} x {Screen.height}");
        yield return Photo("01-the-corners-by-day");

        // A payment: the stack and the figure.
        int before = clock.Earned;
        ShopEconomy.Instance.AddMoney(30);
        clock.RecordPatronIncome(30);
        yield return Seconds(.15f);
        Check(corners.TakingsPunch > 1.01f || corners.TakingsShown != "$" + before, $"a payment punches the figure ({corners.TakingsPunch:0.00}x, {corners.TakingsShown})");
        yield return Photo("02-paid");
        yield return Seconds(1f);
        Check(corners.TakingsShown == "$" + clock.Earned && corners.BillShowing && corners.EdgesShown == Mathf.Min(10, clock.Earned / 24),
            $"...and lands on today's ${clock.Earned}, a bill on the stack and {corners.EdgesShown} edge(s) under it");

        // Low stock: a chip, bottom right.
        ShopInventory stock = ShopInventory.Instance;
        int cupsWas = stock != null ? stock.Cups : 0;
        if (stock != null)
        {
            int beansWas = stock.Beans;
            stock.SetStock(3, beansWas);
            yield return Seconds(.6f);
            Check(corners.CupsChipShowing && corners.ChipsText.Contains("cups"), $"low on cups: a chip, bottom right (\"{corners.ChipsText}\")");
            Rect chipsOn = OnScreen(corners.ChipsRect);
            Check(chipsOn.xMax > Screen.width * .8f && chipsOn.yMin < Screen.height * .3f, "...in the bottom right corner");
            yield return Photo("03-low-on-cups");
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
        SetRemaining(Mathf.Min(clock.TimeRemaining, lastOrdersAt * .7f));
        yield return Until(() => corners.LastOrders, 3f, "an hour before closing the sign turns to LAST ORDERS");
        yield return Seconds(1.2f);
        Check(corners.SignWord == "LAST ORDERS", $"...it reads \"{corners.SignWord}\"");
        Check(corners.VignetteAlpha > .05f, $"...and an amber glow pulses round the edge of the screen ({corners.VignetteAlpha:0.00})");
        yield return Photo("20-last-orders");
        yield return Moment("last orders", pressEsc: false, closeWith: "pad");

        // ---------- closing ----------
        SetRemaining(.5f);
        yield return Until(() => corners.SignClosed, 4f, "at closing the sign flips over to CLOSED");
        yield return Seconds(.35f);
        yield return Photo("30-the-sign-flips");
        yield return Seconds(1.2f);
        Check(corners.SignWord == "CLOSED", $"...it reads \"{corners.SignWord}\" once it has turned");
        yield return Photo("31-closed");
        bool up = pause.Open();
        Check(up, "the phone still comes up while the café finishes up (it isn't the recap yet)");
        yield return Frames(2);
        if (up) yield return Photo("32-phone-after-closing");
        if (PausePhone.Paused) { pause.Resume(); yield return Frames(2); }
    }

    // ---------- a moment: something running out, the phone up, nothing moves, the phone away, the day goes on ----------

    IEnumerator Moment(string when, bool pressEsc, string closeWith)
    {
        // Wait for someone in the café, to watch them hold still.
        yield return Await(() => Customers().Any(), 20f);
        CustomerBrain someone = Customers().FirstOrDefault();
        NightCycle.Note("A note that runs out in two seconds.", 2f);
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
        float sway = corners.SignRect != null ? SwayOf() : 0f, glow = corners.VignetteAlpha;
        View held = ViewNow();
        RecapPhone phone = pause.Phone;
        Check(phone.Root.activeInHierarchy && phone.Current == RecapPhone.App.Reviews && phone.LabelOf(phone.Current) == "Today"
            && string.Join(",", phone.VisibleApps) == "Reviews,Notes,Settings",
            $"{when}: it opens on Today, with Notes and Settings (tabs: {string.Join(", ", phone.VisibleApps.Select(a => phone.LabelOf(a)))})");
        yield return Frames(2);
        string today = phone.ContentText();
        Check(today.Contains("earned so far today") && today.Contains("In the till") && today.Contains("$" + clock.Earned),
            $"{when}: Today shows today's takings (${clock.Earned}) and what's in the till");
        yield return Photo($"1{(when == "early" ? 0 : when == "midday" ? 1 : 2)}-phone-{when.Replace(' ', '-')}");

        yield return Seconds(3f);
        Check(Mathf.Abs(clock.CurrentHour - hour) < .0001f && Mathf.Abs(clock.TimeRemaining - left) < .0001f,
            $"{when}: three seconds later the clock hasn't moved ({ShopUI.FormatHour(clock.CurrentHour)}; {Mathf.Abs(clock.TimeRemaining - left):0.0000} s of the day went)");
        Check((player.transform.position - ace).sqrMagnitude < 1e-6f, $"{when}: nor has Ace ({(player.transform.position - ace).magnitude:0.000} m)");
        if (someone != null)
            Check((someone.transform.position - at).sqrMagnitude < 1e-6f && Mathf.Abs(someone.PatienceFraction - patience) < 1e-5f,
                $"{when}: nor {someone.CustomerName}, whose patience is where it was ({patience:0.000}; moved {(someone.transform.position - at).magnitude:0.000} m)");
        Check(UiClock.Now - ui < .3f, $"{when}: the screen's own clock stood still ({UiClock.Now - ui:0.00} s of it passed)");
        Check(NoteUp() && Juice.PopsUp >= Mathf.Min(1, pops), $"{when}: the two-second note and the badge are still up");
        Check(Mathf.Abs(Mathf.DeltaAngle(SwayOf(), sway)) < .01f && Mathf.Abs(corners.VignetteAlpha - glow) < .01f,
            $"{when}: the sign's sway and the glow round the edge held too ({Mathf.DeltaAngle(sway, SwayOf()):0.000}°, {corners.VignetteAlpha - glow:0.000})");
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
        Check(!PausePhone.Paused && Time.timeScale > 0f && !UiClock.Held && !phone.Root.activeInHierarchy,
            $"{when}: {closeWith} puts it away and gives the game back");
        yield return Seconds(2.6f);
        Check(clock.CurrentHour > hour + .0001f, $"{when}: the day carries on ({ShopUI.FormatHour(hour)} to {ShopUI.FormatHour(clock.CurrentHour)})");
        Check(!NoteUp(), $"{when}: and the note runs out now, after the pause");
        if (pad != null) PadRelease();
    }

    // Today, Notes, Settings: switching, a slider and a switch that change the settings (put back), Quit asking again.
    IEnumerator TheApps()
    {
        if (!pause.Open()) { Check(false, "the phone comes up for the apps"); yield break; }
        yield return Frames(2);
        RecapPhone phone = pause.Phone;
        yield return Key(UnityEngine.InputSystem.Key.E);
        if (phone.Current != RecapPhone.App.Notes) { Note("E didn't reach the game; Notes opened directly."); phone.Open(RecapPhone.App.Notes); }
        yield return Frames(2);
        Check(phone.Current == RecapPhone.App.Notes && phone.ContentText().Contains("Notes"), "E opens Notes");
        yield return Photo("13-phone-notes");
        phone.Open(RecapPhone.App.Settings);
        yield return Frames(2);
        string settings = phone.ContentText();
        Check(settings.Contains("SOUND") && settings.Contains("CONTROLS") && settings.Contains("PICTURE") && settings.Contains("Quit"),
            "Settings: sound, controls, the picture, and Quit");
        yield return Photo("14-phone-settings");

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
        Button quit = phone.ContentButton("quit");
        Check(quit != null && phone.ContentText().Contains("isn't kept"), "Quit says plainly that the day so far isn't kept");
        if (quit != null)
        {
            quit.onClick.Invoke();   // once only: the second press would quit
            yield return Frames(2);
            Check(phone.QuitArmed && phone.ContentText().Contains("Press again to quit"), "a first press of Quit asks again, in red");
            phone.RevealKey("quit");   // scrolled to it, for the photo
            yield return Frames(2);
            yield return Photo("15-quit-asks-again");
            phone.Open(RecapPhone.App.Reviews);
            yield return Frames(2);
            Check(!phone.QuitArmed, "opening another app takes the question back");
        }
        pause.Resume();
        yield return Frames(3);
    }

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
