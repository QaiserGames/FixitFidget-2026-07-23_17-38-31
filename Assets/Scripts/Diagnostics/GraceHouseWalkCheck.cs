using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BREAK-INS' WALK CHECK (chunk A: claude/break-ins-spec.md section 12)
//
// Started by Fixit Fidget > Night > Break-ins 3 - Walk Grace's house by itself (lab, a check), in a lab
// session only (the playtest save is never touched). Ace's own capsule walks her house by itself, steered
// the way a player steers it (PlayerMovement's scripted input, through the same code as a key press):
// in at her door, the entry, the pocket by the cupboard, the kitchen to the fridge and back, up both
// flights, the landing, into the bedroom past the foot of the bed to the wardrobe, then all the way back
// down and out onto the pavement. For every leg: reached or not, how long it took, where it got stuck;
// on the way down, how far the capsule ever left the flight. Photos of the house view on the way.
// With Ace's body on (AceBody: the Sidekick Ace, or the stand-in): it runs while Ace runs, stands still at the
// stops, faces the way Ace goes, stands on the floor, and hides in first person; photos of it running on three
// legs. The Sidekick's face blinks; the stand-in's look is worn by nobody else.
// Then sneaking (chunk B, 30 Sept): back on the pavement, Ace sneaks along the longest clear way (1.6 m/s,
// crouched, the crouch walk showing, steps heard within 1 m), stops crouched, looks in first person (the eye
// lower), stands and walks back (5 m/s, steps heard within 4 m).
// Since chunk C (6 Oct 2026) Grace is at home at night: this lab sends her out for the night (GraceHouse.StartLab), so the
// walk meets nobody; her own checks are Break-ins 7.
// Report and photos: Logs/Night/grace-walk-<time>/. Play Mode stops by itself when it is done.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceHouseWalkCheck : MonoBehaviour
{
    // Plan metres (X north, Y to the street) and what the stop is; photo: take one there. The stops follow the
    // walkable space worked out from the pieces' real bounds (the same ways as Break-ins 2's check).
    static readonly (float X, float Y, string what, bool photo)[] Route =
    {
        (1.11f, 5.30f, "on the pavement at her stoop", false),
        (1.11f, 4.05f, "in her doorway", false),
        (1.11f, 3.35f, "in the entry, inside the door", true),
        (2.06f, 3.20f, "past the foot of the stairs", false),
        (2.45f, 2.75f, "in the front room, by her armchair", true),
        (2.22f, 2.08f, "in the pocket, at the cupboard under the stairs", true),
        (3.00f, 2.02f, "round toward the kitchen", false),
        (3.28f, 1.50f, "past the cupboard's corner", false),
        (3.20f, 1.30f, "in the kitchen, at the cups", true),
        (4.65f, 1.30f, "at the fridge", false),
        (3.20f, 1.30f, "back at the cups", false),
        (3.20f, 1.85f, "out of the kitchen", false),
        (2.45f, 2.30f, "back past her armchair", false),
        (2.30f, 2.95f, "back across the front room", false),
        (1.90f, 3.25f, "back in the entry", false),
        (0.95f, 3.30f, "along the entry", false),
        (0.72f, 3.05f, "at the foot of the stairs", false),
        (0.70f, 2.20f, "on the lower flight", false),
        (0.70f, 1.60f, "at the top of the lower flight", false),
        (0.70f, 0.72f, "on the landing", true),
        (1.30f, 0.70f, "at the foot of the upper flight", false),
        (2.20f, 0.70f, "on the upper flight", false),
        (2.95f, 0.72f, "at the top of the stairs", false),
        (3.05f, 0.85f, "on the landing upstairs, by the bathroom door", true),
        (3.05f, 1.55f, "in the bedroom's doorway", false),
        (2.90f, 2.10f, "just inside the bedroom", false),
        (2.80f, 2.40f, "past the corner of the bed", false),
        (2.65f, 2.90f, "in the room", false),
        (2.75f, 3.25f, "at the foot of the bed", true),
        (1.85f, 3.42f, "across the room", false),
        (1.50f, 3.58f, "toward the south bay", false),
        (1.25f, 3.62f, "at the wardrobe and the dressing table", true),
        (1.50f, 3.58f, "back from the wardrobe", false),
        (1.85f, 3.42f, "back across the room", false),
        (2.30f, 3.05f, "back toward the door", false),
        (2.80f, 2.40f, "back past the corner of the bed", false),
        (2.90f, 2.10f, "back at the bedroom's door", false),
        (3.05f, 1.55f, "back through the doorway", false),
        (3.05f, 0.85f, "on the landing upstairs", false),
        (2.95f, 0.72f, "at the top of the stairs", false),
        (2.20f, 0.70f, "going down the upper flight", false),
        (1.20f, 0.70f, "at the bottom of the upper flight", false),
        (0.70f, 0.90f, "on the landing", false),
        (0.70f, 1.80f, "going down the lower flight", false),
        (0.72f, 2.75f, "at the bottom of the stairs", false),
        (1.11f, 3.35f, "in the entry", false),
        (1.11f, 4.05f, "in her doorway, going out", false),
        (1.11f, 5.30f, "on the pavement again", true),
    };
    const float Reach = .22f;          // a stop counts as reached within this (metres, flat)
    const float LegSeconds = 9f;       // longest a leg may take
    const float StuckAfter = 1.2f;     // no progress for this long: stuck

    GraceHouse house;
    PlayerMovement mover;
    CafeViewMode view;
    CharacterController capsule;
    AceBody body;
    string folder;
    readonly StringBuilder report = new();
    int problems;
    // Ace's body, while it is worn: frames running (and of those, showing the run), frames moving
    // (with how far the body faced from the way Ace went), frames with the floor found under Ace.
    int runFrames, runShown, placedFrames, onFloorFrames, pivotFrames;
    float runRates;
    readonly List<float> facing = new();
    readonly List<string> notStill = new();
    static readonly string[] RunPhotos = { "at the fridge", "on the upper flight", "across the room" };

    void Start()
    {
        house = GetComponent<GraceHouse>();
        view = FindAnyObjectByType<CafeViewMode>();
        mover = view != null ? view.GetComponent<PlayerMovement>() : null;
        capsule = view != null ? view.GetComponent<CharacterController>() : null;
        body = view != null ? view.GetComponent<AceBody>() : null;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "grace-walk-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        StartCoroutine(Walk());
    }

    void Line(bool ok, string what)
    {
        if (!ok) problems++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    IEnumerator Walk()
    {
        report.AppendLine("Break-ins walk check: Ace's own capsule walks Grace's house by itself (" +
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
        if (house == null || mover == null || capsule == null)
        {
            Line(false, "Grace's house, Ace and Ace's walking are all there");
            Finish();
            yield break;
        }
        report.AppendLine($"Ace: radius {capsule.radius:0.00}, height {capsule.height:0.00}, skin {capsule.skinWidth:0.00}, step {capsule.stepOffset:0.00}, slope limit {capsule.slopeLimit:0}°.");
        yield return new WaitForSeconds(1.5f);   // the night settles, the camera arrives
        // Since chunk C (6 Oct) Grace is at home at night; this walk runs at a run through every room, so she's out tonight.
        GraceAtHome grace = GraceAtHome.Instance;
        Line(grace == null || grace.Away, grace == null ? "Grace isn't at home in this scene (no GraceAtHome): nobody to meet"
            : $"Grace is out tonight, so the walk meets nobody ({grace.Doing}; {grace.Why})");
        report.AppendLine(body != null && body.Worn
            ? $"Ace's body: {body.BodyName}, about {body.Height:0.00} m tall (the capsule stays {2f * capsule.radius:0.0} m wide)."
            : "Ace's body: not worn (Ace is the capsule).");
        float started = Time.time;
        int photos = 0;
        float worstAir = 0f;
        string worstAirWhere = "";
        for (int i = 0; i < Route.Length; i++)
        {
            var stop = Route[i];
            Vector3 target = house.World(stop.X, stop.Y);
            float legStart = Time.time, bestAt = Time.time;
            float best = Flat(target - Ace).magnitude;
            string stuck = null;
            bool reached = false;
            float air = 0f, legAir = 0f;
            float legLength = best;
            bool runPhoto = Array.IndexOf(RunPhotos, stop.what) >= 0 && body != null && body.Worn;
            while (Time.time - legStart < LegSeconds)
            {
                Vector3 to = Flat(target - Ace);
                float d = to.magnitude;
                if (d <= Reach) { reached = true; break; }
                WatchBody();
                if (runPhoto && d < legLength * .55f)
                {
                    runPhoto = false;
                    string shot = $"{++photos:00}-running-{Safe(stop.what)}.png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, shot));
                    report.AppendLine($"      photo {shot} (Ace's body running, {body.Speed:0.0} m/s, run {body.Gait.z:0.00})");
                }
                if (d < best - .03f) { best = d; bestAt = Time.time; }
                else if (Time.time - bestAt > StuckAfter) { stuck = $"no closer than {best:0.00} m for {StuckAfter:0.0} s at plan {PlanOf(Ace)}"; break; }
                // The way a player steers: a direction on screen, turned by the camera's yaw.
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, view.MovementYaw, 0f)) * (to / d);
                mover.ScriptedInput = new Vector2(local.x, local.z);
                // How far off the ground (the flights going down): time in the air, this leg.
                air = capsule.isGrounded ? 0f : air + Time.deltaTime;
                legAir = Mathf.Max(legAir, air);
                yield return null;
            }
            mover.ScriptedInput = Vector2.zero;
            float took = Time.time - legStart;
            if (legAir > worstAir) { worstAir = legAir; worstAirWhere = stop.what; }
            Line(reached, $"{i + 1,2}. {stop.what}: {(reached ? $"reached in {took:0.0} s" : stuck ?? $"not reached in {LegSeconds:0} s (at plan {PlanOf(Ace)})")}" +
                          $"{(legAir > .05f ? $", in the air {legAir:0.00} s at most" : "")}; " +
                          $"{(house.Viewing ? (house.Upstairs ? "inside, upstairs" : "inside") : "outside")}");
            if (!reached) break;
            // On the stoop: her door opens only once Ace is let in (E, "Let yourself in"; since 30 Sept). The
            // check presses it the way the zone does, and waits for the door to swing open before walking in.
            if (stop.what == "on the pavement at her stoop")
            {
                yield return null;   // the way-in zone's offer settles
                Line(house.AceOnStoop && house.DoorPromptShown && house.DoorPrompt == "Let yourself in",
                     $"    on the stoop the way in is offered (\"{house.DoorPrompt}\"; on the stoop: {house.AceOnStoop}, offered: {house.DoorPromptShown})");
                house.LetAceIn();
                float pressed = Time.time;
                while (house.door != null && !house.door.IsOpen && Time.time - pressed < 2.5f) yield return null;
                Line(house.door == null || house.door.IsOpen, $"    E opened her door ({Time.time - pressed:0.0} s; let in {house.LetIns} time(s))");
            }
            // Through her door on the way in: wait for it to shut behind Ace before the entry's way north.
            if (stop.what == "in the entry, inside the door")
            {
                float wait = Time.time;
                while (house.door != null && !house.door.IsClosed && Time.time - wait < 3f) yield return null;
                Line(house.door == null || house.door.IsClosed, $"    her door shut behind Ace ({Time.time - wait:0.0} s)");
            }
            if (stop.photo)
            {
                yield return new WaitForSeconds(.9f);   // the camera turns, the walls settle
                if (body != null && body.Worn && body.Gait.x < .9f)
                    notStill.Add($"{stop.what} (idle {body.Gait.x:0.00} at {body.Speed:0.00} m/s)");
                string name = $"{++photos:00}-{Safe(stop.what)}.png";
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));
                yield return null;
                report.AppendLine($"      photo {name}");
            }
        }
        mover.ScriptedInput = null;
        report.AppendLine();
        Line(worstAir < .35f, $"Going down, the capsule stayed on the flights (longest in the air {worstAir:0.00} s, {worstAirWhere}; the stairs caught it {house.StairCatches} times)");
        report.AppendLine($"The whole walk took {Time.time - started:0.0} s.");
        yield return SneakCheck();
        if (body != null && body.Worn)
        {
            report.AppendLine();
            yield return BodyReport();
        }
        Finish();
    }

    // ------------------------------------------------------------------ sneaking (chunk B, 30 Sept)

    // From wherever the walk ended (the pavement at her stoop), the longest clear way Ace's capsule could go: sneak
    // along it (crouched, 1.6 m/s, steps heard within 1 m), stop crouched, look in first person (the eye lower), stand,
    // and walk back (5 m/s, steps heard within 4 m). The check's own sneak key is PlayerMovement.ScriptedSneak.
    IEnumerator SneakCheck()
    {
        report.AppendLine();
        report.AppendLine("Sneaking (chunk B):");
        Vector3 way = Vector3.zero;
        float clear = 0f;
        Vector3 middle = capsule.transform.TransformPoint(capsule.center);
        float half = Mathf.Max(0f, capsule.height * .5f - capsule.radius);
        // The capsule, lifted over a kerb (the controller steps up that much), a hair thinner than Ace.
        Vector3 top = middle + Vector3.up * half;
        Vector3 low = middle + Vector3.down * half + Vector3.up * (capsule.stepOffset + .02f);
        if (low.y > top.y) low = top;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            float free = Physics.CapsuleCast(low, top, capsule.radius * .95f, dir, out RaycastHit hit, 8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.distance : 8f;
            if (free > clear) { clear = free; way = dir; }
        }
        Line(clear >= 3.5f, $"a clear way to sneak along, from {PlanOf(Ace)}: {clear:0.0} m (of the 8 ways round)");
        if (clear < 3.5f) { mover.ScriptedSneak = null; yield break; }
        float length = Mathf.Min(clear - .8f, 4f);
        int[] kinds = NightNoise.CountByKind;

        // 1. Sneak along it.
        int sneakBefore = kinds[(int)NoiseKind.SneakStep], stepBefore = kinds[(int)NoiseKind.Step];
        mover.ScriptedSneak = true;
        yield return Leg(way, length, true);
        int sneakSteps = kinds[(int)NoiseKind.SneakStep] - sneakBefore, walkSteps = kinds[(int)NoiseKind.Step] - stepBefore;
        Line(Mathf.Abs(legSpeed - mover.SneakSpeed) <= .2f,
            $"sneaking goes {legSpeed:0.00} m/s once crouched (set: {mover.SneakSpeed:0.0}; walking is {mover.WalkSpeed:0.0}), over {length:0.0} m");
        NightNoise.Recent(0, out NightNoiseEvent last);
        Line(sneakSteps > 0 && walkSteps == 0 && last.kind == NoiseKind.SneakStep && Mathf.Approximately(last.radius, NightNoise.SneakRadius),
            $"sneaking steps are heard within {NightNoise.SneakRadius:0} m: {sneakSteps} sneaking steps, {walkSteps} walking ones (the last noise: {last.kind}, {last.radius:0.0} m)");
        if (body != null && body.Worn)
        {
            if (body.CanCrouch)
            {
                Line(legFrames > 0 && legCrouchShown >= legFrames * .9f,
                    $"Ace's body crouch-walks while sneaking: the crouch walk showing in {legCrouchShown} of {legFrames} frames once crouched, " +
                    $"played at {(legFrames > 0 ? legCrouchRates / legFrames : 0f):0.00}x on average (the feet keep up near 1)");
                Line(legFrames > 0 && legCrouchRates / legFrames >= .6f && legCrouchRates / legFrames <= 1.6f,
                    "the crouch walk plays within 0.6-1.6x of its own speed");
            }
            else
                Line(!body.WearsSidekick,
                    body.WearsSidekick ? "Ace's body can crouch: the Sidekick body has no crouch clips yet (Fixit Fidget > Night > Ace's body 5)"
                                       : "Ace's body: the stand-in has no crouch clips, so it sneaks upright (as designed)");
        }

        // 2. Stop, still crouched.
        yield return new WaitForSeconds(.6f);
        if (body != null && body.Worn && body.CanCrouch)
            Line(body.CrouchGait.x >= .9f, $"stopped, Ace stays crouched and still (crouched still {body.CrouchGait.x:0.00}, crouch walk {body.CrouchGait.y:0.00})");
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "zz-sneak-2-crouched-still.png"));
        yield return null;
        report.AppendLine("      photos zz-sneak-1-sneaking.png, zz-sneak-2-crouched-still.png");

        // 3. First person, crouched and then standing: the eye drops with the crouch.
        bool switched = view.SetFirstPerson(true);
        yield return new WaitForSeconds(.5f);
        float eyeCrouched = view.firstPersonCamera != null ? view.firstPersonCamera.transform.position.y - view.AceFeet.y : 0f;
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "zz-sneak-3-first-person-crouched.png"));
        yield return null;
        mover.ScriptedSneak = false;
        yield return new WaitForSeconds(.5f);
        float eyeStanding = view.firstPersonCamera != null ? view.firstPersonCamera.transform.position.y - view.AceFeet.y : 0f;
        if (switched) view.SetFirstPerson(false);
        Line(switched && Mathf.Abs(eyeStanding - eyeCrouched - mover.CrouchEyeDrop) <= .05f,
            $"in first person the eye is {eyeCrouched:0.00} m up crouched and {eyeStanding:0.00} m standing (drops {mover.CrouchEyeDrop:0.00} m); photo zz-sneak-3-first-person-crouched.png");
        yield return new WaitForSeconds(.6f);

        // 4. Stand and walk back.
        sneakBefore = kinds[(int)NoiseKind.SneakStep];
        stepBefore = kinds[(int)NoiseKind.Step];
        yield return Leg(-way, length, false);
        sneakSteps = kinds[(int)NoiseKind.SneakStep] - sneakBefore;
        walkSteps = kinds[(int)NoiseKind.Step] - stepBefore;
        Line(Mathf.Abs(legSpeed - mover.WalkSpeed) <= .35f, $"standing up, Ace walks back at {legSpeed:0.00} m/s (set: {mover.WalkSpeed:0.0})");
        NightNoise.Recent(0, out last);
        Line(walkSteps > 0 && sneakSteps == 0 && last.kind == NoiseKind.Step && Mathf.Approximately(last.radius, NightNoise.WalkRadius),
            $"walking steps are heard within {NightNoise.WalkRadius:0} m: {walkSteps} walking steps, {sneakSteps} sneaking ones");
        mover.ScriptedSneak = null;
        mover.ScriptedInput = null;
    }

    // One straight leg of the sneak check: steered like a player (a direction on screen), timed once the crouch (or the
    // standing up) has settled. Sets legSpeed, and while crouched counts the frames the crouch walk shows.
    float legSpeed, legCrouchRates;
    int legFrames, legCrouchShown;
    IEnumerator Leg(Vector3 way, float length, bool sneaking)
    {
        Vector3 from = Ace, steadyFrom = Ace;
        float began = Time.time, steadyAt = -1f;
        // Crouching down takes a quarter of a second (the speed eases with it); standing, the walk starts at once.
        float settle = sneaking ? .5f : .1f;
        legSpeed = legCrouchRates = 0f;
        legFrames = legCrouchShown = 0;
        bool photo = !sneaking;
        while (Flat(Ace - from).magnitude < length && Time.time - began < 8f)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, view.MovementYaw, 0f)) * way;
            mover.ScriptedInput = new Vector2(local.x, local.z);
            if (steadyAt < 0f && Time.time - began >= settle) { steadyAt = Time.time; steadyFrom = Ace; }
            if (steadyAt >= 0f && sneaking && body != null && body.Worn && body.CanCrouch)
            {
                legFrames++;
                legCrouchRates += body.CrouchRate;
                if (body.CrouchGait.y >= .85f) legCrouchShown++;
            }
            if (!photo && Flat(Ace - from).magnitude > length * .6f)
            {
                photo = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "zz-sneak-1-sneaking.png"));
            }
            yield return null;
        }
        if (steadyAt >= 0f) legSpeed = Flat(Ace - steadyFrom).magnitude / Mathf.Max(.01f, Time.time - steadyAt);
        mover.ScriptedInput = Vector2.zero;
    }

    // Ace's body, every frame of a leg.
    void WatchBody()
    {
        if (body == null || !body.Worn) return;
        placedFrames++;
        if (body.OnFloor) onFloorFrames++;
        // Running straight; while the body swings round a sharp turn the legs step on purpose (AceBody's pivot step, 5 Oct).
        if (body.Speed > 4f && body.Pivot < .01f) { runFrames++; runRates += body.RunRate; if (body.Gait.z >= .85f) runShown++; }
        else if (body.Speed > 4f) pivotFrames++;
        if (body.Speed > 1f) facing.Add(body.FacingError);
    }

    IEnumerator BodyReport()
    {
        Line(runFrames > 0 && runShown >= runFrames * .95f,
            $"Ace's body runs while Ace runs: the run clip showing in {runShown} of {runFrames} frames above 4 m/s " +
            $"(played at {(runFrames > 0 ? runRates / runFrames : 0f):0.00}x on average there, so the feet keep up; " +
            $"and {pivotFrames} frames stepping round a sharp turn)");
        facing.Sort();
        float median = facing.Count > 0 ? facing[facing.Count / 2] : 0f;
        float p90 = facing.Count > 0 ? facing[Mathf.Min(facing.Count - 1, facing.Count * 9 / 10)] : 0f;
        Line(facing.Count > 0 && median <= 20f,
            $"Ace's body faces the way Ace goes: {median:0}° off at the median while moving, {p90:0}° at the 90th percentile (turning at the start of each leg), {facing.Count} frames");
        Line(notStill.Count == 0, "Ace's body stands still at the photo stops" + (notStill.Count > 0 ? ": not at " + string.Join("; ", notStill) : ""));
        Line(placedFrames > 0 && onFloorFrames >= placedFrames * .98f,
            $"Ace's body stands on the floor found under Ace in {onFloorFrames} of {placedFrames} frames (else at the capsule's bottom)");
        if (body.WearsSidekick)
        {
            // The Sidekick face lives: it blinked and glanced while Ace walked (about one blink every four seconds).
            AceFace face = body.Face;
            if (face != null && !face.lives)
                Line(true, $"Ace's face: still, as set (Sidekick Face is off); {face.Parts}");
            else
                Line(face != null && face.Blinks > 0 && face.Glances > 0, face != null
                    ? $"Ace's face lives: {face.Blinks} blinks and {face.Glances} glances over the walk ({face.Parts})"
                    : "Ace's face lives: the Sidekick body has no face to move (AceFace found nothing)");
        }
        else
        {
            // The stand-in's look has left the walk-ins' pool: nobody else in the city wears it (the neighbours out tonight included).
            int bodies = 0, doubles = 0;
            foreach (PolygonNpcVisual other in FindObjectsByType<PolygonNpcVisual>(FindObjectsInactive.Include))
            {
                if (other == body.Visual || other.ActiveAppearance < 0) continue;
                bodies++;
                if (other.ActiveAppearanceName == body.LookName) doubles++;
            }
            Line(doubles == 0, $"Nobody else wears Ace's look ({body.LookName}): {doubles} of the {bodies} other city bodies in the scene");
        }
        // First person hides the body (and the capsule), as it hid the capsule before; back out, the body again.
        Renderer capsuleMesh = view.bodyRenderer;
        bool switched = view.SetFirstPerson(true);
        yield return new WaitForSeconds(.4f);
        bool hidden = !body.Drawn && (capsuleMesh == null || !capsuleMesh.enabled);
        if (switched) view.SetFirstPerson(false);
        yield return new WaitForSeconds(.6f);
        bool back = body.Drawn && (capsuleMesh == null || !capsuleMesh.enabled);
        Line(switched && hidden && back, $"First person hides Ace's body ({(hidden ? "hidden" : "still drawn")}), and back overhead it is drawn again instead of the capsule ({(back ? "yes" : "no")})");
        string name = "zz-ace-body-overhead.png";
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));
        yield return null;
        report.AppendLine($"      photo {name}");
    }

    Vector3 Ace => capsule != null ? capsule.transform.position : transform.position;

    string PlanOf(Vector3 world)
    {
        Vector3 p = house.Plan(world);
        return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, z {2:0.00})", p.x, p.y, p.z);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Replace("--", "-").Trim('-');
    }

    void Finish()
    {
        report.Insert(0, (problems == 0 ? "All clear.\n" : problems + " problem(s).\n"));
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = "[Break-ins] Walk check: " + (problems == 0 ? "all clear. " : problems + " problem(s). ") + folder + "\n" + report;
        // A warning, not an error: with the Console's Error Pause on, an error would pause Play Mode here and it
        // would never stop by itself.
        if (problems == 0) Debug.Log(line); else Debug.LogWarning(line);
        StartCoroutine(Stop());
    }

    IEnumerator Stop()
    {
        yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
