#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CarryInteractionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Two-hand bench placement and carry state")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        float timeScale = Time.timeScale;
        try
        {
            var host = new GameObject("Isolated carry checks"); SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = new Vector3(5000, 5000, 5000);
            var player = Child(host, "Player");
            var carry = player.AddComponent<PlayerCarry>();
            Set(carry, "capacity", 1); Call(carry, "Awake");
            Require(carry.Capacity == 2, "Old serialized one-hand scenes acquire shared capacity without a dispenser.");
            var interactor = player.AddComponent<PlayerInteractor>(); Call(interactor, "Awake");

            var benchObject = Child(host, "Bench");
            var benchCollider = benchObject.AddComponent<BoxCollider>();
            var area = benchObject.AddComponent<ItemSlotArea>();
            var points = new[] { Child(benchObject, "Slot 1").transform, Child(benchObject, "Slot 2").transform };
            points[0].localPosition = new Vector3(-.25f, 0, 0); points[1].localPosition = new Vector3(.25f, 0, 0);
            Set(area, "slots", points); Set(area, "baseSlots", 2); Set(area, "capacitySource", ItemSlotArea.CapacitySource.Fixed);
            var drop = benchObject.AddComponent<DropSpot>(); Set(drop, "slotArea", area);
            var bench = benchObject.AddComponent<StationInteractable>(); Set(bench, "dropSpot", drop); Set(bench, "isWorkSurface", true);
            var owner = Child(host, "Device owner").AddComponent<CustomerBrain>();
            var first = Device(host, "First device", owner); var second = Device(host, "Second device", owner);
            var firstRenderer = first.GetComponentInChildren<Renderer>();
            var firstCollider = first.GetComponentInChildren<Collider>();
            var originalScale = first.transform.localScale; var originalShadows = firstRenderer.shadowCastingMode;
            Require(carry.TryPickUp(first) && carry.TryPickUp(second), "Two different items fit the shared hands.");
            Require(carry.Count == 2 && carry.SelectedIndex == 1 && carry.GetItem(0) == first && carry.GetItem(1) == second,
                "HUD indices match actual carried items and newest pickup selection.");
            Require(!firstCollider.enabled && !carry.TryPickUp(first), "Held colliders are disabled and duplicate pickup is rejected.");
            bench.Interact(interactor);
            Require(drop.Holds(second) && carry.Carried == first && carry.Count == 1, "First set-down preserves the remaining selected item.");
            var secondPickup = second.GetComponent<ItemInteractable>();
            Require(secondPickup.IsAvailable, "The placed device remains a valid pickup, reproducing the original target conflict.");
            var candidates = new[] { benchCollider, second.GetComponentInChildren<Collider>() };
            Require(Choose(interactor, candidates) == bench, "The just-placed priority-3 item cannot steal the second set-down.");
            var loose = Device(host, "Loose device", owner);
            Require(Choose(interactor, new[] { benchCollider, loose.GetComponentInChildren<Collider>() }) == loose.GetComponent<ItemInteractable>(),
                "Loose items remain available for intentional two-hand pickup.");
            var customerAction = Child(host, "Customer action").AddComponent<CustomerInteractable>();
            Require(!bench.PrefersPlacementOver(customerAction, carry), "The placement preference never overrides customer actions.");
            bench.Interact(interactor);
            Require(carry.Count == 0 && drop.Holds(first) && drop.Holds(second), "Both items can be placed without changing player position.");
            Require(first.transform.localScale == originalScale && firstCollider.enabled && firstRenderer.shadowCastingMode == originalShadows,
                "Placement restores model scale, collision and shadows.");

            Require(carry.TryPickUp(loose), "A third item can be carried after hands are empty.");
            Require(!bench.PrefersPlacementOver(secondPickup, carry), "A full bench does not redirect a pickup into a failed drop.");
            carry.SetCapacity(1); Require(carry.Capacity == 1 && !carry.HasSpace, "Explicit one-hand capacity remains supported.");
            Set(interactor, "focused", secondPickup); Set(interactor, "lastInteractionFrame", -1);
            Time.timeScale = 0; Call(interactor, "OnInteract");
            Require(carry.Count == 1 && drop.Holds(second), "Paused input cannot pick up a stale focused item.");
            Time.timeScale = timeScale;
            Set(interactor, "lastInteractionFrame", Time.frameCount); Call(interactor, "OnInteract");
            Require(carry.Count == 1 && drop.Holds(second), "Duplicate callbacks in one frame do not activate twice.");
            carry.PlaceAt(Child(host, "Loose return").transform);
            carry.SetCapacity(2);
            var cup = Cup(host, "Held cup");
            var view = Child(host, "Carry camera").AddComponent<Camera>();
            view.nearClipPlane = .03f; view.fieldOfView = 58; view.aspect = 16f / 9f;
            view.transform.rotation = Quaternion.Euler(35, 22, 0);
            Set(carry, "viewCamera", view);
            Require(carry.TryPickUp(cup), "The cup shares the same carrying path as repair devices.");
            Call(carry, "LateUpdate");
            var centre = view.WorldToViewportPoint(cup.GetComponentInChildren<Renderer>().bounds.center);
            Require(Vector3.Dot(cup.transform.up, Vector3.up) > .999f, "Held drinks stay upright while the station camera looks down.");
            Require(centre.x > .2f && centre.x < .7f && centre.y > .05f && centre.y < .3f && centre.z > view.nearClipPlane,
                "The held cup remains visible in the lower view without looking at the floor.");
            Debug.Log("[Carry interaction] PASS: shared capacity migration, two sequential device bench drops, loose pickups, customer priority, full benches, selection, restoration, pause, duplicate-input guards and upright visible cups. No scene or save changes.");
        }
        finally { Time.timeScale = timeScale; EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static DrinkJob Cup(GameObject parent, string name)
        => Item<DrinkJob>(parent, name);
    private static RepairJob Device(GameObject parent, string name, CustomerBrain owner)
    { var device = Item<RepairJob>(parent, name); device.SetOwner(owner); return device; }
    private static T Item<T>(GameObject parent, string name) where T : JobBase
    {
        var obj = Child(parent, name); obj.transform.localScale = new Vector3(.8f, 1.2f, .9f);
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.transform.SetParent(obj.transform, false);
        mesh.transform.localScale = new Vector3(.05f, .12f, .05f);
        var job = obj.AddComponent<T>();
        var pickup = obj.AddComponent<ItemInteractable>(); Call(pickup, "Awake");
        typeof(Interactable).GetField("priority", Private).SetValue(pickup, 3);
        return job;
    }
    private static GameObject Child(GameObject parent, string name)
    { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
    private static Interactable Choose(PlayerInteractor player, Collider[] candidates)
        => (Interactable)typeof(PlayerInteractor).GetMethod("FindFloorTarget", Private).Invoke(player, new object[] { candidates });
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
