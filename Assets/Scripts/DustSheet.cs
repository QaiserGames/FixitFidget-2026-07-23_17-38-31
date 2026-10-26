using UnityEngine;

// ---------------------------------------------------------------------------
// A DUST SHEET ON THE HALF-FINISHED CAFÉ'S FLOOR (24 Oct 2026; furniture-spec.md §1.1, §4.7; his answers of 24 Oct, r1)
//
// His choice of 23 Oct, "Grow the café", and of 24 Oct (r1): Day 1 opens half-finished, with 10 seats open (the repair bar's 3,
// the lounge's 2, the window counter's 3, one bistro 2-top) and the rest of the seating floor under dust sheets, where the
// banquette, the communal table, the reading corner and the second bistro will go. The world says it: canvas drop cloths with
// soft folds and taped cardboard boxes on them, no extra text. A sheet:
//   * lies in a new game (no save file) and never in an old save (CafeLayout.Apply); it is saved as one of sheetsLeft;
//   * goes when a piece is placed on it (CafeLayout, after a buy or a move): it folds away with a soft cloth sound (the
//     bench cloth's "cloth.lift" swish until it has a cue of its own), its boxes with it, and never comes back. Sheets still
//     lying show where the café can still grow;
//   * never blocks the ghost: the floor map ignores sheets and their boxes (FurnitureRules.BuildMap). The cloth has no
//     collider; the boxes have one each and carve, like any piece (Ace shouldn't walk through boxes), and go with their sheet.
// Laid by Fixit Fidget > Cafe redesign > Furniture 3 (switched off in the scene; the runtime lays them for a new game).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class DustSheet : MonoBehaviour
{
    [Tooltip("Its save id (\"sheet-1\"...): saved in CafeLayoutData.sheetsLeft while it lies.")]
    [SerializeField] private string id = "";

    [Tooltip("The floor it covers, world metres: x, z of its corner and its size (a piece whose footprint overlaps it folds it away).")]
    [SerializeField] private Rect area;

    [Tooltip("Seconds it takes to fold away (on real time: the world holds still at closing).")]
    [SerializeField, Range(.05f, 1.5f)] private float foldSeconds = .45f;

    public string Id => id;
    public Rect Area => area;
    /// <summary>Lying on the floor.</summary>
    public bool Lying => gameObject.activeSelf && !folding;

    float foldStart = -1f;
    bool folding;
    Vector3 restScale = Vector3.one;

    /// <summary>Set by Furniture 3.</summary>
    public void Init(string sheetId, Rect floor)
    {
        id = sheetId ?? "";
        area = floor;
    }

    /// <summary>Whether a footprint on the floor overlaps this sheet.</summary>
    public bool Covers(Bounds footprint) =>
        footprint.max.x > area.xMin && footprint.min.x < area.xMax && footprint.max.z > area.yMin && footprint.min.z < area.yMax;

    /// <summary>Lying or gone, at once (CafeLayout.Apply).</summary>
    public void SetLying(bool lying)
    {
        folding = false;
        foldStart = -1f;
        transform.localScale = restScale;
        gameObject.SetActive(lying);
    }

    /// <summary>A piece was put on it: it folds away (a soft cloth sound, its boxes with it) and doesn't come back.</summary>
    public void FoldAway()
    {
        if (!gameObject.activeSelf || folding) return;
        folding = true;
        foldStart = Time.realtimeSinceStartup;
        restScale = transform.localScale;
        // Flat, not placed: Arrange's camera is 26 m up, too far for a soft cloth placed in the room to be heard.
        Sfx.Play2D("cloth.lift");   // the bench cloth's soft swish as it lifts off (no cue of its own yet)
    }

    void Awake() => restScale = transform.localScale;

    void Update()
    {
        if (!folding) return;
        float t = Mathf.Clamp01((Time.realtimeSinceStartup - foldStart) / Mathf.Max(.05f, foldSeconds));
        // Gathered up towards its middle and drawn in: the cloth is lifted off and its boxes go with it.
        float k = 1f - t * t * (3f - 2f * t);
        transform.localScale = new Vector3(restScale.x * Mathf.Lerp(.05f, 1f, k), restScale.y * Mathf.Lerp(.2f, 1f, k), restScale.z * Mathf.Lerp(.05f, 1f, k));
        if (t < 1f) return;
        folding = false;
        transform.localScale = restScale;
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // Switched off mid-fold (a load, the scene closing): it's gone, at its own size.
        if (!folding) return;
        folding = false;
        transform.localScale = restScale;
    }
}
