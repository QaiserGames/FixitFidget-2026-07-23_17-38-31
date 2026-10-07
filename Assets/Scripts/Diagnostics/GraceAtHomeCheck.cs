using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// GRACE AT HOME, CHECKED (break-ins chunk C, 6 Oct 2026; a lab session only: Fixit Fidget > Night > Break-ins 7 ...;
// claude/chunk-c-grace-at-home-plan.md §9)
//
// Her night (Day 5, a Friday), at speed: the clock is moved to just before each step of her night and she's watched
// doing it (where she is, which of her lights are on, her doors, the quilt, what the street sees: her front window, her
// bedroom bays). Then her eyes and ears, with Ace in her house: sneaking behind her armchair (not seen, not heard); taking
// her cups (the rustle: "Hm?", her head turns); a sound, Ace hides in the cupboard under the stairs, and she comes to look
// and goes back to her chair; the creaky stair wakes her (her lamp, "Hello?"), and she's asleep again 20 s later, while a
// sneaking foot on it doesn't; her bedroom doors eased open sneaking (she sleeps on) and opened at a walk (the creak wakes
// her); and walking past her armchair in the light: "Ace?! What on earth—", "Caught.", the night ends, the cups go back.
// Her Thursday (Day 4): the house dark at 11 PM, home along the pavement and in at her door at 1:30, the kettle, bed, and
// no water at 2:40. Photos and report.txt go to Logs/Night/grace-at-home-<mode>-<time>/. The lab save is the only file
// written (by the game); nothing in the scene changes.
// ---------------------------------------------------------------------------
public sealed class GraceAtHomeCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.GraceAtHome.Check";
    public enum Mode { Night = 1, Thursday = 2 }

    public Mode mode = Mode.Night;
    public string folder;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    bool lastWait;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    CharacterController capsule;
    GraceHouse house;
    GraceAtHome grace;
    NightWalk night;
    SaveManager saves;
    Vector3 pavement;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        int asked = PlayerPrefs.GetInt(PendingKey, 0);
        if (asked == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Grace at home check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Grace at home check (this Play session only)");
        var check = go.AddComponent<GraceAtHomeCheck>();
        check.mode = (Mode)asked;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            $"grace-at-home-{(check.mode == Mode.Thursday ? "thursday" : "night")}-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", $"grace-at-home-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine($"Grace at home - {(mode == Mode.Thursday ? "her Thursday" : "her night, her eyes and her ears")} (a lab session)");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Grace at home check] Running: a few minutes. Hands off the mouse and keyboard until it reports. {folder}");

        var steps = new Stack<IEnumerator>();
        steps.Push(Run());
        while (steps.Count > 0)
        {
            IEnumerator step = steps.Peek();
            bool more;
            try { more = step.MoveNext(); }
            catch (Exception e)
            {
                Check(false, "the check ran to its end (it stopped: " + e.Message + ")");
                Debug.LogException(e);
                break;
            }
            if (!more) { steps.Pop(); continue; }
            if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
            yield return step.Current;
        }
        Finish();
    }

    IEnumerator Run()
    {
        yield return Until(() => NightWalk.Instance != null && NightWalk.Instance.Active && GraceHouse.Instance != null
                                 && GraceAtHome.Instance != null && GraceAtHome.Instance.NightOn, 15f, "the night is on, with Grace's house and Grace at home");
        if (!lastWait) yield break;
        night = NightWalk.Instance;
        house = GraceHouse.Instance;
        grace = GraceAtHome.Instance;
        saves = SaveManager.Instance;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        capsule = movement != null ? movement.GetComponent<CharacterController>() : null;
        Check(movement != null && interactor != null && view != null && capsule != null && saves != null, "Ace and the save manager are in the scene");
        if (movement == null || interactor == null || view == null || capsule == null || saves == null) yield break;
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        yield return Seconds(1.5f);   // the lab puts Ace at her door; the camera arrives
        pavement = movement.transform.position;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        Check(grace.look != null, $"her look is set ({(grace.look != null ? grace.look.name : "none: POLYGON City isn't on this machine?")})");
        Check(grace.standardLamp != null && grace.bedsideLamp != null && grace.landingLight != null && grace.kitchenLight != null && grace.tvLight != null
              && grace.tv != null && grace.frontCurtains != null && grace.quiltMade != null && grace.quiltAsleep != null && grace.westLeaf != null
              && grace.eastLeaf != null && grace.armchair != null, "Break-ins 6 gave her everything her night uses");
        Note(grace.Describe());
        if (mode == Mode.Thursday) yield return HerThursday(day);
        else
        {
            yield return HerNight(day);
            yield return HerEyesAndEars();
            yield return Caught();
        }
    }

    // ================================================================== her usual night

    IEnumerator HerNight(int day)
    {
        Check(day == 5 && Weekdays.Name(day) == "Friday" && !grace.Thursday, $"Day {day} is a {Weekdays.Name(day)}: her usual night");
        yield return Until(() => HudClock().Contains("Friday night"), 3f, $"the HUD names the night (\"{HudClock()}\")");

        // 11 PM: her armchair and the TV.
        night.SetHour(23.05f);
        yield return Seconds(.6f);
        Check(grace.Home && grace.Seated && grace.WatchingTv, $"11 PM: in her armchair, watching TV ({grace.Doing})");
        CheckLights("11 PM", standard: true, tv: true, kitchen: false, landing: false, bedside: false);
        Check(grace.FrontWindowGlows && Bays() == 0, $"from the street: her front window lit, her bedroom bays dark ({Bays()} of {BaysAll()} lit)");
        Check(grace.quiltMade.activeSelf && !grace.quiltAsleep.activeSelf && grace.DoorsOpen, "her bed made, her bedroom doors open");
        yield return Photo("01-11pm-from-the-street-the-tv-on");

        // 11:35: the kettle; 11:45 back to her chair.
        night.SetHour(23.55f);
        yield return Seconds(.3f);
        Check(grace.Seated, "11:33: still in her chair");
        yield return Until(() => grace.Spot == GraceHouseMap.Kettle && !grace.Walking && !grace.Seated, 12f, "11:35: up, and to the kettle");
        CheckLights("at the kettle", standard: true, tv: true, kitchen: true, landing: false, bedside: false);
        yield return Seconds(.4f);
        yield return Photo("02-1135-the-kettle");
        yield return Until(() => grace.Seated && grace.Step == GraceNight.Act.Sit, 16f, "11:45: back in her chair with her tea");
        CheckLights("back in her chair", standard: true, tv: true, kitchen: false, landing: false, bedside: false);

        // Midnight: up to bed.
        night.SetHour(23.97f);
        bool landingSeen = false, doorsSwung = false;
        float until = Time.realtimeSinceStartup + 32f;
        while (Time.realtimeSinceStartup < until && !(grace.InBed && grace.DoorsShut))
        {
            if (grace.Lit(grace.landingLight)) landingSeen = true;
            if (!grace.DoorsOpen && !grace.DoorsShut) doorsSwung = true;
            yield return null;
        }
        Check(grace.InBed && grace.DoorsShut, $"midnight: up the stairs, the doors shut behind her, into bed ({grace.Doing}, at {Clock()})");
        Check(landingSeen && doorsSwung, "on the way: the landing light on, the doors swinging shut");
        CheckLights("in bed", standard: false, tv: false, kitchen: false, landing: false, bedside: true);
        Check(grace.quiltAsleep.activeSelf && !grace.quiltMade.activeSelf, "the slept-in quilt shows her in bed (her body put away)");
        yield return Seconds(.3f);
        Check(!grace.FrontWindowGlows && Bays() > 0, $"from the street: her front window dark, a bedroom bay lit ({Bays()} of {BaysAll()})");
        yield return Photo("03-midnight-her-bedroom-lit");
        yield return Until(() => grace.Asleep && !grace.Lit(grace.bedsideLamp), 15f, "then her lamp off: asleep");
        Note($"asleep at {Clock()} (the plan says 12:20 AM: her lamp goes off 4 s after she's in bed at the earliest)");
        CheckLights("asleep", standard: false, tv: false, kitchen: false, landing: false, bedside: false);
        yield return Seconds(.3f);
        Check(Bays() == 0 && !grace.FrontWindowGlows, "from the street: her house dark");
        yield return Photo("04-asleep-her-house-dark");

        // 2:40: a glass of water.
        night.SetHour(26.62f);
        yield return Seconds(.3f);
        Check(grace.Asleep, "2:37: asleep");
        bool kitchenLit = false, doorsOpened = false;
        yield return Until(() =>
        {
            if (grace.Lit(grace.kitchenLight)) kitchenLit = true;
            if (grace.DoorsOpen) doorsOpened = true;
            return grace.Spot == GraceHouseMap.Sink && !grace.Walking;
        }, 30f, "2:40: her lamp, out of bed, the doors, down the stairs to the sink");
        CheckLights("at the sink", standard: false, tv: false, kitchen: true, landing: true, bedside: true);
        Check(kitchenLit && doorsOpened, "the doors opened for her, the kitchen light on");
        yield return Photo("05-0240-a-glass-of-water");
        yield return Until(() => grace.InBed && grace.DoorsShut, 30f, "back up the stairs, the doors shut, into bed");
        yield return Until(() => grace.Asleep && !grace.Lit(grace.bedsideLamp), 15f, $"and asleep again (her lamp off at {Clock()})");
    }

    // A line of hers on screen now: up and not faded, the barks' canvas showing, and where it is (for the report).
    void CheckBarkDrawn(string what)
    {
        Barks.Shown shown = default;
        bool up = grace.Speaker != null && Barks.TryGetShown(grace.Speaker, out shown);
        Check(up && shown.alpha > .5f && !Barks.Hidden,
            $"{what} is on screen (\"{shown.text}\": alpha {shown.alpha:0.00}, its band at {shown.band.center.x:0}, {shown.band.center.y:0} px" +
            $"{(shown.clamped ? ", clamped to the screen's edge" : "")}; barks hidden: {Barks.Hidden}; lines up: {Barks.Showing})");
    }

    void CheckLights(string when, bool standard, bool tv, bool kitchen, bool landing, bool bedside)
    {
        bool ok = grace.Lit(grace.standardLamp) == standard && grace.TvOn == tv && grace.Lit(grace.kitchenLight) == kitchen
                  && grace.Lit(grace.landingLight) == landing && grace.Lit(grace.bedsideLamp) == bedside;
        Check(ok, $"{when}: her lights: {grace.LightsNow()}");
    }

    // ================================================================== her eyes and ears

    IEnumerator HerEyesAndEars()
    {
        NightThing cups = NightThings.CupsOfGrace;
        NightTrophy trophy = FindObjectsByType<NightTrophy>(FindObjectsInactive.Include).FirstOrDefault(t => t.thingId == cups.id);
        Check(trophy != null && !trophy.Taken, "her cups are on her kitchen worktop");

        // ---- sneaking behind her armchair: not seen, not heard ----
        night.SetHour(23.10f);
        yield return Seconds(.5f);
        int notices = grace.Notices;
        movement.ScriptedSneak = true;
        PutAce(1.11f, 3.35f, 0f);
        yield return Seconds(.6f);
        yield return Walk(Plan((1.90f, 3.25f), (2.22f, 2.08f)), .25f, "sneaking from her entry, behind her armchair, to the pocket by the cupboard");
        yield return Seconds(1.5f);
        Check(grace.Notices == notices && grace.Mark < .05f && grace.WatchingTv,
            $"sneaking behind her while she watches TV: not seen, not heard (her mark {grace.Mark:0.00}; {grace.Doing})");
        yield return Photo("06-sneaking-behind-her-chair");

        // ---- the rustle: taking her cups turns her head ----
        yield return Walk(Plan((3.00f, 2.02f), (3.28f, 1.50f), (3.20f, 1.30f)), .22f, "sneaking round to the worktop");
        Check(grace.Notices == notices && grace.WatchingTv, $"at her worktop, sneaking: she hasn't noticed ({grace.Doing})");
        yield return Until(() => interactor.CurrentPrompt.StartsWith("Take ", StringComparison.Ordinal), 3f, $"the cups are offered (\"{interactor.CurrentPrompt}\")");
        int heard = grace.SoundsHeard;
        Press();
        yield return Until(() => grace.SoundsHeard > heard, 1f, "taking them rustles, and she hears it (3 m: her chair is 2.7 m from the worktop)");
        Check(grace.LastHeard == NoiseKind.Rustle && grace.Mark >= GraceNight.Third - .01f, $"a rustle: her mark goes to a third ({grace.Mark:0.00})");
        yield return Until(() => grace.Notices > notices && grace.Now != GraceAtHome.Mood.Calm, 1f, "\"Hm?\": she stops to look");
        Check(Barks.TryGetShown(grace.Body, out Barks.Shown said) && said.text.Length > 0, $"she says it (\"{(said.text ?? "")}\")");
        Check(trophy.Taken, "the cups are Ace's (on the shelf, in the ledger)");
        yield return Seconds(.4f);
        CheckBarkDrawn("her \"Hm?\"");
        yield return Photo("07-the-rustle-hm");
        movement.ScriptedSneak = false;
        PutAce(pavement);
        yield return Until(() => grace.Now == GraceAtHome.Mood.Calm && grace.Mark < .3f, 20f, $"Ace gone, she settles ({grace.Doing})");
        yield return Until(() => grace.Seated && grace.WatchingTv, 20f, "back to her TV");

        // ---- she hears Ace, Ace hides, she comes to look, and goes back to her chair ----
        night.SetHour(23.12f);
        yield return Seconds(.5f);
        movement.ScriptedSneak = true;
        PutAce(2.22f, 2.08f, 0f);
        yield return Seconds(.8f);
        int searches = grace.Searches, hides = grace.Hides;
        Vector3 pocket = house.World(2.22f, 2.08f);
        NightNoise.Make(pocket, NoiseKind.Step);
        yield return Seconds(.8f);
        NightNoise.Make(pocket, NoiseKind.Step);
        yield return Seconds(.2f);
        Check(grace.Mark >= GraceNight.SearchFrom - .01f && grace.Now == GraceAtHome.Mood.Looking,
            $"two steps in the pocket behind her: \"Hm?\", her mark at {grace.Mark:0.00}, and she looks round ({grace.Doing})");
        string hide = "Hide in " + grace.Cupboard.what;
        yield return Until(() => interactor.CurrentPrompt == hide, 2f, $"in front of the cupboard the prompt reads \"{hide}\" (\"{interactor.CurrentPrompt}\")");
        Press();
        yield return Until(() => grace.AceHidden, 1f, "E: Ace hides in the cupboard");
        Check(grace.Hides == hides + 1 && view.AceSetAside && !capsule.enabled && PlayerMovement.Held && NoteShowing().Contains("Hidden"),
            $"hidden: Ace's body and capsule set aside, Ace held, the note says so (\"{Short(NoteShowing())}\")");
        yield return Until(() => grace.Searches > searches, 1.5f, "her mark past a third: she comes to look where she heard Ace");
        float closest = float.MaxValue;
        bool photographed = false;
        float until = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < until && grace.Now == GraceAtHome.Mood.Searching)
        {
            if (grace.Body != null) closest = Mathf.Min(closest, Flat(grace.Body.position - pocket).magnitude);
            if (!photographed && grace.Spot == GraceHouseMap.Pocket && !grace.Walking)
            {
                photographed = true;
                yield return Photo("08-she-comes-to-look");
            }
            yield return null;
        }
        Check(closest < 1.3f && grace.Catches == 0, $"she came to the cupboard ({closest:0.00} m from where she heard Ace), looked round, and never saw Ace");
        yield return Until(() => grace.Seated, 20f, $"and went back to her chair ({grace.Doing})");
        yield return Photo("09-back-in-her-chair-ace-hidden");
        yield return ComeOut();
        Check(!grace.AceHidden && !view.AceSetAside && capsule.enabled && !PlayerMovement.Held && !NoteShowing().Contains("Hidden"),
            "E again: Ace comes out (the body back, the capsule solid, free to move, the note gone)");
        movement.ScriptedSneak = false;
        PutAce(pavement);
        yield return Seconds(.5f);

        // ---- the creaky stair: at a walk it wakes her; sneaking it doesn't ----
        night.SetHour(24.6f);
        yield return Seconds(.6f);
        Check(grace.Asleep && grace.DoorsShut, $"12:36: asleep, her doors shut ({grace.Doing})");
        int wakes = grace.Wakes, creaks = grace.Creaks;
        movement.ScriptedSneak = true;
        PutAce(1.30f, .70f, 1.20f);
        yield return Seconds(.6f);
        yield return Walk(Plan((2.20f, .70f), (2.62f, .70f)), .3f, "up the upper flight, sneaking, over the creaky tread");
        yield return Seconds(1f);
        Check(grace.Creaks > creaks && grace.Wakes == wakes && grace.Asleep, "sneaking, the tread only creaks a little (2.5 m): she sleeps on");
        yield return Walk(Plan((1.30f, .70f)), .3f, "back down to the foot of the upper flight, sneaking");
        movement.ScriptedSneak = false;
        yield return Seconds(.8f);
        creaks = grace.Creaks;
        yield return Walk(Plan((2.20f, .70f), (2.62f, .70f)), .3f, "up the upper flight again, at a walk");
        Check(grace.Creaks > creaks, "the creaky tread creaks under Ace");
        yield return Until(() => grace.Wakes > wakes && grace.Now == GraceAtHome.Mood.Woken, 1f, "it wakes her");
        Check(grace.WokeTo == NoiseKind.CreakyStair, $"the creak woke her, not a step (it carries 5 m; her pillow is 4 m from it): woken by {grace.WokeTo}");
        Check(grace.Lit(grace.bedsideLamp), "her lamp on");
        Check(Barks.Showing > 0, "she says \"Hello?\"");
        yield return Seconds(.4f);
        CheckBarkDrawn("her \"Hello?\" from her pillow");
        yield return Photo("10-the-creak-wakes-her");
        yield return Until(() => grace.Asleep && !grace.Lit(grace.bedsideLamp), 24f, "20 s later, nobody seen, her lamp goes off again");
        Check(grace.Catches == 0, "the doors were shut: she never saw Ace");

        // ---- her bedroom doors ----
        night.SetHour(24.62f);
        yield return Seconds(.4f);
        movement.ScriptedSneak = true;   // sneaking, E eases them open
        PutAce(3.05f, .95f, 2.40f);
        yield return Seconds(.8f);
        yield return Until(() => interactor.CurrentPrompt == "Open the doors", 2f, $"on her landing, the doors shut: \"Open the doors\" (\"{interactor.CurrentPrompt}\")");
        int quiet = grace.QuietOpens, creaky = grace.CreakyOpens;
        wakes = grace.Wakes;
        Press();
        yield return Until(() => grace.DoorsOpen, 3f, "sneaking, Ace eases them open");
        Check(grace.QuietOpens == quiet + 1 && grace.Wakes == wakes && grace.Asleep, "quietly: she sleeps on");
        yield return Photo("11-the-doors-eased-open");
        night.SetHour(24.64f);
        yield return Seconds(.5f);
        Check(grace.DoorsShut && grace.Asleep, "(the clock moved on: the doors shut again, she's asleep)");
        movement.ScriptedSneak = false;
        yield return Seconds(.5f);
        yield return Until(() => interactor.CurrentPrompt == "Open the doors", 2f, "at a walk, \"Open the doors\" again");
        Press();
        yield return Until(() => grace.CreakyOpens > creaky, 1f, "at a walk they swing open at once, and creak");
        yield return Until(() => grace.Wakes > wakes, 1f, "the creak wakes her (5 m)");
        Check(grace.WokeTo == NoiseKind.Door, $"woken by the doors ({grace.WokeTo})");
        yield return Until(() => grace.DoorsOpen, 1.5f, "the doors open");
        PutAce(pavement);   // out of her sight before she has a proper look
        yield return Seconds(.5f);
        Check(grace.Catches == 0, $"Ace gone before she saw enough (her mark {grace.Mark:0.00})");
    }

    // ================================================================== caught

    IEnumerator Caught()
    {
        NightThing cups = NightThings.CupsOfGrace;
        NightTrophy trophy = FindObjectsByType<NightTrophy>(FindObjectsInactive.Include).FirstOrDefault(t => t.thingId == cups.id);
        NightLedger ledger = saves.Night;
        Check(ledger.HasTrophy(cups.id) && trophy != null && trophy.Taken, "before: Ace has her cups (taken earlier tonight)");
        night.SetHour(23.15f);
        yield return Seconds(.6f);
        Check(grace.Seated && grace.WatchingTv, $"11:09: in her chair, the TV on ({grace.Doing})");
        movement.ScriptedSneak = false;
        // In her kitchen behind her shoulder: out of the TV's slice of her view, and a teleport isn't a step.
        PutAce(3.90f, 1.45f, 0f);
        yield return Seconds(.4f);
        Check(grace.Now == GraceAtHome.Mood.Calm && grace.WatchingTv && grace.Mark < .05f,
            $"in her kitchen behind her shoulder: she hasn't noticed ({grace.Doing}; her mark {grace.Mark:0.00})");
        int heardBefore = grace.SoundsHeard, noticedBefore = grace.Notices;
        float from = Time.realtimeSinceStartup;
        yield return Walk(Plan((4.60f, 2.10f), (4.62f, 2.83f)), .3f, "walking out of her kitchen and round to the front of her armchair, in her lamplight",
            giveUp: () => grace.Catches > 0);
        yield return Until(() => grace.Catches > 0, 3f, "she hears Ace's steps, looks round and sees Ace: her mark fills");
        Note($"caught {Time.realtimeSinceStartup - from:0.0} s after Ace set off from her kitchen; she heard {grace.SoundsHeard - heardBefore} step(s) " +
             $"and looked round {grace.Notices - noticedBefore} time(s) first");
        Check(Barks.TryGetShown(grace.Body, out Barks.Shown said) && said.text.StartsWith("Ace?!", StringComparison.Ordinal),
            $"\"{(said.text ?? "")}\"");
        yield return Photo("12-caught-what-on-earth");
        yield return Until(() => NightCycle.Instance != null && NightCycle.Instance.LastEnding == "caught" && Caption() == "Caught.", 4f,
            $"the screen goes dark on \"Caught.\" (\"{Caption()}\")");
        yield return Seconds(.3f);
        yield return Photo("13-the-caught-card");
        yield return Until(() => !night.Active, 6f, "and the night ends there");
        Check(!ledger.HasTrophy(cups.id) && trophy.visual != null && trophy.visual.activeSelf && !saves.Notebook.Knows(cups.id + ".taken"),
            "what Ace took tonight went back: off the shelf, on her worktop again, crossed out of the notebook");
        Check(ledger.HasTrophy(NightThings.GraceGnome), "Barnaby, taken on an earlier night, stays on Ace's shelf");
        yield return Until(() => NightCycle.Instance.Now == NightCycle.Phase.Day && !PlayerMovement.Held, 8f, "the morning: Ace free to move");
        int today = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        yield return Until(() => HudClock().Contains(Weekdays.Label(today)), 3f, $"the HUD names the day (\"{Weekdays.Label(today)}\")");
        Note($"the HUD's clock reads \"{HudClock().Replace("\n", " / ")}\"");
        yield return Seconds(.5f);
        yield return Photo("14-the-morning-after-the-hud");
    }

    // ================================================================== her Thursday

    IEnumerator HerThursday(int day)
    {
        Check(day == 4 && Weekdays.IsThursday(day) && grace.Thursday, $"Day {day} is a {Weekdays.Name(day)}: her odd night");
        yield return Until(() => HudClock().Contains("Thursday night"), 3f, $"the HUD names the night (\"{HudClock()}\")");
        night.SetHour(23.1f);
        yield return Seconds(.6f);
        Check(grace.Away && !grace.Home, $"11 PM: she's out ({grace.Doing})");
        CheckLights("11 PM", standard: false, tv: false, kitchen: false, landing: false, bedside: false);
        Check(!grace.FrontWindowGlows && Bays() == 0, "from the street: her house dark");
        yield return Photo("t01-thursday-11pm-her-house-dark");

        // Her way home, walked first by a capsule her size: nothing solid on it (the night's exact collision is on).
        CheckTheWayHome();

        // Ace waits by her door, as a player watching for her would (at the kerb: standing in the middle of the pavement he'd be
        // in her way, and she'd wait for him to step aside): on her way from 1:00, she appears only where the camera can't see,
        // walks into view along the pavement, and is in at her door at about 1:30.
        Vector3 kerb = house.door.DoorwayPoint + house.door.Outward * 3.8f;
        if (Physics.Raycast(kerb + Vector3.up * 3f, Vector3.down, out RaycastHit road, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            kerb.y = road.point.y;
        PutAce(kerb);
        night.SetHour(25.02f);
        yield return Seconds(.5f);
        Check(grace.Away && !grace.OnTheStreet, $"1:01: on her way, but not yet in sight ({grace.Doing})");
        yield return Until(() => grace.OnTheStreet, 25f, "she sets off along the pavement when she has to, to be home at 1:30");
        Vector3 from = grace.SetOffFrom;
        float doorway = Flat(from - house.World(1.11f, 4.05f)).magnitude;
        Check(!GraceAtHome.InView(from), $"she appeared where the camera can't see, {doorway:0.0} m from her door ({Where(from)}, at {Clock()})");
        yield return Until(() => grace.Body != null && GraceAtHome.InView(grace.Body.position), 25f, "and walks into view along the pavement");
        yield return Seconds(.4f);
        yield return Photo("t02-along-the-pavement");
        bool doorOpened = false, photographed = false;
        float inAt = -1f;
        float until = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < until && !(grace.Spot == GraceHouseMap.Kettle && !grace.Walking))
        {
            if (house.door != null && house.door.IsOpen && grace.Body != null && Flat(grace.Body.position - house.door.DoorwayPoint).magnitude < 2.5f)
                doorOpened = true;
            if (inAt < 0f && !grace.OnTheStreet && !grace.Away) inAt = night.Hour;
            if (!photographed && doorOpened)
            {
                photographed = true;
                yield return Photo("t03-in-at-her-door");
            }
            yield return null;
        }
        Check(inAt > 0f && Mathf.Abs(inAt - GraceNight.ThursdayHome) < 3f / 60f, $"in at her door at {(inAt > 0f ? GraceNight.Clock(inAt) : "?")} (the plan: 1:30 AM)");
        Check(grace.Spot == GraceHouseMap.Kettle && !grace.Walking, $"through to the kettle (at {Clock()})");
        Check(doorOpened, "her front door opened for her");
        CheckLights("tea, Thursday", standard: false, tv: false, kitchen: true, landing: false, bedside: false);
        yield return Seconds(.6f);
        yield return Until(() => house.door == null || house.door.IsClosed, 4f, "the door shut behind her");
        yield return Photo("t04-thursday-tea");

        night.SetHour(25.81f);
        yield return Until(() => grace.InBed && grace.DoorsShut, 35f, $"1:50: up to bed ({grace.Doing})");
        yield return Until(() => grace.Asleep && !grace.Lit(grace.bedsideLamp), 20f, $"asleep, her lamp off ({Clock()})");
        night.SetHour(26.62f);
        yield return Seconds(6f);
        Check(grace.Asleep && grace.Step == GraceNight.Act.Sleep, $"2:40 comes and goes: no glass of water on a Thursday ({grace.Doing})");
        yield return Photo("t05-thursday-asleep");
    }

    // Her way home walked by a capsule her size (0.22 m, from the knee to the head) every quarter metre: nothing solid on it but
    // the pavement underfoot, with the night's exact collision on (lamp posts, planters, steps, bins).
    void CheckTheWayHome()
    {
        // Her pavement, then from its end to the foot of her steps (the steps themselves are hers alone).
        Vector3[] way = grace.wayHome != null && grace.wayHome.Length > 0 ? grace.wayHome.Append(grace.StepsFront).ToArray() : grace.wayHome;
        var names = new HashSet<string>();
        var hits = new List<string>();
        var found = new Collider[16];
        int samples = 0;
        float length = 0f;
        for (int k = 0; way != null && k + 1 < way.Length; k++)
        {
            float stretch = Vector3.Distance(way[k], way[k + 1]);
            length += stretch;
            for (float d = 0f; d <= stretch + .01f; d += .25f)
            {
                Vector3 p = Vector3.MoveTowards(way[k], way[k + 1], d);
                float ground = Physics.Raycast(p + Vector3.up * .8f, Vector3.down, out RaycastHit under, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                    ? under.point.y : p.y;
                int n = Physics.OverlapCapsuleNonAlloc(new Vector3(p.x, ground + .45f, p.z), new Vector3(p.x, ground + 1.55f, p.z), .22f, found, ~0,
                    QueryTriggerInteraction.Ignore);
                samples++;
                for (int i = 0; i < n; i++)
                {
                    Collider c = found[i];
                    if (c == null || c.transform.IsChildOf(movement.transform) || grace.Body != null && c.transform.IsChildOf(grace.Body)) continue;
                    if (!names.Add(c.name)) continue;
                    Bounds b = c.bounds;
                    hits.Add($"'{c.name}' at {Where(p)} (its bounds x {b.min.x:0.00} to {b.max.x:0.00}, z {b.min.z:0.00} to {b.max.z:0.00}, " +
                             $"y {b.min.y:0.00} to {b.max.y:0.00}; clear across the pavement at {ClearAcross(p, ground)})");
                }
            }
        }
        Check(samples > 0 && hits.Count == 0, $"her way home along the pavement is clear for someone her size ({samples} points over {length:0.0} m)" +
                                              (hits.Count > 0 ? ": " + string.Join("; ", hits.Take(6)) : ""));
        // The pavement across, a metre at a time along her way (for placing it): where someone her size could pass.
        for (int k = 0; way != null && k + 1 < way.Length; k++)
        {
            float stretch = Vector3.Distance(way[k], way[k + 1]);
            for (float d = 0f; d <= stretch + .01f; d += 1f)
            {
                Vector3 p = Vector3.MoveTowards(way[k], way[k + 1], d);
                float ground = Physics.Raycast(p + Vector3.up * .8f, Vector3.down, out RaycastHit under, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                    ? under.point.y : p.y;
                Note($"the pavement at z {p.z:0.0} (ground {ground:0.00}): clear for her at {ClearAcross(p, ground)}");
            }
        }
    }

    // Where across the pavement (x, every 10 cm within 1.2 m of the point) someone her size could pass the point.
    string ClearAcross(Vector3 p, float ground)
    {
        var found = new Collider[16];
        var clear = new List<string>();
        float runFrom = float.NaN, last = float.NaN;
        for (float dx = -1.2f; dx <= 1.2001f; dx += .1f)
        {
            float x = p.x + dx;
            int n = Physics.OverlapCapsuleNonAlloc(new Vector3(x, ground + .45f, p.z), new Vector3(x, ground + 1.55f, p.z), .22f, found, ~0,
                QueryTriggerInteraction.Ignore);
            bool free = true;
            for (int i = 0; i < n && free; i++)
            {
                Collider c = found[i];
                if (c == null || c.transform.IsChildOf(movement.transform) || grace.Body != null && c.transform.IsChildOf(grace.Body)) continue;
                free = false;
            }
            if (free && float.IsNaN(runFrom)) runFrom = x;
            if (free) last = x;
            if (!free && !float.IsNaN(runFrom)) { clear.Add($"{runFrom:0.0} to {last:0.0}"); runFrom = float.NaN; }
        }
        if (!float.IsNaN(runFrom)) clear.Add($"{runFrom:0.0} to {last:0.0}");
        return clear.Count > 0 ? "x " + string.Join(", ", clear) : "nowhere within 1.2 m";
    }

    // ================================================================== moving Ace

    List<Vector3> Plan(params (float X, float Y)[] stops) => stops.Select(s => house.World(s.X, s.Y)).ToList();

    // Ace at a point of her house (plan metres, the floor's height), standing on it.
    void PutAce(float X, float Y, float z) => PutAce(house.World(X, Y, z));

    void PutAce(Vector3 feet)
    {
        bool was = capsule.enabled;
        capsule.enabled = false;
        float lift = capsule.height * .5f - capsule.center.y + .05f;
        movement.transform.position = feet + Vector3.up * lift;
        capsule.enabled = was;
        movement.ClearInput();
    }

    IEnumerator Walk(List<Vector3> points, float radius, string what, Func<bool> giveUp = null)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 target = points[i];
            float deadline = Time.realtimeSinceStartup + 12f;
            bool reached = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (giveUp != null && giveUp()) { movement.ScriptedInput = Vector2.zero; yield break; }
                Vector3 p = movement.transform.position;
                Vector2 to = new Vector2(target.x - p.x, target.z - p.z);
                if (to.magnitude <= radius) { reached = true; break; }
                Vector3 direction = new Vector3(to.x, 0f, to.y).normalized;
                Vector3 local = Quaternion.Euler(0f, -view.MovementYaw, 0f) * direction;
                movement.ScriptedInput = new Vector2(local.x, local.z);
                yield return null;
            }
            movement.ScriptedInput = Vector2.zero;
            if (!reached)
            {
                Check(false, $"{what}: stuck short of {Where(target)} (at {Where(movement.transform.position)})");
                yield break;
            }
        }
        Note(what);
    }

    void Press() => movement.SendMessage("OnInteract", SendMessageOptions.DontRequireReceiver);

    // E to come out: the key when the Game view has the keyboard, as a player would; otherwise directly.
    IEnumerator ComeOut()
    {
        if (Application.isFocused && Keyboard.current != null)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Seconds(.3f);
            if (!grace.AceHidden) { Note("came out with the E key"); yield break; }
        }
        grace.ComeOut();
        Note("came out directly (the Game view didn't have the keyboard)");
        yield return Seconds(.2f);
    }

    // ================================================================== what's on screen

    int Bays() => night.Homes != null ? night.Homes.LitOn(house.house, 1) : -1;
    int BaysAll() => night.Homes != null ? night.Homes.RoomsOn(house.house, 1) : -1;
    string Clock() => GraceNight.Clock(night.Hour);

    // By day the clock is under the HUD's sign (HudCorners, playtest 3 session 3); at night it's the clock line, top right.
    string HudClock()
    {
        if (HudCorners.Instance != null && HudCorners.Instance.Showing) return HudCorners.Instance.ClockLine;
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text clock = Field<TMP_Text>(hud, "clockText");
        return clock != null && clock.gameObject.activeInHierarchy ? clock.text ?? "" : "";
    }

    string NoteShowing()
    {
        NightCycle cycle = NightCycle.Instance;
        TMP_Text note = Field<TMP_Text>(cycle, "note");
        RectTransform box = Field<RectTransform>(cycle, "noteBox");
        return note != null && box != null && box.gameObject.activeInHierarchy ? note.text ?? "" : "";
    }

    string Caption()
    {
        TMP_Text title = Field<TMP_Text>(NightCycle.Instance, "title");
        return title != null ? title.text ?? "" : "";
    }

    static T Field<T>(object owner, string name) where T : class
    {
        if (owner == null) return null;
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Any);
            if (field != null) return field.GetValue(owner) as T;
        }
        return null;
    }

    // ================================================================== plumbing

    IEnumerator Until(Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
        Check(lastWait, what + (lastWait ? "" : $" (gave up after {seconds:0} s; {(grace != null ? grace.Doing : "")})"));
    }

    static bool Safe(Func<bool> condition)
    {
        try { return condition(); }
        catch (Exception) { return false; }
    }

    static IEnumerator Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    IEnumerator Photo(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), shot.EncodeToJPG(88));
        Destroy(shot);
    }

    void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    void Note(string what) => report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");

    static string Short(string line) => string.IsNullOrEmpty(line) ? "" : line.Length <= 70 ? line : line.Substring(0, 67) + "...";
    static string Where(Vector3 p) => $"({p.x:0.00}, {p.z:0.00})";
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Finish()
    {
        if (movement != null) { movement.ScriptedInput = null; movement.ScriptedSneak = null; }
        report.AppendLine();
        if (grace != null) report.AppendLine(grace.Describe());
        if (NightCycle.Instance != null) report.AppendLine(NightCycle.Instance.Describe());
        if (night != null && night.Homes != null) report.AppendLine(night.Homes.Describe(night.Hour));
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Grace at home check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Grace at home check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Grace at home check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) { movement.ScriptedInput = null; movement.ScriptedSneak = null; }
    }
}
