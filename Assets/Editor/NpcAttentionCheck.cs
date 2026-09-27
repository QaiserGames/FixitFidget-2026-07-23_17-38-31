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

// Attention, measured live in Play Mode (pass 1b, 26 Sept 2026).
//
// Puts Ace where a player would be and watches what the customers do:
//   * at the counter: do queued customers look at him (head), and do their
//     bodies stay put when he moves along the counter?
//   * in a conversation: does the customer square up to him?
//   * delivery: when he walks over carrying THEIR drink, do they follow him
//     with their eyes, turn to meet him (standing) or keep still (seated),
//     and hold the look through the hand-over with the thank-you gesture?
// Also: the queue does not stand in identical yaws on identical spots.
//
// Run it in a lab session with the autopilot OFF (Fixit Fidget > Café life >
// Lab > Play a lab session - Day 5, I serve), so nothing else serves the
// customers it uses. Ace is put back where he was. Nothing is saved.
// Report: <project>/Logs/NpcAttention/attention-check-<time>.txt.
public static class NpcAttentionCheck
{
    private const string Menu = "Fixit Fidget/NPC/Attention check (Play Mode)";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(CustomerBrain).GetField("state", Any);

    // A stack, so "yield return Wait(x)" really waits (nested routines).
    private static readonly Stack<IEnumerator> routine = new();
    private static readonly StringBuilder report = new();
    private static int checks, failures;
    private static Vector3 aceStart;
    private static Quaternion aceStartRotation;
    private static bool aceMoved;
    private static GameObject cup;

    [MenuItem(Menu)]
    private static void Run()
    {
        report.Clear();
        checks = failures = 0;
        aceMoved = false;
        cup = null;
        routine.Clear();
        routine.Push(Sequence());
        EditorApplication.update += Step;
        Debug.Log("[Attention check] Watching the café...");
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
        routine.Clear();
        Finish();
    }

    private static IEnumerator Sequence()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "The open scene has a DayClock"); yield break; }
        if (clock.DayOver) { Check(false, "A day is open (press Open Tomorrow on the recap, then run the check again)"); yield break; }
        if (clock.TimeRemaining < 240f)
            typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining))?.SetValue(clock, 240f);
        if (CafeLab.Active && CafeLabDirector.Instance != null && CafeLabDirector.Instance.Autopilot)
            Note("The lab autopilot is ON; it may take the customers this check wants. Prefer the 'I serve' lab session.");

        PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>();
        PlayerCarry carry = ace != null ? ace.GetComponent<PlayerCarry>() : null;
        var conversation = Object.FindAnyObjectByType<ConversationController>();
        CounterQueue queue = Object.FindAnyObjectByType<CounterQueue>();
        if (ace == null || carry == null || queue == null) { Check(false, "The scene has a player with PlayerCarry, and a CounterQueue"); yield break; }
        aceStart = ace.transform.position;
        aceStartRotation = ace.transform.rotation;

        // ---------- 1. the counter ----------
        float deadline = Time.time + 90f;
        List<CustomerBrain> queued = Queued();
        while (queued.Count == 0 && Time.time < deadline) { yield return null; queued = Queued(); }
        if (queued.Count == 0) { Check(false, "Somebody queued at the counter within 90 s"); yield break; }

        Transform slot = queue.SlotPoint(queued[0].SlotIndex);
        Vector3 standPoint = CounterStandPoint(queue, slot);
        // Ace first stands well along the counter, away from this customer (he
        // may start right behind it), then comes over to serve them. Pass 2b:
        // the one he is about to serve looks up and stays attentive (eyes on him
        // with short look-aways); the rest of the line look up now and then but
        // never stare.
        MoveAce(ace, standPoint + slot.right * 4.5f, Quaternion.LookRotation(-slot.forward, Vector3.up));
        aceMoved = true;
        yield return Wait(2.5f);
        queued = Queued();
        if (queued.Count == 0) { Check(false, "Somebody was still queueing when Ace came over"); yield break; }
        slot = queue.SlotPoint(queued[0].SlotIndex);
        standPoint = CounterStandPoint(queue, slot);
        CustomerBrain served = queued[0];
        MoveAce(ace, standPoint, Quaternion.LookRotation(-slot.forward, Vector3.up));
        var bodyYaw = new Dictionary<CustomerBrain, float>();
        foreach (CustomerBrain c in queued) bodyYaw[c] = c.transform.eulerAngles.y;
        bool lookedUp = false;
        float t0 = Time.time;
        while (Time.time - t0 < 2f && served != null)
        {
            NpcSocial social = served.GetComponent<NpcSocial>();
            NpcLookAt look = served.GetComponent<NpcLookAt>();
            if (social != null && look != null && social.LookingAtPlayer && look.Weight > .3f) lookedUp = true;
            yield return null;
        }
        if (served != null)
        {
            Vector3 across = ace.transform.position - served.transform.position; across.y = 0f;
            Note($"{served.CustomerName}: Ace across the counter at {across.magnitude:0.00} m, {Mathf.Abs(Bearing(served.transform, ace.transform.position)):0}° off their facing.");
        }
        Check(served != null && lookedUp, $"The customer Ace comes over to serve looks up at him within 2 s ({(served != null ? served.CustomerName : "-")})");

        // He stands there for 10 s: the one he is serving keeps most of their
        // attention on him but looks away at least once; nobody else stares.
        var onHim = new Dictionary<CustomerBrain, float>();
        int samples = 0;
        t0 = Time.time;
        while (Time.time - t0 < 10f)
        {
            samples++;
            foreach (CustomerBrain c in queued)
            {
                if (c == null || State(c) != "WaitingInQueue") continue;
                NpcSocial social = c.GetComponent<NpcSocial>();
                if (social != null && social.LookingAtPlayer) onHim[c] = onHim.GetValueOrDefault(c) + 1f;
            }
            yield return null;
        }
        int starers = 0, others = 0;
        float servedShare = -1f;
        foreach (CustomerBrain c in queued)
        {
            if (c == null || State(c) != "WaitingInQueue") continue;
            float share = onHim.GetValueOrDefault(c) / Mathf.Max(1, samples);
            if (c == served) servedShare = share;
            else { others++; if (share > .6f) starers++; }
            Note($"{c.CustomerName} in slot {c.SlotIndex}{(c == served ? " (being served)" : "")}: eyes on Ace {share * 100f:0}% of the 10 s.");
        }
        Check(servedShare < 0f || servedShare >= .4f && servedShare <= .97f,
              $"The customer being served is attentive but not a statue ({(servedShare >= 0f ? servedShare * 100f : 0f):0}% eyes on Ace, want 40-97%)");
        Check(starers == 0, $"Nobody else in the queue stares at Ace ({starers} of {others} on him more than 60% of 10 s)");

        // Queue variation: not all on the slot centre, not all the same yaw.
        int offSlot = 0, yaws = 0, varied = 0, stillThere = 0;
        foreach (CustomerBrain c in queued)
        {
            if (c == null || State(c) != "WaitingInQueue") continue;
            stillThere++;
            Transform s = queue.SlotPoint(c.SlotIndex);
            Vector3 d = c.transform.position - s.position; d.y = 0f;
            bool off = d.magnitude > .04f, turned = Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.y, s.eulerAngles.y)) > 2f;
            if (off) offSlot++;
            if (turned) yaws++;
            if (off || turned) varied++;
            Note($"{c.CustomerName}: {d.magnitude * 100f:0} cm from the slot centre, turned {Mathf.DeltaAngle(s.eulerAngles.y, c.transform.eulerAngles.y):0}°.");
        }
        // Each person differs from the slot's exact spot-and-yaw in some way (the
        // navmesh can snap a small offset back onto the centre line).
        Check(varied == stillThere,
              $"Nobody in the queue stands on the exact slot point at the exact slot yaw ({offSlot} off-centre, {yaws} turned, {varied} of {stillThere} varied)");

        // Ace moves a metre along the counter: heads follow, bodies stay.
        foreach (CustomerBrain c in queued) if (c != null) bodyYaw[c] = c.transform.eulerAngles.y;
        MoveAce(ace, standPoint + slot.right * 1.0f, Quaternion.LookRotation(-slot.forward, Vector3.up));
        yield return Wait(2f);
        int bodiesStill = 0, heads = 0;
        foreach (CustomerBrain c in queued)
        {
            if (c == null || !bodyYaw.ContainsKey(c) || State(c) != "WaitingInQueue") continue;
            float turned = Mathf.Abs(Mathf.DeltaAngle(bodyYaw[c], c.transform.eulerAngles.y));
            if (turned < 12f) bodiesStill++;
            NpcLookAt look = c.GetComponent<NpcLookAt>();
            if (look != null && look.Looking) heads++;
            Note($"{c.CustomerName}: body turned {turned:0}° while Ace moved 1 m; head {(look != null && look.Looking ? "following" : "not following")}.");
        }
        int stillQueued = queued.Count(c => c != null && State(c) == "WaitingInQueue");
        Check(stillQueued == 0 || bodiesStill == stillQueued, $"Bodies do not spin after Ace as he moves along the counter ({bodiesStill} of {stillQueued} still)");

        // ---------- 2. a conversation ----------
        CustomerBrain partner = Queued().FirstOrDefault(c => c.CanHearIntake || c.CanDecide);
        if (partner != null && conversation != null)
        {
            MoveAce(ace, CounterStandPoint(queue, queue.SlotPoint(partner.SlotIndex)), Quaternion.LookRotation(-slot.forward, Vector3.up));
            yield return Wait(.3f);
            conversation.Begin(partner);
            yield return Wait(1.8f);
            float facing = Mathf.Abs(Bearing(partner.transform, ace.transform.position));
            NpcLookAt look = partner.GetComponent<NpcLookAt>();
            Check(conversation.InConversation && facing < 35f && look != null && look.Looking,
                  $"In conversation {partner.CustomerName} faces Ace (body {facing:0}° off, head {(look != null && look.Looking ? "on him" : "elsewhere")})");
            conversation.End();
            yield return Wait(.6f);
        }
        else Note("No customer could be talked to; the conversation step was skipped.");

        // ---------- 3. a delivery ----------
        // Take jobs so someone goes to wait, then wait for a drink order.
        int taken = 0;
        deadline = Time.time + 100f;
        CustomerBrain target = null;
        while (Time.time < deadline)
        {
            if (taken < 3)
                foreach (CustomerBrain c in Queued())
                {
                    if (c.IsCounterRepair) continue;
                    if (c.CanHearIntake) c.HearIntake();
                    if (c.CanAcceptJob) { c.AcceptJob(); taken++; Note($"Took a job for {c.CustomerName}."); break; }
                }
            target = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => !c.IsLeaving && State(c) == "Waiting" && c.HasDrinkOrder && c.AwaitingDrink && c.WantedDrink != null);
            if (target != null) break;
            yield return Wait(.5f);
        }
        if (target == null) { Check(false, "A waiting customer with a drink order appeared within 100 s"); yield break; }
        if (carry.Count > 0) { Check(false, "Ace's hands are empty for the delivery step"); yield break; }

        cup = new GameObject("Attention check cup");
        DrinkJob drink = cup.AddComponent<DrinkJob>();
        drink.SetDrink(target.WantedDrink, true);
        if (!carry.TryPickUp(drink)) { Check(false, "Ace can pick up the test cup"); yield break; }
        NpcSeating seating = target.GetComponent<NpcSeating>();
        // Let them finish sitting down (or settle standing) before the delivery.
        deadline = Time.time + 10f;
        while (seating != null && seating.Busy && !seating.IsSeated && Time.time < deadline) yield return null;
        yield return Wait(.5f);
        bool seated = seating != null && seating.IsSeated;
        NpcLookAt eyes = target.GetComponent<NpcLookAt>();
        Note($"Delivering {target.WantedDrink.drinkName} to {target.CustomerName}, who is {(seated ? "seated" : "standing")}.");

        Vector3 away = ApproachPoint(target.transform, 4f);
        MoveAce(ace, away, Quaternion.LookRotation(Flat(target.transform.position - away), Vector3.up));
        yield return Wait(1.4f);
        float farDistance = Flat(ace.transform.position - target.transform.position).magnitude;
        Note($"Ace at {farDistance:0.0} m, bearing {Bearing(target.transform, ace.transform.position):0}°; head weight {(eyes != null ? eyes.Weight : 0f):0.00}, turned {(eyes != null ? eyes.AppliedYaw : 0f):0}°.");
        Check(eyes != null && eyes.Looking, $"{target.CustomerName} follows Ace with the eyes when he comes over with the order (from {farDistance:0.0} m)");

        float yawBefore = target.transform.eulerAngles.y;
        Vector3 near = ApproachPoint(target.transform, 1.9f);
        MoveAce(ace, near, Quaternion.LookRotation(Flat(target.transform.position - near), Vector3.up));
        yield return Wait(2.2f);
        Note($"Ace at {Flat(ace.transform.position - target.transform.position).magnitude:0.0} m, bearing {Bearing(target.transform, ace.transform.position):0}°; head weight {(eyes != null ? eyes.Weight : 0f):0.00}, turned {(eyes != null ? eyes.AppliedYaw : 0f):0}°.");
        if (seated)
        {
            float moved = Mathf.Abs(Mathf.DeltaAngle(yawBefore, target.transform.eulerAngles.y));
            Check(moved < 3f && eyes != null && eyes.Looking, $"Seated, {target.CustomerName} turns only the head to Ace (body moved {moved:0}°)");
        }
        else
        {
            float off = Mathf.Abs(Bearing(target.transform, ace.transform.position));
            Check(off < 35f, $"Standing, {target.CustomerName} turns to meet Ace when he is close (body {off:0}° off him)");
        }

        Animator animator = target.GetComponentInChildren<Animator>();
        string line = target.ServeDrink(carry);
        Check(!string.IsNullOrEmpty(line) && !carry.Contains(drink), $"The drink changes hands (\"{line}\")");
        bool gesture = false;
        float until = Time.time + 1.2f;
        while (Time.time < until)
        {
            if (!seated && animator != null &&
                (animator.GetCurrentAnimatorStateInfo(0).IsName("Interact") || animator.GetNextAnimatorStateInfo(0).IsName("Interact")))
                gesture = true;
            yield return null;
        }
        Check(eyes != null && eyes.Looking, $"{target.CustomerName} keeps looking at Ace through the hand-over");
        if (!seated) Check(gesture, $"{target.CustomerName} makes the hand-over gesture");
        else Note("Seated: the standing gesture is not expected.");
    }

    // ---------- helpers ----------

    private static List<CustomerBrain> Queued() =>
        Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)
            .Where(c => c.SlotIndex >= 0 && State(c) == "WaitingInQueue").OrderBy(c => c.SlotIndex).ToList();

    private static string State(CustomerBrain c) => c == null || StateField == null ? "" : StateField.GetValue(c).ToString();

    private static Vector3 CounterStandPoint(CounterQueue queue, Transform slot)
    {
        foreach (StationInteractable station in Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
            if (station.name.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 && station.StandPoint != null)
            {
                Vector3 p = station.StandPoint.position;
                // Stand across from the customer in question rather than the station's centre.
                p.x = Mathf.Lerp(p.x, slot.position.x, .8f);
                return p;
            }
        return slot.position + slot.forward * 1.3f;
    }

    // A point Ace can stand on, `distance` from the customer, as much in front
    // of them as the furniture allows (a delivery comes to your face, not your
    // back): their own forward first, then swinging round in 30° steps.
    private static Vector3 ApproachPoint(Transform customer, float distance)
    {
        Vector3 from = customer.position;
        Vector3 forward = Flat(customer.forward).normalized;
        Vector3 best = from + forward * distance;
        foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f })
        {
            Vector3 d = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 probe = from + d * distance;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .7f, NavMesh.AllAreas)) continue;
            float got = Flat(hit.position - from).magnitude;
            if (got < distance * .75f || got > distance * 1.2f) continue;
            return hit.position;
        }
        if (NavMesh.SamplePosition(best, out NavMeshHit any, 2f, NavMesh.AllAreas)) return any.position;
        return best;
    }

    private static void MoveAce(PlayerInteractor ace, Vector3 at, Quaternion rotation)
    {
        var controller = ace.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        at.y = ace.transform.position.y;   // the capsule's pivot height
        ace.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        if (controller != null) controller.enabled = was;
    }

    private static float Bearing(Transform from, Vector3 to) => Vector3.SignedAngle(Flat(from.forward), Flat(to - from.position), Vector3.up);

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

    private static void Finish()
    {
        if (cup != null) Object.Destroy(cup);
        if (aceMoved)
        {
            PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>();
            if (ace != null) MoveAce(ace, aceStart, aceStartRotation);
        }
        string verdict = $"[Attention check] {(failures == 0 && checks > 0 ? "PASS" : "FAIL")} {checks - failures}/{checks}";
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "NpcAttention");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "attention-check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        File.WriteAllText(path, verdict + "\n" + report, new UTF8Encoding(false));
        string message = verdict + " — report: " + path + "\n" + report;
        if (failures == 0 && checks > 0) Debug.Log(message);
        else Debug.LogWarning(message);
    }
}
#endif
