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
// AIM HELP ON A PAD: A PLAY CHECK (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.3 and §2.5)
//
// Fixit Fidget > Playtest > Aim help - play check (lab, drives itself). Mansoor: "very hard to get the controller
// cursor onto an item". A fresh Day 1 in the lab, a virtual gamepad, and in first person:
//
//   1. at the dispenser: the view turned a few degrees off a cup pad (three ways: left, right, below); the right stick
//      pushed toward it, the way a thumb does, until the crosshair is near; let go: the crosshair settles on the pad
//      within half a second, and it's what A would use. Over the pad the view turns slower than in the open (friction);
//      D-pad right steps to the next control to the right;
//   2. behind the counter, a customer waiting: the same from three offsets;
//   3. at the bench, their device in the close-up: the pad's cursor let go 30 to 50 px from a screw settles on it, and
//      D-pad right steps through the parts in the order the work goes (screws first).
//
// The mouse is never helped, and with aim help switched off nothing settles (checked last). Photos and report.txt go
// to Logs/AimHelp/aim-help-<time>/. Nothing is saved in the scene.
// ---------------------------------------------------------------------------
public sealed class AimHelpCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.AimHelp.Check";

    protected override string Title => "Aim help on a pad - play check";
    protected override string Tag => "[Aim help check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Aim help check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Aim help check (this Play session only)");
        go.AddComponent<AimHelpCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AimHelp",
            $"aim-help-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    ConversationController conversation;
    CustomerSpawner spawner;
    bool aimWas, spawnerWas = true;

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null && spawner != null,
            "Ace and the café's spawner are in the scene");
        if (interactor == null || view == null || spawner == null || interactor.DrinksStation == null || interactor.CounterStation == null) yield break;

        yield return Await(() => typeof(CustomerSpawner).GetField("openingDrink", Any).GetValue(spawner) != null, 10f);
        spawnerWas = spawner.enabled;
        spawner.enabled = false;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        aimWas = AimAssist.Enabled;
        AimAssist.Enabled = true;
        PlugInPad("Aim help check pad");
        // A touch of the pad: the game now takes it as the one in hand (the help is for a pad only).
        yield return PadPress(GamepadButton.LeftShoulder);
        yield return Frames(3);
        Check(PadInput.UsingPad && AimAssist.Active, "the pad is the one in hand and aim help is on");
        view.SetFirstPerson(true);
        yield return AfterBlend();

        // ---------- 1. the dispenser ----------
        StationInteractable drinks = interactor.DrinksStation;
        BeverageControl pad1 = drinks.GetComponentsInChildren<BeverageControl>()
            .Where(c => !c.dispenseButton).OrderBy(c => Mathf.Abs(c.transform.GetComponentInChildren<Collider>().bounds.center.x - drinks.transform.position.x)).FirstOrDefault();
        Check(pad1 != null, "the dispenser has cup pads");
        if (pad1 == null) yield break;
        PutAce(drinks.StandPoint.position, drinks.StandPoint.eulerAngles.y);
        yield return Frames(4);
        Vector3 padPoint = pad1.GetComponentInChildren<Collider>().bounds.center;
        yield return Settles(pad1, padPoint, new Vector2(-5f, 0f), "a cup pad, from 5° to its left");
        yield return Settles(pad1, padPoint, new Vector2(6f, 0f), "a cup pad, from 6° to its right");
        yield return Settles(pad1, padPoint, new Vector2(0f, 5f), "a cup pad, from 5° below");
        yield return Photo("01-settled-on-a-cup-pad");
        yield return Friction(padPoint);

        // D-pad right: the next control to the right.
        Look(padPoint, Vector2.zero);
        yield return Frames(4);
        Vector3 right = Camera.main.transform.right;
        Interactable was = interactor.Focused;
        yield return PadPress(GamepadButton.DpadRight);
        yield return Seconds(.4f);
        Interactable now = interactor.Focused;
        bool stepped = now != null && now != was && PlayerInteractor.IsDrinksThing(now)
                       && Vector3.Dot(Middle(now) - padPoint, right) > 0f;
        Check(stepped, $"D-pad right steps to the next control to the right ({Name(was)} to {Name(now)})");
        yield return Photo("02-dpad-stepped");

        // ---------- 2. a customer at the counter ----------
        DayOneOpening opening = (DayOneOpening)typeof(CustomerSpawner).GetField("opening", Any).GetValue(spawner);
        opening.Reset(true);
        opening.TryStartVisit();
        opening.FinishVisit();          // the lesson's second visit: a repair (their device comes in useful at the bench)
        CustomerBrain who = null;
        for (int attempt = 0; attempt < 8 && who == null; attempt++)
        {
            if (attempt > 0) { opening.Reset(true); opening.TryStartVisit(); opening.FinishVisit(); }
            typeof(CustomerSpawner).GetMethod("Spawn", Any).Invoke(spawner, null);
            CustomerBrain c = spawner.OpeningCustomer;
            if (c == null) break;
            if (c.Record != null && c.Record.kind == JobKind.Repair && !c.IsCounterRepair
                && (c.Record.faultType == FaultType.Mechanical || c.Record.faultType == FaultType.Cleaning)) who = c;
            else { Destroy(c.gameObject); opening.FinishVisit(); yield return Frames(2); }
        }
        Check(who != null, $"a customer with a device for the bench comes in ({(who != null ? who.CustomerName : "none")})");
        if (who == null) yield break;
        yield return Until(() => who == null || who.CanHearIntake, 90f, "they reach the counter");
        if (!lastWait || who == null) yield break;
        Transform till = interactor.CounterStation.StandPoint;
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(4);
        Vector3 chest = who.LookTarget != null ? who.LookTarget.position - Vector3.up * .2f : who.transform.position + Vector3.up * 1.4f;
        CustomerInteractable talk = who.GetComponent<CustomerInteractable>();
        yield return Settles(talk, chest, new Vector2(-6f, 0f), "a customer at the counter, from 6° to their left");
        yield return Settles(talk, chest, new Vector2(7f, 0f), "a customer at the counter, from 7° to their right");
        yield return Settles(talk, chest, new Vector2(0f, -5f), "a customer at the counter, from 5° above");
        yield return Photo("03-settled-on-a-customer");

        // ---------- 3. the bench ----------
        // Their request taken (by the check's hand: this check is about aiming), their device to the bench.
        conversation.Begin(who);
        for (int i = 0; i < 12 && conversation.InConversation && who != null && !who.WasAccepted; i++)
        {
            conversation.Advance();
            yield return Seconds(.35f);
        }
        yield return Await(() => !conversation.InConversation, 6f);
        if (conversation.InConversation) conversation.Advance();
        yield return Await(() => !conversation.InConversation, 2f);
        if (conversation.InConversation) conversation.End();
        yield return Until(() => who != null && who.ActiveJob != null, 4f, "their device comes in");
        JobBase job = who != null ? who.ActiveJob : null;
        StationInteractable bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        if (job == null || bench == null) yield break;
        carry.TryPickUp(job);
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Frames(4);
        Check(interactor.WorkOn(job.GetComponentInChildren<ItemInteractable>()), "the close-up opens on it");
        yield return AfterBlend();
        yield return Frames(6);
        Check(PadCursor.IsActive, "the pad's cursor is on screen");
        Camera cam = Camera.main;
        ScrewTarget screw = job.GetComponentsInChildren<ScrewTarget>().FirstOrDefault(s => On(cam, s.transform));
        Check(screw != null, "a screw is in view");
        if (screw != null)
        {
            Vector2 at = cam.WorldToScreenPoint(Centre(screw.transform));
            float scale = Mathf.Max(.5f, Screen.height / 1080f);
            List<Vector2> others = SnapPoints(job, cam).Where(p => (p - at).sqrMagnitude > 4f).ToList();
            yield return CursorSettles(at, others, 30f * scale, "30 px");
            yield return CursorSettles(at, others, 45f * scale, "45 px");
            yield return CursorSettles(at, others, 50f * scale, "50 px");
            yield return Photo("04-cursor-on-a-screw");
        }

        // D-pad right through the parts, in the order the work goes: from nowhere in particular, the first of the work
        // (a screw); then each of them.
        yield return DpadRun(job, cam, "with the screws in");
        // The screws out (the check's hand): the cover comes first now, and the screws (to go back in) after it.
        foreach (ScrewTarget t in job.GetComponentsInChildren<ScrewTarget>())
            if (!t.GetComponent<Screw>().IsOut && t.CanInteract) { t.Activate(); yield return Seconds(.6f); }
        yield return Seconds(.6f);
        yield return DpadRun(job, cam, "with the screws out");
        yield return Photo("05-dpad-on-the-parts");

        // Off: nothing settles.
        AimAssist.Enabled = false;
        if (screw != null)
        {
            ScrewTarget any = job.GetComponentsInChildren<ScrewTarget>(true).FirstOrDefault();
            Vector2 at = any != null ? (Vector2)cam.WorldToScreenPoint(Centre(any.transform)) : new Vector2(Screen.width * .5f, Screen.height * .5f);
            PadCursor.GlideTo(at + new Vector2(40f, 0f) * Mathf.Max(.5f, Screen.height / 1080f));
            yield return Seconds(.35f);
            PadHold(new GamepadState { rightStick = new Vector2(-.3f, 0f) });
            yield return Frames(2);
            PadRelease();
            yield return Seconds(.4f);
            Check(!PadCursor.Gliding && (PadCursor.Position - at).magnitude > 15f, $"with aim help off, the cursor stays where it was let go ({(PadCursor.Position - at).magnitude:0} px from the screw)");
        }
        AimAssist.Enabled = true;
        inspector.CancelInspection();
        yield return AfterBlend();
    }

    // D-pad right from a corner of the screen, once for each part of the work and once more: where the cursor lands each time.
    IEnumerator DpadRun(JobBase job, Camera cam, string when)
    {
        PadCursor.GlideTo(new Vector2(Screen.width * .08f, Screen.height * .9f));
        yield return Seconds(.4f);
        int points = AllParts(job).Count(p => p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform));
        var orders = new List<int>();
        for (int i = 0; i < Mathf.Max(2, points + 1); i++)
        {
            yield return PadPress(GamepadButton.DpadRight);
            yield return Seconds(.3f);
            BenchInteractable under = NearestPart(job, cam, PadCursor.Position, out float px);
            orders.Add(under != null && px < 12f * Mathf.Max(.5f, Screen.height / 1080f) ? ItemInspector.WorkOrder(under) : -1);
        }
        int first = AllParts(job).Where(p => p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform))
            .Select(ItemInspector.WorkOrder).DefaultIfEmpty(-1).Min();
        bool ordered = true;
        for (int i = 1; i < Mathf.Min(orders.Count, points); i++) ordered &= orders[i] >= orders[i - 1];
        Check(orders.Count > 0 && orders[0] == first && ordered && !orders.Contains(-1),
            $"{when}: D-pad right lands on a part each time, the first of the work first, in the work's order (places: {string.Join(", ", orders)}; first {first})");
    }

    static List<BenchInteractable> AllParts(JobBase job)
    {
        var all = new List<BenchInteractable>(job.GetComponentsInChildren<BenchInteractable>());
        foreach (GameObject loose in job.LooseParts) if (loose != null) all.AddRange(loose.GetComponentsInChildren<BenchInteractable>());
        return all;
    }

    // What the cursor may settle on, on screen: the parts that can be worked on, the grime, the tools.
    static IEnumerable<Vector2> SnapPoints(JobBase job, Camera cam)
    {
        foreach (BenchInteractable p in AllParts(job))
            if (p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform)) yield return cam.WorldToScreenPoint(Centre(p.transform));
        foreach (GrimeSpot g in job.GetComponentsInChildren<GrimeSpot>())
            if (On(cam, g.transform)) yield return cam.WorldToScreenPoint(Centre(g.transform));
        foreach (ToolPickup t in FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude))
            if (On(cam, t.transform)) yield return cam.WorldToScreenPoint(Centre(t.transform));
    }

    protected override void Restore()
    {
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        AimAssist.Enabled = aimWas;
        if (spawner != null) spawner.enabled = spawnerWas;
    }

    // ---------- first person: the stick toward a target, let go, settled ----------

    IEnumerator Settles(Interactable thing, Vector3 point, Vector2 offset, string what)
    {
        Look(point, offset);
        yield return Frames(4);
        // Toward it: as a thumb does, until the crosshair is near (or a second and a half).
        Vector2 toward = new Vector2(-offset.x, offset.y).normalized;   // yaw left offset: push right; pitch below (+): push up
        float until = Time.realtimeSinceStartup + 1.5f;
        PadHold(new GamepadState { rightStick = toward * .45f });
        while (Time.realtimeSinceStartup < until && AngleTo(point) > 3f) yield return null;
        float near = AngleTo(point);
        PadRelease();
        float letGo = Time.realtimeSinceStartup;
        yield return Await(() => interactor.Focused == thing && AngleTo(point) < 1f, .5f);
        bool settled = lastWait;
        float took = Time.realtimeSinceStartup - letGo;
        Check(settled, $"{what}: let go {near:0.0}° off, it settles on it in {took:0.00} s ({AngleTo(point):0.00}° off; A would use {Name(interactor.Focused)})");
    }

    // Over a target the view turns slower than in the open.
    // The push is a firm one held for a short time, not a few frames: four frames of a light push turned the view by a few
    // thousandths of a degree at a high frame rate, so both sides read 0.00 (7 Oct). Short enough that the view is still on the
    // target for most of it.
    const float FrictionPush = .6f, FrictionSeconds = .15f;

    IEnumerator Friction(Vector3 point)
    {
        Look(point, Vector2.zero);
        yield return Frames(4);
        float yaw0 = Get<float>(view, "yaw");
        PadHold(new GamepadState { rightStick = new Vector2(FrictionPush, 0f) });
        yield return Seconds(FrictionSeconds);
        PadRelease();
        yield return Frames(2);
        float over = Mathf.Abs(Mathf.DeltaAngle(yaw0, Get<float>(view, "yaw")));
        // The same push looking at the floor in the open.
        view.LookTo(Get<float>(view, "yaw") + 180f, 60f);
        yield return Frames(4);
        yaw0 = Get<float>(view, "yaw");
        PadHold(new GamepadState { rightStick = new Vector2(FrictionPush, 0f) });
        yield return Seconds(FrictionSeconds);
        PadRelease();
        yield return Frames(2);
        float open = Mathf.Abs(Mathf.DeltaAngle(yaw0, Get<float>(view, "yaw")));
        Check(open > .01f && over < open * .75f, $"over a target the same push turns the view less ({over:0.00}° against {open:0.00}° in the open)");
    }

    // ---------- the bench: the cursor let go near a screw ----------

    // From a point `distance` off the target, on a side where the target is the nearest thing to settle on.
    IEnumerator CursorSettles(Vector2 target, List<Vector2> others, float distance, string what)
    {
        Vector2 offset = Vector2.zero;
        bool found = false;
        for (int k = 0; k < 24 && !found; k++)
        {
            float a = k * 15f * Mathf.Deg2Rad;
            Vector2 o = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
            Vector2 p = target + o;
            if (p.x < 5f || p.y < 5f || p.x > Screen.width - 5f || p.y > Screen.height - 5f) continue;
            if (others.All(q => (q - p).magnitude > distance + 6f)) { offset = o; found = true; }
        }
        if (!found)
        {
            Note($"  no side {what} from the screw where it's the nearest thing to settle on: skipped");
            yield break;
        }
        what = $"{what} off ({Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg:0}°)";
        PadCursor.GlideTo(target + offset);
        yield return Seconds(.35f);
        // A small push toward it, then let go.
        Vector2 dir = (-offset).normalized;
        PadHold(new GamepadState { rightStick = dir * .3f });
        yield return Frames(2);
        PadRelease();
        float letGo = Time.realtimeSinceStartup;
        float tolerance = 3f * Mathf.Max(.5f, Screen.height / 1080f);
        yield return Await(() => (PadCursor.Position - target).magnitude <= tolerance, .5f);
        Check(lastWait, $"the cursor let go {what} of a screw settles on it in {Time.realtimeSinceStartup - letGo:0.00} s ({(PadCursor.Position - target).magnitude:0.0} px off)");
    }

    static BenchInteractable NearestPart(JobBase job, Camera cam, Vector2 at, out float px)
    {
        px = float.PositiveInfinity;
        BenchInteractable best = null;
        foreach (BenchInteractable p in AllParts(job))
        {
            if (!p.isActiveAndEnabled) continue;
            Vector3 s = cam.WorldToScreenPoint(Centre(p.transform));
            if (s.z <= 0f) continue;
            float d = (new Vector2(s.x, s.y) - at).magnitude;
            if (d < px) { px = d; best = p; }
        }
        return best;
    }

    // ---------- looking ----------

    Vector3 Eye()
    {
        Vector3 p = movement.transform.position;
        return new Vector3(p.x, p.y - 1f + view.eyeHeight - movement.EyeDrop, p.z);
    }

    // The view turned onto a point, then off it by an offset (degrees: yaw, pitch; pitch positive looks down).
    void Look(Vector3 point, Vector2 offset)
    {
        Vector2 a = AimAssist.Angles(Eye(), point);
        view.LookTo(a.x + offset.x, a.y + offset.y);
        view.Aim.Reset();
    }

    float AngleTo(Vector3 point)
    {
        Vector2 now = new Vector2(Get<float>(view, "yaw"), Get<float>(view, "pitch"));
        Vector3 forward = Quaternion.Euler(now.y, now.x, 0f) * Vector3.forward;
        return Vector3.Angle(forward, point - Eye());
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

    static bool On(Camera cam, Transform t)
    {
        Vector3 s = cam.WorldToScreenPoint(Centre(t));
        return s.z > 0f && s.x > 0f && s.y > 0f && s.x < Screen.width && s.y < Screen.height;
    }

    static Vector3 Centre(Transform t)
    {
        var c = t.GetComponentInChildren<Collider>();
        if (c != null && c.enabled) return c.bounds.center;
        var r = t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }

    static Vector3 Middle(Interactable it) => Centre(it.transform);

    static string Name(Interactable it) => it == null ? "nothing" : it.name;

    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Any).GetValue(target);
}
