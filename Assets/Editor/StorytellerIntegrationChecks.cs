#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Transient, unsaved preview-scene fixtures. No real customer, save, money or
// schedule is touched. Runtime rendering/NavMesh are still Play Mode gates.
public static class StorytellerIntegrationChecks
{
    [MenuItem("Fixit Fidget/Checks/Storyteller interaction guards")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running storyteller checks.");
        var scene = EditorSceneManager.NewPreviewScene();
        DayClock oldClock = DayClock.Instance;
        float oldScale = Time.timeScale;
        CustomerProfile profile = ScriptableObject.CreateInstance<CustomerProfile>();
        try
        {
            profile.storyteller = true;
            profile.storyLines = new[] { "A story." };
            profile.storyFirstDelay = 1;
            GameObject host = new("Storyteller check");
            SceneManager.MoveGameObjectToScene(host, scene);
            var identity = host.AddComponent<CustomerIdentity>();
            identity.SetupRegular(profile);
            var brain = host.AddComponent<CustomerBrain>();
            var story = host.AddComponent<CustomerStoryteller>();
            var interactable = host.AddComponent<CustomerInteractable>();
            var device = new GameObject("Pending repair");
            device.transform.SetParent(host.transform);
            var repair = device.AddComponent<RepairJob>();
            var grime = new GameObject("Grime");
            grime.transform.SetParent(device.transform);
            grime.AddComponent<GrimeSpot>();
            repair.Configure(new Job { faultType = FaultType.Cleaning });
            repair.CaptureTasks();
            Set(brain, "identity", identity); Set(brain, "state", CustomerBrain.State.Waiting);
            Set(brain, "activeJob", repair); Set(brain, "storyteller", story);
            Set(brain, "patienceLeft", 60f); Set(brain, "serviceMax", 120f);
            Set(interactable, "brain", brain);
            Clock(null); Time.timeScale = 1f;
            story.Initialize(brain);
            var run = (StorytellerRun)Get(story, "run");
            Require(!brain.CanRequestFocus, "No focus prompt before a story.");
            Require(run.Tick(1f, true), "First line is ready."); run.MarkSpoken();
            Require(brain.CanRequestFocus && interactable.IsAvailable && interactable.FloorAvailable
                && interactable.Prompt == "Let me focus", "Focus is a normal floor interaction, before reassurance.");

            Time.timeScale = 0f; brain.RequestFocus();
            Require(!brain.CanRequestFocus && !story.FocusRequested, "Paused input cannot set a boundary.");
            Time.timeScale = 1f;
            var clockHost = new GameObject("Closed day");
            SceneManager.MoveGameObjectToScene(clockHost, scene);
            clockHost.SetActive(false);
            var clock = clockHost.AddComponent<DayClock>();
            Set(clock, "<DayOver>k__BackingField", true); Clock(clock);
            brain.RequestFocus();
            Require(!brain.CanRequestFocus && !story.FocusRequested, "Recap blocks input even with time scale restored.");
            Clock(null);
            foreach (CustomerBrain.State state in new[] { CustomerBrain.State.WalkingToCounter,
                CustomerBrain.State.WaitingInQueue, CustomerBrain.State.Settling,
                CustomerBrain.State.Speaking, CustomerBrain.State.Leaving })
            {
                Set(brain, "state", state); brain.RequestFocus();
                Require(!brain.CanRequestFocus && !story.FocusRequested, "No focus input in " + state);
            }
            Set(brain, "state", CustomerBrain.State.Waiting);
            var conversation = host.AddComponent<ConversationController>();
            Set(brain, "conversation", conversation);
            brain.RequestFocus();
            Require(!story.FocusRequested, "Intake/conversation owns input.");
            Set(brain, "conversation", null);
            JobGrade grade = repair.Grade;
            brain.RequestFocus();
            Require(story.FocusRequested && !brain.CanRequestFocus && run.Quiet,
                "One press quiets this visit.");
            Require((float)Get(brain, "patienceLeft") == 60f && (int)Get(brain, "reassureUses") == 0
                && repair.Grade == grade, "Focus changes neither patience, reassurance uses nor repair grade.");
            brain.RequestFocus();
            Require((int)Get(brain, "reassureUses") == 0, "A repeated direct request has no side effect.");

            identity.SetupRegular(profile, new RegularMemoryData { visits = 1, focusBoundarySet = true });
            story.Initialize(brain);
            Require(!brain.CanRequestFocus && ((StorytellerRun)Get(story, "run")).Quiet,
                "A returning regular actually stays quiet.");
            identity.SetupWalkIn(null, "Walk-in"); story.Initialize(brain);
            Require(Get(story, "run") == null && !brain.CanRequestFocus, "Walk-ins cannot inherit storyteller state.");
            Debug.Log("[Storyteller interaction] PASS: floor prompt, pause/recap, lifecycle, repeat input, unchanged repair/patience and remembered quiet.");
        }
        finally
        {
            Clock(oldClock); Time.timeScale = oldScale;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private static void Clock(DayClock value) => typeof(DayClock)
        .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static object Get(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Storyteller integration: " + why);
    }
}
#endif
