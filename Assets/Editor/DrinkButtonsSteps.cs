#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE DRINKS MACHINE'S NEW BUTTONS: 26 Oct 2026 (Drink buttons)
//
// His 26 Oct note on the machine: "i am really not a fan of the way the buttons look ... its just words on a square in
// different colors, it doesn't give aces aesthetics you know." Three drawn redesigns were "ai art slop". So the buttons
// were built as real pieces in Blender (Tools/Blender/drink_buttons.py): one cap per drink, chunky and toy-like in the
// drink's own colour, with a cream label and the drink's PICTURE in relief (the same picture the tickets and the HUD use,
// draw_button_icons.drink, rebuilt in 3D from its own pixel coordinates). No words: his girlfriend is dyslexic, so each
// drink is told apart by its picture first, then its colour, and the prompt line still says "(A) Pour Latte" at 34 px.
// (A 26 px em, the floor, is 26 mm at the close-up's 0.94 m: "Hot Chocolate" in Baloo 2 Bold is about 7 em, 180 mm,
// across a 92 mm button on a 158 mm pitch; "Americano" 130, "Espresso" 110. One size for all six can't fit; the word
// stays in the prompt line, where it already is.)
//
//   Fixit Fidget > Drinks > Buttons 1 - The machine's new buttons (scene and prefab, with Undo)
//       Imports the six caps (Assets/Art/Models/DrinkButtons/DB_Cap_<Name>.fbx: scale 1, file units, no axis bake, no
//       animation, materials by name: the machine's own BF_<Drink> for the cap, nine DB_ ones made here in the icons'
//       colours), parents each cap under its "<Name> ceramic paddle" at the paddle's origin, facing the player (the cap is
//       built in the paddle's frame, so the press, the hit box and the aim help's targets (c2) all keep working: nothing
//       in BeverageSlot, BeverageControl or the colliders changes), gives BeveragePaddleMotion a 9 mm press (it was 6: a
//       third of the new cap's 30 mm), retires the old tile, its chrome finger grip and the printed word (renderers off,
//       the "Printed <Drink>" label inactive: nothing is deleted, so Undo and a revert are whole), checks the result and
//       photographs it from the close-up's pose and from above. One Undo group; a second run changes nothing. The
//       dispenser is scene objects (the installer builds it; the machine's FBX is a model prefab instance), so "prefab"
//       here means the instance's overrides: there is no dispenser prefab asset to edit.
//       Last line: "[Drink buttons] Buttons 1: DONE, <n> change(s). <folder>" or "... FAILED: <why>".
//   Fixit Fidget > Drinks > Lab - The machine's buttons (drives itself)
//       A lab session on a fresh Day 1 (the lab's own save, never his) that photographs each button at rest and pressed
//       while pouring, and the whole machine, and checks the pictures and the words (DrinkButtonsCheck).
//
// If the installer (Fixit Fidget > Content > Install six-drink dispenser) is ever run again it rebuilds the machine with
// the old tiles and words: run Buttons 1 again after it.
// ---------------------------------------------------------------------------
public static class DrinkButtonsSteps
{
    const string Menu = "Fixit Fidget/Drinks/";
    const string Tag = "[Drink buttons] ";
    const string Title = "Buttons 1";
    const string ModelFolder = "Assets/Art/Models/DrinkButtons";
    const string MaterialFolder = "Assets/Art/Materials/BeverageDispenserV2";   // beside the machine's own BF_ materials
    const string Lit = "Universal Render Pipeline/Lit";
    const float Press = .009f;

    /// <summary>The machine's paddle names (BeverageDispenserV2.fbx; Cocoa is Hot Chocolate) with each drink's asset name.</summary>
    public static readonly (string paddle, string drink)[] Drinks =
    {
        ("Coffee", "Coffee"), ("Tea", "Tea"), ("Cocoa", "HotChocolate"), ("Espresso", "Espresso"), ("Americano", "Americano"), ("Latte", "Latte"),
    };

    // The icons' own colours (Tools/UI/draw_button_icons.py), as Tools/Blender/drink_buttons.py names them.
    static readonly (string name, string hex, string why)[] PictureMaterials =
    {
        ("DB_Ink", "1f1b17", "the icons' ink: outlines, handles, steam, the label's rim"),
        ("DB_Paper", "f7f3ee", "paper: the label, saucers, foam, the cream, the paper cup, the tea tag"),
        ("DB_PaperEdge", "d6cfc5", "the americano's lid"),
        ("DB_Brown", "6b3f1f", "the coffee mug, the americano's band"),
        ("DB_Dark", "3b2314", "the coffee's surface, the espresso cup"),
        ("DB_Amber", "e0a83e", "the teacup"),
        ("DB_Cocoa", "8a5230", "the hot chocolate mug"),
        ("DB_Crema", "a86a3a", "the espresso's crema"),
        ("DB_Latte", "c99d6a", "the latte glass"),
    };

    public static string CapName(string paddle) => "DB_Cap_" + paddle;
    public static string PictureName(string paddle) => "DB_Picture_" + paddle;
    public static string PaddleName(string paddle) => paddle + " ceramic paddle";
    static string GripName(string paddle) => paddle + " finger grip";
    static string ModelPath(string paddle) => ModelFolder + "/" + CapName(paddle) + ".fbx";

    // ------------------------------------------------------------------ the step

    [MenuItem(Menu + "Buttons 1 - The machine's new buttons (scene and prefab, with Undo)")]
    static void ButtonsMenu()
    {
        var report = new StringBuilder(Tag + Title + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        string folder = LogFolder("buttons-1");
        bool began = false;
        int group = 0;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            Scene scene = SceneManager.GetActiveScene();
            BeverageStation[] stations = Object.FindObjectsByType<BeverageStation>(FindObjectsInactive.Include).Where(s => s.gameObject.scene == scene).ToArray();
            if (stations.Length != 1) throw new InvalidOperationException($"Expected one BeverageStation in the open scene, found {stations.Length}. Open the café scene ({AcesCafeLayoutSetup.ScenePath}).");
            Transform root = stations[0].transform;
            Transform model = root.Find("Visual model") ?? throw new InvalidOperationException("The dispenser has no 'Visual model' (run Fixit Fidget > Content > Install six-drink dispenser first).");
            foreach (var d in Drinks)
                if (!File.Exists(ModelPath(d.paddle))) throw new InvalidOperationException(ModelPath(d.paddle) + " isn't in the project: copy Assets/Art/Models/DrinkButtons over first.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Drink buttons - " + Title);
            group = Undo.GetCurrentGroup();
            began = true;
            int changes = 0;

            report.AppendLine("Materials and import");
            var materials = Materials(report, ref changes);
            foreach (var d in Drinks) Import(ModelPath(d.paddle), materials, report, ref changes);

            report.AppendLine().AppendLine("The scene");
            var caps = new List<(string paddle, Transform paddleT, GameObject cap)>();
            foreach (var d in Drinks)
            {
                Transform paddle = Named(model, PaddleName(d.paddle)) ?? throw new InvalidOperationException($"The dispenser model has no '{PaddleName(d.paddle)}'.");
                var motion = paddle.GetComponent<BeveragePaddleMotion>() ?? throw new InvalidOperationException($"{PaddleName(d.paddle)} has no BeveragePaddleMotion (run the installer first).");
                caps.Add((d.paddle, paddle, Fit(root, paddle, motion, d.paddle, report, ref changes)));
                Retire(root, paddle, model, d, report, ref changes);
            }

            report.AppendLine().AppendLine("Checks");
            int failures = 0, passes = 0;
            void Check(bool ok, string what)
            {
                if (ok) passes++; else failures++;
                report.AppendLine((ok ? "  PASS   " : "  FAIL   ") + what);
            }
            Physics.SyncTransforms();
            var controls = root.GetComponentsInChildren<BeverageControl>(true).Where(c => c.dispenseButton && c.slot != null && c.slot.drink != null).ToList();
            Check(controls.Count == 6, $"the six dispense hit boxes are still there ({controls.Count}): nothing of the controls changed");
            var pictures = new List<(string paddle, Mesh mesh)>();
            foreach (var (paddle, paddleT, cap) in caps)
            {
                string drinkName = DrinkName(root, paddle);
                MeshRenderer capR = Renderer(cap, CapName(paddle)), picR = Renderer(cap, PictureName(paddle));
                Check(capR != null && picR != null, $"{paddle}: the cap and its picture are under the paddle ({(capR != null ? "cap" : "NO cap")}, {(picR != null ? "picture" : "NO picture")})");
                if (capR == null || picR == null) continue;
                Bounds cb = capR.bounds;
                Vector3 size = root.InverseTransformVector(cb.size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                Check(Mathf.Abs(size.x - .092f) < .006f && Mathf.Abs(size.y - .098f) < .006f,
                    $"{paddle}: the cap is {size.x * 1000:0} x {size.y * 1000:0} mm across and up (92 x 98 built), {size.z * 1000:0} mm deep");
                BeverageControl control = controls.FirstOrDefault(c => c.slot.drink.name == "Drink_" + Drinks.First(x => x.paddle == paddle).drink || c.slot.drink.drinkName == drinkName);
                if (control != null)
                {
                    Bounds hit = control.GetComponent<Collider>().bounds;
                    Vector3 off = root.InverseTransformVector(cb.center - hit.center);
                    Check(Mathf.Abs(off.x) < .012f && Mathf.Abs(off.y) < .012f,
                        $"{paddle}: the cap sits on its hit box ({off.x * 1000:+0;-0} mm across, {off.y * 1000:+0;-0} mm up, {off.z * 1000:+0;-0} mm toward the player)");
                }
                else Check(false, $"{paddle}: its dispense hit box was found");
                // The cap stands in front of the backing, toward the player (-Z of the dispenser's root is its working side).
                Vector3 frontLocal = root.InverseTransformPoint(cb.center) - new Vector3(0, 0, Mathf.Abs(root.InverseTransformVector(cb.size).z) / 2f);
                Check(frontLocal.z < -.160f && frontLocal.z > -.185f, $"{paddle}: the cap's front is {-frontLocal.z * 1000:0} mm in front of the dispenser's middle (the old tile's was 162, the spouts start at 205)");
                Vector3 pic = root.InverseTransformVector(picR.bounds.size);
                Check(Mathf.Abs(pic.y) >= .040f && Mathf.Abs(pic.x) >= .030f, $"{paddle}: its picture is {Mathf.Abs(pic.x) * 1000:0} x {Mathf.Abs(pic.y) * 1000:0} mm");
                var pf = picR.GetComponent<MeshFilter>();
                if (pf != null && pf.sharedMesh != null) pictures.Add((paddle, pf.sharedMesh));
                var words = paddleT.GetComponentsInChildren<TMP_Text>(true).Where(t => t.gameObject.activeInHierarchy).ToList();
                Check(words.Count == 0, $"{paddle}: no printed word is left on the button ({words.Count} active)");
                var old = paddleT.GetComponent<MeshRenderer>();
                Check(old == null || !old.enabled, $"{paddle}: the old tile is hidden");
                var motion = paddleT.GetComponent<BeveragePaddleMotion>();
                Check(motion != null && Mathf.Abs(motion.pressedOffset.magnitude - Press) < .0005f, $"{paddle}: the press is {(motion != null ? motion.pressedOffset.magnitude * 1000 : 0):0.0} mm (9 built)");
            }
            // Told apart by picture: every picture is its own mesh (a different shape, not the same mesh in six colours).
            int distinct = pictures.Select(p => p.mesh.vertexCount + "/" + p.mesh.name).Distinct().Count();
            Check(pictures.Count == 6 && distinct == 6, $"the six pictures are six different shapes ({distinct} distinct of {pictures.Count})");
            var capColours = caps.Select(c => { var r = Renderer(c.cap, CapName(c.paddle)); return r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "none"; }).ToList();
            Check(capColours.Distinct().Count() == 6 && capColours.All(n => n.StartsWith("BF_")), "the six caps wear six different machine colours (" + string.Join(", ", capColours) + ")");
            if (failures > 0) throw new InvalidOperationException($"{failures} check(s) failed, so the change is undone and nothing is saved.");

            Undo.CollapseUndoOperations(group);
            if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            report.AppendLine().AppendLine("Photos");
            Photos(stations[0], folder, report);

            string line = $"{Tag}{Title}: DONE, {changes} change(s), {passes} check(s) passed.{(changes > 0 ? " Save the scene." : " (Already done.)")} {folder}";
            report.AppendLine().AppendLine(line);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            Debug.Log(line);
        }
        catch (Exception e)
        {
            if (began) { Undo.RevertAllDownToGroup(group); report.AppendLine("  (the change is undone; nothing is saved)"); }
            string line = $"{Tag}{Title}: FAILED: {e.Message} {folder}";
            report.AppendLine().AppendLine(line);
            if (!(e is InvalidOperationException)) report.AppendLine(e.ToString());
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.LogError(report.ToString());
            Debug.LogError(line);
        }
    }

    [MenuItem(Menu + "Buttons 1 - The machine's new buttons (scene and prefab, with Undo)", true)]
    static bool ButtonsValid() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ------------------------------------------------------------------ the lab

    [MenuItem(Menu + "Lab - The machine's buttons (drives itself)")]
    static void LabMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(DrinkButtonsCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "The machine's buttons: a lab session on a fresh Day 1 that drives itself with a virtual gamepad. Keep the Game view " +
                  $"in front and leave the mouse and keyboard alone until it reports (about a minute). Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Lab - The machine's buttons (drives itself)", true)]
    static bool LabValid() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.HasKey(DrinkButtonsCheck.PendingKey)) { PlayerPrefs.DeleteKey(DrinkButtonsCheck.PendingKey); PlayerPrefs.Save(); }
    }

    // ------------------------------------------------------------------ materials and import

    // The cap's own material is the machine's (BF_<Drink>, made by the installer: the same colour as the tray's chip under
    // that nozzle); the picture's nine are made here once, URP Lit, smoothness .45, not metallic.
    static Dictionary<string, Material> Materials(StringBuilder report, ref int changes)
    {
        var result = new Dictionary<string, Material>();
        foreach (var d in Drinks)
        {
            string path = MaterialFolder + "/BF_" + d.paddle + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException(path + " is missing: run Fixit Fidget > Content > Install six-drink dispenser first.");
            result["BF_" + d.paddle] = mat;
        }
        var shader = Shader.Find(Lit) ?? throw new InvalidOperationException("The Universal Render Pipeline's Lit shader isn't in the project.");
        foreach (var (name, hex, why) in PictureMaterials)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Color colour = new Color32(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);
                mat = new Material(shader) { name = name, color = colour };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .45f);
                AssetDatabase.CreateAsset(mat, path);
                changes++;
                report.AppendLine($"  made {name} #{hex} ({why})");
            }
            result[name] = mat;
        }
        return result;
    }

    // The house's import settings for a Blender piece (BlenderKitSteps.Configure: scale 1, the file's units, no axis bake, no
    // animation, materials by name), set once; a second run finds them set and leaves the model alone.
    static void Import(string path, Dictionary<string, Material> materials, StringBuilder report, ref int changes)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter ?? throw new InvalidOperationException(path + " didn't import as a model.");
        var map = importer.GetExternalObjectMap();
        bool mapped = true;
        foreach (Renderer r in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) { mapped = false; continue; }
                if (!materials.TryGetValue(m.name, out Material target)) throw new InvalidOperationException($"{Path.GetFileName(path)} wears a material this step doesn't know: {m.name}.");
                if (!map.TryGetValue(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), out Object have) || have != target) mapped = false;
            }
        bool settings = Mathf.Approximately(importer.globalScale, 1f) && importer.useFileScale && !importer.bakeAxisConversion
                        && !importer.importAnimation && importer.materialImportMode == ModelImporterMaterialImportMode.ImportStandard;
        if (mapped && settings)
        {
            report.AppendLine($"  {Path.GetFileName(path)}: already imported with its materials mapped");
            return;
        }
        var unmapped = new SortedSet<string>();
        BlenderKitSteps.Configure(path, materials, unmapped);
        if (unmapped.Count > 0) throw new InvalidOperationException($"{Path.GetFileName(path)}: no material for {string.Join(", ", unmapped)}.");
        changes++;
        report.AppendLine($"  {Path.GetFileName(path)}: imported (scale 1, the file's metres, no axis bake, no animation; materials by name)");
    }

    // ------------------------------------------------------------------ the scene

    // The cap under its paddle, at the paddle's origin (the old tile's centre), its front toward the player: the cap was
    // built in that frame, so no offset and no turn. The world pose is set from the dispenser's root (its working face is
    // its -Z), never guessed from the paddle's own axes, and the parent's scale is cancelled so the cap is life size.
    static GameObject Fit(Transform root, Transform paddle, BeveragePaddleMotion motion, string name, StringBuilder report, ref int changes)
    {
        Transform have = paddle.Find(CapName(name));
        GameObject cap;
        if (have != null)
        {
            cap = have.gameObject;
            report.AppendLine($"  {PaddleName(name)}: {CapName(name)} is already under it");
        }
        else
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name)) ?? throw new InvalidOperationException(ModelPath(name) + " didn't import.");
            cap = (GameObject)PrefabUtility.InstantiatePrefab(asset, paddle.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(cap, "Drink buttons - " + CapName(name));
            cap.name = CapName(name);
            cap.transform.SetPositionAndRotation(paddle.position, root.rotation * Quaternion.Euler(0f, 180f, 0f));
            cap.transform.SetParent(paddle, true);
            Vector3 ls = paddle.lossyScale;
            cap.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            changes++;
            report.AppendLine($"  {PaddleName(name)}: {CapName(name)} placed under it at {V(cap.transform.localPosition)}, turned {V(cap.transform.localRotation.eulerAngles)}, scale {cap.transform.localScale.x:0.###}");
        }
        Vector3 press = paddle.parent.InverseTransformVector(root.forward * Press);
        if ((motion.pressedOffset - press).magnitude > .0002f)
        {
            Undo.RecordObject(motion, "Drink buttons - the press");
            Vector3 was = motion.pressedOffset;
            motion.pressedOffset = press;
            EditorUtility.SetDirty(motion);
            changes++;
            report.AppendLine($"  {PaddleName(name)}: the press is {Press * 1000:0} mm (was {was.magnitude * 1000:0.0})");
        }
        return cap;
    }

    // The old tile, its chrome grip and its printed word: hidden, never deleted (Undo, and the old look is a toggle away).
    static void Retire(Transform root, Transform paddle, Transform model, (string paddle, string drink) d, StringBuilder report, ref int changes)
    {
        foreach (Transform t in new[] { paddle, Named(model, GripName(d.paddle)) })
        {
            if (t == null) continue;
            var r = t.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled) continue;
            Undo.RecordObject(r, "Drink buttons - retire " + t.name);
            r.enabled = false;
            changes++;
            report.AppendLine($"  {t.name}: renderer off");
        }
        string drinkName = DrinkName(root, d.paddle);
        var labels = root.GetComponentsInChildren<TMP_Text>(true)
            .Where(t => t.gameObject.activeSelf && (t.transform.IsChildOf(paddle) || t.name == "Printed " + drinkName)).ToList();
        foreach (TMP_Text label in labels)
        {
            Undo.RecordObject(label.gameObject, "Drink buttons - retire " + label.name);
            label.gameObject.SetActive(false);
            changes++;
            report.AppendLine($"  {label.name}: inactive (the word was \"{label.text.Replace("\n", " ")}\" at scale {label.transform.localScale.x:0.000}; the picture says it now)");
        }
    }

    // The drink's shown name for a paddle (the slot's DrinkDefinition.drinkName, "Hot Chocolate" for Cocoa), else the paddle's.
    static string DrinkName(Transform root, string paddle)
    {
        string asset = "Drink_" + Drinks.First(x => x.paddle == paddle).drink;
        foreach (BeverageSlot slot in root.GetComponentsInChildren<BeverageSlot>(true))
            if (slot.drink != null && slot.drink.name == asset) return slot.drink.drinkName;
        return paddle;
    }

    static Transform Named(Transform under, string name) => under.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static MeshRenderer Renderer(GameObject cap, string name) =>
        cap.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name || r.name.StartsWith(name));

    // ------------------------------------------------------------------ photos

    // The close-up's own pose (the straight-on camera under the station), then the overhead camera's pose carried over the
    // stand point, both at 1920 x 1080 (Playtest4BenchSteps.Shoot, Ace's stand-in hidden).
    static void Photos(BeverageStation station, string folder, StringBuilder report)
    {
        try
        {
            var so = new SerializedObject(station.GetComponent<StationInteractable>());
            var camera = so.FindProperty("stationCamera")?.objectReferenceValue as CinemachineCamera;
            if (camera != null)
            {
                Playtest4BenchSteps.Shoot(folder, "1-first-person-at-the-machine", camera.transform.position, camera.transform.rotation, camera.Lens.FieldOfView);
                report.AppendLine("  photo 1-first-person-at-the-machine.png (the close-up's pose)");
            }
            else report.AppendLine("  (no station camera: the close-up photo wasn't taken)");
            GameObject shop = GameObject.Find("CmShopCam");
            Transform stand = station.GetComponent<StationInteractable>().StandPoint;
            GameObject player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
            if (shop != null && stand != null)
            {
                var lens = shop.GetComponent<CinemachineCamera>();
                float fov = lens != null ? lens.Lens.FieldOfView : 44f;
                Vector3 at = player != null ? stand.position + (shop.transform.position - player.transform.position) : shop.transform.position;
                Playtest4BenchSteps.Shoot(folder, "2-overhead-at-the-machine", at, shop.transform.rotation, fov);
                report.AppendLine("  photo 2-overhead-at-the-machine.png (CmShopCam's pose, carried over the stand point)");
            }
            else report.AppendLine("  (no CmShopCam or stand point: the overhead photo wasn't taken)");
        }
        catch (Exception e) { report.AppendLine("  (photos failed: " + e.Message + ")"); }
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);

    static string LogFolder(string name)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Drinks",
            name + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
#endif
