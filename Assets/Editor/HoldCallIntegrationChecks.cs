#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class HoldCallIntegrationChecks
{
    [MenuItem("Fixit Fidget/Checks/Support call integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents("Assets/AssetsPrefabs/PhoneJob.prefab");
        var fixture = new GameObject("Support call check") { hideFlags = HideFlags.HideAndDontSave };
        fixture.SetActive(false);
        float scale = Time.timeScale;
        DayClock clock = DayClock.Instance;
        try
        {
            Clock(null); Time.timeScale = 1;
            var job = root.GetComponent<HoldCallJob>();
            var definition = root.GetComponent<DeviceDefinition>();
            Require(job != null && definition != null && definition.faults[0].type == FaultType.Bureaucratic, "Authored call prefab.");
            Require(root.GetComponentsInChildren<JobBase>(true).Length == 1, "Exactly one job owns the device.");
            var input = root.GetComponentInChildren<PhoneInteractable>();
            Require(input != null && !job.CanActivate && !job.IsComplete, "Unowned phone cannot advance.");
            var owner = fixture.AddComponent<CustomerBrain>();
            Set(owner, "jobAccepted", true); Set(owner, "state", CustomerBrain.State.Waiting);
            job.SetOwner(owner); job.Configure(new Job { faultType = FaultType.Bureaucratic });
            Require(job.CanActivate && job.Grade == JobGrade.Rejected, "Accepted unresolved call has no false repair credit.");
            Time.timeScale = 0; job.Activate();
            Require(job.CurrentPhase == HoldCallRun.State.NeedsDialing, "Paused input is ignored.");
            Time.timeScale = 1; job.Activate();
            var run = (HoldCallRun)Get(job, "run");
            Require(job.CurrentPhase == HoldCallRun.State.Connecting, "One E connects without a menu.");
            run.Tick(1000);
            Require(job.MissedCalls == 1 && job.CanActivate, "A missed answer can be redialed.");
            job.Activate(); run.Tick(.6f); run.Tick(run.Remaining);
            Require(job.CurrentPhase == HoldCallRun.State.Ringing, "Retry reaches the answer window.");
            Set(owner, "state", CustomerBrain.State.Leaving); job.Activate();
            Require(!job.IsComplete, "Departing customer cannot complete a call.");
            Set(owner, "state", CustomerBrain.State.Waiting); job.Activate();
            Require(job.IsComplete && job.Grade == JobGrade.Perfect, "Successful retry completes without a hidden grade penalty.");
            job.Activate();
            Require(job.IsComplete && !job.CanActivate, "Completed call is single-shot.");
            Debug.Log("[Support integration] PASS: ownership, prefab, pause, miss/retry, completion and grade guards. No saves changed.");
        }
        finally { Clock(clock); Time.timeScale = scale; UnityEngine.Object.DestroyImmediate(fixture); PrefabUtility.UnloadPrefabContents(root); }
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Clock(DayClock clock) => typeof(DayClock).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, clock);
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
}
#endif
