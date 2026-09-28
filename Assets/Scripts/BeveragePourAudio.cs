using UnityEngine;

// Reusable authored audio clips, with independent valves and a short fade at the
// start/end of a pour. The default clips are synthesized splashes, not a motor tone.
// Sound plan (claude/sound-plan.md): once the sound bank has files for "drink.pour" and
// "drink.ready", those play instead (same fades), "drink.machine" hums under the pour,
// and both sources go through the mixer's World group.
[DisallowMultipleComponent]
public sealed class BeveragePourAudio : MonoBehaviour
{
    public AudioClip pourClip;
    public AudioClip completionClip;
    [Range(0, 1)] public float pourVolume = .18f;
    [Range(0, 1)] public float completionVolume = .15f;
    private AudioSource flowSource, completionSource;
    private bool active, running, flowPaused, completionPaused;
    private float flowVolume;
    private SfxLoop machine;

    private void Awake()
    {
        flowSource = MakeSource();
        flowSource.loop = true;
        completionSource = MakeSource();
        flowVolume = pourVolume;
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
        Sfx.Route(source, SoundBus.World);
        return source;
    }

    public void Begin()
    {
        if (flowSource == null || active) return;
        active = true;
        running = true;
        flowPaused = false;
        machine = Sfx.Loop("drink.machine", transform);
        flowVolume = pourVolume;
        AudioClip clip = Sfx.Choose("drink.pour", flowSource, pourClip, ref flowVolume);
        if (clip == null) return;
        flowSource.Stop();
        flowSource.clip = clip;
        flowSource.volume = 0;
        // Every valve begins at a different seamless point, without consuming the
        // game's random-number stream or stacking six phase-identical loops.
        int seed = GetEntityId().GetHashCode() & int.MaxValue;
        flowSource.timeSamples = clip.samples > 0 ? seed % clip.samples : 0;
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
        StopMachine();
        if (completionSource == null || !cupReady || Time.timeScale <= 0
            || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        float volume = completionVolume;
        AudioClip ready = Sfx.Choose("drink.ready", completionSource, completionClip, ref volume);
        if (ready == null) return;
        completionSource.volume = volume;
        completionSource.PlayOneShot(ready);
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
        float target = active ? flowVolume : 0;
        flowSource.volume = Mathf.MoveTowards(flowSource.volume, target,
            Mathf.Max(.001f, flowVolume) * Time.deltaTime / (active ? .06f : .09f));
        if (!active && flowSource.volume <= 0) flowSource.Stop();
    }

    public void Cancel()
    {
        active = running = flowPaused = completionPaused = false;
        StopMachine();
        if (flowSource != null) flowSource.Stop();
        if (completionSource != null) completionSource.Stop();
    }

    private void StopMachine()
    {
        if (machine != null) machine.Stop(.25f);
        machine = null;
    }

    private void OnDisable() => Cancel();
}
