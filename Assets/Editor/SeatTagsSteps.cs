#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// SEAT TAGS 1: WHAT EACH OF THE CAFÉ'S SEATS IS, AND THE ROOM'S WINDOWS, DOOR AND QUIET CORNER (23 Oct 2026)
//
// His note (22 Oct): drink customers "only sit to the left of the couch", repair customers "only sit on the right next to
// the bar stool": "I want that to be more spread out and diverse... a feel for the player to like kind of look for the
// customer that did the order". People choose seats by their reasons now (SeatChoice, in SeatTags.cs): their character's
// leanings for what a seat is and where it stands. This step says so for the café as it is built:
//
//   * WHAT EACH SEAT IS: a SeatTags on the seat, by the zone the step that laid it named it for (the names below), with the
//     zone's name for the labs and reports:
//       repair bar (3, Seats 1)        Bar        the bar stools at the front counter, across from the bench
//       west banquette (6, Seats 1)    Lounge     the built-in banquette's cushion; the café chairs across it: none
//       communal table (8, Seats 1)    Communal   the low stools at the long shared table
//       bistro (4, Seats 1)            none       the spindle chairs at the two bistro 2-tops
//       window counter (3, Seats 1)    Bar        the bar stools at the counter along the east window
//       reading corner (2, Lived in 2) Lounge     the tub chairs at its low table
//       lounge (2, Café dressing 3)    Lounge     the tub chairs at its low table, wherever that step measured it in
//     A seat it doesn't know (none today) keeps its own shape's tags (SeatTags.DefaultFor) and is listed in the report.
//   * WHERE EACH STANDS: the room's AREAS, a SeatTags with an Area each, under 30 - cafe redesign/Seat tags 1 - the room's
//     areas: by the west (street) window, by the east (courtyard) window, by the front window either side of the door
//     (Window); by the door (NearDoor); the quiet front-left corner (Quiet). A seat whose sit point is inside one has its
//     tags as well, so a table the player places later picks up where it stands without a step.
//   * THE RESULT, by zone: the repair bar Bar; the banquette's cushions Lounge + Window, its chairs Window; the communal
//     table Communal (its two south stools + NearDoor); the bistro none (its south 2-top NearDoor); the window counter
//     Bar + Window; the reading corner Lounge + Window + Quiet; the lounge Lounge. The report lists every seat. (The window
//     counter's stools are waited at standing until they sit, Snap To Seat off: SeatChoice.StandingWait keeps them for
//     overflow and the Officer's favourite, Bar + Window, asleep. The tags don't change when they sit.)
//
//   Fixit Fidget > Cafe redesign > Seat tags 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Seat tags 1 - Tag the seats and the room (scene, with Undo)
//   Fixit Fidget > Cafe redesign > Seat tags 1 - Undo (scene)
//
// Through the café redesign's shared step (CafeRedesignShared.Step): Edit Mode only; the survey first, which changes
// nothing; one undo group and the record on the café's root, so its Undo takes the components and the areas away again
// after a save; the checks after (every seat's tags read the way the game reads them, TableSeat.Tags, against the plan),
// and any FAIL puts everything back. Nothing is moved, switched off or deleted and nothing solid is added, so the routes
// aren't re-baked and the guards aren't run. Not saved: save the scene after. Report: Logs/CafeRedesign/seat-tags-1-<time>/.
//
// THE UNDO ORDER. The shared rule: only the latest of the redesign's steps may be undone. Art tidy 1 (23 Oct, the coats
// out) is on the same list: whichever of the two ran later is undone first (run Art tidy 1 first, then this, and this one
// is undone first).
// ---------------------------------------------------------------------------
internal static class SeatTagsSteps
{
    public const string Title = "Seat tags 1 - Tag the seats and the room";
    const string Label = "seat-tags-1";
    const string GroupName = "Seat tags 1 - the room's areas";
    const string Seats1 = CafeSeatsSteps.Title;      // "Seats 1 - The San Francisco seat plan": its record's name
    const string MenuCheck = Shared.Menu + "Seat tags 0 - Check first (read-only)";
    const string MenuDo = Shared.Menu + "Seat tags 1 - Tag the seats and the room (scene, with Undo)";
    const string MenuUndo = Shared.Menu + "Seat tags 1 - Undo (scene)";

    // ================================================================== menus

    [MenuItem(MenuCheck)]
    static void CheckFirst() => Shared.Check(Title, Label + "-check", run => Survey(run));

    [MenuItem(MenuDo)]
    static void TagTheSeats()
    {
        Plan plan = null;
        Shared.Step(Title, Label, run => plan = Survey(run), run => Change(run, plan), null, guards: false, rebake: false, roomPhotos: false);
    }

    [MenuItem(MenuUndo)]
    static void UndoTheTags() => Shared.UndoStep(Title, Label, rebake: false);

    [MenuItem(MenuCheck, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== what each seat is (by the name the step that laid it gave it)

    sealed class Rule
    {
        public readonly string starts, contains, zone, why;
        public readonly SeatTag tags;
        public Rule(string starts, string contains, SeatTag tags, string zone, string why)
        {
            this.starts = starts; this.contains = contains; this.tags = tags; this.zone = zone; this.why = why;
        }
        public bool Matches(string name) => name.StartsWith(starts, StringComparison.Ordinal)
                                            && (contains == null || name.IndexOf(contains, StringComparison.Ordinal) >= 0);
    }

    // First match wins (the banquette's cushion before its chairs). CafeSeatsSteps.MakeSeats, CafeLivedInSteps (the reading
    // corner) and CafeDressing3Steps (the lounge) name the seats this way.
    static readonly Rule[] Rules =
    {
        new Rule("Seat (repair bar,", null, SeatTag.Bar, "repair bar", "a bar stool at the front counter, across from the bench"),
        new Rule("Seat (banquette 2-top,", "on the banquette", SeatTag.Lounge, "west banquette", "the built-in banquette's cushion"),
        new Rule("Seat (banquette 2-top,", null, SeatTag.None, "west banquette", "the café chair across a banquette 2-top"),
        new Rule("Seat (communal table,", null, SeatTag.Communal, "communal table", "a low stool at the long shared table"),
        new Rule("Seat (bistro 2-top,", null, SeatTag.None, "bistro", "a spindle chair at a bistro 2-top"),
        new Rule("Seat (window counter,", null, SeatTag.Bar, "window counter", "a bar stool at the counter along the east window"),
        new Rule("Seat (reading corner,", null, SeatTag.Lounge, "reading corner", "a tub chair at the reading corner's low table"),
        new Rule("Seat (lounge,", null, SeatTag.Lounge, "lounge", "a tub chair at the lounge's low table (Café dressing 3)"),
    };

    // ================================================================== where each stands: the room's areas

    sealed class AreaPlan
    {
        public readonly string name, why;
        public readonly SeatTag tags;
        public readonly float x0, x1, z0, z1;
        public AreaPlan(string name, SeatTag tags, float x0, float x1, float z0, float z1, string why)
        {
            this.name = name; this.tags = tags; this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; this.why = why;
        }
        public Vector3 Centre => new Vector3((x0 + x1) * .5f, (AreaFloor + AreaTop) * .5f, (z0 + z1) * .5f);
        public Vector3 Size => new Vector3(x1 - x0, AreaTop - AreaFloor, z1 - z0);
        public bool Contains(Vector3 p) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1 && p.y >= AreaFloor && p.y <= AreaTop;
    }

    // The café's frame (Seats 1, Room 1): x from the west wall (-) to the east (+), z from the shop front (0; the door at
    // x 0, its lane x -1..1) to the counter (its customers' edge at z 13.3). The west (street) window's face is at x -7.40
    // (the banquette's back), the east (courtyard) window's at x +7.40 (CafeRoomSteps.EastFace). A seat's sit point is
    // 0.4-0.75 m up, so the areas run from under the floor to above a standing head.
    const float AreaFloor = -.5f, AreaTop = 2.5f;

    static readonly AreaPlan[] Areas =
    {
        new AreaPlan("Seat area - by the west (street) window", SeatTag.Window, -7.7f, -5.0f, -.3f, 11.0f,
            "the banquette's 2-tops, both sides of each table (cushions at x -7.12, chairs at -5.64), and the reading corner; " +
            "it stops at z 11, short of the repair bar's west stool (z 12.86), which is the counter's, not the window's"),
        new AreaPlan("Seat area - by the east (courtyard) window", SeatTag.Window, 5.9f, 7.7f, -.3f, 12.5f,
            "the window counter's stools (sit points at x 6.51); the lounge's chairs stand about 2 m in from the glass and aren't in it"),
        new AreaPlan("Seat area - by the front window, west of the door", SeatTag.Window, -7.7f, -1.2f, -.3f, 2.2f,
            "the shop front west of the door: the reading corner"),
        new AreaPlan("Seat area - by the front window, east of the door", SeatTag.Window, 1.2f, 7.7f, -.3f, 2.2f,
            "the shop front east of the door: the waiting nook today (no seats), a placed table later"),
        new AreaPlan("Seat area - by the door", SeatTag.NearDoor, -4.2f, 4.2f, -.3f, 5.3f,
            "the front of the room nearest the door: the south bistro 2-top (z 4.6) and the communal table's south stools (z 4.85)"),
        new AreaPlan("Seat area - the quiet corner", SeatTag.Quiet, -7.7f, -4.4f, -.3f, 3.3f,
            "the front-left corner, out of the way of the door, the till and the aisle: the reading corner"),
    };

    // ================================================================== the survey (changes nothing)

    sealed class Planned
    {
        public TableSeat seat;
        public Rule rule;               // null: a seat this step doesn't know (it keeps its own shape's tags)
        public SeatTags already;        // a SeatTags it already has (on it or a parent): left as it is
        public SeatTag areas;           // the room's areas its sit point will be in
        public SeatTag Own => already != null ? already.Tags : rule != null ? rule.tags : SeatTags.DefaultFor(seat);
        public SeatTag Final => Own | areas;
        public string Zone => rule != null && already == null ? rule.zone : SeatTags.ZoneOf(seat);
    }

    sealed class Plan
    {
        public readonly List<Planned> seats = new List<Planned>();
        public int Tagging => seats.Count(p => p.rule != null && p.already == null);
    }

    static Plan Survey(Shared.Run run)
    {
        var plan = new Plan();
        run.Require(Shared.Done(run, Seats1), $"'{Seats1}' hasn't run in this scene: these tags are for its seats.");
        run.Info("The redesign's steps in this scene: " + string.Join("; ", Shared.Records(run.Cafe).OrderBy(r => r.order).Select(r => $"{r.order}. {r.step}")) + ".");
        Transform redesign = run.Cafe.Find(Shared.RedesignGroup);
        Vector3 scale = (redesign != null ? redesign : run.Cafe).lossyScale;
        run.Require(Mathf.Abs(scale.x - 1f) < .001f && Mathf.Abs(scale.y - 1f) < .001f && Mathf.Abs(scale.z - 1f) < .001f,
            $"The café's redesign group is scaled {Shared.V(scale)}: the areas are laid in metres and would land wrong.");
        run.Require(redesign == null || run.Cafe.Find(Shared.RedesignGroup + "/" + GroupName) == null,
            $"'{Shared.RedesignGroup}/{GroupName}' is already in the scene (a half-undone earlier run?): remove it or use the Undo first.");

        // ---- the room's areas ----
        run.Line();
        run.Line("The room's areas (world metres, the café's frame; y " + F(AreaFloor) + ".." + F(AreaTop) + "):");
        foreach (AreaPlan a in Areas)
            run.Info($"{a.name}: x {F(a.x0)}..{F(a.x1)}, z {F(a.z0)}..{F(a.z1)} -> {a.tags}. {a.why}.");

        // ---- the seats ----
        TableSeat[] seats = Shared.InScene<TableSeat>(run, false).Where(s => s != null && s.enabled && s.gameObject.activeInHierarchy).ToArray();
        run.Require(seats.Length > 0, "The scene has no live seats (TableSeat).");
        foreach (TableSeat seat in seats.OrderBy(s => s.name, StringComparer.Ordinal))
        {
            var p = new Planned { seat = seat, rule = Rules.FirstOrDefault(r => r.Matches(seat.name)), already = SeatTags.On(seat) };
            foreach (AreaPlan a in Areas) if (a.Contains(seat.SeatPose.position)) p.areas |= a.tags;
            plan.seats.Add(p);
        }
        run.Line();
        run.Line($"The seats ({plan.seats.Count} live; the sit point; what it is + where it stands = its tags):");
        foreach (Planned p in plan.seats)
        {
            Vector3 at = p.seat.SeatPose.position;
            string source = p.already != null ? $"already tagged ({Shared.PathOf(p.already.transform)}): left as it is"
                : p.rule != null ? p.rule.why : "not one this step knows: its own shape's tags (SeatTags.DefaultFor)";
            run.Info($"{p.seat.name} at ({F(at.x)}, {F(at.z)}), zone '{p.Zone}': {p.Own} + {p.areas} = {p.Final}. {source}.");
        }
        foreach (var zone in plan.seats.GroupBy(p => p.Zone).OrderBy(g => g.Key, StringComparer.Ordinal))
            run.Info($"zone '{zone.Key}': {zone.Count()} seat(s), tagged {string.Join(" / ", zone.Select(p => p.Final.ToString()).Distinct())}");
        int unknown = plan.seats.Count(p => p.rule == null && p.already == null);
        if (unknown > 0) run.Info($"{unknown} seat(s) this step doesn't know keep their own shape's tags; name them in SeatTagsSteps.Rules to tag them.");
        run.Require(plan.Tagging > 0, "No seat to tag: every live seat is already tagged or one this step doesn't know.");
        run.Info($"To tag: {plan.Tagging} seat(s); {plan.seats.Count(p => p.already != null)} already tagged; {unknown} unknown. Areas to lay: {Areas.Length}.");
        run.Info("The undo order: only the latest of the redesign's steps may be undone. If Art tidy 1 (the coats out) runs after this, undo it first.");
        return plan;
    }

    // ================================================================== the change, and its checks

    static void Change(Shared.Run run, Plan plan)
    {
        // 1. What each seat is: a SeatTags on the seat (its Undo removes it again).
        foreach (Planned p in plan.seats)
        {
            if (p.rule == null || p.already != null) continue;
            SeatTags tags = Shared.AddComponent<SeatTags>(run, p.seat.gameObject);
            if (tags == null) { run.Check(false, $"{p.seat.name}: a SeatTags could be added"); continue; }
            var so = new SerializedObject(tags);
            so.FindProperty("tags").intValue = (int)p.rule.tags;
            so.FindProperty("zone").stringValue = p.rule.zone;
            so.FindProperty("area").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();             // a new component: its adding is the undo
        }

        // 2. Where each stands: the room's areas, one object each (made: its Undo deletes them).
        Transform group = Shared.StepGroup(run, GroupName);
        foreach (AreaPlan a in Areas)
        {
            var go = new GameObject(a.name);
            go.transform.SetParent(group, false);
            go.transform.SetPositionAndRotation(a.Centre, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            SeatTags area = go.AddComponent<SeatTags>();
            var so = new SerializedObject(area);
            so.FindProperty("tags").intValue = (int)a.tags;
            so.FindProperty("zone").stringValue = "";
            so.FindProperty("area").vector3Value = a.Size;
            so.ApplyModifiedPropertiesWithoutUndo();
            Shared.Made(run, go);
        }

        // 3. The checks: every seat's tags read the way the game reads them (TableSeat.Tags: its own, plus the areas now in the
        //    scene) against the plan, and every area where it was meant to go.
        foreach (AreaPlan a in Areas)
        {
            Transform t = group.Find(a.name);
            SeatTags area = t != null ? t.GetComponent<SeatTags>() : null;
            run.Check(area != null && area.IsArea && area.Tags == a.tags && area.Contains(a.Centre)
                      && !area.Contains(a.Centre + new Vector3(a.Size.x * .5f + .05f, 0f, 0f)),
                $"{a.name}: an area of {Shared.V(a.Size)} m at {Shared.V(a.Centre)}, tagged {a.tags}");
        }
        var counts = new Dictionary<SeatTag, int>();
        foreach (Planned p in plan.seats)
        {
            SeatTag read = p.seat.Tags;
            run.Check(read == p.Final, $"{p.seat.name}: tagged {read} (planned {p.Final}), zone '{p.seat.Zone}'");
            foreach (SeatTag one in new[] { SeatTag.Window, SeatTag.Lounge, SeatTag.Bar, SeatTag.Communal, SeatTag.Quiet, SeatTag.NearDoor })
                if ((read & one) != 0) counts[one] = (counts.TryGetValue(one, out int n) ? n : 0) + 1;
        }
        run.Check(plan.seats.Where(p => p.seat.RepairBar).All(p => (p.seat.Tags & SeatTag.Bar) != 0 && (p.seat.Tags & SeatTag.Window) == 0),
            "every repair-bar stool is tagged Bar and not Window (the Officer's favourite, a stool at the window counter, Bar and Window, " +
            "must never land on the stools kept for repairs)");
        Shared.Note(run, $"Seat tags 1: {plan.Tagging} seats tagged, {Areas.Length} areas laid. Seats per tag: " +
                         string.Join(", ", counts.OrderBy(kv => (int)kv.Key).Select(kv => $"{kv.Key} {kv.Value}")) + ".");
    }

    static string F(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
}
#endif
