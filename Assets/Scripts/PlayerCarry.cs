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
        public float overhead = 1f;     // how much bigger it is drawn from above (OverheadScaleFor, at pickup)
        public DropSpot liftedFrom;     // the spot that held it when it was picked up (LiftedFrom); null from anywhere else
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
    private CafeViewMode viewMode;

    public JobBase Carried { get { Prune(); return SelectedHeld()?.item; } }
    public bool IsCarrying => Carried != null;

    /// <summary>What's in hand, in words, for the HUD ("Phone · Cracked screen", "Coffee + Phone · Cracked screen"); "" with nothing.</summary>
    public string Summary
    {
        get
        {
            Prune();
            if (hands.Count == 0) return "";
            string s = "";
            for (int i = 0; i < hands.Count; i++)
            {
                JobBase item = hands[i].item;
                if (item == null) continue;
                string one = item is DrinkJob cup ? (cup.Drink != null ? cup.Drink.drinkName : "Cup")
                    : item.Record != null ? item.Record.Subject + (string.IsNullOrEmpty(item.Record.faultDescription) ? "" : " · " + item.Record.faultDescription)
                    : item.name;
                s += (s.Length > 0 ? "  +  " : "") + one;
            }
            return s;
        }
    }

    // From above, a thing in hand would be a few pixels at its real size, held at the waist beside a wide body: it is
    // carried high, bigger, with a slow bob, so you can see what you've got (Mansoor, 8 Oct: "literally impossible to
    // notice if I'm grabbing an item like a phone or a watch when in isometric view"). First person keeps real sizes.
    //
    // One scale (14 Oct, td-one-scale.md §6.8): the devices are real size now, in Ace's hands too, so a pocket watch is 5 cm
    // and a phone 14. From above (36-42 px a metre) that is 2-3 px across at 2.2x: a carried DEVICE is drawn at least
    // overheadLength long instead (the watch about 6.4x, the camera 2.6x, the phone 3x; 16-19 px), never more than
    // overheadCap times its size. A cup keeps overheadScale. First person keeps real sizes, as before.
    [Header("From above")]
    [Tooltip("How much bigger a carried item is drawn in the overhead view (a cup; and the least for a device: see Overhead Length).")]
    [SerializeField, Range(1f, 4f)] private float overheadScale = 2.2f;
    [Tooltip("From above, a carried device is drawn at least this long, metres (its largest side when it was picked up).")]
    [SerializeField, Range(.1f, 1f)] private float overheadLength = .45f;
    [Tooltip("...but never more than this many times its real size.")]
    [SerializeField, Range(1f, 10f)] private float overheadCap = 7f;
    [Tooltip("How far up and down the carried item bobs in the overhead view, metres.")]
    [SerializeField, Range(0f, .1f)] private float overheadBob = .03f;
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
    /// <summary>
    /// The spot the item in the selected hand was lifted from (the intake tray's DropSpot, a bench slot's), so the spot's prompt
    /// can say "Put it back" (UI/UX checklist v2 §5.7, First Hour #4). Null when it came from anywhere else, or nothing is in
    /// hand. It is forgotten with the item: set down (PlaceAt), handed back or used up (Consume).
    /// </summary>
    public DropSpot LiftedFrom { get { Prune(); Held held = SelectedHeld(); return held != null ? held.liftedFrom : null; } }

    /// <summary>The spot <paramref name="item"/> was lifted from, in whichever hand holds it (null: not carried, or from elsewhere).</summary>
    public DropSpot LiftedFromFor(JobBase item)
    {
        Prune();
        for (int i = 0; i < hands.Count; i++) if (hands[i].item == item) return hands[i].liftedFrom;
        return null;
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
    // WHY THERE IS A CURRENT (30 Sept 2026)
    // Every station, cup stack, drop spot and item asked the scene for the player's hands
    // (FindAnyObjectByType) inside its IsAvailable and Prompt getters, and the interactor asks those
    // of everything within reach every frame: a scene search and 40 B of garbage per ask. Ace has one
    // pair of hands; this is it (with the search kept as the fallback for a scene without Ace).
    public static PlayerCarry Instance { get; private set; }
    public static PlayerCarry Current => Instance != null ? Instance : FindAnyObjectByType<PlayerCarry>();

    private void Awake()
    {
        Instance = this;
        if (!sharedHandsInitialized) { capacity = 2; sharedHandsInitialized = true; }
        interaction = GetComponent<PlayerInteractor>();
        dialogue = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        viewMode = GetComponent<CafeViewMode>();
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
        Instance = this;
        OnEnableBody();
    }

    private void OnEnableBody()
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
        // C, or RB on a controller. At the dispenser LB / RB already pick a hand
        // for each action (the close-up, or its controls under the crosshair in
        // first person: PlayerInteractor.UsingDrinks), so RB is theirs there.
        bool padSwitch = PadInput.Pressed(PadButton.RightShoulder)
            && (interaction == null || !interaction.UsingDrinks);
        if (canSwitch && (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame || padSwitch)) SelectNext();
    }
    private void LateUpdate() => RefreshPresentation();
    private void RefreshPresentation()
    {
        Prune();
        if (viewCamera == null) viewCamera = Camera.main;
        if (interaction == null) interaction = GetComponent<PlayerInteractor>();
        bool atStation = interaction != null && interaction.IsAtStation;
        bool beverageView = atStation && interaction.CurrentStation.GetComponent<BeverageStation>() != null && viewCamera != null;
        // Pausing releases walking input, but the selected camera stays in first
        // person. Keep held objects in that camera's view while the pause is open.
        bool firstPerson = beverageView || !atStation && viewMode != null && viewMode.isActiveAndEnabled
            && viewMode.FirstPersonSelected && viewCamera != null;
        bool show = (!atStation || beverageView)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput) && (DayClock.Instance == null || !DayClock.Instance.DayOver);
        Quaternion facing = UprightRotation(firstPerson ? viewCamera.transform.forward : transform.forward);
        if (physicalHands != null) physicalHands.SetVisible(show);
        for (int side = 0; side < capacity; side++)
        {
            Held held = null;
            for (int i = 0; i < hands.Count; i++) if (hands[i].hand == side) { held = hands[i]; break; }   // no lambda: no garbage a frame
            bool active = side == SelectedHandIndex;
            Vector3 centre = HandCentre(side, firstPerson, active);
            if (held != null)
            {
                // Metre-authored objects keep their real scale in first person; from above they are drawn bigger (see overheadScale,
                // and for a device overheadLength: its own factor, fixed when it was picked up).
                held.item.transform.localScale = firstPerson || !show ? held.scale : held.scale * held.overhead;
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
                    firstPerson ? viewCamera : null, transform);
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
        // The capsule has no authored hand sockets yet. From above the items are held HIGH, beside the shoulders and clear
        // of the one-metre-wide silhouette (the same sideways points as before, so no facing hides them behind the body),
        // with a slow bob. These points belong to the body, never to the isometric camera.
        float bob = overheadBob * Mathf.Sin(Time.time * 2.2f + side * 1.7f);
        return transform.TransformPoint(new Vector3(sign * .62f, .62f + (active ? .04f : 0) + bob, -.22f));
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
        // Which spot held it, before the spots let it go: its prompt says "Put it back" while it is in hand (LiftedFrom).
        DropSpot[] spots = FindObjectsByType<DropSpot>();
        DropSpot from = null;
        foreach (DropSpot spot in spots) if (from == null && spot != null && spot.Holds(item)) from = spot;
        foreach (DropSpot spot in spots) spot.Release(item);
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
            // Its own solid parts only (DeviceBounds, 12 Oct). From above, the job tag over a device on the tray is a screen-sized
            // sprite about 0.4 m across at 34 m: counted, it was "the device's largest side" and a real-size watch was drawn at
            // 2.2x, 11 cm, instead of 0.45 m (one scale, 14 Oct), and it pulled the hand's centre up toward the tag.
            if (!DeviceBounds.Counts(renderer)) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
        }
        held.visualCentre = hasBounds ? item.transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        held.visualBounds = hasBounds ? bounds.size : new Vector3(.08f, .112f, .08f);
        held.overhead = OverheadScaleFor(item, held.visualBounds);
        held.liftedFrom = from;
        held.collision = new bool[held.colliders.Length];
        for (int i = 0; i < held.colliders.Length; i++) { held.collision[i] = held.colliders[i].enabled; held.colliders[i].enabled = false; }
        hands.Add(held); selected = hands.Count - 1; preferredPickupHand = -1;
        Sfx.Play(item is DrinkJob ? "cup.pickup" : "item.pickup", item.transform.position);
        return true;
    }
    // From above: a cup at overheadScale; a device at least overheadLength long (its largest side as it was picked up), at
    // least overheadScale and at most overheadCap times (TD §6.8: "max(overheadScale, 0.45 m ÷ its largest bound), capped at 7x").
    private float OverheadScaleFor(JobBase item, Vector3 size)
    {
        if (item is DrinkJob) return overheadScale;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (longest < 1e-4f) return overheadScale;
        return Mathf.Min(Mathf.Max(overheadCap, overheadScale), Mathf.Max(overheadScale, overheadLength / longest));
    }

    public void PlaceAt(Transform spot)
    {
        if (spot == null || !IsCarrying) return;
        Held held = SelectedHeld(); Restore(held);
        held.item.transform.position = spot.position + Vector3.up * held.item.restHeight;
        held.item.transform.rotation = spot.rotation;
        hands.Remove(held); preferredPickupHand = -1; Prune();
        Sfx.Play(held.item is DrinkJob ? "cup.set" : "item.putdown", spot.position);
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
        if (Instance == this) Instance = null;
        OnDisableBody();
    }

    private void OnDisableBody()
    {
        Application.onBeforeRender -= RefreshPresentation;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        Camera.onPreCull -= RefreshForCamera;
        foreach (Held held in hands) Restore(held);
        hands.Clear(); preferredPickupHand = -1;
        if (physicalHands != null) physicalHands.SetVisible(false);
    }
}
