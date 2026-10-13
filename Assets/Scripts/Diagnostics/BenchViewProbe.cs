using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ---------------------------------------------------------------------------
// THE BENCH CLOSE-UP'S VIEW: A PLAY CHECK (12 Oct 2026; a diagnostic probe the night before)
//
// Mansoor's playtest (12 Oct, with a controller): "when i place the phone down on the working station, the phone sits
// correctly only until i start working on it ... the phone was not in the view". The probe found why: the phone's job tag
// sat 70 cm along it, and the close-up measured the phone with it, so a customer's phone lay 31.5 cm off the middle of the
// mat, off the screen (claude/playtest-4-findings-and-proposals.md §1). Bench 3 never sees a customer's device: it sets a
// phone down by its own hand, its tag hidden. This check does what a customer's device goes through, and proves what the
// player would see (§1.3, point 5):
//
//   * every bench device (the phone, the pocket watch, Grace's camera), its job tag shown as a customer's is: carried to the
//     bench, set down, the close-up opened; it lies in the middle of the mat (within 5 cm of the table point) with all of
//     it on screen as it opens, a second later, at every quarter turn, and after it is turned over;
//   * the game measures it by its own solid parts (DeviceBounds, which the slab, the name over it, the "Fixed!" pop and the
//     screws' fall share): its box agrees with this check's own count of them, apart from the tag, within a centimetre;
//   * the bench's two cards (the checklist, the controls) are up and cover none of it; the checklist names the device, the
//     controls card names turning, flipping and taking it away;
//   * the second slot: a phone set down beside a watch already in the middle comes to the middle itself, the watch makes
//     room, and goes back when the close-up ends;
//   * with a controller (only with Unity in front, where a virtual pad's presses arrive): the controls card in the pad's
//     glyphs, and A uses, as RT does, and never takes the device away. (X taking it away needs a customer's device: the
//     game only lets Ace pick up a device someone owns. The Stations lab does it with a customer's.) And the stick swap
//     (GameSettings.SwapBenchSticks, 12 Oct) in the card's words: on, the left stick moves the hand and the right turns the
//     device; put back, as before. (Bench 3 measures the sticks themselves, swapped and not.)
//
// Fixit Fidget > Bench > Bench 5 - The close-up's view (lab, a check). Photos and report.txt go to
// Logs/BenchViewProbe/check-<time>/. Nothing is saved in the scene; the lab's own save only.
// ---------------------------------------------------------------------------
public sealed class BenchViewProbe : PlayLab
{
    public const string PendingKey = "FixitFidget.BenchViewProbe";

    protected override string Title => "Bench 5 - the close-up's view: a customer's device, every turn";
    protected override string Tag => "[Bench view check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Bench view check] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Bench view check (this Play session only)");
        go.AddComponent<BenchViewProbe>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BenchViewProbe",
            $"check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const string Phone = "Assets/AssetsPrefabs/PhoneRepair.prefab";
    const string Watch = "Assets/AssetsPrefabs/PocketWatch.prefab";
    const string Camera_ = "Assets/GraceShowcase/GraceReunionCamera.prefab";

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    CustomerSpawner spawner;
    bool spawnerWas = true;
    StationInteractable bench;
    readonly List<GameObject> made = new List<GameObject>();
    bool swapTouched, swapWas;   // the stick-swap setting as the player had it (put back in Restore)
    static readonly Color JobColour = new Color(.95f, .55f, .2f, 1f);

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null, "Ace is in the scene");
        if (interactor == null || view == null || carry == null || inspector == null) yield break;
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(bench != null && BenchStage.Instance != null, "the bench and its stage are in the scene");
        if (bench == null || BenchStage.Instance == null) yield break;
        Note($"Unity in front (a virtual pad's presses arrive): {(Application.isFocused ? "yes" : "no: the controller part is left out")}");

        // Every bench device, as a customer's (its tag shown where it has one).
        yield return Try(Phone, "phone", null);
        yield return Try(Watch, "watch", null);
        yield return Try(Camera_, "grace-camera", null);

        // The second slot: a watch already lying in the middle, then a customer's phone set down beside it.
        GameObject first = Make(Watch, out JobBase firstJob);
        if (firstJob != null)
        {
            carry.TryPickUp(firstJob);
            PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
            yield return Seconds(.6f);
            bench.Interact(interactor);
            yield return Seconds(.4f);
            Check(StationInteractable.BenchHolds(firstJob), "second slot: a watch set down first, in the middle");
            yield return Try(Phone, "phone-second-slot", firstJob);
            foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(firstJob);
            Destroy(first);
            made.Remove(first);
        }

        // The controller: A uses (it never takes the device away), X does. Only with Unity in front.
        if (Application.isFocused) yield return PadButtons();
        else Note("the controller's A and X at the bench: left out (Unity was in the background; the Stations lab also covers X)");
    }

    GameObject Make(string path, out JobBase job)
    {
        job = null;
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        if (prefab == null) { Check(false, "loads " + path); return null; }
        GameObject device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        DeviceFault fault = definition != null ? definition.GetFault(0) : null;
        if (definition == null || job == null || fault == null) { Check(false, path + " has a definition, a job and a first fault"); return device; }
        definition.ApplyFault(0);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = 0,
            faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 1, color = JobColour
        });
        // As CustomerBrain does for a customer's device: its job's tag shown.
        JobMarker marker = device.GetComponentInChildren<JobMarker>(true);
        if (marker != null) marker.Show(1, JobColour);
        return device;
    }

    IEnumerator Try(string path, string name, JobBase inTheMiddle)
    {
        Note($"---------- {name} ----------");
        GameObject device = Make(path, out JobBase job);
        if (device == null || job == null) yield break;
        yield return Frames(2);
        JobMarker marker = device.GetComponentInChildren<JobMarker>(true);
        Note(marker != null ? $"its job tag is shown ({(marker.gameObject.activeInHierarchy ? "drawn" : "not drawn")})" : "it has no job tag");

        carry.TryPickUp(job);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.8f);
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), $"{name}: set down on the bench");
        yield return Seconds(.6f);
        view.SetFirstPerson(false);
        yield return AfterBlend();
        Vector3 middleBefore = inTheMiddle != null ? inTheMiddle.transform.position : Vector3.zero;

        // The close-up, as E or A from above opens it (PlayerInteractor.WorkOn wants a customer to own the device: the check's
        // has none, so the lab's hand opens it, as Bench 3 does).
        bool opened = inspector.BeginInspection(job);
        Check(opened, $"{name}: the close-up opens on it");
        if (!opened) { Cleanup(device, job); yield break; }
        yield return AfterBlend();
        yield return Frames(2);
        Measure(name, "as it opens", job);
        yield return Seconds(1.1f);   // past the close-up's half-second re-measure
        Measure(name, "a second later", job);
        yield return Photo(name + "-1-close-up");
        if (inTheMiddle != null)
        {
            Check(inspector.MadeRoomFor == inTheMiddle.transform && (inTheMiddle.transform.position - middleBefore).magnitude > .05f,
                $"{name}: the watch in the middle made room for it ({(inTheMiddle.transform.position - middleBefore).magnitude * 100f:0} cm)");
        }

        // The game's own measure: the device's solid parts, the tag left out.
        bool game = DeviceBounds.TryGet(job.gameObject, out Bounds measured);
        bool own = Solids(job, out Bounds counted);
        Check(game && own && (measured.center - counted.center).magnitude < .01f && (measured.size - counted.size).magnitude < .01f,
            $"{name}: the game measures it by its own solid parts (its box {Cm(measured.size)}, this check's count {Cm(counted.size)}, " +
            $"middles {(measured.center - counted.center).magnitude * 100f:0.0} cm apart)");
        Vector3 top = FocusNameUI.TopOf(job.gameObject);
        Check(own && Mathf.Abs(top.y - counted.max.y) < .01f && new Vector2(top.x - counted.center.x, top.z - counted.center.z).magnitude < .01f,
            $"{name}: the name over it is pinned to its own top ({Cm(top - new Vector3(counted.center.x, counted.max.y, counted.center.z))} off)");

        // The bench's cards: up, and over none of it.
        yield return Frames(12);   // the cards refresh every 0.15 s
        Rect device2D = OnScreen(job);
        Check(BenchCards.Showing, $"{name}: the checklist and the controls card are up");
        Check(!BenchCards.StripOnScreen.Overlaps(device2D) && !BenchCards.CardOnScreen.Overlaps(device2D),
            $"{name}: the cards cover none of it (it {Px(device2D)}; the checklist {Px(BenchCards.StripOnScreen)}; the controls {Px(BenchCards.CardOnScreen)})");
        string list = Plain(BenchCards.ChecklistText), controls = Plain(BenchCards.ControlsText);
        string device_ = job.Record != null ? job.Record.deviceName.ToLowerInvariant() : "";
        Check(list.ToLowerInvariant().Contains(device_) && BenchCards.Steps != null && BenchCards.Steps.Any(step => step.current),
            $"{name}: the checklist names it and gives the step to do now: \"{list}\"");
        Check(controls.Contains("Turn it") && controls.Contains("Flip it over") && controls.Contains("Pick it up and step back"),
            $"{name}: the controls card says how to turn it, flip it and take it away: \"{controls}\"");

        // Every quarter turn, and turned over.
        foreach (float turn in new[] { 90f, 180f, -90f })
        {
            inspector.LayDown(turn);
            yield return Frames(3);
            Measure(name, $"turned to {turn:0}°", job);
            if (turn == 90f) yield return Photo(name + "-2-turned-a-quarter");
        }
        inspector.LayDown(0f);
        yield return Frames(2);
        float faceBefore = inspector.FaceUp;
        Check(inspector.FlipOver(), $"{name}: it turns over (Y, R)");
        yield return Await(() => !inspector.Flipping, 2f);
        yield return Frames(3);
        Check(inspector.FaceUp == -faceBefore, $"{name}: it lies on its other face");
        Measure(name, "turned over", job);
        yield return Photo(name + "-3-turned-over");

        inspector.CancelInspection();
        yield return AfterBlend();
        if (inTheMiddle != null)
            Check((inTheMiddle.transform.position - middleBefore).magnitude < .01f,
                $"{name}: the watch went back to the middle when the close-up ended ({(inTheMiddle.transform.position - middleBefore).magnitude * 100f:0.0} cm from where it lay)");
        Cleanup(device, job);
        yield return Frames(3);
    }

    // The controller at the bench (agreed 12 Oct): A uses, as RT does, and never takes the device away; X picks it up and
    // steps back.
    IEnumerator PadButtons()
    {
        Note("---------- the controller ----------");
        GameObject device = Make(Phone, out JobBase job);
        if (device == null || job == null) yield break;
        carry.TryPickUp(job);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.8f);
        bench.Interact(interactor);
        yield return Seconds(.6f);
        PlugInPad("Bench view check pad");
        PadHold(new GamepadState { leftStick = new Vector2(.6f, 0f) });   // a touch of the stick: the pad is what's in hand
        yield return Frames(4);
        PadRelease();
        yield return Frames(4);
        bool opened = inspector.BeginInspection(job);
        Check(opened, "pad: the close-up opens on a phone");
        if (!opened) { Cleanup(device, job); yield break; }
        yield return AfterBlend();
        yield return Seconds(.3f);
        Check(PadInput.UsingPad, "pad: the game sees the controller in hand");
        yield return Frames(12);
        string controls = Plain(BenchCards.ControlsText);
        Check(controls.Contains("Right stick") && controls.Contains("Left stick") && controls.Contains(PadInput.Label(PadButton.North) + "]  Flip it over")
              && controls.Contains(PadInput.Label(PadButton.West) + "]  Pick it up and step back"),
            $"pad: the controls card names the sticks, Y to flip and X to take it away: \"{controls}\"");
        yield return Photo("pad-1-controls-card");

        // The stick swap (GameSettings.SwapBenchSticks, 12 Oct), in the card's words; the sticks themselves are Bench 3's.
        swapWas = GameSettings.SwapBenchSticks;
        swapTouched = true;
        GameSettings.SwapBenchSticks = true;
        yield return Seconds(.35f);   // the cards refresh every 0.15 s
        controls = Plain(BenchCards.ControlsText);
        Check(controls.Contains("[Left stick]  Move your hand") && controls.Contains("[Right stick]  Turn it a quarter"),
            $"pad: with the sticks swapped the card says the left stick moves the hand and the right turns it: \"{controls}\"");
        yield return Photo("pad-1b-controls-card-sticks-swapped");
        GameSettings.SwapBenchSticks = swapWas;
        yield return Seconds(.35f);
        controls = Plain(BenchCards.ControlsText);
        Check(controls.Contains("[" + (swapWas ? "Left" : "Right") + " stick]  Move your hand"),
            $"pad: the setting put back as it was ({(swapWas ? "swapped" : "not swapped")}), the card follows: \"{controls}\"");
        yield return PadPress(GamepadButton.South);
        yield return Seconds(.3f);
        Check(inspector.IsHoldingItem && inspector.FocusedItem == job && !carry.Contains(job), "pad: A uses and leaves the phone in the close-up");
        Note("pad: X taking it away is the Stations lab's (it needs a customer's device: nobody owns this one)");
        PadRelease();
        yield return AfterBlend();
        Cleanup(device, job);
    }

    void Cleanup(GameObject device, JobBase job)
    {
        if (inspector.IsHoldingItem) inspector.CancelInspection();
        if (job != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        Destroy(device);   // a carried one leaves Ace's hands with it (PlayerCarry forgets what's gone)
        made.Remove(device);
    }

    // Where the device's own solid parts are in the close-up: their middle against the table point (within 5 cm), and all of
    // them on screen. The tag is left out of this count (by where it hangs), and so are sprites, lines and particles.
    void Measure(string name, string when, JobBase job)
    {
        Camera cam = Camera.main;
        if (!Solids(job, out Bounds b) || cam == null) { Check(false, $"{name}, {when}: something of it drawn to measure"); return; }
        Vector3 off = b.center - inspector.TableTop;
        int on = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 v = cam.WorldToViewportPoint(corner);
            if (v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) on++;
        }
        Vector3 c = cam.WorldToViewportPoint(b.center);
        bool centreOn = c.z > 0f && c.x >= 0f && c.x <= 1f && c.y >= 0f && c.y <= 1f;
        float flat = new Vector2(off.x, off.z).magnitude;
        Check(centreOn && on == 8 && flat < .05f,
            $"{name}, {when}: its middle {flat * 100f:0.0} cm from the middle of the mat, " +
            $"{(centreOn ? $"on screen at {c.x * 100f:0}% across, {c.y * 100f:0}% up" : $"OFF screen (viewport {c.x:0.00}, {c.y:0.00})")}, " +
            $"{on}/8 corners of it on screen ({b.size.x * 100f:0.0} x {b.size.y * 100f:0.0} x {b.size.z * 100f:0.0} cm; yaw {inspector.Yaw:0}, face {inspector.FaceUp:+0;-0})");
    }

    // The device's solid parts, counted here apart from the game's own measure: renderers drawn with a mesh, not under the tag.
    static bool Solids(JobBase job, out Bounds b)
    {
        b = default;
        bool any = false;
        JobMarker marker = job.GetComponentInChildren<JobMarker>(true);
        foreach (Renderer r in job.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is SpriteRenderer || r is LineRenderer || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (marker != null && r.transform.IsChildOf(marker.transform)) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (!(r is SkinnedMeshRenderer) && (mf == null || mf.sharedMesh == null)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // The device's box as it falls on the screen, in pixels.
    static Rect OnScreen(JobBase job)
    {
        Camera cam = Camera.main;
        if (cam == null || !Solids(job, out Bounds b)) return default;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 p = cam.WorldToScreenPoint(corner);
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static string Px(Rect r) => $"x {r.xMin:0}-{r.xMax:0}, y {r.yMin:0}-{r.yMax:0} px";

    static string Cm(Vector3 v) => $"({v.x * 100f:0.0}, {v.y * 100f:0.0}, {v.z * 100f:0.0}) cm";

    static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " / ");

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        view.LookTo(yaw, 10f);
    }

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    protected override void Restore()
    {
        PadRelease();
        if (swapTouched) GameSettings.SwapBenchSticks = swapWas;   // the player's setting, never the check's
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        foreach (GameObject g in made)
            if (g != null)
            {
                JobBase j = g.GetComponent<JobBase>();
                if (j != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(j);
                Destroy(g);
            }
        if (spawner != null) spawner.enabled = spawnerWas;
    }
}
