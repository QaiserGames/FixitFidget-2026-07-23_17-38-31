using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A scene-bound lighting pass driven by the existing shop clock. It owns no
/// timers or closing rules; the same day progression drives work and sunset.
/// A night walk (night step 4, <see cref="NightWalk"/>) can hold the hour at night,
/// add a moon and dim the café's own lights through <see cref="SetNight"/>; by
/// default all three are neutral, so the day plays exactly as before.
/// </summary>
[DisallowMultipleComponent]
public sealed class CafeDaylight : MonoBehaviour
{
    [Header("Scene references")]
    public DayClock clock;
    public Light sun;
    [Tooltip("Use Skybox/Procedural with a sun disk. A private copy is used during play.")]
    public Material sky;
    [Tooltip("Authored intensities are the evening brightness; use a few shadowless lights.")]
    public Light[] warmLights = Array.Empty<Light>();
    [Tooltip("Only these renderers' matching materials receive the evening window/lamp glow.")]
    public Renderer[] emissiveRenderers = Array.Empty<Renderer>();
    public Material[] emissiveMaterials = Array.Empty<Material>();

    [Header("Neighborhood atmosphere")]
    [Range(0f, 360f)] public float morningSunAzimuth = 110f;
    [Range(0f, 360f)] public float eveningSunAzimuth = 255f;
    [Min(0f)] public float daylightIntensity = 1.25f;
    [Range(0f, 1f)] public float daytimeLampStrength = .4f;
    [ColorUsage(false, true)] public Color eveningGlow = new Color(1.35f, .70f, .27f);
    public bool useDistanceHaze = true;
    [Min(1f)] public float hazeStart = 55f;
    [Min(2f)] public float hazeEnd = 160f;

    [Header("Night walk: the moon (only while a night walk runs)")]
    [Range(10f, 80f)] public float moonAltitude = 42f;
    [Range(0f, 360f)] public float moonAzimuth = 215f;
    public Color moonColor = new Color(.62f, .72f, 1f);
    [Min(0f)] public float moonIntensity = .32f;

    /// <summary>The café room: lights inside it are the café's own, dimmed while it is closed for the night.</summary>
    public static readonly Rect CafeInside = Rect.MinMaxRect(-7.4f, .1f, 7.4f, 18f);

    /// <summary>Night walk: the hour held (null: the clock's), the moon (0-1) and the café's own lights (1: as by day).</summary>
    public float? HourOverride { get; private set; }
    public float MoonStrength { get; private set; }
    public float InteriorLampScale { get; private set; } = 1f;

    public struct LightingSample
    {
        public float hour, altitude, sunStrength, dusk, night, skyExposure;
        public Color sunColor, skyTint, ambientSky, ambientEquator, ambientGround, hazeColor;
    }

    struct RendererState { public Renderer renderer; public Material[] materials; }
    struct LightState { public Light light; public float intensity; public bool inside; }

    readonly List<RendererState> rendererStates = new();
    readonly List<LightState> lightStates = new();
    readonly Dictionary<Material, Material> glowCopies = new();
    Material skyCopy, previousSky;
    Light previousSun;
    Quaternion previousSunRotation;
    Color previousSunColor;
    float previousSunIntensity, previousReflectionIntensity;
    AmbientMode previousAmbientMode;
    Color previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFogColor;
    bool previousFog, captured;
    FogMode previousFogMode;
    float previousFogStart, previousFogEnd, lastAppliedHour = float.NaN, nextUpdate;

    static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    static readonly int SkyTint = Shader.PropertyToID("_SkyTint");
    static readonly int Exposure = Shader.PropertyToID("_Exposure");
    static readonly int GroundColor = Shader.PropertyToID("_GroundColor");
    static readonly int AtmosphereThickness = Shader.PropertyToID("_AtmosphereThickness");

    void Start()
    {
        if (clock == null) clock = DayClock.Instance;
        Capture();
        ApplyAtHour(HourOverride ?? (clock != null ? clock.CurrentHour : 9f));
    }

    void Update()
    {
        // Unscaled scheduling lets the final closing light reach a paused recap,
        // while CurrentHour itself remains frozen when the player pauses.
        if ((clock == null && !HourOverride.HasValue) || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .1f;
        float hour = HourOverride ?? clock.CurrentHour;
        if (!captured || Mathf.Abs(hour - lastAppliedHour) > .0001f)
            ApplyAtHour(hour);
    }

    /// <summary>
    /// Night walk: hold the hour (null hands it back to the clock), add a moon (0-1)
    /// and scale the café's own lights inside the room (1 = as by day). Applied at once.
    /// </summary>
    public void SetNight(float? hour, float moon, float interiorLamps)
    {
        HourOverride = hour.HasValue ? Mathf.Clamp(hour.Value, 0f, 24f) : (float?)null;
        MoonStrength = Mathf.Clamp01(moon);
        InteriorLampScale = Mathf.Max(0f, interiorLamps);
        if (Application.isPlaying) ApplyAtHour(HourOverride ?? (clock != null ? clock.CurrentHour : 9f));
    }

    /// <summary>Pure sampling for editor previews/checks; does not advance the day or change the scene.</summary>
    public static LightingSample EvaluateAtHour(float hour)
    {
        hour = Mathf.Clamp(hour, 0f, 24f);
        float night = Mathf.Max(1f - Smooth(5.8f, 7.2f, hour), Smooth(18.8f, 20f, hour));
        float dusk = Smooth(16.4f, 19f, hour);
        float altitude = Mathf.Sin((hour - 6.5f) / 12.75f * Mathf.PI) * 60f;
        float strength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 28f, altitude));
        Color daylightSky = new Color(.59f, .70f, .84f);
        Color eveningSky = new Color(.58f, .43f, .40f);
        return new LightingSample
        {
            hour = hour,
            altitude = altitude,
            dusk = dusk,
            night = night,
            sunStrength = strength,
            sunColor = Color.Lerp(new Color(1f, .94f, .80f), new Color(1f, .42f, .18f), dusk),
            skyExposure = Mathf.Lerp(1.05f, .13f, night),
            skyTint = Color.Lerp(Color.Lerp(new Color(.49f, .52f, .56f),
                new Color(.65f, .46f, .36f), dusk), new Color(.18f, .23f, .36f), night),
            ambientSky = Color.Lerp(Color.Lerp(daylightSky, eveningSky, dusk), new Color(.10f, .14f, .23f), night),
            ambientEquator = Color.Lerp(new Color(.40f, .43f, .47f), new Color(.12f, .11f, .16f), night),
            ambientGround = Color.Lerp(new Color(.25f, .22f, .19f), new Color(.055f, .055f, .07f), night),
            hazeColor = Color.Lerp(Color.Lerp(new Color(.68f, .77f, .83f),
                new Color(.79f, .57f, .42f), dusk), new Color(.075f, .10f, .17f), night)
        };
    }

    /// <summary>Apply a visual sample without changing the clock. The next clock tick resumes normal lighting.</summary>
    public void ApplyAtHour(float hour)
    {
        // Runtime copies protect the authored material assets. Edit-mode tools
        // should use EvaluateAtHour rather than leaving unsaved clone references.
        if (!Application.isPlaying) return;
        if (!captured) Capture();
        var sample = EvaluateAtHour(hour);
        lastAppliedHour = sample.hour;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sample.altitude,
                Mathf.LerpAngle(morningSunAzimuth, eveningSunAzimuth, Mathf.InverseLerp(9f, 20f, sample.hour)), 0f);
            sun.color = sample.sunColor;
            sun.intensity = daylightIntensity * sample.sunStrength;
            // Night walk only: once the sun is down, the same light becomes the moon.
            float moon = MoonStrength * sample.night * Mathf.Clamp01(1f - sample.sunStrength * 4f);
            if (moon > 0f)
            {
                sun.transform.rotation = Quaternion.Euler(moonAltitude, moonAzimuth, 0f);
                sun.color = moonColor;
                sun.intensity = moonIntensity * moon;
            }
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sample.ambientSky;
        RenderSettings.ambientEquatorColor = sample.ambientEquator;
        RenderSettings.ambientGroundColor = sample.ambientGround;
        RenderSettings.reflectionIntensity = Mathf.Lerp(previousReflectionIntensity, .2f, sample.night);
        if (useDistanceHaze)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = hazeStart;
            RenderSettings.fogEndDistance = Mathf.Max(hazeStart + 1f, hazeEnd);
            RenderSettings.fogColor = sample.hazeColor;
        }
        if (skyCopy != null)
        {
            SetColor(skyCopy, SkyTint, sample.skyTint);
            SetColor(skyCopy, GroundColor, sample.ambientGround);
            SetFloat(skyCopy, Exposure, sample.skyExposure);
            SetFloat(skyCopy, AtmosphereThickness, Mathf.Lerp(1f, 1.2f, sample.dusk));
        }
        float evening = Mathf.Max(sample.night, Smooth(17f, 19.3f, sample.hour));
        float lamps = Mathf.Lerp(daytimeLampStrength, 1f, evening);
        foreach (var state in lightStates)
            if (state.light != null) state.light.intensity = state.intensity * lamps * (state.inside ? InteriorLampScale : 1f);
        foreach (var copy in glowCopies.Values)
            copy.SetColor(Emission, eveningGlow * Mathf.Lerp(.035f, 1f, evening));
    }

    void Capture()
    {
        if (captured) return;
        captured = true;
        previousSky = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientSky = RenderSettings.ambientSkyColor;
        previousAmbientEquator = RenderSettings.ambientEquatorColor;
        previousAmbientGround = RenderSettings.ambientGroundColor;
        previousReflectionIntensity = RenderSettings.reflectionIntensity;
        previousFog = RenderSettings.fog;
        previousFogMode = RenderSettings.fogMode;
        previousFogStart = RenderSettings.fogStartDistance;
        previousFogEnd = RenderSettings.fogEndDistance;
        previousFogColor = RenderSettings.fogColor;
        if (sun != null)
        {
            previousSunRotation = sun.transform.rotation;
            previousSunColor = sun.color;
            previousSunIntensity = sun.intensity;
            RenderSettings.sun = sun;
        }
        Material sourceSky = sky != null ? sky : previousSky;
        if (sourceSky != null)
        {
            skyCopy = new Material(sourceSky) { name = sourceSky.name + " (day cycle)", hideFlags = HideFlags.DontSave };
            RenderSettings.skybox = skyCopy;
        }
        foreach (var light in warmLights)
            if (light != null && light != sun)
            {
                Vector3 p = light.transform.position;
                lightStates.Add(new LightState { light = light, intensity = light.intensity, inside = CafeInside.Contains(new Vector2(p.x, p.z)) });
            }
        foreach (var material in emissiveMaterials)
        {
            if (material == null || glowCopies.ContainsKey(material) || !material.HasProperty(Emission)) continue;
            var copy = new Material(material) { name = material.name + " (evening glow)", hideFlags = HideFlags.DontSave };
            copy.EnableKeyword("_EMISSION");
            glowCopies.Add(material, copy);
        }
        foreach (var renderer in emissiveRenderers)
        {
            if (renderer == null) continue;
            var originals = renderer.sharedMaterials;
            var replacements = (Material[])originals.Clone();
            bool changed = false;
            for (int i = 0; i < replacements.Length; i++)
                if (replacements[i] != null && glowCopies.TryGetValue(replacements[i], out var replacement))
                { replacements[i] = replacement; changed = true; }
            if (!changed) continue;
            rendererStates.Add(new RendererState { renderer = renderer, materials = originals });
            renderer.sharedMaterials = replacements;
        }
    }

    void OnDisable()
    {
        if (!captured) return;
        RenderSettings.skybox = previousSky;
        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousAmbientSky;
        RenderSettings.ambientEquatorColor = previousAmbientEquator;
        RenderSettings.ambientGroundColor = previousAmbientGround;
        RenderSettings.reflectionIntensity = previousReflectionIntensity;
        RenderSettings.fog = previousFog;
        RenderSettings.fogMode = previousFogMode;
        RenderSettings.fogStartDistance = previousFogStart;
        RenderSettings.fogEndDistance = previousFogEnd;
        RenderSettings.fogColor = previousFogColor;
        if (sun != null)
        {
            sun.transform.rotation = previousSunRotation;
            sun.color = previousSunColor;
            sun.intensity = previousSunIntensity;
        }
        foreach (var state in lightStates)
            if (state.light != null) state.light.intensity = state.intensity;
        foreach (var state in rendererStates)
            if (state.renderer != null) state.renderer.sharedMaterials = state.materials;
        foreach (var copy in glowCopies.Values) Destroy(copy);
        if (skyCopy != null) Destroy(skyCopy);
        glowCopies.Clear(); rendererStates.Clear(); lightStates.Clear();
        skyCopy = null;
        captured = false;
        lastAppliedHour = float.NaN;
        nextUpdate = 0f;
    }

    static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
    static void SetColor(Material material, int property, Color value)
    { if (material.HasProperty(property)) material.SetColor(property, value); }
    static void SetFloat(Material material, int property, float value)
    { if (material.HasProperty(property)) material.SetFloat(property, value); }
}
