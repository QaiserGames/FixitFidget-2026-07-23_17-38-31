using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE NIGHT'S COLLISION (night step 4, part 2; claude/night-city-proposal.md §7)
//
// By day the neighbourhood outside the café has no collision at all: only the café's
// people walk there, on their own routes, and POLYGON's own rough collision was
// switched off when the city was built. At night Ace walks those streets, so while the
// night runs every fixed mesh Ace could touch is made solid, exactly as it looks.
//
// This asset is the list of those meshes and where each one sits, written in Edit Mode
// by Fixit Fidget > Night > Night walk 1 (the set-up). NightWalk turns it into colliders
// when the night starts and removes them when it ends. It only refers to the meshes
// (the city's own, and Unity's built-in shapes); it never holds a copy of one. Written
// in Edit Mode on purpose: in Play Mode, Unity's static batching merges most of the
// city's meshes into big combined ones, so the originals can't be read from the scene.
// ---------------------------------------------------------------------------
public sealed class NightCollision : ScriptableObject
{
    [Serializable]
    public struct Piece
    {
        public Mesh mesh;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        [Tooltip("The scene object the mesh belongs to, for reports.")]
        public string from;
    }

    public Piece[] pieces = Array.Empty<Piece>();
    public long triangles;
    [Tooltip("Pieces whose world transform has a shear the collider can't copy exactly (their scale is approximate).")]
    public int sheared;
}
