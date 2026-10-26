using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE HUD'S PICTURES, DRAWN IN CODE ONCE (26 Oct 2026 (HUD 3); his answers of 24 and 26 Oct)
//
// Everything the HUD shows that isn't a word is drawn here while playing, at the moment it is first asked for, and kept for
// the session: nothing is imported and nothing is saved. Two ways of drawing, both two-by-two samples a pixel so every edge
// is smooth:
//   * a PAINTER (Draw): a function of (x, y) that says the colour at that point, built from signed distances (a rounded box,
//     an ellipse, a line, a five-pointed star). The old HudCorners painter, moved here so every HUD file shares it;
//   * a RASTER: a small RGBA canvas that fills polygons, ellipses, soft glows and bands on top of each other (Over), for the
//     city in the window and the jar, where a scene is dozens of shapes. Polygons are filled by scanline with exact
//     horizontal coverage and two sub-rows a pixel.
// The sprites below are the HUD's family: the ring round a face, the docket paper's torn foot, his bill, a coin, the jar and
// what's in it, a star, the little café front, a cloud, a glow, the moon, the sign's cup, a sparkle, a brass scroll.
// White where a sprite is tinted by its Image (the sky's hour tints the city), coloured where the colour is the picture.
// Sprites are cached by name; the cache empties on a domain reload (the textures are DontSave and die with the session).
// ---------------------------------------------------------------------------
public static class HudArt
{
    public delegate Color Painter(float x, float y);

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cache.Clear();

    // ------------------------------------------------------------------ the painter

    /// <summary>A texture painted by <paramref name="paint"/> (reference pixels), two by two samples a pixel.</summary>
    public static Texture2D Draw(string name, int width, int height, Painter paint)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD - " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Color c = paint(x + .25f + sx * .5f, y + .25f + sy * .5f);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                sum /= 4f;
                pixels[y * width + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>A sprite of a whole texture (a 9-slice border in pixels when asked; the pivot in the middle unless given).</summary>
    public static Sprite SpriteOf(Texture2D texture, float border = 0f, Vector2? pivot = null)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot ?? new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>A sprite made once per session for <paramref name="key"/>.</summary>
    public static Sprite Cached(string key, Func<Sprite> make)
    {
        if (cache.TryGetValue(key, out Sprite had) && had != null) return had;
        Sprite made = make();
        cache[key] = made;
        return made;
    }

    static Sprite Painted(string key, int w, int h, Painter paint, float border = 0f) => Cached(key, () => SpriteOf(Draw(key, w, h, paint), border));

    // ---------- signed distances (negative inside) ----------

    public static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f) return Color.clear;
        Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
        c.a = a;
        return c;
    }

    /// <summary>Signed distance to a rounded box centred at (cx, cy), half sizes (hx, hy), corner radius r.</summary>
    public static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry) =>
        (new Vector2((x - cx) / rx, (y - cy) / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    /// <summary>Signed distance to a five-pointed star (points r out, inner corners at rf of r; Inigo Quilez's sdStar5).</summary>
    public static float StarShape(float x, float y, float cx, float cy, float r, float rf)
    {
        const float k1x = .809016994f, k1y = -.587785252f, k2x = -k1x, k2y = k1y;
        float px = Mathf.Abs(x - cx), py = y - cy;
        float d1 = Mathf.Max(k1x * px + k1y * py, 0f);
        px -= 2f * d1 * k1x; py -= 2f * d1 * k1y;
        float d2 = Mathf.Max(k2x * px + k2y * py, 0f);
        px -= 2f * d2 * k2x; py -= 2f * d2 * k2y;
        px = Mathf.Abs(px);
        py -= r;
        float bax = rf * -k1y, bay = rf * k1x - 1f;
        float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
        float dx = px - bax * h, dy = py - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(py * bax - px * bay);
    }

    /// <summary>Distance from a segment (ax, ay)-(bx, by).</summary>
    public static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay, wx = x - ax, wy = y - ay;
        float len = vx * vx + vy * vy;
        float t = len > 0f ? Mathf.Clamp01((wx * vx + wy * vy) / len) : 0f;
        float dx = wx - vx * t, dy = wy - vy * t;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public static float Inside(float d) => Mathf.Clamp01(.5f - d);
    public static float Line(float d, float half) => Mathf.Clamp01(.5f - (Mathf.Abs(d) - half));
    public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    public static Color With(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    public static Color Hex(int rgb, float alpha = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

    // ------------------------------------------------------------------ the raster

    /// <summary>
    /// A small RGBA canvas (straight alpha), y down as the painter's is not: here (0, 0) is the TOP left, as a design is
    /// read, and ToTexture flips it. Shapes are laid over what is there (Over).
    /// </summary>
    public sealed class Raster
    {
        public readonly int Width, Height;
        readonly Color[] px;
        readonly List<float> crossings = new List<float>(16);

        public Raster(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            px = new Color[Width * Height];
        }

        public void Clear(Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        void Blend(int x, int y, Color c, float coverage)
        {
            if (coverage <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            px[i] = Over(px[i], With(c, c.a * Mathf.Clamp01(coverage)));
        }

        /// <summary>A polygon (design pixels, y down) filled with <paramref name="c"/>: two sub-rows a pixel, exact coverage across.</summary>
        public void Polygon(IList<Vector2> pts, Color c)
        {
            if (pts == null || pts.Count < 3 || c.a <= 0f) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++) { minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y); }
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
            for (int y = y0; y <= y1; y++)
            {
                for (int sub = 0; sub < 2; sub++)
                {
                    float sy = y + .25f + sub * .5f;
                    crossings.Clear();
                    for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                    {
                        Vector2 a = pts[j], b = pts[i];
                        if ((a.y <= sy) == (b.y <= sy)) continue;   // the edge doesn't cross this row
                        float t = (sy - a.y) / (b.y - a.y);
                        crossings.Add(a.x + (b.x - a.x) * t);
                    }
                    if (crossings.Count < 2) continue;
                    crossings.Sort();
                    for (int k = 0; k + 1 < crossings.Count; k += 2)
                    {
                        float xa = Mathf.Max(0f, crossings[k]), xb = Mathf.Min(Width, crossings[k + 1]);
                        if (xb <= xa) continue;
                        int xs = Mathf.FloorToInt(xa), xe = Mathf.CeilToInt(xb);
                        for (int x = xs; x < xe; x++)
                        {
                            float cover = Mathf.Clamp01(Mathf.Min(xb, x + 1f) - Mathf.Max(xa, x)) * .5f;
                            Blend(x, y, c, cover);
                        }
                    }
                }
            }
        }

        public void Polygon(Color c, params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            Polygon(pts, c);
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c) => Polygon(c, x0, y0, x1, y0, x1, y1, x0, y1);

        /// <summary>An ellipse, its edge softened over <paramref name="soft"/> pixels (0.5 for a crisp edge).</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = .5f)
        {
            if (rx <= 0f || ry <= 0f || c.a <= 0f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - soft - 1f)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + rx + soft + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - soft - 1f)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + ry + soft + 1f));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = HudArt.Ellipse(x + .5f, y + .5f, cx, cy, rx, ry);
                    float a = Mathf.Clamp01(.5f - d / Mathf.Max(.5f, soft) * .5f);
                    if (soft <= .5f) a = Inside(d);
                    Blend(x, y, c, a);
                }
        }

        /// <summary>A soft round glow: full at the middle, gone at r, with <paramref name="power"/> shaping the fall.</summary>
        public void Glow(float cx, float cy, float r, Color c, float power = 2f)
        {
            if (r <= 0f || c.a <= 0f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + r));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = new Vector2(x + .5f - cx, y + .5f - cy).magnitude / r;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    Blend(x, y, c, a);
                }
        }

        /// <summary>A thick line from a to b (round ends).</summary>
        public void Stroke(float ax, float ay, float bx, float by, float width, Color c)
        {
            float half = width * .5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - half - 1f)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + half + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - half - 1f)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + half + 1f));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Blend(x, y, c, Inside(Segment(x + .5f, y + .5f, ax, ay, bx, by) - half));
        }

        /// <summary>Every pixel's alpha multiplied by <paramref name="keep"/>(x, y) (design pixels, y down): a soft clip.</summary>
        public void Mask(System.Func<float, float, float> keep)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a > 0f) px[i] = With(px[i], px[i].a * Mathf.Clamp01(keep(x + .5f, y + .5f)));
                }
        }

        /// <summary>Every pixel's colour multiplied toward <paramref name="c"/> by <paramref name="amount"/> where the canvas is painted (a shade).</summary>
        public void Tint(Color c, float amount)
        {
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0f) px[i] = new Color(Mathf.Lerp(px[i].r, c.r, amount), Mathf.Lerp(px[i].g, c.g, amount), Mathf.Lerp(px[i].b, c.b, amount), px[i].a);
        }

        /// <summary>The canvas as a texture (flipped so y is up, as Unity reads it).</summary>
        public Texture2D ToTexture(string name)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "HUD - " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var flipped = new Color[px.Length];
            for (int y = 0; y < Height; y++) Array.Copy(px, y * Width, flipped, (Height - 1 - y) * Width, Width);
            texture.SetPixels(flipped);
            texture.Apply(false, true);
            return texture;
        }

        public Sprite ToSprite(string name) => SpriteOf(ToTexture(name));
    }

    // ------------------------------------------------------------------ the family's sprites (white unless the colour is the picture)

    /// <summary>A ring: <paramref name="px"/> across, its stroke <paramref name="stroke"/> px wide (drawn at twice the size, smoothed).</summary>
    public static Sprite Ring(int px, float stroke) => Painted($"Ring {px} {stroke:0.0}", px * 2, px * 2, (x, y) =>
    {
        float outer = px - 1f, inner = outer - stroke * 2f;
        float d = new Vector2(x - px, y - px).magnitude;
        return new Color(1f, 1f, 1f, Mathf.Clamp01(outer - d + .5f) * Mathf.Clamp01(d - inner + .5f));
    });

    /// <summary>A disc, its edge smoothed (the skin's Circle is the same; this one is cached here for the HUD's masks).</summary>
    public static Sprite Disc(int px) => Painted($"Disc {px}", px, px, (x, y) =>
        new Color(1f, 1f, 1f, Inside(new Vector2(x - px * .5f, y - px * .5f).magnitude - (px * .5f - 1f))));

    /// <summary>The docket paper's torn foot: a tile 16 px wide and 10 tall, teeth along its bottom, tiled across a card's foot.</summary>
    public static Sprite TornFoot() => Painted("Torn foot", 16, 10, (x, y) =>
    {
        // y is up: the tile is paper (white) above a saw-tooth line, clear below it.
        float tooth = 3f + 4f * Mathf.Abs(Mathf.Repeat(x / 8f, 2f) - 1f);   // 3..7 px up, period 16
        return new Color(1f, 1f, 1f, Mathf.Clamp01(y - tooth + .5f));
    });

    /// <summary>A soft four-point sparkle.</summary>
    public static Sprite Sparkle() => Painted("Sparkle", 32, 32, (x, y) =>
    {
        float dx = Mathf.Abs(x - 16f), dy = Mathf.Abs(y - 16f);
        float arm = Mathf.Max(Mathf.Clamp01(1.2f - dx - dy * .18f) * Mathf.Clamp01(1f - dx / 15f), Mathf.Clamp01(1.2f - dy - dx * .18f) * Mathf.Clamp01(1f - dy / 15f));
        float core = Mathf.Clamp01(1f - new Vector2(dx, dy).magnitude / 3.2f);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(arm, core)));
    });

    /// <summary>A soft round glow, bright in the middle (tinted).</summary>
    public static Sprite Glow() => Painted("Glow", 96, 96, (x, y) =>
    {
        float d = new Vector2(x - 48f, y - 48f).magnitude / 48f;
        float a = Mathf.Clamp01(1f - d);
        return new Color(1f, 1f, 1f, a * a);
    });

    /// <summary>A crescent: a disc with another taken out of its right side.</summary>
    public static Sprite Moon() => Painted("Moon", 40, 40, (x, y) =>
    {
        float disc = Ellipse(x, y, 18f, 20f, 15f, 15f);
        float bite = Ellipse(x, y, 27f, 23f, 13f, 13f);
        return new Color(1f, 1f, 1f, Inside(Mathf.Max(disc, -bite)));
    });

    /// <summary>A five-pointed star.</summary>
    public static Sprite Star() => Painted("Star", 64, 64, (x, y) => new Color(1f, 1f, 1f, Inside(StarShape(x, y, 32f, 30f, 29f, .48f))));

    /// <summary>The sign's cup: a cup, its handle, three wisps of steam.</summary>
    public static Sprite CupMark() => Painted("Cup mark", 60, 52, (x, y) =>
    {
        float body = Mathf.Abs(RoundBox(x, y, 25f, 16f, 15f, 12f, 6f)) - 2.2f;
        float handle = Mathf.Abs(Ellipse(x, y, 42f, 17f, 6f, 6f)) - 2.2f;
        if (x < 39f) handle = Mathf.Max(handle, 2.5f);
        float d = Mathf.Min(body, handle);
        for (int i = -1; i <= 1; i++)
        {
            float sx = 25f + i * 8f;
            float wave = Mathf.Abs(x - (sx + 2f * Mathf.Sin((y - 32f) * .35f))) - 1.6f;
            if (y > 32f && y < 48f) d = Mathf.Min(d, wave);
        }
        return new Color(1f, 1f, 1f, Inside(d));
    });

    /// <summary>A cloud: four soft lumps, a little less solid toward its foot (tinted by the hour).</summary>
    public static Sprite Cloud() => Painted("Cloud", 96, 40, (x, y) =>
    {
        float d = Mathf.Min(Mathf.Min(Ellipse(x, y, 48f, 16f, 30f, 11f), Ellipse(x, y, 30f, 13f, 17f, 9f)),
                            Mathf.Min(Ellipse(x, y, 64f, 14f, 19f, 10f), Ellipse(x, y, 50f, 22f, 16f, 11f)));
        float a = Mathf.Clamp01(.5f - d / 2.5f);
        return new Color(1f, 1f, 1f, a * Mathf.Lerp(.72f, 1f, Mathf.Clamp01((y - 6f) / 14f)));
    });

    /// <summary>A banknote in the brand's green: a dark edge, a light frame inside it, an oval with a portrait's shape in the middle (his bill).</summary>
    public static Sprite Bill() => Painted("Bill", 140, 62, (x, y) =>
    {
        const int w = 140, h = 62;
        Color paper = Color.Lerp(UiSkin.Brand, Color.white, .28f), edge = UiSkin.BrandDeep;
        Color frame = new Color(1f, 1f, 1f, .38f), oval = new Color(1f, 1f, 1f, .2f), ring = new Color(1f, 1f, 1f, .55f);
        Color face = With(UiSkin.BrandDeep, .42f);
        float outer = RoundBox(x, y, w / 2f, h / 2f, w / 2f - 1f, h / 2f - 1f, 6f);
        if (outer > 1f) return Color.clear;
        Color c = With(edge, Inside(outer));
        c = Over(c, With(paper, Inside(outer + 3f)));
        c = Over(c, With(frame, frame.a * Line(RoundBox(x, y, w / 2f, h / 2f, w / 2f - 9f, h / 2f - 9f, 3f), .9f)));
        float o = Ellipse(x, y, w / 2f, h / 2f, 19f, 17f);
        c = Over(c, With(oval, oval.a * Inside(o)));
        c = Over(c, With(ring, ring.a * Line(o, .9f)));
        c = Over(c, With(face, face.a * Inside(Ellipse(x, y, w / 2f, h / 2f + 4f, 5.5f, 6f))));
        c = Over(c, With(face, face.a * Inside(Mathf.Max(Ellipse(x, y, w / 2f, h / 2f - 11f, 10f, 7f), (h / 2f - 9f) - y))));
        foreach (Vector2 m in new[] { new Vector2(17f, h - 17f), new Vector2(w - 17f, 17f) })
            c = Over(c, new Color(1f, 1f, 1f, .5f * Inside(Ellipse(x, y, m.x, m.y, 6f, 6f))));
        return c;
    });

    /// <summary>A gold coin seen face on: a darker rim, a lighter disc, a highlight crescent, a small cup stamped in it.</summary>
    public static Sprite Coin() => Painted("Coin", 48, 48, (x, y) =>
    {
        Color rim = Hex(0xB8780E), face = Hex(0xF2B23A), light = Hex(0xFFE08A), stamp = Hex(0xC98A1A);
        float d = new Vector2(x - 24f, y - 24f).magnitude;
        if (d > 23.6f) return Color.clear;
        Color c = With(rim, Inside(d - 23f));
        c = Over(c, With(face, Inside(d - 19.5f)));
        float hi = Inside(Ellipse(x, y, 19f, 29f, 8f, 5.5f));
        c = Over(c, With(light, .55f * hi));
        // A small cup in the middle, stamped a shade darker.
        float cup = Mathf.Abs(RoundBox(x, y, 23f, 21f, 6f, 5f, 2.5f)) - 1.4f;
        float handle = Mathf.Abs(Ellipse(x, y, 31f, 21.5f, 3f, 3f)) - 1.3f;
        if (x < 29f) handle = Mathf.Max(handle, 2f);
        c = Over(c, With(stamp, .8f * Inside(Mathf.Min(cup, handle))));
        return c;
    });

    // ---------- the jar (48 x 60 on screen, drawn at twice that) ----------

    const int JarW = 96, JarH = 120;

    // The jar's silhouette as a signed distance (texture pixels, y up): the body with round corners under a short neck and lip.
    static float JarBody(float x, float y) => RoundBox(x, y, 48f, 44f, 44f, 42f, 16f);
    static float JarNeck(float x, float y) => RoundBox(x, y, 48f, 92f, 30f, 10f, 4f);
    static float JarLip(float x, float y) => RoundBox(x, y, 48f, 106f, 36f, 7f, 6f);
    static float JarShape(float x, float y) => Mathf.Min(JarBody(x, y), Mathf.Min(JarNeck(x, y), JarLip(x, y)));

    /// <summary>The jar's back: the glass seen through, a shade of the room behind it.</summary>
    public static Sprite JarBack() => Painted("Jar back", JarW, JarH, (x, y) =>
    {
        float d = JarShape(x, y);
        if (d > 1f) return Color.clear;
        // Pale glass, a little darker at its sides.
        float edge = Mathf.Clamp01(1f - Mathf.Abs(x - 48f) / 46f);
        Color glass = Color.Lerp(Hex(0xA9C6CC, .34f), Hex(0xE4F1F3, .2f), edge);
        return With(glass, glass.a * Inside(d));
    });

    /// <summary>What's in the jar: a pile of gold coins and two of his bills, filling from the bottom (a RectMask2D shows the day's share).</summary>
    public static Sprite JarContents() => Cached("Jar contents", () =>
    {
        var r = new Raster(JarW, JarH);
        Color coin = Hex(0xF2B23A), coinEdge = Hex(0xB8780E), coinLight = Hex(0xFFE08A);
        Color bill = Color.Lerp(UiSkin.Brand, Color.white, .24f), billEdge = UiSkin.BrandDeep;
        // Two of his bills standing in the back, leaning on the glass, their tops up in the neck above the label.
        r.Polygon(billEdge, 12, 109, 29, 105, 47, 28, 33, 32);
        r.Polygon(bill, 14, 107, 28, 104, 45, 30, 34, 33);
        r.Polygon(With(Color.white, .3f), 18, 102, 26, 100, 42, 36, 36, 38);
        r.Polygon(billEdge, 84, 109, 67, 105, 49, 28, 63, 32);
        r.Polygon(bill, 82, 107, 68, 104, 51, 30, 62, 33);
        r.Polygon(With(Color.white, .3f), 78, 102, 70, 100, 54, 36, 60, 38);
        // Coins stacked in three leaning columns, each a flat ellipse with a darker rim and a light top.
        int[] columns = { 22, 48, 74 };
        int[] counts = { 6, 7, 5 };
        for (int c = 0; c < columns.Length; c++)
            for (int i = 0; i < counts[c]; i++)
            {
                float cx = columns[c] + (c == 1 ? 0f : (c == 0 ? 1f : -1f)) * i * .4f + ((i % 2 == 0) ? -1.2f : 1.2f);
                float cy = 118f - i * 7.2f - (c == 1 ? 0f : 3f);
                r.Ellipse(cx, cy + 2.4f, 12.5f, 4.4f, coinEdge);
                r.Ellipse(cx, cy, 12.5f, 4.2f, coin);
                r.Ellipse(cx - 3f, cy - 1.1f, 6f, 1.8f, With(coinLight, .75f));
            }
        // A few loose coins lying on the pile.
        r.Ellipse(36f, 70f, 12.5f, 4.4f, coinEdge);
        r.Ellipse(36f, 68f, 12.5f, 4.2f, coin);
        r.Ellipse(33f, 67f, 6f, 1.8f, With(coinLight, .75f));
        r.Ellipse(62f, 78f, 12.5f, 4.4f, coinEdge);
        r.Ellipse(62f, 76f, 12.5f, 4.2f, coin);
        r.Ellipse(59f, 75f, 6f, 1.8f, With(coinLight, .75f));
        // Clipped to the jar: outside its glass the contents are cut away (the raster's y is down; the jar's shape is y up).
        r.Mask((x, yDown) => Inside(JarShape(x, JarH - yDown) + 3f));
        return r.ToSprite("Jar contents");
    });

    /// <summary>The jar's front: the glass's edge and lip, a highlight down its left, a shade down its right, a paper label with the cup.</summary>
    public static Sprite JarFront() => Painted("Jar front", JarW, JarH, (x, y) =>
    {
        float d = JarShape(x, y);
        if (d > 1.5f) return Color.clear;
        Color c = Color.clear;
        // The glass edge all round, and the lip's rim brighter.
        c = Over(c, new Color(1f, 1f, 1f, .62f * Line(d, 1.4f)));
        c = Over(c, new Color(.86f, .95f, .97f, .5f * Inside(JarLip(x, y) + 2f) * Mathf.Clamp01((y - 100f) / 6f)));
        // A soft highlight stripe down the left of the body, a cooler shade down the right.
        float stripe = Mathf.Clamp01(1f - Mathf.Abs(x - 16f) / 7f) * Mathf.Clamp01((y - 6f) / 10f) * Mathf.Clamp01((84f - y) / 10f);
        c = Over(c, new Color(1f, 1f, 1f, .38f * stripe * Inside(d + 2f)));
        float shade = Mathf.Clamp01((x - 70f) / 22f) * Inside(d + 1.5f);
        c = Over(c, new Color(.2f, .35f, .4f, .16f * shade));
        // The label: cream paper with a green border and the cup mark, tied with a thread at the neck.
        float label = RoundBox(x, y, 48f, 54f, 22f, 15f, 4f);
        c = Over(c, With(UiSkin.Paper, Inside(label)));
        c = Over(c, With(UiSkin.Brand, Line(label + 3f, .9f)));
        float cupBody = Mathf.Abs(RoundBox(x, y, 46f, 51f, 7f, 5.5f, 2.5f)) - 1.5f;
        float cupHandle = Mathf.Abs(Ellipse(x, y, 55f, 52f, 3.2f, 3.2f)) - 1.4f;
        if (x < 53f) cupHandle = Mathf.Max(cupHandle, 2f);
        float steam = Mathf.Min(Mathf.Abs(x - (42f + 1.2f * Mathf.Sin((y - 58f) * .8f))) - 1f, Mathf.Abs(x - (49f + 1.2f * Mathf.Sin((y - 58f) * .8f + 1f))) - 1f);
        if (y < 58f || y > 65f) steam = 9f;
        c = Over(c, With(UiSkin.Brand, Inside(Mathf.Min(Mathf.Min(cupBody, cupHandle), steam))));
        float thread = Mathf.Abs(y - 97f) - .9f;
        c = Over(c, With(Hex(0x8C5A2B), .85f * Inside(thread) * Inside(JarNeck(x, y) + 1f)));
        return c;
    });

    /// <summary>The little café front at the end of the stars' road: a green shop with a striped awning, a door, two windows (26 x 24 on screen).</summary>
    public static Sprite CafeFront(bool lit) => Painted(lit ? "Cafe front lit" : "Cafe front", 52, 48, (x, y) =>
    {
        Color wall = UiSkin.BrandDeep, trim = Hex(0xE7C56B), cream = Hex(0xF6EFE4), stripe = UiSkin.Brand;
        Color glass = lit ? Hex(0xFFD98E) : Hex(0xBFD6D8), door = lit ? Hex(0xFFC66B) : Hex(0xA9BEC2);
        Color c = Color.clear;
        // The wall and its fascia.
        c = Over(c, With(wall, Inside(RoundBox(x, y, 26f, 20f, 24f, 20f, 3f))));
        c = Over(c, With(trim, Inside(RoundBox(x, y, 26f, 40f, 25f, 2.2f, 1f))));
        // The awning: scalloped stripes under the fascia.
        float awn = RoundBox(x, y, 26f, 33f, 25f, 4.5f, 2f);
        float scallop = Mathf.Repeat(x, 8f);
        float scallopEdge = y - (28.5f + 1.6f * Mathf.Sin(scallop / 8f * Mathf.PI));
        float awning = Mathf.Max(awn, -scallopEdge);
        bool light = Mathf.FloorToInt(x / 8f) % 2 == 0;
        c = Over(c, With(light ? cream : stripe, Inside(awning)));
        // Two windows and a door, with a step.
        c = Over(c, With(glass, Inside(RoundBox(x, y, 11f, 16f, 6f, 7f, 1.5f))));
        c = Over(c, With(glass, Inside(RoundBox(x, y, 41f, 16f, 6f, 7f, 1.5f))));
        c = Over(c, With(door, Inside(RoundBox(x, y, 26f, 12f, 5.5f, 11f, 2f))));
        c = Over(c, With(trim, Inside(RoundBox(x, y, 26f, 1.5f, 8f, 1.5f, .5f))));
        if (lit) c = Over(c, new Color(1f, .9f, .6f, .35f * Inside(RoundBox(x, y, 26f, 14f, 22f, 12f, 4f) + 6f)));
        return c;
    });

    /// <summary>A brass scroll: three quarters of a ring with a ball at its end (the sign's bracket, 24 px).</summary>
    public static Sprite Scroll() => Painted("Scroll", 48, 48, (x, y) =>
    {
        float d = new Vector2(x - 24f, y - 24f).magnitude;
        float ang = Mathf.Atan2(y - 24f, x - 24f);   // -pi..pi; the gap at the top right
        float ring = Mathf.Abs(d - 15f) - 3.2f;
        if (ang > .2f && ang < 1.4f) ring = 9f;
        float ball = new Vector2(x - (24f + 15f * Mathf.Cos(1.4f)), y - (24f + 15f * Mathf.Sin(1.4f))).magnitude - 4.6f;
        float ball2 = new Vector2(x - (24f + 15f * Mathf.Cos(.2f)), y - (24f + 15f * Mathf.Sin(.2f))).magnitude - 3.6f;
        return new Color(1f, 1f, 1f, Inside(Mathf.Min(ring, Mathf.Min(ball, ball2))));
    });
}
