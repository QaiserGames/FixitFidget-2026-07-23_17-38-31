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
//   the device lies on the mat, face up, square to the view; a push of the stick sideways turns it exactly one quarter,
//   eased, with a click, and held it steps a quarter at a time (one every 0.45 s); a diagonal push does nothing; a push
//   of the stick up tilts an edge up to peek under and it lies back flat; with the sticks swapped (the setting) the right
//   stick turns and the left moves the hand, and the card says so; Y flips
//   it over on the mat; D-pad up and down zoom; the driver held on a screw backs it out along ITS OWN axis, keeps its place if let go early, and when it
//   comes free it falls and lands on the mat or in the tray, leaving a marked hole; the pry held at the cover's edge pops
//   it; a press on the popped cover picks it up, it follows the cursor and drops where it's let go; the tweezers held
//   on the broken part pinch it out and it's dropped in the tray, where the magnet settles it; the fresh part is pinched
//   out of the tray and seats when let go over the empty seat (Perfect, but not finished); the cover dragged home snaps
//   on; the driver held on each empty hole fetches its screw and screws it home: finished. The clock ran throughout;
//   the catch sets a piece that leaves the bench back over the tray; every bench cue has a placeholder sound.
//
//   Act two (4b, 9 Oct): a second phone, the "gunked up" one, wearing its working screen: the brush held and moved over a
//   spot of grime takes the dirt off only where it passed (a puff of dust, the hover's percentage), the other spot
//   untouched; a raster of strokes brushes it clean (a sparkle, gone at 97%); the smudge wants the cloth ("Needs the
//   cloth" under the brush; the cloth rests folded flat on the mat, clear of the caddy, and in the hand it is on the
//   phone, lying flat on the glass: 9 Oct night), and wiped in circles it squeaks and goes with a gleam.
//
// PLAYTEST 4 (10 Oct; the two playtester agents' notes on this check's own photos), checked as they go:
//   * ONE ENDING: the fresh screen's seat wakes the phone (its new screen lights) with no words and no "Fixed!" (the cover is
//     still on the mat); when the last screw is home the phone, lying on its back, TURNS FACE UP by itself (12 Oct, §10.5:
//     the words had landed on its back) and "Fixed! · Perfect" fires once, with the big sparkle, over its lit screen, on a
//     dark plate that reads on any background (its contrast measured); then a finished phone offers nothing to undo: a screw
//     under the cursor reads "Screw" with no verb, and the driver held on it doesn't turn it. Act two's phone, never opened,
//     gets the same one ending at its last task, the smudge, a beat after it wakes; no "Back together".
//   * The pry tool waits in the cover's seam, at a pry angle, its handle out across the seam, not stood on the middle of
//     the back; held, its handle levers down; popped, its blade stays under the lifted edge.
//   * The cracked screen, under the back cover, says why the back comes off, naming only what the player does ("Held in by
//     the back cover. Take the cover off first": 12 Oct, no cable that no verb touches), and the checklist says to flip the
//     phone over while its first step, the screws, lies on the face turned down.
//   * The cloth in the hand is a pad (about a fifth of the glass covered on screen, where the folded cloth covered two
//     thirds); the moment the smudge comes clean it lifts off the glass, up and back, so the gleam and the waking screen are
//     seen; photographed mid-lift.
//   * Act three: Grace's camera, by the check's hand (it is about the moments, not the verbs): its lens and film path clean,
//     no ending while the shutter is jammed; the new shutter in, the camera wakes and "Fixed! · Perfect" fires once; the
//     strap is still there.
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
    bool swapTouched, swapWas;   // the stick-swap setting, as it was before the check tried it (put back in Restore)

    static readonly GamepadState RT = new GamepadState { rightTrigger = 1f };
    GameObject prefab;
    StationInteractable bench;

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
        bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(bench != null, "the repair bench has a stand point");
        if (bench == null) yield break;

        // ---------- the device: a phone with a cracked screen, by the check's hand ----------
        prefab = null;
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
        // Carried, from above: a photo of what the player sees (8 Oct: "impossible to notice if I'm grabbing an item").
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(1.2f);
        Vector3 carriedScale = device.transform.localScale;
        Check(carriedScale.x > 1.5f && device.transform.position.y > movement.transform.position.y + .3f,
            $"from above the carried phone is drawn bigger and held high ({carriedScale.x:0.0}x, {device.transform.position.y - movement.transform.position.y:0.00} m above Ace's middle)");
        yield return Photo("00-carried-from-above");
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        Check(device.transform.localScale.x < 1.2f, $"put down, it is its real size again ({device.transform.localScale.x:0.00}x)");
        yield return Seconds(.6f);
        yield return Photo("00-placed-from-above");

        // ---------- at the bench, the pad in hand ----------
        PlugInPad("Bench check pad");
        yield return PadPress(GamepadButton.LeftShoulder);     // a touch of the pad: the game takes it as the one in hand
        yield return Frames(2);
        Check(PadInput.UsingPad, "the pad is the one in hand");
        view.SetFirstPerson(true);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return AfterBlend();
        yield return Photo("00-the-bench-in-first-person");
        float clockBefore = DayClock.Instance.SecondsIntoDay;
        Quaternion benchPose = device.transform.rotation;
        // The bench's top, for the floating check: a ray down through the inspect point, past the stage's own pieces.
        float benchTopGuess = BenchTopUnder(stage);
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

        // On the mat: lying flat, a work face up, square to the view, its bottom on the mat's top.
        frontSign = Vector3.Dot(device.transform.up, Vector3.up) >= 0f ? 1f : -1f;
        float faceOff = Vector3.Angle(Face(), Vector3.up);
        float gap = LowestPoint(device.transform) - inspector.TableTop.y;
        Check(faceOff < 1f && Mathf.Abs(gap) < .004f && Mathf.Abs(Mathf.DeltaAngle(inspector.Yaw, Mathf.Round(inspector.Yaw / 90f) * 90f)) < .5f,
            $"it lies flat on the mat, face up and square ({faceOff:0.0}° off flat, {gap * 1000f:0.0} mm off the mat, yaw {inspector.Yaw:0})");
        Check(Mathf.Abs(inspector.TableTop.y - (stage.Mat.GetComponent<Renderer>().bounds.max.y)) < .001f && stage.Mat.GetComponent<Renderer>().bounds.min.y < benchTopGuess + .02f,
            $"the stage sits on the bench top (the mat's bottom {(stage.Mat.GetComponent<Renderer>().bounds.min.y - benchTopGuess) * 100f:0.0} cm above it)");
        yield return Photo("01-on-the-mat");

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

        // ---------- the stick (12 Oct, his 1b): a push turns it one quarter; held, a quarter at a time; a diagonal push does nothing; a push up peeks ----------
        // The game's own layout first (the left stick turns), whatever the player's setting; it goes back at the end.
        swapWas = GameSettings.SwapBenchSticks;
        swapTouched = true;
        GameSettings.SwapBenchSticks = false;
        yield return Frames(2);
        Quaternion face0 = device.transform.rotation;
        float yaw0 = inspector.Yaw;
        int quarters0 = inspector.QuarterTurns, snapsBefore = Sfx.Requested("device.snap");
        float pushedAt = Time.realtimeSinceStartup;
        PadHold(new GamepadState { leftStick = new Vector2(.75f, 0f) });   // a push, let go at once
        yield return Seconds(.1f);
        float early = Mathf.Abs(Mathf.DeltaAngle(yaw0, inspector.Yaw));
        yield return LetGo();
        yield return Await(() => !inspector.Turning, 1f);
        float landedIn = Time.realtimeSinceStartup - pushedAt;
        float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, inspector.Yaw));
        float offSquare = Mathf.Abs(Mathf.DeltaAngle(inspector.Yaw, Mathf.Round(inspector.Yaw / 90f) * 90f));
        Check(inspector.QuarterTurns == quarters0 + 1 && Mathf.Abs(turned - 90f) < 1f && offSquare < 1f && early > 2f && early < 88f
              && Quaternion.Angle(face0, device.transform.rotation) > 89f && LowestPoint(device.transform) - inspector.TableTop.y < .004f,
            $"a push of the stick turns it exactly one quarter, eased ({early:0}° round at 0.1 s; {turned:0.0}° when it lands, {offSquare:0.00}° off square, in {landedIn:0.00} s), still flat on the mat");
        Check(Sfx.Requested("device.snap") > snapsBefore, "...and it lands with a click (device.snap)");
        yield return Photo("01a-turned-a-quarter");

        // Held, it steps a quarter at a time at a calm pace: one at once, then one every 0.45 s (so 1.15 s held is three).
        quarters0 = inspector.QuarterTurns;
        PadHold(new GamepadState { leftStick = new Vector2(-.9f, 0f) });
        yield return Seconds(1.15f);
        int stepped = inspector.QuarterTurns - quarters0;
        yield return LetGo();
        yield return Await(() => !inspector.Turning, 1f);
        offSquare = Mathf.Abs(Mathf.DeltaAngle(inspector.Yaw, Mathf.Round(inspector.Yaw / 90f) * 90f));
        Check(stepped == 3 && offSquare < 1f, $"held for 1.15 s it steps {stepped} quarters (one at once, then one every 0.45 s), and lands square ({offSquare:0.00}° off)");

        // A diagonal push does nothing: no half-turn, no half-peek (the old stick turned and tilted at once).
        yaw0 = inspector.Yaw;
        quarters0 = inspector.QuarterTurns;
        PadHold(new GamepadState { leftStick = new Vector2(.8f, .8f) });
        yield return Seconds(.5f);
        float diagonalTurn = Mathf.Abs(Mathf.DeltaAngle(yaw0, inspector.Yaw)), diagonalTilt = Mathf.Abs(inspector.Tilt);
        yield return LetGo();
        yield return Seconds(.3f);
        Check(inspector.QuarterTurns == quarters0 && diagonalTurn < .5f && diagonalTilt < .5f,
            $"a diagonal push does nothing: {diagonalTurn:0.0}° turned, {diagonalTilt:0.0}° tilted (no half-turn, no half-peek)");

        // A push clearly up lifts an edge to peek under; let go, it lies back flat.
        inspector.LayDown();                        // the check's hand puts it square-on again
        yield return Frames(2);
        face0 = device.transform.rotation;
        PadHold(new GamepadState { leftStick = new Vector2(0f, 1f) });
        yield return Seconds(.5f);
        float peeked = inspector.Tilt;
        float gapWhilePeeking = LowestPoint(device.transform) - inspector.TableTop.y;
        yield return LetGo();
        yield return Seconds(1.2f);
        Check(peeked > 15f && Mathf.Abs(gapWhilePeeking) < .004f && Mathf.Abs(inspector.Tilt) < .5f && Quaternion.Angle(face0, device.transform.rotation) < .5f,
            $"a push of the stick up tilts an edge up to peek under ({peeked:0}°, its low edge still on the mat) and, let go, it lies back flat");

        // ---------- the sticks swapped (GameSettings.SwapBenchSticks, 12 Oct): the right stick turns it, the left moves the hand ----------
        GameSettings.SwapBenchSticks = true;
        yield return Frames(2);
        yaw0 = inspector.Yaw;
        quarters0 = inspector.QuarterTurns;
        Vector2 cursor0 = PadCursor.Position;
        PadHold(new GamepadState { rightStick = new Vector2(.8f, 0f) });
        yield return Seconds(.12f);
        yield return LetGo();
        yield return Await(() => !inspector.Turning, 1f);
        float swappedTurn = Mathf.Abs(Mathf.DeltaAngle(yaw0, inspector.Yaw));
        float handMoved = (PadCursor.Position - cursor0).magnitude;
        Check(inspector.QuarterTurns == quarters0 + 1 && Mathf.Abs(swappedTurn - 90f) < 1f && handMoved < 2f,
            $"sticks swapped: the right stick turns it a quarter ({swappedTurn:0.0}°) and the hand stays where it was ({handMoved:0} px)");
        yaw0 = inspector.Yaw;
        quarters0 = inspector.QuarterTurns;
        cursor0 = PadCursor.Position;
        PadHold(new GamepadState { leftStick = new Vector2(.8f, 0f) });
        yield return Seconds(.3f);
        yield return LetGo();
        yield return Seconds(.3f);
        handMoved = (PadCursor.Position - cursor0).magnitude;
        Check(inspector.QuarterTurns == quarters0 && Mathf.Abs(Mathf.DeltaAngle(yaw0, inspector.Yaw)) < .5f && handMoved > 40f,
            $"...and the left stick moves the hand ({handMoved:0} px) without turning it");
        yield return Seconds(.35f);   // the cards refresh every 0.15 s
        string card = Plain(BenchCards.ControlsText);
        Check(card.Contains("[Left stick]  Move your hand") && card.Contains("[Right stick]  Turn it"),
            $"...and the controls card says so: \"{card}\"");
        // Put back as the player had it (the setting is PlayerPrefs, shared with his own game): the turn stick is whichever the
        // setting says again, and so is the card.
        GameSettings.SwapBenchSticks = swapWas;
        yield return Frames(2);
        quarters0 = inspector.QuarterTurns;
        PadHold(swapWas ? new GamepadState { rightStick = new Vector2(-.8f, 0f) } : new GamepadState { leftStick = new Vector2(-.8f, 0f) });
        yield return Seconds(.12f);
        yield return LetGo();
        yield return Await(() => !inspector.Turning, 1f);
        yield return Seconds(.35f);
        card = Plain(BenchCards.ControlsText);
        string handStick = swapWas ? "Left" : "Right", turnStick = swapWas ? "Right" : "Left";
        Check(inspector.QuarterTurns == quarters0 + 1 && card.Contains($"[{handStick} stick]  Move your hand") && card.Contains($"[{turnStick} stick]  Turn it"),
            $"the setting put back ({(swapWas ? "swapped, as the player had it" : "not swapped")}): the {turnStick.ToLowerInvariant()} stick turns it again, and the card says so: \"{card}\"");
        inspector.LayDown();                        // square-on for the rest
        yield return Frames(2);

        // ---------- the cracked screen says why the back comes off (playtest 4: "a gate, not a cause"; 12 Oct: no cable) ----------
        {
            string why = part.Prompt;   // the part's own words (ReplaceablePart.CoveredHint), read from the source
            yield return Hover(BenchInteractable.BoundsCentre(part.transform), part.DisplayName, why, "the cracked screen, its back cover on");
            Check(why.Length <= 90 && why.Contains(cover.DisplayName.ToLowerInvariant()) && !why.StartsWith("Remove") && !why.ToLowerInvariant().Contains("cable"),
                $"it says why the back comes off in words of what the player does, no cable no verb touches (\"{why}\", {why.Length} characters)");
            yield return Photo("01b-held-in-by-the-back");
        }

        // ---------- the checklist says to flip it: its first step, the screws, lies on the face turned down (12-13 Oct) ----------
        yield return Seconds(.35f);   // the cards refresh every 0.15 s
        {
            string list = Plain(BenchCards.ChecklistText);
            Check(list.Contains("Take out the 2 screws") && list.Contains("flip it over"),
                $"face up, the checklist's step is the screws, and it says they're on the other side: \"{list}\"");
        }

        // ---------- the flip: over onto its back, where the screws are ----------
        float heightBefore = device.transform.position.y;
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.9f);
        Check(Vector3.Dot(Face(), Vector3.up) < -.99f && LowestPoint(device.transform) - inspector.TableTop.y < .004f,
            $"Y flips it over on the mat: the back is up, and it lies flat again ({(device.transform.position.y - heightBefore) * 1000f:0.0} mm change of height)");
        {
            string list = Plain(BenchCards.ChecklistText);
            Check(list.Contains("hold on each one") && !list.Contains("flip it over"),
                $"back up, the step says how instead: \"{list}\"");
        }
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
        yield return Until(() => rb0 != null && s0.IsLoose && (rb0.IsSleeping() || rb0.linearVelocity.magnitude < .02f)
                                 && Vector3.Distance(s0.transform.position, s0.HomePosition) > .012f, 8f, "free, it hops clear of its hole, falls and comes to rest");
        Check(OverMat(s0.transform.position) || stage.InTray(s0.transform.position),
            $"it lies on the mat, on the device or in the tray ({Where(s0.transform.position)}, {Vector3.Distance(s0.transform.position, s0.HomePosition) * 100f:0.0} cm from its hole)");
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
            yield return Await(() => rbo != null && so.IsLoose && (rbo.IsSleeping() || rbo.linearVelocity.magnitude < .02f), 8f);
            Check(lastWait && (OverMat(so.transform.position) || stage.InTray(so.transform.position)), $"it lands too ({Where(so.transform.position)})");
        }
        yield return Photo("04-screws-out");

        // ---------- the pry, and the cover ----------
        yield return SelectTool(ToolType.Pry);
        Vector3 edge = BenchInteractable.BoundsCentre(cover.transform) + cover.transform.forward * .05f;   // the end away from the screws
        yield return Hover(edge, cover.DisplayName, "Hold to pry it up", "the cover's far edge");
        // Playtest 4 (the veteran: "the pry tool stands upright on the middle of the back like the screwdriver"): over a
        // cover it can pry, the pry waits in the seam nearest the cursor, low, its handle out across the seam.
        ToolPickup pry = ToolInHand(ToolType.Pry);
        yield return Seconds(.4f);   // the tool settles on its spring
        {
            cover.SeamAt(edge, out Vector3 seam, out Vector3 across);
            Vector3 n = cover.Outward;
            float rise = Rise(pry, n);
            float off = pry != null ? Vector3.Distance(pry.transform.position, seam) : 1f;
            float outward = pry != null ? Vector3.Dot(Vector3.ProjectOnPlane(pry.transform.up, n).normalized, across) : -1f;
            float fromMiddle = pry != null ? Vector3.ProjectOnPlane(pry.transform.position - BenchInteractable.BoundsCentre(cover.transform), n).magnitude : 0f;
            Check(pry != null && off < .008f && rise > 20f && rise < 50f && outward > .8f,
                $"the pry waits in the seam at a pry angle, its handle out across it ({off * 1000f:0.0} mm from the seam, {rise:0}° up from the cover, " +
                $"{fromMiddle * 100f:0.0} cm from its middle), not stood upright on the middle of the back");
            yield return Photo("04b-the-pry-at-the-seam");
        }
        Quaternion coverRot0 = cover.transform.rotation;
        float riseBefore = Rise(pry, cover.Outward), lowest = riseBefore;
        PadHold(RT);
        {
            // (Until, but watching the handle as the lever goes in)
            float until = Time.realtimeSinceStartup + 2.5f;
            while (!cover.IsPopped && Time.realtimeSinceStartup < until)
            {
                lowest = Mathf.Min(lowest, Rise(pry, cover.Outward));
                yield return null;
            }
            lastWait = cover.IsPopped;
            Check(lastWait, "held at the edge, the cover creaks and pops" + (lastWait ? "" : " (gave up after 2.5 s)"));
        }
        Check(riseBefore - lowest > 6f, $"held, the pry levers: its handle went down {riseBefore - lowest:0}° as the edge came up (from {riseBefore:0}°)");
        yield return LetGo();
        yield return Seconds(.25f);
        float popped = Quaternion.Angle(coverRot0, cover.transform.rotation);
        Check(popped > 4f && popped < 20f, $"it sits up {popped:0.0}° at the pried edge");
        {
            bool under = cover.PryPose(edge, out Vector3 tipUnder, out _, out _, out _);
            float off = pry != null && under ? Vector3.Distance(pry.transform.position, tipUnder) : 1f;
            Check(under && off < .008f, $"popped, the pry's blade stays under the lifted edge ({off * 1000f:0.0} mm from it)");
        }
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
        Check(Vector3.Dot(Face(), Vector3.up) > .99f, "Y flips it back onto its front");
        yield return SelectTool(ToolType.Tweezers);
        LoosePart fresh0 = part.Fresh;
        yield return Hover(BenchInteractable.BoundsCentre(part.transform), part.DisplayName, "Hold to pinch it out", "the broken part");
        PadHold(RT);
        yield return Until(() => part.State == ReplaceablePart.PartState.HeldBroken, 1.5f, "held, the tweezers pinch and it lifts out");
        yield return Seconds(.3f);
        {
            // It rides at the depth it was pinched at, 2 cm toward the camera: clear of its seat, never deeper than it (8 Oct:
            // measured from the ray's near-plane origin it rode 5 cm too deep, and was carried in under the tray's floor).
            float rise = Depth(part.SeatPosition) - Depth(part.transform.position);
            Check(rise > .025f && rise < .06f, $"lifted, it rides toward the camera, held up clear of its seat ({rise * 1000f:0} mm nearer than the seat)");
        }
        // Over the tray's device side (the fresh part lies across its far side since 13 Oct: in plain view past a long device's
        // overhang), at the depth the held part rides at, so that, let go, it falls in beside it.
        Vector3 overTheTray = Above(stage.Tray.position - stage.TrayLongAxis * .04f, part.transform.position);
        PadCursor.GlideTo(Px(overTheTray));
        yield return Seconds(.8f);
        {
            float clear = LowestPoint(part.transform) - (stage.Tray.position.y + .003f);
            Check(clear > .021f, $"carried over the tray it rides above the tray's walls ({clear * 1000f:0} mm above the floor's top; the walls stand 21 mm), {TrayPlace(part.transform)}");
        }
        yield return LetGo();
        yield return Until(() => part.State == ReplaceablePart.PartState.Removed, 1f, "let go over the tray, it drops");
        Rigidbody prb = part.GetComponent<Rigidbody>();
        yield return Until(() => prb != null && stage.SettledInTray().Contains(prb), 5f, "the tray's magnet draws it down and settles it (a tink)");
        Check(job.Quality < .01f && !job.CanHandBack, "the grade doesn't move for taking the broken part out, and with a hole in it the device can't be handed back");
        Check(InView(part.transform) && stage.InTray(part.transform.position), $"the broken part lies in the tray, in view ({TrayPlace(part.transform)})");
        Check(fresh0 != null && Vector3.Distance(fresh0.transform.position, part.transform.position) > .04f && LowestPoint(fresh0.transform) < LowestPoint(part.transform) + .004f,
            $"beside the fresh part, not on it ({(fresh0 != null ? Vector3.Distance(fresh0.transform.position, part.transform.position) * 100f : 0f):0.0} cm apart)");
        Check(part.CanInteract && part.Grabbable && part.RequiredTool == ToolType.Hand && part.Prompt == "Hold to move it",
            "out of the device it is a loose piece: any tool picks it up and moves it (it can never bury the fresh part)");
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
        int finishesBefore = Juice.Finishes, wakesBefore = Juice.Wakes;
        float wordsBefore = Juice.LastWordsAt;
        yield return LetGo();
        yield return Until(() => part.IsReplaced, 1f, $"let go over the empty seat ({overSeat * 1000f:0} mm off it as seen), it seats");
        Check(job.Quality >= .999f && job.Grade == JobGrade.Perfect, $"the screen is replaced: {job.Grade}");
        Check(!job.IsComplete && !job.CanHandBack, "but it isn't finished: the cover is on the mat and the screws are out");
        Check(InView(part.transform) && stage.InTray(part.transform.position) && job.LooseParts.Contains(part.gameObject),
            $"the broken one stays in the tray as scrap, in view ({TrayPlace(part.transform)})");
        // Playtest 4, one ending: the last task is done, so the phone WAKES now (its new screen lights), quietly; "Fixed!"
        // waits for the cover and the screws (the veteran: "'Fixed!' fires ... with the cover still on the mat").
        DeviceWake wake = device.GetComponent<DeviceWake>();
        yield return Await(() => wake != null && wake.Woken, 1f);
        yield return Seconds(1.1f);
        Check(wake != null && wake.Woken && Juice.Wakes == wakesBefore + 1 && wake.LitScreen != null && wake.LitScreen.gameObject.activeInHierarchy
              && wake.LitScreen.sharedMaterial.IsKeywordEnabled("_EMISSION"),
            $"the last task done (the seat), the phone wakes: the fresh screen lights up ({(wake == null ? "no DeviceWake on the phone: run Bench 4" : wake.LitScreen == null ? "no screen lit" : wake.LitScreen.name + " glows")})");
        Check(Juice.Finishes == finishesBefore && Juice.LastWordsAt == wordsBefore,
            $"quietly: no \"Fixed!\" and no words while its cover is on the mat and its screws are out (endings so far: {Juice.Finishes - finishesBefore})");
        yield return Photo("08-the-phone-wakes");

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
        bool backUpAtTheEnd = Vector3.Dot(Face(), Vector3.up) < -.99f;   // it lies on its face for the screws: the ending's flip is from here
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
        float finishedAt = Time.realtimeSinceStartup;
        Check(job.IsComplete && job.CanHandBack && job.Grade == JobGrade.Perfect && job.IsFinished, $"the repair is finished: {job.Grade}, in one piece");
        // §10.5 (12 Oct; the veteran: "'Fixed!' fires over the phone's back"): finished lying on its back, the phone turns face
        // up by itself, within a second, and the one ending comes once it lies face up, pinned over its lit screen.
        yield return Await(() => inspector.Flipping || Juice.Finishes > finishesBefore, .5f);
        bool turnedByItself = inspector.Flipping && Juice.Finishes == finishesBefore;
        yield return Await(() => !inspector.Flipping && Juice.Finishes > finishesBefore, 1.5f);
        float faceUpIn = Time.realtimeSinceStartup - finishedAt;
        Check(backUpAtTheEnd && turnedByItself && Vector3.Dot(Face(), Vector3.up) > .99f && faceUpIn < 1f && LowestPoint(device.transform) - inspector.TableTop.y < .004f,
            $"finished lying on its back, the phone turns face up by itself ({(backUpAtTheEnd ? "back up" : "FACE UP")} at the last screw; face up and flat {faceUpIn:0.00} s later)");
        Check(lastWait && Juice.LastWords == "Fixed! · Perfect" && Juice.SparksUp >= 12,
            $"the one ending: \"Fixed! · Perfect\" and the big sparkle, once it lies face up (words: \"{Juice.LastWords}\", {Juice.SparksUp} sparks, {faceUpIn:0.00} s after the last screw)");
        {
            Vector3 middle = DeviceBounds.TryGet(device, out Bounds box) ? box.center : device.transform.position;
            float over = Juice.LastWordsPoint.y - middle.y;
            Check(Vector3.Dot(Face(), Vector3.up) > .99f && over > -.003f,
                $"the words are pinned over its face, the lit screen, not its back ({over * 1000f:0} mm above its middle)");
        }
        Check(Juice.LastWordsContrast >= 4.5f,
            $"on its dark plate the words read on any background ({Juice.LastWordsContrast:0.0}:1 over a white screen, the plate's worst case; 4.5:1 is the bar)");
        yield return Seconds(.8f);
        Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect",
            $"it ends once: one \"Fixed!\", and no \"Back together\" after it (endings: {Juice.Finishes - finishesBefore}; the last words \"{Juice.LastWords}\")");
        Check(InView(part.transform) && stage.InTray(part.transform.position) && part.DisplayName.StartsWith("Old ") && part.Grabbable,
            $"the old screen is still there in the tray, scrap that can be moved ({TrayPlace(part.transform)})");
        yield return Photo("10-fixed-once");

        // Playtest 4 (both playtesters: "Back together · Perfect" sat over "Screw: Hold to unscrew"): a finished phone offers
        // nothing to undo. Its screw reads "Screw" with no verb, and the driver held on it does nothing. The screws are in the
        // back: Y turns it over to look at them (the ending's own flip happens once; this one is the player's).
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.9f);
        Check(Vector3.Dot(Face(), Vector3.up) < -.99f, "Y turns the finished phone back over to look at its screws");
        ScrewTarget seated = screws.FirstOrDefault(t => OnPx(t.WorkPoint));
        Check(seated != null, "a screw of the finished phone is in view");
        if (seated != null)
        {
            Screw ss = seated.GetComponent<Screw>();
            yield return Hover(seated.WorkPoint, "Screw", "", "a screw of the finished phone (no verb)");
            Check(seated.Done && !seated.CanInteract, "a finished phone offers nothing to undo: its screw can't be worked");
            PadHold(RT);
            yield return Seconds(.5f);
            yield return LetGo();
            Check(!ss.IsOut && ss.Progress < .01f && inspector.HoldTarget == null,
                $"and the driver held on it doesn't turn it ({ss.Progress:P0} out)");
            yield return Photo("10b-nothing-to-undo");
        }

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
                          "cover.clack", "part.on", "part.pinch", "part.lift", "part.drop", "part.replace", "tool.pick", "tool.down", "device.snap", "device.flip",
                          "grime.scrub", "grime.clean", "cloth.wipe", "cloth.squeak", "cloth.gleam", "cloth.lift", "repair.fixed", "repair.ready", "phone.boot", "watch.wind",
                          "watch.tick", "camera.wind", "camera.shutter", "juice.sparkle", "juice.sparkle.big" };
        string[] silent = cues.Where(c => !PlaceholderSounds.Has(c)).ToArray();
        Check(silent.Length == 0, "every bench cue has a placeholder sound" + (silent.Length > 0 ? " (missing: " + string.Join(", ", silent) + ")" : ""));
        Check(Sfx.Play("screw.drop", stage.Tray.position), "a bench cue plays (the placeholder, until the bank has a file for it)");

        // ---------- out ----------
        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem && Quaternion.Angle(device.transform.rotation, benchPose) < 1f, "stepping back puts the device down as it lay");

        yield return ActTwo();
        yield return ActThree();
    }

    // ---------- act two (4b): the dirty phone: dirt as a layer the brush takes off where it touches, the smudge the cloth wipes, the device wakes ----------
    //
    // Playtest 4: the smudge is left for last (the other spot of grime goes by the check's hand), so the last wipe is the
    // phone's last task, as a player's would be: the cloth lifts off the glass, the gleam sweeps it, the phone wakes and the
    // one ending comes, and each is seen.

    IEnumerator ActTwo()
    {
        // the finished phone goes (the check's hand), and a second one comes in with the "gunked up" fault
        foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        made.Remove(device);
        Destroy(device);
        yield return Frames(2);
        device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = "Bench check phone (dirty)";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        int dirtyIndex = definition != null && definition.faults != null ? Array.FindIndex(definition.faults, f => f != null && f.type == FaultType.Cleaning) : -1;
        Check(dirtyIndex >= 0, "the phone has a cleaning fault");
        if (dirtyIndex < 0) yield break;
        DeviceFault dirty = definition.GetFault(dirtyIndex);
        definition.ApplyFault(dirtyIndex);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = dirtyIndex,
            faultType = dirty.type, faultDescription = dirty.description, payout = dirty.payout, number = 98, color = Color.white
        });
        yield return Frames(2);
        GrimeSpot[] spots = job.GetComponentsInChildren<GrimeSpot>();
        GrimeSpot[] grimes = spots.Where(g => g.What == GrimeSpot.Kind.Dirt).ToArray();
        GrimeSpot smudge = spots.FirstOrDefault(g => g.What == GrimeSpot.Kind.Smudge);
        Transform glass = device.transform.Find("Screen/Glass");
        Check(grimes.Length == 2 && smudge != null, $"the \"{dirty.description}\" phone has two spots of grime and a smudge ({spots.Length} spots: run Bench 4 if not)");
        Check(glass != null && glass.gameObject.activeInHierarchy && glass.GetComponent<MeshFilter>() != null && glass.GetComponent<MeshFilter>().sharedMesh != null,
            "it wears its working screen (Screen/Glass, on; the stand-in had no glass at all on this fault)");
        Check(device.GetComponentInChildren<ReplaceablePart>() == null, "and no broken screen");
        if (grimes.Length < 2 || smudge == null) yield break;
        Check(carry.TryPickUp(job), "Ace takes it");
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.4f);
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        yield return Seconds(.4f);
        Check(inspector.BeginInspection(job), "the close-up opens on it");
        if (!inspector.IsHoldingItem) yield break;
        yield return AfterBlend();
        yield return Frames(6);
        if (Vector3.Dot(device.transform.up, Vector3.up) < 0f) { yield return PadPress(GamepadButton.North); yield return Seconds(.9f); }
        Check(Vector3.Dot(device.transform.up, Vector3.up) > .99f, "its face is up: the dirt is on the screen");
        foreach (GrimeSpot g in spots)
        {
            Renderer r = g.GetComponent<Renderer>();
            Check(r != null && r.sharedMaterial != null && r.sharedMaterial.GetTexture("_BaseMap") != null && r.bounds.size.y < .003f,
                $"{g.name} is a patch on the glass with its own dirt mask ({(r != null ? r.bounds.size.y * 1000f : 0f):0.0} mm thick)");
        }
        yield return Photo("11-the-dirty-phone");

        // the brush: one stroke across a spot takes the dirt off where it passed, and nowhere else
        yield return SelectTool(ToolType.Brush);
        GrimeSpot first = grimes[0], other = grimes[1];
        Vector3 c0 = BenchInteractable.BoundsCentre(first.transform);
        float half = first.transform.lossyScale.x * .5f;
        Vector3 right = cam.transform.right, fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        yield return Hover(c0, "Grime", "Hold to scrub", "a spot of grime");
        int puffsBefore = Juice.DustPuffs;
        PadHold(RT);
        yield return Sweep(c0 - right * half * .7f, c0 + right * half * .7f);
        yield return LetGo();
        float partial = first.Progress;
        Check(partial > .05f && partial < .8f && first.Strokes > 0, $"a stroke across it takes some of the dirt off, where the bristles passed ({partial:P0} clean)");
        Check(other.Progress <= 0f && smudge.Progress <= 0f, "and none off the spot it didn't touch");
        Check(Juice.DustPuffs > puffsBefore, $"dust puffs up where the brush lifted some ({Juice.DustPuffs - puffsBefore} puffs)");
        yield return Hover(c0, "Grime", null, "the half-brushed spot");
        Check(inspector.HoverAction.StartsWith("Hold to scrub (") && inspector.HoverAction.EndsWith("%)"), $"the hover says how much has come off (\"{inspector.HoverAction}\")");
        yield return Photo("12-one-stroke");
        // a raster of strokes brushes it clean: at 97% it sparkles and goes
        PadHold(RT);
        for (int pass = 0; pass < 18 && first != null; pass++)
        {
            float k = -1f + 2f * ((pass % 9) / 8f);
            Vector3 a = c0 + fwd * half * k - right * half * 1.05f, b = c0 + fwd * half * k + right * half * 1.05f;
            yield return Sweep(pass % 2 == 0 ? a : b, pass % 2 == 0 ? b : a);
        }
        yield return LetGo();
        if (first != null) Note($"after the strokes: {first.Progress:P0} clean, {first.Strokes} strokes that lifted dust; left: {first.Remaining()}; the cursor reads \"{inspector.HoverName}: {inspector.HoverAction}\"");
        yield return Until(() => first == null, 1.5f, "brushed over in strokes, the spot comes clean and goes (97%: the last fleck never holds the grade)");
        Check(Mathf.Abs(job.Quality - 1f / 3f) < .01f, $"one of three cleaning tasks done: {job.Quality:P0}");
        Check(Sfx.Requested("grime.scrub") > 0 && Sfx.Requested("grime.clean") > 0, "the brush hissed where dust came off, and the spot chimed clean");

        // the other spot by the check's hand: two of three tasks done, and nothing ends yet (no wake, no words)
        int finishesBefore = Juice.Finishes, wakesBefore = Juice.Wakes;
        float wordsBefore = Juice.LastWordsAt;
        DeviceWake wake = device.GetComponent<DeviceWake>();
        other.Scrub(100000f);
        yield return Until(() => other == null && Mathf.Abs(job.Quality - 2f / 3f) < .01f, 1f, "the other spot cleaned (the check's hand): two of three tasks done");
        Check(wake != null && !wake.Woken && Juice.Wakes == wakesBefore && Juice.Finishes == finishesBefore && Juice.LastWordsAt == wordsBefore,
            $"with the smudge left nothing ends yet: no wake, no words ({job.Quality:P0}, {job.Grade}{(wake == null ? "; no DeviceWake on the phone: run Bench 4" : "")})");

        // the smudge: the cloth's. At rest the cloth lies folded flat on the mat, clear of the caddy (9 Oct night, his note:
        // "the cloth stuck inside the tray, it needs to sit and look correctly and be used correctly": Bench 2 had stood
        // it 12 mm into the caddy's block like the pointed tools, and it was buried in the wood).
        Vector3 cs = BenchInteractable.BoundsCentre(smudge.transform);
        ToolPickup restingCloth = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.tool == ToolType.Cloth);
        BenchStage stage = BenchStage.Instance;
        if (restingCloth != null && stage != null && stage.Caddy != null && stage.Mat != null)
        {
            Bounds cloth = RendererBounds(restingCloth.transform), caddy = RendererBounds(stage.Caddy), mat = RendererBounds(stage.Mat);
            float tilt = Vector3.Angle(restingCloth.transform.up, Vector3.up), over = (cloth.min.y - mat.max.y) * 1000f;
            bool onMat = cloth.min.x >= mat.min.x && cloth.max.x <= mat.max.x && cloth.min.z >= mat.min.z && cloth.max.z <= mat.max.z;
            Check(!restingCloth.InHand && tilt < 3f && over > -1f && over < 3f && onMat && !cloth.Intersects(caddy),
                $"the cloth rests folded on the mat: level ({tilt:0.0}°), {over:0.0} mm over it, {(onMat ? "inside its edges" : "OFF its edges")}, " +
                (cloth.Intersects(caddy) ? "INSIDE the caddy" : "clear of the caddy"));
            Check(!restingCloth.IsStandIn, "it is the modelled cloth in its own yellow, not taken for a tinted stand-in (which never leaves its stand)");
        }
        else Check(false, "the cloth, the caddy and the mat are on the bench (run Bench 2b if the cloth is missing)");
        yield return Hover(cs, "Smudge", "Needs the cloth", "the smudge, under the brush");
        yield return SelectTool(ToolType.Cloth);
        yield return Hover(cs, "Smudge", "Hold to wipe", "the smudge, under the cloth");
        float halfS = smudge.transform.lossyScale.x * .5f;
        // The smudge's patch, kept for after it has gone: a disc on the glass, to measure what the cloth hides of it.
        Vector3 patchA = right * (halfS * .8f), patchB = fwd * (halfS * .8f);
        Renderer glassRenderer = glass != null ? glass.GetComponent<Renderer>() : null;
        float glassTop = glassRenderer != null ? glassRenderer.bounds.max.y : cs.y;
        int gleamsBefore = Juice.Gleams, liftsBefore = Sfx.Requested("cloth.lift");
        float seenWiping = -1f;
        PadHold(RT);
        for (int lap = 0; lap < 5 && !Gone(smudge); lap++)
            for (int i = 0; i < 8 && !Gone(smudge); i++)
            {
                float a = (i + .5f) / 8f * Mathf.PI * 2f;
                float rr = lap == 0 ? .25f : lap == 1 ? .6f : .85f;
                yield return GlideUntil(cs + (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * halfS * rr, .14f, () => Gone(smudge));
                if (lap == 0 && i == 5 && !Gone(smudge))
                {
                    // in the hand it is ON the phone, pressed flat on the glass (no lean, no swing), leading with its far fold
                    ToolPickup held = ToolInHand(ToolType.Cloth);
                    float off = held != null ? Vector3.Angle(held.transform.up, Vector3.up) : 180f;
                    Vector3 rel = held != null ? held.transform.position - cs : Vector3.one;
                    float across = Vector3.ProjectOnPlane(rel, Vector3.up).magnitude, above = rel.y;
                    Check(held != null && across < .07f && above > -.004f && above < .01f && off < 8f,
                        $"the cloth is in the hand, on the phone, wiping lying flat on the glass: its middle {across * 100f:0.0} cm from the smudge " +
                        $"(its fold leads), {above * 1000f:0.0} mm over the glass, {off:0}° off level");
                    // Playtest 4 (the veteran: "the cloth covers about two-thirds of the screen while you wipe"): in the hand it is
                    // gathered into a pad, so the glass it has just wiped shows behind its leading fold.
                    Vector3 size = PadSize(held);
                    Check(held != null && held.PadShown < .5f && size.x < .045f && size.z < .05f,
                        $"in the hand it is gathered into a pad: {size.x * 1000f:0} x {size.z * 1000f:0} mm across the glass ({(held != null ? held.PadShown : 1f):P0} of its folded 75 x 90)");
                    float hidden = glass != null && FlatFace(glass, out Vector3 gm, out Vector3 ga, out Vector3 gb) ? 1f - SeenPast(held, gm, ga, gb, false, 13) : 1f;
                    Check(hidden <= .3f, $"wiping, it hides {hidden:P0} of the glass on screen (the folded cloth hid about two thirds)");
                    seenWiping = SeenPast(held, cs, patchA, patchB, true);
                    Note($"wiping: {seenWiping:P0} of the smudge's patch in view past the cloth; the hover reads \"{inspector.HoverName}: {inspector.HoverAction}\"");
                    yield return Photo("13a-the-cloth-wiping");
                }
            }
        bool cleaned = Gone(smudge);
        float cleanAt = Time.realtimeSinceStartup;
        yield return LetGo();
        Check(cleaned, "wiped in circles, the smudge thins and comes clean" + (cleaned ? "" : $" (only {(smudge != null ? smudge.Progress : 0f):P0} after five laps)"));
        Check(Sfx.Requested("cloth.squeak") > 0, "the cloth squeaked while grease came off");
        ToolPickup lifted = ToolInHand(ToolType.Cloth);
        Check(lifted != null && lifted.Lifting && Sfx.Requested("cloth.lift") > liftsBefore,
            "the moment it comes clean, the cloth lifts off the glass, with its swish (playtest 4: \"the gleam happens under it\")");

        // Watched for 2.2 s from the moment it came clean (the time the frames take, not a count of them): mid-lift (0.3 s)
        // the gleam sweeps the glass the cloth has just left; by 1.15 s the phone has woken (the smudge was its last task),
        // its screen is lit and the one ending is up, and the cloth is still clear; by 2.2 s it is back on the glass.
        float highest = float.NegativeInfinity;
        bool shotLift = false, shotLit = false;
        while (cleaned && Time.realtimeSinceStartup < cleanAt + 1.3f)
        {
            if (lifted != null) highest = Mathf.Max(highest, lifted.transform.position.y - glassTop);
            float t = Time.realtimeSinceStartup - cleanAt;
            if (!shotLift && t >= .3f)
            {
                shotLift = true;
                float seen = SeenPast(lifted, cs, patchA, patchB, true);
                Check(lifted != null && lifted.Lifted > .5f && seen >= .8f,
                    $"lifting, the cloth is off where the smudge was: {seen:P0} of it in view past the cloth ({t:0.00} s after; " +
                    (seenWiping >= 0f ? $"{seenWiping:P0} while wiping)" : "not measured while wiping)"));
                yield return Photo("13b-the-cloth-lifts");
            }
            if (!shotLit && t >= 1.15f)
            {
                shotLit = true;
                Check(wake != null && wake.Woken && Juice.Wakes == wakesBefore + 1 && wake.LitScreen != null && wake.LitScreen.transform == glass
                      && wake.LitScreen.sharedMaterial.IsKeywordEnabled("_EMISSION"),
                    $"the smudge was its last task: the phone wakes, its working screen lights up ({(wake == null ? "no DeviceWake" : wake.LitScreen == null ? "nothing lit" : wake.LitScreen.name + " glows")})");
                Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect" && job.IsFinished && job.Grade == JobGrade.Perfect,
                    $"never opened, it is finished at that last wipe: \"Fixed! · Perfect\", once, a beat after the wake (words: \"{Juice.LastWords}\", endings: {Juice.Finishes - finishesBefore})");
                Check(lifted != null && lifted.Lifted > .9f, $"and the cloth is still up, clear of the glass, while the screen lights ({(lifted != null ? lifted.Lifted : 0f):P0} lifted, {t:0.00} s after)");
                Check(Juice.Gleams > gleamsBefore && Sfx.Requested("cloth.gleam") > 0, "a gleam swept the glass it had cleared");
                yield return Photo("14-clean-and-lit");
            }
            yield return null;
        }
        Check(cleaned && highest >= .02f, $"lifted, the cloth rose {(float.IsInfinity(highest) ? 0f : highest * 1000f):0} mm off the glass, up and back toward the hand");
        yield return Seconds(Mathf.Max(0f, cleanAt + 2.2f - Time.realtimeSinceStartup));
        float down = lifted != null ? lifted.transform.position.y - glassTop : 1f;
        Check(lifted != null && !lifted.Lifting && down < .012f,
            $"then it comes back down to the cursor on the glass ({down * 1000f:0} mm over it, {Time.realtimeSinceStartup - cleanAt:0.0} s after it came clean)");
        Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect", "it ended once: no \"Back together\" for a phone that was never opened");

        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem, "stepping back puts it down");
    }

    // ---------- act three (playtest 4): Grace's camera, the story's own device, ends the same way ----------
    //
    // By the check's hand (this act is about the moments, not the hands: Checks > Grace camera tweezers interaction proves
    // the verbs on it): its lens and film path cleaned, and nothing ends while its shutter is jammed (a clean camera that
    // can't take a picture isn't fixed: Quality 0); the new shutter in, the camera wakes (wound, its shutter fires once) and
    // "Fixed! · Perfect" comes once. Its scratched strap is still on it.

    IEnumerator ActThree()
    {
        if (inspector.IsHoldingItem) { inspector.CancelInspection(); yield return AfterBlend(); }   // act two stopped short
        foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        made.Remove(device);
        Destroy(device);
        yield return Frames(2);
        GameObject cameraPrefab = null;
#if UNITY_EDITOR
        cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GraceShowcase/GraceReunionCamera.prefab");
#endif
        Check(cameraPrefab != null, "Grace's camera prefab loads");
        if (cameraPrefab == null) yield break;
        device = Instantiate(cameraPrefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = "Bench check Grace camera";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        var grace = job as GraceCameraRepairJob;
        DeviceFault fault = definition != null ? definition.GetFault(0) : null;
        Check(definition != null && grace != null && fault != null && grace.Shutter != null, "it has a definition, Grace's camera job, its fault and a shutter");
        if (definition == null || grace == null || fault == null || grace.Shutter == null) yield break;
        definition.ApplyFault(0);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = cameraPrefab, deviceName = definition.displayName, faultIndex = 0,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 97, color = Color.white
        });
        yield return Frames(2);
        GrimeSpot[] spots = job.GetComponentsInChildren<GrimeSpot>();
        DeviceWake wake = device.GetComponent<DeviceWake>();
        Check(spots.Length == 3 && wake != null && wake.What == DeviceWake.Kind.ShutterFires,
            $"its lens and film path are dirty ({spots.Length} spots) and it wakes by firing its shutter ({(wake != null ? wake.What.ToString() : "no DeviceWake: run Bench 4")})");
        Check(carry.TryPickUp(job), "Ace takes it");
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.4f);
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        yield return Seconds(.4f);
        Check(inspector.BeginInspection(job), "the close-up opens on it");
        if (!inspector.IsHoldingItem) yield break;
        yield return AfterBlend();
        yield return Frames(6);
        yield return Photo("15-grace-camera-on-the-mat");

        int finishesBefore = Juice.Finishes, wakesBefore = Juice.Wakes;
        float wordsBefore = Juice.LastWordsAt;
        foreach (GrimeSpot g in spots) if (g != null) g.Scrub(100000f);
        yield return Until(() => job.GetComponentsInChildren<GrimeSpot>().Length == 0, 1f, "its lens and film path cleaned (the check's hand)");
        yield return Seconds(.5f);
        Check(job.Quality <= 0f && wake != null && !wake.Woken && Juice.Wakes == wakesBefore && Juice.Finishes == finishesBefore && Juice.LastWordsAt == wordsBefore,
            $"clean, with its shutter jammed, it can't take a picture: no wake, no words, no grade yet ({job.Quality:P0}; \"{grace.TaskSummary}\")");
        int windsBefore = Sfx.Requested("camera.wind"), shuttersBefore = Sfx.Requested("camera.shutter");
        grace.Shutter.Activate();
        yield return Until(() => grace.Shutter.IsReplaced, 3f, "the new shutter goes in (the check's hand: the jammed one out to the tray, the working one seated)");
        yield return Await(() => Juice.Finishes > finishesBefore, 1.5f);
        yield return Seconds(.6f);
        Check(wake != null && wake.Woken && Juice.Wakes == wakesBefore + 1, "its last task done, the camera wakes: its advance wound, the new shutter fires");
        Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect" && job.IsFinished && job.Grade == JobGrade.Perfect,
            $"in one piece with every fault fixed, it ends once: \"Fixed! · Perfect\" (words: \"{Juice.LastWords}\", endings: {Juice.Finishes - finishesBefore})");
        yield return Photo("16-grace-camera-fixed");
        yield return Seconds(.5f);
        Check(Sfx.Requested("camera.wind") > windsBefore && Sfx.Requested("camera.shutter") == shuttersBefore + 1,
            $"one shutter is the beat, not two: the advance wound, then a single shutter ({Sfx.Requested("camera.shutter") - shuttersBefore} heard)");
        Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect", "still one ending, and nothing after it");
        Transform strap = device.transform.Find("KEEP - scratched sentimental strap");
        Check(strap != null && strap.gameObject.activeInHierarchy, "its scratched strap is still on it: kept, never a part to replace");

        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem, "stepping back puts it down");
    }

    // The cursor glided from one point on the device to another with the button held: a stroke of the tool between them.
    // A stroke of the tool from one point to another, as a hand would make it: in steps of about a centimetre, a frame or
    // two each, so the cursor is ON the spot at frame boundaries whatever the frame rate. A glide is 0.03 s (PadCursor), one
    // frame with Unity behind other windows: the cursor then jumps from a stroke's start to its end in that frame, and a
    // stroke whose ends lie off the spot's disc (the raster's outermost rows start and end beyond its edge) never has the
    // cursor over the spot at any frame, so nothing is scrubbed along it (13 Oct: 117 of 125 on 9 Oct and 164 of 172 today,
    // the same eight, the spot left at 90-96 % along one side; 125/125 and 161/161 with Unity in front). The game is not
    // the matter: a player's cursor crosses a spot over many frames.
    IEnumerator Sweep(Vector3 from, Vector3 to)
    {
        yield return Glide(from, .3f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / .008f), 2, 12);
        for (int i = 1; i <= steps; i++) yield return Glide(Vector3.Lerp(from, to, i / (float)steps), .1f);
    }

    IEnumerator Glide(Vector3 world, float seconds)
    {
        PadCursor.GlideTo(Px(world));
        yield return Frames(2);
        yield return Await(() => !PadCursor.Gliding, seconds);
    }

    // The same glide, watched every frame: it stops the moment `stop` comes true (the smudge comes clean), so what follows
    // is timed from that frame, not from the end of a glide.
    IEnumerator GlideUntil(Vector3 world, float seconds, Func<bool> stop)
    {
        PadCursor.GlideTo(Px(world));
        for (int f = 0; f < 2 && !Safe(stop); f++) yield return null;
        float until = Time.realtimeSinceStartup + seconds;
        while (PadCursor.Gliding && !Safe(stop) && Time.realtimeSinceStartup < until) yield return null;
    }

    static bool Gone(GrimeSpot spot) => spot == null || spot.IsClean;

    // ---------- measuring the tools (playtest 4) ----------

    // The tool's handle (its +Y) up from a surface with normal n, degrees: 0 lying on it, 90 stood on its tip.
    static float Rise(ToolPickup tool, Vector3 n) =>
        tool == null ? 0f : Mathf.Asin(Mathf.Clamp(Vector3.Dot(tool.transform.up, n.normalized), -1f, 1f)) * Mathf.Rad2Deg;

    // The cloth's size in the world now (its mesh's bounds at its scale): across the glass (x, z) and its height (y).
    static Vector3 PadSize(ToolPickup cloth)
    {
        MeshFilter mf = cloth != null ? cloth.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return Vector3.one;
        Vector3 s = cloth.transform.lossyScale;
        return Vector3.Scale(mf.sharedMesh.bounds.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    // A flat thing's face toward the camera (its mesh's bounds): its middle and its two half-axes, in the world.
    bool FlatFace(Transform t, out Vector3 middle, out Vector3 halfA, out Vector3 halfB)
    {
        middle = t.position;
        halfA = halfB = Vector3.zero;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        Bounds b = mf.sharedMesh.bounds;
        Vector3 e = b.extents;
        int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
        int i = thin == 0 ? 1 : 0, k = thin == 2 ? 1 : 2;
        Vector3 up = Vector3.zero, da = Vector3.zero, db = Vector3.zero;
        up[thin] = e[thin];
        da[i] = e[i];
        db[k] = e[k];
        Vector3 top = t.TransformPoint(b.center + up), bottom = t.TransformPoint(b.center - up);
        Vector3 eye = cam.transform.position;
        middle = (top - eye).sqrMagnitude <= (bottom - eye).sqrMagnitude ? top : bottom;
        halfA = t.TransformVector(da);
        halfB = t.TransformVector(db);
        return true;
    }

    // The share of a patch on the device (a grid of points over a disc or a rectangle, given by its middle and two half-axes)
    // that the camera sees past the cloth in the hand: 1, none of it hidden. The cloth is its mesh's box, wherever it is now.
    float SeenPast(ToolPickup cloth, Vector3 middle, Vector3 halfA, Vector3 halfB, bool round, int rows = 9)
    {
        MeshFilter mf = cloth != null ? cloth.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null || cam == null || rows < 2) return 1f;
        Bounds box = mf.sharedMesh.bounds;
        Vector3 eye = cloth.transform.InverseTransformPoint(cam.transform.position);
        int seen = 0, all = 0;
        for (int i = 0; i < rows; i++)
            for (int k = 0; k < rows; k++)
            {
                float u = i / (rows - 1f) * 2f - 1f, v = k / (rows - 1f) * 2f - 1f;
                if (round && u * u + v * v > 1f) continue;
                all++;
                Vector3 p = cloth.transform.InverseTransformPoint(middle + halfA * u + halfB * v);
                Vector3 d = p - eye;
                float length = d.magnitude;
                if (length > 1e-6f && box.IntersectRay(new Ray(eye, d / length), out float hit) && hit < length) continue;
                seen++;
            }
        return all > 0 ? seen / (float)all : 1f;
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

    // Where a thing lies in the tray, for the report: along and across the tray from its middle, how far off its floor, and
    // whether the close-up camera draws it at all.
    string TrayPlace(Transform t)
    {
        BenchStage stage = BenchStage.Instance;
        Vector3 d = t.position - stage.Tray.position;
        Renderer r = FirstRenderer(t);
        string drawn = r == null ? "no renderer" : !r.enabled ? "renderer off" : !t.gameObject.activeInHierarchy ? "inactive"
            : (cam.cullingMask & (1 << r.gameObject.layer)) == 0 ? $"layer {LayerMask.LayerToName(r.gameObject.layer)} not drawn" : "drawn";
        return $"{Vector3.Dot(d, stage.TrayLongAxis) * 100f:0.0} cm along, {Vector3.Dot(d, Vector3.Cross(Vector3.up, stage.TrayLongAxis)) * 100f:0.0} cm across, "
             + $"{(LowestPoint(t) - stage.Tray.position.y) * 1000f:0} mm off the floor, {drawn}";
    }

    // In view: drawn by the close-up camera, and not sunk into the tray's floor.
    bool InView(Transform t)
    {
        Renderer r = FirstRenderer(t);
        return r != null && r.enabled && t.gameObject.activeInHierarchy && (cam.cullingMask & (1 << r.gameObject.layer)) != 0
               && LowestPoint(t) > BenchStage.Instance.Tray.position.y - .004f;
    }

    static Renderer FirstRenderer(Transform t)
    {
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) if (r.GetComponent<BenchOutlineHull>() == null) return r;
        return null;
    }

    // The lowest point of a thing's renderers (the hull children of the hover outline aside).
    static float LowestPoint(Transform root)
    {
        float low = float.PositiveInfinity;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.GetComponent<BenchOutlineHull>() == null) low = Mathf.Min(low, r.bounds.min.y);
        return float.IsInfinity(low) ? root.position.y : low;
    }

    // The bench's top under the stage, by a ray that ignores the stage's own pieces.
    static float BenchTopUnder(BenchStage stage)
    {
        Vector3 from = stage.Mat.position + Vector3.up * .3f;
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(stage.transform)) continue;
            if (h.collider.GetComponentInParent<JobBase>() != null) continue;
            best = Mathf.Max(best, h.point.y);
        }
        return float.IsInfinity(best) ? stage.Mat.position.y : best;
    }

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
    float Depth(Vector3 world) => Vector3.Dot(world - cam.transform.position, cam.transform.forward);

    static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

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

    static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " / ");

    protected override void Restore()
    {
        PadRelease();
        if (swapTouched) GameSettings.SwapBenchSticks = swapWas;   // the player's setting, never the check's
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (job != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        foreach (GameObject g in made) if (g != null) Destroy(g);      // the job's loose parts go with it (JobBase.OnDestroy)
        if (spawner != null) spawner.enabled = spawnerWas;
    }
}
