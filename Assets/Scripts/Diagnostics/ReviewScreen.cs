using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// ---------------------------------------------------------------------------
// REVIEW 2 - EVERY LAB PHOTO CHECKS ITS OWN WORDS (his girlfriend's playtest, 19 Oct 2026)
//
// She played Days 1-3 on a PlayStation pad, dyslexic, and "felt overall lost most of the time". Part of that was the
// words themselves: "your text needs to be bolder"; the day and the time "way too small"; the bench's "flip it over"
// "so tiny"; buttons written out as words ("Instead of writing out square, just put the icon"); notes gone before a slow
// reader is done. He asked for "something that reviews EVERYTHING AND EVERYTHING, because these silly mistakes I want to
// get ahead of". The labs already photograph every moment they play; this makes every one of those photos measure the
// words on the screen at that instant, so a text that is too small, too faint or a button's name can never pass unseen
// again, in any lab, on any day.
//
// What it does, at photo time only (PlayLab.Photo and the other labs' Photo call Scan right after their capture, in the
// same end-of-frame moment, so the UI it reads is the UI in the photo):
//   * reads every TextMesh Pro text that is on and visible: UGUI (TextMeshProUGUI) and world-space (TextMeshPro);
//   * for each: its words (rich text stripped), its size as one em on a 1080p screen, its colour, the colour behind it
//     sampled from the photo itself (the glyph pixels dropped, the median of the rest), the WCAG contrast of the two, an
//     outline or shadow, its weight (bold, a heavy face, dilate) and any pad button written as a word; and who draws it
//     (the hierarchy path and the nearest script up the parents, so a reader knows which file to open);
//   * grades each: S1 under 18 px, or contrast under 3 with no outline; S2 under 26 px (Xbox Accessibility Guideline
//     101's minimum at 1080p), contrast under 4.5 with no outline, or a button word; S3 thin and under 32 px, or more
//     than 25 words in one block;
//   * writes <name>.text.json next to the photo (every text, every measure), appends one block to the folder's
//     text-check.txt, and saves <name>.text.jpg: the photo with a 2 px box round each flagged text (red S1, amber S2,
//     yellow S3).
//
// Fixit Fidget › Review › Screens 1 gathers every photo's JSON into one ranked report (ReviewScreenGather, editor);
// Screens 2 photographs the Game view now (SnapNow) for a sweep by hand. Off for a session with the PlayerPrefs key
// FixitFidget.ReviewScreen.Off = 1. It never throws (one warning tagged [Review screen] and an empty result), never
// yields, never changes a lab's timing, and never touches the photo it is given: the labs behave exactly as before, with
// three more files beside each photo.
// ---------------------------------------------------------------------------
public static class ReviewScreen
{
    public const string OffKey = "FixitFidget.ReviewScreen.Off";
    const string Tag = "[Review screen]";

    // The grades (see the header). 26 px is XAG 101's minimum at 1080p; 18 is where her "way too small" lives (the sign's
    // day and time are 16, HudCorners); 32 is where a thin face stops reading as thin; 4.5 and 3 are WCAG's AA for small and
    // large text; 25 words is the bench screen's own rule for one block (WordBudget.BenchFirstRepair).
    public const float TinyPx = 18f, SmallPx = 26f, ThinPx = 32f;
    public const float LowContrast = 3f, WeakContrast = 4.5f;
    public const int ManyWords = 25;
    const float FaintAlpha = .05f;       // under this a text isn't on the screen in any way that matters
    const float GlyphDistance = .3f;     // RGB distance (0-1 per channel) within which a photo pixel is the text's own ink
    const float KnownShare = .2f;        // fewer background pixels than this share of the rect and the background is unknown
    const int Margin = 3;                // px round each text's words, sampled and boxed
    const long SampleBudget = 120000;    // pixels sampled per text at most: a huge block is stepped (a median needs no more)

    public struct Result { public int texts, s1, s2, s3; }

    /// <summary>One text as it was on the screen in one photo. Written by JsonUtility, read back by the gather.</summary>
    [Serializable]
    public sealed class TextMeasure
    {
        public string text = "";          // the words, rich text stripped, one space between words
        public int words;
        public float px1080;              // one em on a 1080p screen
        public float fontSize;            // TMP's own number (the fitted size when it auto-sizes)
        public string font = "";
        public bool bold, thin, outlined, underlay;
        public float dilate, outlineWidth;
        public string colour = "";        // "#rrggbb": the text's colour times the material's face colour, sRGB
        public float alpha;               // effective: the colour's, the face's, every CanvasGroup's
        public string background = "";    // "#rrggbb" sampled from the photo behind the words, or "" when unknown
        public bool backgroundKnown;
        public float contrast;            // WCAG ratio of the words as seen against that background; -1 when unknown
        public float glyphShare;          // the share of the rect's pixels that were the text's own ink (0: covered?)
        public string buttonWords = "";   // "R2, Square": pad buttons written as words
        public bool buttonLowConfidence;  // only Menu / View matched, and those may be ordinary words
        public int severity;              // 0 fine, 1-3 as the header says
        public string reasons = "";
        public string owner = "";         // the nearest script up the parents that isn't TMP's or Unity's UI
        public string root = "";          // the root object's name, for a text whose script isn't above it (HudCorners builds its canvas at the root)
        public string path = "";          // the hierarchy, Canvas/.../Label
        public string kind = "";          // "ugui" or "world"
        public float x, y, w, h;          // the words' rect on the screen, px from the bottom left, in Screen units
    }

    /// <summary>One photo's check: &lt;name&gt;.text.json.</summary>
    [Serializable]
    public sealed class PhotoCheck
    {
        public string name = "", folder = "", time = "";
        public int screenWidth, screenHeight, shotWidth, shotHeight;
        public int texts, s1, s2, s3, skipped;
        public List<TextMeasure> items = new List<TextMeasure>();
    }

    // ------------------------------------------------------------------ on / off

    static bool? enabled;

    /// <summary>On unless PlayerPrefs FixitFidget.ReviewScreen.Off is 1 (read once a session; set this to change it mid-session).</summary>
    public static bool Enabled
    {
        get
        {
            if (enabled == null)
            {
                try { enabled = PlayerPrefs.GetInt(OffKey, 0) != 1; }
                catch (Exception) { enabled = true; }
            }
            return enabled.Value;
        }
        set => enabled = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        enabled = null;
        Runner.instance = null;
    }

    // ------------------------------------------------------------------ the scan

    /// <summary>
    /// Measures every word on the screen against <paramref name="shot"/> (the photo just taken, this same end of frame)
    /// and writes <paramref name="name"/>.text.json, .text.jpg and a block of text-check.txt into <paramref name="folder"/>.
    /// Never throws and never yields: a failure is one warning and an empty result. The photo itself is only read.
    /// </summary>
    public static Result Scan(Texture2D shot, string folder, string name)
    {
        var result = new Result();
        try
        {
            if (!Enabled || shot == null || string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name)) return result;
            int sw = shot.width, sh = shot.height;
            if (sw < 2 || sh < 2 || Screen.width < 2 || Screen.height < 2) return result;
            Color32[] pixels = shot.GetPixels32();   // the one read of the photo; every text samples this array
            // The photo and the screen are usually the same size; when they aren't (a scaled Game view) the screen's
            // rects are mapped onto the photo, as AceTurnCheck maps its close-ups.
            float kx = sw / (float)Screen.width, ky = sh / (float)Screen.height;

            var photo = new PhotoCheck
            {
                name = name, folder = folder, time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                screenWidth = Screen.width, screenHeight = Screen.height, shotWidth = sw, shotHeight = sh,
            };
            Camera main = Camera.main;
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                TextMeasure m = null;
                try { m = Measure(text, main, pixels, sw, sh, kx, ky); }
                catch (Exception) { }   // one odd text (destroyed under us, a font with no material) mustn't lose the photo
                if (m == null) { photo.skipped++; continue; }
                Grade(m);
                photo.items.Add(m);
                photo.texts++;
                if (m.severity == 1) photo.s1++;
                else if (m.severity == 2) photo.s2++;
                else if (m.severity == 3) photo.s3++;
            }

            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name + ".text.json"), JsonUtility.ToJson(photo, true));
            File.AppendAllText(Path.Combine(folder, "text-check.txt"), Block(photo));
            SaveMarked(pixels, sw, sh, kx, ky, photo, Path.Combine(folder, name + ".text.jpg"));
            result.texts = photo.texts;
            result.s1 = photo.s1;
            result.s2 = photo.s2;
            result.s3 = photo.s3;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} {name}: the words weren't checked ({e.GetType().Name}: {e.Message}).");
        }
        return result;
    }

    // ------------------------------------------------------------------ one text

    // Null when the text isn't on the screen in the photo: empty, faint, off its canvas, off the screen, behind the camera.
    static TextMeasure Measure(TMP_Text text, Camera main, Color32[] pixels, int sw, int sh, float kx, float ky)
    {
        if (text == null || !text.isActiveAndEnabled) return null;
        string rich = text.text;
        if (string.IsNullOrEmpty(rich)) return null;
        string plain = text.richText ? Strip(rich) : Squash(rich);
        if (plain.Length == 0) return null;

        // Which camera puts it on the screen: none for an overlay canvas (its world units are screen pixels), the
        // canvas's own for a camera canvas (none set: Unity draws it as an overlay), the main one for anything in the world.
        bool ugui = text is TextMeshProUGUI;
        Camera cam;
        if (ugui)
        {
            Canvas canvas = text.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled) return null;
            Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            if (!root.isActiveAndEnabled) return null;
            if (root.renderMode == RenderMode.ScreenSpaceOverlay) cam = null;
            else if (root.renderMode == RenderMode.ScreenSpaceCamera) cam = root.worldCamera;
            else cam = root.worldCamera != null ? root.worldCamera : main;
            if (root.renderMode == RenderMode.WorldSpace && cam == null) return null;
            // Scrolled out of a list's window (a RectMask2D culls it: the phone's rows below "Back to work"): not on the
            // screen, whatever its rect says (20 Oct: the first gather boxed the phone's hidden rows under the phone).
            if (text.canvasRenderer != null && text.canvasRenderer.cull) return null;
        }
        else
        {
            var renderer = text.GetComponent<Renderer>();
            if (renderer != null && !renderer.enabled) return null;
            cam = main;
            if (cam == null) return null;
        }
        if (cam != null && cam.targetTexture != null) return null;   // drawn into a texture (a screen in the world), not the photo

        // The material: the face colour, the dilate, the outline, the underlay. The shared one (fontMaterial would copy it).
        Material material = text.fontSharedMaterial;
        if (material == null && text.font != null) material = text.font.material;
        Color face = Color.white;
        float dilate = 0f, outline = 0f;
        bool underlay = false;
        if (material != null)
        {
            if (material.HasProperty(ShaderUtilities.ID_FaceColor)) face = material.GetColor(ShaderUtilities.ID_FaceColor);
            if (material.HasProperty(ShaderUtilities.ID_FaceDilate)) dilate = material.GetFloat(ShaderUtilities.ID_FaceDilate);
            if (material.HasProperty(ShaderUtilities.ID_OutlineWidth)) outline = material.GetFloat(ShaderUtilities.ID_OutlineWidth);
            underlay = material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay);
        }

        // The colour the eye gets: the text's own (or the one <color> tag wrapped round all of it), times the face.
        Color colour = text.color;
        if (text.richText && WholeColourTag(rich, out Color tagged)) colour = new Color(tagged.r, tagged.g, tagged.b, colour.a * tagged.a);
        float alpha = colour.a * face.a * GroupAlpha(text.transform);
        if (ugui && text.canvasRenderer != null) alpha *= text.canvasRenderer.GetAlpha();
        if (alpha < FaintAlpha) return null;
        var ink = new Color(Mathf.Clamp01(colour.r * face.r), Mathf.Clamp01(colour.g * face.g), Mathf.Clamp01(colour.b * face.b), 1f);

        // Where the words are: the rendered text's own bounds (the box when nothing is laid out yet), four corners
        // through the text's transform to the world and on to the screen, then clamped to the screen.
        RectTransform rt = text.rectTransform;
        Bounds b = text.textBounds;
        var corners = new Vector3[4];
        if (b.size.x > 1e-5f && b.size.y > 1e-5f)
        {
            corners[0] = rt.TransformPoint(new Vector3(b.min.x, b.min.y, b.center.z));
            corners[1] = rt.TransformPoint(new Vector3(b.max.x, b.min.y, b.center.z));
            corners[2] = rt.TransformPoint(new Vector3(b.max.x, b.max.y, b.center.z));
            corners[3] = rt.TransformPoint(new Vector3(b.min.x, b.max.y, b.center.z));
        }
        else rt.GetWorldCorners(corners);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            if (Behind(cam, corners[i])) return null;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        if (float.IsNaN(x0) || float.IsNaN(y0) || float.IsNaN(x1) || float.IsNaN(y1)) return null;
        if (x1 <= 0f || y1 <= 0f || x0 >= Screen.width || y0 >= Screen.height) return null;   // off the screen
        x0 = Mathf.Max(0f, x0); y0 = Mathf.Max(0f, y0); x1 = Mathf.Min(Screen.width, x1); y1 = Mathf.Min(Screen.height, y1);
        if (x1 - x0 < .5f || y1 - y0 < .5f) return null;

        // One em on the screen: the text's own size, as TMP holds it (the fitted size when it auto-sizes: TMP writes it
        // back into fontSize), projected from the text's local space and scaled to a 1080p screen. A world-space
        // TextMeshPro draws a point at a tenth of a unit (TMP's own factor); the UGUI text at one unit per point.
        float em = text.fontSize * (ugui ? 1f : .1f);
        Vector2 e0 = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(Vector3.zero));
        Vector2 e1 = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(new Vector3(0f, em, 0f)));
        float px1080 = Vector2.Distance(e0, e1) * 1080f / Screen.height;
        if (float.IsNaN(px1080) || float.IsInfinity(px1080)) px1080 = 0f;

        var m = new TextMeasure
        {
            text = plain, words = WordBudget.Words(plain),
            px1080 = Round(px1080, 10f), fontSize = Round(text.fontSize, 10f),
            font = text.font != null ? text.font.name : "",
            dilate = Round(dilate, 100f), outlineWidth = Round(outline, 100f), underlay = underlay,
            outlined = outline > .05f || underlay,
            colour = "#" + ColorUtility.ToHtmlStringRGB(ink), alpha = Round(alpha, 100f),
            owner = OwnerOf(text.transform), root = text.transform.root != null ? text.transform.root.name : "",
            path = PathOf(text.transform), kind = ugui ? "ugui" : "world",
            x = Round(x0, 10f), y = Round(y0, 10f), w = Round(x1 - x0, 10f), h = Round(y1 - y0, 10f),
        };
        // Her "needs to be bolder": thin is a regular style on a regular face with no dilate. A <b> round the whole text
        // counts; a <b> round one word in it ("<b>Grace:</b> ...") doesn't make the sentence bold.
        m.bold = (text.fontStyle & FontStyles.Bold) != 0 || (text.richText && WholeBoldTag(rich));
        m.thin = !m.bold && dilate <= .05f && !HeavyName.IsMatch(m.font);

        // Pad buttons written as words ("Instead of writing out square, just put the icon"). Menu and View may be
        // ordinary words: low confidence when nothing surer matched beside them.
        var found = new List<string>();
        bool sure = false;
        foreach (Match match in Buttons.Matches(plain))
        {
            string word = match.Value;
            if (!found.Contains(word)) found.Add(word);
            if (word != "Menu" && word != "View") sure = true;
        }
        m.buttonWords = string.Join(", ", found);
        m.buttonLowConfidence = found.Count > 0 && !sure;

        // The colour behind the words, from the photo: the rect grown by 3 px, the text's own ink dropped, the median of
        // the rest per channel. The words as the eye gets them are the ink blended by its alpha over that background.
        Background(pixels, sw, sh, kx, ky, x0, y0, x1, y1, ink, out Color background, out bool known, out float glyphShare);
        m.glyphShare = Round(glyphShare, 1000f);
        m.backgroundKnown = known;
        if (known)
        {
            m.background = "#" + ColorUtility.ToHtmlStringRGB(background);
            Color seen = Color.Lerp(background, ink, Mathf.Clamp01(alpha));
            m.contrast = Round(Contrast(seen, background), 100f);
        }
        else m.contrast = -1f;
        return m;
    }

    /// <summary>The grade and its reasons, from the measures (the gather re-grades a text's worst measures with this).</summary>
    public static void Grade(TextMeasure m)
    {
        var why = new List<string>();
        int severity = 0;
        void Flag(int level, string reason)
        {
            if (severity == 0 || level < severity) severity = level;
            why.Add(reason);
        }
        if (m.px1080 < TinyPx) Flag(1, Inv($"{m.px1080:0} px at 1080p (under {TinyPx:0})"));
        else if (m.px1080 < SmallPx) Flag(2, Inv($"{m.px1080:0} px at 1080p (under XAG 101's {SmallPx:0})"));
        if (m.backgroundKnown && m.contrast > 0f && !m.outlined)
        {
            if (m.contrast < LowContrast) Flag(1, Inv($"contrast {m.contrast:0.0} with no outline (under {LowContrast:0})"));
            else if (m.contrast < WeakContrast) Flag(2, Inv($"contrast {m.contrast:0.0} with no outline (under {WeakContrast:0.0})"));
        }
        if (!string.IsNullOrEmpty(m.buttonWords))
            Flag(2, "a button written as a word: " + m.buttonWords + (m.buttonLowConfidence ? " (low confidence: may be an ordinary word)" : ""));
        if (m.thin && m.px1080 < ThinPx) Flag(3, Inv($"thin at {m.px1080:0} px (under {ThinPx:0})"));
        if (m.words > ManyWords) Flag(3, Inv($"{m.words} words in one block (over {ManyWords})"));
        m.severity = severity;
        m.reasons = string.Join("; ", why);
    }

    // ------------------------------------------------------------------ the words

    static readonly Regex Tags = new Regex("<[^<>]*>", RegexOptions.Compiled);
    static readonly Regex WholeColour = new Regex(@"^\s*<color=([^>]+)>(.*)</color>\s*$", RegexOptions.Compiled | RegexOptions.Singleline);
    static readonly Regex WholeBold = new Regex(@"^\s*<b>(.*)</b>\s*$", RegexOptions.Compiled | RegexOptions.Singleline);
    static readonly Regex HeavyName = new Regex("Bold|Black|Heavy|SemiBold|Semibold|Medium|ExtraBold", RegexOptions.Compiled);
    static readonly Regex Buttons = new Regex(@"\b(Square|Triangle|Cross|Circle|R1|R2|R3|L1|L2|L3|Options|Share|Touchpad|LB|RB|LT|RT|Menu|View)\b", RegexOptions.Compiled);

    // Rich text stripped: a tag is nothing on the screen, except a line break, which is a space between words.
    static string Strip(string rich) =>
        Squash(Tags.Replace(rich, match => match.Value.StartsWith("<br", StringComparison.OrdinalIgnoreCase) ? " " : ""));

    // One space between words, none at the ends, no invisible characters.
    static string Squash(string s)
    {
        var b = new StringBuilder(s.Length);
        bool space = true;
        foreach (char c in s)
        {
            if (c == '\u200B' || c == '\uFEFF') continue;   // a zero-width space, a byte-order mark: nothing on the screen
            if (char.IsWhiteSpace(c)) { if (!space) { b.Append(' '); space = true; } }
            else { b.Append(c); space = false; }
        }
        return b.ToString().TrimEnd();
    }

    // "<color=#7fd3a8>Fixed!</color>": the whole text in one colour tag, so that is its colour.
    static bool WholeColourTag(string rich, out Color colour)
    {
        colour = Color.white;
        Match m = WholeColour.Match(rich);
        if (!m.Success || m.Groups[2].Value.IndexOf("</color>", StringComparison.Ordinal) >= 0) return false;
        return ColorUtility.TryParseHtmlString(m.Groups[1].Value.Trim().Trim('"'), out colour);
    }

    static bool WholeBoldTag(string rich)
    {
        Match m = WholeBold.Match(rich);
        return m.Success && m.Groups[1].Value.IndexOf("</b>", StringComparison.Ordinal) < 0;
    }

    // ------------------------------------------------------------------ where and how faint

    // Behind the camera: the point lies on the camera's side of its near plane (nothing for an overlay canvas).
    static bool Behind(Camera cam, Vector3 world)
    {
        if (cam == null) return false;
        Transform t = cam.transform;
        return Vector3.Dot(world - t.position, t.forward) <= cam.nearClipPlane;
    }

    // Every CanvasGroup up the parents fades the text (a group that ignores its parents stops the walk, as Unity does).
    static float GroupAlpha(Transform t)
    {
        float alpha = 1f;
        for (Transform p = t; p != null; p = p.parent)
        {
            CanvasGroup g = p.GetComponent<CanvasGroup>();
            if (g == null || !g.enabled) continue;
            alpha *= g.alpha;
            if (g.ignoreParentGroups) break;
        }
        return alpha;
    }

    // ------------------------------------------------------------------ who draws it

    // The nearest script up the parents that isn't TextMesh Pro's or Unity's own: the file a reader opens to change the
    // words. "" when the script that built the canvas isn't above it (HudCorners builds its canvas at the root).
    static string OwnerOf(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            foreach (MonoBehaviour behaviour in p.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;   // a missing script
                Type type = behaviour.GetType();
                string ns = type.Namespace ?? "";
                if (ns.StartsWith("TMPro", StringComparison.Ordinal) || ns.StartsWith("UnityEngine", StringComparison.Ordinal)
                    || ns.StartsWith("Unity.", StringComparison.Ordinal)) continue;
                return type.Name;
            }
        return "";
    }

    // Canvas/.../Label; a long path keeps its root and its last three names (the leaf and what holds it are what a reader looks for).
    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (Transform p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        string path = string.Join("/", names);
        if (path.Length <= 120 || names.Count < 5) return path;
        return names[0] + "/.../" + names[names.Count - 3] + "/" + names[names.Count - 2] + "/" + names[names.Count - 1];
    }

    // ------------------------------------------------------------------ the colour behind the words

    static readonly int[] HistR = new int[256], HistG = new int[256], HistB = new int[256];

    static void Background(Color32[] pixels, int sw, int sh, float kx, float ky, float x0, float y0, float x1, float y1, Color ink,
        out Color background, out bool known, out float glyphShare)
    {
        background = Color.black;
        known = false;
        glyphShare = 0f;
        int px0 = Mathf.Clamp(Mathf.FloorToInt((x0 - Margin) * kx), 0, sw - 1), px1 = Mathf.Clamp(Mathf.CeilToInt((x1 + Margin) * kx), 0, sw - 1);
        int py0 = Mathf.Clamp(Mathf.FloorToInt((y0 - Margin) * ky), 0, sh - 1), py1 = Mathf.Clamp(Mathf.CeilToInt((y1 + Margin) * ky), 0, sh - 1);
        if (px1 < px0 || py1 < py0) return;
        long area = (long)(px1 - px0 + 1) * (py1 - py0 + 1);
        int step = area > SampleBudget ? Mathf.CeilToInt(Mathf.Sqrt(area / (float)SampleBudget)) : 1;
        Array.Clear(HistR, 0, 256);
        Array.Clear(HistG, 0, 256);
        Array.Clear(HistB, 0, 256);
        float ir = ink.r, ig = ink.g, ib = ink.b, limit = GlyphDistance * GlyphDistance;
        int total = 0, glyphs = 0;
        for (int y = py0; y <= py1; y += step)
        {
            int row = y * sw;
            for (int x = px0; x <= px1; x += step)
            {
                Color32 c = pixels[row + x];
                total++;
                float dr = c.r / 255f - ir, dg = c.g / 255f - ig, db = c.b / 255f - ib;
                if (dr * dr + dg * dg + db * db < limit) { glyphs++; continue; }
                HistR[c.r]++;
                HistG[c.g]++;
                HistB[c.b]++;
            }
        }
        if (total == 0) return;
        glyphShare = glyphs / (float)total;
        int rest = total - glyphs;
        if (rest == 0 || rest < KnownShare * total) return;   // the words fill their box: nothing behind them to read
        background = new Color(Median(HistR, rest) / 255f, Median(HistG, rest) / 255f, Median(HistB, rest) / 255f, 1f);
        known = true;
    }

    static int Median(int[] histogram, int count)
    {
        int half = (count + 1) / 2, seen = 0;
        for (int v = 0; v < 256; v++)
        {
            seen += histogram[v];
            if (seen >= half) return v;
        }
        return 255;
    }

    // WCAG 2: relative luminance from sRGB, the ratio (lighter + .05) / (darker + .05).
    static float Contrast(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        return (Mathf.Max(la, lb) + .05f) / (Mathf.Min(la, lb) + .05f);
    }

    static float Luminance(Color c) => .2126f * Linear(c.r) + .7152f * Linear(c.g) + .0722f * Linear(c.b);
    static float Linear(float srgb) => srgb <= .03928f ? srgb / 12.92f : Mathf.Pow((srgb + .055f) / 1.055f, 2.4f);

    // ------------------------------------------------------------------ the files

    // One block of the folder's text-check.txt: the photo's line, then one line per flagged text.
    static string Block(PhotoCheck photo)
    {
        var b = new StringBuilder();
        b.AppendLine(Inv($"{photo.name}: {photo.texts} texts, S1 {photo.s1}, S2 {photo.s2}, S3 {photo.s3}"));
        foreach (TextMeasure m in photo.items)
            if (m.severity > 0) b.AppendLine("  " + Line(m));
        return b.ToString();
    }

    /// <summary>One flagged text as text-check.txt prints it: [S2] 21 px, contrast 3.4, thin, "Hold R2 to wipe" (HudCorners: Canvas/.../Label).</summary>
    public static string Line(TextMeasure m)
    {
        var b = new StringBuilder();
        b.Append(Inv($"[S{m.severity}] {m.px1080:0} px, contrast "));
        b.Append(m.contrast > 0f ? m.contrast.ToString("0.0", CultureInfo.InvariantCulture) : "?");
        b.Append(", ");
        if (m.thin) b.Append("thin, ");
        if (m.outlined) b.Append("outlined, ");
        if (!string.IsNullOrEmpty(m.buttonWords)) b.Append("button word " + m.buttonWords + (m.buttonLowConfidence ? " (low confidence)" : "") + ", ");
        if (m.words > ManyWords) b.Append(Inv($"{m.words} words, "));
        b.Append("\"" + Short(m.text, 80) + "\" (");
        b.Append(!string.IsNullOrEmpty(m.owner) ? m.owner : "no script above it, under \"" + m.root + "\"");
        b.Append(": " + m.path + ")");
        return b.ToString();
    }

    // The photo with a 2 px box round each flagged text, on the copy of the pixels already read (the photo stays as it was).
    static void SaveMarked(Color32[] pixels, int sw, int sh, float kx, float ky, PhotoCheck photo, string file)
    {
        foreach (TextMeasure m in photo.items)
        {
            if (m.severity == 0) continue;
            Color32 colour = m.severity == 1 ? new Color32(235, 45, 45, 255) : m.severity == 2 ? new Color32(255, 160, 0, 255) : new Color32(255, 230, 0, 255);
            Box(pixels, sw, sh, Mathf.RoundToInt((m.x - Margin) * kx), Mathf.RoundToInt((m.y - Margin) * ky),
                Mathf.RoundToInt((m.x + m.w + Margin) * kx), Mathf.RoundToInt((m.y + m.h + Margin) * ky), colour);
        }
        Texture2D marked = null;
        try
        {
            marked = new Texture2D(sw, sh, TextureFormat.RGB24, false);
            marked.SetPixels32(pixels);
            marked.Apply(false, false);
            File.WriteAllBytes(file, marked.EncodeToJPG(85));
        }
        finally
        {
            if (marked != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(marked);
                else UnityEngine.Object.DestroyImmediate(marked);
            }
        }
    }

    // A 2 px box, kept inside the photo's edges.
    static void Box(Color32[] pixels, int sw, int sh, int x0, int y0, int x1, int y1, Color32 colour)
    {
        x0 = Mathf.Clamp(x0, 0, sw - 1); x1 = Mathf.Clamp(x1, 0, sw - 1);
        y0 = Mathf.Clamp(y0, 0, sh - 1); y1 = Mathf.Clamp(y1, 0, sh - 1);
        if (x1 <= x0 || y1 <= y0) return;
        for (int t = 0; t < 2; t++)
        {
            int ya = Mathf.Min(y0 + t, sh - 1), yb = Mathf.Max(y1 - t, 0);
            for (int x = x0; x <= x1; x++) { pixels[ya * sw + x] = colour; pixels[yb * sw + x] = colour; }
            int xa = Mathf.Min(x0 + t, sw - 1), xb = Mathf.Max(x1 - t, 0);
            for (int y = y0; y <= y1; y++) { pixels[y * sw + xa] = colour; pixels[y * sw + xb] = colour; }
        }
    }

    // ------------------------------------------------------------------ a photo by hand (Play mode)

    /// <summary>
    /// Photographs the Game view at the end of this frame, saves it as Logs/Review/screens-&lt;date&gt;/&lt;time&gt;-&lt;name&gt;.jpg
    /// and checks its words: for a sweep by hand (Fixit Fidget › Review › Screens 2). Play mode only.
    /// </summary>
    public static void SnapNow(string name)
    {
        try
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{Tag} SnapNow needs Play mode: nothing is drawn to photograph otherwise.");
                return;
            }
            Runner.Get().StartCoroutine(Runner.Snap(string.IsNullOrEmpty(name) ? "manual" : name));
        }
        catch (Exception e) { Debug.LogWarning($"{Tag} The photo wasn't taken ({e.Message})."); }
    }

    // A hidden object that outlives scene loads, so a sweep by hand can photograph across them; made on the first call.
    sealed class Runner : MonoBehaviour
    {
        internal static Runner instance;

        internal static Runner Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("Review screen (runtime, hidden)") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Runner>();
            return instance;
        }

        internal static IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Review",
                    "screens-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                Directory.CreateDirectory(folder);
                string file = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + "-" + SafeName(name);
                File.WriteAllBytes(Path.Combine(folder, file + ".jpg"), shot.EncodeToJPG(90));
                Result r = Scan(shot, folder, file);
                Debug.Log($"{Tag} {file}.jpg: {r.texts} texts, S1 {r.s1}, S2 {r.s2}, S3 {r.s3}. {folder}");
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} The photo wasn't taken ({e.Message})."); }
            finally { if (shot != null) Destroy(shot); }
        }
    }

    // ------------------------------------------------------------------ helpers

    static string SafeName(string name)
    {
        var b = new StringBuilder(name.Length);
        foreach (char c in name) b.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == ' ' ? '-' : c);
        return b.Length > 0 ? b.ToString() : "manual";
    }

    static float Round(float value, float scale) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Round(value * scale) / scale;

    static string Short(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 3) + "...";

    // Numbers in files are written the same on every machine (a comma for a decimal point would break the gather's reading).
    static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
