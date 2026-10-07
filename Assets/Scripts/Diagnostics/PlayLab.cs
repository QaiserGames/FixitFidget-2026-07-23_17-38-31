using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// A PLAY CHECK'S COMMON PARTS (playtest 3, session 2: the stations, the aim help, the slimmer Ace)
//
// What every lab check that drives the game by itself needs: the steps run one after another (a nested step runs in
// place, and an exception ends the check with a failure rather than leaving it hanging), checks and notes in a report,
// photos, waits with a time limit, a virtual gamepad pressed the way a player would (nothing to hold, nothing saved), and
// a count of anything that logs an error while it runs. Each check is a subclass; the report and photos go to its folder.
// Nothing is saved in the scene; the lab's own save is the only file the game writes.
// ---------------------------------------------------------------------------
public abstract class PlayLab : MonoBehaviour
{
    public string folder;

    protected readonly StringBuilder report = new StringBuilder();
    readonly List<string> errorLines = new List<string>();
    protected int checks, failures, photos, errors;
    protected float started;
    protected bool lastWait;
    protected Gamepad pad;

    /// <summary>The check's name, as its report and log lines say it.</summary>
    protected abstract string Title { get; }
    /// <summary>The check's own log tag ("[Stations check]").</summary>
    protected abstract string Tag { get; }
    /// <summary>The steps.</summary>
    protected abstract IEnumerator Run();
    /// <summary>Put back whatever the check changed (always runs, before the report).</summary>
    protected virtual void Restore() { }

    protected virtual void OnEnable() => Application.logMessageReceived += Heard;
    protected virtual void OnDisable() => Application.logMessageReceived -= Heard;

    // Anything that logs an error while the check runs is counted (the check's own lines aside).
    void Heard(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message != null && message.StartsWith(Tag, StringComparison.Ordinal)) return;
        errors++;
        if (errorLines.Count < 6) errorLines.Add(Short((message ?? "").Split('\n')[0]));
    }

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        Directory.CreateDirectory(folder);
        report.AppendLine(Title + ", lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"{Tag} Running: hands off the mouse and keyboard until it reports. {folder}");

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

    void Finish()
    {
        try { Restore(); }
        catch (Exception e) { Note("putting things back failed: " + e.Message); }
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null;
        Check(errors == 0, $"nothing logged an error while the check ran ({errors}" + (errorLines.Count > 0 ? ": " + string.Join(" | ", errorLines) : "") + ")");
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning($"{Tag} Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"{Tag} All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"{Tag} {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    // ---------- the report ----------

    protected void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    protected void Note(string what) => report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");

    protected static string Short(string line)
    {
        string one = (line ?? "").Replace("\n", " / ");
        return one.Length <= 110 ? one : one.Substring(0, 107) + "...";
    }

    protected IEnumerator Photo(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), shot.EncodeToJPG(88));
        Destroy(shot);
    }

    // ---------- waiting ----------

    protected IEnumerator Until(Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
        Check(lastWait, what + (lastWait ? "" : $" (gave up after {seconds:0} s)"));
    }

    // The same without a check line: lastWait says whether it came true.
    protected IEnumerator Await(Func<bool> condition, float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
    }

    protected static bool Safe(Func<bool> condition)
    {
        try { return condition(); }
        catch (Exception) { return false; }
    }

    protected static IEnumerator Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    protected static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target) yield return null;
    }

    // ---------- a virtual gamepad ----------

    protected void PlugInPad(string name)
    {
        if (pad == null || !pad.added) pad = InputSystem.AddDevice<Gamepad>(name);
    }

    protected void PadHold(GamepadState state) { if (pad != null) InputSystem.QueueStateEvent(pad, state); }
    protected void PadRelease() { if (pad != null) InputSystem.QueueStateEvent(pad, new GamepadState()); }

    /// <summary>A press and a release, a few frames each (the way a thumb does it).</summary>
    protected IEnumerator PadPress(GamepadButton button)
    {
        PadHold(new GamepadState().WithButton(button));
        yield return Frames(3);
        PadRelease();
        yield return Frames(3);
    }

    /// <summary>A trigger pulled and let go (RT is "use" in first person and at the bench).</summary>
    protected IEnumerator PadTrigger(bool right)
    {
        PadHold(right ? new GamepadState { rightTrigger = 1f } : new GamepadState { leftTrigger = 1f });
        yield return Frames(3);
        PadRelease();
        yield return Frames(3);
    }
}
