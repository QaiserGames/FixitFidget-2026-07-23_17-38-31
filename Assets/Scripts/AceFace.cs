using System;
using UnityEngine;
using Random = UnityEngine.Random;

// ---------------------------------------------------------------------------
// ACE'S FACE (29 Sept 2026: claude/break-ins-spec.md section 8, the Sidekick Ace)
//
// Ace's own body is a Synty Sidekick character, chosen because "they show more emotion with their faces".
// For now the face only lives: it blinks every few seconds (now and then twice), and the eyes make small
// glances and come back, the lids following them a little. Expressions (the morning straight face,
// getting caught) come later, with those scenes.
//
// How: the face's blend shapes, found by the end of their names (ARKit names: eyeBlinkUpper/Lower,
// eyeLookIn/Out/Up/Down, each Left and Right), and the eye bones (the Humanoid avatar's eyes, else eye_l and
// eye_r). On the Sidekick face the eyeLook shapes move only the lids; the eyeballs turn with the eye bones.
// It also holds the jaw shut: the Humanoid avatar maps a jaw, the clips have none, and Unity's neutral for
// a jaw it drives leaves the mouth hanging open; so the jaw and the eyes are put back to their modelled
// rest after the animation each frame (the eyes then glance from there).
// Nothing else on the face is touched: the body's build (the Creator's body shapes) stays as exported.
// AceBody adds this to the Sidekick body when it puts it on (with Lives off it only holds the jaw and the
// eyes still), and switches it off while the body is hidden. Apply() poses the face at once (the set-up
// step's photos use it).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent, DefaultExecutionOrder(130)]
public sealed class AceFace : MonoBehaviour
{
    [Tooltip("Blinks and glances. Off: the eyes stay open and still (the jaw is held shut either way).")]
    public bool lives = true;

    [Header("Blinks")]
    [Tooltip("Seconds between blinks: at least, at most.")]
    public Vector2 blinkEvery = new Vector2(2.5f, 5.5f);
    [Tooltip("Chance a blink comes twice.")]
    [Range(0f, 1f)] public float doubleBlinkChance = .15f;
    [Tooltip("Seconds: closing, shut, opening.")]
    public Vector3 blinkSeconds = new Vector3(.06f, .03f, .11f);
    [Tooltip("How far each lid goes when the eyes are shut (blend-shape weight, 0-100).")]
    [Range(0f, 100f)] public float upperLid = 100f, lowerLid = 50f;

    [Header("Glances")]
    [Tooltip("Seconds between glances: at least, at most.")]
    public Vector2 glanceEvery = new Vector2(.8f, 2.8f);
    [Tooltip("How far a glance goes, degrees: to the side, and up or down.")]
    public Vector2 glanceReach = new Vector2(7f, 3f);
    [Tooltip("Chance a glance comes back to straight ahead.")]
    [Range(0f, 1f)] public float backAhead = .55f;
    [Tooltip("Seconds a glance takes (eyes jump; they don't drift).")]
    [Range(.01f, .2f)] public float glanceSeconds = .05f;
    [Tooltip("How much the lids follow the eyes: blend-shape weight at a 30 degree look.")]
    [Range(0f, 100f)] public float lidsFollow = 100f;

    /// <summary>Something to move was found: the lids' blend shapes, the eye bones or the jaw.</summary>
    public bool Bound => skin != null || eyeL != null || eyeR != null || jaw != null;
    /// <summary>Blinks since it was put on (for the checks).</summary>
    public int Blinks { get; private set; }
    /// <summary>Glances since it was put on (for the checks).</summary>
    public int Glances { get; private set; }
    /// <summary>What was found, for the reports.</summary>
    public string Parts { get; private set; } = "";
    /// <summary>Where the eyes look now, degrees: x to Ace's right, y down.</summary>
    public Vector2 Gaze => gaze;

    // Blend shapes, in this order: blink upper L R, blink lower L R, look in L R, look out L R, look up L R, look down L R.
    static readonly string[] ShapeNames =
    {
        "eyeBlinkUpperLeft", "eyeBlinkUpperRight", "eyeBlinkLowerLeft", "eyeBlinkLowerRight",
        "eyeLookInLeft", "eyeLookInRight", "eyeLookOutLeft", "eyeLookOutRight",
        "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight",
    };
    const int UpperL = 0, UpperR = 1, LowerL = 2, LowerR = 3, InL = 4, InR = 5, OutL = 6, OutR = 7, UpL = 8, UpR = 9, DownL = 10, DownR = 11;

    readonly int[] shapes = new int[ShapeNames.Length];
    readonly float[] weights = new float[ShapeNames.Length];
    SkinnedMeshRenderer skin;
    Transform eyeL, eyeR, jaw, body;
    Quaternion restL, restR, restJaw;
    float nextBlink, blinkClock = -1f, nextGlance, glanceClock = -1f;
    int blinksLeft;
    bool secondBlink;
    Vector2 gaze, gazeFrom, gazeTo;

    /// <summary>Finds the face on <paramref name="bodyRoot"/> (the Sidekick body as it was put on, before it moves).</summary>
    public bool Bind(Transform bodyRoot, Animator animator)
    {
        body = bodyRoot;
        skin = null;
        for (int i = 0; i < shapes.Length; i++) { shapes[i] = -1; weights[i] = -1f; }
        if (bodyRoot != null)
            foreach (SkinnedMeshRenderer s in bodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null && Shape(s.sharedMesh, ShapeNames[UpperL]) >= 0) { skin = s; break; }
        int found = 0;
        if (skin != null)
            for (int i = 0; i < shapes.Length; i++)
                if ((shapes[i] = Shape(skin.sharedMesh, ShapeNames[i])) >= 0) found++;

        eyeL = eyeR = jaw = null;
        if (animator != null && animator.isHuman)
        {
            eyeL = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            eyeR = animator.GetBoneTransform(HumanBodyBones.RightEye);
            jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
        }
        if (eyeL == null) eyeL = Find(bodyRoot, "eye_l");
        if (eyeR == null) eyeR = Find(bodyRoot, "eye_r");
        // The rest pose: the body as it was put on, before the animation first moves it (the modelled face, mouth shut).
        if (eyeL != null) restL = eyeL.localRotation;
        if (eyeR != null) restR = eyeR.localRotation;
        if (jaw != null) restJaw = jaw.localRotation;

        Parts = $"{found} of the {ShapeNames.Length} lid shapes" + (skin != null ? $" on '{skin.name}'" : "") +
                $", eyes: {(eyeL != null ? eyeL.name : "none")} and {(eyeR != null ? eyeR.name : "none")}" +
                $", jaw held shut: {(jaw != null ? jaw.name : "none (not driven by the avatar)")}";
        gaze = gazeFrom = gazeTo = Vector2.zero;
        blinkClock = glanceClock = -1f;
        blinksLeft = 0;
        secondBlink = false;
        nextBlink = Time.time + Random.Range(.5f, Mathf.Max(.5f, blinkEvery.y));
        nextGlance = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);
        return Bound;
    }

    void LateUpdate()
    {
        if (!Bound) return;
        if (!lives)
        {
            Apply(0f, Vector2.zero);
            return;
        }
        float now = Time.time, dt = Time.deltaTime;

        // Blinks: shut quickly, a moment closed, open a little slower; now and then twice in a row.
        if (blinkClock < 0f && now >= nextBlink)
        {
            blinkClock = 0f;
            Blinks++;
            if (!secondBlink) blinksLeft = Random.value < doubleBlinkChance ? 1 : 0;
        }
        float shut = 0f;
        if (blinkClock >= 0f)
        {
            blinkClock += dt;
            shut = BlinkAt(blinkClock);
            if (blinkClock >= blinkSeconds.x + blinkSeconds.y + blinkSeconds.z)
            {
                blinkClock = -1f;
                shut = 0f;
                if (blinksLeft > 0) { blinksLeft--; secondBlink = true; nextBlink = now + .1f; }
                else { secondBlink = false; nextBlink = now + Random.Range(blinkEvery.x, blinkEvery.y); }
            }
        }

        // Glances: the eyes jump to a nearby point, or back to straight ahead, and rest there.
        if (glanceClock < 0f && now >= nextGlance)
        {
            gazeFrom = gaze;
            gazeTo = gaze != Vector2.zero && Random.value < backAhead
                ? Vector2.zero
                : new Vector2(Random.Range(-1f, 1f) * glanceReach.x, Random.Range(-1f, 1f) * glanceReach.y);
            glanceClock = 0f;
            Glances++;
            nextGlance = now + Random.Range(glanceEvery.x, glanceEvery.y);
        }
        if (glanceClock >= 0f)
        {
            glanceClock += dt;
            float t = Mathf.Clamp01(glanceClock / glanceSeconds);
            gaze = Vector2.Lerp(gazeFrom, gazeTo, t * t * (3f - 2f * t));
            if (t >= 1f) glanceClock = -1f;
        }

        Apply(shut, gaze);
    }

    float BlinkAt(float t)
    {
        float close = Mathf.Max(.001f, blinkSeconds.x), hold = Mathf.Max(0f, blinkSeconds.y), open = Mathf.Max(.001f, blinkSeconds.z);
        if (t < close) return Smooth(t / close);
        if (t < close + hold) return 1f;
        return 1f - Smooth(Mathf.Clamp01((t - close - hold) / open));
    }

    /// <summary>
    /// Poses the face at once: <paramref name="shut"/> 0 (open) to 1 (eyes shut), and the eyes looking
    /// <paramref name="look"/> degrees (x to Ace's right, y down), the lids following.
    /// </summary>
    public void Apply(float shut, Vector2 look)
    {
        shut = Mathf.Clamp01(shut);
        if (skin != null)
        {
            Set(UpperL, upperLid * shut);
            Set(UpperR, upperLid * shut);
            Set(LowerL, lowerLid * shut);
            Set(LowerR, lowerLid * shut);
            float side = lidsFollow * Mathf.Clamp01(Mathf.Abs(look.x) / 30f);
            float upDown = lidsFollow * Mathf.Clamp01(Mathf.Abs(look.y) / 30f);
            bool right = look.x > 0f, down = look.y > 0f;
            // Looking to Ace's right, the left eye turns in and the right eye out; to the left, the other way.
            Set(InL, right ? side : 0f);
            Set(OutR, right ? side : 0f);
            Set(OutL, right ? 0f : side);
            Set(InR, right ? 0f : side);
            Set(DownL, down ? upDown : 0f);
            Set(DownR, down ? upDown : 0f);
            Set(UpL, down ? 0f : upDown);
            Set(UpR, down ? 0f : upDown);
        }
        // The jaw at rest: shut.
        if (jaw != null) jaw.localRotation = restJaw;
        // The eyes turn about Ace's own up and right (the body faces its forward), from the eyes' rest pose in the head.
        Transform around = body != null ? body : transform;
        Quaternion turn = Quaternion.AngleAxis(look.x, around.up) * Quaternion.AngleAxis(look.y, around.right);
        if (eyeL != null && eyeL.parent != null) eyeL.rotation = turn * (eyeL.parent.rotation * restL);
        if (eyeR != null && eyeR.parent != null) eyeR.rotation = turn * (eyeR.parent.rotation * restR);
    }

    void Set(int which, float weight)
    {
        int index = shapes[which];
        if (index < 0 || Mathf.Abs(weights[which] - weight) < .01f) return;
        weights[which] = weight;
        skin.SetBlendShapeWeight(index, weight);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    // The blend shape whose name ends with this one (the Creator's export puts "MESHBlends." or similar before it).
    static int Shape(Mesh mesh, string name)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string n = mesh.GetBlendShapeName(i);
            if (!n.EndsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Length == name.Length || !char.IsLetterOrDigit(n[n.Length - name.Length - 1])) return i;
        }
        return -1;
    }

    static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
