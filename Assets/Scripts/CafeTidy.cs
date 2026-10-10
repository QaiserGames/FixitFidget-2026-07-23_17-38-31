using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE CAFÉ, TIDIED: THE RECORD (playtest 3 session 6, 9 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §7)
//
// What Fixit Fidget > Cafe furnishing > 3 - Playtest 3: put the junk away (CafeTidySteps) put away, and what 3b moved to
// gather the retail in the intake's cubbies, kept on the café's root so "3 - Undo: bring the junk back" can bring each
// piece back where it was. The pieces are switched off, never deleted: the night's set-up finds some of them by name
// (Night 0's back door moved the loose coffee sack and measured the pile). No behaviour.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class CafeTidy : MonoBehaviour
{
    [Tooltip("What the tidy put away (switched off), in the order it found them.")]
    public GameObject[] putAway = Array.Empty<GameObject>();
    [Tooltip("Why each went, as his note named it (same order).")]
    public string[] why = Array.Empty<string>();
    [Tooltip("What 3b moved into the intake's cubbies (the retail gathered in one place).")]
    public Transform[] moved = Array.Empty<Transform>();
    [Tooltip("Where each moved piece stood before (same order).")]
    public Vector3[] movedFrom = Array.Empty<Vector3>();
}
