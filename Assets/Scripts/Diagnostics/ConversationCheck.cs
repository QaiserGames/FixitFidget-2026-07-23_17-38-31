using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        yield return Until(() => (next = NextAtTheCounter(grace)) != null, 150f, "the next customer reaches the counter");
        if (!lastWait || next == null) yield break;
        bool regular = next.Identity != null && next.Identity.IsRegular;
        Check(!regular, "they are a walk-in");
        yield return Seconds(.3f);
        conversation.Begin(next);
        yield return null;
        yield return Until(() => conversation.RepliesShowing, 12f, "their line, then Ace's replies");
        if (!lastWait) yield break;
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

    static CustomerBrain NextAtTheCounter(CustomerBrain not)
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain != not && brain.CanHearIntake) return brain;
        return null;
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
