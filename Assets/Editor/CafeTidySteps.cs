#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE CAFÉ, FOR PLAYTEST 3'S NOTE (session 6; claude/playtest-3-sessions-2-6-plan.md §7)
//
//   Fixit Fidget > Cafe furnishing > 2 - Photograph the counter with and without its warm fills (read-only)
//   Fixit Fidget > Cafe furnishing > 2b - List what stands at the counter (read-only)
//   Fixit Fidget > Cafe furnishing > 2c - Why the front sofa's seat A can't be served (read-only)
//   Fixit Fidget > Cafe furnishing > 3 - Playtest 3: put the junk away (scene)
//   Fixit Fidget > Cafe furnishing > 3 - Undo: bring the junk back (scene)
//   Fixit Fidget > Cafe furnishing > 3b - Gather the retail in the intake's cubbies (scene)
//   Fixit Fidget > Cafe furnishing > 4 - Light the counter: pendants over the front counter (scene)
//   Fixit Fidget > Cafe furnishing > 4 - Undo: take the counter's pendants down (scene)
//   Fixit Fidget > Cafe furnishing > 5 - Serve the front sofa's seat A: the lounge's waiting spot to the waiting nook (scene)
//   Fixit Fidget > Cafe furnishing > 6 - Switch off the stand-ins still wearing the default material (scene)
//
// His note (playtest 3): "the counters have inconsistencies ... the boxes in the corner ... a bag full of bags near the door
// ... a coat rack with two squares ... research how cafés look in San Francisco". What San Francisco's cafés share is how
// little is in them: one wood, one accent, the counter as the hero, retail on open shelves, almost nothing on the walls or
// the floor. So this pass is subtraction.
//
// 2 (read-only) answers the counter question before any wood is touched: the four front counter sections already share one
// set of materials, so the one that reads lighter and more orange may be lit, not painted (a warm fill hangs behind the
// middle). It reports each section's materials and how much of each warm fill reaches it, and photographs the counter as
// it is, with each fill off in turn, and with all of them off, from the room, from above and from the game's camera. Every
// light is put back. 2b lists every renderer in the counter's strip, the ceiling's fixtures, the café's lights and anything
// still wearing a default material.
//
// 3 puts away what his note named, as the 24 Sept furnishing pass (CafeFurnishing) and the POLYGON details (CafeLivelyProps)
// placed them: the coat stand with its hat, scarf and Ace's tote ("a coat rack with two squares"); the coffee sacks by the
// back door ("a bag full of bags near the door"); the crate and boxes in the staff corner past the bench, the box on the
// counter's end and the one on the back counter ("the boxes in the corner"); the clutter in the counter's cubbies (the
// kraft coffee bags, the crates, the cable coil, the books; the bean jars, the mugs and the plants stay, as retail on an
// open shelf); and what lay loose on the counter belonging to no station (a newspaper, three mugs, a bread on a plate, a
// stack of plates: the pastry case, the sign and the plant stay). Each piece is switched off, not deleted (the
// night's set-up finds the sacks by name), and recorded (CafeTidy) for the undo. The guards of the earlier café passes:
// the layout check and the occupied-circulation check before and after (rolled back if either gets worse), the routes
// re-baked, the whole room photographed before and after (CafeFurnishing.Photograph), and a close-up of each piece before,
// ringed, and the same view after. Save the scene afterwards.
//
// 3b gathers the retail 3 left (four bean jars, two stacks of mugs, two plants, scattered over three sections) in the
// intake's cubbies, where the queue stands; the other sections' cubbies stay empty. 3's undo puts them back too.
//
// 4 is the counter's answer, from 2's photos: the wood is one wood, the light was not. It hangs four pendants over the
// front counter, one of the tables' pendants cut out of the joinery's model so the café has one pendant, evenly spaced over
// its 12 m, each with the same warm light (registered with CafeDaylight so it dims by day and at night; the pendants with
// CafeViewMode's overhead fixtures so the overhead view looks past them, as over the tables); and it moves the fill that hung
// behind the middle section, with no lamp to show for it, back over the back bar, dimmer. Its undo takes them down and puts
// the fill back.
//
// 5 answers session 3's find (the occupied circulation 27 of 28: the front sofa's seat A unserved): 2c draws why, and 5
// moves the waiting spot that stood in the lounge's front corner into the waiting nook by the door, where the coat stand
// stood. 6 switches off what still wears Unity's default material (the bench's old screw-bin disc), Ace's capsule aside.
// 3's undo undoes 3b, 5 and 6 as well.
// ---------------------------------------------------------------------------
internal static class CafeTidySteps
{
    const string Tag = "[Cafe tidy] ";
    const string Menu = "Fixit Fidget/Cafe furnishing/";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    const string UndoName = "Put the cafe's junk away";
    const string FillName = "V3 cafe warm fill";
    static readonly string[] CounterSections = { "Counter", "Matching counter section" };
    static readonly Vector3 RoomMiddle = new Vector3(0f, 1.2f, 9f);

    // What goes, by the group it was placed in (a furnishing zone, a POLYGON details group) and its name; every piece of
    // that name in the group goes (two bags of the house beans, two sets of books). A name only one piece can have (the
    // sacks, the POLYGON boxes) is looked for anywhere in the café if it isn't in its group (Night 0's back door moved the
    // loose sack); "Crate" and "Cable coil" never are (the bench keeps its own).
    static readonly (string group, string name, string why)[] PutAway =
    {
        ("18 - cafe furnishing/D - Entrance and waiting nook", "Coat stand (waiting nook)", "a coat rack with two squares (its scarf and Ace's tote)"),
        ("18 - cafe furnishing/F - Work areas", "Coffee sacks", "a bag full of bags near the (back) door"),
        ("18 - cafe furnishing/F - Work areas", "Coffee sack", "a bag full of bags near the (back) door"),
        ("16 - POLYGON cafe details/Back of house", "Back of house - Crate_03", "the boxes in the corner"),
        ("16 - POLYGON cafe details/Back of house", "Back of house - Cardboard_Box_02", "the boxes in the corner"),
        ("16 - POLYGON cafe details/Back of house", "Back of house - Cardboard_Box_04", "the boxes in the corner"),
        ("16 - POLYGON cafe details/Drinks", "Drinks - Cardboard_Box_04", "a cardboard box on the back counter (the boxes)"),
        ("18 - cafe furnishing/E - Counter cubbies", "Coffee bag (house)", "the kraft bags in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Coffee bag (decaf)", "the kraft bags in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Coffee bag (single origin)", "the kraft bags in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Crate", "clutter in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Parts crate", "clutter in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Cable coil", "clutter in the counter's cubbies"),
        ("18 - cafe furnishing/E - Counter cubbies", "Cubby books", "clutter in the counter's cubbies"),
        ("16 - POLYGON cafe details/Counter", "Counter - Newspaper_01", "loose on the counter, belonging to no station (the counter as the hero)"),
        ("16 - POLYGON cafe details/Counter", "Counter - Mug_01", "loose on the counter, belonging to no station (the counter as the hero)"),
        ("16 - POLYGON cafe details/Counter", "Counter - Food_Bread_01", "loose on the counter, belonging to no station (the counter as the hero)"),
        ("16 - POLYGON cafe details/Counter", "Counter - Plate_01", "loose on the counter, belonging to no station (the counter as the hero)"),
    };

    // ================================================================== 2: the counter, lit and unlit

    [MenuItem(Menu + "2 - Photograph the counter with and without its warm fills (read-only)")]
    static void PhotographTheCounter()
    {
        var report = new StringBuilder();
        var switched = new List<Light>();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            folder = Folder("counter");
            var sections = cafe.GetComponentsInChildren<Transform>(true)
                .Where(t => CounterSections.Contains(t.name) && t.gameObject.activeInHierarchy).ToList();
            report.AppendLine($"The front counter: {sections.Count} section(s).");
            foreach (Transform s in sections.OrderBy(t => Centre(t).x))
            {
                Renderer[] parts = s.GetComponentsInChildren<Renderer>(false);
                string materials = string.Join(", ", parts.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct());
                report.AppendLine($"  {s.name} at x {Centre(s).x:0.00}, z {Centre(s).z:0.00}: {parts.Length} renderer(s); materials: {materials}");
            }
            Light[] fills = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(l => l.name == FillName).OrderBy(l => l.transform.position.x).ToArray();
            report.AppendLine($"The warm fills ('{FillName}'): {fills.Length}.");
            foreach (Light l in fills)
            {
                Vector3 p = l.transform.position;
                report.AppendLine($"  at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}), {(l.enabled && l.gameObject.activeInHierarchy ? "on" : "off")}, intensity {l.intensity:0.00}, " +
                                  $"range {l.range:0.0}, colour #{ColorUtility.ToHtmlStringRGB(l.color)}, shadows {l.shadows}");
            }
            // How much of each fill reaches each section's face (the inverse square, faded to nothing at the light's range,
            // as URP's point lights do): a rough share, to read against the photos.
            report.AppendLine("Warm fill reaching each section's face (relative):");
            foreach (Transform s in sections.OrderBy(t => Centre(t).x))
            {
                Vector3 face = Centre(s);
                var shares = fills.Select(l =>
                {
                    float d = Vector3.Distance(l.transform.position, face);
                    float fade = Mathf.Clamp01(1f - Mathf.Pow(d / Mathf.Max(.01f, l.range), 4f));
                    return l.intensity * fade * fade / Mathf.Max(.25f, d * d);
                }).ToArray();
                report.AppendLine($"  {s.name} at x {face.x:0.00}: {shares.Sum():0.000} ({string.Join(" + ", shares.Select(v => v.ToString("0.000", CultureInfo.InvariantCulture)))})");
            }

            // Photos, three views each (from the room at eye height, from above the room like the overhead view but closer,
            // and the game's overhead camera): as it is; each fill off in turn; all off. Every light is put back. Ace's
            // stand-in capsule is hidden, as in the furnishing photos.
            GameObject shop = GameObject.Find("CmShopCam");
            string localFolder = folder;
            void ShootAll(string state)
            {
                foreach (var v in CounterViews) Snap(Path.Combine(localFolder, $"{v.name}-{state}.png"), v.from, v.to, v.fov, false);
                if (shop != null)
                    Snap(Path.Combine(localFolder, $"game-{state}.png"), shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
            }
            ShootAll("1-as-it-is");
            for (int i = 0; i < fills.Length; i++)
            {
                if (!fills[i].enabled) continue;
                fills[i].enabled = false;
                switched.Add(fills[i]);
                ShootAll($"2-fill-{i + 1}-off");
                fills[i].enabled = true;
                switched.Remove(fills[i]);
            }
            foreach (Light l in fills.Where(l => l.enabled)) { l.enabled = false; switched.Add(l); }
            ShootAll("3-all-fills-off");
            foreach (Light l in switched) l.enabled = true;
            switched.Clear();
            report.AppendLine($"Photos: {folder} (front-, above- and game-: 1 as it is, 2 each fill off, 3 all off). Nothing in the scene changed.");
            Debug.Log(Tag + "The counter, lit and unlit:\n" + report);
            Keep(folder, "The counter, lit and unlit:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Photographing the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Photographing the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
        finally
        {
            foreach (Light l in switched) if (l != null) l.enabled = true;
        }
    }

    // ================================================================== 2b: what stands at the counter (read-only)

    // Every renderer drawn in the counter's strip of the room (both counters and the floor between), with where it sits in
    // the hierarchy, its mesh and its materials; and anywhere in the café, anything still wearing a default material (a
    // stand-in nobody dressed). Read-only.
    [MenuItem(Menu + "2b - List what stands at the counter (read-only)")]
    static void ListTheCounter()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            folder = Folder("counter-list");
            var strip = new Bounds(new Vector3(0f, 1.25f, 15.2f), new Vector3(15f, 2.5f, 5.2f));
            var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.enabled && !r.forceRenderingOff && r.gameObject.scene == cafe.gameObject.scene).ToArray();
            var here = all.Where(r => strip.Contains(r.bounds.center)).OrderBy(r => r.bounds.center.z).ThenBy(r => r.bounds.center.x).ToArray();
            report.AppendLine($"Drawn in the counter's strip (x -7.5..7.5, z 12.6..17.8, up to 2.5 m): {here.Length} renderer(s).");
            foreach (Renderer r in here) report.AppendLine("  " + Describe(r));
            // The ceiling: what the overhead view hides (shown in first person if enabled), and every light in the room.
            CafeViewMode mode = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            if (mode != null)
            {
                report.AppendLine($"Overhead fixtures (hidden in the overhead view; shown in first person only if enabled): {mode.overheadFixtures.Length}.");
                foreach (Renderer r in mode.overheadFixtures)
                    report.AppendLine("  " + (r == null ? "(missing)" : (r.enabled && r.gameObject.activeInHierarchy ? "ON  " : "off ") + Describe(r)));
            }
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(l => l.gameObject.scene == cafe.gameObject.scene && CafeDaylight.CafeInside.Contains(new Vector2(l.transform.position.x, l.transform.position.z)))
                .OrderBy(l => l.transform.position.z).ToArray();
            CafeDaylight daylight = Object.FindAnyObjectByType<CafeDaylight>(FindObjectsInactive.Include);
            report.AppendLine($"Lights in the café: {lights.Length}.");
            foreach (Light l in lights)
            {
                Vector3 p = l.transform.position;
                bool dims = daylight != null && daylight.warmLights.Contains(l);
                report.AppendLine($"  {l.name} ({l.type}) at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}): {(l.enabled && l.gameObject.activeInHierarchy ? "on" : "off")}, " +
                                  $"intensity {l.intensity:0.00}, range {l.range:0.0}, #{ColorUtility.ToHtmlStringRGB(l.color)}, shadows {l.shadows}; {(dims ? "dims with the day" : "NOT in the day's warm lights")}");
            }
            if (daylight != null)
                report.AppendLine($"The day's emissive renderers: {daylight.emissiveRenderers.Length}; emissive materials: {string.Join(", ", daylight.emissiveMaterials.Where(m => m != null).Select(m => m.name))}.");
            string[] defaults = { "Lit", "Default-Material", "Default-Diffuse", "Lit (Instance)", "Simple Lit", "Unlit" };
            var stand = all.Where(r => r.sharedMaterials.Any(m => m == null || defaults.Contains(m.name))).ToArray();
            report.AppendLine($"Wearing a default or missing material anywhere in the scene: {stand.Length}.");
            foreach (Renderer r in stand) report.AppendLine("  " + Describe(r));
            Debug.Log(Tag + "What stands at the counter:\n" + report);
            Keep(folder, "What stands at the counter:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Listing the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Listing the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    static string Describe(Renderer r)
    {
        var names = new List<string>();
        for (Transform p = r.transform; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        string path = string.Join("/", names.Skip(Math.Max(0, names.Count - 4)));
        MeshFilter mf = r.GetComponent<MeshFilter>();
        string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : r.GetType().Name;
        string materials = string.Join(", ", r.sharedMaterials.Select(m => m == null ? "(none)" : m.name));
        Bounds b = r.bounds;
        return $"{path} | {mesh} | {materials} | at ({b.center.x:0.00}, {b.center.y:0.00}, {b.center.z:0.00}) size ({b.size.x:0.00}, {b.size.y:0.00}, {b.size.z:0.00})";
    }

    // ================================================================== 2c: the front sofa's seat A (read-only)

    // Session 3 found the neighbourhood guard's occupied circulation at 27 of 28: with every seat, loiter spot and queue
    // place occupied at once, the front sofa's seat A has no reachable cell within 2.05 m. 2c runs the same model as
    // AcesCafeLayoutSetup.ValidateOccupiedCirculationV2 (Ace's capsule at the game's radius, a 0.20 m grid, 0.65 m bodies)
    // and draws it as a plan (Logs/CafeFurnishing/circulation-*/plan.png): green reachable, yellow clear but cut off, red
    // a collider, blue a body; white the places, magenta seat A and its 2.05 m. It lists what blocks the cells around seat A.
    [MenuItem(Menu + "2c - Why the front sofa's seat A can't be served (read-only)")]
    static void WhySeatA()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            folder = Folder("circulation");
            report.AppendLine(Circulation(folder, "Front sofa - seat A", out _));
            Debug.Log(Tag + "The front sofa's seat A:\n" + report);
            Keep(folder, "The front sofa's seat A:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The seat A plan FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "The seat A plan FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // The occupied-circulation model, as the layout guard runs it, with a plan drawn and the cells near one place explained.
    // Returns the report; `served` says whether that place has a reachable cell within 2.05 m.
    static string Circulation(string folder, string placeName, out bool served)
    {
        var report = new StringBuilder();
        Directory.CreateDirectory(folder);
        Physics.SyncTransforms();
        Scene scene = SceneManager.GetActiveScene();
        Transform playerT = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Player");
        CharacterController player = playerT != null ? playerT.GetComponent<CharacterController>() : null;
        if (player == null) throw new InvalidOperationException("No Player with a CharacterController.");
        Transform queue = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "CounterQueue");
        Transform floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Floor");
        if (queue == null || floor == null) throw new InvalidOperationException("No CounterQueue or Floor in the scene.");
        var targets = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude)
            .Where(w => w.gameObject.scene == scene).Select(w => w.StandPoint)
            .Concat(queue.Cast<Transform>()).Where(t => t != null).Distinct().ToArray();
        var occupied = targets.Select(t => new Vector2(t.position.x, t.position.z)).ToArray();
        float radius = PlayerMovement.RadiusFor(player);
        float height = Mathf.Max(player.height * Mathf.Abs(player.transform.lossyScale.y), radius * 2f);
        const float step = .20f, minX = -7.2f, minZ = -3.8f, standingRadius = .65f, reach = 2.05f;
        const int cols = 73, rows = 109;
        float bodyClearance = radius + standingRadius + .03f;
        var state = new int[cols * rows];          // 0 clear, 1 collider, 2 body
        var blockers = new string[cols * rows];
        var visited = new bool[cols * rows];
        var hits = new Collider[64];
        float floorY = floor.position.y;
        Vector2 start = new Vector2(player.transform.position.x, player.transform.position.z);
        int seed = -1; float seedDistance = .75f * .75f;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < cols; x++)
        {
            int i = z * cols + x;
            var point = new Vector2(minX + x * step, minZ + z * step);
            int body = Array.FindIndex(occupied, o => (point - o).sqrMagnitude < bodyClearance * bodyClearance);
            if (body >= 0) { state[i] = 2; blockers[i] = targets[body].parent.name + "/" + targets[body].name; continue; }
            Vector3 bottom = new Vector3(point.x, floorY + .06f + radius, point.y), top = new Vector3(point.x, floorY + .06f + height - radius, point.y);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + .03f, hits, ~0, QueryTriggerInteraction.Ignore);
            Collider by = null;
            for (int h = 0; h < count && by == null; h++)
                if (hits[h] != null && hits[h].GetComponentInParent<PlayerMovement>() == null) by = hits[h];
            if (by != null || count == hits.Length) { state[i] = 1; blockers[i] = by != null ? PathOf(by.transform) : "(64+ colliders)"; continue; }
            float d = (point - start).sqrMagnitude;
            if (d < seedDistance) { seedDistance = d; seed = i; }
        }
        if (seed >= 0)
        {
            var frontier = new Queue<int>();
            frontier.Enqueue(seed); visited[seed] = true;
            while (frontier.Count > 0)
            {
                int i = frontier.Dequeue(), x = i % cols, z = i / cols;
                foreach (int n in new[] { x > 0 ? i - 1 : -1, x < cols - 1 ? i + 1 : -1, z > 0 ? i - cols : -1, z < rows - 1 ? i + cols : -1 })
                    if (n >= 0 && state[n] == 0 && !visited[n]) { visited[n] = true; frontier.Enqueue(n); }
            }
        }
        int unreachable = 0;
        var notServed = new List<string>();
        for (int t = 0; t < targets.Length; t++)
        {
            bool ok = false;
            for (int i = 0; i < visited.Length && !ok; i++)
                ok = visited[i] && (new Vector2(minX + (i % cols) * step, minZ + (i / cols) * step) - occupied[t]).sqrMagnitude <= reach * reach;
            if (!ok) { unreachable++; notServed.Add(targets[t].parent.name + "/" + targets[t].name); }
        }
        report.AppendLine($"Occupied circulation (the guard's model, re-run): {targets.Length - unreachable}/{targets.Length} served; Ace's radius {radius:0.00} m, " +
                          $"bodies {standingRadius:0.00} m, so a cell needs {bodyClearance:0.00} m from every place. Not served: {(notServed.Count == 0 ? "none" : string.Join("; ", notServed))}.");

        // The place asked about: what is within reach of it.
        int placeIndex = Array.FindIndex(targets, t => t.parent != null && t.parent.name == placeName);
        served = false;
        if (placeIndex < 0) report.AppendLine($"No place named '{placeName}' among the {targets.Length}.");
        else
        {
            Vector2 at = occupied[placeIndex];
            int reachable = 0, cut = 0, colliders = 0, bodies = 0;
            var byCollider = new Dictionary<string, int>();
            var byBody = new Dictionary<string, int>();
            for (int i = 0; i < state.Length; i++)
            {
                var point = new Vector2(minX + (i % cols) * step, minZ + (i / cols) * step);
                if ((point - at).sqrMagnitude > reach * reach) continue;
                if (visited[i]) reachable++;
                else if (state[i] == 0) cut++;
                else if (state[i] == 1) { colliders++; byCollider[blockers[i]] = byCollider.TryGetValue(blockers[i], out int c) ? c + 1 : 1; }
                else { bodies++; byBody[blockers[i]] = byBody.TryGetValue(blockers[i], out int c) ? c + 1 : 1; }
            }
            served = reachable > 0;
            report.AppendLine($"{placeName} stands at ({at.x:0.00}, {at.y:0.00}). Within {reach:0.00} m of it: {reachable} reachable cell(s), {cut} clear but cut off, " +
                              $"{colliders} under a collider, {bodies} inside a body.");
            report.AppendLine("  Colliders: " + string.Join("; ", byCollider.OrderByDescending(k => k.Value).Select(k => $"{k.Key} ({k.Value})")));
            report.AppendLine("  Bodies: " + string.Join("; ", byBody.OrderByDescending(k => k.Value).Select(k => $"{k.Key} ({k.Value})")));
            var near = targets.Select((t, k) => (t, k)).Where(p => p.k != placeIndex && (occupied[p.k] - at).magnitude < 3.2f)
                .OrderBy(p => (occupied[p.k] - at).magnitude);
            report.AppendLine("  Places within 3.2 m: " + string.Join("; ", near.Select(p => $"{p.t.parent.name} at ({occupied[p.k].x:0.00}, {occupied[p.k].y:0.00}), {(occupied[p.k] - at).magnitude:0.00} m")));
        }

        // The plan.
        const int px = 8;
        var image = new Texture2D(cols * px, rows * px, TextureFormat.RGB24, false);
        try
        {
            var colours = new[] { new Color(.95f, .85f, .25f), new Color(.65f, .12f, .10f), new Color(.20f, .35f, .85f) };
            for (int i = 0; i < state.Length; i++)
            {
                Color c = visited[i] ? new Color(.35f, .75f, .35f) : colours[state[i]];
                int cx = (i % cols) * px, cz = (i / cols) * px;
                for (int a = 0; a < px - 1; a++) for (int b = 0; b < px - 1; b++) image.SetPixel(cx + a, cz + b, c);
            }
            void Dot(Vector2 p, Color c, int r)
            {
                int ix = Mathf.RoundToInt((p.x - minX) / step * px + px * .5f), iz = Mathf.RoundToInt((p.y - minZ) / step * px + px * .5f);
                for (int a = -r; a <= r; a++) for (int b = -r; b <= r; b++)
                    if (a * a + b * b <= r * r && ix + a >= 0 && ix + a < cols * px && iz + b >= 0 && iz + b < rows * px) image.SetPixel(ix + a, iz + b, c);
            }
            foreach (Vector2 o in occupied) Dot(o, Color.white, 4);
            if (placeIndex >= 0)
            {
                Vector2 at = occupied[placeIndex];
                Dot(at, Color.magenta, 6);
                for (int k = 0; k < 720; k++)
                {
                    float ang = k / 720f * Mathf.PI * 2f;
                    Dot(at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * reach, Color.magenta, 1);
                }
            }
            Dot(start, Color.black, 5);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, "plan.png"), image.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(image); }
        report.Append($"Plan: {Path.Combine(folder, "plan.png")} (x to the right, the street at the bottom; 8 px a cell).");
        return report.ToString();
    }

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (Transform p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names.Skip(Math.Max(0, names.Count - 3)));
    }

    // ================================================================== 5: the front sofa's seat A served

    // 2c's plan: seat A's stand point (-5.92, 2.45) sits in the lounge's front corner between the tub chair's (0.87 m),
    // a waiting spot's (WaitingArea/WaitSpot 2, at the front window, 1.61 m) and a table seat's (1.64 m); with all of them
    // occupied, their bodies close every cell within 2.05 m of it. The waiting spot is the one that doesn't belong there:
    // the café's waiting is by the door (WaitSpot 3 and 4, the waiting nook, where the coat stand stood until 3 put it
    // away). 5 moves WaitSpot 2 into the nook, trying places from the coat stand's old spot outward, and keeps the first
    // that serves every place (seat A included) with every customer route still complete; otherwise nothing changes.
    // Recorded with 3's moves (CafeTidy) so 3's undo puts it back.
    static readonly Vector3[] NookPlaces =
    {
        new Vector3(6.2f, .06f, 2.4f), new Vector3(6.5f, .06f, 2.4f), new Vector3(5.8f, .06f, 2.4f),
        new Vector3(6.2f, .06f, 5.6f), new Vector3(5.4f, .06f, 1.0f),
    };

    [MenuItem(Menu + "5 - Serve the front sofa's seat A: the lounge's waiting spot to the waiting nook (scene)")]
    static void ServeSeatA()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            CafeTidy record = cafe.GetComponent<CafeTidy>();
            if (record == null) throw new InvalidOperationException("Run 3 - Playtest 3: put the junk away first (the coat stand's spot is where the waiting goes).");
            Scene scene = SceneManager.GetActiveScene();
            GameObject areaObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "WaitingArea");
            if (areaObject == null) throw new InvalidOperationException("No WaitingArea in the scene.");
            Transform area = areaObject.transform;
            Transform spot = area.Find("WaitSpot 2");
            if (spot == null) throw new InvalidOperationException("No WaitingArea/WaitSpot 2.");
            if (spot.position.x > 0f) { Keep(null, "WaitSpot 2 is already in the waiting nook."); return; }

            folder = Folder("seat-a");
            string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
            string before = Circulation(Path.Combine(folder, "before"), "Front sofa - seat A", out bool servedBefore);
            report.AppendLine("Before: " + layoutBefore);
            report.AppendLine(before);
            Vector3 was = spot.position;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Serve the front sofa's seat A");
            try
            {
                Undo.RecordObject(spot, "Serve the front sofa's seat A");
                Vector3? chosen = null;
                string after = null;
                foreach (Vector3 place in NookPlaces)
                {
                    spot.position = place;
                    string tryFolder = Path.Combine(folder, $"try-{place.x:0.0}-{place.z:0.0}");
                    Directory.CreateDirectory(tryFolder);
                    string result = Circulation(tryFolder, "Front sofa - seat A", out bool served);
                    bool all = result.Contains("Not served: none");
                    report.AppendLine($"WaitSpot 2 at ({place.x:0.00}, {place.z:0.00}): seat A {(served ? "served" : "not served")}; {(all ? "every place served" : "a place unserved")}.");
                    if (served && all) { chosen = place; after = result; break; }
                }
                if (chosen == null) throw new InvalidOperationException("No place in the waiting nook serves every place; nothing was kept.");
                Directory.CreateDirectory(Path.Combine(folder, "after"));
                after = Circulation(Path.Combine(folder, "after"), "Front sofa - seat A", out _);
                AcesCafeLayoutSetup.BakeRoutes();
                string layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
                string guard = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                if (Reached(layoutAfter) < Reached(layoutBefore) || (layoutBefore.Contains("PASS") && !layoutAfter.Contains("PASS")) || !guard.Contains("PASS"))
                    throw new InvalidOperationException("The routes got worse with the spot moved; nothing was kept.\n" + layoutAfter + "\n" + guard);
                Undo.RecordObject(record, "Serve the front sofa's seat A");
                record.moved = record.moved.Concat(new[] { spot }).ToArray();
                record.movedFrom = record.movedFrom.Concat(new[] { was }).ToArray();
                EditorUtility.SetDirty(record);
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                report.AppendLine($"Moved WaitingArea/WaitSpot 2 from ({was.x:0.00}, {was.z:0.00}) to ({chosen.Value.x:0.00}, {chosen.Value.z:0.00}).");
                report.AppendLine("After: " + layoutAfter);
                report.AppendLine("The guard: " + guard);
                report.AppendLine(after);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Physics.SyncTransforms();
                AcesCafeLayoutSetup.BakeRoutes();
                throw;
            }
            report.Append($"Plans: {folder} (before, each try, after). The routes are re-baked. Save the scene.");
            Debug.Log(Tag + "The front sofa's seat A:\n" + report);
            Keep(folder, "The front sofa's seat A:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Serving seat A FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Serving seat A FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // ================================================================== 6: stand-ins nobody dressed

    // 2b found one piece in the café still wearing Unity's default material besides Ace's stand-in capsule (which the game
    // hides itself): Workbench/InspectRig/ScrewBin/Cylinder, a 5 cm grey disc floating by the bench's tool stand, left from
    // the bench's first version (BenchRig.ScrewBin is read by nothing; the driver fetches the screws). 6 switches off every
    // such piece but the Player's, and adds it to 3's record so 3's undo brings it back.
    [MenuItem(Menu + "6 - Switch off the stand-ins still wearing the default material (scene)")]
    static void HideStandIns()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            CafeTidy record = cafe.GetComponent<CafeTidy>();
            if (record == null) throw new InvalidOperationException("Run 3 - Playtest 3: put the junk away first (6 adds to its record).");
            string[] defaults = { "Lit", "Default-Material", "Default-Diffuse" };
            Scene scene = cafe.gameObject.scene;
            var standIns = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.gameObject.scene == scene && r.enabled && r.GetComponentInParent<PlayerMovement>(true) == null
                            && r.transform.root.name != "Player"
                            && r.sharedMaterials.Any(m => m != null && defaults.Contains(m.name)))
                .Select(r => r.gameObject).Distinct().ToList();
            if (standIns.Count == 0) { Keep(null, "No stand-ins wearing the default material (but Ace's capsule, which the game hides)."); return; }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Switch off the undressed stand-ins");
            Undo.RecordObject(record, "Switch off the undressed stand-ins");
            foreach (GameObject go in standIns)
            {
                report.AppendLine("  " + PathOf(go.transform) + $" at ({go.transform.position.x:0.00}, {go.transform.position.y:0.00}, {go.transform.position.z:0.00})");
                Undo.RecordObject(go, "Switch off the undressed stand-ins");
                go.SetActive(false);
            }
            record.putAway = record.putAway.Concat(standIns).ToArray();
            record.why = record.why.Concat(standIns.Select(_ => "a stand-in still wearing the default material")).ToArray();
            EditorUtility.SetDirty(record);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            string text = $"Switched off {standIns.Count} stand-in(s) still wearing the default material (recorded with 3's for the undo):\n" + report + "Save the scene.";
            Debug.Log(Tag + text);
            Keep(null, text);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Switching off the stand-ins FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(null, "Switching off the stand-ins FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // ================================================================== 3: the junk away

    [MenuItem(Menu + "3 - Playtest 3: put the junk away (scene)")]
    static void PutTheJunkAway()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            if (cafe.GetComponent<CafeTidy>() != null)
            {
                Debug.Log(Tag + "The junk is already put away (3 - Undo: bring the junk back undoes it).");
                Keep(null, "The junk is already put away (3 - Undo: bring the junk back undoes it).");
                return;
            }
            // Everything is found before anything changes.
            var found = new List<(GameObject go, string why)>();
            var missing = new List<string>();
            foreach (var (group, name, why) in PutAway)
            {
                // The group by its path under the café's root; failing that, the first transform with its last name.
                Transform g = cafe.Find(group);
                if (g == null)
                {
                    string last = group.Substring(group.LastIndexOf('/') + 1);
                    g = cafe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == last);
                }
                if (g == null) { missing.Add($"{name} (no group '{group}')"); continue; }
                var hits = g.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == name && t.gameObject.activeSelf && !found.Any(f => f.go == t.gameObject)).ToList();
                if (hits.Count == 0 && Distinct(name))
                    hits = cafe.GetComponentsInChildren<Transform>(true).Where(t => t.name == name && t.gameObject.activeSelf && !found.Any(f => f.go == t.gameObject)).ToList();
                if (hits.Count == 0) { missing.Add($"{name} (in {group})"); continue; }
                foreach (Transform t in hits) found.Add((t.gameObject, why));
            }
            if (found.Count == 0) throw new InvalidOperationException("None of the pieces his note named is in the scene: nothing to put away.");

            folder = Folder("tidy");
            string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
            string circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
            string roomBefore = CafeFurnishing.Photograph("tidy-before");
            var views = new List<(string name, Vector3 from, Vector3 to, float ring)>();
            foreach (var (go, why) in found)
            {
                var view = CloseUp(go, out float ring);
                string name = $"{views.Count + 1:00}-{Safe(go.name)}";
                views.Add((name, view.from, view.to, ring));
                string path = Path.Combine(folder, name + "-before.png");
                Snap(path, view.from, view.to, CloseUpFov, false);
                Ring(path, ring);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            string layoutAfter, circulationAfter;
            try
            {
                CafeTidy record = Undo.AddComponent<CafeTidy>(cafe.gameObject);
                foreach (var (go, _) in found)
                {
                    Undo.RecordObject(go, UndoName);
                    go.SetActive(false);
                }
                record.putAway = found.Select(f => f.go).ToArray();
                record.why = found.Select(f => f.why).ToArray();
                EditorUtility.SetDirty(record);
                Physics.SyncTransforms();
                AcesCafeLayoutSetup.BakeRoutes();
                layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
                circulationAfter = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                // Not worse: as many paths and served positions as before (the circulation was 27 of 28 before this pass,
                // so a PASS alone can't be the test), and a PASS kept if there was one.
                bool layoutOk = Reached(layoutAfter) >= Reached(layoutBefore) && (layoutAfter.Contains("PASS") || !layoutBefore.Contains("PASS"));
                bool circulationOk = Reached(circulationAfter) >= Reached(circulationBefore) && (circulationAfter.Contains("PASS") || !circulationBefore.Contains("PASS"));
                if (!layoutOk || !circulationOk)
                    throw new InvalidOperationException("Putting the junk away made a customer route worse, so nothing was kept.\nBefore: " + layoutBefore +
                                                        "\n        " + circulationBefore + "\nAfter:  " + layoutAfter + "\n        " + circulationAfter);
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Physics.SyncTransforms();
                AcesCafeLayoutSetup.BakeRoutes();
                throw;
            }
            EditorSceneManager.MarkSceneDirty(cafe.gameObject.scene);
            foreach (var v in views) Snap(Path.Combine(folder, v.name + "-after.png"), v.from, v.to, CloseUpFov, false);
            string roomAfter = CafeFurnishing.Photograph("tidy-after");

            report.AppendLine($"Put away (switched off, recorded on the café's root for the undo): {found.Count} piece(s).");
            foreach (var group2 in found.GroupBy(f => f.why))
                report.AppendLine($"  {group2.Key}: {string.Join(", ", group2.Select(f => f.go.name))}");
            if (missing.Count > 0) report.AppendLine("Not in the scene (nothing to do): " + string.Join("; ", missing));
            report.AppendLine("Layout check before: " + layoutBefore);
            report.AppendLine("Layout check after:  " + layoutAfter);
            report.AppendLine("Circulation before:  " + circulationBefore);
            report.AppendLine("Circulation after:   " + circulationAfter);
            report.AppendLine($"Photos: each piece ringed before and the same view after in {folder}; the room before {roomBefore}, after {roomAfter}.");
            report.Append("The routes are re-baked. Save the scene (Ctrl+S) to keep it.");
            Debug.Log(Tag + "The junk is put away.\n" + report);
            Keep(folder, "The junk is put away.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting the junk away FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Putting the junk away FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "3 - Undo: bring the junk back (scene)")]
    static void BringItBack()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            CafeTidy record = cafe.GetComponent<CafeTidy>();
            if (record == null) { Debug.Log(Tag + "Nothing was put away in this scene."); Keep(null, "Nothing was put away in this scene."); return; }
            Undo.SetCurrentGroupName("Bring the cafe's junk back");
            int group = Undo.GetCurrentGroup();
            int back = 0;
            foreach (GameObject go in record.putAway)
            {
                if (go == null) continue;
                Undo.RecordObject(go, "Bring the cafe's junk back");
                go.SetActive(true);
                back++;
            }
            int returned = 0;
            for (int i = 0; i < record.moved.Length && i < record.movedFrom.Length; i++)
            {
                if (record.moved[i] == null) continue;
                Undo.RecordObject(record.moved[i], "Bring the cafe's junk back");
                record.moved[i].position = record.movedFrom[i];
                returned++;
            }
            Undo.DestroyObjectImmediate(record);
            Undo.CollapseUndoOperations(group);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            EditorSceneManager.MarkSceneDirty(cafe.gameObject.scene);
            string done = $"Brought back {back} piece(s), put {returned} moved piece(s) back where they stood, and re-baked the routes. {AcesCafeLayoutSetup.ValidateLayout()} Save the scene.";
            Debug.Log(Tag + done);
            Keep(null, done);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Bringing the junk back FAILED: " + e.Message + "\n" + e);
            Keep(null, "Bringing the junk back FAILED: " + e.Message + "\n" + e);
        }
    }

    // ================================================================== 3b: the retail, gathered at the intake

    // After 3, the counter's twelve cubbies held a plant at the far left, one bean jar on the left, three jars, two stacks of
    // mugs and a plant at the intake, and nothing on the right: a scatter, not a display. 3b gathers the retail where the
    // queue stands (the intake section, "02 - intake and queue"): the far plant into its left cubby, the four bean jars evenly
    // across its middle cubby, the mugs and the other plant in its right cubby as they were. The other sections' cubbies stay
    // empty: clean. Recorded on the café's root (CafeTidy) for 3's undo.
    [MenuItem(Menu + "3b - Gather the retail in the intake's cubbies (scene)")]
    static void GatherTheRetail()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            CafeTidy record = cafe.GetComponent<CafeTidy>();
            if (record == null) throw new InvalidOperationException("Run 3 - Playtest 3: put the junk away first (3b gathers what it left).");
            if (record.moved.Length > 0) { Keep(null, "The retail is already gathered (3 - Undo: bring the junk back undoes it)."); return; }
            Transform cubbies = cafe.Find("18 - cafe furnishing/E - Counter cubbies");
            if (cubbies == null) throw new InvalidOperationException("The counter's cubbies (18 - cafe furnishing/E - Counter cubbies) aren't in the scene.");
            Transform intake = cafe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Counter_Mesh" && t.parent != null && t.parent.name == "Counter" && t.parent.parent != null && t.parent.parent.name == "02 - intake and queue");
            if (intake == null) throw new InvalidOperationException("The intake's counter (02 - intake and queue/Counter/Counter_Mesh) isn't in the scene.");
            Bounds Part(string name)
            {
                Renderer r = intake.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name == name);
                if (r == null) throw new InvalidOperationException($"The intake's counter has no {name}.");
                return r.bounds;
            }
            Bounds endL = Part("Counter_End_L"), dividerL = Part("Counter_Divider_L"), dividerR = Part("Counter_Divider_R"), endR = Part("Counter_End_R");
            var jars = cubbies.Cast<Transform>().Where(t => t.name == "Bean jar" && t.gameObject.activeSelf).OrderBy(t => t.position.x).ToList();
            var plants = cubbies.Cast<Transform>().Where(t => t.name == "Cubby plant" && t.gameObject.activeSelf).OrderBy(t => t.position.x).ToList();
            if (jars.Count == 0) throw new InvalidOperationException("No bean jars in the cubbies.");
            bool InIntake(Transform t) => t.position.x > endL.max.x && t.position.x < endR.min.x;
            Transform intakeJar = jars.FirstOrDefault(InIntake);
            Transform intakePlant = plants.FirstOrDefault(InIntake);
            float jarY = intakeJar != null ? intakeJar.position.y : jars[0].position.y;

            var moves = new List<(Transform what, Vector3 to)>();
            // The jars, evenly across the middle cubby.
            float from = dividerL.max.x, to = dividerR.min.x, step = (to - from) / jars.Count;
            for (int i = 0; i < jars.Count; i++)
                moves.Add((jars[i], new Vector3(from + step * (i + .5f), jarY, jars[i].position.z)));
            // A plant that isn't at the intake goes to its left cubby, at the height of the one that is.
            Transform farPlant = plants.FirstOrDefault(p => !InIntake(p));
            if (farPlant != null)
                moves.Add((farPlant, new Vector3((endL.max.x + dividerL.min.x) * .5f, intakePlant != null ? intakePlant.position.y : farPlant.position.y, farPlant.position.z)));
            moves = moves.Where(m => (m.what.position - m.to).sqrMagnitude > .0001f).ToList();
            if (moves.Count == 0) { Keep(null, "The retail is already where 3b would put it."); return; }

            folder = Folder("retail");
            Vector3 eye = new Vector3((endL.max.x + endR.min.x) * .5f, .95f, intake.position.z - 2.3f);
            Vector3 look = new Vector3(eye.x, .3f, intake.position.z);
            Snap(Path.Combine(folder, "intake-before.png"), eye, look, 62f, false);
            Snap(Path.Combine(folder, "counter-before.png"), new Vector3(-1.2f, 1.05f, 9.6f), new Vector3(-1.2f, .35f, 13.7f), 70f, false);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Gather the retail in the intake's cubbies");
            Undo.RecordObject(record, "Gather the retail in the intake's cubbies");
            record.moved = moves.Select(m => m.what).ToArray();
            record.movedFrom = moves.Select(m => m.what.position).ToArray();
            foreach (var (what, at) in moves)
            {
                Undo.RecordObject(what, "Gather the retail in the intake's cubbies");
                report.AppendLine($"  {what.name}: from ({what.position.x:0.00}, {what.position.y:0.00}, {what.position.z:0.00}) to ({at.x:0.00}, {at.y:0.00}, {at.z:0.00})");
                what.position = at;
            }
            EditorUtility.SetDirty(record);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cafe.gameObject.scene);
            Snap(Path.Combine(folder, "intake-after.png"), eye, look, 62f, false);
            Snap(Path.Combine(folder, "counter-after.png"), new Vector3(-1.2f, 1.05f, 9.6f), new Vector3(-1.2f, .35f, 13.7f), 70f, false);
            string text = $"The retail gathered in the intake's cubbies ({moves.Count} piece(s) moved; nothing on a route):\n" + report +
                          $"Photos: {folder} (the intake's cubbies and the counter, before and after). Save the scene.";
            Debug.Log(Tag + text);
            Keep(folder, text);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Gathering the retail FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Gathering the retail FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // ================================================================== 4: the counter's light

    const string PendantGroup = "25 - counter pendants (session 6)";
    const string PendantMeshPath = "Assets/Playtests/AcesCafeLayout/Interior/Counter pendant (one of the tables').asset";
    const float PendantZ = 13.75f;              // over the counter's middle line (its top runs z 13.28..14.22)
    const float PendantIntensity = 1.2f;        // each; the tables' pendants are 6 at 2.83 m, the fill was 4
    const float PendantRange = 4.5f;
    static readonly Vector3 FillWas = new Vector3(0f, 2.7f, 15.2f);
    // Over the back bar (the machine, the bench), wider and dimmer: the bar's ends and the staff floor keep about the light
    // they had, the pool it threw on the floor behind the counter goes, and the counter's sections stay within a few levels.
    static readonly Vector3 FillTo = new Vector3(0f, 2.8f, 16.2f);
    const float FillIntensity = 2f, FillRange = 9f;

    // The counter (x -7.29 .. 4.68, 12 m) in four equal bays, a pendant over the middle of each.
    static readonly float[] PendantX = { -5.79f, -2.79f, .21f, 3.21f };

    [MenuItem(Menu + "4 - Light the counter: pendants over the front counter (scene)")]
    static void LightTheCounter()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            if (cafe.Find(PendantGroup) != null) { Keep(null, "The counter's pendants are already up (4 - Undo: take the counter's pendants down)."); return; }
            CafeViewMode mode = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            if (mode == null) throw new InvalidOperationException("No CafeViewMode (on the Player) in the scene.");
            CafeDaylight daylight = Object.FindAnyObjectByType<CafeDaylight>(FindObjectsInactive.Include);
            if (daylight == null) throw new InvalidOperationException("No CafeDaylight in the scene.");
            Transform source = cafe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PendantFixtures");
            if (source == null) throw new InvalidOperationException("The tables' pendants (PendantFixtures, in the joinery) aren't in the scene.");
            Light[] all = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Light tableLight = all.Where(l => l.name == "V3 table pendant").OrderBy(l => (l.transform.position - new Vector3(-3f, 2.83f, 9f)).sqrMagnitude).FirstOrDefault();
            if (tableLight == null) throw new InvalidOperationException("No table pendant's light (V3 table pendant) to match.");
            Light fill = all.Where(l => l.name == FillName).OrderBy(l => (l.transform.position - FillWas).sqrMagnitude).FirstOrDefault();
            if (fill == null || Vector3.Distance(fill.transform.position, FillWas) > .3f)
                throw new InvalidOperationException($"The warm fill behind the counter isn't where it was ({FillWas}).");

            // One of the tables' pendants, cut out of the joinery's combined meshes (one per material), as its own mesh.
            Mesh pendant = OnePendant(source, tableLight.transform.position, out Material[] materials, out float top, out float rim, report);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(PendantMeshPath);
            if (saved == null) { AssetDatabase.CreateAsset(pendant, PendantMeshPath); saved = pendant; }
            else { EditorUtility.CopySerialized(pendant, saved); Object.DestroyImmediate(pendant); EditorUtility.SetDirty(saved); }
            AssetDatabase.SaveAssets();
            report.AppendLine($"The pendant: {saved.vertexCount} vertices, {saved.subMeshCount} materials ({string.Join(", ", materials.Select(m => m != null ? m.name : "none"))}); " +
                              $"it hangs from {top:0.00} m to {rim:0.00} m, as over the tables ({PendantMeshPath}).");

            folder = Folder("counter-light");
            ShootCounter(folder, "1-before");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Hang the counter's pendants");
            var group = new GameObject(PendantGroup);
            Undo.RegisterCreatedObjectUndo(group, "Hang the counter's pendants");
            Undo.SetTransformParent(group.transform, cafe, "Hang the counter's pendants");
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.transform.localScale = Vector3.one;
            var shades = new List<Renderer>();
            var lights = new List<Light>();
            float lightY = rim + .08f;   // inside the shade, just above its rim
            for (int i = 0; i < PendantX.Length; i++)
            {
                var p = new GameObject($"Counter pendant {i + 1}");
                Undo.RegisterCreatedObjectUndo(p, "Hang the counter's pendants");
                p.transform.SetParent(group.transform, false);
                p.transform.SetPositionAndRotation(new Vector3(PendantX[i], top, PendantZ), Quaternion.identity);
                p.AddComponent<MeshFilter>().sharedMesh = saved;
                var r = p.AddComponent<MeshRenderer>();
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.Off;   // a lamp under a ceiling doesn't catch the sun (no blobs on the floor)
                shades.Add(r);
                var lightObject = new GameObject("Counter pendant light");
                Undo.RegisterCreatedObjectUndo(lightObject, "Hang the counter's pendants");
                lightObject.transform.SetParent(p.transform, false);
                lightObject.transform.position = new Vector3(PendantX[i], lightY, PendantZ);
                var l = lightObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = tableLight.color;
                l.intensity = PendantIntensity;
                l.range = PendantRange;
                l.shadows = LightShadows.None;
                l.renderMode = tableLight.renderMode;
                l.cullingMask = tableLight.cullingMask;
                lights.Add(l);
            }
            var record = Undo.AddComponent<CafeCounterPendants>(group);
            record.lights = lights.ToArray();
            record.shades = shades.ToArray();
            record.movedFill = fill;
            record.fillWasAt = fill.transform.position;
            record.fillWasIntensity = fill.intensity;
            record.fillWasRange = fill.range;
            EditorUtility.SetDirty(record);

            // Hidden in the overhead view like every ceiling fixture; dimmed by day and at night like every warm light.
            Undo.RecordObject(mode, "Hang the counter's pendants");
            mode.overheadFixtures = mode.overheadFixtures.Concat(shades).ToArray();
            EditorUtility.SetDirty(mode);
            Undo.RecordObject(daylight, "Hang the counter's pendants");
            daylight.warmLights = daylight.warmLights.Concat(lights).ToArray();
            EditorUtility.SetDirty(daylight);

            // The fill: back over the back bar, dimmer (its light there stays about what it was; the counter's is the pendants').
            Undo.RecordObject(fill.transform, "Hang the counter's pendants");
            fill.transform.position = FillTo;
            Undo.RecordObject(fill, "Hang the counter's pendants");
            fill.intensity = FillIntensity;
            fill.range = FillRange;
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cafe.gameObject.scene);

            ShootCounter(folder, "2-after");
            report.AppendLine($"Hung {shades.Count} pendants over the front counter at x {string.Join(", ", PendantX.Select(x => x.ToString("0.00", CultureInfo.InvariantCulture)))}, z {PendantZ:0.00}; " +
                              $"each light {PendantIntensity:0.0} (range {PendantRange:0.0}, #{ColorUtility.ToHtmlStringRGB(tableLight.color)}, the tables' colour) at {lightY:0.00} m.");
            report.AppendLine($"Registered: {shades.Count} overhead fixtures (hidden from above), {lights.Count} warm lights (they dim with the day and the night).");
            report.AppendLine($"The fill behind the middle section: from ({record.fillWasAt.x:0.00}, {record.fillWasAt.y:0.00}, {record.fillWasAt.z:0.00}) at {record.fillWasIntensity:0.0} (range {record.fillWasRange:0.0}) " +
                              $"to ({FillTo.x:0.00}, {FillTo.y:0.00}, {FillTo.z:0.00}) at {FillIntensity:0.0} (range {FillRange:0.0}).");
            report.AppendLine("Warm light reaching each section's top (relative, the inverse square with the light's angle):");
            foreach (float x in new[] { -6.05f, -3.35f, -.15f, 3.26f })
                report.AppendLine($"  x {x:0.00}: {WarmOn(new Vector3(x, 1.1f, 13.75f), Vector3.up):0.000}");
            report.AppendLine($"And on the back bar's top: x -3: {WarmOn(new Vector3(-3f, 1f, 17.4f), Vector3.up):0.000}, x 0: {WarmOn(new Vector3(0f, 1f, 17.4f), Vector3.up):0.000}, x 3: {WarmOn(new Vector3(3f, 1f, 17.4f), Vector3.up):0.000}; " +
                              $"the staff floor: x -3: {WarmOn(new Vector3(-3f, 0f, 15.6f), Vector3.up):0.000}, x 0: {WarmOn(new Vector3(0f, 0f, 15.6f), Vector3.up):0.000}, x 3: {WarmOn(new Vector3(3f, 0f, 15.6f), Vector3.up):0.000}.");
            report.Append($"Photos: {folder} (front-, above-, door- and game-: 1 before, 2 after). Save the scene.");
            Debug.Log(Tag + "The counter's light:\n" + report);
            Keep(folder, "The counter's light:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Lighting the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Lighting the counter FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "4 - Undo: take the counter's pendants down (scene)")]
    static void TakeThePendantsDown()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform cafe = Cafe();
            Transform group = cafe.Find(PendantGroup);
            if (group == null) { Keep(null, "There are no counter pendants in this scene."); return; }
            CafeCounterPendants record = group.GetComponent<CafeCounterPendants>();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Take the counter's pendants down");
            if (record != null && record.movedFill != null)
            {
                Undo.RecordObject(record.movedFill.transform, "Take the counter's pendants down");
                record.movedFill.transform.position = record.fillWasAt;
                Undo.RecordObject(record.movedFill, "Take the counter's pendants down");
                record.movedFill.intensity = record.fillWasIntensity;
                record.movedFill.range = record.fillWasRange;
            }
            CafeViewMode mode = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            if (mode != null)
            {
                Undo.RecordObject(mode, "Take the counter's pendants down");
                mode.overheadFixtures = mode.overheadFixtures.Where(r => r != null && !r.transform.IsChildOf(group)).ToArray();
                EditorUtility.SetDirty(mode);
            }
            CafeDaylight daylight = Object.FindAnyObjectByType<CafeDaylight>(FindObjectsInactive.Include);
            if (daylight != null)
            {
                Undo.RecordObject(daylight, "Take the counter's pendants down");
                daylight.warmLights = daylight.warmLights.Where(l => l != null && !l.transform.IsChildOf(group)).ToArray();
                EditorUtility.SetDirty(daylight);
            }
            Undo.DestroyObjectImmediate(group.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cafe.gameObject.scene);
            Keep(null, "The counter's pendants are down and the fill is back behind the middle section. Save the scene.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking the pendants down FAILED: " + e.Message + "\n" + e);
            Keep(null, "Taking the pendants down FAILED: " + e.Message + "\n" + e);
        }
    }

    static readonly (string name, Vector3 from, Vector3 to, float fov)[] LightViews =
    {
        ("door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.6f, 12f), 72f),
        ("queue", new Vector3(1.2f, 1.6f, 10.6f), new Vector3(-.6f, 1.55f, 14.6f), 70f),
    };

    static void ShootCounter(string folder, string state)
    {
        foreach (var v in CounterViews.Concat(LightViews)) Snap(Path.Combine(folder, $"{v.name}-{state}.png"), v.from, v.to, v.fov, false);
        GameObject shop = GameObject.Find("CmShopCam");
        if (shop != null)
            Snap(Path.Combine(folder, $"game-{state}.png"), shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
    }

    // The warm light (the café's own point lights, as placed in the scene) reaching a surface: the inverse square, the
    // light's angle to the surface, faded to nothing at its range as URP's point lights do. Relative, to compare sections.
    static float WarmOn(Vector3 at, Vector3 normal)
    {
        float sum = 0f;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (!l.enabled || l.type != LightType.Point) continue;
            Vector3 p = l.transform.position;
            if (!CafeDaylight.CafeInside.Contains(new Vector2(p.x, p.z))) continue;
            Vector3 d = p - at;
            float dist = d.magnitude;
            if (dist >= l.range || dist < .01f) continue;
            float cos = Mathf.Max(0f, Vector3.Dot(normal, d / dist));
            float fade = Mathf.Clamp01(1f - Mathf.Pow(dist / l.range, 4f));
            sum += l.intensity * cos * fade * fade / Mathf.Max(.25f, dist * dist);
        }
        return sum;
    }

    // One pendant out of a group of identical pendants combined per material (the joinery's PendantFixtures): the triangles
    // within 0.9 m (across) of the one nearest `near`, in world axes (the joinery is mirrored: the winding is turned back),
    // with its pivot at the top of the pendant, over its middle. One submesh per material, in the renderers' order.
    static Mesh OnePendant(Transform fixtures, Vector3 near, out Material[] materials, out float top, out float rim, StringBuilder report)
    {
        var renderers = fixtures.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("PendantFixtures has no meshes.");
        var parts = new List<(Material material, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)>();
        foreach (MeshRenderer r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            ReadMesh(mf, out Vector3[] verts, out Vector3[] norms, out Vector2[] uvs, out int[][] subs, report);
            Matrix4x4 T = r.transform.localToWorldMatrix, N = T.inverse.transpose;
            bool flip = T.determinant < 0f;
            for (int s = 0; s < subs.Length; s++)
            {
                Material material = r.sharedMaterials.Length > 0 ? r.sharedMaterials[Mathf.Min(s, r.sharedMaterials.Length - 1)] : null;
                var map = new Dictionary<int, int>();
                var part = (material, new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                int[] tri = subs[s];
                for (int k = 0; k + 2 < tri.Length; k += 3)
                {
                    Vector3 a = T.MultiplyPoint3x4(verts[tri[k]]), b = T.MultiplyPoint3x4(verts[tri[k + 1]]), c = T.MultiplyPoint3x4(verts[tri[k + 2]]);
                    Vector3 mid = (a + b + c) / 3f;
                    if (new Vector2(mid.x - near.x, mid.z - near.z).magnitude > .9f) continue;
                    int[] order = flip ? new[] { tri[k], tri[k + 2], tri[k + 1] } : new[] { tri[k], tri[k + 1], tri[k + 2] };
                    foreach (int index in order)
                    {
                        if (!map.TryGetValue(index, out int to))
                        {
                            to = part.Item2.Count;
                            map[index] = to;
                            part.Item2.Add(T.MultiplyPoint3x4(verts[index]));
                            part.Item3.Add(norms != null && norms.Length > index ? N.MultiplyVector(norms[index]).normalized : Vector3.up);
                            part.Item4.Add(uvs != null && uvs.Length > index ? uvs[index] : Vector2.zero);
                        }
                        part.Item5.Add(to);
                    }
                }
                if (part.Item5.Count > 0) parts.Add(part);
            }
        }
        if (parts.Count == 0) throw new InvalidOperationException($"No pendant within 0.9 m of ({near.x:0.00}, {near.z:0.00}) in PendantFixtures.");
        var every = parts.SelectMany(p => p.v).ToList();
        float minX = every.Min(v => v.x), maxX = every.Max(v => v.x), minZ = every.Min(v => v.z), maxZ = every.Max(v => v.z);
        top = every.Max(v => v.y);
        rim = every.Min(v => v.y);
        var pivot = new Vector3((minX + maxX) * .5f, top, (minZ + maxZ) * .5f);
        report.AppendLine($"Cut out of PendantFixtures: the pendant over ({pivot.x:0.00}, {pivot.z:0.00}), {maxX - minX:0.00} x {maxZ - minZ:0.00} m across.");
        var mesh = new Mesh { name = "Counter pendant (one of the tables')" };
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv0 = new List<Vector2>();
        var triangles = new List<int[]>();
        foreach (var p in parts)
        {
            int offset = vertices.Count;
            vertices.AddRange(p.v.Select(v => v - pivot));
            normals.AddRange(p.n);
            uv0.AddRange(p.uv);
            triangles.Add(p.tri.Select(t => t + offset).ToArray());
        }
        if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.subMeshCount = triangles.Count;
        for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateBounds();
        materials = parts.Select(p => p.material).ToArray();
        return mesh;
    }

    // A mesh's data in the editor: straight from the mesh if it's readable or the editor's read-only copy allows; otherwise
    // with Read/Write switched on in its model's import settings for the moment it takes (then switched back).
    static void ReadMesh(MeshFilter mf, out Vector3[] verts, out Vector3[] norms, out Vector2[] uvs, out int[][] subs, StringBuilder report)
    {
        Mesh m = mf.sharedMesh;
        if (m.isReadable)
        {
            verts = m.vertices; norms = m.normals; uvs = m.uv;
            subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s)).ToArray();
            return;
        }
        try
        {
            using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(m))
            {
                Mesh.MeshData d = data[0];
                var v = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp);
                d.GetVertices(v); verts = v.ToArray(); v.Dispose();
                norms = null; uvs = null;
                if (d.HasVertexAttribute(VertexAttribute.Normal)) { var n = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp); d.GetNormals(n); norms = n.ToArray(); n.Dispose(); }
                if (d.HasVertexAttribute(VertexAttribute.TexCoord0)) { var u = new NativeArray<Vector2>(d.vertexCount, Allocator.Temp); d.GetUVs(0, u); uvs = u.ToArray(); u.Dispose(); }
                subs = new int[d.subMeshCount][];
                for (int s = 0; s < d.subMeshCount; s++)
                {
                    var idx = new NativeArray<int>(d.GetSubMesh(s).indexCount, Allocator.Temp);
                    d.GetIndices(idx, s);
                    subs[s] = idx.ToArray();
                    idx.Dispose();
                }
            }
            if (verts.Length > 0) return;
        }
        catch (Exception e) { report.AppendLine($"  (read-only copy of {m.name} refused: {e.Message}; reading with Read/Write on for a moment)"); }
        string path = AssetDatabase.GetAssetPath(m);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException($"{m.name} isn't readable and isn't from a model ({path}).");
        bool was = importer.isReadable;
        try
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            Mesh fresh = mf.sharedMesh;
            verts = fresh.vertices; norms = fresh.normals; uvs = fresh.uv;
            subs = Enumerable.Range(0, fresh.subMeshCount).Select(s => fresh.GetTriangles(s)).ToArray();
        }
        finally
        {
            importer.isReadable = was;
            importer.SaveAndReimport();
        }
    }

    [MenuItem(Menu + "2 - Photograph the counter with and without its warm fills (read-only)", true)]
    [MenuItem(Menu + "2b - List what stands at the counter (read-only)", true)]
    [MenuItem(Menu + "2c - Why the front sofa's seat A can't be served (read-only)", true)]
    [MenuItem(Menu + "3 - Playtest 3: put the junk away (scene)", true)]
    [MenuItem(Menu + "3 - Undo: bring the junk back (scene)", true)]
    [MenuItem(Menu + "3b - Gather the retail in the intake's cubbies (scene)", true)]
    [MenuItem(Menu + "4 - Light the counter: pendants over the front counter (scene)", true)]
    [MenuItem(Menu + "4 - Undo: take the counter's pendants down (scene)", true)]
    [MenuItem(Menu + "5 - Serve the front sofa's seat A: the lounge's waiting spot to the waiting nook (scene)", true)]
    [MenuItem(Menu + "6 - Switch off the stand-ins still wearing the default material (scene)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== helpers

    const float CloseUpFov = 50f;

    static Transform Cafe()
    {
        GameObject cafe = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
        if (cafe == null) throw new InvalidOperationException($"'{CafeRootName}' isn't in the open scene.");
        return cafe.transform;
    }

    static Vector3 Centre(Transform t)
    {
        Renderer[] parts = t.GetComponentsInChildren<Renderer>(false);
        if (parts.Length == 0) return t.position;
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        return b.center;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] parts = go.GetComponentsInChildren<Renderer>(true);
        if (parts.Length == 0) return new Bounds(go.transform.position, Vector3.one * .2f);
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        return b;
    }

    // A view of a piece from the room's side: 2.4 m out toward the middle of the room, a little above it; and the ring's
    // radius in the photo (pixels on a 1440 x 900 image), its bounds' size seen from there, with a margin.
    static (Vector3 from, Vector3 to) CloseUp(GameObject go, out float ring)
    {
        Bounds b = BoundsOf(go);
        Vector3 toRoom = RoomMiddle - b.center;
        toRoom.y = 0f;
        if (toRoom.sqrMagnitude < .01f) toRoom = Vector3.back;
        float distance = 2.4f + b.extents.magnitude;
        Vector3 from = b.center + toRoom.normalized * distance + Vector3.up * .9f;
        float seen = Vector3.Distance(from, b.center);
        ring = Mathf.Clamp(b.extents.magnitude / seen / Mathf.Tan(CloseUpFov * .5f * Mathf.Deg2Rad) * 450f * 1.25f, 40f, 420f);
        return (from, b.center);
    }

    // A red ring round the middle of a photo (where its camera looked).
    static void Ring(string path, float radius)
    {
        if (!File.Exists(path)) return;
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path))) return;
            int w = texture.width, h = texture.height;
            float cx = w * .5f, cy = h * .5f;
            var red = new Color(.9f, .15f, .12f);
            for (int step = 0; step < 1440; step++)
            {
                float a = step / 1440f * Mathf.PI * 2f;
                for (float k = -2.5f; k <= 2.5f; k += .5f)
                {
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(a) * (radius + k)), y = Mathf.RoundToInt(cy + Mathf.Sin(a) * (radius + k));
                    if (x >= 0 && x < w && y >= 0 && y < h) texture.SetPixel(x, y, red);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
    }

    static readonly (string name, Vector3 from, Vector3 to, float fov)[] CounterViews =
    {
        ("front", new Vector3(-.6f, 1.55f, 8.6f), new Vector3(-.6f, .75f, 13.6f), 66f),
        ("above", new Vector3(-.3f, 6.2f, 8.4f), new Vector3(-.3f, 1.0f, 14.0f), 58f),
    };

    // A photo with Ace's stand-in capsule hidden (CafeFurnishing.Photograph does the same).
    static void Snap(string path, Vector3 from, Vector3 to, float fov, bool isometric)
    {
        var hidden = new List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try { CafeSecondPassSteps.Capture(path, from, to, fov, isometric); }
        finally { foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false; }
    }

    static string Folder(string label)
    {
        string folder = Path.Combine(CafeFurnishingCatalog.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    // Each menu's last word, kept as a file as well as in the Console: Logs/CafeFurnishing/cafe-tidy-last.txt, and the
    // run's report.txt beside its photos.
    static void Keep(string folder, string text)
    {
        try
        {
            Directory.CreateDirectory(CafeFurnishingCatalog.LogRoot);
            File.WriteAllText(Path.Combine(CafeFurnishingCatalog.LogRoot, "cafe-tidy-last.txt"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Couldn't write the report: " + e.Message); }
    }

    // Names that can only be the piece meant, looked for anywhere in the café if not in their group (Night 0's back door
    // moved the loose sack; a group's name may repeat). "Crate" and "Cable coil" are not: the bench keeps its own.
    static bool Distinct(string name) =>
        name.StartsWith("Coffee sack", StringComparison.Ordinal) || name.StartsWith("Back of house - ", StringComparison.Ordinal) ||
        name.StartsWith("Drinks - ", StringComparison.Ordinal) || name.StartsWith("Counter - ", StringComparison.Ordinal);

    // The first "n/m" in a check's result (paths complete, positions served); -1 if there is none.
    static int Reached(string result)
    {
        var m = System.Text.RegularExpressions.Regex.Match(result ?? "", @"(\d+)/(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    static string Safe(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }
}
#endif
