#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// NPC WAVE 2 (14 Oct 2026): the repair-bar stools, settled guests, dealt looks, less robotic. The spec is
// specs/npc-stools-guests-looks.md; the playtest-5 notes it answers are his girlfriend's: the café "felt empty", people
// "wouldn't sit down on the bar stools that are infront of the repair station", and "as for now it seems robotic".
//
// The code does the behaviour; these steps put the data in the scene (and, for the palettes, the prefabs). Each one records
// what it did (the Console and Logs/NpcWave2/), groups its work under one Undo (Edit > Undo puts it back while the session
// lasts; its own Undo menu does it later), says "already done" and changes nothing when run twice, saves the scene, and
// refuses to run in Play mode or in another scene. Nothing they change is visible in Edit mode (seat data, a list of guests,
// a number), so they take no photos; Life 4 photographs the sitters, the guests and the looks in Play mode.
//
//   Wave 2 1 - The repair-bar stools sit (scene). The three repair-bar stools: Repair Bar on (bench-job customers take them
//              first), Snap To Seat on, Max Lift 0.32 (the body rises inside the sit clips to the 0.745 m seat), Approach Side
//              0.50 (between stools 1.0 m apart), and a Face Towards point 1 m out at 45 degrees (west turned west, the
//              middle and east turned east): the knees clear the counter, which has no knee space, and no two neighbours'
//              feet meet. Seats 1 lays the same data on a rerun (RepairBarSeats below).
//   Wave 2 2 - Settled guests and the patrons' rhythm (scene). PatronSpawner: three Opening Guests (a reader in the
//              reading corner, a pair at a banquette 2-top, a laptop worker at the communal table's east 1), Days 1-3, leaving
//              at 30 / 40 / 55 % of the day; Spawn Jitter 0.35 -> 0.6 (arrivals in clumps and lulls).
//   Wave 2 3 - The names' sexes (scene). CustomerSpawner's First Name Sexes, beside its First Names: the NPC lead's
//              proposal, for his sister to correct. Needs the CustomerSpawner patch in the NPC builder's report; without it
//              the step says so and changes nothing.
//   Wave 2 6a/6b - Palette survey and line-up (read-only), then allow the alternates (prefabs): only if a look's body is
//              drawn with a POLYGON City atlas that has alternates in Synty/PolygonCity/Materials/Alts.
//   Life 4 - Stools, settled guests, looks (Play mode, in Life 4's own lab session): the spec's §6 lab.
// ---------------------------------------------------------------------------

/// <summary>The repair-bar stools' sitting data (NPC wave 2): Wave 2 1 sets it in the scene, Seats 1 lays it on a rerun.</summary>
internal static class RepairBarSeats
{
    public const float MaxLift = .32f;        // the lift needed: 0.745 + 0.085 x 1.1 - 0.494 x 1.1 = 0.295 m (NpcSitData, NPC scale 1.1)
    public const float ApproachSide = .50f;   // the stools are 1.0 m apart: 0.50 passes between them (0.62 brushed the next one)
    public const float FaceOut = 1f, FaceHeight = 1f;
    public const string FaceName = "FaceTowards";
    // West turned west, the middle and east turned east (degrees from the stools' square facing, north to the counter).
    public static readonly (string label, float turn)[] Turns = { ("west", -45f), ("middle", 45f), ("east", 45f) };
    public static string SeatName(string label) => $"Seat (repair bar, {label})";
    public static Vector3 FacePoint(Vector3 seatPose, float turn)
    {
        Vector3 d = Quaternion.Euler(0f, turn, 0f) * Vector3.forward * FaceOut;
        return new Vector3(seatPose.x + d.x, FaceHeight, seatPose.z + d.z);
    }
}

public static class NpcWave2Steps
{
    const string Menu = "Fixit Fidget/NPC/";
    const string MenuStools = Menu + "Wave 2 1 - The repair-bar stools sit (scene)";
    const string MenuStoolsUndo = Menu + "Wave 2 1 - Undo (scene)";
    const string MenuGuests = Menu + "Wave 2 2 - Settled guests and the patrons' rhythm (scene)";
    const string MenuGuestsUndo = Menu + "Wave 2 2 - Undo (scene)";
    const string MenuNames = Menu + "Wave 2 3 - The names' sexes (scene; after the CustomerSpawner patch)";
    const string MenuNamesUndo = Menu + "Wave 2 3 - Undo (scene)";
    const string MenuPaletteSurvey = Menu + "Wave 2 6a - Palette survey and line-up (read-only)";
    const string MenuPaletteAllow = Menu + "Wave 2 6b - Allow the palette alternates (prefabs)";
    const string MenuPaletteUndo = Menu + "Wave 2 6b - Undo (prefabs)";
    const string MenuLabSession = Menu + "Life 4 0 - Play its lab session (Day 5, I serve)";
    const string MenuLab = Menu + "Life 4 - Stools, settled guests, looks (Play Mode, Life 4's lab session)";
    const string Tag = "[NPC wave 2] ";

    // The settled guests (spec §2.2; the Producer's calls 13 and T21): seats by name, leave at a share of the day.
    static readonly (string label, string[] seats, string[] alternative, bool laptop, float leaveAt)[] Guests =
    {
        ("a reader", new[] { "Seat (reading corner, north tub chair)" }, new[] { "Seat (reading corner, south tub chair)" }, false, .30f),
        ("a pair", new[] { "Seat (banquette 2-top, south, chair)", "Seat (banquette 2-top, south, on the banquette)" },
                   new[] { "Seat (banquette 2-top, middle, chair)", "Seat (banquette 2-top, middle, on the banquette)" }, false, .40f),
        ("a laptop worker", new[] { "Seat (communal table, east 1)" }, Array.Empty<string>(), true, .55f),
    };
    const int GuestsFirstDay = 1, GuestsLastDay = 3;
    const float SpawnJitterWas = .35f, SpawnJitter = .6f;

    // The NPC lead's proposal for the names' sexes (spec §3.2), his sister's to correct. 0 Any, 1 Woman, 2 Man.
    static readonly Dictionary<string, PolygonNpcVisual.NameSex> NameSexes = new Dictionary<string, PolygonNpcVisual.NameSex>(StringComparer.OrdinalIgnoreCase)
    {
        { "Valerie", PolygonNpcVisual.NameSex.Woman }, { "Priya", PolygonNpcVisual.NameSex.Woman }, { "Ingrid", PolygonNpcVisual.NameSex.Woman },
        { "Alishba", PolygonNpcVisual.NameSex.Woman }, { "Nina", PolygonNpcVisual.NameSex.Woman }, { "Beatriz", PolygonNpcVisual.NameSex.Woman },
        { "Grace", PolygonNpcVisual.NameSex.Woman },
        { "Deshawn", PolygonNpcVisual.NameSex.Man }, { "Tomas", PolygonNpcVisual.NameSex.Man }, { "Mansoor", PolygonNpcVisual.NameSex.Man },
        { "Ali", PolygonNpcVisual.NameSex.Man }, { "Walter", PolygonNpcVisual.NameSex.Man }, { "Omar", PolygonNpcVisual.NameSex.Man },
        { "Malcom", PolygonNpcVisual.NameSex.Man },
        { "Yuki", PolygonNpcVisual.NameSex.Any }, { "Saamin", PolygonNpcVisual.NameSex.Any },
    };

    const string AltsFolder = "Assets/Synty/PolygonCity/Materials/Alts";
    static readonly string[] OwnerPrefabs = { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" };

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NpcWave2"));
    static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
    static string F(float v, string format = "0.000") => v.ToString(format, CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

    [MenuItem(MenuStools, true)]
    [MenuItem(MenuStoolsUndo, true)]
    [MenuItem(MenuGuests, true)]
    [MenuItem(MenuGuestsUndo, true)]
    [MenuItem(MenuNames, true)]
    [MenuItem(MenuNamesUndo, true)]
    [MenuItem(MenuPaletteSurvey, true)]
    [MenuItem(MenuPaletteAllow, true)]
    [MenuItem(MenuPaletteUndo, true)]
    [MenuItem(MenuLabSession, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the scene steps' plumbing

    sealed class Run
    {
        public readonly string title;
        public readonly StringBuilder log = new StringBuilder();
        public Scene scene;
        public Run(string title) { this.title = title; }
        public void Line(string text) => log.AppendLine(text);
    }

    // Runs a scene step: refuses outside the café scene or in Play mode; one Undo group; saves; records.
    static void SceneStep(string title, string file, Func<Run, bool> change)
    {
        var run = new Run(title);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first: this step changes the scene.");
            run.scene = SceneManager.GetActiveScene();
            if (run.scene.path != Playtest4Shared.ScenePath)
                throw new InvalidOperationException($"Open the café scene ({Playtest4Shared.ScenePath}) first; the open scene is '{run.scene.path}'.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            int group = Undo.GetCurrentGroup();
            bool changed = change(run);
            Undo.CollapseUndoOperations(group);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(run.scene);
                bool saved = EditorSceneManager.SaveScene(run.scene);
                run.Line(saved ? "Saved the scene." : "NOTE: the scene could not be saved: save it by hand (Ctrl+S).");
                run.Line("Undo: Edit > Undo (this session), or its own Undo menu later.");
            }
            Finish(run, file, true);
        }
        catch (Exception e)
        {
            run.Line("REFUSED: " + e.Message);
            Finish(run, file, false);
        }
    }

    static void Finish(Run run, string file, bool ok)
    {
        string text = $"{run.title}\n{DateTime.Now:yyyy-MM-dd HH:mm}\n\n{run.log}";
        try
        {
            Directory.CreateDirectory(LogRoot);
            File.WriteAllText(Path.Combine(LogRoot, file + "-" + Stamp() + ".txt"), text);
            File.WriteAllText(Path.Combine(LogRoot, file + "-last.txt"), text);
        }
        catch (Exception e) { text += "\n(the report could not be written: " + e.Message + ")"; }
        if (ok) Debug.Log(Tag + text);
        else Debug.LogError(Tag + text);
    }

    static TableSeat SeatNamed(string name) =>
        Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(s => s != null && s.name == name);

    // ================================================================== Wave 2 1: the repair-bar stools sit

    [MenuItem(MenuStools)]
    static void StoolsMenu() => SceneStep("Wave 2 1 - The repair-bar stools sit", "stools", Stools);

    static bool Stools(Run run)
    {
        var found = new List<(TableSeat seat, float turn)>();
        foreach ((string label, float turn) in RepairBarSeats.Turns)
        {
            TableSeat seat = SeatNamed(RepairBarSeats.SeatName(label));
            if (seat == null) throw new InvalidOperationException($"'{RepairBarSeats.SeatName(label)}' isn't in the scene (Seats 1 lays it): nothing was changed.");
            found.Add((seat, turn));
        }
        if (found.All(f => StoolDone(f.seat, f.turn)))
        {
            run.Line("Already done: the three repair-bar stools sit (Repair Bar, Snap To Seat, Max Lift 0.32, Approach Side 0.50, Face Towards at 45 degrees). Nothing changed.");
            return false;
        }
        foreach ((TableSeat seat, float turn) in found)
        {
            var so = new SerializedObject(seat);
            string before = $"Repair Bar {so.FindProperty("repairBar").boolValue}, Snap To Seat {so.FindProperty("snapToSeat").boolValue}, " +
                            $"Max Lift {F(so.FindProperty("maxLift").floatValue, "0.00")}, Approach Side {F(so.FindProperty("approachSide").floatValue, "0.00")}, " +
                            $"Face Towards {(so.FindProperty("faceTowards").objectReferenceValue != null ? "set" : "empty")}";
            Transform face = FaceAnchor(seat, turn);
            so.Update();
            so.FindProperty("repairBar").boolValue = true;
            so.FindProperty("snapToSeat").boolValue = true;
            so.FindProperty("maxLift").floatValue = RepairBarSeats.MaxLift;
            so.FindProperty("approachSide").floatValue = RepairBarSeats.ApproachSide;
            so.FindProperty("faceTowards").objectReferenceValue = face;
            so.ApplyModifiedProperties();   // recorded in the step's Undo group
            run.Line($"{seat.name}: {before}  ->  Repair Bar on, Snap To Seat on, Max Lift {F(RepairBarSeats.MaxLift, "0.00")}, " +
                     $"Approach Side {F(RepairBarSeats.ApproachSide, "0.00")}, Face Towards {V(face.position)} ({(turn < 0f ? "west" : "east")} {F(Mathf.Abs(turn), "0")} degrees).");
        }
        run.Line("Clearance footprints unchanged (0.30 each: the three can be taken together). No stand point moved (the occupied-circulation guard is unaffected).");
        run.Line("Next: Fixit Fidget > Checks > Waiting space reservations (the bar's order and the drink claim), then Life 4 in Play mode.");
        return true;
    }

    static bool StoolDone(TableSeat seat, float turn)
    {
        Transform face = seat.transform.Find(RepairBarSeats.FaceName);
        Vector3 want = RepairBarSeats.FacePoint(seat.SeatPose.position, turn);
        var so = new SerializedObject(seat);
        return seat.RepairBar && seat.SnapToSeat && Mathf.Abs(seat.MaxLift - RepairBarSeats.MaxLift) < 1e-4f
               && Mathf.Abs(seat.ApproachSide - RepairBarSeats.ApproachSide) < 1e-4f && face != null
               && so.FindProperty("faceTowards").objectReferenceValue == face && Vector3.Distance(face.position, want) < .01f;
    }

    // The seat's Face Towards point: a child named FaceTowards, made (or moved) 1 m out at the stool's turn, at 1 m up.
    static Transform FaceAnchor(TableSeat seat, float turn)
    {
        Vector3 at = RepairBarSeats.FacePoint(seat.SeatPose.position, turn);
        Transform face = seat.transform.Find(RepairBarSeats.FaceName);
        if (face == null)
        {
            var go = new GameObject(RepairBarSeats.FaceName);
            Undo.RegisterCreatedObjectUndo(go, "Wave 2 1 - The repair-bar stools sit");
            Undo.SetTransformParent(go.transform, seat.transform, "Wave 2 1 - The repair-bar stools sit");
            face = go.transform;
        }
        else Undo.RecordObject(face, "Wave 2 1 - The repair-bar stools sit");
        face.SetPositionAndRotation(at, Quaternion.identity);
        return face;
    }

    [MenuItem(MenuStoolsUndo)]
    static void StoolsUndoMenu() => SceneStep("Wave 2 1 - Undo: the repair-bar stools stand again", "stools-undo", StoolsUndo);

    static bool StoolsUndo(Run run)
    {
        bool any = false;
        foreach (var stool in RepairBarSeats.Turns)
        {
            string label = stool.label;
            TableSeat seat = SeatNamed(RepairBarSeats.SeatName(label));
            if (seat == null) { run.Line($"'{RepairBarSeats.SeatName(label)}' isn't in the scene."); continue; }
            var so = new SerializedObject(seat);
            Transform face = seat.transform.Find(RepairBarSeats.FaceName);
            if (!seat.RepairBar && !seat.SnapToSeat && seat.MaxLift == 0f && seat.ApproachSide == 0f && face == null) continue;
            so.FindProperty("repairBar").boolValue = false;
            so.FindProperty("snapToSeat").boolValue = false;
            so.FindProperty("maxLift").floatValue = 0f;
            so.FindProperty("approachSide").floatValue = 0f;
            so.FindProperty("faceTowards").objectReferenceValue = null;
            so.ApplyModifiedProperties();
            if (face != null) Undo.DestroyObjectImmediate(face.gameObject);
            run.Line($"{seat.name}: back as Seats 1 laid it (Snap To Seat off: customers wait standing beside it; no Repair Bar, Max Lift, Approach Side or Face Towards).");
            any = true;
        }
        if (!any) run.Line("Already undone: nothing changed.");
        return any;
    }

    // ================================================================== Wave 2 2: settled guests and the patrons' rhythm

    [MenuItem(MenuGuests)]
    static void GuestsMenu() => SceneStep("Wave 2 2 - Settled guests and the patrons' rhythm", "guests", SettleGuests);

    static bool SettleGuests(Run run)
    {
        PatronSpawner spawner = Object.FindAnyObjectByType<PatronSpawner>(FindObjectsInactive.Include);
        if (spawner == null) throw new InvalidOperationException("The scene has no PatronSpawner: nothing was changed.");
        var resolved = new List<(string label, TableSeat[] seats, TableSeat[] alternative, bool laptop, float leaveAt)>();
        foreach (var g in Guests)
        {
            TableSeat[] seats = g.seats.Select(SeatNamed).ToArray();
            TableSeat[] alternative = g.alternative.Select(SeatNamed).ToArray();
            string missing = string.Join(", ", g.seats.Concat(g.alternative).Where(n => SeatNamed(n) == null));
            if (missing.Length > 0) throw new InvalidOperationException($"{g.label}: no seat named {missing} in the scene (Seats 1 and Lived in 2 lay them): nothing was changed.");
            foreach (TableSeat s in seats.Concat(alternative))
            {
                if (s.RepairBar) throw new InvalidOperationException($"{g.label}: {s.name} is a repair-bar stool: settled guests never take one. Nothing was changed.");
                if (!s.SnapToSeat) throw new InvalidOperationException($"{g.label}: {s.name} has Snap To Seat off: a settled guest must sit. Nothing was changed.");
            }
            resolved.Add((g.label, seats, alternative, g.laptop, g.leaveAt));
        }
        if (resolved.First(r => r.laptop).seats.Any(s => s.transform.Cast<Transform>().All(c => !c.name.StartsWith("LI_Laptop_Open", StringComparison.Ordinal))))
            run.Line("NOTE: no LI_Laptop_Open under the laptop worker's seat (Lived in 2 places it): they sit as an ordinary guest until it's there.");

        var so = new SerializedObject(spawner);
        SerializedProperty list = so.FindProperty("openingGuests");
        SerializedProperty jitter = so.FindProperty("spawnJitter");
        if (list == null || jitter == null) throw new InvalidOperationException("PatronSpawner has no Opening Guests or Spawn Jitter (the wave 2 code hasn't compiled?): nothing was changed.");
        if (GuestsMatch(list, resolved) && Mathf.Abs(jitter.floatValue - SpawnJitter) < 1e-4f)
        {
            run.Line("Already done: three settled guests (Days 1-3) and Spawn Jitter 0.6. Nothing changed.");
            return false;
        }
        int before = list.arraySize;
        float jitterBefore = jitter.floatValue;
        list.arraySize = resolved.Count;
        for (int i = 0; i < resolved.Count; i++)
        {
            var g = resolved[i];
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("label").stringValue = g.label;
            SetSeats(e.FindPropertyRelative("seats"), g.seats);
            SetSeats(e.FindPropertyRelative("alternative"), g.alternative);
            e.FindPropertyRelative("laptop").boolValue = g.laptop;
            e.FindPropertyRelative("leaveAt").floatValue = g.leaveAt;
            e.FindPropertyRelative("firstDay").intValue = GuestsFirstDay;
            e.FindPropertyRelative("lastDay").intValue = GuestsLastDay;
            run.Line($"Settled guest {i + 1}, {g.label}: {string.Join(" + ", g.seats.Select(s => s.name))}" +
                     (g.alternative.Length > 0 ? $" (or {string.Join(" + ", g.alternative.Select(s => s.name))})" : "") +
                     $"{(g.laptop ? ", at the laptop" : "")}; Days {GuestsFirstDay}-{GuestsLastDay}; leaves at {F(g.leaveAt, "0.00")} of the day (at 420 s: {F(g.leaveAt * 420f, "0")} s).");
        }
        jitter.floatValue = SpawnJitter;
        so.ApplyModifiedProperties();
        run.Line($"Opening Guests: {before} -> {resolved.Count} entries (4 people). Spawn Jitter: {F(jitterBefore, "0.00")} -> {F(SpawnJitter, "0.00")} (gaps of 4.8-19.2 s instead of 7.8-16.2 s at 12 s).");
        run.Line("Their seats: the room's south half, never a stool, none among the eight a customer weighs from the counter. The valve sees 26 - 4 = 22 free seats at the open.");
        return true;
    }

    static void SetSeats(SerializedProperty array, TableSeat[] seats)
    {
        array.arraySize = seats.Length;
        for (int i = 0; i < seats.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = seats[i];
    }

    static bool GuestsMatch(SerializedProperty list, List<(string label, TableSeat[] seats, TableSeat[] alternative, bool laptop, float leaveAt)> want)
    {
        if (list.arraySize != want.Count) return false;
        for (int i = 0; i < want.Count; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("label").stringValue != want[i].label || e.FindPropertyRelative("laptop").boolValue != want[i].laptop) return false;
            if (Mathf.Abs(e.FindPropertyRelative("leaveAt").floatValue - want[i].leaveAt) > 1e-4f) return false;
            if (e.FindPropertyRelative("firstDay").intValue != GuestsFirstDay || e.FindPropertyRelative("lastDay").intValue != GuestsLastDay) return false;
            if (!SameSeats(e.FindPropertyRelative("seats"), want[i].seats) || !SameSeats(e.FindPropertyRelative("alternative"), want[i].alternative)) return false;
        }
        return true;
    }

    static bool SameSeats(SerializedProperty array, TableSeat[] seats)
    {
        if (array.arraySize != seats.Length) return false;
        for (int i = 0; i < seats.Length; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue != seats[i]) return false;
        return true;
    }

    [MenuItem(MenuGuestsUndo)]
    static void GuestsUndoMenu() => SceneStep("Wave 2 2 - Undo: no settled guests, Spawn Jitter 0.35", "guests-undo", GuestsUndo);

    static bool GuestsUndo(Run run)
    {
        PatronSpawner spawner = Object.FindAnyObjectByType<PatronSpawner>(FindObjectsInactive.Include);
        if (spawner == null) throw new InvalidOperationException("The scene has no PatronSpawner.");
        var so = new SerializedObject(spawner);
        SerializedProperty list = so.FindProperty("openingGuests"), jitter = so.FindProperty("spawnJitter");
        if (list.arraySize == 0 && Mathf.Abs(jitter.floatValue - SpawnJitterWas) < 1e-4f) { run.Line("Already undone: nothing changed."); return false; }
        run.Line($"Opening Guests {list.arraySize} -> 0; Spawn Jitter {F(jitter.floatValue, "0.00")} -> {F(SpawnJitterWas, "0.00")}.");
        list.arraySize = 0;
        jitter.floatValue = SpawnJitterWas;
        so.ApplyModifiedProperties();
        return true;
    }

    // ================================================================== Wave 2 3: the names' sexes

    [MenuItem(MenuNames)]
    static void NamesMenu() => SceneStep("Wave 2 3 - The names' sexes", "names", Names);

    static bool Names(Run run)
    {
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        if (spawner == null) throw new InvalidOperationException("The scene has no CustomerSpawner: nothing was changed.");
        var so = new SerializedObject(spawner);
        SerializedProperty names = so.FindProperty("firstNames"), sexes = so.FindProperty("firstNameSexes");
        if (names == null) throw new InvalidOperationException("CustomerSpawner has no First Names: nothing was changed.");
        if (sexes == null)
            throw new InvalidOperationException("CustomerSpawner has no First Name Sexes yet: apply the CustomerSpawner patch in the NPC builder's report " +
                                                "(Patches for other owners), let it compile, then run this again. Nothing was changed.");
        var want = new int[names.arraySize];
        var lines = new List<string>();
        for (int i = 0; i < names.arraySize; i++)
        {
            string name = names.GetArrayElementAtIndex(i).stringValue;
            PolygonNpcVisual.NameSex sex = NameSexes.TryGetValue(name ?? "", out PolygonNpcVisual.NameSex s) ? s : PolygonNpcVisual.NameSex.Any;
            want[i] = (int)sex;
            lines.Add($"{name}: {sex}{(NameSexes.ContainsKey(name ?? "") ? "" : " (not in the proposal: Any)")}");
        }
        bool same = sexes.arraySize == want.Length && Enumerable.Range(0, want.Length).All(i => sexes.GetArrayElementAtIndex(i).intValue == want[i]);
        if (same) { run.Line("Already done: " + string.Join("; ", lines) + ". Nothing changed."); return false; }
        sexes.arraySize = want.Length;
        for (int i = 0; i < want.Length; i++) sexes.GetArrayElementAtIndex(i).intValue = want[i];
        so.ApplyModifiedProperties();
        run.Line($"First Name Sexes, beside the {want.Length} First Names (the NPC lead's proposal, his sister's to correct in the Inspector):");
        foreach (string line in lines) run.Line("  " + line);
        run.Line("Named walk-ins are now dealt their name's looks; a woman's name never keeps a CC0 body (they are men's).");
        return true;
    }

    [MenuItem(MenuNamesUndo)]
    static void NamesUndoMenu() => SceneStep("Wave 2 3 - Undo: the names have no sexes", "names-undo", NamesUndo);

    static bool NamesUndo(Run run)
    {
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        SerializedProperty sexes = spawner != null ? new SerializedObject(spawner).FindProperty("firstNameSexes") : null;
        if (sexes == null || sexes.arraySize == 0) { run.Line("Already undone (or the patch isn't in): nothing changed."); return false; }
        SerializedObject so = sexes.serializedObject;
        run.Line($"First Name Sexes {sexes.arraySize} -> 0 (every name: Any).");
        sexes.arraySize = 0;
        so.ApplyModifiedProperties();
        return true;
    }

    // ================================================================== Wave 2 6a/6b: palette swaps (only if the looks use an atlas with alternates)

    [MenuItem(MenuPaletteSurvey)]
    static void PaletteSurveyMenu()
    {
        var run = new Run("Wave 2 6a - Palette survey and line-up");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Survey(run, out string atlas, out List<Material> alternates, out GameObject sample);
            if (atlas != null && sample != null) LineUp(run, sample, atlas, alternates);
            Finish(run, "palette-survey", true);
        }
        catch (Exception e) { run.Line("REFUSED: " + e.Message); Finish(run, "palette-survey", false); }
    }

    // Which looks' bodies are drawn with a POLYGON City atlas that has alternates (by material name), and the alternates.
    static void Survey(Run run, out string atlas, out List<Material> alternates, out GameObject sample)
    {
        atlas = null;
        sample = null;
        alternates = new List<Material>();
        if (!AssetDatabase.IsValidFolder(AltsFolder)) { run.Line($"{AltsFolder} isn't in this project (the Synty pack is copied by hand): no palette swaps."); return; }
        List<Material> alts = AssetDatabase.FindAssets("t:Material", new[] { AltsFolder }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m != null).OrderBy(m => m.name, StringComparer.Ordinal).ToList();
        run.Line($"Alternates in {AltsFolder}: {alts.Count} ({string.Join(", ", alts.Select(m => m.name))}).");
        var usedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string look in PolygonNpcSetup.WalkInLooks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(look));
            if (prefab == null) { run.Line($"  {look}: not built (NPC looks 1)."); continue; }
            string[] materials = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).Select(m => m.name).Distinct().ToArray();
            string[] atlases = materials.Where(n => alts.Any(a => a.name == n)).ToArray();
            run.Line($"  {look}: {string.Join(", ", materials)}{(atlases.Length > 0 ? "  <- an atlas with alternates: " + string.Join(", ", atlases) : "")}");
            foreach (string a in atlases)
            {
                if (!usedBy.TryGetValue(a, out List<string> who)) usedBy[a] = who = new List<string>();
                who.Add(look);
                if (sample == null) sample = prefab;
            }
        }
        if (usedBy.Count == 0)
        {
            run.Line("No look's body is drawn with an atlas that has alternates: palette swaps don't apply (spec §3.2 item 3). Nothing to allow.");
            return;
        }
        KeyValuePair<string, List<string>> best = usedBy.OrderByDescending(kv => kv.Value.Count).First();
        atlas = best.Key;
        string baseName = atlas;
        alternates = alts.Where(m => m.name != baseName).ToList();
        sample = AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(best.Value[0]));
        run.Line($"The atlas: {atlas}, on {best.Value.Count} look(s) ({string.Join(", ", best.Value)}); its alternates: {alternates.Count}.");
        run.Line("Gate (spec §3.2): look at the line-up; any alternate that turns skin or hair an unnatural colour is struck off " +
                 "(remove it from Palette Alternates on the two prefabs after Wave 2 6b).");
    }

    // One body per alternate (and the look's own colouring first), photographed from the front, side by side.
    static void LineUp(Run run, GameObject sample, string atlas, List<Material> alternates)
    {
        const int cell = 220;
        int count = alternates.Count + 1;
        var sheet = new Texture2D(cell * count, cell * 2, TextureFormat.RGB24, false);
        var preview = new PreviewRenderUtility();
        try
        {
            preview.camera.fieldOfView = 26f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.80f, .82f, .85f);
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            preview.lights[1].intensity = .55f;
            preview.ambientColor = new Color(.42f, .42f, .46f);
            for (int i = 0; i < count; i++)
            {
                GameObject body = preview.InstantiatePrefabInScene(sample);
                try
                {
                    body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                    if (i > 0)
                        foreach (SkinnedMeshRenderer r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            Material[] mats = r.sharedMaterials;
                            for (int m = 0; m < mats.Length; m++) if (mats[m] != null && mats[m].name == atlas) mats[m] = alternates[i - 1];
                            r.sharedMaterials = mats;
                        }
                    Bounds b = new Bounds(new Vector3(0f, .9f, 0f), new Vector3(.6f, 1.8f, .4f));
                    foreach (Renderer r in body.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    for (int half = 0; half < 2; half++)
                    {
                        // Top row: the whole body; bottom row: the head and shoulders (skin and hair are what the gate reads).
                        Vector3 aim = half == 0 ? b.center : new Vector3(b.center.x, b.max.y - .25f, b.center.z);
                        float reach = half == 0 ? b.extents.magnitude * 2.4f : .9f;
                        preview.camera.transform.position = aim + new Vector3(0f, .1f, -reach);
                        preview.camera.transform.LookAt(aim);
                        preview.camera.nearClipPlane = .05f;
                        preview.camera.farClipPlane = reach * 3f;
                        preview.BeginStaticPreview(new Rect(0, 0, cell, cell));
                        preview.Render(true);
                        Texture shot = preview.EndStaticPreview();
                        var copy = shot as Texture2D;
                        if (copy != null) sheet.SetPixels(i * cell, (1 - half) * cell, cell, cell, copy.GetPixels());
                        Object.DestroyImmediate(shot);
                    }
                }
                finally { Object.DestroyImmediate(body); }
            }
            sheet.Apply();
            Directory.CreateDirectory(LogRoot);
            string path = Path.Combine(LogRoot, "palette-lineup-" + Stamp() + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            run.Line($"Line-up: {path} (left to right: {atlas} as drawn, then {string.Join(", ", alternates.Select(m => m.name))}).");
        }
        finally
        {
            preview.Cleanup();
            Object.DestroyImmediate(sheet);
        }
    }

    [MenuItem(MenuPaletteAllow)]
    static void PaletteAllowMenu()
    {
        var run = new Run("Wave 2 6b - Allow the palette alternates");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Survey(run, out string atlas, out List<Material> alternates, out _);
            if (atlas == null || alternates.Count == 0) { run.Line("Nothing allowed: the survey found no atlas with alternates on the looks. Nothing changed."); Finish(run, "palette-allow", true); return; }
            WritePalettes(run, atlas, alternates.ToArray());
            Finish(run, "palette-allow", true);
        }
        catch (Exception e) { run.Line("REFUSED: " + e.Message); Finish(run, "palette-allow", false); }
    }

    [MenuItem(MenuPaletteUndo)]
    static void PaletteUndoMenu()
    {
        var run = new Run("Wave 2 6b - Undo: no palette swaps");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            WritePalettes(run, "", Array.Empty<Material>());
            Finish(run, "palette-undo", true);
        }
        catch (Exception e) { run.Line("REFUSED: " + e.Message); Finish(run, "palette-undo", false); }
    }

    // Palette Base and Palette Alternates on the Customer and Patron prefabs' PolygonNpcVisual (as NPC looks 3 edits them).
    static void WritePalettes(Run run, string atlas, Material[] alternates)
    {
        foreach (string path in OwnerPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PolygonNpcVisual visual = root.GetComponent<PolygonNpcVisual>();
                if (visual == null) { run.Line($"{path}: no PolygonNpcVisual (NPC looks 3 adds it): skipped."); continue; }
                var so = new SerializedObject(visual);
                SerializedProperty baseName = so.FindProperty("paletteBase"), list = so.FindProperty("paletteAlternates");
                bool same = baseName.stringValue == atlas && list.arraySize == alternates.Length
                            && Enumerable.Range(0, alternates.Length).All(i => list.GetArrayElementAtIndex(i).objectReferenceValue == alternates[i]);
                if (same) { run.Line($"{Path.GetFileNameWithoutExtension(path)}: already {(alternates.Length == 0 ? "without palette swaps" : "allowed")}; nothing changed."); continue; }
                baseName.stringValue = atlas;
                list.arraySize = alternates.Length;
                for (int i = 0; i < alternates.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = alternates[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                run.Line($"{Path.GetFileNameWithoutExtension(path)}: Palette Base '{atlas}', {alternates.Length} alternate(s){(alternates.Length > 0 ? ": " + string.Join(", ", alternates.Select(m => m.name)) : "")}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        run.Line(alternates.Length > 0 ? "Undo: Fixit Fidget > NPC > Wave 2 6b - Undo (prefabs)." : "");
    }

    // ================================================================== Life 4: the lab (Play mode, a lab session)
    //
    // A. The stools: four bench jobs one after another (customers sent in and heard at the counter; others turned away):
    //    the first three take the middle, east and west stools and sit (hips within 5 cm, the root on the floor until Sit
    //    Down, the hips never below where they settle, the head never below its seated height by more than 3 cm: the clip's
    //    own lean is 1.9; 2 cm until 15 Oct), Ace behind the bench reaches each over the counter,
    //    and they stand and walk out with no stall over 3 s; the fourth, with the three taken, gets a floor seat. Photos:
    //    from above, from the bench, and the middle stool's sit as a strip 0.2 s apart. (The hand-back over the counter is
    //    the Stations lab's, which already serves a stool customer: it must stay 95/95 and 94/94.)
    // B. The settled guests (PatronSpawner.ForceOpeningGuests): four seated in the frame they are placed, at their listed
    //    seats, nothing paid; the laptop on; the pair companions who chat within 30 s; their Sitting phases apart by more than
    //    0.1; each leaves within 0.03 of its share of the day (the clock is moved to just before each), the pair within 1.4 s
    //    of each other. An overhead photo 2 s after they're placed (Art's "café full from above" pass can use it).
    // C. The looks: 20 patrons and 6 walk-ins, each one's look (and palette) reported; no look on more than 2 of the 20, at
    //    most 1 repeat among the first 10 (present together), every named walk-in's body their name's sex.
    // Timings are generous guesses until the Mac runs it (13 Oct lesson); everything is measured on the frames he would see.

    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo BrainSpot = typeof(CustomerBrain).GetField("waitingSpot", Hidden);
    static readonly FieldInfo BrainPatience = typeof(CustomerBrain).GetField("patienceLeft", Hidden);
    static readonly FieldInfo BrainServiceMax = typeof(CustomerBrain).GetField("serviceMax", Hidden);
    static readonly FieldInfo DayLength = typeof(DayClock).GetField("dayLengthSeconds", Hidden);
    static readonly FieldInfo GuestsDay = typeof(PatronSpawner).GetField("openingGuestsDay", Hidden);
    static readonly PropertyInfo Remaining = typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining));

    static readonly Stack<IEnumerator> routine = new Stack<IEnumerator>();
    static readonly StringBuilder report = new StringBuilder();
    static int checks, failures, photos;
    static string folder;
    static float started;
    static bool autopilotBefore, customersOn, patronsOn;
    static CustomerSpawner customerSpawner;
    static PatronSpawner patronSpawner;

    [MenuItem(MenuLabSession)]
    static void LabSessionMenu()
    {
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath) { Debug.LogError(Tag + $"Open the café scene ({Playtest4Shared.ScenePath}) first."); return; }
        // Day 5: three in four of its walk-ins bring a repair (Days 1-3 bring at most three, by design), and the stools need
        // four bench jobs in a row. The settled guests (Days 1-3) are let in for the check (B widens their days in Play mode).
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.DeleteKey(CafeLab.HumanSpeedKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Life 4's lab session: Day 5, the autopilot off, lab save {path}; your playtest save is not used. " +
                  "When the day is open (after the bell), run Fixit Fidget > NPC > Life 4.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(MenuLab)]
    static void LabMenu()
    {
        report.Clear();
        checks = failures = photos = 0;
        folder = Path.Combine(LogRoot, "life4-" + Stamp());
        Directory.CreateDirectory(folder);
        started = Time.realtimeSinceStartup;
        routine.Clear();
        routine.Push(Life4());
        EditorApplication.update += Step;
        Debug.Log(Tag + "Life 4 is running in the lab session: hands off until it reports. " + folder);
    }

    [MenuItem(MenuLab, true)]
    static bool CanRunLab() => EditorApplication.isPlaying && routine.Count == 0;

    static void Step()
    {
        bool running = EditorApplication.isPlaying;
        try
        {
            while (running)
            {
                if (routine.Count == 0) { running = false; break; }
                IEnumerator top = routine.Peek();
                if (!top.MoveNext()) { routine.Pop(); continue; }
                if (top.Current is IEnumerator nested) { routine.Push(nested); continue; }
                break;
            }
        }
        catch (Exception e)
        {
            Check(false, "the lab ran to its end (it stopped: " + e.Message + ")");
            Debug.LogException(e);
            running = false;
        }
        if (running) return;
        EditorApplication.update -= Step;
        routine.Clear();
        Cleanup();
        string verdict = (failures == 0 ? "PASS" : "FAIL") + $" {checks - failures}/{checks}";
        string text = $"Life 4 - Stools, settled guests, looks: {verdict}\n{DateTime.Now:yyyy-MM-dd HH:mm}, {Time.realtimeSinceStartup - started:0} s, {photos} photos\n\n{report}";
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { text += "\n(the report could not be written: " + e.Message + ")"; }
        if (failures == 0) Debug.Log(Tag + text);
        else Debug.LogError(Tag + text);
    }

    static void Cleanup()
    {
        PatronSpawner.ForceOpeningGuests = false;
        if (!EditorApplication.isPlaying) return;
        if (CafeLabDirector.Instance != null) CafeLabDirector.Instance.Autopilot = autopilotBefore;
        if (customerSpawner != null) customerSpawner.enabled = customersOn;
        if (patronSpawner != null) patronSpawner.enabled = patronsOn;
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    static void Note(string what) => report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");

    static IEnumerator Wait(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target) yield return null;
    }

    static IEnumerator Life4()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) { Check(false, "the open scene has a DayClock"); yield break; }
        if (clock.DayOver) { Check(false, "a day is open (press Open Tomorrow on the recap, then run Life 4 again)"); yield break; }
        if (!CafeLab.Active || CafeLabDirector.Instance == null) { Check(false, "this is a lab session (Fixit Fidget > NPC > Life 4 0)"); yield break; }
        CafeLabDirector lab = CafeLabDirector.Instance;
        autopilotBefore = lab.Autopilot;
        lab.Autopilot = false;
        customerSpawner = Object.FindAnyObjectByType<CustomerSpawner>();
        patronSpawner = Object.FindAnyObjectByType<PatronSpawner>();
        if (customerSpawner == null || patronSpawner == null) { Check(false, "the café has its two spawners"); yield break; }
        // Only the people the lab sends: the spawners' own arrivals are held while it runs (put back after).
        customersOn = customerSpawner.enabled;
        patronsOn = patronSpawner.enabled;
        customerSpawner.enabled = false;
        patronSpawner.enabled = false;
        TopUpTheDay(clock, .1f);
        Note($"Day {clock.Day}, {F(clock.NormalizedDay, "0.00")} of the way through; the spawners held, the autopilot off.");

        yield return ClearTheRoom(lab);
        yield return StoolsLab(lab);
        yield return ClearTheRoom(lab);
        yield return SettledGuests(clock);
        yield return ClearTheRoom(lab);
        yield return Looks(lab);
    }

    // The clock back to `share` of the day, so the lab's minutes never run the day out.
    static void TopUpTheDay(DayClock clock, float share)
    {
        float length = DayLength != null ? (float)DayLength.GetValue(clock) : 420f;
        Remaining?.SetValue(clock, length * (1f - Mathf.Clamp01(share)));
    }

    // Everyone out (served and happy, or patrons getting up), and gone through the door; stragglers removed.
    static IEnumerator ClearTheRoom(CafeLabDirector lab)
    {
        lab.CustomersLeaveNow();
        lab.PatronsLeaveNow();
        float until = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < until && (Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Length > 0
                                                    || Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude).Length > 0))
            yield return null;
        int left = 0;
        foreach (CustomerBrain c in Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) { c.ForceRemove(); left++; }
        foreach (PatronBrain p in Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude)) { Object.Destroy(p.gameObject); left++; }
        if (left > 0) Note($"{left} still inside after 40 s were removed (a slow way out, not this check's subject).");
        yield return Frames(3);
    }

    // ---------------------------------------------------------------- A. the stools

    sealed class SitWatch
    {
        public CustomerBrain who;
        public NpcSeating seating;
        public TableSeat seat;
        public Transform head, thighL, thighR;
        public float floorY, rootAboveFloor = float.NegativeInfinity, headLowest = float.PositiveInfinity, hipLowest = float.PositiveInfinity;
        public float seatedHead = float.NaN, seatedHip = float.NaN, hipError = float.NaN;
        public bool sat;
    }

    static IEnumerator StoolsLab(CafeLabDirector lab)
    {
        Transform bench = BenchStand();
        List<TableSeat> stools = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s != null && s.RepairBar).ToList();
        Check(stools.Count == 3 && stools.All(s => s.SnapToSeat && Mathf.Abs(s.MaxLift - RepairBarSeats.MaxLift) < 1e-3f && s.FacingPoint != s.CupSpot.position),
            $"the three repair-bar stools are set up to sit ({stools.Count} flagged; Wave 2 1)");
        if (stools.Count != 3 || bench == null) { Check(bench != null, "the repair bench's stand point is found"); yield break; }
        string[] order = { RepairBarSeats.SeatName("middle"), RepairBarSeats.SeatName("east"), RepairBarSeats.SeatName("west") };
        var watches = new List<SitWatch>();
        for (int k = 0; k < 4; k++)
        {
            CustomerBrain who = null;
            yield return ABenchCustomer(lab, c => who = c, () => KeepPatient(watches));
            if (who == null) { Check(false, $"bench job {k + 1}: a customer with a bench repair reached the counter (sent several; gave up)"); yield break; }
            who.HearIntake();
            yield return Frames(2);
            if (!who.CanAcceptJob) { Check(false, $"bench job {k + 1}: {who.CustomerName}'s job can be taken (the shelf full?)"); yield break; }
            who.AcceptJob();
            // Their device off the shelf's books, so four bench jobs in a row never fill it (the device stays where it lies).
            if (who.ActiveJob != null) foreach (ItemSlotArea area in Object.FindObjectsByType<ItemSlotArea>(FindObjectsInactive.Exclude)) area.ReleaseSlot(who.ActiveJob);
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until && who != null && SpotOf(who) == null) yield return null;
            WaitingSpot spot = SpotOf(who);
            if (k < 3)
            {
                Check(spot != null && spot.name == order[k], $"bench job {k + 1} ({who.CustomerName}) takes the {order[k]} (got: {(spot != null ? spot.name : "nothing")})");
                if (spot is TableSeat stool && stool.RepairBar) watches.Add(Watch(who, stool));
                if (k == 0 && watches.Count == 1) yield return SitStrip(watches[0]);
                else yield return UntilSeated(watches.LastOrDefault());
            }
            else
                Check(spot is TableSeat floorSeat && !floorSeat.RepairBar,
                    $"a fourth bench job ({who.CustomerName}), the three stools taken, gets a floor seat (got: {(spot != null ? spot.name : "nothing")})");
        }
        foreach (SitWatch w in watches) Judge(w);
        yield return Wait(1.5f);
        Photo("A1-stools-from-above", new Vector3(-4.2f, 9.5f, 7.5f), new Vector3(-4.2f, .8f, 12.9f), 40f, true);
        Photo("A2-stools-from-the-bench", bench.position + Vector3.up * 1.62f, new Vector3(-4.2f, 1.0f, 12.9f), 55f, false);

        // Over the counter from behind the bench, Ace not moving: each seated stool customer is in his reach, and when
        // frustrated is the one E reaches first (reassure). The hand-back over the counter is the Stations lab's.
        // At his own floor height (the Stations lab's way): the stand point lies under the floor's top, and a body set down at
        // it fell through (15 Oct: Ace at y -4.9 m by the time E was asked, so the reach found no one over the counter).
        Vector3 standAt = bench.position;
        PlayerMovement mover = Object.FindAnyObjectByType<PlayerMovement>();
        if (mover != null) standAt.y = mover.transform.position.y;
        lab.MoveAce(standAt, bench.eulerAngles.y);
        yield return Wait(.8f);
        PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>();
        Vector3 aceAt = ace != null ? ace.transform.position : Vector3.zero;
        foreach (SitWatch w in watches)
        {
            if (w.who == null || ace == null) continue;
            Check(ace.OnStaffSide && ace.AcrossTheCounter(w.who.transform.position),
                $"behind the bench, {w.who.CustomerName} seated on the {w.seat.name} is across the counter within E's reach ({Flat(w.who.transform.position - ace.transform.position).magnitude:0.00} m flat)");
            float max = BrainServiceMax != null ? (float)BrainServiceMax.GetValue(w.who) : 0f;
            // A call on hold that's ringing or waits to be dialled comes first, by design: E is for the call, not a word.
            if (w.who.JobNeedsAttention)
            {
                Note($"{w.who.CustomerName}'s job is a call that needs Ace first: E doesn't reassure them by design (not counted)");
                continue;
            }
            BrainPatience?.SetValue(w.who, max * .25f);
            yield return Frames(3);
            CustomerInteractable target = ace.FrustratedAcrossTheBench();
            string whyNot = target != null && target.Brain == w.who ? "" : ace.WhyNotFrustratedAcrossTheBench(w.who);
            Check(target != null && target.Brain == w.who, $"frustrated on the stool, {w.who.CustomerName} is who E reaches first from behind the bench (reassure)" +
                (whyNot.Length > 0 ? $": no, {whyNot}" : ""));
            float before = w.who.PatienceFraction;
            w.who.Reassure();
            Check(w.who.PatienceFraction > before + .05f, $"{w.who.CustomerName} is reassured on the stool ({before:P0} -> {w.who.PatienceFraction:P0})");
            BrainPatience?.SetValue(w.who, max);
        }
        if (ace != null) Check(Flat(ace.transform.position - aceAt).magnitude < .005f, $"Ace's displacement while reaching over the counter: {Flat(ace.transform.position - aceAt).magnitude:0.00} m");
        Note("The hand-back over the counter to a stool customer is the Stations lab's (Lab A and B): it must stay 95/95 and 94/94 with them seated.");

        // They get up and go: no stall over 3 s from the moment they stand.
        lab.CustomersLeaveNow();
        var stalls = new Dictionary<SitWatch, float>();
        var lastMoved = new Dictionary<SitWatch, (Vector3 at, float when)>();
        var stood = new HashSet<SitWatch>();
        var away = new HashSet<SitWatch>();
        float end = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < end && away.Count < watches.Count)
        {
            float now = Time.realtimeSinceStartup;
            foreach (SitWatch w in watches)
            {
                if (away.Contains(w)) continue;
                if (w.who == null) { away.Add(w); continue; }
                Vector3 at = w.who.transform.position;
                if (Flat(at - w.seat.SeatPose.position).magnitude > 2f) { away.Add(w); continue; }
                if (w.seating.Current == NpcSeating.Phase.StandingUp || w.seating.Current == NpcSeating.Phase.Seated) { stood.Add(w); lastMoved[w] = (at, now); continue; }
                if (!lastMoved.TryGetValue(w, out var last)) { lastMoved[w] = (at, now); continue; }
                if (Flat(at - last.at).magnitude > .1f) lastMoved[w] = (at, now);
                else stalls[w] = Mathf.Max(stalls.TryGetValue(w, out float s) ? s : 0f, now - last.when);
            }
            yield return null;
        }
        foreach (SitWatch w in watches)
            Check(stood.Contains(w) && away.Contains(w) && (!stalls.TryGetValue(w, out float s) || s <= 3f),
                $"{(w.who != null ? w.who.CustomerName : "a stool customer")} stood up from the {w.seat.name} and walked off (longest stall {(stalls.TryGetValue(w, out float t) ? t : 0f):0.0} s; 3 allowed)");
    }

    // The stools' sitters at full patience while the lab waits for the next bench job (nobody works on their repairs here).
    static void KeepPatient(List<SitWatch> watches)
    {
        foreach (SitWatch w in watches)
        {
            if (w.who == null || BrainServiceMax == null || BrainPatience == null) continue;
            BrainPatience.SetValue(w.who, (float)BrainServiceMax.GetValue(w.who));
        }
    }

    static SitWatch Watch(CustomerBrain who, TableSeat stool)
    {
        Animator animator = who.GetComponentInChildren<Animator>();
        Transform rig = animator != null ? animator.transform : who.transform;
        float floor = stool.StandPoint.position.y;
        if (UnityEngine.AI.NavMesh.SamplePosition(stool.StandPoint.position, out UnityEngine.AI.NavMeshHit hit, .5f, UnityEngine.AI.NavMesh.AllAreas)) floor = hit.position.y;
        return new SitWatch
        {
            who = who, seating = who.GetComponent<NpcSeating>(), seat = stool, floorY = floor,
            head = Bone(rig, "Head"), thighL = Bone(rig, "UpperLeg.L"), thighR = Bone(rig, "UpperLeg.R"),
        };
    }

    // Until they're seated (25 s at most), watching the root before Sit Down and the head during it.
    static IEnumerator UntilSeated(SitWatch w)
    {
        if (w == null || w.seating == null) yield break;
        float until = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < until && w.who != null && w.seating.Current != NpcSeating.Phase.Seated)
        {
            Observe(w);
            yield return null;
        }
        yield return Wait(1.2f);
        if (w.who != null && w.seating.Current == NpcSeating.Phase.Seated) Measure(w);
    }

    // The middle stool's sit, photographed every 0.2 s of Sit Down (the strip the spec asks for), then as UntilSeated.
    static IEnumerator SitStrip(SitWatch w)
    {
        if (w == null || w.seating == null) yield break;
        float until = Time.realtimeSinceStartup + 25f;
        int shot = 0;
        float nextShot = 0f;
        while (Time.realtimeSinceStartup < until && w.who != null && w.seating.Current != NpcSeating.Phase.Seated)
        {
            Observe(w);
            if (w.seating.Current == NpcSeating.Phase.SittingDown && Time.realtimeSinceStartup >= nextShot && shot < 8)
            {
                nextShot = Time.realtimeSinceStartup + .2f;
                Vector3 hip = w.thighL != null && w.thighR != null ? (w.thighL.position + w.thighR.position) * .5f : w.who.transform.position + Vector3.up;
                Photo($"A3-sit-strip-{shot++:00}", hip + new Vector3(2.2f, .5f, -.6f), hip + Vector3.up * .1f, 40f, false);
            }
            yield return null;
        }
        yield return Wait(1.2f);
        if (w.who != null && w.seating.Current == NpcSeating.Phase.Seated) Measure(w);
    }

    static void Observe(SitWatch w)
    {
        if (w.seating.Current == NpcSeating.Phase.Approaching || w.seating.Current == NpcSeating.Phase.Standing)
            w.rootAboveFloor = Mathf.Max(w.rootAboveFloor, w.who.transform.position.y - w.floorY);
        if (w.seating.Current != NpcSeating.Phase.SittingDown) return;
        if (w.head != null) w.headLowest = Mathf.Min(w.headLowest, w.head.position.y);
        if (w.thighL != null && w.thighR != null) w.hipLowest = Mathf.Min(w.hipLowest, (w.thighL.position.y + w.thighR.position.y) * .5f);
    }

    static void Measure(SitWatch w)
    {
        w.sat = true;
        if (w.head != null) w.seatedHead = w.head.position.y;
        if (w.thighL == null || w.thighR == null) return;
        Vector3 hip = (w.thighL.position + w.thighR.position) * .5f;
        w.seatedHip = hip.y;
        var so = new SerializedObject(w.seating);
        float behind = so.FindProperty("hipBehindSeatCentre").floatValue, above = so.FindProperty("hipAboveSeat").floatValue;
        Vector3 forward = Flat(w.seat.FacingPoint - w.seat.SeatPose.position).normalized;
        Vector3 target = w.seat.SeatPose.position - forward * behind;
        target.y = w.seat.SeatPose.position.y + above * w.who.transform.lossyScale.y;
        w.hipError = Vector3.Distance(hip, target);
    }

    static void Judge(SitWatch w)
    {
        string who = w.who != null ? w.who.CustomerName : "a stool customer";
        Check(w.sat, $"{who} sits on the {w.seat.name}");
        if (!w.sat) return;
        Check(!float.IsNaN(w.hipError) && w.hipError <= .05f, $"{who}'s hips land on the stool where the clip expects (within 5 cm: {w.hipError * 100f:0.0} cm)");
        Check(w.rootAboveFloor <= .03f, $"{who}'s body stays on the floor until Sit Down (highest {w.rootAboveFloor * 100f:0.0} cm over it; the lift is in the clip)");
        // The sink-and-rise this watches for (15 Oct: the lift lagged the clip, the hips went 5 cm into the stool and came
        // back up, the head 8.5-9 cm under its seated height) is the hips': they never go below where they settle. The head's
        // bar allows the clip's own lean forward, 1.9 cm under its seated height at an NPC's 1.1 with any lift (Npc Sit Down's
        // curves), and the sitting idle's breath; it was 2 cm, a guess written before the clip was measured.
        if (!float.IsNaN(w.seatedHip) && !float.IsInfinity(w.hipLowest))
            Check(w.hipLowest >= w.seatedHip - .015f, $"{who}'s hips never sink below where they settle while sitting (lowest {w.hipLowest:0.000}, seated {w.seatedHip:0.000} m; 1.5 cm allowed)");
        if (!float.IsNaN(w.seatedHead) && !float.IsInfinity(w.headLowest))
            Check(w.headLowest >= w.seatedHead - .03f, $"{who}'s head never sinks below its seated height by more than the clip's lean while sitting (lowest {w.headLowest:0.000}, seated {w.seatedHead:0.000} m; 3 cm allowed, the clip's own is 1.9)");
    }

    // Customers sent in one at a time until one with a bench repair is at the counter; anyone else is turned away.
    // `meanwhile` runs every frame of the wait (the stools' sitters kept patient: 15 Oct, a 146 s wait for the fourth ran the
    // first one's patience out, and the fourth took the stool he'd left).
    static IEnumerator ABenchCustomer(CafeLabDirector lab, Action<CustomerBrain> found, Action meanwhile = null)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var before = new HashSet<CustomerBrain>(Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude));
            if (lab.SendCustomersNow(1) == 0) { meanwhile?.Invoke(); yield return Wait(1f); continue; }
            yield return Frames(2);
            CustomerBrain fresh = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).FirstOrDefault(c => !before.Contains(c));
            if (fresh == null) continue;
            float until = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < until && fresh != null && !fresh.CanHearIntake && !fresh.IsLeaving) { meanwhile?.Invoke(); yield return null; }
            if (fresh == null || !fresh.CanHearIntake) continue;
            if (fresh.GoesToTheBench) { found(fresh); yield break; }
            fresh.HearIntake();
            yield return Frames(2);
            fresh.RefuseJob();
            Note($"turned away {fresh.CustomerName} (not a bench repair) to wait for one");
        }
    }

    static Transform BenchStand()
    {
        PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>();
        StationInteractable bench = ace != null ? ace.BenchStation : null;
        return bench == null ? null : bench.StandPoint != null ? bench.StandPoint : bench.transform;
    }

    static WaitingSpot SpotOf(CustomerBrain who) => who != null && BrainSpot != null ? BrainSpot.GetValue(who) as WaitingSpot : null;

    // ---------------------------------------------------------------- B. the settled guests

    static IEnumerator SettledGuests(DayClock clock)
    {
        PatronSpawner.OpeningGuest[] authored = patronSpawner.OpeningGuests;
        if (authored == null || authored.Length == 0) { Check(false, "the settled guests are in the scene (Wave 2 2)"); yield break; }
        Check(authored.All(g => g != null && g.firstDay == 1 && g.lastDay == 3), "the settled guests are Days 1-3 (the Producer's call 13)");
        // This lab day (Day 5) let in for the check: Play-mode data only, put back below (and gone when Play stops).
        // Both arms named (the integration, 14 Oct): with "(0, 0)" unnamed, C# 9 drops the names from the conditional's type
        // and days[i].firstDay below doesn't compile.
        var days = authored.Select(g => g != null ? (firstDay: g.firstDay, lastDay: g.lastDay) : (firstDay: 0, lastDay: 0)).ToArray();
        foreach (PatronSpawner.OpeningGuest g in authored)
            if (g != null) { g.firstDay = Mathf.Min(g.firstDay, clock.Day); g.lastDay = Mathf.Max(g.lastDay, clock.Day); }
        TopUpTheDay(clock, .05f);
        int incomeBefore = clock.PatronIncome;
        PatronSpawner.ForceOpeningGuests = true;
        GuestsDay?.SetValue(patronSpawner, -1);   // as a fresh morning (a second run in one session)
        int sat = patronSpawner.SeatOpeningGuests();
        for (int i = 0; i < authored.Length; i++)
            if (authored[i] != null) { authored[i].firstDay = days[i].firstDay; authored[i].lastDay = days[i].lastDay; }
        var guests = patronSpawner.OpeningGuestsSeated.ToList();
        // In the frame they were placed: seated, in their seats, nothing paid.
        Check(guests.Count == 4 && sat == 4, $"4 settled guests seated at once ({sat} of {guests.Count} sat at once)");
        foreach (var g in guests)
        {
            NpcSeating seating = g.guest != null ? g.guest.GetComponent<NpcSeating>() : null;
            Check(seating != null && seating.Current == NpcSeating.Phase.Seated && g.seat.Occupant == g.guest,
                $"{g.label}: seated on {g.seat.name} from the first frame ({(seating != null ? seating.Current.ToString() : "no seating")})");
        }
        Check(clock.PatronIncome == incomeBefore, $"patron income unchanged by them ({incomeBefore} -> {clock.PatronIncome}): they came in before the open");
        yield return Frames(3);

        // Their sitting idles out of step.
        var phases = new List<(string who, float t)>();
        foreach (var g in guests)
        {
            Animator a = g.guest != null ? g.guest.GetComponentInChildren<Animator>() : null;
            if (a == null) continue;
            AnimatorStateInfo state = a.GetCurrentAnimatorStateInfo(0);
            phases.Add((g.label, state.normalizedTime - Mathf.Floor(state.normalizedTime)));
        }
        float closest = 1f;
        for (int i = 0; i < phases.Count; i++)
            for (int j = i + 1; j < phases.Count; j++)
            {
                float d = Mathf.Abs(phases[i].t - phases[j].t);
                closest = Mathf.Min(closest, Mathf.Min(d, 1f - d));
            }
        Check(phases.Count == guests.Count && closest > .1f, $"their sitting idles are out of step (the closest two {closest:0.00} apart; more than 0.10 wanted): {string.Join(", ", phases.Select(p => $"{p.who} {p.t:0.00}"))}");

        // The laptop on; the pair are companions who chat.
        var laptop = guests.FirstOrDefault(g => g.guest != null && g.guest.IsLaptopPatron);
        NpcSocial laptopSocial = laptop.guest != null ? laptop.guest.GetComponent<NpcSocial>() : null;
        Check(laptop.guest != null && laptop.guest.AtLaptop && laptopSocial != null && laptopSocial.Prop != null && laptopSocial.Prop.gameObject.activeSelf,
            "the laptop worker's laptop is open in front of them (their eyes go to it)");
        var pair = guests.Where(g => g.guest != null && g.guest.Companion != null).Select(g => g.guest).ToList();
        NpcSocial a0 = pair.Count == 2 ? pair[0].GetComponent<NpcSocial>() : null, a1 = pair.Count == 2 ? pair[1].GetComponent<NpcSocial>() : null;
        Check(a0 != null && a1 != null && NpcAttentionDirector.AreCompanions(a0, a1), "the pair are companions (they chat sooner and more often)");
        yield return Wait(2f);
        Photo("B1-settled-guests-from-above", OverheadEye(out Vector3 aim), aim, 44f, true);
        float chatUntil = Time.realtimeSinceStartup + 28f;
        while (Time.realtimeSinceStartup < chatUntil && a0 != null && a1 != null && !(a0.InChat || a1.InChat)) yield return null;
        Check(a0 != null && a1 != null && (a0.InChat || a1.InChat), "the pair are chatting within 30 s");

        // They leave at their share of the day: the clock moved to just before each.
        var leftAt = new Dictionary<PatronBrain, (float day, float time)>();
        foreach (float share in guests.Select(g => g.leaveAt).Distinct().OrderBy(s => s))
        {
            TopUpTheDay(clock, share - .01f);
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && guests.Where(g => Mathf.Approximately(g.leaveAt, share)).Any(g => g.guest != null && !leftAt.ContainsKey(g.guest)))
            {
                foreach (var g in guests)
                    if (g.guest != null && g.guest.IsLeaving && !leftAt.ContainsKey(g.guest)) leftAt[g.guest] = (clock.NormalizedDay, Time.time);
                yield return null;
            }
        }
        foreach (var g in guests)
        {
            bool left = g.guest != null && leftAt.ContainsKey(g.guest);
            float at = left ? leftAt[g.guest].day : float.NaN;
            Check(left && Mathf.Abs(at - g.leaveAt) <= .03f, $"{g.label} ({g.seat.name}) leaves at {(left ? F(at, "0.000") : "-")} of the day ({F(g.leaveAt, "0.00")} wanted, within 0.03)");
        }
        if (pair.Count == 2 && leftAt.ContainsKey(pair[0]) && leftAt.ContainsKey(pair[1]))
        {
            float gap = Mathf.Abs(leftAt[pair[0]].time - leftAt[pair[1]].time);
            Check(gap <= 1.4f, $"the pair leave together ({gap:0.0} s apart; 1.4 s allowed)");
        }
        PatronSpawner.ForceOpeningGuests = false;
    }

    // The overhead game camera (CmShopCam) where it is, else the spec's numbers (43 degrees, about 34 m, FOV 44).
    static Vector3 OverheadEye(out Vector3 aim)
    {
        GameObject cam = GameObject.Find("CmShopCam");
        aim = new Vector3(0f, 0f, 7f);
        if (cam != null)
        {
            aim = cam.transform.position + cam.transform.forward * 34f;
            return cam.transform.position;
        }
        return new Vector3(0f, 23.79f, -15.87f);
    }

    // ---------------------------------------------------------------- C. the looks

    static IEnumerator Looks(CafeLabDirector lab)
    {
        var patrons = new List<(PatronBrain who, string look)>();
        foreach (int batch in new[] { 10, 10 })
        {
            var before = new HashSet<PatronBrain>(Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude));
            lab.SendPatronsNow(batch);
            yield return Frames(4);
            foreach (PatronBrain p in Object.FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude))
                if (!before.Contains(p)) patrons.Add((p, LookOf(p.gameObject)));
            if (batch == 10 && patrons.Count <= 10)
            {
                int repeats = patrons.Count - patrons.Select(p => p.look).Distinct().Count();
                Check(patrons.Count == 10 && repeats <= 1, $"the first 10 patrons, in at once: {repeats} repeated look(s) (at most 1)");
            }
        }
        var byLook = patrons.GroupBy(p => p.look).OrderByDescending(g => g.Count()).ToList();
        Check(patrons.Count == 20 && byLook.All(g => g.Count() <= 2), $"20 patrons in {byLook.Count} looks, no look on more than 2 (most: {(byLook.Count > 0 ? byLook[0].Key + " x" + byLook[0].Count() : "-")})");
        Note("Patrons' looks: " + string.Join("; ", byLook.Select(g => $"{g.Key} x{g.Count()}")));

        var walkIns = new List<CustomerBrain>();
        for (int round = 0; round < 4 && walkIns.Count < 6; round++)
        {
            var before = new HashSet<CustomerBrain>(Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude));
            lab.SendCustomersNow(Mathf.Min(3, 6 - walkIns.Count));
            yield return Frames(4);
            List<CustomerBrain> fresh = Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Where(c => !before.Contains(c)).ToList();
            walkIns.AddRange(fresh);
            yield return Wait(1f);
            foreach (CustomerBrain c in fresh) Record(c);
            // Out of the way for the next three (the counter has three places).
            lab.CustomersLeaveNow();
            yield return Wait(6f);
        }
        Check(PolygonNpcVisual.NameSexesKnown > 0, $"the names' sexes are known ({PolygonNpcVisual.NameSexesKnown} names: the CustomerSpawner patch and Wave 2 3)");
        Check(walkIns.Count >= 6 && mismatched.Count == 0, $"every named walk-in's body is their name's sex ({walkIns.Count} walk-ins; wrong: {(mismatched.Count == 0 ? "none" : string.Join(", ", mismatched))})");
        Note("Walk-ins: " + string.Join("; ", recorded));
        mismatched.Clear();
        recorded.Clear();
    }

    static readonly List<string> mismatched = new List<string>(), recorded = new List<string>();

    static void Record(CustomerBrain c)
    {
        if (c == null) return;
        PolygonNpcVisual visual = c.GetComponent<PolygonNpcVisual>();
        string look = LookOf(c.gameObject);
        PolygonNpcVisual.NameSex sex = PolygonNpcVisual.SexOfName(c.CustomerName);
        bool regular = c.Identity != null && c.Identity.IsRegular;
        bool cc0 = visual == null || visual.VisualInstance == null;
        bool ok = regular || sex == PolygonNpcVisual.NameSex.Any
                  || (cc0 ? sex == PolygonNpcVisual.NameSex.Man : PolygonNpcVisual.IsWomansLook(look) == (sex == PolygonNpcVisual.NameSex.Woman));
        recorded.Add($"{c.CustomerName} ({(regular ? "regular" : sex.ToString())}): {look}");
        if (!ok) mismatched.Add($"{c.CustomerName} ({sex}) in {look}");
    }

    // A person's look: the city look (and its palette), or the CC0 outfit they kept.
    static string LookOf(GameObject who)
    {
        PolygonNpcVisual visual = who.GetComponent<PolygonNpcVisual>();
        if (visual != null && visual.VisualInstance != null)
            return visual.ActiveAppearanceName + (visual.ActivePaletteName.Length > 0 ? " / " + visual.ActivePaletteName : "");
        NpcVisualVariants outfits = who.GetComponent<NpcVisualVariants>();
        return "CC0 " + (outfits != null ? outfits.ActiveAppearanceName : "original");
    }

    // ---------------------------------------------------------------- helpers

    static void Photo(string name, Vector3 eye, Vector3 target, float fov, bool overhead)
    {
        try
        {
            CafeSecondPassSteps.Capture(Path.Combine(folder, name + ".png"), eye, target, fov, overhead);
            photos++;
        }
        catch (Exception e) { Note($"photo {name} failed: {e.Message}"); }
    }

    static Transform Bone(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && !IsUnderCityLook(t, root)) return t;
        return null;
    }

    static bool IsUnderCityLook(Transform t, Transform root)
    {
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
            if (p.name.StartsWith("City look", StringComparison.Ordinal)) return true;
        return false;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
#endif
