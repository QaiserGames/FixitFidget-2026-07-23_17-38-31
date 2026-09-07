using UnityEngine;

// Sound and a wordless cue remain spatial; the customer ticket owns the timer.
public sealed class HoldCallPresentation : MonoBehaviour
{
    private HoldCallJob job;
    public HoldCallJob Job => job;
    private AudioSource speaker;
    private AudioClip music, ring;
    private HoldCallRun.State previous = (HoldCallRun.State)(-1);
    private bool audioPaused;

    private void Start()
    {
        job = GetComponent<HoldCallJob>();
        if (GetComponent<SupportCallWorldCue>() == null) gameObject.AddComponent<SupportCallWorldCue>();
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false; speaker.loop = true; speaker.spatialBlend = 1f;
        speaker.minDistance = 1f; speaker.maxDistance = 16f; speaker.rolloffMode = AudioRolloffMode.Linear;
        music = RepairAudio.MakeTone("Support hold music", true);
        ring = RepairAudio.MakeTone("Support answer ring", false);
    }
    private void OnEnable()
    {
        previous = (HoldCallRun.State)(-1);
    }
    private void OnDisable() { if (speaker != null) speaker.Stop(); }
    private void LateUpdate()
    {
        if (job == null) return;
        bool live = job.CanOperate;
        if (!live)
        {
            if (speaker != null && !audioPaused) { speaker.Pause(); audioPaused = true; }
            return;
        }
        if (audioPaused) { speaker.UnPause(); audioPaused = false; }
        if (previous != job.CurrentPhase)
        {
            previous = job.CurrentPhase;
            speaker.Stop();
            speaker.clip = previous == HoldCallRun.State.OnHold ? music : previous == HoldCallRun.State.Ringing ? ring : null;
            speaker.volume = previous == HoldCallRun.State.OnHold ? .10f : .65f;
            if (speaker.clip != null) speaker.Play();
        }
    }
    private void OnDestroy()
    {
        if (music != null) Destroy(music);
        if (ring != null) Destroy(ring);
    }
}
