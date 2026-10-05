using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE HOUSES UP CLOSE, AT NIGHT (5 Oct 2026: a lab photographer, read-only)
//
// Mansoor, 5 Oct: in first person the houses look like "AI slop". This takes the pictures that
// judgement is made on, the same way every time, so a pass over the houses can be seen before and
// after: in a night lab (Fixit Fidget > Night > Slop audit - photograph the houses up close), with
// Ace in first person, at every street door from 3 m (the door and its surround), at eight of
// them from 8 m (the whole front), and inside Grace's house at six spots (the entry, the front
// room, the kitchen, the landing, the bedroom, the wardrobe). Then the same door shots with the
// torch on. Ace is moved from spot to spot (the capsule switched off for the move, as the labs do),
// never walked. Photos and report.txt go to Logs/Night/house-close-ups-<time>/. Play stops by itself.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class HouseCloseUps : MonoBehaviour
{
    public const string Key = "FixitFidget.HouseCloseUps";
    const float DoorDistance = 3.2f, FrontDistance = 8f;

    static readonly (float X, float Y, float lookX, float lookY, bool up, string what)[] Inside =
    {
        (1.11f, 3.35f, 1.11f, 4.6f, false, "the entry, looking back at her door"),
        (1.11f, 3.35f, 3.2f, 1.6f, false, "the entry, looking into the house"),
        (2.45f, 2.75f, 5.3f, 3.0f, false, "the front room, looking at the TV wall"),
        (3.20f, 1.30f, 3.2f, 0.0f, false, "the kitchen, looking at the counter"),
        (0.70f, 0.72f, 2.4f, 0.7f, false, "the landing, looking up the upper flight"),
        (2.75f, 3.25f, 4.2f, 3.9f, true, "the bedroom, at the foot of the bed"),
        (1.25f, 3.62f, 0.0f, 3.0f, true, "the bedroom, at the wardrobe"),
        (3.05f, 0.85f, 3.05f, 3.0f, true, "the landing upstairs, looking into the bedroom"),
    };

    string folder;
    readonly StringBuilder report = new();
    PlayerMovement mover;
    CafeViewMode view;
    CharacterController capsule;
    int photos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
#if UNITY_EDITOR
        if (PlayerPrefs.GetInt(Key, 0) != 1) return;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        new GameObject("House close-ups (lab)") { hideFlags = HideFlags.DontSave }.AddComponent<HouseCloseUps>();
#endif
    }

    IEnumerator Start()
    {
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "house-close-ups-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        // The night, Grace's house and Ace: the lab sets them up over the first frames.
        float waited = 0f;
        while ((NightWalk.Instance == null || !NightWalk.Instance.Active || GraceHouse.Instance == null) && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        view = FindAnyObjectByType<CafeViewMode>();
        mover = view != null ? view.GetComponent<PlayerMovement>() : null;
        capsule = view != null ? view.GetComponent<CharacterController>() : null;
        if (view == null || mover == null || capsule == null || NightWalk.Instance == null || !NightWalk.Instance.Active)
        {
            Debug.LogError("[House close-ups] The night, Ace or the view wasn't found; nothing photographed.");
            Finish();
            yield break;
        }
        yield return new WaitForSeconds(2f);   // the night settles, the camera arrives
        report.AppendLine("The houses up close, at night (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
        report.AppendLine($"Night clock {NightHomes.Clock(NightWalk.Instance.Hour)}; first person at Ace's eyes; the torch off unless the photo says so.");
        mover.ScriptedInput = Vector2.zero;
        view.SetFirstPerson(true);
        NightTorch torch = NightWalk.Instance.Torch;
        if (torch != null && torch.On) torch.Switch(false);

        // 1. Every street door from 3 m: the door, its surround, the steps, the window beside it.
        var doors = new List<StreetDoor>(StreetDoor.All);
        report.AppendLine();
        report.AppendLine($"Street doors: {doors.Count}.");
        int n = 0;
        foreach (StreetDoor door in doors)
        {
            if (door == null) continue;
            n++;
            string house = HouseName(door.transform);
            yield return Shot(door.DoorwayPoint + door.Outward * DoorDistance, door.DoorwayPoint + Vector3.up * 1.2f, 0f,
                $"door-{n:00}-{Safe(house)}.png", $"{house}: the door from {DoorDistance:0} m");
        }
        // 2. Eight fronts from 8 m, slightly looking up: the whole house.
        report.AppendLine();
        int step = Mathf.Max(1, doors.Count / 8), k = 0;
        for (int i = 0; i < doors.Count && k < 8; i += step)
        {
            StreetDoor door = doors[i];
            if (door == null) continue;
            k++;
            string house = HouseName(door.transform);
            yield return Shot(door.DoorwayPoint + door.Outward * FrontDistance, door.DoorwayPoint + Vector3.up * 3.5f, 0f,
                $"front-{k:00}-{Safe(house)}.png", $"{house}: the front from {FrontDistance:0} m");
        }
        // 3. Grace's house inside.
        GraceHouse grace = GraceHouse.Instance;
        report.AppendLine();
        report.AppendLine("Grace's house, inside:");
        int g = 0;
        foreach (var spot in Inside)
        {
            g++;
            float lift = spot.up ? grace.firstFloorAt : 0f;
            Vector3 at = grace.World(spot.X, spot.Y) + Vector3.up * lift;
            Vector3 look = grace.World(spot.lookX, spot.lookY) + Vector3.up * (lift + 1.3f);
            yield return Shot(at, look, 0f, $"grace-{g:00}-{Safe(spot.what)}.png", spot.what);
        }
        // 4. Four doors again with the torch on, for the difference it makes.
        if (torch != null)
        {
            torch.Switch(true);
            report.AppendLine();
            report.AppendLine("With the torch on:");
            int t = 0;
            for (int i = 0; i < doors.Count && t < 4; i += Mathf.Max(1, doors.Count / 4))
            {
                StreetDoor door = doors[i];
                if (door == null) continue;
                t++;
                string house = HouseName(door.transform);
                yield return Shot(door.DoorwayPoint + door.Outward * DoorDistance, door.DoorwayPoint + Vector3.up * 1.2f, 0f,
                    $"torch-{t:00}-{Safe(house)}.png", $"{house}: the door from {DoorDistance:0} m, torch on");
            }
            torch.Switch(false);
        }
        mover.ScriptedInput = null;
        Finish();
    }

    // Ace moved to a spot (the capsule off for the move), looking at a point; a photo once the frame has settled.
    IEnumerator Shot(Vector3 feet, Vector3 lookAt, float pitchExtra, string file, string what)
    {
        // The floor under the spot, so Ace stands on it rather than in it: a short ray from waist height, so inside a
        // house it finds this storey's floor and never the one above (a first try cast from 2.5 m up and landed every
        // ground-floor spot on the first floor).
        if (Physics.Raycast(feet + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            feet.y = ground.point.y;
        float lift = capsule.height * .5f - capsule.center.y + capsule.skinWidth + .02f;
        bool wasOn = capsule.enabled;
        capsule.enabled = false;
        capsule.transform.position = feet + Vector3.up * lift;
        capsule.enabled = wasOn;
        Vector3 eye = feet + Vector3.up * 1.65f;
        Vector3 d = lookAt - eye;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg + pitchExtra;
        view.LookTo(yaw, pitch);
        yield return null;
        yield return new WaitForSeconds(.5f);
        view.LookTo(yaw, pitch);   // the capsule may have settled a little
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, file));
        yield return null;
        yield return new WaitForSeconds(.25f);
        photos++;
        report.AppendLine($"  {file}: {what}  (at {feet.x:0.0}, {feet.z:0.0}; looking {yaw:0}°, {pitch:0}°)");
    }

    static string HouseName(Transform door)
    {
        // The door's house: the nearest ancestor that isn't itself a door part.
        for (Transform t = door.parent; t != null; t = t.parent)
            if (t.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0) return t.name;
        return door.name;
    }

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        return sb.ToString().Replace("--", "-").Trim('-');
    }

    void Finish()
    {
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        Debug.Log($"[House close-ups] {photos} photos: {folder}\n{report}");
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
