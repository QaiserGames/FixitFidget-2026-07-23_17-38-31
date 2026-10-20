using UnityEngine;
using TMPro;

public class Nameplate : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float fadeSpeed = 6f;

    [Tooltip("How close to the centre of view before the name shows. 1 = dead centre.")]
    [SerializeField] private float viewThreshold = 0.9f;

    private Camera cam;
    private PlayerInteractor player;
    private CafeViewMode view;
    private float alpha;

    // 19 Oct 2026 (her: "your text needs to be bolder"): the name in the skin's font with its shadow, semibold, and no longer
    // held at three-quarter alpha (the size is the prefab's, world space: at least 22 px on screen is the prefab's job).
    private const float ShownAlpha = .95f;

    private void Awake()
    {
        if (label == null) return;
        UiSkin.UseFont(label);
        label.fontStyle = UiSkin.SemiBold;
        UiSkin.Shadowed(label);
        SetAlpha(0f);
    }

    private void LateUpdate()
    {
        if (label == null) return;
        if (cam == null) cam = Camera.main;
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        if (cam == null) return;

        // Only ever from up close, never from above: across the counter in first person (where the counter's own
        // camera used to show them, before stations became reach in playtest 3), or in the drinks close-up.
        if (player != null && view == null) view = player.GetComponent<CafeViewMode>();
        bool couldShow = player != null && (player.IsAtStation
            || view != null && view.WalkingFirstPerson && player.BehindCounter);
        float target = 0f;

        if (couldShow)
        {
            Vector3 toMe = (transform.position - cam.transform.position).normalized;
            float centredness = Vector3.Dot(cam.transform.forward, toMe);

            // Fade in as they approach the centre of the screen.
            if (centredness > viewThreshold)
                target = Mathf.InverseLerp(viewThreshold, 1f, centredness);
        }

        alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime);
        SetAlpha(alpha);

        // Always face the camera.
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    public void SetName(string n)
    {
        if (label != null) label.text = n;
    }

    private void SetAlpha(float a)
    {
        Color c = label.color;
        c.a = a * ShownAlpha;
        label.color = c;
    }
}