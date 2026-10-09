using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// GRACE'S COVER: THE RECORD (playtest 3 session 5, 9 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §6.1)
//
// What Fixit Fidget > Night > Break-ins 8 - Grace's cover (GraceCoverSteps) changed in her house, kept on her rooms' root
// so "Break-ins 8 - Take Grace's cover back out" can put it all back: the pieces it added (her breakfast bar, the boxes at
// the foot of her bed), the pieces whose collider was a box the size of their whole bounds and is now their own shape (her
// armchair, her standard lamp, the coat stand, her bed, the dressing table), and the furniture list GraceHouse fades. No
// behaviour.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceHouseCover : MonoBehaviour
{
    [Tooltip("What Break-ins 8 added: her breakfast bar and the boxes at the foot of her bed.")]
    public GameObject[] added = Array.Empty<GameObject>();
    [Tooltip("Pieces whose bounds box became their own shape (a mesh collider), with that box's centre and size before.")]
    public Transform[] reshaped = Array.Empty<Transform>();
    public Vector3[] boxCentreBefore = Array.Empty<Vector3>();
    public Vector3[] boxSizeBefore = Array.Empty<Vector3>();
    [Tooltip("GraceHouse's furniture list before Break-ins 8.")]
    public GraceHouse.Piece[] furnitureBefore = Array.Empty<GraceHouse.Piece>();
}
