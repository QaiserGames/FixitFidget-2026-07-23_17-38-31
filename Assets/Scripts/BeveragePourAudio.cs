using UnityEngine;

// Small, local feedback with no external audio dependency. Each valve can pour
// independently, and pausing a pour never retriggers its sound from the start.
[DisallowMultipleComponent]
public sealed class BeveragePourAudio : MonoBehaviour
{
    private AudioSource source;
    private AudioClip flow, ready;
    private bool active;
    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = .7f;
        source.minDistance = 1; source.maxDistance = 5; source.volume = .11f;
        source.rolloffMode = AudioRolloffMode.Linear;
        const int sampleRate = 22050;
        var samples = new float[sampleRate];
        uint seed = 7817; float filtered = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            seed = seed * 1664525 + 1013904223;
            float noise = (seed & 65535) / 32768f - 1;
            filtered = Mathf.Lerp(filtered, noise, .18f);
            float phase = i / (float)sampleRate;
            samples[i] = filtered * .85f + Mathf.Sin(phase * Mathf.PI * 2 * 140) * .035f;
        }
        flow = AudioClip.Create("Soft dispenser flow", samples.Length, 1, sampleRate, false); flow.SetData(samples, 0);
        samples = new float[(int)(sampleRate * .18f)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 720) * Mathf.Exp(-t * 30) * Mathf.Min(1, t * 500) * .45f;
        }
        ready = AudioClip.Create("Cup ready", samples.Length, 1, sampleRate, false); ready.SetData(samples, 0);
    }
    public void Begin() { active = true; source.clip = flow; source.loop = true; source.Play(); }
    public void SetRunning(bool running)
    {
        if (!active) return;
        if (!running && source.isPlaying) source.Pause();
        else if (running && !source.isPlaying) source.UnPause();
    }
    public void Finish(bool cupReady)
    {
        active = false;
        if (source == null) return;
        source.Stop(); source.loop = false;
        if (cupReady && Time.timeScale > 0) source.PlayOneShot(ready, 1.2f);
    }
    private void OnDestroy() { if (flow != null) Destroy(flow); if (ready != null) Destroy(ready); }
}
