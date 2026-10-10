#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE CAFÉ REDESIGN'S SHARED STEP (10 Oct 2026; claude/studio/cafe-sf-research-and-redesign.md, his decisions of 9 Oct, 23:40)
//
// Every scene step of the café redesign (Fixit Fidget > Cafe redesign > Bar 1, Bar 2, Seats 1, Room 1, Floor, Street 1)
// runs through Step() here, so they all behave the same way:
//   - Edit Mode, the café scene, its root found; a step already done in this scene doesn't run twice;
//   - the survey first: it finds and checks everything it will touch and throws with the reason if anything is wrong,
//     having changed nothing;
//   - the guards before (the layout check and the occupied-circulation check), the step's own photos and the room's 14;
//   - the change, inside one undo group, through the helpers below, each of which writes the step's record
//     (CafeRedesignRecord, on the café's root) so the step's Undo menu item can put everything back after a save;
//   - the step's own checks after the change (Run.Check); the routes re-baked; the guards after. Any failed check, or a
//     guard that got worse (fewer paths, fewer places served, a PASS lost), puts everything back: nothing half-done is
//     kept;
//   - the photos after; the report in Logs/CafeRedesign/<label>-<time>/report.txt and the last word in
//     Logs/CafeRedesign/<label>-last.txt and the Console. Not saved: look at the photos, then save the scene.
//
// Never deleted: what a step finds in the scene is switched off (the night's set-up finds things by name). An Undo
// deletes only what its step made. Only the latest step may be undone (a later one may stand on an earlier one's work).
// ---------------------------------------------------------------------------
internal static class CafeRedesignShared
{
    public const string Menu = "Fixit Fidget/Cafe redesign/";
    public const string CafeRootName = "ACE'S CAFE - layout study 02";
    public const string KitModels = "Assets/Art/Models/CafeKit";
    public const string KitMaterials = "Assets/Art/Materials/Cafe";
    public const string KitTextures = "Assets/Art/Textures/Cafe";
    public const string Geometry = KitModels + "/Cafe redesign geometry.asset";
    // Everything the redesign places goes under this group on the café's root, one child group per step.
    public const string RedesignGroup = "30 - cafe redesign";

    public static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeRedesign"));

    // ================================================================== a run

    public sealed class Run
    {
        public string Title { get; internal set; }
        public string Label { get; internal set; }
        public string Folder { get; internal set; }
        public Scene Scene { get; internal set; }
        public Transform Cafe { get; internal set; }
        public Transform Player { get; internal set; }
        public CafeRedesignRecord Record { get; internal set; }
        // "0-check" (a read-only check), "1-before" (photos before), "2-after" (photos after): the photo files' prefix.
        public string Phase { get; internal set; } = "0-check";
        public bool Changing { get; internal set; }
        public readonly StringBuilder Report = new StringBuilder();
        internal readonly List<string> failures = new List<string>();
        public int Failures => failures.Count;

        public void Line(string text = "") => Report.AppendLine(text);
        public void Info(string text) => Report.AppendLine("  info  " + text);
        public void Check(bool ok, string text)
        {
            Report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + text);
            if (!ok) failures.Add(text);
        }
        // A survey's hard requirement: throws (nothing has changed yet) with the reason.
        public void Require(bool ok, string reason)
        {
            if (ok) return;
            Report.AppendLine("  FAIL  " + reason);
            throw new InvalidOperationException(reason);
        }
    }

    // ================================================================== the step

    // survey: finds and checks (may throw: nothing changed). change: the change, through the helpers below, plus run.Check
    // for its own checks (any FAIL puts everything back). photos: the step's own photos, called before and after (run.Phase
    // says which); null for none. guards: the layout and occupied-circulation checks before and after. rebake: re-bake the
    // routes after the change (and after a rollback). roomPhotos: CafeFurnishing.Photograph's 14 views before and after.
    public static void Step(string title, string label, Action<Run> survey, Action<Run> change, Action<Run> photos,
                            bool guards = true, bool rebake = true, bool roomPhotos = true)
    {
        var run = new Run { Title = title, Label = label };
        string tag = "[" + title + "] ";
        int undoGroup = -1;
        bool changed = false;
        try
        {
            Begin(run);
            if (RecordOf(run.Cafe, title) != null)
            {
                Finish(run, tag, $"Already done in this scene: {title}. Its Undo puts it back.", false);
                return;
            }
            run.Folder = NewFolder(label);
            run.Line(title);
            run.Line(When());
            run.Line();
            run.Line("The survey (nothing changes here):");
            survey(run);
            if (run.Failures > 0) throw new InvalidOperationException($"The survey found {run.Failures} problem(s): " + string.Join("; ", run.failures));

            string layoutBefore = null, circulationBefore = null, roomBefore = null;
            if (guards)
            {
                layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
                circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
            }
            run.Phase = "1-before";
            if (photos != null) Safely(run, "the photos before", () => photos(run));
            if (roomPhotos) roomBefore = CafeFurnishing.Photograph(label + "-before");

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            changed = true;
            run.Record = Undo.AddComponent<CafeRedesignRecord>(run.Cafe.gameObject);
            run.Record.step = title;
            run.Record.when = When();
            run.Record.order = Records(run.Cafe).Where(r => r != run.Record).Select(r => r.order).DefaultIfEmpty(0).Max() + 1;
            run.Changing = true;
            run.Line();
            run.Line("The change, and its checks:");
            change(run);
            EditorUtility.SetDirty(run.Record);
            Physics.SyncTransforms();
            if (run.Failures > 0) throw new InvalidOperationException($"{run.Failures} check(s) failed after the change: " + string.Join("; ", run.failures));

            if (rebake) AcesCafeLayoutSetup.BakeRoutes();
            if (guards)
            {
                string layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
                string circulationAfter = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                run.Line();
                run.Line("Layout check before: " + layoutBefore);
                run.Line("Layout check after:  " + layoutAfter);
                run.Line("Circulation before:  " + circulationBefore);
                run.Line("Circulation after:   " + circulationAfter);
                if (!NotWorse(layoutBefore, layoutAfter) || !NotWorse(circulationBefore, circulationAfter))
                    throw new InvalidOperationException("A customer route or a delivery approach got worse (fewer paths or places served, or a PASS lost), so nothing was kept.");
            }
            run.Changing = false;
            Undo.CollapseUndoOperations(undoGroup);
            CutawayWall.ForgetCandidates();
            EditorSceneManager.MarkSceneDirty(run.Scene);

            run.Phase = "2-after";
            if (photos != null) Safely(run, "the photos after", () => photos(run));
            string roomAfter = roomPhotos ? CafeFurnishing.Photograph(label + "-after") : null;
            run.Line();
            run.Line(Summary(run.Record));
            if (roomPhotos) run.Line($"The room's 14 photos: {roomBefore} (before) and {roomAfter} (after).");
            run.Line($"Photos and this report: {run.Folder}.");
            run.Line("Not saved: look at the photos, then save the scene (Ctrl+S).");
            Finish(run, tag, "DONE. " + title, false);
        }
        catch (Exception e)
        {
            run.Changing = false;
            if (changed && undoGroup >= 0)
            {
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    Physics.SyncTransforms();
                    if (rebake) AcesCafeLayoutSetup.BakeRoutes();
                    CutawayWall.ForgetCandidates();
                }
                catch (Exception rollback) { run.Line("The rollback itself failed: " + rollback.Message); }
            }
            Finish(run, tag, (changed ? "FAILED (nothing half-done was kept): " : "REFUSED (nothing was changed): ") + e.Message + "\n\n" + e, true);
        }
    }

    // A read-only check: the survey and whatever it reports, nothing changed, no photos.
    public static void Check(string title, string label, Action<Run> survey)
    {
        var run = new Run { Title = title, Label = label };
        string tag = "[" + title + "] ";
        try
        {
            Begin(run);
            run.Line(title + " (read-only)");
            run.Line(When());
            run.Line();
            survey(run);
            Finish(run, tag, run.Failures == 0 ? "CHECKED: ready. Nothing was changed." : $"CHECKED: {run.Failures} problem(s). Nothing was changed.", run.Failures > 0);
        }
        catch (Exception e) { Finish(run, tag, "NOT READY (nothing was changed): " + e.Message + "\n\n" + e, true); }
    }

    // The step's Undo: everything its record holds put back, what it made deleted, the record removed, the routes re-baked.
    // refuse: an extra reason not to (e.g. the repair bar stands in this step's counter), or null.
    public static void UndoStep(string title, string label, Func<Run, string> refuse = null, bool rebake = true)
    {
        var run = new Run { Title = title + " - Undo", Label = label + "-undo" };
        string tag = "[" + title + "] ";
        try
        {
            Begin(run);
            CafeRedesignRecord record = RecordOf(run.Cafe, title);
            if (record == null)
            {
                Finish(run, tag, $"Nothing to undo: {title} hasn't been done in this scene.", false);
                return;
            }
            List<CafeRedesignRecord> later = Records(run.Cafe).Where(r => r.order > record.order).OrderByDescending(r => r.order).ToList();
            if (later.Count > 0) throw new InvalidOperationException("Undo the later step(s) first, newest first: " + string.Join(", ", later.Select(r => r.step)) + ".");
            string no = refuse?.Invoke(run);
            if (!string.IsNullOrEmpty(no)) throw new InvalidOperationException(no);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(run.Title);
            string done = Reverse(record, run.Title);
            Undo.DestroyObjectImmediate(record);
            Undo.CollapseUndoOperations(group);
            Physics.SyncTransforms();
            if (rebake) AcesCafeLayoutSetup.BakeRoutes();
            CutawayWall.ForgetCandidates();
            EditorSceneManager.MarkSceneDirty(run.Scene);
            run.Line(done);
            run.Line(AcesCafeLayoutSetup.ValidateLayout());
            run.Line(AcesCafeLayoutSetup.ValidateOccupiedCirculationV2());
            Finish(run, tag, $"UNDONE: {title}. Save the scene.", false);
        }
        catch (Exception e) { Finish(run, tag, "The undo FAILED: " + e.Message + "\n\n" + e, true); }
    }

    static string Reverse(CafeRedesignRecord r, string name)
    {
        int listed = 0, restored = 0, back = 0, on = 0, deleted = 0, missing = 0;
        // 1. Scene lists: take out what the step added.
        foreach (CafeRedesignRecord.Listed l in Enumerable.Reverse(r.listed))
        {
            if (l.owner == null) { missing++; continue; }
            var so = new SerializedObject(l.owner);
            SerializedProperty list = so.FindProperty(l.field);
            if (list == null || !list.isArray) { missing++; continue; }
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == l.item) { RemoveAt(list, i); listed++; break; }
            so.ApplyModifiedProperties();
        }
        // 2. Components as they were.
        foreach (CafeRedesignRecord.Lamp l in Enumerable.Reverse(r.lights))
        {
            if (l.target == null) { missing++; continue; }
            Undo.RecordObject(l.target, name);
            l.target.enabled = l.enabled;
            l.target.type = l.type;
            l.target.color = l.color;
            l.target.intensity = l.intensity;
            l.target.range = l.range;
            l.target.spotAngle = l.spotAngle;
            l.target.innerSpotAngle = l.innerSpotAngle;
            l.target.shadows = l.shadows;
            restored++;
        }
        foreach (CafeRedesignRecord.Box b in Enumerable.Reverse(r.boxes))
        {
            if (b.target == null) { missing++; continue; }
            Undo.RecordObject(b.target, name);
            b.target.enabled = b.enabled;
            b.target.center = b.center;
            b.target.size = b.size;
            restored++;
        }
        foreach (CafeRedesignRecord.Toggle t in Enumerable.Reverse(r.toggled))
        {
            if (t.target == null) { missing++; continue; }
            Undo.RecordObject(t.target, name);
            SetEnabled(t.target, t.enabled);
            if (PrefabUtility.IsPartOfPrefabInstance(t.target)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.target);
            restored++;
        }
        foreach (CafeRedesignRecord.Paint p in Enumerable.Reverse(r.repainted))
        {
            if (p.target == null) { missing++; continue; }
            Undo.RecordObject(p.target, name);
            p.target.sharedMaterials = p.materials;
            if (PrefabUtility.IsPartOfPrefabInstance(p.target)) PrefabUtility.RecordPrefabInstancePropertyModifications(p.target);
            restored++;
        }
        foreach (CafeRedesignRecord.Shape s in Enumerable.Reverse(r.shapes))
        {
            if (s.filter != null) { Undo.RecordObject(s.filter, name); s.filter.sharedMesh = s.mesh; restored++; }
            if (s.collider != null) { Undo.RecordObject(s.collider, name); s.collider.sharedMesh = s.colliderMesh; }
        }
        // 3. Moved pieces back under their parents, at their poses (before anything made goes: one may sit under a made group).
        foreach (CafeRedesignRecord.Pose m in Enumerable.Reverse(r.moved))
        {
            if (m.target == null) { missing++; continue; }
            if (m.target.parent != m.parent) Undo.SetTransformParent(m.target, m.parent, name);
            Undo.RecordObject(m.target, name);
            m.target.localPosition = m.localPosition;
            m.target.localRotation = m.localRotation;
            m.target.localScale = m.localScale;
            if (m.parent != null && m.siblingIndex >= 0 && m.siblingIndex < m.parent.childCount)
            {
                Undo.RegisterChildrenOrderUndo(m.parent, name);
                m.target.SetSiblingIndex(m.siblingIndex);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(m.target)) PrefabUtility.RecordPrefabInstancePropertyModifications(m.target);
            back++;
        }
        // 4. Switched back on.
        foreach (CafeRedesignRecord.Off o in r.switchedOff)
        {
            if (o.target == null) { missing++; continue; }
            Undo.RecordObject(o.target, name);
            o.target.SetActive(true);
            if (PrefabUtility.IsPartOfPrefabInstance(o.target)) PrefabUtility.RecordPrefabInstancePropertyModifications(o.target);
            on++;
        }
        // 5. What the step added and made: the only deletions, newest first.
        foreach (Component c in Enumerable.Reverse(r.addedComponents))
            if (c != null) { Undo.DestroyObjectImmediate(c); deleted++; }
        foreach (GameObject go in Enumerable.Reverse(r.made))
            if (go != null) { Undo.DestroyObjectImmediate(go); deleted++; }
        return $"Put back: {back} moved, {on} switched back on, {restored} component(s) as they were, {listed} list entr(ies) taken out; {deleted} thing(s) the step made deleted" +
               (missing > 0 ? $"; {missing} recorded thing(s) no longer in the scene" : "") + ".";
    }

    // ================================================================== the helpers a change uses (each writes the record)

    // The redesign's group on the café's root, then this step's group in it (made if missing; an existing one is reused).
    public static Transform StepGroup(Run run, string name)
    {
        Transform root = Group(run, run.Cafe, RedesignGroup);
        return Group(run, root, name);
    }

    public static Transform Group(Run run, Transform parent, string name)
    {
        Changing(run);
        Transform there = parent.Find(name);
        if (there != null) return there;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, run.Title);
        run.Record.made.Add(go);
        return go.transform;
    }

    // A café-kit piece (Assets/Art/Models/CafeKit/<piece>.fbx, imported by Kit 1 with its materials mapped) placed in the
    // world: its origin at `world`, turned `yaw` degrees about up (0 = its front, Unity local +Z, faces north; 180 = it faces
    // the street), at `scale` (null = 1; a negative x mirrors it). Named `name` (the night finds things by name: keep
    // names unique and saying what they are).
    public static GameObject Place(Run run, string piece, Transform parent, Vector3 world, float yaw, string name = null, Vector3? scale = null)
    {
        Changing(run);
        string path = KitModels + "/" + piece + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException($"{path} isn't imported: run Fixit Fidget > Cafe redesign > Kit 1 first.");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = string.IsNullOrEmpty(name) ? piece : name;
        go.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = scale ?? Vector3.one;
        Undo.RegisterCreatedObjectUndo(go, run.Title);
        run.Record.made.Add(go);
        return go;
    }

    // Anything else the step makes (a light, a stand-in): made by the caller, registered here.
    public static T Made<T>(Run run, T thing) where T : Object
    {
        Changing(run);
        GameObject go = thing as GameObject ?? (thing as Component)?.gameObject;
        if (go == null) throw new InvalidOperationException("Made() takes a GameObject or a component of one.");
        Undo.RegisterCreatedObjectUndo(go, run.Title);
        run.Record.made.Add(go);
        return thing;
    }

    // A light made by the step: a child of `parent` at a world pose.
    public static Light MakeLight(Run run, Transform parent, string name, Vector3 world, Quaternion rotation, LightType type, Color color,
                                  float intensity, float range, float spotAngle = 30f, LightShadows shadows = LightShadows.None)
    {
        Changing(run);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(world, rotation);
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        if (type == LightType.Spot) { light.spotAngle = spotAngle; light.innerSpotAngle = Mathf.Min(spotAngle * .6f, spotAngle - 1f); }
        light.shadows = shadows;
        Undo.RegisterCreatedObjectUndo(go, run.Title);
        run.Record.made.Add(go);
        return light;
    }

    // Off, never deleted.
    public static void SwitchOff(Run run, GameObject go, string why)
    {
        Changing(run);
        if (go == null || !go.activeSelf) return;
        if (run.Record.switchedOff.Any(o => o.target == go)) return;
        Undo.RecordObject(go, run.Title);
        go.SetActive(false);
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        run.Record.switchedOff.Add(new CafeRedesignRecord.Off { target = go, why = why });
    }

    // Moved (and re-parented if `parent` is given), its parent and local pose before kept.
    public static void Move(Run run, Transform t, Vector3 world, Quaternion rotation, string why, Transform parent = null)
    {
        Changing(run);
        run.Record.moved.Add(new CafeRedesignRecord.Pose
        {
            target = t, parent = t.parent, localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale,
            siblingIndex = t.GetSiblingIndex(), why = why
        });
        if (parent != null && t.parent != parent) Undo.SetTransformParent(t, parent, run.Title);
        Undo.RecordObject(t, run.Title);
        t.SetPositionAndRotation(world, rotation);
        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    public static void Repaint(Run run, Renderer r, Material[] materials, string why)
    {
        Changing(run);
        run.Record.repainted.Add(new CafeRedesignRecord.Paint { target = r, materials = r.sharedMaterials, why = why });
        Undo.RecordObject(r, run.Title);
        r.sharedMaterials = materials;
        if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
    }

    // A light changed by `edit`, as it was kept.
    public static void ChangeLight(Run run, Light l, Action<Light> edit, string why)
    {
        Changing(run);
        run.Record.lights.Add(new CafeRedesignRecord.Lamp
        {
            target = l, enabled = l.enabled, type = l.type, color = l.color, intensity = l.intensity, range = l.range,
            spotAngle = l.spotAngle, innerSpotAngle = l.innerSpotAngle, shadows = l.shadows, why = why
        });
        Undo.RecordObject(l, run.Title);
        edit(l);
        if (PrefabUtility.IsPartOfPrefabInstance(l)) PrefabUtility.RecordPrefabInstancePropertyModifications(l);
    }

    public static void Refit(Run run, BoxCollider box, Vector3 center, Vector3 size, string why, bool enabled = true)
    {
        Changing(run);
        run.Record.boxes.Add(new CafeRedesignRecord.Box { target = box, enabled = box.enabled, center = box.center, size = box.size, why = why });
        Undo.RecordObject(box, run.Title);
        box.center = center;
        box.size = size;
        box.enabled = enabled;
    }

    // A renderer, collider or behaviour switched on or off (the GameObject stays as it is).
    public static void Toggle(Run run, Component c, bool enabled, string why)
    {
        Changing(run);
        bool was = IsEnabled(c);
        if (was == enabled) return;
        run.Record.toggled.Add(new CafeRedesignRecord.Toggle { target = c, enabled = was, why = why });
        Undo.RecordObject(c, run.Title);
        SetEnabled(c, enabled);
        if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    }

    // A component added to something the step didn't make (on a made piece, just AddComponent: it goes with the piece).
    public static T AddComponent<T>(Run run, GameObject go) where T : Component
    {
        Changing(run);
        T c = Undo.AddComponent<T>(go);
        if (!run.Record.made.Any(m => m != null && go.transform.IsChildOf(m.transform))) run.Record.addedComponents.Add(c);
        return c;
    }

    // A mesh replaced (the mesh before kept). The new mesh is saved into Cafe redesign geometry.asset.
    public static void Reshape(Run run, MeshFilter filter, Mesh mesh, string why, MeshCollider collider = null)
    {
        Changing(run);
        SaveMesh(mesh);
        run.Record.shapes.Add(new CafeRedesignRecord.Shape
        {
            filter = filter, mesh = filter != null ? filter.sharedMesh : null, collider = collider, colliderMesh = collider != null ? collider.sharedMesh : null, why = why
        });
        if (filter != null) { Undo.RecordObject(filter, run.Title); filter.sharedMesh = mesh; }
        if (collider != null) { Undo.RecordObject(collider, run.Title); collider.sharedMesh = mesh; }
    }

    public static Mesh SaveMesh(Mesh mesh)
    {
        if (AssetDatabase.Contains(mesh)) return mesh;
        if (!AssetDatabase.IsValidFolder(KitModels)) throw new InvalidOperationException($"{KitModels} is missing: run Kit 1 first.");
        if (AssetDatabase.LoadMainAssetAtPath(Geometry) == null) AssetDatabase.CreateAsset(mesh, Geometry);
        else AssetDatabase.AddObjectToAsset(mesh, Geometry);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    // An entry added at the end of a scene list (an array field of a component in the scene, e.g. CafeViewMode's
    // "overheadFixtures" or CafeDaylight's "warmLights"), unless it is in it already.
    public static void AddToList(Run run, Component owner, string field, Object item, string why)
    {
        Changing(run);
        var so = new SerializedObject(owner);
        SerializedProperty list = so.FindProperty(field);
        if (list == null || !list.isArray) throw new InvalidOperationException($"{owner.GetType().Name} has no list called {field}.");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        so.ApplyModifiedProperties();      // recorded in the undo group
        run.Record.listed.Add(new CafeRedesignRecord.Listed { owner = owner, field = field, item = item, why = why });
    }

    // A note in the record (and the report).
    public static void Note(Run run, string text)
    {
        if (run.Record != null) run.Record.notes.Add(text);
        run.Info(text);
    }

    // ================================================================== photos

    // A photo into the run's folder as "<phase>-<name>.png", with Ace's stand-in hidden (CafeSecondPassSteps.Capture:
    // 1440 x 900). isometric: what the overhead view hides is hidden (overhead fixtures, walls in the way).
    public static void Photo(Run run, string name, Vector3 from, Vector3 at, float fov, bool isometric = false)
    {
        string path = Path.Combine(run.Folder ?? NewFolder(run.Label), run.Phase + "-" + name + ".png");
        var hidden = new List<Renderer>();
        if (run.Player != null)
            foreach (Renderer r in run.Player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try { CafeSecondPassSteps.Capture(path, from, at, fov, isometric); }
        finally { foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false; }
    }

    // The game's overhead camera (CmShopCam), as CafeFurnishing.Photograph takes it.
    public static void GameCameraPhoto(Run run, string name)
    {
        GameObject shop = GameObject.Find("CmShopCam");
        if (shop == null) { run.Info("No CmShopCam: the game camera's photo wasn't taken."); return; }
        Photo(run, name, shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
    }

    // ================================================================== reading the scene

    public static Transform Find(Run run, string path) => run.Cafe.Find(path);

    public static Transform Need(Run run, string path)
    {
        Transform t = run.Cafe.Find(path);
        run.Require(t != null, $"'{path}' isn't under the café's root.");
        return t;
    }

    // Every transform under the café's root (active or not) whose name is exactly `name`.
    public static List<Transform> Named(Run run, string name, bool includeInactive = true) =>
        run.Cafe.GetComponentsInChildren<Transform>(includeInactive).Where(t => t.name == name).ToList();

    public static T[] InScene<T>(Run run, bool includeInactive = true) where T : Component =>
        run.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(includeInactive)).ToArray();

    // The step's record, if it has run in this scene.
    public static CafeRedesignRecord RecordOf(Transform cafe, string title) => cafe.GetComponents<CafeRedesignRecord>().FirstOrDefault(r => r.step == title);
    public static List<CafeRedesignRecord> Records(Transform cafe) => cafe.GetComponents<CafeRedesignRecord>().ToList();
    public static bool Done(Run run, string title) => RecordOf(run.Cafe, title) != null;
    public static bool RepairBarDone(Run run) => run.Cafe.GetComponent<CafeRepairBar>() != null;

    // The renderers' world box (active, enabled ones), or a point at the root if there are none.
    public static Bounds RendererBounds(Transform root, bool includeInactive = false)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // The first solid straight down from `from` (triggers and Ace ignored), or null.
    public static RaycastHit? Down(Run run, Vector3 from, float distance = 5f, Func<Collider, bool> skip = null)
    {
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (run.Player != null && h.collider.transform.IsChildOf(run.Player)) continue;
            if (skip != null && skip(h.collider)) continue;
            return h;
        }
        return null;
    }

    // The heading of a rotation's forward, degrees clockwise from north (+Z), 0..360.
    public static float Facing(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        return Mathf.Repeat(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 360f);
    }

    public static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (Transform p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names.Skip(Math.Max(0, names.Count - 4)));
    }

    public static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);

    public static string BoundsText(Bounds b) => string.Format(CultureInfo.InvariantCulture, "x {0:0.000}..{1:0.000}, y {2:0.000}..{3:0.000}, z {4:0.000}..{5:0.000}",
                                                               b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z);

    public static string F(float f, string format = "0.000") => f.ToString(format, CultureInfo.InvariantCulture);

    // ================================================================== the plumbing

    static void Begin(Run run)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first (Edit > Play Mode > Play).");
        CityPackChecks.RequireScene();
        run.Scene = SceneManager.GetActiveScene();
        GameObject cafe = run.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
        if (cafe == null) throw new InvalidOperationException($"'{CafeRootName}' isn't in the open scene (Fixit Fidget > Ace's Cafe > Open layout playtest).");
        run.Cafe = cafe.transform;
        GameObject player = run.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        run.Player = player != null ? player.transform : null;
        Physics.SyncTransforms();
    }

    static void Changing(Run run)
    {
        if (!run.Changing || run.Record == null) throw new InvalidOperationException("A scene change was asked for outside the step's change (the survey and the photos change nothing).");
    }

    static void Safely(Run run, string what, Action act)
    {
        try { act(); }
        catch (Exception e) { run.Info($"{what}: {e.Message} (the step went on)"); }
    }

    static string Summary(CafeRedesignRecord r) =>
        $"The record ({r.step}, step {r.order} of the redesign): {r.made.Count} made, {r.switchedOff.Count} switched off, {r.moved.Count} moved, {r.repainted.Count} repainted, " +
        $"{r.lights.Count} light(s) changed, {r.boxes.Count} collider(s) refitted, {r.shapes.Count} mesh(es) replaced, {r.toggled.Count} component(s) switched, " +
        $"{r.addedComponents.Count} component(s) added, {r.listed.Count} list entr(ies) added." +
        (r.switchedOff.Count > 0 ? "\nSwitched off:\n" + string.Join("\n", r.switchedOff.Where(o => o.target != null).Select(o => $"  {PathOf(o.target.transform)}: {o.why}")) : "");

    // Not worse: as many paths and served places as before, and a PASS kept if there was one (CafeTidySteps' rule).
    static bool NotWorse(string before, string after) =>
        Reached(after) >= Reached(before) && (after.Contains("PASS") || !before.Contains("PASS"));

    static int Reached(string result)
    {
        Match m = Regex.Match(result ?? "", @"(\d+)/(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    static bool IsEnabled(Component c) =>
        c is Behaviour b ? b.enabled : c is Renderer r ? r.enabled : c is Collider col ? col.enabled : c.gameObject.activeSelf;

    static void SetEnabled(Component c, bool on)
    {
        if (c is Behaviour b) b.enabled = on;
        else if (c is Renderer r) r.enabled = on;
        else if (c is Collider col) col.enabled = on;
        else throw new InvalidOperationException($"{c.GetType().Name} can't be switched on or off.");
    }

    static void RemoveAt(SerializedProperty list, int index)
    {
        int size = list.arraySize;
        list.GetArrayElementAtIndex(index).objectReferenceValue = null;
        list.DeleteArrayElementAtIndex(index);
        if (list.arraySize == size) list.DeleteArrayElementAtIndex(index);
    }

    static string When() => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    static string NewFolder(string label)
    {
        string folder = Path.Combine(LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static void Finish(Run run, string tag, string verdict, bool error)
    {
        string text = verdict + "\n\n" + run.Report;
        try
        {
            Directory.CreateDirectory(LogRoot);
            File.WriteAllText(Path.Combine(LogRoot, run.Label + "-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (!string.IsNullOrEmpty(run.Folder) && Directory.Exists(run.Folder)) File.WriteAllText(Path.Combine(run.Folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(tag + "Couldn't write the report: " + e.Message); }
        if (error) Debug.LogError(tag + text); else Debug.Log(tag + text);
    }
}
#endif
