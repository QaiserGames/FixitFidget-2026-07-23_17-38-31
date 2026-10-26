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
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// The NPC life pass (12-13 Oct 2026; findings doc §9, his "yes and i trust the judgement you will take"): the steps and
// the checks that go with the code.
//
// LIFE 1 - WIRE THE UPPER-BODY BEATS LAYER (Edit mode). The customer animator (tracked) gets a third layer, "UpperBeats",
// behind an avatar mask from the spine up: every transform of the café rig from Abdomen upward (the spine, neck, head,
// both arms to the fingertips) is in; Root, Body/Hips, the legs and the feet are out, so the base layer's walk keeps the
// legs and NpcLocomotion keeps the feet planted. Its states are the Beats layer's STANDING states again, pointing at the
// same empty placeholder clips ("Beat slot - Texting"): the shared override controller that swaps the baked clips in
// (NpcBeats) fills both layers at once, and the public repository still holds no Mixamo motion. The mask is measured on
// the Customer prefab's own rig and saved inside the controller asset (nothing new to track). Re-running is safe.
// NpcBeats plays texting, a phone call, Holding Idle, a nod, a head shake and the storm-out's gestures there while the
// body walks, and carries a standing phone beat over as the person sets off.
//
// LIFE 2 - UPPER BODY CHECK (Play mode, in a lab session: Fixit Fidget > Café life > Lab > Play a lab session). A
// customer walks to the door; the lowest foot's height off the floor is measured on the plain walk, then with Texting,
// the call and Holding Idle playing on the upper layer: the foot never lifts more than on the plain walk (plus 1.2 cm),
// the beat shows fully while the body walks, the phone is in the hand and gone afterwards, a standing phone beat is
// carried over as they set off, and a seated patron is refused the layer. Photos of the walking texter and the caller.
// Report: Logs/NpcLife/upper-check-<time>.txt.
//
// LIFE 3 - LIFE PASS CHECK (Play mode, lab session). The reactions (the bell as people come in, a storm-out watched, a
// Fixed! at the bench turning heads), the walks by mood (a storm-out brisk and hurried, a Perfect repair's customer
// strolling out, patrons strolling, a Rushed walk-in hurrying when one comes), a pair (two seats of one table, a chat,
// leaving together), the cup (on the table, into the bins by the door) and a laptop patron (the laptop on, the bag at
// their feet, their eyes on the screen). Props the Environment lead places this session (LI_BinStation, LI_Laptop_Open,
// LI_ToteBag) are NOTEd, not failed, when absent. Report: Logs/NpcLife/life-pass-<time>.txt, photos beside it.
public static class NpcLifeSteps
{
    private const string Menu = "Fixit Fidget/NPC/";
    private const string WireMenu = Menu + "Life 1 - Wire the upper-body beats layer (UpperBeats, mask from the spine up)";
    private const string UpperMenu = Menu + "Life 2 - Upper body check (Play Mode, lab session)";
    private const string LifeMenu = Menu + "Life 3 - Life pass check (Play Mode, lab session)";
    private const string Tag = "[NPC life] ";
    private const string ControllerPath = "Assets/CustomerAnimator(.controller";
    private const string CustomerPrefab = "Assets/AssetsPrefabs/Customer.prefab";
    private const string PatronPrefab = "Assets/AssetsPrefabs/Patron.prefab";

    // The café rig from the spine up: these and everything under them go into the mask. Root, Body, Hips, the legs and
    // the feet (and anything else, IK targets included) stay out.
    private static readonly string[] UpperBones =
    {
        "Abdomen", "Torso", "Chest", "Neck", "Head",
        "Shoulder.L", "UpperArm.L", "LowerArm.L", "Wrist.L",
        "Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R",
    };

    // The standing clips (NpcBeats.Clip): the Beats layer's states with these names are wired again on the upper layer.
    private static readonly string[] StandingClips =
    {
        NpcBeats.Clip.BreathingIdle, NpcBeats.Clip.WeightShift, NpcBeats.Clip.LookingAround, NpcBeats.Clip.Bored, NpcBeats.Clip.Thinking,
        NpcBeats.Clip.HoldingIdle, NpcBeats.Clip.Texting, NpcBeats.Clip.PhoneCall, NpcBeats.Clip.Leaning, NpcBeats.Clip.ShoulderLean,
        NpcBeats.Clip.HeadShake, NpcBeats.Clip.Pouting, NpcBeats.Clip.AngryGesture, NpcBeats.Clip.Dismissing, NpcBeats.Clip.Disappointed,
        NpcBeats.Clip.Shrugging, NpcBeats.Clip.Thankful, NpcBeats.Clip.RelievedSigh, NpcBeats.Clip.HappyIdle, NpcBeats.Clip.HappyHand,
        NpcBeats.Clip.Excited, NpcBeats.Clip.Laughing, NpcBeats.Clip.Greeting, NpcBeats.Clip.NodYes,
    };

    // ================================================================== Life 1: the layer and its mask

    [MenuItem(WireMenu)]
    private static void WireMenuItem() => Run("Life 1", WireUpperLayer);

    public static string WireUpperLayer()
    {
        RequireStopped();
        var notes = new StringBuilder();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null, "Missing " + ControllerPath);
        int beatsIndex = Array.FindIndex(controller.layers, l => l.name == NpcBeatLibrary.LayerName);
        Require(beatsIndex >= 0, "The animator has no '" + NpcBeatLibrary.LayerName + "' layer: run Fixit Fidget > NPC > Mixamo 3 first.");
        AnimatorStateMachine beats = controller.layers[beatsIndex].stateMachine;

        // 1. The standing states the Beats layer has, with their placeholder clips.
        var slots = new List<(string name, AnimationClip slot)>();
        foreach (string clipName in StandingClips)
        {
            AnimatorState state = FindState(beats, clipName);
            if (state == null) continue;
            var slot = state.motion as AnimationClip;
            if (slot == null) continue;
            slots.Add((clipName, slot));
        }
        Require(slots.Count > 0, "The Beats layer has none of the standing clips' states (" + StandingClips.Length + " looked for).");
        var missing = StandingClips.Where(c => slots.All(s => s.name != c)).ToList();
        notes.AppendLine($"{slots.Count} standing beats on the Beats layer" + (missing.Count > 0 ? $"; not wired there (so not here either): {string.Join(", ", missing)}" : "") + ".");

        // 2. The mask, measured on the Customer prefab's rig; the Patron prefab must have the same bones.
        AvatarMask mask = BuildMask(controller, out string maskReport);
        notes.AppendLine(maskReport);

        // 3. The layer.
        int index = Array.FindIndex(controller.layers, l => l.name == NpcBeatLibrary.UpperLayerName);
        bool newLayer = index < 0;
        if (newLayer)
        {
            controller.AddLayer(NpcBeatLibrary.UpperLayerName);
            index = controller.layers.Length - 1;
        }
        var layers = controller.layers;
        layers[index].defaultWeight = 0f;
        layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[index].avatarMask = mask;
        layers[index].iKPass = false;
        layers[index].syncedLayerIndex = -1;
        controller.layers = layers;
        AnimatorStateMachine machine = controller.layers[index].stateMachine;
        AnimatorState rest = FindState(machine, NpcBeatLibrary.RestState) ?? machine.AddState(NpcBeatLibrary.RestState, new Vector3(300f, 40f));
        rest.motion = null;
        rest.writeDefaultValues = false;
        machine.defaultState = rest;
        int row = 0, newStates = 0;
        foreach (var (clipName, slot) in slots)
        {
            AnimatorState state = FindState(machine, clipName);
            if (state == null) { state = machine.AddState(clipName, new Vector3(300f, 120f + 50f * row)); newStates++; }
            row++;
            state.motion = slot;
            state.writeDefaultValues = false;
            state.speed = 1f;
            foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
        }
        var wanted = new HashSet<string>(slots.Select(s => s.name), StringComparer.Ordinal);
        int removed = 0;
        foreach (var child in machine.states.ToArray())
            if (child.state != rest && !wanted.Contains(child.state.name)) { machine.RemoveState(child.state); removed++; }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        notes.AppendLine($"Animator: layer '{NpcBeatLibrary.UpperLayerName}' {(newLayer ? "added" : "updated")} (index {index}, weight 0, override, masked), " +
                         $"{slots.Count} states + '{NpcBeatLibrary.RestState}' ({newStates} new, {removed} removed), the same placeholder clips as the Beats layer: " +
                         "the shared override fills both layers; no Mixamo motion is in the tracked file.");
        notes.AppendLine("Next: Fixit Fidget > NPC > Life 2 (the feet stay planted while a beat plays on a walking body), in a lab session.");
        string report = notes.ToString();
        WriteLog("life1-wire.txt", report);
        return "\n" + report;
    }

    // The mask: every transform under the Customer prefab's Animator, by its path from the Animator's object (how the
    // baked clips name them), active when it is one of UpperBones or hangs under one. Saved inside the controller.
    private static AvatarMask BuildMask(AnimatorController controller, out string report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        Require(prefab != null, "Missing " + CustomerPrefab);
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        AvatarMask mask = null;
        try
        {
            Animator animator = actor.GetComponentInChildren<Animator>(true);
            Require(animator != null, CustomerPrefab + " has no Animator.");
            Transform root = animator.transform;
            var all = root.GetComponentsInChildren<Transform>(true)
                .Where(t => !t.name.StartsWith("City look", StringComparison.Ordinal) && !IsUnderCityLook(t, root)).ToList();
            var upper = new HashSet<string>(UpperBones, StringComparer.Ordinal);
            bool Upper(Transform t)
            {
                for (Transform p = t; p != null && p != root; p = p.parent) if (upper.Contains(p.name)) return true;
                return false;
            }
            foreach (string bone in UpperBones) Require(all.Any(t => t.name == bone), "The café rig has no bone " + bone + " under its Animator.");

            foreach (AvatarMask old in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<AvatarMask>().Where(m => m.name == NpcBeatLibrary.UpperMaskName).ToArray())
            {
                AssetDatabase.RemoveObjectFromAsset(old);
                Object.DestroyImmediate(old, true);
            }
            mask = new AvatarMask { name = NpcBeatLibrary.UpperMaskName, transformCount = all.Count };
            var active = new List<string>();
            var inactive = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(all[i], root);
                bool on = Upper(all[i]);
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, on);
                (on ? active : inactive).Add(path.Length == 0 ? "(root)" : path);
            }
            // Humanoid parts are not used (a generic rig), all off so nothing surprising applies.
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
            AssetDatabase.AddObjectToAsset(mask, controller);

            // The Patron prefab's rig must answer to the same paths.
            string patronNote = "";
            var patronAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PatronPrefab);
            if (patronAsset != null)
            {
                var patron = (GameObject)PrefabUtility.InstantiatePrefab(patronAsset);
                patron.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    Animator pa = patron.GetComponentInChildren<Animator>(true);
                    int found = 0;
                    var lost = new List<string>();
                    foreach (string path in active)
                    {
                        if (pa != null && (path == "(root)" || pa.transform.Find(path) != null)) found++;
                        else lost.Add(path);
                    }
                    patronNote = lost.Count == 0 ? $" The Patron prefab's rig has every one of them ({found})."
                                                 : $" NOTE: the Patron prefab's rig lacks {lost.Count} of them: {string.Join(", ", lost.Take(8))}.";
                }
                finally { Object.DestroyImmediate(patron); }
            }
            string legs = string.Join(", ", inactive.Where(p => p.EndsWith("Body") || p.EndsWith("Hips") || p.EndsWith("Root") || p.Contains("Leg") || p.Contains("Foot")).Select(p => p.Substring(p.LastIndexOf('/') + 1)).Distinct());
            report = $"Mask '{NpcBeatLibrary.UpperMaskName}': {active.Count} transforms in (from {string.Join(", ", UpperBones.Take(5))}... down to the fingertips), " +
                     $"{inactive.Count} out ({legs}). Saved inside the controller asset.{patronNote}\n  In: {string.Join(", ", active.Select(p => p.Substring(p.LastIndexOf('/') + 1)))}";
            return mask;
        }
        finally { Object.DestroyImmediate(actor); }
    }

    private static bool IsUnderCityLook(Transform t, Transform root)
    {
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
            if (p.name.StartsWith("City look", StringComparison.Ordinal)) return true;
        return false;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name) =>
        machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    // ================================================================== the Play-mode checks' plumbing (as Mixamo 5)

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo PatienceField = typeof(CustomerBrain).GetField("patienceLeft", Any);
    private static readonly FieldInfo LeaveAtField = typeof(PatronBrain).GetField("leaveAt", Any);
    private static readonly FieldInfo AmbientField = typeof(NpcSocial).GetField("ambient", Any);
    private static readonly FieldInfo LastGradeField = typeof(CustomerBrain).GetField("lastGrade", Any);
    private static readonly FieldInfo RepairReturnedField = typeof(CustomerBrain).GetField("repairReturned", Any);
    private static readonly FieldInfo WasServedField = typeof(CustomerBrain).GetField("wasServed", Any);
    private static readonly MethodInfo FinishAndLeaveMethod = typeof(CustomerBrain).GetMethod("FinishAndLeave", Any);
    private static readonly MethodInfo StormOutMethod = typeof(CustomerBrain).GetMethod("StormOut", Any);
    private static readonly MethodInfo PatronLeaveMethod = typeof(PatronBrain).GetMethod("Leave", Any);

    private static readonly Stack<IEnumerator> routine = new();
    private static readonly StringBuilder report = new();
    private static readonly List<NpcSocial> paused = new();
    private static int checks, failures;
    private static CustomerBrain keepPatient;
    private static PatronBrain[] keepSeated = Array.Empty<PatronBrain>();
    private static bool autopilotBefore;
    private static string folder, title, logName;

    [MenuItem(UpperMenu)]
    private static void UpperMenuItem() => Begin("Upper body check", "upper-check", UpperSequence());

    [MenuItem(LifeMenu)]
    private static void LifeMenuItem() => Begin("Life pass check", "life-pass", LifeSequence());

    [MenuItem(UpperMenu, true)]
    [MenuItem(LifeMenu, true)]
    private static bool CanRun() => EditorApplication.isPlaying && routine.Count == 0;

    private static void Begin(string what, string file, IEnumerator sequence)
    {
        report.Clear();
        checks = failures = 0;
        paused.Clear();
        keepPatient = null;
        keepSeated = Array.Empty<PatronBrain>();
        title = what;
        logName = file;
        folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcLife")),
                              file + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        routine.Clear();
        routine.Push(sequence);
        EditorApplication.update += Step;
        Debug.Log(Tag + what + ": running in the lab session...");
    }

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
        if (CafeLabDirector.Instance != null) CafeLabDirector.Instance.Autopilot = autopilotBefore;
    }

    private static void KeepSubjectsStill()
    {
        if (!EditorApplication.isPlaying) return;
        if (keepPatient != null && PatienceField != null) PatienceField.SetValue(keepPatient, 1000f);
        foreach (PatronBrain p in keepSeated) if (p != null && LeaveAtField != null) LeaveAtField.SetValue(p, Time.time + 60f);
    }

    // A lab session with an open day and the clip library; the autopilot off while the check drives.
    private static bool LabReady(out CafeLabDirector lab)
    {
        lab = null;
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "The open scene has a DayClock"); return false; }
        if (clock.DayOver) { Check(false, "A day is open (press Open Tomorrow on the recap, then run the check again)"); return false; }
        if (clock.TimeRemaining < 420f) typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining))?.SetValue(clock, 420f);
        if (!CafeLab.Active || CafeLabDirector.Instance == null) { Check(false, "This is a lab session (Fixit Fidget > Café life > Lab)"); return false; }
        Check(NpcBeats.Library != null && NpcBeats.Library.entries.Length > 0,
              $"The clip library is in the project ({(NpcBeats.Library != null ? NpcBeats.Library.entries.Length : 0)} clips)");
        if (NpcBeats.Library == null) return false;
        lab = CafeLabDirector.Instance;
        autopilotBefore = lab.Autopilot;
        lab.Autopilot = false;
        return true;
    }

    // ================================================================== Life 2: the upper body on a walking body

    private static IEnumerator UpperSequence()
    {
        if (!LabReady(out CafeLabDirector lab)) yield break;
        Check(PatienceField != null && LeaveAtField != null && AmbientField != null && FinishAndLeaveMethod != null,
              "The check can hold its subjects still and send them off (patienceLeft, leaveAt, ambient, FinishAndLeave found)");

        // ---------- 1. a customer in the queue, a patron in a chair ----------
        lab.SendCustomersNow(2);
        lab.SendPatronsNow(2);
        float deadline = Time.time + 50f;
        CustomerBrain inQueue = null;
        NpcSeating atTable = null;
        while (Time.time < deadline)
        {
            inQueue = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.CanHearIntake && c.GetComponent<NpcLocomotion>() is NpcLocomotion l && !l.IsMoving && l.Speed < .05f);
            atTable = Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude)
                .FirstOrDefault(s => s.Current == NpcSeating.Phase.Seated && s.Seat != null && s.GetComponent<PatronBrain>() != null);
            if (inQueue != null && atTable != null) break;
            yield return null;
        }
        Check(inQueue != null, "A customer stands in the queue" + (inQueue != null ? $" ({inQueue.CustomerName})" : ""));
        Check(atTable != null, "A patron sits at a table" + (atTable != null ? $" ({atTable.Seat.name})" : ""));
        if (inQueue == null) yield break;
        NpcBeats beats = inQueue.GetComponent<NpcBeats>();
        NpcLocomotion loco = inQueue.GetComponent<NpcLocomotion>();
        Check(beats != null && beats.Ready, "The customer has working beats");
        Check(beats != null && beats.HasUpperLayer, $"The animator has the upper-body layer '{NpcBeatLibrary.UpperLayerName}' (run Fixit Fidget > NPC > Life 1 if not)" +
                                                     (beats != null ? $" (layer index {beats.UpperLayer})" : ""));
        if (beats == null || !beats.HasUpperLayer) yield break;
        keepPatient = inQueue;
        Pause(inQueue.GetComponent<NpcSocial>());
        if (atTable != null)
        {
            keepSeated = new[] { atTable.GetComponent<PatronBrain>() };
            Pause(atTable.GetComponent<NpcSocial>());
            NpcBeats seatedBeats = atTable.GetComponent<NpcBeats>();
            Check(seatedBeats != null && !seatedBeats.PlayUpper(NpcBeats.Clip.Texting, 3f), "A seated patron is refused the upper layer (seated clips and chairs keep the whole body)");
            if (seatedBeats != null) seatedBeats.Stop(.05f);
        }

        // ---------- 2. standing: a beat on the upper layer plays while the legs are the idle's ----------
        beats.Stop(.05f);
        yield return Wait(.8f);
        var probe = new Probe();
        yield return PlayUpperAndWatch(beats, loco, NpcBeats.Clip.NodYes, 2f, probe, false);
        Check(probe.started && probe.onUpper && probe.peak > .95f && probe.cleared, $"Standing, 'Head Nod Yes' on the upper layer: {probe.Line()}");

        // ---------- 3. carried over: texting as they set off ----------
        beats.Stop(.05f);
        yield return Wait(.8f);
        bool texting = beats.PlayClip(NpcBeats.Clip.Texting, 12f);
        yield return Wait(1.2f);
        Check(texting && beats.Playing && !beats.OnUpperLayer && beats.Phone != null, "Texting in the queue plays on the whole-body layer with the phone in hand (the carry-over's start)");
        int carriedBefore = NpcBeats.Carried;
        keepPatient = null;
        AmbientField?.SetValue(inQueue.GetComponent<NpcSocial>(), true);
        WasServedField?.SetValue(inQueue, true);
        FinishAndLeaveMethod?.Invoke(inQueue, new object[] { "Right, I'm off.", true });
        float moving = -1f, carriedAt = -1f, shownWhileWalking = 0f, walked = 0f;
        deadline = Time.time + 14f;
        while (Time.time < deadline && inQueue != null)
        {
            if (moving < 0f && loco.Speed > .5f) moving = Time.time;
            if (moving >= 0f && carriedAt < 0f && beats.OnUpperLayer) carriedAt = Time.time;
            if (moving >= 0f && loco.Speed > .5f) { walked += Time.deltaTime; if (beats.Shown > .5f) shownWhileWalking += Time.deltaTime; }
            if (moving >= 0f && Time.time - moving > 3f) break;
            yield return null;
        }
        Check(moving >= 0f && NpcBeats.Carried > carriedBefore && carriedAt >= 0f && carriedAt - moving < .6f,
              $"Setting off mid-text, the beat moved to the upper layer {(carriedAt >= 0f && moving >= 0f ? $"{(carriedAt - moving) * 1000f:0} ms after the walk began" : "(it did not)")} (carried {NpcBeats.Carried - carriedBefore})");
        Check(walked > 1f && shownWhileWalking > walked * .8f, $"The phone stayed out while they walked: shown {shownWhileWalking:0.0} s of {walked:0.0} s walking");
        if (inQueue != null) beats.Stop(.1f);

        // ---------- 4. a long walk: the plain walk's feet, then each clip on the upper layer ----------
        lab.SendCustomersNow(1);
        deadline = Time.time + 20f;
        CustomerBrain walker = null;
        while (Time.time < deadline)
        {
            walker = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c != inQueue && !c.IsLeaving && c.GetComponent<NpcLocomotion>() is NpcLocomotion l && l.Speed > .5f);
            if (walker != null) break;
            yield return null;
        }
        Check(walker != null, "A second customer walks in" + (walker != null ? $" ({walker.CustomerName})" : ""));
        if (walker == null) yield break;
        NpcBeats wBeats = walker.GetComponent<NpcBeats>();
        NpcLocomotion wLoco = walker.GetComponent<NpcLocomotion>();
        keepPatient = walker;
        Pause(walker.GetComponent<NpcSocial>());
        wBeats.Stop(.05f);
        // The walk in, for the record; the feet's reference comes from the walk OUT, the walk the clips play over (the walk
        // in is their profile's gait, the walk out a served customer's: 13 Oct, 5.7 cm in against 8.4 cm out).
        var feet = new FeetWatch(walker.transform);
        float inMax = 0f;
        float until = Time.time + 1.2f;
        while (Time.time < until) { if (wLoco.Speed > .5f) inMax = Mathf.Max(inMax, feet.Lift()); yield return null; }
        Note($"The walk in: the lowest foot lifts up to {inMax * 100f:0.0} cm off the floor ({feet.Samples} samples).");
        // To the door: the longest walk in the café. They were caught on the way in, so first let them arrive (sent off from
        // the door's end, the walk out was a few steps: 13 Oct, the second clip got 0.2 s of walking and the third none);
        // then send them off, take the plain walk's feet over the first second, and play the clips. Three clips of 1.5 s
        // and their fades fit the rest of the queue-to-door walk (about 9 s); 2.2 s each did not.
        float arrive = Time.time + 25f;
        while (Time.time < arrive && walker != null && (wLoco.IsMoving || wLoco.Speed > .05f)) yield return null;
        keepPatient = null;
        WasServedField?.SetValue(walker, true);
        FinishAndLeaveMethod?.Invoke(walker, new object[] { "Right, I'm off.", true });
        float setOff = Time.time + 6f;
        while (Time.time < setOff && walker != null && wLoco.Speed < .5f) yield return null;   // the goodbye line, then the walk
        float plainMax = 0f;
        feet = walker != null ? new FeetWatch(walker.transform) : feet;
        until = Time.time + 1f;
        while (Time.time < until && walker != null) { if (wLoco.Speed > .5f) plainMax = Mathf.Max(plainMax, feet.Lift()); yield return null; }
        Note($"Plain walk (out, the clips' own walk): the lowest foot lifts up to {plainMax * 100f:0.0} cm off the floor ({feet.Samples} samples).");
        foreach (string clip in new[] { NpcBeats.Clip.Texting, NpcBeats.Clip.PhoneCall, NpcBeats.Clip.HoldingIdle })
        {
            if (walker == null) { Note($"  '{clip}' not tried: the walker had reached the door."); break; }
            var p = new Probe();
            yield return PlayUpperAndWatch(wBeats, wLoco, clip, 1.5f, p, true, feet, folder, clip);
            bool phoneClip = clip != NpcBeats.Clip.HoldingIdle;
            bool ok = p.started && p.onUpper && p.peak > .95f && p.cleared && p.walkedSeconds > .8f
                      && p.footMax <= plainMax + .012f
                      && (!phoneClip || p.phoneInHand && p.phoneGone);
            Check(ok, $"Walking, '{clip}' on the upper layer: {p.Line()}; the lowest foot lifted up to {p.footMax * 100f:0.0} cm (plain walk {plainMax * 100f:0.0} cm)");
            if (clip == NpcBeats.Clip.HoldingIdle) Note(p.phoneInHand ? "  Holding Idle carried the phone (a phone customer)." : "  Holding Idle played empty-handed (not a phone customer, or the prop is not in the library).");
        }
        Note($"Beats started on the upper layer this session: {NpcBeats.UpperStarts}; carried over: {NpcBeats.Carried}; cut short by the body: {NpcBeats.Interruptions}.");
        lab.CustomersLeaveNow();
        lab.PatronsLeaveNow();
    }

    private sealed class Probe
    {
        public bool started, onUpper, cleared, phoneInHand, phoneGone = true;
        public float peak, footMax, walkedSeconds, faceHand;
        public string Line() =>
            $"{(started ? "played" : "DID NOT PLAY")}, {(onUpper ? "upper layer" : "WHOLE-BODY LAYER")}, peak {peak:0.00}, walked {walkedSeconds:0.0} s while it showed" +
            (phoneInHand ? ", phone in the hand" : "") + (phoneGone ? "" : ", phone STILL OUT after") + (faceHand > 0f ? $", hand at the face {faceHand:0.00}" : "") +
            $", {(cleared ? "faded out cleanly" : "DID NOT FADE OUT")}";
    }

    // A clip on the upper layer, watched: its weight, whether it stayed on the upper layer, the phone, the feet while the
    // body walked, and a photo at full weight (when a folder is given).
    private static IEnumerator PlayUpperAndWatch(NpcBeats beats, NpcLocomotion loco, string clip, float seconds, Probe probe, bool expectWalk,
                                                 FeetWatch feet = null, string photoFolder = null, string label = null)
    {
        if (beats == null) yield break;
        beats.Stop(.1f);
        float settle = Time.time + 1.2f;
        while (Time.time < settle && beats.Playing) yield return null;
        probe.started = beats.PlayUpper(clip, seconds);
        if (!probe.started) yield break;
        probe.onUpper = beats.OnUpperLayer;
        PolygonNpcVisual visual = beats.GetComponent<PolygonNpcVisual>();
        NpcBeatLibrary.Hand wanted = NpcBeats.Library.Find(clip)?.phoneHand ?? NpcBeatLibrary.Hand.None;
        float until = Time.time + seconds + 1.5f;
        bool faded = false, photographed = false;
        while (Time.time < until)
        {
            if (beats == null) yield break;
            probe.peak = Mathf.Max(probe.peak, beats.Shown);
            if (!beats.OnUpperLayer && beats.Playing) probe.onUpper = false;
            if (beats.Shown > .5f)
            {
                bool walking = loco != null && loco.Speed > .5f;
                if (walking) probe.walkedSeconds += Time.deltaTime;
                if (walking && feet != null) probe.footMax = Mathf.Max(probe.footMax, feet.Lift());
                if (beats.Phone != null)
                {
                    NpcBeatLibrary.Hand hand = beats.PhoneHand;
                    Transform handBone = visual != null && visual.VisualInstance != null ? visual.CityHand(hand == NpcBeatLibrary.Hand.Left) : null;
                    if (handBone == null) handBone = FindRig(beats.transform, hand == NpcBeatLibrary.Hand.Left ? "Wrist.L" : "Wrist.R");
                    if (handBone != null && beats.Phone.transform.IsChildOf(handBone)) probe.phoneInHand = true;
                }
                if (clip == NpcBeats.Clip.PhoneCall && visual != null && visual.VisualInstance != null)
                    probe.faceHand = Mathf.Max(probe.faceHand, Mathf.Max(visual.HandsAtFace.x, visual.HandsAtFace.y));
                if (!photographed && photoFolder != null && beats.Shown > .95f && (!expectWalk || walking))
                {
                    photographed = true;
                    Photograph(beats.transform, photoFolder, label ?? clip);
                }
            }
            if (probe.peak > .9f && !beats.Playing) { faded = true; break; }
            yield return null;
        }
        probe.cleared = faded && beats.Shown <= 0f;
        probe.phoneGone = beats.Phone == null;
    }

    // The rig's two foot bones against the floor the body stands on (its own position: the agent keeps it on the floor).
    private sealed class FeetWatch
    {
        private readonly Transform body, left, right;
        public int Samples { get; private set; }
        public FeetWatch(Transform body)
        {
            this.body = body;
            left = FindRig(body, "Foot.L");
            right = FindRig(body, "Foot.R");
        }
        public float Lift()
        {
            if (left == null || right == null || body == null) return 0f;
            Samples++;
            return Mathf.Max(0f, Mathf.Min(left.position.y, right.position.y) - body.position.y);
        }
    }

    // ================================================================== Life 3: the life pass

    private static IEnumerator LifeSequence()
    {
        if (!LabReady(out CafeLabDirector lab)) yield break;
        Check(StormOutMethod != null && FinishAndLeaveMethod != null && PatronLeaveMethod != null && LastGradeField != null && RepairReturnedField != null && WasServedField != null,
              "The check can drive the brains (StormOut, FinishAndLeave, Leave, lastGrade, repairReturned, wasServed found)");
        Note("Props: " + (NpcLifeProps.BinStation != null ? "bin station '" + NpcLifeProps.BinStation.name + "'" : "no LI_BinStation") +
             $", {NpcLifeProps.LaptopCount} laptops (LI_Laptop_Open), {NpcLifeProps.BagCount} bags (LI_ToteBag).");
        Note("Juice's OnFixed event: " + (NpcAttentionDirector.ListensToJuice ? "listened to" : "not there yet; Juice.Finishes is polled instead") + ".");

        // ---------- 1. the room: four patrons seated ----------
        lab.SendPatronsNow(4);
        float deadline = Time.time + 45f;
        while (Time.time < deadline && Seated().Count < 3) yield return null;
        int seated = Seated().Count;
        Check(seated >= 3, $"Patrons seated to watch the room with: {seated}");
        keepSeated = Seated().Select(s => s.GetComponent<PatronBrain>()).Where(p => p != null).ToArray();
        yield return Wait(1.5f);

        // ---------- 2. the bell: someone comes in, heads turn ----------
        int arrivalLooksBefore = NpcAttentionDirector.ArrivalLooks, arrivalsBefore = NpcAttentionDirector.ArrivalsSeen;
        lab.SendCustomersNow(1);
        yield return Wait(4f);
        int looked = NpcAttentionDirector.ArrivalLooks - arrivalLooksBefore;
        Check(NpcAttentionDirector.ArrivalsSeen > arrivalsBefore,
              $"Someone came in and the room was told ({NpcAttentionDirector.ArrivalsSeen - arrivalsBefore} arrival seen; {looked} people glanced at the door; 12 Oct: 47 of 62 arrivals drew none)");
        // The glance itself rolls dice (curiosity, who can see the door): counted, not asserted (QA, 13 Oct).
        if (looked == 0) Note("Nobody happened to glance at that arrival (the look is a roll by curiosity among those who can see the door).");
        CustomerBrain first = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).FirstOrDefault(c => !c.IsLeaving);
        Check(first != null, "The customer is in the café" + (first != null ? $" ({first.CustomerName}, {Sort(first)})" : ""));
        if (first != null)
        {
            NpcLocomotion l = first.GetComponent<NpcLocomotion>();
            Note($"  Their walk: profile {first.GetComponent<NpcSocial>()?.ProfileName}, asked {l.RequestedGait}, clip {NpcGaitData.StyleName(l.WalkStyle)}, {l.WalkSpeed:0.00} m/s" +
                 (Sort(first).Contains("Rushed") ? " (a Rushed walk-in: hurries)" : ""));
            if (Sort(first).Contains("Rushed")) Check(l.RequestedGait == NpcLocomotion.Gait.Hurry && l.WalkStyle == (int)NpcLocomotion.Gait.Brisk || l.WalkStyle == (int)NpcLocomotion.Gait.Hurry,
                                                      "A Rushed walk-in hurries (the brisk clip at a higher pace until batch 2's hurry is baked)");
            if (Sort(first).Contains("Impatient")) Check(l.RequestedGait == NpcLocomotion.Gait.Brisk, "An Impatient walk-in is brisk");
        }

        // ---------- 3. a storm-out, watched; the walk out brisk and at once ----------
        deadline = Time.time + 25f;
        while (Time.time < deadline && (first == null || !first.CanHearIntake || first.GetComponent<NpcLocomotion>().IsMoving)) yield return null;
        if (first != null && first.CanHearIntake)
        {
            int stormsBefore = NpcAttentionDirector.StormOutsSeen, stormLooksBefore = NpcAttentionDirector.StormOutLooks;
            int near = NpcAttentionDirector.People.Count(p => p != null && p.transform != first.transform && p.Available(true)
                                                          && Vector3.Distance(p.transform.position, first.transform.position) < 6.5f && p.CanSee(first.transform.position, 110f));
            NpcLocomotion l = first.GetComponent<NpcLocomotion>();
            StormOutMethod.Invoke(first, null);
            yield return Wait(2.5f);
            int watched = NpcAttentionDirector.StormOutLooks - stormLooksBefore;
            Check(NpcAttentionDirector.StormOutsSeen > stormsBefore, "The storm-out reached the room (NotifyStormOut)");
            if (near > 0) Check(watched >= 1, $"{watched} of the {near} people who could see the storm-out watched them go");
            else Note($"Nobody who could see the storm-out was within 6.5 m ({watched} watched).");
            float t0 = Time.time, setOff = -1f;
            deadline = Time.time + 8f;
            while (Time.time < deadline && first != null && setOff < 0f) { if (l.Speed > .4f) setOff = Time.time; yield return null; }
            Check(first == null || l.RequestedGait == NpcLocomotion.Gait.Brisk && l.Hurried,
                  $"The storm-out walks out brisk and hurried (asked {(first != null ? l.RequestedGait.ToString() : "-")}, hurried {(first != null && l.Hurried)})");
            Note($"  Set off {(setOff >= 0f ? $"{setOff - t0:0.0} s after the line" : "not within 8 s")} (the line is said standing first).");
        }
        else Note("No customer was free in the queue for the storm-out.");

        // ---------- 4. a Fixed! at the bench turns heads ----------
        int fixedBefore = NpcAttentionDirector.FixedSeen, fixedLooksBefore = NpcAttentionDirector.FixedLooks;
        Vector3 bench = ItemInspector.Instance != null ? ItemInspector.Instance.TableTop + Vector3.up * .12f : (NpcAttentionDirector.CounterPoint ?? new Vector3(-3.2f, 1f, 13f));
        int couldSee = NpcAttentionDirector.People.Count(p => p != null && p.Available(true) && Vector3.Distance(p.transform.position, bench) < 6.5f && p.CanSee(bench, 110f));
        Juice.FixedMoment(bench, bench + Vector3.up * .25f, JobGrade.Perfect);
        yield return Wait(2.5f);
        Check(NpcAttentionDirector.FixedSeen > fixedBefore, "A Fixed! at the bench reached the room (Juice.OnFixed, or the Finishes poll)");
        if (couldSee > 0) Check(NpcAttentionDirector.FixedLooks - fixedLooksBefore >= 1, $"{NpcAttentionDirector.FixedLooks - fixedLooksBefore} of the {couldSee} people who could see the bench looked toward it");
        else Note($"Nobody who could see the bench was within 6.5 m ({NpcAttentionDirector.FixedLooks - fixedLooksBefore} looked).");

        // ---------- 5. patrons stroll ----------
        // Every patron asks for the relaxed walk but the hurried sorts (PatronBrain.IsHurriedSort: a profile named Hurried,
        // or urgency over 0.75), who keep their own: counted here by the same rule, not allowed for by a fixed one (13 Oct:
        // 3 of 14 were hurried sorts).
        var strollers = Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude)
            .Select(p => (loco: p.GetComponent<NpcLocomotion>(), social: p.GetComponent<NpcSocial>())).Where(x => x.loco != null).ToList();
        int hurriedSorts = strollers.Count(x => x.social != null && x.social.Profile != null
                                                && (x.social.Profile.Name.IndexOf("Hurried", StringComparison.OrdinalIgnoreCase) >= 0 || x.social.Profile.urgency > .75f));
        int relaxed = strollers.Count(x => x.loco.RequestedGait == NpcLocomotion.Gait.Relaxed);
        Check(strollers.Count > 0 && relaxed >= strollers.Count - hurriedSorts,
              $"Patrons stroll: {relaxed} of {strollers.Count} asked for the relaxed walk ({hurriedSorts} hurried sort(s) keep their own)");

        // ---------- 6. a pair: two seats of one table, a chat, out together ----------
        PatronSpawner spawner = Object.FindAnyObjectByType<PatronSpawner>();
        PatronBrain leader = spawner != null ? spawner.SendPairNow() : null;
        Check(leader != null && leader.Companion != null, "A pair came in (PatronSpawner.SendPairNow)");
        if (leader != null && leader.Companion != null)
        {
            PatronBrain other = leader.Companion;
            deadline = Time.time + 45f;
            while (Time.time < deadline && !(SeatedIn(leader) && SeatedIn(other))) yield return null;
            bool bothSeated = SeatedIn(leader) && SeatedIn(other);
            float apart = leader.Seat is TableSeat a && other.Seat is TableSeat b ? Vector3.Distance(a.SeatPose.position, b.SeatPose.position) : 99f;
            // One table by PatronBrain.ClaimTableForTwo's measure (1.65 m: a banquette 2-top's chair and bench, the two
            // reading-corner chairs, two across the communal table all count).
            Check(bothSeated && apart < 1.7f, $"The pair sat at one table ({(bothSeated ? $"{apart:0.00} m apart" : "not both seated in 45 s")})");
            if (bothSeated) Photograph(leader.transform, folder, "pair at the table");
            NpcSocial sa = leader.GetComponent<NpcSocial>(), sb = other.GetComponent<NpcSocial>();
            Check(NpcAttentionDirector.AreCompanions(sa, sb), "The attention director knows them as companions");
            keepSeated = keepSeated.Concat(new[] { leader, other }).ToArray();
            deadline = Time.time + 30f;
            bool chatted = false;
            while (Time.time < deadline && !chatted) { chatted = sa != null && sb != null && sa.InChat && sa.Chat == sb.Chat; yield return null; }
            // The chat rolls dice each pass (×0.6 for companions): counted, not asserted (QA, 13 Oct).
            if (chatted) Check(true, "The pair fell into a chat within 30 s of sitting");
            else Note("The pair hadn't fallen into a chat within 30 s (the chat is a roll each pass; companions roll more often).");
            keepSeated = keepSeated.Where(p => p != leader && p != other).ToArray();
            float leaderLeftAt = Time.time;
            PatronLeaveMethod.Invoke(leader, null);
            float left = -1f;
            deadline = Time.time + 6f;
            while (Time.time < deadline && left < 0f) { if (other == null || other.IsLeaving) left = Time.time; yield return null; }
            Check(left >= 0f, $"When the first got up, the other followed{(left >= 0f ? $" {left - leaderLeftAt:0.0} s later" : " (not within 6 s)")}");
        }

        // ---------- 7. the cup: on the table, then into the bins ----------
        bool bins = NpcLifeProps.TryGetBin(out Vector3 drop, out Vector3 mouth);
        if (!bins) Note("No bin station they can reach (LI_BinStation): cups go home with people; the bin beat is not tested.");
        else Note($"Bin station: drop point {drop}, mouth {mouth}.");
        // 24 Oct 2026 (Days, clearing tables): about one guest in three leaves the cup on the table (NpcCup.LeavesItBehind, their
        // own dice for the visit). The bin beat is tested on one who bins it; one who leaves it is tested in 7b.
        NpcSeating cupSeat = Seated().FirstOrDefault(s => s.GetComponent<PatronBrain>() != null && keepSeated.Contains(s.GetComponent<PatronBrain>())
                                                         && !NpcCup.LeavesItBehind(s.gameObject, s.GetComponent<PatronBrain>().SettledGuest));
        NpcSeating leaverSeat = Seated().FirstOrDefault(s => s.GetComponent<PatronBrain>() != null && keepSeated.Contains(s.GetComponent<PatronBrain>())
                                                         && s != cupSeat && NpcCup.LeavesItBehind(s.gameObject, s.GetComponent<PatronBrain>().SettledGuest));
        if (cupSeat != null)
        {
            PatronBrain cupPatron = cupSeat.GetComponent<PatronBrain>();
            NpcCup cup = NpcCup.Of(cupPatron.gameObject);
            deadline = Time.time + 12f;
            while (Time.time < deadline && (cup == null || !cup.OnTable)) { cup = NpcCup.Of(cupPatron.gameObject); yield return null; }
            Check(cup != null && cup.OnTable, "A seated patron's cup stood on their table within 12 s" + (cup != null && cup.Cup != null ? $" (at {cup.Cup.transform.position})" : ""));
            if (cup != null && cup.OnTable) Photograph(cupPatron.transform, folder, "cup on the table");
            keepSeated = keepSeated.Where(p => p != cupPatron).ToArray();
            int binnedBefore = NpcCup.Binned;
            NpcLocomotion l = cupPatron.GetComponent<NpcLocomotion>();
            PatronLeaveMethod.Invoke(cupPatron, null);
            bool wentToBin = false, photographed = false;
            deadline = Time.time + 30f;
            while (Time.time < deadline && cupPatron != null && NpcCup.Binned == binnedBefore)
            {
                if (l.Purpose == "bin") wentToBin = true;
                if (!photographed && wentToBin && l.HasArrived) { photographed = true; Photograph(cupPatron.transform, folder, "at the bins"); }
                yield return null;
            }
            if (bins)
            {
                Check(wentToBin, "Leaving with a cup, they walked to the bin station first");
                Check(NpcCup.Binned > binnedBefore, $"The cup went into the bins ({NpcCup.Binned - binnedBefore} binned)");
            }
            else Check(cupPatron == null || cup == null || cup.Held || cup.State == NpcCup.Where.Gone, "Without bins the cup went with them");
        }
        else Note("No seated patron left for the cup test (or all of them leave their cups today).");

        // ---------- 7b. a guest who leaves the cup on the table (24 Oct 2026, clearing tables) ----------
        if (leaverSeat != null)
        {
            PatronBrain leaver = leaverSeat.GetComponent<PatronBrain>();
            TableSeat at = leaver.Seat as TableSeat;
            NpcCup cup = NpcCup.Of(leaver.gameObject);
            deadline = Time.time + 12f;
            while (Time.time < deadline && (cup == null || !cup.OnTable)) { cup = NpcCup.Of(leaver.gameObject); yield return null; }
            keepSeated = keepSeated.Where(p => p != leaver).ToArray();
            if (cup != null && cup.OnTable && at != null)
            {
                PatronLeaveMethod.Invoke(leaver, null);
                yield return null;
                DirtyCup left = DirtyCup.Live.FirstOrDefault(c => c != null && c.FromAGuest && c.seat == at && c.OnTheTable);
                Check(left != null && at.IsDirty && !at.IsAvailable && left.table != null && left.table.Badge != 0,
                    $"A guest who leaves the cup left it on {at.name}'s table: a used cup there, the seat out of play, the table's badge up");
                if (left != null) Photograph(left.transform, folder, "a guest's cup left on the table");
            }
            else Note("The leaver's cup never stood on their table within 12 s: 7b not tested.");
        }
        else Note("Nobody seated today leaves their cup (about one in three do): 7b not tested this run.");

        // ---------- 8. a laptop patron ----------
        if (NpcLifeProps.LaptopCount == 0) Note("No laptops in the scene (LI_Laptop_Open): laptop patrons are ordinary patrons; not tested.");
        else
        {
            // A laptop seat must be free for the test to mean anything (both were taken by ordinary patrons on 13 Oct, and
            // the laptop patron rightly sat elsewhere as an ordinary one): whoever sits at one is asked to leave first.
            var laptopSeats = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => NpcLifeProps.LaptopFor(s) != null).ToList();
            if (laptopSeats.Count > 0 && !laptopSeats.Any(s => s.IsAvailable))
            {
                foreach (TableSeat s in laptopSeats)
                {
                    PatronBrain sitter = s.Occupant as PatronBrain;
                    if (sitter == null) continue;
                    keepSeated = keepSeated.Where(p => p != sitter).ToArray();
                    PatronLeaveMethod.Invoke(sitter, null);
                }
                deadline = Time.time + 15f;
                while (Time.time < deadline && !laptopSeats.Any(s => s.IsAvailable)) yield return null;
                Note($"  Laptop seats freed first: {laptopSeats.Count(s => s.IsAvailable)} of {laptopSeats.Count} free now ({string.Join(", ", laptopSeats.Select(s => s.name))}).");
            }
            PatronBrain worker = spawner != null ? spawner.SendLaptopPatronNow() : null;
            Check(worker != null, "A laptop patron came in (PatronSpawner.SendLaptopPatronNow)");
            if (worker != null)
            {
                deadline = Time.time + 45f;
                while (Time.time < deadline && !SeatedIn(worker)) yield return null;
                Transform laptop = worker.Seat is TableSeat ws ? NpcLifeProps.LaptopFor(ws, 1.2f) ?? NearestProp(ws, NpcLifeProps.LaptopName) : null;
                bool laptopOn = laptop != null && laptop.gameObject.activeInHierarchy && NpcLifeProps.UserOf(laptop) == worker;
                Check(SeatedIn(worker) && worker.AtLaptop, $"The laptop patron sat at a laptop seat ({(worker.Seat != null ? worker.Seat.name : "no seat")})");
                Check(laptopOn, "Their laptop is switched on and theirs" + (laptop != null ? $" ({laptop.name})" : ""));
                NpcSocial ws2 = worker.GetComponent<NpcSocial>();
                Check(ws2 != null && ws2.Prop != null, "Their eyes have the screen to go to (NpcSocial.Prop)");
                Transform bag = worker.Seat is TableSeat bs ? NearestProp(bs, NpcLifeProps.BagName) : null;
                Note(bag != null ? $"  Bag {bag.name}: {(bag.gameObject.activeInHierarchy ? "on at their feet" : "not switched on")}." : "  No LI_ToteBag near that seat.");
                if (SeatedIn(worker)) { yield return Wait(2f); Photograph(worker.transform, folder, "laptop patron"); }
                PatronLeaveMethod.Invoke(worker, null);
                yield return Wait(2f);
                Check(laptop == null || !laptop.gameObject.activeInHierarchy, "Getting up, the laptop is switched off again");
            }
        }

        // ---------- 9. the nod hook ----------
        CustomerBrain anyone = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).FirstOrDefault();
        bool safe = true;
        try { if (anyone != null) anyone.OnAceReplied(); }
        catch (Exception e) { safe = false; Note("OnAceReplied threw: " + e.Message); }
        Check(safe, "CustomerBrain.OnAceReplied is safe to call outside a conversation (does nothing)");

        Note("Director: " + NpcAttentionDirector.Summary());
        Note($"Cups made {NpcCup.Made}, binned {NpcCup.Binned}; upper-layer beats {NpcBeats.UpperStarts}, carried {NpcBeats.Carried}.");
        keepSeated = Array.Empty<PatronBrain>();
        lab.PatronsLeaveNow();
        lab.CustomersLeaveNow();
    }

    private static string Sort(CustomerBrain c) => c != null && c.Identity != null && c.Identity.Archetype != null ? c.Identity.Archetype.archetypeName ?? "" : "";

    private static List<NpcSeating> Seated() =>
        Object.FindObjectsByType<NpcSeating>(FindObjectsInactive.Exclude)
            .Where(s => s.Current == NpcSeating.Phase.Seated && s.Seat != null && s.GetComponent<PatronBrain>() != null).ToList();

    private static bool SeatedIn(PatronBrain p) => p != null && p.GetComponent<NpcSeating>() is NpcSeating s && s.Current == NpcSeating.Phase.Seated;

    private static Transform NearestProp(TableSeat seat, string prefix)
    {
        Transform best = null;
        float bestD = 1.5f * 1.5f;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || !t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            float d = (t.position - seat.SeatPose.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // ================================================================== helpers

    private static void Photograph(Transform who, string into, string label)
    {
        try
        {
            if (who == null || into == null) return;
            Vector3 head = Barks.HeadPointOf(who);
            Vector3 forward = who.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            string name = $"{Directory.GetFiles(into, "*.png").Length + 1:00} {label}";
            CafeSecondPassSteps.Capture(Path.Combine(into, name + ".png"), head + forward * 2.2f + right * 1.1f + Vector3.up * .25f, head + Vector3.down * .35f, 38f, false);
        }
        catch (Exception e) { Note("photo failed: " + e.Message); }
    }

    private static Transform FindRig(Transform root, string name)
    {
        var visual = root.GetComponent<PolygonNpcVisual>();
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        return Find(root, name, skip);
    }

    private static Transform Find(Transform node, string name, Transform skip)
    {
        if (node == skip || node.name.StartsWith("City look", StringComparison.Ordinal)) return null;
        if (node.name == name) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform hit = Find(node.GetChild(i), name, skip);
            if (hit != null) return hit;
        }
        return null;
    }

    private static void Pause(NpcSocial social)
    {
        if (social == null || AmbientField == null) return;
        AmbientField.SetValue(social, false);
        social.StopGesture();
        paused.Add(social);
    }

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
        string text = $"{title} - {verdict} ({checks - failures} of {checks})\n{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{report}";
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "report.txt");
        File.WriteAllText(file, text);
        if (failures == 0) Debug.Log($"{Tag}{title}: {verdict} ({checks - failures}/{checks}). {file}\n{report}");
        else Debug.LogWarning($"{Tag}{title}: {verdict} ({checks - failures}/{checks}). {file}\n{report}");
    }

    private static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcLife"));

    private static void WriteLog(string file, string text)
    {
        Directory.CreateDirectory(LogRoot);
        File.WriteAllText(Path.Combine(LogRoot, file), text);
    }

    private static void RequireStopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
