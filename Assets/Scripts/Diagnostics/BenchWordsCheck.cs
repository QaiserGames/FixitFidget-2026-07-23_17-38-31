using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ---------------------------------------------------------------------------
// BENCH 6 - THE WORDS ON THE BENCH SCREEN: A PLAY CHECK (checklist v2, 14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md
// §6 B; the Producer's feel target T7)
//
// His playtest 5: "the checklist is terrible"; the First Hour counted about 120 words on the first repair's screen; his
// decision 7: "Under 25 words on the bench screen". This counts what the player sees, as he would: every TextMesh Pro text
// that is on, on a screen canvas that is on, its CanvasGroups and its own colour at least half opaque, and on the screen;
// rich text stripped, words counted as WordBudget counts them; the tab's initial (an icon) left out. With the keyboard's
// words and then the pad's (the input flag set for the measure, as the Day 1 guide lab does):
//
//   S1 THE FIRST REPAIR: a fresh Day 1, Grace's own visit (she comes in, Ace takes her camera, picks it up from the intake
//      tray: the tray doesn't offer it back on the carry step, sets it on the bench, works on it), the cloth not taken:
//      at most 25 words (T7), the glow on the cloth in its stand; the cursor moved onto the lens when the Game view has the
//      mouse (otherwise the hover is counted as its one word, and the report says so);
//   S2 STEADY: the pocket watch's seized movement (a lab device; Grace stands in as its owner for the card's line), its back
//      up, the first repair's card off: at most 24 (the aim is 15); again at last orders;
//   S3 HIDDEN: the phone face up (its screws are on its back): "[R] Flip it over", the whole device breathing; then Grace's
//      camera's swap (fault 1), any target out of sight drawn dashed;
//   S4 FINISHED: the camera's clean done ("Fixed!" up, the card folded to "[E] Take it to ..."): at most 24;
//   S5 HELD: Tab held (with Unity in front): the full controls card, every control, no budget; the press showed what's left;
//   S6 AN ALERT: Grace frustrated while Ace works: the bench's impatience card alone at most 12, in the stage's zone U6.
//
// In each state, at every quarter turn and turned over (as Bench 5 does): no text or card over the device's projected box,
// the mat's compartments (BenchStage's cups, pads, wells and scrap slots) or the tools in their stand; the cards in their
// zones (BenchStageContract: the job card in U3, the controls and the hint in U5, the tabs in U1, the alert in U6); no
// text under the cursor's point; nothing smaller than 16 px at 1080p. Every element is listed with its words and its rect.
//
// Run it twice: with the Game view at 1920 x 1080, then at 2560 x 1600 (his Mac's 16:10). Fixit Fidget › Bench › Bench 6 -
// Words on the bench screen (lab, a check). Photos and report.txt go to Logs/BenchWords/check-<time>/. Nothing is saved in
// the scene; the lab's own save only. Its timings are generous (guesses until the Mac runs them).
// ---------------------------------------------------------------------------
public sealed class BenchWordsCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.BenchWordsCheck";

    protected override string Title => "Bench 6 - words on the bench screen";
    protected override string Tag => "[Bench words lab]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Bench words lab] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Bench words lab (this Play session only)");
        go.AddComponent<BenchWordsCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BenchWords",
            $"check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const string Phone = "Assets/AssetsPrefabs/PhoneRepair.prefab";
    const string Watch = "Assets/AssetsPrefabs/PocketWatch.prefab";
    const string Camera_ = "Assets/GraceShowcase/GraceReunionCamera.prefab";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    ConversationController conversation;
    CustomerSpawner spawner;
    DayOneGuideUI guide;
    StationInteractable bench;
    CustomerBrain grace;
    bool spawnerWas = true, spawnerTouched;
    MethodInfo setPad;
    bool padWas;
    readonly List<GameObject> made = new List<GameObject>();
    readonly StringBuilder totals = new StringBuilder();
    static readonly Color JobColour = new Color(.95f, .55f, .2f, 1f);

    struct Shown
    {
        public string text, path;
        public int words;
        public Rect rect;
        public float size;
        public bool overDevice;   // drawn over the device by design (the hover at the cursor, "Fixed!")
        public bool hover;        // the hover card at the cursor
    }

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.Day == 1 && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver
                                 && Camera.main != null, 30f, "the lab opened on a fresh Day 1");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        guide = FindAnyObjectByType<DayOneGuideUI>();
        bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(interactor != null && view != null && carry != null && inspector != null && conversation != null && bench != null,
            "Ace, his hands, his bench and the bench station are in the scene");
        if (interactor == null || view == null || carry == null || inspector == null || conversation == null || bench == null) yield break;
        Check(BenchStage.Instance != null, "the bench's stage is in the scene (its compartments are checked against the words)");
        setPad = typeof(PadInput).GetProperty("UsingPad", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true);
        padWas = PadInput.UsingPad;
        if (view.FirstPersonSelected) { view.SetFirstPerson(false); yield return Seconds(1.5f); }
        Note($"the Game view is {Screen.width} x {Screen.height} ({(Mathf.Abs(Screen.width / (float)Screen.height - 1.6f) < .02f ? "16:10, his Mac's" : "16:9 or other")}); " +
             $"Unity in front: {(Application.isFocused ? "yes" : "no: the cursor and the held key are left out")}");

        yield return FirstRepair();

        // The rest on lab devices, the café quiet (as Bench 5 does).
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; spawnerTouched = true; }
        yield return Steady();
        yield return Hidden();
        yield return Finished();
        yield return Held();
        yield return Alert();

        report.AppendLine();
        report.AppendLine("Totals, state by state:");
        report.Append(totals);
    }

    // ------------------------------------------------------------------ S1: the first repair, Grace's own visit

    IEnumerator FirstRepair()
    {
        Note("---------- S1: the first repair ----------");
        JobBase camera = null;
        yield return Await(() => (grace = FindRegular(GraceCameraEpisode.ProfileId)) != null, 90f);
        if (grace != null) yield return Await(() => grace == null || grace.CanHearIntake, 180f);
        if (grace != null && grace.CanHearIntake)
        {
            Transform till = interactor.CounterStation != null ? interactor.CounterStation.StandPoint : null;
            if (till != null) PutAce(till.position, till.eulerAngles.y);
            conversation.Begin(grace);
            yield return Frames(2);
            conversation.Advance();
            yield return Await(() => conversation.RepliesShowing, 10f);
            int take = conversation.RepliesShowing ? conversation.Replies.ToList().FindIndex(r => r.Kind == ReplyKind.Accept) : -1;
            if (take >= 0)
            {
                conversation.ChooseReply(take);
                for (int i = 0; i < 8 && conversation.InConversation; i++) { conversation.Advance(); yield return Seconds(.4f); }
                yield return Await(() => !conversation.InConversation && grace != null && grace.ActiveJob != null, 12f);
                camera = grace != null ? grace.ActiveJob : null;
            }
        }
        if (camera != null)
        {
            // Picked up at the till: on Day 1's carry step the tray doesn't offer it back (§5.7, the First Hour's #4).
            ItemInteractable item = camera.GetComponentInChildren<ItemInteractable>();
            if (item != null) item.Interact(interactor);
            yield return Await(() => carry.Contains(camera), 3f);
            yield return Seconds(.6f);   // the guide reads the café every 0.15 s
            string prompt = interactor.CurrentPrompt ?? "";
            Check(carry.Contains(camera) && !prompt.Contains(DropInteractable.IntakeTrayWords) && !prompt.Contains(DropInteractable.PutBackWords),
                $"picked up at the till on Day 1's carry step, E doesn't offer to put it back (the line: \"{(prompt.Length > 0 ? prompt : "nothing")}\")");
            PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
            yield return Seconds(.6f);
            bench.Interact(interactor);
            yield return Await(() => StationInteractable.BenchHolds(camera), 3f);
        }
        else
        {
            Note("Grace's visit didn't come round within the check's time: a lab camera with no owner stands in (the card's line has no name: one word fewer than his)");
            camera = Make(Camera_, 0, out _);
            if (camera != null)
            {
                carry.TryPickUp(camera);
                PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
                yield return Seconds(.8f);
                bench.Interact(interactor);
                yield return Seconds(.6f);
            }
        }
        if (camera == null) { Check(false, "S1: a camera to work on"); yield break; }
        if (!inspector.BeginInspection(camera)) { Check(false, "S1: the close-up opens on the camera"); yield break; }
        yield return AfterBlend();
        yield return Seconds(1.2f);   // the card slides in (0.25 s), the corners go quiet (0.2 s), the tabs become chips
        Check(BenchCards.FirstRepairNow, "S1: it is the first repair (Day 1, nothing handed back yet)");
        Check(guide == null || !guide.CardShowing, "S1: no Day 1 card at the bench (the bench's card and the glow are the guide)");
        Check(BenchGlow.GlowingTool != null && BenchGlow.GlowingTool.tool == ToolType.Cloth,
            $"S1: the cloth glows in its stand (glowing: {(BenchGlow.GlowingTool != null ? BenchGlow.GlowingTool.displayName : "no tool")}; the card's icon \"{BenchCards.CurrentIcon}\", verb \"{BenchCards.CurrentVerb}\")");
        bool hovered = HoverOnLens(camera);
        yield return Frames(6);
        foreach (bool pad in new[] { false, true })
        {
            SetPad(pad);
            yield return Seconds(.4f);
            string input = pad ? "pad" : "mouse";
            Check(BenchCards.ControlsShowing && !BenchCards.ControlsHeld, $"S1, {input}: the first repair's controls card is up: \"{BenchCards.Plain(BenchCards.ControlsText)}\"");
            Measure($"S1 first repair, {input}", WordBudget.BenchFirstRepair, camera, !hovered);
            yield return Photo($"S1-first-repair-{input}");
        }
        SetPad(false);
        yield return Overlaps("S1", camera);
        inspector.CancelInspection();
        yield return AfterBlend();
    }

    // Moves the mouse onto the camera's lens (or the device's middle) with Unity in front; false when it can't.
    bool HoverOnLens(JobBase job)
    {
        if (!Application.isFocused || Mouse.current == null || Camera.main == null) return false;
        GrimeSpot spot = job.GetComponentsInChildren<GrimeSpot>().FirstOrDefault(s => !s.IsClean && s.Tool == ToolType.Cloth);
        Vector3 at = spot != null ? BenchInteractable.BoundsCentre(spot.transform) : DeviceBounds.Centre(job.gameObject);
        Vector3 p = Camera.main.WorldToScreenPoint(at);
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(p.x, p.y) });
        return true;
    }

    // ------------------------------------------------------------------ S2: steady play

    IEnumerator Steady()
    {
        Note("---------- S2: steady play (the watch's seized movement, its back up) ----------");
        BenchCards.LabControls = false;
        JobBase watch = Make(Watch, -1, out string fault, "Seized");
        if (watch == null) yield break;
        if (grace != null) watch.SetOwner(grace);   // a name for the card's line, as a customer's device has
        yield return OpenOnBench(watch);
        if (!inspector.IsHoldingItem) yield break;
        if (inspector.FaceUp > 0f && BenchCards.CurrentStep != null && BenchCards.CurrentStep.under)
        {
            inspector.FlipOver();
            yield return Await(() => !inspector.Flipping, 2f);
            yield return Seconds(.4f);
        }
        Note($"the watch's fault: \"{fault}\"; the card: \"{BenchCards.JobLine}\" / \"{BenchCards.CurrentVerb}\"");
        Check(BenchCards.HintShowing && !BenchCards.ControlsShowing, $"S2: steady play shows the one hint, \"{BenchCards.Plain(BenchCards.HintText)}\"");
        bool hovered = HoverOnScrew(watch);
        yield return Frames(6);
        foreach (bool pad in new[] { false, true })
        {
            SetPad(pad);
            yield return Seconds(.4f);
            int n = Measure($"S2 steady, {(pad ? "pad" : "mouse")}", WordBudget.BenchSteady, watch, !hovered);
            if (n > WordBudget.BenchSteadyAim) Note($"S2 steady: {n} words, over the aim of {WordBudget.BenchSteadyAim}");
        }
        SetPad(false);
        yield return Photo("S2-steady");
        yield return Overlaps("S2", watch);

        // Again at last orders (the sign's two words), then the clock put back where it was (the day mustn't end mid-lab).
        DayClock clock = DayClock.Instance;
        MethodInfo setRemaining = typeof(DayClock).GetProperty("TimeRemaining", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);
        float length = (float)(typeof(DayClock).GetField("dayLengthSeconds", Any)?.GetValue(clock) ?? 0f);
        if (clock != null && setRemaining != null && length > 0f)
        {
            float before = clock.TimeRemaining;
            // NormalizedDay = 1 - TimeRemaining / length; the hour runs from opening to closing; last orders is the last hour.
            float n = Mathf.InverseLerp(clock.OpeningHour, clock.ClosingHour, clock.ClosingHour - .7f);
            setRemaining.Invoke(clock, new object[] { (1f - n) * length });
            yield return Seconds(1.2f);
            Check(HudCorners.Instance == null || HudCorners.Instance.LastOrders, "S2: the clock moved on to last orders");
            Measure("S2 steady at last orders, mouse", WordBudget.BenchSteady, watch, !hovered);
            yield return Photo("S2b-steady-last-orders");
            setRemaining.Invoke(clock, new object[] { before });
            yield return Seconds(.6f);
        }
        else Note("S2: the clock couldn't be moved to last orders here: that measure is left out");
        yield return Close(watch);
    }

    bool HoverOnScrew(JobBase job)
    {
        if (!Application.isFocused || Mouse.current == null || Camera.main == null) return false;
        RepairChecklist.Step step = BenchCards.CurrentStep;
        Component target = step != null && step.targets != null ? step.targets.FirstOrDefault(t => t != null) : null;
        Vector3 at = target != null ? BenchInteractable.BoundsCentre(target.transform) : DeviceBounds.Centre(job.gameObject);
        Vector3 p = Camera.main.WorldToScreenPoint(at);
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(p.x, p.y) });
        return true;
    }

    // ------------------------------------------------------------------ S3: hidden work

    IEnumerator Hidden()
    {
        Note("---------- S3: hidden work ----------");
        BenchCards.LabControls = false;
        JobBase phone = Make(Phone, 0, out string fault);
        if (phone != null)
        {
            yield return OpenOnBench(phone);
            if (inspector.IsHoldingItem)
            {
                yield return Seconds(.5f);
                RepairChecklist.Step step = BenchCards.CurrentStep;
                Check(step != null && step.under && BenchCards.CurrentVerb.Contains("Flip it over"),
                    $"S3a: the phone face up, its screws on its back: the card says \"{BenchCards.CurrentVerb}\" (\"{fault}\")");
                Check(BenchGlow.DeviceGlowing, "S3a: the whole device breathes (the work is on the face turned down)");
                Measure("S3a the phone face up, mouse", WordBudget.BenchSteady, phone, true);
                yield return Photo("S3a-flip-it-over");
                yield return Overlaps("S3a", phone);
                yield return Close(phone);
            }
        }

        // Grace's swap (fault 1 once the camera has two faults): anything the step wants that is out of sight is drawn dashed.
        JobBase camera = Make(Camera_, 1, out string swap);
        if (camera == null) yield break;
        if (grace != null) camera.SetOwner(grace);
        yield return OpenOnBench(camera);
        if (!inspector.IsHoldingItem) yield break;
        yield return Seconds(.6f);
        RepairChecklist.Step now = BenchCards.CurrentStep;
        int hidden = 0;
        if (now != null && now.targets != null)
            foreach (Component t in now.targets) if (t != null && !inspector.IsSeen(t.transform)) hidden++;
        Note($"S3b: \"{swap}\", the step \"{BenchCards.CurrentVerb}\", {(now != null && now.targets != null ? now.targets.Count : 0)} targets, {hidden} out of sight");
        if (hidden > 0)
            Check(BenchGlow.DashedCount > 0, $"S3b: a target out of sight glows dashed ({BenchGlow.DashedCount} dashed)");
        else Note("S3b: nothing the step wants is out of sight from this framing: the dashed glow isn't seen here (the hand in Bench 8 meets it)");
        Measure("S3b Grace's swap, mouse", WordBudget.BenchSteady, camera, true);
        yield return Photo("S3b-swap");
        yield return Close(camera);
    }

    // ------------------------------------------------------------------ S4: finished

    IEnumerator Finished()
    {
        Note("---------- S4: finished ----------");
        BenchCards.LabControls = false;
        JobBase camera = Make(Camera_, 0, out string fault);
        if (camera == null) yield break;
        if (grace != null) camera.SetOwner(grace);
        yield return OpenOnBench(camera);
        if (!inspector.IsHoldingItem) yield break;
        yield return Seconds(.5f);
        // The labs' hand: each spot cleaned at once (GrimeSpot's whole-spot scrub).
        foreach (GrimeSpot spot in camera.GetComponentsInChildren<GrimeSpot>()) if (spot != null && !spot.IsClean) spot.Scrub(1000f);
        yield return Await(() => camera.IsFinished, 6f);
        if (!camera.IsFinished)
        {
            Note($"S4: the clean (\"{fault}\") didn't finish the camera by its spots alone (its shutter is in this fault until the camera has two): \"Fixed!\" isn't measured");
            yield return Close(camera);
            yield break;
        }
        yield return Seconds(1.2f);   // the checks hold 0.6 s, then the row folds
        Check(BenchCards.FinishedShowing && BenchCards.FinishedText.Contains("[" + ControlHints.TakeFromBench + "]"),
            $"S4: the card folds into its way out: \"{BenchCards.FinishedText}\"");
        Check(BenchGlow.Count == 0, $"S4: nothing glows at \"Fixed!\" ({BenchGlow.Count})");
        Measure("S4 finished, mouse", WordBudget.BenchSteady, camera, true);
        yield return Photo("S4-finished");
        yield return Overlaps("S4", camera);
        yield return Close(camera);
    }

    // ------------------------------------------------------------------ S5: help held

    IEnumerator Held()
    {
        Note("---------- S5: Tab held ----------");
        if (!Application.isFocused || Keyboard.current == null)
        {
            Note("S5: Unity is in the background (a key held from here doesn't arrive): left out");
            yield break;
        }
        BenchCards.LabControls = false;
        JobBase watch = Make(Watch, 0, out _);
        if (watch == null) yield break;
        yield return OpenOnBench(watch);
        if (!inspector.IsHoldingItem) yield break;
        yield return Seconds(.5f);
        SetPad(false);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Tab));
        yield return Frames(2);
        Check(BenchGlow.Flashing, "S5: the press shows what's left at once (before the hold)");
        yield return Seconds(.6f);
        Check(BenchCards.ControlsHeld, $"S5: held 0.35 s, the full controls card: \"{BenchCards.Plain(BenchCards.ControlsText)}\"");
        Measure("S5 help held (no budget)", int.MaxValue, watch, true);
        yield return Photo("S5-help-held");
        OverlapCheck("S5", watch, false);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return Seconds(.3f);
        Check(!BenchCards.ControlsHeld && BenchCards.HintShowing, "S5: let go, the card goes and the hint is back");
        yield return Close(watch);
    }

    // ------------------------------------------------------------------ S6: an alert

    IEnumerator Alert()
    {
        Note("---------- S6: an alert at the bench ----------");
        FieldInfo left = typeof(CustomerBrain).GetField("patienceLeft", Any);
        if (guide == null || grace == null || left == null || left.FieldType != typeof(float) || !grace.ShowsPatience || DayOneGuideUI.ReassuredOnce)
        {
            Note("S6: no one waiting whose patience the lab can set (or the impatience card has done its job this session): left out");
            yield break;
        }
        BenchCards.LabControls = false;
        JobBase watch = Make(Watch, 0, out _);
        if (watch == null) yield break;
        yield return OpenOnBench(watch);
        if (!inspector.IsHoldingItem) yield break;
        float was = (float)left.GetValue(grace);
        float max = grace.PatienceFraction > .001f ? was / grace.PatienceFraction : 0f;
        if (max > 0f) left.SetValue(grace, max * .2f);
        yield return Await(() => guide.ImpatienceShowing, 4f);
        if (guide.ImpatienceShowing)
        {
            string text = BenchCards.Plain(guide.CardText);
            int words = WordBudget.Words(text);
            Check(words <= WordBudget.BenchAlert && text.Contains("[" + ControlHints.Cancel + "]") && guide.CardAtBench,
                $"S6: the bench's impatience card alone: \"{text}\" ({WordBudget.Report(text, WordBudget.BenchAlert)})");
            InZone(guide.CardOnScreen, BenchStageContract.Zone.U6, "S6: the impatience card");
            yield return Photo("S6-impatient-at-the-bench");
        }
        else Note("S6: the impatience card didn't show (it shows once a day)");
        if (grace != null) left.SetValue(grace, was);
        yield return Close(watch);
    }

    // ------------------------------------------------------------------ counting

    // Every word on the screen now, as Bench 6's rule counts it. Also checks the smallest text and the cursor's point.
    int Measure(string state, int budget, JobBase job, bool hoverAssumed)
    {
        List<Shown> shown = Count();
        int total = shown.Sum(s => s.words);
        bool hoverSeen = shown.Any(s => s.hover);
        int assumed = hoverAssumed && !hoverSeen ? 1 : 0;
        foreach (Shown s in shown)
            Note($"  {state}: {s.words,2} \"{Short(s.text)}\" at {Px(s.rect)}, {s.size:0} px ({s.path})");
        if (budget < int.MaxValue)
            Check(total + assumed <= budget, $"{state}: {total + assumed} words on the bench screen (the rule is {budget})" +
                                             (assumed > 0 ? "; the hover counted as its one word (the cursor wasn't moved)" : ""));
        List<Shown> small = shown.Where(s => s.size < 15.5f).ToList();
        Check(small.Count == 0, $"{state}: every text is at least 16 px at 1080p" +
                                (small.Count > 0 ? " (smaller: " + string.Join("; ", small.Select(s => $"\"{Short(s.text)}\" {s.size:0} px")) + ")" : ""));
        Vector2 cursor = GamePointer.Position;
        List<Shown> under = shown.Where(s => s.rect.Contains(cursor)).ToList();
        Check(under.Count == 0, $"{state}: no words under the cursor's point ({cursor.x:0}, {cursor.y:0})" +
                                (under.Count > 0 ? ": " + string.Join("; ", under.Select(s => $"\"{Short(s.text)}\"")) : ""));
        totals.AppendLine($"  {state}: {total + assumed} words{(budget < int.MaxValue ? $" (rule {budget})" : "")}{(assumed > 0 ? ", hover assumed" : "")}");
        return total + assumed;
    }

    List<Shown> Count()
    {
        var list = new List<Shown>();
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text == null || !text.isActiveAndEnabled) continue;
            Canvas canvas = text.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            Canvas root = canvas.rootCanvas;
            if (root == null || root.renderMode == RenderMode.WorldSpace || !root.enabled) continue;
            if (text.color.a < .5f || GroupAlpha(text.transform) < .5f) continue;
            if (text.name == "Customer initial") continue;   // the tab's initial: an icon
            string plain = BenchCards.Plain(text.text).Replace(" / ", " ");
            int words = WordBudget.Words(plain);
            if (words == 0) continue;
            Rect rect = TextRect(text, root);
            if (rect.xMax < 0f || rect.yMax < 0f || rect.xMin > Screen.width || rect.yMin > Screen.height || rect.width <= 0f) continue;
            float scale = root.transform.lossyScale.y > 0f ? text.rectTransform.lossyScale.y / root.transform.lossyScale.y : 1f;
            list.Add(new Shown
            {
                text = plain, words = words, rect = rect, size = text.fontSize * scale, path = PathOf(text.transform),
                hover = text.GetComponentInParent<HoverTooltipUI>() != null,
                overDevice = text.GetComponentInParent<HoverTooltipUI>() != null || text.GetComponentInParent<Juice>() != null,
            });
        }
        return list;
    }

    static float GroupAlpha(Transform t)
    {
        float alpha = 1f;
        for (Transform p = t; p != null; p = p.parent)
        {
            CanvasGroup g = p.GetComponent<CanvasGroup>();
            if (g == null || !g.enabled) continue;
            alpha *= g.alpha;
            if (g.ignoreParentGroups) break;
        }
        return alpha;
    }

    // The text's own drawn words on the screen (its text bounds, not its box), in pixels from the bottom left.
    static Rect TextRect(TMP_Text text, Canvas root)
    {
        Bounds b = text.textBounds;
        RectTransform r = text.rectTransform;
        Vector3 a = r.TransformPoint(b.min), c = r.TransformPoint(b.max);
        if (b.size.sqrMagnitude < 1e-6f)
        {
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            a = corners[0];
            c = corners[2];
        }
        if (root.renderMode == RenderMode.ScreenSpaceCamera && root.worldCamera != null)
        {
            a = RectTransformUtility.WorldToScreenPoint(root.worldCamera, a);
            c = RectTransformUtility.WorldToScreenPoint(root.worldCamera, c);
        }
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    // ------------------------------------------------------------------ what the words may not cover

    IEnumerator Overlaps(string state, JobBase job)
    {
        foreach (float turn in new[] { 0f, 90f, 180f, -90f })
        {
            inspector.LayDown(turn);
            yield return Frames(4);
            OverlapCheck($"{state}, turned to {turn:0}°", job, true);
        }
        inspector.LayDown(0f);
        yield return Frames(2);
        if (inspector.FlipOver())
        {
            yield return Await(() => !inspector.Flipping, 2f);
            yield return Seconds(.3f);
            OverlapCheck($"{state}, turned over", job, true);
            inspector.FlipOver();
            yield return Await(() => !inspector.Flipping, 2f);
        }
        inspector.LayDown(0f);
        yield return Frames(3);
    }

    void OverlapCheck(string when, JobBase job, bool zones)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var ui = new List<(string what, Rect rect)>();
        foreach (Shown s in Count()) if (!s.overDevice) ui.Add(("\"" + Short(s.text) + "\"", s.rect));
        if (BenchCards.Showing) ui.Add(("the job card", BenchCards.CardOnScreen));
        if (BenchCards.ControlsShowing) ui.Add(("the controls card", BenchCards.ControlsOnScreen));
        if (BenchCards.HintShowing) ui.Add(("the help hint", BenchCards.HintOnScreen));
        if (guide != null && guide.CardShowing) ui.Add(("the guide's card", guide.CardOnScreen));

        Rect device = DeviceBox(job, cam);
        var onDevice = ui.Where(u => u.rect.Overlaps(device)).Select(u => u.what).ToList();
        Check(onDevice.Count == 0, $"{when}: nothing over the device (it {Px(device)})" + (onDevice.Count > 0 ? ": " + string.Join(", ", onDevice) : ""));

        var covered = new List<string>();
        foreach ((string what, Vector3 at) in StagePoints())
        {
            Vector3 p = cam.WorldToScreenPoint(at);
            if (p.z <= 0f) continue;
            foreach (var u in ui)
                if (Grown(u.rect, 8f).Contains(new Vector2(p.x, p.y))) { covered.Add($"{what} under {u.what}"); break; }
        }
        foreach (ToolPickup tool in FindObjectsByType<ToolPickup>(FindObjectsSortMode.None))
        {
            if (tool == null || tool.InHand || !tool.isActiveAndEnabled || !DeviceBounds.TryGet(tool.gameObject, out Bounds b)) continue;
            Rect r = Project(b, cam);
            foreach (var u in ui)
                if (u.rect.Overlaps(r)) { covered.Add($"the {tool.displayName} under {u.what}"); break; }
        }
        Check(covered.Count == 0, $"{when}: the mat's compartments and the tools in their stand are clear of the words" +
                                  (covered.Count > 0 ? ": " + string.Join("; ", covered) : ""));
        if (!zones) return;
        if (BenchCards.Showing) InZone(BenchCards.CardOnScreen, BenchStageContract.Zone.U3, when + ": the job card");
        if (BenchCards.ControlsShowing && !BenchCards.ControlsHeld && !CafeLab.Active) InZone(BenchCards.ControlsOnScreen, BenchStageContract.Zone.U5, when + ": the controls card");
        if (BenchCards.HintShowing && !CafeLab.Active) InZone(BenchCards.HintOnScreen, BenchStageContract.Zone.U5, when + ": the help hint");
    }

    void InZone(Rect rect, BenchStageContract.Zone zone, string what)
    {
        Rect z = BenchStageContract.Rect(zone, Screen.width, Screen.height);
        bool inside = rect.xMin >= z.xMin - 2f && rect.xMax <= z.xMax + 2f && rect.yMin >= z.yMin - 2f && rect.yMax <= z.yMax + 2f;
        Check(inside, $"{what} lies in the stage's zone {zone} ({Px(rect)}; the zone {Px(z)})");
    }

    // The mat's compartments and the tool rest's seats (BenchStage, the stage contract's fields).
    static IEnumerable<(string, Vector3)> StagePoints()
    {
        BenchStage stage = BenchStage.Instance;
        if (stage == null) yield break;
        if (stage.cups != null) for (int i = 0; i < stage.cups.Length; i++) if (stage.cups[i] != null) yield return ($"cup {i + 1}", stage.cups[i].position);
        foreach ((string name, Transform t) in new[] { ("the small pad", stage.padSmall), ("the large pad", stage.padLarge), ("the small well", stage.wellSmall), ("the large well", stage.wellLarge) })
            if (t != null) yield return (name, t.position);
        if (stage.scrapSlots != null) for (int i = 0; i < stage.scrapSlots.Length; i++) if (stage.scrapSlots[i] != null) yield return ($"scrap slot {i + 1}", stage.scrapSlots[i].position);
        if (stage.toolSlots != null) for (int i = 0; i < stage.toolSlots.Length; i++) if (stage.toolSlots[i] != null) yield return ($"tool seat {i + 1}", stage.toolSlots[i].position);
    }

    static Rect DeviceBox(JobBase job, Camera cam) =>
        job != null && DeviceBounds.TryGet(job.gameObject, out Bounds b) ? Project(b, cam) : default;

    static Rect Project(Bounds b, Camera cam)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 p = cam.WorldToScreenPoint(corner);
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static Rect Grown(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

    // ------------------------------------------------------------------ the lab's devices

    // A bench device as a customer's (its tag shown), with fault `index`, or the first fault whose label has `labelHas`.
    JobBase Make(string path, int index, out string faultLabel, string labelHas = null)
    {
        faultLabel = "";
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        if (prefab == null) { Check(false, "loads " + path); return null; }
        GameObject device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        JobBase job = device.GetComponent<JobBase>();
        if (definition == null || job == null || definition.faults == null || definition.faults.Length == 0) { Check(false, path + " has a definition, a job and faults"); return null; }
        if (labelHas != null)
        {
            index = 0;
            for (int i = 0; i < definition.faults.Length; i++)
                if (definition.faults[i] != null && definition.faults[i].description.Contains(labelHas)) { index = i; break; }
        }
        if (index < 0 || index >= definition.faults.Length) { Note($"{Path.GetFileNameWithoutExtension(path)} has no fault {index}: its fault 0 instead"); index = 0; }
        DeviceFault fault = definition.GetFault(index);
        definition.ApplyFault(index);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = index,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 1, color = JobColour
        });
        JobMarker marker = device.GetComponentInChildren<JobMarker>(true);
        if (marker != null) marker.Show(1, JobColour);
        faultLabel = fault.description;
        return job;
    }

    IEnumerator OpenOnBench(JobBase job)
    {
        carry.TryPickUp(job);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.8f);
        bench.Interact(interactor);
        yield return Seconds(.5f);
        if (!inspector.BeginInspection(job)) { Check(false, $"the close-up opens on the {job.name}"); yield break; }
        yield return AfterBlend();
        yield return Seconds(1f);
    }

    IEnumerator Close(JobBase job)
    {
        if (inspector.IsHoldingItem) inspector.CancelInspection();
        yield return AfterBlend();
        if (job != null)
        {
            foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None)) spot.Release(job);
            made.Remove(job.gameObject);
            Destroy(job.gameObject);
        }
        BenchCards.LabControls = null;
        yield return Frames(3);
    }

    // ------------------------------------------------------------------ helpers

    void SetPad(bool pad) => setPad?.Invoke(null, new object[] { pad });

    static CustomerBrain FindRegular(string id)
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsSortMode.None))
            if (brain != null && brain.Identity != null && brain.Identity.Profile != null && brain.Identity.Profile.PersistentId == id)
                return brain;
        return null;
    }

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        view.LookTo(yaw, 10f);
    }

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && path.Length < 90; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    static string Px(Rect r) => $"x {r.xMin:0}-{r.xMax:0}, y {r.yMin:0}-{r.yMax:0} px";

    protected override void Restore()
    {
        BenchCards.LabControls = null;
        setPad?.Invoke(null, new object[] { padWas });
        if (Application.isFocused && Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        foreach (GameObject g in made)
            if (g != null)
            {
                JobBase j = g.GetComponent<JobBase>();
                if (j != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None)) spot.Release(j);
                Destroy(g);
            }
        if (spawnerTouched && spawner != null) spawner.enabled = spawnerWas;
    }
}
