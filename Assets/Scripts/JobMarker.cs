using UnityEngine;
using TMPro;

// ---------------------------------------------------------------------------
// THE JOB'S COLOUR TAG (playtest 4, 10 Oct 2026)
//
// The HUD spec's §4 (24 Aug, agreed): "the JobMarker digit goes. Not on the device, not over customers." The build still
// floated a big "#2" in the job's colour over the device, across the pocket watch's dial in the bench close-up, and the
// ticket showed no number to match it. Now it is a tag in the job's colour and nothing else: a small round tag on a paper
// rim over the thing it belongs to, the same few pixels across at any distance (the cup's freshness ring is sized the
// same way). The ticket's tape is that colour, and so is the dot beside the customer's patience bar (UiPins): colour ties
// the device, the ticket and the person together, and finding the customer is search, not memory (the name pinned over
// whoever E acts on confirms it).
//
// Never over the work: hidden while the bench's close-up is up (whatever is on the mat), and while a conversation has the
// screen (the intake tray sits between Ace and the customer). Kept off the cup's freshness ring (it sits beside it). In
// first person the thing in Ace's own hands has none.
//
// Show(number, colour) keeps its callers (CustomerBrain, EspressoMachine): the number is simply not shown any more.
// The tag is made in code while playing, with no collider, so it never takes E or the crosshair.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(100)]   // after Billboard turns the marker, so the tag's place is this frame's
public class JobMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    const float TagPixels = 15f;     // the tag's width at 1080p, its paper rim included
    const float ColourShare = .74f;  // the colour's share of that width
    const float GapPixels = 5f;      // between the top of the thing and the tag

    static Sprite disc;
    static Material material;
    static ItemInspector inspector;
    static PlayerCarry carry;
    static CafeViewMode view;
    static float lookedAt = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        disc = null;
        material = null;
        inspector = null;
        carry = null;
        view = null;
        lookedAt = -10f;
    }

    Transform tagRoot;
    SpriteRenderer rim, dot;
    JobBase job;
    Renderer[] solids;
    Camera cam;

    /// <summary>The job's colour, as last shown.</summary>
    public Color TagColour { get; private set; } = Color.white;
    /// <summary>The tag is drawn now.</summary>
    public bool TagShowing => tagRoot != null && gameObject.activeInHierarchy && tagRoot.gameObject.activeSelf;
    /// <summary>Where the tag is now (the world).</summary>
    public Vector3 TagPosition => tagRoot != null ? tagRoot.position : transform.position;
    /// <summary>What the old label says (always nothing now: the digit is gone).</summary>
    public string LabelText => label != null ? label.text : "";

    private void Awake()
    {
        // The prefabs' label still says "#1": it never shows (the HUD spec §4).
        if (label != null)
        {
            label.text = "";
            label.enabled = false;
        }
        Hide();
    }

    /// <summary>The job's tag, in its colour (the number is not shown: the HUD spec §4).</summary>
    public void Show(int number, Color color) => ShowTag(color);

    /// <summary>Kept for any caller: the tag in its colour (no words over a person: their name is pinned when E is on them).</summary>
    public void ShowText(string text, Color color) => ShowTag(color);

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void ShowTag(Color color)
    {
        TagColour = color;
        if (label != null)
        {
            label.text = "";
            label.enabled = false;
        }
        if (Application.isPlaying)
        {
            Build();
            if (dot != null) dot.color = new Color(color.r, color.g, color.b, 1f);
            job = GetComponentInParent<JobBase>(true);
            solids = null;
        }
        gameObject.SetActive(true);
    }

    void Build()
    {
        if (tagRoot != null) return;
        tagRoot = new GameObject("Job colour tag").transform;
        tagRoot.SetParent(transform, false);
        rim = Part("Paper rim", UiSkin.Paper, 0, 1f);
        dot = Part("The job's colour", TagColour, 1, ColourShare);
        tagRoot.gameObject.SetActive(false);
    }

    SpriteRenderer Part(string name, Color colour, int order, float size)
    {
        var part = new GameObject(name);
        part.transform.SetParent(tagRoot, false);
        part.transform.localScale = new Vector3(size, size, size);
        var sprite = part.AddComponent<SpriteRenderer>();
        sprite.sprite = Disc;
        if (TagMaterial != null) sprite.sharedMaterial = TagMaterial;
        sprite.color = colour;
        sprite.sortingOrder = order;
        sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sprite.receiveShadows = false;
        return sprite;
    }

    private void LateUpdate()
    {
        if (tagRoot == null) return;
        bool show = !Quiet();
        if (show)
        {
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            show = cam != null && Place();
        }
        if (tagRoot.gameObject.activeSelf != show) tagRoot.gameObject.SetActive(show);
    }

    // Out of the way: the bench's close-up is up, a conversation has the screen, the recap is up, or (in first person) it's
    // in Ace's own hands.
    bool Quiet()
    {
        if (ConversationController.AnyOpen || RecapUI.Showing) return true;
        if (Time.unscaledTime - lookedAt > 2f && (inspector == null || carry == null))
        {
            lookedAt = Time.unscaledTime;
            carry = PlayerCarry.Instance;
            if (carry != null)
            {
                inspector = carry.GetComponent<ItemInspector>();
                view = carry.GetComponent<CafeViewMode>();
            }
            if (inspector == null) inspector = FindAnyObjectByType<ItemInspector>();
        }
        if (inspector != null && inspector.IsHoldingItem) return true;
        return job != null && carry != null && view != null && view.FirstPersonSelected && carry.Contains(job);
    }

    // Over the top of the thing on the screen, the same size at any distance.
    bool Place()
    {
        Vector3 top = TopOfTheThing();
        Vector3 s = cam.WorldToScreenPoint(top);
        if (s.z <= cam.nearClipPlane) return false;
        float scale = Mathf.Max(.5f, Screen.height / 1080f);
        float width = TagPixels * scale;
        float worldPerPixel = cam.orthographic ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height)
            : 2f * s.z * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, Screen.height);
        s.y += GapPixels * scale + width * .5f;
        // A cup's freshness ring sits right there (DrinkFreshnessRing): the tag sits beside it, to its left.
        if (job is DrinkJob)
        {
            Transform ring = job.transform.Find("Cup freshness indicator");
            if (ring != null && ring.gameObject.activeSelf)
                s.x -= 10f * Mathf.Clamp(Screen.height / 900f, .7f, 1.4f) + 4f * scale + width * .5f;
        }
        tagRoot.position = cam.ScreenToWorldPoint(s);
        tagRoot.rotation = Quaternion.LookRotation(tagRoot.position - cam.transform.position, cam.transform.up);
        // The disc is one unit across at a scale of one: the tag's width in metres at its distance, whatever it hangs from.
        float parent = Mathf.Max(1e-5f, Mathf.Abs(transform.lossyScale.x));
        float world = width * worldPerPixel / parent;
        tagRoot.localScale = new Vector3(world, world, world);
        return true;
    }

    // The top middle of what's drawn of the device or cup (not the tag itself, not its old label); the marker's own place
    // when it hangs from something that isn't a job.
    Vector3 TopOfTheThing()
    {
        if (job == null) return transform.position;
        if (solids == null) solids = job.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in solids)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.transform.IsChildOf(transform)) continue;
            if (r is LineRenderer || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position;
    }

    // ------------------------------------------------------------------ the disc, drawn in code once

    // A white disc one unit across, its edge smoothed (the renderer tints it).
    static Sprite Disc
    {
        get
        {
            if (disc != null) return disc;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Job tag - disc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            float centre = size / 2f, radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(centre, centre));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - d + .5f) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            disc = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
            disc.name = texture.name;
            disc.hideFlags = HideFlags.DontSave;
            return disc;
        }
    }

    // The plain sprite shader the cup's freshness ring already draws with (DrinkFreshnessRing): unlit, no shadows.
    static Material TagMaterial
    {
        get
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            material = new Material(shader) { name = "Job tag (while playing)", hideFlags = HideFlags.DontSave };
            return material;
        }
    }
}
