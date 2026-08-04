using UnityEngine;

public enum JobFamily { Mechanical, Cleaning, Software, Human, Bureaucratic }

public abstract class JobBase : MonoBehaviour
{
    [SerializeField] protected int payout = 25;
    [SerializeField] protected string jobCardText = "Fix it.";

    public int Payout => payout;
    public string JobCard => jobCardText;
    public abstract JobFamily Family { get; }

    // The only question the customer's brain ever asks.
    public abstract bool IsComplete { get; }

    // Who owns this job — set at spawn so jobs can talk back if needed.
    public CustomerBrain Owner { get; private set; }
    public void SetOwner(CustomerBrain owner) => Owner = owner;

private readonly System.Collections.Generic.List<GameObject> detached = new();

    public void RegisterDetached(GameObject part)
    {
        if (part != null && !detached.Contains(part)) detached.Add(part);
    }

    public void UnregisterDetached(GameObject part) => detached.Remove(part);

    // Destroy anything of ours that's sitting loose on the bench.
    private void OnDestroy()
    {
        foreach (GameObject g in detached)
            if (g != null) Destroy(g);
    }

}