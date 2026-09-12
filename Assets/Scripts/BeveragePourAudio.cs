using UnityEngine;

// Reusable authored audio clips, with independent valves and a short fade at the
// start/end of a pour. The default clips are synthesized splashes, not a motor tone.
[DisallowMultipleComponent]
public sealed class BeveragePourAudio : MonoBehaviour
{
    public AudioClip pourClip;
    public AudioClip completionClip;
    [Range(0, 1)] public float pourVolume = .18f;
    [Range(0, 1)] public float completionVolume = .15f;
    private AudioSource flowSource, completionSource;
    private bool active, running, flowPaused, completionPaused;

    private void Awake()
    {
        flowSource = MakeSource();
        flowSource.loop = true;
        completionSource = MakeSource();
    }

    private AudioSource MakeSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = .85f;
        source.minDistance = .65f;
        source.maxDistance = 4.5f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0;
        source.volume = 0;
        return source;
    }

    public void Begin()
    {
        if (flowSource == null || active) return;
        active = true;
        running = true;
        flowPaused = false;
        if (pourClip == null) return;
        flowSource.Stop();
        flowSource.clip = pourClip;
        flowSource.volume = 0;
        // Every valve begins at a different seamless point, without consuming the
        // game's random-number stream or stacking six phase-identical loops.
        int seed = GetEntityId().GetHashCode() & int.MaxValue;
        flowSource.timeSamples = pourClip.samples > 0 ? seed % pourClip.samples : 0;
        flowSource.Play();
    }

    public void SetRunning(bool value)
    {
        if (!active) return;
        running = value;
        if (!value && flowSource != null && flowSource.isPlaying)
        {
            flowSource.Pause();
            flowPaused = true;
        }
    }

    public void Finish(bool cupReady)
    {
        active = false;
        running = true;
        if (completionSource == null || !cupReady || completionClip == null || Time.timeScale <= 0
            || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        completionSource.volume = completionVolume;
        completionSource.PlayOneShot(completionClip);
    }

    private void Update()
    {
        if (flowSource == null) return;
        bool suspended = Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.DayOver;
        if (suspended)
        {
            if (flowSource.isPlaying) { flowSource.Pause(); flowPaused = true; }
            if (completionSource.isPlaying) { completionSource.Pause(); completionPaused = true; }
            return;
        }
        if (completionPaused) { completionSource.UnPause(); completionPaused = false; }
        if (active && !running) return;
        if (flowPaused) { flowSource.UnPause(); flowPaused = false; }
        float target = active ? pourVolume : 0;
        flowSource.volume = Mathf.MoveTowards(flowSource.volume, target,
            Mathf.Max(.001f, pourVolume) * Time.deltaTime / (active ? .06f : .09f));
        if (!active && flowSource.volume <= 0) flowSource.Stop();
    }

    private void OnDisable()
    {
        active = running = flowPaused = completionPaused = false;
        if (flowSource != null) flowSource.Stop();
        if (completionSource != null) completionSource.Stop();
    }
}
