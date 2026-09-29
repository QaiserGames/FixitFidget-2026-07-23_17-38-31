using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: THE CAFÉ'S SOUNDSCAPE (claude/sound-plan.md §4.4)
//
// The beds, flat loops that cross-fade (1.5 s) as things change:
//   - by day: the café's room (full inside, faint outside); the murmur, which grows with the people in
//     the café's room (none with one person, full with about thirteen); the street (full outside,
//     quieter and muffled inside);
//   - at night: the city, busy at 11 pm and cross-fading to its late bed by 2 am (muffled inside);
//     the closed café, inside only.
// Now and then at night, a far-off one-off ("night.far"): every 20-60 s of the night, half as often
// after 2 am, panned somewhere left or right, quieter inside.
// The café's door: anyone crossing into or out of the café's room rings the door bell where they
// are; a group rings once (the cue's gap). At night Ace's own comings and goings ring it softer.
//
// Everything here is silent until the bank has files for it. Made while playing by SoundRig; never
// saved in the scene.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class CafeSoundscape : MonoBehaviour
{
    sealed class Bed
    {
        public readonly string cue;
        public SfxLoop loop;
        public float retryAt;
        public Bed(string cue) { this.cue = cue; }
    }

    const float Open = 22000f;
    static readonly System.Random Rng = new System.Random();

    readonly Bed room = new Bed("cafe.room"), murmur = new Bed("cafe.murmur"), street = new Bed("street.day"),
                 cityBusy = new Bed("night.city"), cityLate = new Bed("night.city.late"), cafeNight = new Bed("cafe.night");
    readonly Dictionary<Transform, bool> wasInside = new Dictionary<Transform, bool>();
    readonly List<Transform> gone = new List<Transform>();
    CafeViewMode view;
    float nextCount, nextDoorCheck, nextFar = -1f, nextTidy;

    /// <summary>
    /// The one soundscape while playing (SoundRig makes it). Its object belongs to the Play session and
    /// ends with it (PlaySessionLeftovers); this is how the game finds it.
    /// </summary>
    public static CafeSoundscape Instance { get; private set; }

    public bool Night { get; private set; }
    /// <summary>Customers and patrons inside the café's room (counted once a second).</summary>
    public int People { get; private set; }
    /// <summary>Times someone crossed the café's doorway (each asks for the bell; a group rings once).</summary>
    public int Crossings { get; private set; }
    public int FarOffs { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start() => view = FindAnyObjectByType<CafeViewMode>();

    void OnDisable()
    {
        foreach (Bed bed in Beds()) if (bed.loop != null) { bed.loop.Stop(.3f); bed.loop = null; }
    }

    IEnumerable<Bed> Beds()
    {
        yield return room; yield return murmur; yield return street;
        yield return cityBusy; yield return cityLate; yield return cafeNight;
    }

    void Update()
    {
        if (view == null) view = FindAnyObjectByType<CafeViewMode>();
        NightWalk night = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance : null;
        Night = night != null;
        bool inside = view != null && view.AceInsideCafe;

        if (Time.unscaledTime >= nextCount)
        {
            nextCount = Time.unscaledTime + 1f;
            People = CountPeople();
        }

        float busy = Night ? 0f : Mathf.Clamp01((People - 1) / 12f);
        float late = Night ? Mathf.InverseLerp(24f, 26f, night.Hour) : 0f;
        Keep(room, !Night, inside ? 1f : .25f, Open);
        Keep(murmur, !Night, busy * (inside ? 1f : .3f), Open);
        Keep(street, !Night, inside ? .45f : 1f, inside ? 1100f : Open);
        Keep(cityBusy, Night, (1f - late) * (inside ? .45f : 1f), inside ? 900f : Open);
        Keep(cityLate, Night, late * (inside ? .45f : 1f), inside ? 900f : Open);
        Keep(cafeNight, Night, inside ? 1f : 0f, Open);

        if (Night) FarOff(inside, late);
        else nextFar = -1f;

        if (Time.unscaledTime >= nextDoorCheck)
        {
            nextDoorCheck = Time.unscaledTime + .2f;
            Doors();
        }
    }

    // A bed that should play fades to its level; one that shouldn't fades out. A bed without a file
    // is asked for again every 5 s (so a report shows it being wanted), never every frame.
    void Keep(Bed bed, bool want, float level, float cutoff)
    {
        if (bed.loop != null && !bed.loop.Alive) bed.loop = null;
        if (!want)
        {
            if (bed.loop != null) { bed.loop.Stop(1.5f); bed.loop = null; }
            return;
        }
        if (bed.loop == null)
        {
            if (Time.unscaledTime < bed.retryAt) return;
            bed.loop = Sfx.Loop2D(bed.cue, 0f);
            if (bed.loop == null) { bed.retryAt = Time.unscaledTime + 5f; return; }
            bed.loop.FadeSeconds = 1.5f;
        }
        bed.loop.Volume = level;
        bed.loop.Muffle(cutoff);
    }

    void FarOff(bool inside, float late)
    {
        if (nextFar < 0f) { nextFar = Time.time + 12f + (float)Rng.NextDouble() * 10f; return; }
        if (Time.time < nextFar) return;
        nextFar = Time.time + (20f + (float)Rng.NextDouble() * 40f) * (late > .5f ? 1.8f : 1f);
        FarOffs++;
        Sfx.Play2D("night.far", inside ? .35f : 1f, ((float)Rng.NextDouble() * 2f - 1f) * .7f);
    }

    int CountPeople()
    {
        Rect cafe = CafeDaylight.CafeInside;
        int n = 0;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>()) if (Inside(c.transform, cafe)) n++;
        foreach (PatronBrain p in FindObjectsByType<PatronBrain>()) if (Inside(p.transform, cafe)) n++;
        return n;
    }

    static bool Inside(Transform who, Rect cafe) => cafe.Contains(new Vector2(who.position.x, who.position.z));

    void Doors()
    {
        Rect cafe = CafeDaylight.CafeInside;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>()) Cross(c.transform, cafe, 1f);
        foreach (PatronBrain p in FindObjectsByType<PatronBrain>()) Cross(p.transform, cafe, 1f);
        if (view != null) Cross(view.transform, cafe, Night ? .6f : 1f);

        // Forget people who have left the scene.
        if (Time.unscaledTime < nextTidy) return;
        nextTidy = Time.unscaledTime + 5f;
        gone.Clear();
        foreach (Transform who in wasInside.Keys) if (who == null) gone.Add(who);
        foreach (Transform who in gone) wasInside.Remove(who);
    }

    void Cross(Transform who, Rect cafe, float volume)
    {
        bool inside = Inside(who, cafe);
        if (wasInside.TryGetValue(who, out bool before) && before != inside)
        {
            Crossings++;
            Sfx.Play("door.bell", who.position + Vector3.up * 1.2f, volume);
        }
        wasInside[who] = inside;
    }

    public string Describe()
    {
        var text = new StringBuilder();
        text.Append($"Soundscape: {(Night ? "night" : "day")}, {People} people in the café's room, {Crossings} doorway crossings, {FarOffs} far-off sounds asked for. Beds:");
        foreach (Bed bed in Beds())
            text.Append($" {bed.cue} {(bed.loop != null && bed.loop.Alive ? $"playing at {bed.loop.Volume:0.00}" : "silent")};");
        return text.ToString();
    }
}
