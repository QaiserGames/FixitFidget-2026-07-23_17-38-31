using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE NOTEBOOK'S PICTURES, PART 2: THE PICTURES THEMSELVES (23 Oct 2026)
//
// His note (22 Oct): the night's notes are "just a cluster of words, it's word vomit, and I rather like it being easier
// to see." Every subject the notebook can be about (NoteSubjects) has a picture, found here in this order:
//   1. a PNG under Assets/Data/Resources/UI/NotePhotos/ named after the subject's key (barnaby.png, gracehouse.png...).
//      Fixit Fidget › Night › Notebook pictures 1 (NotePhotoSteps) photographs each subject from the game's own models
//      in a preview studio and writes them there, with index.json (each key's file, how it was taken, and where a place
//      is, for its map). His girlfriend can replace any PNG with a drawing of the same name: the step never overwrites a
//      PNG it didn't make (it keeps each file's fingerprint), and the game simply shows whatever PNG is there;
//   2. otherwise a drawing made here in code (a head and shoulders in the person's colour, a house, a door, the bins, a
//      gnome, cups, a crate, a cone, the café): never a blank, never a missing picture.
// Places also get a tiny street map card (Map): drawn in code, like the HUD's sun dial, not rendered from above. From the
// overhead camera the district is rooftops, lamps and trees, and at 70 px a render of it is a grey blur; drawn, it is
// four streets, the café in its green and one pin, readable at a glance, with no words to read (the title says the
// address). It is turned the way the overhead camera looks (CafeViewMode) when the page is laid out, so the pin is where
// she'd look on screen; orbiting with the page open doesn't redraw it.
// A PNG named map_<key>.png in the same folder replaces a drawn map, for a hand-drawn one.
//
// The UI pieces (Polaroid, MapCard, Thumb) are the night's page's (NightNotebook) and the phone's Notes' to share:
// placed in their parent's top-left frame, x right and y down (RecapPhone's Box convention), tilted about their middle.
// Everything here is made in code while playing (or in the editor's checks), cached, and never saved. What it costs: a
// drawing or a map is about 25 ms the first time it's needed (128 px, one sample a pixel: see Draw), on the main thread,
// then nothing. So before the photos exist, the first open of the night's page draws its first screen's pictures and its
// map (five and one: about 150 ms, a short hitch once a session); with the photos, only the map. The cache keeps each
// drawing, and one map a place (and its amber twin): a new turn of the camera replaces it.
// ---------------------------------------------------------------------------
public static class NotePhotos
{
    /// <summary>The pictures' Resources folder (Assets/Data/Resources/UI/NotePhotos).</summary>
    public const string Folder = "UI/NotePhotos";
    /// <summary>The same folder as an asset path (the photo step writes there).</summary>
    public const string AssetFolder = "Assets/Data/Resources/" + Folder;
    /// <summary>The index's name in the folder (index.json).</summary>
    public const string IndexName = "index";
    /// <summary>A photo's size in pixels (square).</summary>
    public const int PhotoSize = 256;
    /// <summary>A polaroid is this much taller than it is wide (the chin under the photo).</summary>
    public const float PolaroidAspect = 1.15f;
    /// <summary>The bench's amber, the game's "next" (BenchOutline.Guide, #FFB238): tonight's pins and the street to take.</summary>
    public static readonly Color Next = new Color(1f, .698f, .220f);   // #FFB238

    // ---------- the index (written by the photo step) ----------

    [Serializable]
    public sealed class Entry
    {
        public string key = "";
        /// <summary>The PNG's name in the folder, without ".png" ("" when there is none: the drawing is shown).</summary>
        public string file = "";
        /// <summary>How the picture was made, or why it couldn't be (the step's report line).</summary>
        public string how = "";
        /// <summary>The fingerprint of the PNG the step last wrote ("" if it never wrote one).</summary>
        public string made = "";
        /// <summary>The PNG there now isn't the step's (a drawing): the step leaves it alone.</summary>
        public bool kept;
        /// <summary>Where the subject is in the world (metres, x and z), for a place's map.</summary>
        public bool hasSpot;
        public float x, z;
    }

    [Serializable]
    public sealed class Index
    {
        public int version = 1;
        public string made = "";
        public Entry[] photos = new Entry[0];

        public Entry Find(string key)
        {
            if (photos == null || string.IsNullOrEmpty(key)) return null;
            foreach (Entry e in photos) if (e != null && e.key == key) return e;
            return null;
        }
    }

    static Index index;
    static bool indexRead;
    static readonly Dictionary<string, Sprite> photos = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static readonly Dictionary<string, Sprite> drawn = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static readonly Dictionary<string, DrawnMap> maps = new Dictionary<string, DrawnMap>(StringComparer.Ordinal);
    static readonly HashSet<string> noPhoto = new HashSet<string>(StringComparer.Ordinal);
    static CafeViewMode view;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Forget();

    /// <summary>Forgets every picture and the index, and lets go of what was drawn (the photo step calls it after writing new ones).</summary>
    public static void Forget()
    {
        index = null;
        indexRead = false;
        foreach (Sprite s in photos.Values) Throw(s, false);   // their textures are the project's PNGs
        foreach (Sprite s in drawn.Values) Throw(s, true);
        foreach (DrawnMap m in maps.Values) Throw(m.sprite, true);
        photos.Clear();
        drawn.Clear();
        maps.Clear();
        noPhoto.Clear();
        view = null;
    }

    /// <summary>The photo step's index, or null when it hasn't run.</summary>
    public static Index Loaded
    {
        get
        {
            if (indexRead) return index;
            indexRead = true;
            TextAsset text = Resources.Load<TextAsset>(Folder + "/" + IndexName);
            if (text == null) return index = null;
            try { index = JsonUtility.FromJson<Index>(text.text); }
            catch (Exception e)
            {
                Debug.LogWarning("[Notebook pictures] " + Folder + "/" + IndexName + ".json couldn't be read (" + e.Message + "): the drawings are used.");
                index = null;
            }
            return index;
        }
    }

    // ---------- the pictures ----------

    /// <summary>True when <paramref name="key"/> has a PNG (a photo or a drawing put in its place), not the drawing made here.</summary>
    public static bool HasPhoto(string key) => TryPhoto(key, out _);

    /// <summary>The PNG for <paramref name="key"/> as a sprite (the index's file, else one named after the key).</summary>
    public static bool TryPhoto(string key, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(key) || noPhoto.Contains(key)) return false;
        if (photos.TryGetValue(key, out sprite) && sprite != null) return true;
        Entry entry = Loaded != null ? Loaded.Find(key) : null;
        Texture2D texture = null;
        if (entry != null && !string.IsNullOrEmpty(entry.file)) texture = Resources.Load<Texture2D>(Folder + "/" + entry.file);
        if (texture == null) texture = Resources.Load<Texture2D>(Folder + "/" + key);
        if (texture == null)
        {
            noPhoto.Add(key);
            return false;
        }
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        sprite.name = "Note photo - " + key;
        sprite.hideFlags = HideFlags.DontSave;
        photos[key] = sprite;
        return true;
    }

    /// <summary>The picture for <paramref name="key"/>: its PNG, else its drawing. Never null.</summary>
    public static Sprite Picture(string key) => TryPhoto(key, out Sprite photo) ? photo : Drawing(key);

    /// <summary>What a subject is drawn as when it has no PNG ("person", "house"...); a key with no subject is someone.</summary>
    public static string DrawingOf(string key)
    {
        NoteSubject s = NoteSubjects.Find(key) ?? NoteSubjects.Find(NoteSubjects.Someone);
        return s != null ? s.draw : "person";
    }

    /// <summary>The drawings this file can make: every subject's "draw" must be one of them (the notebook rules check it).</summary>
    public static readonly string[] Drawings = { "person", "house", "door", "bins", "corner", "gnome", "cups", "crate", "cone", "cafe" };

    /// <summary>The backdrop behind a subject's picture, by kind: people warm, places sky, things sage (the photo step's too).</summary>
    public static Color Backdrop(NoteKind kind) => kind == NoteKind.Person ? new Color(.91f, .79f, .66f)
        : kind == NoteKind.Place ? new Color(.66f, .77f, .86f) : new Color(.75f, .80f, .69f);

    /// <summary>The drawing made in code for <paramref name="key"/> (cached).</summary>
    public static Sprite Drawing(string key)
    {
        string k = string.IsNullOrEmpty(key) ? NoteSubjects.Someone : key;
        if (drawn.TryGetValue(k, out Sprite sprite) && sprite != null) return sprite;
        NoteSubject s = NoteSubjects.Find(k) ?? NoteSubjects.Find(NoteSubjects.Someone);
        Color colour = s != null && ColorUtility.TryParseHtmlString(s.colour, out Color c) ? c : new Color(.55f, .48f, .42f);
        Texture2D texture = DrawSubject(s != null ? s.draw : "person", colour, Backdrop(s != null ? s.kind : NoteKind.Person), k);
        sprite = MakeSprite(texture, "Note drawing - " + k);
        drawn[k] = sprite;
        return sprite;
    }

    /// <summary>A tilt for <paramref name="key"/>'s polaroid, -3 to +3 degrees and never flat, the same every time.</summary>
    public static float Tilt(string key)
    {
        uint hash = 2166136261;
        foreach (char ch in key ?? "") hash = (hash ^ ch) * 16777619;
        int t = (int)(hash % 7u) - 3;
        return t == 0 ? 2f : t;
    }

    // ---------- where a place is, and its map ----------

    /// <summary>Where a place is (metres, x and z): the photo step's measure, else the scene's own (Grace's door, the bins).</summary>
    public static bool TrySpot(string key, out Vector2 spot)
    {
        spot = default;
        Entry entry = Loaded != null ? Loaded.Find(key) : null;
        if (entry != null && entry.hasSpot) { spot = new Vector2(entry.x, entry.z); return true; }
        switch (key)
        {
            case NoteSubjects.Cafe:
                spot = CafeDaylight.CafeInside.center;
                return true;
            case NoteSubjects.GraceHouse:
            {
                HomeDoor door = HomeDoor.Find("home.grace");   // Grace's profile's Home Id (HomeSetup)
                if (door == null) return false;
                spot = new Vector2(door.DoorPoint.x, door.DoorPoint.z);
                return true;
            }
            case NoteSubjects.BackDoor:
            case NoteSubjects.Alley:
            case NoteSubjects.Corner:
            {
                NightZeroSet set = NightZeroSet.Instance;
                if (set == null) return false;
                Vector3 at = key == NoteSubjects.BackDoor && set.outsideDoor != null ? set.outsideDoor.position
                    : key == NoteSubjects.Corner && set.cornerGnome != null ? set.cornerGnome.transform.position : set.Bins;
                spot = new Vector2(at.x, at.z);
                return true;
            }
        }
        return false;
    }

    // The district's four streets round the café, their middle lines (metres; NightWalkSteps.StreetsX and StreetsZ): West
    // Street at x -12.0 and East Street at x 12.6 run along z; Front Street at z -7.7 and Back Street at z 23.2 run along x.
    // The café's floor is CafeDaylight.CafeInside (x -7.4 to 7.4, z 0.1 to 18; its door on Front Street).
    static readonly (string id, bool alongZ, float at)[] Streets =
    {
        ("west", true, -12.0f), ("east", true, 12.6f), ("front", false, -7.7f), ("back", false, 23.2f),
    };
    const float RoadHalfWidth = 4f;
    const int MapPixels = 128;

    /// <summary>
    /// A place's street map card: the four streets round the café (its own street lit, in amber when it's tonight's), the
    /// café in its green, and a pin, turned the way the overhead camera looks as it's asked for (in 15 degree steps; the
    /// night's page asks when it's laid out). Null for a subject that isn't a place, or a place with no spot. A PNG named
    /// map_&lt;key&gt; replaces the drawing.
    /// </summary>
    public static Sprite Map(string key, bool now)
    {
        NoteSubject s = NoteSubjects.Find(key);
        if (s == null || s.kind != NoteKind.Place) return null;
        if (TryPhoto("map_" + key, out Sprite drawnByHand)) return drawnByHand;
        if (!TrySpot(key, out Vector2 spot)) return null;
        int turn = Mathf.RoundToInt(Mathf.Repeat(OverheadYaw(), 360f) / 15f) % 24;
        string mapKey = key + (now ? ":now" : "");
        if (maps.TryGetValue(mapKey, out DrawnMap made) && made.sprite != null && made.turn == turn) return made.sprite;
        // A new turn of the camera: the map is drawn again and the old one goes (the page lays its rows out afresh each
        // time, so nothing still shows it). One map a place, and its amber one: the cache never holds more than that.
        if (made != null) Throw(made.sprite, true);
        Sprite sprite = MakeSprite(DrawMap(spot, s.street, turn * 15f, now), "Note map - " + key);
        maps[mapKey] = new DrawnMap { sprite = sprite, turn = turn };
        return sprite;
    }

    sealed class DrawnMap
    {
        public Sprite sprite;
        public int turn;
    }

    // A sprite this file made, gone, and its texture with it when this file drew that too (never a Resources texture).
    static void Throw(Sprite sprite, bool andTexture)
    {
        if (sprite == null) return;
        Texture2D texture = andTexture ? sprite.texture : null;
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    /// <summary>The overhead camera's turn (degrees), what the map is turned by; 0 with no café view in the scene.</summary>
    public static float OverheadYaw()
    {
        if (view == null) view = UnityEngine.Object.FindAnyObjectByType<CafeViewMode>();
        return view != null ? view.OverheadAngle.x : 0f;
    }

    // ---------- the UI pieces (the night's page and the phone's Notes) ----------

    /// <summary>
    /// A polaroid of <paramref name="key"/> in <paramref name="parent"/>: its top-left corner at (x, y) from the parent's
    /// top-left (y down), <paramref name="width"/> wide and PolaroidAspect times as tall, tilted about its middle, a soft
    /// shadow under it, and tape over its top edge (or, <paramref name="pin"/>, a pin in the bench's amber: tonight's).
    /// </summary>
    public static RectTransform Polaroid(RectTransform parent, string key, float x, float y, float width, float tilt, bool pin)
    {
        float height = Mathf.Round(width * PolaroidAspect);
        float border = Mathf.Max(3f, Mathf.Round(width * .07f));
        RectTransform card = Centred("Polaroid - " + key, parent, x, y, width, height, tilt);
        UiSkin.Paint(card, new Color(.985f, .97f, .94f), Mathf.Max(2f, width * .03f));
        UiSkin.DropShadow(card, Mathf.Max(4f, width * .06f), Mathf.Max(2f, width * .03f), .5f);
        float photo = width - 2f * border;
        Image picture = Child("Photo", card, border, border, photo, photo).gameObject.AddComponent<Image>();
        picture.sprite = Picture(key);
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        if (pin) Pin(card, width);
        else Tape(card, width, tilt);
        return card;
    }

    /// <summary>A place's map card (<see cref="Map"/>) as a small square card, tilted; null when the place has no map.</summary>
    public static RectTransform MapCard(RectTransform parent, string key, float x, float y, float size, float tilt, bool now)
    {
        Sprite map = Map(key, now);
        if (map == null) return null;
        float border = Mathf.Max(3f, Mathf.Round(size * .06f));
        RectTransform card = Centred("Map - " + key, parent, x, y, size, size, tilt);
        UiSkin.Paint(card, new Color(.985f, .97f, .94f), 3f);
        UiSkin.DropShadow(card, 5f, 2f, .5f);
        Image picture = Child("Map", card, border, border, size - 2f * border, size - 2f * border).gameObject.AddComponent<Image>();
        picture.sprite = map;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        return card;
    }

    /// <summary>A small square picture of <paramref name="key"/> with round corners (a fact's line in the phone's Notes).</summary>
    public static RectTransform Thumb(RectTransform parent, string key, float x, float y, float size)
    {
        RectTransform frame = Centred("Thumb - " + key, parent, x, y, size, size, 0f);
        UiSkin.Paint(frame, new Color(.985f, .97f, .94f), Mathf.Max(2f, size * .12f));
        float border = Mathf.Max(1.5f, size * .06f);
        Image picture = Child("Photo", frame, border, border, size - 2f * border, size - 2f * border).gameObject.AddComponent<Image>();
        picture.sprite = Picture(key);
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        return frame;
    }

    // A box placed by its top-left corner in the parent's top-left frame (y down), turned about its own middle.
    static RectTransform Centred(string name, RectTransform parent, float x, float y, float width, float height, float tilt)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x + width * .5f, -(y + height * .5f));
        rect.sizeDelta = new Vector2(width, height);
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        return rect;
    }

    // A child at (x, y) from its parent's top-left corner (y down).
    static RectTransform Child(string name, RectTransform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // A strip of tape across the top edge: paper, half see-through, at an angle against the card's own.
    static void Tape(RectTransform card, float width, float tilt)
    {
        var rect = new GameObject("Tape", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(card, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(width * .04f, -1f);
        rect.sizeDelta = new Vector2(width * .46f, Mathf.Max(8f, width * .15f));
        rect.localRotation = Quaternion.Euler(0f, 0f, -tilt * 1.5f - 4f);
        UiSkin.Paint(rect, new Color(1f, .96f, .86f, .62f), 1.5f);
    }

    // A pin in the bench's amber with a dark rim and a glint: tonight's.
    static void Pin(RectTransform card, float width)
    {
        float size = Mathf.Max(10f, Mathf.Round(width * .18f));
        var rim = new GameObject("Pin", typeof(RectTransform)).GetComponent<RectTransform>();
        rim.SetParent(card, false);
        rim.anchorMin = rim.anchorMax = new Vector2(.5f, 1f);
        rim.pivot = new Vector2(.5f, .5f);
        rim.anchoredPosition = new Vector2(0f, -size * .45f);
        rim.sizeDelta = new Vector2(size + 3f, size + 3f);
        Image ring = rim.gameObject.AddComponent<Image>();
        ring.sprite = UiSkin.Circle;
        ring.color = new Color(.36f, .22f, .05f);
        ring.raycastTarget = false;
        var head = new GameObject("Head", typeof(RectTransform)).GetComponent<RectTransform>();
        head.SetParent(rim, false);
        head.sizeDelta = new Vector2(size, size);
        Image fill = head.gameObject.AddComponent<Image>();
        fill.sprite = UiSkin.Circle;
        fill.color = Next;
        fill.raycastTarget = false;
        var glint = new GameObject("Glint", typeof(RectTransform)).GetComponent<RectTransform>();
        glint.SetParent(head, false);
        glint.anchoredPosition = new Vector2(-size * .18f, size * .18f);
        glint.sizeDelta = new Vector2(size * .3f, size * .3f);
        Image shine = glint.gameObject.AddComponent<Image>();
        shine.sprite = UiSkin.Circle;
        shine.color = new Color(1f, 1f, 1f, .75f);
        shine.raycastTarget = false;
    }

    static Sprite MakeSprite(Texture2D texture, string name)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // ---------- drawing in code (signed distances, every edge smoothed over one pixel by Inside) ----------

    delegate Color Painter(float x, float y);

    const int DrawPixels = 128;

    // One sample a pixel, at its middle (UiSkin's way). Inside already smooths every edge over a pixel, so the HUD's two by
    // two samples looked the same here at four times the cost: about 25 ms a drawing or a map instead of 100 (timed under
    // Mono on a 2.8 GHz Xeon), on the main thread, the first time each is needed.
    static Texture2D Draw(string name, int size, Painter paint)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = paint(x + .5f, y + .5f);
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f) return Color.clear;
        Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
        c.a = a;
        return c;
    }

    static Color With(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(alpha));
    static float Inside(float d) => Mathf.Clamp01(.5f - d);

    static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Circle(float x, float y, float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry) =>
        (new Vector2((x - cx) / rx, (y - cy) / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    // Signed distance to a triangle (negative inside; Inigo Quilez's sdTriangle).
    static float Triangle(float x, float y, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        var p = new Vector2(x, y);
        Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
        Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        Vector2 d = Vector2.Min(Vector2.Min(new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                                            new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }

    // A filled shape with a dark rim 2 px wide round it.
    static Color Shape(Color under, float d, Color fill, Color rim)
    {
        under = Over(under, With(rim, Inside(d - 2f)));
        return Over(under, With(fill, Inside(d)));
    }

    static Color Darker(Color c, float k = .55f) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    static Color Lighter(Color c, float k = .35f) => Color.Lerp(c, Color.white, k);

    // A subject drawn in its colour on its kind's backdrop, a soft vignette round the edge (a photo's look).
    static Texture2D DrawSubject(string what, Color colour, Color backdrop, string key)
    {
        Color rim = Darker(colour, .45f), cream = new Color(.97f, .94f, .88f), ink = new Color(.16f, .14f, .12f);
        Color skin = new Color(.95f, .78f, .64f);
        bool grace = key == NoteSubjects.GraceHouse;
        return Draw("Note drawing - " + key, DrawPixels, (x, y) =>
        {
            float edge = new Vector2(x - 64f, y - 64f).magnitude / 90f;
            Color c = Color.Lerp(Lighter(backdrop, .18f), Darker(backdrop, .86f), Mathf.Clamp01(edge * edge));
            switch (what)
            {
                case "house":
                    c = Shape(c, RoundBox(x, y, 64f, 44f, 38f, 30f, 3f), colour, rim);
                    c = Shape(c, Triangle(x, y, new Vector2(18f, 72f), new Vector2(110f, 72f), new Vector2(64f, 108f)), Darker(colour, .75f), rim);
                    c = Over(c, With(Darker(colour, .4f), Inside(RoundBox(x, y, 64f, 26f, 9f, 13f, 2f))));
                    c = Over(c, With(cream, Inside(RoundBox(x, y, 40f, 52f, 9f, 9f, 1.5f))));
                    c = Over(c, With(cream, Inside(RoundBox(x, y, 88f, 52f, 9f, 9f, 1.5f))));
                    if (grace) c = Over(c, With(new Color(1f, .86f, .45f), Inside(Circle(x, y, 79f, 40f, 4f))));   // her porch light
                    break;
                case "door":
                    c = Shape(c, RoundBox(x, y, 64f, 62f, 32f, 54f, 3f), Darker(colour, .7f), ink);
                    c = Shape(c, RoundBox(x, y, 64f, 58f, 24f, 46f, 2f), colour, rim);
                    c = Over(c, With(Darker(colour, .6f), Inside(Mathf.Abs(RoundBox(x, y, 64f, 80f, 16f, 15f, 2f)) - 1.2f)));
                    c = Over(c, With(Darker(colour, .6f), Inside(Mathf.Abs(RoundBox(x, y, 64f, 38f, 16f, 15f, 2f)) - 1.2f)));
                    c = Over(c, With(new Color(.93f, .76f, .36f), Inside(Circle(x, y, 80f, 58f, 3.5f))));
                    break;
                case "bins":
                    c = Shape(c, RoundBox(x, y, 64f, 46f, 48f, 28f, 4f), colour, rim);
                    c = Shape(c, Triangle(x, y, new Vector2(14f, 72f), new Vector2(114f, 72f), new Vector2(110f, 86f)), Darker(colour, .7f), rim);
                    c = Shape(c, Triangle(x, y, new Vector2(14f, 72f), new Vector2(110f, 86f), new Vector2(18f, 86f)), Darker(colour, .7f), rim);
                    c = Shape(c, Circle(x, y, 32f, 14f, 7f), ink, ink);
                    c = Shape(c, Circle(x, y, 96f, 14f, 7f), ink, ink);
                    break;
                case "corner":
                    c = Shape(c, RoundBox(x, y, 46f, 44f, 18f, 28f, 3f), colour, rim);
                    c = Shape(c, RoundBox(x, y, 46f, 74f, 21f, 4f, 2f), Darker(colour, .75f), rim);
                    c = Shape(c, Triangle(x, y, new Vector2(37f, 79f), new Vector2(55f, 79f), new Vector2(46f, 104f)), new Color(.78f, .27f, .18f), rim);
                    c = Shape(c, RoundBox(x, y, 90f, 30f, 22f, 16f, 2f), new Color(.69f, .54f, .35f), rim);
                    break;
                case "gnome":
                    c = Shape(c, RoundBox(x, y, 64f, 28f, 22f, 18f, 8f), new Color(.23f, .37f, .63f), rim);
                    c = Shape(c, Triangle(x, y, new Vector2(42f, 62f), new Vector2(86f, 62f), new Vector2(64f, 24f)), cream, rim);
                    c = Shape(c, Circle(x, y, 64f, 64f, 11f), skin, rim);
                    c = Shape(c, Triangle(x, y, new Vector2(40f, 72f), new Vector2(88f, 72f), new Vector2(64f, 118f)), colour, rim);
                    break;
                case "cups":
                    for (int i = 0; i < 3; i++)
                    {
                        float top = 92f - i * 8f;
                        c = Shape(c, RoundBox(x, y, 64f, top - 34f, 20f - i, 34f, 3f), cream, Darker(colour, .5f));
                        c = Over(c, With(new Color(.80f, .45f, .20f), Inside(RoundBox(x, y, 64f, top - 40f, 20f - i, 4f, 0f))));
                    }
                    break;
                case "crate":
                    c = Shape(c, RoundBox(x, y, 64f, 48f, 44f, 32f, 3f), colour, rim);
                    c = Over(c, With(Lighter(colour, .45f), Inside(RoundBox(x, y, 64f, 48f, 6f, 32f, 0f))));
                    c = Over(c, With(Darker(colour, .6f), Inside(Mathf.Abs(y - 66f) - .8f) * Inside(RoundBox(x, y, 64f, 48f, 44f, 32f, 3f))));
                    break;
                case "cone":
                    c = Shape(c, RoundBox(x, y, 64f, 16f, 36f, 6f, 2f), Darker(colour, .7f), rim);
                    float cone = Triangle(x, y, new Vector2(40f, 20f), new Vector2(88f, 20f), new Vector2(64f, 112f));
                    c = Shape(c, cone, colour, rim);
                    c = Over(c, With(cream, Inside(Mathf.Abs(y - 46f) - 5f) * Inside(cone)));
                    c = Over(c, With(cream, Inside(Mathf.Abs(y - 76f) - 4f) * Inside(cone)));
                    break;
                case "cafe":
                    c = Shape(c, RoundBox(x, y, 64f, 46f, 46f, 34f, 3f), cream, ink);
                    c = Shape(c, RoundBox(x, y, 64f, 84f, 52f, 9f, 3f), colour, rim);
                    c = Over(c, With(cream, Inside(RoundBox(x, y, 64f, 84f, 52f, 9f, 3f)) * (Mathf.Repeat(x, 16f) < 8f ? .35f : 0f)));
                    c = Shape(c, RoundBox(x, y, 46f, 46f, 18f, 15f, 2f), new Color(.55f, .70f, .78f), ink);
                    c = Shape(c, RoundBox(x, y, 88f, 36f, 10f, 24f, 2f), colour, rim);
                    break;
                default:   // a person: head and shoulders
                    c = Shape(c, Ellipse(x, y, 64f, 6f, 48f, 42f), colour, rim);
                    c = Shape(c, RoundBox(x, y, 64f, 48f, 9f, 10f, 4f), skin, rim);
                    c = Shape(c, Circle(x, y, 64f, 74f, 24f), skin, rim);
                    c = Over(c, With(Darker(colour, .6f), Inside(Mathf.Max(Circle(x, y, 64f, 78f, 25f), 80f - y))));   // hair
                    break;
            }
            return c;
        });
    }

    // The map: ground, the four streets (the place's own lit), the café, the pin. Turned by yaw: up on the card is the way
    // the overhead camera looks.
    static Texture2D DrawMap(Vector2 spot, string street, float yaw, bool now)
    {
        Rect cafe = CafeDaylight.CafeInside;
        Vector2 middle = (cafe.center + spot) * .5f;
        float half = Mathf.Max((cafe.center - spot).magnitude * .5f + 14f, 24f);   // metres from the card's middle to its edge
        float scale = (MapPixels * .5f) / half;                                    // pixels a metre
        float a = yaw * Mathf.Deg2Rad;
        var forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        var right = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
        Vector2 off = spot - middle;
        float pinX = MapPixels * .5f + Vector2.Dot(off, right) * scale, pinY = MapPixels * .5f + Vector2.Dot(off, forward) * scale;
        Color ground = new Color(.86f, .83f, .76f), road = new Color(.45f, .42f, .40f), lit = now ? Next : new Color(.96f, .92f, .84f);
        Color green = UiSkin.Brand, ink = new Color(.16f, .14f, .12f), pinColour = now ? Next : UiSkin.Red;
        return Draw("Note map", MapPixels, (x, y) =>
        {
            float u = (x - MapPixels * .5f) / scale, v = (y - MapPixels * .5f) / scale;
            Vector2 p = middle + right * u + forward * v;
            Color c = ground;
            foreach (var s in Streets)
            {
                float d = (Mathf.Abs((s.alongZ ? p.x : p.y) - s.at) - RoadHalfWidth) * scale;
                bool mine = s.id == street;
                c = Over(c, With(mine ? lit : road, Inside(d) * (mine ? .9f : 1f)));
            }
            float block = RoundBox(p.x, p.y, cafe.center.x, cafe.center.y, cafe.width * .5f, cafe.height * .5f, .8f) * scale;
            c = Over(c, With(ink, Inside(block - 1.5f)));
            c = Over(c, With(green, Inside(block)));
            c = Over(c, With(Color.white, .85f * Inside(Circle(p.x, p.y, cafe.center.x, cafe.center.y, 2.2f) * scale)));
            // The pin, upright on the card whatever the turn: a round head over a point.
            float head = Circle(x, y, pinX, pinY + 13f, 8.5f);
            float tip = Triangle(x, y, new Vector2(pinX - 6.5f, pinY + 9f), new Vector2(pinX + 6.5f, pinY + 9f), new Vector2(pinX, pinY));
            float pin = Mathf.Min(head, tip);
            c = Over(c, With(ink, Inside(pin - 1.6f)));
            c = Over(c, With(pinColour, Inside(pin)));
            c = Over(c, With(Color.white, .9f * Inside(Circle(x, y, pinX, pinY + 13f, 3f))));
            return c;
        });
    }
}
