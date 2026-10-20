using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// CONVERSATION CHECK: THE DIALOGUE PASS, DRIVEN (a lab session only: Fixit Fidget > Dialogue >
// Conversation - play check)
//
// A fresh Day 1 in the lab (DialoguePassSteps writes the lab's own save). Grace comes in first with her
// camera, and then whoever comes next:
//   1. Grace at the counter: her request is two short lines, shown one at a time with no replies yet; E
//      while she talks brings everything up and Ace's replies, without taking the job;
//   2. the replies: "I'll take a look." (highlighted), "Big plans tonight?", "Not today."; S moves the
//      highlight (the real key, when the Game view has the keyboard);
//   3. asking about her plans: she answers with Barnaby, the notebook has him, the question greys and
//      is marked noted, and taking the job is highlighted again;
//   4. taking the camera: her thanks is the reveal, the notebook has her whole story, and E closes it at
//      once;
//   5. the next customer: a walk-in gets only take it / turn away, and says the device the way a
//      sentence does ("my pocket watch"); Q turns them away, and they go.
// Playtest 4 (10 Oct 2026):
//   * the walk-in is the next walk-in in the queue (a regular there, the Officer on Day 1, is passed over);
//   * the walk-in says what's wrong the way a person does (FaultSpeech: "the screen's cracked"), never the ticket's label;
//   * a quick second E (the frames right after E opened it, as a player pressing twice) brings the replies up at once, before
//     the listening beat would have, and still never takes the job (the lock). The inputDelay of 0.2 s threw that press
//     away (Fixit Fidget › Playtest 4 › Content › 4 makes it 0). The presses for the visit are written in the report.
// A photo at each step and report.txt go to Logs/Dialogue. Nothing is saved in the scene; the lab save
// is the only file the game writes.
// ---------------------------------------------------------------------------
public sealed class ConversationCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.Dialogue.ConversationCheck";

    public string folder;

    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    bool lastWait, lastPress;

    ConversationController conversation;
    ConversationUI ui;
    SaveManager saves;

#if UNITY_EDITOR
    // Asked for by the editor (DialoguePassSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Conversation check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Conversation check (this Play session only)");
        var check = go.AddComponent<ConversationCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Dialogue",
            $"conversation-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // Runs the steps below; nested steps are run in place, and an exception ends the check with a
    // failure rather than leaving it hanging.
    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Dialogue", $"conversation-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine("Conversation - play check (the dialogue pass), lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Conversation check] Running: about two minutes. Hands off the mouse and keyboard until it reports. {folder}");

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
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.Day == 1, 20f,
            "the lab opened on a fresh Day 1");
        if (!lastWait) yield break;
        PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        ui = FindAnyObjectByType<ConversationUI>();
        saves = SaveManager.Instance;
        Check(conversation != null && ui != null && saves != null, "Ace's conversation, its screen and the save manager are in the scene");
        if (conversation == null || ui == null || saves == null) yield break;
        NightThing gnome = NightThings.GnomeOfGrace;

        // ---------- Grace brings her camera ----------
        CustomerBrain grace = null;
        yield return Until(() => (grace = FindGrace()) != null, 60f, "Grace comes in on Day 1 (the featured visit)");
        if (!lastWait) yield break;
        yield return Until(() => grace == null || grace.CanHearIntake, 150f, "she reaches the counter");
        if (!lastWait || grace == null) yield break;
        yield return Seconds(.4f);
        yield return Photo("01-grace-at-the-counter");

        conversation.Begin(grace);   // what E on her does (CustomerInteractable)
        yield return null;
        Check(conversation.InConversation, "the conversation opens");
        Check(conversation.CurrentLine == GraceCameraEpisode.Intake, $"her request is the two short lines (\"{Short(conversation.CurrentLine)}\")");
        Check(ui.LineCount == 2 && ui.LinesShown == 1 && !conversation.RepliesShowing, "at first only her first line shows, and no replies yet");
        yield return Seconds(1.2f);   // the camera turns to her first (the lab starts on the overview); her first line lasts about 4 s
        Check(ui.LinesShown == 1 && !conversation.RepliesShowing, "…and it stays up on its own while there's time to read it");
        yield return Photo("02-her-first-line");

        // E while she talks: everything at once, and the replies, but never an answer.
        yield return Press(Key.E, () => conversation.RepliesShowing || ui.LinesShown == 2);
        if (!lastPress)
        {
            Note("the Game view did not have the keyboard: E was pressed directly");
            conversation.Advance();
        }
        yield return Until(() => conversation.RepliesShowing, 6f, "E while she talks shows her second line and brings Ace's replies up");
        Check(grace.CanDecide && !grace.InService, "…without taking the job (an E before the replies never answers)");
        string[] expected = { AceReplies.TakeRepair, gnome.topic, AceReplies.TurnAwayRepair };
        Check(conversation.ReplyTexts.SequenceEqual(expected), $"Ace's replies: {Options()}");
        Check(conversation.Highlighted == 0, "taking it is highlighted, so E, E still takes the job");
        yield return Seconds(.3f);
        yield return Photo("03-her-request-and-the-replies");

        // S moves the highlight down (the real key).
        yield return Press(Key.S, () => conversation.Highlighted == 1);
        if (lastPress) Check(conversation.Highlighted == 1, "S moves the highlight down to the question");
        else Note("the Game view did not have the keyboard: S wasn't tried");

        // ---------- asking about her plans ----------
        conversation.ChooseReply(1);
        yield return null;
        Check(conversation.CurrentLine == gnome.mention && !conversation.RepliesShowing,
            $"asked \"{gnome.topic}\": she answers with Barnaby, and the replies wait (\"{Short(conversation.CurrentLine)}\")");
        Check(saves.Notebook.Knows(gnome.id), "the notebook has Barnaby");
        yield return Seconds(.8f);
        yield return Photo("04-her-answer");
        yield return Until(() => conversation.RepliesShowing, 12f, "then the replies come back");
        ConversationReply asked = conversation.Replies.Count > 1 ? conversation.Replies[1] : null;
        Check(asked != null && asked.Topic != null && asked.Topic.Asked && asked.Topic.Taught, "the question is greyed and marked noted");
        Check(conversation.Highlighted == 0, "and taking the job is highlighted again");
        yield return Seconds(.3f);
        yield return Photo("05-asked-and-noted");

        // ---------- taking the camera ----------
        conversation.ChooseReply(0);
        yield return null;
        Check(conversation.Closing && conversation.CurrentLine == GraceCameraEpisode.AcceptedLine,
            $"taking it: her thanks is the reveal (\"{Short(conversation.CurrentLine)}\")");
        Check(grace.InService, "the job is taken");
        Check(saves.Notebook.Knows("grace.camera.strap") && saves.Notebook.Knows("grace.husband.strap")
              && saves.Notebook.Knows("grace.reunion.date") && saves.Notebook.Knows("grace.behind.camera"),
            "the notebook has her whole story: the strap and her husband from her request, the reunion and the photos from her thanks");
        yield return Seconds(.5f);
        yield return Photo("06-her-thanks");
        float closing = Time.realtimeSinceStartup;
        for (int i = 0; i < 3 && conversation.InConversation; i++)
        {
            yield return Press(Key.E, () => !conversation.InConversation);
            if (!lastPress && conversation.InConversation) conversation.Advance();
            yield return null;
        }
        Check(!conversation.InConversation && Time.realtimeSinceStartup - closing < 3f, "E closes her thanks at once (no waiting it out)");
        yield return Seconds(1f);

        // ---------- the next customer: a walk-in ----------
        CustomerBrain next = null;
        // 20 Oct: since 15 Oct Day 1 brings the Officer (a regular) second and holds two customers at most (Calm: one every
        // 75 s), so a walk-in only comes once a place frees up. The lab sends any regular but Grace away (its own save) and
        // waits up to 4 minutes for the walk-in.
        float giveUp = Time.realtimeSinceStartup + 240f;
        while ((next = NextWalkIn(grace)) == null && Time.realtimeSinceStartup < giveUp)
        {
            foreach (CustomerBrain other in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
                if (other != null && other != grace && other.Identity != null && other.Identity.IsRegular)
                {
                    Note($"{other.Identity.DisplayName} sent away by the lab, to free a place for a walk-in");
                    other.ForceRemove();
                }
            yield return Seconds(1f);
        }
        Check(next != null, next != null ? "the next walk-in reaches the queue" : "the next walk-in reaches the queue (gave up after 240 s)");
        if (next == null) yield break;
        bool regular = next.Identity != null && next.Identity.IsRegular;
        Check(!regular, "they are a walk-in (a regular in the queue is passed over)");
        yield return Seconds(.3f);

        // Playtest 4: E to talk, then a quick second E, as a player pressing twice: the replies come up at once, sooner than the
        // listening beat would bring them, and the job isn't taken (an E before the replies only brings them up).
        float opened = Time.realtimeSinceStartup;
        conversation.Begin(next);   // what E on them does (CustomerInteractable)
        yield return null;
        string said = conversation.CurrentLine;
        float beat = ListenBeat(said);
        float pressedAt = -1f, repliesAt = -1f;
        if (Application.isFocused && Keyboard.current != null)
        {
            pressedAt = Time.realtimeSinceStartup;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                if (i == 1) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                if (repliesAt < 0f && conversation.RepliesShowing) repliesAt = Time.realtimeSinceStartup;
            }
        }
        yield return Until(() => conversation.RepliesShowing, 12f, "their line, then Ace's replies");
        if (!lastWait) yield break;
        if (pressedAt < 0f) Note("the Game view did not have the keyboard: the quick second E wasn't tried");
        else if (pressedAt - opened >= beat - .05f)
            Note($"the frame rate was too low to press inside the listening beat ({pressedAt - opened:0.00} s after opening; the beat {beat:0.00} s): the quick second E wasn't judged");
        else
            Check(repliesAt >= 0f && repliesAt - opened < beat && !next.WasAccepted,
                $"a quick second E brought the replies up at once ({(repliesAt >= 0f ? (repliesAt - opened).ToString("0.00") + " s" : "not")} after opening; " +
                $"by themselves they come after {beat:0.00} s) without taking the job" +
                (repliesAt < 0f ? $" (the press was thrown away: inputDelay {InputDelay():0.##} s; Fixit Fidget › Playtest 4 › Content › 4 makes it 0)" : ""));
        Note($"presses for this walk-in: E to talk, E to take it (2); the quick E in between (3) only brings the replies up sooner. Their line: \"{Short(said)}\"");
        bool drink = next.Record != null && next.Record.kind == JobKind.Drink;
        string[] offered = next.OutOfStock ? new[] { AceReplies.OutOfStock }
            : next.ShelfFull ? new[] { AceReplies.ShelfFull }
            : drink ? new[] { AceReplies.TakeDrink, AceReplies.TurnAwayDrink }
            : new[] { AceReplies.TakeRepair, AceReplies.TurnAwayRepair };
        Check(conversation.ReplyTexts.SequenceEqual(offered), $"a walk-in gets only take it or turn away: {Options()}");
        string line = conversation.CurrentLine;
        Check(!string.IsNullOrWhiteSpace(line) && !line.Contains("{") && line.Split('\n').All(l => l.Length <= DialogueLimit)
              && !line.Split('\n').Any(l => l.Length > 0 && char.IsLower(l[0])),
            $"their line reads naturally (\"{Short(line)}\")");
        string subject = next.Record != null ? next.Record.Subject : "";
        Check(string.IsNullOrEmpty(subject) || !line.Contains(subject) || CustomerIdentity.SpokenName(subject) == subject,
            $"the device is said the way a sentence says it (\"{CustomerIdentity.SpokenName(subject)}\")");
        // Playtest 4: the fault, the way a person says it (FaultSpeech), never the ticket's label.
        string label = !drink && next.Record != null ? next.Record.faultDescription : "";
        string spoken = label.Length > 0 ? FaultSpeech.Spoken(label) : "";
        bool knows = label.Length == 0 || FaultSpeech.TryGet(label, out _);
        bool labelled = FaultSpeech.ReadsLikeALabel(line, label.Length > 0 ? new[] { label } : null, out string why);
        Check(knows && !labelled && (label.Length == 0 || line.Contains(spoken)),
            label.Length > 0 ? $"they say what's wrong the way a person does (\"{spoken}\"), not the ticket's \"{label}\"{(labelled ? ": it " + why : "")}"
                             : $"a drink order reads as speech, no label in it{(labelled ? ": it " + why : "")}");
        yield return Seconds(.3f);
        yield return Photo("07-a-walk-in");

        // Q turns them away, whatever is highlighted.
        yield return Press(Key.Q, () => conversation.Closing || !conversation.InConversation);
        if (!lastPress)
        {
            Note("the Game view did not have the keyboard: turned them away directly");
            int turnAway = conversation.Replies.ToList().FindIndex(r => r.Kind == ReplyKind.Refuse);
            conversation.ChooseReply(turnAway);
        }
        yield return null;
        Check(conversation.Closing || !conversation.InConversation, "Q turns them away: their closing line plays");
        yield return Seconds(.4f);
        yield return Photo("08-turned-away");
        yield return Until(() => !conversation.InConversation, 12f, "…and the conversation closes by itself once it's read");
        Check(next == null || next.IsLeaving, "they leave");
    }

    const int DialogueLimit = 150;

    // ---------- helpers ----------

    // A key press as a player makes it, when the Game view has the keyboard; lastPress says whether it
    // did what it should.
    IEnumerator Press(Key key, Func<bool> worked)
    {
        lastPress = false;
        if (!Application.isFocused || Keyboard.current == null) yield break;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return null;
        lastPress = Safe(worked);
    }

    string Options() => conversation != null ? string.Join(" | ", conversation.ReplyTexts) : "";

    static CustomerBrain FindGrace()
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain.Identity != null && brain.Identity.Profile != null
                && brain.Identity.Profile.PersistentId == GraceCameraEpisode.ProfileId)
                return brain;
        return null;
    }

    // The next walk-in waiting in the queue to be heard; a regular there (the Officer, on Day 1) is passed over.
    static CustomerBrain NextWalkIn(CustomerBrain not)
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain != not && brain.CanHearIntake && (brain.Identity == null || !brain.Identity.IsRegular)) return brain;
        return null;
    }

    // ConversationUI's listening beat for this speech (its last line): 0.25 s and a hundredth of a second a character, within
    // the scene's listenBeat (0.35-1 s), the time the replies take to come up by themselves.
    float ListenBeat(string speech)
    {
        string[] lines = (speech ?? "").Split('\n');
        string last = lines.Length > 0 ? lines[lines.Length - 1].Trim() : "";
        Vector2 range = new Vector2(.35f, 1f);
        FieldInfo field = typeof(ConversationUI).GetField("listenBeat", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null && ui != null && field.GetValue(ui) is Vector2 v) range = v;
        return Mathf.Clamp(.25f + last.Length / 100f, Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    // The conversation's inputDelay (for the report when a quick press is thrown away).
    float InputDelay()
    {
        FieldInfo field = typeof(ConversationController).GetField("inputDelay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null && conversation != null && field.GetValue(conversation) is float f ? f : -1f;
    }

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
        ReviewScreen.Scan(shot, folder, name);   // Review 2: the words in this photo, measured (reads the shot only; never throws)
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

    static string Short(string line)
    {
        string one = (line ?? "").Replace("\n", " / ");
        return one.Length <= 90 ? one : one.Substring(0, 87) + "...";
    }

    void Finish()
    {
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Conversation check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Conversation check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Conversation check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }
}
