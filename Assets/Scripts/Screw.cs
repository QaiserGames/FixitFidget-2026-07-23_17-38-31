using System.Collections;
using UnityEngine;

public class Screw : MonoBehaviour
{
    [SerializeField] private float turnDuration = 0.45f;
    [SerializeField] private float flyDuration = 0.25f;
    [SerializeField] private float liftHeight = 0.02f;

    public bool IsOut { get; private set; }
    public bool IsBusy { get; private set; }

    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform homeParent;

    private void Awake()
    {
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
    }

    // Upgrades make the driver faster.
    private float TurnTime
    {
        get
        {
            float mult = UpgradeManager.Instance != null
                ? UpgradeManager.Instance.ScrewTimeMultiplier : 1f;
            return turnDuration * mult;
        }
    }

    public void Unscrew(Transform bin)
    {
        if (IsOut || IsBusy) return;
        StartCoroutine(UnscrewRoutine(bin));
    }

    public void Rescrew()
    {
        if (!IsOut || IsBusy) return;
        StartCoroutine(RescrewRoutine());
    }

    private IEnumerator UnscrewRoutine(Transform bin)
    {
        IsBusy = true;

        float turn = TurnTime;

        // Spin three turns while rising, easing out as it loosens.
        Vector3 start = transform.localPosition;
        Vector3 lifted = start + Vector3.up * liftHeight;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / turn;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);

            transform.localPosition = Vector3.Lerp(start, lifted, eased);
            transform.Rotate(Vector3.up, 1080f * Time.deltaTime / turn, Space.Self);
            yield return null;
        }

        // The little hang before it goes — this beat sells it.
        yield return new WaitForSeconds(0.05f);

        // Leave the item so rotating it doesn't drag us along, but stay OWNED
        // by the job so cleanup still finds us.
        JobBase job = GetComponentInParent<JobBase>();
        transform.SetParent(null);
        if (job != null) job.RegisterDetached(gameObject);

        // Arc into the bin.
        Vector3 from = transform.position;
        Vector2 scatter = Random.insideUnitCircle * 0.012f;
        Vector3 to = bin.position + new Vector3(scatter.x, 0.004f, scatter.y);
        float apex = 0.06f;
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / flyDuration;
            float c = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, to, c);
            p.y += Mathf.Sin(c * Mathf.PI) * apex;
            transform.position = p;
            transform.Rotate(Vector3.up, 720f * Time.deltaTime / flyDuration, Space.Self);
            yield return null;
        }

        transform.position = to;
        IsOut = true;
        IsBusy = false;
        // TODO audio: tink, pitch varied ±10%
    }

    private IEnumerator RescrewRoutine()
    {
        IsBusy = true;

        float turn = TurnTime;

        // Rejoin the item.
        transform.SetParent(homeParent);
        JobBase job = homeParent.GetComponentInParent<JobBase>();
        if (job != null) job.UnregisterDetached(gameObject);

        // Fly back to just above the hole. Resolve home fresh each frame in
        // case the item has been rotated while we were out.
        Vector3 from = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / flyDuration;
            Vector3 hover = homeParent.TransformPoint(homeLocalPos + Vector3.up * liftHeight);
            transform.position = Vector3.Lerp(from, hover, Mathf.Clamp01(t));
            yield return null;
        }

        // Then screw down into the hole.
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / turn;
            transform.localPosition = Vector3.Lerp(
                homeLocalPos + Vector3.up * liftHeight, homeLocalPos, Mathf.Clamp01(t));
            transform.Rotate(Vector3.up, -1080f * Time.deltaTime / turn, Space.Self);
            yield return null;
        }

        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        IsOut = false;
        IsBusy = false;
    }
}