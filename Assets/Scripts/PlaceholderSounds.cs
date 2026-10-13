using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// PLACEHOLDER SOUNDS, MADE IN CODE (the bench, v2; claude/bench-spec-v2.md §2.6; Mansoor, 7 Oct: "exactly placeholders for now")
//
// The Sonniss files haven't been chosen, and a silent bench can't be judged: every beat of the work (a screw's tick, its
// click when it comes free, the tink in the tray, the cover's creak and pop, the tweezers' pinch, the seat's snap...) gets
// a small synthesized sound here, played by SoundPlayer whenever a cue has no file. Each is a few milliseconds to a few
// hundred: enveloped noise and a sine or two, metallic where metal meets metal, soft where a hand lifts something. When
// the real files go into the bank, cue by cue, these stop being heard without any code changing.
// ---------------------------------------------------------------------------
public static class PlaceholderSounds
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> made = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    static readonly Dictionary<string, SoundBank.Cue> shapes = new Dictionary<string, SoundBank.Cue>(StringComparer.Ordinal);
    static readonly System.Random rng = new System.Random(7);

    /// <summary>The placeholder for a cue, or null when the cue has none (then it stays silent, as before).</summary>
    public static AudioClip For(string cue)
    {
        if (string.IsNullOrEmpty(cue)) return null;
        if (made.TryGetValue(cue, out AudioClip ready)) return ready;
        float[] data = Synthesize(cue);
        if (data == null) { made[cue] = null; return null; }
        var clip = AudioClip.Create("placeholder " + cue, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        made[cue] = clip;
        return clip;
    }

    /// <summary>How a cue the bank doesn't know is shaped: a small, near, world sound.</summary>
    public static SoundBank.Cue CueFor(string cue)
    {
        if (shapes.TryGetValue(cue, out SoundBank.Cue shape)) return shape;
        shape = new SoundBank.Cue { name = cue, volume = .4f, threeD = true, near = .8f, far = 6f, minGap = .03f, maxAtOnce = 4, pitchWobble = .05f, bus = SoundBus.World };
        shapes[cue] = shape;
        return shape;
    }

    public static bool Has(string cue) => Synthesize(cue) != null;

    // ---------- the recipes ----------

    static float[] Synthesize(string cue)
    {
        switch (cue)
        {
            // the screw
            case "screw.turn":   return Mix(.012f, Noise(.0028f, .55f), Sine(2600f, .006f, .5f));
            case "screw.free":   return Mix(.035f, Noise(.004f, .8f), Sine(1800f, .012f, .6f), Sine(3400f, .006f, .3f));
            case "screw.drop":   return Mix(.24f, Noise(.002f, .5f), Sine(4200f, .06f, .55f), Sine(6900f, .035f, .35f), Sine(9100f, .02f, .15f));
            case "screw.bounce": return Mix(.06f, Noise(.0025f, .35f), Sine(3000f, .015f, .35f));
            case "screw.fetch":  return Sweep(.12f, 600f, 1400f, .3f, .05f);
            case "screw.seat":   return Mix(.05f, Noise(.005f, .7f), Sine(900f, .02f, .55f), Sine(2400f, .008f, .25f));
            // the cover
            case "cover.creak":  return Creak(.3f);
            case "cover.pop":    return Mix(.07f, Noise(.008f, .9f), Sine(500f, .015f, .7f), Sine(1300f, .01f, .3f));
            case "cover.lift":   return Swish(.08f, .25f);
            case "cover.clack":  return Mix(.09f, Noise(.006f, .8f), Sine(700f, .025f, .6f), Sine(1100f, .018f, .4f));
            case "part.on":      return Mix(.06f, Noise(.005f, .7f), Sine(800f, .02f, .6f), Sine(2000f, .01f, .3f));
            case "part.off":     return Swish(.1f, .3f);
            // the parts
            case "part.pinch":   return Mix(.04f, Noise(.0015f, .4f), Sine(2200f, .01f, .5f));
            case "part.lift":    return Swish(.09f, .2f);
            case "part.drop":    return Mix(.08f, Noise(.004f, .6f), Sine(1500f, .02f, .5f));
            case "part.replace": return Mix(.15f, Noise(.004f, .6f), Sine(1600f, .015f, .5f), Delay(.07f, Mix(.06f, Noise(.004f, .7f), Sine(900f, .02f, .7f))));
            // the tools, the device
            case "tool.pick":    return Mix(.05f, Noise(.003f, .5f), Sine(1000f, .018f, .5f));
            case "tool.down":    return Mix(.06f, Noise(.004f, .5f), Sine(1200f, .025f, .45f));
            case "device.snap":  return Mix(.06f, Noise(.003f, .4f), Sine(600f, .025f, .6f));
            case "device.flip":  return Sweep(.15f, 900f, 400f, .22f, .06f, noisy: true);
            // cleaning (4b: the brush's hiss where dust comes off, the cloth's duller rub and its squeak, the chimes)
            case "grime.scrub":  return Noise(.07f, .45f, soft: true);
            case "grime.clean":  return Mix(.4f, Sine(1320f, .12f, .45f), Delay(.09f, Sine(1760f, .16f, .45f)));
            case "cloth.wipe":   return Noise(.1f, .28f, soft: true);
            case "cloth.squeak": return Sweep(.12f, 2400f, 3200f, .18f, .03f);
            case "cloth.gleam":  return Mix(.35f, Sine(2093f, .12f, .35f), Delay(.06f, Sine(2637f, .14f, .35f)), Delay(.12f, Sine(3136f, .16f, .3f)));
            // the cloth lifted off the glass as a smudge comes clean (playtest 4): a soft cloth swish, longer and lower than a part's lift
            case "cloth.lift":   return Swish(.16f, .16f);
            // the repair done (4b): "Fixed!", "back together", the sparkles
            case "repair.fixed": return Mix(.65f, Sine(880f, .10f, .38f), Delay(.10f, Sine(1109f, .12f, .38f)), Delay(.20f, Sine(1319f, .22f, .42f)), Delay(.20f, Sine(1760f, .18f, .18f)));
            case "repair.ready": return Mix(.35f, Sine(1047f, .08f, .32f), Delay(.09f, Sine(1319f, .12f, .32f)));
            case "juice.sparkle":     return Mix(.3f, Sine(3520f, .06f, .16f), Delay(.04f, Sine(4186f, .06f, .14f)), Delay(.09f, Sine(5274f, .08f, .12f)));
            case "juice.sparkle.big": return Mix(.5f, Sine(2637f, .08f, .2f), Delay(.05f, Sine(3520f, .08f, .18f)), Delay(.1f, Sine(4186f, .1f, .16f)), Delay(.16f, Sine(5274f, .14f, .14f)));
            // the device wakes (4b, DeviceWake): the phone boots, the watch is wound and ticks, the camera is wound and its shutter fires
            case "phone.boot":   return Mix(.75f, Sweep(.25f, 300f, 900f, .16f, .05f), Delay(.22f, Sine(659f, .18f, .22f)), Delay(.22f, Sine(988f, .16f, .18f)), Delay(.30f, Sine(1319f, .2f, .2f)));
            case "watch.wind":   return Ratchet(.9f, 12, 2600f, .35f);
            case "watch.tick":   return Ticks(2.4f, 12, .5f);
            case "camera.wind":  return Ratchet(.45f, 7, 1700f, .4f);
            case "camera.shutter": return Mix(.1f, Noise(.004f, .9f), Sine(700f, .012f, .6f), Delay(.035f, Mix(.05f, Noise(.003f, .8f), Sine(1100f, .01f, .5f))));
            // the day intro (12 Oct 2026, Cinematics; Mansoor: "coffee grinding and screws unwinding ... blended together"): named for
            // the real sounds to come (Audio's cue sheet is in SoundBank.Defaults), so the cuts are right before the sounds are.
            // The screw's own beats in the intro are the bench's own cues (screw.turn, screw.free, screw.drop); the pour is the
            // dispenser's own file when it has one, else intro.pour; the phone's screen is phone.boot.
            case "intro.grind":  return Motor(1.3f, 96f, .30f);                                            // the grinder running: a motor's buzz under a hiss, sagging as it stops
            case "intro.pour":   return Pour(1.0f, .22f);                                                  // a drink pouring: a soft hiss with a low gurgle
            case "intro.sign":   return Mix(.16f, Noise(.005f, .8f), Sine(620f, .03f, .6f), Sine(980f, .02f, .35f),
                                            Delay(.055f, Mix(.07f, Noise(.004f, .6f), Sine(560f, .022f, .5f))));   // the OPEN sign's wooden flap, two knocks
            case "intro.lamp":   return Mix(.08f, Noise(.0018f, .5f), Sine(1900f, .008f, .5f), Delay(.03f, Sine(2600f, .006f, .3f)));   // the magnifier lamp's switch and LED tick
            case "intro.sting":  return Mix(1.6f, Sine(220f, .5f, .16f), Sine(440f, .45f, .22f), Delay(.13f, Sine(659.3f, .45f, .2f)),
                                            Delay(.26f, Sine(880f, .5f, .2f)), Delay(.39f, Sine(1108.7f, .6f, .18f)), Delay(.39f, Sine(1318.5f, .7f, .1f)));   // the morning's sting: A, E, A, C sharp (the signature family's chord)
            default: return null;
        }
    }

    // ---------- the building blocks (each returns samples; Mix sums and trims them to a length) ----------

    static float[] Sine(float hz, float tau, float amp)
    {
        int n = Mathf.CeilToInt(tau * 6f * Rate);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            s[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t / tau) * amp;
        }
        return s;
    }

    static float[] Noise(float tau, float amp, bool soft = false)
    {
        int n = Mathf.CeilToInt(tau * (soft ? 1.2f : 6f) * Rate);
        var s = new float[n];
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            // soft: a brush's hiss (low-passed noise under a hump); else a sharp burst
            float v = soft ? (last = last + (white - last) * .25f) : white;
            float env = soft ? Mathf.Sin(Mathf.Clamp01(t / tau) * Mathf.PI) : Mathf.Exp(-t / tau);
            s[i] = v * env * amp;
        }
        return s;
    }

    static float[] Sweep(float seconds, float from, float to, float amp, float attack, bool noisy = false)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = t / seconds;
            float hz = Mathf.Lerp(from, to, u);
            phase += 2f * Mathf.PI * hz / Rate;
            float env = Mathf.Min(1f, t / attack) * (1f - u) * (1f - u);
            float v = Mathf.Sin(phase);
            if (noisy) v = v * .4f + (float)(rng.NextDouble() * 2.0 - 1.0) * .6f;
            s[i] = v * env * amp;
        }
        return s;
    }

    // A creak: a low tone whose pitch climbs and whose volume judders.
    static float[] Creak(float seconds)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = t / seconds;
            float hz = Mathf.Lerp(170f, 280f, u * u);
            phase += 2f * Mathf.PI * hz / Rate;
            // a buzzy wave: the first five harmonics, falling off
            float v = 0f;
            for (int h = 1; h <= 5; h++) v += Mathf.Sin(phase * h) / h;
            float judder = .55f + .45f * Mathf.Sin(2f * Mathf.PI * 23f * t + Mathf.Sin(t * 50f));
            float env = Mathf.Min(1f, t / .03f) * (1f - u);
            s[i] = v * judder * env * .22f;
        }
        return s;
    }

    static float[] Swish(float seconds, float amp)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            last += (white - last) * .18f;
            s[i] = last * Mathf.Sin(u * Mathf.PI) * amp;
        }
        return s;
    }

    // A ratchet: `count` short metallic clicks spread over `seconds`, each a touch different in pitch.
    static float[] Ratchet(float seconds, int count, float hz, float amp)
    {
        var parts = new List<float[]>();
        for (int i = 0; i < count; i++)
        {
            float at = seconds * (i + .5f) / count;
            float pitch = hz * (1f + ((i * 7) % 5 - 2) * .02f);
            parts.Add(Delay(at, Mix(.025f, Noise(.0015f, .5f * amp), Sine(pitch, .004f, .9f * amp), Sine(pitch * 2.3f, .002f, .3f * amp))));
        }
        return Mix(seconds + .05f, parts.ToArray());
    }

    // A watch's ticking: tick, tock, at five a second, fading away over `seconds`.
    static float[] Ticks(float seconds, int count, float amp)
    {
        var parts = new List<float[]>();
        for (int i = 0; i < count; i++)
        {
            float at = seconds * i / count;
            float fade = 1f - .7f * i / (float)count;
            float pitch = i % 2 == 0 ? 4200f : 3500f;
            parts.Add(Delay(at, Mix(.02f, Noise(.0008f, .3f * amp * fade), Sine(pitch, .0025f, .9f * amp * fade))));
        }
        return Mix(seconds, parts.ToArray());
    }

    // A small motor running (the day intro's grinder): a buzzy wave at `hz` with its first five harmonics and a slow wobble,
    // a hiss of filtered noise under it; it starts in 0.08 s, runs, and sags in pitch and level over its last 0.3 s.
    static float[] Motor(float seconds, float hz, float amp)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        float phase = 0f, last = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float stop = Mathf.Clamp01((t - (seconds - .3f)) / .3f);          // the last 0.3 s: winding down
            float pitch = hz * (1f + .012f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * (1f - .35f * stop);
            phase += 2f * Mathf.PI * pitch / Rate;
            float v = 0f;
            for (int h = 1; h <= 5; h++) v += Mathf.Sin(phase * h) / (h * 1.3f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            last += (white - last) * .12f;                                    // the hiss: low-passed noise
            float env = Mathf.Min(1f, t / .08f) * (1f - .85f * stop * stop);
            s[i] = (v * .55f + last * 1.1f) * env * amp;
        }
        return s;
    }

    // A drink pouring (the day intro's fallback when the dispenser has no file): a soft hiss swelling in, with a low gurgle
    // whose pitch wanders, fading out.
    static float[] Pour(float seconds, float amp)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        float phase = 0f, last = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = t / seconds;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            last += (white - last) * .2f;
            float hz = 160f + 40f * Mathf.Sin(2f * Mathf.PI * 1.7f * t) + 25f * Mathf.Sin(2f * Mathf.PI * 4.3f * t + 1f);
            phase += 2f * Mathf.PI * hz / Rate;
            float gurgle = Mathf.Sin(phase) * (.4f + .6f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 3f * t)));
            float env = Mathf.Min(1f, t / .12f) * Mathf.Clamp01((1f - u) / .18f);
            s[i] = (last * .9f + gurgle * .25f) * env * amp;
        }
        return s;
    }

    static float[] Delay(float seconds, float[] inner)
    {
        int d = Mathf.CeilToInt(seconds * Rate);
        var s = new float[d + inner.Length];
        Array.Copy(inner, 0, s, d, inner.Length);
        return s;
    }

    static float[] Mix(float seconds, params float[][] parts)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var s = new float[n];
        foreach (float[] part in parts)
            for (int i = 0; i < n && i < part.Length; i++) s[i] += part[i];
        // a short fade at the end so nothing clicks, and a limiter
        int fade = Mathf.Min(n, Rate / 200);
        for (int i = 0; i < fade; i++) s[n - 1 - i] *= i / (float)fade;
        float peak = 0f;
        foreach (float v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > .9f) for (int i = 0; i < n; i++) s[i] *= .9f / peak;
        return s;
    }
}
