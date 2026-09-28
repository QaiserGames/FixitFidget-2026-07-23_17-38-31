using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// NIGHT WALK CHECK: THE TOUR (a lab session only: Fixit Fidget > Night > Night
// walk 3 - Walk the tour)
//
// Walks Ace out of the closed café and round the 9 blocks, overhead, on a fixed
// route: down the front street, south to the road works on West Street, up to
// the back street and out to its west end, east along it and up East Street to
// the north road works, down to the front street and out to its east end, and
// back into the café. The overhead camera is turned on each leg so that the
// buildings on one side stand between it and Ace, then it zooms out and in. At
// the end Ace walks a stretch of the front street in first person.
//
// The route was worked out from the night sweep's grid (Logs/Night/edges-night-*):
// every point is ground Ace can reach at night, well clear of walls.
//
// It drives Ace exactly like a player: PlayerMovement.ScriptedInput stands in for
// the keys and goes through the same path. It writes to its folder: a photo of
// the Game view every couple of seconds, trail.csv, and report.txt (stuck spots,
// what turned see-through, frame times). Nothing is saved to the scene.
// ---------------------------------------------------------------------------
public sealed class NightTour : MonoBehaviour
{
    public string folder;
    [Tooltip("Seconds between photos of the Game view.")]
    public float photoEvery = 2.5f;

    const float NoChange = -1f;

    // A point on the route; the camera settings apply to the leg that starts there
    // (NoChange: carry on). radius: how close counts as arrived.
    struct Stop
    {
        public float x, z, yaw, pitch, distance, radius;
        public string note;
        public Stop(float x, float z, string note = null, float yaw = NoChange, float pitch = NoChange, float distance = NoChange, float radius = .9f)
        { this.x = x; this.z = z; this.note = note; this.yaw = yaw; this.pitch = pitch; this.distance = distance; this.radius = radius; }
    }

    static readonly Stop[] Overhead =
    {
        new Stop(5.12f, 15.62f, "behind the counter", radius: .45f),
        new Stop(5.88f, 14.62f, radius: .45f),
        new Stop(5.88f, 12.12f, radius: .45f),
        new Stop(1.12f, 10.88f, radius: .5f),
        new Stop(.62f, 7.12f, radius: .5f),
        new Stop(.12f, .6f, "the café door", radius: .25f),
        new Stop(.12f, -.8f, radius: .3f),
        new Stop(.12f, -7.62f, "out on the front street", radius: .7f),
        new Stop(-11.88f, -7.62f, "west along the front street"),
        new Stop(-11.88f, -35.88f, "south down West Street to the road works (camera to the west)", yaw: 90f),
        new Stop(-11.88f, 22.12f, "north up West Street (camera to the east)", yaw: 270f),
        new Stop(-39.88f, 22.12f, "west along the back street to its road works (camera to the south)", yaw: 0f),
        new Stop(12.12f, 22.12f, "east along the back street (camera to the north, lower)", yaw: 180f, pitch: 55f),
        new Stop(12.12f, 31.12f, "up East Street to the north road works", yaw: 180f, pitch: 62f),
        new Stop(12.12f, -7.62f, "south down East Street (camera to the east)", yaw: 270f),
        new Stop(38.12f, -7.62f, "east along the front street to its road works (camera to the south)", yaw: 0f),
        new Stop(.12f, -7.62f, "back west to the café, zoomed out", yaw: 25f, distance: 34f),
        new Stop(.12f, -.8f, "zoomed in to the door", distance: 12f, radius: .3f),
        new Stop(.12f, .6f, radius: .25f),
        new Stop(.12f, 5.12f, "inside the café again", distance: 20f, radius: .5f),
    };

    PlayerMovement movement;
    CafeViewMode view;
    NightSeeThrough seeThrough;
    readonly StringBuilder trail = new StringBuilder("t,x,z,turn,tilt,distance,see_through_now,inside_cafe,first_person\n");
    readonly List<string> events = new();
    readonly List<float> frameTimes = new();
    float targetYaw, targetPitch, targetDistance;
    int photos, stuck, skipFrames;
    float walked, started;
    Vector3 last;
    bool cafeWallDownOutside;
    int cafeWallDownFrames;

    IEnumerator Start()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        seeThrough = NightWalk.Instance != null ? NightWalk.Instance.GetComponent<NightSeeThrough>() : null;
        if (movement == null || view == null || NightWalk.Instance == null || !NightWalk.Instance.Active)
        {
            Debug.LogError("[Night tour] Start a night walk first (Fixit Fidget > Night > Play the night walk (lab)).");
            Destroy(gameObject);
            yield break;
        }
        Directory.CreateDirectory(folder);
        started = Time.time;
        last = movement.transform.position;
        Debug.Log($"[Night tour] Walking the tour; photos and notes go to {folder}");

        // Overhead, following Ace, from the café's heading.
        view.SetFirstPerson(false);
        Vector3 a = view.OverheadAngle;
        targetYaw = a.x; targetPitch = a.y; targetDistance = a.z;
        yield return StartCoroutine(Photo("start"));

        foreach (Stop stop in Overhead)
        {
            if (stop.yaw != NoChange) targetYaw = stop.yaw;
            if (stop.pitch != NoChange) targetPitch = stop.pitch;
            if (stop.distance != NoChange) targetDistance = stop.distance;
            if (stop.note != null) events.Add($"{Time.time - started,6:0.0}s  {stop.note}");
            yield return StartCoroutine(WalkTo(stop));
        }

        // First person: a stretch of the front street, looking west, then north.
        events.Add($"{Time.time - started,6:0.0}s  first person on the front street");
        movement.ScriptedInput = Vector2.zero;
        view.SetFirstPerson(true);
        view.LookTo(180f, 4f);
        yield return StartCoroutine(WalkTo(new Stop(.12f, -.8f, radius: .3f)));
        yield return StartCoroutine(WalkTo(new Stop(.12f, -7.62f, radius: .7f)));
        view.LookTo(270f, 2f);
        yield return StartCoroutine(Photo("first-person-front-street-west"));
        yield return StartCoroutine(WalkTo(new Stop(-9f, -7.62f)));
        view.LookTo(0f, 3f);
        yield return StartCoroutine(Photo("first-person-looking-north"));
        view.LookTo(90f, 3f);
        yield return StartCoroutine(Photo("first-person-looking-east"));
        movement.ScriptedInput = null;
        view.SetFirstPerson(false);
        yield return new WaitForSeconds(1.5f);   // the blend back to the overhead view
        yield return StartCoroutine(Photo("end-overhead"));
        Finish();
    }

    IEnumerator WalkTo(Stop stop)
    {
        float nextPhoto = Time.time + photoEvery;
        float checkAt = Time.time + 1.5f;
        Vector3 checkFrom = movement.transform.position;
        while (true)
        {
            Vector3 p = movement.transform.position;
            Vector2 to = new Vector2(stop.x - p.x, stop.z - p.z);
            if (to.magnitude <= stop.radius) break;

            // The same keys a player would press: the direction, in the camera's frame.
            Vector3 world = new Vector3(to.x, 0f, to.y).normalized;
            Vector3 local = Quaternion.Euler(0f, -view.MovementYaw, 0f) * world;
            movement.ScriptedInput = new Vector2(local.x, local.z);

            if (!view.FirstPersonSelected) Orbit();
            Record();
            if (Time.time >= nextPhoto) { nextPhoto = Time.time + photoEvery; yield return StartCoroutine(Photo(null)); }
            if (Time.time >= checkAt)
            {
                if (Vector3.Distance(checkFrom, movement.transform.position) < .3f)
                {
                    stuck++;
                    events.Add($"{Time.time - started,6:0.0}s  STUCK at ({p.x:0.00}, {p.z:0.00}) on the way to ({stop.x:0.00}, {stop.z:0.00}); skipped to the next point");
                    Debug.LogWarning($"[Night tour] Stuck at ({p.x:0.00}, {p.z:0.00}) on the way to ({stop.x:0.00}, {stop.z:0.00}).");
                    break;
                }
                checkAt = Time.time + 1.5f;
                checkFrom = movement.transform.position;
            }
            yield return null;
        }
    }

    // Turn the overhead camera smoothly toward this leg's angle, as a player would with the mouse.
    void Orbit()
    {
        Vector3 a = view.OverheadAngle;
        float dt = Time.deltaTime;
        float yaw = Mathf.MoveTowardsAngle(a.x, targetYaw, 120f * dt);
        float pitch = Mathf.MoveTowards(a.y, targetPitch, 30f * dt);
        float distance = Mathf.MoveTowards(a.z, targetDistance, 15f * dt);
        if (!Mathf.Approximately(yaw, a.x) || !Mathf.Approximately(pitch, a.y) || !Mathf.Approximately(distance, a.z))
            view.OrbitTo(yaw, pitch, distance);
    }

    void Record()
    {
        Vector3 p = movement.transform.position;
        walked += Vector3.Distance(new Vector3(last.x, 0, last.z), new Vector3(p.x, 0, p.z));
        last = p;
        if (skipFrames > 0) skipFrames--;
        else frameTimes.Add(Time.unscaledDeltaTime);
        bool inside = view.AceInsideCafe;
        bool wallDown = false;
        foreach (var cut in view.CutawayWalls) if (cut != null && cut.Lowered) wallDown = true;
        if (!inside && wallDown) { cafeWallDownFrames++; cafeWallDownOutside = true; }
        if (Time.frameCount % 10 == 0)
        {
            Vector3 a = view.OverheadAngle;
            trail.Append($"{Time.time - started:0.00},{p.x:0.00},{p.z:0.00},{a.x:0},{a.y:0},{a.z:0.0}," +
                         $"{(seeThrough != null ? seeThrough.FadedNow : 0)},{(inside ? 1 : 0)},{(view.FirstPersonSelected ? 1 : 0)}\n");
        }
    }

    IEnumerator Photo(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        string file = Path.Combine(folder, $"{photos:00}-{name ?? $"{Time.time - started:000.0}s"}.jpg");
        File.WriteAllBytes(file, shot.EncodeToJPG(88));
        Destroy(shot);
        skipFrames = 2;   // the capture itself costs a frame or two: keep it out of the frame times
    }

    void Finish()
    {
        frameTimes.Sort();
        float avg = 0f;
        foreach (float f in frameTimes) avg += f;
        avg = frameTimes.Count > 0 ? avg / frameTimes.Count : 0f;
        float p95 = frameTimes.Count > 0 ? frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * .95f))] : 0f;
        float worst = frameTimes.Count > 0 ? frameTimes[frameTimes.Count - 1] : 0f;
        var report = new StringBuilder();
        report.AppendLine("Night walk 3 - the tour (lab session, night walk)");
        report.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"Walked {walked:0} m in {Time.time - started:0} s; {stuck} stuck spot(s); {photos} photos.");
        report.AppendLine($"Frame times (photo frames left out): average {avg * 1000f:0.0} ms ({(avg > 0 ? 1f / avg : 0):0} fps), " +
                          $"95% under {p95 * 1000f:0.0} ms, worst {worst * 1000f:0.0} ms.");
        if (seeThrough != null)
        {
            report.AppendLine(seeThrough.Describe());
            var names = new List<string>(seeThrough.EverFaded);
            names.Sort();
            foreach (string n in names) report.AppendLine($"  turned see-through: {n}");
        }
        report.AppendLine(cafeWallDownOutside
            ? $"A café wall was down while Ace was outside for {cafeWallDownFrames} frames (it should only be when the café hides Ace)."
            : "No café wall went down while Ace was outside.");
        report.AppendLine();
        report.AppendLine("The route:");
        foreach (string e in events) report.AppendLine("  " + e);
        report.AppendLine();
        report.AppendLine(NightWalk.Instance != null ? NightWalk.Instance.Describe() : "");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        File.WriteAllText(Path.Combine(folder, "trail.csv"), trail.ToString());
        Debug.Log($"[Night tour] Done: {walked:0} m, {stuck} stuck, {photos} photos, " +
                  $"{(seeThrough != null ? seeThrough.EverFaded.Count : 0)} buildings and trees turned see-through. {folder}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null) movement.ScriptedInput = null;
    }
}
