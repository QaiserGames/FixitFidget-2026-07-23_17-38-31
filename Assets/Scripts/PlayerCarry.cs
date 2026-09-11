using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField] private float carryScale = .6f;
    [SerializeField, Range(1, 2)] private int capacity = 2;
    [SerializeField, HideInInspector] private bool sharedHandsInitialized;
    private sealed class Held
    {
        public JobBase item;
        public Vector3 scale;
        public Renderer[] renderers;
        public bool[] visible;
        public Collider[] colliders;
        public bool[] collision;
        public Vector3 visualCentre;
        public float visualSize;
        public UnityEngine.Rendering.ShadowCastingMode[] shadows;
    }
    private readonly List<Held> hands = new();
    private PlayerInteractor interaction;
    private int selected;
    private Camera viewCamera;
    private ConversationController dialogue;
    private ItemInspector inspector;
    private CounterRepairView counterRepair;
    public JobBase Carried { get { Prune(); return hands.Count > 0 ? hands[selected].item : null; } }
    public bool IsCarrying => Carried != null;
    public int Count { get { Prune(); return hands.Count; } }
    public bool HasSpace => Count < capacity;
    public int Capacity => capacity;
    public int SelectedIndex { get { Prune(); return selected; } }
    public JobBase GetItem(int index) { Prune(); return index >= 0 && index < hands.Count ? hands[index].item : null; }
    public bool Contains(JobBase item) { Prune(); return item != null && hands.Exists(h => h.item == item); }
    public void SetCapacity(int count) { capacity = Mathf.Clamp(count, 1, 2); sharedHandsInitialized = true; }
    private void Awake()
    {
        // Upgrade existing scenes whose serialized one-hand value predates shared carrying.
        // An explicit SetCapacity call still supports legacy/test scenes that need one slot.
        if (!sharedHandsInitialized) { capacity = 2; sharedHandsInitialized = true; }
        interaction = GetComponent<PlayerInteractor>();
        dialogue = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        viewCamera = Camera.main;
        if (Application.isPlaying && GetComponent<PlayerCarryHUD>() == null) gameObject.AddComponent<PlayerCarryHUD>();
    }
    private void Prune()
    {
        hands.RemoveAll(h => h.item == null);
        selected = hands.Count == 0 ? 0 : Mathf.Clamp(selected, 0, hands.Count - 1);
    }
    public void SelectNext() { Prune(); if (hands.Count > 1) selected = (selected + 1) % hands.Count; }
    private void Update()
    {
        Prune();
        bool canSwitch = Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput);
        if (canSwitch && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) SelectNext();
    }
    private void LateUpdate()
    {
        Prune();
        if (viewCamera == null) viewCamera = Camera.main;
        Transform anchor = carryPoint != null ? carryPoint : transform;
        bool show = (interaction == null || !interaction.IsAtStation || interaction.CurrentStation.GetComponent<BeverageStation>() != null)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput) && (DayClock.Instance == null || !DayClock.Instance.DayOver);
        for (int i = 0; i < hands.Count; i++)
        {
            Held held = hands[i];
            if (viewCamera != null && show) PositionInView(held, i);
            else
            {
                held.item.transform.localScale = held.scale * carryScale;
                held.item.transform.position = anchor.position + (hands.Count > 1 ? anchor.right * (i == 0 ? -.18f : .18f) : Vector3.zero);
                held.item.transform.rotation = anchor.rotation;
            }
            for (int j = 0; j < held.renderers.Length; j++)
                if (held.renderers[j] != null) held.renderers[j].enabled = show && held.visible[j];
        }
    }
    private void PositionInView(Held held, int index)
    {
        float distance = Mathf.Max(.75f, viewCamera.nearClipPlane + .5f);
        float height = viewCamera.orthographic ? viewCamera.orthographicSize * 2
            : 2 * distance * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
        float diameter = height * (held.item is DrinkJob ? .105f : .14f);
        float factor = diameter / Mathf.Max(.01f, held.visualSize);
        if (viewCamera.orthographic) distance = Mathf.Max(distance, viewCamera.nearClipPlane + diameter + .2f);
        held.item.transform.localScale = held.scale * factor;
        if (held.item is DrinkJob)
        {
            Vector3 facing = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            held.item.transform.rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        }
        else held.item.transform.rotation = viewCamera.transform.rotation * Quaternion.Euler(10, -15, index == selected ? -4 : 4);
        Vector3 centre = viewCamera.ViewportToWorldPoint(new Vector3(hands.Count > 1 ? .32f + index * .16f : .40f,
            index == selected ? .155f : .135f, distance));
        held.item.transform.position += centre - held.item.transform.TransformPoint(held.visualCentre);
    }
    public void PickUp(JobBase item) => TryPickUp(item);
    public bool TryPickUp(JobBase item)
    {
        if (!HasSpace || item == null || Contains(item) || item is DrinkJob cup && cup.Locked) return false;
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None)) spot.Release(item);
        if (item is DrinkJob drink)
            foreach (BeverageSlot slot in FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude)) slot.Release(drink);
        var held = new Held { item = item, scale = item.transform.localScale,
            renderers = item.GetComponentsInChildren<Renderer>(true), colliders = item.GetComponentsInChildren<Collider>(true) };
        held.visible = new bool[held.renderers.Length];
        held.shadows = new UnityEngine.Rendering.ShadowCastingMode[held.renderers.Length];
        bool hasBounds = false; Bounds bounds = default;
        for (int i = 0; i < held.renderers.Length; i++)
        {
            var renderer = held.renderers[i]; held.visible[i] = renderer.enabled;
            held.shadows[i] = renderer.shadowCastingMode; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Projected labels and freshness rings must not inflate the physical item bounds.
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer || renderer is ParticleSystemRenderer
                || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
        }
        held.visualCentre = hasBounds ? item.transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        held.visualSize = hasBounds ? bounds.size.magnitude : .15f;
        held.collision = new bool[held.colliders.Length];
        for (int i = 0; i < held.colliders.Length; i++) { held.collision[i] = held.colliders[i].enabled; held.colliders[i].enabled = false; }
        item.transform.localScale = held.scale * carryScale;
        hands.Add(held); selected = hands.Count - 1;
        return true;
    }
    public void PlaceAt(Transform spot)
    {
        if (spot == null || !IsCarrying) return;
        Held held = hands[selected]; Restore(held);
        held.item.transform.position = spot.position + Vector3.up * held.item.restHeight;
        held.item.transform.rotation = spot.rotation;
        hands.RemoveAt(selected); Prune();
    }
    public void Consume()
    {
        if (!IsCarrying) return;
        var item = hands[selected].item;
        hands.RemoveAt(selected); Prune(); Destroy(item.gameObject);
    }
    private static void Restore(Held held)
    {
        if (held.item == null) return;
        held.item.transform.localScale = held.scale;
        for (int i = 0; i < held.renderers.Length; i++) if (held.renderers[i] != null)
        { held.renderers[i].enabled = held.visible[i]; held.renderers[i].shadowCastingMode = held.shadows[i]; }
        for (int i = 0; i < held.colliders.Length; i++) if (held.colliders[i] != null) held.colliders[i].enabled = held.collision[i];
    }
    private void OnDisable() { foreach (Held held in hands) Restore(held); hands.Clear(); }
}
