#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE HERO DEVICES INTO THEIR PREFABS, AT REAL SIZE (8-9 Oct 2026; one scale, 14 Oct: specs/td-one-scale.md §6.4)
//
//   Fixit Fidget > Bench > Devices 2 - Fit the hero devices at real size (PhoneRepair, PocketWatch, GraceReunionCamera)
//
// His note (14 Oct, 00:30): "ENOURMOUSE HOLES for the screwdrivers ... the holes are ENERMOUSe and the screws are so tiny,
// and that doesn't mean to go say to make the screws enormous as well." The bench held three scales: the phone at 1x, Grace's
// camera at 2x, the pocket watch at 3x, against real-size tools and mat. The Blender lead's wave 2 exports all three at real
// size (1 unit = 1 m, the FBX names unchanged: HD_Phone, HD_Watch, HD_Camera) with screw heads at the Producer's 3.8 mm
// (feel targets §2.3). This step puts those meshes in and BAKES each prefab to real size:
//
//   1. THE FIT (as on 9 Oct): each FBX piece's mesh goes into the prefab objects devices.json's "targets" names (this step's
//      own table when the manifest has none): the object's scale set to 1, its box collider fitted to the mesh (a screw's to its
//      HEAD only: Ø 3.8 × 1.33 mm, what Screw.HeadSize reads; a cover's stops at the face the screws rest on; a hollow piece
//      gets a box per solid part; the crown keeps its capsule), the FBX's materials put on (the phone's screens keep theirs).
//      A collider is the piece's solid, never its bounds, wherever other pieces sit in a recess of it (9 Oct: the phone's
//      camera island caught the cursor before the screws; the watch's case box filled the cavity the grime sits in).
//
//   2. THE BAKE, once per prefab, by hierarchy and rule (wave 2 may rename pieces; no list of names decides it), with the
//      device's factor k (phone 1, watch 1/3, camera 1/2: the scale its prefab was AUTHORED at, whatever the FBX says):
//        * every object's place × k, as seen from the device's root (in its parent's frame: × k ÷ the scale its parents
//          gained, so nothing inside a scaled stand-in moves twice);
//        * a stand-in shaped by its own scale (grime, Dookie, smudges, scratches: a non-unit scale, no hero mesh) × k too;
//        * a hero piece (a mesh from the device's FBX, already real) and a grouping node ("KEEP - scratched sentimental
//          strap", "Screen") keep scale 1, or their children would be scaled twice;
//        * the job tag (JobMarker) and everything under it: left alone (it sizes itself on screen);
//        * a screw: its head put back where it meets its surface (the 3.8 mm head is 1.33 mm tall, not the old head × k: by
//          its centre it would hover 0.1–0.3 mm off the glass or the plate);
//        * GrimeSpot.domeRadius × k (the lens's dome, or the smudge floats at its edges); ReplaceablePart.seatDistance and
//          RemovablePart.snapDistance × k while Systems keeps them;
//        * JobBase.restHeight so the device's lowest point is 3.3 mm over the point it is set on (0.3 mm over the intake
//          tray's felt). The TD wrote "× k", but the felt doesn't scale: × k sinks the watch 1.9 mm into it, the camera 1.5.
//      Then DeviceDefinition.realSize = true. A second run says "already real" for the bake and changes nothing.
//
//   3. V3 (the integration, 14 Oct: the Blender lead's wave 2, devices.json v3; ApplyV3 below): after the bake, the moved
//      objects to their new places (the camera's front face and all on it up 9 mm, the new strap at its lugs, the loop behind
//      the lens, the scratches onto the loop, the screws' heads on their surfaces), the new "Clean shutter mechanism" made
//      from the FBX's CleanMechanism in the shutter's seat, switched off, with a box collider, no collider on the strap and
//      its scratches, and the camera's shutter wired (the clean mechanism is the fresh part and the one DeviceWake fires).
//      The rest height is then set from the moved device (the strap's loop is now its lowest point, 0.62 mm under it).
//
//   It refuses a device whose FBX isn't real yet (its body against the real one ±7 %, its screw head 3.8 ± 0.1 mm): until
//   wave 2 lands every device is refused and nothing changes. Before it changes a prefab it writes the "before" snapshot the
//   scale audit compares against (Logs/Bench/scale/), checks the result in numbers (every object at before × k ± 0.1 mm, or at
//   v3's new place with what is under it carried; the heads; the new pieces; the rest height) and saves the prefab only when
//   every check passes. Report and before/after photos (each device beside the real screwdriver, from the close-up's 58°):
//   Logs/Bench/devices2-<time>/. Prefab contents have no Undo in Unity: the undo is git (each prefab is one file) and the
//   snapshot.
//
// Run order (TD §7, stage 3): Devices 1 (the wave 2 FBX) → Bench scale audit (before) → this → Bench 9 → the audit (after).
// The days builder's step B (Fixit Fidget > Days > B, or the whole "Days 1-3 - the intro level (data)") copies the jammed
// assembly into "Shutter mechanism (resting)" for Day 1: it must run AFTER this (it refuses while the camera isn't real
// size), so the copy is of the baked, risen assembly; a copy made before the bake would keep the 2x meshes.
// ---------------------------------------------------------------------------
internal static class HeroDeviceSteps
{
    const string Menu = "Fixit Fidget/Bench/Devices 2 - Fit the hero devices at real size (PhoneRepair, PocketWatch, GraceReunionCamera)";

    // What the fit can't read off a mesh, by the prefab object's name (the names the bench's code finds them by; the FBX's
    // piece names may change in wave 2, these don't). Screws need no entry: any object with a Screw gets the head box.
    sealed class Rule
    {
        public bool keepMaterials;   // the object's own materials stay in the slots they fill (M_BrokenGlass, M_FreshGlass); a
                                     // submesh beyond them takes the FBX's (the crack on the broken screen, v3.3)
        public float? faceY;         // a cover: other pieces rest on its outer face at this local Y; its collider stops there
        public Bounds[] boxes;       // a hollow piece: one box per solid part, local metres, AT REAL SIZE
        public string createUnder;   // a target the prefab doesn't have yet is made under this object (the phone's Glass, 4b)
        public bool box;             // give it a box collider fitted to its mesh if it has none (v3's clean shutter mechanism:
                                     // the fresh part is pressed, wave 2 §5.2)
    }

    static readonly Dictionary<string, Dictionary<string, Rule>> Rules = new Dictionary<string, Dictionary<string, Rule>>
    {
        ["phone"] = new Dictionary<string, Rule>
        {
            // the body is a rail ring round a chassis; the glass sits flush INSIDE the rail, so a box round the body's bounds
            // would tie with the screen for the cursor. Its solid: the chassis under the glass and the rim (k = 1: unchanged)
            ["Body"] = new Rule { boxes = new[]
            {
                new Bounds(new Vector3(0f, .0006f, 0f), new Vector3(.063f, .0042f, .133f)),
                new Bounds(new Vector3(.0332f, .0012f, 0f), new Vector3(.0036f, .0054f, .140f)),
                new Bounds(new Vector3(-.0332f, .0012f, 0f), new Vector3(.0036f, .0054f, .140f)),
                new Bounds(new Vector3(0f, .0012f, .0682f), new Vector3(.0628f, .0054f, .0036f)),
                new Bounds(new Vector3(0f, .0012f, -.0682f), new Vector3(.0628f, .0054f, .0036f)),
            } },
            ["Broken"] = new Rule { keepMaterials = true },
            ["Fresh"] = new Rule { keepMaterials = true },
            ["Glass"] = new Rule { createUnder = "Screen" },
            // the back glass's outer face is at -3.9 mm in the phone (local -0.6), where the screw heads sit
            ["BackCover"] = new Rule { faceY = -.0006f },
        },
        ["watch"] = new Dictionary<string, Rule>
        {
            // the case is a ring with the dial sunk in its front and the movement open at its back; its solid is the pillar
            // plate and dial as two crossed slabs inside the round, and the pendant-and-bow tower. The 3x boxes ÷ 3 (TD §6.4)
            ["Cylinder"] = new Rule { boxes = new[]
            {
                new Bounds(new Vector3(0f, .000725f, 0f), new Vector3(.042f, .0052167f, .0306667f)),
                new Bounds(new Vector3(0f, .000725f, 0f), new Vector3(.0306667f, .0052167f, .042f)),
                new Bounds(new Vector3(0f, 0f, .0336667f), new Vector3(.01f, .0026667f, .0173333f)),
            } },
        },
        ["camera"] = new Dictionary<string, Rule>(),
    };

    static readonly Vector3 HeadBox = new Vector3(BenchScale.ScrewHead, BenchScale.ScrewHeadHeight, BenchScale.ScrewHead);

    [MenuItem(Menu)]
    static void Fit()
    {
        var run = new BenchScale.Run("Devices 2 - Fit the hero devices at real size", "devices2");
        bool error = false;
        int saved = 0, refused = 0, failed = 0;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit mode: stop Play first.");
            BenchScale.Manifest manifest = BenchScale.Manifest.Load(out string note);
            run.Info(note);
            foreach (BenchScale.Device d in BenchScale.Devices)
            {
                switch (Device(d, manifest, run))
                {
                    case Outcome.Saved: saved++; break;
                    case Outcome.Refused: refused++; break;
                    case Outcome.Failed: failed++; break;
                }
            }
        }
        catch (Exception e)
        {
            error = true;
            run.Info("ERROR " + e.Message);
            run.Info(e.ToString());
        }
        string verdict = error ? "ERROR: stopped (see above); a prefab is saved only after its own checks pass."
            : failed > 0 ? $"{failed} device(s) FAILED their checks and were NOT saved; {saved} saved, {refused} refused."
            : refused > 0 && saved == 0 ? $"Nothing changed: {refused} device(s) refused (their FBX aren't real size yet: the Blender lead's wave 2, then Devices 1)."
            : $"Done: {saved} prefab(s) saved, {refused} refused, the rest already real. Next: Bench 9, then the scale audit (after).";
        run.Finish(verdict, error || failed > 0);
        AssetDatabase.SaveAssets();
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    enum Outcome { Saved, Unchanged, Refused, Failed }

    static Outcome Device(BenchScale.Device d, BenchScale.Manifest manifest, BenchScale.Run run)
    {
        run.Section($"{d.label}: {d.prefab} ← {d.model} (k = {d.k:0.###})");
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(d.model);
        if (asset == null) { run.Check(false, $"{d.model} is imported (run Devices 1 first)"); return Outcome.Failed; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) == null) { run.Check(false, $"{d.prefab} is in the project"); return Outcome.Failed; }

        // The map: devices.json's "targets" (the export's own word on its pieces), else this step's table.
        Dictionary<string, object> piece = manifest != null ? manifest.Piece(Path.GetFileNameWithoutExtension(d.model)) : null;
        List<(string source, string[] targets)> map = manifest != null ? manifest.Targets(piece) : null;
        bool fromManifest = map != null && map.Count > 0;
        if (!fromManifest) map = d.fallback.ToList();
        run.Info($"the map ({(fromManifest ? "devices.json's targets" : "this step's own table")}): " + string.Join("; ", map.Select(m => $"{(m.source == "" ? "(root)" : m.source)} → {string.Join(", ", m.targets)}")));
        if (piece != null)
        {
            float authored = manifest.AuthoredScale(piece);
            run.Info($"the export says: units {manifest.Units(piece) ?? "(not said)"}, authored at {(float.IsNaN(authored) ? "(not said)" : authored.ToString("0.##", CultureInfo.InvariantCulture) + "x")}; " +
                     $"the bake's k is the PREFAB's authored scale ({d.k:0.###}), whatever the FBX was authored at");
        }
        // v3 (the Blender lead's wave 2): places after the bake, new pieces, pieces with no collider (empty with an older manifest).
        Dictionary<string, Vector3> movedTo = manifest != null ? manifest.MovedTo(piece) : new Dictionary<string, Vector3>();
        List<BenchScale.Manifest.NewObject> newObjects = manifest != null ? manifest.NewObjects(piece) : new List<BenchScale.Manifest.NewObject>();
        List<string> noCollider = manifest != null ? manifest.NoCollider(piece) : new List<string>();
        if (movedTo.Count + newObjects.Count + noCollider.Count > 0)
            run.Info($"v3: {movedTo.Count} object(s) to new places after the bake ({string.Join(", ", movedTo.Keys)}); new: {(newObjects.Count > 0 ? string.Join(", ", newObjects.Select(n => n.name)) : "none")}; " +
                     $"no collider: {(noCollider.Count > 0 ? string.Join(", ", noCollider) : "none")}");
        // A new piece is made by the fit under its parent, with a box collider fitted to its mesh (this run's rules only).
        var rules = new Dictionary<string, Rule>(Rules[d.label]);
        foreach (BenchScale.Manifest.NewObject n in newObjects)
            if (!rules.ContainsKey(n.name)) rules[n.name] = new Rule { createUnder = string.IsNullOrEmpty(n.under) ? Path.GetFileNameWithoutExtension(d.prefab) : n.under, box = true };

        GameObject root = PrefabUtility.LoadPrefabContents(d.prefab);
        try
        {
            var def = root.GetComponent<DeviceDefinition>();
            var job = root.GetComponent<JobBase>();
            if (def == null || job == null) { run.Check(false, $"{d.prefab} has a DeviceDefinition and a JobBase on its root"); return Outcome.Failed; }
            bool wasReal = def.realSize;

            // ---- is the FBX real yet? ----
            string screwSource = map.Where(m => m.targets.Any(n => Find(root.transform, n) is Transform t && t.GetComponent<Screw>() != null))
                                    .Select(m => m.source).FirstOrDefault();
            bool fbxReal = BenchScale.FbxIsReal(d, asset, screwSource, out string why);
            if (!fbxReal)
            {
                if (wasReal) { run.Check(false, $"{d.prefab} is baked real, but {d.model} isn't: {why}. Re-import the wave 2 FBX (Devices 1); nothing was changed"); return Outcome.Failed; }
                run.Refused($"{d.model} isn't real size yet: {why}. Wait for the Blender lead's wave 2, then Devices 1. Nothing changed.");
                return Outcome.Refused;
            }
            run.Info($"{d.model} is real size: {why}");

            // ---- before: the snapshot the audit and this step's own checks compare against ----
            BenchScale.Snap before = BenchScale.Take(root, d.prefab);
            if (!wasReal)
            {
                BenchScale.Save(before);
                run.Info($"the \"before\" snapshot: {before.items.Count} objects → {BenchScale.SnapPath(d.prefab)}");
                Photograph(d, "1-before", run);
            }
            int changesBefore = run.Changes;
            var problems = new List<string>();

            // ---- 1. the fit ----
            Fit(root, asset, map, rules, d, run, problems);

            // ---- 2. the bake ----
            if (!wasReal)
            {
                Bake(root, asset, d.k, before, run, problems);
                def.realSize = true;
                run.Changed("DeviceDefinition.realSize: false → true (the old builder steps refuse to run on it from now on)");
            }
            else run.Info("already real (DeviceDefinition.realSize): no bake; the fit above changed only what differed");

            // ---- 3. v3: the new places, the new pieces, no collider on the strap, the camera's shutter wired ----
            ApplyV3(root, d, movedTo, newObjects, noCollider, run, problems);

            // ---- the rest height: its lowest point 3.3 mm over the point it is set on ----
            float bottom = BenchScale.Bottom(root), rest = BenchScale.SurfaceGap - bottom;
            if (Mathf.Abs(rest - job.restHeight) > 1e-5f)
            {
                run.Changed($"JobBase.restHeight: {job.restHeight:0.0000} → {rest:0.0000} m (its lowest point {BenchScale.Mm(-bottom)} under its anchor: 3.3 mm over its slot point, 0.3 mm over the intake tray's felt)");
                job.restHeight = rest;
            }
            else run.Info($"JobBase.restHeight {job.restHeight:0.0000} m: already puts its lowest point 3.3 mm over its slot point");

            // ---- the checks, in numbers; the prefab is saved only if they all pass ----
            int failsBefore = run.Fails;
            run.Check(problems.Count == 0, problems.Count == 0 ? "every piece the map names was found and fitted" : "the fit: " + string.Join("; ", problems));
            // A grime spot builds its own dirt patch when it wakes (GrimeSpot: mf.sharedMesh = patch), so its filter may be
            // empty in the prefab (the watch's Smudge_Crystal has been since 13 Oct): that isn't a mesh the map lost.
            List<string> meshless = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh == null && m.GetComponentInParent<JobMarker>(true) == null && m.GetComponent<GrimeSpot>() == null).Select(m => BenchScale.PathIn(root.transform, m.transform)).ToList();
            run.Check(meshless.Count == 0, meshless.Count == 0 ? "every mesh filter has a mesh" : "lost their meshes (the map no longer reaches them): " + string.Join(", ", meshless));
            Screw[] screws = root.GetComponentsInChildren<Screw>(true);
            if (screws.Length > 0)
            {
                bool heads = screws.All(s => s.GetComponent<BoxCollider>() is BoxCollider b && b != null && (b.size - HeadBox).magnitude < 1e-6f && b.center.sqrMagnitude < 1e-12f);
                run.Check(heads, $"{screws.Length} screws' head boxes are {BenchScale.MmV(HeadBox)} round their origin (Screw.HeadSize reads them: the hole and the driver's tip follow)");
            }
            run.Check(def.realSize, "DeviceDefinition.realSize is true");
            if (!wasReal)
            {
                List<string> misses = BenchScale.Compare(before, root, d.k, out int matched, movedTo);
                run.Check(misses.Count == 0, $"every object at before × k ± 0.1 mm ({matched} of {before.items.Count}; a screw by where its head meets its surface" +
                                             $"{(movedTo.Count > 0 ? $"; {movedTo.Count} at v3's new places, what is under them carried" : "")})" +
                                             (misses.Count > 0 ? ": " + string.Join("; ", misses.Take(10)) : ""));
            }
            CheckV3(root, d, movedTo, newObjects, noCollider, run);
            run.Check(Mathf.Abs(job.restHeight + BenchScale.Bottom(root) - BenchScale.SurfaceGap) < 1e-5f, "its lowest point sits 3.3 mm over its slot point");

            if (run.Fails > failsBefore) { run.Info($"NOT SAVED: {d.prefab} is as it was."); return Outcome.Failed; }
            if (run.Changes == changesBefore) { run.Info("nothing to change: already real and fitted"); return Outcome.Unchanged; }
            PrefabUtility.SaveAsPrefabAsset(root, d.prefab);
            run.Info("saved " + d.prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Photograph(d, "2-after", run);
        return Outcome.Saved;
    }

    // ------------------------------------------------------------------ 1. the fit

    static void Fit(GameObject root, GameObject asset, List<(string source, string[] targets)> map, Dictionary<string, Rule> rules,
                    BenchScale.Device d, BenchScale.Run run, List<string> problems)
    {
        var fitted = new HashSet<string>(map.SelectMany(m => m.targets));
        var known = new HashSet<string>(d.fallback.SelectMany(m => m.targets));
        foreach (var (source, targets) in map)
        {
            Transform src = source == "" ? asset.transform : asset.transform.Find(source);
            MeshFilter filter = src != null ? src.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = src != null ? src.GetComponent<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null) { problems.Add($"{asset.name} has no mesh '{source}'"); continue; }
            Mesh mesh = filter.sharedMesh;
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
            foreach (string name in targets)
            {
                rules.TryGetValue(name, out Rule rule);
                Transform t = Find(root.transform, name);
                var changed = new List<string>();
                if (t == null && rule != null && !string.IsNullOrEmpty(rule.createUnder))
                {
                    Transform under = Find(root.transform, rule.createUnder);
                    if (under != null)
                    {
                        var made = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                        made.transform.SetParent(under, false);
                        made.layer = under.gameObject.layer;
                        t = made.transform;
                        changed.Add($"made under {rule.createUnder}");
                    }
                }
                if (t == null)
                {
                    // A piece the prefab has always had is a problem; a NEW piece of the export's (Art A7's clean mechanism) is
                    // made by whoever wires it, and fitted here once it exists.
                    if (known.Contains(name)) problems.Add($"the prefab has no '{name}'");
                    else run.Info($"{name}: not in the prefab yet (the export maps {(source == "" ? "its root" : source)} to it); whoever wires it makes it, and this step fits it then");
                    continue;
                }

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null) { mf = t.gameObject.AddComponent<MeshFilter>(); changed.Add("a MeshFilter added"); }
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null) { mr = t.gameObject.AddComponent<MeshRenderer>(); changed.Add("a MeshRenderer added"); }
                if (mf.sharedMesh != mesh) { changed.Add($"mesh {(mf.sharedMesh != null ? mf.sharedMesh.name : "none")} → {mesh.name} ({indices / 3} triangles, {BenchScale.MmV(mesh.bounds.size)})"); mf.sharedMesh = mesh; }

                // scale 1: the mesh is the size. Children placed through this object's scale keep their places (and, unless
                // fitted themselves, their sizes): the stand-in camera's blades, 9 Oct.
                Vector3 oldScale = t.localScale;
                if (!BenchScale.IsUnit(oldScale))
                {
                    t.localScale = Vector3.one;
                    foreach (Transform child in t)
                    {
                        child.localPosition = Vector3.Scale(child.localPosition, oldScale);
                        if (!fitted.Contains(child.name)) child.localScale = Vector3.Scale(child.localScale, oldScale);
                    }
                    changed.Add($"scale {BenchScale.V(oldScale)} → 1 (its children kept their places)");
                }

                if (renderer != null)
                {
                    Material[] want;
                    if (rule == null || !rule.keepMaterials) want = renderer.sharedMaterials;
                    else
                    {
                        Material[] own = mr.sharedMaterials, fbx = renderer.sharedMaterials;
                        want = new Material[Mathf.Max(mesh.subMeshCount, 1)];
                        for (int i = 0; i < want.Length; i++) want[i] = i < own.Length && own[i] != null ? own[i] : (i < fbx.Length ? fbx[i] : null);
                    }
                    if (!mr.sharedMaterials.SequenceEqual(want))
                    {
                        mr.sharedMaterials = want;
                        changed.Add("materials " + string.Join(", ", want.Select(m => m != null ? m.name : "none")) + (rule != null && rule.keepMaterials ? " (its own, kept)" : ""));
                    }
                }

                FitColliders(t, mesh, rule, changed);
                if (changed.Count > 0) run.Changed($"{name} ← {(source == "" ? "the FBX root" : source)}: {string.Join("; ", changed)}");
                else run.Info($"{name} ← {(source == "" ? "the FBX root" : source)}: already fitted");
            }
        }
    }

    static void FitColliders(Transform t, Mesh mesh, Rule rule, List<string> changed)
    {
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box == null && rule != null && rule.box && t.GetComponent<Collider>() == null)
        {
            box = t.gameObject.AddComponent<BoxCollider>();   // fitted to the mesh's box just below
            changed.Add("a box collider added");
        }
        if (box != null && rule != null && rule.boxes != null)
        {
            // one box per solid part: the existing colliders reused in order, missing ones added, extras removed
            List<BoxCollider> boxes = t.GetComponents<BoxCollider>().ToList();
            bool differs = boxes.Count != rule.boxes.Length;
            for (int i = 0; i < Mathf.Min(boxes.Count, rule.boxes.Length); i++)
                differs |= (boxes[i].center - rule.boxes[i].center).magnitude > 1e-6f || (boxes[i].size - rule.boxes[i].size).magnitude > 1e-6f;
            if (!differs) return;
            while (boxes.Count < rule.boxes.Length) boxes.Add(t.gameObject.AddComponent<BoxCollider>());
            for (int i = boxes.Count - 1; i >= rule.boxes.Length; i--) { Object.DestroyImmediate(boxes[i]); boxes.RemoveAt(i); }
            for (int i = 0; i < rule.boxes.Length; i++)
            {
                boxes[i].isTrigger = box.isTrigger;
                boxes[i].sharedMaterial = box.sharedMaterial;
                boxes[i].center = rule.boxes[i].center;
                boxes[i].size = rule.boxes[i].size;
            }
            changed.Add($"{rule.boxes.Length} boxes, the solid not the bounds: " + string.Join("; ", rule.boxes.Select(b => BenchScale.MmV(b.size) + " at " + BenchScale.V(b.center))));
            return;
        }
        if (box != null)
        {
            Vector3 centre, size;
            string what;
            if (t.GetComponent<Screw>() != null) { centre = Vector3.zero; size = HeadBox; what = "the head only"; }
            else if (rule != null && rule.faceY.HasValue)
            {
                // from the face other pieces rest on up to the mesh's top: what stands proud of the face is left out
                Bounds b = mesh.bounds;
                float face = rule.faceY.Value, top = b.max.y;
                centre = new Vector3(b.center.x, (top + face) / 2f, b.center.z);
                size = new Vector3(b.size.x, top - face, b.size.z);
                what = "to the face, not the island";
            }
            else { centre = mesh.bounds.center; size = mesh.bounds.size; what = "the mesh's box"; }
            if ((box.center - centre).magnitude > 1e-6f || (box.size - size).magnitude > 1e-6f)
            {
                changed.Add($"box {BenchScale.MmV(box.size)} → {BenchScale.MmV(size)} ({what})");
                box.center = centre;
                box.size = size;
            }
        }
        CapsuleCollider capsule = t.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            // the crown, the lens housing: along their own Y, as long and as wide as the mesh
            Bounds b = mesh.bounds;
            float radius = Mathf.Max(b.extents.x, b.extents.z);
            if ((capsule.center - b.center).magnitude > 1e-6f || capsule.direction != 1 || Mathf.Abs(capsule.height - b.size.y) > 1e-6f || Mathf.Abs(capsule.radius - radius) > 1e-6f)
            {
                changed.Add($"capsule r {capsule.radius * 1000f:0.00} → {radius * 1000f:0.00} mm, h {capsule.height * 1000f:0.00} → {b.size.y * 1000f:0.00} mm");
                capsule.center = b.center;
                capsule.direction = 1;
                capsule.height = b.size.y;
                capsule.radius = radius;
            }
        }
    }

    // ------------------------------------------------------------------ 2. the bake

    static void Bake(GameObject root, GameObject asset, float k, BenchScale.Snap before, BenchScale.Run run, List<string> problems)
    {
        var hero = new HashSet<Mesh>(asset.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).Where(m => m != null));
        var lines = new List<string>();
        foreach (Transform child in root.transform) BakeNode(root.transform, child, 1f, k, hero, lines, problems);

        // A screw: its head where it met its surface, × k (the new head is 1.33 mm tall, not the old head × k).
        var contacts = before.items.Where(i => i.screw).ToDictionary(i => i.path, i => i.contact);
        foreach (Screw screw in root.GetComponentsInChildren<Screw>(true))
        {
            string path = BenchScale.PathIn(root.transform, screw.transform);
            if (!contacts.TryGetValue(path, out Vector3 was)) { problems.Add($"{path}: not in the snapshot"); continue; }
            Vector3 now = BenchScale.Contact(root, screw.transform, BenchScale.HeadHeight(screw.transform));
            Vector3 shift = was * k - now;   // root frame (its scale is 1)
            screw.transform.position += root.transform.TransformVector(shift);
            lines.Add($"{path}: its head back on its surface, moved {BenchScale.Mm(shift.magnitude)} along its axis (the head is {BenchScale.Mm(BenchScale.ScrewHeadHeight)} tall now)");
        }

        // The metre fields that are the device's own (TD §6.4); the rest are Systems' to make relative.
        foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b == null) continue;
            foreach (string field in new[] { "domeRadius", "seatDistance", "snapDistance" })
            {
                var so = new SerializedObject(b);
                SerializedProperty p = so.FindProperty(field);
                if (p == null || p.propertyType != SerializedPropertyType.Float || p.floatValue <= 0f) continue;
                float was = p.floatValue;
                p.floatValue = was * k;
                so.ApplyModifiedPropertiesWithoutUndo();
                lines.Add($"{BenchScale.PathIn(root.transform, b.transform)} · {b.GetType().Name}.{field}: {was:0.0000} → {was * k:0.0000} m");
            }
            foreach (string field in new[] { "threadLength", "liftHeight" })
            {
                float v = BenchScale.Serialized(b, field);
                if (!float.IsNaN(v)) lines.Add($"{BenchScale.PathIn(root.transform, b.transform)} · {b.GetType().Name}.{field} {v:0.0000} m: left as it is (Systems makes it relative to the head)");
            }
        }
        run.Changed($"the bake × {k:0.###}, {lines.Count} line(s):");
        foreach (string line in lines) run.Info("    " + line);
    }

    // One object and everything under it. s: how much its parent's frame has grown as seen from the root (1 outside a scaled
    // stand-in, k inside one): its place × k / s keeps every object at k × its place from the root.
    static void BakeNode(Transform root, Transform t, float s, float k, HashSet<Mesh> hero, List<string> lines, List<string> problems)
    {
        string path = BenchScale.PathIn(root, t);
        if (t.GetComponent<JobMarker>() != null) { lines.Add($"{path}: left alone with everything under it (the job tag sizes itself on screen)"); return; }
        MeshFilter mf = t.GetComponent<MeshFilter>();
        bool isHero = mf != null && mf.sharedMesh != null && hero.Contains(mf.sharedMesh);
        bool shaped = !isHero && !BenchScale.IsUnit(t.localScale);
        float f = k / s, inner = s;
        Vector3 was = t.localPosition;
        t.localPosition = was * f;
        string kind;
        if (isHero)
        {
            if (!BenchScale.IsUnit(t.localScale)) problems.Add($"{path}: a hero piece at scale {BenchScale.V(t.localScale)} (the fit puts every one at 1)");
            if (Mathf.Abs(s - 1f) > 1e-5f) problems.Add($"{path}: a hero piece inside a stand-in shaped by its scale (its mesh would be scaled twice)");
            kind = "hero piece: its mesh is real, scale 1 kept";
        }
        else if (shaped)
        {
            Vector3 size = t.localScale;
            t.localScale = size * f;
            inner = s * f;
            kind = $"stand-in shaped by its scale: {BenchScale.V(size)} → {BenchScale.V(t.localScale)}";
        }
        else if (mf != null && mf.sharedMesh != null)
        {
            if (Mathf.Abs(s - 1f) < 1e-5f) problems.Add($"{path}: a mesh at scale 1 that isn't the device's model ({mf.sharedMesh.name}): its size can't be told");
            kind = "a mesh inside a scaled stand-in (it rides its parent's scale)";
        }
        else kind = t.childCount > 0 ? "grouping node: scale 1 kept, so its children aren't scaled twice" : "no mesh (logic)";
        lines.Add($"{path}: place {BenchScale.V(was)} → {BenchScale.V(t.localPosition)}; {kind}");
        foreach (Transform child in t) BakeNode(root, child, inner, k, hero, lines, problems);
    }

    // ------------------------------------------------------------------ 3. v3: the Blender lead's wave 2 (devices.json v3)

    // The Producer's integration call 6 (14 Oct; blender-wave2.md §4.3, §5.2), after the bake, by the manifest:
    //   * each moved object to its new place at real size, in the root's frame (what is under it moves with it): the camera's
    //     body 32 mm deep with its back where it was, so its front face and everything on it or in its well rise 9 mm (the
    //     lens, the lens grime, the film path and its two grime spots, the shutter's seat with both blades); the new strap's two
    //     runs at their lugs and its loop on the mat behind the lens; the three scratches onto the loop's top turn (rotation and
    //     baked scale kept); the phone's and the watch's 3.8 × 1.33 mm screw heads on their surfaces;
    //   * each new piece (Art A7's "Clean shutter mechanism": the fit made it under the root from the FBX's CleanMechanism,
    //     with a box collider fitted to its mesh) at its place, in the shutter's seat, and switched off;
    //   * no collider on the strap's runs, its loop and its scratches (Art A6: never a hover target; fitted to the new strap,
    //     the lower run's box would wrap the camera's opening end);
    //   * the camera's shutter wired: ReplaceablePart.freshVisual is the clean mechanism (on Day 2 the bright one waiting on the
    //     pad is the same mechanism, clean, not a lone blade), brokenVisual the jammed assembly itself, and DeviceWake fires the
    //     clean one (R4: Day 1 winds only, Day 2 winds and fires once it is in). The working blade stays in the prefab, unused.
    // A second run finds each where it is and changes nothing.
    const string CleanShutter = "Clean shutter mechanism";

    static void ApplyV3(GameObject root, BenchScale.Device d, IReadOnlyDictionary<string, Vector3> movedTo, List<BenchScale.Manifest.NewObject> made,
                        List<string> noCollider, BenchScale.Run run, List<string> problems)
    {
        foreach (KeyValuePair<string, Vector3> move in movedTo)
        {
            Transform t = Find(root.transform, move.Key);
            if (t == null) { problems.Add($"v3 moves '{move.Key}', which the prefab doesn't have"); continue; }
            Vector3 was = BenchScale.RootPoint(root.transform, t.position);
            if ((was - move.Value).magnitude < 1e-6f) { run.Info($"{move.Key}: already at its v3 place {BenchScale.V(move.Value)}"); continue; }
            t.position = root.transform.TransformPoint(move.Value);
            run.Changed($"{move.Key}: {BenchScale.V(was)} → {BenchScale.V(move.Value)}, v3's place ({BenchScale.Mm((move.Value - was).magnitude)}" +
                        $"{(t.childCount > 0 ? "; what is under it moved with it" : "")})");
        }
        foreach (BenchScale.Manifest.NewObject n in made)
        {
            Transform t = Find(root.transform, n.name);
            if (t == null) { problems.Add($"v3's new '{n.name}' wasn't made (is '{n.source}' in the FBX? run Devices 1)"); continue; }
            Vector3 was = BenchScale.RootPoint(root.transform, t.position);
            if ((was - n.at).magnitude >= 1e-6f)
            {
                t.position = root.transform.TransformPoint(n.at);
                run.Changed($"{n.name}: to v3's place {BenchScale.V(n.at)} (from {BenchScale.V(was)})");
            }
            if (t.gameObject.activeSelf != n.active)
            {
                t.gameObject.SetActive(n.active);
                run.Changed($"{n.name}: {(n.active ? "switched on" : "switched off in the prefab (the bench shows it when it presents the fresh part)")}");
            }
        }
        foreach (string name in noCollider)
        {
            Transform t = Find(root.transform, name);
            if (t == null) { problems.Add($"v3 names '{name}' to carry no collider, but the prefab has no such object"); continue; }
            Collider[] colliders = t.GetComponents<Collider>();
            if (colliders.Length == 0) continue;
            foreach (Collider c in colliders) Object.DestroyImmediate(c);
            run.Changed($"{name}: {colliders.Length} collider(s) removed (Art A6: the strap is never a hover target)");
        }
        if (d.label == "camera") WireShutter(root, run, problems);
    }

    static void WireShutter(GameObject root, BenchScale.Run run, List<string> problems)
    {
        var job = root.GetComponent<GraceCameraRepairJob>();
        ReplaceablePart shutter = job != null && job.Shutter != null ? job.Shutter : root.GetComponentInChildren<ReplaceablePart>(true);
        Transform clean = Find(root.transform, CleanShutter);
        if (shutter == null || clean == null) { problems.Add($"the camera's shutter wiring: {(shutter == null ? "no ReplaceablePart" : "no '" + CleanShutter + "'")}"); return; }
        var so = new SerializedObject(shutter);
        SerializedProperty fresh = so.FindProperty("freshVisual"), broken = so.FindProperty("brokenVisual");
        if (fresh == null || broken == null) { problems.Add("ReplaceablePart has no freshVisual / brokenVisual to wire"); return; }
        if (fresh.objectReferenceValue != clean.gameObject)
        {
            run.Changed($"{shutter.name} · ReplaceablePart.freshVisual: {(fresh.objectReferenceValue != null ? fresh.objectReferenceValue.name : "none")} → {CleanShutter} (the fresh part, Art A7)");
            fresh.objectReferenceValue = clean.gameObject;
        }
        if (broken.objectReferenceValue != shutter.gameObject)
        {
            run.Changed($"{shutter.name} · ReplaceablePart.brokenVisual: {(broken.objectReferenceValue != null ? broken.objectReferenceValue.name : "none")} → {shutter.name} (the whole jammed assembly)");
            broken.objectReferenceValue = shutter.gameObject;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var wake = root.GetComponent<DeviceWake>();
        if (wake == null) { problems.Add("the camera has no DeviceWake (its shutter fires on Day 2)"); return; }
        SerializedProperty parts = new SerializedObject(wake).FindProperty("parts");
        Object first = parts != null && parts.arraySize > 0 ? parts.GetArrayElementAtIndex(0).objectReferenceValue : null;
        if (wake.What != DeviceWake.Kind.ShutterFires || parts == null || parts.arraySize != 1 || first != clean)
        {
            run.Changed($"DeviceWake: fires {(first != null ? first.name : "nothing")} → {CleanShutter} (R4: Day 2 winds and fires once it is in; Day 1 winds only)");
            wake.Configure(DeviceWake.Kind.ShutterFires, clean);
            EditorUtility.SetDirty(wake);
        }
    }

    // The checks for v3, whether this run changed anything or not.
    static void CheckV3(GameObject root, BenchScale.Device d, IReadOnlyDictionary<string, Vector3> movedTo, List<BenchScale.Manifest.NewObject> made,
                        List<string> noCollider, BenchScale.Run run)
    {
        if (movedTo.Count > 0)
        {
            var off = movedTo.Select(m => (m.Key, t: Find(root.transform, m.Key)))
                             .Where(x => x.t == null || (BenchScale.RootPoint(root.transform, x.t.position) - movedTo[x.Key]).magnitude > BenchScale.PlaceTolerance)
                             .Select(x => x.Key).ToList();
            run.Check(off.Count == 0, $"v3: {movedTo.Count} object(s) at their new places ± 0.1 mm{(off.Count > 0 ? " (NOT: " + string.Join(", ", off) + ")" : "")}");
        }
        foreach (BenchScale.Manifest.NewObject n in made)
        {
            Transform t = Find(root.transform, n.name);
            MeshFilter mf = t != null ? t.GetComponent<MeshFilter>() : null;
            run.Check(t != null && mf != null && mf.sharedMesh != null && t.GetComponent<BoxCollider>() != null && t.gameObject.activeSelf == n.active
                      && (BenchScale.RootPoint(root.transform, t.position) - n.at).magnitude <= BenchScale.PlaceTolerance,
                $"v3's new {n.name}: its mesh, a box collider, {(n.active ? "on" : "off in the prefab")}, at {BenchScale.V(n.at)}");
        }
        if (noCollider.Count > 0)
        {
            List<string> still = noCollider.Where(n => Find(root.transform, n) is Transform t && t.GetComponent<Collider>() != null).ToList();
            run.Check(still.Count == 0, $"no collider on {string.Join(", ", noCollider)} (Art A6){(still.Count > 0 ? " (STILL ON: " + string.Join(", ", still) + ")" : "")}");
        }
        if (d.label == "camera" && Find(root.transform, CleanShutter) is Transform clean)
        {
            var job = root.GetComponent<GraceCameraRepairJob>();
            ReplaceablePart shutter = job != null && job.Shutter != null ? job.Shutter : root.GetComponentInChildren<ReplaceablePart>(true);
            var so = shutter != null ? new SerializedObject(shutter) : null;
            bool freshOk = so != null && so.FindProperty("freshVisual")?.objectReferenceValue == clean.gameObject;
            bool brokenOk = so != null && so.FindProperty("brokenVisual")?.objectReferenceValue == shutter.gameObject;
            var wake = root.GetComponent<DeviceWake>();
            SerializedProperty parts = wake != null ? new SerializedObject(wake).FindProperty("parts") : null;
            bool fires = wake != null && wake.What == DeviceWake.Kind.ShutterFires && parts != null && parts.arraySize == 1 && parts.GetArrayElementAtIndex(0).objectReferenceValue == clean;
            run.Check(freshOk && brokenOk && fires, $"the camera's shutter: the fresh part is the {CleanShutter} ({freshOk}), the broken one the jammed assembly ({brokenOk}), and DeviceWake fires the clean one ({fires})");
        }
    }

    // ------------------------------------------------------------------ photos

    // The device as set down on a plain mat beside the real screwdriver, from the close-up's 58° (a preview scene: the café
    // scene isn't touched). "The screwdriver is longer than the camera is wide" (TD, What he will see).
    static void Photograph(BenchScale.Device d, string phase, BenchScale.Run run)
    {
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
            GameObject driver = AssetDatabase.LoadAssetAtPath<GameObject>(BenchScale.ToolModels + "/BT_Screwdriver.fbx");
            using (var s = new BenchScale.Studio())
            {
                Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                s.Proxy("Mat (photo)", cube, new[] { s.Flat(new Color(.31f, .44f, .63f)) }, new Vector3(0f, -.0025f, 0f), Quaternion.identity, new Vector3(.6f, .005f, .36f));
                GameObject device = s.Place(prefab);
                if (device != null)
                {
                    foreach (Renderer r in device.GetComponentsInChildren<Renderer>(true)) if (!DeviceBounds.Counts(r)) r.enabled = false;
                    float bottom = BenchScale.Bottom(device);
                    device.transform.SetPositionAndRotation(new Vector3(-.045f, -bottom, 0f), Quaternion.identity);
                }
                GameObject tool = s.Place(driver);
                if (tool != null) tool.transform.SetPositionAndRotation(new Vector3(.085f, .008f, -.07f), Quaternion.Euler(90f, 0f, 0f));   // lying, tip away
                const float pitch = 58f, distance = .45f;
                Quaternion look = Quaternion.Euler(pitch, 0f, 0f);
                Vector3 from = new Vector3(.01f, 0f, 0f) - look * Vector3.forward * distance;
                Texture2D picture = s.Shoot(from, look, 40f, 960, 540);
                string file = Path.Combine(run.Folder, $"{d.label}-{phase}.png");
                BenchScale.SavePng(picture, file);
                Object.DestroyImmediate(picture);
                run.Info($"photo ({phase}): {file}");
            }
        }
        catch (Exception e) { run.Info($"the {phase} photo wasn't taken: {e.Message}"); }
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
#endif
