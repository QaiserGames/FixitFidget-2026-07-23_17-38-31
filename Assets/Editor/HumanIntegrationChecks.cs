#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class HumanIntegrationChecks
{
    private const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab";

    [MenuItem("Fixit Fidget/Checks/Human counter integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running Human integration checks.");
        CheckPhone();
        CheckMissingTask();
        CheckLayoutAndInput();
        Debug.Log("[Human integration] PASS: phone fault selection, ownership, pause/recap guards, " +
            "physical completion, reassembly, pause and input cleanup. " +
            "No scene, prefab, money or save changes. Perform the counter playtest next.");
    }

    private static void CheckPhone()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PhonePath);
        var fixture = new GameObject("Human owner check") { hideFlags = HideFlags.HideAndDontSave };
        fixture.SetActive(false);
        DayClock oldClock = DayClock.Instance;
        float oldScale = Time.timeScale;
        try
        {
            Clock(null);
            Time.timeScale = 1f;
            var def = root.GetComponent<DeviceDefinition>();
            var repair = root.GetComponent<RepairJob>();
            Require(def != null && repair != null && def.faults.Length >= 4, "Human phone fault is installed.");
            Require(def.faults[0].type == FaultType.Mechanical && def.faults[1].type == FaultType.Cleaning
                && def.faults[2].type == FaultType.Software && def.faults[3].type == FaultType.Human,
                "Existing serialized fault indices have not moved.");
            def.ApplyFault(3);
            var record = new Job { faultType = FaultType.Human, faultIndex = 3 };
            repair.Configure(record);
            var human = root.GetComponentInChildren<HumanFault>();
            Require(human != null && human.TotalTasks == 1, "Physical toggle is one task.");
            Require(repair.Family == JobFamily.Human && repair.Grade == JobGrade.Rejected && !repair.IsComplete,
                "Untouched Human fault does not receive the old free Perfect.");
            Require(root.GetComponentsInChildren<CircuitPuzzle>().Length == 0
                && root.GetComponentsInChildren<ReplaceablePart>().Length == 0
                && root.GetComponentsInChildren<GrimeSpot>().Length == 0, "Only the chosen family contributes work.");

            var owner = fixture.AddComponent<CustomerBrain>();
            Set(owner, "record", record); Set(owner, "activeJob", repair); Set(owner, "jobAccepted", true);
            Set(owner, "state", CustomerBrain.State.Waiting);
            var strangerObject = new GameObject("Other customer");
            strangerObject.transform.SetParent(fixture.transform, false);
            var stranger = strangerObject.AddComponent<CustomerBrain>();
            Require(!human.Flip(owner), "Unowned task cannot be answered.");
            repair.SetOwner(owner);
            Require(owner.CanFixAtCounter && !human.Flip(stranger), "Only the accepted device's customer can advance it.");
            Time.timeScale = 0f;
            Require(!human.Flip(owner) && human.RemainingTasks == 1, "Paused conversations cannot progress.");
            Time.timeScale = 1f;
            var clock = fixture.AddComponent<DayClock>();
            clock.SetDay(1); Clock(clock);
            clock.RestoreRecap(new RecapSaveData { day = 1 });
            Time.timeScale = 1f; // Recap guard must work even if another system changes the clock.
            Require(!human.Flip(owner), "A closed-day recap independently blocks input.");
            Clock(null);
            human.gameObject.SetActive(false);
            Require(repair.Grade == JobGrade.Rejected && !human.Flip(owner), "Hidden task cannot complete.");
            human.gameObject.SetActive(true);
            Set(owner, "state", CustomerBrain.State.Leaving);
            Require(!human.Flip(owner), "Departing owner cannot be repaired.");
            Set(owner, "state", CustomerBrain.State.WaitingInQueue);
            Require(human.Flip(owner) && repair.Grade == JobGrade.Perfect && repair.IsComplete,
                "One physical action completes the actual RepairJob.");
            Require(!human.Flip(owner), "Repeated input cannot award another task.");
            repair.RegisterDetached(fixture);
            Require(!repair.CanHandBack && !repair.IsComplete, "Conversation cannot hand back a disassembled phone.");
            repair.UnregisterDetached(fixture);
            Require(repair.CanHandBack && repair.IsComplete, "Reassembly restores handback eligibility.");
            UnityEngine.Object.DestroyImmediate(human.gameObject);
            Require(repair.Grade == JobGrade.Rejected, "Deleted task fails closed instead of retaining free completion.");
        }
        finally
        {
            Clock(oldClock); Time.timeScale = oldScale;
            UnityEngine.Object.DestroyImmediate(fixture);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CheckMissingTask()
    {
        var root = new GameObject("Missing Human task") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var repair = root.AddComponent<RepairJob>();
            repair.Configure(new Job { faultType = FaultType.Human });
            Require(repair.Grade == JobGrade.Rejected, "Missing Human component cannot make an empty perfect repair.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckLayoutAndInput()
    {
        var root = new GameObject("Human UI check") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var ui = root.AddComponent<ConversationUI>();
            TMP_Text choices = TextChild(root.transform, "Choices");
            TMP_Text dialogue = TextChild(root.transform, "Dialogue");
            Set(ui, "optionsText", choices); Set(ui, "dialogueText", dialogue);
            choices.rectTransform.sizeDelta = new Vector2(900, 60);
            choices.rectTransform.anchoredPosition = new Vector2(100, 120);
            dialogue.rectTransform.anchoredPosition = new Vector2(100, 240);
            ui.SetHumanLayout(true); ui.SetHumanLayout(true);
            Require(choices.rectTransform.sizeDelta.y == 140 && dialogue.rectTransform.anchoredPosition.y == 296,
                "Repeated layout calls do not accumulate offsets.");
            ui.Hide();
            Require(choices.rectTransform.sizeDelta == new Vector2(900, 60)
                && choices.rectTransform.anchoredPosition == new Vector2(100, 120)
                && dialogue.rectTransform.anchoredPosition == new Vector2(100, 240), "Intake layout restores exactly after Human dialogue.");
            var controller = root.AddComponent<ConversationController>();
            Set(controller, "ui", ui); Set(controller, "conversationOpen", true);
            var interactor = root.AddComponent<PlayerInteractor>();
            var station = root.AddComponent<StationInteractable>();
            Set(interactor, "conversation", controller); Set(interactor, "currentStation", station);
            Invoke(interactor, "OnBack");
            Require(interactor.CurrentStation == station, "Back key belongs to the open conversation.");
            controller.End();
            Require(controller.InConversation, "Closing consumes the key for the rest of this frame.");
            Invoke(interactor, "OnBack");
            Require(interactor.CurrentStation == station, "Closing key cannot also exit a station.");
            Set(controller, "closedAtFrame", -1); // Next-frame ownership in a non-running Editor fixture.
            Require(!controller.InConversation, "Ownership returns on the following frame.");
            Set(controller, "conversationOpen", true);
            Invoke(controller, "Update"); // A destroyed/missing partner.
            Require(!(bool)Get(controller, "conversationOpen"), "Missing customer cannot strand an open dialogue.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Fixit Fidget/Playtest/Spawn Human phone customer")]
    public static void SpawnPracticeCustomer() => SpawnPractice(PhonePath, FaultType.Human);

    [MenuItem("Fixit Fidget/Playtest/Spawn support-call customer")]
    public static void SpawnSupportCustomer() => SpawnPractice("Assets/AssetsPrefabs/PhoneJob.prefab", FaultType.Bureaucratic);

    private static void SpawnPractice(string prefabPath, FaultType family)
    {
        if (!EditorApplication.isPlaying || Time.timeScale <= 0f
            || (DayClock.Instance != null && DayClock.Instance.DayOver))
            throw new InvalidOperationException("Enter Play Mode during an open day first.");
        var spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null) throw new InvalidOperationException("No active customer spawner in this scene.");
        var source = Get(spawner, "customerPrefab") as GameObject;
        var start = Get(spawner, "spawnPoint") as Transform;
        var exit = Get(spawner, "exitPoint") as Transform;
        var queue = Get(spawner, "counterQueue") as CounterQueue;
        var archetypes = Get(spawner, "archetypes") as CustomerArchetype[];
        var phone = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (source == null || start == null || exit == null || queue == null || !queue.HasFreeSlot || phone == null)
            throw new InvalidOperationException("Need the scene's customer/door references and a free counter slot.");
        var def = phone.GetComponent<DeviceDefinition>();
        int index = def != null ? Array.FindIndex(def.faults, f => f != null && f.type == family) : -1;
        if (index < 0) throw new InvalidOperationException("Prefab has no requested fault family.");
        if (!NavMesh.SamplePosition(start.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            throw new InvalidOperationException("No walkable NavMesh at the scene's spawn point.");
        GameObject guest = UnityEngine.Object.Instantiate(source, hit.position, start.rotation);
        var brain = guest.GetComponent<CustomerBrain>();
        var identity = guest.GetComponent<CustomerIdentity>();
        var agent = guest.GetComponent<NavMeshAgent>();
        if (brain == null || identity == null || agent == null || !agent.isOnNavMesh)
        {
            UnityEngine.Object.Destroy(guest);
            throw new InvalidOperationException("Customer prefab needs its brain, identity and an agent on the NavMesh.");
        }
        guest.name = family + " practice customer";
        identity.SetupWalkIn(archetypes != null ? Array.Find(archetypes, a => a != null) : null, "Practice guest");
        brain.Init(queue, exit, new Job { devicePrefab = phone, deviceName = def.displayName,
            faultIndex = index, faultType = family, faultDescription = def.faults[index].description, payout = 0 });
        Selection.activeGameObject = guest;
        Debug.Log("Practice guest is joining the normal counter queue. " +
            "Human: click the mute switch at intake. Support: E calls from the shelf, then answer when it rings. " +
            "Zero payout, but this visit counts in this playtest's recap/log. No save was reset.");
    }

    private static TMP_Text TextChild(Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.AddComponent<TextMeshProUGUI>();
    }
    private static void Clock(DayClock clock) => typeof(DayClock)
        .GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, clock);
    private static object Get(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    private static void Require(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Human integration: " + why);
    }
}
#endif
