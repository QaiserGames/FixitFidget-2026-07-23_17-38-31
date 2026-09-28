using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: THINGS THAT ARE A LITTLE BROKEN (claude/night-city-proposal.md §9)
//
// A lived-in street has a few things nobody has fixed yet. Each of these is one light (or glowing
// sign) that misbehaves while the night runs, in its own way:
//
//   * Buzz: a failing bulb (the old lamp in front of the sky blue house). Mostly on with a faint
//     flutter; every few seconds it sputters, and now and then it drops out for a moment. While it
//     is out, a dark cap covers its glowing glass (NightRooms.Cap), so the lamp really looks off;
//   * Stutter: a dying street lamp. On for a while, a burst of flicker, off for a few seconds, then
//     a weak sputtering restart, and on again. Its bulb disappears while it is off;
//   * CutOut: a sign whose tube is going. It blinks, goes out for a second or two, and comes back.
//
// NightWalk sets these up when the night begins and puts everything back when it ends (Restore).
// Time.time drives them, so a paused game holds them still.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightFlicker : MonoBehaviour
{
    public enum Kind { Buzz, Stutter, CutOut }

    public Kind kind;
    [Tooltip("What it is, for the night's notes.")]
    public string what = "";

    readonly List<(Light light, float intensity)> lights = new();
    readonly List<Renderer> whenOn = new(), whenOff = new();
    readonly List<(Material material, int property, Color colour)> glows = new();

    enum Phase { On, Sputter, Out, Restart }
    Phase phase = Phase.On;
    float phaseEnds, nextToggle, level = 1f;
    bool flick = true;
    System.Random random;

    /// <summary>Times it has gone out for a while so far (for reports; the quick blinks aren't counted).</summary>
    public int Outages { get; private set; }
    public bool IsOut => level < .35f;
    public int Parts => lights.Count + whenOn.Count + whenOff.Count + glows.Count;

    public void AddLight(Light light)
    {
        if (light != null) lights.Add((light, light.intensity));
    }

    /// <summary>Shown only while the light is on (a bulb).</summary>
    public void AddWhenOn(Renderer r)
    {
        if (r != null) whenOn.Add(r);
    }

    /// <summary>Shown only while the light is out (a dark cap over its glass).</summary>
    public void AddWhenOff(Renderer r)
    {
        if (r != null) { whenOff.Add(r); r.enabled = false; }
    }

    /// <summary>A glow colour (a sign's own material copy) that fades with the light.</summary>
    public void AddGlow(Material material, int property)
    {
        if (material != null && material.HasProperty(property)) glows.Add((material, property, material.GetColor(property)));
    }

    public void Begin(int seed)
    {
        random = new System.Random(seed);
        phase = Phase.On;
        phaseEnds = Time.time + Range(1.5f, 5f);
        level = 1f;
        Apply(1f);
    }

    /// <summary>Everything as it was: full brightness, the bulb shown, the cap hidden.</summary>
    public void Restore()
    {
        foreach (var (light, intensity) in lights) if (light != null) light.intensity = intensity;
        foreach (Renderer r in whenOn) if (r != null) r.enabled = true;
        foreach (Renderer r in whenOff) if (r != null) r.enabled = false;
        foreach (var (material, property, colour) in glows) if (material != null) material.SetColor(property, colour);
        level = 1f;
    }

    void OnDestroy() => Restore();

    void Update()
    {
        if (random == null) return;
        float now = Time.time;
        if (now >= phaseEnds) NextPhase(now);
        float target = kind switch
        {
            Kind.Buzz => Buzz(now),
            Kind.Stutter => Stutter(now),
            _ => CutOut(now),
        };
        level = target;
        Apply(level);
    }

    void NextPhase(float now)
    {
        switch (kind)
        {
            case Kind.Buzz:
                phase = phase == Phase.On ? Phase.Sputter
                      : phase == Phase.Sputter && random.NextDouble() < .35 ? Phase.Out
                      : Phase.On;
                phaseEnds = now + (phase == Phase.On ? Range(3f, 9f) : phase == Phase.Sputter ? Range(.25f, .9f) : Range(.4f, 1.4f));
                break;
            case Kind.Stutter:
                phase = phase switch { Phase.On => Phase.Sputter, Phase.Sputter => Phase.Out, Phase.Out => Phase.Restart, _ => Phase.On };
                phaseEnds = now + phase switch
                {
                    Phase.On => Range(7f, 16f), Phase.Sputter => Range(1.2f, 2.5f), Phase.Out => Range(2f, 5f), _ => Range(.6f, 1.2f),
                };
                break;
            default:
                phase = phase switch { Phase.On => Phase.Sputter, Phase.Sputter => Phase.Out, Phase.Out => Phase.Restart, _ => Phase.On };
                phaseEnds = now + phase switch
                {
                    Phase.On => Range(5f, 14f), Phase.Sputter => Range(.15f, .4f), Phase.Out => Range(.7f, 2.2f), _ => Range(.1f, .3f),
                };
                break;
        }
        nextToggle = now;
        if (phase == Phase.Out) Outages++;
    }

    // A failing bulb: a faint flutter, a sputter now and then, sometimes out for a moment.
    float Buzz(float now) => phase switch
    {
        Phase.On => .9f + .1f * Mathf.PerlinNoise(now * 9f, .37f),
        Phase.Sputter => Toggle(now, .03f, .09f, .15f),
        _ => 0f,
    };

    // A dying street lamp: on, a burst of flicker, off, a weak restart.
    float Stutter(float now) => phase switch
    {
        Phase.On => 1f,
        Phase.Sputter => Toggle(now, .05f, .16f, 0f),
        Phase.Out => 0f,
        _ => Toggle(now, .08f, .2f, 0f) * Range(.25f, .6f),
    };

    // A sign's tube: two quick blinks, out, a blink back on.
    float CutOut(float now) => phase switch
    {
        Phase.On => 1f,
        Phase.Sputter => Toggle(now, .04f, .1f, 0f),
        Phase.Out => 0f,
        _ => Toggle(now, .04f, .08f, .3f),
    };

    float Toggle(float now, float shortest, float longest, float low)
    {
        if (now >= nextToggle)
        {
            flick = !flick;
            nextToggle = now + Range(shortest, longest);
        }
        return flick ? 1f : low;
    }

    void Apply(float value)
    {
        foreach (var (light, intensity) in lights) if (light != null) light.intensity = intensity * value;
        bool on = value >= .35f;
        foreach (Renderer r in whenOn) if (r != null && r.enabled != on) r.enabled = on;
        foreach (Renderer r in whenOff) if (r != null && r.enabled == on) r.enabled = !on;
        foreach (var (material, property, colour) in glows) if (material != null) material.SetColor(property, colour * value);
    }

    float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

    public string Describe() => $"{what}: {kind}, {(IsOut ? "out" : "on")} now, out {Outages} times so far ({Parts} parts)";
}
