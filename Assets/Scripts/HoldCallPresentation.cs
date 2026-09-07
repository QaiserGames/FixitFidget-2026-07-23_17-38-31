using System.Collections.Generic;
using UnityEngine;

// Sound remains spatial; readable status belongs to the screen-space HUD.
public sealed class HoldCallPresentation : MonoBehaviour
{
    private static readonly List<HoldCallPresentation> active = new();
    public static IReadOnlyList<HoldCallPresentation> Live => active;
    private HoldCallJob job;
    public HoldCallJob Job => job;
    private AudioSource speaker;
    private AudioClip music, ring;
    private HoldCallRun.State previous = (HoldCallRun.State)(-1);
    private bool audioPaused;

    private void Start()
    {
        job = GetComponent<HoldCallJob>();
        SupportCallHUD.EnsureExists();
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false; speaker.loop = true; speaker.spatialBlend = 1f;
        speaker.minDistance = 1f; speaker.maxDistance = 16f; speaker.rolloffMode = AudioRolloffMode.Linear;
        music = RepairAudio.MakeTone("Support hold music", true);
        ring = RepairAudio.MakeTone("Support answer ring", false);
    }
    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        previous = (HoldCallRun.State)(-1);
    }
    private void OnDisable() { active.Remove(this); if (speaker != null) speaker.Stop(); }
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
        active.Remove(this);
        if (music != null) Destroy(music);
        if (ring != null) Destroy(ring);
    }
}
