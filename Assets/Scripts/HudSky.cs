using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE SHOP WINDOW, TOP CENTRE: A LIVING SKY OVER THE CAFÉ'S CITY (26 Oct 2026 (HUD 3))
//
// His pick (24 Oct): design A, "the Shop window": an arched café window whose glass holds the sky as the day goes, with the
// time on its sill. His notes: "make sure its apparent that we are in san francisco ... we can always change the hud to match
// the cafe is as of currently during the franchising"; at night the time stays top centre; no goal line and no "today".
//
// HudSky is the sky's arithmetic, the same for every city: the colours of the sky at an hour (top, middle, horizon), the
// sun's day (born behind the roofs at opening, climbing, warming and growing as it lowers, down behind the roofs at closing:
// his note of 22 Oct, "so it looks like sunrise, sunset, dawn, dusk, and like the sun is actually changing"), the moon after
// closing, the stars, the hour the city's windows light, the hazed colours of far and near things, the clouds. Every number
// is designer M2's (review24/designs/M2/src/sky.py), so the window matches the pictures he approved.
//
// HudWindow is the piece itself: a painted deep-green frame with a gilt line, a keystone with a gilt bead, the glass set back
// in it (an inner shadow, two faint reflections), the sky, the sun, the moon, the stars, two clouds that drift, and the city
// (HudCity: one swappable layer; San Francisco now, Seattle as the placeholder next city). The sill under the glass carries
// the time in gilt (32 px bold) and the day in mint (22 px). It is built twice: full size top centre (the glass 316 x 100) and
// at 36 % in the bench strip, where it draws no clouds or small things. Everything times itself on the owner's clock.
// ---------------------------------------------------------------------------
public static class HudSky
{
    /// <summary>What the sky and the city need to know about an hour, worked out once a minute (HudWindow.Set).</summary>
    public struct Moment
    {
        public float hour;                         // the café's clock: 9 opening, 20 closing; the night runs on past 24
        public Color top, mid, hor;                // the sky's three colours
        public float night;                        // the stars' share (0 by day, 1 at night)
        public float lit;                          // the city's windows lit (0 by day, 1 after dusk)
        public float dusk;                         // the dusk's share, from 6:54 PM
        public bool sunUp;                         // the sun is in the sky (or just under the roofs)
        public float sunP, sunElev, sunSize, sunGlow, evening, morning;
        public Color sunColour;
        public bool moonUp;
        public float moonP, moonElev, moonAlpha;
        public Color far, near;                    // silhouettes: hazed far things, dark near things
        public Color cloud;
        public float cloudAlpha;
        public float warm;                         // dawn's or golden hour's warmth, 0..1 (low sun at the day's ends)
    }

    public const float Opening = 9f, Closing = 20f;

    static readonly Color Ink = HudArt.Hex(0x1F1B2A);

    // The sky's colours at keyed hours (top, middle, horizon), lerped smoothly between: the brief's six and steps between.
    static readonly float[] KeyHours = { 7f, 9f, 10f, 11f, 14f, 16.5f, 18.5f, 19.3f, 20f, 21f, 22.5f, 23f, 30f };
    static readonly Color[] KeyTop = { H(0x3B3B78), H(0xC99BB4), H(0x86B4E4), H(0x4C9AE6), H(0x4A8FD6), H(0x4D8FD3), H(0xD98B4E), H(0x8B5677), H(0x2B2259), H(0x151A47), H(0x0A1130), H(0x0A1130), H(0x0A1130) };
    static readonly Color[] KeyMid = { H(0x8A6C9E), H(0xF4B3A0), H(0xBFD8EE), H(0x8CC3F1), H(0x9CC6E6), H(0xA6BCD2), H(0xF4B35E), H(0xE0856C), H(0x61418A), H(0x2E2C69), H(0x15214A), H(0x14204A), H(0x14204A) };
    static readonly Color[] KeyHor = { H(0xE9A18E), H(0xFFDDB6), H(0xFBE3CC), H(0xD2EBFA), H(0xFFE4A8), H(0xFCDDA4), H(0xFFE08C), H(0xFFBE76), H(0xD97579), H(0x5B4778), H(0x26355F), H(0x24345F), H(0x24345F) };

    static Color H(int rgb) => HudArt.Hex(rgb);
    static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
    public static float Smooth(float t) => HudArt.Smooth(t);

    /// <summary>The café's hour for the sky: the night's small hours (0-6) count on past midnight (24-30).</summary>
    public static float SkyHour(float hour) => hour < 7f ? hour + 24f : hour;

    public static Moment At(float hourIn)
    {
        float hour = SkyHour(hourIn);
        var m = new Moment { hour = hour };
        // The sky's three colours.
        float h = Mathf.Clamp(hour, KeyHours[0], KeyHours[KeyHours.Length - 1]);
        m.top = KeyTop[KeyTop.Length - 1]; m.mid = KeyMid[KeyMid.Length - 1]; m.hor = KeyHor[KeyHor.Length - 1];
        for (int i = 0; i < KeyHours.Length - 1; i++)
        {
            if (h < KeyHours[i] || h > KeyHours[i + 1]) continue;
            float t = Smooth((h - KeyHours[i]) / (KeyHours[i + 1] - KeyHours[i]));
            m.top = Mix(KeyTop[i], KeyTop[i + 1], t);
            m.mid = Mix(KeyMid[i], KeyMid[i + 1], t);
            m.hor = Mix(KeyHor[i], KeyHor[i + 1], t);
            break;
        }
        m.night = Smooth((hour - 19.6f) / 2f);
        m.lit = Smooth((hour - 18.8f) / 1.6f);
        m.dusk = Smooth((hour - 18.9f) / 1.3f);
        // The sun: born at 8:40 just under the roofs, down again at 7:50.
        const float rise = 8.67f, sett = 19.83f;
        float p = (hour - rise) / (sett - rise);
        m.sunUp = p >= -.06f && p <= 1.10f;
        m.sunP = p;
        m.sunElev = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p));
        float low = 1f - m.sunElev;
        m.evening = Smooth((hour - 16f) / 3f);
        m.morning = Smooth((10.3f - hour) / 1.5f);
        Color c = Mix(H(0xFFF8DC), H(0xFFD25C), Smooth(low * 1.5f));
        c = Mix(c, H(0xFF9A3D), Mathf.Max(m.evening, m.morning * .8f) * Smooth(low * 1.3f));
        c = Mix(c, H(0xFF6B3D), m.evening * Smooth((low - .6f) / .4f));
        m.sunColour = c;
        m.sunSize = 1f + .22f * low;
        m.sunGlow = .55f + .45f * Mathf.Max(m.evening, m.morning) * low + .15f * low;
        m.warm = m.sunUp ? Mathf.Max(m.evening, m.morning) * low : 0f;
        // The moon after closing: up on the left at 8 PM, high by 1 AM.
        m.moonUp = hour >= 19.6f;
        m.moonP = Mathf.Clamp01((hour - 19.7f) / 8f);
        m.moonElev = Mathf.Sin(Mathf.PI * (.06f + .88f * m.moonP));
        m.moonAlpha = Smooth((hour - 19.6f) / .45f);   // up by closing (8 PM), so the sign's turn and the moon come together
        // Silhouettes and clouds.
        m.far = Mix(Mix(m.hor, m.mid, .35f), Ink, .5f + .22f * m.night);
        m.near = Mix(Mix(m.hor, Ink, .82f), Ink, .5f * m.night);
        float day = 1f - m.night;
        Color body = Mix(Mix(m.mid, Color.white, .62f), Mix(m.hor, H(0xFFB0A0), .3f), m.warm * .55f);
        m.cloud = Mix(Mix(m.mid, m.top, .5f) * .9f, body, day);
        m.cloud.a = 1f;
        m.cloudAlpha = day > .5f ? .95f : .35f * day + .15f;
        return m;
    }

    /// <summary>A colour pushed toward the sky's horizon (by day) or the night's ink: aerial perspective.</summary>
    public static Color Haze(Color c, in Moment m, float amount) => Mix(c, Mix(m.hor, m.mid, .3f), amount);

    /// <summary>The fog's colour at this hour: pale and warm when the sun is low, a blue-grey veil at night.</summary>
    public static Color FogColour(in Moment m)
    {
        Color c = Mix(H(0xF6F3EE), Mix(m.hor, Color.white, .45f), .25f + .5f * m.warm);
        return Mix(c, Mix(m.mid, H(0x3A4468), .55f), m.night * .85f);
    }
}

// 26 Oct 2026 (his Mac): HudGradient lives in its own file, HudGradient.cs, so Unity gives it a script (and its CanvasRenderer).

/// <summary>The window piece (see the header): the frame, the glass and what's in it, the sill with the time and the day.</summary>
public sealed class HudWindow
{
    // The design, in reference pixels at full size (the piece's origin is its top-left corner; y is down in these numbers).
    public const float Width = 360f, Height = 170f, GlassX = 22f, GlassY = 32f, GlassW = 316f, GlassH = 100f, SillY = 128f, SillH = 42f;
    public const float Pad = 14f, TimeSize = 32f, DaySize = 22f, DaySizeMin = 20f;
    const float ArcRadius = 578f;   // the arch's circle: 22 px of rise across the glass
    const float SunTop = 18f, SunHorizon = 70f;   // the sun's path inside the glass: its noon height, and the roofline it rises from

    public static readonly Color Green = HudArt.Hex(0x1D4F3A), GreenLit = HudArt.Hex(0x2C6A4E), GreenDark = HudArt.Hex(0x143829);
    public static readonly Color Gilt = HudArt.Hex(0xF4D58D), Mint = HudArt.Hex(0xDCEFE1), GiltDark = HudArt.Hex(0x9A7A30);

    public RectTransform Rect { get; private set; }
    public TextMeshProUGUI TimeText { get; private set; }
    public TextMeshProUGUI DayText { get; private set; }
    public HudCity City { get; private set; }
    public float Scale { get; private set; } = 1f;

    RectTransform glass, cityRoot;
    HudGradient sky;
    Image sunGlow, sun, moonGlow, moon, stars, warmth, frame, goldLine, shadowIn, reflections;
    readonly Image[] clouds = new Image[2];
    readonly float[] cloudBase = new float[2];
    bool fine;
    float shownHour = float.MinValue;
    HudSky.Moment moment;
    string shownDay;
    float laidOutDaySize;
    /// <summary>True once the sun is below the roofs after closing.</summary>
    public bool SunSet => moment.sunP > 1.0f || !moment.sunUp;
    public bool MoonShowing => moment.moonAlpha > .5f;
    /// <summary>How far the sky has warmed toward golden hour (0 by day, 1 at closing).</summary>
    public float Warmth => Mathf.Max(moment.evening * (1f - moment.sunElev), moment.dusk);
    public bool NightLook { get; private set; }

    public void Build(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 at, float scale, bool fineDetail)
    {
        Scale = scale;
        fine = fineDetail;
        Rect = Box(name, parent, anchor, pivot, at, new Vector2(Width, Height));
        Rect.localScale = new Vector3(scale, scale, 1f);

        // The glass, clipped to its box; the frame over it hides the arch's corners.
        glass = Box("Glass", Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GlassX, -GlassY), new Vector2(GlassW, GlassH));
        glass.gameObject.AddComponent<RectMask2D>();
        var skyRect = Box("Sky", glass, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(GlassW, GlassH));
        skyRect.anchorMin = Vector2.zero; skyRect.anchorMax = Vector2.one; skyRect.offsetMin = skyRect.offsetMax = Vector2.zero;
        skyRect.gameObject.AddComponent<CanvasRenderer>();   // 26 Oct 2026 (his Mac): it had none, so the RectMask2D threw on every hide
        sky = skyRect.gameObject.AddComponent<HudGradient>();
        sky.raycastTarget = false;
        sky.middleAt = .45f;
        warmth = Picture("Sun's warmth", glass, HudArt.Glow(), new Vector2(GlassW * .5f, -GlassH * .7f), new Vector2(GlassW * 1.1f, GlassH * 1.3f));
        stars = Picture("Stars", glass, StarsSprite(), new Vector2(GlassW * .5f, -GlassH * .31f), new Vector2(GlassW, GlassH * .62f));
        stars.color = HudArt.Hex(0xFFF8E6, 0f);
        moonGlow = Picture("Moon glow", glass, HudArt.Glow(), Vector2.zero, new Vector2(46f, 46f));
        moon = Picture("Moon", glass, HudArt.Moon(), Vector2.zero, new Vector2(12f, 12f));
        if (fine)
        {
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i] = Picture("Cloud " + (i + 1), glass, HudArt.Cloud(), Vector2.zero, new Vector2(i == 0 ? 64f : 52f, i == 0 ? 27f : 22f));
                cloudBase[i] = i == 0 ? GlassW * .21f : GlassW * .66f;
            }
        }
        sunGlow = Picture("Sun glow", glass, HudArt.Glow(), Vector2.zero, new Vector2(54f, 54f));
        sun = Picture("Sun", glass, HudArt.Disc(64), Vector2.zero, new Vector2(15f, 15f));
        // The city: one swappable layer, drawn in its own box so a swap replaces only it.
        cityRoot = Box("City", glass, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(GlassW, GlassH));
        cityRoot.anchorMin = Vector2.zero; cityRoot.anchorMax = Vector2.one; cityRoot.offsetMin = cityRoot.offsetMax = Vector2.zero;
        SwapCity(HudCity.CityName);
        // The glass set back in its frame: an inner shadow inside its top and left edges, and two faint reflection bands.
        shadowIn = Picture("Inner shadow", glass, InnerShadowSprite(), new Vector2(GlassW * .5f, -GlassH * .5f), new Vector2(GlassW, GlassH));
        reflections = Picture("Reflections", glass, ReflectionSprite(), new Vector2(GlassW * .5f, -GlassH * .5f), new Vector2(GlassW, GlassH));

        // The frame and the sill, one painted piece; the gilt line on its own, so it can dim at night.
        var frameRect = Box("Frame", Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, Height));
        frame = frameRect.gameObject.AddComponent<Image>();
        frame.sprite = FrameSprite();
        frame.raycastTarget = false;
        var lineRect = Box("Gilt line", Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, Height));
        goldLine = lineRect.gameObject.AddComponent<Image>();
        goldLine.sprite = GiltSprite();
        goldLine.color = Gilt;
        goldLine.raycastTarget = false;

        // The time in gilt on the sill (its own dark drop), the day in mint at the right.
        TimeText = UiSkin.Text("Time", Rect, TimeSize, Gilt, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(TimeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad, -SillY), new Vector2(Width - 2f * Pad, SillH));
        UiSkin.Shadowed(TimeText);
        DayText = UiSkin.Text("Day", Rect, DaySize, Mint, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        Place(DayText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad, -SillY), new Vector2(Width - 2f * Pad, SillH));
        if (!fine)
        {
            // The bench's small window carries no words of its own (the strip writes the time beside it).
            TimeText.gameObject.SetActive(false);
            DayText.gameObject.SetActive(false);
        }
    }

    /// <summary>The city layer swapped for <paramref name="cityName"/> (the café's city; the franchise's next city later).</summary>
    public void SwapCity(string cityName)
    {
        if (City != null && City.Name == cityName) return;
        if (City != null) City.Destroy();
        City = HudCity.Make(cityName);
        City.Build(cityRoot, GlassW, GlassH, fine);
        shownHour = float.MinValue;
    }

    /// <summary>The hour (the café's clock, the night's small hours past 24 allowed), the day's words, and whether it's night.</summary>
    public void Set(float hour, string dayLabel, bool night)
    {
        NightLook = night;
        if (dayLabel != shownDay) { shownDay = dayLabel; DayText.text = dayLabel; if (fine) FitDay(); }   // the bench's small window writes no words
        if (Mathf.Abs(hour - shownHour) < 1f / 120f) return;
        shownHour = hour;
        moment = HudSky.At(hour);
        HudSky.Moment m = moment;
        sky.Set(m.top, m.mid, m.hor);
        // The sun: born behind the roofs on the city's rising side, down on its setting side.
        Vector2 path = City.SunPath;   // x at its rise, x at its set (0..1 of the glass)
        if (m.sunUp)
        {
            float pp = Mathf.Clamp(m.sunP, -.06f, 1.08f);
            float sx = Mathf.Lerp(path.x, path.y, pp) * GlassW;
            float arc = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pp));
            float sy = SunHorizon - (SunHorizon - SunTop) * arc;
            if (pp < 0f || pp > 1f) sy = SunHorizon + 10.5f * Mathf.Abs(pp - (pp < 0f ? 0f : 1f)) / .06f;
            float rad = 7.5f * m.sunSize;
            sun.rectTransform.anchoredPosition = new Vector2(sx, -sy);
            sun.rectTransform.sizeDelta = new Vector2(rad * 2f, rad * 2f);
            sun.color = Color.Lerp(m.sunColour, Color.white, .12f + .4f * m.sunElev);
            sunGlow.rectTransform.anchoredPosition = new Vector2(sx, -sy);
            sunGlow.rectTransform.sizeDelta = new Vector2(rad * 7.2f, rad * 7.2f);
            sunGlow.color = HudArt.With(Color.Lerp(m.sunColour, Color.white, .2f), .85f * m.sunGlow);
            float reach = GlassW * (.42f + .25f * (1f - m.sunElev));
            float strength = (.25f + .45f * (1f - m.sunElev)) * (pp <= 1f ? 1f : Mathf.Max(0f, 1f - (pp - 1f) / .1f));
            warmth.rectTransform.anchoredPosition = new Vector2(sx, -sy);
            warmth.rectTransform.sizeDelta = new Vector2(reach * 2f, reach * 1.6f);
            warmth.color = HudArt.With(Color.Lerp(m.sunColour, Color.white, .25f), strength);
            if (!sun.enabled) { sun.enabled = sunGlow.enabled = warmth.enabled = true; }
        }
        else if (sun.enabled) sun.enabled = sunGlow.enabled = warmth.enabled = false;
        // The moon, kept off the city's landmarks.
        if (m.moonUp && m.moonAlpha > .01f)
        {
            Vector2 box = City.MoonBox;
            float mx = Mathf.Lerp(box.x, box.y, .08f + .5f * m.moonP) * GlassW;
            float my = SunHorizon - (SunHorizon - SunTop) * (.25f + .7f * m.moonElev) * .95f;
            moon.rectTransform.anchoredPosition = new Vector2(mx, -my);
            moonGlow.rectTransform.anchoredPosition = new Vector2(mx, -my);
            moon.color = HudArt.With(HudArt.Hex(0xFBF5E2), m.moonAlpha);
            moonGlow.color = HudArt.With(HudArt.Hex(0xF1EBD8), .35f * m.moonAlpha);
            if (!moon.enabled) moon.enabled = moonGlow.enabled = true;
        }
        else if (moon.enabled) moon.enabled = moonGlow.enabled = false;
        stars.color = HudArt.With(HudArt.Hex(0xFFF8E6), m.night);
        // The clouds, tinted by the hour; placed where the city lets them.
        if (fine)
        {
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i].color = HudArt.With(m.cloud, m.cloudAlpha * .92f);
                Vector2 at = City.CloudAt(i);
                cloudBase[i] = at.x * GlassW;
                clouds[i].rectTransform.anchoredPosition = new Vector2(cloudBase[i], -at.y * GlassH);
            }
        }
        // Night: the frame's gold leaf dims to 62 % (the café is shut and Ace is sneaking).
        goldLine.color = HudArt.With(Gilt, night ? .62f : 1f);
        TimeText.color = night ? Color.Lerp(Gilt, Mint, .25f) : Gilt;
        City.Set(in m);
    }

    /// <summary>The tiny life: the clouds drift, the city's own small movements (fog, a cable car, beacons, a crown that breathes).</summary>
    public void Tick(float now)
    {
        if (City == null) return;
        if (fine)
            for (int i = 0; i < clouds.Length; i++)
                if (clouds[i] != null)
                {
                    Vector2 p = clouds[i].rectTransform.anchoredPosition;
                    p.x = cloudBase[i] + 6f * Mathf.Sin(now / (18f + 7f * i) + i * 2f);
                    clouds[i].rectTransform.anchoredPosition = p;
                }
        City.Tick(now, in moment);
    }

    /// <summary>The time's words (the sill's left), as the owner formats them.</summary>
    public void SetTime(string words)
    {
        if (TimeText.text != words) TimeText.text = words;
    }

    // The day line: 22 px, or the largest half-pixel step down to 20 that leaves the sill's gap (the time at its widest).
    static readonly string[] WidestTimes = { "12:00 PM", "10:00 AM", "10:00 PM" };
    const float LineGap = 10f;

    void FitDay()
    {
        float size = DaySize;
        DayText.fontSize = size;
        while (size > DaySizeMin + .01f && GapFor(DayText.text, WidestTime()) < LineGap)
        {
            size -= .5f;
            DayText.fontSize = size;
        }
        // 26 Oct 2026 (his Mac, PauseCheck): "Wednesday · Day 10" never fits beside "12:00 PM", even at the smallest size: the
        // weekday goes short ("Wed · Day 10") at the full size again, rather than squeezing it further.
        if (GapFor(DayText.text, WidestTime()) < LineGap)
        {
            string shorter = Shorter(DayText.text);
            if (shorter != DayText.text)
            {
                DayText.text = shorter;
                size = DaySize;
                DayText.fontSize = size;
                while (size > DaySizeMin + .01f && GapFor(DayText.text, WidestTime()) < LineGap)
                {
                    size -= .5f;
                    DayText.fontSize = size;
                }
            }
        }
        laidOutDaySize = DayText.fontSize;
    }

    /// <summary>"Wednesday · Day 10" → "Wed · Day 10": the weekday's first three letters (26 Oct 2026).</summary>
    public static string Shorter(string dayLabel)
    {
        if (string.IsNullOrEmpty(dayLabel)) return dayLabel;
        int dot = dayLabel.IndexOf(" · ", System.StringComparison.Ordinal);
        if (dot <= 3) return dayLabel;
        return dayLabel.Substring(0, 3) + dayLabel.Substring(dot);
    }

    public float DaySizeNow => DayText != null ? DayText.fontSize : 0f;

    float WidestTime()
    {
        float most = 0f;
        foreach (string t in WidestTimes) most = Mathf.Max(most, Measure(TimeText, t));
        return most;
    }

    /// <summary>The sill's room to spare with these words on it (the day at its size now, the time as given).</summary>
    public float GapFor(string dayLabel, string time)
    {
        if (DayText == null || TimeText == null) return 0f;
        return Width - 2f * Pad - Measure(DayText, dayLabel) - Measure(TimeText, time);
    }

    // 26 Oct 2026 (compile fix from his Mac): the same, with the time's width already measured (WidestTime()).
    public float GapFor(string dayLabel, float timeWidth)
    {
        if (DayText == null || TimeText == null) return 0f;
        return Width - 2f * Pad - Measure(DayText, dayLabel) - timeWidth;
    }

    /// <summary>The gap the sill keeps now, with the time at its widest.</summary>
    public float Gap => GapFor(DayText != null ? DayText.text : "", WidestTime());

    /// <summary>What GapFor would say for another day's label, the day line sized for it (the lines on screen are left as they are).</summary>
    public float GapForDay(string dayLabel, string time)
    {
        if (DayText == null) return 0f;
        float was = DayText.fontSize;
        string words = DayText.text;
        float gap = 0f;
        // 26 Oct 2026 (his Mac): as FitDay lays it, the long weekday first, then the short one ("Wed · Day 10").
        foreach (string label in new[] { dayLabel, Shorter(dayLabel) })
        {
            for (float size = DaySize; size >= DaySizeMin - .01f; size -= .5f)
            {
                DayText.fontSize = size;
                gap = Width - 2f * Pad - Measure(DayText, label) - Measure(TimeText, time);
                if (gap >= LineGap) break;
            }
            if (gap >= LineGap) break;
        }
        DayText.fontSize = was;
        DayText.text = words;
        return gap;
    }

    // TextMesh Pro measures its own words: other words are set in for the measure and put back, within the frame.
    static float Measure(TMP_Text text, string words)
    {
        string was = text.text;
        if (was == words) return text.GetPreferredValues(999f, 999f).x;
        text.text = words;
        float width = text.GetPreferredValues(999f, 999f).x;
        text.text = was;
        return width;
    }

    // ------------------------------------------------------------------ the pictures

    // The arch's top edge at x (piece pixels, y down): the glass's boundary.
    static float ArcTop(float x)
    {
        float dx = x - Width * .5f;
        float inside = ArcRadius * ArcRadius - dx * dx;
        return GlassY + ArcRadius - (inside > 0f ? Mathf.Sqrt(inside) : 0f);
    }

    // Signed distance (y down, piece pixels) to the glass: the box, and below the arch (the arch is shallow, so its vertical
    // distance serves for the edge's smoothing).
    static float GlassShape(float x, float yDown)
    {
        float box = HudArt.RoundBox(x, yDown, Width * .5f, GlassY + GlassH * .5f, GlassW * .5f, GlassH * .5f, 1f);
        return Mathf.Max(box, ArcTop(x) - yDown);
    }

    static float OuterShape(float x, float yDown)
    {
        // The frame's outline: the arch grown by 22 px, straight sides, the sill's rounded box at the bottom.
        float dx = x - Width * .5f;
        float outerR = ArcRadius + 22f;
        float inside = outerR * outerR - dx * dx;
        float top = GlassY + ArcRadius - (inside > 0f ? Mathf.Sqrt(inside) : 0f);
        float arch = Mathf.Max(top - yDown, Mathf.Max(Mathf.Abs(dx) - Width * .5f, yDown - (SillY + 8f)));
        float sill = HudArt.RoundBox(x, yDown, Width * .5f, SillY + SillH * .5f, Width * .5f, SillH * .5f, 8f);
        return Mathf.Min(arch, sill);
    }

    static Sprite FrameSprite() => HudArt.Cached("Window frame", () => HudArt.SpriteOf(HudArt.Draw("Window frame", (int)Width, (int)Height, (x, y) =>
    {
        float yd = Height - y;   // the painter's y is up
        float outer = OuterShape(x, yd);
        // The keystone sits proud at the top centre.
        float key = HudArt.RoundBox(x, yd, Width * .5f, 10f, 12f, 10f, 2f);
        float shape = Mathf.Min(outer, key);
        if (shape > 1f) return Color.clear;
        float glass = GlassShape(x, yd);
        float alpha = HudArt.Inside(shape) * (key < 0f || yd >= SillY - 1f ? 1f : 1f - HudArt.Inside(glass));   // the sill is whole under the glass's foot
        if (alpha <= 0f) return Color.clear;
        Color c = HudArt.With(Green, alpha);
        // Light from the top left: a lit lip along the outer top and left edges, a darker edge bottom right.
        float edge = Mathf.Clamp01(1f - (-shape) / 2.2f);
        bool litSide = x < Width * .5f || yd < ArcTop(x) + 6f;
        float lip = edge * (yd < SillY ? 1f : 0f) * (litSide ? 1f : .35f);
        c = HudArt.Over(c, HudArt.With(GreenLit, .85f * lip));
        float dark = edge * (x > Width * .5f ? 1f : .3f) * (yd > SillY ? 1f : .5f);
        c = HudArt.Over(c, HudArt.With(GreenDark, .6f * dark));
        // The glass's rebate: a dark line just inside the frame round the glass, so the glass sits back.
        c = HudArt.Over(c, HudArt.With(GreenDark, .7f * HudArt.Line(glass - 1.2f, .8f) * (yd < SillY ? 1f : 0f)));
        // The sill's top edge catches the light.
        c = HudArt.Over(c, HudArt.With(GreenLit, .9f * HudArt.Line(yd - SillY - 1f, .8f) * HudArt.Inside(Mathf.Abs(x - Width * .5f) - Width * .5f + 4f)));
        return HudArt.With(c, alpha);
    })));

    static Sprite GiltSprite() => HudArt.Cached("Window gilt", () => HudArt.SpriteOf(HudArt.Draw("Window gilt", (int)Width, (int)Height, (x, y) =>
    {
        float yd = Height - y;
        // A fine line 5 px outside the glass, following the arch and the sides; another inset in the sill; the keystone's bead.
        float glass = GlassShape(x, yd);
        float arch = yd < SillY - 2f ? HudArt.Line(glass - 5f, .8f) : 0f;
        float sill = HudArt.Line(HudArt.RoundBox(x, yd, Width * .5f, SillY + SillH * .5f, Width * .5f - 5f, SillH * .5f - 5f, 5f), .7f);
        float bead = HudArt.Inside(new Vector2(x - Width * .5f, yd - 10f).magnitude - 3f);
        return new Color(1f, 1f, 1f, Mathf.Max(Mathf.Max(arch * .9f, sill * .8f), bead));
    })));

    static Sprite InnerShadowSprite() => HudArt.Cached("Glass shadow", () => HudArt.SpriteOf(HudArt.Draw("Glass shadow", (int)GlassW, (int)GlassH, (x, y) =>
    {
        float yd = GlassH - y;
        float top = Mathf.Clamp01(1f - yd / 12f), left = Mathf.Clamp01(1f - x / 9f), right = Mathf.Clamp01(1f - (GlassW - x) / 5f);
        float a = Mathf.Max(top * .42f, Mathf.Max(left * .3f, right * .12f));
        return new Color(.05f, .06f, .1f, a * a * .9f + a * .1f);
    })));

    static Sprite ReflectionSprite() => HudArt.Cached("Glass reflections", () => HudArt.SpriteOf(HudArt.Draw("Glass reflections", (int)GlassW, (int)GlassH, (x, y) =>
    {
        // Two faint diagonal bands of light across the glass.
        float u = x + y * .9f;
        float a1 = Mathf.Clamp01(1f - Mathf.Abs(u - 112f) / 16f), a2 = Mathf.Clamp01(1f - Mathf.Abs(u - 150f) / 8f);
        return new Color(1f, 1f, 1f, .07f * a1 + .05f * a2);
    })));

    static Sprite StarsSprite() => HudArt.Cached("Stars", () =>
    {
        var r = new HudArt.Raster((int)GlassW, (int)(GlassH * .62f));
        var rand = new System.Random(3);
        for (int i = 0; i < 18; i++)
        {
            float px = (float)rand.NextDouble() * GlassW, py = (float)rand.NextDouble() * (GlassH * .62f) * .95f, b = .45f + .55f * (float)rand.NextDouble();
            float rad = .55f + .75f * b;
            r.Glow(px, py, rad * 1.8f, HudArt.With(Color.white, b), 1.2f);
            if (rand.NextDouble() > .82) { r.Stroke(px - rad * 3f, py, px + rad * 3f, py, .8f, HudArt.With(Color.white, b * .7f)); r.Stroke(px, py - rad * 3f, px, py + rad * 3f, .8f, HudArt.With(Color.white, b * .7f)); }
        }
        return r.ToSprite("Stars");
    });

    // ------------------------------------------------------------------ plumbing

    public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, anchor, pivot, position, size);
        return rect;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>An Image under <paramref name="parent"/> (anchored at its top left, pivot in the middle), never catching the pointer.</summary>
    public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 at, Vector2 size)
    {
        RectTransform rect = Box(name, parent, new Vector2(0f, 1f), new Vector2(.5f, .5f), at, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }
}
