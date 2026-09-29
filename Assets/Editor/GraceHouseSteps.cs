#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// BREAK-INS, CHUNK A: GRACE'S HOUSE, INSIDE AND WALKABLE (claude/break-ins-spec.md sections 4, 8 and 12)
//
//   Fixit Fidget > Night > Break-ins 1 - Build Grace's house inside (door, window, rooms)
//   Fixit Fidget > Night > Break-ins 1 - Take Grace's house back out
//   Fixit Fidget > Night > Break-ins 2 - Check Grace's house (read-only, photos)
//   Fixit Fidget > Night > Break-ins 3 - Play the night at Grace's door (lab)
//   Fixit Fidget > Night > Break-ins 3 - Walk Grace's house by itself (lab, a check)
//
// Build (Edit Mode, with undo; save the scene afterwards):
//   * her front door is widened for a 1.0 m Ace (call 1): the doorway from 0.98 to 1.30 m, the cream surround
//     moved out 0.16 m each side (as wide as the stoop), the threshold and the stone foundation's cut to match,
//     the leaf stretched (its knob keeps its shape), its collider thinned to the leaf's back so the open door
//     leaves the doorway clear, and the dark hall put away;
//   * her front window becomes a real window: a hole in the wall and the clapboard behind it, the pane clear
//     glass, and its curtains a part of their own (the night's lit pane leaves her ground floor alone);
//   * the bay's two carved brackets over her door get a part of their own, solid only above the door's head
//     (the front one dips below it, where Ace's head passes);
//   * her rooms, from the plan's numbers (layout v2, revised for the real pieces on 29 Sept): floors, linings
//     and ceilings inside the shell, each wall in two parts at sill height so the part above can slide down in
//     the overhead view; the quarter-turn stairs with a smooth ramp over each flight; her furniture with
//     colliders; the bedroom's pair of doors folded back; her lamps (on at night). GraceHouse runs them.
// The new meshes go into Assets/Playtests/AcesCafeLayout/Grace's house.asset (the originals are kept: Take
// Grace's house back out puts them all back). Nothing here changes the other houses.
//
// Afterwards: Fixit Fidget > Night > Night walk 2 - Rebuild the list of what is solid by night, so the night's
// collision follows her wider door.
internal static class GraceHouseSteps
{
    const string Tag = "[Break-ins] ";
    const string Menu = "Fixit Fidget/Night/";
    const string HouseName = "1 - Saffron bay-window house";
    public const string RootName = "Grace's house - inside (break-ins)";
    const string AssetPath = "Assets/Playtests/AcesCafeLayout/Grace's house.asset";
    const string ModelFolder = "Assets/Art/Models/GraceHouse/";
    const string MaterialFolder = "Assets/Art/Materials/GraceHouse";
    const string Tag2 = " (Grace's house)";

    // ---- her house in plan metres (claude/break-ins-spec.md section 4; Tools/Blender/plan_v2.py) ----
    // X along West Street from the south wall (north is +X), Y from the back wall to the street, z up from the
    // ground floor. The house's own space is (2.71 - X, 0.15 + z, Y - 4.11); the rooms' root sits at (2.71, 0.15,
    // -4.11) in it, so the rooms' own space is (-X, z, Y).
    const float W = 5.42f, D = 4.02f, T = .08f;        // inside; the linings are 0.08 thick, 1 cm off the shell
    const float Up = 2.40f, Ceiling0 = 2.25f, Ceiling1 = 4.85f, CutAt = .78f;
    const float BedroomDoorWest = 2.35f, BedroomDoorEast = 3.75f;   // the bedroom's doorway (a pair of 0.70 m leaves)
    const float HeadroomFrom = 3.40f, HeadroomLift = .30f;          // over the foot of the stairs (see the floor in front of the stairwell)
    const string BoxName = "Grace's house - box (a wall, a floor, a ceiling)";
    const float DoorLeft = .46f, DoorRight = 1.76f, DoorHead = 2.19f;
    const float WindowLeft = 2.635f, WindowRight = 4.885f, WindowSill = .605f, WindowHead = 2.055f;
    const float Wider = .16f;                             // the doorway, each side
    static readonly Vector3 RootInHouse = new Vector3(2.71f, .15f, -4.11f);

    // Her door as the street builder made it (house space): the hinge edge, the far edge, the surround.
    const float OldLeft = 1.11f, OldRight = 2.09f, OldFrameLeft = .99f, OldFrameRight = 2.21f, OldLeaf = .98f;

    // ================================================================== menus

    [MenuItem(Menu + "Break-ins 1 - Build Grace's house inside (door, window, rooms)")]
    static void BuildMenu()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName);
            if (house == null) throw new InvalidOperationException($"'{HouseName}' isn't in the scene.");
            if (house.Find(RootName) != null) { Debug.Log(Tag + "Grace's house is already built inside. (Break-ins 1 - Take Grace's house back out undoes it.)"); return; }
            StreetDoor door = house.Find(StreetDoorSteps.DoorName) != null ? house.Find(StreetDoorSteps.DoorName).GetComponent<StreetDoor>() : null;
            if (door == null || door.hinge == null) throw new InvalidOperationException("Her house has no hinged front door yet (Fixit Fidget > Night > Doors 2 makes it).");
            Build(house, door, report);
            EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
            Debug.Log(Tag + "Grace's house is built inside. Next: Night walk 2 - Rebuild the list of what is solid by night; then Break-ins 2 - Check " +
                      "Grace's house; then save the scene (Ctrl+S).\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Building Grace's house FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 1 - Take Grace's house back out")]
    static void TakeOutMenu()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName);
            GraceHouse built = house != null && house.Find(RootName) != null ? house.Find(RootName).GetComponent<GraceHouse>() : null;
            if (built == null) { Debug.Log(Tag + "There is no Grace's house inside to take out."); return; }
            Undo.SetCurrentGroupName("Take Grace's house back out");
            int group = Undo.GetCurrentGroup();
            for (int i = 0; i < Mathf.Min(built.changedParts.Length, built.meshesBefore.Length); i++)
                if (built.changedParts[i] != null && built.meshesBefore[i] != null)
                {
                    Undo.RecordObject(built.changedParts[i], "Take Grace's house back out");
                    built.changedParts[i].sharedMesh = built.meshesBefore[i];
                }
            StreetDoor door = built.door;
            if (door != null)
            {
                Undo.RecordObject(door.transform, "Take Grace's house back out");
                door.transform.localPosition = built.doorBefore;
                BoxCollider leafBox = door.hinge != null ? door.hinge.GetComponentInChildren<BoxCollider>(true) : null;
                if (leafBox != null)
                {
                    Undo.RecordObject(leafBox, "Take Grace's house back out");
                    leafBox.center = built.leafColliderCentreBefore;
                    leafBox.size = built.leafColliderSizeBefore;
                }
            }
            for (int i = 0; i < Mathf.Min(built.movedMarkers.Length, built.markersBefore.Length); i++)
                if (built.movedMarkers[i] != null)
                {
                    Undo.RecordObject(built.movedMarkers[i], "Take Grace's house back out");
                    built.movedMarkers[i].localPosition = built.markersBefore[i];
                }
            foreach (GameObject hidden in built.hiddenInHouse)
                if (hidden != null) { Undo.RecordObject(hidden, "Take Grace's house back out"); hidden.SetActive(true); }
            foreach (GameObject added in built.addedToHouse)
                if (added != null) Undo.DestroyObjectImmediate(added);
            Undo.DestroyObjectImmediate(built.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
            Debug.Log(Tag + "Grace's house is taken back out: her door, window and shell are as the street builder and Doors 2 left them. " +
                      "Rebuild the list of what is solid by night (Night walk 2), then save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking Grace's house out FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Break-ins 1 - Build Grace's house inside (door, window, rooms)", true)]
    [MenuItem(Menu + "Break-ins 1 - Take Grace's house back out", true)]
    [MenuItem(Menu + "Break-ins 3 - Play the night at Grace's door (lab)", true)]
    [MenuItem(Menu + "Break-ins 3 - Walk Grace's house by itself (lab, a check)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the build

    sealed class Built
    {
        public Transform house, root, ground, first;
        public GraceHouse runner;
        public Mesh box;
        public readonly List<GraceHouse.Wall> walls = new();
        public readonly List<Renderer> ceilings = new();
        public readonly List<Light> lights = new();
        public readonly Dictionary<string, Material> mats = new();
        public readonly List<Object> meshes = new();
        public int pieces;
    }

    static void Build(Transform house, StreetDoor door, StringBuilder report)
    {
        // Everything is found and checked before anything changes.
        MeshFilter palette = Part(house, "Street palette"), stone = Part(house, "Sidewalk stone"), trim = Part(house, "Cream trim");
        MeshFilter glass = Part(house, "Window glass"), curtains = Part(house, "Curtain glow");
        MeshFilter leaf = door.hinge.GetComponentInChildren<MeshFilter>(true);
        BoxCollider leafBox = leaf != null ? leaf.GetComponent<BoxCollider>() : null;
        Transform hall = house.Find(StreetDoorSteps.HallName);
        if (leaf == null || leaf.sharedMesh == null || leafBox == null) throw new InvalidOperationException("Her door has no leaf with a collider.");
        if (Mathf.Abs(door.transform.localPosition.x - OldLeft) > .005f) throw new InvalidOperationException($"Her door's hinge is at x {door.transform.localPosition.x:0.000}, not {OldLeft} (already widened?).");
        Bounds leafBounds = leaf.sharedMesh.bounds;
        if (Mathf.Abs(leafBounds.size.x - OldLeaf) > .01f) throw new InvalidOperationException($"Her door leaf is {leafBounds.size.x:0.000} m wide, not {OldLeaf}.");
        foreach (MeshFilter f in new[] { palette, stone, trim, glass, curtains, leaf })
            if (!f.sharedMesh.isReadable) throw new InvalidOperationException($"{f.sharedMesh.name} can't be read; nothing was changed.");

        Undo.SetCurrentGroupName("Build Grace's house inside");
        int undoGroup = Undo.GetCurrentGroup();
        var b = new Built { house = house };
        MakeMaterials(b);
        // The unit box every wall, floor and ceiling is drawn with: the one an earlier build saved, if there is one.
        b.box = AssetDatabase.LoadMainAssetAtPath(AssetPath) is Mesh saved && saved.name == BoxName ? saved : BoxMesh();
        b.meshes.Add(b.box);

        // ---- her door, window and shell ----
        var changed = new List<MeshFilter>();
        var before = new List<Mesh>();
        void Swap(MeshFilter filter, Mesh mesh)
        {
            changed.Add(filter);
            before.Add(filter.sharedMesh);
            b.meshes.Add(mesh);
            Undo.RecordObject(filter, "Build Grace's house inside");
            filter.sharedMesh = mesh;
        }

        var added = new List<GameObject>();
        Surgery wall = new Surgery(palette.sharedMesh);
        WidenDoor(wall, report);
        CutWindow(wall, report);
        Swap(palette, wall.ToMesh(BaseName(palette.sharedMesh) + StreetDoorSteps.CutSuffix));

        Surgery foundation = new Surgery(stone.sharedMesh);
        int moved = foundation.MoveX(p => true, OldLeft, OldLeft - Wider) + foundation.MoveX(p => true, OldRight, OldRight + Wider);
        int cut = foundation.CutHole(1, .54f, 1, Rect.MinMaxRect(-2.71f, -4.11f, 2.71f, -.09f));
        report.AppendLine($"The stone foundation: {moved} corners of its front and the threshold moved out; its top cut away inside the house ({cut} face(s)).");
        Swap(stone, foundation.ToMesh(BaseName(stone.sharedMesh) + StreetDoorSteps.CutSuffix));

        Surgery surround = new Surgery(trim.sharedMesh);
        bool InFrame(Vector3 p) => p.x > .98f && p.x < 2.22f && p.y > .06f && p.y < 2.56f && p.z > -.001f && p.z < .201f;
        int frame = surround.MoveX(InFrame, OldFrameLeft, OldFrameLeft - Wider) + surround.MoveX(InFrame, OldLeft, OldLeft - Wider)
                    + surround.MoveX(InFrame, OldRight, OldRight + Wider) + surround.MoveX(InFrame, OldFrameRight, OldFrameRight + Wider);
        // The bay's two carved brackets over the door: a part of their own, solid only above the door's head.
        Surgery brackets = surround.Extract(t => Inside(t, new Bounds(new Vector3(1.35f, 2.41f, .37f), new Vector3(1.3f, .52f, .54f))));
        report.AppendLine($"The cream surround: {frame} corners moved out 0.16 m each side; the bay's brackets over the door: {brackets.Triangles} triangles, a part of their own.");
        Swap(trim, surround.ToMesh(BaseName(trim.sharedMesh) + StreetDoorSteps.CutSuffix));

        Surgery panes = new Surgery(glass.sharedMesh);
        Surgery pane = panes.Extract(t => Inside(t, new Bounds(new Vector3(-1.05f, 1.48f, .09f), new Vector3(2.3f, 1.5f, .08f))));
        if (pane.Triangles != 12) throw new InvalidOperationException($"Her front window's glass wasn't found as one pane ({pane.Triangles} triangles).");
        Swap(glass, panes.ToMesh(BaseName(glass.sharedMesh) + StreetDoorSteps.CutSuffix));

        Surgery drapes = new Surgery(curtains.sharedMesh);
        Surgery front = drapes.Extract(t => Inside(t, new Bounds(new Vector3(-1.05f, 1.48f, .12f), new Vector3(2.1f, 1.45f, .05f))));
        if (front.Triangles != 24) throw new InvalidOperationException($"Her front window's curtains weren't found as two ({front.Triangles} triangles).");
        Swap(curtains, drapes.ToMesh(BaseName(curtains.sharedMesh) + StreetDoorSteps.CutSuffix));

        added.Add(PartOfHouse(house, "Front window (clear glass)", pane.ToMesh(HouseName + " - front window (clear glass)"), new[] { b.mats["Glass"] }, false, b));
        added.Add(PartOfHouse(house, "Front window curtains", front.ToMesh(HouseName + " - front window curtains"), curtains.GetComponent<Renderer>().sharedMaterials, false, b));
        GameObject bracketPart = PartOfHouse(house, GraceHouse.BracketsName, brackets.ToMesh(HouseName + " - bay brackets over the door"),
                                             trim.GetComponent<Renderer>().sharedMaterials, false, b);
        var bracketBox = bracketPart.AddComponent<BoxCollider>();
        Bounds bb = bracketPart.GetComponent<MeshFilter>().sharedMesh.bounds;
        float headTop = 2.35f;
        bracketBox.center = new Vector3(bb.center.x, (headTop + bb.max.y) * .5f, bb.center.z);
        bracketBox.size = new Vector3(bb.size.x, bb.max.y - headTop, bb.size.z);
        added.Add(bracketPart);

        // The leaf: stretched to the new width about its hinge; the knob moves with its edge and keeps its shape.
        Surgery door2 = new Surgery(leaf.sharedMesh);
        float stretch = (OldLeaf + 2f * Wider) / OldLeaf;
        var knob = new Bounds(new Vector3(.81f, 1.04f, .17f), new Vector3(.1f, .1f, .1f));
        door2.Reshape(p => knob.Contains(p) ? p + new Vector3(2f * Wider, 0f, 0f) : new Vector3(p.x * stretch, p.y, p.z), p => !knob.Contains(p));
        Swap(leaf, door2.ToMesh(leaf.sharedMesh.name + Tag2));
        Undo.RecordObject(leafBox, "Build Grace's house inside");
        Vector3 colliderCentreBefore = leafBox.center, colliderSizeBefore = leafBox.size;
        // Only the leaf's back 5 cm is solid: opened into the hall, the leaf's full depth (its panels and knob stand
        // 0.2 m proud of its back) would take that much off the doorway, and a 1.0 m Ace needs 1.16 m to pass.
        leafBox.size = new Vector3(OldLeaf + 2f * Wider, colliderSizeBefore.y, .05f);
        leafBox.center = new Vector3((OldLeaf + 2f * Wider) * .5f, colliderCentreBefore.y, .025f);

        // The door moves 0.16 m toward its hinge side; its markers stay where they were (the doorway's middle
        // hasn't moved), except the hall, which is now at the foot of her stairs, clear of the door's swing.
        var markers = new List<Transform>();
        var markersBefore = new List<Vector3>();
        Undo.RecordObject(door.transform, "Build Grace's house inside");
        Vector3 doorBefore = door.transform.localPosition;
        door.transform.localPosition = doorBefore - new Vector3(Wider, 0f, 0f);
        foreach (Transform child in door.transform)
        {
            if (child == door.hinge) continue;
            markers.Add(child);
            markersBefore.Add(child.localPosition);
            Undo.RecordObject(child, "Build Grace's house inside");
            child.localPosition += new Vector3(Wider, 0f, 0f);
        }
        Transform hallMarker = door.hall;
        if (hallMarker != null)
        {
            // Plan (0.95, 2.95): in front of the bottom step, between the newel and the door's swing.
            Vector3 hallInHouse = HouseSpace(.95f, 2.95f, 0f);
            hallMarker.localPosition = door.transform.InverseTransformPoint(house.TransformPoint(hallInHouse));
        }
        var hidden = new List<GameObject>();
        if (hall != null) { Undo.RecordObject(hall.gameObject, "Build Grace's house inside"); hall.gameObject.SetActive(false); hidden.Add(hall.gameObject); }
        report.AppendLine($"Her door: 1.30 m (from 0.98), hinge {doorBefore.x:0.000} -> {door.transform.localPosition.x:0.000}; the leaf stretched x{stretch:0.000}, " +
                          $"its knob moved with its edge; its collider {leafBox.size.x:0.00} x {leafBox.size.y:0.00} x {leafBox.size.z:0.00}; the dark hall put away; " +
                          $"{markers.Count} markers kept in place, the hall marker at the foot of her stairs.");

        // ---- her rooms ----
        BuildRooms(b, report);
        b.runner.house = house;
        b.runner.door = door;
        b.runner.changedParts = changed.ToArray();
        b.runner.meshesBefore = before.ToArray();
        b.runner.addedToHouse = added.ToArray();
        b.runner.hiddenInHouse = hidden.ToArray();
        b.runner.movedMarkers = markers.ToArray();
        b.runner.markersBefore = markersBefore.ToArray();
        b.runner.doorBefore = doorBefore;
        b.runner.leafColliderCentreBefore = colliderCentreBefore;
        b.runner.leafColliderSizeBefore = colliderSizeBefore;
        EditorUtility.SetDirty(b.runner);

        SaveMeshes(b.meshes, report);
        Undo.CollapseUndoOperations(undoGroup);
    }

    // The palette: the doorway's edges out 0.16 m each side (the front wall and the clapboard lips across it).
    static void WidenDoor(Surgery wall, StringBuilder report)
    {
        int edges = wall.MoveX(p => Mathf.Abs(p.z) < .001f, OldLeft, OldLeft - Wider) + wall.MoveX(p => Mathf.Abs(p.z) < .001f, OldRight, OldRight + Wider);
        // The lips (0.035 tall, from 0.8 m up every 0.25 m) stop 1 cm short of the doorway.
        int lips = wall.MoveX(IsLip, OldLeft - .01f, OldLeft - .01f - Wider) + wall.MoveX(IsLip, OldRight + .01f, OldRight + .01f + Wider);
        report.AppendLine($"The front wall: the doorway's edges moved out ({edges} corners), the clapboard lips' ends with them ({lips} corners).");
    }

    static bool IsLip(Vector3 p)
    {
        if (p.z < -.001f || p.z > .051f) return false;
        for (int k = 0; k < 7; k++)
        {
            float y = .8f + .25f * k;
            if (Mathf.Abs(p.y - (y - .0175f)) < .002f || Mathf.Abs(p.y - (y + .0175f)) < .002f) return true;
        }
        return false;
    }

    // The ground floor's window: a hole in the front wall behind the glass, and through the six lips across it.
    static void CutWindow(Surgery wall, StringBuilder report)
    {
        // The glass in house space: x -2.175..0.075, y 0.755..2.205 (the trim overlaps its edges from the front).
        const float x0 = -2.175f, x1 = .075f, y0 = .755f, y1 = 2.205f;
        int faces = wall.CutHole(2, 0f, 1, Rect.MinMaxRect(x0, y0, x1, y1));
        int lipFaces = 0, caps = 0;
        for (int k = 0; k < 6; k++)
        {
            float y = .8f + .25f * k, lo = y - .0175f, hi = y + .0175f;
            lipFaces += wall.CutHole(2, .05f, 1, Rect.MinMaxRect(x0, lo - .01f, x1, hi + .01f));
            lipFaces += wall.CutHole(1, hi, 1, Rect.MinMaxRect(x0, -.01f, x1, .06f));
            lipFaces += wall.CutHole(1, lo, -1, Rect.MinMaxRect(x0, -.01f, x1, .06f));
            // Their new ends, mapped like the lip's end by the doorway.
            caps += wall.AddCap(0, x0, 1, new Rect(0f, lo, .05f, hi - lo), p => IsLip(p) && Mathf.Abs(p.x - (OldLeft - .01f - Wider)) < .002f);
            caps += wall.AddCap(0, x1, -1, new Rect(0f, lo, .05f, hi - lo), p => IsLip(p) && Mathf.Abs(p.x - (OldLeft - .01f - Wider)) < .002f);
        }
        report.AppendLine($"Her front window: the wall cut behind the glass ({faces} face(s)), the six lips across it ({lipFaces} faces, {caps} new ends).");
    }

    static string BaseName(Mesh mesh)
    {
        int at = mesh.name.IndexOf(StreetDoorSteps.CutSuffix, StringComparison.Ordinal);
        return at >= 0 ? mesh.name.Substring(0, at) : mesh.name;
    }

    static MeshFilter Part(Transform house, string name)
    {
        Transform t = house.Find(name);
        MeshFilter f = t != null ? t.GetComponent<MeshFilter>() : null;
        if (f == null || f.sharedMesh == null) throw new InvalidOperationException($"Her house has no '{name}' mesh.");
        return f;
    }

    static bool Inside((Vector3 a, Vector3 b, Vector3 c) t, Bounds box) => box.Contains(t.a) && box.Contains(t.b) && box.Contains(t.c);

    // A new child of the house (in the house's own space), with its mesh and materials.
    static GameObject PartOfHouse(Transform house, string name, Mesh mesh, Material[] materials, bool shadows, Built b)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(house, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = materials;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        b.meshes.Add(mesh);
        return go;
    }

    static Vector3 HouseSpace(float X, float Y, float z) => new Vector3(RootInHouse.x - X, RootInHouse.y + z, Y + RootInHouse.z);

    // ================================================================== the rooms

    static void BuildRooms(Built b, StringBuilder report)
    {
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Grace's house inside");
        root.transform.SetParent(b.house, false);
        root.transform.localPosition = RootInHouse;
        b.root = root.transform;
        b.runner = root.AddComponent<GraceHouse>();
        b.ground = Group(b.root, "Ground floor");
        b.first = Group(b.root, "First floor");

        Material plaster = b.mats["Plaster"], sage = b.mats["Plaster (bedroom)"], boards = b.mats["Boards"], tiles = b.mats["Tiles"];
        Material ceiling = b.mats["Ceiling"], bath = b.mats["Bathroom cap"], glass = b.mats["Glass"];
        const float xs = -T, xn = W + T, yb = -T, yf = D + T;   // the linings' outer faces

        // ---- the ground floor ----
        Transform g = Group(b.ground, "Floor, walls and stairs");
        Box(b, g, "Floor", xs, xn, yb, yf, -.10f, 0f, boards);
        Box(b, g, "Kitchen floor", 2.60f, W, 0f, 2.06f, -.01f, .002f, tiles);
        Wall(b, g, 0, "Back wall", xs, xn, yb, 0f, 0f, Up, plaster, new Vector3(0f, 0f, -1f));
        Wall(b, g, 0, "South wall", xs, 0f, 0f, D, 0f, Up, plaster, new Vector3(1f, 0f, 0f));
        Wall(b, g, 0, "North wall (next door)", W, xn, 0f, D, 0f, Up, plaster, new Vector3(-1f, 0f, 0f));
        var street = new Vector3(0f, 0f, 1f);
        Wall(b, g, 0, "Street wall, south of the door", xs, DoorLeft, D, yf, 0f, Up, plaster, street);
        Wall(b, g, 0, "Street wall, over the door", DoorLeft, DoorRight, D, yf, DoorHead, Up, plaster, street);
        Wall(b, g, 0, "Street wall, by the window", DoorRight, WindowLeft, D, yf, 0f, Up, plaster, street);
        Wall(b, g, 0, "Street wall, under the window", WindowLeft, WindowRight, D, yf, 0f, WindowSill, plaster, street);
        Wall(b, g, 0, "Street wall, over the window", WindowLeft, WindowRight, D, yf, WindowHead, Up, plaster, street);
        Wall(b, g, 0, "Street wall, north of the window", WindowRight, xn, D, yf, 0f, Up, plaster, street);
        Stairs(b, g, report);

        Transform kitchen = Group(b.ground, "Kitchen");
        Piece(b, kitchen, "GH_KitchenCounter_Short", 3.62f, .31f, 0f, 0f);
        Transform cupboards = Piece(b, kitchen, "GH_WallCupboards", 3.64f, 0f, 1.45f, 0f);
        Piece(b, kitchen, "GH_Fridge", 5.03f, .345f, 0f, 0f);
        Piece(b, kitchen, "GH_Kettle", 3.385f, .42f, .926f, 15f);
        Piece(b, kitchen, "GH_Teapot", 3.665f, .21f, .926f, -25f);
        Piece(b, kitchen, "GH_CupBox", 2.93f, .345f, .92f, 90f);   // the reunion cups, on the worktop by the stairs

        Transform lounge = Group(b.ground, "Front room");
        Piece(b, lounge, "GH_Rug", 3.95f, 2.90f, 0f, 0f);
        Piece(b, lounge, "GH_TVCabinet", 5.175f, 2.83f, 0f, -90f);
        Piece(b, lounge, "GH_TV", 5.175f, 2.83f, .50f, -90f);
        Piece(b, lounge, "GH_Armchair", 4.00f, 2.83f, 0f, 90f);
        Piece(b, lounge, "GH_StandardLamp", 3.25f, 3.55f, 0f, 0f);
        Piece(b, lounge, "GH_Sideboard", 3.60f, 3.765f, 0f, 180f);
        Piece(b, lounge, "GH_Frame_S", 3.20f, 3.80f, .865f, 170f);
        Piece(b, lounge, "GH_Frame_M", 3.95f, 3.78f, .865f, 186f);
        Piece(b, lounge, "GH_CoatStand", 5.12f, 3.72f, 0f, -90f);   // in the corner by the TV: by the door it was in a 1.0 m Ace's way
        Transform tvFrame = Piece(b, lounge, "GH_Frame_L", W, 2.83f, 1.35f, -90f);
        Transform stairsFrame = Piece(b, g, "GH_Frame_XL", 0f, 2.00f, 1.45f, 90f);
        Hang(b, "Back wall", cupboards);
        Hang(b, "North wall (next door)", tvFrame);
        Hang(b, "South wall", stairsFrame);
        b.lights.Add(Lamp(b.ground, "Her standard lamp (night)", 3.25f, 3.55f, 1.40f, new Color(1f, .78f, .52f), 1.6f, 4.5f));

        // ---- the first floor ----
        Transform f1 = Group(b.first, "Floor, walls and ceiling");
        float s0 = Ceiling0, s1 = Up;
        foreach (var (name, x0, x1, y0, y1) in new[]
                 {
                     ("Floor over the front room and kitchen", 1.46f, xn, 1.46f, yf),
                     ("Floor, the landing and the bathroom", 2.58f, xn, yb, 1.46f),
                     ("Floor in front of the stairwell", xs, 1.46f, 2.90f, yf),
                 })
        {
            GameObject slab = Box(b, f1, name, x0, x1, y0, y1, s0 + .01f, s1, boards);
            GameObject under = Box(b, f1, name + " (the ceiling below it)", x0, x1, y0, y1, s0, s0 + .01f, ceiling);
            if (y0 == 2.90f)
            {
                // Headroom over the foot of her stairs (29 Sept, the first walk check): a 1.16 m capsule climbing the
                // lower flight is still half under this floor's edge after 0.45 m of rise, so the floor over the
                // entry is solid only from Y 3.40 (Ace upstairs can't get nearer the stairwell than Y 3.53 anyway).
                SolidOnly(slab, x0, x1, HeadroomFrom, y1, s0 + .01f, s1);
                SolidOnly(under, x0, x1, HeadroomFrom, y1, s0, s0 + .01f);
            }
        }
        float top = Ceiling1;
        Wall(b, f1, 1, "Back wall (upstairs)", xs, xn, yb, 0f, Up, top, sage, new Vector3(0f, 0f, -1f));
        Wall(b, f1, 1, "South wall (upstairs)", xs, 0f, 0f, D, Up, top, sage, new Vector3(1f, 0f, 0f));
        Wall(b, f1, 1, "North wall (upstairs)", W, xn, 0f, D, Up, top, sage, new Vector3(-1f, 0f, 0f));
        // The street wall, open where the bays are (their linings: 4 cm, 1 cm off the shell's 0.61-2.11 and 3.31-4.81).
        const float bayTop = 4.35f, bayFront = 4.86f;
        (float x0, float x1)[] bays = { (.62f, 2.10f), (3.35f, 4.77f) };
        Wall(b, f1, 1, "Street wall (upstairs), south end", xs, bays[0].x0, D, yf, Up, top, sage, street);
        Wall(b, f1, 1, "Street wall (upstairs), over the south bay", bays[0].x0, bays[0].x1, D, yf, bayTop, top, sage, street);
        Wall(b, f1, 1, "Street wall (upstairs), between the bays", bays[0].x1, bays[1].x0, D, yf, Up, top, sage, street);
        Wall(b, f1, 1, "Street wall (upstairs), over the north bay", bays[1].x0, bays[1].x1, D, yf, bayTop, top, sage, street);
        Wall(b, f1, 1, "Street wall (upstairs), north end", bays[1].x1, xn, D, yf, Up, top, sage, street);
        string[] bayNames = { "south bay", "north bay" };
        for (int i = 0; i < 2; i++)
        {
            var (x0, x1) = bays[i];
            string n = bayNames[i];
            Box(b, f1, $"Floor in the {n}", x0, x1, D, bayFront + .04f, s0 + .01f, s1, boards);
            Wall(b, f1, 1, $"The {n}, south side", x0, x0 + .04f, D, bayFront + .04f, Up, bayTop, sage, new Vector3(1f, 0f, 0f));
            Wall(b, f1, 1, $"The {n}, north side", x1 - .04f, x1, D, bayFront + .04f, Up, bayTop, sage, new Vector3(-1f, 0f, 0f));
            Wall(b, f1, 1, $"The {n}, under its window", x0, x1, bayFront, bayFront + .04f, Up, Up + .10f, sage, street);
            Wall(b, f1, 1, $"The {n}, over its window", x0, x1, bayFront, bayFront + .04f, 4.15f, bayTop, sage, street);
            Box(b, f1, $"The {n}'s window", x0 + .04f, x1 - .04f, bayFront + .005f, bayFront + .015f, Up + .10f, 4.15f, glass)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            b.ceilings.Add(Box(b, f1, $"The {n}'s ceiling", x0, x1, D, bayFront + .04f, bayTop, bayTop + .05f, ceiling).GetComponent<Renderer>());
        }
        // Inside walls: the bedroom's back wall either side of its door, over it, round the stairwell, the bathroom.
        // The doorway is X 2.35-3.75 (moved 0.25 m west of the drawn 2.60-4.00 on 29 Sept, after the first build's
        // check: between the west leaf's hinge and the foot of the bed there were 1.21 m for Ace's 1.16; now 1.39).
        Wall(b, f1, 1, "Bedroom wall, west of the door", 1.46f, BedroomDoorWest, 1.46f, 1.56f, Up, top, sage, Vector3.zero);
        Wall(b, f1, 1, "Bedroom wall, east of the door", BedroomDoorEast, W, 1.46f, 1.56f, Up, top, sage, Vector3.zero);
        Wall(b, f1, 1, "Over the bedroom door", BedroomDoorWest, BedroomDoorEast, 1.46f, 1.56f, Up + 2.20f, top, sage, Vector3.zero);
        Wall(b, f1, 1, "Along the stairwell", 1.41f, 1.46f, 1.46f, 2.95f, Up, top, sage, Vector3.zero);
        Wall(b, f1, 1, "Stairwell front", 0f, 1.46f, 2.90f, 2.95f, Up, top, sage, Vector3.zero);
        // It stands over the foot of her stairs: solid only from 0.30 above the floor up here, so a capsule climbing
        // below passes under it (Ace upstairs, 0.58 round at 0.58 up, still can't get past it into the stairwell).
        SolidOnly(b.walls.Last().lower.gameObject, 0f, 1.46f, 2.90f, 2.95f, Up + HeadroomLift, top);
        Wall(b, f1, 1, "Bathroom wall, back end", 4.00f, 4.10f, 0f, .32f, Up, top, plaster, Vector3.zero);
        Wall(b, f1, 1, "Bathroom wall, over its door", 4.00f, 4.10f, .32f, 1.14f, Up + 2.03f, top, plaster, Vector3.zero);
        Wall(b, f1, 1, "Bathroom wall, front end", 4.00f, 4.10f, 1.14f, 1.46f, Up, top, plaster, Vector3.zero);
        // Ceilings: hidden in the overhead view while Ace is inside; the bathroom's stays (a closed room).
        b.ceilings.Add(Box(b, f1, "Ceiling upstairs", xs, 4.10f, yb, yf, top, top + .05f, ceiling).GetComponent<Renderer>());
        b.ceilings.Add(Box(b, f1, "Ceiling upstairs, over the bed", 4.10f, xn, 1.46f, yf, top, top + .05f, ceiling).GetComponent<Renderer>());
        Box(b, f1, "Over the bathroom (it stays shut)", 4.10f, xn, yb, 1.46f, top, top + .05f, bath);

        Transform bedroom = Group(b.first, "Bedroom");
        // Revised for the real pieces (29 Sept): a 1.35 x 1.90 bed against the street wall's north end, one bedside
        // table (behind it, with the lamp), the wardrobe in the corner south of the stairwell, both doors folded back.
        const float bedY = 3.283f, tableY = 2.306f;
        Piece(b, bedroom, "GH_Bed", 4.41f, bedY, Up, -90f);
        Piece(b, bedroom, "GH_Quilt_Made", 4.41f, bedY, Up, -90f, collider: false);
        Piece(b, bedroom, "GH_BedsideTable", 5.195f, tableY, Up, -90f);
        Piece(b, bedroom, "GH_BedsideLamp", 5.195f, tableY, Up + .55f, 0f);
        RaiseBottom(Piece(b, bedroom, "GH_Wardrobe", .305f, 3.485f, Up, 90f), HeadroomLift);   // it too stands over the foot of the stairs
        Piece(b, bedroom, "GH_DressingTable", 1.36f, 4.49f, Up, 180f);
        Piece(b, bedroom, "GH_Rug", 2.52f, 3.20f, Up, 0f);
        Piece(b, bedroom, "GH_BedroomDoor_Frame", (BedroomDoorWest + BedroomDoorEast) * .5f, 1.51f, Up, 0f, collider: false);
        // The pair of doors, folded back against the wall either side (they close in chunk C).
        Leaf(b, bedroom, "Bedroom door, west leaf", BedroomDoorWest, 0f, 175f);
        Leaf(b, bedroom, "Bedroom door, east leaf", BedroomDoorEast, 180f, 5f);
        Piece(b, bedroom, "GH_InteriorDoor_Frame", 4.05f, .73f, Up, 90f, collider: false);
        Piece(b, bedroom, "GH_InteriorDoor_Leaf", 4.05f, 1.13f, Up, 90f);
        // Her photos (placeholder canon: her in none of them), mostly up here.
        Transform photos = Group(b.first, "Her photos");
        Hang(b, "Along the stairwell", Piece(b, photos, "GH_Frame_L", 1.46f, 2.10f, Up + 1.30f, 90f));
        Hang(b, "Along the stairwell", Piece(b, photos, "GH_Frame_L", 1.46f, 2.62f, Up + 1.40f, 90f));
        Hang(b, "Street wall (upstairs), between the bays", Piece(b, photos, "GH_Frame_XL", 2.72f, D, Up + 1.25f, 180f));
        Hang(b, "Stairwell front", Piece(b, photos, "GH_Frame_L", .95f, 2.95f, Up + 1.30f, 0f));
        Piece(b, photos, "GH_Frame_S", .92f, 4.36f, Up + .76f, 172f);
        Piece(b, photos, "GH_Frame_M", 1.80f, 4.36f, Up + .76f, 190f);
        b.lights.Add(Lamp(b.first, "Her bedside lamp (night)", 5.195f, tableY, Up + .85f, new Color(1f, .76f, .5f), 1.4f, 3.5f));
        b.lights.Add(Lamp(b.first, "The landing light (night)", 3.30f, .73f, Up + 2.25f, new Color(1f, .85f, .65f), .8f, 3f));

        GraceHouse runner = b.runner;
        runner.groundFloor = b.ground;
        runner.firstFloor = b.first;
        runner.walls = b.walls.ToArray();
        runner.ceilings = b.ceilings.ToArray();
        runner.nightLights = b.lights.ToArray();
        runner.firstFloorAt = Up;
        report.AppendLine($"Her rooms: {b.pieces} pieces, {b.walls.Count} walls in two parts at sill height ({b.walls.Count(w => w.upper != null)} can slide down), " +
                          $"{b.ceilings.Count} ceilings hidden in the overhead view, {b.lights.Count} lamps (night only).");
    }

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // A box in plan metres: X0..X1 (north), Y0..Y1 (to the street), z0..z1 (up), in the rooms' space (-X, z, Y).
    static GameObject Box(Built b, Transform parent, string name, float X0, float X1, float Y0, float Y1, float z0, float z1, Material m)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-(X0 + X1) * .5f, (z0 + z1) * .5f, (Y0 + Y1) * .5f);
        go.transform.localScale = new Vector3(Mathf.Abs(X1 - X0), Mathf.Abs(z1 - z0), Mathf.Abs(Y1 - Y0));
        go.AddComponent<MeshFilter>().sharedMesh = b.box;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        // Solid, with a collider of its own, so the night's list of what is solid leaves it alone: her rooms bring
        // their own collision.
        go.AddComponent<BoxCollider>();
        b.pieces++;
        return go;
    }

    // Keep a box's collider to part of it (plan metres, as Box takes them); the box is still drawn whole.
    static void SolidOnly(GameObject box, float X0, float X1, float Y0, float Y1, float z0, float z1)
    {
        Transform t = box.transform;
        Vector3 p = t.localPosition, s = t.localScale;
        Vector3 a = new Vector3((-X1 - p.x) / s.x, (z0 - p.y) / s.y, (Y0 - p.z) / s.z);
        Vector3 c = new Vector3((-X0 - p.x) / s.x, (z1 - p.y) / s.y, (Y1 - p.z) / s.z);
        var col = box.GetComponent<BoxCollider>();
        col.center = (a + c) * .5f;
        col.size = new Vector3(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y), Mathf.Abs(c.z - a.z));
    }

    // Lift the bottom of a piece's collider by some metres (its top stays), along whichever of its axes is up.
    static void RaiseBottom(Transform piece, float by)
    {
        var col = piece.GetComponent<BoxCollider>();
        Vector3 up = piece.InverseTransformDirection(piece.parent != null ? piece.parent.up : Vector3.up);
        int axis = Mathf.Abs(up.x) > .9f ? 0 : Mathf.Abs(up.y) > .9f ? 1 : 2;
        float sign = Mathf.Sign(up[axis]);
        Vector3 size = col.size, centre = col.center;
        size[axis] -= by;
        centre[axis] += sign * by * .5f;
        col.size = size;
        col.center = centre;
    }

    // A wall in two parts: up to the cut (sill height above its floor) always drawn, the rest can slide down.
    // The lower part's collider covers the whole wall, so Ace never finds a gap while the top is down.
    static void Wall(Built b, Transform parent, int storey, string name, float X0, float X1, float Y0, float Y1, float z0, float z1, Material m, Vector3 outward)
    {
        float cut = (storey == 0 ? 0f : Up) + CutAt;
        var wall = new GraceHouse.Wall { name = name, storey = storey, perimeter = outward != Vector3.zero, outward = outward };
        if (z0 < cut - .001f)
        {
            float lowTop = Mathf.Min(cut, z1);
            GameObject low = Box(b, parent, z1 > cut ? name + " (to sill height)" : name, X0, X1, Y0, Y1, z0, lowTop, m);
            wall.lower = low.GetComponent<Renderer>();
            if (z1 > cut)
            {
                var c = low.GetComponent<BoxCollider>();
                float whole = (z1 - z0) / (lowTop - z0);
                c.size = new Vector3(1f, whole, 1f);
                c.center = new Vector3(0f, -.5f + whole * .5f, 0f);
            }
        }
        if (z1 > cut + .001f)
        {
            GameObject high = Box(b, parent, z0 < cut ? name + " (above the sill)" : name, X0, X1, Y0, Y1, Mathf.Max(z0, cut), z1, m);
            wall.upper = high.transform;
        }
        b.walls.Add(wall);   // those with nothing above the cut stay in the list, so things hung on them can be found
    }

    static void Hang(Built b, string wallName, Transform thing)
    {
        GraceHouse.Wall wall = b.walls.FirstOrDefault(w => w.name == wallName);
        if (wall == null) throw new InvalidOperationException($"No wall called '{wallName}' to hang {thing.name} on.");
        wall.hanging = wall.hanging.Concat(thing.GetComponentsInChildren<Renderer>(true)).ToArray();
    }

    // One of her pieces, placed by plan (X, Y, z, turn): turn 0 faces the street, 90 north, -90 south, 180 the back.
    static Transform Piece(Built b, Transform parent, string model, float X, float Y, float z, float turn, bool collider = true)
    {
        string path = ModelFolder + model + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException($"{path} isn't there (Break-ins - Grace's furniture: import and check).");
        MeshFilter source = asset.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null);
        if (source == null) throw new InvalidOperationException($"{path} has no mesh.");
        Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        var go = new GameObject(model);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(parent, false);
        Quaternion yaw = Quaternion.Euler(0f, -turn, 0f);
        go.transform.localPosition = new Vector3(-X, z, Y) + yaw * (Vector3)toRoot.GetColumn(3);
        go.transform.localRotation = yaw * toRoot.rotation;
        go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
        if (collider)
        {
            var c = go.AddComponent<BoxCollider>();
            Bounds m = source.sharedMesh.bounds;
            c.center = m.center;
            c.size = Vector3.Max(m.size, new Vector3(.01f, .01f, .01f));
        }
        // Without a collider of its own, a piece low enough to touch Ace gets exact collision by night from the night's
        // list (Night walk 2); by day nothing walks in here.
        b.pieces++;
        return go.transform;
    }

    // One of the bedroom's leaves: its hinge at the jamb, 6 cm into the bedroom off the wall's face (Y 1.56); closed
    // along the doorway at closedYaw, turned by openBy into the room (folded back against the wall). Only the leaf
    // itself is solid, not its knobs: folded back, the leaf's room side is the edge of the way in past the bed.
    static void Leaf(Built b, Transform parent, string name, float hingeX, float closedYaw, float openBy)
    {
        var hinge = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(hinge, "Build Grace's house inside");
        hinge.transform.SetParent(parent, false);
        hinge.transform.localPosition = new Vector3(-hingeX, Up, 1.62f);
        float open = closedYaw == 0f ? closedYaw + openBy : closedYaw - (180f - openBy);
        hinge.transform.localRotation = Quaternion.Euler(0f, open, 0f);
        Transform leaf = Piece(b, hinge.transform, "GH_BedroomDoor_Leaf", 0f, 0f, 0f, 0f);
        leaf.localPosition = Vector3.zero;
        leaf.localRotation = Quaternion.identity;
        var box = leaf.GetComponent<BoxCollider>();
        box.center = new Vector3(box.center.x, box.center.y, 0f);
        box.size = new Vector3(box.size.x, box.size.y, .06f);
    }

    static Light Lamp(Transform parent, string name, float X, float Y, float z, Color colour, float intensity, float range)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-X, z, Y);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = colour;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.enabled = false;   // on at night (GraceHouse)
        return l;
    }

    // ================================================================== her stairs

    // GH_Stairs_L is built in plan metres from the back-south corner, so it sits at the rooms' origin, unturned.
    // Its collision: the landing and the cupboard under the upper flight (a block, on the stairs themselves, so the
    // night's list leaves the stairs alone), a smooth ramp along the line of the nosings over each flight (39.8°,
    // under the capsule's 45° slope limit), and the banisters' sides.
    static void Stairs(Built b, Transform parent, StringBuilder report)
    {
        Transform stairs = Piece(b, parent, "GH_Stairs_L", 0f, 0f, 0f, 0f, collider: false);
        var block = stairs.gameObject.AddComponent<BoxCollider>();
        // Its front stops at Y 1.42, where the lower flight's ramp reaches the landing's 1.2: no 3 cm lip there (the
        // cupboard's front, Y 1.40-1.46, is closed by its own side below).
        block.center = new Vector3(-1.30f, .60f, .71f);
        block.size = new Vector3(2.60f, 1.20f, 1.42f);
        const float t = .30f;
        // The lower flight: from the floor at Y 2.86 up to the landing at Y 1.42 (z 1.2), across X 0..1.40.
        Ramp(stairs, "Lower flight (ramp)", new Vector3(-.70f, 0f, 2.86f), new Vector3(-.70f, 1.20f, 1.42f), 1.40f, t);
        // The upper flight: from the landing at X 1.14 (z 1.2) up to the first floor at X 2.58 (z 2.4), across Y 0..1.40.
        Ramp(stairs, "Upper flight (ramp)", new Vector3(-1.14f, 1.20f, .70f), new Vector3(-2.58f, 2.40f, .70f), 1.40f, t);
        // The lower flight's open side (its banister and newel) and the upper flight's (the cupboard's front): as
        // high as the ceiling, so nothing leans over them.
        Side(stairs, "Lower flight's banister", 1.40f, 1.475f, 1.40f, 2.68f, 0f, Ceiling0);
        Side(stairs, "Upper flight's side (the cupboard front)", 1.40f, 2.60f, 1.40f, 1.46f, 0f, Up);
        report.AppendLine("Her stairs: the landing and cupboard solid, a ramp over each flight (39.8°), both open sides closed to the ceiling.");
    }

    static void Ramp(Transform stairs, string name, Vector3 low, Vector3 high, float across, float thickness)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(stairs, false);
        Vector3 down = (low - high).normalized;                   // along the flight, downhill
        Vector3 side = Vector3.Cross(Vector3.up, down).normalized;
        Vector3 normal = Vector3.Cross(down, side).normalized;   // up, and toward the foot of the flight
        if (normal.y < 0f) { normal = -normal; side = -side; }
        go.transform.localRotation = Quaternion.LookRotation(down, normal);
        go.transform.localPosition = (low + high) * .5f - normal * (thickness * .5f);
        var c = go.AddComponent<BoxCollider>();
        c.size = new Vector3(across, thickness, (high - low).magnitude);
    }

    static void Side(Transform stairs, string name, float X0, float X1, float Y0, float Y1, float z0, float z1)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Grace's house inside");
        go.transform.SetParent(stairs, false);
        go.transform.localPosition = new Vector3(-(X0 + X1) * .5f, (z0 + z1) * .5f, (Y0 + Y1) * .5f);
        var c = go.AddComponent<BoxCollider>();
        c.size = new Vector3(X1 - X0, z1 - z0, Y1 - Y0);
    }

    // ================================================================== materials and the box

    static void MakeMaterials(Built b)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader wasn't found.");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "GraceHouse");
        foreach (var (key, file, hex, smoothness) in new[]
                 {
                     ("Plaster", "GH_Room_Plaster", "EDE3D1", .12f),
                     ("Plaster (bedroom)", "GH_Room_Plaster_Bedroom", "E2E6D6", .12f),
                     ("Boards", "GH_Room_Boards", "8A6746", .20f),
                     ("Tiles", "GH_Room_Tiles", "D7CFBD", .30f),
                     ("Ceiling", "GH_Room_Ceiling", "F4EFE6", .05f),
                     ("Bathroom cap", "GH_Room_Closed", "B9B2A6", .05f),
                 })
        {
            // Indoors: no reflections of the sky (the first build's brown boards came out lavender, the sky in them).
            b.mats[key] = Lit(lit, file, hex, smoothness, reflections: false);
        }
        // Clear glass, for her front window and her bays from inside: the café's own window glass if it can be found.
        Renderer cafeGlass = CityPackChecks.InScene<Renderer>().FirstOrDefault(r => r.name == "Courtyard window glass" && r.sharedMaterial != null);
        Material glass = cafeGlass != null ? cafeGlass.sharedMaterial : null;
        if (glass == null)
        {
            glass = Lit(lit, "GH_Glass_Clear", "B9D3DE", .92f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            Color c = glass.GetColor("_BaseColor");
            c.a = .16f;
            glass.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(glass);
        }
        b.mats["Glass"] = glass;
        AssetDatabase.SaveAssets();
    }

    static Material Lit(Shader lit, string file, string hex, float smoothness, bool reflections = true)
    {
        string path = MaterialFolder + "/" + file + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(lit) { name = file, enableInstancing = true };
            AssetDatabase.CreateAsset(m, path);
        }
        ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_EnvironmentReflections", reflections ? 1f : 0f);
        if (reflections) m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        else m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(m);
        return m;
    }

    // A unit box (-0.5..0.5), readable, with a normal, tangent and UVs per face.
    static Mesh BoxMesh()
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var tan = new List<Vector4>();
        var tris = new List<int>();
        void Face(Vector3 normal, Vector3 u, Vector3 w)
        {
            int start = v.Count;
            Vector3 c = normal * .5f;
            v.Add(c - u * .5f - w * .5f); v.Add(c + u * .5f - w * .5f); v.Add(c + u * .5f + w * .5f); v.Add(c - u * .5f + w * .5f);
            for (int i = 0; i < 4; i++) { n.Add(normal); tan.Add(new Vector4(u.x, u.y, u.z, -1f)); }
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            // Wound so the face looks along its normal.
            if (Vector3.Dot(Vector3.Cross(u, w), normal) > 0f) tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            else tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        Face(Vector3.right, Vector3.forward, Vector3.up);
        Face(Vector3.left, Vector3.back, Vector3.up);
        Face(Vector3.up, Vector3.right, Vector3.forward);
        Face(Vector3.down, Vector3.right, Vector3.back);
        Face(Vector3.forward, Vector3.left, Vector3.up);
        Face(Vector3.back, Vector3.right, Vector3.up);
        var mesh = new Mesh { name = BoxName };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTangents(tan);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Every new mesh in one asset, readable (the see-through and the cut-away walls read them at run time).
    static void SaveMeshes(List<Object> meshes, StringBuilder report)
    {
        Object main = AssetDatabase.LoadMainAssetAtPath(AssetPath);
        bool first = main == null;
        var keep = new HashSet<Object>(meshes);
        int added = 0;
        foreach (Object o in meshes.Distinct())
        {
            if (AssetDatabase.Contains(o)) continue;   // the box, saved by an earlier build
            if (o is Mesh m) m.UploadMeshData(false);
            if (first)
            {
                AssetDatabase.CreateAsset(o, AssetPath);
                first = false;
            }
            else AssetDatabase.AddObjectToAsset(o, AssetPath);
            added++;
        }
        // What an earlier build left in the asset and this one doesn't use (Take Grace's house back out put the
        // house's own meshes back, so nothing points at them any more) is taken out of it: the asset holds one
        // build's meshes, not every build's.
        int pruned = 0;
        if (main != null)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                if (o != null && o != main && !keep.Contains(o))
                {
                    AssetDatabase.RemoveObjectFromAsset(o);
                    Object.DestroyImmediate(o);
                    pruned++;
                }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath);
        report.AppendLine($"{added} new meshes saved in {AssetPath}" + (pruned > 0 ? $" ({pruned} left there by earlier builds taken out of it)." : "."));
    }

    // ================================================================== mesh surgery

    // One mesh, worked on in its own space: positions, normals, tangents, two UV sets and colours per vertex,
    // triangles per sub-mesh. New faces take their texture mapping from the faces they continue.
    sealed class Surgery
    {
        readonly List<Vector3> v = new(), n = new();
        readonly List<Vector4> t = new();
        readonly List<Vector2> uv0 = new(), uv1 = new();
        readonly List<Color> col = new();
        readonly List<List<int>> subs = new();
        readonly bool hasN, hasT, hasUv0, hasUv1, hasCol;

        public Surgery(Mesh m)
        {
            m.GetVertices(v); m.GetNormals(n); m.GetTangents(t); m.GetUVs(0, uv0); m.GetUVs(1, uv1); m.GetColors(col);
            hasN = n.Count == v.Count; hasT = t.Count == v.Count; hasUv0 = uv0.Count == v.Count; hasUv1 = uv1.Count == v.Count; hasCol = col.Count == v.Count;
            for (int s = 0; s < m.subMeshCount; s++) subs.Add(new List<int>(m.GetTriangles(s)));
        }

        Surgery(Surgery like)
        {
            hasN = like.hasN; hasT = like.hasT; hasUv0 = like.hasUv0; hasUv1 = like.hasUv1; hasCol = like.hasCol;
            foreach (var _ in like.subs) subs.Add(new List<int>());
        }

        public int Triangles => subs.Sum(s => s.Count) / 3;

        public Mesh ToMesh(string name)
        {
            // Only the vertices still used, in order.
            var map = new Dictionary<int, int>();
            var keep = new List<int>();
            foreach (var s in subs) foreach (int i in s) if (!map.ContainsKey(i)) { map[i] = keep.Count; keep.Add(i); }
            var mesh = new Mesh { name = name, indexFormat = keep.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(keep.Select(i => v[i]).ToList());
            if (hasN) mesh.SetNormals(keep.Select(i => n[i]).ToList());
            if (hasT) mesh.SetTangents(keep.Select(i => t[i]).ToList());
            if (hasUv0) mesh.SetUVs(0, keep.Select(i => uv0[i]).ToList());
            if (hasUv1) mesh.SetUVs(1, keep.Select(i => uv1[i]).ToList());
            if (hasCol) mesh.SetColors(keep.Select(i => col[i]).ToList());
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s].Select(i => map[i]).ToList(), s);
            mesh.RecalculateBounds();
            return mesh;
        }

        IEnumerable<(int sub, int at)> AllTriangles()
        {
            for (int s = 0; s < subs.Count; s++)
                for (int k = 0; k + 2 < subs[s].Count; k += 3) yield return (s, k);
        }

        // Where a point lies on a triangle's plane, as the triangle's texture maps it (affine: it carries on past the edges).
        static Vector2 Map(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 e1 = b - a, e2 = c - a, d = p - a;
            float d11 = Vector3.Dot(e1, e1), d12 = Vector3.Dot(e1, e2), d22 = Vector3.Dot(e2, e2);
            float det = d11 * d22 - d12 * d12;
            if (Mathf.Abs(det) < 1e-12f) return ua;
            float s1 = (d22 * Vector3.Dot(d, e1) - d12 * Vector3.Dot(d, e2)) / det;
            float s2 = (d11 * Vector3.Dot(d, e2) - d12 * Vector3.Dot(d, e1)) / det;
            return ua + s1 * (ub - ua) + s2 * (uc - ua);
        }

        // A triangle using vertex i (for its texture mapping), as (a, b, c) indices.
        (int a, int b, int c) TriangleOf(int i)
        {
            foreach (var (s, k) in AllTriangles())
            {
                var l = subs[s];
                if (l[k] == i || l[k + 1] == i || l[k + 2] == i) return (l[k], l[k + 1], l[k + 2]);
            }
            return (-1, -1, -1);
        }

        /// <summary>Moves every vertex at x == from (and where) to x == to; its texture keeps the same scale. The count moved.</summary>
        public int MoveX(Func<Vector3, bool> where, float from, float to)
        {
            var moving = new List<int>();
            for (int i = 0; i < v.Count; i++) if (Mathf.Abs(v[i].x - from) < .001f && where(v[i])) moving.Add(i);
            Reshape(moving, i => new Vector3(to, v[i].y, v[i].z));
            return moving.Count;
        }

        /// <summary>Moves every vertex to where(p) says; those remap says so get their texture mapping from before.</summary>
        public void Reshape(Func<Vector3, Vector3> to, Func<Vector3, bool> remap)
        {
            var all = Enumerable.Range(0, v.Count).ToList();
            var target = all.Select(i => to(v[i])).ToList();
            var keepUv = all.Where(i => !remap(v[i])).ToHashSet();
            Reshape(all, i => target[i], keepUv);
        }

        void Reshape(List<int> which, Func<int, Vector3> to, HashSet<int> keepUv = null)
        {
            var newPos = new Dictionary<int, Vector3>();
            var newUv0 = new Dictionary<int, Vector2>();
            var newUv1 = new Dictionary<int, Vector2>();
            // All from the positions as they were, before any moves.
            foreach (int i in which)
            {
                Vector3 p = to(i);
                newPos[i] = p;
                if (keepUv != null && keepUv.Contains(i)) continue;
                var (a, b, c) = TriangleOf(i);
                if (a < 0) continue;
                if (hasUv0) newUv0[i] = Map(p, v[a], v[b], v[c], uv0[a], uv0[b], uv0[c]);
                if (hasUv1) newUv1[i] = Map(p, v[a], v[b], v[c], uv1[a], uv1[b], uv1[c]);
            }
            foreach (var kv in newPos) v[kv.Key] = kv.Value;
            foreach (var kv in newUv0) uv0[kv.Key] = kv.Value;
            foreach (var kv in newUv1) uv1[kv.Key] = kv.Value;
        }

        /// <summary>Takes the triangles which says out of this mesh, into a mesh of their own (same sub-meshes).</summary>
        public Surgery Extract(Func<(Vector3 a, Vector3 b, Vector3 c), bool> which)
        {
            var part = new Surgery(this);
            // The part shares this mesh's vertex list (ToMesh keeps only what it uses).
            part.v.AddRange(v); part.n.AddRange(n); part.t.AddRange(t); part.uv0.AddRange(uv0); part.uv1.AddRange(uv1); part.col.AddRange(col);
            for (int s = 0; s < subs.Count; s++)
            {
                var kept = new List<int>();
                var l = subs[s];
                for (int k = 0; k + 2 < l.Count; k += 3)
                {
                    bool take = which((v[l[k]], v[l[k + 1]], v[l[k + 2]]));
                    (take ? part.subs[s] : kept).AddRange(new[] { l[k], l[k + 1], l[k + 2] });
                }
                subs[s] = kept;
            }
            return part;
        }

        static Vector2 In2D(Vector3 p, int axis) => axis == 0 ? new Vector2(p.z, p.y) : axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);

        static Vector3 From2D(Vector2 q, int axis, float at) => axis == 0 ? new Vector3(at, q.y, q.x) : axis == 1 ? new Vector3(q.x, at, q.y) : new Vector3(q.x, q.y, at);

        Vector3 FaceNormal(int a, int b, int c) => Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;

        /// <summary>
        /// Cuts a rectangular hole out of the faces lying in the plane (axis = at) that look toward sign along it:
        /// each such face (an axis-aligned rectangle of two triangles) is replaced by the parts of it outside the hole.
        /// The hole is in the plane's own two coordinates (x, y for axis 2; x, z for axis 1; z, y for axis 0).
        /// Returns how many faces it cut.
        /// </summary>
        public int CutHole(int axis, float at, int sign, Rect hole)
        {
            int cut = 0;
            for (int s = 0; s < subs.Count; s++)
            {
                var l = subs[s];
                // The triangles in the plane, grouped into faces by shared vertices.
                var inPlane = new List<int>();
                for (int k = 0; k + 2 < l.Count; k += 3)
                {
                    int a = l[k], b = l[k + 1], c = l[k + 2];
                    if (Mathf.Abs(v[a][axis] - at) > .001f || Mathf.Abs(v[b][axis] - at) > .001f || Mathf.Abs(v[c][axis] - at) > .001f) continue;
                    if (FaceNormal(a, b, c)[axis] * sign < .9f) continue;
                    inPlane.Add(k);
                }
                var faces = new List<List<int>>();
                var faceOf = new Dictionary<int, List<int>>();
                foreach (int k in inPlane)
                {
                    List<int> face = null;
                    foreach (int i in new[] { l[k], l[k + 1], l[k + 2] })
                        if (faceOf.TryGetValue(i, out var f)) { face = f; break; }
                    if (face == null) { face = new List<int>(); faces.Add(face); }
                    face.Add(k);
                    foreach (int i in new[] { l[k], l[k + 1], l[k + 2] }) faceOf[i] = face;
                }
                var remove = new HashSet<int>();
                var addTris = new List<int>();
                foreach (var face in faces)
                {
                    var corners = face.SelectMany(k => new[] { l[k], l[k + 1], l[k + 2] }).Distinct().ToList();
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                    foreach (int i in corners) { Vector2 q = In2D(v[i], axis); min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                    var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    if (!rect.Overlaps(hole) || Clip(rect, hole).width < .001f || Clip(rect, hole).height < .001f) continue;
                    // Only a plain rectangle is cut: its triangles' area must be the rectangle's.
                    float area = face.Sum(k => Vector3.Cross(v[l[k + 1]] - v[l[k]], v[l[k + 2]] - v[l[k]]).magnitude * .5f);
                    if (Mathf.Abs(area - rect.width * rect.height) > .002f * Mathf.Max(1f, rect.width * rect.height))
                        throw new InvalidOperationException($"A face in the plane {"xyz"[axis]} = {at} isn't a plain rectangle; nothing was changed.");
                    int k0 = face[0];
                    int ta = l[k0], tb = l[k0 + 1], tc = l[k0 + 2];
                    Vector3 normal = FaceNormal(ta, tb, tc);
                    foreach (Rect piece in Subtract(rect, hole))
                    {
                        if (piece.width < .0005f || piece.height < .0005f) continue;
                        var q = new[] { new Vector2(piece.xMin, piece.yMin), new Vector2(piece.xMax, piece.yMin), new Vector2(piece.xMax, piece.yMax), new Vector2(piece.xMin, piece.yMax) };
                        int start = v.Count;
                        foreach (Vector2 c2 in q)
                        {
                            Vector3 p = From2D(c2, axis, at);
                            v.Add(p);
                            if (hasN) n.Add(n[ta]);
                            if (hasT) t.Add(t[ta]);
                            if (hasUv0) uv0.Add(Map(p, v[ta], v[tb], v[tc], uv0[ta], uv0[tb], uv0[tc]));
                            if (hasUv1) uv1.Add(Map(p, v[ta], v[tb], v[tc], uv1[ta], uv1[tb], uv1[tc]));
                            if (hasCol) col.Add(col[ta]);
                        }
                        bool facing = Vector3.Dot(Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]), normal) > 0f;
                        addTris.AddRange(facing ? new[] { start, start + 1, start + 2, start, start + 2, start + 3 }
                                                : new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    }
                    foreach (int k in face) remove.Add(k);
                    cut++;
                }
                if (remove.Count == 0) continue;
                var kept = new List<int>();
                for (int k = 0; k + 2 < l.Count; k += 3) if (!remove.Contains(k)) kept.AddRange(new[] { l[k], l[k + 1], l[k + 2] });
                kept.AddRange(addTris);
                subs[s] = kept;
            }
            return cut;
        }

        /// <summary>
        /// Adds a rectangular face in the plane (axis = at), looking toward sign along it, mapped like the face whose
        /// vertices like says (the end of a lip, say). The rectangle is in the plane's own coordinates. Returns 1.
        /// </summary>
        public int AddCap(int axis, float at, int sign, Rect rect, Func<Vector3, bool> like)
        {
            int ta = -1, tb = -1, tc = -1, sub = 0;
            for (int s = 0; s < subs.Count && ta < 0; s++)
            {
                var l = subs[s];
                for (int k = 0; k + 2 < l.Count; k += 3)
                    if (like(v[l[k]]) && like(v[l[k + 1]]) && like(v[l[k + 2]]) && Mathf.Abs(FaceNormal(l[k], l[k + 1], l[k + 2])[axis]) > .9f)
                    { ta = l[k]; tb = l[k + 1]; tc = l[k + 2]; sub = s; break; }
            }
            if (ta < 0) throw new InvalidOperationException($"No face to map a new end at {"xyz"[axis]} = {at} like; nothing was changed.");
            Vector3 normal = Vector3.zero;
            normal[axis] = sign;
            var q = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
            int start = v.Count;
            // Mapped from the template face's own plane: the same place on it, less the distance between the planes.
            Vector3 offset = Vector3.zero;
            offset[axis] = v[ta][axis] - at;
            foreach (Vector2 c2 in q)
            {
                Vector3 p = From2D(c2, axis, at);
                v.Add(p);
                if (hasN) n.Add(normal);
                if (hasT) t.Add(t[ta]);
                if (hasUv0) uv0.Add(Map(p + offset, v[ta], v[tb], v[tc], uv0[ta], uv0[tb], uv0[tc]));
                if (hasUv1) uv1.Add(Map(p + offset, v[ta], v[tb], v[tc], uv1[ta], uv1[tb], uv1[tc]));
                if (hasCol) col.Add(col[ta]);
            }
            bool facing = Vector3.Dot(Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]), normal) > 0f;
            subs[sub].AddRange(facing ? new[] { start, start + 1, start + 2, start, start + 2, start + 3 }
                                      : new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            return 1;
        }

        static Rect Clip(Rect r, Rect to) =>
            Rect.MinMaxRect(Mathf.Max(r.xMin, to.xMin), Mathf.Max(r.yMin, to.yMin), Mathf.Min(r.xMax, to.xMax), Mathf.Min(r.yMax, to.yMax));

        // The rectangle less the hole: below, above, and either side of it (up to four pieces).
        static IEnumerable<Rect> Subtract(Rect face, Rect hole)
        {
            Rect h = Clip(hole, face);
            if (h.yMin > face.yMin) yield return Rect.MinMaxRect(face.xMin, face.yMin, face.xMax, h.yMin);
            if (h.yMax < face.yMax) yield return Rect.MinMaxRect(face.xMin, h.yMax, face.xMax, face.yMax);
            if (h.xMin > face.xMin) yield return Rect.MinMaxRect(face.xMin, h.yMin, h.xMin, h.yMax);
            if (h.xMax < face.xMax) yield return Rect.MinMaxRect(h.xMax, h.yMin, face.xMax, h.yMax);
        }
    }

    // ================================================================== the lab

    [MenuItem(Menu + "Break-ins 3 - Play the night at Grace's door (lab)")]
    static void PlayLab() => StartLab(1);

    [MenuItem(Menu + "Break-ins 3 - Walk Grace's house by itself (lab, a check)")]
    static void WalkLab() => StartLab(2);

    static void StartLab(int mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional(HouseName);
            if (house == null || house.Find(RootName) == null)
                throw new InvalidOperationException("Grace's house isn't built inside yet (Break-ins 1 - Build Grace's house inside).");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        // A test save only (the playtest save is never touched): Day 5, the café closed for the night, Barnaby
        // already on Ace's shelf (Night 1), and what Grace has told Ace, so the notebook knows her house.
        var facts = new List<NotebookFactData>();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName)) { fact.day = 1; facts.Add(fact); }
        SaveCheckpointStorage.Write(path, new SaveData
        {
            day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false, notebook = facts.ToArray(),
            night = new NightSaveData { nights = 1, trophies = new[] { NightThings.GraceGnome } },
        });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.SetInt(GraceHouse.LabKey, mode);
        PlayerPrefs.Save();
        Debug.Log(Tag + (mode == 2
            ? "Break-ins walk check: the night at Grace's door; Ace walks her house by itself (in, the kitchen, up the stairs, the bedroom, down and out). " +
              "Keep the Game view in front and leave the mouse and keyboard alone. Report and photos: Logs/Night/grace-walk-<time>/."
            : "Break-ins lab: the night at Grace's door, 12 West Street. Walk in (her door opens for Ace), look round both floors, V for first person. " +
              "The test save is " + path + "; your playtest save is not used."));
        EditorApplication.EnterPlaymode();
    }

    // A request that never became a Play session must not make the next Play a break-ins lab.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(GraceHouse.LabKey))
        {
            PlayerPrefs.DeleteKey(GraceHouse.LabKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
