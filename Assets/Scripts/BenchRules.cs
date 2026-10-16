using System;

// ---------------------------------------------------------------------------
// THE FORGIVING BENCH'S RULES, AS PLAIN MATHS (14 Oct 2026; specs/systems-forgiving-bench.md §7 step 1)
//
// His playtest 5 (14 Oct, 00:30): "it was so hard to get the camera perfectly repaired, i stayed all day on the repair ...
// moving it and putting them on the tray literally seems sooo hard, i kept dropping a part of the camera under the camera".
// Everything the bench now decides by number lives here, with no Unity in it, so the console tests (Tests/BenchRules) and
// the editor's menu check (Fixit Fidget > Checks > Bench rules) prove the same numbers the game runs:
//   * the hit areas (§4.6): screws and holes within max(28 px, the head + 10 px); covers, parts, dirt and tools within their
//     outline + 12-16 px; a normalised distance (0 at the middle, 1 at the edge) and four priority classes;
//   * the flight every freed piece takes to its place (§4.0.2): a quadratic arc, its length in screen pixels setting its
//     time, an ease with a quick launch and a gentle landing, a hop on landing;
//   * the slot rule (§4.0.1): a place is free for a piece when it lies clear of every resting quarter turn of its device;
//   * the press pass (§4.4.4): one full pass of the tool over a speck takes a fixed share of what was painted there, at any
//     frame rate, speed or zoom (the chord of the frame's path inside the speck's circle, over the whole chord); grease
//     smears first; at 90 % the next pass finishes the spot (§4.4.5);
//   * the stroke speed the sounds, the cloth and the rumble follow (Audio §7.1).
// Pixels are 1080p reference pixels throughout (multiply by Screen.height / 1080 for real ones). Seconds are game seconds.
// ---------------------------------------------------------------------------
public static class BenchRules
{
    // ================================================================== hit areas and priority (§4.6)

    /// <summary>A screw or an empty hole is hit within this many reference pixels of its middle, at least.</summary>
    public const float ScrewHitPx = 28f;
    /// <summary>...or within its projected head radius plus this, when that is bigger.</summary>
    public const float ScrewHeadMarginPx = 10f;
    /// <summary>The margins round an outline: a cover, a part (broken or fresh), a dirt spot, a tool at rest.</summary>
    public const float CoverMarginPx = 12f, PartMarginPx = 14f, SpotMarginPx = 16f, ToolMarginPx = 16f;
    /// <summary>A work normal this far from facing away still counts as facing the camera (cos 100°).</summary>
    public const float FacingDot = -.17f;

    /// <summary>A screw's (or a hole's) hit radius, from its head's projected radius (reference pixels).</summary>
    public static float ScrewHitRadius(float projectedHeadRadiusPx) => Math.Max(ScrewHitPx, projectedHeadRadiusPx + ScrewHeadMarginPx);

    /// <summary>The normalised distance from a point to a circle's hit area: 0 at its middle, 1 at its edge.</summary>
    public static float CircleDistance(float px, float py, float cx, float cy, float hitRadius)
    {
        if (!(hitRadius > 0f)) return float.PositiveInfinity;
        float dx = px - cx, dy = py - cy;
        return (float)Math.Sqrt(dx * dx + dy * dy) / hitRadius;
    }

    /// <summary>
    /// The convex hull of <paramref name="count"/> points (Andrew's monotone chain), anticlockwise, into the out arrays (each at
    /// least count long). Returns how many corners it has (0 for no points; a single point or a line folds to 1 or 2).
    /// </summary>
    public static int ConvexHull(float[] xs, float[] ys, int count, float[] hx, float[] hy)
    {
        if (count <= 0) return 0;
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        Array.Sort(order, (a, b) => xs[a] != xs[b] ? xs[a].CompareTo(xs[b]) : ys[a].CompareTo(ys[b]));
        var cx = new float[count * 2];
        var cy = new float[count * 2];
        int k = 0;
        for (int n = 0; n < count; n++)
        {
            int i = order[n];
            while (k >= 2 && Cross(cx[k - 2], cy[k - 2], cx[k - 1], cy[k - 1], xs[i], ys[i]) <= 0f) k--;
            cx[k] = xs[i]; cy[k] = ys[i]; k++;
        }
        for (int n = count - 2, lower = k + 1; n >= 0; n--)
        {
            int i = order[n];
            while (k >= lower && Cross(cx[k - 2], cy[k - 2], cx[k - 1], cy[k - 1], xs[i], ys[i]) <= 0f) k--;
            cx[k] = xs[i]; cy[k] = ys[i]; k++;
        }
        int corners = Math.Max(1, k - 1);
        if (count == 1) corners = 1;
        for (int i = 0; i < corners; i++) { hx[i] = cx[i]; hy[i] = cy[i]; }
        return corners;
    }

    static float Cross(float ox, float oy, float ax, float ay, float bx, float by) => (ax - ox) * (by - oy) - (ay - oy) * (bx - ox);

    /// <summary>
    /// The normalised distance from a point to a convex outline grown by <paramref name="margin"/> pixels: 0 at the outline's
    /// middle (its centroid, deep inside), 1 on the grown edge, over 1 outside it. Monotonic in how far inside or outside the
    /// point is, so the nearer of two overlapping outlines is the one whose middle the cursor is nearer to.
    /// </summary>
    public static float HullDistance(float px, float py, float[] hx, float[] hy, int corners, float margin)
    {
        if (corners <= 0) return float.PositiveInfinity;
        float mx = 0f, my = 0f;
        for (int i = 0; i < corners; i++) { mx += hx[i]; my += hy[i]; }
        mx /= corners; my /= corners;
        float edge = SignedDistance(px, py, hx, hy, corners);       // negative inside
        float inset = Math.Max(1f, -SignedDistance(mx, my, hx, hy, corners));   // how deep the middle lies
        margin = Math.Max(1f, margin);
        return Math.Max(0f, (edge + inset) / (margin + inset));
    }

    /// <summary>The signed distance from a point to a convex outline (anticlockwise corners): negative inside.</summary>
    public static float SignedDistance(float px, float py, float[] hx, float[] hy, int corners)
    {
        if (corners == 1) return Hypot(px - hx[0], py - hy[0]);
        float nearest = float.PositiveInfinity;
        bool inside = corners >= 3;
        for (int i = 0; i < corners; i++)
        {
            int j = (i + 1) % corners;
            float ax = hx[i], ay = hy[i], bx = hx[j], by = hy[j];
            float ex = bx - ax, ey = by - ay;
            float len2 = ex * ex + ey * ey;
            float t = len2 > 1e-12f ? Clamp01(((px - ax) * ex + (py - ay) * ey) / len2) : 0f;
            nearest = Math.Min(nearest, Hypot(px - (ax + ex * t), py - (ay + ey * t)));
            if (Cross(ax, ay, bx, by, px, py) < 0f) inside = false;     // to the right of an anticlockwise edge: outside
        }
        return inside ? -nearest : nearest;
    }

    /// <summary>
    /// The four priority classes, best first (§4.6): 1 the current step's targets the tool in hand can act on; 2 other
    /// targets it can act on; 3 the current step's targets it can't (the hover says what is needed); 4 everything else.
    /// </summary>
    public static int PriorityClass(bool currentStepTarget, bool toolCanAct)
        => currentStepTarget && toolCanAct ? 1 : toolCanAct ? 2 : currentStepTarget ? 3 : 4;

    /// <summary>True when a candidate (class, normalised distance) beats the best so far.</summary>
    public static bool Beats(int candidateClass, float candidateDistance, int bestClass, float bestDistance)
        => candidateClass != bestClass ? candidateClass < bestClass : candidateDistance < bestDistance;

    // ================================================================== the flight (§4.0.2)

    /// <summary>The settle hop after a landing, seconds.</summary>
    public const float HopSeconds = .11f;
    /// <summary>The arc's rise as a share of the flat distance it covers (a 58° look sees it as an arc).</summary>
    public const float ArcRise = .35f;
    /// <summary>A screw's tumbles along its path; the tumble blends into the landing over the path's last quarter.</summary>
    public const float ScrewTumbles = 1.5f, TumbleFadeFrom = .75f;
    /// <summary>A broken part's wobble in flight, degrees, fading over the flight.</summary>
    public const float PartWobbleDegrees = 15f;
    /// <summary>A tool's flight home, seconds (§4.4.1).</summary>
    public const float ToolHomeSeconds = .2f;

    // ================================================================== the holds (§4.2-4.3)

    /// <summary>The fresh part's press-in, the cover's "close it anyway" hold, and how fast a let-go pinch or press decays.</summary>
    public const float PressInSeconds = .35f, CloseAnywaySeconds = .4f, KeptProgressDecay = .5f;
    /// <summary>The press-in's last share of travel is a snap (the part descends the rest over the hold).</summary>
    public const float PressSnapShare = .15f;

    /// <summary>
    /// A flight's time from its on-screen length (reference pixels): 0.24 s + 0.25 ms a pixel, 0.30-0.55 s; a screw's is
    /// quicker, 0.20 s + 0.25 ms a pixel, 0.22-0.35 s (the feel sheet's T1: a screw's flight is 0.35 s at most).
    /// </summary>
    public static float FlightSeconds(float screenPx, bool screw)
    {
        if (float.IsNaN(screenPx) || screenPx < 0f) screenPx = 0f;
        return screw ? Clamp(.20f + .00025f * screenPx, .22f, .35f) : Clamp(.24f + .00025f * screenPx, .30f, .55f);
    }

    /// <summary>The fetch of a screw from its cup back to its hole: four fifths of its flight, never under 0.22 s.</summary>
    public static float FetchSeconds(float screwFlightSeconds) => Math.Max(.22f, screwFlightSeconds * .8f);

    /// <summary>The flight's ease, 0..1 over 0..1: a quick launch and a gentle landing.</summary>
    public static float Ease(float t)
    {
        t = Clamp01(t);
        float u = 1f - t;
        return .35f * t + .65f * (1f - u * u * u);
    }

    /// <summary>The quadratic Bézier's weights at u for the launch point, the raised middle and the landing point.</summary>
    public static void BezierWeights(float u, out float w0, out float w1, out float w2)
    {
        u = Clamp01(u);
        w0 = (1f - u) * (1f - u);
        w1 = 2f * u * (1f - u);
        w2 = u * u;
    }

    /// <summary>
    /// How high the control point rises over the launch-landing midpoint for a flat distance (metres): ArcRise of it, as the
    /// spec words it (the arc itself peaks at half that, a quadratic Bézier's way).
    /// </summary>
    public static float ControlRise(float flatDistance) => ArcRise * Math.Max(0f, flatDistance);

    /// <summary>How much of a screw's tumble is left at u: all of it until the path's last quarter, then none at the landing.</summary>
    public static float TumbleLeft(float u) => 1f - SmoothStep(TumbleFadeFrom, 1f, u);

    /// <summary>The settle hop's height, 0..1, s seconds after landing (up and down over HopSeconds).</summary>
    public static float Hop(float s) => s <= 0f || s >= HopSeconds ? 0f : (float)Math.Sin(Math.PI * s / HopSeconds);

    // ================================================================== the slot rule (§4.0.1)

    /// <summary>A box on the mat: its middle and half-sizes, millimetres in Art's frame (+x the player's right, +y away).</summary>
    public struct MatBox
    {
        public float x, y, hx, hy;
        public MatBox(float x, float y, float hx, float hy) { this.x = x; this.y = y; this.hx = Math.Abs(hx); this.hy = Math.Abs(hy); }
        /// <summary>The same box turned a quarter about its own middle.</summary>
        public MatBox Quarter => new MatBox(x, y, hy, hx);
        public override string ToString() => $"({x:0},{y:0}) ±({hx:0},{hy:0})";
    }

    /// <summary>The margin every place keeps from its device's resting footprints, millimetres.</summary>
    public const float SlotMarginMm = 6f;

    /// <summary>True when two boxes overlap once one of them is grown by <paramref name="margin"/>.</summary>
    public static bool Overlaps(MatBox a, MatBox b, float margin)
        => Math.Abs(a.x - b.x) < a.hx + b.hx + margin && Math.Abs(a.y - b.y) < a.hy + b.hy + margin;

    /// <summary>
    /// A place is free for a piece when the piece lying there is clear (by SlotMarginMm) of all four resting quarter turns of
    /// its device: the device turns about its own middle, so its quarter turns are its footprint and that footprint turned.
    /// </summary>
    public static bool ClearOfDevice(MatBox pieceAtSlot, MatBox deviceAtRest)
        => !Overlaps(pieceAtSlot, deviceAtRest, SlotMarginMm) && !Overlaps(pieceAtSlot, deviceAtRest.Quarter, SlotMarginMm);

    /// <summary>True when a piece's footprint fits inside a bay (both lying the same way).</summary>
    public static bool Fits(MatBox piece, float bayHalfX, float bayHalfY) => piece.hx <= bayHalfX + .5f && piece.hy <= bayHalfY + .5f;

    // ================================================================== the press pass (§4.4.4) and the clean (§4.4.5)

    /// <summary>The brush's and the cloth's clean radius as a share of the spot's radius, and the share of what was painted a
    /// full pass takes at the footprint's core.</summary>
    public const float BrushK = .75f, ClothK = 1f, BrushStrength = 1f, ClothStrength = .8f;
    /// <summary>The tools' contact widths (the brush's chisel; Art's held cloth pad), metres, and the share of it the clean
    /// radius may reach (the cleaned core of a stroke stays narrower than the tool that made it).</summary>
    public const float BrushContact = .024f, ClothContact = .045f, ContactCap = .6f;
    /// <summary>The clean radius never shrinks under this many reference pixels on screen.</summary>
    public const float CleanFloorPx = 10f;
    /// <summary>Smear first (grease): a pass over a speck not yet smeared takes this share of the normal amount, and raises
    /// its smear by SmearGain times the pass (so half a pass smears it fully).</summary>
    public const float SmearShare = .15f, SmearGain = 2f;
    /// <summary>The 90 % finisher: past ArmAt the spot is armed; it finishes once the footprint's middle has travelled this
    /// share of the spot's diameter over it, or after this many seconds of a pad's rub in place.</summary>
    public const float ArmAt = .9f, FinishPathShare = .6f, FinishPadSeconds = .4f;
    /// <summary>The pad's in-place rub: the footprint orbits the cursor at this share of its clean radius, this many turns a second.</summary>
    public const float PadOrbitShare = .35f, PadOrbitTurns = 2f;
    /// <summary>A pad's cursor counts as held still on dirt after this long without moving (one frame isn't still: 15 Oct).</summary>
    public const float PadStillSeconds = .15f;
    /// <summary>
    /// A pad's rub reaches this much further than the mouse's (its clean radius, as Wide Brush widens it; the contact cap
    /// stays). 15 Oct, Bench 8 on his Mac: a thumb waggling the stick over the camera's film guide never cleaned it (160 s,
    /// "wasn't clean after 15 s of scrubbing" nine times): a stick's stroke is a line through the middle, the brush's
    /// 0.75 x the spot's radius never reached its edges, so it stalled under the 90 % finisher. The pad's rub held still
    /// already reached about this far (its orbit, 0.35 of the radius out); now its strokes do too.
    /// </summary>
    public const float PadRubReach = 1.35f;

    /// <summary>A spot's radius from its patch's two scales (metres): the geometric mean.</summary>
    public static float SpotRadius(float scaleX, float scaleZ) => .5f * (float)Math.Sqrt(Math.Abs(scaleX * scaleZ));

    /// <summary>
    /// The tool's clean radius on a spot of radius R (metres): k × R (the Wide Brush upgrade multiplies k), never under the
    /// on-screen floor (passed in metres) and never over 0.6 × the tool's contact width.
    /// </summary>
    public static float CleanRadius(float spotRadius, bool cloth, float upgrade, float floorMetres)
    {
        float k = (cloth ? ClothK : BrushK) * Math.Max(1f, upgrade);
        float cap = ContactCap * (cloth ? ClothContact : BrushContact);
        float r = k * Math.Max(0f, spotRadius);
        r = Math.Max(r, Math.Max(0f, floorMetres));
        return Math.Min(r, cap);
    }

    /// <summary>The falloff across the footprint: 1 inside 0.6 r, falling to 0 at r.</summary>
    public static float Falloff(float d, float r)
    {
        if (!(r > 0f) || d >= r) return 0f;
        float core = .6f * r;
        return d <= core ? 1f : 1f - (d - core) / (r - core);
    }

    /// <summary>
    /// How much of one full pass a segment of the footprint's path gives a speck: the length of the segment inside the speck's
    /// r-circle over the length of the whole line inside it (0..1). <paramref name="along"/> is where the speck's foot lies
    /// along the segment from its start (metres, may be outside 0..length), <paramref name="d"/> its distance from the line.
    /// Summed over a straight path's frames it is exactly 1 for every speck the path crosses: at any frame rate or speed.
    /// </summary>
    public static float ChordShare(float along, float d, float r, float segmentLength)
    {
        if (!(r > 0f) || d >= r || !(segmentLength > 0f)) return 0f;
        float h = (float)Math.Sqrt(r * r - d * d);       // half the full chord
        float a = Math.Max(0f, along - h), b = Math.Min(segmentLength, along + h);
        return b <= a ? 0f : (b - a) / (2f * h);
    }

    /// <summary>
    /// A share w of a pass over a speck whose smear is <paramref name="smear"/> (0..1): the part spent smearing (taking only
    /// SmearShare) and the part that cleans at the full rate, split where the smear reaches 1, so one long frame and four
    /// short ones leave the same speck.
    /// </summary>
    public static void SmearSplit(float smear, float w, out float smearing, out float cleaning)
    {
        w = Math.Max(0f, w);
        float toFull = Math.Max(0f, 1f - Clamp01(smear)) / SmearGain;
        smearing = Math.Min(w, toFull);
        cleaning = w - smearing;
    }

    /// <summary>The share of a speck's painted dirt a pass-share takes, given the tool's strength and its smear split.</summary>
    public static float Removed(float strength, float smearing, float cleaning) => strength * (SmearShare * smearing + cleaning);

    /// <summary>True once an armed spot is finished: by the footprint's travel over it, or a pad's rub in place. The travel
    /// has a hair's tolerance (one part in ten thousand) so the boundary itself counts on every runtime: Unity's Mono may keep
    /// the product in double precision, and exactly 0.6 of the diameter then read as just short (15 Oct, Bench rules).</summary>
    public static bool Finished(float pathOverSpot, float spotDiameter, float padRubSeconds)
        => pathOverSpot >= FinishPathShare * spotDiameter * .9999f || padRubSeconds >= FinishPadSeconds;

    // ================================================================== the stroke speed and what follows it (Audio §7.1-7.2)

    /// <summary>The stroke speed's smoothing time constant, seconds.</summary>
    public const float SpeedSmoothing = .06f;

    /// <summary>One frame of the stroke speed, in closed form (15 fps and 60 fps agree).</summary>
    public static float SmoothSpeed(float s, float raw, float dt)
    {
        if (!(dt > 0f)) return s;
        return s + (raw - s) * (1f - (float)Math.Exp(-dt / SpeedSmoothing));
    }

    /// <summary>The rub loop's volume for a stroke speed: silent under 40 px/s, full at 1,000.</summary>
    public static float RubVolume(float s) => (float)Math.Pow(Clamp01((s - 40f) / 960f), .6);
    /// <summary>The rub loop's pitch for a stroke speed.</summary>
    public static float RubPitch(float s) => .92f + .16f * Clamp01(s / 1200f);
    /// <summary>The cloth's Bunch key for a stroke speed; the brush's Bend.</summary>
    public static float Bunch(float s) => Clamp01((s - 60f) / 840f);
    public static float Bend(float s) => Clamp01(s / 700f);
    /// <summary>The device's budge as a share of its 6 px for a stroke speed.</summary>
    public static float BudgeShare(float s) => Clamp01(s / 900f);
    /// <summary>The squeak's stroke reversal: the smoothed direction turns back on itself.</summary>
    public static bool Reversed(float vx, float vy, float px, float py)
        => vx * px + vy * py < -.3f * Hypot(vx, vy) * Hypot(px, py);

    // ================================================================== small helpers

    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : (float.IsNaN(v) ? 0f : v);
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    public static float Hypot(float x, float y) => (float)Math.Sqrt(x * x + y * y);
    public static float SmoothStep(float from, float to, float x)
    {
        float t = to == from ? (x >= to ? 1f : 0f) : Clamp01((x - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    /// <summary>A critically damped spring toward 0, solved for the frame (ω = w): the value and its rate after dt.</summary>
    public static void Spring(ref float x, ref float v, float w, float dt)
    {
        if (!(dt > 0f)) return;
        float c2 = v + w * x;
        float decay = (float)Math.Exp(-w * dt);
        float drift = x + c2 * dt;
        x = drift * decay;
        v = (c2 - w * drift) * decay;
    }

#if UNITY_EDITOR || BENCH_RULES_TESTS
    // ================================================================== the self-check (console: Tests/BenchRules; editor: Fixit Fidget > Checks > Bench rules)

    static int assertions;

    /// <summary>Proves the rules above; throws on the first that fails. Returns the number of assertions.</summary>
    public static int SelfCheck()
    {
        assertions = 0;

        // ---- hit areas
        Check(ScrewHitRadius(4f) == 28f && ScrewHitRadius(25f) == 35f, "A screw's hit radius is max(28 px, the head + 10 px).");
        Check(Near(CircleDistance(14f, 0f, 0f, 0f, 28f), .5f) && CircleDistance(0f, 0f, 0f, 0f, 28f) == 0f, "A circle's distance is 0 at its middle and 1 at its edge.");
        float[] xs = { 0f, 100f, 100f, 0f, 50f }, ys = { 0f, 0f, 60f, 60f, 30f };
        var hx = new float[10]; var hy = new float[10];
        int corners = ConvexHull(xs, ys, 5, hx, hy);
        Check(corners == 4, "The hull of a rectangle and its middle has the rectangle's four corners.");
        Check(SignedDistance(50f, 30f, hx, hy, corners) < -29f && Near(SignedDistance(110f, 30f, hx, hy, corners), 10f), "Signed distance: negative inside, the gap outside.");
        float middle = HullDistance(50f, 30f, hx, hy, corners, 12f), edge = HullDistance(112f, 30f, hx, hy, corners, 12f), beyond = HullDistance(130f, 30f, hx, hy, corners, 12f);
        Check(middle < .01f && Near(edge, 1f, .01f) && beyond > 1f, $"A hull's distance: 0 at its middle ({middle:0.000}), 1 on the edge grown by its margin ({edge:0.000}), over 1 beyond ({beyond:0.000}).");
        Check(HullDistance(95f, 30f, hx, hy, corners, 12f) < HullDistance(105f, 30f, hx, hy, corners, 12f), "Nearer the middle is always nearer.");
        Check(PriorityClass(true, true) == 1 && PriorityClass(false, true) == 2 && PriorityClass(true, false) == 3 && PriorityClass(false, false) == 4, "The four priority classes.");
        Check(Beats(1, .9f, 2, .1f) && !Beats(2, .1f, 1, .9f) && Beats(2, .2f, 2, .3f), "A better class beats a nearer one; within a class the nearer wins.");

        // ---- the flight
        Check(Near(FlightSeconds(300f, false), .315f) && Near(FlightSeconds(600f, false), .39f) && Near(FlightSeconds(900f, false), .465f), "Flight time: 300 px 0.32 s, 600 px 0.39 s, 900 px 0.47 s.");
        Check(FlightSeconds(0f, false) == .30f && FlightSeconds(5000f, false) == .55f, "Flights are 0.30-0.55 s.");
        Check(Near(FlightSeconds(300f, true), .275f) && Near(FlightSeconds(500f, true), .325f) && FlightSeconds(5000f, true) == .35f && FlightSeconds(0f, true) == .22f, "Screws fly in 0.22-0.35 s (300 px 0.28 s, 500 px 0.33 s).");
        Check(Near(FetchSeconds(.3f), .24f) && FetchSeconds(.22f) == .22f, "A fetch takes four fifths of the screw's flight, at least 0.22 s.");
        Check(Ease(0f) == 0f && Near(Ease(1f), 1f) && Ease(.5f) > .5f, "The ease starts at 0, ends at 1 and launches quickly.");
        for (int i = 1; i <= 20; i++) Check(Ease(i / 20f) > Ease((i - 1) / 20f), "The ease always moves forward.");
        BezierWeights(.5f, out float w0, out float w1, out float w2);
        Check(Near(w0 + w1 + w2, 1f) && Near(w1 * ControlRise(1f), ArcRise * .5f), "The control point rises 0.35 of the flat distance; the arc peaks at half that.");
        Check(TumbleLeft(.5f) == 1f && TumbleLeft(1f) == 0f, "A screw's tumble is gone by its landing.");
        Check(Hop(0f) == 0f && Hop(HopSeconds) == 0f && Near(Hop(HopSeconds * .5f), 1f), "The hop goes up and comes down.");
        // The pose is a function of elapsed time: 15 fps and 60 fps meet the same pose at the same time.
        float t15 = 0f, t60 = 0f;
        for (int f = 0; f < 4; f++) t15 += 1f / 15f;
        for (int f = 0; f < 16; f++) t60 += 1f / 60f;
        Check(Near(Ease(t15 / .4f), Ease(t60 / .4f), 1e-4f), "15 fps and 60 fps reach the same point of a flight at the same moment.");

        // ---- the slot rule (Art's layout: the phone at (-30, -14), 72 x 148; the cups at x 36 and 60)
        var phone = new MatBox(-30f, -14f, 36f, 74f);
        var cup1 = new MatBox(36f, -24f, 9f, 9f);
        var cup4 = new MatBox(60f, -24f, 9f, 9f);
        Check(!ClearOfDevice(cup1, phone) && ClearOfDevice(cup4, phone), "The quarter-turned phone covers the left cup column; the right one is clear (its screws take cups 4-6).");
        var watch = new MatBox(-20f, 0f, 25f, 32.5f);
        Check(ClearOfDevice(cup1, watch), "The watch leaves every cup free.");
        Check(Fits(new MatBox(0f, 0f, 34f, 69f), 40f, 83f) && !Fits(new MatBox(0f, 0f, 65f, 65f), 40f, 83f), "A cover fits its well, or doesn't.");

        // ---- the press pass: one full pass over a speck takes exactly s × f, whatever the frames
        float r = .006f;
        for (int pieces = 1; pieces <= 9; pieces += 4)
        {
            float d = .002f, total = 0f, length = .04f;
            for (int k = 0; k < pieces; k++)
            {
                float segment = length / pieces;
                float along = .02f - k * segment;     // the speck's foot lies 2 cm along the whole path
                total += ChordShare(along, d, r, segment);
            }
            Check(Near(total, 1f, 1e-4f), $"A straight pass in {pieces} frame(s) gives the speck exactly one pass ({total:0.0000}).");
        }
        Check(ChordShare(0f, .007f, r, .04f) == 0f && ChordShare(.02f, 0f, r, 0f) == 0f, "Nothing outside the footprint, nothing for a still tool.");
        Check(Falloff(0f, 1f) == 1f && Falloff(.6f, 1f) == 1f && Near(Falloff(.8f, 1f), .5f) && Falloff(1f, 1f) == 0f, "The falloff: 1 inside 0.6 r, 0 at r.");
        // Smear first: one long frame and four short ones leave a speck the same.
        float smearA = 0f, takenA = 0f;
        SmearSplit(smearA, 1f, out float sm, out float cl); takenA += Removed(ClothStrength, sm, cl); smearA = Math.Min(1f, smearA + SmearGain * sm);
        float smearB = 0f, takenB = 0f;
        for (int k = 0; k < 4; k++) { SmearSplit(smearB, .25f, out sm, out cl); takenB += Removed(ClothStrength, sm, cl); smearB = Math.Min(1f, smearB + SmearGain * sm); }
        Check(Near(takenA, takenB, 1e-5f) && Near(smearA, 1f) && Near(smearB, 1f), $"Smear first is frame-rate independent ({takenA:0.000} vs {takenB:0.000} of the speck).");
        Check(takenA < ClothStrength && takenA > .3f, "The first pass over grease smears more than it clears.");
        // The widths (§4.4.5's table): the lens 9.6 mm under the cloth, the phone's grime 13.5 mm under the brush, the cap.
        Check(Near(CleanRadius(.0096f, true, 1f, 0f), .0096f) && Near(CleanRadius(.018f, false, 1f, 0f), .0135f), "Clean radius: the lens 9.6 mm (cloth), the phone's grime 13.5 mm (brush).");
        Check(Near(CleanRadius(.05f, false, 1f, 0f), .0144f) && Near(CleanRadius(.001f, false, 1f, .003f), .003f), "The brush is capped at 14.4 mm; the on-screen floor holds.");
        Check(CleanRadius(.008f, false, 1.25f, 0f) > CleanRadius(.008f, false, 1f, 0f), "Wide Brush widens the stroke.");
        Check(!Finished(.5f * .01f, .01f, 0f) && Finished(.6f * .01f, .01f, 0f) && Finished(0f, .01f, .4f), "The finisher: 0.6 of the diameter of travel, or 0.4 s of a pad's rub.");

        // ---- the stroke speed and its sounds
        float s = 0f;
        for (int f = 0; f < 30; f++) s = SmoothSpeed(s, 600f, 1f / 60f);
        float s15 = 0f;
        for (int f = 0; f < 8; f++) s15 = SmoothSpeed(s15, 600f, 1f / 15f);
        Check(Near(s, 600f, 1f) && Near(s15, 600f, 1f), "The stroke speed settles to the hand's speed at 60 and at 15 fps.");
        Check(RubVolume(30f) == 0f && Near(RubVolume(1000f), 1f) && RubPitch(0f) == .92f && Near(RubPitch(1200f), 1.08f), "The rub's volume and pitch follow the speed.");
        Check(Reversed(1f, 0f, -1f, .1f) && !Reversed(1f, 0f, 1f, .2f), "A stroke turning back is a reversal; one carrying on is not.");
        float x = 1f, v = 0f, x15 = 1f, v15 = 0f;
        for (int f = 0; f < 18; f++) Spring(ref x, ref v, 20f, 1f / 60f);
        for (int f = 0; f < 5; f++) Spring(ref x15, ref v15, 20f, .3f / 5f);
        Check(Math.Abs(x) < .02f && x >= -1e-3f && Math.Abs(x - x15) < 1e-4f, "The budge springs back within 0.3 s, without a bounce, the same at 60 and 15 fps.");
        return assertions;
    }

    static bool Near(float a, float b, float within = 1e-3f) => Math.Abs(a - b) <= within;

    static void Check(bool ok, string what)
    {
        assertions++;
        if (!ok) throw new InvalidOperationException("Bench rules FAILED: " + what);
    }
#endif
}
