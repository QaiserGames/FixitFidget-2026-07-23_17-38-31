using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Cinemachine resolves its camera in LateUpdate. The final presentation pass is
// repeated immediately before rendering so held objects share that exact pose.
[DefaultExecutionOrder(10000)]
public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField, Range(1, 2)] private int capacity = 2;
    [SerializeField, HideInInspector] private bool sharedHandsInitialized;
    [Tooltip("Deferred prototype only. Final hands will be authored and rigged in Blender.")]
    [SerializeField] private bool showExperimentalHands = false;
    private sealed class Held
    {
        public JobBase item;
        public int hand;
        public Vector3 scale;
        public Renderer[] renderers;
        public bool[] visible;
        public Collider[] colliders;
        public bool[] collision;
        public Vector3 visualCentre;
        public Vector3 visualBounds;
        public UnityEngine.Rendering.ShadowCastingMode[] shadows;
    }
    private readonly List<Held> hands = new();
    private PlayerInteractor interaction;
    private int selected;
    private int preferredPickupHand = -1;
    private Camera viewCamera;
    private ConversationController dialogue;
    private ItemInspector inspector;
    private CounterRepairView counterRepair;
    private PlayerHandsVisual physicalHands;

    public JobBase Carried { get { Prune(); return SelectedHeld()?.item; } }
    public bool IsCarrying => Carried != null;
    public int Count { get { Prune(); return hands.Count; } }
    public bool HasSpace => Count < capacity;
    public int Capacity => capacity;
    public int SelectedIndex
    {
        get { Prune(); return preferredPickupHand >= 0 ? hands.FindIndex(h => h.hand == preferredPickupHand) : selected; }
    }
    public int SelectedHandIndex
    {
        get { Prune(); return preferredPickupHand >= 0 ? preferredPickupHand : hands.Count > 0 ? hands[selected].hand : 0; }
    }
    public JobBase GetItem(int index) { Prune(); return index >= 0 && index < hands.Count ? hands[index].item : null; }
    public JobBase GetHandItem(int side) { Prune(); return hands.Find(h => h.hand == side)?.item; }
    public bool Contains(JobBase item) { Prune(); return item != null && hands.Exists(h => h.item == item); }
    public void SetCapacity(int count)
    {
        capacity = Mathf.Clamp(count, 1, 2); sharedHandsInitialized = true;
        if (preferredPickupHand >= capacity) preferredPickupHand = -1;
    }

    // Left = 0, right = 1. An empty selected hand is intentionally empty:
    // it must never silently place, discard or exchange the other hand's item.
    public bool SelectHand(int side)
    {
        if (side < 0 || side >= capacity) return false;
        Prune(); preferredPickupHand = side;
        int index = hands.FindIndex(h => h.hand == side);
        if (index >= 0) selected = index;
        return true;
    }
    public void UseAutomaticHand() { preferredPickupHand = -1; Prune(); }
    public void SelectNext()
    {
        UseAutomaticHand();
        if (hands.Count > 1) selected = (selected + 1) % hands.Count;
    }
    private Held SelectedHeld()
    {
        if (preferredPickupHand >= 0) return hands.Find(h => h.hand == preferredPickupHand);
        return hands.Count > 0 ? hands[selected] : null;
    }
    private void Awake()
    {
        if (!sharedHandsInitialized) { capacity = 2; sharedHandsInitialized = true; }
        interaction = GetComponent<PlayerInteractor>();
        dialogue = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        viewCamera = Camera.main;
        // Existing scenes may still carry the old component after a reload.
        var oldHUD = GetComponent<PlayerCarryHUD>();
        if (oldHUD != null) oldHUD.Retire();
        if (Application.isPlaying && showExperimentalHands)
        {
            physicalHands = GetComponent<PlayerHandsVisual>();
            if (physicalHands == null) physicalHands = gameObject.AddComponent<PlayerHandsVisual>();
        }
    }
    private void OnEnable()
    {
        Application.onBeforeRender += RefreshPresentation;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        Camera.onPreCull += RefreshForCamera;
    }
    private void BeforeCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        => RefreshForCamera(camera);
    private void RefreshForCamera(Camera camera)
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (camera == viewCamera) RefreshPresentation();
    }
    private void Prune()
    {
        hands.RemoveAll(h => h.item == null);
        selected = hands.Count == 0 ? 0 : Mathf.Clamp(selected, 0, hands.Count - 1);
    }
    private void Update()
    {
        Prune();
        bool canSwitch = Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput);
        if (canSwitch && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) SelectNext();
    }
    private void LateUpdate() => RefreshPresentation();
    private void RefreshPresentation()
    {
        Prune();
        if (viewCamera == null) viewCamera = Camera.main;
        if (interaction == null) interaction = GetComponent<PlayerInteractor>();
        bool atStation = interaction != null && interaction.IsAtStation;
        bool beverageView = atStation && interaction.CurrentStation.GetComponent<BeverageStation>() != null && viewCamera != null;
        bool show = (!atStation || beverageView)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput) && (DayClock.Instance == null || !DayClock.Instance.DayOver);
        Quaternion facing = UprightRotation(beverageView ? viewCamera.transform.forward : transform.forward);
        if (physicalHands != null) physicalHands.SetVisible(show);
        for (int side = 0; side < capacity; side++)
        {
            Held held = hands.Find(h => h.hand == side);
            bool active = side == SelectedHandIndex;
            Vector3 centre = HandCentre(side, beverageView, active);
            if (held != null)
            {
                // Metre-authored objects keep their real scale in both views.
                held.item.transform.localScale = held.scale;
                held.item.transform.rotation = facing;
                held.item.transform.position += centre - held.item.transform.TransformPoint(held.visualCentre);
                for (int j = 0; j < held.renderers.Length; j++)
                    if (held.renderers[j] != null) held.renderers[j].enabled = show && held.visible[j];
            }
            if (physicalHands != null && show)
            {
                bool cup = held != null && held.item is DrinkJob;
                Vector3 size = held != null ? held.visualBounds : Vector3.zero;
                physicalHands.Pose(side, centre, facing, held != null, cup, size,
                    beverageView ? viewCamera : null, transform);
            }
        }
        if (physicalHands != null) physicalHands.SetCapacity(capacity);
    }
    private Vector3 HandCentre(int side, bool firstPerson, bool active)
    {
        float sign = side == 0 ? -1 : 1;
        if (firstPerson)
            return viewCamera.ViewportToWorldPoint(new Vector3(side == 0 ? .28f : .72f, active ? .20f : .18f,
                Mathf.Max(.62f, viewCamera.nearClipPlane + .40f)));
        // Character origin is at the capsule centre (one metre above the floor).
        // The capsule has no authored hand sockets yet. Keep both items beside
        // its visible sides, clear of its one-metre-wide silhouette. These
        // temporary points belong to the body, never to the isometric camera.
        return transform.TransformPoint(new Vector3(sign * .62f, -.10f + (active ? .025f : 0), -.22f));
    }
    private static Quaternion UprightRotation(Vector3 forward)
    {
        Vector3 level = Vector3.ProjectOnPlane(forward, Vector3.up);
        return level.sqrMagnitude > .001f ? Quaternion.LookRotation(level) : Quaternion.identity;
    }
    public void PickUp(JobBase item) => TryPickUp(item);
    public bool TryPickUp(JobBase item)
    {
        if (!HasSpace || item == null || Contains(item) || item is DrinkJob cup && cup.Locked) return false;
        int side = preferredPickupHand;
        if (side >= 0)
        {
            if (side >= capacity || hands.Exists(h => h.hand == side)) return false;
        }
        else
        {
            side = 0;
            while (side < capacity && hands.Exists(h => h.hand == side)) side++;
            if (side >= capacity) return false;
        }
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None)) spot.Release(item);
        if (item is DrinkJob drink)
            foreach (BeverageSlot slot in FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude)) slot.Release(drink);
        var held = new Held { item = item, hand = side, scale = item.transform.localScale,
            renderers = item.GetComponentsInChildren<Renderer>(true), colliders = item.GetComponentsInChildren<Collider>(true) };
        held.visible = new bool[held.renderers.Length];
        held.shadows = new UnityEngine.Rendering.ShadowCastingMode[held.renderers.Length];
        bool hasBounds = false; Bounds bounds = default;
        for (int i = 0; i < held.renderers.Length; i++)
        {
            var renderer = held.renderers[i]; held.visible[i] = renderer.enabled;
            held.shadows[i] = renderer.shadowCastingMode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer || renderer is ParticleSystemRenderer
                || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
        }
        held.visualCentre = hasBounds ? item.transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        held.visualBounds = hasBounds ? bounds.size : new Vector3(.08f, .112f, .08f);
        held.collision = new bool[held.colliders.Length];
        for (int i = 0; i < held.colliders.Length; i++) { held.collision[i] = held.colliders[i].enabled; held.colliders[i].enabled = false; }
        hands.Add(held); selected = hands.Count - 1; preferredPickupHand = -1;
        return true;
    }
    public void PlaceAt(Transform spot)
    {
        if (spot == null || !IsCarrying) return;
        Held held = SelectedHeld(); Restore(held);
        held.item.transform.position = spot.position + Vector3.up * held.item.restHeight;
        held.item.transform.rotation = spot.rotation;
        hands.Remove(held); preferredPickupHand = -1; Prune();
    }
    public void Consume()
    {
        if (!IsCarrying) return;
        Held held = SelectedHeld();
        hands.Remove(held); preferredPickupHand = -1; Prune(); Destroy(held.item.gameObject);
    }
    private static void Restore(Held held)
    {
        if (held.item == null) return;
        held.item.transform.localScale = held.scale;
        for (int i = 0; i < held.renderers.Length; i++) if (held.renderers[i] != null)
        { held.renderers[i].enabled = held.visible[i]; held.renderers[i].shadowCastingMode = held.shadows[i]; }
        for (int i = 0; i < held.colliders.Length; i++) if (held.colliders[i] != null) held.colliders[i].enabled = held.collision[i];
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= RefreshPresentation;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        Camera.onPreCull -= RefreshForCamera;
        foreach (Held held in hands) Restore(held);
        hands.Clear(); preferredPickupHand = -1;
        if (physicalHands != null) physicalHands.SetVisible(false);
    }
}
