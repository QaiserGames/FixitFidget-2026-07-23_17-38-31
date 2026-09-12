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
            // Preview scenes already isolate rendering. Keeping this fixture near
            // the origin avoids half-millimetre float quantization at 5 km when
            // checking a cup held only 62 cm away from a perspective camera.
            host.transform.position = new Vector3(5, 5, 5);
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
                "Item indices match actual carried items and newest pickup selection.");
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
            Vector3 floorPosition = cup.transform.position;
            view.transform.position += new Vector3(4, 2, -3); view.orthographic = true; view.orthographicSize = 8;
            Call(carry, "LateUpdate");
            Require(Vector3.Distance(cup.transform.position, floorPosition) < .0001f,
                "Moving or resizing the isometric camera cannot drag a held cup away from the character.");
            Vector3 step = new Vector3(.7f, 0, -.3f); player.transform.position += step;
            Call(carry, "LateUpdate");
            Require(Vector3.Distance(cup.transform.position, floorPosition + step) < .001f && cup.transform.localScale == new Vector3(.8f, 1.2f, .9f),
                "Floor carrying follows the body at the exact authored scale.");
            var beverage = Child(host, "Beverage view"); beverage.AddComponent<BeverageStation>();
            var beverageStation = beverage.AddComponent<StationInteractable>();
            Set(interactor, "currentStation", beverageStation); Set(carry, "interaction", interactor);
            view.orthographic = false;
            Call(carry, "LateUpdate");
            var centre = view.WorldToViewportPoint(cup.GetComponentInChildren<Renderer>().bounds.center);
            Require(Vector3.Dot(cup.transform.up, Vector3.up) > .999f, "Held drinks stay upright while the station camera looks down.");
            Require(centre.x > .2f && centre.x < .4f && centre.y > .05f && centre.y < .3f && centre.z > view.nearClipPlane,
                "The left-hand cup is visible in the lower left of the beverage view.");
            view.transform.position += new Vector3(.2f, .1f, .3f);
            view.transform.rotation = Quaternion.Euler(15, 48, 0);
            typeof(PlayerCarry).GetMethod("RefreshForCamera", Private).Invoke(carry, new object[] { view });
            var afterCameraMove = view.WorldToViewportPoint(cup.GetComponentInChildren<Renderer>().bounds.center);
            Require(Vector2.Distance(centre, afterCameraMove) < .001f,
                $"A camera pose resolved after LateUpdate still renders the cup in the same hand without a frame of lag. Before {centre:F6}; after {afterCameraMove:F6}.");
            Require(carry.GetHandItem(0) == cup && carry.GetHandItem(1) == null && carry.SelectedHandIndex == 0,
                "The first cup physically occupies the left hand.");
            Require(carry.SelectHand(1) && carry.Carried == null && carry.Count == 1,
                "Choosing an empty right hand cannot act on the left cup.");
            var rightCup = Cup(host, "Right cup");
            Require(carry.TryPickUp(rightCup) && carry.GetHandItem(1) == rightCup && carry.GetHandItem(0) == cup,
                "The right hand takes a separate cup without moving the left cup.");
            carry.SelectHand(0); carry.PlaceAt(Child(host, "Left return").transform);
            Require(carry.GetHandItem(0) == null && carry.GetHandItem(1) == rightCup,
                "Placing the left cup never shifts the right cup into the other hand.");
            carry.SelectHand(0);
            carry.PlaceAt(Child(host, "Empty left hand target").transform);
            Require(carry.GetHandItem(1) == rightCup && carry.Count == 1,
                "A second placement with the empty left hand never moves the right-hand cup.");
            var replacement = Cup(host, "Replacement cup"); carry.SelectHand(1);
            Require(!carry.TryPickUp(replacement) && carry.GetHandItem(1) == rightCup && carry.Count == 1,
                "An explicitly occupied hand refuses pickup even when the other hand is empty.");
            carry.UseAutomaticHand();
            Require(carry.TryPickUp(replacement) && carry.GetHandItem(0) == replacement,
                "Automatic pickup uses the available hand after explicit selection is cleared.");
            Call(carry, "LateUpdate");
            var rightCentre = view.WorldToViewportPoint(rightCup.GetComponentInChildren<Renderer>().bounds.center);
            Require(rightCentre.x > .6f && rightCentre.x < .8f, "The right cup stays visibly in the right hand.");
            Set(interactor, "currentStation", null);
            view.orthographic = true;
            view.transform.position = player.transform.position + new Vector3(-4, 4, -4);
            view.transform.LookAt(player.transform.position);
            Call(carry, "LateUpdate");
            foreach (var carriedCup in new[] { replacement, rightCup })
            {
                // The real capsule is radius .5. A ray toward the established
                // isometric view must miss it by at least the cup's radius.
                Vector3 offset = carriedCup.GetComponentInChildren<Renderer>().bounds.center - player.transform.position;
                Vector2 planarOffset = new Vector2(offset.x, offset.z);
                Vector2 towardCamera = new Vector2(-view.transform.forward.x, -view.transform.forward.z).normalized;
                float alongRay = Mathf.Max(0, -Vector2.Dot(planarOffset, towardCamera));
                Require((planarOffset + towardCamera * alongRay).magnitude > .545f,
                    "Both carried cups remain visible past the capsule silhouette in the isometric view.");
            }
            var oldOverlay = Child(player, "Carried items");
            var retired = player.AddComponent<PlayerCarryHUD>(); retired.Retire();
            Require(!oldOverlay.activeSelf && !retired.enabled, "An existing hands overlay is retired rather than rebuilt.");
            Debug.Log("[Carry interaction] PASS: two-device bench drops, input guards, exact placement restoration, stable left/right slots, occupied-hand rejection, camera-independent visible floor carry, first-person cup presentation and retired hands HUD. No scene or save changes.");
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
