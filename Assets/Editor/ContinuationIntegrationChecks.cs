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
            var cup = cupObject.AddComponent<DrinkJob>();
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
            var second = secondObject.AddComponent<DrinkJob>();
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
            EditorSceneManager.ClosePreviewScene(scene);
            typeof(ShopInventory).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, previousInventory);
            if (recipe != null) UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    [MenuItem("Fixit Fidget/Checks/Grace camera content")]
    public static void GraceContent()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GraceShowcaseSetup.CameraPath);
        Require(prefab != null, "Run Content > Grace showcase > Create prototype assets first.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camera = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(camera, scene);
            var definition = camera.GetComponent<DeviceDefinition>(); definition.ApplyFault(0);
            var job = camera.GetComponent<GraceCameraRepairJob>();
            Require(job != null && job.Shutter != null && job.Quality == 0, "Broken shutter is not a usable camera.");
            var grime = camera.GetComponentsInChildren<GrimeSpot>(); Require(grime.Length == 3, "Lens and film path have three cleaning tasks.");
            var request = new FeaturedRepairRequest { devicePrefab = prefab, faultIndex = 0, storyEpisodeId = GraceCameraEpisode.EpisodeId };
            Require(request.TryCreateJob(out Job record, out _) && record.storyEpisodeId == GraceCameraEpisode.EpisodeId,
                "Featured job carries the exact camera episode.");
            job.Shutter.Activate(); Require(job.Grade == JobGrade.Passable, "Working but dirty camera gives the imperfect-print path.");
            UnityEngine.Object.DestroyImmediate(grime[0].gameObject);
            UnityEngine.Object.DestroyImmediate(grime[1].gameObject);
            Require(job.Grade == JobGrade.Good, "Mostly cleaned working camera is Good.");
            UnityEngine.Object.DestroyImmediate(grime[2].gameObject);
            Require(job.Grade == JobGrade.Perfect, "Clean working camera is Perfect.");
            Require(camera.transform.Find("KEEP - scratched sentimental strap") != null, "Original strap survives the repair.");
            Debug.Log("[Grace content] PASS: exact request, broken-shutter gate, cleaning grades and preserved strap. No scenes or saves changed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
}
#endif
