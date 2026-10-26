using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// SEATS CHOSEN THE WAY PEOPLE CHOOSE THEM (23 Oct 2026)
//
// His note (his own playtest, 22 Oct): "when customers come in to do a coffee order, they literally just only sit to the
// left of the couch. And then when a customer comes in to do a repair, they only sit on the right next to the bar stool.
// And I want that to be more spread out and diverse in a sense, because I want there to be a feel for the player to like
// kind of look for the customer that did the order in a sense as well."
//
// Why it happened: since 27 Sept everyone took the NEAREST free seat by walking distance (that replaced a random free seat,
// which "read as automated": people walking past empty chairs to one across the café). From the till the nearest is the
// lounge's west tub chair, every time ("to the left of the couch"); and since 14 Oct a bench job takes the repair bar's
// stool nearest the bench, the middle one first (her playtest 5 note). So the same three or four seats got used all day.
//
// Now a seat's SCORE for a person is a sum of reasons, in points (a point is about three metres of walking for someone in
// no hurry), and they take the best; seats within WaitingArea's Nearly As Near of the best are near-equals and one of them
// is chosen at random, so two people don't mirror each other. The reasons:
//   + what the seat is, by the person's character: the Leanings below. A Chatty person likes the communal table, a Cheerful
//     one the window, a Shy one a quiet corner, a Relaxed or Sentimental one something soft. They come from the walk-in's
//     personality (CustomerArchetype.archetypeName) and from how everyone moves (NpcMovementProfile: walk-ins, patrons and
//     regulars all have one), added together;
//   + a regular's favourite, when one is free (Favourites): Grace a soft seat by the window, the Officer a stool at the
//     window counter (asleep until its stools sit: below). Big enough that they take it if it's free; when none is, they
//     score like anyone else;
//   - strangers close by (personal space): one at the very next seat costs Next To, one at the same table Same Table. A quiet
//     café spreads out across the room; a busy one fills in, because then every seat has someone near it;
//   - the walk, but less than the reasons (Walk Weight a metre, more for a Rushed or Hurried person): the reasons outweigh
//     the walk and the walk breaks the ties, so nobody crosses the café past a seat as good without a reason;
//   - a repair-bar stool, for anyone whose own thing isn't on the bench (Kept For Repairs: her playtest 5 note, and those
//     three stools are the only seats Ace reaches over the counter from the bench);
//   - a seat nobody sits on (Snap To Seat off: the window counter's three stools, since Seats 1), where one waits standing
//     (Standing Wait; her playtest 5 note again, "people wouldn't sit down on the bar stools"): overflow only, and never a
//     favourite. Making those stools sit, as NPC wave 2 did for the repair bar's, is a scene job of its own; this reads the
//     seat, so it lifts itself then.
//
// THE REPAIR BAR. A bench job still takes the repair bar's stools first (WaitingArea.ClaimRepairBar), unless their character
// wants comfort more than the bar (StaysAtTheBar): a Sentimental person, or a Relaxed one with a long repair (a part to swap,
// a support call on hold) who isn't the Impatient or Rushed sort that watches. About one walk-in repair in three (two in
// five over Days 2-3's people, one in four once the Impatient and the Rushed come), none on a Day 1 of short cleans. Then
// they take a seat like anyone else and Ace carries the device to them: the hand-back and a reassurance work wherever a
// customer waits, the interact button within arm's reach (CustomerInteractable.FloorAvailable), and what already points at
// a customer finds them anywhere: their ticket carries their name and a tape in their job's colour, the patience bar over
// their head a dot of that colour, the device's tag that colour too (UiPins, JobTicket), and facing them, their name
// (FocusNameUI). Regulars stay at the bar while their own thing is on the bench: Grace tells her stories while she waits
// (CustomerStoryteller never spends a line off screen), and the bar keeps her beside where Ace works.
//
// 24 OCT 2026 (Days; the 23 Oct run, his c11 kept: "From Day 2, about 2 in 5 repair customers sit away from the bar; never on
// Day 1"). In six Day 2-3 runs only 1 of 11 walk-in bench jobs left the bar: only a Sentimental person, or a Relaxed one with a
// long repair, ever did, and those days' walk-ins were Cheerful, Chatty and Impatient. Now, from Day 2, each walk-in bench job
// rolls once a visit (AwayChance, on dice of its own: the game's random sequence is left alone) whether to sit away from the
// bar, about two in five over Days 2-3's people, biased by character: the watchers (Impatient, Rushed) less, the comfort-seekers
// (Sentimental, Relaxed, a long repair) more. Never on Day 1, and regulars stay. StaysAtTheBar(Person) is still the character's
// own leaning, which the roll's bias is made of. And the stool itself (WaitingArea.ClaimRepairBar) is chosen like any seat now:
// personal space and near-equals at random, so the middle stool is no longer everyone's.
//
// Every number is here, in one place: Tuning, Leanings, Favourites. Tune here.
//
// THE TAGS. A seat's tags come from a SeatTags on the seat or on any parent of it (a piece of furniture tags all its
// seats), else from the seat itself (DefaultFor: a raised seat is at a bar, a bench seat is soft), plus the tags of every
// room AREA its sit point is in (a SeatTags with an Area: the windows, the door, a quiet corner). So a table placed
// anywhere picks up where it stands, and nothing in the scoring knows a seat's name: only tags and positions. A seat placed
// at run time needs nothing else: it registers itself with WaitingArea when it is switched on (WaitingSpot.OnEnable) and
// is weighed from its next claim on. Fixit Fidget > Cafe redesign > Seat tags 1 tags the café's own seats and lays the
// room's areas (SeatTagsSteps). The checks: Fixit Fidget > Checks > Seat choice rules (SeatChoiceRules); the Day labs
// count the seats and zones a day uses (DayShapeCheck).
// ---------------------------------------------------------------------------

/// <summary>What a seat is, or where it stands (SeatTags). Several at once.</summary>
[Flags]
public enum SeatTag
{
    None = 0,
    /// <summary>By a window: something to look at.</summary>
    Window = 1 << 0,
    /// <summary>Soft and low ("comfy"): a tub chair, a sofa, a banquette's cushion.</summary>
    Lounge = 1 << 1,
    /// <summary>A stool at a counter.</summary>
    Bar = 1 << 2,
    /// <summary>A long shared table: you sit with strangers.</summary>
    Communal = 1 << 3,
    /// <summary>A quiet corner, out of the way of the door, the till and the aisle.</summary>
    Quiet = 1 << 4,
    /// <summary>By the door: in and out quickly.</summary>
    NearDoor = 1 << 5,
}

/// <summary>
/// Tags for seats. On a seat, or on a piece of furniture (every seat on it or under it): what they are. With an Area: a
/// part of the room (every seat whose sit point is inside it gets these tags as well, wherever it was placed).
/// </summary>
[DisallowMultipleComponent]
public sealed class SeatTags : MonoBehaviour
{
    [Tooltip("What these seats are (on a seat, or a piece of furniture: every seat on it or under it), or, with an Area, " +
             "what this part of the room is. Several at once.")]
    [SerializeField] private SeatTag tags;

    [Tooltip("A short name for the zone these seats make (\"window counter\"), for the labs and the reports only: it never " +
             "chooses a seat. Empty: the seat's parent's name.")]
    [SerializeField] private string zone = "";

    [Tooltip("Zero: these tags are for the seats on this object and under it. Not zero: an area of the room this size (metres, " +
             "in this object's own frame, centred on it); every seat whose sit point is inside it gets these tags too, wherever " +
             "it stands: the windows, the door, a quiet corner.")]
    [SerializeField] private Vector3 area;

    /// <summary>A raised seat (its seat this far over its stand point, metres) is at a bar: a bar stool is 0.745, a café chair 0.445.</summary>
    public const float RaisedSeat = .6f;

    public SeatTag Tags => tags;
    public string Zone => zone ?? "";
    public Vector3 Area => area;
    public bool IsArea => area.x > 0f && area.y > 0f && area.z > 0f;

    /// <summary>Set by code (a placed piece, a check). With an area it joins the room's areas while it is on (in Play; Edit
    /// Mode looks areas up instead).</summary>
    public void Set(SeatTag seatTags, string zoneName, Vector3 areaSize = default)
    {
        tags = seatTags;
        zone = zoneName ?? "";
        area = areaSize;
        areas.Remove(this);
        if (Application.isPlaying && IsArea && isActiveAndEnabled) areas.Add(this);
    }

    /// <summary>Whether a point in the world is inside this area (never, for a seat's or a piece's own tags).</summary>
    public bool Contains(Vector3 world)
    {
        if (!IsArea) return false;
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= area.x * .5f && Mathf.Abs(p.y) <= area.y * .5f && Mathf.Abs(p.z) <= area.z * .5f;
    }

    // The room's areas while playing (they register as they are switched on, as the seats do with WaitingArea). Edit Mode
    // sends no OnEnable to a script without ExecuteAlways, so there the step's report and the checks look them up instead:
    // every area switched on in a loaded scene, the open one or a check's preview scene, never a prefab asset (its scene
    // isn't valid). "Switched on" is read as enabled on an active object there, not isActiveAndEnabled, which may wait for
    // an OnEnable that Edit Mode never sends.
    private static readonly List<SeatTags> areas = new List<SeatTags>();
    private static readonly List<SeatTags> found = new List<SeatTags>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        areas.Clear();
        found.Clear();
    }

    private void OnEnable()
    {
        if (IsArea && !areas.Contains(this)) areas.Add(this);
    }

    private void OnDisable() => areas.Remove(this);

    /// <summary>How many room areas are switched on: none means Seat tags 1 hasn't laid them in this scene (the Day labs ask).</summary>
    public static int AreaCount => Areas().Count;

    private static List<SeatTags> Areas()
    {
        if (Application.isPlaying) return areas;
        found.Clear();
        foreach (SeatTags t in Resources.FindObjectsOfTypeAll<SeatTags>())
            if (t != null && t.gameObject.scene.IsValid() && t.enabled && t.gameObject.activeInHierarchy && t.IsArea) found.Add(t);
        return found;
    }

    /// <summary>
    /// A seat's tags: its own SeatTags or a parent's (never an area's), else its own shape (DefaultFor), plus every area its
    /// sit point is in.
    /// </summary>
    public static SeatTag Of(TableSeat seat)
    {
        if (seat == null) return SeatTag.None;
        SeatTags own = On(seat);
        return (own != null ? own.tags : DefaultFor(seat)) | AreasAt(seat.SeatPose.position);
    }

    /// <summary>The seat's own SeatTags, or the nearest parent's (a piece of furniture's), never an area; null when none.</summary>
    public static SeatTags On(TableSeat seat)
    {
        for (Transform t = seat != null ? seat.transform : null; t != null; t = t.parent)
        {
            SeatTags own = t.GetComponent<SeatTags>();
            if (own != null && own.enabled && !own.IsArea) return own;
        }
        return null;
    }

    /// <summary>
    /// An untagged seat's tags, from the seat itself: a raised seat (a bar stool) is at a bar, a bench seat (a banquette, a
    /// sofa) is soft. Anything else is plain: chosen by who is near it and the walk, and liked by nobody in particular.
    /// </summary>
    public static SeatTag DefaultFor(TableSeat seat)
    {
        if (seat == null) return SeatTag.None;
        SeatTag t = SeatTag.None;
        if (seat.SeatPose.position.y - seat.StandPoint.position.y > RaisedSeat) t |= SeatTag.Bar;
        if (seat.Style == TableSeat.SitStyle.Bench) t |= SeatTag.Lounge;
        return t;
    }

    /// <summary>The tags of every room area a point is in.</summary>
    public static SeatTag AreasAt(Vector3 world)
    {
        SeatTag t = SeatTag.None;
        List<SeatTags> list = Areas();
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].Contains(world)) t |= list[i].tags;
        return t;
    }

    /// <summary>The zone a seat belongs to, for the labs and the reports (never for choosing): its SeatTags' zone, else its parent's name.</summary>
    public static string ZoneOf(TableSeat seat)
    {
        if (seat == null) return "";
        SeatTags own = On(seat);
        if (own != null && !string.IsNullOrWhiteSpace(own.zone)) return own.zone.Trim();
        Transform parent = seat.transform.parent;
        return parent != null ? parent.name : seat.name;
    }

    // An area shows in the Scene view as a box, the colour of its first tag, so its edges can be checked against the room.
    private void OnDrawGizmos()
    {
        if (!IsArea) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = (tags & SeatTag.Window) != 0 ? new Color(.35f, .7f, 1f, .8f)
            : (tags & SeatTag.NearDoor) != 0 ? new Color(1f, .75f, .25f, .8f)
            : new Color(.6f, .9f, .5f, .8f);
        Gizmos.DrawWireCube(Vector3.zero, area);
        Gizmos.matrix = Matrix4x4.identity;
    }
}

/// <summary>
/// How a person chooses a seat (23 Oct 2026; see the header of SeatTags.cs): the numbers, the table of leanings, and the score.
/// Pure: no scene, so the rules check it without Play (SeatChoiceRules). WaitingArea asks it for every claim.
/// </summary>
public static class SeatChoice
{
    // ================================================================== Tuning (points: a point is about 3 m of walking)

    /// <summary>What a metre of walking costs someone in no hurry, in points (a Rushed or Hurried person counts it more: Leanings).</summary>
    public const float WalkWeight = .3f;
    /// <summary>A stranger at the very next seat (their seat within Next To Within), or at the same table (within Same Table Within).</summary>
    public const float NextTo = 3f, SameTable = 1.5f;
    /// <summary>Seat to seat, metres: the repair bar's stools and the communal table's along a row stand 0.9-1.0 m apart (the
    /// next seat); the window counter's stools 1.2 and the bistro's two across their table 1.18 (those count as one table, as
    /// do across a banquette 2-top or the communal table, 1.5-1.6). 1.25 would make the bistro's pair next-seat strangers.</summary>
    public const float NextToWithin = 1.15f, SameTableWithin = 1.9f;
    /// <summary>A repair-bar stool for anyone whose own repair isn't on the bench: the stools are the repair customers'.</summary>
    public const float KeptForRepairs = 1.5f;
    /// <summary>A seat nobody sits on (Snap To Seat off: the window counter's stools, until they sit) is waited at standing,
    /// and her playtest 5 note was "people wouldn't sit down on the bar stools". As bad as a stranger at the next seat, so
    /// it is overflow only: taken when nothing near as good is free. It reads the seat, so it goes once a seat sits.</summary>
    public const float StandingWait = 3f;
    /// <summary>A regular's favourite seat, free: more than any walk in the café, so they take it if one is free.</summary>
    public const float Favourite = 20f;
    /// <summary>What the repair bar is worth to someone whose thing is on the bench (watching it, Ace across the counter): more comfort than this takes them elsewhere.</summary>
    public const float RepairBar = 2f;
    /// <summary>A long repair (a part to swap, a support call on hold): they settle in, and soft seats count this much more.</summary>
    public const float LongRepairComfort = 2f;
    /// <summary>How much shorter than the straight line a walk can measure (the route starts where the NavMesh snaps them), metres:
    /// keeps WaitingArea's search exact for anyone standing on the NavMesh (an agent snaps within centimetres; the search looks 1.5 m).</summary>
    public const float StraightSlack = .5f;

    // ================================================================== Leanings (who likes what; tune here)

    /// <summary>One sort of person's leanings: points for each tag (negative: avoids it), and how much the walk and strangers count.</summary>
    public sealed class Leaning
    {
        /// <summary>Matched against the walk-in's personality and against everyone's way of moving (ignoring case; a name containing it counts).</summary>
        public readonly string who;
        public readonly float window, lounge, bar, communal, quiet, nearDoor;
        /// <summary>The walk counts this many times as much (a Rushed person takes the nearest).</summary>
        public readonly float walk;
        /// <summary>Strangers close by count this many times as much (a Chatty person likes company: less; a Shy one: more).</summary>
        public readonly float space;
        /// <summary>A reason of their own to stay at the repair bar while their thing is on the bench: they want to watch (an
        /// Impatient or Rushed person's 2 keeps them there even with a Relaxed walk and a long repair, comfort 3.5).</summary>
        public readonly float watches;

        public Leaning(string who, float window = 0f, float lounge = 0f, float bar = 0f, float communal = 0f, float quiet = 0f,
                       float nearDoor = 0f, float walk = 1f, float space = 1f, float watches = 0f)
        {
            this.who = who;
            this.window = window;
            this.lounge = lounge;
            this.bar = bar;
            this.communal = communal;
            this.quiet = quiet;
            this.nearDoor = nearDoor;
            this.walk = walk;
            this.space = space;
            this.watches = watches;
        }
    }

    /// <summary>
    /// Who likes what. The first five are the walk-ins' personalities (CustomerSpawner's archetypes; the repo's Days 1-3
    /// assets allow Cheerful and Chatty, and Sentimental from Day 2), the last five how people move (NpcProfileLibrary:
    /// walk-ins, patrons and regulars each have one). A person gets every row whose name is theirs, added together (a
    /// Cheerful, Relaxed walk-in likes the window and soft seats).
    /// </summary>
    public static readonly Leaning[] Leanings =
    {
        new Leaning("Cheerful", window: 3f),
        new Leaning("Chatty", communal: 3f, space: .5f),
        new Leaning("Sentimental", lounge: 2.5f, quiet: 1.5f),
        new Leaning("Impatient", bar: 1.5f, walk: 2f, watches: 2f),
        new Leaning("Rushed", nearDoor: 1.5f, walk: 3f, watches: 2f),
        new Leaning("Relaxed", lounge: 1.5f),
        new Leaning("Hurried", nearDoor: 1f, walk: 1.5f),
        new Leaning("Shy", window: 1f, communal: -2f, quiet: 2f, space: 1.5f),
        new Leaning("Social", communal: 1.5f, space: .6f),
        new Leaning("Distracted", window: 1.5f),
    };

    /// <summary>
    /// A regular's favourite seat, as the tags it must have (every one of them): Grace a soft seat by the window (the
    /// banquette's cushion, the reading corner), the Officer a stool at the window counter (Bar and Window: the repair bar's
    /// stools are Bar alone, and stay the repair customers'). A favourite never points at a seat nobody sits on (Reasons), so
    /// the Officer's sleeps until the window counter's stools sit; until then he chooses like anyone, his Distracted way of
    /// moving liking a window. Keyed by the profile's save id (CustomerProfile.PersistentId).
    /// </summary>
    public static readonly (string regular, SeatTag favourite)[] Favourites =
    {
        (GraceCameraEpisode.ProfileId, SeatTag.Window | SeatTag.Lounge),
        (OfficerStory.ProfileId, SeatTag.Bar | SeatTag.Window),
    };

    // ================================================================== a person

    /// <summary>Who is choosing: their leanings added up, and what they're here for. Made by Make or PersonOf (a bare default
    /// counts no walk and no strangers).</summary>
    public struct Person
    {
        /// <summary>The walk-in's personality, their way of moving, a regular's save id: what the leanings were read from ("" when none).</summary>
        public string sort, manner, regular;
        public float window, lounge, bar, communal, quiet, nearDoor;
        public float walk, space, watches;
        /// <summary>A regular's favourite seat: the tags it must have; None for anyone else.</summary>
        public SeatTag favourite;
        /// <summary>Their repair is worked at the bench (the repair bar is theirs), and it's a long one.</summary>
        public bool benchJob, longJob;

        public bool IsRegular => !string.IsNullOrEmpty(regular);

        /// <summary>Their leanings for every tag in <paramref name="tags"/>, added up.</summary>
        public float Pull(SeatTag tags)
        {
            float p = 0f;
            if ((tags & SeatTag.Window) != 0) p += window;
            if ((tags & SeatTag.Lounge) != 0) p += lounge;
            if ((tags & SeatTag.Bar) != 0) p += bar;
            if ((tags & SeatTag.Communal) != 0) p += communal;
            if ((tags & SeatTag.Quiet) != 0) p += quiet;
            if ((tags & SeatTag.NearDoor) != 0) p += nearDoor;
            return p;
        }

        public override string ToString()
        {
            var said = new List<string>();
            if (!string.IsNullOrEmpty(sort)) said.Add(sort);
            if (!string.IsNullOrEmpty(manner)) said.Add(manner);
            if (!string.IsNullOrEmpty(regular)) said.Add("regular " + regular);
            if (benchJob) said.Add(longJob ? "a long repair on the bench" : "a repair on the bench");
            return said.Count > 0 ? string.Join(", ", said) : "nobody in particular";
        }
    }

    /// <summary>A person from what is known about them (any part may be empty: someone with no leanings at all).</summary>
    public static Person Make(string sort = "", string manner = "", string regular = "", bool benchJob = false, bool longJob = false)
    {
        var p = new Person
        {
            sort = sort ?? "", manner = manner ?? "", regular = regular ?? "",
            walk = 1f, space = 1f, benchJob = benchJob, longJob = benchJob && longJob,
        };
        foreach (Leaning l in Leanings)
        {
            if (l == null || !Named(p.sort, l.who) && !Named(p.manner, l.who)) continue;
            p.window += l.window;
            p.lounge += l.lounge;
            p.bar += l.bar;
            p.communal += l.communal;
            p.quiet += l.quiet;
            p.nearDoor += l.nearDoor;
            p.walk *= l.walk;
            p.space *= l.space;
            p.watches += l.watches;
        }
        // Someone with a long repair likes comfort: they're settling in.
        if (p.longJob) p.lounge += LongRepairComfort;
        foreach (var f in Favourites)
            if (p.regular.Length > 0 && string.Equals(f.regular, p.regular, StringComparison.OrdinalIgnoreCase)) p.favourite |= f.favourite;
        return p;
    }

    private static bool Named(string name, string who) =>
        !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(who) && name.IndexOf(who, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// The person claiming a spot: a customer (their personality, their way of moving, a regular's id, their job), a patron
    /// (their way of moving), or anything else (nobody in particular).
    /// </summary>
    public static Person PersonOf(Component occupant)
    {
        if (occupant == null) return Make();
        CustomerBrain brain = occupant as CustomerBrain;
        if (brain == null) brain = occupant.GetComponent<CustomerBrain>();
        CustomerIdentity identity = brain != null && brain.Identity != null ? brain.Identity : occupant.GetComponent<CustomerIdentity>();
        NpcSocial social = occupant.GetComponent<NpcSocial>();
        string sort = identity != null && !identity.IsRegular && identity.Archetype != null ? identity.Archetype.archetypeName : "";
        string manner = social != null && social.Profile != null ? social.Profile.Name : "";
        string regular = identity != null && identity.IsRegular ? identity.Profile.PersistentId : "";
        bool bench = brain != null && brain.GoesToTheBench;
        bool longJob = bench && brain.Record != null
            && (brain.Record.faultType == FaultType.Mechanical || brain.Record.faultType == FaultType.Bureaucratic);
        return Make(sort, manner, regular, bench, longJob);
    }

    /// <summary>
    /// A bench job's own leaning: they'd wait at the repair bar unless comfort means more to them than the bar: their soft-seat
    /// leaning (a long repair adds to it) over Repair Bar plus their own wish to watch. Regulars stay at the bar with their own
    /// thing on the bench (they talk to Ace while he works). Anyone without a bench job: false. (24 Oct 2026: where they wait on
    /// a day is StaysAtTheBar(who, day, dice), c11's roll, biased by this same character.)
    /// </summary>
    public static bool StaysAtTheBar(Person who)
    {
        if (!who.benchJob) return false;
        if (who.IsRegular) return true;
        return who.lounge <= RepairBar + who.watches;
    }

    // ================================================================== c11: who sits away from the bar (24 Oct 2026)

    /// <summary>c11, kept by him: from Day 2 about this share of walk-in bench jobs sit away from the bar ("about 2 in 5").</summary>
    public const float AwayShare = .4f;
    /// <summary>The roll's base, and what each point of comfort (the soft-seat leaning: Sentimental 2.5, Relaxed 1.5, a long
    /// repair 2) adds and each point of wanting to watch (Impatient, Rushed: 2) takes off; then held within Away Least..Most.
    /// Over Days 2-3's walk-ins (Cheerful, Chatty, Sentimental, Impatient by the five ways of moving, Day 3's repairs mostly
    /// long) it comes to about 0.4 (SeatChoiceRules simulates it): a Cheerful walk-in with a short repair 0.36, a Sentimental,
    /// Relaxed one with a long repair 0.60, an Impatient one with a short repair 0.20.</summary>
    public const float AwayBase = .36f, AwayPerComfort = .04f, AwayPerWatch = .08f, AwayLeast = .12f, AwayMost = .7f;
    /// <summary>The first day anyone sits away from the bar (c11: "never on Day 1": the tutorial's reassure over the counter).</summary>
    public const int AwayFromDay = 2;

    /// <summary>
    /// The chance this bench job sits away from the bar on <paramref name="day"/> (c11): 0 for anyone without a bench job, a
    /// regular, or before Day 2; else the base, more for comfort, less for wanting to watch.
    /// </summary>
    public static float AwayChance(Person who, int day)
    {
        if (!who.benchJob || who.IsRegular || day < AwayFromDay) return 0f;
        return Mathf.Clamp(AwayBase + AwayPerComfort * who.lounge - AwayPerWatch * who.watches, AwayLeast, AwayMost);
    }

    /// <summary>
    /// Where a bench job waits on <paramref name="day"/> (24 Oct 2026): at the repair bar, unless their roll this visit
    /// (<paramref name="dice"/>, 0..1, the same all visit) falls under AwayChance. Day 1 and regulars: always the bar. Anyone
    /// without a bench job: false.
    /// </summary>
    public static bool StaysAtTheBar(Person who, int day, float dice)
    {
        if (!who.benchJob) return false;
        return dice >= AwayChance(who, day);
    }

    /// <summary>
    /// 0..1 from a person (their identity this visit) and a salt: the same answer however often it is asked, and the game's
    /// random sequence untouched (PlacementJitter's way: the walk-ins' rolls and the first customer's walk keep theirs).
    /// </summary>
    public static float Dice(object who, int salt)
    {
        if (who == null) return .5f;
        unchecked
        {
            uint h = (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(who) * 2654435761u;
            h ^= (uint)salt * 2246822519u + 0x9E3779B9u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>One of <paramref name="count"/> near-equals for this person (Dice): the same pick however often it is asked.</summary>
    public static int DiceIndex(object who, int salt, int count) =>
        count <= 1 ? 0 : Mathf.Clamp(Mathf.FloorToInt(Dice(who, salt) * count), 0, count - 1);

    // ================================================================== a seat, and the score

    /// <summary>A place to wait, as the score sees it: where one sits there, its tags, whether it is a repair-bar stool, and
    /// whether it is waited at standing (a seat nobody sits on: TableSeat.SnapToSeat off).</summary>
    /// <summary>
    /// Where a seat is, in the Now line's words (24 Oct 2026; Designer P's §2.1, from these tags): "by the window", "in the
    /// lounge", "at the bar", "at the long table", "in the quiet corner", "by the door"; "" for an untagged seat. A seat with
    /// several tags is called by the first of these (what a player sees first). Pure: the Downtime rules check it.
    /// </summary>
    public static string PlaceWords(SeatTag tags)
    {
        if ((tags & SeatTag.Window) != 0) return "by the window";
        if ((tags & SeatTag.Lounge) != 0) return "in the lounge";
        if ((tags & SeatTag.Bar) != 0) return "at the bar";
        if ((tags & SeatTag.Communal) != 0) return "at the long table";
        if ((tags & SeatTag.Quiet) != 0) return "in the quiet corner";
        if ((tags & SeatTag.NearDoor) != 0) return "by the door";
        return "";
    }

    public struct Seat
    {
        public Vector3 at;
        public SeatTag tags;
        public bool repairBar;
        public bool standing;

        public Seat(Vector3 at, SeatTag tags, bool repairBar = false, bool standing = false)
        {
            this.at = at;
            this.tags = tags;
            this.repairBar = repairBar;
            this.standing = standing;
        }
    }

    /// <summary>The seat's reasons for this person, before the walk and the strangers: their leanings, their favourite (never a
    /// seat nobody sits on), the bar kept for repairs, and a standing wait (Standing Wait).</summary>
    public static float Reasons(Person who, Seat seat)
    {
        float r = who.Pull(seat.tags);
        if (who.favourite != SeatTag.None && !seat.standing && (seat.tags & who.favourite) == who.favourite) r += Favourite;
        if (seat.repairBar && !who.benchJob) r -= KeptForRepairs;
        if (seat.standing) r -= StandingWait;
        return r;
    }

    /// <summary>What the strangers near a place cost this person (personal space): every one at the next seat or the same table.</summary>
    public static float Crowding(Person who, Vector3 at, IReadOnlyList<Vector3> others)
    {
        if (others == null) return 0f;
        float c = 0f;
        for (int i = 0; i < others.Count; i++)
        {
            Vector3 d = others[i] - at;
            d.y = 0f;
            float apart = d.magnitude;
            if (apart < NextToWithin) c += NextTo;
            else if (apart < SameTableWithin) c += SameTable;
        }
        return c * who.space;
    }

    /// <summary>What walking this far costs this person. No way there at all (WaitingArea: an infinite walk) costs everything,
    /// whoever they are.</summary>
    public static float WalkCost(Person who, float metres) =>
        float.IsPositiveInfinity(metres) ? float.PositiveInfinity : Mathf.Max(0f, metres) * WalkWeight * who.walk;

    /// <summary>A seat's whole score for this person: the reasons, less the strangers near it and the walk there (metres).</summary>
    public static float Score(Person who, Seat seat, IReadOnlyList<Vector3> others, float walkMetres) =>
        Reasons(who, seat) - Crowding(who, seat.at, others) - WalkCost(who, walkMetres);

    /// <summary>The near-equals: every index whose score is within <paramref name="margin"/> of the best (one is chosen at random).</summary>
    public static void NearlyBest(IReadOnlyList<float> scores, float margin, List<int> into)
    {
        into.Clear();
        if (scores == null || scores.Count == 0) return;
        float best = float.NegativeInfinity;
        for (int i = 0; i < scores.Count; i++) best = Mathf.Max(best, scores[i]);
        for (int i = 0; i < scores.Count; i++) if (scores[i] >= best - Mathf.Max(0f, margin)) into.Add(i);
    }
}
