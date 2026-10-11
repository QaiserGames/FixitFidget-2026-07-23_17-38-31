#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4 FIXES: THE INTAKE TRAY (10 Oct 2026; Lead 6, 3D Art & Blender)
//
//   Fixit Fidget > Playtest 4 > Tray > 0 - Check first (read-only)
//   Fixit Fidget > Playtest 4 > Tray > 1 - Put a tray in every intake slot (scene)
//   Fixit Fidget > Playtest 4 > Tray > 2 - The trays in play: Shelf Space shows its trays (lab, drives itself)
//
// The finding (playtester-first-hour, Counter, L): "The intake shelf has no object of its own: devices lie on the bare
// stone in front of the till (slots at x -1.56...1.56, z 13.65), but the card says 'pick up ... from the intake shelf'."
//
// The tray: CK_IntakeTray (Tools/Blender/cafe_intake.py; Assets/Art/Models/CafeKit/CK_IntakeTray.fbx + cafe_intake.json):
// a chestnut tray lined with felt in the brand green, walled on the customers' side, open on Ace's, 0.360 x 0.305 x 0.020.
// Its origin is under the slot point on its own bottom; its felt is 8 mm over the stone, which is where the phones and
// Grace's camera already rest (their anchors put their bottoms 3-4.2 mm over the slot point, and the slot point is 5 mm
// over the stone), so NO SLOT MOVES.
//
// Step 1:
//   - assets (outside the scene's undo, like Kit 1; a second run says "already"): makes or updates CF_Felt.mat (URP Lit,
//     from the manifest: #2E7D5B, smoothness 0.08, metallic 0) and sets CK_IntakeTray.fbx's import settings the way Kit 1
//     does (scale 1, no animation, cameras or lights, readable, no colliders, normals imported), its wood remapped to the
//     café's grain ("Cafe grain - CC_Wood_Counter") and its felt to CF_Felt;
//   - the scene (one undo group): under each slot of the IntakeShelf's ItemSlotArea, a child "CK_IntakeTray (slot N)",
//     local rotation 0, standing on the stone (a ray down under the slot), with an IntakeTray component (Assets/Scripts)
//     that hides the tray until its slot is usable (the base slots from Day 1, the others when Shelf Space buys them).
//     No collider: the device on it keeps E, the crosshair and the aim help (every pick in PlayerInteractor is a ray or an
//     overlap against colliders). Not in CafeViewMode.overheadFixtures: the tray must show from above.
//   - checks in numbers: one tray per slot, its pose and its stone, its height, its felt, its materials, no collider,
//     nothing else where it stands (register v4 behind slot 2 included), every gameplay object where it was (1 mm), every
//     device's bottom against the felt and every device's footprint (with the shelf's jitter) against the rims;
//   - photos before and after (Logs/Playtest4/tray-1-photos-<time>/): the game's overhead camera, the overhead closer,
//     first person at the till, Ace's eye at the counter's stand point and a close one from the customers' side, with
//     the hero phone, the pocket watch and Grace's camera put on the base slots the way PlacementJitter does (no
//     jitter) for the photo only (never saved, removed after).
// It never saves the scene and never deletes anything.
// ---------------------------------------------------------------------------
static class Playtest4TraySteps
{
    const string Menu = Playtest4Shared.Menu + "Tray/";
    const string Title0 = "Tray 0 - Check first (read-only)";
    const string Title1 = "Tray 1 - Put a tray in every intake slot (scene)";

    const string Model = "Assets/Art/Models/CafeKit/CK_IntakeTray.fbx";
    const string ManifestPath = "Assets/Art/Models/CafeKit/cafe_intake.json";
    const string MaterialFolder = "Assets/Art/Materials/Cafe";
    const string FeltName = "CF_Felt";
    const string FeltPath = MaterialFolder + "/" + FeltName + ".mat";
    const string Palette = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";
    const string WoodSource = "CC_Wood_Counter";
    const string Grain = "Cafe grain - CC_Wood_Counter";
    const string ShelfPath = "ACE'S CAFE - layout study 02/02 - intake and queue/IntakeShelf";
    const string CustomerPrefab = "Assets/AssetsPrefabs/Customer.prefab";
    static readonly string[] DeviceFolders = { "Assets/AssetsPrefabs", "Assets/GraceShowcase" };
    // The devices put on the base slots for the photos (slot 0, 1, 2), by prefab name.
    static readonly string[] PhotoDevices = { "PhoneRepair", "PocketWatch", "GraceReunionCamera" };

    static string TrayName(int slot) => $"CK_IntakeTray (slot {slot})";

    [MenuItem(Menu + "0 - Check first (read-only)")]
    static void CheckFirst()
    {
        photos = false;
        Playtest4Shared.Survey(Title0, "tray-0", Survey);
    }

    [MenuItem(Menu + "1 - Put a tray in every intake slot (scene)")]
    static void PutTrays()
    {
        photos = true;
        try { Playtest4Shared.Step(Title1, "tray-1", Survey, Change, Checks); }
        finally { photos = false; }
    }

    // The trays in play: a lab session on a fresh Day 1 (the lab's own save), the autopilot off; IntakeTrayCheck sets
    // Shelf Space from level 0 to its top and back and checks the trays shown follow the usable slots, with a photo a level.
    [MenuItem(Menu + "2 - The trays in play: Shelf Space shows its trays (lab, drives itself)")]
    static void TraysInPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError("[Tray check] Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(IntakeTrayCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[Tray check] The intake trays in play: a lab session on a fresh Day 1 that checks itself (about 15 s). " +
                  $"Keep the Game view in front; stop Play when it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!PlayerPrefs.HasKey(IntakeTrayCheck.PendingKey)) return;
        PlayerPrefs.DeleteKey(IntakeTrayCheck.PendingKey);
        PlayerPrefs.Save();
    }

    [MenuItem(Menu + "0 - Check first (read-only)", true)]
    [MenuItem(Menu + "1 - Put a tray in every intake slot (scene)", true)]
    [MenuItem(Menu + "2 - The trays in play: Shelf Space shows its trays (lab, drives itself)", true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    // =========================================================================================== the manifest

    [Serializable] sealed class Range2 { public float[] x; public float[] z; }
    [Serializable] sealed class PieceDef
    {
        public string name; public int tris; public float[] size; public float bottom; public float back;
        public string[] materials; public float felt_top; public float rim_top; public Range2 felt_bed; public Range2 footprint;
    }
    [Serializable] sealed class MatDef { public string hex; public float smoothness; }
    [Serializable] sealed class MatTable { public MatDef CF_Felt; }
    [Serializable] sealed class ManifestDef { public int version; public PieceDef[] pieces; public MatTable materials; }

    // =========================================================================================== what the survey finds

    sealed class Slot
    {
        public int index;
        public Transform t;
        public Vector3 position;
        public Quaternion rotation;
        public float stone;                 // the stone's height under the slot point (world y)
        public string stoneBy;              // what the ray hit
        public Transform tray;              // an existing tray under it, if any
    }

    sealed class Device
    {
        public string name, path;
        public float rest;                  // JobBase.restHeight
        public float bottom;                // its lowest physical point under its anchor (root frame, negative = below)
        public List<Bounds> parts = new List<Bounds>();     // its physical renderers' mesh bounds, root frame
    }

    static bool photos;
    static string photoFolder;
    static ManifestDef manifest;
    static PieceDef piece;
    static IntakeShelf shelf;
    static ItemSlotArea area;
    static int baseSlots;
    static List<Slot> slots;
    static List<Device> devices;
    static float yawJitter, offsetJitter;
    static List<(Transform t, Vector3 p, Quaternion r, string what)> gameplay;

    static void Survey(Playtest4Shared.Run run)
    {
        Physics.SyncTransforms();

        // ---- the model and its manifest ----
        string manifestFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
        run.Require(File.Exists(manifestFile), $"{ManifestPath} is missing: copy cafe_intake.json and CK_IntakeTray.fbx into Assets/Art/Models/CafeKit, then Fixit Fidget › Checks › Pick up scripts changed from outside (reimport).");
        manifest = JsonUtility.FromJson<ManifestDef>(File.ReadAllText(manifestFile));
        piece = manifest?.pieces?.FirstOrDefault(p => p != null && p.name == "CK_IntakeTray");
        run.Require(piece != null && piece.size != null && piece.size.Length == 3 && piece.felt_bed?.x != null && piece.felt_bed?.z != null
                    && piece.footprint?.x != null && piece.footprint?.z != null && manifest.materials?.CF_Felt != null,
                    $"{ManifestPath} has no complete CK_IntakeTray entry (size, felt_top, rim_top, felt_bed, footprint) or no CF_Felt material.");
        run.Require(AssetDatabase.LoadAssetAtPath<GameObject>(Model) != null, $"{Model} isn't in the project: copy it there, then reimport (Fixit Fidget › Checks › Pick up scripts changed from outside).");
        run.Info($"The model: {Model}, {piece.tris} triangles as built, {F(piece.size[0])} x {F(piece.size[1])} x {F(piece.size[2])} m (across, up, deep); felt {F(piece.felt_top)} m and rims {F(piece.rim_top)} m over its bottom; felt bed x {F(piece.felt_bed.x[0])}..{F(piece.felt_bed.x[1])}, z {F(piece.felt_bed.z[0])}..{F(piece.felt_bed.z[1])} (slot-local; +z toward the queue, the walled side).");
        Material grain = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().FirstOrDefault(m => m.name == Grain);
        run.Require(grain != null, $"\"{Grain}\" isn't in {Palette} (the café's grain, which Kit 1 maps the kit's chestnut to).");
        run.Require(Shader.Find("Universal Render Pipeline/Lit") != null, "The URP Lit shader wasn't found.");
        var felt = AssetDatabase.LoadAssetAtPath<Material>(FeltPath);
        run.Info(felt != null ? $"{FeltPath} is there (it will be checked against the manifest: #{manifest.materials.CF_Felt.hex}, smoothness {manifest.materials.CF_Felt.smoothness:0.00})."
                              : $"{FeltPath} will be made: URP Lit, #{manifest.materials.CF_Felt.hex}, smoothness {manifest.materials.CF_Felt.smoothness:0.00}, metallic 0.");
        run.Info(ImportNeeds(manifest, grain, felt) is string need ? "CK_IntakeTray.fbx's import settings will be set: " + need : "CK_IntakeTray.fbx's import settings are already Kit 1's, its materials mapped.");

        // ---- the intake shelf and its slots ----
        shelf = Playtest4Shared.One<IntakeShelf>(run);
        area = shelf.GetComponent<ItemSlotArea>();
        run.Require(area != null, $"{Playtest4Shared.PathOf(shelf.gameObject)} has no ItemSlotArea.");
        if (Playtest4Shared.PathOf(shelf.gameObject) != ShelfPath)
            run.Info($"The intake shelf is at {Playtest4Shared.PathOf(shelf.gameObject)} (the brief knew it as {ShelfPath}).");
        var so = new SerializedObject(area);
        SerializedProperty slotsProp = run.Prop(so, "slots");
        baseSlots = run.Prop(so, "baseSlots").intValue;
        SerializedProperty source = run.Prop(so, "capacitySource");
        run.Require(source.enumValueIndex == (int)ItemSlotArea.CapacitySource.Shelf, "The intake's ItemSlotArea doesn't grow with Shelf Space (capacitySource isn't Shelf).");
        run.Require(slotsProp.arraySize >= 1, "The intake's ItemSlotArea has no slots.");
        slots = new List<Slot>();
        for (int i = 0; i < slotsProp.arraySize; i++)
        {
            var t = slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            run.Require(t != null, $"The intake's slot {i} is empty in its ItemSlotArea.");
            run.Require(Quaternion.Angle(Quaternion.Euler(0f, t.eulerAngles.y, 0f), t.rotation) < .5f, $"{Playtest4Shared.PathOf(t.gameObject)} is tilted: a tray stands on a level slot only.");
            var s = new Slot { index = i, t = t, position = t.position, rotation = t.rotation, tray = t.Find(TrayName(i)) };
            run.Require(Stone(t.position, out s.stone, out s.stoneBy), $"No stone under {Playtest4Shared.PathOf(t.gameObject)} (a ray down from 0.5 m over it hits nothing solid within 1 m).");
            slots.Add(s);
            run.Info($"slot {i} ({t.name}, {(i < baseSlots ? "open from Day 1" : $"Shelf Space level {i - baseSlots + 1}")}) at {V(t.position)}, turned {t.eulerAngles.y:0}: the stone under it at {F(s.stone)} ({1000f * (t.position.y - s.stone):0.0} mm under the slot point; it is {s.stoneBy}){(s.tray != null ? "; it has its tray already" : "")}");
        }
        run.Require(slots.All(s => Mathf.Abs(s.stone - slots[0].stone) < .002f), "The stone isn't one height under every slot (within 2 mm): a tray would float or sink somewhere.");
        run.Info($"Base slots (open from Day 1): {baseSlots}; Shelf Space adds the rest ({slots.Count - baseSlots}). Upgrade_ShelfSpace: one slot a level.");
        run.Info($"The felt's top will be at {F(slots[0].stone + piece.felt_top)} ({1000f * (slots[0].stone + piece.felt_top - slots[0].position.y):+0.0;-0.0} mm against the slot point); the slots don't move.");

        // ---- every device that can land on a slot, against the felt ----
        ReadJitter(run);
        devices = Devices();
        run.Require(devices.Count > 0, "No device prefabs (a JobBase that isn't a drink) were found in Assets/AssetsPrefabs or Assets/GraceShowcase.");
        float feltOverSlot = slots[0].stone + piece.felt_top - slots[0].position.y;
        float stoneOverSlot = slots[0].stone - slots[0].position.y;
        foreach (Device d in devices)
        {
            float over = d.rest + d.bottom;              // its lowest point against the slot point
            run.Info($"{d.name}: rest height {F(d.rest)}, its lowest point {1000f * d.bottom:0.0} mm under its anchor: it lands {1000f * over:+0.0;-0.0} mm against the slot point, {1000f * (over - stoneOverSlot):+0.0;-0.0} mm against the bare stone today, {1000f * (over - feltOverSlot):+0.0;-0.0} mm against the felt.");
        }

        // ---- what must not move ----
        gameplay = new List<(Transform, Vector3, Quaternion, string)>();
        void Keep(Transform t, string what) { if (t != null) gameplay.Add((t, t.position, t.rotation, what)); }
        Keep(shelf.transform, "the intake shelf");
        foreach (Slot s in slots) Keep(s.t, $"intake slot {s.index}");
        foreach (StationInteractable st in Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Include))
        {
            Keep(st.transform, $"the station \"{st.name}\"");
            Keep(st.StandPoint, $"the station \"{st.name}\"'s stand point");
        }
        foreach (CounterQueue q in Object.FindObjectsByType<CounterQueue>(FindObjectsInactive.Include)) Keep(q.transform, "the queue");
        run.Info($"Kept for the check after: {gameplay.Count} gameplay transforms (the shelf, its slots, every station and stand point, the queue).");

        if (photos)
        {
            photoFolder = Path.Combine(Playtest4Shared.LogRoot, "tray-1-photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(photoFolder);
            Photographs(run, "1-before", false);
        }
    }

    // =========================================================================================== the change

    static void Change(Playtest4Shared.Run run)
    {
        // ---- assets: the felt, the model's import ----
        Material felt = MakeFelt(run);
        Material grain = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().First(m => m.name == Grain);
        string need = ImportNeeds(manifest, grain, felt);
        if (need != null)
        {
            Configure(grain, felt);
            run.Changed($"{Model}: import settings set (Kit 1's: scale 1, no animation, cameras or lights, readable, no colliders, normals imported; {WoodSource} → \"{Grain}\", {FeltName} → {FeltPath}): {need}");
        }
        else run.Info($"{Model}: import settings already Kit 1's, its materials mapped.");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        run.Require(model != null, $"{Model} didn't import.");

        // ---- the scene: a tray under each slot ----
        foreach (Slot s in slots)
        {
            if (s.tray != null)
            {
                run.Info($"{Playtest4Shared.PathOf(s.tray.gameObject)}: already there.");
                var existing = s.tray.GetComponent<IntakeTray>();
                if (existing == null) existing = run.Add<IntakeTray>(s.tray.gameObject, "it shows only while its slot is usable");
                run.SetInt(existing, "slotIndex", s.index, $"{s.tray.name} · IntakeTray.slotIndex");
                run.SetInt(existing, "baseSlots", baseSlots, $"{s.tray.name} · IntakeTray.baseSlots");
                continue;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, s.t);
            Undo.RegisterCreatedObjectUndo(go, run.Title);
            go.name = TrayName(s.index);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = new Vector3(0f, s.stone - s.position.y, 0f);
            run.Changed($"{Playtest4Shared.PathOf(go)}: made (the intake tray under slot {s.index}, standing on the stone at {F(s.stone)}: its local y {F(s.stone - s.position.y)})");
            IntakeTray tray = run.Add<IntakeTray>(go, "it shows only while its slot is usable (Shelf Space)");
            run.SetInt(tray, "slotIndex", s.index, $"{go.name} · IntakeTray.slotIndex");
            run.SetInt(tray, "baseSlots", baseSlots, $"{go.name} · IntakeTray.baseSlots");
            s.tray = go.transform;
        }
        run.Info("The slots stay where they are (y " + F(slots[0].position.y) + "): the felt meets the devices' bottoms, so nothing is raised.");
    }

    // =========================================================================================== the checks

    static void Checks(Playtest4Shared.Run run)
    {
        Physics.SyncTransforms();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);

        // ---- the model as imported ----
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        run.Check(filters.Length == 1, $"{Model} is one mesh ({filters.Length} found)");
        if (filters.Length == 1)
        {
            Mesh mesh = filters[0].sharedMesh;
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
            int tris = (int)(indices / 3);
            Bounds b = Transformed(mesh.bounds, model.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix);
            var built = new Vector3(piece.size[0], piece.size[1], piece.size[2]);
            run.Check(Mathf.Abs(tris - piece.tris) <= 2, $"its triangles: {tris} (built {piece.tris})");
            run.Check((b.size - built).magnitude < .002f, $"its size {V(b.size)} (built {V(built)})");
            run.Check(Mathf.Abs(b.min.y - piece.bottom) < .001f, $"it stands on its origin: its bottom at {F(b.min.y)} (built {F(piece.bottom)})");
            run.Check(mesh.isReadable, "its mesh is readable (the checks read the felt's top off it)");
        }
        Material grain = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().First(m => m.name == Grain);
        Material felt = AssetDatabase.LoadAssetAtPath<Material>(FeltPath);
        run.Check(felt != null && felt.shader != null && felt.shader.name == "Universal Render Pipeline/Lit"
                  && ColorUtility.ToHtmlStringRGB(felt.GetColor("_BaseColor")) == manifest.materials.CF_Felt.hex.ToUpperInvariant()
                  && Mathf.Abs(felt.GetFloat("_Smoothness") - manifest.materials.CF_Felt.smoothness) < .005f && felt.GetFloat("_Metallic") == 0f,
                  $"{FeltPath}: URP Lit, #{(felt != null ? ColorUtility.ToHtmlStringRGB(felt.GetColor("_BaseColor")) : "?")}, smoothness {(felt != null ? felt.GetFloat("_Smoothness") : -1f):0.00}, metallic 0 (the manifest: #{manifest.materials.CF_Felt.hex}, {manifest.materials.CF_Felt.smoothness:0.00})");

        // ---- one tray per slot, where it belongs ----
        float feltTop;
        foreach (Slot s in slots)
        {
            Transform t = s.t.Find(TrayName(s.index));
            string label = $"slot {s.index}'s tray";
            if (!run.Check(t != null, $"{label}: {TrayName(s.index)} is a child of {Playtest4Shared.PathOf(s.t.gameObject)}")) continue;
            var r = t.GetComponentInChildren<MeshRenderer>(true);
            var mf = t.GetComponentInChildren<MeshFilter>(true);
            var tray = t.GetComponent<IntakeTray>();
            run.Check(tray != null && tray.SlotIndex == s.index && SerializedInt(tray, "baseSlots") == baseSlots,
                      $"{label}: an IntakeTray for slot {s.index} with the shelf's {baseSlots} base slots (found slot {(tray != null ? tray.SlotIndex : -1)}, base {(tray != null ? SerializedInt(tray, "baseSlots") : -1)})");
            run.Check(Quaternion.Angle(t.localRotation, Quaternion.identity) < .1f && (t.lossyScale - Vector3.one).magnitude < .001f,
                      $"{label}: turned with its slot ({t.eulerAngles.y:0}, the slot {s.t.eulerAngles.y:0}: its walled side to the queue), scale 1");
            run.Check(t.GetComponentsInChildren<Collider>(true).Length == 0, $"{label}: no collider (E, the crosshair and the aim help still find the device on it)");
            if (r == null || mf == null || mf.sharedMesh == null) { run.Check(false, $"{label}: it has its mesh and renderer"); continue; }
            string[] mats = r.sharedMaterials.Select(m => m != null ? m.name : "NONE").ToArray();
            run.Check(r.sharedMaterials.Length == 2 && r.sharedMaterials.Contains(grain) && r.sharedMaterials.Contains(felt),
                      $"{label}: its materials are \"{Grain}\" and {FeltName} ({string.Join(", ", mats)})");

            Bounds b = r.bounds;
            run.Check(Mathf.Abs(b.min.y - s.stone) < .001f, $"{label}: its bottom {F(b.min.y)} on the stone {F(s.stone)} (not floating, not sunk)");
            run.Check(Mathf.Abs(b.max.y - (s.stone + piece.rim_top)) < .001f, $"{label}: its rims' top at {F(b.max.y)} ({1000f * (b.max.y - s.stone):0.0} mm over the stone)");
            feltTop = FeltTop(mf, r, felt);
            run.Check(Mathf.Abs(feltTop - (s.stone + piece.felt_top)) < .001f, $"{label}: its felt's top at {F(feltTop)} ({1000f * (feltTop - s.stone):0.0} mm over the stone, {1000f * (feltTop - s.position.y):+0.0;-0.0} mm against the slot point)");

            // The stone under all four corners of its footprint (no corner over the edge, the step or the lowered section).
            bool allStone = true;
            var lows = new List<string>();
            foreach (var (cx, cz) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
            {
                Vector3 corner = t.TransformPoint(new Vector3(piece.footprint.x[cx] * .97f, 0f, piece.footprint.z[cz] * .97f));
                bool hit = Stone(corner, out float y, out string by);
                if (!hit || Mathf.Abs(y - s.stone) > .001f) allStone = false;
                lows.Add(hit ? F(y) : "nothing");
            }
            run.Check(allStone, $"{label}: the stone under its four corners ({string.Join(", ", lows)}): it sits whole on the top");

            // Nothing else stands where it goes.
            var clash = Clashes(t, r);
            run.Check(clash.Count == 0, $"{label}: nothing else stands where it goes{(clash.Count > 0 ? ": " + string.Join("; ", clash) : "")}");
            run.Check(tray != null && tray.IsOpen == (s.index < baseSlots),
                      $"{label}: in play it shows {(s.index < baseSlots ? "from Day 1" : $"once Shelf Space reaches level {s.index - baseSlots + 1}")} (IntakeTray.IsOpen now, with no upgrade: {(tray != null && tray.IsOpen ? "yes" : "no")})");
        }

        // The register stands right behind slot 2: say how close.
        Renderer register = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .FirstOrDefault(x => x.gameObject.name.StartsWith("Register v4", StringComparison.Ordinal));
        if (register != null)
            foreach (Slot s in slots)
            {
                Transform t = s.t.Find(TrayName(s.index));
                Renderer r = t != null ? t.GetComponentInChildren<MeshRenderer>(true) : null;
                if (r == null) continue;
                float gx = Mathf.Max(register.bounds.min.x - r.bounds.max.x, r.bounds.min.x - register.bounds.max.x);
                if (gx > .05f) continue;
                float gz = register.bounds.min.z - r.bounds.max.z;
                run.Check(gz >= .002f, $"slot {s.index}'s tray: {1000f * gz:0.0} mm from register v4 behind it (its open side faces it; 2 mm or more)");
            }
        else run.Info("Register v4 wasn't found by name (\"Register v4 …\"): the general clash check above stands for it.");

        // ---- the gameplay objects never moved ----
        int moved = 0;
        foreach (var (t, p, q, what) in gameplay)
            if (t == null || (t.position - p).magnitude > .001f || Quaternion.Angle(t.rotation, q) > .1f)
            {
                moved++;
                run.Info($"MOVED: {what} ({(t == null ? "gone" : V(p) + " → " + V(t.position))})");
            }
        run.Check(moved == 0, $"the {gameplay.Count} gameplay transforms (the shelf, its slots, the stations and stand points, the queue) stand where they stood (1 mm, 0.1°)");

        // ---- every device against the felt and the rims ----
        Slot s0 = slots[0];
        float feltOverSlot = s0.stone + piece.felt_top - s0.position.y;
        float stoneOverSlot = s0.stone - s0.position.y;
        float rimOverSlot = s0.stone + piece.rim_top - s0.position.y;
        int resting = 0;
        foreach (Device d in devices)
        {
            float over = d.rest + d.bottom - feltOverSlot;       // + floats over the felt, - sinks into it
            bool sankBefore = d.rest + d.bottom < stoneOverSlot - .0005f;
            if (over > .0025f)
                run.Check(false, $"{d.name}: {1000f * over:0.0} mm over the felt (it would hover: the felt is too low for it)");
            else if (over >= -.0015f)
            {
                resting++;
                run.Check(true, $"{d.name}: rests on the felt ({1000f * over:+0.0;-0.0} mm)");
            }
            else if (sankBefore)
                run.Info($"FLAG {d.name}: {1000f * -over:0.0} mm into the tray (its rest height {F(d.rest)} is {1000f * -(d.rest + d.bottom - stoneOverSlot):0.0} mm short of its own lowest point: it already sank that far into the bare stone). " +
                         $"Its prefab's JobBase.restHeight {F(d.rest + (feltOverSlot - (d.rest + d.bottom)) + .0003f)} puts it on the felt (the bench lead's file).");
            else
                run.Check(false, $"{d.name}: {1000f * -over:0.0} mm into the felt (the felt is too high for it)");

            // Its footprint, at the shelf's jitter (±{yawJitter}°, ±{offsetJitter} m), against the rims (parts below the rims only).
            Footprint(d, rimOverSlot, out float xMax, out float zMin, out float zMax, out float xMax0, out float zMin0, out float zMax0);
            float rimX = piece.felt_bed.x[1], rimZ = piece.felt_bed.z[1], edgeZ = piece.footprint.z[0];
            bool exact = xMax <= rimX + .0005f && zMax <= rimZ + .0005f && zMin >= edgeZ - .0005f;
            bool atRest = xMax0 <= rimX + .0005f && zMax0 <= rimZ + .0005f && zMin0 >= edgeZ - .0005f;
            if (exact)
                run.Check(true, $"{d.name}: inside the rims at the shelf's worst turn and nudge (±{yawJitter:0}°, ±{100f * offsetJitter:0} cm): x ±{F(xMax)} of ±{F(rimX)}, z {F(zMin)}..{F(zMax)} of {F(edgeZ)}..{F(rimZ)}");
            else if (atRest)
                run.Info($"{d.name}: inside the rims at its rest turn (x ±{F(xMax0)}, z {F(zMin0)}..{F(zMax0)}); its parts' boxes would reach x ±{F(xMax)}, z {F(zMin)}..{F(zMax)} at ±{yawJitter:0}°, true only for a box-shaped part " +
                         "(Blender measured the round pocket watch vertex by vertex inside: x ±0.095, z -0.095..+0.149; the manifest's device_envelope).");
            else
                run.Check(false, $"{d.name}: crosses a rim even at its rest turn: x ±{F(xMax0)} (rims ±{F(rimX)}), z {F(zMin0)}..{F(zMax0)} (rim {F(rimZ)}, open edge {F(edgeZ)})");
        }
        run.Check(resting > 0, $"{resting} of {devices.Count} devices rest on the felt as they are");

        if (photos) Photographs(run, "2-after", true);
    }

    // =========================================================================================== assets

    static Material MakeFelt(Playtest4Shared.Run run)
    {
        MatDef def = manifest.materials.CF_Felt;
        Color colour = ColorUtility.TryParseHtmlString("#" + def.hex.TrimStart('#'), out Color c) ? c : Color.magenta;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialFolder)?.Replace('\\', '/'), Path.GetFileName(MaterialFolder));
        Material m = AssetDatabase.LoadAssetAtPath<Material>(FeltPath);
        bool made = m == null;
        if (!made && m.shader == lit && ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")) == ColorUtility.ToHtmlStringRGB(colour)
            && Mathf.Abs(m.GetFloat("_Smoothness") - def.smoothness) < .005f && m.GetFloat("_Metallic") == 0f && m.GetTexture("_BaseMap") == null)
        {
            run.Info($"{FeltPath}: already #{def.hex}, smoothness {def.smoothness:0.00}.");
            return m;
        }
        if (made)
        {
            m = new Material(lit) { name = FeltName };
            AssetDatabase.CreateAsset(m, FeltPath);
        }
        // URP Lit, opaque, flat colour, matte: the way Kit 1 defines the kit's own materials (CafeKitSteps.Define).
        if (m.shader != lit) m.shader = lit;
        m.enableInstancing = true;
        m.SetTexture("_BaseMap", null);
        m.SetTexture("_BumpMap", null);
        m.DisableKeyword("_NORMALMAP");
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Smoothness", def.smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = -1;
        m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        run.Changed($"{FeltPath}: {(made ? "made" : "updated")} (URP Lit, #{def.hex}, smoothness {def.smoothness:0.00}, metallic 0: the intake tray's felt)");
        return m;
    }

    // What differs from Kit 1's import settings and this model's remaps (null: nothing).
    static string ImportNeeds(ManifestDef m, Material grain, Material felt)
    {
        var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
        if (importer == null) return "(not imported yet)";
        var needs = new List<string>();
        if (!Mathf.Approximately(importer.globalScale, 1f) || !importer.useFileScale || importer.bakeAxisConversion) needs.Add("scale");
        if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None) needs.Add("animation");
        if (importer.importCameras || importer.importLights || importer.importVisibility || importer.importBlendShapes) needs.Add("cameras, lights, visibility, blend shapes");
        if (importer.meshCompression != ModelImporterMeshCompression.Off || !importer.isReadable) needs.Add("readable, uncompressed");
        if (importer.addCollider) needs.Add("no collider");
        if (importer.importNormals != ModelImporterNormals.Import) needs.Add("normals");
        Dictionary<AssetImporter.SourceAssetIdentifier, Object> map = importer.GetExternalObjectMap();
        Object Mapped(string source) => map.FirstOrDefault(p => p.Key.type == typeof(Material) && p.Key.name == source).Value;
        if (Mapped(WoodSource) != grain) needs.Add($"{WoodSource} → \"{Grain}\"");
        if (felt == null || Mapped(FeltName) != felt) needs.Add($"{FeltName} → {FeltPath}");
        return needs.Count == 0 ? null : string.Join("; ", needs);
    }

    // Kit 1's model settings (CafeKitSteps.Configure), for this one model.
    static void Configure(Material grain, Material felt)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = true;             // the checks read the felt's top off the mesh
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var old in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            if (old.name != WoodSource && old.name != FeltName) importer.RemoveRemap(old);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), WoodSource), grain);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), FeltName), felt);
        importer.SaveAndReimport();
    }

    // =========================================================================================== measuring

    // The stone under a point: a ray down from 0.5 m over it, triggers ignored (the counter station's thin reach trigger
    // lies 1 mm over the stone).
    static bool Stone(Vector3 at, out float y, out string by)
    {
        y = float.NaN;
        by = "nothing";
        if (!Physics.Raycast(new Vector3(at.x, at.y + .5f, at.z), Vector3.down, out RaycastHit hit, 1.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        y = hit.point.y;
        by = Playtest4Shared.PathOf(hit.collider.gameObject);
        return true;
    }

    // The felt's top: the highest point of the felt's submesh, in the world.
    static float FeltTop(MeshFilter mf, Renderer r, Material felt)
    {
        Mesh mesh = mf.sharedMesh;
        if (!mesh.isReadable) return float.NaN;
        int sub = Array.IndexOf(r.sharedMaterials, felt);
        if (sub < 0 || sub >= mesh.subMeshCount) return float.NaN;
        Vector3[] v = mesh.vertices;
        float top = float.MinValue;
        foreach (int i in mesh.GetIndices(sub)) top = Mathf.Max(top, mf.transform.TransformPoint(v[i]).y);
        return top;
    }

    // Other renderers whose boxes reach into the tray's (shrunk 1.5 mm): the stone itself and big room pieces left out.
    static List<string> Clashes(Transform tray, Renderer own)
    {
        Bounds b = own.bounds;
        b.Expand(-.003f);
        var found = new List<string>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r == null || !r.enabled || r.transform.IsChildOf(tray)) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.transform.IsChildOf(shelf.transform) && r.GetComponentInParent<IntakeTray>() != null) continue;     // another tray (they never overlap: 0.42 m apart)
            Bounds o = r.bounds;
            if (o.size.x > 3f || o.size.z > 3f || o.size.y > 3f) continue;                          // the counter, the room
            if (!o.Intersects(b)) continue;
            found.Add($"{Playtest4Shared.PathOf(r.gameObject)} ({V(o.min)}..{V(o.max)})");
        }
        return found;
    }

    static void ReadJitter(Playtest4Shared.Run run)
    {
        yawJitter = 12f;
        offsetJitter = .02f;
        var customer = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        CustomerBrain brain = customer != null ? customer.GetComponentInChildren<CustomerBrain>(true) : null;
        if (brain == null) { run.Info($"No CustomerBrain on {CustomerPrefab}: the shelf's jitter is taken as 12° and 2 cm (CustomerBrain's defaults)."); return; }
        var so = new SerializedObject(brain);
        SerializedProperty yaw = so.FindProperty("shelfYawJitter"), off = so.FindProperty("shelfOffsetJitter");
        if (yaw != null) yawJitter = yaw.floatValue;
        if (off != null) offsetJitter = off.floatValue;
        run.Info($"The shelf's jitter ({CustomerPrefab}, CustomerBrain): ±{yawJitter:0.#}° turn, ±{100f * offsetJitter:0.#} cm nudge{(yaw == null || off == null ? " (a field wasn't found; its default used)" : "")}.");
    }

    // Every device that can be a job on the shelf: a prefab with a JobBase that isn't a drink.
    static List<Device> Devices()
    {
        var result = new List<Device>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", DeviceFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            JobBase job = root != null ? root.GetComponent<JobBase>() : null;
            if (job == null || job is DrinkJob) continue;
            var d = new Device { name = root.name, path = path, rest = job.restHeight, bottom = float.MaxValue };
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !ActiveIn(mf.transform, root.transform)) continue;
                if (mf.transform is RectTransform) continue;                       // the job's tag (text), not the device
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                Bounds b = Transformed(mf.sharedMesh.bounds, toRoot * mf.transform.localToWorldMatrix);
                d.parts.Add(b);
                d.bottom = Mathf.Min(d.bottom, b.min.y);
            }
            if (d.parts.Count > 0) result.Add(d);
        }
        return result.OrderBy(x => x.name, StringComparer.Ordinal).ToList();
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

    // A device's parts below the rims, at the shelf's jitter, in the slot's frame (+z toward the queue): the box corners of
    // each part turned through ±yaw and nudged ±offset (PlacementJitter), and the same at its rest turn with no nudge.
    static void Footprint(Device d, float rimOverSlot, out float xMax, out float zMin, out float zMax, out float xMax0, out float zMin0, out float zMax0)
    {
        xMax = 0f; zMin = float.MaxValue; zMax = float.MinValue;
        xMax0 = 0f; zMin0 = float.MaxValue; zMax0 = float.MinValue;
        foreach (Bounds b in d.parts)
        {
            if (d.rest + b.min.y >= rimOverSlot) continue;                       // it passes over the rims (the camera's strap)
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, 0f, (k & 2) == 0 ? b.min.z : b.max.z);
                xMax0 = Mathf.Max(xMax0, Mathf.Abs(p.x));
                zMin0 = Mathf.Min(zMin0, p.z);
                zMax0 = Mathf.Max(zMax0, p.z);
                for (int a = -4; a <= 4; a++)
                {
                    Vector3 q = Quaternion.Euler(0f, yawJitter * a / 4f, 0f) * p;
                    xMax = Mathf.Max(xMax, Mathf.Abs(q.x) + offsetJitter);
                    zMin = Mathf.Min(zMin, q.z - offsetJitter);
                    zMax = Mathf.Max(zMax, q.z + offsetJitter);
                }
            }
        }
        if (zMin0 == float.MaxValue) { zMin0 = zMax0 = zMin = zMax = 0f; }
    }

    static int SerializedInt(Object o, string field)
    {
        SerializedProperty p = new SerializedObject(o).FindProperty(field);
        return p != null ? p.intValue : int.MinValue;
    }

    // =========================================================================================== photos

    // Before and after from the views a player has: the game's overhead camera, the overhead closer, first person at the
    // till, Ace's eye at the counter's stand point, and a close one from the queue. The hero phone, the pocket watch and
    // Grace's camera are put on the base slots for the photos only (as PlacementJitter would, with no jitter), hidden from
    // the hierarchy and never saved, and removed after. After: the trays the game shows on Day 1 (the upgrade slots'
    // trays are hidden for the shot, as IntakeTray hides them), then all five (Shelf Space bought).
    static void Photographs(Playtest4Shared.Run run, string phase, bool after)
    {
        var temporary = new List<GameObject>();
        var hidden = new List<Renderer>();
        try
        {
            for (int i = 0; i < PhotoDevices.Length && i < slots.Count; i++)
            {
                Device d = devices.FirstOrDefault(x => x.name == PhotoDevices[i]);
                GameObject prefab = d != null ? AssetDatabase.LoadAssetAtPath<GameObject>(d.path) : null;
                if (prefab == null) continue;
                GameObject go = Object.Instantiate(prefab);
                go.name = PhotoDevices[i] + " (photo only)";
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetPositionAndRotation(slots[i].t.position + Vector3.up * d.rest, slots[i].t.rotation);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.transform is RectTransform) { r.forceRenderingOff = true; }      // the job's tag isn't shown off the shelf
                temporary.Add(go);
            }
            if (after)
                foreach (Slot s in slots.Where(x => x.index >= baseSlots))
                {
                    Transform t = s.t.Find(TrayName(s.index));
                    if (t == null) continue;
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                        if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
                }

            var shots = new List<string>();
            GameObject shop = GameObject.Find("CmShopCam");
            if (shop != null)
            {
                Shoot(Path.Combine(photoFolder, phase + "-1-the-game-camera.png"), shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
                shots.Add("the game camera");
            }
            Vector3 mid = new Vector3(0f, slots[0].position.y, slots[0].position.z);
            float pitch = 50f * Mathf.Deg2Rad;
            Shoot(Path.Combine(photoFolder, phase + "-2-the-trays-from-above.png"), mid + new Vector3(0f, 5f * Mathf.Sin(pitch), -5f * Mathf.Cos(pitch)), mid, 40f, true);
            shots.Add("from above, closer");
            GameObject counterCam = GameObject.Find("CmCounterCam");
            if (counterCam != null)
            {
                Shoot(Path.Combine(photoFolder, phase + "-3-first-person-at-the-till.png"), counterCam.transform.position, counterCam.transform.position + counterCam.transform.forward * 10f, 60f, false);
                shots.Add("first person at the till (the counter camera)");
            }
            StationInteractable counter = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude).FirstOrDefault(x => x.name == "Counter");
            Vector3 stand = counter != null && counter.StandPoint != null ? counter.StandPoint.position : new Vector3(-.26f, 0f, 14.54f);
            Vector3 eye = new Vector3(stand.x, stand.y + 1.65f, stand.z);
            Shoot(Path.Combine(photoFolder, phase + "-4-aces-eye-at-the-counter.png"), eye, new Vector3(slots[0].position.x * .35f, slots[0].position.y, slots[0].position.z), 60f, false);
            shots.Add("Ace's eye at the counter's stand point");
            Vector3 one = slots.Count > 1 ? slots[1].position : slots[0].position;
            Shoot(Path.Combine(photoFolder, phase + "-5-close-from-the-queue.png"), one + new Vector3(.17f, .32f, -.63f), one + new Vector3(0f, .01f, .01f), 40f, false);
            shots.Add("close, from the queue (slot 1)");
            if (after && hidden.Count > 0)
            {
                foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false;
                hidden.Clear();
                Shoot(Path.Combine(photoFolder, phase + "-6-shelf-space-bought.png"), mid + new Vector3(0f, 5f * Mathf.Sin(pitch), -5f * Mathf.Cos(pitch)), mid, 40f, true);
                shots.Add("all five (Shelf Space bought)");
            }
            run.Info($"Photos ({phase}): {string.Join(", ", shots)}, in {photoFolder}.");
        }
        catch (Exception e)
        {
            run.Info($"The photos ({phase}) weren't all taken: {e.Message}");
        }
        finally
        {
            foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false;
            foreach (GameObject go in temporary) if (go != null) Object.DestroyImmediate(go);
        }
    }

    // CafeSecondPassSteps.Capture's way (1440 x 900; isometric hides what the overhead view hides: the overhead fixtures
    // and the walls between the camera and the room), copied here so this step stands on its own.
    static void Shoot(string path, Vector3 position, Vector3 target, float fov, bool isometric)
    {
        var hidden = new List<Renderer>();
        if (isometric)
        {
            CafeViewMode mode = Object.FindObjectsByType<CafeViewMode>(FindObjectsInactive.Include).FirstOrDefault();
            if (mode != null)
            {
                hidden.AddRange(mode.overheadFixtures.Where(r => r != null && r.enabled && !r.forceRenderingOff));
                Vector3 centre = new Vector3(0f, 1.2f, 9f);
                foreach (Renderer wall in mode.cutawayWalls.Where(r => r != null && r.enabled && !r.forceRenderingOff))
                {
                    Vector3 toCentre = centre - position;
                    if (wall.bounds.IntersectRay(new Ray(position, toCentre.normalized), out float hit) && hit < toCentre.magnitude - .02f)
                        hidden.Add(wall);
                }
            }
        }
        foreach (Renderer r in hidden) r.forceRenderingOff = true;
        var g = new GameObject("Temporary tray review camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = g.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1440, 900, 24);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            cam.fieldOfView = fov;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 250f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false;
            Object.DestroyImmediate(g);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    // =========================================================================================== helpers

    static Bounds Transformed(Bounds b, Matrix4x4 m)
    {
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    static string F(float f) => f.ToString("0.000", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
}
#endif
