using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// THE NIGHT SKY: STARS AND THE MOON (the second playtest, 29 Sept;
// claude/playtest-2-plan.md §4.6 in the project)
//
// The night walk's sky was Unity's procedural sky turned dark, with the sun's
// light turned into moonlight and nothing in it. This draws what was missing:
//
//   * a field of stars, a few dozen of them brighter, some a little blue or
//     warm, a handful twinkling; they thin out towards the horizon;
//   * the moon, where the moonlight comes from (CafeDaylight's Moon Altitude and
//     Moon Azimuth), with a soft glow round it and no stars in front of it.
//
// It is one mesh of small quads, drawn by the shader "Fixit Fidget/Night sky"
// (Night walk - sky.shader) round whichever camera is drawing, just inside its
// far plane: the stars never come closer as Ace walks, and anything nearer (a
// building, a lamp post) hides them. The light is added, and there is no fog.
// The stars are rolled from a fixed seed with their own random numbers, so it
// is the same sky every night and nothing else's random stream moves.
//
// CafeDaylight makes it the first time the moon is up (only on a night walk)
// and says how strong it is: at 0 the renderer is off and nothing is drawn. By
// day it doesn't exist. Only first person ever sees the sky; the overhead
// camera looks down at the street.
//
// A player build must include the shader (it is looked up by name), as with the
// see-through shader.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightSky : MonoBehaviour
{
    public const string ShaderName = "Fixit Fidget/Night sky";

    [Tooltip("How many ordinary stars.")]
    [Range(0, 6000)] public int starCount = 1600;
    [Tooltip("How many brighter stars, on top of those.")]
    [Range(0, 300)] public int brightCount = 40;
    [Tooltip("An ordinary star's size: the half-width of its point in the sky, in degrees. Brighter stars are about twice as big.")]
    [Range(.02f, .6f)] public float starSize = .16f;
    [Tooltip("The moon's radius in the sky, in degrees. The real moon's is a quarter of a degree; bigger reads better on a screen.")]
    [Range(.2f, 6f)] public float moonRadius = 1.6f;
    [Tooltip("No stars within this angle of the moon, in degrees.")]
    [Range(0f, 30f)] public float clearAroundMoon = 5f;
    [Tooltip("The sky is rolled from this number: the same number, the same sky.")]
    public int seed = 2909;

    // The moon's disc takes this share of its quad; the rest is its glow.
    const float MoonDiscShare = .24f;

    static readonly int StrengthId = Shader.PropertyToID("_Strength");
    static readonly int MoonDiscId = Shader.PropertyToID("_MoonDisc");

    MeshRenderer view;
    Mesh mesh;
    Material material;
    Vector3 builtForMoon;
    bool built, missingShader;
    float strength;
    int stars, brightStars;

    /// <summary>Makes the night sky under a parent (CafeDaylight). Made at run time, never saved.</summary>
    public static NightSky Create(Transform parent)
    {
        var go = new GameObject("Night sky (stars and the moon)") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(parent, false);
        return go.AddComponent<NightSky>();
    }

    /// <summary>
    /// How strong the stars and the moon are (0-1; 0: nothing is drawn), and the way to
    /// the moon from the ground (a unit vector). Rebuilds the sky only if the moon moved.
    /// </summary>
    public void Show(float amount, Vector3 towardMoon)
    {
        strength = Mathf.Clamp01(amount);
        if (strength <= 0f)
        {
            if (view != null) view.enabled = false;
            return;
        }
        if (!Prepare()) return;
        towardMoon = towardMoon.sqrMagnitude > 0f ? towardMoon.normalized : Vector3.up;
        if (!built || Vector3.Angle(builtForMoon, towardMoon) > .01f) Build(towardMoon);
        material.SetFloat(StrengthId, strength);
        view.enabled = true;
    }

    /// <summary>What is in the sky, for the night's reports.</summary>
    public string Describe()
    {
        if (missingShader) return $"Night sky: not drawn (the shader '{ShaderName}' is missing).";
        if (!built) return "Night sky: not built yet.";
        float up = Mathf.Asin(Mathf.Clamp(builtForMoon.y, -1f, 1f)) * Mathf.Rad2Deg;
        float toward = Mathf.Repeat(Mathf.Atan2(builtForMoon.x, builtForMoon.z) * Mathf.Rad2Deg, 360f);
        return $"Night sky: {stars} stars ({brightStars} of them brighter) and the moon, {up:0}° up towards {toward:0}°; " +
               $"strength {strength:0.00}, {(view != null && view.enabled ? "drawn" : "not drawn")}.";
    }

    bool Prepare()
    {
        if (material != null) return true;
        if (missingShader) return false;
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            missingShader = true;
            Debug.LogWarning($"[Night sky] The shader '{ShaderName}' is missing, so the night sky stays empty.");
            return false;
        }
        material = new Material(shader) { name = "Night sky (run time)", hideFlags = HideFlags.DontSave };
        material.SetFloat(MoonDiscId, MoonDiscShare);
        mesh = new Mesh { name = "Night sky: stars and the moon", hideFlags = HideFlags.DontSave };
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        view = gameObject.AddComponent<MeshRenderer>();
        view.sharedMaterial = material;
        view.shadowCastingMode = ShadowCastingMode.Off;
        view.receiveShadows = false;
        view.lightProbeUsage = LightProbeUsage.Off;
        view.reflectionProbeUsage = ReflectionProbeUsage.Off;
        view.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        view.allowOcclusionWhenDynamic = false;
        view.enabled = false;
        return true;
    }

    // Every star is a quad: four corners that all carry the star's direction (the shader
    // spreads them round it, facing the camera), its colour and brightness, its size and
    // how it twinkles. The moon is one more quad, marked as the moon.
    void Build(Vector3 towardMoon)
    {
        builtForMoon = towardMoon;
        built = true;
        var directions = new List<Vector3>();
        var colours = new List<Color>();
        var corners = new List<Vector4>();   // x, y: the corner; z: half-size (radians); w: 0 a star, 1 the moon
        var twinkles = new List<Vector2>();  // phase, speed
        var triangles = new List<int>();
        var rng = new System.Random(seed);
        float clear = Mathf.Cos(clearAroundMoon * Mathf.Deg2Rad);
        int total = Mathf.Max(0, starCount) + Mathf.Max(0, brightCount);
        stars = brightStars = 0;
        for (int i = 0; i < total; i++)
        {
            // Every star rolls the same numbers whether or not it is kept, so the rest of
            // the sky stays put if the moon moves.
            float height = Mathf.Lerp(.02f, 1f, Next(rng));   // an even height is an even spread over a sphere
            float around = Next(rng) * Mathf.PI * 2f;
            float light = Next(rng), size = Next(rng), tint = Next(rng), flicker = Next(rng), phase = Next(rng), speed = Next(rng);
            float ring = Mathf.Sqrt(1f - height * height);
            var direction = new Vector3(ring * Mathf.Cos(around), height, ring * Mathf.Sin(around));
            if (Vector3.Dot(direction, towardMoon) > clear) continue;

            bool bright = i >= starCount;
            float brightness = bright ? .9f + .5f * light : .16f + .64f * Mathf.Pow(light, 2.2f);
            float degrees = starSize * (bright ? 1.7f + .8f * size : .8f + .5f * size);
            Color colour = tint < .14f ? new Color(.78f, .86f, 1f) : tint < .28f ? new Color(1f, .9f, .76f) : Color.white;
            colour *= brightness;
            colour.a = flicker < .2f ? .18f + .2f * phase : 0f;   // how much it twinkles
            AddQuad(directions, colours, corners, twinkles, triangles, direction, colour,
                degrees * Mathf.Deg2Rad, 0f, new Vector2(phase * Mathf.PI * 2f, 1.5f + 2.5f * speed));
            stars++;
            if (bright) brightStars++;
        }
        // The moon: its quad holds the glow too, so it is bigger than the disc.
        AddQuad(directions, colours, corners, twinkles, triangles, towardMoon, Color.white,
            moonRadius / MoonDiscShare * Mathf.Deg2Rad, 1f, Vector2.zero);

        mesh.Clear();
        mesh.indexFormat = directions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(directions);
        mesh.SetColors(colours);
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, twinkles);
        mesh.SetTriangles(triangles, 0);
        // The shader puts the sky round the camera, wherever this object is: never cull it.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
    }

    static void AddQuad(List<Vector3> directions, List<Color> colours, List<Vector4> corners, List<Vector2> twinkles,
        List<int> triangles, Vector3 direction, Color colour, float halfSize, float kind, Vector2 twinkle)
    {
        int first = directions.Count;
        for (int c = 0; c < 4; c++)
        {
            directions.Add(direction);
            colours.Add(colour);
            corners.Add(new Vector4(c == 1 || c == 2 ? 1f : -1f, c >= 2 ? 1f : -1f, halfSize, kind));
            twinkles.Add(twinkle);
        }
        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
    }

    static float Next(System.Random rng) => (float)rng.NextDouble();

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
