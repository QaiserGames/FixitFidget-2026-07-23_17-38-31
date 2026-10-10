using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE REPAIR BAR: THE RECORD (the café redesign, 10 Oct 2026; Mansoor's decision of 9 Oct, 23:40: "the repair bar: yes",
// the bench in the front counter's left third, facing the room; claude/studio/cafe-sf-research-and-redesign.md §6.5)
//
// What Fixit Fidget > Cafe redesign > Repair bar 1 (CafeRepairBarSteps) did, kept on the café's root so its Undo can put
// every piece back exactly, after a save and on either machine: the pieces it moved (the repair work area, the rubber mat
// Ace stands on, the clamp lamp) with their local poses before; the pieces it switched off (never deleted: the night's
// set-up finds things by name) and why; the Workbench's solid box it refitted to the new bench, as it was; and the
// temporary stand-in bench it made, if it made one. No behaviour.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class CafeRepairBar : MonoBehaviour
{
    [Tooltip("Where the work area's root stood before the move (the turn's centre).")]
    public Vector3 rootWasAt;
    [Tooltip("Where the work area's root stands after it.")]
    public Vector3 rootMovedTo;
    [Tooltip("The turn about world up, in degrees.")]
    public float turnDegrees = 180f;

    [Tooltip("What the move carried (each by the same turn and shift).")]
    public Transform[] moved = Array.Empty<Transform>();
    [Tooltip("Each one's local position before (same order).")]
    public Vector3[] movedFromLocalPosition = Array.Empty<Vector3>();
    [Tooltip("Each one's local rotation before (same order).")]
    public Quaternion[] movedFromLocalRotation = Array.Empty<Quaternion>();

    [Tooltip("What the move switched off (never deleted).")]
    public GameObject[] switchedOff = Array.Empty<GameObject>();
    [Tooltip("Why each went (same order).")]
    public string[] why = Array.Empty<string>();

    [Tooltip("The Workbench's solid box, refitted to the new bench.")]
    public BoxCollider refitted;
    public Vector3 refittedFromCenter;
    public Vector3 refittedFromSize;

    [Tooltip("The temporary stand-in bench made where the kit's bench isn't in yet (null when there was none, or it was taken away).")]
    public GameObject standIn;
}
