using UnityEngine;

// ---------------------------------------------------------------------------
// GRACE'S HOUSE: THE FINISH (5 Oct 2026; claude/house-interiors-plan.md §9E, the lighting pass)
//
// What Fixit Fidget > Night > Break-ins 5 - Grace's house: the lamps and the window at night (scene) changed,
// kept on her rooms' root so "Break-ins 5 - Take the finish back out" can put it all back. No behaviour: the
// lamps are GraceHouse's night lights as before, and the window's curtains are a mesh with two materials.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceHouseFinish : MonoBehaviour
{
    [Tooltip("Her front window's curtains: the mesh and materials they had before the finish (the glow on both sides).")]
    public MeshFilter curtains;
    public Mesh curtainsBefore;
    public Material[] curtainsMaterialsBefore = System.Array.Empty<Material>();
    [Tooltip("Her house's painted front wall (the street palette): the mesh it had before the clapboard lips' backs were cut out of the window.")]
    public MeshFilter palette;
    public Mesh paletteBefore;
    [Tooltip("Lamps the finish added (night lights of GraceHouse); taken out with it.")]
    public Light[] addedLights = System.Array.Empty<Light>();
    [Tooltip("Lamps the finish changed, and their settings before.")]
    public Light[] changedLights = System.Array.Empty<Light>();
    public float[] intensityBefore = System.Array.Empty<float>();
    public float[] rangeBefore = System.Array.Empty<float>();
    public LightShadows[] shadowsBefore = System.Array.Empty<LightShadows>();
    [Tooltip("GraceHouse's night lights before the finish.")]
    public Light[] nightLightsBefore = System.Array.Empty<Light>();
}
