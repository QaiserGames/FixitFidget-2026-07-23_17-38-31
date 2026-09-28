using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: NEIGHBOURS COMING HOME (claude/night-city-proposal.md §9)
//
// A few neighbours are still out when the night begins (their houses are dark: NightHomes). At
// their hour each one appears somewhere out of sight, walks along the pavement to their own front
// door, lets themselves in (the door opens for them and closes behind them, the way the café's
// visitors use it: StreetDoor, NpcJourney), and a moment later a room lights up; later, one
// upstairs. They are nobody in particular: no names, no notebook entries, nothing to do with them.
//
//   * They only appear where the camera can't see (checked every second). If their starting spot
//     stays in view for 90 s (almost two hours of the night), they came home the back way: the room
//     lights anyway.
//   * The café's visitors' walking code does the walking (NpcJourney: keeping their line, stepping
//     round Ace, waiting for their turn at the door); CafeArrivals, which normally holds doors open,
//     rests at night, so the door is held here, the same way.
//
// Only while a night walk runs: NightWalk adds it, calls Begin, Tick every frame, and Clear.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightNeighbours : MonoBehaviour
{
    const float GiveUpUnseenAfter = 90f;     // seconds their spot may stay in view before they come in the back way
    const float WalkTooLong = 150f;          // a walk that takes longer than this is finished for them
    const float DoorReach = 2f;              // metres in front of a door where it opens for them (as CafeArrivals)
    const float PavementRoom = .3f, StoopRoom = .25f, DoorwayRoom = .1f;

    sealed class Walker
    {
        public NightWalk.Neighbour who;
        public Transform house;
        public StreetDoor door;
        public GameObject body;
        public NpcJourney journey;
        public string state = "not yet";
        public float dueSince = -1f, walkingSince, closingSince;
        public bool closing;
        public string rooms = "";
        public float cameHome = -1f;
    }

    readonly List<Walker> walkers = new();
    NightHomes homes;
    GameObject prefab;
    float nextPlayerScan, nextSightCheck;
    float hour;

    /// <summary>Neighbours home so far.</summary>
    public int Home { get; private set; }
    /// <summary>Neighbours walking right now.</summary>
    public int Walking { get; private set; }

    public void Begin(NightWalk.Neighbour[] list, NightHomes nightHomes, GameObject body, float startHour)
    {
        Clear();
        homes = nightHomes;
        prefab = body;
        hour = startHour;
        foreach (NightWalk.Neighbour n in list ?? Array.Empty<NightWalk.Neighbour>())
        {
            if (n == null || string.IsNullOrWhiteSpace(n.house)) continue;
            var w = new Walker { who = n, house = FindHouse(n.house) };
            w.door = w.house != null ? w.house.GetComponentInChildren<StreetDoor>(true) : null;
            if (w.house == null) w.state = "no such house";
            else if (w.door == null) w.state = "no front door";
            else if (n.walk == null || n.walk.Length == 0) w.state = "no walk";
            walkers.Add(w);
        }
        RefreshPlayers();
    }

    /// <summary>The houses whose neighbour is still out at <paramref name="startHour"/> (dark until they come home).</summary>
    public static List<string> AwayAt(NightWalk.Neighbour[] list, float startHour)
    {
        var away = new List<string>();
        foreach (NightWalk.Neighbour n in list ?? Array.Empty<NightWalk.Neighbour>())
            if (n != null && !string.IsNullOrWhiteSpace(n.house) && n.comesHomeAt > startHour) away.Add(n.house.Trim());
        return away;
    }

    public void Tick(float clockHour)
    {
        hour = clockHour;
        if (Time.unscaledTime >= nextPlayerScan) RefreshPlayers();
        bool look = Time.unscaledTime >= nextSightCheck;
        if (look) nextSightCheck = Time.unscaledTime + 1f;
        int walking = 0;
        foreach (Walker w in walkers)
        {
            switch (w.state)
            {
                case "not yet":
                    if (hour >= w.who.comesHomeAt) { w.state = "due"; w.dueSince = Time.time; }
                    break;
                case "due":
                    if (!look) break;
                    if (!Seen(w.who.walk[0])) Spawn(w);
                    else if (Time.time - w.dueSince > GiveUpUnseenAfter) Arrive(w, "came in the back way (their corner stayed in view)");
                    break;
                case "walking":
                    walking++;
                    HoldTheDoor(w);
                    if (Time.time - w.walkingSince > WalkTooLong) { Remove(w); Arrive(w, "home (the walk took too long, finished for them)"); }
                    break;
                case "inside":
                    // In the hall: the way through is still theirs until the door has shut behind them.
                    if (w.journey != null) w.door.Ask(w.journey, StreetDoor.Way.In);
                    if (w.door.IsClosed || Time.time - w.closingSince > 3f || w.journey != null && w.door.OthersUsing(w.journey))
                    {
                        Remove(w);
                        Arrive(w, "home");
                    }
                    break;
            }
        }
        Walking = walking;
    }

    /// <summary>
    /// A jump in time (photos, checks): anyone due before <paramref name="clockHour"/> who hasn't set off is
    /// counted as home from the hour they were due, so their house is lit as it would be by now.
    /// </summary>
    public void SkipTo(float clockHour)
    {
        foreach (Walker w in walkers)
            if ((w.state == "not yet" || w.state == "due") && w.who.comesHomeAt <= clockHour)
                Arrive(w, "home (the clock was moved on)", w.who.comesHomeAt + .03f);
        hour = clockHour;
    }

    /// <summary>For checks: everyone still out sets off now, a few seconds apart.</summary>
    public void SendHomeNow(float clockHour)
    {
        int i = 0;
        foreach (Walker w in walkers)
        {
            if (w.state != "not yet") continue;
            w.who.comesHomeAt = clockHour + .01f * i++;
        }
    }

    /// <summary>How many neighbours there are tonight.</summary>
    public int Count => walkers.Count;

    /// <summary>Where neighbour <paramref name="index"/> is: "not yet", "due", "walking", "inside", "home"... (for checks).</summary>
    public string StateOf(int index) => index >= 0 && index < walkers.Count ? walkers[index].state : "";

    /// <summary>The first neighbour still out (not yet set off), or -1. <paramref name="outOfSight"/>: only one whose
    /// corner the camera can't see right now (they would set off at once).</summary>
    public int NextOut(bool outOfSight = false)
    {
        for (int i = 0; i < walkers.Count; i++)
        {
            Walker w = walkers[i];
            if (w.state != "not yet" || w.who.walk == null || w.who.walk.Length == 0) continue;
            if (!outOfSight || !Seen(w.who.walk[0])) return i;
        }
        return -1;
    }

    /// <summary>For checks: neighbour <paramref name="index"/> sets off now (once the camera can't see their corner).</summary>
    public bool SendHome(int index, float clockHour)
    {
        if (index < 0 || index >= walkers.Count || walkers[index].state != "not yet") return false;
        walkers[index].who.comesHomeAt = clockHour;
        return true;
    }

    /// <summary>Where neighbour <paramref name="index"/> is right now, if walking (else null).</summary>
    public Vector3? BodyOf(int index) =>
        index >= 0 && index < walkers.Count && walkers[index].body != null ? walkers[index].body.transform.position : (Vector3?)null;

    public void Clear()
    {
        foreach (Walker w in walkers) Remove(w);
        walkers.Clear();
        Home = Walking = 0;
    }

    void OnDestroy() => Clear();

    // ---------- walking home ----------

    void Spawn(Walker w)
    {
        if (prefab == null) { Arrive(w, "came home unseen (no body to walk them: the patron prefab is missing)"); return; }
        // Made inside a switched-off holder, so nothing of the café's wakes up first: no navigation agent is
        // made off the navigation mesh (it would warn), no brain, nothing to talk to.
        var holder = new GameObject("(a neighbour, getting ready)");
        holder.SetActive(false);
        GameObject body = Instantiate(prefab, holder.transform);
        body.name = $"Neighbour coming home ({w.who.house})";
        foreach (NavMeshAgent agent in body.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (MonoBehaviour brain in Brains(body)) brain.enabled = false;
        body.transform.SetParent(null, false);
        Destroy(holder);

        foreach (MonoBehaviour brain in Brains(body)) Destroy(brain);
        foreach (Interactable talk in body.GetComponents<Interactable>()) Destroy(talk);
        foreach (PatienceBar bar in body.GetComponentsInChildren<PatienceBar>(true)) bar.gameObject.SetActive(false);
        foreach (Transform child in body.transform)
            if (child.name == "SpeechBubble") child.gameObject.SetActive(false);
        Animator animator = body.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        // The pavement to their door, up the step, through the doorway into the dark hall.
        StreetDoor door = w.door;
        var points = new List<Vector3>(w.who.walk);
        Vector3 last = points[points.Count - 1];
        Vector3 stoop = door.DoorwayPoint + door.Outward * Mathf.Max(.2f, w.who.stoop);
        stoop.y = last.y;
        points.Add(stoop);
        points.Add(door.DoorwayPoint - door.Outward * .1f);
        points.Add(door.HallPoint);
        int segments = points.Count - 1;
        var left = new float[segments];
        var right = new float[segments];
        for (int i = 0; i < segments; i++) left[i] = right[i] = PavementRoom;
        left[segments - 3] = right[segments - 3] = StoopRoom;      // the front of the door to the step
        left[segments - 2] = right[segments - 2] = DoorwayRoom;    // up the step into the doorway
        left[segments - 1] = right[segments - 1] = DoorwayRoom;    // through it into the hall

        NpcJourney journey = body.AddComponent<NpcJourney>();
        w.body = body;
        w.journey = journey;
        journey.Begin(points.ToArray(), null, left, right, UnityEngine.Random.Range(1.05f, 1.25f), false, CafeArrivals.Kind.Patron,
                      () => ReachedTheHall(w));
        // The last stretch (from the front of the door into the hall) on their turn, and not into a shut door.
        journey.DoorPassage(door, StreetDoor.Way.In, points.Count - 4, points.Count - 2);
        journey.Gate(points.Count - 3, () => door == null || door.IsOpen, "their front door to open");
        w.state = "walking";
        w.walkingSince = Time.time;
    }

    // As CafeArrivals.WatchDoors does for the café's visitors: the door opens for whoever's turn it is,
    // once they are within reach of it.
    void HoldTheDoor(Walker w)
    {
        if (w.body == null || w.journey == null || w.door == null) return;
        if (w.journey.TurnDoor == w.door && !w.journey.HasTurn) return;
        Vector3 at = w.body.transform.position;
        Vector3 flat = at - w.door.DoorwayPoint;
        flat.y = 0f;
        float outside = w.door.Outside(at);
        if (outside < 0f ? flat.magnitude < DoorReach + 1f : flat.magnitude < DoorReach) w.door.Hold(w.body, .35f);
    }

    // In the dark hall: the door closes behind them, and then they are home.
    void ReachedTheHall(Walker w)
    {
        if (w.door != null && w.body != null) w.door.Release(w.body);
        w.state = "inside";
        w.closing = true;
        w.closingSince = Time.time;
    }

    void Arrive(Walker w, string how, float at = -1f)
    {
        float from = at >= 0f ? at : hour;
        Vector3 doorPoint = w.door != null ? w.door.DoorwayPoint : w.house != null ? w.house.position : Vector3.zero;
        w.rooms = homes != null ? homes.ComeHome(w.who.house.Trim(), doorPoint, from, w.who.downstairsFor, w.who.upstairsFor) : "no rooms (no NightHomes)";
        w.state = how;
        w.cameHome = from;
        Home++;
    }

    void Remove(Walker w)
    {
        if (w.door != null && w.journey != null) w.door.Leave(w.journey);
        if (w.door != null && w.body != null) w.door.Release(w.body);
        if (w.body != null) Destroy(w.body);
        w.body = null;
        w.journey = null;
    }

    static IEnumerable<MonoBehaviour> Brains(GameObject npc)
    {
        CustomerBrain customer = npc.GetComponent<CustomerBrain>();
        if (customer != null) yield return customer;
        PatronBrain patron = npc.GetComponent<PatronBrain>();
        if (patron != null) yield return patron;
    }

    // The café's walkers step round Ace through CafeArrivals.Players, which CafeArrivals keeps up to date
    // by day; it rests at night, so the list is kept here.
    void RefreshPlayers()
    {
        nextPlayerScan = Time.unscaledTime + 2f;
        CafeArrivals.Players.Clear();
        CafeArrivals.Players.AddRange(FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude));
    }

    // In the camera's view (with a margin): somewhere a person mustn't appear out of nowhere.
    static bool Seen(Vector3 point)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        for (int i = 0; i < 2; i++)
        {
            Vector3 v = cam.WorldToViewportPoint(point + Vector3.up * (i == 0 ? .1f : 1.8f));
            if (v.z > 0f && v.x > -.03f && v.x < 1.03f && v.y > -.03f && v.y < 1.03f) return true;
        }
        return false;
    }

    static Transform FindHouse(string name)
    {
        string wanted = name.Trim();
        foreach (StreetDoor door in FindObjectsByType<StreetDoor>(FindObjectsInactive.Exclude))
            for (Transform t = door.transform; t != null; t = t.parent)
                if (t.name == wanted) return t;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name == wanted) return t;
        return null;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"Neighbours: {Home} of {walkers.Count} home, {Walking} walking now.");
        foreach (Walker w in walkers)
        {
            sb.Append($"\n  {w.who.name} ({w.who.house}, due {NightHomes.Clock(w.who.comesHomeAt)}): {w.state}");
            if (w.journey != null)
                sb.Append($", point {w.journey.NextPoint} of {w.journey.PointCount}{(w.journey.Waiting ? ", waiting for " + w.journey.WaitingFor : "")}" +
                          $"{(string.IsNullOrEmpty(w.journey.DoorTurn) ? "" : ", door: " + w.journey.DoorTurn)}");
            if (w.cameHome >= 0f) sb.Append($" at {NightHomes.Clock(w.cameHome)}; lights {w.rooms}");
        }
        return sb.ToString();
    }
}
