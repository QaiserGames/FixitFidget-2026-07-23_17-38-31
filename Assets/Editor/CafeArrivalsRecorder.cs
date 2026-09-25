#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// A flight recorder for the café's arrivals (Play mode only, 25 Sept).
//
// WHY
//   Mansoor saw visitors "line up because of the cars" and bump into each other.
//   Screenshots can't show who is waiting for what, so this writes down, ten times
//   a second, where every visitor, café NPC, street walker and car is and what the
//   visitors are waiting on, and takes a photo from straight above every second.
//
// WHAT IT WRITES (Logs/ArrivalsTrace/<time>/)
//   trace.csv   - one row per body per sample: t, type, id, name, x, z, vx, vz, yaw, info
//   events.txt  - people overlapping, people inside a car's footprint, visitors held
//                 still (and by what), long kerb waits, walks that had to be helped
//                 on, café cars stopped for people
//   f0000.jpg   - top-down photos, x -19..21, z -28.5..11.5 (1000 px = 40 m)
//
// Nothing in the scene changes; it only watches.
public static class CafeArrivalsRecorder
{
    const string Menu = "Fixit Fidget/Cafe parking lot/";
    const string Tag = "[Arrivals recorder] ";
    const float SampleEvery = 0.1f, PhotoEvery = 1f;
    public static readonly Vector3 PhotoCentre = new Vector3(1f, 0f, -8.5f);
    public const float PhotoHalf = 20f;
    const int PhotoPixels = 1000;

    const float PersonRadius = 0.28f;
    const float OverlapFraction = 0.72f;    // centres closer than this share of the two radii: bodies interpenetrate
    const float StillSpeed = 0.06f;

    static StreamWriter trace, events;
    static string folder;
    static float stopAt, nextSample, nextPhoto;
    static int photoIndex, overlapCount, carOverlapCount, heldCount, unstuckCount, kerbCount, carHeldCount, givingWayCount;
    static readonly Dictionary<string, float> lastEvent = new Dictionary<string, float>();
    static readonly Dictionary<Object, float> stillSince = new Dictionary<Object, float>();
    static readonly Dictionary<Object, float> waitSince = new Dictionary<Object, float>();
    static readonly Dictionary<NpcJourney, int> unstuckSeen = new Dictionary<NpcJourney, int>();
    static readonly StringBuilder row = new StringBuilder();

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly FieldInfo AIsWalker = typeof(StreetLife.Actor).GetField("isWalker", Any);
    static readonly FieldInfo AVelocity = typeof(StreetLife.Actor).GetField("velocity", Any);
    static readonly FieldInfo ATraffic = typeof(StreetLife.Actor).GetField("trafficManaged", Any);
    static readonly FieldInfo AGuest = typeof(StreetLife.Actor).GetField("guest", Any);
    static readonly FieldInfo ALastSpeed = typeof(StreetLife.Actor).GetField("lastSpeed", Any);
    static readonly FieldInfo ARespawning = typeof(StreetLife.Actor).GetField("respawning", Any);

    struct Body
    {
        public string type, id, name, info;
        public Vector3 position, velocity;
        public float radius;
        public Object key;
    }

    struct Car
    {
        public string type, id, name;
        public Vector3 position, forward;
        public float halfLength, halfWidth, speed;
    }

    static readonly List<Body> people = new List<Body>();
    static readonly List<Car> carsNow = new List<Car>();

    [MenuItem(Menu + "Play - record arrivals for 2 minutes")]
    static void RecordMenu() => Start(120f);

    [MenuItem(Menu + "Play - record arrivals for 2 minutes", true)]
    static bool CanRecord() => EditorApplication.isPlaying && trace == null;

    [MenuItem(Menu + "Play - stop recording arrivals")]
    static void StopMenu() => Stop("stopped from the menu");

    [MenuItem(Menu + "Play - stop recording arrivals", true)]
    static bool CanStop() => trace != null;

    public static bool Recording => trace != null;
    public static string Folder => folder;

    public static void Start(float seconds)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Only in Play mode."); return; }
        if (trace != null) Stop("restarted");
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "ArrivalsTrace", stamp);
        Directory.CreateDirectory(folder);
        trace = new StreamWriter(Path.Combine(folder, "trace.csv"), false, new UTF8Encoding(false));
        events = new StreamWriter(Path.Combine(folder, "events.txt"), false, new UTF8Encoding(false));
        trace.WriteLine("t,type,id,name,x,z,vx,vz,yaw,info");
        events.WriteLine($"Arrivals recording started {DateTime.Now:HH:mm:ss}, game time {Time.time:0.0}s, for {seconds:0}s.");
        events.WriteLine($"Photos: centre {PhotoCentre.x},{PhotoCentre.z}, half size {PhotoHalf} m, {PhotoPixels} px, +x right, +z up.");
        lastEvent.Clear(); stillSince.Clear(); waitSince.Clear(); unstuckSeen.Clear();
        overlapCount = carOverlapCount = heldCount = unstuckCount = kerbCount = carHeldCount = givingWayCount = 0;
        photoIndex = 0;
        stopAt = Time.time + seconds;
        nextSample = nextPhoto = Time.time;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        Debug.Log(Tag + "Recording to " + folder);
    }

    static void PlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) Stop("play mode ended");
    }

    public static void Stop(string why)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        if (trace == null) return;
        string summary = $"Stopped ({why}) at game time {Time.time:0.0}s: {photoIndex} photos; " +
                         $"{overlapCount} people overlaps, {carOverlapCount} people inside a car's footprint, " +
                         $"{heldCount} visitors held still 3 s+, {kerbCount} kerb waits 10 s+, {givingWayCount} waits 6 s+ to let someone out, " +
                         $"{unstuckCount} walks helped on, {carHeldCount} café-car holds 2 s+.";
        try
        {
            events.WriteLine(summary);
            events.Flush(); events.Dispose();
            trace.Flush(); trace.Dispose();
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Closing the files: " + e.Message); }
        trace = null; events = null;
        Debug.Log(Tag + summary + " Folder: " + folder);
    }

    static void Tick()
    {
        if (trace == null) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
        float t = Time.time;
        try
        {
            if (t >= nextSample) { nextSample = t + SampleEvery; Sample(t); }
            if (t >= nextPhoto) { nextPhoto = t + PhotoEvery; Photo(); }
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Sample failed: " + e);
            Stop("error");
            return;
        }
        if (t >= stopAt) Stop("time up");
    }

    // ------------------------------------------------------------------ sampling

    static void Sample(float t)
    {
        people.Clear();
        carsNow.Clear();
        CafeArrivals arrivals = CafeArrivals.Instance;
        StreetLife life = StreetLife.Main;

        foreach (NpcJourney j in NpcJourney.Active)
        {
            if (j == null || !j.isActiveAndEnabled) continue;
            var waiting = j.KerbCrossing;
            Vector3 to = j.NextTarget;
            string info = $"{(j.Arriving ? "in" : "out")}|{j.Kind}|next {j.NextPoint}/{j.PointCount}" +
                          $"|to {F(to.x)} {F(to.z)}|kerb {(waiting != null ? waiting.Name : "-")}" +
                          $"|on {(j.CurrentCrossing != null ? j.CurrentCrossing.Name : "-")}|held {(j.HeldBy.Length > 0 ? j.HeldBy : "-")}" +
                          $"|stuck {F(j.StuckFor)}|pref {F(j.Preference)}|unstuck {j.Unstuck}";
            people.Add(new Body
            {
                type = "visitor", id = "J" + Id(j), name = j.name, info = info,
                position = j.transform.position, velocity = j.Velocity, radius = PersonRadius, key = j
            });
        }

        foreach (NavMeshAgent agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude))
        {
            if (agent == null || !agent.isActiveAndEnabled) continue;
            string state = BrainState(agent.gameObject);
            people.Add(new Body
            {
                type = "agent", id = "A" + Id(agent), name = agent.name,
                info = agent.isOnNavMesh
                    ? $"{state}|r {F(agent.radius)}|stopped {(agent.isStopped ? 1 : 0)}|dest {F(agent.destination.x)} {F(agent.destination.z)}"
                    : $"{state}|r {F(agent.radius)}|off the NavMesh",
                position = agent.transform.position, velocity = agent.velocity, radius = Mathf.Max(0.2f, agent.radius), key = agent
            });
        }

        foreach (CharacterController player in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
            if (player != null && player.enabled)
                people.Add(new Body
                {
                    type = "player", id = "P" + Id(player), name = player.name, info = "",
                    position = player.transform.position, velocity = player.velocity, radius = 0.3f, key = player
                });

        if (life != null)
            foreach (StreetLife.Actor a in life.actors)
            {
                if (a == null || a.actor == null || !a.actor.gameObject.activeInHierarchy) continue;
                bool walker = AIsWalker != null && (bool)AIsWalker.GetValue(a);
                bool traffic = ATraffic != null && (bool)ATraffic.GetValue(a);
                bool guest = AGuest != null && (bool)AGuest.GetValue(a);
                bool respawning = ARespawning != null && (bool)ARespawning.GetValue(a);
                if (respawning) continue;
                if (walker)
                {
                    Vector3 v = AVelocity != null ? (Vector3)AVelocity.GetValue(a) : Vector3.zero;
                    people.Add(new Body
                    {
                        type = "street", id = "S" + Id(a.actor), name = a.actor.name, info = "",
                        position = a.actor.position, velocity = v, radius = 0.3f, key = a.actor
                    });
                }
                else if (traffic && !guest)
                {
                    float speed = ALastSpeed != null ? (float)ALastSpeed.GetValue(a) : 0f;
                    Vector3 f = a.actor.forward; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
                    carsNow.Add(new Car
                    {
                        type = "traffic", id = "T" + Id(a.actor), name = a.actor.name + " (" + a.trafficGroup + ")",
                        position = a.actor.position, forward = f, halfLength = Mathf.Max(0.5f, a.vehicleLength) * 0.5f,
                        halfWidth = 1.0f, speed = speed
                    });
                }
            }

        if (arrivals != null)
            foreach (CafeCar car in arrivals.Cars)
            {
                if (car == null || !car.gameObject.activeInHierarchy || car.State == CafeCar.Phase.Pooled) continue;
                Vector3 f = car.transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
                float speed = car.InTraffic ? CarSpeedInTraffic(life, car) : car.Velocity.magnitude;
                carsNow.Add(new Car
                {
                    type = "cafecar", id = "C" + Id(car),
                    name = $"{car.name}|{car.State}|stall {car.Stall}|{(car.DriverWaiting ? "driver in" : car.Owner != null ? "owner " + car.Owner.name : "no owner")}",
                    position = car.transform.position, forward = f, halfLength = car.Length * 0.5f, halfWidth = car.Width * 0.5f, speed = speed
                });
            }

        // ---- trace rows ----
        foreach (Body b in people)
            Row(t, b.type, b.id, b.name, b.position, b.velocity, Yaw(b.velocity), b.info);
        foreach (Car c in carsNow)
            Row(t, c.type, c.id, c.name, c.position, c.forward * c.speed, Mathf.Atan2(c.forward.x, c.forward.z) * Mathf.Rad2Deg,
                $"half {F(c.halfLength)} {F(c.halfWidth)}|speed {F(c.speed)}");
        if (arrivals != null)
            foreach (StreetCrossing crossing in arrivals.LiveCrossings)
                if (crossing != null)
                    Row(t, "crossing", crossing.Name, crossing.Name, crossing.Block.center, Vector3.zero, crossing.Block.yaw,
                        $"waiting {crossing.Waiting}|on {crossing.OnIt}|traffic stopped {(crossing.Block.active ? 1 : 0)}" +
                        (crossing.Signalled && life != null ? $"|walk left {F(life.WalkTimeLeft(1 - crossing.CrossingTrafficPhase))}" : "") +
                        (life != null ? $"|signals {life.CurrentSignalState}" : ""));

        // ---- events ----
        for (int i = 0; i < people.Count; i++)
            for (int k = i + 1; k < people.Count; k++)
            {
                Body a = people[i], b = people[k];
                if (a.type == "street" && b.type == "street") continue;
                Vector3 d = a.position - b.position;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                d.y = 0f;
                float limit = (a.radius + b.radius) * OverlapFraction;
                if (d.magnitude >= limit) continue;
                string key = "o|" + (string.CompareOrdinal(a.id, b.id) < 0 ? a.id + b.id : b.id + a.id);
                if (!Due(key, t, 1.5f)) continue;
                overlapCount++;
                Event(t, $"OVERLAP {a.type} {a.name} [{a.info}] and {b.type} {b.name} [{b.info}]: centres {F(d.magnitude)} m apart at {F(a.position.x)},{F(a.position.z)}");
            }

        foreach (Car c in carsNow)
        {
            Vector3 right = new Vector3(c.forward.z, 0f, -c.forward.x);
            foreach (Body p in people)
            {
                Vector3 d = p.position - c.position;
                if (Mathf.Abs(d.y) > 2f) continue;
                if (Mathf.Abs(Vector3.Dot(d, c.forward)) > c.halfLength + 0.05f || Mathf.Abs(Vector3.Dot(d, right)) > c.halfWidth + 0.05f) continue;
                if (!Due("c|" + c.id + p.id, t, 1.5f)) continue;
                carOverlapCount++;
                Event(t, $"IN A CAR {p.type} {p.name} [{p.info}] inside {c.type} {c.name} (speed {F(c.speed)}) at {F(p.position.x)},{F(p.position.z)}");
            }
        }

        foreach (Body b in people)
        {
            if (b.type != "visitor") continue;
            var j = (NpcJourney)b.key;
            Vector3 v = b.velocity; v.y = 0f;
            if (unstuckSeen.TryGetValue(j, out int seen) && j.Unstuck > seen)
            {
                unstuckCount++;
                Event(t, $"HELPED ON {b.name} [{b.info}] jumped to its next point near {F(b.position.x)},{F(b.position.z)}");
            }
            unstuckSeen[j] = j.Unstuck;
            if (j.Waiting)
            {
                // Waiting on purpose (a kerb, someone coming out of a doorway) is not being held.
                stillSince.Remove(j);
                if (!waitSince.TryGetValue(j, out float began)) { waitSince[j] = t; continue; }
                float waited = t - began;
                var crossing = j.KerbCrossing;
                if (crossing != null)
                {
                    if (waited >= 10f && Due("k|" + b.id, t, 10f))
                    {
                        kerbCount++;
                        Event(t, $"KERB WAIT {b.name} {waited:0}s at {crossing.Name} ({crossing.Waiting} waiting, {crossing.OnIt} on it, traffic stopped {crossing.Block.active}" +
                                 (crossing.Signalled && life != null ? $", walk time left {life.WalkTimeLeft(1 - crossing.CrossingTrafficPhase):0.0}s" : "") +
                                 $"); cars near: {CarsNear(crossing.Block.center, 9f)}");
                    }
                }
                else if (waited >= 6f && Due("g|" + b.id, t, 6f))
                {
                    givingWayCount++;
                    Event(t, $"GIVING WAY {b.name} {waited:0}s: {j.WaitingFor}, at {F(b.position.x)},{F(b.position.z)}");
                }
                continue;
            }
            waitSince.Remove(j);
            if (v.magnitude > StillSpeed) { stillSince.Remove(j); continue; }
            if (!stillSince.TryGetValue(j, out float since)) { stillSince[j] = t; continue; }
            float still = t - since;
            if (still >= 3f && Due("h|" + b.id, t, 4f))
            {
                heldCount++;
                Vector3 to = j.NextTarget;
                Event(t, $"HELD {b.name} still {still:0.0}s at {F(b.position.x)},{F(b.position.z)} heading for {F(to.x)},{F(to.z)} [{b.info}]; in the way: {InTheWay(b, to)}");
            }
        }

        if (arrivals != null)
            foreach (CafeCar car in arrivals.Cars)
            {
                if (car == null || !car.gameObject.activeInHierarchy) continue;
                bool driving = car.State == CafeCar.Phase.TurningIn || car.State == CafeCar.Phase.BackingOut
                            || car.State == CafeCar.Phase.ToExit || car.State == CafeCar.Phase.TurningOut;
                bool held = driving ? car.Velocity.magnitude < 0.05f : car.State == CafeCar.Phase.HeldAtLot;
                if (!held) { stillSince.Remove(car); continue; }
                if (!stillSince.TryGetValue(car, out float since)) { stillSince[car] = t; continue; }
                if (t - since >= 2f && Due("ch|" + Id(car), t, 4f))
                {
                    carHeldCount++;
                    Event(t, $"CAFE CAR HELD {car.name} {car.State} {t - since:0.0}s at {F(car.transform.position.x)},{F(car.transform.position.z)}; people near: {PeopleNear(car.transform.position, 5f)}");
                }
            }
    }

    static readonly FieldInfo CarTraffic = typeof(CafeCar).GetField("traffic", Any);

    // In a lane the car is driven by StreetLife, so its own velocity isn't kept.
    static float CarSpeedInTraffic(StreetLife life, CafeCar car) =>
        CarTraffic?.GetValue(car) is StreetLife.Actor actor ? actor.CurrentSpeed : car.Velocity.magnitude;

    static string BrainState(GameObject go)
    {
        foreach (MonoBehaviour m in go.GetComponents<MonoBehaviour>())
        {
            if (m == null) continue;
            Type type = m.GetType();
            if (type.Name != "CustomerBrain" && type.Name != "PatronBrain") continue;
            FieldInfo state = type.GetField("state", Any);
            return type.Name.Replace("Brain", "") + " " + (state != null ? state.GetValue(m)?.ToString() : "?") + (m.enabled ? "" : " (off)");
        }
        return "no brain";
    }

    static string InTheWay(Body walker, Vector3 to)
    {
        Vector3 heading = to - walker.position; heading.y = 0f;
        if (heading.sqrMagnitude < 1e-6f) return "at its point";
        heading.Normalize();
        Vector3 right = new Vector3(heading.z, 0f, -heading.x);
        string best = "nobody"; float bestAlong = float.PositiveInfinity;
        foreach (Body b in people)
        {
            if (ReferenceEquals(b.key, walker.key)) continue;
            Vector3 d = b.position - walker.position; d.y = 0f;
            float along = Vector3.Dot(d, heading), side = Vector3.Dot(d, right);
            if (along < -0.1f || along > 2.2f || Mathf.Abs(side) > 0.7f + b.radius || along >= bestAlong) continue;
            bestAlong = along;
            best = $"{b.type} {b.name} {F(along)} m ahead, {F(side)} m right, speed {F(new Vector3(b.velocity.x, 0f, b.velocity.z).magnitude)} [{b.info}]";
        }
        foreach (Car c in carsNow)
        {
            Vector3 d = c.position - walker.position; d.y = 0f;
            float along = Vector3.Dot(d, heading), side = Vector3.Dot(d, right);
            if (along < -c.halfLength || along > 2.5f + c.halfLength || Mathf.Abs(side) > 0.7f + c.halfLength || along - c.halfLength >= bestAlong) continue;
            bestAlong = along - c.halfLength;
            best = $"{c.type} {c.name} centre {F(along)} m ahead, {F(side)} m right, speed {F(c.speed)}";
        }
        return best;
    }

    static string CarsNear(Vector3 at, float within)
    {
        var sb = new StringBuilder();
        foreach (Car c in carsNow)
        {
            Vector3 d = c.position - at; d.y = 0f;
            if (d.magnitude > within) continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append($"{c.type} {c.name} at {F(c.position.x)},{F(c.position.z)} speed {F(c.speed)}");
        }
        return sb.Length > 0 ? sb.ToString() : "none";
    }

    static string PeopleNear(Vector3 at, float within)
    {
        var sb = new StringBuilder();
        foreach (Body b in people)
        {
            Vector3 d = b.position - at; d.y = 0f;
            if (d.magnitude > within) continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append($"{b.type} {b.name} at {F(b.position.x)},{F(b.position.z)}");
        }
        return sb.Length > 0 ? sb.ToString() : "none";
    }

    static bool Due(string key, float t, float every)
    {
        if (lastEvent.TryGetValue(key, out float last) && t - last < every) return false;
        lastEvent[key] = t;
        return true;
    }

    static void Event(float t, string text) => events?.WriteLine($"{t:0.0}s {text}");

    static void Row(float t, string type, string id, string name, Vector3 p, Vector3 v, float yaw, string info)
    {
        row.Clear();
        row.Append(F(t)).Append(',').Append(type).Append(',').Append(id).Append(',').Append(Clean(name)).Append(',')
           .Append(F(p.x)).Append(',').Append(F(p.z)).Append(',').Append(F(v.x)).Append(',').Append(F(v.z)).Append(',')
           .Append(F(yaw)).Append(',').Append(Clean(info));
        trace.WriteLine(row.ToString());
    }

    static string Id(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o).ToString(CultureInfo.InvariantCulture);
    static float Yaw(Vector3 v) => v.x * v.x + v.z * v.z > 1e-6f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : 0f;
    static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(',', ';').Replace('\n', ' ');
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ photos

    static void Photo()
    {
        var g = new GameObject("Temporary arrivals camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = g.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(PhotoPixels, PhotoPixels, 24);
        var texture = new Texture2D(PhotoPixels, PhotoPixels, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.orthographic = true;
            cam.orthographicSize = PhotoHalf;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
            cam.transform.SetPositionAndRotation(PhotoCentre + Vector3.up * 80f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, PhotoPixels, PhotoPixels), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"f{photoIndex:0000}.jpg"), texture.EncodeToJPG(80));
            trace.WriteLine($"{F(Time.time)},photo,f{photoIndex:0000},,,,,,,");
            photoIndex++;
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(g);
        }
    }
}
#endif
