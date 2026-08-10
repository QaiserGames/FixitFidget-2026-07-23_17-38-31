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
    private float alpha;

    private void Awake()
    {
        if (label != null) SetAlpha(0f);
    }

    private void LateUpdate()
    {
        if (label == null) return;
        if (cam == null) cam = Camera.main;
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        if (cam == null) return;

        // Only ever visible from a station view — never in isometric.
        bool couldShow = player != null && player.IsAtStation;
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
        c.a = a * 0.75f;      // never fully opaque — subtle, like Skyrim
        label.color = c;
    }
}