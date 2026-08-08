using UnityEngine;

public class JobIdentityManager : MonoBehaviour
{
    public static JobIdentityManager Instance { get; private set; }

    [Tooltip("Job colours, assigned in order and cycling. Keep them bright and distinct.")]
    [SerializeField] private Color[] palette =
    {
        new Color(0.95f, 0.55f, 0.15f),   // orange
        new Color(0.25f, 0.65f, 0.95f),   // sky blue
        new Color(0.55f, 0.85f, 0.35f),   // lime
        new Color(0.90f, 0.35f, 0.75f),   // pink
        new Color(0.95f, 0.85f, 0.25f),   // yellow
        new Color(0.60f, 0.45f, 0.95f),   // violet
    };

    private int nextNumber = 1;

    private void Awake()
    {
        Instance = this;
    }

    // Hand out the next identity: a number and its colour.
    public void Next(out int number, out Color color)
    {
        number = nextNumber;
        color = palette[(nextNumber - 1) % palette.Length];
        nextNumber++;
        if (nextNumber > 9) nextNumber = 1;   // single digits read best
    }
}