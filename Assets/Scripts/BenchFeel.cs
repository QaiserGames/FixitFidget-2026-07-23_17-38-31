using UnityEngine;

// ---------------------------------------------------------------------------
// FEELING THE IMPACT (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.4.3; audio-bench-sounds.md §7.1-7.2)
//
// His note (14 Oct, 00:30): "when i point my crosshair at the point of the cloth, it should just grab and i just drag it across
// an object and for it to feel the impact". His decision 4 reads "feel the impact" as all three of: the look (the cloth presses
// and bunches toward the stroke, the brush's bristles bend), the sound and rumble (a rub that follows the hand's speed, a pad
// rumble), and the device budging a few millimetres under the hand.
//
// One number drives all of it: the stroke speed s, the pointer's speed on screen in 1080p reference pixels a second while the
// tool is pressed (Audio's s, verbatim), plus the pad's in-place scrub rate while a stick rubs still; smoothed in closed form
// (60 ms), so 15 fps and 60 fps agree. From s:
//   * the rub loops (brush.rub, cloth.rub): volume (silent under 40 px/s, full at 1,000), pitch, the brush's grit only while
//     dirt comes off; on at the contact (60 ms fade), off 0.12 s after the release or leaving the device;
//   * the squeak (cloth.squeak): the stroke turns back on itself at 250 px/s or more while grease is coming off;
//   * the cloth's Bunch and the brush's Bend (ToolPickup reads s);
//   * the budge: the device shifts up to 6 px on screen along the stroke, critically damped back (ItemInspector.Pose adds it);
//   * the pad's rumble while rubbing, and short pulses on the moments (a screw free, a cover's pop, a press-in's snap, a spot
//     clean, a tool grabbed). Off with Settings' new Vibration row, at a pause, on focus loss and when the close-up ends.
// ---------------------------------------------------------------------------
public static class BenchFeel
{
    /// <summary>s: the stroke's speed, 1080p reference pixels a second, smoothed.</summary>
    public static float StrokeSpeed { get; private set; }
    /// <summary>The stroke's smoothed velocity on screen (reference pixels a second; the pad's in-place rub has none).</summary>
    public static Vector2 StrokeVelocity { get; private set; }
    /// <summary>The device's budge now (world metres; ItemInspector adds it to the device's rest point).</summary>
    public static Vector3 BudgeOffset { get; private set; }
    /// <summary>The rub loop's volume and pitch now (labs: Audio §8 logs s against the volume).</summary>
    public static float RubLoudness { get; private set; }
    public static float RubPitchNow { get; private set; } = 1f;
    /// <summary>The rub loop is playing (labs).</summary>
    public static bool RubPlaying => loop != null && loop.Alive;
    /// <summary>The motors' speeds asked for now (labs).</summary>
    public static Vector2 RumbleNow { get; private set; }

    /// <summary>The budge's reach (reference pixels on screen at full speed), its spring and the squeak's floor.</summary>
    public const float BudgePx = 6f, BudgeOmega = 20f, SqueakSpeed = 250f, CameOffWindow = .15f, LoopStopSeconds = .12f;

    static SfxLoop loop;
    static ToolType loopTool;
    static float dirtOffAt = -10f, greaseOffAt = -10f;
    static Vector2 strokeDir;                 // the direction of the stroke under way (for the reversal)
    static float lastSqueakAt = -10f;
    static Vector3 budgeVelocity;
    static float pulseLow, pulseHigh, pulseUntil = -1f;
    static bool rubbingNow;
    static float rubLow, rubHigh;
    static bool rumbling;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        StrokeSpeed = 0f; StrokeVelocity = Vector2.zero; BudgeOffset = Vector3.zero; RubLoudness = 0f; RubPitchNow = 1f; RumbleNow = Vector2.zero;
        loop = null; dirtOffAt = greaseOffAt = lastSqueakAt = -10f; strokeDir = Vector2.zero; budgeVelocity = Vector3.zero;
        pulseUntil = -1f; rubbingNow = false; rumbling = false;
    }

    // ---------- s ----------

    /// <summary>
    /// One frame of the pointer (call every frame of the close-up): its movement this frame (pixels) and whether the pad is
    /// rubbing in place (its scrub rate, reference pixels a second, then counts as speed).
    /// </summary>
    public static void Sample(Vector2 pointerDelta, float dt, bool padRubbingInPlace, float padScrubRate)
    {
        if (!(dt > 0f)) return;
        float toRef = 1080f / Mathf.Max(1, Screen.height);
        Vector2 velocity = pointerDelta / dt * toRef;
        float raw = velocity.magnitude + (padRubbingInPlace ? padScrubRate * toRef : 0f);
        StrokeSpeed = BenchRules.SmoothSpeed(StrokeSpeed, raw, dt);
        float k = 1f - Mathf.Exp(-dt / BenchRules.SpeedSmoothing);
        StrokeVelocity += (velocity - StrokeVelocity) * k;
    }

    // ---------- the rub: the loop, the squeak, the rumble ----------

    /// <summary>
    /// One frame of the cloth or the brush in the hand. <paramref name="contact"/>: pressed on the device over its own dirt;
    /// <paramref name="cameOff"/>: dirt (or grease) came off under it this frame.
    /// </summary>
    public static void Rubbing(ToolType tool, Transform tip, bool contact, bool cameOff, float dt)
    {
        float now = Time.time;
        bool grease = tool == ToolType.Cloth;
        if (cameOff) { if (grease) greaseOffAt = now; else dirtOffAt = now; }
        bool recently = now - (grease ? greaseOffAt : dirtOffAt) < CameOffWindow;
        string cue = grease ? "cloth.rub" : "brush.rub";

        if (contact)
        {
            // On at the contact (a new tool, or a loop fading out, starts a fresh one).
            if (loop == null || !loop.Alive || loopTool != tool)
            {
                if (loop != null && loop.Alive) loop.Stop(LoopStopSeconds);
                loop = Sfx.Loop(cue, tip, 0f);          // null when the cue has no file (Audio §7.1): guarded below
                loopTool = tool;
                if (loop != null) loop.FadeSeconds = .06f;
            }
            float v = BenchRules.RubVolume(StrokeSpeed);
            if (!grease) v *= recently ? 1f : .55f;      // the brush's grit only while dirt comes off
            RubLoudness = v;
            RubPitchNow = BenchRules.RubPitch(StrokeSpeed);
            if (loop != null)
            {
                loop.Volume = v;
                if (loop.Source != null) loop.Source.pitch = RubPitchNow;
            }
            // The squeak (Audio §7.2): the stroke turns back on itself, briskly, while grease is coming off.
            Vector2 vel = StrokeVelocity;
            if (vel.magnitude > 80f)
            {
                if (strokeDir != Vector2.zero && BenchRules.Reversed(vel.x, vel.y, strokeDir.x, strokeDir.y))
                {
                    if (grease && StrokeSpeed >= SqueakSpeed && recently && now - lastSqueakAt > .18f)
                    {
                        lastSqueakAt = now;
                        Sfx.Play("cloth.squeak", tip != null ? tip.position : Vector3.zero, .8f);
                    }
                }
                strokeDir = vel.normalized;
            }
            // The rumble while rubbing: the low motor with the speed, the high one with the grit.
            rubbingNow = true;
            rubLow = .10f + .25f * v;
            rubHigh = .05f + .30f * v * (recently ? 1f : .4f);
        }
        else NotRubbing();
    }

    /// <summary>The tool is off the dirt (or let go): the loop fades out over 0.12 s (Audio §7.1), the rumble stops at once.</summary>
    public static void NotRubbing()
    {
        rubbingNow = false;
        RubLoudness = 0f;
        if (loop != null) { loop.Stop(LoopStopSeconds); loop = null; }
    }

    // ---------- the budge ----------

    /// <summary>
    /// One frame of the device's budge: along the stroke while rubbing (up to 6 px on screen, converted at the device's depth,
    /// by s), springing back critically damped. Returns the offset (world) to add to the device's rest point. It never turns the
    /// device.
    /// </summary>
    public static Vector3 Budge(Camera cam, Vector3 devicePoint, Vector3 matUp, bool rubbing, float dt)
    {
        Vector3 target = Vector3.zero;
        if (rubbing && cam != null && StrokeVelocity.sqrMagnitude > 1f)
        {
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, matUp).normalized;
            Vector3 upOnMat = Vector3.ProjectOnPlane(cam.transform.up, matUp).normalized;
            Vector3 along = (right * StrokeVelocity.x + upOnMat * StrokeVelocity.y).normalized;
            float depth = Mathf.Max(.05f, Vector3.Dot(devicePoint - cam.transform.position, cam.transform.forward));
            float fov = cam.orthographic ? 0f : cam.fieldOfView;
            float metresPerRefPx = 2f * depth * Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / 1080f;
            target = along * (BudgePx * metresPerRefPx * BenchRules.BudgeShare(StrokeSpeed));
        }
        // Critically damped toward the target, per axis, solved for the frame (a stepped spring runs away at 15 fps: 13 Oct).
        Vector3 x = BudgeOffset - target;
        float xx = x.x, vx = budgeVelocity.x, xy = x.y, vy = budgeVelocity.y, xz = x.z, vz = budgeVelocity.z;
        BenchRules.Spring(ref xx, ref vx, BudgeOmega, dt);
        BenchRules.Spring(ref xy, ref vy, BudgeOmega, dt);
        BenchRules.Spring(ref xz, ref vz, BudgeOmega, dt);
        budgeVelocity = new Vector3(vx, vy, vz);
        BudgeOffset = target + new Vector3(xx, xy, xz);
        if (!rubbing && BudgeOffset.sqrMagnitude < 1e-12f) { BudgeOffset = Vector3.zero; budgeVelocity = Vector3.zero; }
        return BudgeOffset;
    }

    /// <summary>No budge (a flip, a new close-up).</summary>
    public static void ResetBudge() { BudgeOffset = Vector3.zero; budgeVelocity = Vector3.zero; }

    // ---------- the rumble ----------

    /// <summary>A short rumble on a moment (60 ms unless said): a screw free, a cover's pop, a press-in's snap, a clean spot.</summary>
    public static void Pulse(float low, float high, float seconds = .06f)
    {
        float until = Time.unscaledTime + Mathf.Max(.02f, seconds);
        if (Time.unscaledTime < pulseUntil) { pulseLow = Mathf.Max(pulseLow, low); pulseHigh = Mathf.Max(pulseHigh, high); pulseUntil = Mathf.Max(pulseUntil, until); }
        else { pulseLow = low; pulseHigh = high; pulseUntil = until; }
        ApplyRumble();
    }

    /// <summary>Every frame of the close-up, after the rub: the motors follow the rub and the pulses, or stop.</summary>
    public static void Tick()
    {
        ApplyRumble();
    }

    static void ApplyRumble()
    {
        float low = 0f, high = 0f;
        if (rubbingNow) { low = rubLow; high = rubHigh; }
        if (Time.unscaledTime < pulseUntil) { low = Mathf.Max(low, pulseLow); high = Mathf.Max(high, pulseHigh); }
        bool allowed = GameSettings.Vibration && PadInput.UsingPad && Time.timeScale > 0f && Application.isFocused;
        if (!allowed || low <= 0f && high <= 0f)
        {
            if (rumbling) { PadInput.StopRumble(); rumbling = false; }
            RumbleNow = Vector2.zero;
            return;
        }
        RumbleNow = new Vector2(low, high);
        PadInput.Rumble(low, high);
        rumbling = true;
    }

    /// <summary>Everything off at once: the pause, focus lost, the close-up's end.</summary>
    public static void Silence()
    {
        rubbingNow = false;
        pulseUntil = -1f;
        RubLoudness = 0f;
        if (loop != null) { loop.Stop(LoopStopSeconds); loop = null; }
        if (rumbling) { PadInput.StopRumble(); rumbling = false; }
        RumbleNow = Vector2.zero;
        StrokeSpeed = 0f;
        StrokeVelocity = Vector2.zero;
        strokeDir = Vector2.zero;
    }
}
