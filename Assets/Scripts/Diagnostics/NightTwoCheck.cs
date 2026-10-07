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
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// NIGHT 2 CHECK: THE BINS EVERY NIGHT, GRACE'S CUPS, THE RETURN, THE MORNING, THE OFFICER, AND STALLING, DRIVEN
// (a lab session only: Fixit Fidget > Night > Night 2 - Play check ...; claude/session-3-favours-stalling-officer.md)
//
// From Day 2's recap of a made-up save after Night 1 (NightTwoSteps.DayTwoRecap: the man met, Barnaby given, Nerve,
// Grace's morning held, the officer in for a coffee on Day 1):
//   * Cups (the main check):
//       1. "Close up for the night": Ace inside the back door with the bag, the clock waiting, the man out of sight in the
//          dumpster (since 6 Oct he pops up when the bag goes in: on Night 2, the peek over the rim);
//       2. "Take the bins out", "Bin it": the near lid, the bag in; his verdict on the day (a straight face held) and the
//          ask for the cups (warm: he says why); Ace answers (+1); the notebook learns where the cups are; the clock runs;
//       3. Grace's house: the cups can't be taken from behind her kitchen wall; she's home (chunk C), so Ace waits on her
//          stoop until she's asleep, and sneaks in; at the worktop, "Take the reunion cups": in hand, the notebook says who
//          for, Ace's line; she sleeps through it;
//       4. back at the bins, "Give him the reunion cups": the return (Ace answers, +1), four cups set out on the crate one
//          by one, Soft feet learned (no page: her photos aren't in the game yet); calling it a night at the back door;
//       5. Day 3: the save has it all; Grace comes in first about her cups (with Nerve the green is wider), and Ace keeps
//          a straight face; the officer comes in, orders, and after his order asks about the man: "Say nothing", held.
//   * Visit (one skip): the ask, then calling it a night without the cups: a skip. On Day 3 he comes into the café at
//     opening on foot in his own look, takes a table, says one line when Ace is near, and sits until about 1:30 PM.
//   * Note (the third skip; playtest 3): he has asked twice before: tonight he asks again, colder (no reply); calling it
//     a night is the third skip. Day 3 opens with his mess (a dirty cup on every seat, cleared with E), his planted
//     one-star review in the day, his note on the counter (and in the notebook), his table, and the officer coming in
//     for his word, a harder question. The cups are still what he wants.
// A photo at each step and report.txt go to Logs/Night/night-two-check-<mode>-<time>/. Nothing is saved in the scene;
// the lab save is the only file written (by the game).
// ---------------------------------------------------------------------------
public sealed class NightTwoCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.NightTwo.Check";
    public enum Mode { Cups = 1, Visit = 2, Note = 3 }

    public Mode mode = Mode.Cups;
    public string folder;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Grace = GraceCameraEpisode.ProfileId;
    // The back street and West Street, where the night tour walks them (NightTour).
    const float BackStreet = 22.12f, WestStreet = -11.88f;

    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    bool lastWait;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    ConversationController conversation;
    SaveManager saves;
    NightLedger ledger;
    NightZeroSet set;
    NightLines lines;

#if UNITY_EDITOR
    // Asked for by the editor (NightTwoSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        int asked = PlayerPrefs.GetInt(PendingKey, 0);
        if (asked == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night 2 check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Night 2 check (this Play session only)");
        var check = go.AddComponent<NightTwoCheck>();
        check.mode = asked == (int)Mode.Visit ? Mode.Visit : asked == (int)Mode.Note ? Mode.Note : Mode.Cups;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            $"night-two-check-{check.mode.ToString().ToLowerInvariant()}-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", $"night-two-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine($"Night 2 - play check ({mode}), lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Night 2 check] Running ({mode}): a few minutes. Hands off the mouse and keyboard until it reports. {folder}");

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
        // ---------- Day 2's recap ----------
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapShowing(), 15f, "the lab opened on Day 2's recap");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        saves = SaveManager.Instance;
        ledger = saves != null ? saves.Night : null;
        set = NightZeroSet.Instance;
        lines = NightLines.Current;
        Check(movement != null && interactor != null && view != null && conversation != null && ledger != null, "Ace and the save manager are in the scene");
        Check(set != null && set.cornerCups != null && set.cornerCups.transform.childCount == 4 && set.crate != null,
            "the bins are in the scene with his crate and four cups (Bins 1, Night 2 1): " + (set != null ? set.Describe() : "none"));
        if (movement == null || ledger == null || set == null || lines == null) yield break;
        NightThing cups = NightThings.CupsOfGrace;
        LodgerStory.Favour favour = LodgerStory.FindFavour(cups.id);
        NightTrophy trophy = FindObjectsByType<NightTrophy>(FindObjectsInactive.Include).FirstOrDefault(t => t.thingId == cups.id);
        TrophyShelf shelf = FindAnyObjectByType<TrophyShelf>();
        GraceHouse house = GraceHouse.Instance;
        Check(trophy != null && trophy.needsSight && trophy.visual != null && trophy.visual.activeSelf, "Grace's cups are in her kitchen, taken only in sight (Night 2 1)");
        Check(shelf != null && shelf.CopyOf(cups.id) != null, "Ace's shelf has a place for them");
        Check(house != null, "Grace's house has an inside (Break-ins 1)");
        Check(lines.FindScene(favour.ask) != null && lines.FindScene(favour.askWarm) != null && lines.FindScene(favour.returnScene) != null,
            "the Night lines have the cups' ask, its warm one and the return (Barks 1)");
        Check(ledger.MetHim && ledger.Favour == cups.id && ledger.HasGiven(NightThings.GraceGnome) && ledger.Knows(LodgerStory.Nerve) && ledger.Warmth == 2,
            $"Day 2's record: met, Barnaby given, Nerve, warmth {ledger.Warmth}; his favour now the cups");
        Check(mode == Mode.Note ? ledger.AskedOn > 0 && ledger.Skips == LodgerStory.WordAtSkips - 1 : ledger.AskedOn == 0 && ledger.Skips == 0,
            mode == Mode.Note ? $"he has asked for them before, and been let down {ledger.Skips} time(s)" : "he hasn't asked for them yet");
        CheckTheOfficerIsSetUp();
        if (trophy == null || shelf == null || house == null) yield break;
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        yield return Photo("01-day-2-recap");

        // ---------- Night 2: the bins ----------
        RecapButton().onClick.Invoke();
        yield return Until(() => NightCycle.Instance != null && NightCycle.Instance.Now == NightCycle.Phase.Night, 12f, "the recap's button began the night");
        if (!lastWait) yield break;
        NightZero zero = NightZero.Instance;
        Lodger man = Lodger.Instance;
        Check(zero != null && !zero.First && NightZero.Pending && zero.Now == NightZero.Step.AtTheDoor, "the night opens at the bins (" + (zero != null ? zero.Describe() : "none") + ")");
        Check(man != null && man.Hidden, "he's out of sight in the dumpster, its lid shut, until the bag goes in");
        CafeArrivals arrivals = FindAnyObjectByType<CafeArrivals>();
        Check(arrivals == null || arrivals.Visiting == 0,
            $"nobody from the café is left on their way home ({(NightWalk.Instance != null ? NightWalk.Instance.SentHomeAtNightfall : 0)} sent home at nightfall)");
        if (zero == null || man == null) yield break;
        Check(Flat(movement.transform.position - set.insideDoor.position).magnitude < .3f && NightCarry.Current != null && NightCarry.Current.HeldId == NightZero.BagId,
            "Ace is just inside the back door with the bin bag");
        Check(NightWalk.Instance.ClockHeld && !NightCycle.Instance.CanCallItANight && !trophy.IsAvailable, "the clock waits, and nothing else is offered yet");
        yield return Seconds(1.6f);
        yield return Until(() => interactor.CurrentPrompt == "Take the bins out", 4f, "the prompt reads \"Take the bins out\"");
        yield return Photo("02-night-2-at-the-back-door");
        // Any door (playtest 3): out the front with the bag, the step follows Ace; back in, the back door is the way again.
        Vector3 insideBefore = movement.transform.position;
        MoveAce(new Vector3(.12f, 0f, -2.2f));
        yield return null;
        yield return null;
        Check(zero.Now == NightZero.Step.Outside && zero.CanBinIt, "out the front door with the bag, \"Bin it\" would be offered at the dumpster");
        MoveAce(insideBefore);
        yield return null;
        yield return null;
        Check(zero.Now == NightZero.Step.AtTheDoor, "back inside, the back door is the way again");
        yield return Until(() => interactor.CurrentPrompt == "Take the bins out", 3f, "…and its prompt is back");
        Press();
        yield return Until(() => zero.Now == NightZero.Step.Outside, 3f, "E took Ace out on Back Street");
        yield return Seconds(1f);
        yield return Walk(new List<Vector3> { new Vector3(6.25f, 0f, 20.2f), set.binSpot.position }, new List<float> { .35f, .25f }, "to the dumpster's near half");
        yield return Until(() => interactor.CurrentPrompt == "Bin it", 3f, "the prompt reads \"Bin it\"");
        float nearMost = 0f;
        bool reveal = false;
        Press();
        float binned = Time.realtimeSinceStartup;
        while (zero.Now != NightZero.Step.Deal && zero.Now != NightZero.Step.Done && Time.realtimeSinceStartup - binned < 8f)
        {
            nearMost = Mathf.Max(nearMost, set.nearLid != null ? Quaternion.Angle(set.nearLid.localRotation, Quaternion.identity) : 0f);
            reveal |= set.revealCamera != null && set.revealCamera.activeSelf;
            yield return null;
        }
        Check(nearMost > 40f && !reveal, $"the near lid lifted for the bag ({nearMost:0}°), and there was no reveal this time");
        Check(man.Up && man.Pops == 1 && zero.Popped == Lodger.StyleFor(2) && set.LidOpen(set.farLid) > .99f,
            $"he popped up out of the far half, the lid up ({zero.Popped}: on Night 2 the peek over the rim)");
        Check(Time.realtimeSinceStartup - binned < 3.5f, $"he spoke {Time.realtimeSinceStartup - binned:0.0} s after the bag went in (a pop, not a reveal)");

        // ---------- his verdict, and tonight's ask ----------
        yield return Until(() => Barks.ScenePlaying, 3f, "his scene began");
        LodgerStory.Tonight expected = mode == Mode.Note ? LodgerStory.Tonight.AskAgain : LodgerStory.Tonight.Ask;
        Check(zero.Tonight == expected && zero.Verdict == LodgerStory.VerdictHeld, $"tonight: {zero.Tonight} (expected {expected}); his verdict {zero.Verdict} (Grace's morning, held)");
        var said = new List<string>();
        int answered = 0, warmth = ledger.Warmth;
        bool photographed = false;
        float sceneStart = Time.realtimeSinceStartup;
        while (Barks.ScenePlaying && Time.realtimeSinceStartup - sceneStart < 60f)
        {
            if (Barks.TryGetShown(man.Speaker, out Barks.Shown up) && (said.Count == 0 || said[said.Count - 1] != up.text)) said.Add(up.text);
            if (Barks.Choosing)
            {
                yield return Until(() => Barks.ChipsUp, 3f, "Ace's replies come up");
                yield return Seconds(.4f);
                if (!photographed) { photographed = true; yield return Photo("03-his-ask-and-the-replies"); }
                Note($"replies: \"{Barks.ReplyText(0)}\" / \"{Barks.ReplyText(1)}\"");
                yield return Answer(0);
                answered++;
                continue;
            }
            yield return Seconds(.85f);
            if (Barks.TryGetShown(man.Speaker, out Barks.Shown again) && (said.Count == 0 || said[said.Count - 1] != again.text)) said.Add(again.text);
            if (!Barks.Choosing) Barks.Advance();
        }
        Note("he said: " + string.Join(" / ", said.Select(s => "\"" + s + "\"")));
        Check(said.Count > 0 && lines.Pool(LodgerStory.SpeakerId, LodgerStory.VerdictHeld).Any(l => l.text == said[0]), "his first line is his verdict on the morning");
        if (mode == Mode.Note)
        {
            Check(answered == 0 && said.Any(s => lines.Pool(LodgerStory.SpeakerId, LodgerStory.Cold).Any(l => l.text == s))
                  && said.Contains(lines.FindLine(favour.remind)?.text), "he asks again, colder: a cold line, then what he wants (no reply)");
            Check(ledger.AskedOn == 1 && ledger.LastAsked == 2, $"asked again tonight (first asked on Night {ledger.AskedOn})");
        }
        else
        {
            Check(said.Any(s => s == lines.FindLine(favour.firstLine)?.text), "he asks for the cups");
            Check(said.Any(s => s == lines.FindLine("lodger.night2.w02")?.text), "warm (2), he says why before Ace asks");
            Check(answered == 1 && ledger.Warmth == warmth + 1, $"Ace answered once, warmly (+1: his warmth {ledger.Warmth})");
            Check(saves.Notebook.Knows(cups.id) && saves.Notebook.Find(cups.id).text == cups.notebookMention, "the notebook learned where her cups are (his word)");
            Check(ledger.AskedOn == 2 && ledger.LastAsked == 2 && LodgerStory.Errand(ledger) == cups.id, "asked tonight: the cups are Ace's errand");
        }
        yield return Until(() => zero.Now == NightZero.Step.Done && !NightZero.Pending, 3f, "his scene is over");
        Check(!NightWalk.Instance.ClockHeld, "the night's clock runs");
        yield return Until(() => man.Hidden && set.LidOpen(set.farLid) < .01f, 3f, "his say done, he ducks back into the dumpster and the lid drops");
        yield return Seconds(1f);
        Check(NoteShowing().Contains("kitchen"), $"the note says where the cups are (\"{Short(NoteShowing())}\")");
        yield return Photo("04-after-his-ask");

        if (mode != Mode.Cups)
        {
            yield return Stalling(man);
            yield break;
        }

        // ---------- Grace's house: never through a wall ----------
        HomeDoor door = HomeDoor.Find("home.grace");
        Vector3 stoop = house.World(1.11f, 5.30f);
        yield return Walk(new List<Vector3>
        {
            new Vector3(set.binSpot.position.x, 0f, BackStreet), new Vector3(WestStreet, 0f, BackStreet), new Vector3(WestStreet, 0f, stoop.z), stoop,
        }, new List<float> { .8f, .9f, .6f, .35f }, "along Back Street and down West Street to Grace's stoop");
        Vector3 there = movement.transform.position;
        MoveAce(house.World(2.93f, -.75f));
        yield return null;
        yield return null;
        yield return null;
        Check(!trophy.Seen && !trophy.IsAvailable && interactor.CurrentPrompt != "Take " + cups.name,
            $"behind her kitchen wall, a metre from the cups, they aren't offered (seen: {trophy.Seen}; the prompt \"{interactor.CurrentPrompt}\")");
        MoveAce(there);
        yield return Seconds(.3f);
        // ---------- she's home (break-ins chunk C): Ace waits on her stoop until she's asleep, as a player would ----------
        GraceAtHome atHome = GraceAtHome.Instance;
        if (atHome != null && atHome.NightOn)
        {
            Check(atHome.Home && !atHome.Asleep, $"Grace is home and up ({atHome.Doing}; {atHome.LightsNow()})");
            yield return Photo("04b-her-front-window-lit");
            float waitFrom = Time.realtimeSinceStartup;
            yield return Until(() => atHome.Asleep, 120f, "Ace waits on her stoop until she's asleep (her bedroom window goes dark)");
            Note($"she was asleep {Time.realtimeSinceStartup - waitFrom:0} s later, at {GraceNight.Clock(NightWalk.Instance.Hour)}");
            yield return Photo("04c-her-house-dark-she-is-asleep");
        }
        else Note("Grace isn't at home in this scene (no GraceAtHome): straight in");
        yield return Until(() => house.DoorPromptShown && house.DoorPrompt == "Let yourself in", 4f, "at her stoop the way in is offered");
        house.LetAceIn();
        yield return Until(() => house.door == null || house.door.IsOpen, 3f, "her door opened");
        // Sneaking inside, as a player would with her asleep upstairs (walking, a step near the kitchen's back corner is within
        // 4 m of her pillow).
        movement.ScriptedSneak = true;
        yield return Walk(Plan(house, (1.11f, 4.05f), (1.11f, 3.35f), (2.06f, 3.20f), (2.45f, 2.75f), (2.22f, 2.08f), (3.00f, 2.02f), (3.28f, 1.50f), (3.20f, 1.30f)),
            new List<float> { .35f, .3f, .3f, .3f, .3f, .3f, .3f, .25f }, "in at her door and through to the kitchen, sneaking");
        string take = "Take " + cups.name;
        yield return Until(() => interactor.CurrentPrompt == take, 3f, $"at the worktop the prompt reads \"{take}\" (Ace knows them from his ask)");
        Check(trophy.Seen, "in the kitchen Ace can see them");
        yield return Photo("05-grace-kitchen-the-cups");
        Press();
        yield return null;
        yield return null;
        Check(trophy.Taken && ledger.HasTrophy(cups.id) && !trophy.visual.activeSelf, "E took them: Ace's deed, gone from her worktop");
        Check(NightCarry.Current != null && NightCarry.Current.HeldId == cups.id && shelf.Showing == 0, "in Ace's hand, for him, not on the shelf");
        Check(saves.Notebook.Find(cups.id + ".taken")?.text == cups.notebookTakenFor, "the notebook says who they were for");
        Check(NoteShowing().Contains("back to the man at the bins"), $"the note says where to take them (\"{Short(NoteShowing())}\")");
        string aceLine = lines.FindLine(favour.aceLine)?.text ?? "?";
        Check(Barks.TryGetShown(movement.transform, out Barks.Shown mine) && mine.text == aceLine, $"Ace mutters \"{aceLine}\"");
        yield return Seconds(.6f);
        yield return Photo("06-the-cups-in-hand");

        // ---------- back to the bins ----------
        yield return Walk(Plan(house, (3.20f, 1.85f), (2.45f, 2.30f), (2.30f, 2.95f), (1.90f, 3.25f), (1.11f, 3.35f), (1.11f, 4.05f), (1.11f, 5.30f)),
            new List<float> { .3f, .3f, .3f, .3f, .3f, .35f, .4f }, "out of the kitchen and out of her door, sneaking");
        movement.ScriptedSneak = null;
        if (atHome != null && atHome.NightOn)
            Check(atHome.Asleep && atHome.Catches == 0 && atHome.Wakes == 0, $"she slept through it ({atHome.Doing}; woken {atHome.Wakes} times)");
        yield return Walk(new List<Vector3>
        {
            new Vector3(WestStreet, 0f, stoop.z), new Vector3(WestStreet, 0f, BackStreet), new Vector3(set.giveSpot.position.x, 0f, BackStreet), set.giveSpot.position,
        }, new List<float> { .6f, .9f, .6f, .25f }, "back up West Street and along Back Street to his half of the dumpster");
        string give = "Give him " + cups.name;
        yield return Until(() => interactor.CurrentPrompt == give, 3f, $"at his half the prompt reads \"{give}\"");
        Check(man.Up && man.PoppedFor > 0f, $"he popped up as Ace came back with the cups, {man.PoppedFor:0.0} m from his half");
        yield return Photo("07-give-him-the-cups");

        // ---------- the return: four cups on the crate ----------
        warmth = ledger.Warmth;
        Press();
        yield return Until(() => Barks.ScenePlaying && man.Returning, 2f, "E: the return began");
        int mostCups = 0, cupsWhenPaid = -1;
        bool replied = false, cupsPhoto = false;
        float returnStart = Time.realtimeSinceStartup;
        while (Barks.ScenePlaying && Time.realtimeSinceStartup - returnStart < 60f)
        {
            mostCups = Mathf.Max(mostCups, man.CupsOut);
            if (Barks.Choosing)
            {
                yield return Until(() => Barks.ChipsUp, 3f, "the return's replies come up");
                Check(NightCarry.Current == null || !NightCarry.Current.Holding, "he took them: Ace's hand is empty");
                Check(ledger.HasGiven(cups.id) && man.CupsOut == 0, "they're his, and the crate is still empty (he sets them out on his line)");
                yield return Seconds(.3f);
                yield return Answer(0);
                replied = true;
                continue;
            }
            yield return Seconds(1.6f);   // long enough for the cups to go out one by one
            mostCups = Mathf.Max(mostCups, man.CupsOut);
            if (!cupsPhoto && man.CupsOut == 4) { cupsPhoto = true; yield return Photo("08-four-cups-on-the-crate"); }
            if (cupsWhenPaid < 0 && ledger.Knows(LodgerStory.Feet)) cupsWhenPaid = man.CupsOut;
            if (!Barks.Choosing) Barks.Advance();
        }
        yield return Until(() => !man.Returning, 3f, "the return ended");
        Check(replied && ledger.Warmth == warmth + 1, $"Ace answered once, warmly (+1: his warmth {ledger.Warmth})");
        Check(mostCups == 4 && man.CupsOut == 4, $"he set four cups out on the crate ({mostCups})");
        Check(ledger.Knows(LodgerStory.Feet) && !ledger.Knows(LodgerStory.Doors), "he taught Ace Soft feet (Doors waits for the keys)");
        Check(!saves.Notebook.Knows("lodger.page.grace.photos"), "no page about her photos: they aren't in the game yet");
        Check(ledger.Favour == LodgerStory.Cones && LodgerStory.Errand(ledger) == "", "the cups are done: his next favour comes up (the cones, not in the game yet)");
        Note($"Soft feet came with {cupsWhenPaid} cup(s) out");
        yield return Until(() => man.Hidden, 4f, "a moment after the return he's back in the bin, the lid shut");
        yield return Seconds(.4f);
        yield return Photo("09-his-corner");

        // ---------- calling it a night ----------
        yield return Walk(new List<Vector3> { new Vector3(6.25f, 0f, 20.2f), set.outsideDoor.position }, new List<float> { .35f, .3f }, "to the back door");
        yield return Until(() => interactor.CurrentPrompt == "Call it a night", 3f, "at the back door the prompt reads \"Call it a night\"");
        Press();
        yield return Until(() => NightCycle.Instance.Now == NightCycle.Phase.Day && DayClock.Instance.Day == 3 && !DayClock.Instance.DayOver, 15f,
            "E called it a night: Day 3 opened");
        if (!lastWait) yield break;
        Check(ledger.Nights == 2 && ledger.HasGiven(cups.id) && ledger.Knows(LodgerStory.Feet) && ledger.Skips == 0 && !ledger.VisitDue(3) && !ledger.MessDue(3),
            $"the night's record: two nights, the cups given, Soft feet, no skip and nothing due (warmth {ledger.Warmth})");
        Check(shelf.Showing == 0 && !set.cornerCups.activeSelf && !trophy.visual.activeSelf, "by day: nothing on Ace's shelf, his corner put away, her worktop without the sleeve");
        SaveData disk = null;
        try { disk = SaveCheckpointStorage.Read(Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName)); }
        catch (Exception e) { Note("could not read the lab save: " + e.Message); }
        Check(disk != null && disk.day == 3 && disk.night.given.Contains(cups.id) && disk.night.lessons.Contains(LodgerStory.Feet)
              && disk.night.favour == LodgerStory.Cones && disk.night.deeds.Any(d => d.thing == cups.id && !d.faced),
            "Day 3's save has it all (the cups given, Soft feet, his next favour, Grace still to come in)");
        yield return Seconds(2.2f);
        yield return Photo("10-day-3-morning");

        // ---------- Grace, about her cups ----------
        CustomerBrain grace = null;
        yield return Until(() => (grace = FindRegular(Grace)) != null, 60f, "Grace comes in first on Day 3");
        if (!lastWait) yield break;
        yield return Until(() => grace == null || grace.CanHearIntake, 150f, "she reaches the counter");
        if (!lastWait || grace == null) yield break;
        conversation.Begin(grace);
        yield return null;
        MorningFace face = conversation.Face;
        Check(face != null && face.Thing == cups && face.Now == MorningFace.Step.Complaint, "the morning scene opens her visit: her cups");
        if (face == null) yield break;
        yield return Until(() => face.Now == MorningFace.Step.Meter, 15f, "the straight-face meter appears");
        if (!lastWait) yield break;
        Check(face.Nerve && Mathf.Abs(face.Meter.Green - LodgerStory.Green(cups.green, true)) < .001f && cups.green < NightThings.GnomeOfGrace.green,
            $"with Nerve the green is wider ({face.Meter.Green:0.000}); the cups' own ({cups.green:0.00}) is narrower than Barnaby's");
        yield return Seconds(.35f);
        yield return Photo("11-grace-her-cups");
        yield return Hold(face.Meter);
        yield return Until(() => face.Now == MorningFace.Step.Result || face.Now == MorningFace.Step.Done, 4f, "the needle stopped");
        Check(face.Meter.Held && ledger.Deeds.Any(d => d.thing == cups.id && d.faced && !d.cracked), "Ace kept a straight face");
        yield return Photo("12-straight-face");
        yield return Until(() => conversation.Face == null, 8f, "her scene is over: on to her order");
        yield return Until(() => conversation.RepliesShowing, 8f, $"she orders (\"{Short(conversation.CurrentLine)}\")");
        conversation.ChooseReply(0);
        yield return Until(() => !conversation.InConversation, 8f, "her order taken, the conversation closes");

        // ---------- the officer: his question after his order ----------
        float dayLength = Field<object>(DayClock.Instance, "dayLengthSeconds") is float f ? f : 180f;
        CustomerBrain officer = null;
        yield return Until(() => (officer = FindRegular(OfficerStory.ProfileId)) != null, dayLength * .3f + 90f, "the officer comes in on Day 3");
        if (!lastWait) yield break;
        PolygonNpcVisual look = officer.GetComponent<PolygonNpcVisual>();
        Check(look != null && look.ActiveAppearanceName == "Character_Male_Police", $"in his own look ({(look != null ? look.ActiveAppearanceName : "none")})");
        yield return Until(() => officer == null || officer.CanHearIntake, 150f, "he reaches the counter");
        if (!lastWait || officer == null) yield break;
        conversation.Begin(officer);
        yield return null;
        Check(conversation.Face == null, "no scene before his order");
        yield return Until(() => conversation.RepliesShowing, 8f, $"he orders (\"{Short(conversation.CurrentLine)}\")");
        yield return Photo("13-the-officer-orders");
        conversation.ChooseReply(0);
        yield return null;
        MorningFace question = conversation.Face;
        OfficerStory.Question asked = OfficerStory.Find("officer.description");
        Check(question != null && question.AfterOrder && question.Question == asked, "after his order, he asks his question");
        if (question == null) yield break;
        yield return Until(() => question.Now == MorningFace.Step.Meter, 15f, "the meter appears");
        if (!lastWait) yield break;
        Check(TitleShowing().Contains(asked.title) && TitleShowing().Contains("Nerve"), $"its title says what to do: \"{TitleShowing()}\"");
        Check(Mathf.Abs(question.Meter.Green - LodgerStory.Green(asked.green, true)) < .001f, $"his question's green, with Nerve ({question.Meter.Green:0.000})");
        yield return Seconds(.35f);
        yield return Photo("14-say-nothing");
        yield return Hold(question.Meter);
        yield return Until(() => question.Now == MorningFace.Step.Result, 4f, "the needle stopped");
        Check(question.Meter.Held && TitleShowing().Contains(asked.heldWord), $"Ace said nothing (\"{TitleShowing()}\")");
        yield return Photo("15-said-nothing");
        yield return Until(() => !conversation.InConversation, 10f, "after his answer the conversation closes");
        QuestionData record = ledger.QuestionOn(3);
        Check(record != null && record.id == asked.id && !record.cracked && ledger.Suspicion(OfficerStory.ProfileId) == 0,
            "the night's record: his question asked on Day 3, kept; he isn't suspicious");
        Check(saves.Notebook.Knows(asked.id) && !saves.Notebook.Knows(asked.id + ".flinched"), "the notebook has his description");
        report.AppendLine();
        report.AppendLine("The night's ledger now: " + JsonUtility.ToJson(ledger.Snapshot()));
    }

    // Visit and Note: the night ends without the cups, and the morning after costs the day (playtest 3).
    IEnumerator Stalling(Lodger man)
    {
        yield return Walk(new List<Vector3> { new Vector3(6.25f, 0f, 20.2f), set.outsideDoor.position }, new List<float> { .35f, .3f }, "straight back to the back door");
        yield return Until(() => interactor.CurrentPrompt == "Call it a night", 3f, "at the back door the prompt reads \"Call it a night\"");
        Press();
        yield return Until(() => NightCycle.Instance.Now == NightCycle.Phase.Day && DayClock.Instance.Day == 3 && !DayClock.Instance.DayOver, 15f,
            "E called it a night without the cups: Day 3 opened");
        if (!lastWait) yield break;
        LodgerDay day = LodgerDay.Instance;
        Check(day != null, "the man by day is looked after (LodgerDay, on the patron spawner)");
        if (day == null) yield break;
        float length = Field<object>(DayClock.Instance, "dayLengthSeconds") is float g ? g : 180f;
        LodgerStory.Favour cups = LodgerStory.FindFavour(NightThings.GraceCups);

        if (mode == Mode.Note)
        {
            Check(ledger.Skips == LodgerStory.WordAtSkips && ledger.VisitDue(3) && ledger.MessDue(3) && ledger.WordDue(3) && ledger.NoteDue(3)
                  && ledger.Favour == NightThings.GraceCups && ledger.Dropped.Count == 0,
                $"the third skip: his table, his mess, his word and his note all due on Day 3; the cups are still what he wants");

            // His mess: a used cup on every seat near the door, the seats out of play, sorted into tables with a badge over
            // each; one note that names him; then his note on the counter, a beat later.
            yield return Until(() => day.CupsLaid > 0, 6f, "the café opens with his mess");
            if (!lastWait) yield break;
            TableSeat[] seats = FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude);
            int dirty = seats.Count(t => t.IsDirty);
            Check(day.CupsLaid == Mathf.Min(seats.Length, LodgerStory.MessCupsAtMost) && dirty == day.CupsLaid && seats.Where(t => t.IsDirty).All(t => !t.IsAvailable),
                $"a used cup on every table seat near the door ({day.CupsLaid} of {seats.Length}); a seat with a cup can't be sat in");
            // The tables, seat by seat: the seats don't know their tables, so a wrong guess shows here.
            foreach (MessTable t in day.Tables)
                Note($"a table with {t.cups.Count} of his cups: " + string.Join(", ", t.cups.Where(c => c != null).Select(c => $"{c.seat.name} {Where(c.transform.position)}")));
            int tables = day.Tables.Count;
            Check(tables >= 1 && tables < day.CupsLaid && day.Tables.Sum(t => t.cups.Count) == day.CupsLaid && day.TablesLeft == tables,
                $"his cups sorted into {tables} table(s), every cup on exactly one");
            yield return Seconds(.4f);   // the badges spring up
            int badges = day.Tables.Count(t => Juice.MarkShowing(t.Badge, out _));
            Check(badges == tables && Juice.MarksUp == tables, $"a badge over each table ({badges} of {tables} showing; {Juice.MarksUp} up)");
            yield return Until(() => NoteShowing().Contains(LodgerStory.MessNote), 6f, "the note says so, and whose mess it is");
            Note($"the note: \"{NoteShowing()}\"");
            yield return Photo("05-his-mess");
            yield return Until(() => day.NoteShown, 14f, "then his note on the counter");
            Check(NoteShowing().Contains(cups.note), $"on screen (\"{Short(NoteShowing())}\")");
            NotebookFactData page = saves.Notebook.Find("lodger.note." + NightThings.GraceCups);
            Check(page != null && page.source == Notebook.Sources.Inherited && page.text == cups.note, "and in the notebook, as one of his pages");
            yield return Photo("06-his-note");

            // His review, in the day's reviews already.
            Check(day.ReviewPlanted && saves.Reputation.Count(Review.NeverAgain) >= 1 && saves.Reputation.TodayChange <= -2,
                $"his one-star review is in the day already (today {ReputationRecap.Signed(saves.Reputation.TodayChange)})");

            // Ace clears one table: E at any cup on it clears every cup on it (one press a table).
            DirtyCup cup = FindObjectsByType<DirtyCup>(FindObjectsInactive.Exclude).OrderBy(c => Flat(c.transform.position - new Vector3(0f, 0f, 9f)).sqrMagnitude).FirstOrDefault();
            Check(cup != null && cup.seat != null && cup.table != null, "one of his cups is on a table");
            if (cup == null || cup.table == null) yield break;
            MessTable table = cup.table;
            List<TableSeat> tableSeats = table.cups.Where(c => c != null && c.seat != null).Select(c => c.seat).ToList();
            int onIt = table.CupsLeft, left = day.CupsLeft, tablesLeft = day.TablesLeft, badge = table.Badge;
            Vector3 fromCup = Flat(new Vector3(0f, 0f, 9f) - cup.transform.position).normalized;
            MoveAce(cup.transform.position + fromCup * .9f);
            yield return Until(() => interactor.CurrentPrompt == LodgerStory.ClearPrompt, 4f, $"at the table the prompt reads \"{LodgerStory.ClearPrompt}\"");
            Press();
            yield return Until(() => tableSeats.All(t => !t.IsDirty), 3f, $"one press clears the whole table: its {onIt} seat(s) are free again");
            Check(day.CupsLeft == left - onIt && day.TablesLeft == tablesLeft - 1,
                $"{day.CupsLeft} of his cups left (from {left}), on {day.TablesLeft} table(s) (from {tablesLeft})");
            yield return null;
            Check(badge != 0 && !Juice.MarkShowing(badge, out _) && Juice.MarksUp == day.TablesLeft
                  && day.Tables.Where(t => !t.Cleared).All(t => Juice.MarkShowing(t.Badge, out _)),
                $"its badge comes down; the other tables keep theirs ({Juice.MarksUp} up)");
            yield return Photo("07-one-table-cleared");

            // His table, from opening.
            yield return Until(() => day.Visitor != null, length * .15f + 30f, "he comes in from opening and takes a table");
            if (!lastWait) yield break;
            Check(day.Visitor.GetComponent<CustomerBrain>() == null, "he isn't a customer (no order, no ticket)");

            // The officer, for his word: a harder question after his order.
            // The schedule brings him in on Day 3 anyway (The officer 1): the word rides on that visit, no second one.
            CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
            Check(spawner != null && spawner.WordVisitor == null && !spawner.WordVisitorCame, "the schedule brings the officer in today anyway: no second visit for the word");
            CustomerBrain officer = null;
            yield return Until(() => (officer = FindRegular(OfficerStory.ProfileId)) != null, length * .3f + 90f, "the officer comes in on Day 3");
            if (!lastWait) yield break;
            yield return Until(() => officer == null || officer.CanHearIntake, 150f, "he reaches the counter");
            if (!lastWait || officer == null) yield break;
            conversation.Begin(officer);
            yield return null;
            yield return Until(() => conversation.RepliesShowing, 8f, $"he orders (\"{Short(conversation.CurrentLine)}\")");
            conversation.ChooseReply(0);
            yield return null;
            MorningFace question = conversation.Face;
            string wordId = OfficerStory.WordQuestionId(NightThings.GraceCups);
            Check(question != null && question.AfterOrder && question.Question != null && question.Question.id == wordId && question.Question.afterWord,
                $"after his order, the word: \"{Short(question != null && question.Question != null ? question.Question.question.Replace("\n", " ") : "")}\"");
            if (question == null) yield break;
            yield return Until(() => question.Now == MorningFace.Step.Meter, 15f, "the meter appears");
            if (!lastWait) yield break;
            OfficerStory.Question description = OfficerStory.Find("officer.description");
            Check(question.Meter.Green < LodgerStory.Green(description.green, true) + .001f, $"harder than his description ({question.Meter.Green:0.000})");
            yield return Seconds(.35f);
            yield return Photo("08-his-word");
            yield return Hold(question.Meter);
            yield return Until(() => question.Now == MorningFace.Step.Result, 4f, "the needle stopped");
            Check(question.Meter.Held, "Ace said nothing");
            yield return Until(() => !conversation.InConversation, 10f, "after his answer the conversation closes");
            Check(ledger.HasAsked(wordId) && ledger.QuestionOn(3)?.id == wordId && saves.Notebook.Knows(wordId),
                "the record and the notebook have the word, asked on Day 3");
            Check(!day.TableNoteShown, "with his mess on the tables, no second note about his table (one note a beat)");
            yield break;
        }

        Check(ledger.Skips == 1 && ledger.VisitDue(3) && !ledger.MessDue(3) && !ledger.WordDue(3) && !ledger.NoteDue(3) && ledger.Favour == NightThings.GraceCups,
            "a skip: his table is due on Day 3, nothing more; the cups are still his favour");
        yield return Until(() => day.Visitor != null, length * .15f + 30f, "he comes in as the day opens");
        if (!lastWait) yield break;
        Check(day.CupsLaid == 0, "no mess after one skip");
        Check(DayClock.Instance.NormalizedDay >= LodgerStory.VisitFrom - .01f && DayClock.Instance.NormalizedDay < .15f,
            $"from opening ({DayClock.Instance.NormalizedDay:0.00} of the day)");
        GameObject visitor = day.Visitor;
        PatronBrain brain = visitor.GetComponent<PatronBrain>();
        PolygonNpcVisual look = visitor.GetComponent<PolygonNpcVisual>();
        yield return Until(() => look == null || look.VisualInstance != null, 5f, "his look is on");
        Check(look != null && look.ActiveAppearanceName == (set.look != null ? set.look.name : "?"), $"in his own look ({(look != null ? look.ActiveAppearanceName : "none")})");
        Check(visitor.GetComponent<CustomerBrain>() == null, "he isn't a customer (no order, no ticket)");
        yield return Until(() => visitor == null || brain.IsSeated, 120f, "he takes a table");
        if (!lastWait || visitor == null) yield break;
        float seatedAt = Time.time, seatedDay = DayClock.Instance.NormalizedDay;
        // No mess to say it, so a note does: he's taken a table, and why.
        yield return Until(() => day.TableNoteShown, 3f, "once he's sat down, a note is put up");
        yield return Until(() => NoteShowing() == LodgerStory.TableNote, 12f, "...and shows, after any note already up");
        Note($"the note: \"{NoteShowing()}\"");
        yield return Photo("05a-his-table-note");
        // Ace walks past his table.
        Vector3 at = visitor.transform.position;
        Vector3 toRoom = Flat(new Vector3(0f, 0f, 9f) - at).normalized;
        MoveAce(at + toRoom * 1.6f);
        yield return Until(() => day.SaidHisLine, 8f, "when Ace passes close he says his line");
        string line = Barks.TryGetShown(visitor.transform, out Barks.Shown shown) ? shown.text : "";
        Check(new[] { LodgerStory.Visit, LodgerStory.VisitWarm, LodgerStory.VisitCold }.Any(p => lines.Pool(LodgerStory.SpeakerId, p).Any(l => l.text == line)),
            $"one of his lines in the café: \"{line}\" (warmth {ledger.Warmth}: {LodgerStory.VisitPool(ledger.Warmth)})");
        yield return Seconds(.3f);
        yield return Photo("05-his-table");
        yield return Until(() => visitor == null || brain.IsLeaving, length * (LodgerStory.VisitUntil - seatedDay) + 25f, "he gets up and goes about 1:30 PM");
        float sat = Time.time - seatedAt, dayWhenUp = DayClock.Instance.NormalizedDay;
        Check(dayWhenUp > LodgerStory.VisitUntil - .08f && dayWhenUp < LodgerStory.VisitUntil + .08f,
            $"he sat all morning: {sat:0} s, up at {dayWhenUp:0.00} of the day (about {LodgerStory.VisitUntil:0.00})");
        yield return Seconds(2f);
        yield return Photo("06-he-leaves");
    }

    // The officer's profile, look and visits (The officer 1).
    void CheckTheOfficerIsSetUp()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        DayDefinition[] schedule = Field<DayDefinition[]>(spawner, "schedule");
        StoryVisit Visit(int day) => schedule?.FirstOrDefault(d => d != null && d.dayNumber == day)?.StoryVisitsOn(day)
            .FirstOrDefault(v => v?.who != null && v.who.PersistentId == OfficerStory.ProfileId);
        StoryVisit first = Visit(1), third = Visit(3);
        Check(first != null && third != null && third.arrivesAt <= .3f, $"the officer comes in on Day 1 ({(first != null ? first.arrivesAt.ToString("0.00") : "no")}) " +
              $"and Day 3 ({(third != null ? third.arrivesAt.ToString("0.00") : "no")}) (The officer 1)");
        CustomerProfile officer = first?.who ?? third?.who;
        Check(officer != null && officer.StandInLook == "Character_Male_Police" && officer.primaryVisitKind == RegularVisitKind.DrinkOnly
              && officer.lines != null && officer.lines.orderedDrink != null && officer.lines.orderedDrink.Any(l => l.Contains("second batch")),
            "his profile: the police look, a coffee, and the joke about the second batch on Day 1");
        GameObject customer = Field<GameObject>(spawner, "customerPrefab");
        PolygonNpcVisual visual = customer != null ? customer.GetComponent<PolygonNpcVisual>() : null;
        GameObject[] looks = Field<GameObject[]>(visual, "appearancePrefabs");
        Check(looks != null && looks.Any(l => l != null && l.name == "Character_Male_Police"), "the café's customers can wear his look (Customer.prefab)");
        Check(CustomerProfile.IsStandInLook("Character_Male_Police"), "and it's kept for him: nobody else wears it");
        NightLines.Scene deal = lines.FindScene(LodgerStory.DealScene);
        Check(deal != null && deal.lines.Contains("lodger.night0.12"), "Night 0's deal ends with the cop line (Barks 1)");
    }

    // ---------- the meter ----------

    // A steady player: stop the needle in the middle of the green.
    IEnumerator Hold(StraightFaceMeter meter)
    {
        float giveUp = Time.realtimeSinceStartup + meter.Patience + 2f;
        while (!meter.Stopped && Time.realtimeSinceStartup < giveUp)
        {
            if (Mathf.Abs(meter.Needle - meter.GreenCentre) < meter.Green * .25f) meter.Stop();
            yield return null;
        }
    }

    string TitleShowing()
    {
        object ui = typeof(StraightFaceUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
        TMP_Text title = Field<TMP_Text>(ui, "title");
        return title != null && title.gameObject.activeInHierarchy ? title.text ?? "" : "";
    }

    // ---------- driving Ace ----------

    void Press() => movement.SendMessage("OnInteract", SendMessageOptions.DontRequireReceiver);

    // Reply 0 or 1: the key (1 or 2) when the Game view has the keyboard, as a player would; otherwise directly.
    IEnumerator Answer(int reply)
    {
        if (Application.isFocused && Keyboard.current != null)
        {
            var state = new KeyboardState(reply == 0 ? Key.Digit1 : Key.Digit2);
            InputSystem.QueueStateEvent(Keyboard.current, state);
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Seconds(.3f);
            if (!Barks.Choosing) { Note($"picked reply {reply + 1} with the {reply + 1} key"); yield break; }
        }
        Barks.Choose(reply);
        Note($"picked reply {reply + 1} directly (the Game view didn't have the keyboard)");
        yield return Seconds(.3f);
    }

    static List<Vector3> Plan(GraceHouse house, params (float X, float Y)[] stops) => stops.Select(s => house.World(s.X, s.Y)).ToList();

    IEnumerator Walk(List<Vector3> points, List<float> radii, string what)
    {
        int stuck = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 target = points[i];
            float radius = radii[Mathf.Min(i, radii.Count - 1)];
            float deadline = Time.realtimeSinceStartup + 30f;
            float checkAt = Time.realtimeSinceStartup + 1.5f;
            Vector3 checkFrom = movement.transform.position;
            bool gotStuck = false;
            while (true)
            {
                Vector3 p = movement.transform.position;
                Vector2 to = new Vector2(target.x - p.x, target.z - p.z);
                if (to.magnitude <= radius) break;
                if (Time.realtimeSinceStartup > deadline) { gotStuck = true; break; }
                Vector3 direction = new Vector3(to.x, 0f, to.y).normalized;
                Vector3 local = Quaternion.Euler(0f, -view.MovementYaw, 0f) * direction;
                movement.ScriptedInput = new Vector2(local.x, local.z);
                if (Time.realtimeSinceStartup >= checkAt)
                {
                    if (Vector3.Distance(checkFrom, p) < .25f) { gotStuck = true; break; }
                    checkAt = Time.realtimeSinceStartup + 1.5f;
                    checkFrom = p;
                }
                yield return null;
            }
            movement.ScriptedInput = Vector2.zero;
            if (gotStuck)
            {
                stuck++;
                Note($"STUCK at {Where(movement.transform.position)} on the way to {Where(target)}; moved Ace on there");
                MoveAce(target);
                yield return null;
            }
        }
        movement.ScriptedInput = null;
        Check(stuck == 0, $"Ace walked {what}" + (stuck == 0 ? "" : $" (stuck {stuck} time(s))"));
    }

    void MoveAce(Vector3 at)
    {
        var controller = movement.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        movement.transform.position = new Vector3(at.x, movement.transform.position.y + .05f, at.z);
        if (controller != null) controller.enabled = was;
    }

    // ---------- what's on screen ----------

    Button RecapButton() => Field<Button>(FindAnyObjectByType<RecapUI>(), "nextDayButton");
    bool RecapShowing() { GameObject panel = Field<GameObject>(FindAnyObjectByType<RecapUI>(), "panel"); return panel != null && panel.activeInHierarchy; }

    string NoteShowing()
    {
        NightCycle cycle = NightCycle.Instance;
        TMP_Text note = Field<TMP_Text>(cycle, "note");
        RectTransform box = Field<RectTransform>(cycle, "noteBox");
        return note != null && box != null && box.gameObject.activeInHierarchy ? note.text ?? "" : "";
    }

    static CustomerBrain FindRegular(string id)
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain.Identity != null && brain.Identity.Profile != null && brain.Identity.Profile.PersistentId == id)
                return brain;
        return null;
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

    // ---------- waiting, photos and the report ----------

    IEnumerator Until(Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
        Check(lastWait, what + (lastWait ? "" : $" (gave up after {seconds:0} s)"));
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
        if (NightCycle.Instance != null) report.AppendLine(NightCycle.Instance.Describe());
        if (LodgerDay.Instance != null) report.AppendLine(LodgerDay.Instance.Describe());
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Night 2 check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Night 2 check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Night 2 check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) { movement.ScriptedInput = null; movement.ScriptedSneak = null; }
    }
}
