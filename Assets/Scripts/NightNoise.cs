using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// NOISE AT NIGHT (break-ins chunk B, 30 Sept 2026: claude/break-ins-spec.md §6)
//
// Every sound Ace makes that someone could hear goes through here, as a place, a radius and a kind:
// anyone listening (Grace at home, chunk C) hears it if she is within the radius, whatever the walls
// (the spec's starting rule; to tune in play). The radii are the spec's starting numbers:
//
//   | Sound                    | Heard within |
//   | walking                  | 4 m          |
//   | sneaking                 | 1 m          |
//   | the lock pick            | 4 m          |
//   | taking the cups (rustle) | 3 m          |
//   | the creaky stair         | 5 m          |
//
// Only at night (NightWalk active): by day nobody listens, and the café's noise isn't Ace's. Nothing
// here allocates: a noise is a struct, the listeners are one multicast delegate, and the last few are
// kept in a small ring for the checks and the debug overlay.
// ---------------------------------------------------------------------------
public enum NoiseKind { Step, SneakStep, LockPick, Rustle, CreakyStair, Door, Knock }

public readonly struct NightNoiseEvent
{
    public readonly Vector3 position;
    public readonly float radius;
    public readonly NoiseKind kind;
    public readonly float time;

    public NightNoiseEvent(Vector3 position, float radius, NoiseKind kind, float time)
    {
        this.position = position;
        this.radius = radius;
        this.kind = kind;
        this.time = time;
    }

    /// <summary>Whether a listener at <paramref name="ear"/> hears it (flat distance: floors apart still count, as the spec's first rule).</summary>
    public bool HeardAt(Vector3 ear)
    {
        float dx = ear.x - position.x, dz = ear.z - position.z, dy = ear.y - position.y;
        return dx * dx + dz * dz + dy * dy <= radius * radius;
    }
}

public static class NightNoise
{
    public const float WalkRadius = 4f, SneakRadius = 1f, LockPickRadius = 4f, RustleRadius = 3f, CreakyStairRadius = 5f;

    /// <summary>Called for every noise made at night. Listeners check the distance themselves (NightNoiseEvent.HeardAt).</summary>
    public static event Action<NightNoiseEvent> Made;

    const int Kept = 16;
    static readonly NightNoiseEvent[] recent = new NightNoiseEvent[Kept];
    static int next;

    /// <summary>Noises made since Play started (for checks).</summary>
    public static int Count { get; private set; }
    /// <summary>Of those, how many of each kind (indexed by NoiseKind).</summary>
    public static readonly int[] CountByKind = new int[Enum.GetValues(typeof(NoiseKind)).Length];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Made = null;
        Count = 0;
        next = 0;
        Array.Clear(recent, 0, recent.Length);
        Array.Clear(CountByKind, 0, CountByKind.Length);
    }

    /// <summary>The radius the spec gives each kind of noise (metres).</summary>
    public static float RadiusOf(NoiseKind kind) => kind switch
    {
        NoiseKind.Step => WalkRadius,
        NoiseKind.SneakStep => SneakRadius,
        NoiseKind.LockPick => LockPickRadius,
        NoiseKind.Rustle => RustleRadius,
        NoiseKind.CreakyStair => CreakyStairRadius,
        NoiseKind.Door => CreakyStairRadius,
        NoiseKind.Knock => CreakyStairRadius,
        _ => WalkRadius,
    };

    /// <summary>A noise at night, heard within its kind's radius. By day it does nothing.</summary>
    public static void Make(Vector3 at, NoiseKind kind) => Make(at, RadiusOf(kind), kind);

    /// <summary>A noise at night, heard within <paramref name="radius"/> metres. By day it does nothing.</summary>
    public static void Make(Vector3 at, float radius, NoiseKind kind)
    {
        NightWalk night = NightWalk.Instance;
        if (night == null || !night.Active || radius <= 0f) return;
        var noise = new NightNoiseEvent(at, radius, kind, Time.time);
        recent[next] = noise;
        next = (next + 1) % Kept;
        Count++;
        CountByKind[(int)kind]++;
        Made?.Invoke(noise);
    }

    /// <summary>The <paramref name="ago"/>th most recent noise (0 = the last one); false if there aren't that many.</summary>
    public static bool Recent(int ago, out NightNoiseEvent noise)
    {
        noise = default;
        if (ago < 0 || ago >= Kept || ago >= Count) return false;
        noise = recent[(next - 1 - ago + Kept * 2) % Kept];
        return true;
    }
}
