using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BREAK-INS' WALK CHECK (chunk A: claude/break-ins-spec.md section 12)
//
// Started by Fixit Fidget > Night > Break-ins 3 - Walk Grace's house by itself (lab, a check), in a lab
// session only (the playtest save is never touched). Ace's own capsule walks her house by itself, steered
// the way a player steers it (PlayerMovement's scripted input, through the same code as a key press):
// in at her door, the entry, the pocket by the cupboard, the kitchen to the fridge and back, up both
// flights, the landing, into the bedroom past the foot of the bed to the wardrobe, then all the way back
// down and out onto the pavement. For every leg: reached or not, how long it took, where it got stuck;
// on the way down, how far the capsule ever left the flight. Photos of the house view on the way.
// Report and photos: Logs/Night/grace-walk-<time>/. Play Mode stops by itself when it is done.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceHouseWalkCheck : MonoBehaviour
{
    // Plan metres (X north, Y to the street) and what the stop is; photo: take one there. The stops follow the
    // walkable space worked out from the pieces' real bounds (the same ways as Break-ins 2's check).
    static readonly (float X, float Y, string what, bool photo)[] Route =
    {
        (1.11f, 5.30f, "on the pavement at her stoop", false),
        (1.11f, 4.05f, "in her doorway", false),
        (1.11f, 3.35f, "in the entry, inside the door", true),
        (2.06f, 3.20f, "past the foot of the stairs", false),
        (2.45f, 2.75f, "in the front room, by her armchair", true),
        (2.22f, 2.08f, "in the pocket, at the cupboard under the stairs", true),
        (3.00f, 2.02f, "round toward the kitchen", false),
        (3.28f, 1.50f, "past the cupboard's corner", false),
        (3.20f, 1.30f, "in the kitchen, at the cups", true),
        (4.65f, 1.30f, "at the fridge", false),
        (3.20f, 1.30f, "back at the cups", false),
        (3.20f, 1.85f, "out of the kitchen", false),
        (2.45f, 2.30f, "back past her armchair", false),
        (2.30f, 2.95f, "back across the front room", false),
        (1.90f, 3.25f, "back in the entry", false),
        (0.95f, 3.30f, "along the entry", false),
        (0.72f, 3.05f, "at the foot of the stairs", false),
        (0.70f, 2.20f, "on the lower flight", false),
        (0.70f, 1.60f, "at the top of the lower flight", false),
        (0.70f, 0.72f, "on the landing", true),
        (1.30f, 0.70f, "at the foot of the upper flight", false),
        (2.20f, 0.70f, "on the upper flight", false),
        (2.95f, 0.72f, "at the top of the stairs", false),
        (3.05f, 0.85f, "on the landing upstairs, by the bathroom door", true),
        (3.05f, 1.55f, "in the bedroom's doorway", false),
        (2.90f, 2.10f, "just inside the bedroom", false),
        (2.80f, 2.40f, "past the corner of the bed", false),
        (2.65f, 2.90f, "in the room", false),
        (2.75f, 3.25f, "at the foot of the bed", true),
        (1.85f, 3.42f, "across the room", false),
        (1.50f, 3.58f, "toward the south bay", false),
        (1.25f, 3.62f, "at the wardrobe and the dressing table", true),
        (1.50f, 3.58f, "back from the wardrobe", false),
        (1.85f, 3.42f, "back across the room", false),
        (2.30f, 3.05f, "back toward the door", false),
        (2.80f, 2.40f, "back past the corner of the bed", false),
        (2.90f, 2.10f, "back at the bedroom's door", false),
        (3.05f, 1.55f, "back through the doorway", false),
        (3.05f, 0.85f, "on the landing upstairs", false),
        (2.95f, 0.72f, "at the top of the stairs", false),
        (2.20f, 0.70f, "going down the upper flight", false),
        (1.20f, 0.70f, "at the bottom of the upper flight", false),
        (0.70f, 0.90f, "on the landing", false),
        (0.70f, 1.80f, "going down the lower flight", false),
        (0.72f, 2.75f, "at the bottom of the stairs", false),
        (1.11f, 3.35f, "in the entry", false),
        (1.11f, 4.05f, "in her doorway, going out", false),
        (1.11f, 5.30f, "on the pavement again", true),
    };
    const float Reach = .22f;          // a stop counts as reached within this (metres, flat)
    const float LegSeconds = 9f;       // longest a leg may take
    const float StuckAfter = 1.2f;     // no progress for this long: stuck

    GraceHouse house;
    PlayerMovement mover;
    CafeViewMode view;
    CharacterController capsule;
    string folder;
    readonly StringBuilder report = new();
    int problems;

    void Start()
    {
        house = GetComponent<GraceHouse>();
        view = FindAnyObjectByType<CafeViewMode>();
        mover = view != null ? view.GetComponent<PlayerMovement>() : null;
        capsule = view != null ? view.GetComponent<CharacterController>() : null;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "grace-walk-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        StartCoroutine(Walk());
    }

    void Line(bool ok, string what)
    {
        if (!ok) problems++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    IEnumerator Walk()
    {
        report.AppendLine("Break-ins walk check: Ace's own capsule walks Grace's house by itself (" +
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
        if (house == null || mover == null || capsule == null)
        {
            Line(false, "Grace's house, Ace and Ace's walking are all there");
            Finish();
            yield break;
        }
        report.AppendLine($"Ace: radius {capsule.radius:0.00}, height {capsule.height:0.00}, skin {capsule.skinWidth:0.00}, step {capsule.stepOffset:0.00}, slope limit {capsule.slopeLimit:0}°.");
        yield return new WaitForSeconds(1.5f);   // the night settles, the camera arrives
        float started = Time.time;
        int photos = 0;
        float worstAir = 0f;
        string worstAirWhere = "";
        for (int i = 0; i < Route.Length; i++)
        {
            var stop = Route[i];
            Vector3 target = house.World(stop.X, stop.Y);
            float legStart = Time.time, bestAt = Time.time;
            float best = Flat(target - Ace).magnitude;
            string stuck = null;
            bool reached = false;
            float air = 0f, legAir = 0f;
            while (Time.time - legStart < LegSeconds)
            {
                Vector3 to = Flat(target - Ace);
                float d = to.magnitude;
                if (d <= Reach) { reached = true; break; }
                if (d < best - .03f) { best = d; bestAt = Time.time; }
                else if (Time.time - bestAt > StuckAfter) { stuck = $"no closer than {best:0.00} m for {StuckAfter:0.0} s at plan {PlanOf(Ace)}"; break; }
                // The way a player steers: a direction on screen, turned by the camera's yaw.
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, view.MovementYaw, 0f)) * (to / d);
                mover.ScriptedInput = new Vector2(local.x, local.z);
                // How far off the ground (the flights going down): time in the air, this leg.
                air = capsule.isGrounded ? 0f : air + Time.deltaTime;
                legAir = Mathf.Max(legAir, air);
                yield return null;
            }
            mover.ScriptedInput = Vector2.zero;
            float took = Time.time - legStart;
            if (legAir > worstAir) { worstAir = legAir; worstAirWhere = stop.what; }
            Line(reached, $"{i + 1,2}. {stop.what}: {(reached ? $"reached in {took:0.0} s" : stuck ?? $"not reached in {LegSeconds:0} s (at plan {PlanOf(Ace)})")}" +
                          $"{(legAir > .05f ? $", in the air {legAir:0.00} s at most" : "")}; " +
                          $"{(house.Viewing ? (house.Upstairs ? "inside, upstairs" : "inside") : "outside")}");
            if (!reached) break;
            // Through her door on the way in: wait for it to shut behind Ace before the entry's way north.
            if (stop.what == "in the entry, inside the door")
            {
                float wait = Time.time;
                while (house.door != null && !house.door.IsClosed && Time.time - wait < 3f) yield return null;
                Line(house.door == null || house.door.IsClosed, $"    her door shut behind Ace ({Time.time - wait:0.0} s)");
            }
            if (stop.photo)
            {
                yield return new WaitForSeconds(.9f);   // the camera turns, the walls settle
                string name = $"{++photos:00}-{Safe(stop.what)}.png";
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));
                yield return null;
                report.AppendLine($"      photo {name}");
            }
        }
        mover.ScriptedInput = null;
        report.AppendLine();
        Line(worstAir < .35f, $"Going down, the capsule stayed on the flights (longest in the air {worstAir:0.00} s, {worstAirWhere}; the stairs caught it {house.StairCatches} times)");
        report.AppendLine($"The whole walk took {Time.time - started:0.0} s.");
        Finish();
    }

    Vector3 Ace => capsule != null ? capsule.transform.position : transform.position;

    string PlanOf(Vector3 world)
    {
        Vector3 p = house.Plan(world);
        return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, z {2:0.00})", p.x, p.y, p.z);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Replace("--", "-").Trim('-');
    }

    void Finish()
    {
        report.Insert(0, (problems == 0 ? "All clear.\n" : problems + " problem(s).\n"));
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = "[Break-ins] Walk check: " + (problems == 0 ? "all clear. " : problems + " problem(s). ") + folder + "\n" + report;
        // A warning, not an error: with the Console's Error Pause on, an error would pause Play Mode here and it
        // would never stop by itself.
        if (problems == 0) Debug.Log(line); else Debug.LogWarning(line);
        StartCoroutine(Stop());
    }

    IEnumerator Stop()
    {
        yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
