using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private int priority = 0;

    public int Priority => priority;

    public virtual bool IsAvailable => true;
    public abstract string Prompt { get; }
    public abstract void Interact(PlayerInteractor player);

    // Called when the crosshair lands on / leaves this thing.
    public virtual void SetFocused(bool focused) { }
}