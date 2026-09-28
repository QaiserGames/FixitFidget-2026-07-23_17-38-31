using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// NIGHT WALK (night step 4; see claude/night-city-proposal.md in the project)
//
// The café's street at night, with nothing to do yet: Ace walks out into the
// lit neighbourhood and back. This component holds the night and switches it
// on and off. It is built in parts:
//
//   Part 1, the night's lighting (this file so far):
//     * the hour: CafeDaylight is held at a night hour, with a moon, and the
//       café's own lights inside the room are dimmed (closed for the night);
//     * street lamps: lights set up by Fixit Fidget > Night > Night walk 1,
//       switched off in the scene and on only here;
//     * windows: POLYGON buildings get a copy of their material with one of the
//       pack's five window masks (Emissive_01-05) as its glow, chosen per
//       building and the same every night, so some windows are lit and some dark;
//     * signs glow with their own colours; the one late spot's window glows warm
//       and throws light on the pavement;
//     * a night look (more bloom, a cooler balance) on its own Volume, weight 0
//       by day;
//     * the street is quiet: the café sends nobody, the day's clock stops, and
//       the day's walkers and traffic go home (part 4 brings a few night owls).
//
//   Part 2, the night's edges and collision (in progress):
//     * solid by night: by day the streets have no collision at all (only the
//       café's people walk there, on their own routes), so while the night runs
//       every fixed mesh Ace could touch gets exact collision: solid exactly where
//       it looks solid, no invisible walls, nothing to walk through;
//     * the patio's invisible day fence (it keeps Ace in the café by day) is off.
//
// Nothing here runs by day. Only Begin() switches anything on, and End() (or
// leaving Play Mode) puts it all back. Material copies are made at run time and
// never saved: the scene keeps its originals. For now a night walk is only ever
// started from the editor, in a café lab session (Fixit Fidget > Night > Play
// the night walk (lab)), so the real playtest save is never used.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightWalk : MonoBehaviour
{
    public static NightWalk Instance { get; private set; }

    /// <summary>Set by the editor before a lab Play session that should start at night.</summary>
    public const string PendingKey = "FixitFidget.NightWalk.Pending";

    [Header("The hour")]
    [Tooltip("The café clock's hour the night is held at (0-24).")]
    [Range(0f, 24f)] public float nightHour = 23f;
    [Range(0f, 1f)] public float moon = 1f;
    [Tooltip("The café's own lights inside the room, closed for the night (1 = as by day).")]
    [Range(0f, 1f)] public float cafeInsideLights = .15f;

    [Header("Street lamps and other night-only things (inactive in the scene)")]
    [Tooltip("Objects that exist only at night: the street lamps' lights and bulbs, the late spot's light.")]
    public GameObject[] nightOnly = Array.Empty<GameObject>();
    [Tooltip("The street lamps' lights (for the report; they live under Night Only objects).")]
    public Light[] lampLights = Array.Empty<Light>();

    [Header("Windows")]
    public Renderer[] buildingRenderers = Array.Empty<Renderer>();
    [Tooltip("Per building renderer: which window mask (0-4), or -1 for a building that is dark tonight.")]
    public int[] windowPatterns = Array.Empty<int>();
    [Tooltip("POLYGON City's Emissive_01-05.")]
    public Texture[] windowMasks = Array.Empty<Texture>();
    [ColorUsage(false, true)] public Color windowGlow = new Color(1.2f, .84f, .46f);

    [Header("Signs (they glow with their own colours)")]
    public Renderer[] signRenderers = Array.Empty<Renderer>();
    [Range(0f, 3f)] public float signGlow = .55f;

    [Header("The late spot")]
    public Renderer[] lateSpotGlass = Array.Empty<Renderer>();
    public Light[] lateSpotLights = Array.Empty<Light>();
    [ColorUsage(false, true)] public Color lateSpotGlow = new Color(1.5f, 1.05f, .6f);

    [Header("Night look")]
    public Volume nightLook;

    [Header("Solid by night (part 2)")]
    [Tooltip("While the night runs, every fixed mesh Ace could touch gets exact collision, so the neighbourhood is solid exactly " +
             "where it looks solid. By day the streets have none (only the café's people walk there, on their own routes), and " +
             "nothing here changes that. The list is written by the set-up (Fixit Fidget > Night > Night walk 1).")]
    public bool solidNight = true;
    public NightCollision nightCollision;
    [Tooltip("Colliders that only keep Ace inside the café by day (the patio's invisible fence). Off while the night runs.")]
    public Collider[] dayOnlyColliders = Array.Empty<Collider>();

    public bool Active { get; private set; }
    public int LitBuildings { get; private set; }
    public int GlowingSigns { get; private set; }
    public int QuietedActors { get; private set; }
    public int SolidMeshes { get; private set; }
    public long SolidTriangles { get; private set; }
    public float SolidSeconds { get; private set; }

    // Synty's POLYGON shaders (Generic_Basic / Generic_Standard) name their glow like this.
    static readonly int SyntyEmissionMap = Shader.PropertyToID("_Emission_Map");
    static readonly int SyntyEmissionColor = Shader.PropertyToID("_Emission_Color");
    static readonly int SyntyAlbedo = Shader.PropertyToID("_Albedo_Map");
    static readonly int SyntyEnableEmission = Shader.PropertyToID("_Enable_Emission");
    // URP Lit, for anything that isn't POLYGON.
    static readonly int UrpEmissionMap = Shader.PropertyToID("_EmissionMap");
    static readonly int UrpEmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int UrpBaseMap = Shader.PropertyToID("_BaseMap");

    readonly List<(Renderer renderer, Material[] materials)> swapped = new();
    readonly Dictionary<(Material, int), Material> copies = new();
    readonly List<Behaviour> paused = new();
    readonly List<GameObject> hiddenActors = new();
    GameObject solidRoot;
    readonly List<Collider> dayOnlyOff = new();
    List<StreetLife.Actor> streetActors;
    StreetLife street;
    CafeDaylight daylight;

    void Awake() => Instance = this;

    // Leaving Play Mode tears the scene down: nothing to put back then (objects being destroyed can't be
    // switched on again), only the run-time material copies to free.
    void OnDestroy()
    {
        if (solidRoot != null) Destroy(solidRoot);
        foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
        copies.Clear();
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    // A lab Play session asked for the night: start it once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) != 1) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night walk] A night walk was asked for outside a lab session; it only runs in the lab for now.");
            return;
        }
        var walk = Instance != null ? Instance : FindAnyObjectByType<NightWalk>();
        if (walk == null)
        {
            Debug.LogWarning("[Night walk] No NightWalk in this scene: run Fixit Fidget > Night > Night walk 1 first.");
            return;
        }
        walk.QuietTheStreet();
        walk.Begin();
    }
#endif

    /// <summary>Switch the night on. Safe to call twice.</summary>
    public void Begin()
    {
        if (Active) return;
        Active = true;
        daylight = FindAnyObjectByType<CafeDaylight>();
        if (daylight != null) daylight.SetNight(nightHour, moon, cafeInsideLights);
        foreach (var go in nightOnly) if (go != null) go.SetActive(true);
        LightTheWindows();
        LightTheSigns();
        LightTheLateSpot();
        if (nightLook != null) nightLook.weight = 1f;
        MakeTheNightSolid();
        foreach (var c in dayOnlyColliders)
            if (c != null && c.enabled) { c.enabled = false; dayOnlyOff.Add(c); }
        Debug.Log($"[Night walk] Night at {nightHour:0.0}h: {Count(lampLights)} street lamps, {LitBuildings} building parts with lit windows, " +
                  $"{GlowingSigns} signs glowing, the late spot {(lateSpotGlass.Length > 0 ? "lit" : "not set")}; " +
                  $"{QuietedActors} of the day's walkers and cars sent home; {SolidMeshes} meshes made solid ({SolidTriangles:N0} triangles, " +
                  $"{SolidSeconds:0.00} s); {dayOnlyOff.Count} day-only colliders off.", this);
    }

    /// <summary>Put the day back exactly as it was.</summary>
    public void End()
    {
        if (!Active) return;
        Active = false;
        if (daylight != null) daylight.SetNight(null, 0f, 1f);
        foreach (var go in nightOnly) if (go != null) go.SetActive(false);
        // Newest first: a renderer swapped twice (windows, then the late spot's glass) gets its own materials back last.
        for (int i = swapped.Count - 1; i >= 0; i--)
            if (swapped[i].renderer != null) swapped[i].renderer.sharedMaterials = swapped[i].materials;
        swapped.Clear();
        foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
        copies.Clear();
        if (nightLook != null) nightLook.weight = 0f;
        if (solidRoot != null) Destroy(solidRoot);
        solidRoot = null;
        SolidMeshes = 0;
        SolidTriangles = 0;
        foreach (var c in dayOnlyOff) if (c != null) c.enabled = true;
        dayOnlyOff.Clear();
        foreach (var b in paused) if (b != null) b.enabled = true;
        paused.Clear();
        foreach (var go in hiddenActors) if (go != null) go.SetActive(true);
        hiddenActors.Clear();
        if (street != null && streetActors != null)
        {
            street.actors = streetActors;
            street.RebuildRoutes();
        }
        streetActors = null;
        QuietedActors = LitBuildings = GlowingSigns = 0;
    }

    /// <summary>
    /// The café is closed and the street has gone to bed: the café sends nobody, the day's clock
    /// stops, and StreetLife's walkers and cars leave (part 4 brings a few night owls back).
    /// CafeArrivals (the café's visitors arriving by car or on foot) rests with the spawners: with
    /// the street's routes gone it would find no lanes, warn, and send its cars away for the rest
    /// of the session. Paused, it finds the lanes again when End() brings the street back.
    /// </summary>
    public void QuietTheStreet()
    {
        foreach (Behaviour b in new Behaviour[]
                 {
                     FindAnyObjectByType<CustomerSpawner>(), FindAnyObjectByType<PatronSpawner>(), FindAnyObjectByType<CafeArrivals>(),
                     FindAnyObjectByType<DayClock>(),
                 })
            if (b != null && b.enabled) { b.enabled = false; paused.Add(b); }
        street = StreetLife.Main != null ? StreetLife.Main : FindAnyObjectByType<StreetLife>();
        if (street == null) return;
        streetActors = street.actors;
        var kept = new List<StreetLife.Actor>();
        foreach (var actor in streetActors)
        {
            if (actor == null) continue;
            if (actor.actor != null && actor.actor.gameObject.activeSelf)
            {
                actor.actor.gameObject.SetActive(false);
                hiddenActors.Add(actor.actor.gameObject);
            }
            QuietedActors++;
        }
        street.actors = kept;
        street.RebuildRoutes();
    }

    // ---------- solid by night ----------

    /// <summary>
    /// Exact collision, for the night only, on every fixed mesh Ace could touch (the set-up's list):
    /// the neighbourhood is solid exactly where it looks solid, no more (no invisible walls) and no
    /// less (nothing to walk through, no ground to fall through). POLYGON's own rough convex collision
    /// stays off. Plain objects in the Play scene, so they go with it; End() removes them sooner.
    /// </summary>
    void MakeTheNightSolid()
    {
        SolidMeshes = 0;
        SolidTriangles = 0;
        SolidSeconds = 0f;
        if (!solidNight || nightCollision == null) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // At the scene's root, so each piece's scale is its world scale.
        solidRoot = new GameObject("Solid by night (while the night runs)");
        foreach (var piece in nightCollision.pieces)
        {
            if (piece.mesh == null) continue;
            var go = new GameObject(piece.from);
            go.transform.SetParent(solidRoot.transform, false);
            go.transform.SetPositionAndRotation(piece.position, piece.rotation);
            go.transform.localScale = piece.scale;
            go.AddComponent<MeshCollider>().sharedMesh = piece.mesh;
            SolidMeshes++;
            for (int s = 0; s < piece.mesh.subMeshCount; s++) SolidTriangles += piece.mesh.GetSubMesh(s).indexCount / 3;
        }
        SolidSeconds = (float)watch.Elapsed.TotalSeconds;
    }

    // ---------- windows, signs, the late spot ----------

    void LightTheWindows()
    {
        LitBuildings = 0;
        if (windowMasks.Length == 0) return;
        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            Renderer r = buildingRenderers[i];
            int pattern = i < windowPatterns.Length ? windowPatterns[i] : -1;
            if (r == null || pattern < 0 || pattern >= windowMasks.Length || windowMasks[pattern] == null) continue;
            // The masks are drawn for POLYGON City's own atlas; other materials on a building keep theirs.
            if (Swap(r, m => IsCityAtlas(m) ? GlowCopy(m, pattern, windowMasks[pattern], windowGlow, false) : null)) LitBuildings++;
        }
    }

    void LightTheSigns()
    {
        GlowingSigns = 0;
        Color glow = Color.white * signGlow;
        foreach (var r in signRenderers)
            if (r != null && Swap(r, m => GlowCopy(m, 100, null, glow, true))) GlowingSigns++;
    }

    void LightTheLateSpot()
    {
        foreach (var r in lateSpotGlass)
            if (r != null) Swap(r, m => IsGlass(m) ? GlowCopy(m, 200, Texture2D.whiteTexture, lateSpotGlow, false) : null);
    }

    static bool IsGlass(Material m) => m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;

    static bool IsCityAtlas(Material m)
    {
        if (m == null || !m.HasProperty(SyntyAlbedo)) return false;
        Texture albedo = m.GetTexture(SyntyAlbedo);
        return albedo != null && albedo.name.StartsWith("PolygonCity_", StringComparison.Ordinal);
    }

    /// <summary>Replace the renderer's materials with their night copies (null: keep that one). True if any changed.</summary>
    bool Swap(Renderer r, Func<Material, Material> night)
    {
        Material[] originals = r.sharedMaterials;
        Material[] replaced = (Material[])originals.Clone();
        bool changed = false;
        for (int i = 0; i < replaced.Length; i++)
        {
            if (replaced[i] == null) continue;
            Material copy = night(replaced[i]);
            if (copy == null) continue;
            replaced[i] = copy;
            changed = true;
        }
        if (!changed) return false;
        swapped.Add((r, originals));
        r.sharedMaterials = replaced;
        return true;
    }

    /// <summary>
    /// A run-time copy of a material that glows: with the mask given, or (ownColours) with its own
    /// albedo, so a sign lights up in its own colours. One copy per material and key. Null when the
    /// shader has no glow to set.
    /// </summary>
    Material GlowCopy(Material source, int key, Texture mask, Color glow, bool ownColours)
    {
        if (copies.TryGetValue((source, key), out var existing)) return existing;
        Material copy = null;
        if (source.HasProperty(SyntyEmissionMap) && source.HasProperty(SyntyEmissionColor))
        {
            Texture map = ownColours ? (source.HasProperty(SyntyAlbedo) ? source.GetTexture(SyntyAlbedo) : null) : mask;
            if (map != null)
            {
                copy = new Material(source) { name = source.name + " (night)", hideFlags = HideFlags.DontSave };
                copy.SetTexture(SyntyEmissionMap, map);
                copy.SetColor(SyntyEmissionColor, glow);
                // POLYGON materials ship with their glow switched off (Enable Emission 0).
                if (copy.HasProperty(SyntyEnableEmission)) copy.SetFloat(SyntyEnableEmission, 1f);
            }
        }
        else if (source.HasProperty(UrpEmissionMap) && source.HasProperty(UrpEmissionColor))
        {
            Texture map = ownColours ? (source.HasProperty(UrpBaseMap) ? source.GetTexture(UrpBaseMap) : null) : mask;
            copy = new Material(source) { name = source.name + " (night)", hideFlags = HideFlags.DontSave };
            copy.EnableKeyword("_EMISSION");
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (map != null) copy.SetTexture(UrpEmissionMap, map);
            copy.SetColor(UrpEmissionColor, glow);
        }
        copies[(source, key)] = copy;
        return copy;
    }

    /// <summary>What is switched on right now, for the night photos' notes.</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        int active = 0;
        foreach (var go in nightOnly) if (go != null && go.activeInHierarchy) active++;
        sb.AppendLine($"Night walk {(Active ? "on" : "off")} at {nightHour:0.0}h, moon {moon:0.00}, café lights inside x{cafeInsideLights:0.00}.");
        sb.AppendLine($"Night-only objects active: {active} of {nightOnly.Length}; street lamp lights {Count(lampLights)}.");
        sb.AppendLine($"Building parts lit: {LitBuildings} of {buildingRenderers.Length}; signs glowing: {GlowingSigns} of {signRenderers.Length}; " +
                      $"material copies: {copies.Count}; renderers swapped: {swapped.Count}; street actors sent home: {QuietedActors}.");
        sb.AppendLine($"Solid by night: {SolidMeshes} meshes with exact collision ({SolidTriangles:N0} triangles, made in {SolidSeconds:0.00} s); " +
                      $"day-only colliders off: {dayOnlyOff.Count} of {dayOnlyColliders.Length}.");
        foreach (var kv in copies)
        {
            var m = kv.Value;
            if (m == null) { sb.AppendLine($"  {kv.Key.Item1.name} [{kv.Key.Item2}]: no glow (shader has none)"); continue; }
            string enable = m.HasProperty(SyntyEnableEmission) ? $", Enable Emission {m.GetFloat(SyntyEnableEmission):0}" : "";
            string map = m.HasProperty(SyntyEmissionMap) && m.GetTexture(SyntyEmissionMap) != null ? m.GetTexture(SyntyEmissionMap).name
                       : m.HasProperty(UrpEmissionMap) && m.GetTexture(UrpEmissionMap) != null ? m.GetTexture(UrpEmissionMap).name : "-";
            sb.AppendLine($"  {m.name} [{kv.Key.Item2}]: shader {m.shader.name}, glow map {map}{enable}");
        }
        foreach (var l in lateSpotLights)
            if (l != null) sb.AppendLine($"  late spot light at ({l.transform.position.x:0.0}, {l.transform.position.y:0.0}, {l.transform.position.z:0.0}), {(l.gameObject.activeInHierarchy ? "on" : "off")}");
        return sb.ToString();
    }

    static int Count(Light[] lights)
    {
        int n = 0;
        foreach (var l in lights) if (l != null) n++;
        return n;
    }
}
