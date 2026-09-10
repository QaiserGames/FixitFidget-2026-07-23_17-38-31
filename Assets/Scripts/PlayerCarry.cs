using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField] private float carryScale = .6f;
    // The beverage setup opts in. Existing scenes retain their established capacity.
    [SerializeField, Range(1, 2)] private int capacity = 1;
    private sealed class Held
    {
        public JobBase item;
        public Vector3 scale;
        public Renderer[] renderers;
        public bool[] visible;
        public Collider[] colliders;
        public bool[] collision;
    }
    private readonly List<Held> hands = new();
    private PlayerInteractor interaction;
    private int selected;
    public JobBase Carried { get { Prune(); return hands.Count > 0 ? hands[selected].item : null; } }
    public bool IsCarrying => Carried != null;
    public int Count { get { Prune(); return hands.Count; } }
    public bool HasSpace => Count < capacity;
    public bool Contains(JobBase item) { Prune(); return item != null && hands.Exists(h => h.item == item); }
    public void SetCapacity(int count) => capacity = Mathf.Clamp(count, 1, 2);
    private void Awake() => interaction = GetComponent<PlayerInteractor>();
    private void Prune()
    {
        hands.RemoveAll(h => h.item == null);
        selected = hands.Count == 0 ? 0 : Mathf.Clamp(selected, 0, hands.Count - 1);
    }
    public void SelectNext() { Prune(); if (hands.Count > 1) selected = (selected + 1) % hands.Count; }
    private void Update()
    {
        Prune();
        var dialogue = GetComponent<ConversationController>();
        var inspector = GetComponent<ItemInspector>();
        bool canSwitch = Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (GetComponent<CounterRepairView>() == null || !GetComponent<CounterRepairView>().OwnsInput);
        if (canSwitch && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) SelectNext();
        Transform anchor = carryPoint != null ? carryPoint : transform;
        bool show = interaction == null || !interaction.IsAtStation;
        for (int i = 0; i < hands.Count; i++)
        {
            Held held = hands[i];
            held.item.transform.position = anchor.position + (hands.Count > 1 ? anchor.right * (i == 0 ? -.18f : .18f) : Vector3.zero);
            held.item.transform.rotation = anchor.rotation;
            for (int j = 0; j < held.renderers.Length; j++)
                if (held.renderers[j] != null) held.renderers[j].enabled = show && held.visible[j];
        }
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
        for (int i = 0; i < held.renderers.Length; i++) held.visible[i] = held.renderers[i].enabled;
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
        for (int i = 0; i < held.renderers.Length; i++) if (held.renderers[i] != null) held.renderers[i].enabled = held.visible[i];
        for (int i = 0; i < held.colliders.Length; i++) if (held.colliders[i] != null) held.colliders[i].enabled = held.collision[i];
    }
    private void OnDisable() { foreach (Held held in hands) Restore(held); hands.Clear(); }
}
