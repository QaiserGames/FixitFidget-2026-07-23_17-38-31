#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// PLAYTEST 4 FIXES: THE NIGHT (Lead 5, 10 Oct 2026; the playtesters' reports, Docs/Studio/playtests/*-2026-10-10.md)
//
//   Fixit Fidget > Playtest 4 > Night > 0 - Check first                              (read-only)
//   Fixit Fidget > Playtest 4 > Night > 1 - Her window and her porch light (scene)
//   Fixit Fidget > Playtest 4 > Night > 2 - Her front window: what she sees from inside (read-only)   (★1, 12 Oct)
//
// Item 2 (12 Oct 2026, playtest 4's ★1, "Grace sees out of her front window"): nothing in the scene changes for it; her sight
// out of the glass is code (GraceAtHome: the same eyes, through the pane the night makes solid, past the frame and the drapes).
// This read-only check measures the window as the scene has it (the pane, the trim's jambs, mullion and transom, the two
// curtain panels, the pier, her armchair and the TV) against the numbers her eyes and the checks work with
// (GraceAtHome.WindowSouth and the rest), and prints what the glass shows her from her armchair and from the window spot:
// the bands of pavement a standing head is seen in. Run it before the labs; if the window ever moves, it says so.
//
// Step 1 answers the veteran at 12:19 AM on Night 2 ("From the night overhead I can't pick out her window, or tell whether
// it's lit or dark") and the first hour on Night 1 ("at night the saffron house looks brown", no marker for her step). From
// the overhead her street wall is seen edge on, so her windows' light now falls where the camera does look: on her
// pavement. Three spot lights, night only (off in the scene; GraceAtHome switches them), under her house in one group:
//   * her porch light: under the bay over her front door, on all night every night (her house, her step, Barnaby on it);
//   * her front window's light on the pavement: while her standard lamp is on, the TV's colour jumping in it;
//   * her bedroom's light on the pavement, out of the north bay: while her bedside lamp is on.
// Each aims down and out toward the street, never toward her walls: they cast no shadows, so a light that reached behind her
// walls would light her rooms through them (the checks sample 116,000 points inside her walls and her bays: none may be
// lit). None of them is one of her lamps, so what she sees by (7 m lit, 3 m dark) is unchanged. A light already there is
// left as it is (tuned in the Inspector, it stays tuned); the step only wires what's missing. Nothing is saved or deleted.
//
// Plan metres as GraceHouse's (X north along West Street from her south wall, Y from her back wall toward the street, z up
// from her ground floor; her street wall's inside face at Y 4.02, its outside about 4.11; Tools/Blender/plan_v2.py): her door
// X 0.62-1.60, the bay over it X 0.61-2.11 (Y 4.11-4.91, from 2.35 m up), her front window X 2.635-4.885 (sill 0.605,
// head 2.055), the north bay X 3.31-4.81.
// ---------------------------------------------------------------------------
internal static class Playtest4NightSteps
{
    const string Menu = Playtest4Shared.Menu + "Night/";
    const string GroupName = "Her lights outside (night only: GraceAtHome switches them)";
    const string PorchName = "Her porch light (night)";
    const string FrontName = "Her front window's light on the pavement (night)";
    const string BedroomName = "Her bedroom's light on the pavement (night)";

    sealed class Spec
    {
        public string name, field, why;
        public Vector3 at;          // plan metres
        public float tilt;          // degrees out from straight down, toward the street
        public float spot, range, intensity;
        public Color colour;
    }

    // Worked out so that no point inside her walls or her bays is within any cone and range (the closest misses are
    // 4.5-6.1°), and the pool's middle lands on her pavement: the porch 1.9 m out from her wall, her front window 1.7 m,
    // her bedroom 2.9 m (at the kerb, a soft wash from 3.35 m up). Brightness about 0.3-0.45 where each lands, about the old
    // street lamps' 0.23 under them: a warm pool, not a street lamp's (40 over 6 m).
    static readonly Spec[] Specs =
    {
        new Spec { name = PorchName, field = "porchLight", why = "her porch light: her house and her step from the street, all night",
                   at = new Vector3(1.11f, 4.80f, 2.20f), tilt = 28f, spot = 90f, range = 4f, intensity = 2.2f, colour = new Color(1f, .82f, .58f) },
        new Spec { name = FrontName, field = "frontSpill", why = "her front room's light on the pavement while her standard lamp is on",
                   at = new Vector3(3.76f, 4.25f, 1.45f), tilt = 44f, spot = 100f, range = 4.5f, intensity = 2.2f, colour = new Color(1f, .78f, .52f) },
        new Spec { name = BedroomName, field = "bedroomSpill", why = "her bedroom's light on the pavement while her bedside lamp is on",
                   at = new Vector3(4.06f, 5.00f, 3.35f), tilt = 30f, spot = 70f, range = 6f, intensity = 3f, colour = new Color(1f, .76f, .5f) },
    };

    // Her pavement is 0.15 m under her ground floor.
    const float Pavement = -.15f;

    // ================================================================== 0 - Check first

    [MenuItem(Menu + "0 - Check first")]
    static void CheckFirst() => Playtest4Shared.Survey("Playtest 4 · Night · 0 - Check first", "night-0-check-first", run =>
    {
        GraceAtHome home = Playtest4Shared.One<GraceAtHome>(run);
        GraceHouse house = home.house;
        run.Check(house != null && house.house != null && house.door != null, "Grace at home has her house, its building and its front door");
        run.Info("Grace at home: " + Playtest4Shared.PathOf(home.gameObject));
        foreach (Spec s in Specs)
        {
            Light l = Wired(home, s.field);
            Transform found = house != null && house.house != null ? FindChild(house.house, s.name) : null;
            run.Info($"{s.field}: {(l != null ? Playtest4Shared.PathOf(l.gameObject) + Describe(l) : "not wired")}" +
                     (l == null && found != null ? " (a light of that name is under her house: step 1 wires it)" : ""));
            if (l != null && house != null) run.Check(LitInside(house, l) == 0, $"{s.field} lights nothing inside her walls");
        }
        run.Check(home.standardLamp != null && home.bedsideLamp != null, "her standard lamp and bedside lamp are wired (they switch the pavement's light)");
        run.Section("Her night (GraceNight, playtest 4)");
        run.Info("usual: " + GraceNight.Describe(GraceNight.Usual));
        run.Info("Thursday: " + GraceNight.Describe(GraceNight.Thursday));
        const float SecondsAnHour = 48f;
        run.Info($"on Night 2 from her stoop (about 11:15 PM): her front window dark after about {(GraceNight.UsualBed + 6f / 60f - 23.25f) * SecondsAnHour:0} s, " +
                 $"her lamp off after about {(GraceNight.UsualAsleep - 23.25f) * SecondsAnHour:0} s; then {(GraceNight.UsualWater - GraceNight.UsualAsleep) * SecondsAnHour:0} s " +
                 "before her bay lights up again at 2:40");
        run.Section("The cups' morning (NightThings, playtest 4)");
        NightThing cups = NightThings.CupsOfGrace, gnome = NightThings.GnomeOfGrace;
        float cupsBand = LodgerStory.Green(cups.green, true) + 2f * LodgerStory.Near(cups.near, true);
        float gnomeBand = LodgerStory.Green(gnome.green, true) + 2f * LodgerStory.Near(gnome.near, true);
        run.Info($"with Nerve: the cups' band {cupsBand:0.000} of the bar, {cupsBand * cups.sweepSeconds * 1000f:0} ms a pass; " +
                 $"the gnome's {gnomeBand:0.000}, {gnomeBand * gnome.sweepSeconds * 1000f:0} ms");
    });

    [MenuItem(Menu + "0 - Check first", true)]
    static bool CanCheckFirst() => Playtest4Shared.NotPlaying();

    // ================================================================== 1 - Her window and her porch light

    [MenuItem(Menu + "1 - Her window and her porch light (scene)")]
    static void HerLights()
    {
        GraceAtHome home = null;
        GraceHouse house = null;
        var lights = new Light[Specs.Length];
        Playtest4Shared.Step("Playtest 4 · Night · 1 - Her window and her porch light", "night-1-her-lights",
            survey: run =>
            {
                home = Playtest4Shared.One<GraceAtHome>(run);
                house = home.house;
                run.Require(house != null, "Grace at home has no house (Break-ins 6 - Put Grace at home).");
                run.Require(house.house != null && house.door != null, "Her house has no building or no front door (Break-ins 1).");
                run.Require(home.standardLamp != null && home.bedsideLamp != null, "Her standard lamp or bedside lamp isn't wired (Break-ins 6).");
                run.Info($"her house: {Playtest4Shared.PathOf(house.house.gameObject)}; her door {Playtest4Shared.PathOf(house.door.gameObject)}");
                for (int i = 0; i < Specs.Length; i++)
                {
                    Light wired = Wired(home, Specs[i].field);
                    Transform named = FindChild(house.house, Specs[i].name);
                    run.Info($"{Specs[i].field}: {(wired != null ? "wired to " + Playtest4Shared.PathOf(wired.gameObject) : named != null ? "not wired; a light of that name is there" : "not there yet")}");
                }
            },
            change: run =>
            {
                Transform group = FindChild(house.house, GroupName);
                for (int i = 0; i < Specs.Length; i++)
                {
                    Spec s = Specs[i];
                    Light l = Wired(home, s.field);
                    Transform named = l == null ? FindChild(house.house, s.name) : null;
                    if (l == null && named != null) l = named.GetComponent<Light>();
                    if (l != null)
                    {
                        run.Info($"{s.name}: already there ({Describe(l)}): left as it is");
                    }
                    else
                    {
                        if (group == null) group = run.Make(GroupName, house.house, "her lights outside her walls, night only").transform;
                        GameObject go = run.Make(s.name, group, s.why);
                        Vector3 at = house.World(s.at.x, s.at.y, s.at.z);
                        Vector3 aim = Aim(house, s);
                        Vector3 along = (house.World(s.at.x + 1f, s.at.y, s.at.z) - at).normalized;
                        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(aim, along));
                        l = run.Add<Light>(go, "a spot light, off in the scene");
                        l.type = LightType.Spot;
                        l.spotAngle = s.spot;
                        l.innerSpotAngle = s.spot * .6f;
                        l.range = s.range;
                        l.intensity = s.intensity;
                        l.color = s.colour;
                        l.shadows = LightShadows.None;
                        l.renderMode = LightRenderMode.ForcePixel;
                        l.enabled = false;
                        run.Info($"{s.name}: {Describe(l)}");
                    }
                    lights[i] = l;
                    run.SetObject(home, s.field, l, $"Grace at home · {s.field}");
                }
            },
            checks: run =>
            {
                Light[] hers = { home.standardLamp, home.bedsideLamp, home.landingLight, home.kitchenLight, home.tvLight };
                for (int i = 0; i < Specs.Length; i++)
                {
                    Spec s = Specs[i];
                    Light l = lights[i];
                    run.Check(l != null && Wired(home, s.field) == l, $"{s.field} is wired to {(l != null ? Playtest4Shared.PathOf(l.gameObject) : "nothing")}");
                    if (l == null) continue;
                    run.Check(l.type == LightType.Spot && !l.enabled && l.shadows == LightShadows.None,
                        $"{s.name}: a spot, off in the scene (GraceAtHome switches it at night), no shadows");
                    run.Check(Array.IndexOf(hers, l) < 0 && (house.nightLights == null || Array.IndexOf(house.nightLights, l) < 0),
                        $"{s.name}: not one of her lamps (she sees by those only: 7 m lit, 3 m dark)");
                    int inside = LitInside(house, l);
                    run.Check(inside == 0, $"{s.name}: lights no point inside her walls or her bays ({inside} of the samples lit)");
                    Vector3 p = house.Plan(l.transform.position), d = PlanAim(house, l);
                    float outFromWall = d.z < -.1f ? p.y + (p.z - Pavement) / -d.z * d.y - 4.11f : -1f;
                    run.Check(outFromWall > .3f && outFromWall < 3.5f, $"{s.name}: its middle lands on her pavement, {outFromWall:0.00} m out from her wall");
                }
                Light porch = lights[0];
                if (porch != null)
                {
                    Vector3 p = house.Plan(porch.transform.position);
                    run.Check(p.x > .62f && p.x < 1.60f && p.y > 4.11f && p.y < 4.91f && p.z > 2f && p.z < 2.35f,
                        $"the porch light is under the bay over her door (plan {p.x:0.00}, {p.y:0.00}, {p.z:0.00})");
                }
            });
    }

    [MenuItem(Menu + "1 - Her window and her porch light (scene)", true)]
    static bool CanHerLights() => Playtest4Shared.NotPlaying();

    // ================================================================== 2 - Her front window: what she sees from inside (read-only)

    [MenuItem(Menu + "2 - Her front window: what she sees from inside (read-only)")]
    static void HerFrontWindow() => Playtest4Shared.Survey("Playtest 4 · Night · 2 - Her front window: what she sees from inside", "night-2-her-front-window", run =>
    {
        GraceAtHome home = Playtest4Shared.One<GraceAtHome>(run);
        GraceHouse house = home.house;
        run.Require(house != null && house.house != null, "Grace at home has no house (Break-ins 6 - Put Grace at home).");
        run.Info($"Grace at home: {Playtest4Shared.PathOf(home.gameObject)}; she stands at her front window {home.windowSeconds:0.0} s with each cup of tea " +
                 $"(GraceAtHome.windowSeconds); her front window is watched {home.WindowHours()}");
        run.Check(home.windowSeconds > 0f, "her pause at the glass is on (Window Seconds above 0: 0 would leave her eyes off the street for good)");

        // ---- the pane: a part of its own since Break-ins 1, clear glass (plan metres from its vertices) ----
        run.Section("The window as the scene has it (plan metres: X north along the street, Y toward it, z up from her floor)");
        Transform pane = FindChild(house.house, GraceHouse.FrontGlassName), drapes = FindChild(house.house, GraceHouse.FrontCurtainsName), trim = house.house.Find("Cream trim");
        run.Check(pane != null && pane.GetComponent<MeshFilter>() != null && pane.GetComponent<MeshFilter>().sharedMesh != null,
            $"her front window's pane is a part of its own, '{GraceHouse.FrontGlassName}' (the night gives it a collider by that name; her eyes look through it)");
        run.Check(drapes != null && drapes.GetComponent<MeshFilter>() != null && drapes.GetComponent<MeshFilter>().sharedMesh != null,
            $"her curtain panels are a part of their own, '{GraceHouse.FrontCurtainsName}' (solid by night: fabric)");
        run.Check(pane == null || pane.GetComponent<Collider>() == null, "the pane has no collider of its own by day (by night the night walk's copy, named by its path)");
        if (pane != null && Bounds(house, pane, out Vector3 paneMin, out Vector3 paneMax, null))
        {
            run.Info($"the pane: X {paneMin.x:0.000}-{paneMax.x:0.000}, z {paneMin.z:0.000}-{paneMax.z:0.000}, Y {paneMin.y:0.000}-{paneMax.y:0.000}");
            run.Check(Near(paneMin.x, GraceAtHome.WindowSouth) && Near(paneMax.x, GraceAtHome.WindowNorth) && Near(paneMin.z, GraceAtHome.WindowSill) && Near(paneMax.z, GraceAtHome.WindowHead),
                $"...as her eyes have the opening (X {GraceAtHome.WindowSouth}-{GraceAtHome.WindowNorth}, sill {GraceAtHome.WindowSill}, head {GraceAtHome.WindowHead})");
            run.Check(Near((paneMin.y + paneMax.y) * .5f, GraceAtHome.GlassY, .03f), $"...the glass at Y {(paneMin.y + paneMax.y) * .5f:0.000} (her eyes: {GraceAtHome.GlassY})");
        }
        // ---- the two curtain panels: 0.5 m each side, the middle clear ----
        if (drapes != null)
        {
            bool south = Bounds(house, drapes, out Vector3 sMin, out Vector3 sMax, p => p.x < (GraceAtHome.WindowSouth + GraceAtHome.WindowNorth) * .5f);
            bool north = Bounds(house, drapes, out Vector3 nMin, out Vector3 nMax, p => p.x >= (GraceAtHome.WindowSouth + GraceAtHome.WindowNorth) * .5f);
            if (south) run.Info($"the south drape: X {sMin.x:0.000}-{sMax.x:0.000}, z {sMin.z:0.000}-{sMax.z:0.000}, Y {sMin.y:0.000}-{sMax.y:0.000}");
            if (north) run.Info($"the north drape: X {nMin.x:0.000}-{nMax.x:0.000}, z {nMin.z:0.000}-{nMax.z:0.000}, Y {nMin.y:0.000}-{nMax.y:0.000}");
            run.Check(south && north && Near(sMin.x, GraceAtHome.DrapeSouthFrom) && Near(sMax.x, GraceAtHome.DrapeSouthTo) && Near(nMin.x, GraceAtHome.DrapeNorthFrom) && Near(nMax.x, GraceAtHome.DrapeNorthTo)
                      && Near((sMin.y + sMax.y) * .5f, GraceAtHome.DrapesY, .03f),
                $"...two panels where her eyes have them (X {GraceAtHome.DrapeSouthFrom}-{GraceAtHome.DrapeSouthTo} and {GraceAtHome.DrapeNorthFrom}-{GraceAtHome.DrapeNorthTo}, at Y {GraceAtHome.DrapesY}); " +
                $"the clear middle between them is {GraceAtHome.DrapeNorthFrom - GraceAtHome.DrapeSouthTo:0.00} m wide");
        }
        // ---- the trim in front of the glass: the jambs, the mullion and the transom rail, which block a grazing look ----
        if (trim != null && trim.GetComponent<MeshFilter>() != null && trim.GetComponent<MeshFilter>().sharedMesh != null)
        {
            var xs = new HashSet<float>();
            var zs = new HashSet<float>();
            float yMin = float.MaxValue, yMax = float.MinValue;
            int near = 0;
            foreach (Vector3 v in trim.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 p = house.Plan(trim.TransformPoint(v));
                if (p.x < 2.5f || p.x > 5f || p.z < .5f || p.z > 2.2f || p.y < 4.15f || p.y > 4.35f) continue;
                near++;
                xs.Add(Mathf.Round(p.x * 200f) / 200f);
                zs.Add(Mathf.Round(p.z * 200f) / 200f);
                yMin = Mathf.Min(yMin, p.y);
                yMax = Mathf.Max(yMax, p.y);
            }
            run.Info($"the trim round the glass: {near} corners, X {{{string.Join(", ", Sorted(xs))}}}, z {{{string.Join(", ", Sorted(zs))}}}, Y {yMin:0.000}-{yMax:0.000}");
            bool jambs = Has(xs, GraceAtHome.JambSouthFrom) && Has(xs, GraceAtHome.JambSouthTo) && Has(xs, GraceAtHome.JambNorthFrom) && Has(xs, GraceAtHome.JambNorthTo);
            bool mullion = Has(xs, GraceAtHome.MullionFrom) && Has(xs, GraceAtHome.MullionTo);
            bool transom = Has(zs, GraceAtHome.TransomFrom) && Has(zs, GraceAtHome.TransomTo);
            run.Check(near > 0 && jambs && mullion && transom && Near(yMin, GraceAtHome.TrimFrom, .02f) && Near(yMax, GraceAtHome.TrimTo, .02f),
                $"...its jambs (X {GraceAtHome.JambSouthFrom}-{GraceAtHome.JambSouthTo}, {GraceAtHome.JambNorthFrom}-{GraceAtHome.JambNorthTo}), its mullion (X {GraceAtHome.MullionFrom}-{GraceAtHome.MullionTo}) " +
                $"and its transom rail (z {GraceAtHome.TransomFrom}-{GraceAtHome.TransomTo}), {GraceAtHome.TrimFrom}-{GraceAtHome.TrimTo} out, are where her eyes have them");
        }
        else run.Check(false, "her house has a 'Cream trim' mesh (the window's frame)");
        // ---- the pier between her door and her window, her armchair and the TV ----
        Transform pier = FindChild(house.transform, "Street wall, by the window (above the sill)");
        if (pier != null && pier.GetComponent<MeshFilter>() != null && Bounds(house, pier, out Vector3 pMin, out Vector3 pMax, null))
            run.Check(Near(pMin.x, GraceAtHome.DoorNorth, .02f) && Near(pMax.x, GraceAtHome.WindowSouth, .02f),
                $"the pier between her door and her window spans X {pMin.x:0.000}-{pMax.x:0.000} (her eyes: {GraceAtHome.DoorNorth}-{GraceAtHome.WindowSouth}): her step is behind it from her front room");
        else run.Check(false, "her house has the pier 'Street wall, by the window (above the sill)' (Break-ins 1)");
        if (home.armchair != null)
        {
            Vector3 chair = house.Plan(home.armchair.position);
            run.Info($"her armchair at plan ({chair.x:0.00}, {chair.y:0.00}); the TV at (5.10, 2.83); the window spot of her map at (4.62, 3.45), 0.57 m from the glass");
            run.Check(chair.y < GraceAtHome.StreetWallInside - 1f && chair.x < GraceAtHome.WindowNorth, "her armchair stands back from the window, facing the TV along the wall, not the street");
        }
        // ---- the outside lights she sees by out there ----
        run.Check(home.porchLight != null && home.frontSpill != null, "her porch light and her front window's light on the pavement are wired (step 1): what she sees by outside");

        // ---- what the glass shows her, by the numbers her eyes work with ----
        run.Section("What the glass shows her (GraceAtHome.ThroughTheFrontWall; a standing head, 1.8 m up)");
        Vector3 seat = new Vector3(4.35f, 2.83f, 1.30f), glass = new Vector3(4.62f, 3.60f, 1.60f);
        run.Info("from her armchair (seated, facing the TV at +X; the window is 66°-145° off it, outside her TV slice of ±30° and her view of ±55°):");
        run.Info("    " + Bands(seat));
        Vector2 looks = new Vector2(GraceAtHome.StreetAhead.x - 4.62f, GraceAtHome.StreetAhead.y - 3.45f);
        float half = GraceNight.Cone * .5f;
        run.Info($"from the glass (standing at the window spot with her tea, looking at the road in front of her door, plan ({GraceAtHome.StreetAhead.x:0.00}, {GraceAtHome.StreetAhead.y:0.0}): " +
                 $"{Vector2.Angle(looks, Vector2.up):0}° off straight out toward her door, since straight out from the spot there is only the north drape; her cone ±{half:0}°):");
        run.Info("    " + Bands(glass));
        foreach (var (what, at) in new[] { ("on her step at her door", new Vector3(1.11f, 4.78f, 1.8f)), ("the foot of her steps", new Vector3(1.11f, 5.30f, Pavement + 1.8f)),
                                           ("where Barnaby is taken from", new Vector3(1.73f, 5.55f, Pavement + 1.8f)), ("a pace back from her door", new Vector3(1.20f, 5.50f, Pavement + 1.8f)),
                                           ("under her window, 1.7 m out", new Vector3(3.76f, 5.70f, Pavement + 1.8f)), ("2 m out in front of her door's middle", new Vector3(1.11f, 6.20f, Pavement + 1.8f)) })
        {
            GraceAtHome.Through fromSeat = GraceAtHome.ThroughTheFrontWall(seat, at, out _, out _, out string seatBy);
            GraceAtHome.Through fromGlass = GraceAtHome.ThroughTheFrontWall(glass, at, out _, out _, out string glassBy);
            float off = Vector2.Angle(looks, new Vector2(at.x - glass.x, at.y - glass.y));
            run.Info($"{what} ({at.x:0.00}, {at.y:0.00}): from her armchair {(fromSeat == GraceAtHome.Through.Window ? "in view of the glass (but off her TV)" : seatBy)}; " +
                     $"from the glass {(fromGlass == GraceAtHome.Through.Window ? (off <= half ? "SEEN" : $"through the glass but {off:0}° off where she looks") : glassBy)} ({off:0}° off where she looks)");
        }
        run.Check(Vector2.Angle(looks, new Vector2(1.20f - glass.x, 5.50f - glass.y)) < half - 15f && Vector2.Angle(looks, new Vector2(1.11f - glass.x, 6.20f - glass.y)) < half - 15f,
            "the pavement in front of her door, a pace to 2 m out, lies inside her cone from the glass with 15° to spare: the frame and the drapes decide there, not the cone's edge");
        run.Check(GraceAtHome.ThroughTheFrontWall(glass, new Vector3(1.11f, 4.78f, 1.8f), out _, out _) == GraceAtHome.Through.Wall,
            "pressed to her door Ace is out of her sight from the glass: the careful player's place");
        run.Check(GraceAtHome.ThroughTheFrontWall(glass, new Vector3(1.20f, 5.50f, Pavement + 1.8f), out _, out _) == GraceAtHome.Through.Window,
            "a pace back from her door Ace is in her sight from the glass: \"Hm?\" in about 0.7 s, caught in about 2 s standing still (twice that crouched)");
        run.Info("The labs: Night > Break-ins 7 - Grace at home: her front window (the rays against these numbers, her eye as it really stands); " +
                 "Night 2 - Play check (the careful route at her door, the nervous player's pace back); Night 1 (Barnaby at a walk, still calm by her timetable).");
    });

    [MenuItem(Menu + "2 - Her front window: what she sees from inside (read-only)", true)]
    static bool CanHerFrontWindow() => Playtest4Shared.NotPlaying();

    // The bands of X (plan metres) a standing head is seen in from an eye, at a few distances out from her wall.
    static string Bands(Vector3 eyePlan)
    {
        var text = new System.Text.StringBuilder();
        foreach (float y in new[] { 4.78f, 5.3f, 5.6f, 6.2f, 7.0f })
        {
            float from = float.NaN, last = float.NaN;
            var bands = new List<string>();
            for (float x = -1f; x <= 6.5f; x += .05f)
            {
                bool seen = GraceAtHome.ThroughTheFrontWall(eyePlan, new Vector3(x, y, (y < 5f ? 0f : Pavement) + 1.8f), out _, out _) == GraceAtHome.Through.Window;
                if (seen && float.IsNaN(from)) from = x;
                if (seen) last = x;
                if (!seen && !float.IsNaN(from)) { bands.Add($"{from:0.00}-{last:0.00}"); from = float.NaN; }
            }
            if (!float.IsNaN(from)) bands.Add($"{from:0.00}-{last:0.00}");
            text.Append($"{y - GraceAtHome.StreetWallOutside:0.0} m out: X {(bands.Count > 0 ? string.Join(", ", bands) : "nothing")}; ");
        }
        return text.ToString().TrimEnd(' ', ';') + " (her door X 0.46-1.76, the window X 2.64-4.89)";
    }

    // A mesh's corners (those a filter keeps) in plan metres: the least and the most along each axis.
    static bool Bounds(GraceHouse house, Transform part, out Vector3 min, out Vector3 max, Func<Vector3, bool> keep)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;
        int n = 0;
        foreach (Vector3 v in filter.sharedMesh.vertices)
        {
            Vector3 p = house.Plan(part.TransformPoint(v));
            if (keep != null && !keep(p)) continue;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            n++;
        }
        return n > 0;
    }

    static bool Near(float a, float b, float within = .015f) => Mathf.Abs(a - b) <= within;
    static bool Has(HashSet<float> values, float wanted) => values.Any(v => Mathf.Abs(v - wanted) <= .015f);
    static string[] Sorted(HashSet<float> values) => values.OrderBy(v => v).Select(v => v.ToString("0.000")).ToArray();

    // ================================================================== helpers

    static Light Wired(GraceAtHome home, string field)
    {
        if (home == null) return null;
        switch (field)
        {
            case "porchLight": return home.porchLight;
            case "frontSpill": return home.frontSpill;
            case "bedroomSpill": return home.bedroomSpill;
            default: return null;
        }
    }

    // Down and out toward the street (world), as the spec's tilt says.
    static Vector3 Aim(GraceHouse house, Spec s)
    {
        float t = s.tilt * Mathf.Deg2Rad;
        Vector3 from = house.World(s.at.x, s.at.y, s.at.z);
        Vector3 to = house.World(s.at.x, s.at.y + Mathf.Sin(t), s.at.z - Mathf.Cos(t));
        return (to - from).normalized;
    }

    // The light's aim in plan metres (X, Y, z).
    static Vector3 PlanAim(GraceHouse house, Light l)
    {
        Vector3 p = l.transform.position;
        return (house.Plan(p + l.transform.forward) - house.Plan(p)).normalized;
    }

    // How many points inside her walls (every 10 cm) and her bays (inside their glass, from her first floor up) are within
    // the light's range and its cone: lit through her walls, since it casts no shadow. 0 is the only right answer.
    static int LitInside(GraceHouse house, Light l)
    {
        if (house == null || l == null || l.type != LightType.Spot) return l == null ? 0 : -1;
        Vector3 p = house.Plan(l.transform.position), d = PlanAim(house, l);
        float half = l.spotAngle * .5f, range = l.range;
        int lit = 0;
        bool Lit(float x, float y, float z)
        {
            Vector3 v = new Vector3(x, y, z) - p;
            float m = v.magnitude;
            return m > 1e-4f && m < range && Vector3.Angle(d, v) < half;
        }
        for (float x = .05f; x <= 5.37f + 1e-4f; x += .1f)
            for (float y = .05f; y <= 3.97f + 1e-4f; y += .1f)
                for (float z = .05f; z <= 4.8f + 1e-4f; z += .1f)
                    if (Lit(x, y, z)) lit++;
        foreach (Vector2 bay in new[] { new Vector2(.61f, 2.11f), new Vector2(3.31f, 4.81f) })
            for (float x = bay.x + .05f; x <= bay.y - .05f + 1e-4f; x += .1f)
                for (float y = 4.05f; y <= 4.86f + 1e-4f; y += .05f)
                    for (float z = 2.45f; z <= 4.8f + 1e-4f; z += .1f)
                        if (Lit(x, y, z)) lit++;
        return lit;
    }

    static string Describe(Light l) =>
        l == null ? "none" : $" ({l.type}, {l.spotAngle:0}°, range {l.range:0.0} m, intensity {l.intensity:0.00}, " +
                             $"{(l.enabled ? "ON in the scene" : "off in the scene")}, shadows {l.shadows})";

    static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindChild(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
#endif
