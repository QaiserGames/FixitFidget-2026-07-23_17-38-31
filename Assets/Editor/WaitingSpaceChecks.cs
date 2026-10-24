#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Waiting spaces: the reservation rules on synthetic spots, then (playtest 4, 10 Oct 2026) the café scene's own waiting
// rules when it is the open scene:
//   * every walk-in archetype and every regular waits in a seat (the Impatient and Rushed stood by choice, at x1.15, and
//     17 of 18 standing waiters were lost; Playtest 4 > Systems > 1 seats them);
//   * every seat drains at a seat's rate (at most 0.6), the bar stools too (a customer waits standing beside a bar stool,
//     Snap To Seat off, but it is a seat's wait);
//   * the repair bar's stools are within E's reach over the counter from the bench's stand point
//     (PlayerInteractor.acrossCounterReach), so a hand-back there is no walk round the counter.
//
// THE STOOLS NOBODY USED (12 Oct 2026; the veteran, run 2: "Only the x -3.2 stool ever took a waiting customer: 4 of 4
// repair-bar waits; 0 at -4.2 and -5.2"). WaitingArea adds 2.5 m to a seat for each person who has claimed a seat within
// 2 m of it and is "not sitting yet" (so a group spreads over the room). Not sitting yet was NpcSeating.Busy alone, and
// at a bar stool nobody ever sits (Snap To Seat off), so whoever held the first stool counted as still on the way for
// their whole wait and the two stools beside them carried the penalty the whole time: from the counter's middle and west
// places that put the second stool behind the nearest floor seat. Now someone standing on their stand point has arrived
// (WaitingArea.HasArrived). Checked here on synthetic spots (arrived / still walking), and in the café scene: the three
// stools' stand points are far enough apart to be claimed together, and with the east stool held by someone standing at
// it, the middle stool carries no on-the-way count (before: 1 for the whole wait). Since 23 Oct 2026 seats are chosen by
// their reasons (SeatChoice): a claim from someone with no repair on the bench is given a floor seat while one is free
// (the stools are kept for repairs, and the middle one is beside a stranger); bench jobs reach the stools through
// ClaimRepairBar, checked below.
//
// THE REPAIR BAR FIRST (14 Oct 2026, NPC wave 2; his girlfriend: "people wouldn't sit down on the bar stools that are
// infront of the repair station"). The stools are found by TableSeat.RepairBar now (they sit, so Snap To Seat no longer
// tells them apart), and three more rules are checked: the three are flagged; from every counter place, three bench jobs in
// turn take the middle, east and west stools (WaitingArea.ClaimRepairBar), and a fourth gets nothing from the bar (the
// best seat for them then, WaitingArea.Claim); a drink claim from place 0 gets one of the best seats for someone with no
// leanings (23 Oct 2026: the walk alone, the stools kept for repairs, a seat nobody sits on waited at standing), never a
// repair-bar stool nor a standing wait.
public static class WaitingSpaceChecks
{
    const float SeatDrain = .6f;
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Fixit Fidget/Checks/Waiting space reservations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before checking waiting spaces.");
        var scene = EditorSceneManager.NewPreviewScene();
        WaitingSpot seatA = null, seatB = null, stoolA = null, stoolB = null;
        try
        {
            GameObject host = new GameObject("Reservation checks");
            SceneManager.MoveGameObjectToScene(host, scene);
            GameObject a = new GameObject("Seat A"), b = new GameObject("Seat B");
            GameObject first = new GameObject("First occupant"), second = new GameObject("Second occupant");
            foreach (var go in new[] { a, b, first, second }) go.transform.SetParent(host.transform);
            // Keep these synthetic spots away from the loaded gameplay scene.
            a.transform.position = b.transform.position = new Vector3(10000, 0, 10000);
            seatA = a.AddComponent<WaitingSpot>();
            seatB = b.AddComponent<WaitingSpot>();
            // Spots join WaitingArea's registry from OnEnable and leave it from
            // OnDisable. Edit mode never sends those messages to a script
            // without ExecuteAlways, so the check sends them itself — the
            // bodies that run are the real ones.
            Lifecycle(seatA, "OnEnable");
            Lifecycle(seatB, "OnEnable");
            Require(seatA.Claim(first.transform), "First claim succeeds.");
            Require(!seatA.Claim(second.transform), "One spot cannot have two occupants.");
            Require(!seatB.Claim(second.transform), "Duplicate markers cannot hold different bodies.");
            b.transform.position += Vector3.right * 0.4f;
            Require(!seatB.Claim(second.transform), "Nearby markers still overlap.");
            b.transform.position += Vector3.right * 2;
            Require(seatB.Claim(second.transform), "Separate seats remain usable.");
            seatA.Release(second.transform);
            Require(seatA.Occupant == first.transform, "Someone else cannot release a reservation.");
            seatB.Release(second.transform);
            b.transform.position = a.transform.position;
            a.SetActive(false);
            Lifecycle(seatA, "OnDisable");
            Require(seatB.Claim(second.transform), "Disabled seat releases its physical reservation.");
            Debug.Log("[Waiting space] PASS: shared occupancy, overlapping markers, release and disable.");

            // The stools (12 Oct): two spots a metre apart, as the repair bar's are; someone standing on the first's stand
            // point has arrived, so the second carries no "still on the way" penalty; someone still walking over does.
            GameObject sa = new GameObject("Stool A"), sb = new GameObject("Stool B"), stander = new GameObject("Standing at stool A");
            foreach (var go in new[] { sa, sb, stander }) go.transform.SetParent(host.transform);
            sa.transform.position = new Vector3(10050, 0, 10000);
            sb.transform.position = sa.transform.position + Vector3.right;
            stoolA = sa.AddComponent<WaitingSpot>();
            stoolB = sb.AddComponent<WaitingSpot>();
            Lifecycle(stoolA, "OnEnable");
            Lifecycle(stoolB, "OnEnable");
            MethodInfo onTheirWay = typeof(WaitingArea).GetMethod("OnTheirWayNear", Hidden);
            Require(onTheirWay != null, "WaitingArea has OnTheirWayNear (the check reads it by name).");
            stander.transform.position = sa.transform.position;              // on the stand point: arrived, chair or no chair
            Require(stoolA.Claim(stander.transform), "The first stool is claimed by someone standing at it.");
            Require(WaitingArea.HasArrived(stoolA), "Standing on the stand point counts as arrived (no seating component needed).");
            int penalised = (int)onTheirWay.Invoke(null, new object[] { stoolB, second.transform });
            Require(penalised == 0, $"The stool beside someone who has arrived carries no on-the-way penalty (counted {penalised}; before 12 Oct it was 1 for the whole wait).");
            stander.transform.position = sa.transform.position + Vector3.forward * 1.5f;   // still walking over
            Require(!WaitingArea.HasArrived(stoolA), "Someone 1.5 m from the stand point has not arrived.");
            penalised = (int)onTheirWay.Invoke(null, new object[] { stoolB, second.transform });
            Require(penalised == 1, $"The stool beside someone still walking over carries the penalty (counted {penalised}).");
            stoolA.Release(stander.transform);
            Debug.Log("[Waiting space] PASS: arrived at a stool (standing on the stand point) against still on the way.");
        }
        finally
        {
            // Leave the static registry as it was found.
            if (seatA != null) WaitingArea.Unregister(seatA);
            if (seatB != null) WaitingArea.Unregister(seatB);
            if (stoolA != null) WaitingArea.Unregister(stoolA);
            if (stoolB != null) WaitingArea.Unregister(stoolB);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        CafeScene();
    }

    // The café scene's waiting rules (playtest 4), when it is the open scene. Read-only.
    private static void CafeScene()
    {
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.Log("[Waiting space] The café scene isn't the open scene, so its waiting rules weren't checked.");
            return;
        }
        var failures = new List<string>();
        var passes = new List<string>();
        void Expect(bool ok, string what) => (ok ? passes : failures).Add(what);

        CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        Expect(spawner != null, "the café has a CustomerSpawner");
        if (spawner != null)
        {
            SerializedProperty archetypes = new SerializedObject(spawner).FindProperty("archetypes");
            Expect(archetypes != null && archetypes.arraySize > 0, "the spawner has walk-in archetypes");
            for (int i = 0; archetypes != null && i < archetypes.arraySize; i++)
            {
                SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
                var kind = (WaitingSpot.SpotKind)a.FindPropertyRelative("preferredWaitKind").enumValueIndex;
                Expect(kind == WaitingSpot.SpotKind.Seat,
                    $"the {a.FindPropertyRelative("archetypeName").stringValue} walk-ins wait in a seat (they wait: {kind}; Playtest 4 > Systems > 1)");
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile != null)
                Expect(profile.preferredWaitKind != WaitingSpot.SpotKind.Loiter, $"{profile.name} doesn't wait standing (waits: {profile.preferredWaitKind})");
        }

        var seats = UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s != null && s.isActiveAndEnabled).ToList();
        Expect(seats.Count > 0, $"the café has seats ({seats.Count})");
        foreach (TableSeat seat in seats)
            Expect(seat.Kind == WaitingSpot.SpotKind.Seat && seat.DrainMultiplier <= SeatDrain + .001f,
                $"{seat.name} drains at a seat's rate (x{seat.DrainMultiplier:0.##}{(seat.RepairBar ? ", a repair-bar stool: they sit on it, raised" : seat.SnapToSeat ? "" : ", a window-counter stool: they wait standing beside it")})");

        // The repair bar: its stools, across the counter from the bench, within E's reach over it.
        PlayerInteractor ace = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        StationInteractable bench = UnityEngine.Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude)
            .FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        SerializedProperty reachProp = ace != null ? new SerializedObject(ace).FindProperty("acrossCounterReach") : null;
        Expect(ace != null && bench != null && reachProp != null, "Ace's reach over the counter and the repair bench are found");
        var barStools = new List<TableSeat>();
        if (bench != null && reachProp != null)
        {
            float reach = reachProp.floatValue;
            Vector3 from = bench.StandPoint.position, toward = bench.StandPoint.forward;
            toward.y = 0f;
            toward.Normalize();
            // The repair bar's stools by their flag (NPC wave 2: they sit now, so Snap To Seat no longer tells them apart).
            var inFront = seats.Where(s => s.RepairBar).Select(s => (seat: s, d: Flat(s.StandPoint.position - from))).ToList();
            Expect(inFront.Count == 3, $"the three repair-bar stools are flagged Repair Bar ({inFront.Count}; Fixit Fidget > NPC > Wave 2 1)");
            foreach (var x in inFront)
                Expect(Vector3.Dot(x.d, toward) > .5f && x.d.magnitude <= reach,
                    $"{x.seat.name} is {x.d.magnitude:0.00} m from the bench's stand point, in front of it: within E's reach over the counter ({reach:0.0} m)");
            barStools = inFront.Select(x => x.seat).OrderByDescending(s => s.StandPoint.position.x).ToList();   // east first: nearest the till
        }

        // The stools nobody used (12 Oct): they can be claimed together (their stand points clear each other's footprint), and
        // with the east one held by someone standing at it, the middle one carries no on-the-way count.
        if (barStools.Count >= 2)
        {
            for (int i = 0; i < barStools.Count; i++)
                for (int j = i + 1; j < barStools.Count; j++)
                {
                    float apart = Flat(barStools[i].StandPoint.position - barStools[j].StandPoint.position).magnitude;
                    float needed = Footprint(barStools[i]) + Footprint(barStools[j]) + .1f;
                    Expect(apart >= needed, $"{barStools[i].name} and {barStools[j].name} stand {apart:0.00} m apart, clear of each other's footprint ({needed:0.00} m): both can be taken");
                }
            TheSecondStool(barStools, Expect);
        }
        if (barStools.Count == 3) TheRepairBarFirst(bench, barStools, Expect);

        string text = $"[Waiting space] The café scene: {passes.Count} passed, {failures.Count} failed.\n" +
                      string.Join("\n", failures.Select(f => "FAIL  " + f).Concat(passes.Select(p => "PASS  " + p)));
        if (failures.Count > 0)
        {
            Debug.LogError(text);
            throw new InvalidOperationException($"{failures.Count} of the café scene's waiting rules failed: " + failures[0]);
        }
        Debug.Log(text);
    }

    // A customer at the counter claims a seat with the east stool held by someone STANDING at it (a bar stool is waited at
    // standing). The scene's own WaitingArea does the choosing. 23 Oct 2026: the stand-in has no repair on the bench, so the
    // stools are kept for repairs (SeatChoice.KeptForRepairs) and the middle one is beside a stranger: from the place nearest
    // the stools it is given a floor seat (the middle stool starts 4.5 points under any floor seat, before the walks). What
    // 12 Oct fixed is read directly: the middle stool's on-the-way count is 0 for a stander who has arrived. The café's spots
    // are registered for the test (Edit Mode sends no OnEnable), the two stand-in bodies live in a preview scene, and
    // everything is released after it.
    private static void TheSecondStool(List<TableSeat> barStools, Action<bool, string> expect)
    {
        WaitingArea area = UnityEngine.Object.FindAnyObjectByType<WaitingArea>(FindObjectsInactive.Exclude);
        CounterQueue queue = UnityEngine.Object.FindAnyObjectByType<CounterQueue>(FindObjectsInactive.Exclude);
        expect(area != null && queue != null && queue.SlotCount > 0, "the café has a WaitingArea and a CounterQueue with places");
        if (area == null || queue == null || queue.SlotCount == 0) return;
        TableSeat east = barStools[0], middle = barStools[1];
        var registered = new List<WaitingSpot>();
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject holder = new GameObject("Waiting space check: standing at the east stool");
        GameObject customer = new GameObject("Waiting space check: a customer at the counter");
        SceneManager.MoveGameObjectToScene(holder, preview);
        SceneManager.MoveGameObjectToScene(customer, preview);
        try
        {
            foreach (WaitingSpot spot in UnityEngine.Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
            {
                if (spot == null || !spot.isActiveAndEnabled) continue;
                int before = WaitingArea.RegisteredSpots;
                WaitingArea.Register(spot);
                if (WaitingArea.RegisteredSpots > before) registered.Add(spot);
            }
            holder.transform.position = east.StandPoint.position;
            expect(east.Claim(holder.transform), $"{east.name} (the east stool) is held by someone standing at it");
            expect(WaitingArea.HasArrived(east), $"standing on {east.name}'s stand point counts as arrived (12 Oct; before, a stander never did)");

            // The counter place nearest the stools (the west one) must give the middle stool; the others are reported.
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform slot = queue.SlotPoint(i);
                if (slot == null) continue;
                float d = Flat(slot.position - east.StandPoint.position).magnitude;
                if (d < nearestDistance) { nearestDistance = d; nearest = i; }
            }
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform slot = queue.SlotPoint(i);
                if (slot == null) continue;
                customer.transform.position = slot.position;
                WaitingSpot given = area.Claim(customer.transform, WaitingSpot.SpotKind.Seat);
                float toMiddle = Flat(middle.StandPoint.position - slot.position).magnitude;
                string line = $"from the counter's place {i} at ({slot.position.x:0.0}, {slot.position.z:0.0}) with the east stool held, the claim gives " +
                              $"{(given != null ? given.name : "nothing")} (the middle stool is {toMiddle:0.00} m away, straight)";
                if (i == nearest)
                {
                    TableSeat givenSeat = given as TableSeat;
                    expect(given != null && (givenSeat == null || !givenSeat.RepairBar),
                        line + ": a floor seat (23 Oct: no repair on the bench, so the stools are kept for repairs, and the middle one is beside a stranger)");
                    MethodInfo onTheirWay = typeof(WaitingArea).GetMethod("OnTheirWayNear", Hidden);
                    int counted = onTheirWay != null ? (int)onTheirWay.Invoke(null, new object[] { middle, customer.transform }) : -1;
                    expect(counted == 0, $"the middle stool beside the stander carries no on-the-way count (counted {counted}; before 12 Oct it was 1 for the whole wait)");
                }
                else expect(true, line + " (reported, not required)");
                if (given != null) given.Release(customer.transform);
            }
        }
        finally
        {
            foreach (WaitingSpot spot in UnityEngine.Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
                if (spot != null) { spot.Release(customer.transform); spot.Release(holder.transform); }
            foreach (WaitingSpot spot in registered) WaitingArea.Unregister(spot);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // THE REPAIR BAR FIRST (14 Oct 2026). From every counter place, three bench jobs in turn (three stand-in bodies there)
    // take the middle stool, then the east, then the west (the seat nearest the bench first; the east and west tie and go to
    // the one nearer the customer); a fourth gets nothing from the bar. Then a drink claim from place 0 by a stand-in with no
    // leanings: one of the seats SeatChoice scores best for them (23 Oct 2026: the walk alone, by the area's own walking
    // distance, the stools kept for repairs and a seat nobody sits on waited at standing; within its Nearly As Near, in
    // points), never a repair-bar stool nor a standing wait. Registered and released as TheSecondStool does.
    private static void TheRepairBarFirst(StationInteractable bench, List<TableSeat> barStools, Action<bool, string> expect)
    {
        WaitingArea area = UnityEngine.Object.FindAnyObjectByType<WaitingArea>(FindObjectsInactive.Exclude);
        CounterQueue queue = UnityEngine.Object.FindAnyObjectByType<CounterQueue>(FindObjectsInactive.Exclude);
        expect(area != null && queue != null && queue.SlotCount > 0 && bench != null && bench.StandPoint != null, "the café has a WaitingArea, a CounterQueue and the bench for the bar's order");
        if (area == null || queue == null || queue.SlotCount == 0 || bench == null || bench.StandPoint == null) return;
        string[] order =
        {
            barStools.First(s => s.name.Contains("middle")).name,
            barStools.First(s => s.name.Contains("east")).name,
            barStools.First(s => s.name.Contains("west")).name,
        };
        Vector3 benchPoint = bench.StandPoint.position;
        var registered = new List<WaitingSpot>();
        var people = new List<GameObject>();
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject Person(string name, Vector3 at)
        {
            var go = new GameObject("Waiting space check: " + name);
            SceneManager.MoveGameObjectToScene(go, preview);
            go.transform.position = at;
            people.Add(go);
            return go;
        }
        try
        {
            foreach (WaitingSpot spot in UnityEngine.Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
            {
                if (spot == null || !spot.isActiveAndEnabled) continue;
                int before = WaitingArea.RegisteredSpots;
                WaitingArea.Register(spot);
                if (WaitingArea.RegisteredSpots > before) registered.Add(spot);
            }
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform slot = queue.SlotPoint(i);
                if (slot == null) continue;
                var claimed = new List<(WaitingSpot spot, Transform who)>();
                var given = new List<string>();
                for (int k = 0; k < 3; k++)
                {
                    Transform who = Person($"bench job {k + 1} at place {i}", slot.position).transform;
                    WaitingSpot spot = area.ClaimRepairBar(who, benchPoint);
                    given.Add(spot != null ? spot.name : "nothing");
                    if (spot != null) claimed.Add((spot, who));
                }
                expect(given.SequenceEqual(order), $"from the counter's place {i} at ({slot.position.x:0.0}, {slot.position.z:0.0}), three bench jobs in turn take: " +
                                                   $"{string.Join(", then ", given)} (the middle, east and west stools wanted)");
                Transform fourth = Person($"a fourth bench job at place {i}", slot.position).transform;
                WaitingSpot none = area.ClaimRepairBar(fourth, benchPoint);
                expect(none == null, $"from place {i}, with the three taken the bar gives a fourth bench job {(none != null ? none.name : "nothing")} (nothing wanted: the best seat for them then)");
                if (none != null) none.Release(fourth);
                foreach (var c in claimed) c.spot.Release(c.who);
            }

            // A drink customer at place 0 (anyone alone at the counter), a stand-in with no leanings: the seats SeatChoice scores
            // best for them (23 Oct 2026), which for them is the walk alone, the stools kept for repairs, and the seats nobody sits
            // on (the window counter's, until they sit) waited at standing: scored exactly as WaitingArea.PickBest scores them.
            Transform zero = queue.SlotPoint(0);
            if (zero == null) return;
            Transform drinker = Person("a drink order at place 0", zero.position).transform;
            MethodInfo walking = typeof(WaitingArea).GetMethod("WalkingDistance", Hidden);
            SerializedProperty tieProp = new SerializedObject(area).FindProperty("nearlyAsNear");
            float tie = tieProp != null ? tieProp.floatValue : .6f;          // points (SeatChoice), since 23 Oct
            SeatChoice.Person nobody = SeatChoice.Make();
            var weighed = new List<(WaitingSpot seat, float score)>();
            if (walking != null)
                foreach (TableSeat candidate in UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
                             .Where(t => t.isActiveAndEnabled && t.IsAvailable && WaitingArea.HasClaimClearance(t, drinker)))
                {
                    float walk = (float)walking.Invoke(null, new object[] { zero.position, candidate.StandPoint.position });
                    var place = new SeatChoice.Seat(candidate.SeatPose.position, candidate.Tags, candidate.RepairBar, standing: !candidate.SnapToSeat);
                    weighed.Add((candidate, SeatChoice.Score(nobody, place, null, walk)));
                }
            float best = weighed.Count > 0 ? weighed.Max(w => w.score) : float.NegativeInfinity;
            var bestSet = weighed.Where(w => w.score >= best - tie).Select(w => w.seat).ToList();
            WaitingSpot drinkSeat = area.Claim(drinker, WaitingSpot.SpotKind.Seat);
            TableSeat drinkStool = drinkSeat as TableSeat;
            expect(drinkSeat != null && (walking == null || bestSet.Contains(drinkSeat)) && (drinkStool == null || (!drinkStool.RepairBar && drinkStool.SnapToSeat)),
                $"a drink claim from place 0 gets {(drinkSeat != null ? drinkSeat.name : "nothing")}: one of the best seats for someone with no leanings " +
                $"({string.Join(", ", bestSet.Select(s => s.name))}; within {tie:0.0} points, {tie / SeatChoice.WalkWeight:0.0} m of walking, of the best), " +
                "never a repair-bar stool nor a seat waited at standing");
            if (drinkSeat != null) drinkSeat.Release(drinker);
        }
        finally
        {
            foreach (WaitingSpot spot in UnityEngine.Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
                if (spot != null) foreach (GameObject p in people) if (p != null) spot.Release(p.transform);
            foreach (WaitingSpot spot in registered) WaitingArea.Unregister(spot);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // The room WaitingArea keeps round a bar stool's stand point for other claims (its own clearance radius; a stander's
    // body otherwise).
    private static float Footprint(TableSeat stool) => stool.ClearanceRadius > 0f ? stool.ClearanceRadius : .35f;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static void Lifecycle(WaitingSpot spot, string message)
    {
        MethodInfo method = typeof(WaitingSpot).GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null) throw new InvalidOperationException($"WaitingSpot has no {message}.");
        method.Invoke(spot, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
