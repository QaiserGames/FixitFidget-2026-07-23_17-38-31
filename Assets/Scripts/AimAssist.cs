using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// AIM HELP ON A PAD (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.3)
//
// Mansoor's note: "very hard to get the controller cursor onto an item". A thumbstick can't make the small, exact
// corrections a mouse makes. So the views that aim with the middle of the screen (first person, and the drinks
// close-up) help a pad the way console shooters and sims do, and leave the mouse alone:
//
//   1. Friction: over or near something Ace can use, the view turns at 40% of its speed, so the crosshair doesn't
//      sail past it.
//   2. Magnetism: while the stick turns the view, the crosshair is drawn gently toward the nearest target's middle
//      (never against the stick: pushing away from a target lets it go).
//   3. Settling: let go of the stick with the crosshair near a target, and it slides onto it.
//   4. Stepping: D-pad left or right turns the view to the next target that side (PlayerInteractor asks; the view
//      glides there).
//
// The bench's pointer has its own help (PadCursor: it settles on the nearest part, tool or grime within about 60 px,
// and the D-pad steps through the item's parts in the order the work goes; ItemInspector).
//
// The help only ever turns the view: what's picked is still decided in one place (PlayerInteractor.FindBest), so
// what's highlighted is always what E or RT will act on. Which things count as targets is the interactor's too
// (AimTargets: what's in reach and in sight, by the same rules as the crosshair).
//
// On by default; a saved switch (its place in the pause phone's Settings comes in session 3).
// ---------------------------------------------------------------------------
public static class AimAssist
{
    /// <summary>The player's switch, kept between sessions.</summary>
    public const string PrefKey = "FixitFidget.AimHelp";

    static bool? enabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => enabled = null;

    /// <summary>Aim help on or off (on by default; saved).</summary>
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

    /// <summary>The help applies now: switched on, and a pad in hand (the mouse is never helped).</summary>
    public static bool Active => Enabled && PadInput.UsingPad;

    /// <summary>The turn's speed over a target, of full.</summary>
    public const float Friction = .4f;
    /// <summary>Degrees past a target's edge over which the slowing fades out.</summary>
    public const float FrictionMargin = 3f;
    /// <summary>Degrees past a target's edge within which turning draws the crosshair toward its middle.</summary>
    public const float MagnetCone = 7f;
    /// <summary>How hard it draws: degrees per second at full stick.</summary>
    public const float MagnetRate = 22f;
    /// <summary>Degrees past a target's edge within which letting go of the stick settles onto it.</summary>
    public const float SettleCone = 4f;
    /// <summary>The settle's time constant, seconds (it lands in about three).</summary>
    public const float SettleSeconds = .05f;
    /// <summary>A D-pad step's time constant, seconds.</summary>
    public const float StepSeconds = .045f;
    /// <summary>A target's angular half-size counts for at most this many degrees.</summary>
    public const float MaxSize = 8f;

    /// <summary>Something to aim at: where its middle is, and half its smallest width (metres).</summary>
    public struct Target
    {
        public Vector3 point;
        public float size;
        public Target(Vector3 point, float size) { this.point = point; this.size = size; }
    }

    /// <summary>One view's help as it goes: turning, or gliding (a settle, or a D-pad step) toward a point.</summary>
    public sealed class State
    {
        internal bool turning, gliding;
        internal Vector3 glideTo;
        internal float glideSeconds;

        /// <summary>A settle or a step is under way (checks).</summary>
        public bool Gliding => gliding;
        /// <summary>Where the glide is going.</summary>
        public Vector3 GlideTarget => glideTo;

        public void Reset() { turning = false; gliding = false; }

        /// <summary>Whether this frame needs the targets: the stick is turning the view, or has just let go.</summary>
        public bool WantsTargets(bool stickMoving) => stickMoving || turning;

        /// <summary>A D-pad step: glide to <paramref name="point"/>.</summary>
        public void StepTo(Vector3 point)
        {
            gliding = true;
            turning = false;
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

    /// <summary>How far (degrees) a target is from the middle of the view, and its angular half-size.</summary>
    public static float AngleTo(Vector3 eye, Vector2 view, Target t, out float size)
    {
        Vector3 d = t.point - eye;
        float distance = d.magnitude;
        size = distance < .01f ? MaxSize : Mathf.Min(MaxSize, Mathf.Atan2(Mathf.Max(0f, t.size), distance) * Mathf.Rad2Deg);
        return distance < .001f ? 0f : Vector3.Angle(Forward(view), d);
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

    /// <summary>
    /// The view after this frame, with the help: <paramref name="view"/> is (yaw, pitch) in degrees (pitch positive looks
    /// down), <paramref name="turn"/> the stick's own turn this frame in the same terms, <paramref name="stick"/> how far
    /// the stick is pushed (0 to 1). Call it every frame the help is active (a glide carries on with the stick still).
    /// </summary>
    public static Vector2 Steer(Vector3 eye, Vector2 view, Vector2 turn, float stick, float dt,
        IReadOnlyList<Target> targets, State state)
    {
        bool turningNow = stick > .01f && turn.sqrMagnitude > 1e-10f;
        float angle = 0f, size = 0f;
        int best = turningNow || state.turning ? Nearest(eye, view, targets, out angle, out size) : -1;

        if (turningNow)
        {
            state.gliding = false;
            state.turning = true;
            float scale = best >= 0 ? Mathf.Lerp(Friction, 1f, Mathf.InverseLerp(size, size + FrictionMargin, angle)) : 1f;
            Vector2 next = view + turn * scale;
            if (best >= 0 && angle <= size + MagnetCone)
            {
                Vector2 off = Offset(next, Angles(eye, targets[best].point));
                if (off.sqrMagnitude > 1e-8f && Vector2.Dot(turn.normalized, off.normalized) > -.3f)
                    next += Vector2.ClampMagnitude(off, MagnetRate * stick * dt);
            }
            return next;
        }

        if (state.turning)
        {
            // The stick has just let go: near a target, settle onto it.
            state.turning = false;
            if (best >= 0 && angle <= size + SettleCone)
            {
                state.gliding = true;
                state.glideTo = targets[best].point;
                state.glideSeconds = SettleSeconds;
            }
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
