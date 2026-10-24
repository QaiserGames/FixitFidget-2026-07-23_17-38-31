#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// ART TIDY 1: THE COATS OUT (23 Oct 2026; review23 spec C, part 1)
//
//   Fixit Fidget > Cafe redesign > Art tidy 1 - The coats out: off the chairs and the coat stand (scene, with Undo)
//   Fixit Fidget > Cafe redesign > Art tidy 1 - Undo: the coats back (scene)
//
// His note (22 Oct 2026, his own playtest): "I also noticed there's clothes on the on chairs in the cafe that just look like
// squares and rectangles together." What he saw: Lived in 2's two coats (13 Oct 2026, CafeLivedInSteps: LI_Coat_OnChair from
// Tools/Blender/cafe_livedin.py, a slate-blue chore coat whose body is one ribbon of flat lens-shaped sections and whose
// sleeves are six-sided tubes) hung over the backs of the two banquette café chairs people reach last. From the overhead
// they are two dark slate rectangles on the chairs (CafeLife photos, 20 Oct 2026); Art Direction already called them weak on
// 14 Oct 2026 ("a dark slate blob, about 20 x 12 px. Reads weakly") and the 19 Oct 2026 reviewers saw "two dark blocks at
// mid-height" that "hide their chairs entirely". The other clothes the dressing passes built are on the waiting nook's coat
// stand (CafeFurnishing, 24 Sept 2026): a scarf and Ace's tote, each made of plain boxes ("a coat rack with two squares", his
// playtest 3 note).
//
// What goes, switched off and recorded (never deleted; the night's set-up finds things by name):
//   * the two coats on the chairs, "LI_Coat_OnChair (seat ...)", each the child of its chair (the chairs stay);
//   * the coat stand's two boxes, its "Scarf" and its "Tote bag". The stand stays as it is: a 1.74 m post with a ball finial,
//     four angled pegs with brass knobs and a cross foot with brass caps reads as a coat stand from its shapes alone (its
//     feet are flat bars, as a real stand's are; its hat is two cylinders and a band, not boxes). It has been off since
//     Playtest 3's tidy (9 Oct 2026, Cafe furnishing > 3, recorded in CafeTidy) and stays off; if that tidy is undone, the
//     stand comes back bare.
// A real draped coat is a Blender job, only if he wants coats back (the plan, note 2).
//
// Nothing that runs switches them back on: the people's props are found by name (NpcLifeProps: LI_Laptop_Open, LI_ToteBag,
// LI_BinStation) and none of these is named so; no script sits under them; the night's lists are its own. The survey checks
// all three. Lived in 2 now places the coats switched off (its guard), so redoing it never brings them back. No collider is
// under any of them (the survey checks), so no route can change: the guards run before and after, the routes aren't re-baked.
//
// Through the café redesign's shared step (CafeRedesignShared.Step: Edit Mode, the survey first, one undo group with a record
// on the café's root, the checks, the photos before and after, nothing saved). Its Undo switches each back on; the shared rule
// stands: only the latest of the redesign's steps can be undone. Photos: Logs/CafeRedesign/art-tidy-1-coats-<time>/ (the
// banquette's chairs, the game camera, the plan from above) and the room's 14 in Logs/CafeFurnishing/art-tidy-1-coats-
// before|after-<time>/. Save the scene after.
// ---------------------------------------------------------------------------
internal static class ArtTidySteps
{
    public const string Title = "Art tidy 1 - The coats out";
    const string Label = "art-tidy-1-coats";
    const string MenuDo = Shared.Menu + "Art tidy 1 - The coats out: off the chairs and the coat stand (scene, with Undo)";
    const string MenuUndo = Shared.Menu + "Art tidy 1 - Undo: the coats back (scene)";

    // Lived in 2's coats: "LI_Coat_OnChair (seat <the seat's words>)" (CafeLivedInSteps.PlanSeatThings). The FBX's own mesh child
    // inside each is named "LI_Coat_OnChair" alone, so this prefix finds only the placed ones.
    const string CoatPrefix = "LI_Coat_OnChair (seat ";
    // The 24 Sept 2026 furnishing's coat stand and its two box-built things (CafeFurnishing.Entrance, CafeFurnishing.CoatStand).
    const string StandPath = "18 - cafe furnishing/D - Entrance and waiting nook/Coat stand (waiting nook)";
    static readonly string[] StandBoxes = { "Scarf", "Tote bag" };
    const string HatName = "Hat";

    const string HisNote = "his note (22 Oct): \"clothes on the on chairs in the cafe that just look like squares and rectangles together\"";

    [MenuItem(MenuDo)]
    static void TakeTheCoatsOut()
    {
        Plan plan = null;
        Shared.Step(Title, Label, run => plan = Survey(run), run => Change(run, plan), run => Photos(run),
                    guards: true, rebake: false, roomPhotos: true);
    }

    [MenuItem(MenuUndo)]
    static void BringTheCoatsBack() => Shared.UndoStep(Title, Label, rebake: false);

    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    sealed class Plan
    {
        public readonly List<Transform> coats = new List<Transform>();
        public readonly List<Transform> standBoxes = new List<Transform>();
        // What stays as it was (the coats' chairs, the stand, its hat), with its state before.
        public readonly List<(Transform t, bool on)> keep = new List<(Transform, bool)>();
    }

    // ================================================================== the survey (changes nothing)

    static Plan Survey(Shared.Run run)
    {
        var p = new Plan();

        // ---- the coats on the chairs (Lived in 2) ----
        foreach (Transform t in run.Cafe.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(CoatPrefix, StringComparison.Ordinal)) p.coats.Add(t);
        run.Info($"Lived in 2's coats on chairs ({CoatPrefix}...): {p.coats.Count}.");
        foreach (Transform c in p.coats)
        {
            run.Info($"  {Shared.PathOf(c)}: {(c.gameObject.activeSelf ? "on" : "already off")}, on {(c.parent != null ? c.parent.name : "nothing")}, " +
                     $"drawn at {Shared.BoundsText(Shared.RendererBounds(c, true))}");
            if (c.parent != null && !p.keep.Any(k => k.t == c.parent)) p.keep.Add((c.parent, c.parent.gameObject.activeSelf));
        }

        // ---- the coat stand (the 24 Sept 2026 furnishing): its two boxes go, the stand stays as it is ----
        Transform stand = Shared.Find(run, StandPath);
        if (stand == null) run.Info($"No coat stand at '{StandPath}': only the coats.");
        else
        {
            CafeTidy tidy = run.Cafe.GetComponent<CafeTidy>();
            bool tidied = tidy != null && tidy.putAway != null && tidy.putAway.Contains(stand.gameObject);
            run.Info($"The coat stand: {(stand.gameObject.activeSelf ? "on" : "off")}" +
                     (tidied ? " since Playtest 3's tidy (9 Oct, Cafe furnishing > 3; its undo would bring it back)" : "") +
                     ". The stand stays as it is (a post, a ball finial, four angled pegs with knobs, a cross foot: it reads as a stand); its hat (two cylinders and a band) stays.");
            p.keep.Add((stand, stand.gameObject.activeSelf));
            foreach (string name in StandBoxes)
            {
                Transform box = stand.Find(name);
                if (box == null) { run.Info($"  its {name}: not there"); continue; }
                p.standBoxes.Add(box);
                run.Info($"  its {name}: {(box.gameObject.activeSelf ? "on" : "already off")}, {box.GetComponentsInChildren<Renderer>(true).Length} plain box(es)");
            }
            Transform hat = stand.Find(HatName);
            if (hat != null) p.keep.Add((hat, hat.gameObject.activeSelf));
        }

        List<Transform> all = p.coats.Concat(p.standBoxes).ToList();
        run.Require(all.Count > 0, "Nothing to take out: no lived-in coat on a chair (Lived in 2) and no coat stand (the 24 Sept furnishing) in this scene.");
        run.Require(all.Any(t => t.gameObject.activeSelf), "Nothing to take out: the coats and the coat stand's scarf and tote are already off.");

        // ---- nothing that runs can bring them back, and no route can change ----
        List<string> solid = all.SelectMany(t => t.GetComponentsInChildren<Collider>(true)).Where(c => c != null && c.enabled).Select(c => Shared.PathOf(c.transform)).ToList();
        run.Check(solid.Count == 0, "no collider under anything that goes off, so no route can change (the routes aren't re-baked)" +
                                    (solid.Count > 0 ? ": " + string.Join("; ", solid) : ""));
        List<string> scripts = all.SelectMany(t => t.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null).Select(m => $"{m.GetType().Name} on {m.name}").ToList();
        run.Check(scripts.Count == 0, "no script under them that could switch them back on" + (scripts.Count > 0 ? ": " + string.Join("; ", scripts) : ""));
        string[] props = { NpcLifeProps.LaptopName, NpcLifeProps.BagName, NpcLifeProps.BinStationName };
        List<string> named = all.Where(t => props.Any(n => t.name.StartsWith(n, StringComparison.Ordinal))).Select(t => t.name).ToList();
        run.Check(named.Count == 0, $"none is named like a prop the people switch on and off (NpcLifeProps finds {string.Join(", ", props)} by name)" +
                                    (named.Count > 0 ? ": " + string.Join("; ", named) : ""));
        return p;
    }

    // ================================================================== the change (recorded; any FAIL puts everything back)

    static void Change(Shared.Run run, Plan p)
    {
        foreach (Transform coat in p.coats)
            Shared.SwitchOff(run, coat.gameObject, $"{HisNote}: Lived in 2's coat over the back of {(coat.parent != null ? coat.parent.name : "its chair")}");
        foreach (Transform box in p.standBoxes)
            Shared.SwitchOff(run, box.gameObject, $"the coat stand's {box.name.ToLowerInvariant()}, built of plain boxes (his playtest 3 note: \"a coat rack with two squares\"; {HisNote})");
        Shared.Note(run, $"Art tidy 1: {p.coats.Count} coat(s) off the chairs and the coat stand's {string.Join(" and ", p.standBoxes.Select(b => b.name.ToLowerInvariant()))} off " +
                         "(switched off, not deleted); the chairs, the stand and its hat as they were. Lived in 2 places its coats off from now on.");

        foreach (Transform t in p.coats.Concat(p.standBoxes))
            run.Check(t != null && !t.gameObject.activeSelf, $"{Shared.PathOf(t)}: off (not deleted)");
        foreach ((Transform t, bool on) in p.keep)
            run.Check(t != null && t.gameObject.activeSelf == on, $"{Shared.PathOf(t)}: as it was ({(on ? "on" : "off")})");
    }

    // ================================================================== photos (before and after, from the same places)

    static void Photos(Shared.Run run)
    {
        // Lived in 2's own views of the coats: its "08-banquette-napkins-and-coats" and "11-plan-from-above".
        Shared.Photo(run, "01-the-banquette-chairs", new Vector3(-4.3f, 1.35f, 3.4f), new Vector3(-6.1f, .75f, 5.8f), 55f);
        Shared.GameCameraPhoto(run, "02-game-camera");
        Shared.Photo(run, "03-plan-from-above", new Vector3(0f, 17f, 6.6f), new Vector3(0f, 0f, 6.61f), 50f, true);
    }
}
#endif
