using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ---------------------------------------------------------------------------
// THE BENCH, V2: A PLAY CHECK (playtest 3, session 4, 7 Oct 2026; claude/bench-spec-v2.md §5)
//
// Fixit Fidget > Bench > Bench 3 - The bench - play check (lab, drives itself). A fresh Day 1 in the lab, nobody in the
// café, a phone with a cracked screen put on the bench by the check's own hand, and then a virtual gamepad does the
// whole repair the way a player would, checking each beat of the spec as it goes:
//
//   the device is presented face-on and a nudge of the stick settles back onto the face; Y flips it over; D-pad up and
//   down zoom; the driver held on a screw backs it out along ITS OWN axis, keeps its place if let go early, and when it
//   comes free it falls and lands on the mat or in the tray, leaving a marked hole; the pry held at the cover's edge pops
//   it; a press on the popped cover picks it up, it follows the cursor and drops where it's let go; the tweezers held
//   on the broken part pinch it out and it's dropped in the tray, where the magnet settles it; the fresh part is pinched
//   out of the tray and seats when let go over the empty seat (Perfect, but not finished); the cover dragged home snaps
//   on; the driver held on each empty hole fetches its screw and screws it home: finished. The clock ran throughout;
//   the catch sets a piece that leaves the bench back over the tray; every bench cue has a placeholder sound.
//
// Photos and report.txt go to Logs/Bench/bench-check-<time>/. Nothing is saved in the scene. The Game view must have
// focus (the pad's cursor needs it), as for every lab check.
// ---------------------------------------------------------------------------
public sealed class BenchLab : PlayLab
{
    public const string PendingKey = "FixitFidget.Bench.Check";

    protected override string Title => "The bench - play check";
    protected override string Tag => "[Bench check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Bench check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Bench check (this Play session only)");
        go.AddComponent<BenchLab>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Bench",
            $"bench-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    CustomerSpawner spawner;
    bool spawnerWas = true;
    JobBase job;
    GameObject device;
    Camera cam;
    float frontSign = 1f;     // +1: the device's local up is the face first shown; -1: its local down is
    readonly List<GameObject> made = new List<GameObject>();

    static readonly GamepadState RT = new GamepadState { rightTrigger = 1f };

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
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null, "Ace is in the scene");
        if (interactor == null || view == null || carry == null || inspector == null) yield break;
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);

        BenchStage stage = BenchStage.Instance;
        Check(stage != null && stage.Mat != null && stage.Tray != null, "the bench stage is in the scene (Fixit Fidget > Bench > Bench 2 builds it)");
        if (stage == null || stage.Mat == null || stage.Tray == null) yield break;
        StationInteractable bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(bench != null, "the repair bench has a stand point");
        if (bench == null) yield break;

        // ---------- the device: a phone with a cracked screen, by the check's hand ----------
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/PhoneRepair.prefab");
#endif
        Check(prefab != null, "the phone prefab loads");
        if (prefab == null) yield break;
        device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = "Bench check phone";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        DeviceFault fault = definition != null ? definition.GetFault(0) : null;
        Check(definition != null && job != null && fault != null, "it has a definition, a job and a first fault");
        if (definition == null || job == null || fault == null) yield break;
        definition.ApplyFault(0);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = 0,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 99, color = Color.white
        });
        yield return Frames(2);
        ScrewTarget[] screws = job.GetComponentsInChildren<ScrewTarget>();
        RemovablePart cover = job.GetComponentInChildren<RemovablePart>();
        ReplaceablePart part = job.GetComponentInChildren<ReplaceablePart>();
        Check(screws.Length == 2 && cover != null && part != null,
            $"the fault is the one the check knows ({fault.description}: {screws.Length} screws, {(cover != null ? "a cover" : "no cover")}, {(part != null ? "a part to replace" : "no part")})");
        if (screws.Length < 1 || cover == null || part == null) yield break;
        Check(carry.TryPickUp(job), "Ace takes it");
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");

        // ---------- at the bench, the pad in hand ----------
        PlugInPad("Bench check pad");
        yield return PadPress(GamepadButton.LeftShoulder);     // a touch of the pad: the game takes it as the one in hand
        yield return Frames(2);
        Check(PadInput.UsingPad, "the pad is the one in hand");
        view.SetFirstPerson(true);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return AfterBlend();
        float clockBefore = DayClock.Instance.SecondsIntoDay;
        Quaternion benchPose = device.transform.rotation;
        Check(inspector.BeginInspection(job), "the close-up opens on it");
        if (!inspector.IsHoldingItem) yield break;
        yield return AfterBlend();
        yield return Frames(6);
        cam = Camera.main;
        Check(PadCursor.IsActive, "the pad's cursor is on screen (the Game view must have focus)");
        if (!PadCursor.IsActive) yield break;

        // The fresh part was put in the tray when the close-up opened: where is it a moment later?
        LoosePart freshAtStart = part.Fresh;
        yield return Seconds(.8f);
        Check(freshAtStart != null && stage.InTray(freshAtStart.transform.position),
            $"the fresh part is in the tray from the start ({(freshAtStart != null ? Where(freshAtStart.transform.position) + $", {freshAtStart.transform.position - stage.Tray.position}, body {(freshAtStart.GetComponent<Rigidbody>() != null ? (freshAtStart.GetComponent<Rigidbody>().isKinematic ? "kinematic" : "free, " + freshAtStart.GetComponent<Rigidbody>().linearVelocity.magnitude.ToString("0.00") + " m/s") : "none")}, collider {(freshAtStart.GetComponent<Collider>() != null ? freshAtStart.GetComponent<Collider>().bounds.size.ToString("0.000") : "none")}" : "no fresh part")})");

        // Face-on: a phone lying flat on the bench is turned to show the camera a face, not an edge.
        frontSign = Vector3.Dot(device.transform.up, -cam.transform.forward) >= 0f ? 1f : -1f;
        float faceOff = Vector3.Angle(Face(), -cam.transform.forward);
        Check(faceOff < 1f, $"it's presented face-on ({faceOff:0.0}° off square)");
        yield return Photo("01-face-on");

        // ---------- the zoom ----------
        float d0 = Distance();
        PadHold(new GamepadState().WithButton(GamepadButton.DpadUp));
        yield return Seconds(.5f);
        yield return LetGo();
        yield return Seconds(.5f);
        float dIn = Distance();
        Check(inspector.Zoom > .3f && dIn < d0 * .85f, $"D-pad up zooms in ({d0 * 100f:0} cm to {dIn * 100f:0} cm)");
        PadHold(new GamepadState().WithButton(GamepadButton.DpadDown));
        yield return Seconds(.5f);
        yield return LetGo();
        yield return Seconds(.5f);
        float dOut = Distance();
        Check(dOut > dIn * 1.1f, $"D-pad down zooms out again ({dOut * 100f:0} cm)");

        // ---------- the spin: a nudge settles back onto the face; a swing is left where it stops ----------
        Quaternion face0 = device.transform.rotation;
        PadHold(new GamepadState { leftStick = new Vector2(.5f, 0f) });
        yield return Seconds(.3f);
        float nudged = Quaternion.Angle(face0, device.transform.rotation);
        yield return LetGo();
        yield return Seconds(1.6f);
        float after = Quaternion.Angle(face0, device.transform.rotation);
        Check(nudged > 3f && nudged < 20f && after < .5f, $"a nudge of the left stick turns it ({nudged:0}°) and, let go, it coasts and settles back onto the face ({after:0.0}° off)");
        PadHold(new GamepadState { leftStick = new Vector2(1f, 0f) });
        yield return Seconds(.4f);
        float swung = Quaternion.Angle(face0, device.transform.rotation);
        yield return LetGo();
        yield return Seconds(1.6f);
        float stayed = Quaternion.Angle(face0, device.transform.rotation);
        Check(swung > 40f && swung < 100f && stayed > swung + 3f && stayed < 135f,
            $"a swing turns it well past the face ({swung:0}°), it coasts on ({stayed:0}°) and stays where it stops (no face near enough to settle on)");
        device.transform.rotation = face0;          // the check's hand puts it square again
        yield return Frames(2);

        // ---------- the flip: the back, where the screws are ----------
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.8f);
        Check(Vector3.Dot(Face(), -cam.transform.forward) < -.99f, "Y flips it over: the back faces the camera");
        yield return Photo("02-the-back");

        // ---------- the screwdriver, and the screws ----------
        yield return SelectTool(ToolType.Screwdriver);
        ToolPickup driver = ToolInHand(ToolType.Screwdriver);
        PadCursor.GlideTo(Px(device.transform.position));
        yield return Seconds(.5f);
        Check(driver != null && driver.InHand && Vector3.Distance(driver.transform.position, device.transform.position) < .12f
              && Vector3.Distance(driver.transform.position, stage.ToolSlot(ToolType.Screwdriver).position) > .05f,
            "the driver leaves the caddy and rides with the cursor over the device");

        ScrewTarget first = screws.FirstOrDefault(s => s.CanInteract && OnPx(s.WorkPoint));
        Check(first != null, "a screw is in view on the back");
        if (first == null) yield break;
        Screw s0 = first.GetComponent<Screw>();
        yield return Hover(first.WorkPoint, "Screw", "Hold to unscrew", "the first screw");
        PadHold(RT);
        yield return Seconds(.3f);
        yield return LetGo();
        float backedOut = Vector3.Dot(s0.transform.position - s0.HomePosition, s0.Axis);
        float away = Vector3.ProjectOnPlane(s0.transform.position - s0.HomePosition, s0.Axis).magnitude;
        Check(!s0.IsOut && s0.Progress > .1f && s0.Progress < .9f && backedOut > .0008f && away < .0005f,
            $"a short hold backs it part way out along its own axis ({s0.Progress:P0}, {backedOut * 1000f:0.0} mm out, {away * 1000f:0.00} mm sideways) and it keeps its place when let go");
        yield return Await(() => inspector.HoverAction == "Hold to unscrew (part way out)", .6f);
        Check(lastWait, "the prompt says it's part way out");
        yield return Photo("03-part-way-out");
        PadHold(RT);
        yield return Until(() => s0.IsOut, 3f, "held on, it comes free");
        yield return LetGo();
        yield return Seconds(.5f);
        Rigidbody rb0 = s0.GetComponent<Rigidbody>();
        yield return Until(() => rb0 != null && s0.IsLoose && rb0.linearVelocity.magnitude < .01f
                                 && Vector3.Distance(s0.transform.position, s0.HomePosition) > .02f, 6f, "free, it falls and comes to rest");
        Check(OverMat(s0.transform.position) || stage.InTray(s0.transform.position), $"it lies on the mat or in the tray ({Where(s0.transform.position)})");
        Check(s0.Socket != null && s0.Socket.gameObject.activeSelf && s0.Socket.CanInteract, "its empty hole is marked and can be held");
        Check(job.HasDetachedComponent<Screw>() && !job.CanHandBack, "with a screw out the device can't be handed back");

        foreach (ScrewTarget other in screws.Where(s => s != first))
        {
            Screw so = other.GetComponent<Screw>();
            yield return Hover(other.WorkPoint, "Screw", "Hold to unscrew", "the next screw");
            PadHold(RT);
            yield return Until(() => so.IsOut, 3f, "held on, it comes free too");
            yield return LetGo();
            yield return Seconds(.5f);
            Rigidbody rbo = so.GetComponent<Rigidbody>();
            yield return Await(() => rbo != null && so.IsLoose && rbo.linearVelocity.magnitude < .01f, 6f);
            Check(lastWait && (OverMat(so.transform.position) || stage.InTray(so.transform.position)), $"it lands too ({Where(so.transform.position)})");
        }
        yield return Photo("04-screws-out");

        // ---------- the pry, and the cover ----------
        yield return SelectTool(ToolType.Pry);
        Vector3 edge = BenchInteractable.BoundsCentre(cover.transform) + cover.transform.forward * .05f;   // the end away from the screws
        yield return Hover(edge, cover.DisplayName, "Hold to pry it up", "the cover's far edge");
        Quaternion coverRot0 = cover.transform.rotation;
        PadHold(RT);
        yield return Until(() => cover.IsPopped, 2.5f, "held at the edge, the cover creaks and pops");
        yield return LetGo();
        yield return Seconds(.25f);
        float popped = Quaternion.Angle(coverRot0, cover.transform.rotation);
        Check(popped > 4f && popped < 20f, $"it sits up {popped:0.0}° at the pried edge");
        yield return Photo("05-popped");

        yield return Hover(BenchInteractable.BoundsCentre(cover.transform), cover.DisplayName, "Lift it off (drag)", "the popped cover");
        PadHold(RT);
        yield return Frames(4);
        Check(inspector.GrabTarget == cover && cover.State == RemovablePart.CoverState.Held, "a press on the popped cover picks it up");
        Vector3 overTheMat = stage.Mat.position + Vector3.up * .10f - cam.transform.right * .14f;
        PadCursor.GlideTo(Px(overTheMat));
        yield return Seconds(.8f);
        Check(Vector3.Distance(cover.transform.position, device.transform.position) > .08f,
            $"it follows the cursor away from the device ({Vector3.Distance(cover.transform.position, device.transform.position) * 100f:0} cm)");
        yield return LetGo();
        yield return Until(() => cover.State == RemovablePart.CoverState.Loose, 1f, "let go, it drops");
        yield return Seconds(.6f);
        Rigidbody crb = cover.GetComponent<Rigidbody>();
        yield return Until(() => crb != null && !crb.isKinematic && crb.linearVelocity.magnitude < .01f, 5f, "it falls and settles");
        Check(OverMat(cover.transform.position) || stage.InTray(cover.transform.position), $"it lies on the mat ({Where(cover.transform.position)})");
        Check(cover.IsRemoved && job.HasDetachedComponent<RemovablePart>(), "the device is open");
        yield return Photo("06-cover-on-the-mat");

        // ---------- the front, the tweezers, the broken part ----------
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.8f);
        Check(Vector3.Dot(Face(), -cam.transform.forward) > .99f, "Y flips it back to the front");
        yield return SelectTool(ToolType.Tweezers);
        yield return Hover(BenchInteractable.BoundsCentre(part.transform), part.DisplayName, "Hold to pinch it out", "the broken part");
        PadHold(RT);
        yield return Until(() => part.State == ReplaceablePart.PartState.HeldBroken, 1.5f, "held, the tweezers pinch and it lifts out");
        // Over the tray's near end (the fresh part lies in its middle), at the depth the held part rides at, so that, let go, it falls in.
        Vector3 overTheTray = Above(stage.Tray.position - stage.TrayLongAxis * .055f, part.transform.position);
        PadCursor.GlideTo(Px(overTheTray));
        yield return Seconds(.8f);
        yield return LetGo();
        yield return Until(() => part.State == ReplaceablePart.PartState.Removed, 1f, "let go over the tray, it drops");
        Rigidbody prb = part.GetComponent<Rigidbody>();
        yield return Until(() => prb != null && stage.SettledInTray().Contains(prb), 5f, "the tray's magnet draws it down and settles it (a tink)");
        Check(job.Quality < .01f && !job.CanHandBack, "the grade doesn't move for taking the broken part out, and with a hole in it the device can't be handed back");
        yield return Photo("07-broken-part-in-the-tray");

        // ---------- the fresh part, from the tray to the seat ----------
        LoosePart fresh = part.Fresh;
        Check(fresh != null && job.LooseParts.Contains(fresh.gameObject) && stage.InTray(fresh.transform.position),
            $"the fresh part has been waiting in the tray since the close-up opened ({(fresh != null ? Where(fresh.transform.position) : "no fresh part")})");
        if (fresh == null) yield break;
        yield return Hover(BenchInteractable.BoundsCentre(fresh.transform), fresh.DisplayName, "Hold to pinch it up, carry it to the seat", "the fresh part");
        PadHold(RT);
        yield return Seconds(.4f);
        Check(inspector.HoldTarget == fresh && fresh.HoldProgress >= 1f, "the tweezers pinch it up");
        PadCursor.GlideTo(Px(part.SeatPosition));
        yield return Seconds(.9f);
        float overSeat = Vector3.Distance(ReplaceablePart.AtDepthOf(fresh.transform.position, part.SeatPosition, cam), part.SeatPosition);
        yield return LetGo();
        yield return Until(() => part.IsReplaced, 1f, $"let go over the empty seat ({overSeat * 1000f:0} mm off it as seen), it seats");
        Check(job.Quality >= .999f && job.Grade == JobGrade.Perfect, $"the screen is replaced: {job.Grade}");
        Check(!job.IsComplete && !job.CanHandBack, "but it isn't finished: the cover is on the mat and the screws are out");
        yield return Photo("08-fresh-part-in");

        // ---------- the cover home ----------
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.8f);
        yield return Hover(BenchInteractable.BoundsCentre(cover.transform), cover.DisplayName, null, "the cover on the mat");
        PadHold(RT);
        yield return Frames(4);
        Check(inspector.GrabTarget == cover && cover.State == RemovablePart.CoverState.Held, "a press on the loose cover picks it up (any tool)");
        PadCursor.GlideTo(Px(cover.HomePosition));
        yield return Seconds(.9f);
        yield return LetGo();
        yield return Until(() => cover.State == RemovablePart.CoverState.Seated, 1.5f, "let go over its seat, it snaps home");
        Check(!job.HasDetachedComponent<RemovablePart>(), "the cover is counted as back on");
        yield return Photo("09-cover-home");

        // ---------- the screws home ----------
        yield return SelectTool(ToolType.Screwdriver);
        foreach (ScrewTarget t in screws)
        {
            Screw s = t.GetComponent<Screw>();
            if (!s.IsOut || s.Socket == null) continue;
            yield return Hover(s.Socket.WorkPoint, "Screw hole", null, "an empty hole");
            PadHold(RT);
            yield return Until(() => s.Fetched || !s.IsOut, 2f, "held, the driver fetches the screw to its hole");
            yield return Until(() => !s.IsOut, 3f, "and screws it home");
            yield return LetGo();
            Check(Vector3.Distance(s.transform.position, s.HomePosition) < .001f && !s.Socket.gameObject.activeSelf, "it sits in its hole, the hole's mark gone");
        }
        Check(job.IsComplete && job.CanHandBack && job.Grade == JobGrade.Perfect, $"the repair is finished: {job.Grade}, in one piece");
        yield return Seconds(.6f);
        yield return Photo("10-fixed");

        // ---------- the clock, the catch, the sounds ----------
        Check(DayClock.Instance.SecondsIntoDay > clockBefore + 5f, $"the clock ran the whole time ({DayClock.Instance.SecondsIntoDay - clockBefore:0} s of the day)");
        var stray = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stray.name = "Bench check stray";
        stray.transform.localScale = Vector3.one * .006f;
        made.Add(stray);
        Rigidbody strayBody = stray.AddComponent<Rigidbody>();
        strayBody.mass = .003f;
        stage.Watch(strayBody, "screw.drop");
        strayBody.position = stage.Mat.position - Vector3.up * 1f;
        stray.transform.position = strayBody.position;
        yield return Until(() => strayBody.position.y > stage.Mat.position.y - .05f, 1f, "a piece that gets away under the bench is caught and set back over the tray");
        yield return Until(() => stage.SettledInTray().Contains(strayBody), 5f, "where the magnet settles it");
        string[] cues = { "screw.turn", "screw.free", "screw.drop", "screw.bounce", "screw.fetch", "screw.seat", "cover.creak", "cover.pop", "cover.lift",
                          "cover.clack", "part.on", "part.pinch", "part.lift", "part.drop", "part.replace", "tool.pick", "tool.down", "device.snap", "device.flip" };
        string[] silent = cues.Where(c => !PlaceholderSounds.Has(c)).ToArray();
        Check(silent.Length == 0, "every bench cue has a placeholder sound" + (silent.Length > 0 ? " (missing: " + string.Join(", ", silent) + ")" : ""));
        Check(Sfx.Play("screw.drop", stage.Tray.position), "a bench cue plays (the placeholder, until the bank has a file for it)");

        // ---------- out ----------
        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem && Quaternion.Angle(device.transform.rotation, benchPose) < 1f, "stepping back puts the device down as it lay");
    }

    // ---------- the hands ----------

    IEnumerator SelectTool(ToolType tool)
    {
        for (int i = 0; i < 7 && inspector.CurrentTool != tool; i++)
        {
            yield return PadPress(GamepadButton.RightShoulder);
            yield return Frames(2);
        }
        Check(inspector.CurrentTool == tool, $"RB cycles to the {Name(tool)} (in hand: {inspector.CurrentToolName})");
    }

    static string Name(ToolType tool) => tool switch
    {
        ToolType.Screwdriver => "screwdriver", ToolType.Pry => "pry tool", ToolType.Tweezers => "tweezers",
        ToolType.Brush => "brush", ToolType.Cloth => "cloth", _ => "bare hands"
    };

    static ToolPickup ToolInHand(ToolType tool) =>
        FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.tool == tool && t.InHand);

    // The cursor glided onto a point of the device; what the hover then says.
    IEnumerator Hover(Vector3 world, string name, string action, string what)
    {
        PadCursor.GlideTo(Px(world));
        yield return Frames(2);
        yield return Await(() => !PadCursor.Gliding && inspector.HoverName == name && (action == null || inspector.HoverAction == action), 1.5f);
        Check(lastWait, $"{what}: the cursor over it reads \"{inspector.HoverName}: {inspector.HoverAction}\"" + (lastWait ? "" : $" (wanted \"{name}: {action ?? "..."}\")"));
    }

    Vector2 Px(Vector3 world) => cam.WorldToScreenPoint(world);

    // The button let go, and a frame or two for the game to see it before anything else is pressed (a release and a press
    // queued in one frame read as one press on whatever the cursor was still over: the first run unscrewed a screw it had
    // just driven home).
    IEnumerator LetGo()
    {
        PadRelease();
        yield return Frames(2);
    }

    bool OnPx(Vector3 world)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        return p.z > 0f && p.x > 0f && p.y > 0f && p.x < UnityEngine.Screen.width && p.y < UnityEngine.Screen.height;
    }

    Vector3 Face() => device.transform.up * frontSign;

    // A point straight above `target`, at the camera depth a held thing rides at, so that the thing, let go there, falls onto
    // the target (a held part keeps the depth it was picked up at; the cursor over the tray would leave it short of it).
    Vector3 Above(Vector3 target, Vector3 held)
    {
        Vector3 forward = cam.transform.forward;
        float depthHeld = Vector3.Dot(held - cam.transform.position, forward);
        float depthTarget = Vector3.Dot(target - cam.transform.position, forward);
        float dy = Vector3.Dot(Vector3.up, forward);
        float h = Mathf.Abs(dy) > .05f ? (depthHeld - depthTarget) / dy : .08f;
        return target + Vector3.up * Mathf.Clamp(h, .03f, .25f);
    }

    float Distance() => Vector3.Distance(cam.transform.position, device.transform.position);

    bool OverMat(Vector3 p)
    {
        Transform mat = BenchStage.Instance.Mat;
        var box = mat.GetComponent<BoxCollider>();
        Bounds b = box != null ? box.bounds : new Bounds(mat.position, new Vector3(.6f, .01f, .36f));
        float top = b.max.y;
        return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y > top - .005f && p.y < top + .04f;
    }

    string Where(Vector3 p)
    {
        if (BenchStage.Instance.InTray(p)) return "in the tray";
        if (OverMat(p)) return $"on the mat, {(p - BenchStage.Instance.Mat.position).x * 100f:0} cm across, {(p - BenchStage.Instance.Mat.position).z * 100f:0} cm back";
        return $"elsewhere: {p - BenchStage.Instance.Mat.position}";
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

    protected override void Restore()
    {
        PadRelease();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (job != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        foreach (GameObject g in made) if (g != null) Destroy(g);      // the job's loose parts go with it (JobBase.OnDestroy)
        if (spawner != null) spawner.enabled = spawnerWas;
    }
}
