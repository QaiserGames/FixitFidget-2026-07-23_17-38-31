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

    public bool InConversation => partner != null;

    private CustomerBrain partner;
    private float inputReadyAt;
    private bool closing;
    private float closeAt;

    public void Begin(CustomerBrain brain)
    {
        if (brain == null || InConversation) return;

        partner = brain;
        closing = false;
        inputReadyAt = Time.time + inputDelay;

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
    }

    public void End()
    {
        partner = null;
        closing = false;

        if (conversationCam != null) conversationCam.Priority = 0;
        ui.Hide();
    }

    private void Update()
    {
        if (!InConversation) return;

        // They stormed out or were destroyed under us.
        if (partner == null) { End(); return; }

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

            if (partner.CanAcceptJob) { CloseWith(partner.AcceptJob(), 1.2f); return; }
            if (partner.JobReady)     { CloseWith(partner.CompleteJob(), 1.2f); return; }
        }

        // Q: turn them away.
        if (kb.qKey.wasPressedThisFrame && ui.LineFinished && partner.CanRefuse)
        {
            CloseWith(partner.RefuseJob(), 1.2f);
            return;
        }

        // F or Esc: walk away without deciding. They keep waiting.
        if (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            End();
    }

    private void CloseWith(string line, float hold)
    {
        ui.SetLine(line);
        ui.SetOptions("");
        closing = true;
        closeAt = Time.time + hold;
    }

    private string BuildOptions()
    {
        if (partner.OutOfStock)  return "We're out of stock          [Q]  Apologise";
        if (partner.CanAcceptJob) return "[E]  Take the job          [Q]  Turn them away";
        if (partner.JobReady)     return "[E]  Hand it back";
        if (partner.JobFixedButAway) return "Their item isn't here          [F]  Step away";
        return "[F]  Step away";
    }
}