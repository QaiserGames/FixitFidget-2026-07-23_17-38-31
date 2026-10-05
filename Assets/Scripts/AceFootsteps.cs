using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: ACE'S STEPS (claude/sound-plan.md §4.5)
//
// Ace is a capsule with no walk animation, so a step sounds every stride walked (the bank's stride,
// about 0.7 m, varied a little), whatever the view. What's underfoot picks the cue:
//
//   - inside the café's room: "ace.step.cafe";
//   - outside, a short ray down: ground whose name, or its parent's, has a road word in it (whole
//     words: "road", "asphalt", "crossing", "lane", "zebra", "driveway", "stall", "aisle"...) is
//     "ace.step.road"; anything else, "ace.step.pavement". The parent counts because the car park's
//     "Stall line" sits under "Car park surface and paint"; at night the solid copies of the ground
//     are named by their whole path, so their words are all there.
//
// The names the ray meets are counted for the sound check, so the road/pavement words can be tuned
// against the real scene (the first night tour, 28 Sept: asphalt, crossing paint and zebra stripes
// as road; sidewalk stone, kerbs and the café's entry apron as pavement). Nothing sounds while the
// game is paused or Ace stands still; a jump of more than 1.5 m in a frame (a reset, a teleport)
// isn't a step. Added to Ace while playing by SoundRig; never saved.
//
// At night each step is also a noise someone could hear (break-ins chunk B, NightNoise): heard within
// 4 m walking, 1 m sneaking. A sneaking step is shorter (the crouch walk's stride) and much quieter.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class AceFootsteps : MonoBehaviour
{
    static readonly HashSet<string> RoadWords = new HashSet<string>
    {
        "road", "roads", "asphalt", "tarmac", "crossing", "crosswalk", "lane", "lanes", "zebra", "driveway", "stall", "aisle",
    };
    static readonly System.Random Rng = new System.Random();

    // A sneaking step: this much of a walking stride, at this much of its volume.
    const float SneakStride = .7f, SneakVolume = .3f;

    CafeViewMode view;
    PlayerMovement movement;
    Vector3 last;
    float walked, stride = .72f, thisStride = .72f;
    readonly Dictionary<string, int> surfaces = new Dictionary<string, int>();
    readonly Dictionary<string, int> names = new Dictionary<string, int>();

    public int Steps { get; private set; }
    /// <summary>Steps per surface: cafe, pavement, road.</summary>
    public IReadOnlyDictionary<string, int> Surfaces => surfaces;
    /// <summary>What the ray met outside, by object name (for tuning the road words).</summary>
    public IReadOnlyDictionary<string, int> Underfoot => names;

    void OnEnable()
    {
        view = GetComponent<CafeViewMode>();
        movement = GetComponent<PlayerMovement>();
        last = transform.position;
        SoundPlayer player = SoundPlayer.Ensure();
        if (player != null && player.Bank != null) stride = player.Bank.stride;
        NextStride();
    }

    bool Sneaking => movement != null && movement.Sneaking;

    void NextStride() => thisStride = stride * (Sneaking ? SneakStride : 1f) * (.94f + (float)Rng.NextDouble() * .12f);

    void Update()
    {
        Vector3 now = transform.position;
        Vector3 moved = now - last;
        moved.y = 0f;
        last = now;
        if (Time.timeScale <= 0f) return;
        float distance = moved.magnitude;
        if (distance > 1.5f) { walked = 0f; return; }
        walked += distance;
        if (walked < thisStride) return;
        walked = Mathf.Min(walked - thisStride, thisStride * .5f);
        bool sneaking = Sneaking;
        NextStride();

        Vector3 feet = view != null ? view.AceFeet : now;
        string surface = Surface(feet);
        Steps++;
        if (sneaking) SneakSteps++;
        surfaces[surface] = (surfaces.TryGetValue(surface, out int n) ? n : 0) + 1;
        Sfx.Play(Cue(surface), feet + Vector3.up * .05f, sneaking ? SneakVolume : 1f);
        // Heard at night: 4 m walking, 1 m sneaking (NightNoise does nothing by day).
        NightNoise.Make(feet, sneaking ? NoiseKind.SneakStep : NoiseKind.Step);
    }

    // The three cues' names, made once (a step used to build its name each time: a little garbage per step).
    static string Cue(string surface) => surface == "cafe" ? "ace.step.cafe" : surface == "road" ? "ace.step.road" : "ace.step.pavement";

    /// <summary>Of the steps, how many were sneaking.</summary>
    public int SneakSteps { get; private set; }

    string Surface(Vector3 feet)
    {
        bool inside = view != null ? view.AceInsideCafe : CafeDaylight.CafeInside.Contains(new Vector2(feet.x, feet.z));
        if (inside) return "cafe";
        if (!Physics.Raycast(feet + Vector3.up * .4f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            return "pavement";
        string name = hit.collider.name;
        if (names.Count < 60 || names.ContainsKey(name)) names[name] = (names.TryGetValue(name, out int n) ? n : 0) + 1;
        Transform parent = hit.collider.transform.parent;
        return IsRoad(name) || parent != null && IsRoad(parent.name) ? "road" : "pavement";
    }

    // Whole words only ("Plane" isn't a lane): a name splits at anything that isn't a letter and where a
    // small letter meets a capital ("RoadTile" is "road tile").
    static bool IsRoad(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var word = new System.Text.StringBuilder(16);
        for (int i = 0; i <= name.Length; i++)
        {
            char c = i < name.Length ? name[i] : ' ';
            bool letter = char.IsLetter(c);
            bool camel = letter && i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]);
            if (!letter || camel)
            {
                if (word.Length > 0 && RoadWords.Contains(word.ToString())) return true;
                word.Clear();
                if (!letter) continue;
            }
            word.Append(char.ToLowerInvariant(c));
        }
        return false;
    }

    public string Describe()
    {
        var text = new System.Text.StringBuilder();
        text.Append($"Ace's steps: {Steps}");
        foreach (KeyValuePair<string, int> kv in surfaces) text.Append($", {kv.Key} {kv.Value}");
        text.Append('.');
        if (names.Count > 0)
        {
            text.Append(" Underfoot outside:");
            foreach (KeyValuePair<string, int> kv in names) text.Append($" '{kv.Key}' ×{kv.Value};");
        }
        return text.ToString();
    }
}
