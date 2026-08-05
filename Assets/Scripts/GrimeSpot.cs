using UnityEngine;

public class GrimeSpot : MonoBehaviour
{
    [SerializeField] private float scrubHealth = 100f;

    public string DisplayName => "Grime";

    private float maxHealth;
    private Vector3 startScale;

    private void Awake()
    {
        maxHealth = scrubHealth;
        startScale = transform.localScale;
    }

    public void Scrub(float amount)
    {
        scrubHealth -= amount;

        // Shrink toward 30% size as it gets cleaner — visible progress.
        float t = Mathf.Clamp01(scrubHealth / maxHealth);
        transform.localScale = startScale * Mathf.Lerp(0.3f, 1f, t);

        if (scrubHealth <= 0f)
        {
            Debug.Log("Sparkling clean!");   // placeholder for sparkle particles + chime
            Destroy(gameObject);
        }
    }
}