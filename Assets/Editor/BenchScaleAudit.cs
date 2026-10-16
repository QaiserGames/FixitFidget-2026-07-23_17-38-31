#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// ONE SCALE AT THE BENCH: THE AUDIT, AND WHAT THE SCALE STEPS SHARE (14 Oct 2026; specs/td-one-scale.md §3, §6.4, §7)
//
//   Fixit Fidget > Checks > Bench scale audit (read-only)
//
// His note (14 Oct, 00:30): "ENOURMOUSE HOLES for the screwdrivers ... the holes are ENERMOUSe and the screws are so tiny,
// and that doesn't mean to go say to make the screws enormous as well." Three scales stood at one bench: the phone real,
// Grace's camera 2x (hero_camera.py), the pocket watch 3x (hero_watch.py), against real-size tools and mat. After the
// Blender lead's real-size exports (wave 2) and Devices 2's bake, every device is real size at the bench and in the café.
//
// The audit measures the TD's §3 table in Unity, against the reference sheet's real sizes (±7 %) and the device ÷ driver
// ratios (phone 1.10, camera 1.31 with its strap, watch 0.49), with the Producer's screw call (feel targets §2.3: heads
// 3.8 mm, not 3.0; the hole decal 1.15 × head; the driver's tip 0.6 × head). Run it BEFORE Devices 2 (the camera, watch,
// screw and hole rows FAIL by design: that is the "before") and AFTER Devices 2 and Bench 9 (every row PASS).
//
// Read-only: the prefabs are loaded into their own preview scenes and unloaded unsaved. It writes a report and, while a
// prefab is not yet real size, its "before" snapshot (every object's place and size, Logs/Bench/scale/<prefab>-before.json),
// which the "after" run compares against: every object at before × k ± 0.1 mm (a screw by its head's face on its surface:
// the 3.8 mm head is 1.33 mm tall, not the old head × k, and it must still sit ON the glass or the plate).
//
// BenchScale (below) is what Devices 2 (HeroDeviceSteps), Bench 9 (BenchFramingSteps) and the intake tray check share:
// the devices and their factors, the screw sizes, the snapshot, the manifest, the measuring and the preview-scene photos.
// ---------------------------------------------------------------------------
internal static class BenchScale
{
    public const string Tag = "[One scale] ";
    public const string ManifestPath = BlenderKitSteps.DeviceModels + "/devices.json";
    public const string ToolModels = BlenderKitSteps.ToolModels;
    public const string CafeScene = "Assets/Playtests/AcesCafeLayoutPlaytest.unity";

    // The Producer's call 3 (feel-targets-2026-10-14.md §2): "Screw heads (TD Q2): 3.8 mm, not 3.0." 3.0 drew a head at
    // 9.4 px at 1080p, under the 12 px where its cross reads; 3.8 is an M2 pan head, under half of the old 8 mm.
    public const float ScrewHead = .0038f;
    public const float ScrewHeadHeight = .35f * ScrewHead;   // 1.33 mm: Art A8's pan head, 0.35 × its diameter
    public const float HoleDecal = 1.15f;                    // the hole is Art's decal, a quad 1.15 × the head (4.4 mm)
    public const float DriverTip = .6f;                      // the driver's tip cross, 0.6 × the head (2.3 mm)
    public const float RealTolerance = .07f;                 // a piece within ±7 % of its real size (TD §7, stage 3)
    public const float PlaceTolerance = .0001f;              // every child at before × k ± 0.1 mm (TD §7)
    public const float HeadTolerance = .0001f;               // a screw head 3.8 ± 0.1 mm (TD §7, at the Producer's size)

    // Where a device's lowest point sits over the point it is set down on (JobBase.restHeight's surface): 3.3 mm, which is
    // 0.3 mm over the intake tray's felt (the felt is 3.0 mm over its slot points, Playtest4TraySteps' header). The TD's
    // "restHeight × k" (§6.4) would scale the gap with the device, but the felt doesn't scale: the watch would sink 1.9 mm
    // into it and the camera 1.5 mm. 3.3 mm is the Producer's own number for the watch (Playtest4ProducerSteps, 10 Oct).
    public const float SurfaceGap = .0033f;
    public const float FeltOverSlot = .003f;
    public const float FeltBandLow = -.0002f, FeltBandHigh = .00121f;  // "each device 0–1.2 mm over the felt" (TD §7); the phone sits at 1.2 today

    // From above (PlayerCarry, TD §6.8): a carried device is drawn at least this long, never more than this many times.
    public const float OverheadLength = .45f, OverheadCap = 7f, OverheadFloor = 2.2f;

    public sealed class Device
    {
        public string label, prefab, model;
        public float k;              // the bake: authored 1x (phone), 3x (watch), 2x (camera) → real; positions and stand-ins × k
        public float realWidth;      // the body's real width across (the FBX root mesh's x), metres: is the FBX real yet?
        public float driverRatio;    // its footprint's length ÷ the screwdriver's (TD §7)
        public (string source, string[] targets)[] fallback;   // the fit's map when devices.json has no "targets"
    }

    public static readonly Device[] Devices =
    {
        new Device
        {
            label = "phone", prefab = "Assets/AssetsPrefabs/PhoneRepair.prefab", model = BlenderKitSteps.DeviceModels + "/HD_Phone.fbx",
            k = 1f, realWidth = .0716f, driverRatio = 1.10f,     // iPhone 15: 71.6 × 147.6 × 7.80 (reference sheet §6)
            fallback = new[]
            {
                ("", new[] { "Body" }), ("ScreenCracked", new[] { "Broken" }), ("Screen", new[] { "Fresh" }), ("ScreenPlain", new[] { "Glass" }),
                ("BackCover", new[] { "BackCover" }), ("Screw", new[] { "Screw0", "Screw1" }),
            },
        },
        new Device
        {
            label = "watch", prefab = "Assets/AssetsPrefabs/PocketWatch.prefab", model = BlenderKitSteps.DeviceModels + "/HD_Watch.fbx",
            k = 1f / 3f, realWidth = .050f, driverRatio = .49f,  // case Ø 48–52 (reference sheet §7a)
            fallback = new[]
            {
                ("", new[] { "Cylinder" }), ("BackPlate", new[] { "BackPlate" }), ("Crown", new[] { "Crown" }),
                ("Screw", new[] { "Screw0", "Screw1", "Screw2", "Screw3" }), ("MainspringBroken", new[] { "Part_Extra_Mainspring" }), ("Mainspring", new[] { "Fresh" }),
            },
        },
        new Device
        {
            label = "camera", prefab = "Assets/GraceShowcase/GraceReunionCamera.prefab", model = BlenderKitSteps.DeviceModels + "/HD_Camera.fbx",
            // The body's mesh carries the strap's two lugs since wave 2 (v3.6: 134 mm across them), so its real width is an
            // overall width with lugs: the Olympus 35 SP's 129 mm, the Blender lead's REAL table (devices.json). The Canonet
            // QL17's 120 × 75 × 32 (reference sheet §8) is without lugs: 134 against 120 is 1.12x, and the step refused it.
            k = .5f, realWidth = .129f, driverRatio = 1.31f,
            fallback = new[]
            {
                ("", new[] { "Camera body" }), ("LensHousing", new[] { "Lens housing" }), ("LensGlass", new[] { "Lens glass" }),
                ("FilmPath", new[] { "Open film path" }), ("Mechanism", new[] { "Jammed shutter mechanism" }), ("BentBlade", new[] { "Bent shutter blade" }),
                ("WorkingBlade", new[] { "Working shutter blade" }), ("Strap", new[] { "Upper strap", "Lower strap" }), ("WornLoop", new[] { "Worn loop" }),
            },
        },
    };

    // ======================================================================= the report

    public sealed class Run
    {
        public readonly string Title, Folder;
        readonly StringBuilder text = new StringBuilder();
        public int Passes, Fails, Changes, Refusals;

        public Run(string title, string label)
        {
            Title = title;
            Folder = Path.Combine("Logs", "Bench", label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Folder);
            text.AppendLine(title + "   (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ")");
        }

        public bool Check(bool ok, string what)
        {
            if (ok) Passes++; else Fails++;
            text.AppendLine((ok ? "  PASS    " : "  FAIL    ") + what);
            return ok;
        }

        public void Info(string line) => text.AppendLine("  " + line);
        public void Changed(string line) { Changes++; text.AppendLine("  CHANGE  " + line); }
        public void Refused(string line) { Refusals++; text.AppendLine("  REFUSED " + line); }
        public void Section(string name) => text.AppendLine().AppendLine(name);

        /// <summary>Writes report.txt into the run's folder (and the Console line); returns the report's path.</summary>
        public string Finish(string verdict, bool error)
        {
            text.AppendLine().AppendLine(verdict);
            string path = Path.Combine(Folder, "report.txt");
            File.WriteAllText(path, text.ToString());
            string line = Tag + Title + ": " + verdict + "  Report: " + path;
            if (error) Debug.LogError(line); else Debug.Log(line);
            return path;
        }
    }

    // ======================================================================= the manifest (devices.json)

    /// <summary>devices.json, read whole: its "targets" table is an object keyed by the FBX's piece names, which JsonUtility can't read.</summary>
    public sealed class Manifest
    {
        public Dictionary<string, object> Root;

        public static Manifest Load(out string note)
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
            if (!File.Exists(file)) { note = $"{ManifestPath} is missing: the fit uses its own table."; return null; }
            try
            {
                var root = Json(File.ReadAllText(file)) as Dictionary<string, object>;
                note = $"{ManifestPath}: version {Str(root, "version")}, units {Str(root, "units") ?? "(not said: authored scale)"}.";
                return root != null ? new Manifest { Root = root } : null;
            }
            catch (Exception e) { note = $"{ManifestPath} couldn't be read ({e.Message}): the fit uses its own table."; return null; }
        }

        public Dictionary<string, object> Piece(string name)
        {
            if (Root == null || !Root.TryGetValue("pieces", out object list) || !(list is List<object> pieces)) return null;
            foreach (object p in pieces)
                if (p is Dictionary<string, object> d && Str(d, "name") == name) return d;
            return null;
        }

        /// <summary>"real" when the export says its devices are real size (the TD's §6.3: units "real", per device authoredScale).</summary>
        public string Units(Dictionary<string, object> piece) => Str(piece, "units") ?? Str(Root, "units");

        public float AuthoredScale(Dictionary<string, object> piece) =>
            piece != null && piece.TryGetValue("authoredScale", out object v) && v is double d && d > 0 ? (float)d : float.NaN;

        /// <summary>The FBX piece → prefab objects map ("" is the FBX root), or null when the manifest has none.</summary>
        public List<(string source, string[] targets)> Targets(Dictionary<string, object> piece)
        {
            if (piece == null || !piece.TryGetValue("targets", out object t) || !(t is Dictionary<string, object> map)) return null;
            string self = Str(piece, "name");
            var result = new List<(string, string[])>();
            foreach (KeyValuePair<string, object> kv in map)
            {
                if (!(kv.Value is List<object> names)) continue;
                result.Add((kv.Key == self ? "" : kv.Key, names.OfType<string>().ToArray()));
            }
            return result;
        }

        // ---- v3 (the Blender lead's wave 2, §4.3 and §5.2; the Producer's integration call 6) ----

        /// <summary>
        /// Where v3 puts objects after the bake, at real size in the prefab root's frame (metres), by the prefab object's name:
        /// each "moves" entry's "to" (the front face and all on it risen 9 mm with the deeper body, the new strap at its lugs,
        /// the loop behind the lens, the screws' heads on their surfaces) and each "scratches" entry's "at" (onto the loop's
        /// top turn). Empty when the manifest has none.
        /// </summary>
        public Dictionary<string, Vector3> MovedTo(Dictionary<string, object> piece)
        {
            var result = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            if (piece == null) return result;
            foreach (var (list, key) in new[] { ("moves", "to"), ("scratches", "at") })
            {
                if (!piece.TryGetValue(list, out object v) || !(v is List<object> entries)) continue;
                foreach (object e in entries)
                    if (e is Dictionary<string, object> d && Str(d, "name") is string name && Vec(d, key, out Vector3 to)) result[name] = to;
            }
            return result;
        }

        /// <summary>An object v3 adds that the prefab doesn't have yet (Art A7's "Clean shutter mechanism").</summary>
        public sealed class NewObject { public string name, source, under; public Vector3 at; public bool active = true; }

        public List<NewObject> NewObjects(Dictionary<string, object> piece)
        {
            var result = new List<NewObject>();
            if (piece == null || !piece.TryGetValue("newObjects", out object v) || !(v is List<object> entries)) return result;
            foreach (object e in entries)
            {
                if (!(e is Dictionary<string, object> d) || string.IsNullOrEmpty(Str(d, "name"))) continue;
                var made = new NewObject { name = Str(d, "name"), source = Str(d, "source") ?? "", under = Str(d, "createUnder") ?? "" };
                if (Vec(d, "at", out Vector3 at)) made.at = at;
                if (d.TryGetValue("active", out object a) && a is bool b) made.active = b;
                result.Add(made);
            }
            return result;
        }

        /// <summary>The objects that carry no collider (Art A6: the strap and its scratches are never a hover target).</summary>
        public List<string> NoCollider(Dictionary<string, object> piece) =>
            piece != null && piece.TryGetValue("strapNoCollider", out object v) && v is List<object> names ? names.OfType<string>().ToList() : new List<string>();

        static bool Vec(Dictionary<string, object> d, string key, out Vector3 v)
        {
            v = default;
            if (d == null || !d.TryGetValue(key, out object o) || !(o is List<object> xyz) || xyz.Count != 3) return false;
            if (!(xyz[0] is double x) || !(xyz[1] is double y) || !(xyz[2] is double z)) return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        static string Str(Dictionary<string, object> d, string key) =>
            d != null && d.TryGetValue(key, out object v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>A small JSON reader: objects → Dictionary, arrays → List, numbers → double.</summary>
    public static object Json(string text)
    {
        int i = 0;
        return JValue(text, ref i);
    }

    static void JSkip(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    static object JValue(string s, ref int i)
    {
        JSkip(s, ref i);
        if (i >= s.Length) return null;
        char c = s[i];
        if (c == '{')
        {
            var o = new Dictionary<string, object>(StringComparer.Ordinal);
            i++;
            while (true)
            {
                JSkip(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return o; }
                string key = JValue(s, ref i) as string ?? "";
                JSkip(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                o[key] = JValue(s, ref i);
                JSkip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return o; }
                throw new FormatException("a JSON object breaks off at " + i);
            }
        }
        if (c == '[')
        {
            var a = new List<object>();
            i++;
            while (true)
            {
                JSkip(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return a; }
                a.Add(JValue(s, ref i));
                JSkip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return a; }
                throw new FormatException("a JSON list breaks off at " + i);
            }
        }
        if (c == '"')
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    if (e == 'u' && i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                    i++;
                }
                else sb.Append(s[i++]);
            }
            i++;
            return sb.ToString();
        }
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (i == start) throw new FormatException("unexpected '" + c + "' in JSON at " + i);
        return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    // ======================================================================= the FBX: real yet?

    public static Mesh RootMesh(GameObject g)
    {
        if (g == null) return null;
        MeshFilter mf = g.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }

    public static Mesh ChildMesh(GameObject asset, string source)
    {
        if (asset == null) return null;
        if (string.IsNullOrEmpty(source)) return RootMesh(asset);
        Transform t = asset.transform.Find(source);
        return t != null ? RootMesh(t.gameObject) : null;
    }

    /// <summary>The device's FBX is real size: its body within ±7 % of the real one, and (a device with screws) its screw head
    /// 3.8 ± 0.1 mm. The camera and the watch FBX of 13 Oct are 2x and 3x; the phone's screws 8 mm.</summary>
    public static bool FbxIsReal(Device d, GameObject asset, string screwSource, out string why)
    {
        Mesh body = RootMesh(asset);
        if (body == null) { why = d.model + " isn't imported or has no root mesh (run Devices 1)."; return false; }
        float width = body.bounds.size.x * Mathf.Abs(asset.transform.localScale.x);
        float ratio = width / d.realWidth;
        bool ok = Mathf.Abs(ratio - 1f) <= RealTolerance;
        why = $"its body is {Mm(width)} across (real {Mm(d.realWidth)}: {ratio:0.00}x)";
        if (screwSource != null)
        {
            Mesh screw = ChildMesh(asset, screwSource);
            float head = screw != null ? Mathf.Max(screw.bounds.size.x, screw.bounds.size.z) : 0f;
            bool headOk = screw != null && Mathf.Abs(head - ScrewHead) <= HeadTolerance;
            why += screw != null ? $", its screw head {Mm(head)} (the Producer's {Mm(ScrewHead)})" : $", and it has no '{screwSource}' screw piece";
            ok &= headOk;
        }
        return ok;
    }

    // ======================================================================= the prefab, measured

    public static bool IsUnit(Vector3 s) => Mathf.Abs(s.x - 1f) < 1e-4f && Mathf.Abs(s.y - 1f) < 1e-4f && Mathf.Abs(s.z - 1f) < 1e-4f;

    public static string PathIn(Transform root, Transform t)
    {
        var names = new List<string>();
        for (Transform x = t; x != null && x != root; x = x.parent)
        {
            string name = x.name;
            Transform parent = x.parent;
            if (parent != null)
            {
                int same = 0;
                for (int i = 0; i < x.GetSiblingIndex(); i++) if (parent.GetChild(i).name == name) same++;
                if (same > 0) name += "#" + same;
            }
            names.Add(name);
        }
        names.Reverse();
        return string.Join("/", names);
    }

    public static Vector3 RootPoint(Transform root, Vector3 world) => root.InverseTransformPoint(world);

    /// <summary>A mesh's box in the root's frame (8 corners carried across).</summary>
    public static Bounds InRoot(Transform root, Mesh mesh, Transform at)
    {
        Bounds b = mesh.bounds;
        Matrix4x4 m = root.worldToLocalMatrix * at.localToWorldMatrix;
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    /// <summary>Every mesh under <paramref name="piece"/> (switched off ones too), as one box in the root's frame.</summary>
    public static bool PieceBounds(Transform root, Transform piece, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.transform is RectTransform || mf.GetComponentInParent<JobMarker>(true) != null) continue;
            Bounds b = InRoot(root, mf.sharedMesh, mf.transform);
            if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
        }
        return any;
    }

    static bool ActiveIn(Transform t, Transform root)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (!x.gameObject.activeSelf) return false;
            if (x == root) break;
        }
        return true;
    }

    /// <summary>The device's lowest physical point under its anchor (root frame; negative = below): its switched-on mesh
    /// renderers' boxes, the job tag's text left out. The intake tray check's own measure (Playtest4TraySteps.Devices).</summary>
    public static float Bottom(GameObject root)
    {
        float bottom = float.MaxValue;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !ActiveIn(mf.transform, root.transform) || mf.transform is RectTransform) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;
            bottom = Mathf.Min(bottom, InRoot(root.transform, mf.sharedMesh, mf.transform).min.y);
        }
        return bottom == float.MaxValue ? 0f : bottom;
    }

    /// <summary>The device's own solid parts' box in its root frame (DeviceBounds' rule: what's drawn of it, no tag).</summary>
    public static bool Footprint(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!DeviceBounds.Counts(r)) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = InRoot(root.transform, mf.sharedMesh, mf.transform);
            if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
        }
        return any;
    }

    public static float HeadHeight(Transform screw)
    {
        BoxCollider box = screw.GetComponent<BoxCollider>();
        return box != null ? box.size.y * Mathf.Abs(screw.lossyScale.y) : 0f;
    }

    public static float HeadWidth(Transform screw)
    {
        BoxCollider box = screw.GetComponent<BoxCollider>();
        return box != null ? Mathf.Max(box.size.x * Mathf.Abs(screw.lossyScale.x), box.size.z * Mathf.Abs(screw.lossyScale.z)) : 0f;
    }

    /// <summary>Where a screw's head meets its surface (root frame): its centre less half its height, along the way out of the
    /// device (Screw.Awake's rule: away from the middle of the device's other parts).</summary>
    public static Vector3 Contact(GameObject root, Transform screw, float headHeight)
    {
        Vector3 up = root.transform.InverseTransformDirection(screw.up).normalized;
        Vector3 p = RootPoint(root.transform, screw.position);
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r.transform == screw || !DeviceBounds.Counts(r)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        Vector3 centre = any ? RootPoint(root.transform, b.center) : Vector3.zero;
        float sign = Vector3.Dot(p - centre, up) < 0f ? -1f : 1f;
        return p - up * sign * (headHeight * .5f);
    }

    public static float Serialized(Object o, string field)
    {
        if (o == null) return float.NaN;
        SerializedProperty p = new SerializedObject(o).FindProperty(field);
        return p != null && p.propertyType == SerializedPropertyType.Float ? p.floatValue : float.NaN;
    }

    // ======================================================================= the "before" snapshot

    [Serializable]
    public sealed class SnapItem
    {
        public string path;
        public Vector3 local, root, size;   // its local place, its place in the root's frame, its size (lossy scale)
        public bool shaped, marker, screw;  // a stand-in shaped by its own scale; under the job tag; a screw
        public Vector3 contact;             // a screw: where its head meets its surface (root frame)
        public float dome;                  // a grime spot's domeRadius
    }

    [Serializable]
    public sealed class Snap
    {
        public string prefab, taken;
        public bool realSize;
        public float restHeight, bottom;
        public List<SnapItem> items = new List<SnapItem>();
    }

    public static string SnapPath(string prefab) => Path.Combine("Logs", "Bench", "scale", Path.GetFileNameWithoutExtension(prefab) + "-before.json");

    public static Snap Take(GameObject root, string prefab)
    {
        var def = root.GetComponent<DeviceDefinition>();
        var job = root.GetComponent<JobBase>();
        var snap = new Snap
        {
            prefab = prefab, taken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            realSize = def != null && def.realSize, restHeight = job != null ? job.restHeight : 0f, bottom = Bottom(root),
        };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;
            var item = new SnapItem
            {
                path = PathIn(root.transform, t), local = t.localPosition, root = RootPoint(root.transform, t.position), size = t.lossyScale,
                shaped = !IsUnit(t.localScale), marker = t.GetComponentInParent<JobMarker>(true) != null,
            };
            if (t.GetComponent<Screw>() != null) { item.screw = true; item.contact = Contact(root, t, HeadHeight(t)); }
            GrimeSpot spot = t.GetComponent<GrimeSpot>();
            if (spot != null) item.dome = Serialized(spot, "domeRadius");
            snap.items.Add(item);
        }
        return snap;
    }

    public static void Save(Snap snap)
    {
        string path = SnapPath(snap.prefab);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(snap, true));
    }

    public static Snap LoadSnap(string prefab)
    {
        string path = SnapPath(prefab);
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<Snap>(File.ReadAllText(path)); }
        catch (Exception) { return null; }
    }

    /// <summary>Every object of <paramref name="root"/> against the snapshot at × k: its place (a screw: its head's face on its
    /// surface), a stand-in's size, a grime spot's dome; the job tag's subtree unmoved. With <paramref name="movedTo"/> (v3's
    /// places, Manifest.MovedTo), a moved object is expected exactly at its new place and whatever is under it is carried with
    /// it (the camera's blades under its mechanism). Returns the misses, worst first.</summary>
    public static List<string> Compare(Snap before, GameObject root, float k, out int matched, IReadOnlyDictionary<string, Vector3> movedTo = null)
    {
        var misses = new List<(float err, string line)>();
        matched = 0;
        var now = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root.transform) now[PathIn(root.transform, t)] = t;
        // v3's moved objects as the snapshot had them (by name: the last part of the path; the first of a name wins, as Find).
        var movedItems = new Dictionary<string, SnapItem>(StringComparer.Ordinal);
        if (movedTo != null)
            foreach (SnapItem item in before.items)
            {
                string name = LastName(item.path);
                if (movedTo.ContainsKey(name) && !movedItems.ContainsKey(name)) movedItems[name] = item;
            }
        foreach (SnapItem item in before.items)
        {
            if (!now.TryGetValue(item.path, out Transform t)) { misses.Add((1f, $"{item.path}: gone (it was in the snapshot)")); continue; }
            matched++;
            if (item.marker)
            {
                float e = (t.localPosition - item.local).magnitude;
                if (e > PlaceTolerance) misses.Add((e, $"{item.path}: the job tag moved {Mm(e)} (it is skipped by the bake)"));
                continue;
            }
            // v3: moved itself (exactly at its new place, a screw included), or carried by the nearest moved object above it.
            bool exact = false;
            Vector3 exactAt = default, carried = Vector3.zero;
            if (movedItems.Count > 0)
            {
                string own = LastName(item.path);
                if (movedItems.TryGetValue(own, out SnapItem self) && self == item) { exact = true; exactAt = movedTo[own]; }
                else
                    for (string up = Parent(item.path); up != null; up = Parent(up))
                    {
                        string name = LastName(up);
                        if (movedItems.TryGetValue(name, out SnapItem above) && above.path == up) { carried = movedTo[name] - above.root * k; break; }
                    }
            }
            if (exact)
            {
                Vector3 at = RootPoint(root.transform, t.position);
                float e = (at - exactAt).magnitude;
                if (e > PlaceTolerance) misses.Add((e, $"{item.path}: {Mm(e)} off its v3 place ({V(exactAt)} → {V(at)})"));
            }
            else if (item.screw)
            {
                Vector3 contact = Contact(root, t, HeadHeight(t));
                Vector3 want = item.contact * k + carried;
                float e = (contact - want).magnitude;
                if (e > PlaceTolerance) misses.Add((e, $"{item.path}: its head meets its surface {Mm(e)} off before × k ({V(want)} → {V(contact)})"));
                continue;
            }
            else
            {
                Vector3 p = RootPoint(root.transform, t.position), want = item.root * k + carried;
                float err = (p - want).magnitude;
                if (err > PlaceTolerance) misses.Add((err, $"{item.path}: {Mm(err)} off before × k{(carried != Vector3.zero ? " (carried with what it is under)" : "")} ({V(want)} → {V(p)})"));
            }
            if (item.screw) continue;
            if (item.shaped)
            {
                Vector3 want = item.size * k, have = t.lossyScale;
                float rel = Mathf.Max(Mathf.Abs(have.x / Mathf.Max(1e-7f, want.x) - 1f), Mathf.Abs(have.y / Mathf.Max(1e-7f, want.y) - 1f), Mathf.Abs(have.z / Mathf.Max(1e-7f, want.z) - 1f));
                if (rel > .01f) misses.Add((rel, $"{item.path}: its size {V(have)}, before × k {V(want)}"));
            }
            GrimeSpot spot = t.GetComponent<GrimeSpot>();
            if (spot != null && item.dome > 0f)
            {
                float dome = Serialized(spot, "domeRadius");
                if (Mathf.Abs(dome - item.dome * k) > PlaceTolerance) misses.Add((Mathf.Abs(dome - item.dome * k), $"{item.path}: domeRadius {dome:0.0000}, before × k {item.dome * k:0.0000}"));
            }
        }
        return misses.OrderByDescending(m => m.err).Select(m => m.line).ToList();
    }

    // A snapshot path's last name (PathIn's "#n" for a repeated name left on), and its parent's path (null at the top).
    static string LastName(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? path : path.Substring(slash + 1); }
    static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? null : path.Substring(0, slash); }

    // ======================================================================= photos (a preview scene: the café scene is never touched)

    public sealed class Studio : IDisposable
    {
        public readonly PreviewRenderUtility Utility = new PreviewRenderUtility();
        readonly List<Object> made = new List<Object>();

        public Studio()
        {
            Camera cam = Utility.camera;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.20f, .19f, .18f);
            // The bench lamp's warm white (#FFE4C2) from above the mat, and a dim fill from behind.
            Utility.lights[0].intensity = 1.15f;
            Utility.lights[0].color = new Color(1f, .894f, .761f);
            Utility.lights[0].transform.rotation = Quaternion.Euler(68f, 12f, 0f);
            Utility.lights[1].intensity = .45f;
            Utility.lights[1].transform.rotation = Quaternion.Euler(35f, -150f, 0f);
            Utility.ambientColor = new Color(.32f, .31f, .30f);
        }

        /// <summary>A copy of a mesh for the photo only (never in the open scene).</summary>
        public GameObject Proxy(string name, Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            Utility.AddSingleGO(go);
            made.Add(go);
            return go;
        }

        public Material Flat(Color c)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(lit != null ? lit : Shader.Find("Standard")) { color = c };
            made.Add(m);
            return m;
        }

        public GameObject Place(GameObject prefab) => prefab != null ? Utility.InstantiatePrefabInScene(prefab) : null;

        /// <summary>Renders from a pose (vertical FOV, as Unity's cameras); the texture's size may be the request × the screen's
        /// point scale, so callers draw on it at its own width and height.</summary>
        public Texture2D Shoot(Vector3 from, Quaternion rotation, float fov, int width, int height)
        {
            Camera cam = Utility.camera;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(from, rotation);
            Utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
            Utility.Render(true, false);
            Utility.Render(true, false);
            return Utility.EndStaticPreview();
        }

        public void Dispose()
        {
            Utility.Cleanup();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
        }
    }

    /// <summary>An outline on a picture's pixels (origin bottom left, as Unity's screen space).</summary>
    public static void Outline(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, Color32 c, int thick)
    {
        int ax = Mathf.Clamp(Mathf.RoundToInt(x0), 0, w - 1), bx = Mathf.Clamp(Mathf.RoundToInt(x1), 0, w - 1);
        int ay = Mathf.Clamp(Mathf.RoundToInt(y0), 0, h - 1), by = Mathf.Clamp(Mathf.RoundToInt(y1), 0, h - 1);
        for (int t = 0; t < thick; t++)
        {
            for (int x = ax; x <= bx; x++)
            {
                if (ay + t < h) px[(ay + t) * w + x] = c;
                if (by - t >= 0) px[(by - t) * w + x] = c;
            }
            for (int y = ay; y <= by; y++)
            {
                if (ax + t < w) px[y * w + ax + t] = c;
                if (bx - t >= 0) px[y * w + bx - t] = c;
            }
        }
    }

    public static void SavePng(Texture2D tex, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }

    // ======================================================================= words

    public static string Mm(float metres) => (metres * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " mm";
    public static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.0000}, {1:0.0000}, {2:0.0000})", v.x, v.y, v.z);
    public static string MmV(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:0.0} × {1:0.0} × {2:0.0} mm", v.x * 1000f, v.y * 1000f, v.z * 1000f);
}

internal static class BenchScaleAudit
{
    const string Title = "Bench scale audit (read-only)";

    [MenuItem("Fixit Fidget/Checks/Bench scale audit (read-only)")]
    static void Audit()
    {
        var run = new BenchScale.Run(Title + ": one scale at the bench (td-one-scale.md §3)", "scale-audit");
        bool error = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            BenchScale.Manifest manifest = BenchScale.Manifest.Load(out string note);
            run.Info(note);
            float driver = ToolLongest("BT_Screwdriver", out _);
            run.Info(driver > 0f ? $"The screwdriver (BT_Screwdriver.fbx) is {BenchScale.Mm(driver)} long: every device's ratio is against it." : "BT_Screwdriver.fbx isn't imported: the ratios are skipped.");
            foreach (BenchScale.Device d in BenchScale.Devices) AuditDevice(d, manifest, driver, run);
            AuditTools(run);
            AuditStage(run);
        }
        catch (Exception e)
        {
            error = true;
            run.Info("ERROR " + e.Message);
            run.Info(e.ToString());
        }
        // A FAIL row is the audit's answer, not an error: before Devices 2 the camera, watch, screw and hole rows fail by design.
        run.Finish(error ? "ERROR: the audit stopped (see above)."
                         : $"{run.Passes} PASS, {run.Fails} FAIL (read-only). Before Devices 2 the camera, watch, screw and hole rows FAIL by design; after it and Bench 9, every row passes.",
                   error);
    }

    [MenuItem("Fixit Fidget/Checks/Bench scale audit (read-only)", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ------------------------------------------------------------------ a device

    static void AuditDevice(BenchScale.Device d, BenchScale.Manifest manifest, float driver, BenchScale.Run run)
    {
        run.Section($"{d.label}: {d.prefab} (the bake's factor k = {d.k:0.###})");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) == null) { run.Check(false, $"{d.prefab} is in the project"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(d.prefab);
        try
        {
            var def = root.GetComponent<DeviceDefinition>();
            var job = root.GetComponent<JobBase>();
            bool real = def != null && def.realSize;
            run.Info($"DeviceDefinition.realSize {real}; JobBase.restHeight {(job != null ? job.restHeight : float.NaN):0.0000} m");

            // ---- the body (the object wearing the FBX's root mesh) ----
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(d.model);
            Mesh bodyMesh = BenchScale.RootMesh(fbx);
            MeshFilter body = bodyMesh == null ? null : root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.sharedMesh == bodyMesh);
            if (body == null) run.Check(false, $"the body: no object wears {d.model}'s root mesh (Devices 1, then Devices 2)");
            else
            {
                Vector3 s = BenchScale.InRoot(root.transform, bodyMesh, body.transform).size;
                switch (d.label)
                {
                    case "phone":
                        Real(run, "the phone's body across", s.x, .0716f, .0716f);
                        Real(run, "the phone's body along", s.z, .1476f, .1476f);
                        Real(run, "the phone's body thick", s.y, .0078f, .0078f);
                        break;
                    case "watch":
                        Real(run, "the watch's case across (Ø)", s.x, .048f, .052f);
                        run.Info($"the watch's case is {BenchScale.Mm(s.y)} thick with its back (real 12–16 mm): built 2.2x in thickness, not 3x (TD §3); the TD keeps it");
                        break;
                    case "camera":
                        // Across is what says 2x or real. Its height and depth carry the top plate's controls and Art A6's
                        // deepening (23 → 32 mm), so they are reported against the TD's "after", not checked.
                        Real(run, "the camera's body across, with its lugs", s.x, .129f, .129f);
                        run.Info($"the camera's body is {BenchScale.Mm(s.z)} tall with its controls and {BenchScale.Mm(s.y)} deep (the TD's after: 80 and 32 mm; a Canonet is 75 × 32)");
                        break;
                }
            }

            // ---- screws and their holes ----
            Screw[] screws = root.GetComponentsInChildren<Screw>(true);
            if (screws.Length > 0)
            {
                var heads = new List<string>();
                bool ok = true;
                foreach (Screw screw in screws)
                {
                    float w = BenchScale.HeadWidth(screw.transform), h = BenchScale.HeadHeight(screw.transform);
                    MeshFilter mf = screw.GetComponent<MeshFilter>();
                    float drawn = mf != null && mf.sharedMesh != null ? Mathf.Max(mf.sharedMesh.bounds.size.x, mf.sharedMesh.bounds.size.z) * Mathf.Abs(screw.transform.lossyScale.x) : 0f;
                    ok &= Mathf.Abs(w - BenchScale.ScrewHead) <= BenchScale.HeadTolerance && Mathf.Abs(h - BenchScale.ScrewHeadHeight) <= BenchScale.HeadTolerance
                          && Mathf.Abs(drawn - BenchScale.ScrewHead) <= BenchScale.HeadTolerance;
                    heads.Add($"{screw.name} head box {BenchScale.Mm(w)} × {BenchScale.Mm(h)}, drawn {BenchScale.Mm(drawn)}");
                }
                run.Check(ok, $"{screws.Length} screws at the Producer's head, Ø {BenchScale.Mm(BenchScale.ScrewHead)} × {BenchScale.Mm(BenchScale.ScrewHeadHeight)} (± 0.1 mm): {string.Join("; ", heads)}");
                Hole(run, root, screws[0]);
            }

            // ---- the pieces, against their real sizes ----
            switch (d.label)
            {
                case "phone":
                    foreach (ReplaceablePart part in root.GetComponentsInChildren<ReplaceablePart>(true)) Flat(run, root, part.transform, "the phone's screen (the part)", .064f, .140f);
                    foreach (RemovablePart cover in root.GetComponentsInChildren<RemovablePart>(true)) Flat(run, root, cover.transform, "the phone's back cover", .064f, .140f);
                    break;
                case "watch":
                    foreach (RemovablePart cover in root.GetComponentsInChildren<RemovablePart>(true)) Round(run, root, cover.transform, "the watch's back plate (Ø)", .043f, .043f);
                    Transform crown = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Crown");
                    if (crown != null && BenchScale.PieceBounds(root.transform, crown, out Bounds cb)) Real(run, "the watch's crown (Ø)", cb.size.x, .007f, .009f);
                    foreach (ReplaceablePart part in root.GetComponentsInChildren<ReplaceablePart>(true)) Round(run, root, part.transform, "the watch's mainspring (Ø)", .015f, .017f);
                    break;
                case "camera":
                    Transform lens = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Lens housing");
                    if (lens != null && BenchScale.PieceBounds(root.transform, lens, out Bounds lb))
                        run.Info($"the camera's lens barrel is Ø {BenchScale.Mm(Mathf.Max(lb.size.x, lb.size.z))} (real 50–52 mm at the body): 0.73x real at half scale, Art's call (TD §3)");
                    // Grace's piece: it must still be there by its name (BenchLab checks it). Its width is a look (Art A6 drapes
                    // it in an S, so a box round a run isn't its width): reported, judged in the photos.
                    Transform strap = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("KEEP", StringComparison.Ordinal));
                    if (strap == null) run.Check(false, "the camera's strap: no \"KEEP - scratched sentimental strap\" (BenchLab checks it by that name)");
                    else
                    {
                        var runs = new List<string>();
                        foreach (MeshFilter mf in strap.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (mf.sharedMesh == null || !BenchScale.IsUnit(mf.transform.localScale)) continue;   // the scratches are stand-ins
                            Vector3 s = BenchScale.InRoot(root.transform, mf.sharedMesh, mf.transform).size;
                            runs.Add($"{mf.name} {BenchScale.Mm(Mathf.Min(s.x, s.z))}");
                        }
                        run.Info($"the camera's strap (\"{strap.name}\"): its runs' narrow sides {string.Join(", ", runs)} (Art A6: 14 mm leather, real 12–15)");
                    }
                    foreach (ReplaceablePart part in root.GetComponentsInChildren<ReplaceablePart>(true))
                        if (BenchScale.PieceBounds(root.transform, part.transform, out Bounds mb))
                            run.Info($"the camera's shutter mechanism ({part.name}) is {BenchScale.MmV(mb.size)} with its lever (a leaf shutter is Ø 30–35 mm; the TD's after for its chassis: 36.5 × 24.5 × 15)");
                    break;
            }
            foreach (GrimeSpot spot in root.GetComponentsInChildren<GrimeSpot>(true))
            {
                Vector3 s = spot.transform.lossyScale;
                float dome = BenchScale.Serialized(spot, "domeRadius");
                run.Info($"grime {spot.name}: {BenchScale.Mm(s.x)} × {BenchScale.Mm(s.z)}{(dome > 0f ? $", dome r {dome:0.0000} m" : "")} (a patch: checked as before × k below)");
            }

            // ---- the device against the screwdriver ----
            if (driver > 0f && BenchScale.Footprint(root, out Bounds foot))
            {
                float length = Mathf.Max(foot.size.x, foot.size.z), ratio = length / driver;
                run.Check(Mathf.Abs(ratio / d.driverRatio - 1f) <= BenchScale.RealTolerance,
                    $"its length ÷ the screwdriver's: {BenchScale.Mm(length)} ÷ {BenchScale.Mm(driver)} = {ratio:0.00} (real {d.driverRatio:0.00}, ±7 %)");
            }

            // ---- every object against the snapshot taken before the bake ----
            if (!real)
            {
                BenchScale.Snap snap = BenchScale.Take(root, d.prefab);
                BenchScale.Save(snap);
                run.Info($"the \"before\" snapshot: {snap.items.Count} objects, saved to {BenchScale.SnapPath(d.prefab)} (the \"after\" audit compares against it)");
            }
            else
            {
                BenchScale.Snap snap = BenchScale.LoadSnap(d.prefab);
                if (snap == null) run.Check(false, $"every object at before × k: no snapshot at {BenchScale.SnapPath(d.prefab)} (the audit or Devices 2 writes it before the bake)");
                else
                {
                    // v3's moves and scratches (devices.json): those objects are expected at their new places (Devices 2 puts them there).
                    Dictionary<string, Vector3> movedTo = manifest != null ? manifest.MovedTo(manifest.Piece(Path.GetFileNameWithoutExtension(d.model))) : null;
                    List<string> misses = BenchScale.Compare(snap, root, d.k, out int matched, movedTo);
                    run.Check(misses.Count == 0, $"every object at before × k ± 0.1 mm ({matched} of {snap.items.Count} from the snapshot of {snap.taken}; a screw by where its head meets its surface" +
                                                 $"{(movedTo != null && movedTo.Count > 0 ? $"; {movedTo.Count} at v3's new places, what is under them carried" : "")})" +
                                                 (misses.Count > 0 ? ": " + string.Join("; ", misses.Take(10)) + (misses.Count > 10 ? $"; and {misses.Count - 10} more" : "") : ""));
                }
            }

            // ---- where it rests on the intake tray's felt ----
            if (job != null)
            {
                float bottom = BenchScale.Bottom(root), over = job.restHeight + bottom - BenchScale.FeltOverSlot;
                run.Check(over >= BenchScale.FeltBandLow && over <= BenchScale.FeltBandHigh,
                    $"on the intake tray it rests {over * 1000f:+0.0;-0.0} mm over the felt (rest height {job.restHeight:0.0000} m, its lowest point {BenchScale.Mm(-bottom)} under its anchor, the felt 3.0 mm over the slot; 0–1.2 mm wanted). The intake tray check measures the felt itself");
            }

            // ---- from above, in Ace's hands ----
            if (BenchScale.Footprint(root, out Bounds all))
            {
                float longest = Mathf.Max(all.size.x, Mathf.Max(all.size.y, all.size.z));
                float scale = Mathf.Min(BenchScale.OverheadCap, Mathf.Max(BenchScale.OverheadFloor, BenchScale.OverheadLength / Mathf.Max(1e-4f, longest)));
                run.Check(longest * scale >= BenchScale.OverheadLength - 1e-4f || scale >= BenchScale.OverheadCap - 1e-4f,
                    $"carried, from above it is drawn {longest * scale:0.00} m long ({scale:0.0}x its {BenchScale.Mm(longest)}: PlayerCarry draws a device at least 0.45 m, at most 7x)");
            }

            // ---- its close-up framing (Bench 9) ----
            if (def != null)
            {
                var layout = def.benchLayout;
                bool framed = layout.cameraDistance > 0f && layout.fovClose > 0f;
                run.Check(framed, framed
                    ? $"framed by Bench 9: aim ({layout.cameraAim.x:0}, {layout.cameraAim.y:0}) mm at {layout.cameraDistance:0.000} m, FOV 40 → zoom limit aim ({layout.closeAim.x:0}, {layout.closeAim.y:0}) at FOV {layout.fovClose:0.0}"
                    : "framed by Bench 9 (BenchLayout.cameraDistance and fovClose are 0: the bench keeps the scene's framing)");
            }

            // ---- the metre fields on its pieces (Systems makes them relative or deletes them) ----
            var metres = new List<string>();
            foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null) continue;
                foreach (string field in new[] { "seatDistance", "snapDistance", "threadLength", "liftHeight" })
                {
                    float v = BenchScale.Serialized(b, field);
                    if (!float.IsNaN(v)) metres.Add($"{b.name}.{b.GetType().Name}.{field} {v:0.0000}");
                }
            }
            if (metres.Count > 0) run.Info("metre fields: " + string.Join("; ", metres.Distinct()));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // A size against a real range, ±7 % beyond it.
    static void Real(BenchScale.Run run, string what, float have, float low, float high)
    {
        bool ok = have >= low * (1f - BenchScale.RealTolerance) && have <= high * (1f + BenchScale.RealTolerance);
        string real = Mathf.Approximately(low, high) ? BenchScale.Mm(low) : $"{low * 1000f:0.#}–{high * 1000f:0.#} mm";
        run.Check(ok, $"{what}: {BenchScale.Mm(have)} (real {real}, ±7 %)");
    }

    // A flat part's two sides (across, along) against real ones.
    static void Flat(BenchScale.Run run, GameObject root, Transform piece, string what, float across, float along)
    {
        if (!BenchScale.PieceBounds(root.transform, piece, out Bounds b)) { run.Check(false, what + ": no mesh"); return; }
        float a = Mathf.Min(b.size.x, b.size.z), l = Mathf.Max(b.size.x, b.size.z);
        bool ok = Mathf.Abs(a / across - 1f) <= BenchScale.RealTolerance && Mathf.Abs(l / along - 1f) <= BenchScale.RealTolerance;
        run.Check(ok, $"{what} ({piece.name}): {BenchScale.Mm(a)} × {BenchScale.Mm(l)} (real ≈ {across * 1000f:0} × {along * 1000f:0} mm, ±7 %)");
    }

    // A round or small part's longest flat side against a real range.
    static void Round(BenchScale.Run run, GameObject root, Transform piece, string what, float low, float high)
    {
        if (!BenchScale.PieceBounds(root.transform, piece, out Bounds b)) { run.Check(false, what + ": no mesh"); return; }
        Real(run, $"{what} ({piece.name})", Mathf.Max(b.size.x, b.size.z), low, high);
    }

    // The hole a screw leaves, as the game makes it (ScrewSocket.Make, the bench builder's decal): made on the loaded copy,
    // measured, thrown away with it. Read by reflection so a changed signature reports itself instead of breaking the build.
    static void Hole(BenchScale.Run run, GameObject root, Screw screw)
    {
        try
        {
            Type socket = typeof(Screw).Assembly.GetType("ScrewSocket");
            MethodInfo make = socket != null ? socket.GetMethod("Make", BindingFlags.Public | BindingFlags.Static) : null;
            if (make == null || make.GetParameters().Length != 4) { run.Info("the hole: ScrewSocket.Make(screw, parent, localPos, localRot) isn't there to call: measure it in Bench 3's photos"); return; }
            MethodInfo awake = typeof(Screw).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (awake != null) awake.Invoke(screw, null);   // the head's size is read in Awake, as in play
            Transform t = screw.transform;
            var made = make.Invoke(null, new object[] { screw, t.parent, t.localPosition, t.localRotation }) as Component;
            if (made == null) { run.Info("the hole: ScrewSocket.Make made nothing"); return; }
            bool any = false;
            Bounds b = default;
            foreach (MeshFilter mf in made.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds one = BenchScale.InRoot(root.transform, mf.sharedMesh, mf.transform);
                if (!any) { b = one; any = true; } else b.Encapsulate(one);
            }
            Object.DestroyImmediate(made.gameObject);
            float head = BenchScale.HeadWidth(t), drawn = any ? Mathf.Max(b.size.x, b.size.z) : 0f;
            run.Check(any && Mathf.Abs(drawn - BenchScale.HoleDecal * head) <= BenchScale.HeadTolerance,
                $"the hole {screw.name} leaves is drawn {BenchScale.Mm(drawn)} across = {(head > 0f ? drawn / head : 0f):0.00} × its head (Art's decal: 1.15 × head = {BenchScale.Mm(BenchScale.HoleDecal * head)})");
        }
        catch (Exception e)
        {
            run.Info($"the hole: couldn't be made in Edit mode ({(e.InnerException ?? e).Message}): measure it in Bench 3's photos");
        }
    }

    // ------------------------------------------------------------------ the tools

    static float ToolLongest(string file, out Vector3 size)
    {
        size = Vector3.zero;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(BenchScale.ToolModels + "/" + file + ".fbx");
        if (asset == null) return 0f;
        bool any = false;
        Bounds b = default;
        foreach (MeshFilter mf in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds one = BenchScale.InRoot(asset.transform, mf.sharedMesh, mf.transform);
            if (!any) { b = one; any = true; } else b.Encapsulate(one);
        }
        size = any ? b.size : Vector3.zero;
        return any ? Mathf.Max(size.x, Mathf.Max(size.y, size.z)) : 0f;
    }

    static void AuditTools(BenchScale.Run run)
    {
        run.Section("The tools (Assets/Art/Models/BenchTools; real sizes from the reference sheet §1–5)");
        foreach (var (file, what, low, high) in new[]
                 {
                     ("BT_Screwdriver", "the screwdriver (PicoFinish PH00: 134 mm)", .134f, .134f), ("BT_Tweezers", "the tweezers", .110f, .140f),
                     ("BT_Brush", "the brush", .140f, .230f), ("BT_PryTool", "the pry tool", .125f, .125f),
                 })
        {
            float longest = ToolLongest(file, out _);
            if (longest <= 0f) { run.Info($"{file}.fbx isn't imported"); continue; }
            Real(run, what, longest, low, high);
        }
        float cloth = ToolLongest("BT_Cloth", out Vector3 cs);
        if (cloth > 0f)
        {
            float a = Mathf.Min(cs.x, cs.z), l = Mathf.Max(cs.x, cs.z);
            run.Check(Mathf.Abs(a / .075f - 1f) <= BenchScale.RealTolerance && Mathf.Abs(l / .090f - 1f) <= BenchScale.RealTolerance,
                $"the cloth folded: {BenchScale.Mm(a)} × {BenchScale.Mm(l)} (a 15 × 18 cm lens cloth folded in four: 75 × 90 mm, ±7 %)");
        }
        // The driver's tip cross is geometry at its tip: the export's manifest says it, or it isn't measured here.
        string manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", BenchScale.ToolModels, "bench_tools.json"));
        double tip = double.NaN;
        if (File.Exists(manifest))
        {
            try
            {
                if (BenchScale.Json(File.ReadAllText(manifest)) is Dictionary<string, object> m && m.TryGetValue("pieces", out object ps) && ps is List<object> pieces)
                    foreach (object p in pieces)
                        if (p is Dictionary<string, object> piece && piece.TryGetValue("name", out object n) && (n as string) == "BT_Screwdriver")
                            foreach (string key in new[] { "tip_cross", "tipCross" })
                                if (piece.TryGetValue(key, out object v) && v is double dv) tip = dv;
            }
            catch (Exception) { }
        }
        float want = BenchScale.DriverTip * BenchScale.ScrewHead;
        if (double.IsNaN(tip)) run.Info($"the driver's tip cross isn't in bench_tools.json: the Blender lead's wave 2 sets it to 0.6 × head = {BenchScale.Mm(want)} (bench_tools.py:62-63; 2.6 mm today)");
        else run.Check(Mathf.Abs((float)tip - want) <= BenchScale.HeadTolerance, $"the driver's tip cross: {BenchScale.Mm((float)tip)} (0.6 × head = {BenchScale.Mm(want)})");
    }

    // ------------------------------------------------------------------ the stage (the café scene, if it is open)

    static void AuditStage(BenchScale.Run run)
    {
        run.Section("The stage (the café scene's BenchStage, if the scene is open) and the zones it must leave free");
        if (SceneManager.GetActiveScene().path != BenchScale.CafeScene) { run.Info($"Open {BenchScale.CafeScene} to audit the mat, the rest, the scrap tray and the zones."); return; }
        BenchStage[] stages = Object.FindObjectsByType<BenchStage>(FindObjectsInactive.Include);
        if (stages.Length != 1) { run.Check(false, $"one BenchStage in the scene ({stages.Length} found)"); return; }
        BenchStage stage = stages[0];
        if (stage.matFrame == null) { run.Check(false, "BenchStage.matFrame is set (Bench 7, the forgiving stage, wires it)"); return; }
        Matrix4x4 toMat = Matrix4x4.TRS(stage.matFrame.position, stage.matFrame.rotation, Vector3.one).inverse;
        Size(run, "the mat (Art A1: 600 × 360)", stage.matFrame, toMat, .600f, .360f);
        Size(run, "the tool rest (Art A2: 110 × 230)", stage.toolRest, toMat, .110f, .230f);
        Size(run, "the scrap tray (Art A3: 160 × 90)", stage.scrapTray, toMat, .160f, .090f);
        int cups = stage.cups != null ? stage.cups.Count(c => c != null) : 0;
        run.Check(cups == 6, $"six screw cups ({cups})");
        run.Check(stage.padSmall != null && stage.padLarge != null && stage.wellSmall != null && stage.wellLarge != null, "the two pads and the two wells");
        int seats = stage.toolSlots != null ? stage.toolSlots.Count(s => s != null) : 0;
        run.Check(seats == 5, $"five tool seats ({seats})");

        // UI/UX's stage contract (§5.8) at 1080p, as BenchStageContract gives it (Unity's screen space, origin bottom left).
        var table = new Dictionary<BenchStageContract.Zone, Rect>
        {
            { BenchStageContract.Zone.U1, Rect.MinMaxRect(24, 1080 - 64, 1408, 1080 - 20) },
            { BenchStageContract.Zone.U2, Rect.MinMaxRect(1596, 1080 - 110, 1896, 1080 - 18) },
            { BenchStageContract.Zone.U3, Rect.MinMaxRect(1416, 1080 - 300, 1896, 1080 - 132) },
            { BenchStageContract.Zone.U4, Rect.MinMaxRect(24, 1080 - 1060, 256, 1080 - 864) },
            { BenchStageContract.Zone.U5, Rect.MinMaxRect(1496, 1080 - 1054, 1896, 1080 - 740) },
            { BenchStageContract.Zone.U6, Rect.MinMaxRect(670, 1080 - 1054, 1250, 1080 - 966) },
        };
        foreach (BenchStageContract.Zone zone in BenchStageContract.All)
        {
            Rect r = BenchStageContract.Rect(zone, 1920, 1080), r10 = BenchStageContract.Rect(zone, 2560, 1600);
            Rect want = table[zone];
            bool same = Mathf.Abs(r.xMin - want.xMin) <= 1f && Mathf.Abs(r.xMax - want.xMax) <= 1f && Mathf.Abs(r.yMin - want.yMin) <= 1f && Mathf.Abs(r.yMax - want.yMax) <= 1f;
            run.Check(same, $"{zone} at 1920 × 1080: x {r.xMin:0}–{r.xMax:0}, y {r.yMin:0}–{r.yMax:0} from the bottom (UI/UX §5.8: x {want.xMin:0}–{want.xMax:0}, y {want.yMin:0}–{want.yMax:0}); at 2560 × 1600: x {r10.xMin:0}–{r10.xMax:0}, y {r10.yMin:0}–{r10.yMax:0}");
        }
    }

    static void Size(BenchScale.Run run, string what, Transform t, Matrix4x4 toMat, float across, float deep)
    {
        if (t == null) { run.Check(false, what + ": not wired on BenchStage"); return; }
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || r.GetComponentInParent<ToolPickup>(true) != null) continue;
            Bounds m = mf.sharedMesh.bounds;
            Matrix4x4 to = toMat * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = to.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? m.min.x : m.max.x, (i & 2) == 0 ? m.min.y : m.max.y, (i & 4) == 0 ? m.min.z : m.max.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        if (!any) { run.Info(what + ": nothing drawn under it"); return; }
        run.Check(Mathf.Abs(b.size.x - across) <= .012f && Mathf.Abs(b.size.z - deep) <= .012f,
            $"{what}: {BenchScale.Mm(b.size.x)} × {BenchScale.Mm(b.size.z)} in the mat's frame (± 12 mm)");
    }
}
#endif
