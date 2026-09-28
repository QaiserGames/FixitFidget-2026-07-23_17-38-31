using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: THE ROOMS BEHIND THE WINDOWS (claude/night-city-proposal.md §9)
//
// Every house round the café (the six bay-window houses and the shop houses) has
// all its window panes in one merged "Window glass" mesh, and some have one merged
// "Curtain glow" mesh that the day's evening light makes glow all at once. At night
// that read as every window of a house lit at 11 PM, and a street that never went
// to bed. This asset splits them into rooms: one room per window (a bay's three
// panes are one room), so the night can light them one by one and put them out one
// by one (NightHomes).
//
// Written in Edit Mode by Fixit Fidget > Night > Night walk 4a - Build the lived-in
// windows. In Play Mode, Unity's static batching merges the houses' meshes, so the
// originals can't be read then (the same reason as the night's collision list). The
// meshes here are new ones, each room's panes and curtains, kept inside this asset
// and made in the house's own space, so they follow the house if it ever moves.
// Nothing here changes the day: by day none of it is shown.
// ---------------------------------------------------------------------------
public sealed class NightRooms : ScriptableObject
{
    [Serializable]
    public sealed class Room
    {
        [Tooltip("The house object's name, e.g. \"1 - Saffron bay-window house\".")]
        public string house = "";
        [Tooltip("The house's path in the scene, to find it at run time.")]
        public string housePath = "";
        [Tooltip("0, 1, 2 ... within the house: floor by floor, and along each floor from one end to the other.")]
        public int index;
        [Tooltip("0 is the ground floor.")]
        public int floor;
        [Tooltip("A shop house's ground floor: the shop, closed by night.")]
        public bool shopFront;
        [Tooltip("Where the room's window is (world), for reports and for choosing rooms.")]
        public Vector3 centre;
        [Tooltip("The room's window panes, pushed out a few millimetres from the glass, in the house's space. " +
                 "Their UVs run across (u) and up (v) each pane, for the lit room's soft falloff.")]
        public Mesh panes;
        [Tooltip("The room's curtains, in the house's space. Null where the house has none at that window.")]
        public Mesh curtains;
    }

    [Serializable]
    public sealed class Cap
    {
        [Tooltip("What it covers, e.g. \"Old lamp (-17.1, 30.0)\": a night-only light of that name flickers with it.")]
        public string name = "";
        [Tooltip("The merged renderer it covers, by path, to find at run time.")]
        public string rendererPath = "";
        [Tooltip("That piece of the merged mesh, pushed out a little, in the renderer's space. Shown dark while its light is off.")]
        public Mesh mesh;
    }

    public Room[] rooms = Array.Empty<Room>();
    public Cap[] caps = Array.Empty<Cap>();
    [Tooltip("The merged \"Curtain glow\" renderers (by path) the rooms replace at night: hidden while the night runs.")]
    public string[] curtainPaths = Array.Empty<string>();
    [Tooltip("The merged \"Window glass\" renderer of each house with rooms (by path), for reports. A lit pane is a copy " +
             "of its house's glass material, glowing.")]
    public string[] glassPaths = Array.Empty<string>();
    [Tooltip("How many houses the rooms come from.")]
    public int houses;
}
