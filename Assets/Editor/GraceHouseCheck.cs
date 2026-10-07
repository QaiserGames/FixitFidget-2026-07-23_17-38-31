#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// BREAK-INS, CHUNK A: THE CHECK OF GRACE'S HOUSE (read-only; claude/break-ins-spec.md section 12)
//
//   Fixit Fidget > Night > Break-ins 2 - Check Grace's house (read-only, photos)
//
// Before the build it only photographs her house from the street (the "before" pictures). After it:
//   * her door: 1.30 m, its hinge, its leaf and its thin collider, the dark hall put away, the hall marker at
//     the foot of her stairs, the other markers where they were;
//   * her rooms: the pieces, the walls in two parts, the ceilings, the lamps (off in the scene);
//   * Ace's capsule (radius 0.35 and a 3.5 cm skin since playtest 3: 0.77 m across; it was 0.5 and 8 cm, 1.16 m) stands clear at every place Ace needs
//     to get to on both floors and on both flights, and sweeps clear along the flat ways between them;
//   * photos: the street by day (to compare with the "before" ones: only her door and window change), her
//     rooms from the street side as the break-ins show them (each floor), and a look in first person.
// Nothing is changed: every renderer hidden for a photo is put back at once. Logs/Night/grace-house-<time>/.
internal static class GraceHouseCheck
{
    const string Tag = "[Break-ins] ";
    const float Radius = PlayerMovement.CapsuleRadius, Skin = PlayerMovement.CapsuleSkin, Height = PlayerMovement.StandingHeight;

    // Where Ace must be able to stand: plan X, Y and the floor's height there (the flights: on the ramp, 39.8°).
    // The stops and the ways between them follow the walkable space worked out from the pieces' real bounds
    // (the capsule's 0.58 clear of everything, with a few cm to spare), after the first build's check.
    static readonly (float X, float Y, float z, bool ramp, string what)[] Spots =
    {
        (1.11f, 3.35f, 0f, false, "the entry, inside her door"),
        (0.72f, 3.05f, 0f, false, "the foot of the stairs"),
        (2.45f, 2.75f, 0f, false, "in the front room, by her armchair"),
        (2.22f, 2.08f, 0f, false, "in front of the cupboard under the stairs"),
        (3.20f, 1.30f, 0f, false, "at the cups in the kitchen"),
        (4.65f, 1.30f, 0f, false, "at the fridge"),
        (0.70f, 2.20f, (2.86f - 2.20f) * 1.2f / 1.44f, true, "on the lower flight"),
        (0.70f, 0.72f, 1.2f, false, "on the landing"),
        (2.00f, 0.70f, 1.2f + (2.00f - 1.14f) * 1.2f / 1.44f, true, "on the upper flight"),
        (3.05f, 0.85f, 2.4f, false, "on the landing upstairs"),
        (3.05f, 1.55f, 2.4f, false, "in the bedroom's doorway"),
        (2.90f, 2.10f, 2.4f, false, "just inside the bedroom"),
        (2.75f, 3.25f, 2.4f, false, "at the foot of the bed"),
        (1.25f, 3.62f, 2.4f, false, "at the wardrobe and the dressing table"),
    };

    // Flat ways between them that the capsule must sweep along.
    static readonly (float X0, float Y0, float X1, float Y1, float z, string what)[] Ways =
    {
        (1.11f, 3.35f, 2.06f, 3.20f, 0f, "the entry, north past the foot of the stairs"),
        (2.06f, 3.20f, 2.45f, 2.75f, 0f, "into the front room"),
        (2.45f, 2.75f, 2.22f, 2.08f, 0f, "to the cupboard under the stairs"),
        (2.22f, 2.08f, 3.00f, 2.02f, 0f, "round toward the kitchen"),
        (3.00f, 2.02f, 3.28f, 1.50f, 0f, "past the cupboard's corner"),
        (3.28f, 1.50f, 3.20f, 1.30f, 0f, "to the cups"),
        (3.20f, 1.30f, 4.65f, 1.30f, 0f, "along the kitchen to the fridge"),
        (3.20f, 1.30f, 3.20f, 1.85f, 0f, "out of the kitchen"),
        (3.20f, 1.85f, 2.45f, 2.30f, 0f, "back past her armchair"),
        (2.45f, 2.30f, 2.30f, 2.95f, 0f, "back across the front room"),
        (2.30f, 2.95f, 1.90f, 3.25f, 0f, "back into the entry"),
        (1.90f, 3.25f, 0.95f, 3.30f, 0f, "along the entry"),
        (0.95f, 3.30f, 0.72f, 3.05f, 0f, "to the foot of the stairs"),
        (1.11f, 3.35f, 0.72f, 3.05f, 0f, "the entry to the foot of the stairs"),
        (2.95f, 0.72f, 3.05f, 0.85f, 2.4f, "the top of the stairs onto the landing"),
        (3.05f, 0.85f, 3.05f, 1.55f, 2.4f, "the landing to the bedroom's doorway"),
        (3.05f, 1.55f, 2.90f, 2.10f, 2.4f, "through the doorway"),
        (2.90f, 2.10f, 2.80f, 2.40f, 2.4f, "past the corner of the bed"),
        (2.80f, 2.40f, 2.65f, 2.90f, 2.4f, "into the room"),
        (2.65f, 2.90f, 2.75f, 3.25f, 2.4f, "to the foot of the bed"),
        (2.75f, 3.25f, 1.85f, 3.42f, 2.4f, "across the room"),
        (1.85f, 3.42f, 1.50f, 3.58f, 2.4f, "toward the south bay"),
        (1.50f, 3.58f, 1.25f, 3.62f, 2.4f, "to the wardrobe"),
        (1.85f, 3.42f, 2.30f, 3.05f, 2.4f, "back toward the door"),
        (2.30f, 3.05f, 2.80f, 2.40f, 2.4f, "back past the corner of the bed"),
    };

    [MenuItem("Fixit Fidget/Night/Break-ins 2 - Check Grace's house (read-only, photos)")]
    static void Check()
    {
        var report = new StringBuilder();
        int problems = 0;
        void Line(bool ok, string what) { if (!ok) problems++; report.AppendLine((ok ? "ok       " : "PROBLEM  ") + what); }
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "grace-house-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Transform house = StreetDoorSteps.FindOptional("1 - Saffron bay-window house");
            if (house == null) throw new InvalidOperationException("Her house isn't in the scene.");
            StreetDoor door = house.Find(StreetDoorSteps.DoorName) != null ? house.Find(StreetDoorSteps.DoorName).GetComponent<StreetDoor>() : null;
            Transform rootT = house.Find(GraceHouseSteps.RootName);
            GraceHouse built = rootT != null ? rootT.GetComponent<GraceHouse>() : null;
            report.AppendLine($"Break-ins 2 - Check Grace's house, {DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}: " +
                              (built != null ? "built inside" : "NOT built inside yet (these are the 'before' photos)") + "\n");

            // ---- the street by day ----
            if (door != null)
            {
                Vector3 d = door.DoorwayPoint, o = door.Outward;
                Vector3 along = Vector3.Cross(Vector3.up, o).normalized;
                Vector3 front = house.TransformPoint(new Vector3(.4f, 1.6f, 0f));
                NightWalkSteps.Capture(Path.Combine(folder, "street-1-her-house.png"), front + o * 11f + Vector3.up * 2.2f - along * 1.5f, front + Vector3.up * .8f, 52f, false);
                NightWalkSteps.Capture(Path.Combine(folder, "street-2-door-and-window.png"), d + o * 4.6f + Vector3.up * 1.7f + along * 1.0f, d + Vector3.up * 1.1f + along * 1.0f, 50f, false);
                NightWalkSteps.Capture(Path.Combine(folder, "street-3-from-the-corner.png"), d + o * 7f + Vector3.up * 3.2f - along * 6.5f, d + Vector3.up * 1.2f + along * 1.2f, 45f, false);
                report.AppendLine("Photos of her house from the street by day: street-1, street-2, street-3.");
            }
            if (built == null)
            {
                report.AppendLine("\nNothing more to check until Break-ins 1 - Build Grace's house inside has run.");
                Finish(folder, report, problems);
                return;
            }

            // ---- her door ----
            MeshFilter leaf = door != null && door.hinge != null ? door.hinge.GetComponentInChildren<MeshFilter>(true) : null;
            BoxCollider leafBox = leaf != null ? leaf.GetComponent<BoxCollider>() : null;
            Line(door != null && Mathf.Abs(door.transform.localPosition.x - .95f) < .005f, $"her door's hinge is at x {(door != null ? door.transform.localPosition.x : 0f):0.000} (0.950)");
            Line(leaf != null && Mathf.Abs(leaf.sharedMesh.bounds.size.x - 1.30f) < .01f, $"her door leaf is {(leaf != null ? leaf.sharedMesh.bounds.size.x : 0f):0.000} m wide (1.30)");
            Line(leafBox != null && leafBox.size.z <= .06f && Mathf.Abs(leafBox.size.x - 1.30f) < .01f, "the leaf's collider is its back 5 cm, 1.30 m across (open, it leaves the doorway clear)");
            Transform hall = house.Find(StreetDoorSteps.HallName);
            Line(hall == null || !hall.gameObject.activeSelf, "the dark hall is put away");
            if (door != null && door.hall != null)
            {
                Vector3 p = built.Plan(door.hall.position);
                Line(Mathf.Abs(p.x - .95f) < .02f && Mathf.Abs(p.y - 2.95f) < .02f, $"people going in stop at the foot of her stairs, plan ({p.x:0.00}, {p.y:0.00})");
                Vector3 m = built.Plan(door.doorway.position);
                Line(Mathf.Abs(m.x - 1.11f) < .02f, $"the doorway's middle is where it was, plan X {m.x:0.00} (1.11)");
            }
            Line(built.changedParts.Length == 6 && built.meshesBefore.All(x => x != null), $"{built.changedParts.Length} of the house's meshes changed, each with its original kept for Take Grace's house back out");

            // ---- her rooms ----
            int renderers = rootT.GetComponentsInChildren<Renderer>(true).Length;
            int colliders = rootT.GetComponentsInChildren<Collider>(true).Count(c => c.enabled && !c.isTrigger);
            int sliding = built.walls.Count(w => w.upper != null);
            Line(built.walls.Length >= 30 && sliding >= 25, $"her rooms: {renderers} pieces, {colliders} colliders, {built.walls.Length} walls ({sliding} can slide down)");
            Line(built.ceilings.Length >= 4, $"{built.ceilings.Length} ceilings hide in the overhead view while Ace is inside");
            Line(built.nightLights.Length == 3 && built.nightLights.All(l => l != null && !l.enabled), $"{built.nightLights.Length} lamps, off in the scene (on at night)");
            Line(built.groundFloor != null && built.firstFloor != null, "the two floors are there");
            Line(built.walls.All(w => w.upper == null || w.upper.GetComponent<Renderer>() != null), "every sliding wall part draws");

            // ---- Ace's capsule ----
            Physics.SyncTransforms();
            report.AppendLine();
            report.AppendLine($"Where Ace needs to stand (Ace's capsule, {2f * (Radius + Skin):0.00} m across with its skin):");
            foreach (var s in Spots)
            {
                Vector3 feet = built.World(s.X, s.Y, s.z);
                string hit = Blocked(feet, s.ramp ? RampLift : StepLift);
                Line(hit == null, $"  {s.what}, plan ({s.X:0.00}, {s.Y:0.00}){(hit != null ? " - touches " + hit : "")}");
            }
            report.AppendLine("The flat ways between them:");
            foreach (var w in Ways)
            {
                string hit = Swept(built.World(w.X0, w.Y0, w.z), built.World(w.X1, w.Y1, w.z));
                Line(hit == null, $"  {w.what}{(hit != null ? " - runs into " + hit : "")}");
            }
            // Up the flights a step at a time: the capsule resting on each ramp must touch nothing, the floor over the
            // entry and the wall on it included (headroom at the foot of the stairs).
            report.AppendLine("Up her stairs (the capsule resting on the ramps, every 10-20 cm):");
            var lower = new List<string>();
            for (float y = 2.80f; y >= 1.55f; y -= .10f)
            {
                string hit = Blocked(built.World(.70f, y, (2.86f - y) * 1.2f / 1.44f), RampLift);
                if (hit != null) lower.Add($"Y {y:0.00}: {hit}");
            }
            Line(lower.Count == 0, "  the lower flight, from its foot to the landing" + (lower.Count > 0 ? " - touches at " + string.Join("; ", lower.Take(4)) : ""));
            var upper = new List<string>();
            for (float x = 1.25f; x <= 2.50f; x += .15f)
            {
                string hit = Blocked(built.World(x, .70f, 1.2f + (x - 1.14f) * 1.2f / 1.44f), RampLift);
                if (hit != null) upper.Add($"X {x:0.00}: {hit}");
            }
            Line(upper.Count == 0, "  the upper flight, from the landing to the top" + (upper.Count > 0 ? " - touches at " + string.Join("; ", upper.Take(4)) : ""));

            // ---- photos inside ----
            report.AppendLine();
            Photos(built, house, folder, report);
            Finish(folder, report, problems);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Grace's house check FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem("Fixit Fidget/Night/Break-ins 2 - Check Grace's house (read-only, photos)", true)]
    static bool CanCheck() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Finish(string folder, StringBuilder report, int problems)
    {
        report.Insert(0, (problems == 0 ? "All clear." : problems + " problem(s).") + "\n");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = Tag + "Grace's house check: " + (problems == 0 ? "all clear. " : problems + " problem(s). ") + folder + "\n" + report;
        if (problems == 0) Debug.Log(line); else Debug.LogError(line);
    }

    // How far the capsule's round bottom is lifted off the floor under its middle: on the flat, a step's worth; on a
    // 39.8° flight, where a round bottom resting on the slope sits r / cos(39.8°) above it, not r.
    const float StepLift = .12f;
    static readonly float RampLift = (Radius + Skin) / Mathf.Cos(39.8f * Mathf.Deg2Rad) - (Radius + Skin) + .02f;

    // The capsule standing with its feet at feet (its skin 1 cm short): what it overlaps, or null.
    static string Blocked(Vector3 feet, float lift = StepLift)
    {
        float r = Radius + Skin - .01f;
        Vector3 bottom = feet + Vector3.up * (r + lift), top = feet + Vector3.up * (Height - Radius);
        Collider[] hits = Physics.OverlapCapsule(bottom, top, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        return hits.Length == 0 ? null : string.Join(", ", hits.Select(h => h.name).Distinct().Take(4));
    }

    static string Swept(Vector3 from, Vector3 to)
    {
        float r = Radius + Skin - .01f;
        Vector3 bottom = from + Vector3.up * (r + StepLift), top = from + Vector3.up * (Height - Radius);
        Vector3 d = to - from;
        if (Physics.CapsuleCast(bottom, top, r, d.normalized, out RaycastHit hit, d.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.collider.name;
        return Blocked(from) ?? Blocked(to);
    }

    // Her rooms as the break-ins show them: the shell out of the way, the walls toward the camera down to sill
    // height, the floor above hidden, the ceilings off. Everything is put back after each photo.
    static void Photos(GraceHouse built, Transform house, string folder, StringBuilder report)
    {
        var shell = house.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(built.transform)).ToList();
        var upstairs = built.firstFloor.GetComponentsInChildren<Renderer>(true).ToList();
        Vector3 Eye(Vector3 focus, float yaw, float pitch, float distance) => focus - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
        void Shot(string name, int storey, Vector3 focus, float yaw, float pitch, float distance)
        {
            Vector3 eye = Eye(focus, yaw, pitch, distance);
            var hidden = new List<Renderer>(shell);
            if (storey == 0) hidden.AddRange(upstairs);
            hidden.AddRange(built.ceilings);
            foreach (GraceHouse.Wall w in built.walls)
            {
                if (w.upper == null || w.storey != storey || !w.perimeter) continue;
                Vector3 centre = w.lower != null ? w.lower.bounds.center : w.upper.position;
                if (Vector3.Dot(eye - centre, built.transform.TransformDirection(w.outward)) > 0f)
                {
                    hidden.Add(w.upper.GetComponent<Renderer>());
                    hidden.AddRange(w.hanging);
                }
            }
            var was = hidden.Where(r => r != null).Distinct().Select(r => (r, r.forceRenderingOff)).ToList();
            try
            {
                foreach (var (r, _) in was) r.forceRenderingOff = true;
                NightWalkSteps.Capture(Path.Combine(folder, name), eye, focus, 40f, false);
            }
            finally
            {
                foreach (var (r, off) in was) r.forceRenderingOff = off;
            }
        }
        Vector3 downstairs = built.World(2.7f, 2.0f, .8f), up = built.World(2.7f, 2.6f, 3.2f);
        Shot("inside-1-ground-floor.png", 0, downstairs, built.houseYaw, built.housePitch, built.houseDistance);
        Shot("inside-2-first-floor.png", 1, up, built.houseYaw, built.housePitch, built.houseDistance);
        Shot("inside-3-ground-floor-from-the-south.png", 0, downstairs, built.houseYaw + 45f, 55f, 12f);
        Shot("inside-4-first-floor-from-the-north.png", 1, up, built.houseYaw - 40f, 58f, 11f);
        Shot("inside-5-the-stairs.png", 0, built.World(1.2f, 1.6f, 1.0f), built.houseYaw + 20f, 45f, 7.5f);
        // In first person: from the entry toward the kitchen, and from the bedroom's doorway.
        NightWalkSteps.Capture(Path.Combine(folder, "first-person-1-from-the-entry.png"), built.World(1.1f, 3.6f, 1.65f), built.World(3.6f, 1.0f, 1.0f), 65f, false);
        NightWalkSteps.Capture(Path.Combine(folder, "first-person-2-into-the-bedroom.png"), built.World(3.05f, 1.2f, 2.4f + 1.65f), built.World(2.2f, 3.6f, 2.4f + .9f), 65f, false);
        report.AppendLine("Photos inside (by day, the lamps off): inside-1 to inside-5 as the break-ins show them from the street side, " +
                          "first-person-1 and -2 as Ace sees them.");
    }
}
#endif
