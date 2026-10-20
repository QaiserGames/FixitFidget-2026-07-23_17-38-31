#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// NIGHT GUIDANCE (playtest 7, 19 Oct 2026; his girlfriend, dyslexic, first time: "What's saffron how?" ... "Is this West
// Street or is that West Street?")
//
//   Fixit Fidget > Night > Night guidance 1 - Only Grace's house is yellow; her porch light reads from the street (scene, with Undo)
//   Fixit Fidget > Night > Night guidance 1 - Undo (the other house saffron again, her porch light as Playtest 4 left it)
//
// The night names her house one way now, "12 West Street, the yellow house with the porch light" (LodgerStory, NightThings,
// HomeDoor). Two things in the scene stood in the way of that, both from the street builder (CafeStreetUpgrade: six
// bay-window houses, "Saffron, Dusty rose, Sea green, Lavender, Sky blue, Saffron"):
//   * West Street had TWO saffron houses: hers (#1, by the Front Street corner) and #6 by the Back Street end, which is the
//     first house a player walking from the bins reaches, "on a corner" too. #6 is repainted from the street's own palette
//     (Dusty rose: the warm one, two doors from the other dusty rose at #2, nothing like its sky-blue neighbour at #5). Its
//     name stays ("6 - Saffron bay-window house": StreetDoorSteps and the surveys find it by name), only its paint changes.
//   * Her porch light (Playtest 4 › Night › 1: a 2.2 spot, 4 m, under the bay over her door) was "barely visible" from the
//     night's 34 m view (the reviewers; "no fixture glows"). Its intensity goes up (the pool on her step and door reads), its
//     range and cone stay (they are what she sees Ace by outside: GraceAtHome.LitOutside; the stealth balance is untouched),
//     and a small glowing lamp (an emissive sphere, URP Lit) is hung at the light: a bright point at any zoom, hers alone on
//     that block (the street lamps' glass glows at 0.65; hers at 2.4).
// The step records every change in its report (Logs/Playtest4/night-guidance-1-<time>.txt), photographs West Street from above
// and her door before and after (Logs/Night/night-guidance-1-<time>/), holds one Undo group (Edit › Undo), marks the scene
// dirty and never saves it. Any failed check undoes it all. The Undo item puts it back by inspection, in any later session.
// ---------------------------------------------------------------------------
internal static class NightGuidanceSteps
{
    const string Menu = "Fixit Fidget/Night/";
    const string Title = "Night guidance 1 - Only Grace's house is yellow; her porch light reads from the street";
    const string Label = "night-guidance-1";
    const string StreetFolder = "Assets/Playtests/AcesCafeLayout";
    const string PalettePath = StreetFolder + "/Street palette.asset";
    const string LampMaterialPath = StreetFolder + "/GH_Porch lamp glass.mat";
    /// <summary>The street's other saffron house (StreetDoorSteps.Buildings[5]): its name is kept, only its paint changes.</summary>
    public const string OtherHouse = "6 - Saffron bay-window house";
    public const string Saffron = "Saffron", Repaint = "Dusty rose";
    public const string LampName = "Her porch lamp (glows: the bright point on her block)";
    /// <summary>Her porch light's intensity after the step (Playtest 4 left it at 2.2); its range and cone are not touched.</summary>
    public const float PorchIntensity = 6f, PorchWas = 2.2f;
    public const float LampRadius = .14f, LampGlow = 2.4f;
    static readonly Color LampColour = new Color(1f, .84f, .54f), LampEmission = new Color(1f, .70f, .34f);

    // The night's 34 m framing of West Street (the overhead's tilt 50, yaw 45, 34 m, 41 lens: CafeViewMode), and her door.
    static readonly Vector3 StreetFrom = new Vector3(5f, 26f, -24f), StreetAt = new Vector3(-16f, 0f, 2f);
    static readonly Vector3 DoorFrom = new Vector3(-9f, 2.4f, -5.5f), DoorAt = new Vector3(-17.4f, 1.6f, -3.6f);

    [MenuItem(Menu + Title + " (scene, with Undo)")]
    static void Apply()
    {
        Transform other = null;
        GraceAtHome home = null;
        Light porch = null;
        Material saffron = null, repaint = null;
        float rangeWas = 0f, coneWas = 0f;
        var painted = new List<MeshRenderer>();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", Label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss")));
        Playtest4Shared.Step(Title, Label,
            survey: run =>
            {
                other = StreetDoorSteps.FindOptional(OtherHouse);
                run.Require(other != null, $"'{OtherHouse}' is not in the scene (CafeStreetUpgrade builds the street).");
                Material[] palette = AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Material>().ToArray();
                run.Require(palette.Length > 0, $"The street palette is missing ({PalettePath}).");
                saffron = palette.FirstOrDefault(m => m.name == Saffron);
                repaint = palette.FirstOrDefault(m => m.name == Repaint);
                run.Require(repaint != null, $"The street palette has no '{Repaint}' material to repaint with.");
                foreach (MeshRenderer r in other.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterials.Any(IsSaffron)) painted.Add(r);
                run.Info($"{OtherHouse}: {painted.Count} part(s) painted saffron (of {other.GetComponentsInChildren<MeshRenderer>(true).Length})");
                home = Playtest4Shared.One<GraceAtHome>(run);
                porch = home.porchLight;
                run.Require(porch != null, "Grace at home has no porch light wired (Fixit Fidget › Playtest 4 › Night › 1 puts it in).");
                rangeWas = porch.range;
                coneWas = porch.spotAngle;
                run.Info($"her porch light: {Playtest4Shared.PathOf(porch.gameObject)}: intensity {porch.intensity:0.##}, range {porch.range:0.##} m, " +
                         $"cone {porch.spotAngle:0}°{(porch.transform.Find(LampName) != null ? "; her lamp already hangs at it" : "")}");
                Transform grace = StreetDoorSteps.FindOptional(StreetDoorSteps.Buildings[0]);
                run.Info(grace != null ? $"her house: {Playtest4Shared.PathOf(grace.gameObject)} (its paint is left alone: hers is the yellow one)" : "her house wasn't found by name");
                Directory.CreateDirectory(folder);
                CafeSecondPassSteps.Capture(Path.Combine(folder, "1-west-street-before.png"), StreetFrom, StreetAt, 41f, false);
                CafeSecondPassSteps.Capture(Path.Combine(folder, "2-her-door-before.png"), DoorFrom, DoorAt, 55f, false);
                run.Info("photographed before: " + folder);
            },
            change: run =>
            {
                // 1. The other saffron house, repainted from the palette (every saffron slot, the rest of its paint as it is).
                int slots = 0;
                foreach (MeshRenderer r in painted)
                {
                    Material[] wears = r.sharedMaterials;
                    bool any = false;
                    for (int i = 0; i < wears.Length; i++)
                        if (IsSaffron(wears[i])) { wears[i] = repaint; any = true; slots++; }
                    if (!any) continue;
                    run.Record(r);
                    r.sharedMaterials = wears;
                }
                if (painted.Count > 0) run.Changed($"{OtherHouse}: {painted.Count} part(s) ({slots} slot(s)) repainted {Saffron} → {Repaint}");
                else run.Info($"{OtherHouse}: nothing painted saffron (already repainted)");

                // 2. Her porch light: brighter where it lands (her step, her door); the same reach and cone (her sight outside).
                run.SetFloat(porch, "m_Intensity", PorchIntensity, "her porch light · intensity");

                // 3. Her lamp: a small glowing sphere at the light, the bright point on her block at any zoom.
                if (porch.transform.Find(LampName) == null)
                {
                    Material glass = LampMaterial(run);
                    GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    lamp.name = LampName;
                    Undo.RegisterCreatedObjectUndo(lamp, Title);
                    Undo.SetTransformParent(lamp.transform, porch.transform, Title);
                    lamp.transform.localPosition = Vector3.zero;
                    lamp.transform.localRotation = Quaternion.identity;
                    lamp.transform.localScale = Vector3.one * (LampRadius * 2f);
                    lamp.layer = porch.gameObject.layer;
                    Collider ball = lamp.GetComponent<Collider>();
                    if (ball != null) Undo.DestroyObjectImmediate(ball);   // a lamp is not solid
                    MeshRenderer mr = lamp.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = glass;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    run.Changed($"{Playtest4Shared.PathOf(lamp)}: made (a {LampRadius * 2f:0.00} m glowing sphere at her porch light, {AssetDatabase.GetAssetPath(glass)})");
                }
                else run.Info("her lamp already hangs at her porch light");
            },
            checks: run =>
            {
                int saffronLeft = other.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.sharedMaterials.Any(IsSaffron));
                run.Check(saffronLeft == 0, $"{OtherHouse} wears no saffron now ({saffronLeft} part(s) still do)");
                int rose = other.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.sharedMaterials.Contains(repaint));
                run.Check(rose >= painted.Count, $"...its {rose} repainted part(s) wear {Repaint}");
                Transform grace = StreetDoorSteps.FindOptional(StreetDoorSteps.Buildings[0]);
                int hers = grace != null ? grace.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.sharedMaterials.Any(IsSaffron)) : -1;
                run.Check(hers != 0, $"her house ({StreetDoorSteps.Buildings[0]}) is still the saffron one ({hers} part(s))");
                run.Check(Mathf.Approximately(porch.intensity, PorchIntensity) && Mathf.Approximately(porch.range, rangeWas) && Mathf.Approximately(porch.spotAngle, coneWas),
                    $"her porch light: intensity {porch.intensity:0.##} (from {PorchWas:0.##}); range {porch.range:0.##} m and cone {porch.spotAngle:0}° untouched (what she sees Ace by outside)");
                Transform lamp = porch.transform.Find(LampName);
                MeshRenderer glow = lamp != null ? lamp.GetComponent<MeshRenderer>() : null;
                run.Check(glow != null && glow.sharedMaterial != null && glow.sharedMaterial.IsKeywordEnabled("_EMISSION")
                          && lamp.GetComponent<Collider>() == null && (lamp.position - porch.transform.position).magnitude < .01f,
                    "her lamp hangs at her porch light: a glowing sphere, no collider");
                CafeSecondPassSteps.Capture(Path.Combine(folder, "3-west-street-after.png"), StreetFrom, StreetAt, 41f, false);
                CafeSecondPassSteps.Capture(Path.Combine(folder, "4-her-door-after.png"), DoorFrom, DoorAt, 55f, false);
                run.Info("photographed after: " + folder + " (by day: the night's lab photos show the lamp lit, Night 0 / Night 1 play checks)");
            });
    }

    [MenuItem(Menu + Title + " (scene, with Undo)", true)]
    [MenuItem(Menu + "Night guidance 1 - Undo (the other house saffron again, her porch light as Playtest 4 left it)", true)]
    static bool NotPlaying() => Playtest4Shared.NotPlaying();

    // Put back by inspection (any later session): the other house's dusty rose slots saffron again, her porch light at 2.2,
    // her lamp switched off (never deleted: a scene object is switched off and said so).
    [MenuItem(Menu + "Night guidance 1 - Undo (the other house saffron again, her porch light as Playtest 4 left it)")]
    static void PutBack()
    {
        Playtest4Shared.Step("Night guidance 1 - Undo", Label + "-undo",
            survey: run =>
            {
                run.Require(StreetDoorSteps.FindOptional(OtherHouse) != null, $"'{OtherHouse}' is not in the scene.");
                run.Require(Playtest4Shared.One<GraceAtHome>(run).porchLight != null, "Grace at home has no porch light wired.");
            },
            change: run =>
            {
                Transform other = StreetDoorSteps.FindOptional(OtherHouse);
                Material[] palette = AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Material>().ToArray();
                Material saffron = palette.FirstOrDefault(m => m.name == Saffron), rose = palette.FirstOrDefault(m => m.name == Repaint);
                run.Require(saffron != null && rose != null, "The street palette lacks Saffron or Dusty rose.");
                int parts = 0;
                foreach (MeshRenderer r in other.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] wears = r.sharedMaterials;
                    bool any = false;
                    for (int i = 0; i < wears.Length; i++)
                        if (wears[i] == rose) { wears[i] = saffron; any = true; }
                    if (!any) continue;
                    run.Record(r);
                    r.sharedMaterials = wears;
                    parts++;
                }
                if (parts > 0) run.Changed($"{OtherHouse}: {parts} part(s) {Repaint} → {Saffron}");
                Light porch = Playtest4Shared.One<GraceAtHome>(run).porchLight;
                run.SetFloat(porch, "m_Intensity", PorchWas, "her porch light · intensity");
                Transform lamp = porch.transform.Find(LampName);
                if (lamp != null) run.SwitchOff(lamp.gameObject, "her lamp: the step's, put away");
            },
            checks: run =>
            {
                Transform other = StreetDoorSteps.FindOptional(OtherHouse);
                run.Check(other.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.sharedMaterials.Any(IsSaffron)), $"{OtherHouse} wears saffron again");
                Light porch = Playtest4Shared.One<GraceAtHome>(run).porchLight;
                run.Check(Mathf.Approximately(porch.intensity, PorchWas), $"her porch light is back at {PorchWas:0.##}");
            });
    }

    // The saffron paint is the palette's MAIN material, named "Street palette" (CafeStreetUpgrade: mats["Saffron"] = the
    // asset's main object, BaseColor .88/.68/.32), not a sub-asset named "Saffron" (20 Oct, the first run on his Mac found 0).
    const string SaffronMain = "Street palette";
    static bool IsSaffron(Material m) => m != null && (m.name == SaffronMain || m.name.StartsWith(Saffron, StringComparison.Ordinal));

    // Her lamp's glass: URP Lit, warm, emissive (made once as an asset beside the street's palette; saved once the checks pass).
    static Material LampMaterial(Playtest4Shared.Run run)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
        if (existing != null) return existing;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        run.Require(lit != null, "URP's Lit shader wasn't found (the project is URP).");
        var m = new Material(lit) { name = Path.GetFileNameWithoutExtension(LampMaterialPath) };
        m.SetColor("_BaseColor", LampColour);
        m.SetFloat("_Smoothness", .6f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", LampEmission * LampGlow);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(m, LampMaterialPath);
        run.SaveAsset(m);
        run.Info("made " + LampMaterialPath + " (her lamp's glass)");
        return m;
    }
}
#endif
