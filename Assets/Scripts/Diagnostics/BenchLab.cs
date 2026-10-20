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
//   Act two (the forgiving bench, 14 Oct): a second phone, the "gunked up" one, wearing its working screen: the brush taken
//   by a press on it at rest and dragged over, let go over the phone and still in the hand; a stroke takes the dirt off only
//   where it passed; four strokes at most clean a spot; s follows the hand and the rub loop with it; the phone budges along the
//   stroke and comes back; its last spot clean, the brush flies home by itself; "what's left" lights the smudge gold; the
//   cloth by RB smears the grease, then wipes it clean, and flies home by itself as the phone wakes and ends once.
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
//   * (The cloth's lift-off after each smudge is gone with the forgiving bench: it goes home by itself after its last spot.)
//   * Act three (14 Oct): Grace's camera by its verbs: Day 1's clean (the cloth and the brush in the hand) and Day 2's swap
//     (the tweezers' pinch, the clean shutter's one hold), one "Fixed! · Perfect" each; the strap is still there.
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
        Check(stage != null && stage.Mat != null && stage.Tray != null, "the bench stage is in the scene (Fixit Fidget > Bench > Bench 2, then Bench 7, build it)");
        Check(stage != null && stage.matFrame != null && stage.cups != null && stage.cups.Length == 6 && stage.padLarge != null && stage.wellLarge != null && stage.scrapTray != null && stage.toolRest != null,
            "the forgiving stage is wired: the mat with its six cups, its pad and its well, the old-parts tray, the tool rest (Bench 7)");
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
        // 19 Oct (her "I don't know where to go"): while a device that needs the bench is in hand, its place on the mat is
        // marked (BenchGlow's put-down mark and its ring on the screen), on the mat's top, where the device's layout rests it.
        {
            Vector3 rest = stage.MatToWorld(DeviceDefinition.LayoutOf(job).rest, BenchStage.MatTop);
            Check(BenchGlow.PutDownShowing && Vector3.Distance(BenchGlow.PutDownPoint, rest) < .01f,
                $"carried, the phone's place on the mat is marked for it ({(BenchGlow.PutDownShowing ? $"{Vector3.Distance(BenchGlow.PutDownPoint, rest) * 1000f:0} mm from its rest point" : "no mark")})");
        }
        yield return Photo("00-carried-from-above");
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        Check(device.transform.localScale.x < 1.2f, $"put down, it is its real size again ({device.transform.localScale.x:0.00}x)");
        yield return Seconds(.6f);
        Check(!BenchGlow.PutDownShowing, "set down, the mark on the mat is gone");
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
        int guardTripsBefore = BenchStage.GuardTrips;
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

        // The fresh part was laid on the new-part pad when the close-up opened: where is it a moment later?
        LoosePart freshAtStart = part.Fresh;
        yield return Seconds(.8f);
        Check(freshAtStart != null && (stage.PlaceOf(freshAtStart) == stage.padSmall || stage.PlaceOf(freshAtStart) == stage.padLarge) && !HasBody(freshAtStart.gameObject),
            $"the fresh part waits on the new-part pad from the start ({(freshAtStart != null && stage.PlaceOf(freshAtStart) != null ? stage.PlaceOf(freshAtStart).name : "not on a pad")}), never a rigidbody");

        // On the mat: lying flat, a work face up, square to the view, its bottom on the mat's top.
        frontSign = Vector3.Dot(device.transform.up, Vector3.up) >= 0f ? 1f : -1f;
        float faceOff = Vector3.Angle(Face(), Vector3.up);
        float gap = LowestPoint(device.transform) - inspector.TableTop.y;
        Check(faceOff < 1f && Mathf.Abs(gap) < .004f && Mathf.Abs(Mathf.DeltaAngle(inspector.Yaw, Mathf.Round(inspector.Yaw / 90f) * 90f)) < .5f,
            $"it lies flat on the mat, face up and square ({faceOff:0.0}° off flat, {gap * 1000f:0.0} mm off the mat, yaw {inspector.Yaw:0})");
        {
            // The mat's own drawn pieces only: the fresh part waiting on its pad is under the mat too, and its 3 mm read as the
            // mat's top (15 Oct). And the mat's bottom within 2 mm of the bench top either way: sunk into the wood is as wrong as
            // floating (the first Bench 7 put it 6 mm in, and the wood flickered through the mat in play).
            Bounds matBounds = MatOwnBounds(stage.Mat);
            Check(Mathf.Abs(inspector.TableTop.y - matBounds.max.y) < .0015f && Mathf.Abs(matBounds.min.y - benchTopGuess) < .002f,
                $"the stage sits on the bench top (the mat's bottom {(matBounds.min.y - benchTopGuess) * 1000f:0.0} mm above it; the device rests on its top, {(inspector.TableTop.y - matBounds.max.y) * 1000f:0.0} mm off)");
        }
        yield return Photo("01-on-the-mat");

        // ---------- the zoom (the TD's §5.3: a lens zoom; the camera keeps its distance, the FOV narrows) ----------
        float fov0 = cam.fieldOfView, d0 = Distance();
        PadHold(new GamepadState().WithButton(GamepadButton.DpadUp));
        yield return Seconds(.5f);
        yield return LetGo();
        yield return Seconds(.5f);
        float fovIn = cam.fieldOfView, dIn = Distance();
        Check(inspector.Zoom > .3f && fovIn < fov0 * .85f && Mathf.Abs(dIn - d0) < .04f,
            $"D-pad up zooms the lens in (FOV {fov0:0.0}° to {fovIn:0.0}°; the camera {Mathf.Abs(dIn - d0) * 100f:0.0} cm nearer the device: it keeps its distance)");
        // Held until it is all the way out, as a player holds it (the zoom stops at the layout view): at 15 fps an equal 0.5 s
        // in and out can differ by a frame, 0.05 of the zoom (15 Oct: FOV 38.6).
        PadHold(new GamepadState().WithButton(GamepadButton.DpadDown));
        yield return Seconds(.9f);
        yield return LetGo();
        yield return Seconds(.5f);
        float fovOut = cam.fieldOfView;
        Check(inspector.Zoom < .05f && fovOut > fovIn * 1.1f && Mathf.Abs(fovOut - ItemInspector.LayoutFov) < 1f,
            $"D-pad down zooms out again, back to the layout view (FOV {fovOut:0.0}°)");

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
        // 19 Oct: the sticks are icons in the card's rich text, found through the helper the card uses (ControlHints follows the
        // setting); the words after them in the plain text.
        string rich = BenchCards.HeldControlsText ?? "", card = Plain(rich);
        Check(GameSettings.SwapBenchSticks && LineHas(rich, "Move your hand", ControlHints.BenchHandStick) && LineHas(rich, "Turn it", ControlHints.BenchTurnStick) && !rich.Contains("["),
            $"...and the controls card says so, the sticks as icons: \"{card}\"");
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
        rich = BenchCards.HeldControlsText ?? "";
        card = Plain(rich);
        string turnStick = swapWas ? "right" : "left";
        Check(inspector.QuarterTurns == quarters0 + 1 && GameSettings.SwapBenchSticks == swapWas && LineHas(rich, "Move your hand", ControlHints.BenchHandStick) && LineHas(rich, "Turn it", ControlHints.BenchTurnStick),
            $"the setting put back ({(swapWas ? "swapped, as the player had it" : "not swapped")}): the {turnStick} stick turns it again, and the card says so: \"{card}\"");
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
            Check(BenchCards.CurrentIcon == "screwdriver" && BenchCards.CurrentVerb.Contains("Flip it over"),
                $"face up, the card's step is the screwdriver, and it says to flip it over: \"{BenchCards.CurrentIcon}\" / \"{BenchCards.CurrentVerb}\"");
        }

        // ---------- the flip: over onto its back, where the screws are ----------
        float heightBefore = device.transform.position.y;
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.9f);
        Check(Vector3.Dot(Face(), Vector3.up) < -.99f && LowestPoint(device.transform) - inspector.TableTop.y < .004f,
            $"Y flips it over on the mat: the back is up, and it lies flat again ({(device.transform.position.y - heightBefore) * 1000f:0.0} mm change of height)");
        {
            Check(BenchCards.CurrentVerb == "Unscrew", $"back up, the card's verb is \"{BenchCards.CurrentVerb}\" (the screws are in reach)");
        }
        yield return Photo("02-the-back");

        // ---------- the screwdriver, and the screws (the forgiving bench, 14 Oct: each screw pops and flies to its own cup) ----------
        yield return SelectTool(ToolType.Screwdriver);
        ToolPickup driver = ToolInHand(ToolType.Screwdriver);
        PadCursor.GlideTo(Px(device.transform.position));
        yield return Seconds(.5f);
        Check(driver != null && driver.InHand && Vector3.Distance(driver.transform.position, device.transform.position) < .12f
              && Vector3.Distance(driver.transform.position, stage.ToolSlot(ToolType.Screwdriver).position) > .05f,
            "the driver leaves the rest and rides with the cursor over the device");

        ScrewTarget first = screws.FirstOrDefault(s => s.CanInteract && OnPx(s.WorkPoint));
        Check(first != null, "a screw is in view on the back");
        if (first == null) yield break;
        Screw s0 = first.GetComponent<Screw>();
        yield return Hover(first.WorkPoint, "Screw", null, "the first screw");
        PadHold(RT);
        yield return Seconds(.3f);
        yield return LetGo();
        float backedOut = Vector3.Dot(s0.transform.position - s0.HomePosition, s0.Axis);
        float away = Vector3.ProjectOnPlane(s0.transform.position - s0.HomePosition, s0.Axis).magnitude;
        Check(!s0.IsOut && s0.Progress > .1f && s0.Progress < .9f && backedOut > .0002f && away < .0005f,
            $"a short hold backs it part way out along its own axis ({s0.Progress:P0}, {backedOut * 1000f:0.0} mm out, {away * 1000f:0.00} mm sideways) and it keeps its place when let go");
        yield return Photo("03-part-way-out");
        int dropsBefore = Sfx.Requested("screw.drop"), flysBefore = Sfx.Requested("screw.fly"), bouncesBefore = Sfx.Requested("screw.bounce");
        PadHold(RT);
        yield return Until(() => s0.IsOut, 3f, "held on, it comes free");
        yield return LetGo();
        yield return ScrewLands(s0, "the first screw");
        Check(s0.Socket != null && s0.Socket.gameObject.activeSelf && s0.Socket.CanInteract, "its empty hole is marked and can be held");
        Check(job.HasDetachedComponent<Screw>() && !job.CanHandBack, "with a screw out the device can't be handed back");

        foreach (ScrewTarget other in screws.Where(s => s != first))
        {
            Screw so = other.GetComponent<Screw>();
            yield return Hover(other.WorkPoint, "Screw", null, "the next screw");
            PadHold(RT);
            yield return Until(() => so.IsOut, 3f, "held on, it comes free too");
            yield return LetGo();
            yield return ScrewLands(so, "the next screw");
        }
        Check(Sfx.Requested("screw.fly") > flysBefore && Sfx.Requested("screw.drop") >= dropsBefore + screws.Length && Sfx.Requested("screw.bounce") == bouncesBefore,
            $"each flight whooshes (screw.fly) and lands with a tink (screw.drop), never a physics bounce (screw.bounce: {Sfx.Requested("screw.bounce") - bouncesBefore})");
        {
            // The slot rule (§4.0.1): the phone turned a quarter covers the left cup column, so its screws take cups 4-6.
            var used = screws.Select(t => stage.PlaceOf(t.GetComponent<Screw>())).Where(p => p != null).Select(p => p.name).ToArray();
            Check(used.Length == screws.Length && used.All(n => n == "Cup_4" || n == "Cup_5" || n == "Cup_6"),
                $"one cup each, clear of the phone's every quarter turn: {string.Join(", ", used)} (the left column, cups 1-3, lies under a turned phone)");
        }
        yield return Photo("04-screws-in-their-cups");
        // 19 Oct (her "it just said something about the hole"): in the pry step a hole is a mark on the cover and nothing more:
        // it shows (its plate is on), it can't be picked (no screw step), and the cursor over it never reads "Screw hole".
        {
            Screw sh = first.GetComponent<Screw>();
            Check(sh.Socket != null && sh.Socket.gameObject.activeInHierarchy && !sh.Socket.isActiveAndEnabled,
                $"the pry step: the empty hole shows on the cover but can't be picked (shown {(sh.Socket != null && sh.Socket.gameObject.activeInHierarchy)}, pickable {(sh.Socket != null && sh.Socket.isActiveAndEnabled)})");
            PadCursor.GlideTo(Px(sh.Socket != null ? sh.Socket.WorkPoint : sh.HomePosition));
            yield return Frames(2);
            yield return Await(() => !PadCursor.Gliding, 1f);
            yield return Frames(3);
            Check(inspector.HoverName != "Screw hole", $"...and the cursor over it reads \"{inspector.HoverName}\", not \"Screw hole\"");
        }

        // ---------- the pry, and the cover (it pops where it was pried, then flies to the well by itself) ----------
        yield return SelectTool(ToolType.Pry);
        Vector3 edge = BenchInteractable.BoundsCentre(cover.transform) + cover.transform.forward * .05f;   // the end away from the screws
        yield return Hover(edge, cover.DisplayName, null, "the cover's far edge");
        // Playtest 4 (the veteran: "the pry tool stands upright on the middle of the back like the screwdriver"): over a
        // cover it can pry, the pry waits in the seam nearest the cursor, low, its handle out across the seam.
        ToolPickup pry = ToolInHand(ToolType.Pry);
        yield return Seconds(.4f);
        {
            cover.SeamAt(edge, out Vector3 seam, out Vector3 across);
            Vector3 n = cover.Outward;
            float rise = Rise(pry, n);
            float off = pry != null ? Vector3.Distance(pry.transform.position, seam) : 1f;
            float outward = pry != null ? Vector3.Dot(Vector3.ProjectOnPlane(pry.transform.up, n).normalized, across) : -1f;
            Check(pry != null && off < .008f && rise > 20f && rise < 50f && outward > .8f,
                $"the pry waits in the seam at a pry angle, its handle out across it ({off * 1000f:0.0} mm from the seam, {rise:0}° up from the cover)");
            yield return Photo("04b-the-pry-at-the-seam");
        }
        int clacksBefore = Sfx.Requested("cover.clack"), liftsBefore = Sfx.Requested("cover.lift");
        PadHold(RT);
        yield return Until(() => cover.IsPopped || cover.IsRemoved, 2.5f, "held at the edge, the cover creaks and pops");
        yield return LetGo();
        yield return Until(() => cover.InWell && stage.InPlace(cover), 2f, "after the pop it lifts off and flies to the cover well by itself (no drag, no drop)");
        Transform well = stage.PlaceOf(cover);
        Check(well == stage.wellLarge || well == stage.wellSmall, $"it lies in the well ({(well != null ? well.name : "no place")}), outer face up: {Vector3.Dot(cover.transform.TransformDirection(cover.OuterFaceLocal), Vector3.up) > .9f}");
        Check(Sfx.Requested("cover.lift") > liftsBefore && Sfx.Requested("cover.clack") > clacksBefore, "its lift (cover.lift) and its landing (cover.clack) sounded");
        Check(cover.IsRemoved && job.HasDetachedComponent<RemovablePart>() && InView(cover.transform), "the device is open, and the cover is in view");
        Check(!HasBody(cover.gameObject), "the cover was never a rigidbody");
        Check(screws.All(t => t.GetComponent<Screw>().Socket != null && !t.GetComponent<Screw>().Socket.gameObject.activeInHierarchy),
            "the cover off, its holes go with it: none floats over the open device (19 Oct)");
        yield return Photo("06-cover-in-the-well");

        // ---------- the front, the tweezers, the broken screen (to the old-parts tray by itself) ----------
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.8f);
        Check(Vector3.Dot(Face(), Vector3.up) > .99f, "Y flips it back onto its front");
        yield return SelectTool(ToolType.Tweezers);
        LoosePart fresh0 = part.Fresh;
        Check(fresh0 != null && (stage.PlaceOf(fresh0) == stage.padSmall || stage.PlaceOf(fresh0) == stage.padLarge) && InView(fresh0.transform),
            $"the fresh screen has been waiting on the new-part pad since the close-up opened ({(fresh0 != null && stage.PlaceOf(fresh0) != null ? stage.PlaceOf(fresh0).name : "no place")}), in view");
        yield return Hover(BenchInteractable.BoundsCentre(part.transform), part.DisplayName, null, "the broken screen");
        int partDropsBefore = Sfx.Requested("part.drop");
        PadHold(RT);
        yield return Until(() => part.State != ReplaceablePart.PartState.InPlace, 1.5f, "held, the tweezers pinch and it lifts out");
        yield return Seconds(.1f);
        yield return LetGo();
        yield return Until(() => part.State == ReplaceablePart.PartState.Removed && stage.InPlace(part), 2f, "it flies into the old-parts tray by itself, button down or not");
        Check(stage.PlaceOf(part) != null && stage.PlaceOf(part).IsChildOf(stage.Tray), $"it lies in the old-parts tray ({(stage.PlaceOf(part) != null ? stage.PlaceOf(part).name : "nowhere")})");
        Check(Sfx.Requested("part.drop") > partDropsBefore, "it landed with part.drop");
        Check(job.Quality < .01f && !job.CanHandBack, "the grade doesn't move for taking the broken part out, and with a hole in it the device can't be handed back");
        Check(InView(part.transform) && !part.CanInteract && !part.Grabbable, "the broken screen is in view in the tray, and finished with: it can't be picked up (§3: scrap can't be moved)");
        Check(!HasBody(part.gameObject), "it was never a rigidbody");
        yield return Photo("07-broken-screen-in-the-tray");

        // ---------- the fresh screen: a click sends it over to hover above its seat; a hold presses it in ----------
        LoosePart fresh = part.Fresh;
        if (fresh == null) { Check(false, "the fresh screen is there"); yield break; }
        yield return Seconds(.45f);   // the glow waits 0.3 s after a tick
        Check(BenchGlow.IsGlowing(fresh) && BenchGlow.GuideOuterNow.r > .9f && BenchGlow.GuideOuterNow.b < .6f,
            $"the fresh screen waiting on its pad wears the next-thing glow, amber over ink (19 Oct: the mint never showed on the pale pad; glowing {BenchGlow.Count})");
        Check(fresh.DisplayName == "New screen", $"the fresh part is named by the part, not the fault: \"{fresh.DisplayName}\" (19 Oct)");
        yield return Hover(BenchInteractable.BoundsCentre(fresh.transform), fresh.DisplayName, null, "the fresh screen on its pad");
        int freshBefore = Sfx.Requested("part.fresh"), replacesBefore = Sfx.Requested("part.replace");
        PadHold(RT);
        yield return Frames(3);
        yield return LetGo();
        Check(Sfx.Requested("part.fresh") > freshBefore, "a click on it (3 frames) sends it over at once (part.fresh)");
        yield return Until(() => fresh.Hovering, 1.5f, "let go before it arrives, it completes its flight and hovers over its seat");
        yield return Seconds(.6f);
        Check(fresh.Hovering && !part.IsReplaced, "and waits there, hovering, until it is held (no drop anywhere)");
        yield return Photo("07b-the-fresh-screen-hovering");
        int finishesBefore = Juice.Finishes, wakesBefore = Juice.Wakes;
        float wordsBefore = Juice.LastWordsAt;
        yield return Hover(BenchInteractable.BoundsCentre(fresh.transform), fresh.DisplayName, null, "the hovering screen");
        float holdAt = Time.realtimeSinceStartup;
        PadHold(RT);
        yield return Until(() => part.IsReplaced, 1.5f, "held on, it presses down and snaps in");
        float pressedIn = Time.realtimeSinceStartup - holdAt;
        yield return LetGo();
        Check(pressedIn < .9f && Sfx.Requested("part.replace") > replacesBefore, $"the press-in took {pressedIn:0.00} s (0.35 s by its clock) and snapped (part.replace)");
        Check(job.Quality >= .999f && job.Grade == JobGrade.Perfect, $"the screen is replaced: {job.Grade}");
        Check(!job.IsComplete && !job.CanHandBack, "but it isn't finished: the cover is in its well and the screws are out");
        Check(job.LooseParts.Contains(part.gameObject) && InView(part.transform), "the broken one stays in the tray as scrap, in view");
        // Playtest 4, one ending: the last task is done, so the phone WAKES now (its new screen lights), quietly; "Fixed!"
        // waits for the cover and the screws.
        DeviceWake wake = device.GetComponent<DeviceWake>();
        yield return Await(() => wake != null && wake.Woken, 1f);
        yield return Seconds(1.1f);
        Check(wake != null && wake.Woken && Juice.Wakes == wakesBefore + 1 && wake.LitScreen != null && wake.LitScreen.gameObject.activeInHierarchy
              && wake.LitScreen.sharedMaterial.IsKeywordEnabled("_EMISSION"),
            $"the last task done (the seat), the phone wakes: the fresh screen lights up ({(wake == null ? "no DeviceWake on the phone: run Bench 4" : wake.LitScreen == null ? "no screen lit" : wake.LitScreen.name + " glows")})");
        Check(Juice.Finishes == finishesBefore && Juice.LastWordsAt == wordsBefore,
            $"quietly: no \"Fixed!\" and no words while its cover is off and its screws are out (endings so far: {Juice.Finishes - finishesBefore})");
        yield return Photo("08-the-phone-wakes");

        // ---------- the cover home: one press on it in its well ----------
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.8f);
        int onsBefore = Sfx.Requested("part.on");
        yield return Hover(BenchInteractable.BoundsCentre(cover.transform), cover.DisplayName, null, "the cover in its well");
        PadHold(RT);
        yield return Frames(3);
        yield return LetGo();
        yield return Until(() => cover.State == RemovablePart.CoverState.Seated, 1.5f, "one press on it (nothing of this job left under it) sends it home: it flies to its seat and clips on");
        Check(!job.HasDetachedComponent<RemovablePart>() && Sfx.Requested("part.on") > onsBefore, "the cover is counted as back on, with its clip (part.on)");
        yield return Photo("09-cover-home");

        // ---------- the screws home: each hole held fetches its screw from its cup ----------
        yield return SelectTool(ToolType.Screwdriver);
        bool backUpAtTheEnd = Vector3.Dot(Face(), Vector3.up) < -.99f;   // it lies on its face for the screws: the ending's flip is from here
        foreach (ScrewTarget t in screws)
        {
            Screw s = t.GetComponent<Screw>();
            if (!s.IsOut || s.Socket == null) continue;
            Transform cup = stage.PlaceOf(s);
            int fetchesBefore = Sfx.Requested("screw.fetch");
            yield return Hover(s.Socket.WorkPoint, "Screw hole", null, "an empty hole");
            PadHold(RT);
            yield return Until(() => s.Fetched || !s.IsOut, 2f, $"held, the driver fetches the screw from its cup ({(cup != null ? cup.name : "no cup")}) to hover at its hole");
            yield return Until(() => !s.IsOut, 3f, "and the same hold screws it home");
            yield return LetGo();
            Check(Vector3.Distance(s.transform.position, s.HomePosition) < .001f && !s.Socket.gameObject.activeSelf && Sfx.Requested("screw.fetch") > fetchesBefore,
                "it sits in its hole, the hole's mark gone (screw.fetch on the way)");
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
        Check(InView(part.transform) && part.DisplayName.StartsWith("Old ") && !part.Grabbable,
            "the old screen is still there in the tray, scrap, finished with");
        yield return Photo("10-fixed-once");

        // Playtest 4 (both playtesters: "Back together · Perfect" sat over "Screw: Hold to unscrew"): a finished phone offers
        // nothing to undo. Its screw can't be worked, and the driver held on it does nothing.
        yield return PadPress(GamepadButton.North);
        yield return Seconds(.9f);
        Check(Vector3.Dot(Face(), Vector3.up) < -.99f, "Y turns the finished phone back over to look at its screws");
        ScrewTarget seated = screws.FirstOrDefault(t => OnPx(t.WorkPoint));
        Check(seated != null, "a screw of the finished phone is in view");
        if (seated != null)
        {
            Screw ss = seated.GetComponent<Screw>();
            yield return Hover(seated.WorkPoint, "Screw", null, "a screw of the finished phone (no verb)");
            Check(seated.Done && !seated.CanInteract, "a finished phone offers nothing to undo: its screw can't be worked");
            PadHold(RT);
            yield return Seconds(.5f);
            yield return LetGo();
            Check(!ss.IsOut && ss.Progress < .01f && inspector.HoldTarget == null,
                $"and the driver held on it doesn't turn it ({ss.Progress:P0} out)");
            yield return Photo("10b-nothing-to-undo");
        }

        // ---------- the clock, the guard, the sounds ----------
        Check(DayClock.Instance.SecondsIntoDay > clockBefore + 5f, $"the clock ran the whole time ({DayClock.Instance.SecondsIntoDay - clockBefore:0} s of the day)");
        Check(BenchStage.GuardTrips == guardTripsBefore, $"nothing ever had to be set back in its place: {BenchStage.GuardTrips - guardTripsBefore} guard trips (the v2 catch is a guard now: under this design it can't trip)");
        Check(!job.GetComponentsInChildren<Rigidbody>(true).Any() && !screws.Any(t => HasBody(t.gameObject)), "no bench piece held a rigidbody at any point");
        string[] cues = { "screw.turn", "screw.free", "screw.fly", "screw.drop", "screw.fetch", "screw.seat", "cover.creak", "cover.pop", "cover.lift",
                          "cover.clack", "part.on", "part.pinch", "part.lift", "part.drop", "part.fresh", "part.replace", "tool.pick", "tool.down", "device.snap", "device.flip",
                          "grime.scrub", "grime.clean", "cloth.squeak", "cloth.gleam", "repair.fixed", "repair.ready", "phone.boot", "watch.wind",
                          "watch.tick", "camera.wind", "camera.shutter", "juice.sparkle", "juice.sparkle.big" };
        string[] silent = cues.Where(c => !PlaceholderSounds.Has(c)).ToArray();
        Note(silent.Length == 0 ? "every bench cue has at least a placeholder sound" : "cues with no placeholder yet (the bank's files bring them, Audio §6): " + string.Join(", ", silent));
        Check(Sfx.Play("screw.drop", stage.Tray.position), "a bench cue plays");

        // ---------- close it anyway: on a fresh phone with its work still inside, a press does nothing; a 0.4 s hold closes it ----------
        yield return CloseAnyway();

        // ---------- out ----------
        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem && Quaternion.Angle(device.transform.rotation, benchPose) < 1f, "stepping back puts the device down as it lay");

        yield return ActTwo();
        yield return ActThree();
    }

    // ---------- act two (the forgiving bench, 14 Oct): the dirty phone, by direct grab ----------
    //
    // The brush taken by a press on it at rest and dragged onto a spot, let go over the phone: still in the hand. A stroke
    // takes the dirt off only where it passed; four strokes at most clean a spot (the pass-based erase and the 90 % finisher;
    // a slow stroke takes as much as a quick one); the rub's speed s rises with the cursor's and the loop follows it; the
    // phone budges along the stroke (6 px at most) and is back within 0.3 s. Its last spot clean, the brush flies home by
    // itself. "What's left" lights the smudge gold, then lets it go. The cloth by RB (the pad's way): the smudge smears first,
    // then comes clean; it is the phone's last task, so the gleam, the wake and the one ending come, and the cloth flies home
    // by itself, the press spent until the trigger is let go.

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

        // ---- the brush by direct grab: a press on it at rest, dragged onto a spot, let go over the phone ----
        ToolPickup brush = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.tool == ToolType.Brush && !t.IsStandIn);
        Check(brush != null, "the modelled brush lies on the tool rest");
        if (brush == null) yield break;
        Check(BenchGlow.GlowingTool == brush && BenchGlow.SeatRinged,
            $"the brush is next: it glows on the rest and its seat is ringed on the screen (19 Oct: a tray's walls hid the cloth's line; glowing {(BenchGlow.GlowingTool != null ? BenchGlow.GlowingTool.displayName : "no tool")}, seat ringed {BenchGlow.SeatRinged})");
        Vector3 brushAt = brush.transform.position + brush.transform.rotation * brush.RestBox.center;
        int picksBefore = Sfx.Requested("tool.pick");
        yield return Hover(brushAt, brush.displayName, "", "the brush at rest (its name, nothing more)");
        // The spot nearest the brush is brushed first, so the drag to it crosses no other.
        GrimeSpot first = (Px(grimes[0].Centre) - Px(brushAt)).sqrMagnitude <= (Px(grimes[1].Centre) - Px(brushAt)).sqrMagnitude ? grimes[0] : grimes[1];
        GrimeSpot other = first == grimes[0] ? grimes[1] : grimes[0];
        Vector3 c0 = first.Centre;
        float half = Mathf.Abs(first.transform.lossyScale.x) * .5f;
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, first.Normal).normalized;
        Vector3 fwd = Vector3.Cross(first.Normal, right).normalized;
        PadHold(RT);
        yield return Frames(2);
        Check(inspector.CurrentTool == ToolType.Brush && brush.InHand && Sfx.Requested("tool.pick") > picksBefore,
            "a press on the brush puts it in the hand on that press (tool.pick)");
        Vector3 towardBrush = Vector3.ProjectOnPlane(brushAt - c0, first.Normal).normalized;
        yield return Glide(c0 + towardBrush * half * 1.6f, .6f);          // dragged over to just short of the spot, the trigger still held
        yield return LetGo();
        yield return Frames(3);
        Check(inspector.CurrentTool == ToolType.Brush && brush.InHand && !brush.Returning,
            "let go over the phone, the brush stays in the hand (the grab rule)");
        yield return Hover(c0, first.PartName, "", "a spot of grime under the brush (its part's name, no \"Hold to\", no percentage)");

        // One stroke across it, slowly: some dirt off where the bristles passed, none off the others; s low.
        int puffsBefore = Juice.DustPuffs;
        float slowS = 0f, slowLoud = 0f, fastS = 0f, fastLoud = 0f, budgeMax = 0f;
        int slowFrames = 0, fastFrames = 0;
        bool loopWhilePressed = false;
        PadHold(RT);
        yield return Sweep(c0 - right * half * 1.1f - fwd * half * .35f, c0 + right * half * 1.1f - fwd * half * .35f, .002f,
            () => { if (inspector.Rubbing) { slowS += BenchFeel.StrokeSpeed; slowLoud += BenchFeel.RubLoudness; slowFrames++; loopWhilePressed |= BenchFeel.RubPlaying; } budgeMax = Mathf.Max(budgeMax, BudgePx()); });
        float partial = first.Progress;
        Check(partial > .05f && partial < .9f && first.Passes >= 1, $"one stroke across it takes some of the dirt off, where the bristles passed ({partial:P0} clean)");
        Check(other.Progress <= 0f && smudge.Progress <= 0f, "and none off the spots it didn't touch");
        Check(Juice.DustPuffs > puffsBefore, $"dust puffs up where the brush lifted some ({Juice.DustPuffs - puffsBefore} puffs)");
        yield return Photo("12-one-stroke");

        // The strokes after it, quickly, back and forth over the rest: four in all clean it (the 90 % finisher sees it done).
        float[] across = { .35f, 0f, -.15f };
        for (int k = 0; k < across.Length && !Gone(first); k++)
        {
            Vector3 a = c0 - right * half * 1.1f + fwd * half * across[k], b = c0 + right * half * 1.1f + fwd * half * across[k];
            yield return Sweep(k % 2 == 0 ? b : a, k % 2 == 0 ? a : b, .008f,
                () => { if (inspector.Rubbing) { fastS += BenchFeel.StrokeSpeed; fastLoud += BenchFeel.RubLoudness; fastFrames++; loopWhilePressed |= BenchFeel.RubPlaying; } budgeMax = Mathf.Max(budgeMax, BudgePx()); });
        }
        int firstPasses = first != null ? first.Passes : -1;
        yield return Await(() => Gone(first), .6f);
        Check(Gone(first), $"four strokes at most clean it: the press pass and the 90 % finisher ({(first != null ? $"{first.Progress:P0} after {first.Passes} passes; left: {first.Remaining()}" : "gone")})");
        Check(Sfx.Requested("grime.scrub") > 0 && Sfx.Requested("grime.clean") > 0, "the brush hissed where dust came off, and the spot chimed clean");
        slowS /= Mathf.Max(1, slowFrames); slowLoud /= Mathf.Max(1, slowFrames);
        fastS /= Mathf.Max(1, fastFrames); fastLoud /= Mathf.Max(1, fastFrames);
        Check(slowFrames > 0 && fastFrames > 0 && fastS > slowS * 1.5f && fastLoud >= slowLoud,
            $"s follows the hand: {slowS:0} px/s on the slow stroke, {fastS:0} on the quick ones; the rub loop's volume with it ({slowLoud:0.00} → {fastLoud:0.00})");
        if (PlaceholderSounds.Has("brush.rub") || loopWhilePressed) Check(loopWhilePressed, "the brush's rub loop played while it was pressed on the dirt");
        else Note("brush.rub has no sound yet (the bank's files bring it): the loop is asked for and silent");
        Check(budgeMax <= 6.5f, $"the phone budged along the stroke, {budgeMax:0.0} px at most (6 px: BenchFeel)");
        yield return LetGo();
        yield return Seconds(.18f);
        Check(!BenchFeel.RubPlaying, "let go, the rub loop is gone within 0.15 s");
        yield return Seconds(.15f);
        Check(BudgePx() < .5f, $"and the budge is back to nothing within 0.3 s ({BudgePx():0.00} px)");
        Note($"the first spot took {firstPasses} passes (a pass: each contact and each turn of the stroke)");

        // The other spot, by the brush too: its last dirt spot clean, the brush flies home by itself.
        Vector3 c1 = other.Centre;
        float half1 = Mathf.Abs(other.transform.lossyScale.x) * .5f;
        int downsBefore = Sfx.Requested("tool.down");
        PadHold(RT);
        for (int k = 0; k < 6 && !Gone(other); k++)
        {
            float off = (k % 3 - 1) * .35f;
            Vector3 a = c1 - right * half1 * 1.1f + fwd * half1 * off, b = c1 + right * half1 * 1.1f + fwd * half1 * off;
            yield return Sweep(k % 2 == 0 ? a : b, k % 2 == 0 ? b : a, .008f, null);
        }
        yield return Await(() => Gone(other), .6f);
        Check(Gone(other), "the other spot comes clean the same way");
        yield return Await(() => !brush.InHand && !brush.Returning, 1f);
        Pose home = brush.HomePose;
        Check(!brush.InHand && inspector.CurrentTool == ToolType.Hand && (brush.transform.position - home.position).magnitude < .001f && Sfx.Requested("tool.down") > downsBefore,
            $"its last dirt spot clean, the brush lifted and flew home by itself, landing with tool.down ({(brush.transform.position - home.position).magnitude * 1000f:0.0} mm from its seat)");
        yield return LetGo();

        // ---- "what's left" (§4.5): the smudge lights gold, then lets go ----
        int flashesBefore = BenchWork.Flashes, shimmersBefore = Sfx.Requested("bench.showleft");
        BenchWork.Flash(job);
        yield return Seconds(.25f);
        Check(smudge.Flashed > 1f && BenchWork.Flashes == flashesBefore + 1 && Sfx.Requested("bench.showleft") > shimmersBefore,
            $"\"what's left\" lights the smudge gold ({smudge.Flashed:0.00} of 1.5) with its shimmer");
        yield return Photo("12b-whats-left");
        yield return Seconds(1.4f);
        Check(smudge.Flashed <= 0f, "and lets it go after 1.5 s");

        // ---- the cloth, the pad's way (RB): smear first, then clean; the last task ----
        int finishesBefore = Juice.Finishes, wakesBefore = Juice.Wakes;
        DeviceWake wake = device.GetComponent<DeviceWake>();
        Vector3 cs = smudge.Centre;
        yield return Hover(cs, smudge.PartName, "", "the smudge with bare hands (its part's name only)");
        yield return SelectTool(ToolType.Cloth);
        ToolPickup cloth = ToolInHand(ToolType.Cloth);
        Check(cloth != null && !cloth.IsStandIn, "the cloth is in the hand (RB)");
        if (cloth == null) yield break;
        float halfS = Mathf.Abs(smudge.transform.lossyScale.x) * .5f;
        Vector3 rs = Vector3.ProjectOnPlane(cam.transform.right, smudge.Normal).normalized, fs = Vector3.Cross(smudge.Normal, rs).normalized;
        int gleamsBefore = Juice.Gleams, downsCloth = Sfx.Requested("tool.down");
        // One true pass: the cursor to the stroke's start with the trigger up, then held across (15 Oct: pressed at the smudge's
        // middle and gliding out to the start, the hand made a pass and a half, and 71 % came off).
        Vector3 passFrom = cs - rs * halfS * 1.1f, passTo = cs + rs * halfS * 1.1f;
        yield return Glide(passFrom, .3f);
        PadHold(RT);
        yield return Sweep(passFrom, passTo, .006f, null);
        float smeared = smudge.Progress;
        float flat = Vector3.Angle(cloth.transform.up, smudge.Normal), over = Vector3.Dot(cloth.transform.position - cs, smudge.Normal);
        Check(cloth.InHand && flat < 8f && over > -.002f && over < .004f && (cloth.PressShown > .8f || cloth.transform.Find("BT_ClothHeld") == null),
            $"wiping, the cloth lies flat on the glass, pressed ({flat:0.0}° off, {over * 1000f:0.0} mm over it; Press {cloth.PressShown:0.00})");
        // BenchRules' smear first: one full pass takes 0.8 x (0.15 x 0.5 + 0.5) = 46 % at the footprint's core, where a pass
        // without the smear would take 80 %; so under half means the grease smeared first (the old 30 % bar was under even
        // the rules' own first pass).
        Check(smeared > 0f && smeared < .5f, $"the first pass smears the grease more than it lifts it ({smeared:P0} off; a pass without the smear takes 80 %)");
        yield return Photo("13a-the-cloth-wiping");
        for (int k = 0; k < 8 && !Gone(smudge); k++)
        {
            float off = ((k % 4) - 1.5f) * .3f;
            Vector3 a = cs - rs * halfS * 1.1f + fs * halfS * off, b = cs + rs * halfS * 1.1f + fs * halfS * off;
            yield return Sweep(k % 2 == 0 ? b : a, k % 2 == 0 ? a : b, .008f, null);
        }
        bool cleaned = Gone(smudge);
        Check(cleaned, "wiped back and forth, the smudge thins to haze and comes clean" + (cleaned ? "" : $" (only {(smudge != null ? smudge.Progress : 0f):P0})"));
        if (Sfx.Requested("cloth.squeak") > 0) Note("the cloth squeaked at a turn while grease came off");
        else Note("no squeak heard (it needs a turn at 250 px/s or more while grease comes off: the glides may be too slow)");
        yield return Await(() => !cloth.InHand && !cloth.Returning, 1f);
        Check(!cloth.InHand && inspector.CurrentTool == ToolType.Hand && Sfx.Requested("tool.down") > downsCloth,
            "the smudge clean, the cloth lifts off and flies home by itself (no lift to come back from: §4.4.6)");
        Check(inspector.ToolInHand == null && !inspector.Rubbing, "and the trigger still held does nothing until it's let go (the press is spent)");
        yield return LetGo();
        yield return Await(() => Juice.Finishes > finishesBefore, 2f);
        Check(Juice.Gleams > gleamsBefore && Sfx.Requested("cloth.gleam") > 0, "a gleam swept the glass it had cleared, on the frame it came clean");
        Check(wake != null && wake.Woken && Juice.Wakes == wakesBefore + 1 && Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect" && job.IsFinished,
            $"the smudge was its last task: the phone wakes and it ends once, \"Fixed! · Perfect\" (words: \"{Juice.LastWords}\")");
        yield return Photo("14-clean-and-lit");

        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem, "stepping back puts it down");
    }

    // The device's budge now, in 1080p pixels on screen (BenchFeel adds it in ItemInspector.Pose).
    float BudgePx()
    {
        if (device == null || cam == null) return 0f;
        Vector3 at = device.transform.position;
        Vector2 a = cam.WorldToScreenPoint(at), b = cam.WorldToScreenPoint(at - BenchFeel.BudgeOffset);
        return (a - b).magnitude * 1080f / Mathf.Max(1, UnityEngine.Screen.height);
    }

    // ---------- act three: Grace's camera, by its verbs (the forgiving bench, §6) ----------
    //
    // Day 1's camera (fault 0): its lens wiped with the cloth and its film path and guide brushed, by the pad's own hand, each
    // tool home by itself after its last spot; Day 2's (fault 1): the jammed shutter pinched out to the old-parts tray, the clean
    // one held from its pad: it flies over and presses in. One "Fixed! · Perfect" each, the scratched strap kept. (Before the
    // days builder's split the camera has one fault: the clean and the swap are then one job, and one ending.)

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
        DeviceDefinition definitionOnPrefab = cameraPrefab.GetComponent<DeviceDefinition>();
        int faults = definitionOnPrefab != null && definitionOnPrefab.faults != null ? definitionOnPrefab.faults.Length : 0;
        yield return GraceCamera(cameraPrefab, 0, faults >= 2 ? "Day 1's clean" : "the clean and the swap (one fault: the days builder's split isn't in yet)");
        if (faults >= 2) yield return GraceCamera(cameraPrefab, 1, "Day 2's swap");
    }

    IEnumerator GraceCamera(GameObject cameraPrefab, int faultIndex, string what)
    {
        if (inspector.IsHoldingItem) { inspector.CancelInspection(); yield return AfterBlend(); }
        if (device != null)
        {
            foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
            made.Remove(device);
            Destroy(device);
            yield return Frames(2);
        }
        device = Instantiate(cameraPrefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = $"Bench check Grace camera ({what})";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        var grace = job as GraceCameraRepairJob;
        DeviceFault fault = definition != null ? definition.GetFault(faultIndex) : null;
        Check(definition != null && grace != null && fault != null, $"{what}: Grace's camera has its definition, its job and the fault");
        if (definition == null || grace == null || fault == null) yield break;
        definition.ApplyFault(faultIndex);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = cameraPrefab, deviceName = definition.displayName, faultIndex = faultIndex,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 97 - faultIndex, color = Color.white
        });
        yield return Frames(2);
        GrimeSpot[] spots = job.GetComponentsInChildren<GrimeSpot>();
        ReplaceablePart shutter = grace.Shutter != null && grace.Shutter.gameObject.activeInHierarchy ? grace.Shutter : null;
        Note($"{what}: {spots.Length} spots, {(shutter != null ? "the jammed shutter on" : "no shutter to swap")} (\"{fault.description}\")");
        Check(spots.Length > 0 || shutter != null, $"{what}: there is work on it");
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
        yield return Photo($"15-grace-camera-{faultIndex}-on-the-mat");
        int finishesBefore = Juice.Finishes;

        // The spots, in the card's order, by the pad's own hand.
        for (int guard = 0; guard < 8; guard++)
        {
            RepairChecklist.Step now = inspector.CurrentStep;
            GrimeSpot next = now != null && now.targets != null ? now.targets.OfType<GrimeSpot>().FirstOrDefault(g => !Gone(g)) : null;
            if (next == null) next = spots.FirstOrDefault(g => !Gone(g));
            if (next == null) break;
            yield return CleanByHand(next);
        }
        Check(spots.All(Gone), $"{what}: every spot is clean, by the cloth and the brush in the hand");

        // The swap: the jammed shutter out to the tray, the clean one in with one hold.
        if (shutter != null)
        {
            BenchStage stage = BenchStage.Instance;
            yield return SelectTool(ToolType.Tweezers);
            yield return Hover(BenchInteractable.BoundsCentre(shutter.transform), shutter.DisplayName, null, "the jammed shutter");
            PadHold(RT);
            yield return Until(() => shutter.State != ReplaceablePart.PartState.InPlace, 1.5f, "held, the tweezers pinch the jammed shutter out");
            yield return LetGo();
            yield return Until(() => shutter.State == ReplaceablePart.PartState.Removed && stage != null && stage.InPlace(shutter), 2f, "it flies into the old-parts tray by itself");
            LoosePart fresh = shutter.Fresh;
            Check(fresh != null && stage != null && stage.PlaceOf(fresh) != null, "the clean shutter waits on the new-part pad");
            if (fresh != null)
            {
                yield return Hover(BenchInteractable.BoundsCentre(fresh.transform), fresh.DisplayName, null, "the clean shutter on its pad");
                PadHold(RT);
                yield return Until(() => shutter.IsReplaced, 2.5f, "one hold: it flies over its seat, hovers and presses in");
                yield return LetGo();
            }
        }

        yield return Await(() => Juice.Finishes > finishesBefore, 3f);
        yield return Seconds(.8f);
        Check(Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect" && job.IsFinished && job.Grade == JobGrade.Perfect,
            $"{what}: it ends once, \"Fixed! · Perfect\" (words: \"{Juice.LastWords}\", endings: {Juice.Finishes - finishesBefore})");
        Transform strap = device.transform.Find("KEEP - scratched sentimental strap");
        Check(strap != null && strap.gameObject.activeInHierarchy, $"{what}: its scratched strap is still on it, kept");
        yield return Photo($"16-grace-camera-{faultIndex}-fixed");
        inspector.CancelInspection();
        yield return AfterBlend();
        Check(!inspector.IsHoldingItem, "stepping back puts it down");
    }

    // A spot cleaned the way a pad player does it: its tool by RB, the cursor onto it, the trigger held, strokes across it.
    IEnumerator CleanByHand(GrimeSpot spot)
    {
        if (Gone(spot)) yield break;
        yield return SelectTool(spot.Tool);
        if (Gone(spot)) yield break;
        string name = spot.PartName;
        Vector3 c = spot.Centre;
        float half = Mathf.Abs(spot.transform.lossyScale.x) * .5f;
        Vector3 r = Vector3.ProjectOnPlane(cam.transform.right, spot.Normal).normalized, f = Vector3.Cross(spot.Normal, r).normalized;
        PadHold(RT);
        for (int k = 0; k < 8 && !Gone(spot); k++)
        {
            float off = ((k % 4) - 1.5f) * .3f;
            Vector3 a = c - r * half * 1.1f + f * half * off, b = c + r * half * 1.1f + f * half * off;
            yield return Sweep(k % 2 == 0 ? a : b, k % 2 == 0 ? b : a, .008f, null);
        }
        yield return LetGo();
        yield return Await(() => Gone(spot), .6f);
        Check(Gone(spot), $"{name}: cleaned by the {Name(spot.Tool)} in strokes");
    }

    // The cursor glided from one point on the device to another with the button held: a stroke of the tool between them.
    // A stroke of the tool from one point to another, as a hand would make it: in steps of about a centimetre, a frame or
    // two each, so the cursor is ON the spot at frame boundaries whatever the frame rate. A glide is 0.03 s (PadCursor), one
    // frame with Unity behind other windows: the cursor then jumps from a stroke's start to its end in that frame, and a
    // stroke whose ends lie off the spot's disc (the raster's outermost rows start and end beyond its edge) never has the
    // cursor over the spot at any frame, so nothing is scrubbed along it (13 Oct: 117 of 125 on 9 Oct and 164 of 172 today,
    // the same eight, the spot left at 90-96 % along one side; 125/125 and 161/161 with Unity in front). The game is not
    // the matter: a player's cursor crosses a spot over many frames.
    IEnumerator Sweep(Vector3 from, Vector3 to) => Sweep(from, to, .008f, null);

    // ...in steps of `step` metres (smaller: a slower hand), with `watch` called every frame of it.
    IEnumerator Sweep(Vector3 from, Vector3 to, float step, Action watch)
    {
        yield return Glide(from, .3f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / Mathf.Max(.001f, step)), 2, 40);
        for (int i = 1; i <= steps; i++)
        {
            PadCursor.GlideTo(Px(Vector3.Lerp(from, to, i / (float)steps)));
            float until = Time.realtimeSinceStartup + .1f;
            for (int f = 0; f < 2 || PadCursor.Gliding && Time.realtimeSinceStartup < until; f++) { watch?.Invoke(); yield return null; }
        }
    }

    IEnumerator Glide(Vector3 world, float seconds)
    {
        PadCursor.GlideTo(Px(world));
        yield return Frames(2);
        yield return Await(() => !PadCursor.Gliding, seconds);
    }

    static bool Gone(GrimeSpot spot) => spot == null || spot.IsClean;

    // A freed screw: it pops and flies to its own cup, lands with its tink on the flight's last frame, in view, never a rigidbody.
    IEnumerator ScrewLands(Screw s, string what)
    {
        BenchStage stage = BenchStage.Instance;
        bool flew = false;
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !(s.InCup && stage.InPlace(s)))
        {
            flew |= s.Flying;
            if (HasBody(s.gameObject)) { Check(false, what + " became a rigidbody"); yield break; }
            yield return null;
        }
        Transform cup = stage.PlaceOf(s);
        Check(s.InCup && cup != null && cup.name.StartsWith("Cup_"), $"{what}: free, it pops and flies to its own cup ({(cup != null ? cup.name : "no cup")}){(flew ? "" : " (the flight wasn't seen)")}");
        Check(InView(s.transform), $"...where it lies in view ({Vector3.Distance(s.transform.position, cup != null ? cup.position : s.transform.position) * 1000f:0.0} mm from the cup's middle)");
    }

    // A fresh phone, opened by the check's hand: with its screen still to replace, a press on its cover in the well does nothing,
    // and a 0.4 s hold closes it anyway (§4.2: a stray click can't close the device over unfinished work).
    IEnumerator CloseAnyway()
    {
        if (inspector.IsHoldingItem) { inspector.CancelInspection(); yield return AfterBlend(); }
        foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        made.Remove(device);
        Destroy(device);
        yield return Frames(2);
        device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = "Bench check phone (close anyway)";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        definition.ApplyFault(0);
        DeviceFault fault = definition.GetFault(0);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = 0,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 96, color = Color.white
        });
        yield return Frames(2);
        Check(carry.TryPickUp(job), "a fresh phone: Ace takes it");
        bench.Interact(interactor);
        yield return Seconds(.4f);
        Check(inspector.BeginInspection(job), "the close-up opens on it");
        if (!inspector.IsHoldingItem) yield break;
        yield return AfterBlend();
        RemovablePart cover = job.GetComponentInChildren<RemovablePart>();
        foreach (Screw s in cover.HeldBy) s.Unscrew(null);                 // the check's hand: screws out, cover off
        yield return Await(() => cover.HeldBy.All(s => s.InCup), 4f);
        cover.Activate();
        yield return Until(() => cover.InWell, 3f, "(the check's hand: screws out, cover off, in its well)");
        if (Vector3.Dot(Face(), Vector3.up) > 0f) { yield return PadPress(GamepadButton.North); yield return Seconds(.9f); }
        Check(cover.WorkBeneathToDo, "its screen is still to replace: work is left under the cover");
        yield return Hover(BenchInteractable.BoundsCentre(cover.transform), cover.DisplayName, null, "the cover in its well, work left inside");
        PadHold(RT);
        yield return Frames(3);
        yield return LetGo();
        yield return Seconds(.6f);
        Check(cover.InWell, "a press (3 frames) does nothing: a stray click can't close it over unfinished work");
        PadHold(RT);
        yield return Until(() => cover.State == RemovablePart.CoverState.Seated, 1.6f, "a 0.4 s hold closes it anyway (and it flies home)");
        yield return LetGo();
        yield return Photo("09b-closed-anyway");
    }

    static bool HasBody(GameObject g) => g != null && g.GetComponentInChildren<Rigidbody>(true) != null;

    // ---------- measuring the tools (playtest 4) ----------

    // The tool's handle (its +Y) up from a surface with normal n, degrees: 0 lying on it, 90 stood on its tip.
    static float Rise(ToolPickup tool, Vector3 n) =>
        tool == null ? 0f : Mathf.Asin(Mathf.Clamp(Vector3.Dot(tool.transform.up, n.normalized), -1f, 1f)) * Mathf.Rad2Deg;

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

    // In view: drawn by the close-up camera, and not sunk into the tray's floor.
    bool InView(Transform t)
    {
        Renderer r = FirstRenderer(t);
        if (r == null || !r.enabled || !t.gameObject.activeInHierarchy || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) return false;
        Vector3 v = cam.WorldToViewportPoint(BenchInteractable.BoundsCentre(t));
        return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
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

    // The mat as drawn, without what lies on it (a part on its pad, a screw in a cup, a tool, an outline hull).
    static Bounds MatOwnBounds(Transform mat)
    {
        Renderer[] own = mat.GetComponentsInChildren<Renderer>()
            .Where(r => r.GetComponentInParent<LoosePart>() == null && r.GetComponentInParent<JobBase>() == null && r.GetComponentInParent<Screw>() == null &&
                        r.GetComponentInParent<ToolPickup>() == null && r.GetComponent<BenchOutlineHull>() == null)
            .ToArray();
        if (own.Length == 0) return RendererBounds(mat);
        Bounds b = own[0].bounds;
        foreach (Renderer r in own) b.Encapsulate(r.bounds);
        return b;
    }

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
        BenchStage stage = BenchStage.Instance;
        Vector2 mm = stage.WorldToMat(p);
        float up = p.y - stage.MatToWorld(Vector2.zero, BenchStage.MatTop).y;
        return Mathf.Abs(mm.x) < 300f && Mathf.Abs(mm.y) < 180f && up > -.005f && up < .04f;
    }

    string Where(Vector3 p)
    {
        BenchStage stage = BenchStage.Instance;
        Vector2 mm = stage.WorldToMat(p);
        return OverMat(p) ? $"on the mat at ({mm.x:0}, {mm.y:0}) mm" : $"off the mat: ({mm.x:0}, {mm.y:0}) mm, {(p.y - stage.MatToWorld(Vector2.zero, BenchStage.MatTop).y) * 1000f:0} mm up";
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

    // 20 Oct: a card line carries its key as an icon wrapped in the card's key colour (BenchCards.K: <color=…><sprite…></color>),
    // so "the key, then the words" is checked line by line: the line that says the words must hold that key's icon.
    static bool LineHas(string rich, string words, string key)
    {
        foreach (string line in (rich ?? "").Split('\n'))
            if (line.Contains(words)) return line.Contains(key);
        return false;
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
