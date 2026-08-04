using System.Collections;
using UnityEngine;

public class RemovablePart : BenchInteractable
{
    [SerializeField] private Screw[] heldBy;
    [SerializeField] private int traySlot = 0;
    [SerializeField] private float moveDuration = 0.35f;

    [Tooltip("Faults beneath this part. It refuses to close over unfinished work.")]
    [SerializeField] private GameObject[] faultsBeneath;

    [Tooltip("Parts beneath that must be replaced before this can close.")]
    [SerializeField] private ReplaceablePart[] replacementsBeneath;

    public bool IsRemoved { get; private set; }
    private bool busy;

    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform homeParent;
    private BenchRig rig;

    protected override void Awake()
    {
        base.Awake();
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;

        // Tell each screw which plate it belongs to.
        foreach (Screw s in heldBy)
        {
            if (s == null) continue;
            ScrewTarget t = s.GetComponent<ScrewTarget>();
            if (t != null) t.SetPlate(this);
        }
    }

    private void Start()
    {
        rig = FindAnyObjectByType<BenchRig>();
    }

    private bool AllScrewsOut
    {
        get
        {
            foreach (Screw s in heldBy)
                if (s != null && !s.IsOut) return false;
            return true;
        }
    }

    // Everything beneath is dealt with (grime destroys itself when clean).
    private bool WorkBeneathDone
    {
        get
        {
            foreach (GameObject f in faultsBeneath)
                if (f != null) return false;

            foreach (ReplaceablePart r in replacementsBeneath)
                if (r != null && !r.IsReplaced) return false;

            return true;
        }
    }

    public override bool CanInteract
    {
        get
        {
            if (busy) return false;
            if (!IsRemoved) return AllScrewsOut;      // lift off
            return WorkBeneathDone;                    // put back
        }
    }

    public override ToolType RequiredTool => ToolType.Pry;

    public override void Activate()
    {
        if (busy) return;
        StartCoroutine(IsRemoved ? Reattach() : LiftOff());
    }

    private IEnumerator LiftOff()
    {
        busy = true;
        Transform slot = rig != null ? rig.TraySlot(traySlot) : null;

        // Detach so rotating the item doesn't drag us out of the tray,
        // but register with the job so cleanup still finds us.
        JobBase job = GetComponentInParent<JobBase>();
        transform.SetParent(null);
        if (job != null) job.RegisterDetached(gameObject);

        Vector3 from = transform.position;
        Vector3 to = slot != null ? slot.position : from + Vector3.up * 0.1f;
        Quaternion rotFrom = transform.rotation;
        Quaternion rotTo = slot != null ? slot.rotation : rotFrom;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float c = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, to, c);
            p.y += Mathf.Sin(c * Mathf.PI) * 0.05f;
            transform.position = p;
            transform.rotation = Quaternion.Slerp(rotFrom, rotTo, c);
            yield return null;
        }

        IsRemoved = true;
        busy = false;
    }

    private IEnumerator Reattach()
    {
        busy = true;

        // Rejoin the item.
        transform.SetParent(homeParent);
        JobBase ownerJob = homeParent.GetComponentInParent<JobBase>();
        if (ownerJob != null) ownerJob.UnregisterDetached(gameObject);

        Vector3 from = transform.position;
        Quaternion rotFrom = transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float c = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(from, homeParent.TransformPoint(homeLocalPos), c);
            transform.rotation = Quaternion.Slerp(rotFrom, homeParent.rotation * homeLocalRot, c);
            yield return null;
        }

        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        IsRemoved = false;
        busy = false;

        // Screws are NOT auto-returned — the player rescrews each one by hand.
    }
}