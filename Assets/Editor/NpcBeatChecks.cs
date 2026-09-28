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

// The Mixamo beats in play (Step B, 27 Sept 2026), checked in a lab session.
//
// Fixit Fidget > NPC > Mixamo 5 - Beat check (Play Mode, lab session). Needs the
// clip library (Mixamo 3) and a lab session (Fixit Fidget > Café life > Lab), so the
// real playtest save is never touched. It:
//  1. seats patrons at a café chair and on the lounge seats, and brings customers
//     into the queue;
//  2. checks everyone has the beats (library loaded, override controller, Beats
//     layer) and every city body copies its fingers;
//  3. plays every standing clip on a customer in the queue and every seated clip on
//     a seated patron: each blends in, the body stays where it is, a seated body
//     stays on its seat with a city body's feet on the floor, not in it, the
//     phone is in the right hand for the two phone clips and
//     gone afterwards, the calling hand is at the face, and the sofa-only and
//     table-only clips keep to their seats;
//  4. makes them leave in the middle of a beat: it gets out of the way at once;
//  5. lets the café run with the autopilot for a minute and counts what played by
//     itself, the reactions, and any time a beat showed while the body was moving.
// While a clip is being tested the person's own ambient life is paused and their
// patience (or a patron's stay) is topped up, so nothing else interferes.
// Report: <project>/Logs/NpcMixamo/beat-check-<time>.txt.
public static class NpcBeatChecks
{
    private const string Menu = "Fixit Fidget/NPC/Mixamo 5 - Beat check (Play Mode, lab session)";
    private const string LoungeGroup = "Lounge seats (pass 2)";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo PatienceField = typeof(CustomerBrain).GetField("patienceLeft", Any);
    private static readonly FieldInfo LeaveAtField = typeof(PatronBrain).GetField("leaveAt", Any);
    private static readonly FieldInfo AmbientField = typeof(NpcSocial).GetField("ambient", Any);

    private static readonly Stack<IEnumerator> routine = new();
    private static readonly StringBuilder report = new();
    private static int checks, failures;
    private static readonly List<GameObject> temporary = new();
    private static readonly List<Component> holders = new();
    private static readonly List<NpcSocial> paused = new();
    private static CustomerBrain keepPatient;
    private static PatronBrain[] keepSeated = Array.Empty<PatronBrain>();
    private static bool autopilotBefore;

    [MenuItem(Menu)]
    private static void Run()
    {
        report.Clear();
        checks = failures = 0;
        temporary.Clear();
        holders.Clear();
        paused.Clear();
        keepPatient = null;
        keepSeated = Array.Empty<PatronBrain>();
        routine.Clear();
        routine.Push(Sequence());
        EditorApplication.update += Step;
        Debug.Log("[Beat check] Running in the lab session...");
    }

    [MenuItem(Menu, true)]
    private static bool CanRun() => EditorApplication.isPlaying && routine.Count == 0;

    private static void Step()
    {
        bool running = EditorApplication.isPlaying;
        try
        {
            KeepSubjectsStill();
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
        Cleanup();
        Finish();
    }

    private static void Cleanup()
    {
        foreach (NpcSocial s in paused) if (s != null && AmbientField != null) AmbientField.SetValue(s, true);
        paused.Clear();
        keepPatient = null;
        keepSeated = Array.Empty<PatronBrain>();
        if (WaitingArea.Instance != null) foreach (Component h in holders) if (h != null) WaitingArea.Instance.Release(h);
        holders.Clear();
        foreach (GameObject go in temporary) if (go != null) Object.Destroy(go);
        temporary.Clear();
        if (CafeLabDirector.Instance != null) CafeLabDirector.Instance.Autopilot = autopilotBefore;
    }

    // The people under test: patience and stay topped up every editor tick.
    private static void KeepSubjectsStill()
    {
        if (!EditorApplication.isPlaying) return;
        if (keepPatient != null && PatienceField != null) PatienceField.SetValue(keepPatient, 1000f);
        foreach (PatronBrain p in keepSeated) if (p != null && LeaveAtField != null) LeaveAtField.SetValue(p, Time.time + 60f);
    }

    private static IEnumerator Sequence()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "The open scene has a DayClock"); yield break; }
        if (clock.DayOver) { Check(false, "A day is open (press Open Tomorrow on the recap, then run the check again)"); yield break; }
        if (clock.TimeRemaining < 400f) typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining))?.SetValue(clock, 400f);
        if (!CafeLab.Active || CafeLabDirector.Instance == null) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab)"); yield break; }
        Check(NpcBeats.Library != null && NpcBeats.Library.entries.Length > 0,
              $"The clip library is in the project (Resources/{NpcBeatLibrary.ResourceName}: {(NpcBeats.Library != null ? NpcBeats.Library.entries.Length : 0)} clips)");
        if (NpcBeats.Library == null) yield break;
        Check(PatienceField != null && LeaveAtField != null && AmbientField != null, "The check can hold its subjects still (patienceLeft, leaveAt, ambient found)");
        CafeLabDirector lab = CafeLabDirector.Instance;
        autopilotBefore = lab.Autopilot;
        lab.Autopilot = false;

        // ---------- 1. people in chairs, on the sofas and in the queue ----------
        var seats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude);
        var lounge = seats.Where(s => s.transform.parent != null && s.transform.parent.name == LoungeGroup).ToArray();
        var withTable = seats.Where(NpcBeats.TableInFront).ToArray();
        var noTable = seats.Where(s => !NpcBeats.TableInFront(s)).ToArray();
        Check(lounge.Length > 0 && lounge.All(s => noTable.Contains(s)) && noTable.All(s => lounge.Contains(s)),
              $"Seats without a table in front are exactly the lounge seats ({noTable.Length} without, {lounge.Length} lounge, {withTable.Length} with a table)");
        // Leave two café chairs and the lounge free, so patrons land in both kinds of seat.
        var keepFree = withTable.Where(s => !s.IsOccupied && s.SnapToSeat).OrderBy(s => s.name, StringComparer.Ordinal).Take(2).ToList();
        foreach (TableSeat seat in withTable)
        {
            if (keepFree.Contains(seat) || seat.IsOccupied) continue;
            Component holder = Dummy("seat holder");
            if (seat.Claim(holder)) holders.Add(holder);
        }
        lab.SendPatronsNow(5);
        lab.SendCustomersNow(2);
        float deadline = Time.time + 50f;
        NpcSeating atTable = null, onSofa = null;
        CustomerBrain inQueue = null;
        while (Time.time < deadline)
        {
            atTable = Seated(withTable).FirstOrDefault();
            onSofa = Seated(noTable).FirstOrDefault();
            inQueue = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.CanHearIntake && c.GetComponent<NpcLocomotion>() is NpcLocomotion l && !l.IsMoving && l.Speed < .05f);
            if (atTable != null && onSofa != null && inQueue != null) break;
            yield return null;
        }
        Check(atTable != null, "A patron sat at a café chair" + (atTable != null ? $" ({atTable.Seat.name})" : ""));
        Check(onSofa != null, "A patron sat on a lounge seat" + (onSofa != null ? $" ({onSofa.Seat.name})" : ""));
        Check(inQueue != null, "A customer stands in the queue" + (inQueue != null ? $" ({inQueue.CustomerName})" : ""));
        // From here the two patrons stay seated until the check lets them go.
        keepSeated = new[] { atTable, onSofa }.Where(s => s != null).Select(s => s.GetComponent<PatronBrain>()).Where(p => p != null).ToArray();
        yield return Wait(1.5f);

        // ---------- 2. everyone has the beats; city bodies copy their fingers ----------
        var everyone = Object.FindObjectsByType<NpcBeats>(FindObjectsInactive.Exclude);
        int ready = everyone.Count(b => b.Ready);
        Check(everyone.Length > 0 && ready == everyone.Length, $"Every café person has working beats ({ready} of {everyone.Length} ready)");
        int overridden = everyone.Count(b => b.GetComponentInChildren<Animator>()?.runtimeAnimatorController is AnimatorOverrideController);
        Check(overridden == everyone.Length, $"Their animators play the clips through the shared override ({overridden} of {everyone.Length})");
        var bodies = Object.FindObjectsByType<PolygonNpcVisual>(FindObjectsInactive.Exclude).Where(v => v.VisualInstance != null).ToList();
        Check(bodies.Count == 0 || bodies.All(v => v.FingerCount == 22),
              $"City bodies copy 22 finger bones ({string.Join(", ", bodies.GroupBy(v => v.FingerCount).Select(g => $"{g.Count()} with {g.Key}"))})");

        // ---------- 3. every clip on the right body ----------
        var standingClips = NpcBeats.Library.entries.Where(e => !e.seated && e.clip != null).Select(e => e.name).ToList();
        var seatedClips = NpcBeats.Library.entries.Where(e => e.seated && e.clip != null).Select(e => e.name).ToList();
        string[] leaning = { NpcBeats.Clip.Leaning, NpcBeats.Clip.ShoulderLean };
        if (inQueue != null)
        {
            keepPatient = inQueue;
            Pause(inQueue.GetComponent<NpcSocial>());
            var results = new List<string>();
            foreach (string clip in standingClips.Where(c => !leaning.Contains(c)))
            {
                var probe = new Probe();
                yield return PlayAndWatch(inQueue.GetComponent<NpcBeats>(), clip, 2.4f, probe);
                results.Add(probe.Line(clip));
                bool phoneClip = clip == NpcBeats.Clip.Texting || clip == NpcBeats.Clip.PhoneCall;
                bool ok = probe.started && probe.peak > .95f && probe.drift < .03f && probe.cleared
                          && (!phoneClip || probe.phoneInHand && probe.phoneGone)
                          && (clip != NpcBeats.Clip.PhoneCall || probe.visual == null || probe.visual.VisualInstance == null
                              || probe.faceHand > .6f && probe.phoneFromEar >= 0f && probe.phoneFromEar < .03f);
                Check(ok, $"Standing '{clip}': {probe.Line(clip)}");
                if (clip == NpcBeats.Clip.PhoneCall && probe.visual != null)
                    Note($"  On the call the phone is {probe.phoneFromEar * 100f:0.0} cm from its spot by the ear (reach weight {probe.faceHand:0.00}).");
            }
        }
        if (atTable != null)
        {
            Pause(atTable.GetComponent<NpcSocial>());
            foreach (string clip in seatedClips.Where(c => c != NpcBeats.Clip.SitLaughing && c != NpcBeats.Clip.SitThumbsUp))
            {
                var probe = new Probe();
                yield return PlayAndWatch(atTable.GetComponent<NpcBeats>(), clip, 2.4f, probe);
                Check(probe.started && probe.peak > .95f && probe.hipsMoved < .12f && probe.cleared && FeetOnFloor(probe) && atTable.Current == NpcSeating.Phase.Seated,
                      $"At the table '{clip}': {probe.Line(clip)}");
            }
            var tableBeats = atTable.GetComponent<NpcBeats>();
            Check(!tableBeats.PlayClip(NpcBeats.Clip.SitLaughing) && !tableBeats.PlayClip(NpcBeats.Clip.SitThumbsUp),
                  "At a table, 'Sitting Laughing' and 'Sitting Thumbs Up' are refused (sofas and tub chair only)");
            tableBeats.Stop(.05f);
        }
        if (onSofa != null)
        {
            Pause(onSofa.GetComponent<NpcSocial>());
            foreach (string clip in new[] { NpcBeats.Clip.SitLaughing, NpcBeats.Clip.SitThumbsUp, NpcBeats.Clip.SitTalking, NpcBeats.Clip.SitBreathing })
            {
                var probe = new Probe();
                yield return PlayAndWatch(onSofa.GetComponent<NpcBeats>(), clip, 2.4f, probe);
                Check(probe.started && probe.peak > .95f && probe.hipsMoved < .12f && probe.cleared && FeetOnFloor(probe) && onSofa.Current == NpcSeating.Phase.Seated,
                      $"On the sofa '{clip}': {probe.Line(clip)}");
            }
            var sofaBeats = onSofa.GetComponent<NpcBeats>();
            Check(!sofaBeats.PlayClip(NpcBeats.Clip.SitTapping), "On a lounge seat, 'Sitting - Tapping Fingers' is refused (it needs a table)");
            sofaBeats.Stop(.05f);
        }

        // ---------- 4. called away mid-beat ----------
        if (onSofa != null)
        {
            var b = onSofa.GetComponent<NpcBeats>();
            b.PlayClip(NpcBeats.Clip.SitBreathing, 8f);
            yield return Wait(1f);
            keepSeated = keepSeated.Where(p => p != null && p.gameObject != onSofa.gameObject).ToArray();
            LeaveAtField?.SetValue(onSofa.GetComponent<PatronBrain>(), Time.time);
            float rising = -1f, cleared = -1f;
            deadline = Time.time + 6f;
            while (Time.time < deadline && cleared < 0f)
            {
                if (rising < 0f && onSofa.Current != NpcSeating.Phase.Seated) rising = Time.time;
                if (rising >= 0f && b.Shown < .1f) cleared = Time.time;
                yield return null;
            }
            Check(rising >= 0f && cleared >= 0f && cleared - rising <= .35f,
                  $"Getting up from the sofa mid-beat: the beat is gone {(cleared >= 0f && rising >= 0f ? $"{(cleared - rising) * 1000f:0} ms" : "never")} after they start to rise");
        }
        if (inQueue != null)
        {
            var b = inQueue.GetComponent<NpcBeats>();
            var loco = inQueue.GetComponent<NpcLocomotion>();
            b.PlayClip(NpcBeats.Clip.Texting, 10f);
            yield return Wait(1.2f);
            keepPatient = null;
            lab.CustomersLeaveNow();
            float moving = -1f, cleared = -1f;
            deadline = Time.time + 8f;
            while (Time.time < deadline && cleared < 0f)
            {
                if (moving < 0f && loco.Speed > .3f) moving = Time.time;
                if (moving >= 0f && b.Shown < .1f) cleared = Time.time;
                yield return null;
            }
            Check(moving >= 0f && cleared >= 0f && cleared - moving <= .35f,
                  $"Walking off mid-beat: the beat is gone {(cleared >= 0f && moving >= 0f ? $"{(cleared - moving) * 1000f:0} ms" : "never")} after they set off; phone {(b.Phone == null ? "put away" : "STILL OUT")}");
        }

        // ---------- 5. a minute of the café by itself ----------
        foreach (NpcSocial s in paused) if (s != null) AmbientField?.SetValue(s, true);
        paused.Clear();
        keepSeated = Array.Empty<PatronBrain>();
        lab.PatronsLeaveNow();
        foreach (Component h in holders) WaitingArea.Instance.Release(h);
        holders.Clear();
        yield return Wait(4f);
        var startedBefore = NpcBeats.Started.ToDictionary(kv => kv.Key, kv => kv.Value);
        int reactionsBefore = NpcBeats.Reactions, interruptionsBefore = NpcBeats.Interruptions;
        lab.Autopilot = true;
        lab.SendPatronsNow(6);
        lab.SendCustomersNow(3);
        float sliding = 0f, watched = 0f;
        string worstSlide = "";
        float runUntil = Time.time + 75f;
        int sent = 0;
        while (Time.time < runUntil)
        {
            foreach (NpcBeats b in Object.FindObjectsByType<NpcBeats>(FindObjectsInactive.Exclude))
            {
                var agent = b.GetComponent<NavMeshAgent>();
                float speed = agent != null && agent.enabled ? new Vector3(agent.velocity.x, 0f, agent.velocity.z).magnitude : 0f;
                if (b.Shown > .5f)
                {
                    watched += Time.deltaTime;
                    if (speed > .35f) { sliding += Time.deltaTime; worstSlide = $"{b.name} '{b.CurrentName}' at {speed:0.00} m/s"; }
                }
            }
            if (sent == 0 && Time.time > runUntil - 45f) { lab.SendCustomersNow(2); sent++; }
            yield return null;
        }
        var naturally = NpcBeats.Started.Where(kv => kv.Value > (startedBefore.TryGetValue(kv.Key, out int n) ? n : 0))
                                         .Select(kv => $"{kv.Key} x{kv.Value - (startedBefore.TryGetValue(kv.Key, out int m) ? m : 0)}").ToList();
        Check(naturally.Count >= 4, $"In 75 s of the café by itself, {naturally.Count} different clips played: {string.Join(", ", naturally)}");
        Check(NpcBeats.Reactions > reactionsBefore, $"Reactions played in place of the old gesture: {NpcBeats.Reactions - reactionsBefore} (greetings, nods, let-downs...)");
        Check(sliding <= .5f, $"A beat showed while the body moved for {sliding:0.00} s of {watched:0.0} s of beats{(sliding > 0f ? " (worst: " + worstSlide + ")" : "")}");
        Note($"Beats cut short by the body having something else to do: {NpcBeats.Interruptions - interruptionsBefore}.");
        Note("Director: " + NpcAttentionDirector.Summary());
        lab.PatronsLeaveNow();
        lab.CustomersLeaveNow();
    }

    // ---------- one clip, watched ----------

    private sealed class Probe
    {
        public bool started, phoneInHand, phoneGone = true, cleared;
        public float peak, drift, hipsMoved, faceHand, phoneFromEar = -1f;
        // Seated clips on a city body: its lowest point against the floor, in the clip and just before it.
        public float lowest = float.MaxValue, lowestBefore = float.MaxValue;
        public string look = "", hand = "";
        public PolygonNpcVisual visual;

        public string Line(string clip) =>
            $"{(started ? "played" : "DID NOT PLAY")}, peak {peak:0.00}, body moved {drift * 100f:0.0} cm" +
            (hipsMoved > 0f ? $", hips moved {hipsMoved * 100f:0.0} cm on the seat" : "") +
            (hand.Length > 0 ? $", phone in the {hand} hand {(phoneInHand ? "(held)" : "(NOT IN THE HAND)")}, {(phoneGone ? "put away after" : "STILL OUT after")}" : "") +
            (faceHand > 0f ? $", hand at the face {faceHand:0.00}" : "") +
            (lowest < float.MaxValue ? $", lowest point {lowest * 100f:0.0} cm from the floor" +
                                       (lowestBefore < float.MaxValue ? $" ({lowestBefore * 100f:0.0} before)" : "") : "") +
            $", {(cleared ? "faded out cleanly" : "DID NOT FADE OUT")}" + (look.Length > 0 ? $" ({look})" : "");
    }

    private static IEnumerator PlayAndWatch(NpcBeats beats, string clip, float seconds, Probe probe)
    {
        if (beats == null) yield break;
        beats.Stop(.1f);
        float settle = Time.time + 1.5f;
        while (Time.time < settle && beats.Playing) yield return null;
        yield return Wait(.3f);
        Transform body = beats.transform;
        probe.visual = body.GetComponent<PolygonNpcVisual>();
        if (probe.visual != null && probe.visual.VisualInstance != null) probe.look = probe.visual.ActiveAppearanceName;
        Vector3 start = body.position;
        Vector3 hipsBefore = RigHips(body);
        NpcSeating seating = beats.GetComponent<NpcSeating>();
        bool seatedSubject = seating != null && seating.Current == NpcSeating.Phase.Seated && seating.Seat != null;
        if (seatedSubject) probe.lowestBefore = LowestAboveFloor(probe.visual, seating.Seat);
        float nextFoot = 0f;
        probe.started = beats.PlayClip(clip, seconds);
        if (!probe.started) yield break;
        NpcBeatLibrary.Hand wanted = NpcBeats.Library.Find(clip)?.phoneHand ?? NpcBeatLibrary.Hand.None;
        if (wanted != NpcBeatLibrary.Hand.None) probe.hand = wanted == NpcBeatLibrary.Hand.Left ? "left" : "right";
        float until = Time.time + seconds + 1.5f;
        bool faded = false;
        while (Time.time < until)
        {
            probe.peak = Mathf.Max(probe.peak, beats.Shown);
            probe.drift = Mathf.Max(probe.drift, Flat(body.position - start).magnitude);
            if (beats.Shown > .5f)
            {
                Vector3 hips = RigHips(body);
                if (hips != Vector3.zero && hipsBefore != Vector3.zero && beats.GetComponent<NpcSeating>()?.Current == NpcSeating.Phase.Seated)
                    probe.hipsMoved = Mathf.Max(probe.hipsMoved, (hips - hipsBefore).magnitude);
                // The city body's feet against the floor, a few times a second while the clip shows fully.
                if (seatedSubject && beats.Shown > .95f && Time.time >= nextFoot)
                {
                    nextFoot = Time.time + .25f;
                    probe.lowest = Mathf.Min(probe.lowest, LowestAboveFloor(probe.visual, seating.Seat));
                }
                if (wanted != NpcBeatLibrary.Hand.None && beats.Phone != null)
                {
                    Transform hand = probe.visual != null && probe.visual.VisualInstance != null ? probe.visual.CityHand(wanted == NpcBeatLibrary.Hand.Left) : null;
                    if (hand == null) hand = FindRig(body, wanted == NpcBeatLibrary.Hand.Left ? "Wrist.L" : "Wrist.R");
                    if (hand != null && beats.Phone.transform.IsChildOf(hand)) probe.phoneInHand = true;
                }
                if (clip == NpcBeats.Clip.PhoneCall && probe.visual != null && probe.visual.VisualInstance != null)
                {
                    Vector2 face = probe.visual.HandsAtFace;
                    float w = wanted == NpcBeatLibrary.Hand.Left ? face.x : face.y;
                    if (w >= probe.faceHand)
                    {
                        probe.faceHand = w;
                        probe.phoneFromEar = probe.visual.PhoneFromEar;
                    }
                }
            }
            if (probe.peak > .9f && !beats.Playing) { faded = true; break; }
            yield return null;
        }
        probe.cleared = faded && beats.Shown <= 0f;
        probe.phoneGone = beats.Phone == null;
    }

    // A seated clip keeps a city body's feet out of the floor (2 cm of slack; the café's
    // own sit clip rests them 0.7 cm above it). True without a city body to measure.
    private static bool FeetOnFloor(Probe probe) => probe.lowest == float.MaxValue || probe.lowest > -.02f;

    // How far the city body's lowest point is above the floor under this seat (negative:
    // into it). MaxValue without a city body. The same measure as Sit 4 (the live sit check).
    private static float LowestAboveFloor(PolygonNpcVisual visual, TableSeat seat)
    {
        if (visual == null || visual.VisualInstance == null || seat == null) return float.MaxValue;
        float floor = seat.StandPoint.position.y;
        int mask = Physics.DefaultRaycastLayers;
        int npc = LayerMask.NameToLayer("NPC"), player = LayerMask.NameToLayer("Player");
        if (npc >= 0) mask &= ~(1 << npc);
        if (player >= 0) mask &= ~(1 << player);
        if (Physics.Raycast(seat.StandPoint.position + Vector3.up * .5f, Vector3.down, out RaycastHit hit, 2f, mask, QueryTriggerInteraction.Ignore))
            floor = hit.point.y;
        float lowest = float.MaxValue;
        foreach (SkinnedMeshRenderer skin in visual.VisualInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (skin == null || !skin.enabled || skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
            Mesh posed = PolygonNpcSetup.SkinToWorld(skin);
            foreach (Vector3 v in posed.vertices) lowest = Mathf.Min(lowest, v.y);
            Object.DestroyImmediate(posed);
        }
        return lowest < float.MaxValue ? lowest - floor : float.MaxValue;
    }

    // The rig's hip joints (midpoint), or zero without them.
    private static Vector3 RigHips(Transform body)
    {
        Transform l = FindRig(body, "UpperLeg.L"), r = FindRig(body, "UpperLeg.R");
        return l != null && r != null ? (l.position + r.position) * .5f : Vector3.zero;
    }

    private static Transform FindRig(Transform root, string name)
    {
        var visual = root.GetComponent<PolygonNpcVisual>();
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        return Find(root, name, skip);
    }

    private static Transform Find(Transform node, string name, Transform skip)
    {
        if (node == skip) return null;
        if (node.name == name) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform hit = Find(node.GetChild(i), name, skip);
            if (hit != null) return hit;
        }
        return null;
    }

    private static Transform FindUnder(Transform node, string name) => Find(node, name, null);

    private static List<NpcSeating> Seated(TableSeat[] seats) =>
        Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude)
            .Where(s => s.Current == NpcSeating.Phase.Seated && s.Seat != null && seats.Contains(s.Seat) && s.GetComponent<PatronBrain>() != null)
            .ToList();

    private static void Pause(NpcSocial social)
    {
        if (social == null || AmbientField == null) return;
        AmbientField.SetValue(social, false);
        social.StopGesture();
        paused.Add(social);
    }

    private static Component Dummy(string name)
    {
        var go = new GameObject("Beat check - " + name);
        temporary.Add(go);
        return go.transform;
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

    private static void Finish()
    {
        string verdict = failures == 0 ? "PASS" : "FAIL";
        string text = $"Mixamo beat check - {verdict} ({checks - failures} of {checks})\n{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{report}";
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcMixamo"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"beat-check-{DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.txt");
        File.WriteAllText(file, text);
        if (failures == 0) Debug.Log($"[Beat check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
        else Debug.LogWarning($"[Beat check] {verdict} ({checks - failures}/{checks}). {file}\n{report}");
    }
}
#endif
