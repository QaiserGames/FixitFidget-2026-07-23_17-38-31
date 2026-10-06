using System;

// ---------------------------------------------------------------------------
// GRACE'S NIGHT AND HER MARK: THE RULES (break-ins chunk C, 6 Oct 2026; claude/chunk-c-grace-at-home-plan.md §2-§4 and
// §7, all as Mansoor took them; claude/break-ins-spec.md §6 for the numbers)
//
// Her night by the night's clock (23 is 11 PM, 24 midnight, 26.667 is 2:40 AM; the night runs 11 PM to 4 AM in four
// minutes, 48 s an hour):
//
//   a usual night: 11:00 her armchair and the TV, 11:35 the kettle, 11:45 back to her armchair, midnight up to bed (the
//                  TV and her lamp off, the doors shut behind her), 12:20 her lamp off (asleep), 2:40 a glass of water
//                  (down to the kitchen and back), about 3:05 asleep again;
//   Thursdays:     out till late (the house dark), home through her front door at 1:30, the kettle, bed at 1:50, asleep at
//                  2:10, and no water at 2:40.
//
// Each step names what she sets out to do at its hour (Act). GraceAtHome walks her through it while the night runs; a
// jump in the clock (a check's SetHour) puts her where that step leaves her at once.
//
// Her mark (the "?" over her head) goes from 0 to 1:
//   * seeing Ace fills it: about 1 s close up in a lit room, about 3 s at the edge of her view or in the dark; half as fast
//     while Ace sneaks, twice as fast with Ace's torch on, half as fast while she watches TV (the TV has her attention);
//   * a sound she hears takes it to a third at once ("Hm?"); another while she's listening adds a sixth, but sounds alone
//     never take it past two thirds: to catch Ace she has to see Ace (chunk C's reading of the spec's "a sound adds a third":
//     footsteps come seven a second, and nobody is caught by a wall);
//   * when nothing has caught her eye or ear for a moment, it drains;
//   * at a third: "Hm?", and she turns to look; full: she has caught Ace.
//
// No Unity types: Tests/GraceRules compiles this file.
// ---------------------------------------------------------------------------
public static class GraceNight
{
    public enum Act { Out, Sit, Tea, Bed, Sleep, Water, ComeHome }

    public readonly struct Step
    {
        public readonly float at;
        public readonly Act act;

        public Step(float at, Act act)
        {
            this.at = at;
            this.act = act;
        }
    }

    const float Minute = 1f / 60f;

    /// <summary>The night's first hour (11 PM).</summary>
    public const float NightStarts = 23f;
    /// <summary>Thursdays: she comes in through her front door at 1:30 AM...</summary>
    public const float ThursdayHome = 25.5f;
    /// <summary>... on her way from 1:00 AM: out of sight until she has to set off from wherever the street can't be seen to be
    /// at her door at 1:30 (GraceAtHome works out where and when).</summary>
    public const float ThursdayOnHerWay = 25f;

    public static readonly Step[] Usual =
    {
        new Step(23f, Act.Sit),
        new Step(23f + 35f * Minute, Act.Tea),
        new Step(23f + 45f * Minute, Act.Sit),
        new Step(24f, Act.Bed),
        new Step(24f + 20f * Minute, Act.Sleep),
        new Step(26f + 40f * Minute, Act.Water),
        new Step(27f + 5f * Minute, Act.Sleep),
    };

    public static readonly Step[] Thursday =
    {
        new Step(23f, Act.Out),
        new Step(ThursdayOnHerWay, Act.ComeHome),
        new Step(25f + 50f * Minute, Act.Bed),
        new Step(26f + 10f * Minute, Act.Sleep),
    };

    /// <summary>Her night on the night after day <paramref name="day"/> (its weekday: Day 4 is a Thursday).</summary>
    public static Step[] For(int day) => Weekdays.IsThursday(day) ? Thursday : Usual;

    /// <summary>The step under way at <paramref name="hour"/>: the last one that has begun (the first one before any has).</summary>
    public static int Now(Step[] plan, float hour)
    {
        int now = 0;
        for (int i = 0; i < plan.Length; i++) if (plan[i].at <= hour + 1e-4f) now = i;
        return now;
    }

    /// <summary>"11:35 PM" for 23.583.</summary>
    public static string Clock(float hour)
    {
        float h = hour % 24f;
        if (h < 0f) h += 24f;
        int minutes = (int)Math.Round(h * 60f) % (24 * 60);
        int hh = minutes / 60, mm = minutes % 60;
        int twelve = hh % 12 == 0 ? 12 : hh % 12;
        return twelve + ":" + mm.ToString("00") + (hh < 12 ? " AM" : " PM");
    }

    // ---------------------------------------------------------------- her eyes

    /// <summary>How wide she sees (degrees): the whole cone, and the slice toward the screen while she watches TV.</summary>
    public const float Cone = 110f, TvCone = 60f;
    /// <summary>How far she sees Ace (metres): in a lit room, in a dark one.</summary>
    public const float LitRange = 7f, DarkRange = 3f;
    /// <summary>Seconds her mark takes to fill: close up in a lit room, at the edge of her view or in the dark.</summary>
    public const float CloseSeconds = 1f, EdgeSeconds = 3f;
    /// <summary>Within this (metres) Ace is close up.</summary>
    public const float CloseUp = 1f;
    public const float SneakFactor = .5f, TorchFactor = 2f, TvFactor = .5f;

    /// <summary>
    /// How much of her mark fills in a second while she sees Ace <paramref name="distance"/> metres away,
    /// <paramref name="offAxis"/> degrees off the middle of her view (<paramref name="halfCone"/> at its edge). 0 when Ace
    /// is out of her view or beyond <paramref name="range"/>.
    /// </summary>
    public static float SeeRate(bool lit, float distance, float range, float offAxis, float halfCone, bool sneaking, bool torch, bool tv)
    {
        if (range <= 0f || halfCone <= 0f || distance > range || offAxis > halfCone) return 0f;
        // Within a metre is close up; from there to the edge of her range, and from the middle of her view to its edge, the
        // time grows toward 3 s.
        float edge = Math.Max(Clamp01((distance - CloseUp) / Math.Max(.1f, range - CloseUp)), Clamp01(offAxis / halfCone));
        float seconds = lit ? CloseSeconds + (EdgeSeconds - CloseSeconds) * edge : EdgeSeconds;
        float rate = 1f / seconds;
        if (sneaking) rate *= SneakFactor;
        if (torch) rate *= TorchFactor;
        if (tv) rate *= TvFactor;
        return rate;
    }

    /// <summary>How far she sees: 7 m in a lit room (or Ace's torch on: it lights Ace up), 3 m in a dark one.</summary>
    public static float Range(bool lit, bool torch) => lit || torch ? LitRange : DarkRange;

    // ---------------------------------------------------------------- her ears, and the mark

    public const float Third = 1f / 3f;
    /// <summary>Sounds alone take her mark no further than this.</summary>
    public const float SoundCap = 2f / 3f;
    /// <summary>A sound while she's already listening adds this much.</summary>
    public const float SoundAgain = 1f / 6f;
    /// <summary>Sounds closer together than this (seconds) count once (seven footsteps a second are one sound).</summary>
    public const float SoundGap = .6f;
    /// <summary>At a third she turns to look where she noticed Ace, this long (seconds).</summary>
    public const float LookSeconds = 2f;
    /// <summary>With her mark at least this full and Ace gone from her view, she comes to look where she last saw or heard Ace.</summary>
    public const float SearchFrom = .5f;
    /// <summary>Where she comes to look, she looks round this long (seconds), then goes back to what she was doing.</summary>
    public const float SearchSeconds = 3f;
    /// <summary>Nothing seen or heard for this long (seconds), her mark drains this much a second.</summary>
    public const float DrainAfter = 1.5f, DrainPerSecond = .2f;
    /// <summary>Asleep, a loud sound near her wakes her this long (seconds): her lamp on, "Hello?", a look round from the bed.</summary>
    public const float WakeSeconds = 20f;

    /// <summary>Her mark after a sound she hears (its own gap is the caller's: <see cref="SoundGap"/>).</summary>
    public static float Heard(float mark)
    {
        if (mark < Third) return Third;
        return Math.Max(mark, Math.Min(SoundCap, mark + SoundAgain));
    }

    /// <summary>Her mark after <paramref name="seconds"/> of seeing Ace at <paramref name="rate"/> a second (never past full).</summary>
    public static float Seen(float mark, float rate, float seconds) => Math.Min(1f, mark + Math.Max(0f, rate) * Math.Max(0f, seconds));

    /// <summary>Her mark after <paramref name="seconds"/> of draining.</summary>
    public static float Drained(float mark, float seconds) => Math.Max(0f, mark - DrainPerSecond * Math.Max(0f, seconds));

    public static bool Caught(float mark) => mark >= 1f - 1e-5f;

    static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
