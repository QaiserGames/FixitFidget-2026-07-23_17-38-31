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
// THE DAY INTRO: A PLAY CHECK (12 Oct 2026; Cinematics & Camera. Intro v2, 14 Oct: the cinematics spec §4.1.9)
//
// A lab session on a fresh Day 1 with the intro forced on (it is off in every other lab). The first morning plays by itself
// from the clock's own bell hook; the check watches it, then plays more mornings by hand (DayIntro.BeginForCheck), and
// photographs every shot in the pool at its best moments (the storyboard, for Art Direction's eye):
//
//   * Day 1 plays its fixed order: street.row | bench.ready | drinks.pour | street.front;
//   * free mornings (Days 2, 3, 4, consecutive): Day 2 (Grace comes in early) ties her house to slot 2; Day 3 shows no Grace
//     (one morning in six); consecutive mornings differ in at least two slots and never repeat a recent order;
//   * the storyboard, by name: Day 3 street.storefront|counter.ready|neighbour.front|street.sign, Day 4
//     street.row|cafe.lights|patio.flower|street.front (street.across and car.pass until 15 Oct: both parked, DayIntro.
//     ParkedIds), Day 5 street.row|drinks.pour|patio.flower|street.sign, Day 2 (Grace)
//     street.row|bench.ready|house.grace|street.front, and neighbours.row in a slot-2 run; a moving subject that isn't in
//     reach (nobody on the pavement) retries once after 3 s, then is noted, not failed;
//   * every frame of every shot: (a) the camera never nearer its subject than the shot's least (Wide 4 m, Mid 1.2 m, the
//     dispenser 0.6 m); at each photo moment (b) the subject inside the middle 80% of a 16:9 frame, (c) nothing hides it,
//     (d) the camera isn't inside anything, (e) in a shot that isn't Grace's, her door isn't in frame nearer than 15 m;
//   * the picker, thirty mornings simulated with DayIntro.Plan: Day 1 canonical; no order repeated within six mornings;
//     consecutive mornings differ in two slots or more; Grace in at most one of any six and never Day 1 (tied on Days 2, 3
//     and 9 she shows on 2 and 9 only); every untied shot comes at least once; each tie, forced, lands in slot 2 or is noted
//     "tie not built";
//   * kept from 12 Oct: Full is 6-8 s from its first frame to the controls coming back; the clock doesn't advance; every
//     other screen is faded out while it plays and back after; the bell (day.open) rings exactly once; nothing of the intro is
//     left behind; Ace, the clock, the spawners and the screen's clock are free again; the skip from the first frame (back
//     within 1 s, the player's camera within 1 cm and 0.5°, the title still read for its 1.2 s); Short 2-3 s; Off; the title
//     inside the 5% margins at 16:9, 16:10, 4:3 and 21:9; every day's line under 90 characters. Added: the café's lights are
//     back at their intensity after cafe.lights; each shot's worst frame is logged (over 100 ms at a cut is noted).
//
// Fixit Fidget > Cinematics > Day intro - a check (lab, drives itself). Photos and report.txt go to Logs/DayIntro/check-<time>/.
// Nothing is saved in the scene; the lab's own save only; the player's setting and the intro's history are put back.
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
    static readonly string Canonical = string.Join("|", DayIntro.Day1Order);
    static readonly string[] Dynamic = { "neighbour.front", "neighbours.row" };

    DayClock clock;
    Camera main;
    CinemachineBrain brain;
    CustomerSpawner spawner;
    HomeDoor graceDoor;
    bool spawnerWas = true, spawnerHeld;
    string lastOrderBefore, historyBefore;
    object settingBefore;
    bool settingRead;
    readonly HashSet<string> photographed = new HashSet<string>(StringComparer.Ordinal);
    readonly HashSet<string> begunThisRun = new HashSet<string>(StringComparer.Ordinal);

    // What each shot did across the session (every frame it played): the nearest the camera came to its subject, the worst
    // frame, and its checks at the photo moments.
    sealed class ShotRecord
    {
        public string id;
        public float least = float.PositiveInfinity, minimum, worstFrame, worstCut;
        public int frames;
        public readonly List<string> problems = new List<string>();
        public bool grace, composition;
    }
    readonly Dictionary<string, ShotRecord> records = new Dictionary<string, ShotRecord>(StringComparer.Ordinal);

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
        graceDoor = HomeDoor.Find("home.grace");
        lastOrderBefore = PlayerPrefs.GetString(DayIntro.LastOrderKey, "");
        historyBefore = PlayerPrefs.GetString(DayIntro.HistoryKey, "");
        DayIntro.ShotBegan += Began;
        Note($"the screen {Screen.width} x {Screen.height}; the brain {(brain != null ? "found" : "MISSING")}; Grace's door {(graceDoor != null ? "found" : "not in this scene")}");
        PlugInPad("Day intro check pad");

        // ---------- 1. the first morning, by itself (Day 1: the fixed order) ----------
        yield return Until(() => DayIntro.Playing || DayIntro.MorningsPlayed > 0, 5f,
            "the first morning's intro began by itself (the clock's bell hook; ForceOn in the lab)");
        Watched first = NewWatch("1-first-morning");
        first.bellBefore = Sfx.Requested("day.open") - (DayIntro.LastRangTheBell ? 1 : 0);   // it may have rung already in the frames before this check woke
        yield return WatchMorning(first, expectFull: true, before: null);
        CheckFull(first);
        Check(DayIntro.LastMode == DayIntro.Mode.Full, $"{first.run}: it played in Full ({DayIntro.LastMode})");
        Check(first.order == Canonical, $"{first.run}: Day 1 plays its fixed order, the street, the bench, the drinks, the café's name ({first.order}; wanted {Canonical})");

        // From here on the day's people stay out of the pictures (the intro holds and gives back the spawner itself; this is on top).
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; spawnerHeld = true; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        yield return Seconds(.5f);

        // ---------- 2. free mornings, Days 2-4 in a row: the tie, Grace's ration, the yesterday rule ----------
        DayIntro.TieToday tie2 = DayIntro.TodaysTie(2);
        Note($"Day 2's tie: {tie2.kind}{(tie2.who != null ? " (" + tie2.who.PersistentId + ")" : "")} {tie2.how}");
        Watched day2 = NewWatch("2-free-day-2");
        yield return PlayMorning(day2, DayIntro.Mode.Full, 2, null, expectFull: true);
        bool graceTied = tie2.kind == DayIntro.Tie.Regular && tie2.grace;
        Check(!graceTied || graceDoor == null || day2.order.Split('|').Contains("house.grace"),
            $"Day 2 (Grace comes in early: the story morning) shows her front in slot 2 ({day2.order}{(graceTied ? "" : "; Grace isn't tied on Day 2 in this data")})");
        Watched day3 = NewWatch("3-free-day-3");
        yield return PlayMorning(day3, DayIntro.Mode.Full, 3, null, expectFull: true);
        Check(!day3.order.Split('|').Contains("house.grace"), $"Day 3 shows no Grace (one morning in six; Days spec §8) ({day3.order})");
        Watched day4 = NewWatch("4-free-day-4");
        yield return PlayMorning(day4, DayIntro.Mode.Full, 4, null, expectFull: true);
        Check(Differ(day2.order, day3.order) >= 2 && Differ(day3.order, day4.order) >= 2,
            $"consecutive mornings differ in at least two slots ({day2.order} / {day3.order} / {day4.order})");
        Check(day2.order != day3.order && day3.order != day4.order && day2.order != day4.order, "no free morning repeats a recent order");
        Check(DayIntro.TodaysTie(1).kind == DayIntro.Tie.None, "Day 1 is never tied (its order is fixed)");

        // ---------- 3. the storyboard, by name ----------
        var storyboard = new (int day, string[] order)[]
        {
            (3, new[] { "street.storefront", "counter.ready", "neighbour.front", "street.sign" }),
            (4, new[] { "street.row", "cafe.lights", "patio.flower", "street.front" }),
            (5, new[] { "street.row", "drinks.pour", "patio.flower", "street.sign" }),
            (2, new[] { "street.row", "bench.ready", "house.grace", "street.front" }),
            (6, new[] { "street.storefront", "counter.ready", "neighbours.row", "street.front" }),
        };
        int board = 0;
        foreach (var (day, order) in storyboard)
        {
            board++;
            List<float> lightsBefore = order.Contains("cafe.lights") ? CafeLightLevels() : null;
            Watched w = NewWatch($"5-board-{board}");
            yield return PlayMorning(w, DayIntro.Mode.Full, day, order, expectFull: true);
            string missing = order.FirstOrDefault(id => Dynamic.Contains(id) && !begunThisRun.Contains(id));
            if (missing != null)
            {
                Note($"{w.run}: {missing} found nobody in reach; once more after 3 s");
                yield return Seconds(3f);
                Watched again = NewWatch($"5-board-{board}-again");
                yield return PlayMorning(again, DayIntro.Mode.Full, day, order, expectFull: true);
                if (!begunThisRun.Contains(missing)) Note($"{again.run}: {missing} found nobody in reach twice: noted, not failed (its slot redraws in a real morning)");
            }
            if (lightsBefore != null)
            {
                List<float> after = CafeLightLevels();
                bool same = lightsBefore.Count == after.Count && !lightsBefore.Where((v, i) => Mathf.Abs(v - after[i]) > .001f).Any();
                Check(same, $"{w.run}: the café's own lights are back at their intensity after cafe.lights ({after.Count} lights)");
            }
        }
        var never = DayIntro.PoolIds.Where(id => !photographed.Contains(id)).ToList();
        var neverStatic = never.Where(id => !Dynamic.Contains(id)).ToList();
        Check(neverStatic.Count == 0, $"every shot in the pool that doesn't wait on a passer-by played and was photographed ({photographed.Count} of {DayIntro.PoolIds.Length}" +
                                      (neverStatic.Count > 0 ? "; never came: " + string.Join(", ", neverStatic) : "") + ")");
        if (never.Count > neverStatic.Count) Note("never caught (nobody in reach at its cut, twice): " + string.Join(", ", never.Except(neverStatic)));

        // ---------- 4. every shot, every frame: (a)-(e) ----------
        JudgeShots();

        // ---------- 5. the picker: thirty mornings ----------
        SimulateThirtyMornings();
        ForceEachTie();

        // ---------- 6. the skip from the first frame ----------
        Watched skipped = NewWatch("6-skipped");
        yield return SkipMorning(skipped);

        // ---------- 7. Short ----------
        Watched brief = NewWatch("7-short");
        yield return PlayMorning(brief, DayIntro.Mode.Short, 7, null, expectFull: false);
        Check(brief.seconds >= 2f && brief.seconds <= 3f, $"{brief.run}: Short held the controls 2-3 s ({brief.seconds:0.00} s)");
        Check(brief.cameraMovedDuring <= .01f, $"{brief.run}: the player's camera never moved in Short ({brief.cameraMovedDuring * 100f:0.0} cm at most)");
        Check(brief.titleSeen && brief.bellAfter == brief.bellBefore + 1, $"{brief.run}: the title showed and the bell rang once ({brief.bellAfter - brief.bellBefore})");

        // ---------- 8. Off, and the other labs ----------
        yield return OffAndTheLabs();

        // ---------- 9. the title's layout, the words ----------
        Shapes();
        for (int d = 1; d <= 8; d++)
        {
            string line = DayIntro.LineForDay(d);
            Check(!string.IsNullOrWhiteSpace(line) && line.Length <= 90, $"Day {d}'s line is under 90 characters ({line.Length}): \"{line}\"");
        }
        Note("The intro leaves the HUD's sign at OPEN without a flip (HudCorners turns it without animating on a new morning): UI's to time, if wanted.");
    }

    void Began(string id) => begunThisRun.Add(id);

    Watched NewWatch(string run)
    {
        begunThisRun.Clear();
        return new Watched { run = run, bellBefore = Sfx.Requested("day.open") };
    }

    static int Differ(string a, string b)
    {
        string[] x = a.Split('|'), y = b.Split('|');
        int n = Mathf.Max(x.Length, y.Length), differ = 0;
        for (int i = 0; i < n; i++) if (i >= x.Length || i >= y.Length || x[i] != y[i]) differ++;
        return differ;
    }

    List<float> CafeLightLevels()
    {
        var list = new List<float>();
        CafeDaylight daylight = FindAnyObjectByType<CafeDaylight>();
        if (daylight != null && daylight.warmLights != null)
            foreach (Light l in daylight.warmLights) list.Add(l != null ? l.intensity : -1f);
        return list;
    }

    // ---------- a morning by hand ----------

    IEnumerator PlayMorning(Watched w, DayIntro.Mode mode, int day, string[] order, bool expectFull)
    {
        yield return Until(() => DayIntro.Instance == null, 6f, $"{w.run}: the morning before has put everything away");
        Pose before = new Pose(main.transform.position, main.transform.rotation);
        var screensBefore = SnapshotScreens();
        float remainingBefore = clock.TimeRemaining, intoDayBefore = clock.SecondsIntoDay;
        w.bellBefore = Sfx.Requested("day.open");
        begunThisRun.Clear();
        bool began = DayIntro.BeginForCheck(mode, day, order);
        Check(began, $"{w.run}: a {mode} morning for Day {day} began{(order != null ? " (" + string.Join("|", order) + ")" : "")}");
        if (!began) yield break;
        if (DayIntro.Instance != null && DayIntro.Instance.PlanNotes.Count > 0) Note($"{w.run}: the picker: " + string.Join("; ", DayIntro.Instance.PlanNotes));
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
            CheckFull(w);
            CheckScreensBack(w, screensBefore);
        }
        w.order = DayIntro.LastOrder;
    }

    // What every Full morning must do.
    void CheckFull(Watched w)
    {
        w.order = DayIntro.LastOrder;
        Note($"{w.run}: {w.order} ({w.seconds:0.00} s)");
        Check(w.seconds >= 6f && w.seconds <= 8f, $"{w.run}: 6-8 s from its first frame to the controls coming back ({w.seconds:0.00} s)");
        Check(w.timeRemainingDrift <= .001f, $"{w.run}: the clock did not advance while it played (time remaining moved {w.timeRemainingDrift:0.000} s)");
        Check(w.hudOffenders == 0, $"{w.run}: every other screen was faded out while it played" + (w.hudOffenders > 0 ? $" ({w.hudOffenders} showing: {w.hudOffenderNames})" : w.hudOffenders < 0 ? " (not sampled)" : ""));
        Check(w.titleSeen && w.titleLongest >= DayIntro.TitleMinimumSeconds, $"{w.run}: the title came up and stayed its minimum ({w.titleLongest:0.00} s of {DayIntro.TitleMinimumSeconds:0.0})");
        Check(w.bellAfter == w.bellBefore + 1, $"{w.run}: the bell rang once, by the intro ({w.bellAfter - w.bellBefore} ring(s); the intro says {(DayIntro.LastRangTheBell ? "it rang it" : "it did not")})");
        Check(w.landed && !PlayerMovement.Held && !UiClock.Held && clock.enabled, $"{w.run}: afterwards Ace, the screen's clock and the day's clock are free");
        Check(GameObject.Find("Day intro (while playing)") == null && GameObject.Find("The cup") == null && GameObject.Find("The pour (day intro)") == null,
            $"{w.run}: nothing of the intro is left behind");
        if (w.cameraMoved >= 0f)
            Check(w.cameraMoved <= .01f && w.cameraTurned <= .5f,
                $"{w.run}: the player's camera came back where it was ({w.cameraMoved * 100f:0.0} cm, {w.cameraTurned:0.00}°)");
    }

    // Watches a morning from now until the intro has put itself away (the title gone): every frame of every shot judged, photos
    // at each shot's best moments, the clock sampled, the screens sampled once, the title timed, the camera measured after it lands.
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
                bool cut = id.Length > 0 && id != lastShot;
                if (cut) { lastShot = id; momentsDone.Clear(); }
                if (id.Length > 0 && !intro.PullingOut) EveryFrame(intro, cut);
                if (id.Length > 0)
                {
                    IReadOnlyList<float> moments = intro.ShotMoments;
                    for (int j = 0; j < moments.Count; j++)
                    {
                        if (momentsDone.Contains(j) || intro.ShotTime < moments[j] * intro.ShotLength) continue;
                        momentsDone.Add(j);
                        AtThePhotoMoment(intro);
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

    // ---------- (a)-(e): every shot, every frame ----------

    ShotRecord RecordOf(DayIntro.Shot s)
    {
        if (!records.TryGetValue(s.Id, out ShotRecord r))
            records[s.Id] = r = new ShotRecord { id = s.Id, minimum = s.MinSubjectDistance, grace = s.IsGrace, composition = s.SubjectIsComposition };
        return r;
    }

    // (a) the nearest the camera comes to the subject, every frame; the worst frame; a long frame at a cut.
    void EveryFrame(DayIntro intro, bool cut)
    {
        DayIntro.Shot s = intro.Current;
        if (s == null) return;
        ShotRecord r = RecordOf(s);
        r.frames++;
        r.least = Mathf.Min(r.least, Vector3.Distance(intro.CameraPosition, s.Subject));
        float dt = Time.unscaledDeltaTime;
        r.worstFrame = Mathf.Max(r.worstFrame, dt);
        if (cut) r.worstCut = Mathf.Max(r.worstCut, dt);
    }

    // (b)-(e) at a photo moment.
    void AtThePhotoMoment(DayIntro intro)
    {
        DayIntro.Shot s = intro.Current;
        if (s == null) return;
        ShotRecord r = RecordOf(s);
        Vector3 eye = intro.CameraPosition;
        Quaternion turn = intro.CameraRotation;
        float fov = intro.CameraFov;
        // (b) the subject inside the middle 80% of a 16:9 frame.
        Vector2 at = OnScreen(eye, turn, fov, s.Subject, out bool ahead);
        if (!ahead || at.x < .1f || at.x > .9f || at.y < .1f || at.y > .9f)
            r.problems.Add($"(b) the subject at {at.x:P0} across, {at.y:P0} up{(ahead ? "" : ", behind the camera")}");
        // (c) nothing hides it (a composition is the frame's aim, not a thing: not tested).
        if (!s.SubjectIsComposition)
        {
            Vector3 to = s.Subject - eye;
            float length = to.magnitude;
            if (length > .05f)
                foreach (RaycastHit h in Physics.RaycastAll(eye, to / length, length, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                {
                    if (s.SubjectRoot != null && h.transform.IsChildOf(s.SubjectRoot)) break;
                    if (h.distance >= length - .5f) break;   // at the subject itself (its own collider, or the wall right behind it)
                    // A collider that draws nothing hides nothing (15 Oct: street.front's "Window collision boundaries", the
                    // invisible box that keeps Ace off the glass, failed a shot whose photo shows the café's name in clear view).
                    if (!Drawn(h.collider)) continue;
                    r.problems.Add($"(c) {h.collider.name} stands between the camera and the subject ({h.distance:0.0} of {length:0.0} m)");
                    break;
                }
        }
        // (d) the camera isn't inside anything.
        foreach (Collider c in Physics.OverlapSphere(eye, .1f, ~0, QueryTriggerInteraction.Ignore))
        {
            r.problems.Add($"(d) the camera is inside {c.name}");
            break;
        }
        // (e) Grace's door is not in frame nearer than 15 m, unless it's her shot.
        if (!s.IsGrace && graceDoor != null)
        {
            Vector3 door = graceDoor.transform.position + Vector3.up * 1f;
            float far = Vector3.Distance(eye, door);
            Vector2 d = OnScreen(eye, turn, fov, door, out bool doorAhead);
            if (doorAhead && far < 15f && d.x >= 0f && d.x <= 1f && d.y >= 0f && d.y <= 1f)
                r.problems.Add($"(e) Grace's door is in frame {far:0.0} m away");
        }
    }

    // Something of the collider's own object (or under it) is drawn: an invisible boundary box is not.
    static bool Drawn(Collider c)
    {
        if (c == null) return false;
        foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }

    // A point's place on a 16:9 frame (0-1 across and up) from a camera's place, its turn and its vertical lens.
    static Vector2 OnScreen(Vector3 eye, Quaternion turn, float fov, Vector3 point, out bool ahead)
    {
        Vector3 v = Quaternion.Inverse(turn) * (point - eye);
        ahead = v.z > .01f;
        if (!ahead) return new Vector2(-1f, -1f);
        float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        return new Vector2(.5f + .5f * v.x / (v.z * t * 16f / 9f), .5f + .5f * v.y / (v.z * t));
    }

    void JudgeShots()
    {
        foreach (ShotRecord r in records.Values.OrderBy(x => Array.IndexOf(DayIntro.PoolIds, x.id)))
        {
            Check(r.least >= r.minimum - .01f,
                $"{r.id}: (a) the camera never nearer its subject than {r.minimum:0.0} m ({(float.IsPositiveInfinity(r.least) ? "no frames" : r.least.ToString("0.00") + " m at the nearest")}, {r.frames} frames)");
            var distinct = r.problems.Distinct().ToList();
            Check(distinct.Count == 0, $"{r.id}: (b)-(e) at its photo moments" + (distinct.Count > 0 ? ": " + string.Join("; ", distinct.Take(4)) : " (in the middle 80%, nothing in the way, the camera in the clear, no Grace's door nearer than 15 m)")
                                       + (r.composition ? "; its subject is the frame's aim, so (c) isn't tested" : ""));
            Note($"{r.id}: the worst frame {r.worstFrame * 1000f:0} ms" + (r.worstCut > .1f ? $"; at a cut {r.worstCut * 1000f:0} ms (over 100 ms: a hitch where the street is drawn)" : ""));
        }
    }

    // ---------- the picker, thirty mornings (pure: DayIntro.Plan) ----------

    void SimulateThirtyMornings()
    {
        // Every untied shot framable (the dynamic ones too: the picker doesn't know who'll be on the pavement), plus her house.
        List<DayIntro.Shot> catalogue = DayIntro.Catalogue();
        var framable = catalogue.Where(s => s.Tie == DayIntro.Tie.None || s.Id == "house.grace").ToList();
        var rng = new System.Random(20261014);
        var history = new List<DayIntro.Morning>();
        var mornings = new List<DayIntro.Morning>();
        var notes = new List<string>();
        int[] graceDays = { 2, 3, 9 };
        for (int day = 1; day <= 30; day++)
        {
            DayIntro.TieToday tie = graceDays.Contains(day) ? new DayIntro.TieToday { kind = DayIntro.Tie.Regular, grace = true, how = "the lab's Grace" } : DayIntro.TieToday.None;
            List<DayIntro.Shot> plan = DayIntro.Plan(day, tie, framable, history, rng, notes);
            var m = new DayIntro.Morning { day = day, order = string.Join("|", plan.Select(s => s.Id)), grace = plan.Any(s => s.IsGrace) };
            mornings.Add(m);
            history.Add(m);
            if (history.Count > 8) history.RemoveAt(0);
        }
        Note("thirty mornings: " + string.Join("  ", mornings.Select(m => $"{m.day}:{m.order.Replace("street.", "s.").Replace("bench.", "b.")}{(m.grace ? "*" : "")}")));
        Check(mornings[0].order == Canonical, $"the picker: Day 1 is the fixed order ({mornings[0].order})");
        int repeats = 0, close = 0, graceRuns = 0;
        for (int i = 0; i < mornings.Count; i++)
        {
            for (int j = Mathf.Max(0, i - 5); j < i; j++) if (mornings[j].order == mornings[i].order) repeats++;
            if (i > 0 && Differ(mornings[i - 1].order, mornings[i].order) < 2) close++;
            int inSix = 0;
            for (int j = Mathf.Max(0, i - 5); j <= i; j++) if (mornings[j].grace) inSix++;
            if (inSix > 1) graceRuns++;
        }
        Check(repeats == 0, $"the picker: no order repeats within any six consecutive mornings ({repeats})");
        Check(close == 0, $"the picker: consecutive mornings differ in at least two slots ({close} pair(s) closer)");
        Check(graceRuns == 0 && !mornings[0].grace, $"the picker: Grace in at most one of any six consecutive mornings, never on Day 1 ({graceRuns} window(s) over)");
        string graceOn = string.Join(", ", mornings.Where(m => m.grace).Select(m => m.day));
        Check(graceOn == "2, 9", $"the picker: tied on Days 2, 3 and 9, Grace shows on Days 2 and 9 only (she showed on: {(graceOn.Length > 0 ? graceOn : "none")})");
        var untied = catalogue.Where(s => s.Tie == DayIntro.Tie.None).Select(s => s.Id).ToList();
        var unseen = untied.Where(id => !mornings.Any(m => m.Used(id))).ToList();
        Check(unseen.Count == 0, $"the picker: every untied shot comes at least once in thirty mornings ({untied.Count - unseen.Count} of {untied.Count}" +
                                 (unseen.Count > 0 ? "; never: " + string.Join(", ", unseen) : "") + ")");
        var slotOf = catalogue.ToDictionary(s => s.Id, s => s.Slot);
        int wrongSlot = 0;
        foreach (DayIntro.Morning m in mornings.Skip(1))
        {
            string[] ids = m.order.Split('|');
            for (int i = 0; i < ids.Length; i++) if (slotOf.TryGetValue(ids[i], out int slot) && slot != i) wrongSlot++;
        }
        Check(wrongSlot == 0, $"the picker: after Day 1 every shot plays in its own slot ({wrongSlot} out of place)");
    }

    // Each tie forced once on a morning with no history: its shot in slot 2, or "tie not built" (the stubs wait on other leads).
    void ForceEachTie()
    {
        var notes = new List<string>();
        List<DayIntro.Shot> ready = DayIntro.PreparedNow(notes);
        var ties = new (string name, DayIntro.TieToday tie)[]
        {
            ("Grace (today's regular)", new DayIntro.TieToday { kind = DayIntro.Tie.Regular, grace = true, how = "forced" }),
            ("another regular", new DayIntro.TieToday { kind = DayIntro.Tie.Regular, grace = false, how = "forced" }),
            ("a rush morning", new DayIntro.TieToday { kind = DayIntro.Tie.Rush, how = "forced" }),
            ("a caught night", new DayIntro.TieToday { kind = DayIntro.Tie.Caught, how = "forced" }),
            ("rain", new DayIntro.TieToday { kind = DayIntro.Tie.Rain, how = "forced" }),
        };
        foreach (var (name, tie) in ties)
        {
            var planNotes = new List<string>();
            List<DayIntro.Shot> plan = DayIntro.Plan(10, tie, ready, new List<DayIntro.Morning>(), new System.Random(7), planNotes);
            DayIntro.Shot second = plan.Count > 2 ? plan[2] : null;
            if (second != null && second.Tie == tie.kind) Check(true, $"tied to {name}: {second.Id} lands in slot 2");
            else Note($"tied to {name}: tie not built (the normal draw: {string.Join("|", plan.Select(s => s.Id))}){(planNotes.Count > 0 ? "; " + string.Join("; ", planNotes) : "")}");
        }
        Check(graceDoor == null || ready.Any(s => s.Id == "house.grace"), "Grace's front can be framed in this scene (her door is found)");
    }

    // ---------- the skip ----------

    IEnumerator SkipMorning(Watched w)
    {
        yield return Until(() => DayIntro.Instance == null, 6f, $"{w.run}: the morning before has put everything away");
        Pose before = new Pose(main.transform.position, main.transform.rotation);
        w.bellBefore = Sfx.Requested("day.open");
        bool began = DayIntro.BeginForCheck(DayIntro.Mode.Full, 8, null);
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
        ReviewScreen.Scan(shot, folder, name);   // Review 2: the words in this photo, measured (reads the shot only; never throws)
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
        DayIntro.ShotBegan -= Began;
        if (DayIntro.Instance != null) DayIntro.RequestSkip();
        DayIntro.ForceOn = false;
        DayIntro.ModeOverride = null;
        if (settingRead && settingBefore != null) TrySetSetting(settingBefore.ToString());
        if (spawnerHeld && spawner != null) spawner.enabled = spawnerWas;
        // The intro's memory of mornings (cosmetic, per machine) as it was before the check: the check's mornings never count.
        if (historyBefore != null)
        {
            if (historyBefore.Length == 0) PlayerPrefs.DeleteKey(DayIntro.HistoryKey);
            else PlayerPrefs.SetString(DayIntro.HistoryKey, historyBefore);
        }
        if (lastOrderBefore != null)
        {
            if (lastOrderBefore.Length == 0) PlayerPrefs.DeleteKey(DayIntro.LastOrderKey);
            else PlayerPrefs.SetString(DayIntro.LastOrderKey, lastOrderBefore);
        }
        PlayerPrefs.Save();
#if UNITY_EDITOR
        // Play mode ends by itself once the report is written (the editor's next update, after this frame's Finish).
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
