using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// UI MOTION, SMALL AND QUICK (23 Oct 2026; his note of 22 Oct: "I want there to be like juice, I want there to be a feeling, I
// want there to feel good, you know.")
//
// A tween helper for UI built in code (the phone first: RecapPhone). Not a package and not a MonoBehaviour: a list of running
// motions that its owner steps from its own LateUpdate, on its own clock. The phone's clock is unscaled (the phone pauses the
// game), and a lab can slow it down to photograph a motion on its way.
//
// Five kinds of motion: a box's x or y (its anchored position), its scale, a canvas group's alpha and a graphic's colour. The
// eases are the arrivals (out-quad, out-cubic, out-quint, and a gentle out-back that settles rather than bounces), the exits
// (in-quad, in-cubic), and three shapes that end where they began: a press's squish (down to 0.94, back up past 1.03, home),
// a tab icon's pop (up to 1.15 and back) and a refused press's shake.
//
// Why it is built like this:
//   * Nothing is allocated per frame. The motions are structs in an array that grows only if it must; the eases are a switch
//     (no delegates, no closures); a colour rides in a Vector4 (no boxing). The phone's comments ask that of everything it runs
//     every frame.
//   * A motion shows its first value the moment it is added, so the frame it begins on draws its start, wherever in the frame
//     it began (a press handled in Update, a phone switched on by another script).
//   * One motion per object and kind: a new one takes over from the old (a second press restarts the squish; it never stacks).
//   * Finish puts every motion at its end at once. The phone calls it before it acts on a press, so a press during a motion
//     never waits for the motion.
//   * A motion whose object has gone (a rebuilt app's old card) is dropped; one whose object has been switched off is put at its
//     end, so nothing is left halfway for the next time it shows.
// ---------------------------------------------------------------------------
public static class UiTween
{
    public enum Ease : byte { Linear, OutQuad, OutCubic, OutQuint, OutBack, InQuad, InCubic, Squish, Pop, Shake }
    public enum Kind : byte { MoveX, MoveY, Scale, Alpha, Colour }

    /// <summary>Out-back's overshoot: about 5% of the way (a slight settle, not a bounce).</summary>
    public const float Back = 1.2f;

    /// <summary>The squish's low point and its overshoot (scale): his phone's buttons give a little and spring back.</summary>
    public const float SquishLow = .94f, SquishHigh = 1.03f;

    /// <summary>True for the shapes that end where they began (Squish, Pop, Shake): their end is their "from".</summary>
    public static bool EndsWhereItBegan(Ease ease) => ease == Ease.Squish || ease == Ease.Pop || ease == Ease.Shake;

    /// <summary>
    /// The ease at <paramref name="t"/> (0 to 1, clamped): 0 at the start and 1 at the end for the arrivals and exits (out-back
    /// passes 1 on the way). The shapes give the share of the way from "from" toward "to": the squish 0, 1 at its low point,
    /// then -0.5 (the overshoot, for a squish from 1 to 0.94: 1.03), then 0; the pop 0, 1, 0; the shake swings between -1 and 1,
    /// dying away, and ends at 0.
    /// </summary>
    public static float Eased(Ease ease, float t)
    {
        t = Mathf.Clamp01(t);
        switch (ease)
        {
            case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
            case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
            case Ease.OutQuint: { float u = 1f - t; return 1f - u * u * u * u * u; }
            case Ease.OutBack: { float u = t - 1f; return 1f + (Back + 1f) * u * u * u + Back * u * u; }
            case Ease.InQuad: return t * t;
            case Ease.InCubic: return t * t * t;
            case Ease.Squish: return Squish(t);
            case Ease.Pop: return Pop(t);
            case Ease.Shake: return Mathf.Sin(t * 6f * Mathf.PI) * (1f - t);   // three swings, each smaller
            default: return t;
        }
    }

    // A press: down to the low point in the first 22% (quickly, easing into it), back up past home to the overshoot by 60%
    // (springing), and settling home by the end. In shares of the way from 1 to SquishLow: 0, 1, then -0.5 (SquishHigh), then 0.
    static float Squish(float t)
    {
        const float low = .22f, high = .6f;
        float over = (SquishHigh - 1f) / (1f - SquishLow);   // 0.5: the overshoot, as a share of the dip
        if (t <= low) return OutQuad(t / low);
        if (t <= high) return 1f - (1f + over) * OutCubic((t - low) / (high - low));
        return -over * (1f - InOutQuad((t - high) / (1f - high)));
    }

    // A tab's icon: up in the first 35% (easing out), back down by the end (easing in and out).
    static float Pop(float t) => t <= .35f ? OutQuad(t / .35f) : 1f - InOutQuad((t - .35f) / .65f);

    static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
    static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
    static float InOutQuad(float t) => t < .5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

    // ------------------------------------------------------------------ the running motions

    /// <summary>
    /// The motions one piece of UI is running. Its owner adds them with a start time and a length on its own clock, and calls
    /// Step with that clock once a frame (after anything that rebuilds or moves the UI that frame, before it is drawn).
    /// </summary>
    public sealed class Set
    {
        struct Motion
        {
            public Object target;          // a RectTransform (MoveX, MoveY, Scale), a CanvasGroup (Alpha) or a Graphic (Colour)
            public Kind kind;
            public Ease ease;
            public Vector4 from, to;       // x alone, except a colour (rgba)
            public float start, seconds;
        }

        Motion[] list;
        int count;

        public Set(int capacity = 32) => list = new Motion[Mathf.Max(4, capacity)];

        /// <summary>How many motions are running (a delayed one, waiting for its start, counts).</summary>
        public int Count => count;
        public bool Running => count > 0;

        /// <summary>A box's anchored x, from one value to another.</summary>
        public void MoveX(RectTransform box, float from, float to, float start, float seconds, Ease ease) =>
            Add(box, Kind.MoveX, ease, new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f), start, seconds);

        /// <summary>A box's anchored y, from one value to another.</summary>
        public void MoveY(RectTransform box, float from, float to, float start, float seconds, Ease ease) =>
            Add(box, Kind.MoveY, ease, new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f), start, seconds);

        /// <summary>A box's scale (x and y together; it turns about its pivot).</summary>
        public void Scale(RectTransform box, float from, float to, float start, float seconds, Ease ease) =>
            Add(box, Kind.Scale, ease, new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f), start, seconds);

        /// <summary>A canvas group's alpha (everything under it fades together).</summary>
        public void Alpha(CanvasGroup group, float from, float to, float start, float seconds, Ease ease) =>
            Add(group, Kind.Alpha, ease, new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f), start, seconds);

        /// <summary>A graphic's colour (an image's or a text's).</summary>
        public void Colour(Graphic graphic, Color from, Color to, float start, float seconds, Ease ease) =>
            Add(graphic, Kind.Colour, ease, from, to, start, seconds);

        /// <summary>True while <paramref name="target"/> has a motion of this kind running.</summary>
        public bool Has(Object target, Kind kind) => target != null && IndexOf(target, kind) >= 0;

        /// <summary>Time on the owner's clock until the last motion has ended (0 when none runs).</summary>
        public float Left(float now)
        {
            float left = 0f;
            for (int i = 0; i < count; i++) left = Mathf.Max(left, list[i].start + list[i].seconds - now);
            return left;
        }

        /// <summary>Every motion one step on, to <paramref name="now"/>; the ones that have ended are put at their end and dropped.</summary>
        public void Step(float now)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                ref Motion m = ref list[i];
                if (m.target == null) { RemoveAt(i); continue; }                  // its object has gone (an old card)
                float t = (now - m.start) / m.seconds;
                if (t >= 1f || !On(m.target)) { Apply(in m, End(in m)); RemoveAt(i); continue; }
                Apply(in m, t <= 0f ? m.from : Vector4.LerpUnclamped(m.from, m.to, Eased(m.ease, t)));
            }
        }

        /// <summary>Every motion at its end, at once (a press during a motion: it finishes, then the press acts).</summary>
        public void Finish()
        {
            for (int i = 0; i < count; i++)
                if (list[i].target != null) Apply(in list[i], End(in list[i]));
            Clear();
        }

        /// <summary>Every motion dropped where it stands (nothing applied).</summary>
        public void Clear()
        {
            for (int i = 0; i < count; i++) list[i] = default;   // no references kept to objects that have gone
            count = 0;
        }

        void Add(Object target, Kind kind, Ease ease, Vector4 from, Vector4 to, float start, float seconds)
        {
            if (target == null) return;
            int at = IndexOf(target, kind);
            if (at < 0)
            {
                if (count == list.Length) System.Array.Resize(ref list, list.Length * 2);   // rare: only past the capacity
                at = count++;
            }
            list[at] = new Motion { target = target, kind = kind, ease = ease, from = from, to = to, start = start, seconds = Mathf.Max(.0001f, seconds) };
            Apply(in list[at], from);   // its first value at once: the frame it begins on draws its start
        }

        int IndexOf(Object target, Kind kind)
        {
            for (int i = 0; i < count; i++)
                if (list[i].kind == kind && ReferenceEquals(list[i].target, target)) return i;
            return -1;
        }

        void RemoveAt(int i)
        {
            count--;
            list[i] = list[count];
            list[count] = default;
        }

        static Vector4 End(in Motion m) => EndsWhereItBegan(m.ease) ? m.from : m.to;

        // Switched off (an app's card as the app is built again, the tab bar on Tonight): put at its end, not left halfway.
        static bool On(Object target) => target is Component c && c.gameObject.activeInHierarchy;

        static void Apply(in Motion m, Vector4 v)
        {
            switch (m.kind)
            {
                case Kind.MoveX:
                {
                    var box = (RectTransform)m.target;
                    Vector2 p = box.anchoredPosition;
                    p.x = v.x;
                    box.anchoredPosition = p;
                    break;
                }
                case Kind.MoveY:
                {
                    var box = (RectTransform)m.target;
                    Vector2 p = box.anchoredPosition;
                    p.y = v.x;
                    box.anchoredPosition = p;
                    break;
                }
                case Kind.Scale:
                    ((RectTransform)m.target).localScale = new Vector3(v.x, v.x, 1f);
                    break;
                case Kind.Alpha:
                    ((CanvasGroup)m.target).alpha = v.x;
                    break;
                case Kind.Colour:
                    ((Graphic)m.target).color = v;
                    break;
            }
        }
    }
}
