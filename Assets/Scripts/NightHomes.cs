using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: THE HOUSES ROUND THE CAFÉ ARE LIVED IN (claude/night-city-proposal.md §9)
//
// Before part 4 every house round the café glowed at every window from 11 PM on (the day's
// evening light makes each house's merged "Curtain glow" glow all at once). Now the night lights
// them room by room, from the rooms list (NightRooms, made in Edit Mode):
//
//   * a lit room is a soft warm pane just in front of its window's glass, with its curtains
//     glowing; a dark room shows its own glass and its curtains unlit;
//   * when the night begins some rooms are lit and some are dark, and the lit ones go out one by
//     one: downstairs first (by about a quarter to one), upstairs later (all out by 2 AM). Now and
//     then a light comes on upstairs as the one downstairs goes out: someone going up to bed;
//   * a couple of rooms flicker blue, like a TV; later in the night a few rooms light for a few
//     minutes: someone up for a glass of water; and somewhere one light stays on nearly all night;
//   * shop fronts stay dark (closed);
//   * Grace's house has one warm room until about midnight, and nothing marks it;
//     her ground floor has real rooms behind clear glass (the break-ins, GraceHouse), so her
//     front window gets no pane of its own: her lamps light it;
//   * houses whose neighbour is still out (NightNeighbours) are dark until they come home, then a
//     room lights, and later one upstairs.
//
// Which room does what is worked out from the house's name and the room's number, so it is the
// same every night. Everything here is made at run time and removed when the night ends: the
// rooms are switched on and off (never given other materials), so a house that has turned
// see-through (NightSeeThrough) keeps whatever it wore when it began to fade.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightHomes : MonoBehaviour
{
    sealed class Room
    {
        public NightRooms.Room data;
        public Transform house;
        public string houseName = "";
        public MeshRenderer panes, curtainsLit, curtainsDark;
        public GameObject root;
        public readonly List<Vector2> on = new();    // (from, to): clock hours it is lit (23-28)
        public bool lit, tv, wake, grace, shop, homecoming, owl;
        public Material tvPane;
        public float tvLevel = 1f, tvTarget = 1f, tvNext;
        public Color tvColour;
    }

    readonly List<Room> rooms = new();

    /// <summary>
    /// Houses whose ground floor has real rooms behind a clear window (Grace's: GraceHouse registers it). Their
    /// ground-floor windows get no lit pane or curtains of their own; the house's own lamps light them.
    /// </summary>
    public static readonly HashSet<Transform> RealGroundFloors = new();
    readonly List<Renderer> hiddenCurtains = new();
    readonly List<UnityEngine.Object> made = new();          // materials and textures to free
    readonly Dictionary<Material, Material> darkCurtains = new();
    readonly HashSet<Transform> away = new();
    Material paneLit;
    Transform graceHouse;
    bool settled;
    NightWalk.HomeHours hours;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    public int Rooms => rooms.Count;
    public int Houses { get; private set; }
    public int LitNow { get; private set; }
    public int LitAtStart { get; private set; }
    public int MostLit { get; private set; }
    /// <summary>Rooms switched on or off since the night began (for reports).</summary>
    public int Changes { get; private set; }
    public string GraceHouse => graceHouse != null ? graceHouse.name : "";
    public string Problems { get; private set; } = "";

    /// <summary>
    /// Make every room's pane and curtains (all switched on, so the see-through can find them), hide the
    /// houses' merged curtains, and plan the night. <paramref name="awayHouses"/>: houses (by name) whose
    /// neighbour is still out; dark until <see cref="ComeHome"/>. Call <see cref="Settle"/> once the
    /// see-through is built.
    /// </summary>
    public void Build(NightRooms data, NightWalk.HomeHours settings, float startHour, IEnumerable<string> awayHouses)
    {
        Clear();
        hours = settings ?? new NightWalk.HomeHours();
        var problems = new StringBuilder();
        if (data == null || data.rooms == null || data.rooms.Length == 0)
        {
            Problems = "no rooms list (Fixit Fidget > Night > Night walk 4a - Build the lived-in windows)";
            return;
        }
        var awayNames = new HashSet<string>(awayHouses ?? Array.Empty<string>(), StringComparer.Ordinal);
        var houseByPath = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var houseCount = new HashSet<Transform>();
        Texture2D glow = Gradient();
        made.Add(glow);

        foreach (NightRooms.Room r in data.rooms)
        {
            if (r == null || r.panes == null) continue;
            if (!houseByPath.TryGetValue(r.housePath ?? "", out Transform house))
                houseByPath[r.housePath ?? ""] = house = FindByPath(r.housePath);
            if (house == null || !house.gameObject.activeInHierarchy) continue;
            houseCount.Add(house);
            if (r.floor == 0 && RealGroundFloors.Contains(house)) continue;
            if (awayNames.Contains(house.name)) away.Add(house);
            if (paneLit == null) paneLit = PaneMaterial(house, glow);
            if (paneLit == null) { problems.Append("no window glass material to copy; "); break; }

            var room = new Room { data = r, house = house, houseName = house.name, shop = r.shopFront };
            room.root = new GameObject($"Night room {r.index} (while the night runs)");
            room.root.transform.SetParent(house, false);   // the meshes are in the house's space
            room.panes = Part(room.root.transform, "Lit pane", r.panes, paneLit);
            Renderer merged = CurtainOf(house);
            if (r.curtains != null && merged != null && merged.sharedMaterial != null)
            {
                // Lit, the curtains glow toward the street only: their faces looking into the room wear the dark
                // curtain, so a room Ace is in (Grace's) isn't lit orange by its own window (5 Oct).
                Material dark = DarkCurtain(merged.sharedMaterial);
                Mesh facing = FacingOut(r.curtains, house, r.centre);
                room.curtainsLit = Part(room.root.transform, "Curtains, lit", facing, merged.sharedMaterial);
                room.curtainsLit.sharedMaterials = new[] { merged.sharedMaterial, dark };
                room.curtainsDark = Part(room.root.transform, "Curtains, dark", r.curtains, dark);
            }
            rooms.Add(room);
        }
        Houses = houseCount.Count;

        // The houses' merged curtains glow at every window: the rooms' own replace them for the night.
        foreach (string path in data.curtainPaths ?? Array.Empty<string>())
        {
            Transform t = FindByPath(path);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null && r.enabled) { r.enabled = false; hiddenCurtains.Add(r); }
        }

        HomeDoor graceDoor = HomeDoor.Find(hours.graceHomeId);
        if (graceDoor != null)
            foreach (Transform h in houseCount)
                if (graceDoor.transform.IsChildOf(h)) { graceHouse = h; break; }
        if (graceDoor == null) problems.Append($"no HomeDoor '{hours.graceHomeId}' (Grace's house stays dark); ");
        else if (graceHouse == null) problems.Append($"Grace's door '{graceDoor.name}' is not in any house with rooms; ");

        Plan(startHour);
        Problems = problems.ToString().TrimEnd(' ', ';');
    }

    /// <summary>Show every room as it should be at <paramref name="hour"/>, from scratch (after the see-through is built, or a jump in time).</summary>
    public void Settle(float hour)
    {
        foreach (Room room in rooms) Show(room, Wants(room, hour));
        Count();
        if (!settled) LitAtStart = LitNow;
        settled = true;
    }

    /// <summary>The night moves on: rooms go dark or light as planned; TVs flicker.</summary>
    public void Tick(float hour)
    {
        bool changed = false;
        foreach (Room room in rooms)
        {
            bool want = Wants(room, hour);
            if (want != room.lit) { Show(room, want); Changes++; changed = true; }
            if (room.tv && room.lit) Flicker(room);
        }
        if (changed) Count();
    }

    /// <summary>
    /// A neighbour is home (NightNeighbours): the room nearest their door on the lowest floor that isn't a
    /// shop lights at <paramref name="at"/> for <paramref name="downstairsFor"/> hours, then one upstairs for
    /// <paramref name="upstairsFor"/>. Returns which rooms, for the report.
    /// </summary>
    public string ComeHome(string houseName, Vector3 door, float at, float downstairsFor, float upstairsFor)
    {
        var mine = rooms.FindAll(r => r.houseName == houseName && !r.shop);
        if (mine.Count == 0) return "no rooms to light";
        int low = int.MaxValue, high = int.MinValue;
        foreach (Room r in mine) { low = Mathf.Min(low, r.data.floor); high = Mathf.Max(high, r.data.floor); }
        Room first = null;
        float best = float.MaxValue;
        foreach (Room r in mine)
        {
            if (r.data.floor != low) continue;
            Vector3 d = r.data.centre - door;
            d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; first = r; }
        }
        if (first == null) return "no rooms to light";
        first.on.Add(new Vector2(at, at + Mathf.Max(.05f, downstairsFor)));
        first.homecoming = true;
        string said = $"room {first.data.index} (floor {first.data.floor}) from {Clock(at)} to {Clock(at + downstairsFor)}";
        if (high > low && upstairsFor > 0f)
        {
            var upstairs = mine.FindAll(r => r.data.floor == high);
            Room second = upstairs[(int)(Hash01(houseName, first.data.index, "bedroom") * upstairs.Count) % upstairs.Count];
            float from = at + downstairsFor - .04f;
            second.on.Add(new Vector2(from, from + upstairsFor));
            second.homecoming = true;
            said += $", then room {second.data.index} (floor {second.data.floor}) until {Clock(from + upstairsFor)}";
        }
        away.RemoveWhere(h => h != null && h.name == houseName);
        return said;
    }

    /// <summary>Everything back as it was: the rooms go, the houses' own curtains come back.</summary>
    public void Clear()
    {
        foreach (Room room in rooms) if (room.root != null) Destroy(room.root);
        rooms.Clear();
        foreach (Renderer r in hiddenCurtains) if (r != null) r.enabled = true;
        hiddenCurtains.Clear();
        foreach (UnityEngine.Object o in made) if (o != null) Destroy(o);
        made.Clear();
        darkCurtains.Clear();
        away.Clear();
        paneLit = null;
        graceHouse = null;
        settled = false;
        LitNow = LitAtStart = MostLit = Changes = Houses = 0;
    }

    void OnDestroy() => Clear();

    // ---------- the plan ----------

    void Plan(float start)
    {
        float before = start - 1f;
        var byHouse = new Dictionary<Transform, List<Room>>();
        foreach (Room r in rooms)
        {
            if (!byHouse.TryGetValue(r.house, out var list)) byHouse[r.house] = list = new List<Room>();
            list.Add(r);
        }
        foreach (var pair in byHouse)
        {
            Transform house = pair.Key;
            List<Room> list = pair.Value;
            if (house == graceHouse)
            {
                // One warm room, until about midnight. Nothing marks it.
                foreach (Room r in list)
                {
                    r.grace = true;
                    if (r.data.index == hours.graceRoom && !r.shop) r.on.Add(new Vector2(before, hours.graceBedtime));
                }
                continue;
            }
            if (away.Contains(house)) continue;   // nobody home yet
            var litDownstairs = new List<Room>();
            foreach (Room r in list)
            {
                if (r.shop) continue;
                if (Hash01(r.houseName, r.data.index, "lit") >= hours.litAtStart) continue;
                float u = Hash01(r.houseName, r.data.index, "bedtime");
                float bedtime = r.data.floor == 0
                    ? Mathf.Lerp(start + .15f, Mathf.Max(start + .2f, hours.downstairsOutBy), u)
                    : Mathf.Lerp(start + .4f, Mathf.Max(start + .5f, hours.lastLightsOut), u);
                r.on.Add(new Vector2(before, bedtime));
                if (r.data.floor == 0) litDownstairs.Add(r);
            }
            // Going up to bed: as a downstairs light goes out, one upstairs comes on for a while.
            foreach (Room down in litDownstairs)
            {
                if (Hash01(down.houseName, down.data.index, "upstairs") >= .6f) continue;
                var dark = list.FindAll(r => !r.shop && r.data.floor > 0 && r.on.Count == 0);
                if (dark.Count == 0) break;
                Room up = dark[(int)(Hash01(down.houseName, down.data.index, "which") * dark.Count) % dark.Count];
                float from = down.on[0].y - .03f;
                float to = from + Mathf.Lerp(.25f, .75f, Hash01(down.houseName, down.data.index, "reads"));
                up.on.Add(new Vector2(from, Mathf.Min(to, hours.lastLightsOut + .5f)));
            }
        }

        // TVs: a few rooms that stay up late.
        var late = rooms.FindAll(r => !r.shop && !r.grace && r.on.Count > 0 && r.on[0].x < start && r.on[0].y >= start + 1.2f);
        late.Sort((a, b) => Hash01(a.houseName, a.data.index, "tv").CompareTo(Hash01(b.houseName, b.data.index, "tv")));
        for (int i = 0; i < late.Count && i < hours.tvRooms; i++)
        {
            Room r = late[i];
            r.tv = true;
            r.tvPane = new Material(paneLit) { name = $"TV room (night) - {r.houseName} {r.data.index}", hideFlags = HideFlags.DontSave };
            r.tvColour = hours.tvGlow;
            made.Add(r.tvPane);
            r.panes.sharedMaterial = r.tvPane;
        }

        // A night owl: somebody still up when the night ends (one per house), upstairs.
        var owlChoices = rooms.FindAll(r => !r.shop && !r.grace && !r.tv && !away.Contains(r.house) && r.data.floor > 0);
        owlChoices.Sort((a, b) => Hash01(a.houseName, a.data.index, "owl").CompareTo(Hash01(b.houseName, b.data.index, "owl")));
        var owlHouses = new HashSet<Transform>();
        foreach (Room r in owlChoices)
        {
            if (owlHouses.Count >= hours.nightOwls) break;
            if (!owlHouses.Add(r.house)) continue;
            r.on.Clear();
            r.on.Add(new Vector2(before, Mathf.Max(start + .5f, hours.owlsUntil)));
            r.owl = true;
        }

        // Later on, someone gets up: a light for a few minutes, in a room that is dark by then.
        var sleepers = rooms.FindAll(r => !r.shop && !r.grace && !away.Contains(r.house) && !LitAfter(r, 25.3f));
        sleepers.Sort((a, b) => Hash01(a.houseName, a.data.index, "wake").CompareTo(Hash01(b.houseName, b.data.index, "wake")));
        var woken = new HashSet<Transform>();
        foreach (Room r in sleepers)
        {
            if (woken.Count >= hours.wakes) break;
            if (!woken.Add(r.house)) continue;   // one per house
            float at = Mathf.Lerp(25.8f, 27.6f, Hash01(r.houseName, r.data.index, "wakes at"));
            r.on.Add(new Vector2(at, at + Mathf.Lerp(.07f, .2f, Hash01(r.houseName, r.data.index, "wakes for"))));
            r.wake = true;
        }
    }

    static bool LitAfter(Room r, float hour)
    {
        foreach (Vector2 span in r.on) if (span.y > hour) return true;
        return false;
    }

    static bool Wants(Room r, float hour)
    {
        foreach (Vector2 span in r.on) if (hour >= span.x && hour < span.y) return true;
        return false;
    }

    // ---------- showing a room ----------

    // Switched on and off, never re-dressed: a house wearing its see-through copies keeps them.
    static void Show(Room room, bool on)
    {
        room.lit = on;
        if (room.panes != null) room.panes.enabled = on;
        if (room.curtainsLit != null) room.curtainsLit.enabled = on && !room.tv;
        if (room.curtainsDark != null) room.curtainsDark.enabled = !on || room.tv;
    }

    // A television: the light jumps as the picture cuts, a little bluer or whiter each time.
    void Flicker(Room room)
    {
        if (room.tvPane == null) return;
        float now = Time.time;
        if (now >= room.tvNext)
        {
            room.tvTarget = UnityEngine.Random.Range(.35f, 1f);
            room.tvNext = now + (UnityEngine.Random.value < .2f ? UnityEngine.Random.Range(.05f, .15f) : UnityEngine.Random.Range(.3f, 1.6f));
            room.tvColour = Color.Lerp(hours.tvGlow, hours.tvGlow * new Color(1.1f, 1.05f, .85f), UnityEngine.Random.value);
        }
        room.tvLevel = Mathf.MoveTowards(room.tvLevel, room.tvTarget, Time.deltaTime * 12f);
        room.tvPane.SetColor(EmissionColorId, room.tvColour * room.tvLevel);
    }

    void Count()
    {
        int n = 0;
        foreach (Room r in rooms) if (r.lit) n++;
        LitNow = n;
        if (n > MostLit) MostLit = n;
    }

    // ---------- making the parts ----------

    // The curtains' mesh in two parts: the faces looking out of the house (toward its window, away from the house's
    // middle) and the rest (the faces into the room, the edges). Made at run time and freed with the night.
    Mesh FacingOut(Mesh curtains, Transform house, Vector3 windowCentre)
    {
        Vector3 outward = windowCentre - house.position;
        outward.y = 0f;
        if (outward.sqrMagnitude < 1e-4f) outward = house.forward;
        Vector3 local = house.InverseTransformDirection(outward.normalized);
        Vector3[] v = curtains.vertices;
        var outside = new List<int>();
        var inside = new List<int>();
        for (int sub = 0; sub < curtains.subMeshCount; sub++)
        {
            int[] t = curtains.GetTriangles(sub);
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 n = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
                (Vector3.Dot(n, local) > 0f ? outside : inside).AddRange(new[] { t[k], t[k + 1], t[k + 2] });
            }
        }
        var mesh = new Mesh { name = curtains.name + " (facing out)", hideFlags = HideFlags.DontSave };
        mesh.SetVertices(v);
        if (curtains.normals.Length == v.Length) mesh.SetNormals(curtains.normals);
        if (curtains.uv.Length == v.Length) mesh.SetUVs(0, curtains.uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(outside, 0);
        mesh.SetTriangles(inside, 1);
        mesh.RecalculateBounds();
        made.Add(mesh);
        return mesh;
    }

    MeshRenderer Part(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return r;
    }

    // A lit room: the house's own glass material (URP Lit), darkened, with a warm glow that is
    // brightest low in the middle of the window and softer towards its edges.
    Material PaneMaterial(Transform house, Texture2D glow)
    {
        Transform glass = house.Find("Window glass");
        Renderer r = glass != null ? glass.GetComponent<Renderer>() : null;
        Material source = r != null ? r.sharedMaterial : null;
        if (source == null || !source.HasProperty(EmissionColorId)) return null;
        var m = new Material(source) { name = "Lit room (night)", hideFlags = HideFlags.DontSave };
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, new Color(.09f, .07f, .05f));
        if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, .35f);
        if (m.HasProperty(EmissionMapId)) m.SetTexture(EmissionMapId, glow);
        m.SetColor(EmissionColorId, hours.roomGlow);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        made.Add(m);
        return m;
    }

    // The curtains of a dark room: the curtain colour, a little dimmer, and no glow.
    Material DarkCurtain(Material lit)
    {
        if (darkCurtains.TryGetValue(lit, out Material dark)) return dark;
        dark = new Material(lit) { name = lit.name + " (dark room)", hideFlags = HideFlags.DontSave };
        if (dark.HasProperty(EmissionColorId)) dark.SetColor(EmissionColorId, Color.black);
        dark.DisableKeyword("_EMISSION");
        if (dark.HasProperty(BaseColorId)) dark.SetColor(BaseColorId, dark.GetColor(BaseColorId) * .55f);
        made.Add(dark);
        darkCurtains[lit] = dark;
        return dark;
    }

    static Renderer CurtainOf(Transform house)
    {
        Transform t = house.Find("Curtain glow");
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    static Texture2D Gradient()
    {
        const int size = 32;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
        {
            name = "Lit room glow (night)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                // A lamp low in the room: brightest a third of the way up, falling off to the sides and top.
                float d = new Vector2((u - .5f) * 1.15f, (v - .36f) * 1.35f).magnitude;
                float value = Mathf.Lerp(1f, .32f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .8f, d)));
                value *= Mathf.Lerp(1f, .8f, Mathf.InverseLerp(.8f, 1f, v));   // the pelmet's shadow at the top
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        t.SetPixels32(pixels);
        t.Apply(false, true);
        return t;
    }

    // ---------- helpers ----------

    /// <summary>A stable number in [0, 1) for this house, room and question: the same every night.</summary>
    public static float Hash01(string house, int index, string what)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in house ?? "") { h ^= c; h *= 16777619u; }
            h ^= (uint)(index * 374761393); h *= 16777619u;
            foreach (char c in what ?? "") { h ^= c; h *= 16777619u; }
            h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>"Root/child/grandchild": the object at that path in the active scene (inactive ones too), or null.</summary>
    public static Transform FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != rootName) continue;
            if (slash < 0) return root.transform;
            Transform found = root.transform.Find(path.Substring(slash + 1));
            if (found != null) return found;
        }
        return null;
    }

    public static string Clock(float hour)
    {
        float h = Mathf.Repeat(hour, 24f);
        int minutes = Mathf.FloorToInt(h * 60f + .001f);
        int hh = minutes / 60 % 24;
        int twelve = hh % 12 == 0 ? 12 : hh % 12;
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00} {2}", twelve, minutes % 60, hh < 12 ? "AM" : "PM");
    }

    /// <summary>For the night's notes.</summary>
    public string Describe(float hour)
    {
        var sb = new StringBuilder();
        int tv = 0, wakes = 0, homecoming = 0, shops = 0, owls = 0;
        foreach (Room r in rooms)
        {
            if (r.tv) tv++;
            if (r.owl) owls++;
            if (r.wake) wakes++;
            if (r.homecoming) homecoming++;
            if (r.shop) shops++;
        }
        sb.Append($"Houses: {Rooms} rooms in {Houses} houses ({shops} shop fronts, dark); lit now {LitNow} at {Clock(hour)} " +
                  $"(lit when the night began {LitAtStart}, most at once {MostLit}, {Changes} rooms switched since); {tv} TV rooms, " +
                  $"{wakes} rooms where someone gets up later, {owls} night owls, {homecoming} lit by a neighbour coming home; the houses' own curtains hidden: {hiddenCurtains.Count}.");
        if (graceHouse != null)
        {
            Room lit = rooms.Find(r => r.grace && r.on.Count > 0);
            sb.Append($" Grace's house '{graceHouse.name}': room {hours.graceRoom} lit until {Clock(hours.graceBedtime)}" +
                      (lit != null ? $", {(lit.lit ? "lit now" : "dark now")}." : " (that room isn't in the list: dark all night)."));
        }
        if (away.Count > 0)
        {
            var names = new List<string>();
            foreach (Transform h in away) if (h != null) names.Add(h.name);
            sb.Append(" Still out (dark until they come home): " + string.Join(", ", names) + ".");
        }
        if (!string.IsNullOrEmpty(Problems)) sb.Append(" Problems: " + Problems + ".");
        return sb.ToString();
    }

    /// <summary>Every room's plan, for the check report.</summary>
    public string Timetable()
    {
        var sb = new StringBuilder();
        foreach (Room r in rooms)
        {
            if (r.on.Count == 0 && !r.shop) continue;
            sb.Append($"  {r.houseName} room {r.data.index} (floor {r.data.floor}{(r.shop ? ", shop" : "")}{(r.tv ? ", TV" : "")}{(r.wake ? ", up later" : "")}{(r.owl ? ", night owl" : "")}{(r.grace ? ", Grace's" : "")}):");
            if (r.on.Count == 0) sb.Append(" dark");
            foreach (Vector2 span in r.on) sb.Append($" {Clock(span.x)}-{Clock(span.y)}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
