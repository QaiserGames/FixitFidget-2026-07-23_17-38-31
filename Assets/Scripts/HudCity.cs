using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE CITY IN THE WINDOW: ONE SWAPPABLE LAYER (26 Oct 2026 (HUD 3))
//
// His note (24 Oct): "for the artwork that we selected for the hud, for example the skyline in the option a, make sure its
// apparent that we are in san francisco, i like this because we can always change the hud to match the cafe is as of
// currently during the franchising." So the window draws ONE sky (HudSky) and ONE city in front of it, and a city is data:
// its shapes in layers (far, landmarks, near), each a sprite drawn once from polygons and tinted every minute by the hour's
// colours, its night lights, its weather (fog in San Francisco; drizzle on the glass in the placeholder) and where the sun,
// the moon and the clouds may go so they never cover a landmark. HudCity.CityName picks the city (San Francisco until the
// café franchises; the Franchise work sets it); Seattle is a placeholder (his answer h4, 26 Oct), not a decision.
//
// San Francisco, read through a window that faces north from a hill (so the sun rises on the right, behind the houses, and
// sets on the left behind the Golden Gate, as it really does): the Golden Gate's two towers and cables in International
// Orange (#C0362C as it shows by day), fog in the Gate at opening, gone by noon, back over the Marin hills from about 5:40
// PM, a dim veil at night; the Presidio's wooded hill; downtown with the Transamerica Pyramid (a pale spike with its two
// wings; its spire lit at night) and the Salesforce Tower (round-topped; its crown glows and breathes at night); a hill
// street of Victorian row houses in their pastels (the Painted Ladies: bay windows, gables, a turret) and a cable car
// creeping up the slope, its lamp on at night. Coit Tower, Sutro Tower and the Bay Bridge were tried by designer M2 and left
// out: at this size they read as a lighthouse, an antenna and a second bridge. Every shape is M2's (city.py).
// Everything is drawn in a 316 x 100 design frame (the glass), y down as a design is read; the Raster flips it.
// ---------------------------------------------------------------------------
public abstract class HudCity
{
    /// <summary>The café's city: what the window draws. San Francisco until the café franchises.</summary>
    public static string CityName = SanFrancisco;
    public const string SanFrancisco = "San Francisco", Seattle = "Seattle";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => CityName = SanFrancisco;

    public static HudCity Make(string name) => name == Seattle ? new HudCitySeattle() : (HudCity)new HudCitySanFrancisco();

    public abstract string Name { get; }
    /// <summary>Where the sun rises and sets across the glass (0..1 of its width): x at its rise, x at its set.</summary>
    public abstract Vector2 SunPath { get; }
    /// <summary>Where the moon may travel (0..1 of the glass's width), clear of the landmarks.</summary>
    public abstract Vector2 MoonBox { get; }
    /// <summary>Where cloud <paramref name="i"/> sits (0..1 of the glass), clear of the landmarks.</summary>
    public abstract Vector2 CloudAt(int i);

    protected RectTransform root;
    protected float W, H;
    protected bool fine;
    protected readonly List<Image> made = new List<Image>();

    public void Build(RectTransform parent, float width, float height, bool fineDetail)
    {
        root = parent;
        W = width; H = height;
        fine = fineDetail;
        Draw();
    }

    public void Destroy()
    {
        foreach (Image i in made) if (i != null) Object.Destroy(i.gameObject);
        made.Clear();
    }

    /// <summary>The layers made (once).</summary>
    protected abstract void Draw();
    /// <summary>The layers tinted for the hour (once a minute).</summary>
    public abstract void Set(in HudSky.Moment m);
    /// <summary>The tiny life, every frame (the owner's clock).</summary>
    public abstract void Tick(float now, in HudSky.Moment m);

    // ---------- helpers shared by the cities ----------

    protected static readonly Color WarmLight = HudArt.Hex(0xFFCF7A);
    protected static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
    protected static Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    protected static Color With(Color c, float a) => HudArt.With(c, a);
    protected static float Smooth(float t) => HudArt.Smooth(t);

    protected HudArt.Raster Canvas() => new HudArt.Raster(Mathf.RoundToInt(W), Mathf.RoundToInt(H));

    /// <summary>A full-glass layer from a raster (anchored over the whole glass).</summary>
    protected Image Layer(string name, HudArt.Raster r)
    {
        Image i = HudWindow.Picture(name, root, r.ToSprite(Name + " " + name), new Vector2(W * .5f, -H * .5f), new Vector2(W, H));
        made.Add(i);
        return i;
    }

    /// <summary>A small picture placed by hand (the cable car, a glow).</summary>
    protected Image Small(string name, Sprite sprite, Vector2 at, Vector2 size)
    {
        Image i = HudWindow.Picture(name, root, sprite, at, size);
        made.Add(i);
        return i;
    }

    protected static Vector2[] P(params float[] xy)
    {
        var pts = new Vector2[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return pts;
    }

    protected static void Tint(Image i, Color c) { if (i != null) i.color = c; }
    protected static void Tint(Image i, Color c, float a) { if (i != null) i.color = With(c, a); }
}

// =========================================================================== San Francisco

public sealed class HudCitySanFrancisco : HudCity
{
    public override string Name => SanFrancisco;
    public override Vector2 SunPath => new Vector2(.88f, .12f);
    public override Vector2 MoonBox => new Vector2(.30f, .62f);
    public override Vector2 CloudAt(int i) => i == 0 ? new Vector2(.16f, .18f) : new Vector2(.86f, .17f);

    static readonly Color GoldenGate = HudArt.Hex(0xC0362C), CreamStone = HudArt.Hex(0xEEE7DA), RedBeacon = HudArt.Hex(0xFF4B3A);
    const float WaterY = 74f, Top = 35.5f, Deck = 58.5f, T1 = 42f, T2 = 108f, Base = 84f, SX = 242f, PX = 205f;
    const float CarLength = 21f, CarHeight = 9.2f;
    static readonly float StreetAngle = Mathf.Atan2(21f, 166f) * Mathf.Rad2Deg;

    Image water, marin, fogBack, bridge, bridgeShine, fogFront, presidio, towers, salesforce, pyramid, pyramidEdge, houseShadow, housePastel,
          houseGlass, houseLit, kerb, car, carLit, carLamp, cityLights, spireLight, crownGlow, crownCap, deckLights, beacons, waterLights, fogNight, sunRoad;
    float fogBackHome, fogFrontHome, carSpeedPhase;

    static float Ground(float x) => 101.5f - (x - 150f) * (21f / 166f);

    protected override void Draw()
    {
        var rand = new System.Random(21);
        // ---- the water of the Gate and the Bay (left): a gradient, lighter at its top; dashes of light on it
        {
            var r = Canvas();
            for (int y = (int)WaterY; y < H; y++)
            {
                float t = (y - WaterY) / (H - WaterY);
                r.Rect(0f, y, 205f, y + 1f, Color.Lerp(Color.white, new Color(.52f, .5f, .62f), t));
            }
            if (fine)
                for (int i = 0; i < 26; i++)
                {
                    float dx = (float)rand.NextDouble() * 190f, dy = WaterY + 2.5f + (float)rand.NextDouble() * 22f, ln = 3f + (float)rand.NextDouble() * 6f;
                    r.Rect(dx, dy, dx + ln, dy + .8f, new Color(1f, 1f, 1f, .32f));
                }
            water = Layer("Water", r);
        }
        // ---- the sun's road on the water: a column of dashes under a low sun (placed by Set)
        {
            var r = new HudArt.Raster(20, 30);
            for (int j = 0; j < 8; j++)
            {
                float yy = 2f + j * 3.1f, half = 6f - j * .45f, off = j % 2 == 1 ? 1.3f : -1.3f;
                r.Rect(10f - half + off, yy, 10f + half + off, yy + .9f + j * .05f, Color.white);
            }
            sunRoad = Small("Sun road", r.ToSprite("SF sun road"), new Vector2(60f, -WaterY - 15f), new Vector2(20f, 30f));
        }
        // ---- the Marin hills behind the bridge
        {
            var r = Canvas();
            var hills = new List<Vector2> { new Vector2(0f, WaterY + .5f) };
            for (int i = 0; i <= 30; i++)
            {
                float x = 134f * i / 30f;
                float y = WaterY - (10.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, i / 30f * 1.12f)), .7f) + 2.5f) * (1f - .25f * i / 30f);
                hills.Add(new Vector2(x, y));
            }
            hills.Add(new Vector2(134f, WaterY + .5f));
            r.Polygon(hills, Color.white);
            marin = Layer("Marin hills", r);
        }
        // ---- fog behind the bridge (over the hills and the water) and in front of its deck: soft banks, drifting
        {
            var r = new HudArt.Raster(360, 100);
            // The sprite is 360 wide and sits 70 px left of the glass's origin, so a sprite x is a glass x + 70: the fog
            // stays over the Gate (glass x under about 215), never over downtown.
            for (int i = 0; i < 12; i++)
            {
                float cx = 60f + i * 19f, cy = WaterY - 3.5f + 2.2f * Mathf.Sin(i * 1.7f);
                r.Ellipse(cx, cy, 16f + 5f * Mathf.Sin(i * 2.3f), 4.6f + 1.4f * Mathf.Cos(i), new Color(1f, 1f, 1f, .78f), 2.4f);
            }
            for (int i = 0; i < 7; i++)
            {
                float cx = 62f + i * 24f, cy = WaterY - 12f + 2.5f * Mathf.Sin(i * 1.3f);
                r.Ellipse(cx, cy, 15f + 4f * Mathf.Sin(i * 1.9f), 4.5f + 1.2f * Mathf.Sin(i * .7f), new Color(1f, 1f, 1f, .6f), 2.4f);
            }
            fogBack = Small("Fog behind", r.ToSprite("SF fog behind"), new Vector2(110f, -50f), new Vector2(360f, 100f));
            fogBackHome = 110f;
            var f = new HudArt.Raster(360, 100);
            for (int i = 0; i < 9; i++)
            {
                float cx = 58f + i * 27f, cy = WaterY + 1f + 1.6f * Mathf.Sin(i * 2.1f + .5f);
                f.Ellipse(cx, cy, 19f + 6f * Mathf.Cos(i * 1.4f), 3.9f + 1f * Mathf.Sin(i), new Color(1f, 1f, 1f, .75f), 2f);
            }
            fogFront = Small("Fog in front", f.ToSprite("SF fog in front"), new Vector2(104f, -50f), new Vector2(360f, 100f));
            fogFrontHome = 104f;
            var n = Canvas();
            for (int i = 0; i < 8; i++) n.Ellipse(i * 31f + 5f, WaterY + 4f, 24f, 3.2f, new Color(1f, 1f, 1f, .5f), 2.8f);
            fogNight = Layer("Fog at night", n);
        }
        // ---- the Golden Gate Bridge: towers, the deck, the main cable and the side spans, the hangers (fine)
        {
            var r = Canvas();
            foreach (float tx in new[] { T1, T2 })
            {
                foreach (float leg in new[] { -3.6f, 1.3f })
                    r.Polygon(Color.white, tx + leg, WaterY + .8f, tx + leg + 2.3f, WaterY + .8f, tx + leg + 2f, Top, tx + leg + .3f, Top);
                foreach (float f in new[] { .30f, .55f, .78f, .97f })
                {
                    float yb = WaterY - (WaterY - Top) * f;
                    r.Rect(tx - 3.5f, yb, tx + 3.4f, yb + 1.25f, Color.white);
                }
                r.Rect(tx - 3.8f, Top - .2f, tx + 3.7f, Top + 1.2f, Color.white);
            }
            r.Rect(-2f, Deck, 152f, Deck + 1.9f, Color.white);
            List<Vector2> main = Cable(T1, Top + .3f, T2, Top + .3f, Deck - Top - 3.2f, 24), left = Cable(T1, Top + .3f, -6f, Deck - 4f, 6f, 10), right = Cable(T2, Top + .3f, 150f, Deck - 1f, 8f, 12);
            foreach (List<Vector2> line in new[] { main, left, right })
                for (int i = 0; i + 1 < line.Count; i++) r.Stroke(line[i].x, line[i].y + .5f, line[i + 1].x, line[i + 1].y + .5f, 1f, Color.white);
            if (fine)
            {
                for (int i = 1; i < main.Count - 1; i++) r.Stroke(main[i].x, main[i].y + .8f, main[i].x, Deck, .45f, new Color(1f, 1f, 1f, .5f));
                foreach (List<Vector2> line in new[] { left, right })
                    for (int i = 1; i < line.Count - 1; i++)
                        if (line[i].y + .8f < Deck) r.Stroke(line[i].x, line[i].y + .8f, line[i].x, Deck, .4f, new Color(1f, 1f, 1f, .5f));
            }
            bridge = Layer("Golden Gate", r);
            if (fine)
            {
                var s = Canvas();
                foreach (float tx in new[] { T1, T2 }) s.Rect(tx + 1.3f, Top + 1f, tx + 2.2f, WaterY, Color.white);
                bridgeShine = Layer("Golden Gate shine", s);
            }
        }
        // ---- the Presidio: the wooded hill the bridge lands on
        {
            var r = Canvas();
            var pres = new List<Vector2> { new Vector2(126f, 84f) };
            for (int i = 0; i <= 24; i++)
            {
                float x = 126f + 56f * i / 24f;
                float bump = 1.1f * Mathf.Sin(i * 2.6f) + .8f * Mathf.Sin(i * 1.3f + 1f);
                pres.Add(new Vector2(x, 63.5f + 6.5f * Mathf.Pow(Mathf.Abs(i - 9) / 15f, 1.4f) + bump));
            }
            pres.Add(new Vector2(182f, 84f));
            r.Polygon(pres, Color.white);
            presidio = Layer("Presidio", r);
        }
        // ---- downtown: hazed towers (a shade lighter and darker by turns), the Salesforce Tower, the Transamerica Pyramid
        float[,] specs = { { 181f, 7f, 63f }, { 188.5f, 6f, 58f }, { 195f, 8f, 52f }, { 214f, 7f, 49f }, { 221.5f, 9f, 41f }, { 231.5f, 6f, 54f }, { 250f, 8f, 47f }, { 258.5f, 6f, 57f }, { 265.5f, 7f, 62f } };
        {
            var r = Canvas();
            for (int i = 0; i < specs.GetLength(0); i++)
            {
                float x0 = specs[i, 0], tw = specs[i, 1], ty = specs[i, 2];
                Color c = i % 2 == 1 ? Color.white : new Color(.93f, .93f, .93f);
                r.Rect(x0, ty, x0 + tw, Base, c);
                if (fine && tw > 6.5f && i % 3 == 1) r.Rect(x0 + 1.5f, ty - 3f, x0 + tw - 1.5f, ty, c);
            }
            towers = Layer("Downtown", r);
            var s = Canvas();
            s.Polygon(Color.white, SX - 4.6f, Base, SX + 4.6f, Base, SX + 4.4f, 36f, SX + 3.6f, 31.5f, SX + 2.2f, 28.6f, SX, 27.6f, SX - 2.2f, 28.6f, SX - 3.6f, 31.5f, SX - 4.4f, 36f);
            if (fine) s.Rect(SX - 4.4f, 36f, SX + 4.4f, 37f, new Color(1f, 1f, 1f, .5f));
            salesforce = Layer("Salesforce Tower", s);
            var p = Canvas();
            p.Polygon(Color.white, PX - 6.2f, Base, PX + 6.2f, Base, PX + .75f, 25f, PX - .75f, 25f);
            p.Polygon(Color.white, PX - 4f, 48f, PX - 3f, 36.5f, PX - 1.4f, 36.5f, PX - 1.9f, 48f);
            p.Polygon(Color.white, PX + 4f, 48f, PX + 3f, 36.5f, PX + 1.4f, 36.5f, PX + 1.9f, 48f);
            p.Polygon(Color.white, PX - .45f, 25.4f, PX + .45f, 25.4f, PX, 16.5f);
            pyramid = Layer("Transamerica Pyramid", p);
            if (fine)
            {
                var e = Canvas();
                e.Polygon(new Color(1f, 1f, 1f, .6f), PX + 6f, Base, PX + 4.2f, Base, PX + .2f, 26.5f, PX + .7f, 25.5f);
                pyramidEdge = Layer("Pyramid's lit side", e);
            }
        }
        // ---- the hill street: Victorian row houses climbing to the right, their pastels, their bay windows
        var houses = new List<(float x0, float hw, float gnd, int i)>();
        {
            float x = 158f; int i = 0;
            int[] widths = { 23, 25, 22, 26, 23, 24, 25, 22 };
            while (x < 322f)
            {
                float hw = widths[i % widths.Length];
                houses.Add((x, hw, Ground(x + hw * .5f), i));
                x += hw; i++;
            }
        }
        var bays = new List<float[]>();
        var gables = new List<Vector2[]>();
        {
            var shadow = Canvas();
            var trims = new List<float[]>();
            foreach (var (x0, hw, gnd, i) in houses)
            {
                float body = 15.5f + (i % 3) * 1.5f, topY = gnd - body;
                shadow.Polygon(Color.white, x0, gnd + 12f, x0, topY, x0 + hw, topY, x0 + hw, gnd + 12f);
                if (i % 4 == 2)
                {
                    float tx = x0 + hw * .74f;
                    shadow.Polygon(Color.white, tx - 4f, topY + 3f, tx - 4f, topY - 4f, tx + 4f, topY - 4f, tx + 4f, topY + 3f);
                    shadow.Polygon(Color.white, tx - 4.8f, topY - 4f, tx + 4.8f, topY - 4f, tx, topY - 13.5f);
                    shadow.Polygon(Color.white, x0 - 1f, topY + .6f, x0 + hw + 1f, topY + .6f, x0 + hw + 1f, topY - 1.4f, x0 - 1f, topY - 1.4f);
                }
                else
                {
                    float peak = topY - 8.5f - (i % 2) * 1.5f;
                    Vector2[] g = P(x0 - 1f, topY + .4f, x0 + hw * .5f, peak, x0 + hw + 1f, topY + .4f);
                    gables.Add(g);
                    shadow.Polygon(g, Color.white);
                    trims.Add(new[] { x0 + hw * .5f - 1.6f, topY - 4.2f, x0 + hw * .5f + 1.6f, topY - 1.6f });
                }
                foreach (float fy in new[] { topY + 3.4f, topY + 9.4f })
                {
                    if (fy + 3.6f >= gnd) continue;
                    float b0 = x0 + hw * .14f, b1 = x0 + hw * .56f;
                    bays.Add(new[] { b0, fy, b0 + (b1 - b0) * .30f, fy + 3.6f });
                    bays.Add(new[] { b0 + (b1 - b0) * .36f, fy, b0 + (b1 - b0) * .64f, fy + 3.6f });
                    bays.Add(new[] { b0 + (b1 - b0) * .70f, fy, b1, fy + 3.6f });
                    bays.Add(new[] { x0 + hw * .66f, fy, x0 + hw * .84f, fy + 3.6f });
                }
            }
            houseShadow = Layer("Houses", shadow);
            // The pastels, one a house, with the gables a shade darker: shown by day, fading through the evening.
            var pastel = Canvas();
            Color[] pastels = { HudArt.Hex(0xA9C8A3), HudArt.Hex(0xE7A9A7), HudArt.Hex(0xEBCB82), HudArt.Hex(0x9EC1E2), HudArt.Hex(0xC3AED8), HudArt.Hex(0xF0B98E) };
            foreach (var (x0, hw, gnd, i) in houses)
            {
                float body = 15.5f + (i % 3) * 1.5f, topY = gnd - body;
                Color c = pastels[i % pastels.Length];
                pastel.Polygon(c, x0, gnd + 12f, x0, topY, x0 + hw, topY, x0 + hw, gnd + 12f);
                if (i % 4 == 2)
                {
                    float tx = x0 + hw * .74f;
                    pastel.Polygon(c, tx - 4f, topY + 3f, tx - 4f, topY - 4f, tx + 4f, topY - 4f, tx + 4f, topY + 3f);
                    pastel.Polygon(Mix(c, HudArt.Hex(0x5A4A4A), .45f), tx - 4.8f, topY - 4f, tx + 4.8f, topY - 4f, tx, topY - 13.5f);
                    pastel.Polygon(Mix(c, HudArt.Hex(0x5A4A4A), .3f), x0 - 1f, topY + .6f, x0 + hw + 1f, topY + .6f, x0 + hw + 1f, topY - 1.4f, x0 - 1f, topY - 1.4f);
                }
            }
            foreach (Vector2[] g in gables) pastel.Polygon(g, Mix(pastels[(gables.IndexOf(g) * 2) % pastels.Length], HudArt.Hex(0x5A4A4A), .5f));
            housePastel = Layer("Painted Ladies", pastel);
            var glass = Canvas();
            foreach (float[] b in bays) glass.Rect(b[0], b[1], b[2], b[3], Color.white);
            foreach (float[] t in trims) glass.Rect(t[0], t[1], t[2], t[3], Color.white);
            houseGlass = Layer("Bay windows", glass);
            var lit = Canvas();
            var rr = new System.Random(9);
            foreach (float[] b in bays) if (rr.NextDouble() > .32) lit.Rect(b[0], b[1], b[2], b[3], WarmLight);
            houseLit = Layer("Windows lit", lit);
        }
        // ---- the kerb, and the cable car on the slope (fine): drawn flat, turned to the street's angle when placed
        {
            var r = Canvas();
            r.Polygon(Color.white, 150f, Ground(150f) + .2f, 322f, Ground(322f) + .2f, 322f, Ground(322f) + 1.6f, 150f, Ground(150f) + 1.6f);
            kerb = Layer("Kerb", r);
            if (fine)
            {
                // The car at 4x: 26 x 14 design px, its wheels on the bottom edge.
                var c = new HudArt.Raster(104, 56);
                Color maroon = HudArt.Hex(0x9A2B28), cream = HudArt.Hex(0xEFE3C8), roof = Mix(maroon, HudArt.Hex(0x1F1B17), .5f), dark = HudArt.Hex(0x3A2E28), wheel = HudArt.Hex(0x2A2420);
                float L = CarLength * 4f, Hh = CarHeight * 4f, gy = 52f, c0 = 10f;
                c.Polygon(maroon, c0 + 8.8f, gy - 3.6f, c0 + L - 8.8f, gy - 3.6f, c0 + L - 8.8f, gy - Hh + 6.4f, c0 + 8.8f, gy - Hh + 6.4f);
                c.Polygon(maroon, c0 + 1.6f, gy - 3.6f, c0 + L - 1.6f, gy - 3.6f, c0 + L - 1.6f, gy - 10.4f, c0 + 1.6f, gy - 10.4f);
                foreach (float q in new[] { 0f, L - 7.6f }) c.Rect(c0 + 2.4f + q, gy - Hh + 8f, c0 + 5.2f + q, gy - 10.4f, maroon);
                c.Rect(c0 + 10.4f, gy - 26.4f, c0 + L - 10.4f, gy - 14.4f, cream);
                c.Polygon(roof, c0 - 3.2f, gy - Hh + 7.6f, c0 + L + 3.2f, gy - Hh + 7.6f, c0 + L - .8f, gy - Hh, c0 + .8f, gy - Hh);
                for (int q = 0; q < 5; q++) c.Rect(c0 + 13.6f + q * 12f, gy - 24.8f, c0 + 21.6f + q * 12f, gy - 16f, dark);
                c.Ellipse(c0 + 20f, gy - 2f, 5.2f, 5.2f, wheel);
                c.Ellipse(c0 + L - 20f, gy - 2f, 5.2f, 5.2f, wheel);
                car = Small("Cable car", c.ToSprite("SF cable car"), Vector2.zero, new Vector2(26f, 14f));
                car.rectTransform.pivot = new Vector2(.5f, 0f);
                car.rectTransform.localRotation = Quaternion.Euler(0f, 0f, StreetAngle);
                var w = new HudArt.Raster(104, 56);
                for (int q = 0; q < 5; q++) w.Rect(c0 + 13.6f + q * 12f, gy - 24.8f, c0 + 21.6f + q * 12f, gy - 16f, WarmLight);
                carLit = Small("Cable car windows", w.ToSprite("SF cable car lit"), Vector2.zero, new Vector2(26f, 14f));
                carLit.rectTransform.pivot = new Vector2(.5f, 0f);
                carLit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, StreetAngle);
                carLamp = Small("Cable car lamp", HudArt.Glow(), Vector2.zero, new Vector2(8f, 8f));
            }
        }
        // ---- night: the towers' windows, the Pyramid's spire, the Salesforce crown, the bridge's lights, the beacons, the water's lights
        {
            var r = Canvas();
            var rr = new System.Random(8);
            var all = new List<float[]>();
            for (int i = 0; i < specs.GetLength(0); i++) all.Add(new[] { specs[i, 0], specs[i, 2], specs[i, 0] + specs[i, 1], Base });
            all.Add(new[] { SX - 4f, 38f, SX + 4f, Base });
            foreach (float[] t in all)
            {
                int rows = (int)((t[3] - t[1]) / 3.2f), cols = Mathf.Max(1, (int)((t[2] - t[0]) / 2.6f));
                for (int j = 0; j < rows; j++)
                    for (int col = 0; col < cols; col++)
                        if (rr.NextDouble() > .62)
                        {
                            float wx = t[0] + .9f + col * 2.6f, wy = t[1] + 1.5f + j * 3.2f;
                            if (wx + 1.1f < t[2]) r.Rect(wx, wy, wx + 1.1f, wy + 1.3f, WarmLight);
                        }
            }
            for (int j = 0; j < 14; j++)
            {
                float yy = 30f + j * 3.6f, half = .75f + (6.2f - .75f) * (yy - 25f) / (Base - 25f);
                for (int q = 0; q < (int)(half * 2f / 2.4f); q++)
                    if (rr.NextDouble() > .55) { float wx = PX - half + .9f + q * 2.4f; r.Rect(wx, yy, wx + 1f, yy + 1.2f, WarmLight); }
            }
            cityLights = Layer("City lights", r);
            spireLight = Small("Spire light", HudArt.Glow(), new Vector2(PX, -17.6f), new Vector2(7f, 7f));
            crownGlow = Small("Crown glow", HudArt.Glow(), new Vector2(SX, -31f), new Vector2(13f, 12f));
            var cap = new HudArt.Raster(16, 8);
            cap.Polygon(Color.white, 1.6f, 7.6f, 14.4f, 7.6f, 12.4f, 2f, 8f, .4f, 3.6f, 2f);
            crownCap = Small("Crown", cap.ToSprite("SF crown"), new Vector2(SX, -30f), new Vector2(8f, 4f));
            var d = Canvas();
            for (float x = -1f; x < 150f; x += 4.4f) d.Rect(x - .5f, Deck - .2f, x + .5f, Deck + 1.2f, HudArt.Hex(0xFFD98E));
            deckLights = Layer("Deck lights", d);
            var b = Canvas();
            foreach (float tx in new[] { T1, T2 }) b.Rect(tx - 1f, Top - 2.6f, tx + 1f, Top - .8f, RedBeacon);
            beacons = Layer("Beacons", b);
            var wl = Canvas();
            int k = 0;
            for (float x = 2f; x < 172f; x += 8.8f, k++) wl.Rect(x - .3f, WaterY + 2f + (k % 3), x + .3f, WaterY + 7f + (k % 4), HudArt.Hex(0xFFC98A));
            waterLights = Layer("Lights on the water", wl);
        }
        // Draw order: the night layers were made last, so they lie on top; the fog in front of the deck goes under the
        // Presidio and downtown (the towers' feet in the fog, the famous picture); the night's veil over everything.
        fogFront.transform.SetSiblingIndex(presidio.transform.GetSiblingIndex());
        if (fogNight != null) fogNight.transform.SetAsLastSibling();
    }

    static List<Vector2> Cable(float x0, float y0, float x1, float y1, float sag, int n)
    {
        var pts = new List<Vector2>(n + 1);
        for (int s = 0; s <= n; s++)
        {
            float u = s / (float)n;
            pts.Add(new Vector2(x0 + (x1 - x0) * u, y0 + (y1 - y0) * u + sag * 4f * u * (1f - u)));
        }
        return pts;
    }

    static float FogAmount(float hour)
    {
        float morning = 1f - Smooth((hour - 9.4f) / 3f);
        float evening = Smooth((hour - 17.6f) / 2f);
        return Mathf.Clamp01(Mathf.Max(morning * .95f, evening * .85f));
    }

    public override void Set(in HudSky.Moment m)
    {
        float elev = m.sunUp ? m.sunElev : 0f, night = m.night, lit = m.lit, dusk = m.dusk;
        float dw = Smooth(elev / .6f);
        Color warmTop = Scale(Mix(m.hor, m.top, .15f), .88f), blueTop = Mix(Mix(m.hor, HudArt.Hex(0x4C8DB4), .62f), m.top, .1f);
        Color waterTop = Mix(Mix(warmTop, blueTop, dw), HudArt.Hex(0x121B3A), night * .8f);
        Tint(water, waterTop);
        Tint(marin, Mix(Mix(m.far, HudArt.Hex(0x4E6650), .30f * (1f - night)), m.near, .15f));
        float fa = FogAmount(m.hour), morning = 1f - Smooth((m.hour - 9.4f) / 3f);
        Color fog = HudSky.FogColour(in m);
        Tint(fogBack, fog, fa);
        Tint(fogFront, fog, fa * (.55f + .3f * morning) / .75f);
        Tint(fogNight, fog, night * .28f / .5f);
        Color ggDay = HudSky.Haze(GoldenGate, in m, .16f + .10f * (1f - elev));
        Color gg = Mix(Mix(ggDay, Mix(Scale(GoldenGate, .62f), m.near, .35f), dusk), Mix(Scale(GoldenGate, .78f), HudArt.Hex(0x3A1A1A), .25f), night);
        Tint(bridge, gg);
        if (bridgeShine != null)
        {
            // The sunny side: the sun from the right in the morning, the left in the evening (the shine moves with it).
            bool morningSide = m.sunUp && m.sunP < .5f;
            bridgeShine.rectTransform.anchoredPosition = new Vector2(W * .5f + (morningSide ? 0f : -4.9f), -H * .5f);
            Tint(bridgeShine, Mix(gg, HudArt.Hex(0xFFD9B0), .45f), .55f * (1f - night) * (.4f + .6f * elev));
        }
        Tint(presidio, Mix(Mix(m.far, HudArt.Hex(0x5E7E5A), .42f * (1f - night)), m.near, .10f + .15f * night));
        Tint(towers, m.far);
        Color glass = Mix(Mix(HudSky.Haze(HudArt.Hex(0xCCD6DD), in m, .42f), m.far, .35f + .45f * dusk), Scale(m.near, 1.15f), night * .9f);
        Tint(salesforce, glass);
        Color pyrDay = HudSky.Haze(CreamStone, in m, .26f);
        Color pyr = Mix(Mix(pyrDay, Scale(m.far, 1.05f), dusk * .8f), Mix(Scale(m.near, 1.25f), HudArt.Hex(0x2A2E44), .3f), night);
        Tint(pyramid, pyr);
        if (pyramidEdge != null)
        {
            bool morningSide = m.sunUp && m.sunP < .5f;
            pyramidEdge.rectTransform.localScale = new Vector3(morningSide ? 1f : -1f, 1f, 1f);
            pyramidEdge.rectTransform.anchoredPosition = new Vector2(morningSide ? W * .5f : W * .5f + (W - 2f * PX), -H * .5f);
            Tint(pyramidEdge, Mix(pyr, Color.white, .35f), 1f - night);
        }
        Color houseShade = Mix(Mix(m.hor, m.near, .78f), m.near, .5f * night);
        Tint(houseShadow, houseShade);
        float dayAmount = (.62f - .32f * Smooth((m.hour - 17.5f) / 2f)) * (1f - lit);
        Tint(housePastel, Mix(Color.white, m.hor, .18f), dayAmount);
        Tint(houseGlass, Mix(Mix(m.near, m.mid, .45f), Color.white, .12f), (1f - lit) * .9f);
        Tint(houseLit, Color.white, lit);
        Tint(kerb, Mix(Mix(HudArt.Hex(0xB9B2A6), m.near, .35f), m.near, .7f * night), .9f);
        Color dim = Mix(Color.white, new Color(.42f, .42f, .5f), night * .55f);
        Tint(car, dim);
        Tint(carLit, Color.white, lit);
        Tint(carLamp, HudArt.Hex(0xFFE7A8), lit);
        Tint(cityLights, Color.white, lit * .9f);
        Tint(spireLight, HudArt.Hex(0xFFF4DA), lit);
        Tint(deckLights, Color.white, lit * .95f);
        Tint(waterLights, Color.white, lit * .35f);
        // The sun's road on the water, under a low sun over the Gate.
        if (m.sunUp && m.sunP <= 1.02f && elev < .55f)
        {
            Vector2 path = SunPath;
            float sx = Mathf.Lerp(path.x, path.y, Mathf.Clamp(m.sunP, -.06f, 1.02f)) * W;
            bool over = sx < 210f;
            float strength = (.65f + .3f * (1f - elev)) * (1f - Smooth((m.sunP - .97f) / .05f)) * Smooth((.55f - elev) / .3f);
            sunRoad.rectTransform.anchoredPosition = new Vector2(sx, -WaterY - 15f);
            Tint(sunRoad, Mix(m.sunColour, Color.white, .3f), over ? strength : 0f);
        }
        else Tint(sunRoad, Color.white, 0f);
        crownPulseBase = lit;
    }

    float crownPulseBase;

    public override void Tick(float now, in HudSky.Moment m)
    {
        // The fog drifts about 2 px a second; the beacons blink every 1.4 s; the crown breathes over 3.2 s; the car creeps 3 px a second.
        float drift = 12f * Mathf.Sin(now * 2f * Mathf.PI / 40f);
        if (fogBack != null) fogBack.rectTransform.anchoredPosition = new Vector2(fogBackHome + drift, -50f);
        if (fogFront != null) fogFront.rectTransform.anchoredPosition = new Vector2(fogFrontHome + drift * .7f, -50f);
        if (fogNight != null) fogNight.rectTransform.anchoredPosition = new Vector2(W * .5f + drift * .4f, -H * .5f);
        float lit = m.lit;
        if (beacons != null)
        {
            float blink = .55f + .45f * (Mathf.Sin(now * 2f * Mathf.PI / 1.4f) > 0f ? 1f : 0f);
            Tint(beacons, Color.white, lit * blink);
        }
        float pulse = .8f + .2f * Mathf.Sin(now * 2f * Mathf.PI / 3.2f);
        Tint(crownGlow, HudArt.Hex(0xE6F0FF), lit * .75f * pulse);
        Tint(crownCap, HudArt.Hex(0xF6FAFF), lit * .8f * pulse);
        if (car != null)
        {
            float carX = 288f - Mathf.Repeat(now * 3f, 130f);
            float cx = carX + CarLength * .5f, gy = Ground(cx) - .2f;
            car.rectTransform.anchoredPosition = new Vector2(cx, -gy);
            carLit.rectTransform.anchoredPosition = new Vector2(cx, -gy);
            float ang = StreetAngle * Mathf.Deg2Rad;
            Vector2 lamp = new Vector2(cx + Mathf.Cos(ang) * (CarLength * .5f + .6f) - Mathf.Sin(ang) * 3.4f, gy - 3.4f * Mathf.Cos(ang) - Mathf.Sin(ang) * (CarLength * .5f + .6f));
            carLamp.rectTransform.anchoredPosition = new Vector2(lamp.x, -lamp.y);
        }
    }
}

// =========================================================================== Seattle (the placeholder second city; his answer h4)

public sealed class HudCitySeattle : HudCity
{
    public override string Name => Seattle;
    public override Vector2 SunPath => new Vector2(.14f, .86f);
    public override Vector2 MoonBox => new Vector2(.45f, .72f);
    public override Vector2 CloudAt(int i) => i == 0 ? new Vector2(.38f, .2f) : new Vector2(.9f, .16f);

    const float WaterY = 78f, Base = 82f, CC = 154f, NX = 74f, WX = 104f, WY = 70f, WR = 7.5f;

    Image mountain, snow, water, towers, columbia, needle, wheel, ferry, houses, houseGreen, houseGlass, houseLit, lights, needleLight, drizzle;

    static float Ground(float x) => 92f + x * (8f / 120f);

    protected override void Draw()
    {
        // Mount Rainier: huge, pale, far; snow on top.
        { var r = Canvas(); r.Polygon(Color.white, 120, 80, 175, 55, 205, 40, 222, 30.5f, 231, 28, 240, 30, 258, 40, 285, 55, 330, 72, 330, 80); mountain = Layer("Rainier", r); }
        { var r = Canvas(); r.Polygon(Color.white, 196, 44.5f, 210, 36.5f, 222, 30.5f, 231, 28, 240, 30, 254, 38, 266, 45.5f, 252, 44, 240, 47, 230, 43.5f, 219, 47.5f, 208, 44); snow = Layer("Snow", r); }
        // Elliott Bay.
        {
            var r = Canvas();
            for (int y = (int)WaterY; y < H; y++) r.Rect(0f, y, W, y + 1f, Color.Lerp(Color.white, new Color(.6f, .6f, .7f), (y - WaterY) / (H - WaterY)));
            water = Layer("Elliott Bay", r);
        }
        // Downtown and the Columbia Center.
        float[,] specs = { { 118, 6, 64 }, { 125, 7, 58 }, { 133, 8, 52 }, { 142, 7, 56 }, { 166, 8, 50 }, { 176, 6, 60 }, { 184, 8, 48 }, { 194, 7, 57 }, { 203, 6, 63 } };
        {
            var r = Canvas();
            for (int i = 0; i < specs.GetLength(0); i++) r.Rect(specs[i, 0], specs[i, 2], specs[i, 0] + specs[i, 1], Base, i % 2 == 1 ? Color.white : new Color(.93f, .93f, .93f));
            towers = Layer("Downtown", r);
            var c = Canvas();
            c.Polygon(Color.white, CC - 5.5f, Base, CC + 5.5f, Base, CC + 5.5f, 38, CC + 3.8f, 38, CC + 3.8f, 33.5f, CC + 1.6f, 33.5f, CC + 1.6f, 30, CC - 1.6f, 30, CC - 1.6f, 33.5f, CC - 3.8f, 33.5f, CC - 3.8f, 38, CC - 5.5f, 38);
            columbia = Layer("Columbia Center", c);
        }
        // The Space Needle.
        {
            var r = Canvas();
            r.Polygon(Color.white, NX - 5.6f, Base, NX - 3f, Base, NX - 1.2f, 50, NX - .9f, 37.5f, NX + .9f, 37.5f, NX + 1.2f, 50, NX + 3f, Base, NX + 5.6f, Base, NX + 1.7f, 57, NX + 1.1f, 47, NX - 1.1f, 47, NX - 1.7f, 57);
            r.Polygon(Color.white, NX - 10, 35.2f, NX + 10, 35.2f, NX + 7, 38.4f, NX - 7, 38.4f);
            r.Polygon(Color.white, NX - 7.2f, 35.2f, NX + 7.2f, 35.2f, NX + 5.2f, 32.6f, NX - 5.2f, 32.6f);
            r.Polygon(Color.white, NX - 3, 32.8f, NX + 3, 32.8f, NX + 2, 31, NX - 2, 31);
            r.Polygon(Color.white, NX - .4f, 31.2f, NX + .4f, 31.2f, NX, 22);
            needle = Layer("Space Needle", r);
            needleLight = Small("Needle light", HudArt.Glow(), new Vector2(NX, -22.5f), new Vector2(6f, 6f));
        }
        // The Great Wheel on the waterfront.
        {
            var r = Canvas();
            for (int q = 0; q < 48; q++)
            {
                float a0 = q * Mathf.PI * 2f / 48f, a1 = (q + 1) * Mathf.PI * 2f / 48f;
                r.Stroke(WX + WR * Mathf.Cos(a0), WY + WR * Mathf.Sin(a0), WX + WR * Mathf.Cos(a1), WY + WR * Mathf.Sin(a1), 1f, new Color(1f, 1f, 1f, .9f));
            }
            for (int q = 0; q < 8; q++) { float a = q * Mathf.PI / 4f; r.Stroke(WX, WY, WX + WR * Mathf.Cos(a), WY + WR * Mathf.Sin(a), .5f, new Color(1f, 1f, 1f, .75f)); }
            r.Polygon(new Color(1f, 1f, 1f, .75f), WX - 1.4f, WY, WX + 1.4f, WY, WX + 3.2f, WaterY, WX - 3.2f, WaterY);
            wheel = Layer("Great Wheel", r);
        }
        // A ferry crossing the bay (tiny life).
        {
            var r = new HudArt.Raster(20, 6);
            r.Polygon(HudArt.Hex(0xF5F5F0), 0, 2.5f, 19, 2.5f, 17, 5.2f, 2, 5.2f);
            r.Polygon(HudArt.Hex(0xF5F5F0), 4, 2.6f, 15, 2.6f, 14, .4f, 5, .4f);
            r.Polygon(UiSkin.Brand, 1, 3.6f, 18, 3.6f, 17.6f, 4.3f, 1.4f, 4.3f);
            ferry = Small("Ferry", r.ToSprite("Seattle ferry"), new Vector2(20f, -85f), new Vector2(20f, 6f));
        }
        // Queen Anne in front: craftsman houses on a hill on the left.
        {
            var shadow = Canvas(); var green = Canvas(); var glass = Canvas(); var lit = Canvas();
            float x = -6f; int i = 0;
            var rr = new System.Random(3);
            while (x < 128f)
            {
                float hw = (new[] { 28f, 31f, 27f, 30f })[i % 4];
                float g = Ground(x + hw * .5f), topY = g - 9.5f;
                shadow.Polygon(Color.white, x, g + 12f, x, topY, x + hw, topY, x + hw, g + 12f);
                shadow.Polygon(Color.white, x - 2f, topY + .5f, x + hw * .5f, topY - 7.5f, x + hw + 2f, topY + .5f);
                green.Polygon(new Color(1f, 1f, 1f, .45f), x, g + 12f, x, topY, x + hw, topY, x + hw, g + 12f);
                foreach (float[] wdw in new[] { new[] { x + hw * .2f, topY + 2.6f, x + hw * .42f, topY + 5.8f }, new[] { x + hw * .58f, topY + 2.6f, x + hw * .8f, topY + 5.8f } })
                {
                    glass.Rect(wdw[0], wdw[1], wdw[2], wdw[3], Color.white);
                    if (rr.NextDouble() > .3) lit.Rect(wdw[0], wdw[1], wdw[2], wdw[3], WarmLight);
                }
                x += hw; i++;
            }
            houses = Layer("Queen Anne", shadow);
            houseGreen = Layer("Queen Anne gardens", green);
            houseGlass = Layer("Queen Anne windows", glass);
            houseLit = Layer("Queen Anne lit", lit);
        }
        // Night: downtown's windows.
        {
            var r = Canvas();
            var rr = new System.Random(5);
            for (int i = 0; i < specs.GetLength(0); i++)
            {
                float x0 = specs[i, 0], ty = specs[i, 2], x1 = x0 + specs[i, 1];
                for (float wy = ty + 1.5f; wy < Base - 2f; wy += 3.2f)
                    for (float wx = x0 + .9f; wx + 1.1f < x1; wx += 2.6f)
                        if (rr.NextDouble() > .62) r.Rect(wx, wy, wx + 1.1f, wy + 1.3f, WarmLight);
            }
            for (float wy = 40f; wy < Base - 2f; wy += 3.2f)
                for (float wx = CC - 4.6f; wx + 1.1f < CC + 4.6f; wx += 2.6f)
                    if (rr.NextDouble() > .5) r.Rect(wx, wy, wx + 1.1f, wy + 1.3f, WarmLight);
            lights = Layer("City lights", r);
        }
        // The weather: drizzle on the glass (fine only): a column of small drops that slides down and wraps.
        if (fine)
        {
            var r = new HudArt.Raster((int)W, (int)(H * 2f));
            var rr = new System.Random(11);
            for (int i = 0; i < 34; i++)
            {
                float dx = (float)rr.NextDouble() * W, dy = (float)rr.NextDouble() * H;
                foreach (float oy in new[] { 0f, H })
                {
                    r.Ellipse(dx, dy + oy, 1.1f, 1.6f, new Color(1f, 1f, 1f, .5f));
                    r.Stroke(dx, dy + oy - 4f, dx, dy + oy - 1f, .6f, new Color(1f, 1f, 1f, .25f));
                }
            }
            drizzle = Small("Drizzle", r.ToSprite("Seattle drizzle"), new Vector2(W * .5f, -H * .5f), new Vector2(W, H * 2f));
        }
    }

    public override void Set(in HudSky.Moment m)
    {
        float elev = m.sunUp ? m.sunElev : 0f, night = m.night, lit = m.lit, dusk = m.dusk;
        Tint(mountain, Mix(Mix(m.hor, m.mid, .35f), m.far, .25f), .75f - .35f * night);
        Tint(snow, Mix(Color.white, m.hor, .25f + .55f * dusk), .85f - .55f * night);
        float dw = Smooth(elev / .6f);
        Color waterTop = Mix(Scale(Mix(m.hor, m.top, .15f), .85f), Mix(Mix(m.hor, HudArt.Hex(0x567E98), .6f), m.top, .1f), dw);
        Tint(water, Mix(waterTop, HudArt.Hex(0x121B32), night * .8f));
        Tint(towers, m.far);
        Tint(columbia, Mix(m.far, HudArt.Hex(0x101418), .45f));
        Color needleColour = Mix(Mix(HudSky.Haze(HudArt.Hex(0x9FA4A8), in m, .18f), m.far, dusk * .7f), Mix(Scale(m.near, 1.2f), HudArt.Hex(0x2A2E44), .3f), night);
        Tint(needle, needleColour);
        Tint(needleLight, HudArt.Hex(0xFFF4DA), lit);
        Tint(wheel, Mix(HudArt.Hex(0xF2F2F2), m.far, .35f + .5f * night));
        Tint(ferry, Mix(Color.white, new Color(.4f, .4f, .5f), night * .5f));
        Tint(houses, Mix(Mix(m.hor, m.near, .8f), m.near, .5f * night));
        Tint(houseGreen, Mix(m.near, HudArt.Hex(0x6E8C6A), .45f), 1f - lit);
        Tint(houseGlass, Mix(m.near, m.mid, .4f), .6f * (1f - lit));
        Tint(houseLit, Color.white, lit);
        Tint(lights, Color.white, lit * .9f);
        Tint(drizzle, Mix(Color.white, m.mid, .3f), .55f);
    }

    public override void Tick(float now, in HudSky.Moment m)
    {
        if (ferry != null) ferry.rectTransform.anchoredPosition = new Vector2(12f + Mathf.Repeat(now * 4f, 120f) + 10f, -85f);
        if (drizzle != null) drizzle.rectTransform.anchoredPosition = new Vector2(W * .5f, -H * .5f - H + Mathf.Repeat(now * 9f, H));
    }
}
