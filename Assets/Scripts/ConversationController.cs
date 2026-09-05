using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class ConversationController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera conversationCam;
    [SerializeField] private ConversationUI ui;
    [SerializeField] private PlayerInteractor interactor;

    [Tooltip("Ignore input briefly after opening, so the key that started it doesn't also answer it.")]
    [SerializeField] private float inputDelay = 0.2f;
    [Tooltip("Pause after a closing line has been read, before the panel closes. " +
             "The customer does not move until this elapses, so raising it holds " +
             "them at the counter longer.")]
    [SerializeField] private float closingPause = 1.2f;

    public bool InConversation => partner != null;

    private CustomerBrain partner;
    private float inputReadyAt;
    private bool closing;
    private float closeAt;

    public void Begin(CustomerBrain brain)
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return;
        if (brain == null || InConversation) return;

        partner = brain;
        closing = false;
        inputReadyAt = Time.time + inputDelay;

        // Take ownership of their body. Until End(), nothing moves them.
        brain.OnConversationOpened(this);

        // Frame them. Rotation Composer holds the shot, so look input does nothing.
        if (conversationCam != null)
        {
            conversationCam.Target.TrackingTarget = brain.LookTarget != null
                ? brain.LookTarget : brain.transform;
            conversationCam.Priority = 40;
        }

        Sprite face = brain.Identity != null ? brain.Identity.Portrait : null;
        Color tint = brain.Identity != null ? brain.Identity.ThemeColor : Color.white;
        ui.Show(brain.CustomerName, tint, face);

        // First beat: whatever they came here to say.
        ui.SetLine(brain.HearIntake());
        RefreshPortrait();
    }

    public void End()
    {
        // Hand the body back BEFORE dropping the reference. This is the single
        // moment a customer is allowed to release their counter slot, claim a
        // waiting spot, and start walking.
        //
        // Unity's overloaded == is deliberate here: it catches a partner who
        // has been Destroy()ed, which the null-conditional operator would not.
        CustomerBrain leaving = partner;
        partner = null;
        closing = false;

        if (conversationCam != null)
        {
            conversationCam.Priority = 0;
            conversationCam.Target.TrackingTarget = null;
        }

        if (ui != null) ui.Hide();

        if (leaving != null) leaving.OnConversationClosed();
    }

    private void Update()
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            if (partner != null || closing) End();
            return;
        }
        if (!InConversation) return;

        // They stormed out or were destroyed under us.
        if (partner == null) { End(); return; }
        RefreshPortrait();

        // A closing line is playing — hold until it's read, then release.
        if (closing)
        {
            if (ui.LineFinished && Time.time >= closeAt) End();
            return;
        }

        ui.SetOptions(ui.LineFinished ? BuildOptions() : "");

        if (Time.time < inputReadyAt) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // E: finish the line if it's still revealing, otherwise take the job.
        if (kb.eKey.wasPressedThisFrame)
        {
            if (!ui.LineFinished) { ui.SkipReveal(); return; }

            if (partner.CanAcceptJob) { CloseWith(partner.AcceptJob()); return; }
            if (partner.JobReady)     { CloseWith(partner.CompleteJob()); return; }
        }

        // Q: turn them away.
        if (kb.qKey.wasPressedThisFrame && ui.LineFinished && partner.CanRefuse)
        {
            CloseWith(partner.RefuseJob());
            return;
        }

        // F or Esc: walk away without deciding. They keep waiting.
        if (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            End();
    }

    // `closingPause` was declared and then never referenced — every call site
    // hardcoded 1.2f, so nudging the field in the Inspector did nothing at all.
    // It now actually controls the hold.
    private void CloseWith(string line)
    {
        ui.SetLine(line);
        RefreshPortrait();
        ui.SetOptions("");
        closing = true;

        // Scale with length so long closing lines aren't cut off.
        float readTime = string.IsNullOrEmpty(line) ? 0f : line.Length / 30f;
        closeAt = Time.time + readTime + closingPause;
    }

    private void RefreshPortrait()
    {
        if (ui == null || partner == null || partner.Identity == null) return;
        CustomerIdentity identity = partner.Identity;
        ui.SetPortrait(identity.PortraitAt(partner.PatienceFraction), identity.ExpressionAt(partner.PatienceFraction));
    }

    private string BuildOptions()
    {
        if (partner.OutOfStock)  return "We're out of stock          [Q]  Apologise";
        if (partner.ShelfFull)   return "No room on the shelf        [Q]  Turn them away";
        if (partner.CanAcceptJob) return "[E]  Take the job          [Q]  Turn them away";
        return "[F]  Step away";
    }
}
