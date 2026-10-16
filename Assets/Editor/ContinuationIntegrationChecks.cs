#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ContinuationIntegrationChecks
{
    [MenuItem("Fixit Fidget/Checks/Dispenser stock and two-hand transfers")]
    public static void Beverage()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousInventory = ShopInventory.Instance;
        DrinkDefinition recipe = null;
        DrinkJob cup = null, second = null;
        try
        {
            var host = new GameObject("Isolated dispenser checks"); SceneManager.MoveGameObjectToScene(host, scene);
            var stock = host.AddComponent<ShopInventory>(); Call(stock, "Awake"); stock.SetStock(5, 5);
            var carry = host.AddComponent<PlayerCarry>(); carry.SetCapacity(2);
            var slotObject = new GameObject("Coffee slot"); slotObject.transform.SetParent(host.transform);
            var slot = slotObject.AddComponent<BeverageSlot>(); slot.cupPoint = slotObject.transform;
            recipe = ScriptableObject.CreateInstance<DrinkDefinition>(); recipe.beansCost = 1; recipe.brewSeconds = 3;
            slot.drink = recipe;
            Require(slot.TryPour() && stock.Beans == 4, "No-cup press spends exactly one portion.");
            Require(!slot.TryPour() && stock.Beans == 4, "A second press during a pour cannot spend stock.");
            Set(slot, "pourLeft", 0f); Call(slot, "Update");
            Require(slot.Cup == null, "Wasted pour creates no cup.");
            var cupObject = new GameObject("Empty cup"); cupObject.transform.SetParent(host.transform);
            cup = cupObject.AddComponent<DrinkJob>();
            // Preview-scene MonoBehaviours do not receive runtime OnEnable.
            // Exercise the same registry lifecycle as a cup spawned in Play Mode.
            Call(cup, "OnEnable");
            Require(carry.TryPickUp(cup) && slot.TransferCup(carry) && !carry.IsCarrying, "Cup leaves the selected hand for its slot.");
            Require(slot.TryPour() && cup.Locked && stock.Beans == 3, "Loaded pour spends stock and locks its cup.");
            Require(!carry.TryPickUp(cup) && !slot.TransferCup(carry), "No mid-pour pickup.");
            Set(slot, "pourLeft", 0f); Call(slot, "Update");
            Require(cup.CanHandBack && cup.Owner == null && cup.HasFreshness, "Finished cup is fresh and not order-bound.");
            stock.SetStock(0, 0); Require(stock.CanMake(recipe), "A prebrewed drink can still be accepted with no unspent stock.");
            stock.SetStock(5, 3);
            Require(!slot.TryPour() && stock.Beans == 3, "Filled cup blocks another drink.");
            Require(slot.TransferCup(carry) && slot.Cup == null, "Lifting frees the slot immediately.");
            var secondObject = new GameObject("Second cup"); secondObject.transform.SetParent(host.transform);
            second = secondObject.AddComponent<DrinkJob>(); Call(second, "OnEnable");
            Require(carry.TryPickUp(second) && carry.Count == 2 && !carry.HasSpace, "Two items fill both hands.");
            Require(!carry.TryPickUp(cup), "The same cup cannot occupy both hands.");
            Require(carry.Carried == second, "Newest pickup is selected.");
            carry.SelectNext(); Require(carry.Carried == cup, "Switching selects the other held item.");
            carry.PlaceAt(slotObject.transform);
            Require(carry.Count == 1 && carry.Carried == second, "Placing selected item preserves the other hand.");
            stock.SetStock(5, 0); Require(!slot.TryPour() && stock.Beans == 0, "No stock cannot become negative.");
            Debug.Log("[Dispenser integration] PASS: waste, single debit, locking, full-slot blocking, unowned freshness, two hands and stock exhaustion. No saves changed.");
        }
        finally
        {
            // Remove only this fixture's cups; never clear a scene's live registry.
            if (cup != null) Call(cup, "OnDisable");
            if (second != null) Call(second, "OnDisable");
            EditorSceneManager.ClosePreviewScene(scene);
            typeof(ShopInventory).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, previousInventory);
            if (recipe != null) UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    // DAYS 1-3 (14 Oct 2026; specs/days-1-3.md §5, specs/systems-forgiving-bench.md §6): the camera comes in twice, so "working
    // but dirty → Passable" can no longer happen in one job. Fault 0 (Day 1, the clean) grades by its three spots: all three
    // Perfect, two Good, one Passable, none Rejected, with the shutter switched off (R1). Fault 1 (Day 2, the shutter) arrives
    // clean: untouched it is Rejected (no photo from a jammed shutter), swapped it is Perfect. The photo reads the shutter's
    // grade, downgraded after a poor clean (R3: ContinuationRuleChecks). Needs Fixit Fidget › Days › B first (the two faults).
    [MenuItem("Fixit Fidget/Checks/Grace camera content")]
    public static void GraceContent()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GraceShowcaseSetup.CameraPath);
        Require(prefab != null, "Run Content > Grace showcase > Create prototype assets first.");
        var cameraDefinition = prefab.GetComponent<DeviceDefinition>();
        Require(cameraDefinition != null && cameraDefinition.faults != null && cameraDefinition.faults.Length == 2,
            "The camera has its two faults, the clean and the shutter (Fixit Fidget › Days › B).");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            // Fault 0, Day 1: the clean.
            var camera = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(camera, scene);
            var definition = camera.GetComponent<DeviceDefinition>(); definition.ApplyFault(0);
            var job = camera.GetComponent<GraceCameraRepairJob>();
            Require(job != null && job.Shutter != null && !job.Shutter.gameObject.activeSelf && !job.ShutterIsTask,
                "Day 1's clean: the shutter part is off (not today's job).");
            var grime = camera.GetComponentsInChildren<GrimeSpot>(); Require(grime.Length == 3, "Lens and film path have three cleaning tasks.");
            var request = new FeaturedRepairRequest { devicePrefab = prefab, faultIndex = 0, storyEpisodeId = GraceCameraEpisode.EpisodeId };
            Require(request.TryCreateJob(out Job record, out _) && record.storyEpisodeId == GraceCameraEpisode.EpisodeId
                    && record.faultType == FaultType.Cleaning, "Day 1's featured job carries the exact camera episode, as a cleaning job.");
            Require(job.Grade == JobGrade.Rejected, "An uncleaned camera is Rejected.");
            UnityEngine.Object.DestroyImmediate(grime[0].gameObject);
            Require(job.Grade == JobGrade.Passable, "One spot of three is Passable (the smudged-photo path, once the shutter is fixed).");
            UnityEngine.Object.DestroyImmediate(grime[1].gameObject);
            Require(job.Grade == JobGrade.Good, "Mostly cleaned camera is Good.");
            UnityEngine.Object.DestroyImmediate(grime[2].gameObject);
            Require(job.Grade == JobGrade.Perfect, "Clean camera is Perfect, its shutter still jammed: it isn't today's job (R1).");
            Require(camera.transform.Find("KEEP - scratched sentimental strap") != null, "Original strap survives the clean.");

            // Fault 1, Day 2: the shutter.
            var second = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(second, scene);
            second.GetComponent<DeviceDefinition>().ApplyFault(1);
            var swap = second.GetComponent<GraceCameraRepairJob>();
            Require(second.GetComponentsInChildren<GrimeSpot>().Length == 0, "Day 2's camera comes back clean (no spots).");
            Require(swap != null && swap.Shutter != null && swap.ShutterIsTask && swap.Grade == JobGrade.Rejected,
                "Broken shutter is not a usable camera: untouched, Rejected.");
            var shutterRequest = new FeaturedRepairRequest { devicePrefab = prefab, faultIndex = 1, storyEpisodeId = GraceCameraEpisode.EpisodeId };
            Require(shutterRequest.TryCreateJob(out Job shutterRecord, out _) && shutterRecord.storyEpisodeId == GraceCameraEpisode.EpisodeId
                    && shutterRecord.faultType != FaultType.Cleaning, "Day 2's featured job carries the exact camera episode, as the shutter visit.");
            swap.Shutter.Activate(); Require(swap.Grade == JobGrade.Perfect, "Swapped, the shutter visit is Perfect.");   // (edit mode: the swap is done at once; the bench)
            Require(second.transform.Find("KEEP - scratched sentimental strap") != null, "Original strap survives the swap.");
            Debug.Log("[Grace content] PASS: exact requests for both visits, the clean graded by its spots (the shutter off), the shutter gate " +
                      "on Day 2, the swap Perfect, and the preserved strap. No scenes or saves changed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
}
#endif
