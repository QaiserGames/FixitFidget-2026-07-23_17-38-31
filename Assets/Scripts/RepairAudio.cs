using UnityEngine;

// Small original synthesized cues; no downloaded audio assets.
public static class RepairAudio
{
    public static AudioClip MakeTone(string name, bool hold)
    {
        const int rate = 22050;
        float duration = hold ? 4f : 1.5f;
        var samples = new float[Mathf.RoundToInt(rate * duration)];
        float[] notes = { 261.63f, 329.63f, 392f, 329.63f, 293.66f, 349.23f, 440f, 349.23f };
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float beat = hold ? t % .5f : t % .3f;
            float frequency = hold ? notes[Mathf.FloorToInt(t * 2) % notes.Length] : 880f;
            float envelope = Mathf.Clamp01(beat / .015f) * Mathf.Clamp01(((hold ? .4f : .16f) - beat) / .04f);
            if (!hold && t > .7f) envelope = 0f;
            samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2) * envelope * .3f;
        }
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }
}
