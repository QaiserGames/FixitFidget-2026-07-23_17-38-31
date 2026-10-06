using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

// ---------------------------------------------------------------------------
// BARKS: THE PLAY CHECK (Fixit Fidget > Night > Barks 2 - Check barks; a night walk lab session only)
//
// Out on the front street at night, with a stand-in speaker (a grey capsule, 2 m) beside Ace, it checks what
// claude/foundation-pass-build-plan.md §5 promised, and photographs each:
//   * a line is pinned over its speaker's head (the tail's tip a few pixels above the head point) at the
//     overhead camera's home, nearest and farthest zoom, and in first person; the band is the same size on
//     screen at every zoom;
//   * Ace's own line sits over Ace overhead and at the bottom of the screen in first person;
//   * a speaker off screen (behind Ace in first person, or to the side) clamps to the edge with its arrow;
//   * a line from too far away isn't heard; the ambient throttle holds (a speaker's cooldown, the gap between
//     anyone's lines); a pool says each of its lines before any repeats;
//   * a scene that holds Ace holds him (the stick does nothing), E moves it on, and he walks again after;
//     a scene that doesn't hold him runs at reading pace by itself;
//   * barks hide while the notebook is open;
//   * a line on screen adds no garbage a frame.
// Report and photos: Logs/Night/barks-check-<time>/. Nothing is saved to the scene or to any save.
// ---------------------------------------------------------------------------
public sealed class BarkCheck : MonoBehaviour
{
    public const string LabKey = "FixitFidget.Barks.Check";

    string folder;
    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    PlayerMovement movement;
    CafeViewMode view;
    Transform ace, speaker, speaker2;
    Vector3 sideways = Vector3.right;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(LabKey, 0) != 1) return;
        PlayerPrefs.DeleteKey(LabKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Bark check] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        new GameObject("Bark check (this Play session only)") { hideFlags = PlaySessionLeftovers.RuntimeFlags }.AddComponent<BarkCheck>();
    }
#endif

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "barks-check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        report.AppendLine("Barks 2 - Check barks (lab)");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        report.AppendLine();

        float until = Time.realtimeSinceStartup + 25f;
        while ((NightWalk.Instance == null || !NightWalk.Instance.Active || Camera.main == null) && Time.realtimeSinceStartup < until) yield return null;
        movement = FindAnyObjectByType<PlayerMovement>();
        view = FindAnyObjectByType<CafeViewMode>();
        Check(NightWalk.Instance != null && NightWalk.Instance.Active && movement != null && view != null, "The night walk is on, with Ace and the camera.");
        if (failures > 0) { Finish(); yield break; }
        ace = movement.transform;
        Note($"Game view {Screen.width} x {Screen.height}; the barks canvas scale {Scale():0.000}.");
        yield return Seconds(1.5f);

        var sections = new List<(Func<IEnumerator> run, string name)>
        {
            (new Func<IEnumerator>(SetUp), "setting up"), (new Func<IEnumerator>(Overhead), "overhead"),
            (new Func<IEnumerator>(FirstPerson), "first person"), (new Func<IEnumerator>(Hearing), "hearing"),
            (new Func<IEnumerator>(Throttle), "the throttle"), (new Func<IEnumerator>(Pools), "the pools"),
            (new Func<IEnumerator>(Scenes), "scenes"), (new Func<IEnumerator>(HiddenWhileNotebook), "the notebook"),
            (new Func<IEnumerator>(Garbage), "garbage"),
        };
        foreach (var (run, name) in sections) yield return Guard(run(), name);
        Finish();
    }

    // ================================================================== the sections

    IEnumerator SetUp()
    {
        // Out on the front street (the tour's spot), clear of the café's furniture.
        Teleport(new Vector3(.12f, 0f, -7.62f));
        view.SetFirstPerson(false);
        Vector3 home = view.OverheadHome;
        view.OrbitTo(home.x, home.y, home.z);
        yield return Seconds(.3f);
        // Along the camera's right, so on screen the speaker stands beside Ace, not behind: 8 m keeps their two
        // lines apart at the home zoom (a 250 px band; about 39 px a metre at 34 m).
        Vector3 right = Camera.main.transform.right;
        right.y = 0f;
        right = right.sqrMagnitude > .01f ? right.normalized : Vector3.right;
        sideways = right;
        speaker = Stand("Bark check speaker", ace.position + right * 8f);
        speaker2 = Stand("Bark check speaker 2", ace.position - right * 4f);
        Barks.Ensure();
        Barks.ClearAll();
        yield return Seconds(1f);
        Vector3 head = Barks.HeadPointOf(speaker);
        Check(Mathf.Abs(head.y - (speaker.position.y + 1f + .15f)) < .05f,
              $"The stand-in's head point is the top of what's drawn plus 0.15 m ({head.y - speaker.position.y + 1f:0.00} m above its feet).");
    }

    IEnumerator Overhead()
    {
        Vector3 home = view.OverheadHome;
        Vector4 limits = view.OverheadLimits;
        Check(Barks.Say(speaker, "lodger", "You didn't see me.", 60f), "A line from a speaker 8 m from Ace is heard (Hearing 12 m).");
        var heights = new List<float>();
        foreach (var (distance, name) in new[] { (home.z, "home"), (limits.z, "nearest"), (limits.w, "farthest") })
        {
            view.OrbitTo(home.x, home.y, distance);
            yield return Seconds(.6f);
            Pinned(speaker, $"overhead, {name} zoom ({distance:0} m)", out float height);
            heights.Add(height);
            yield return Photo($"1-overhead-{name}");
        }
        Check(heights.Count == 3 && Mathf.Max(heights[0], heights[1], heights[2]) - Mathf.Min(heights[0], heights[1], heights[2]) < 1.01f,
              $"The band is the same height on screen at every zoom ({string.Join(", ", heights.ConvertAll(h => h.ToString("0.0")))} px).");
        view.OrbitTo(home.x, home.y, home.z);
        Check(Barks.SayAce("...I didn't see you.", 60f), "Ace's own line is said.");
        yield return Seconds(.6f);
        if (Barks.TryGetShown(ace, out Barks.Shown self))
            Check(!self.slotted && self.tail && Near(self.tip.x, self.anchor.x, 1.5f),
                  $"Overhead, Ace's line sits over Ace's head (tail at {self.tip}, head at {self.anchor}).");
        else Check(false, "Overhead, Ace's line is on screen.");
        Pinned(speaker, "two lines up at once (the man's and Ace's)", out _);
        yield return Photo("2-overhead-two-speakers");
        Barks.ClearAll();
    }

    IEnumerator FirstPerson()
    {
        Check(view.SetFirstPerson(true), "First person.");
        float yaw = Yaw(ace.position, speaker.position);
        view.LookTo(yaw, -4f);
        yield return Seconds(1.4f);
        Barks.Say(speaker, "lodger", "There's a gnome on the corner step. Bring it to me.", 60f);
        yield return Seconds(.6f);
        Pinned(speaker, "first person, facing the speaker", out _);
        yield return Photo("3-first-person-facing");

        Barks.SayAce("It's a gnome. It's just a gnome.", 60f);
        yield return Seconds(.6f);
        float scale = Scale();
        if (Barks.TryGetShown(ace, out Barks.Shown self))
            Check(self.slotted && !self.tail && Near(self.band.center.x, Screen.width * .5f, 2f) && Near(self.band.y, 330f * scale, 2f),
                  $"In first person Ace's line sits at the bottom of the screen, in the middle (band at {self.band}).");
        else Check(false, "In first person, Ace's line is on screen.");
        yield return Photo("4-first-person-ace");

        // Behind Ace: clamped to the bottom with its arrow. To the side: clamped to that edge.
        view.LookTo(yaw + 180f, -4f);
        yield return Seconds(.6f);
        Clamped(speaker, "the speaker behind Ace");
        yield return Photo("5-first-person-speaker-behind");
        view.LookTo(yaw + 70f, -4f);
        yield return Seconds(.6f);
        Clamped(speaker, "the speaker off to one side (70° round)");
        yield return Photo("6-first-person-speaker-to-the-side");
        Barks.ClearAll();
        view.SetFirstPerson(false);
        yield return Seconds(1.4f);
    }

    IEnumerator Hearing()
    {
        speaker2.position = ace.position - sideways * 20f + Vector3.up * (speaker2.position.y - ace.position.y);
        yield return null;
        Check(!Barks.Say(speaker2, "neighbour", "Who's out there at this hour?") && !Barks.TryGetShown(speaker2, out _),
              "A line from 20 m away isn't heard (Hearing 12 m).");
        Check(!Barks.SayAmbient(speaker2, "neighbour", "Go home!"), "Nor is an ambient line from there.");
        speaker2.position = ace.position - sideways * 4f + Vector3.up * (speaker2.position.y - ace.position.y);
        yield return null;
    }

    IEnumerator Throttle()
    {
        BarkRules rules = Barks.Instance.Rules;
        float cooldown = rules.speakerCooldown, gap = rules.globalGap;
        rules.speakerCooldown = 5f;
        rules.globalGap = 2f;
        rules.Reset();
        Check(Barks.SayAmbient(speaker, "neighbour", "One."), "An ambient line when nobody has spoken.");
        Check(!Barks.SayAmbient(speaker, "neighbour", "Two."), "The same speaker again at once: refused (their cooldown).");
        Check(!Barks.SayAmbient(speaker2, "lodger", "Three."), "Someone else at once: refused (the gap between anyone's lines).");
        yield return Seconds(2.2f);
        Check(Barks.SayAmbient(speaker2, "lodger", "Three."), "Someone else after the gap: said.");
        Check(!Barks.SayAmbient(speaker, "neighbour", "Four."), "The first speaker still inside their cooldown: refused.");
        yield return Photo("7-two-ambient-lines");
        yield return Seconds(3.2f);
        Check(Barks.SayAmbient(speaker, "neighbour", "Four."), "The first speaker after their cooldown: said.");
        rules.speakerCooldown = cooldown;
        rules.globalGap = gap;
        rules.Reset();
        Barks.ClearAll();
    }

    IEnumerator Pools()
    {
        NightLines lines = NightLines.Current;
        Check(lines != null, "The Night lines asset loads from Resources.");
        if (lines == null) yield break;
        IReadOnlyList<NightLines.Line> pool = lines.Pool("lodger", "lodger.waiting");
        Check(pool.Count >= 2, $"The man's 'waiting' pool has lines ({pool.Count}).");
        BarkRules rules = Barks.Instance.Rules;
        float cooldown = rules.speakerCooldown, gap = rules.globalGap;
        rules.speakerCooldown = 0f;
        rules.globalGap = 0f;
        var said = new HashSet<string>();
        for (int i = 0; i < pool.Count; i++)
        {
            Check(Barks.SayFrom(speaker, "lodger", "lodger.waiting"), $"Pool line {i + 1} is said.");
            yield return null;
            if (Barks.TryGetShown(speaker, out Barks.Shown s)) said.Add(s.text);
        }
        Check(said.Count == pool.Count, $"A round of the pool says each of its {pool.Count} lines once ({said.Count} different).");
        rules.speakerCooldown = cooldown;
        rules.globalGap = gap;
        rules.Reset();
        Barks.ClearAll();
    }

    IEnumerator Scenes()
    {
        Vector3 before = ace.position;
        bool done = false;
        int lines = 0;
        Check(Barks.PlayLines(new[] { "lodger", "ace", "lodger" }, new[] { "Bring me the gnome.", "Why?", "Don't ask why. Ask how." }, true,
                              id => id == "lodger" ? speaker : null, (i, text) => lines++, () => done = true),
              "A scene that holds Ace starts.");
        Check(Barks.ScenePlaying && PlayerMovement.Held && Barks.SceneLine == 0, "It plays its first line and holds Ace.");
        movement.ScriptedInput = new Vector2(0f, 1f);
        yield return Seconds(.7f);
        movement.ScriptedInput = null;
        Check(Vector3.Distance(Flat(before), Flat(ace.position)) < .03f, $"Held, the stick moves Ace nowhere ({Vector3.Distance(Flat(before), Flat(ace.position)):0.000} m).");
        yield return Photo("8-scene-line-1");
        Barks.Advance();
        yield return Seconds(.5f);
        Check(Barks.SceneLine == 1, "E moves the scene to its second line (Ace's).");
        if (Barks.TryGetShown(ace, out Barks.Shown self)) Check(self.tail && !self.slotted, "Ace's scene line sits over Ace.");
        if (Barks.TryGetShown(speaker, out Barks.Shown first)) Check(first.alpha < .7f, $"The man's answered line stays up, faded ({first.alpha:0.00}).");
        yield return Photo("9-scene-line-2");
        Barks.Advance();
        yield return null;
        Barks.Advance();
        yield return null;
        Check(done && !Barks.ScenePlaying && !PlayerMovement.Held && lines == 3, "After its last line the scene ends, says so, and lets Ace go.");
        before = ace.position;
        movement.ScriptedInput = new Vector2(0f, 1f);
        yield return Seconds(.4f);
        movement.ScriptedInput = null;
        Check(Vector3.Distance(Flat(before), Flat(ace.position)) > .5f, "Ace walks again after the scene.");
        Teleport(new Vector3(.12f, 0f, -7.62f));
        yield return Seconds(.3f);

        // A scene that doesn't hold Ace runs at reading pace by itself.
        done = false;
        Check(Barks.PlayLines(new[] { "lodger", "lodger" }, new[] { "Tonight, then.", "Still waiting." }, false, id => speaker, null, () => done = true),
              "A scene that doesn't hold Ace starts.");
        Check(!PlayerMovement.Held, "It doesn't hold Ace.");
        float limit = 2f * (Barks.Instance.Rules.ReadingSeconds("Still waiting.") + Barks.Instance.sceneBeat) + 1f;
        float t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < limit) yield return null;
        Check(done, $"It ends by itself at reading pace ({Time.realtimeSinceStartup - t0:0.0} s for two short lines).");
        Barks.ClearAll();
    }

    IEnumerator HiddenWhileNotebook()
    {
        NightNotebook notebook = NightWalk.Instance != null ? NightWalk.Instance.NotebookPage : null;
        if (notebook == null) { Note("No notebook page tonight; skipped."); yield break; }
        Barks.Say(speaker, "lodger", "Read it later.", 30f);
        yield return null;
        notebook.Show(true);
        yield return null;
        yield return null;
        Check(Barks.Hidden, "Barks hide while the notebook is open.");
        yield return Photo("10-notebook-open");
        notebook.Show(false);
        yield return null;
        yield return null;
        Check(!Barks.Hidden && Barks.TryGetShown(speaker, out _), "They come back when it closes, the line still there.");
        Barks.ClearAll();
    }

    IEnumerator Garbage()
    {
        // Allocations a frame (the profiler's own count), with nothing said and then with two lines up. The
        // lowest of three windows each, so a passer-by being made somewhere else doesn't count.
        var without = new List<double>();
        var with = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            yield return Measure(without);
        }
        Barks.Say(speaker, "lodger", "Allocations are counted now.", 30f);
        Barks.SayAce("Still counting.", 30f);
        yield return Seconds(.5f);
        for (int i = 0; i < 3; i++)
        {
            yield return Measure(with);
        }
        double a = Min(without), b = Min(with);
        Check(b - a <= 32.0, $"Two lines on screen add no garbage a frame ({a:0} B without, {b:0} B with; lowest of 3 windows of 60 frames).");
        Barks.ClearAll();
    }

    IEnumerator Measure(List<double> into)
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

    static double Min(List<double> values)
    {
        double m = double.MaxValue;
        foreach (double v in values) m = Math.Min(m, v);
        return values.Count > 0 ? m : 0d;
    }

    // ================================================================== what's checked

    void Pinned(Transform who, string where, out float bandHeight)
    {
        bandHeight = 0f;
        if (!Barks.TryGetShown(who, out Barks.Shown s)) { Check(false, $"{where}: the line is on screen."); return; }
        bandHeight = s.band.height;
        Vector3 p = Camera.main.WorldToScreenPoint(Barks.HeadPointOf(who));
        float gap = 6f * Scale();
        bool onHead = Near(s.anchor.x, p.x, 1.5f) && Near(s.anchor.y, p.y, 1.5f);
        bool tipAbove = s.tail && Near(s.tip.x, s.anchor.x, 1.5f) && Near(s.tip.y - s.anchor.y, gap, 1.5f);
        Check(onHead && tipAbove && !s.clamped && s.alpha > .9f,
              $"{where}: pinned over the head (head {Px(p)}, tail's tip {Px(s.tip)}, band {s.band.width:0} x {s.band.height:0} px, alpha {s.alpha:0.00}).");
    }

    void Clamped(Transform who, string where)
    {
        if (!Barks.TryGetShown(who, out Barks.Shown s)) { Check(false, $"{where}: the line is on screen."); return; }
        bool inside = s.band.xMin >= -.5f && s.band.xMax <= Screen.width + .5f && s.band.yMin >= -.5f && s.band.yMax <= Screen.height + .5f;
        Check(s.clamped && s.arrow && !s.tail && inside, $"{where}: clamped to the screen's edge with its arrow (band {s.band}).");
    }

    // ================================================================== helpers

    Transform Stand(string name, Vector3 feet)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.hideFlags = PlaySessionLeftovers.RuntimeFlags;
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = new Vector3(.6f, 1f, .6f);
        if (Physics.Raycast(feet + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            feet.y = hit.point.y;
        go.transform.position = feet + Vector3.up;   // a 2 m capsule's middle
        return go.transform;
    }

    void Teleport(Vector3 at)
    {
        if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            at.y = hit.point.y;
        var controller = movement.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        float lift = controller != null ? controller.height * .5f - controller.center.y + .03f : 1.03f;
        ace.position = at + Vector3.up * lift;
        if (controller != null) controller.enabled = was;
    }

    static float Yaw(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static bool Near(float a, float b, float within) => Mathf.Abs(a - b) <= within;
    static string Px(Vector2 v) => $"({v.x:0.0}, {v.y:0.0})";

    // The barks canvas's scale (a CanvasScaler at 1920 x 1080, matched half width, half height).
    static float Scale() => Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(Screen.width / 1920f, 2f), Mathf.Log(Screen.height / 1080f, 2f), .5f));

    IEnumerator Guard(IEnumerator section, string name)
    {
        while (true)
        {
            object current;
            try
            {
                if (!section.MoveNext()) yield break;
                current = section.Current;
            }
            catch (Exception e)
            {
                Check(false, $"{name}: threw {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
                yield break;
            }
            yield return current;
        }
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

    void Finish()
    {
        if (movement != null) movement.ScriptedInput = null;
        Barks.ClearAll();
        if (speaker != null) Destroy(speaker.gameObject);
        if (speaker2 != null) Destroy(speaker2.gameObject);
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Bark check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Bark check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Bark check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) movement.ScriptedInput = null;
    }
}
