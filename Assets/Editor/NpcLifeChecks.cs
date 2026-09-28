#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// The café's life layer, checked (pass 2, 26 Sept 2026).
//
// EDIT MODE - "Café life setup": the project side of pass 2 is in place: the
// NPC layer and collision matrix, the gait data and animator blend trees, the
// five movement profiles and Grace's, the prefabs' components, and the five
// lounge seats (on the NavMesh, reachable from the door, poses at cushion height).
//
// PLAY MODE - "Café life (Play Mode)", in a lab session: sofa and banquette
// seating end to end (simultaneous claims, five patrons seated without overlap,
// nobody walking through a seated body, everyone up and out on request, seats
// released), and the life layer's plumbing (profiles assigned and varied,
// idle beats happening, a forced chat and a forced exchange doing what they
// say on the tin, arrival looks). Nothing is saved.
// Reports: <project>/Logs/NpcLife/.
public static class NpcLifeChecks
{
    private const string SetupMenu = "Fixit Fidget/Checks/Café life setup";
    private const string LiveMenu = "Fixit Fidget/NPC/Café life check (Play Mode)";
    private const string LoungeGroup = "Lounge seats (pass 2)";

    private static readonly Stack<IEnumerator> routine = new();
    private static readonly StringBuilder report = new();
    private static int checks, failures;
    private static readonly List<GameObject> temporary = new();

    // ------------------------------------------------------------ edit mode

    [MenuItem(SetupMenu)]
    private static void RunSetup()
    {
        report.Clear();
        checks = failures = 0;
        try { SetupChecks(); }
        catch (Exception e) { Check(false, "Check stopped by an exception: " + e.Message); Debug.LogException(e); }
        Finish("setup-check");
    }

    private static void SetupChecks()
    {
        // Layers and matrix.
        int npc = LayerMask.NameToLayer("NPC"), player = LayerMask.NameToLayer("Player");
        Check(npc >= 0 && player >= 0, $"Layers exist: Player={player}, NPC={npc}");
        if (npc >= 0 && player >= 0)
        {
            Check(Physics.GetIgnoreLayerCollision(player, npc), "Player x NPC collisions are off in the matrix");
            Check(!Physics.GetIgnoreLayerCollision(npc, 0) && !Physics.GetIgnoreLayerCollision(player, 0), "NPC and Player still collide with Default");
        }
        foreach (string path in new[] { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Check(false, path + " exists"); continue; }
            Check(go.layer == npc, $"{Path.GetFileName(path)} is on the NPC layer ({LayerMask.LayerToName(go.layer)})");
            var social = go.GetComponent<NpcSocial>();
            var loco = go.GetComponent<NpcLocomotion>();
            Check(social != null && go.GetComponent<NpcPosture>() != null && loco != null && go.GetComponent<PersonalSpace>() != null && go.GetComponent<NpcLookAt>() != null || social != null && loco != null,
                  $"{Path.GetFileName(path)} carries NpcSocial, NpcPosture, NpcLocomotion, PersonalSpace");
            if (social != null)
            {
                var so = new SerializedObject(social);
                Check(so.FindProperty("library").objectReferenceValue != null, $"{Path.GetFileName(path)}: NpcSocial has the profile library");
            }
            if (loco != null)
            {
                var so = new SerializedObject(loco);
                Check(so.FindProperty("gait").objectReferenceValue != null, $"{Path.GetFileName(path)}: NpcLocomotion has the gait data");
            }
        }
        PlayerMovement ace = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        var obstacle = ace != null ? ace.GetComponent<NavMeshObstacle>() : null;
        Check(ace != null && ace.gameObject.layer == player && obstacle != null && !obstacle.carving && obstacle.radius >= .35f,
              $"Ace is on the Player layer with a non-carving obstacle of radius >= 0.35 ({(obstacle != null ? obstacle.radius.ToString("0.00") : "none")})");

        // Gait data and animator.
        var gait = AssetDatabase.LoadAssetAtPath<NpcGaitData>(NpcGaitAnimations.DataPath);
        Check(gait != null, "NpcGaitData exists (Fixit Fidget > NPC > Gait 1)");
        if (gait != null)
        {
            Check(gait.walks != null && gait.walks.Length >= 3 && gait.walks.All(w => w != null && w.clip != null), $"Three walk styles with clips ({(gait.walks != null ? gait.walks.Length : 0)})");
            if (gait.walks != null)
                foreach (var w in gait.walks)
                    if (w != null) Check(w.clipSpeed > .6f && w.clipSpeed < 2.6f, $"Walk '{w.name}' has a measured natural speed ({w.clipSpeed:0.00} m/s)");
            Check(gait.idles != null && gait.idles.Length >= 2 && gait.idles.All(i => i != null), $"Two idle styles ({(gait.idles != null ? gait.idles.Length : 0)})");
            Check(gait.standingTalk != null, "A standing talk clip");
            var distinct = gait.walks != null ? gait.walks.Where(w => w != null && w.clip != null).Select(w => w.clip).Distinct().Count() : 0;
            Check(distinct >= 3, $"The three walks are three different clips ({distinct})");
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/CustomerAnimator(.controller");
        Check(controller != null, "The customer animator exists");
        if (controller != null)
        {
            var names = controller.parameters.Select(p => p.name).ToArray();
            Check(names.Contains("WalkStyle") && names.Contains("IdleStyle") && names.Contains("WalkRate") && names.Contains("Talking"), "Animator has WalkStyle, IdleStyle, WalkRate and Talking");
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            var walk = states.FirstOrDefault(s => s.name == "CharacterArmature|Walk");
            var idle = states.FirstOrDefault(s => s.name == "CharacterArmature|Idle");
            Check(walk != null && walk.motion is BlendTree wt && wt.children.Length >= 3 && wt.blendParameter == "WalkStyle" && walk.speedParameterActive && walk.speedParameter == "WalkRate",
                  "Walk state is a WalkStyle blend tree of 3 clips, sped by WalkRate");
            Check(idle != null && idle.motion is BlendTree it && it.children.Length >= 2 && it.blendParameter == "IdleStyle", "Idle state is an IdleStyle blend tree of 2 clips");
            var talk = states.FirstOrDefault(s => s.name == "Standing Talk");
            Check(talk != null && talk.motion != null && idle != null && idle.transitions.Any(t => t.destinationState == talk), "Standing Talk state, reached from Idle while Talking");
        }

        // Profiles.
        var library = AssetDatabase.LoadAssetAtPath<NpcProfileLibrary>(CafeNpcPass2Setup.LibraryPath);
        Check(library != null && library.Count >= 5, $"Profile library with 5 archetypes ({(library != null ? library.Count : 0)})");
        if (library != null && library.Count > 0)
        {
            var speeds = library.profiles.Where(p => p != null).Select(p => p.walkSpeed).ToArray();
            Check(speeds.Max() - speeds.Min() >= .25f, $"Walk speeds spread across the profiles ({speeds.Min():0.00}-{speeds.Max():0.00})");
            Check(library.profiles.Where(p => p != null).Select(p => p.Name).Distinct().Count() == library.Count, "Profile names are distinct");
            Check(library.profiles.Where(p => p != null).Select(p => p.walkStyle).Distinct().Count() >= 3, "At least three walk styles are used across the profiles");
            var grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>("Assets/Data/Regulars/Regular_Grace.asset");
            Check(grace != null && grace.movementProfile != null, $"Grace has a fixed movement profile ({(grace != null && grace.movementProfile != null ? grace.movementProfile.Name : "none")})");
            // The stable pick is stable.
            Check(library.ForKey("grace") == library.ForKey("grace") && library.ForKey("someone") == library.ForKey("someone"), "A regular's fallback profile is the same every time");
        }

        // Lounge seats.
        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include);
        var lounge = seats.Where(s => s.transform.parent != null && s.transform.parent.name == LoungeGroup).ToArray();
        Check(lounge.Length == 5, $"Five lounge seats under '{LoungeGroup}' ({lounge.Length})");
        Check(seats.Length >= 21, $"At least 21 table seats in the scene ({seats.Length})");
        Vector3 door = new Vector3(0f, 0f, 1.5f);
        foreach (TableSeat seat in lounge)
        {
            bool onMesh = NavMesh.SamplePosition(seat.StandPoint.position, out NavMeshHit hit, .2f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            bool reachable = onMesh && NavMesh.CalculatePath(door, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            Check(onMesh && reachable, $"'{seat.name}': stand point on the NavMesh ({onMesh}) and reachable from the door ({reachable})");
            float h = seat.SeatPose.position.y;
            Check(h > .38f && h < .52f, $"'{seat.name}': seat pose at cushion height ({h:0.00} m)");
            Check(seat.SnapToSeat, $"'{seat.name}': Snap To Seat on");
            bool bench = seat.name.Contains("sofa");
            Check(seat.Style == (bench ? TableSeat.SitStyle.Bench : TableSeat.SitStyle.Chair), $"'{seat.name}': sit style {seat.Style}");
            if (bench) Check(seat.ClearanceRadius > 0f && seat.ClearanceRadius < .4f, $"'{seat.name}': bench clearance footprint ({seat.ClearanceRadius:0.00} m)");
            Vector3 f = seat.FacingPoint - seat.SeatPose.position; f.y = 0f;
            Check(f.sqrMagnitude > .01f && Vector3.Dot(f.normalized, Vector3.right) > .8f || !bench, $"'{seat.name}': faces into the room");
        }
        // No walkable plateau at table or cushion height anywhere on the cafe floor
        // (the bake collects colliders; furniture without a Not Walkable modifier
        // becomes a step people can climb).
        int plateau = 0, islands = 0; float highest = 0f; string where = "";
        var doorPath = new NavMeshPath();
        for (float z = .5f; z <= 12.5f; z += .5f)
            for (float x = -7f; x <= 7f; x += .5f)
                if (NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit h, .6f, NavMesh.AllAreas) && h.position.y > .2f && h.position.y < .9f)
                {
                    // A table top nobody can climb onto is an island; a plateau someone can walk up onto is the problem.
                    bool connected = NavMesh.CalculatePath(door, h.position, NavMesh.AllAreas, doorPath) && doorPath.status == NavMeshPathStatus.PathComplete;
                    if (!connected) { islands++; continue; }
                    plateau++;
                    if (h.position.y > highest) { highest = h.position.y; where = $"({h.position.x:0.0}, {h.position.z:0.0})"; }
                }
        Check(plateau == 0, $"No walkable NavMesh between 0.2 and 0.9 m that people can walk up onto ({plateau} connected sample points{(plateau > 0 ? $", highest {highest:0.00} m at {where}" : "")}; {islands} unreachable table-top islands ignored)");

        // Two bench seats 0.75 m apart may both be claimed (the footprint rule).
        if (lounge.Length == 5)
        {
            var reading = lounge.Where(s => s.name.StartsWith("Reading")).ToArray();
            if (reading.Length == 2)
            {
                float apart = Vector3.Distance(reading[0].StandPoint.position, reading[1].StandPoint.position);
                float need = reading[0].ClearanceRadius + reading[1].ClearanceRadius + .1f;
                Check(apart >= need, $"Reading sofa seats can both be claimed (stand points {apart:0.00} m apart, need {need:0.00})");
            }
        }
    }

    // ------------------------------------------------------------ play mode

    [MenuItem(LiveMenu)]
    private static void RunLive()
    {
        report.Clear();
        checks = failures = 0;
        temporary.Clear();
        routine.Clear();
        routine.Push(Sequence());
        EditorApplication.update += Step;
        Debug.Log("[Café life check] Watching the café...");
    }

    [MenuItem(LiveMenu, true)]
    private static bool CanRunLive() => EditorApplication.isPlaying && routine.Count == 0;

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
        routine.Clear();
        foreach (GameObject go in temporary) if (go != null) Object.Destroy(go);
        temporary.Clear();
        Finish("life-check");
    }

    private static IEnumerator Sequence()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "The open scene has a DayClock"); yield break; }
        if (clock.DayOver) { Check(false, "A day is open (press Open Tomorrow on the recap, then run the check again)"); yield break; }
        if (clock.TimeRemaining < 300f) typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining))?.SetValue(clock, 300f);
        if (!CafeLab.Active || CafeLabDirector.Instance == null) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab)"); yield break; }
        CafeLabDirector lab = CafeLabDirector.Instance;

        var all = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude);
        var lounge = all.Where(s => s.transform.parent != null && s.transform.parent.name == LoungeGroup).ToArray();
        var tables = all.Where(s => !lounge.Contains(s)).ToArray();
        Check(lounge.Length == 5, $"Five lounge seats are live ({lounge.Length})");
        if (lounge.Length == 0) yield break;

        // ---------- 1. simultaneous claims ----------
        // Hold every ordinary table seat, then ask for seven seats in one frame:
        // exactly the five lounge seats come back, no seat twice.
        var holders = new List<Component>();
        foreach (TableSeat seat in tables)
        {
            if (seat.IsOccupied) continue;
            Component holder = Dummy("seat holder");
            if (seat.Claim(holder)) holders.Add(holder);
        }
        Note($"Held {holders.Count} table seats ({tables.Count(t => t.IsOccupied)} of {tables.Length} occupied).");
        var claimed = new List<WaitingSpot>();
        var claimers = new List<Component>();
        for (int i = 0; i < 7; i++)
        {
            Component c = Dummy("claimer " + i);
            claimers.Add(c);
            WaitingSpot spot = WaitingArea.Instance.Claim(c, WaitingSpot.SpotKind.Seat);
            if (spot != null) claimed.Add(spot);
        }
        int loungeClaims = claimed.Count(s => lounge.Contains(s));
        int loiterClaims = claimed.Count(s => s.Kind != WaitingSpot.SpotKind.Seat);
        Check(loungeClaims == 5 && claimed.Distinct().Count() == claimed.Count,
              $"Seven simultaneous claims get all five lounge seats, each once ({loungeClaims} lounge seats, {loiterClaims} loiter fallbacks, {claimed.Distinct().Count()} distinct of {claimed.Count})");
        foreach (TableSeat seat in lounge)
            if (!claimed.Contains(seat))
            {
                // Say what blocked it.
                foreach (TableSeat other in all)
                    if (other != seat && other.IsOccupied)
                    {
                        float apart = Vector3.Distance(seat.StandPoint.position, other.StandPoint.position);
                        if (apart < 1.6f) Note($"  '{seat.name}' not claimed: '{other.name}' ({other.transform.parent?.name}) stand point {apart:0.00} m away is occupied.");
                    }
            }
        foreach (Component c in claimers) WaitingArea.Instance.Release(c);
        Check(lounge.All(s => !s.IsOccupied), "Released claims leave every lounge seat free");
        yield return null;

        // ---------- 2. five patrons take the sofas ----------
        int sent = lab.SendPatronsNow(6);
        Note($"Sent {sent} patrons; only the lounge seats are free.");
        float deadline = Time.time + 45f;
        float closestWalkerToSeated = float.MaxValue;
        string closestNote = "";
        steps.Clear();
        exits.Clear();
        exitResults.Clear();
        flicker.Clear();
        flickers = 0;
        walkingSeconds = 0f;
        flickerNotes.Clear();
        while (Time.time < deadline)
        {
            var seatedNow = SeatedIn(lounge);
            WatchWalkPast(ref closestWalkerToSeated, ref closestNote);
            WatchSteps();
            if (seatedNow.Count >= 5) break;
            yield return null;
        }
        var seated = SeatedIn(lounge);
        Check(seated.Count == 5, $"Five patrons sat down on the lounge seats within 45 s ({seated.Count})");
        Check(seated.Select(p => p.seat).Distinct().Count() == seated.Count, "No lounge seat has two sitters");
        float minApart = float.MaxValue;
        foreach (var s1 in seated) foreach (var s2 in seated)
            if (s1.seating != s2.seating) minApart = Mathf.Min(minApart, Flat(s1.seating.transform.position - s2.seating.transform.position).magnitude);
        Check(seated.Count < 2 || minApart >= .5f, $"Seated bodies do not overlap (closest {minApart:0.00} m)");
        foreach (var p in seated)
        {
            Vector3 d = Flat(p.seating.transform.position - p.seat.SeatPose.position);
            float feetAbove = p.seating.transform.position.y - p.seat.StandPoint.position.y;
            Check(d.magnitude < .45f && Mathf.Abs(feetAbove) <= .13f, $"{p.name} on '{p.seat.name}': body {d.magnitude:0.00} m from the pose, feet {feetAbove * 100f:+0;-0} cm off the floor, {p.seat.Style}");
            var visual = p.seating.GetComponent<PolygonNpcVisual>();
            var social = p.seating.GetComponent<NpcSocial>();
            Note($"  {p.name}: look {(visual != null ? visual.ActiveAppearanceName : "rig")}, layer {LayerMask.LayerToName(p.seating.gameObject.layer)} while seated, " +
                 $"y {p.seating.transform.position.y:0.00} (stand {p.seat.StandPoint.position.y:0.00}), situation {(social != null ? social.Current.ToString() : "no NpcSocial")}, profile {(social != null ? social.ProfileName : "-")}.");
        }
        Check(seated.All(p => p.seating.gameObject.layer == 0), "Seated bodies are solid to Ace (Default layer while in the chair)");

        // ---------- 3. someone walks past ----------
        // Three customers cross the room while the sofas are occupied.
        lab.SendCustomersNow(3);
        deadline = Time.time + 20f;
        while (Time.time < deadline) { WatchWalkPast(ref closestWalkerToSeated, ref closestNote); yield return null; }
        Check(closestWalkerToSeated > .5f, $"No walker passed through a seated body (closest {closestWalkerToSeated:0.00} m {closestNote})");

        // ---------- 4. the life layer while they sit ----------
        var people = NpcAttentionDirector.People.Where(p => p != null && p.isActiveAndEnabled).ToList();
        var profiles = people.Select(p => p.ProfileName).Where(n => n != "-").Distinct().ToList();
        Check(people.Count >= 6 && profiles.Count >= 3, $"Profiles are assigned and varied: {people.Count} people, {profiles.Count} profiles ({string.Join(", ", profiles)})");
        var seatedSocial = seated.Select(p => p.seating.GetComponent<NpcSocial>()).Where(s => s != null && s.Current == NpcSocial.Situation.Seated).ToList();
        Check(seatedSocial.Count == seated.Count, $"Seated patrons report the Seated situation ({seatedSocial.Count} of {seated.Count})");
        int beatsBefore = NpcAttentionDirector.CountOf(NpcSocial.Beat.Posture) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookAround)
                          + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookNeighbour) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookPasser)
                          + NpcAttentionDirector.CountOf(NpcSocial.Beat.Gesture) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookCounter);
        // A chat between the two reading-sofa sitters (same table by distance), forced.
        NpcSocial a = null, b = null;
        foreach (NpcSocial x in seatedSocial) foreach (NpcSocial y in seatedSocial)
            if (x != y && x.Seating != null && y.Seating != null && x.Seating.Seat != null && y.Seating.Seat != null
                && Vector3.Distance(x.Seating.Seat.SeatPose.position, y.Seating.Seat.SeatPose.position) < 1.2f) { a = x; b = y; }
        if (a != null && b != null)
        {
            bool started = NpcAttentionDirector.ForceChat(a, b);
            yield return Wait(2.2f);
            NpcLookAt la = a.LookAt, lb = b.LookAt;
            float bearingA = Mathf.Abs(Bearing(a.transform, b.EyePoint)), bearingB = Mathf.Abs(Bearing(b.transform, a.EyePoint));
            bool aLooks = la != null && la.Looking && Mathf.Sign(la.AppliedYaw) == Mathf.Sign(Bearing(a.transform, b.EyePoint)) || bearingA < 6f;
            bool bLooks = lb != null && lb.Looking && Mathf.Sign(lb.AppliedYaw) == Mathf.Sign(Bearing(b.transform, a.EyePoint)) || bearingB < 6f;
            var animA = a.GetComponentInChildren<Animator>(); var animB = b.GetComponentInChildren<Animator>();
            // Since the Mixamo beats (27 Sept) a seated speaker talks with the "Sitting Talking" or
            // "Talking" clip on the Beats layer instead of the Talking flag, when the clips are there.
            bool someoneTalks = (animA != null && animA.GetBool("Talking")) || (animB != null && animB.GetBool("Talking"))
                                || TalkBeat(a) || TalkBeat(b);
            // They may already be chatting on their own (the director got there first): that counts.
            bool together = a.InChat && b.InChat && a.Chat != null && a.Chat == b.Chat;
            Check(together, $"A chat runs between {a.name} and {b.name} ({(started ? "forced" : "they were already chatting on their own")})");
            Check(aLooks && bLooks, $"Both sitters look at each other during the chat (heads {(la != null ? la.AppliedYaw : 0f):0}° / {(lb != null ? lb.AppliedYaw : 0f):0}°, bearings {bearingA:0}° / {bearingB:0}°)");
            Check(someoneTalks, "One of them is talking (Talking flag / seated talk clip / Mixamo talking beat)");
            deadline = Time.time + 16f;
            while (Time.time < deadline && (a.InChat || b.InChat)) yield return null;
            yield return Wait(.6f);
            bool cleared = !(animA != null && animA.GetBool("Talking")) && !(animB != null && animB.GetBool("Talking"));
            Check(!a.InChat && !b.InChat && cleared, "The chat ends by itself and both return to their own idles (Talking off)");
        }
        else Note("No two sitters at one table for a forced chat; skipped.");
        // A forced exchange between two people who can see each other.
        NpcSocial p1 = seatedSocial.FirstOrDefault(), p2 = seatedSocial.FirstOrDefault(x => x != p1 && p1 != null && p1.CanSee(x.transform.position, 95f) && x.CanSee(p1.transform.position, 95f));
        if (p1 != null && p2 != null)
        {
            float bearing12 = Bearing(p1.transform, p2.EyePoint);
            NpcAttentionDirector.ForceExchange(p1, p2);
            yield return Wait(1.4f);
            Check(p1.LookAt != null && p1.LookAt.Looking, $"{p1.name} ({p1.ProfileName}, {p1.Current}) looks at {p2.name} in an exchange (head {(p1.LookAt != null ? p1.LookAt.AppliedYaw : 0f):0}°, weight {(p1.LookAt != null ? p1.LookAt.Weight : 0f):0.00}, bearing {bearing12:0}°, beat {p1.CurrentBeat}, chat {p1.InChat})");
            yield return Wait(1.4f);
            Note($"{p2.name} {(p2.LookAt != null && p2.LookAt.Looking ? "looked back" : "did not look back (may be mid-beat)")}.");
        }
        // ---------- 5. everyone up and out (while they are still here) ----------
        var patronSeats = lounge.Where(s => s.IsOccupied && s.Occupant is PatronBrain).ToList();
        int leavers = lab.PatronsLeaveNow();
        deadline = Time.time + 30f;
        while (Time.time < deadline && patronSeats.Any(s => s.IsOccupied && s.Occupant is PatronBrain || s.IsOccupied && s.Occupant is NpcSeating)) { WatchSteps(); yield return null; }
        int stillHeld = patronSeats.Count(s => s.IsOccupied && (s.Occupant is PatronBrain || s.Occupant is NpcSeating));
        Check(stillHeld == 0, $"All patron-held lounge seats released after {leavers} patrons were told to leave ({patronSeats.Count} were held, {stillHeld} still held)");
        deadline = Time.time + 30f;
        while (Time.time < deadline && Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Any(p => p.transform.position.z > .5f && p.enabled)) { WatchSteps(); yield return null; }
        int stillInside = Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Count(p => p.transform.position.z > .5f && p.enabled);
        Check(stillInside == 0, $"Every sofa patron is up and out of the café within 60 s ({stillInside} still inside)");

        // ---------- 6. idle beats: a fresh room of patrons for 25 s ----------
        lab.SendPatronsNow(5);
        float beatsUntil = Time.time + 35f;
        while (Time.time < beatsUntil) { WatchSteps(); yield return null; }
        ReportSteps();
        int beatsAfter = NpcAttentionDirector.CountOf(NpcSocial.Beat.Posture) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookAround)
                         + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookNeighbour) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookPasser)
                         + NpcAttentionDirector.CountOf(NpcSocial.Beat.Gesture) + NpcAttentionDirector.CountOf(NpcSocial.Beat.LookCounter);
        Check(beatsAfter - beatsBefore >= 4, $"Seated people did things while sitting: {beatsAfter - beatsBefore} idle beats in ~30 s across {seatedSocial.Count} sitters");
        Check(NpcAttentionDirector.ArrivalLooks >= 1 || NpcAttentionDirector.People.Count < 4, $"Someone looked up when people came in ({NpcAttentionDirector.ArrivalLooks} arrival looks)");
        Note("Director: " + NpcAttentionDirector.Summary());

        // ---------- 7. out of the chairs and on their way (pass 2c) ----------
        // The sofa patrons leave; then the table chairs are opened up, a group
        // sits at them and is sent home: every walk out is followed onto its
        // next leg (turning against the shortest turn), and every set-off is
        // watched for the walk clip flickering.
        lab.PatronsLeaveNow();
        float exitsUntil = Time.time + 6f;
        while (Time.time < exitsUntil) { WatchSteps(); yield return null; }
        foreach (Component h in holders) WaitingArea.Instance.Release(h);
        holders.Clear();
        lab.SendPatronsNow(6);
        float sitUntil = Time.time + 45f;
        while (Time.time < sitUntil && SeatedIn(tables).Count < 4) { WatchSteps(); yield return null; }
        int atTables = SeatedIn(tables).Count;
        Note($"{atTables} patrons sat at the tables for the walk-out check.");
        exitsUntil = Time.time + 3f;
        while (Time.time < exitsUntil) { WatchSteps(); yield return null; }
        lab.PatronsLeaveNow();
        exitsUntil = Time.time + 14f;
        while (Time.time < exitsUntil) { WatchSteps(); yield return null; }
        FinishExits(true);
        ReportExits();
        lab.CustomersLeaveNow();
        foreach (Component h in holders) WaitingArea.Instance.Release(h);
    }

    // ---------- walking into and out of the seats (pass 2b) ----------
    // The hand-walked steps between the stand point and the seat must play the
    // walk clip (they once slid in the idle pose - "hovering") and must not
    // snap-turn. Watched every frame for every NPC that is using a seat.
    private sealed class StepWatch
    {
        public NpcSeating.Phase phase;
        public float moving, walkPlaying, slideRun, worstSlide, worstRate, turnedIn;
        public int ins, outs;
    }
    private static readonly Dictionary<NpcSeating, StepWatch> steps = new();
    private static readonly int WalkState = Animator.StringToHash("CharacterArmature|Walk");

    private static void WatchSteps()
    {
        float dt = Time.deltaTime;
        WatchFlicker(dt);
        FinishExits(false);
        foreach (NpcSeating s in Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude))
        {
            if (!steps.TryGetValue(s, out StepWatch w)) steps[s] = w = new StepWatch { phase = s.Current };
            NpcSeating.Phase now = s.Current;
            bool stepping = now == NpcSeating.Phase.Approaching || now == NpcSeating.Phase.Returning;
            WatchExit(s, w.phase, now, dt);
            if (w.phase != now)
            {
                // A walk in or out just ended: keep how it turned.
                if (w.phase == NpcSeating.Phase.Approaching) { w.ins++; w.turnedIn += s.StepTurned; w.worstRate = Mathf.Max(w.worstRate, s.StepWorstTurnRate); }
                if (w.phase == NpcSeating.Phase.Returning) { w.outs++; w.worstRate = Mathf.Max(w.worstRate, s.StepWorstTurnRate); }
                w.phase = now;
                w.slideRun = 0f;
            }
            if (!stepping || s.StepSpeed < .25f || dt <= 0f) continue;
            Animator animator = s.GetComponentInChildren<Animator>();
            bool walk = false;
            if (animator != null)
            {
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                walk = current.shortNameHash == WalkState
                       || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == WalkState;
            }
            w.moving += dt;
            if (walk) { w.walkPlaying += dt; w.slideRun = 0f; }
            else { w.slideRun += dt; w.worstSlide = Mathf.Max(w.worstSlide, w.slideRun); }
        }
    }

    private static void ReportSteps()
    {
        float moving = steps.Values.Sum(w => w.moving), playing = steps.Values.Sum(w => w.walkPlaying);
        int ins = steps.Values.Sum(w => w.ins), outs = steps.Values.Sum(w => w.outs);
        float worstSlide = steps.Values.Select(w => w.worstSlide).DefaultIfEmpty(0f).Max();
        float worstRate = steps.Values.Select(w => w.worstRate).DefaultIfEmpty(0f).Max();
        float turnedIn = ins > 0 ? steps.Values.Sum(w => w.turnedIn) / ins : 0f;
        Check(ins >= 5 && outs >= 3, $"Walks into and out of seats were watched ({ins} in, {outs} out)");
        Check(moving > 0f && playing / moving >= .85f,
              $"The walk clip plays while people step into and out of their seats ({(moving > 0f ? playing / moving * 100f : 0f):0}% of {moving:0.0} s moving by hand; they used to slide in the idle pose)");
        Check(worstSlide <= .35f, $"Nobody slides without walking for more than a moment on the way in or out (longest {worstSlide:0.00} s)");
        Check(worstRate <= 340f, $"No snap turns stepping in or out (fastest {worstRate:0} deg/s; the old steps turned at 360 deg/s on the spot)");
        Note($"Average turning on the way into a seat: {turnedIn:0} deg (walk plus the swivel into the seat comes after).");
    }

    // ---------- out of the seat towards where they go, and set-offs (pass 2c) ----------
    // From the first moment of standing up to 1.5 s after the seating hands the
    // body back to the agent: every degree turned, and the largest turn one way
    // (a loop shows as more than a turn-round). The pass 2b walk out went round
    // the back of the chair to the stand point first: in the recordings its
    // largest one-way turn was 243 degrees at the median and up to 440 (loops),
    // 340 degrees turned in all at the median.
    private sealed class ExitWatch
    {
        public float startYaw, lastYaw, turned, signed, oneWay, handedBackAt = -1f, speed;
        public Vector3 lastPosition, heading;
        public bool bench;
    }
    private static readonly Dictionary<NpcSeating, ExitWatch> exits = new();
    private static readonly List<(float oneWay, float total, bool bench)> exitResults = new();

    private static void WatchExit(NpcSeating s, NpcSeating.Phase before, NpcSeating.Phase now, float dt)
    {
        float yaw = s.transform.eulerAngles.y;
        Vector3 position = s.transform.position;
        if (before != now && now == NpcSeating.Phase.StandingUp)
            exits[s] = new ExitWatch
            {
                startYaw = yaw, lastYaw = yaw, lastPosition = position,
                bench = s.Seat != null && s.Seat.Style == TableSeat.SitStyle.Bench
            };
        if (!exits.TryGetValue(s, out ExitWatch e)) return;
        if (before != now && now == NpcSeating.Phase.Standing) e.handedBackAt = Time.time;
        if (dt <= 0f) return;
        float turn = Mathf.DeltaAngle(e.lastYaw, yaw);
        e.turned += Mathf.Abs(turn);
        e.signed += turn;
        e.oneWay = Mathf.Max(e.oneWay, Mathf.Abs(e.signed));
        e.lastYaw = yaw;
        Vector3 step = Flat(position - e.lastPosition);
        e.lastPosition = position;
        e.speed = Mathf.Lerp(e.speed, step.magnitude / dt, 1f - Mathf.Exp(-dt / .1f));
        if (e.speed > .4f && step.sqrMagnitude > 1e-8f) e.heading = Vector3.Lerp(e.heading, step.normalized, 1f - Mathf.Exp(-dt / .15f));
    }

    private static void FinishExits(bool all)
    {
        foreach (NpcSeating s in exits.Keys.ToList())
        {
            ExitWatch e = exits[s];
            bool gone = s == null || !s.isActiveAndEnabled;
            bool done = e.handedBackAt >= 0f && Time.time - e.handedBackAt >= 1.5f;
            if (!done && !gone && !all) continue;
            if (e.handedBackAt >= 0f) exitResults.Add((e.oneWay, e.turned, e.bench));
            exits.Remove(s);
        }
    }

    private static void ReportExits()
    {
        var chairs = exitResults.Where(r => !r.bench).ToList();
        var all = exitResults;
        Check(all.Count >= 4 && chairs.Count >= 2, $"Walks out of seats were followed onto their next leg ({all.Count}: {chairs.Count} from chairs, {all.Count - chairs.Count} from sofas)");
        if (all.Count == 0) return;
        float[] oneWay = all.Select(r => r.oneWay).OrderBy(x => x).ToArray();
        float[] total = all.Select(r => r.total).OrderBy(x => x).ToArray();
        Check(oneWay[oneWay.Length - 1] <= 220f,
              $"No loops on the way out of a seat: the largest turn one way is {oneWay[oneWay.Length - 1]:0} deg (median {oneWay[oneWay.Length / 2]:0}; " +
              "the pass 2b walk round the back of the chair: 243 median, up to 440)");
        Check(total[total.Length / 2] <= 240f,
              $"Out of a seat they turn about as much as the way out needs: {total[total.Length / 2]:0} deg in all at the median, stand-up to 1.5 s on (pass 2b: 340)");
        if (chairs.Count > 0)
        {
            float[] co = chairs.Select(r => r.oneWay).OrderBy(x => x).ToArray();
            float[] ct = chairs.Select(r => r.total).OrderBy(x => x).ToArray();
            Note($"From chairs only ({chairs.Count}): largest one-way turn median {co[co.Length / 2]:0}, worst {co[co.Length - 1]:0}; turned in all median {ct[ct.Length / 2]:0}.");
        }
        Check(walkingSeconds < 20f || flickers / (walkingSeconds / 60f) <= .5f,
              $"The walk clip does not flicker off and on while people set off or carry on ({flickers} in {walkingSeconds / 60f:0.0} min of walking; pass 2b had about 3 a minute)"
              + (flickerNotes.Count > 0 ? " - " + string.Join("; ", flickerNotes.Take(4)) : ""));
    }

    // Walk clip off and back on within 0.6 s while the body kept moving: the
    // stutter every set-off after a turn used to have.
    private sealed class FlickerWatch { public bool walk; public float offAt = -1f, speed; public Vector3 lastPosition; public bool seen; }
    private static readonly Dictionary<NpcLocomotion, FlickerWatch> flicker = new();
    private static readonly List<string> flickerNotes = new();
    private static int flickers;
    private static float walkingSeconds;

    private static void WatchFlicker(float dt)
    {
        if (dt <= 0f) return;
        foreach (NpcLocomotion loco in NpcLocomotion.All)
        {
            if (loco == null || !loco.isActiveAndEnabled) continue;
            Animator animator = loco.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isActiveAndEnabled) continue;
            if (!flicker.TryGetValue(loco, out FlickerWatch f)) flicker[loco] = f = new FlickerWatch();
            Vector3 position = loco.transform.position;
            if (f.seen)
            {
                float v = Flat(position - f.lastPosition).magnitude / dt;
                f.speed = Mathf.Lerp(f.speed, Mathf.Min(v, 3f), 1f - Mathf.Exp(-dt / .1f));
            }
            f.lastPosition = position;
            f.seen = true;
            bool walk = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).shortNameHash == WalkState
                : animator.GetCurrentAnimatorStateInfo(0).shortNameHash == WalkState;
            if (f.speed > .3f) walkingSeconds += dt;
            if (f.walk && !walk && f.speed > .3f) f.offAt = Time.time;
            if (!f.walk && walk && f.offAt >= 0f)
            {
                if (Time.time - f.offAt < .6f)
                {
                    flickers++;
                    if (flickerNotes.Count < 8) flickerNotes.Add($"{loco.name.Replace("(Clone)", "")} at t={Time.time:0.0} ({loco.Purpose})");
                }
                f.offAt = -1f;
            }
            f.walk = walk;
        }
    }

    private static void WatchWalkPast(ref float closest, ref string note)
    {
        var seatedBodies = Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude).Where(s => s.IsSeated).ToArray();
        foreach (NpcLocomotion loco in Object.FindObjectsByType<NpcLocomotion>(FindObjectsInactive.Exclude))
        {
            if (!loco.IsMoving || loco.Speed < .3f) continue;
            foreach (NpcSeating s in seatedBodies)
            {
                float d = Flat(loco.transform.position - s.transform.position).magnitude;
                if (d < closest) { closest = d; note = $"({loco.name} past {s.name} at t={Time.time:0.0})"; }
            }
        }
    }

    private static List<(string name, NpcSeating seating, TableSeat seat)> SeatedIn(TableSeat[] seats)
    {
        var list = new List<(string, NpcSeating, TableSeat)>();
        foreach (NpcSeating s in Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude))
            if (s.IsSeated && s.Current == NpcSeating.Phase.Seated && s.Seat != null && seats.Contains(s.Seat))
                list.Add((s.name.Replace("(Clone)", ""), s, s.Seat));
        return list;
    }

    private static Component Dummy(string name)
    {
        var go = new GameObject("Café life check - " + name);
        temporary.Add(go);
        return go.transform;
    }

    // A seated talking turn played by the Mixamo beats (NpcBeats), when the clips are there.
    private static bool TalkBeat(NpcSocial s)
    {
        NpcBeats beats = s != null ? s.GetComponent<NpcBeats>() : null;
        return beats != null && beats.CurrentKind == NpcBeats.Kind.Talk;
    }

    private static float Bearing(Transform from, Vector3 to)
    {
        Vector3 d = to - from.position; d.y = 0f;
        return Vector3.SignedAngle(from.forward, d, Vector3.up);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    private static void Note(string what) => report.AppendLine("NOTE  " + what);

    private static void Finish(string prefix)
    {
        string verdict = failures == 0 ? "PASS" : "FAIL";
        string text = $"Café life check - {verdict} ({checks - failures} of {checks})\n{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{report}";
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcLife"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"{prefix}-{DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.txt");
        File.WriteAllText(file, text);
        if (failures == 0) Debug.Log($"[Café life check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
        else Debug.LogWarning($"[Café life check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
    }
}
#endif
