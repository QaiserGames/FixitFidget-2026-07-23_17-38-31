using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE CLUMSY HAND (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §5)
//
// A first-timer's hand, as a model: seeded, pure (it is told the time and what is on screen; it never looks at the scene),
// so a run can be played again from its seed. ClumsyBenchLab drives the real input path with it (a virtual mouse, the lab's
// virtual pad) and watches what happens.
//
//   * Reaction before each move: lognormal, median 0.30 s (σ 0.25); 0.6 s more the first time a verb appears in a run.
//   * The mouse's reach (Fitts, ISO 9241-9's mouse: 3.78 bit/s; W the target's VISUAL width, not its hit area: a first-timer
//     doesn't know the hit area is generous): a primary submovement that overshoots on average (D x N(1.05, 0.07), sideways
//     N(0, 0.04 D)), then a correction to the target ± N(0, max(3 px, 0.15 W)), each on a minimum-jerk profile over its own
//     Fitts time. Positions come from real time, so a 15 fps frame makes a real-sized jump.
//   * Tremor: N(0, 1.2 px) a frame at 60 fps (scaled by √(dt/0.0167)); a random-walk drift of 8 px/s during a hold.
//   * 20 % of presses fire at the end of the primary submovement, before the correction (the near miss).
//   * Clicks: lognormal, median 0.11 s (0.06-0.25). Holds last until the hand sees the result + 0.2 s; 10 % let go early at
//     40-80 % of the need and hold again 0.3 s later. The cursor slips 4-12 px over the last 2 frames before every release.
//   * 15 % of new steps take the neighbouring tool first; the hand notices from the hover within 0.6 s.
//   * The stick (rate control; ISO 9241-9's stick: 1.48 bit/s): a push ramping to min(1, √(D/600)) in 0.08 s, its angle off by
//     N(0, 6°); let go once the rest of the way is under 0.12 s at the speed seen, late by N(0.10, 0.03) s (the overshoot);
//     then small pushes (0.3-0.4) until within 10 px or the aim help's snap lands it. Stick noise ±0.03.
//   * On a pad: 70 % of tool picks by LB/RB (15 % of new steps one press too many or too few), 30 % the cursor to the tool
//     and A; 25 % of new steps D-pad right ("next part"); holds on A (70 %) or RT (30 %).
//   * Scrubbing: back and forth across the spot (1.3 x its diameter end to end) at 2.2 Hz along an axis turned ±15°, each
//     stroke off its middle by U(-0.45, 0.45) of its radius, blind; half the strokes aim at what is still drawn. A pad waggles
//     the stick ±0.5 at 2 Hz with A or RT held (and the bench's in-place rub).
//
// Two random streams: one for the hand's decisions (the same seed makes the same choices at 60 fps and at 15 fps, so their
// steps can be compared) and one for what happens every frame (tremor, drift, stick noise).
// ---------------------------------------------------------------------------
public sealed class ClumsyHand
{
    public readonly int Seed;
    public readonly bool Pad;
    readonly System.Random choices, frames;

    public ClumsyHand(int seed, bool pad)
    {
        Seed = seed;
        Pad = pad;
        choices = new System.Random(seed);
        frames = new System.Random(seed * 7919 + 17);
    }

    // ---------- the numbers ----------

    /// <summary>ISO 9241-9 throughputs (bit/s): a mouse and an analogue stick (Natapov, Castellucci and MacKenzie, GI 2009).</summary>
    public const float MouseThroughput = 3.78f, StickThroughput = 1.48f;
    public const float NearMissShare = .20f, EarlyShare = .10f, WrongToolShare = .15f, ShoulderShare = .70f, DpadShare = .25f, HoldOnAShare = .70f;
    public const float ScrubHz = 2.2f, ScrubSpan = 1.3f, ScrubBlind = .45f, PadWaggle = .5f, PadWaggleHz = 2f;

    // ---------- samplers (the decisions' stream) ----------

    public float Uniform(float a, float b) => a + (float)choices.NextDouble() * (b - a);
    public bool Chance(float p) => choices.NextDouble() < p;
    public float Normal(float mean, float sd) => mean + sd * Gauss(choices);
    public float LogNormal(float median, float sigma) => median * Mathf.Exp(sigma * Gauss(choices));

    static float Gauss(System.Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    /// <summary>Seconds before a move begins (+0.6 s the first time its verb appears in the run).</summary>
    public float Reaction(bool firstTimeForVerb) => LogNormal(.30f, .25f) + (firstTimeForVerb ? .6f : 0f);
    /// <summary>A click's length, seconds (0.06-0.25).</summary>
    public float ClickSeconds() => Mathf.Clamp(LogNormal(.11f, .35f), .06f, .25f);
    public bool NearMiss() => Chance(NearMissShare);
    public bool LetsGoEarly() => Chance(EarlyShare);
    /// <summary>How far into the need an early let-go comes (40-80 %).</summary>
    public float EarlyAt() => Uniform(.4f, .8f);
    public bool WrongTool() => Chance(WrongToolShare);
    /// <summary>How long the hand takes to see the wrong tool is in it (from the hover), seconds (≤ 0.6).</summary>
    public float Notice() => Uniform(.25f, .6f);
    public bool PadPicksByShoulders() => Chance(ShoulderShare);
    public bool PadUsesNextPart() => Chance(DpadShare);
    public bool PadHoldsA() => Chance(HoldOnAShare);
    /// <summary>A pad's one press too many (+1) or too few (-1).</summary>
    public int Miscount() => Chance(.5f) ? 1 : -1;

    /// <summary>The slip before a release: 4-12 px in a random direction (spread over the last 2 frames by the lab).</summary>
    public Vector2 Slip()
    {
        float a = Uniform(0f, 2f * Mathf.PI);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Uniform(4f, 12f);
    }

    // ---------- every frame (the frames' stream) ----------

    float FrameNormal(float sd) => sd * Gauss(frames);

    /// <summary>The hand's tremor this frame (px; N(0, 1.2) at 60 fps, scaled by √(dt/0.0167)).</summary>
    public Vector2 Tremor(float dt)
    {
        float k = Mathf.Sqrt(Mathf.Max(0f, dt) / .0167f);
        return new Vector2(FrameNormal(1.2f), FrameNormal(1.2f)) * k;
    }

    /// <summary>The drift of a hand holding still (px this frame): a random walk, about 8 px in a second.</summary>
    public Vector2 Drift(float dt)
    {
        float k = 8f * Mathf.Sqrt(Mathf.Max(0f, dt));
        return new Vector2(FrameNormal(k), FrameNormal(k)) * .7071f;
    }

    /// <summary>A thumb's noise on a stick (±0.03 each way).</summary>
    public Vector2 StickNoise() => new Vector2((float)frames.NextDouble() * .06f - .03f, (float)frames.NextDouble() * .06f - .03f);

    // ---------- the mouse's reach ----------

    /// <summary>Fitts' movement time: 0.15 s + log2(D/W + 1) / throughput.</summary>
    public static float FittsSeconds(float distance, float width, float throughput)
        => .15f + Mathf.Log(Mathf.Max(0f, distance) / Mathf.Max(1f, width) + 1f, 2f) / throughput;

    /// <summary>A reach of the mouse: its primary submovement and its correction (screen pixels, seconds).</summary>
    public struct Reach
    {
        public Vector2 from, aimEnd, end;
        public float primary, corrective;
        public bool nearMiss;
        public float Seconds => primary + corrective;
    }

    /// <summary>Plans a reach from the cursor to a target whose visual width (px) the eye aims at.</summary>
    public Reach PlanReach(Vector2 from, Vector2 target, float visualWidth)
    {
        Vector2 d = target - from;
        float distance = d.magnitude;
        Vector2 dir = distance > 1e-3f ? d / distance : Vector2.right;
        Vector2 across = new Vector2(-dir.y, dir.x);
        Vector2 aimEnd = from + dir * (distance * Normal(1.05f, .07f)) + across * Normal(0f, .04f * distance);
        float spread = Mathf.Max(3f, .15f * visualWidth);
        Vector2 end = target + new Vector2(Normal(0f, spread), Normal(0f, spread));
        return new Reach
        {
            from = from, aimEnd = aimEnd, end = end,
            primary = FittsSeconds(distance, visualWidth, MouseThroughput),
            corrective = FittsSeconds((end - aimEnd).magnitude, visualWidth, MouseThroughput),
            nearMiss = NearMiss(),
        };
    }

    /// <summary>Where a reach has the cursor <paramref name="t"/> seconds after it began (pure: a 15 fps frame jumps).</summary>
    public static Vector2 At(Reach r, float t)
    {
        if (t <= 0f) return r.from;
        if (t < r.primary) return Vector2.LerpUnclamped(r.from, r.aimEnd, MinimumJerk(t / Mathf.Max(1e-3f, r.primary)));
        return Vector2.LerpUnclamped(r.aimEnd, r.end, MinimumJerk((t - r.primary) / Mathf.Max(1e-3f, r.corrective)));
    }

    /// <summary>The minimum-jerk profile: 10s³ − 15s⁴ + 6s⁵.</summary>
    public static float MinimumJerk(float s)
    {
        s = Mathf.Clamp01(s);
        return s * s * s * (10f - 15f * s + 6f * s * s);
    }

    // ---------- the stick's reach (rate control) ----------

    /// <summary>
    /// A thumb taking the pad's cursor to a target: a push, a late let-go, then small corrective pushes until it is within 10
    /// px or the aim help has snapped it there. Update it every frame with where the cursor is; it says the stick.
    /// </summary>
    public sealed class StickReach
    {
        readonly ClumsyHand hand;
        public Vector2 Target;
        enum Phase { Start, Push, LettingGo, Looking, Done }
        Phase phase = Phase.Start;
        Vector2 dir;
        float deflection, pushedAt, letGoAt, lookUntil;
        Vector2 last;
        bool hasLast;
        float speed;               // the cursor's speed as the eye sees it (reference px a second)
        public int Pushes { get; private set; }
        public bool Done => phase == Phase.Done;
        /// <summary>The stick has just been let go after a push (the moment a near-miss press fires).</summary>
        public bool JustLetGo { get; private set; }

        internal StickReach(ClumsyHand hand, Vector2 target) { this.hand = hand; Target = target; }

        /// <summary>The stick this frame (its x and y, before the pad's own deadzone and curve).</summary>
        public Vector2 Update(Vector2 cursor, float now, float dt, float pxScale, bool gliding)
        {
            JustLetGo = false;
            if (hasLast && dt > 0f) speed = Mathf.Lerp(speed, (cursor - last).magnitude / pxScale / dt, 1f - Mathf.Exp(-dt / .05f));
            last = cursor;
            hasLast = true;
            Vector2 to = (Target - cursor) / pxScale;
            switch (phase)
            {
                case Phase.Start:
                case Phase.Looking when now >= lookUntil && !gliding:
                    if (to.magnitude <= 10f || Pushes >= 14) { phase = Phase.Done; return Vector2.zero; }
                    // A push: the first is as big as the distance asks, its angle off a little; then small ones.
                    float angle = hand.Normal(0f, 6f) * Mathf.Deg2Rad;
                    Vector2 d = to.normalized;
                    dir = new Vector2(d.x * Mathf.Cos(angle) - d.y * Mathf.Sin(angle), d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle));
                    deflection = Pushes == 0 ? Mathf.Min(1f, Mathf.Sqrt(to.magnitude / 600f)) : hand.Uniform(.3f, .4f);
                    pushedAt = now;
                    Pushes++;
                    phase = Phase.Push;
                    return dir * (deflection * Mathf.Clamp01(dt / .08f));
                case Phase.Looking:
                    return Vector2.zero;
                case Phase.Push:
                {
                    // Let go once the rest of the way (along the push) is under 0.12 s at the speed seen, late by N(0.10, 0.03) s.
                    float ahead = Vector2.Dot(to, dir);
                    if (ahead < speed * .12f || ahead < 0f)
                    {
                        phase = Phase.LettingGo;
                        letGoAt = now + Mathf.Max(0f, hand.Normal(.10f, .03f));
                    }
                    return dir * (deflection * Mathf.Clamp01((now - pushedAt) / .08f));
                }
                case Phase.LettingGo:
                    if (now < letGoAt) return dir * deflection;
                    phase = Phase.Looking;
                    lookUntil = now + hand.Uniform(.12f, .25f);
                    JustLetGo = true;
                    return Vector2.zero;
            }
            return Vector2.zero;
        }
    }

    public StickReach PlanStick(Vector2 target) => new StickReach(this, target);

    // ---------- scrubbing ----------

    /// <summary>The hand scrubbing a spot: back and forth along its axis, each stroke off the middle by its own offset.</summary>
    public sealed class Scrub
    {
        readonly ClumsyHand hand;
        public Vector2 Centre;
        public float Radius;            // the spot's radius on screen, px
        public readonly Vector2 Axis, Across;
        public readonly float StartedAt;
        int stroke = -1;
        float offset;
        public int Strokes => stroke + 1;

        internal Scrub(ClumsyHand hand, Vector2 centre, float radiusPx, float now)
        {
            this.hand = hand;
            Centre = centre;
            Radius = Mathf.Max(3f, radiusPx);
            float a = hand.Uniform(-15f, 15f) * Mathf.Deg2Rad;
            Axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Across = new Vector2(-Axis.y, Axis.x);
            StartedAt = now;
        }

        /// <summary>
        /// Where the cursor is <paramref name="now"/>: along the axis, 1.3 x the spot's diameter end to end at 2.2 Hz; across
        /// it, each stroke's own offset (blind, U(-0.45, 0.45) of the radius; every other one toward <paramref name="dirtLeft"/>,
        /// what is still drawn).
        /// </summary>
        public Vector2 At(float now, Vector2 dirtLeft)
        {
            float t = now - StartedAt;
            // A stroke is half a cycle; it starts at an end (the sine at ±1).
            int s = Mathf.FloorToInt(t * 2f * ScrubHz);
            if (s != stroke)
            {
                stroke = s;
                offset = stroke % 2 == 1
                    ? Mathf.Clamp(Vector2.Dot(dirtLeft - Centre, Across), -Radius, Radius)
                    : hand.Uniform(-ScrubBlind, ScrubBlind) * Radius;
            }
            float along = -Mathf.Cos(t * 2f * Mathf.PI * ScrubHz) * ScrubSpan * Radius;   // ±1.3 R: 1.3 x the diameter end to end
            return Centre + Axis * along + Across * offset;
        }

        /// <summary>A pad's waggle at 2 Hz along the axis, as wide as the spot (a thumb sizes its strokes to what it sees, as the
        /// mouse's ±1.3 R; never under ±0.5 on the stick, the waggle of every spot until 15 Oct), leaning back toward the spot's
        /// middle when the cursor has wandered off it. A cosine on the stick, so the cursor swings either side of where it
        /// started (with the sine it had until 15 Oct, it swung from the middle out to one side and back).</summary>
        public Vector2 PadStick(float now, Vector2 cursor)
        {
            float t = now - StartedAt;
            Vector2 waggle = Axis * (padAmplitude * Mathf.Cos(t * 2f * Mathf.PI * PadWaggleHz));
            Vector2 back = (Centre - cursor) / Mathf.Max(1f, Radius * 3f);
            if (back.magnitude > .35f) back = back.normalized * .35f;
            return waggle + back;
        }

        float padAmplitude = PadWaggle;

        // The stick's waggle for strokes of ±1.3 R: a stick swung V cos(wt) moves the cursor V/w either side, so V = w x 1.3 R,
        // taken back through the pad's dead zone (0.2) and curve (^1.6) at the cursor's 1150 px/s (reference pixels).
        internal void SizeThePadStroke(float pxScale)
        {
            float v = 2f * Mathf.PI * PadWaggleHz * ScrubSpan * Radius / Mathf.Max(.5f, pxScale);
            float curved = Mathf.Clamp01(v / 1150f);
            padAmplitude = Mathf.Clamp(.2f + .8f * Mathf.Pow(curved, 1f / 1.6f), PadWaggle, 1f);
        }
    }

    public Scrub PlanScrub(Vector2 centre, float radiusPx, float now) => new Scrub(this, centre, radiusPx, now);

    /// <summary>As PlanScrub, with a pad's waggle sized to the spot (<paramref name="pxScale"/>: screen pixels per reference pixel).</summary>
    public Scrub PlanScrub(Vector2 centre, float radiusPx, float now, float pxScale)
    {
        var scrub = new Scrub(this, centre, radiusPx, now);
        scrub.SizeThePadStroke(pxScale);
        return scrub;
    }
}
