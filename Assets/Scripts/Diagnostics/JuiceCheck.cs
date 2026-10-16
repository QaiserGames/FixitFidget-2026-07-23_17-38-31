using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

// ---------------------------------------------------------------------------
// JUICE CHECK: THE FEEDBACK, SHOWN AND PHOTOGRAPHED IN THE CAFÉ (a lab session only: Fixit Fidget > Playtest > Juice -
// Photograph the feedback (lab, Play Mode); 6 Oct 2026, Mansoor's playtest: "it needs to start feeling a lot better in
// terms of juice", and "a lot of people ... react the same way, they just do like a little leaning in the chair")
//
// On the Day 5 lab save, the lab's autopilot keeping the café busy, the view pulled in close, once three people sit in view:
//   1. the badges, every one (nine since the used cup, 7 Oct), over their heads: each pinned over its head, a newer one
//      taking an older one's place;
//   2. a drink served to each of them in turn: how they react (NpcBeats), and how many different reactions the room shows;
//   3. paying: "+$6" and "+$3 tip" over one of them;
//   4. the money in the corner counting up when the till takes $25 (ShopUI), and landing on the till's sum;
//   5. handing over: a cup flies from Ace to one of them and pops into their hands;
//   6. a repair done: the one ending (playtest 4, 10 Oct), "Fixed! · Perfect" on its dark plate with the big sparkle,
//      counted once, its contrast measured (the playtesters: pale green on the freshly lit white screen); and the quiet
//      "Back together · Good" a device closed up with faults left gets instead (no sparkle, not an ending);
//   7. a lasting badge (the man at the bins' mess on a table, playtest 3): up over a point, still up after a pop would
//      have gone, and gone when it's taken down;
//   8. the forgiving bench (14 Oct; systems-forgiving-bench.md §6): the four repairs of the clumsy-hand lab (Grace's camera
//      clean and swap, the phone's screen, the watch's seized movement) done at the bench by the check's hand, each piece
//      waited for until it has landed: each ends once, face up, "Fixed! · Perfect" (Juice.OnFixed once for each), and the
//      fresh part laid on its pad as the close-up opens pops no "New part" word any more (the pad and the glow say it);
// and nothing logs an error the whole time. A photo for each and report.txt go to Logs/Juice/juice-check-<time>/. Nothing is
// saved in the scene; the lab save is the only file written (by the game). The view goes back where it was at the end.
// ---------------------------------------------------------------------------
public sealed class JuiceCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.Juice.Check";

    public string folder;

    readonly StringBuilder report = new StringBuilder();
    readonly List<string> errorLines = new List<string>();
    int checks, failures, photos, errors;
    float started;
    bool lastWait;
    CafeViewMode view;
    Vector3 viewWas;
    bool viewMoved;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

#if UNITY_EDITOR
    // Asked for by the editor (JuiceSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Juice check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Juice check (this Play session only)");
        go.AddComponent<JuiceCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Juice",
            $"juice-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    void OnEnable() => Application.logMessageReceived += Heard;

    void OnDisable() => Application.logMessageReceived -= Heard;

    // Anything that logs an error while the check runs is counted (the check's own lines aside).
    void Heard(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message != null && message.StartsWith("[Juice check]", StringComparison.Ordinal)) return;
        errors++;
        if (errorLines.Count < 5) errorLines.Add(Short((message ?? "").Split('\n')[0]));
    }

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Juice", $"juice-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine("Juice - the feedback, shown and photographed in the café, lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Juice check] Running: a minute or two (people have to sit down first). Hands off the mouse and keyboard until it reports. {folder}");

        var steps = new Stack<IEnumerator>();
        steps.Push(Run());
        while (steps.Count > 0)
        {
            IEnumerator step = steps.Peek();
            bool more;
            try { more = step.MoveNext(); }
            catch (Exception e)
            {
                Check(false, "the check ran to its end (it stopped: " + e.Message + ")");
                Debug.LogException(e);
                break;
            }
            if (!more) { steps.Pop(); continue; }
            if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
            yield return step.Current;
        }
        Finish();
    }

    IEnumerator Run()
    {
        // ---------- the day, Ace, the view pulled in ----------
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && Camera.main != null && ShopEconomy.Instance != null,
            20f, "the lab's day is on");
        if (!lastWait) yield break;
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        view = ace != null ? ace.GetComponent<CafeViewMode>() : null;
        Check(ace != null && view != null, "Ace and the view are in the café");
        if (ace == null || view == null) yield break;
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        viewWas = view.OverheadAngle;
        Vector4 limits = view.OverheadLimits;
        float close = Mathf.Lerp(limits.z, viewWas.z, .35f);   // most of the way to as close as a player can zoom
        view.OrbitTo(viewWas.x, viewWas.y, close);
        viewMoved = true;
        yield return Seconds(1.2f);
        Note($"the view: turned {viewWas.x:0}, tilted {viewWas.y:0}, pulled in from {viewWas.z:0.0} to {close:0.0} (a player can go to {limits.z:0.0})");

        List<NpcSeating> people = null;
        yield return Until(() => (people = SittingInView(Camera.main)).Count >= 3, 150f, "three people are sitting down, in view");
        if (people == null || people.Count == 0) yield break;
        Camera cam = Camera.main;
        Note("sitting, in view: " + string.Join(", ", people.Select(p => p.name)));

        // ---------- 1. the badges ----------
        var icons = (Juice.Icon[])Enum.GetValues(typeof(Juice.Icon));
        int n = Mathf.Min(4, people.Count);
        int round = 0;
        for (int k = 0; k < icons.Length; k += n, round++)
        {
            var shown = new List<(Transform who, Juice.Icon icon)>();
            for (int i = 0; i < n && k + i < icons.Length; i++)
            {
                Transform who = people[i].transform;
                Juice.Emote(who, icons[k + i]);
                shown.Add((who, icons[k + i]));
            }
            yield return Seconds(.4f);
            float rise = 46f * Screen.height / 1080f;
            foreach (var (who, icon) in shown)
            {
                bool up = Juice.ShowingOver(who, out Vector2 at, out string what);
                Vector3 head = cam.WorldToScreenPoint(Barks.HeadPointOf(who));
                float dx = at.x - head.x, dy = at.y - head.y;
                Check(up && what == icon.ToString() && Mathf.Abs(dx) < 10f && dy > -10f && dy < rise + 10f,
                    $"the {icon} badge is up over {who.name}'s head" + (up ? $" ({what}; {dx:0}, {dy:0} px from the head point)" : " (nothing up)"));
            }
            yield return Photo($"1{round}-badges-" + string.Join("-", shown.Select(s => s.icon.ToString().ToLowerInvariant())));
            yield return Seconds(1.3f);   // they float off
        }
        // One at a time over a head: a second badge takes the first one's place.
        Transform twice = people[0].transform;
        Juice.Emote(twice, Juice.Icon.Star);
        Juice.Emote(twice, Juice.Icon.Heart);
        yield return null;
        yield return null;
        Juice.ShowingOver(twice, out _, out string newest);
        Check(newest == nameof(Juice.Icon.Heart) && Juice.PopsUp >= 1, $"a newer badge takes an older one's place over a head (showing {newest})");
        Check(Juice.SortingOrder == 75, $"the badges draw under the barks (sorting order {Juice.SortingOrder}; the barks' is 80)");
        yield return Seconds(1.6f);

        // ---------- 2. a drink served to each in turn: how they react ----------
        people = StillSitting(people, cam);
        if (people.Count == 0) { Check(false, "someone is still sitting down for the rest"); yield break; }
        var reactions = new List<(string who, string what, bool played)>();
        foreach (NpcSeating s in people.Take(6))
        {
            NpcBeats beats = s.GetComponent<NpcBeats>();
            if (beats == null) { reactions.Add((s.name, "no beats on this body", false)); continue; }
            bool atTable = s.Seat != null && NpcBeats.TableInFront(s.Seat);
            bool played = beats.React(NpcBeats.Moment.Served);
            yield return null;
            reactions.Add((s.name, played ? beats.CurrentName + (atTable ? " (at a table)" : " (no table)")
                                          : "nothing played (" + (beats.Ready ? "busy" : "its beats aren't ready") + ")", played));
            yield return Seconds(.25f);
        }
        int playedCount = reactions.Count(r => r.played);
        int kinds = reactions.Where(r => r.played).Select(r => r.what).Distinct().Count();
        foreach (var r in reactions) Note($"served {r.who}: {r.what}");
        Check(playedCount >= 2, $"served, they react: {playedCount} of {reactions.Count} played a reaction");
        Check(playedCount < 2 || kinds >= 2, $"...and not all the same way: {kinds} different reaction(s) among {playedCount}");
        yield return Seconds(.6f);
        yield return Photo("20-served-reactions");
        yield return Seconds(2.6f);

        // ---------- 3. paying ----------
        people = StillSitting(people, cam);
        if (people.Count == 0) { Check(false, "someone is still sitting down for the rest"); yield break; }
        Transform payer = people[Mathf.Min(2, people.Count - 1)].transform;
        Juice.Money(payer, 6, 3);
        yield return Seconds(.8f);     // it comes a beat after the face (8 Oct), then the photo catches it up
        bool paid = Juice.ShowingOver(payer, out _, out string said);
        Check(paid && said.Contains("+$6") && said.Contains("+$3 tip"), $"paying: \"{OneLine(said)}\" over {payer.name}");
        yield return Photo("30-paying");
        yield return Seconds(1.2f);

        // ---------- 4. the money in the corner: today's takings, as a cash stack (playtest 3, session 3) ----------
        HudCorners corners = HudCorners.Instance;
        Check(corners != null && corners.Showing, "the HUD's corners are on screen (today's takings top right)");
        if (corners != null && DayClock.Instance != null)
        {
            int before = DayClock.Instance.Earned;
            int edgesBefore = corners.EdgesShown;
            // As a customer paying does: the till and today's takings both go up.
            ShopEconomy.Instance.AddMoney(25);
            DayClock.Instance.RecordServed(25, 0, false);   // a customer's payment (patrons pay quietly since playtest 4)
            yield return Seconds(.18f);
            string mid = corners.TakingsShown;
            float punch = corners.TakingsPunch;
            yield return Photo("40-money-counting-up");
            yield return Seconds(.9f);
            string end = corners.TakingsShown;
            int today = DayClock.Instance.Earned;
            Check(end == "$" + today, $"today's takings counted up from ${before} and landed on the day's sum ({end}; today ${today})");
            Check(punch > 1.01f || mid != "$" + before && mid != "$" + today,
                $"...moving on the way: {mid} at 0.18 s, {punch:0.00} times its size");
            Check(corners.BillShowing, "a bill lies on top of the stack once there's money today");
            Check(corners.EdgesShown == Mathf.Min(10, today / 24) && corners.EdgesShown >= edgesBefore,
                $"the stack grows by the day's takings: {corners.EdgesShown} edge(s) under the bill for ${today} (one per $24, ten at most)");
            Check(!string.IsNullOrEmpty(corners.SignWord) && corners.ClockLine.Contains(Weekdays.Label(DayClock.Instance.Day)),
                $"the sign reads {corners.SignWord}, and under it \"{corners.ClockLine}\"");
        }

        // ---------- 5. handing over ----------
        people = StillSitting(people, cam);
        if (people.Count == 0) { Check(false, "someone is still sitting down for the rest"); yield break; }
        Transform to = people[Mathf.Min(1, people.Count - 1)].transform;
        var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.name = "Juice check cup";
        Collider solid = cup.GetComponent<Collider>();
        if (solid != null) Destroy(solid);
        cup.transform.localScale = new Vector3(.09f, .055f, .09f);
        Vector3 from = ace.transform.position + Flat(ace.transform.forward).normalized * .45f + Vector3.up * .2f;
        cup.transform.position = from;
        Juice.HandOver(cup, to);
        Destroy(cup);   // as the café does: the real thing goes at once, the copy makes the trip
        yield return Seconds(.16f);
        GameObject ghost = GameObject.Find("Handed over (Juice check cup)");
        float along = ghost != null ? Vector3.Distance(Flat(ghost.transform.position), Flat(from)) : 0f;
        Check(ghost != null && along > .05f, $"handing over: a copy of the cup is on its way to {to.name}" +
                                             (ghost != null ? $" ({along:0.00} m from Ace)" : " (no copy flying)"));
        yield return Photo("50-handing-over");
        yield return Seconds(.45f);
        Check(GameObject.Find("Handed over (Juice check cup)") == null, "...and popped into their hands (the copy's gone)");

        // ---------- 6. a repair done: the one ending (playtest 4), as the bench makes it ----------
        Vector3 bench = ace.transform.position + Flat(ace.transform.forward).normalized * .7f + Vector3.up * .15f;
        int finishesBefore = Juice.Finishes;
        Juice.FixedMoment(bench, bench + Vector3.up * .12f, JobGrade.Perfect);
        yield return Seconds(.12f);
        Check(Juice.SparksUp >= 16, $"a repair done: the big sparkle ({Juice.SparksUp} sparks flying)");
        Check(Juice.PopsUp >= 1 && Juice.Finishes == finishesBefore + 1 && Juice.LastWords == "Fixed! · Perfect",
            $"...and \"{Juice.LastWords}\" pops up, counted once ({Juice.Finishes - finishesBefore})");
        Check(Juice.LastWordsContrast >= 4.5f,
            $"...on its dark plate, legible on any background: {Juice.LastWordsContrast:0.0}:1 over the plate on white, its worst case (4.5:1 is the bar)");
        yield return Photo("60-fixed");
        yield return Seconds(2.3f);   // the ending stays up 2.1 s, a little longer than a word
        // A device closed up with faults left: the quiet line, not an ending.
        Juice.BackTogether(bench + Vector3.up * .12f, JobGrade.Good);
        yield return Seconds(.3f);
        Check(Juice.LastWords == "Back together · Good" && Juice.Finishes == finishesBefore + 1 && Juice.LastWordsContrast >= 4.5f,
            $"closed up with faults left: \"{Juice.LastWords}\", quietly and not counted as an ending, {Juice.LastWordsContrast:0.0}:1 on its plate");
        yield return Photo("61-back-together");
        yield return Seconds(1.6f);
        Note($"a moment later: {Juice.SparksUp} sparks, {Juice.PopsUp} pops up (the café's own may be among them)");

        // ---------- 7. a lasting badge (playtest 3: the man at the bins' mess on a table) ----------
        people = StillSitting(people, cam);
        Vector3 markAt = people.Count > 0 && people[0].Seat != null ? people[0].Seat.CupSpot.position + Vector3.up * .3f
                       : ace.transform.position + Flat(ace.transform.forward).normalized * 1.2f + Vector3.up * .8f;
        int upBefore = Juice.MarksUp;
        int mark = Juice.Mark(markAt, Juice.Icon.Mess);
        yield return Seconds(.5f);
        bool showing = Juice.MarkShowing(mark, out Vector2 markScreen);
        Vector3 wanted = cam.WorldToScreenPoint(markAt);
        float mdx = markScreen.x - wanted.x, mdy = markScreen.y - wanted.y;
        Check(mark != 0 && showing && Juice.MarksUp == upBefore + 1 && Mathf.Abs(mdx) < 10f && Mathf.Abs(mdy) < 12f,
            $"a lasting badge stays over a point ({mdx:0}, {mdy:0} px from it)");
        yield return Photo("70-lasting-badge");
        yield return Seconds(2.2f);   // longer than any pop lives
        Check(Juice.MarkShowing(mark, out _), "...still up after a pop would have gone");
        Juice.Unmark(mark);
        yield return null;
        Check(!Juice.MarkShowing(mark, out _) && Juice.MarksUp == upBefore, "...and gone when it's taken down");

        // ---------- 8. the bench's endings, the four repairs by the check's hand (the forgiving bench, §6) ----------
        yield return BenchEndings(ace);

        Check(errors == 0, errors == 0 ? "nothing logged an error the whole time" : $"{errors} error(s) logged: {string.Join(" | ", errorLines)}");
    }

    // The clumsy-hand lab's four repairs, done at the bench by the check's hand (each part's own Activate, each spot's whole-spot
    // scrub), each piece waited for until it lands: one ending each, face up; no "New part" word as the fresh part is laid out.
    IEnumerator BenchEndings(PlayerMovement ace)
    {
        ItemInspector inspector = ace.GetComponent<ItemInspector>();
        if (inspector == null || BenchStage.Instance == null) { Note("the bench's endings: no inspector or bench stage in the scene: left out"); yield break; }
        if (inspector.IsHoldingItem) inspector.CancelInspection();
        var repairs = new List<(string path, string[] words, int index, string what)>
        {
            ("Assets/GraceShowcase/GraceReunionCamera.prefab", null, 0, "Grace's camera, the clean (fault 0)"),
            ("Assets/GraceShowcase/GraceReunionCamera.prefab", null, 1, "Grace's camera, the swap (fault 1)"),
            ("Assets/AssetsPrefabs/PhoneRepair.prefab", new[] { "crack", "screen" }, -1, "the phone's cracked screen"),
            ("Assets/AssetsPrefabs/PocketWatch.prefab", new[] { "seiz", "spring" }, -1, "the watch's seized movement"),
        };
        var fixedFor = new Dictionary<JobBase, int>();
        Action<JobBase> counted = job => { if (job != null) fixedFor[job] = (fixedFor.TryGetValue(job, out int n) ? n : 0) + 1; };
        Juice.OnFixed += counted;
        try
        {
            foreach (var (path, words, index, what) in repairs)
            {
                GameObject prefab = null;
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
                DeviceDefinition onPrefab = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
                int fault = onPrefab == null || onPrefab.faults == null ? -1 : words == null ? (index < onPrefab.faults.Length ? index : -1)
                    : Array.FindIndex(onPrefab.faults, f => f != null && words.Any(w => (f.description ?? "").ToLowerInvariant().Contains(w)));
                if (fault < 0) { Note($"the bench's endings, {what}: not on this build (no such fault yet): left out"); continue; }
                Vector3 at = ace.transform.position + Flat(ace.transform.forward).normalized * .8f + Vector3.up * .9f;
                GameObject device = Instantiate(prefab, at, Quaternion.identity);
                device.name = "Juice check: " + what;
                var definition = device.GetComponent<DeviceDefinition>();
                JobBase job = device.GetComponent<JobBase>();
                DeviceFault f = definition.GetFault(fault);
                definition.ApplyFault(fault);
                job.Configure(new Job
                {
                    kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = fault,
                    faultType = f.type, faultDescription = f.description, payout = f.payout, number = 80 + fault, color = Color.white
                });
                yield return null;
                yield return null;
                int finishesBefore = Juice.Finishes, popsBefore = Juice.PopsUp;
                string wordsBefore = Juice.LastWords;
                if (!inspector.BeginInspection(job)) { Check(false, $"{what}: the close-up opens on it"); Destroy(device); continue; }
                yield return Seconds(1.2f);
                Check(Juice.LastWords == wordsBefore && Juice.LastWords != "New part",
                    $"{what}: no \"New part\" word as the close-up lays out its stock (the last words: \"{Juice.LastWords}\")");
                yield return ByHand(job);
                float until = Time.realtimeSinceStartup + 3f;
                while (Juice.Finishes == finishesBefore && Time.realtimeSinceStartup < until) yield return null;
                yield return Seconds(.9f);
                fixedFor.TryGetValue(job, out int endings);
                Check(job.IsFinished && Juice.Finishes == finishesBefore + 1 && endings == 1 && Juice.LastWords == "Fixed! · Perfect" && inspector.FaceShowing,
                    $"{what}: it ends once, face up: \"{Juice.LastWords}\" (Juice.OnFixed {endings} time(s); endings {Juice.Finishes - finishesBefore}; {job.Grade})");
                yield return Photo("80-bench-ending-" + new string(what.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant());
                inspector.CancelInspection();
                yield return Seconds(.6f);
                foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
                Destroy(device);
                yield return Seconds(.3f);
            }
        }
        finally { Juice.OnFixed -= counted; }
    }

    // The job's steps by the check's hand in the work's order (the Stations lab's hand): open it up and fix it while work is left,
    // then close it up; each piece is waited for until it lands in its place (or back on the device) before the next.
    IEnumerator ByHand(JobBase job)
    {
        for (int step = 0; step < 60 && job != null && !job.IsComplete; step++)
        {
            var parts = new List<Component>(job.GetComponentsInChildren<Component>());
            foreach (GameObject loose in job.LooseParts) if (loose != null) parts.AddRange(loose.GetComponentsInChildren<Component>());
            bool fixing = job.Quality < .999f;
            Component did = null;
            if (fixing)
            {
                ScrewTarget unscrew = parts.OfType<ScrewTarget>().FirstOrDefault(t => !t.GetComponent<Screw>().IsOut && t.CanInteract);
                RemovablePart lift = parts.OfType<RemovablePart>().FirstOrDefault(c => !c.IsRemoved && c.CanInteract);
                GrimeSpot grime = parts.OfType<GrimeSpot>().FirstOrDefault(g => g != null && !g.IsClean && g.CoveredBy == null);
                ReplaceablePart replace = parts.OfType<ReplaceablePart>().FirstOrDefault(p => p.CanInteract);
                if (unscrew != null) { unscrew.Activate(); did = unscrew; }
                else if (lift != null) { lift.Activate(); did = lift; }
                else if (grime != null) { grime.Scrub(100000f); did = grime; }
                else if (replace != null) { replace.Activate(); did = replace; }
            }
            else
            {
                RemovablePart refit = parts.OfType<RemovablePart>().FirstOrDefault(c => c.InWell && c.CanInteract);
                ScrewTarget rescrew = parts.OfType<ScrewTarget>().FirstOrDefault(t => t.GetComponent<Screw>().IsOut && t.CanInteract);
                if (refit != null) { refit.Activate(); did = refit; }
                else if (rescrew != null) { rescrew.Activate(); did = rescrew; }
            }
            float until = Time.realtimeSinceStartup + (did == null ? .3f : 4f);
            while (Time.realtimeSinceStartup < until && (did == null || !Landed(did))) yield return null;
            yield return Seconds(.1f);
        }
    }

    static bool Landed(Component did)
    {
        if (did == null) return true;
        BenchStage stage = BenchStage.Instance;
        switch (did)
        {
            case ScrewTarget target:
            {
                Screw screw = target.GetComponent<Screw>();
                return screw == null || !screw.IsBusy && !screw.Flying && (!screw.IsOut || screw.InCup && (stage == null || stage.InPlace(screw)));
            }
            case RemovablePart cover:
                return !cover.Flying && !cover.Popping && (cover.State == RemovablePart.CoverState.Seated || cover.InWell);
            case ReplaceablePart part:
                return part.IsReplaced && (stage == null || !stage.IsFlying(part));
            default:
                return true;
        }
    }

    // Everyone sitting down whose head is on screen, the middle of the view first.
    static List<NpcSeating> SittingInView(Camera cam)
    {
        var list = new List<NpcSeating>();
        if (cam == null) return list;
        foreach (NpcSeating s in FindObjectsByType<NpcSeating>())
        {
            if (s.Current != NpcSeating.Phase.Seated || s.Seat == null) continue;
            Vector3 v = cam.WorldToViewportPoint(Barks.HeadPointOf(s.transform));
            if (v.z > 0f && v.x > .08f && v.x < .92f && v.y > .12f && v.y < .85f) list.Add(s);
        }
        list.Sort((a, b) => FromMiddle(cam, a.transform).CompareTo(FromMiddle(cam, b.transform)));
        return list;
    }

    // Those still sitting (people come and go while the lab runs), topped up with anyone else sitting in view.
    static List<NpcSeating> StillSitting(List<NpcSeating> people, Camera cam)
    {
        var list = people.Where(s => s != null && s.Current == NpcSeating.Phase.Seated && s.Seat != null).ToList();
        foreach (NpcSeating s in SittingInView(cam)) if (!list.Contains(s)) list.Add(s);
        return list;
    }

    static float FromMiddle(Camera cam, Transform t)
    {
        Vector3 v = cam.WorldToViewportPoint(t.position);
        return new Vector2(v.x - .5f, v.y - .5f).sqrMagnitude;
    }

    static T Field<T>(object owner, string name) where T : class
    {
        if (owner == null) return null;
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Any);
            if (field != null) return field.GetValue(owner) as T;
        }
        return null;
    }

    // ---------- waiting, photos and the report ----------

    IEnumerator Until(Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
        Check(lastWait, what + (lastWait ? "" : $" (gave up after {seconds:0} s)"));
    }

    static bool Safe(Func<bool> condition)
    {
        try { return condition(); }
        catch (Exception) { return false; }
    }

    static IEnumerator Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    IEnumerator Photo(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), shot.EncodeToJPG(88));
        Destroy(shot);
    }

    void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    void Note(string what) => report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");

    static string Short(string line) => string.IsNullOrEmpty(line) ? "" : line.Length <= 90 ? line : line.Substring(0, 87) + "...";
    static string OneLine(string text) => (text ?? "").Replace("\n", " / ");
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Finish()
    {
        if (viewMoved && view != null) view.OrbitTo(viewWas.x, viewWas.y, viewWas.z);
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Juice check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Juice check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Juice check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }
}
