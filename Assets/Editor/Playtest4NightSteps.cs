#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// PLAYTEST 4 FIXES: THE NIGHT (Lead 5, 10 Oct 2026; the playtesters' reports, Docs/Studio/playtests/*-2026-10-10.md)
//
//   Fixit Fidget > Playtest 4 > Night > 0 - Check first                              (read-only)
//   Fixit Fidget > Playtest 4 > Night > 1 - Her window and her porch light (scene)
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
