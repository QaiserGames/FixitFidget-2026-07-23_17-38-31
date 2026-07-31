using UnityEngine;

public class RepairJob : MonoBehaviour
{
    [SerializeField] private int payout = 25;

    public int Payout => payout;

    // The job is done when every grime spot on this item has been scrubbed away.
    public bool IsComplete => GetComponentsInChildren<GrimeSpot>().Length == 0;
}