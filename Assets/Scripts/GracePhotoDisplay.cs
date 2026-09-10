using UnityEngine;

// A scene-mounted placeholder keepsake. Saved memory chooses visibility; the
// scene carries no separate collected flag that could drift from the checkpoint.
public class GracePhotoDisplay : MonoBehaviour
{
    [SerializeField] private GameObject photograph;
    [SerializeField] private GameObject smudge;
    [SerializeField] private TextMesh caption;
    private float nextRefresh;
    public string DisplayedVariant { get; private set; } = "";

    private void Start() => RefreshDisplay();
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        RefreshDisplay();
    }

    public static bool CanDisplay(RegularMemoryData memory) => memory != null
        && memory.profileId == GraceCameraEpisode.ProfileId && memory.gracePhotoClaimed
        && (memory.gracePhotoVariant == nameof(GracePhotoOutcome.Clear)
            || memory.gracePhotoVariant == nameof(GracePhotoOutcome.Imperfect));

    public void RefreshDisplay()
    {
        RegularMemoryData memory = SaveManager.Instance != null
            ? SaveManager.Instance.MemoryForId(GraceCameraEpisode.ProfileId) : null;
        bool visible = CanDisplay(memory);
        DisplayedVariant = visible ? memory.gracePhotoVariant : "";
        if (photograph != null) photograph.SetActive(visible);
        if (smudge != null) smudge.SetActive(visible && DisplayedVariant == nameof(GracePhotoOutcome.Imperfect));
        if (caption != null) caption.text = visible
            ? (DisplayedVariant == nameof(GracePhotoOutcome.Clear)
                ? "FINALLY IN THE FRAME\nGrace's family reunion"
                : "ARTISTIC, APPARENTLY\nGrace's family reunion")
            : "";
    }
}
