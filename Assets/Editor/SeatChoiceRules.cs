#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// SEAT CHOICE RULES (23 Oct 2026; Fixit Fidget > Checks > Seat choice rules; Edit Mode, nothing saved)
//
// The way people choose seats (SeatChoice, in SeatTags.cs), checked without Play: his note was that drink customers "only
// sit to the left of the couch" and repair customers "only... next to the bar stool", and he wants "a feel for the player to
// like kind of look for the customer that did the order". The rules:
//   1. personal space: a stranger at the next seat costs more than one at the same table, and nothing beyond it; two people
//      in turn never sit side by side while a seat with nobody beside it is as good; in a busy room, a stranger beside every
//      free seat, the walk still ranks them and the nearest is taken (personal space ranks seats, it never refuses one);
//   2. the walk counts, but less than the reasons: someone with no leanings takes the nearer of two plain seats; a Cheerful
//      person walks past a plain seat for a window; a Rushed person takes the near seat over a far one by the door; a seat
//      nobody sits on (waited at standing) is overflow only: a window-lover sits at a window further off, someone with no
//      leanings walks 6 m further to sit, and with nothing else free it is still given;
//   3. regulars: Grace takes her favourite (a soft seat by the window) when one is free, and otherwise scores like anyone
//      else; the Officer (Distracted, as the game rolls him) has a stool at the window counter for his favourite, asleep
//      while those stools are waited at standing (he sits elsewhere, and never on a repair-bar stool), his once they sit;
//   4. the repair bar: a bench job stays at it unless their character wants comfort more (StaysAtTheBar); Day 1's short
//      cleans keep every walk-in there, and over Days 2-3's people about one in three sit elsewhere; the stools are kept
//      for repairs;
//   5. in a scene (a preview scene, through WaitingArea.Claim itself): a dirty seat is never chosen, not even as the only
//      seat; once cleared it is; the second of two people doesn't sit beside the first; a seat's tags come from its
//      SeatTags, else its shape, plus the room's areas;
//   6. Day 1 from the till, without dice, on the café's seats as Seats 1, Lived in 2 and Café dressing 3 laid them (straight
//      walks; the lounge where the 20 Oct photos put it; the window counter's stools waited at standing, as today): Day 1's
//      ten sorts of walk-in (Cheerful or Chatty, by five ways of moving), each alone, have near-equal seats across at least
//      three zones, none of them a standing wait or a repair-bar stool, and no one seat is the best for more than six of
//      the ten (the old nearest rule gave all ten one chair); Day 1's morning (Grace at the repair bar, then the Officer,
//      then four walk-ins, each taking their best while the others sit) uses at least three zones, the drinks at least two,
//      nobody sits beside a stranger, and the Officer sits;
//   7. the same on the open café scene itself, when it is the open scene: its own seats (their tags, sit and stand points,
//      Snap To Seat) and its walks from the counter's place 0 along the NavMesh, WaitingArea's own, read by name (straight
//      lines if Edit Mode has no NavMesh loaded). This is the deterministic proof the Day labs' medians stand on.
// Report: Logs/SeatChoice/rules-<time>.txt; the Console's last line says "[Seat choice rules] All N checks passed." or
// "[Seat choice rules] N of M checks FAILED. <folder>". Edit Mode only, like the Waiting space check: no Play, so no lab
// save and nothing on screen to photograph.
// ---------------------------------------------------------------------------
public static class SeatChoiceRules
{
    const string Tag = "[Seat choice rules]";

    static readonly StringBuilder report = new StringBuilder();
    static int checks, failures;

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
    }

    static void Line(string text = "") => report.AppendLine(text);

    [MenuItem("Fixit Fidget/Checks/Seat choice rules")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(Tag + " Stop Play Mode first: these rules run in Edit Mode.");
            return;
        }
        report.Clear();
        checks = failures = 0;
        Line("Seat choice rules, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        try
        {
            PersonalSpace();
            TheWalk();
            Regulars();
            TheRepairBar();
            InAScene();
            AQuietDayOne();
            TheOpenCafe();
        }
        catch (Exception e)
        {
            Check(false, "the rules stopped: " + e);
        }
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "SeatChoice"));
        string file = Path.Combine(folder, "rules-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, report.ToString());
        }
        catch (Exception e) { Debug.LogWarning(Tag + " Couldn't write the report: " + e.Message); }
        if (failures == 0) Debug.Log(Tag + " The report:\n" + report);
        else Debug.LogError(Tag + " The report:\n" + report);
        string verdict = failures == 0 ? $"{Tag} All {checks} checks passed." : $"{Tag} {failures} of {checks} checks FAILED. {folder}";
        if (failures == 0) Debug.Log(verdict);
        else Debug.LogError(verdict);
    }

    // ---------------------------------------------------------------- 1. personal space

    static SeatChoice.Seat Plain(float x, float z) => new SeatChoice.Seat(new Vector3(x, .45f, z), SeatTag.None);

    static void PersonalSpace()
    {
        Line();
        Line("1. Personal space");
        SeatChoice.Person nobody = SeatChoice.Make();
        Vector3 at = new Vector3(0f, .45f, 0f);
        float next = SeatChoice.Crowding(nobody, at, new List<Vector3> { at + new Vector3(1.0f, 0f, 0f) });
        float table = SeatChoice.Crowding(nobody, at, new List<Vector3> { at + new Vector3(1.6f, 0f, 0f) });
        float beyond = SeatChoice.Crowding(nobody, at, new List<Vector3> { at + new Vector3(2.0f, 0f, 0f) });
        Check(next > table && table > 0f && beyond == 0f,
            $"a stranger at the next seat (1.0 m) costs {next:0.0}, one at the same table (1.6 m) {table:0.0}, one 2.0 m off nothing");

        // Two seats equally far: one beside a stranger, one at an empty table. The empty table wins by Next To.
        SeatChoice.Seat beside = Plain(3f, 0f), empty = Plain(3f, 4f);
        var stranger = new List<Vector3> { new Vector3(3f, .45f, -1f) };
        float b = SeatChoice.Score(nobody, beside, stranger, 4f), e = SeatChoice.Score(nobody, empty, stranger, 4f);
        Check(e > b && Mathf.Abs(e - b - SeatChoice.NextTo) < 1e-4f, $"at the same walk, the seat at an empty table beats the one beside a stranger by {e - b:0.00}");

        // Two people in turn from the same place, four seats in two pairs side by side (1.0 m apart), the pairs equally near:
        // the second never sits beside the first while the other pair is free.
        var four = new List<SeatChoice.Seat> { Plain(3f, -.5f), Plain(3f, .5f), Plain(-3f, -.5f), Plain(-3f, .5f) };
        var walks = four.Select(s => new Vector3(s.at.x, 0f, s.at.z).magnitude).ToList();
        int first = Best(nobody, four, new List<Vector3>(), walks, out _);
        var taken = new List<Vector3> { four[first].at };
        int second = Best(nobody, four, taken, walks, out List<int> secondNear, except: first);
        bool besideFirst = secondNear.Any(i => Vector3.Distance(four[i].at, four[first].at) < SeatChoice.NextToWithin);
        Check(second >= 0 && !besideFirst, $"two people in turn: the first takes seat {first}, the second's near-equals ({string.Join(", ", secondNear)}) are all at the other table");

        // A busy room: a stranger beside every free seat (each 1.0 m from one), so personal space costs them all the same and
        // the walk ranks them: the nearest is taken, and alone among near-equals. That a seat is taken at all can't fail (a
        // score is always a number); that it is the nearest shows personal space ranks seats and never refuses one.
        var crowded = new List<Vector3> { new Vector3(3f, .45f, -1.5f), new Vector3(3f, .45f, 1.5f), new Vector3(-3f, .45f, -1.5f), new Vector3(-3f, .45f, 1.5f) };
        var busyWalks = new List<float> { 6f, 3f, 9f, 12f };
        int filled = Best(nobody, four, crowded, busyWalks, out List<int> busyNear);
        Check(filled == 1 && busyNear.Count == 1 && busyNear[0] == 1,
            $"a busy room, a stranger beside every free seat: they still sit, at the nearest (seat {filled} of walks 6, 3, 9 and 12 m; " +
            $"near-equals {string.Join(", ", busyNear)}): personal space ranks seats, it never refuses one");

        Check(SeatChoice.Make("Chatty").space < 1f && SeatChoice.Make(manner: "Shy").space > 1f,
            $"a Chatty person minds strangers less (x{SeatChoice.Make("Chatty").space:0.0}), a Shy one more (x{SeatChoice.Make(manner: "Shy").space:0.0})");
    }

    // The best of `seats` for `who` (index), and the near-equals (within WaitingArea's default margin, 0.6 points).
    static int Best(SeatChoice.Person who, List<SeatChoice.Seat> seats, List<Vector3> others, List<float> walks, out List<int> near, int except = -1)
    {
        var scores = new List<float>();
        var index = new List<int>();
        for (int i = 0; i < seats.Count; i++)
        {
            if (i == except) continue;
            scores.Add(SeatChoice.Score(who, seats[i], others, walks[i]));
            index.Add(i);
        }
        var nearIdx = new List<int>();
        SeatChoice.NearlyBest(scores, Margin, nearIdx);
        near = nearIdx.Select(k => index[k]).ToList();
        if (scores.Count == 0) return -1;
        int best = 0;
        for (int k = 1; k < scores.Count; k++) if (scores[k] > scores[best]) best = k;
        return index[best];
    }

    // WaitingArea's Nearly As Near as it ships (points).
    const float Margin = .6f;

    // ---------------------------------------------------------------- 2. the walk

    static void TheWalk()
    {
        Line();
        Line("2. The walk counts, but less than the reasons");
        SeatChoice.Person nobody = SeatChoice.Make();
        var two = new List<SeatChoice.Seat> { Plain(3f, 0f), Plain(6f, 0f) };
        int pick = Best(nobody, two, new List<Vector3>(), new List<float> { 3f, 6f }, out List<int> near);
        Check(pick == 0 && near.Count == 1, $"someone with no leanings takes the nearer of two plain seats (3 m against 6 m: {3f * SeatChoice.WalkWeight:0.0} against {6f * SeatChoice.WalkWeight:0.0} points), and the far one isn't a near-equal");

        var room = new List<SeatChoice.Seat> { Plain(3f, 0f), new SeatChoice.Seat(new Vector3(11f, .45f, 0f), SeatTag.Window) };
        var roomWalks = new List<float> { 3f, 11f };
        int cheerful = Best(SeatChoice.Make("Cheerful"), room, new List<Vector3>(), roomWalks, out _);
        Check(cheerful == 1, "a Cheerful person walks 8 m past a plain seat for one by the window (the reasons outweigh the walk)");

        var door = new List<SeatChoice.Seat> { Plain(3f, 0f), new SeatChoice.Seat(new Vector3(11f, .45f, 0f), SeatTag.NearDoor | SeatTag.Window) };
        int rushed = Best(SeatChoice.Make("Rushed", "Hurried"), door, new List<Vector3>(), roomWalks, out _);
        Check(rushed == 0, "a Rushed person takes the near seat, not one 8 m further by the door and the window (the walk counts x" +
                           $"{SeatChoice.Make("Rushed", "Hurried").walk:0.0} for them)");
        int rushedAlone = Best(SeatChoice.Make("Rushed"), new List<SeatChoice.Seat> { Plain(3f, 0f), new SeatChoice.Seat(new Vector3(3.5f, .45f, 0f), SeatTag.NearDoor) },
                               new List<Vector3>(), new List<float> { 3f, 3.5f }, out _);
        Check(rushedAlone == 1, "...and between two near seats, the one by the door");

        // A seat nobody sits on (Snap To Seat off: the window counter's stools until they sit) is waited at standing, and her
        // playtest 5 note was "people wouldn't sit down on the bar stools": overflow only (Standing Wait).
        var standingStool = new SeatChoice.Seat(new Vector3(5.9f, .75f, 0f), SeatTag.Bar | SeatTag.Window, standing: true);
        var windowSeat = new SeatChoice.Seat(new Vector3(7.6f, .45f, 0f), SeatTag.Window);
        int sits = Best(SeatChoice.Make("Cheerful", "Distracted"), new List<SeatChoice.Seat> { standingStool, windowSeat, Plain(3.7f, 0f) },
                        new List<Vector3>(), new List<float> { 5.9f, 7.6f, 3.7f }, out _);
        Check(sits == 1, $"a Cheerful, Distracted person sits at a window 1.7 m further rather than wait standing at a window stool (Standing Wait {SeatChoice.StandingWait:0.0})");
        var standingSpot = new SeatChoice.Seat(new Vector3(3f, .75f, 0f), SeatTag.None, standing: true);
        int overflow = Best(nobody, new List<SeatChoice.Seat> { standingSpot, Plain(9f, 0f) }, new List<Vector3>(), new List<float> { 3f, 9f }, out List<int> overflowNear);
        Check(overflow == 1 && overflowNear.Count == 1, "...and someone with no leanings walks 6 m further to sit: a standing wait is no near-equal of a seat");
        int onlyOne = Best(nobody, new List<SeatChoice.Seat> { standingSpot }, new List<Vector3>(), new List<float> { 3f }, out _);
        Check(onlyOne == 0, "...but with nothing else free it is still given (overflow, never refused)");
    }

    // ---------------------------------------------------------------- 3. regulars

    static void Regulars()
    {
        Line();
        Line("3. Regulars' favourites");
        SeatChoice.Person grace = SeatChoice.Make(manner: "Relaxed", regular: GraceCameraEpisode.ProfileId);
        Check(grace.favourite == (SeatTag.Window | SeatTag.Lounge), $"Grace's favourite is a soft seat by the window ({grace.favourite})");
        var seats = new List<SeatChoice.Seat>
        {
            Plain(2f, 0f),
            new SeatChoice.Seat(new Vector3(3f, .45f, 0f), SeatTag.Lounge),
            new SeatChoice.Seat(new Vector3(10f, .45f, 0f), SeatTag.Window),
            new SeatChoice.Seat(new Vector3(12f, .45f, 0f), SeatTag.Window | SeatTag.Lounge),
        };
        var walks = new List<float> { 2f, 3f, 10f, 12f };
        int free = Best(grace, seats, new List<Vector3>(), walks, out _);
        Check(free == 3, "Grace takes her favourite when one is free, 12 m off, past a near plain seat, a near soft one and a window seat without a cushion");
        var taken = new List<SeatChoice.Seat>(seats.Take(3));
        int otherwise = Best(grace, taken, new List<Vector3>(), walks.Take(3).ToList(), out _);
        Check(otherwise == 1, "with her favourite taken she scores like anyone else (Relaxed: the near soft seat)");
        var besideStranger = new List<Vector3> { seats[3].at + new Vector3(0f, 0f, 1f) };
        Check(Best(grace, seats, besideStranger, walks, out _) == 3, "a stranger beside her favourite doesn't keep her off it (\"take it if free\")");

        // The Officer as the game rolls him: his profile has no way of moving of its own, so NpcSocial takes the library's by
        // his save id (NpcProfileLibrary.ForKey("officer"), FNV-1a: the fifth of five, Distracted).
        SeatChoice.Person officer = SeatChoice.Make(manner: "Distracted", regular: OfficerStory.ProfileId);
        Check(officer.favourite == (SeatTag.Bar | SeatTag.Window), $"the Officer's favourite is a stool at the window counter ({officer.favourite})");
        var repairStool = new SeatChoice.Seat(new Vector3(4.8f, .75f, 0f), SeatTag.Bar, repairBar: true);
        var counterStanding = new SeatChoice.Seat(new Vector3(5.9f, .75f, 0f), SeatTag.Bar | SeatTag.Window, standing: true);   // today
        var counterSitting = new SeatChoice.Seat(new Vector3(5.9f, .75f, 0f), SeatTag.Bar | SeatTag.Window);                    // once they sit
        var barWalks = new List<float> { 4.8f, 5.9f, 3.7f };
        Check(Best(officer, new List<SeatChoice.Seat> { repairStool, counterStanding, Plain(3.7f, 0f) }, new List<Vector3>(), barWalks, out _) == 2,
            "while the window counter's stools are waited at standing his favourite sleeps: he sits at a plain seat, neither standing there nor on a repair-bar stool");
        Check(Best(officer, new List<SeatChoice.Seat> { repairStool, counterSitting, Plain(3.7f, 0f) }, new List<Vector3>(), barWalks, out _) == 1,
            "once they sit, he takes one, past a nearer plain seat and a repair-bar stool");
        Check(Best(officer, new List<SeatChoice.Seat> { repairStool, Plain(3.7f, 0f) }, new List<Vector3>(), new List<float> { 4.8f, 3.7f }, out _) == 1,
            "a repair-bar stool is never his favourite (kept for repairs): with the window counter's taken, a plain seat");
        Check(SeatChoice.Make("Cheerful").favourite == SeatTag.None, "a walk-in has no favourite");
    }

    // ---------------------------------------------------------------- 4. the repair bar

    static readonly string[] Manners = { "Relaxed", "Hurried", "Shy", "Social", "Distracted" };

    static void TheRepairBar()
    {
        Line();
        Line("4. The repair bar: a bench job stays unless their character wants comfort more");
        Check(SeatChoice.StaysAtTheBar(SeatChoice.Make(benchJob: true)), "a bench job with no leanings stays at the bar");
        Check(SeatChoice.StaysAtTheBar(SeatChoice.Make("Cheerful", "Social", benchJob: true, longJob: true)),
            $"Cheerful and Social with a long repair stays (comfort {SeatChoice.LongRepairComfort:0.0} is not more than the bar's {SeatChoice.RepairBar:0.0})");
        Check(!SeatChoice.StaysAtTheBar(SeatChoice.Make("Sentimental", "Hurried", benchJob: true)), "a Sentimental person sits somewhere soft, even for a short repair");
        Check(!SeatChoice.StaysAtTheBar(SeatChoice.Make("Chatty", "Relaxed", benchJob: true, longJob: true)), "a Relaxed person with a long repair (a part to swap) sits elsewhere");
        Check(SeatChoice.StaysAtTheBar(SeatChoice.Make("Chatty", "Relaxed", benchJob: true)), "...but stays for a short one");
        Check(SeatChoice.StaysAtTheBar(SeatChoice.Make("", "Relaxed", GraceCameraEpisode.ProfileId, benchJob: true, longJob: true)),
            "Grace with her camera on the bench stays at the bar (her stories; a regular watches her own thing), even for the shutter's swap");
        Check(SeatChoice.StaysAtTheBar(SeatChoice.Make("Impatient", "Relaxed", benchJob: true, longJob: true)), "an Impatient person watches their repair, even a long one");
        Check(!SeatChoice.StaysAtTheBar(SeatChoice.Make("Cheerful")), "a drink customer never asks for the repair bar");
        Check(!SeatChoice.Make("Cheerful", benchJob: false, longJob: true).longJob, "a long job is only a bench job's");

        // Day 1's walk-ins (Cheerful or Chatty) with Day 1's short cleans: every one stays at the bar (the tutorial's reassure
        // over the counter keeps working).
        bool allStay = new[] { "Cheerful", "Chatty" }.All(a => Manners.All(m => SeatChoice.StaysAtTheBar(SeatChoice.Make(a, m, benchJob: true))));
        Check(allStay, "Day 1: a Cheerful or Chatty walk-in with a short clean stays at the bar, whatever their way of moving");

        // Days 2-3's walk-ins (Cheerful, Chatty, Sentimental), every way of moving equally likely, half the repairs long:
        // about one in three sits elsewhere.
        int elsewhere = 0, all = 0;
        foreach (string a in new[] { "Cheerful", "Chatty", "Sentimental" })
            foreach (string m in Manners)
                foreach (bool longJob in new[] { false, true })
                {
                    all++;
                    if (!SeatChoice.StaysAtTheBar(SeatChoice.Make(a, m, benchJob: true, longJob: longJob))) elsewhere++;
                }
        float share = elsewhere / (float)all;
        Check(share >= .2f && share <= .5f, $"Days 2-3: {elsewhere} of {all} sorts of walk-in repair sit elsewhere ({share:P0}; about one in three wanted: 20-50 %)");

        // The stools are kept for repairs.
        var stool = new SeatChoice.Seat(new Vector3(3f, .75f, 0f), SeatTag.Bar, repairBar: true);
        var plain = Plain(3f, 2f);
        SeatChoice.Person drinker = SeatChoice.Make();
        Check(SeatChoice.Reasons(drinker, stool) < SeatChoice.Reasons(drinker, plain), "a repair-bar stool counts against someone whose thing isn't on the bench (kept for repairs)");
        SeatChoice.Person benchElsewhere = SeatChoice.Make("Sentimental", benchJob: true);
        Check(Mathf.Approximately(SeatChoice.Reasons(benchElsewhere, stool), SeatChoice.Reasons(benchElsewhere, plain) + benchElsewhere.Pull(SeatTag.Bar)),
            "...never against a repair customer, wherever they choose to sit");
    }

    // ---------------------------------------------------------------- 5. in a scene, through WaitingArea.Claim

    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static void Lifecycle(WaitingSpot spot, string message)
    {
        MethodInfo method = typeof(WaitingSpot).GetMethod(message, Hidden);
        if (method == null) throw new InvalidOperationException($"WaitingSpot has no {message}.");
        method.Invoke(spot, null);
    }

    static TableSeat MakeSeat(Scene scene, string name, Vector3 floor, float seatHeight = .445f, bool bench = false)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = floor;
        var pose = new GameObject("SeatPose").transform;
        pose.SetParent(go.transform, false);
        pose.localPosition = new Vector3(0f, seatHeight, 0f);
        var stand = new GameObject("StandPoint").transform;
        stand.SetParent(go.transform, false);
        stand.localPosition = new Vector3(0f, .04f, -.67f);
        TableSeat seat = go.AddComponent<TableSeat>();
        var so = new SerializedObject(seat);
        so.FindProperty("kind").intValue = (int)WaitingSpot.SpotKind.Seat;
        so.FindProperty("drainMultiplier").floatValue = .6f;
        so.FindProperty("standPoint").objectReferenceValue = stand;
        so.FindProperty("seatPose").objectReferenceValue = pose;
        so.FindProperty("snapToSeat").boolValue = true;
        so.FindProperty("sitStyle").intValue = (int)(bench ? TableSeat.SitStyle.Bench : TableSeat.SitStyle.Chair);
        so.ApplyModifiedPropertiesWithoutUndo();
        return seat;
    }

    static void InAScene()
    {
        Line();
        Line("5. In a scene, through WaitingArea.Claim (a preview scene far from the café; nothing in the open scene changes)");
        Scene preview = EditorSceneManager.NewPreviewScene();
        var seats = new List<TableSeat>();
        var people = new List<GameObject>();
        GameObject Person(string name, Vector3 at)
        {
            var go = new GameObject("Seat choice rules: " + name);
            SceneManager.MoveGameObjectToScene(go, preview);
            go.transform.position = at;
            people.Add(go);
            return go;
        }
        try
        {
            Vector3 o = new Vector3(20000f, 0f, 20000f);
            var host = new GameObject("Seat choice rules: the waiting area");
            SceneManager.MoveGameObjectToScene(host, preview);
            WaitingArea area = host.AddComponent<WaitingArea>();
            TableSeat near = MakeSeat(preview, "near", o + new Vector3(3f, 0f, 0f));
            TableSeat beside = MakeSeat(preview, "beside the near one", o + new Vector3(3f, 0f, 1f));
            TableSeat far = MakeSeat(preview, "at another table", o + new Vector3(3f, 0f, 5f));
            seats.AddRange(new[] { near, beside, far });
            foreach (TableSeat s in seats) Lifecycle(s, "OnEnable");

            // The second of two people doesn't sit beside the first.
            Transform one = Person("one", o).transform, two = Person("two", o).transform;
            WaitingSpot first = area.Claim(one, WaitingSpot.SpotKind.Seat);
            WaitingSpot second = area.Claim(two, WaitingSpot.SpotKind.Seat);
            Check(first == near || first == beside, $"the first person takes one of the two near seats ({(first != null ? first.name : "nothing")})");
            Check(second == far, $"the second takes the seat at another table, not the one beside the first ({(second != null ? second.name : "nothing")})");
            area.Release(one);
            area.Release(two);

            // A dirty seat is never chosen, not even as the only seat; cleared, it is.
            var cup = new GameObject("Seat choice rules: a used cup");
            SceneManager.MoveGameObjectToScene(cup, preview);
            people.Add(cup);
            near.SetDirty(cup);
            bool everDirty = false;
            for (int i = 0; i < 6; i++)
            {
                Transform who = Person("claim " + i, o).transform;
                WaitingSpot got = area.Claim(who, WaitingSpot.SpotKind.Seat);
                if (got == near) everDirty = true;
                area.Release(who);
            }
            Check(!everDirty, "six claims with the nearest seat dirty: it is never chosen");
            Transform holderA = Person("holder a", o).transform, holderB = Person("holder b", o).transform;
            beside.Claim(holderA);
            far.Claim(holderB);
            Transform alone = Person("alone", o).transform;
            WaitingSpot onlyDirty = area.Claim(alone, WaitingSpot.SpotKind.Seat);
            Check(onlyDirty == null, $"the dirty seat the only one left: nobody is given it ({(onlyDirty != null ? onlyDirty.name : "nothing")}; they'd wait standing)");
            near.Clean();
            WaitingSpot cleared = area.Claim(alone, WaitingSpot.SpotKind.Seat);
            Check(cleared == near, "cleared, it is chosen again");
            area.Release(alone);
            area.Release(holderA);
            area.Release(holderB);

            // Tags: its own SeatTags, else its shape, plus the room's areas.
            TableSeat stool = MakeSeat(preview, "a raised stool", o + new Vector3(-6f, 0f, 0f), .745f);
            TableSeat cushion = MakeSeat(preview, "a bench seat", o + new Vector3(-6f, 0f, 3f), .445f, bench: true);
            TableSeat tagged = MakeSeat(preview, "a tagged stool", o + new Vector3(-6f, 0f, 6f), .745f);
            seats.AddRange(new[] { stool, cushion, tagged });
            Check(stool.Tags == SeatTag.Bar && cushion.Tags == SeatTag.Lounge && near.Tags == SeatTag.None,
                $"untagged: a raised seat is a bar stool ({stool.Tags}), a bench seat soft ({cushion.Tags}), a plain chair plain ({near.Tags})");
            SeatTags own = tagged.gameObject.AddComponent<SeatTags>();
            own.Set(SeatTag.Communal, "test zone");
            Check(tagged.Tags == SeatTag.Communal && tagged.Zone == "test zone", $"a SeatTags on the seat wins over its shape ({tagged.Tags}, zone '{tagged.Zone}')");
            var room = new GameObject("Seat choice rules: a window area");
            SceneManager.MoveGameObjectToScene(room, preview);
            people.Add(room);
            room.transform.position = o + new Vector3(-6f, 1f, 3f);
            SeatTags window = room.AddComponent<SeatTags>();
            window.Set(SeatTag.Window, "", new Vector3(2f, 3f, 4f));
            Check(cushion.Tags == (SeatTag.Lounge | SeatTag.Window) && tagged.Tags == SeatTag.Communal && near.Tags == SeatTag.None,
                $"a room area adds its tags to the seats inside it, wherever they came from (the bench seat: {cushion.Tags}; outside it: {tagged.Tags}, {near.Tags})");
            Check(near.Zone == near.name, $"an untagged seat's zone is its parent's name, or its own with no parent ('{near.Zone}')");
        }
        finally
        {
            foreach (TableSeat s in seats) if (s != null) WaitingArea.Unregister(s);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // ---------------------------------------------------------------- 6. Day 1 from the till, without dice

    // The café's seats as Seats 1, Lived in 2 and Café dressing 3 laid them (sit point and stand point, x and z), with the
    // tags Seat tags 1 gives them (their own and the room's areas), and the window counter's three stools waited at standing
    // (Snap To Seat off since Seats 1). The lounge's place is measured by its step: the 20 Oct photos put it about here.
    sealed class Fixture
    {
        public readonly string name, zone;
        public readonly Vector2 sit, stand;
        public readonly SeatTag tags;
        public readonly bool repairBar, standing;
        public Fixture(string name, string zone, float sx, float sz, float tx, float tz, SeatTag tags, bool repairBar = false, bool standing = false)
        {
            this.name = name; this.zone = zone; sit = new Vector2(sx, sz); stand = new Vector2(tx, tz); this.tags = tags;
            this.repairBar = repairBar; this.standing = standing;
        }
        public SeatChoice.Seat Seat => new SeatChoice.Seat(new Vector3(sit.x, .45f, sit.y), tags, repairBar, standing);
    }

    const SeatTag W = SeatTag.Window, L = SeatTag.Lounge, B = SeatTag.Bar, C = SeatTag.Communal, Q = SeatTag.Quiet, D = SeatTag.NearDoor;

    static readonly Fixture[] Cafe =
    {
        new Fixture("repair bar west", "repair bar", -5.2f, 12.857f, -5.2f, 12.187f, B, true),
        new Fixture("repair bar middle", "repair bar", -4.2f, 12.857f, -4.2f, 12.187f, B, true),
        new Fixture("repair bar east", "repair bar", -3.2f, 12.857f, -3.2f, 12.187f, B, true),
        new Fixture("banquette south cushion", "west banquette", -7.12f, 4.69f, -6.24f, 3.36f, L | W),
        new Fixture("banquette south chair", "west banquette", -5.636f, 4.69f, -5.286f, 5.49f, W),
        new Fixture("banquette middle cushion", "west banquette", -7.12f, 6.79f, -6.09f, 7.77f, L | W),
        new Fixture("banquette middle chair", "west banquette", -5.636f, 6.79f, -5.336f, 7.49f, W),
        new Fixture("banquette north cushion", "west banquette", -7.12f, 8.89f, -6.34f, 9.72f, L | W),
        new Fixture("banquette north chair", "west banquette", -5.636f, 8.89f, -5.536f, 9.94f, W),
        new Fixture("communal west 1", "communal table", -3.75f, 4.839f, -4.60f, 5.088f, C | D),
        new Fixture("communal east 1", "communal table", -2.15f, 4.862f, -1.48f, 4.862f, C | D),
        new Fixture("communal west 2", "communal table", -3.75f, 5.739f, -4.40f, 5.788f, C),
        new Fixture("communal east 2", "communal table", -2.15f, 5.762f, -1.48f, 5.762f, C),
        new Fixture("communal west 3", "communal table", -3.75f, 6.639f, -4.35f, 6.639f, C),
        new Fixture("communal east 3", "communal table", -2.15f, 6.662f, -1.48f, 6.662f, C),
        new Fixture("communal west 4", "communal table", -3.75f, 7.539f, -4.60f, 7.338f, C),
        new Fixture("communal east 4", "communal table", -2.15f, 7.562f, -1.48f, 7.562f, C),
        new Fixture("bistro south west", "bistro", 2.41f, 4.6f, 1.66f, 4.6f, D),
        new Fixture("bistro south east", "bistro", 3.59f, 4.6f, 4.34f, 4.6f, D),
        new Fixture("bistro north west", "bistro", 2.41f, 7.8f, 1.66f, 7.8f, SeatTag.None),
        new Fixture("bistro north east", "bistro", 3.59f, 7.8f, 4.34f, 7.8f, SeatTag.None),
        new Fixture("window counter south", "window counter", 6.507f, 6.0f, 5.837f, 6.0f, B | W, standing: true),
        new Fixture("window counter middle", "window counter", 6.507f, 7.2f, 5.837f, 7.2f, B | W, standing: true),
        new Fixture("window counter north", "window counter", 6.507f, 8.4f, 5.837f, 8.4f, B | W, standing: true),
        new Fixture("reading corner south", "reading corner", -6.0f, .65f, -5.08f, .52f, L | W | Q),
        new Fixture("reading corner north", "reading corner", -6.0f, 2.15f, -5.05f, 1.95f, L | W | Q),
        new Fixture("lounge west", "lounge", 3.85f, 10.5f, 3.85f, 9.6f, L),
        new Fixture("lounge east", "lounge", 5.5f, 10.5f, 5.5f, 9.6f, L),
    };

    // The counter's place everyone alone is heard at (CafeBarSteps: queue place 0).
    static readonly Vector2 Till = new Vector2(1.6f, 12.5f);

    static void AQuietDayOne()
    {
        Line();
        Line("6. Day 1 from the till, without dice (the café's seats as laid, straight walks, the window counter's stools waited at standing)");
        DayOne("", Cafe.Select(f => f.name).ToList(), Cafe.Select(f => f.zone).ToList(), Cafe.Select(f => f.Seat).ToList(),
               Cafe.Select(f => Vector2.Distance(Till, f.stand)).ToList(), Margin, Array.FindIndex(Cafe, f => f.name == "repair bar middle"));
    }

    // Day 1 judged without dice (sections 6 and 7). Day 1's ten sorts of walk-in, each alone in the room: their best seat and
    // their near-equals, the seats WaitingArea picks between at random. Then Day 1's morning: Grace on her stool at the
    // repair bar (her camera is on the bench), the Officer, and four walk-ins, each taking their best while the others sit.
    static void DayOne(string where, List<string> names, List<string> zones, List<SeatChoice.Seat> seats, List<float> walks, float margin, int graceSeat)
    {
        var scores = new List<float>();
        var near = new List<int>();
        var bestOf = new List<int>();
        var nearZones = new HashSet<string>();
        bool nearStanding = false, nearStool = false;
        foreach (string a in new[] { "Cheerful", "Chatty" })
            foreach (string m in Manners)
            {
                SeatChoice.Person sort = SeatChoice.Make(a, m);
                scores.Clear();
                for (int k = 0; k < seats.Count; k++) scores.Add(SeatChoice.Score(sort, seats[k], null, walks[k]));
                SeatChoice.NearlyBest(scores, margin, near);
                int top = Top(scores);
                bestOf.Add(top);
                foreach (int n in near)
                {
                    nearZones.Add(zones[n]);
                    nearStanding |= seats[n].standing;
                    nearStool |= seats[n].repairBar;
                }
                Line($"     {a}, {m}: {names[top]} ({zones[top]}); near-equals {string.Join(", ", near.Select(x => names[x]))}");
            }
        int most = bestOf.GroupBy(x => x).Max(g => g.Count());
        Check(nearZones.Count >= 3, $"{where}Day 1's ten sorts of walk-in, each alone, have near-equal seats across {nearZones.Count} zones " +
                                    $"({string.Join(", ", nearZones)}; at least 3 wanted)");
        Check(most <= 6, $"{where}...and no one seat is the best for more than six of the ten (the most is {most}; the old nearest rule gave all " +
                         "ten one chair, his \"left of the couch\")");
        Check(!nearStanding && !nearStool, $"{where}...and none of their near-equals is a seat waited at standing or a repair-bar stool");

        var taken = new List<int>();
        if (graceSeat >= 0)
        {
            taken.Add(graceSeat);
            Line($"     Grace, her camera on the bench: {names[graceSeat]} ({zones[graceSeat]})");
        }
        var cast = new (string who, SeatChoice.Person person)[]
        {
            ("the Officer", SeatChoice.Make(manner: "Distracted", regular: OfficerStory.ProfileId)),
            ("Cheerful, Distracted", SeatChoice.Make("Cheerful", "Distracted")),
            ("Chatty, Social", SeatChoice.Make("Chatty", "Social")),
            ("Cheerful, Relaxed", SeatChoice.Make("Cheerful", "Relaxed")),
            ("Chatty, Hurried", SeatChoice.Make("Chatty", "Hurried")),
        };
        int officerSeat = -1;
        foreach (var c in cast)
        {
            var others = taken.Select(x => seats[x].at).ToList();
            scores.Clear();
            for (int k = 0; k < seats.Count; k++)
                scores.Add(taken.Contains(k) ? float.NegativeInfinity : SeatChoice.Score(c.person, seats[k], others, walks[k]));
            int top = Top(scores);
            if (officerSeat < 0) officerSeat = top;
            taken.Add(top);
            Line($"     {c.who}: {names[top]} ({zones[top]})");
        }
        int used = taken.Select(x => zones[x]).Distinct().Count();
        int drinkZones = taken.Skip(graceSeat >= 0 ? 1 : 0).Select(x => zones[x]).Distinct().Count();
        bool sideBySide = false;
        for (int p = 0; p < taken.Count; p++)
            for (int q = p + 1; q < taken.Count; q++)
                if (Apart(seats[taken[p]].at, seats[taken[q]].at) < SeatChoice.NextToWithin) sideBySide = true;
        Check(used >= 3, $"{where}Day 1's morning (Grace, the Officer, four walk-ins) uses {used} zones (at least 3 wanted)");
        Check(drinkZones >= 2, $"{where}...its drink customers {drinkZones} (at least 2 wanted)");
        Check(!sideBySide, $"{where}...nobody sits beside a stranger while the room has space");
        Check(officerSeat >= 0 && !seats[officerSeat].standing && !seats[officerSeat].repairBar,
            $"{where}...and the Officer sits ({(officerSeat >= 0 ? names[officerSeat] : "nowhere")}): not standing at the window counter, not on a stool kept for repairs");
    }

    // The first of the highest scores.
    static int Top(List<float> scores)
    {
        int top = 0;
        for (int k = 1; k < scores.Count; k++) if (scores[k] > scores[top]) top = k;
        return top;
    }

    static float Apart(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------- 7. the open café, without dice

    const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    // Section 6 on the café scene itself, when it is the open scene: its live seats (their tags as the game reads them, sit and
    // stand points, Snap To Seat), the walks from the counter's place 0 along its NavMesh (WaitingArea's own, by name, as the
    // Waiting space check reads it; straight lines when Edit Mode has no NavMesh loaded), its Nearly As Near.
    static void TheOpenCafe()
    {
        Line();
        Line("7. The open café, without dice: its own seats and its walks from the counter's place 0");
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Line($"     Not run: the café scene ({Playtest4Shared.ScenePath}) isn't the open scene. Open it and run the rules again.");
            return;
        }
        WaitingArea area = UnityEngine.Object.FindAnyObjectByType<WaitingArea>(FindObjectsInactive.Exclude);
        CounterQueue queue = UnityEngine.Object.FindAnyObjectByType<CounterQueue>(FindObjectsInactive.Exclude);
        Transform till = queue != null && queue.SlotCount > 0 ? queue.SlotPoint(0) : null;
        MethodInfo walking = typeof(WaitingArea).GetMethod("WalkingDistance", HiddenStatic);
        Check(area != null && till != null && walking != null, "the café has a WaitingArea, the counter's place 0, and WaitingArea's walk (read by name)");
        if (area == null || till == null || walking == null) return;
        Check(SeatTags.AreaCount > 0, $"the room's seat areas are laid ({SeatTags.AreaCount}; Fixit Fidget > Cafe redesign > Seat tags 1, then save the scene)");

        SerializedProperty tieProp = new SerializedObject(area).FindProperty("nearlyAsNear");
        float margin = tieProp != null ? Mathf.Max(0f, tieProp.floatValue) : Margin;
        bool navMesh = NavMesh.SamplePosition(till.position, out NavMeshHit _, 1.5f, NavMesh.AllAreas);
        List<TableSeat> live = UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude)
            .Where(t => t != null && t.isActiveAndEnabled).OrderBy(t => t.name, StringComparer.Ordinal).ToList();
        var walks = new List<float>();
        foreach (TableSeat seat in live)
            walks.Add(navMesh ? (float)walking.Invoke(null, new object[] { till.position, seat.StandPoint.position })
                              : Apart(till.position, seat.StandPoint.position));
        var unreachable = new List<string>();
        for (int k = 0; k < live.Count; k++) if (float.IsPositiveInfinity(walks[k])) unreachable.Add(live[k].name);
        Line($"     {live.Count} seats; walks {(navMesh ? "along the NavMesh (WaitingArea's own)" : "in straight lines (no NavMesh is loaded in Edit Mode)")}; " +
             $"Nearly As Near {margin:0.00} points" + (unreachable.Count > 0 ? $"; no route from the till reaches {string.Join(", ", unreachable)}" : ""));

        // Grace's stool: the repair bar's nearest the bench, the one WaitingArea.ClaimRepairBar gives first.
        StationInteractable bench = UnityEngine.Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude)
            .FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        int grace = -1;
        float nearest = float.PositiveInfinity;
        for (int k = 0; k < live.Count; k++)
        {
            if (!live[k].RepairBar) continue;
            float d = bench != null ? Apart(live[k].SeatPose.position, bench.StandPoint.position) : k;
            if (d < nearest) { nearest = d; grace = k; }
        }
        DayOne("the open café: ", live.Select(t => t.name).ToList(), live.Select(t => t.Zone).ToList(),
               live.Select(t => new SeatChoice.Seat(t.SeatPose.position, t.Tags, t.RepairBar, standing: !t.SnapToSeat)).ToList(), walks, margin, grace);
    }
}
#endif
