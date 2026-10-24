using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// AIM HELP ON A PAD (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.3)
//
// Mansoor's note: "very hard to get the controller cursor onto an item". A thumbstick can't make the small, exact
// corrections a mouse makes. So the views that aim with the middle of the screen (first person, and the drinks
// close-up) help a pad the way console shooters and sims do, and leave the mouse alone:
//
//   1. Friction: over or near something Ace can use, the view turns slower, so the crosshair doesn't sail past it.
//   2. Magnetism: while the stick turns the view, the crosshair is drawn gently toward the middle of a target ahead.
//   3. Settling: let go of the stick with the crosshair near a target, and it slides onto it.
//   4. Stepping: D-pad left or right turns the view to the next target that side (PlayerInteractor asks; the view
//      glides there).
//
// HELP THAT DOESN'T FIGHT (23 Oct 2026). His note, from his own playtest on a pad: "if I grab a cup and like someone ordered
// an espresso, I grab the cup and I go to the espresso machine, I'm like locked to the cup. It's like pushing me. It's like
// hard for me to even look to press the nozzle ... I would rather it be, you know, like helpful, but not so overpowering that
// it's like fighting me". The cup in his hand was never a target: PlayerCarry turns a held thing's colliders off, and the
// targets are found by their colliders (PlayerInteractor.AimTargets now also leaves out whatever Ace holds, by name). What
// held him was the thing he was LEAVING: the cup stack he took the cup from, then the cup under the nozzle and its nozzle
// area. The 7 Oct code, run outside Unity on the drinks machine's own numbers (its hit boxes, the close-up's camera), showed it:
//   * letting go settled onto the nearest target, the one behind too, so a small nudge off the cup that he let go of before
//     the next thing was nearer glided back onto it: twenty gentle nudges (the stick half way for a quarter of a second)
//     each ended exactly where they began, on the cup, on its nozzle area, on the cup stack;
//   * friction slowed the view to 0.4 near anything, at any push. The machine's targets sit 10° apart and each slowed it
//     within 7.5° of its middle, so a full push from the cup stack across the nozzles turned at 42°/s instead of 95;
//   * the pull drew toward the nearest target unless it lay more than 107° behind the push: sideways, at up to 58% of the
//     slowed view's own speed in the close-up, bending the view about 30° away from where the thumb pointed.
// Now:
//   * the help fades with intent: whole for a gentle aiming push, none once the stick is pushed most of the way (Intent);
//   * nothing the crosshair was on, or at the edge of, as a push began slows it once that thing's middle is behind the push
//     (off a cup standing in its slot, neither the cup nor its nozzle area: the cup sits inside the area's hit box), and the
//     pull draws only toward a target ahead of the push (within AheadCone of its way), never one beside or behind it;
//   * letting go never glides back the way the push came, unless the push went just past something (an overshoot, within
//     half the push: the bench's own rule since 15 Oct, PadCursor.Nearest), and never back to the middle of a target the
//     crosshair is still on;
//   * where targets crowd (within CrowdGap of each other, edge to edge: the drinks machine), friction and the pull are halved;
//   * two strengths in the phone's Settings, Light (the default) and Strong (the 7 Oct numbers), or Off (AimHelpLevel).
// The settle stays whole at both strengths: a small nudge that stops a few degrees short of a target still lands on it. That
// is the help his girlfriend found useful on a pad once it was on (19 Oct: "Better? Yeah.").
//
// The bench's pointer has its own help (PadCursor: it settles on the nearest part, tool or grime within about 60 px,
// never undoing a push, and the D-pad steps through the item's parts in the order the work goes; ItemInspector).
//
// The help only ever turns the view: what's picked is still decided in one place (PlayerInteractor.FindBest), so
// what's highlighted is always what E or RT will act on. Which things count as targets is the interactor's too
// (AimTargets: what's in reach and in sight, by the same rules as the crosshair).
//
// Saved: the switch (PrefKey, on by default, its meaning since 7 Oct) and, since 23 Oct 2026, the strength it has while it's on
// (StrengthKey, Light unless Strong was chosen; a machine whose help was simply "on" has Light). Settings › CONTROLS.
// ---------------------------------------------------------------------------

/// <summary>
/// How strongly the pad's aim help works (Settings › CONTROLS, "Aim help"; 23 Oct 2026). The numbers are what PlayerPrefs keeps
/// under AimAssist.StrengthKey (Light and Strong; Off is the switch, AimAssist.PrefKey): never reorder them.
/// </summary>
public enum AimHelpLevel
{
    /// <summary>No help: the stick turns the view and nothing else (and the bench's pointer doesn't settle either).</summary>
    Off = 0,
    /// <summary>The default: a light touch (AimAssist.LightTuning).</summary>
    Light = 1,
    /// <summary>The 7 Oct numbers, under the rules of 23 Oct 2026 (AimAssist.StrongTuning).</summary>
    Strong = 2,
}

public static class AimAssist
{
    /// <summary>The player's switch, kept between sessions: 1 on, 0 off (on is the strength kept under StrengthKey).</summary>
    public const string PrefKey = "FixitFidget.AimHelp";
    /// <summary>
    /// The help's strength while it's on (23 Oct 2026), kept as AimHelpLevel's number: Light (1; also when nothing is kept, so
    /// every machine whose help was simply "on" has Light) or Strong (2). Turning the help off leaves it as it was.
    /// </summary>
    public const string StrengthKey = "FixitFidget.AimHelp.Strength";

    static bool? enabled;
    static AimHelpLevel? strength;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Reload();

    /// <summary>Forgets what was read, so the switch and the strength are read again from PlayerPrefs (GameSettings.Reload).</summary>
    public static void Reload()
    {
        enabled = null;
        strength = null;
    }

    /// <summary>Aim help on or off (on by default; saved). While on, it works at Strength.</summary>
    public static bool Enabled
    {
        get
        {
            enabled ??= PlayerPrefs.GetInt(PrefKey, 1) == 1;
            return enabled.Value;
        }
        set
        {
            enabled = value;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The strength the help has while it's on: Light (the default) or Strong; saved. Anything else is taken as Light.</summary>
    public static AimHelpLevel Strength
    {
        get
        {
            strength ??= PlayerPrefs.GetInt(StrengthKey, (int)AimHelpLevel.Light) == (int)AimHelpLevel.Strong
                ? AimHelpLevel.Strong : AimHelpLevel.Light;
            return strength.Value;
        }
        set
        {
            strength = value == AimHelpLevel.Strong ? AimHelpLevel.Strong : AimHelpLevel.Light;
            PlayerPrefs.SetInt(StrengthKey, (int)strength.Value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Off, Light or Strong, as the phone's row shows it. Off keeps the strength, so turning the help on brings it back.</summary>
    public static AimHelpLevel Level
    {
        get => Enabled ? Strength : AimHelpLevel.Off;
        set
        {
            if (value != AimHelpLevel.Off) Strength = value;
            Enabled = value != AimHelpLevel.Off;
        }
    }

    /// <summary>The help applies now: switched on, and a pad in hand (the mouse is never helped).</summary>
    public static bool Active => Enabled && PadInput.UsingPad;

    // ------------------------------------------------------------------ the numbers

    /// <summary>One strength's numbers.</summary>
    public readonly struct Tuning
    {
        /// <summary>The turn's speed over a target, of full (before the push's intent and the crowd lighten it).</summary>
        public readonly float friction;
        /// <summary>Degrees past a target's edge over which the slowing fades out.</summary>
        public readonly float frictionMargin;
        /// <summary>Degrees past a target's edge within which turning draws the crosshair toward its middle.</summary>
        public readonly float magnetCone;
        /// <summary>How hard it draws: degrees per second at full stick (before the intent and the crowd lighten it).</summary>
        public readonly float magnetRate;
        /// <summary>Degrees past a target's edge within which letting go of the stick settles onto it.</summary>
        public readonly float settleCone;

        public Tuning(float friction, float frictionMargin, float magnetCone, float magnetRate, float settleCone)
        {
            this.friction = friction;
            this.frictionMargin = frictionMargin;
            this.magnetCone = magnetCone;
            this.magnetRate = magnetRate;
            this.settleCone = settleCone;
        }
    }

    // The 7 Oct numbers: Strong's since 23 Oct 2026.
    /// <summary>Strong: the turn's speed over a target, of full.</summary>
    public const float Friction = .4f;
    /// <summary>Strong: degrees past a target's edge over which the slowing fades out.</summary>
    public const float FrictionMargin = 3f;
    /// <summary>Strong: degrees past a target's edge within which turning draws the crosshair toward its middle.</summary>
    public const float MagnetCone = 7f;
    /// <summary>Strong: how hard it draws, degrees per second at full stick.</summary>
    public const float MagnetRate = 22f;
    /// <summary>Both strengths: degrees past a target's edge within which letting go of the stick settles onto it.</summary>
    public const float SettleCone = 4f;

    /// <summary>Strong: the 7 Oct numbers, under the rules of 23 Oct 2026.</summary>
    public static readonly Tuning StrongTuning = new Tuning(Friction, FrictionMargin, MagnetCone, MagnetRate, SettleCone);
    /// <summary>
    /// Light, the default: about half of Strong (the view at 0.7 of its speed over a lone target, the pull at half the rate and
    /// over a smaller cone), and the same settle, the part that lands a small nudge without fighting anything.
    /// </summary>
    public static readonly Tuning LightTuning = new Tuning(.7f, 2f, 5f, 11f, SettleCone);
    /// <summary>The strength in force (whether the help is on is Enabled's).</summary>
    public static Tuning Now => Strength == AimHelpLevel.Strong ? StrongTuning : LightTuning;

    /// <summary>The settle's time constant, seconds (it lands in about three).</summary>
    public const float SettleSeconds = .05f;
    /// <summary>A D-pad step's time constant, seconds.</summary>
    public const float StepSeconds = .045f;
    /// <summary>A target's angular half-size counts for at most this many degrees.</summary>
    public const float MaxSize = 8f;

    // ---- the push's intent (23 Oct 2026) ----
    // His note: "helpful, but not so overpowering that it's like fighting me to like for it to move the analog stick". A thumb
    // that is aiming moves the stick a little; one that is going somewhere pushes it most of the way. So the help (friction and
    // the pull; the settle comes after the stick lets go) is whole up to 35% of the stick's travel past its dead zone, about half
    // way on the stick itself (the view at 19% of its full speed: 18°/s in the drinks close-up, 32°/s in first person, slow
    // enough to cross a nozzle in half a second), and gone at 75%, about three quarters of the way (63% of full speed, the
    // whole machine in about a second): there the player is travelling, and a help that slows or bends him is in his way. A
    // smooth step between, so a thumb easing off as it nears something gets the help back gradually, with no step to feel.

    /// <summary>The stick's travel past its dead zone (0 to 1) below which the help is whole: a nudge, aiming.</summary>
    public const float FullHelpBelow = .35f;
    /// <summary>The travel above which there is none: the stick pushed most of the way, going somewhere.</summary>
    public const float NoHelpAbove = .75f;
    // The curve the views give the stick before they pass it to Steer (PadInput.Curved's own exponent; CafeViewMode and
    // BeverageLook both use its default), undone here so the help fades by how far the thumb pushes, not by how fast the view
    // turns.
    const float StickCurve = 1.6f;

    /// <summary>
    /// How much of the help a push gets (1 whole, 0 none), from <paramref name="stick"/> as the views pass it to Steer (0 to 1,
    /// after PadInput.Curved): whole up to FullHelpBelow of the stick's travel, none from NoHelpAbove, a smooth step between.
    /// </summary>
    public static float Intent(float stick)
    {
        if (stick <= 0f) return 1f;
        float travel = Mathf.Pow(Mathf.Clamp01(stick), 1f / StickCurve);
        return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(FullHelpBelow, NoHelpAbove, travel));
    }

    // ---- what lies ahead (23 Oct 2026) ----

    /// <summary>
    /// The pull draws only toward a target whose middle lies within this many degrees of the way the stick turns the view:
    /// never one beside or behind the push, so never back toward what the crosshair is leaving.
    /// </summary>
    public const float AheadCone = 40f;
    // Degrees: the target a push begins on slows it only while its middle is at least this far ahead along the push.
    const float LeaveSlack = .25f;

    // ---- crowds (23 Oct 2026) ----
    // At the drinks machine the six nozzle areas touch, edge to edge, as the help measures them from where Ace stands, the
    // paddles sit about 4° apart, the cup stack and the cup return beside them: the crosshair is always on or near something,
    // so slowing over each and pulling toward each is all the help does there, and any of them is a fine place to stop. Where
    // two targets are closer than CrowdGap, edge to edge, friction's slowing and the pull are CrowdHelp of themselves; from
    // twice that apart, whole. The settle and the D-pad's steps are whole everywhere.

    /// <summary>Degrees, edge to edge: two targets closer than this crowd each other; from twice this apart they don't.</summary>
    public const float CrowdGap = 6f;
    /// <summary>The share of friction's slowing and of the pull left over a target in a crowd (the settle is whole).</summary>
    public const float CrowdHelp = .5f;

    /// <summary>How much of the help is left over target <paramref name="i"/> for the targets around it: CrowdHelp to 1.</summary>
    public static float Crowd(Vector3 eye, IReadOnlyList<Target> targets, int i)
    {
        if (targets == null || i < 0 || i >= targets.Count) return 1f;
        Vector3 to = targets[i].point - eye;
        float size = HalfAngle(eye, targets[i]);
        float gap = float.PositiveInfinity;
        for (int j = 0; j < targets.Count; j++)
        {
            if (j == i) continue;
            float g = Vector3.Angle(to, targets[j].point - eye) - size - HalfAngle(eye, targets[j]);
            if (g < gap) gap = g;
        }
        return Mathf.Lerp(CrowdHelp, 1f, Mathf.InverseLerp(CrowdGap, 2f * CrowdGap, gap));
    }

    // ------------------------------------------------------------------ targets

    /// <summary>Something to aim at: where its middle is, half its smallest width as seen (metres), and what it is.</summary>
    public struct Target
    {
        public Vector3 point;
        public float size;
        /// <summary>What it is (the Interactable; may be null): how the help knows a target from one frame to the next (23 Oct 2026).</summary>
        public UnityEngine.Object thing;
        public Target(Vector3 point, float size) { this.point = point; this.size = size; thing = null; }
        public Target(Vector3 point, float size, UnityEngine.Object thing) { this.point = point; this.size = size; this.thing = thing; }
    }

    /// <summary>One view's help as it goes: turning, or gliding (a settle, or a D-pad step) toward a point.</summary>
    public sealed class State
    {
        internal bool turning, gliding;
        internal Vector3 glideTo;
        internal float glideSeconds;
        // The push under way: the view where it began (yaw, pitch), the way the stick last turned it, and the target the
        // crosshair was on (or at the edge of) as it began: what the push leaves.
        internal Vector2 pushFrom, way;
        internal bool hasLeaving;
        internal Target leaving;
        // Everything the crosshair was on, or at the edge of, as the push began, the nearest first (23 Oct 2026, the review: a
        // cup standing in its slot sits inside its nozzle area's box, so off the cup the area went on slowing the view, on 110
        // frames of a gentle push at Light). Four places, filled where a push begins: no garbage.
        internal readonly Target[] leavingAll = new Target[4];
        internal int leavingCount;
        // What the help did this frame (and the last settle), for the checks.
        internal float help = 1f, slow = 1f;
        internal UnityEngine.Object slowing, pulling, settling;

        /// <summary>A settle or a step is under way (checks).</summary>
        public bool Gliding => gliding;
        /// <summary>Where the glide is going.</summary>
        public Vector3 GlideTarget => glideTo;
        /// <summary>The share of the help the push got this frame: 1 for a nudge, 0 for a stick pushed most of the way (checks).</summary>
        public float Help => help;
        /// <summary>The share of the stick's own turn friction left this frame: 1 when nothing slowed it (checks).</summary>
        public float Slow => slow;
        /// <summary>What slowed the view this frame (null: nothing, or nothing named; checks).</summary>
        public UnityEngine.Object Slowing => slowing;
        /// <summary>What the view was drawn toward this frame (null: nothing; checks).</summary>
        public UnityEngine.Object Pulling => pulling;
        /// <summary>What the last settle went to (null: none since the last reset; checks).</summary>
        public UnityEngine.Object Settling => settling;
        /// <summary>The target the push under way began on, the nearest of those it leaves (null: none, or nothing named; checks).</summary>
        public UnityEngine.Object Leaving => hasLeaving ? leaving.thing : null;

        public void Reset()
        {
            turning = false;
            gliding = false;
            hasLeaving = false;
            leavingCount = 0;
            help = slow = 1f;
            slowing = pulling = settling = null;
        }

        /// <summary>Whether this frame needs the targets: the stick is turning the view, or has just let go.</summary>
        public bool WantsTargets(bool stickMoving) => stickMoving || turning;

        /// <summary>A D-pad step: glide to <paramref name="point"/>.</summary>
        public void StepTo(Vector3 point)
        {
            gliding = true;
            turning = false;
            hasLeaving = false;
            leavingCount = 0;
            glideTo = point;
            glideSeconds = StepSeconds;
        }
    }

    /// <summary>The yaw and pitch (degrees; pitch positive looks down, as Unity's) that look from <paramref name="eye"/> at
    /// <paramref name="point"/>.</summary>
    public static Vector2 Angles(Vector3 eye, Vector3 point)
    {
        Vector3 d = point - eye;
        float flat = Mathf.Sqrt(d.x * d.x + d.z * d.z);
        return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg);
    }

    static Vector2 Offset(Vector2 view, Vector2 want) => new Vector2(Mathf.DeltaAngle(view.x, want.x), want.y - view.y);

    static Vector3 Forward(Vector2 view) => Quaternion.Euler(view.y, view.x, 0f) * Vector3.forward;

    // A target's angular half-size from the eye, degrees (at most MaxSize).
    static float HalfAngle(Vector3 eye, Target t)
    {
        float distance = (t.point - eye).magnitude;
        return distance < .01f ? MaxSize : Mathf.Min(MaxSize, Mathf.Atan2(Mathf.Max(0f, t.size), distance) * Mathf.Rad2Deg);
    }

    /// <summary>How far (degrees) a target is from the middle of the view, and its angular half-size.</summary>
    public static float AngleTo(Vector3 eye, Vector2 view, Target t, out float size)
    {
        Vector3 d = t.point - eye;
        size = HalfAngle(eye, t);
        return d.magnitude < .001f ? 0f : Vector3.Angle(Forward(view), d);
    }

    /// <summary>The target nearest the middle of the view (by how far it is past its own edge), or -1.</summary>
    public static int Nearest(Vector3 eye, Vector2 view, IReadOnlyList<Target> targets, out float angle, out float size)
    {
        int best = -1;
        angle = size = 0f;
        float bestPast = float.PositiveInfinity;
        if (targets == null) return -1;
        for (int i = 0; i < targets.Count; i++)
        {
            float a = AngleTo(eye, view, targets[i], out float s);
            float past = a - s;
            if (past < bestPast) { bestPast = past; best = i; angle = a; size = s; }
        }
        return best;
    }

    // The same target as one frame ago: the same thing, or (nothing named) the same place within a centimetre.
    static bool Same(Target a, Target b)
    {
        bool named = !object.ReferenceEquals(a.thing, null) || !object.ReferenceEquals(b.thing, null);
        return named ? object.ReferenceEquals(a.thing, b.thing) : (a.point - b.point).sqrMagnitude < 1e-4f;
    }

    // ------------------------------------------------------------------ the help

    /// <summary>
    /// The view after this frame, with the help: <paramref name="view"/> is (yaw, pitch) in degrees (pitch positive looks
    /// down), <paramref name="turn"/> the stick's own turn this frame in the same terms, <paramref name="stick"/> how far
    /// the stick is pushed (0 to 1, after PadInput.Curved). Call it every frame the help is active (a glide carries on with the
    /// stick still). The view never moves against the stick: friction only shortens the turn, and the pull lies within
    /// AheadCone of it.
    /// </summary>
    public static Vector2 Steer(Vector3 eye, Vector2 view, Vector2 turn, float stick, float dt,
        IReadOnlyList<Target> targets, State state)
    {
        Tuning tune = Now;
        int count = targets != null ? targets.Count : 0;
        bool turningNow = stick > .01f && turn.sqrMagnitude > 1e-10f;
        state.slowing = state.pulling = null;
        state.slow = 1f;

        if (turningNow)
        {
            Vector2 way = turn.normalized;
            if (!state.turning)
            {
                // A push begins: what the crosshair is on, or at the edge of, is what it may be leaving: the nearest, then any
                // other within its own fade (if the nearest isn't, none is: the margin is the same for all).
                state.pushFrom = view;
                state.leavingCount = 0;
                int on = Nearest(eye, view, targets, out float onAngle, out float onSize);
                state.hasLeaving = on >= 0 && onAngle <= onSize + tune.frictionMargin;
                if (state.hasLeaving)
                {
                    state.leaving = targets[on];
                    state.leavingAll[state.leavingCount++] = targets[on];
                    for (int j = 0; j < count && state.leavingCount < state.leavingAll.Length; j++)
                    {
                        if (j == on) continue;
                        float aj = AngleTo(eye, view, targets[j], out float sj);
                        if (aj <= sj + tune.frictionMargin) state.leavingAll[state.leavingCount++] = targets[j];
                    }
                }
            }
            state.turning = true;
            state.gliding = false;
            state.way = way;
            float help = Intent(stick);
            state.help = help;

            // Friction: from the nearest target by its edge, but never from one being left while its middle is behind the push
            // (the 7 Oct help slowed the first degrees off the cup most: "hard for me to even look").
            float scale = 1f;
            if (help > 0f && count > 0)
            {
                int f = NearestNotLeft(eye, view, way, targets, state, out float angle, out float size);
                if (f >= 0 && angle <= size + tune.frictionMargin)
                {
                    float near = 1f - Mathf.InverseLerp(size, size + tune.frictionMargin, angle);
                    scale = 1f - (1f - tune.friction) * near * help * Crowd(eye, targets, f);
                    state.slowing = targets[f].thing;
                    state.slow = scale;
                }
            }
            Vector2 next = view + turn * scale;

            // The pull: toward the middle of the nearest target ahead of the push, never one beside or behind it.
            if (help > 0f && count > 0)
            {
                int m = NearestAhead(eye, next, way, targets, tune.magnetCone, out Vector2 off);
                if (m >= 0)
                {
                    next += Vector2.ClampMagnitude(off, tune.magnetRate * stick * help * Crowd(eye, targets, m) * dt);
                    state.pulling = targets[m].thing;
                }
            }
            return next;
        }

        if (state.turning)
        {
            // The stick has just let go: near a target, settle onto it (never back the way the push came, but for an overshoot).
            state.turning = false;
            int s = SettleTarget(eye, view, targets, state, tune.settleCone);
            if (s >= 0)
            {
                state.gliding = true;
                state.glideTo = targets[s].point;
                state.glideSeconds = SettleSeconds;
                state.settling = targets[s].thing;
            }
            state.hasLeaving = false;
        }
        if (state.gliding)
        {
            Vector2 off = Offset(view, Angles(eye, state.glideTo));
            if (off.magnitude < .08f)
            {
                state.gliding = false;
                return view + off;
            }
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(.005f, state.glideSeconds));
            return view + off * k;
        }
        return view;
    }

    // The nearest target by its edge (Nearest's measure), leaving out those the push began on or at the edge of unless a
    // middle is still ahead of the push (by LeaveSlack, so a push that begins on one's very middle, where ahead and behind
    // can't be told, is leaving it from the first frame): what the crosshair leaves never slows it. If the push turns back
    // toward one, it counts again.
    static int NearestNotLeft(Vector3 eye, Vector2 view, Vector2 way, IReadOnlyList<Target> targets, State state,
        out float angle, out float size)
    {
        int best = -1;
        angle = size = 0f;
        float bestPast = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            if (state.hasLeaving && Left(state, targets[i])
                && Vector2.Dot(Offset(view, Angles(eye, targets[i].point)), way) < LeaveSlack) continue;
            float a = AngleTo(eye, view, targets[i], out float s);
            if (a - s < bestPast) { bestPast = a - s; best = i; angle = a; size = s; }
        }
        return best;
    }

    // One of the targets the push under way began on, or at the edge of.
    static bool Left(State state, Target t)
    {
        for (int k = 0; k < state.leavingCount; k++)
            if (Same(t, state.leavingAll[k])) return true;
        return false;
    }

    // The nearest target by its edge within `cone` past its edge whose middle lies ahead of the push (within AheadCone of
    // `way`, a unit vector), and the offset from `from` to its middle; -1 when there is none.
    static int NearestAhead(Vector3 eye, Vector2 from, Vector2 way, IReadOnlyList<Target> targets, float cone, out Vector2 offset)
    {
        int best = -1;
        offset = Vector2.zero;
        float bestPast = float.PositiveInfinity, ahead = Mathf.Cos(AheadCone * Mathf.Deg2Rad);
        for (int i = 0; i < targets.Count; i++)
        {
            float a = AngleTo(eye, from, targets[i], out float s);
            if (a > s + cone || a - s >= bestPast) continue;
            Vector2 off = Offset(from, Angles(eye, targets[i].point));
            float length = off.magnitude;
            if (length < 1e-4f || Vector2.Dot(off / length, way) < ahead) continue;
            bestPast = a - s;
            best = i;
            offset = off;
        }
        return best;
    }

    // Letting go: the nearest target (by its edge) within the settle cone, leaving out any the crosshair would have to go back
    // the way the push came to reach, by more than half the push. That is the bench's rule (15 Oct, PadCursor.Nearest, after a
    // tug of war at the bench in Bench 8): a nudge away from something is never pulled back onto it, and a push that went just
    // past something settles back onto it. If the nearest is one the crosshair is already on, with its middle behind, the view
    // stays where it is: it is on it already, and gliding to its middle would be a step back.
    static int SettleTarget(Vector3 eye, Vector2 view, IReadOnlyList<Target> targets, State state, float cone)
    {
        if (targets == null) return -1;
        float pushed = Offset(state.pushFrom, view).magnitude;
        int best = -1;
        bool backOnto = false;
        float bestPast = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            float a = AngleTo(eye, view, targets[i], out float s);
            if (a > s + cone || a - s >= bestPast) continue;
            Vector2 off = Offset(view, Angles(eye, targets[i].point));
            bool behind = Vector2.Dot(off, state.way) < 0f;
            if (behind && off.sqrMagnitude > .25f * pushed * pushed) continue;
            bestPast = a - s;
            best = i;
            backOnto = behind && a <= s;
        }
        return backOnto ? -1 : best;
    }

    /// <summary>
    /// The next target to the left (<paramref name="direction"/> -1) or right (+1) of the middle of the screen: the
    /// nearest of those at least a little to that side, or -1. Targets off screen don't count.
    /// </summary>
    public static int Next(Camera cam, IReadOnlyList<Target> targets, int direction)
    {
        if (cam == null || targets == null || direction == 0) return -1;
        float middle = Screen.width * .5f, height = Screen.height * .5f;
        float least = Mathf.Max(6f, Screen.height / 180f);
        int best = -1;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 p = cam.WorldToScreenPoint(targets[i].point);
            if (p.z <= cam.nearClipPlane || p.x < 0f || p.x > Screen.width || p.y < 0f || p.y > Screen.height) continue;
            float dx = (p.x - middle) * direction;
            if (dx < least) continue;
            float score = dx + Mathf.Abs(p.y - height) * .5f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}
