#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE HERO DEVICES INTO THEIR PREFABS (8-9 Oct 2026; claude/hero-models-spec.md §3)
//
//   Fixit Fidget > Bench > Devices 2 - Fit the hero devices into their prefabs (PhoneRepair, PocketWatch, GraceReunionCamera)
//
// The stand-in devices were built from Unity's primitives: a cube or a cylinder for each piece, shaped by its object's
// scale, with a unit collider scaled along. The hero devices (Tools/Blender/hero_phone.py, hero_watch.py; imported by
// Devices 1 as HD_Phone.fbx, HD_Watch.fbx and HD_Camera.fbx) are built to the same sizes in metres, so this step changes only what
// the player sees: each piece's mesh is swapped for the hero one, the object's scale set to 1 (the mesh is real size),
// its box collider resized to the mesh (a screw's to its HEAD only, which is what Screw.HeadSize sizes the hole's ring
// and the driver's tip from; a cover's stops at the FACE the screws rest on, see Piece.faceY; the watch's crown keeps
// its capsule), and the FBX's materials put on (the phone's screens keep their own M_BrokenGlass / M_FreshGlass).
// Positions, rotations, parents, scripts, screw holes and seats are untouched, so the bench and every lab see the
// devices they knew. Undo is git (each prefab is one file).
//
// Bench 3 on 9 Oct (the first play check on the hero phone) failed at the first screw: the cursor over it read "Back
// cover: Screws first". The hero cover's camera island stands 2.5 mm proud of the glass, so a collider sized to the
// whole mesh reached past the screw heads (which sit on the glass) and caught the cursor before them. The stand-in
// cover was a flat slab, so this never showed. Hence faceY: what stands proud of the face is not the cover.
//
// The grime check the same night failed on the watch: its three grime spots sit in the open movement cavity, and a
// box sized to the case's whole mesh fills that cavity (and the sunken dial), so every view ray met the case first.
// Hence Piece.boxes: a hollow piece names its solid parts, and gets one box collider per part. The rule behind both:
// a collider is the piece's SOLID, never its bounds, wherever other pieces sit in a recess of it.
// ---------------------------------------------------------------------------
internal static class HeroDeviceSteps
{
    const string Tag = "[Hero devices] ";

    sealed class Piece
    {
        public string source;        // the FBX child's name; "" is the root
        public string[] targets;     // the prefab objects the mesh goes into
        public bool keepMaterials;   // the prefab object's own materials stay
        public Vector3? head;        // a screw: its collider is this box round the origin, not the whole mesh
        public float? faceY;         // a cover: other pieces rest on its outer face at this local Y, and its collider stops
                                     // there (from that face to the mesh's top); what stands proud of the face (the phone's
                                     // camera island) is left out, or it would catch the cursor before the screws do
        public Bounds[] boxes;       // a hollow piece (the watch case): its collider is these boxes, its solid parts in local
                                     // metres, instead of one box round the whole mesh that would fill the recess other
                                     // pieces sit in (the movement cavity, where the grime and the mainspring are)
    }

    sealed class Move
    {
        public string name;          // a prefab object to put somewhere else (local position), with the reason
        public Vector3 to;
        public string why;
    }

    sealed class Device
    {
        public string model, prefab;
        public Piece[] pieces;
        public Move[] moves;
    }

    static readonly Device[] Devices =
    {
        new Device
        {
            model = BlenderKitSteps.DeviceModels + "/HD_Phone.fbx", prefab = "Assets/AssetsPrefabs/PhoneRepair.prefab",
            pieces = new[]
            {
                new Piece { source = "", targets = new[] { "Body" } },
                new Piece { source = "Screen", targets = new[] { "Broken", "Fresh" }, keepMaterials = true },
                // the glass's outer face is at -7 mm in the phone (local -1.5: hero_phone.back_cover), where the screw heads sit
                new Piece { source = "BackCover", targets = new[] { "BackCover" }, faceY = -.0015f },
                new Piece { source = "Screw", targets = new[] { "Screw0", "Screw1" }, head = new Vector3(.008f, .0020f, .008f) },
            }
        },
        new Device
        {
            model = BlenderKitSteps.DeviceModels + "/HD_Watch.fbx", prefab = "Assets/AssetsPrefabs/PocketWatch.prefab",
            pieces = new[]
            {
                // the case is a ring with the dial sunk in its front and the movement open at its back; its solid, for the
                // cursor and the mat, is the pillar plate and dial (from the plate's back face at -5.65, where the mainspring
                // rests, to the bezel's top at +10) as two crossed slabs that stay inside the round (corners 3 mm past the
                // band, against 14 mm for one square), and the pendant-and-bow tower in the case's mid-plane. The band's
                // back rim at -14 is left without a collider, as the stand-in's was: the watch rests on its back plate
                new Piece { source = "", targets = new[] { "Cylinder" }, boxes = new[]
                {
                    new Bounds(new Vector3(0f, .002175f, 0f), new Vector3(.126f, .01565f, .092f)),
                    new Bounds(new Vector3(0f, .002175f, 0f), new Vector3(.092f, .01565f, .126f)),
                    new Bounds(new Vector3(0f, 0f, .101f), new Vector3(.030f, .008f, .052f)),
                } },
                new Piece { source = "BackPlate", targets = new[] { "BackPlate" } },
                new Piece { source = "Crown", targets = new[] { "Crown" } },
                new Piece { source = "Screw", targets = new[] { "Screw0", "Screw1", "Screw2", "Screw3" }, head = new Vector3(.012f, .0026f, .012f) },
                new Piece { source = "Mainspring", targets = new[] { "Part_Extra_Mainspring", "Fresh" } },
            },
            moves = new[]
            {
                // the stand-in left this spot 84 mm from the centre, 9 mm outside a 75 mm case, floating beside the watch;
                // it goes onto the movement plate (58 mm out, clear of the barrel and the balance), where the other inside spots are
                new Move { name = "Grime_Extra_Watch_1", to = new Vector3(-.0483f, -.013f, .0324f), why = "was 84 mm out, outside the case" },
            }
        },
        new Device
        {
            // the shutter mechanism's blades are its children, placed and sized through its scale: when its scale goes to 1
            // their places are kept (ScaleChildren) and their own meshes are fitted after it
            model = BlenderKitSteps.DeviceModels + "/HD_Camera.fbx", prefab = "Assets/GraceShowcase/GraceReunionCamera.prefab",
            pieces = new[]
            {
                new Piece { source = "", targets = new[] { "Camera body" } },
                new Piece { source = "LensHousing", targets = new[] { "Lens housing" } },
                new Piece { source = "LensGlass", targets = new[] { "Lens glass" } },
                new Piece { source = "FilmPath", targets = new[] { "Open film path" } },
                new Piece { source = "Mechanism", targets = new[] { "Jammed shutter mechanism" } },
                new Piece { source = "BentBlade", targets = new[] { "Bent shutter blade" } },
                new Piece { source = "WorkingBlade", targets = new[] { "Working shutter blade" } },
                new Piece { source = "Strap", targets = new[] { "Upper strap", "Lower strap" } },
                new Piece { source = "WornLoop", targets = new[] { "Worn loop" } },
            }
        },
    };

    [MenuItem("Fixit Fidget/Bench/Devices 2 - Fit the hero devices into their prefabs (PhoneRepair, PocketWatch, GraceReunionCamera)")]
    static void Fit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "This one is for Edit Mode: stop Play first."); return; }
        var report = new StringBuilder();
        int failures = 0;
        foreach (Device device in Devices)
        {
            report.AppendLine(device.prefab + " <- " + device.model);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(device.model);
            if (asset == null) { failures++; report.AppendLine("FAIL  " + device.model + " isn't imported: run Devices 1 first."); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(device.prefab);
            try
            {
                foreach (Piece piece in device.pieces)
                {
                    Transform src = piece.source == "" ? asset.transform : asset.transform.Find(piece.source);
                    MeshFilter filter = src != null ? src.GetComponent<MeshFilter>() : null;
                    MeshRenderer renderer = src != null ? src.GetComponent<MeshRenderer>() : null;
                    if (filter == null || filter.sharedMesh == null) { failures++; report.AppendLine($"FAIL  {device.model} has no mesh '{piece.source}'"); continue; }
                    Mesh mesh = filter.sharedMesh;
                    long indices = 0;
                    for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                    foreach (string name in piece.targets)
                    {
                        Transform t = FindDeep(root.transform, name);
                        if (t == null) { failures++; report.AppendLine($"FAIL  the prefab has no '{name}'"); continue; }
                        Vector3 oldScale = t.localScale;
                        MeshFilter mf = t.GetComponent<MeshFilter>() ?? t.gameObject.AddComponent<MeshFilter>();
                        MeshRenderer mr = t.GetComponent<MeshRenderer>() ?? t.gameObject.AddComponent<MeshRenderer>();
                        mf.sharedMesh = mesh;
                        t.localScale = Vector3.one;
                        // children placed through this object's scale keep their places (and, unless fitted themselves, their sizes)
                        var fitted = new HashSet<string>(device.pieces.SelectMany(q => q.targets));
                        foreach (Transform child in t)
                        {
                            child.localPosition = Vector3.Scale(child.localPosition, oldScale);
                            if (!fitted.Contains(child.name)) child.localScale = Vector3.Scale(child.localScale, oldScale);
                        }
                        if (!piece.keepMaterials && renderer != null) mr.sharedMaterials = renderer.sharedMaterials;
                        string collider = "no box collider";
                        var box = t.GetComponent<BoxCollider>();
                        if (box != null && piece.boxes != null)
                        {
                            // one box per solid part: the existing colliders are reused in order, missing ones added, extras removed
                            var boxes = t.GetComponents<BoxCollider>().ToList();
                            while (boxes.Count < piece.boxes.Length) boxes.Add(t.gameObject.AddComponent<BoxCollider>());
                            for (int i = piece.boxes.Length; i < boxes.Count; i++) UnityEngine.Object.DestroyImmediate(boxes[i]);
                            for (int i = 0; i < piece.boxes.Length; i++)
                            {
                                boxes[i].isTrigger = box.isTrigger;
                                boxes[i].sharedMaterial = box.sharedMaterial;
                                boxes[i].center = piece.boxes[i].center;
                                boxes[i].size = piece.boxes[i].size;
                            }
                            collider = piece.boxes.Length + " boxes, the solid not the bounds: " +
                                       string.Join("; ", piece.boxes.Select(b => V(b.size) + " at " + V(b.center)));
                        }
                        else if (box != null)
                        {
                            if (piece.head.HasValue) { box.center = Vector3.zero; box.size = piece.head.Value; }
                            else if (piece.faceY.HasValue)
                            {
                                // from the face other pieces rest on up to the mesh's top: the island beyond the face is left out
                                Bounds b = mesh.bounds;
                                float face = piece.faceY.Value, top = b.max.y;
                                box.center = new Vector3(b.center.x, (top + face) / 2f, b.center.z);
                                box.size = new Vector3(b.size.x, top - face, b.size.z);
                            }
                            else { box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
                            collider = "box " + V(box.size) + " at " + V(box.center) + (piece.faceY.HasValue ? " (to the face, not the island)" : "");
                        }
                        var capsule = t.GetComponent<CapsuleCollider>();
                        if (capsule != null)
                        {
                            // the crown: along its own Y, as long and as wide as the new mesh
                            Bounds b = mesh.bounds;
                            capsule.center = b.center;
                            capsule.direction = 1;
                            capsule.height = b.size.y;
                            capsule.radius = Mathf.Max(b.extents.x, b.extents.z);
                            collider = $"capsule r {capsule.radius:0.0000} h {capsule.height:0.0000} at {V(capsule.center)}";
                        }
                        string mats = string.Join(", ", mr.sharedMaterials.Select(m => m != null ? m.name : "none"));
                        report.AppendLine($"PASS  {name}: {(piece.source == "" ? "the root" : piece.source)} mesh, {indices / 3} triangles, {V(mesh.bounds.size)}; " +
                                          $"scale {V(oldScale)} -> 1; {collider}; materials {mats}{(piece.keepMaterials ? " (its own, kept)" : "")}");
                    }
                }
                foreach (Move move in device.moves ?? Array.Empty<Move>())
                {
                    Transform t = FindDeep(root.transform, move.name);
                    if (t == null) { failures++; report.AppendLine($"FAIL  the prefab has no '{move.name}' to move"); continue; }
                    Vector3 was = t.localPosition;
                    t.localPosition = move.to;
                    report.AppendLine($"PASS  {move.name}: moved {V(was)} -> {V(move.to)} ({move.why})");
                }
                PrefabUtility.SaveAsPrefabAsset(root, device.prefab);
            }
            catch (Exception e) { failures++; report.AppendLine("FAIL  " + e.Message); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            report.AppendLine();
        }
        string folder = Path.Combine("Logs", "Bench", "hero-devices-fit-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = Tag + (failures == 0 ? "The hero devices are in their prefabs. " : failures + " step(s) FAILED. ") + folder + "\n" + report;
        if (failures == 0) Debug.Log(line); else Debug.LogError(line);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.0000}, {1:0.0000}, {2:0.0000})", v.x, v.y, v.z);
}
#endif
