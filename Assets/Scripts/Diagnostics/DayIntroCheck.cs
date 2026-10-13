using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE DAY INTRO: A PLAY CHECK (12 Oct 2026; Cinematics & Camera)
//
// A lab session on a fresh Day 1 with the intro forced on (it is off in every other lab). The first morning plays by itself
// from the clock's own bell hook; the check watches it, then plays more mornings by hand (DayIntro.BeginForCheck), and
// photographs every shot in the pool at its best moments (the storyboard):
//
//   * Full: 6-8 s from its first frame to the controls coming back; the clock's time remaining doesn't move; every other
//     screen (each root canvas) is faded out while it plays and every canvas group is back at its alpha after; the bell
//     (day.open) rang exactly once, rung by the intro; nothing of the intro is left behind; Ace, the clock, the spawners
//     and the screen's clock are all free again;
//   * the picker: two free mornings never pick the same order; Day 1 plays the canonical order; a morning Grace is coming
//     early (Day 2) shows her house;
//   * the skip, from the first frame (the virtual pad's A, or DayIntro.RequestSkip when the pad's press can't arrive): the
//     controls back within 1 s, the player's camera back within 1 cm and 0.5°, the title still read for its 1.2 s;
//   * Short: 2-3 s, the title and the sting over the player's own camera, which never moves;
//   * Off: nothing plays and the clock rings its own bell; and with the lab's hand off, a lab session doesn't see it;
//   * the title block inside the 5% safe margins at 16:9, 16:10, 4:3 and 21:9 (laid out as the conversation probe computes
//     the canvas for each shape); every day's line under 90 characters;
//   * nothing logged an error.
//
// Fixit Fidget > Cinematics > Day intro - a check (lab, drives itself). Photos and report.txt go to Logs/DayIntro/check-<time>/.
// Nothing is saved in the scene; the lab's own save only; the player's setting and last order are put back.
// ---------------------------------------------------------------------------
public sealed class DayIntroCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.DayIntroCheck";

    protected override string Title => "Day intro - the morning's montage";
    protected override string Tag => "[Day intro check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Day intro check] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        // Before the scene's first Update: the clock's bell hook starts the first morning's intro by itself, Full, whatever
        // the player's setting says.
        DayIntro.ForceOn = true;
        DayIntro.ModeOverride = DayIntro.Mode.Full;
        var go = new GameObject("Day intro check (this Play session only)");
        go.AddComponent<DayIntroCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "DayIntro",
            $"check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const float Margin = DayIntro.SafeMargin;

    DayClock clock;
    Camera main;
    CinemachineBrain brain;
    CustomerSpawner spawner;
    bool spawnerWas = true, spawnerHeld;
    string lastOrderBefore;
    object settingBefore;
    bool settingRead;
    readonly HashSet<string> photographed = new HashSet<string>(StringComparer.Ordinal);
    readonly List<string> orders = new List<string>();

    sealed class Watched
    {
        public string run;
        public float seconds, timeRemainingDrift, cameraMoved, cameraTurned, cameraMovedDuring;
        public int hudOffenders = -1;
        public string hudOffenderNames = "";
        public bool titleSeen, titleHeldMinimum, landed;
        public float titleLongest;
        public int bellBefore, bellAfter;
        public string order = "";
        // The day's clock the moment the controls came back (the hold's end: the title may stay a while over the player's
        // own view with the day running under it, by design), and the title's tail after that.
        public float intoDayAtLanding = float.NaN, remainingAtLanding = float.NaN, tailAfterLanding;
    }

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && Camera.main != null, 30f, "the lab's scene is up");
        if (!lastWait) yield break;
        clock = DayClock.Instance;
        main = Camera.main;
        brain = main.GetComponent<CinemachineBrain>();
        spawner = FindAnyObjectByType<CustomerSpawner>();
        lastOrderBefore = PlayerPrefs.GetString(DayIntro.LastOrderKey, "");
        Note($"the screen {Screen.width} x {Screen.height}; the brain {(brain != null ? "found" : "MISSING")}");
        PlugInPad("Day intro check pad");

        // ---------- 1. the first morning, by itself ----------
        yield return Until(() => DayIntro.Playing || DayIntro.MorningsPlayed > 0, 5f,
            "the first morning's intro began by itself (the clock's bell hook; ForceOn in the lab)");
        Watched first = NewWatch("1-first-morning");
        first.bellBefore = Sfx.Requested("day.open") - (DayIntro.LastRangTheBell ? 1 : 0);   // it may have rung already in the frames before this check woke
        yield return WatchMorning(first, expectFull: true, before: null);
        CheckFull(first, expectCanonical: true);
        Check(DayIntro.LastMode == DayIntro.Mode.Full, $"{first.run}: it played in Full ({DayIntro.LastMode})");

        // From here on the day's people stay out of the pictures (the intro holds and gives back the spawner itself; this is on top).
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; spawnerHeld = true; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        yield return Seconds(.5f);

        // ---------- 2. two free mornings: never the same order ----------
        Watched second = NewWatch("2-free-morning");
        yield return PlayMorning(second, DayIntro.Mode.Full, 3, null, expectFull: true);
        Watched third = NewWatch("3-free-morning");
        yield return PlayMorning(third, DayIntro.Mode.Full, 3, null, expectFull: true);
        Check(second.order != third.order && second.order.Length > 0,
            $"no two consecutive mornings pick the same order ({second.order} then {third.order})");
        Check(second.order != first.order, $"a free morning's order differs from Day 1's canonical one ({first.order} then {second.order})");

        // ---------- 3. the rest of the pool, by name (the storyboard) ----------
        Watched fourth = NewWatch("4-pool-b");
        yield return PlayMorning(fourth, DayIntro.Mode.Full, 4, new[] { "grinder.burr", "bench.lamp", "bench.wake", "street.sign" }, expectFull: true);
        Watched grace = NewWatch("5-grace-morning");
        yield return PlayMorning(grace, DayIntro.Mode.Full, 2, new[] { "grinder.beans", "bench.screw", "house.grace", "street.front" }, expectFull: true);
        string tied = DayIntro.TiedShotFor(2);
        bool graceDoor = HomeDoor.Find("home.grace") != null;
        Check(!graceDoor || tied == "house.grace", $"Day 2 (Grace comes in early) ties her house to the morning ({tied ?? "none"}{(graceDoor ? "" : "; her door isn't in this scene")})");
        Check(DayIntro.TiedShotFor(1) == null && DayIntro.TiedShotFor(3) == null, "Days 1 and 3 tie no shot");
        var missing = DayIntro.PoolIds.Where(id => !photographed.Contains(id)).ToList();
        Check(missing.Count == 0, $"every shot in the pool played and was photographed ({photographed.Count} of {DayIntro.PoolIds.Length}" +
                                  (missing.Count > 0 ? "; never came: " + string.Join(", ", missing) : "") + ")");

        // ---------- 4. the skip from the first frame ----------
        Watched skipped = NewWatch("6-skipped");
        yield return SkipMorning(skipped);

        // ---------- 5. Short ----------
        Watched brief = NewWatch("7-short");
        yield return PlayMorning(brief, DayIntro.Mode.Short, 6, null, expectFull: false);
        Check(brief.seconds >= 2f && brief.seconds <= 3f, $"{brief.run}: Short held the controls 2-3 s ({brief.seconds:0.00} s)");
        Check(brief.cameraMovedDuring <= .01f, $"{brief.run}: the player's camera never moved in Short ({brief.cameraMovedDuring * 100f:0.0} cm at most)");
        Check(brief.titleSeen && brief.bellAfter == brief.bellBefore + 1, $"{brief.run}: the title showed and the bell rang once ({brief.bellAfter - brief.bellBefore})");

        // ---------- 6. Off, and the other labs ----------
        yield return OffAndTheLabs();

        // ---------- 7. the title's layout, the words ----------
        Shapes();
        for (int d = 1; d <= 8; d++)
        {
            string line = DayIntro.LineForDay(d);
            Check(!string.IsNullOrWhiteSpace(line) && line.Length <= 90, $"Day {d}'s line is under 90 characters ({line.Length}): \"{line}\"");
        }
        Note("The intro leaves the HUD's sign at OPEN without a flip (HudCorners turns it without animating on a new morning): UI's to time, if wanted.");
    }

    Watched NewWatch(string run) => new Watched { run = run, bellBefore = Sfx.Requested("day.open") };

    // ---------- a morning by hand ----------

    IEnumerator PlayMorning(Watched w, DayIntro.Mode mode, int day, string[] order, bool expectFull)
    {
        yield return Until(() => DayIntro.Instance == null, 6f, $"{w.run}: the morning before has put everything away");
        Pose before = new Pose(main.transform.position, main.transform.rotation);
        var screensBefore = SnapshotScreens();
        float remainingBefore = clock.TimeRemaining, intoDayBefore = clock.SecondsIntoDay;
        w.bellBefore = Sfx.Requested("day.open");
        bool began = DayIntro.BeginForCheck(mode, day, order);
        Check(began, $"{w.run}: a {mode} morning for Day {day} began{(order != null ? " (" + string.Join("|", order) + ")" : "")}");
        if (!began) yield break;
        yield return WatchMorning(w, expectFull, before);
        // Across the hold: from the call that began it to the controls coming back. The title stays a while over the player's
        // own view after that, with the day running under it (the player has the controls: that is the game, not the intro).
        float intoDayAfter = float.IsNaN(w.intoDayAtLanding) ? clock.SecondsIntoDay : w.intoDayAtLanding;
        float remainingAfter = float.IsNaN(w.remainingAtLanding) ? clock.TimeRemaining : w.remainingAtLanding;
        Check(Mathf.Abs(intoDayAfter - intoDayBefore) <= .3f,
            $"{w.run}: the day's seconds didn't run on under the intro ({intoDayAfter - intoDayBefore:+0.00;-0.00} s from the call to the controls coming back)");
        Check(Mathf.Abs(remainingAfter - remainingBefore) <= .1f,     // a few frames: the clock is back on before this watcher's turn in the frame
            $"{w.run}: the time remaining is what it was when the controls came back ({remainingBefore:0.00} -> {remainingAfter:0.00} s)");
        if (!float.IsNaN(w.intoDayAtLanding))
            Note($"{w.run}: the title stayed {w.tailAfterLanding:0.00} s after the controls came back, the day running under it by {clock.SecondsIntoDay - w.intoDayAtLanding:0.00} s " +
                 "(by design: Ace can already move; the clock, the spawners and the HUD are back).");
        if (expectFull)
        {
            CheckFull(w, expectCanonical: false);
            CheckScreensBack(w, screensBefore);
        }
        w.order = DayIntro.LastOrder;
        orders.Add(w.order);
    }

    // What every Full morning must do.
    void CheckFull(Watched w, bool expectCanonical)
    {
        w.order = DayIntro.LastOrder;
        Note($"{w.run}: {w.order} ({w.seconds:0.00} s)");
        Check(w.seconds >= 6f && w.seconds <= 8f, $"{w.run}: 6-8 s from its first frame to the controls coming back ({w.seconds:0.00} s)");
        Check(w.timeRemainingDrift <= .001f, $"{w.run}: the clock did not advance while it played (time remaining moved {w.timeRemainingDrift:0.000} s)");
        Check(w.hudOffenders == 0, $"{w.run}: every other screen was faded out while it played" + (w.hudOffenders > 0 ? $" ({w.hudOffenders} showing: {w.hudOffenderNames})" : w.hudOffenders < 0 ? " (not sampled)" : ""));
        Check(w.titleSeen && w.titleLongest >= DayIntro.TitleMinimumSeconds, $"{w.run}: the title came up and stayed its minimum ({w.titleLongest:0.00} s of {DayIntro.TitleMinimumSeconds:0.0})");
        Check(w.bellAfter == w.bellBefore + 1, $"{w.run}: the bell rang once, by the intro ({w.bellAfter - w.bellBefore} ring(s); the intro says {(DayIntro.LastRangTheBell ? "it rang it" : "it did not")})");
        Check(w.landed && !PlayerMovement.Held && !UiClock.Held && clock.enabled, $"{w.run}: afterwards Ace, the screen's clock and the day's clock are free");
        Check(GameObject.Find("Day intro (while playing)") == null && GameObject.Find("The phone (back up)") == null && GameObject.Find("The cup") == null,
            $"{w.run}: nothing of the intro is left behind");
        if (expectCanonical)
            Check(w.order.StartsWith("grinder.beans|bench.screw", StringComparison.Ordinal) && w.order.EndsWith("street.front", StringComparison.Ordinal),
                $"{w.run}: Day 1 plays the canonical order, ending on the storefront's name ({w.order})");
        if (w.cameraMoved >= 0f)
            Check(w.cameraMoved <= .01f && w.cameraTurned <= .5f,
                $"{w.run}: the player's camera came back where it was ({w.cameraMoved * 100f:0.0} cm, {w.cameraTurned:0.00}°)");
    }

    // Watches a morning from now until the intro has put itself away (the title gone): photos at each shot's best moments,
    // the clock sampled, the screens sampled once, the title timed, the camera measured after it lands.
    IEnumerator WatchMorning(Watched w, bool expectFull, Pose? before)
    {
        float started = Time.realtimeSinceStartup;
        string lastShot = "";
        var momentsDone = new HashSet<int>();
        bool pullPhoto = false, titlePhoto = false, hudSampled = false;
        float landedAt = -1f;
        float remainingAtStart = clock.TimeRemaining;
        Pose atStart = new Pose(main.transform.position, main.transform.rotation);
        w.cameraMoved = -1f;
        while (DayIntro.Instance != null)
        {
            DayIntro intro = DayIntro.Instance;
            float now = Time.realtimeSinceStartup - started;
            if (DayIntro.Playing)
            {
                w.timeRemainingDrift = Mathf.Max(w.timeRemainingDrift, Mathf.Abs(clock.TimeRemaining - remainingAtStart));
                if (!expectFull) w.cameraMovedDuring = Mathf.Max(w.cameraMovedDuring, Vector3.Distance(main.transform.position, atStart.position));
                string id = intro.ShotId;
                if (id.Length > 0 && id != lastShot) { lastShot = id; momentsDone.Clear(); }
                if (id.Length > 0)
                {
                    IReadOnlyList<float> moments = intro.ShotMoments;
                    for (int j = 0; j < moments.Count; j++)
                    {
                        if (momentsDone.Contains(j) || intro.ShotTime < moments[j] * intro.ShotLength) continue;
                        momentsDone.Add(j);
                        yield return Photo1080($"{w.run}-{intro.ShotIndex + 1}-{id.Replace('.', '-')}-{(char)('a' + j)}");
                        photographed.Add(id);
                        break;
                    }
                }
                if (expectFull && !hudSampled && now >= 1f)
                {
                    hudSampled = true;
                    w.hudOffenders = ScreensShowing(out w.hudOffenderNames);
                }
                if (intro.PullingOut && !pullPhoto)
                {
                    // A third of a second into the pull-out: the blend half way to the player's view, the title sliding in.
                    pullPhoto = true;
                    yield return Seconds(.3f);
                    yield return Photo1080($"{w.run}-pull-out");
                }
            }
            else if (landedAt < 0f)
            {
                landedAt = Time.realtimeSinceStartup;
                w.landed = true;
                w.seconds = DayIntro.LastRunSeconds;
                w.bellAfter = Sfx.Requested("day.open");
                w.intoDayAtLanding = clock.SecondsIntoDay;
                w.remainingAtLanding = clock.TimeRemaining;
            }
            if (intro != null && intro.TitleShowing)
            {
                w.titleSeen = true;
                w.titleLongest = Mathf.Max(w.titleLongest, intro.TitleShownFor);
                if (!titlePhoto && intro.TitleShownFor >= .5f)
                {
                    titlePhoto = true;
                    yield return Photo1080($"{w.run}-title");
                }
            }
            if (landedAt >= 0f && w.cameraMoved < 0f && Time.realtimeSinceStartup - landedAt >= .35f && before.HasValue)
            {
                w.cameraMoved = Vector3.Distance(main.transform.position, before.Value.position);
                w.cameraTurned = Quaternion.Angle(main.transform.rotation, before.Value.rotation);
            }
            if (now > 20f) { Check(false, $"{w.run}: the intro put itself away within 20 s"); break; }
            yield return null;
        }
        if (landedAt >= 0f) w.tailAfterLanding = Time.realtimeSinceStartup - landedAt;
        if (landedAt < 0f)
        {
            w.landed = !DayIntro.Playing;
            w.seconds = DayIntro.LastRunSeconds;
            w.bellAfter = Sfx.Requested("day.open");
            w.intoDayAtLanding = clock.SecondsIntoDay;
            w.remainingAtLanding = clock.TimeRemaining;
        }
        // The intro destroys its objects as it puts itself away, and Unity takes them at the end of that frame: a frame later,
        // anything still there is really left behind.
        yield return null;
        yield return null;
        if (w.cameraMoved < 0f && before.HasValue)
        {
            w.cameraMoved = Vector3.Distance(main.transform.position, before.Value.position);
            w.cameraTurned = Quaternion.Angle(main.transform.rotation, before.Value.rotation);
        }
        w.titleHeldMinimum = w.titleLongest >= DayIntro.TitleMinimumSeconds;
    }

    // ---------- the skip ----------

    IEnumerator SkipMorning(Watched w)
    {
        yield return Until(() => DayIntro.Instance == null, 6f, $"{w.run}: the morning before has put everything away");
        Pose before = new Pose(main.transform.position, main.transform.rotation);
        w.bellBefore = Sfx.Requested("day.open");
        bool began = DayIntro.BeginForCheck(DayIntro.Mode.Full, 5, null);
        Check(began, $"{w.run}: a Full morning began");
        if (!began) yield break;
        yield return null;   // the frame it began in never counts a press
        float pressed = Time.realtimeSinceStartup;
        yield return Photo1080($"{w.run}-first-frame");
        yield return PadPress(GamepadButton.South);
        yield return Await(() => DayIntro.Instance == null || DayIntro.Instance.SkipRequested, .3f);
        bool byPad = lastWait;
        if (!byPad)
        {
            Note($"{w.run}: the virtual pad's A didn't arrive (Unity not in front?): skipped by DayIntro.RequestSkip instead");
            pressed = Time.realtimeSinceStartup;
            DayIntro.RequestSkip();
        }
        yield return Await(() => !DayIntro.Playing, 2f);
        float took = Time.realtimeSinceStartup - pressed;
        Check(lastWait && took <= 1f, $"{w.run}: the skip ends it within 1 s ({took:0.00} s, {(byPad ? "the pad's A" : "by request")})");
        Check(DayIntro.LastSkipped, $"{w.run}: the intro counts itself skipped");
        yield return WatchMorning(w, expectFull: true, before: before);
        Check(w.cameraMoved <= .01f && w.cameraTurned <= .5f,
            $"{w.run}: after the skip the player's camera is back where it was ({w.cameraMoved * 100f:0.0} cm, {w.cameraTurned:0.00}°)");
        Check(w.titleSeen && w.titleLongest >= DayIntro.TitleMinimumSeconds - .05f,
            $"{w.run}: the title still showed for its minimum ({w.titleLongest:0.00} s)");
        Check(w.bellAfter == w.bellBefore + 1, $"{w.run}: the bell still rang once ({w.bellAfter - w.bellBefore})");
        Check(!PlayerMovement.Held && !UiClock.Held && clock.enabled, $"{w.run}: Ace, the screen's clock and the day's clock are free again");
    }

    // ---------- Off, and the other labs ----------

    IEnumerator OffAndTheLabs()
    {
        yield return Until(() => DayIntro.Instance == null, 6f, "off: the morning before has put everything away");
        // The player's own setting, set to Off for a moment (the lab's override off), then put back.
        bool found = TryReadSetting(out settingBefore);
        settingRead = found;
        Check(found, "the player's setting GameSettings.DayIntro is there to read (the UI lead's row)");
        if (found)
        {
            bool set = TrySetSetting("Off");
            DayIntro.ModeOverride = null;
            Check(set && DayIntro.ModeNow() == DayIntro.Mode.Off, $"the setting Off reads as Off ({DayIntro.ModeNow()})");
            int bells = Sfx.Requested("day.open");
            bool began = DayIntro.TryBegin(clock);
            yield return Seconds(.6f);
            Check(!began && !DayIntro.Playing && DayIntro.Instance == null, "Off: the clock's call starts nothing");
            Check(clock.enabled && !PlayerMovement.Held, "Off: the clock keeps running and Ace is free");
            Note($"Off: day.open requested {Sfx.Requested("day.open") - bells} time(s) by the call itself (the clock rings it in its own Update, not here)");
            TrySetSetting(settingBefore != null ? settingBefore.ToString() : "Full");
            DayIntro.ModeOverride = DayIntro.Mode.Full;
        }
        // The other labs: a lab session with no hand on it doesn't see the intro.
        DayIntro.ForceOn = false;
        Check(CafeLab.Active && !DayIntro.WouldPlay, "a lab session that didn't ask for it gets no intro (CafeLab.Active: WouldPlay is false)");
        DayIntro.ForceOn = true;
        Check(DayIntro.WouldPlay, "...and with the lab's hand on, it would play");
    }

    // GameSettings.DayIntro, read and written by name (a property or a field; the enum's type is whatever the UI lead declared).
    static bool TryReadSetting(out object value)
    {
        value = null;
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = typeof(GameSettings).GetProperty("DayIntro", Any);
        if (property != null) { value = property.GetValue(null); return value != null; }
        FieldInfo field = typeof(GameSettings).GetField("DayIntro", Any);
        if (field != null) { value = field.GetValue(null); return value != null; }
        return false;
    }

    static bool TrySetSetting(string name)
    {
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            PropertyInfo property = typeof(GameSettings).GetProperty("DayIntro", Any);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, Enum.Parse(property.PropertyType, name, true));
                return true;
            }
            FieldInfo field = typeof(GameSettings).GetField("DayIntro", Any);
            if (field != null)
            {
                field.SetValue(null, Enum.Parse(field.FieldType, name, true));
                return true;
            }
        }
        catch (Exception) { }
        return false;
    }

    // ---------- the screens ----------

    // Every root canvas that isn't the intro's own, the night's or the pad cursor's: how many are showing (no group, or a group above 0.02).
    static int ScreensShowing(out string names)
    {
        int showing = 0;
        var list = new List<string>();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.isRootCanvas || !c.enabled) continue;
            if (c.GetComponentInParent<DayIntro>(true) != null || c.GetComponentInParent<NightCycle>(true) != null || c.GetComponentInParent<PadCursor>(true) != null) continue;
            CanvasGroup group = c.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= .02f) continue;
            showing++;
            if (list.Count < 6) list.Add(c.name);
        }
        names = string.Join(", ", list);
        return showing;
    }

    List<(Canvas canvas, bool hadGroup, float alpha)> SnapshotScreens()
    {
        var snapshot = new List<(Canvas, bool, float)>();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.isRootCanvas) continue;
            if (c.GetComponentInParent<DayIntro>(true) != null) continue;
            CanvasGroup group = c.GetComponent<CanvasGroup>();
            snapshot.Add((c, group != null, group != null ? group.alpha : 1f));
        }
        return snapshot;
    }

    void CheckScreensBack(Watched w, List<(Canvas canvas, bool hadGroup, float alpha)> before)
    {
        var wrong = new List<string>();
        foreach (var (canvas, hadGroup, alpha) in before)
        {
            if (canvas == null) continue;
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if ((group != null) != hadGroup) { wrong.Add($"{canvas.name}: {(group != null ? "a group was left on it" : "its group is gone")}"); continue; }
            if (group != null && Mathf.Abs(group.alpha - alpha) > .01f) wrong.Add($"{canvas.name}: alpha {alpha:0.00} -> {group.alpha:0.00}");
        }
        Check(wrong.Count == 0, $"{w.run}: every screen's canvas group is back as it was ({before.Count} canvases" + (wrong.Count > 0 ? "; " + string.Join("; ", wrong) : "") + ")");
    }

    // ---------- the title's layout ----------

    // The title block on other shapes of screen: the canvas scaler keeps the screen's area near 1920 x 1080's (the conversation
    // probe computes the same), and the block is centred at the same share of the height.
    void Shapes()
    {
        Vector2 size = DayIntro.LastTitleSize;
        Check(size.x > 0f && size.y > 0f, $"the title block was laid out ({size.x:0} x {size.y:0} reference px)");
        var shapes = new (string name, float aspect)[] { ("16:9", 16f / 9f), ("16:10", 1.6f), ("4:3", 4f / 3f), ("21:9", 21f / 9f) };
        foreach (var (shape, aspect) in shapes)
        {
            var canvas = new Vector2(Mathf.Sqrt(aspect * 1920f * 1080f), Mathf.Sqrt(1920f * 1080f / aspect));
            Rect box = DayIntro.TitleBoxIfTheScreenWere(canvas);
            bool inside = box.xMin >= canvas.x * Margin && box.xMax <= canvas.x * (1f - Margin) && box.yMin >= canvas.y * Margin && box.yMax <= canvas.y * (1f - Margin);
            Check(inside, $"the title on a {shape} screen ({canvas.x:0} x {canvas.y:0}): inside the 5% margins (x {box.xMin:0}-{box.xMax:0}, y {box.yMin:0}-{box.yMax:0})");
        }
    }

    // ---------- photos at 1920 x 1080 (as the Day 1 guide's check takes them) ----------

    IEnumerator Photo1080(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        Texture2D save = shot;
        if (shot.width != 1920 || shot.height != 1080)
        {
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(shot, target);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = target;
            save = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            save.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            save.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(target);
        }
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), save.EncodeToJPG(90));
        if (save != shot) Destroy(save);
        Destroy(shot);
    }

    protected override void Restore()
    {
        if (DayIntro.Instance != null) DayIntro.RequestSkip();
        DayIntro.ForceOn = false;
        DayIntro.ModeOverride = null;
        if (settingRead && settingBefore != null) TrySetSetting(settingBefore.ToString());
        if (spawnerHeld && spawner != null) spawner.enabled = spawnerWas;
        if (lastOrderBefore != null)
        {
            if (lastOrderBefore.Length == 0) PlayerPrefs.DeleteKey(DayIntro.LastOrderKey);
            else PlayerPrefs.SetString(DayIntro.LastOrderKey, lastOrderBefore);
            PlayerPrefs.Save();
        }
#if UNITY_EDITOR
        // Play mode ends by itself once the report is written (the editor's next update, after this frame's Finish).
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
