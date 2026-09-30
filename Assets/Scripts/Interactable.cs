using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private int priority = 0;

    public int Priority => priority;

    /// <summary>
    /// For things made in code (a night's zone, say), which have no Inspector: with several in reach,
    /// the higher priority is offered first; equal, the nearer one. Barnaby on the stoop (0) beats the
    /// way into the house (-1), so "Take Barnaby" stays first while he stands there.
    /// </summary>
    protected void SetPriority(int value) => priority = value;

    public virtual bool IsAvailable => true;
    public abstract string Prompt { get; }
    public abstract void Interact(PlayerInteractor player);

    // Called when the crosshair lands on / leaves this thing.
    public virtual void SetFocused(bool focused) { }
}
