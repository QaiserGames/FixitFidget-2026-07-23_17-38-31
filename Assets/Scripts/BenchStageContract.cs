using UnityEngine;

// ---------------------------------------------------------------------------
// WHAT THE 3D BENCH MUST LEAVE FREE ON SCREEN (14 Oct 2026; specs/uiux-checklist-v2.md §5.8, contracts §3)
//
// UI/UX's six zones at the bench, where the HUD and the cards draw: the device (every quarter turn, both faces), the mat's
// cups, wells, pad and old-parts bin, and every tool on the rest must stay out of U1-U5 (U6, the brief impatience alert, must
// miss only the device). The numbers are UI/UX's, in 1080p canvas units measured from the top left, each zone anchored where
// its HUD piece is (the tabs and the sign on the left, the takings, the card and the controls on the right, the alert at the
// bottom middle). The canvas scales as UiSkin.ScreenCanvas does (1920 x 1080 reference, match 0.5), so 16:9 and 16:10 (his
// Mac's 2560 x 1600) both come out right: at 16:10 the canvas is 1821 x 1138 and the right-hand zones move 99 units left,
// the bottom ones 58 down (U3 at 16:10 is UI/UX's 1317-1797).
//
// Read by Bench 5 (the view probe), Bench 6 (UI/UX's words lab) and Bench 9 (the framing search).
// ---------------------------------------------------------------------------
public static class BenchStageContract   // UI/UX §5.8's zones the 3D bench must leave free
{
    public enum Zone { U1, U2, U3, U4, U5, U6 }
    public static readonly Zone[] All = { Zone.U1, Zone.U2, Zone.U3, Zone.U4, Zone.U5, Zone.U6 };

    const float RefWidth = 1920f, RefHeight = 1080f;

    /// <summary>
    /// The zone in pixels, Unity screen space (origin bottom-left), for the given screen size; both 16:9 and 16:10 handled
    /// (any size: the canvas scales by the square root of the two ratios, as a Canvas Scaler matching width and height 0.5).
    /// </summary>
    public static Rect Rect(Zone zone, int screenWidth, int screenHeight)
    {
        float w = Mathf.Max(1, screenWidth), h = Mathf.Max(1, screenHeight);
        float scale = Mathf.Sqrt(w / RefWidth * (h / RefHeight));
        float cw = w / scale, ch = h / scale;          // the canvas in its own units
        // Each zone in canvas units: x from the left, y from the TOP, as UI/UX wrote them, re-anchored to the canvas's size.
        float x0, x1, y0, y1;
        switch (zone)
        {
            case Zone.U1: x0 = 24f; x1 = cw - 512f; y0 = 20f; y1 = 64f; break;                       // the tabs (the rail keeps 512 free on the right at the bench)
            case Zone.U2: x0 = cw - 324f; x1 = cw - 24f; y0 = 18f; y1 = 110f; break;                // today's takings
            case Zone.U3: x0 = cw - 504f; x1 = cw - 24f; y0 = 132f; y1 = 300f; break;               // the paper card
            case Zone.U4: x0 = 24f; x1 = 256f; y0 = ch - 216f; y1 = ch - 20f; break;                 // the sign, the bar, the time
            case Zone.U5: x0 = cw - 424f; x1 = cw - 24f; y0 = ch - 340f; y1 = ch - 26f; break;      // the controls card / the help hint / the chips
            default: x0 = cw * .5f - 290f; x1 = cw * .5f + 290f; y0 = ch - 114f; y1 = ch - 26f; break;   // U6: the impatience alert
        }
        // Into pixels, origin bottom-left.
        return UnityEngine.Rect.MinMaxRect(x0 * scale, (ch - y1) * scale, x1 * scale, (ch - y0) * scale);
    }

    /// <summary>The zone a screen point (pixels, origin bottom-left) lies in, the first of U1-U6, or null.</summary>
    public static Zone? ZoneAt(Vector2 point, int screenWidth, int screenHeight)
    {
        foreach (Zone zone in All)
            if (Rect(zone, screenWidth, screenHeight).Contains(point)) return zone;
        return null;
    }

    /// <summary>True when a screen rect (pixels, origin bottom-left) overlaps any of U1-U5 (U6 binds only the device).</summary>
    public static bool OverlapsHud(Rect area, int screenWidth, int screenHeight, bool includeU6 = false)
    {
        foreach (Zone zone in All)
        {
            if (zone == Zone.U6 && !includeU6) continue;
            if (Rect(zone, screenWidth, screenHeight).Overlaps(area)) return true;
        }
        return false;
    }
}
