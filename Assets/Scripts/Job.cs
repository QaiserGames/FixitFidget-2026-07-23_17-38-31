using UnityEngine;

public enum JobKind { Repair, Drink }

// The master record of a transaction. Created when a customer spawns,
// before any physical object exists.
public class Job
{
    public JobKind kind = JobKind.Repair;

    // --- repair jobs ---
    public GameObject devicePrefab;
    public string deviceName = "thing";
    public int faultIndex;
    public FaultType faultType;
    public string faultDescription = "broken";
    // Explicit authored episode identity. Random jobs never infer story facts
    // from a display name or a similar-looking device.
    public string storyEpisodeId = "";

    // --- drink jobs ---
    public DrinkDefinition drink;

    public int payout = 25;

    // Assigned on accept.
    public int number;
    public Color color = Color.white;

    // What the ticket and dialogue call this job.
    public string Subject => kind == JobKind.Drink && drink != null ? drink.drinkName : deviceName;
    public string Detail  => kind == JobKind.Drink ? "to make" : faultDescription;
}
