using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// NIGHT 0 CHECK: THE BINS, THE DEAL, THE ERRAND AND THE RETURN, DRIVEN (a lab session only: Fixit Fidget > Night >
// Night 0 - Play check; claude/the-man-at-the-bins-story.md)
//
// From Day 1's recap (NightOneSteps' made-up Day 1: Grace mentioned Barnaby) to Grace's visit on Day 2:
//   1. "Close up for the night": Ace is just inside the café's back door with the bin bag, the night's clock waits,
//      and nothing else is offered (not Grace's gnome, not calling it a night);
//   2. "Take the bins out": a blink, and Ace is out on Back Street; the camera looks at the bins from the street;
//   3. "Bin it": the near lid lifts, the bag goes in, the lid drops; the lamp stutters, the view pushes in, the far
//      lid lifts and he stands up out of the dumpster;
//   4. the deal: two replies (1 and 2 on the keyboard when the Game view has it, as a player would), his warmth
//      nudged by each (+1, then 0), the notebook's first pages his (in italics, first on its page), the deal made,
//      the clock running, Night 0 over;
//   5. the errand: Barnaby taken from Grace's step goes into Ace's hand (not onto the shelf), and in first person he sits
//      low on the right of the view, looking back at Ace (photo 09b: his hat the right way out, 6 Oct 2026); back at the
//      bins he pops up as Ace comes (he ducked back in after the deal), and "Give him Barnaby": the return (one reply,
//      +1), the gnome in his corner turned to face the street, Nerve learned, a page for the notebook, and he ducks again;
//   6. "Call it a night" at the back door; Day 2's save has all of it; Grace comes in, and the straight face's green is
//      wider (Nerve).
// Also: standing near him adds no garbage a frame. A photo at each step and report.txt go to
// Logs/Night/night-zero-check-<time>/. Nothing is saved in the scene; the lab save is the only file written (by the game).
// ---------------------------------------------------------------------------
public sealed class NightZeroCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.NightZero.Check";

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

#if UNITY_EDITOR
    // Asked for by the editor (NightZeroSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night 0 check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Night 0 check (this Play session only)");
        go.AddComponent<NightZeroCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            $"night-zero-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", $"night-zero-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine("Night 0 - play check (the bins, the deal, the errand, the return), lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Night 0 check] Running: about three or four minutes. Hands off the mouse and keyboard until it reports. {folder}");

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
        // ---------- Day 1's recap ----------
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapShowing(), 15f, "the lab opened on Day 1's recap");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        saves = SaveManager.Instance;
        ledger = saves != null ? saves.Night : null;
        set = NightZeroSet.Instance;
        Check(movement != null && interactor != null && view != null && conversation != null && ledger != null, "Ace and the save manager are in the scene");
        Check(set != null && set.insideDoor != null && set.outsideDoor != null && set.nearLid != null && set.farLid != null && set.inside != null,
            "the bins are in the scene (Bins 1): " + (set != null ? set.Describe() : "none"));
        NightLines lines = NightLines.Current;
        Check(lines != null && lines.FindScene(LodgerStory.DealScene) != null && lines.FindScene(LodgerStory.ReturnScene) != null
              && lines.FindScene(LodgerStory.DealScene).choices != null && lines.FindScene(LodgerStory.DealScene).choices.Length == 2,
            "the Night lines have the deal (with its two replies) and the return (Barks 1)");
        if (movement == null || interactor == null || view == null || ledger == null || set == null || lines == null) yield break;
        Check(!ledger.MetHim && ledger.Warmth == 0 && ledger.Lessons.Count == 0, "nobody has met him yet");
        NightThing barnaby = NightThings.GnomeOfGrace;
        // The gnome by its id: since session 3 there is more than one thing to take (Grace's cups).
        NightTrophy gnome = FindObjectsByType<NightTrophy>(FindObjectsInactive.Include).FirstOrDefault(t => t.thingId == barnaby.id);
        TrophyShelf shelf = FindAnyObjectByType<TrophyShelf>();
        HomeDoor door = HomeDoor.Find("home.grace");
        Check(gnome != null && shelf != null && door != null, "Grace's gnome, Ace's shelf and Grace's door are in the scene");
        if (gnome == null || shelf == null || door == null) yield break;
        Check(saves.Notebook.Knows(barnaby.id), "the notebook has what Grace said about Barnaby on Day 1");
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        yield return Photo("01-day-1-recap");

        // ---------- Night 0: at the back door with the bag ----------
        RecapButton().onClick.Invoke();
        yield return Until(() => NightCycle.Instance != null && NightCycle.Instance.Now == NightCycle.Phase.Night, 12f, "the recap's button began the night");
        if (!lastWait) yield break;
        NightZero zero = NightZero.Instance;
        Lodger man = Lodger.Instance;
        Check(zero != null && NightZero.Pending && zero.Now == NightZero.Step.AtTheDoor, "Night 0 opened the night (" + (zero != null ? zero.Describe() : "none") + ")");
        Check(man != null && !man.Up, "the man at the bins is there, hidden in the dumpster");
        if (zero == null || man == null) yield break;
        Check(Flat(movement.transform.position - set.insideDoor.position).magnitude < .3f, $"Ace is just inside the back door ({Where(movement.transform.position)})");
        Check(NightCarry.Current != null && NightCarry.Current.HeldId == NightZero.BagId, "with the bin bag in hand");
        Check(NightWalk.Instance.ClockHeld, "the night's clock waits");
        float hour = NightWalk.Instance.Hour;
        yield return Seconds(1.6f);   // the fade in
        Check(Mathf.Approximately(hour, NightWalk.Instance.Hour), $"…it hasn't moved ({hour:0.00})");
        Check(NoteShowing().Contains("the bins"), $"the note says what to do (\"{NoteShowing()}\")");
        Check(!gnome.IsAvailable && !NightCycle.Instance.CanCallItANight, "nothing else is offered yet: not Grace's gnome, not calling it a night");
        yield return Until(() => interactor.CurrentPrompt == "Take the bins out", 4f, "the prompt reads \"Take the bins out\"");
        yield return Photo("02-night-0-at-the-back-door");

        // ---------- any door (playtest 3): out the front with the bag, the dumpster would take it; back in, the back door offers the way ----------
        Vector3 insideBefore = movement.transform.position;
        MoveAce(new Vector3(.12f, 0f, -2.2f));   // on the street in front of the café's door
        yield return null;
        yield return null;
        Check(zero.Now == NightZero.Step.Outside && zero.CanBinIt, "out the front door with the bag, the step follows Ace: \"Bin it\" would be offered at the dumpster");
        MoveAce(insideBefore);
        yield return null;
        yield return null;
        Check(zero.Now == NightZero.Step.AtTheDoor, "back inside, the back door is the way again");
        yield return Until(() => interactor.CurrentPrompt == "Take the bins out", 3f, "…and its prompt is back");

        // ---------- out the back ----------
        Press();
        yield return Until(() => zero.Now == NightZero.Step.Outside && Flat(movement.transform.position - set.outsideDoor.position).magnitude < .3f, 3f,
            "E took Ace out through the back door (a blink) onto Back Street");
        yield return Until(() => man.InBinsView, 2f, "round the back, the camera looks at the bins from the street");
        yield return Seconds(1.2f);   // the camera's turn
        yield return Photo("03-out-on-back-street");
        yield return Walk(new List<Vector3> { new Vector3(6.25f, 0f, 20.2f), set.binSpot.position }, new List<float> { .35f, .25f }, "to the dumpster's near half");
        yield return Until(() => interactor.CurrentPrompt == "Bin it", 3f, "at the dumpster the prompt reads \"Bin it\"");
        yield return Photo("04-bin-it");

        // ---------- the bins ----------
        float nearMost = 0f, farMost = 0f;
        bool sawCamera = false, sawHimRise = false, heldDuring = true, revealPhoto = false;
        float binned = Time.realtimeSinceStartup;
        Press();
        while (zero.Now != NightZero.Step.Deal && zero.Now != NightZero.Step.Done && Time.realtimeSinceStartup - binned < 12f)
        {
            nearMost = Mathf.Max(nearMost, Angle(set.nearLid));
            farMost = Mathf.Max(farMost, Angle(set.farLid));
            sawCamera |= set.revealCamera != null && set.revealCamera.activeSelf;
            sawHimRise |= man.Up;
            if (zero.Now == NightZero.Step.Binning || zero.Now == NightZero.Step.Reveal) heldDuring &= PlayerMovement.Held;
            if (!revealPhoto && man.Up && set.revealCamera != null && set.revealCamera.activeSelf && farMost > 60f)
            {
                revealPhoto = true;
                yield return Photo("05-the-lid-lifts");
            }
            yield return null;
        }
        Check(zero.Now == NightZero.Step.Deal || zero.Now == NightZero.Step.Done, $"the reveal ran its course ({zero.RevealSeconds:0.0} s from the bag to his first line)");
        Check(nearMost > 40f, $"the near lid lifted for the bag ({nearMost:0}°)");
        Check(NightCarry.Current == null || !NightCarry.Current.Holding, "the bag went in: Ace's hand is empty");
        Check(sawCamera, "the view pushed in for the lid (the reveal camera came on)");
        Check(sawHimRise && farMost > 90f, $"the far lid stood open ({farMost:0}°) and he stood up out of the dumpster");
        PolygonNpcVisual his = man.Speaker.GetComponent<PolygonNpcVisual>();
        int extra = 0;
        foreach (SkinnedMeshRenderer skin in man.Speaker.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (skin.enabled && (his == null || his.VisualInstance == null || !skin.transform.IsChildOf(his.VisualInstance.transform))) extra++;
        Check(his != null && his.VisualInstance != null && extra == 0,
            $"one body: his own look ({man.Look}), nothing of the café's walk-in outfits showing under it ({extra} other part(s) drawn)");
        Check(heldDuring, "Ace held still while it happened");

        // ---------- the deal ----------
        yield return Until(() => Barks.ScenePlaying, 3f, "the deal began");
        yield return Seconds(.6f);
        yield return Photo("06-the-deal-begins");
        int warmthBefore = ledger.Warmth;
        int choices = 0;
        float dealStart = Time.realtimeSinceStartup;
        while (Barks.ScenePlaying && Time.realtimeSinceStartup - dealStart < 90f)
        {
            if (Barks.Choosing)
            {
                yield return Until(() => Barks.ChipsUp, 3f, $"reply {choices + 1}: the chips come up");
                int pick = choices == 0 ? 0 : 1;
                string first = lines.FindLine(choices == 0 ? "ace.night0.02" : "ace.night0.10a")?.text ?? "?";
                Check(Barks.ReplyText(0) == first, $"reply {choices + 1}: the first chip is \"{Barks.ReplyText(0)}\", the second \"{Barks.ReplyText(1)}\"");
                yield return Seconds(.4f);
                ChipsClear($"reply {choices + 1}");
                yield return Photo($"07-the-deal-reply-{choices + 1}");
                int was = ledger.Warmth;
                yield return Answer(pick);
                Check(!Barks.Choosing, $"reply {choices + 1}: picked \"{(pick == 0 ? "1" : "2")}\"");
                Note($"reply {choices + 1}: warmth {was} to {ledger.Warmth}");
                choices++;
                continue;
            }
            yield return Seconds(.85f);
            if (!Barks.Choosing) Barks.Advance();   // E, as a player reads on
        }
        Check(choices == 2, $"Ace answered twice in the deal ({choices})");
        Check(ledger.Warmth == warmthBefore + 1, $"his warmth: +1 for the first reply, 0 for the second ({ledger.Warmth})");
        Check(zero.Replies == "01", $"the replies, as picked: {zero.Replies}");
        yield return Until(() => zero.Now == NightZero.Step.Done, 2f, "the deal is made: Night 0 is over");
        Check(ledger.MetHim && !NightZero.Pending, "Ace has met him (the night's record)");
        Check(!NightWalk.Instance.ClockHeld, "the night's clock runs");
        var pages = LodgerStory.Pages(NightZero.InTheGame).ToList();
        Check(pages.Count == 3 && pages.All(p => saves.Notebook.Knows(p.id) && saves.Notebook.Find(p.id).source == Notebook.Sources.Inherited)
              && !saves.Notebook.Knows("lodger.page.parking"),
            $"his pages are in the notebook ({pages.Count}), in his hand; the parking page waits for the cones");
        foreach (NotebookFactData p in pages)
            Check(!WordBudget.Over(p.text, WordBudget.Page), $"\"{p.text}\" keeps to the budget ({WordBudget.Report(p.text, WordBudget.Page)})");
        string page = NotebookRecap.Page(saves.Notebook);
        Check(page.StartsWith("<b>" + LodgerStory.PagesName + "</b>", StringComparison.Ordinal) && page.Contains("<i>"),
            "the notebook's page opens with his pages, in italics");
        Check(LodgerStory.Errand(ledger) == barnaby.id, "his errand: Grace's gnome");
        yield return Seconds(4f);   // Ace's line, then the night's note
        Check(NoteShowing().Contains("Grace's front step") && !WordBudget.Over(NoteShowing(), WordBudget.Note),
            $"the note says where the gnome is, within the budget (\"{Short(NoteShowing())}\": {WordBudget.Report(NoteShowing(), WordBudget.Note)})");
        Check(man.Hidden && set.LidOpen(set.farLid) < .01f, $"his say done, he ducked back into the dumpster and the lid dropped ({man.Describe()})");
        Check(interactor.CurrentPrompt != "Call it a night",
            $"at the dumpster after the deal the back door isn't offered (it's two metres away; the prompt reads \"{interactor.CurrentPrompt}\")");
        yield return Photo("08-after-the-deal");

        // ---------- no garbage from him: near the bins, and far from them ----------
        var near = new List<double>();
        for (int i = 0; i < 3; i++) yield return Measure(near);

        // ---------- the errand: Grace's step ----------
        Vector3 forward = Flat(door.transform.forward).normalized;
        Vector3 stand = gnome.transform.position + forward * .95f;
        yield return Walk(new List<Vector3>
        {
            new Vector3(set.binSpot.position.x, 0f, BackStreet), new Vector3(WestStreet, 0f, BackStreet),
            new Vector3(WestStreet, 0f, stand.z), stand,
        }, new List<float> { .8f, .9f, .6f, .3f }, "along Back Street and down West Street to Grace's step");
        var far = new List<double>();
        for (int i = 0; i < 3; i++) yield return Measure(far);
        double nearMin = near.Min(), farMin = far.Min();
        Check(nearMin - farMin <= 64.0, $"standing near him adds no garbage a frame ({nearMin:0} B near, {farMin:0} B far; lowest of 3 windows of 60 frames)");
        yield return Until(() => interactor.CurrentPrompt == "Take " + barnaby.name, 3f, $"at her step the prompt reads \"Take {barnaby.name}\"");
        Press();
        yield return null;
        yield return null;
        Check(gnome.Taken && ledger.HasTrophy(barnaby.id), "E took him: it's Ace's deed");
        Check(NightCarry.Current != null && NightCarry.Current.HeldId == barnaby.id, "he's in Ace's hand, for the man at the bins");
        Check(shelf.Showing == 0, "and not on Ace's shelf");
        Check(saves.Notebook.Find(barnaby.id + ".taken")?.text == barnaby.notebookTakenFor, "the notebook says who it was for");
        Check(NoteShowing().Contains("back to the man at the bins"), $"the note says where to take him (\"{Short(NoteShowing())}\")");
        yield return Seconds(.6f);
        yield return Photo("09-barnaby-in-hand");

        // ...and in first person, as Mansoor held him (6 Oct 2026: "when picking it up in first person, you can see inside the
        // head"): low on the right of the view, looking back at Ace, his hat the right way out (Barnaby.fbx rebuilt).
        view.SetFirstPerson(true);
        yield return Until(() => view.FirstPersonSelected && view.WalkingFirstPerson, 2f, "V: first person, Barnaby in hand");
        yield return Seconds(1.2f);
        Camera eye = Camera.main;
        GameObject heldNow = NightCarry.Current != null ? NightCarry.Current.Held : null;
        bool inView = false;
        if (heldNow != null && eye != null)
        {
            Bounds seen = default;
            bool any = false;
            foreach (Renderer r in heldNow.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (any) seen.Encapsulate(r.bounds);
                else { seen = r.bounds; any = true; }
            }
            Vector3 at = any ? eye.WorldToViewportPoint(seen.center) : Vector3.zero;
            inView = any && at.z > 0f && at.x > .5f && at.x < 1f && at.y > 0f && at.y < .5f;
            Note($"in first person Barnaby sits at {at.x:0.00}, {at.y:0.00} of the view, {at.z:0.00} m ahead");
        }
        Check(inView, "in first person he's in view, low on the right");
        yield return Photo("09b-barnaby-in-hand-first-person");
        view.SetFirstPerson(false);
        yield return Until(() => !view.FirstPersonSelected && eye != null
                                 && Vector3.Distance(eye.transform.position, movement.transform.position) > 5f, 3f,
            "V again: back to the view from above");

        // ---------- back to the bins ----------
        int passing = man.Passing;
        yield return Walk(new List<Vector3>
        {
            new Vector3(WestStreet, 0f, stand.z), new Vector3(WestStreet, 0f, BackStreet), new Vector3(set.giveSpot.position.x, 0f, BackStreet),
            set.giveSpot.position,
        }, new List<float> { .6f, .9f, .6f, .25f }, "back up West Street and along Back Street to his half of the dumpster");
        string give = "Give him " + barnaby.name;
        yield return Until(() => interactor.CurrentPrompt == give, 3f, $"at his half the prompt reads \"{give}\"");
        Check(man.Up && man.PoppedFor > 0f, $"he popped up as Ace came back with Barnaby, {man.PoppedFor:0.0} m from his half");
        Note($"he said {man.Passing - passing} line(s) in passing on the way back ({man.FromTheBin} from inside the bin tonight)");
        yield return Photo("10-give-him-barnaby");

        // ---------- the return ----------
        int warmth = ledger.Warmth;
        Press();
        yield return Until(() => Barks.ScenePlaying && man.Returning, 2f, "E: the return began");
        bool turned = false, gotNerve = false, gotPage = false, answered = false, cornerFirst = false;
        float returnStart = Time.realtimeSinceStartup;
        while (Barks.ScenePlaying && Time.realtimeSinceStartup - returnStart < 60f)
        {
            if (!cornerFirst && set.cornerGnome.activeSelf)
            {
                cornerFirst = true;
                Check(NightCarry.Current == null || !NightCarry.Current.Holding, "he took it: Ace's hand is empty");
                Check(ledger.HasGiven(barnaby.id) && !ledger.OnShelf(barnaby.id), "it's in his corner, not on Ace's shelf");
                Check(Mathf.Abs(Mathf.DeltaAngle(set.cornerGnome.transform.localEulerAngles.y, -set.gnomeTurn)) < 2f, "set down facing the wall, as it came");
            }
            if (Barks.Choosing)
            {
                yield return Until(() => Barks.ChipsUp, 3f, "the return's reply: the chips come up");
                yield return Seconds(.3f);
                ChipsClear("the return's reply");
                yield return Photo("11-the-return-reply");
                yield return Answer(0);
                answered = true;
                continue;
            }
            gotNerve |= ledger.Knows(LodgerStory.Nerve);
            gotPage |= saves.Notebook.Knows("lodger.page.grace.thursdays");
            yield return Seconds(1f);   // long enough for the gnome to turn
            turned |= set.cornerGnome.activeSelf && Quaternion.Angle(set.cornerGnome.transform.localRotation, Quaternion.identity) < 2f;
            if (!Barks.Choosing) Barks.Advance();
        }
        yield return Until(() => !man.Returning, 3f, "the return ended");
        Check(answered && ledger.Warmth == warmth + 1, $"Ace answered once, warmly (+1: his warmth {ledger.Warmth})");
        Check(turned, "he turned the gnome to face the street");
        Check(ledger.Knows(LodgerStory.Nerve), "he taught Ace Nerve");
        Check(saves.Notebook.Knows("lodger.page.grace.thursdays"), "and gave Ace a page (\"Grace. Thursdays. Find out.\")");
        Note(gotNerve && gotPage ? "Nerve and the page came on their lines" : "Nerve or the page came only at the end (the return's lines missed them)");
        yield return Seconds(.5f);
        yield return Photo("12-his-corner");
        yield return Until(() => man.Hidden, 4f, "a moment after the return he's back in the bin, the lid shut");

        // ---------- calling it a night at the back door ----------
        yield return Walk(new List<Vector3> { new Vector3(6.25f, 0f, 20.2f), set.outsideDoor.position }, new List<float> { .35f, .3f },
            "to the back door");
        yield return Until(() => interactor.CurrentPrompt == "Call it a night", 3f, "at the back door the prompt reads \"Call it a night\"");
        yield return Photo("13-call-it-a-night");
        Press();
        yield return Until(() => NightCycle.Instance.Now == NightCycle.Phase.Day && DayClock.Instance.Day == 2 && !DayClock.Instance.DayOver, 15f,
            "E called it a night: Day 2 opened");
        if (!lastWait) yield break;
        Check(ledger.Nights == 1 && ledger.MetHim && ledger.Warmth == 2 && ledger.Knows(LodgerStory.Nerve) && ledger.HasGiven(barnaby.id),
            $"the night's record: met, warmth {ledger.Warmth} (2), Nerve, the gnome given");
        Check(shelf.Showing == 0, "Barnaby isn't on Ace's shelf by day (he's in the man's corner)");
        Check(set.cornerGnome == null || !set.cornerGnome.activeSelf, "and the corner is put away by day");
        SaveData disk = null;
        try { disk = SaveCheckpointStorage.Read(Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName)); }
        catch (Exception e) { Note("could not read the lab save: " + e.Message); }
        Check(disk != null && disk.day == 2 && disk.night.metHim && disk.night.warmth == 2 && disk.night.lessons.Contains(LodgerStory.Nerve)
              && disk.night.given.Contains(barnaby.id) && disk.notebook.Any(f => f.id == "lodger.page.grace" && f.source == Notebook.Sources.Inherited),
            "Day 2's save has all of it (met, warmth, Nerve, his corner, his pages)");
        yield return Seconds(2.2f);
        yield return Photo("14-day-2-morning");

        // ---------- Grace, and Nerve ----------
        CustomerBrain grace = null;
        yield return Until(() => (grace = FindGrace()) != null, 60f, "Grace comes to the café on Day 2");
        if (!lastWait) yield break;
        yield return Until(() => grace == null || grace.CanHearIntake, 150f, "she reaches the counter");
        if (!lastWait || grace == null) yield break;
        conversation.Begin(grace);
        yield return null;
        MorningFace face = conversation.Face;
        Check(face != null && face.Now == MorningFace.Step.Complaint, "the morning scene opens her visit");
        if (face == null) yield break;
        yield return Until(() => face.Now == MorningFace.Step.Meter, 15f, "the straight-face meter appears");
        if (!lastWait) yield break;
        StraightFaceMeter meter = face.Meter;
        float green = LodgerStory.Green(barnaby.green, true);
        Check(face.Nerve && Mathf.Abs(meter.Green - green) < .001f, $"with Nerve the green is wider ({meter.Green:0.000}, not {barnaby.green:0.000})");
        yield return Seconds(.35f);
        yield return Photo("15-the-meter-with-nerve");
        float giveUp = Time.realtimeSinceStartup + meter.Patience + 2f;
        while (!meter.Stopped && Time.realtimeSinceStartup < giveUp)
        {
            if (Mathf.Abs(meter.Needle - meter.GreenCentre) < meter.Green * .25f) meter.Stop();
            yield return null;
        }
        yield return Until(() => face.Now == MorningFace.Step.Result || face.Now == MorningFace.Step.Done, 4f, "the needle stopped");
        Check(meter.Held, "Ace kept a straight face");
        yield return Photo("16-straight-face");
        report.AppendLine();
        report.AppendLine("The night's ledger now: " + JsonUtility.ToJson(ledger.Snapshot()));
    }

    // The replies' chips sit beside the line they answer, clear of his head and Ace's (from the street's camera the two
    // stand one behind the other).
    void ChipsClear(string what)
    {
        Camera cam = Camera.main;
        Rect chips = Barks.ChipsOnScreen;
        Lodger man = Lodger.Instance;
        if (cam == null || man == null || chips.width <= 0f) { Check(false, $"{what}: the chips are on screen"); return; }
        Vector3 his = cam.WorldToScreenPoint(Barks.HeadPointOf(man.Speaker));
        Vector3 aces = cam.WorldToScreenPoint(Barks.HeadPointOf(movement.transform));
        bool clear = !chips.Contains(new Vector2(his.x, his.y)) && !chips.Contains(new Vector2(aces.x, aces.y))
                     && chips.xMin >= 0f && chips.yMin >= 0f && chips.xMax <= Screen.width && chips.yMax <= Screen.height;
        Check(clear, $"{what}: the chips sit clear of his head and Ace's, on screen (chips {chips.xMin:0}-{chips.xMax:0} x {chips.yMin:0}-{chips.yMax:0}; " +
                     $"his head {his.x:0},{his.y:0}; Ace's {aces.x:0},{aces.y:0})");
    }

    // ---------- driving Ace ----------

    // E, as the interactor's own input message.
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

    IEnumerator Walk(List<Vector3> points, List<float> radii, string what)
    {
        int stuck = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 target = points[i];
            float radius = radii[i];
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

    // A lid's opening, in degrees from shut.
    static float Angle(Transform hinge) => hinge != null ? Quaternion.Angle(hinge.localRotation, Quaternion.identity) : 0f;

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

    static CustomerBrain FindGrace()
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain.Identity != null && brain.Identity.Profile != null && brain.Identity.Profile.PersistentId == Grace)
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

    // ---------- waiting, measuring, photos and the report ----------

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

    // The garbage made a frame, over 60 frames (as the bark check measures it).
    static IEnumerator Measure(List<double> into)
    {
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 64);
        yield return null;
        yield return null;
        long sum = 0;
        int frames = 0;
        for (int i = 0; i < 60; i++)
        {
            yield return null;
            if (recorder.Valid) { sum += recorder.LastValue; frames++; }
        }
        into.Add(frames > 0 ? sum / (double)frames : 0d);
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
        if (movement != null) movement.ScriptedInput = null;
        report.AppendLine();
        if (NightCycle.Instance != null) report.AppendLine(NightCycle.Instance.Describe());
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Night 0 check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Night 0 check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Night 0 check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) movement.ScriptedInput = null;
    }
}
