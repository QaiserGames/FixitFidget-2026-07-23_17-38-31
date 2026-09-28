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
// NIGHT 1 CHECK: THE WHOLE SLICE, DRIVEN (a lab session only: Fixit Fidget > Night > Night 1 - Play
// check, keeping a straight face / cracking)
//
// From the recap of a made-up day (NightOneSteps writes the lab save) to Grace's visit the next morning:
//   1. the recap's button reads "Close up for the night", and pressing it begins the night;
//   2. Ace walks out of the café, along the front street and up to Grace's front step, where the prompt
//      reads "Take Barnaby" when she told Ace his name on Day 1 (the Day 1 lab), "Take the garden gnome"
//      when she never did (the Day 2 lab, like a save from before this step); E takes him: into the
//      night's ledger, off her step, onto Ace's shelf, into the notebook;
//   3. back in through the café's door the prompt reads "Call it a night"; E ends the night, and the next
//      day opens, saved with what the night did;
//   4. Grace comes in: on Day 2 as its featured regular, on Day 3 as the morning's visitor. At the
//      counter her first line is the complaint, then the straight-face meter runs.
//      Keeping a straight face: the needle is stopped in the green (with Space, the player's key, when
//      the Game view has the keyboard; otherwise directly). Cracking: it is left to run out. Her reaction,
//      the ledger, suspicion and the notebook are checked, and the conversation goes on to what she
//      came in for.
// Ace is driven like a player: PlayerMovement.ScriptedInput stands in for the keys (as in NightTour), and
// E goes through the interactor's own input message. A photo at each step and report.txt go to the
// check's folder. Nothing is saved in the scene; the lab save is the only file written (by the game).
// ---------------------------------------------------------------------------
public sealed class NightOneCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.NightOne.Check";
    public enum Mode { StraightFace = 1, Crack = 2 }

    public Mode mode = Mode.StraightFace;
    public string folder;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Grace = GraceCameraEpisode.ProfileId;

    // Out from behind the counter and through the café's door, the way the night tour goes (NightTour),
    // then out to the front street.
    static readonly Vector3[] OutOfTheCafe =
    {
        new Vector3(5.12f, 0f, 15.62f), new Vector3(5.88f, 0f, 14.62f), new Vector3(5.88f, 0f, 12.12f),
        new Vector3(1.12f, 0f, 10.88f), new Vector3(.62f, 0f, 7.12f), new Vector3(.12f, 0f, .6f),
        new Vector3(.12f, 0f, -.8f), new Vector3(.12f, 0f, -7.62f),
    };
    static readonly float[] OutOfTheCafeRadii = { .45f, .45f, .45f, .5f, .5f, .25f, .3f, .7f };
    // West Street's middle line, where the tour walks it.
    const float WestStreet = -11.88f;
    const float FrontStreet = -7.62f;

    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    bool lastWait;
    Vector3 home;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    ConversationController conversation;
    SaveManager saves;
    NightLedger ledger;

#if UNITY_EDITOR
    // Asked for by the editor (NightOneSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        int asked = PlayerPrefs.GetInt(PendingKey, 0);
        if (asked == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night 1 check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Night 1 check (this Play session only)");
        var check = go.AddComponent<NightOneCheck>();
        check.mode = asked == (int)Mode.Crack ? Mode.Crack : Mode.StraightFace;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            $"night-one-check-{(check.mode == Mode.Crack ? "cracking" : "straight-face")}-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // Runs the steps below; nested steps are run in place, and an exception ends the check with a
    // failure rather than leaving it hanging.
    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", $"night-one-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine($"Night 1 - play check ({(mode == Mode.Crack ? "cracking" : "keeping a straight face")}), lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Night 1 check] Running ({(mode == Mode.Crack ? "cracking" : "keeping a straight face")}): about two or three minutes. " +
                  $"Hands off the mouse and keyboard until it reports. {folder}");

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
        // ---------- the day's recap (Day 1, or Day 2 like an older save) ----------
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapShowing(), 15f,
            "the lab opened on a closed day with its recap on screen");
        if (!lastWait) yield break;
        int recapDay = DayClock.Instance.Day;
        int morning = recapDay + 1;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        saves = SaveManager.Instance;
        ledger = saves != null ? saves.Night : null;
        Check(movement != null && interactor != null && view != null && conversation != null, "Ace is in the scene (movement, interactor, view, conversation)");
        Check(ledger != null, "the save manager keeps the night's ledger");
        if (movement == null || interactor == null || view == null || conversation == null || ledger == null) yield break;
        home = movement.transform.position;

        NightThing barnaby = NightThings.GnomeOfGrace;
        NightTrophy gnome = FindAnyObjectByType<NightTrophy>();
        TrophyShelf shelf = FindAnyObjectByType<TrophyShelf>();
        HomeDoor door = HomeDoor.Find("home.grace");
        Check(recapDay == 1 || recapDay == 2, $"it is Day {recapDay}'s recap");
        Check(ledger.Nights == 0 && ledger.Trophies.Count == 0 && ledger.Deeds.Count == 0, "no nights yet: nothing taken, nothing on the shelf");
        bool heardOfHim = recapDay == 1;
        Check(saves.Notebook.Knows(barnaby.id) == heardOfHim, heardOfHim
            ? "the notebook has what Grace said about Barnaby on Day 1"
            : "the notebook knows nothing of Barnaby (she never mentioned him: a save from before this step)");
        string takePrompt = "Take " + (heardOfHim ? barnaby.name : barnaby.unknownName);
        Check(NightCycle.FollowsTheDay, "a night follows the day (Night Follows The Day is on)");
        TMP_Text label = RecapButton() != null ? RecapButton().GetComponentInChildren<TMP_Text>(true) : null;
        Check(label != null && label.text == RecapUI.NightLabel, $"the recap's button reads \"{RecapUI.NightLabel}\" (it reads \"{label?.text}\")");
        Check(gnome != null && gnome.thingId == barnaby.id, "Grace's gnome is in the scene");
        Check(shelf != null, "Ace's shelf is in the scene");
        Check(door != null, "Grace's front door is in the scene");
        if (gnome == null || shelf == null || door == null || RecapButton() == null) yield break;
        Check(gnome.visual != null && gnome.visual.activeInHierarchy && !gnome.Taken, "Barnaby stands by her door");
        Check(shelf.Showing == 0, "the shelf is empty");
        yield return Photo("01-day-1-recap");

        // ---------- nightfall ----------
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        RecapButton().onClick.Invoke();
        yield return Until(() => NightCycle.Instance != null && NightCycle.Instance.Now == NightCycle.Phase.Night, 12f,
            "the recap's button began the night");
        if (!lastWait) yield break;
        Check(NightWalk.Instance != null && NightWalk.Instance.Active, "the night walk is running");
        Check(DayClock.Instance.DayOver && DayClock.Instance.Day == recapDay, $"it is still the night after Day {recapDay} (the day stays closed)");
        Check(!DayClock.Instance.RecapOwnsInput, "the recap no longer holds Ace still");
        Check(!RecapShowing(), "the recap is put away");
        Check(Time.timeScale > .99f, "time runs at night");
        Check(FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Length == 0
              && FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Length == 0, "the café is empty");
        Check(NightCycle.Instance.HasDoorway, "calling it a night is offered inside the café's door");
        yield return Seconds(1.6f);   // the fade in
        yield return Photo("02-night-begins");

        // ---------- to Grace's step ----------
        Vector3 forward = Flat(door.transform.forward).normalized;
        Vector3 stand = gnome.transform.position + forward * .95f;
        var route = new List<Vector3>(OutOfTheCafe);
        var radii = new List<float>(OutOfTheCafeRadii);
        route.Add(new Vector3(WestStreet, 0f, FrontStreet)); radii.Add(.9f);
        route.Add(new Vector3(WestStreet, 0f, stand.z)); radii.Add(.6f);
        route.Add(stand); radii.Add(.3f);
        yield return Walk(route, radii, "out of the café and up to Grace's front step");
        yield return Seconds(.5f);
        Check(interactor.CurrentPrompt == takePrompt, $"at her step the prompt reads \"{takePrompt}\" (it reads \"{interactor.CurrentPrompt}\")");
        yield return Photo("03-at-grace-step");

        movement.SendMessage("OnInteract", SendMessageOptions.DontRequireReceiver);   // E
        yield return null;
        yield return null;
        NightDeedData deed = ledger.Deeds.FirstOrDefault(d => d.thing == barnaby.id);
        Check(gnome.Taken && ledger.HasTrophy(barnaby.id), "E took him: he is in the night's ledger");
        Check(deed != null && deed.owner == Grace && deed.night == recapDay && !deed.faced, $"the deed: Grace's, the night after Day {recapDay}, not yet faced");
        Check(gnome.visual != null && !gnome.visual.activeSelf, "he is gone from her step");
        Check(shelf.Showing == 1, "he stands on Ace's shelf");
        Check(saves.Notebook.Knows(barnaby.id + ".taken") == heardOfHim, heardOfHim
            ? "Ace noted it down (a secret)"
            : "nothing in the notebook yet: Ace doesn't know whose it is");
        Check(interactor.CurrentPrompt != takePrompt, "there is nothing more to take there");
        yield return Seconds(.8f);
        yield return Photo("04-taken");

        // ---------- back in through the café's door ----------
        route = new List<Vector3>
        {
            new Vector3(WestStreet, 0f, stand.z), new Vector3(WestStreet, 0f, FrontStreet), new Vector3(.12f, 0f, FrontStreet),
            new Vector3(.12f, 0f, -.8f), new Vector3(.12f, 0f, 1.3f),
        };
        radii = new List<float> { .6f, .9f, .7f, .3f, .3f };
        yield return Walk(route, radii, "back along the front street and in through the café's door");
        yield return Until(() => interactor.CurrentPrompt == "Call it a night", 4f, "inside the café's door the prompt reads \"Call it a night\"");
        yield return Photo("05-call-it-a-night");
        movement.SendMessage("OnInteract", SendMessageOptions.DontRequireReceiver);   // E
        yield return Until(() => NightCycle.Instance.Now == NightCycle.Phase.Day && DayClock.Instance.Day == morning && !DayClock.Instance.DayOver, 15f,
            $"E called it a night: Day {morning} opened");
        if (!lastWait) yield break;
        Check(!NightWalk.Instance.Active, "the night walk is over");
        Check(NightCycle.Instance.LastEnding == "called it a night", $"the night ended because Ace called it a night ({NightCycle.Instance.LastEnding})");
        Check(ledger.Nights == 1, "one night home");
        Check(Vector3.Distance(Flat(movement.transform.position), Flat(home)) < .6f, $"Ace starts Day {morning} back where the day starts");
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        bool asMorningVisitor = spawner != null && spawner.MorningVisitor != null && spawner.MorningVisitor.PersistentId == Grace;
        if (recapDay == 2)
            Check(asMorningVisitor || FindGrace() != null, "Grace is due first thing, as the morning's visitor (Day 3 has no featured regular)");
        else Note(asMorningVisitor ? "Grace is due as the morning's visitor" : "Grace is due as Day 2's featured regular");
        Check(shelf.Showing == 1, "Barnaby is on the shelf by day");
        Check(gnome.visual != null && !gnome.visual.activeSelf, "and not back on Grace's step");

        string savePath = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveData disk = null;
        try { disk = SaveCheckpointStorage.Read(savePath); }
        catch (Exception e) { Note("could not read the lab save: " + e.Message); }
        Check(disk != null && disk.day == morning && !disk.dayCompleted, $"the lab save is Day {morning}'s morning");
        if (disk != null)
        {
            NightDeedData saved = disk.night.deeds.FirstOrDefault(d => d.thing == barnaby.id);
            Check(disk.night.nights == 1 && disk.night.trophies.Contains(barnaby.id), "the morning's save has the night: one night home, Barnaby on the shelf");
            Check(saved != null && saved.owner == Grace && saved.night == recapDay && !saved.faced, "and the deed, still to be faced");
            Check(disk.notebook.Any(f => f.id == barnaby.id + ".taken") == heardOfHim, heardOfHim ? "and Ace's note of it" : "and no note of it yet");
        }
        yield return Seconds(2.2f);   // the morning's caption and fade in
        yield return Photo($"06-day-{morning}-morning");

        // ---------- Grace comes in ----------
        CustomerBrain grace = null;
        yield return Until(() => (grace = FindGrace()) != null, 60f, $"Grace comes to the café on Day {morning}");
        if (!lastWait) yield break;
        yield return Until(() => grace == null || grace.CanHearIntake, 150f, "she reaches the counter");
        if (!lastWait || grace == null) yield break;
        yield return Seconds(.5f);
        yield return Photo("07-grace-at-the-counter");

        conversation.Begin(grace);   // what E on her does (CustomerInteractable)
        yield return null;
        MorningFace face = conversation.Face;
        Check(conversation.InConversation && face != null && face.Now == MorningFace.Step.Complaint,
            "the conversation opens with the morning scene, before her request");
        if (face == null) yield break;
        Check(Line() == barnaby.complaint, $"her first line is the complaint (\"{Short(Line())}\")");
        Check(saves.Notebook.Knows(barnaby.id) && saves.Notebook.Knows(barnaby.id + ".taken"),
            heardOfHim ? "the notebook has Barnaby and Ace's secret" : "now Ace knows whose he was: the notebook has Barnaby and Ace's secret");
        yield return Until(() => face.Now == MorningFace.Step.Meter, 15f, "the straight-face meter appears once she has said it");
        if (!lastWait) yield break;
        Check(StraightFaceUI.Showing, "the meter is on screen");
        yield return Seconds(.35f);
        yield return Photo("08-the-meter");

        StraightFaceMeter meter = face.Meter;
        if (mode == Mode.StraightFace)
        {
            // Wait for the needle to come into the middle of the green, then press Space as a player would.
            bool keyTried = false, keyWorked = false;
            float giveUp = Time.realtimeSinceStartup + meter.Patience + 2f;
            while (!meter.Stopped && Time.realtimeSinceStartup < giveUp)
            {
                bool middle = Mathf.Abs(meter.Needle - meter.GreenCentre) < meter.Green * .25f;
                if (middle && !keyTried && Application.isFocused && Keyboard.current != null)
                {
                    keyTried = true;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Space));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    keyWorked = meter.Stopped;
                    continue;
                }
                if (middle && (!keyTried || !keyWorked)) meter.Stop();
                yield return null;
            }
            Note(keyWorked ? "stopped the needle with Space, as a player would"
                : keyTried ? "Space did not reach the game (the Game view may not have had the keyboard): stopped the needle directly"
                : "the Game view did not have the keyboard: stopped the needle directly");
        }
        else Note("left the needle to run (a crack, the patience running out)");

        yield return Until(() => face.Now == MorningFace.Step.Result || face.Now == MorningFace.Step.Done, meter.Patience + 3f, "the needle stopped");
        bool held = meter.Held;
        bool wantHeld = mode == Mode.StraightFace;
        Check(held == wantHeld, held ? "Ace kept a straight face" : meter.TimedOut ? "Ace cracked (the needle was never stopped)" : "Ace cracked");
        Check(Line() == (held ? barnaby.held : barnaby.cracked), $"her reaction: \"{Short(Line())}\"");
        Check(deed != null && deed.faced && deed.cracked == !held && deed.facedDay == morning, $"the deed is faced (on Day {morning}), " + (held ? "held" : "cracked"));
        Check(ledger.Suspicion(Grace) == (held ? 0 : 1), $"Grace's suspicion is {ledger.Suspicion(Grace)} (a crack adds one; stars are never touched)");
        Check(saves.Notebook.Knows(barnaby.id + ".suspects") == !held,
            held ? "nothing new in the notebook" : "the notebook notes she's watching her step now");
        Check(ledger.Unfaced(Grace, morning) == null, "nothing left for her to tell Ace");
        yield return Seconds(.4f);
        yield return Photo("09-" + (held ? "straight-face" : "cracked"));

        yield return Until(() => conversation.Face == null, 10f, "after a moment the conversation moves on by itself");
        Check(conversation.InConversation && conversation.Face == null, "the conversation stays open, on to what she came in for");
        if (morning == 2)
        {
            string opening = GraceCameraEpisode.ReturnLine(GracePhotoOutcome.Clear);
            Check(Line().StartsWith(opening.Substring(0, 24), StringComparison.Ordinal),
                $"her usual Day 2 visit follows: the reunion photo (\"{Short(Line())}\")");
        }
        else Check(!string.IsNullOrWhiteSpace(Line()) && Line() != barnaby.complaint && grace.CanDecide,
            $"then what she came in for, as any regular's visit (\"{Short(Line())}\")");
        Check(!StraightFaceUI.Showing, "the meter is put away");
        yield return Seconds(1.2f);
        yield return Photo("10-her-visit-goes-on");
        report.AppendLine();
        report.AppendLine("The night's ledger now: " + JsonUtility.ToJson(ledger.Snapshot()));
        report.AppendLine(face.Describe());
        report.AppendLine(NightCycle.Instance.Describe());
    }

    // ---------- driving Ace ----------

    // Walks Ace through the points like a player (the keys, in the camera's frame). A leg that makes no
    // headway is written down as stuck, and Ace is moved on to its end so the rest of the check can run.
    IEnumerator Walk(List<Vector3> points, List<float> radii, string what)
    {
        int stuck = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 target = points[i];
            float radius = radii[i];
            float deadline = Time.realtimeSinceStartup + 25f;
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
                Vector3 p = movement.transform.position;
                Note($"STUCK at ({p.x:0.00}, {p.z:0.00}) on the way to ({target.x:0.00}, {target.z:0.00}); moved Ace on there");
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

    GameObject RecapPanel() => Field<GameObject>(FindAnyObjectByType<RecapUI>(), "panel");
    Button RecapButton() => Field<Button>(FindAnyObjectByType<RecapUI>(), "nextDayButton");
    bool RecapShowing() { GameObject panel = RecapPanel(); return panel != null && panel.activeInHierarchy; }

    string Line()
    {
        ConversationUI ui = Field<ConversationUI>(conversation, "ui");
        TMP_Text text = Field<TMP_Text>(ui, "dialogueText");
        return text != null ? text.text ?? "" : "";
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

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Finish()
    {
        if (movement != null) movement.ScriptedInput = null;
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Night 1 check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Night 1 check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Night 1 check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) movement.ScriptedInput = null;
    }
}
