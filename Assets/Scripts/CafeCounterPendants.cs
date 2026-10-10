using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE COUNTER'S PENDANTS: THE RECORD (playtest 3 session 6, 9 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §7)
//
// His note: "the counters have inconsistencies". The four front counter sections share one set of materials; the one that
// read lighter and more orange was lit, not painted: a warm fill hung behind the middle section with no lamp to show for it,
// and gave that section about four times the warm light of its neighbours (the far-left section got almost none).
// Fixit Fidget > Cafe furnishing > 4 - Light the counter (CafeTidySteps) hangs four pendants over the counter, the same
// pendant as over the tables, evenly spaced, each with the same warm light, and moves that fill back over the back bar
// (its own work: the machine, the bench), dimmer. This record is kept on the pendants' group so "4 - Undo: take the
// counter's pendants down" can put the fill back where it was. No behaviour.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class CafeCounterPendants : MonoBehaviour
{
    [Tooltip("The pendants' lights (registered with CafeDaylight's warm lights, so they dim by day and at night).")]
    public Light[] lights = Array.Empty<Light>();
    [Tooltip("The pendants themselves (registered with CafeViewMode's overhead fixtures: hidden in the overhead view).")]
    public Renderer[] shades = Array.Empty<Renderer>();
    [Tooltip("The warm fill that hung behind the middle section, moved back over the back bar.")]
    public Light movedFill;
    public Vector3 fillWasAt;
    public float fillWasIntensity;
    public float fillWasRange;
}
