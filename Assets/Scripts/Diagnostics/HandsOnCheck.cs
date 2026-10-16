using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// HANDS ON IT - PLAY CHECK (15 Oct 2026; a lab session only: Fixit Fidget > Checks > Hands on it - play check (lab, drives
// itself))
//
// Day 1 played by hand: Grace stormed out "Never again" with her camera in the bench's close-up, and again with it fixed in
// Ace's hands. CustomerBrain.HandsOnFloor: nobody walks out on a device in Ace's hands; their patience still drains (the tip
// still pays speed) but stops at a tenth of their bar while it's in the close-up or carried, and runs on once it's put down.
//
// A fresh Day 1 in the lab (its own save), the autopilot off. Grace's visit through the game's own calls (as the café lab's
// autopilot hears and takes a job) until she sits waiting on her camera; then:
//   1. her patience set to 15 % of her bar and her camera in the close-up for 20 s: down to the floor, no further, she stays;
//   2. stepped back from (the camera put down): her patience runs on past the floor within 4 s; put back to half;
//   3. her camera carried (Ace's hands), her patience set to 12 %, 15 s: at the floor, she stays.
// Report: Logs/HandsOn/hands-on-<time>/report.txt. Nothing is saved in the scene; the lab's save is the only file written.
// Play mode ends by itself when it reports.
// ---------------------------------------------------------------------------
public sealed class HandsOnCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.HandsOnCheck";

    protected override string Title => "Hands on it: nobody walks out on a device in Ace's hands - play check";
    protected override string Tag => "[Hands on check]";

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly FieldInfo PatienceField = typeof(CustomerBrain).GetField("patienceLeft", Any);
    static readonly FieldInfo ServiceField = typeof(CustomerBrain).GetField("serviceMax", Any);
    static readonly FieldInfo StateField = typeof(CustomerBrain).GetField("state", Any);

    ItemInspector inspector;
    PlayerCarry carry;

#if UNITY_EDITOR
    // Asked for by the editor (BenchHandOverProbe's menu) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Hands on check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Hands on check (this Play session only)");
        go.AddComponent<HandsOnCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "HandsOn",
            $"hands-on-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    protected override IEnumerator Run()
    {
        Check(PatienceField != null && ServiceField != null && StateField != null, "CustomerBrain's patience, bar and state can be read");
        if (failures > 0) yield break;

        CustomerBrain grace = null;
        yield return Until(() => (grace = Find("Grace")) != null && grace.CanHearIntake, 90f, "Grace at the counter, to be heard");
        if (!lastWait) yield break;
        grace.HearIntake();
        yield return Frames(2);
        Check(grace.CanAcceptJob, "her job can be taken");
        grace.AcceptJob();
        yield return Until(() => grace != null && State(grace) == "Waiting" && grace.ActiveJob != null, 60f, "Grace sat down, waiting on her camera");
        if (!lastWait) yield break;
        JobBase camera = grace.ActiveJob;
        inspector = ItemInspector.Instance;
        carry = PlayerCarry.Current;
        Check(inspector != null && carry != null, "the bench's close-up and Ace's hands are there");
        if (inspector == null || carry == null) yield break;
        float floor = CustomerBrain.HandsOnFloor;
        Note($"Grace's bar {Service(grace):0.0} s, the floor {floor:P0}; her camera: {camera.name}");

        // 1. In the close-up from 15 %: down to the floor and no further, and she stays.
        SetPatience(grace, .15f);
        Check(inspector.BeginInspection(camera), "the close-up opens on her camera");
        yield return Seconds(20f);
        float held = Fraction(grace);
        Check(!grace.IsLeaving && held >= floor - .005f && held <= floor + .02f,
            $"in the close-up 20 s from 15 %: {held:P1} (the floor {floor:P0}), still waiting: {!grace.IsLeaving}");

        // 2. Stepped back from: the camera put down, her patience runs on past the floor.
        if (inspector.IsHoldingItem) inspector.CancelInspection();
        yield return Frames(2);
        Check(!inspector.IsHoldingItem && !carry.Contains(camera), "stepped back: the camera is out of Ace's hands");
        yield return Seconds(4f);
        float after = Fraction(grace);
        Check(after < floor - .01f, $"put down, 4 s later her patience runs on past the floor: {after:P1}");
        SetPatience(grace, .5f);

        // 3. Carried: at the floor, and she stays.
        bool taken = carry.TryPickUp(camera);
        Check(taken && carry.Contains(camera), "her camera picked up, in Ace's hands");
        SetPatience(grace, .12f);
        yield return Seconds(15f);
        float carried = Fraction(grace);
        Check(!grace.IsLeaving && carried >= floor - .005f && carried <= floor + .02f,
            $"carried 15 s from 12 %: {carried:P1} (the floor {floor:P0}), still waiting: {!grace.IsLeaving}");
        SetPatience(grace, .5f);
    }

    protected override void Restore()
    {
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
#if UNITY_EDITOR
        // Play mode ends by itself once the report is written (the editor's next update, after this frame's Finish).
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    static CustomerBrain Find(string name)
    {
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (c != null && c.isActiveAndEnabled && c.CustomerName == name) return c;
        return null;
    }

    static string State(CustomerBrain c) => StateField.GetValue(c)?.ToString() ?? "";
    static float Service(CustomerBrain c) => (float)ServiceField.GetValue(c);
    static float Fraction(CustomerBrain c) => (float)PatienceField.GetValue(c) / Mathf.Max(.001f, Service(c));
    static void SetPatience(CustomerBrain c, float share) => PatienceField.SetValue(c, Service(c) * share);
}
