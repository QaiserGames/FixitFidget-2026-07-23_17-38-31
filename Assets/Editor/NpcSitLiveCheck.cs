#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Sitting, measured live in Play Mode.
//
// Watches every café NPC that has NpcSeating through a real stretch of an open
// day and answers the questions a player would ask by looking:
//   * do people who come in actually sit down on the chairs?
//   * do their hips land on the seat (not in it, not floating above it)?
//   * is a chair ever shared, or do two bodies ever overlap?
//   * when they leave, do they stand up, get their navigation back and walk off?
// It also watches personal space outside: whether any two walkers (street
// neighbours or café NPCs on their feet) walk through each other.
//
// To give customers a chance to sit, it takes up to three walk-in jobs at the
// counter the way the player would (hear them out, accept). Photographs go next
// to the report. The day clock is topped up so the day cannot end mid-check;
// nothing is saved into the scene.
//
// Report: <project>/Logs/NpcSit/live-check-<time>.txt (and the Console).
public static class NpcSitLiveCheck
{
    private const string Menu = "Fixit Fidget/NPC/Sit 4 - Live sit check (Play Mode)";
    private const float MaxSeconds = 300f;      // real time
    // Real speed: crowd avoidance behaves differently in bigger time steps, and the
    // point is to see the café as the player sees it.
    private const float CheckTimeScale = 1f;
    private const int SeatedPhotoLimit = 3;
    private const int JobLimit = 3;
    private const float PersonalSpace = .45f;   // centre to centre, metres

    private sealed class Watch
    {
        public NpcSeating seating;
        public NavMeshAgent agent;
        public Transform body, thighL, thighR, footL, footR;
        public string name;
        public bool customer;
        public NpcSeating.Phase phase;
        public float phaseAt, seatedAt = -1f, handedBackAt = -1f;
        public TableSeat seat;
        public bool measured, photographed, stoodUp, handedBack, walkedOff, gone, tracking, detachedNoted;
        public Vector3 handBackPosition;
        public float walked, worstDesync, hipError = -1f, hipHeight, feetGap, facingError;
        // The visible city body (when the NPC wears one): its own hip joints and lowest point.
        public float bodyHipHeight = float.NaN, bodyLowest = float.NaN;
        // Where a customer went to wait once its job was taken (a seat, a standing spot, browsing).
        public WaitingSpot.SpotKind? waitKind;
        public readonly StringBuilder timeline = new();
    }

    private static readonly Stack<IEnumerator> routine = new();
    private static readonly List<Watch> watches = new();
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new(), warnings = new(), overlaps = new();
    private static readonly HashSet<string> overlapPairs = new();
    private static readonly List<NavMeshAgent> agents = new();
    private static StreetLife[] streets = Array.Empty<StreetLife>();
    private static int checks, failures, jobsAccepted, seatedPhotos, sharedSeatFrames;
    private static bool sitDownPhoto, standUpPhoto, extendedDay, dayEnded;
    private static float started, previousTimeScale = 1f, closestOverlap = float.PositiveInfinity;
    private static string folder, stamp;

    [MenuItem(Menu)]
    private static void Run()
    {
        report.Clear();
        watches.Clear();
        errors.Clear();
        warnings.Clear();
        overlaps.Clear();
        overlapPairs.Clear();
        agents.Clear();
        checks = failures = jobsAccepted = seatedPhotos = sharedSeatFrames = 0;
        sitDownPhoto = standUpPhoto = extendedDay = dayEnded = false;
        closestOverlap = float.PositiveInfinity;
        stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "NpcSit", "live-" + stamp);
        Directory.CreateDirectory(folder);
        routine.Clear();
        routine.Push(Sequence());
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Step;
        Debug.Log("[Sit check] Watching café NPCs for up to " + MaxSeconds + " s...");
    }

    [MenuItem(Menu, true)]
    private static bool CanRun() => EditorApplication.isPlaying && routine.Count == 0;

    private static void Step()
    {
        bool running = EditorApplication.isPlaying;
        try
        {
            while (running)
            {
                if (routine.Count == 0) { running = false; break; }
                IEnumerator top = routine.Peek();
                if (!top.MoveNext()) { routine.Pop(); continue; }
                if (top.Current is IEnumerator nested) { routine.Push(nested); continue; }
                break;
            }
        }
        catch (Exception exception)
        {
            Check(false, "Check stopped by an exception: " + exception.Message);
            Debug.LogException(exception);
            running = false;
        }
        if (running) return;
        EditorApplication.update -= Step;
        Application.logMessageReceived -= OnLog;
        routine.Clear();
        Finish();
    }

    private static IEnumerator Sequence()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "The open scene has a DayClock"); yield break; }
        if (clock.DayOver)
        {
            Check(false, "A day is open (press Open Tomorrow on the recap, then run the check again)");
            yield break;
        }
        // The check needs a few minutes of open day.
        float needed = MaxSeconds * CheckTimeScale + 20f;
        if (clock.TimeRemaining < needed)
        {
            PropertyInfo remaining = typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining));
            remaining?.SetValue(clock, needed);
            extendedDay = true;
        }
        streets = Object.FindObjectsByType<StreetLife>(FindObjectsInactive.Exclude);
        previousTimeScale = Time.timeScale;
        Time.timeScale = CheckTimeScale;
        started = Time.realtimeSinceStartup;
        float nextScan = 0f, nextJob = started + 3f;

        while (Time.realtimeSinceStartup - started < MaxSeconds)
        {
            if (!EditorApplication.isPlaying) yield break;
            if (clock == null || clock.DayOver) { dayEnded = true; break; }
            float now = Time.realtimeSinceStartup;
            if (now >= nextScan) { Scan(); nextScan = now + .25f; }
            if (now >= nextJob) { AcceptOneJob(); nextJob = now + 6f; }
            foreach (Watch watch in watches) Observe(watch);
            CheckSharing();
            CheckPersonalSpace();
            if (Enough()) break;
            yield return null;
        }
        Summarise();
    }

    // ---------- watching ----------

    private static void Scan()
    {
        foreach (NpcSeating seating in Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude))
        {
            if (watches.Any(w => w.seating == seating)) continue;
            var watch = new Watch
            {
                seating = seating,
                agent = seating.GetComponent<NavMeshAgent>(),
                body = seating.transform,
                customer = seating.GetComponent<CustomerBrain>() != null,
                phase = seating.Current,
                phaseAt = Time.realtimeSinceStartup,
            };
            watch.name = (watch.customer ? "Customer " : "Patron ") + (watches.Count + 1);
            CustomerBrain brain = seating.GetComponent<CustomerBrain>();
            if (brain != null) watch.name += " (" + brain.CustomerName + ")";
            Animator animator = seating.GetComponentInChildren<Animator>();
            Transform rig = animator != null ? animator.transform : seating.transform;
            watch.thighL = Bone(rig, "UpperLeg.L");
            watch.thighR = Bone(rig, "UpperLeg.R");
            watch.footL = Bone(rig, "Foot.L");
            watch.footR = Bone(rig, "Foot.R");
            watches.Add(watch);
            Note(watch, "arrived (" + watch.phase + ")");
        }
        agents.Clear();
        agents.AddRange(Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude));
    }

    private static void Observe(Watch w)
    {
        if (w.gone) return;
        if (w.seating == null)
        {
            w.gone = true;
            Note(w, "left the scene");
            return;
        }
        float now = Time.realtimeSinceStartup;
        NpcSeating.Phase current = w.seating.Current;
        if (w.seating.Seat != null) w.seat = w.seating.Seat;
        if (w.customer && w.waitKind == null)
        {
            CustomerBrain brain = w.seating.GetComponent<CustomerBrain>();
            if (brain != null && brain.WaitKind != null)
            {
                w.waitKind = brain.WaitKind;
                Note(w, "went to wait at a " + brain.WaitKind.Value.ToString().ToLowerInvariant() + " spot");
            }
        }
        if (current != w.phase)
        {
            string extra = "";
            if (current == NpcSeating.Phase.Approaching && w.seat != null)
            {
                Vector3 offset = w.body.position - w.seat.StandPoint.position;
                offset.y = 0f;
                extra = $" towards {SeatName(w.seat)}, starting {offset.magnitude:0.00} m from its stand point";
            }
            Note(w, w.phase + " -> " + current + extra);
            if (current == NpcSeating.Phase.Seated) w.seatedAt = now;
            if (current == NpcSeating.Phase.StandingUp) w.stoodUp = true;
            if (current == NpcSeating.Phase.Standing && w.phase == NpcSeating.Phase.Returning)
            {
                w.handedBack = w.tracking = true;
                w.handedBackAt = now;
                w.handBackPosition = w.body.position;
            }
            w.phase = current;
            w.phaseAt = now;
        }

        if (current == NpcSeating.Phase.SittingDown && !sitDownPhoto && now - w.phaseAt > .6f / CheckTimeScale && w.seat != null)
        {
            sitDownPhoto = true;
            Photograph(w, "sitting-down");
        }
        if (current == NpcSeating.Phase.Seated && !w.measured && now - w.seatedAt > 1f / CheckTimeScale && w.seat != null) Measure(w);
        if (current == NpcSeating.Phase.StandingUp && !standUpPhoto && now - w.phaseAt > .5f / CheckTimeScale && w.seat != null)
        {
            standUpPhoto = true;
            Photograph(w, "standing-up");
        }

        if (w.tracking)
        {
            float since = now - w.handedBackAt;
            if (w.agent != null && w.agent.isActiveAndEnabled && since > .15f)
            {
                Vector3 gap = w.body.position - w.agent.nextPosition;
                gap.y = 0f;
                w.worstDesync = Mathf.Max(w.worstDesync, gap.magnitude);
                if (!w.agent.updatePosition && !w.detachedNoted)
                {
                    w.detachedNoted = true;
                    Note(w, "navigation still detached after standing up");
                }
            }
            Vector3 moved = w.body.position - w.handBackPosition;
            moved.y = 0f;
            w.walked = Mathf.Max(w.walked, moved.magnitude);
            if (w.walked > 1f && !w.walkedOff)
            {
                w.walkedOff = true;
                Note(w, $"walking away ({since * CheckTimeScale:0.0} s of game time after standing)");
            }
            if (w.walkedOff || since > 8f) w.tracking = false;
        }
    }

    private static void Measure(Watch w)
    {
        w.measured = true;
        if (w.thighL == null || w.thighR == null || w.footL == null || w.footR == null)
        {
            Note(w, "seated, but its rig has no UpperLeg/Foot bones to measure");
            return;
        }
        Vector3 hip = (w.thighL.position + w.thighR.position) * .5f;
        Vector3 seatCentre = w.seat.SeatPose.position;
        Vector3 forward = TableForward(w.seat, w.body);
        var serialized = new SerializedObject(w.seating);
        float behind = serialized.FindProperty("hipBehindSeatCentre").floatValue;
        float above = serialized.FindProperty("hipAboveSeat").floatValue;
        Vector3 target = seatCentre - forward * behind;
        target.y = seatCentre.y + above * w.body.lossyScale.y;
        w.hipError = Vector3.Distance(hip, target);
        w.hipHeight = hip.y - seatCentre.y;
        float floor = w.seat.transform.position.y;
        w.feetGap = Mathf.Min(w.footL.position.y, w.footR.position.y) - floor;
        Vector3 facing = w.body.forward;
        facing.y = 0f;
        w.facingError = Vector3.Angle(facing, forward);
        Note(w, $"seated on {SeatName(w.seat)}: hips {w.hipError * 100f:0.0} cm from the target, " +
                $"{w.hipHeight * 100f:0.0} cm above the seat; lowest ankle {w.feetGap * 100f:0.0} cm above the floor; " +
                $"facing the table within {w.facingError:0.0} degrees");
        var visual = w.seating.GetComponent<PolygonNpcVisual>();
        if (visual != null && visual.VisualInstance != null)
        {
            Transform l = Bone(visual.VisualInstance.transform, "UpperLeg_L"), r = Bone(visual.VisualInstance.transform, "UpperLeg_R");
            if (l != null && r != null) w.bodyHipHeight = (l.position.y + r.position.y) * .5f - seatCentre.y;
            float lowest = float.MaxValue;
            foreach (SkinnedMeshRenderer skin in visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin == null || !skin.enabled || skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
                Mesh posed = PolygonNpcSetup.SkinToWorld(skin);
                foreach (Vector3 v in posed.vertices) lowest = Mathf.Min(lowest, v.y);
                Object.DestroyImmediate(posed);
            }
            if (lowest < float.MaxValue) w.bodyLowest = lowest - floor;
            Note(w, $"city body {visual.ActiveAppearanceName}: its hip joints {w.bodyHipHeight * 100f:0.0} cm above the seat, " +
                    $"its lowest point {w.bodyLowest * 100f:0.0} cm from the floor");
        }
        if (seatedPhotos < SeatedPhotoLimit)
        {
            seatedPhotos++;
            w.photographed = true;
            Photograph(w, "seated");
        }
    }

    private static void CheckSharing()
    {
        var bySeat = new Dictionary<TableSeat, int>();
        foreach (Watch w in watches)
        {
            if (w.gone || w.seating == null || !w.seating.IsSeated || w.seat == null) continue;
            bySeat.TryGetValue(w.seat, out int count);
            bySeat[w.seat] = count + 1;
        }
        if (bySeat.Values.Any(c => c > 1)) sharedSeatFrames++;
    }

    // Street neighbours and café NPCs on their feet, centre to centre.
    private static void CheckPersonalSpace()
    {
        var bodies = new List<(string name, Vector3 position)>();
        foreach (StreetLife street in streets)
        {
            if (street == null) continue;
            foreach (StreetLife.Actor actor in street.actors)
                if (actor != null && actor.actor != null && actor.actor.gameObject.activeInHierarchy && IsWalker(actor))
                    bodies.Add((actor.actor.name, actor.actor.position));
        }
        foreach (NavMeshAgent agent in agents)
        {
            if (agent == null || !agent.isActiveAndEnabled) continue;
            NpcSeating seating = agent.GetComponent<NpcSeating>();
            if (seating != null && seating.Busy) continue;   // the chair owns that body
            bodies.Add((agent.name + "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(agent), agent.transform.position));
        }
        for (int i = 0; i < bodies.Count; i++)
            for (int j = i + 1; j < bodies.Count; j++)
            {
                Vector3 d = bodies[i].position - bodies[j].position;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance >= PersonalSpace) continue;
                closestOverlap = Mathf.Min(closestOverlap, distance);
                string key = string.CompareOrdinal(bodies[i].name, bodies[j].name) < 0
                    ? bodies[i].name + " | " + bodies[j].name : bodies[j].name + " | " + bodies[i].name;
                if (overlapPairs.Add(key))
                    overlaps.Add($"{key}: {distance:0.00} m apart near ({bodies[i].position.x:0.0}, {bodies[i].position.z:0.0})");
            }
    }

    private static bool IsWalker(StreetLife.Actor actor) =>
        (actor.animator != null || actor.legs != null && actor.legs.Length > 0)
        && (actor.wings == null || actor.wings.Length == 0) && (actor.wheels == null || actor.wheels.Length == 0);

    // The player's first two presses at the counter: hear them out, accept.
    // Customers who like to wait sitting down go first (they are the ones this
    // check is about); after a minute anyone will do.
    private static void AcceptOneJob()
    {
        if (jobsAccepted >= JobLimit) return;
        var conversation = Object.FindAnyObjectByType<ConversationController>();
        if (conversation != null && conversation.InConversation) return;
        bool anyone = Time.realtimeSinceStartup - started > 60f;
        foreach (CustomerBrain customer in Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (customer == null || customer.IsLeaving || customer.IsCounterRepair) continue;
            var identity = customer.GetComponent<CustomerIdentity>();
            if (!anyone && (identity == null || identity.PreferredWaitKind != WaitingSpot.SpotKind.Seat)) continue;
            if (customer.CanHearIntake) customer.HearIntake();
            // A repair done at the counter keeps its customer standing there by design.
            if (customer.IsCounterRepair || !customer.CanAcceptJob) continue;
            customer.AcceptJob();
            jobsAccepted++;
            report.AppendLine($"NOTE  Took a job for {customer.CustomerName} at {Clock()} (as if the player pressed E twice).");
            return;
        }
    }

    private static bool Enough()
    {
        int sat = watches.Count(w => w.seatedAt >= 0f);
        int cycles = watches.Count(w => w.walkedOff);
        bool customerSat = watches.Any(w => w.customer && w.seatedAt >= 0f);
        bool customersHadTheirChance = jobsAccepted >= JobLimit && Time.realtimeSinceStartup - started > 150f;
        return sat >= 5 && cycles >= 3 && seatedPhotos >= SeatedPhotoLimit && standUpPhoto
            && (customerSat || customersHadTheirChance);
    }

    // ---------- verdict ----------

    private static void Summarise()
    {
        var seated = watches.Where(w => w.seatedAt >= 0f).ToList();
        var measured = watches.Where(w => w.hipError >= 0f).ToList();
        var stood = watches.Where(w => w.handedBack).ToList();
        var header = new StringBuilder();
        header.AppendLine($"Watched {watches.Count} café NPCs for {Time.realtimeSinceStartup - started:0} s real time " +
                          $"({(Time.realtimeSinceStartup - started) * CheckTimeScale:0} s of game time).");
        if (extendedDay) header.AppendLine("NOTE  The day clock was topped up so the day could not end mid-check.");
        if (dayEnded) header.AppendLine("NOTE  The day ended during the check.");
        report.Insert(0, header.ToString());

        Check(seated.Count >= 3, $"NPCs who come in sit down on the chairs ({seated.Count} sat: " +
            $"{seated.Count(w => !w.customer)} patrons, {seated.Count(w => w.customer)} customers)");
        if (jobsAccepted > 0)
        {
            var toSeats = watches.Where(w => w.customer && w.waitKind == WaitingSpot.SpotKind.Seat).ToList();
            if (toSeats.Count > 0)
                Check(toSeats.All(w => w.seatedAt >= 0f),
                    $"Waiting customers who took a seat sat down on it ({toSeats.Count(w => w.seatedAt >= 0f)} of {toSeats.Count}; " +
                    $"{watches.Count(w => w.customer && w.waitKind != null && w.waitKind != WaitingSpot.SpotKind.Seat)} others chose to stand or browse)");
            else
                report.AppendLine($"NOTE  None of the {jobsAccepted} customers whose jobs were taken chose a seat to wait on this time, " +
                                  "so customer sitting was not exercised (patrons were).");
        }
        if (measured.Count > 0)
        {
            float worstHip = measured.Max(w => w.hipError);
            float lowHeight = measured.Min(w => w.hipHeight), highHeight = measured.Max(w => w.hipHeight);
            float lowFeet = measured.Min(w => w.feetGap), highFeet = measured.Max(w => w.feetGap);
            float worstFacing = measured.Max(w => w.facingError);
            Check(worstHip <= .05f, $"Hips land on the seat where the clip expects (worst {worstHip * 100f:0.0} cm off)");
            Check(lowHeight >= .04f && highHeight <= .15f,
                $"Hips rest a thigh's thickness above the seat, not in it or floating ({lowHeight * 100f:0.0} to {highHeight * 100f:0.0} cm)");
            Check(lowFeet >= -.03f && highFeet <= .1f,
                $"Feet stay on the floor while seated (lowest ankle {lowFeet * 100f:0.0} to {highFeet * 100f:0.0} cm above it)");
            Check(worstFacing <= 10f, $"Seated NPCs face their table (worst {worstFacing:0.0} degrees off)");
            var bodies = measured.Where(w => !float.IsNaN(w.bodyHipHeight)).ToList();
            if (bodies.Count > 0)
            {
                float low = bodies.Min(w => w.bodyHipHeight), high = bodies.Max(w => w.bodyHipHeight);
                Check(low >= .04f && high <= .15f,
                    $"City bodies sit on the seat too, not sunk into it ({bodies.Count} measured, hip joints {low * 100f:0.0} to {high * 100f:0.0} cm above it)");
                var feet = bodies.Where(w => !float.IsNaN(w.bodyLowest)).ToList();
                if (feet.Count > 0)
                {
                    float lowest = feet.Min(w => w.bodyLowest), highest = feet.Max(w => w.bodyLowest);
                    Check(lowest >= -.04f && highest <= .08f,
                        $"City bodies' feet rest on the floor (lowest points {lowest * 100f:0.0} to {highest * 100f:0.0} cm from it)");
                }
            }
        }
        else Check(false, "At least one seated NPC was measured");
        Check(sharedSeatFrames == 0, sharedSeatFrames == 0 ? "No chair ever had two people on it"
            : $"A chair had two people on it for {sharedSeatFrames} frames");
        Check(stood.Count >= 2, $"NPCs stand up and hand the body back to navigation when they leave ({stood.Count} did)");
        if (stood.Count > 0)
        {
            // Only NPCs whose eight-second window finished (or who already walked) count.
            var resolved = stood.Where(w => w.walkedOff || !w.tracking).ToList();
            int walked = resolved.Count(w => w.walkedOff);
            Check(walked == resolved.Count, $"Everyone who stood up walked away afterwards ({walked} of {resolved.Count})");
            float desync = stood.Max(w => w.worstDesync);
            Check(desync <= .2f, $"After standing, the visible body and navigation stay together (worst {desync:0.00} m apart)");
        }
        Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions in the Console during the check"
            : $"{errors.Count} error(s) in the Console during the check (listed below)");
        Check(overlaps.Count == 0, overlaps.Count == 0
            ? "Nobody walked through anybody (street neighbours and café NPCs on their feet)"
            : $"{overlaps.Count} pair(s) of walkers got closer than {PersonalSpace:0.00} m (closest {closestOverlap:0.00} m, listed below)");

        report.AppendLine();
        report.AppendLine("---- Per NPC (game-time stamps) ----");
        foreach (Watch w in watches)
        {
            report.AppendLine(w.name);
            report.Append(w.timeline);
        }
        if (overlaps.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("---- Walkers closer than " + PersonalSpace.ToString("0.00", CultureInfo.InvariantCulture) + " m ----");
            foreach (string line in overlaps) report.AppendLine(line);
        }
        if (errors.Count > 0 || warnings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("---- Console ----");
            foreach (string line in errors) report.AppendLine("ERROR   " + line);
            foreach (string line in warnings.Distinct()) report.AppendLine("WARNING " + line);
        }
    }

    private static void Finish()
    {
        if (EditorApplication.isPlaying && Mathf.Approximately(Time.timeScale, CheckTimeScale))
            Time.timeScale = previousTimeScale;
        string verdict = (failures == 0 ? "PASS" : "FAIL") + $" {checks - failures}/{checks}";
        string text = "Live sit check: " + verdict + "\n" + report;
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "NpcSit", "live-check-" + stamp + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
        string summary = "[Sit check] " + verdict + " — report: " + path + ", photos: " + folder;
        if (failures == 0) Debug.Log(summary + "\n" + text);
        else Debug.LogWarning(summary + "\n" + text);
    }

    // ---------- helpers ----------

    private static void Photograph(Watch w, string label)
    {
        try
        {
            Vector3 hip = w.thighL != null && w.thighR != null ? (w.thighL.position + w.thighR.position) * .5f
                : w.body.position + Vector3.up * .5f;
            Vector3 forward = TableForward(w.seat, w.body);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            string name = $"{Directory.GetFiles(folder, "*.png").Length / 2 + 1}-{Safe(w.name)}-{label}";
            CafeSecondPassSteps.Capture(Path.Combine(folder, name + "-side.png"),
                hip + right * 2.0f + Vector3.up * 1.0f - forward * .1f, hip + Vector3.up * .15f, 40f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, name + "-front.png"),
                hip + forward * 2.1f - right * 1.2f + Vector3.up * 1.3f, hip + Vector3.up * .2f, 40f, false);
            Note(w, "photographed (" + label + ")");
        }
        catch (Exception exception)
        {
            Note(w, "photo failed: " + exception.Message);
        }
    }

    private static Vector3 TableForward(TableSeat seat, Transform fallback)
    {
        if (seat == null) return fallback.forward;
        Vector3 forward = seat.CupSpot.position - seat.SeatPose.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) { forward = seat.SeatPose.position - seat.StandPoint.position; forward.y = 0f; }
        if (forward.sqrMagnitude < 1e-4f) forward = fallback.forward;
        return forward.normalized;
    }

    private static Transform Bone(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static string SeatName(TableSeat seat)
    {
        if (seat == null) return "no seat";
        Transform table = seat.transform.parent;
        return (table != null ? table.name + " / " : "") + seat.name;
    }

    private static string Safe(string text)
    {
        var builder = new StringBuilder();
        foreach (char c in text) builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        return builder.ToString().Trim('_');
    }

    private static string Clock() => $"{(Time.realtimeSinceStartup - started) * CheckTimeScale,6:0.0}s";

    private static void Note(Watch w, string what) => w.timeline.AppendLine("   " + Clock() + "  " + what);

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (condition.StartsWith("[Sit check]", StringComparison.Ordinal)) return;
        string line = condition.Split('\n')[0];
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(line);
        else if (type == LogType.Warning) warnings.Add(line);
    }

    private static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }
}
#endif
