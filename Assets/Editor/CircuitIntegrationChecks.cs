#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CircuitIntegrationChecks
{
    private const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab";

    [MenuItem("Fixit Fidget/Playtest/Spawn circuit phone at empty bench")]
    public static void SpawnPracticePhone()
    {
        if (!EditorApplication.isPlaying || Time.timeScale <= 0f
            || (DayClock.Instance != null && DayClock.Instance.DayOver))
            throw new InvalidOperationException("Enter Play Mode during an open day first.");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
        if (source == null) throw new InvalidOperationException("Phone prefab missing.");
        GameObject phone = UnityEngine.Object.Instantiate(source);
        phone.name = "Practice circuit phone (no customer)";
        var job = phone.GetComponent<RepairJob>();
        var def = phone.GetComponent<DeviceDefinition>();
        int faultIndex = Array.FindIndex(def.faults, f => f.type == FaultType.Software);
        if (faultIndex < 0)
        {
            UnityEngine.Object.Destroy(phone);
            throw new InvalidOperationException("No phone software fault is installed.");
        }
        foreach (DropSpot spot in UnityEngine.Object.FindObjectsByType<DropSpot>(FindObjectsSortMode.None))
        {
            if (spot.Kind != DropSpot.SpotKind.Bench || !spot.CanAccept(job)) continue;
            Transform point = spot.ResolvePoint(job);
            if (point == null) continue;
            def.ApplyFault(faultIndex);
            job.Configure(new Job { deviceName = "Practice phone", faultType = FaultType.Software,
                faultIndex = faultIndex, faultDescription = "Practice: reconnect the signal", payout = 0 });
            phone.transform.SetPositionAndRotation(point.position + Vector3.up * job.restHeight, point.rotation);
            Selection.activeGameObject = phone;
            Debug.Log("Practice phone placed on an empty bench. Inspect it normally with F, then left-click. " +
                "It has no customer/payout and disappears when you stop Play Mode. No save was reset.");
            return;
        }
        UnityEngine.Object.Destroy(phone);
        throw new InvalidOperationException("No empty repair-bench slot. Free a slot before spawning the practice phone.");
    }

    [MenuItem("Fixit Fidget/Checks/Circuit integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running circuit integration checks.");
        for (int fault = 0; fault < 3; fault++) CheckPhone(fault);
        CheckPhone(2, true);
        CheckSharedFaultObjects();
        CheckInputIsolation();
        Debug.Log("[Circuit integration] PASS: prefab references, all phone faults, immediate grading, " +
            "hidden work, software family, shared fault objects, and recap/inspection guards. " +
            "No scene, prefab, money or save changes were made. Still perform the circuit playtest.");
    }

    private static void CheckPhone(int faultIndex, bool exhaustRetryCredit = false)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PhonePath);
        try
        {
            var def = root.GetComponent<DeviceDefinition>();
            var repair = root.GetComponent<RepairJob>();
            Require(def != null && repair != null && def.faults.Length >= 3, "Phone retains the existing faults and appended software fault.");
            Require(def.faults[0].type == FaultType.Mechanical && def.faults[1].type == FaultType.Cleaning
                && def.faults[2].type == FaultType.Software, "Serialized fault indices preserved.");
            def.ApplyFault(faultIndex);
            repair.Configure(new Job { faultType = def.faults[faultIndex].type });
            Require(repair.Quality == 0f && repair.Grade == JobGrade.Rejected, "Fresh fault is not Perfect before Start.");
            CircuitPuzzle puzzle = root.GetComponentInChildren<CircuitPuzzle>();
            if (faultIndex != 2)
            {
                Require(puzzle == null, "Circuit not enabled for mechanical/cleaning faults.");
                if (faultIndex == 1)
                {
                    GrimeSpot[] spots = root.GetComponentsInChildren<GrimeSpot>();
                    Require(spots.Length == 3, "Original three-task cleaning fault retained.");
                    UnityEngine.Object.DestroyImmediate(spots[0].gameObject);
                    Require(repair.Grade == JobGrade.Passable, "One of three cleaned = Passable.");
                    UnityEngine.Object.DestroyImmediate(spots[1].gameObject);
                    Require(repair.Grade == JobGrade.Good, "Two of three cleaned = Good.");
                    UnityEngine.Object.DestroyImmediate(spots[2].gameObject);
                    Require(repair.Grade == JobGrade.Perfect, "All three cleaned = Perfect.");
                }
                return;
            }
            Require(puzzle != null && repair.Family == JobFamily.Software, "Software fault is a playable task with correct family.");
            var material = new SerializedObject(puzzle).FindProperty("boardMaterial").objectReferenceValue as Material;
            Require(material != null && material.shader != null && material.shader.name == "FixItFiasco/CircuitBoard"
                && !ShaderUtil.ShaderHasError(material.shader), "Explicit board shader reference imports without errors.");
            Require(root.GetComponentsInChildren<GrimeSpot>().Length == 0
                && root.GetComponentsInChildren<ReplaceablePart>().Length == 0, "Other fault tasks excluded.");
            puzzle.gameObject.SetActive(false);
            Require(repair.Quality == 0f, "Hiding circuit cannot award completion.");
            puzzle.gameObject.SetActive(true);
            var run = puzzle.Run;
            Require(run.Count >= 6 && run.Count <= 9, "Phone route stays within the authored attention budget.");
            if (exhaustRetryCredit)
            {
                if (run.IsAligned(0)) run.Turn(0);
                for (int attempt = 0; attempt < run.Count + 2; attempt++)
                {
                    run.Tick(100f, true);
                    Require(run.Halted && run.Retry(), "Deliberate failure spends one retry.");
                }
                Require(repair.Grade == JobGrade.Rejected, "Unfinished zero-credit circuit is still Rejected.");
            }
            for (int i = 0; i < run.Count; i++)
                for (int r = 0; r < 4 && !run.IsAligned(i); r++) run.Turn(i);
            for (int i = 0; i < run.Count; i++) run.Tick(100f, true);
            if (exhaustRetryCredit)
                Require(run.Finished && repair.Quality > 0f && repair.Grade == JobGrade.Passable,
                    "Finished circuit reaches Passable through real RepairJob grading after excessive retries.");
            else
                Require(repair.Quality == 1f && repair.IsComplete, "Verified circuit flows into existing quality/grade.");
            var detached = new GameObject("Test detached cover");
            try
            {
                repair.RegisterDetached(detached);
                Require(!repair.CanHandBack && !repair.IsComplete, "Circuit cannot bypass reassembly gate.");
                repair.UnregisterDetached(detached);
            }
            finally { UnityEngine.Object.DestroyImmediate(detached); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void CheckSharedFaultObjects()
    {
        var root = new GameObject("Circuit fault selection check") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var shared = new GameObject("Shared fault part"); shared.transform.SetParent(root.transform);
            var def = root.AddComponent<DeviceDefinition>();
            def.faults = new[] {
                new DeviceFault { enableObjects = new[] { shared } },
                new DeviceFault { enableObjects = new[] { shared } }
            };
            def.ApplyFault(0);
            Require(shared.activeSelf, "Later fault cannot disable chosen shared part.");
            def.ApplyFault(-1);
            Require(shared.activeSelf, "Fault selection clamps consistently with GetFault.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckInputIsolation()
    {
        var root = new GameObject("Circuit input check") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false); // No Awake, camera search, input, save writes or view allocation.
        DayClock oldClock = DayClock.Instance;
        float oldScale = Time.timeScale;
        try
        {
            var clock = root.AddComponent<DayClock>();
            // Inactive fixtures never run Awake/Start. Mirror save loading by
            // setting the day before RestoreRecap validates its checkpoint.
            clock.SetDay(1);
            typeof(DayClock).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, clock);
            var repair = root.AddComponent<RepairJob>();
            var puzzle = root.AddComponent<CircuitPuzzle>();
            var inspector = root.AddComponent<ItemInspector>();
            var station = root.AddComponent<StationInteractable>();
            var interactor = root.AddComponent<PlayerInteractor>();
            Set(station, "isWorkSurface", true); Set(interactor, "currentStation", station);
            Set(inspector, "interaction", interactor); Set(inspector, "focusedItem", repair);
            Set(inspector, "focusedCircuits", new[] { puzzle }); Set(puzzle, "inspector", inspector);
            Set(puzzle, "scrambledTiles", 0);
            puzzle.EnsureInitialized();
            bool hasScramble = false;
            for (int i = 0; i < puzzle.Run.Count; i++) hasScramble |= !puzzle.Run.IsAligned(i);
            Require(hasScramble, "Normal job clamps a zero scramble setting to at least one wrong tile.");
            var tile = root.AddComponent<CircuitTile>();
            tile.Build(puzzle, 0, null, null, Color.white);
            int turns = puzzle.Run.Turns(0);
            tile.Activate();
            Require(puzzle.Run.Turns(0) == turns, "Disabled/non-inspected puzzle cannot be clicked.");
            Set(puzzle, "<IsBoosting>k__BackingField", true);
            Set(puzzle, "boostRequiresRelease", false);
            puzzle.HideForInspection();
            Require(!puzzle.IsBoosting, "Leaving inspection cancels pulse boost.");
            Require((bool)typeof(CircuitPuzzle).GetField("boostRequiresRelease",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(puzzle),
                "Reopening inspection requires releasing a held boost key.");
            clock.RestoreRecap(new RecapSaveData { day = 1 });
            Require(clock.Day == 1 && clock.DayOver && !clock.IsOpen && Time.timeScale == 0f,
                "The fixture restores a matching closed-day recap before checking input.");
            Require(!puzzle.IsBeingInspected && !tile.CanInteract, "Recap blocks circuit input.");
            typeof(CircuitPuzzle).GetMethod("UpdateBoost", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(puzzle, new object[] { true });
            Require(!puzzle.IsBoosting, "Held Space cannot enable boost during recap.");
            inspector.CancelInspection();
            Require(inspector.FocusedItem == null && !puzzle.ContainsHudPoint(Vector2.zero), "Inspection release clears circuit interaction.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            typeof(DayClock).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, oldClock);
            Time.timeScale = oldScale;
        }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Circuit integration: " + message);
    }
}
#endif
